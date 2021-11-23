using System;

using Xunit;

namespace mellite.tests {
	public class AttributeStripperTests {
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

		[Fact]
		public void StripDeeplyNestedAttributes ()
		{
			TestMethodAttributeStripping (@"#if true
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
#if !NET
        [Deprecated (PlatformName.iOS, 13, 0)]
        [Unavailable (PlatformName.MacCatalyst)]
#endif
#endif
");
		}

		[Fact]
		public void FSEventExample ()
		{
			TestStrip (@"struct FSEventStreamContext {
		nint version; /* CFIndex: only valid value is zero */
		internal IntPtr Info; /* void * __nullable */
		IntPtr Retain; /* CFAllocatorRetainCallBack __nullable */
#if NET
		internal unsafe delegate* unmanaged<IntPtr, void> Release; /* CFAllocatorReleaseCallBack __nullable */
#else
		internal FSEventStream.ReleaseContextCallback Release; /* CFAllocatorReleaseCallBack __nullable */
#endif
		IntPtr CopyDescription; /* CFAllocatorCopyDescriptionCallBack __nullable */
}", @"struct FSEventStreamContext {
		nint version; /* CFIndex: only valid value is zero */
		internal IntPtr Info; /* void * __nullable */
		IntPtr Retain; /* CFAllocatorRetainCallBack __nullable */
#if NET
		internal unsafe delegate* unmanaged<IntPtr, void> Release; /* CFAllocatorReleaseCallBack __nullable */
#else
		internal FSEventStream.ReleaseContextCallback Release; /* CFAllocatorReleaseCallBack __nullable */
#endif
		IntPtr CopyDescription; /* CFAllocatorCopyDescriptionCallBack __nullable */
}
");

			TestStrip (@"#if !NET
		static readonly FSEventStreamCallback eventsCallback = EventsCallback;

		static readonly ReleaseContextCallback releaseContextCallback = FreeGCHandle;
		internal delegate void ReleaseContextCallback (IntPtr info);
#endif

#if NET
		[UnmanagedCallersOnly]
#endif
		static void FreeGCHandle (IntPtr gchandle) {}", @"#if !NET
		static readonly FSEventStreamCallback eventsCallback = EventsCallback;

		static readonly ReleaseContextCallback releaseContextCallback = FreeGCHandle;
		internal delegate void ReleaseContextCallback (IntPtr info);
#endif

#if NET
		[UnmanagedCallersOnly]
#endif
		static void FreeGCHandle (IntPtr gchandle) {}
");
		}

		[Fact]
		public void AudioUnitExample ()
		{
			TestStrip (@"namespace AudioUnit {
	public class AudioUnit : DisposableObject {
#if !XAMCORE_3_0 || MONOMAC
#if NET
#else
#endif
#endif

#if !MONOMAC
#if !NET
		[iOS (7, 0)]
#else
		[UnsupportedOSPlatform (""ios13.0"")]
#endif
		static extern AudioComponentStatus AudioOutputUnitPublish (AudioComponentDescription inDesc, IntPtr /* CFStringRef */ inName, uint /* UInt32 */ inVersion, IntPtr /* AudioUnit */ inOutputUnit);
#endif
	}
}
", @"namespace AudioUnit {
	public class AudioUnit : DisposableObject {
#if !XAMCORE_3_0 || MONOMAC
#if !NET
#endif
#endif

#if !MONOMAC
#if !NET
		[iOS (7, 0)]
#endif
		static extern AudioComponentStatus AudioOutputUnitPublish (AudioComponentDescription inDesc, IntPtr /* CFStringRef */ inName, uint /* UInt32 */ inVersion, IntPtr /* AudioUnit */ inOutputUnit);
#endif
	}
}
");
		}
	}
}

