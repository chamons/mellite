using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using mellite.Utilities;

namespace mellite {
	public static class Converter {
		public static void ConvertFile (string path)
		{
			var text = ConvertText (File.ReadAllText (path));
			File.WriteAllText (path, text);
		}

		public static string ConvertText (string text)
		{
			SyntaxTree tree = CSharpSyntaxTree.ParseText (text);
			CompilationUnitSyntax root = tree.GetCompilationUnitRoot ();

			var compilation = CSharpCompilation.Create ("ConvertAssembly")
				.AddReferences (MetadataReference.CreateFromFile (typeof (string).Assembly.Location)).AddSyntaxTrees (tree);
			SemanticModel model = compilation.GetSemanticModel (tree);

			return root!.Accept (new AttributeConverterVisitor (model))!.ToFullString ();
		}
	}

	class AttributeConverterVisitor : CSharpSyntaxRewriter {
		public AttributeConverterVisitor (SemanticModel semanticModel) { }

		public override SyntaxNode? VisitPropertyDeclaration (PropertyDeclarationSyntax node) => Apply (node);
		public override SyntaxNode? VisitMethodDeclaration (MethodDeclarationSyntax node) => Apply (node);

		public override SyntaxNode? VisitClassDeclaration (ClassDeclarationSyntax node)
		{
			var processedNode = (ClassDeclarationSyntax?) base.VisitClassDeclaration (node);
			if (processedNode != null) {
				return Apply (processedNode);
			}
			return null;
		}

		public MemberDeclarationSyntax Apply (MemberDeclarationSyntax member)
		{
			HarvestedMemberInfo info = Harvester.Process (member);

			var createdAttributes = new List<AttributeListSyntax> ();
			createdAttributes.AddRange (ProcessIntroduced (info));
			createdAttributes.AddRange (ProcessDeprecated (info));

			return member.WithAttributeLists (new SyntaxList<AttributeListSyntax> (GenerateFinalAttributes (info, createdAttributes)));
		}

		List<AttributeListSyntax> GenerateFinalAttributes (HarvestedMemberInfo info, List<AttributeListSyntax> createdAttributes)
		{
			List<AttributeListSyntax> finalAttributes = new List<AttributeListSyntax> ();

			// We want to generate:
			// #if NET
			// CONVERTED_ATTRIBUTES
			// #else
			// EXISTING_ATTRIBUTES
			// #endif
			// The #if is leading trivia of the first CONVERTED_ATTRIBUTE,
			// and all the rest is the trailing of the last one
			// Each attribute will need indentTrivia to be tabbed over enough
			var leading = new List<SyntaxTrivia> ();
			leading.AddRange (info.NewlineTrivia);
			leading.AddRange (SyntaxFactory.ParseLeadingTrivia ("#if NET"));
			leading.AddRange (SyntaxFactory.ParseTrailingTrivia ("\r\n"));
			leading.AddRange (info.IndentTrivia);

			var trailing = new List<SyntaxTrivia> ();
			trailing.AddRange (SyntaxFactory.ParseTrailingTrivia ("\r\n"));
			trailing.AddRange (SyntaxFactory.ParseLeadingTrivia ("#else"));
			trailing.AddRange (SyntaxFactory.ParseTrailingTrivia ("\r\n"));

			foreach (var attribute in info.ExistingAttributes) {
				trailing.Add (SyntaxFactory.DisabledText (attribute.ToAttributeList ().WithLeadingTrivia (info.IndentTrivia).ToFullString ()));
				trailing.AddRange (SyntaxFactory.ParseTrailingTrivia ("\r\n"));
			}
			trailing.AddRange (SyntaxFactory.ParseTrailingTrivia ("#endif"));
			trailing.AddRange (SyntaxFactory.ParseTrailingTrivia ("\r\n"));

			for (int i = 0; i < createdAttributes.Count; i += 1) {
				var attribute = createdAttributes [i];

				if (i == 0) {
					attribute = attribute.WithLeadingTrivia (attribute.GetLeadingTrivia ().AddRange (leading));
				}
				if (i == createdAttributes.Count - 1) {
					attribute = attribute.WithTrailingTrivia (attribute.GetTrailingTrivia ().AddRange (trailing));
				}
				finalAttributes.Add (attribute);
			}

			return finalAttributes;
		}

