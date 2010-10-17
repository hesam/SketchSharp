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
namespace Microsoft.VisualStudio.IntegrationHelper{
  public interface IDebugInfo {
    IDebugType  GetDebugType {
      get; 
    }
    IDebugValue GetDebugValue{
      get;
    }
  }
  public class DebugMethod : Method{
    public DebugEnvironment debugEnv;
    public CDebugMethodSymbol methodSymbol;
    public DebugMethod(DebugEnvironment envr, CDebugMethodSymbol method, DebugMethodScope scope){
      this.debugEnv = envr;
      this.methodSymbol = method;
      this.DeclaringType = method.GetDeclaringType().CompilerType;
      SymbolModifiers modifier = this.methodSymbol.Modifiers;
      if ((modifier & SymbolModifiers.Abstract) != 0)
        this.Flags |= MethodFlags.Abstract;
      if ((modifier & SymbolModifiers.Final) != 0)
        this.Flags |= MethodFlags.Final;
      if ((modifier & SymbolModifiers.Private) != 0)
        this.Flags |= MethodFlags.Private;
      if ((modifier & SymbolModifiers.Public) != 0)
        this.Flags |= MethodFlags.Public;
      if ((modifier & SymbolModifiers.Static) != 0)
        this.Flags |= MethodFlags.Static;

      this.Scope = scope;
      if (this.methodSymbol != null){
        IDebugFieldSymbol thisSymbol = this.methodSymbol.GetThis();
        if (thisSymbol != null)
          this.ThisParameter = new This(new DebugClassNode(this.debugEnv, thisSymbol.Type, thisSymbol.GetValue(null)));
        ParameterList pList = new ParameterList();
        IEnumSymbol param = methodSymbol.GetParameters();
        if (param != null){
          for (int i=1; ; i++){
            if (param.Current == null) break;
            ParameterField paramField = new DebugParameterField(this.debugEnv, param.Current, new Identifier(param.Current.Name), null, scope);
            paramField.DeclaringType = scope;
            pList[i] = new Parameter(paramField.Name, paramField.Type);
            pList[i].ArgumentListIndex = i;
            param.MoveNext();
          }
        }
        this.Parameters = pList;
      }
    }
    public override Local GetLocalForField(Field f){
      DebugFieldNode debugField = f as DebugFieldNode;
      if (debugField != null){
        Local loc = (Local)this.Locals[debugField.index+1];
        if (loc == null) 
          this.Locals[debugField.index+1] = loc = new Local(debugField.Name, debugField.Type);
        loc.Index = debugField.index;
        return loc;
      }
      return null;
    }
  }
  public class DebugTypeScope : TypeScope{
    public DebugEnvironment debugEnv;
    public DebugTypeScope(DebugEnvironment envr, Class parentScope, TypeNode type) 
      :base(null, type){
      this.debugEnv = envr;
      this.DeclaringModule = new Module();
      this.BaseClass = type.BaseType as Class;
    }
  }

