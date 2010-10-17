//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Designer.Interfaces;

using IServiceProvider = System.IServiceProvider;

namespace Microsoft.VisualStudio.Package{
	/// <summary>
	/// This class provides a WinForms way of dealing with _VSPROPSHEETPAGEs.
	/// </summary>
	public class PropertySheet : System.Windows.Forms.UserControl
	{
		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		public PropertySheet()
		{
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();

			// TODO: Add any initialization after the InitializeComponent call
      this.SetStyle(ControlStyles.EnableNotifyMessage, true);      
		}

    
		/// <summary> 
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}


    protected override void OnNotifyMessage(Message m){
      if (m.Msg == NativeWindowHelper.WM_NOTIFY && m.LParam != IntPtr.Zero) {
        NotifyMessageHeader hdr = (NotifyMessageHeader)Marshal.PtrToStructure(m.LParam, typeof(NotifyMessageHeader));
        bool rc = OnNotify(hdr);
        m.Result = new IntPtr(rc ? 1 : 0);
      }
    }

    public virtual bool OnNotify(NotifyMessageHeader hdr) {
      
      switch ((NotifyMessageCode)hdr.code) {
        case NotifyMessageCode.Apply:
          NativeWindowHelper.SetWindowLong(this.Handle, NativeWindowHelper.DWLP_MSGRESULT, NativeWindowHelper.PSNRET_NOERROR);
          OnApply();
          return true;
        case NotifyMessageCode.Reset:
          OnCancel();
          return false;
        case NotifyMessageCode.QueryCancel:
          return false;
        case NotifyMessageCode.SetActive:
          OnSetActive();
          return true;
        case NotifyMessageCode.KillActive:
          if (ValidateOptions()) {
            OnKillActive();
            NativeWindowHelper.SetWindowLong(this.Handle, NativeWindowHelper.DWLP_MSGRESULT, 0);
          } else {
            NativeWindowHelper.SetWindowLong(this.Handle, NativeWindowHelper.DWLP_MSGRESULT, 1);
          }
          return true;
      }    
      return true;
    }

    /// <summary>
    /// This is called just before OnApply, return false if there is a
    /// problem with the settings and you want to cancel the apply.
    /// </summary>
    protected virtual bool ValidateOptions() {
      return true;
    }

    /// <summary>
    /// This is notification that the options are valid, and apply is
    /// about to be called.
    /// </summary>
    protected virtual void OnKillActive() {
      // we are about to go away.
    }

    /// <summary>
    /// Save your settings.
    /// </summary>
    protected virtual void OnApply() {
    }

    /// <summary>
    /// Reset, or cancel any changes made
    /// </summary>
    protected virtual void OnCancel() {
    }

    /// <summary>
    /// The form has just loaded, populate the controls with the correct values.
    /// </summary>
    protected virtual void OnSetActive() {
    }

