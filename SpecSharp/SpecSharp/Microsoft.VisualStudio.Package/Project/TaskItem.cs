//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
//using System.CodeDom.Compiler;
using System.Runtime.InteropServices;
using System.Xml;
using System.Collections;
using System.IO;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

using IServiceProvider = System.IServiceProvider;

namespace Microsoft.VisualStudio.Package{
  public class TaskProvider : IVsTaskProvider, IVsTaskListEvents{
    ArrayList items;
    uint dwCookie;
    IServiceProvider site;
    IVsTaskList taskList;
    TaskTokens taskTokens;

    public static Guid GUID_VsTaskListViewAll = new Guid(0x1880202e, 0xfc20, 0x11d2, 0x8b, 0xb1, 0x0, 0xc0, 0x4f, 0x8e, 0xc2, 0x8c);
    public static Guid GUID_VsTaskListViewUserTasks = new Guid(0x1880202f, 0xfc20, 0x11d2, 0x8b, 0xb1, 0x0, 0xc0, 0x4f, 0x8e, 0xc2, 0x8c);
    public static Guid GUID_VsTaskListViewShortcutTasks = new Guid(0x18802030, 0xfc20, 0x11d2, 0x8b, 0xb1, 0x0, 0xc0, 0x4f, 0x8e, 0xc2, 0x8c);
    public static Guid GUID_VsTaskListViewHTMLTasks = new Guid(0x36ac1c0d, 0xfe86, 0x11d2, 0x8b, 0xb1, 0x0, 0xc0, 0x4f, 0x8e, 0xc2, 0x8c);
    public static Guid GUID_VsTaskListViewCompilerTasks = new Guid(0x18802033, 0xfc20, 0x11d2, 0x8b, 0xb1, 0x0, 0xc0, 0x4f, 0x8e, 0xc2, 0x8c);
    public static Guid GUID_VsTaskListViewCommentTasks = new Guid(0x18802034, 0xfc20, 0x11d2, 0x8b, 0xb1, 0x0, 0xc0, 0x4f, 0x8e, 0xc2, 0x8c);
    public static Guid GUID_VsTaskListViewCurrentFileTasks = new Guid(0x18802035, 0xfc20, 0x11d2, 0x8b, 0xb1, 0x0, 0xc0, 0x4f, 0x8e, 0xc2, 0x8c);
    public static Guid GUID_VsTaskListViewCheckedTasks = new Guid(0x18802036, 0xfc20, 0x11d2, 0x8b, 0xb1, 0x0, 0xc0, 0x4f, 0x8e, 0xc2, 0x8c);
    public static Guid GUID_VsTaskListViewUncheckedTasks = new Guid(0x18802037, 0xfc20, 0x11d2, 0x8b, 0xb1, 0x0, 0xc0, 0x4f, 0x8e, 0xc2, 0x8c);

    public TaskProvider(IServiceProvider site){
      this.site = site;
      this.taskList = (IVsTaskList)this.site.GetService(typeof(SVsTaskList));
      items = new ArrayList();
      RegisterProvider();
      taskTokens = new TaskTokens(site);
      taskTokens.Refresh();
    }

    public void Dispose(){
      ClearErrors();
      if (this.taskList != null && dwCookie != 0){
        OnTaskListFinalRelease(this.taskList);
      }
      this.site = null;
      this.taskList = null;
    }

    public void ClearErrors(){
      if (items.Count>0){
        foreach (TaskItem item in items){
          item.OnDeleteTask();
        }
        items.Clear();
        RefreshTaskWindow();
      }
    }

    public bool IsTaskToken( string text, out VSTASKPRIORITY priority ){
      return this.taskTokens.IsTaskToken(text, out priority);
    }

    public void AddTask(TaskItem item){
      this.items.Add(item);
    }

    public string GetDataTipText(TextSpan cursorPosition, ref TextSpan errorSpan){
      if (this.items == null || this.items.Count == 0) return null;
      string result = null;
      foreach (TaskItem item in items){
        result = item.GetDataTipText(cursorPosition, ref errorSpan);
        if (result != null) return result;
      }
      return null;
    }

    public void RefreshTaskWindow(){
      if(this.taskList != null && dwCookie != 0){   
        this.taskList.RefreshTasks(dwCookie);
      }
    }

