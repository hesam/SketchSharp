//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
#if WHIDBEY
using Microsoft.VisualStudio.CodeTools;
#endif

using VsShellInterop = Microsoft.VisualStudio.Shell.Interop; // because it contains redundant TextSpan definition.

namespace Microsoft.VisualStudio.Package{

  public enum Severity{
    SevHint,
    SevWarning,
    SevError,
    SevFatal
  };


  public enum ScopeKind{
    ScopeUnknown = 0,
    ScopeModule,
    ScopeClass,
    ScopeInterface,
    ScopeUnion,

    ScopeProcedure,
    ScopeVariable,

    ScopeBlock
  };

  /*---------------------------------------------------------
  ScopeAccess:
  ---------------------------------------------------------*/
  public enum ScopeAccess{
    AccessPrivate = 0,
    AccessProtected,
    AccessPublic
  };

  /*---------------------------------------------------------
  ScopeStorage:
    Together with a [ScopeKind] this determines the object.
    For example, a method has a kind [ScopeProcedure] and
    a storage [StorageMember]. 
  ---------------------------------------------------------*/
  public enum ScopeStorage{
    StorageConstant = 0,
    StorageStatic,
    StorageMember,
    StorageVirtual,
    StorageParameter,
    StorageResult,
    StorageLocal,
    StorageType,
    StorageNone,
    StorageOther
  };

  /*---------------------------------------------------------
  Icons:
    There are 180 default icons available. 
    There are 30 groups of 6 icons. The first 27 groups
    have 6 variants of a certain icon. For example, the
    first group [IconGroupClass] has a class icon shown
    as normal [IconItemNormal], with a little key [IconItemProtected],
    with a lock [IconItemPrivate] etc.
    These icons can be indexed as: [IconGroupXXX * IconGroupSize + IconItemXXX]
    The last 3 groups consist of 18 random icons which have
    their indices in the [ScopeIcon] enumerations.

    You can use your own icons by implementing GetImageList
  ---------------------------------------------------------*/
  //const int IconGroupSize = 6;

  public enum ScopeIconGroup{
    IconGroupClass = 0,
    IconGroupType,
    IconGroupDelegate,
    IconGroupEnum,
    IconGroupEnumConst,
    IconGroupEvent,
    IconGroupResource,
    IconGroupFieldBlue,
    IconGroupInterface,
    IconGroupTextLine,
    IconGroupScript,
    IconGroupScript2,
    IconGroupMethod,
    IconGroupMethod2,
    IconGroupDiagram,
    IconGroupNameSpace,
    IconGroupFormula,
    IconGroupProperty,
    IconGroupStruct,
    IconGroupTemplate,
    IconGroupOpenSquare,
    IconGroupBits,
    IconGroupChannel,
    IconGroupFieldRed,
    IconGroupUnion,
    IconGroupForm,
    IconGroupFieldYellow,
    IconGroupMisc1,
    IconGroupMisc2,
    IconGroupMisc3
  };

  public enum ScopeIconItem{
    IconItemPublic,
    IconItemInternal,
    IconItemSpecial,
    IconItemProtected,
    IconItemPrivate,
    IconItemShortCut,
    IconItemNormal  = IconItemPublic
  };

  public enum ScopeIconMisc{
    IconBlackBox = 162,   /* (IconGroupMisc1 * IconGroupSize) */
    IconLibrary,
    IconProgram,
    IconWebProgram,
    IconProgramEmpty,
    IconWebProgramEmpty,

    IconComponents,
    IconEnvironment,
    IconWindow,
    IconFolderOpen,
    IconFolder,
    IconArrowRight,

    IconAmbigious,
    IconShadowClass,
    IconShadowMethodPrivate,
    IconShadowMethodProtected,
    IconShadowMethod,
    IconInCompleteSource
  };


  public class CommentInfo{
    public bool supported;
    public string lineStart;
    public string blockStart;
    public string blockEnd;
    public bool useLineComments;
  }

  //===================================================================================
  // Default Implementations
  //===================================================================================


  /// <summary>
  /// Source represents one source file and manages the parsing and intellisense on this file
  /// and keeping things like the drop down combos in sync with the source and so on.
  /// </summary>
  public class Source : IVsTextLinesEvents, IVsFinalTextChangeCommitEvents{
    protected LanguageService service;
    protected IVsTextLines textLines;
    protected Colorizer colorizer;
    protected TaskProvider taskProvider;
    protected CompletionSet completionSet;
    protected int dirtyTime;
    protected bool dirty;
    protected MethodData methodData;
    protected VsShellInterop.IVsStatusbar statusBar;
    protected CommentInfo commentInfo;
    protected uint textChangeCommitEventsCookie;
    protected uint textLinesEventsCookie;
    protected IVsTextColorState colorState;
#if WHIDBEY
    private ITaskManager taskManager;
#endif
    public Source(LanguageService service, IVsTextLines textLines, Colorizer colorizer){
      this.service = service;
      this.textLines = textLines;
      this.colorizer = colorizer;
      this.taskProvider = new TaskProvider(service.site); // task list
      this.completionSet = this.GetCompletionSet();      
      this.methodData = this.GetMethodData();
      this.colorState = (IVsTextColorState)textLines;

      Guid statusBarGuid = typeof(VsShellInterop.IVsStatusbar).GUID;
      this.statusBar = (VsShellInterop.IVsStatusbar)service.site.QueryService(statusBarGuid, typeof(VsShellInterop.IVsStatusbar));
      
      this.commentInfo = new CommentInfo();
      service.GetCommentFormat(this.commentInfo);

      // track source changes
      if (service.Preferences.EnableCodeSenseFastOnLineChange){
        textChangeCommitEventsCookie = VsShell.Connect(textLines, (IVsFinalTextChangeCommitEvents)this, ref VsConstants.IID_IVsFinalTextChangeCommitEvents);
      }
      this.textLinesEventsCookie = VsShell.Connect(textLines, (IVsTextLinesEvents)this, ref VsConstants.IID_IVsTextLinesEvents);
      this.SetDirty();

#if WHIDBEY
      // create a task manager
      if (taskManager == null) {
        ITaskManagerFactory taskManagerFactory = (ITaskManagerFactory)(service.site).GetService(typeof(ITaskManagerFactory));
        if (taskManagerFactory != null) {
          taskManager = taskManagerFactory.QuerySharedTaskManager("SpecSharp", true);
        }
      }
#endif
    }

