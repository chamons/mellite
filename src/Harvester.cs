using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using mellite.Utilities;

namespace mellite {
	// In general 
	public class HarvestedAvailabilityInfo {
		public readonly AttributeSyntax Attribute;
		public readonly string? Target;
		public readonly string? Comment;
		public bool Implied;

		public HarvestedAvailabilityInfo (AttributeSyntax attribute, string? target, string? comment, bool implied = false)
		{
			Attribute = attribute;
			Target = target;
			Comment = comment;
			Implied = implied;
		}

		public HarvestedAvailabilityInfo (string name, string argList, bool implied = false)
		{
			var args = SyntaxFactory.ParseAttributeArgumentList ($"({argList})");
			Attribute = SyntaxFactory.Attribute (SyntaxFactory.ParseName (name), args);
			Implied = implied;
			Target = null;
			Comment = null;
		}

		public static HarvestedAvailabilityInfo From (AttributeSyntax attribute, AttributeListSyntax list)
		{
			string? comment = list.GetTrailingTrivia ().FirstOrDefault (x => x.Kind () == SyntaxKind.SingleLineCommentTrivia).ToString ();
			return new HarvestedAvailabilityInfo (attribute, list.Target?.ToString (), comment);
		}
	}

	public class HarvestedMemberInfo {
		public ReadOnlyCollection<HarvestedAvailabilityInfo> ExistingAvailabilityAttributes;
		public ReadOnlyCollection<HarvestedAvailabilityInfo> NonAvailabilityAttributes;

		public ReadOnlyCollection<AttributeSyntax> ImpliedIntroducedAttributesToProcess;
		public ReadOnlyCollection<AttributeSyntax> IntroducedAttributesToProcess;
		public ReadOnlyCollection<AttributeSyntax> DeprecatedAttributesToProcess;
		public ReadOnlyCollection<AttributeSyntax> UnavailableAttributesToProcess;
		public ReadOnlyCollection<AttributeSyntax> ObsoleteAttributesToProcess;

		// This is a bit of a hack. #if and such can be added to the trivia of our first node, and we want those, only once, before
		// any attributes. So store them off separate for now.
		public SyntaxTriviaList NonWhitespaceTrivia;

		public SyntaxTriviaList NewlineTrivia;
		public SyntaxTriviaList IndentTrivia;

		public HarvestedMemberInfo (List<HarvestedAvailabilityInfo> existingAvailabilityAttributes, List<HarvestedAvailabilityInfo> nonAvailabilityAttributes, List<AttributeSyntax> impliedIntroducedAttributesToProcess, List<AttributeSyntax> introducedAttributesToProcess, List<AttributeSyntax> deprecatedAttributesToProcess, List<AttributeSyntax> unavailableAttributesToProcess, List<AttributeSyntax> obsoleteAttributesToProcess, SyntaxTriviaList? nonWhitespaceTrivia, SyntaxTriviaList? newlineTrivia, SyntaxTriviaList? indentTrivia)
		{
			ExistingAvailabilityAttributes = existingAvailabilityAttributes.AsReadOnly ();
			NonAvailabilityAttributes = nonAvailabilityAttributes.AsReadOnly ();

			ImpliedIntroducedAttributesToProcess = impliedIntroducedAttributesToProcess.AsReadOnly ();
			IntroducedAttributesToProcess = introducedAttributesToProcess.AsReadOnly ();
			DeprecatedAttributesToProcess = deprecatedAttributesToProcess.AsReadOnly ();
			UnavailableAttributesToProcess = unavailableAttributesToProcess.AsReadOnly ();
			ObsoleteAttributesToProcess = obsoleteAttributesToProcess.AsReadOnly ();

			NonWhitespaceTrivia = nonWhitespaceTrivia ?? new SyntaxTriviaList ();
			NewlineTrivia = newlineTrivia ?? new SyntaxTriviaList ();
			IndentTrivia = indentTrivia ?? new SyntaxTriviaList ();
		}
	}

	// Harvest information from a given Roslyn node for later conversion
	public class AttributeHarvester {
		List<HarvestedAvailabilityInfo> ExistingAvailabilityAttributes = new List<HarvestedAvailabilityInfo> ();
		List<HarvestedAvailabilityInfo> NonAvailabilityAttributes = new List<HarvestedAvailabilityInfo> ();

