﻿using System;
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

			OptionSet os = new OptionSet ()
			{
				{ "h|?|help", "Displays the help", v => requestedAction = ActionType.Help },
				{ "l|list-files", "Lists files considered for processing", v => requestedAction = ActionType.ListFilesToProcess },
				{ "i|ignore=", "Directories (relative to root) not to be considered for processing", i => locatorOptions.Ignore.Add (i) },
				{ "strip-attributes", "Instead of converting attributes, strip existing NET/!NET blocks of attributes", i => steps = ProcessSteps.StripExistingNET6Attributes },
				{ "strip-blocks", "Instead of converting attributes, strip existing NET/!NET blocks of attributes", i => steps = ProcessSteps.StripConditionBlocks },

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

			if (!Directory.Exists (path)) {
				Console.Error.WriteLine ($"Could not find directory: {path}");
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
					Processor.ProcessFile (file, steps);
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
