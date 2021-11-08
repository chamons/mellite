using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
		readonly SemanticModel SemanticModel;

		public AttributeConverterVisitor (SemanticModel semanticModel) => SemanticModel = semanticModel;

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
		(SyntaxTriviaList, SyntaxTriviaList) SplitNodeTrivia (SyntaxNode node)
		{
			var newlines = new SyntaxTriviaList ();
			var rest = new SyntaxTriviaList ();

			// XXX - this could be more efficient if we find the split point and bulk copy
			bool foundSplit = false;
			foreach (var trivia in node.GetLeadingTrivia ().Reverse ()) {
				if (trivia.ToFullString () == "\r\n" || trivia.ToFullString () == "\n") {
					foundSplit = true;
				}

				if (foundSplit) {
					newlines = newlines.Add (trivia);
				} else {
					rest = rest.Add (trivia);
				}
			}
			return (new SyntaxTriviaList (newlines.Reverse ()), new SyntaxTriviaList (rest.Reverse ()));
		}

		public MemberDeclarationSyntax Apply (MemberDeclarationSyntax member)
		{
			// All availability attributes such as [Introduced (PlatformName.iOS, 6, 0), Introduced (PlatformName.MacOSX, 10, 0)] need to be collected
			var createdAttributes = new List<AttributeListSyntax> ();
			var existingAttributes = new List<AttributeSyntax> ();
			// As we have to create a large #if sequence, these must be processed together
			var deprecatedAttributesToProcess = new List<AttributeSyntax> ();
			var introducedAttributesToProcess = new List<AttributeSyntax> ();

			// Need to process trivia from first element to get proper tabbing and newline before...
			SyntaxTriviaList? newlineTrivia = null;
			SyntaxTriviaList? indentTrivia = null;
			foreach (var attributeList in member.AttributeLists) {
				if (newlineTrivia == null) {
					(newlineTrivia, indentTrivia) = SplitNodeTrivia (attributeList);
				}

				foreach (var attribute in attributeList.Attributes) {
					switch (attribute.Name.ToString ()) {
					case "Introduced": {
						introducedAttributesToProcess.Add (attribute);
						existingAttributes.Add (attribute);
						break;
					}
					case "Deprecated": {
						deprecatedAttributesToProcess.Add (attribute);
						existingAttributes.Add (attribute);
						break;
					}
					case "AttributeUsage":
					case "NoMac":
					case "NoiOS":
						// XXX - For now...
						break;
					default:
						throw new NotImplementedException ($"AttributeConverterVisitor came across mixed set of availability attributes and others: '{attribute.Name}'");
					}
				}
			}

			for (int i = 0; i < introducedAttributesToProcess.Count; ++i) {
				var attribute = introducedAttributesToProcess [i];
				var newNode = ProcessSupportedAvailabilityNode (attribute);
				if (newNode != null) {
					var newAttribute = CreateAttributeList (newNode);
					if (i != introducedAttributesToProcess.Count - 1) {
						newAttribute = newAttribute.WithTrailingTrivia (SyntaxFactory.ParseTrailingTrivia ("\r\n").AddRange (indentTrivia));
					}
					createdAttributes.Add (newAttribute);
				}
			}

			if (deprecatedAttributesToProcess.Count > 0) {
				foreach (var newNode in ProcessDeprecatedNode (deprecatedAttributesToProcess, indentTrivia)) {
					createdAttributes.Add (newNode);
				}
			}

			if (newlineTrivia != null && indentTrivia != null) {
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
				leading.AddRange (newlineTrivia);
				leading.AddRange (SyntaxFactory.ParseLeadingTrivia ("#if NET"));
				leading.AddRange (SyntaxFactory.ParseTrailingTrivia ("\r\n"));
				leading.AddRange (indentTrivia);

				var trailing = new List<SyntaxTrivia> ();
				trailing.AddRange (SyntaxFactory.ParseTrailingTrivia ("\r\n"));
				trailing.AddRange (SyntaxFactory.ParseLeadingTrivia ("#else"));
				trailing.AddRange (SyntaxFactory.ParseTrailingTrivia ("\r\n"));

				foreach (var attribute in existingAttributes) {
					trailing.Add (SyntaxFactory.DisabledText (CreateAttributeList (attribute).WithLeadingTrivia (indentTrivia).ToFullString ()));
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

				SyntaxList<AttributeListSyntax> finalAttributeLists = new SyntaxList<AttributeListSyntax> (finalAttributes);
				return member.WithAttributeLists (finalAttributeLists);
			}
			return member;
		}

		AttributeListSyntax CreateAttributeList (AttributeSyntax createdAttribute)
		{
			createdAttribute = createdAttribute.WithName (createdAttribute.Name.WithTrailingTrivia (SyntaxFactory.ParseLeadingTrivia (" ")));
			var netAttributeElements = SyntaxFactory.SeparatedList (new List<AttributeSyntax> () { createdAttribute }, Enumerable.Repeat (SyntaxFactory.Token (SyntaxKind.CommaToken), 0));
			return SyntaxFactory.AttributeList (netAttributeElements);
		}

		public override SyntaxNode? VisitPropertyDeclaration (PropertyDeclarationSyntax node)
		{
			return Apply (node);
		}

		public override SyntaxNode? VisitMethodDeclaration (MethodDeclarationSyntax node)
		{
			return Apply (node);
		}

		public override SyntaxNode? VisitClassDeclaration (ClassDeclarationSyntax node)
		{
			var processedNode = (ClassDeclarationSyntax?) base.VisitClassDeclaration (node);
			if (processedNode != null) {
				return Apply (processedNode);
			}
			return null;
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

		List<AttributeListSyntax> ProcessDeprecatedNode (List<AttributeSyntax> nodes, SyntaxTriviaList? indentTrivia)
		{
			var returnNodes = new List<AttributeListSyntax> ();

			// Filter any attributes that don't line up on NET6, such as watch first
			nodes = nodes.Where (n => PlatformArgumentParser.ParseDefine (n.ArgumentList!.Arguments [0].ToString ()) != null).ToList ();

			// Add all of the deprecated as unsupported in net6
			for (int i = 0; i < nodes.Count; i++) {
				var unsupported = ProcessUnsupportedAvailabilityNode (nodes [i])!;
				AttributeListSyntax attribute = CreateAttributeList (unsupported);
				// Indent if not first
				if (i != 0) {
					attribute = attribute.WithLeadingTrivia (indentTrivia);
				}
				// Add newline at end of all but last
				if (i != nodes.Count - 1) {
					attribute = attribute.WithTrailingTrivia (SyntaxFactory.ParseTrailingTrivia ("\r\n"));
				}
				returnNodes.Add (attribute);
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
					leading.Add (SyntaxFactory.DisabledText (CreateAttributeList (CreateObsoleteAttribute (node)).WithLeadingTrivia (indentTrivia).ToFullString ()));
				}
			}
			if (indentTrivia != null) {
				leading.AddRange (indentTrivia);
			}

			// Generate #endif after attribute
			var trailing = new List<SyntaxTrivia> ();
			trailing.AddRange (SyntaxFactory.ParseTrailingTrivia ("\r\n"));
			trailing.AddRange (SyntaxFactory.ParseLeadingTrivia ("#endif"));

			// Create the actual attribute and add it to the list returned
			returnNodes.Add (CreateAttributeList (CreateObsoleteAttribute (nodes.Last ())).WithLeadingTrivia (leading).WithTrailingTrivia (trailing));
			return returnNodes;
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

	public static class PlatformArgumentParser {
		public static string? Parse (string s)
		{
			switch (s) {
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

		public static string? ParseDefine (string s)
		{
			switch (s) {
			case "PlatformName.MacOSX":
				return "MONOMAC";
			case "PlatformName.iOS":
				return "IOS";
			case "PlatformName.TvOS":
				return "TVOS";
			case "PlatformName.MacCatalyst":
				return "__MACCATALYST__";
			case "PlatformName.None":
			case "PlatformName.WatchOS":
			case "PlatformName.UIKitForMac":
			default:
				return null;
			}
		}
	}
}
