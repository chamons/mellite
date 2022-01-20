using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using mellite.Utilities;

namespace mellite {
	class Converter {
		public static string Convert (string text, List<string> defines, string? verboseConditional, AssemblyHarvestInfo? assemblyInfo)
		{
			var uniqueDefines = (new DefineParser (verboseConditional)).FindUniqueDefinesThatCoverAll (text, false);
			if (uniqueDefines == null) {
				throw new InvalidOperationException ("Converter was unable to find unique defines that cover all attributes. Run --strip-verify and fix first.");
			}

			CSharpParseOptions options = new CSharpParseOptions (preprocessorSymbols: defines.Union (uniqueDefines));
			SyntaxTree tree = CSharpSyntaxTree.ParseText (text, options);

			CompilationUnitSyntax root = tree.GetCompilationUnitRoot ();

			root = (CompilationUnitSyntax) root!.Accept (new AttributeConverterVisitor (assemblyInfo))!;
			return root!.ToFullString ();
		}
	}


	class AttributeConverterVisitor : CSharpSyntaxRewriter {
		AssemblyHarvestInfo? AssemblyInfo;

		public AttributeConverterVisitor (AssemblyHarvestInfo? assemblyInfo)
		{
			AssemblyInfo = assemblyInfo;
		}

		public override SyntaxNode? VisitPropertyDeclaration (PropertyDeclarationSyntax node) => Apply (node, node.Parent as BaseTypeDeclarationSyntax);
		public override SyntaxNode? VisitMethodDeclaration (MethodDeclarationSyntax node) => Apply (node, node.Parent as BaseTypeDeclarationSyntax);
		public override SyntaxNode? VisitEventDeclaration (EventDeclarationSyntax node) => Apply (node, node.Parent as BaseTypeDeclarationSyntax);
		public override SyntaxNode? VisitFieldDeclaration (FieldDeclarationSyntax node) => Apply (node, node.Parent as BaseTypeDeclarationSyntax);

		public override SyntaxNode? VisitEnumMemberDeclaration (EnumMemberDeclarationSyntax node) => Apply (node, node.Parent as BaseTypeDeclarationSyntax);
		public override SyntaxNode? VisitConstructorDeclaration (ConstructorDeclarationSyntax node) => Apply (node, node.Parent as BaseTypeDeclarationSyntax);
		public override SyntaxNode? VisitInterfaceDeclaration (InterfaceDeclarationSyntax node) => Apply (node, node.Parent as BaseTypeDeclarationSyntax);

		public override SyntaxNode? VisitEnumDeclaration (EnumDeclarationSyntax node)
		{
			// Need to first call base so properties/methods are visited
			var processedNode = (EnumDeclarationSyntax?) base.VisitEnumDeclaration (node);
			if (processedNode != null) {
				return Apply (processedNode, null);
			}
			return null;
		}

		public override SyntaxNode? VisitStructDeclaration (StructDeclarationSyntax node)
		{
			// Need to first call base so properties/methods are visited
			var processedNode = (StructDeclarationSyntax?) base.VisitStructDeclaration (node);
			if (processedNode != null) {
				return Apply (processedNode, null);
			}
			return null;
		}

		public override SyntaxNode? VisitClassDeclaration (ClassDeclarationSyntax node)
		{
			// Need to first call base so properties/methods are visited
			var processedNode = (ClassDeclarationSyntax?) base.VisitClassDeclaration (node);
			if (processedNode != null) {
				return Apply (processedNode, null);
			}
			return null;
		}

