using System;
using System.IO;
using System.Collections.Generic;
using mellite;

using Xunit;

namespace mellite.tests
{
    public class ConverterTests
    {
        [Fact]
        public void SingleAttributeOnClass()
        {
            string original = @"using System;
using ObjCRuntime;

namespace binding
{
    [Introduced (PlatformName.MacOSX, 10, 0)]
    public partial class Class1
    {
        public void Foo () {}
    }
}
";
            string result = Converter.ConvertText(original);

            Assert.Equal(@"using System;
using ObjCRuntime;

namespace binding
{
    [SupportedOSPlatform(""macos10.0"")]
    public partial class Class1
    {
        public void Foo () {}
    }
}
", result);
        }
    }
}
