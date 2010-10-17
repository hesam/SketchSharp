//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
#if CCINamespace
using Microsoft.Cci;
#else
using System.Compiler;
#endif
using System.Runtime.InteropServices;
using HRESULT = System.Int32;
using System.Diagnostics;

namespace Microsoft.VisualStudio.IntegrationHelper{
  internal abstract class SymbolHelper {
    internal static TypeNodeList GetTypeList(string typeList, IDebugContext context){
      TypeNodeList list = new TypeNodeList();
      int startIndex = typeList.LastIndexOf(".")+1;
      int endIndex;
      IDebugType typ;
      while((endIndex = typeList.IndexOf("Or", startIndex)) > 0){
        typ = context.GetType(typeList.Substring(startIndex, endIndex - startIndex));
        if (typ != null) list.Add(typ.CompilerType);
        startIndex = endIndex+2;
      }
      typ = context.GetType(typeList.Substring(startIndex));
      if (typ != null) list.Add(typ.CompilerType);
      return list;
    }

    internal static IDebugType DebugTypeFromField(IDebugField field, IDebugContext context) {
      IDebugType type = null;
      FIELD_KIND kind;
      field.GetKind(out kind);
      if (0 != (FIELD_KIND.FIELD_KIND_TYPE & kind)) {
        if (0 != (kind & FIELD_KIND.FIELD_TYPE_PRIMITIVE)){
          type = new CDebugPrimitiveType(field, context);
        } else if (0 != (kind & (FIELD_KIND.FIELD_TYPE_CLASS|FIELD_KIND.FIELD_TYPE_STRUCT|FIELD_KIND.FIELD_TYPE_INNERCLASS))) {
          IDebugClassField classField = null;
          if (null != (classField = field as IDebugClassField)) {
            if (classField.DoesInterfaceExist("StructuralTypes.TupleType") == (int)HResult.S_OK)
              type = new CDebugStructuralType(classField, context, StructTypes.Tuple);
            else if(classField.DoesInterfaceExist("StructuralTypes.TypeUnion") == (int)HResult.S_OK)
              type = new CDebugStructuralType(classField, context, StructTypes.Union);
            else if(classField.DoesInterfaceExist("StructuralTypes.TypeIntersection") == (int)HResult.S_OK)
              type = new CDebugStructuralType(classField, context, StructTypes.Intersection);
            else{
              FIELD_INFO name = new FIELD_INFO();
              classField.GetInfo(FIELD_INFO_FIELDS.FIF_NAME, out name);
              if (name.bstrName.IndexOf("IEnumerableOf") >=0)
                type = new CDebugStreamType(classField, context, StreamType.GenericIEnumerable);
              else if (name.bstrName.IndexOf("NonEmptyIEnumerableOf") >=0)
                type = new CDebugStreamType(classField, context, StreamType.GenericNonEmptyIEnumerable);
              else if (name.bstrName.IndexOf("NonNullOf") >=0)
                type = new CDebugStreamType(classField, context, StreamType.GenericNonNull);
              else if (name.bstrName.IndexOf("BoxedOf") >=0)
                type = new CDebugStreamType(classField, context, StreamType.GenericBoxed);
              else if (name.bstrName.IndexOf("IListOf") >=0)
                type = new CDebugFlexArrayType(classField, context);
              else 
                type = new CDebugClassType(classField, context);
            }
          }
        } else if (0 != (kind & FIELD_KIND.FIELD_TYPE_ENUM)) {
          IDebugEnumField enumField = null;
          if (null != (enumField = field as IDebugEnumField)) {
            type = new CDebugEnumType(enumField, context);
          }
        } else if (0 != (kind & FIELD_KIND.FIELD_TYPE_ARRAY)) {
          IDebugArrayField arrayField = null;
          if (null != (arrayField = field as IDebugArrayField)) {
            type = new CDebugArrayType(arrayField, context);
          }
        }
      }
      return type;
    }

    internal static uint SymbolKindToFieldKind(SymbolKind kind) {
      uint ret = 0;
      if (kind == SymbolKind.All)
        ret = (uint ) FIELD_KIND.FIELD_KIND_ALL;
      else{
        if (0 != (kind & SymbolKind.Method))
          ret |= (uint ) (FIELD_KIND.FIELD_SYM_MEMBER|FIELD_KIND.FIELD_TYPE_METHOD);
        if (0 != (kind & SymbolKind.Property))
          ret |= (uint ) (FIELD_KIND.FIELD_SYM_MEMBER|FIELD_KIND.FIELD_TYPE_PROP);
        if (0 != (kind & SymbolKind.Field))
          ret |= (uint ) (FIELD_KIND.FIELD_KIND_ALL & ~(FIELD_KIND.FIELD_TYPE_METHOD|FIELD_KIND.FIELD_TYPE_PROP));
        if (0 != (kind & SymbolKind.This))
          ret |= (uint ) FIELD_KIND.FIELD_SYM_THIS;
        if (0 != (kind & SymbolKind.Local))
          ret |= (uint ) FIELD_KIND.FIELD_SYM_LOCAL;
        if (0 != (kind & SymbolKind.Parameter))
          ret |= (uint ) FIELD_KIND.FIELD_SYM_PARAM;
      }
      return ret;
    }

    internal static uint SymbolModifiersToFieldModifiers(uint mod) {
      uint ret = 0;
      if (0 != (mod & (uint ) SymbolModifiers.All))
        ret |= (uint ) SymbolModifiers.All;
      if (0 != (mod & (uint ) SymbolModifiers.Static))
        ret |= (uint ) FIELD_MODIFIERS.FIELD_MOD_STATIC;
      if (0 != (mod & (uint ) SymbolModifiers.Abstract))
        ret |= (uint ) FIELD_MODIFIERS.FIELD_MOD_ABSTRACT;
      if (0 != (mod & (uint ) SymbolModifiers.Virtual))
        ret |= (uint ) FIELD_MODIFIERS.FIELD_MOD_VIRTUAL;
      if (0 != (mod & (uint ) SymbolModifiers.Native))
        ret |= (uint ) FIELD_MODIFIERS.FIELD_MOD_NATIVE;
      if (0 != (mod & (uint ) SymbolModifiers.Public))
        ret |= (uint ) FIELD_MODIFIERS.FIELD_MOD_ACCESS_PUBLIC;
      if (0 != (mod & (uint ) SymbolModifiers.Protected))
        ret |= (uint ) FIELD_MODIFIERS.FIELD_MOD_ACCESS_PROTECTED;
      if (0 != (mod & (uint ) SymbolModifiers.Private))
        ret |= (uint ) FIELD_MODIFIERS.FIELD_MOD_ACCESS_PRIVATE;
      return ret;
    }