		#region Component Designer generated code
		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
      // 
      // PropertySheet
      // 
      this.Name = "PropertySheet";
      this.Size = new System.Drawing.Size(464, 312);

    }
		#endregion

	}

  /// <summary>
  /// LanguagePreferences encapsulates the standard General and Tab settings for a language service
  /// and provides a way of getting and setting the values.
  /// </summary>
  public class LanguagePreferences : IDisposable {
    protected IServiceProvider site;
    protected Guid langSvc;
    IVsTextManager textMgr;
    LANGPREFERENCES prefs;
    protected bool user;
    protected string editorName;

    // Our base language service perferences (from Babel originally)
    public bool       EnableCodeSense;
    public bool       EnableCodeSenseFastOnLineChange;
    public bool       EnableMatchBraces;
    public bool       EnableQuickInfo;
    public bool       EnableShowMatchingBrace;
    public bool       EnableMatchBracesAtCaret;
    public int        MaxErrorMessages;
    public int        CodeSenseDelay;
    public int        ThreadModel;
    public bool       Binary;


    /// <summary>
    /// Gets the language preferences.
    /// </summary>
    public LanguagePreferences(IServiceProvider site, Guid langSvc, string editorName) {
      this.site = site;
      this.textMgr = (IVsTextManager)site.GetService(typeof(SVsTextManager));
      this.langSvc = langSvc;
      this.user = true;
      this.editorName = editorName;
      Init();
    }

    public virtual void Init() {
      ILocalRegistry3 localRegistry = (ILocalRegistry3)site.GetService(typeof(SLocalRegistry));
      string root = null;        
      if (localRegistry != null) {
        localRegistry.GetLocalRegistryRoot(out root);
      }
      if (root != null) {
        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(root,false)) {
          if (key != null) {
            InitMachinePreferences(key, editorName);
          }
        }
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(root,false)) {
          if (key != null) {
            InitUserPreferences(key, editorName);
          }
        }
      }
      localRegistry = null;      
    }

    public virtual void InitUserPreferences(RegistryKey key, string editorName) {

    }

    public bool GetBooleanValue(RegistryKey key, string name, bool def) {
      object value = key.GetValue(name);
      if (value != null) {
        int i = (int)value;
        return (i != 0);
      }
      return def;
    }
    public int GetIntValue(RegistryKey key, string name, int def) {
      object value = key.GetValue(name);
      if (value != null) {
        return (int)value;
      }
      return def;
    }
    
    public virtual void InitMachinePreferences(RegistryKey key, string editorName) {

      using (RegistryKey keyLanguage = key.OpenSubKey("languages\\language services\\"+editorName,false)) {
        if (keyLanguage != null) { 
          this.EnableCodeSense = GetBooleanValue(keyLanguage, "CodeSense", true);
          this.EnableCodeSenseFastOnLineChange = GetBooleanValue(keyLanguage, "CodeSenseFastOnLineChange", true);
          this.EnableMatchBraces = GetBooleanValue(keyLanguage, "MatchBraces", true);
          this.EnableQuickInfo = GetBooleanValue(keyLanguage, "QuickInfo", true);
          this.EnableShowMatchingBrace = GetBooleanValue(keyLanguage, "ShowMatchingBrace", true);
          this.EnableMatchBracesAtCaret = GetBooleanValue(keyLanguage, "MatchBracesAtCaret", true);
          this.MaxErrorMessages = GetIntValue(keyLanguage, "MaxErrorMessages", 10);
          this.CodeSenseDelay = GetIntValue(keyLanguage, "CodeSenseDelay", 1000);
          this.ThreadModel = GetIntValue(keyLanguage, "ThreadModel", 1);
          this.Binary = GetBooleanValue(keyLanguage, "Binary", false);
        }
      }

    }


    public virtual void Dispose() {
      textMgr = null;
      site = null;
    }

    // General tab
    public bool AutoListMembers {
      get { this.GetLanguagePrefs(); return prefs.fAutoListMembers != 0; }
    }
    public bool HideAdvancedMembers {
      get { this.GetLanguagePrefs(); return prefs.fHideAdvancedAutoListMembers != 0; }
    }
    public bool ParameterInformation {
      get { this.GetLanguagePrefs(); return prefs.fAutoListParams != 0; }
    }
    public bool EnableVirtualSpace {
      get { this.GetLanguagePrefs(); return prefs.fVirtualSpace != 0; }
    }
    public bool WordWrap {
      get { this.GetLanguagePrefs(); return prefs.fWordWrap != 0; }
    }
    public bool LineNumbers {
      get { this.GetLanguagePrefs(); return prefs.fLineNumbers != 0; }
    }
    public bool EnableSingleClickUrlNavigation {
      get { this.GetLanguagePrefs(); return prefs.fHotURLs != 0; }
    }

    // Tabs tab
    public IndentingStyle Indenting {
      get { this.GetLanguagePrefs(); return (IndentingStyle)prefs.IndentStyle; }
    }
    public int TabSize {
      get { this.GetLanguagePrefs(); return (int)prefs.uTabSize; }
    }
    public int IndentSize {
      get { this.GetLanguagePrefs(); return (int)prefs.uIndentSize; }
    }
    public bool InsertSpaces {
      get { this.GetLanguagePrefs(); return prefs.fInsertTabs == 0; }
    }
    private void GetLanguagePrefs() {
      if (this.textMgr != null) {
        this.prefs.guidLang = langSvc;
        LANGPREFERENCES[] langPrefs = new LANGPREFERENCES[1];
        langPrefs[0] = this.prefs;
        try {
          if (! this.user) {                   
            textMgr.GetPerLanguagePreferences(langPrefs);          
          }
          else {
            textMgr.GetUserPreferences(null, null, langPrefs, null);
          }
          this.prefs = langPrefs[0];
        } catch (Exception ex) {
          Trace.WriteLine(ex.Message);
        }
      }
    }

  }
}
