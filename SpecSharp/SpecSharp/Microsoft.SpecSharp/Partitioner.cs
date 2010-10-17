//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
#if CCINamespace
using Microsoft.Cci;
using CciPartitioner = Microsoft.Cci.Partitioner;
#else
using System.Compiler;
using CciPartitioner = System.Compiler.Partitioner;
#endif
using System;

namespace Microsoft.SpecSharp {
  public class Partitioner: CciPartitioner {
    public Partitioner() {
    }
    public Partitioner(Visitor callingVisitor)
      : base(callingVisitor) {
    }

    public override Node VisitUnknownNodeType(Node node) {
      if (node == null) return null;
      switch (((SpecSharpNodeType)node.NodeType)) {
        default:
          return base.VisitUnknownNodeType(node);      
      }
    }    
  }
}
