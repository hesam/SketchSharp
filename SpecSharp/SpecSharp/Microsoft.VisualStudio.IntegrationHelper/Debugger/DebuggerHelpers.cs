//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
#if CCINamespace
using Microsoft.Cci;
#else
using System.Compiler;
#endif
using System;
using System.Runtime.InteropServices;
using HRESULT = System.Int32;
namespace Microsoft.VisualStudio.IntegrationHelper{
  public enum HResult {
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
    S_FALSE =   0x00000001,
  }
  public class NativeHelpers {
    public static bool Succeeded(int hr) {
      return(hr >= 0);
    }
    public static bool Failed(int hr) {
      return(hr < 0);
    }
    public static void RaiseComError(HResult hr) {
      throw new System.Runtime.InteropServices.COMException("",(int)hr);
    }
    public static void RaiseComError(HResult hr, string message) {
      throw new System.Runtime.InteropServices.COMException(message,(int)hr);
    }
  }
  public interface IDebugContext{
    IDebugAddress Address{ get; }
    IDebugBinder Binder{ get; }
    IDebugSymbolProvider SymbolProvider{ get; }
    EvaluationFlags flags{ get;}
    long radix{ get;}
    uint timeout{ get; }
    IDebugSymbol GetContainer();
    IDebugType GetType(string fullName);
    IDebugValue CreatePrimitiveValue(object val);
    IDebugValue CreateObject(IDebugMethodSymbol constructor, IDebugValue[] args);
    IDebugValue CreateString(string str);
    IDebugValue CreateObjectNoConstructor(IDebugType type);
  }
  public abstract class BaseExpressionEvaluator : IDebugExpressionEvaluator {
    public CommonExpressionEvaluator cciEvaluator;
    
    public BaseExpressionEvaluator() {
      this.cciEvaluator = new CommonExpressionEvaluator();
    }
    //IDebugExpressionEvaluator
    public  HRESULT Parse( 
      [In,MarshalAs(UnmanagedType.LPWStr)]
      string                    pszExpression,
      PARSEFLAGS                  flags,
      uint                        radix,
      out string                  pbstrErrorMessages,
      out uint                    perrorCount,
      out IDebugParsedExpression  ppparsedExpression
      ) 
    {
      
      HRESULT hr = (HRESULT)HResult.S_OK;
      perrorCount = 0;
      pbstrErrorMessages = null;
      ppparsedExpression = null;
      ErrorNodeList errors =  new ErrorNodeList();
      Module symbolTable = new Module();
      Document doc = this.cciEvaluator.ExprCompiler.CreateDocument(null, 1, pszExpression);
      IParser exprParser = this.cciEvaluator.ExprCompiler.CreateParser(doc.Name, doc.LineNumber, doc.Text, symbolTable, errors, null);
      Expression parsedExpression = exprParser.ParseExpression();

      perrorCount = (uint)errors.Count;
      if (perrorCount > 0)
        pbstrErrorMessages = errors[0].GetMessage();
      else
        ppparsedExpression = new BaseParsedExpression(pszExpression, parsedExpression, this);

      return hr;
    }

    public  HRESULT GetMethodProperty( 
      IDebugSymbolProvider    pSymbolProvider,
      IDebugAddress           pAddress,
      IDebugBinder			      pBinder,
      bool	                  includeHiddenLocals,
      out IDebugProperty2     ppproperty
      ) 
    {

      HRESULT hr = (int)HResult.S_OK;
      ppproperty = null;
      IDebugContext context = new CDebugContext(pSymbolProvider, pAddress, pBinder);
      if (context != null)
      {
        IDebugProperty prop = null;
        prop = this.cciEvaluator.GetCurrentMethodProperty((IDebugMethodSymbol)context.GetContainer());

        if (null != prop)
        {
          CDebugProperty debugProp = new CDebugProperty(prop);
          if (null != debugProp)
          {
            ppproperty = debugProp as IDebugProperty2;
          }
          else
            hr = (HRESULT)HResult.E_OUTOFMEMORY;
        }
      } 
      else
        hr = (HRESULT)HResult.E_OUTOFMEMORY;

      return hr;
    }

