//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop; 
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.Win32;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

using IServiceProvider = System.IServiceProvider;

namespace Microsoft.VisualStudio.Package{

  public enum ParseReason {
    Colorize,
    Check,
    MemberSelect,
    CompleteWord,
    QuickInfo,
    MethodTip,
    MatchBraces,
    HighlightBraces,
    Autos,
    CodeSpan,
    CollapsibleRegions,
//    Compile,
  };

  public class SourceContext{
    public int StartLine;
    public int StartColumn;
    public int EndLine;
    public int EndColumn;
    public string FileName;
    /// <summary>Lines and columns are zero based</summary>
    public SourceContext(int startLine, int startColumn, int endLine, int endColumn, string fileName){
      this.StartLine = startLine;
      this.StartColumn = startColumn;
      this.EndLine = endLine;
      this.EndColumn = endColumn;
      this.FileName = fileName;
    }
  }
  public abstract class ErrorNode{
    public int Code;
    public SourceContext SourceContext;

    protected ErrorNode(int code, SourceContext sourceContext){
      this.Code = code;
      this.SourceContext = sourceContext;
    }
    public String GetMessage(){
      return this.GetMessage(null);
    }
    public abstract string GetMessage(System.Globalization.CultureInfo culture);
    public abstract Severity Severity{
      get;
    }
  }

