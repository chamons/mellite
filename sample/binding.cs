using System;
using ObjCRuntime;

namespace binding {
#if NET
	[SupportedOSPlatform("ios10.0")]
	[SupportedOSPlatform("macos10.0")]
#else
	[Introduced (PlatformName.iOS, 10, 0)]
	[Introduced (PlatformName.MacOSX, 10, 0)]
#endif
	public partial class Class1 {
		public void Foo () { }

#if NET
		[SupportedOSPlatform("ios11.0")]
		[SupportedOSPlatform("macos11.0")]
		[UnsupportedOSPlatform("macos11.0")]
		[Obsolete("Starting with macos11.0 Don't use it man!", DiagnosticId = "BI1234", UrlFormat = "https://github.com/xamarin/xamarin-macios/wiki/Obsolete")]
#else
		[Introduced (PlatformName.iOS, 11, 0)]
		[Introduced (PlatformName.MacOSX, 11, 0)]
		[Deprecated (PlatformName.MacOSX, 11, 0, message: "Don't use it man!")]
#endif
		public void Bar () { }

#if NET
		[SupportedOSPlatform("ios12.0")]
#else
		[Introduced (PlatformName.iOS, 12, 0)]
#endif
		public partial void Buzz () { }
		public int One { get; set; }
	}

#if NET
	[SupportedOSPlatform("tvos10.0")]
#else
	[Introduced (PlatformName.TvOS, 10, 0)]
#endif
	public partial class Class1 {
#if NET
		[SupportedOSPlatform("tvos12.0")]
#else
		[Introduced (PlatformName.TvOS, 12, 0)]
#endif
		public partial void Buzz ();
	}
}