    public  HRESULT GetMethodLocationProperty( 
      string                  pszFullyQualifiedMethodPlusOffset,
      IDebugSymbolProvider	pprovider,
      IDebugAddress			paddress,
      IDebugBinder			pbinder,
      out IDebugProperty2 	ppproperty
      ) 
    {
      ppproperty = null;
      return (HRESULT)HResult.E_NOTIMPL;
    }

    public  HRESULT SetLocale( ushort wLangID) {
      return (HRESULT)HResult.E_NOTIMPL;
    }

    public  HRESULT SetRegistryRoot( string in_szRegistryRoot ) {
      return (HRESULT)HResult.E_NOTIMPL;
    }
  }
  public class BaseParsedExpression : IDebugParsedExpression {
    public ParsedExpression cciExpr;
    private DebugEnvironment debugContext;
    public BaseParsedExpression(String expr, Expression parsedExpression, BaseExpressionEvaluator ee){
      this.cciExpr = new ParsedExpression(expr, parsedExpression, ee.cciEvaluator);
      this.debugContext = null;
    }
    // IDebugParsedExpression
    public void EvaluateSync(uint dwEvalFlags, uint dwTimeout, IDebugSymbolProvider pSymbolProvider,
      IDebugAddress pAddress, IDebugBinder pBinder, String bstrResultType, out IDebugProperty2 ppResult) {

      ppResult = null;
      IDebugContext context = new CDebugContext(pSymbolProvider, pAddress, pBinder);
      if (context != null){
        IDebugProperty prop = null;
        prop = this.EvaluateExpression(dwEvalFlags, dwTimeout, context, bstrResultType);

        if (prop != null){
          CDebugProperty debugProp = new CDebugProperty(prop);
          if (null != debugProp)
            ppResult = debugProp as IDebugProperty2;
        }
      }
    }
    
