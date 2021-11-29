using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using mellite;

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

		public static AttributeListSyntax ToAttributeList (this (AttributeSyntax, string?) createdAttributeInfo)
		{
			if (createdAttributeInfo.Item2 == null) {
				return createdAttributeInfo.Item1.ToAttributeList ();
			} else {
				AttributeSyntax createdAttribute = createdAttributeInfo.Item1;
				// Add a ' ' between Attribute and attribute '(' if there is a '('
				if (createdAttribute.ArgumentList?.Arguments.Count > 0) {
					createdAttribute = createdAttribute.WithName (createdAttribute.Name.WithTrailingTrivia (TriviaConstants.Space));
				}
				var netAttributeElements = SyntaxFactory.SeparatedList (new List<AttributeSyntax> () { createdAttribute }, Enumerable.Repeat (SyntaxFactory.Token (SyntaxKind.CommaToken), 0));
				// Remove the trailing : from the parsed target, as AttributeTargetSpecifier will re-add it
				var target = SyntaxFactory.AttributeTargetSpecifier (SyntaxFactory.Identifier (createdAttributeInfo.Item2.TrimEnd (':'))).WithTrailingTrivia (TriviaConstants.Space);
				return SyntaxFactory.AttributeList (target, netAttributeElements);
			}
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
	}
}
