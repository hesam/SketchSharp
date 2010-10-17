using System;
using Microsoft.VisualStudio.OLE.Interop;
using System.Runtime.InteropServices;
using System.Collections;

namespace Microsoft.VisualStudio.Package{
	
  public enum tagDVASPECT {
    DVASPECT_CONTENT	= 1,
    DVASPECT_THUMBNAIL	= 2,
    DVASPECT_ICON	= 4,
    DVASPECT_DOCPRINT	= 8
  }
  public enum tagTYMED {
    TYMED_HGLOBAL	= 1,
    TYMED_FILE	= 2,
    TYMED_ISTREAM	= 4,
    TYMED_ISTORAGE	= 8,
    TYMED_GDI	= 16,
    TYMED_MFPICT	= 32,
    TYMED_ENHMF	= 64,
    TYMED_NULL	= 0
  }


  struct DataCacheEntry {
    public FORMATETC      format;
    public IntPtr      data;
    public DATADIR  dataDir;

    public DataCacheEntry(FORMATETC fmt, IntPtr data, DATADIR dir) {
      this.format = fmt;
      this.data = data;
      this.dataDir = dir;
    }
  }

	/// <summary>
	/// Unfortunately System.Windows.Forms.IDataObject and
	/// Microsoft.VisualStudio.OLE.Interop.IDataObject are different...
	/// </summary>
	public class MyDataObject : IDataObject
	{
    CookieMap map;
    ArrayList entries;

		public MyDataObject()
		{
      this.map = new CookieMap();
      this.entries = new ArrayList();
		}

    // Public methods
    public void SetData(FORMATETC format, IntPtr data) {
      this.entries.Add(new DataCacheEntry(format, data, DATADIR.DATADIR_SET));
    }

    // IDataObject methods
    int IDataObject.DAdvise(FORMATETC[] e, uint adv, IAdviseSink sink, out uint cookie) {
      STATDATA sdata = new STATDATA();
      sdata.ADVF = adv;
      sdata.FORMATETC = e[0];
      sdata.pAdvSink = sink;
      cookie = this.map.Add(sdata);
      sdata.dwConnection = cookie;
      return 0;
    }

    void IDataObject.DUnadvise(uint cookie) {
      this.map.RemoveAt(cookie);
    }

    int IDataObject.EnumDAdvise(out IEnumSTATDATA e) {
      e = new EnumSTATDATA((IEnumerable)this.map);
      return 0; //??
    }

    int IDataObject.EnumFormatEtc(uint direction, out IEnumFORMATETC penum) {
      penum = new EnumFORMATETC((DATADIR)direction, (IEnumerable)this.entries);
      return 0;
    }

    static int DATA_S_SAMEFORMATETC = 0x00040130;
    static int DATA_E_FORMATETC = ForceCast(0x80040064);

    static int ForceCast(uint i) {
      unchecked { return (int)i; } 
    }   
    static uint ForceCast(int i) {
      unchecked { return (uint)i; } 
    }   

    int IDataObject.GetCanonicalFormatEtc(FORMATETC[] format, FORMATETC[] fmt) {
      throw new System.Runtime.InteropServices.COMException("",DATA_S_SAMEFORMATETC);
    }

    void IDataObject.GetData(FORMATETC[] fmt, STGMEDIUM[] m) {
      STGMEDIUM retMedium = new STGMEDIUM();
      foreach (DataCacheEntry e in this.entries) {
        if (e.format.cfFormat == fmt[0].cfFormat) {
          retMedium.tymed = e.format.tymed;
          retMedium.unionmember = CopyHGlobal(e.data);
          break;
        }
      }
      m[0] = retMedium;
    }

    void IDataObject.GetDataHere(FORMATETC[] fmt, STGMEDIUM[] m) {
      throw new NotImplementedException();
    }

    int IDataObject.QueryGetData(FORMATETC[] fmt) {   
      foreach (DataCacheEntry e in this.entries) {
        if (e.format.cfFormat == fmt[0].cfFormat)
          return 1;
      }
      return 0;
    }

    void IDataObject.SetData(FORMATETC[] fmt, STGMEDIUM[] m, int fRelease) {
      throw new NotImplementedException();
    }     


    // Beats me why this isn't in the Marshal class.
    [DllImport("kernel32.dll", EntryPoint="GlobalLock",  
       SetLastError=true, CharSet=CharSet.Unicode, ExactSpelling=true,
       CallingConvention=CallingConvention.StdCall)]
    public static extern IntPtr GlobalLock(IntPtr h);

    [DllImport("kernel32.dll", EntryPoint="GlobalUnlock",  
       SetLastError=true, CharSet=CharSet.Unicode, ExactSpelling=true,
       CallingConvention=CallingConvention.StdCall)]
    public static extern bool GlobalUnLock(IntPtr h);

