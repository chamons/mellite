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

	class PlatformAssemblyExistenceFinder {
		// FullName -> (exists on PlatformName)
		Dictionary<string, HashSet<string>> AssemblyAvailability = new Dictionary<string, HashSet<string>> ();

		static (string, string []) [] PlatformMapping = new (string, string []) [] {
					("Xamarin.iOS.dll", new [] { "PlatformName.iOS", "PlatformName.MacCatalyst"}),
					("Xamarin.Mac.dll", new [] { "PlatformName.MacOSX"}),
					("Xamarin.TVOS.dll", new [] { "PlatformName.TvOS"})};

		public PlatformAssemblyExistenceFinder (string addDefaultIntroducedPath)
		{
			foreach (var (assemblyName, platformNames) in PlatformMapping) {

				var resolver = new DefaultAssemblyResolver ();
				resolver.AddSearchDirectory (addDefaultIntroducedPath);
				var parameters = new ReaderParameters () {
					AssemblyResolver = resolver,
				};
				var assembly = AssemblyDefinition.ReadAssembly (Path.Join (addDefaultIntroducedPath, assemblyName), parameters);
				foreach (var module in assembly.Modules) {
					foreach (var type in module.Types) {
						foreach (var platformName in platformNames) {
							Add (type.FullName, platformName);
						}
					}
				}
			}
		}

		void Add (string fullName, string platformName)
		{
			if (AssemblyAvailability.TryGetValue (fullName, out var v)) {
				v.Add (platformName);
			} else {
				AssemblyAvailability [fullName] = new HashSet<string> (new [] { platformName });
			}
		}

		public IEnumerable<string> PlatformsFoundOn (string fullName)
		{
			switch (fullName.Split (".").First ()) {
			case "AppKit": {
				string swapped = "UIKit." + String.Join (".", fullName.Split (".").Skip (1));
				if (AssemblyAvailability.TryGetValue (swapped, out var swappedValue)) {
					return AssemblyAvailability [fullName].Union (swappedValue);
				} else {
					return AssemblyAvailability [fullName];
				}
			}
			case "UIKit": {
				string swapped = "AppKit." + String.Join (".", fullName.Split (".").Skip (1));
				if (AssemblyAvailability.TryGetValue (swapped, out var swappedValue)) {
					return AssemblyAvailability [fullName].Union (swappedValue);
				} else {
					return AssemblyAvailability [fullName];
				}
			}
			default:
				return AssemblyAvailability [fullName];
			}
		}
	}

	// Harvest information from a given .NET assembly to inform AttributeHarvester processing
	public class AssemblyHarvester {
		Dictionary<string, List<HarvestedAvailabilityInfo>> Data = new Dictionary<string, List<HarvestedAvailabilityInfo>> ();
		PlatformAssemblyExistenceFinder? Finder;

		public AssemblyHarvestInfo Harvest (string path, string? addDefaultIntroducedPath = null)
		{
			Data = new Dictionary<string, List<HarvestedAvailabilityInfo>> ();
			if (addDefaultIntroducedPath != null) {
				Finder = new PlatformAssemblyExistenceFinder (addDefaultIntroducedPath);
			}

			var resolver = new DefaultAssemblyResolver ();
			resolver.AddSearchDirectory (Path.GetDirectoryName (path));
			var parameters = new ReaderParameters () {
				AssemblyResolver = resolver,
			};
			var assembly = AssemblyDefinition.ReadAssembly (path, parameters);
			foreach (var module in assembly.Modules) {
				foreach (var type in module.Types) {
					Data [type.FullName] = GetAvailabilityAttributes (type.CustomAttributes).ToList ();
					if (Finder != null) {
						ProcessDefaultIntroduced (type);
					}
				}
			}
			return new AssemblyHarvestInfo (Data);
		}

		void ProcessDefaultIntroduced (TypeDefinition type)
		{
			var platformsAlreadyIntroduced = Data [type.FullName].Where (d => d.Attribute.Name.ToString () == "Introduced");

			var existing = new HashSet<string> (platformsAlreadyIntroduced.Select (i => i.Attribute.ArgumentList!.Arguments [0].ToString ()));
			var platformsFound = Finder!.PlatformsFoundOn (type.FullName);
			foreach (var platform in platformsFound.Where (p => !existing.Contains (p))) {
				Data [type.FullName].Add (new HarvestedAvailabilityInfo ("Introduced", platform, true));
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