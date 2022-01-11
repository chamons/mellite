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
	}
}

