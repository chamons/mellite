using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
