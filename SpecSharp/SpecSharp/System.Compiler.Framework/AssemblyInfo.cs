//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

#if CCINamespace
[assembly: AssemblyTitle("Microsoft.Cci.Framework")]
[assembly: AssemblyDescription("Contains a collection of standard compiler base classes as well as visitors for the standard node types defined in Microsoft.Cci")]
#else
[assembly: AssemblyTitle("System.Compiler.Framework")]
[assembly: AssemblyDescription("Contains a collection of standard compiler base classes as well as visitors for the standard node types defined in System.Compiler")]
#endif
[assembly: AssemblyCompany("Microsoft Corporation")]
[assembly: AssemblyProduct("Microsoft (R) .NET Framework")]
[assembly: AssemblyCopyright("Copyright (C) Microsoft Corp. 2002, 2003, 2004. All rights reserved")]
[assembly: AssemblyTrademark("Microsoft and Windows are either registered trademarks or trademarks of Microsoft Corporation in the U.S. and/or other countries")]
#if DelaySign
[assembly: AssemblyDelaySign(true)]
[assembly: AssemblyKeyFile("..\\..\\..\\Common\\FinalPublicKey.snk")]
#else
[assembly: AssemblyKeyFile("..\\..\\..\\Common\\InterimKey.snk")]
#endif
[assembly: System.Runtime.InteropServices.ComVisible(false)]
[assembly: CLSCompliant(false)]

