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
        (SyntaxTriviaList, SyntaxTriviaList) SplitNodeTrivia(AttributeListSyntax node)
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

        public override SyntaxNode? VisitAttributeList(AttributeListSyntax node)
        {
            // All availability attributes such as [Introduced (PlatformName.iOS, 6, 0), Introduced (PlatformName.MacOSX, 10, 0)] need to be collected
            var createdAttributes = new List<AttributeSyntax>();

            foreach (var attribute in node.Attributes)
            {
                switch (attribute.Name.ToString())
                {
                    case "Introduced":
                        var newNode = ProcessAvailabilityNode(attribute);
                        if (newNode != null)
                        {
                            createdAttributes.Add(newNode);
                        }
                        break;
                }
            }

            // Assume every attribute in a list is availability or none. Will need to extend if not true assumption in our code base....
            if (createdAttributes.Count != 0 && createdAttributes.Count != node.Attributes.Count)
            {
                throw new NotImplementedException($"AttributeConverterVisitor came across mixed set of availability attributes and others: '{node.ToFullString()}'");
            }

            if (createdAttributes.Count > 0)
            {
                // We separate the final line's tabing with any newlines and such in between
                // And then output both the original and WithTriviaFrom without any trivia
                // Manually add the 'rest' before each, and the newlines only before the #if !NET
                var (newlines, rest) = SplitNodeTrivia(node);

                var leading = new List<SyntaxTrivia>();
                leading.AddRange(newlines);
                leading.AddRange(SyntaxFactory.ParseLeadingTrivia("#if !NET"));
                leading.AddRange(SyntaxFactory.ParseTrailingTrivia("\r\n"));

                // As we're splitting the trivia, only output the tab 'rest' trivia here before the old attribute...
                leading.AddRange(rest);
                leading.Add(SyntaxFactory.DisabledText(node.WithoutTrivia().ToFullString()));
                leading.AddRange(SyntaxFactory.ParseTrailingTrivia("\r\n"));
                leading.AddRange(SyntaxFactory.ParseLeadingTrivia("#else"));
                leading.AddRange(SyntaxFactory.ParseTrailingTrivia("\r\n"));

                // And also here before the new attribute.
                leading.AddRange(rest);

                var trailing = new List<SyntaxTrivia>();
                trailing.AddRange(SyntaxFactory.ParseTrailingTrivia("\r\n"));
                trailing.AddRange(SyntaxFactory.ParseTrailingTrivia("#endif"));
                trailing.AddRange(SyntaxFactory.ParseTrailingTrivia("\r\n"));

                var netAttributeElements = SyntaxFactory.SeparatedList(createdAttributes, Enumerable.Repeat(SyntaxFactory.Token(SyntaxKind.CommaToken), Math.Max(createdAttributes.Count - 1, 0)));
                // Do not WithTriviaFrom here as we copied it over in VisitAttributeList with split
                var netAttribute = SyntaxFactory.AttributeList(netAttributeElements);

                return netAttribute.WithLeadingTrivia(leading).WithTrailingTrivia(trailing);
            }
            else
            {
                return node;
            }
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
