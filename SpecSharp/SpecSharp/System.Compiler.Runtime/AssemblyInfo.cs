//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System.Reflection;
using System.Runtime.CompilerServices;

#if CCINamespace
[assembly: AssemblyTitle("Microsoft.Cci.Runtime")]
#else
[assembly: AssemblyTitle("System.Compiler.Runtime")]
#endif
[assembly: AssemblyDescription("Extensions to the Common Language Runtime used by the Common Compiler Infrastructure")]
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
[assembly: System.Security.AllowPartiallyTrustedCallers]
