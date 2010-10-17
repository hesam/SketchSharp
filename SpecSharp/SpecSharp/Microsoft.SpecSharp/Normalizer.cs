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
using System.Diagnostics;
using System.Collections;

namespace Microsoft.SpecSharp{
  /// <summary>
  /// Walks an IR, producing an equivalent IR that can be serialized to IL+MD by Writer
  /// </summary> 
  public sealed class Normalizer : Cci.Normalizer{
    internal Normalizer(TypeSystem typeSystem)
      : base(typeSystem){
    }
    public Normalizer(Visitor callingVisitor)
      : base(callingVisitor){
    }

    protected override bool CodeMightBeVerified {
      get {
        if (this.currentCompilation != null) {
          SpecSharpCompilerOptions sopts = this.currentCompilation.CompilerParameters as SpecSharpCompilerOptions;
          if (sopts != null && !sopts.Compatibility) return true;
        }
        return false;
      }
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