    internal static IDebugSymbol SymbolFromDebugField(IDebugContext context, IDebugField field) {
      IDebugSymbol symbol = null;
      FIELD_KIND fieldKind;
      field.GetKind(out fieldKind);

      if (0 != (fieldKind & FIELD_KIND.FIELD_KIND_SYMBOL)) {
        if (0 != (fieldKind & FIELD_KIND.FIELD_TYPE_METHOD)) {
          IDebugMethodField methodField = null;
          methodField = field as IDebugMethodField;
          if (null != methodField){
            CDebugMethodSymbol methodSymbol = null;
            methodSymbol = CDebugMethodSymbol.Create(methodField, context);
            if (null != methodSymbol) {
              symbol = methodSymbol;
            }
          }
        } else if (0 != (fieldKind & FIELD_KIND.FIELD_TYPE_PROP)) {
          IDebugPropertyField propertyField = null;
          propertyField = field as IDebugPropertyField;
          if (null != propertyField){
            CDebugPropertySymbol propertySymbol = null;
            propertySymbol = CDebugPropertySymbol.Create(propertyField, context);
            if (null != propertySymbol) {
              symbol = propertySymbol;
            }
          }
        } else{
          CDebugFieldSymbol fieldSymbol = null;
          fieldSymbol = CDebugFieldSymbol.Create(field, context);
          if (null != fieldSymbol) {
            symbol = fieldSymbol;
          }
        }
      }

      return symbol;
    }
  }

  public  class CDebugSymbol :  IDebugSymbol {

    public IDebugField  m_Field;
    public IDebugContext m_Context;

    public  CDebugSymbol(IDebugField field, IDebugContext context) {
      this.m_Field   = field;
      this.m_Context = context;
    }

    public  String Name {
      get {
        FIELD_INFO fieldInfo;
        this.m_Field.GetInfo(FIELD_INFO_FIELDS.FIF_FULLNAME, out fieldInfo);
        return fieldInfo.bstrFullName;
      }
    }

    public  virtual IDebugType Type { 
      get {
        IDebugField typeField = null;
        this.m_Field.GetType(out typeField);
        return SymbolHelper.DebugTypeFromField(typeField, this.m_Context);
      }
    }

    public  SymbolKind Kind { 
      get {
        FIELD_KIND fieldKind = 0;
        this.m_Field.GetKind(out fieldKind);
        if (0 != (fieldKind & FIELD_KIND.FIELD_TYPE_METHOD))
          return SymbolKind.Method;
        else if (0 != (fieldKind & FIELD_KIND.FIELD_TYPE_PROP))
          return SymbolKind.Property;
        else if (0 != (fieldKind & FIELD_KIND.FIELD_SYM_MEMBER))
          return SymbolKind.Field;
        else if (0 != (fieldKind & FIELD_KIND.FIELD_SYM_THIS))
          return SymbolKind.This;
        else if (0 != (fieldKind & FIELD_KIND.FIELD_SYM_LOCAL))
          return SymbolKind.Local;
        else if (0 != (fieldKind & FIELD_KIND.FIELD_SYM_PARAM))
          return SymbolKind.Parameter;

        return 0;

      }
    }
    public  SymbolModifiers Modifiers { 
      get {
        SymbolModifiers ret = 0;
        FIELD_INFO fieldInfo;
        this.m_Field.GetInfo(FIELD_INFO_FIELDS.FIF_MODIFIERS, out fieldInfo);
        if (0 != (fieldInfo.dwModifiers & FIELD_MODIFIERS.FIELD_MOD_STATIC))
          ret |= SymbolModifiers.Static;
        if (0 != (fieldInfo.dwModifiers & FIELD_MODIFIERS.FIELD_MOD_ABSTRACT))
          ret |= SymbolModifiers.Abstract;
        if (0 != (fieldInfo.dwModifiers & FIELD_MODIFIERS.FIELD_MOD_VIRTUAL))
          ret |= SymbolModifiers.Virtual;
        if (0 != (fieldInfo.dwModifiers & FIELD_MODIFIERS.FIELD_MOD_NATIVE))
          ret |= SymbolModifiers.Native;
        if (0 != (fieldInfo.dwModifiers & FIELD_MODIFIERS.FIELD_MOD_ACCESS_PUBLIC))
          ret |= SymbolModifiers.Public;
        if (0 != (fieldInfo.dwModifiers & FIELD_MODIFIERS.FIELD_MOD_ACCESS_PROTECTED))
          ret |= SymbolModifiers.Protected;
        if (0 != (fieldInfo.dwModifiers & FIELD_MODIFIERS.FIELD_MOD_ACCESS_PRIVATE))
          ret |= SymbolModifiers.Private;

        return ret;
      }
    }

    public  IDebugType GetContainer() {
      IDebugContainerField container = null;
      this.m_Field.GetContainer(out container);
      if (null != container) {
        return SymbolHelper.DebugTypeFromField(container as IDebugField, this.m_Context);
      }
      return null;
    }
  }
  public  class CDebugFieldSymbol :  CDebugSymbol, IDebugFieldSymbol {
    public IDebugValue m_Parent;
    public  CDebugFieldSymbol(IDebugField field, IDebugContext context) 
      : base(field, context){
      this.m_Parent = null;
    }
    public  CDebugFieldSymbol(IDebugField field, IDebugValue parent, IDebugContext context) 
      : this(field, context){
      this.m_Parent = parent;
    }

    public  static CDebugFieldSymbol Create(IDebugField field, IDebugContext context) {
      return new CDebugFieldSymbol(field, context);
    }

    public  override IDebugType Type { 
      get {
        IDebugField typeField = null;
        this.m_Field.GetType(out typeField);
        return SymbolHelper.DebugTypeFromField(typeField, this.m_Context);
      }
    }

    public  IDebugValue GetValue(IDebugValue container) {

      IDebugValue pRetVal = null;

      IDebugObject containerObject = null;
      if (this.m_Parent != null)
        container = this.m_Parent;
      if (null != container)
        containerObject = ((CDebugValue) container).GetObject();
      IDebugBinder binder = this.m_Context.Binder;
      if (null != binder){
        IDebugObject pObject = null;
        binder.Bind(containerObject, this.m_Field, out pObject);

        IDebugEnumField enumField = null;
        if ( null != (enumField = this.m_Field as IDebugEnumField)) {
          IDebugField underlyingField = null;
          enumField.GetUnderlyingSymbol(out underlyingField);
          if (null != underlyingField){
            IDebugObject underlyingObject = null;
            binder.Bind(pObject, underlyingField, out underlyingObject);
            if (null != underlyingObject){
              pObject = underlyingObject;
            }
          }
        }
        if (null != pObject){
          if(this.Type.Kind == TypeKind.Array)
            pRetVal = new CDebugArrayValue(pObject as IDebugArrayObject, this.m_Context);
          else
            pRetVal = new CDebugValue(pObject, this.m_Context);
        }
      }
      return pRetVal;
    }
  }

  public  class CDebugMethodSymbol : CDebugSymbol, IDebugMethodSymbol {

    protected IDebugMethodField m_MethodField;

