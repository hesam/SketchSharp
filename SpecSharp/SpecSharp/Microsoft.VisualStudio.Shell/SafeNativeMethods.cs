//------------------------------------------------------------------------------
/// <copyright file="SafeNativeMethods.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

namespace Microsoft.VisualStudio {
    using System.Runtime.InteropServices;
    using System;
    using System.Security.Permissions;
    using System.Collections;
    using System.IO;
    using System.Text;

    [
    System.Security.SuppressUnmanagedCodeSecurityAttribute()
    ]
    internal class SafeNativeMethods {

        [DllImport(ExternDll.User32, ExactSpelling=true, CharSet=CharSet.Auto)]
        internal static extern bool InvalidateRect(IntPtr hWnd, ref NativeMethods.RECT rect, bool erase);

        [DllImport(ExternDll.User32, ExactSpelling=true, CharSet=CharSet.Auto)]
        internal static extern bool InvalidateRect(IntPtr hWnd, [MarshalAs(UnmanagedType.Interface)] object rect, bool erase);

        [DllImport(ExternDll.User32, ExactSpelling=true, CharSet=CharSet.Auto)]
        internal extern static bool IsChild(IntPtr parent, IntPtr child);

        [DllImport(ExternDll.User32, ExactSpelling=true, CharSet=System.Runtime.InteropServices.CharSet.Auto)]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport(ExternDll.Kernel32, ExactSpelling=true, CharSet=System.Runtime.InteropServices.CharSet.Auto)]
        internal static extern int GetCurrentThreadId();

        [DllImport(ExternDll.User32, ExactSpelling=true, CharSet=CharSet.Auto)]
        internal static extern int MapWindowPoints(IntPtr hWndFrom, IntPtr hWndTo, [In, Out] ref NativeMethods.RECT rect, int cPoints);

        [DllImport(ExternDll.User32, ExactSpelling=true, CharSet=CharSet.Auto)]
        internal static extern int MapWindowPoints(IntPtr hWndFrom, IntPtr hWndTo, [In, Out] NativeMethods.POINT pt, int cPoints);

        [DllImport(ExternDll.User32, CharSet=System.Runtime.InteropServices.CharSet.Auto)]
        internal static extern int RegisterWindowMessage(string msg);

        [DllImport(ExternDll.User32, ExactSpelling=true, CharSet=CharSet.Auto)]
        internal static extern bool GetWindowRect(IntPtr hWnd, [In, Out] ref NativeMethods.RECT rect);
    }
}