    public void ShowTashList(){
      if(this.taskList != null)
        this.taskList.AutoFilter2(ref GUID_VsTaskListViewAll);
    }

    // IVsTaskProvider methods
    public int EnumTaskItems(out IVsEnumTaskItems ppEnum){
      ppEnum = new TaskEnumerator((TaskItem[])items.ToArray(typeof(TaskItem)));
      return 0;
    }
    public int ImageList(out IntPtr phImageList){
      phImageList = IntPtr.Zero;
      //TODO: find out which image list is wanted
      return 0;
    }
    public int SubcategoryList(uint cbstr, string[] rgbstr, out uint pcActual){
      // just one subcategory is all we support right now.
      pcActual = 1;
      if (rgbstr != null && rgbstr.Length>0) rgbstr[0] = "dummy";
      return 0;
    }
    public int ReRegistrationKey(out string pbstrKey){
      // We don't need to support "re-registration".
      pbstrKey = null;
      return (int)HResult.E_FAIL;
    }
    public int OnTaskListFinalRelease(IVsTaskList pTaskList){
      if (dwCookie != 0){
        this.taskList.UnregisterTaskProvider(dwCookie);
        dwCookie = 0;
      }
      return 0;
    }

    #region IVsTaskListEvents methods
    public virtual int OnCommentTaskInfoChanged(){
      taskTokens.Refresh();
      return 0;
    }
    #endregion 

    private void RegisterProvider(){       
      if (taskList != null){
        if (dwCookie != 0){
          taskList.UnregisterTaskProvider(dwCookie);
          dwCookie = 0;
        }
        taskList.RegisterTaskProvider(this, out dwCookie);
      }
    }    

  }