    public  CDebugMethodSymbol(IDebugMethodField field, IDebugContext context) : base (field as IDebugField, context) {
      this.m_MethodField = field;
    }

    public  static CDebugMethodSymbol Create(IDebugMethodField field, IDebugContext context) {
      return new CDebugMethodSymbol(field, context);
    }

    public IDebugClassType GetDeclaringType(){
      FIELD_INFO info = new FIELD_INFO();
      this.m_MethodField.GetInfo(FIELD_INFO_FIELDS.FIF_MODIFIERS, out info);
      if ((info.dwModifiers|FIELD_MODIFIERS.FIELD_MOD_STATIC) != 0){
        IDebugContainerField containerField = null;
        IDebugClassField classField = null;
        this.m_MethodField.GetContainer(out containerField);
        classField = containerField as IDebugClassField;
        if (classField != null){
          return new CDebugClassType(classField, this.m_Context);
        }
      }
      return null;
    }

    public  IEnumSymbol GetParameters() {
      IEnumSymbol pRetVal = null;
      IEnumDebugFields enumFields = null;
      this.m_MethodField.EnumParameters(out enumFields);
      if (null != enumFields){
        pRetVal = new CEnumSymbols(enumFields, this.m_Context);
      }
      return pRetVal;
    }

    public  IEnumSymbol GetLocals() {
      IEnumSymbol pRetVal = null;
      IEnumDebugFields enumFields = null;
      this.m_MethodField.EnumLocals(this.m_Context.Address, out enumFields);
      if (null != enumFields){
        pRetVal = new CEnumLocalSymbols(enumFields, this.m_Context);
      }
      return pRetVal;
    }

    public  IEnumSymbol GetStaticLocals() {
      IEnumSymbol pRetVal = null;
      IEnumDebugFields enumFields = null;
      this.m_MethodField.EnumStaticLocals(out enumFields);
      if (null != enumFields){
        pRetVal = new CEnumSymbols(enumFields, this.m_Context);
      }
      return pRetVal;
    }

    public  IDebugFieldSymbol GetThis() {
      IDebugFieldSymbol pRetVal = null;
      IDebugClassField classField = null;
      this.m_MethodField.GetThis(out classField);
      if (null != classField){
        pRetVal = CDebugFieldSymbol.Create(classField as IDebugField, this.m_Context);
        IDebugClassType classType = pRetVal.Type as IDebugClassType;
        IDebugValue parent = pRetVal.GetValue(null);
        if (classType.Name.IndexOf("closure") >= 0){
          IEnumSymbol thisSymbol = classType.GetMembers("this value: ", true, SymbolKind.Field, SymbolModifiers.All);
          if(thisSymbol != null && thisSymbol.Current != null && thisSymbol.Current is CDebugFieldSymbol){
            CDebugFieldSymbol realThis = thisSymbol.Current as CDebugFieldSymbol;
            realThis.m_Parent = parent;
            pRetVal = realThis;
          }
        }
      }
      
      return pRetVal;
    }

    public  IDebugValue Evaluate(IDebugValue containerValue, IDebugValue[] arguments) {
      IDebugValue pRetVal = null;
      uint argcount = 0;
      if( null != arguments ) {
        argcount = (uint ) arguments.Length;
      }

      IDebugObject[] args = new IDebugObject[argcount];
      if (null != args){
        if (argcount > 0){
          for (uint i = 0; i < argcount; i++){
            IDebugValue value = arguments[i] as IDebugValue;
            if (null != value){
              args[i] = ((CDebugValue ) value).GetObject();
            }
            else {
              return null;
            }
          }
        }
        IDebugObject containerObject = null;
        if (null != containerValue){
          containerObject = ((CDebugValue ) containerValue).GetObject();
        }
        IDebugBinder binder = this.m_Context.Binder;
        IDebugObject pObject = null;
        binder.Bind(containerObject, this.m_Field, out pObject);
        IDebugFunctionObject pFuncObject = null;
        pFuncObject = pObject as IDebugFunctionObject;
        if (null != pFuncObject) {
          IDebugObject result = null;
          pFuncObject.Evaluate(args, argcount, /*this.m_Context.timeout*/ 99999, out result);
          if (null != result) {
            pRetVal = new CDebugValue(result, this.m_Context);
          }
        }
      }
      return pRetVal;
    }
  }

  public  class CDebugPropertySymbol : CDebugSymbol, IDebugPropertySymbol {
    protected IDebugPropertyField   m_PropertyField;

    public  CDebugPropertySymbol(IDebugPropertyField field, IDebugContext context) : base (field as IDebugField, context) {
      this.m_PropertyField = field;
    }

    public  static CDebugPropertySymbol Create(IDebugPropertyField field, IDebugContext context) {
      return new CDebugPropertySymbol(field, context);
    }

    public IDebugMethodSymbol GetGetter() {
      IDebugMethodSymbol pRetVal = null;
      IDebugMethodField method = null;
      this.m_PropertyField.GetPropertyGetter(out method);
      if (null != method){
        CDebugMethodSymbol methodSymbol = null;
        methodSymbol = CDebugMethodSymbol.Create(method, this.m_Context);
        if (null != methodSymbol) {
          pRetVal = methodSymbol;
        }
      }
      return pRetVal;
    }

    public IDebugMethodSymbol GetSetter() {
      IDebugMethodSymbol pRetVal = null;
      IDebugMethodField method = null;
      this.m_PropertyField.GetPropertySetter(out method);
      if (null != method){
        CDebugMethodSymbol methodSymbol = null;
        methodSymbol = CDebugMethodSymbol.Create(method, this.m_Context);
        if (null != methodSymbol) {
          pRetVal = methodSymbol;
        }
      }
      return pRetVal;
    }
  }

  public  class CEnumSymbols : IEnumSymbol {
    protected IDebugContext m_Context;
    protected IEnumDebugFields m_Fields;
    protected IDebugSymbol m_Current;

    public  CEnumSymbols(IEnumDebugFields fields, IDebugContext context) {
      this.m_Fields = fields;
      this.m_Context = context;
      this.m_Current = null;
    }

    public  IDebugSymbol Current { 
      get{
        IDebugSymbol pRetVal = null;
        if (null == this.m_Current){
          IDebugField field = null;
          int fetched = 0;
          IDebugField[] fields = new IDebugField[1];
          this.m_Fields.Next(1, fields,  out fetched);
          if (null != fields && fetched > 0){
            field = fields[0];
            IDebugSymbol symbol = null;
            symbol = SymbolHelper.SymbolFromDebugField(this.m_Context, field);
            if (null != symbol) {
              this.m_Current = symbol;
            }
                                                                                    
          }
        }
        if (null != this.m_Current){
          pRetVal = this.m_Current;
        }

        return pRetVal;
      }
    }

