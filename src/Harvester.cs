using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace mellite {
	public class HarvestedMemberInfo {
		public ReadOnlyCollection<AttributeSyntax> ExistingAttributes;

		public ReadOnlyCollection<AttributeSyntax> IntroducedAttributesToProcess;
		public ReadOnlyCollection<AttributeSyntax> DeprecatedAttributesToProcess;
		public ReadOnlyCollection<AttributeSyntax> UnavailableAttributesToProcess;
		public ReadOnlyCollection<AttributeSyntax> ObsoleteAttributesToProcess;

		public SyntaxTriviaList NewlineTrivia;
		public SyntaxTriviaList IndentTrivia;

		public HarvestedMemberInfo (List<AttributeSyntax> existingAttributes, List<AttributeSyntax> introducedAttributesToProcess, List<AttributeSyntax> deprecatedAttributesToProcess, List<AttributeSyntax> unavailableAttributesToProcess, List<AttributeSyntax> obsoleteAttributesToProcess, SyntaxTriviaList? newlineTrivia, SyntaxTriviaList? indentTrivia)
		{
			ExistingAttributes = existingAttributes.AsReadOnly ();

			IntroducedAttributesToProcess = introducedAttributesToProcess.AsReadOnly ();
			DeprecatedAttributesToProcess = deprecatedAttributesToProcess.AsReadOnly ();
			UnavailableAttributesToProcess = unavailableAttributesToProcess.AsReadOnly ();
			ObsoleteAttributesToProcess = obsoleteAttributesToProcess.AsReadOnly ();

			NewlineTrivia = newlineTrivia ?? new SyntaxTriviaList ();
			IndentTrivia = indentTrivia ?? new SyntaxTriviaList ();
		}
	}

	// Harvest information from a given Roslyn node for later conversion
	public static class Harvester {
		public static HarvestedMemberInfo Process (MemberDeclarationSyntax member)
		{
			var existingAttributes = new List<AttributeSyntax> ();
			var introducedAttributesToProcess = new List<AttributeSyntax> ();
			var deprecatedAttributesToProcess = new List<AttributeSyntax> ();
			var unavailableAttributesToProcess = new List<AttributeSyntax> ();
			var obsoleteAttributesToProcess = new List<AttributeSyntax> ();

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
					case "Unavailable": {
						unavailableAttributesToProcess.Add (attribute);
						existingAttributes.Add (attribute);
						break;
					}
					case "NoMac":
					case "NoiOS":
					case "NoWatch":
					case "NoTVAttribute":
					case "NoMacCatalyst":
					case "Obsolete": {
						obsoleteAttributesToProcess.Add (attribute);
						existingAttributes.Add (attribute);
						break;
					}
					case "AttributeUsage": // HACK
						break;
					default:
						throw new NotImplementedException ($"AttributeConverterVisitor came across mixed set of availability attributes and others: '{attribute.Name}'");
					}
				}
			}

			// We must sort IOS to be the last element in deprecatedAttributesToProcess and obsoleteAttributesToProcess
			// as the #if define in the block is a superset of others and must come last
			ForceiOSToEndOfList (deprecatedAttributesToProcess);
			ForceiOSToEndOfList (obsoleteAttributesToProcess);

			return new HarvestedMemberInfo (existingAttributes, introducedAttributesToProcess, deprecatedAttributesToProcess, unavailableAttributesToProcess, obsoleteAttributesToProcess, newlineTrivia, indentTrivia);
		}

		static void ForceiOSToEndOfList (List<AttributeSyntax> nodes)
		{
			// We must sort IOS to be the last element in deprecatedAttributesToProcess
			// as the #if define is a superset of others and must come last
			int iOSDeprecationIndex = nodes.FindIndex (a => a.ArgumentList!.Arguments [0].ToString () == "PlatformName.iOS");
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
		static (SyntaxTriviaList, SyntaxTriviaList) SplitNodeTrivia (SyntaxNode node)
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
	}

}
