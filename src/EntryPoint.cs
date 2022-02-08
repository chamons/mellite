using System;
using System.Collections.Generic;
using System.IO;

using Mono.Options;

namespace mellite {
	public enum ActionType {
		Help,
		ListFilesToProcess,
		Process
	}

	class EntryPoint {
		static void Main (string [] args)
		{
			ActionType requestedAction = ActionType.Process;
			string? path = null;
			LocatorOptions locatorOptions = new LocatorOptions ();
			ProcessOptions processOptions = new ProcessOptions ();

			OptionSet os = new OptionSet ()
			{
				{ "h|?|help", "Displays the help", v => requestedAction = ActionType.Help },
				{ "l|list-files", "Lists files considered for processing", v => requestedAction = ActionType.ListFilesToProcess },
				{ "i|ignore=", "Directories (relative to root) not to be considered for processing", i => locatorOptions.Ignore.Add (i) },
				{ "strip-attributes", "Instead of converting attributes, strip existing NET/!NET blocks of attributes", i => processOptions.Step = ProcessSteps.StripExistingNET6Attributes },
				{ "strip-blocks", "Instead of converting attributes, strip existing NET/!NET blocks", i => processOptions.Step = ProcessSteps.StripConditionBlocks },
				{ "strip-verify", "Instead of converting attributes, 'strip' blocks looking for required [Verify] when the tool can be confused.", i => processOptions.Step = ProcessSteps.StripVerify },
				{ "detect-defines", "Detect the full set of defines needed to process all availability attributes in a file", i => processOptions.Step = ProcessSteps.ListDefinesDetected },
				{ "detect-unresolvable", "Detect the files that can not be resolved due to complex or conflicting defines", i => processOptions.Step = ProcessSteps.ListDefineUnresolvableFiles },
				{ "d|define=", "Set of defines to enable when parsing code.", d => processOptions.Defines.Add(d) },
				{ "v|verbose-conditional=", "When using tools that analyze conditionals, output the line numbers of blocks that triggered this conditional.", v => processOptions.VerboseConditional = v },
				{ "ignore-root", "Only process files in subdirectories of the target directory, do not process root level files", _ => locatorOptions.IgnoreRoot = true },
				{ "harvest-assembly=", "Process assembly to provide additional context for partial only classes", a => processOptions.AssemblyPath = a },
				{ "allow-errors", "Instead of crashing on first fatal error, just print and continue.", a => processOptions.AllowErrors = true },
				{ "add-default-introduced", "When processing the harvested assembly treat types with no introduced attribute as ios/mac based upon namespace.", a => processOptions.AddDefaultIntroduced = true },
			};

			try {
				IList<string> unprocessed = os.Parse (args);
				if (requestedAction == ActionType.Help || unprocessed.Count != 1) {
					ShowHelp (os);
					return;
				}
				path = unprocessed [0];
			} catch (Exception e) {
				Console.Error.WriteLine ($"Could not parse the command line arguments: {e.Message}");
				return;
			}

			List<string> files = Locator.LocateFiles (path, locatorOptions);

			switch (requestedAction) {
			case ActionType.ListFilesToProcess: {
				foreach (var file in files) {
					Console.WriteLine (file);
				}
				break;
			}
			case ActionType.Process: {
				foreach (var file in files) {
					Processor.ProcessFile (file, processOptions);
				}
				break;
			}
			case ActionType.Help:
				throw new InvalidOperationException (); // Should have been handled above or missed
			}
		}

		static void ShowHelp (OptionSet os)
		{
			Console.WriteLine ("mellite [options] path");
			os.WriteOptionDescriptions (Console.Out);
		}
	}
}