  public class DebugMethodScope : MethodScope{
    public DebugEnvironment debugEnv;
    public CDebugMethodSymbol methodSymbol;
    public DebugMethodScope(DebugEnvironment envr, CDebugMethodSymbol method){
      this.debugEnv = envr;
      if (method != null){
        this.methodSymbol = method;
        IDebugFieldSymbol thisSymbol = method.GetThis();
        if (thisSymbol != null){
          this.ThisType = new DebugClassNode(this.debugEnv, thisSymbol.Type, thisSymbol.GetValue(null));
          this.BaseClass = new DebugTypeScope(this.debugEnv, null, this.ThisType);
        }
        else {
          IDebugClassType classType = method.GetDeclaringType();
          if (classType != null){
            Class declaringType = new DebugClassNode(this.debugEnv, classType, null);
            this.BaseClass = new DebugTypeScope(this.debugEnv, null, declaringType);
          }
        }
        this.DeclaringMethod = new DebugMethod(this.debugEnv, this.methodSymbol, this);
      }
    }
    public override MemberList GetMembersNamed(Identifier name){
      MemberList returnList = new MemberList();
      IDebugSymbol container = this.debugEnv.context.GetContainer();

      CDebugMethodSymbol methodSymbol = null;
      if ( (methodSymbol = container as CDebugMethodSymbol) != null ){
        if (name.Name == "this")
          returnList.Add(new DebugFieldNode(this.debugEnv, methodSymbol.GetThis(), name, null, null, 0));
        else {
          IEnumSymbol param = methodSymbol.GetParameters();
          if (param != null){
            for (int i=1; ; i++){
              if (param.Current == null) break;
              if (param.Current.Name == name.Name){
                DebugParameterField paramField = new DebugParameterField(this.debugEnv, param.Current, name, null, this);
                paramField.DeclaringType = this;
                paramField.Parameter = this.DeclaringMethod.Parameters[i];
                returnList.Add(paramField);
                break;
              }
              param.MoveNext();
            }
          }
        }
      }
      return returnList;
    }
  }

  public class DebugBlockScope : BlockScope {
    public DebugEnvironment debugEnv;

    public DebugBlockScope(DebugEnvironment envr) {
      this.debugEnv = envr;
      IDebugSymbol container = this.debugEnv.context.GetContainer();
      this.BaseClass = new DebugMethodScope(this.debugEnv, container as CDebugMethodSymbol);
    }

