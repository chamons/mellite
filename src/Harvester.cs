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

		public HarvestedAvailabilityInfo (AttributeSyntax attribute, string? target, string? comment)
		{
			Attribute = attribute;
			Target = target;
			Comment = comment;
		}

		public HarvestedAvailabilityInfo (string name, string argList)
		{
			var args = SyntaxFactory.ParseAttributeArgumentList ($"({argList})");
			Attribute = SyntaxFactory.Attribute (SyntaxFactory.ParseName (name), args);
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

		public ReadOnlyCollection<AttributeSyntax> IntroducedAttributesToProcess;
		public ReadOnlyCollection<AttributeSyntax> DeprecatedAttributesToProcess;
		public ReadOnlyCollection<AttributeSyntax> UnavailableAttributesToProcess;
		public ReadOnlyCollection<AttributeSyntax> ObsoleteAttributesToProcess;

		// This is a bit of a hack. #if and such can be added to the trivia of our first node, and we want those, only once, before
		// any attributes. So store them off separate for now.
		public SyntaxTriviaList NonWhitespaceTrivia;

		public SyntaxTriviaList NewlineTrivia;
		public SyntaxTriviaList IndentTrivia;

		public HarvestedMemberInfo (List<HarvestedAvailabilityInfo> existingAvailabilityAttributes, List<HarvestedAvailabilityInfo> nonAvailabilityAttributes, List<AttributeSyntax> introducedAttributesToProcess, List<AttributeSyntax> deprecatedAttributesToProcess, List<AttributeSyntax> unavailableAttributesToProcess, List<AttributeSyntax> obsoleteAttributesToProcess, SyntaxTriviaList? nonWhitespaceTrivia, SyntaxTriviaList? newlineTrivia, SyntaxTriviaList? indentTrivia)
		{
			ExistingAvailabilityAttributes = existingAvailabilityAttributes.AsReadOnly ();
			NonAvailabilityAttributes = nonAvailabilityAttributes.AsReadOnly ();

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
	public static class AttributeHarvester {
		public static HarvestedMemberInfo Process (MemberDeclarationSyntax member, BaseTypeDeclarationSyntax? parent, AssemblyHarvestInfo? assemblyInfo)
		{
			var existingAvailabilityAttributes = new List<HarvestedAvailabilityInfo> ();
			var nonAvailabilityAttributes = new List<HarvestedAvailabilityInfo> ();

			var introducedAttributesToProcess = new List<AttributeSyntax> ();
			var deprecatedAttributesToProcess = new List<AttributeSyntax> ();
			var unavailableAttributesToProcess = new List<AttributeSyntax> ();
			var obsoleteAttributesToProcess = new List<AttributeSyntax> ();

			SyntaxTriviaList? nonWhitespaceTrivia = null;
			SyntaxTriviaList? newlineTrivia = null;
			SyntaxTriviaList? indentTrivia = null;

			bool processingFirst = true;
			foreach (var attributeList in member.AttributeLists) {
				if (newlineTrivia == null) {
					(nonWhitespaceTrivia, newlineTrivia, indentTrivia) = SplitNodeTrivia (attributeList);
				}

				bool anyAvailabilityFound = false;
				foreach (var attribute in attributeList.Attributes) {
					switch (attribute.Name.ToString ()) {
					case "Mac":
					case "iOS":
					case "TV":
					case "MacCatalyst":
					case "Introduced": {
						AddIfSupportedPlatform (attribute, introducedAttributesToProcess);
						existingAvailabilityAttributes.Add (HarvestedAvailabilityInfo.From (attribute, attributeList));
						anyAvailabilityFound = true;
						break;
					}
					case "Deprecated": {
						AddIfSupportedPlatform (attribute, deprecatedAttributesToProcess);
						existingAvailabilityAttributes.Add (HarvestedAvailabilityInfo.From (attribute, attributeList));
						anyAvailabilityFound = true;
						break;
					}
					case "NoMac":
					case "NoiOS":
					case "NoTV":
					case "NoMacCatalyst":
					case "Unavailable": {
						AddIfSupportedPlatform (attribute, unavailableAttributesToProcess);
						existingAvailabilityAttributes.Add (HarvestedAvailabilityInfo.From (attribute, attributeList));
						anyAvailabilityFound = true;
						break;
					}
					case "Obsoleted": {
						AddIfSupportedPlatform (attribute, obsoleteAttributesToProcess);
						existingAvailabilityAttributes.Add (HarvestedAvailabilityInfo.From (attribute, attributeList));
						anyAvailabilityFound = true;
						break;
					}
					case "NoWatch":
					case "Watch": {
						existingAvailabilityAttributes.Add (HarvestedAvailabilityInfo.From (attribute, attributeList));
						break;
					}
					default:
						nonAvailabilityAttributes.Add (HarvestedAvailabilityInfo.From (attribute, attributeList));
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

			// If we define any availability attributes on a member and have a parent, we must copy
			// all non-conflicting availabilities down. This is the crux of the problem this tool is to solve.
			bool hasAnyAvailability = introducedAttributesToProcess.Any () ||
				deprecatedAttributesToProcess.Any () ||
				unavailableAttributesToProcess.Any () ||
				obsoleteAttributesToProcess.Any ();
			if (hasAnyAvailability && parent != null) {
				// First copy down any information from the harvested assembly, if it exists
				List<string> fullyUnavailablePlatforms = unavailableAttributesToProcess.Where (u => PlatformArgumentParser.GetVersionFromNode (u) == "" && PlatformArgumentParser.GetPlatformFromNode (u) != null)
					.Select (u => PlatformArgumentParser.GetPlatformFromNode (u)!).ToList ();

				if (assemblyInfo != null) {
					string fullName = GetFullName (parent);

					if (assemblyInfo.Data.TryGetValue (fullName, out var assemblyData)) {
						HarvestedMemberInfo assemblyParentInfo = ProcessAssemblyParent (assemblyData);
						CopyNonConflicting (introducedAttributesToProcess, assemblyParentInfo.IntroducedAttributesToProcess, fullyUnavailablePlatforms);
						CopyNonConflicting (deprecatedAttributesToProcess, assemblyParentInfo.DeprecatedAttributesToProcess, fullyUnavailablePlatforms);
						CopyNonConflicting (unavailableAttributesToProcess, assemblyParentInfo.UnavailableAttributesToProcess, fullyUnavailablePlatforms);
						CopyNonConflicting (obsoleteAttributesToProcess, assemblyParentInfo.ObsoleteAttributesToProcess, fullyUnavailablePlatforms);
					}
				}
				// Then copy down any information from our current roslyn context
				HarvestedMemberInfo parentInfo = AttributeHarvester.Process (parent, null, assemblyInfo);
				CopyNonConflicting (introducedAttributesToProcess, parentInfo.IntroducedAttributesToProcess, fullyUnavailablePlatforms);
				CopyNonConflicting (deprecatedAttributesToProcess, parentInfo.DeprecatedAttributesToProcess, fullyUnavailablePlatforms);
				CopyNonConflicting (unavailableAttributesToProcess, parentInfo.UnavailableAttributesToProcess, fullyUnavailablePlatforms);
				CopyNonConflicting (obsoleteAttributesToProcess, parentInfo.ObsoleteAttributesToProcess, fullyUnavailablePlatforms);
			}

			// We must sort IOS to be the last element in deprecatedAttributesToProcess and obsoleteAttributesToProcess
			// as the #if define in the block is a superset of others and must come last
			ForceiOSToEndOfList (deprecatedAttributesToProcess);
			ForceiOSToEndOfList (obsoleteAttributesToProcess);

			return new HarvestedMemberInfo (existingAvailabilityAttributes, nonAvailabilityAttributes, introducedAttributesToProcess, deprecatedAttributesToProcess, unavailableAttributesToProcess, obsoleteAttributesToProcess, nonWhitespaceTrivia, newlineTrivia, indentTrivia);
		}

		public static HarvestedMemberInfo ProcessAssemblyParent (List<HarvestedAvailabilityInfo> infos)
		{
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
					AddIfSupportedPlatform (info.Attribute, introducedAttributesToProcess);
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

			return new HarvestedMemberInfo (new List<HarvestedAvailabilityInfo> (), new List<HarvestedAvailabilityInfo> (), introducedAttributesToProcess, deprecatedAttributesToProcess, unavailableAttributesToProcess, obsoleteAttributesToProcess, null, null, null);
		}

		static string GetFullName (BaseTypeDeclarationSyntax parent)
		{
			string name = parent.Identifier.ToString ();

			SyntaxNode? current = parent.Parent;
			while (current != null) {
				switch (current) {
				case NamespaceDeclarationSyntax space:
					name = space.Name + "." + name;
					break;
				case ClassDeclarationSyntax klass:
					name = klass.Identifier.ToString () + "." + name;
					break;
				case StructDeclarationSyntax str:
					name = str.Identifier.ToString () + "." + name;
					break;
				default:
					throw new NotImplementedException ();
				}
				current = current.Parent as BaseTypeDeclarationSyntax;
			}
			return name;
		}

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