		List<AttributeListSyntax> ProcessDeprecated (HarvestedMemberInfo info)
		{
			if (info.DeprecatedAttributesToProcess.Count == 0) {
				return new List<AttributeListSyntax> ();
			}
			var createdAttributes = new List<AttributeListSyntax> ();

			// Filter any attributes that don't line up on NET6, such as watch first
			var nodes = info.DeprecatedAttributesToProcess.Where (n => PlatformArgumentParser.ParseDefine (n.ArgumentList!.Arguments [0].ToString ()) != null).ToList ();

			// Add all of the deprecated as unsupported in net6
			for (int i = 0; i < nodes.Count; i++) {
				var unsupported = ProcessUnsupportedAvailabilityNode (nodes [i])!;
				AttributeListSyntax attribute = unsupported.ToAttributeList ();
				// Indent if not first
				if (i != 0) {
					attribute = attribute.WithLeadingTrivia (info.IndentTrivia);
				}
				// Add newline at end of all but last
				if (i != nodes.Count - 1) {
					attribute = attribute.WithTrailingTrivia (SyntaxFactory.ParseTrailingTrivia ("\r\n"));
				}
				createdAttributes.Add (attribute);
			}

			// Now build up with super attribute like this:
			// #if __MACCATALYST__
			// [Obsolete ("Starting with maccatalyst$5.$6 $11", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
			// #elif IOS
			// [Obsolete ("Starting with ios$1.$2 $11", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
			// #elif TVOS
			// [Obsolete ("Starting with tvos$3.$4 $11' instead.", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
			// #elif MONOMAC
			// [Obsolete ("Starting with macos$7.$8 $11", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
			// #endif
			// So in order of platform listed:
			// Generate ''#if define' then Obsolete for first element, '#elif define' then Obsolete for all but last
			// Attach ^ to last attribute as 

			// Generate #if block with disabled attributes, skipping the last attribute which this will be attached to
			var leading = new List<SyntaxTrivia> ();

			for (int i = 0; i < nodes.Count; i++) {
				var node = nodes [i];
				var define = PlatformArgumentParser.ParseDefine (node.ArgumentList!.Arguments [0].ToString ());
				leading.AddRange (SyntaxFactory.ParseTrailingTrivia ("\r\n"));
				leading.AddRange (SyntaxFactory.ParseLeadingTrivia ($"#{(i == 0 ? "if" : "elif")} {define}"));
				leading.AddRange (SyntaxFactory.ParseTrailingTrivia ("\r\n"));
				if (i != nodes.Count - 1) {
					leading.Add (SyntaxFactory.DisabledText (CreateObsoleteAttribute (node).ToAttributeList ().WithLeadingTrivia (info.IndentTrivia).ToFullString ()));
				}
			}
			leading.AddRange (info.IndentTrivia);

			// Generate #endif after attribute
			var trailing = new List<SyntaxTrivia> ();
			trailing.AddRange (SyntaxFactory.ParseTrailingTrivia ("\r\n"));
			trailing.AddRange (SyntaxFactory.ParseLeadingTrivia ("#endif"));

			// Create the actual attribute and add it to the list returned
			createdAttributes.Add (CreateObsoleteAttribute (nodes.Last ()).ToAttributeList ().WithLeadingTrivia (leading).WithTrailingTrivia (trailing));
			return createdAttributes;
		}

		List<AttributeListSyntax> ProcessIntroduced (HarvestedMemberInfo info)
		{
			var createdAttributes = new List<AttributeListSyntax> ();
			for (int i = 0; i < info.IntroducedAttributesToProcess.Count; ++i) {
				var attribute = info.IntroducedAttributesToProcess [i];
				var newNode = ProcessSupportedAvailabilityNode (attribute);
				if (newNode != null) {
					var newAttribute = newNode.ToAttributeList ();
					if (i != info.IntroducedAttributesToProcess.Count - 1) {
						newAttribute = newAttribute.WithTrailingTrivia (SyntaxFactory.ParseTrailingTrivia ("\r\n").AddRange (info.IndentTrivia));
					}
					createdAttributes.Add (newAttribute);
				}
			}
			return createdAttributes;
		}

		AttributeSyntax? ProcessSupportedAvailabilityNode (AttributeSyntax node)
		{
			var platform = PlatformArgumentParser.Parse (node.ArgumentList!.Arguments [0].ToString ());
			if (platform != null) {
				var version = $"{node.ArgumentList!.Arguments [1]}.{node.ArgumentList!.Arguments [2]}";
				var args = SyntaxFactory.ParseAttributeArgumentList ($"(\"{platform}{version}\")");
				// Do not WithTriviaFrom here as we copied it over in VisitAttributeList with split
				return SyntaxFactory.Attribute (SyntaxFactory.ParseName ("SupportedOSPlatform"), args);
			}
			return null;
		}

		AttributeSyntax? ProcessUnsupportedAvailabilityNode (AttributeSyntax node)
		{
			var platform = PlatformArgumentParser.Parse (node.ArgumentList!.Arguments [0].ToString ());
			if (platform != null) {
				if (node.ArgumentList.Arguments.Count > 0) {
					var version = $"{node.ArgumentList!.Arguments [1]}.{node.ArgumentList!.Arguments [2]}";
					var args = SyntaxFactory.ParseAttributeArgumentList ($"(\"{platform}{version}\")");
					// Do not WithTriviaFrom here as we copied it over in VisitAttributeList with split
					return SyntaxFactory.Attribute (SyntaxFactory.ParseName ("UnsupportedOSPlatform"), args);
				} else {
					var args = SyntaxFactory.ParseAttributeArgumentList ($"(\"{platform}\")");
					return SyntaxFactory.Attribute (SyntaxFactory.ParseName ("UnsupportedOSPlatform"), args);
				}
			}
			return null;
		}

		AttributeSyntax CreateObsoleteAttribute (AttributeSyntax node)
		{
			var platform = PlatformArgumentParser.Parse (node.ArgumentList!.Arguments [0].ToString ());
			var version = $"{node.ArgumentList!.Arguments [1]}.{node.ArgumentList!.Arguments [2]}";
			// Skip 10 - sizeof("message: \"") and last "
			var message = node.ArgumentList!.Arguments.Count > 3 ? $" {node.ArgumentList!.Arguments [3].ToString () [10..^1]}" : "";

			var args = SyntaxFactory.ParseAttributeArgumentList ($"(\"Starting with {platform}{version}{message}\", DiagnosticId = \"BI1234\", UrlFormat = \"https://github.com/xamarin/xamarin-macios/wiki/Obsolete\")");
			return SyntaxFactory.Attribute (SyntaxFactory.ParseName ("Obsolete"), args);
		}
	}
}