    public bool CommentSupported{
      get{ return this.commentInfo.supported; }
    }

    public Colorizer GetColorizer(){
      return this.colorizer;
    }

    public virtual CompletionSet GetCompletionSet(){
      return new CompletionSet(this.service.GetImageList(), this);
    }

    public virtual MethodData GetMethodData(){
      MethodData mdata = new MethodData(this.service.site);
      string typeStart, typeEnd; bool typePrefixed;
      this.service.GetMethodFormat(out typeStart, out typeEnd, out typePrefixed);
      mdata.SetMethodFormat(typeStart, typeEnd, typePrefixed);
      return mdata;
    }


    public TaskProvider GetTaskProvider(){
      return this.taskProvider;
    }

    public void Close(){
      if (this.textLinesEventsCookie != 0) 
        VsShell.DisConnect(this.textLines, ref VsConstants.IID_IVsTextLinesEvents, this.textLinesEventsCookie );
      if (this.textChangeCommitEventsCookie != 0) 
        VsShell.DisConnect(this.textLines, ref VsConstants.IID_IVsFinalTextChangeCommitEvents, this.textChangeCommitEventsCookie );

#if WHIDBEY
      // release the task manager
      if (this.taskManager != null) {
        ITaskManagerFactory taskManagerFactory = (ITaskManagerFactory)service.site.GetService(typeof(ITaskManagerFactory));
        if (taskManagerFactory != null) {
          taskManagerFactory.ReleaseSharedTaskManager(this.taskManager);
        }
      }
      this.taskManager = null;
#endif

      this.statusBar = null;
      this.methodData.Close();
      this.methodData = null;
      this.completionSet.Close();
      this.completionSet = null;
      this.taskProvider.Dispose();
      this.taskProvider = null;
      this.service = null;
      this.colorizer = null;      
    }

    public IVsTextLines GetTextLines(){
      return this.textLines;
    }

    public void SetDirty(){
      this.dirty = true;
      this.dirtyTime = System.Environment.TickCount;
    }

    // IVsFinalTextChangeCommitEvents
    public virtual void OnChangesCommitted(uint reason, TextSpan[] changedArea){      
      SetDirty();
    }

    // IVsTextLinesEvents
    public virtual void OnChangeLineText(TextLineChange[] lineChange, int last){
      SetDirty();
    }

    public virtual void OnChangeLineAttributes(int firstLine, int lastLine){
    }

    //===================================================================================
    // Helper methods:
    //===================================================================================   
    public string GetFilePath(){
      return VsShell.GetFilePath(this.textLines);
    }

    public string GetTextUpToLine(int line){

      int lastLine;
      this.textLines.GetLineCount(out lastLine );
      lastLine--;

      if (line > 0) lastLine = Math.Min(line, lastLine );
    
      int lastIdx;
      this.textLines.GetLengthOfLine(lastLine, out lastIdx );

      string text;
      this.textLines.GetLineText(0, 0, lastLine, lastIdx, out text );

      return text;
    }

    // helper methods.
    public TaskItem CreateErrorTaskItem(TextSpan span, string message, Severity severity)
    {
      bool codeSense = true;  // should be a parameter
#if WHIDBEY
      const MARKERTYPE MARKER_COMPILE_WARNING = (MARKERTYPE)11;
#else
      const MARKERTYPE MARKER_COMPILE_WARNING = MARKERTYPE.MARKER_CODESENSE_ERROR;
#endif

      //normalize text span
      TextSpanHelper.TextSpanNormalize(ref span, textLines);

      //remove control characters
      StringBuilder sb = new StringBuilder();
      for (int i = 0, n = message.Length; i<n; i++){
        char ch = message[i];
        sb.Append(System.Convert.ToInt32(ch) < 0x20 ? ' ' : ch);
      }
      message = sb.ToString();

      //set options
      VsShellInterop.VSTASKPRIORITY priority;
      VsShellInterop.VSTASKCATEGORY category;
      VsShellInterop._vstaskbitmap bitmap;
      MARKERTYPE markerType;

      if (severity == Severity.SevHint) {
        priority = VsShellInterop.VSTASKPRIORITY.TP_NORMAL;
        bitmap = VsShellInterop._vstaskbitmap.BMP_COMMENT;
        markerType = MARKERTYPE.MARKER_INVISIBLE;
        category = VsShellInterop.VSTASKCATEGORY.CAT_COMMENTS;
        if (this.taskProvider != null) {
          if (!taskProvider.IsTaskToken(message, out priority))
            return null;
        }
      }
      else {
        if (codeSense)
          category = VsShellInterop.VSTASKCATEGORY.CAT_CODESENSE;
        else
          category = VsShellInterop.VSTASKCATEGORY.CAT_BUILDCOMPILE;

        if (severity == Severity.SevFatal)
          priority = VsShellInterop.VSTASKPRIORITY.TP_HIGH;
        else
          priority = VsShellInterop.VSTASKPRIORITY.TP_NORMAL;

        if (severity == Severity.SevWarning) 
          bitmap = VsShellInterop._vstaskbitmap.BMP_COMPILE;
        else
          bitmap = VsShellInterop._vstaskbitmap.BMP_SQUIGGLE;

        if (severity == Severity.SevWarning)
          markerType = MARKER_COMPILE_WARNING;
        else if (codeSense) 
          markerType = MARKERTYPE.MARKER_CODESENSE_ERROR;
        else 
          markerType = MARKERTYPE.MARKER_COMPILE_ERROR;        
      }

      // create marker so task item navigation works even after file is edited.
      IVsTextLineMarker textLineMarker = TextMarkerClient.CreateMarker(textLines, span, markerType, message);

      string fileName = this.GetFilePath();
      // create task item
      TaskItem taskItem = new TaskItem(this.service.site, textLineMarker, fileName, message, true, category, priority, bitmap, null, severity );
      return taskItem;
    }

