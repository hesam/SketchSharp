using System;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.OLE.Interop;
using System.Runtime.InteropServices;
using System.Collections;

namespace Microsoft.VisualStudio.Package
{
    internal enum tagDVASPECT
    {
        DVASPECT_CONTENT = 1,
        DVASPECT_THUMBNAIL = 2,
        DVASPECT_ICON = 4,
        DVASPECT_DOCPRINT = 8
    }

    internal enum tagTYMED
    {
        TYMED_HGLOBAL = 1,
        TYMED_FILE = 2,
        TYMED_ISTREAM = 4,
        TYMED_ISTORAGE = 8,
        TYMED_GDI = 16,
        TYMED_MFPICT = 32,
        TYMED_ENHMF = 64,
        TYMED_NULL = 0
    }

    internal struct DataCacheEntry
    {
        public FORMATETC format;

        public long data;

        public DATADIR dataDir;

        public DataCacheEntry(FORMATETC fmt, IntPtr data, DATADIR dir)
        {
            this.format = fmt;
            this.data = (long)data;
            this.dataDir = dir;
        }
    }

    /// <summary>
    /// Unfortunately System.Windows.Forms.IDataObject and
    /// Microsoft.VisualStudio.OLE.Interop.IDataObject are different...
    /// </summary>
    internal class DataObject : IDataObject
    {
        EventSinkCollection map;

        ArrayList entries;

        public DataObject()
        {
            this.map = new EventSinkCollection();
            this.entries = new ArrayList();
        }

        // Public methods
        public void SetData(FORMATETC format, IntPtr data)
        {
            this.entries.Add(new DataCacheEntry(format, data, DATADIR.DATADIR_SET));
        }

        // IDataObject methods
        int IDataObject.DAdvise(FORMATETC[] e, uint adv, IAdviseSink sink, out uint cookie)
        {
            STATDATA sdata = new STATDATA();

            sdata.ADVF = adv;
            sdata.FORMATETC = e[0];
            sdata.pAdvSink = sink;
            cookie = this.map.Add(sdata);
            sdata.dwConnection = cookie;
            return 0;
        }

        void IDataObject.DUnadvise(uint cookie)
        {
            this.map.RemoveAt(cookie);
        }

        int IDataObject.EnumDAdvise(out IEnumSTATDATA e)
        {
            e = new EnumSTATDATA((IEnumerable)this.map);
            return 0; //??
        }

        int IDataObject.EnumFormatEtc(uint direction, out IEnumFORMATETC penum)
        {
            penum = new EnumFORMATETC((DATADIR)direction, (IEnumerable)this.entries);
            return 0;
        }

        static int DATA_S_SAMEFORMATETC = 0x00040130;

        static int DATA_E_FORMATETC = ForceCast(0x80040064);

        static int ForceCast(uint i)
        {
            unchecked { return (int)i; }
        }

        static uint ForceCast(int i)
        {
            unchecked { return (uint)i; }
        }

        int IDataObject.GetCanonicalFormatEtc(FORMATETC[] format, FORMATETC[] fmt)
        {
            throw new System.Runtime.InteropServices.COMException("", DATA_S_SAMEFORMATETC);
        }

        void IDataObject.GetData(FORMATETC[] fmt, STGMEDIUM[] m)
        {
            STGMEDIUM retMedium = new STGMEDIUM();

            if (fmt == null || fmt.Length < 1)
                return;

            foreach (DataCacheEntry e in this.entries)
            {
                if (e.format.cfFormat == fmt[0].cfFormat)
                {
                    retMedium.tymed = e.format.tymed;
                    retMedium.unionmember = CopyHGlobal(new IntPtr(e.data));
                    break;
                }
            }

            if (m != null && m.Length > 0)
                m[0] = retMedium;
        }

        void IDataObject.GetDataHere(FORMATETC[] fmt, STGMEDIUM[] m)
        {
        }

        int IDataObject.QueryGetData(FORMATETC[] fmt)
        {
            if (fmt == null || fmt.Length < 1)
                return NativeMethods.S_FALSE;

            foreach (DataCacheEntry e in this.entries)
            {
                if (e.format.cfFormat == fmt[0].cfFormat)
                    return NativeMethods.S_OK;
            }

            return NativeMethods.S_FALSE;
        }

