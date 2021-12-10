using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace mellite.tests {
	public class DefineParserTests {
		void ParseAndExpect (string text, List<string> defines, bool expectConflict = false)
		{
			var found = (new DefineParser ()).ParseAllDefines (text);
			Assert.Equal (defines, found);
			var uniqueDefines = ((new DefineParser ()).FindUniqueDefinesThatCoverAll (text));
			Assert.Equal (expectConflict, uniqueDefines == null);
		}

		[Fact]
		public void NoDefinesInFile ()
		{
			ParseAndExpect (@"namespace Accessibility {
	public static partial class AXHearingUtilities {
		[return: MarshalAs (UnmanagedType.I1)] // This is a comment
		public static extern bool SupportsBidirectionalStreaming ();
	}
", new List<string> ());
		}

		[Fact]
		public void MultipleDefinesInFile ()
		{
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
", new List<string> () { "IOS" });

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
", new List<string> () { "MONOMAC", "IOS" });
		}

		[Fact]
		public void ElseInFile ()
		{
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
", new List<string> () { "!MONOMAC" });
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
", new List<string> () { "!NET", "MONOMAC" });

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
", new List<string> () { "WATCH", "MONOMAC" });
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
", new List<string> () { "!NET", "MONOMAC", "IOS" });
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
}", new List<string> () { "MONOMAC", "WATCH", "!MONOMAC" }, expectConflict: true);
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
}", new List<string> () { "!XAMCORE_4_0" });
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
}", new List<string> () { "!__IOS__", "!NET" });
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

