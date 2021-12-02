using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using mellite;
using System.Text;

namespace mellite.Utilities {
	public static class RoslynExtensions {
		public static AttributeListSyntax ToAttributeList (this AttributeSyntax createdAttribute)
		{
			// Add a ' ' between Attribute and attribute '(' if there is a '('
			if (createdAttribute.ArgumentList?.Arguments.Count > 0) {
				createdAttribute = createdAttribute.WithName (createdAttribute.Name.WithTrailingTrivia (SyntaxFactory.ParseLeadingTrivia (" ")));
			}
			var netAttributeElements = SyntaxFactory.SeparatedList (new List<AttributeSyntax> () { createdAttribute }, Enumerable.Repeat (SyntaxFactory.Token (SyntaxKind.CommaToken), 0));
			return SyntaxFactory.AttributeList (netAttributeElements);
		}

		public static AttributeListSyntax ToAttributeList (this HarvestedAvailabilityInfo createdAttributeInfo)
		{
			AttributeSyntax createdAttribute = createdAttributeInfo.Attribute;
			// Add a ' ' between Attribute and attribute '(' if there is a '('
			if (createdAttribute.ArgumentList?.Arguments.Count > 0) {
				createdAttribute = createdAttribute.WithName (createdAttribute.Name.WithTrailingTrivia (SyntaxFactory.ParseLeadingTrivia (" ")));
			}
			var netAttributeElements = SyntaxFactory.SeparatedList (new List<AttributeSyntax> () { createdAttribute }, Enumerable.Repeat (SyntaxFactory.Token (SyntaxKind.CommaToken), 0));

			AttributeListSyntax list;
			if (createdAttributeInfo.Target == null) {
				list = SyntaxFactory.AttributeList (netAttributeElements);
			} else {
				// Remove the trailing : from the parsed target, as AttributeTargetSpecifier will re-add it
				var target = SyntaxFactory.AttributeTargetSpecifier (SyntaxFactory.Identifier (createdAttributeInfo.Target.TrimEnd (':'))).WithTrailingTrivia (TriviaConstants.Space);
				list = SyntaxFactory.AttributeList (target, netAttributeElements);
			}

			if (!String.IsNullOrEmpty (createdAttributeInfo.Comment)) {
				list = list.WithTrailingTrivia (list.GetTrailingTrivia ().AddRange (TriviaConstants.Space).AddRange (SyntaxFactory.TriviaList (SyntaxFactory.Comment (createdAttributeInfo.Comment))));
			}
			return list;
		}
	}

	public static class LinqExtensions {
		public static int IndexOf<TSource> (this IEnumerable<TSource> source, Func<TSource, bool> predicate)
		{
			var index = 0;
			foreach (var item in source) {
				if (predicate.Invoke (item)) {
					return index;
				}
				index++;
			}
			return -1;
		}

		public static int LastIndexOf<TSource> (this IEnumerable<TSource> source, Func<TSource, bool> predicate)
		{
			var found = -1;
			var index = 0;
			foreach (var item in source) {
				if (predicate.Invoke (item)) {
					found = index;
				}
				index++;
			}
			return found;
		}
	}

	public static class StringExtensions {
		internal static IEnumerable<string> SplitLines (this string text)
		{
			string? line;
			using (var reader = new StringReader (text)) {
				while ((line = reader.ReadLine ()) != null) {
					yield return line;
				}
			}
		}

		internal static string LeadingWhitespace (this string text)
		{
			StringBuilder whitespace = new StringBuilder ();
			foreach (char c in text) {
				if (!Char.IsWhiteSpace (c)) {
					break;
				}
				whitespace.Append (c);
			}

			return whitespace.ToString ();
		}
	}
}
