using System;
using System.IO;

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

            return root!.Accept(new ConverterVisitor(model))!.ToFullString();
        }
    }

    class ConverterVisitor : CSharpSyntaxRewriter
    {
        readonly SemanticModel SemanticModel;

        public ConverterVisitor(SemanticModel semanticModel) => SemanticModel = semanticModel;

        public override SyntaxNode? VisitAttribute(AttributeSyntax node)
        {
            switch (node.Name.ToString())
            {
                case "Introduced":
                    var platform = PlatformArgumentParser.Parse(node.ArgumentList!.Arguments[0].ToString());
                    if (platform != null)
                    {
                        var version = $"{node.ArgumentList!.Arguments[1]}.{node.ArgumentList!.Arguments[2]}";
                        var args = SyntaxFactory.ParseAttributeArgumentList($"(\"{platform}{version}\")");
                        return SyntaxFactory.Attribute(SyntaxFactory.ParseName("SupportedOSPlatform"), args);
                    }
                    break;
            }
            return node;
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
