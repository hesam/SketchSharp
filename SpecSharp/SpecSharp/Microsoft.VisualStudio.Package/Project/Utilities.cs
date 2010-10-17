//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Designer.Interfaces;
using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Win32;
using System.Globalization;
using System.Windows.Forms;
using System.IO;
using System.Collections;
using System.Xml;
using System.Text;

using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using IServiceProvider = System.IServiceProvider;

namespace Microsoft.VisualStudio.Package{

	/// <include file='doc\Utilities.uex' path='docs/doc[@for="VsRecordState"]/*' />
  internal enum VsRecordState
  {
		/// <include file='doc\Utilities.uex' path='docs/doc[@for="VsRecordState.On"]/*' />
    On = 1,
		/// <include file='doc\Utilities.uex' path='docs/doc[@for="VsRecordState.Off"]/*' />
    Off = 2,
		/// <include file='doc\Utilities.uex' path='docs/doc[@for="VsRecordState.Paused"]/*' />
    Paused = 3
  }

  internal class TextSpanHelper
  {
    public static bool TextSpanAfterAt(TextSpan span1, TextSpan span2)
    {
      return (span1.iStartLine > span2.iStartLine || (span1.iStartLine == span2.iStartLine && span1.iStartIndex >= span2.iStartIndex));
    }

    public static bool TextSpanEndsBeforeAt(TextSpan span1, TextSpan span2)
    {
      return (span1.iEndLine < span2.iEndLine || (span1.iEndLine == span2.iEndLine && span1.iEndIndex <= span2.iEndIndex));
    }

    public static TextSpan TextSpanMerge(TextSpan span1, TextSpan span2)
    {
      TextSpan span = new TextSpan();

      if (TextSpanAfterAt(span1, span2))
      {
        span.iStartLine = span2.iStartLine;
        span.iStartIndex = span2.iStartIndex;
      }
      else
      {
        span.iStartLine = span1.iStartLine;
        span.iStartIndex = span1.iStartIndex;
      }

      if (TextSpanEndsBeforeAt(span1, span2))
      {
        span.iEndLine = span2.iEndLine;
        span.iEndIndex = span2.iEndIndex;
      }
      else
      {
        span.iEndLine = span1.iEndLine;
        span.iEndIndex = span1.iEndIndex;
      }

      return span;
    }

    public static bool TextSpanPositive(TextSpan span)
    {
      return (span.iStartLine < span.iEndLine || (span.iStartLine == span.iEndLine && span.iStartIndex <= span.iEndIndex));
    }

    public static void ClearTextSpan(ref TextSpan span)
    {
      span.iStartLine = span.iEndLine = -1;
      span.iStartIndex = span.iEndIndex = -1;
    }

    public static bool TextSpanIsEmpty(TextSpan span)
    {
      return (span.iStartLine < 0 || span.iEndLine < 0 || span.iStartIndex < 0 || span.iEndIndex < 0);
    }

    public static void TextSpanMakePositive(ref  TextSpan span)
    {
      if (!TextSpanPositive(span))
      {
        int line;
        int idx;

        line = span.iStartLine;
        idx = span.iStartIndex;
        span.iStartLine = span.iEndLine;
        span.iStartIndex = span.iEndIndex;
        span.iEndLine = line;
        span.iEndIndex = idx;
      }

      return;
    }

    public static void TextSpanNormalize(ref  TextSpan span, IVsTextLines textLines)
    {
      TextSpanMakePositive(ref span);
      if (textLines == null) return;

      //adjust max. lines
      int lineCount;

      try
      {
        textLines.GetLineCount(out lineCount);
      }
      catch (Exception)
      {
        return;
      }
      span.iEndLine = Math.Min(span.iEndLine, lineCount - 1);

      //make sure the start is still before the end
      if (!TextSpanPositive(span))
      {
        span.iStartLine = span.iEndLine;
        span.iStartIndex = span.iEndIndex;
      }

      //adjust for line length
      int lineLength;

      try
      {
        textLines.GetLengthOfLine(span.iStartLine, out lineLength);
      }
      catch (Exception)
      {
        return;
      }
      span.iStartIndex = Math.Min(span.iStartIndex, lineLength);
      try
      {
        textLines.GetLengthOfLine(span.iEndLine, out lineLength);
      }
      catch (Exception)
      {
        return;
      }
      span.iEndIndex = Math.Min(span.iEndIndex, lineLength);
    }

    public static bool IsSameSpan(TextSpan span1, TextSpan span2)
    {
      return span1.iStartLine == span2.iStartLine && span1.iStartIndex == span2.iStartIndex && span1.iEndLine == span2.iEndLine && span1.iEndIndex == span2.iEndIndex;
    }
  }