  public abstract class LanguageService : 
      ILanguageService, IVsLanguageInfo, IVsLanguageDebugInfo, IVsProvideColorableItems, IVsLanguageContextProvider, IVsOutliningCapableLanguage,
      Microsoft.VisualStudio.OLE.Interop.IServiceProvider, Microsoft.VisualStudio.OLE.Interop.IOleCommandTarget{
    public CultureInfo culture;
    public ServiceProvider site;
    /// <summary>Set this to True if you want drop downs showing types and members</summary>
    public bool EnableDropDownCombos; 
    public ImageList imageList;
    private ArrayList codeWindowManagers;
    private LanguagePreferences preferences;
    private uint langServiceCookie;
    private string extensions;
    private Hashtable sources;

    public LanguageService(ImageList completionImages){
      this.extensions = null;
      this.codeWindowManagers = new ArrayList();
      if (completionImages != null)
        this.imageList = completionImages;
      else{
        this.imageList = new ImageList();
        this.imageList.AddImages("Microsoft.VisualStudio.Package.LanguageService.completionset.bmp", typeof(LanguageService).Assembly, 180, 16, 16, Color.FromArgb(0,255,0));
      }
      this.sources = new Hashtable();
    }

    /// <summary>
    /// Initialize the shell and uiShell objects, initialize the user preferences object,
    /// get the CultureInfo, and register the language service with VS.
    /// </summary>
    public virtual void Init(ServiceProvider site, ref Guid languageGuid, uint lcid, string extensions){
      this.culture = lcid == 0 ? CultureInfo.InvariantCulture : new CultureInfo((int)lcid);
      this.site = site;      

      // Register this language service with VS.
      // should be: 0xCB728B20, 0xF786, 0x11ce, { 0x92, 0xAD, 0x00, 0xAA, 0x00, 0xA7, 0x4C, 0xD0 }
      IProfferService proffer = (IProfferService)this.site.QueryService(VsConstants.SID_ProfferService, typeof(IProfferService));
      proffer.ProfferService(ref languageGuid, this.site.Unwrap(), out this.langServiceCookie);      
    }

    public LanguagePreferences Preferences {
      get {
        if (this.preferences == null){
          this.preferences = this.CreateLanguagePreferences();
        }
        return this.preferences;
      }
      set {
        this.preferences = value;
      }
    }
    /// <summary>
    /// Cleanup the sources, uiShell, shell, preferences and imageList objects
    /// and unregister this language service with VS.
    /// </summary>
    public virtual void Done(){
      this.StopThread();
      if (this.sources != null){
        foreach (Source s in this.sources.Values) s.Close();
      }
      this.sources = null;

      if (this.langServiceCookie != 0){
        IProfferService proffer = (IProfferService)this.site.QueryService( VsConstants.SID_ProfferService, typeof(IProfferService));
        if (proffer != null){
          proffer.RevokeService(this.langServiceCookie);
        }
	    this.langServiceCookie = 0;
      }
      this.site = null;            
      this.preferences = null;
      if (this.imageList != null) this.imageList.Dispose();
      this.imageList = null;
      GC.Collect();
    }

    // Methods implemented by subclass.
    public abstract LanguagePreferences CreateLanguagePreferences();
    public abstract IScanner GetScanner(string filePath);
    public abstract string LanguageShortName { get; }

    #region IVsProvideColorableItems
    public virtual int GetItemCount(out int count){
      count = 0;
      return (int)HResult.E_NOTIMPL;
    }
    public virtual int GetColorableItem(int index, out IVsColorableItem item){
      item = null;
      return (int)HResult.E_NOTIMPL;
    }
    #endregion 

    #region IVsLanguageContextProvider 
    int IVsLanguageContextProvider.UpdateLanguageContext(uint dwHint, IVsTextLines buffer, TextSpan[] ptsSelection, object ptr){
      if (ptr != null && ptr is IVsUserContext){
        UpdateLanguageContext((LanguageContextHint)dwHint, buffer, ptsSelection, (IVsUserContext)ptr);
      }
      return 0;
    }
    #endregion 

    public int CollapseToDefinitions(IVsTextLines textLines, IVsOutliningSession session){
      if (textLines == null || session == null) return (int)HResult.E_INVALIDARG;
      int lastLine;
      int lastIdx;
      string text;
      textLines.GetLineCount(out lastLine );
      textLines.GetLengthOfLine(--lastLine, out lastIdx);
      textLines.GetLineText(0, 0, lastLine, lastIdx, out text);
      NewOutlineRegion[] outlineRegions = this.GetCollapsibleRegions(text, VsShell.GetFilePath(textLines));
      if (outlineRegions != null && outlineRegions.Length > 0)
        session.AddOutlineRegions((uint)ADD_OUTLINE_REGION_FLAGS.AOR_PRESERVE_EXISTING, outlineRegions.Length, outlineRegions);
      return 0;
    }

    public virtual NewOutlineRegion[] GetCollapsibleRegions(string text, string fileName){
      return null;
    }

    public virtual void UpdateLanguageContext(LanguageContextHint hint, IVsTextLines buffer, TextSpan[] ptsSelection, IVsUserContext context){
    }

    public virtual void GetCommentFormat(CommentInfo info){      
      info.supported = true;
      info.lineStart = "//";
      info.blockStart = "/*";
      info.blockEnd = "*/";
      info.useLineComments = true;
    }
    public virtual ImageList GetImageList(){
      return this.imageList;
    }

    public virtual void GetMethodFormat(out string typeStart, out string typeEnd, out bool typePrefixed){
      typeStart = null;
      typeEnd = null;
      typePrefixed = true;
    }

    public abstract AuthoringScope ParseSource(string text, int line, int col, string fname, AuthoringSink asink, ParseReason reason);

    public bool IsMacroRecordingOn(){
      IVsShell shell = GetVsShell();
      if (shell != null){
        object pvar;
        shell.GetProperty((int)__VSSPROPID.VSSPROPID_RecordState, out pvar);
        shell = null;
        if (pvar != null){
          return ((VsRecordState)pvar == VsRecordState.On);
        }
      }
      return false;
    }

    public Microsoft.VisualStudio.Shell.Interop.IVsDebugger GetIVsDebugger(){
      Guid guid = typeof(Microsoft.VisualStudio.Shell.Interop.IVsDebugger).GUID;
      Microsoft.VisualStudio.Shell.Interop.IVsDebugger debugger = (Microsoft.VisualStudio.Shell.Interop.IVsDebugger)this.site.QueryService(VsConstants.SID_SVsShellDebugger, typeof(Microsoft.VisualStudio.Shell.Interop.IVsDebugger));
      return debugger;
    }

    public IVsTextMacroHelper GetIVsTextMacroHelperIfRecordingOn(){
      if (IsMacroRecordingOn()){
        IVsTextManager textmgr = (IVsTextManager)site.QueryService(VsConstants.SID_SVsTextManager, typeof(IVsTextManager));
        return (IVsTextMacroHelper)textmgr;
      }
      return null;
    }

    public IVsUIShell GetUIShell(){
      return (IVsUIShell)this.site.QueryService(VsConstants.guidShellIID, typeof (IVsUIShell ));
    }
    public IVsShell GetVsShell(){
      Guid IID_IVsShell = typeof(IVsShell).GUID;
      Guid SID_IVsShell = IID_IVsShell;
      return (IVsShell)this.site.QueryService(SID_IVsShell, typeof(IVsShell));
    }

    public virtual void ShowContextMenu(int menuID, Guid groupGuid){     
      IVsUIShell uiShell = GetUIShell();
      if (uiShell != null && ! IsMacroRecordingOn()){ // disable context menu while recording macros.
        System.Drawing.Point pt = System.Windows.Forms.Cursor.Position;        
        POINTS[] pnts = new POINTS[1];
        pnts[0].x = (short)pt.X;
        pnts[0].y = (short)pt.Y;
        try {
          uiShell.ShowContextMenu(0, ref groupGuid, menuID, pnts, (IOleCommandTarget)this);
        } catch (Exception){
          // no need to bubble these up to VS...
        }
      }
      uiShell = null;
    }
    protected int lastLine = -1;
    protected int lastCol = -1;
    protected string lastFileName;
    public virtual void OnIdle(bool periodic){

      // here's our chance to synchronize combo's and so on, 
      // first we see if the caret has moved.      
      IVsTextView view = ViewFilter.LastActiveTextView;
      if (view != null){
        IVsTextLines buffer;
        view.GetBuffer(out buffer);
        CodeWindowManager m = this.GetCodeWindowManagerForView(view);
        foreach (Source s in this.sources.Values){
          if (s.GetTextLines() == buffer){
            if (m != null){
              int line = -1, col = -1;
              view.GetCaretPos(out line, out col);
              string fileName = this.GetFileName(view);
              if (line != this.lastLine || col != this.lastCol || fileName != this.lastFileName){
                this.lastLine = line;
                this.lastCol = col;
                this.lastFileName = fileName;
                this.OnCaretMoved(m, view, line, col);          
              }      
            }
            s.OnIdle(periodic);
          }
        }
      }
    }
    public string GetFileName(IVsTextView view){
      if (view == null) return null;
      string fname = null;
      try{
        uint formatIndex;
        IVsTextLines pBuffer;
        view.GetBuffer(out pBuffer);
        IPersistFileFormat pff = (IPersistFileFormat)pBuffer;
        pff.GetCurFile(out fname, out formatIndex);
        pff = null;
        pBuffer = null;
      }catch{}
      return fname;
    }
    public virtual void OnCaretMoved(CodeWindowManager mgr, IVsTextView textView, int line, int col){
      if (this.EnableDropDownCombos && mgr.dropDownHelper != null){
        mgr.dropDownHelper.SynchronizeDropdowns(textView, line, col);
        mgr.dropDownHelper.dropDownBar.RefreshCombo(0, mgr.dropDownHelper.selectedType);
        mgr.dropDownHelper.dropDownBar.RefreshCombo(1, mgr.dropDownHelper.selectedMember);
      }
    }
    public virtual void SynchronizeDropdowns(){
      if (this.EnableDropDownCombos){ 
        IVsTextView textView = ViewFilter.LastActiveTextView;
        CodeWindowManager mgr = this.GetCodeWindowManagerForView(textView);
        if (mgr != null && mgr.dropDownHelper != null && textView != null){
          try{ 
            int line = -1, col = -1;
            textView.GetCaretPos(out line, out col);
            mgr.dropDownHelper.SynchronizeDropdowns(textView, line, col);
          }catch{}
        } 
      }
    }
    protected virtual void OnChangesCommitted(uint flags,Microsoft.VisualStudio.TextManager.Interop.TextSpan[] ptsChanged){
    }
    /// <summary>
    /// Override this method to intercept the IOleCommandTarget::QueryStatus call.
    /// </summary>
    /// <param name="guidCmdGroup"></param>
    /// <param name="cmd"></param>
    /// <returns>Usually returns OLECMDF_ENABLED  | OLECMDF_SUPPORTED
    /// or return OLECMDERR_E_UNKNOWNGROUP if you don't handle this command
    /// </returns>
    protected virtual int QueryCommandStatus(ref Guid guidCmdGroup, uint cmd){
      unchecked { return (int)OleDocumentError.OLECMDERR_E_UNKNOWNGROUP; }
    }
    /// <summary>
    /// Override this method to intercept the IOleCommandTarget::Exec call.
    /// </summary>
    /// <param name="guidCmdGroup"></param>
    /// <param name="nCmdId"></param>
    /// <param name="nCmdexecopt"></param>
    /// <param name="pvaIn"></param>
    /// <param name="pvaOut"></param>
    /// <returns>Usually returns 0 if ok, or OLECMDERR_E_NOTSUPPORTED</returns>
    protected virtual int ExecCommand(ref Guid guidCmdGroup, uint nCmdId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut){
      unchecked { return (int)OleDocumentError.OLECMDERR_E_NOTSUPPORTED; }
    }

    // IOleCommandTarget    
    int IOleCommandTarget.QueryStatus(ref Guid guidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText){

      for (uint i = 0; i < cCmds; i++){
        int rc = QueryCommandStatus(ref guidCmdGroup, (uint)prgCmds[i].cmdID);
        if (rc<0) return rc;
        prgCmds[i].cmdf = (uint)rc; 
      }
      return 0;
    }


    int IOleCommandTarget.Exec(ref Guid guidCmdGroup, uint nCmdId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut){
      return ExecCommand(ref guidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut);
    }

    public IVsOutputWindowPane GetOutputPane(){
      Guid sid = new Guid("533FAD11-FE7F-41EE-A381-8B67792CD692");
      IVsOutputWindow outputWindow = (IVsOutputWindow)site.QueryService(sid, typeof(IVsOutputWindow));
      Guid buildOutput = new Guid("df39052f-854c-4d4e-9b2e-d2500ddd09d1");
      IVsOutputWindowPane pane;
      outputWindow.CreatePane(ref buildOutput, "Build", 1, 1);
      outputWindow.GetPane(ref buildOutput, out pane); 
      return pane;
    }

    // Override this method to plug in your own custom colorizer.
    // You shouldn't need to do this since the colorizer simply
    // uses your Scanner to get the color information.
    public virtual Colorizer GetColorizer(IVsTextLines buffer){
      return new Colorizer(this, buffer);
    }
    // We have to make sure we return the same colorizer for each text buffer,
    // so we keep a hashtable of IVsTextLines -> Source objects, the Source
    // object owns the Colorizer for that buffer.
    public virtual Source GetSource(IVsTextLines buffer){
      Source s = (Source)this.sources[buffer];
      if (s == null){
        s = new Source(this, buffer, this.GetColorizer(buffer));
        this.sources.Add(buffer,s);
      }
      return s;
    }

    public virtual void CloseSource(Source source){
      object key = source.GetTextLines();
      if (this.sources.Contains(key)){
        this.sources.Remove(key);
      }
      source.Close();
    }

    #region IVsLanguageInfo methods
    // GetCodeWindowManager -- this gives us the VsCodeWindow which is what we need to
    // add adornments and so forth.
    public virtual int GetCodeWindowManager(IVsCodeWindow w, out IVsCodeWindowManager mgr) {
      IVsCodeWindow codeWindow = (IVsCodeWindow)w;
      IVsTextView textView;
      w.GetPrimaryView(out textView);
      IVsTextLines buffer;
      textView.GetBuffer(out buffer);
      mgr = this.GetCodeWindowManager(this, codeWindow, GetSource(buffer));
      return 0;
    }
    public abstract CodeWindowManager GetCodeWindowManager(LanguageService languageService, IVsCodeWindow codeWindow, Source source);

    public int GetColorizer(IVsTextLines buffer, out IVsColorizer result){
      Source s = GetSource(buffer);
      result = s.GetColorizer();
      return 0;
    }

    public virtual int GetFileExtensions(out string fileExtensions) {
      fileExtensions = this.extensions;
      return 0;
    }

    public virtual int GetLanguageName(out string name) {
      name = this.LanguageShortName;
      return 0;
    }
    #endregion 
   
    #region IVsLanguageDebugInfo methods
    public virtual int GetLanguageID(IVsTextBuffer buffer, int line, int col, out Guid langId){
      langId = this.GetType().GUID;
      return 0;
    }
    public virtual int GetLocationOfName(string name, out string pbstrMkDoc, TextSpan[] spans){
      pbstrMkDoc = null;
      return (int)HResult.E_NOTIMPL;
    }
    public virtual int GetNameOfLocation(IVsTextBuffer buffer, int line, int col, out string name, out int lineOffset){
      name = null;
      lineOffset = 0;
      /*
         TRACE1( "LanguageService(%S)::GetNameOfLocation", m_languageName );
        OUTARG(lineOffset);
        OUTARG(name);
        INARG(textBuffer);

        HRESULT hr;
        IScope* scope = NULL;
        hr = GetScopeFromBuffer( textBuffer, &scope );
        if (FAILED(hr)) return hr;
  
        long realLine = line;
        hr = scope->Narrow( line, idx, name, &realLine );
        RELEASE(scope);
        if (hr != S_OK) return hr;

        *lineOffset = line - realLine;
        return S_OK;
      */  
      return 0;
    }
    public virtual int GetProximityExpressions(IVsTextBuffer buffer, int line, int col, int cLines,  out IVsEnumBSTR ppEnum){
      ppEnum = null;
      /*
        TRACE2( "LanguageService(%S)::GetProximityExpressions: line %i", m_languageName, line );
        OUTARG(exprs);
        INARG(textBuffer);

        //check the linecount
        if (lineCount <= 0) lineCount = 1;

        //get the source 
        //TODO: this only works for sources that are opened in the environment
        HRESULT hr;
        Source* source = NULL;
        hr = GetSource( textBuffer, &source );
        if (FAILED(hr)) return hr;

        //parse and find the proximity expressions
        StringList* strings = NULL;
        hr = source->GetAutos( line - 1, line + lineCount - 1, &strings );
        RELEASE(source);
        if (FAILED(hr)) return hr;

        hr = strings->QueryInterface( IID_IVsEnumBSTR, reinterpret_cast<void**>(exprs) );
        RELEASE(strings);
        if (FAILED(hr)) return hr;
  
        return S_OK;
      */
	    return (int)HResult.S_FALSE; 
    }
    public virtual int IsMappedLocation(IVsTextBuffer buffer, int line, int col){
      return (int)HResult.S_FALSE;
    }
    public virtual int ResolveName(string name, uint flags, out IVsEnumDebugName ppNames){
      ppNames = null;
      return (int)HResult.E_NOTIMPL;
    }
    public virtual int ValidateBreakpointLocation(IVsTextBuffer buffer, int line, int col, TextSpan[] pCodeSpan){
      // for some reason, letting this method return normally, even though it is returning E_NOTIMPL
      // means breakpoints don't get set properly. I found this next line in version 16 of this file.
      // It throws an exception which somehow leads to the breakpoint being set on the right line.
      // Sheesh!
      NativeHelpers.RaiseComError(HResult.E_NOTIMPL);
      return (int)HResult.E_NOTIMPL;
    }
    #endregion 

    #region Microsoft.VisualStudio.OLE.Interop.IServiceProvider methods
    public virtual int QueryService(ref Guid guidService, ref Guid iid, out IntPtr obj) {
      obj = IntPtr.Zero;
      if (this.site != null) {
        this.site.Unwrap().QueryService(ref guidService, ref iid, out obj);
        return 0;
      }
      return (int)HResult.E_UNEXPECTED;
    }   
    #endregion 
    
    // CreateViewFilter -- this gives us the abilility to insert our own view filter
    // into the command chain.  This will only be called if GetCodeWindowManager
    // above returns a new CodeWindowManager object. 
    public virtual ViewFilter CreateViewFilter(Source source, IVsTextView newView){
      return new ViewFilter(this, source, newView);
    }

    public void AddCodeWindowManager(CodeWindowManager m){
      this.codeWindowManagers.Add(m);
    }
    public void RemoveCodeWindowManager(CodeWindowManager m){
      this.codeWindowManagers.Remove(m);
    }
    public CodeWindowManager GetCodeWindowManagerForView(IVsTextView view){
      if (view == null) return null;

      // find the window who's last active text view = the ViewFileter.LastActiveTextView
      foreach (CodeWindowManager m in this.codeWindowManagers){
        if (m.codeWindow != null){
          try {
            IVsTextView pView;
            m.codeWindow.GetLastActiveView(out pView);
            if (pView == view)
              return m;
          }catch{}
        }
      }
      return null;
    }

    internal void BeginParse(ParseRequest request, ParseResultHandler handler){
      lock(this){
        request.Callback = handler;
        this.parseRequest = request;
        this.parseRequestPending.Set();
        if (this.parseThread == null){
          if (this.Control == null){
            this.Control = new System.Windows.Forms.Button();
            this.Control.CreateControl();
          }
          this.parseThread = new Thread(new ThreadStart(this.ParseThread));
          this.parseThread.Start();
        }
      }
    }

    private void StopThread(){
      if (this.parseThread != null){
        this.parseRequest = new ParseRequest(true);
        this.parseRequestPending.Set();
        this.parseThreadTerminated.WaitOne(500, false);
        this.parseThread = null;
        this.Control = null;
      }
    }

    internal ParseRequest parseRequest;
    internal ManualResetEvent parseRequestPending = new ManualResetEvent(false);
    internal ManualResetEvent parseThreadTerminated = new ManualResetEvent(false);
    internal Thread parseThread;
    /// <summary>Provides a way to call back to the foreground thread via its message loop.</summary>
    internal System.Windows.Forms.Button Control;

    internal void ParseThread(){
      while (true){
        //Sleep until a parse request arrives. Get impatient after 3 seconds have elapsed.
        if (!this.parseRequestPending.WaitOne(3000, true)){
          continue;
        }
        ParseRequest req = null;
        lock(this){
          req = this.parseRequest;
          this.parseRequestPending.Reset();
        }
        if (req.Terminate)
          break;
        
        try {
          this.receivedParseRequest = req; //This is put in the instance field so that the next line does not need a parameter
#if DEBUG_Break
          DebugTools.BreakOnFirstChance(new DebugTools.MethodInvoker(this.HandleReceivedParseRequest));
#else
          this.HandleReceivedParseRequest();
#endif
        } catch{
        }
        this.receivedParseRequest = null;
      }
      this.parseThreadTerminated.Set();
    }
    private ParseRequest receivedParseRequest;

    void HandleReceivedParseRequest(){
      ParseRequest req = this.receivedParseRequest;
      this.ParseRequest(req);
      if (this.Control != null){
        ISynchronizeInvoke sinvoke = (ISynchronizeInvoke)this.Control;
        sinvoke.BeginInvoke(req.Callback, new object[1] { req } );
      }
    }

    internal void ParseRequest(ParseRequest req){      
      req.Scope = this.ParseSource(req.Text, req.Line, req.Col, req.FileName, req.Sink, req.Reason);
    }

  } // end class LanguageService

