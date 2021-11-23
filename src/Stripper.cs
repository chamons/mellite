//#define STRIPPER_DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using mellite.Utilities;

namespace mellite {

	public abstract class StripperBase {
		protected StringBuilder File = new StringBuilder ();
		protected StringBuilder Chunk = new StringBuilder ();

		protected abstract bool InsideInterestBlock { get; }

		protected virtual void Reset ()
		{
			File.Clear ();
			Chunk.Clear ();
		}

		protected void Write (string line, bool skipNewLine = false)
		{
			var current = InsideInterestBlock ? Chunk : File;

			current.Append (line);
			if (!skipNewLine) {
				current.Append (Environment.NewLine);
			}
		}

		protected void FileAppend (string line, bool skipNewLine = false)
		{
			File.Append (line);
			if (!skipNewLine) {
				File.Append (Environment.NewLine);
			}
		}

		public static string TrimLine (string line)
		{
			int commentIndex = line.IndexOf ("//");
			if (commentIndex != -1) {
				line = line.Substring (0, commentIndex);
			}
			commentIndex = line.IndexOf ("/*");
			if (commentIndex != -1) {
				line = line.Substring (0, commentIndex);
			}
			return line.Trim ();
		}
	}

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
	// and does it without using Roslyn to parse the major structure.
	// The reason for not using roslyn is that with #else cases, the information of #if and #endif gets 
	// split across multiple nodes, and becomes a nightmare.
	// Just use a dumb "read each line one at a time and process" parser/state machine, and then feed the contents to roslyn
	// to know if the contents are just attributes. 
	class AttributeStripper : StripperBase {
		enum State {
			InsideInterestBlock,
			WaitingForPotentialElseNotNetBlock,
			InsideUnrelatedBlock,
		}

		Stack<State> States = new Stack<State> ();

		bool HasCurrentState => States.Count > 0;

		// Are we directly nested within a InsideInterestBlock (possibly within a #if as well)
		protected override bool InsideInterestBlock => States.Any (x => x == State.InsideInterestBlock);
		State GetCurrentState (string context) => States.TryPeek (out State s) ? s : throw new InvalidOperationException ($"No state found: {context}");

		protected override void Reset ()
		{
			base.Reset ();
			States.Clear ();
		}

		[System.Diagnostics.Conditional ("STRIPPER_DEBUG")]
		void DebugLog (string line, string log)
		{
			Console.WriteLine ($"{new String ('	', States.Count)}{log}   // {line.Trim ()}");
		}

		public string StripText (string text)
		{
			Reset ();

			foreach (var line in text.SplitLines ()) {
				switch (TrimLine (line)) {
				case "#if NET":
					States.Push (State.InsideInterestBlock);
					Write (line);
					DebugLog (line, "#if NET -> InsideInterestBlock");
					break;
				case "#if !NET":
					States.Push (State.WaitingForPotentialElseNotNetBlock);
					Write (line);
					DebugLog (line, "#if !NET -> WaitingForPotentialElseNotNetBlock");
					break;
				case string s when s.StartsWith ("#if"):
					States.Push (State.InsideUnrelatedBlock);
					Write (line);
					DebugLog (line, $"{s} -> InsideUnrelatedBlock");
					break;
				case "#else":
					switch (GetCurrentState ("#else")) {
					case State.InsideInterestBlock:
						// We've wrapped up the block of interest, write out chunk and move to unrelated.
						Write (line);
						FinishCurrentChunk (line);
						States.Pop ();
						States.Push (State.InsideUnrelatedBlock);
						DebugLog (line, $"#else (InsideInterestBlock) -> InsideUnrelatedBlock");
						break;
					case State.WaitingForPotentialElseNotNetBlock:
						// We were waiting for this block
						States.Pop ();
						States.Push (State.InsideInterestBlock);
						DebugLog (line, $"#else (WaitingForPotentialElseNotNetBlock) -> InsideInterestBlock");
						Write (line);
						break;
					default:
						Write (line);
						break;
					}
					break;
				case "#endif":
					Write (line);
					DebugLog (line, $"#endif {State.InsideInterestBlock}");
					if (GetCurrentState ("#endif") == State.InsideInterestBlock) {
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

		public void FinishCurrentChunk (string current)
		{
			try {
				// Skip the #if and #else or #end for analysis by roslyn
				string section = String.Join ('\n', Chunk!.ToString ().SplitLines ().Skip (1).SkipLast (1));
				if (!ContainsOnlyAttributes (section)) {
					FileAppend (Chunk.ToString (), true);
					return;
				}

				switch (TrimLine (current)) {
				case "#else":
					// Invert the first if and drop the rest
					FileAppend ("#if !NET");
					break;
				case "#endif":
					// If our first line is #else then replace with #endif, otherwise drop everything
					var lines = Chunk!.ToString ().SplitLines ();
					if (TrimLine (lines.First ()) == "#else") {
						FileAppend (lines.Last ());
					}
					break;
				default:
					throw new NotImplementedException ();
				}
			} finally {
				Chunk.Clear ();
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

	// Processing code with many #condition blocks is difficult to get right, and they aren't truly needed
	// as we'll be adding them back with gobs of code. So strip #if NET/#if !NET and associated #else/#endif
	// meant to run after AttributeStripper step.
	class ConditionBlockStripper : StripperBase {
		enum State {
			InsideInterestBlock,
			InsideUnrelatedBlock,
		}
		Stack<State> States = new Stack<State> ();
		State GetCurrentState (string context) => States.TryPeek (out State s) ? s : throw new InvalidOperationException ($"No state found: {context}");

		protected override bool InsideInterestBlock => States.Any (x => x == State.InsideInterestBlock);

		protected override void Reset ()
		{
			base.Reset ();
			States.Clear ();
		}

		public string StripText (string text)
		{
			Reset ();

			foreach (var line in text.SplitLines ()) {
				switch (TrimLine (line)) {
				case "#if NET":
					States.Push (State.InsideInterestBlock);
					break;
				case "#if !NET":
					States.Push (State.InsideInterestBlock);
					break;
				case string s when s.StartsWith ("#if"):
					States.Push (State.InsideUnrelatedBlock);
					Write (line);
					break;
				case "#else":
					// Skip writing out the else and keep looking for the #endif to skip
					if (GetCurrentState ("#else") != State.InsideInterestBlock) {
						Write (line);
					}
					break;
				case "#endif":
					switch (GetCurrentState ("#endif")) {
					case State.InsideInterestBlock:
						FileAppend (Chunk.ToString (), true);
						Chunk.Clear ();
						break;
					case State.InsideUnrelatedBlock:
						Write (line);
						break;
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
				foreach (var attribute in ((AttributeListSyntax) node).Attributes) {
					switch (attribute.Name.ToString ()) {
					case "Mac":
					case "iOS":
					case "TV":
					case "MacCatalyst":
					case "Introduced":
					case "Deprecated":
					case "NoMac":
					case "NoiOS":
					case "NoTV":
					case "NoMacCatalyst":
					case "Unavailable":
					case "Obsoleted":
					case "NoWatch":
					case "Watch":
					case "UnsupportedOSPlatform":
					case "SupportedOSPlatform":
					case "Obsolete":
						break;
					default:
						EverythingIsAvailabilityAttribute = false;
						break;
					}
				}
				break;
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