    private IDebugProperty EvaluateExpression(uint evalFlags, uint timeout, IDebugContext context, String resultType){
      if (this.debugContext == null) this.debugContext = new DebugEnvironment();
      this.debugContext.context = context;  
      BlockScope scope = new DebugBlockScope(this.debugContext);
      ErrorNodeList errors = new ErrorNodeList();
      if (this.cciExpr.compiledExpression == null){
        this.cciExpr.compiledExpression = (Expression)this.cciExpr.EE.ExprCompiler.CompileParseTree(this.cciExpr.ParsedExpr, scope, new Module(), errors);
        if (errors.Count > 0)
          this.cciExpr.compiledExpression = null;
      }
      if (this.cciExpr.compiledExpression != null){
        StackFrame currentFrame = new StackFrame();
        IDebugMethodSymbol methodSym = this.debugContext.context.GetContainer() as CDebugMethodSymbol;
        if (methodSym != null){
          IDebugFieldSymbol thisSymbol = methodSym.GetThis();
          if (thisSymbol != null)
            currentFrame.thisObject = new Literal(thisSymbol.GetValue(null), ((MethodScope ) scope.BaseClass).ThisType);
          else
            currentFrame.thisObject = null;
          currentFrame.parameters[0] = currentFrame.thisObject;
          IEnumSymbol locals = methodSym.GetLocals();
          if (locals != null){
            for (int i=0; ; i++){
              if (locals.Current == null) break;
              Field localField = new DebugFieldNode(this.debugContext, locals.Current, new Identifier(locals.Current.Name), null, null, i);
              currentFrame.locals[i] = localField.GetValue(null);
              locals.MoveNext();
            }
          }
          IEnumSymbol param = methodSym.GetParameters();
          if (param != null){
            for (int i=1; ; i++){
              if (param.Current == null) break;
              Field paramField = new DebugFieldNode(this.debugContext, param.Current, new Identifier(param.Current.Name), null, null, i);
              currentFrame.parameters[i] = paramField.GetValue(null);
              param.MoveNext();
            }
          }
        }
        if (this.cciExpr.EE.ExprEvaluator == null)
          this.cciExpr.EE.ExprEvaluator = new Evaluator();
        this.cciExpr.EE.ExprEvaluator.stackFrame = currentFrame;
        Literal resultExpr = this.cciExpr.EE.ExprEvaluator.VisitExpression(this.cciExpr.compiledExpression) as Literal;
        if (resultExpr != null){
          if (resultExpr.Value is IDebugValue && resultExpr.Type is IDebugInfo) //already wrapped for use by debugger
            return this.cciExpr.EE.MakeProperty(this.cciExpr.Expr, ((IDebugInfo)resultExpr.Type).GetDebugType, (IDebugValue)resultExpr.Value, null);
          else if(resultExpr.Value is IDebugValue)
            return this.cciExpr.EE.MakeProperty(this.cciExpr.Expr, ((IDebugValue)resultExpr.Value).RuntimeType(), (IDebugValue)resultExpr.Value, null);
          if (resultExpr.Value != null)
            return new ExpressionEvalProperty(this.cciExpr.Expr, resultExpr.Type.FullName, resultExpr.Value.ToString(), resultExpr, this.cciExpr.EE);
        }
        else
          return new ExpressionEvalProperty(this.cciExpr.Expr, String.Empty, "Error Evaluating Expression.", null, this.cciExpr.EE);
      }
      else if (errors.Count > 0){
        return new ExpressionEvalProperty(this.cciExpr.Expr, String.Empty, errors[0].GetMessage(), null, this.cciExpr.EE);
      }
      return new ExpressionEvalProperty(this.cciExpr.Expr, String.Empty, "Unknown Compiler Error.", null, this.cciExpr.EE);
    }
  }
  public class CDebugContext : IDebugContext {

    private IDebugSymbolProvider m_SymbolProvider;
    private IDebugAddress m_Address;
    private IDebugBinder m_Binder;
    private uint m_EvalFlags;
    private uint m_Timeout;
    private long m_Radix; // TODO : figure out how to get this in all cases

    public  CDebugContext(IDebugSymbolProvider symbolProvider, IDebugAddress address, IDebugBinder binder) {
      
      this.m_SymbolProvider = symbolProvider;
      this.m_Address = address;
      this.m_Binder = binder;
      this.m_Radix = 0;
    }

    public  CDebugContext(IDebugSymbolProvider symbolProvider, IDebugAddress address, IDebugBinder binder, uint evalFlags, uint timeout) : this(symbolProvider, address, binder) {

      this.m_EvalFlags = evalFlags;
      this.m_Timeout = timeout;
      //this.radix = radix;
    }

    public  IDebugAddress Address {
      get {
        return m_Address;
      }
    }

    public  IDebugBinder Binder {
      get {
        return m_Binder;
      }
    }

    public  IDebugSymbolProvider SymbolProvider {
      get {
        return m_SymbolProvider;
      }
    }

    public  EvaluationFlags flags { 
      get {

        EvaluationFlags pRetVal = 0;
        if (0 != (this.m_EvalFlags & (uint) EVALFLAGS.EVAL_NOSIDEEFFECTS))
          pRetVal |= EvaluationFlags.NoSideEffects;
        if (0 != (this.m_EvalFlags & (uint) EVALFLAGS.EVAL_FUNCTION_AS_ADDRESS))
          pRetVal |= EvaluationFlags.FunctionAsAddress;
        if (0 != (this.m_EvalFlags & (uint) EVALFLAGS.EVAL_NOFUNCEVAL))
          pRetVal |= EvaluationFlags.NoFuncEval;

        return pRetVal;
      }
    }

    public  long radix { 
      get {
        return this.m_Radix;
      }
    }

