using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace mellite {
	public class LocatorOptions {
		public List<string> Ignore = new ();
		public bool IgnoreRoot = false;
	}

	public static class Locator {
		public static List<string> LocateFiles (string root) => LocateFiles (root, new LocatorOptions ());

		public static List<string> LocateFiles (string root, LocatorOptions options)
		{
			var ignore = options.Ignore.Select (x => Path.Combine (root, x)).ToList ();
			if (Directory.Exists (root)) {
				if (options.IgnoreRoot) {
					ignore.AddRange (Directory.EnumerateFiles (root, "*.cs"));
				}

				var allFiles = Directory.EnumerateFiles (root, "*.cs", new EnumerationOptions () { RecurseSubdirectories = true });
				return allFiles.Where (file => !ignore.Any (i => file.StartsWith (i))).ToList ();
			} else if (File.Exists (root)) {
				return new List<string> () { root };
			} else {
				throw new InvalidOperationException ($"{root} is not found as a directory or a file.");
			}
		}
	}
}
