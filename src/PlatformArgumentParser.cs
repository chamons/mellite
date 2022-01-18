using System.Linq;
using System.Text;

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
			if (node.ArgumentList == null) {
				return "";
			}
			// Look at every argument type
			// Introduced (Platform, Major, Minor, Point, Message) is the full attribute, but it could also be
			// (Platform, Major, Minor, Message) or (Platform, Major, Minor, Point), etc.
			var version = new StringBuilder ();
			int count = 0;
			foreach (var arg in node.ArgumentList!.Arguments.Select (s => s.ToString ())) {
				if (int.TryParse (arg, out var bit)) {
					count += 1;
					switch (count) {
					case 1:
						// On the first one don't appent a period
						version.Append (arg);
						break;
					case 3:
						// If we have a point, ignore it if set to 0
						// We do this as the assembly harvester will read
						// every single attribute as set to 0
						if (bit != 0) {
							version.Append (".");
							version.Append (arg);
						}
						break;
					default:
						version.Append (".");
						version.Append (arg);
						break;
					}
				}
			}
			return version.ToString ();
		}
	}
}