    public  uint timeout { 
      get {
        return this.m_Timeout;
      }
    }

    public  IDebugSymbol GetContainer() {

      IDebugContainerField container = null;
      IDebugSymbol pRetVal = null;
      if ((HRESULT)HResult.S_OK == this.m_SymbolProvider.GetContainerField(this.m_Address, out container)) {
        pRetVal = SymbolHelper.SymbolFromDebugField(this, container as IDebugField);
      }

      return pRetVal;
    }

    public  IDebugType GetType(string fullName) {
      IDebugField type = null;
      this.SymbolProvider.GetTypeByName(fullName, NAME_MATCH.nmCaseSensitive, out type);
      if (type != null) return SymbolHelper.DebugTypeFromField(type, this);
      IEnumDebugFields namespaceList = null;
      this.SymbolProvider.GetNamespacesUsedAtAddress(this.Address, out namespaceList);
      if (namespaceList!= null){
        int namespaceCount = 0;
        int fetched = 0;
        FIELD_INFO namespaceInfo = new FIELD_INFO();
        namespaceList.GetCount(out namespaceCount);
        for (int i = 0; i < namespaceCount; i++){
          IDebugField[] namespc = new IDebugField[1];
          namespaceList.Next(1, namespc, out fetched);
          if (fetched > 0){
            namespc[0].GetInfo(FIELD_INFO_FIELDS.FIF_FULLNAME, out namespaceInfo);
            this.SymbolProvider.GetTypeByName(namespaceInfo.bstrFullName+"."+fullName, NAME_MATCH.nmCaseSensitive, out type);
            if (type != null) return SymbolHelper.DebugTypeFromField(type, this);
          }
        }
      }
      if (type == null)
        this.SymbolProvider.GetTypeByName("StructuralTypes."+fullName, NAME_MATCH.nmCaseSensitive, out type);
      if (type != null)
        return SymbolHelper.DebugTypeFromField(type, this);
      return null;

      
    }

    public  IDebugValue CreatePrimitiveValue(object val) {
      throw new Exception("Not implemented Yet");
    }

    public  IDebugValue CreateObject(IDebugMethodSymbol constructor, IDebugValue[] args) {
      throw new Exception("Not implemented Yet");
    }

    public  IDebugValue CreateString(string str) {
      throw new Exception("Not implemented Yet");
    }

    public  IDebugValue CreateObjectNoConstructor(IDebugType type) {
      throw new Exception("Not implemented Yet");
    }
  }
  public  class CDebugProperty : IDebugProperty2 {

    private IDebugProperty prop;
    private CDebugProperty parent;

    public  CDebugProperty(IDebugProperty prop) {
      this.prop = prop;
      this.parent = null;
    }

