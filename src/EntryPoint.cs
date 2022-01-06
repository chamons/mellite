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
			ProcessSteps steps = ProcessSteps.ConvertXamarinAttributes;
			List<string> defines = new List<string> ();
			string? verboseConditional = null;

			OptionSet os = new OptionSet ()
			{
				{ "h|?|help", "Displays the help", v => requestedAction = ActionType.Help },
				{ "l|list-files", "Lists files considered for processing", v => requestedAction = ActionType.ListFilesToProcess },
				{ "i|ignore=", "Directories (relative to root) not to be considered for processing", i => locatorOptions.Ignore.Add (i) },
				{ "strip-attributes", "Instead of converting attributes, strip existing NET/!NET blocks of attributes", i => steps = ProcessSteps.StripExistingNET6Attributes },
				{ "strip-blocks", "Instead of converting attributes, strip existing NET/!NET blocks", i => steps = ProcessSteps.StripConditionBlocks },
				{ "strip-verify", "Instead of converting attributes, 'strip' blocks looking for required [Verify] when the tool can be confused.", i => steps = ProcessSteps.StripVerify },
				{ "detect-defines", "Detect the full set of defines needed to process all availability attributes in a file", i => steps = ProcessSteps.ListDefinesDetected },
				{ "detect-unresolvable", "Detect the files that can not be resolved due to complex or conflicting defines", i => steps = ProcessSteps.ListDefineUnresolvableFiles },
				{ "d|define=", "Set of defines to enable when parsing code.", d => defines.Add(d) },
				{ "v|verbose-conditional=", "When using tools that analyze conditionals, output the line numbers of blocks that triggered this conditional.", v => verboseConditional = v },
				{ "ignore-root", "Only process files in subdirectories of the target directory, do not process root level files", _ => locatorOptions.IgnoreRoot = true },
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
					Processor.ProcessFile (file, steps, defines, verboseConditional);
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
