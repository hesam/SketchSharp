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
	public class MethodBodySpecializer : Cci.MethodBodySpecializer{
		public MethodBodySpecializer(Visitor callingVisitor)
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
