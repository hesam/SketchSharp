//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Runtime.InteropServices;
using HRESULT = System.Int32;
using _mdToken = System.UInt32;


namespace Microsoft.VisualStudio.IntegrationHelper{
  // Imported from ee.idl
  [
  ComImport(),
  Guid("29ECD774-75AE-11d2-B74E-0000F87572EF"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public interface IDebugArrayObject /*: IDebugObject*/ 
  {
    //
    // IDebugObject
    //
    void GetSize(
      out System.UInt32		pnSize
      );
      
    void GetValue(
      //[Out, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.U1, SizeParamIndex=1)]
      [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.U1, SizeParamIndex=1)]
      System.Byte[]			pValue,
      System.UInt32			nSize
      );
      
    void SetValue(
      [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1)]
      System.Byte[]			pValue,
      System.UInt32			nSize
      );
      
    void SetReferenceValue(
      IDebugObject			pObject
      );
      
    void GetMemoryContext(
      out IDebugMemoryContext2    pContext
      );
      
    void GetManagedDebugObject(
      out IDebugManagedObject ppObject
      );
      
    [PreserveSig]
    HRESULT IsNullReference(
      out bool				pfIsNull
      );
      
    void IsEqual(
      IDebugObject			pObject,
      out bool				pfIsEqual
      );
      
    void IsReadOnly(
      out bool				pfIsReadOnly
      );
      
    void IsProxy(
      out bool				pfIsProxy
      );
      
    //
    // IDebugArrayObject
    //
    [PreserveSig]
    HRESULT  GetCount(
      out uint pdwElements);
      
    [PreserveSig]
    HRESULT GetElement(
      uint dwIndex,
      out IDebugObject  ppElement);
      
    [PreserveSig]
    HRESULT GetElements(
      out IEnumDebugObjects  ppEnum);
      
    [PreserveSig]
    HRESULT GetElements2(
      uint size,
      [In, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.U4, SizeParamIndex=0)]
      uint[] pdwIndices,
      out IEnumDebugObjects  ppEnum);
      
    [PreserveSig]
    HRESULT GetRank(
      out uint   pdwRank);
      
