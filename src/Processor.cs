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
		StripConditionBlocks,
	};

	public static class Processor {
		public static void ProcessFile (string path, ProcessSteps steps)
		{
			var text = ProcessText (File.ReadAllText (path), steps);
			File.WriteAllText (path, text);
		}

		public static string ProcessText (string text, ProcessSteps steps)
		{
			switch (steps) {
			case ProcessSteps.ConvertXamarinAttributes:
				SyntaxTree tree = CSharpSyntaxTree.ParseText (text);

				CompilationUnitSyntax root = tree.GetCompilationUnitRoot ();

				root = (CompilationUnitSyntax) root!.Accept (new AttributeConverterVisitor ())!;
				return root!.ToFullString ();
			case ProcessSteps.StripExistingNET6Attributes:
				return (new AttributeStripper ()).StripText (text);
			case ProcessSteps.StripConditionBlocks:
				return (new ConditionBlockStripper ()).StripText (text);
			default:
				return text;
			}

		}
	}
}
