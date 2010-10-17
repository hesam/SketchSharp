//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  [Flags]
  public enum SymbolKind{
    Field = 0x1,
    Method = 0x2,
    Property = 0x4,
    Local = 0x8,
    Parameter = 0x10,
    This = 0x20,
    All = 0xff
  }
  public enum StreamType{
    GenericIEnumerable,
    GenericNonEmptyIEnumerable,
    GenericNonNull,
    GenericBoxed,
    GenericInvariant,
    GenericIList
  }
  public enum StructTypes{
    Tuple,
    Union,
    Intersection
  }
  public enum TypeKind{
    Primitive,
    Class,
    Array,
    Enum,
    InnerClass,
    All,
    Tuple,
    Stream,
    FlexArray
  }
  [Flags] 
  public enum SymbolModifiers{
    None = 0x0,
    Static = 0x1,
    Abstract = 0x2,
    Virtual = 0x4,
    Final = 0x8,
    Native = 0x10,
    Public = 0x20,
    Protected = 0x40,
    Private = 0x80,
    All = 0xff
  }
  public interface IDebugType{
    String Name{ get; }
    String FullName{ get; }
    TypeKind Kind{ get; }
    TypeCode TypeCode{ get; }
    TypeNode CompilerType{ get; }
  }
  public interface IDebugArrayType : IDebugType{
    int Rank{ get; }
    IDebugType ElementType{ get; }
    int NumberOfElements{ get; }
  }
  public interface IDebugFlexArrayType : IDebugType{
  }
  public interface IDebugNonPrimitiveType : IDebugType{
    IEnumSymbol GetMembers(string name, bool caseSensitive, SymbolKind kindFilter, SymbolModifiers modifierFilter);
  }
  public interface IDebugClassType : IDebugNonPrimitiveType{
    IEnumDebugTypes GetBaseClasses();
    IEnumDebugTypes GetNestedClasses();
    IEnumDebugTypes GetInterfaces();
    bool ImplementsInterface(string interfaceName);
    IDebugClassType GetEnclosingType();
    IEnumSymbol GetClosureClassMembers(IDebugValue value);
  }
  public interface IDebugStructuralType : IDebugNonPrimitiveType{
    StructTypes StructuralType { get; }
  }
  public interface IDebugStreamType : IDebugNonPrimitiveType{
  }
  public interface IDebugEnumType : IDebugType{
    IDebugType GetUnderlyingType();
    string GetEnumMemberName(long value);
    long GetEnumMemberValue(string name, bool caseSensitive);
  }

  public interface IEnumDebugTypes{
    IDebugType Current{ get; }
    bool MoveNext();
    void Reset();
    int Count{ get; }
    IEnumDebugTypes Clone();
  }
  public interface IDebugSymbol{
    String Name{get;}
    IDebugType Type{ get; }
    SymbolKind Kind{ get; }
    SymbolModifiers Modifiers{ get; }
    IDebugType GetContainer();
  }
  public interface IEnumSymbol{
    IDebugSymbol Current{ get; }
    bool MoveNext();
    void Reset();
    int Count{ get; }
    IEnumSymbol Clone();
  }
  public interface IDebugFieldSymbol : IDebugSymbol{
    IDebugValue GetValue(IDebugValue container); 
  }
  public interface IDebugMethodSymbol : IDebugSymbol{
    IEnumSymbol GetParameters();
    IEnumSymbol GetLocals();
    IDebugFieldSymbol GetThis();
    IDebugValue Evaluate(IDebugValue containerValue, IDebugValue[] arguments);
    IDebugClassType GetDeclaringType();
    IEnumSymbol GetStaticLocals();
  }
  public interface IDebugPropertySymbol : IDebugSymbol{
    IDebugMethodSymbol GetGetter();
    IDebugMethodSymbol GetSetter();
  }
  public interface IDebugValue{
    IDebugType RuntimeType();
    bool IsNullReference();
    int GetSize();
    byte[] GetBytes();
    Object GetValue();
    void SetValue(object val);
    void SetReferenceValue(IDebugValue val);
    bool IsEqual(IDebugValue val);
    bool IsReadOnly();
    bool IsProxy();
  }
  public interface IDebugArrayValue : IDebugValue{
    int Count{ get; }
    IDebugValue GetElement(int index);
    IEnumDebugValues GetElements();
    int Rank{ get; }
    int[] GetDimensions();
  }
  public interface IEnumDebugValues{
	IDebugValue Current{ get; }
	bool MoveNext();
	void Reset();
	int Count{ get; }
	IEnumDebugValues Clone();
  }
  [Flags]
  public enum EvaluationFlags{
    None = 0x0,
    NoSideEffects = 0x1,
    FunctionAsAddress = 0x2,
    NoFuncEval = 0x4
  }
  /*public interface IDebugContext{
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
  }*/

  public interface IExpressionEvaluator{
    IDebugExpression Parse(string expression, out string errorMessage);
    IDebugProperty GetCurrentMethodProperty(IDebugMethodSymbol methodSymbol);
    void SetLocale(int langid);
  }
  public interface IDebugExpression{
    IDebugProperty Evaluate();
    void SetValue(string expression);
  }
  [Flags]
  public enum DebugPropertyAttributes{
    None = 0x0,
    Expandable = 0x1,
    ReadOnly = 0x10,
    Error = 0x20,
    SideEffect = 0x40,
    OverloadedContainer = 0x80,
    Boolean = 0x100,
    BooleanTrue = 0x200,
    Invalid = 0x400,
  }
  public enum EnumerationKind{
    None,
    Locals,
    Arguments,
    LocalsPlusArguments,
    This
  }
  public interface IDebugProperty{
    string Name{ get; }
    string FullName{ get; }
    string Type{ get; }
    string GetValue(uint radix, uint timeout);
    DebugPropertyAttributes Attributes{ get; }
    IEnumDebugProperty EnumChildren(EnumerationKind kind, int radix, int timeout, bool allowFuncEval);
    IDebugProperty Parent{ get; }
    void SetValue(string expr, uint radix, uint timeout);
  }
  public interface IEnumDebugProperty{
    IDebugProperty Current{ get; }
    bool MoveNext();
    void Reset();
    int Count{ get; }
    IEnumDebugProperty Clone();
  }
  public abstract class BaseProperty : IDebugProperty{
    protected string name;
    protected IDebugType staticType;
    protected IDebugProperty parent;
    protected IDebugValue value;
    protected CommonExpressionEvaluator evaluator;
    public BaseProperty(string name, IDebugType staticType, IDebugValue value, IDebugProperty parent, CommonExpressionEvaluator evaluator){
      this.name = name;
      this.staticType = staticType;
      this.parent = parent;
      this.value = value;
      this.evaluator = evaluator;
    }
    public virtual string Name{
      get{ return this.name; }
    }
    public virtual string FullName{
      get{ return this.name; }
    }
    public virtual string Type{
      get{ 
        return this.evaluator.ExprErrorHandler.GetTypeName(this.staticType.CompilerType);
      }
    }
    public virtual string GetValue(uint radix, uint timeout){
      return null;
    }
    public virtual DebugPropertyAttributes Attributes{
      get{ return DebugPropertyAttributes.None;  }
    }
    public virtual IDebugProperty Parent{
      get{ return this.parent; }
    }
    public virtual IEnumDebugProperty EnumChildren(EnumerationKind kind, int radix, int timeout, bool allowFuncEval){
      return null;
    }
    public virtual void SetValue(string expr, uint radix, uint timeout){
      IDebugType type = null;
      if (this.value != null && !this.value.IsNullReference()) {
        type = this.value.RuntimeType();
        if (type.Kind == TypeKind.Primitive){
          switch(type.TypeCode){
            case TypeCode.Boolean:
              if (String.Compare(expr, "true", true) == 0)
                this.value.SetValue(true);
              else if (String.Compare(expr, "false", true) == 0)
                this.value.SetValue(false);
              break;
            case TypeCode.Byte:
              this.value.SetValue(System.Byte.Parse(expr));
              break;
            case TypeCode.Char:
              this.value.SetValue(System.Char.Parse(expr));
              break;
            case TypeCode.Decimal:
              this.value.SetValue(System.Decimal.Parse(expr));
              break;
            case TypeCode.Double:
              this.value.SetValue(System.Double.Parse(expr));
              break;
            case TypeCode.Int16:
              this.value.SetValue(System.Int16.Parse(expr));
              break;
            case TypeCode.Int32:
              this.value.SetValue(System.Int32.Parse(expr));
              break;
            case TypeCode.SByte:
              this.value.SetValue(System.SByte.Parse(expr));
              break;
            case TypeCode.Single:
              this.value.SetValue(System.Single.Parse(expr));
              break;
            case TypeCode.String:
              this.value.SetValue(expr);
              break;
            case TypeCode.UInt16:
              this.value.SetValue(System.UInt16.Parse(expr));
              break;
            case TypeCode.UInt32:
              this.value.SetValue(System.UInt32.Parse(expr));
              break;
            case TypeCode.UInt64:
              this.value.SetValue(System.UInt64.Parse(expr));
              break;
          }
        }
      }
    }
  }

  public class ErrorProperty : BaseProperty{
    protected string message;
    public ErrorProperty(string name, string message)
      : base(name, null, null, null, null){
      this.name = name;
      this.message = message;
    }
    public override string Name{
      get{ return this.name; }
    }
    public override string FullName{
      get{ return this.name; }
    }
    public override string Type{
      get{ return ""; }
    }
    public override string GetValue(uint radix, uint timeout){
      return this.message;
    }
    public override DebugPropertyAttributes Attributes{
      get{ return DebugPropertyAttributes.None;  }
    }
    public override IDebugProperty Parent{
      get{ return null; }
    }
    public override IEnumDebugProperty EnumChildren(EnumerationKind kind, int radix, int timeout, bool allowFuncEval){
      return null;
    }
    public override void SetValue(string expr, uint radix, uint timeout){
    }
  }

  public class MethodProperty : IDebugProperty{
    protected IDebugMethodSymbol method;
    protected CommonExpressionEvaluator evaluator;
    public MethodProperty(IDebugMethodSymbol method, CommonExpressionEvaluator evaluator){
      this.method = method;
      this.evaluator = evaluator;
    }
    public virtual string Name{
      get{ return this.method.Name; }
    }
    public virtual string FullName{
      get{
        return this.method.GetContainer().FullName + this.method.Name;
      }
    }
    public virtual string Type{
      get{ return this.method.GetContainer().FullName; }
    }
    public virtual string GetValue(uint radix, uint timeout){
      return null;
    }
    public virtual void SetValue(string expr, uint radix, uint timeout){
      throw new NotImplementedException();
    }
    public virtual DebugPropertyAttributes Attributes{
      get{ return DebugPropertyAttributes.Expandable;  }
    }
    public virtual IDebugProperty Parent{
      get{ return null; }
    }
    private IEnumDebugProperty MakeEnumDebugProperty(IEnumSymbol enumSymbols){
      return new EnumDebugPropertySymbols(enumSymbols, null, null, this.evaluator);
    }
    public virtual IEnumDebugProperty EnumChildren(EnumerationKind kind, int radix, int timeout, bool allowFuncEval){
      IEnumSymbol enumSymbols = null;
      if (kind == EnumerationKind.Locals)
        enumSymbols = this.method.GetLocals();
      else if (kind == EnumerationKind.Arguments)
        enumSymbols = this.method.GetParameters();
      else if (kind == EnumerationKind.LocalsPlusArguments){
        IEnumSymbol thisEnum = new EnumSingleSymbol(this.method.GetThis());
        IEnumSymbol localsEnum = this.method.GetLocals();
        IEnumSymbol paramsEnum = this.method.GetParameters();
        return new AggregateEnumDebugProperty(this.MakeEnumDebugProperty(thisEnum),
          this.MakeEnumDebugProperty(paramsEnum), this.MakeEnumDebugProperty(localsEnum));
      }
      else if (kind == EnumerationKind.This)
        enumSymbols = new EnumSingleSymbol(this.method.GetThis());
      return this.MakeEnumDebugProperty(enumSymbols);
    }
  }
  public class PrimitiveTypeProperty : BaseProperty{
    public PrimitiveTypeProperty(string name, IDebugType staticType, IDebugValue value, IDebugProperty parent, CommonExpressionEvaluator evaluator)
      : base(name, staticType, value, parent, evaluator){
    }
    public override string GetValue(uint radix, uint timeout){
      if (this.value == null || this.value.IsNullReference())
        return "null";
      object v = this.value.GetValue();
      if (v != null){
        if (v is string){
          string str = v as string;
          str = str.Insert(0, "\"");
          str = str.Insert(str.Length - 1, "\"");
          return str;
        }
        else
          return v.ToString();
      }
      return "null";
    }
  }
  public class StreamProperty : BaseProperty{
    public StreamProperty(string name, IDebugType staticType, IDebugValue value, IDebugProperty parent, CommonExpressionEvaluator evaluator)
      : base(name, staticType, value, parent, evaluator){
    }
    public override string GetValue(uint radix, uint timeout){
      if (this.value == null || this.value.IsNullReference())
        return "null";
      else
        return "{" + this.Type + "}";
    }
    public override IEnumDebugProperty EnumChildren(EnumerationKind kind, int radix, int timeout, bool allowFuncEval){
      if (this.value == null || this.value.IsNullReference())
        return null;
      IDebugType typ = this.value.RuntimeType();
      IDebugClassType classType = null;
      IDebugStreamType streamType = null;
      if ((classType = typ as IDebugClassType) != null){
        //IEnumSymbol enumSymbol = new CEnumClosureClassSymbols(this.value, this.context);
        IEnumSymbol enumSymbol = classType.GetClosureClassMembers(this.value);
        //IEnumSymbol enumSymbol = classType.GetMembers(null, true, SymbolKind.Field|SymbolKind.Property, SymbolModifiers.All);
        return new EnumDebugPropertySymbols(enumSymbol, this, this.value, this.evaluator);
      }
      else if ((streamType = typ as IDebugStreamType) != null){
        //IDebugStreamType classType = (IDebugStreamType)this.value.RuntimeType();
        IEnumSymbol enumSymbol = streamType.GetMembers(null, true, SymbolKind.Field|SymbolKind.Property, SymbolModifiers.All);
        return new EnumDebugPropertySymbols(enumSymbol, this, this.value, this.evaluator);
      }
      else
        return null;
      
    }
    public override DebugPropertyAttributes Attributes{
      get { 
        return (this.value == null || this.value.IsNullReference()) ? DebugPropertyAttributes.None : DebugPropertyAttributes.Expandable;  }
    }
  }
  public class StructuralProperty : BaseProperty{
    protected IDebugStructuralType structuralType;
    public StructuralProperty(string name, IDebugType staticType, IDebugValue value, IDebugProperty parent, CommonExpressionEvaluator evaluator)
      : base(name, staticType, value, parent, evaluator){
      if (this.value != null)
        this.structuralType = this.value.RuntimeType() as IDebugStructuralType;
      else
        this.structuralType = staticType as IDebugStructuralType;
    }
    public override string GetValue(uint radix, uint timeout){
      if (this.value == null || this.value.IsNullReference())
        return "null";
      else{
        TypeNode typ = this.structuralType.CompilerType;
        return "{" + this.evaluator.ExprErrorHandler.GetTypeName(typ) + "}";
      }
    }
    public override IEnumDebugProperty EnumChildren(EnumerationKind kind, int radix, int timeout, bool allowFuncEval){
      if (this.value == null || this.value.IsNullReference())
        return null;
      if (this.structuralType.StructuralType == StructTypes.Tuple){
        IDebugStructuralType classType = (IDebugStructuralType)this.value.RuntimeType();
        IEnumSymbol enumSymbol = classType.GetMembers(null, true, SymbolKind.Field|SymbolKind.Property, SymbolModifiers.All);
        return new EnumDebugPropertySymbols(enumSymbol, this, this.value, this.evaluator);
      }
      else if (this.structuralType.StructuralType == StructTypes.Union){
        // TODO: Handle Union Types, Properly
        IDebugStructuralType structType = (IDebugStructuralType)this.value.RuntimeType();
        IEnumSymbol enumSymbol = structType.GetMembers(null, true, SymbolKind.Field|SymbolKind.Property, SymbolModifiers.All);
        return new EnumDebugPropertySymbols(enumSymbol, this, this.value, this.evaluator);
      }
      else if (this.structuralType.StructuralType == StructTypes.Intersection){
        // TODO: Handle Intersection Types, Properly
        return null;
      }
      return null;
    }
    public override DebugPropertyAttributes Attributes{
      get { 
        return (this.value == null || this.value.IsNullReference()) ? DebugPropertyAttributes.None : DebugPropertyAttributes.Expandable;  }
    }
  }
  public class ClassProperty : BaseProperty{
    public ClassProperty(string name, IDebugType staticType, IDebugValue value, IDebugProperty parent, CommonExpressionEvaluator evaluator)
      : base(name, staticType, value, parent, evaluator){
    }
    public override string GetValue(uint radix, uint timeout){
      if (this.value == null || this.value.IsNullReference())
        return "null";
      else
        return "{" + this.value.RuntimeType().FullName + "}";
    }
    public override IEnumDebugProperty EnumChildren(EnumerationKind kind, int radix, int timeout, bool allowFuncEval){
      if (this.value == null || this.value.IsNullReference())
        return null;
      IDebugClassType classType = (IDebugClassType)this.value.RuntimeType();
      if (classType.Name.IndexOf("closure") >= 0){
        IEnumSymbol closureSymbol = classType.GetClosureClassMembers(this.value);
        return new EnumDebugPropertySymbols(closureSymbol, this, this.value, this.evaluator);
      }
      else{
        IEnumSymbol enumSymbol = classType.GetMembers(null, true, SymbolKind.Field|SymbolKind.Property, SymbolModifiers.All);
        IEnumDebugTypes enumBaseClasses = classType.GetBaseClasses();
        return new AggregateEnumDebugProperty(
          new EnumDebugPropertyTypes(enumBaseClasses, this, this.value, this.evaluator),
          new EnumDebugPropertySymbols(enumSymbol, this, this.value, this.evaluator));
      }
    }
    public override DebugPropertyAttributes Attributes{
      get { 
        return (this.value == null || this.value.IsNullReference()) ? DebugPropertyAttributes.None : DebugPropertyAttributes.Expandable;  }
    }
  }
  public class FlexArrayProperty : BaseProperty{
    public FlexArrayProperty(string name, IDebugType staticType, IDebugValue value, IDebugProperty parent, CommonExpressionEvaluator evaluator)
      : base(name, staticType, value, parent, evaluator){
    }
    public override string GetValue(uint radix, uint timeout){
      if (this.value == null || this.value.IsNullReference())
        return "null";
      else{
        IDebugClassType classType = (IDebugClassType)this.value.RuntimeType();
        IDebugFieldSymbol countSymbol;
        IDebugValue count = null;
        IEnumSymbol enumCountSymbol = classType.GetMembers("count", true, SymbolKind.Field, SymbolModifiers.All);
        if ((countSymbol = enumCountSymbol.Current as IDebugFieldSymbol) != null){
           count = countSymbol.GetValue(this.value);
        }
        return "{length = " + count.GetValue().ToString() + "}";
      }
    }
    public override IEnumDebugProperty EnumChildren(EnumerationKind kind, int radix, int timeout, bool allowFuncEval){
      if (this.value == null || this.value.IsNullReference())
        return null;
      IDebugClassType classType = (IDebugClassType)this.value.RuntimeType();
      IEnumSymbol enumCountSymbol = classType.GetMembers("Count", true, SymbolKind.Property, SymbolModifiers.All);
      IEnumSymbol enumElementsSymbol = classType.GetMembers("elements", true, SymbolKind.Field, SymbolModifiers.All);
      return new AggregateEnumDebugProperty(
        new EnumDebugPropertySymbols(enumCountSymbol, this, this.value, this.evaluator),
        new EnumDebugPropertySymbols(enumElementsSymbol, this, this.value, this.evaluator));
    }
    public override DebugPropertyAttributes Attributes{
      get { 
        return (this.value == null || this.value.IsNullReference()) ? DebugPropertyAttributes.None : DebugPropertyAttributes.Expandable;  }
    }
  }

  public class ArrayProperty : BaseProperty{
    IDebugArrayValue arrayValue;
    public ArrayProperty(string name, IDebugType staticType, IDebugValue containerValue, IDebugProperty parent, CommonExpressionEvaluator evaluator)
      : base(name, staticType, containerValue, parent, evaluator){
      arrayValue = (IDebugArrayValue)this.value;
    }
    public override string GetValue(uint radix, uint timeout){
      if (this.value == null || this.value.IsNullReference())
        return "null";
      else
        return "{length = " + this.arrayValue.Count.ToString() + "}";
    }
    public override IEnumDebugProperty EnumChildren(EnumerationKind kind, int radix, int timeout, bool allowFuncEval){
      IDebugArrayType arrayType = this.staticType as IDebugArrayType;
      if (arrayType == null)
        arrayType = this.value.RuntimeType() as IDebugArrayType;
      return new EnumArrayIndices(this.arrayValue.GetElements(), arrayType.ElementType, this, this.value, this.evaluator);
    }
    public override DebugPropertyAttributes Attributes{
      get{ return (this.value == null || this.value.IsNullReference()) ? DebugPropertyAttributes.None : DebugPropertyAttributes.Expandable;  }
    }
  }

  public class EnumProperty : BaseProperty{
    IDebugEnumType enumType;
    public EnumProperty(string name, IDebugType staticType, IDebugValue value, IDebugProperty parent, CommonExpressionEvaluator evaluator)
      : base(name, staticType, value, parent, evaluator){
      this.enumType = value.RuntimeType() as IDebugEnumType;
      if (this.enumType == null)
        this.enumType = staticType as IDebugEnumType;
    }
    public override string GetValue(uint radix, uint timeout){
      object v = this.value.GetValue();
      long l = long.Parse(v.ToString());
      string enumName = this.enumType.GetEnumMemberName(l);
      if (enumName == null)
        return v.ToString();
      else
        return enumName;
    }
  }

  public class EnumDebugPropertySymbols : IEnumDebugProperty{
    private IEnumSymbol enumSymbols;
    private IDebugProperty parent;
    private IDebugValue containerValue;
    private CommonExpressionEvaluator evaluator;
    internal EnumDebugPropertySymbols(IEnumSymbol enumSymbols, IDebugProperty parent, IDebugValue containerValue, CommonExpressionEvaluator evaluator){
      this.enumSymbols = enumSymbols;
      this.parent = parent;
      this.containerValue = containerValue;
      this.evaluator = evaluator;
    }
    public IDebugProperty Current{
      get{
        IDebugSymbol sym = enumSymbols.Current;
        IDebugFieldSymbol fieldSymbol = sym as IDebugFieldSymbol;
        if (null != fieldSymbol)
          return this.evaluator.MakeProperty(fieldSymbol, this.parent, this.containerValue);
        else{
          IDebugPropertySymbol propertySymbol = sym as IDebugPropertySymbol;
          Debug.Assert(propertySymbol != null, "Unknown symbol type");
          return this.evaluator.MakeProperty(propertySymbol, this.parent, this.containerValue);
        }
      }
    }
    public bool MoveNext(){
      if (this.enumSymbols == null)
        return false;
      else
        return this.enumSymbols.MoveNext();
    }
    public void Reset(){
      if (this.enumSymbols != null)
        this.enumSymbols.Reset(); 
    }
    public int Count{
      get{
        if (this.enumSymbols == null)
          return 0;
        else
          return this.enumSymbols.Count;
      }
    }
    public IEnumDebugProperty Clone(){
      return new EnumDebugPropertySymbols(this.enumSymbols.Clone(), this.parent, this.containerValue, this.evaluator);
    }
  }
  public class AggregateEnumDebugProperty : IEnumDebugProperty{
    IEnumDebugProperty[] properties;
    int current;
    internal AggregateEnumDebugProperty(params IEnumDebugProperty[] properties){
      this.properties = properties;
      this.current = 0;
      while (this.current < properties.Length && this.properties[this.current].Count == 0)
        this.current++;
    }
    public IDebugProperty Current{
      get{
        if (current >= this.properties.Length)
          return null;
        return this.properties[this.current].Current;
      }
    }
    public bool MoveNext(){
      if (this.current >= this.properties.Length)
        return false;
      if (this.properties[this.current].MoveNext())
        return true;
      do{
        this.current++;
      }while (this.current < this.properties.Length && this.properties[this.current].Count == 0);
      return this.current < this.properties.Length;
    }
    public void Reset(){
      foreach (IEnumDebugProperty e in this.properties)
        e.Reset();
      this.current = 0;
      while (this.current < properties.Length && this.properties[this.current].Count == 0)
        this.current++;
    }
    public int Count{
      get{
        int count = 0;
        foreach (IEnumDebugProperty e in this.properties)
          count += e.Count;
        return count;
      }
    }
    public IEnumDebugProperty Clone(){
      return new AggregateEnumDebugProperty((IEnumDebugProperty[])this.properties.Clone());
    }
  }
  public class TypeProperty : IDebugProperty{
    protected IDebugType type;
    protected IDebugProperty parent;
    IDebugValue containerValue;
    private string name;
    private CommonExpressionEvaluator evaluator;
    public TypeProperty(IDebugType type, IDebugProperty parent, IDebugValue containerValue, CommonExpressionEvaluator evaluator){
      this.type = type;
      this.parent = parent;
      this.name = this.type.FullName;
      this.containerValue = containerValue;
      this.evaluator = evaluator;
    }
    public virtual string Name{
      get{ return this.name; }
    }
    public virtual string FullName{
      get{ return this.name; }
    }
    public virtual string Type{
      get{ return this.name; }
    }
    public virtual string GetValue(uint radix, uint timeout){
      return "";
    }
    public virtual DebugPropertyAttributes Attributes{
      get{
        TypeKind kind = this.type.Kind;
        switch(kind){
          case TypeKind.Class:
          case TypeKind.InnerClass:
            return DebugPropertyAttributes.Expandable;
         default:
            return DebugPropertyAttributes.None;
        }
      }
    }
    public virtual IDebugProperty Parent{
      get{ return this.parent; }
    }
    public virtual IEnumDebugProperty EnumChildren(EnumerationKind kind, int radix, int timeout, bool allowFuncEval){
      IDebugClassType classType = this.type as IDebugClassType;
      if (classType != null){
        IEnumDebugTypes enumBaseClasses = classType.GetBaseClasses();
        IEnumSymbol enumMembers = classType.GetMembers(null, true, SymbolKind.Field, SymbolModifiers.All);
        return new AggregateEnumDebugProperty(
          new EnumDebugPropertySymbols(enumMembers,this, this.containerValue, this.evaluator), 
          new EnumDebugPropertyTypes(enumBaseClasses, this, containerValue, this.evaluator));
      }
      return null;
    }
    public virtual void SetValue(string expr, uint radix, uint timeout){
    }
  }
  public class EnumDebugPropertyTypes : IEnumDebugProperty{
    IEnumDebugTypes enumTypes;
    IDebugProperty parent;
    IDebugValue containerValue;
    CommonExpressionEvaluator evaluator;
    internal EnumDebugPropertyTypes(IEnumDebugTypes enumTypes, IDebugProperty parent, IDebugValue containerValue, CommonExpressionEvaluator evaluator){
      this.enumTypes = enumTypes;
      this.parent = parent;
      this.containerValue = containerValue;
      this.evaluator = evaluator;
    }
    public IDebugProperty Current{
      get{
        IDebugType type = this.enumTypes.Current;
        return new TypeProperty(type, this.parent, this.containerValue, this.evaluator);
      }
    }
    public bool MoveNext(){
      if (this.enumTypes == null)
        return false;
      else
        return this.enumTypes.MoveNext();
    }
    public void Reset(){
      if (this.enumTypes != null)
        this.enumTypes.Reset(); 
    }
    public int Count{
      get{
        if (this.enumTypes == null)
          return 0;
        else
          return this.enumTypes.Count;
      }
    }
    public IEnumDebugProperty Clone(){
      return new EnumDebugPropertyTypes(this.enumTypes.Clone(), this.parent, this.containerValue, this.evaluator);
    }
  }
  public class EnumSingleSymbol : IEnumSymbol{
    private IDebugSymbol symbol;
    private bool atEnd;
    public EnumSingleSymbol(IDebugSymbol symbol){
      this.symbol = symbol;
      this.atEnd = false;
    }
    public IDebugSymbol Current{
      get{
        if (this.atEnd)
          return null;
        else
          return this.symbol;
      }
    }
    public bool MoveNext(){
      this.atEnd = true;
      return false;
    }
    public void Reset(){
      this.atEnd = false;
    }
    public int Count{
      get{ return this.symbol == null ? 0 : 1; }
    }
    public IEnumSymbol Clone(){
      return new EnumSingleSymbol(this.symbol);
    }
  }
  public class EnumArrayIndices : IEnumDebugProperty{
    IEnumDebugValues enumValues;
    IDebugProperty parent;
    IDebugValue containerValue;
    int index;
    IDebugType elementType;
    CommonExpressionEvaluator evaluator;
    internal EnumArrayIndices(IEnumDebugValues enumValues, IDebugType elementType, IDebugProperty parent, IDebugValue containerValue, CommonExpressionEvaluator evaluator){
      this.enumValues = enumValues;
      this.elementType = elementType;
      this.parent = parent;
      this.containerValue = containerValue;
      this.index = 0;
      this.evaluator = evaluator;
    }
    public IDebugProperty Current{
      get{
        if (this.enumValues == null)
          return null;
        IDebugValue value = this.enumValues.Current;
        return this.evaluator.MakeProperty("[" + this.index.ToString() + "]", this.elementType, value,  this.parent);
      }
    }
    public bool MoveNext(){
      if (this.enumValues == null)
        return false;
      else{
        this.index++;
        return this.enumValues.MoveNext();
      }
    }
    public void Reset(){
      if (this.enumValues != null)
        this.enumValues.Reset(); 
      this.index = 0;
    }
    public int Count{
      get{
        if (this.enumValues == null)
          return 0;
        else
          return this.enumValues.Count;
      }
    }
    public IEnumDebugProperty Clone(){
      return new EnumArrayIndices(this.enumValues.Clone(), this.elementType, this.parent, this.containerValue, this.evaluator);
    }
  }
  
  public class CommonExpressionEvaluator : IExpressionEvaluator {

    public Compiler ExprCompiler;
    public Evaluator ExprEvaluator;
    public ErrorHandler ExprErrorHandler;
    public CommonExpressionEvaluator() {
      this.ExprEvaluator = new Evaluator();
      this.ExprErrorHandler = new ErrorHandler(new ErrorNodeList());
    }

    //IExpressionEvaluator
    public virtual IDebugExpression Parse(string expression, out string errorMessage){
      errorMessage = null;
      throw new NotImplementedException();
    }
    public virtual IDebugProperty GetCurrentMethodProperty(IDebugMethodSymbol methodSymbol){
      return new MethodProperty(methodSymbol, this);
    }
    public virtual void SetLocale(int langid){
    }

    // Override these factory methods to create language specfic IDebugProperty implementations
    public virtual BaseProperty MakeProperty(string name, IDebugType staticType, IDebugValue value, IDebugProperty parent){
      IDebugType type = null;
      if (value == null || value.IsNullReference() || staticType.Kind == TypeKind.Stream 
        || staticType.Kind == TypeKind.FlexArray || staticType.Kind == TypeKind.Enum)
        type = staticType;
      else
        type = value.RuntimeType();

      switch (type.Kind){
        case TypeKind.Primitive:
          return MakePrimitiveTypeProperty(name, staticType, value, parent);
        case TypeKind.InnerClass:
        case TypeKind.Class:
          return MakeClassProperty(name, staticType, value, parent);
        case TypeKind.FlexArray:
          return MakeFlexArrayProperty(name, staticType, value, parent);
        case TypeKind.Tuple:
          return MakeStructuralProperty(name, staticType, value, parent);
        case TypeKind.Stream:
          return MakeStreamProperty(name, staticType, value, parent);
        case TypeKind.Array:
          return MakeArrayProperty(name, staticType, value, parent);
        case TypeKind.Enum:
          return MakeEnumProperty(name, staticType, value, parent);
      }
      return null;
    }

    public virtual BaseProperty MakePrimitiveTypeProperty(string name, IDebugType staticType, IDebugValue value, IDebugProperty parent){
      return new PrimitiveTypeProperty(name, staticType, value, parent, this);
    }
    public virtual BaseProperty MakeClassProperty(string name, IDebugType staticType, IDebugValue value, IDebugProperty parent){
      return new ClassProperty(name, staticType, value, parent, this);
    }
    public virtual BaseProperty MakeStructuralProperty(string name, IDebugType staticType, IDebugValue value, IDebugProperty parent){
      return new StructuralProperty(name, staticType, value, parent, this);
    }
    public virtual BaseProperty MakeStreamProperty(string name, IDebugType staticType, IDebugValue value, IDebugProperty parent){
      return new StreamProperty(name, staticType, value, parent, this);
    }
    public virtual BaseProperty MakeArrayProperty(string name, IDebugType staticType, IDebugValue value, IDebugProperty parent){
      return new ArrayProperty(name, staticType, value, parent, this);
    }
    public virtual BaseProperty MakeEnumProperty(string name, IDebugType staticType, IDebugValue value, IDebugProperty parent){
      return new EnumProperty(name, staticType, value, parent, this);
    }
    public virtual BaseProperty MakeFlexArrayProperty(string name, IDebugType staticType, IDebugValue value, IDebugProperty parent){
      return new FlexArrayProperty(name, staticType, value, parent, this);
    }

    public virtual BaseProperty MakeProperty(IDebugFieldSymbol symbol, IDebugProperty parent, IDebugValue containerValue){
      IDebugType staticType = symbol.Type;
      IDebugValue value = symbol.GetValue(containerValue);
      if (symbol.Name == "this value: ")
        return this.MakeProperty("this", symbol.Type, value, parent);
      return this.MakeProperty(symbol.Name, symbol.Type, value, parent);
    }
    public virtual BaseProperty MakeProperty(IDebugPropertySymbol symbol, IDebugProperty parent, IDebugValue containerValue){
      IDebugMethodSymbol getter = symbol.GetGetter();
      IDebugValue value = null;
      if (getter != null /*&& (context.flags & EvaluationFlags.NoFuncEval) == 0*/){
        IEnumSymbol parameters = getter.GetParameters();
        if (parameters == null || parameters.Count == 0){
          IDebugValue[] arguments = null;
          if ((getter.Modifiers & SymbolModifiers.Static) == 0)
            arguments = new IDebugValue[]{containerValue};
          else
            arguments = new IDebugValue[0];
          value = getter.Evaluate(containerValue, arguments);
        }
      }
      return this.MakeProperty(symbol.Name, symbol.Type, value, parent);
    }
  }
}
