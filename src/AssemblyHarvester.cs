using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mono.Cecil;

namespace mellite {

	// Harvest information from a given .NET assembly to inform AttributeHarvester processing
	public class AssemblyHarvester {
		Dictionary<string, HarvestedAvailabilityInfo> Data = new Dictionary<string, HarvestedAvailabilityInfo> ();

		public Dictionary<string, HarvestedAvailabilityInfo> Harvest (string path)
		{
			var resolver = new DefaultAssemblyResolver ();
			resolver.AddSearchDirectory (Path.GetDirectoryName (path));
			var parameters = new ReaderParameters () {
				AssemblyResolver = resolver,
			};
			var assembly = AssemblyDefinition.ReadAssembly (path, parameters);
			foreach (var module in assembly.Modules) {
				foreach (var type in module.Types) {
					foreach (var method in type.Methods) {
						Process (method, type);
					}
					foreach (var prop in type.Properties) {
						Process (prop, type);
					}
					foreach (var e in type.Events) {
						Process (e, type);
					}
					if (type.IsEnum) {
						Process (type, null);
					}
				}
			}
			return Data;
		}

		void Process (MemberReference member, MemberReference? parent)
		{
			var attributes = new List<HarvestedAvailabilityInfo> ();
			switch (member) {
			case TypeDefinition definition:
				attributes = GetAvailabilityAttributes (definition.CustomAttributes).ToList ();
				break;
			case PropertyDefinition prop:
				attributes = GetAvailabilityAttributes (prop.CustomAttributes).ToList ();
				break;
			case MethodDefinition meth:
				attributes = GetAvailabilityAttributes (meth.CustomAttributes).ToList ();
				break;
			case EventDefinition e:
				attributes = GetAvailabilityAttributes (e.CustomAttributes).ToList ();
				break;
			}
		}

		IEnumerable<HarvestedAvailabilityInfo> GetAvailabilityAttributes (IEnumerable<CustomAttribute> attributes)
		{
			var availability = new List<HarvestedAvailabilityInfo> ();
			foreach (var attribute in attributes.Where (a => IsAvailabilityAttribute (a))) {
				var args = attribute.ConstructorArguments;
				var kind = attribute.AttributeType.Name.Substring (0, attribute.AttributeType.Name.Length - 9 /* len(Attribute) */);
				var message = String.IsNullOrEmpty ((string) args.Last ().Value) ? "" : $", message: \"{(string) args.Last ().Value}\"";
				var platform = GetPlatformName ((byte) args [0].Value);
				if (platform == null) {
					continue;
				}
				switch (args.Count) {
				case 3:
					availability.Add (new HarvestedAvailabilityInfo (kind, $"{platform}, {args [1].Value}{message}"));
					break;
				case 4:
					availability.Add (new HarvestedAvailabilityInfo (kind, $"{platform}, {args [1].Value}, {args [2].Value}{message}]"));
					break;
				case 5: {
					string minor = GetMinorValue (args [3].Value) != 255 ? $", {args [3].Value}" : "";
					availability.Add (new HarvestedAvailabilityInfo (kind, $"{platform}, {args [1].Value}, {args [2].Value}{minor}{message}"));
					break;
				}
				case 6: {
					string minor = GetMinorValue (args [3].Value) != 255 ? $", {args [3].Value}" : "";
					availability.Add (new HarvestedAvailabilityInfo (kind, $"{platform}, {platform}, {args [1].Value}, {args [2].Value}{minor}, {args [4].Value}{message}]"));
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