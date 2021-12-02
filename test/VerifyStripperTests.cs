using System;

using Xunit;

namespace mellite.tests {
	public class VerifyStripperTests {
		void TestStrip (string original, string expected) => TestUtilities.TestProcess (original, ProcessSteps.StripVerify, expected);
		void TestStripToSame (string original) => TestUtilities.TestProcess (original, ProcessSteps.StripVerify, original);

		[Fact]
		public void StripVerifyTests ()
		{
			/*
			TestStrip (@"#if MONOMAC
namespace AppKit {
#else
namespace UIKit {
#endif
		partial class NSLayoutManager {
#if !XAMCORE_4_0 && !__MACCATALYST__
#if MONOMAC
		[Deprecated (PlatformName.MacOSX, 10, 15, message: ""Use the overload that takes 'nint glyphCount' instead."")]
		[Deprecated (PlatformName.iOS, 13, 0, message: ""Use the overload that takes 'nint glyphCount' instead."")]
		[Deprecated (PlatformName.WatchOS, 6, 0, message: ""Use the overload that takes 'nint glyphCount' instead."")]
		[Deprecated (PlatformName.TvOS, 13, 0, message: ""Use the overload that takes 'nint glyphCount' instead."")]
		public unsafe void ShowGlyphs (
#else
		[Deprecated (PlatformName.MacOSX, 10, 15, message: ""Use the 'ShowGlyphs' overload that takes 'nint glyphCount' instead."")]
		[Deprecated (PlatformName.iOS, 13, 0, message: ""Use the 'ShowGlyphs' overload that takes 'nint glyphCount' instead."")]
		[Deprecated (PlatformName.WatchOS, 6, 0, message: ""Use the 'ShowGlyphs' overload that takes 'nint glyphCount' instead."")]
		[Deprecated (PlatformName.TvOS, 13, 0, message: ""Use the 'ShowGlyphs' overload that takes 'nint glyphCount' instead."")]
		public unsafe void ShowCGGlyphs (
#endif // MONOMAC
        }
#endif
    }
}
", @"#if MONOMAC
namespace AppKit {
#else
namespace UIKit {
#endif
		partial class NSLayoutManager {
#if !XAMCORE_4_0 && !__MACCATALYST__
		[Verify] // Nested Conditionals are not always correctly processed
#if MONOMAC
		[Deprecated (PlatformName.MacOSX, 10, 15, message: ""Use the overload that takes 'nint glyphCount' instead."")]
		[Deprecated (PlatformName.iOS, 13, 0, message: ""Use the overload that takes 'nint glyphCount' instead."")]
		[Deprecated (PlatformName.WatchOS, 6, 0, message: ""Use the overload that takes 'nint glyphCount' instead."")]
		[Deprecated (PlatformName.TvOS, 13, 0, message: ""Use the overload that takes 'nint glyphCount' instead."")]
		public unsafe void ShowGlyphs (
#else
#if NET
		[UnsupportedOSPlatform (""macos10.15"")]
		[UnsupportedOSPlatform (""tvos13.0"")]
		[UnsupportedOSPlatform (""ios13.0"")]
#if MONOMAC
		[Obsolete (""Starting with macos10.15 Use the 'ShowGlyphs' overload that takes 'nint glyphCount' instead."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif TVOS
		[Obsolete (""Starting with tvos13.0 Use the 'ShowGlyphs' overload that takes 'nint glyphCount' instead."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif IOS
		[Obsolete (""Starting with ios13.0 Use the 'ShowGlyphs' overload that takes 'nint glyphCount' instead."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
#else
		[Deprecated (PlatformName.MacOSX, 10, 15, message: ""Use the 'ShowGlyphs' overload that takes 'nint glyphCount' instead."")]
		[Deprecated (PlatformName.iOS, 13, 0, message: ""Use the 'ShowGlyphs' overload that takes 'nint glyphCount' instead."")]
		[Deprecated (PlatformName.WatchOS, 6, 0, message: ""Use the 'ShowGlyphs' overload that takes 'nint glyphCount' instead."")]
		[Deprecated (PlatformName.TvOS, 13, 0, message: ""Use the 'ShowGlyphs' overload that takes 'nint glyphCount' instead."")]
#endif
		public unsafe void ShowCGGlyphs (
#endif // MONOMAC
        }
#endif
    }
}
");*/

			// Also warn if our conditional block is the last one in the file
			TestStrip (@"namespace AppKit {
	partial class NSLayoutManager {
#if IOS
		[iOS (12,2)]
		[DllImport (Constants.UIKitLibrary)]
		static extern void UIGuidedAccessConfigureAccessibilityFeatures (/* UIGuidedAccessAccessibilityFeature */ nuint features, [MarshalAs (UnmanagedType.I1)] bool enabled, IntPtr completion);
#endif
    }
}
", @"namespace AppKit {
	partial class NSLayoutManager {
	[Verify] // Nested Conditionals are not always correctly processed
#if IOS
		[iOS (12,2)]
		[DllImport (Constants.UIKitLibrary)]
		static extern void UIGuidedAccessConfigureAccessibilityFeatures (/* UIGuidedAccessAccessibilityFeature */ nuint features, [MarshalAs (UnmanagedType.I1)] bool enabled, IntPtr completion);
#endif
    }
}
");

			// #if !WATCH shouldn't trigger [Verify] as it has a ! prefix
			TestStripToSame (@"namespace UIKit {
	public partial class UIApplication : UIResponder
	{
#if !WATCH
		[DllImport (/*Constants.UIKitLibrary*/ ""__Internal"")]
		extern static int UIApplicationMain (int argc, /* char[]* */ string []? argv, /* NSString* */ IntPtr principalClassName, /* NSString* */ IntPtr delegateClassName);
#endif
	}
}
");

			// Hard code #if XAMCORE_4_0 as not worth warning about
			TestStripToSame (@"namespace UIKit {
	public partial class UIApplication : UIResponder
	{
#if XAMCORE_4_0
		[DllImport (/*Constants.UIKitLibrary*/ ""__Internal"")]
		extern static int A (int argc, /* char[]* */ string []? argv, /* NSString* */ IntPtr principalClassName, /* NSString* */ IntPtr delegateClassName);
#endif

		[DllImport (/*Constants.UIKitLibrary*/ ""__Internal"")]
		extern static int B (int argc, /* char[]* */ string []? argv, /* NSString* */ IntPtr principalClassName, /* NSString* */ IntPtr delegateClassName);
	}
}
");
		}
	}
}

