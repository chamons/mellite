using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using mellite.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.IO;

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
	// and does it without using Roslyn at all.
	// The reason for not using roslyn is that with #else cases, the information of #if and #endif gets 
	// split across multiple nodes, and becomes a nightmare.
	// Just use a dumb "read each line one at a time and process" parser
	enum StripperState {
		InsideInterestBlock,
		WaitingForPotentialElseNotNetBlock,
		InsideUnrelatedBlock,
	}
	class Stripper {
		Stack<StripperState> States = new Stack<StripperState> ();

		bool HasCurrentState => States.Count > 0;

		// Are we directly nested within a InsideInterestBlock (possibly within a #if as well)
		bool InsideInterestBlock => States.Any (x => x == StripperState.InsideInterestBlock);
		StripperState GetCurrentState (string context) => States.TryPeek (out StripperState s) ? s : throw new InvalidOperationException ($"No state found: {context}");

		StringBuilder File = new StringBuilder ();
		StringBuilder Chunk = new StringBuilder ();

		public Stripper ()
		{
		}

		public void Reset ()
		{
			States.Clear ();
			File.Clear ();
			Chunk.Clear ();
		}

		public string StripText (string text)
		{
			Reset ();

			foreach (var line in text.SplitLines ()) {
				var trimmedLine = line.Trim ();
				switch (trimmedLine) {
				case "#if NET":
					States.Push (StripperState.InsideInterestBlock);
					Write (line);
					break;
				case "#if !NET":
					States.Push (StripperState.WaitingForPotentialElseNotNetBlock);
					Write (line);
					break;
				case string s when s.StartsWith ("#if"):
					States.Push (StripperState.InsideUnrelatedBlock);
					Write (line);
					break;
				case "#else":
					switch (GetCurrentState ("#else")) {
					case StripperState.InsideInterestBlock:
						// We've wrapped up the block of interest, write out chunk and move to unrelated.
						Write (line);
						FinishCurrentChunk (line);
						States.Pop ();
						States.Push (StripperState.InsideUnrelatedBlock);
						break;
					case StripperState.WaitingForPotentialElseNotNetBlock:
						// We were waiting for this block
						States.Pop ();
						States.Push (StripperState.InsideInterestBlock);
						Write (line);
						break;
					default:
						Write (line);
						break;
					}
					break;
				case "#endif":
					Write (line);
					if (GetCurrentState ("#endif") == StripperState.InsideInterestBlock) {
						FinishCurrentChunk (line);
					}
					States.Pop ();
					break;
				default:
					Write (line);
					break;
				}
			}
			return File.ToString ();
		}

		void Write (string line, bool skipNewLine = false)
		{
			var current = InsideInterestBlock ? Chunk : File;

			current.Append (line);
			if (!skipNewLine) {
				current.Append (Environment.NewLine);
			}
		}

		void FileAppend (string line, bool skipNewLine = false)
		{
			File.Append (line);
			if (!skipNewLine) {
				File.Append (Environment.NewLine);
			}
		}

		public void FinishCurrentChunk (string current)
		{
			// Skip the #if and #else or #end for analysis by roslyn
			string section = String.Join ('\n', Chunk!.ToString ().SplitLines ().Skip (1).SkipLast (1));
			if (!ContainsOnlyAttributes (section)) {
				FileAppend (Chunk.ToString (), true);
				return;
			}

			switch (current.Trim ()) {
			case "#else":
				// Invert the first if and drop the rest
				FileAppend ("#if !NET");
				break;
			case "#endif":
				// If our first line is #else then replace with #endif, otherwise drop everything
				if (Chunk!.ToString ().SplitLines ().First () == "#else") {
					FileAppend ("#endif");
				}
				break;
			default:
				throw new NotImplementedException ();
			}
		}

		public bool ContainsOnlyAttributes (string text)
		{
			SyntaxTree tree = CSharpSyntaxTree.ParseText (text);

			CompilationUnitSyntax root = tree.GetCompilationUnitRoot ();

			var visitor = new TriviaContentsVisitor ();
			root!.Accept (visitor);
			return visitor.EverythingIsAvailabilityAttribute;
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
