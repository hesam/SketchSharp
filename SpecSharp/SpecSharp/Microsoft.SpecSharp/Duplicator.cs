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
  public class Duplicator : Cci.Duplicator{
    /// <param name="module">The module into which the duplicate IR will be grafted.</param>
    /// <param name="type">The type into which the duplicate Member will be grafted. Ignored if entire type, or larger unit is duplicated.</param>
    public Duplicator(Module module, TypeNode type)
      : base(module, type){
    }
    public Duplicator(Visitor callingVisitor)
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