		// An example of desired behavior - https://github.com/xamarin/xamarin-macios/blob/main/src/AudioUnit/AudioComponentDescription.cs#L166
		public MemberDeclarationSyntax Apply (MemberDeclarationSyntax member, BaseTypeDeclarationSyntax? parent)
		{
			HarvestedMemberInfo info = AttributeHarvester.Process (member, parent, AssemblyInfo);

			var createdAttributes = new List<AttributeListSyntax> ();
			// Some general rules for trivia in created nodes
			// Assume you are at the beginning (not indented) of a line and leave a newline when completed with your section (no indend)
			createdAttributes.AddRange (ProcessIntroduced (info));
			createdAttributes.AddRange (ProcessDeprecated (info));
			createdAttributes.AddRange (ProcessUnavailable (info));
			createdAttributes.AddRange (ProcessObsolete (info));

			return member.WithAttributeLists (new SyntaxList<AttributeListSyntax> (GenerateFinalAttributes (info, createdAttributes)));
		}

		List<AttributeListSyntax> ProcessIntroduced (HarvestedMemberInfo info)
		{
			if (info.IntroducedAttributesToProcess.Count == 0) {
				return new List<AttributeListSyntax> ();
			}

			var createdAttributes = new List<AttributeListSyntax> ();
			for (int i = 0; i < info.IntroducedAttributesToProcess.Count; ++i) {
				var attribute = info.IntroducedAttributesToProcess [i];
				var newNode = ProcessSupportedAvailabilityNode (attribute);
				if (newNode != null) {
					var newAttribute = newNode.ToAttributeList ().WithLeadingTrivia (info.IndentTrivia).WithTrailingTrivia (TriviaConstants.Newline);
					createdAttributes.Add (newAttribute);
				}
			}
			return createdAttributes;
		}

		List<AttributeListSyntax> ProcessDeprecated (HarvestedMemberInfo info)
		{
			if (info.DeprecatedAttributesToProcess.Count == 0) {
				return new List<AttributeListSyntax> ();
			}

			var createdAttributes = new List<AttributeListSyntax> ();

			// Add all of the deprecated as unsupported in net6
			createdAttributes.AddRange (AddAllAsUnsupported (info, info.DeprecatedAttributesToProcess));
			createdAttributes.AddRange (AddConditionalObsoleteGrid (info, info.DeprecatedAttributesToProcess));
			return createdAttributes;
		}

		List<AttributeListSyntax> ProcessUnavailable (HarvestedMemberInfo info)
		{
			if (info.UnavailableAttributesToProcess.Count == 0) {
				return new List<AttributeListSyntax> ();
			}

			return AddAllAsUnsupported (info, info.UnavailableAttributesToProcess);
		}

		List<AttributeListSyntax> ProcessObsolete (HarvestedMemberInfo info)
		{
			if (info.ObsoleteAttributesToProcess.Count == 0) {
				return new List<AttributeListSyntax> ();
			}
			return AddConditionalObsoleteGrid (info, info.ObsoleteAttributesToProcess);
		}

		List<AttributeListSyntax> AddConditionalObsoleteGrid (HarvestedMemberInfo info, IList<AttributeSyntax> nodes)
		{
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
				var define = PlatformArgumentParser.GetDefineFromNode (node);
				leading.AddRange (SyntaxFactory.ParseLeadingTrivia ($"#{(i == 0 ? "if" : "elif")} {define}"));
				leading.AddRange (TriviaConstants.Newline);
				if (i != nodes.Count - 1) {
					leading.Add (SyntaxFactory.DisabledText (CreateObsoleteAttribute (node).ToAttributeList ().WithLeadingTrivia (info.IndentTrivia).ToFullString ()));
					leading.AddRange (TriviaConstants.Newline);
				}
			}
			leading.AddRange (info.IndentTrivia);

			// Generate #endif after attribute
			var trailing = new List<SyntaxTrivia> ();
			trailing.AddRange (TriviaConstants.Newline);
			trailing.AddRange (TriviaConstants.EndIf);
			trailing.AddRange (TriviaConstants.Newline);

			// Create the actual attribute and add it to the list returned
			var finalAttribute = CreateObsoleteAttribute (nodes.Last ()).ToAttributeList ().WithLeadingTrivia (leading).WithTrailingTrivia (trailing);
			return new List<AttributeListSyntax> { finalAttribute };
		}