    [PreserveSig]
    HRESULT GetDimensions(
      uint dwCount,
      [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.U4, SizeParamIndex=0)]
      uint[] dwDimensions);
  }

  [
  ComImport(),
  Guid("EA786CF4-C09E-4714-98AD-67303271EC93"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public interface IDebugArrayObject2 /*:IDebugArrayObject*/ 
  {
    //
    // IDebugObject
    //
    void GetSize(
      out System.UInt32		pnSize
      );

    void GetValue(
      //[Out, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.U1, SizeParamIndex=1)]
      [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.U1, SizeParamIndex=1)]
      System.Byte[]			pValue,
      System.UInt32			nSize
      );

    void SetValue(
      [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1)]
      System.Byte[]			pValue,
      System.UInt32			nSize
      );

    void SetReferenceValue(
      IDebugObject			pObject
      );

    void GetMemoryContext(
      out IDebugMemoryContext2    pContext
      );

    void GetManagedDebugObject(
      out IDebugManagedObject ppObject
      );

    [PreserveSig]
    HRESULT IsNullReference(
      out bool				pfIsNull
      );

    void IsEqual(
      IDebugObject			pObject,
      out bool				pfIsEqual
      );

    void IsReadOnly(
      out bool				pfIsReadOnly
      );

    void IsProxy(
      out bool				pfIsProxy
      );

    //
    // IDebugArrayObject
    //
    [PreserveSig]
    HRESULT  GetCount(
      out uint pdwElements);

    [PreserveSig]
    HRESULT GetElement(
      uint dwIndex,
      out IDebugObject  ppElement);

    [PreserveSig]
    HRESULT GetElements(
      out IEnumDebugObjects  ppEnum);

    [PreserveSig]
    HRESULT GetElements2(
      uint size,
      [In, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.U4, SizeParamIndex=0)]
      uint[] pdwIndices,
      out IEnumDebugObjects  ppEnum);

    [PreserveSig]
    HRESULT GetRank(
      out uint   pdwRank);

    [PreserveSig]
    HRESULT GetDimensions(
      uint dwCount,
      [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.U4, SizeParamIndex=0)]
      uint[] dwDimensions);

    //
    // IDebugArrayObject2
    //
    [PreserveSig]
    HRESULT HasBaseIndices( out bool pfHasBaseIndices );

    [PreserveSig]
    HRESULT GetBaseIndices(
      uint rank,
      [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.U4, SizeParamIndex=0)]
      uint[] indices
      );
  }

  [
  ComImport(),
  Guid("C077C833-476C-11d2-B73C-0000F87572EF"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public  interface IDebugBinder 
  {
    void Bind(
      IDebugObject			pContainer,
      IDebugField				pField,
      out IDebugObject		ppObject
      );

    void ResolveDynamicType(
      IDebugDynamicField		pDynamic,
      out IDebugField			ppResolved
      );

    void ResolveRuntimeType(
      IDebugObject			pObject,
      out IDebugField			ppResolved
      );

    void GetMemoryContext(
      IDebugField					pField,                // Ask for a cxt based on a symbol (this can be NULL)
      System.UInt32				dwConstant,            // If pField is null, then the EE need a cxt wrapper around a constant.
      out IDebugMemoryContext2	ppMemCxt
      );

    void GetFunctionObject(
      out IDebugFunctionObject	ppFunction
      );
  }

  [
  ComImport(),
  Guid("B5A2A5EA-D5AB-11d2-9033-00C04FA302A1"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public  interface IDebugDynamicField 
  {/* : IDebugField */
    //
    //IDebugField
    //
        
    // Get user-displayable information
    void GetInfo(
      FIELD_INFO_FIELDS dwFields, 
      out FIELD_INFO pFieldInfo
      );

    // Get the kind of this field
    void GetKind(
      out FIELD_KIND pdwKind
      );

    // Get a field that describes the type of this field
    void GetType(
      out IDebugField ppType
      );

    // Get this field's container
    void GetContainer(
      out IDebugContainerField ppContainerField
      );

    // Get the field's address
    void GetAddress(
      out IDebugAddress ppAddress
      );

    // Get the size of the field in bytes
    void GetSize(
      out uint pdwSize
      );

    // Get extended info about this field (the caller must free the buffer via CoTaskMemFree)
    // Return S_FALSE when the funtion does not return anything useful.
    [PreserveSig]
    HRESULT GetExtendedInfo(
      ref Guid guidExtendedInfo, 
      // This is a "out BYTE[]" (or "BYTE **"), but the caller must free the returned buffer 
      // using CoTaskMemFree.. So we expose it as "IntPtr" to be able to use the Marshal methods
      out IntPtr prgBuffer,	
      out uint pdwLen
      );

    // S_OK if same type or symbols, S_FALSE if not
    [PreserveSig]
    HRESULT Equal(
      IDebugField pField
      );

    void GetTypeInfo(
      out TYPE_INFO pTypeInfo
      );
        
    //
    //IDebugDynamicField
    //

  }

  public  enum PARSEFLAGS : uint 
  {
    // the expression is an expression (not a statement)
    PARSE_EXPRESSION		= 0x0001,
    // the expression might contain function name/parameter signatures, and
    // the expression is to be parsed [and later evaluated] as an address
    PARSE_FUNCTION_AS_ADDRESS = 0x0002
  };
	


  [
  ComImport(),
  Guid("C077C822-476C-11d2-B73C-0000F87572EF"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public  interface IDebugExpressionEvaluator 
  {
    [ PreserveSig ]
    HRESULT Parse(
      [In,MarshalAs(UnmanagedType.LPWStr)]
      string                       upstrExpression,
      PARSEFLAGS                   dwFlags,
      uint                         nRadix,
      out  string                  pbstrError,
      out  uint                    pichError,
      out  IDebugParsedExpression  ppParsedExpression
      );

    [ PreserveSig ]
    HRESULT GetMethodProperty(
      IDebugSymbolProvider    pSymbolProvider,
      IDebugAddress           pAddress,
      IDebugBinder            pBinder,
      bool                    fIncludeHiddenLocals,
      out IDebugProperty2     ppProperty
      );

    [ PreserveSig ]
    HRESULT GetMethodLocationProperty(
      string				    upstrFullyQualifiedMethodPlusOffset,
      IDebugSymbolProvider	pSymbolProvider,
      IDebugAddress			pAddress,
      IDebugBinder			pBinder,
      out	IDebugProperty2 	ppProperty
      );

    [ PreserveSig ]
    HRESULT SetLocale(
      ushort wLangID
      );

    [ PreserveSig ]
    HRESULT SetRegistryRoot(
      string               ustrRegistryRoot
      );
  }

  [ Flags ]
  public  enum OBJECT_TYPE : uint 
  {
    OBJECT_TYPE_BOOLEAN = 0x0,
    OBJECT_TYPE_CHAR    = 0x1,
    OBJECT_TYPE_I1      = 0x2,
    OBJECT_TYPE_U1      = 0x3,
    OBJECT_TYPE_I2      = 0x4,
    OBJECT_TYPE_U2      = 0x5,
    OBJECT_TYPE_I4      = 0x6,
    OBJECT_TYPE_U4      = 0x7,
    OBJECT_TYPE_I8      = 0x8,
    OBJECT_TYPE_U8      = 0x9,
    OBJECT_TYPE_R4      = 0xa,
    OBJECT_TYPE_R8      = 0xb,
    OBJECT_TYPE_OBJECT  = 0xc,
    OBJECT_TYPE_NULL    = 0xd,
    OBJECT_TYPE_CLASS   = 0xe
  }

  [
  ComImport(),
  Guid("F71D9EA0-4269-48dc-9E8D-F86DEFA042B3"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public  interface IDebugFunctionObject 
  { /*: IDebugObject*/

    //
    // IDebugObject 
    //

    void GetSize(
      out System.UInt32		pnSize
      );

    void GetValue(
      //[Out, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.U1, SizeParamIndex=1)]
      [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1)]
      System.Byte[]			pValue,
      System.UInt32			nSize
      );

    void SetValue(
      [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1)]
      System.Byte[]			pValue,
      System.UInt32			nSize
      );

    void SetReferenceValue(
      IDebugObject			pObject
      );

    void GetMemoryContext(
      out IDebugMemoryContext2    pContext
      );

    void GetManagedDebugObject(
      out IDebugManagedObject ppObject
      );

    [PreserveSig]
    HRESULT IsNullReference(
      out bool				pfIsNull
      );

    void IsEqual(
      IDebugObject			pObject,
      out bool				pfIsEqual
      );

    void IsReadOnly(
      out bool				pfIsReadOnly
      );

    void IsProxy(
      out bool				pfIsProxy
      );

    //
    // IDebugFunctionObject
    //

    void CreatePrimitiveObject(
      OBJECT_TYPE				ot,
      out IDebugObject		ppObject
      );

    void CreateObject(
      IDebugFunctionObject	pConstructor,
      uint					dwArgs,
      [MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.Interface, SizeParamIndex=1)]
      IDebugObject[]			pArgs,
      out IDebugObject		ppObject
      );

    void CreateObjectNoConstructor(
      IDebugField				pClassField,
      out IDebugObject		ppObject
      );

    void CreateArrayObject(
      OBJECT_TYPE				ot,
      IDebugField				pClassField,
      uint					dwRank,
      [MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.U4, SizeParamIndex=2)]
      uint[]					dwDims,
      [MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.U4, SizeParamIndex=2)]
      uint[]					dwLowBounds,
      out IDebugObject		ppObject
      );

    void CreateStringObject(
      String					pcstrString,
      out IDebugObject		ppOjbect
      );

    void Evaluate(
      [MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.Interface, SizeParamIndex=1)]
      IDebugObject[]			ppParams,
      uint					dwParams,
      uint					dwTimeout,
      out IDebugObject		ppResult
      );
  }

  [
  ComImport(),
  Guid("71AF87C9-66C5-49e4-A602-B9012115AFD5"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public  interface IDebugManagedObject 
  {
  }

  [
  ComImport(),
  Guid("C077C823-476C-11d2-B73C-0000F87572EF"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public  interface IDebugObject 
  {
    void GetSize(
      out System.UInt32		pnSize
      );

    void GetValue(
      //[Out, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.U1, SizeParamIndex=1)]
      [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.U1, SizeParamIndex=1)]
      System.Byte[]			pValue,
      System.UInt32			nSize
      );

    void SetValue(
      [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1)]
      System.Byte[]			pValue,
      System.UInt32			nSize
      );

    void SetReferenceValue(
      IDebugObject			pObject
      );

    void GetMemoryContext(
      out IDebugMemoryContext2    pContext
      );

    void GetManagedDebugObject(
      out IDebugManagedObject ppObject
      );

    [PreserveSig]
    HRESULT IsNullReference(
      out bool				pfIsNull
      );

    void IsEqual(
      IDebugObject			pObject,
      out bool				pfIsEqual
      );

    void IsReadOnly(
      out bool				pfIsReadOnly
      );

    void IsProxy(
      out bool				pfIsProxy
      );
  }

  [
  ComImport(),
  Guid("7895C94C-5A3F-11d2-B742-0000F87572EF"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public  interface IDebugParsedExpression 
  {
    void EvaluateSync(
      uint dwEvalFlags, 
      uint dwTimeout, 
      IDebugSymbolProvider pSymbolProvider,
      IDebugAddress pAddress, 
      IDebugBinder pBinder, 
      String bstrResultType, 
      out IDebugProperty2 ppResult
      );
  }

  [
  ComImport(),
  Guid("112756A1-3F04-4ccd-BFD6-ACB4BCA614C9"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]

  public  interface IDebugPointerObject 
  {
  }

  [
  ComImport(),
  Guid("0881751C-99F4-11d2-B767-0000F87572EF"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public  interface IEnumDebugObjects 
  {

    void Next(
      System.Int32 celt, 
      [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.Interface, SizeParamIndex=0)]
      IDebugObject []rgelt, 
      out System.Int32 pceltFetched);

    void Skip(
      System.Int32 celt);

    void Reset();

    void Clone(
      out IEnumDebugObjects ppEnum);

    void GetCount(
      out System.Int32 pcelt);
  }

  // Imported from msdbg.idl
  internal class FilterGuids 
  {
    internal static Guid guidFilterLocals = new Guid("B200F725-E725-4C53-B36A-1EC27AEF12EF");
    internal static Guid guidFilterAllLocals = new Guid("196DB21F-5F22-45A9-B5A3-32CDDB30DB06");
    internal static Guid guidFilterArgs = new Guid("804BCCEA-0475-4AE7-8A46-1862688AB863");
    internal static Guid guidFilterLocalsPlusArgs = new Guid("E74721BB-10C0-40F5-807F-920D37F95419");
    internal static Guid guidFilterAllLocalsPlusArgs = new Guid("00000000-0000-0000-0000-000000000000");
    internal static Guid guidFilterRegisters = new Guid("00000000-0000-0000-0000-000000000000");
    internal static Guid guidFilterThis = new Guid("ADD901FD-BFC9-48B2-B0C7-68B459539D7A");

  }

  [Flags]
  public  enum EVALFLAGS : uint 
  {
    // the return value is interesting
    EVAL_RETURNVALUE			= 0x0002,
    // don't allow side effects
    EVAL_NOSIDEEFFECTS			= 0x0004,
    // stop on breakpoints
    EVAL_ALLOWBPS				= 0x0008,
    // allow error reporting to the host
    EVAL_ALLOWERRORREPORT		= 0x0010,
    // evaluate any functions as address (instead of invoking the function)
    EVAL_FUNCTION_AS_ADDRESS	= 0x0040,
    // don't allow function/property evaluation
    EVAL_NOFUNCEVAL				= 0x0080,
    // don't allow events
    EVAL_NOEVENTS				= 0x1000,
  }

  [Flags]
  public  enum CONTEXT_COMPARE : uint 
  {
    CONTEXT_EQUAL					= 0x0001,
    CONTEXT_LESS_THAN				= 0x0002,
    CONTEXT_GREATER_THAN			= 0x0003,
    CONTEXT_LESS_THAN_OR_EQUAL		= 0x0004,
    CONTEXT_GREATER_THAN_OR_EQUAL	= 0x0005,
    CONTEXT_SAME_SCOPE				= 0x0006,
    CONTEXT_SAME_FUNCTION			= 0x0007,
    CONTEXT_SAME_MODULE				= 0x0008,
    CONTEXT_SAME_PROCESS			= 0x0009,
  }

  [Flags]
  public  enum CONTEXT_INFO_FIELDS : uint 
  {
    CIF_MODULEURL		= 0x00000001,
    CIF_FUNCTION		= 0x00000002,
    CIF_FUNCTIONOFFSET	= 0x00000004,
    CIF_ADDRESS			= 0x00000008,
    CIF_ADDRESSOFFSET	= 0x00000010,
    CIF_ADDRESSABSOLUTE = 0x00000020,
		
    CIF_ALLFIELDS		= 0x0000003f,
  };

  [StructLayout(LayoutKind.Sequential)]
  public  struct CONTEXT_INFO 
  {
    public CONTEXT_INFO_FIELDS dwFields;
    [MarshalAs(UnmanagedType.BStr)]
    public string bstrModuleUrl;
    [MarshalAs(UnmanagedType.BStr)]
    public string bstrFunction;
    public TEXT_POSITION posFunctionOffset;
    [MarshalAs(UnmanagedType.BStr)]
    public string bstrAddress;
    [MarshalAs(UnmanagedType.BStr)]
    public string bstrAddressOffset;
    [MarshalAs(UnmanagedType.BStr)]
    public string bstrAddressAbsolute;
  }


  [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),Guid("ac17b76b-2b09-419a-ad5f-7d7402da8875")]
  public  interface IDebugCodeContext2 
  {/* : IDebugMemoryContext2 */
    //
    //IDebugMemoryContext2
    //
        
    void GetName(
      [Out, MarshalAs(UnmanagedType.BStr)]
      out string pbstrName);

    void GetInfo(
      CONTEXT_INFO_FIELDS dwFields, 
      out CONTEXT_INFO pInfo);

    void Add(
      UInt64 dwCount, 
      out IDebugMemoryContext2 ppMemCxt);

    void Subtract(
      UInt64 dwCount, 
      out IDebugMemoryContext2 ppMemCxt);

    void Compare(
      CONTEXT_COMPARE compare, 
      [In, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.Interface, SizeParamIndex=2)]
      IDebugMemoryContext2 []rgpMemoryContextSet, 
      uint dwMemoryContextSetLen, 
      out uint pdwMemoryContext);
        
    //
    //IDebugCodeContext2
    //

    void GetDocumentContext(
      out IDebugDocumentContext2 ppSrcCxt);

    void GetLanguageInfo(
      [Out, MarshalAs(UnmanagedType.BStr)]
      out string pbstrLanguage, 
      out Guid pguidLanguage);
  }

  [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),Guid("1606dd73-5d5f-405c-b4f4-ce32baba2501")]
  public  interface IDebugDocument2 
  {

    void GetName(
      GETNAME_TYPE gnType,
      [Out, MarshalAs(UnmanagedType.BStr)]
      out string pbstrFileName);

    void GetDocumentClassId(
      out Guid pclsid);
  }

  public  enum GETNAME_TYPE : uint 
  {
    GN_NAME,				// Gets the (as friendly as possible) name of the document or context
    GN_FILENAME,			// Gets the full path file name (drive+path+filename+ext or as much as possible) of the document or context
    GN_BASENAME,			// Gets the basename+ext part of the file name
    GN_MONIKERNAME,			// Gets the unique, monikerized name of the document or context
    GN_URL,					// Gets the URL name of the document or context
    GN_TITLE,				// Gets the title of the document if possible.
    GN_STARTPAGEURL,		// Gets the start page URL for processes -- used for XSP/ATL Server debugging
  }

  public  enum TEXT_POSITION_LIMITS : uint 
  {
    TEXT_POSITION_MAX_LINE		= 0xffffffff,
    TEXT_POSITION_MAX_COLUMN	= 0xffffffff,
  }

  [StructLayout(LayoutKind.Sequential)]
  public  struct TEXT_POSITION 
  {
    public uint dwLine;
    public uint dwColumn;
  }

  public  enum DOCCONTEXT_COMPARE : uint 
  {
    DOCCONTEXT_EQUAL			= 0x0001,
    DOCCONTEXT_LESS_THAN		= 0x0002,
    DOCCONTEXT_GREATER_THAN		= 0x0003,
    DOCCONTEXT_SAME_DOCUMENT	= 0x0004,
  }

  [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),Guid("931516ad-b600-419c-88fc-dcf5183b5fa9")]
  public  interface IDebugDocumentContext2 
  {
    void GetDocument(
      out IDebugDocument2 ppDocument);

    void GetName(
      GETNAME_TYPE gnType, 
      [Out, MarshalAs(UnmanagedType.BStr)]
      out string pbstrFileName);

    void EnumCodeContexts(
      out IEnumDebugCodeContexts2 ppEnumCodeCxts);

    void GetLanguageInfo(
      [Out, MarshalAs(UnmanagedType.BStr)]
      out string pbstrLanguage, 
      /*TODO: [in, out, ptr] */
      out Guid pguidLanguage);

    void GetStatementRange(
      /*TODO: [in, out, ptr] */
      out TEXT_POSITION pBegPosition, 
      /*TODO: [in, out, ptr] */
      out TEXT_POSITION pEndPosition);

    void GetSourceRange(
      /*TODO: [in, out, ptr] */
      out TEXT_POSITION pBegPosition, 
      /*TODO: [in, out, ptr] */
      out TEXT_POSITION pEndPosition);


    void Compare(
      DOCCONTEXT_COMPARE compare, 
      [In, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.Interface, SizeParamIndex=2)]
      IDebugDocumentContext2 []rgpDocContextSet, 
      uint dwDocContextSetLen, 
      out uint pdwDocContext);

    void Seek(
      int nCount, 
      out IDebugDocumentContext2 ppDocContext);
  }

  [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),Guid("bdde0eee-3b8d-4c82-b529-33f16b42832e")]
  public  interface IDebugDocumentPosition2 
  {
    void GetFileName(
      [Out, MarshalAs(UnmanagedType.BStr)]
      out string pbstrFileName);

    void GetDocument(
      out IDebugDocument2 ppDoc);

    //PreserveSig: Must return S_OK or S_FALSE.
    [PreserveSig]
    HRESULT IsPositionInDocument(
      IDebugDocument2 pDoc);

    void GetRange(
      out TEXT_POSITION pBegPosition, 
      out TEXT_POSITION pEndPosition);
  }

  [
  ComImport(),
  Guid("925837d1-3aa1-451a-b7fe-cc04bb42cfb8"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public  interface IDebugMemoryBytes2 
  {
  }

  [
  ComImport(),
  Guid("1ab276dd-f27b-4445-825d-5df0b4a04a3a"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public  interface IDebugMemoryContext2 
  {
    void GetName(
      [Out, MarshalAs(UnmanagedType.BStr)]
      out string pbstrName
      );

    void GetInfo(
      CONTEXT_INFO_FIELDS dwFields, 
      out CONTEXT_INFO pInfo
      );

    void Add(
      UInt64 dwCount, 
      out IDebugMemoryContext2 ppMemCxt
      );

    void Subtract(
      UInt64 dwCount, 
      out IDebugMemoryContext2 ppMemCxt
      );

    void Compare(
      CONTEXT_COMPARE compare, 
      [In, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.Interface, SizeParamIndex=2)]
      IDebugMemoryContext2 []rgpMemoryContextSet, 
      uint dwMemoryContextSetLen, 
      out uint pdwMemoryContext
      );
  }

  [ Flags ]
  public  enum DEBUGPROP_INFO_FLAGS : uint 
  {
    DEBUGPROP_INFO_FULLNAME				= 0x00000001,
    DEBUGPROP_INFO_NAME					= 0x00000002,
    DEBUGPROP_INFO_TYPE					= 0x00000004,
    DEBUGPROP_INFO_VALUE				= 0x00000008,
    DEBUGPROP_INFO_ATTRIB				= 0x00000010,
    DEBUGPROP_INFO_PROP					= 0x00000020,

    DEBUGPROP_INFO_VALUE_AUTOEXPAND		= 0x00010000,
    DEBUGPROP_INFO_NOFUNCEVAL			= 0x00020000, // hack for VS7.0 for locals window scenario

    DEBUGPROP_INFO_NONE					= 0x00000000,
    DEBUGPROP_INFO_STANDARD				= DEBUGPROP_INFO_ATTRIB | DEBUGPROP_INFO_NAME | DEBUGPROP_INFO_TYPE | DEBUGPROP_INFO_VALUE,
    DEBUGPROP_INFO_ALL					= 0xffffffff
  }
	
  [ StructLayout( LayoutKind.Sequential ) ]
  public  struct DEBUG_PROPERTY_INFO 
  {
    public DEBUGPROP_INFO_FLAGS	dwFields;
    [MarshalAs(UnmanagedType.BStr)]
    public string 				bstrFullName;
    [MarshalAs(UnmanagedType.BStr)]
    public string				bstrName;
    [MarshalAs(UnmanagedType.BStr)]
    public string				bstrType;
    [MarshalAs(UnmanagedType.BStr)]
    public string				bstrValue;
    public IDebugProperty2		pProperty;
    public DBG_ATTRIB_FLAGS		dwAttrib;
  }

  [ Flags ]
  public  enum DBG_ATTRIB_FLAGS : ulong 
  {
    DBG_ATTRIB_NONE						= 0x0000000000000000,
    DBG_ATTRIB_ALL						= 0xffffffffffffffff,

    // Attributes about the object itself

    // The reference/property is expandable
    DBG_ATTRIB_OBJ_IS_EXPANDABLE		= 0x0000000000000001,

    // Attributes about the value of the object

    // The value of this reference/property is read only
    DBG_ATTRIB_VALUE_READONLY			= 0x0000000000000010,
    // The value is an error
    DBG_ATTRIB_VALUE_ERROR				= 0x0000000000000020,
    // The evaluation caused a side effect
    DBG_ATTRIB_VALUE_SIDE_EFFECT		= 0x0000000000000040,
    // This property is really a container of overloads
    DBG_ATTRIB_OVERLOADED_CONTAINER		= 0x0000000000000080,
    // This property is a boolean value
    DBG_ATTRIB_VALUE_BOOLEAN			= 0x0000000000000100,
    // If DBG_ATTRIB_VALUE_BOOLEAN is set,
    // then this flag indicates whether the boolean value is true or false
    DBG_ATTRIB_VALUE_BOOLEAN_TRUE		= 0x0000000000000200,
    // The value for this property is invalid (i.e. has no value)
    DBG_ATTRIB_VALUE_INVALID			= 0x0000000000000400,
    // The value for this property is NAT (not a thing)
    DBG_ATTRIB_VALUE_NAT				= 0x0000000000000800,
    // The value for this property has possibly been autoexpanded
    DBG_ATTRIB_VALUE_AUTOEXPANDED		= 0x0000000000001000,

    // Attributes that describe field access control
    DBG_ATTRIB_ACCESS_NONE				= 0x0000000000010000,
    DBG_ATTRIB_ACCESS_PUBLIC			= 0x0000000000020000,
    DBG_ATTRIB_ACCESS_PRIVATE			= 0x0000000000040000,
    DBG_ATTRIB_ACCESS_PROTECTED			= 0x0000000000080000,
    DBG_ATTRIB_ACCESS_FINAL				= 0x0000000000100000,

    DBG_ATTRIB_ACCESS_ALL				= 0x00000000001f0000,

    // Attributes that describe storage types
    DBG_ATTRIB_STORAGE_NONE				= 0x0000000001000000,
    DBG_ATTRIB_STORAGE_GLOBAL			= 0x0000000002000000,
    DBG_ATTRIB_STORAGE_STATIC			= 0x0000000004000000,
    DBG_ATTRIB_STORAGE_REGISTER			= 0x0000000008000000,

    DBG_ATTRIB_STORAGE_ALL				= 0x000000000f000000,

    // Attributes that describe type modifiers
    DBG_ATTRIB_TYPE_NONE				= 0x0000000100000000,
    DBG_ATTRIB_TYPE_VIRTUAL				= 0x0000000200000000,
    DBG_ATTRIB_TYPE_CONSTANT			= 0x0000000400000000,
    DBG_ATTRIB_TYPE_SYNCHRONIZED		= 0x0000000800000000,
    DBG_ATTRIB_TYPE_VOLATILE			= 0x0000001000000000,

    DBG_ATTRIB_TYPE_ALL					= 0x0000001f00000000,

    // Attributes that describe the IDebugProperty2 type
    DBG_ATTRIB_DATA						= 0x0000010000000000,
    DBG_ATTRIB_METHOD					= 0x0000020000000000,
    DBG_ATTRIB_PROPERTY					= 0x0000040000000000,
    DBG_ATTRIB_CLASS					= 0x0000080000000000,
    DBG_ATTRIB_BASECLASS				= 0x0000100000000000,
    DBG_ATTRIB_INTERFACE				= 0x0000200000000000,
    DBG_ATTRIB_INNERCLASS				= 0x0000400000000000,
    DBG_ATTRIB_MOSTDERIVEDCLASS			= 0x0000800000000000,

    DBG_ATTRIB_CHILD_ALL				= 0x0000ff0000000000

  };


  /// <summary>
  /// Summary description for IDebugProperty2.
  /// </summary>
  [
  ComImport(),
  Guid("a7ee3e7e-2dd2-4ad7-9697-f4aae3427762"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public  interface IDebugProperty2 
  {

    // Get the DEBUG_PROPERTY_INFO that describes this property
    void GetPropertyInfo(
      DEBUGPROP_INFO_FLAGS dwFields, 
      uint dwRadix, 
      uint dwTimeout, 
      [In, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.Interface, SizeParamIndex=4)]
      IDebugReference2 []rgpArgs, 
      uint dwArgCount, 
      out DEBUG_PROPERTY_INFO pPropertyInfo);

    // Set the value of this property
    void SetValueAsString(
      [In, MarshalAs(UnmanagedType.LPWStr)]
      String pszValue, 
      uint dwRadix, 
      uint dwTimeout);

    // Set the value of this property
    void SetValueAsReference(
      [In, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.Interface, SizeParamIndex=1)]
      IDebugReference2 []rgpArgs, 
      uint dwArgCount, 
      IDebugReference2 pValue, 
      uint dwTimeout);

    // Enum the children of this property
    void EnumChildren(
      DEBUGPROP_INFO_FLAGS dwFields, 
      uint dwRadix, 
      //TODO
      ref Guid guidFilter, 
      DBG_ATTRIB_FLAGS dwAttribFilter, 
      [In, MarshalAs(UnmanagedType.LPWStr)]
      String pszNameFilter, 
      uint dwTimeout, 
      out IEnumDebugPropertyInfo2 ppEnum);

    // Get the parent of this property
    void GetParent(
      out IDebugProperty2 ppParent);

    // Get the property that describes the derived most property of this property
    void GetDerivedMostProperty(
      out IDebugProperty2 ppDerivedMost);

    // Get the memory bytes that contains this property
    void GetMemoryBytes(
      out IDebugMemoryBytes2 ppMemoryBytes);

    // Get a memory context for this property within the memory bytes returned by GetMemoryBytes
    void GetMemoryContext(
      out IDebugMemoryContext2 ppMemory);

    // Get the size (in bytes) of this property
    void GetSize(
      out uint pdwSize);

    // Get a reference for this property
    void GetReference(
      out IDebugReference2 ppReference);

    // Get extended info for this property
    void GetExtendedInfo(
      ref Guid guidExtendedInfo, 
      /*VARIANT*/
      out object pExtendedInfo);
  }

  [ Flags ]
  public  enum REFERENCE_TYPE : uint 
  {
    // Weak reference
    REF_TYPE_WEAK						= 0x0001,
    // Strong reference
    REF_TYPE_STRONG						= 0x0002,
  };
	

  [ Flags ]
  public  enum DEBUGREF_INFO_FLAGS : uint 
  {
    DEBUGREF_INFO_NAME					= 0x00000001,
    DEBUGREF_INFO_TYPE					= 0x00000002,
    DEBUGREF_INFO_VALUE					= 0x00000004,
    DEBUGREF_INFO_ATTRIB				= 0x00000008,
    DEBUGREF_INFO_REFTYPE				= 0x00000010,
    DEBUGREF_INFO_REF					= 0x00000020,

    DEBUGREF_INFO_VALUE_AUTOEXPAND		= 0x00010000,

    DEBUGREF_INFO_NONE					= 0x00000000,
    DEBUGREF_INFO_ALL					= 0xffffffff
  };
	

  [ StructLayout( LayoutKind.Sequential ) ]
  public  struct DEBUG_REFERENCE_INFO 
  {
    public DEBUGREF_INFO_FLAGS		dwFields;
    [MarshalAs(UnmanagedType.BStr)]
    public string					bstrName;
    [MarshalAs(UnmanagedType.BStr)]
    public string					bstrType;
    [MarshalAs(UnmanagedType.BStr)]
    public string					bstrValue;
    public DBG_ATTRIB_FLAGS		dwAttrib;
    public REFERENCE_TYPE			dwRefType;
    public IDebugReference2		pReference;
  };

  [
  ComImport(),
  Guid("10b793ac-0c47-4679-8454-adb36f29f802"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public  interface IDebugReference2 
  {
  }

  [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),Guid("ad47a80b-eda7-459e-af82-647cc9fbaa50")]
  public  interface IEnumDebugCodeContexts2 
  {
    void Next(
      ulong celt, 
      [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.Interface, SizeParamIndex=0)]
      IDebugCodeContext2 []rgelt, 
      out ulong pceltFetched);

    void Skip(
      ulong celt);

    HRESULT Reset();

    void Clone(
      out IEnumDebugCodeContexts2 ppEnum);

    void GetCount(
      out ulong pcelt);
  }

  [
  ComImport(),
  Guid("c2e34ebc-8b9d-11d2-9014-00c04fa38338"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]

  public  interface IEnumDebugFields 
  {

    void Next(
      System.Int32 celt, 
      [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.Interface, SizeParamIndex=0)]
      IDebugField []rgelt, 
      out System.Int32 pceltFetched);

    void Skip(
      System.Int32 celt);

    void Reset();

    void Clone(
      out IEnumDebugFields ppEnum);

    void GetCount(
      out System.Int32 pcelt);
  }

  // IEnumDebugPropertyInfo2
  [
  ComImport(),
  Guid("6c7072c3-3ac4-408f-a680-fc5a2f96903e"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public  interface IEnumDebugPropertyInfo2 
  {
    void Next(
      System.UInt32 celt, 
      [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=0)]
      DEBUG_PROPERTY_INFO []rgelt, 
      out System.Int32 pceltFetched);

    void Skip(
      System.UInt32 celt);

    void Reset();

    void Clone(
      out IEnumDebugPropertyInfo2 ppEnum);

    void GetCount(
      out System.UInt32 pcelt);
  }
  
  // Imported from sh.idl
  public  enum ADDRESS_KIND : uint 
  {
    ADDRESS_KIND_NATIVE						= 0x0001,
    ADDRESS_KIND_UNMANAGED_THIS_RELATIVE	= 0x0002,
    ADDRESS_KIND_UNMANAGED_PHYSICAL			= 0x0005,
    ADDRESS_KIND_METADATA_METHOD			= 0x0010,
    ADDRESS_KIND_METADATA_FIELD				= 0x0011,
    ADDRESS_KIND_METADATA_LOCAL				= 0x0012,
    ADDRESS_KIND_METADATA_PARAM				= 0x0013,
    ADDRESS_KIND_METADATA_ARRAYELEM			= 0x0014,
    ADDRESS_KIND_METADATA_RETVAL			= 0x0015,
  }

  [StructLayout(LayoutKind.Sequential)]
  public  struct NATIVE_ADDRESS 
  {
    public System.UInt32 unknown;
  }

  [StructLayout(LayoutKind.Sequential)]
  public  struct UNMANAGED_ADDRESS_THIS_RELATIVE 
  {
    public System.UInt32 dwOffset;
    public System.UInt32 dwBitOffset;  // This is 0 unless a bit field
    public System.UInt32 dwBitLength;  // This is 0 unless a bit field
  }

  [StructLayout(LayoutKind.Sequential)]
  public  struct UNMANAGED_ADDRESS_PHYSICAL 
  {
    public System.UInt64 offset;
  }

  [StructLayout(LayoutKind.Sequential)]
  public  struct METADATA_ADDRESS_METHOD 
  {
    public _mdToken tokMethod;
    public System.UInt32 dwOffset;
    public System.UInt32 dwVersion;
  }

  [StructLayout(LayoutKind.Sequential)]
  public  struct METADATA_ADDRESS_FIELD 
  {
    public _mdToken tokField;
  }

  [StructLayout(LayoutKind.Sequential)]
  public  struct METADATA_ADDRESS_LOCAL 
  {
    public _mdToken tokMethod;
    //rpaquay: 
    // We can't make this an "object" member, because it is not supported
    // by the CLR type system (because the object field is overlapped by a
    // non object field, which would break the type system).
    // The solution is to declare the field as a IntPtr, then make it to an RCW
    // (using Marshal.GetObjectForIUnknown).
    // TODO:NOT ACTUALLY TESTED!!!
    //[MarshalAs(UnmanagedType.Interface)]
    //public object pLocal;
    public IntPtr pLocal;
    public System.UInt32 dwIndex;
  }

  [StructLayout(LayoutKind.Sequential)]
  public  struct METADATA_ADDRESS_PARAM 
  {
    public _mdToken tokMethod;
    public _mdToken tokParam;
    public System.UInt32 dwIndex;
  }

  [StructLayout(LayoutKind.Sequential)]
  public  struct METADATA_ADDRESS_ARRAYELEM 
  {
    public _mdToken tokMethod;
    public System.UInt32 dwIndex;
  }

  [StructLayout(LayoutKind.Sequential)]
  public  struct METADATA_ADDRESS_RETVAL 
  {
    public _mdToken tokMethod;
    public System.UInt32 dwCorType;
    public System.UInt32 dwSigSize;
    //[MarshalAs(UnmanagedType.U1, SizeConst=10)]
    //public BYTE []rgSig;
    public System.Byte rgSrgB1;
    public System.Byte rgSrgB2;
    public System.Byte rgSrgB3;
    public System.Byte rgSrgB4;
    public System.Byte rgSrgB5;
    public System.Byte rgSrgB6;
    public System.Byte rgSrgB7;
    public System.Byte rgSrgB8;
    public System.Byte rgSrgB9;
    public System.Byte rgSrgB10;
  }

  [StructLayout(LayoutKind.Explicit)]
  public  struct DEBUG_ADDRESS_UNION 
  {
    private const int Offset = 8; // sizeof dwKind. don't why 8
    [FieldOffset(0)]
    public ADDRESS_KIND dwKind;
    [FieldOffset(Offset)]
    public NATIVE_ADDRESS addrNative;
    [FieldOffset(Offset)]
    public UNMANAGED_ADDRESS_THIS_RELATIVE addrThisRel;
    [FieldOffset(Offset)]
    public UNMANAGED_ADDRESS_PHYSICAL addrUPhysical;
    [FieldOffset(Offset)]
    public METADATA_ADDRESS_METHOD addrMethod;
    [FieldOffset(Offset)]
    public METADATA_ADDRESS_FIELD addrField;
    [FieldOffset(Offset)]
    public METADATA_ADDRESS_LOCAL addrLocal;
    [FieldOffset(Offset)]
    public METADATA_ADDRESS_PARAM addrParam;
    [FieldOffset(Offset)]
    public METADATA_ADDRESS_ARRAYELEM addrArrayElem;
    [FieldOffset(Offset)]
    public METADATA_ADDRESS_RETVAL addrRetVal;
  }

  [StructLayout(LayoutKind.Explicit)]
  public  struct DEBUG_ADDRESS 
  {
    [FieldOffset(0)]
    public System.UInt32       ulAppDomainID;
    [FieldOffset(4)]
    public Guid                guidModule;
    [FieldOffset(20)]
    public _mdToken            tokClass;
    [FieldOffset(24)]
    public DEBUG_ADDRESS_UNION addr;
  }

  [
  ComImport(),
  Guid("c2e34ebb-8b9d-11d2-9014-00c04fa38338"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]

  public  interface IDebugAddress 
  {
    void GetAddress(
      out DEBUG_ADDRESS pAddress
      );
  }

  public enum CONSTRUCTOR_ENUM : uint 
  {
    crAll,
    crNonStatic,
    crStatic
  }

  [
  ComImport(),
  Guid("c2e34eb5-8b9d-11d2-9014-00c04fa38338"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]

  public  interface IDebugClassField /*: IDebugContainerField*/ 
  {
    //
    //IDebugField
    //
        
    // Get user-displayable information
    void GetInfo(
      FIELD_INFO_FIELDS dwFields, 
      out FIELD_INFO pFieldInfo);

    // Get the kind of this field
    void GetKind(
      out FIELD_KIND pdwKind);

    // Get a field that describes the type of this field
    void GetType(
      out IDebugField ppType);

    // Get this field's container
    void GetContainer(
      out IDebugContainerField ppContainerField);

    // Get the field's address
    void GetAddress(
      out IDebugAddress ppAddress);

    // Get the size of the field in bytes
    void GetSize(
      out uint pdwSize);

    // Get extended info about this field (the caller must free the buffer via CoTaskMemFree)
    // Return S_FALSE when the funtion does not return anything useful.
    [PreserveSig]
    HRESULT GetExtendedInfo(
      ref Guid guidExtendedInfo, 
      // This is a "out BYTE[]" (or "BYTE **"), but the caller must free the returned buffer 
      // using CoTaskMemFree.. So we expose it as "IntPtr" to be able to use the Marshal methods
      out IntPtr prgBuffer,	
      out uint pdwLen);

    // S_OK if same type or symbols, S_FALSE if not
    [PreserveSig]
    HRESULT Equal(
      IDebugField pField);

    void GetTypeInfo(
      out TYPE_INFO pTypeInfo);
        
    //
    //IDebugContainerField
    //

    // Get all the child fields of this field
    void EnumFields(
      FIELD_KIND dwKindFilter, 
      FIELD_MODIFIERS dwModifiersFilter, 
      /*REVIEWED: [in, ptr] */
      string pszNameFilter, 
      NAME_MATCH nameMatch, 
      out IEnumDebugFields ppEnum);
        
    //
    //IDebugClassField
    //

    void EnumBaseClasses(
      out IEnumDebugFields ppEnum);

    // S_OK if Interface is defined, otherwise S_FALSE
    [PreserveSig]
    HRESULT DoesInterfaceExist(
      string pszInterfaceName);

    // S_FALSE if no Nested Classes	
    [PreserveSig]
    HRESULT EnumNestedClasses(
      out IEnumDebugFields ppEnum);

    // S_FALSE if no Enclosing class
    [PreserveSig]
    HRESULT GetEnclosingClass(
      out IDebugClassField ppClassField);

    // Provide IDebugClassFields for each interface implemented
    void EnumInterfacesImplemented(
      out IEnumDebugFields ppEnum);

    // Provide IDebugMethodFields for constructors
    void EnumConstructors(
      CONSTRUCTOR_ENUM cMatch, 
      out IEnumDebugFields ppEnum);

    //Provide name of default indexer
    void GetDefaultIndexer(
      [Out, MarshalAs(UnmanagedType.BStr)]
      out string pbstrIndexer);

    // S_FALSE if no Nested Enums	
    [PreserveSig]
    HRESULT EnumNestedEnums(
      out IEnumDebugFields ppEnum);
  }

  public  enum NAME_MATCH 
  {
    nmNone,
    nmCaseSensitive,
    nmCaseInsensitive
  };


  [
  ComImport(),
  Guid("c2e34eb2-8b9d-11d2-9014-00c04fa38338"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]

  public  interface IDebugContainerField 
  { /*: IDebugField*/
    //
    //IDebugField
    //
        
    // Get user-displayable information
    void GetInfo(
      FIELD_INFO_FIELDS dwFields, 
      out FIELD_INFO pFieldInfo);

    // Get the kind of this field
    void GetKind(
      out FIELD_KIND pdwKind);

    // Get a field that describes the type of this field
    void GetType(
      out IDebugField ppType);

    // Get this field's container
    void GetContainer(
      out IDebugContainerField ppContainerField);

    // Get the field's address
    void GetAddress(
      out IDebugAddress ppAddress);

    // Get the size of the field in bytes
    void GetSize(
      out uint pdwSize);

    // Get extended info about this field (the caller must free the buffer via CoTaskMemFree)
    // Return S_FALSE when the funtion does not return anything useful.
    [PreserveSig]
    HRESULT GetExtendedInfo(
      ref Guid guidExtendedInfo, 
      // This is a "out BYTE[]" (or "BYTE **"), but the caller must free the returned buffer 
      // using CoTaskMemFree.. So we expose it as "IntPtr" to be able to use the Marshal methods
      out IntPtr prgBuffer,	
      out uint pdwLen);

    // S_OK if same type or symbols, S_FALSE if not
    [PreserveSig]
    HRESULT Equal(
      IDebugField pField);

    void GetTypeInfo(
      out TYPE_INFO pTypeInfo);

    // IDebugConatainerField

    // Get all the child fields of this field
    HRESULT EnumFields(
      FIELD_KIND						dwKindFilter,
      FIELD_MODIFIERS					dwModifiersFilter,
      [MarshalAs(UnmanagedType.LPWStr)]
      string							pszNameFilter,
      NAME_MATCH						nameMatch,
      out IEnumDebugFields			ppEnum
      );
  }

  [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),Guid("83919262-ACD6-11d2-9028-00C04FA302A1")]
  public  interface IDebugEngineSymbolProviderServices 
  {
    void EnumCodeContexts(
      [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.Interface, SizeParamIndex=1)]
      IDebugAddress []rgpAddresses, 
      uint celtAddresses, 
      out IEnumDebugCodeContexts2 ppEnum);
  }


  [ Flags ]
  public  enum FIELD_MODIFIERS : uint 
  {
    FIELD_MOD_NONE				= 0x00000000,

    // Access modifiers
    FIELD_MOD_ACCESS_NONE		= 0x00000001,
    FIELD_MOD_ACCESS_PUBLIC		= 0x00000002,
    FIELD_MOD_ACCESS_PROTECTED	= 0x00000004,
    FIELD_MOD_ACCESS_PRIVATE	= 0x00000008,

    // Modifiers
    FIELD_MOD_NOMODIFIERS		= 0x00000010,
    FIELD_MOD_STATIC			= 0x00000020,
    FIELD_MOD_CONSTANT			= 0x00000040,
    FIELD_MOD_TRANSIENT			= 0x00000080,
    FIELD_MOD_VOLATILE			= 0x00000100,
    FIELD_MOD_ABSTRACT			= 0x00000200,
    FIELD_MOD_NATIVE			= 0x00000400,
    FIELD_MOD_SYNCHRONIZED		= 0x00000800,
    FIELD_MOD_VIRTUAL			= 0x00001000,
    FIELD_MOD_INTERFACE			= 0x00002000,
    FIELD_MOD_FINAL				= 0x00004000,
    FIELD_MOD_SENTINEL			= 0x00008000,
    FIELD_MOD_INNERCLASS		= 0x00010000,
    FIELD_MOD_OPTIONAL			= 0x00020000,

    // FIELD_MOD_BYREF is specifically for Arguments to methods
    FIELD_MOD_BYREF				= 0x00040000,

    // This mod is emitted when the field must be hidden from
    // the user or presented in a different context.
    // VB static locals are an example.  
    FIELD_MOD_HIDDEN			= 0x00080000,

    FIELD_MOD_MARSHALASOBJECT   = 0x00100000,
		
    // This mod indicates a Property field is writeonly
    // It is not included in FIELD_MOD_ALL as the only
    // use of these fields is for Func-eval.
    // A user must explicitly ask for FIELD_MOD_WRITEONLY fields
		
    FIELD_MOD_WRITEONLY			= 0x80000000,
		
    FIELD_MOD_ACCESS_MASK		= 0x000000ff,
    FIELD_MOD_MASK				= 0xffffff00,

    FIELD_MOD_ALL				= 0x7fffffff,


    // Examples:
    // - private static final: FIELD_MOD_ACCESS_PRIVATE | FIELD_MOD_STATIC | FIELD_MOD_CONSTANT
    // - public virtual: FIELD_MOD_ACCESS_PUBLIC | FIELD_MOD_VIRTUAL
    // - protected: FIELD_MOD_ACCESS_PROTECTED | FIELD_MOD_NOMODIFIERS
  };

  [ Flags ]
  public  enum FIELD_KIND : uint 
  {
    FIELD_KIND_NONE				= 0x00000000,

    // Type of the field
    FIELD_KIND_TYPE				= 0x00000001,
    FIELD_KIND_SYMBOL			= 0x00000002,

    // Storage type of the field
    FIELD_TYPE_PRIMITIVE		= 0x00000010,
    FIELD_TYPE_STRUCT			= 0x00000020,
    FIELD_TYPE_CLASS			= 0x00000040,
    FIELD_TYPE_INTERFACE		= 0x00000080,
    FIELD_TYPE_UNION			= 0x00000100,
    FIELD_TYPE_ARRAY			= 0x00000200,
    FIELD_TYPE_METHOD			= 0x00000400,
    FIELD_TYPE_BLOCK			= 0x00000800,
    FIELD_TYPE_POINTER			= 0x00001000,
    FIELD_TYPE_ENUM				= 0x00002000,
    FIELD_TYPE_LABEL			= 0x00004000,
    FIELD_TYPE_TYPEDEF			= 0x00008000,
    FIELD_TYPE_BITFIELD			= 0x00010000,
    FIELD_TYPE_NAMESPACE		= 0x00020000,
    FIELD_TYPE_MODULE			= 0x00040000,
    FIELD_TYPE_DYNAMIC			= 0x00080000,
    FIELD_TYPE_PROP				= 0x00100000,
    FIELD_TYPE_INNERCLASS		= 0x00200000,
    FIELD_TYPE_REFERENCE		= 0x00400000,
    FIELD_TYPE_EXTENDED			= 0x00800000,  // Reserved for future use

    // Specific info about symbols
    FIELD_SYM_MEMBER			= 0x01000000,
    FIELD_SYM_LOCAL				= 0x02000000,
    FIELD_SYM_PARAM				= 0x04000000,
    FIELD_SYM_THIS				= 0x08000000,
    FIELD_SYM_GLOBAL			= 0x10000000,
    FIELD_SYM_PROP_GETTER		= 0x20000000,
    FIELD_SYM_PROP_SETTER		= 0x40000000,
    FIELD_SYM_EXTENED			= 0x80000000, // Reserved for future use

    FIELD_KIND_MASK				= 0x0000000f,
    FIELD_TYPE_MASK				= 0x00fffff0,
    FIELD_SYM_MASK				= 0xff000000,

    FIELD_KIND_ALL				= 0xffffffff,

    // Examples:
    // - global namespace: FIELD_KIND_GLOBAL | FIELD_KIND_NAMESPACE
    // - this pointer: FIELD_KIND_THIS | FIELD_KIND_POINTER
    // - this object: FIELD_KIND_THIS | FIELD_KIND_DATA_OBJECT
    // - property getter: FIELD_PROP_GETTER | FIELD_KIND_METHOD
  };

  [ Flags ]
  public  enum FIELD_INFO_FIELDS : uint 
  {
    FIF_FULLNAME			= 0x0001,
    FIF_NAME				= 0x0002,
    FIF_TYPE				= 0x0004,
    FIF_MODIFIERS			= 0x0008,
    FIF_ALL					= 0xffffffff,
  };

  [ Flags ]
  public  enum dwTYPE_KIND : uint 
  {
    TYPE_KIND_METADATA		= 0x0001,
    TYPE_KIND_PDB			= 0x0002,
  };

  [ StructLayout( LayoutKind.Sequential ) ]
  public  struct FIELD_INFO 
  {
    public FIELD_INFO_FIELDS  dwFields;
    [MarshalAs(UnmanagedType.BStr)]
    public string             bstrFullName;
    [MarshalAs(UnmanagedType.BStr)]
    public string             bstrName;
    [MarshalAs(UnmanagedType.BStr)]
    public string             bstrType;
    public FIELD_MODIFIERS    dwModifiers;
  };

  public  struct METADATA_TYPE 
  {
    public ulong               ulAppDomainID;
    public Guid                guidModule;
    public int                 tokClass;
  };

  public  struct PDB_TYPE 
  {
    public ulong               ulAppDomainID;
    public Guid                guidModule;
    public uint				symid;
  };

  [ StructLayout( LayoutKind.Explicit )]
  public  struct TYPE_INFO 
  {
    [ FieldOffset( 0 )]
    public dwTYPE_KIND           dwKind;
    [ FieldOffset( 4 )]
    public METADATA_TYPE         typeMeta;
    [ FieldOffset( 4 )]
    public PDB_TYPE              typePdb;
    [ FieldOffset( 4 )]
    public uint                  unused;
  };



  [
  ComImport(),
  Guid("c2e34eb1-8b9d-11d2-9014-00c04fa38338"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]

  public  interface IDebugField 
  {
    // Get user-displayable information
    void GetInfo(
      FIELD_INFO_FIELDS dwFields, 
      out FIELD_INFO pFieldInfo);

    // Get the kind of this field
    void GetKind(
      out FIELD_KIND pdwKind);

    // Get a field that describes the type of this field
    void GetType(
      out IDebugField ppType);

    // Get this field's container
    void GetContainer(
      out IDebugContainerField ppContainerField);

    // Get the field's address
    void GetAddress(
      out IDebugAddress ppAddress);

    // Get the size of the field in bytes
    void GetSize(
      out uint pdwSize);

    // Get extended info about this field (the caller must free the buffer via CoTaskMemFree)
    // Return S_FALSE when the funtion does not return anything useful.
    [PreserveSig]
    HRESULT GetExtendedInfo(
      ref Guid guidExtendedInfo, 
      // This is a "out BYTE[]" (or "BYTE **"), but the caller must free the returned buffer 
      // using CoTaskMemFree.. So we expose it as "IntPtr" to be able to use the Marshal methods
      out IntPtr prgBuffer,	
      out uint pdwLen);

    // S_OK if same type or symbols, S_FALSE if not
    [PreserveSig]
    HRESULT Equal(
      IDebugField pField);

    void GetTypeInfo(
      out TYPE_INFO pTypeInfo);
  }

  [
  ComImport(),
  Guid("c2e34eb4-8b9d-11d2-9014-00c04fa38338"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]

  public  interface IDebugMethodField 
  { /*: IDebugContainerField*/

    //
    //IDebugField
    //
        
    // Get user-displayable information
    void GetInfo(
      FIELD_INFO_FIELDS dwFields, 
      out FIELD_INFO pFieldInfo);

    // Get the kind of this field
    void GetKind(
      out FIELD_KIND pdwKind);

    // Get a field that describes the type of this field
    void GetType(
      out IDebugField ppType);

    // Get this field's container
    void GetContainer(
      out IDebugContainerField ppContainerField);

    // Get the field's address
    void GetAddress(
      out IDebugAddress ppAddress);

    // Get the size of the field in bytes
    void GetSize(
      out uint pdwSize);

    // Get extended info about this field (the caller must free the buffer via CoTaskMemFree)
    // Return S_FALSE when the funtion does not return anything useful.
    [PreserveSig]
    HRESULT GetExtendedInfo(
      ref Guid guidExtendedInfo, 
      // This is a "out BYTE[]" (or "BYTE **"), but the caller must free the returned buffer 
      // using CoTaskMemFree.. So we expose it as "IntPtr" to be able to use the Marshal methods
      out IntPtr prgBuffer,	
      out uint pdwLen);

    // S_OK if same type or symbols, S_FALSE if not
    [PreserveSig]
    HRESULT Equal(
      IDebugField pField);

    void GetTypeInfo(
      out TYPE_INFO pTypeInfo);

    // IDebugConatainerField

    // Get all the child fields of this field
    HRESULT EnumFields(
      FIELD_KIND						dwKindFilter,
      FIELD_MODIFIERS					dwModifiersFilter,
      [MarshalAs(UnmanagedType.LPWStr)]
      string							pszNameFilter,
      NAME_MATCH						nameMatch,
      out IEnumDebugFields			ppEnum
      );

    //
    //IDebugMethodField
    //

    void EnumParameters(
      out IEnumDebugFields ppParams);

    void GetThis(
      out IDebugClassField ppClass);

    void EnumAllLocals(
      IDebugAddress pAddress, 
      out IEnumDebugFields ppLocals);

    void EnumLocals(
      IDebugAddress pAddress, 
      out IEnumDebugFields ppLocals);

    // S_OK if defined on this field else S_FALSE
    [PreserveSig]
    HRESULT IsCustomAttributeDefined(
      /*REVIEWED: [in, ptr] */
      string pszCustomAttributeName);

    void EnumStaticLocals(
      out IEnumDebugFields ppLocals);

    //
    // This returns a class that represents the 
    // global methods and fields from the metadata of
    // a specific module.  (The one in which this method is defined.)
    //
    void GetGlobalContainer(
      out IDebugClassField ppClass);

    // Enum Arguments provides the types of each argument
    // required to call the function.
    void EnumArguments(
      out IEnumDebugFields ppParams);
  }

  [
  ComImport(),
  Guid("c2e34eae-8b9d-11d2-9014-00c04fa38338"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public  interface IDebugSymbolProvider 
  {
    [ PreserveSig ]
    HRESULT Initialize(
      IDebugEngineSymbolProviderServices pServices);

    [ PreserveSig ]
    HRESULT Uninitialize();

    // REVIEW: is this here or on an EE private interface?
    // DOUGROS: I think it should be here - the SH should be defined
    // without a dependence on and EE.

    [ PreserveSig ]
    HRESULT GetContainerField(
      IDebugAddress pAddress, 
      out IDebugContainerField ppContainerField);

    // REVIEW: is this here or on an EE private interface?
    [ PreserveSig ]
    HRESULT GetField(
      IDebugAddress pAddress, 
      IDebugAddress pAddressCur, 
      out IDebugField ppField);

    [ PreserveSig ]
    HRESULT GetAddressesFromPosition(
      IDebugDocumentPosition2 pDocPos, 
      [MarshalAs(UnmanagedType.Bool)]
      bool fStatmentOnly, 
      out IEnumDebugAddresses ppEnumBegAddresses, 
      out IEnumDebugAddresses ppEnumEndAddresses);

    [ PreserveSig ]
    HRESULT GetAddressesFromContext(
      IDebugDocumentContext2 pDocContext, 
      [MarshalAs(UnmanagedType.Bool)]
      bool fStatmentOnly, 
      out IEnumDebugAddresses ppEnumBegAddresses, 
      out IEnumDebugAddresses ppEnumEndAddresses);

    [PreserveSig]
    HRESULT GetContextFromAddress(
      IDebugAddress pAddress, 
      out IDebugDocumentContext2 ppDocContext);

    [ PreserveSig ]
    HRESULT GetLanguage(
      IDebugAddress pAddress, 
      out Guid pguidLanguage, 
      out Guid pguidLanguageVendor);

    [ PreserveSig ]
    HRESULT GetGlobalContainer(
      out IDebugContainerField pField);

    //
    // Given a fully qualified method name return its field.
    // Overloaded functions should return multiple fields
    //
    [ PreserveSig ]
    HRESULT GetMethodFieldsByName(
      /*REVIEWED: [in, ptr] */
      string pszFullName, 
      NAME_MATCH nameMatch, 
      out IEnumDebugFields ppEnum);

    [ PreserveSig ]
    HRESULT GetClassTypeByName(
      /*REVIEWED: [in, ptr] */
      string pszClassName, 
      NAME_MATCH nameMatch, 
      out IDebugClassField ppField);

    [ PreserveSig ]
    HRESULT GetNamespacesUsedAtAddress(
      IDebugAddress pAddress, 
      out IEnumDebugFields ppEnum);

    // A more generic version of GetClassTypeByName
    [ PreserveSig ]
    HRESULT GetTypeByName(
      /*REVIEWED: [in, ptr] */
      string pszClassName, 
      NAME_MATCH nameMatch, 
      out IDebugField ppField);

    [ PreserveSig ]
    HRESULT GetNextAddress(
      IDebugAddress pAddress, 
      [MarshalAs(UnmanagedType.Bool)]
      bool fStatmentOnly, 
      out IDebugAddress ppAddress);
  }

  [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),Guid("c2e34ebd-8b9d-11d2-9014-00c04fa38338")]
  public  interface IEnumDebugAddresses 
  {

    void Next(
      ulong celt, 
      [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.Interface, SizeParamIndex=0)]
      IDebugAddress []rgelt, 
      out uint pceltFetched);

    void Skip(
      ulong celt);

    HRESULT Reset();

    void Clone(
      out IEnumDebugAddresses ppEnum);

    void GetCount(
      out ulong pcelt);
  }

  [
  ComImport(),
  Guid("c2e34eb6-8b9d-11d2-9014-00c04fa38338"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public  interface IDebugPropertyField 
  {/* : IDebugContainerField */
    //
    //IDebugField
    //
        
    // Get user-displayable information
    void GetInfo(
      FIELD_INFO_FIELDS dwFields, 
      out FIELD_INFO pFieldInfo);

    // Get the kind of this field
    void GetKind(
      out FIELD_KIND pdwKind);

    // Get a field that describes the type of this field
    void GetType(
      out IDebugField ppType);

    // Get this field's container
    void GetContainer(
      out IDebugContainerField ppContainerField);

    // Get the field's address
    void GetAddress(
      out IDebugAddress ppAddress);

    // Get the size of the field in bytes
    void GetSize(
      out uint pdwSize);

    // Get extended info about this field (the caller must free the buffer via CoTaskMemFree)
    // Return S_FALSE when the funtion does not return anything useful.
    [PreserveSig]
    HRESULT GetExtendedInfo(
      ref Guid guidExtendedInfo, 
      // This is a "out BYTE[]" (or "BYTE **"), but the caller must free the returned buffer 
      // using CoTaskMemFree.. So we expose it as "IntPtr" to be able to use the Marshal methods
      out IntPtr prgBuffer,	
      out uint pdwLen);

    // S_OK if same type or symbols, S_FALSE if not
    [PreserveSig]
    HRESULT Equal(
      IDebugField pField);

    void GetTypeInfo(
      out TYPE_INFO pTypeInfo);
        
    //
    //IDebugContainerField
    //

    // Get all the child fields of this field
    void EnumFields(
      FIELD_KIND dwKindFilter, 
      FIELD_MODIFIERS dwModifiersFilter, 
      /*REVIEWED: [in, ptr] */
      string pszNameFilter, 
      NAME_MATCH nameMatch, 
      out IEnumDebugFields ppEnum);
        
    //
    //IDebugPropertyField
    //

    void GetPropertyGetter(
      out IDebugMethodField ppField);

    void GetPropertySetter(
      out IDebugMethodField ppField);
  }

  [
  ComImport(),
  Guid("c2e34eb7-8b9d-11d2-9014-00c04fa38338"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public  interface IDebugArrayField 
  {/* : IDebugContainerField */
    //
    //IDebugField
    //
        
    // Get user-displayable information
    void GetInfo(
      FIELD_INFO_FIELDS dwFields, 
      out FIELD_INFO pFieldInfo);

    // Get the kind of this field
    void GetKind(
      out FIELD_KIND pdwKind);

    // Get a field that describes the type of this field
    void GetType(
      out IDebugField ppType);

    // Get this field's container
    void GetContainer(
      out IDebugContainerField ppContainerField);

    // Get the field's address
    void GetAddress(
      out IDebugAddress ppAddress);

    // Get the size of the field in bytes
    void GetSize(
      out uint pdwSize);

    // Get extended info about this field (the caller must free the buffer via CoTaskMemFree)
    // Return S_FALSE when the funtion does not return anything useful.
    [PreserveSig]
    HRESULT GetExtendedInfo(
      ref Guid guidExtendedInfo, 
      // This is a "out BYTE[]" (or "BYTE **"), but the caller must free the returned buffer 
      // using CoTaskMemFree.. So we expose it as "IntPtr" to be able to use the Marshal methods
      out IntPtr prgBuffer,	
      out uint pdwLen);

    // S_OK if same type or symbols, S_FALSE if not
    [PreserveSig]
    HRESULT Equal(
      IDebugField pField);

    void GetTypeInfo(
      out TYPE_INFO pTypeInfo);
        
    //
    //IDebugContainerField
    //

    // Get all the child fields of this field
    void EnumFields(
      FIELD_KIND dwKindFilter, 
      FIELD_MODIFIERS dwModifiersFilter, 
      /*REVIEWED: [in, ptr] */
      string pszNameFilter, 
      NAME_MATCH nameMatch, 
      out IEnumDebugFields ppEnum);
        
    //
    //IDebugArrayField
    //

    void GetNumberOfElements(
      out uint pdwNumElements);

    void GetElementType(
      out IDebugField ppType);

    void GetRank(
      out uint pdwRank);
  }

  [
  ComImport(),
  Guid("c2e34eb9-8b9d-11d2-9014-00c04fa38338"), 
  InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)
  ]
  public  interface IDebugEnumField 
  {/* : IDebugContainerField */
    //
    //IDebugField
    //
        
    // Get user-displayable information
    void GetInfo(
      FIELD_INFO_FIELDS dwFields, 
      out FIELD_INFO pFieldInfo);

    // Get the kind of this field
    void GetKind(
      out FIELD_KIND pdwKind);

    // Get a field that describes the type of this field
    void GetType(
      out IDebugField ppType);

    // Get this field's container
    void GetContainer(
      out IDebugContainerField ppContainerField);

    // Get the field's address
    void GetAddress(
      out IDebugAddress ppAddress);

    // Get the size of the field in bytes
    void GetSize(
      out uint pdwSize);

    // Get extended info about this field (the caller must free the buffer via CoTaskMemFree)
    // Return S_FALSE when the funtion does not return anything useful.
    [PreserveSig]
    HRESULT GetExtendedInfo(
      ref Guid guidExtendedInfo, 
      // This is a "out BYTE[]" (or "BYTE **"), but the caller must free the returned buffer 
      // using CoTaskMemFree.. So we expose it as "IntPtr" to be able to use the Marshal methods
      out IntPtr prgBuffer,	
      out uint pdwLen);

    // S_OK if same type or symbols, S_FALSE if not
    [PreserveSig]
    HRESULT Equal(
      IDebugField pField);

    void GetTypeInfo(
      out TYPE_INFO pTypeInfo);
        
    //
    //IDebugContainerField
    //

    // Get all the child fields of this field
    void EnumFields(
      FIELD_KIND dwKindFilter, 
      FIELD_MODIFIERS dwModifiersFilter, 
      /*REVIEWED: [in, ptr] */
      string pszNameFilter, 
      NAME_MATCH nameMatch, 
      out IEnumDebugFields ppEnum);
        
    //
    //IDebugEnumField
    //

    void GetUnderlyingSymbol(
      out IDebugField ppField);

    void GetStringFromValue(
      ulong value, 
      [Out, MarshalAs(UnmanagedType.BStr)]
      out string pbstrValue);

    void GetValueFromString(
      /*REVIEWED: [in, ptr] */
      string pszValue, 
      out ulong pvalue);

    void GetValueFromStringCaseInsensitive(
      /*REVIEWED: [in, ptr] */
      String pszValue, 
      out ulong pvalue);

  }
  
}