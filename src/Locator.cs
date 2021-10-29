using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace mellite
{
    public static class Locator
    {
        public static List<string> LocateFiles(string root)
        {
            return Directory.EnumerateFiles(root, "*.cs", new EnumerationOptions() { RecurseSubdirectories = true }).ToList();
        }
    }
}