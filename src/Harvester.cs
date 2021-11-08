using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace mellite {
	public class HarvestedMemberInfo {
		public List<AttributeListSyntax> CreatedAttributes = new List<AttributeListSyntax> ();
		public List<AttributeSyntax> ExistingAttributes = new List<AttributeSyntax> ();

		public List<AttributeSyntax> DeprecatedAttributesToProcess = new List<AttributeSyntax> ();
		public List<AttributeSyntax> IntroducedAttributesToProcess = new List<AttributeSyntax> ();

		public SyntaxTriviaList NewlineTrivia = new SyntaxTriviaList ();
		public SyntaxTriviaList IndentTrivia = new SyntaxTriviaList ();
	}

	// Harvest information from a given Roslyn node for later conversion
	public static class Harvester {
		public static HarvestedMemberInfo Process (MemberDeclarationSyntax member)
		{
			HarvestedMemberInfo info = new HarvestedMemberInfo ();

			SyntaxTriviaList? newlineTrivia = null;
			SyntaxTriviaList? indentTrivia = null;
			foreach (var attributeList in member.AttributeLists) {
				if (newlineTrivia == null) {
					(newlineTrivia, indentTrivia) = SplitNodeTrivia (attributeList);
				}

				foreach (var attribute in attributeList.Attributes) {
					switch (attribute.Name.ToString ()) {
					case "Introduced": {
						info.IntroducedAttributesToProcess.Add (attribute);
						info.ExistingAttributes.Add (attribute);
						break;
					}
					case "Deprecated": {
						info.DeprecatedAttributesToProcess.Add (attribute);
						info.ExistingAttributes.Add (attribute);
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
			if (newlineTrivia != null) {
				info.NewlineTrivia = (SyntaxTriviaList) newlineTrivia;
			}
			if (indentTrivia != null) {
				info.IndentTrivia = (SyntaxTriviaList) indentTrivia;
			}
			return info;
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
