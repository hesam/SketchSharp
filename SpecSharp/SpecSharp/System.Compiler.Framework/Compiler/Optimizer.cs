//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler
{
#endif

  /// <summary>
  /// Runs after Normalizer and Analyzer. Performs rewrites that we don't want the analyzer to see, e.g., removes
  /// calls to conditional methods.
  /// It can also take advantage of invariants learned during the Analyzer phase.
  /// </summary>
  public class Optimizer : StandardVisitor
  {

    public Hashtable currentPreprocessorDefinedSymbols;
    protected TypeNode currentType;

    public Optimizer()
    {}

    public override TypeNode VisitTypeNode(TypeNode typeNode) {
      TypeNode savedCurrentType = this.currentType;
      this.currentType = typeNode;
      TypeNode result = base.VisitTypeNode(typeNode);
      this.currentType = savedCurrentType;
      return result;
    }

    public override CompilationUnit VisitCompilationUnit(CompilationUnit cUnit)
    {
      if (cUnit == null) return null;
      this.currentPreprocessorDefinedSymbols = cUnit.PreprocessorDefinedSymbols;
      return base.VisitCompilationUnit(cUnit);
    }

    public override Statement VisitLocalDeclarationsStatement(LocalDeclarationsStatement localDeclarations) {
      return localDeclarations;
    }

    public override Expression VisitMethodCall(MethodCall call)
    {
      if (call == null) return null;
      call.Callee = this.VisitExpression(call.Callee);
      MemberBinding mb = call.Callee as MemberBinding;
      if (mb != null)
      {
        Method m = mb.BoundMember as Method;
        string conditionalSymbol = m == null ? null : m.ConditionalSymbol;
        if (conditionalSymbol != null)
        {
          if (this.currentPreprocessorDefinedSymbols == null) return null;
          if (this.currentPreprocessorDefinedSymbols[conditionalSymbol] == null) return null;          
        }
      }
      return base.VisitMethodCall(call);
    }
  }
}