using System.Compiler;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics; 
using System.Windows.Forms;
using System.Drawing;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Designer.Interfaces;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.Win32;
using System.Text;
using System.Collections;
using System.Reflection;

namespace Microsoft.VisualStudio.Package{

  /// <summary>
  /// 
  /// 
  /// Subclass this and simply add the [ComVisible] and [GuidAttribute] and then 
  /// specify the EditorFactoryGuid and EditorName properties 
  /// in your Registration class.
  /// 
  /// Register your class in the HKLM/Microsoft/VisualStudio/7.1/Editors, specifying
  /// the file extensions for your editor.  See InstallEditor.rgs.
  /// 
  /// The other thing you can do here is provide custom property pages for your
  /// editor which show up in the properties window by overriding GetPropertyPage.
  /// </summary>
  public abstract class VsPackage : IVsPackage, IVsInstalledProduct,
    Microsoft.VisualStudio.OLE.Interop.IServiceProvider,
    Microsoft.VisualStudio.OLE.Interop.IOleComponent {
    
    uint projectFactoryCookie;
    uint editorFactoryCookie;
    public ServiceProvider site;
    protected EditorFactory editorFactory;
    protected ProjectFactory projectFactory;
    protected Hashtable languageServices; // key is the Guid.
    protected IOleComponentManager componentManager;
    private uint componentID;
    bool entered;

    public VsPackage() {
      CCITracing.AddTraceLog("c:\\ccitrace.log"); 
    }

    /// <summary>
    /// Override this method if you want to return a custom editor factory.
    /// </summary>
    public virtual EditorFactory CreateEditorFactory(){
      return null;
    }

    /// <summary>
    /// Override this method and return a non-null ProjectFactory if you
    /// have your own project types.
    /// </summary>
    public virtual ProjectFactory CreateProjectFactory() {
      return null;
    }
   
    /// <summary>
    /// This method constructs a language service given the guid found in the
    /// registry associated with this package.  The default implementation assumes
    /// the guid is registered and uses Type.GetTypeFromCLSID to construct the
    /// language service.  You can override this method if you want to construct
    /// the language service directly without having to register it as a COM object.
    /// </summary>
    /// <returns></returns>
    public virtual ILanguageService CreateLanguageService(ref Guid guid) {
      Type type = Type.GetTypeFromCLSID(guid, true);
      if (type != null) {
        System.Reflection.ConstructorInfo ci = type.GetConstructor(new Type[0]);
        if (ci != null) {
          ILanguageService svc = (ILanguageService)ci.Invoke(new object[0]);
          return svc;
        }
      }
      return null;
    }

    #region IVsPackage methods
    public virtual void SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider site){

      this.site = new ServiceProvider(site);
      this.editorFactory = CreateEditorFactory();
      if (this.editorFactory != null) {
        this.editorFactory.SetSite(site);
        Guid editorGuid = this.editorFactory.GetType().GUID;
        IVsRegisterEditors vre = (IVsRegisterEditors)this.site.QueryService( VsConstants.SID_SVsRegisterEditors, typeof(IVsRegisterEditors));
        vre.RegisterEditor(ref editorGuid, editorFactory, out this.editorFactoryCookie);
      }

      this.projectFactory = CreateProjectFactory();
      if (this.projectFactory != null) {
        this.projectFactory.SetSite(site);
        IVsRegisterProjectTypes rpt = (IVsRegisterProjectTypes)this.site.QueryService(VsConstants.SID_IVsRegisterProjectTypes, typeof(IVsRegisterProjectTypes));
        if (rpt != null) {
          Guid projectType = this.projectFactory.GetType().GUID;
          rpt.RegisterProjectType(ref projectType, this.projectFactory, out this.projectFactoryCookie);          
        }
      }

      uint lcid = VsShell.GetProviderLocale(this.site);
      
      languageServices = new Hashtable();
      string thisPackage = "{"+this.GetType().GUID.ToString() + "}";
      ServiceProvider thisSite = new ServiceProvider((Microsoft.VisualStudio.OLE.Interop.IServiceProvider)this);

      ILocalRegistry3 localRegistry = (ILocalRegistry3)this.site.QueryService( VsConstants.SID_SLocalRegistry, typeof(ILocalRegistry3));
      string root = null;        
      if (localRegistry != null) {
        localRegistry.GetLocalRegistryRoot(out root);
      }      
      using (RegistryKey rootKey = Registry.LocalMachine.OpenSubKey(root)) {
        if (rootKey != null) {
          using (RegistryKey languages = rootKey.OpenSubKey("Languages\\Language Services")) {
            if (languages != null) {
              foreach (string languageName in languages.GetSubKeyNames()) {
                using (RegistryKey langKey = languages.OpenSubKey(languageName)) {
                  object pkg = langKey.GetValue("Package");
                  if (pkg is string && string.Compare((string)pkg, thisPackage, false) == 0) {
                    object guid = langKey.GetValue(null);
                    if (guid is string) {
                      Guid langGuid = new Guid((string)guid);
                      if (!this.languageServices.Contains(langGuid.ToString())){
                        ILanguageService svc = CreateLanguageService(ref langGuid);
                        if (svc != null) {
                          svc.Init(thisSite, ref langGuid, lcid, GetFileExtensions(rootKey, (string)guid));
                          this.languageServices.Add(langGuid.ToString(), svc);
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

      //register with ComponentManager for Idle processing
      this.componentManager = (IOleComponentManager)this.site.QueryService(VsConstants.SID_SOleComponentManager, typeof(IOleComponentManager));
      if (componentID == 0)
      {
          OLECRINFO[]   crinfo = new OLECRINFO[1];
          crinfo[0].cbSize   = (uint)Marshal.SizeOf(typeof(OLECRINFO));
          crinfo[0].grfcrf   = (uint)OLECRF.olecrfNeedIdleTime |
                               (uint)OLECRF.olecrfNeedPeriodicIdleTime; 
          crinfo[0].grfcadvf = (uint)OLECADVF.olecadvfModal |
                               (uint)OLECADVF.olecadvfRedrawOff |
                               (uint)OLECADVF.olecadvfWarningsOff;
          crinfo[0].uIdleTimeInterval = 1000;
          this.componentManager.FRegisterComponent(this, crinfo, out componentID);
      }

    }

    public string GetFileExtensions(RegistryKey languages, string languageGuid) {

      StringBuilder sb = new StringBuilder();
      using (RegistryKey key = languages.OpenSubKey("Languages\\File Extensions",false)) {
        if (key != null) {
          foreach (string ext in key.GetSubKeyNames()) {
            using (RegistryKey exKey = key.OpenSubKey(ext)) {
              object guid = exKey.GetValue(null);
              if (guid is String && string.Compare((string)guid, languageGuid, true) == 0) {
                string ex = ext.Trim();
                if (sb.Length>0) sb.Append(";");
                sb.Append(ex);
              }
            }
          }           
        }
      }
      return sb.ToString();
    }

    public virtual void Close()	{
      if (this.site != null) {
        if (this.editorFactory != null) {
          Guid editorGuid = this.editorFactory.GetType().GUID;
          IVsRegisterEditors vre = (IVsRegisterEditors)site.QueryService( VsConstants.SID_SVsRegisterEditors, typeof(IVsRegisterEditors));
          vre.UnregisterEditor(this.editorFactoryCookie);
          this.editorFactory.Close();
          this.editorFactory = null;
        }
        if (this.projectFactory != null) {
          IVsRegisterProjectTypes rpt = (IVsRegisterProjectTypes)this.site.QueryService(VsConstants.SID_IVsRegisterProjectTypes, typeof(IVsRegisterProjectTypes));
          if (rpt != null) {
            rpt.UnregisterProjectType(this.projectFactoryCookie);          
          }
          this.projectFactoryCookie = 0;
          this.projectFactory.Close();
          this.projectFactory = null;
        }

      }
      foreach (ILanguageService svc in this.languageServices.Values) {
        svc.Done();
      }
      this.languageServices.Clear();
      
      if (this.componentID != 0) {
        this.componentManager.FRevokeComponent(this.componentID);
        this.componentID = 0;
      }
      this.componentManager = null;

      if (site != null) site.Dispose();
      this.site = null;
      GC.Collect();
    }

    public virtual void ResetDefaults(uint flags){
      throw new NotImplementedException();
    }

    public virtual void QueryClose(out int canClose) {
      canClose = 1;
    }

    public void GetPropertyPage(ref Guid guidPage, Microsoft.VisualStudio.Shell.Interop.VSPROPSHEETPAGE[] ppage){      
      PropertySheet sheet = GetPropertySheet(guidPage);
      if (sheet != null && ppage != null ) {
        ppage[0] = new VSPROPSHEETPAGE();
        ppage[0].dwSize = (uint)Marshal.SizeOf(typeof(VSPROPSHEETPAGE));        
        ppage[0].hwndDlg = sheet.Handle;        
        ppage[0].pfnDlgProc = NativeWindowHelper.GetNativeWndProc(sheet); 
        return;
      }
      throw new NotImplementedException();
    }

    /// <summary>
    /// Override this method if you want to return a custom property sheet
    /// for your package. This will show up under Tools/Options/Text Editor/...
    /// </summary>
    public virtual PropertySheet GetPropertySheet(Guid guidPage) {
      return null;
    }

    public virtual void GetAutomationObject(string propName, out object obj) {
      throw new NotImplementedException();
    }

    public virtual void CreateTool(ref Guid persistenceSlot){
      throw new NotImplementedException();
    }
    #endregion 


    #region IVsInstalledProduct methods
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////
    //
    //  this interface implements splashscreen/aboutbox. Due to resource issues in the current build (pre April 
    //    release), only text in about works
    //                                                  
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Default splash screen bitmap resource id is 101.  Override this to return a different resource id.
    /// </summary>
    /// <param name="IdBmp"></param>
    public virtual void IdBmpSplash(out uint IdBmp) {
      IdBmp = 101;
    }

    /// <summary>
    /// Default icon id for about box is 100.  Override this to return different icon id.
    /// </summary>
    /// <param name="IdIcoLogo"></param>
    public virtual void IdIcoLogoForAboutbox(out uint IdIcoLogo) {
      IdIcoLogo = 100;      
    }

    /// <summary>
    /// Returns the contents of the AssemblyTitleAttribute 
    /// </summary>
    public virtual void OfficialName(out string strOfficialName) {
      strOfficialName = GetAssemblyAttribute("System.Reflection.AssemblyTitleAttribute");
    }

    /// <summary>
    /// Returns the contents of the AssemblyDescriptionAttribute
    /// </summary>
    public virtual void ProductDetails(out string strProductDetails) {
      strProductDetails = GetAssemblyAttribute("System.Reflection.AssemblyDescriptionAttribute") + ", Version " + GetAssemblyAttribute("System.Reflection.AssemblyVersionAttribute");
    }
    
    /// <summary>
    /// Returns the contents of the AssemblyVersionAttribute
    /// </summary>
    public virtual void ProductID(out string strProductID) {
      strProductID = GetAssemblyAttribute("System.Reflection.AssemblyVersionAttribute");
    }

    string GetAssemblyAttribute(string strAttributeName) {
      Type thisType = this.GetType();
      Assembly thisAssembly  = Assembly.GetAssembly(thisType);
      object[] oAttributes = thisAssembly.GetCustomAttributes(false);
      string strReturn = "";

      string strFullName = thisAssembly.FullName; 
      string [] astrVersion = strFullName.Split(new Char[] {'='}); 

      if (strAttributeName == "System.Reflection.AssemblyVersionAttribute") {
        if (astrVersion.Length > 1) {
          strReturn = astrVersion[1];
          astrVersion = strReturn.Split(new Char[]  {',' });
          if (astrVersion.Length > 1) {
            strReturn = astrVersion[0];
          }
        }
      }

      for (int i=0; i < oAttributes.Length; i++) {
        if (oAttributes[i].GetType().ToString() == strAttributeName) {
          switch (strAttributeName) {
            case "System.Reflection.AssemblyTitleAttribute":
              AssemblyTitleAttribute title = (AssemblyTitleAttribute) oAttributes[i];
              strReturn = title.Title.ToString();
              break;
            case "System.Reflection.AssemblyDescriptionAttribute":
              AssemblyDescriptionAttribute desc = (AssemblyDescriptionAttribute) oAttributes[i];
              strReturn = desc.Description.ToString();
              break;

          }
          break;
        }
      }
      return strReturn;
    }
    #endregion

    #region IServiceProvider methods
    public virtual int QueryService( ref Guid sid, ref Guid iid, out IntPtr obj ) {
      obj = IntPtr.Zero;
      int result = (int)HResult.E_NOINTERFACE;
      if (entered) return result; // guard against infinite recurrsion.
      entered = true;
      try {
        string key = sid.ToString();
        if (this.languageServices.Contains(key)) {
          ILanguageService languageService = (ILanguageService)this.languageServices[key];
          IntPtr pUnk = Marshal.GetIUnknownForObject(languageService);
          Marshal.QueryInterface(pUnk, ref iid, out obj);
          Marshal.Release(pUnk);
          if (obj != IntPtr.Zero)
            result = 0;
        }
        if (result != 0) {
          result = this.site.Unwrap().QueryService(ref sid, ref iid, out obj);
        }
      } catch (Exception) {
        result = (int)HResult.E_NOINTERFACE;
      }
      entered = false;
      return result;
    }
    #endregion

    #region IOleComponent methods
    public virtual int FDoIdle( uint grfidlef ) 
    {
      bool  periodic = ((grfidlef & (uint)_OLEIDLEF.oleidlefPeriodic) != 0);
      foreach (ILanguageService svc in this.languageServices.Values) {
        svc.OnIdle(periodic);
      }
      return 0; 
    }

    public virtual void Terminate() {
      CCITracing.Trace(this.GetType().FullName + ": terminated.");
    }
    public virtual int FPreTranslateMessage(MSG[] msg) {
      return 0;
    }
  
    public virtual void OnEnterState(uint uStateID, int fEnter) {
    }

    public virtual void OnAppActivate(int fActive, uint dwOtherThreadID) {
    }

    public virtual void OnLoseActivation()                                 {
    }

    public virtual void OnActivationChange(Microsoft.VisualStudio.OLE.Interop.IOleComponent pic, int fSameComponent, OLECRINFO[] pcrinfo, int fHostIsActivating, OLECHOSTINFO[] pchostinfo, uint dwReserved) {
    }

    public virtual int FContinueMessageLoop(uint uReason, IntPtr pvLoopData, MSG[] pMsgPeeked)   { 
      return 1; 
    }

    public virtual int FQueryTerminate(int fPromptUser)                  { 
      return 1; 
    }

    public virtual IntPtr HwndGetWindow(uint dwWhich, uint dwReserved)     { 
      return IntPtr.Zero; 
    }

    public virtual int FReserved1(uint reserved, uint message, IntPtr wParam, IntPtr lParam) { 
      return 1; 
    }
    #endregion
  }

}