		List<AttributeSyntax> IntroducedAttributesToProcess = new List<AttributeSyntax> ();
		List<AttributeSyntax> DeprecatedAttributesToProcess = new List<AttributeSyntax> ();
		List<AttributeSyntax> UnavailableAttributesToProcess = new List<AttributeSyntax> ();
		List<AttributeSyntax> ObsoleteAttributesToProcess = new List<AttributeSyntax> ();

		SyntaxTriviaList? NonWhitespaceTrivia = null;
		SyntaxTriviaList? NewlineTrivia = null;
		SyntaxTriviaList? IndentTrivia = null;

		bool HasAnyAvailability => IntroducedAttributesToProcess.Any () ||
				DeprecatedAttributesToProcess.Any () ||
				UnavailableAttributesToProcess.Any () ||
				ObsoleteAttributesToProcess.Any ();

		public HarvestedMemberInfo Process (MemberDeclarationSyntax member, MemberDeclarationSyntax? parent, AssemblyHarvestInfo? assemblyInfo)
		{
			ProcessAttributesOnMember (member);

			// Platforms that are explicitly unavailable with no version [NoiOS] should not have any attributes copied from assembly/parent
			List<string> fullyUnavailablePlatforms = UnavailableAttributesToProcess.Where (u => PlatformArgumentParser.GetVersionFromNode (u) == "" && PlatformArgumentParser.GetPlatformFromNode (u) != null)
				.Select (u => PlatformArgumentParser.GetPlatformFromNode (u)!).ToList ();

			// First copy down any information from the harvested assembly, if it exists
			// We have a pickle here in ordering:
			// 1. We must copy attributes from the harvested assembly (if any) before copying from the parent context,
			//    as attributes from the generator.cs should be respected
			// 2. However, implied attributes (those created because a type exists but are not "real"), must not 
			//    "override" attributes copied from the parent context.
			// To solve this bind, we copy everything but the implied, copy from the parent, then copy the implied
			// CopyNonConflicting will do the right thing and not override/duplicate introduced
			var impliedIntroducedAttributesToProcess = ProcessAttributeFromAssemblyInfo (member, parent, assemblyInfo, fullyUnavailablePlatforms);

			// If we define any availability attributes on a member and have a parent, we must copy
			// all non-conflicting availabilities down. This is the crux of the problem this tool is to solve.

			if (HasAnyAvailability && parent != null) {
				CopyAvailabilityFromParent (member, parent, assemblyInfo, fullyUnavailablePlatforms);
			}

			// Then finally, copy any implied attributes in one of two cases:
			//    - If we have any availability at all
			//    - We are on a class/struct context
			// These won't prevent any parent info to be copied down, since we're last
			if (HasAnyAvailability || (member is ClassDeclarationSyntax || member is StructDeclarationSyntax)) {
				CopyNonConflicting (IntroducedAttributesToProcess, impliedIntroducedAttributesToProcess, fullyUnavailablePlatforms);

				// Hack - When we don't have a parent context because we're on a class that has no attributes, the indent is almost always one tab
				// so if we don't have one set, hack that in if not set...
				if (parent is NamespaceDeclarationSyntax && assemblyInfo != null && IndentTrivia == null) {
					IndentTrivia = new SyntaxTriviaList (TriviaConstants.Tab);
				}
			}

			// We must sort IOS to be the last element in deprecatedAttributesToProcess and obsoleteAttributesToProcess
			// as the #if define in the block is a superset of others and must come last
			ForceiOSToEndOfList (DeprecatedAttributesToProcess);
			ForceiOSToEndOfList (ObsoleteAttributesToProcess);

			return new HarvestedMemberInfo (ExistingAvailabilityAttributes, NonAvailabilityAttributes, new List<AttributeSyntax> (), IntroducedAttributesToProcess, DeprecatedAttributesToProcess, UnavailableAttributesToProcess, ObsoleteAttributesToProcess, NonWhitespaceTrivia, NewlineTrivia, IndentTrivia);
		}

