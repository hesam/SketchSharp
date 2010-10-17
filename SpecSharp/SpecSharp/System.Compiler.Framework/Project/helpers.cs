using Microsoft.VisualStudio.Shell.Interop;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.OLE.Interop;
using System.Xml;
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Compiler.Framework.Ole.Misc; 


////////////////////////////////////////////////////
/// <summary>
/// This file contains some simple helper classes used by the project classes
/// </summary>
////////////////////////////////////////////////////

namespace System.Compiler{
  public class CookieEnumerator : IEnumerator {
    object[] map;
    uint used;
    uint pos;

    public CookieEnumerator(object[] map, uint used){
      this.map = map; this.used = used;
    }

    object IEnumerator.Current{
      get { return (this.pos>0&&this.pos<=this.used) ? this.map[this.pos-1] : null; }
    }

    bool IEnumerator.MoveNext() {

      if (this.pos<this.used) {
        this.pos++;
        while (this.pos <= this.used && this.map[this.pos-1] == null)
          this.pos++;

        if (this.pos <= this.used)  {
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
  public class CookieMap : IEnumerable {
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
        object[] newmap = new object[size*2];
        System.Array.Copy(this.map, 0, newmap, 0, (int)this.size);
        this.map = newmap;
        this.size *= 2;
      }
      this.map[this.used++] = o;
      return result;
    }


    public void Remove(Object obj) {
      for(uint i = 0; i < this.used; i++) {
        if (this.map[i] == obj) {
          this.map[i] = null; // todo - re-use these gaps.
          return;
        }
      }
      throw new InvalidOperationException("Object not found");
    }

    public void RemoveAt(uint cookie) {
      Debug.Assert(cookie<this.used);
      this.map[cookie] = null;
    }
    public void SetAt(uint cookie, object value){
      this.map[cookie] = value;
    }
    public object FromCookie(uint cookie) {
      Debug.Assert(cookie<used);
      return this.map[cookie];
    }

    public object this[uint cookie] {
      get {
        Debug.Assert(cookie<used);
        return this.map[cookie];
      }
      set {
        this.map[cookie] = value;
      }
    }

    public void Clear() {
      for(uint i = 0; i < this.used; i++) {
        this.map[i] = null;
      }
      this.used = 0;
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return new CookieEnumerator(map, used);
    }
  }

  //===========================================================================
  // This custom writer puts attributes on a new line which makes the 
  // .xsproj files easier to read.
  class MyXmlWriter : XmlTextWriter {
    int depth;
    TextWriter tw;
    string    indent;

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
        this.tw.Write( this.IndentString );
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
  public class TrackDocumentsHelper {
                                                                
    private CookieMap trackProjectEventSinks = new CookieMap();
    private ProjectManager theProject; 


    public TrackDocumentsHelper(ProjectManager project) {
      theProject = project; 
    }

    private IVsTrackProjectDocuments2 getTPD() {
      return (IVsTrackProjectDocuments2)theProject.Site.QueryService(VsConstants.SID_SVsTrackProjectDocuments, typeof(IVsTrackProjectDocuments2)); 
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
      try  {
        for (int i = 0; i < files.Length; i++) 
          flags[i] = VSQUERYADDFILEFLAGS.VSQUERYADDFILEFLAGS_NoFlags;
        foreach (IVsTrackProjectDocumentsEvents2 sink in this.trackProjectEventSinks) {
          VSQUERYADDFILERESULTS[] summary = new VSQUERYADDFILERESULTS[1];
          sink.OnQueryAddFiles((IVsProject)this, len, files, flags, summary, null);
          if (summary[0] == VSQUERYADDFILERESULTS.VSQUERYADDFILERESULTS_AddNotOK)
            return false;
          }
        return true;
      }
      catch  {
        return false;
      }
    }


    public void OnAddFile(string file) {
      try  {
        foreach (IVsTrackProjectDocumentsEvents2 sink in this.trackProjectEventSinks) {
          sink.OnAfterAddFilesEx(1, 1, new IVsProject[1] { (IVsProject)this },
            new int[1] { 0 }, new string[1] { file } , 
            new VSADDFILEFLAGS[1] { VSADDFILEFLAGS.VSADDFILEFLAGS_NoFlags });
        }
      }
      catch  {
      }
    }

    /// <summary>
    /// Get's called to ask the environent if a file is allowed to be renamed
    /// </summary>
    /// returns FALSE if the doc can not be renamed
    public bool CanRenameFile(string strOldName, string strNewName) {
      CCITracing.TraceCall();
      int iCanContinue = 0;
      this.getTPD().OnQueryRenameFile(this.theProject, strOldName, strNewName, VSRENAMEFILEFLAGS.VSRENAMEFILEFLAGS_NoFlags, out iCanContinue);
      return (iCanContinue != 0); 
    }

    /// <summary>
    /// Get's called to tell the env that a file was renamed
    /// </summary>
    /// 
    public void AfterRenameFile(string strOldName, string strNewName) {
      CCITracing.TraceCall();
      this.getTPD().OnAfterRenameFile(this.theProject, strOldName, strNewName, VSRENAMEFILEFLAGS.VSRENAMEFILEFLAGS_NoFlags);
    }


  }
/*

    // IVsTrackProjectDocuments2
    void IVsTrackProjectDocuments2.AdviseTrackProjectDocumentsEvents(IVsTrackProjectDocumentsEvents2 sink, out uint cookie) {
      CCITracing.TraceCall();
      cookie = this.tracker.Advise(sink);
    }

    void IVsTrackProjectDocuments2.UnadviseTrackProjectDocumentsEvents(uint cookie) {
      CCITracing.TraceCall();
      this.tracker.Unadvise(cookie);
    }

    void IVsTrackProjectDocuments2.BeginBatch() {
      CCITracing.TraceCall();
      //TODO
    }
    void IVsTrackProjectDocuments2.EndBatch() {
      CCITracing.TraceCall();
      //TODO
    }
    void IVsTrackProjectDocuments2.Flush() {
      CCITracing.TraceCall();
      //TODO
    }
    void IVsTrackProjectDocuments2.OnAfterAddDirectories(IVsProject p, int count, string[] mkDocuments) {
      CCITracing.TraceCall();
      //TODO
    }
    void IVsTrackProjectDocuments2.OnAfterAddDirectoriesEx(IVsProject p, int count, string[] mkDocuments, tagVSADDDIRECTORYFLAGS[] f) {
      CCITracing.TraceCall();
      //TODO
    }

    void IVsTrackProjectDocuments2.OnAfterAddFiles(IVsProject p, int count, string[] mkDocuments) {
      CCITracing.TraceCall();
      //TODO
    }

    void IVsTrackProjectDocuments2.OnAfterAddFilesEx(IVsProject p, int count, string[] mkDocuments, VSADDFILEFLAGS[] f) {
      CCITracing.TraceCall();
      //TODO
    }

    void IVsTrackProjectDocuments2.OnAfterRemoveDirectories(IVsProject p, int count, string[] mkDocuments, VSREMOVEDIRECTORYFLAGS[] f) {
      CCITracing.TraceCall();
      //TODO
    }

    void IVsTrackProjectDocuments2.OnAfterRemoveFiles(IVsProject p, int count, string[] mkDocuments, tagVSREMOVEFILEFLAGS[] f) {
      CCITracing.TraceCall();
      //TODO
    }

    void IVsTrackProjectDocuments2.OnAfterRenameDirectories(IVsProject p, int count, string[] mkOldNames, string[] newNames, tagVSRENAMEDIRECTORYFLAGS[] f) {
      CCITracing.TraceCall();
      //TODO
    }

    void IVsTrackProjectDocuments2.OnAfterRenameFiles(IVsProject p, int count, string[] mkOldNames, string[] newNames, VSRENAMEFILEFLAGS[] f) {
      CCITracing.TraceCall();
      //TODO
    }

    void IVsTrackProjectDocuments2.OnAfterSccStatusChanged(IVsProject p, int count, string[] mkDocuments, uint[] sccStatus) {
      CCITracing.TraceCall();
      //TODO
    }

    void IVsTrackProjectDocuments2.OnQueryAddDirectories(IVsProject p, int count, string[] mkDocuments, tagVSQUERYADDDIRECTORYFLAGS[] f, out tagVSQUERYADDDIRECTORYRESULTS summary, tagVSQUERYADDDIRECTORYRESULTS[] r) {
      CCITracing.TraceCall();
      summary = tagVSQUERYADDDIRECTORYRESULTS.VSQUERYADDDIRECTORYRESULTS_AddOK;
    }

    void IVsTrackProjectDocuments2.OnQueryAddFiles(IVsProject p, int count, string[] mkDocuments, tagVSQUERYADDFILEFLAGS[] f, out VSQUERYADDFILERESULTS summary, VSQUERYADDFILERESULTS[] r) {
      CCITracing.TraceCall();
      summary = VSQUERYADDFILERESULTS.VSQUERYADDFILERESULTS_AddOK;
    }

    void IVsTrackProjectDocuments2.OnQueryRemoveDirectories(IVsProject p, int count, string[] mkDocuments, tagVSQUERYREMOVEDIRECTORYFLAGS[] f, out tagVSQUERYREMOVEDIRECTORYRESULTS summary, tagVSQUERYREMOVEDIRECTORYRESULTS[] r) {
      CCITracing.TraceCall();
      summary = tagVSQUERYREMOVEDIRECTORYRESULTS.VSQUERYREMOVEDIRECTORYRESULTS_RemoveOK;
    }

    void IVsTrackProjectDocuments2.OnQueryRemoveFiles(IVsProject p, int count, string[] mkDocuments, VSQUERYREMOVEFILEFLAGS[] f, out tagVSQUERYREMOVEFILERESULTS summary, tagVSQUERYREMOVEFILERESULTS[] r) {
      CCITracing.TraceCall();
      summary = tagVSQUERYREMOVEFILERESULTS.VSQUERYREMOVEFILERESULTS_RemoveOK;
    }

    void IVsTrackProjectDocuments2.OnQueryRenameDirectories(IVsProject p, int count, string[] oldNames, string[] newNames, tagVSQUERYRENAMEDIRECTORYFLAGS[] f, out tagVSQUERYRENAMEDIRECTORYRESULTS summary, tagVSQUERYRENAMEDIRECTORYRESULTS[] r) {
      CCITracing.TraceCall();
      summary = tagVSQUERYRENAMEDIRECTORYRESULTS.VSQUERYRENAMEDIRECTORYRESULTS_RenameOK;
    }
    void IVsTrackProjectDocuments2.OnQueryRenameFiles(IVsProject p, int count, string[] oldNames, string[] newNames, VSQUERYRENAMEFILEFLAGS[] f, out tagVSQUERYRENAMEFILERESULTS summary, tagVSQUERYRENAMEFILERESULTS[] r) {

      CCITracing.TraceCall();

      summary = tagVSQUERYRENAMEFILERESULTS.VSQUERYRENAMEFILERESULTS_RenameOK;
    }

*/


} // end of namespace system.compiler


namespace System.Compiler.Framework.Ole.Misc {
    [StructLayoutAttribute(LayoutKind.Sequential)]
    public struct _DROPFILES {
      public Int32 pFiles;
      public Int32 X;
      public Int32 Y;
      public Int32 fNC;
      public Int32 fWide;
    };

    public enum OleErrors : uint  {
      OLE_E_FIRST               = 0x80040000,
      OLE_E_OLEVERB             = 0x80040000,
      OLE_E_ADVF                = 0x80040001,
      OLE_E_ENUM_NOMORE         = 0x80040002,
      OLE_E_ADVISENOTSUPPORTED  = 0x80040003,
      OLE_E_NOCONNECTION        = 0x80040004,
      OLE_E_NOTRUNNING          = 0x80040005,
      OLE_E_NOCACHE             = 0x80040006,
      OLE_E_BLANK               = 0x80040007,
      OLE_E_CLASSDIFF           = 0x80040008,
      OLE_E_CANT_GETMONIKER     = 0x80040009,
      OLE_E_CANT_BINDTOSOURCE   = 0x8004000A,
      OLE_E_STATIC              = 0x8004000B,
      OLE_E_PROMPTSAVECANCELLED = 0x8004000C,
      OLE_E_INVALIDRECT         = 0x8004000D,
      OLE_E_WRONGCOMPOBJ        = 0x8004000E,
      OLE_E_INVALIDHWND         = 0x8004000F,
      OLE_E_NOT_INPLACEACTIVE   = 0x80040010,
      OLE_E_CANTCONVERT         = 0x80040011,
      OLE_E_NOSTORAGE           = 0x80040012,
      OLE_E_LAST                = 0x800400FF,
    }

    // winerr.h, docobj.h
    public enum OleDocumentError : uint {
      OLECMDERR_E_FIRST         = (OleErrors.OLE_E_LAST+1), 
      OLECMDERR_E_NOTSUPPORTED  = (OLECMDERR_E_FIRST),
      OLECMDERR_E_UNKNOWNGROUP  = (OLECMDERR_E_FIRST+4)
    };

    public enum OleDispatchErrors : uint  {
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

    public enum OLECRF {
     	  olecrfNeedIdleTime	= 1,
        olecrfNeedPeriodicIdleTime	= 2,
        olecrfPreTranslateKeys	= 4,
        olecrfPreTranslateAll	= 8,
        olecrfNeedSpecActiveNotifs	= 16,
        olecrfNeedAllActiveNotifs	= 32,
        olecrfExclusiveBorderSpace	= 64,
        olecrfExclusiveActivation	= 128
    } ;


    public enum OLECADVF {
      olecadvfModal	= 1,
      olecadvfRedrawOff	= 2,
      olecadvfWarningsOff	= 4,
      olecadvfRecording	= 8
    } ;

    public enum NativeBoolean : int  {
      True = 1,
      False = 0
    }; 

    public enum DropDataType {//Drop types
      None, Shell, VsStg, VsRef
    };
   
    public class DragDropHelper  {

      [DllImport("user32.dll", EntryPoint="RegisterClipboardFormatW",  
        SetLastError=true, CharSet=CharSet.Unicode, ExactSpelling=true,
        CallingConvention=CallingConvention.StdCall)]
      static extern ushort RegisterClipboardFormat(string format);

      public static ushort CF_VSREFPROJECTS = 0;
      public static ushort CF_VSSTGPROJECTS = 0;
      public static ushort CF_VSREFPROJECTITEMS = 0;
      public static ushort CF_VSSTGPROJECTITEMS = 0;

      public static void RegisterClipboardFormats() {
        if (CF_VSREFPROJECTITEMS == 0) {
          CF_VSREFPROJECTS = RegisterClipboardFormat("CF_VSREFPROJECTS");
          CF_VSSTGPROJECTS = RegisterClipboardFormat("CF_VSSTGPROJECTS");
          CF_VSREFPROJECTITEMS = RegisterClipboardFormat("CF_VSREFPROJECTITEMS");
          CF_VSSTGPROJECTITEMS = RegisterClipboardFormat("CF_VSSTGPROJECTITEMS");
        }
      }

      public static FORMATETC CreateFormatEtc(ushort iFormat)  {
        FORMATETC fmt = new FORMATETC();
        fmt.cfFormat = iFormat;
        fmt.ptd = IntPtr.Zero;
        fmt.dwAspect = (uint)DVASPECT.DVASPECT_CONTENT;
        fmt.lindex = -1;
        fmt.tymed = (uint)TYMED.TYMED_HGLOBAL;      
        return fmt;
    } 


    public static FORMATETC CreateFormatEtc()  {
        return CreateFormatEtc(CF_VSREFPROJECTITEMS); 
    } 

    public static void QueryGetData(Microsoft.VisualStudio.OLE.Interop.IDataObject pDataObject, ref FORMATETC fmtetc){
      FORMATETC[] af = new FORMATETC[1];
      af[0] = fmtetc;
      pDataObject.QueryGetData(af);
      fmtetc = af[0];
    }
      public static STGMEDIUM GetData(Microsoft.VisualStudio.OLE.Interop.IDataObject pDataObject, ref FORMATETC fmtetc){
        FORMATETC[] af = new FORMATETC[1];
        af[0] = fmtetc;
        STGMEDIUM[] sm = new STGMEDIUM[1];
        pDataObject.GetData(af, sm);
        fmtetc = af[0];
        return sm[0];
      }

    public static bool AttemptVsFormat(HierarchyNode activeNode, ushort cfFormat, Microsoft.VisualStudio.OLE.Interop.IDataObject pDataObject, uint grfKeyState, out DropDataType pddt) {
      pddt = DropDataType.None;
      FORMATETC fmtetc = new FORMATETC();

      fmtetc.cfFormat = cfFormat;
      fmtetc.ptd = IntPtr.Zero;
      fmtetc.dwAspect = (uint)DVASPECT.DVASPECT_CONTENT;
      fmtetc.lindex = -1;
      fmtetc.tymed = (uint)TYMED.TYMED_HGLOBAL;

      bool hasData = false;
      try {
        QueryGetData(pDataObject, ref fmtetc);
        hasData= true;
      } catch (Exception) {
      }

      if (hasData) {
        try {
          STGMEDIUM stgmedium = GetData(pDataObject, ref fmtetc);
          if (stgmedium.tymed == (uint)TYMED.TYMED_HGLOBAL) {
            IntPtr hDropInfo = stgmedium.unionmember;
            if (hDropInfo != IntPtr.Zero) {
              pddt = DropDataType.VsRef;
              try {
                activeNode.AddFiles(UtilGetFilesFromPROJITEMDrop(hDropInfo));
                Marshal.FreeHGlobal(hDropInfo);
              } catch (Exception e) {
                Marshal.FreeHGlobal(hDropInfo);
                throw e;
              }
              return true;
            }
          }
        } catch (Exception e) {
          Console.WriteLine("Exception:"+e.Message);
        }
      }
      return false;
    }

    // Split the series of null terminated strings back up into an array of strings.
    static string[] UtilGetFilesFromPROJITEMDrop(IntPtr hDropInfo) {
      string[] result = null;
      IntPtr data = MyDataObject.GlobalLock(hDropInfo);
      try {
        _DROPFILES df = (_DROPFILES)Marshal.PtrToStructure(data, typeof(_DROPFILES));
        if (df.fWide != 0) {// unicode?
          IntPtr pdata = new IntPtr((long)data + df.pFiles);
          string s = Marshal.PtrToStringUni(pdata);
          ArrayList list = new ArrayList();
          int pos = 0;
          int i = 0;
          int len = s.Length;
          for (; i < len; i++) {
            if (s[i] == '\0') {
              if (i == len-1 || s[i+1] == '\0')
                break;
              list.Add(s.Substring(pos,i-1));
              pos=i+1;
            }
          }
          if (i>pos) {
            list.Add(s.Substring(pos,i));
          }
          result = (String[])list.ToArray(typeof(string));
        }
      } catch {
      }
      MyDataObject.GlobalUnLock(data);
      return result;
    }
  } // end of dragdrophelper


    /// <summary>
    /// helper to make the editor ignore external changes
    /// </summary>
    /// returns FALSE if the doc can not be renamed
    public class SuspendFileChanges  {
      private string strDocumentFileName;
      private bool fSuspending; 
      private ServiceProvider site; 
      private IVsDocDataFileChangeControl fileChangeControl;


      public SuspendFileChanges(ServiceProvider site, string strDocument) {
        this.site = site; 
        this.strDocumentFileName = strDocument;
        this.fSuspending = false;
        this.fileChangeControl = null;
      }

      public void Suspend() {
        if (this.fSuspending)
          return;

        try  {

          IVsRunningDocumentTable pRDT = (IVsRunningDocumentTable)this.site.QueryService(VsConstants.SID_SVsRunningDocumentTable, typeof(IVsRunningDocumentTable));
          IntPtr            docData;
          IVsHierarchy      pIVsHierarchy;
          uint              itemId; 
          uint              uiVsDocCookie;
          IVsFileChangeEx   vsFileChange; 


          if (pRDT == null) return;

          pRDT.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock,
              this.strDocumentFileName, out pIVsHierarchy, out itemId,
              out docData, out uiVsDocCookie);

          if ( (uiVsDocCookie == VsConstants.VSDOCCOOKIE_NIL) || docData==IntPtr.Zero)
              return;
          
          vsFileChange = (IVsFileChangeEx) this.site.QueryService(VsConstants.SID_SVsFileChangeEx, typeof(IVsFileChangeEx)); 

          if (vsFileChange != null) {
            this.fSuspending = true;
            vsFileChange.IgnoreFile(0, this.strDocumentFileName, (int) 1); 
            if (docData != IntPtr.Zero) {
              try  {
                // if interface is not supported, return null
                IVsPersistDocData ppIVsPersistDocData = (IVsPersistDocData)Marshal.GetObjectForIUnknown(docData);          
                this.fileChangeControl = (IVsDocDataFileChangeControl) ppIVsPersistDocData; 
                if (this.fileChangeControl != null)  {
                  this.fileChangeControl.IgnoreFileChanges(1);
                }
              }
              catch  {
              };

            }
          }
        }
        catch  {
        }
        return;
      }

      public void Resume()  {
        if (!this.fSuspending)
          return;
        try  {
          IVsFileChangeEx   vsFileChange; 
          vsFileChange = (IVsFileChangeEx) this.site.QueryService(VsConstants.SID_SVsFileChangeEx, typeof(IVsFileChangeEx)); 
          if (vsFileChange != null) {
            vsFileChange.IgnoreFile(0, this.strDocumentFileName, 0);
            if (this.fileChangeControl != null) {
              this.fileChangeControl.IgnoreFileChanges(0);
            }
          }
        }
        catch  {
        }
      }
    }
} // end of namespace System.Compiler.Framework.Ole.Misc