    public  bool MoveNext() {

      bool pRetVal = false;

      if (null != this.m_Current) {
        this.m_Current = null;
      } else {
        // Skip the first element
        this.m_Fields.Skip(1);
      }

      IDebugField field = null;
      int fetched = 0;
      IDebugField[] fields = new IDebugField[1];
      this.m_Fields.Next(1, fields, out fetched);

      if (null != fields[0]){
        field = fields[0];
        IDebugSymbol symbol = null;
        symbol = SymbolHelper.SymbolFromDebugField(this.m_Context, field);

        if (null != symbol) {
          this.m_Current = symbol;
          pRetVal = true;
        }
      }
      return pRetVal;
    }

    public  void Reset() {
      this.m_Fields.Reset();
      this.m_Current = null;
    }

    public  int Count { 
      get {
        int pRetVal = 0;
        this.m_Fields.GetCount(out pRetVal);
        return pRetVal;
      }
    }

    public  IEnumSymbol Clone() {
      IEnumSymbol pRetVal = null;
      IEnumDebugFields copyFields = null;
      this.m_Fields.Clone(out copyFields);

      if (null != copyFields){
        pRetVal = new CEnumSymbols(copyFields, this.m_Context);
      }
      return pRetVal;
    }
  }
  public class CEnumClosureClassSymbols : IEnumSymbol{
    protected IDebugContext m_Context;
    protected IDebugValue m_Parent;
    protected IDebugClassType m_ClassType;
    protected IEnumSymbol m_FieldEnumerator;
    protected IDebugSymbol m_Current;
    public CEnumClosureClassSymbols(IDebugValue parent, IDebugContext context){
      this.m_Context = context;
      this.m_Parent = parent;
      this.m_ClassType = parent.RuntimeType() as IDebugClassType;
      this.m_Current = null;
      if (this.m_Parent != null && this.m_ClassType != null){
        this.m_FieldEnumerator = this.m_ClassType.GetMembers(null, true, SymbolKind.Field, SymbolModifiers.All);
      }
    }
    public IDebugSymbol Current{ 
      get{
        IDebugSymbol pRetVal = null;
        if (this.m_Current == null){
          if (this.m_FieldEnumerator != null)
            while (this.m_FieldEnumerator.Current != null){
              if (this.m_FieldEnumerator.Current.Name.IndexOf(": ") > 0)
                this.m_FieldEnumerator.MoveNext();
              else{
                CDebugSymbol sym = this.m_FieldEnumerator.Current as CDebugSymbol;
                this.m_Current = new CDebugFieldSymbol(sym.m_Field, this.m_Parent, this.m_Context);
                //this.m_Current = new CDebugClosureFieldSymbol(this.m_FieldEnumerator.Current as IDebugFieldSymbol, this.m_Context, this.m_Parent);
                break;
              }
            }
        }
        if (this.m_Current != null)
          pRetVal = this.m_Current;
        return pRetVal;
      }
    }
    public bool MoveNext(){
      bool pRetVal = false;
      if (null != this.m_Current) {
        this.m_Current = null;
      } else {
        // Skip the first element
        this.m_Current = this.Current;
        this.m_Current = null;
      }
      if (this.m_FieldEnumerator != null)
        while (this.m_FieldEnumerator.Current != null){
          this.m_FieldEnumerator.MoveNext();
          if (this.m_FieldEnumerator.Current != null && this.m_FieldEnumerator.Current.Name.IndexOf(": ") < 0){
            CDebugSymbol sym = this.m_FieldEnumerator.Current as CDebugSymbol;
            this.m_Current = new CDebugFieldSymbol(sym.m_Field, this.m_Parent, this.m_Context);
            pRetVal = true;
            break;
          }
        }
      return pRetVal;
    }
    public void Reset(){
      if (this.m_FieldEnumerator != null)
        this.m_FieldEnumerator.Reset();
      this.m_Current = null;
    }
    public int Count{ 
      get {
        int pRetVal = 0;
        IEnumSymbol counter = this.Clone();
        while(counter.Current != null){
          pRetVal++;
          counter.MoveNext();
        }
        return pRetVal;
      }
    }
    public IEnumSymbol Clone(){
      IEnumSymbol pRetVal = null;
      if (this.m_Parent != null){
        CEnumClosureClassSymbols copy = new CEnumClosureClassSymbols(this.m_Parent, this.m_Context);
        copy.m_Current = this.m_Current;
        copy.m_FieldEnumerator = this.m_FieldEnumerator.Clone();
        pRetVal = copy;
      }
      return pRetVal;
    }
  }
  public class CEnumLocalSymbols : IEnumSymbol{
    protected IDebugContext m_Context;
    protected IEnumDebugFields m_Fields;
    protected IDebugSymbol m_Current;
    protected bool m_IsEnumeratingClosureClass;
    protected IEnumSymbol m_ClosureClassFields;
    protected IDebugSymbol m_ReturnLocal;
    protected bool m_DisplayRetunLocal;

    public  CEnumLocalSymbols(IEnumDebugFields fields, IDebugContext context) {
      this.m_Fields = fields;
      this.m_Context = context;
      this.m_Current = null;
      this.m_IsEnumeratingClosureClass = false;
      this.m_DisplayRetunLocal = false;
    }

    public  IDebugSymbol Current{ 
      get{
        IDebugSymbol pRetVal = null;
        if (null == this.m_Current){
          if (this.m_IsEnumeratingClosureClass){
            this.m_Current = this.m_ClosureClassFields.Current;
          }
          else{
            IDebugField field = null;
            int fetched = 0;
            IDebugField[] fields = new IDebugField[1];
            this.m_Fields.Next(1, fields,  out fetched);
            if (null != fields && fetched > 0){
              field = fields[0];
              IDebugSymbol symbol = null;
              symbol = SymbolHelper.SymbolFromDebugField(this.m_Context, field);
              if (null != symbol) {
                if (symbol.Name.StartsWith("SS$Closure Class Local")){
                  this.m_IsEnumeratingClosureClass = true;
                  this.m_ClosureClassFields = new CEnumClosureClassSymbols(((IDebugFieldSymbol) symbol).GetValue(null), this.m_Context);
                  this.m_Current = this.m_ClosureClassFields.Current;
                }
                else if (String.Compare(symbol.Name, "return value") == 0){
                  this.m_ReturnLocal = symbol;
                  if (this.m_DisplayRetunLocal)
                    this.m_Current = symbol;
                  else
                    this.m_Current = this.Current;
                }
                else if (String.Compare(symbol.Name, "SS$Display Return Local") == 0){
                  this.m_DisplayRetunLocal = true;
                  if (this.m_ReturnLocal != null)
                    this.m_Current = this.m_ReturnLocal;
                  else
                    this.m_Current = this.Current;
                }
                else{
                  this.m_Current = symbol;
                }
              }
            }
          }
        }
        if (null != this.m_Current){
          pRetVal = this.m_Current;
        }

        return pRetVal;
      }
    }

