//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
//
#if CCINamespace
using Microsoft.Cci;
using Cci = Microsoft.Cci;
#else
using System.Compiler;
using Cci = System.Compiler;
#endif
using System;

namespace Microsoft.SpecSharp{
  public class TypeSystem : Cci.TypeSystem{
    public TypeSystem(ErrorHandler errorHandler)
      :base(errorHandler){
    }

    public override Expression CoerceAnonymousNestedFunction(AnonymousNestedFunction func, TypeNode targetType, bool explicitCoercion, TypeViewer typeViewer) {
      if (func is AnonymousNestedDelegate && !(targetType is DelegateNode)) {
        this.HandleError(func, Error.AnonMethToNonDel, this.GetTypeName(targetType));
        return null;
      }
      return base.CoerceAnonymousNestedFunction(func, targetType, explicitCoercion, typeViewer);
    }

    public override Expression ExplicitLiteralCoercion(Literal lit, TypeNode sourceType, TypeNode targetType, TypeViewer typeViewer) {
      if (sourceType == targetType && (sourceType == SystemTypes.Double || sourceType == SystemTypes.Single)) return lit;
      TypeNode originalTargetType = targetType;
      EnumNode sourceEnum = sourceType as EnumNode;
      if (sourceEnum != null) sourceType = sourceEnum.UnderlyingType;
      bool needsRuntimeCoercion = this.suppressOverflowCheck;
      if (!sourceType.IsPrimitiveInteger || sourceType == SystemTypes.IntPtr || sourceType == SystemTypes.UIntPtr)
        needsRuntimeCoercion = true;
      else if (!targetType.IsPrimitiveInteger || targetType == SystemTypes.IntPtr || targetType == SystemTypes.UIntPtr)
        needsRuntimeCoercion = true;
      if (needsRuntimeCoercion){
        if (lit != null && lit.Value != null)
          targetType = TypeNode.StripModifier(targetType, SystemTypes.NonNullType);
        return this.ExplicitCoercion(lit, targetType, typeViewer);
      }else
        return this.LiteralCoercion(lit, sourceType, targetType, true, originalTargetType, null, false);
    }
    public override Literal ImplicitLiteralCoercionForLabel(Literal lit, TypeNode sourceType, TypeNode targetType, TypeViewer typeViewer) {
      return this.LiteralCoercion(lit, sourceType, targetType, false, targetType, typeViewer, true);
    }
    public override Literal ImplicitLiteralCoercion(Literal lit, TypeNode sourceType, TypeNode targetType, TypeViewer typeViewer) {
      return this.LiteralCoercion(lit, sourceType, targetType, false, targetType, typeViewer, false);
    }
    private Literal LiteralCoercion(Literal/*!*/ lit, TypeNode sourceType, TypeNode targetType, bool explicitCoercion, TypeNode originalTargetType, TypeViewer typeViewer, bool forLabel) {
      if (sourceType == targetType){
        if (sourceType == lit.Type) return lit;
        return new Literal(Convert.ChangeType(lit.Value, sourceType.TypeCode), sourceType, lit.SourceContext);
      }
      object val = lit.Value;
      EnumNode eN = targetType as EnumNode;
      if (eN != null){
        if (sourceType.IsPrimitiveInteger && val is IConvertible && ((IConvertible)val).ToDouble(null) == 0.0){
          if (eN.UnderlyingType == SystemTypes.Int64 || eN.UnderlyingType == SystemTypes.UInt64) val = 0L; else val = 0;
          return new Literal(val, eN, lit.SourceContext);
        }
        goto error;
      }
      if (targetType.TypeCode == TypeCode.Boolean){
        this.HandleError(lit, Error.ConstOutOfRange, lit.SourceContext.SourceText, "bool");
        lit.SourceContext.Document = null;
        return null;
      }
      if (targetType.TypeCode == TypeCode.String){
        if (val != null || lit.Type != SystemTypes.Object){
          this.HandleError(lit, Error.NoImplicitConversion, this.GetTypeName(sourceType), this.GetTypeName(targetType));
          lit.SourceContext.Document = null;
          return null;
        }
        return lit;
      }
      if (targetType.TypeCode == TypeCode.Object){
        if (val == null && sourceType == SystemTypes.Object && (explicitCoercion || !this.IsNonNullType(targetType))) return lit;
        if (val is string && this.IsNonNullType(targetType) && TypeNode.StripModifiers(targetType) == SystemTypes.String) return lit;
        Method coercion = null;
        if (explicitCoercion)
          coercion = this.UserDefinedExplicitCoercionMethod(lit, sourceType, targetType, true, originalTargetType, typeViewer);
        else
          coercion = this.UserDefinedImplicitCoercionMethod(lit, sourceType, targetType, true, typeViewer);
        if (coercion != null) return null;
        this.HandleError(lit, Error.NoImplicitConversion, this.GetTypeName(sourceType), this.GetTypeName(targetType));
        lit.SourceContext.Document = null;
        return null;
      }
      if ((targetType.TypeCode == TypeCode.Char || sourceType.TypeCode == TypeCode.Boolean || sourceType.TypeCode == TypeCode.Decimal) && !forLabel) goto error;
      switch (sourceType.TypeCode){
        case TypeCode.Double:
          switch (targetType.TypeCode){
            case TypeCode.Single: this.HandleError(lit, Error.LiteralDoubleCast, "float", "F"); return lit;
            case TypeCode.Decimal: this.HandleError(lit, Error.LiteralDoubleCast, "decimal", "M"); return lit;
            default: 
              this.HandleError(lit, Error.NoImplicitConversion, this.GetTypeName(sourceType), this.GetTypeName(targetType));
              lit.SourceContext.Document = null;
              return null;
          }
        case TypeCode.Single:
          switch (targetType.TypeCode){
            case TypeCode.Double: break;
            default: 
              this.HandleError(lit, Error.NoImplicitConversion, this.GetTypeName(sourceType), this.GetTypeName(targetType));
              lit.SourceContext.Document = null;
              return null;
          }
          break;
        case TypeCode.Int64:
        case TypeCode.UInt64:
          switch (targetType.TypeCode){
            case TypeCode.Int64: 
            case TypeCode.UInt64:
            case TypeCode.Decimal:
            case TypeCode.Single:
            case TypeCode.Double:
              break;
            default: 
              if (explicitCoercion || !lit.TypeWasExplicitlySpecifiedInSource) break;
              this.HandleError(lit, Error.NoImplicitConversion, this.GetTypeName(sourceType), this.GetTypeName(targetType));
              lit.SourceContext.Document = null;
              return null;
          }
          break;
      }
      try{
        if (val == null){
          if (targetType.IsValueType) goto error;
        }else
          val = System.Convert.ChangeType(val, targetType.TypeCode);
        return new Literal(val, targetType, lit.SourceContext);
      }catch(InvalidCastException){
      }catch(OverflowException){
      }catch(FormatException){}
    error:
      if (sourceType.IsPrimitiveNumeric && lit.SourceContext.Document != null){
        Error e = Error.ConstOutOfRange;
        if (explicitCoercion) e = Error.ConstOutOfRangeChecked;
        this.HandleError(lit, e, lit.SourceContext.SourceText, this.GetTypeName(targetType));
      }else
        this.HandleError(lit, Error.NoImplicitConversion, this.GetTypeName(sourceType), this.GetTypeName(targetType));
      if (this.ErrorHandler != null) lit.SourceContext.Document = null;
      return null;
    }
    public override bool ImplicitLiteralCoercionFromTo(Literal lit, TypeNode sourceType, TypeNode targetType){
      if (lit == null) return false;
      if (sourceType == targetType) return true;
      object val = lit.Value;
      if (targetType is EnumNode){
        if (val is int && ((int)val) == 0)
          return true;
        return false;
      }
      if (targetType.TypeCode == TypeCode.Boolean) return false;
      if (val == null && lit.Type == SystemTypes.Object){
        //null literal
        if (targetType != null && !this.IsCompatibleWithNull(targetType))
          return false;
        return true;
      }
      if (targetType.TypeCode == TypeCode.String) return false;
      if (targetType.TypeCode == TypeCode.Object) return false;
      if (targetType.TypeCode == TypeCode.Char || sourceType.TypeCode == TypeCode.Boolean || sourceType.TypeCode == TypeCode.Decimal) return false;
      switch (sourceType.TypeCode){
        case TypeCode.Char:
        case TypeCode.Double:
          return false;
        case TypeCode.Single:
          switch (targetType.TypeCode){
            case TypeCode.Double: return true;
            default: return false;
          }
        case TypeCode.Int64:
          switch (targetType.TypeCode) {
            case TypeCode.Int64:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Decimal:
            case TypeCode.Single:
            case TypeCode.Double:
              break;
            default:
              return false;
          }
          break;
        case TypeCode.UInt64:
          switch (targetType.TypeCode){
            case TypeCode.Int64: 
            case TypeCode.UInt64:
            case TypeCode.Decimal:
            case TypeCode.Single:
            case TypeCode.Double:
              break;
            default: 
              return false;
          }
          break;
      }
      try{
        if (val == null){
          if (targetType.IsValueType) return false;
        }else
          val = System.Convert.ChangeType(val, targetType.TypeCode);
        return true;
      }catch(InvalidCastException){
      }catch(OverflowException){
      }catch(FormatException){}
      return false;
    }
    public override string GetTypeName(TypeNode type){
      if (this.ErrorHandler == null){return "";}
      ((ErrorHandler)this.ErrorHandler).currentParameter = this.currentParameter;
      return this.ErrorHandler.GetTypeName(type);
    }
    private void HandleError(Node offendingNode, Error error, params string[] messageParameters){
      if (this.ErrorHandler == null) return;
      ((ErrorHandler)this.ErrorHandler).HandleError(offendingNode, error, messageParameters);
    }
  }
}
