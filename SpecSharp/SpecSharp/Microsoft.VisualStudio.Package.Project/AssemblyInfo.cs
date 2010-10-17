
/***************************************************************************
         Copyright (c) Microsoft Corporation. All rights reserved.             
    This code sample is provided "AS IS" without warranty of any kind
    it is not recommended for use in a production environment.
***************************************************************************/

using System.Reflection;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System;
using System.Runtime.InteropServices;
using System.Net;
using System.Security.Permissions;


// Assembly permissions requirements
[assembly: SecurityPermissionAttribute(SecurityAction.RequestMinimum, Assertion = true)]
[assembly: IsolatedStorageFilePermissionAttribute(SecurityAction.RequestMinimum, Unrestricted = true)]
[assembly: UIPermissionAttribute(SecurityAction.RequestMinimum, Unrestricted = true)]
[assembly: PermissionSetAttribute(SecurityAction.RequestRefuse, Unrestricted = false)]
[assembly: FileIOPermissionAttribute(SecurityAction.RequestMinimum, Unrestricted = true)]
[assembly: ReflectionPermissionAttribute(SecurityAction.RequestMinimum, Unrestricted = true)]
[assembly: EventLogPermissionAttribute(SecurityAction.RequestMinimum, Unrestricted = true)]
[assembly: EnvironmentPermissionAttribute(SecurityAction.RequestMinimum, Unrestricted = true)]
[assembly: RegistryPermissionAttribute(SecurityAction.RequestRefuse)]
//[assembly: DirectoryServicesPermissionAttribute(SecurityAction.RequestRefuse)]
//[assembly: SocketPermissionAttribute(SecurityAction.RequestRefuse)]

// Interop attributes
[assembly: ComVisible(true)]
[assembly: CLSCompliant(true)]
