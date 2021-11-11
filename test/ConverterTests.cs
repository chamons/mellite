using System;

using Xunit;

namespace mellite.tests {
	public class ConverterTests {
		string GetConditionalAttributeBlock (string xamarinAttribute, string newAttribute, int spaceCount, int? newAttributeSpaceCount = null)
		{
			newAttributeSpaceCount ??= spaceCount;
			return $@"#if NET
{new String (' ', (int) newAttributeSpaceCount)}{newAttribute}
#else
{new String (' ', spaceCount)}{xamarinAttribute}
#endif";

		}

		string GetTestProgram (string body) => $@"using System;
using ObjCRuntime;

namespace binding
{{
{body}
}}
";

		void TestConversion (string original, string expected)
		{
#if true
			Console.WriteLine (Converter.ConvertText (original));
			Console.WriteLine (expected);
#endif

			Assert.Equal (expected, Converter.ConvertText (original), ignoreLineEndingDifferences: true);
		}

		void TestClassAttributeConversion (string xamarinAttribute, string newAttribute, string? xamarinAttributeAfterConvert = null)
		{
			string body = @"{0}
    public partial class Class1
    {{
        public void Foo () {{}}
    }}";
			xamarinAttributeAfterConvert ??= xamarinAttribute;
			var original = GetTestProgram (string.Format (body, "    " + xamarinAttribute));
			var expected = GetTestProgram (string.Format (body, GetConditionalAttributeBlock (xamarinAttributeAfterConvert, newAttribute, 4)));
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
			var original = GetTestProgram (string.Format (body, "        " + xamarinAttribute));
			var expected = GetTestProgram (string.Format (body, GetConditionalAttributeBlock (xamarinAttributeAfterConvert, newAttribute, 8, newAttributeSpaceCount)));
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
			var original = GetTestProgram (string.Format (body, "        " + xamarinAttribute));
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
        [Obsolete (""Starting with ios11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif");

			TestMethodAttributeConversion ("[Deprecated (PlatformName.MacOSX, 11, 0)][Deprecated (PlatformName.iOS, 11, 0)]", @"[UnsupportedOSPlatform (""macos11.0"")]
        [UnsupportedOSPlatform (""ios11.0"")]
#if MONOMAC
        [Obsolete (""Starting with macos11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif IOS
        [Obsolete (""Starting with ios11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif", xamarinAttributeAfterConvert: @"[Deprecated (PlatformName.MacOSX, 11, 0)]
        [Deprecated (PlatformName.iOS, 11, 0)]");

			// iOS must be ordered at end
			TestMethodAttributeConversion ("[Deprecated (PlatformName.MacOSX, 11, 0)][Deprecated (PlatformName.iOS, 11, 0)][Deprecated (PlatformName.MacCatalyst, 11, 0)][Deprecated (PlatformName.TvOS, 11, 0)]", @"[UnsupportedOSPlatform (""macos11.0"")]
        [UnsupportedOSPlatform (""maccatalyst11.0"")]
        [UnsupportedOSPlatform (""tvos11.0"")]
        [UnsupportedOSPlatform (""ios11.0"")]
#if MONOMAC
        [Obsolete (""Starting with macos11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif __MACCATALYST__
        [Obsolete (""Starting with maccatalyst11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif TVOS
        [Obsolete (""Starting with tvos11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif IOS
        [Obsolete (""Starting with ios11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif", xamarinAttributeAfterConvert: @"[Deprecated (PlatformName.MacOSX, 11, 0)]
        [Deprecated (PlatformName.iOS, 11, 0)]
        [Deprecated (PlatformName.MacCatalyst, 11, 0)]
        [Deprecated (PlatformName.TvOS, 11, 0)]");

			// Watch is not a platform we should output
			TestMethodAttributeConversion ("[Deprecated (PlatformName.MacOSX, 11, 0)][Deprecated (PlatformName.iOS, 11, 0)][Deprecated (PlatformName.WatchOS, 11, 0)]", @"[UnsupportedOSPlatform (""macos11.0"")]
        [UnsupportedOSPlatform (""ios11.0"")]
#if MONOMAC
        [Obsolete (""Starting with macos11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif IOS
        [Obsolete (""Starting with ios11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif", xamarinAttributeAfterConvert: @"[Deprecated (PlatformName.MacOSX, 11, 0)]
        [Deprecated (PlatformName.iOS, 11, 0)]
        [Deprecated (PlatformName.WatchOS, 11, 0)]");
		}

		[Fact]
		public void Obsolete ()
		{
			TestMethodAttributeConversion ("[Obsoleted (PlatformName.iOS, 11, 0)]", @"#if IOS
        [Obsolete (""Starting with ios11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif", newAttributeSpaceCount: 0);

			TestMethodAttributeConversion (@"[Obsoleted (PlatformName.iOS, 11, 0)]
        [Obsoleted (PlatformName.MacOSX, 11, 0)]", @"#if MONOMAC
        [Obsolete (""Starting with macos11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif IOS
        [Obsolete (""Starting with ios11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif", newAttributeSpaceCount: 0);

			TestMethodAttributeConversion (@"[Obsoleted (PlatformName.iOS, 11, 0)]
        [Obsoleted (PlatformName.MacOSX, 11, 0)]
        [Obsoleted (PlatformName.TvOS, 11, 0)]", @"#if MONOMAC
        [Obsolete (""Starting with macos11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif TVOS
        [Obsolete (""Starting with tvos11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif IOS
        [Obsolete (""Starting with ios11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
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
        [Obsolete (""Starting with maccatalyst"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#elif IOS
        [Obsolete (""Starting with ios11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif", newAttributeSpaceCount: 0);

			TestMethodAttributeConversion (@"[Obsoleted (PlatformName.iOS, 11, 0)]
        [Deprecated (PlatformName.MacCatalyst)]", @"[UnsupportedOSPlatform (""maccatalyst"")]
#if __MACCATALYST__
        [Obsolete (""Starting with maccatalyst"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
#if IOS
        [Obsolete (""Starting with ios11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
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
        [Flags]
        [Obsolete]
#if NET
        [UnsupportedOSPlatform (""ios11.0"")]
#else
        [Unavailable (PlatformName.iOS, 11, 0)]
#endif
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
        [Obsolete (""Starting with macos11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
#endif
        [UnsupportedOSPlatform (""maccatalyst"")]
#if IOS
        [Obsolete (""Starting with ios11.0"", DiagnosticId = ""BI1234"", UrlFormat = ""https://github.com/xamarin/xamarin-macios/wiki/Obsolete"")]
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
	}
}
