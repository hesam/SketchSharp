//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Diagnostics;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  public class TypeSystem{
    public bool allowStringLiteralToOtherPrimitiveCoercion;
    public bool checkOverflow;
    public bool suppressOverflowCheck;
    public bool insideUnsafeCode;
    public bool useGenerics;
    public Parameter currentParameter;
    public TypeNode currentType;
    public ErrorHandler ErrorHandler;
    public ErrorNodeList Errors;

    public TypeSystem(ErrorHandler errorHandler){
      this.ErrorHandler = errorHandler;
      if (errorHandler != null) this.Errors = errorHandler.Errors;
      this.useGenerics = TargetPlatform.UseGenerics;
    }

    public static TypeNode DoesNotMatchAnyType = new Struct();

    /// <summary>
    /// Returns true if conversion from t3 to t1 exists and is better (closer) than the conversion from t3 to t2
    /// Call this only if both conversions exist.
    /// </summary>
    public virtual bool IsBetterMatch(TypeNode t1, TypeNode t2, TypeNode t3) {
      return this.IsBetterMatch(t1, t2, t3, null);
    }
    /// <summary>
    /// Returns true if conversion from t3 to t1 exists and is better (closer) than the conversion from t3 to t2
    /// Call this only if both conversions exist.
    /// </summary>
    public virtual bool IsBetterMatch(TypeNode t1, TypeNode t2, TypeNode t3, TypeViewer typeViewer){
      if (t1 == null) return false;
      if (t2 == null || t2 == TypeSystem.DoesNotMatchAnyType) return true; //this type always loses
      if (t1 == t2) return false;
      if (t1 == t3) return true; //t2 is different from t3 while t1 == t3, so t1 must be a better match
      if (t2 == t3) return false; //t1 is different from t3, while t2 == t3, so t1 cannot be a better match
      //t3 can go to t1 and t2 only via conversions. Try to establish which conversion is better (closer).
      bool t1tot2 = this.ImplicitCoercionFromTo(t1, t2, typeViewer);
      bool t2tot1 = this.ImplicitCoercionFromTo(t2, t1, typeViewer);
      if (t1tot2 && !t2tot1) return this.ImplicitCoercionFromTo(t3, t1, typeViewer); //Can get from t3 to t2 via t1, but can't get from t3 to t1 via t2, so t3 is closer to t1
      if (t2tot1 && !t1tot2) return !this.ImplicitCoercionFromTo(t3, t2, typeViewer); //Get get from t3 to t1 via t2, but can't get from t3 to t2 via t1, so t2 is closer to t1
      //Special rule for integer types:
      //Prefer conversions to signed integers over conversions to unsigned integers.
      //But always prefer smaller int over larger int.
      switch (t1.TypeCode){
        case TypeCode.SByte:
          switch (t2.TypeCode){
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
              return true;
          }
          break;
        case TypeCode.Byte:
          switch (t2.TypeCode){
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
              return true;
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
              return false;
          }
          break;
        case TypeCode.Int16:
          switch (t2.TypeCode){
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Int32:
            case TypeCode.Int64:
              return true;  
            case TypeCode.SByte:
              return false;
          }
          break;
        case TypeCode.UInt16:
          switch (t2.TypeCode){
            case TypeCode.UInt32:
            case TypeCode.UInt64:
              return true;  
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
              return false;
          }
          break;
        case TypeCode.Int32:
          switch (t2.TypeCode){
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Int64:
              return true;
            case TypeCode.SByte:
            case TypeCode.Int16:
              return false;
          }
          break;
        case TypeCode.UInt32:
          switch (t2.TypeCode){
            case TypeCode.UInt64:
              return true;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
              return false;
          }
          break;
        case TypeCode.Int64:
          switch (t2.TypeCode){
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
              return true;
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32:
              return false;
          }
          break;
        case TypeCode.UInt64:
          switch (t2.TypeCode){
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
              return false;
          }
          break;
      }
      if (!t1.IsValueType && t2.IsValueType && t3 == SystemTypes.Object) return true;
      if (t1 is Pointer && !(t2 is Pointer) && t3 is Pointer && ((Pointer)t3).ElementType == SystemTypes.Void)
        return true;
      return false;
    }
    public virtual bool ImplicitCoercionFromTo(TypeNode t1, TypeNode t2){
      return this.ImplicitCoercionFromTo(null, t1, t2, null);
    }
    public virtual bool ImplicitCoercionFromTo(TypeNode t1, TypeNode t2, TypeViewer typeViewer){
      return this.ImplicitCoercionFromTo(null, t1, t2, typeViewer);
    }
    public virtual bool ImplicitCoercionFromTo(Expression source, TypeNode t1, TypeNode t2){
      return this.ImplicitCoercionFromTo(source, t1, t2, null);
    }
    public virtual bool ImplicitCoercionFromTo(Expression source, TypeNode t1, TypeNode t2, TypeViewer typeViewer){
      if (t1 == null || t2 == null) return false;
      if (t1 == t2) return true;
      if (this.IsNullableType(t1) && this.IsNullableType(t2) && this.ImplicitCoercionFromTo(this.RemoveNullableWrapper(t1), this.RemoveNullableWrapper(t2)))
        return true;
      if (this.StandardImplicitCoercionFromTo(source, t1, t2, typeViewer)) return true;
      Method uconv = this.UserDefinedImplicitCoercionMethod(source, t1, t2, true, typeViewer);
      return uconv != null;
    }
    public virtual bool StandardImplicitCoercionFromTo(Expression source, TypeNode sourceType, TypeNode targetType, TypeViewer typeViewer){
      if (Literal.IsNullLiteral(source) && this.IsNullableType(targetType)) return true;
      //Strip aliases
      for (TypeAlias tAlias = sourceType as TypeAlias; tAlias != null && !tAlias.RequireExplicitCoercionFromUnderlyingType; tAlias = sourceType as TypeAlias)
        sourceType = tAlias.AliasedType;
      for (TypeAlias tAlias = targetType as TypeAlias; tAlias != null && !tAlias.RequireExplicitCoercionFromUnderlyingType; tAlias = targetType as TypeAlias)
        targetType = tAlias.AliasedType;
      //Identity coercion
      if (sourceType == targetType) return true;
      if (sourceType != SystemTypes.Object && this.IsNullableType(targetType))
        return this.StandardImplicitCoercionFromTo(source, sourceType, this.RemoveNullableWrapper(targetType), typeViewer);
      //Conversion to Object
      if (targetType == SystemTypes.Object) {
        // do not consider an arglist, which is not really a typable expression
        // as an object. 
        if ((source is ArglistArgumentExpression)) return false;
        else return true;
      }
      //Dereference source
      Reference sr = sourceType as Reference;
      if (sr != null) sourceType = TypeNode.StripModifiers(sr.ElementType);
      //Identity coercion after dereference
      if (sourceType == targetType) return true;
      //Special case for null literal
      if (source != null && source.NodeType == NodeType.Literal && ((Literal)source).Value == null)
        return !targetType.IsValueType || this.IsNullableType(targetType);
      //Implicit numeric coercions + implicit enumeration coercions + implicit constant expression coercions
      if (sourceType.IsPrimitive && sourceType != SystemTypes.String){
        if (targetType.IsPrimitive || targetType is EnumNode){
          if (this.ImplicitPrimitiveCoercionFromTo(source, sourceType, targetType)) return true;
        }else if (targetType == SystemTypes.Decimal){
          if (this.ImplicitLiteralCoercionFromTo(source as Literal, sourceType, targetType)) return true;
          switch (sourceType.TypeCode){
            case TypeCode.Char:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
              return true;
            default:
              return false;
          }
        }
        return false;
      }
      //Implicit reference coercions
      if (TypeViewer.GetTypeView(typeViewer, sourceType).IsAssignableTo(targetType)) return true;
      //Implicit anonymous function to delegate or anonymous function return type coercion
      if (source is AnonymousNestedFunction){
        AnonymousNestedFunction func = (AnonymousNestedFunction)source;
        if (func.Method == null) return false;
        DelegateNode targetDel = targetType as DelegateNode;
        if (targetDel != null){
          if (targetDel.ReturnType != func.Method.ReturnType) return false;
          if (!func.Method.ParametersMatch(targetDel.Parameters)) return false;
          return true;
        }else{
          TypeNode frt = TypeNode.StripModifiers(func.Method.ReturnType);
          if (frt != null && func.Parameters == null || func.Parameters.Count == 0)
            return TypeViewer.GetTypeView(typeViewer, frt).IsAssignableTo(targetType);
          return false;
        }
      }
      //Implicit type union coercion
      TypeUnion sUnion = sourceType as TypeUnion;
      if (sUnion != null) return this.ImplicitCoercionFromUnionTo(sUnion, targetType, typeViewer);
      TypeUnion tUnion = targetType as TypeUnion;
      if (tUnion != null) return this.ImplicitCoercionToUnion(sourceType, tUnion, typeViewer);
      //Implicit type intersection coercion
      TypeIntersection tIntersect = targetType as TypeIntersection;
      if (tIntersect != null) return this.ImplicitCoercionToIntersection(sourceType, tIntersect, typeViewer);
      //Implicit tuple coercions
      TupleType sTuple = sourceType as TupleType; if (sTuple == null) return false;
      MemberList sMembers = sTuple.Members;
      if (sMembers == null) return false;
      int n = sMembers.Count;
      TupleType tTuple = targetType as TupleType;
      if (tTuple == null){
        if (n == 3){
          Field sField = sMembers[0] as Field;
          if (sField == null || !this.ImplicitCoercionFromTo(sField.Type, targetType, typeViewer)) return false;
          return true;
        }
        return false;
      }
      MemberList tMembers = tTuple.Members;
      if (tMembers == null) return false;
      if (n != tMembers.Count) return false;
      n-=2;
      for (int i = 0; i < n; i++){
        Field sField = sMembers[i] as Field; if (sField == null) return false;
        Field tField = tMembers[i] as Field; if (tField == null) return false;
        if (!tField.IsAnonymous && tField.Name != null && 
          (sField.IsAnonymous || sField.Name == null || tField.Name.UniqueIdKey != sField.Name.UniqueIdKey)) return false;
        if (!this.ImplicitCoercionFromTo(sField.Type, tField.Type, typeViewer)) return false;
      }
      return true;
    }
    public virtual bool ImplicitCoercionFromUnionTo(TypeUnion union, TypeNode targetType, TypeViewer typeViewer){
      if (union == null) return false;
      TypeNodeList types = union.Types;
      for (int i = 0, n = types == null ? 0 : types.Count; i < n; i++){
        TypeNode t = types[i];
        if (t == null) continue;
        if (!this.ImplicitCoercionFromTo(t, targetType, typeViewer)) return false;
      }
      return true;
    }
    public virtual bool ImplicitCoercionToIntersection(TypeNode sourceType, TypeIntersection intersect, TypeViewer typeViewer){
      if (intersect == null) return false;
      TypeNodeList types = intersect.Types;
      for (int i = 0, n = types == null ? 0 : types.Count; i < n; i++){
        TypeNode t = types[i];
        if (t == null) continue;
        if (!this.ImplicitCoercionFromTo(sourceType, t, typeViewer)) return false;
      }
      return true;
    }
    public virtual bool ImplicitCoercionToUnion(TypeNode sourceType, TypeUnion union, TypeViewer typeViewer){
      if (union == null) return false;
      TypeNodeList types = union.Types;
      for (int i = 0, n = types == null ? 0 : types.Count; i < n; i++){
        TypeNode t = types[i];
        if (t == null) continue;
        if (this.ImplicitCoercionFromTo(sourceType, t, typeViewer)) return true;
      }
      return true;
    }
    public virtual bool ImplicitPrimitiveCoercionFromTo(Expression source, TypeNode sourceType, TypeNode targetType){
      if (this.ImplicitLiteralCoercionFromTo(source as Literal, sourceType, targetType)) return true;
      TypeCode ttc = targetType.TypeCode;
      TypeCode stc = sourceType.TypeCode;
      switch(stc){
        case TypeCode.SByte:
          switch(ttc){
            case TypeCode.Int16:
            case TypeCode.Int32:  
            case TypeCode.Int64:  
            case TypeCode.Single: 
            case TypeCode.Double: return true;
          }
          break;
        case TypeCode.Int16:
          switch(ttc){
            case TypeCode.Int32:  
            case TypeCode.Int64:  
            case TypeCode.Single: 
            case TypeCode.Double: return true;
          }
          break;
        case TypeCode.Int32:
          switch(ttc){
            case TypeCode.Int64:  
            case TypeCode.Single: 
            case TypeCode.Double: return true;
          }
          break;
        case TypeCode.Int64:
          switch(ttc){
            case TypeCode.Single: 
            case TypeCode.Double: return true;
          }
          break;
        case TypeCode.Byte:
          switch(ttc){
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32: 
            case TypeCode.Int64:  
            case TypeCode.UInt64: 
            case TypeCode.Single: 
            case TypeCode.Double: return true;
          }
          break;
        case TypeCode.UInt16:
          switch(ttc){
            case TypeCode.Int32:
            case TypeCode.UInt32: 
            case TypeCode.Int64:  
            case TypeCode.UInt64: 
            case TypeCode.Single: 
            case TypeCode.Double: return true;
          }
          break;
        case TypeCode.UInt32:
          switch(ttc){
            case TypeCode.Int64:  
            case TypeCode.UInt64: 
            case TypeCode.Single: 
            case TypeCode.Double: return true;
          }
          break;
        case TypeCode.UInt64:
          switch(ttc){
            case TypeCode.Single: 
            case TypeCode.Double: return true;
          }
          break;
        case TypeCode.Char:
          switch(ttc){
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32: 
            case TypeCode.Int64:  
            case TypeCode.UInt64: 
            case TypeCode.Single: 
            case TypeCode.Double: return true;
          }
          break;
        case TypeCode.Single:
          if (ttc == TypeCode.Double) return true;           
          break;
      }
      return false;
    }
    public virtual bool ImplicitLiteralCoercionFromTo(Literal lit, TypeNode sourceType, TypeNode targetType){
      if (lit == null) return false;
      if (lit.Value == null && !targetType.IsValueType) return true;
      try {
        object val = System.Convert.ChangeType(lit.Value, targetType.TypeCode);
        return true;
      }catch(FormatException){
      }catch(InvalidCastException){
      }catch(OverflowException){}
      return false;
    }
    public virtual Method UserDefinedImplicitCoercionMethod(Expression source, TypeNode sourceType, TypeNode targetType, bool tryStandardCoercions, TypeViewer typeViewer){
      Reference rtype = sourceType as Reference;
      if (rtype != null) sourceType = rtype.ElementType;
      if (tryStandardCoercions && this.IsNullableType(sourceType) && this.IsNullableType(targetType)) {
        sourceType = sourceType.TemplateArguments[0];
        targetType = targetType.TemplateArguments[0];
      }
      //First do efficient searches for a method that implicitly coerces directly between source and target type
      //If the source type knows how to convert to the target type, give it preference
      Method coercion = TypeViewer.GetTypeView(typeViewer, sourceType).GetImplicitCoercionToMethod(targetType);
      if (coercion != null) return coercion;
      //If the target type knows how to convert from the source type, that is dandy too
      coercion = TypeViewer.GetTypeView(typeViewer, targetType).GetImplicitCoercionFromMethod(sourceType);
      if (coercion != null) return coercion;
      //Perhaps the base type can convert to the target type, or the target type can convert from the base type
      if (sourceType.BaseType != null && sourceType != SystemTypes.Object){
        coercion = this.UserDefinedImplicitCoercionMethod(source, sourceType.BaseType, targetType, tryStandardCoercions, typeViewer);
        if (coercion != null) return coercion;
      }
      if (!tryStandardCoercions) return null;
      //Now resort to desperate measures
      //See if the source type has a conversion that results in a type that can be converted to the target type via a standard coercion
      MemberList coercions = TypeViewer.GetTypeView(typeViewer, sourceType).ImplicitCoercionMethods;
      for (int i = 0, n = coercions == null ? 0 : coercions.Count; i < n; i++){
        coercion = coercions[i] as Method;
        if (coercion == null) continue;
        if (coercion.ReturnType == sourceType) continue;
        if (this.StandardImplicitCoercionFromTo(source, coercion.ReturnType, targetType, typeViewer)) return coercion;
      }
      //See if the target type has a conversion that can convert the source after a standard coercion has been applied
      coercions = TypeViewer.GetTypeView(typeViewer, targetType).ImplicitCoercionMethods;
      for (int i = 0, n = coercions == null ? 0 : coercions.Count; i < n; i++){
        coercion = coercions[i] as Method;
        if (coercion == null) continue;
        if (coercion.ReturnType != targetType) continue;
        ParameterList pars = coercion.Parameters;
        if (pars == null || pars.Count != 1) continue;
        Parameter par = pars[0];
        if (par.Type == null) continue;
        if (this.StandardImplicitCoercionFromTo(source, sourceType, par.Type, typeViewer)) return coercion;
      }
      return null;
    }
    public virtual TypeNode UnifiedType(Literal lit, TypeNode t, TypeViewer typeViewer){
      if (lit == null || lit.Type == null || t == null){Debug.Assert(false); return SystemTypes.Object;}
      t = this.Unwrap(t);
      MemberList coercions = TypeViewer.GetTypeView(typeViewer, t).ImplicitCoercionMethods;
      for (int i = 0, n = coercions == null ? 0 : coercions.Count; i < n; i++){
        Method coercion = coercions[i] as Method;
        if (coercion == null) continue;
        TypeNode t2 = coercion.ReturnType;
        if (t2 == t || t2 == null || !t2.IsPrimitive) continue;
        if (this.ImplicitLiteralCoercionFromTo(lit, lit.Type, t2)) return t2;
      }
      return this.UnifiedType(lit.Type, t);
    }
    public virtual TypeNode UnifiedType(TypeNode t1, TypeNode t2){
      t1 = this.Unwrap(t1);
      t2 = this.Unwrap(t2);
      if (t1 == null || t2 == null) return SystemTypes.Object;
      if (t1.IsPrimitive && t2.IsPrimitive)
        return this.UnifiedPrimitiveType(t1, t2);
      if (t1 == t2) return t1;
      return SystemTypes.Object;
    }
    /// <summary>
    /// Computes an upper bound in the type hierarchy for the set of argument types.
    /// This upper bound is a type that all types in the list are assignable to.
    /// If the types are all classes, then *the* least-upper-bound in the class
    /// hierarchy is returned.
    /// If the types contain at least one interface, then *a* deepest upper-bound
    /// is found from the intersection of the upward closure of each type.
    /// Note that if one of the types is System.Object, then that is immediately
    /// returned as the unified type without further examination of the list.
    /// </summary>
    /// <param name="ts">A list containing the set of types from which to compute the unified type.</param>
    /// <returns>The type corresponding to the least-upper-bound.</returns>
    public virtual TypeNode UnifiedType(TypeNodeList ts, TypeViewer typeViewer){
      if (ts == null || ts.Count == 0) return null;
      TypeNode unifiedType = SystemTypes.Object; // default unified type
      bool atLeastOneInterface = false;
      #region If at least one of the types is System.Object, then that is the unified type
      for (int i = 0, n = ts.Count; i < n; i++){
        TypeNode t = this.Unwrap(ts[i]);
        if (t == SystemTypes.Object){
          return SystemTypes.Object;
        }
      }
      #endregion If at least one of the types is System.Object, then that is the unified type
      // assert forall{TypeNode t in ts; t != SystemTypes.Object};
      #region See if any of the types are interfaces
      for (int i = 0, n = ts.Count; i < n; i++){
        TypeNode t = this.Unwrap(ts[i]);
        if (t.NodeType == NodeType.Interface){
          atLeastOneInterface = true;
          break;
        }
      }
      #endregion See if any of the types are interfaces

      #region Find the LUB in the class hierarchy (if there are no interfaces)
      if (!atLeastOneInterface){
        TrivialHashtable h = new TrivialHashtable(ts.Count);
        // Create the list [s, .., t] for each element t of ts where for each item
        // in the list, t_i, t_i = t_{i+1}.BaseType. (s.BaseType == SystemTypes.Object)
        // Store the list in a hashtable keyed by t.
        // Do this only for classes. Handle interfaces in a different way because of
        // multiple inheritance.
        for (int i = 0, n = ts.Count; i < n; i++){
          TypeNodeList tl = new TypeNodeList();
          TypeNode t = this.Unwrap(ts[i]);
          tl.Add(t);
          TypeNode t2 = t.BaseType;
          while (t2 != null && t2 != SystemTypes.Object){ // avoid including System.Object in the list for classes
            tl.Insert(t2,0);
            t2 = this.Unwrap(t2.BaseType);
          }
          h[ts[i].UniqueKey] = tl;
        }
        bool stop = false;
        int depth = 0;
        while (!stop){
          TypeNode putativeUnifiedType = null;
          int i = 0;
          int n = ts.Count;
          putativeUnifiedType = ((TypeNodeList) h[ts[0].UniqueKey])[depth];
          while (i < n){
            TypeNode t = ts[i];
            TypeNodeList subTypes = (TypeNodeList) h[t.UniqueKey];
            if (subTypes.Count <= depth || subTypes[depth] != putativeUnifiedType){
              // either reached the top of the hierarchy for t_i or it is on a different branch
              // than the current one.
              stop = true;
              break;
            }
            i++;
          }
          if (i == n){ // made it all the way through: all types are subtypes of the current one
            unifiedType = putativeUnifiedType;
          }
          depth++;
        }
      }
      #endregion Find the LUB in the class hierarchy (if there are no interfaces)
      #region Find *a* LUB in the interface hierarchy (if there is at least one interface or current LUB is object)
      if (unifiedType == SystemTypes.Object || atLeastOneInterface){
        TrivialHashtable interfaces = new TrivialHashtable();
        for (int i = 0, n = ts.Count; i < n; i++){
          InterfaceList il = new InterfaceList();
          interfaces[ts[i].UniqueKey] = il;
          this.SupportedInterfaces(ts[i],il,typeViewer); // side-effect: il gets added to
        }
        // interfaces[ts[i]] is the upward closure of all of the interfaces supported by ts[i]
        // compute the intersection of all of the upward closures
        // might as well start with the first type in the list ts
        InterfaceList intersection = new InterfaceList();
        InterfaceList firstIfaceList = (InterfaceList)interfaces[ts[0].UniqueKey];
        for (int i = 0, n = firstIfaceList.Count; i < n; i++){
          Interface iface = firstIfaceList[i];
          bool found = false;
          int j = 1; // start at second type in the list ts
          while (j < ts.Count){
            InterfaceList cur = (InterfaceList)interfaces[ts[j].UniqueKey];
            found = false;
            for (int k = 0, p = cur.Count; k < p; k++){
              if (cur[k] == iface){
                found = true;
                break;
              }
            }
            if (!found){
              // then the j-th type doesn't support iface, don't bother looking in the rest
              break;
            }
            j++;
          }
          if (found){
            intersection.Add(iface);
          }
        }
        // TODO: take the "deepest" interface in the intersection.
        // "deepest" means that if any other type in the intersection is a subtype
        // of it, then *don't* consider it.
        if (intersection.Count > 0){
          InterfaceList finalIntersection = new InterfaceList(intersection.Count);
          Interface iface = intersection[0];
          for (int i = 0, n = intersection.Count; i < n; i++){
            Interface curFace = intersection [i];
            int j = 0;
            int m = intersection.Count;
            while (j < m){
              if (j != i){
                Interface jFace = intersection[j];
                if (TypeViewer.GetTypeView(typeViewer, jFace).IsAssignableTo(curFace))
                  break;
              }
              j++;
            }
            if (j == m){ // made it all the way through, no other iface is a subtype of curFace
              finalIntersection.Add(curFace);
            }
          }
          if (finalIntersection.Count > 0){
            unifiedType = finalIntersection[0]; // heuristic: just take the first one
          }
        }
      }
      #endregion Find *a* LUB in the interface hierarchy (if there is at least one interface or current LUB is object)
      return unifiedType;
    }
    public virtual void SupportedInterfaces(TypeNode t, InterfaceList ifaceList, TypeViewer typeViewer){
      if (ifaceList == null) return;
      TypeNode unwrappedT = this.Unwrap(t);
      Interface iface = unwrappedT as Interface;
      if (iface != null){
        // possibly not needed, but seems better to keep ifaceList as a set
        int i = 0;
        while (i < ifaceList.Count){
          if (ifaceList[i] == iface)
            break;
          i++;
        }
        if (i == ifaceList.Count) // not found
          ifaceList.Add(iface);
      }else{
        // nop
      }
      InterfaceList ifaces = TypeViewer.GetTypeView(typeViewer, unwrappedT).Interfaces;
      for (int i = 0, n = ifaces == null ? 0 : ifaces.Count; i < n; i++){
        this.SupportedInterfaces(ifaces[i],ifaceList,typeViewer);
      }
      return;
    }
    public virtual TypeNode UnifiedPrimitiveType(TypeNode t1, TypeNode t2){
      if (t1 == null || t2 == null) return SystemTypes.Object;
      t1 = this.Unwrap(t1);
      t2 = this.Unwrap(t2);
      switch(t1.TypeCode){
        case TypeCode.Boolean:
          if (t2.TypeCode == TypeCode.Boolean) return SystemTypes.Boolean;
          break;
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
          switch(t2.TypeCode){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32: return SystemTypes.Int32;
            case TypeCode.UInt32: 
            case TypeCode.Int64: 
            case TypeCode.UInt64: return SystemTypes.Int64;
            case TypeCode.Single: return SystemTypes.Single;
            case TypeCode.Double: return SystemTypes.Double;
          }
          break;
        case TypeCode.Byte:
        case TypeCode.UInt16:
        case TypeCode.Char:
          switch(t2.TypeCode){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16: 
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32: return SystemTypes.Int32;
            case TypeCode.UInt32: return SystemTypes.UInt32;
            case TypeCode.Int64: return SystemTypes.Int64;
            case TypeCode.UInt64: return SystemTypes.UInt64;
            case TypeCode.Single: return SystemTypes.Single;
            case TypeCode.Double: return SystemTypes.Double;
          }
          break;
        case TypeCode.UInt32:
          switch(t2.TypeCode){
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: return SystemTypes.UInt32;
            case TypeCode.SByte:
            case TypeCode.Int16: 
            case TypeCode.Int32:
            case TypeCode.Int64: return SystemTypes.Int64;
            case TypeCode.UInt64: return SystemTypes.UInt64;
            case TypeCode.Single: return SystemTypes.Single;
            case TypeCode.Double: return SystemTypes.Double;
          }
          break;
        case TypeCode.Int64:
          switch(t2.TypeCode){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64: return SystemTypes.Int64;
            case TypeCode.Single: return SystemTypes.Single;
            case TypeCode.Double: return SystemTypes.Double;
          }
          break;
        case TypeCode.UInt64:
          switch(t2.TypeCode){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64: return SystemTypes.Int64;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32:
            case TypeCode.UInt64: return SystemTypes.UInt64;
            case TypeCode.Single: return SystemTypes.Single;
            case TypeCode.Double: return SystemTypes.Double;
          }
          break;
        case TypeCode.Single:
          switch(t2.TypeCode){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
            case TypeCode.Single: return SystemTypes.Single;
            case TypeCode.Double: return SystemTypes.Double;
          }
          break;
        case TypeCode.Double:
          switch(t2.TypeCode){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
            case TypeCode.Single:
            case TypeCode.Double: return SystemTypes.Double;
          }
          break;
      }
      if (t1 is Pointer && t2 is Pointer){
        TypeNode t = this.UnifiedType(((Pointer)t1).ElementType, ((Pointer)t2).ElementType);
        if (t == SystemTypes.Object) t = SystemTypes.Void;
        if (t != null) return t.GetPointerType();
      }
      if (t1.IsTemplateParameter && !t1.IsReferenceType) {
        // we assume t1 != t2
        return null;
      }
      if (t2.IsTemplateParameter && !t2.IsReferenceType) {
        // we assume t1 != t2
        return null;
      }
      return SystemTypes.Object;
    }
    public virtual TypeNode GetUnderlyingType(TypeNode t){
      if (t == null) return SystemTypes.Object;
      for(;;){
        switch(t.NodeType){
          case NodeType.EnumNode: t = ((EnumNode)t).UnderlyingType; break;
          case NodeType.OptionalModifier:
          case NodeType.RequiredModifier: t = ((TypeModifier)t).ModifiedType; break;
          case NodeType.Reference: t = ((Reference)t).ElementType; break; //REVIEW: do all callers expect this?
          default: return t;
        }
      }
    }
    public virtual TypeNode GetStreamElementType(TypeNode t){
      return this.GetStreamElementType(t, null);
    }
    public virtual TypeNode GetStreamElementType(TypeNode t, TypeViewer typeViewer){
      TypeNode originalT = t;
      TypeAlias ta = t as TypeAlias;
      while (ta != null) { t = ta.AliasedType; ta = t as TypeAlias; }
      if (t == null) return null;
      ArrayType aType = t as ArrayType; //REVIEW: should [] and {} participate in this?
      if (aType != null) return aType.ElementType;
      TypeNode template = t.Template;
      if (template == SystemTypes.GenericIEnumerable || template == SystemTypes.GenericIEnumerator || template == SystemTypes.GenericIList ||
        template == SystemTypes.GenericList || template == SystemTypes.GenericNonEmptyIEnumerable ||
        template == SystemTypes.GenericBoxed || template == SystemTypes.GenericNonNull || template == SystemTypes.GenericInvariant){
        if (t.TemplateArguments != null && t.TemplateArguments.Count > 0)
          return t.TemplateArguments[0];
      }
      if (TypeViewer.GetTypeView(typeViewer, t).IsAssignableTo(SystemTypes.Range)) return SystemTypes.Int32;
      if (TypeViewer.GetTypeView(typeViewer, t).IsAssignableTo(SystemTypes.IList)) return SystemTypes.Object;
      if (TypeViewer.GetTypeView(typeViewer, t).IsAssignableTo(SystemTypes.IDictionary)) return SystemTypes.Object;
      if (t is Interface) return originalT;
      InterfaceList ifaces = TypeViewer.GetTypeView(typeViewer, t).Interfaces;
      for (int i = 0, n = ifaces == null ? 0 : ifaces.Count; i < n; i++){
        Interface iface = ifaces[i];
        if (iface == null) continue;
        TypeNode eType = this.GetStreamElementType(iface, typeViewer);
        if (eType != iface) return eType;
      }
      return originalT;
      
    }
    public virtual TypeNode GetCollectionElementType(TypeNode t, TypeViewer typeViewer){
      bool foundObject = false;
      TypeAlias ta = t as TypeAlias;
      while (ta != null){t = ta.AliasedType; ta = t as TypeAlias;}
      if (t == null || t == SystemTypes.String || t is TupleType) return null;
      // look for get_Item indexer
      MemberList list = TypeViewer.GetTypeView(typeViewer, t).GetMembersNamed(StandardIds.getItem);
      if (list != null) {
        for( int i = 0, n = list.Count; i < n; i++ ) {
          Method m = list[i] as Method;
          if (m == null) continue;
          if (m.ReturnType != SystemTypes.Object) return m.ReturnType;
          foundObject = true;
        }
      }
      // look for enumerable pattern
      Method mge = TypeViewer.GetTypeView(typeViewer, t).GetMethod(StandardIds.GetEnumerator);
      if (mge != null) {
        Method mgc = TypeViewer.GetTypeView(typeViewer, mge.ReturnType).GetMethod(StandardIds.getCurrent);
        if (mgc != null) {
          if (mgc.ReturnType != SystemTypes.Object) return mgc.ReturnType;
          foundObject = true;
        }
      }
      InterfaceList ilist = TypeViewer.GetTypeView(typeViewer, t).Interfaces;
      if (ilist != null) {
        for( int i = 0, n = ilist.Count; i < n; i++ ) {
          Interface iface = ilist[i];
          if (iface == null) continue;
          TypeNode tn = this.GetCollectionElementType(iface, typeViewer);
          if (tn == null) continue;
          if (tn != SystemTypes.Object) return tn;
          foundObject = true;
        }
      }
      if (foundObject) return SystemTypes.Object;
      if (t.BaseType != null && t.BaseType != SystemTypes.Object) {
        return this.GetCollectionElementType(t.BaseType, typeViewer);
      }
      return null;
    }

    public virtual MemberList GetDataMembers(TypeNode type){
      MemberList result = new MemberList();
      this.GetDataMembers(type, result);
      return result;
    }
    protected virtual void GetDataMembers(TypeNode type, MemberList list){
      if (type == null) return;
      if (type.BaseType != SystemTypes.Object) this.GetDataMembers(type.BaseType, list);
      MemberList tMembers = type.Members;
      for (int i = 0, n = tMembers == null ? 0 : tMembers.Count; i < n; i++ ){
        Member m = tMembers[i];
        if ((m is Field || m is Property) && m.IsPublic) list.Add(m);
      }
    }
    protected virtual Expression GetInnerExpression(Expression x){
      //TODO: move this to a helper class
      if (x == null) return null;
      for (;;){
        switch (x.NodeType){
          case NodeType.Parentheses:
          case NodeType.SkipCheck:
            x = ((UnaryExpression)x).Operand;
            if (x == null) return null;
            continue;
          default:
            return x;
        }
      }
    }
    public virtual TypeNode GetStreamElementType(Expression x){
      return this.GetStreamElementType(x, null);
    }
    public virtual TypeNode GetStreamElementType(Expression x, TypeViewer typeViewer){
      x = this.GetInnerExpression(x);
      if (x == null) return null;
      if (x.NodeType == NodeType.Composition) x = ((Composition)x).Expression;
      if (x == null) return null;
      TypeNode xt = TypeNode.StripModifiers(x.Type);
      TypeNode t = this.GetCollectionElementType(xt, typeViewer);
      if (t != null && (t != SystemTypes.Object || xt == SystemTypes.IEnumerable)) return t;
      t = this.GetStreamElementType(xt, typeViewer);
      if (t != xt && t != SystemTypes.Object) return t;
      switch (x.NodeType){
        case NodeType.MethodCall:
        case NodeType.Call:
        case NodeType.Calli:
        case NodeType.Callvirt: {
          MethodCall mc = (MethodCall) x;
          MemberBinding mb = mc.Callee as MemberBinding;
          if (mb != null) {
            TypeNode mt = this.GetMemberElementType(mb.BoundMember, typeViewer);
            if (mt != null && mt != SystemTypes.Object) return mt;
          }
          break;
        }
        case NodeType.MemberBinding: {
          TypeNode mt = this.GetMemberElementType(((MemberBinding)x).BoundMember, typeViewer);
          if (mt != null && mt != SystemTypes.Object) return mt;
          break;
        }
      }
      return t;
    }
    public virtual TypeNode GetMemberElementType(Member member, TypeViewer typeViewer) {
      if (member == null) return null;
      AttributeNode attr = MetadataHelper.GetCustomAttribute(member, SystemTypes.ElementTypeAttribute);
      if (attr != null){
        Literal litType = MetadataHelper.GetNamedAttributeValue(attr, StandardIds.ElementType);
        if (litType != null) return litType.Value as TypeNode;
      }        
      return this.GetStreamElementType(this.GetMemberType(member), typeViewer);
    }
    public virtual TypeNode GetMemberType(Member m){
      Field f = m as Field;
      if (f != null) return f.Type;
      Property p = m as Property;
      if (p != null) return p.Type;
      Method mth = m as Method;
      if (mth != null) return mth.ReturnType;
      return null;
    }
    public virtual Cardinality GetCardinality(Expression collection) {
      return this.GetCardinality(collection, null);
    }
    public virtual Cardinality GetCardinality(Expression collection, TypeViewer typeViewer) {
      if (collection == null) return Cardinality.None;
      if (collection is Literal && collection.Type == SystemTypes.Type) {
        return Cardinality.One;
      }
      TypeNode type = collection.Type;
      TypeAlias ta = type as TypeAlias;
      if (ta != null) type = ta.AliasedType;
      Cardinality card = this.GetCardinality(type, typeViewer);
      if ((card == Cardinality.None || card == Cardinality.One) && 
        !(this.IsStructural(type) || type == SystemTypes.GenericNonNull)) {
        TypeNode elementType = this.GetStreamElementType(collection, typeViewer);
        if (elementType != type) {
          return Cardinality.ZeroOrMore;
        }     
      }
      return card;
    }
    public virtual Cardinality GetCardinality(TypeNode collectionType, TypeViewer typeViewer) {
      if (collectionType == null) return Cardinality.None;
      TypeAlias ta = collectionType as TypeAlias;
      if (ta != null) collectionType = ta.AliasedType;
      if (collectionType is TupleType) {
        return Cardinality.One;
      }
      else if (collectionType.Template == SystemTypes.GenericBoxed) {
        return Cardinality.ZeroOrOne;
      }
      else if (collectionType.Template == SystemTypes.GenericNonNull) {
        return Cardinality.One;
      }
      else if (collectionType.Template == SystemTypes.GenericNonEmptyIEnumerable) {
        return Cardinality.OneOrMore;
      }
      else if (TypeViewer.GetTypeView(typeViewer, collectionType).IsAssignableTo(SystemTypes.INullable)) {
        return Cardinality.ZeroOrOne;
      }
      else {
        TypeUnion tu = collectionType as TypeUnion;
        if (tu != null && tu.Types.Count > 0) {
          Cardinality c = this.GetCardinality(tu.Types[0], typeViewer);
          for( int i = 1, n = tu.Types.Count; i < n; i++ ) {
            TypeNode tn = tu.Types[i];
            if (tn == null) continue;
            c = this.GetCardinalityOr(c, this.GetCardinality(tn, typeViewer));
          }
          return c;
        }
        TypeNode elementType = this.GetStreamElementType(collectionType, typeViewer);
        if (elementType != collectionType) {
          return Cardinality.ZeroOrMore;
        }
        else if (collectionType.IsValueType) {
          return Cardinality.One;
        }
        else {
          return Cardinality.None;
        }
      }
    }
    public virtual Cardinality GetCardinalityAnd(Cardinality source, Cardinality element) {
      switch( source ) {
        case Cardinality.None:
          switch( element ) {
            case Cardinality.None:        return Cardinality.ZeroOrMore;
            case Cardinality.One:         return Cardinality.OneOrMore;
            case Cardinality.ZeroOrOne:   return Cardinality.ZeroOrMore;
            case Cardinality.OneOrMore:   return Cardinality.OneOrMore;
            case Cardinality.ZeroOrMore:  return Cardinality.ZeroOrMore;
          }
          break;
        case Cardinality.ZeroOrOne:
          switch( element ) {
            case Cardinality.None:        return Cardinality.ZeroOrMore;
            case Cardinality.One:         return Cardinality.OneOrMore;
            case Cardinality.ZeroOrOne:   return Cardinality.ZeroOrMore;
            case Cardinality.OneOrMore:   return Cardinality.OneOrMore;
            case Cardinality.ZeroOrMore:  return Cardinality.ZeroOrMore;
          }
          break;
        case Cardinality.ZeroOrMore:
          switch( element ) {
            case Cardinality.None:        return Cardinality.ZeroOrMore;
            case Cardinality.One:         return Cardinality.OneOrMore;
            case Cardinality.ZeroOrOne:   return Cardinality.ZeroOrMore;
            case Cardinality.OneOrMore:   return Cardinality.OneOrMore;
            case Cardinality.ZeroOrMore:  return Cardinality.ZeroOrMore;
          }
          break;
        case Cardinality.One:
          switch (element) {
            case Cardinality.None:        return Cardinality.OneOrMore;
            case Cardinality.One:         return Cardinality.OneOrMore;
            case Cardinality.ZeroOrOne:   return Cardinality.OneOrMore;
            case Cardinality.OneOrMore:   return Cardinality.OneOrMore;
            case Cardinality.ZeroOrMore:  return Cardinality.OneOrMore;
          }
          break;
        case Cardinality.OneOrMore:
          switch( element ) {
            case Cardinality.None:        return Cardinality.OneOrMore;
            case Cardinality.One:         return Cardinality.OneOrMore;
            case Cardinality.ZeroOrOne:   return Cardinality.OneOrMore;
            case Cardinality.OneOrMore:   return Cardinality.OneOrMore;
            case Cardinality.ZeroOrMore:  return Cardinality.OneOrMore;
          }
          break;
      }
      return Cardinality.None;
    }
    public virtual Cardinality GetCardinalityOr(Cardinality source, Cardinality element) {
      switch( source ) {
        case Cardinality.None:
          switch( element ) {
            case Cardinality.None:        return Cardinality.None;
            case Cardinality.One:         return Cardinality.None;
            case Cardinality.ZeroOrOne:   return Cardinality.ZeroOrOne;
            case Cardinality.OneOrMore:   return Cardinality.ZeroOrMore;
            case Cardinality.ZeroOrMore:  return Cardinality.ZeroOrMore;
          }
          break;
        case Cardinality.ZeroOrOne:
          switch( element ) {
            case Cardinality.None:        return Cardinality.ZeroOrOne;
            case Cardinality.One:         return Cardinality.ZeroOrOne;
            case Cardinality.ZeroOrOne:   return Cardinality.ZeroOrOne;
            case Cardinality.OneOrMore:   return Cardinality.ZeroOrMore;
            case Cardinality.ZeroOrMore:  return Cardinality.ZeroOrMore;
          }
          break;
        case Cardinality.ZeroOrMore:
          return Cardinality.ZeroOrMore;
        case Cardinality.One:
          switch (element) {
            case Cardinality.None:        return Cardinality.None;
            case Cardinality.One:         return Cardinality.One;
            case Cardinality.ZeroOrOne:   return Cardinality.ZeroOrOne;
            case Cardinality.OneOrMore:   return Cardinality.OneOrMore;
            case Cardinality.ZeroOrMore:  return Cardinality.ZeroOrMore;
            default:                      return element;
          }
        case Cardinality.OneOrMore:
          switch( element ) {
            case Cardinality.None:        return Cardinality.ZeroOrMore;
            case Cardinality.One:         return Cardinality.OneOrMore;
            case Cardinality.ZeroOrOne:   return Cardinality.ZeroOrMore;
            case Cardinality.OneOrMore:   return Cardinality.OneOrMore;
            case Cardinality.ZeroOrMore:  return Cardinality.ZeroOrMore;
          }
          break;
      }
      return Cardinality.None;
    }
    public virtual TypeNode GetRootType(TypeNode type, TypeViewer typeViewer) {
      if (type == null) return null;
      Cardinality card = this.GetCardinality(type, typeViewer);
      switch( card ) {
        case Cardinality.None:
        case Cardinality.One:
          if (type.Template != SystemTypes.GenericNonNull)
            return type;
          goto default;
        case Cardinality.ZeroOrOne:
          if (TypeViewer.GetTypeView(typeViewer, type).IsAssignableTo(SystemTypes.INullable))
            return type;
          goto default;
        case Cardinality.OneOrMore:
        case Cardinality.ZeroOrMore:
        default:
          return this.GetStreamElementType(type, typeViewer);
      }
    }
    public virtual bool IsStructural(TypeNode tn) {
      TypeAlias ta = tn as TypeAlias;
      if (ta != null) tn = ta.AliasedType;
      if (tn == null) return false;
      switch (tn.NodeType) {
        case NodeType.TupleType:
        case NodeType.TypeUnion:
          return true;
        default:
          return false;
      }
    }
    public virtual bool IsTransparent(Member m, TypeViewer typeViewer) {
      TypeNode rootType = this.GetRootType(this.GetMemberType(m), typeViewer);
      return m.Anonymity == Anonymity.Full || (m.Anonymity == Anonymity.Structural && this.IsStructural(rootType));
    }
    public virtual bool IsVoid(TypeNode type){
      return (type == SystemTypes.Void);
    }
    public virtual TypeNode UnwrapForMemberLookup(TypeNode obType) {
      return Unwrap(obType);
    }
    public virtual TypeNode Unwrap(TypeNode t){
      return this.Unwrap(t, false, false);
    }
    public virtual TypeNode Unwrap(TypeNode t, bool preserveSingletonTuple){
      return this.Unwrap(t, preserveSingletonTuple, false);
    }
    public virtual TypeNode Unwrap(TypeNode t, bool preserveSingletonTuple, bool preserveBoxed){
      if (t == null) return SystemTypes.Object;
      for(;;){
        switch(t.NodeType){
          case NodeType.ConstrainedType: t = ((ConstrainedType)t).UnderlyingType; break;
          case NodeType.OptionalModifier:
          case NodeType.RequiredModifier: t = ((TypeModifier)t).ModifiedType; break;
          case NodeType.TypeAlias: t = ((TypeAlias)t).AliasedType; break;
          case NodeType.Reference: 
            // recurse to give overriding methods a chance
            return Unwrap(((Reference)t).ElementType, preserveSingletonTuple, preserveBoxed);
          case NodeType.Interface:
            if (t.Template != SystemTypes.GenericNonEmptyIEnumerable || t.TemplateArguments == null || t.TemplateArguments.Count == 0)
              return t;
            t = t.TemplateArguments[0]; break;
          case NodeType.Struct:
            if (((t.Template != SystemTypes.GenericBoxed || preserveBoxed) && t.Template != SystemTypes.GenericNonNull && 
            t.Template != SystemTypes.GenericInvariant) || t.TemplateArguments == null || t.TemplateArguments.Count == 0)
              return t;
            t = t.TemplateArguments[0]; break;
          case NodeType.TupleType:
            TupleType tup = (TupleType)t;
            MemberList mems = tup.Members;
            if (!preserveSingletonTuple && mems != null && mems.Count == 3) //Singleton tuple
              return ((Field)mems[0]).Type;
            return t;
          case NodeType.TypeUnion:
            return SystemTypes.Object;
          default: return t;
        }
      }
    }
    public virtual bool HasValueEquality(TypeNode type, TypeViewer typeViewer){
      if (type == null) return false;
      if (type == SystemTypes.String) return true;
      if (type.IsValueType) return true;
      MemberList members = TypeViewer.GetTypeView(typeViewer, type).GetMembersNamed(StandardIds.opEquality);
      if (members == null || members.Count == 0) return false;
      for (int i = 0, n = members.Count; i < n; i++){
        Method m = members[i] as Method;
        if (m == null || !m.IsStatic || !m.IsSpecialName || m.ReturnType != SystemTypes.Boolean) continue;
        return true;
      }
      return false;
    }
    public virtual bool CoercionExtendsSign(TypeNode t1, TypeNode t2){
      if (t1 == null || t2 == null) return false;
      switch (t1.TypeCode){
        case TypeCode.SByte: 
          switch (t2.TypeCode){
            case TypeCode.SByte:
            case TypeCode.Byte: 
              return false;
            default:
              return true;
          }
        case TypeCode.Int16:
          switch (t2.TypeCode){
            case TypeCode.SByte:
            case TypeCode.Byte: 
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
              return false;
            default: return true;
          }
        case TypeCode.Int32:
          switch (t2.TypeCode){
            case TypeCode.Int64:
            case TypeCode.UInt64: 
              return true;
            default:
              return false;
          }
        default:
          return false;
      }
    }
    [Obsolete("Use TypeNode.StripModifiers instead")]
    public virtual TypeNode RemoveTransparentWrappers(TypeNode t){
      return TypeNode.StripModifiers(t);
    }
    public virtual TypeNode RemoveNullableWrapper(TypeNode t){
      if (t == null) return t;
      if (t.Template == SystemTypes.GenericNullable && t.TemplateArguments != null && t.TemplateArguments.Count == 1)
        return t.TemplateArguments[0];
      return t;
    }
    public virtual TypeNode CreateNullableWrapper(TypeNode currType, TypeNode t) {
      if (t == null || this.IsNullableType(t)) return t;
      return SystemTypes.GenericNullable.GetTemplateInstance(currType, t);
    }
    public virtual bool AssignmentCompatible(TypeNode sourceType, TypeNode targetType, TypeViewer typeViewer) {
      return TypeViewer.GetTypeView(typeViewer, sourceType).IsAssignableTo(targetType);
    }

    /// <summary>
    /// Hook for overriding current policy of considering field reads to return non-null values
    /// </summary>
    public virtual bool FieldReadAsNonNull(CompilerOptions options, Field f) {
      return false;
    }

    public virtual bool IsNonNullType(TypeNode t){
      if (t == null) return false;
      Reference rt = t as Reference;
      if (rt != null) t = rt.ElementType;
      for(;;){
        switch(t.NodeType){
          case NodeType.OptionalModifier: 
            if (((TypeModifier)t).Modifier == SystemTypes.NonNullType) return true;
            //if (((TypeModifier)t).Modifier == SystemTypes.NullableType) return false; // 'T!?' is 'T?'
            t = ((TypeModifier)t).ModifiedType; 
            break;
          case NodeType.RequiredModifier: t = ((TypeModifier)t).ModifiedType; break;
          default: return false;
        }
      }
    }

    public virtual bool IsPossibleNonNullType(TypeNode t) {
      bool rvalue =  IsNonNullType(t);
      rvalue = rvalue || (t.IsTemplateParameter && !IsTQuestionMark(t));
      rvalue = rvalue && (!t.IsValueType);
      return rvalue;
    }

    public virtual bool IsTQuestionMark(TypeNode t) {
      if (t == null) return false;
      Reference rt = t as Reference;
      if (rt != null) t = rt.ElementType;
      if (!t.IsTemplateParameter) {
      }
      for (; ; ) {
        switch (t.NodeType) {
          case NodeType.OptionalModifier:
            if (((TypeModifier)t).Modifier == SystemTypes.NullableType) return true;
            if (((TypeModifier)t).Modifier == SystemTypes.NonNullType) return false; // 'T?!' is not T? 
            t = ((TypeModifier)t).ModifiedType;
            break;
          case NodeType.RequiredModifier: t = ((TypeModifier)t).ModifiedType; break;
          default: return false;
        }
      }
    }
    /// <summary>
    /// Should return false if null cannot be converted to targetType.
    /// </summary>
    /// <returns></returns>
    public virtual bool IsCompatibleWithNull(TypeNode targetType) {
      if (IsNullableType(targetType)) return true;
      if (targetType.IsValueType) return false;
      if (this.IsNonNullType(targetType)) return false;
      if (targetType.IsTemplateParameter && !targetType.IsReferenceType) {
        return false; 
      }
      return true;
    }
    public virtual bool IsNullableType(TypeNode t) {
      if (t == null) return false;
      return t.Template != null && t.Template == SystemTypes.GenericNullable && t.TemplateArguments != null && t.TemplateArguments.Count == 1;
    }

    public virtual Expression ExplicitCoercion(Expression source, TypeNode targetType){
      return this.ExplicitCoercion(source, targetType, null);
    }
    public virtual Expression ExplicitCoercion(Expression source, TypeNode targetType, TypeViewer typeViewer){
      if (targetType == null || targetType.Name == Looker.NotFound) return source;
      TypeNode originalTargetType = targetType;
      if (source == null) return null;
      Literal sourceLit = source as Literal;
      if (sourceLit != null && sourceLit.Value is TypeNode){
        this.HandleError(source, Error.TypeInVariableContext, this.GetTypeName((TypeNode)sourceLit.Value), "class", "variable");
        return null;
      }
      //Ignore parentheses
      if (source.NodeType == NodeType.Parentheses){
        UnaryExpression uex = (UnaryExpression)source;
        uex.Operand = this.ExplicitCoercion(uex.Operand, targetType, typeViewer);
        if (uex.Operand == null) return null;
        uex.Type = uex.Operand.Type;
        return uex;
      }
      bool targetIsNonNullType = this.IsNonNullType(targetType);
      targetType = TypeNode.StripModifiers(targetType);
      //TODO: handle EnforceCheck and SkipCheck
      //Special case for closure expressions
      if (source.NodeType == NodeType.AnonymousNestedFunction)
        return this.CoerceAnonymousNestedFunction((AnonymousNestedFunction)source, targetType, true, typeViewer);
      TypeNode sourceType = source.Type;
      if (sourceType == null) sourceType = SystemTypes.Object;
      bool sourceIsNonNullType = this.IsNonNullType(sourceType);
      sourceType = TypeNode.StripModifiers(sourceType);
      Expression result = this.StandardExplicitCoercion(source, sourceIsNonNullType, sourceType, targetIsNonNullType, targetType, originalTargetType, typeViewer);
      if (result != null) return result;
      Method coercion = this.UserDefinedExplicitCoercionMethod(source, sourceType, targetType, true, originalTargetType, typeViewer);
      if (coercion != null){
        if (this.IsNullableType(targetType) && this.IsNullableType(sourceType) && !this.IsNullableType(coercion.Parameters[0].Type))
          return this.CoerceWithLiftedCoercion(source, sourceType, targetType, coercion, true, typeViewer);
        Expression arg = this.StandardExplicitCoercion(source, sourceIsNonNullType, sourceType, this.IsNonNullType(coercion.Parameters[0].Type), coercion.Parameters[0].Type, coercion.Parameters[0].Type, typeViewer);
        if (arg != null){
          ExpressionList args = new ExpressionList(arg);        
          Expression e = new MethodCall(new MemberBinding(null, coercion), args, NodeType.Call, coercion.ReturnType);
          if (coercion.ReturnType != targetType){
            if (targetType is TypeAlias)
              e = this.ExplicitCoercion(e, targetType, typeViewer);
            else
              e = this.StandardExplicitCoercion(e, this.IsNonNullType(coercion.ReturnType), coercion.ReturnType, targetIsNonNullType, targetType, originalTargetType, typeViewer);
          }
          if (e != null) return e;
        }
      }
      if (this.IsNullableType(sourceType)) {
        source = this.ExplicitCoercion(source, this.RemoveNullableWrapper(sourceType), typeViewer);
        return this.ExplicitCoercion(source, targetType, typeViewer);
      }
      if (this.IsNullableType(targetType) && sourceType.IsValueType) {
        source = this.ExplicitCoercion(source, this.RemoveNullableWrapper(targetType), typeViewer);
        return this.ExplicitCoercion(source, targetType, typeViewer);
      }
      if (sourceType == SystemTypes.Type && source is Literal)
        this.HandleError(source, Error.TypeInVariableContext, this.GetTypeName((TypeNode)((Literal)source).Value), "class", "variable");
      else if (Literal.IsNullLiteral(source)){
        if (this.IsNullableType(targetType))
          return new Local(StandardIds.NewObj, targetType, source.SourceContext);
        this.HandleError(source, Error.CannotCoerceNullToValueType, this.GetTypeName(targetType));
      }else
        this.HandleError(source, Error.NoExplicitCoercion, this.GetTypeName(sourceType), this.GetTypeName(targetType));
      return null;
    }
    public virtual Expression ExplicitLiteralCoercion(Literal lit, TypeNode sourceType, TypeNode targetType, TypeViewer typeViewer){
      try{
        object val = System.Convert.ChangeType(lit.Value, targetType.TypeCode);
        return new Literal(val, targetType, lit.SourceContext);
      }catch(InvalidCastException){
      }catch(OverflowException){
      }catch(FormatException){}
      return this.ExplicitCoercion(lit, targetType, typeViewer);
    }
    public virtual TypeNode SmallestIntegerType(object value){
      IConvertible convertible = value as IConvertible;
      if (convertible == null){Debug.Assert(false); return null;}
      switch (convertible.GetTypeCode()){
        case TypeCode.Byte:
        case TypeCode.UInt16:
        case TypeCode.UInt32:
        case TypeCode.UInt64:
          ulong ul = convertible.ToUInt64(null);
          if (ul <= Byte.MaxValue) return SystemTypes.UInt8;
          if (ul <= UInt16.MaxValue) return SystemTypes.UInt16;
          if (ul <= UInt32.MaxValue) return SystemTypes.UInt32;
          return SystemTypes.UInt64;
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
        case TypeCode.Int64:
          long l = convertible.ToInt64(null);
          if (l >= 0){
            if (l <= Byte.MaxValue) return SystemTypes.UInt8;
            if (l <= UInt16.MaxValue) return SystemTypes.UInt16;
            if (l <= UInt32.MaxValue) return SystemTypes.UInt32;
            return SystemTypes.UInt64;
          }else{
            if (l >= SByte.MinValue) return SystemTypes.Int8;
            if (l >= Int16.MinValue) return SystemTypes.Int16;
            if (l >= Int32.MinValue) return SystemTypes.Int32;
            return SystemTypes.Int64;
          }
        default:
          Debug.Assert(false); return null;
      }
    }
    protected virtual Expression StandardExplicitCoercion(Expression source, bool sourceIsNonNullType,  TypeNode sourceType, bool targetIsNonNullType, TypeNode targetType, TypeNode originalTargetType, TypeViewer typeViewer){
      ErrorHandler savedErrorHandler = this.ErrorHandler;
      this.ErrorHandler = null;
      Expression result;
      try { result = this.StandardExplicitCoercionHelper(savedErrorHandler, source, sourceIsNonNullType, sourceType, targetIsNonNullType, targetType, originalTargetType, typeViewer); }
      finally {
        this.ErrorHandler = savedErrorHandler;
      };
      return result;
    }
    /// <summary>
    /// This method only handles the coercion from possibly null to non-null. It does not do any Class-Type coercions!
    /// </summary>
    public Expression ExplicitNonNullCoercion(Expression source, TypeNode targetType) {
      if (targetType == null) return null;
      TypeNode strippedSourceType = TypeNode.StripModifier(source.Type, SystemTypes.NonNullType);
      Method m;
      if (targetType.IsPointerType) {
        m = SystemTypes.NonNullType.GetMethod(Identifier.For("AssertNotNull"), SystemTypes.UIntPtr);
      }
      else if (this.useGenerics) {
        m = GetGenericAssertNotNullMethod(strippedSourceType);
      }
      else {
        m = SystemTypes.NonNullType.GetMethod(Identifier.For("AssertNotNull"), SystemTypes.Object);
      }
      if (m == null) return source;
      return CreateCoercionBlock(source, m, strippedSourceType, targetType);
    }
    private static Method GetGenericAssertNotNullMethod(TypeNode argumentType) {
      Method m = null;
      MemberList ml = SystemTypes.NonNullType.GetMembersNamed(Identifier.For("AssertNotNullGeneric"));
      if (ml != null && ml.Count > 0) {
        m = ml[0] as Method;
        Debug.Assert(m != null);
        m = m.GetTemplateInstance(m.DeclaringType, argumentType);
      }
      return m;
    }
    /// <summary>
    /// This method only performs the non-null coercion, no other type coercions
    /// </summary>
    public Expression ImplicitNonNullCoercion(ErrorHandler handler, Expression source, TypeNode targetType) {
      if (targetType == null) return null;
      Literal lit = source as Literal;
      if (lit != null && lit.Value == null) {
        // will always fail. Emit error.
        if (handler != null) { handler.HandleError(source, Error.CannotCoerceNullToNonNullType); }
        // turn into explicit coercion so nonnull checker will stay quiet
        return ExplicitNonNullCoercion(source, targetType);
      }
      TypeNode strippedSourceType = TypeNode.StripModifier(source.Type, SystemTypes.NonNullType);
      Method testMethod;
      if (targetType.IsPointerType) {
        testMethod = SystemTypes.NonNullType.GetMethod(Identifier.For("AssertNotNullImplicit"), SystemTypes.UIntPtr);
      }
      else if (this.useGenerics) {
        testMethod = GetGenericAssertNotNullImplicitMethod(strippedSourceType);
      } 
      else
      {
        testMethod = SystemTypes.NonNullType.GetMethod(Identifier.For("AssertNotNullImplicit"), SystemTypes.Object);
      }
      return CreateCoercionBlock(source, testMethod, strippedSourceType, targetType);
    }
    private static Expression CreateCoercionBlock(Expression source, Method coercionMethod, TypeNode strippedSourceType, TypeNode targetType) {
      Block block = new Block(new StatementList(3));
      // if the expression is a local or a literal, we don't need a fresh local.
      Expression temp;
      if (source is Variable || source is Literal) {
        temp = source;
      }
      else {
        Local loc = new Local(strippedSourceType, source.SourceContext); // don't yet have non-null value
        block.Statements.Add(new AssignmentStatement(loc, source));
        temp = loc;
      }
      block.Statements.Add(new ExpressionStatement(new MethodCall(new MemberBinding(null, coercionMethod), new ExpressionList(temp), NodeType.Call, SystemTypes.Void, source.SourceContext), source.SourceContext));
      block.Statements.Add(new ExpressionStatement(temp));
      return new BlockExpression(block, targetType, source.SourceContext);
    }

    private static Method GetGenericAssertNotNullImplicitMethod(TypeNode argumentType) {
      Method m = null;
      MemberList ml = SystemTypes.NonNullType.GetMembersNamed(Identifier.For("AssertNotNullImplicitGeneric"));
      if (ml != null && ml.Count > 0) {
        m = ml[0] as Method;
        Debug.Assert(m != null);
        m = m.GetTemplateInstance(m.DeclaringType, argumentType);
      }
      return m;
    }

    protected virtual Expression StandardExplicitCoercionHelper(ErrorHandler savedErrorHandler, Expression source, bool sourceIsNonNullType, TypeNode sourceType, bool targetIsNonNullType, TypeNode targetType,
      TypeNode originalTargetType, TypeViewer typeViewer){
      //Identity coercion
      if (sourceType == targetType && (!targetIsNonNullType || (targetIsNonNullType == sourceIsNonNullType))){
        if (sourceType.IsPrimitiveNumeric){
          Expression e = this.ExplicitPrimitiveCoercionHelper(source, sourceType, targetType);
          if (e == null) return source;
          e.Type = targetType;
          return e;
        }
        return source;
      }
      //Dereference source
      Reference sr = sourceType as Reference;
      if (sr != null && targetType is Pointer) return source;
      if (sr != null && source.NodeType != NodeType.AddressOf){
        sourceType = sr.ElementType;
        source = new AddressDereference(source, sourceType, source.SourceContext);
        sourceType = TypeNode.StripModifier(sourceType, SystemTypes.NonNullType);
      }
      //Get rid of Nullable wrapper
      if (this.IsNullableType(sourceType)){
        Method getValue = TypeViewer.GetTypeView(typeViewer, sourceType).GetMethod(Identifier.For("get_Value"));
        sourceType = this.RemoveNullableWrapper(sourceType);
        source = new MethodCall(new MemberBinding(source, getValue), null, NodeType.Call, sourceType, source.SourceContext);
      }
      //Identity coercion after dereference
      if (sourceType == targetType && (!targetIsNonNullType || (targetIsNonNullType == sourceIsNonNullType))){
        if (sourceType.IsPrimitiveNumeric){
          source = this.ExplicitPrimitiveCoercionHelper(source, sourceType, targetType);
          if (source == null) return null;
          source.Type = targetType;
        }
        return source;
      }
      //Special case for type union
      if (sourceType.NodeType == NodeType.TypeUnion)
        return this.CoerceFromTypeUnion(source, (TypeUnion)sourceType, targetType, true, originalTargetType, typeViewer);
      //Special case for null literal
      if (source.NodeType == NodeType.Literal && ((Literal)source).Value == null){
        if (targetType.IsTemplateParameter && !targetType.IsReferenceType) {
          // Check for reference constraint
          //this.HandleError(source, Error.TypeVarCantBeNull, targetType.Name.Name);
          return null;
        }
        if (targetIsNonNullType) {
          //savedErrorHandler.HandleError(source, Error.CannotCoerceNullToNonNullType, savedErrorHandler.GetTypeName(targetType));
          return ExplicitNonNullCoercion(source, originalTargetType);
        }

        if (targetType is ITypeParameter && sourceType == SystemTypes.Object && this.useGenerics)
          return new BinaryExpression(source, new Literal(targetType, SystemTypes.Type), NodeType.UnboxAny);
        if (!targetType.IsValueType || targetType.Template == SystemTypes.GenericBoxed) 
          return new Literal(null, targetType, source.SourceContext);
        TypeAlias tAlias = targetType as TypeAlias;
        if (tAlias != null){
          source = this.ExplicitCoercion(source, tAlias.AliasedType, typeViewer);
          if (source == null) return null;
          Method coercion = this.UserDefinedExplicitCoercionMethod(source, tAlias.AliasedType, targetType, false, originalTargetType, typeViewer);
          if (coercion != null){
            ExpressionList args = new ExpressionList(this.ImplicitCoercion(source, coercion.Parameters[0].Type, typeViewer));
            return new MethodCall(new MemberBinding(null, coercion), args, NodeType.Call, coercion.ReturnType);
          }
        }
        return null;
      }
      //Explicit reference coercions
      if (TypeViewer.GetTypeView(typeViewer, sourceType).IsAssignableTo(targetType)){
        //Handling for non null types
        if (targetIsNonNullType && !sourceIsNonNullType && !sourceType.IsValueType) {
          //savedErrorHandler.HandleError(source, Error.CoercionToNonNullTypeMightFail, savedErrorHandler.GetTypeName(targetType));
          return ExplicitNonNullCoercion(source, originalTargetType);
        }
        if (targetType == SystemTypes.Object && sourceType.Template == SystemTypes.GenericIEnumerable)
          return this.CoerceStreamToObject(source, sourceType, typeViewer);
        if (sourceType.IsValueType && !targetType.IsValueType){
          if (sourceType.NodeType == NodeType.TypeUnion){
            Debug.Assert(targetType == SystemTypes.Object);
            return this.CoerceTypeUnionToObject(source, typeViewer);
          }
          if (sourceType is TupleType){
            if (targetType == SystemTypes.Object)
              return this.TupleCoercion(source, sourceType, targetType, true, typeViewer);
          }else if (this.GetStreamElementType(sourceType, typeViewer) != sourceType)
            return this.ExplicitCoercion(this.CoerceStreamToObject(source, sourceType, typeViewer), targetType, typeViewer);
          return new BinaryExpression(source, new MemberBinding(null, sourceType), NodeType.Box, targetType);
        }else if (this.useGenerics && sourceType is ITypeParameter){
          source = new BinaryExpression(source, new MemberBinding(null, sourceType), NodeType.Box, sourceType);
          if (targetType == SystemTypes.Object) return source;
          return new BinaryExpression(source, new MemberBinding(null, targetType), NodeType.UnboxAny, targetType);
        }else
          return source;
      }
      //Special case for typed streams
      Expression streamCoercion = this.StreamCoercion(source, sourceType, targetType, true, originalTargetType, typeViewer);
      if (streamCoercion != null) return streamCoercion;
      //Down casts
      if (!targetType.IsPointerType && !sourceType.IsPointerType && TypeViewer.GetTypeView(typeViewer, targetType).IsAssignableTo(sourceType)){
        if (!sourceType.IsValueType){
          if (targetType.NodeType == NodeType.TypeUnion)
            return this.CoerceObjectToTypeUnion(source, (TypeUnion)targetType, typeViewer);
          if (source.NodeType == NodeType.Literal && ((Literal)source).Value == null)
            return source;
          if (targetType.IsValueType){
            Expression e = new AddressDereference(new BinaryExpression(source, new MemberBinding(null, targetType), NodeType.Unbox, targetType.GetReferenceType()), targetType);
            e.Type = targetType;
            return e;
          }else{
            NodeType op = this.useGenerics && (targetType is ClassParameter || targetType is TypeParameter) ? NodeType.UnboxAny : NodeType.Castclass;
            Expression e = new BinaryExpression(source, new MemberBinding(null, targetType), op, source.SourceContext);
            e.Type = originalTargetType;
            //Handling for non null types
            if (targetIsNonNullType && !sourceIsNonNullType){
              //savedErrorHandler.HandleError(source, Error.CoercionToNonNullTypeMightFail, savedErrorHandler.GetTypeName(targetType));
              return ExplicitNonNullCoercion(e, originalTargetType);
            }
            return e;
          }
        }
      }
      //Special case for casts to and from interfaces
      if ((sourceType.NodeType == NodeType.Interface && !targetType.IsSealed) ||
        (targetType.NodeType == NodeType.Interface && !(sourceType.IsSealed || (sourceType is Pointer) || 
        (sourceType is ArrayType && this.GetStreamElementType(targetType, typeViewer) == targetType)))){
        Expression e = new BinaryExpression(source, new MemberBinding(null, targetType), NodeType.Castclass, source.SourceContext);
        e.Type = targetType;
        return e;
      }
      //Explicit numeric coercions + explicit enumeration coercions + explicit constant expression coercions
      Expression primitiveConversion = this.ExplicitPrimitiveCoercion(source, sourceType, targetType);
      if (primitiveConversion != null) return primitiveConversion;
      //Special case for decimal
      if (targetType == SystemTypes.Decimal && this.ImplicitCoercionFromTo(sourceType, targetType, typeViewer))
        return this.ImplicitCoercion(source, targetType, typeViewer);
      //Special case for delegates
      if (targetType is DelegateNode)
        return this.CoerceToDelegate(source, sourceType, (DelegateNode)targetType, true, typeViewer);
      //Special case for type union
      if (targetType.NodeType == NodeType.TypeUnion)
        return this.CoerceToTypeUnion(source, sourceType, (TypeUnion)targetType, typeViewer);
      //Special case for Type intersection target type
      if (targetType.NodeType == NodeType.TypeIntersection)
        return this.CoerceToTypeIntersection(source, sourceType, (TypeIntersection)targetType, false, typeViewer);
      //Tuple coercion
      return this.TupleCoercion(source, sourceType, targetType, true, typeViewer);
    }
    protected virtual Expression StreamCoercion(Expression source, TypeNode sourceType, TypeNode targetType, bool explicitCoercion,
      TypeNode originalTargetType, TypeViewer typeViewer){
      TypeNode tTemplate = targetType.Template;
      if (tTemplate != null){
        if (tTemplate == SystemTypes.GenericBoxed || tTemplate == SystemTypes.GenericNonNull || tTemplate == SystemTypes.GenericInvariant)
          return this.CoerceAndBox(source, sourceType, targetType, explicitCoercion, typeViewer);
        if (tTemplate == SystemTypes.GenericIEnumerable)
          return this.CoercionToIEnumerable(source, sourceType, targetType, explicitCoercion, typeViewer);
        if (tTemplate == SystemTypes.GenericNonEmptyIEnumerable)
          return this.CoercionToNonEmptyIEnumerable(source, sourceType, targetType, explicitCoercion, typeViewer);
      }
      TypeNode sTemplate = sourceType.Template;
      if (sTemplate != null){
        if (sTemplate == SystemTypes.GenericBoxed || sTemplate == SystemTypes.GenericNonNull || sTemplate == SystemTypes.GenericInvariant)
          return this.UnboxAndCoerce(source, sourceType, targetType, explicitCoercion, typeViewer);
        if (sTemplate == SystemTypes.GenericIEnumerable)
          return this.CoercionFromIEnumerable(source, sourceType, targetType, explicitCoercion, originalTargetType, typeViewer);
        if (sTemplate == SystemTypes.GenericNonEmptyIEnumerable)
          return this.CoercionFromNonEmptyIEnumerable(source, sourceType, targetType, explicitCoercion, originalTargetType, typeViewer);
      }
      return null;
    }
    public virtual Expression CoerceAndBox(Expression source, TypeNode sourceType, TypeNode targetType, bool explicitCoercion, TypeViewer typeViewer){
      if (source == null || sourceType == null || targetType == null) return null;
      Debug.Assert(targetType.Template == SystemTypes.GenericBoxed || targetType.Template == SystemTypes.GenericNonNull || targetType.Template == SystemTypes.GenericInvariant);
      TypeNode tElementType = this.GetStreamElementType(targetType, typeViewer);
      if (tElementType == targetType || tElementType == null){Debug.Assert(false); return null;}
      //TODO: if source type is also a boxed type, instantiate a wrapper around the boxed value, i.e. call GetBoxed
      if (explicitCoercion)
        source = this.ExplicitCoercion(source, tElementType, typeViewer);
      else{
        if (targetType.Template == SystemTypes.GenericBoxed || sourceType.Template == SystemTypes.GenericInvariant)
          source = this.ImplicitCoercion(source, tElementType, typeViewer);
        else{
          Debug.Assert(sourceType != targetType);
          //Only allow this if T is really T-, which only happens for constructor calls
          Construct constr = source as Construct;
          if (constr == null || constr.Type != tElementType) return null;
        }
      }
      if (source == null) return null;
      InstanceInitializer cons = TypeViewer.GetTypeView(typeViewer, targetType).GetConstructor(tElementType);
      return new Construct(new MemberBinding(null, cons), new ExpressionList(source), targetType);
    }

    /// <summary>
    /// This method wrap every return expression in the "body" with
    /// an implicit coercion. 
    /// Precondition is that the return expressions are of a type implicitly
    /// coercable to the "targetReturnType"
    /// </summary>
    /// <param name="body">Block of statements where the return expressions
    /// are supposed to be changed.</param>
    /// <param name="targetReturnType">
    /// The type to which the return expressions will be coerced.
    /// </param>
    /// <returns></returns>
    private Block ImplicitCoercionReturnExp(Block body, TypeNode targetReturnType) {
      if (body == null || body.Statements == null) {
        return body;
      }

      ImplicitCoercionReturnExpVisitor visitor = new ImplicitCoercionReturnExpVisitor(this, targetReturnType);
      return visitor.VisitBlock(body);
    }

    /// <summary>
    /// For each return expression it sees, this visitor try to wrap it
    /// with an implicit coercion method call, if exists. 
    /// </summary>
    internal class ImplicitCoercionReturnExpVisitor : StandardVisitor {
      TypeNode targetReturnType;
      TypeSystem typeSystem;

      public ImplicitCoercionReturnExpVisitor(TypeSystem ts, TypeNode targetReturnType) {
        typeSystem = ts;
        this.targetReturnType = targetReturnType;
      }

      public override Statement VisitReturn(Return Return) {
        Return.Expression = typeSystem.ImplicitCoercion(Return.Expression, targetReturnType);
        return base.VisitReturn(Return);
      }
    }

    public virtual Expression CoerceAnonymousNestedFunction(AnonymousNestedFunction func, TypeNode targetType, bool explicitCoercion, TypeViewer typeViewer) {
      if (func.Invocation != null){
        Debug.Assert(!explicitCoercion);
        if (func.Type != targetType && this.ImplicitCoercionFromTo(func.Invocation.Type, targetType)){
          func.Type = targetType;
          func.Invocation = this.ImplicitCoercion(func.Invocation, targetType, typeViewer);
        }
        return func;
      }
      DelegateNode targetDType = targetType as DelegateNode;
      if (targetDType != null) {
        //TODO: if func is nested in a generic method and delegate is generic instance
        Method meth = func.Method;
        if (meth == null) return null;
        //We wrap each return expression of the anonymous method with an implicit 
        //coercion
        if (TypeNode.StripModifiers(meth.ReturnType) != TypeNode.StripModifiers(targetDType.ReturnType)) {
          if (this.ImplicitCoercionFromTo(meth.ReturnType, targetDType.ReturnType)) {
            func.Type = targetType;
            meth.ReturnType = targetDType.ReturnType;
            meth.Body = ImplicitCoercionReturnExp(meth.Body, targetDType.ReturnType);
          }
          else {
            this.HandleError(func, Error.NestedFunctionDelegateReturnTypeMismatch, this.GetTypeName(targetDType));
            return func;
          }
        }
        if (func.Parameters == null && targetDType.Parameters != null) {
          ParameterList parameters = meth.Parameters = targetDType.Parameters.Clone();
          for (int i = 0, n = parameters.Count; i < n; i++) {
            Parameter par = parameters[i];
            if (par == null) continue;
            if (par.IsOut || (par.Type != null && par.Type is Reference)) {
              this.HandleError(func, Error.NestedFunctionDelegateParameterMismatchBecauseOfOutParameter, this.GetTypeName(targetDType));
              return func;
            }
            parameters[i] = par = (Parameter)par.Clone();
            par.DeclaringMethod = meth;
          }
        }
        else {
          ParameterList savedMethodParams = meth.Parameters;
          meth.Parameters = func.Parameters;
          if (!meth.ParametersMatch(targetDType.Parameters)) {
            this.HandleError(func, Error.NestedFunctionDelegateParameterMismatch, this.GetTypeName(targetDType));
            return func;
          }
          meth.Parameters = savedMethodParams;
        }
        InstanceInitializer dcons = TypeViewer.GetTypeView(typeViewer, targetType).GetConstructor(SystemTypes.Object, SystemTypes.IntPtr);
        if (dcons == null) return func;
        MemberBinding memb = new MemberBinding(null, meth);
        memb.Type = null;
        Expression ldftn = new UnaryExpression(memb, NodeType.Ldftn);
        ldftn.Type = SystemTypes.IntPtr;
        ExpressionList arguments = new ExpressionList(2);
        if (meth.IsStatic)
          arguments.Add(Literal.Null);
        else
          arguments.Add(new CurrentClosure(meth, meth.DeclaringType));
        arguments.Add(ldftn);
        Construct cons = new Construct(new MemberBinding(null, dcons), arguments, targetType);
        cons.SourceContext = func.SourceContext;
        func.Invocation = cons;
        func.Type = targetType;
        return func;
      }
      else {
        Method invoker = func.Method;
        if (invoker != null && (invoker.Parameters == null || invoker.Parameters.Count == 0)) {
          Expression ob = invoker.IsStatic ? null : new CurrentClosure(invoker, invoker.DeclaringType);
          Expression call = new MethodCall(new MemberBinding(ob, invoker), null, NodeType.Call, invoker.ReturnType, func.SourceContext);
          if (explicitCoercion)
            func.Invocation = this.ExplicitCoercion(call, targetType, typeViewer);
          else
            func.Invocation = this.ImplicitCoercion(call, targetType, typeViewer);
          func.Type = targetType;
        }
        else {
          func.Invocation = null;
          Error e = explicitCoercion ? Error.NoExplicitCoercion : Error.NoImplicitCoercion;
          FunctionType ftype = FunctionType.For(func.Method.ReturnType, func.Method.Parameters, this.currentType);
          Node n = new Expression(NodeType.Nop);
          n.SourceContext = invoker.Parameters[0].SourceContext;
          n.SourceContext.EndPos = func.Body.SourceContext.EndPos;
          this.HandleError(n, e, this.GetMemberSignature(ftype), this.GetTypeName(targetType));
        }
        return func;
      }
    }
    public virtual Expression CoerceToDelegate(Expression source, TypeNode sourceType, DelegateNode targetType, bool explicitCoercion, TypeViewer typeViewer){
      if (source == null || sourceType == null || targetType == null) return null;
      if (!(sourceType is DelegateNode || sourceType is FunctionType)) return null;
      DelegateNode sourceDType = sourceType as DelegateNode;
      MemberList invokeMembers = TypeViewer.GetTypeView(typeViewer, sourceType).GetMembersNamed(StandardIds.Invoke);
      for (int i = 0, n = invokeMembers.Count; i < n; i++){
        Method invoke = invokeMembers[i] as Method;
        if (invoke == null) continue;
        //TODO: if signature is not the same, but can be coerced, emit an adapter function
        if (invoke.ReturnType != targetType.ReturnType) continue;
        if (!invoke.ParametersMatch(targetType.Parameters)) continue;
        InstanceInitializer dcons = TypeViewer.GetTypeView(typeViewer, targetType).GetConstructor(SystemTypes.Object, SystemTypes.IntPtr);
        if (dcons == null) return null;
        MemberBinding memb = new MemberBinding(source, invoke);
        memb.Type = null; 
        Expression ldftn = new UnaryExpression(memb, NodeType.Ldftn);
        ldftn.Type = SystemTypes.IntPtr;
        ExpressionList arguments = new ExpressionList(2);
        arguments.Add(source);
        arguments.Add(ldftn);
        Construct cons = new Construct(new MemberBinding(null, dcons), arguments, targetType);
        cons.SourceContext = source.SourceContext;
        return cons;
      }
      return null;
    }
    public virtual Expression CoerceToIndex(Expression index, TypeViewer typeViewer){
      if (index == null) return null;
      TypeNode indexType = index.Type;
      if (indexType == SystemTypes.Int32) return index;
      if (indexType == SystemTypes.IntPtr) return index;
      if (indexType == SystemTypes.UIntPtr) return index;
      bool savedCheckOverflow = this.checkOverflow;
      this.checkOverflow = true;
      if (this.ImplicitCoercionFromTo(indexType, SystemTypes.Int32, typeViewer))
        return this.ImplicitCoercion(index, SystemTypes.Int32, typeViewer);
      else if (this.ImplicitCoercionFromTo(indexType, SystemTypes.UInt32, typeViewer)){
        index = this.ImplicitCoercion(index, SystemTypes.UInt32, typeViewer);
        this.checkOverflow = savedCheckOverflow; //If this overflows, it becomes negative and the array instruction will throw
        return this.ExplicitPrimitiveCoercion(index, index.Type, SystemTypes.UIntPtr);
      }else if (this.ImplicitCoercionFromTo(indexType, SystemTypes.Int64, typeViewer))
        index = this.ImplicitCoercion(index, SystemTypes.Int64, typeViewer);
      else if (this.ImplicitCoercionFromTo(indexType, SystemTypes.UInt64, typeViewer))
        index = this.ImplicitCoercion(index, SystemTypes.UInt64, typeViewer);
      else{
        this.checkOverflow = savedCheckOverflow;
        return this.ImplicitCoercion(index, SystemTypes.Int32, typeViewer); //Reports the error
      }
      index = this.ExplicitPrimitiveCoercion(index, index.Type, SystemTypes.IntPtr);
      this.checkOverflow = savedCheckOverflow;
      return index;
    }
    public virtual Expression CoercionToIEnumerable(Expression source, TypeNode sourceType, TypeNode targetType, bool explicitCoercion, TypeViewer typeViewer){
      if (source == null || sourceType == null || targetType == null) return null;
      if (sourceType == SystemTypes.Object) return null;
      SourceContext sctx = source.SourceContext;
      TypeNode sElementType = this.GetStreamElementType(sourceType, typeViewer);
      TypeNode tElementType = this.GetStreamElementType(targetType, typeViewer);
      if (!explicitCoercion && !this.ImplicitCoercionFromTo(sElementType, tElementType, typeViewer)){
        if (tElementType is TupleType && sourceType is TupleType)
          sElementType = sourceType;
        else
          return null;
      }
      //S[] -> T*
      if (sourceType is ArrayType) return null; //TODO: provide a wrapper
      //S- -> S
      if (sourceType.Template == SystemTypes.GenericInvariant){
        source = new MethodCall(new MemberBinding(null, TypeViewer.GetTypeView(typeViewer, sourceType).GetMethod(StandardIds.opImplicit, sourceType)), 
          new ExpressionList(source), NodeType.Call, sElementType);
        sourceType = sElementType;
      }
      //S -> S?
      if (sElementType == sourceType){
        sourceType = SystemTypes.GenericBoxed.GetTemplateInstance(this.currentType, sourceType);
        if (sourceType == null) return null;
        source = this.ImplicitCoercion(source, sourceType, typeViewer);
        if (source == null) return null;
      }
      // (S! | S? | S+ | S*) -> T*
      TypeNode sStreamType = SystemTypes.GenericIEnumerable.GetTemplateInstance(this.currentType, sElementType);
      if (sStreamType == null) return null;
      Debug.Assert(TypeViewer.GetTypeView(typeViewer, sourceType).IsAssignableTo(sStreamType));
      if (sStreamType == targetType){
        if (sourceType.IsValueType)
          source = new BinaryExpression(source, new MemberBinding(null, sourceType), NodeType.Box, targetType);
        return source;
      }

      Coercer coercer = null;
      if (explicitCoercion)
        coercer = new Coercer(this.ExplicitCoercionAdapter);
      else
        coercer = new Coercer(this.ImplicitCoercionAdapter);
      TypeNode streamAdapter = StreamAdapter.For((Interface)sStreamType, (Interface)targetType, this.currentType, coercer, sctx);
      if (streamAdapter == null) return null;
      if (sourceType.IsValueType)
        source = new BinaryExpression(source, new MemberBinding(null, sourceType), NodeType.Box, targetType);
      return new Construct(new MemberBinding(null, TypeViewer.GetTypeView(typeViewer, streamAdapter).GetConstructor(sStreamType)), new ExpressionList(source), targetType);
    }
    public Expression ExplicitCoercionAdapter(Expression source, TypeNode targetType, TypeViewer typeViewer){
      ErrorHandler savedErrorHandler = this.ErrorHandler;
      this.ErrorHandler = null;
      Expression e;
      try
      {
        e = this.ExplicitCoercion(source, targetType, typeViewer);
        if (e is CoerceTuple || e is BlockExpression || e is ConstructTuple)
        {
          Normalizer n = new Normalizer(this);
          e = n.VisitExpression(e);
        }
      }
      finally
      {
        this.ErrorHandler = savedErrorHandler;
      }
      return e;
    }
    public Expression ImplicitCoercionAdapter(Expression source, TypeNode targetType, TypeViewer typeViewer){
      ErrorHandler savedErrorHandler = this.ErrorHandler;
      this.ErrorHandler = null;
      Expression e = null;
      try
      {
        e = this.ImplicitCoercion(source, targetType, typeViewer);
        if (e is CoerceTuple || e is BlockExpression || e is ConstructTuple)
        {
          Normalizer n = new Normalizer(this);
          e = n.VisitExpression(e);
        }
      }
      finally
      {
        this.ErrorHandler = savedErrorHandler;
      };
      return e;
    }
    public virtual Expression CoercionToNonEmptyIEnumerable(Expression source, TypeNode sourceType, TypeNode targetType, bool explicitCoercion, TypeViewer typeViewer){
      if (source == null || sourceType == null || targetType == null) return null;
      if (sourceType == SystemTypes.Object) return null;
      TypeNode sElementType = this.GetStreamElementType(sourceType, typeViewer);
      TypeNode tElementType = this.GetStreamElementType(targetType, typeViewer);
      if (!explicitCoercion && !this.ImplicitCoercionFromTo(sElementType, tElementType, typeViewer)) return null;
      //S- -> S -> T -> T+
      if (sourceType.Template == SystemTypes.GenericInvariant){
        source = new MethodCall(new MemberBinding(null, TypeViewer.GetTypeView(typeViewer, sourceType).GetMethod(StandardIds.opImplicit, sourceType)), 
          new ExpressionList(source), NodeType.Call, sElementType);
        if (explicitCoercion)
          source = this.ExplicitCoercion(source, tElementType, typeViewer);
        else
          source = this.ImplicitCoercion(source, tElementType, typeViewer);
        if (source == null) return null;
        return new Construct(new MemberBinding(null, TypeViewer.GetTypeView(typeViewer, targetType).GetConstructor(tElementType)), new ExpressionList(source), targetType);
      }
      //S -> S+ (if explicit or value type)
      if (sElementType == sourceType){
        if (!sourceType.IsValueType && !explicitCoercion) return null;
        if (sourceType != tElementType) source = this.ExplicitCoercion(source, tElementType, typeViewer);
        if (source == null) return null;
        return new Construct(new MemberBinding(null, TypeViewer.GetTypeView(typeViewer, targetType).GetConstructor(tElementType)), new ExpressionList(source), targetType);
      }
      // S* -> S+ (if explicit)
      if (sourceType.Template != SystemTypes.GenericNonEmptyIEnumerable && !explicitCoercion)
        //TODO: handle new T[] and new T{}
        return null;
      TypeNode sStreamType = SystemTypes.GenericIEnumerable.GetTemplateInstance(this.currentType, sElementType);
      Debug.Assert(TypeViewer.GetTypeView(typeViewer, sourceType).IsAssignableTo(sStreamType));
      TypeNode tStreamType = SystemTypes.GenericIEnumerable.GetTemplateInstance(this.currentType, tElementType);
      if (sStreamType != tStreamType){
        Coercer coercer = null;
        if (explicitCoercion)
          coercer = new Coercer(this.ExplicitCoercion);
        else
          coercer = new Coercer(this.ImplicitCoercion);
        TypeNode streamAdaptor = StreamAdapter.For((Interface)sStreamType, (Interface)tStreamType, this.currentType, coercer, source.SourceContext);
        if (streamAdaptor == null) return null;
        source = new Construct(new MemberBinding(null, TypeViewer.GetTypeView(typeViewer, streamAdaptor).GetConstructor(sStreamType)), new ExpressionList(source), tStreamType);
      }
      return new Construct(new MemberBinding(null, TypeViewer.GetTypeView(typeViewer, targetType).GetConstructor(tStreamType)), new ExpressionList(source), targetType);
    }
    public virtual Expression UnboxAndCoerce(Expression source, TypeNode sourceType, TypeNode targetType, bool explicitCoercion, TypeViewer typeViewer) {
      if (sourceType == null || targetType == null) return null;
      Debug.Assert(sourceType.Template == SystemTypes.GenericBoxed || sourceType.Template == SystemTypes.GenericNonNull || sourceType.Template == SystemTypes.GenericInvariant);
      TypeNode unaliasedTargetType = targetType; 
      { TypeAlias ta = unaliasedTargetType as TypeAlias;
        while (ta != null) {
          unaliasedTargetType = ta.AliasedType; ta = unaliasedTargetType as TypeAlias;
        }
      }
      if (!(targetType is TupleType) && this.GetStreamElementType(unaliasedTargetType, typeViewer) != unaliasedTargetType) {
        Debug.Assert(false,"UnboxAndCoerce");
      }
      TypeNode sElemType = this.GetStreamElementType(sourceType, typeViewer);
      Method meth = null;
      if (sourceType.Template != SystemTypes.GenericBoxed)
        meth = TypeViewer.GetTypeView(typeViewer, sourceType).GetMethod(StandardIds.GetValue);
      else
        meth = TypeViewer.GetTypeView(typeViewer, sourceType).GetMethod(StandardIds.ToObject);
      source = new MethodCall(new MemberBinding(new UnaryExpression(source, NodeType.AddressOf), meth),
        new ExpressionList(0), NodeType.Call, meth.ReturnType);
      if (sourceType.Template == SystemTypes.GenericBoxed)
        source = this.ExplicitCoercion(source, sElemType, typeViewer);
      if (source == null) return null;
      if (explicitCoercion)
        return this.ExplicitCoercion(source, targetType, typeViewer);
      else
        return this.ImplicitCoercion(source, targetType, typeViewer);
    }
    protected virtual Expression CoercionFromIEnumerable(Expression source, TypeNode sourceType, TypeNode targetType, bool explicitCoercion,
        TypeNode originalTargetType, TypeViewer typeViewer){
      if (source == null || sourceType == null || targetType == null) return null;
      Debug.Assert(sourceType.Template == SystemTypes.GenericIEnumerable);
      if (!explicitCoercion) return null;
      TypeNode tElemType = this.GetStreamElementType(targetType, typeViewer);
      if (tElemType != targetType) return null; //Target is an array or flex array type. Handle elsewhere.
      //Get to this point if stream is explicitly being coerced to non stream type. This means that the first element of the stream
      //must be extracted and explicitly coerced to the target type
      TypeNode elemType = this.GetStreamElementType(sourceType, typeViewer);
      if (elemType == null || elemType == sourceType){Debug.Assert(false); return null;}
      //Coerce the stream to a non empty stream, so that the coercion fails it the stream is empty
      TypeNode sNonEmptyIEnumerable = SystemTypes.GenericNonEmptyIEnumerable.GetTemplateInstance(this.currentType, elemType);
      source = this.CoercionToNonEmptyIEnumerable(source, sourceType, sNonEmptyIEnumerable, true, typeViewer);
      if (source == null) return null;
      return this.CoercionFromNonEmptyIEnumerable(source, sNonEmptyIEnumerable, targetType, true, originalTargetType, typeViewer);
    }
    protected virtual Expression CoercionFromNonEmptyIEnumerable(Expression source, TypeNode sourceType, TypeNode targetType, bool explicitCoercion,
      TypeNode originalTargetType, TypeViewer typeViewer){
      if (source == null || sourceType == null || targetType == null) return null;
      Debug.Assert(sourceType.Template == SystemTypes.GenericNonEmptyIEnumerable);
      if (!explicitCoercion) return null;
      TypeNode tElemType = this.GetStreamElementType(targetType, typeViewer);
      if (tElemType != targetType) return null; //Target is an array or flex array type. Handle elsewhere.
      TypeNode elemType = this.GetStreamElementType(sourceType, typeViewer);
      if (elemType == null || elemType == sourceType) return null;
      Method coercion = this.UserDefinedExplicitCoercionMethod(source, sourceType, elemType, false, elemType, typeViewer);
      if (coercion == null){Debug.Assert(false); return null;}
      source = new MethodCall(new MemberBinding(null, coercion), new ExpressionList(source), NodeType.Call, elemType);
      if (explicitCoercion)
        return this.ExplicitCoercion(source, targetType, typeViewer);
      return this.ImplicitCoercion(source, targetType, typeViewer);
    }
    public virtual Expression CoerceStreamToObject(Expression source, TypeNode sourceType, TypeViewer typeViewer){
      if (source == null || sourceType == null) return null;
      Debug.Assert(this.GetStreamElementType(sourceType, typeViewer) != sourceType);
      Method meth;
      if (sourceType.Template == SystemTypes.GenericIEnumerable){
        TypeNode unboxer = SystemTypes.GenericUnboxer.GetTemplateInstance(this.currentType, this.GetStreamElementType(sourceType, typeViewer));
        meth = TypeViewer.GetTypeView(typeViewer, unboxer).GetMethod(StandardIds.ToObject, sourceType);
        Debug.Assert(meth != null);
        return new MethodCall(new MemberBinding(null, meth), new ExpressionList(source), NodeType.Call, SystemTypes.Object);
      }
      if (sourceType.Template == SystemTypes.GenericNonEmptyIEnumerable || sourceType.Template == SystemTypes.GenericNonNull ||
        sourceType.Template == SystemTypes.GenericBoxed || sourceType.Template == SystemTypes.GenericInvariant){
        meth = TypeViewer.GetTypeView(typeViewer, sourceType).GetMethod(StandardIds.ToObject);
        Debug.Assert(meth != null);
        return new MethodCall(new MemberBinding(new UnaryExpression(source, NodeType.AddressOf), meth), 
          new ExpressionList(0), NodeType.Call, SystemTypes.Object);
      }
      if (sourceType.IsValueType)
        return new BinaryExpression(source, new MemberBinding(null, sourceType), NodeType.Box, SystemTypes.Object);        
      return source;
    }
    protected virtual Method UserDefinedExplicitCoercionMethod(Expression source, TypeNode sourceType, TypeNode targetType, bool tryStandardCoercions, TypeNode originalTargetType, TypeViewer typeViewer){
      Reference rtype = sourceType as Reference;
      if (rtype != null) sourceType = rtype.ElementType;
      if (sourceType == targetType) return null;
      //First do efficient searches for a method that coerces directly between source and target type
      //If the source type knows how to convert to the target type, give it preference
      Method coercion = TypeViewer.GetTypeView(typeViewer, sourceType).GetExplicitCoercionToMethod(targetType);
      if (coercion != null) return coercion;
      coercion = TypeViewer.GetTypeView(typeViewer, sourceType).GetImplicitCoercionToMethod(targetType);
      if (coercion != null) return coercion;
      //If the target type knows how to convert from the source type, that is dandy too
      coercion = TypeViewer.GetTypeView(typeViewer, targetType).GetExplicitCoercionFromMethod(sourceType);
      if (coercion != null) return coercion;
      coercion = TypeViewer.GetTypeView(typeViewer, targetType).GetImplicitCoercionFromMethod(sourceType);
      if (coercion != null) return coercion;
      //Perhaps the base type can convert to the target type, or the target type can convert from the base type
      if (sourceType.BaseType != null){
        coercion = this.UserDefinedExplicitCoercionMethod(source, sourceType.BaseType, targetType, false, originalTargetType, typeViewer);
        if (coercion != null) return coercion;
      }
      if (!tryStandardCoercions) return null;
      //Now resort to desperate measures
      //See if the target type has a conversion that can convert the source after a standard coercion has been applied
      MemberList coercions = TypeViewer.GetTypeView(typeViewer, targetType).ExplicitCoercionMethods;
      for (int i = 0, n = coercions == null ? 0 : coercions.Count; i < n; i++){
        coercion = coercions[i] as Method;
        if (coercion == null) continue;
        if (coercion.ReturnType != targetType) continue;
        ParameterList pars = coercion.Parameters;
        if (pars == null || pars.Count != 1) continue;
        Parameter par = pars[0];
        if (par == null) continue;
        if (sourceType == par.Type) return coercion;
        if (this.StandardExplicitCoercion(source, this.IsNonNullType(sourceType), sourceType, this.IsNonNullType(par.Type), par.Type, par.Type, typeViewer) != null) return coercion;
        //REVIEW: choose the best of the bunch?
      }
      coercions = TypeViewer.GetTypeView(typeViewer, targetType).ImplicitCoercionMethods;
      for (int i = 0, n = coercions == null ? 0 : coercions.Count; i < n; i++){
        coercion = coercions[i] as Method;
        if (coercion == null) continue;
        if (coercion.ReturnType != targetType) continue;
        ParameterList pars = coercion.Parameters;
        if (pars == null || pars.Count != 1) continue;
        Parameter par = pars[0];
        if (par == null) continue;
        if (sourceType == par.Type) return coercion;
        if (this.StandardExplicitCoercion(source, this.IsNonNullType(sourceType), sourceType, this.IsNonNullType(par.Type), par.Type, par.Type, typeViewer) != null) return coercion;
        //REVIEW: choose the best of the bunch?
      }
      //See if the source type has a conversion that results in a type that can be converted to the target type via a standard coercion
      TypeNode tgtType = targetType;
      if (targetType is EnumNode) tgtType = ((EnumNode)targetType).UnderlyingType;
      coercions = TypeViewer.GetTypeView(typeViewer, sourceType).ExplicitCoercionMethods;
      TypeNode bestSoFar = null;
      coercion = null;
      for (int i = 0, n = coercions == null ? 0 : coercions.Count; i < n; i++){
        Method m = coercions[i] as Method;
        if (m == null) continue;
        TypeNode rType = m.ReturnType;
        if (rType == sourceType) continue;
        if (this.StandardImplicitCoercionFromTo(null, rType, targetType, typeViewer)) return m;
        if (this.StandardExplicitCoercion(source, this.IsNonNullType(rType), rType, this.IsNonNullType(targetType), targetType, originalTargetType, typeViewer) != null){
          //Possible information loss, try to choose the least bad coercion
          if (bestSoFar == null || this.IsBetterMatch(tgtType, bestSoFar, rType, typeViewer)){
            coercion = m;
            bestSoFar = rType;
          }
        }
      }
      coercions = TypeViewer.GetTypeView(typeViewer, sourceType).ImplicitCoercionMethods;
      for (int i = 0, n = coercions == null ? 0 : coercions.Count; i < n; i++){
        Method m = coercions[i] as Method;
        if (m == null) continue;
        TypeNode rType = m.ReturnType;
        if (rType == sourceType) continue;
        if (this.StandardImplicitCoercionFromTo(null, rType, targetType, typeViewer)) return m;
        if (this.StandardExplicitCoercion(source, this.IsNonNullType(rType), rType, this.IsNonNullType(targetType), targetType, originalTargetType, typeViewer) != null){
          if (bestSoFar == null || this.IsBetterMatch(tgtType, bestSoFar, rType, typeViewer)){
            coercion = m;
            bestSoFar = rType;
          }
        }
      }
      if (coercion != null) return coercion; //TODO: pass this into the recursive call
      //Perhaps the base type can convert to the target type, or the target type can convert from the base type, via standard coercions
      if (sourceType.BaseType != null){
        coercion = this.UserDefinedExplicitCoercionMethod(source, sourceType.BaseType, targetType, true, originalTargetType, typeViewer);
        if (coercion != null) return coercion;
      }
      //Since this is an explicit coercion, try converting to the base type of the target type
      if (targetType.BaseType != null){
        coercion = this.UserDefinedExplicitCoercionMethod(source, sourceType, targetType.BaseType, true, originalTargetType, typeViewer);
        if (coercion != null) return coercion;
      }
      return null;
    }
    public virtual Expression ExplicitPrimitiveCoercion(Expression source, TypeNode sourceType, TypeNode targetType){
      Expression result = this.ExplicitPrimitiveCoercionHelper(source, this.GetUnderlyingType(sourceType), this.GetUnderlyingType(targetType));
      Literal lit = result as Literal;
      if (lit != null) return new Literal(lit.Value, targetType, lit.SourceContext);
      if (result != null) result.Type = targetType;
      if (source is Literal && result is UnaryExpression)
        try{
          lit = new Evaluator().VisitUnaryExpression((UnaryExpression)result) as Literal;
          if (lit != null){
            if (result.Type != SystemTypes.Object)
              lit.Type = result.Type;
            return lit;
          }
        }catch(OverflowException){
          this.HandleError(result, Error.CTOverflow);
          return null;
        }catch{} //TODO: be more specific
      return result;
    }
    private Expression ExplicitPrimitiveCoercionHelper(Expression source, TypeNode sourceType, TypeNode targetType){
      if (this.insideUnsafeCode && targetType is Pointer) targetType = SystemTypes.IntPtr;
      TypeCode ttc = targetType.TypeCode;
      TypeCode stc = sourceType.TypeCode;
      switch (stc) {
        case TypeCode.SByte:
          switch(ttc){
            case TypeCode.SByte:
              return source;
            case TypeCode.Int16:
              return new UnaryExpression(source, NodeType.Conv_I2);
            case TypeCode.Int32:  
              return new UnaryExpression(source, NodeType.Conv_I4);
            case TypeCode.Int64:  
              return new UnaryExpression(source, NodeType.Conv_I8);
            case TypeCode.Byte:   
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U1) : source;
            case TypeCode.Char:   
            case TypeCode.UInt16: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U2) : source;
            case TypeCode.UInt32: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U4) : source;
            case TypeCode.UInt64: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U8) : new UnaryExpression(source, NodeType.Conv_I8);
            case TypeCode.Single: 
              return new UnaryExpression(source, NodeType.Conv_R4);
            case TypeCode.Double: 
              return new UnaryExpression(source, NodeType.Conv_R8);
            case TypeCode.Object:
              if (targetType == SystemTypes.IntPtr) 
                return source; //new UnaryExpression(source, NodeType.Conv_I);
              if (targetType == SystemTypes.UIntPtr) 
                return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U) : source; //new UnaryExpression(source, NodeType.Conv_U);
              break;
          }
          break;
        case TypeCode.Int16:
          switch(ttc){
            case TypeCode.SByte:  
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I1) : new UnaryExpression(source, NodeType.Conv_I1);
            case TypeCode.Int16:
              return source;
            case TypeCode.Int32:  
              return new UnaryExpression(source, NodeType.Conv_I4);
            case TypeCode.Int64:  
              return new UnaryExpression(source, NodeType.Conv_I8);
            case TypeCode.Byte:   
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U1) : new UnaryExpression(source, NodeType.Conv_U1);
            case TypeCode.Char:   
            case TypeCode.UInt16: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U2) : source;
            case TypeCode.UInt32: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U4) : source;
            case TypeCode.UInt64: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U8) : new UnaryExpression(source, NodeType.Conv_I8);
            case TypeCode.Single: 
              return new UnaryExpression(source, NodeType.Conv_R4);
            case TypeCode.Double: 
              return new UnaryExpression(source, NodeType.Conv_R8);
            case TypeCode.Object:
              if (targetType == SystemTypes.IntPtr) 
                return source; //new UnaryExpression(source, NodeType.Conv_I);
              if (targetType == SystemTypes.UIntPtr) 
                return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U) : source; //new UnaryExpression(source, NodeType.Conv_U);
              break;
          }
          break;
        case TypeCode.Int32:
          switch(ttc){
            case TypeCode.SByte:  
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I1) : new UnaryExpression(source, NodeType.Conv_I1);
            case TypeCode.Int16:  
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I2) : new UnaryExpression(source, NodeType.Conv_I2);
            case TypeCode.Int32:
              return source;
            case TypeCode.Int64:  
              return new UnaryExpression(source, NodeType.Conv_I8);
            case TypeCode.Byte:   
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U1) : new UnaryExpression(source, NodeType.Conv_U1);
            case TypeCode.Char:   
            case TypeCode.UInt16: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U2) : new UnaryExpression(source, NodeType.Conv_U2);
            case TypeCode.UInt32: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U4) : source;
            case TypeCode.UInt64: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U8) : new UnaryExpression(source, NodeType.Conv_I8);
            case TypeCode.Single: 
              return new UnaryExpression(source, NodeType.Conv_R4);
            case TypeCode.Double: 
              return new UnaryExpression(source, NodeType.Conv_R8);
            case TypeCode.Object:
              if (targetType == SystemTypes.IntPtr) 
                return source; //new UnaryExpression(source, NodeType.Conv_I);
              if (targetType == SystemTypes.UIntPtr) 
                return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U) : source; //new UnaryExpression(source, NodeType.Conv_U);
              break;
          }
          break;
        case TypeCode.Int64:
          switch(ttc){
            case TypeCode.SByte:  
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I1) : new UnaryExpression(source, NodeType.Conv_I1);
            case TypeCode.Int16:  
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I2) : new UnaryExpression(source, NodeType.Conv_I2);
            case TypeCode.Int32:  
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I4) : new UnaryExpression(source, NodeType.Conv_I4);
            case TypeCode.Int64:
              return source;
            case TypeCode.Byte:   
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U1) : new UnaryExpression(source, NodeType.Conv_U1);
            case TypeCode.Char:   
            case TypeCode.UInt16: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U2) : new UnaryExpression(source, NodeType.Conv_U2);
            case TypeCode.UInt32: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U4) : new UnaryExpression(source, NodeType.Conv_U4);
            case TypeCode.UInt64: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U8) : source;
            case TypeCode.Single: 
              return new UnaryExpression(source, NodeType.Conv_R4);
            case TypeCode.Double: 
              return new UnaryExpression(source, NodeType.Conv_R8);
            case TypeCode.Object:
              if (targetType == SystemTypes.IntPtr) 
                return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I) : new UnaryExpression(source, NodeType.Conv_I);
              if (targetType == SystemTypes.UIntPtr) 
                return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U) : new UnaryExpression(source, NodeType.Conv_U);
              break;
          }
          break;
        case TypeCode.Byte:
          switch(ttc){
            case TypeCode.SByte:
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I1_Un) : new UnaryExpression(source, NodeType.Conv_I1);
            case TypeCode.Int16:
              return new UnaryExpression(source, NodeType.Conv_U2);
            case TypeCode.Int32:  
              return new UnaryExpression(source, NodeType.Conv_U4);
            case TypeCode.Int64:  
              return new UnaryExpression(source, NodeType.Conv_U8);
            case TypeCode.Byte:
              return source;
            case TypeCode.Char:
            case TypeCode.UInt16:
              return new UnaryExpression(source, NodeType.Conv_U2);
            case TypeCode.UInt32: 
              return new UnaryExpression(source, NodeType.Conv_U4);
            case TypeCode.UInt64: 
              return new UnaryExpression(source, NodeType.Conv_U8);
            case TypeCode.Single: 
            case TypeCode.Double: 
              return new UnaryExpression(source, NodeType.Conv_R_Un);
            case TypeCode.Object:
              if (targetType == SystemTypes.IntPtr) 
                return new UnaryExpression(source, NodeType.Conv_I);
              if (targetType == SystemTypes.UIntPtr) 
                return new UnaryExpression(source, NodeType.Conv_U);
              break;
          }
          break;
        case TypeCode.Char:
        case TypeCode.UInt16:
          switch(ttc){
            case TypeCode.SByte: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I1_Un) : new UnaryExpression(source, NodeType.Conv_I1);
            case TypeCode.Int16:  
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I2_Un) : new UnaryExpression(source, NodeType.Conv_I2);
            case TypeCode.Int32:
              return new UnaryExpression(source, NodeType.Conv_U4);
            case TypeCode.Int64:  
              return new UnaryExpression(source, NodeType.Conv_U8);
            case TypeCode.Byte:   
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U1_Un) : new UnaryExpression(source, NodeType.Conv_U1);
            case TypeCode.Char:
            case TypeCode.UInt16:
              return source;
            case TypeCode.UInt32: 
              return new UnaryExpression(source, NodeType.Conv_U4);
            case TypeCode.UInt64: 
              return new UnaryExpression(source, NodeType.Conv_U8);
            case TypeCode.Single: 
            case TypeCode.Double: 
              return new UnaryExpression(source, NodeType.Conv_R_Un);
            case TypeCode.Object:
              if (targetType == SystemTypes.IntPtr) 
                return new UnaryExpression(source, NodeType.Conv_I);
              if (targetType == SystemTypes.UIntPtr) 
                return new UnaryExpression(source, NodeType.Conv_U);
              break;
          }
          break;
        case TypeCode.UInt32:
          switch(ttc){
            case TypeCode.SByte: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I1_Un) : new UnaryExpression(source, NodeType.Conv_I1);
            case TypeCode.Int16:  
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I2_Un) : new UnaryExpression(source, NodeType.Conv_I2);
            case TypeCode.Int32:
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I4_Un) : new UnaryExpression(source, NodeType.Conv_I4);
            case TypeCode.Int64:  
              return new UnaryExpression(source, NodeType.Conv_U8);
            case TypeCode.Byte:   
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U1_Un) : new UnaryExpression(source, NodeType.Conv_U1);
            case TypeCode.Char:
            case TypeCode.UInt16:
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U2_Un) : new UnaryExpression(source, NodeType.Conv_U2);
            case TypeCode.UInt32:
              return source;
            case TypeCode.UInt64: 
              return new UnaryExpression(source, NodeType.Conv_U8);
            case TypeCode.Single: 
              return new UnaryExpression(new UnaryExpression(source, NodeType.Conv_R_Un), NodeType.Conv_R4);
            case TypeCode.Double: 
              return new UnaryExpression(new UnaryExpression(source, NodeType.Conv_R_Un), NodeType.Conv_R8);
            case TypeCode.Object:
              if (targetType == SystemTypes.IntPtr) 
                return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I_Un) : new UnaryExpression(source, NodeType.Conv_I);
              if (targetType == SystemTypes.UIntPtr) 
                return new UnaryExpression(source, NodeType.Conv_U);
              break;
          }
          break;
        case TypeCode.UInt64:
          switch(ttc){
            case TypeCode.SByte: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I1_Un) : new UnaryExpression(source, NodeType.Conv_I1);
            case TypeCode.Int16:  
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I2_Un) : new UnaryExpression(source, NodeType.Conv_I2);
            case TypeCode.Int32:  
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I4_Un) : new UnaryExpression(source, NodeType.Conv_I4);
            case TypeCode.Int64:  
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I8_Un) : new UnaryExpression(source, NodeType.Conv_I8);
            case TypeCode.Byte:   
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U1_Un) : new UnaryExpression(source, NodeType.Conv_U1);
            case TypeCode.Char:
            case TypeCode.UInt16:
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U2_Un) : new UnaryExpression(source, NodeType.Conv_U2);
            case TypeCode.UInt32:
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U4_Un) : new UnaryExpression(source, NodeType.Conv_U4);
            case TypeCode.UInt64: 
              return source;
            case TypeCode.Single: 
              return new UnaryExpression(new UnaryExpression(source, NodeType.Conv_R_Un), NodeType.Conv_R4);
            case TypeCode.Double: 
              return new UnaryExpression(new UnaryExpression(source, NodeType.Conv_R_Un), NodeType.Conv_R8);
            case TypeCode.Object:
              if (targetType == SystemTypes.IntPtr) 
                return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I_Un) : new UnaryExpression(source, NodeType.Conv_I);
              if (targetType == SystemTypes.UIntPtr) 
                return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U_Un) : new UnaryExpression(source, NodeType.Conv_U);
              break;
          }
          break;
        case TypeCode.Single:
        case TypeCode.Double:
          switch(ttc){
            case TypeCode.SByte:  
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I1) : new UnaryExpression(source, NodeType.Conv_I1);
            case TypeCode.Int16:  
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I2) : new UnaryExpression(source, NodeType.Conv_I2);
            case TypeCode.Int32:  
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I4) : new UnaryExpression(source, NodeType.Conv_I4);
            case TypeCode.Int64:  
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I8) : new UnaryExpression(source, NodeType.Conv_I8);
            case TypeCode.Byte:   
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U1) : new UnaryExpression(source, NodeType.Conv_U1);
            case TypeCode.Char:
            case TypeCode.UInt16: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U2) : new UnaryExpression(source, NodeType.Conv_U2);
            case TypeCode.UInt32: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U4) : new UnaryExpression(source, NodeType.Conv_U4);
            case TypeCode.UInt64: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U8) : new UnaryExpression(source, NodeType.Conv_U8);
            case TypeCode.Single: 
              return new UnaryExpression(source, NodeType.Conv_R4);
            case TypeCode.Double: 
              return new UnaryExpression(source, NodeType.Conv_R8);
            case TypeCode.Object:
              if (targetType == SystemTypes.IntPtr) 
                return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I) : new UnaryExpression(source, NodeType.Conv_I);
              if (targetType == SystemTypes.UIntPtr) 
                return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U) : new UnaryExpression(source, NodeType.Conv_U);
              break;
          }
          break;
        case TypeCode.Object:
          if (sourceType == targetType) return source;
          if (!this.insideUnsafeCode || !(sourceType is Pointer)) break;
          switch(ttc){
            case TypeCode.SByte: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I1_Un) : new UnaryExpression(source, NodeType.Conv_I1);
            case TypeCode.Int16:  
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I2_Un) : new UnaryExpression(source, NodeType.Conv_I2);
            case TypeCode.Int32:  
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_I4_Un) : new UnaryExpression(source, NodeType.Conv_I4);
            case TypeCode.Byte:   
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U1_Un) : new UnaryExpression(source, NodeType.Conv_U1);
            case TypeCode.Char:
            case TypeCode.UInt16:
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U2_Un) : new UnaryExpression(source, NodeType.Conv_U2);
            case TypeCode.UInt32:
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U4_Un) : new UnaryExpression(source, NodeType.Conv_U4);
            case TypeCode.Int64:  
            case TypeCode.UInt64: 
              return this.checkOverflow ? new UnaryExpression(source, NodeType.Conv_Ovf_U8_Un) : new UnaryExpression(source, NodeType.Conv_U8);
            case TypeCode.Single: 
              return new UnaryExpression(new UnaryExpression(source, NodeType.Conv_R_Un), NodeType.Conv_R4);
            case TypeCode.Double: 
              return new UnaryExpression(new UnaryExpression(source, NodeType.Conv_R_Un), NodeType.Conv_R8);
            case TypeCode.Object:
              if (targetType == SystemTypes.IntPtr) {
                //if (sourceType.IsPointerType) return new UnaryExpression(source, NodeType.Conv_I);
                return source;
              }
              if (targetType == SystemTypes.UIntPtr) {
                if (sourceType.IsPointerType) return new UnaryExpression(source, NodeType.Conv_U);
                return source;
              }
              break;
          }
          break;
      }
      return null;
    }
    public Expression ImplicitCoercion(Expression source, TypeNode targetType){
      return this.ImplicitCoercion(source, targetType, null);
    }
    public virtual Expression TryImplicitCoercion(Expression source, TypeNode targetType, TypeViewer typeViewer) {
      ErrorHandler oldEH = this.ErrorHandler;
      this.ErrorHandler = null;
      Expression e = null;
      try { e = this.ImplicitCoercion(source, targetType, typeViewer); }
      finally {
        this.ErrorHandler = oldEH;
      };
      return e;
    }
    public virtual Expression ImplicitCoercion(Expression source, TypeNode targetType, TypeViewer typeViewer){
      TypeNode originalTargetType = targetType;
      if (targetType == null || targetType.Name == Looker.NotFound) return source;
      if (source == null) return null;
      //HS D
      if (source is Hole)
          {
              source.Type = targetType;
              return source;
          }
      //HS D
      if (source is LambdaHole)
          {
              if (targetType == SystemTypes.Boolean)
                  source = new LambdaHole(source, new Literal(0), NodeType.Ge, source.SourceContext);                      
              source.Type = targetType;
              return source;
          }
      Literal sourceLit = source as Literal;
      if (sourceLit != null && sourceLit.Value is TypeNode){
        this.HandleError(source, Error.TypeInVariableContext, this.GetTypeName((TypeNode)sourceLit.Value), "class", "variable");
        return null;
      }
      //Ignore parentheses
      if (source.NodeType == NodeType.Parentheses){
        UnaryExpression uex = (UnaryExpression)source;
        uex.Operand = this.ImplicitCoercion(uex.Operand, targetType, typeViewer);
        if (uex.Operand == null) return null;
        uex.Type = uex.Operand.Type;
        return uex;
      }
      bool targetIsNonNullType = this.IsNonNullType(targetType);
      targetType = TypeNode.StripModifier(targetType, SystemTypes.NonNullType);
      targetType = TypeNode.StripModifier(targetType, SystemTypes.NullableType);
      //TODO: handle SkipCheck and EnforceCheck
      //Special case for closure expressions
      if (source.NodeType == NodeType.AnonymousNestedFunction)
        return this.CoerceAnonymousNestedFunction((AnonymousNestedFunction)source, targetType, false, typeViewer);
      TypeNode sourceType = source.Type;
      if (sourceType == null) sourceType = SystemTypes.Object;
      bool sourceIsNonNullType = this.IsNonNullType(source.Type);
      sourceType = TypeNode.StripModifier(sourceType, SystemTypes.NonNullType);
      sourceType = TypeNode.StripModifier(sourceType, SystemTypes.NullableType);
      if (sourceType == SystemTypes.String && !sourceIsNonNullType && source is Literal)
        sourceIsNonNullType = ((Literal)source).Value != null;
      if (this.currentParameter != null && targetType is Reference){
        UnaryExpression uex = source as UnaryExpression;
        if (uex != null){
          if (sourceIsNonNullType && !targetIsNonNullType){
            string ttypeName = this.GetTypeName(targetType);
            string stypeName = this.GetTypeName(source.Type);
            this.HandleError(source, Error.NoImplicitCoercion, stypeName, ttypeName);
            return null;
          }
          if (!sourceIsNonNullType && targetIsNonNullType){
            string ttypeName = this.GetTypeName(targetType);
            string stypeName = this.GetTypeName(source.Type);
            this.HandleError(source, Error.NoImplicitCoercion, stypeName, ttypeName);
            return null;
          }
          if (uex.NodeType == NodeType.OutAddress){
            if ((this.currentParameter.Flags & ParameterFlags.Out) == 0){
              this.currentParameter.Flags |= ParameterFlags.Out;
              string stypeName = this.GetTypeName(sourceType);
              this.currentParameter.Flags &= ~ParameterFlags.Out;
              this.HandleError(source, Error.NoImplicitCoercion, stypeName, this.GetTypeName(targetType));
              return null;
            }
          }else if (uex.NodeType == NodeType.RefAddress){
            if ((this.currentParameter.Flags & ParameterFlags.Out) != 0){
              this.currentParameter.Flags &= ~ParameterFlags.Out;
              string stypeName = this.GetTypeName(sourceType);
              this.currentParameter.Flags |= ParameterFlags.Out;
              this.HandleError(source, Error.NoImplicitCoercion, stypeName, this.GetTypeName(targetType));
              return null;
            }
          }
        }
      }
      Expression result = this.StandardImplicitCoercion(source, sourceIsNonNullType, sourceType, targetIsNonNullType, targetType, originalTargetType, typeViewer);
      if (result != null) return result;
      Method coercion = this.UserDefinedImplicitCoercionMethod(source, sourceType, targetType, true, typeViewer);
      if (coercion != null){
        if (this.IsNullableType(targetType) && this.IsNullableType(sourceType) && !this.IsNullableType(coercion.Parameters[0].Type))
          return this.CoerceWithLiftedCoercion(source, sourceType, targetType, coercion, false, typeViewer);
        ExpressionList args = new ExpressionList(1);
        args.Add(this.ImplicitCoercion(source, coercion.Parameters[0].Type, typeViewer));
        return this.ImplicitCoercion(new MethodCall(new MemberBinding(null, coercion), args, NodeType.Call, coercion.ReturnType, source.SourceContext), targetType, typeViewer);
      }
      if (sourceType == SystemTypes.Type && source is Literal)
        this.HandleError(source, Error.TypeInVariableContext, this.GetTypeName((TypeNode)((Literal)source).Value), "class", "variable");
      else if (this.IsNullableType(sourceType) && this.IsNullableType(targetType) && this.ImplicitCoercionFromTo(this.RemoveNullableWrapper(sourceType), this.RemoveNullableWrapper(targetType))) {
        TypeNode usType = this.RemoveNullableWrapper(sourceType);
        TypeNode utType = this.RemoveNullableWrapper(targetType);

        Local tempSrc = new Local(sourceType);
        Local tempTar = new Local(targetType);
        StatementList statements = new StatementList();
        BlockExpression result1 = new BlockExpression(new Block(statements));
        statements.Add(new AssignmentStatement(tempSrc, source));

        Method hasValue = sourceType.GetMethod(StandardIds.getHasValue);
        Method getValueOrDefault = sourceType.GetMethod(StandardIds.GetValueOrDefault);
        Method ctor = targetType.GetMethod(StandardIds.Ctor, utType);
        Block pushValue = new Block();
        Block done = new Block();

        Expression tempHasValue = new MethodCall(new MemberBinding(new UnaryExpression(tempSrc, NodeType.AddressOf), hasValue), null);
        tempHasValue.Type = SystemTypes.Boolean;
        statements.Add(new Branch(tempHasValue, pushValue));
        statements.Add(new AssignmentStatement(new AddressDereference(new UnaryExpression(tempTar, NodeType.AddressOf), targetType), new Literal(null, CoreSystemTypes.Object)));
        statements.Add(new Branch(null, done));
        statements.Add(pushValue);
        Expression value = new MethodCall(new MemberBinding(new UnaryExpression(tempSrc, NodeType.AddressOf), getValueOrDefault), null);
        value.Type = usType;
        value = this.ImplicitCoercion(value, utType);
        Construct cons = new Construct(new MemberBinding(null, ctor), new ExpressionList(value));
        result1.Type = ctor.DeclaringType;
        statements.Add(new AssignmentStatement(tempTar, cons));
        statements.Add(done);

        statements.Add(new ExpressionStatement(tempTar));
        return result1;
      }else
        this.HandleError(source, Error.NoImplicitCoercion, this.GetTypeName(sourceType), this.GetTypeName(originalTargetType));
      return null;
    }
    public virtual Expression CoerceWithLiftedCoercion(Expression source, TypeNode sourceType, TypeNode targetType, Method coercion, bool explicitCoercion, TypeViewer typeViewer){
      if (source == null || sourceType == null || targetType == null || coercion == null){Debug.Assert(false); return null;}
      Block nullCase = new Block(new StatementList(1));
      Block nonNullCase = new Block(new StatementList(1));
      Block done = new Block();
      Block coercionBlock = new Block(new StatementList(7));
      Local copyOfSource = new Local(source.Type);
      Local result = new Local(targetType);
      //null case
      StatementList statements = nullCase.Statements;
      statements.Add(new AssignmentStatement(result, new Local(StandardIds.NewObj, targetType)));
      //nonNull case
      statements = nonNullCase.Statements;
      Method getValue = TypeViewer.GetTypeView(typeViewer, sourceType).GetMethod(Identifier.For("get_Value"));
      Expression getVal = new MethodCall(
        new MemberBinding(new UnaryExpression(copyOfSource, NodeType.AddressOf, TypeViewer.GetTypeView(typeViewer, sourceType).GetReferenceType()), getValue), null, NodeType.Call, getValue.ReturnType);
      if (explicitCoercion)
        getVal = this.ExplicitCoercion(getVal, coercion.Parameters[0].Type, typeViewer);
      else
        getVal = this.ImplicitCoercion(getVal, coercion.Parameters[0].Type, typeViewer);
      if (getVal == null) return null;
      Expression nonNullVal = new MethodCall(new MemberBinding(null, coercion), new ExpressionList(getVal), NodeType.Call, coercion.ReturnType);
      statements.Add(new AssignmentStatement(result, this.ImplicitCoercion(nonNullVal, targetType, typeViewer)));
      //coercion block
      statements = coercionBlock.Statements;
      statements.Add(new AssignmentStatement(copyOfSource, source));
      Method hasValue = TypeViewer.GetTypeView(typeViewer, sourceType).GetMethod(StandardIds.getHasValue);
      if (hasValue == null){Debug.Assert(false); return null;}
      Expression ifNonNull = new MethodCall(
        new MemberBinding(new UnaryExpression(copyOfSource, NodeType.AddressOf, TypeViewer.GetTypeView(typeViewer, sourceType).GetReferenceType()), hasValue), null, NodeType.Call);
      statements.Add(new Branch(ifNonNull, nonNullCase));
      statements.Add(nullCase);
      statements.Add(new Branch(null, done));
      statements.Add(nonNullCase);
      statements.Add(done);
      statements.Add(new ExpressionStatement(result));
      return new BlockExpression(coercionBlock, targetType);
    }
    /// <summary>
    /// Can be called after Checker.VisitExpression to have expressions of type "reference"
    /// be automatically dereferenced
    /// </summary>
    public virtual Expression AutoDereferenceCoercion(Expression expression) {
      if (expression == null) return null;
      if (expression.NodeType == NodeType.AddressOf || expression.NodeType == NodeType.RefAddress || expression.NodeType == NodeType.OutAddress) {
        return expression;
      }
      UnaryExpression unexpr = expression as UnaryExpression;
      if (unexpr != null && unexpr.NodeType == NodeType.Parentheses) {
        unexpr.Operand = this.AutoDereferenceCoercion(unexpr.Operand);
        return unexpr;
      }
      Reference rt = expression.Type as Reference;
      if (rt != null) {
        expression = new AddressDereference(expression, rt.ElementType, expression.SourceContext);
      }
      return expression;
    }

    protected virtual Expression StandardImplicitCoercion(Expression source, bool sourceIsNonNullType, TypeNode sourceType, bool targetIsNonNullType, TypeNode targetType, TypeNode originalTargetType, TypeViewer typeViewer){
      if (Literal.IsNullLiteral(source)) {
        if (this.IsNullableType(targetType)) {
          Local temp = new Local(targetType);
          StatementList statements = new StatementList();
          BlockExpression result = new BlockExpression(new Block(statements));
          statements.Add(new AssignmentStatement(new AddressDereference(new UnaryExpression(temp, NodeType.AddressOf), targetType), new Literal(null, CoreSystemTypes.Object)));
          statements.Add(new ExpressionStatement(temp));
          return result;
        }
        if (targetType.IsTemplateParameter && !targetType.IsReferenceType) {
          // Check for reference constraint
          this.HandleError(source, Error.TypeVarCantBeNull, targetType.Name.Name);
          return new Local(targetType);
        }
      }
      //Identity coercion
      if (sourceType == targetType && (!targetIsNonNullType || (targetIsNonNullType == sourceIsNonNullType))) return source;
      ITypeParameter stp = sourceType as ITypeParameter;
      ITypeParameter ttp = targetType as ITypeParameter;
      if (stp != null && ttp != null && stp.ParameterListIndex == ttp.ParameterListIndex && stp.DeclaringMember == ttp.DeclaringMember &&
        (!targetIsNonNullType || (targetIsNonNullType == sourceIsNonNullType))) return source;
      if (source is This && targetType != null && sourceType == targetType.Template && targetType.IsNotFullySpecialized)
        //TODO: add check for sourceType.TemplateParameters == targetType.TemplateArguments
        return source;
      //Dereference source
      Reference sr = sourceType as Reference;
      if (sr != null){
        sourceType = sr.ElementType;
        Pointer pType = targetType as Pointer;
        if (pType != null && this.StandardImplicitCoercionFromTo(null, sourceType, pType.ElementType, typeViewer))
          return source;
        else if (pType != null && pType.ElementType == SystemTypes.Void)
          return source;
        bool sourceIsThis = source is This;
        source = new AddressDereference(source, sourceType, source.SourceContext);
        source.Type = sourceType;
        sourceIsNonNullType = this.IsNonNullType(sourceType);
        sourceType = TypeNode.StripModifier(sourceType, SystemTypes.NonNullType);
        //Special case for coercion of this in template class
        if (sourceIsThis && targetType != null && sourceType == targetType.Template && targetType.IsNotFullySpecialized)
          //TODO: add check for sourceType.TemplateParameters == targetType.TemplateArguments
          return source;
      }
      //Identity coercion after dereference
      if (sourceType == targetType && (!targetIsNonNullType || (targetIsNonNullType == sourceIsNonNullType))) return source;
      //Special case for null literal
      if (Literal.IsNullLiteral(source)){
        if (targetIsNonNullType) 
          return ImplicitNonNullCoercion(this.ErrorHandler, source, originalTargetType);
        if (targetType is ITypeParameter && this.useGenerics)
          return new BinaryExpression(source, new Literal(targetType, SystemTypes.Type), NodeType.UnboxAny);
        if (!targetType.IsValueType || targetType.Template == SystemTypes.GenericBoxed)
          return new Literal(null, targetType, source.SourceContext);
        if (this.IsNullableType(targetType))
          return new Local(StandardIds.NewObj, targetType, source.SourceContext);
        TypeAlias tAlias = targetType as TypeAlias;
        if (tAlias != null){
          if (tAlias.RequireExplicitCoercionFromUnderlyingType) return null;
          source = this.ImplicitCoercion(source, tAlias.AliasedType, typeViewer);
          if (source == null) return null;
          Method coercion = this.UserDefinedImplicitCoercionMethod(source, tAlias.AliasedType, targetType, false, typeViewer);
          if (coercion != null){
            ExpressionList args = new ExpressionList(this.ImplicitCoercion(source, coercion.Parameters[0].Type, typeViewer));
            return new MethodCall(new MemberBinding(null, coercion), args, NodeType.Call, coercion.ReturnType);
          }
        }else{
          Method coercion = this.UserDefinedImplicitCoercionMethod(source, source.Type, targetType, true, typeViewer);
          if (coercion != null){
            ExpressionList args = new ExpressionList(this.ImplicitCoercion(source, coercion.Parameters[0].Type, typeViewer));
            return new MethodCall(new MemberBinding(null, coercion), args, NodeType.Call, coercion.ReturnType);
          }
        }
        this.HandleError(source, Error.CannotCoerceNullToValueType, this.GetTypeName(targetType));
        return new Local(targetType);
      }
      //Special case for string literal
      if (source.NodeType == NodeType.Literal && sourceType == SystemTypes.String &&
          (targetType.Template == SystemTypes.GenericNonNull || targetType.Template == SystemTypes.GenericInvariant) && 
          this.GetStreamElementType(targetType, typeViewer) == SystemTypes.String)
          return this.ExplicitCoercion(source, targetType, typeViewer);
      //Implicit numeric coercions + implicit enumeration coercions + implicit constant expression coercions
      if (sourceType.IsPrimitive && sourceType != SystemTypes.String && (targetType.IsPrimitive || targetType == SystemTypes.Decimal || targetType is EnumNode)){
        Expression primitiveCoercion = this.ImplicitPrimitiveCoercion(source, sourceType, targetType, typeViewer);
        if (primitiveCoercion != null) return primitiveCoercion;
      }
      //Implicit coercion from string literal to numbers or eums
      if (this.allowStringLiteralToOtherPrimitiveCoercion && sourceType == SystemTypes.String && (targetType.IsPrimitive || targetType == SystemTypes.Decimal || targetType is EnumNode)){
        Expression primitiveCoercion = this.ImplicitPrimitiveCoercion(source, sourceType, targetType, typeViewer);
        if (primitiveCoercion != null) return primitiveCoercion;
      }
      
      //Implicit reference coercions
      if (TypeViewer.GetTypeView(typeViewer, sourceType).IsAssignableTo(targetType)){
        if (targetIsNonNullType && !(sourceIsNonNullType) && !sourceType.IsValueType) {
          //Handling for non null types
          return ImplicitNonNullCoercion(this.ErrorHandler, source, originalTargetType);
        }else if (sourceType.IsValueType && !targetType.IsValueType){
          if (sourceType.NodeType == NodeType.TypeUnion){
            Debug.Assert(targetType == SystemTypes.Object);
            return this.CoerceTypeUnionToObject(source, typeViewer);
          }
          if (sourceType is TupleType){
            if (targetType == SystemTypes.Object)
              return this.TupleCoercion(source, sourceType, targetType, false, typeViewer);
          }else if (targetType.Template != SystemTypes.GenericIEnumerable && this.GetStreamElementType(sourceType, typeViewer) != sourceType)
            return this.ExplicitCoercion(this.CoerceStreamToObject(source, sourceType, typeViewer), targetType, typeViewer);
          Expression e = new BinaryExpression(source, new MemberBinding(null, sourceType), NodeType.Box, targetType, source.SourceContext);
          e.Type = targetType;
          return e;
        }else if (this.useGenerics && (sourceType is TypeParameter || sourceType is ClassParameter)){
          source = new BinaryExpression(source, new MemberBinding(null, sourceType), NodeType.Box, sourceType);
          if (targetType == SystemTypes.Object) return source;
          return new BinaryExpression(source, new MemberBinding(null, targetType), NodeType.UnboxAny, targetType);
        }
        else if (this.useGenerics && sourceType is ArrayType) {
          ArrayType sat = (ArrayType)sourceType;
          while (sat.ElementType is ArrayType) sat = (ArrayType)sat.ElementType;
          if (sat.ElementType is ITypeParameter)
            return new BinaryExpression(source, new MemberBinding(null, targetType), NodeType.Castclass, targetType, source.SourceContext);
          return source;
        }else
          return source;
      }
      //Special case for delegates
      if (targetType is DelegateNode)
        return this.CoerceToDelegate(source, sourceType, (DelegateNode)targetType, false, typeViewer);
      //Special case for type union to common base type
      if (sourceType.NodeType == NodeType.TypeUnion)
        return this.CoerceFromTypeUnion(source, (TypeUnion)sourceType, targetType, false, originalTargetType, typeViewer);
      //Special case for Type intersection target type
      if (targetType.NodeType == NodeType.TypeIntersection)
        return this.CoerceToTypeIntersection(source, sourceType, (TypeIntersection)targetType, false, typeViewer);
      //Special cases for typed streams
      Expression streamCoercion = this.StreamCoercion(source, sourceType, targetType, false, originalTargetType, typeViewer);
      if (streamCoercion != null) return streamCoercion;
      //Implicit tuple coercions
      return this.TupleCoercion(source, sourceType, targetType, false, typeViewer);
    }
    public virtual Expression TupleCoercion(Expression source, TypeNode sourceType, TypeNode targetType, bool explicitCoercion, TypeViewer typeViewer){
      TupleType sTuple = sourceType as TupleType;
      TupleType tTuple = targetType as TupleType; 
      if (sTuple == null){
        if (!explicitCoercion) return null;
        if (tTuple == null) return null;
        MemberList tMems = tTuple.Members;
        if (tMems == null || tMems.Count != 3) return null;
        ConstructTuple consTuple = new ConstructTuple();
        consTuple.Type = tTuple;
        Field f = (Field)tMems[0].Clone();
        consTuple.Fields = new FieldList(f);
        if (f.Type is TypeAlias)
          f.Initializer = this.ExplicitCoercion(source, f.Type, typeViewer);
        else
          f.Initializer = this.StandardExplicitCoercion(source, this.IsNonNullType(sourceType), sourceType, this.IsNonNullType(f.Type), f.Type, f.Type, typeViewer);
        if (f.Initializer == null) return null;
        return consTuple;
      }
      MemberList sMembers = sTuple.Members;
      if (sMembers == null) return null;
      int n = sMembers.Count;
      if (tTuple == null){
        if (n == 3){
          TypeUnion tUnion = targetType as TypeUnion;
          if (tUnion != null){
            Method coercion = this.UserDefinedImplicitCoercionMethod(source, sourceType, targetType, true, typeViewer);
            if (coercion != null) 
              return new MethodCall(new MemberBinding(null, coercion), new ExpressionList(this.ImplicitCoercion(source, coercion.Parameters[0].Type, typeViewer)), 
                NodeType.Call, coercion.ReturnType, source.SourceContext);
          }
          Field sField = sMembers[0] as Field;
          if (sField == null || (!explicitCoercion && !this.ImplicitCoercionFromTo(sField.Type, targetType, typeViewer))) return null;
          if (!sField.IsAnonymous && targetType == SystemTypes.Object)
            return new BinaryExpression(source, new MemberBinding(null, sTuple), NodeType.Box, SystemTypes.Object);
          ConstructTuple cTuple = source as ConstructTuple;
          if (cTuple != null){
            //TODO: give a warning
            source = cTuple.Fields[0].Initializer;
          }else{
            MemberBinding mb = new MemberBinding(new UnaryExpression(source, NodeType.AddressOf), sField);
            mb.Type = sField.Type;
            source = mb;
          }
          if (explicitCoercion)
            return this.ExplicitCoercion(source, targetType, typeViewer);
          else
            return this.ImplicitCoercion(source, targetType, typeViewer);
        }
        if (targetType == SystemTypes.Object)
          return new BinaryExpression(source, new MemberBinding(null, sTuple), NodeType.Box, SystemTypes.Object);
        return null;
      }
      MemberList tMembers = tTuple.Members;
      if (sMembers == tMembers) return source;
      if (tMembers == null) return null;
      if (n != tMembers.Count) return null;
      n-=2;
      ConstructTuple consTup = source as ConstructTuple;
      if (consTup != null){
        FieldList consFields = consTup.Fields;
        for (int i = 0; i < n; i++){
          Field cField = consFields[i];
          if (cField == null) continue;
          Field tField = tMembers[i] as Field;
          if (tField == null) return null;
          if (explicitCoercion)
            cField.Initializer = this.ExplicitCoercion(cField.Initializer, tField.Type, typeViewer);
          else{
            if (!tField.IsAnonymous && tField.Name != null && 
              (cField.IsAnonymous || cField.Name == null || cField.Name.UniqueIdKey != tField.Name.UniqueIdKey)) return null;
            cField.Initializer = this.ImplicitCoercion(cField.Initializer, tField.Type, typeViewer);
          }
          if (cField.Initializer == null) return null;
          cField.Type = tField.Type;
        }
        consTup.Type = tTuple;
        return consTup;
      }
      Local loc = new Local(sTuple);
      CoerceTuple cTup = new CoerceTuple();
      cTup.OriginalTuple = source;
      cTup.Temp = loc;
      cTup.Type = tTuple;
      FieldList cFields = cTup.Fields = new FieldList(n);
      for (int i = 0; i < n; i++){
        Field sField = sMembers[i] as Field;
        if (sField == null) return null;
        Field tField = tMembers[i] as Field;
        if (tField == null) return null;
        Field cField = new Field();
        cField.Type = tField.Type;
        MemberBinding mb = new MemberBinding(loc, sField);
        if (explicitCoercion)
          cField.Initializer = this.ExplicitCoercion(mb, tField.Type, typeViewer);
        else{
          if (!tField.IsAnonymous && tField.Name != null && 
            (sField.IsAnonymous || sField.Name == null || sField.Name.UniqueIdKey != tField.Name.UniqueIdKey)) return null;
          cField.Initializer = this.ImplicitCoercion(mb, tField.Type, typeViewer);
        }
        if (cField.Initializer == null) return null;
        cFields.Add(cField);
      }
      return cTup;
    }
    public virtual Expression CoerceToTypeIntersection(Expression source, TypeNode sourceType, TypeIntersection targetType, bool explicitCoercion, TypeViewer typeViewer){
      if (source == null || sourceType == null || targetType == null) return null;
      if (!explicitCoercion){
        TypeNodeList types = targetType.Types;
        for (int i = 0, n = types == null ? 0 : types.Count; i < n; i++){
          TypeNode t = types[i]; if (t == null) continue;
          if (!TypeViewer.GetTypeView(typeViewer, sourceType).IsAssignableTo(t)) return null;
        }
      }
      Method fromObject = TypeViewer.GetTypeView(typeViewer, targetType).GetMethod(StandardIds.FromObject, SystemTypes.Object);
      Method getType = Runtime.GetType;
      MethodCall fromObjectCall = new MethodCall(new MemberBinding(null, fromObject), new ExpressionList(source), NodeType.Call);
      fromObjectCall.Type = targetType;
      return fromObjectCall;
    }
    public virtual Expression CoerceObjectToTypeUnion(Expression source, TypeUnion targetType, TypeViewer typeViewer){
      Method fromObject = TypeViewer.GetTypeView(typeViewer, targetType).GetMethod(StandardIds.FromObject, SystemTypes.Object, SystemTypes.Type);
      Method getType = Runtime.GetType;
      ExpressionList arguments = new ExpressionList(2);
      arguments.Add(source);
      arguments.Add(new MethodCall(new MemberBinding(new Expression(NodeType.Dup), getType), null, NodeType.Call));
      MethodCall fromObjectCall = new MethodCall(new MemberBinding(null, fromObject), arguments, NodeType.Call);
      fromObjectCall.Type = targetType;
      return fromObjectCall;
    }
    public virtual Expression CoerceToTypeUnion(Expression source, TypeNode sourceType, TypeUnion targetType, TypeViewer typeViewer){
      if (source == null || sourceType == null || targetType == null) return null;
      if (this.UserDefinedImplicitCoercionMethod(source, sourceType, targetType, true, typeViewer) != null) return null;
      TypeUnion tType = targetType.UnlabeledUnion;
      if (tType == null) return null;
      Method coercion = this.UserDefinedImplicitCoercionMethod(source, sourceType, tType, true, typeViewer);
      if (coercion == null) return null; //No coercion possible
      TypeNode chosenType = coercion.Parameters[0].Type;
      TypeNodeList types1 = tType.Types;
      TypeNodeList types2 = targetType.Types;
      for (int i = 0, n = types1.Count; i < n; i++){
        TypeNode t = types1[i];
        if (t == chosenType){
          source = this.ExplicitCoercion(source, types2[i], typeViewer);
          if (source == null) return null;
          return this.ExplicitCoercion(source, targetType, typeViewer);
        }
      }
      Debug.Assert(false);
      return null;
    }
    protected virtual Expression CoerceFromTypeUnion(Expression source, TypeUnion sourceType, TypeNode targetType, bool explicitCoercion, TypeNode originalTargetType, TypeViewer typeViewer){
      if (source == null || sourceType == null || targetType == null) return null;
      if (targetType == SystemTypes.Object) return this.CoerceTypeUnionToObject(source, typeViewer);
      int cErrors = (this.Errors != null) ? this.Errors.Count : 0;
      if (explicitCoercion){
        Method coercion = this.UserDefinedExplicitCoercionMethod(source, sourceType, targetType, false, originalTargetType, typeViewer);
        if (coercion != null && coercion.ReturnType == targetType && coercion.Parameters != null && coercion.Parameters[0] != null &&
          this.ImplicitCoercionFromTo(sourceType, coercion.Parameters[0].Type, typeViewer))
          return this.ImplicitCoercion(new MethodCall(new MemberBinding(null, coercion), new ExpressionList(source), NodeType.Call, coercion.ReturnType),
            targetType, typeViewer);
      }
      Method getTag = TypeViewer.GetTypeView(typeViewer, sourceType).GetMethod(StandardIds.GetTag);
      if (getTag == null) return null;
      Method getValue = TypeViewer.GetTypeView(typeViewer, sourceType).GetMethod(StandardIds.GetValue);
      if (getValue == null) return null;
      Local src = new Local(sourceType);
      Local srcOb = new Local(SystemTypes.Object, source.SourceContext);
      Local tgt = new Local(targetType);
      Expression callGetTag = new MethodCall(new MemberBinding(new UnaryExpression(src, NodeType.AddressOf), getTag), null);
      Expression callGetValue = new MethodCall(new MemberBinding(new UnaryExpression(src, NodeType.AddressOf), getValue), null);
      TypeNodeList types = sourceType.Types;
      int n = types == null ? 0 : types.Count;
      Block endOfSwitch = new Block();
      StatementList statements = new StatementList(5+n);
      statements.Add(new AssignmentStatement(src, source));
      statements.Add(new AssignmentStatement(srcOb, callGetValue));
      BlockList cases = new BlockList(n);
      statements.Add(new SwitchInstruction(callGetTag, cases));
      bool hadCoercion = false;
      Block eb = new Block(new StatementList(1));
      Construct c = new Construct(new MemberBinding(null, SystemTypes.InvalidCastException.GetConstructor()), null, SystemTypes.InvalidCastException);
      eb.Statements.Add(new Throw(c));
      for (int i = 0; i < n; i++){
        TypeNode t = types[i];
        if (t == null) continue;
        if (!explicitCoercion && !this.ImplicitCoercionFromTo(t, targetType, typeViewer)) return null;
        Expression expr = this.ExplicitCoercion(srcOb, t, typeViewer);
        if (expr == null) return null;
        expr = this.ExplicitCoercion(expr, targetType, typeViewer);
        if (expr == null) {
          cases.Add(eb);
          statements.Add(eb);
        }
        else {
          Block b = new Block(new StatementList(2));
          hadCoercion = true;
          expr.SourceContext = srcOb.SourceContext;
          b.Statements.Add(new AssignmentStatement(tgt, expr));
          b.Statements.Add(new Branch(null, endOfSwitch));
          cases.Add(b);
          statements.Add(b);
        }
      }
      if (this.Errors != null) {
        for (int ie = cErrors, ne = this.Errors.Count; ie < ne; ie++) {
          this.Errors[ie] = null;
        }
      }
      if (!hadCoercion) return null;
      statements.Add(endOfSwitch);
      statements.Add(new ExpressionStatement(tgt));
      return new BlockExpression(new Block(statements));
      //TODO: wrap this in a CoerceTypeUnion node so that source code can be reconstructed easily
    }
    public virtual Expression CoerceTypeIntersectionToObject(Expression source, TypeViewer typeViewer){
      TypeIntersection sourceType = (TypeIntersection)source.Type;
      Method coercion = TypeViewer.GetTypeView(typeViewer, sourceType).GetImplicitCoercionToMethod(SystemTypes.Object);
      ExpressionList args = new ExpressionList(1);
      args.Add(source);
      MethodCall result = new MethodCall(new MemberBinding(null, coercion), args);
      result.Type = SystemTypes.Object;
      return result;
    }
    public virtual Expression CoerceTypeUnionToObject(Expression source, TypeViewer typeViewer){
      TypeUnion sourceType = (TypeUnion)source.Type;
      Method getValue = TypeViewer.GetTypeView(typeViewer, sourceType).GetMethod(StandardIds.GetValue);
      Local temp = new Local(Identifier.Empty, sourceType);
      Expression tempAddr = new UnaryExpression(temp, NodeType.AddressOf);
      StatementList statements = new StatementList(2);
      statements.Add(new AssignmentStatement(temp, source));
      statements.Add(new ExpressionStatement(new MethodCall(new MemberBinding(tempAddr, getValue), null)));
      BlockExpression result = new BlockExpression(new Block(statements));
      result.Type = SystemTypes.Object;
      return result;
    }
    public virtual Expression ImplicitPrimitiveCoercion(Expression source, TypeNode sourceType, TypeNode targetType, TypeViewer typeViewer){
      if (this.insideUnsafeCode && (targetType == SystemTypes.IntPtr || targetType == SystemTypes.UIntPtr))
        return this.ImplicitPrimitiveCoercionHelper(source, sourceType, targetType);
      Literal lit = source as Literal;
      if (lit != null) return this.ImplicitLiteralCoercion(lit, sourceType, targetType, typeViewer);
      Expression result = this.ImplicitPrimitiveCoercionHelper(source, sourceType, targetType);
      if (result != null) result.Type = targetType;
      return result;
    }
    public virtual Literal ImplicitLiteralCoercionForLabel(Literal lit, TypeNode sourceType, TypeNode targetType, TypeViewer typeViewer) {
      try {
        object val = System.Convert.ChangeType(lit.Value, targetType.TypeCode);
        return new Literal(val, targetType);
      } catch (InvalidCastException) {
      } catch (OverflowException) {
      } catch (FormatException) { }
      this.HandleError(lit, Error.NoImplicitCoercionFromConstant, lit.SourceContext.SourceText, this.GetTypeName(targetType));
      return null;
    }
    public virtual Literal ImplicitLiteralCoercion(Literal lit, TypeNode sourceType, TypeNode targetType, TypeViewer typeViewer) {
      try{
        object val = System.Convert.ChangeType(lit.Value, targetType.TypeCode);
        return new Literal(val, targetType);
      }catch(InvalidCastException){
      }catch(OverflowException){
      }catch(FormatException){}
      this.HandleError(lit, Error.NoImplicitCoercionFromConstant, lit.SourceContext.SourceText, this.GetTypeName(targetType));
      return null;
    }
    private Expression ImplicitPrimitiveCoercionHelper(Expression source, TypeNode sourceType, TypeNode targetType){
      TypeCode ttc = targetType.TypeCode;
      TypeCode stc = sourceType.TypeCode;
      switch(stc){
        case TypeCode.SByte:
          switch(ttc){
            case TypeCode.Int16:
              if (source is AddressDereference) 
                return new UnaryExpression(source, NodeType.Conv_I2, targetType, source.SourceContext);
              return source;
            case TypeCode.Int32:
              if (source is AddressDereference) 
                return new UnaryExpression(source, NodeType.Conv_I4, targetType, source.SourceContext);
              return source;
            case TypeCode.Int64:  return new UnaryExpression(source, NodeType.Conv_I8);
            case TypeCode.Single: return new UnaryExpression(source, NodeType.Conv_R4);
            case TypeCode.Double: return new UnaryExpression(source, NodeType.Conv_R8);
            case TypeCode.Object:
              if (this.insideUnsafeCode && targetType == SystemTypes.IntPtr)
                return new UnaryExpression(source, NodeType.Conv_I);
              break;
          }
          break;
        case TypeCode.Int16:
          switch(ttc){
            case TypeCode.Int32:
              if (source is AddressDereference) 
                return new UnaryExpression(source, NodeType.Conv_I4, targetType, source.SourceContext);
              return source;
            case TypeCode.Int64:  return new UnaryExpression(source, NodeType.Conv_I8);
            case TypeCode.Single: return new UnaryExpression(source, NodeType.Conv_R4);
            case TypeCode.Double: return new UnaryExpression(source, NodeType.Conv_R8);
            case TypeCode.Object:
              if (this.insideUnsafeCode && targetType == SystemTypes.IntPtr)
                return new UnaryExpression(source, NodeType.Conv_I);
              break;
          }
          break;
        case TypeCode.Int32:
          switch(ttc){
            case TypeCode.Int64:  return new UnaryExpression(source, NodeType.Conv_I8);
            case TypeCode.Single: return new UnaryExpression(source, NodeType.Conv_R4);
            case TypeCode.Double: return new UnaryExpression(source, NodeType.Conv_R8);
            case TypeCode.Object:
              if (this.insideUnsafeCode && targetType == SystemTypes.IntPtr)
                return new UnaryExpression(source, NodeType.Conv_I);
              break;
          }
          break;
        case TypeCode.Int64:
          switch(ttc){
            case TypeCode.Single: return new UnaryExpression(source, NodeType.Conv_R4);
            case TypeCode.Double: return new UnaryExpression(source, NodeType.Conv_R8);
            case TypeCode.Object:
              if (this.insideUnsafeCode && targetType == SystemTypes.IntPtr)
                return new UnaryExpression(source, NodeType.Conv_I);
              break;
          }
          break;
        case TypeCode.Byte:
          switch(ttc){
            case TypeCode.Int16:
              if (source is AddressDereference) 
                return new UnaryExpression(source, NodeType.Conv_I2, targetType, source.SourceContext);
              return source;
            case TypeCode.UInt16:
              if (source is AddressDereference) 
                return new UnaryExpression(source, NodeType.Conv_U2, targetType, source.SourceContext);
              return source;
            case TypeCode.Int32:
              if (source is AddressDereference) 
                return new UnaryExpression(source, NodeType.Conv_I4, targetType, source.SourceContext);
              return source;
            case TypeCode.UInt32:
              if (source is AddressDereference) 
                return new UnaryExpression(source, NodeType.Conv_U4, targetType, source.SourceContext);
              return source;
            case TypeCode.Int64: 
            case TypeCode.UInt64: return new UnaryExpression(source, NodeType.Conv_U8);
            case TypeCode.Single: 
            case TypeCode.Double: return new UnaryExpression(source, NodeType.Conv_R_Un);
            case TypeCode.Object:
              if (this.insideUnsafeCode){
                if (targetType == SystemTypes.IntPtr)
                  return new UnaryExpression(source, NodeType.Conv_I);
                else if (targetType == SystemTypes.UIntPtr)
                  return new UnaryExpression(source, NodeType.Conv_U);
              }
              break;
          }
          break;
        case TypeCode.UInt16:
          switch(ttc){
            case TypeCode.Int32:
              if (source is AddressDereference) 
                return new UnaryExpression(source, NodeType.Conv_I4, targetType, source.SourceContext);
              return source;
            case TypeCode.UInt32:
              if (source is AddressDereference) 
                return new UnaryExpression(source, NodeType.Conv_U4, targetType, source.SourceContext);
              return source;
            case TypeCode.Int64: 
            case TypeCode.UInt64: return new UnaryExpression(source, NodeType.Conv_U8);
            case TypeCode.Single: 
            case TypeCode.Double: return new UnaryExpression(source, NodeType.Conv_R_Un);
            case TypeCode.Object:
              if (this.insideUnsafeCode){
                if (targetType == SystemTypes.IntPtr)
                  return new UnaryExpression(source, NodeType.Conv_I);
                else if (targetType == SystemTypes.UIntPtr)
                  return new UnaryExpression(source, NodeType.Conv_U);
              }
              break;
          }
          break;
        case TypeCode.UInt32:
          switch(ttc){
            case TypeCode.Int64: 
            case TypeCode.UInt64: return new UnaryExpression(source, NodeType.Conv_U8);
            case TypeCode.Single: 
            case TypeCode.Double: return new UnaryExpression(source, NodeType.Conv_R_Un);
            case TypeCode.Object:
              if (this.insideUnsafeCode){
                if (targetType == SystemTypes.IntPtr)
                  return new UnaryExpression(source, NodeType.Conv_I);
                else if (targetType == SystemTypes.UIntPtr)
                  return new UnaryExpression(source, NodeType.Conv_U);
              }
              break;
          }
          break;
        case TypeCode.UInt64:
          switch(ttc){
            case TypeCode.Single: 
            case TypeCode.Double: return new UnaryExpression(source, NodeType.Conv_R_Un);
            case TypeCode.Object:
              if (this.insideUnsafeCode){
                if (targetType == SystemTypes.IntPtr)
                  return new UnaryExpression(source, NodeType.Conv_I);
                else if (targetType == SystemTypes.UIntPtr)
                  return new UnaryExpression(source, NodeType.Conv_U);
              }
              break;
          }
          break;
        case TypeCode.Char:
          switch(ttc){
            case TypeCode.UInt16:
              return source;
            case TypeCode.Int32:
              if (source is AddressDereference) 
                return new UnaryExpression(source, NodeType.Conv_I4, targetType, source.SourceContext);
              return source;
            case TypeCode.UInt32: 
              if (source is AddressDereference) 
                return new UnaryExpression(source, NodeType.Conv_U4, targetType, source.SourceContext);
              return source;
            case TypeCode.Int64:  
            case TypeCode.UInt64: return new UnaryExpression(source, NodeType.Conv_U8);
            case TypeCode.Single: 
            case TypeCode.Double: return new UnaryExpression(source, NodeType.Conv_R_Un);
          }
          break;
        case TypeCode.Single:
          if (ttc == TypeCode.Double) return new UnaryExpression(source, NodeType.Conv_R8);           
          break;
      }
      return null;
    }
    public virtual string GetMemberSignature(Member mem){
      if (this.ErrorHandler == null) return "";
      return this.ErrorHandler.GetMemberSignature(mem);
    }
    public virtual string GetTypeName(TypeNode type){
      if (this.ErrorHandler == null) return "";
      return this.ErrorHandler.GetTypeName(type);
    }
    public virtual void HandleError(Node offendingNode, Error error, params string[] messageParameters){
      if (this.ErrorHandler == null) return;
      this.ErrorHandler.HandleError(offendingNode, error, messageParameters);
    }

    public static bool MostSignificantBitIsOneAndItExtends(Literal lit1) {
      if (lit1 == null) return true;
      IConvertible ic = lit1.Value as IConvertible;
      if (ic == null) return false;
      switch (ic.GetTypeCode()) {
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32: return ic.ToInt32(null) < 0;
        case TypeCode.UInt32: return ic.ToUInt32(null) > int.MaxValue;
      }
      return false;
    }
  }
}
