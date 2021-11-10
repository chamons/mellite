using System;

using Xunit;

namespace mellite.tests {
	public class PlatformArgumentParserTests {
		[Theory]
		[InlineData ("PlatformName.MacOSX", "macos")]
		[InlineData ("PlatformName.iOS", "ios")]
		[InlineData ("PlatformName.TvOS", "tvos")]
		[InlineData ("PlatformName.MacCatalyst", "maccatalyst")]
		[InlineData ("PlatformName.None", null)]
		[InlineData ("PlatformName.WatchOS", null)]
		[InlineData ("PlatformName.UIKitForMac", null)]
		public void PlatformNameParsing (string platformName, string? netName)
		{
			Assert.Equal (netName, PlatformArgumentParser.GetPlatformFromAttributeName (platformName));
		}

		[Theory]
		[InlineData ("PlatformName.MacOSX", "MONOMAC")]
		[InlineData ("PlatformName.iOS", "IOS")]
		[InlineData ("PlatformName.TvOS", "TVOS")]
		[InlineData ("PlatformName.MacCatalyst", "__MACCATALYST__")]
		[InlineData ("PlatformName.None", null)]
		[InlineData ("PlatformName.WatchOS", null)]
		[InlineData ("PlatformName.UIKitForMac", null)]
		public void PlatformDefineParsing (string platformName, string? netName)
		{
			Assert.Equal (netName, PlatformArgumentParser.GetDefineFromAttributeName (platformName));
		}
	}
}
