//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: AssemblyTitle("Microsoft.VisualStudio.IntegrationHelper")]
[assembly: AssemblyDescription("Implements the abstract base classes in Microsoft.VisualStudio.Package with the aid of System.Compiler.Framework")]
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
#if !WHIDBEY
[assembly: System.Runtime.InteropServices.ComVisible(false)]
#endif
[assembly: CLSCompliant(false)]
