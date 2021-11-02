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

        void TestConversion(string xamarinAttribute, string newAttribute)
        {
            string result = Converter.ConvertText(GetTestProgram(xamarinAttribute));
            Assert.Equal(GetTestProgramWithConditional(xamarinAttribute, newAttribute), result);
        }

        [Fact]
        public void SingleAttributeOnClass()
        {
            TestConversion("[Introduced (PlatformName.MacOSX, 10, 0)]", "[SupportedOSPlatform(\"macos10.0\")]");
            TestConversion("[Introduced (PlatformName.iOS, 6, 0)]", "[SupportedOSPlatform(\"ios6.0\")]");
            TestConversion("[Introduced (PlatformName.iOS, 6, 0), Introduced (PlatformName.MacOSX, 10, 0)]", @"[SupportedOSPlatform(""ios6.0""),SupportedOSPlatform(""macos10.0"")]");
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
