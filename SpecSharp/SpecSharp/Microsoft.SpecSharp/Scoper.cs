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
  /// Walks a CompilationUnit and creates a scope for each namespace and each type.
  /// The scopes are attached to the corresponding instances via the ScopeFor hash table.
  /// </summary>
  public sealed class Scoper : Cci.Scoper{
    internal Scoper(TrivialHashtable scopeFor)
      : base(scopeFor){
    }
    public Scoper(Visitor callingVisitor)
      : base(callingVisitor){
    }

    public override Node VisitUnknownNodeType(Node node){
      if (node == null) return null;
      switch (((SpecSharpNodeType)node.NodeType)){
#if Xaml
        case SpecSharpNodeType.XamlSnippet:
          return this.VisitXamlSnippet((XamlSnippet)node);
#endif
        default:
          return base.VisitUnknownNodeType(node);
      }
    }

#if Xaml
    public Node VisitXamlSnippet(XamlSnippet snippet){
      Microsoft.XamlCompiler.Compiler xamlCompiler = new Microsoft.XamlCompiler.Compiler(snippet.XamlDocument, this.currentModule, 
        snippet.ErrorHandler, snippet.ParserFactory, snippet.Options);
      CompilationUnit cu = xamlCompiler.GetCompilationUnit();
      if (cu.Nodes != null && cu.Nodes.Length >= 1 && cu.Nodes[0] is Namespace)
        return this.VisitNamespace((Namespace)cu.Nodes[0]);
      else{
        Debug.Assert(false);
        return null;
      }
    }
#endif
  }
}
