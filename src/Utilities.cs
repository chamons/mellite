using System.Collections.Generic;
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
}
