//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
#if CCINamespace
using Microsoft.Cci;
#else
using System.Compiler;
#endif
using System;
using System.Diagnostics;

namespace Microsoft.SpecSharp{
  /// <summary>
  /// Provides a way for third parties to walk over an already checked IR, enforcing additional constraints
  /// </summary>
  public abstract class CustomVisitor : StandardCheckingVisitor{
    public CustomVisitor(Visitor callingVisitor)
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