  public delegate void ParseResultHandler(ParseRequest request);

  public class ParseRequest {
    public int Line, Col;
    public string FileName;
    public string Text;
    public ParseReason Reason;
    public IVsTextView View;
    public bool Terminate;
    public ParseResultHandler Callback;
    public AuthoringSink Sink;
    public AuthoringScope Scope;
    public TokenInfo TokenInfo; 
#if LookForMemoryLeaks
    public long UsedMemoryAtStartOfRequest;
#endif

    public ParseRequest(bool terminate){ 
      this.Terminate = terminate; 
    }
    public ParseRequest(int line, int col, TokenInfo info, string text, string fname, ParseReason reason, IVsTextView view){
      this.Line = line; this.Col = col;
      this.FileName = fname;
      this.Text = text; this.Reason = reason;
      this.View = view;
      this.Sink = new AuthoringSink(reason, line, col);
      this.TokenInfo = info;
#if LookForMemoryLeaks
      System.GC.Collect();
      this.UsedMemoryAtStartOfRequest = System.GC.GetTotalMemory(true);
#endif
    }
  }

  public abstract class AuthoringScope{
    public abstract string GetDataTipText(int line, int col, out TextSpan span);
    public abstract Overloads GetMethods(int line, int col, string name);
    public abstract Overloads GetTypes(int line, int col, string name);
    public abstract string Goto(VsCommands cmd, IVsTextView textView, int line, int col, out TextSpan span);
    public abstract Declarations GetDeclarations(IVsTextView view, int line, int col, TokenInfo info);
    //REVIEW: why pass in the view and the info?
  }