    public override MemberList GetMembersNamed(Identifier name){
      MemberList returnList = new MemberList();
      IDebugSymbol container = this.debugEnv.context.GetContainer();

      CDebugMethodSymbol methodSymbol = null;
      if ( (methodSymbol = container as CDebugMethodSymbol) != null ) {
        if (name.Name == "this") {
          returnList.Add(new DebugFieldNode(this.debugEnv, methodSymbol.GetThis(), name, null, null, 0));
        }
        else {
          IEnumSymbol locals = methodSymbol.GetLocals();
          if (locals != null){
            for (int i=0; ; i++){
              if (locals.Current == null) break;
              if (locals.Current.Name == name.Name) {
                Field localField = new DebugFieldNode(this.debugEnv, locals.Current, name, null, this, i);
                localField.DeclaringType = this;
                returnList.Add(localField);
                break;
              }
              locals.MoveNext();
            }
          }
        }
      }
      return returnList;
    }
  }
  public class DebugParameterField : ParameterField, IDebugInfo{
    public DebugEnvironment debugEnv;
    public IDebugSymbol  Symbol;
    public IDebugValue   Container;
    public DebugParameterField(DebugEnvironment envr, IDebugSymbol symbol, Identifier name, IDebugValue container, TypeNode declaringType) {
      this.debugEnv = envr;
      this.Symbol  = symbol;
      this.Container = container;
      this.Name = name;
      this.DeclaringType = declaringType;
      switch(symbol.Type.Kind) {
        case TypeKind.Class:
          this.Type = new DebugClassNode(this.debugEnv, this.Symbol.Type, ((IDebugFieldSymbol ) symbol).GetValue(Container));
          break;
        case TypeKind.Stream:
          this.Type = symbol.Type.CompilerType;
          break;
        case TypeKind.Tuple:
          StructTypes sType = ((IDebugStructuralType) this.Symbol.Type).StructuralType;
        switch (sType){
          case StructTypes.Tuple:
            FieldList list = new FieldList();
            IEnumSymbol symbols = ((IDebugStructuralType) this.Symbol.Type).GetMembers(null, true, SymbolKind.Field, SymbolModifiers.All);
            if (symbols != null){
              while(symbols.Current != null){
                Field fieldMember = new DebugFieldNode(this.debugEnv, symbols.Current, new Identifier(symbols.Current.Name), ((IDebugFieldSymbol ) symbol).GetValue(Container), null, 0);
                SymbolModifiers modifier = symbols.Current.Modifiers;
                if ((modifier & SymbolModifiers.Abstract) != 0)
                  fieldMember.Flags |= FieldFlags.None;
                if ((modifier & SymbolModifiers.Final) != 0)
                  fieldMember.Flags |= FieldFlags.None;
                if ((modifier & SymbolModifiers.Private) != 0)
                  fieldMember.Flags |= FieldFlags.Private;
                if ((modifier & SymbolModifiers.Public) != 0)
                  fieldMember.Flags |= FieldFlags.Public;
                if ((modifier & SymbolModifiers.Static) != 0)
                  fieldMember.Flags |= FieldFlags.Static;
                list.Add(fieldMember);
                symbols.MoveNext();
              }
            }
            Class dummy = new Class();
            dummy.DeclaringModule = new Module();
            this.Type = TupleType.For(list, dummy);
            break;
          case StructTypes.Union:
            // HACK: Need a better way for identifying return types
            this.Type = TypeUnion.For(SymbolHelper.GetTypeList(this.Symbol.Type.FullName, this.debugEnv.context), new Module());
            break;
          case StructTypes.Intersection:
            // TODO: Need to figure out Intersection Types, I think depends on figuring out return Type
            //this.Type = TypeIntersection.For(typeList, new Module());
            this.Type = new Class();
            break;
        }
          break;
        case TypeKind.Primitive:
        switch(this.Symbol.Type.TypeCode){
          case TypeCode.Boolean:
            this.Type = SystemTypes.Boolean;
            break;
          case TypeCode.Char:
            this.Type = SystemTypes.Char;
            break;
          case TypeCode.Int16:
            this.Type = SystemTypes.Int16;
            break;
          case TypeCode.UInt16:
            this.Type = SystemTypes.UInt32;
            break;
          case TypeCode.Int32:
            this.Type = SystemTypes.Int32;
            break;
          case TypeCode.UInt32:
            this.Type = SystemTypes.UInt32;
            break;
          case TypeCode.Int64:
            this.Type = SystemTypes.Int64;
            break;
          case TypeCode.UInt64:
            this.Type = SystemTypes.UInt64;
            break;
          case TypeCode.Double:
            this.Type = SystemTypes.Double;
            break;
          case TypeCode.Single:
            this.Type = SystemTypes.Single;
            break;
          case TypeCode.SByte:
            this.Type = SystemTypes.Int8;
            break;
          case TypeCode.Byte:
            this.Type = SystemTypes.UInt8;
            break;
        }
          break;
        case TypeKind.Enum:
          this.Type = new DebugEnumNode(this.debugEnv, this.Symbol.Type);
          break;
      }
    }
    public virtual IDebugType  GetDebugType {
      get {
        IDebugInfo typeInfo = this.Type as IDebugInfo;
        if (typeInfo != null) {
          return typeInfo.GetDebugType;
        }
        return null;
      }
    }
    public virtual IDebugValue GetDebugValue{
      get{
        IDebugFieldSymbol symbol = this.Symbol as IDebugFieldSymbol;
        if (symbol != null)
          return symbol.GetValue(this.Container);
        else
          return null;
      }
    }
    public override Literal GetValue(Literal targetObject){
      return new Literal(this.GetDebugValue, this.Type);
    }
  }
  public class DebugFieldNode : Field, IDebugInfo{
    public DebugEnvironment debugEnv;
    public IDebugSymbol  Symbol;
    public IDebugValue   Container;
    public int index;
    public DebugFieldNode(DebugEnvironment envr, IDebugSymbol symbol, Identifier name, IDebugValue container, TypeNode declaringType, int id) {
      this.debugEnv = envr;
      this.Symbol  = symbol;
      this.Container = container;
      this.Name = name;
      this.index = id;
      this.DeclaringType = declaringType;
      switch(symbol.Type.Kind) {
        case TypeKind.Class:
          this.Type = new DebugClassNode(this.debugEnv, this.Symbol.Type, ((IDebugFieldSymbol ) symbol).GetValue(Container));
          break;
        case TypeKind.Stream:
          this.Type = symbol.Type.CompilerType;
          break;
        case TypeKind.Tuple:
          StructTypes sType = ((IDebugStructuralType) this.Symbol.Type).StructuralType;
        switch (sType){
          case StructTypes.Tuple:
            FieldList list = new FieldList();
            IEnumSymbol symbols = ((IDebugStructuralType) this.Symbol.Type).GetMembers(null, true, SymbolKind.Field, SymbolModifiers.All);
            if (symbols != null){
              while(symbols.Current != null){
                Field fieldMember = new DebugFieldNode(this.debugEnv, symbols.Current, new Identifier(symbols.Current.Name), ((IDebugFieldSymbol ) symbol).GetValue(Container), null, 0);
                SymbolModifiers modifier = symbols.Current.Modifiers;
                if ((modifier & SymbolModifiers.Abstract) != 0)
                  fieldMember.Flags |= FieldFlags.None;
                if ((modifier & SymbolModifiers.Final) != 0)
                  fieldMember.Flags |= FieldFlags.None;
                if ((modifier & SymbolModifiers.Private) != 0)
                  fieldMember.Flags |= FieldFlags.Private;
                if ((modifier & SymbolModifiers.Public) != 0)
                  fieldMember.Flags |= FieldFlags.Public;
                if ((modifier & SymbolModifiers.Static) != 0)
                  fieldMember.Flags |= FieldFlags.Static;
                list.Add(fieldMember);
                symbols.MoveNext();
              }
            }
            Class dummy = new Class();
            dummy.DeclaringModule = new Module();
            this.Type = TupleType.For(list, dummy);
            break;
          case StructTypes.Union:
            // HACK: Need a better way for identifying return types
            this.Type = TypeUnion.For(SymbolHelper.GetTypeList(this.Symbol.Type.FullName, this.debugEnv.context), new Module());
            break;
          case StructTypes.Intersection:
            // TODO: Need to figure out Intersection Types, I think depends on figuring out return Type
            //this.Type = TypeIntersection.For(typeList, new Module());
            this.Type = new Class();
            break;
        }

          /*FieldList list = new FieldList();
          IEnumSymbol symbols = ((IDebugStructuralType) this.Symbol.Type).GetMembers(null, true, SymbolKind.Field, SymbolModifiers.All);
          if (symbols != null){
            while(symbols.Current != null){
              list.Add(new DebugFieldNode(this.debugEnv, symbols.Current, new Identifier(symbols.Current.Name), null, null, 0));
              symbols.MoveNext();
            }
          }
          Class dummy = new Class();
          dummy.DeclaringModule = new Module();
          this.Type = TupleType.For(list, dummy);*/
          break;
        case TypeKind.Primitive:
        switch(this.Symbol.Type.TypeCode){
          case TypeCode.Boolean:
            this.Type = SystemTypes.Boolean;
            break;
          case TypeCode.Char:
            this.Type = SystemTypes.Char;
            break;
          case TypeCode.Int16:
            this.Type = SystemTypes.Int16;
            break;
          case TypeCode.UInt16:
            this.Type = SystemTypes.UInt32;
            break;
          case TypeCode.Int32:
            this.Type = SystemTypes.Int32;
            break;
          case TypeCode.UInt32:
            this.Type = SystemTypes.UInt32;
            break;
          case TypeCode.Int64:
            this.Type = SystemTypes.Int64;
            break;
          case TypeCode.UInt64:
            this.Type = SystemTypes.UInt64;
            break;
          case TypeCode.Double:
            this.Type = SystemTypes.Double;
            break;
          case TypeCode.Single:
            this.Type = SystemTypes.Single;
            break;
          case TypeCode.SByte:
            this.Type = SystemTypes.Int8;
            break;
          case TypeCode.Byte:
            this.Type = SystemTypes.UInt8;
            break;
          case TypeCode.String:
            this.Type = SystemTypes.String;
            break;
        }
          break;
        case TypeKind.Enum:
          this.Type = new DebugEnumNode(this.debugEnv, this.Symbol.Type);
          break;
      }
    }
    public virtual IDebugType  GetDebugType {
      get {
        IDebugInfo typeInfo = this.Type as IDebugInfo;
        if (typeInfo != null) {
          return typeInfo.GetDebugType;
        }
        return null;
      }
    }
    public virtual IDebugValue GetDebugValue{
      get{
        IDebugFieldSymbol symbol = this.Symbol as IDebugFieldSymbol;
        if (symbol != null)
          return symbol.GetValue(this.Container);
        IDebugPropertySymbol propSymbol = this.Symbol as IDebugPropertySymbol;
        if (propSymbol != null){
          return propSymbol.GetGetter().Evaluate(this.Container, new IDebugValue[] { this.Container });
        }
        return null;
      }
    }
    public override Literal GetValue(Literal targetObject){
      return new Literal(this.GetDebugValue, this.Type);
    }
  }
  public class DebugClassNode : Class, IDebugInfo{
    public DebugEnvironment debugEnv;
    IDebugType          SymbolType;
    IDebugValue         Value;