    public int GetTokenInfoAt(TokenInfo[] infos, int col, ref TokenInfo info){
      for (int i = 0, len = infos.Length; i < len; i++){
        int start = infos[i].startIndex;
        int end = infos[i].endIndex;
        if (i == 0 && start > col)
          return -1;
        if (col >= start && col <= end){
          info = infos[i];
          return i;
        }
      }
      return -1;
    }

    public virtual void DismissMethodTip(){
      if (this.methodData.IsDisplayed){
        this.methodData.Dismiss();
      }
    }

    public bool IsCompletorActive{
      get{
        return this.methodData.IsDisplayed || this.completionSet.IsDisplayed;
      }
    }

    public virtual void OnIdle(bool periodic){
      // Kick of a background parse, but only in the periodic intervals and only if the source has been edited
      if (!periodic || !this.dirty){      
        return; 
      }
      // Son't kick off a background parse, while the user is typing.
      // this.dirtyTime moves with every keystroke.

      int msec = System.Environment.TickCount;
      if ((msec < this.dirtyTime) ||                                    //overflow
        (msec > this.dirtyTime + this.service.Preferences.CodeSenseDelay)){ //at least X secs have passed
        if (!IsCompletorActive && this.dirty){
          this.BeginParse(0, 0, new TokenInfo(), ParseReason.Check, null, new ParseResultHandler(this.HandleParseResponse));
          this.dirty = false;
        }
      }
    }

    public TokenInfo GetTokenInfo(IVsTextView textView, out int line, out int idx){
      //get current line 
      textView.GetCaretPos(out line, out idx);
      
      TokenInfo info = new TokenInfo();

      //get line info
      TokenInfo[] lineInfo = this.colorizer.GetLineInfo(line, this.colorState);
      if (lineInfo != null){
        //get character info      
        GetTokenInfoAt(lineInfo, idx - 1, ref info);
      }
      return info;
    }

    public virtual void OnCommand(IVsTextView textView, VsCommands2K command, bool backward){
      if (textView == null) return;
      int line, column;
      TokenInfo tokenInfo = this.GetTokenInfo(textView, out line, out column);
      if (backward && column > 0) column--;
      TokenTrigger triggerClass = tokenInfo.trigger;
      if ((triggerClass&TokenTrigger.MemberSelect) != 0 && (command == VsCommands2K.TYPECHAR) && this.service.Preferences.AutoListMembers){
        this.Completion(textView, tokenInfo, line, column, false);
        return;
      }
      if ((triggerClass&TokenTrigger.MatchBraces) != 0 && this.service.Preferences.EnableMatchBraces){
        if ((command != VsCommands2K.BACKSPACE) && ((command == VsCommands2K.TYPECHAR) || this.service.Preferences.EnableMatchBracesAtCaret)){
          this.MatchBraces(textView, line, column, tokenInfo);
        }
      }
      if ((triggerClass&TokenTrigger.MethodTip) != 0 && (command == VsCommands2K.TYPECHAR) && this.service.Preferences.ParameterInformation){
        this.MethodTip(textView, tokenInfo, line, column);
        return;
      }
      if (this.methodData.IsDisplayed && CommandOneOnLine(command)){
        this.MethodTip(textView, tokenInfo, line, column);
      }
    }

    internal static bool CommandOneOnLine(VsCommands2K command){
      switch (command){
        case VsCommands2K.TYPECHAR:
        case VsCommands2K.BACKSPACE:
        case VsCommands2K.TAB:
        case VsCommands2K.BACKTAB:
        case VsCommands2K.DELETE:
        case VsCommands2K.LEFT:
        case VsCommands2K.LEFT_EXT:
        case VsCommands2K.RIGHT:
        case VsCommands2K.RIGHT_EXT:
          return true;
        default:
          return false;
      }
    }

    public bool GetWordExtent(int line, int idx, WORDEXTFLAGS flags, out int startIdx, out int endIdx){
      Debug.Assert(line >=0 && idx >= 0);

      startIdx = endIdx = 0;

      //get the character classes
      TokenInfo[] lineInfo = this.colorizer.GetLineInfo(line, this.colorState);
      if (lineInfo == null) return false;

      int count = lineInfo.Length;

      TokenInfo info = new TokenInfo();
      int index = this.GetTokenInfoAt(lineInfo, idx, ref info);
      if (index<0) return false;

      //don't do anything in comment or text or literal space
      if (info.type == TokenType.Comment || 
        info.type == TokenType.LineComment ||
        info.type == TokenType.Embedded || 
        info.type == TokenType.Text || 
        info.type == TokenType.String || 
        info.type == TokenType.Literal )
        return false;

      //search for a token
      switch (flags & WORDEXTFLAGS.WORDEXT_MOVETYPE_MASK){
        case WORDEXTFLAGS.WORDEXT_PREVIOUS:
          index--;
          while (index >= 0 && ! MatchToken(flags, lineInfo[index])) index--;
          if (idx < 0) return false;
          break;

        case WORDEXTFLAGS.WORDEXT_NEXT:
          idx++;
          while (index < count && !MatchToken(flags,lineInfo[index])) index++;
          if (index >= count) return false;
          break;

        case WORDEXTFLAGS.WORDEXT_NEAREST:{
          int prevIdx = index;
          prevIdx--;
          while (prevIdx >= 0 && !MatchToken(flags,lineInfo[prevIdx])) prevIdx--;

          int nextIdx = index;
          while (nextIdx < count && !MatchToken(flags,lineInfo[nextIdx])) nextIdx++;
      
          if (prevIdx < 0 && nextIdx >= count) return false;
          else if (nextIdx >= count) index = prevIdx;
          else if (prevIdx <  0)     index = nextIdx;
          else if (index - prevIdx < nextIdx - index) index = prevIdx;
          else index = nextIdx;      
          break;
        }
        case WORDEXTFLAGS.WORDEXT_CURRENT:
        default:
          if (!MatchToken(flags, info))
            return false;
          break;
      }

      info = lineInfo[index];
      //we found something, set the span
      startIdx = info.startIndex;
      endIdx = info.endIndex+1;

      return true;        
    }

