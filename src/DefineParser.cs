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

		string? VerboseConditional;
		int LineCount = 0;

		public DefineParser (string? verboseConditional)
		{
			VerboseConditional = verboseConditional;
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

		// Find every line that looks like an availability attribute, and determine what block they are in
		// Returns null if they conflict (one block in MAC and one in ELSE), else the list of defines to 
		public List<string>? FindUniqueDefinesThatCoverAll (string text, bool ignoreNETDefines)
		{
			var defines = ParseAllDefines (text);

			// We explicitly do not care about WATCH when it comes to conflicts
			defines = defines.Where (d => d != "WATCH").ToList ();

			// We conditionally does not care about NET/!NET
			if (ignoreNETDefines) {
				defines = defines.Where (d => d != "NET" && d != "!NET").ToList ();
			}

			if (defines.Any (d => defines.Contains (Invert (d)))) {
				return null;
			}

			// Now that we know that there aren't conflicts, return the list of things to define:
			// Those without ! in front (since we by default don't define things)
			return defines.Where (d => !d.StartsWith ("!")).ToList ();
		}

		// List all detected defines, even if they conflict.
		public List<string> ParseAllDefines (string text)
		{
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
						if (Conditionals.Any ()) {
							CheckThenClearCurrentBlock ();
						}
						Conditionals.Push (s.Split ("#if") [1].Trim ());
					} else if (CurrentConditional != null) {
						Chunk.Append (s);
					}
					break;
				}
				}
				LineCount += 1;
			}
			if (Conditionals.Any ())
				throw new InvalidOperationException ("DefineParser ends with unclosed conditional");

			// Split any A || B to [A, B]. Use a HashSet to de-deduplicate 
			// TODO - This is too conservative, as we could have !A || B and declare we need to set B, even though
			// A isn't set. 
			return new HashSet<string> (BlocksWithAttributes.SelectMany (d => SplitConditionalParts (d))).ToList ();
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
					if (VerboseConditional != null && SplitConditionalParts (conditional).Any (c => c == VerboseConditional)) {
						Console.Error.WriteLine (LineCount);
					}
				}
			}
			Chunk.Clear ();
		}
	}
}