  public abstract class Declarations{
    public abstract int GetCount();
    public abstract string GetDisplayText(int index);
    public abstract string GetInsertionText(int index);
    public abstract string GetDescription(int index);
    public abstract int GetGlyph(int index);
    public abstract void GetBestMatch(string text, out int index, out bool uniqueMatch);
    public abstract bool IsCommitChar(string textSoFar, char commitChar);
  }

  //-------------------------------------------------------------------------------------
  public abstract class Overloads{
    public abstract string GetName(int index);
    public abstract int GetCount();
    public abstract string GetDescription(int index);
    public abstract string GetType(int index);
    public abstract int GetParameterCount(int index);
    public abstract void GetParameterInfo(int index, int parameter, out string name, out string display, out string description);
    public abstract string GetParameterClose(int index);
    public abstract string GetParameterOpen(int index);
    public abstract string GetParameterSeparator(int method);
    public abstract int GetPositionOfSelectedMember();
  }

  // This little class keeps track of method call depth for nested calls on a single line.
  public class CallInfo {
    public int  currentParameter;
    public StringCollection names;
    public ArrayList sourceLocations;
    public bool isTemplateInstance;
  };

  public class MethodCalls {
    private Stack calls;
    private CallInfo call;

    public MethodCalls(){
      this.calls = new Stack();
      this.Push(new StringCollection(), new ArrayList(), false);
    }

