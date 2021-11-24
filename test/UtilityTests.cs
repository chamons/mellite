using System;

using Xunit;

namespace mellite.tests {
	public class UtilityTests {
		[Fact]
		public void StripCommentTests ()
		{
			Assert.Equal ("[Foo]", StripperHelpers.TrimLine ("[Foo]"));
			Assert.Equal ("[Foo]", StripperHelpers.TrimLine ("[Foo]  "));
			Assert.Equal ("[Foo]", StripperHelpers.TrimLine ("[Foo] // Comment"));
			Assert.Equal ("[Foo]", StripperHelpers.TrimLine ("[Foo] /* Comment */"));
		}
	}
}
