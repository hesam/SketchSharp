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

namespace Microsoft.VisualStudio.Package{
  public class TaskProvider : IVsTaskProvider, IVsTaskListEvents {
    ArrayList items;
    uint dwCookie;
    ServiceProvider site;
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

    public TaskProvider(ServiceProvider site) {
      this.site = site;
      this.taskList = (IVsTaskList)this.site.QueryService(VsConstants.SID_SVsTaskList, typeof(IVsTaskList));
      items = new ArrayList();
      RegisterProvider();
      taskTokens = new TaskTokens(site);
      taskTokens.Refresh();
    }

    public void Close() {
      ClearErrors();
      if (this.taskList != null && dwCookie != 0) {
        OnTaskListFinalRelease(this.taskList);
      }
      this.site = null;
      this.taskList = null;
    }

    public void ClearErrors() {
      if (items.Count>0) {
        foreach (TaskItem item in items) {
          item.OnDeleteTask();
        }
        items.Clear();
        RefreshTaskWindow();
      }
    }

    public bool IsTaskToken( string text, out VSTASKPRIORITY priority ) {
      return this.taskTokens.IsTaskToken(text, out priority);
    }
    public void AddTask(TaskItem item) {
      items.Add(item);
    }

    public void RefreshTaskWindow() {
      if(this.taskList != null && dwCookie != 0) {   
        this.taskList.RefreshTasks(dwCookie);
      }
    }

    public void ShowTashList() {
      if(this.taskList != null)
        this.taskList.AutoFilter2(ref GUID_VsTaskListViewAll);
    }

    // IVsTaskProvider methods
    public void EnumTaskItems(out IVsEnumTaskItems ppEnum){
      ppEnum = new TaskEnumerator((TaskItem[])items.ToArray(typeof(TaskItem)));
    }
    public void ImageList(out IntPtr phImageList){
      phImageList = IntPtr.Zero;
      //TODO: find out which image list is wanted
    }
    public void SubcategoryList(uint cbstr, string[] rgbstr, out uint pcActual) {
      // just one subcategory is all we support right now.
      pcActual = 1;
      if (rgbstr != null && rgbstr.Length>0) rgbstr[0] = "dummy"; 
    }
    public void ReRegistrationKey(out string pbstrKey){
      // We don't need to support "re-registration".
      pbstrKey = null;
      NativeHelpers.RaiseComError(HResult.E_FAIL);
    }
    public void OnTaskListFinalRelease(IVsTaskList pTaskList){
      if (dwCookie != 0) {
        this.taskList.UnregisterTaskProvider(dwCookie);
        dwCookie = 0;
      }
    }

    #region IVsTaskListEvents methods
    public virtual void OnCommentTaskInfoChanged(){
      taskTokens.Refresh();
    }
    #endregion 

    private void RegisterProvider() {       
      if (taskList != null) {
        if (dwCookie != 0) {
          taskList.UnregisterTaskProvider(dwCookie);
          dwCookie = 0;
        }
        taskList.RegisterTaskProvider(this, out dwCookie);
      }
    }    

  }

  // Custom task items for the task list.
  public class TaskItem : IVsTaskItem, IVsProvideUserContext {
    // Since all taskitems support this field we define it generically. Can use put_Text to set it.
    ServiceProvider site;
    string text;
    string helpKeyword;
    string fileName; 
    IVsTextLineMarker textLineMarker;
    public VSTASKCATEGORY category;
    public VSTASKPRIORITY priority;
    public _vstaskbitmap bitmap;
    bool readOnly;

    public TaskItem(ServiceProvider site, IVsTextLineMarker textLineMarker, string fileName, string text, bool readOnly, VSTASKCATEGORY cat, VSTASKPRIORITY pri, _vstaskbitmap bitmap, string helpKeyword) {
      this.site = site;
      this.text = text;
      this.fileName = fileName;
      this.textLineMarker = textLineMarker;
      this.helpKeyword = helpKeyword;
      this.category = cat;
      this.priority = pri;
      this.bitmap = bitmap;
      this.readOnly = readOnly;
    }