    public void Push(StringCollection names, ArrayList sourceLocations, bool isTemplateInstance){
      this.calls.Push(call);
      this.call = new CallInfo();
      this.call.names = names;
      this.call.sourceLocations = sourceLocations;
      this.call.isTemplateInstance = isTemplateInstance;
    }
    public void NextParameter(){
      if (this.call != null) this.call.currentParameter++;
    }
    public void Pop(){
      if (this.calls.Count <= 0){Debug.Assert(false); return;}
      this.calls.Pop();
      if (this.calls.Count > 0) this.call = (CallInfo)this.calls.Peek();
    }
    public CallInfo GetCurrentMethodCall(){
      return this.call;
    }
  }

  /// <summary>
  /// AuthoringSink is used to gather information from the parser to help in the following:
  /// - error reporting
  /// - matching braces (ctrl-])
  /// - intellisense: Member Selection, CompleteWord, QuickInfo, MethodTips
  /// - management of the autos window in the debugger
  /// - breakpoint validation
  /// </summary>
  public class AuthoringSink{
    protected ParseReason reason;
    public ArrayList Errors;
    public StringCollection Names;
    public ArrayList SourceLocations;
    public int Line;
    public int Column;
    public MethodCalls MethodCalls;
    public ArrayList Spans;
    public bool FoundMatchingBrace;

