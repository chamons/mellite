using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace mellite {
	public static class TriviaConstants {
		public static SyntaxTriviaList IfNet = SyntaxFactory.ParseLeadingTrivia ("#if NET");
		public static SyntaxTriviaList IfNotNet = SyntaxFactory.ParseLeadingTrivia ("#if !NET");
		public static SyntaxTriviaList Else = SyntaxFactory.ParseLeadingTrivia ("#else");
		public static SyntaxTriviaList EndIf = SyntaxFactory.ParseTrailingTrivia ("#endif");

		public static SyntaxTriviaList Newline = SyntaxFactory.ParseTrailingTrivia (Environment.NewLine);
		public static SyntaxTriviaList Space = SyntaxFactory.ParseLeadingTrivia (" ");
		public static SyntaxTriviaList Tab = SyntaxFactory.ParseLeadingTrivia ("\t");

	}
}
