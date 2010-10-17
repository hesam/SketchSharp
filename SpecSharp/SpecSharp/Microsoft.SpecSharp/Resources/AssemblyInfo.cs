//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
// This is a pure resource DLL only. It cannot be a C# project because such projects cannot attach Win32 resources to their output.
using System.Reflection;

[assembly: AssemblyTitle("Microsoft.SpecSharp.Resources")]
[assembly: AssemblyDescription("Sattelite Contains Win32 resources for Microsoft.SpecSharp")]
[assembly: AssemblyCompany("Microsoft Corporation")]
[assembly: AssemblyProduct("Microsoft (R) .NET Framework")]
[assembly: AssemblyCopyright("Copyright (C) Microsoft Corp. 2001. All rights reserved")]
[assembly: AssemblyTrademark("Microsoft and Windows are either registered trademarks or trademarks of Microsoft Corporation in the U.S. and/or other countries")]
#if DelaySign
[assembly: AssemblyDelaySign(true)]
[assembly: AssemblyKeyFile("..\\..\\Common\\FinalPublicKey.snk")]
#else
[assembly: AssemblyKeyFile("..\\..\\Common\\InterimKey.snk")]
#endif