    // Get the DEBUG_PROPERTY_INFO that describes this property
    public  void GetPropertyInfo(
      DEBUGPROP_INFO_FLAGS dwFields, 
      uint dwRadix, 
      uint dwTimeout, 
      IDebugReference2 []rgpArgs, 
      uint dwArgCount, 
      out DEBUG_PROPERTY_INFO pPropertyInfo) {

      pPropertyInfo = new DEBUG_PROPERTY_INFO();

      if (0 != (dwFields & DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME)) {
				
        pPropertyInfo.dwFields       |= DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME;
        pPropertyInfo.bstrFullName    = this.prop.FullName;
      }

      if (0 != (dwFields & DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME)) {
        pPropertyInfo.dwFields     |= DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME;
        pPropertyInfo.bstrName     = this.prop.Name;
      }

      if (0 != (dwFields & DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_TYPE)) {
        pPropertyInfo.dwFields     |= DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_TYPE;
        pPropertyInfo.bstrType    = this.prop.Type;
      }

      if (0 != (dwFields & DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP)) {
        pPropertyInfo.dwFields     |= DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP;
        pPropertyInfo.pProperty     = this as IDebugProperty2;
      }

      if (0 != (dwFields & DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE)) {
        pPropertyInfo.dwFields |= DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE;
        pPropertyInfo.bstrValue    = this.prop.GetValue(dwRadix, dwTimeout);
      }

      if (0 != (dwFields & DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND)) {
        pPropertyInfo.dwFields   |= DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_AUTOEXPAND;
      }

      if (0 != (dwFields & DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB)){
        pPropertyInfo.dwFields |= DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB;

        DebugPropertyAttributes attrib;
        attrib = this.prop.Attributes;

        pPropertyInfo.dwAttrib = 0;
        if (0 != (attrib & DebugPropertyAttributes.Expandable))
          pPropertyInfo.dwAttrib |= DBG_ATTRIB_FLAGS.DBG_ATTRIB_OBJ_IS_EXPANDABLE;
        if (0 != (attrib & DebugPropertyAttributes.ReadOnly))
          pPropertyInfo.dwAttrib |= DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_READONLY;
        if (0 != (attrib & DebugPropertyAttributes.Error))
          pPropertyInfo.dwAttrib |= DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_ERROR;
        if (0 != (attrib & DebugPropertyAttributes.SideEffect))
          pPropertyInfo.dwAttrib |= DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_SIDE_EFFECT;
        if (0 != (attrib & DebugPropertyAttributes.OverloadedContainer))
          pPropertyInfo.dwAttrib |= DBG_ATTRIB_FLAGS.DBG_ATTRIB_OVERLOADED_CONTAINER;
        if (0 != (attrib & DebugPropertyAttributes.Boolean))
          pPropertyInfo.dwAttrib |= DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_BOOLEAN;
        if (0 != (attrib & DebugPropertyAttributes.BooleanTrue))
          pPropertyInfo.dwAttrib |= DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_BOOLEAN_TRUE;
        if (0 != (attrib & DebugPropertyAttributes.Invalid))
          pPropertyInfo.dwAttrib |= DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_INVALID;
        
      }
    }

    // Set the value of this property
    public  void SetValueAsString(
      String pszValue, 
      uint dwRadix, 
      uint dwTimeout) {

      this.prop.SetValue(pszValue, dwRadix, dwTimeout);
    }

    // Set the value of this property
    public  void SetValueAsReference(
      IDebugReference2 []rgpArgs, 
      uint dwArgCount, 
      IDebugReference2 pValue, 
      uint dwTimeout) {

      throw new Exception("Not Implemented Yet");
    }

    // Enum the children of this property
    public  void EnumChildren(
      DEBUGPROP_INFO_FLAGS dwFields, 
      uint dwRadix, 
      ref Guid guidFilter, 
      DBG_ATTRIB_FLAGS dwAttribFilter, 
      String pszNameFilter, 
      uint dwTimeout, 
      out IEnumDebugPropertyInfo2 ppEnum) {

      ppEnum = null;
      EnumerationKind kind;
      if (guidFilter == FilterGuids.guidFilterArgs)
        kind = EnumerationKind.Arguments;
        //kind = EnumerationKind.Locals;
      else if (guidFilter == FilterGuids.guidFilterLocals)
        kind = EnumerationKind.Locals;
      else if (guidFilter == FilterGuids.guidFilterLocalsPlusArgs)
        kind = EnumerationKind.LocalsPlusArguments;
      else if (guidFilter == FilterGuids.guidFilterThis)
        kind = EnumerationKind.This;
      else
        kind = EnumerationKind.None;

      IEnumDebugProperty enumProperty = null;
      enumProperty = this.prop.EnumChildren(kind, (int ) dwRadix, (int ) dwTimeout, 
        (0 != (dwFields & DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NOFUNCEVAL)) ? false : true);

      ppEnum = new CEnumDebugProperty2(enumProperty, dwRadix, dwFields, dwTimeout);

    }