		List<AttributeSyntax> ProcessAttributeFromAssemblyInfo (MemberDeclarationSyntax member, MemberDeclarationSyntax? parent, AssemblyHarvestInfo? assemblyInfo, List<string> fullyUnavailablePlatforms)
		{
			var impliedIntroducedAttributesToProcess = new List<AttributeSyntax> ();
			if (assemblyInfo != null) {
				string typeName = GetClassAndNamespace (member, parent);

				if (assemblyInfo.Data.TryGetValue (typeName, out var assemblyData)) {
					HarvestedMemberInfo assemblyParentInfo = ProcessAssemblyParent (assemblyData);
					impliedIntroducedAttributesToProcess = assemblyParentInfo.ImpliedIntroducedAttributesToProcess.ToList ();
					CopyNonConflicting (IntroducedAttributesToProcess, assemblyParentInfo.IntroducedAttributesToProcess, fullyUnavailablePlatforms);
					CopyNonConflicting (DeprecatedAttributesToProcess, assemblyParentInfo.DeprecatedAttributesToProcess, fullyUnavailablePlatforms);
					CopyNonConflicting (UnavailableAttributesToProcess, assemblyParentInfo.UnavailableAttributesToProcess, fullyUnavailablePlatforms);
					CopyNonConflicting (ObsoleteAttributesToProcess, assemblyParentInfo.ObsoleteAttributesToProcess, fullyUnavailablePlatforms);
				}
			}
			return impliedIntroducedAttributesToProcess;
		}

		void ProcessAttributesOnMember (MemberDeclarationSyntax member)
		{
			bool processingFirst = true;
			foreach (var attributeList in member.AttributeLists) {
				if (NewlineTrivia == null) {
					(NonWhitespaceTrivia, NewlineTrivia, IndentTrivia) = SplitNodeTrivia (attributeList);
				}

				bool anyAvailabilityFound = false;
				foreach (var attribute in attributeList.Attributes) {
					switch (attribute.Name.ToString ()) {
					case "Mac":
					case "iOS":
					case "TV":
					case "MacCatalyst":
					case "Introduced": {
						AddIfSupportedPlatform (attribute, IntroducedAttributesToProcess);
						ExistingAvailabilityAttributes.Add (HarvestedAvailabilityInfo.From (attribute, attributeList));
						anyAvailabilityFound = true;
						break;
					}
					case "Deprecated": {
						AddIfSupportedPlatform (attribute, DeprecatedAttributesToProcess);
						ExistingAvailabilityAttributes.Add (HarvestedAvailabilityInfo.From (attribute, attributeList));
						anyAvailabilityFound = true;
						break;
					}
					case "NoMac":
					case "NoiOS":
					case "NoTV":
					case "NoMacCatalyst":
					case "Unavailable": {
						AddIfSupportedPlatform (attribute, UnavailableAttributesToProcess);
						ExistingAvailabilityAttributes.Add (HarvestedAvailabilityInfo.From (attribute, attributeList));
						anyAvailabilityFound = true;
						break;
					}
					case "Obsoleted": {
						AddIfSupportedPlatform (attribute, ObsoleteAttributesToProcess);
						ExistingAvailabilityAttributes.Add (HarvestedAvailabilityInfo.From (attribute, attributeList));
						anyAvailabilityFound = true;
						break;
					}
					case "NoWatch":
					case "Watch": {
						ExistingAvailabilityAttributes.Add (HarvestedAvailabilityInfo.From (attribute, attributeList));
						break;
					}
					default:
						NonAvailabilityAttributes.Add (HarvestedAvailabilityInfo.From (attribute, attributeList));
						break;
					}
				}

				// Detect case where we have availability attributes with #if blocks around just some of them
				// General case is very complex, but simplification of "look for #endif after first attribute 
				// until you hit item being decorated 
				if (anyAvailabilityFound) {
					List<string> trivia;
					if (processingFirst) {
						trivia = new List<string> () { attributeList.GetTrailingTrivia ().ToFullString () };
					} else {
						trivia = new List<string> () { attributeList.GetLeadingTrivia ().ToFullString (), attributeList.GetTrailingTrivia ().ToFullString () };
					}

					if (trivia.Any (t => t.Contains ("#endif"))) {
						throw new InvalidOperationException ($"{attributeList} contains conditions that will not be parsed correctly");
					}
				}

				processingFirst = false;
			}
		}