    static bool MatchToken(WORDEXTFLAGS flags, TokenInfo info){
      if ((flags & WORDEXTFLAGS.WORDEXT_FINDTOKEN) != 0)
        return !(info.type == TokenType.Comment ||
          info.type == TokenType.LineComment ||
          info.type == TokenType.Embedded);
      else 
        return (info.type == TokenType.Keyword ||
          info.type == TokenType.Identifier ||
          info.type == TokenType.String ||
          info.type == TokenType.Literal );

    }


    // Special View filter command handling.
    public virtual void CommentSelection(IVsTextView textView){

      //get text range
      TextSpan[] aspan = new TextSpan[1];
      textView.GetSelectionSpan(aspan);
      TextSpan span = aspan[0];

      //check bounds
      if (span.iEndIndex == 0) span.iEndLine--;

      //get line lengths
      int startLen,endLen;

      this.textLines.GetLengthOfLine(span.iStartLine, out startLen );
      this.textLines.GetLengthOfLine(span.iEndLine, out endLen );

      // adjust end index if necessary
      if (span.iEndIndex == 0) span.iEndIndex = endLen;

      int adjustment = 0;

      //try to use line comments first, if we can.        
      if (this.commentInfo.useLineComments && span.iStartIndex == 0 && span.iEndIndex == endLen){ 
        //comment each line
        for (int line = span.iStartLine; line <= span.iEndLine; line++){
          IntPtr lineStart = Marshal.StringToCoTaskMemAuto(this.commentInfo.lineStart);
          this.textLines.ReplaceLines(line, 0, line, 0, lineStart, this.commentInfo.lineStart.Length, null);
        }
        adjustment = this.commentInfo.lineStart.Length;
        span.iStartIndex = 0;
      }
        // otherwise try to use block comments
      else if (this.commentInfo.blockStart != null && this.commentInfo.blockEnd != null){
        //add end comment
        IntPtr blockEnd = Marshal.StringToCoTaskMemAuto(this.commentInfo.blockEnd);
        this.textLines.ReplaceLines(span.iEndLine, span.iEndIndex, span.iEndLine, span.iEndIndex, blockEnd, this.commentInfo.blockEnd.Length, null);
        //add start comment
        IntPtr blockStart = Marshal.StringToCoTaskMemAuto(this.commentInfo.blockStart);
        this.textLines.ReplaceLines(span.iStartLine, span.iStartIndex, span.iStartLine, span.iStartIndex, blockStart, this.commentInfo.blockStart.Length, null);

        adjustment = this.commentInfo.blockEnd.Length;
        if (span.iStartLine == span.iEndLine) adjustment += this.commentInfo.blockStart.Length;
      }
      else
        NativeHelpers.RaiseComError(HResult.E_FAIL);

       
      if (TextSpanHelper.TextSpanPositive(span))
        textView.SetSelection(span.iStartLine, span.iStartIndex, span.iEndLine, span.iEndIndex + adjustment );
      else
        textView.SetSelection(span.iEndLine, span.iEndIndex + adjustment, span.iStartLine, span.iStartIndex );
  
    }
    public virtual void UnCommentSelection(IVsTextView textView){

      //get text range
      TextSpan[] aspan = new TextSpan[1];
      textView.GetSelectionSpan(aspan);
      TextSpan span = aspan[0];

      //check bounds
      if (span.iEndIndex == 0) span.iEndLine--;

      //get line lengths
      int startLen,endLen;

      this.textLines.GetLengthOfLine(span.iStartLine, out startLen );
      this.textLines.GetLengthOfLine(span.iEndLine, out endLen );

      // adjust end index if necessary
      if (span.iEndIndex == 0) span.iEndIndex = endLen;

      int adjustment = 0;

      // is block comment selected?
      if (this.commentInfo.blockStart != null && this.commentInfo.blockEnd != null){

        // TODO: this doesn't work if the selection contains a mix of code and block comments
        // or multiple block comments!!  We should use our parse tree to find the embedded 
        // comments and uncomment the resulting comment spans only.

        string startText = null;
        this.textLines.GetLineText(span.iStartLine, span.iStartIndex, span.iStartLine, span.iStartIndex+this.commentInfo.blockStart.Length, out startText );

        if (startText == this.commentInfo.blockStart){
          string endText = null;
          this.textLines.GetLineText(span.iEndLine, span.iEndIndex- this.commentInfo.blockEnd.Length, span.iEndLine, span.iEndIndex, out endText );

          if (endText == this.commentInfo.blockEnd){
            //yes, block comment selected; remove it        
            this.textLines.ReplaceLines(span.iEndLine, span.iEndIndex-this.commentInfo.blockEnd.Length, span.iEndLine, span.iEndIndex, IntPtr.Zero, 0, null);       
            this.textLines.ReplaceLines(span.iStartLine, span.iStartIndex, span.iStartLine, span.iStartIndex+this.commentInfo.blockStart.Length, IntPtr.Zero, 0, null);
  
            adjustment = - commentInfo.blockEnd.Length;
            if (span.iStartLine == span.iEndLine) adjustment -= commentInfo.blockStart.Length;
 
            goto end;
          }
        }
      }

      //if no line comment possible, we are done
      if (!this.commentInfo.useLineComments) 
        NativeHelpers.RaiseComError(HResult.S_FALSE);
  
      // try if we can remove line comments, using the scanner to find them
      for (int line = span.iStartLine; line <= span.iEndLine; line++){

        TokenInfo[] lineInfo = this.colorizer.GetLineInfo(line, this.colorState);

        for (int i = 0, n = lineInfo.Length; i<n; i++){
          if (lineInfo[i].type == TokenType.LineComment){            
            this.textLines.ReplaceLines(line, lineInfo[i].startIndex, line, lineInfo[i].startIndex+this.commentInfo.lineStart.Length, IntPtr.Zero, 0, null);
            if (line == span.iEndLine){
              adjustment = - this.commentInfo.lineStart.Length;
            }
          }
        }
      }

      end:
        if (TextSpanHelper.TextSpanPositive(span))
          textView.SetSelection(span.iStartLine, span.iStartIndex, span.iEndLine, span.iEndIndex + adjustment );
        else
          textView.SetSelection(span.iEndLine, span.iEndIndex + adjustment, span.iStartLine, span.iStartIndex );
    }