        void IDataObject.SetData(FORMATETC[] fmt, STGMEDIUM[] m, int fRelease)
        {
        }

        // Beats me why this isn't in the Marshal class.
        [DllImport("kernel32.dll", EntryPoint = "GlobalLock", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr GlobalLock(IntPtr h);

        [DllImport("kernel32.dll", EntryPoint = "GlobalUnlock", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool GlobalUnLock(IntPtr h);

        [DllImport("kernel32.dll", EntryPoint = "GlobalSize", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern int GlobalSize(IntPtr h);

        //------------ privates-----------------------------------
        void FillFormatEtc(ref FORMATETC template, ushort clipFormat, ref FORMATETC result)
        {
            if (clipFormat != 0)
            {
                result = template;
                result.cfFormat = clipFormat;
                result.ptd = IntPtr.Zero;
                result.dwAspect = (uint)DVASPECT.DVASPECT_CONTENT;
                result.lindex = -1;
                result.tymed = (uint)TYMED.TYMED_NULL;
            }
        }

        void OleCopyFormatEtc(ref FORMATETC src, ref FORMATETC dest)
        {
            dest.cfFormat = src.cfFormat;
            dest.ptd = Marshal.AllocCoTaskMem(Marshal.SizeOf(src.ptd));
            Marshal.StructureToPtr(src.ptd, dest.ptd, false);
            dest.dwAspect = src.dwAspect;
            dest.lindex = src.lindex;
            dest.tymed = src.tymed;
        }

        public IntPtr CopyHGlobal(IntPtr data)
        {
            IntPtr src = GlobalLock(data);
            int size = GlobalSize(data);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            IntPtr buffer = GlobalLock(ptr);

            try
            {
                for (int i = 0; i < size; i++)
                {
                    byte val = Marshal.ReadByte(new IntPtr((long)src + i));

                    Marshal.WriteByte(new IntPtr((long)buffer + i), val);
                }
            }
            catch (Exception)
            {
            }
            GlobalUnLock(buffer);
            GlobalUnLock(src);
            return ptr;
        }

        public static void CopyStringToHGlobal(string s, IntPtr data, int bufferSize)
        {
            Int16 nullTerminator = 0;
            int dwSize = Marshal.SizeOf(nullTerminator);

            if ((s.Length + 1) * Marshal.SizeOf(s[0]) > bufferSize)
                throw new OverflowException("Buffer is too small");
            // IntPtr memory already locked...
            try
            {
                for (int i = 0, len = s.Length; i < len; i++)
                {
                    Marshal.WriteInt16(data, i * dwSize, s[i]);
                }
                // NULL terminate it
                Marshal.WriteInt16(new IntPtr((long)data + (s.Length * dwSize)), nullTerminator);
            }
            catch (Exception)
            {
            }
        }

        void CopyStgMedium(IntPtr cfFormat, ref STGMEDIUM lpDest, ref STGMEDIUM lpSource)
        {
            /* STGMEDIUM appears to be messed up...
      if (lpDest.tymed == TYMED_NULL) {
        Debug.Assert(lpSource.tymed != TYMED_NULL);
        switch (lpSource.tymed) {
          case TYMED_ENHMF:
          case TYMED_HGLOBAL:
            lpDest.tymed = lpSource.tymed;
            lpDest.hGlobal = NULL;
            break;  

          case TYMED_ISTREAM:
            lpDest.pstm = lpSource.pstm;
            lpDest.pstm.AddRef();
            lpDest.tymed = TYMED_ISTREAM;
            return TRUE;

          case TYMED_ISTORAGE:
            lpDest.pstg = lpSource.pstg;
            lpDest.pstg.AddRef();
            lpDest.tymed = TYMED_ISTORAGE;
            return TRUE;

          case TYMED_MFPICT: {
            // copy LPMETAFILEPICT struct + embedded HMETAFILE
            HGLOBAL hDest = ::CopyGlobalMemory(NULL, lpSource.hGlobal);
            if (hDest == NULL)
              return FALSE;
            LPMETAFILEPICT lpPict = (LPMETAFILEPICT)::GlobalLock(hDest);
            ASSERT(lpPict != NULL);
            lpPict.hMF = ::CopyMetaFile(lpPict.hMF, NULL);
            if (lpPict.hMF == NULL) {
              ::GlobalUnlock(hDest);
              ::GlobalFree(hDest);
              return FALSE;
            }
            ::GlobalUnlock(hDest);

            // fill STGMEDIUM struct
            lpDest.hGlobal = hDest;
            lpDest.tymed = TYMED_MFPICT;
          }
            return TRUE;

          case TYMED_GDI:
            lpDest.tymed = TYMED_GDI;
            lpDest.hGlobal = NULL;
            break;

          case TYMED_FILE: {
            USES_CONVERSION;
            lpDest.tymed = TYMED_FILE;
            ASSERT(lpSource.lpszFileName != NULL);
            UINT cbSrc = (int)ocslen(lpSource.lpszFileName);
            LPOLESTR szFileName = (LPOLESTR)CoTaskMemAlloc(cbSrc*sizeof(OLECHAR));
            lpDest.lpszFileName = szFileName;
            if (szFileName == NULL)
              return FALSE;
            memcpy(szFileName, lpSource.lpszFileName,  (cbSrc+1)*sizeof(OLECHAR));
            return TRUE;
          }

            // unable to create + copy other TYMEDs
          default:
            return FALSE;
        }
      }
      ASSERT(lpDest.tymed == lpSource.tymed);

      switch (lpSource.tymed) {
        case TYMED_HGLOBAL: {
          HGLOBAL hDest = ::CopyGlobalMemory(lpDest.hGlobal,
                              lpSource.hGlobal);
          if (hDest == NULL)
            return FALSE;

          lpDest.hGlobal = hDest;
        }
          return TRUE;

        case TYMED_ISTREAM: {
          ASSERT(lpDest.pstm != NULL);
          ASSERT(lpSource.pstm != NULL);

          // get the size of the source stream
          STATSTG stat;
          if (lpSource.pstm.Stat(&stat, STATFLAG_NONAME) != S_OK) {
            // unable to get size of source stream
            return FALSE;
          }
          ASSERT(stat.pwcsName == NULL);

          // always seek to zero before copy
          LARGE_INTEGER zero = { 0, 0 };
          lpDest.pstm.Seek(zero, STREAM_SEEK_SET, NULL);
          lpSource.pstm.Seek(zero, STREAM_SEEK_SET, NULL);

          // copy source to destination
          if (lpSource.pstm.CopyTo(lpDest.pstm, stat.cbSize,
            NULL, NULL) != NULL) {
            // copy from source to dest failed
            return FALSE;
          }

          // always seek to zero after copy
          lpDest.pstm.Seek(zero, STREAM_SEEK_SET, NULL);
          lpSource.pstm.Seek(zero, STREAM_SEEK_SET, NULL);
        }
          return TRUE;

        case TYMED_ISTORAGE: {
          ASSERT(lpDest.pstg != NULL);
          ASSERT(lpSource.pstg != NULL);

          // just copy source to destination
          if (lpSource.pstg.CopyTo(0, NULL, NULL, lpDest.pstg) != S_OK)
            return FALSE;
        }
          return TRUE;

        case TYMED_FILE: {
          USES_CONVERSION;
          ASSERT(lpSource.lpszFileName != NULL);
          ASSERT(lpDest.lpszFileName != NULL);
          return CopyFile(OLE2T(lpSource.lpszFileName), OLE2T(lpDest.lpszFileName), FALSE);
        }


        case TYMED_ENHMF:
        case TYMED_GDI: {
          ASSERT(sizeof(HGLOBAL) == sizeof(HENHMETAFILE));

          // with TYMED_GDI cannot copy into existing HANDLE
          if (lpDest.hGlobal != NULL)
            return FALSE;

          // otherwise, use OleDuplicateData for the copy
          lpDest.hGlobal = OleDuplicateData(lpSource.hGlobal, cfFormat, 0);
          if (lpDest.hGlobal == NULL)
            return FALSE;
        }
          return TRUE;

          // other TYMEDs cannot be copied
        default:
          return FALSE;
      }
      */
        }
    }
    internal enum DropDataType
    {//Drop types
        None,
        Shell,
        VsStg,
        VsRef
    }
    internal class DragDropHelper
    {
        private static ushort cf_VSREFPROJECTS = 0;
        private static ushort cf_VSSTGPROJECTS = 0;
        private static ushort cf_VSREFPROJECTITEMS = 0;
        private static ushort cf_VSSTGPROJECTITEMS = 0;

        private DragDropHelper() { }

        [DllImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        static extern ushort RegisterClipboardFormat(string format);

        public static ushort CF_VSREFPROJECTS
        {
            get
            {
                if (cf_VSREFPROJECTS == 0)
                    RegisterClipboardFormats();
                return cf_VSREFPROJECTS;
            }
        }
        public static ushort CF_VSSTGPROJECTS
        {
            get
            {
                if (cf_VSSTGPROJECTS == 0)
                    RegisterClipboardFormats();
                return cf_VSSTGPROJECTS;
            }
        }
        public static ushort CF_VSREFPROJECTITEMS
        {
            get
            {
                if (cf_VSREFPROJECTITEMS == 0)
                    RegisterClipboardFormats();
                return cf_VSREFPROJECTITEMS;
            }
        }
        public static ushort CF_VSSTGPROJECTITEMS
        {
            get
            {
                if (cf_VSSTGPROJECTITEMS == 0)
                    RegisterClipboardFormats();
                return cf_VSSTGPROJECTITEMS;
            }
        }

        private static void RegisterClipboardFormats()
        {
            if (cf_VSREFPROJECTITEMS == 0)
            {
                cf_VSREFPROJECTS = RegisterClipboardFormat("CF_VSREFPROJECTS");
                cf_VSSTGPROJECTS = RegisterClipboardFormat("CF_VSSTGPROJECTS");
                cf_VSREFPROJECTITEMS = RegisterClipboardFormat("CF_VSREFPROJECTITEMS");
                cf_VSSTGPROJECTITEMS = RegisterClipboardFormat("CF_VSSTGPROJECTITEMS");
            }
        }
        public static FORMATETC CreateFormatEtc(ushort iFormat)
        {
            FORMATETC fmt = new FORMATETC();
            fmt.cfFormat = iFormat;
            fmt.ptd = IntPtr.Zero;
            fmt.dwAspect = (uint)DVASPECT.DVASPECT_CONTENT;
            fmt.lindex = -1;
            fmt.tymed = (uint)TYMED.TYMED_HGLOBAL;
            return fmt;
        }
        public static FORMATETC CreateFormatEtc()
        {
            return CreateFormatEtc(CF_VSSTGPROJECTITEMS);
        }
        public static int QueryGetData(Microsoft.VisualStudio.OLE.Interop.IDataObject pDataObject, ref FORMATETC fmtetc)
        {
            FORMATETC[] af = new FORMATETC[1];
            af[0] = fmtetc;
            int hr = pDataObject.QueryGetData(af);
            fmtetc = af[0];
            return hr;
        }
        public static STGMEDIUM GetData(Microsoft.VisualStudio.OLE.Interop.IDataObject pDataObject, ref FORMATETC fmtetc)
        {
            FORMATETC[] af = new FORMATETC[1];
            af[0] = fmtetc;
            STGMEDIUM[] sm = new STGMEDIUM[1];
            pDataObject.GetData(af, sm);
            fmtetc = af[0];
            return sm[0];
        }
        public static bool AttemptVsFormat(HierarchyNode activeNode, ushort cfFormat, Microsoft.VisualStudio.OLE.Interop.IDataObject pDataObject, uint grfKeyState, out DropDataType pddt)
        {
            pddt = DropDataType.None;
            FORMATETC fmtetc = new FORMATETC();

            fmtetc.cfFormat = cfFormat;
            fmtetc.ptd = IntPtr.Zero;
            fmtetc.dwAspect = (uint)DVASPECT.DVASPECT_CONTENT;
            fmtetc.lindex = -1;
            fmtetc.tymed = (uint)TYMED.TYMED_HGLOBAL;

            bool hasData = false;
            try
            {
                QueryGetData(pDataObject, ref fmtetc);
                hasData = true;
            }
            catch
            {
            }

            if (hasData)
            {
                try
                {
                    STGMEDIUM stgmedium = GetData(pDataObject, ref fmtetc);
                    if (stgmedium.tymed == (uint)TYMED.TYMED_HGLOBAL)
                    {
                        IntPtr hDropInfo = stgmedium.unionmember;
                        if (hDropInfo != IntPtr.Zero)
                        {
                            pddt = DropDataType.VsRef;
                            try
                            {
                                activeNode.AddFiles(UtilGetFilesFromPROJITEMDrop(hDropInfo));
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(hDropInfo);
                            }
                            return true;
                        }
                    }
                }
                catch (Exception e)
                {
                    Trace.WriteLine("Exception:" + e.Message);
                }
                catch
                {
                    Trace.WriteLine("Exception");
                }
            }
            return false;
        }
        // Split the series of null terminated strings back up into an array of strings.
        static string[] UtilGetFilesFromPROJITEMDrop(IntPtr hDropInfo)
        {
            string[] result = null;
            IntPtr data = Microsoft.VisualStudio.Package.DataObject.GlobalLock(hDropInfo);
            try
            {
                _DROPFILES df = (_DROPFILES)Marshal.PtrToStructure(data, typeof(_DROPFILES));
                if (df.fWide != 0)
                {// unicode?
                    IntPtr pdata = new IntPtr((long)data + df.pFiles);
                    string s = Marshal.PtrToStringUni(pdata);
                    ArrayList list = new ArrayList();
                    int pos = 0;
                    int i = 0;
                    int len = s.Length;
                    for (; i < len; i++)
                    {
                        if (s[i] == '\0')
                        {
                            if (i == len - 1 || s[i + 1] == '\0')
                                break;
                            list.Add(s.Substring(pos, i - 1));
                            pos = i + 1;
                        }
                    }
                    if (i > pos)
                    {
                        list.Add(s.Substring(pos, i));
                    }
                    result = (String[])list.ToArray(typeof(string));
                }
            }
            catch
            {
            }
            DataObject.GlobalUnLock(data);
            return result;
        }
    } // end of dragdrophelper

    internal class EnumSTATDATA : IEnumSTATDATA
    {
        IEnumerable i;

        IEnumerator e;

        public EnumSTATDATA(IEnumerable i)
        {
            this.i = i;
            this.e = i.GetEnumerator();
        }

        void IEnumSTATDATA.Clone(out IEnumSTATDATA clone)
        {
            clone = new EnumSTATDATA(i);
        }

        int IEnumSTATDATA.Next(uint celt, STATDATA[] d, out uint fetched)
        {
            uint rc = 0;
            //uint size = (fetched != null) ? fetched[0] : 0;
            for (uint i = 0; i < celt; i++)
            {
                if (e.MoveNext())
                {
                    STATDATA sdata = (STATDATA)e.Current;

                    rc++;
                    if (d != null && d.Length > i)
                    {
                        d[i] = sdata;
                    }
                }
            }

            fetched = rc;
            return 0;
        }

        int IEnumSTATDATA.Reset()
        {
            e.Reset();
            return 0;
        }

        int IEnumSTATDATA.Skip(uint celt)
        {
            for (uint i = 0; i < celt; i++)
            {
                e.MoveNext();
            }

            return 0;
        }
    }

    internal class EnumFORMATETC : IEnumFORMATETC
    {
        IEnumerable cache; // of DataCacheEntrys.

        DATADIR dir;

        IEnumerator e;

        public EnumFORMATETC(DATADIR dir, IEnumerable cache)
        {
            this.cache = cache;
            this.dir = dir;
            e = cache.GetEnumerator();
        }

        void IEnumFORMATETC.Clone(out IEnumFORMATETC clone)
        {
            clone = new EnumFORMATETC(dir, cache);
        }

        int IEnumFORMATETC.Next(uint celt, FORMATETC[] d, uint[] fetched)
        {
            uint rc = 0;
            //uint size = (fetched != null) ? fetched[0] : 0;
            for (uint i = 0; i < celt; i++)
            {
                if (e.MoveNext())
                {
                    DataCacheEntry entry = (DataCacheEntry)e.Current;

                    rc++;
                    if (d != null && d.Length > i)
                    {
                        d[i] = entry.format;
                    }
                }
                else
                {
                    return NativeMethods.S_FALSE;
                }
            }

            if (fetched != null && fetched.Length > 0)
                fetched[0] = rc;
            return NativeMethods.S_OK;
        }

        int IEnumFORMATETC.Reset()
        {
            e.Reset();
            return 0;
        }

        int IEnumFORMATETC.Skip(uint celt)
        {
            for (uint i = 0; i < celt; i++)
            {
                e.MoveNext();
            }

            return 0;
        }
    }
}