    public virtual void get_Priority(VSTASKPRIORITY[] priority){
      priority[0] = this.priority;
    }
    public virtual void put_Priority(VSTASKPRIORITY priority){
      this.priority = priority;
    }
    public virtual void Category(VSTASKCATEGORY[] pCat){
      pCat[0] = this.category;
    }
    public virtual void SubcategoryIndex(out int pIndex) {
      pIndex = 0; 
    }
    public virtual void ImageListIndex(out int pIndex){
      pIndex = -1; // no images for now.
    }
    public virtual void get_Checked(out int pfChecked){
      pfChecked = 0; // no checkboxes for now.
    }
    public virtual void put_Checked(int fChecked){
    }
    public virtual void get_Text(out string pbstrText){
      pbstrText = this.text;
    }
    public virtual void put_Text(string pbstrText){
      this.text = pbstrText;
    }
    public virtual void Document(out string pbstrMkDocument) {
      pbstrMkDocument = this.fileName;
    }
    public virtual void Line(out int piLine){            
      TextSpan[] span = new TextSpan[1];
      textLineMarker.GetCurrentSpan(span);      
      piLine = span[0].iStartLine;  
    }
    public virtual void Column(out int piCol){
      TextSpan[] span = new TextSpan[1];
      textLineMarker.GetCurrentSpan(span);            
      piCol = span[0].iStartIndex;  
    }

    public virtual void CanDelete(out int fCanDelete){
      fCanDelete = 1;
    }
    public virtual void IsReadOnly(VSTASKFIELD field, out int fReadOnly){
      // this.readOnly ?
      fReadOnly = 1; // yep
    }
    public virtual void HasHelp(out int pfHasHelp){
      pfHasHelp = (this.helpKeyword != null) ? 1 : 0;
    }
    public virtual void NavigateTo(){
      
      TextSpan[] spanArray = new TextSpan[1];
      textLineMarker.GetCurrentSpan(spanArray);            
      TextSpan span = spanArray[0];
  
      IVsUIHierarchy hierarchy;
      uint itemID;
      IVsWindowFrame docFrame;
      IVsTextView  textView;
      VsShell.OpenDocument(this.site, this.fileName, out hierarchy, out itemID, out docFrame, out textView );
      docFrame.Show();
      textView.SetCaretPos( span.iStartLine, span.iStartIndex );
      textView.SetSelection( span.iStartLine, span.iStartIndex, span.iEndLine, span.iEndIndex );
      textView.EnsureSpanVisible( span );
    }

    public virtual void NavigateToHelp(){
      //if(helpKeyword != null && helpKeyword != "") {   
      //  IVsHelp help = ProjectManager.QueryService(site, SID_SVsHelp, IID_IVsHelp);
      //  help.DisplayTopicFromF1Keyword(helpKeyword);
      //  help = null;
      //}
    }

    public virtual void OnFilterTask(int fVisible){
    }

    public virtual void OnDeleteTask(){
      if (textLineMarker != null) 
        textLineMarker.Invalidate();
      textLineMarker = null;
    }   

    // IVsProvideUserContext methods
    public void GetUserContext(out IVsUserContext ppUserContext){
      ppUserContext = null;
    }


  };  