    public AuthoringSink(ParseReason reason, int line, int col){
      this.reason = reason;
      this.Errors = new ArrayList();
      this.Line = line;
      this.Column = col;
      this.Names = new StringCollection();
      this.SourceLocations = new ArrayList();
      this.MethodCalls = new MethodCalls();
      this.Spans = new ArrayList();
    }

    /// <summary>
    /// Whenever a matching pair is parsed, e.g. '{' and '}', this method is called
    /// with the text span of both the left and right item. The
    /// information is used when a user types "ctrl-]" in VS
    /// to find a matching brace and when auto-highlight matching
    /// braces is enabled.
    /// </summary>
    public virtual void MatchPair(SourceContext startContext, SourceContext endContext){      
      switch(this.reason){
        case ParseReason.MatchBraces: 
        case ParseReason.HighlightBraces:{
          int startLine1 = startContext.StartLine;
          int endLine1 = startContext.EndLine;
          int startCol1 = startContext.StartColumn;
          int endCol1 = startContext.EndColumn;
          int startLine2 = endContext.StartLine;
          int endLine2 = endContext.EndLine;
          int startCol2 = endContext.StartColumn;
          int endCol2 = endContext.EndColumn;
          if (startLine1 < 0 || endLine1 < startLine1 || startCol1 < 0 || (endLine1 == startLine1 && endCol1 < startCol1) ||
            startLine2 < endLine1 || endLine2 < endLine1 || startCol2 < 0 || (endLine2 == startLine2 && endCol2 < startCol2)){
            Debug.Assert(false);
            return;
          }
          if (startLine1 < endLine1){
            // can't handle multiline .... just move it to the lower line
            startLine1 = endLine1;
            if (startCol1 >= endCol1) startCol1 = 0; // make sure start is to the left of end in case we moved the line
          }
          this.MatchPair(startLine1, startCol1, endLine1, endCol1,startLine2, startCol2, endLine2, endCol2);
          break;
        }
      }
    }

    private void MatchPair( int startLine1, int startIdx1
      , int endLine1,   int endIdx1 
      , int startLine2, int startIdx2
      , int endLine2,   int endIdx2 ){

      bool foundLeftParen = (this.Line >= startLine1 && this.Line <= endLine1 &&
        (this.Line != startLine1 || this.Column >= startIdx1) &&
        (this.Line != endLine1 || this.Column <= endIdx1));
  
      bool foundRightParen= (this.Line >= startLine2 && this.Line <= endLine2 &&
        (this.Line != startLine2 || this.Column >= startIdx2) &&
        (this.Line != endLine2 || this.Column <= endIdx2));

      if (foundLeftParen || foundRightParen){
        this.FoundMatchingBrace = true;
    
        TextSpan spanLeftParen = new TextSpan();
        TextSpan spanRightParen = new TextSpan();

        spanLeftParen.iStartLine = startLine1;
        spanLeftParen.iEndLine   = endLine1;
        spanLeftParen.iStartIndex= startIdx1;
        spanLeftParen.iEndIndex  = endIdx1;

        spanRightParen.iStartLine = startLine2;
        spanRightParen.iEndLine   = endLine2;
        spanRightParen.iStartIndex= startIdx2;
        spanRightParen.iEndIndex  = endIdx2;

        TextSpan spanFound;
        TextSpan spanOther;
        if (foundRightParen){
          if (this.reason == ParseReason.MatchBraces){
            // For some reason we were given the span of the whole method declaration here
            // (probably because the same code is used for ReasonHighlightBraces where we
            // actually do want to highlight the whole method declaration).  So we need to 
            // scale this back to just the left paren when we're doing ReasonMatchBraces.
            spanLeftParen.iStartIndex = spanLeftParen.iEndIndex-1;
          }
          spanFound = spanRightParen; 
          spanOther = spanLeftParen;
        }
        else {
          spanFound = spanLeftParen;
          spanOther = spanRightParen;
        }

        //check which one is preferred in case of 2 touching braces
        if (this.Spans.Count > 0){
          TextSpan prevFound = (TextSpan)this.Spans[0];

          if (prevFound.iEndLine == spanFound.iStartLine  && prevFound.iEndIndex == spanFound.iStartIndex){
            //previous was left-most
            if (this.reason == ParseReason.HighlightBraces) 
              return; //favor leftmost
          }
          else if (prevFound.iStartLine == spanFound.iEndLine && prevFound.iStartIndex == spanFound.iEndIndex){
            //previous was right-most
            if (this.reason == ParseReason.MatchBraces) 
              return; //favor right most
          }
          else {
            // This happens normally when the user is typing in and hasn't closed
            // the parens yet, so it's not a debug assert.
            //Debug.Assert(false, "MatchPair: found, but not left or right??");
          }
        }

        this.Spans.Clear();
        this.Spans.Add(spanFound);
        this.Spans.Add(spanOther);        
      }     
    }
    
