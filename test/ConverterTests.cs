using System;
using System.IO;
using System.Collections.Generic;
using mellite;

using Xunit;

namespace mellite.tests
{
    public class ConverterTests
    {
        string GetTestProgramWithConditional(string xamarinAttribute, string newAttribute)
        {
            string attribute = $@"#if !NET
    {xamarinAttribute}
#else
    {newAttribute}
#endif";
            return GetTestProgramBase(attribute);

        }

        string GetTestProgram(string attribute)
        {
            return GetTestProgramBase("    " + attribute);
        }

        string GetTestProgramBase(string attributeCode)
        {
            return $@"using System;
using ObjCRuntime;

namespace binding
{{
{attributeCode}
    public partial class Class1
    {{
        public void Foo () {{}}
    }}
}}
";
        }

        void TestConversion(string original, string expected)
        {
            Assert.Equal(expected, Converter.ConvertText(original));
        }

        void TestAttributeConversion(string xamarinAttribute, string newAttribute)
        {
            TestConversion(GetTestProgram(xamarinAttribute), GetTestProgramWithConditional(xamarinAttribute, newAttribute));
        }

        [Fact]
        public void SingleAttributeOnClass()
        {
            TestAttributeConversion("[Introduced (PlatformName.MacOSX, 10, 0)]", "[SupportedOSPlatform(\"macos10.0\")]");
            TestAttributeConversion("[Introduced (PlatformName.iOS, 6, 0)]", "[SupportedOSPlatform(\"ios6.0\")]");
            TestAttributeConversion("[Introduced (PlatformName.iOS, 6, 0), Introduced (PlatformName.MacOSX, 10, 0)]", @"[SupportedOSPlatform(""ios6.0""),SupportedOSPlatform(""macos10.0"")]");
        }


        [Fact]
        public void NewLinesBetweenElements()
        {
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