  // Custom task items for the task list.
  public class TaskItem : IVsTaskItem, IVsProvideUserContext
#if WHIDBEY    
    , IVsErrorItem 
#endif  
  {
    // Since all taskitems support this field we define it generically. Can use put_Text to set it.
    IServiceProvider site;
    string text;
    string helpKeyword;
    string fileName; 
    IVsTextLineMarker textLineMarker;
    public VSTASKCATEGORY category;
    public VSTASKPRIORITY priority;
    public _vstaskbitmap bitmap;
    bool readOnly;
    Severity severity;

    public TaskItem(IServiceProvider site, IVsTextLineMarker textLineMarker, string fileName, string text, bool readOnly, VSTASKCATEGORY cat, VSTASKPRIORITY pri, _vstaskbitmap bitmap, string helpKeyword, Severity severity){
      this.site = site;
      this.text = text;
      this.fileName = fileName;
      this.textLineMarker = textLineMarker;
      this.helpKeyword = helpKeyword;
      this.category = cat;
      this.priority = pri;
      this.bitmap = bitmap;
      this.readOnly = readOnly;
      this.severity = severity;
    }

    public virtual int get_Priority(VSTASKPRIORITY[] priority){
      priority[0] = this.priority;
      return 0;
    }
    public virtual int put_Priority(VSTASKPRIORITY priority){
      this.priority = priority;
      return 0;
    }
    public virtual int Category(VSTASKCATEGORY[] pCat){
      pCat[0] = this.category;
      return 0;
    }
    public virtual int SubcategoryIndex(out int pIndex){
      pIndex = 0;
      return 0;
    }
    public virtual int ImageListIndex(out int pIndex){
      pIndex = (int)bitmap;
      return 0;
    }
    public virtual int get_Checked(out int pfChecked){
      pfChecked = 0; // no checkboxes for now.
      return 0;
    }
    public virtual int put_Checked(int fChecked){
      return 0;
    }
    public virtual int get_Text(out string pbstrText){
      pbstrText = this.text;
      return 0;
    }
    public virtual int put_Text(string pbstrText){
      this.text = pbstrText;
      return 0;
    }
    public virtual int Document(out string pbstrMkDocument){
      pbstrMkDocument = this.fileName;
      return 0;
    }
    public virtual int Line(out int piLine){
      piLine = this.GetLine();
      return 0;
    }
    public virtual int GetLine(){
      if (this.textLineMarker == null)
        return 1;
      else{
        TextSpan[] span = new TextSpan[1];
        textLineMarker.GetCurrentSpan(span);      
        return span[0].iStartLine;
      }
    }
    public virtual int Column(out int piCol){
      piCol = this.GetColumn();
      return 0;
    }
    public virtual int GetColumn(){
      if (this.textLineMarker == null)
        return 1;
      else{
        TextSpan[] span = new TextSpan[1];
        textLineMarker.GetCurrentSpan(span);            
        return span[0].iStartIndex;
      }
    }
    public string GetDataTipText(TextSpan cursorPosition, ref TextSpan errorSpan){
      TextSpan errSpan = new TextSpan();
      if (this.textLineMarker != null){
        TextSpan[] span = new TextSpan[1];     
        textLineMarker.GetCurrentSpan(span); 
        errSpan = span[0];
      }
      if (errSpan.iEndLine < cursorPosition.iStartLine || (errSpan.iEndLine == cursorPosition.iStartLine && errSpan.iEndIndex < cursorPosition.iStartIndex)) 
        return null; //error span precedes cursor
      if (errSpan.iStartLine > cursorPosition.iEndLine || (errSpan.iStartLine == cursorPosition.iEndLine && errSpan.iStartIndex > cursorPosition.iEndIndex)) 
        return null; //error span follows cursor
      errorSpan = errSpan; 
      return this.text;
    }
    public virtual int CanDelete(out int fCanDelete){
      fCanDelete = 1;
      return 0;
    }
    public virtual int IsReadOnly(VSTASKFIELD field, out int fReadOnly){
      fReadOnly = 1; // yep
      return 0;
    }
    public virtual int HasHelp(out int pfHasHelp){
      pfHasHelp = (this.helpKeyword != null) ? 1 : 0;
      return 0;
    }
    public virtual int NavigateTo(){
      TextSpan span = new TextSpan();
      if (this.textLineMarker != null){
        TextSpan[] spanArray = new TextSpan[1];
        textLineMarker.GetCurrentSpan(spanArray);            
        span = spanArray[0];
      }
  
      IVsUIHierarchy hierarchy;
      uint[] itemID = new uint[1];
      IVsWindowFrame docFrame;
      IVsTextView  textView;
      VsShell.OpenDocument(this.site, this.fileName, Guid.Empty, out hierarchy, itemID, out docFrame, out textView );
      docFrame.Show();
      textView.SetCaretPos( span.iStartLine, span.iStartIndex );
      textView.SetSelection( span.iStartLine, span.iStartIndex, span.iEndLine, span.iEndIndex );
      textView.EnsureSpanVisible( span );
      return 0;
    }


    public virtual int NavigateToHelp(){
      return 0;
    }

    public virtual int OnFilterTask(int fVisible){
      return 0;
    }

    public virtual int OnDeleteTask(){
      if (textLineMarker != null) 
        textLineMarker.Invalidate();
      textLineMarker = null;
      return 0;
    }   

    // IVsProvideUserContext methods
    public int GetUserContext(out IVsUserContext ppUserContext){
      ppUserContext = null;
      return 0;
    }

#if WHIDBEY
    #region IVsErrorItem Members

    public int GetCategory(out uint pCategory)
    {
      switch (severity) {
        case Severity.SevWarning: pCategory = (uint)__VSERRORCATEGORY.EC_WARNING; break;
        case Severity.SevHint: pCategory = (uint)__VSERRORCATEGORY.EC_MESSAGE; break;
        default: pCategory = (uint)__VSERRORCATEGORY.EC_ERROR; break;
      }
      return 0;
    }

    public int GetHierarchy(out IVsHierarchy ppProject)
    {
      ppProject = null;
      return 0;
    }

    #endregion
#endif
  };  

  class TaskEnumerator : IVsEnumTaskItems{
    TaskItem[] items;
    uint pos;
    public TaskEnumerator(TaskItem[] items){
      this.items = items;
    }
    public virtual int Clone(out IVsEnumTaskItems ppenum){
      ppenum = new TaskEnumerator(this.items);
      return 0;
    }  
    public virtual int Next(uint celt, IVsTaskItem[] rgelt, uint[] pceltFetched){
      uint count = 0;
      for (uint i = pos, n = (uint)Math.Min(pos+celt, items.Length); rgelt != null && i<n; i++){
        rgelt[count++] = items[i];
      }
      if (count == 0) 
        return (int)HResult.S_FALSE;
      
      if (pceltFetched != null) 
        pceltFetched[0] = count;
      pos += count;
      return 0;
    }
    public virtual int Reset(){
      pos = 0;
      return 0;
    }
    public virtual int Skip(uint celt){
      pos += celt;
      return 0;
    }
  }

