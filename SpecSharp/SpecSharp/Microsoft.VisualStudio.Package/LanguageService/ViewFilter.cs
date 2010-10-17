//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using VsShellInterop = Microsoft.VisualStudio.Shell.Interop;
using IServiceProvider = System.IServiceProvider;

namespace Microsoft.VisualStudio.Package{
  //========================================================================
  public class ViewFilter : IVsTextViewFilter, IVsTextViewEvents, IOleCommandTarget{
    private uint cookie;
    private Guid IID_IVsTextViewEvents;
    protected LanguageService service;
    protected IVsTextView textView;
    protected IOleCommandTarget nextTarget;
    protected TextTipData textTipData;
    public static IVsTextView LastActiveTextView;
    public Source source;

    // the current dataTipText info...
    private int quickInfoLine;
    private int quickInfoIdx;
    private TextSpan quickInfoSpan;
    private string quickInfoText;

    public ViewFilter(LanguageService service, Source source, IVsTextView view){
      this.service = service;
      this.source = source;
      this.textView = view;     
      view.AddCommandFilter(this, out nextTarget);
      this.IID_IVsTextViewEvents = typeof(IVsTextViewEvents).GUID;
      this.cookie = VsShell.Connect(view, this, ref IID_IVsTextViewEvents);
    }
    
    public virtual void Close(){
      if (cookie!=0){
        VsShell.DisConnect(this.textView, ref IID_IVsTextViewEvents, this.cookie);
        cookie = 0;
      }
      if (textView == LastActiveTextView)
        LastActiveTextView = null;
      textView.RemoveCommandFilter(this);    
  
      if (textTipData != null)
        textTipData.Close(textView);
    }
    
    #region IVsTextViewFilter methods
    public virtual int GetWordExtent(int line, int index, uint flags,  TextSpan[] span){      
      Debug.Assert(line>=0 && index >=0);
      if (span == null) return (int)HResult.E_INVALIDARG;
      else span[0] = new TextSpan();

      span[0].iStartLine  = span[0].iEndLine = line;
      span[0].iStartIndex = index;
      int start, end;      
      if (!this.source.GetWordExtent(line, index, (WORDEXTFLAGS)flags, out start, out end)){
        return (int)HResult.S_FALSE;
      }
      span[0].iStartIndex = start;
      span[0].iEndIndex = end;
      return 0;
    }

    public virtual int GetDataTipText(TextSpan[] aspan, out string text){
      
      text = null;
      TextSpan span = aspan[0];

      if (!service.Preferences.EnableQuickInfo){ 
        return (int)HResult.E_FAIL; 
      }
      
      if (span.iEndLine == this.quickInfoLine && span.iEndIndex == this.quickInfoIdx){
        if (this.quickInfoText == null){
          //Check if the span has error information
          this.quickInfoText = this.source.GetTaskProvider().GetDataTipText(span, ref this.quickInfoSpan);
          if (this.quickInfoText == null){
            // still parsing on the background thread, so return E_PENDING.
            return (int)HResult.E_PENDING;
          }
        }
        this.quickInfoLine = -1;
        if (this.quickInfoText == ""){ 
          // then the parser found nothing to display.
          return (int)HResult.E_FAIL; 
        }
        text = this.GetFullDataTipText(this.quickInfoText, span);
        aspan[0] = this.quickInfoSpan;
        this.quickInfoText = null;
      }else{
        TextSpan errorSpan = span;
        text = this.source.GetTaskProvider().GetDataTipText(span, ref errorSpan);
        if (text != null){
          aspan[0] = errorSpan;
          return 0;
        }
        // kick off the background parse to get this information...
        this.quickInfoText = null;
        this.quickInfoLine = span.iEndLine;
        this.quickInfoIdx = span.iEndIndex;
        this.source.BeginParse(span.iEndLine, span.iEndIndex, new TokenInfo(), ParseReason.QuickInfo, this.textView, new ParseResultHandler(HandleQuickInfoResponse));
        return (int)HResult.E_PENDING;        
      }
      return (int)TipSuccesses.TIP_S_ONLYIFNOMARKER;
    }

    public virtual int GetPairExtents(int line, int index, TextSpan[] span){
      Debug.Assert(line>=0 && index >=0);
      if (span == null) return (int)HResult.E_INVALIDARG;      
      this.source.GetPairExtents(line, index, out span[0]);
      return 0;
    }
    #endregion