    /// <summary>
    /// Matching tripples are used to highlight in bold a completed statement.  For example
    /// when you type the closing brace on a foreach statement VS highlights in bold the statement
    /// that was closed.  The first two source contexts are the beginning and ending of the statement that
    /// opens the block (for example, the span of the "foreach(...){" and the third source context
    /// is the closing brace for the block (e.g., the "}").
    /// </summary>
    public virtual void MatchTriple( SourceContext startContext, SourceContext middleContext, SourceContext endContext ){
        
      switch(this.reason){
        case ParseReason.MatchBraces: 
        case ParseReason.HighlightBraces:{
          int startLine1 = startContext.StartLine;
          int endLine1 = startContext.EndLine;
          int startCol1 = startContext.StartColumn;
          int endCol1 = startContext.EndColumn;
          int startLine2 = middleContext.StartLine;
          int endLine2 = middleContext.EndLine;
          int startCol2 = middleContext.StartColumn;
          int endCol2 = middleContext.EndColumn;
          int startLine3 = endContext.StartLine;
          int endLine3 = endContext.EndLine;
          int startCol3 = endContext.StartColumn;
          int endCol3 = endContext.EndColumn;
          if (startLine1 < 0 || endLine1 < startLine1 || startCol1 < 0 || (endLine1 == startLine1 && endCol1 < startCol1) ||
            startLine2 < endLine1 || endLine2 < startLine2 || startCol2 < 0 || (endLine2 == startLine2 && endCol2 < startCol2) ||
            startLine3 < endLine2 || endLine3 < startLine3 || startCol3 < 0 || (endLine3 == startLine3 && endCol3 < startCol3)){
            Debug.Assert(false);
            return;
          }
          this.MatchTriple(startLine1, startCol1, endLine1, endCol1, 
            startLine2, startCol2, endLine2, endCol2,
            startLine3, startCol3, endLine3, endCol3);
          break;
        }
      }     
    }

    private void MatchTriple( int startLine1, int startIdx1, int endLine1, int endIdx1 
      , int startLine2, int startIdx2, int endLine2,   int endIdx2
      , int startLine3, int startIdx3, int endLine3,   int endIdx3 ){

      if (this.FoundMatchingBrace) return;

      bool foundLeftParen = (this.Line >= startLine1 && this.Line <= endLine1 &&
        (this.Line != startLine1 || this.Column >= startIdx1) &&
        (this.Line != endLine1 || this.Column <= endIdx1));
  
  
      bool foundMiddleParen=(this.Line >= startLine2 && this.Line <= endLine2 &&
        (this.Line != startLine2 || this.Column >= startIdx2) &&
        (this.Line != endLine2 || this.Column <= endIdx2));
  
      bool foundRightParen= (this.Line >= startLine3 && this.Line <= endLine3 &&
        (this.Line != startLine3 || this.Column >= startIdx3) &&
        (this.Line != endLine3 || this.Column <= endIdx3));

      this.FoundMatchingBrace = foundLeftParen || foundMiddleParen || foundRightParen;

      if (this.FoundMatchingBrace){
        TextSpan spanLeftParen, spanMiddleParen, spanRightParen;

        spanLeftParen.iStartLine = startLine1;
        spanLeftParen.iEndLine   = endLine1;
        spanLeftParen.iStartIndex= startIdx1;
        spanLeftParen.iEndIndex  = endIdx1;

        spanMiddleParen.iStartLine = startLine2;
        spanMiddleParen.iEndLine   = endLine2;
        spanMiddleParen.iStartIndex= startIdx2;
        spanMiddleParen.iEndIndex  = endIdx2;
    
        spanRightParen.iStartLine = startLine3;
        spanRightParen.iEndLine   = endLine3;
        spanRightParen.iStartIndex= startIdx3;
        spanRightParen.iEndIndex  = endIdx3;

        if (foundRightParen){
          this.Spans.Add(spanLeftParen);
          this.Spans.Add( spanMiddleParen );
          this.Spans.Add( spanRightParen );
        }
        else {
          this.Spans.Add( spanRightParen );
          this.Spans.Add( spanMiddleParen );
          this.Spans.Add( spanLeftParen );
        }
      }
    }


    /// <summary>
    /// In support of Member Selection, CompleteWord, QuickInfo, 
    /// MethodTip, and Autos, the StartName and QualifyName methods
    /// are called.
    /// StartName is called for each identifier that is parsed (e.g. "Console")
    /// </summary>
    public virtual void StartName(SourceContext context, string name){
      switch(this.reason){
        case ParseReason.MemberSelect:
        case ParseReason.CompleteWord:
        case ParseReason.QuickInfo:
        case ParseReason.MethodTip:
        case ParseReason.Autos:{
          int startLine = context.StartLine;
          int startCol = context.StartColumn;
          if (startLine < 0 || startCol < 0){
            Debug.Assert(false);
            return;
          }
          if (startLine < this.Line || (startLine == this.Line && startCol <= this.Column)){
            this.Names.Add(name);
            this.SourceLocations.Add(context);
          }
          break;
        }
      }
    }

    /// <summary>
    /// QualifyName is called for each qualification with both
    /// the text span of the selector (e.g. ".")  and the text span 
    /// of the name ("WriteLine").
    /// </summary>
    public virtual void QualifyName(SourceContext selectorContext, SourceContext nameContext, string name){
      switch(this.reason){
        case ParseReason.MemberSelect:
        case ParseReason.CompleteWord:
        case ParseReason.QuickInfo:
        case ParseReason.MethodTip:
        case ParseReason.Autos:{
          int startCol = nameContext.StartColumn;
          int startLine = nameContext.StartLine;
          if (startLine < 0 || startCol < 0){
            Debug.Assert(false);
            return;
          }
          if (startLine < this.Line || (startLine == this.Line && startCol < this.Column)){
            this.Names.Add(name);
            this.SourceLocations.Add(nameContext);
          }
          break;
        }
      }
    }