  public class TaskTokens{
    IServiceProvider site;
    Hashtable taskTokens;

    public TaskTokens( IServiceProvider site){
      this.site = site;
    }

    public void Clear(){
      taskTokens.Clear();
    }

    // called when OnCommentTaskInfoChanged is called.
    public void Refresh(){

      //Get token enumerator and allocate memory
      IVsCommentTaskInfo commentTaskInfo = null;
      try{
        commentTaskInfo = (IVsCommentTaskInfo)this.site.GetService(typeof(IVsCommentTaskInfo));
        if (commentTaskInfo == null) return;
      
        int count;
        commentTaskInfo.TokenCount(out count );

        IVsEnumCommentTaskTokens commentTaskTokens = null;
        commentTaskInfo.EnumTokens(out commentTaskTokens );

        Hashtable newTokens = new Hashtable();

        //Get all tokens

        int index = 0;
        for (index = 0; index < count; index++){
          uint fetched;
          IVsCommentTaskToken[] commentTaskToken = new IVsCommentTaskToken[1];

          commentTaskTokens.Next( 1, commentTaskToken, out fetched );
          if (fetched == 1){
            string token = null;
            VSTASKPRIORITY[] priority = new VSTASKPRIORITY[1]{ VSTASKPRIORITY.TP_NORMAL };
            commentTaskToken[0].Text(out token);            
            commentTaskToken[0].Priority(priority);
            if (token != null){
              newTokens[token] = priority[0];
            }
          }
        }
        
        //update the token information
        this.taskTokens = newTokens;

      } catch (Exception){
        return;
      }
    }

    public bool IsTaskToken( string text, out VSTASKPRIORITY priority ){
      priority = VSTASKPRIORITY.TP_NORMAL;
      if (text == null) return false;
      if (this.taskTokens == null) return false;

      //extract the token
      int i = 0;
      int len = text.Length;
      while (i < len && Char.IsWhiteSpace(text[i])){
        i++;
      }

      int start = i;
      while (i < len && !Char.IsWhiteSpace(text[i]) && 
        text[i] != ':' ){ 
        i++;
      }
      if (len == 0) return false;
  
      string token = text.Substring(start, i-start); 

      //check if this is a defined token
      return this.taskTokens.Contains(token);
    }
  };

  //-----------------------------------------------------------*/
  public class TextMarkerClient : IVsTextMarkerClient{
    string tipText;

    public TextMarkerClient(string tipText){
      this.tipText = tipText;
    }

    public static IVsTextLineMarker CreateMarker( IVsTextLines textLines, TextSpan span, MARKERTYPE mt, string tipText){

      IVsTextLineMarker[] marker = new IVsTextLineMarker[1];    
      TextMarkerClient textMarkerClient = new TextMarkerClient( tipText );
      textLines.CreateLineMarker((int)mt, span.iStartLine, span.iStartIndex, 
        span.iEndLine, span.iEndIndex, textMarkerClient, marker);
      return marker[0];
    }

    /*---------------------------------------------------------
      IVsTextMarkerClient
    -----------------------------------------------------------*/
    public virtual void MarkerInvalidated(  ){
      //TRACE("MarkerInvalidated" );
      return;
    }

    public virtual void OnBufferSave(string fileName ){
      //TRACE1("OnBufferSave: %S", fileName );
      return;
    }

    public virtual void OnBeforeBufferClose(  ){
      //TRACE("OnBeforeBufferClose" );
      return;
    }

    public virtual void OnAfterSpanReload(  ){
      //TRACE("OnAfterSpanReload" );
      return;
    }

    public virtual int OnAfterMarkerChange(IVsTextMarker marker){
      //TRACE("OnAfterMarkerChange" );
      return 0;
    }

    public virtual int GetTipText(IVsTextMarker marker, string[] tipText){
      if (tipText != null) tipText[0] = this.tipText;
      return 0;
    }

    public virtual int GetMarkerCommandInfo(IVsTextMarker marker, int item, string[] text, uint[] commandFlags){
      if (text != null){
        text[0] = this.tipText;
        commandFlags[0] = 0;
      }
      return 0;
    }

    public virtual int ExecMarkerCommand(IVsTextMarker marker, int item){
      return 0;
    }
  }
}
