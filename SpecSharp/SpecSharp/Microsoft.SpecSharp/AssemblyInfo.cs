//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("Microsoft.SpecSharp")]
[assembly: AssemblyDescription("Microsoft® Spec# Compiler")]
[assembly: AssemblyCompany("Microsoft Corporation")]
[assembly: AssemblyProduct("Microsoft (R) .NET Framework")]
[assembly: AssemblyCopyright("Copyright (C) Microsoft Corp. 2004. All rights reserved")]
[assembly: AssemblyTrademark("Microsoft and Windows are either registered trademarks or trademarks of Microsoft Corporation in the U.S. and/or other countries")]
#if DelaySign
[assembly: AssemblyDelaySign(true)]
[assembly: AssemblyKeyFile("..\\..\\..\\Common\\FinalPublicKey.snk")]
#else
[assembly: AssemblyKeyFile("..\\..\\..\\Common\\InterimKey.snk")]
#endif
[assembly: System.Runtime.InteropServices.ComVisible(false)]
[assembly: ClassInterface(ClassInterfaceType.None)]
