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

	public class ProcessOptions {
		public ProcessSteps Step = ProcessSteps.ConvertXamarinAttributes;
		public List<string> Defines = new List<string> ();
		public string? VerboseConditional = null;
		public bool AllowErrors = false;
		public string? AssemblyPath = null;
		public string? AddDefaultIntroducedPath = null;

		public void Validate ()
		{
			if (AddDefaultIntroducedPath != null && AssemblyPath == null) {
				throw new InvalidOperationException ("add-default-introduced is only valid when harvest-assembly is set");
			}
		}
	}

	public static class Processor {
		public static void ProcessFile (string path, ProcessOptions options)
		{
			// Special case hack - OpenTK has some inline C++ defines in a /* */ comment block that are confusing the parser
			// and there are literally zero defines we care about, so just early return
			if (path.EndsWith ("OpenGLES/OpenTK/Platform/Windows/API.cs")) {
				return;
			}

			try {
				var text = ProcessText (File.ReadAllText (path), options, path);
				if (text != null) {
					File.WriteAllText (path, text);
				}
			} catch (Exception e) {
				Console.Error.WriteLine ($"Fatal Error while processing {path}: {e}");
				throw;
			}
		}

		public static string? ProcessText (string text, ProcessOptions options, string? path = null)
		{
			options.Validate ();
			try {
				switch (options.Step) {
				case ProcessSteps.ConvertXamarinAttributes:
					AssemblyHarvestInfo? assemblyInfo = null;
					if (options.AssemblyPath != null) {
						var harvester = new AssemblyHarvester ();
						assemblyInfo = harvester.Harvest (options.AssemblyPath, options.AddDefaultIntroducedPath);
					}
					return Converter.Convert (text, options.Defines, options.VerboseConditional, assemblyInfo);
				case ProcessSteps.StripExistingNET6Attributes:
					return (new AttributeStripper ()).StripText (text);
				case ProcessSteps.StripConditionBlocks:
					return (new ConditionBlockStripper ()).StripText (text);
				case ProcessSteps.StripVerify:
					return (new VerifyStripper ()).StripText (text, options.VerboseConditional);
				case ProcessSteps.ListDefinesDetected: {
					var detectedDefines = (new DefineParser (options.VerboseConditional)).ParseAllDefines (text);
					Console.WriteLine (detectedDefines != null ? $"  Found Defines:\n\t{String.Join ("\n\t", detectedDefines)}" : "Error parsing defines.");

					var uniqueDefines = (new DefineParser (options.VerboseConditional)).FindUniqueDefinesThatCoverAll (text, ignoreNETDefines: false);
					Console.WriteLine ();
					Console.WriteLine (uniqueDefines != null ? $"Found Unique Defines:\n{String.Join (' ', uniqueDefines)}" : "No set of unique defines");
					return null;
				}
				case ProcessSteps.ListDefineUnresolvableFiles: {
					if ((new DefineParser (options.VerboseConditional)).FindUniqueDefinesThatCoverAll (text, ignoreNETDefines: false) == null) {
						Console.WriteLine ($"Could not process: {path}");
						var detectedDefines = (new DefineParser (options.VerboseConditional)).ParseAllDefines (text);
						Console.WriteLine (detectedDefines != null ? $"  Found Defines:\n\t{String.Join ("\n\t", detectedDefines)}" : "Error parsing defines.");
					}
					return null;
				}
				default:
					return null;
				}
			} catch (Exception e) {
				if (options.AllowErrors) {
					Console.Error.WriteLine ($"While processing '{(String.IsNullOrEmpty (path) ? "unknown" : path)}' we hit exception: '{e}'");
					return null;
				} else {
					throw;
				}
			}
		}
	}
}