    public virtual void Completion(IVsTextView textView, TokenInfo info, int line, int idx, bool completeWord){
      this.completeWord = completeWord;
      ParseReason reason = completeWord ? ParseReason.CompleteWord : ParseReason.MemberSelect;
      this.BeginParse(line, idx, info, reason, textView, new ParseResultHandler(this.HandleCompletionResponse));
    }

    bool completeWord;

    internal void HandleCompletionResponse(ParseRequest req){

      try{
        Declarations decls = req.Scope.GetDeclarations(req.View, req.Line, req.Col, req.TokenInfo);
        if (decls.GetCount()>0){
          //TODO: before putting up a completer first check if the caret has not moved. It would be bad if it moved beyond a completion char
          this.completionSet.Init(req.View, decls, this.completeWord );
        }
      } catch{
      }
    }

    public virtual void MethodTip(IVsTextView textView, TokenInfo info, int line, int column){
      this.BeginParse(line, column, info, ParseReason.MethodTip, textView, new ParseResultHandler(this.HandleMethodTipResponse));
    }

    internal void HandleMethodTipResponse(ParseRequest req){
      try{
        CallInfo call = req.Sink.MethodCalls.GetCurrentMethodCall();
        if (call == null) goto fail;
        StringCollection names = call.names;
        if (names.Count == 0) goto fail;
        ArrayList contexts = call.sourceLocations;

        string name = names[names.Count-1];
        SourceContext ctx = (SourceContext)contexts[names.Count-1];

        Overloads overloads;
        if (call.isTemplateInstance)
          overloads = req.Scope.GetTypes(ctx.StartLine, ctx.StartColumn, name);
        else
          overloads = req.Scope.GetMethods(ctx.StartLine, ctx.StartColumn, name);
        if (overloads == null)
          goto fail;

        TextSpan span = new TextSpan();
        span.iStartLine = ctx.StartLine;
        span.iStartIndex = ctx.StartColumn;
        span.iEndLine = ctx.EndLine;
        span.iEndIndex = ctx.EndColumn;

        int currentParameter = call.currentParameter-1;
        this.methodData.Refresh(req.View, overloads, currentParameter, span );
        return;

      fail:
        DismissMethodTip();        

      } catch{
      }
    }    

    public virtual void MatchBraces(IVsTextView textView, int line, int idx, TokenInfo info){
      this.BeginParse(line, idx, info, ParseReason.HighlightBraces, textView, new ParseResultHandler(this.HandleMatchBracesResponse));
    }

    internal void HandleMatchBracesResponse(ParseRequest req){

      try{

        if (req.Sink.Spans.Count == 0)
          return;

        //transform spanList into an array of spans
        TextSpan[] spans = (TextSpan[])req.Sink.Spans.ToArray(typeof(TextSpan));         

        for(int index = 0, n = spans.Length; index < n; index++){
          TextSpanHelper.TextSpanNormalize(ref spans[index], this.textLines);
        }

        //highlight
        req.View.HighlightMatchingBrace((uint)0, (uint)spans.Length, spans );

        //try to show the matching line in the statusbar
        if (this.statusBar != null && this.service.Preferences.EnableShowMatchingBrace){
          TextSpan span = spans[spans.Length-1]; //the matching brace

          string text;
          this.textLines.GetLineText(span.iEndLine, 0, span.iEndLine, span.iEndIndex, out text );

          int start;
          int len = text.Length;
          for (start = 0 ; start < span.iEndIndex && start < len && 
                Char.IsWhiteSpace(text[start]); start++) ;

          if (start < span.iEndIndex){
            if (text.Length>80) text = text.Substring(0,80)+"...";
            text = String.Format(SR.GetString(UIStringNames.BraceMatchStatus), text);
            this.statusBar.SetText(text);
          }          
        }
        
      } catch{
      }
    }
    public virtual string OnSyncQuickInfo(IVsTextView textView, int line, int col){
      // synchronous parse and return data tip text.
      string text = this.GetTextUpToLine(line+1);
      string fname = this.GetFilePath();
      ParseReason reason = ParseReason.Autos;
      AuthoringSink sink = new AuthoringSink(reason, line, col);
      AuthoringScope scope = this.service.ParseSource(text, line, col, fname, sink, reason);
      if (scope != null){
        TextSpan span;
        return scope.GetDataTipText(line, col, out span);
      }
      return null;
    }

