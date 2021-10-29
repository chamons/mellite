using System;
using System.Collections.Generic;
using System.IO;

using Mono.Options;

namespace mellite
{
    public enum ActionType
    {
        Help,
        ListFilesToProcess,
        Process
    }

    class EntryPoint
    {
        static void Main(string[] args)
        {
            ActionType requestedAction = ActionType.ListFilesToProcess;
            string? path = null;
            LocatorOptions locatorOptions = new LocatorOptions();

            OptionSet os = new OptionSet()
            {
                { "h|?|help", "Displays the help", v => requestedAction = ActionType.Help },
                { "l|list-files", "Lists files considered for processing", v => requestedAction = ActionType.ListFilesToProcess },
                { "i|ignore=", "Directories (relative to root) not to be considered for processing", i => locatorOptions.Ignore.Add (i) },
            };

            try
            {
                IList<string> unprocessed = os.Parse(args);
                if (requestedAction == ActionType.Help || unprocessed.Count != 1)
                {
                    ShowHelp(os);
                    return;
                }
                path = unprocessed[0];
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Could not parse the command line arguments: {e.Message}");
                return;
            }

            if (!Directory.Exists(path))
            {
                Console.Error.WriteLine($"Could not find directory: {path}");
                return;
            }

            List<string> files = Locator.LocateFiles(path, locatorOptions);

            if (requestedAction == ActionType.ListFilesToProcess)
            {
                foreach (var file in files)
                {
                    Console.WriteLine(file);
                }
            }

        }

        static void ShowHelp(OptionSet os)
        {
            Console.WriteLine("mellite [options] path");
            os.WriteOptionDescriptions(Console.Out);
        }
    }
}
