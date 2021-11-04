using System;
using System.IO;
using System.Collections.Generic;
using mellite;

using Xunit;

namespace mellite.tests {
	public class LocatorTests {
		[Fact]
		public void ProcessesRecursively ()
		{
			var cache = Cache.CreateTemporaryDirectory ();

			var src = Path.Combine (cache, "src");
			Directory.CreateDirectory (src);

			File.WriteAllText (Path.Combine (src, "foo.cs"), "");
			File.WriteAllText (Path.Combine (src, "bar.cs"), "");
			File.WriteAllText (Path.Combine (src, "Makefile"), "");

			var appkit = Path.Combine (src, "AppKit");
			Directory.CreateDirectory (appkit);
			File.WriteAllText (Path.Combine (appkit, "buzz.cs"), "");
			File.WriteAllText (Path.Combine (appkit, "README.txt"), "");

			var results = new HashSet<string> (Locator.LocateFiles (src));
			Assert.Equal (3, results.Count);
			Assert.Contains (Path.Combine (src, "foo.cs"), results);
			Assert.Contains (Path.Combine (src, "bar.cs"), results);
			Assert.Contains (Path.Combine (appkit, "buzz.cs"), results);
		}

		[Fact]
		public void SkipsIgnoreDirectories ()
		{
			var cache = Cache.CreateTemporaryDirectory ();

			var src = Path.Combine (cache, "src");
			Directory.CreateDirectory (src);
			File.WriteAllText (Path.Combine (src, "foo.cs"), "");

			var build = Path.Combine (src, "builds");
			Directory.CreateDirectory (build);
			File.WriteAllText (Path.Combine (build, "bar.cs"), "");

			var results = new HashSet<string> (Locator.LocateFiles (src, new LocatorOptions () { Ignore = new List<string> { "build" } }));
			Assert.Single (results);
			Assert.Contains (Path.Combine (src, "foo.cs"), results);
		}
	}
}
