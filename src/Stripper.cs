//#define STRIPPER_DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using mellite.Utilities;
using System.Text.RegularExpressions;

namespace mellite {
	public static class StripperHelpers {
		// Start of string, #if, space, some number of (word chars, whitespace, |, &, (, )), end of string
		public const string PositiveConditionalTrivia = "^#if [\\w\\s|&()]+$";

		// Start of string, #if, space, some number of (!, word chars, whitespace, |, &, (, )), end of string
		public const string ConditionalTrivia = "^#if [!\\w\\s|&()]+$";

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

		static CompilationUnitSyntax PrepareForVisitor (string chunk, bool stripContainingLines)
		{
			// Skip the #if and #else or #end for analysis by roslyn
			string section = stripContainingLines ? String.Join ('\n', chunk.SplitLines ().Skip (1).SkipLast (1)) : chunk;

			SyntaxTree tree = CSharpSyntaxTree.ParseText (section);

			return tree.GetCompilationUnitRoot ();
		}

		public static bool ChunkContainsOnlyAttributes (string chunk, bool stripContainingLines = true)
		{
			var root = PrepareForVisitor (chunk, stripContainingLines);
			var visitor = new OnlyContainsTriviaVisitor ();
			root!.Accept (visitor);
			return visitor.EverythingIsAvailabilityAttribute;
		}