    public  bool MoveNext() {

      bool pRetVal = false;
      IDebugSymbol saveCurrent = this.m_Current;

      if (null != this.m_Current) {
        this.m_Current = null;
      } else {
        // Skip the first element
        this.m_Current = this.Current;
        this.m_Current = null;
      }

      if (this.m_IsEnumeratingClosureClass){
        if (this.m_ClosureClassFields.MoveNext()){
          this.m_Current = this.m_ClosureClassFields.Current;
          pRetVal = true;
        }
        else{
          this.m_IsEnumeratingClosureClass = false;
          this.m_Current = saveCurrent;
          pRetVal = this.MoveNext();
        }
      }
      else{
        IDebugField field = null;
        int fetched = 0;
        IDebugField[] fields = new IDebugField[1];
        this.m_Fields.Next(1, fields, out fetched);

        if (null != fields[0]){
          field = fields[0];
          IDebugSymbol symbol = null;
          symbol = SymbolHelper.SymbolFromDebugField(this.m_Context, field);
          if (null != symbol){
            if (symbol.Name.StartsWith("SS$Closure Class Local")){
              this.m_IsEnumeratingClosureClass = true;
              this.m_ClosureClassFields = new CEnumClosureClassSymbols(((IDebugFieldSymbol) symbol).GetValue(null), this.m_Context);
              this.m_Current = this.m_ClosureClassFields.Current;
              pRetVal = true;
            }
            else if (String.Compare(symbol.Name, "return value") == 0){
              this.m_ReturnLocal = symbol;
              if (this.m_DisplayRetunLocal){
                this.m_Current = symbol;
                pRetVal = true;
              }
              else{
                this.m_Current = symbol;
                pRetVal = this.MoveNext();
              }
            }
            else if (String.Compare(symbol.Name, "SS$Display Return Local") == 0){
              if (this.m_ReturnLocal != null){
                this.m_Current = this.m_ReturnLocal;
                pRetVal = true;
              }
              else{
                this.m_DisplayRetunLocal = true;
                pRetVal = this.MoveNext();
              }
            }
            else{
              this.m_Current = symbol;
              pRetVal = true;
            }
            
          }
        }
      }
      return pRetVal;
    }

    public  void Reset() {
      this.m_Fields.Reset();
      this.m_Current = null;
    }

    public  int Count { 
      get {
        int pRetVal = 0;
        IEnumSymbol counter = this.Clone();
        while(counter.Current != null){
          pRetVal++;
          counter.MoveNext();
        }
        return pRetVal;
      }
    }

    public  IEnumSymbol Clone() {
      IEnumSymbol pRetVal = null;
      IEnumDebugFields copyFields = null;
      this.m_Fields.Clone(out copyFields);

      if (null != copyFields){
        CEnumLocalSymbols copy = new CEnumLocalSymbols(copyFields, this.m_Context);
        if (this.m_IsEnumeratingClosureClass){
          copy.m_IsEnumeratingClosureClass = true;
          copy.m_ClosureClassFields = this.m_ClosureClassFields.Clone();
          copy.m_Current = this.m_Current;
        }
        pRetVal = copy;
      }
      return pRetVal;
    }
  }


  public  class CDebugType : IDebugType {
    
    protected IDebugContext m_Context;
    protected IDebugField m_Field;

    public  CDebugType(IDebugField field, IDebugContext context) {
      this.m_Field = field;
      this.m_Context = context;
    }

    public virtual String Name { 
      get {
        String pRetVal = null;
        FIELD_INFO fieldInfo;
        this.m_Field.GetInfo(FIELD_INFO_FIELDS.FIF_NAME, out fieldInfo);
        pRetVal = fieldInfo.bstrName;

        return pRetVal;
      }
    }

    public virtual String FullName { 
      get {
        String pRetVal = null;
        FIELD_INFO fieldInfo;
        this.m_Field.GetInfo(FIELD_INFO_FIELDS.FIF_FULLNAME, out fieldInfo);
        pRetVal = fieldInfo.bstrFullName;

        return pRetVal;
      }
    }

    public virtual TypeKind Kind { 
      get {
        TypeKind pRetVal = 0;
        FIELD_KIND kind = 0;
        this.m_Field.GetKind(out kind);

        if (0 != (kind & FIELD_KIND.FIELD_TYPE_PRIMITIVE))
          pRetVal = TypeKind.Primitive;
        else if (0 != (kind & (FIELD_KIND.FIELD_TYPE_CLASS|FIELD_KIND.FIELD_TYPE_STRUCT)))
          pRetVal = TypeKind.Class;
        else if (0 != (kind & FIELD_KIND.FIELD_TYPE_ARRAY))
          pRetVal = TypeKind.Array;
        else if (0 != (kind & FIELD_KIND.FIELD_TYPE_INNERCLASS))
          pRetVal = TypeKind.InnerClass;
        else if (0 != (kind & FIELD_KIND.FIELD_TYPE_ENUM))
          pRetVal = TypeKind.Enum;


        return pRetVal;
      }
    }

    public virtual TypeCode TypeCode { 
      get {
        TypeCode pRetVal = TypeCode.Empty;
        FIELD_KIND kind = 0;
        this.m_Field.GetKind(out kind);

        if (0 != (kind & FIELD_KIND.FIELD_TYPE_PRIMITIVE)){
          uint size = 0;
          this.m_Field.GetSize(out size);
          if (0 <= size){
            FIELD_INFO fieldInfo;
            this.m_Field.GetInfo(FIELD_INFO_FIELDS.FIF_NAME, out fieldInfo);
            switch(size){
              case 0:
                if ( 0 == String.Compare("string", fieldInfo.bstrName, true))
                  pRetVal = TypeCode.String;
                else if (0 == String.Compare("void", fieldInfo.bstrName, true))
                  pRetVal = TypeCode.Empty;
                break;
              case 1:
                if (0 == String.Compare("whole", fieldInfo.bstrName, true))
                  pRetVal = TypeCode.SByte;
                else if (0 == String.Compare("uwhole", fieldInfo.bstrName, true))
                  pRetVal = TypeCode.Byte;
                else if (0 == String.Compare("bool", fieldInfo.bstrName, true))
                  pRetVal = TypeCode.Boolean;
                break;
              case 2:
                if (0 == String.Compare("whole", fieldInfo.bstrName, true))
                  pRetVal = TypeCode.Int16;
                else if (0 == String.Compare("char", fieldInfo.bstrName, true))
                  pRetVal = TypeCode.Char;
                else if (0 == String.Compare("uwhole", fieldInfo.bstrName, true))
                  pRetVal = TypeCode.UInt16 ;
                else if (0 == String.Compare("bool", fieldInfo.bstrName, true))
                  pRetVal = TypeCode.Boolean;
                break;
              case 4:
                if (0 == String.Compare("whole", fieldInfo.bstrName, true))
                  pRetVal = TypeCode.Int32;
                else if (0 == String.Compare("uwhole", fieldInfo.bstrName, true))
                  pRetVal = TypeCode.UInt32;
                else if (0 == String.Compare("real", fieldInfo.bstrName, true))
                  pRetVal = TypeCode.Single;
                else if (0 == String.Compare("bool", fieldInfo.bstrName, true)) 
                  pRetVal = TypeCode.Boolean;
                break;
              case 8:
                if (0 == String.Compare("whole", fieldInfo.bstrName, true))
                  pRetVal = TypeCode.Int64;
                else if (0 == String.Compare("uwhole", fieldInfo.bstrName, true))
                  pRetVal = TypeCode.UInt64;
                else if (0 == String.Compare("real", fieldInfo.bstrName, true))
                  pRetVal = TypeCode.Double;
                break;
            }
          }
        }
        return pRetVal;
      }
    }