		void CopyAvailabilityFromParent (MemberDeclarationSyntax member, MemberDeclarationSyntax parent, AssemblyHarvestInfo? assemblyInfo, List<string> fullyUnavailablePlatforms)
		{
			HarvestedMemberInfo parentInfo = (new AttributeHarvester ()).Process (parent, null, assemblyInfo);
			CopyNonConflicting (IntroducedAttributesToProcess, parentInfo.IntroducedAttributesToProcess, fullyUnavailablePlatforms);
			CopyNonConflicting (DeprecatedAttributesToProcess, parentInfo.DeprecatedAttributesToProcess, fullyUnavailablePlatforms);
			CopyNonConflicting (UnavailableAttributesToProcess, parentInfo.UnavailableAttributesToProcess, fullyUnavailablePlatforms);
			CopyNonConflicting (ObsoleteAttributesToProcess, parentInfo.ObsoleteAttributesToProcess, fullyUnavailablePlatforms);
		}

		public static HarvestedMemberInfo ProcessAssemblyParent (List<HarvestedAvailabilityInfo> infos)
		{
			var impliedIntroducedAttributesToProcess = new List<AttributeSyntax> ();
			var introducedAttributesToProcess = new List<AttributeSyntax> ();
			var deprecatedAttributesToProcess = new List<AttributeSyntax> ();
			var unavailableAttributesToProcess = new List<AttributeSyntax> ();
			var obsoleteAttributesToProcess = new List<AttributeSyntax> ();

			foreach (var info in infos) {
				switch (info.Attribute.Name.ToString ()) {
				case "Mac":
				case "iOS":
				case "TV":
				case "MacCatalyst":
				case "Introduced": {
					if (info.Implied) {
						AddIfSupportedPlatform (info.Attribute, impliedIntroducedAttributesToProcess);
					} else {
						AddIfSupportedPlatform (info.Attribute, introducedAttributesToProcess);
					}
					break;
				}
				case "Deprecated": {
					AddIfSupportedPlatform (info.Attribute, deprecatedAttributesToProcess);
					break;
				}
				case "NoMac":
				case "NoiOS":
				case "NoTV":
				case "NoMacCatalyst":
				case "Unavailable": {
					AddIfSupportedPlatform (info.Attribute, unavailableAttributesToProcess);
					break;
				}
				case "Obsoleted": {
					AddIfSupportedPlatform (info.Attribute, obsoleteAttributesToProcess);
					break;
				}
				default:
					break;
				}
			}

			return new HarvestedMemberInfo (new List<HarvestedAvailabilityInfo> (), new List<HarvestedAvailabilityInfo> (), impliedIntroducedAttributesToProcess, introducedAttributesToProcess, deprecatedAttributesToProcess, unavailableAttributesToProcess, obsoleteAttributesToProcess, null, null, null);
		}

		// Given a member of a class or the class itself
		// calculate the Namespace.Class or Class name
		static string GetClassAndNamespace (MemberDeclarationSyntax type, MemberDeclarationSyntax? parent)
		{
			string name = "";
			switch (type) {
			case ClassDeclarationSyntax klass:
				name = klass.Identifier.ToString ();
				break;
			case StructDeclarationSyntax str:
				name = str.Identifier.ToString ();
				break;
			}

			SyntaxNode? current = parent ?? type.Parent;
			while (current != null) {
				switch (current) {
				case NamespaceDeclarationSyntax space:
					name = AppendIdentifier (name, space.Name);
					break;
				case ClassDeclarationSyntax klass:
					name = AppendIdentifier (name, klass.Identifier);
					break;
				case StructDeclarationSyntax str:
					name = AppendIdentifier (name, str.Identifier);
					break;
				case CompilationUnitSyntax: // Just ignore, we'll be bailing out
					break;
				default:
					throw new InvalidOperationException ($"GetFullName parent unexpected type: {current.GetType ()}");
				}
				current = current.Parent;
			}
			return name;
		}

		static string AppendIdentifier (string current, object addition) => current.Length == 0 ? addition.ToString ()! : $"{addition.ToString ()}.{current}";

