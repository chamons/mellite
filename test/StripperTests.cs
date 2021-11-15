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
			TestMethodAttributeStripping (@"#if NET
        [UnsupportedOSPlatform (""macos10"")]
#endif
", "");

			TestMethodAttributeStripping (@"#if !NET
        [NoiOS]
#else
        [UnsupportedOSPlatform (""ios13.0"")]
#endif
", @"#if !NET
        [NoiOS]
#endif
");
		}

		[Fact]
		public void SkipStripEntireBlock ()
		{
			TestMethodAttributeStripping (@"#if NET
        [public static int Something;
        [[UnsupportedOSPlatform (""ios13.0"")]
#endif
", @"#if NET
        [public static int Something;
        [[UnsupportedOSPlatform (""ios13.0"")]
#endif
");

			TestMethodAttributeStripping (@"#if !NET
        [NoiOS]
#else
        public static int Something;
        [UnsupportedOSPlatform (""macos10.0"")]
#endif
", @"#if !NET
        [NoiOS]
#else
        public static int Something;
        [UnsupportedOSPlatform (""macos10.0"")]
#endif
");
		}

		[Fact]
		public void StripNestedAttributes ()
		{
			TestMethodAttributeStripping (@"#if NET
        [UnsupportedOSPlatform (""ios13.0"")]
#if IOS
        [Obsolete (""Starting with ios13.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
        [UnsupportedOSPlatform (""maccatalyst"")]
#else
        [Deprecated (PlatformName.iOS, 13, 0)]
        [Unavailable (PlatformName.MacCatalyst)]
#endif
", @"#if !NET
        [Deprecated (PlatformName.iOS, 13, 0)]
        [Unavailable (PlatformName.MacCatalyst)]
#endif
");
		}
	}
}

