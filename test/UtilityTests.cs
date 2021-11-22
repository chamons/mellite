using System;

using Xunit;

namespace mellite.tests {
	public class UtilityTests {
		[Fact]
		public void StripCommentTests ()
		{
			Assert.Equal ("[Foo]", StripperBase.TrimLine ("[Foo]"));
			Assert.Equal ("[Foo]", StripperBase.TrimLine ("[Foo]  "));
			Assert.Equal ("[Foo]", StripperBase.TrimLine ("[Foo] // Comment"));
			Assert.Equal ("[Foo]", StripperBase.TrimLine ("[Foo] /* Comment */"));
		}
	}
}
