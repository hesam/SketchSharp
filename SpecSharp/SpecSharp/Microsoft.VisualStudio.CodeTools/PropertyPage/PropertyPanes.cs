//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.VisualStudio.OLE.Interop;

namespace Microsoft.VisualStudio.CodeTools
{
  internal class PropertyPanes 
  {
    #region Construction
    public PropertyPanes()
    {
      RegisterProperyPanesFromRegistry();
    }

    ~PropertyPanes()
    {
      Release();
    }

    public void Release()
    {
      UnregisterPropertyPanes();
    }
    #endregion

    #region PropertyPage class registration
    // one cookie per registered COM class
    private List<uint> cookies = new List<uint>(1);

    private int RegisterPropertyPane(Guid pageid, Guid clsid)
    {
      uint cookie = 0;
      int hr = NativeMethods.CoRegisterClassObject(ref pageid, new PropertyPageFactory(clsid)
                                            , NativeMethods.CLSCTX_INPROC_SERVER
                                            , NativeMethods.REGCLS_MULTIPLEUSE, out cookie);
      if (hr == 0 && cookie != 0) {
        cookies.Add(cookie);
      }
      return hr;
    }

    private void UnregisterPropertyPanes()
    {
      foreach (uint cookie in cookies) {
        NativeMethods.CoRevokeClassObject(cookie);
      }
      cookies.Clear();
    }

    private void RegisterProperyPanesFromRegistry()
    {
      // Open the "CodeTools" key
      string vsRoot = Common.GetLocalRegistryRoot();
      RegistryKey root = Registry.LocalMachine.OpenSubKey(vsRoot + "\\CodeTools", false);
      if (root != null) {
        String[] toolKeys = root.GetSubKeyNames();
        foreach (string toolName in toolKeys) {
          RegistryKey toolKey = root.OpenSubKey(toolName, false);
          if (toolKey != null) {
            RegistryKey propPagesKey = toolKey.OpenSubKey("PropertyPages", false);
            if (propPagesKey != null) {
              foreach (string propPage in propPagesKey.GetSubKeyNames()) {
                Guid propPageId = new Guid(propPage);
                if (propPageId != Guid.Empty) {
                  RegistryKey propPageKey = propPagesKey.OpenSubKey(propPage, false);
                  if (propPageKey != null) {
                    string clsid = propPageKey.GetValue("clsid") as String;
                    if (clsid != null) {
                      Guid propPaneClsid = new Guid(clsid);
                      if (propPaneClsid != Guid.Empty) {
                        RegisterPropertyPane(propPageId, propPaneClsid);
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
    }

    #endregion
  }

  internal class PropertyPageFactory : NativeMethods.IClassFactory
  {
    private Guid clsid; // The clsid of an IPropertyPagePane object

    public PropertyPageFactory(Guid clsid)
    {
      this.clsid = clsid;
    }

    public int CreateInstance(IntPtr unkOuter, ref Guid iid, out IntPtr obj)
    {
      obj = IntPtr.Zero;
      if (unkOuter != IntPtr.Zero) {
        Marshal.ThrowExceptionForHR(NativeMethods.CLASS_E_NOAGGREGATION);
      }
      else if (iid == NativeMethods.iidIUnknown || iid == typeof(IPropertyPage).GUID) {
        // Create the PropertyPagePane from the clsid
        IntPtr paneUnknown;
        int hr = NativeMethods.CoCreateInstance(ref clsid, unkOuter, NativeMethods.CLSCTX_INPROC_SERVER, ref iid, out paneUnknown);
        if (hr != 0)
          Marshal.ThrowExceptionForHR(hr);
        else {
          // Marshal it into a C# object
          IPropertyPane pane = Marshal.GetTypedObjectForIUnknown(paneUnknown, typeof(IPropertyPane)) as IPropertyPane;
          if (pane == null) {
            Marshal.ThrowExceptionForHR(NativeMethods.E_NOINTERFACE);
          }
          else {
            // Create a property page from the property page pane.
            IPropertyPage page = new PropertyPage(pane);
            // And marshall the property page back to an IPropertyPage COM interface
            obj = Marshal.GetComInterfaceForObject(page, typeof(IPropertyPage));
          }
        }
      }
      else {
        Marshal.ThrowExceptionForHR(NativeMethods.E_NOINTERFACE);
      }

      return 0;
    }
  
    public int LockServer(bool lockIt)
    {
      return 0;
    }
  }

  static internal class NativeMethods
  {
    // constants
    public const uint CLSCTX_INPROC_SERVER = 1;
    public const uint REGCLS_MULTIPLEUSE = 1;
    public const int CLASS_E_NOAGGREGATION = unchecked((int)0x80040110);
    public const int E_NOINTERFACE = unchecked((int)0x80004002);
    public const int E_FAIL = unchecked((int)0x80004005);
    public static Guid iidIUnknown = new Guid("00000000-0000-0000-C000-000000000046");

    
    [ ComImport()
    , InterfaceType(ComInterfaceType.InterfaceIsIUnknown)
    , Guid("00000001-0000-0000-C000-000000000046")]
    internal interface IClassFactory
    {
      [PreserveSig]
      int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);
      [PreserveSig]
      int LockServer(bool fLock);
    }

    [DllImport("ole32.dll")]
    public static extern Int32 CoCreateInstance(ref Guid clsid
                                               , IntPtr unkOuter, UInt32 clsContext
                                               , ref Guid iid, out IntPtr obj);
    [DllImport("ole32.dll")]
    public static extern Int32 CoRegisterClassObject( ref Guid clsid
                                                    , [MarshalAs(UnmanagedType.Interface)]IClassFactory classFactory
                                                    , UInt32 context, UInt32 flags
                                                    , out UInt32 cookie);
    [DllImport("ole32.dll")]
    public static extern Int32 CoRevokeClassObject(UInt32 cookie);
  }
}