    public virtual IDebugType  GetDebugType {
      get {
        return this.SymbolType;
      }
    }
    public virtual IDebugValue GetDebugValue{
      get{
        return this.Value;
      }
    }

    public DebugClassNode(DebugEnvironment envr, IDebugType symbolType, IDebugValue value) {
      this.debugEnv = envr;
      this.SymbolType = symbolType;
      this.Value = value;
      this.Name = Identifier.For(this.GetDebugType.Name);
    }

    public override String FullName {
      get {
        String name = "";
        if (this.GetDebugType != null)
          name = this.GetDebugType.FullName;
        return name;
      }
    }

    public override MemberList GetMembersNamed(Identifier name) {
      MemberList returnList = new MemberList();
      CDebugClassType classType = this.GetDebugType as CDebugClassType;

      if (classType != null) {
        IEnumSymbol members = classType.GetMembers(name.Name, true, SymbolKind.Field|SymbolKind.Property, SymbolModifiers.All);
        if (members != null){
          while(members.Current != null) {
            if (members.Current.Name == name.Name) {
              Field fieldMember = new DebugFieldNode(this.debugEnv, members.Current, name, this.Value, this, 0);
              SymbolModifiers modifier = members.Current.Modifiers;
              if ((modifier & SymbolModifiers.Abstract) != 0)
                fieldMember.Flags |= FieldFlags.None;
              if ((modifier & SymbolModifiers.Final) != 0)
                fieldMember.Flags |= FieldFlags.None;
              if ((modifier & SymbolModifiers.Private) != 0)
                fieldMember.Flags |= FieldFlags.Private;
              if ((modifier & SymbolModifiers.Public) != 0)
                fieldMember.Flags |= FieldFlags.Public;
              if ((modifier & SymbolModifiers.Static) != 0)
                fieldMember.Flags |= FieldFlags.Static;
              returnList.Add(fieldMember);
              break;
            }
            members.MoveNext();
          }
        }
      }

      return returnList;
    }
  }
  
  public class DebugEnumNode : EnumNode, IDebugInfo {
    public DebugEnvironment debugEnv;
    public IDebugType    type;

    public DebugEnumNode(DebugEnvironment envr, IDebugType type) {
      this.debugEnv = envr;
      this.type = type; 
    }

    public virtual IDebugType  GetDebugType{
      get {
        return type;
      }
    }
    public virtual IDebugValue GetDebugValue{
      get{
        return null;
      }
    }
  }
}