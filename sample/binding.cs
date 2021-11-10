using System;
using ObjCRuntime;

namespace binding {
	[Introduced (PlatformName.iOS, 10, 0)]
	[Introduced (PlatformName.MacOSX, 10, 0)]
	public partial class Class1 {
		[Deprecated (PlatformName.iOS, 13, 0)]
		[Obsolete (PlatformName.MacOSX, 14, 0)]
		[Unavailable (PlatformName.MacCatalyst)]
		public void Foo () { }

		[Introduced (PlatformName.iOS, 11, 0)]
		[Introduced (PlatformName.MacOSX, 11, 0)]
		public void Bar () { }

		[Introduced (PlatformName.iOS, 12, 0)]
		[NoMac]
		public partial void Buzz () { }

		[NoiOS]
		public int One { get; set; }
	}

	[Introduced (PlatformName.TvOS, 10, 0)]
	public partial class Class1 {
		[Introduced (PlatformName.TvOS, 12, 0)]
		[NoMac]
		public partial void Buzz ();
	}
}
