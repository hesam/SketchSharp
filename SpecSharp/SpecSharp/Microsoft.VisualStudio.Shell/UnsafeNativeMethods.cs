//------------------------------------------------------------------------------
/// <copyright file="UnsafeNativeMethods.cs" company="Microsoft">
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
    internal class UnsafeNativeMethods {

        // APIS

        [DllImport(ExternDll.Kernel32, CharSet=CharSet.Auto, SetLastError=true)]
        internal static extern int GetFileAttributes(String name);

        [DllImport(ExternDll.Kernel32, CharSet=CharSet.Auto)]
        public static extern void GetTempFileName(string tempDirName, string prefixName, int unique, StringBuilder sb);

        [DllImport(ExternDll.User32, CharSet=CharSet.Auto)]
        internal static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport(ExternDll.User32, CharSet=CharSet.Auto)]
        internal static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        [DllImport(ExternDll.User32, CharSet=CharSet.Auto)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport(ExternDll.User32, ExactSpelling=true, CharSet=CharSet.Auto)]
        internal static extern IntPtr SetParent(IntPtr hWnd, IntPtr hWndParent);

        [DllImport(ExternDll.User32, CharSet=CharSet.Auto)]
        internal static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport(ExternDll.User32, ExactSpelling=true, CharSet=CharSet.Auto)]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
                                               int x, int y, int cx, int cy, int flags);

        [DllImport(ExternDll.User32, ExactSpelling=true, CharSet=CharSet.Auto)]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        /// IDataObject stuff
        [DllImport(ExternDll.Kernel32, ExactSpelling=true, CharSet=CharSet.Auto)]
        internal static extern IntPtr GlobalAlloc(int uFlags, int dwBytes);

        [DllImport(ExternDll.Kernel32, ExactSpelling=true, CharSet=CharSet.Auto)]
        internal static extern IntPtr GlobalReAlloc(HandleRef handle, int bytes, int flags);

        [DllImport(ExternDll.Kernel32, ExactSpelling=true, CharSet=CharSet.Auto)]
        internal static extern IntPtr GlobalLock(HandleRef handle);

        [DllImport(ExternDll.Kernel32, ExactSpelling=true, CharSet=CharSet.Auto)]
        internal static extern bool GlobalUnlock(HandleRef handle);

        [DllImport(ExternDll.Kernel32, ExactSpelling=true, CharSet=CharSet.Auto)]
        internal static extern IntPtr GlobalFree(HandleRef handle);

        [DllImport(ExternDll.Kernel32, ExactSpelling=true, CharSet=CharSet.Auto)]
        internal static extern int GlobalSize(HandleRef handle);

        [DllImport(ExternDll.Kernel32, ExactSpelling=true, EntryPoint="RtlMoveMemory", CharSet=CharSet.Unicode)]
        internal static extern void CopyMemoryW(IntPtr pdst, string psrc, int cb);

        [DllImport(ExternDll.Kernel32, ExactSpelling=true, EntryPoint="RtlMoveMemory", CharSet=CharSet.Unicode)]
        internal static extern void CopyMemoryW(IntPtr pdst, char[] psrc, int cb);

        [DllImport(ExternDll.Kernel32, ExactSpelling=true, EntryPoint="RtlMoveMemory", CharSet=CharSet.Unicode)]
        internal static extern void CopyMemoryW(StringBuilder pdst, HandleRef psrc, int cb);

        [DllImport(ExternDll.Kernel32, ExactSpelling=true, EntryPoint="RtlMoveMemory", CharSet=CharSet.Unicode)]
        internal static extern void CopyMemoryW(char[] pdst, HandleRef psrc, int cb);

        [DllImport(ExternDll.Kernel32, ExactSpelling=true, EntryPoint="RtlMoveMemory")]
        internal static extern void CopyMemory(IntPtr pdst, byte[] psrc, int cb);

        [DllImport(ExternDll.Kernel32, ExactSpelling=true, EntryPoint="RtlMoveMemory")]
        internal static extern void CopyMemory(byte[] pdst, HandleRef psrc, int cb);

        [DllImport(ExternDll.Kernel32, ExactSpelling=true, EntryPoint="RtlMoveMemory")]
        internal static extern void CopyMemory(IntPtr pdst, HandleRef psrc, int cb);

        [DllImport(ExternDll.Kernel32, ExactSpelling=true, EntryPoint="RtlMoveMemory")]
        internal static extern void CopyMemory(IntPtr pdst, string psrc, int cb);

        [DllImport(ExternDll.Kernel32, ExactSpelling=true, CharSet=CharSet.Unicode)]
        internal static extern int WideCharToMultiByte(int codePage, int flags, [MarshalAs(UnmanagedType.LPWStr)]string wideStr, int chars, [In,Out]byte[] pOutBytes, int bufferBytes, IntPtr defaultChar, IntPtr pDefaultUsed);

        // endof IDataObject stuff 

        ///////////// UNUSED

#if false
        [DllImport(ExternDll.Oleaut32, PreserveSig=false)]
        public static extern UCOMITypeLib LoadRegTypeLib(ref Guid clsid, int majorVersion, int minorVersion, int lcid);

        #endif
        
    }
}

