//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using System.Runtime.InteropServices;
using System.Collections;
using System.IO;
using Microsoft.Win32;

using IServiceProvider = System.IServiceProvider;

namespace Microsoft.VisualStudio.Package{

  /// <summary>
  /// You must inherit from this class and simply add a [ComVisible] and 
  /// [GuidAttribute] and then specify the EditorFactoryGuid, EditorFactoryGuid 
  /// and EditorName variables in your Registration class.
  /// This base class provides a default editor factory implementation
  /// that hosts the Visual Studio Core editor.  
  /// </summary>
  public class EditorFactory : IVsEditorFactory{
    protected Microsoft.VisualStudio.Shell.Package package;
    protected IServiceProvider site;
    Hashtable extensions; // registered

    public EditorFactory(Microsoft.VisualStudio.Shell.Package package){
      this.package = package; 
    }

    public virtual bool IsRegisteredExtension(string ext){
      return extensions.Contains(ext);
    }
    public virtual bool CheckAllFileTypes(){
      return extensions.Contains("*");
    }

    // override this method if you registered the file extension "*".
    public virtual Guid GetLanguageSID(){
      return Guid.Empty;
    }

    public virtual bool IsOurKindOfFile(string moniker){
      return true;
    }

    /// <summary>
    /// Here we simply host the visual studio core text editor.
    /// </summary>
    public virtual int CreateEditorInstance(uint createDocFlags, string moniker, 
      string physicalView, IVsHierarchy pHier, uint itemid, IntPtr existingDocData,
      out IntPtr docView, out IntPtr docData, out string editorCaption,
      out Guid cmdUI, out int cancelled){

      cancelled = 0;
      cmdUI = Guid.Empty;
      docData = IntPtr.Zero;
      docView = IntPtr.Zero;
      editorCaption = null;
      string ext = Path.GetExtension(moniker).ToLower();
      if (ext.StartsWith(".")){
        ext = ext.Substring(1);
      }
      bool takeover = CheckAllFileTypes() && !this.IsRegisteredExtension(ext);

      if (takeover && ! IsOurKindOfFile(moniker)){
        return (int)VsConstants.VS_E_UNSUPPORTEDFORMAT;
      }

      IVsTextLines buffer = null;
      if (existingDocData != IntPtr.Zero){
        buffer = (IVsTextLines)Marshal.GetTypedObjectForIUnknown(existingDocData, typeof(IVsTextLines));
      }else{      
        buffer = (IVsTextLines)VsShell.CreateInstance(this.site, ref VsConstants.CLSID_VsTextBuffer, ref VsConstants.IID_IVsTextLines, typeof(IVsTextLines));
      }
      
      IObjectWithSite objWithSite = (IObjectWithSite)buffer;
      objWithSite.SetSite(this.site);

      object window = CreateEditorView(moniker, buffer, physicalView, out editorCaption, out cmdUI);

      if (takeover){
        Guid langSid = GetLanguageSID();
        if (langSid != Guid.Empty){
          buffer.SetLanguageServiceID(ref langSid);
          IVsUserData vud = (IVsUserData)buffer;
          vud.SetData(ref VsConstants.GUID_VsBufferDetectLangSID, false);
        }
        // todo: for some reason my commands are disabled when we go through this
        // code path...
      }

      docView = Marshal.GetIUnknownForObject(window);
      docData = Marshal.GetIUnknownForObject(buffer);

      // VS core editor is the primary command handler
      cancelled = 0;
      return 0;
    } 

    public virtual object CreateEditorView(string moniker, IVsTextLines buffer, string physicalView, out string editorCaption, out Guid cmdUI){
      IVsCodeWindow window = (IVsCodeWindow)VsShell.CreateInstance(this.site, ref VsConstants.CLSID_VsCodeWindow, ref VsConstants.IID_IVsCodeWindow, typeof(IVsCodeWindow));
      window.SetBuffer(buffer);
      window.SetBaseEditorCaption(null);
      window.GetEditorCaption(READONLYSTATUS.ROSTATUS_Unknown, out editorCaption);

      Guid CMDUIGUID_TextEditor = new Guid( 0x8B382828, 0x6202, 0x11d1, 0x88, 0x70, 0x00, 0x00, 0xF8, 0x75, 0x79, 0xD2 );
      cmdUI = CMDUIGUID_TextEditor;
      return window;
    }

    public virtual int MapLogicalView(ref Guid logicalView, out string physicalView){
      physicalView = null;
      if (logicalView == VsConstants.LOGVIEWID_Designer)
        physicalView = "Form";
      return 0;
    }

    public virtual int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp){
      this.site = new ServiceProvider(psp); 

      extensions = new Hashtable();
      ILocalRegistry3 localRegistry = (ILocalRegistry3)site.GetService(typeof(SLocalRegistry));
      string root = null;        
      if (localRegistry != null){
        localRegistry.GetLocalRegistryRoot(out root);
      }
      using (RegistryKey rootKey = Registry.LocalMachine.OpenSubKey(root)){
        if (rootKey != null){
          string relpath = "Editors\\{"+this.GetType().GUID.ToString()+"}\\Extensions";
          using (RegistryKey key = rootKey.OpenSubKey(relpath,false)){
            if (key != null){
              foreach (string ext in key.GetValueNames()){
                extensions.Add(ext.ToLower(),ext);
              }           
            }
          }
        }
      }
      return 0;
    } 
        
    public virtual int Close(){      
      this.site = null;
      this.package = null;
      GC.Collect();
      return 0;
    }
  }
}
