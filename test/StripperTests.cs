using System;

using Xunit;

namespace mellite.tests {
	public class StripperTests {
		void TestStrip (string original, string expected) => TestUtilities.TestProcess (original, ProcessSteps.StripExistingNET6Attributes, expected);

		void TestMethodAttributeStripping (string originalAttributes, string expectedAttributes)
		{
			string body = @"    public partial class Class1
    {{
{0}        public void Foo () {{}}

        public void Bar () {{}}
    }}";

			var original = TestUtilities.GetTestProgram (string.Format (body, originalAttributes));
			var expected = TestUtilities.GetTestProgram (string.Format (body, expectedAttributes));
			TestStrip (original, expected);
		}


		[Fact]
		public void StripEntireBlock ()
		{
			// 			TestMethodAttributeStripping (@"#if NET
			//         [UnsupportedOSPlatform (""ios13.0"")]
			// #endif
			// ", "");

			TestMethodAttributeStripping (@"#if !NET
        [NoiOS]
#else
        [UnsupportedOSPlatform (""ios13.0"")]
#endif
", "");
		}
	}
}