		List<AttributeListSyntax> AddAllAsUnsupported (HarvestedMemberInfo info, IList<AttributeSyntax> nodes)
		{
			var createdAttributes = new List<AttributeListSyntax> ();

			for (int i = 0; i < nodes.Count; i++) {
				var unsupported = ProcessUnsupportedAvailabilityNode (nodes [i])!;
				createdAttributes.Add (unsupported.ToAttributeList ().WithLeadingTrivia (info.IndentTrivia).WithTrailingTrivia (TriviaConstants.Newline));
			}
			return createdAttributes;
		}

		List<AttributeListSyntax> GenerateFinalAttributes (HarvestedMemberInfo info, IList<AttributeListSyntax> createdAttributes)
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

			// That is UNLESS EXISTING_ATTRIBUTES has zero elements but CONVERTED_ATTRIBUTES has some, which is possible with watch for example
			// Then we generate the existing attributes with #if !NET and #endif only
			bool newLinesAdded;
			if (createdAttributes.Count == 0 && info.ExistingAvailabilityAttributes.Count > 0) {
				var leading = new List<SyntaxTrivia> ();

				// These blocks look just like the else case block, but are different IfNot vs If, EndIf vs Else 
				leading.AddRange (info.NewlineTrivia);
				leading.AddRange (TriviaConstants.IfNotNet);
				leading.AddRange (TriviaConstants.Newline);

				var trailing = new List<SyntaxTrivia> ();
				trailing.AddRange (TriviaConstants.EndIf);
				trailing.AddRange (TriviaConstants.Newline);

				var attributes = info.ExistingAvailabilityAttributes.Select (x => x.ToAttributeList ().WithLeadingTrivia (info.IndentTrivia).WithTrailingTrivia (TriviaConstants.Newline)).ToList ();
				newLinesAdded = AddToListWithLeadingTrailing (finalAttributes, attributes, leading, trailing);
			} else {
				var leading = new List<SyntaxTrivia> ();
				leading.AddRange (info.NewlineTrivia);
				leading.AddRange (TriviaConstants.IfNet);
				leading.AddRange (TriviaConstants.Newline);

				var trailing = new List<SyntaxTrivia> ();
				trailing.AddRange (TriviaConstants.Else);
				trailing.AddRange (TriviaConstants.Newline);

				foreach (var attribute in info.ExistingAvailabilityAttributes) {
					// Roslyn harvested existing attributes don't follow our "every node should own the newline to the next line" rule
					// So add info.IndentTrivia here by hand
					trailing.Add (SyntaxFactory.DisabledText (attribute.ToAttributeList ().WithLeadingTrivia (info.IndentTrivia).ToFullString ()));
					trailing.AddRange (TriviaConstants.Newline);
				}
				trailing.AddRange (TriviaConstants.EndIf);
				trailing.AddRange (TriviaConstants.Newline);

				newLinesAdded = AddToListWithLeadingTrailing (finalAttributes, createdAttributes, leading, trailing);
			}

			for (int i = 0; i < info.NonAvailabilityAttributes.Count; ++i) {
				// Roslyn harvested existing attributes don't follow our "every node should own the newline to the next line" rule
				// So add info.IndentTrivia here by hand
				var attribute = info.NonAvailabilityAttributes [i].ToAttributeList ();
				attribute = attribute.WithLeadingTrivia (attribute.GetLeadingTrivia ().AddRange (info.IndentTrivia));
				attribute = attribute.WithTrailingTrivia (attribute.GetTrailingTrivia ().AddRange (TriviaConstants.Newline));
				// If we added zero items above, then we are responsible to prepend info.NewlineTrivia to our first element 
				if (i == 0 && !newLinesAdded) {
					attribute = attribute.WithLeadingTrivia (info.NewlineTrivia.AddRange (attribute.GetLeadingTrivia ()));
				}
				finalAttributes.Add (attribute);
			}

