using System;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: AssemblyTitle("Microsoft.VisualStudio.Package.LanguageService")]
[assembly: AssemblyDescription("Contains abstract base classes implementing VSIP Language Service interfaces via overridable boiler plate code")]
[assembly: AssemblyCompany("Microsoft Corporation")]
[assembly: AssemblyProduct("Microsoft Development Environment")]
[assembly: AssemblyCopyright("Copyright (C) Microsoft Corp. 2003. All rights reserved")]
[assembly: AssemblyTrademark("Microsoft and Windows are either registered trademarks or trademarks of Microsoft Corporation in the U.S. and/or other countries")]
#if DelaySign
[assembly: AssemblyDelaySign(true)]
[assembly: AssemblyKeyFile("..\\..\\..\\Common\\FinalPublicKey.snk")]
#else
[assembly: AssemblyKeyFile("..\\..\\..\\Common\\InterimKey.snk")]
#endif
[assembly: System.Runtime.InteropServices.ComVisible(true)]
[assembly: CLSCompliant(false)]