    public virtual string OnSyncGoto(VsCommands cmd, IVsTextView textView, int line, int col, out TextSpan span){
      // synchronous parse and return definition location.
      string text = this.GetTextUpToLine(line+1);
      string fname = this.GetFilePath();
      ParseReason reason = ParseReason.Autos;
      AuthoringSink sink = new AuthoringSink(reason, line, col);
      AuthoringScope scope = this.service.ParseSource(text, line, col, fname, sink, reason);
      if (scope != null){
        return scope.Goto(cmd, textView, line, col, out span);
      }else{
        span = new TextSpan();
      }
      return null;
    }

    public virtual void GetPairExtents(int line, int col, out TextSpan span){

      span = new TextSpan();

      // Synchronously return the matching brace location.      
      string text = this.GetTextUpToLine(0); // Might be matching forwards so we have to search the whole file.
      string fname = this.GetFilePath();
      ParseReason reason = ParseReason.MatchBraces;
      AuthoringSink sink = new AuthoringSink(reason, line, col);
      AuthoringScope scope = this.service.ParseSource(text, line, col, fname, sink, reason);

      if (sink.Spans.Count == 0)
        return;

      //transform spanList into an array of spans
      TextSpan[] spans = (TextSpan[])sink.Spans.ToArray(typeof(TextSpan));      
      int spanCount = spans.Length;

      //called from ViewFilter::GetPairExtents
      if (spans[0].iStartLine < spans[spanCount-1].iStartLine ||
        (spans[0].iStartLine == spans[spanCount-1].iStartLine && spans[0].iStartIndex <= spans[spanCount-1].iStartIndex )){
        span.iStartLine  = spans[0].iStartLine;
        span.iStartIndex = spans[0].iStartIndex;
        span.iEndLine    = spans[spanCount-1].iStartLine;
        span.iEndIndex   = spans[spanCount-1].iStartIndex;
      }
      else{
        span.iStartLine  = spans[spanCount-1].iStartLine;
        span.iStartIndex = spans[spanCount-1].iStartIndex;
        span.iEndLine    = spans[0].iStartLine;
        span.iEndIndex   = spans[0].iStartIndex;
      }

      if (span.iStartLine == span.iEndLine && span.iStartIndex == span.iEndIndex)
        NativeHelpers.RaiseComError(HResult.S_FALSE);

      return;
    }

    internal void BeginParse(int line, int idx, TokenInfo info, ParseReason reason, IVsTextView view, ParseResultHandler callback){
     
      string text = null;
      if (reason == ParseReason.MemberSelect || reason == ParseReason.MethodTip)
        text = this.GetTextUpToLine(line );
      else
        text = this.GetTextUpToLine(0 ); // get all the text.      

      string fname = this.GetFilePath();

      this.service.BeginParse(new ParseRequest(line, idx, info, text, fname, reason, view), callback); 
    }

    internal void HandleParseResponse(ParseRequest req){

      try{
        this.ReportErrors(req.Sink.Errors);
#if LookForMemoryLeaks
        GC.Collect();
        Trace.WriteLine("Check file for errors leaked "+(System.GC.GetTotalMemory(true)-req.UsedMemoryAtStartOfRequest)+" bytes");
#endif
      } catch{
      }

    }

    internal string GetProjectName()
    {
      return null;
    }

    internal void ReportErrors(ArrayList errors)
    {
      int errorMax = this.service.Preferences.MaxErrorMessages;
#if WHIDBEY
      if (taskManager != null) {
        taskManager.ClearTasksOnSource(this.GetProjectName(), this.GetFilePath());

        for (int i = 0, n = errors.Count; i < n; i++) {
          ErrorNode enode = (ErrorNode)errors[n-i-1];  
          string message = enode.GetMessage(this.service.culture);
          if (message == null) continue;
          
          SourceContext ctx = enode.SourceContext;
          // Don't do multi-line squiggles, instead just squiggle to the end of the first line.
          if (ctx.EndLine > ctx.StartLine) {
            ctx.EndLine = ctx.StartLine;
            this.textLines.GetLengthOfLine(ctx.StartLine, out ctx.EndColumn);
          }
          
          TaskCategory category;
          TaskPriority priority;
          TaskMarker marker;
          switch (enode.Severity) {
            case Severity.SevWarning:
              category = TaskCategory.Warning;
              priority = TaskPriority.Normal;
              marker = TaskMarker.Warning;
              break;
            case Severity.SevHint:
              category = TaskCategory.Message;
              priority = TaskPriority.Low;
              marker = TaskMarker.Invisible;
              break;
            case Severity.SevFatal:
              category = TaskCategory.Error;
              priority = TaskPriority.High;
              marker = TaskMarker.CodeSense;
              break;
            case Severity.SevError:
            default:
              category = TaskCategory.Error;
              priority = TaskPriority.Normal;
              marker = TaskMarker.CodeSense;
              break;
          }

          taskManager.AddTask(message, null,
            enode.Code.ToString(), "CS" + enode.Code.ToString(),
            priority, category, marker, TaskOutputPane.None,
            this.GetProjectName(), this.GetFilePath(),
            ctx.StartLine + 1, ctx.StartColumn + 1, ctx.EndLine + 1, ctx.EndColumn + 1, null);

          //check error count
          if (i == errorMax) {
            string maxMsg = SR.GetString(UIStringNames.MaxErrorsReached);
            taskManager.AddTask( maxMsg, null, "0", "CS0000",
              TaskPriority.Low, TaskCategory.Warning, TaskMarker.Invisible, TaskOutputPane.None,
              this.GetProjectName(), this.GetFilePath(),
              ctx.StartLine + 1, ctx.EndColumn + 1, ctx.EndLine + 1, ctx.EndColumn + 2, null
            );
            break;
          }
        }
        taskManager.Refresh();
        return;
      }
#endif
      
      if (this.service == null) return;
      TextSpan firstError = new TextSpan();
      
      this.taskProvider.ClearErrors();

      for (int i = 0, n = errors.Count; i < n; i++){
        ErrorNode enode = (ErrorNode)errors[i];
        SourceContext ctx = enode.SourceContext;

        Severity severity = enode.Severity;
        string message = enode.GetMessage(this.service.culture);
        if (message == null)  return;        
  
        //set error
        TextSpan span;
        span.iStartLine  = ctx.StartLine;
        span.iStartIndex = ctx.StartColumn;
        span.iEndLine    = ctx.EndLine;
        span.iEndIndex   = ctx.EndColumn;

        // Don't do multi-line squiggles, instead just squiggle to the
        // end of the first line.
        if (span.iEndLine > span.iStartLine){
          span.iEndLine = span.iStartLine;
          this.textLines.GetLengthOfLine(span.iStartLine, out span.iEndIndex);
        }

        if (TextSpanHelper.TextSpanIsEmpty(firstError) && (severity > Severity.SevWarning)){
          firstError = span;
        }

        this.taskProvider.AddTask(this.CreateErrorTaskItem(span, message, severity));
  
        //check error count
        if (i == errorMax){
          string maxMsg = SR.GetString(UIStringNames.MaxErrorsReached);
          TaskItem error = this.CreateErrorTaskItem(span, maxMsg, Severity.SevWarning);
          this.taskProvider.AddTask(error);
          break;
        }
      }
      this.taskProvider.RefreshTaskWindow();

    }

  } // end Source