    public virtual TypeNode CompilerType{ 
      get{
        return null;
      }
    }
  }


  public  class CDebugPrimitiveType : CDebugType {

    public  CDebugPrimitiveType(IDebugField field, IDebugContext context) : base(field, context) { 
    }

    public  static String GetURTName(IDebugField type) {

      String translated = null;
      FIELD_INFO fieldInfo;
      uint size = 0;
      String name = null;
      type.GetInfo(FIELD_INFO_FIELDS.FIF_NAME, out fieldInfo);
      name = fieldInfo.bstrName;
      type.GetSize(out size);

      if (0 <= size ){
        switch(size){
          case 0:
            if (0 == String.Compare("string", name, true))
              translated = "System.String";
            else if (0 == String.Compare("void", name, true))
              translated = "System.Void";
            break;
          case 1:
            if (0 == String.Compare("whole", name, true))
              translated = "System.SByte";
            else if (0 == String.Compare("uwhole", name, true))
              translated = "System.Byte";
            else if (0 == String.Compare("bool", name, true))
              translated = "System.Boolean";
            break;
          case 2:
            if (0 == String.Compare("whole", name, true))
              translated = "System.Int16";
            else if (0 == String.Compare("char", name, true))
              translated = "System.Char";
            else if (0 == String.Compare("uwhole", name, true))
              translated = "System.UInt16";
            else if (0 == String.Compare("bool", name, true))
              translated = "System.Boolean";
            break;
          case 4:
            if (0 == String.Compare("whole", name, true))
              translated = "System.Int32";
            else if (0 == String.Compare("uwhole", name, true))
              translated = "System.UIn32";
            else if (0 == String.Compare("real", name, true))
              translated = "System.Float";
            else if (0 == String.Compare("bool", name, true)) 
              translated = "System.Boolean";
            break;
          case 8:
            if (0 == String.Compare("whole", name, true))
              translated = "System.Int64";
            else if (0 == String.Compare("uwhole", name, true))
              translated = "System.UInt64";
            else if (0 == String.Compare("real", name, true))
              translated = "System.Double";
            break;
        }
      }
      return translated;
    }

    public override String Name { 
      get {
        return GetURTName(this.m_Field);
      }
    }

    public override String FullName { 
      get {
        return this.Name;
      }
    }
    public override TypeNode CompilerType{ 
      get{
        switch(this.TypeCode){
          case TypeCode.Boolean:
            return SystemTypes.Boolean;
          case TypeCode.Char:
            return SystemTypes.Char;
          case TypeCode.Int16:
            return SystemTypes.Int16;
          case TypeCode.UInt16:
            return SystemTypes.UInt32;
          case TypeCode.Int32:
            return SystemTypes.Int32;
          case TypeCode.UInt32:
            return SystemTypes.UInt32;
          case TypeCode.Int64:
            return SystemTypes.Int64;
          case TypeCode.UInt64:
            return SystemTypes.UInt64;
          case TypeCode.Double:
            return SystemTypes.Double;
          case TypeCode.Single:
            return SystemTypes.Single;
          case TypeCode.SByte:
            return SystemTypes.Int8;
          case TypeCode.Byte:
            return SystemTypes.UInt8;
          case TypeCode.String:
            return SystemTypes.String;
          default:
            return null;
        }
      }
    }
  }


  public  class CDebugNonPrimitiveType : CDebugType, IDebugNonPrimitiveType {

    protected IDebugContainerField  m_ContainerField;

    public  CDebugNonPrimitiveType(IDebugContainerField containerField, IDebugContext context) : base(containerField as IDebugField, context) {
      this.m_ContainerField = containerField;
    }

