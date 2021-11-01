using System;
using System.IO;
using System.Collections.Generic;
using mellite;

using Xunit;

namespace mellite.tests
{
    public class ConverterTests
    {
        string GetTestProgram(string attribute)
        {
            return $@"using System;
using ObjCRuntime;

namespace binding
{{
    {attribute}
    public partial class Class1
    {{
        public void Foo () {{}}
    }}
}}
";
        }

        [Fact]
        public void SingleAttributeOnClass()
        {
            string result = Converter.ConvertText(GetTestProgram("[Introduced (PlatformName.MacOSX, 10, 0)]"));
            Assert.Equal(GetTestProgram("[SupportedOSPlatform(\"macos10.0\")]"), result);

            result = Converter.ConvertText(GetTestProgram("[Introduced (PlatformName.iOS, 6, 0)]"));
            Assert.Equal(GetTestProgram("[SupportedOSPlatform(\"ios6.0\")]"), result);
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
