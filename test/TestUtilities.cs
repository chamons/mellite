using System;
using System.Collections.Generic;

using Xunit;

namespace mellite.tests {
	public static class TestUtilities {
		public static string GetConditionalAttributeBlock (string xamarinAttribute, string newAttribute, int spaceCount, int? newAttributeSpaceCount = null)
		{
			newAttributeSpaceCount ??= spaceCount;
			return $@"#if NET
{new String (' ', (int) newAttributeSpaceCount)}{newAttribute}
#else
{new String (' ', spaceCount)}{xamarinAttribute}
#endif";

		}

		public static string GetTestProgram (string body) => $@"using System;
using ObjCRuntime;

namespace binding
{{
{body}
}}
";

		public static void TestProcess (string original, ProcessSteps step, string expected, List<string>? defines = null, string? assemblyPath = null)
		{
			var options = new ProcessOptions { Step = step, Defines = defines ?? new List<string> (), AssemblyPath = assemblyPath };
			TestProcess (original, expected, options);
		}

		public static void TestProcess (string original, string expected, ProcessOptions options)
		{
			string processedText = Processor.ProcessText (original, options)!;
			if (Environment.GetEnvironmentVariable ("V") == "1") {
				Console.WriteLine (processedText);
				Console.WriteLine ();
				Console.WriteLine (expected);

			}
			Assert.Equal (expected, processedText, ignoreLineEndingDifferences: true);
		}
	}
}