			// Now that we have the final list, apply any NonWhitespaceTrivia to the very first attribute in the list
			if (info.NonWhitespaceTrivia.Any () && finalAttributes.Any ()) {
				finalAttributes [0] = finalAttributes [0].WithLeadingTrivia (info.NonWhitespaceTrivia.AddRange (finalAttributes [0].GetLeadingTrivia ()));
			}

			return finalAttributes;
		}

		// Add each attribute to finalAttributes, appending leading trivia to first and trailing to last element
		bool AddToListWithLeadingTrailing (List<AttributeListSyntax> finalAttributes, IList<AttributeListSyntax> attributes, List<SyntaxTrivia> leading, List<SyntaxTrivia> trailing)
		{
			for (int i = 0; i < attributes.Count; i += 1) {
				var attribute = attributes [i];

				if (i == 0) {
					// This order matters in some cases, but not in the trailing case
					leading.AddRange (attribute.GetLeadingTrivia ());
					attribute = attribute.WithLeadingTrivia (leading);
				}
				if (i == attributes.Count - 1) {
					attribute = attribute.WithTrailingTrivia (attribute.GetTrailingTrivia ().AddRange (trailing));
				}
				finalAttributes.Add (attribute);
			}
			return attributes.Count > 0;
		}

		AttributeSyntax? ProcessSupportedAvailabilityNode (AttributeSyntax node)
		{
			var platform = PlatformArgumentParser.GetPlatformFromNode (node);
			if (platform != null) {
				var args = SyntaxFactory.ParseAttributeArgumentList ($"(\"{platform}{PlatformArgumentParser.GetVersionFromNode (node)}\")");
				// Do not WithTriviaFrom here as we copied it over in VisitAttributeList with split
				return SyntaxFactory.Attribute (SyntaxFactory.ParseName ("SupportedOSPlatform"), args);
			}
			return null;
		}

		AttributeSyntax? ProcessUnsupportedAvailabilityNode (AttributeSyntax node)
		{
			var platform = PlatformArgumentParser.GetPlatformFromNode (node);
			if (platform != null) {
				if (node.ArgumentList?.Arguments.Count > 0) {
					var version = PlatformArgumentParser.GetVersionFromNode (node);
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
			var platform = PlatformArgumentParser.GetPlatformFromNode (node);
			var version = PlatformArgumentParser.GetVersionFromNode (node);

			string messageLeader = "";
			string message = "";
			if (node.ArgumentList?.Arguments.Count > 3) {
				var lastArg = node.ArgumentList!.Arguments.Last ().ToString ();
				if (lastArg.EndsWith ("\"")) {
					// Most messages are quoted strings
					if (lastArg.StartsWith ("message:")) {
						// Skip 10 - sizeof("message: \"") and last "
						message = " " + lastArg [10..^1];
					} else if (lastArg.StartsWith ("message :")) {
						// Skip 11 - sizeof("message : \"") and last "
						message = " " + lastArg [11..^1];
					}
					// Make the first non-space lower case
					if (!String.IsNullOrEmpty (message)) {
						message = " " + char.ToLower (message [1]) + message.Substring (2);
					}
				} else {
					// But some rare ones are constant variables
					if (lastArg.StartsWith ("message:")) {
						// Skip 10 - sizeof(message: ")
						message = " " + lastArg [9..];
					} else if (lastArg.StartsWith ("message :")) {
						// Skip 11 - sizeof(message : ")
						message = " " + lastArg [10..];
					}
					messageLeader = "[Verify (\"Constants in descriptions are not\")]";
				}
			}

			var messageArgs = $"{messageLeader}\"Starting with {platform}{version}{message}{(message.EndsWith (".") ? "" : ".")}\"";
			var args = SyntaxFactory.ParseAttributeArgumentList ($"({messageArgs}, DiagnosticId = \"BI1234\", UrlFormat = \"https://github.com/xamarin/xamarin-macios/wiki/Obsolete\")");
			return SyntaxFactory.Attribute (SyntaxFactory.ParseName ("Obsolete"), args);
		}
	}
}
