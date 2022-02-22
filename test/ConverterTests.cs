using System;
using System.Collections.Generic;
using Xunit;

namespace mellite.tests {
	public class ConverterTests {

		void TestConversion (string original, string expected) => TestUtilities.TestProcess (original, ProcessSteps.ConvertXamarinAttributes, expected);
		void TestConversionToSame (string original) => TestUtilities.TestProcess (original, ProcessSteps.ConvertXamarinAttributes, original);

		void TestClassAttributeConversion (string xamarinAttribute, string newAttribute, string? xamarinAttributeAfterConvert = null)
		{
			string body = @"{0}
    public partial class Class1
    {{
        public void Foo () {{}}
    }}";
			xamarinAttributeAfterConvert ??= xamarinAttribute;
			var original = TestUtilities.GetTestProgram (string.Format (body, "    " + xamarinAttribute));
			var expected = TestUtilities.GetTestProgram (string.Format (body, TestUtilities.GetConditionalAttributeBlock (xamarinAttributeAfterConvert, newAttribute, 4)));
			TestConversion (original, expected);
		}

		[Fact]
		public void SingleAttributeOnClass ()
		{
			TestClassAttributeConversion ("[Introduced (PlatformName.MacOSX, 10, 0)]", "[SupportedOSPlatform (\"macos10.0\")]");
			TestClassAttributeConversion ("[Introduced (PlatformName.iOS, 6, 0)]", "[SupportedOSPlatform (\"ios6.0\")]");
			TestClassAttributeConversion ("[Introduced (PlatformName.iOS, 6, 0), Introduced (PlatformName.MacOSX, 10, 0)]", @"[SupportedOSPlatform (""ios6.0"")]
    [SupportedOSPlatform (""macos10.0"")]", xamarinAttributeAfterConvert: @"[Introduced (PlatformName.iOS, 6, 0)]
    [Introduced (PlatformName.MacOSX, 10, 0)]");
			TestClassAttributeConversion ("[Introduced (PlatformName.iOS, 6, 0), Introduced (PlatformName.MacOSX, 10, 0), Introduced (PlatformName.MacCatalyst, 10, 0)]", @"[SupportedOSPlatform (""ios6.0"")]
    [SupportedOSPlatform (""macos10.0"")]
    [SupportedOSPlatform (""maccatalyst10.0"")]", xamarinAttributeAfterConvert: @"[Introduced (PlatformName.iOS, 6, 0)]
    [Introduced (PlatformName.MacOSX, 10, 0)]
    [Introduced (PlatformName.MacCatalyst, 10, 0)]");
			TestClassAttributeConversion (@"[Introduced (PlatformName.iOS, 6, 0)]
    [Introduced (PlatformName.MacOSX, 10, 0)]
    [Introduced (PlatformName.MacCatalyst, 10, 0)]", @"[SupportedOSPlatform (""ios6.0"")]
    [SupportedOSPlatform (""macos10.0"")]
    [SupportedOSPlatform (""maccatalyst10.0"")]");
		}

		void TestMethodAttributeConversion (string xamarinAttribute, string newAttribute, string? xamarinAttributeAfterConvert = null, int? newAttributeSpaceCount = null)
		{
			string body = @"    public partial class Class1
    {{
{0}
        public void Foo () {{}}

        public void Bar () {{}}
    }}";
			xamarinAttributeAfterConvert ??= xamarinAttribute;
			var original = TestUtilities.GetTestProgram (string.Format (body, "        " + xamarinAttribute));
			var expected = TestUtilities.GetTestProgram (string.Format (body, TestUtilities.GetConditionalAttributeBlock (xamarinAttributeAfterConvert, newAttribute, 8, newAttributeSpaceCount)));
			TestConversion (original, expected);
		}

		void TestMethodAttributeConversionToFinal (string xamarinAttribute, string expected)
		{
			string body = @"    public partial class Class1
    {{
{0}
        public void Foo () {{}}

        public void Bar () {{}}
    }}";
			var original = TestUtilities.GetTestProgram (string.Format (body, "        " + xamarinAttribute));
			TestConversion (original, expected);
		}

		[Fact]
		public void Introduced ()
		{
			TestMethodAttributeConversion ("[Introduced (PlatformName.MacOSX, 10, 0)]", "[SupportedOSPlatform (\"macos10.0\")]");
			TestMethodAttributeConversion ("[Introduced (PlatformName.iOS, 6, 0)]", "[SupportedOSPlatform (\"ios6.0\")]");
			TestMethodAttributeConversion ("[Introduced (PlatformName.iOS, 6, 0), Introduced (PlatformName.MacOSX, 10, 0)]", @"[SupportedOSPlatform (""ios6.0"")]
        [SupportedOSPlatform (""macos10.0"")]", xamarinAttributeAfterConvert: @"[Introduced (PlatformName.iOS, 6, 0)]
        [Introduced (PlatformName.MacOSX, 10, 0)]");
			TestMethodAttributeConversion ("[Introduced (PlatformName.iOS, 6, 0), Introduced (PlatformName.MacOSX, 10, 0), Introduced (PlatformName.MacCatalyst, 10, 0)]", @"[SupportedOSPlatform (""ios6.0"")]
        [SupportedOSPlatform (""macos10.0"")]
        [SupportedOSPlatform (""maccatalyst10.0"")]", xamarinAttributeAfterConvert: @"[Introduced (PlatformName.iOS, 6, 0)]
        [Introduced (PlatformName.MacOSX, 10, 0)]
        [Introduced (PlatformName.MacCatalyst, 10, 0)]");
			TestMethodAttributeConversion (@"[Introduced (PlatformName.iOS, 6, 0)]
        [Introduced (PlatformName.MacOSX, 10, 0)]
        [Introduced (PlatformName.MacCatalyst, 10, 0)]", @"[SupportedOSPlatform (""ios6.0"")]
        [SupportedOSPlatform (""macos10.0"")]
        [SupportedOSPlatform (""maccatalyst10.0"")]");
		}

		[Fact]
		public void Deprecated ()
		{
			TestMethodAttributeConversion ("[Deprecated (PlatformName.iOS, 11, 0)]", @"[UnsupportedOSPlatform (""ios11.0"")]
#if IOS
        [Obsolete (""Starting with ios11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif");

			TestMethodAttributeConversion ("[Deprecated (PlatformName.MacOSX, 11, 0)][Deprecated (PlatformName.iOS, 11, 0)]", @"[UnsupportedOSPlatform (""macos11.0"")]
        [UnsupportedOSPlatform (""ios11.0"")]
#if MONOMAC
        [Obsolete (""Starting with macos11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif IOS
        [Obsolete (""Starting with ios11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif", xamarinAttributeAfterConvert: @"[Deprecated (PlatformName.MacOSX, 11, 0)]
        [Deprecated (PlatformName.iOS, 11, 0)]");

			// iOS must be ordered at end
			TestMethodAttributeConversion ("[Deprecated (PlatformName.MacOSX, 11, 0)][Deprecated (PlatformName.iOS, 11, 0)][Deprecated (PlatformName.MacCatalyst, 11, 0)][Deprecated (PlatformName.TvOS, 11, 0)]", @"[UnsupportedOSPlatform (""macos11.0"")]
        [UnsupportedOSPlatform (""maccatalyst11.0"")]
        [UnsupportedOSPlatform (""tvos11.0"")]
        [UnsupportedOSPlatform (""ios11.0"")]
#if MONOMAC
        [Obsolete (""Starting with macos11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif __MACCATALYST__
        [Obsolete (""Starting with maccatalyst11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif TVOS
        [Obsolete (""Starting with tvos11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif IOS
        [Obsolete (""Starting with ios11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif", xamarinAttributeAfterConvert: @"[Deprecated (PlatformName.MacOSX, 11, 0)]
        [Deprecated (PlatformName.iOS, 11, 0)]
        [Deprecated (PlatformName.MacCatalyst, 11, 0)]
        [Deprecated (PlatformName.TvOS, 11, 0)]");

			// Watch is not a platform we should output
			TestMethodAttributeConversion ("[Deprecated (PlatformName.MacOSX, 11, 0)][Deprecated (PlatformName.iOS, 11, 0)][Deprecated (PlatformName.WatchOS, 11, 0)]", @"[UnsupportedOSPlatform (""macos11.0"")]
        [UnsupportedOSPlatform (""ios11.0"")]
#if MONOMAC
        [Obsolete (""Starting with macos11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif IOS
        [Obsolete (""Starting with ios11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif", xamarinAttributeAfterConvert: @"[Deprecated (PlatformName.MacOSX, 11, 0)]
        [Deprecated (PlatformName.iOS, 11, 0)]
        [Deprecated (PlatformName.WatchOS, 11, 0)]");
		}

		[Fact]
		public void Obsolete ()
		{
			TestMethodAttributeConversion ("[Obsoleted (PlatformName.iOS, 11, 0)]", @"#if IOS
        [Obsolete (""Starting with ios11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif", newAttributeSpaceCount: 0);

			TestMethodAttributeConversion (@"[Obsoleted (PlatformName.iOS, 11, 0)]
        [Obsoleted (PlatformName.MacOSX, 11, 0)]", @"#if MONOMAC
        [Obsolete (""Starting with macos11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif IOS
        [Obsolete (""Starting with ios11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif", newAttributeSpaceCount: 0);

			TestMethodAttributeConversion (@"[Obsoleted (PlatformName.iOS, 11, 0)]
        [Obsoleted (PlatformName.MacOSX, 11, 0)]
        [Obsoleted (PlatformName.TvOS, 11, 0)]", @"#if MONOMAC
        [Obsolete (""Starting with macos11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif TVOS
        [Obsolete (""Starting with tvos11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif IOS
        [Obsolete (""Starting with ios11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif", newAttributeSpaceCount: 0);
		}

		[Fact]
		public void Unavailable ()
		{
			TestMethodAttributeConversion ("[Unavailable (PlatformName.iOS, 11, 0)]", @"[UnsupportedOSPlatform (""ios11.0"")]");
			TestMethodAttributeConversion (@"[Unavailable (PlatformName.iOS, 11, 0)]
        [Unavailable (PlatformName.MacOSX, 11, 0)]", @"[UnsupportedOSPlatform (""ios11.0"")]
        [UnsupportedOSPlatform (""macos11.0"")]");
			TestMethodAttributeConversion (@"[Unavailable (PlatformName.iOS, 11, 0)]
        [Unavailable (PlatformName.MacOSX, 11, 0)]
        [Unavailable (PlatformName.TvOS, 11, 0)]", @"[UnsupportedOSPlatform (""ios11.0"")]
        [UnsupportedOSPlatform (""macos11.0"")]
        [UnsupportedOSPlatform (""tvos11.0"")]");
		}

		[Fact]
		public void UnavailableAsOtherNames ()
		{
			TestMethodAttributeConversion ("[NoiOS]", @"[UnsupportedOSPlatform (""ios"")]");
			TestMethodAttributeConversion (@"[NoiOS]
        [NoMac]", @"[UnsupportedOSPlatform (""ios"")]
        [UnsupportedOSPlatform (""macos"")]");
			TestMethodAttributeConversion (@"[NoiOS]
        [NoWatch]", @"[UnsupportedOSPlatform (""ios"")]");
		}

		[Fact]
		public void IntroducedAsOtherNames ()
		{
			TestMethodAttributeConversion ("[iOS (11,0)]", @"[SupportedOSPlatform (""ios11.0"")]");
			TestMethodAttributeConversion (@"[iOS (11,0)]
        [Mac (11,0)]", @"[SupportedOSPlatform (""ios11.0"")]
        [SupportedOSPlatform (""macos11.0"")]");
			TestMethodAttributeConversion (@"[iOS (11,0)]
        [Watch (11,0)]", @"[SupportedOSPlatform (""ios11.0"")]");
		}

		// TODO - Deprecated and Obsolete #if blocks are not shared and generated separately.
		// https://github.com/chamons/mellite/issues/1
		[Fact]
		public void MergeDifferentIfBlocks ()
		{
			TestMethodAttributeConversion (@"[Obsoleted (PlatformName.iOS, 11, 0)]
        [Obsoleted (PlatformName.MacCatalyst)]", @"#if __MACCATALYST__
        [Obsolete (""Starting with maccatalyst."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif IOS
        [Obsolete (""Starting with ios11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif", newAttributeSpaceCount: 0);

			TestMethodAttributeConversion (@"[Obsoleted (PlatformName.iOS, 11, 0)]
        [Deprecated (PlatformName.MacCatalyst)]", @"[UnsupportedOSPlatform (""maccatalyst"")]
#if __MACCATALYST__
        [Obsolete (""Starting with maccatalyst."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
#if IOS
        [Obsolete (""Starting with ios11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif");
		}

		[Fact]
		public void IgnoreUnknownAttributes ()
		{
			TestMethodAttributeConversionToFinal ("[Flags][Obsolete][Unavailable (PlatformName.iOS, 11, 0)]", @"using System;
using ObjCRuntime;

namespace binding
{
    public partial class Class1
    {
#if NET
        [UnsupportedOSPlatform (""ios11.0"")]
#else
        [Unavailable (PlatformName.iOS, 11, 0)]
#endif
        [Flags]
        [Obsolete]
        public void Foo () {}

        public void Bar () {}
    }
}
");
		}

		// Final smoke test of all base attributes
		[Fact]
		public void TestAllAttributeKinds ()
		{
			TestMethodAttributeConversion (@"[Introduced (PlatformName.MacOSX, 10, 0)]
        [Deprecated (PlatformName.MacOSX, 11, 0)]
        [Obsoleted (PlatformName.iOS, 11, 0)]
        [Unavailable (PlatformName.MacCatalyst)]", @"[SupportedOSPlatform (""macos10.0"")]
        [UnsupportedOSPlatform (""macos11.0"")]
#if MONOMAC
        [Obsolete (""Starting with macos11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
        [UnsupportedOSPlatform (""maccatalyst"")]
#if IOS
        [Obsolete (""Starting with ios11.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif");

			TestMethodAttributeConversion (@"[Mac (11, 0)]
        [iOS (11, 0)]
        [TV (10, 0)]
        [MacCatalyst (11, 0)]", @"[SupportedOSPlatform (""macos11.0"")]
        [SupportedOSPlatform (""ios11.0"")]
        [SupportedOSPlatform (""tvos10.0"")]
        [SupportedOSPlatform (""maccatalyst11.0"")]");

			TestMethodAttributeConversion (@"[NoMac]
        [NoiOS]
        [NoTV]
        [NoMacCatalyst]", @"[UnsupportedOSPlatform (""macos"")]
        [UnsupportedOSPlatform (""ios"")]
        [UnsupportedOSPlatform (""tvos"")]
        [UnsupportedOSPlatform (""maccatalyst"")]");

		}

		[Fact]
		public void OnlyWatch ()
		{
			TestMethodAttributeConversionToFinal (@"[Watch (11, 0)][NoWatch]", @"using System;
using ObjCRuntime;

namespace binding
{
    public partial class Class1
    {
#if !NET
        [Watch (11, 0)]
        [NoWatch]
#endif
        public void Foo () {}

        public void Bar () {}
    }
}
");
		}

		[Fact]
		public void NewLinesBetweenElements ()
		{
			TestConversion (
			$@"using System;
using ObjCRuntime;

namespace binding
{{
	public partial class Class1
	{{
		[Introduced (PlatformName.MacOSX, 10, 0)]
		public void Foo () {{}}

		[Introduced (PlatformName.iOS, 6, 0)]
		public void Bar () {{}}

		[Introduced (PlatformName.MacOSX, 10, 0)]
		public void Buzz () {{}}

		public void FooBar () {{}}
	}}
}}
			",
			$@"using System;
using ObjCRuntime;

namespace binding
{{
	public partial class Class1
	{{
#if NET
		[SupportedOSPlatform (""macos10.0"")]
#else
		[Introduced (PlatformName.MacOSX, 10, 0)]
#endif
		public void Foo () {{}}

#if NET
		[SupportedOSPlatform (""ios6.0"")]
#else
		[Introduced (PlatformName.iOS, 6, 0)]
#endif
		public void Bar () {{}}

#if NET
		[SupportedOSPlatform (""macos10.0"")]
#else
		[Introduced (PlatformName.MacOSX, 10, 0)]
#endif
		public void Buzz () {{}}

		public void FooBar () {{}}
	}}
}}
			");

			// CRLF CRLF TAB CRLF CRLF
			TestConversion (
			$@"using System;
using ObjCRuntime;

namespace binding
{{
	public partial class Class1
	{{
		[Introduced (PlatformName.MacOSX, 10, 0)]
		public void Foo () {{}}




		[Introduced (PlatformName.iOS, 6, 0)]
		public void Bar () {{}}
	}}
}}
",
			$@"using System;
using ObjCRuntime;

namespace binding
{{
	public partial class Class1
	{{
#if NET
		[SupportedOSPlatform (""macos10.0"")]
#else
		[Introduced (PlatformName.MacOSX, 10, 0)]
#endif
		public void Foo () {{}}




#if NET
		[SupportedOSPlatform (""ios6.0"")]
#else
		[Introduced (PlatformName.iOS, 6, 0)]
#endif
		public void Bar () {{}}
	}}
}}
");
		}

		void TestParentAndChildConversion (string parentXamarinAttribute, string childXamarinAttribute, string expected)
		{
			string body = @"    {0}
    public partial class Class1
    {{
        {1}
        public void Foo () {{}}

        public void Bar () {{}}
    }}";
			var original = TestUtilities.GetTestProgram (string.Format (body, parentXamarinAttribute, childXamarinAttribute));
			TestConversion (original, expected);
		}

		[Fact]
		public void ChildInheritance ()
		{
			TestParentAndChildConversion ("[Unavailable (PlatformName.MacOSX, 10, 0)][Unavailable (PlatformName.iOS, 11, 0)]",
			"[Introduced (PlatformName.MacOSX, 11, 0)]",
			@"using System;
using ObjCRuntime;

namespace binding
{
#if NET
    [UnsupportedOSPlatform (""macos10.0"")]
    [UnsupportedOSPlatform (""ios11.0"")]
#else
    [Unavailable (PlatformName.MacOSX, 10, 0)]
    [Unavailable (PlatformName.iOS, 11, 0)]
#endif
    public partial class Class1
    {
#if NET
        [SupportedOSPlatform (""macos11.0"")]
        [UnsupportedOSPlatform (""macos10.0"")]
        [UnsupportedOSPlatform (""ios11.0"")]
#else
        [Introduced (PlatformName.MacOSX, 11, 0)]
#endif
        public void Foo () {}

        public void Bar () {}
    }
}
");
		}

		[Fact]
		public void DuplicatedChildInheritance ()
		{
			TestParentAndChildConversion ("[Introduced (PlatformName.MacOSX, 10, 0)]",
			"[Introduced (PlatformName.MacOSX, 11, 0)]",
			@"using System;
using ObjCRuntime;

namespace binding
{
#if NET
    [SupportedOSPlatform (""macos10.0"")]
#else
    [Introduced (PlatformName.MacOSX, 10, 0)]
#endif
    public partial class Class1
    {
#if NET
        [SupportedOSPlatform (""macos11.0"")]
#else
        [Introduced (PlatformName.MacOSX, 11, 0)]
#endif
        public void Foo () {}

        public void Bar () {}
    }
}
");
		}

		[Fact]
		public void DifferentPlatformNotDuplicatedChildInheritance ()
		{
			TestParentAndChildConversion ("[Introduced (PlatformName.iOS, 10, 0)]",
			"[Introduced (PlatformName.MacOSX, 11, 0)]",
			@"using System;
using ObjCRuntime;

namespace binding
{
#if NET
    [SupportedOSPlatform (""ios10.0"")]
#else
    [Introduced (PlatformName.iOS, 10, 0)]
#endif
    public partial class Class1
    {
#if NET
        [SupportedOSPlatform (""macos11.0"")]
        [SupportedOSPlatform (""ios10.0"")]
#else
        [Introduced (PlatformName.MacOSX, 11, 0)]
#endif
        public void Foo () {}

        public void Bar () {}
    }
}
");
		}

		[Fact]
		public void RespectUnsupportedPlatformsWhenChildInheritance ()
		{
			TestParentAndChildConversion ("[Introduced (PlatformName.iOS, 10, 0)]",
			"[NoiOS]",
			@"using System;
using ObjCRuntime;

namespace binding
{
#if NET
    [SupportedOSPlatform (""ios10.0"")]
#else
    [Introduced (PlatformName.iOS, 10, 0)]
#endif
    public partial class Class1
    {
#if NET
        [UnsupportedOSPlatform (""ios"")]
#else
        [NoiOS]
#endif
        public void Foo () {}

        public void Bar () {}
    }
}
");
		}

		[Fact]
		public void MismatchedAvailabilityWhenChildInheritance ()
		{
			TestParentAndChildConversion ("[Introduced (PlatformName.iOS, 12, 0)]",
			"[Unavailable (PlatformName.iOS)]",
			@"using System;
using ObjCRuntime;

namespace binding
{
#if NET
    [SupportedOSPlatform (""ios12.0"")]
#else
    [Introduced (PlatformName.iOS, 12, 0)]
#endif
    public partial class Class1
    {
#if NET
        [UnsupportedOSPlatform (""ios"")]
#else
        [Unavailable (PlatformName.iOS)]
#endif
        public void Foo () {}

        public void Bar () {}
    }
}
");
		}

		[Fact]
		public void NonExistantParentPlatformAvailabilityWhenChildInheritance ()
		{
			TestParentAndChildConversion ("[Introduced (PlatformName.WatchOS, 12, 0)]",
			"[Unavailable (PlatformName.iOS, 14, 0)]",
			@"using System;
using ObjCRuntime;

namespace binding
{
#if !NET
    [Introduced (PlatformName.WatchOS, 12, 0)]
#endif
    public partial class Class1
    {
#if NET
        [UnsupportedOSPlatform (""ios14.0"")]
#else
        [Unavailable (PlatformName.iOS, 14, 0)]
#endif
        public void Foo () {}

        public void Bar () {}
    }
}
");
		}

		[Fact]
		public void ConvertWithIfInLeading ()
		{
			TestConversion (@"namespace UIKit {

	public static partial class UIGuidedAccessRestriction {
#if !COREBUILD
		[iOS (7,0)]
		[DllImport (Constants.UIKitLibrary)]
		extern static /* UIGuidedAccessRestrictionState */ nint UIGuidedAccessRestrictionStateForIdentifier (/* NSString */ IntPtr restrictionIdentifier);
#endif // !COREBUILD
	}
}
", @"namespace UIKit {

	public static partial class UIGuidedAccessRestriction {
#if !COREBUILD
#if NET
		[SupportedOSPlatform (""ios7.0"")]
#else
		[iOS (7,0)]
#endif
		[DllImport (Constants.UIKitLibrary)]
		extern static /* UIGuidedAccessRestrictionState */ nint UIGuidedAccessRestrictionStateForIdentifier (/* NSString */ IntPtr restrictionIdentifier);
#endif // !COREBUILD
	}
}
");
		}

		[Fact]
		public void NewLineEating ()
		{
			TestConversionToSame (@"#if !XAMCORE_3_0
	partial class AVAsset {

		[Obsolete (""Use 'GetChapterMetadataGroups'."")]
		public virtual AVMetadataItem[] ChapterMetadataGroups (NSLocale forLocale, AVMetadataItem[] commonKeys)
		{

		}
	}
#endif
"
);
		}

		[Fact]
		public void NullableExample ()
		{
			TestConversion (@"	#nullable enable
	[Obsolete (""Removed in Xcode 13."")]
	[Deprecated (PlatformName.TvOS, 15,0, PlatformArchitecture.All)]
	[Deprecated (PlatformName.MacOSX, 12,0, PlatformArchitecture.All)]
	[Deprecated (PlatformName.iOS, 15,0, PlatformArchitecture.All)]
	[Deprecated (PlatformName.MacCatalyst, 15,0, PlatformArchitecture.All)]
	[Deprecated (PlatformName.WatchOS, 8,0, PlatformArchitecture.All)]
	public partial class AVPlayerInterstitialEventObserver : NSObject {
	}
", @"	#nullable enable
#if NET
	[UnsupportedOSPlatform (""tvos15.0"")]
	[UnsupportedOSPlatform (""macos12.0"")]
	[UnsupportedOSPlatform (""maccatalyst15.0"")]
	[UnsupportedOSPlatform (""ios15.0"")]
#if TVOS
	[Obsolete (""Starting with tvos15.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif MONOMAC
	[Obsolete (""Starting with macos12.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif __MACCATALYST__
	[Obsolete (""Starting with maccatalyst15.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif IOS
	[Obsolete (""Starting with ios15.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
#else
	[Deprecated (PlatformName.TvOS, 15,0, PlatformArchitecture.All)]
	[Deprecated (PlatformName.MacOSX, 12,0, PlatformArchitecture.All)]
	[Deprecated (PlatformName.iOS, 15,0, PlatformArchitecture.All)]
	[Deprecated (PlatformName.MacCatalyst, 15,0, PlatformArchitecture.All)]
	[Deprecated (PlatformName.WatchOS, 8,0, PlatformArchitecture.All)]
#endif
	[Obsolete (""Removed in Xcode 13."")]
	public partial class AVPlayerInterstitialEventObserver : NSObject {
	}
");
		}

		[Fact]
		public void ReturnAs ()
		{
			TestConversionToSame (@"namespace Accessibility {
	public static partial class AXHearingUtilities {
		[DllImport (Constants.AccessibilityLibrary, EntryPoint = ""AXSupportsBidirectionalAXMFiHearingDeviceStreaming"")]
		[return: MarshalAs (UnmanagedType.I1)]
		public static extern bool SupportsBidirectionalStreaming ();
	}
");
		}


		[Fact]
		public void AttributesWithComments ()
		{
			TestConversionToSame (@"namespace Accessibility {
	public static partial class AXHearingUtilities {
		[return: MarshalAs (UnmanagedType.I1)] // This is a comment
		public static extern bool SupportsBidirectionalStreaming ();
	}
");

			TestConversion (@"namespace Accessibility {
	public static partial class AXHearingUtilities {
		[iOS (7,0)] // This is another comment
		public static extern bool SupportsBidirectionalStreaming ();
	}
", @"namespace Accessibility {
	public static partial class AXHearingUtilities {
#if NET
		[SupportedOSPlatform (""ios7.0"")]
#else
		[iOS (7,0)] // This is another comment
#endif
		public static extern bool SupportsBidirectionalStreaming ();
	}
");
		}

		[Fact]
		public void ConvertEvents ()
		{
			TestConversion (@"namespace UIKit {
	public static partial class UIGuidedAccessRestriction {
		[iOS (9,0)]
		public event EventHandler PrimaryActionTriggered {
			add { }
			remove { }
		}
	}
}
", @"namespace UIKit {
	public static partial class UIGuidedAccessRestriction {
#if NET
		[SupportedOSPlatform (""ios9.0"")]
#else
		[iOS (9,0)]
#endif
		public event EventHandler PrimaryActionTriggered {
			add { }
			remove { }
		}
	}
}
");
		}

		[Fact]
		public void Defines ()
		{
			TestUtilities.TestProcess (@"namespace UIKit {
	public partial class UIVibrancyEffect {
#if HAS_NOTIFICATIONCENTER
		[Deprecated (PlatformName.iOS, 10,0, message: ""Use 'CreatePrimaryVibrancyEffectForNotificationCenter' instead."")]
		static public UIVibrancyEffect CreateForNotificationCenter ()
#endif
	}
}
", ProcessSteps.ConvertXamarinAttributes, @"namespace UIKit {
	public partial class UIVibrancyEffect {
#if HAS_NOTIFICATIONCENTER
#if NET
		[UnsupportedOSPlatform (""ios10.0"")]
#if IOS
		[Obsolete (""Starting with ios10.0 use 'CreatePrimaryVibrancyEffectForNotificationCenter' instead."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
#else
		[Deprecated (PlatformName.iOS, 10,0, message: ""Use 'CreatePrimaryVibrancyEffectForNotificationCenter' instead."")]
#endif
		static public UIVibrancyEffect CreateForNotificationCenter ()
#endif
	}
}
", defines: new List<string> () { "HAS_NOTIFICATIONCENTER" });
		}

		[Fact]
		public void EnumConvert ()
		{
			TestConversion (@"[Mac (10,10)]
[NoiOS][NoTV][NoWatch][NoMacCatalyst]
[Native]
public enum AVCaptureViewControlsStyle : long {
	Inline,
	Floating,
	InlineDeviceSelection,
	Default = Inline,
}", @"#if NET
[SupportedOSPlatform (""macos10.10"")]
[UnsupportedOSPlatform (""ios"")]
[UnsupportedOSPlatform (""tvos"")]
[UnsupportedOSPlatform (""maccatalyst"")]
#else
[Mac (10,10)]
[NoiOS]
[NoTV]
[NoWatch]
[NoMacCatalyst]
#endif
[Native]
public enum AVCaptureViewControlsStyle : long {
	Inline,
	Floating,
	InlineDeviceSelection,
	Default = Inline,
}");
		}

		[Fact]
		public void EnumElementConvert ()
		{
			TestConversion (@"public enum InitMode {
	WithConnection,
	[iOS (8,0)]
	WithNoConnection,
}", @"public enum InitMode {
	WithConnection,
#if NET
	[SupportedOSPlatform (""ios8.0"")]
#else
	[iOS (8,0)]
#endif
	WithNoConnection,
}");
		}

		[Fact]
		public void StructConvert ()
		{
			TestConversion (@"[Mac (10,10)]
[NoiOS][NoTV][NoWatch][NoMacCatalyst]
public struct AVCaptureViewControlsStyle {
}", @"#if NET
[SupportedOSPlatform (""macos10.10"")]
[UnsupportedOSPlatform (""ios"")]
[UnsupportedOSPlatform (""tvos"")]
[UnsupportedOSPlatform (""maccatalyst"")]
#else
[Mac (10,10)]
[NoiOS]
[NoTV]
[NoWatch]
[NoMacCatalyst]
#endif
public struct AVCaptureViewControlsStyle {
}");
		}

		const string SystemXI = "/Users/donblas/Programming/mellite/Xamarin.iOS.dll";
		const string SystemXM = "/Users/donblas/Programming/mellite/Xamarin.Mac.dll";
		// Sorry, if you aren't me you'll have to fix this...
		const string SystemAssemblies = "/Users/donblas/Programming/mellite/";

		// Convert a partial class's member with the parent info defined in another assembly (outside of our scope) 
		[Fact]
		public void PartialInfoConversion ()
		{
			TestUtilities.TestProcess (@"namespace Foundation {
	partial class NSDateComponentsFormatter {
		[iOS (9,0)]
		[Export (""CustomStyle"")]
		int CustomStyle { get; set; }
	}
}", ProcessSteps.ConvertXamarinAttributes, @"namespace Foundation {
#if NET
	[SupportedOSPlatform (""ios8.0"")]
	[SupportedOSPlatform (""macos10.10"")]
#else
#endif
	partial class NSDateComponentsFormatter {
#if NET
		[SupportedOSPlatform (""ios9.0"")]
		[SupportedOSPlatform (""macos10.10"")]
#else
		[iOS (9,0)]
#endif
		[Export (""CustomStyle"")]
		int CustomStyle { get; set; }
	}
}", null, SystemXI);
		}

		[Fact]
		public void ConvertWithTVFromAssembly ()
		{
			TestUtilities.TestProcess (@"namespace AVFoundation {
	public partial class AVCaptureConnection {
		[Deprecated (PlatformName.iOS, 7, 0)]
		public bool SupportsVideoMaxFrameDuration {
		}
	}
}", ProcessSteps.ConvertXamarinAttributes, @"namespace AVFoundation {
#if NET
	[SupportedOSPlatform (""maccatalyst14.0"")]
	[UnsupportedOSPlatform (""tvos"")]
#else
#endif
	public partial class AVCaptureConnection {
#if NET
		[SupportedOSPlatform (""maccatalyst14.0"")]
		[UnsupportedOSPlatform (""ios7.0"")]
#if IOS
		[Obsolete (""Starting with ios7.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
		[UnsupportedOSPlatform (""tvos"")]
#else
		[Deprecated (PlatformName.iOS, 7, 0)]
#endif
		public bool SupportsVideoMaxFrameDuration {
		}
	}
}", null, SystemXI);
		}

		[Fact]
		public void ConvertWithSomeAttributesDefinedOff ()
		{
			Assert.Throws<InvalidOperationException> (() =>
			 TestUtilities.TestProcess (@"namespace AVFoundation {
#if XAMCORE_3_0
	[NoiOS]
	[NoWatch]
#endif
	[Unavailable (PlatformName.MacCatalyst)]
	[NoTV]
	[Native]
	// NSInteger - AVCaptureDevice.h
	public enum AVCaptureDeviceTransportControlsPlaybackMode : long {
		NotPlaying, Playing
	}
}", ProcessSteps.ConvertXamarinAttributes, @"", null, SystemXI));
		}

		[Fact]
		public void DeprecatedWithMessage ()
		{
			TestConversion (@"[Deprecated (PlatformName.iOS, 9, 0, message : ""Use the 'Contacts' API instead."")]
		[Introduced (PlatformName.MacCatalyst, 14, 0)]
		[Deprecated (PlatformName.MacCatalyst, 14, 0, message : ""Use the 'Contacts' API instead."")]
		public class ExternalChangeEventArgs : EventArgs {}
", @"#if NET
[SupportedOSPlatform (""maccatalyst14.0"")]
[UnsupportedOSPlatform (""maccatalyst14.0"")]
[UnsupportedOSPlatform (""ios9.0"")]
#if __MACCATALYST__
[Obsolete (""Starting with maccatalyst14.0 use the 'Contacts' API instead."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif IOS
[Obsolete (""Starting with ios9.0 use the 'Contacts' API instead."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
#else
[Deprecated (PlatformName.iOS, 9, 0, message : ""Use the 'Contacts' API instead."")]
[Introduced (PlatformName.MacCatalyst, 14, 0)]
[Deprecated (PlatformName.MacCatalyst, 14, 0, message : ""Use the 'Contacts' API instead."")]
#endif
		public class ExternalChangeEventArgs : EventArgs {}
");
		}

		[Fact]
		public void CtorAttributes ()
		{
			TestConversion (@"public class ABAddressBook : NativeObject, IEnumerable<ABRecord> {
			[Deprecated (PlatformName.iOS, 6, 0, message : ""Use the static Create method instead"")]
			public ABAddressBook () {}
}", @"public class ABAddressBook : NativeObject, IEnumerable<ABRecord> {
#if NET
			[UnsupportedOSPlatform (""ios6.0"")]
#if IOS
			[Obsolete (""Starting with ios6.0 use the static Create method instead."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
#else
			[Deprecated (PlatformName.iOS, 6, 0, message : ""Use the static Create method instead"")]
#endif
			public ABAddressBook () {}
}");
		}

		[Fact]
		public void ABPersonCorrectDeprecation ()
		{
			TestUtilities.TestProcess (@"namespace AddressBook {
[Deprecated (PlatformName.iOS, 9, 0, message : ""Use the 'Contacts' API instead."")]
[Introduced (PlatformName.MacCatalyst, 14, 0)]
[Deprecated (PlatformName.MacCatalyst, 14, 0, message : ""Use the 'Contacts' API instead."")]
public class ABPerson {
   [iOS (7,0)]
   public static ABPersonCompositeNameFormat GetCompositeNameFormat (ABRecord? record) {}
}
}", ProcessSteps.ConvertXamarinAttributes, @"namespace AddressBook {
#if NET
[SupportedOSPlatform (""maccatalyst14.0"")]
[UnsupportedOSPlatform (""maccatalyst14.0"")]
[UnsupportedOSPlatform (""ios9.0"")]
#if __MACCATALYST__
[Obsolete (""Starting with maccatalyst14.0 use the 'Contacts' API instead."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif IOS
[Obsolete (""Starting with ios9.0 use the 'Contacts' API instead."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
#else
[Deprecated (PlatformName.iOS, 9, 0, message : ""Use the 'Contacts' API instead."")]
[Introduced (PlatformName.MacCatalyst, 14, 0)]
[Deprecated (PlatformName.MacCatalyst, 14, 0, message : ""Use the 'Contacts' API instead."")]
#endif
public class ABPerson {
#if NET
   [SupportedOSPlatform (""ios7.0"")]
   [SupportedOSPlatform (""maccatalyst14.0"")]
   [UnsupportedOSPlatform (""maccatalyst14.0"")]
   [UnsupportedOSPlatform (""ios9.0"")]
#if __MACCATALYST__
   [Obsolete (""Starting with maccatalyst14.0 use the 'Contacts' API instead."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif IOS
   [Obsolete (""Starting with ios9.0 use the 'Contacts' API instead."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
#else
   [iOS (7,0)]
#endif
   public static ABPersonCompositeNameFormat GetCompositeNameFormat (ABRecord? record) {}
}
}", null, SystemXI);
		}

		[Fact]
		public void ABMultiValueExample ()
		{
			TestConversion (@"[Deprecated (PlatformName.iOS, 9,0)]
	[Introduced (PlatformName.MacCatalyst, 14, 0)]
	[Deprecated (PlatformName.MacCatalyst, 14, 0, message : ""Use the 'Contacts' API instead."")]
	static class ABMultiValue {}", @"#if NET
[SupportedOSPlatform (""maccatalyst14.0"")]
[UnsupportedOSPlatform (""maccatalyst14.0"")]
[UnsupportedOSPlatform (""ios9.0"")]
#if __MACCATALYST__
[Obsolete (""Starting with maccatalyst14.0 use the 'Contacts' API instead."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif IOS
[Obsolete (""Starting with ios9.0."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
#else
[Deprecated (PlatformName.iOS, 9,0)]
[Introduced (PlatformName.MacCatalyst, 14, 0)]
[Deprecated (PlatformName.MacCatalyst, 14, 0, message : ""Use the 'Contacts' API instead."")]
#endif
	static class ABMultiValue {}");
		}

		[Fact]
		public void EnumWithThreeVersionBits ()
		{
			TestConversion (@"namespace Foundation {
	[NoMacCatalyst]
	[Mac (10,12,2)]
	[Native]
	public enum NSScrubberAlignment : long
	{
		None = 0,
		Leading,
		Trailing,
		Center
	}
}", @"namespace Foundation {
#if NET
	[SupportedOSPlatform (""macos10.12.2"")]
	[UnsupportedOSPlatform (""maccatalyst"")]
#else
	[NoMacCatalyst]
	[Mac (10,12,2)]
#endif
	[Native]
	public enum NSScrubberAlignment : long
	{
		None = 0,
		Leading,
		Trailing,
		Center
	}
}");
		}


		[Fact]
		public void InterfaceConvert ()
		{
			TestConversion (@"namespace AppKit
{
	[Mac (10,10)] // protocol added in 10.10
	public partial interface INSAccessibility {}
}", @"namespace AppKit
{
#if NET
	[SupportedOSPlatform (""macos10.10"")]
#else
	[Mac (10,10)] // protocol added in 10.10
#endif
	public partial interface INSAccessibility {}
}");
		}

		[Fact]
		public void StructStatic ()
		{
			TestConversion (@"[StructLayout (LayoutKind.Sequential)]
	public struct AudioStreamBasicDescription {
		[Deprecated (PlatformName.MacOSX, 10, 10, message : ""Canonical is no longer encouraged, since fixed-point no longer provides a performance advantage over floating point. 'AudioFormatFlagsNativeFloatPacked' is preffered instead."")]
		public static readonly AudioFormatFlags AudioFormatFlagsAudioUnitCanonical = AudioFormatFlags.IsSignedInteger | (BitConverter.IsLittleEndian ? 0 : AudioFormatFlags.IsBigEndian) |
			AudioFormatFlags.IsPacked | AudioFormatFlags.IsNonInterleaved | (AudioFormatFlags) (AudioUnitSampleFractionBits << (int)AudioFormatFlags.LinearPCMSampleFractionShift);
}", @"[StructLayout (LayoutKind.Sequential)]
	public struct AudioStreamBasicDescription {
#if NET
		[UnsupportedOSPlatform (""macos10.10"")]
#if MONOMAC
		[Obsolete (""Starting with macos10.10 canonical is no longer encouraged, since fixed-point no longer provides a performance advantage over floating point. 'AudioFormatFlagsNativeFloatPacked' is preffered instead."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
#else
		[Deprecated (PlatformName.MacOSX, 10, 10, message : ""Canonical is no longer encouraged, since fixed-point no longer provides a performance advantage over floating point. 'AudioFormatFlagsNativeFloatPacked' is preffered instead."")]
#endif
		public static readonly AudioFormatFlags AudioFormatFlagsAudioUnitCanonical = AudioFormatFlags.IsSignedInteger | (BitConverter.IsLittleEndian ? 0 : AudioFormatFlags.IsBigEndian) |
			AudioFormatFlags.IsPacked | AudioFormatFlags.IsNonInterleaved | (AudioFormatFlags) (AudioUnitSampleFractionBits << (int)AudioFormatFlags.LinearPCMSampleFractionShift);
}");
		}

		[Fact]
		public void DeprecatedWithVariableMessage ()
		{
			TestConversion (@"static class Constants {
				public static string UseNetworkInstead = ""Use Network Instead"";
			}

			[Deprecated (PlatformName.MacOSX, 12, 0, message: Constants.UseNetworkInstead)]
			class CFHost {}", @"static class Constants {
				public static string UseNetworkInstead = ""Use Network Instead"";
			}

#if NET
			[UnsupportedOSPlatform (""macos12.0"")]
#if MONOMAC
			[Obsolete ([Verify (""Constants in descriptions are not"")]""Starting with macos12.0 Constants.UseNetworkInstead."", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
#else
			[Deprecated (PlatformName.MacOSX, 12, 0, message: Constants.UseNetworkInstead)]
#endif
			class CFHost {}");
		}

		[Fact]
		public void DelegateDef ()
		{
			TestConversion (@"[iOS (7,1), Watch (6,0)]
	public delegate nint CMBufferGetSize (INativeObject buffer);
", @"#if NET
[SupportedOSPlatform (""ios7.1"")]
#else
[iOS (7,1)]
[Watch (6,0)]
#endif
	public delegate nint CMBufferGetSize (INativeObject buffer);
");
		}

		[Fact]
		public void LaunchExample ()
		{
			TestConversion (@"#if MONOMAC
namespace CoreServices
{
public static class LaunchServices
{
		#region Locating an Application

		[Mac (10, 10)]
		[DllImport (Constants.CoreServicesLibrary)]
		static extern IntPtr LSCopyDefaultApplicationURLForURL (IntPtr inUrl, LSRoles inRole, /*out*/ IntPtr outError);

		#endregion
}
#endif
", @"#if MONOMAC
namespace CoreServices
{
public static class LaunchServices
{
		#region Locating an Application

#if NET
		[SupportedOSPlatform (""macos10.10"")]
#else
		[Mac (10, 10)]
#endif
		[DllImport (Constants.CoreServicesLibrary)]
		static extern IntPtr LSCopyDefaultApplicationURLForURL (IntPtr inUrl, LSRoles inRole, /*out*/ IntPtr outError);

		#endregion
}
#endif
");
		}

		[Fact]
		public void NetworkHangingDefine ()
		{
			TestConversionToSame (@"namespace Network {
	public class NWTxtRecord : NativeObject {
#if !XAMCORE_4_0
		[Obsolete (""Use the overload that takes an NWTxtRecordApplyDelegate2 instead."")]
#endif
		[BindingImpl (BindingImplOptions.Optimizable)]
		public bool Apply (NWTxtRecordApplyDelegate handler)
		{
		}
	}
}
");
		}

		[Fact]
		public void DefaultPlatformAttributeFromNamespace ()
		{
			TestUtilities.TestProcess (@"namespace Network {
	public class NWTxtRecord : NativeObject {
		[Mac (10, 10)]
		public bool Apply (NWTxtRecordApplyDelegate handler)
		{
		}
	}
}
", @"namespace Network {
#if NET
	[SupportedOSPlatform (""ios"")]
	[SupportedOSPlatform (""maccatalyst"")]
	[SupportedOSPlatform (""macos"")]
	[SupportedOSPlatform (""tvos"")]
#else
#endif
	public class NWTxtRecord : NativeObject {
#if NET
		[SupportedOSPlatform (""macos10.10"")]
		[SupportedOSPlatform (""ios"")]
		[SupportedOSPlatform (""maccatalyst"")]
		[SupportedOSPlatform (""tvos"")]
#else
		[Mac (10, 10)]
#endif
		public bool Apply (NWTxtRecordApplyDelegate handler)
		{
		}
	}
}
", new ProcessOptions () { AssemblyPath = SystemXI, AddDefaultIntroducedPath = SystemAssemblies });

			TestUtilities.TestProcess (@"namespace AppKit {
	public class NWTxtRecord : NativeObject {
		[Mac (10, 10)]
		public bool Apply (NWTxtRecordApplyDelegate handler)
		{
		}
	}
}
", @"namespace AppKit {
	public class NWTxtRecord : NativeObject {
#if NET
		[SupportedOSPlatform (""macos10.10"")]
#else
		[Mac (10, 10)]
#endif
		public bool Apply (NWTxtRecordApplyDelegate handler)
		{
		}
	}
}
", new ProcessOptions () { AssemblyPath = SystemXI, AddDefaultIntroducedPath = SystemAssemblies });

			TestUtilities.TestProcess (@"namespace UIKit {
	public class NWTxtRecord : NativeObject {
		[iOS (10, 0)]
		public bool Apply (NWTxtRecordApplyDelegate handler)
		{
		}
	}
}
", @"namespace UIKit {
	public class NWTxtRecord : NativeObject {
#if NET
		[SupportedOSPlatform (""ios10.0"")]
#else
		[iOS (10, 0)]
#endif
		public bool Apply (NWTxtRecordApplyDelegate handler)
		{
		}
	}
}
", new ProcessOptions () { AssemblyPath = SystemXI, AddDefaultIntroducedPath = SystemAssemblies });
		}

		[Fact]
		public void NSTextExample ()
		{
			TestUtilities.TestProcess (@"namespace AppKit {
	public class NSTextContainer {
		[Mac (10,11)]
		public static NSTextContainer FromSize (CGSize size)
		{
			return new NSTextContainer (size, false);
		}
	}
}
", @"namespace AppKit {
#if NET
	[SupportedOSPlatform (""ios7.0"")]
	[SupportedOSPlatform (""macos"")]
	[SupportedOSPlatform (""maccatalyst"")]
	[SupportedOSPlatform (""tvos"")]
#else
#endif
	public class NSTextContainer {
#if NET
		[SupportedOSPlatform (""macos10.11"")]
		[SupportedOSPlatform (""ios7.0"")]
		[SupportedOSPlatform (""maccatalyst"")]
		[SupportedOSPlatform (""tvos"")]
#else
		[Mac (10,11)]
#endif
		public static NSTextContainer FromSize (CGSize size)
		{
			return new NSTextContainer (size, false);
		}
	}
}
", new ProcessOptions () { AssemblyPath = SystemXM, AddDefaultIntroducedPath = SystemAssemblies });
		}

		[Fact]
		public void CompressionExample ()
		{
			TestUtilities.TestProcess (@"namespace VideoToolbox {
	public partial class VTCompressionProperties {
		[Mac (10,9)]
		public VTH264EntropyMode H264EntropyMode { 
	}
}
", @"namespace VideoToolbox {
#if NET
	[SupportedOSPlatform (""ios8.0"")]
	[SupportedOSPlatform (""tvos10.2"")]
	[SupportedOSPlatform (""maccatalyst"")]
	[SupportedOSPlatform (""macos"")]
#else
#endif
	public partial class VTCompressionProperties {
#if NET
		[SupportedOSPlatform (""macos10.9"")]
		[SupportedOSPlatform (""ios8.0"")]
		[SupportedOSPlatform (""tvos10.2"")]
		[SupportedOSPlatform (""maccatalyst"")]
#else
		[Mac (10,9)]
#endif
		public VTH264EntropyMode H264EntropyMode { 
	}
}
", new ProcessOptions () { AssemblyPath = SystemXI, AddDefaultIntroducedPath = SystemAssemblies });
		}

		[Fact]
		public void PdfExample ()
		{
			TestUtilities.TestProcess (@"namespace PdfKit {
	public partial class PdfAnnotation {
		[Mac (10,12)]
		public bool SetValue<T> (T value, PdfAnnotationKey key) where T : class, INativeObject {}
	}
}
", @"namespace PdfKit {
#if NET
	[SupportedOSPlatform (""ios11.0"")]
	[SupportedOSPlatform (""maccatalyst"")]
	[SupportedOSPlatform (""macos"")]
#else
#endif
	public partial class PdfAnnotation {
#if NET
		[SupportedOSPlatform (""macos10.12"")]
		[SupportedOSPlatform (""ios11.0"")]
		[SupportedOSPlatform (""maccatalyst"")]
#else
		[Mac (10,12)]
#endif
		public bool SetValue<T> (T value, PdfAnnotationKey key) where T : class, INativeObject {}
	}
}
", new ProcessOptions () { AssemblyPath = SystemXI, AddDefaultIntroducedPath = SystemAssemblies });
		}

		[Fact]
		public void VideoToolboxExample ()
		{
			TestUtilities.TestProcess (@"namespace VideoToolbox {
	public partial class VTCompressionSession {
	}
}
", @"namespace VideoToolbox {
#if NET
	[SupportedOSPlatform (""ios"")]
	[SupportedOSPlatform (""maccatalyst"")]
	[SupportedOSPlatform (""macos"")]
	[SupportedOSPlatform (""tvos"")]
#else
#endif
	public partial class VTCompressionSession {
	}
}
", new ProcessOptions () { AssemblyPath = SystemXI, AddDefaultIntroducedPath = SystemAssemblies });

			TestUtilities.TestProcess (@"namespace VideoToolbox {
	[iOS (8,0)]
	[TV (10,2)]
	public partial class VTCompressionSession {
	}
}
", @"namespace VideoToolbox {
#if NET
	[SupportedOSPlatform (""ios8.0"")]
	[SupportedOSPlatform (""tvos10.2"")]
	[SupportedOSPlatform (""maccatalyst"")]
	[SupportedOSPlatform (""macos"")]
#else
	[iOS (8,0)]
	[TV (10,2)]
#endif
	public partial class VTCompressionSession {
	}
}
", new ProcessOptions () { AssemblyPath = SystemXI, AddDefaultIntroducedPath = SystemAssemblies });

			TestUtilities.TestProcess (@"namespace VideoToolbox {
	[iOS (8,0)]
	[TV (10,2)]
	public partial class VTCompressionSession {
		[Mac (10,9)]
		public VTStatus PrepareToEncodeFrames () {}
	}
}
", @"namespace VideoToolbox {
#if NET
	[SupportedOSPlatform (""ios8.0"")]
	[SupportedOSPlatform (""tvos10.2"")]
	[SupportedOSPlatform (""maccatalyst"")]
	[SupportedOSPlatform (""macos"")]
#else
	[iOS (8,0)]
	[TV (10,2)]
#endif
	public partial class VTCompressionSession {
#if NET
		[SupportedOSPlatform (""macos10.9"")]
		[SupportedOSPlatform (""ios8.0"")]
		[SupportedOSPlatform (""tvos10.2"")]
		[SupportedOSPlatform (""maccatalyst"")]
#else
		[Mac (10,9)]
#endif
		public VTStatus PrepareToEncodeFrames () {}
	}
}
", new ProcessOptions () { AssemblyPath = SystemXI, AddDefaultIntroducedPath = SystemAssemblies });

			TestUtilities.TestProcess (@"namespace VideoToolbox {
	[iOS (8,0)]
	[TV (10,2)]
	public partial class VTCompressionSession {
		GCHandle callbackHandle;
	}
}
", @"namespace VideoToolbox {
#if NET
	[SupportedOSPlatform (""ios8.0"")]
	[SupportedOSPlatform (""tvos10.2"")]
	[SupportedOSPlatform (""maccatalyst"")]
	[SupportedOSPlatform (""macos"")]
#else
	[iOS (8,0)]
	[TV (10,2)]
#endif
	public partial class VTCompressionSession {
		GCHandle callbackHandle;
	}
}
", new ProcessOptions () { AssemblyPath = SystemXI, AddDefaultIntroducedPath = SystemAssemblies });

		}

		[Fact]
		public void EnumExampleWithAssemblies ()
		{
			const string testCase = @"using Foundation;
	public enum NSFontCollectionAction
	{
		Unknown,
		Shown,
		Hidden,
		Renamed
	}

";
			TestUtilities.TestProcess (testCase, testCase, new ProcessOptions () { AssemblyPath = SystemXI, AddDefaultIntroducedPath = SystemAssemblies });
		}
	}
}
