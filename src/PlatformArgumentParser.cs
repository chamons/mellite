using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace mellite {
	public static class PlatformArgumentParser {

		public static string? GetPlatformFromNode (AttributeSyntax node)
		{
			string? value = null;
			if (node.ArgumentList?.Arguments.Count > 0) {
				value = GetPlatformFromAttributeName (node.ArgumentList!.Arguments [0].ToString ());
			}
			if (value is null) {
				value = GetPlatformFromNodeKind (node);
			}
			return value;
		}

		public static string? GetDefineFromNode (AttributeSyntax node)
		{
			string? value = null;

			if (node.ArgumentList?.Arguments.Count > 0) {
				value = GetDefineFromAttributeName (node.ArgumentList!.Arguments [0].ToString ());
			}
			if (value is null) {
				value = GetDefineFromNodeKind (node);
			}
			return value;
		}

		public static string? GetPlatformFromNodeKind (AttributeSyntax node)
		{
			switch (node.Name.ToString ()) {
			case "NoMac":
			case "Mac":
				return "macos";
			case "NoiOS":
			case "iOS":
				return "ios";
			case "NoTV":
			case "TV":
				return "tvos";
			case "NoMacCatalyst":
			case "MacCatalyst":
				return "maccatalyst";
			}
			return null;
		}

		public static string? GetDefineFromNodeKind (AttributeSyntax node)
		{
			switch (node.Name.ToString ()) {
			case "NoMac":
			case "Mac":
				return "MONOMAC";
			case "NoiOS":
			case "iOS":
				return "IOS";
			case "NoTV":
			case "TV":
				return "TVOS";
			case "NoMacCatalyst":
			case "MacCatalyst":
				return "__MACCATALYST__";
			}
			return null;
		}

		public static string? GetPlatformFromAttributeName (string s)
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

		public static string? GetDefineFromAttributeName (string s)
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

		public static string GetVersionFromNode (AttributeSyntax node)
		{
			switch (node.ArgumentList?.Arguments.Count) {
			case 2: // iOS (Major, Minor)
				return $"{node.ArgumentList!.Arguments [0]}.{node.ArgumentList!.Arguments [1]}";
			case 3: // Introduced (Platform, Major, Minor)
			case 4: // Introduced (Platform, Major, Minor, Message)
			case 5: // Introduced (Platform, Major, Minor, Point, Message) - Ignore Point
				return $"{node.ArgumentList!.Arguments [1]}.{node.ArgumentList!.Arguments [2]}";
			default:
				return "";
			}
		}
	}
}
