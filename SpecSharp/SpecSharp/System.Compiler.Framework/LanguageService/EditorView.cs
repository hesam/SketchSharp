using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections;
using System.Compiler;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Microsoft.VisualStudio.Package{
  public interface IDesigner{
    /// <summary>
    /// Initialize provides vital information that connects you into the VS Shell.
    /// </summary>
    /// <param name="site">The IServiceProvier gives you access to all the services
    /// of the Visual Studio shell and is needed in order to register for change 
    /// events on the text buffer (See IVsTextLinesEvents).</param>
    /// <param name="buffer">This is the core text buffer that you can read/write to.
    /// This buffer can be shared by other views (like the XML editor view).</param>
    void Initialize(Microsoft.VisualStudio.OLE.Interop.IServiceProvider site, IVsTextLines buffer);

    /// <summary>
    /// This method is called periodically and gives you the "pulse" of Visual Studio.
    /// This is where you do idle time processing, for example, this is where you 
    /// could periodically sync up your view with the underlying buffer in case there
    /// have been any changes.
    /// </summary>
    /// <param name="grfidlef"></param>
    void OnIdle(uint grfidlef);

    /// <summary>
    /// This method is called when your view is activated.
    /// </summary>
    void OnActivate();
    /// <summary>
    /// This method is called when your view loses activation.
    /// </summary>
    void OnDeactivate();
    
    /// <summary>
    /// This method is called before closing the window frame.
    /// </summary>
    /// <returns>Return false if for some reason you cannot commit any pending edits.</returns>
    bool CommitPendingEdit();

    /// <summary>
    /// This method is called before VS is closed to see if there's any 
    /// reason not to close.  
    /// </summary>
    /// <param name="promptUser"></param>
    /// <returns>Return false if the user cancels the termination.</returns>
    bool CanTerminate(bool promptUser);

    /// <summary>
    /// This method is callled when your window frame is closed.
    /// </summary>
    void Close();
  }

	/// <summary>
	/// This class View provides an abstract base class for simple editor views
	/// that follow the VS simple embedding model.
	/// For example, the XmlSchemaDesigner and the XQueryDesigner.
	/// </summary>
  public abstract class SimpleEditorView : 
    IOleCommandTarget,
    IVsWindowPane,
    IVsToolboxUser,
    IVsStatusbarUser,
    IVsWindowPaneCommit,
    IOleComponent // for idle processing.
    //IServiceProvider,
    //IVsMultiViewDocumentView,
    //IVsFindTarget,
    //IVsWindowFrameNotify,
    //IVsCodeWindow,
    //IVsBroadcastMessageEvents,
    //IVsDocOutlineProvider,
    //IVsDebuggerEvents,
    // ??? VxDTE::IExtensibleObject,
    //IVsBackForwardNavigation
    // ??? public ISelectionContainer,
	{

    protected ServiceProvider site;
    protected IVsTextLines buffer;
    protected IOleComponentManager componentManager;
    protected uint componentID;

    public SimpleEditorView(IVsTextLines buffer) 
    {
      this.buffer = buffer;
    }

    #region IOleCommandTarget methods
    /// <summary>
    /// Override this method to provide custom command status, 
    /// e.g. (int)OLECMDF.OLECMDF_SUPPORTED | (int)OLECMDF.OLECMDF_ENABLED
    /// </summary>
    protected virtual int QueryCommandStatus(ref Guid guidCmdGroup, uint nCmdId) 
    {
      unchecked { return (int)OleDocumentError.OLECMDERR_E_UNKNOWNGROUP; }
    }
    /// <summary>
    /// Override this method to intercept the IOleCommandTarget::Exec call.
    /// </summary>
    /// <returns>Usually returns 0 if ok, or OLECMDERR_E_NOTSUPPORTED</returns>
    protected virtual int ExecCommand(ref Guid guidCmdGroup, uint nCmdId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) 
    {
      CCITracing.Trace(String.Format("ExecCommand({0},{1})", guidCmdGroup.ToString(), nCmdId));
      unchecked { return (int)OleDocumentError.OLECMDERR_E_NOTSUPPORTED; }
    }

    /// <summary>
    /// IOleCommandTarget implementation
    /// </summary>
    int IOleCommandTarget.QueryStatus(ref Guid guidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) 
    {
      for (uint i = 0; i < cCmds; i++) 
      {
        int rc = QueryCommandStatus(ref guidCmdGroup, (uint)prgCmds[i].cmdID);
        if (rc<0) return rc;
      }
      return 0;
    }
    int IOleCommandTarget.Exec(ref Guid guidCmdGroup, uint nCmdId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) 
    {
      return ExecCommand(ref guidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut);
    }
    #endregion

    #region IVsWindowPane methods

    public virtual void ClosePane() 
    {
      this.componentManager.FRevokeComponent(this.componentID);
    }

    public abstract void CreatePaneWindow(IntPtr hwndParent, int x, int y, int cx, int cy, out IntPtr hwnd);

    public virtual int GetDefaultSize(SIZE[] size) 
    {
      size[0].cx = 100;
      size[0].cy = 100;
      return 0;
    }
    public virtual int LoadViewState(Microsoft.VisualStudio.OLE.Interop.IStream stream) 
    {
      return 0;
    }
    public virtual int SaveViewState(Microsoft.VisualStudio.OLE.Interop.IStream stream) 
    {
      return 0;
    }
    public virtual void SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider site) 
    {
      this.site = new ServiceProvider(site);

      // register our independent view with the IVsTextManager so that it knows
      // the user is working with a view over the text buffer. this will trigger
      // the text buffer to prompt the user whether to reload the file if it is
      // edited outside of the development Environment.
      IVsTextManager textManager = (IVsTextManager)this.site.QueryService(VsConstants.SID_SVsTextManager, typeof(IVsTextManager));
      if (textManager != null) {
        IVsWindowPane windowPane = (IVsWindowPane)this;
        textManager.RegisterIndependentView(this, (VsTextBuffer)this.buffer);
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
        this.componentManager.FRegisterComponent(this, crinfo, out this.componentID);
      }

    }
    public virtual int TranslateAccelerator(MSG[] msg) 
    {
      return (int)HResult.S_FALSE;
    }
    #endregion 

    #region IVsToolboxUser methods
    public virtual int IsSupported(Microsoft.VisualStudio.OLE.Interop.IDataObject data) 
    {
      return (int)HResult.S_FALSE;
    }
    public virtual int ItemPicked(Microsoft.VisualStudio.OLE.Interop.IDataObject data)
    {
      return 0;
    }
    #endregion

    #region IVsStatusbarUser methods
    public virtual void SetInfo() 
    {
    }
    #endregion

    #region IVsWindowPaneCommit methods
    public virtual void CommitPendingEdit(out int fCommitFailed) 
    {
      fCommitFailed = 0;
    }
    #endregion

    #region IOleComponent Methods
    public virtual int FDoIdle( uint grfidlef ) 
    {
      return 0; 
    }

    public virtual void Terminate() 
    {
    }
    public virtual int FPreTranslateMessage(MSG[] msg) 
    {
      return 0;
    }
  
    public virtual void OnEnterState(uint uStateID, int fEnter) 
    {
    }

    public virtual void OnAppActivate(int fActive, uint dwOtherThreadID) 
    {
    }

    public virtual void OnLoseActivation()                                 
    {
    }

    public virtual void OnActivationChange(Microsoft.VisualStudio.OLE.Interop.IOleComponent pic, int fSameComponent, OLECRINFO[] pcrinfo, int fHostIsActivating, OLECHOSTINFO[] pchostinfo, uint dwReserved) 
    {
    }

    public virtual int FContinueMessageLoop(uint uReason, IntPtr pvLoopData, MSG[] pMsgPeeked)   
    { 
      return 1; 
    }

    public virtual int FQueryTerminate(int fPromptUser)                  
    { 
      return 1; 
    }

    public virtual IntPtr HwndGetWindow(uint dwWhich, uint dwReserved)     
    { 
      return IntPtr.Zero; 
    }

    public virtual int FReserved1(uint reserved, uint message, IntPtr wParam, IntPtr lParam) 
    { 
      return 1; 
    }
    #endregion 
	}


  /// <summary>
  /// This class View provides an abstract base class for custom editor views that
  /// support Ole Inplace activation (ActiveX controls).
  /// </summary>
  public abstract class OleEditorView : SimpleEditorView, 
    IOleCommandTarget,
    IVsWindowPane,
    IVsToolboxUser,
    IVsStatusbarUser,
    IOleObject,
    IOleInPlaceActiveObject,
    IOleInPlaceObject,
    IOleInPlaceComponent
    //IServiceProvider,
    //IOleDocumentView,
    //IOleDocument,
    //IOleInPlaceUIWindow,
    //IVsMultiViewDocumentView,
    //IVsFindTarget,
    //IVsWindowFrameNotify,
    //IVsCodeWindow,
    //IVsWindowPaneCommit,
    //IVsBroadcastMessageEvents,
    //IVsDocOutlineProvider,
    //IVsDebuggerEvents,
    // ??? VxDTE::IExtensibleObject,
    //IVsBackForwardNavigation
    // ??? public IVsTextLinesEvents,
    // ??? public ISelectionContainer,
    // ??? public IVsTextBufferDataEvents,
  {
    protected CookieMap eventSinks = new CookieMap();
    protected IOleComponentUIManager pCompUIMgr;
    protected IOleInPlaceComponentSite pIPCompSite;
    protected IOleClientSite pClientSite;
    protected Hashtable monikers = new Hashtable();    

    public OleEditorView(IVsTextLines buffer) : base(buffer) 
    {
    }
    
    #region IOleObject methods
    public virtual void Advise(IAdviseSink sink, out uint cookie) 
    {
      cookie = eventSinks.Add(sink);
    }
    public virtual void Close(uint dwSaveOption)
    {
    }
    public virtual void DoVerb(int iVerb, MSG[] msg, IOleClientSite site, 
      int index, IntPtr hwndParent, RECT[] posRect) 
    {
    }
    public virtual void EnumAdvise(out IEnumSTATDATA ppEnumAdvise)
    {
      ppEnumAdvise = null;
    }
    public virtual void EnumVerbs(out IEnumOLEVERB ppEnumVerbs)
    {
      ppEnumVerbs = null;
    }
    public virtual void GetClientSite(out IOleClientSite site)
    {
      site = this.pClientSite;
    }
    public virtual void GetClipboardData(uint reserved, out Microsoft.VisualStudio.OLE.Interop.IDataObject obj)
    {
      obj = null;
    }
    public virtual void GetExtent(uint dwDrawAspect, SIZEL[] size)
    {
    }
    public virtual void GetMiscStatus(uint dwAspect, out uint status)
    {
      status = 0;
    }
    public virtual void GetMoniker(uint iAssign, uint whichMoniker, out IMoniker moniker)
    {
      object key = (object)whichMoniker;
      moniker = (IMoniker)monikers[key];
    }
    public virtual void GetUserClassID(out Guid pClsid)
    {
      pClsid = this.GetType().GUID;
    }
    public virtual void GetUserType(uint formOfType, IntPtr userType)
    {
    }
    public virtual void InitFromData(Microsoft.VisualStudio.OLE.Interop.IDataObject data, int fCreation, uint reserved)
    {
    }
    public virtual void IsUpToDate()
    {
    }
    public virtual void SetClientSite(IOleClientSite site)
    {
      this.pClientSite = site;
    }
    public virtual void SetColorScheme(LOGPALETTE[] logicalPalette)
    {
    }
    public virtual void SetExtent(uint drawAspect, SIZEL[] size)
    {
    }
    public virtual void SetHostNames(string containerApp, string containerObj)
    {
    }
    public virtual void SetMoniker(uint whichMoniker, IMoniker moniker)
    {
      object key = (object)whichMoniker;
      if (monikers.Contains(key)) monikers.Remove(key);
      monikers.Add(key, moniker);
    }
    public virtual void Unadvise(uint dwCookie)
    {
      eventSinks.RemoveAt(dwCookie);
    }
    public virtual void Update()
    {
    }
    #endregion 
    
    #region IOleInPlaceActiveObject
    public virtual void EnableModeless(int fEnable)
    {
    }
    public virtual void OnDocWindowActivate(int fActivate)
    {
    }
    public virtual void OnFrameWindowActivate(int fActivate)
    {
    }
    public virtual void ResizeBorder(RECT[] border, ref Guid iid, IOleInPlaceUIWindow window, int fFrameWindow)
    {
    }
    #endregion 

    #region IOleInPlaceObject methods
    public virtual void ContextSensitiveHelp(int fEnterHelp)
    {
    }
    public virtual void GetWindow(out IntPtr hwnd) 
    {
      hwnd = IntPtr.Zero;
    }
    public virtual void InPlaceDeactivate()
    {
    }
    public virtual void ReactivateAndUndo()
    {
    }
    public virtual void SetObjectRects(RECT[] posRect, RECT[] clipRect)
    {
    }
    public virtual void UIDeactivate()
    {
    }
    #endregion 

    #region IOleInPlaceComponent methods

    public virtual int FQueryClose(int fPromptUser) 
    {
      return 0;
    }
    public virtual void GetCntrContextMenu(uint dwRoleActiveObject, ref Guid clsidActiveObject, 
      int nMenuIdActiveObject,  POINTS[] pos, out Guid clsidCntr, OLEMENUID[] menuid, out uint pgrf)
    {
      clsidCntr = Guid.Empty;
      pgrf = 0;
    }
    public virtual void GetCntrHelp(ref uint pdwRole, ref Guid pclsid, POINT posMouse, 
      uint dwHelpCmd, string pszHelpFileIn, out string pwsHelpFileOut, uint dwDataIn, out uint dwDataOut) 
    {
      pwsHelpFileOut = pszHelpFileIn;
      dwDataOut = dwDataIn;
    }
    public virtual void GetCntrMessage(ref uint pdwRolw, ref Guid clsid, string titleIn, string textIn,
      string helpFileIn, out string titleOut, out string textOut, out string helpFileOut, ref uint dwHelpContextId,
      OLEMSGBUTTON[] msgbutton, OLEMSGDEFBUTTON[] msgdefbutton, OLEMSGICON[] msgicon, ref int sysAlert) 
    {
      titleOut = titleIn;
      textOut = textIn;
      helpFileOut = helpFileIn;      
    }

    public virtual void OnWindowActivate(uint windowType, int fActivate) 
    {
    }
    public virtual void TranslateCntrAccelerator(MSG[] msg) 
    {
    }
    public virtual void UseComponentUIManager(uint dwCompRole, out uint pgrfCompFlags, 
      IOleComponentUIManager pCompUIMgr, IOleInPlaceComponentSite pIPCompSite) 
    {
      pgrfCompFlags = 0;    
      this.pCompUIMgr = pCompUIMgr;
      this.pIPCompSite = pIPCompSite;
      
    }
    #endregion
  }


  /// <summary>
  /// This class wraps a managed WinForm control and uses that as the editor window.
  /// </summary>
  public class EditorControl : SimpleEditorView
  {
    protected Control control;    
    protected IDesigner designer;
    
    // the Controls must also be an IDesigner.
    public EditorControl(ServiceProvider site, IVsTextLines buffer, Control ctrl) : base(buffer)
    {
      this.control = ctrl;
      this.site = site;
      if (ctrl is IDesigner) {
        this.designer = (IDesigner)ctrl;
      }           
    }

    public override void ClosePane() 
    {
      base.ClosePane();

      if (designer != null) {
        designer.Close();
      }
      if (control != null) 
      {
        control.Dispose();
        control = null;
      }      
    }

    public override void CreatePaneWindow(IntPtr hwndParent, int x, int y, int cx, int cy, out IntPtr hwnd) 
    {
      control.SuspendLayout();
      control.Left = x;
      control.Top = y;
      control.Width = cx;
      control.Height = cy;
      control.ResumeLayout();
      control.CreateControl();
      NativeWindowHelper.SetParent(control.Handle, hwndParent);
      hwnd = control.Handle;
    }
    public override void CommitPendingEdit(out int fCommitFailed) {
      fCommitFailed = 0;
      if (designer != null) {
        fCommitFailed = (designer.CommitPendingEdit() ? 0 : 1);
      }
    }   
 
    public override int FDoIdle(uint grfidlef) {
      if (designer != null) designer.OnIdle( grfidlef);        
      return 0;
    }

    public override void OnAppActivate(int fActive, uint dwOtherThreadID) {
      if (designer != null) designer.OnActivate();
    }
    public override int FQueryTerminate(int fPromptUser) {
      if (designer != null) {
        return designer.CanTerminate(fPromptUser == 1) ? 1 : 0;
      }
      return 1;
    }
    public override void OnLoseActivation() {
      if (designer != null) designer.OnDeactivate();
    }

    public override IntPtr HwndGetWindow(uint dwWhich, uint dwReserved) {
      return control.Handle;
    }

    protected override int ExecCommand(ref Guid guidCmdGroup, uint nCmdId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
      // todo: we really need to pass all of IOleCommandTarget through to the control
      // so it can decide what to implement.  In fact, we should do all of 
      // IOleInPlaceUIWindow so the control can even add menu items.
      if (guidCmdGroup == VsConstants.guidStandardCommandSet97) {
        VsCommands cmd = (VsCommands)nCmdId;
        int msg = 0;
        IntPtr wParam = IntPtr.Zero;
        switch (cmd) {
          case VsCommands.Cut:
            msg = NativeWindowHelper.WM_CUT;
            break;
          case VsCommands.Copy:
            msg = NativeWindowHelper.WM_COPY;
            break;
          case VsCommands.Paste:
            msg = NativeWindowHelper.WM_PASTE;
            break;
          case VsCommands.Undo:
            msg = NativeWindowHelper.WM_UNDO;
            break;
          case VsCommands.Delete:
            msg = NativeWindowHelper.WM_KEYDOWN;
            wParam = (IntPtr)(int)Keys.Delete;
            break;
        }
        if (msg != 0) {
          Control target = Control.FromHandle(NativeWindowHelper.GetFocus());
          if (target != null) {
            IntPtr rc = NativeWindowHelper.SendMessage(target.Handle, msg, wParam, IntPtr.Zero);
            if ((int)rc == 0) return 0;
          }
        }
      }
      return base.ExecCommand(ref guidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut);
    }
  }
}
