using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace mellite.tests {
	public class DefineParserTests {
		void ParseAndExpect (string text, List<string> expectedDefines, List<string>? expectedUniqueDefines)
		{
			var found = (new DefineParser ()).ParseAllDefines (text);
			Assert.Equal (expectedDefines, found);

			var uniqueDefines = ((new DefineParser ()).FindUniqueDefinesThatCoverAll (text, ignoreNETDefines: false));
			Assert.Equal (expectedUniqueDefines, uniqueDefines);
		}

		[Fact]
		public void NoDefinesInFile ()
		{
			ParseAndExpect (@"namespace Accessibility {
	public static partial class AXHearingUtilities {
		[return: MarshalAs (UnmanagedType.I1)] // This is a comment
		public static extern bool SupportsBidirectionalStreaming ();
	}
", new List<string> (), new List<string> ());
		}

		[Fact]
		public void MultipleDefinesInFile ()
		{
			// MONOMAC has no availability inside here
			ParseAndExpect (@"namespace Accessibility {
	public static partial class AXHearingUtilities {
#if MONOMAC
		[return: MarshalAs (UnmanagedType.I1)] // This is a comment
		public static extern bool SupportsBidirectionalStreaming ();
#endif
#if IOS
		[iOS (7, 0)]
		public static extern bool SupportsBidirectionalStreaming ();
#endif
	}
", new List<string> () { "IOS" }, new List<string> () { "IOS" });

			ParseAndExpect (@"namespace Accessibility {
	public static partial class AXHearingUtilities {
#if MONOMAC
		[Mac (10, 15)]
		public static extern bool SupportsBidirectionalStreaming ();
#endif
#if IOS
		[iOS (7, 0)]
		public static extern bool SupportsBidirectionalStreaming ();
#endif
	}
", new List<string> () { "MONOMAC", "IOS" }, new List<string> () { "MONOMAC", "IOS" });
		}

		[Fact]
		public void ElseInFile ()
		{
			// MONOMAC has no availability inside here
			ParseAndExpect (@"namespace Accessibility {
	public static partial class AXHearingUtilities {
#if MONOMAC
		[return: MarshalAs (UnmanagedType.I1)] // This is a comment
		public static extern bool SupportsBidirectionalStreaming ();
#else
		[iOS (7, 0)]
		public static extern bool SupportsBidirectionalStreaming ();
#endif
	}
", new List<string> () { "!MONOMAC" }, new List<string> ());

			ParseAndExpect (@"namespace Accessibility {
	public static partial class AXHearingUtilities {
#if MONOMAC
		[Mac (10, 15)]
		public static extern bool SupportsBidirectionalStreaming ();
#else
		[iOS (7, 0)]
		public static extern bool SupportsBidirectionalStreaming ();
#endif
	}
", new List<string> () { "MONOMAC", "!MONOMAC" }, null);
		}

		[Fact]
		public void NestedDefinesInFile ()
		{
			ParseAndExpect (@"namespace Accessibility {
	public static partial class AXHearingUtilities {
#if MONOMAC
#if !NET
		[Mac (10, 15)]
		public static extern bool SupportsBidirectionalStreaming ();
#endif
#endif
	}
", new List<string> () { "!NET", "MONOMAC" }, new List<string> () { "MONOMAC" });

			ParseAndExpect (@"namespace Accessibility {
	public static partial class AXHearingUtilities {
#if MONOMAC
		[Mac (10, 15)]
#if WATCH
		[Watch (7, 0)]
#endif
		public static extern bool SupportsBidirectionalStreaming ();
#else
		public static extern bool SupportsBidirectionalStreaming ();
#endif
	}
", new List<string> () { "WATCH", "MONOMAC" }, new List<string> () { "WATCH", "MONOMAC" });
		}

		[Fact]
		public void ComplexDefinesInFile ()
		{
			ParseAndExpect (@"namespace Accessibility {
	public static partial class AXHearingUtilities {
#if MONOMAC || IOS
#if !NET
		[Mac (10, 15)]
		public static extern bool SupportsBidirectionalStreaming ();
#endif
#endif
	}
", new List<string> () { "!NET", "MONOMAC", "IOS" }, new List<string> () { "MONOMAC", "IOS" });
		}

		[Fact]
		public void NegativeDefinesInFile ()
		{
			ParseAndExpect (@"namespace Accessibility {
	public static partial class AXHearingUtilities {
#if MONOMAC
		[Mac (10, 15)]
		public static extern bool SupportsBidirectionalStreaming ();
#else
		[iOS (7, 0)]
#if WATCH
		[Watch (7, 0)]
#endif
		public static extern bool SupportsBidirectionalStreaming ();
#endif
		}
	}
}", new List<string> () { "MONOMAC", "WATCH", "!MONOMAC" }, null);
		}


		[Fact]
		public void AppKitExample ()
		{
			ParseAndExpect (@"namespace AppKit {
	[NoMacCatalyst]
	[BaseType (typeof (NSObject))]
	[Model]
	[Protocol]
	interface NSApplicationDelegate {
#if !XAMCORE_4_0 // Needs to move from delegate in next API break
		[Obsolete (""Use the 'RegisterServicesMenu2' on NSApplication."")]
		[Export (""registerServicesMenuSendTypes:returnTypes:""), EventArgs (""NSApplicationRegister"")]
		void RegisterServicesMenu (string [] sendTypes, string [] returnTypes);
#endif
		}
}", new List<string> () { "!XAMCORE_4_0" }, new List<string> ());
		}

		[Fact]
		public void SplitConditions ()
		{
			ParseAndExpect (@"namespace AppKit {
	[BaseType (typeof (NSObject))]
	interface NSApplicationDelegate {
#if !__IOS__ && !NET
		[Obsolete (""Use the 'RegisterServicesMenu2' on NSApplication."")]
		[Export (""registerServicesMenuSendTypes:returnTypes:""), EventArgs (""NSApplicationRegister"")]
		void RegisterServicesMenu (string [] sendTypes, string [] returnTypes);
#endif
		}
}", new List<string> () { "!__IOS__", "!NET" }, new List<string> () { });
		}

		[Fact]
		public void SplitPartsTest ()
		{
			Assert.Equal (new [] { "A" }, DefineParser.SplitConditionalParts ("A"));
			Assert.Equal (new [] { "A", "B" }, DefineParser.SplitConditionalParts ("A && B"));
			Assert.Equal (new [] { "!A", "B" }, DefineParser.SplitConditionalParts ("!A && B"));
			Assert.Equal (new [] { "A", "B", "C" }, DefineParser.SplitConditionalParts ("A && B || C"));
			Assert.Equal (new [] { "A", "B", "C" }, DefineParser.SplitConditionalParts ("A && (B || C)"));
		}
	}
}

