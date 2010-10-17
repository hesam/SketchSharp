//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
#if CCINamespace
using Microsoft.Cci;
using Cci = Microsoft.Cci;
#else
using System.Compiler;
using Cci = System.Compiler;
#endif
using System;

namespace Microsoft.SpecSharp{
  /// <summary>
  /// Walks the statement list of a Block, entering any declarations into the associated scope. Does not recurse.
  /// This visitor is instantiated and called by Looker.
  /// </summary>
  public sealed class Declarer : Cci.Declarer{
    internal Declarer(Cci.ErrorHandler errorHandler)
      : base(errorHandler){
    }
    public Declarer(Visitor callingVisitor)
      : base(callingVisitor){
    }

    public override Node VisitUnknownNodeType(Node node){
      if (node == null) return null;
      switch (((SpecSharpNodeType)node.NodeType)){
        default:
          return base.VisitUnknownNodeType(node);
      }
    }
  }
}