		public static bool ChunkContainsAnyAvailabilityAttributes (string chunk, bool stripContainingLines = true)
		{
			var root = PrepareForVisitor (chunk, stripContainingLines);
			var visitor = new HasAnyAvailabilityVisitor ();
			root!.Accept (visitor);
			return visitor.HasAnyAvailability;
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
	class AttributeStripper {
		enum State {
			InsideInterestBlock,
			WaitingForPotentialElseNotNetBlock,
			InsideUnrelatedBlock,
		}

		StringBuilder File = new StringBuilder ();
		StringBuilder Chunk = new StringBuilder ();
		Stack<State> States = new Stack<State> ();

		bool HasCurrentState => States.Count > 0;

		// Are we directly nested within a InsideInterestBlock (possibly within a #if as well)
		bool InsideInterestBlock => States.Any (x => x == State.InsideInterestBlock);
		State GetCurrentState (string context) => States.TryPeek (out State s) ? s : throw new InvalidOperationException ($"No state found: {context}");

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

		void Reset ()
		{
			File.Clear ();
			Chunk.Clear ();
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
				switch (StripperHelpers.TrimLine (line)) {
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
				if (!StripperHelpers.ChunkContainsOnlyAttributes (Chunk!.ToString ())) {
					FileAppend (Chunk.ToString (), true);
					return;
				}

				switch (StripperHelpers.TrimLine (current)) {
				case "#else":
					// Invert the first if and drop the rest
					FileAppend ("#if !NET");
					break;
				case "#endif":
					// If our first line is #else then replace with #endif, otherwise drop everything
					var lines = Chunk!.ToString ().SplitLines ();
					if (StripperHelpers.TrimLine (lines.First ()) == "#else") {
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
	}

	// Processing code with many #condition blocks is difficult to get right, and they aren't truly needed
	// as we'll be adding them back with gobs of code. So strip #if NET/#if !NET and associated #else/#endif
	// meant to run after AttributeStripper step. Only strip if the contents are only attributes.
	class ConditionBlockStripper {
		enum State {
			InsideInterestBlock,
			InsideUnrelatedBlock,
		}

		Stack<State> States = new Stack<State> ();
		State GetCurrentState (string context) => States.TryPeek (out State s) ? s : throw new InvalidOperationException ($"No state found: {context}");

		StringBuilder File = new StringBuilder ();
		StringBuilder Chunk = new StringBuilder ();
		// Write each line in Chunk and ConditionalLessChunk, except for #if/#else/#endif
		// If we determine the block is ChunkContainsOnlyAttributes, then write this instead of Chunk
		protected StringBuilder ConditionalLessChunk = new StringBuilder ();

		bool InsideInterestBlock => States.Any (x => x == State.InsideInterestBlock);

		void Write (string line, bool isConditionDefine, bool skipNewLine = false)
		{
			if (InsideInterestBlock) {
				Chunk.Append (line);
				if (!skipNewLine) {
					Chunk.Append (Environment.NewLine);
				}
				if (!isConditionDefine) {
					ConditionalLessChunk.Append (line);
					if (!skipNewLine) {
						ConditionalLessChunk.Append (Environment.NewLine);
					}
				}
			} else {
				FileAppend (line, skipNewLine);
			}
		}

		void FileAppend (string line, bool skipNewLine = false)
		{
			File.Append (line);
			if (!skipNewLine) {
				File.Append (Environment.NewLine);
			}
		}

		void Reset ()
		{
			File.Clear ();
			Chunk.Clear ();
			States.Clear ();
			ConditionalLessChunk.Clear ();
		}

		public string StripText (string text)
		{
			Reset ();

			foreach (var line in text.SplitLines ()) {
				switch (StripperHelpers.TrimLine (line)) {
				case "#if NET":
				case "#if !NET":
					States.Push (State.InsideInterestBlock);
					Write (line, true);
					break;
				case string s when s.StartsWith ("#if"):
					States.Push (State.InsideUnrelatedBlock);
					Write (line, false);
					break;
				case "#else":
					Write (line, GetCurrentState ("#else") == State.InsideInterestBlock);
					break;
				case "#endif":
					switch (GetCurrentState ("#endif")) {
					case State.InsideInterestBlock:
						Write (line, true);
						if (StripperHelpers.ChunkContainsOnlyAttributes (Chunk!.ToString ())) {
							FileAppend (ConditionalLessChunk.ToString (), true);
						} else {
							FileAppend (Chunk.ToString (), true);
						}
						Chunk.Clear ();
						ConditionalLessChunk.Clear ();
						break;
					case State.InsideUnrelatedBlock:
						Write (line, false);
						break;
					}
					States.Pop ();
					break;
				default:
					Write (line, false);
					break;
				}
			}
			return File.ToString ();
		}


	}

	// Ok, this really isn't technically "Stripping", but it fits the pattern of line by line parsing
	class VerifyStripper {

		StringBuilder File = new StringBuilder ();

		public void Reset ()
		{
			File.Clear ();
		}

		public string StripText (string text, string? verboseConditional)
		{
			Reset ();

			// First see if there are a set of unique defines that exist for the file, if so skip verify that file
			// Skip NET and !NET since very likely those are attributes that will be stripped
			var uniqueDefines = (new DefineParser (verboseConditional)).FindUniqueDefinesThatCoverAll (text, ignoreNETDefines: true);
			if (uniqueDefines != null) {
				return text;
			}

			bool addedVerify = false;
			foreach (var line in text.SplitLines ()) {
				if (Regex.IsMatch (line, StripperHelpers.PositiveConditionalTrivia)) {
					if (!line.Contains ("XAMCORE_4_0")) {
						// Find the last line and count the number of leading tabs, and prepend that to roughly get right tabbing
						// TODO - Super non-performant...
						var whitespace = File.ToString ().SplitLines ().LastOrDefault ()?.LeadingWhitespace () ?? "";
						File.AppendLine ($"{whitespace}[Verify] // Nested Conditionals are not always correctly processed");
						addedVerify = true;
					}
				}
				File.AppendLine (line);
			}

			return addedVerify ? $"// Verify found under defines: {String.Join (' ', (new DefineParser (verboseConditional)).ParseAllDefines (text))} \n" + File.ToString () : File.ToString ();
		}
	}

	// This "rewriter" verified that all contents of a #if or #else block are attributes and are safe to remove
	// It in theory could be a visitor, but I had trouble making that work.
	class OnlyContainsTriviaVisitor : CSharpSyntaxRewriter {
		public bool EverythingIsAvailabilityAttribute = true;

		public OnlyContainsTriviaVisitor ()
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
				switch (StripperHelpers.TrimLine (node.ToString ())) {
				case "internal":
				case "public":
					// Roslyn shows all of this as "incomplete" but special case internal/public
					EverythingIsAvailabilityAttribute = false;
					break;
				default:
					return base.Visit (node);
				}
				break;
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
					case "Obsolete": // Assume all Obsolete are safe to remove
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

	class HasAnyAvailabilityVisitor : CSharpSyntaxRewriter {
		public bool HasAnyAvailability = false;

		public HasAnyAvailabilityVisitor ()
		{
		}

		public override SyntaxNode? VisitAttributeArgumentList (AttributeArgumentListSyntax node)
		{
			return base.VisitAttributeArgumentList (node);
		}

		public override SyntaxNode? Visit (SyntaxNode? node)
		{
			switch (node?.GetType ().ToString ()) {
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
						HasAnyAvailability = true;
						break;
					case "Obsolete":
						if (attribute.ArgumentList?.Arguments.Count > 1) {
							// Obsolete can have DiagnosticId/UrlFormat which are NET6 specific :(
							HasAnyAvailability = true;
						}
						break;
					}
				}
				return node;
			default:
				return base.Visit (node);
			}
		}
	}
}
