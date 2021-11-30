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
	public static class Harvester {
		public static HarvestedMemberInfo Process (MemberDeclarationSyntax member, MemberDeclarationSyntax? parent)
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
			foreach (var attributeList in member.AttributeLists) {
				if (newlineTrivia == null) {
					(nonWhitespaceTrivia, newlineTrivia, indentTrivia) = SplitNodeTrivia (attributeList);
				}

				foreach (var attribute in attributeList.Attributes) {
					switch (attribute.Name.ToString ()) {
					case "Mac":
					case "iOS":
					case "TV":
					case "MacCatalyst":
					case "Introduced": {
						AddIfSupportedPlatform (attribute, introducedAttributesToProcess);
						existingAvailabilityAttributes.Add (HarvestedAvailabilityInfo.From (attribute, attributeList));
						break;
					}
					case "Deprecated": {
						AddIfSupportedPlatform (attribute, deprecatedAttributesToProcess);
						existingAvailabilityAttributes.Add (HarvestedAvailabilityInfo.From (attribute, attributeList));
						break;
					}
					case "NoMac":
					case "NoiOS":
					case "NoTV":
					case "NoMacCatalyst":
					case "Unavailable": {
						AddIfSupportedPlatform (attribute, unavailableAttributesToProcess);
						existingAvailabilityAttributes.Add (HarvestedAvailabilityInfo.From (attribute, attributeList));
						break;
					}
					case "Obsoleted": {
						AddIfSupportedPlatform (attribute, obsoleteAttributesToProcess);
						existingAvailabilityAttributes.Add (HarvestedAvailabilityInfo.From (attribute, attributeList));
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
			}

			// If we define any availability attributes on a member and have a parent, we must copy
			// all non-conflicting availabilities down. This is the crux of the problem this tool is to solve.
			bool hasAnyAvailability = introducedAttributesToProcess.Any () ||
				deprecatedAttributesToProcess.Any () ||
				unavailableAttributesToProcess.Any () ||
				obsoleteAttributesToProcess.Any ();
			if (hasAnyAvailability && parent != null) {
				HarvestedMemberInfo parentInfo = Harvester.Process (parent, null);
				List<string> fullyUnavailablePlatforms = unavailableAttributesToProcess.Where (u => PlatformArgumentParser.GetVersionFromNode (u) == "" && PlatformArgumentParser.GetPlatformFromNode (u) != null)
					.Select (u => PlatformArgumentParser.GetPlatformFromNode (u)!).ToList ();
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

		static void CopyNonConflicting (List<AttributeSyntax> destination, IEnumerable<AttributeSyntax> source, List<string> fullyUnavailablePlatforms)
		{
			foreach (var s in source) {
				string? platform = PlatformArgumentParser.GetPlatformFromNode (s);
				// Only copy if we don't have a matching kind (Introduced vs Introduced) that also matches platform (iOS)
				bool noExistingExactMatch = !destination.Any (d => d.Name.ToString () == s.Name.ToString () && PlatformArgumentParser.GetPlatformFromNode (d) == platform);
				// If we have an unversioned "NoPlatform" for our platform, also skip
				bool notFullyUnsupported = !fullyUnavailablePlatforms.Any (p => p == platform);
				if (noExistingExactMatch && notFullyUnsupported) {
					destination.Add (s);
				}
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
			return (new SyntaxTriviaList (nonWhitespaceTrivia), new SyntaxTriviaList (newlines.Reverse ()), new SyntaxTriviaList (rest.Reverse ()));
		}
	}

}