    public  IEnumSymbol GetMembers(string name, bool caseSensitive, SymbolKind kindFilter, SymbolModifiers modifierFilter) {
      IEnumSymbol pRetVal = null;
      uint fieldKindFiter = SymbolHelper.SymbolKindToFieldKind(kindFilter);
      uint fieldModFilter = SymbolHelper.SymbolModifiersToFieldModifiers((uint) modifierFilter);
      IEnumDebugFields enumFields = null;
      this.m_ContainerField.EnumFields((FIELD_KIND ) fieldKindFiter, (FIELD_MODIFIERS) fieldModFilter, name,
        caseSensitive == true ? NAME_MATCH.nmCaseSensitive : NAME_MATCH.nmCaseInsensitive, out enumFields);
      if (null != enumFields){
        pRetVal = new CEnumSymbols(enumFields, this.m_Context);
      }
      return pRetVal;
    }
    public override TypeNode CompilerType{ 
      get{
        return new DebugClassNode(new DebugEnvironment(this.m_Context), this, null);
      }
    }
  }
  public class CDebugStreamType : CDebugNonPrimitiveType, IDebugStreamType{
    protected IDebugClassField m_ClassField;
    protected StreamType m_StreamType;
    public CDebugStreamType(IDebugClassField classField, IDebugContext context, StreamType streamType) 
      : base(classField as IDebugContainerField, context){
      this.m_ClassField = classField;
      this.m_StreamType = streamType;
    }
    public override TypeKind Kind {
      get{
        return TypeKind.Stream;
      }
    }
    public override TypeNode CompilerType{ 
      get{
        int startIndex;
        string typeString = this.Name;
        IDebugType elementType;
        switch(this.m_StreamType){
          case StreamType.GenericIEnumerable:
            startIndex = typeString.LastIndexOf("IEnumerableOf");
            typeString = typeString.Substring(startIndex+13);
            elementType = this.m_Context.GetType(typeString);
            if (elementType != null)
              return SystemTypes.GenericIEnumerable.GetTemplateInstance(this.DummyReferringType, elementType.CompilerType);
            break;
          case StreamType.GenericNonEmptyIEnumerable:
            startIndex = typeString.LastIndexOf("NonEmptyIEnumerableOf");
            typeString = typeString.Substring(startIndex+21);
            elementType = this.m_Context.GetType(typeString);
            if (elementType != null)
              return SystemTypes.GenericNonEmptyIEnumerable.GetTemplateInstance(this.DummyReferringType, elementType.CompilerType);
            break;
          case StreamType.GenericNonNull:
            startIndex = typeString.LastIndexOf("NonNullOf");
            typeString = typeString.Substring(startIndex+9);
            elementType = this.m_Context.GetType(typeString);
            if (elementType != null)
              return SystemTypes.GenericNonNull.GetTemplateInstance(this.DummyReferringType, elementType.CompilerType);
            break;
          case StreamType.GenericBoxed:
            startIndex = typeString.LastIndexOf("BoxedOf");
            typeString = typeString.Substring(startIndex+7);
            elementType = this.m_Context.GetType(typeString);
            if (elementType != null)
              return SystemTypes.GenericBoxed.GetTemplateInstance(this.DummyReferringType, elementType.CompilerType);
            break;
          default:
            return null;
            
        }
        return null;
      }
    }
    private TypeNode dummyReferringType;
    private TypeNode DummyReferringType{
      get{
        if (this.dummyReferringType == null){
          this.dummyReferringType = new Class();
          this.dummyReferringType.DeclaringModule = new Module();
        }
        return this.dummyReferringType;
      }
    }
    /*public override TypeNode CompilerType{ 
      get{
        IDebugMethodField methGetEnumerator = null;
        IDebugMethodField methCurrent = null;
        IDebugField elementType;
        IDebugType streamType;
        FIELD_INFO typeInfo;
        IDebugField pFunctionField = null;
        IDebugField[] pFunctionFieldList = new IDebugField[1];
        IEnumDebugFields  pFunctionEnum = null;
        int fetched = 0;
        int count = 0;

        this.m_ClassField.EnumFields(FIELD_KIND.FIELD_KIND_ALL, FIELD_MODIFIERS.FIELD_MOD_ALL, null,
          NAME_MATCH.nmNone, out pFunctionEnum);
        pFunctionEnum.GetCount(out count);
        for(int i = 0; i < count; i++){
          pFunctionEnum.Next(1, pFunctionFieldList, out fetched);
          pFunctionField = pFunctionFieldList[0];
          pFunctionField.GetInfo(FIELD_INFO_FIELDS.FIF_ALL, out typeInfo);

          if ((typeInfo.bstrName == "GetEnumerator")){
            methGetEnumerator = pFunctionField as IDebugMethodField;
            break;
          }
        }
        if (methGetEnumerator != null){
          IDebugMethodSymbol methodSymbol = CDebugMethodSymbol.Create(methGetEnumerator, this.m_Context);
          IDebugType enumeratorType = null;

          IDebugValue[] funcParams = new IDebugValue[1];
          funcParams[0] = this.m_FieldSymbol.GetValue(null);
          IDebugValue resultValue = methodSymbol.Evaluate(this.m_FieldSymbol.GetValue(null), funcParams);
          enumeratorType = resultValue.RuntimeType();

          if (enumeratorType != null && enumeratorType is IDebugClassType){
            IDebugClassType enumeratorClass = enumeratorType as IDebugClassType;
            IEnumSymbol memberEnumerator = enumeratorClass.GetMembers("get_Current", true, SymbolKind.All, SymbolModifiers.All);
            if (memberEnumerator != null){
              while(memberEnumerator.Current != null){
                if (memberEnumerator.Current.Name.IndexOf("get_Current") >= 0){
                  methodSymbol = memberEnumerator.Current as IDebugMethodSymbol;
                  funcParams = new IDebugValue[1];
                  funcParams[0] = resultValue;
                  resultValue = methodSymbol.Evaluate(resultValue, funcParams);
                  if (resultValue.IsNullReference()) return null;
                  streamType = resultValue.RuntimeType();
                  //return SystemTypes.GenericIEnumerable.GetTemplateInstance(new Module(), streamType.CompilerType);
                  return SystemTypes.GenericNonEmptyIEnumerable.GetTemplateInstance(new Module(), streamType.CompilerType);
                }
                memberEnumerator.MoveNext();
              }
            }
          }
        }
        return null;
      }
    }*/
  }
  public class CDebugStructuralType : CDebugNonPrimitiveType, IDebugStructuralType{
    protected IDebugClassField m_ClassField;
    protected StructTypes m_StructType;
    public CDebugStructuralType(IDebugClassField classField, IDebugContext context, StructTypes sType) 
      : base(classField as IDebugContainerField, context){
      this.m_ClassField = classField;
      this.m_StructType = sType;
    }

    public override TypeKind Kind {
      get{
        return TypeKind.Tuple;
      }
    }
    public virtual StructTypes StructuralType{ 
      get{
        return this.m_StructType;
      }
    }
    public override TypeNode CompilerType{ 
      get{
        DebugEnvironment envr = new DebugEnvironment(this.m_Context);
        
        switch (this.m_StructType){
          case StructTypes.Tuple:
            FieldList list = new FieldList();
            IEnumSymbol symbols = this.GetMembers(null, true, SymbolKind.Field, SymbolModifiers.All);
            if (symbols != null){
              while(symbols.Current != null){
                Field field = new DebugFieldNode(envr, symbols.Current, new Identifier(symbols.Current.Name), null, null, 0);
                list.Add(field);
                symbols.MoveNext();
              }
            }
            Class dummy = new Class();
            dummy.DeclaringModule = new Module();
            return TupleType.For(list, dummy);
          case StructTypes.Union:
            // HACK: Need a better way for identifying return types
            return TypeUnion.For(SymbolHelper.GetTypeList(this.Name, this.m_Context), new Module());
          case StructTypes.Intersection:
            // TODO: Need to figure out Intersection Types, I think depends on figuring out return Type
            //return TypeIntersection.For(typeList, new Module());
            return null;
        }

        return null;
        
      }
    }
  }
  public class CDebugClassType : CDebugNonPrimitiveType, IDebugClassType{
    protected IDebugClassField m_ClassField;

    public  CDebugClassType(IDebugClassField classField, IDebugContext context) : base(classField as IDebugContainerField, context) {
      this.m_ClassField = classField;
    }

    public  IEnumDebugTypes GetBaseClasses() {
      IEnumDebugTypes pRetVal = null;
      IEnumDebugFields fields = null;
      this.m_ClassField.EnumBaseClasses(out fields);
      if (null != fields){
        pRetVal = new CEnumDebugTypes(fields, this.m_Context);
      }
      return pRetVal;
    }

    public  IEnumDebugTypes GetNestedClasses() {
      throw new Exception("Not Implemented Yet");
    }

    public  IEnumDebugTypes GetInterfaces() {
      throw new Exception("Not Implemented Yet");
    }

    public  bool ImplementsInterface(string interfaceName) {
      throw new Exception("Not Implemented Yet");
    }

    public  IDebugClassType GetEnclosingType() {
      throw new Exception("Not Implemented Yet");
    }
    
