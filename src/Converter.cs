using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace mellite
{
    public static class Converter
    {
        public static void ConvertFile(string path)
        {
            var text = ConvertText(File.ReadAllText(path));
            File.WriteAllText(path, text);
        }

        public static string ConvertText(string text)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(text);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            var compilation = CSharpCompilation.Create("ConvertAssembly")
                .AddReferences(MetadataReference.CreateFromFile(typeof(string).Assembly.Location)).AddSyntaxTrees(tree);
            SemanticModel model = compilation.GetSemanticModel(tree);

            return root!.Accept(new AttributeConverterVisitor(model))!.ToFullString();
        }
    }

    class AttributeConverterVisitor : CSharpSyntaxRewriter
    {
        readonly SemanticModel SemanticModel;

        public AttributeConverterVisitor(SemanticModel semanticModel) => SemanticModel = semanticModel;

        // In this example:
        //  [Introduced (PlatformName.MacOSX, 10, 0)]
        //  public void Foo () {{}}
        //
        //  [Introduced (PlatformName.iOS, 6, 0)]
        //  public void Bar () {{}}
        // Bar has two elements in its leading trivia: Newline and Tab
        // The newline being between the Foo and Bar declaration
        // and the Tab being the indent of Bar
        // We want to copy just the later (Tab) to the synthesized attributes
        // and put the newline BEFORE the #if if
        // So split the trivia to everything before and including the last newline and everything else
        (SyntaxTriviaList, SyntaxTriviaList) SplitNodeTrivia(SyntaxNode node)
        {
            var newlines = new SyntaxTriviaList();
            var rest = new SyntaxTriviaList();

            // XXX - this could be more efficient if we find the split point and bulk copy
            bool foundSplit = false;
            foreach (var trivia in node.GetLeadingTrivia().Reverse())
            {
                if (trivia.ToFullString() == "\r\n")
                {
                    foundSplit = true;
                }

                if (foundSplit)
                {
                    newlines = newlines.Add(trivia);
                }
                else
                {
                    rest = rest.Add(trivia);
                }
            }
            return (new SyntaxTriviaList(newlines.Reverse()), new SyntaxTriviaList(rest.Reverse()));
        }

        public MemberDeclarationSyntax Apply(MemberDeclarationSyntax member)
        {
            // All availability attributes such as [Introduced (PlatformName.iOS, 6, 0), Introduced (PlatformName.MacOSX, 10, 0)] need to be collected
            var createdAttributes = new List<AttributeSyntax>();
            var existingAttributes = new List<AttributeSyntax>();

            // Need to process trivia from first element to get proper tabbing and newline before...
            SyntaxTriviaList? newlineTrivia = null;
            SyntaxTriviaList? indentTrivia = null;
            foreach (var attributeList in member.AttributeLists)
            {
                if (newlineTrivia == null)
                {
                    (newlineTrivia, indentTrivia) = SplitNodeTrivia(attributeList);
                }

                foreach (var attribute in attributeList.Attributes)
                {
                    switch (attribute.Name.ToString())
                    {
                        case "Introduced":
                            var newNode = ProcessAvailabilityNode(attribute);
                            if (newNode != null)
                            {
                                createdAttributes.Add(newNode);
                                existingAttributes.Add(attribute);
                            }
                            break;
                        case "AttributeUsage":
                            // XXX - For now...
                            break;
                        default:
                            throw new NotImplementedException($"AttributeConverterVisitor came across mixed set of availability attributes and others: '{attribute.Name}'");
                    }
                }
            }

            if (newlineTrivia != null && indentTrivia != null)
            {
                List<AttributeListSyntax> finalAttributes = new List<AttributeListSyntax>();

                // We want to generate:
                // #if !NET
                // EXISTING_ATTRIBUTES
                // #else
                // CONVERTED_ATTRIBUTES
                // #endif
                // The #if, all EXISTING_ATTRIBUTES, and the #else are all leading trivia of
                // the first CONVERTED_ATTRIBUTE, and the #endif is trailing of the last one
                // Each attribute will need indentTrivia to be tabbed over enough
                var leading = new List<SyntaxTrivia>();
                leading.AddRange(newlineTrivia);
                leading.AddRange(SyntaxFactory.ParseLeadingTrivia("#if !NET"));
                leading.AddRange(SyntaxFactory.ParseTrailingTrivia("\r\n"));
                foreach (var attribute in existingAttributes)
                {
                    leading.Add(SyntaxFactory.DisabledText(CreateAttributeList(attribute).WithLeadingTrivia(indentTrivia).ToFullString()));
                    leading.AddRange(SyntaxFactory.ParseTrailingTrivia("\r\n"));
                }
                leading.AddRange(SyntaxFactory.ParseLeadingTrivia("#else"));
                leading.AddRange(SyntaxFactory.ParseTrailingTrivia("\r\n"));
                leading.AddRange(indentTrivia);

                var trailing = new List<SyntaxTrivia>();
                trailing.AddRange(SyntaxFactory.ParseTrailingTrivia("\r\n"));
                trailing.AddRange(SyntaxFactory.ParseTrailingTrivia("#endif"));
                trailing.AddRange(SyntaxFactory.ParseTrailingTrivia("\r\n"));

                for (int i = 0; i < createdAttributes.Count; i += 1)
                {
                    var finalAttribute = CreateAttributeList(createdAttributes[i]).WithLeadingTrivia(indentTrivia).WithTrailingTrivia(SyntaxFactory.ParseTrailingTrivia("\r\n"));
                    if (i == 0)
                    {
                        finalAttribute = finalAttribute.WithLeadingTrivia(leading);
                    }
                    if (i == createdAttributes.Count - 1)
                    {
                        finalAttribute = finalAttribute.WithTrailingTrivia(trailing);
                    }
                    finalAttributes.Add(finalAttribute);
                }

                SyntaxList<AttributeListSyntax> finalAttributeLists = new SyntaxList<AttributeListSyntax>(finalAttributes);
                return member.WithAttributeLists(finalAttributeLists);
            }
            return member;
        }

        AttributeListSyntax CreateAttributeList(AttributeSyntax createdAttribute)
        {
            var netAttributeElements = SyntaxFactory.SeparatedList(new List<AttributeSyntax>() { createdAttribute }, Enumerable.Repeat(SyntaxFactory.Token(SyntaxKind.CommaToken), 0));
            return SyntaxFactory.AttributeList(netAttributeElements);
        }

        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            return Apply(node);
        }

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            return Apply(node);
        }

        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var processedNode = (ClassDeclarationSyntax?)base.VisitClassDeclaration(node);
            if (processedNode != null)
            {
                return Apply(processedNode);
            }
            return null;
        }

        AttributeSyntax? ProcessAvailabilityNode(AttributeSyntax node)
        {
            var platform = PlatformArgumentParser.Parse(node.ArgumentList!.Arguments[0].ToString());
            if (platform != null)
            {
                var version = $"{node.ArgumentList!.Arguments[1]}.{node.ArgumentList!.Arguments[2]}";
                var args = SyntaxFactory.ParseAttributeArgumentList($"(\"{platform}{version}\")");
                // Do not WithTriviaFrom here as we copied it over in VisitAttributeList with split
                return SyntaxFactory.Attribute(SyntaxFactory.ParseName("SupportedOSPlatform"), args);
            }
            return null;
        }
    }

    public static class PlatformArgumentParser
    {
        public static string? Parse(string s)
        {
            switch (s)
            {
                case "PlatformName.MacOSX":
                    return "macos";
                case "PlatformName.iOS":
                    return "ios";
                case "PlatformName.TvOS":
                    return "tvos";
                case "PlatformName.MacCatalyst":
                    return "maccatalyst";
                case "PlatformName.None":
                case "PlatformName.WatchOS":
                case "PlatformName.UIKitForMac":
                default:
                    return null;
            }
        }
    }
}