    /// <summary>
    /// AutoExpression is in support of IVsLanguageDebugInfo.GetProximityExpressions.
    /// It is called for each expression that might be interesting for
    /// a user in the "Auto Debugging" window. All names that are
    /// set using StartName and QualifyName are already automatically
    /// added to the "Auto" window! This means that AutoExpression
    /// is rarely used.
    /// </summary>   
    public virtual void AutoExpression( SourceContext expr ){
    }
    
    /// <summary>
    /// CodeSpan is in support of IVsLanguageDebugInfo.ValidateBreakpointLocation.
    /// It is called for each region that contains "executable" code.
    /// This is used to validate breakpoints. Comments are
    /// automatically taken care of based on TokenInfo returned from scanner. 
    /// Normally this method is called when a procedure is started/ended.
    /// </summary>
    public virtual void CodeSpan( SourceContext span ){
    }
    
    /// <summary>
    /// The StartParameters, Parameter and EndParameter methods are
    /// called in support of method tip intellisense (ECMD_PARAMINFO).
    /// [StartParameters] is called when the parameters of a method
    /// are started, ie. "(".
    /// [NextParameter] is called on the start of a new parameter, ie. ",".
    /// [EndParameter] is called on the end of the paramters, ie. ")".
    /// REVIEW: perhaps this entire scheme should go away
    /// </summary>
    public virtual void StartParameters(SourceContext context){
      switch(this.reason){
        case ParseReason.MethodTip:
        case ParseReason.QuickInfo:{
          int startLine = context.StartLine;
          int startCol = context.StartColumn;
          if (startLine< 0 || startCol < 0){
            Debug.Assert(false);
            return;
          }
          if (startLine < this.Line || (startLine == this.Line && startCol < this.Column)){
            this.MethodCalls.Push(this.Names, this.SourceLocations, false);
            this.Names = new StringCollection();
            this.SourceLocations = new ArrayList();
          }
          break;
        }
      }
    }
    
    /// <summary>
    /// NextParameter is called after StartParameters on the start of each new parameter, ie. ",".
    /// </summary>
    public virtual void NextParameter(SourceContext context){      
      switch(this.reason){
        case ParseReason.MethodTip:
        case ParseReason.QuickInfo:{
          int startLine = context.StartLine;
          int startCol = context.StartColumn;
          if (startLine < 0 || startCol < 0){
            Debug.Assert(false);
            return;
          }
          if (startLine < this.Line || startLine == this.Line && startCol < this.Column)
            this.MethodCalls.NextParameter();
          break;
        }
      }
    }
    
    /// <summary>
    /// EndParameter is called on the end of the paramters, ie. ")".
    /// </summary>
    public virtual void EndParameters(SourceContext context){      
      switch(this.reason){
        case ParseReason.MethodTip:
        case ParseReason.QuickInfo:{
          int startLine = context.StartLine;
          int startCol = context.StartColumn;
          if (startLine < 0 || startCol < 0){
            Debug.Assert(false);
            return;
          }
          if (startLine < this.Line || (startLine == this.Line && startCol < this.Column))
            this.MethodCalls.Pop();
          break;
        }
      }
    }

    public virtual void StartTemplateParameters(SourceContext context){
      switch(this.reason){
        case ParseReason.MethodTip:
        case ParseReason.QuickInfo:{
          int startLine = context.StartLine;
          int startCol = context.StartColumn;
          if (startLine< 0 || startCol < 0){
            Debug.Assert(false);
            return;
          }
          if (startLine < this.Line || (startLine == this.Line && startCol < this.Column)){ 
            this.MethodCalls.Push(this.Names, this.SourceLocations, true);
            this.Names = new StringCollection();
            this.SourceLocations = new ArrayList();
          }
          break;
        }
      }
    }
    
    public virtual void NextTemplateParameter(SourceContext context){      
      switch(this.reason){
        case ParseReason.MethodTip:
        case ParseReason.QuickInfo:{
          int startLine = context.StartLine;
          int startCol = context.StartColumn;
          if (startLine < 0 || startCol < 0){
            Debug.Assert(false);
            return;
          }
          if (startLine < this.Line || startLine == this.Line && startCol < this.Column)
            this.MethodCalls.NextParameter();
          break;
        }
      }
    }
    
    public virtual void EndTemplateParameters(SourceContext context){
      switch(this.reason){
        case ParseReason.MethodTip:
        case ParseReason.QuickInfo:{
          int startLine = context.StartLine;
          int startCol = context.StartColumn;
          if (startLine < 0 || startCol < 0){
            Debug.Assert(false);
            return;
          }
          if (startLine < this.Line || (startLine == this.Line && startCol < this.Column)){
            CallInfo call = this.MethodCalls.GetCurrentMethodCall();
            if (call != null){
              this.Names = call.names;
              this.SourceLocations = call.sourceLocations;
            }
            this.MethodCalls.Pop();
          }
          break;
        }
      }
    }

    /// <summary>
    /// Send a message to the VS enviroment. The kind of message
    /// is specified through the given severity. 
    /// </summary>
    public virtual void AddError(ErrorNode node){

//      if (this.reason != ParseReason.Check && this.reason != ParseReason.Compile) 
      if (this.reason != ParseReason.Check) 
        return;

      SourceContext ctx = node.SourceContext;

      if (ctx.StartLine < 0 || ctx.EndLine < ctx.StartLine || ctx.StartColumn < 0 || 
        (ctx.EndLine == ctx.StartLine && ctx.EndColumn < ctx.StartColumn)){
        Debug.Assert(false);
        return;
      }

      this.Errors.Add(node);
    }

  }; // AuthoringSink



}
