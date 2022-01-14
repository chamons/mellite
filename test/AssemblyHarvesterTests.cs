using System;

using Xunit;

namespace mellite.tests {
	public class AssemblyHarvesterTests {

		[Fact]
		public void SmokeTest ()
		{
			const string path = "/Library/Frameworks/Xamarin.iOS.framework/Versions/Current/lib/mono/Xamarin.iOS/Xamarin.iOS.dll";
			var info = (new AssemblyHarvester ()).Harvest (path);
			Assert.Equal (2, info.Data ["Speech.SFSpeechRecognitionTaskHint"].Count);
		}

		[Fact]
		public void ABPersonCorrectDeprecation ()
		{
			const string path = "/Library/Frameworks/Xamarin.iOS.framework/Versions/Current/lib/mono/Xamarin.iOS/Xamarin.iOS.dll";
			var info = (new AssemblyHarvester ()).Harvest (path);
			var attrib = info.Data ["AddressBook.ABPerson"] [0].Attribute;
			Assert.Equal (@"Deprecated(PlatformName.iOS, 9, 0, 0, message: ""Use the 'Contacts' API instead."")", attrib.ToFullString ());
		}
	}
}