    public IEnumSymbol GetClosureClassMembers(IDebugValue value){
      return new CEnumClosureClassSymbols(value, this.m_Context);
    }
  }
  public class CDebugFlexArrayType : CDebugType, IDebugFlexArrayType{
    protected IDebugClassField m_ClassField;
    public CDebugFlexArrayType(IDebugClassField flexType, IDebugContext context) 
      : base(flexType as IDebugField, context){
      this.m_ClassField = flexType;
    }
    public override TypeKind Kind {
      get{
        return TypeKind.FlexArray;
      }
    }
    public override TypeNode CompilerType{
      get{
        string typeString = this.Name;
        int startIndex = typeString.LastIndexOf("IListOf");
        typeString = typeString.Substring(startIndex+7);
        IDebugType elementType = this.m_Context.GetType(typeString);
        if (elementType != null)
          return SystemTypes.GenericIList.GetTemplateInstance(this.DummyReferringType, elementType.CompilerType);
        return null;
      }
    }
    private TypeNode dummyReferringType;
    private TypeNode DummyReferringType{
      get{
        if (this.dummyReferringType == null){
          this.dummyReferringType = new Class();
          this.dummyReferringType.DeclaringModule = new Module();
        }
        return this.dummyReferringType;
      }
    }
  }
  public class CDebugArrayType : CDebugType, IDebugArrayType {
    protected IDebugArrayField  m_ArrayField;
    public  CDebugArrayType(IDebugArrayField arrayField, IDebugContext context) 
      : base(arrayField as IDebugField, context) {
      this.m_ArrayField = arrayField;
    }
    public  int Rank { 
      get {
        uint rank = 0;
        this.m_ArrayField.GetRank(out rank);
        return (int ) rank;
      }
    }
    public  IDebugType ElementType { 
      get {
        IDebugType pRetVal = null;
        IDebugField type = null;
        this.m_ArrayField.GetElementType(out type);
        pRetVal = SymbolHelper.DebugTypeFromField(type, this.m_Context);

        return pRetVal;
      }
    }
    public int NumberOfElements{
      get{
        uint count = 0;
        this.m_ArrayField.GetNumberOfElements(out count);
        return (int) count;
      }
    }

    public override String Name {
      get {
        String pRetVal = String.Empty;
        IDebugField type = null;
        this.m_ArrayField.GetElementType(out type);
        if (null != type){
          FIELD_KIND kind = 0;
          type.GetKind(out kind);
          if (0 != (kind & FIELD_KIND.FIELD_TYPE_PRIMITIVE)) {
            String translated = null;
            translated = CDebugPrimitiveType.GetURTName(type);
            if (null != translated) {
              uint rank = 0;
              this.m_ArrayField.GetRank(out rank);
              if (0 < rank) {
                pRetVal = (String ) translated.Clone();
                if (null != pRetVal) {
                  pRetVal += "[";
                  for (uint i = 0; i < rank-1; i++)
                    pRetVal += ",";
                  pRetVal += "]";
                }
              }
            }
          } else{
            FIELD_INFO fieldInfo;
            this.m_ArrayField.GetInfo(FIELD_INFO_FIELDS.FIF_NAME, out fieldInfo);
            pRetVal = fieldInfo.bstrName;
          }
        }
        return pRetVal;
      }
    }

    public override String FullName {
      get {
        return this.Name;
      }
    }
    public override TypeNode CompilerType{
      get{
        return this.ElementType.CompilerType.GetArrayType(this.Rank);
      }
    }
  }


  public  class CDebugEnumType : CDebugType, IDebugEnumType {

    protected IDebugEnumField m_EnumField;

    public  CDebugEnumType(IDebugEnumField enumField, IDebugContext context) : base(enumField as IDebugField, context) {
      this.m_EnumField = enumField;
    }

    public  IDebugType GetUnderlyingType() {
      IDebugType pRetVal = null;
      IDebugField field = null;
      this.m_EnumField.GetUnderlyingSymbol(out field);
      if (null != field){
        IDebugField type = null;
        field.GetType(out type);
        if (null != type){
          pRetVal = SymbolHelper.DebugTypeFromField(type, this.m_Context);
        }
      }
      return pRetVal;
    }

    public  string GetEnumMemberName(long val) {
      String pRetVal = null;
      this.m_EnumField.GetStringFromValue((ulong) val, out pRetVal);
      return pRetVal;
    }

    public  long GetEnumMemberValue(string name, bool caseSensitive) {
      ulong pRetVal;
      if (caseSensitive == true)
        this.m_EnumField.GetValueFromString(name, out pRetVal);
      else
        this.m_EnumField.GetValueFromStringCaseInsensitive(name, out pRetVal);

      return (long ) pRetVal;
    }
    public override TypeNode CompilerType{
      get{
        TypeNode pRetVal = new EnumNode();
        pRetVal.Name = new Identifier(this.Name);
        return pRetVal;
      }
    }
  }


  public  class CEnumDebugTypes : IEnumDebugTypes {
    
    protected IDebugContext     m_Context;
    protected IEnumDebugFields  m_Fields;
    protected IDebugType        m_Current;

    public  CEnumDebugTypes(IEnumDebugFields fields, IDebugContext context) {
      this.m_Fields = fields;
      this.m_Context = context;
      this.m_Current = null;
    }

    public  IDebugType Current { 
      get {
        IDebugType pRetVal = null;
        if (this.m_Current == null){
          IDebugField field = null;
          IDebugField[] fields = new IDebugField[1];
          int fetched = 0;
          this.m_Fields.Next(1, fields, out fetched);
          if (null != fields[0]){
            IDebugType type = null;
            field = fields[0];
            type = SymbolHelper.DebugTypeFromField(field, this.m_Context);
            if (null != type)
              this.m_Current = type;
          }
        }

        pRetVal = this.m_Current;

        return pRetVal;
      }
    }

    public  bool MoveNext() {
      
      bool pRetVal = false;

      if (null != this.m_Current) {
        this.m_Current = null;
      } else {
        // Skip the first element
        this.m_Fields.Skip(1);
      }

      IDebugField field = null;
      IDebugField[] fields = new IDebugField[1];
      int fetched = 0;
      this.m_Fields.Next(1, fields, out fetched);
      if (null != fields[0]){
        field = fields[0];
        IDebugType type = null;
        type = SymbolHelper.DebugTypeFromField(field, this.m_Context);
        if (null != type) {
          this.m_Current = type;
          pRetVal = true;
        }
      }
      return pRetVal;
    }

    public  void Reset() {

      this.m_Fields.Reset();
      if (null != this.m_Current){
        this.m_Current = null;
      }
    }

    public  int Count{ 
      get {
        int pRetVal = 0;
        this.m_Fields.GetCount(out pRetVal);
        return pRetVal;
      }
    }

    public  IEnumDebugTypes Clone() {
      IEnumDebugTypes pRetVal = null;
      IEnumDebugFields copyFields = null;
      this.m_Fields.Clone(out copyFields);
      if (null != copyFields) {
        pRetVal = new CEnumDebugTypes(copyFields, this.m_Context);
      }

      return pRetVal;
    }
  }
}