    #region IVsTextViewEvents methods
    public virtual void OnChangeCaretLine(IVsTextView view, int line, int col){
    }
    public virtual void OnChangeScrollInfo(IVsTextView view, int iBar, int iMinUnit, int iMaxUnits, int iVisibleUnits, int iFirstVisibleUnit){
    }
    public virtual void OnKillFocus(IVsTextView view){
    }
    public virtual void OnSetBuffer(IVsTextView view, IVsTextLines buffer){
    }
    public virtual void OnSetFocus(IVsTextView view){
      // just in case another file we were dependent on has changed while we were
      // inactive, this will force us to sync up.
      //this.source.SetDirty();
    }
    #endregion

    internal void HandleQuickInfoResponse(ParseRequest req){
      if (req.Line == this.quickInfoLine && req.Col == this.quickInfoIdx){
        this.quickInfoText = req.Scope.GetDataTipText(req.Line, req.Col, out this.quickInfoSpan);
      }
    }

    /// <summary>
    /// Override this method to intercept the IOleCommandTarget::QueryStatus call.
    /// </summary>
    /// <param name="guidCmdGroup"></param>
    /// <param name="cmd"></param>
    /// <returns>Usually returns OLECMDF_ENABLED  | OLECMDF_SUPPORTED
    /// or return OLECMDERR_E_UNKNOWNGROUP if you don't handle this command
    /// </returns>
    protected virtual int QueryCommandStatus(ref Guid guidCmdGroup, uint nCmdId){
      if (guidCmdGroup == VsConstants.guidStandardCommandSet97) 
    {
        VsCommands cmd = (VsCommands)nCmdId;
        switch (cmd)
      {
          case VsCommands.GotoDefn:
          case VsCommands.GotoDecl:
          case VsCommands.GotoRef:
            return (int)OLECMDF.OLECMDF_SUPPORTED | (int)OLECMDF.OLECMDF_ENABLED;
        }
      }
      else if (guidCmdGroup == VsConstants.guidStandardCommandSet2K) 
    {
        VsCommands2K cmd = (VsCommands2K)nCmdId;
        switch (cmd) 
      {
          case VsCommands2K.COMMENT_BLOCK:
          case VsCommands2K.UNCOMMENT_BLOCK:
            if (this.source == null || !this.source.CommentSupported) break;
            return (int)OLECMDF.OLECMDF_SUPPORTED | (int)OLECMDF.OLECMDF_ENABLED;

          case VsCommands2K.SHOWMEMBERLIST:
          case VsCommands2K.COMPLETEWORD:
          case VsCommands2K.PARAMINFO:
            return (int)OLECMDF.OLECMDF_SUPPORTED | (int)OLECMDF.OLECMDF_ENABLED;

          case VsCommands2K.QUICKINFO:
            if (this.service.Preferences.EnableQuickInfo) 
          {
              return (int)OLECMDF.OLECMDF_SUPPORTED | (int)OLECMDF.OLECMDF_ENABLED;
            }
            break;

          case VsCommands2K.HANDLEIMEMESSAGE:
            return 0;
        }
      }
      unchecked{ return (int)OleDocumentError.OLECMDERR_E_UNKNOWNGROUP; }
    }
    /// <summary>
    /// Override this method to intercept the IOleCommandTarget::Exec call.
    /// </summary>
    /// <returns>Usually returns 0 if ok, or OLECMDERR_E_NOTSUPPORTED</returns>
    protected virtual int ExecCommand(ref Guid guidCmdGroup, uint nCmdId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut){

      if (guidCmdGroup == VsConstants.guidStandardCommandSet97){
        VsCommands cmd = (VsCommands)nCmdId;
        switch (cmd){
          case VsCommands.GotoDefn:
          case VsCommands.GotoDecl:
          case VsCommands.GotoRef:
            HandleGoto(cmd);
            return 0;
        }
      }else if (guidCmdGroup == VsConstants.guidStandardCommandSet2K){
        VsCommands2K cmd = (VsCommands2K)nCmdId;
        switch (cmd){

          case VsCommands2K.COMMENT_BLOCK:
            this.source.CommentSelection(this.textView);
            return 0;

          case VsCommands2K.UNCOMMENT_BLOCK:
            this.source.UnCommentSelection(this.textView);
            return 0;
    
          case VsCommands2K.COMPLETEWORD:{
            int line;
            int idx;
            this.source.Completion(this.textView, this.source.GetTokenInfo(this.textView, out line, out idx), line, idx, true);
            return 0;
          }

          case VsCommands2K.SHOWMEMBERLIST:{
            int line;
            int idx;
            this.source.Completion(this.textView, this.source.GetTokenInfo(this.textView, out line, out idx), line, idx, false);
            return 0;
          }

          case VsCommands2K.PARAMINFO:{ 
            int line;
            int idx;
            this.source.MethodTip(this.textView, this.source.GetTokenInfo(this.textView, out line, out idx), line, idx);
            return 0;
          }
          case VsCommands2K.QUICKINFO:{
            HandleQuickInfo();
            return 0;
          }
          case VsCommands2K.SHOWCONTEXTMENU:
            this.service.ShowContextMenu(VsConstants.IDM_VS_CTXT_CODEWIN, VsConstants.guidSHLMainMenu);
            return 0;

          case VsCommands2K.HANDLEIMEMESSAGE:
            if (pvaOut != IntPtr.Zero){
              Marshal.GetNativeVariantForObject(false, pvaOut); //debug this make sure it's right ...
            }
            return this.nextTarget.Exec(ref guidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut);      

          case VsCommands2K.BACKSPACE:
          case VsCommands2K.BACKTAB:
          case VsCommands2K.LEFT:
          case VsCommands2K.LEFT_EXT:{
            // check method data to see if we need to AdjustCurrentParameter appropriately.
            this.source.OnCommand(this.textView, cmd, true);
            int rc = this.nextTarget.Exec(ref guidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut); 
            return rc;
          }

          case VsCommands2K.TYPECHAR:
          default:{
            // check general trigger characters for intellisense, but insert the new char into
            // the text buffer first.
            int rc = this.nextTarget.Exec(ref guidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut); 
            this.source.OnCommand(this.textView, cmd, false);
            return rc;          
          }

        }        
      }
      unchecked{ return (int)OleDocumentError.OLECMDERR_E_NOTSUPPORTED; }
    }


