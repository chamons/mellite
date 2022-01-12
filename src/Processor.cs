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
		public static void ProcessFile (string path, ProcessSteps steps, List<string> defines, string? assemblyPath, string? verboseConditional, bool allowErrors)
		{
			// Special case hack - OpenTK has some inline C++ defines in a /* */ comment block that are confusing the parser
			// and there are literally zero defines we care about, so just early return
			if (path.EndsWith ("OpenGLES/OpenTK/Platform/Windows/API.cs")) {
				return;
			}

			try {
				if (assemblyPath != null) {
					AssemblyHarvester harvester = new AssemblyHarvester ();
					var info = harvester.Harvest (assemblyPath);
				}
				var text = ProcessText (File.ReadAllText (path), steps, defines, assemblyPath, allowErrors, path, verboseConditional);
				if (text != null) {
					File.WriteAllText (path, text);
				}
			} catch (Exception e) {
				Console.Error.WriteLine ($"Fatal Error while processing {path}: {e}");
				throw;
			}
		}

		public static string? ProcessText (string text, ProcessSteps steps, List<string> defines, string? assemblyPath, bool allowErrors, string? path = null, string? verboseConditional = null)
		{
			try {
				switch (steps) {
				case ProcessSteps.ConvertXamarinAttributes:
					AssemblyHarvestInfo? assemblyInfo = null;
					if (assemblyPath != null) {
						var harvester = new AssemblyHarvester ();
						assemblyInfo = harvester.Harvest (assemblyPath);
					}
					return Converter.Convert (text, defines, verboseConditional, assemblyInfo);
				case ProcessSteps.StripExistingNET6Attributes:
					return (new AttributeStripper ()).StripText (text);
				case ProcessSteps.StripConditionBlocks:
					return (new ConditionBlockStripper ()).StripText (text);
				case ProcessSteps.StripVerify:
					return (new VerifyStripper ()).StripText (text, verboseConditional);
				case ProcessSteps.ListDefinesDetected: {
					var detectedDefines = (new DefineParser (verboseConditional)).ParseAllDefines (text);
					Console.WriteLine (detectedDefines != null ? $"  Found Defines:\n\t{String.Join ("\n\t", detectedDefines)}" : "Error parsing defines.");

					var uniqueDefines = (new DefineParser (verboseConditional)).FindUniqueDefinesThatCoverAll (text, ignoreNETDefines: false);
					Console.WriteLine ();
					Console.WriteLine (uniqueDefines != null ? $"Found Unique Defines:\n{String.Join (' ', uniqueDefines)}" : "No set of unique defines");
					return null;
				}
				case ProcessSteps.ListDefineUnresolvableFiles: {
					if ((new DefineParser (verboseConditional)).FindUniqueDefinesThatCoverAll (text, ignoreNETDefines: false) == null) {
						Console.WriteLine ($"Could not process: {path}");
						var detectedDefines = (new DefineParser (verboseConditional)).ParseAllDefines (text);
						Console.WriteLine (detectedDefines != null ? $"  Found Defines:\n\t{String.Join ("\n\t", detectedDefines)}" : "Error parsing defines.");
					}
					return null;
				}
				default:
					return null;
				}
			} catch (Exception e) {
				if (allowErrors) {
					Console.Error.WriteLine ($"While processing '{(String.IsNullOrEmpty (path) ? "unknown" : path)}' we hit exception: '{e}'");
					return null;
				} else {
					throw;
				}
			}
		}
	}
}
