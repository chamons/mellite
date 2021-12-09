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
		StripVerify,
		ListDefinesDetected,
		ListDefineUnresolvableFiles,
	};

	public static class Processor {
		public static void ProcessFile (string path, ProcessSteps steps, List<string> defines)
		{
			try {
				var text = ProcessText (File.ReadAllText (path), steps, defines, path);
				if (text != null) {
					File.WriteAllText (path, text);
				}
			} catch (Exception e) {
				Console.Error.WriteLine ($"Fatal Error while processing {path}: {e}");
				throw;
			}
		}

		public static string? ProcessText (string text, ProcessSteps steps, List<string> defines, string? path = null)
		{
			switch (steps) {
			case ProcessSteps.ConvertXamarinAttributes:
				CSharpParseOptions options = new CSharpParseOptions (preprocessorSymbols: defines);
				SyntaxTree tree = CSharpSyntaxTree.ParseText (text, options);

				CompilationUnitSyntax root = tree.GetCompilationUnitRoot ();

				root = (CompilationUnitSyntax) root!.Accept (new AttributeConverterVisitor ())!;
				return root!.ToFullString ();
			case ProcessSteps.StripExistingNET6Attributes:
				return (new AttributeStripper ()).StripText (text);
			case ProcessSteps.StripConditionBlocks:
				return (new ConditionBlockStripper ()).StripText (text);
			case ProcessSteps.StripVerify:
				return (new VerifyStripper ()).StripText (text);
			case ProcessSteps.ListDefinesDetected: {
				var detectedDefines = (new DefineParser ()).ParseAllDefines (text);
				Console.WriteLine (detectedDefines != null ? $"Found Defines:\n{String.Join ('\n', detectedDefines)}" : "Error parsing defines.");
				var uniqueDefines = (new DefineParser ()).FindUniqueDefinesThatCoverAll (text);
				Console.WriteLine ();
				Console.WriteLine (uniqueDefines != null ? $"Found Unique Defines:\n{String.Join (' ', uniqueDefines)}" : "No set of unique defines");
				return null;
			}
			case ProcessSteps.ListDefineUnresolvableFiles: {
				if ((new DefineParser ()).FindUniqueDefinesThatCoverAll (text) == null) {
					Console.WriteLine (path);
					var detectedDefines = (new DefineParser ()).ParseAllDefines (text);
					Console.WriteLine (detectedDefines != null ? $"Found Defines:\n{String.Join ('\n', detectedDefines)}" : "Error parsing defines.");
				}
				return null;
			}
			default:
				return null;
			}

		}
	}
}
