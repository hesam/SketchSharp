//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections;
using System.Diagnostics;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  /// <summary>
  /// Executes an IR, typically an expression tree, resulting in a literal node representing the computed value of the IR.
  /// This class is intended for use by a debugger expression evaluator, as well as partial evaluation (constant folding) during compilation.
  /// </summary>
  public class Evaluator : StandardVisitor{
    public StackFrame stackFrame;
    public Environment Environment;
    public Evaluator()
      : this((Environment)null){
    }
    public Evaluator(Environment environment){
      this.Environment = environment;
    }
    public Evaluator(Visitor callingVisitor)
      :base(callingVisitor){
    }
    public override void TransferStateTo(Visitor targetVisitor){
      base.TransferStateTo(targetVisitor);
      Evaluator target = targetVisitor as Evaluator;
      if (target == null) return;
      target.Environment = this.Environment;
    }
    public override Expression VisitBinaryExpression(BinaryExpression binaryExpression){
      if (binaryExpression == null) return null;
      Literal opnd1 = this.Visit(binaryExpression.Operand1) as Literal;
      Literal opnd2 = this.Visit(binaryExpression.Operand2) as Literal;
      return PureEvaluator.EvalBinaryExpression(opnd1, opnd2, binaryExpression, null);
    }
    public class CircularConstantException : ApplicationException{}
    public static readonly Expression BeingEvaluated = new Expression(NodeType.Nop);
    public override Expression VisitMemberBinding(MemberBinding memberBinding){
      if (memberBinding == null) return null;
      Field f = memberBinding.BoundMember as Field;
      if (f != null){
        if (f.IsLiteral){
          if (f.DefaultValue != null) return f.DefaultValue;
          Expression e = f.Initializer;
          if (e == Evaluator.BeingEvaluated) throw new CircularConstantException();
          f.Initializer = Evaluator.BeingEvaluated;
          Literal lit = null;
          try{
            lit = this.VisitExpression(e) as Literal;
          }catch(CircularConstantException){
            f.Initializer = null;
            throw;
          }
          if (lit != null){
            if (f.DeclaringType is EnumNode && f.Type == f.DeclaringType) lit.Type = f.DeclaringType;
            f.DefaultValue = lit;
            f.Initializer = null;
            return lit;
          }
        }else{
          //Should never get here from the compiler itself, but only from runtime code paths.
          if (f.IsStatic) return f.GetValue(null);
          Expression targetLiteral = this.VisitExpression(memberBinding.TargetObject) as Expression;
          if (targetLiteral != null){
            // HACK: Unary Expressions with NodeType "AddressOf" not getting converted to Literal.
            UnaryExpression uexpr = targetLiteral as UnaryExpression;
            if (uexpr != null)
              targetLiteral = this.VisitExpression(uexpr.Operand);
            if (targetLiteral is Literal)
              return f.GetValue(targetLiteral as Literal);
          }
        }
      }
      return null;
    }
    public override Expression VisitThis(This This){
      if (This == null) return null;
      if (this.stackFrame != null)
        return this.stackFrame.thisObject;
      return null;
    }
    public override Expression VisitLocal(Local local){
      if (local == null || this.stackFrame == null || this.stackFrame.locals == null) return null;
      return this.stackFrame.locals[local.Index];
    }
    public override Expression VisitParameter(Parameter parameter){
      if (parameter == null || this.stackFrame == null || this.stackFrame.parameters == null) return null;
      return this.stackFrame.parameters[parameter.ArgumentListIndex];
    }
    public override Expression VisitUnaryExpression(UnaryExpression unaryExpression){
      if (unaryExpression == null) return null;
      Literal lit = this.Visit(unaryExpression.Operand) as Literal;
      
      Literal result = PureEvaluator.EvalUnaryExpression(lit, unaryExpression);
      if (result != null) return result;
      return unaryExpression;
    }
  }
  /// <summary>
  /// 
  /// </summary>
  public class Environment{
    public Environment Container;

    protected TrivialHashtable table = new TrivialHashtable();
    public virtual Literal this[Node node]{
      get{
        object result = this.table[node.UniqueKey];
        if (result == null && this.Container != null)
          return this.Container[node];
        return (Literal)result;
      }
      set{
        this.table[node.UniqueKey] = value;
      }
    }
  }

  /// <summary>
  /// Has no side effects on any Cci structures, just evaluates a single node if possible.
  /// </summary>
  public class PureEvaluator {

    /// <summary>
    /// Tries to return the literal obtained from constant folding the binary expression whose literal arguments are given
    /// by opnd1 and opnd2. If any of these are null, the result is null. If the binary expression cannot be constant folded
    /// the result is also null.
    /// </summary>
    /// <param name="opnd1">null or literal corresponding to binary expression's 1st constant folded argument</param>
    /// <param name="opnd2">null or literal corresponding to binary expression's 2nd constant folded argument</param>
    /// <param name="binaryExpression">the original binary expression</param>
    /// <returns>null, or constant folded literal</returns>
    public static Literal EvalBinaryExpression(Literal opnd1, Literal opnd2, BinaryExpression binaryExpression, TypeSystem typeSystem){
      if (binaryExpression == null) return null;
      if (opnd1 == null) return null;
      if (opnd2 == null) return null;
      IConvertible ic1 = opnd1.Value as IConvertible;
      IConvertible ic2 = opnd2.Value as IConvertible;
      TypeCode code1 = ic1 == null ? TypeCode.Object : ic1.GetTypeCode();
      TypeCode code2 = ic2 == null ? TypeCode.Object : ic2.GetTypeCode();
      TypeNode type = SystemTypes.Object;
      object val = null;
      switch (binaryExpression.NodeType){
        case NodeType.Add :
          if (typeSystem != null && typeSystem.checkOverflow) goto case NodeType.Add_Ovf;
          return PureEvaluator.DoAdd(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.Add_Ovf : 
        case NodeType.Add_Ovf_Un : 
          return PureEvaluator.DoAddOvf(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.AddEventHandler :
          return null;
        case NodeType.And : 
          return PureEvaluator.DoAnd(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.As :
          return null;
        case NodeType.Box :
        case NodeType.Castclass : 
        case NodeType.ExplicitCoercion :
          type = (TypeNode)opnd2.Value;
          TypeNode t = type;
          EnumNode en = type as EnumNode;
          if (en != null) t = en.UnderlyingType;
          if (t == null || !t.IsPrimitive) return null;
          if (typeSystem != null && binaryExpression.NodeType == NodeType.ExplicitCoercion){
            ErrorHandler savedErrorHandler = typeSystem.ErrorHandler;
            typeSystem.ErrorHandler = null;
            Expression result;
            try {
              result = typeSystem.ExplicitLiteralCoercion(opnd1, opnd1.Type, t, null);
            }
            finally {
              typeSystem.ErrorHandler = savedErrorHandler;
            };
            return result as Literal;
          }
          Type rt = t.GetRuntimeType();
          if (rt != null)
            val = Convert.ChangeType(opnd1.Value, rt, null);
          else
            val = opnd1.Value;
          break;
        case NodeType.Ceq : 
        case NodeType.Cgt : 
        case NodeType.Cgt_Un : 
        case NodeType.Clt : 
        case NodeType.Clt_Un : 
          return null;
        case NodeType.Comma:
          return opnd2;
        case NodeType.Div : 
        case NodeType.Div_Un : 
          return PureEvaluator.DoDiv(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.Eq : 
          return PureEvaluator.DoEq(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.Ge : 
          return PureEvaluator.DoGe(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.Gt : 
          return PureEvaluator.DoGt(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.Is : 
        case NodeType.Isinst : 
        case NodeType.Ldvirtftn :
          return null;
        case NodeType.Le : 
          return PureEvaluator.DoLe(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.LogicalAnd :
          return PureEvaluator.DoLogicalAnd(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.LogicalOr :
          return PureEvaluator.DoLogicalOr(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.Lt : 
          return PureEvaluator.DoLt(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.Mkrefany :
          return null;
        case NodeType.Mul :
          if (typeSystem != null && typeSystem.checkOverflow) goto case NodeType.Mul_Ovf;
          return PureEvaluator.DoMul(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.Mul_Ovf : 
        case NodeType.Mul_Ovf_Un : 
          return PureEvaluator.DoMulOvf(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.Ne : 
          return PureEvaluator.DoNe(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.Or : 
          return PureEvaluator.DoOr(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.Refanyval :
          return null;
        case NodeType.Rem : 
        case NodeType.Rem_Un : 
          return PureEvaluator.DoRem(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.RemoveEventHandler :
          return null;
        case NodeType.Shl : 
          return PureEvaluator.DoLeftShift(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.Shr : 
        case NodeType.Shr_Un : 
          return PureEvaluator.DoRightShift(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.Sub :
          if (typeSystem != null && typeSystem.checkOverflow) goto case NodeType.Sub_Ovf;
          return PureEvaluator.DoSub(ic1, ic2, code1, code2, binaryExpression);        
        case NodeType.Sub_Ovf : 
        case NodeType.Sub_Ovf_Un : 
          return PureEvaluator.DoSubOvf(ic1, ic2, code1, code2, binaryExpression);
        case NodeType.Unbox : 
        case NodeType.Xor :
        default:
          return null;
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }


    #region Binary evaluators
    public static Literal DoLogicalAnd(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Object;
      object val = null;
      if (code1 == TypeCode.Boolean && code2 == TypeCode.Boolean){
        type = SystemTypes.Boolean;
        val = ic1.ToBoolean(null) && ic2.ToBoolean(null);
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoLogicalOr(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Object;
      object val = null;
      if (code1 == TypeCode.Boolean && code2 == TypeCode.Boolean){
        type = SystemTypes.Boolean;
        val = ic1.ToBoolean(null) || ic2.ToBoolean(null);
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoAdd(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Object;
      object val = null;
      switch(code1){
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
          int i = ic1.ToInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = i + ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = i + ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Single: 
              val = i + ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = i + ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = i + ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Byte:
        case TypeCode.UInt16:
          ushort us = ic1.ToUInt16(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
              val = us + ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = us + ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.Int64: 
              val = us + ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.UInt64: 
              val = us + ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = us + ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = us + ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = us + ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.UInt32:
          uint ui = ic1.ToUInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = ui + ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ui + ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.UInt64: 
              val = ui + ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = ui + ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = ui + ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = ui + ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Int64:
          long l = ic1.ToInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = l + ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = l + ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.UInt64: 
              val = l + (long)ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = l + ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = l + ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = l + ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.UInt64:
          ulong ul = ic1.ToUInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ul + ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = ul + ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = ul + ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = ul + ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = ul + ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Single:
          float f = ic1.ToSingle(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
              val = f + ic2.ToInt16(null);
              type = SystemTypes.Single;
              break;
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = f + (double)ic2.ToInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = f + ic2.ToUInt16(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = f + (double)ic2.ToUInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
              val = f + ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = f + (double)ic2.ToDecimal(null);
              type = SystemTypes.Double;
              break;
            default: return null;
          }
          break;
        case TypeCode.Double:
          double d = ic1.ToDouble(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = d + ic2.ToInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = d + ic2.ToUInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
              val = d + ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = d + (double)ic2.ToDecimal(null);
              type = SystemTypes.Double;
              break;
            default: return null;
          }
          break;
        case TypeCode.Decimal:
          decimal dec = ic1.ToDecimal(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32: 
              val = dec + ic2.ToInt32(null); 
              type = SystemTypes.Decimal;
              break;
            case TypeCode.UInt32: 
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = dec + ic2.ToInt64(null); 
              type = SystemTypes.Decimal;
              break;
            case TypeCode.Decimal:
              val = dec + ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.String:
          string str = ic1.ToString(null);
          switch (code2) {
            case TypeCode.String:
              val = str + ic2.ToString(null);
              type = SystemTypes.String;
              break;
            default: return null;
          }
          break;
        default: return null;
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoAddOvf(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Object;
      object val = null;
      checked{switch(code1){
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
          int i = ic1.ToInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = i + ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = i + ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Single: 
              val = i + ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = i + ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = i + ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Byte:
        case TypeCode.UInt16:
          ushort us = ic1.ToUInt16(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
              val = us + ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = us + ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.Int64: 
              val = us + ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.UInt64: 
              val = us + ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = us + ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = us + ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = us + ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.UInt32:
          uint ui = ic1.ToUInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = ui + ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ui + ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.UInt64: 
              val = ui + ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = ui + ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = ui + ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = ui + ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Int64:
          long l = ic1.ToInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = l + ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = l + ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.UInt64: 
              if (l >= 0){
                val = ((ulong)l) + ic2.ToUInt64(null);
                type = SystemTypes.UInt64;
              }else{
                val = l + (long)ic2.ToUInt64(null); 
                type = SystemTypes.Int64;
              }
              break;
            case TypeCode.Single: 
              val = l + ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = l + ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = l + ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.UInt64:
          ulong ul = ic1.ToUInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ul + ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = ul + ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = ul + ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = ul + ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = ul + ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Single:
          float f = ic1.ToSingle(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
              val = f + ic2.ToInt16(null);
              type = SystemTypes.Single;
              break;
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = f + (double)ic2.ToInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = f + ic2.ToUInt16(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = f + (double)ic2.ToUInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
              val = f + ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = f + (double)ic2.ToDecimal(null);
              type = SystemTypes.Double;
              break;
            default: return null;
          }
          break;
        case TypeCode.Double:
          double d = ic1.ToDouble(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = d + ic2.ToInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = d + ic2.ToUInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
              val = d + ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = d + (double)ic2.ToDecimal(null);
              type = SystemTypes.Double;
              break;
            default: return null;
          }
          break;
        case TypeCode.Decimal:
          decimal dec = ic1.ToDecimal(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32: 
              val = dec + ic2.ToInt32(null); 
              type = SystemTypes.Decimal;
              break;
            case TypeCode.UInt32: 
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = dec + ic2.ToInt64(null); 
              type = SystemTypes.Decimal;
              break;
            case TypeCode.Decimal:
              val = dec + ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.String:
          string str = ic1.ToString(null);
          switch (code2) {
            case TypeCode.String:
              val = str + ic2.ToString(null);
              type = SystemTypes.String;
              break;
            default: return null;
          }
          break;
        default: return null;
      }}
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoDiv(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Object;
      object val = null;
      switch(code1){
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
          int i = ic1.ToInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = i / ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = i / ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Single: 
              val = i / ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = i / ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = i / ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Byte:
        case TypeCode.UInt16:
          ushort us = ic1.ToUInt16(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
              val = us / ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = us / ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.Int64: 
              val = us / ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.UInt64: 
              val = us / ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
              val = us / ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = us / ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.UInt32:
          uint ui = ic1.ToUInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = ui / ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ui / ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.UInt64: 
              val = ui / ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = ui / ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = ui / ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = ui / ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Int64:
          long l = ic1.ToInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = l / ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = l / ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.UInt64: 
              val = l / (long)ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = l / ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = l / ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = l / ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.UInt64:
          ulong ul = ic1.ToUInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ul / ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = ul / ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = ul / ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = ul / ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = ul / ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Single:
          float f = ic1.ToSingle(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
              val = f / ic2.ToInt16(null);
              type = SystemTypes.Single;
              break;
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = f / (double)ic2.ToInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = f / ic2.ToUInt16(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = f / (double)ic2.ToUInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Single: 
              val = f / ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = f / ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = f / (double)ic2.ToDecimal(null);
              type = SystemTypes.Double;
              break;
            default: return null;
          }
          break;
        case TypeCode.Double:
          double d = ic1.ToDouble(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = d / ic2.ToInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = d / ic2.ToUInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
              val = d / ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = d / (double)ic2.ToDecimal(null);
              type = SystemTypes.Double;
              break;
            default: return null;
          }
          break;
        case TypeCode.Decimal:
          decimal dec = ic1.ToDecimal(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32: 
              val = dec / ic2.ToInt32(null); 
              type = SystemTypes.Decimal;
              break;
            case TypeCode.UInt32: 
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = dec / ic2.ToInt64(null); 
              type = SystemTypes.Decimal;
              break;
            case TypeCode.Decimal:
              val = dec / ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        default: return null;
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoEq(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Boolean;
      object val = null;
      switch(code1){
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
          int i = ic1.ToInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = i == ic2.ToInt32(null); 
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt32:
              val = ((long)i) == ic2.ToInt64(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)i) == ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Byte:
        case TypeCode.UInt16:
          ushort us = ic1.ToUInt16(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32: 
              val = ((int)us) == ic2.ToInt32(null); 
              break;
            case TypeCode.UInt32: 
              val = ((uint)us) == ic2.ToUInt32(null); 
              break;
            case TypeCode.Int64: 
              val = ((long)us) == ic2.ToInt64(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)us) == ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.UInt32:
          uint ui = ic1.ToUInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = ((long)ui) == ic2.ToInt64(null); 
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ui == ic2.ToUInt32(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)ui) == ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Int64:
          long l = ic1.ToInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = l == ic2.ToInt64(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)l) == ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.UInt64:
          ulong ul = ic1.ToUInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = ul == ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Single:
        case TypeCode.Double:
        case TypeCode.Decimal:
          return null;
        default: return null;
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoNe(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Boolean;
      object val = null;
      switch(code1){
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
          int i = ic1.ToInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = i != ic2.ToInt32(null); 
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt32:
              val = ((long)i) != ic2.ToInt64(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)i) != ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Byte:
        case TypeCode.UInt16:
          ushort us = ic1.ToUInt16(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32: 
              val = ((int)us) != ic2.ToInt32(null); 
              break;
            case TypeCode.UInt32: 
              val = ((uint)us) != ic2.ToUInt32(null); 
              break;
            case TypeCode.Int64: 
              val = ((long)us) != ic2.ToInt64(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)us) != ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.UInt32:
          uint ui = ic1.ToUInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = ((long)ui) != ic2.ToInt64(null); 
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ui != ic2.ToUInt32(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)ui) != ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Int64:
          long l = ic1.ToInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = l != ic2.ToInt64(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)l) != ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.UInt64:
          ulong ul = ic1.ToUInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = ul != ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Single:
        case TypeCode.Double:
        case TypeCode.Decimal:
          return null;
        default: return null;
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoGt(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Boolean;
      object val = null;
      switch(code1){
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
          int i = ic1.ToInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = i > ic2.ToInt32(null); 
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt32:
              val = ((long)i) > ic2.ToInt64(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)i) > ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Byte:
        case TypeCode.UInt16:
          ushort us = ic1.ToUInt16(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32: 
              val = ((int)us) > ic2.ToInt32(null); 
              break;
            case TypeCode.UInt32: 
              val = ((uint)us) > ic2.ToUInt32(null); 
              break;
            case TypeCode.Int64: 
              val = ((long)us) > ic2.ToInt64(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)us) > ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.UInt32:
          uint ui = ic1.ToUInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = ((long)ui) > ic2.ToInt64(null); 
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ui > ic2.ToUInt32(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)ui) > ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Int64:
          long l = ic1.ToInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = l > ic2.ToInt64(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)l) > ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.UInt64:
          ulong ul = ic1.ToUInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = ul > ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Single:
        case TypeCode.Double:
        case TypeCode.Decimal:
          return null;
        default: return null;
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoGe(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Boolean;
      object val = null;
      switch(code1){
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
          int i = ic1.ToInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = i >= ic2.ToInt32(null); 
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt32:
              val = ((long)i) >= ic2.ToInt64(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)i) >= ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Byte:
        case TypeCode.UInt16:
          ushort us = ic1.ToUInt16(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32: 
              val = ((int)us) >= ic2.ToInt32(null); 
              break;
            case TypeCode.UInt32: 
              val = ((uint)us) >= ic2.ToUInt32(null); 
              break;
            case TypeCode.Int64: 
              val = ((long)us) >= ic2.ToInt64(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)us) >= ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.UInt32:
          uint ui = ic1.ToUInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = ((long)ui) >= ic2.ToInt64(null); 
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ui >= ic2.ToUInt32(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)ui) >= ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Int64:
          long l = ic1.ToInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = l >= ic2.ToInt64(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)l) >= ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.UInt64:
          ulong ul = ic1.ToUInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = ul >= ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Single:
        case TypeCode.Double:
        case TypeCode.Decimal:
          return null;
        default: return null;
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoLt(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Boolean;
      object val = null;
      switch(code1){
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
          int i = ic1.ToInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = i < ic2.ToInt32(null); 
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt32:
              val = ((long)i) < ic2.ToInt64(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)i) < ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Byte:
        case TypeCode.UInt16:
          ushort us = ic1.ToUInt16(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32: 
              val = ((int)us) < ic2.ToInt32(null); 
              break;
            case TypeCode.UInt32: 
              val = ((uint)us) < ic2.ToUInt32(null); 
              break;
            case TypeCode.Int64: 
              val = ((long)us) < ic2.ToInt64(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)us) < ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.UInt32:
          uint ui = ic1.ToUInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = ((long)ui) < ic2.ToInt64(null); 
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ui != ic2.ToUInt32(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)ui) < ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Int64:
          long l = ic1.ToInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = l < ic2.ToInt64(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)l) < ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.UInt64:
          ulong ul = ic1.ToUInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = ul < ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Single:
        case TypeCode.Double:
        case TypeCode.Decimal:
          return null;
        default: return null;
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoLe(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Boolean;
      object val = null;
      switch(code1){
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
          int i = ic1.ToInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = i <= ic2.ToInt32(null); 
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt32:
              val = ((long)i) <= ic2.ToInt64(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)i) <= ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Byte:
        case TypeCode.UInt16:
          ushort us = ic1.ToUInt16(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32: 
              val = ((int)us) <= ic2.ToInt32(null); 
              break;
            case TypeCode.UInt32: 
              val = ((uint)us) <= ic2.ToUInt32(null); 
              break;
            case TypeCode.Int64: 
              val = ((long)us) <= ic2.ToInt64(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)us) <= ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.UInt32:
          uint ui = ic1.ToUInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = ((long)ui) <= ic2.ToInt64(null); 
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ui <= ic2.ToUInt32(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)ui) <= ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Int64:
          long l = ic1.ToInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = l <= ic2.ToInt64(null); 
              break;
            case TypeCode.UInt64: 
              val = ((ulong)l) <= ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.UInt64:
          ulong ul = ic1.ToUInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = ul <= ic2.ToUInt64(null); 
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Single:
        case TypeCode.Double:
        case TypeCode.Decimal:
          return null;
        default: return null;
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoMul(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Object;
      object val = null;
      switch(code1){
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
          int i = ic1.ToInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = i * ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = i * ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Single: 
              val = i * ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = i * ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = i * ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Byte:
        case TypeCode.UInt16:
          ushort us = ic1.ToUInt16(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
              val = us * ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = us * ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.Int64: 
              val = us * ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.UInt64: 
              val = us * ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = us * ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = us * ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = us * ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.UInt32:
          uint ui = ic1.ToUInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = ui * ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ui * ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.UInt64: 
              val = ui * ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = ui * ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = ui * ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = ui * ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Int64:
          long l = ic1.ToInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = l * ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = l * ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.UInt64: 
              val = l * (long)ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = l * ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = l * ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = l * ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.UInt64:
          ulong ul = ic1.ToUInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ul * ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = ul * ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = ul * ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = ul * ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = ul * ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Single:
          float f = ic1.ToSingle(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
              val = f * ic2.ToInt16(null);
              type = SystemTypes.Single;
              break;
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = f * (double)ic2.ToInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = f * ic2.ToUInt16(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = f * (double)ic2.ToUInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
              val = f * ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = f * (double)ic2.ToDecimal(null);
              type = SystemTypes.Double;
              break;
            default: return null;
          }
          break;
        case TypeCode.Double:
          double d = ic1.ToDouble(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = d * ic2.ToInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = d * ic2.ToUInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
              val = d * ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = d * (double)ic2.ToDecimal(null);
              type = SystemTypes.Double;
              break;
            default: return null;
          }
          break;
        case TypeCode.Decimal:
          decimal dec = ic1.ToDecimal(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32: 
              val = dec * ic2.ToInt32(null); 
              type = SystemTypes.Decimal;
              break;
            case TypeCode.UInt32: 
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = dec * ic2.ToInt64(null); 
              type = SystemTypes.Decimal;
              break;
            case TypeCode.Decimal:
              val = dec * ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        default: return null;
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoMulOvf(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Object;
      object val = null;
      checked{switch(code1){
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
          int i = ic1.ToInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = i * ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = i * ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Single: 
              val = i * ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = i * ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = i * ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Byte:
        case TypeCode.UInt16:
          ushort us = ic1.ToUInt16(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
              val = us * ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = us * ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.Int64: 
              val = us * ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.UInt64: 
              val = us * ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = us * ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = us * ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = us * ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.UInt32:
          uint ui = ic1.ToUInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = ui * ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ui * ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.UInt64: 
              val = ui * ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = ui * ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = ui * ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = ui * ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Int64:
          long l = ic1.ToInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = l * ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = l * ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.UInt64: 
              val = l * (long)ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = l * ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = l * ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = l * ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.UInt64:
          ulong ul = ic1.ToUInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ul * ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = ul * ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = ul * ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = ul * ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = ul * ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Single:
          float f = ic1.ToSingle(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
              val = f * ic2.ToInt16(null);
              type = SystemTypes.Single;
              break;
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = f * (double)ic2.ToInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = f * ic2.ToUInt16(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = f * (double)ic2.ToUInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
              val = f * ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = f * (double)ic2.ToDecimal(null);
              type = SystemTypes.Double;
              break;
            default: return null;
          }
          break;
        case TypeCode.Double:
          double d = ic1.ToDouble(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = d * ic2.ToInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = d * ic2.ToUInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
              val = d * ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = d * (double)ic2.ToDecimal(null);
              type = SystemTypes.Double;
              break;
            default: return null;
          }
          break;
        case TypeCode.Decimal:
          decimal dec = ic1.ToDecimal(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32: 
              val = dec * ic2.ToInt32(null); 
              type = SystemTypes.Decimal;
              break;
            case TypeCode.UInt32: 
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = dec * ic2.ToInt64(null); 
              type = SystemTypes.Decimal;
              break;
            case TypeCode.Decimal:
              val = dec * ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        default: return null;
      }}
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoAnd(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Object;
      object val = null;
      switch(code1){
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
          int i = ic1.ToInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = i & ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.UInt32: 
              if (i >= 0){
                val = ((uint)i) & ic2.ToUInt32(null); 
                type = SystemTypes.UInt32;
                break;
              }
              goto case TypeCode.Int64;
            case TypeCode.Int64: 
              val = ((long)i) & ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.UInt64: 
              val = ((ulong)i) & ic2.ToUInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Byte:
        case TypeCode.Char:
        case TypeCode.UInt16:
          ushort us = ic1.ToUInt16(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
              val = ((int)us) & ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.UInt32: 
              val = ((uint)us) & ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.Int64: 
              val = ((long)us) & ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.UInt64: 
              val = ((ulong)us) & ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.UInt32:
          uint ui = ic1.ToUInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
              i = ic2.ToInt32(null);
              if (i >= 0){
                val = ui & (uint)i;
                type = SystemTypes.UInt32;
                break;
              }
              goto case TypeCode.Int64;
            case TypeCode.Int64: 
              val = ((long)ui) & ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ui & ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.UInt64: 
              val = ((ulong)ui) & ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Int64:
          long l = ic1.ToInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32:
              val = l & ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.UInt64: 
              val = ((ulong)l) & ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.UInt64:
          ulong ul = ic1.ToUInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = ul & ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Single:
        case TypeCode.Double:
        case TypeCode.Decimal:
          return null;
        default: return null;
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoOr(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Object;
      object val = null;
      switch(code1){
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
          int i = ic1.ToInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = i | ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.UInt32: 
              if (i >= 0){
                val = ((uint)i) | ic2.ToUInt32(null); 
                type = SystemTypes.UInt32;
                break;
              }
              goto case TypeCode.Int64;
            case TypeCode.Int64: 
              long lng = i;
              val = lng | ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.UInt64: 
              ulong ulng = (ulong) i;
              val = ulng | ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Byte:
        case TypeCode.Char:
        case TypeCode.UInt16:
          ushort us = ic1.ToUInt16(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
              val = ((int)us) | ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.UInt32: 
              val = ((uint)us) | ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.Int64: 
              val = ((long)us) | ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.UInt64: 
              val = ((ulong)us) | ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.UInt32:
          uint ui = ic1.ToUInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
              i = ic2.ToInt32(null);
              if (i >= 0){
                val = ui | (uint)i;
                type = SystemTypes.UInt32;
                break;
              }
              goto case TypeCode.Int64;
            case TypeCode.Int64: 
              val = ((long)ui) | ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ui | ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.UInt64: 
              val = ((ulong)ui) | ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Int64:
          long l = ic1.ToInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32:
              val = l | ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.UInt64: 
              val = ((ulong)l) | ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.UInt64:
          ulong ul = ic1.ToUInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = ul | ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
            case TypeCode.Decimal:
              return null;
            default: return null;
          }
          break;
        case TypeCode.Single:
        case TypeCode.Double:
        case TypeCode.Decimal:
          return null;
        default: return null;
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoLeftShift(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Object;
      object val = null;
      int shift = 0;
      switch (code2){
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
        case TypeCode.Byte:
        case TypeCode.UInt16:
          shift = ic2.ToInt32(null) & 0x3f;
          break;
        case TypeCode.Int64: 
          shift = ((int)ic2.ToInt64(null)) & 0x3f;
          break;
        case TypeCode.UInt32: 
        case TypeCode.UInt64: 
          shift = ((int)ic2.ToUInt64(null)) & 0x3f;
          break;
        default: return null;
      }
      switch(code1){
        case TypeCode.SByte:
        case TypeCode.Byte:
        case TypeCode.Int16:
        case TypeCode.UInt16:
        case TypeCode.Char:
        case TypeCode.Int32:
          int i = ic1.ToInt32(null);
          val = i << shift; 
          type = SystemTypes.Int32;
          break;
        case TypeCode.UInt32:
          uint ui = ic1.ToUInt32(null);
          val = ui << shift; 
          type = SystemTypes.UInt32;
          break;
        case TypeCode.Int64:
          long l = ic1.ToInt64(null);
          val = l << shift; 
          type = SystemTypes.Int64;
          break;
        case TypeCode.UInt64:
          ulong ul = ic1.ToUInt64(null);
          val = ul << shift; 
          type = SystemTypes.UInt64;
          break;
        default: return null;
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoRem(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Object;
      object val = null;
      switch(code1){
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
          int i = ic1.ToInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = i % ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = i % ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Single: 
              val = i % ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = i % ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = i % ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Byte:
        case TypeCode.UInt16:
          ushort us = ic1.ToUInt16(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
              val = us % ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = us % ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.Int64: 
              val = us % ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.UInt64: 
              val = us % ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
              val = us % ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = us % ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.UInt32:
          uint ui = ic1.ToUInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = ui % ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ui % ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.UInt64: 
              val = ui % ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = ui % ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = ui % ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = ui % ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Int64:
          long l = ic1.ToInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = l % ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = l % ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.UInt64: 
              val = l % (long)ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = l % ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = l % ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = l % ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.UInt64:
          ulong ul = ic1.ToUInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ul % ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = ul % ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = ul % ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = ul % ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = ul % ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Single:
          float f = ic1.ToSingle(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
              val = f % ic2.ToInt16(null);
              type = SystemTypes.Single;
              break;
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = f % (double)ic2.ToInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = f % ic2.ToUInt16(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = f % (double)ic2.ToUInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Single: 
              val = f % ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = f % ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = f % (double)ic2.ToDecimal(null);
              type = SystemTypes.Double;
              break;
            default: return null;
          }
          break;
        case TypeCode.Double:
          double d = ic1.ToDouble(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = d % ic2.ToInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = d % ic2.ToUInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
              val = d % ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = d % (double)ic2.ToDecimal(null);
              type = SystemTypes.Double;
              break;
            default: return null;
          }
          break;
        case TypeCode.Decimal:
          decimal dec = ic1.ToDecimal(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32: 
              val = dec % ic2.ToInt32(null); 
              type = SystemTypes.Decimal;
              break;
            case TypeCode.UInt32: 
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = dec % ic2.ToInt64(null); 
              type = SystemTypes.Decimal;
              break;
            case TypeCode.Decimal:
              val = dec % ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        default: return null;
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoRightShift(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Object;
      object val = null;
      int shift = 0;
      switch (code2){
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
        case TypeCode.Byte:
        case TypeCode.UInt16:
          shift = ic2.ToInt32(null) & 0x3f;
          break;
        case TypeCode.Int64: 
          shift = ((int)ic2.ToInt64(null)) & 0x3f;
          break;
        case TypeCode.UInt32: 
        case TypeCode.UInt64: 
          shift = ((int)ic2.ToUInt64(null)) & 0x3f;
          break;
        default: return null;
      }
      switch(code1){
        case TypeCode.SByte:
        case TypeCode.Byte:
        case TypeCode.Int16:
        case TypeCode.UInt16:
        case TypeCode.Char:
        case TypeCode.Int32:
          int i = ic1.ToInt32(null);
          val = i >> shift; 
          type = SystemTypes.Int32;
          break;
        case TypeCode.UInt32:
          uint ui = ic1.ToUInt32(null);
          val = ui >> shift; 
          type = SystemTypes.UInt32;
          break;
        case TypeCode.Int64:
          long l = ic1.ToInt64(null);
          val = l >> shift; 
          type = SystemTypes.Int64;
          break;
        case TypeCode.UInt64:
          ulong ul = ic1.ToUInt64(null);
          val = ul >> shift; 
          type = SystemTypes.UInt64;
          break;
        default: return null;
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoSub(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Object;
      object val = null;
      switch(code1){
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
          int i = ic1.ToInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = i - ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = i - ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Single: 
              val = i - ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = i - ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = i - ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Byte:
        case TypeCode.UInt16:
          ushort us = ic1.ToUInt16(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
              val = us - ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = us - ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.Int64: 
              val = us - ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.UInt64: 
              val = us - ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = us - ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = us - ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = us - ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Char:
          char ch = ic1.ToChar(null);
          if (code2 != TypeCode.Char) goto default;
          val = ch - ic2.ToChar(null);
          type = SystemTypes.Int32;
          break;
        case TypeCode.UInt32:
          uint ui = ic1.ToUInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = ui - ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ui - ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.UInt64: 
              val = ui - ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = ui - ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = ui - ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = ui - ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Int64:
          long l = ic1.ToInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = l - ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = l - ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.UInt64: 
              val = l - (long)ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = l - ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = l - ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = l - ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.UInt64:
          ulong ul = ic1.ToUInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ul - ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = ul - ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = ul - ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = ul - ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = ul - ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Single:
          float f = ic1.ToSingle(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
              val = f - ic2.ToInt16(null);
              type = SystemTypes.Single;
              break;
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = f - (double)ic2.ToInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = f - ic2.ToUInt16(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = f - (double)ic2.ToUInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
              val = f - ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = f - (double)ic2.ToDecimal(null);
              type = SystemTypes.Double;
              break;
            default: return null;
          }
          break;
        case TypeCode.Double:
          double d = ic1.ToDouble(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = d - ic2.ToInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = d - ic2.ToUInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
              val = d - ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = d - (double)ic2.ToDecimal(null);
              type = SystemTypes.Double;
              break;
            default: return null;
          }
          break;
        case TypeCode.Decimal:
          decimal dec = ic1.ToDecimal(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32: 
              val = dec - ic2.ToInt32(null); 
              type = SystemTypes.Decimal;
              break;
            case TypeCode.UInt32: 
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = dec - ic2.ToInt64(null); 
              type = SystemTypes.Decimal;
              break;
            case TypeCode.Decimal:
              val = dec - ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        default: return null;
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    public static Literal DoSubOvf(IConvertible ic1, IConvertible ic2, TypeCode code1, TypeCode code2, BinaryExpression binaryExpression){
      TypeNode type = SystemTypes.Object;
      object val = null;
      checked{switch(code1){
        case TypeCode.SByte:
        case TypeCode.Int16:
        case TypeCode.Int32:
          int i = ic1.ToInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = i - ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = i - ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Single: 
              val = i - ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = i - ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = i - ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Byte:
        case TypeCode.UInt16:
          ushort us = ic1.ToUInt16(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
              val = us - ic2.ToInt32(null); 
              type = SystemTypes.Int32;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = us - ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.Int64: 
              val = us - ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.UInt64: 
              val = us - ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
              val = us - ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = us - ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Char:
          char ch = ic1.ToChar(null);
          if (code2 != TypeCode.Char) goto default;
          val = ch - ic2.ToChar(null);
          type = SystemTypes.Int32;
          break;
        case TypeCode.UInt32:
          uint ui = ic1.ToUInt32(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = ui - ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ui - ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.UInt64: 
              val = ui - ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = ui - ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = ui - ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = ui - ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Int64:
          long l = ic1.ToInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = l - ic2.ToInt64(null); 
              type = SystemTypes.Int64;
              break;
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = l - ic2.ToUInt32(null); 
              type = SystemTypes.UInt32;
              break;
            case TypeCode.UInt64: 
              val = l - (long)ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = l - ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = l - ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = l - ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.UInt64:
          ulong ul = ic1.ToUInt64(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Byte:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.UInt32: 
              val = ul - ic2.ToUInt32(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = ul - ic2.ToUInt64(null); 
              type = SystemTypes.UInt64;
              break;
            case TypeCode.Single: 
              val = ul - ic2.ToSingle(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.Double: 
              val = ul - ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = ul - ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        case TypeCode.Single:
          float f = ic1.ToSingle(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
              val = f - ic2.ToInt16(null);
              type = SystemTypes.Single;
              break;
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = f - (double)ic2.ToInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
              val = f - ic2.ToUInt16(null); 
              type = SystemTypes.Single;
              break;
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = f - (double)ic2.ToUInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
              val = f - ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = f - (double)ic2.ToDecimal(null);
              type = SystemTypes.Double;
              break;
            default: return null;
          }
          break;
        case TypeCode.Double:
          double d = ic1.ToDouble(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.Int32: 
            case TypeCode.Int64: 
              val = d - ic2.ToInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Byte:
            case TypeCode.Char:
            case TypeCode.UInt16:
            case TypeCode.UInt32: 
            case TypeCode.UInt64: 
              val = d - ic2.ToUInt64(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Single: 
            case TypeCode.Double: 
              val = d - ic2.ToDouble(null); 
              type = SystemTypes.Double;
              break;
            case TypeCode.Decimal:
              val = d - (double)ic2.ToDecimal(null);
              type = SystemTypes.Double;
              break;
            default: return null;
          }
          break;
        case TypeCode.Decimal:
          decimal dec = ic1.ToDecimal(null);
          switch(code2){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
            case TypeCode.Int32: 
              val = dec - ic2.ToInt32(null); 
              type = SystemTypes.Decimal;
              break;
            case TypeCode.UInt32: 
            case TypeCode.Int64: 
            case TypeCode.UInt64: 
              val = dec - ic2.ToInt64(null); 
              type = SystemTypes.Decimal;
              break;
            case TypeCode.Decimal:
              val = dec - ic2.ToDecimal(null);
              type = SystemTypes.Decimal;
              break;
            default: return null;
          }
          break;
        default: return null;
      }
      }
      return new Literal(val, type, binaryExpression.SourceContext);
    }
    #endregion // binary evaluators

    public static Literal EvalUnaryExpression(Literal opnd1, UnaryExpression unaryExpression){
      if (unaryExpression == null) return null;
      if (opnd1 == null) return null;
      object val = opnd1.Value;
      IConvertible ic = val as IConvertible;
      TypeCode code = ic == null ? TypeCode.Object : ic.GetTypeCode();
      TypeNode type = SystemTypes.Object;
      switch (unaryExpression.NodeType){
        case NodeType.AddressOf:
        case NodeType.OutAddress:
        case NodeType.RefAddress:
        case NodeType.Ckfinite :
        case NodeType.Conv_I :
          return null;
        case NodeType.Conv_I1 :
          unchecked{
            switch(code){
              case TypeCode.Boolean: return null;
              case TypeCode.Byte: val = (sbyte)ic.ToByte(null); break;
              case TypeCode.Char: val = (sbyte)ic.ToChar(null); break;
              case TypeCode.DateTime: return null;
              case TypeCode.DBNull: return null;
              case TypeCode.Decimal: val = (sbyte)ic.ToDecimal(null); break;
              case TypeCode.Double: val = (sbyte)ic.ToDouble(null); break;
              case TypeCode.Empty: return null;
              case TypeCode.Int16: val = (sbyte)ic.ToInt16(null); break;
              case TypeCode.Int32: val = (sbyte)ic.ToInt32(null); break;
              case TypeCode.Int64: val = (sbyte)ic.ToInt64(null); break;
              case TypeCode.Object: return null;
              case TypeCode.SByte: val = (sbyte)ic.ToSByte(null); break;
              case TypeCode.Single: val = (sbyte)ic.ToSingle(null); break;
              case TypeCode.String: return null;
              case TypeCode.UInt16: val = (sbyte)ic.ToUInt16(null); break;
              case TypeCode.UInt32: val = (sbyte)ic.ToUInt32(null); break;
              case TypeCode.UInt64: val = (sbyte)ic.ToUInt64(null); break;
            }
          }
          type = SystemTypes.Int8;
          break;
        case NodeType.Conv_I2 :
          unchecked{
            switch(code){
              case TypeCode.Boolean: return null;
              case TypeCode.Byte: val = (short)ic.ToByte(null); break;
              case TypeCode.Char: val = (short)ic.ToChar(null); break;
              case TypeCode.DateTime: return null;
              case TypeCode.DBNull: return null;
              case TypeCode.Decimal: val = (short)ic.ToDecimal(null); break;
              case TypeCode.Double: val = (short)ic.ToDouble(null); break;
              case TypeCode.Empty: return null;
              case TypeCode.Int16: val = (short)ic.ToInt16(null); break;
              case TypeCode.Int32: val = (short)ic.ToInt32(null); break;
              case TypeCode.Int64: val = (short)ic.ToInt64(null); break;
              case TypeCode.Object: return null;
              case TypeCode.SByte: val = (short)ic.ToSByte(null); break;
              case TypeCode.Single: val = (short)ic.ToSingle(null); break;
              case TypeCode.String: return null;
              case TypeCode.UInt16: val = (short)ic.ToUInt16(null); break;
              case TypeCode.UInt32: val = (short)ic.ToUInt32(null); break;
              case TypeCode.UInt64: val = (short)ic.ToUInt64(null); break;
            }
          }
          type = SystemTypes.Int16;
          break;
        case NodeType.Conv_I4 :
          unchecked{
            switch(code){
              case TypeCode.Boolean: return null;
              case TypeCode.Byte: val = (int)ic.ToByte(null); break;
              case TypeCode.Char: val = (int)ic.ToChar(null); break;
              case TypeCode.DateTime: return null;
              case TypeCode.DBNull: return null;
              case TypeCode.Decimal: val = (int)ic.ToDecimal(null); break;
              case TypeCode.Double: val = (int)ic.ToDouble(null); break;
              case TypeCode.Empty: return null;
              case TypeCode.Int16: val = (int)ic.ToInt16(null); break;
              case TypeCode.Int32: val = (int)ic.ToInt32(null); break;
              case TypeCode.Int64: val = (int)ic.ToInt64(null); break;
              case TypeCode.Object: return null;
              case TypeCode.SByte: val = (int)ic.ToSByte(null); break;
              case TypeCode.Single: val = (int)ic.ToSingle(null); break;
              case TypeCode.String: return null;
              case TypeCode.UInt16: val = (int)ic.ToUInt16(null); break;
              case TypeCode.UInt32: val = (int)ic.ToUInt32(null); break;
              case TypeCode.UInt64: val = (int)ic.ToUInt64(null); break;
            }
          }
          type = SystemTypes.Int32;
          break;
        case NodeType.Conv_I8 :
          unchecked{
            switch(code){
              case TypeCode.Boolean: return null;
              case TypeCode.Byte: val = (long)ic.ToByte(null); break;
              case TypeCode.Char: val = (long)ic.ToChar(null); break;
              case TypeCode.DateTime: return null;
              case TypeCode.DBNull: return null;
              case TypeCode.Decimal: val = (long)ic.ToDecimal(null); break;
              case TypeCode.Double: val = (long)ic.ToDouble(null); break;
              case TypeCode.Empty: return null;
              case TypeCode.Int16: val = (long)ic.ToInt16(null); break;
              case TypeCode.Int32: val = (long)ic.ToInt32(null); break;
              case TypeCode.Int64: val = (long)ic.ToInt64(null); break;
              case TypeCode.Object: return null;
              case TypeCode.SByte: val = (long)ic.ToSByte(null); break;
              case TypeCode.Single: val = (long)ic.ToSingle(null); break;
              case TypeCode.String: return null;
              case TypeCode.UInt16: val = (long)ic.ToUInt16(null); break;
              case TypeCode.UInt32: val = (long)ic.ToUInt32(null); break;
              case TypeCode.UInt64: val = (long)ic.ToUInt64(null); break;
            }
          }
          type = SystemTypes.Int64;
          break;
        case NodeType.Conv_Ovf_I :
        case NodeType.Conv_Ovf_I_Un :
          return null;
        case NodeType.Conv_Ovf_I1 :
        case NodeType.Conv_Ovf_I1_Un :
          checked{
            switch(code){
              case TypeCode.Boolean: return null;
              case TypeCode.Byte: val = (sbyte)ic.ToByte(null); break;
              case TypeCode.Char: val = (sbyte)ic.ToChar(null); break;
              case TypeCode.DateTime: return null;
              case TypeCode.DBNull: return null;
              case TypeCode.Decimal: val = (sbyte)ic.ToDecimal(null); break;
              case TypeCode.Double: val = (sbyte)ic.ToDouble(null); break;
              case TypeCode.Empty: return null;
              case TypeCode.Int16: val = (sbyte)ic.ToInt16(null); break;
              case TypeCode.Int32: val = (sbyte)ic.ToInt32(null); break;
              case TypeCode.Int64: val = (sbyte)ic.ToInt64(null); break;
              case TypeCode.Object: return null;
              case TypeCode.SByte: val = (sbyte)ic.ToSByte(null); break;
              case TypeCode.Single: val = (sbyte)ic.ToSingle(null); break;
              case TypeCode.String: return null;
              case TypeCode.UInt16: val = (sbyte)ic.ToUInt16(null); break;
              case TypeCode.UInt32: val = (sbyte)ic.ToUInt32(null); break;
              case TypeCode.UInt64: val = (sbyte)ic.ToUInt64(null); break;
            }
          }
          type = SystemTypes.Int8;
          break;
        case NodeType.Conv_Ovf_I2 :
        case NodeType.Conv_Ovf_I2_Un :
          checked{
            switch(code){
              case TypeCode.Boolean: return null;
              case TypeCode.Byte: val = (short)ic.ToByte(null); break;
              case TypeCode.Char: val = (short)ic.ToChar(null); break;
              case TypeCode.DateTime: return null;
              case TypeCode.DBNull: return null;
              case TypeCode.Decimal: val = (short)ic.ToDecimal(null); break;
              case TypeCode.Double: val = (short)ic.ToDouble(null); break;
              case TypeCode.Empty: return null;
              case TypeCode.Int16: val = (short)ic.ToInt16(null); break;
              case TypeCode.Int32: val = (short)ic.ToInt32(null); break;
              case TypeCode.Int64: val = (short)ic.ToInt64(null); break;
              case TypeCode.Object: return null;
              case TypeCode.SByte: val = (short)ic.ToSByte(null); break;
              case TypeCode.Single: val = (short)ic.ToSingle(null); break;
              case TypeCode.String: return null;
              case TypeCode.UInt16: val = (short)ic.ToUInt16(null); break;
              case TypeCode.UInt32: val = (short)ic.ToUInt32(null); break;
              case TypeCode.UInt64: val = (short)ic.ToUInt64(null); break;
            }
          }
          type = SystemTypes.Int16;
          break;
        case NodeType.Conv_Ovf_I4 :
        case NodeType.Conv_Ovf_I4_Un :
          checked{
            switch(code){
              case TypeCode.Boolean: return null;
              case TypeCode.Byte: val = (int)ic.ToByte(null); break;
              case TypeCode.Char: val = (int)ic.ToChar(null); break;
              case TypeCode.DateTime: return null;
              case TypeCode.DBNull: return null;
              case TypeCode.Decimal: val = (int)ic.ToDecimal(null); break;
              case TypeCode.Double: val = (int)ic.ToDouble(null); break;
              case TypeCode.Empty: return null;
              case TypeCode.Int16: val = (int)ic.ToInt16(null); break;
              case TypeCode.Int32: val = (int)ic.ToInt32(null); break;
              case TypeCode.Int64: val = (int)ic.ToInt64(null); break;
              case TypeCode.Object: return null; //TODO: look for overloaded operator
              case TypeCode.SByte: val = (int)ic.ToSByte(null); break;
              case TypeCode.Single: val = (int)ic.ToSingle(null); break;
              case TypeCode.String: return null;
              case TypeCode.UInt16: val = (int)ic.ToUInt16(null); break;
              case TypeCode.UInt32: val = (int)ic.ToUInt32(null); break;
              case TypeCode.UInt64: val = (int)ic.ToUInt64(null); break;
            }
          }
          type = SystemTypes.Int32;
          break;
        case NodeType.Conv_Ovf_I8 :
        case NodeType.Conv_Ovf_I8_Un :
          checked{
            switch(code){
              case TypeCode.Boolean: return null;
              case TypeCode.Byte: val = (long)ic.ToByte(null); break;
              case TypeCode.Char: val = (long)ic.ToChar(null); break;
              case TypeCode.DateTime: return null;
              case TypeCode.DBNull: return null;
              case TypeCode.Decimal: val = (long)ic.ToDecimal(null); break;
              case TypeCode.Double: val = (long)ic.ToDouble(null); break;
              case TypeCode.Empty: return null;
              case TypeCode.Int16: val = (long)ic.ToInt16(null); break;
              case TypeCode.Int32: val = (long)ic.ToInt32(null); break;
              case TypeCode.Int64: val = (long)ic.ToInt64(null); break;
              case TypeCode.Object: return null;
              case TypeCode.SByte: val = (long)ic.ToSByte(null); break;
              case TypeCode.Single: val = (long)ic.ToSingle(null); break;
              case TypeCode.String: return null;
              case TypeCode.UInt16: val = (long)ic.ToUInt16(null); break;
              case TypeCode.UInt32: val = (long)ic.ToUInt32(null); break;
              case TypeCode.UInt64: val = (long)ic.ToUInt64(null); break;
            }
          }
          type = SystemTypes.Int64;
          break;
        case NodeType.Conv_Ovf_U :
        case NodeType.Conv_Ovf_U_Un :
          return null;
        case NodeType.Conv_Ovf_U1 :
        case NodeType.Conv_Ovf_U1_Un :
          checked{
            switch(code){
              case TypeCode.Boolean: return null;
              case TypeCode.Byte: val = (byte)ic.ToByte(null); break;
              case TypeCode.Char: val = (byte)ic.ToChar(null); break;
              case TypeCode.DateTime: return null;
              case TypeCode.DBNull: return null;
              case TypeCode.Decimal: val = (byte)ic.ToDecimal(null); break;
              case TypeCode.Double: val = (byte)ic.ToDouble(null); break;
              case TypeCode.Empty: return null;
              case TypeCode.Int16: val = (byte)ic.ToInt16(null); break;
              case TypeCode.Int32: val = (byte)ic.ToInt32(null); break;
              case TypeCode.Int64: val = (byte)ic.ToInt64(null); break;
              case TypeCode.Object: return null;
              case TypeCode.SByte: val = (byte)ic.ToSByte(null); break;
              case TypeCode.Single: val = (byte)ic.ToSingle(null); break;
              case TypeCode.String: return null;
              case TypeCode.UInt16: val = (byte)ic.ToUInt16(null); break;
              case TypeCode.UInt32: val = (byte)ic.ToUInt32(null); break;
              case TypeCode.UInt64: val = (byte)ic.ToUInt64(null); break;
            }
          }
          type = SystemTypes.UInt8;
          break;
        case NodeType.Conv_Ovf_U2 :
        case NodeType.Conv_Ovf_U2_Un :
          checked{
            switch(code){
              case TypeCode.Boolean: return null;
              case TypeCode.Byte: val = (ushort)ic.ToByte(null); break;
              case TypeCode.Char: val = (ushort)ic.ToChar(null); break;
              case TypeCode.DateTime: return null;
              case TypeCode.DBNull: return null;
              case TypeCode.Decimal: val = (ushort)ic.ToDecimal(null); break;
              case TypeCode.Double: val = (ushort)ic.ToDouble(null); break;
              case TypeCode.Empty: return null;
              case TypeCode.Int16: val = (ushort)ic.ToInt16(null); break;
              case TypeCode.Int32: val = (ushort)ic.ToInt32(null); break;
              case TypeCode.Int64: val = (ushort)ic.ToInt64(null); break;
              case TypeCode.Object: return null;
              case TypeCode.SByte: val = (ushort)ic.ToSByte(null); break;
              case TypeCode.Single: val = (ushort)ic.ToSingle(null); break;
              case TypeCode.String: return null;
              case TypeCode.UInt16: val = (ushort)ic.ToUInt16(null); break;
              case TypeCode.UInt32: val = (ushort)ic.ToUInt32(null); break;
              case TypeCode.UInt64: val = (ushort)ic.ToUInt64(null); break;
            }
          }
          type = SystemTypes.UInt16;
          break;
        case NodeType.Conv_Ovf_U4 :
        case NodeType.Conv_Ovf_U4_Un :
          checked{
            switch(code){
              case TypeCode.Boolean: return null;
              case TypeCode.Byte: val = (uint)ic.ToByte(null); break;
              case TypeCode.Char: val = (uint)ic.ToChar(null); break;
              case TypeCode.DateTime: return null;
              case TypeCode.DBNull: return null;
              case TypeCode.Decimal: val = (uint)ic.ToDecimal(null); break;
              case TypeCode.Double: val = (uint)ic.ToDouble(null); break;
              case TypeCode.Empty: return null;
              case TypeCode.Int16: val = (uint)ic.ToInt16(null); break;
              case TypeCode.Int32: val = (uint)ic.ToInt32(null); break;
              case TypeCode.Int64: val = (uint)ic.ToInt64(null); break;
              case TypeCode.Object: return null;
              case TypeCode.SByte: val = (uint)ic.ToSByte(null); break;
              case TypeCode.Single: val = (uint)ic.ToSingle(null); break;
              case TypeCode.String: return null;
              case TypeCode.UInt16: val = (uint)ic.ToUInt16(null); break;
              case TypeCode.UInt32: val = (uint)ic.ToUInt32(null); break;
              case TypeCode.UInt64: val = (uint)ic.ToUInt64(null); break;
            }
          }
          type = SystemTypes.UInt32;
          break;
        case NodeType.Conv_Ovf_U8 :
        case NodeType.Conv_Ovf_U8_Un :
          checked{
            switch(code){
              case TypeCode.Boolean: return null;
              case TypeCode.Byte: val = (ulong)ic.ToByte(null); break;
              case TypeCode.Char: val = (ulong)ic.ToChar(null); break;
              case TypeCode.DateTime: return null;
              case TypeCode.DBNull: return null;
              case TypeCode.Decimal: val = (ulong)ic.ToDecimal(null); break;
              case TypeCode.Double: val = (ulong)ic.ToDouble(null); break;
              case TypeCode.Empty: return null;
              case TypeCode.Int16: val = (ulong)ic.ToInt16(null); break;
              case TypeCode.Int32: val = (ulong)ic.ToInt32(null); break;
              case TypeCode.Int64: val = (ulong)ic.ToInt64(null); break;
              case TypeCode.Object: return null;
              case TypeCode.SByte: val = (ulong)ic.ToSByte(null); break;
              case TypeCode.Single: val = (ulong)ic.ToSingle(null); break;
              case TypeCode.String: return null;
              case TypeCode.UInt16: val = (ulong)ic.ToUInt16(null); break;
              case TypeCode.UInt32: val = (ulong)ic.ToUInt32(null); break;
              case TypeCode.UInt64: val = (ulong)ic.ToUInt64(null); break;
            }
          }
          type = SystemTypes.UInt64;
          break;
        case NodeType.Conv_R4 :
          unchecked{
            switch(code){
              case TypeCode.Boolean: return null;
              case TypeCode.Byte: val = (float)ic.ToByte(null); break;
              case TypeCode.Char: val = (float)ic.ToChar(null); break;
              case TypeCode.DateTime: return null;
              case TypeCode.DBNull: return null;
              case TypeCode.Decimal: val = (float)ic.ToDecimal(null); break;
              case TypeCode.Double: val = (float)ic.ToDouble(null); break;
              case TypeCode.Empty: return null;
              case TypeCode.Int16: val = (float)ic.ToInt16(null); break;
              case TypeCode.Int32: val = (float)ic.ToInt32(null); break;
              case TypeCode.Int64: val = (float)ic.ToInt64(null); break;
              case TypeCode.Object: return null;
              case TypeCode.SByte: val = (float)ic.ToSByte(null); break;
              case TypeCode.Single: val = (float)ic.ToSingle(null); break;
              case TypeCode.String: return null;
              case TypeCode.UInt16: val = (float)ic.ToUInt16(null); break;
              case TypeCode.UInt32: val = (float)ic.ToUInt32(null); break;
              case TypeCode.UInt64: val = (float)ic.ToUInt64(null); break;
            }
          }
          type = SystemTypes.Single;
          break;
        case NodeType.Conv_R8 :
          unchecked{
            switch(code){
              case TypeCode.Boolean: return null;
              case TypeCode.Byte: val = (double)ic.ToByte(null); break;
              case TypeCode.Char: val = (double)ic.ToChar(null); break;
              case TypeCode.DateTime: return null;
              case TypeCode.DBNull: return null;
              case TypeCode.Decimal: val = (double)ic.ToDecimal(null); break;
              case TypeCode.Double: val = (double)ic.ToDouble(null); break;
              case TypeCode.Empty: return null;
              case TypeCode.Int16: val = (double)ic.ToInt16(null); break;
              case TypeCode.Int32: val = (double)ic.ToInt32(null); break;
              case TypeCode.Int64: val = (double)ic.ToInt64(null); break;
              case TypeCode.Object: return null;
              case TypeCode.SByte: val = (double)ic.ToSByte(null); break;
              case TypeCode.Single: val = (double)ic.ToSingle(null); break;
              case TypeCode.String: return null;
              case TypeCode.UInt16: val = (double)ic.ToUInt16(null); break;
              case TypeCode.UInt32: val = (double)ic.ToUInt32(null); break;
              case TypeCode.UInt64: val = (double)ic.ToUInt64(null); break;
            }
          }
          type = SystemTypes.Double;
          break;
        case NodeType.Conv_R_Un :
        case NodeType.Conv_U :
          return null;
        case NodeType.Conv_U1 :
          unchecked{
            switch(code){
              case TypeCode.Boolean: return null;
              case TypeCode.Byte: val = (byte)ic.ToByte(null); break;
              case TypeCode.Char: val = (byte)ic.ToChar(null); break;
              case TypeCode.DateTime: return null;
              case TypeCode.DBNull: return null;
              case TypeCode.Decimal: val = (byte)ic.ToDecimal(null); break;
              case TypeCode.Double: val = (byte)ic.ToDouble(null); break;
              case TypeCode.Empty: return null;
              case TypeCode.Int16: val = (byte)ic.ToInt16(null); break;
              case TypeCode.Int32: val = (byte)ic.ToInt32(null); break;
              case TypeCode.Int64: val = (byte)ic.ToInt64(null); break;
              case TypeCode.Object: return null;
              case TypeCode.SByte: val = (byte)ic.ToSByte(null); break;
              case TypeCode.Single: val = (byte)ic.ToSingle(null); break;
              case TypeCode.String: return null;
              case TypeCode.UInt16: val = (byte)ic.ToUInt16(null); break;
              case TypeCode.UInt32: val = (byte)ic.ToUInt32(null); break;
              case TypeCode.UInt64: val = (byte)ic.ToUInt64(null); break;
            }
          }
          type = SystemTypes.UInt8;
          break;
        case NodeType.Conv_U2 :
          unchecked{
            switch(code){
              case TypeCode.Boolean: return null;
              case TypeCode.Byte: val = (ushort)ic.ToByte(null); break;
              case TypeCode.Char: val = (ushort)ic.ToChar(null); break;
              case TypeCode.DateTime: return null;
              case TypeCode.DBNull: return null;
              case TypeCode.Decimal: val = (ushort)ic.ToDecimal(null); break;
              case TypeCode.Double: val = (ushort)ic.ToDouble(null); break;
              case TypeCode.Empty: return null;
              case TypeCode.Int16: val = (ushort)ic.ToInt16(null); break;
              case TypeCode.Int32: val = (ushort)ic.ToInt32(null); break;
              case TypeCode.Int64: val = (ushort)ic.ToInt64(null); break;
              case TypeCode.Object: return null;
              case TypeCode.SByte: val = (ushort)ic.ToSByte(null); break;
              case TypeCode.Single: val = (ushort)ic.ToSingle(null); break;
              case TypeCode.String: return null;
              case TypeCode.UInt16: val = (ushort)ic.ToUInt16(null); break;
              case TypeCode.UInt32: val = (ushort)ic.ToUInt32(null); break;
              case TypeCode.UInt64: val = (ushort)ic.ToUInt64(null); break;
            }
          }
          type = SystemTypes.UInt16;
          break;
        case NodeType.Conv_U4 :
          unchecked{
            switch(code){
              case TypeCode.Boolean: return null;
              case TypeCode.Byte: val = (uint)ic.ToByte(null); break;
              case TypeCode.Char: val = (uint)ic.ToChar(null); break;
              case TypeCode.DateTime: return null;
              case TypeCode.DBNull: return null;
              case TypeCode.Decimal: val = (uint)ic.ToDecimal(null); break;
              case TypeCode.Double: val = (uint)ic.ToDouble(null); break;
              case TypeCode.Empty: return null;
              case TypeCode.Int16: val = (uint)ic.ToInt16(null); break;
              case TypeCode.Int32: val = (uint)ic.ToInt32(null); break;
              case TypeCode.Int64: val = (uint)ic.ToInt64(null); break;
              case TypeCode.Object: return null;
              case TypeCode.SByte: val = (uint)ic.ToSByte(null); break;
              case TypeCode.Single: val = (uint)ic.ToSingle(null); break;
              case TypeCode.String: return null;
              case TypeCode.UInt16: val = (uint)ic.ToUInt16(null); break;
              case TypeCode.UInt32: val = (uint)ic.ToUInt32(null); break;
              case TypeCode.UInt64: val = (uint)ic.ToUInt64(null); break;
            }
          }
          type = SystemTypes.UInt32;
          break;
        case NodeType.Conv_U8 :
          unchecked{
            switch(code){
              case TypeCode.Boolean: return null;
              case TypeCode.Byte: val = (ulong)ic.ToByte(null); break;
              case TypeCode.Char: val = (ulong)ic.ToChar(null); break;
              case TypeCode.DateTime: return null;
              case TypeCode.DBNull: return null;
              case TypeCode.Decimal: val = (ulong)ic.ToDecimal(null); break;
              case TypeCode.Double: val = (ulong)ic.ToDouble(null); break;
              case TypeCode.Empty: return null;
              case TypeCode.Int16: val = (ulong)ic.ToInt16(null); break;
              case TypeCode.Int32: val = (ulong)ic.ToInt32(null); break;
              case TypeCode.Int64: val = (ulong)ic.ToInt64(null); break;
              case TypeCode.Object: return null;
              case TypeCode.SByte: val = (ulong)ic.ToSByte(null); break;
              case TypeCode.Single: val = (ulong)ic.ToSingle(null); break;
              case TypeCode.String: return null;
              case TypeCode.UInt16: val = (ulong)ic.ToUInt16(null); break;
              case TypeCode.UInt32: val = (ulong)ic.ToUInt32(null); break;
              case TypeCode.UInt64: val = (ulong)ic.ToUInt64(null); break;
            }
          }
          type = SystemTypes.UInt64;
          break;
        case NodeType.Decrement :
        case NodeType.Increment :
        case NodeType.Ldftn :
        case NodeType.Ldlen :
        case NodeType.Ldtoken :
        case NodeType.Localloc :
        case NodeType.LogicalNot :
          return null;
        case NodeType.Neg :
          switch(code){
            case TypeCode.Boolean: return null;
            case TypeCode.Byte: val = -ic.ToByte(null); type = SystemTypes.Int32; break;
            case TypeCode.Char: val = -ic.ToChar(null); type = SystemTypes.Int32; break;
            case TypeCode.DateTime: return null;
            case TypeCode.DBNull: return null;
            case TypeCode.Decimal: val = -ic.ToDecimal(null); type = SystemTypes.Decimal; break;
            case TypeCode.Double: val = -ic.ToDouble(null); type = SystemTypes.Double; break;
            case TypeCode.Empty: return null;
            case TypeCode.Int16: val = -ic.ToInt16(null); type = SystemTypes.Int16; break;
            case TypeCode.Int32: val = -ic.ToInt32(null); type = SystemTypes.Int32; break;
            case TypeCode.Int64: val = -ic.ToInt64(null); type = SystemTypes.Int64; break;
            case TypeCode.Object: return null;
            case TypeCode.SByte: val = -ic.ToSByte(null); type = SystemTypes.Int8; break;
            case TypeCode.Single: val = -ic.ToSingle(null); type = SystemTypes.Single; break;
            case TypeCode.String: return null;
            case TypeCode.UInt16: val = -ic.ToUInt16(null); type = SystemTypes.Int32; break;
            case TypeCode.UInt32: val = -ic.ToUInt32(null); type = SystemTypes.Int64; break;
            case TypeCode.UInt64: val = -(long)ic.ToUInt64(null); type = SystemTypes.Int64; break;
          }
          break;
        case NodeType.Parentheses:
        case NodeType.UnaryPlus :
          return opnd1;
        case NodeType.DefaultValue :
          return null;
        case NodeType.Not:
          switch (code) {
            case TypeCode.Boolean: return null;
            case TypeCode.Byte: val = ~ic.ToByte(null); type = SystemTypes.Int32; break;
            case TypeCode.Char: val = ~ic.ToChar(null); type = SystemTypes.Int32; break;
            case TypeCode.DateTime: return null;
            case TypeCode.DBNull: return null;
            case TypeCode.Decimal: return null;
            case TypeCode.Double: return null;
            case TypeCode.Empty: return null;
            case TypeCode.Int16: val = ~ic.ToInt16(null); type = SystemTypes.Int32; break;
            case TypeCode.Int32: val = ~ic.ToInt32(null); type = SystemTypes.Int32; break;
            case TypeCode.Int64: val = ~ic.ToInt64(null); type = SystemTypes.Int64; break;
            case TypeCode.Object: return null;
            case TypeCode.SByte: val = ~ic.ToSByte(null); type = SystemTypes.Int32; break;
            case TypeCode.Single: return null;
            case TypeCode.String: return null;
            case TypeCode.UInt16: val = ~ic.ToUInt16(null); type = SystemTypes.Int32; break;
            case TypeCode.UInt32: val = ~ic.ToUInt32(null); type = SystemTypes.UInt32; break;
            case TypeCode.UInt64: val = ~ic.ToUInt64(null); type = SystemTypes.UInt64; break;
          }
          break;
        case NodeType.Refanytype:
          return null;
        case NodeType.Sizeof :
          type = SystemTypes.Int32;
          TypeNode ty = val as TypeNode;
          if (ty == null) return null;
          switch (ty.TypeCode){
            case TypeCode.Boolean: val = 1; break;
            case TypeCode.Byte: val = 1; break;
            case TypeCode.Char: val = 2; break;
            case TypeCode.Double: val = 8; break;
            case TypeCode.Int16: val = 2; break;
            case TypeCode.Int32: val = 4; break;
            case TypeCode.Int64: val = 8; break;
            case TypeCode.SByte: val = 1; break;
            case TypeCode.Single: val = 4; break;
            case TypeCode.UInt16: val = 2; break;
            case TypeCode.UInt32: val = 4; break;
            case TypeCode.UInt64: val = 8; break;
            default: return null;
          }
          break;
        case NodeType.SkipCheck :
        case NodeType.Typeof :
        default:
          return null;
      }
      return new Literal(val, type, unaryExpression.SourceContext);
    }



    public static Literal TryEvalBinaryExpression(Literal opnd1, Literal opnd2, BinaryExpression binaryExpression, TypeSystem typeSystem){
      try{
        return PureEvaluator.EvalBinaryExpression(opnd1, opnd2, binaryExpression, typeSystem);
      }
      catch{
        return null;
      }
    }

    public static Literal TryEvalUnaryExpression(Literal opnd1, UnaryExpression unaryExpression) {
      try {
        return PureEvaluator.EvalUnaryExpression(opnd1, unaryExpression);
      }
      catch {
        return null;
      }
    }

  }
}