  class TaskEnumerator : IVsEnumTaskItems {
    TaskItem[] items;
    uint pos;
    public TaskEnumerator(TaskItem[] items) {
      this.items = items;
    }
    public virtual void Clone (out IVsEnumTaskItems ppenum) {
      ppenum = new TaskEnumerator(this.items);
    }  
    public virtual int Next(uint celt, IVsTaskItem[] rgelt, uint[] pceltFetched) {
      uint count = 0;
      for (uint i = pos, n = (uint)Math.Min(pos+celt, items.Length); rgelt != null && i<n; i++) {
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
    public virtual int Skip(uint celt) {
      pos += celt;
      return 0;
    }
  }

  public class TaskTokens {
    ServiceProvider site;
    Hashtable taskTokens;

    public TaskTokens( ServiceProvider site) {
      this.site = site;
    }

    public void Clear() {
      taskTokens.Clear();
    }

    // called when OnCommentTaskInfoChanged is called.
    public void Refresh() {

      //Get token enumerator and allocate memory
      Guid guid = typeof(IVsCommentTaskInfo).GUID;
      IVsCommentTaskInfo commentTaskInfo = null;
      try {
        commentTaskInfo = (IVsCommentTaskInfo)this.site.QueryService(guid, typeof(IVsCommentTaskInfo));
        if (commentTaskInfo == null) return;
      
        int count;
        commentTaskInfo.TokenCount(out count );

        IVsEnumCommentTaskTokens commentTaskTokens = null;
        commentTaskInfo.EnumTokens(out commentTaskTokens );

        Hashtable newTokens = new Hashtable();

        //Get all tokens

        int index = 0;
        for (index = 0; index < count; index++) {
          uint fetched;
          IVsCommentTaskToken[] commentTaskToken = new IVsCommentTaskToken[1];

          commentTaskTokens.Next( 1, commentTaskToken, out fetched );
          if (fetched == 1) {
            string token = null;
            VSTASKPRIORITY[] priority = new VSTASKPRIORITY[1] { VSTASKPRIORITY.TP_NORMAL };
            commentTaskToken[0].Text(out token);            
            commentTaskToken[0].Priority(priority);
            if (token != null) {
              newTokens[token] = priority[0];
            }
          }
        }
        
        //update the token information
        this.taskTokens = newTokens;

      } catch (Exception) {
        return;
      }
    }

    public bool IsTaskToken( string text, out VSTASKPRIORITY priority ) {
      priority = VSTASKPRIORITY.TP_NORMAL;
      if (text == null) return false;
      if (this.taskTokens == null) return false;

      //extract the token
      int i = 0;
      int len = text.Length;
      while (i < len && Char.IsWhiteSpace(text[i])) {
        i++;
      }

      int start = i;
      while (i < len && !Char.IsWhiteSpace(text[i]) && 
        text[i] != ':' ) { 
        i++;
      }
      if (len == 0) return false;
  
      string token = text.Substring(start, i-start); 

      //check if this is a defined token
      return this.taskTokens.Contains(token);
    }
  };

  //-----------------------------------------------------------*/
  public class TextMarkerClient : IVsTextMarkerClient {
    string tipText;

    public TextMarkerClient(string tipText) {
      this.tipText = tipText;
    }

    public static IVsTextLineMarker CreateMarker( IVsTextLines textLines, TextSpan span, MARKERTYPE mt, string tipText) {

      IVsTextLineMarker marker = null;    
      TextMarkerClient textMarkerClient = new TextMarkerClient( tipText );
      textLines.CreateLineMarker((int)mt, span.iStartLine, span.iStartIndex, 
        span.iEndLine, span.iEndIndex, textMarkerClient, out marker);
      return marker;
    }

    /*---------------------------------------------------------
      IVsTextMarkerClient
    -----------------------------------------------------------*/
    public virtual void MarkerInvalidated(  ) {
      //TRACE("MarkerInvalidated" );
      return;
    }

    public virtual void OnBufferSave(string fileName ) {
      //TRACE1("OnBufferSave: %S", fileName );
      return;
    }

    public virtual void OnBeforeBufferClose(  ) {
      //TRACE("OnBeforeBufferClose" );
      return;
    }

    public virtual void OnAfterSpanReload(  ) {
      //TRACE("OnAfterSpanReload" );
      return;
    }

    public virtual void OnAfterMarkerChange(IVsTextMarker marker) {
      //TRACE("OnAfterMarkerChange" );
      return;
    }

    public virtual void GetTipText( IVsTextMarker marker, string[] tipText ) {
      if (tipText != null) tipText[0] = this.tipText;
    }

    public virtual void GetMarkerCommandInfo(IVsTextMarker marker, int item, string[] text, out uint commandFlags ) {
      commandFlags = 0;
      if (text != null) text[0] = this.tipText;
    }

    public virtual void ExecMarkerCommand(  IVsTextMarker marker, int item ) {
      Trace.WriteLine("ExecMarkerCommand: " + item );
    }
  }
}
