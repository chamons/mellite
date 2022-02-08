using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

using Mono.Cecil;

namespace mellite {
	public class AssemblyHarvestInfo {
		public ReadOnlyDictionary<string, List<HarvestedAvailabilityInfo>> Data;

		public AssemblyHarvestInfo (Dictionary<string, List<HarvestedAvailabilityInfo>> data)
		{
			Data = new ReadOnlyDictionary<string, List<HarvestedAvailabilityInfo>> (data);
		}
	}

	// Harvest information from a given .NET assembly to inform AttributeHarvester processing
	public class AssemblyHarvester {
		Dictionary<string, List<HarvestedAvailabilityInfo>> Data = new Dictionary<string, List<HarvestedAvailabilityInfo>> ();

		public AssemblyHarvestInfo Harvest (string path, bool addDefaultIntroduced = false)
		{
			Data = new Dictionary<string, List<HarvestedAvailabilityInfo>> ();

			var resolver = new DefaultAssemblyResolver ();
			resolver.AddSearchDirectory (Path.GetDirectoryName (path));
			var parameters = new ReaderParameters () {
				AssemblyResolver = resolver,
			};
			var assembly = AssemblyDefinition.ReadAssembly (path, parameters);
			foreach (var module in assembly.Modules) {
				foreach (var type in module.Types) {
					Data [type.FullName] = GetAvailabilityAttributes (type.CustomAttributes).ToList ();
					if (addDefaultIntroduced) {
						ProcessDefaultIntroduced (type, Data);
					}
				}
			}
			return new AssemblyHarvestInfo (Data);
		}

		void ProcessDefaultIntroduced (TypeDefinition type, Dictionary<string, List<HarvestedAvailabilityInfo>> data)
		{
			string ns = type.FullName.Split ('.').First ();
			List<string> defaultIntroducedPlatforms;
			switch (ns) {
			case "AppKit":
				defaultIntroducedPlatforms = new List<string> { "PlatformName.MacOSX" };
				break;
			case "UIKit":
				defaultIntroducedPlatforms = new List<string> { "PlatformName.iOS" };
				break;
			default:
				defaultIntroducedPlatforms = new List<string> { "PlatformName.MacOSX", "PlatformName.iOS" };
				break;
			}

			var introduced = Data [type.FullName].Where (d => d.Attribute.Name.ToString () == "Introduced").ToList ();

			foreach (var platform in defaultIntroducedPlatforms) {
				if (!introduced.Any (i => i.Attribute.ArgumentList!.Arguments [0].ToString () == platform)) {
					Data [type.FullName].Add (new HarvestedAvailabilityInfo ("Introduced", platform));
				}
			}
		}

		IEnumerable<HarvestedAvailabilityInfo> GetAvailabilityAttributes (IEnumerable<CustomAttribute> attributes)
		{
			var availability = new List<HarvestedAvailabilityInfo> ();
			foreach (var attribute in attributes.Where (a => IsAvailabilityAttribute (a))) {
				var args = attribute.ConstructorArguments;
				var kind = attribute.AttributeType.Name.Substring (0, attribute.AttributeType.Name.Length - 9 /* len(Attribute) */);
				var message = String.IsNullOrEmpty ((string) args.Last ().Value) ? "" : $", message: \"{(string) args.Last ().Value}\"";
				// Since revision has a default value of -1, we have to ignore it when 'unset'
				var revision = ((byte) args [args.Count - 2].Value) == 255 ? "" : $", {args [args.Count - 2].Value}";

				var platform = GetPlatformName ((byte) args [0].Value);
				if (platform == null) {
					continue;
				}
				switch (args.Count) {
				case 3:
					availability.Add (new HarvestedAvailabilityInfo (kind, $"{platform}{revision}{message}"));
					break;
				case 4:
					availability.Add (new HarvestedAvailabilityInfo (kind, $"{platform}, {args [1].Value}{revision}{message}]"));
					break;
				case 5: {
					availability.Add (new HarvestedAvailabilityInfo (kind, $"{platform}, {args [1].Value}, {args [2].Value}{revision}{message}"));
					break;
				}
				case 6: {
					availability.Add (new HarvestedAvailabilityInfo (kind, $"{platform}, {platform}, {args [1].Value}, {args [2].Value}, {args [3].Value}{revision}{message}]"));
					break;
				}
				default:
					throw new InvalidOperationException ();
				}
			}
			return availability;
		}

		int GetMinorValue (object o)
		{
			switch (o) {
			case byte b:
				return (int) b;
			case int i:
				return i;
			default:
				throw new InvalidOperationException ();
			}
		}

		string? GetPlatformName (byte v)
		{
			switch (v) {
			case 1: // MacOSX,
				return "PlatformName.MacOSX";
			case 2: // iOS,
				return "PlatformName.iOS";
			case 3: // WatchOS,
				return "PlatformName.WatchOS";
			case 4: // TvOS,
				return "PlatformName.TvOS";
			case 5: // MacCatalyst
				return "PlatformName.MacCatalyst";
			case 0: // None,
			case 6: // UIKitForMac (Obsolete)
			default:
				return null;
			}
		}

		bool IsAvailabilityAttribute (CustomAttribute attribute)
		{
			switch (attribute.AttributeType.Name) {
			case "IntroducedAttribute":
			case "DeprecatedAttribute":
			case "UnavailableAttribute":
			case "ObsoletedAttribute":
				return true;
			default:
				return false;
			}
		}
	}
}