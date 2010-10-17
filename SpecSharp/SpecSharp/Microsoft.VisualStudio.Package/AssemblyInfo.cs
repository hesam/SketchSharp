//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("Microsoft.VisualStudio.Package")]
[assembly: AssemblyDescription("Contains abstract base classes implementing VSIP interfaces via overridable boiler plate code")]
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
[assembly: ComVisible(false)]
[assembly: ClassInterface(ClassInterfaceType.None)]
[assembly: CLSCompliant(false)]
