//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  public class ExpressionEvalProperty : IDebugProperty{
    protected CommonExpressionEvaluator evaluator;
    protected String ValueType;
    protected String Value;
    protected Expression ResultExpr;
    protected String Expr;
    public ExpressionEvalProperty(string expr, string type, string value, Expression result, CommonExpressionEvaluator evaluator){
      this.ValueType = type;
      this.Value = value;
      this.evaluator = evaluator;
      this.ResultExpr = result;
      this.Expr = expr;
    }
    public virtual string Name{
      get{ return this.Expr; }
    }
    public virtual string FullName{
      get{
        return this.Expr;
      }
    }
    public virtual string Type{
      get{ return this.ValueType; }
    }
    public virtual string GetValue(uint radix, uint timeout){
      if ( this.ValueType == "System.Boolean")
        return this.Value.ToLower();
      return this.Value;
    }
    public virtual void SetValue(string expr, uint radix, uint timeout){
      throw new NotImplementedException();
    }
    public virtual DebugPropertyAttributes Attributes{
      get{ 
        DebugPropertyAttributes attrib = DebugPropertyAttributes.None;
        if ( this.ValueType == "System.Boolean"){
          attrib |= DebugPropertyAttributes.Boolean;
          if (this.Value.ToLower() == "true")
            attrib |= DebugPropertyAttributes.BooleanTrue;
        }
        return attrib;  
      }
    }
    public virtual IDebugProperty Parent{
      get{ return null; }
    }
    public virtual IEnumDebugProperty EnumChildren(EnumerationKind kind, int radix, int timeout, bool allowFuncEval){
      
      return null;
    }
  }
  public class LiteralList : ArrayList{
    public new Literal this[int i]{
      get{
        return base[i] as Literal;
      }
      set{
        this.Add(value);
      }
    }
  }
  public class StackFrame{
    public Literal thisObject;
    public LiteralList locals;
    public LiteralList parameters;

    public StackFrame(){
      this.locals = new LiteralList();
      this.parameters = new LiteralList();
    }
  }
  public class ParsedExpression : IDebugExpression {
    public Expression ParsedExpr;
    public CommonExpressionEvaluator EE;
    public String Expr;
    public Expression compiledExpression;
    public ParsedExpression(String expr, Expression parsedExpression, CommonExpressionEvaluator ee){
      this.ParsedExpr = parsedExpression;
      this.EE = ee;
      this.Expr = expr;
      this.compiledExpression = null;
    }
    public IDebugProperty Evaluate() {
      return null;
    }
    public void SetValue(string expression){
    }
  }
}