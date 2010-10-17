#if CCINamespace
using Microsoft.Cci;
#else
using System.Compiler;
#endif
using System;
using System.Diagnostics;

namespace Microsoft.SpecSharp {
  public class Passer: Visitor {
    Visitor callback;
    public Passer(Visitor callback) {            
      this.callback = callback;
    }
    public override Node Visit(Node node){
      if (node == null) return null;
      switch (((SpecSharpNodeType)node.NodeType)){
        default:
          Debug.Assert(false, "Unhandled node type in Microsoft.SpecSharp.Passer.Visit()");
          return null;
      }
    }    
    public override ExpressionList VisitExpressionList(ExpressionList list) {
      if (list == null) return null;
      for( int i = 0, n = list.Length; i < n; i++ ) {
        list[i] = (Expression)this.callback.Visit(list[i]);
      }
      return list;
    }
  }
}