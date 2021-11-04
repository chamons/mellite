using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace mellite {
	public class LocatorOptions {
		public List<string> Ignore = new ();
	}

	public static class Locator {
		public static List<string> LocateFiles (string root) => LocateFiles (root, new LocatorOptions ());

		public static List<string> LocateFiles (string root, LocatorOptions options)
		{
			var ignore = options.Ignore.Select (x => Path.Combine (root, x)).ToList ();

			var allFiles = Directory.EnumerateFiles (root, "*.cs", new EnumerationOptions () { RecurseSubdirectories = true });

			return allFiles.Where (file => !ignore.Any (i => file.StartsWith (i))).ToList ();
		}
	}
}
