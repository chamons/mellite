using System;

using Xunit;

namespace mellite.tests
{
    public class ConverterTests
    {
        string GetConditionalAttributeBlock(string xamarinAttribute, string newAttribute, int spaceCount)
        {
            var spaces = new String(' ', spaceCount);
            return $@"#if !NET
{spaces}{xamarinAttribute}
#else
{spaces}{newAttribute}
#endif";

        }

        string GetTestProgram(string body) => $@"using System;
using ObjCRuntime;

namespace binding
{{
{body}
}}
";

        void TestConversion(string original, string expected)
        {
#if true
            Console.WriteLine(original);
            Console.WriteLine(Converter.ConvertText(original));
            Console.WriteLine(expected);
#endif

            Assert.Equal(expected, Converter.ConvertText(original));
        }

        void TestClassAttributeConversion(string xamarinAttribute, string newAttribute, string? xamarinAttributeAfterConvert = null)
        {
            string body = @"{0}
    public partial class Class1
    {{
        public void Foo () {{}}
    }}";
            xamarinAttributeAfterConvert ??= xamarinAttribute;
            var original = GetTestProgram(string.Format(body, "    " + xamarinAttribute));
            var expected = GetTestProgram(string.Format(body, GetConditionalAttributeBlock(xamarinAttributeAfterConvert, newAttribute, 4)));
            TestConversion(original, expected);
        }