  //==================================================================================
  public class CompletionSet : IVsCompletionSet{
    ImageList imageList;    
    bool displayed;
    bool completeWord;
    IVsTextView textView;
    Declarations decls;
    Source source;
  
    public CompletionSet(ImageList ilist, Source source){
      this.imageList = ilist;
      this.source = source;
    }

    public bool IsDisplayed{
      get{ return this.displayed; }
    }

    public virtual void Init(IVsTextView textView, Declarations decls, bool completeWord){
      
      Close();

      this.textView = textView;
      this.decls = decls;
      this.completeWord = completeWord;

      //check if we have members
      long count = decls.GetCount();
      if (count <= 0) return ;

      //initialise and refresh      
      UpdateCompletionFlags flags = UpdateCompletionFlags.UCS_NAMESCHANGED;
      if (this.completeWord) flags |= UpdateCompletionFlags.UCS_COMPLETEWORD;

      textView.UpdateCompletionStatus(this, (uint)flags);
      this.displayed = true;
    }
    
    public virtual void Close(){
      if (this.displayed && this.textView != null){
        textView.UpdateCompletionStatus(null, 0 );
      }
      this.displayed = false;
      this.textView = null;
      this.decls = null;
    }
  
    //--------------------------------------------------------------------------
    //IVsCompletionSet methods
    //--------------------------------------------------------------------------
    public virtual int GetImageList(out IntPtr phImages){
      phImages = this.imageList.GetNativeImageList();
      return 0;
    }

    public virtual uint GetFlags(){
      return (uint)UpdateCompletionFlags.CSF_HAVEDESCRIPTIONS |
        (uint)UpdateCompletionFlags.CSF_CUSTOMCOMMIT | 
        (uint)UpdateCompletionFlags.CSF_INITIALEXTENTKNOWN |
        (uint)UpdateCompletionFlags.CSF_CUSTOMMATCHING;
    }
    public virtual int GetCount(){
      return this.decls.GetCount();
    }

    public int GetDisplayText(int index, out string text, int[] glyph){
      if (glyph != null){
        glyph[0] = this.decls.GetGlyph(index);
      }
      text = this.decls.GetDisplayText(index);
      return 0;
    }
    public int GetDescriptionText(int index, out string description){
      description = this.decls.GetDescription(index);
      return 0;
    }
    public virtual int GetInitialExtent(out int line, out int startIdx, out int endIdx){
      startIdx = 0;
      endIdx = 0;
      int idx;
      this.textView.GetCaretPos(out line, out idx);
      bool rc = this.source.GetWordExtent(line, idx, WORDEXTFLAGS.WORDEXT_CURRENT, out startIdx, out endIdx);
      if (!rc && idx > 0){
        rc = this.source.GetWordExtent(line, idx-1, WORDEXTFLAGS.WORDEXT_CURRENT, out startIdx, out endIdx );        
        if (!rc){ 
          startIdx = idx; 
          endIdx = idx;
          return (int)HResult.S_FALSE;
        }
      }
      return 0;
    }

    public virtual int GetBestMatch(string textSoFar, int length, out int index, out uint flags){
      index = 0;
      flags = 0;
      bool uniqueMatch;
      this.decls.GetBestMatch(textSoFar, out index, out uniqueMatch);

      flags = (uint)UpdateCompletionFlags.GBM_SELECT;
      if (uniqueMatch) flags |= (uint)UpdateCompletionFlags.GBM_UNIQUE;
      return 0;
    }

    public virtual int OnCommit(string textSoFar, int index, int selected, ushort commitChar, out string completeWord){

      char ch = (char)commitChar;
      bool isCommitChar = this.decls.IsCommitChar(textSoFar, ch);
      completeWord = textSoFar;
      
      if (isCommitChar){  
        if (selected == 1){
          completeWord = this.decls.GetInsertionText(index);
        }else{
          completeWord += commitChar.ToString();
        }
      }else{
        // S_FALSE return means the character is not a commit character.
        completeWord = textSoFar;
        return (int)HResult.S_FALSE;
      }
      return 0;
    }

    public virtual void Dismiss(){
      this.displayed = false;
      this.decls = null;
      this.textView = null;
    }
  }

  //-------------------------------------------------------------------------------------
  public class MethodData : IVsMethodData{
    protected IServiceProvider provider;
    protected Overloads methods;
    int currentParameter;
    int currentMethod;
    bool displayed;
    protected IVsMethodTipWindow methodTipWindow;
    // method format info
    string typeStart, typeEnd;
    bool typePrefixed;
    protected IVsTextView textView;
    private TextSpan context;

