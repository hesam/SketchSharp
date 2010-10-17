//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  /// <summary>
  /// A passer is used to pass through nodes without acting on them.  All callback will
  /// be called for each child node of the passed-through node.
  /// </summary>
  public sealed class Passer: Visitor{
    StandardPasser stdPasser;
        
    public Passer(Visitor callback) {
      this.stdPasser = new StandardPasser(callback);
    }        
    public override Node Visit(Node node) {
      return this.stdPasser.Pass(node);
    }    
    public override ExpressionList VisitExpressionList(ExpressionList list) {
      return this.stdPasser.VisitExpressionList(list);
    }
    class StandardPasser: StandardVisitor {
      Visitor callback;
      internal StandardPasser(Visitor callback) {
        this.callback = callback;
      }      
      internal Node Pass(Node node) {
        return base.Visit(node);
      }      
      public override Node Visit(Node node) {
        return this.callback.Visit(node);
      }      
      public override Visitor GetVisitorFor(Node node) {
        return (Visitor) node.GetVisitorFor(this.callback, "Passer");
      }
    }
  }
}
