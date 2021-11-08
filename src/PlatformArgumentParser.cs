namespace mellite {
	public static class PlatformArgumentParser {
		public static string? Parse (string s)
		{
			switch (s) {
			case "PlatformName.MacOSX":
				return "macos";
			case "PlatformName.iOS":
				return "ios";
			case "PlatformName.TvOS":
				return "tvos";
			case "PlatformName.MacCatalyst":
				return "maccatalyst";
			case "PlatformName.None":
			case "PlatformName.WatchOS":
			case "PlatformName.UIKitForMac":
			default:
				return null;
			}
		}

		public static string? ParseDefine (string s)
		{
			switch (s) {
			case "PlatformName.MacOSX":
				return "MONOMAC";
			case "PlatformName.iOS":
				return "IOS";
			case "PlatformName.TvOS":
				return "TVOS";
			case "PlatformName.MacCatalyst":
				return "__MACCATALYST__";
			case "PlatformName.None":
			case "PlatformName.WatchOS":
			case "PlatformName.UIKitForMac":
			default:
				return null;
			}
		}
	}
}
