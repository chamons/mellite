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

		public static void TestProcess (string original, ProcessSteps steps, string expected, List<string>? defines = null, string? assemblyPath = null)
		{
			defines ??= new List<string> ();
			string processedText = Processor.ProcessText (original, steps, defines, assemblyPath, false)!;
#if true
			Console.WriteLine (processedText);
			Console.WriteLine ();
			Console.WriteLine (expected);
#endif
			Assert.Equal (expected, processedText, ignoreLineEndingDifferences: true);
		}

	}
}
