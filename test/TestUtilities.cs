using System;

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

		public static void TestProcess (string original, ProcessSteps steps, string expected)
		{
			string processedText = Processor.ProcessText (original, steps);
#if true
			Console.WriteLine (processedText);
			Console.WriteLine (expected);
#endif
			Assert.Equal (expected, processedText, ignoreLineEndingDifferences: true);
		}

	}
}
