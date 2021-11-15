using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using mellite.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace mellite {
	// We want to strip blocks like this:
	// #if NET
	//    [UnsupportedOSPlatform (""ios13.0"")]
	// #endif
	// from source code.
	// The general problem is more difficult than it appears, once you consider nested block and 
	// blocks that contains attributes and other items, and not leaving empty blocks.
	// So this solves a constrained subproblem and detects only two cases:
	// - #1 - #if NET
	// - #2 - #if !NET
	// A further problem is that inside the "non-active" case we just see a bunch of disabled text, and in the "active" case we have roslyn structured code
	// So we run this twice, and only visit the trivia in between a recognized #if block and it's #else of #endif. By doing that, and defining NET in one of them
	// we can treat it always as disabled text consistently.

	// This rewriter runs first and marks trivia that should be removed with ToRemoveAnnotation.
	class MarkingAttributeStripperVisitor : CSharpSyntaxRewriter {
		public static string ToRemoveAnnotation = "mellite.remove";

		StringBuilder Text = new StringBuilder ();

		bool Enabled = false; // Should we start recording text for potential removal?
		bool EnableNextElse = false; // We should set Enable on the next else if it occurs before endif

		// TODO - If hack below works, remove this and run only with define not defined
		bool NetIsDefined;

		public MarkingAttributeStripperVisitor (bool netIsDefined)
		{
			NetIsDefined = netIsDefined;
		}

		public override SyntaxTrivia VisitTrivia (SyntaxTrivia trivia)
		{
			bool markForRemoval = false;
			switch (trivia.Kind ()) {
			case SyntaxKind.IfDirectiveTrivia:
				// - If we see #if NET start listening
				// - If we see #if !NET note that we should start listening at the else
				var text = trivia.ToFullString ().Trim ();
				Enabled = text == "#if NET" && !NetIsDefined;
				EnableNextElse = text == "#if !NET" && !NetIsDefined;
				break;
			case SyntaxKind.ElseDirectiveTrivia:
				if (EnableNextElse) {
					Enabled = true;
					EnableNextElse = false;
				} else if (Enabled) {
					markForRemoval = ProcessTriviaBlock ();
					Enabled = false;
				}
				break;
			case SyntaxKind.EndIfDirectiveTrivia:
				if (Enabled) {
					markForRemoval = ProcessTriviaBlock ();
					Enabled = false;
					EnableNextElse = false;
				}
				break;
			default:
				if (Enabled) {
					Text.Append (trivia.ToFullString ());
				}
				break;
			}
			if (markForRemoval) {
				return trivia.WithAdditionalAnnotations (new SyntaxAnnotation (ToRemoveAnnotation));
			} else {
				return trivia;
			}
		}

		bool ProcessTriviaBlock ()
		{
			var text = Text.ToString ().Trim ();
			if (text.Length > 0) {
				SyntaxTree tree = CSharpSyntaxTree.ParseText (text);

				CompilationUnitSyntax root = tree.GetCompilationUnitRoot ();

				var visitor = new TriviaContentsVisitor ();
				root!.Accept (visitor);
				// If everything in our block is an attribute, mark it for removal
				return visitor.EverythingIsAvailabilityAttribute;
			}
			Text.Clear ();
			return false;
		}
	}

	// This rewriter finds all nodes with 'mellite.remove' and rewrites or removes their trivia
	class RemoveMarkedTriviaStripperVisitor : CSharpSyntaxRewriter {
		public override SyntaxNode? VisitPropertyDeclaration (PropertyDeclarationSyntax node)
		{
			return Process (node) ?? base.VisitPropertyDeclaration (node);
		}

		public override SyntaxNode? VisitMethodDeclaration (MethodDeclarationSyntax node)
		{
			return Process (node) ?? base.VisitMethodDeclaration (node);
		}

		public override SyntaxNode? VisitClassDeclaration (ClassDeclarationSyntax node)
		{
			return Process (node) ?? base.VisitClassDeclaration (node);
		}

		T? Process<T> (T node) where T : MemberDeclarationSyntax
		{
			Func<SyntaxTrivia, bool> hasMarking = x => x.GetAnnotations (MarkingAttributeStripperVisitor.ToRemoveAnnotation).Any ();
			var markedIndex = node.GetLeadingTrivia ().IndexOf (hasMarking);
			if (markedIndex == -1) {
				markedIndex = node.GetTrailingTrivia ().IndexOf (hasMarking);
			}
			if (markedIndex != -1) {
				var triviaList = node.GetLeadingTrivia ();

				switch (triviaList [markedIndex].Kind ()) {
				case SyntaxKind.EndIfDirectiveTrivia: {
					// This will be the range of trivia to delete in removing the #if block section
					var endIndex = triviaList.IndexOf (triviaList [markedIndex]);
					var index = endIndex;

					// Walk backwards until we hit an else or the if.
					bool complete = false;
					while (!complete && index >= 0) {
						switch (triviaList [index].Kind ()) {
						case SyntaxKind.IfDirectiveTrivia:
							// Delete the entire range from #if to #endif
							complete = true;
							break;
						case SyntaxKind.ElseDirectiveTrivia:
							throw new NotImplementedException ();
						default:
							// Continue processing
							index -= 1;
							break;
						}
					}

					// Apply that deletion range
					var finalTrivia = triviaList.ToList ();
					finalTrivia.RemoveRange (index, endIndex - index + 1);
					return node.WithLeadingTrivia (finalTrivia);
				}
				case SyntaxKind.ElseDirectiveTrivia: {
					// TODO - The problem is that we're hitting this with NET defined, so we see
					// #if STUFF #else defined as leading the "real" UnsupportedOSPlatform
					// which is wrong. 
					// (text == "#if !NET" && NetIsDefined on 47 should be text == "#else"
					// So we can delete the else -> before the endif 
					// However, we can't just trigger than on every #else
					// so MarkingAttributeStripperVisitor will have to have state to remember "I just saw a #if !NET
					// so mark the next #else", and then flip all of that logic. What a mess....
					throw new NotImplementedException ();
					break;
				}
				default:
					throw new NotImplementedException ();
				}
			}
			return null;
		}
	}

	// This "rewriter" verified that all contents of a #if or #else block are attributes and are safe to remove
	// It in theory could be a visitor, but I had trouble making that work.
	class TriviaContentsVisitor : CSharpSyntaxRewriter {
		public bool EverythingIsAvailabilityAttribute = true;

		public TriviaContentsVisitor ()
		{
		}

		public override SyntaxNode? VisitAttributeArgumentList (AttributeArgumentListSyntax node)
		{
			return base.VisitAttributeArgumentList (node);
		}

		public override SyntaxNode? Visit (SyntaxNode? node)
		{
			switch (node?.GetType ().ToString ()) {
			case "Microsoft.CodeAnalysis.CSharp.Syntax.IncompleteMemberSyntax":
				return base.Visit (node);
			case "Microsoft.CodeAnalysis.CSharp.Syntax.AttributeListSyntax":
			case null:
				break;
			default:
				EverythingIsAvailabilityAttribute = false;
				break;
			}
			return node;
		}
	}
}
