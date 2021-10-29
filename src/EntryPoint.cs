using System;
using System.Collections.Generic;
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
            string path = null;

            OptionSet os = new OptionSet()
            {
                { "h|?|help", "Displays the help", v => requestedAction = ActionType.Help },
                { "l|list-files", "Lists files considered for processing", v => requestedAction = ActionType.ListFilesToProcess },
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
                Console.Error.WriteLine("Could not parse the command line arguments: {0}", e.Message);
                return;
            }
        }

        static void ShowHelp(OptionSet os)
        {
            Console.WriteLine("mellite [options] path");
            os.WriteOptionDescriptions(Console.Out);
        }
    }
}