    public MethodData(IServiceProvider site){
      this.provider = site;
      this.methodTipWindow = (IVsMethodTipWindow)VsShell.CreateInstance(provider, ref VsConstants.CLSID_VsMethodTipWindow, ref VsConstants.IID_IVsMethodTipWindow, typeof(IVsMethodTipWindow));
      if (this.methodTipWindow != null){  
        methodTipWindow.SetMethodData(this );
      }
    }
    
    public void SetMethodFormat(string typeStart, string typeEnd, bool typePrefixed){
      this.typeStart = typeStart;
      this.typeEnd = typeEnd;
      this.typePrefixed = typePrefixed;
    }

    public bool IsDisplayed{ 
      get{
        return this.displayed;
      }
    }

    public void Refresh(IVsTextView textView, Overloads methods, int currentParameter, TextSpan context){
      this.methods = methods;
      this.context = context;
      this.textView = textView; 
      this.currentMethod = methods.GetPositionOfSelectedMember();
      this.currentParameter = currentParameter < 0 ? 0 : currentParameter;
      this.AdjustCurrentParameter(0);
    }

    public void AdjustCurrentParameter(int increment){
      this.currentParameter += increment;
      if (this.currentParameter < 0) 
        this.currentParameter = -1;
      else if (this.currentParameter >= this.GetParameterCount(this.currentMethod )) 
        this.currentParameter = this.GetParameterCount(this.currentMethod);
      this.UpdateView();
    }

    public void Close(){
      this.Dismiss();
      if (this.methodTipWindow != null) this.methodTipWindow.SetMethodData(null);
      this.methodTipWindow = null;
    }

    public void Dismiss(){         
      if (this.displayed && this.textView != null){
        this.textView.UpdateTipWindow(this.methodTipWindow, (uint)TipWindowFlags.UTW_DISMISS );
      }
      this.OnDismiss();
    }

    //========================================================================
    //IVsMethodData
    public int GetOverloadCount(){
      if (this.textView == null || this.methods == null) return 0;
      return this.methods.GetCount();
    }
    public int GetCurMethod(){
      return this.currentMethod;
    }
    public int NextMethod(){
      if (this.currentMethod < GetOverloadCount()-1) this.currentMethod++;
      return this.currentMethod;
    }
    public int PrevMethod(){
      if (this.currentMethod > 0) this.currentMethod--;
      return this.currentMethod;    
    }
    public int GetParameterCount(int method){
      if (this.methods == null) return 0;
      if (method < 0 || method >= GetOverloadCount()) return 0;
      return this.methods.GetParameterCount(method);
    }
    public int GetCurrentParameter(int method){
      return this.currentParameter;
    }
    public void OnDismiss(){
      this.textView         = null;
      this.methods          = null;
      this.currentMethod    = 0;
      this.currentParameter = 0;
      this.displayed        = false;
    }
    public void UpdateView(){
      if (this.textView == null) return;
      this.textView.UpdateTipWindow(this.methodTipWindow, (uint)TipWindowFlags.UTW_CONTENTCHANGED | (uint)TipWindowFlags.UTW_CONTEXTCHANGED );
      this.displayed = true;
    }
    public int GetContextStream(out int pos, out int length){
      pos = 0;
      length = 0;

      int line, idx;
      this.textView.GetCaretPos(out line, out idx);

      line = Math.Max(line, this.context.iStartLine);

      int vspace;
      this.textView.GetNearestPosition(line, this.context.iStartIndex, out pos, out vspace );

      line = Math.Max(line, this.context.iEndLine);
      int endpos;
      this.textView.GetNearestPosition(line, this.context.iEndIndex, out endpos, out vspace);

      length = endpos - pos;
      return 0;
    }
    public IntPtr GetMethodText(int method, MethodTextType type){
      if (this.methods == null) return IntPtr.Zero;
      if (method < 0 || method >= GetOverloadCount()) return IntPtr.Zero;

      string result = null;

      //a type
      if ((type == MethodTextType.MTT_TYPEPREFIX && this.typePrefixed) ||
        (type == MethodTextType.MTT_TYPEPOSTFIX && !this.typePrefixed)){

        string str = this.methods.GetType(method);
        if (str == null) return IntPtr.Zero;
    
        result = this.typeStart + str + this.typeEnd;        
      }else{

        //other
        switch (type){
          case MethodTextType.MTT_OPENBRACKET:
            result = this.methods.GetParameterOpen(method);
            break;

          case MethodTextType.MTT_CLOSEBRACKET:
            result = this.methods.GetParameterClose(method);
            break;

          case MethodTextType.MTT_DELIMITER:
            result = this.methods.GetParameterSeparator(method);
            break;

          case MethodTextType.MTT_NAME:{
            result = this.methods.GetName(method);
            break;
          }

          case MethodTextType.MTT_DESCRIPTION:
            result = this.methods.GetDescription(method);
            break;

          case MethodTextType.MTT_TYPEPREFIX:
          case MethodTextType.MTT_TYPEPOSTFIX:
          default:
            break;
        }
      }
      return result == null ? IntPtr.Zero : Marshal.StringToBSTR(result);
    }

    public IntPtr GetParameterText(int method, int parameter, ParameterTextType type){      
      if (this.methods == null) return IntPtr.Zero;
      if (method < 0 || method >= GetOverloadCount()) return IntPtr.Zero;
      if (parameter < 0 || parameter >= GetParameterCount(method )) return IntPtr.Zero;
      
      string name;
      string description;
      string display;
      this.methods.GetParameterInfo(method, parameter, out name, out display, out description );
      string result = null;

      switch (type){
        case ParameterTextType.PTT_NAME:        
          result = name; 
          break;
        case ParameterTextType.PTT_DESCRIPTION: 
          result = description;
          break;
        case ParameterTextType.PTT_DECLARATION: 
          result = display; 
          break;
        default:              
          break;
      }
      return result == null ? IntPtr.Zero : Marshal.StringToBSTR(result);
    }
  }

}