    #region IOleCommandTarget methods
    int IOleCommandTarget.QueryStatus(ref Guid guidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {

      LastActiveTextView = this.textView;

      for (uint i = 0; i < cCmds; i++) {
        int rc = QueryCommandStatus(ref guidCmdGroup, (uint)prgCmds[i].cmdID);
        if (rc<0) {
          if (nextTarget != null) {
            try {
              return this.nextTarget.QueryStatus(ref guidCmdGroup, cCmds, prgCmds, pCmdText);
            } catch {
            }
          }
          return rc;
        }
        prgCmds[i].cmdf = (uint)rc; 
      }
      return 0;
    }

    int IOleCommandTarget.Exec(ref Guid guidCmdGroup, uint nCmdId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {      
      // On every command, update the tip window if it's active.
      if (this.textTipData != null && this.textTipData.IsActive())
        textTipData.CheckCaretPosition(this.textView);

      int rc = ExecCommand(ref guidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut);
      if (rc < 0 && nextTarget != null) {
        try {
          return this.nextTarget.Exec(ref guidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut);
        } catch{
          return rc;
        }
      }
      return rc;
    }
    #endregion

    public virtual void HandleQuickInfo(){
      TextSpan    ts = new TextSpan();

      // Get the caret position
      this.textView.GetCaretPos(out ts.iStartLine, out ts.iStartIndex);
      ts.iEndLine  = ts.iStartLine;
      ts.iEndIndex = ts.iStartIndex;

      // Get the tip text at that location. 
      // Do this synchronously since the user specifically requested this one...
      string text = this.source.OnSyncQuickInfo(this.textView, ts.iEndLine, ts.iEndIndex);

      if (text == null){ // nothing to show
        return;
      }

      string fullText = this.GetFullDataTipText(text, ts);
      if (fullText == null){
        return;
      }

      int iPos, iPosEnd, iSpace, iLength;

      // Calculate the stream position
      textView.GetNearestPosition(ts.iStartLine, ts.iStartIndex, out iPos, out iSpace);
      textView.GetNearestPosition (ts.iEndLine, ts.iEndIndex, out iPosEnd, out iSpace);
      iLength = Math.Max(iPosEnd - iPos, 1);

      // Tear down the method tip if it's there
      this.source.DismissMethodTip();

      // Update the text tip window
      TextTipData textTipData = this.GetTextTipData();
      textTipData.Update(fullText, iPos, iLength, this.textView);

    }

    public virtual string GetFullDataTipText(string text, TextSpan ts){

      IVsTextLines textLines;
      this.textView.GetBuffer(out textLines);

      // Now, check if the debugger is running and has anything to offer
      string debugDataTip = null;

      try{
        Microsoft.VisualStudio.Shell.Interop.IVsDebugger debugger = this.service.GetIVsDebugger();
        if (debugger != null){
          TextSpan[] tsdeb = new TextSpan[1]{ ts };
          bool selection = ( (ts.iStartLine  != ts.iEndLine) || (ts.iStartIndex != ts.iEndIndex));
          if (!selection){
            // The debugger can't determine the current word by itself. 
            // Do it for them...
            textView.GetWordExtent(ts.iStartLine, ts.iStartIndex, (uint)WORDEXTFLAGS.WORDEXT_FINDWORD | (uint)WORDEXTFLAGS.WORDEXT_CURRENT, tsdeb);
          }
          debugger.GetDataTipValue(textLines, tsdeb, null, out debugDataTip);
        }   
      } catch (COMException){
      }

      if (debugDataTip == null || debugDataTip == ""){
        return text ;
      }

      int i = debugDataTip.IndexOf('=');
      if (i < 0){
        return text;
      }else{
        string spacer = (i < debugDataTip.Length-1 && debugDataTip[i+1] == ' ') ? " " : "";
        return text + spacer + debugDataTip.Substring(i);
      }
    }

    public virtual TextTipData GetTextTipData(){
      if (this.textTipData != null)
        return textTipData;

      // create it 
      this.textTipData = new TextTipData(this.service.site);      
      return this.textTipData;
    }

    public virtual void HandleGoto(VsCommands cmd){
      TextSpan    ts = new TextSpan();

      // Get the caret position
      this.textView.GetCaretPos(out ts.iStartLine, out ts.iStartIndex);
      ts.iEndLine  = ts.iStartLine;
      ts.iEndIndex = ts.iStartIndex;

      // Get the tip text at that location. 
      // Wait, since the user specifically requested this one...
      TextSpan span;
      string url = this.source.OnSyncGoto(cmd, this.textView, ts.iEndLine, ts.iEndIndex, out span);

      if (url == null || url.Trim() == ""){ // nothing to show
        return;
      }

      // Open the referenced document, and scroll to the given location.
      VsShellInterop.IVsUIHierarchy hierarchy;
      uint[] itemID = new uint[1];
      VsShellInterop.IVsWindowFrame frame;
      IVsTextView view;
      VsShell.OpenDocument(this.service.site, url, VsConstants.CLSID_VsCodeWindow, out hierarchy, itemID, out frame, out view);
      if (view != null){   
        view.EnsureSpanVisible(span);
        view.SetSelection(span.iStartLine, span.iStartIndex, span.iEndLine, span.iEndIndex);
      }
    }

  }

  public class TextTipData : IVsTextTipData{

    IVsTextTipWindow   textTipWindow;
    int pos;
    int len;
    string text;
    bool isWindowUp;

    public TextTipData(IServiceProvider site){
      Debug.Assert(site != null);

      //this.textView = view;
      // Create our method tip window (through the local registry)
      this.textTipWindow = (IVsTextTipWindow)VsShell.CreateInstance(site, ref VsConstants.CLSID_VsTextTipWindow, ref VsConstants.IID_IVsTextTipWindow, typeof(IVsTextTipWindow));
      if (this.textTipWindow == null)
        NativeHelpers.RaiseComError(HResult.E_FAIL);

      textTipWindow.SetTextTipData(this);
    }

    public void Close(IVsTextView textView){
      if (this.isWindowUp)
        textView.UpdateTipWindow(this.textTipWindow, (uint)TipWindowFlags.UTW_DISMISS);
      this.textTipWindow = null;
    }

    public bool IsActive(){ return this.isWindowUp; }

    public void Update(string text, int pos, int len, IVsTextView textView){
      if (textView == null) return;
      this.pos = pos;
      this.len = len;
      this.text = text;
      if (text == "" || text == null) 
        NativeHelpers.RaiseComError(HResult.E_FAIL);     

      textView.UpdateTipWindow(textTipWindow, (uint)TipWindowFlags.UTW_CONTEXTCHANGED | (uint)TipWindowFlags.UTW_CONTENTCHANGED);
      this.isWindowUp = true;
    }

    public void CheckCaretPosition (IVsTextView textView){
      if (textView == null) return;
      int line, col, pos, space;
      textView.GetCaretPos (out line, out col);
      textView.GetNearestPosition(line, col, out pos, out space);
      if (pos < this.pos || pos > this.pos + this.len){
        textView.UpdateTipWindow (this.textTipWindow, (uint)TipWindowFlags.UTW_DISMISS);
      }
    }


    ////////////////////////////////////////////////////////////////////////////////
    #region IVsTextTipData
    public virtual int GetTipText(string[] pbstrText, out int pfFontData){
      pfFontData = 0;
      if (pbstrText == null || pbstrText.Length==0)
        return (int)HResult.E_INVALIDARG;
      pfFontData = 0; // TODO: Do whatever formatting we might want...
      pbstrText[0] = this.text;
      return 0;
    }

    public virtual int GetTipFontInfo(int iChars, uint[] pdwFontInfo){
      return (int)HResult.E_NOTIMPL;
    }

    public virtual int GetContextStream(out int piPos, out int piLen){
      piPos = this.pos;
      piLen = this.len;
      return 0;
    }

    public virtual void OnDismiss (){
      this.isWindowUp = false;
    }

    public virtual void UpdateView (){
    }
    #endregion
  }
}
