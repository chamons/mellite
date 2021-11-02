using System;
using System.IO;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

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
                var leading = new List<SyntaxTrivia>();
                leading.AddRange(SyntaxFactory.ParseLeadingTrivia("#if !NET"));
                leading.AddRange(SyntaxFactory.ParseTrailingTrivia("\r\n"));
                leading.Add(SyntaxFactory.DisabledText(node.ToFullString()));
                leading.AddRange(SyntaxFactory.ParseLeadingTrivia("#else"));
                leading.AddRange(SyntaxFactory.ParseTrailingTrivia("\r\n"));
                // Copy existing attribute trivial to get tab'ed over
                leading.AddRange(node.GetLeadingTrivia());

                var trailing = new List<SyntaxTrivia>();
                trailing.AddRange(SyntaxFactory.ParseTrailingTrivia("\r\n"));
                trailing.AddRange(SyntaxFactory.ParseTrailingTrivia("#endif"));
                trailing.AddRange(SyntaxFactory.ParseTrailingTrivia("\r\n"));

                var netAttributeElements = SyntaxFactory.SeparatedList(createdAttributes, Enumerable.Repeat(SyntaxFactory.Token(SyntaxKind.CommaToken), Math.Max(createdAttributes.Count - 1, 0)));
                var netAttribute = SyntaxFactory.AttributeList(netAttributeElements).WithTriviaFrom(node);

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

                return SyntaxFactory.Attribute(SyntaxFactory.ParseName("SupportedOSPlatform"), args).WithTriviaFrom(node);
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