    [DllImport("kernel32.dll", EntryPoint="GlobalSize",  
       SetLastError=true, CharSet=CharSet.Unicode, ExactSpelling=true,
       CallingConvention=CallingConvention.StdCall)]
    public static extern int GlobalSize(IntPtr h);

    //------------ privates-----------------------------------

    void FillFormatEtc(ref FORMATETC template, ushort clipFormat, ref FORMATETC result) {
      if (clipFormat != 0) {
        result = template;
        result.cfFormat = clipFormat;
        result.ptd = IntPtr.Zero;
        result.dwAspect = (uint)DVASPECT.DVASPECT_CONTENT;
        result.lindex = -1;
        result.tymed = (uint)TYMED.TYMED_NULL;
      }
    }

    void OleCopyFormatEtc(ref FORMATETC src, ref FORMATETC dest) {
      dest.cfFormat = src.cfFormat;
      dest.ptd = Marshal.AllocCoTaskMem(Marshal.SizeOf(src.ptd));
      Marshal.StructureToPtr(src.ptd, dest.ptd, false);
      dest.dwAspect = src.dwAspect;
      dest.lindex = src.lindex;
      dest.tymed = src.tymed;
    }

    public IntPtr CopyHGlobal(IntPtr data) {
      IntPtr src = GlobalLock(data);
      int size = GlobalSize(data);
      IntPtr ptr = Marshal.AllocHGlobal(size);
      IntPtr buffer = GlobalLock(ptr);
      try {
        for (int i = 0; i < size; i++) {
          byte val = Marshal.ReadByte(new IntPtr((long)src+i));
          Marshal.WriteByte(new IntPtr((long)buffer+i),val);
        }
      } catch (Exception) {
      }
      GlobalUnLock(buffer);
      GlobalUnLock(src);
      return ptr;
    }

    public static void CopyStringToHGlobal(string s, IntPtr data) {
      // IntPtr memory already locked...
      try {
        Int16 nullTerminator = 0;
        int dwSize = Marshal.SizeOf(nullTerminator);
        for (int i = 0, len = s.Length; i < len; i++) {
          char val = s[i];
        }
        // NULL terminate it
        Marshal.WriteInt16(new IntPtr((long)data+(s.Length*dwSize)), nullTerminator);
      } catch (Exception) {
      }
    }

    void CopyStgMedium(IntPtr cfFormat, ref STGMEDIUM lpDest, ref STGMEDIUM lpSource) {
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

  public class EnumSTATDATA : IEnumSTATDATA {
    IEnumerable i;
    IEnumerator e;

    public EnumSTATDATA(IEnumerable i) {
      this.i = i;
      this.e = i.GetEnumerator();
    }

    void IEnumSTATDATA.Clone(out IEnumSTATDATA clone) {
      clone = new EnumSTATDATA(i);
    }

    int IEnumSTATDATA.Next(uint celt, STATDATA[] d, out uint fetched) {
      uint rc = 0;
      //uint size = (fetched != null) ? fetched[0] : 0;
      for (uint i = 0; i < celt; i++) {
        if (e.MoveNext()) {
          STATDATA sdata = (STATDATA)e.Current;          
          rc++;
          if (d != null) {
            d[i] = sdata;
          }
        }
      }
      fetched = rc;
      return 0;      
    }

    int IEnumSTATDATA.Reset() {
      e.Reset();
      return 0; 
    }

    int IEnumSTATDATA.Skip(uint celt) {
      for (uint i = 0; i < celt; i++) {
        e.MoveNext();
      }
      return 0; 
    }
  }

  public class EnumFORMATETC : IEnumFORMATETC {
    IEnumerable cache; // of DataCacheEntrys.
    DATADIR dir;
    IEnumerator e;

    public EnumFORMATETC(DATADIR dir, IEnumerable cache) {
      this.cache = cache;
      this.dir = dir;
      e = cache.GetEnumerator();
    }

    void IEnumFORMATETC.Clone(out IEnumFORMATETC clone) {
      clone = new EnumFORMATETC(dir, cache);
    }

    int IEnumFORMATETC.Next(uint celt, FORMATETC[] d, uint[] fetched) {
      uint rc = 0;
      //uint size = (fetched != null) ? fetched[0] : 0;
      for (uint i = 0; i < celt; i++) {
        if (e.MoveNext()) {
          DataCacheEntry entry = (DataCacheEntry)e.Current;
          rc++;
          if (d != null) {
            d[i] = entry.format;
          }
        }
      }
      fetched[0] = rc;
      return 0;
    }

    int IEnumFORMATETC.Reset() {
      e.Reset();
      return 0; 
    }

    int IEnumFORMATETC.Skip(uint celt) {
      for (uint i = 0; i < celt; i++) {
        e.MoveNext();
      }
      return 0;
    }
  }

}