  internal enum HResult {
    E_UNEXPECTED = unchecked((int)0x8000FFFF),
    E_NOTIMPL = unchecked((int)0x80004001),
    E_OUTOFMEMORY = unchecked((int)0x8007000E),
    E_INVALIDARG = unchecked((int)0x80070057),
    E_NOINTERFACE = unchecked((int)0x80004002),
    E_POINTER = unchecked((int)0x80004003),
    E_HANDLE = unchecked((int)0x80070006),
    E_ABORT = unchecked((int)0x80004004),
    E_FAIL = unchecked((int)0x80004005),
    E_ACCESSDENIED = unchecked((int)0x80070005),
    E_PENDING = unchecked((int)0x8000000A),
    S_OK = 0x00000000,
    S_FALSE = 0x00000001,
  }
  internal class NativeHelpers {
    public static bool Succeeded(int hr) {
      return (hr >= 0);
    }
    public static bool Failed(int hr) {
      return (hr < 0);
    }
    public static void RaiseComError(HResult hr) {
      throw new System.Runtime.InteropServices.COMException("", (int)hr);
    }
    public static void RaiseComError(HResult hr, string message) {
      throw new System.Runtime.InteropServices.COMException(message, (int)hr);
    }
    public static void TraceRef(object obj, string msg) {
        if (obj == null) return;
        IntPtr pUnk = Marshal.GetIUnknownForObject(obj);
        obj = null;
        Marshal.Release(pUnk);
        TraceRef(pUnk, msg);
    }
    public static void TraceRef(IntPtr pUnk, string msg) {
        if (pUnk == IntPtr.Zero) return;
        Marshal.AddRef(pUnk);
        int count = Marshal.Release(pUnk);
//        Trace.WriteLine(msg + ": 0x" + pUnk.ToString("x") + "(ref=" + count + ")");
    }
  }
  internal class CookieEnumerator: IEnumerator {
    object[] map;
    uint used;
    uint pos;
    public CookieEnumerator(object[] map, uint used) {
      this.map = map; this.used = used;
    }
    object IEnumerator.Current {
      get { return (this.pos > 0 && this.pos <= this.used) ? this.map[this.pos - 1] : null; }
    }
    bool IEnumerator.MoveNext() {
      if (this.pos < this.used) {
        this.pos++;
        while (this.pos <= this.used && this.map[this.pos - 1] == null)
          this.pos++;

        if (this.pos <= this.used) {
          return true;
        }
        return false;
      }
      return false;
    }
    void IEnumerator.Reset() {
      this.pos = 0;
    }
  }
  /// <summary>
  /// Maps objects to and from integer "cookies"
  /// </summary>
  internal class CookieMap: IEnumerable {
    object[] map;
    uint size;
    uint used;
    public CookieMap() {
      this.size = 10;
      this.used = 0;
      this.map = new object[size];
    }
    public uint Length { get { return used; } }
    public uint Add(Object o) {
      uint result = this.used;

      if (this.used == this.size) {
        object[] newmap = new object[size * 2];
        System.Array.Copy(this.map, 0, newmap, 0, (int)this.size);
        this.map = newmap;
        this.size *= 2;
      }
      this.map[this.used++] = o;
      return result;
    }
    public void Remove(Object obj) {
      for (uint i = 0; i < this.used; i++) {
        if (this.map[i] == obj) {
          this.map[i] = null; // todo - re-use these gaps.
          return;
        }
      }
      throw new InvalidOperationException("Object not found");
    }
    public void RemoveAt(uint cookie) {
      Debug.Assert(cookie < this.used);
      this.map[cookie] = null;
    }
    public void SetAt(uint cookie, object value) {
      this.map[cookie] = value;
    }
    public object FromCookie(uint cookie) {
      //Debug.Assert(cookie < used);
      if (cookie >= used) return null;
      return this.map[cookie];
    }
    public object this[uint cookie] {
      get {
        //Debug.Assert(cookie < used);
        if (cookie >= used) return null;
        return this.map[cookie];
      }
      set {
        this.map[cookie] = value;
      }
    }
    public void Clear() {
      for (uint i = 0; i < this.used; i++) {
        this.map[i] = null;
      }
      this.used = 0;
    }
    IEnumerator IEnumerable.GetEnumerator() {
      return new CookieEnumerator(map, used);
    }
  }
  public interface ILanguageService{
    void Init(ServiceProvider site, ref Guid languageGuid, uint lcid, string extensions);
    void Done();
    void OnIdle(bool periodic);
  }
  //===========================================================================
  // This custom writer puts attributes on a new line which makes the 
  // .xsproj files easier to read.
  internal class MyXmlWriter: XmlTextWriter {
    int depth;
    TextWriter tw;
    string indent;
    public MyXmlWriter(TextWriter tw) : base(tw) {
      this.tw = tw;
      this.Formatting = Formatting.Indented;
    }
    string IndentString {
      get {
        if (this.indent == null) {
          StringBuilder sb = new StringBuilder();
          for (int j = 0; j < this.Indentation; j++) {
            sb.Append(this.IndentChar);
          }
          this.indent = sb.ToString();
        }
        return this.indent;
      }
    }
    public override void WriteEndAttribute() {
      base.WriteEndAttribute();
      this.tw.WriteLine();
      for (int i = 0; i < depth; i++) {
        this.tw.Write(this.IndentString);
      }
    }
    public override void WriteStartElement(string prefix, string localName, string ns) {
      base.WriteStartElement(prefix, localName, ns);
      this.depth++;
    }
    public override void WriteEndElement() {
      base.WriteEndElement();
      this.depth--;
    }
  }
  //The tracker class will need to implement helpers to CALL IVsTrackProjectDocuments2 for notifications
  internal class TrackDocumentsHelper {
    private CookieMap trackProjectEventSinks = new CookieMap();
    private Project theProject;
    public TrackDocumentsHelper(Project project) {
      theProject = project;
    }
    private IVsTrackProjectDocuments2 getTPD() {
			return theProject.Site.GetService(typeof(SVsTrackProjectDocuments)) as IVsTrackProjectDocuments2;
    }
    public uint Advise(IVsTrackProjectDocumentsEvents2 sink) {
      return this.trackProjectEventSinks.Add(sink);
    }
    public void Unadvise(uint cookie) {
      this.trackProjectEventSinks.RemoveAt(cookie);
    }
    public bool CanAddFiles(string[] files) {
      int len = files.Length;
      VSQUERYADDFILEFLAGS[] flags = new VSQUERYADDFILEFLAGS[len];
      try {
        for (int i = 0; i < files.Length; i++)
          flags[i] = VSQUERYADDFILEFLAGS.VSQUERYADDFILEFLAGS_NoFlags;
        foreach (IVsTrackProjectDocumentsEvents2 sink in this.trackProjectEventSinks) {
          VSQUERYADDFILERESULTS[] summary = new VSQUERYADDFILERESULTS[1];
          sink.OnQueryAddFiles((IVsProject)this, len, files, flags, summary, null);
          if (summary[0] == VSQUERYADDFILERESULTS.VSQUERYADDFILERESULTS_AddNotOK)
            return false;
        }
        return true;
      } catch {
        return false;
      }
    }
    public void OnAddFile(string file) {
      try {
        foreach (IVsTrackProjectDocumentsEvents2 sink in this.trackProjectEventSinks) {
          sink.OnAfterAddFilesEx(1, 1, new IVsProject[1] { (IVsProject)this }, new int[1] { 0 }, new string[1] { file }, new VSADDFILEFLAGS[1] { VSADDFILEFLAGS.VSADDFILEFLAGS_NoFlags });
        }
      } catch {
      }
    }
    /// <summary>
    /// Get's called to ask the environent if a file is allowed to be renamed
    /// </summary>
    /// returns FALSE if the doc can not be renamed
    public bool CanRenameFile(string strOldName, string strNewName) {
      int iCanContinue = 0;
      this.getTPD().OnQueryRenameFile(this.theProject, strOldName, strNewName, VSRENAMEFILEFLAGS.VSRENAMEFILEFLAGS_NoFlags, out iCanContinue);
      return (iCanContinue != 0);
    }
    /// <summary>
    /// Get's called to tell the env that a file was renamed
    /// </summary>
    /// 
    public void AfterRenameFile(string strOldName, string strNewName) {
      this.getTPD().OnAfterRenameFile(this.theProject, strOldName, strNewName, VSRENAMEFILEFLAGS.VSRENAMEFILEFLAGS_NoFlags);
    }
  }
  /*

    // IVsTrackProjectDocuments2
    void IVsTrackProjectDocuments2.AdviseTrackProjectDocumentsEvents(IVsTrackProjectDocumentsEvents2 sink, out uint cookie) {
      cookie = this.tracker.Advise(sink);
    }

    void IVsTrackProjectDocuments2.UnadviseTrackProjectDocumentsEvents(uint cookie) {
      this.tracker.Unadvise(cookie);
    }

    void IVsTrackProjectDocuments2.BeginBatch() {
      //TODO
    }
    void IVsTrackProjectDocuments2.EndBatch() {
      //TODO
    }
    void IVsTrackProjectDocuments2.Flush() {
      //TODO
    }
    void IVsTrackProjectDocuments2.OnAfterAddDirectories(IVsProject p, int count, string[] mkDocuments) {
      //TODO
    }
    void IVsTrackProjectDocuments2.OnAfterAddDirectoriesEx(IVsProject p, int count, string[] mkDocuments, tagVSADDDIRECTORYFLAGS[] f) {
      //TODO
    }

    void IVsTrackProjectDocuments2.OnAfterAddFiles(IVsProject p, int count, string[] mkDocuments) {
      //TODO
    }

    void IVsTrackProjectDocuments2.OnAfterAddFilesEx(IVsProject p, int count, string[] mkDocuments, VSADDFILEFLAGS[] f) {
      //TODO
    }

    void IVsTrackProjectDocuments2.OnAfterRemoveDirectories(IVsProject p, int count, string[] mkDocuments, VSREMOVEDIRECTORYFLAGS[] f) {
      //TODO
    }

    void IVsTrackProjectDocuments2.OnAfterRemoveFiles(IVsProject p, int count, string[] mkDocuments, tagVSREMOVEFILEFLAGS[] f) {
      //TODO
    }

    void IVsTrackProjectDocuments2.OnAfterRenameDirectories(IVsProject p, int count, string[] mkOldNames, string[] newNames, tagVSRENAMEDIRECTORYFLAGS[] f) {
      //TODO
    }

    void IVsTrackProjectDocuments2.OnAfterRenameFiles(IVsProject p, int count, string[] mkOldNames, string[] newNames, VSRENAMEFILEFLAGS[] f) {
      //TODO
    }

    void IVsTrackProjectDocuments2.OnAfterSccStatusChanged(IVsProject p, int count, string[] mkDocuments, uint[] sccStatus) {
      //TODO
    }

    void IVsTrackProjectDocuments2.OnQueryAddDirectories(IVsProject p, int count, string[] mkDocuments, tagVSQUERYADDDIRECTORYFLAGS[] f, out tagVSQUERYADDDIRECTORYRESULTS summary, tagVSQUERYADDDIRECTORYRESULTS[] r) {
      summary = tagVSQUERYADDDIRECTORYRESULTS.VSQUERYADDDIRECTORYRESULTS_AddOK;
    }

    void IVsTrackProjectDocuments2.OnQueryAddFiles(IVsProject p, int count, string[] mkDocuments, tagVSQUERYADDFILEFLAGS[] f, out VSQUERYADDFILERESULTS summary, VSQUERYADDFILERESULTS[] r) {
      summary = VSQUERYADDFILERESULTS.VSQUERYADDFILERESULTS_AddOK;
    }

    void IVsTrackProjectDocuments2.OnQueryRemoveDirectories(IVsProject p, int count, string[] mkDocuments, tagVSQUERYREMOVEDIRECTORYFLAGS[] f, out tagVSQUERYREMOVEDIRECTORYRESULTS summary, tagVSQUERYREMOVEDIRECTORYRESULTS[] r) {
      summary = tagVSQUERYREMOVEDIRECTORYRESULTS.VSQUERYREMOVEDIRECTORYRESULTS_RemoveOK;
    }

    void IVsTrackProjectDocuments2.OnQueryRemoveFiles(IVsProject p, int count, string[] mkDocuments, VSQUERYREMOVEFILEFLAGS[] f, out tagVSQUERYREMOVEFILERESULTS summary, tagVSQUERYREMOVEFILERESULTS[] r) {
      summary = tagVSQUERYREMOVEFILERESULTS.VSQUERYREMOVEFILERESULTS_RemoveOK;
    }

    void IVsTrackProjectDocuments2.OnQueryRenameDirectories(IVsProject p, int count, string[] oldNames, string[] newNames, tagVSQUERYRENAMEDIRECTORYFLAGS[] f, out tagVSQUERYRENAMEDIRECTORYRESULTS summary, tagVSQUERYRENAMEDIRECTORYRESULTS[] r) {
      summary = tagVSQUERYRENAMEDIRECTORYRESULTS.VSQUERYRENAMEDIRECTORYRESULTS_RenameOK;
    }
    void IVsTrackProjectDocuments2.OnQueryRenameFiles(IVsProject p, int count, string[] oldNames, string[] newNames, VSQUERYRENAMEFILEFLAGS[] f, out tagVSQUERYRENAMEFILERESULTS summary, tagVSQUERYRENAMEFILERESULTS[] r) {
      summary = tagVSQUERYRENAMEFILERESULTS.VSQUERYRENAMEFILERESULTS_RenameOK;
    }

*/
  [StructLayoutAttribute(LayoutKind.Sequential)]
  internal struct _DROPFILES {
    public Int32 pFiles;
    public Int32 X;
    public Int32 Y;
    public Int32 fNC;
    public Int32 fWide;
  }
  internal enum OleErrors: uint {
    OLE_E_FIRST = 0x80040000,
    OLE_E_OLEVERB = 0x80040000,
    OLE_E_ADVF = 0x80040001,
    OLE_E_ENUM_NOMORE = 0x80040002,
    OLE_E_ADVISENOTSUPPORTED = 0x80040003,
    OLE_E_NOCONNECTION = 0x80040004,
    OLE_E_NOTRUNNING = 0x80040005,
    OLE_E_NOCACHE = 0x80040006,
    OLE_E_BLANK = 0x80040007,
    OLE_E_CLASSDIFF = 0x80040008,
    OLE_E_CANT_GETMONIKER = 0x80040009,
    OLE_E_CANT_BINDTOSOURCE = 0x8004000A,
    OLE_E_STATIC = 0x8004000B,
    OLE_E_PROMPTSAVECANCELLED = 0x8004000C,
    OLE_E_INVALIDRECT = 0x8004000D,
    OLE_E_WRONGCOMPOBJ = 0x8004000E,
    OLE_E_INVALIDHWND = 0x8004000F,
    OLE_E_NOT_INPLACEACTIVE = 0x80040010,
    OLE_E_CANTCONVERT = 0x80040011,
    OLE_E_NOSTORAGE = 0x80040012,
    OLE_E_LAST = 0x800400FF,
  }
  // winerr.h, docobj.h
  internal enum OleDocumentError: uint {
    OLECMDERR_E_FIRST = (OleErrors.OLE_E_LAST + 1),
    OLECMDERR_E_NOTSUPPORTED = (OLECMDERR_E_FIRST),
    OLECMDERR_E_UNKNOWNGROUP = (OLECMDERR_E_FIRST + 4)
  }
  internal enum OleDispatchErrors: uint {
    DISP_E_UNKNOWNINTERFACE = 0x80020001,
    DISP_E_MEMBERNOTFOUND = 0x80020003,
    DISP_E_PARAMNOTFOUND = 0x80020004,
    DISP_E_TYPEMISMATCH = 0x80020005,
    DISP_E_UNKNOWNNAME = 0x80020006,
    DISP_E_NONAMEDARGS = 0x80020007,
    DISP_E_BADVARTYPE = 0x80020008,
    DISP_E_EXCEPTION = 0x80020009,
    DISP_E_OVERFLOW = 0x8002000A,
    DISP_E_BADINDEX = 0x8002000B,
    DISP_E_UNKNOWNLCID = 0x8002000C,
    DISP_E_ARRAYISLOCKED = 0x8002000D,
    DISP_E_BADPARAMCOUNT = 0x8002000E,
    DISP_E_PARAMNOTOPTIONAL = 0x8002000F,
    DISP_E_BADCALLEE = 0x80020010,
    DISP_E_NOTACOLLECTION = 0x80020011,
    DISP_E_DIVBYZERO = 0x80020012,
    DISP_E_BUFFERTOOSMALL = 0x80020013,
  }
  internal enum OLECRF {
    olecrfNeedIdleTime = 1,
    olecrfNeedPeriodicIdleTime = 2,
    olecrfPreTranslateKeys = 4,
    olecrfPreTranslateAll = 8,
    olecrfNeedSpecActiveNotifs = 16,
    olecrfNeedAllActiveNotifs = 32,
    olecrfExclusiveBorderSpace = 64,
    olecrfExclusiveActivation = 128
  }
  internal enum OLECADVF {
    olecadvfModal = 1,
    olecadvfRedrawOff = 2,
    olecadvfWarningsOff = 4,
    olecadvfRecording = 8
  }
  internal enum NativeBoolean: int {
    True = 1,
    False = 0
  }
  /// <summary>
  /// helper to make the editor ignore external changes
  /// </summary>
  /// returns FALSE if the doc can not be renamed
  internal class SuspendFileChanges {
    private string strDocumentFileName;
    private bool fSuspending;
    private IServiceProvider site;
    private IVsDocDataFileChangeControl fileChangeControl;
    public SuspendFileChanges(IServiceProvider site, string strDocument) {
      this.site = site;
      this.strDocumentFileName = strDocument;
      this.fSuspending = false;
      this.fileChangeControl = null;
    }
    public void Suspend() {
      if (this.fSuspending)
        return;

      try {
				IVsRunningDocumentTable pRDT = this.site.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
        IntPtr docData;
        IVsHierarchy pIVsHierarchy;
        uint itemId;
        uint uiVsDocCookie;
        IVsFileChangeEx vsFileChange;


        if (pRDT == null) return;

        pRDT.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, this.strDocumentFileName, out pIVsHierarchy, out itemId, out docData, out uiVsDocCookie);

        if ((uiVsDocCookie == VsConstants.VSDOCCOOKIE_NIL) || docData == IntPtr.Zero)
          return;

				vsFileChange = this.site.GetService(typeof(SVsFileChangeEx)) as IVsFileChangeEx;

        if (vsFileChange != null) {
          this.fSuspending = true;
          vsFileChange.IgnoreFile(0, this.strDocumentFileName, (int)1);
          if (docData != IntPtr.Zero) {
            try {
              // if interface is not supported, return null
              IVsPersistDocData ppIVsPersistDocData = (IVsPersistDocData)Marshal.GetObjectForIUnknown(docData);
              this.fileChangeControl = (IVsDocDataFileChangeControl)ppIVsPersistDocData;
              if (this.fileChangeControl != null) {
                this.fileChangeControl.IgnoreFileChanges(1);
              }
            } catch {
            }
            ;
          }
        }
      } catch {
      }
      return;
    }
    public void Resume() {
      if (!this.fSuspending)
        return;
      try {
        IVsFileChangeEx vsFileChange;
				vsFileChange = this.site.GetService(typeof(SVsFileChangeEx)) as IVsFileChangeEx;
        if (vsFileChange != null) {
          vsFileChange.IgnoreFile(0, this.strDocumentFileName, 0);
          if (this.fileChangeControl != null) {
            this.fileChangeControl.IgnoreFileChanges(0);
          }
        }
      } catch {
      }
    }
  }

  /// <summary>
  /// A CLR friendly wrapper for an OLE service provider
  /// </summary>
  public class ServiceProvider : IServiceProvider, IOleServiceProvider{
    private IOleServiceProvider site;

    public ServiceProvider(Microsoft.VisualStudio.OLE.Interop.IServiceProvider site){
      this.site = site;
    }
    public void Dispose(){
      site = null;
    }

    /// <include file='doc\NativeMethods.uex' path='docs/doc[@for="NativeMethods.IID_IUnknown"]/*' />
    public static readonly Guid IID_IUnknown = new Guid("{00000000-0000-0000-C000-000000000046}");                    

    /// <summary>
    /// Returns an object implementing the service specified by serviceContract
    /// </summary>
    /// <param name="sid">The service being requested</param>
    /// <param name="t">The interface representing the contract the service must implement</param>
    public object QueryService(Guid sid, Type serviceContract){
      if (site == null) return null;
      Guid iid = IID_IUnknown;
      IntPtr p;
      this.site.QueryService(ref sid, ref iid, out p);
      if (p == IntPtr.Zero) return null;
      object result = null;
      try {
        result = Marshal.GetObjectForIUnknown(p);
      } finally{
        Marshal.Release(p);
      }
      return result;
    }
    public virtual object GetService(Type service){
      if (service == typeof(Microsoft.VisualStudio.OLE.Interop.IServiceProvider))
        return this.site;
      return this.QueryService(service.GUID, service);
    }
    internal IntPtr QueryInterface(ref Guid guid){
      IntPtr pUnk = Marshal.GetIUnknownForObject(this.site);
      IntPtr pSite;
      try {
        Marshal.QueryInterface(pUnk, ref guid, out pSite);
      } finally {
        Marshal.Release(pUnk);
      }
      return pSite;
    }
    int IOleServiceProvider.QueryService(ref System.Guid guidService, ref System.Guid riid, out System.IntPtr ppvObject){
      return this.site.QueryService(ref guidService, ref riid, out ppvObject);
    }
    /// <summary>
    /// Returns the wrapped OLE service provider
    /// </summary>
    public Microsoft.VisualStudio.OLE.Interop.IServiceProvider Unwrap(){
      return this.site;
    }
  }

  public enum IndentingStyle {
    None, Block, Smart
  }

  internal class VsConstants {

    public const int CLSCTX_INPROC_SERVER  = 0x1;

    public static Guid SID_SVsRunningDocumentTable = new Guid("A928AA21-EA77-47AC-8A07-355206C94BDD");
    public static Guid SID_VsUIShellOpenDocument = new Guid("35299EEC-11EE-4518-9F08-401638D1D3BC");
    public static Guid guidShellIID = new Guid("B61FC35B-EEBF-4DEC-BFF1-28A2DD43C38F");

    // menu command guids.
    public static Guid guidStandardCommandSet97 = new Guid("5efc7975-14bc-11cf-9b2b-00aa00573819");
    public static Guid guidStandardCommandSet2K = new Guid("1496A755-94DE-11D0-8C3F-00C04FC2AAE2");
    public static Guid guidVsVbaPkg = new Guid( 0xa659f1b3, 0xad34, 0x11d1, 0xab, 0xad, 0x0, 0x80, 0xc7, 0xb8, 0x9c, 0x95 );


    public static Guid guidSHLMainMenu = new Guid(0xd309f791, 0x903f, 0x11d0, 0x9e, 0xfc, 0x00, 0xa0, 0xc9, 0x11, 0x00, 0x4f);
    public static Guid guidVSUISet = new Guid("60481700-078b-11d1-aaf8-00a0c9055a90");
    public static Guid guidCciSet = new Guid("2805D6BD-47A8-4944-8002-4e29b9ac2269");
    public static Guid guidSourceControl = new Guid("aa8eb8cd-7a51-11d0-92c3-00a0c9138c45");
    public static Guid guidVsUIHierarchyWindowCmds = new Guid("60481700-078B-11D1-AAF8-00A0C9055A90");

    public static Guid IID_ILocalRegistry3 = typeof(ILocalRegistry3).GUID;
    public static Guid SID_SLocalRegistry = new Guid("6D5140D3-7436-11CE-8034-00AA006009FA");
    public static Guid CLSID_VsTextBuffer = new Guid("8E7B96A8-E33D-11D0-A6D5-00C04FB67F6A");
    public static Guid IID_IVsTextLines = typeof(IVsTextLines).GUID;
    public static Guid CLSID_VsCodeWindow = new Guid("F5E7E719-1401-11D1-883B-0000F87579D2");
    public static Guid IID_IVsCodeWindow = typeof(IVsCodeWindow).GUID;
    public static Guid SID_IVsOutputWindow = new Guid("533FAD11-FE7F-41EE-A381-8B67792CD692");

    //public static Guid IID_IVsHelp = typeof(IVsHelp).GUID;
    //public static Guid SID_SVsHelp = IID_IVsHelp;

    public static Guid IID_IVsTaskList = typeof(IVsTaskList).GUID;
    public static Guid SID_SVsTaskList = IID_IVsTaskList;

    public static Guid IID_IVsToolbox = typeof(IVsToolbox).GUID;
    public static Guid SID_SVsToolbox = IID_IVsToolbox;

    // fm: switched to the 2K standard set. Renamed this to guidVSStdSet, instead of
    // a version dependant global name.
    public static Guid guidCOMPLUSLibrary = new Guid(0x1ec72fd7, 0xc820, 0x4273, 0x9a, 0x21, 0x77, 0x7a, 0x5c, 0x52, 0x2e, 0x03);

    public static Guid SID_SVsFileChangeEx = new Guid("9BC72973-194A-4EA8-B4D5-AFB0B0D0DCB1");
    public static Guid SID_SVsSolutionBuildManager = new Guid("93E969D6-1AA0-455F-B208-6ED3C82B5C58");
    public static Guid SID_SVsObjBrowser = new Guid("0DF98187-FD9A-4669-8A56-727910A4866C");
    public static Guid SID_SVsComplusLibrary = new Guid("699D5E17-9B22-466b-ACFA-2E12CD64E249");

    public static Guid Guid_SolutionExplorer = new Guid("3AE79031-E1BC-11D0-8F78-00A0C9110057");
    public static Guid SID_SVsSolution = new Guid("7F7CD0DB-91EF-49DC-9FA9-02D128515DD4");
    public static Guid SID_SVsShellMonitorSelection = new Guid("55AB9450-F9C7-4305-94E8-BEF12065338D");
    public static Guid SID_SVsComponentSelectorDlg = new Guid("66899421-F497-4503-8C9D-ADAE290F2F27");
    //public static Guid SID_SVsAddProjectItemDlg2 = new Guid("6B90D260-E363-4e8a-AE51-BD19C493416D");
    public static Guid SID_SVsAddProjectItemDlg = new Guid("11DFCCEB-D935-4a9f-9796-5BA433C5AF8E");

    public static Guid SID_SVsRegisterEditors = typeof(IVsRegisterEditors).GUID; 
    public static Guid SID_IVsRegisterProjectTypes = typeof(IVsRegisterProjectTypes).GUID;

    public static Guid IID_SVsTrackProjectDocuments2 = new Guid("53544C4D-6639-11d3-a60d-005004775ab1");
    public static Guid SID_SVsTrackProjectDocuments = new Guid("53544C4D-1639-11d3-a60d-005004775ab1");

    public static Guid CLSID_VsTextManager = new Guid("F5E7E71D-1401-11D1-883B-0000F87579D2");
    public static Guid IID_IVsTextManager = typeof(IVsTextManager).GUID;
    public static Guid SID_SVsTextManager = CLSID_VsTextManager;

    public static Guid IID_ITrackSelection = typeof(ITrackSelection).GUID;
    public static Guid SID_STrackSelection = IID_ITrackSelection;

    public static Guid IID_IVsUIShellOpenDocument = typeof(IVsUIShellOpenDocument).GUID;
    public static Guid SID_SVsUIShellOpenDocument = IID_IVsUIShellOpenDocument;
  
    public static Guid IID_IVsFinalTextChangeCommitEvents = typeof(IVsFinalTextChangeCommitEvents).GUID;
    public static Guid IID_IVsTextLinesEvents = typeof(IVsTextLinesEvents).GUID;
    public static Guid IID_IVsTextBufferDataEvents = typeof(IVsTextBufferDataEvents).GUID;

    public static Guid CLSID_VsMethodTipWindow = new Guid(0x261A5572,0xC649,0x11D0,0xA8,0xDF,0x00,0xA0,0xC9,0x21,0xA4,0xD2);
    public static Guid IID_IVsMethodTipWindow = typeof(IVsMethodTipWindow).GUID;

    public static Guid CLSID_VsTextTipWindow = new Guid(0x05DD7650,0x130A,0x11D3,0xAF,0xCB,0x00,0x10,0x5A,0x99,0x91,0xEF);
    public static Guid IID_IVsTextTipWindow = typeof(IVsTextTipWindow).GUID;

    public static Guid SID_SOleComponentManager = new Guid(0x000C060B,0x0000,0x0000,0xC0,0x00,0x00,0x00,0x00,0x00,0x00,0x46);
    public static Guid IID_IOleComponentManager = new Guid(0x000C0601,0x0000,0x0000,0xC0,0x00,0x00,0x00,0x00,0x00,0x00,0x46);

    public static Guid SID_SUIHostLocale = new Guid(0x2C2EA031,0x02BE,0x11D1,0x8C,0x85,0x00,0xC0,0x4F,0xC2,0xAA,0x89);
    public static Guid IID_IUIHostLocale = new Guid(0x2C2EA031,0x02BE,0x11D1,0x8C,0x85,0x00,0xC0,0x4F,0xC2,0xAA,0x89);

    public static Guid SID_SVsShellDebugger = new Guid(0x7D960B16,0x7AF8,0x11D0,0x8E,0x5E,0x00,0xA0,0xC9,0x11,0x00,0x5A);

    public static Guid SID_ProfferService = typeof(IProfferService).GUID;

    // Predefined logical views...
    public static Guid LOGVIEWID_Any = new Guid( 0xffffffff, 0xffff, 0xffff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff );
    public static Guid LOGVIEWID_Debugging = new Guid( 0x7651a700, 0x06e5, 0x11d1, 0x8e, 0xbd, 0x00, 0xa0, 0xc9, 0x0f, 0x26, 0xea );
    public static Guid LOGVIEWID_Code = new Guid( 0x7651a701, 0x06e5, 0x11d1, 0x8e, 0xbd, 0x00, 0xa0, 0xc9, 0x0f, 0x26, 0xea );
    public static Guid LOGVIEWID_Designer = new Guid( 0x7651a702, 0x06e5, 0x11d1, 0x8e, 0xbd, 0x00, 0xa0, 0xc9, 0x0f, 0x26, 0xea );
    public static Guid LOGVIEWID_TextView = new Guid( 0x7651a703, 0x06e5, 0x11d1, 0x8e, 0xbd, 0x00, 0xa0, 0xc9, 0x0f, 0x26, 0xea );
    public static Guid LOGVIEWID_UserChooseView = new Guid( 0x7651a704, 0x06e5, 0x11d1, 0x8e, 0xbd, 0x00, 0xa0, 0xc9, 0x0f, 0x26, 0xea );
    public static Guid LOGVIEWID_ProjectSpecificEditor = new Guid( 0x80a3471a, 0x6b87, 0x433e, 0xa7, 0x5a, 0x9d, 0x46, 0x1d, 0xe0, 0x64, 0x5f );

    public static uint VSITEMID_ROOT = ForceCast(-2);
    public static uint VSITEMID_NIL = ForceCast(-1);
    public static uint VSITEMID_SELECTION = ForceCast(-3);
    public static uint VSDOCCOOKIE_NIL = 0;

    public static uint VS_E_PACKAGENOTLOADED = 0x80041fe1;
    public static uint VS_E_PROJECTNOTLOADED = 0x80041fe2;
    public static uint VS_E_SOLUTIONNOTOPEN = 0x80041fe3;
    public static uint VS_E_SOLUTIONALREADYOPEN = 0x80041fe4;
    public static uint VS_E_PROJECTMIGRATIONFAILED = 0x80041fe5;
    public static uint VS_E_INCOMPATIBLEDOCDATA = 0x80041fea;
    public static uint VS_E_UNSUPPORTEDFORMAT = 0x80041feb;
    public static uint VS_E_WIZARDBACKBUTTONPRESS = 0x80041fff;
    public static uint VS_S_PROJECTFORWARDED = 0x41ff0;
    public static uint VS_S_TBXMARKER = 0x41ff1;

    // Special Menus.
    public const int IDM_VS_CTXT_CODEWIN   = 0x040D;
    public const int IDM_VS_CTXT_ITEMNODE = 0x0430;
    public const int IDM_VS_CTXT_PROJNODE = 0x0402;
    public const int IDM_VS_CTXT_REFERENCEROOT = 0x0450;
    public const int IDM_VS_CTXT_REFERENCE = 0x0451;
    public const int IDM_VS_CTXT_FOLDERNODE  = 0x0431;
    public const int IDM_VS_CTXT_NOCOMMANDS  = 0x041A;

    // For lang services...
    public static Guid GUID_VsBufferDetectLangSID = new Guid(0x17F375AC, 0xC814, 0x11D1, 0x88, 0xAD, 0x00, 0x00, 0xF8, 0x75, 0x79, 0xD2);
    public static Guid GUID_VsBufferMoniker = typeof(IVsUserData).GUID;

    public static uint ForceCast(int i) {
      unchecked { return (uint)i; } 
    }   
  }

  internal class NativeMethods {

    public const int CLSCTX_INPROC_SERVER  = 0x1;

    public static Guid SID_SVsRunningDocumentTable = new Guid("A928AA21-EA77-47AC-8A07-355206C94BDD");
    public static Guid SID_VsUIShellOpenDocument = new Guid("35299EEC-11EE-4518-9F08-401638D1D3BC");
    public static Guid guidShellIID = new Guid("B61FC35B-EEBF-4DEC-BFF1-28A2DD43C38F");

    // menu command guids.
    public static Guid guidStandardCommandSet97 = new Guid("5efc7975-14bc-11cf-9b2b-00aa00573819");
    public static Guid guidStandardCommandSet2K = new Guid("1496A755-94DE-11D0-8C3F-00C04FC2AAE2");
    public static Guid guidVsVbaPkg = new Guid( 0xa659f1b3, 0xad34, 0x11d1, 0xab, 0xad, 0x0, 0x80, 0xc7, 0xb8, 0x9c, 0x95 );


    public static Guid guidSHLMainMenu = new Guid(0xd309f791, 0x903f, 0x11d0, 0x9e, 0xfc, 0x00, 0xa0, 0xc9, 0x11, 0x00, 0x4f);
    public static Guid guidVSUISet = new Guid("60481700-078b-11d1-aaf8-00a0c9055a90");
    public static Guid guidCciSet = new Guid("2805D6BD-47A8-4944-8002-4e29b9ac2269");
    public static Guid guidSourceControl = new Guid("aa8eb8cd-7a51-11d0-92c3-00a0c9138c45");
    public static Guid guidVsUIHierarchyWindowCmds = new Guid("60481700-078B-11D1-AAF8-00A0C9055A90");

    public static Guid IID_ILocalRegistry3 = typeof(ILocalRegistry3).GUID;
    public static Guid SID_SLocalRegistry = new Guid("6D5140D3-7436-11CE-8034-00AA006009FA");
    public static Guid CLSID_VsTextBuffer = new Guid("8E7B96A8-E33D-11D0-A6D5-00C04FB67F6A");
    public static Guid IID_IVsTextLines = typeof(IVsTextLines).GUID;
    public static Guid CLSID_VsCodeWindow = new Guid("F5E7E719-1401-11D1-883B-0000F87579D2");
    public static Guid IID_IVsCodeWindow = typeof(IVsCodeWindow).GUID;

    //public static Guid IID_IVsHelp = typeof(IVsHelp).GUID;
    //public static Guid SID_SVsHelp = IID_IVsHelp;

    public static Guid IID_IVsTaskList = typeof(IVsTaskList).GUID;
    public static Guid SID_SVsTaskList = IID_IVsTaskList;

    public static Guid IID_IVsToolbox = typeof(IVsToolbox).GUID;
    public static Guid SID_SVsToolbox = IID_IVsToolbox;

    // fm: switched to the 2K standard set. Renamed this to guidVSStdSet, instead of
    // a version dependant global name.
    public static Guid guidCOMPLUSLibrary = new Guid(0x1ec72fd7, 0xc820, 0x4273, 0x9a, 0x21, 0x77, 0x7a, 0x5c, 0x52, 0x2e, 0x03);

    public static Guid SID_SVsFileChangeEx = new Guid("9BC72973-194A-4EA8-B4D5-AFB0B0D0DCB1");
    public static Guid SID_SVsSolutionBuildManager = new Guid("93E969D6-1AA0-455F-B208-6ED3C82B5C58");
    public static Guid SID_SVsObjBrowser = new Guid("0DF98187-FD9A-4669-8A56-727910A4866C");
    public static Guid SID_SVsComplusLibrary = new Guid("699D5E17-9B22-466b-ACFA-2E12CD64E249");

    public static Guid Guid_SolutionExplorer = new Guid("3AE79031-E1BC-11D0-8F78-00A0C9110057");
    public static Guid SID_SVsSolution = new Guid("7F7CD0DB-91EF-49DC-9FA9-02D128515DD4");
    public static Guid SID_SVsShellMonitorSelection = new Guid("55AB9450-F9C7-4305-94E8-BEF12065338D");
    public static Guid SID_SVsComponentSelectorDlg = new Guid("66899421-F497-4503-8C9D-ADAE290F2F27");
    //public static Guid SID_SVsAddProjectItemDlg2 = new Guid("6B90D260-E363-4e8a-AE51-BD19C493416D");
    public static Guid SID_SVsAddProjectItemDlg = new Guid("11DFCCEB-D935-4a9f-9796-5BA433C5AF8E");

    public static Guid SID_SVsRegisterEditors = typeof(IVsRegisterEditors).GUID; 
    public static Guid SID_IVsRegisterProjectTypes = typeof(IVsRegisterProjectTypes).GUID;

    public static Guid IID_SVsTrackProjectDocuments2 = new Guid("53544C4D-6639-11d3-a60d-005004775ab1");
    public static Guid SID_SVsTrackProjectDocuments = new Guid("53544C4D-1639-11d3-a60d-005004775ab1");

    public static Guid CLSID_VsTextManager = new Guid("F5E7E71D-1401-11D1-883B-0000F87579D2");
    public static Guid IID_IVsTextManager = typeof(IVsTextManager).GUID;
    public static Guid SID_SVsTextManager = CLSID_VsTextManager;

    public static Guid IID_ITrackSelection = typeof(ITrackSelection).GUID;
    public static Guid SID_STrackSelection = IID_ITrackSelection;

    public static Guid IID_IVsUIShellOpenDocument = typeof(IVsUIShellOpenDocument).GUID;
    public static Guid SID_SVsUIShellOpenDocument = IID_IVsUIShellOpenDocument;
  
    public static Guid IID_IVsFinalTextChangeCommitEvents = typeof(IVsFinalTextChangeCommitEvents).GUID;
    public static Guid IID_IVsTextLinesEvents = typeof(IVsTextLinesEvents).GUID;
    public static Guid IID_IVsTextBufferDataEvents = typeof(IVsTextBufferDataEvents).GUID;

    public static Guid CLSID_VsMethodTipWindow = new Guid(0x261A5572,0xC649,0x11D0,0xA8,0xDF,0x00,0xA0,0xC9,0x21,0xA4,0xD2);
    public static Guid IID_IVsMethodTipWindow = typeof(IVsMethodTipWindow).GUID;

    public static Guid CLSID_VsTextTipWindow = new Guid(0x05DD7650,0x130A,0x11D3,0xAF,0xCB,0x00,0x10,0x5A,0x99,0x91,0xEF);
    public static Guid IID_IVsTextTipWindow = typeof(IVsTextTipWindow).GUID;

    public static Guid SID_SOleComponentManager = new Guid(0x000C060B,0x0000,0x0000,0xC0,0x00,0x00,0x00,0x00,0x00,0x00,0x46);
    public static Guid IID_IOleComponentManager = new Guid(0x000C0601,0x0000,0x0000,0xC0,0x00,0x00,0x00,0x00,0x00,0x00,0x46);

    public static Guid SID_SUIHostLocale = new Guid(0x2C2EA031,0x02BE,0x11D1,0x8C,0x85,0x00,0xC0,0x4F,0xC2,0xAA,0x89);
    public static Guid IID_IUIHostLocale = new Guid(0x2C2EA031,0x02BE,0x11D1,0x8C,0x85,0x00,0xC0,0x4F,0xC2,0xAA,0x89);

    public static Guid SID_SVsShellDebugger = new Guid(0x7D960B16,0x7AF8,0x11D0,0x8E,0x5E,0x00,0xA0,0xC9,0x11,0x00,0x5A);

    public static Guid SID_ProfferService = typeof(IProfferService).GUID;

    // Predefined logical views...
    public static Guid LOGVIEWID_Any = new Guid( 0xffffffff, 0xffff, 0xffff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff );
    public static Guid LOGVIEWID_Debugging = new Guid( 0x7651a700, 0x06e5, 0x11d1, 0x8e, 0xbd, 0x00, 0xa0, 0xc9, 0x0f, 0x26, 0xea );
    public static Guid LOGVIEWID_Code = new Guid( 0x7651a701, 0x06e5, 0x11d1, 0x8e, 0xbd, 0x00, 0xa0, 0xc9, 0x0f, 0x26, 0xea );
    public static Guid LOGVIEWID_Designer = new Guid( 0x7651a702, 0x06e5, 0x11d1, 0x8e, 0xbd, 0x00, 0xa0, 0xc9, 0x0f, 0x26, 0xea );
    public static Guid LOGVIEWID_TextView = new Guid( 0x7651a703, 0x06e5, 0x11d1, 0x8e, 0xbd, 0x00, 0xa0, 0xc9, 0x0f, 0x26, 0xea );
    public static Guid LOGVIEWID_UserChooseView = new Guid( 0x7651a704, 0x06e5, 0x11d1, 0x8e, 0xbd, 0x00, 0xa0, 0xc9, 0x0f, 0x26, 0xea );
    public static Guid LOGVIEWID_ProjectSpecificEditor = new Guid( 0x80a3471a, 0x6b87, 0x433e, 0xa7, 0x5a, 0x9d, 0x46, 0x1d, 0xe0, 0x64, 0x5f );

    public static uint VSDOCCOOKIE_NIL = 0;

    public static uint VS_E_PACKAGENOTLOADED = 0x80041fe1;
    public static uint VS_E_PROJECTNOTLOADED = 0x80041fe2;
    public static uint VS_E_SOLUTIONNOTOPEN = 0x80041fe3;
    public static uint VS_E_SOLUTIONALREADYOPEN = 0x80041fe4;
    public static uint VS_E_PROJECTMIGRATIONFAILED = 0x80041fe5;
    public static uint VS_E_INCOMPATIBLEDOCDATA = 0x80041fea;
    public static uint VS_E_UNSUPPORTEDFORMAT = 0x80041feb;
    public static uint VS_E_WIZARDBACKBUTTONPRESS = 0x80041fff;
    public static uint VS_S_PROJECTFORWARDED = 0x41ff0;
    public static uint VS_S_TBXMARKER = 0x41ff1;

    // Special Menus.
    public const int IDM_VS_CTXT_CODEWIN   = 0x040D;
    public const int IDM_VS_CTXT_ITEMNODE = 0x0430;
    public const int IDM_VS_CTXT_PROJNODE = 0x0402;
    public const int IDM_VS_CTXT_REFERENCEROOT = 0x0450;
    public const int IDM_VS_CTXT_REFERENCE = 0x0451;
    public const int IDM_VS_CTXT_FOLDERNODE  = 0x0431;
    public const int IDM_VS_CTXT_NOCOMMANDS  = 0x041A;
  }

  // This class provides some useful helper static methods
  internal class VsShell {

    /// <summary>
    /// Please use this "approved" method to compare file names.
    /// </summary>
    public static bool IsSamePath(string file1, string file2) {
      return 0 == String.Compare(file1, file2, true, System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string GetFilePath(IVsTextLines textLines) {
      IntPtr pUnk = Marshal.GetIUnknownForObject(textLines);
      try {
          return GetFilePath(pUnk);
      } finally {
          Marshal.Release(pUnk);
      }
    }
    public static string GetFilePath(IntPtr pUnk) {
      try {
        IVsUserData ud = (IVsUserData)Marshal.GetTypedObjectForIUnknown(pUnk, typeof(IVsUserData));
        if (ud != null) {
          object oname;
          ud.GetData(ref VsConstants.GUID_VsBufferMoniker, out oname);
          if (oname != null) return oname.ToString();
        }
      } catch (InvalidCastException) {
      } catch (COMException) {
      }
      try {
        IPersistFileFormat fileFormat = (IPersistFileFormat)Marshal.GetTypedObjectForIUnknown(pUnk, typeof(IPersistFileFormat));
        if (fileFormat != null) {
          uint format;
          string fname;
          fileFormat.GetCurFile(out fname, out format);
          return fname;
        }
      } catch (InvalidCastException) {
      } catch (COMException) {
      }
      return null;
    }

    /*---------------------------------------------------------
      Connection points
    -----------------------------------------------------------*/
    public static uint Connect( object eventSource, object eventSink, ref Guid eventIID) {
      uint cookie = 0;
      try {
        IConnectionPointContainer  connectContainer = (IConnectionPointContainer)eventSource;
        IConnectionPoint           connectionPoint     = null;
        connectContainer.FindConnectionPoint( ref eventIID, out connectionPoint);      
        if (connectionPoint != null) {
          connectionPoint.Advise(eventSink, out cookie);
        }
      } catch (Exception) {
        return 0;
      }
      return cookie;
    }

    public static void DisConnect( object eventSource, ref Guid eventIID, uint cookie ) {

      IConnectionPointContainer  connectContainer = (IConnectionPointContainer)eventSource;

      IConnectionPoint           connectPoint;
      connectContainer.FindConnectionPoint(ref eventIID, out connectPoint);

      connectPoint.Unadvise(cookie);
    }

    public static IntPtr CreateInstance(IServiceProvider provider, ref Guid clsid, ref Guid iid) {
      ILocalRegistry3 localRegistry = provider.GetService(typeof(ILocalRegistry)) as ILocalRegistry3;
      IntPtr pUnk;
      localRegistry.CreateInstance(clsid, null, ref iid, VsConstants.CLSCTX_INPROC_SERVER, out pUnk);
      localRegistry = null;
      return pUnk;
    }

    public static object CreateInstance(IServiceProvider provider, ref Guid clsid, ref Guid iid, Type t) {
      object result = null;
      IntPtr pUnk = VsShell.CreateInstance(provider, ref clsid, ref iid);
      if (pUnk != IntPtr.Zero) {
          try {
              result = Marshal.GetTypedObjectForIUnknown(pUnk, t);
          } finally {
              Marshal.Release(pUnk);
          }
      }
      return result;
    }

    public static uint GetProviderLocale( IServiceProvider provider ) {
      CultureInfo ci = CultureInfo.CurrentCulture;
      uint lcid = (uint)ci.LCID;

      if (provider != null) {
        try {
					IUIHostLocale locale = provider.GetService(typeof(IUIHostLocale)) as IUIHostLocale;
          locale.GetUILocale(out lcid );
        } catch (Exception) {
        }
      }
      return lcid;
    }


    public static void OpenItem(IServiceProvider site, bool newFile, bool openWith, uint editorFlags, ref Guid editorType, string physicalView, ref Guid logicalView, IntPtr punkDocDataExisting, IVsHierarchy pHierarchy, uint hierarchyId, out IVsWindowFrame windowFrame) {
      windowFrame = null;
      IntPtr docData = punkDocDataExisting;
      try {
        uint itemid = hierarchyId;

        object pvar;        
        pHierarchy.GetProperty(hierarchyId, (int)__VSHPROPID.VSHPROPID_Caption, out pvar);
        string caption = (string)pvar;

        string fullPath = null;
        
        if (punkDocDataExisting != IntPtr.Zero && punkDocDataExisting.ToInt64() != -1L) {
          fullPath = VsShell.GetFilePath(punkDocDataExisting);
        } 
        if (fullPath == null) {
          string dir;
          pHierarchy.GetProperty((uint)VsConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ProjectDir, out pvar);
          dir = (string)pvar;
          pHierarchy.GetProperty(hierarchyId, (int)__VSHPROPID.VSHPROPID_SaveName, out pvar);          
          // todo: what if the path is a URL?
          fullPath = dir != null ? Path.Combine(dir,(string)pvar) : (string)pvar;
        }

        HierarchyNode node = pHierarchy as HierarchyNode;
        IVsUIHierarchy pRootHierarchy = node == null ? (IVsUIHierarchy) pHierarchy : node.projectMgr;
        IVsUIHierarchy pVsUIHierarchy = pRootHierarchy;

				IVsUIShellOpenDocument doc = site.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
        const uint   OSE_ChooseBestStdEditor  = 0x20000000;
        const uint OSE_UseOpenWithDialog  = 0x10000000;
        const uint OSE_OpenAsNewFile  = 0x40000000;

        if (openWith) {          
					IOleServiceProvider psp = site.GetService(typeof(IOleServiceProvider)) as IOleServiceProvider;
					doc.OpenStandardEditor(OSE_UseOpenWithDialog, fullPath, ref logicalView, caption, pVsUIHierarchy, itemid, docData, psp, out windowFrame);
        } else {
          // First we see if someone else has opened the requested view of the file.
					IVsRunningDocumentTable pRDT = site.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
          if (pRDT != null) {
            uint docCookie;
            IVsHierarchy ppIVsHierarchy;
            pRDT.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, fullPath, out ppIVsHierarchy, out itemid, out docData, out docCookie);
            if (ppIVsHierarchy != null && docCookie != VsConstants.VSDOCCOOKIE_NIL && pHierarchy != ppIVsHierarchy && pVsUIHierarchy != ppIVsHierarchy) {
              // not opened by us, so call IsDocumentOpen with the right IVsUIHierarchy so we avoid the
              // annoying "This document is opened by another project" message prompt.
              pVsUIHierarchy = (IVsUIHierarchy)ppIVsHierarchy;
              itemid = (uint)VsConstants.VSITEMID_SELECTION;
            }
            ppIVsHierarchy = null;
          }
          IVsUIHierarchy ppHierOpen;
          uint[] pitemidOpen = new uint[1];
          int pfOpen;      
          doc.IsDocumentOpen(pVsUIHierarchy, itemid, fullPath, ref logicalView, (uint)__VSIDOFLAGS.IDO_ActivateIfOpen, out ppHierOpen, pitemidOpen, out windowFrame, out pfOpen);
          if (pfOpen == 1 && editorType != Guid.Empty) {
            if (windowFrame != null) {
              Guid currentEditor;
              windowFrame.GetGuidProperty((int)__VSFPROPID.VSFPROPID_guidEditorType, out currentEditor);
              if (currentEditor == editorType) {
                goto done;
              }
            }
            windowFrame.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_PromptSave);
            windowFrame = null;
            pfOpen = 0;
          }
          if (pfOpen != 1) {
            uint openFlags = 0;
            if (newFile) openFlags |= OSE_OpenAsNewFile;

            //NOTE: we MUST pass the IVsProject in pVsUIHierarchy and the itemid
            // of the node being opened, otherwise the debugger doesn't work.
            IOleServiceProvider psp = site.GetService(typeof(IOleServiceProvider)) as IOleServiceProvider;
            if (editorType != Guid.Empty) {
              doc.OpenSpecificEditor(editorFlags, fullPath, ref editorType, physicalView, ref logicalView, caption, pRootHierarchy, hierarchyId, docData, psp, out windowFrame);
            } else {
              openFlags |= OSE_ChooseBestStdEditor;
              doc.OpenStandardEditor(openFlags, fullPath, ref logicalView, caption, pRootHierarchy, hierarchyId, docData, psp, out windowFrame);
            }
            if (windowFrame != null) {
              if (newFile) {
                object var;
                windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_DocData, out var);
                IVsPersistDocData ppd = (IVsPersistDocData)var;
                ppd.SetUntitledDocPath(fullPath);
              }
            }
          }
        }
      done:
        if (windowFrame != null)
          windowFrame.Show();        

      } catch (COMException e) {
        if ((uint)e.ErrorCode != (uint)OleErrors.OLE_E_PROMPTSAVECANCELLED) {
#if DEBUG
          MessageBox.Show(e.Message);
#endif
        }
      } catch (Exception e) {
        if (e != null) {
#if DEBUG
        MessageBox.Show(e.Message);
#endif
        }
        if (docData != punkDocDataExisting && docData != IntPtr.Zero) {
          Marshal.Release(docData);
        }
      }
    }   

    public static void RenameDocument(IServiceProvider site, string oldName, string newName) {
			IVsRunningDocumentTable pRDT = site.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
			IVsUIShellOpenDocument doc = site.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
			IVsUIShell uiShell = site.GetService(typeof(SVsUIShell)) as IVsUIShell;

      if (pRDT == null || doc == null) return;
      
      IVsHierarchy      pIVsHierarchy;
      uint              itemId; 
      IntPtr            docData;
      uint              uiVsDocCookie;
      pRDT.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, oldName, out pIVsHierarchy, out itemId, out docData, out uiVsDocCookie);

      if (docData != IntPtr.Zero) {     
        IntPtr pUnk = Marshal.GetIUnknownForObject(pIVsHierarchy);
        Guid iid = typeof(IVsHierarchy).GUID;
        IntPtr pHier;
        Marshal.QueryInterface(pUnk, ref iid, out pHier); 
        try{
          pRDT.RenameDocument(oldName, newName, pHier, itemId);
        }finally{
          Marshal.Release(pHier);
          Marshal.Release(pUnk);
        }

        string newCaption = Path.GetFileName(newName);

        // now we need to tell the windows to update their captions. 
        IEnumWindowFrames ppenum;
        uiShell.GetDocumentWindowEnum(out ppenum);
        IVsWindowFrame[] rgelt = new IVsWindowFrame[1];
        uint fetched;
        while (ppenum.Next(1, rgelt, out fetched) == 0 && fetched == 1) {
          IVsWindowFrame windowFrame = rgelt[0];
          object data;
          windowFrame.GetProperty((int) __VSFPROPID.VSFPROPID_DocData, out data);
          IntPtr ptr = Marshal.GetIUnknownForObject(data);        
          if (ptr == docData) {
            windowFrame.SetProperty((int) __VSFPROPID.VSFPROPID_OwnerCaption, newCaption);
          }
          Marshal.Release(ptr);
        }
        Marshal.Release(docData);        
      }
    }
  
    public static void OpenDocument(IServiceProvider provider, string fullPath, Guid logicalView, out IVsUIHierarchy hierarchy, uint[] itemID, out IVsWindowFrame windowFrame, out IVsTextView view) {
      OpenDocument(provider, fullPath, logicalView, out hierarchy, itemID, out windowFrame);
      view = WindowFrameGetTextView(windowFrame);
    }

    public static void OpenDocument(IServiceProvider provider, string fullPath, Guid logicalView, out IVsUIHierarchy hierarchy, uint[] itemID, out IVsWindowFrame windowFrame) {
      windowFrame = null;
      hierarchy   = null;

      //open document
      IVsUIShellOpenDocument shellOpenDoc = provider.GetService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
      IVsRunningDocumentTable pRDT = provider.GetService(typeof(IVsRunningDocumentTable)) as IVsRunningDocumentTable;
      if (pRDT != null) {
        IntPtr punkDocData;
        uint docCookie;
        uint pitemid;
        IVsHierarchy ppIVsHierarchy;
        pRDT.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, fullPath, out ppIVsHierarchy, out pitemid, out punkDocData, out docCookie);
        if (punkDocData == IntPtr.Zero) {
          IOleServiceProvider psp;
          uint itemid;
          shellOpenDoc.OpenDocumentViaProject(fullPath, ref logicalView, out psp, out hierarchy, out itemid, out windowFrame);
          if (windowFrame != null) 
            windowFrame.Show();          
          psp = null;

        } else {
          Marshal.Release(punkDocData);

          int pfOpen;

          shellOpenDoc.IsDocumentOpen((IVsUIHierarchy)ppIVsHierarchy, pitemid, fullPath, ref logicalView, (uint)__VSIDOFLAGS.IDO_IgnoreLogicalView, out hierarchy, itemID, out windowFrame, out pfOpen);

          if (windowFrame != null)
            windowFrame.Show();
        }
      }        
 
    }      


    public static IVsTextView WindowFrameGetTextView(IVsWindowFrame windowFrame) {
      IVsTextView textView = null;
      object pvar;
      windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out pvar );
      if (pvar is IVsTextView) {
        textView = (IVsTextView)pvar;
      } else if (pvar is IVsCodeWindow) {
        IVsCodeWindow codeWin = (IVsCodeWindow)pvar;
        try {
          IVsTextView vsTextView;
          codeWin.GetPrimaryView(out vsTextView);
          textView = (IVsTextView)vsTextView;
        } catch (Exception) {
          // perhaps the code window doesn't use IVsTextWindow?
          textView = null;
        }
      }
      return textView;
    }

    public static void OpenDocument(IServiceProvider provider, string path) {
      IVsUIHierarchy hierarchy;
      uint[] itemID = new uint[1];
      IVsWindowFrame windowFrame;
      Guid logicalView = Guid.Empty;
      VsShell.OpenDocument(provider, path, logicalView, out hierarchy, itemID, out windowFrame);
      windowFrame = null;
      hierarchy = null;
    }

    public static IVsWindowFrame OpenDocumentWithSpecificEditor(IServiceProvider provider, string fullPath, Guid editorType, string physicalView, Guid logicalView) {
      IVsUIHierarchy hierarchy;
      uint[] itemID = new uint[1];
      IVsWindowFrame windowFrame;
      OpenDocumentWithSpecificEditor(provider, fullPath, editorType, physicalView, logicalView, out hierarchy, itemID, out windowFrame);
      hierarchy = null;            
      return windowFrame;
    }
    public static void OpenDocumentWithSpecificEditor(IServiceProvider provider, string fullPath, Guid editorType, string physicalView, Guid logicalView, out IVsUIHierarchy hierarchy, uint[] itemID, out IVsWindowFrame windowFrame) {
      windowFrame = null;
      hierarchy = null;
      //open document
      IVsUIShellOpenDocument shellOpenDoc = provider.GetService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
      IVsRunningDocumentTable pRDT = provider.GetService(typeof(IVsRunningDocumentTable)) as IVsRunningDocumentTable;
      if (pRDT != null) {
        IntPtr punkDocData;
        uint docCookie;
        uint pitemid;
        IVsHierarchy ppIVsHierarchy;
        pRDT.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, fullPath, out ppIVsHierarchy, out pitemid, out punkDocData, out docCookie);
        if (punkDocData != IntPtr.Zero) {
          Marshal.Release(punkDocData);
          int pfOpen;
          shellOpenDoc.IsDocumentOpen((IVsUIHierarchy)ppIVsHierarchy, pitemid, fullPath, ref logicalView, (uint)__VSIDOFLAGS.IDO_IgnoreLogicalView, out hierarchy, itemID, out windowFrame, out pfOpen);
          hierarchy = null;
          if (windowFrame != null) {
            Guid currentEditor;
            windowFrame.GetGuidProperty((int)__VSFPROPID.VSFPROPID_guidEditorType, out currentEditor);
            object currentView;
            windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_pszPhysicalView, out currentView);
            if (currentEditor == editorType && currentView != null && physicalView == currentView.ToString()) {
              windowFrame.Show();
              return;
            }
          }
          //windowFrame.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_PromptSave);
          windowFrame = null;
        } 
        IOleServiceProvider psp;
        uint itemid;
        uint editorFlags = (uint)__VSSPECIFICEDITORFLAGS.VSSPECIFICEDITOR_UseEditor;
        shellOpenDoc.OpenDocumentViaProjectWithSpecific(fullPath, editorFlags, ref editorType, physicalView, ref logicalView, out psp, out hierarchy, out itemid, out windowFrame);
        if (windowFrame != null)
          windowFrame.Show();
        psp = null;
      }
    }
    internal static void TileVertically(IServiceProvider site) {
      IOleServiceProvider psp = site.GetService(typeof(IOleServiceProvider)) as IOleServiceProvider;
      Guid SID_SUIHostCommandDispatcher = new Guid("E69CD190-1276-11D1-9F64-00A0C911004F");
      Guid iid = typeof(IOleCommandTarget).GUID;
      IntPtr ptr;
      psp.QueryService(ref SID_SUIHostCommandDispatcher, ref iid, out ptr);
      if (ptr != IntPtr.Zero) {
        try {
          IOleCommandTarget cmdTarget = (IOleCommandTarget)Marshal.GetTypedObjectForIUnknown(ptr, typeof(IOleCommandTarget));
          cmdTarget.Exec(ref VsConstants.guidStandardCommandSet97, (uint)VsCommands.TileVert, 0, IntPtr.Zero, IntPtr.Zero);
        } finally {
          Marshal.Release(ptr);
        }
      }
    }
    public static Guid BuildOrder = new Guid("2032b126-7c8d-48ad-8026-0e0348004fc0");
    public static Guid BuildOutput = new Guid("1BD8A850-02D1-11d1-BEE7-00A0C913D1F8");
    public static Guid DebugOutput = new Guid("FC076020-078A-11D1-A7DF-00A0C9110051");

    public static IVsOutputWindowPane GetOutputPane(IServiceProvider site, Guid page, string caption) {
      IVsOutputWindow outputWindow = site.GetService(typeof(IVsOutputWindow)) as IVsOutputWindow;
      IVsOutputWindowPane pane = null;
      try {
        outputWindow.GetPane(ref page, out pane);                
      } catch (COMException) {
        // catch E_FAIL
        if (caption != null) {
          try {
            outputWindow.CreatePane(ref page, caption, 1, 1);
            outputWindow.GetPane(ref page, out pane);
          } catch (COMException) {
            // catch E_FAIL
          }
        }
      }
      if (pane != null) pane.Activate();
      return pane;
    }
  } // VSShell

  //------------------------------------------------------------------------
  [StructLayoutAttribute(LayoutKind.Sequential)]
  public struct NotifyMessageHeader {
    public IntPtr hwndFrom;
    public int idFrom;
    public int code;
  }

  internal enum NotifyMessageCode {
    SetActive = -200,
    KillActive = -201,
    Apply = -202,
    Reset = -203,
    Help = -205,
    QueryCancel = -209,
  }

  internal class NativeWindowHelper {

    public static IntPtr GetNativeWndProc(System.Windows.Forms.Control control) {
      IntPtr handle = control.Handle;
      return GetWindowLong(new HandleRef(control, handle), GWL_WNDPROC);
    }

    public const int GWL_WNDPROC = (-4);

    [DllImport("User32.dll",CharSet=CharSet.Auto)]
    public static extern IntPtr GetWindowLong(HandleRef hWnd, int nIndex);

    [DllImport("user32.dll", CharSet=CharSet.Unicode, EntryPoint="SetWindowLong")]
    public static extern int SetWindowLong(IntPtr hWnd, short nIndex, int value);

    [DllImport("user32.dll", EntryPoint="SetParent", SetLastError=true, ExactSpelling=true, CallingConvention=CallingConvention.StdCall)]
    public static extern int SetParent(IntPtr child, IntPtr parent);

    [DllImport("user32.dll", EntryPoint="IsDialogMessageA", SetLastError=true, CharSet=CharSet.Ansi, ExactSpelling=true, CallingConvention=CallingConvention.StdCall)]
    public static extern bool IsDialogMessageA(IntPtr hDlg,  ref MSG msg);

    [DllImport("user32.dll")]
    public static extern void SetFocus(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetFocus();

    [DllImport("user32.dll", CharSet=CharSet.Auto)]
    public extern static IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    public const short DWLP_MSGRESULT  = 0;
    public const int PSNRET_NOERROR  = 0;

    // winuser.h
    public const int WH_JOURNALPLAYBACK = 1,
      WH_GETMESSAGE = 3,
      WH_MOUSE = 7,
      WSF_VISIBLE = 0x0001,
      WM_NULL = 0x0000,
      WM_CREATE = 0x0001,
      WM_DELETEITEM = 0x002D,
      WM_DESTROY = 0x0002,
      WM_MOVE = 0x0003,
      WM_SIZE = 0x0005,
      WM_ACTIVATE = 0x0006,
      WA_INACTIVE = 0,
      WA_ACTIVE = 1,
      WA_CLICKACTIVE = 2,
      WM_SETFOCUS = 0x0007,
      WM_KILLFOCUS = 0x0008,
      WM_ENABLE = 0x000A,
      WM_SETREDRAW = 0x000B,
      WM_SETTEXT = 0x000C,
      WM_GETTEXT = 0x000D,
      WM_GETTEXTLENGTH = 0x000E,
      WM_PAINT = 0x000F,
      WM_CLOSE = 0x0010,
      WM_QUERYENDSESSION = 0x0011,
      WM_QUIT = 0x0012,
      WM_QUERYOPEN = 0x0013,
      WM_ERASEBKGND = 0x0014,
      WM_SYSCOLORCHANGE = 0x0015,
      WM_ENDSESSION = 0x0016,
      WM_SHOWWINDOW = 0x0018,
      WM_WININICHANGE = 0x001A,
      WM_SETTINGCHANGE = 0x001A,
      WM_DEVMODECHANGE = 0x001B,
      WM_ACTIVATEAPP = 0x001C,
      WM_FONTCHANGE = 0x001D,
      WM_TIMECHANGE = 0x001E,
      WM_CANCELMODE = 0x001F,
      WM_SETCURSOR = 0x0020,
      WM_MOUSEACTIVATE = 0x0021,
      WM_CHILDACTIVATE = 0x0022,
      WM_QUEUESYNC = 0x0023,
      WM_GETMINMAXINFO = 0x0024,
      WM_PAINTICON = 0x0026,
      WM_ICONERASEBKGND = 0x0027,
      WM_NEXTDLGCTL = 0x0028,
      WM_SPOOLERSTATUS = 0x002A,
      WM_DRAWITEM = 0x002B,
      WM_MEASUREITEM = 0x002C,
      WM_VKEYTOITEM = 0x002E,
      WM_CHARTOITEM = 0x002F,
      WM_SETFONT = 0x0030,
      WM_GETFONT = 0x0031,
      WM_SETHOTKEY = 0x0032,
      WM_GETHOTKEY = 0x0033,
      WM_QUERYDRAGICON = 0x0037,
      WM_COMPAREITEM = 0x0039,
      WM_GETOBJECT = 0x003D,
      WM_COMPACTING = 0x0041,
      WM_COMMNOTIFY = 0x0044,
      WM_WINDOWPOSCHANGING = 0x0046,
      WM_WINDOWPOSCHANGED = 0x0047,
      WM_POWER = 0x0048,
      WM_COPYDATA = 0x004A,
      WM_CANCELJOURNAL = 0x004B,
      WM_NOTIFY = 0x004E,
      WM_INPUTLANGCHANGEREQUEST = 0x0050,
      WM_INPUTLANGCHANGE = 0x0051,
      WM_TCARD = 0x0052,
      WM_HELP = 0x0053,
      WM_USERCHANGED = 0x0054,
      WM_NOTIFYFORMAT = 0x0055,
      WM_CONTEXTMENU = 0x007B,
      WM_STYLECHANGING = 0x007C,
      WM_STYLECHANGED = 0x007D,
      WM_DISPLAYCHANGE = 0x007E,
      WM_GETICON = 0x007F,
      WM_SETICON = 0x0080,
      WM_NCCREATE = 0x0081,
      WM_NCDESTROY = 0x0082,
      WM_NCCALCSIZE = 0x0083,
      WM_NCHITTEST = 0x0084,
      WM_NCPAINT = 0x0085,
      WM_NCACTIVATE = 0x0086,
      WM_GETDLGCODE = 0x0087,
      WM_NCMOUSEMOVE = 0x00A0,
      WM_NCLBUTTONDOWN = 0x00A1,
      WM_NCLBUTTONUP = 0x00A2,
      WM_NCLBUTTONDBLCLK = 0x00A3,
      WM_NCRBUTTONDOWN = 0x00A4,
      WM_NCRBUTTONUP = 0x00A5,
      WM_NCRBUTTONDBLCLK = 0x00A6,
      WM_NCMBUTTONDOWN = 0x00A7,
      WM_NCMBUTTONUP = 0x00A8,
      WM_NCMBUTTONDBLCLK = 0x00A9,
      WM_NCXBUTTONDOWN               = 0x00AB,
      WM_NCXBUTTONUP                 = 0x00AC,
      WM_NCXBUTTONDBLCLK             = 0x00AD,
      WM_KEYFIRST = 0x0100,
      WM_KEYDOWN = 0x0100,
      WM_KEYUP = 0x0101,
      WM_CHAR = 0x0102,
      WM_DEADCHAR = 0x0103,
      WM_CTLCOLOR = 0x0019,
      WM_SYSKEYDOWN = 0x0104,
      WM_SYSKEYUP = 0x0105,
      WM_SYSCHAR = 0x0106,
      WM_SYSDEADCHAR = 0x0107,
      WM_KEYLAST = 0x0108,
      WM_IME_STARTCOMPOSITION = 0x010D,
      WM_IME_ENDCOMPOSITION = 0x010E,
      WM_IME_COMPOSITION = 0x010F,
      WM_IME_KEYLAST = 0x010F,
      WM_INITDIALOG = 0x0110,
      WM_COMMAND = 0x0111,
      WM_SYSCOMMAND = 0x0112,
      WM_TIMER = 0x0113,
      WM_HSCROLL = 0x0114,
      WM_VSCROLL = 0x0115,
      WM_INITMENU = 0x0116,
      WM_INITMENUPOPUP = 0x0117,
      WM_MENUSELECT = 0x011F,
      WM_MENUCHAR = 0x0120,
      WM_ENTERIDLE = 0x0121,
      WM_CHANGEUISTATE = 0x0127,
      WM_UPDATEUISTATE = 0x0128,
      WM_QUERYUISTATE = 0x0129,
      WM_CTLCOLORMSGBOX = 0x0132,
      WM_CTLCOLOREDIT = 0x0133,
      WM_CTLCOLORLISTBOX = 0x0134,
      WM_CTLCOLORBTN = 0x0135,
      WM_CTLCOLORDLG = 0x0136,
      WM_CTLCOLORSCROLLBAR = 0x0137,
      WM_CTLCOLORSTATIC = 0x0138,
      WM_MOUSEFIRST = 0x0200,
      WM_MOUSEMOVE = 0x0200,
      WM_LBUTTONDOWN = 0x0201,
      WM_LBUTTONUP = 0x0202,
      WM_LBUTTONDBLCLK = 0x0203,
      WM_RBUTTONDOWN = 0x0204,
      WM_RBUTTONUP = 0x0205,
      WM_RBUTTONDBLCLK = 0x0206,
      WM_MBUTTONDOWN = 0x0207,
      WM_MBUTTONUP = 0x0208,
      WM_MBUTTONDBLCLK = 0x0209,
      WM_XBUTTONDOWN                 = 0x020B,
      WM_XBUTTONUP                   = 0x020C,
      WM_XBUTTONDBLCLK               = 0x020D,
      WM_MOUSEWHEEL = 0x020A,
      WM_MOUSELAST = 0x020A;
        
    public const int WHEEL_DELTA = 120,
      WM_PARENTNOTIFY = 0x0210,
      WM_ENTERMENULOOP = 0x0211,
      WM_EXITMENULOOP = 0x0212,
      WM_NEXTMENU = 0x0213,
      WM_SIZING = 0x0214,
      WM_CAPTURECHANGED = 0x0215,
      WM_MOVING = 0x0216,
      WM_POWERBROADCAST = 0x0218,
      WM_DEVICECHANGE = 0x0219,
      WM_IME_SETCONTEXT = 0x0281,
      WM_IME_NOTIFY = 0x0282,
      WM_IME_CONTROL = 0x0283,
      WM_IME_COMPOSITIONFULL = 0x0284,
      WM_IME_SELECT = 0x0285,
      WM_IME_CHAR = 0x0286,
      WM_IME_KEYDOWN = 0x0290,
      WM_IME_KEYUP = 0x0291,
      WM_MDICREATE = 0x0220,
      WM_MDIDESTROY = 0x0221,
      WM_MDIACTIVATE = 0x0222,
      WM_MDIRESTORE = 0x0223,
      WM_MDINEXT = 0x0224,
      WM_MDIMAXIMIZE = 0x0225,
      WM_MDITILE = 0x0226,
      WM_MDICASCADE = 0x0227,
      WM_MDIICONARRANGE = 0x0228,
      WM_MDIGETACTIVE = 0x0229,
      WM_MDISETMENU = 0x0230,
      WM_ENTERSIZEMOVE = 0x0231,
      WM_EXITSIZEMOVE = 0x0232,
      WM_DROPFILES = 0x0233,
      WM_MDIREFRESHMENU = 0x0234,
      WM_MOUSEHOVER = 0x02A1,
      WM_MOUSELEAVE = 0x02A3,
      WM_CUT = 0x0300,
      WM_COPY = 0x0301,
      WM_PASTE = 0x0302,
      WM_CLEAR = 0x0303,
      WM_UNDO = 0x0304,
      WM_RENDERFORMAT = 0x0305,
      WM_RENDERALLFORMATS = 0x0306,
      WM_DESTROYCLIPBOARD = 0x0307,
      WM_DRAWCLIPBOARD = 0x0308,
      WM_PAINTCLIPBOARD = 0x0309,
      WM_VSCROLLCLIPBOARD = 0x030A,
      WM_SIZECLIPBOARD = 0x030B,
      WM_ASKCBFORMATNAME = 0x030C,
      WM_CHANGECBCHAIN = 0x030D,
      WM_HSCROLLCLIPBOARD = 0x030E,
      WM_QUERYNEWPALETTE = 0x030F,
      WM_PALETTEISCHANGING = 0x0310,
      WM_PALETTECHANGED = 0x0311,
      WM_HOTKEY = 0x0312,
      WM_PRINT = 0x0317,
      WM_PRINTCLIENT = 0x0318,
      WM_HANDHELDFIRST = 0x0358,
      WM_HANDHELDLAST = 0x035F,
      WM_AFXFIRST = 0x0360,
      WM_AFXLAST = 0x037F,
      WM_PENWINFIRST = 0x0380,
      WM_PENWINLAST = 0x038F,
      WM_APP = unchecked((int)0x8000),
      WM_USER = 0x0400,
      WM_REFLECT = WM_USER + 0x1C00,
      WS_OVERLAPPED = 0x00000000,
      WS_POPUP = unchecked((int)0x80000000),
      WS_CHILD = 0x40000000,
      WS_MINIMIZE = 0x20000000,
      WS_VISIBLE = 0x10000000,
      WS_DISABLED = 0x08000000,
      WS_CLIPSIBLINGS = 0x04000000,
      WS_CLIPCHILDREN = 0x02000000,
      WS_MAXIMIZE = 0x01000000,
      WS_CAPTION = 0x00C00000,
      WS_BORDER = 0x00800000,
      WS_DLGFRAME = 0x00400000,
      WS_VSCROLL = 0x00200000,
      WS_HSCROLL = 0x00100000,
      WS_SYSMENU = 0x00080000,
      WS_THICKFRAME = 0x00040000,
      WS_TABSTOP = 0x00010000,
      WS_MINIMIZEBOX = 0x00020000,
      WS_MAXIMIZEBOX = 0x00010000,
      WS_EX_DLGMODALFRAME = 0x00000001,
      WS_EX_MDICHILD = 0x00000040,
      WS_EX_TOOLWINDOW = 0x00000080,
      WS_EX_CLIENTEDGE = 0x00000200,
      WS_EX_CONTEXTHELP = 0x00000400,
      WS_EX_RIGHT = 0x00001000,
      WS_EX_LEFT = 0x00000000,
      WS_EX_RTLREADING = 0x00002000,
      WS_EX_LEFTSCROLLBAR = 0x00004000,
      WS_EX_CONTROLPARENT = 0x00010000,
      WS_EX_STATICEDGE = 0x00020000,
      WS_EX_APPWINDOW = 0x00040000,
      WS_EX_LAYERED           = 0x00080000,
      WS_EX_TOPMOST = 0x00000008,
      WPF_SETMINPOSITION = 0x0001,
      WM_CHOOSEFONT_GETLOGFONT = (0x0400+1);
  }

}
