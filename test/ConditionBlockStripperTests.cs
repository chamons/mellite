using System;

using Xunit;

namespace mellite.tests {
	public class ConditionaBlockStripperTests {
		void TestStrip (string original, string expected) => TestUtilities.TestProcess (original, ProcessSteps.StripConditionBlocks, expected);

		void TestBlockStripping (string originalAttributes, string expectedAttributes)
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
			TestBlockStripping (@"#if NET
        [UnsupportedOSPlatform (""macos10"")]
#endif
", @"        [UnsupportedOSPlatform (""macos10"")]
");

			TestBlockStripping (@"#if !NET
        [NoiOS]
#else
        [UnsupportedOSPlatform (""ios13.0"")]
#endif
", @"        [NoiOS]
        [UnsupportedOSPlatform (""ios13.0"")]
");
		}

		[Fact]
		public void StripNestedAttributes ()
		{
			TestBlockStripping (@"#if NET
        [UnsupportedOSPlatform (""ios13.0"")]
#if IOS
        [Obsolete (""Starting with ios13.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
        [UnsupportedOSPlatform (""maccatalyst"")]
#else
        [Deprecated (PlatformName.iOS, 13, 0)]
        [Unavailable (PlatformName.MacCatalyst)]
#endif
", @"        [UnsupportedOSPlatform (""ios13.0"")]
#if IOS
        [Obsolete (""Starting with ios13.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
        [UnsupportedOSPlatform (""maccatalyst"")]
        [Deprecated (PlatformName.iOS, 13, 0)]
        [Unavailable (PlatformName.MacCatalyst)]
");
		}

		[Fact]
		public void StripDeeplyNestedAttributes ()
		{
			TestBlockStripping (@"#if true
#if NET
        [UnsupportedOSPlatform (""ios13.0"")]
#if IOS
        [Obsolete (""Starting with ios13.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
        [UnsupportedOSPlatform (""maccatalyst"")]
#else
        [Deprecated (PlatformName.iOS, 13, 0)]
        [Unavailable (PlatformName.MacCatalyst)]
#endif
#endif
", @"#if true
        [UnsupportedOSPlatform (""ios13.0"")]
#if IOS
        [Obsolete (""Starting with ios13.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
        [UnsupportedOSPlatform (""maccatalyst"")]
        [Deprecated (PlatformName.iOS, 13, 0)]
        [Unavailable (PlatformName.MacCatalyst)]
#endif
");
		}

		[Fact]
		public void DllImportExample ()
		{
			TestStrip (@"
namespace ARKit {
	public partial class ARSkeleton {

#if !NET
		[iOS (14, 0)]
#endif
		[DllImport (Constants.ARKitLibrary)]
		static extern IntPtr /* NSString */ ARSkeletonJointNameForRecognizedPointKey (/* NSString */ IntPtr recognizedPointKey);

#if !NET
		[iOS (14, 0)]
#endif
		public static NSString? CreateJointName (NSString recognizedPointKey) {	}
	}
}
", @"
namespace ARKit {
	public partial class ARSkeleton {

		[iOS (14, 0)]
		[DllImport (Constants.ARKitLibrary)]
		static extern IntPtr /* NSString */ ARSkeletonJointNameForRecognizedPointKey (/* NSString */ IntPtr recognizedPointKey);

		[iOS (14, 0)]
		public static NSString? CreateJointName (NSString recognizedPointKey) {	}
	}
}
");
		}
	}
}

