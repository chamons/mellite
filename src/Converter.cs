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

            return root!.Accept(new ConverterVisitor(model))!.ToString();
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
                    var args = SyntaxFactory.ParseAttributeArgumentList("(\"macos10.0\")");
                    return SyntaxFactory.Attribute(SyntaxFactory.ParseName("SupportedOSPlatform"), args);
            }
            return node;
        }
    }
}