    // Get the parent of this property
    public  void GetParent(out IDebugProperty2 ppParent) {
      ppParent = null;
      if (null == this.parent){
        IDebugProperty parentProp = null;
        parentProp = this.prop.Parent;

        if (null != parentProp){
          this.parent = new CDebugProperty(parentProp);
        }
      }

      if (null != this.parent){
        ppParent = this.parent;
      }

    }

    // Get the property that describes the derived most property of this property
    public  void GetDerivedMostProperty(out IDebugProperty2 ppDerivedMost) {
      throw new Exception("Not Implemented Yet");
    }

    // Get the memory bytes that contains this property
    public  void GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes) {
      throw new Exception("Not Implemented Yet");
    }

    // Get a memory context for this property within the memory bytes returned by GetMemoryBytes
    public  void GetMemoryContext(out IDebugMemoryContext2 ppMemory) {
      throw new Exception("Not Implemented Yet");
    }

    // Get the size (in bytes) of this property
    public  void GetSize(out uint pdwSize) {
      throw new Exception("Not Implemented Yet");
    }

    // Get a reference for this property
    public  void GetReference(out IDebugReference2 ppReference) {
      throw new Exception("Not Implemented Yet");
    }

    // Get extended info for this property
    public  void GetExtendedInfo(ref Guid guidExtendedInfo, /*VARIANT*/ out object pExtendedInfo) {
      throw new Exception("Not Implemented Yet");
    }
  }


  public  class CEnumDebugProperty2 : IEnumDebugPropertyInfo2 {

    protected IEnumDebugProperty enumDebugProperty;
    protected uint dwRadix;
    protected DEBUGPROP_INFO_FLAGS dwFields;
    protected uint timeout;

    public  CEnumDebugProperty2(IEnumDebugProperty enumDebugProperty, uint dwRadix, DEBUGPROP_INFO_FLAGS dwFields, uint timeout) {
      this.enumDebugProperty = enumDebugProperty;
      this.dwFields = dwFields;
      this.dwRadix = dwRadix;
      this.timeout = timeout;
    }

    public  void Next(
      System.UInt32 celt, 
      DEBUG_PROPERTY_INFO []rgelt, 
      out System.Int32 pceltFetched) {
      
      pceltFetched = 0;
      if (rgelt == null)
        rgelt	= new DEBUG_PROPERTY_INFO[celt];

      for (ulong i = 0; i < celt; i++){
        IDebugProperty debugProperty = null;
        debugProperty = this.enumDebugProperty.Current;
        if (null != debugProperty) {
          CDebugProperty prop = new CDebugProperty(debugProperty);
          if (null != prop){
            prop.GetPropertyInfo(this.dwFields, this.dwRadix, this.timeout, null, 0, out rgelt[i]);
          } 
        }

        pceltFetched++;

        bool success = false;
        success = this.enumDebugProperty.MoveNext();
        if (success == false && i < celt-1) {
          break;
        }
      }
    }

    public  void Skip(System.UInt32 celt) {
      for (uint i = 0; i < celt; i++){
        bool success = false;
        success = this.enumDebugProperty.MoveNext();
        if (success == false)
          break;
      }
    }

    public  void Reset() {
      this.enumDebugProperty.Reset();
    }

    public  void Clone(out IEnumDebugPropertyInfo2 ppEnum) {
      ppEnum = null;
      IEnumDebugProperty copyEnum = null;
      copyEnum = this.enumDebugProperty.Clone();
      if (null != copyEnum)
        ppEnum = new CEnumDebugProperty2(copyEnum, this.dwRadix, this.dwFields, this.timeout);
    }

    public  void GetCount(out System.UInt32 pcelt) {
      pcelt = (uint ) this.enumDebugProperty.Count;
    }
  }
  public class DebugEnvironment{
    public IDebugContext context;
    public DebugEnvironment(){
      this.context = null;
    }
    public DebugEnvironment(IDebugContext context){
      this.context = context;
    }
  }
}