        [Fact]
        public void SingleAttributeOnClass()
        {
            TestClassAttributeConversion("[Introduced (PlatformName.MacOSX, 10, 0)]", "[SupportedOSPlatform(\"macos10.0\")]");
            TestClassAttributeConversion("[Introduced (PlatformName.iOS, 6, 0)]", "[SupportedOSPlatform(\"ios6.0\")]");
            TestClassAttributeConversion("[Introduced (PlatformName.iOS, 6, 0), Introduced (PlatformName.MacOSX, 10, 0)]", @"[SupportedOSPlatform(""ios6.0"")]
    [SupportedOSPlatform(""macos10.0"")]", xamarinAttributeAfterConvert: @"[Introduced (PlatformName.iOS, 6, 0)]
    [Introduced (PlatformName.MacOSX, 10, 0)]");
            TestClassAttributeConversion("[Introduced (PlatformName.iOS, 6, 0), Introduced (PlatformName.MacOSX, 10, 0), Introduced (PlatformName.MacCatalyst, 10, 0)]", @"[SupportedOSPlatform(""ios6.0"")]
    [SupportedOSPlatform(""macos10.0"")]
    [SupportedOSPlatform(""maccatalyst10.0"")]", xamarinAttributeAfterConvert: @"[Introduced (PlatformName.iOS, 6, 0)]
    [Introduced (PlatformName.MacOSX, 10, 0)]
    [Introduced (PlatformName.MacCatalyst, 10, 0)]");
            TestClassAttributeConversion(@"[Introduced (PlatformName.iOS, 6, 0)]
    [Introduced (PlatformName.MacOSX, 10, 0)]
    [Introduced (PlatformName.MacCatalyst, 10, 0)]", @"[SupportedOSPlatform(""ios6.0"")]
    [SupportedOSPlatform(""macos10.0"")]
    [SupportedOSPlatform(""maccatalyst10.0"")]");
        }

        void TestMethodAttributeConversion(string xamarinAttribute, string newAttribute, string? xamarinAttributeAfterConvert = null)
        {
            string body = @"    public partial class Class1
    {{
{0}
        public void Foo () {{}}

        public void Bar () {{}}
    }}";
            xamarinAttributeAfterConvert ??= xamarinAttribute;
            var original = GetTestProgram(string.Format(body, "        " + xamarinAttribute));
            var expected = GetTestProgram(string.Format(body, GetConditionalAttributeBlock(xamarinAttributeAfterConvert, newAttribute, 8)));
            TestConversion(original, expected);
        }

        [Fact]
        public void SingleAttributeOnMethod()
        {
            TestMethodAttributeConversion("[Introduced (PlatformName.MacOSX, 10, 0)]", "[SupportedOSPlatform(\"macos10.0\")]");
            TestMethodAttributeConversion("[Introduced (PlatformName.iOS, 6, 0)]", "[SupportedOSPlatform(\"ios6.0\")]");
            TestMethodAttributeConversion("[Introduced (PlatformName.iOS, 6, 0), Introduced (PlatformName.MacOSX, 10, 0)]", @"[SupportedOSPlatform(""ios6.0"")]
        [SupportedOSPlatform(""macos10.0"")]", xamarinAttributeAfterConvert: @"[Introduced (PlatformName.iOS, 6, 0)]
        [Introduced (PlatformName.MacOSX, 10, 0)]");
            TestMethodAttributeConversion("[Introduced (PlatformName.iOS, 6, 0), Introduced (PlatformName.MacOSX, 10, 0), Introduced (PlatformName.MacCatalyst, 10, 0)]", @"[SupportedOSPlatform(""ios6.0"")]
        [SupportedOSPlatform(""macos10.0"")]
        [SupportedOSPlatform(""maccatalyst10.0"")]", xamarinAttributeAfterConvert: @"[Introduced (PlatformName.iOS, 6, 0)]
        [Introduced (PlatformName.MacOSX, 10, 0)]
        [Introduced (PlatformName.MacCatalyst, 10, 0)]");
            TestMethodAttributeConversion(@"[Introduced (PlatformName.iOS, 6, 0)]
        [Introduced (PlatformName.MacOSX, 10, 0)]
        [Introduced (PlatformName.MacCatalyst, 10, 0)]", @"[SupportedOSPlatform(""ios6.0"")]
        [SupportedOSPlatform(""macos10.0"")]
        [SupportedOSPlatform(""maccatalyst10.0"")]");
        }

        [Fact]
        public void NewLinesBetweenElements()
        {
            //             TestConversion(
            //             $@"using System;
            // using ObjCRuntime;

            // namespace binding
            // {{
            //     public partial class Class1
            //     {{
            //         [Introduced (PlatformName.MacOSX, 10, 0)]
            //         public void Foo () {{}}

            //         [Introduced (PlatformName.iOS, 6, 0)]
            //         public void Bar () {{}}

            //         [Introduced (PlatformName.MacOSX, 10, 0)]
            //         public void Buzz () {{}}

            //         public void FooBar () {{}}
            //     }}
            // }}
            // ",
            //             $@"using System;
            // using ObjCRuntime;

            // namespace binding
            // {{
            //     public partial class Class1
            //     {{
            // #if !NET
            //         [Introduced (PlatformName.MacOSX, 10, 0)]
            // #else
            //         [SupportedOSPlatform(""macos10.0"")]
            // #endif
            //         public void Foo () {{}}

            // #if !NET
            //         [Introduced (PlatformName.iOS, 6, 0)]
            // #else
            //         [SupportedOSPlatform(""ios6.0"")]
            // #endif
            //         public void Bar () {{}}

            // #if !NET
            //         [Introduced (PlatformName.MacOSX, 10, 0)]
            // #else
            //         [SupportedOSPlatform(""macos10.0"")]
            // #endif
            //         public void Buzz () {{}}

            //         public void FooBar () {{}}
            //     }}
            // }}
            // ");
            // CRLF CRLF TAB CRLF CRLF
            TestConversion(
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
#if !NET
                    [Introduced (PlatformName.MacOSX, 10, 0)]
#else
                    [SupportedOSPlatform(""macos10.0"")]
#endif
                    public void Foo () {{}}




#if !NET
                    [Introduced (PlatformName.iOS, 6, 0)]
#else
                    [SupportedOSPlatform(""ios6.0"")]
#endif
                    public void Bar () {{}}
                }}
            }}
            ");
        }

        [Theory]
        [InlineData("PlatformName.MacOSX", "macos")]
        [InlineData("PlatformName.iOS", "ios")]
        [InlineData("PlatformName.TvOS", "tvos")]
        [InlineData("PlatformName.MacCatalyst", "maccatalyst")]
        [InlineData("PlatformName.None", null)]
        [InlineData("PlatformName.WatchOS", null)]
        [InlineData("PlatformName.UIKitForMac", null)]
        public void PlatformNameParsing(string platformName, string? netName)
        {
            Assert.Equal(netName, PlatformArgumentParser.Parse(platformName));
        }
    }
}