		static void CopyNonConflicting (List<AttributeSyntax> destination, IEnumerable<AttributeSyntax> source, List<string> fullyUnavailablePlatforms)
		{
			foreach (var s in source) {
				string? platform = PlatformArgumentParser.GetPlatformFromNode (s);
				// Only copy if we don't have a matching kind (Introduced vs Introduced) that also matches platform (iOS)
				bool noExistingExactMatch = !destination.Any (d => AreMatchingPlatforms (s, d));
				// If we have an unversioned "NoPlatform" for our platform, also skip
				bool notFullyUnsupported = !fullyUnavailablePlatforms.Any (p => p == platform);
				if (noExistingExactMatch && notFullyUnsupported) {
					destination.Add (s);
				}
			}
		}

		static bool AreMatchingPlatforms (AttributeSyntax left, AttributeSyntax right)
		{
			// Check that the platforms line up
			bool matchingPlatforms = PlatformArgumentParser.GetPlatformFromNode (left) == PlatformArgumentParser.GetPlatformFromNode (right);
			// Check that they are compatible kinds ([iOS] and [Introduced])
			bool matchingAttributeKinds = GetComparablePlatformName (left.Name.ToString ()) == GetComparablePlatformName (right.Name.ToString ());
			return matchingPlatforms && matchingAttributeKinds;
		}

		// Convert our shortcut names to Introduced or Unavailable for comparision
		static string GetComparablePlatformName (string name)
		{
			switch (name) {
			case "Mac":
			case "iOS":
			case "TV":
			case "MacCatalyst":
				return "Introduced";
			case "NoMac":
			case "NoiOS":
			case "NoTV":
			case "NoMacCatalyst":
				return "Unavailable";
			default:
				return name;
			}
		}

		static void AddIfSupportedPlatform (AttributeSyntax attribute, List<AttributeSyntax> list)
		{
			// We don't want to add Watch to IntroducedAttributesToProcess for example
			if (PlatformArgumentParser.GetPlatformFromNode (attribute) != null) {
				list.Add (attribute);
			}
		}

		static void ForceiOSToEndOfList (List<AttributeSyntax> nodes)
		{
			// We must sort IOS to be the last element in deprecatedAttributesToProcess
			// as the #if define is a superset of others and must come last
			int iOSDeprecationIndex = nodes.FindIndex (a => PlatformArgumentParser.GetPlatformFromNode (a) == "ios");
			if (iOSDeprecationIndex != -1) {
				var deprecationElement = nodes [iOSDeprecationIndex];
				nodes.RemoveAt (iOSDeprecationIndex);
				nodes.Add (deprecationElement);
			}
		}

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
		static (SyntaxTriviaList, SyntaxTriviaList, SyntaxTriviaList) SplitNodeTrivia (SyntaxNode node)
		{
			var nonWhitespaceTrivia = new SyntaxTriviaList ();
			var newlines = new SyntaxTriviaList ();
			var rest = new SyntaxTriviaList ();

			// XXX - this could be more efficient if we find the split point and bulk copy
			bool foundSplit = false;
			var triviaToProcess = node.GetLeadingTrivia ();

			// If we come across any non-whitespace trivia, hoist it and all elements before it to nonWhitespaceTrivia
			int lastNonWhiteSpaceTrivia = triviaToProcess.LastIndexOf (x => !String.IsNullOrWhiteSpace (x.ToString ()));
			if (lastNonWhiteSpaceTrivia != -1) {
				nonWhitespaceTrivia = nonWhitespaceTrivia.AddRange (triviaToProcess.Take (lastNonWhiteSpaceTrivia + 1));
				triviaToProcess = new SyntaxTriviaList (triviaToProcess.Skip (lastNonWhiteSpaceTrivia + 1));
			}

			foreach (var trivia in triviaToProcess.Reverse ()) {
				if (trivia.Kind () == SyntaxKind.EndOfLineTrivia) {
					foundSplit = true;
				}

				if (foundSplit) {
					newlines = newlines.Add (trivia);
				} else {
					rest = rest.Add (trivia);
				}
			}
			newlines = new SyntaxTriviaList (newlines.Reverse ());
			rest = new SyntaxTriviaList (rest.Reverse ());

			return (new SyntaxTriviaList (nonWhitespaceTrivia), newlines, rest);
		}
	}


}
