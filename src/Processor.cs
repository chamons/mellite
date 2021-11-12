using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using mellite.Utilities;

namespace mellite {
	public enum ProcessSteps {
		ConvertXamarinAttributes,
		StripExistingNET6Attributes,
	};

	public static class Processor {
		public static void ProcessFile (string path, ProcessSteps steps)
		{
			var text = ProcessText (File.ReadAllText (path), steps);
			File.WriteAllText (path, text);
		}

		public static string ProcessText (string text, ProcessSteps steps)
		{
			if (steps == ProcessSteps.StripExistingNET6Attributes) {
				// First process the stripping with NET defined, then again without
				return ProcessTextCore (ProcessTextCore (text, steps, true), steps, false);
			} else {
				return ProcessTextCore (text, steps, false);
			}
		}

		// For some cases we have to both NET and !NET to process both sides, so just do all of the work twice
		static string ProcessTextCore (string text, ProcessSteps steps, bool defineNet)
		{
			CSharpParseOptions options = new CSharpParseOptions (preprocessorSymbols: defineNet ? new string [] { "NET" } : new string [] { });
			SyntaxTree tree = CSharpSyntaxTree.ParseText (text, options);

			CompilationUnitSyntax root = tree.GetCompilationUnitRoot ();

			switch (steps) {
			case ProcessSteps.ConvertXamarinAttributes:
				root = (CompilationUnitSyntax) root!.Accept (new AttributeConverterVisitor ())!;
				break;
			case ProcessSteps.StripExistingNET6Attributes:
				root = (CompilationUnitSyntax) root!.Accept (new MarkingAttributeStripperVisitor (defineNet))!;
				root = (CompilationUnitSyntax) root!.Accept (new RemoveMarkedTriviaStripperVisitor ())!;
				break;
			}

			return root!.ToFullString ();
		}
	}
}
