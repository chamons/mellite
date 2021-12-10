using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using mellite.Utilities;

namespace mellite {
	public class DefineParser {
		StringBuilder Chunk = new StringBuilder ();
		HashSet<string> BlocksWithAttributes = new HashSet<string> ();
		Stack<string> Conditionals = new Stack<string> ();
		string? CurrentConditional => Conditionals.FirstOrDefault ();

		void Reset ()
		{
			Chunk.Clear ();
			BlocksWithAttributes.Clear ();
			Conditionals.Clear ();
		}

		static string Invert (string s) => s.StartsWith ("!") ? s.Substring (1) : "!" + s;

		// Split the parts of a conditional A && B || C => A, B, C
		public static List<string> SplitConditionalParts (string define)
		{
			// Since we only care about the list of parts, strip all parans first
			define = define.Replace ("(", "");
			define = define.Replace (")", "");

			var parts = new List<string> ();
			while (define.Contains ("||") || define.Contains ("&&")) {
				int orIndex = define.IndexOf ("||");
				int andIndex = define.IndexOf ("&&");
				int index;
				if (orIndex == -1) {
					index = andIndex;
				} else if (andIndex == -1) {
					index = orIndex;
				} else {
					index = Math.Min (andIndex, orIndex);
				}
				parts.Add (define.Substring (0, index - 1).Trim ());
				define = define.Substring (index + 2);
			}
			// Add the remaining bit
			parts.Add (define.Trim ());
			return parts;
		}

		static string ProcessPotentialSpecialCase (string define)
		{
			switch (define) {
			default:
				return define;
			}
		}

		// Find every line that looks like an availability attribute, and determine what block they are in
		// Returns null if there are complex defines (A && B) or those that conflict (one block in MAC and one in ELSE), else the list of defines to  
		public List<string>? FindUniqueDefinesThatCoverAll (string text)
		{
			var defines = ParseAllDefines (text).ToList ();
			var conflictingDefines = defines.Where (d => defines.Contains (Invert (d)));
			// conflictingDefines = conflictingDefines.Select (d => ProcessPotentialSpecialCase (d));

			if (conflictingDefines.Any ()) {
				return null;
			}
			return defines.ToList ();
		}

		// List all detected defines, even if they conflict.
		public List<string> ParseAllDefines (string text)
		{
			int lineCount = 0;
			foreach (var line in text.SplitLines ()) {
				switch (StripperHelpers.TrimLine (line)) {
				case "#else":
					CheckThenClearCurrentBlock ();
					// Remove the ! from a block if it has one, else add one
					var newConditional = Invert (CurrentConditional!);
					Conditionals.Pop ();
					Conditionals.Push (newConditional);
					break;
				case "#endif":
					CheckThenClearCurrentBlock ();
					Conditionals.Pop ();
					break;
				case string s: {
					if (Regex.IsMatch (s, StripperHelpers.ConditionalTrivia)) {
						Conditionals.Push (s.Split ("#if") [1].Trim ());
					} else if (CurrentConditional != null) {
						Chunk.Append (s);
					}
					break;
				}
				}
				lineCount += 1;
			}
			if (Conditionals.Any ())
				throw new InvalidOperationException ("DefineParser ends with unclosed conditional");

			// Split any A || B to [A, B]
			return BlocksWithAttributes.SelectMany (d => SplitConditionalParts (d)).ToList ();
		}

		void CheckThenClearCurrentBlock ()
		{
			if (!Conditionals.Any ())
				throw new InvalidOperationException ("CheckThenClearCurrentBlock but no conditional");

			bool hasAttributes = StripperHelpers.ChunkContainsAnyAvailabilityAttributes (Chunk.ToString (), stripContainingLines: false);

			if (hasAttributes) {
				// Process for every conditional in our stack
				foreach (var conditional in Conditionals) {
					BlocksWithAttributes.Add (conditional);
				}
			}
			Chunk.Clear ();
		}
	}
}
