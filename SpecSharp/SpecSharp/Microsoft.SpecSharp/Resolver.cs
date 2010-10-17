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
using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace Microsoft.SpecSharp {
  /// <summary>
  /// Walks an IR, mutuating it by resolving overloads and inferring expression result types
  /// </summary>
  public sealed class Resolver : Cci.Resolver{
    private bool inCompatibilityMode = false;
        
    public Resolver(ErrorHandler errorHandler, TypeSystem typeSystem)
      : base(errorHandler, typeSystem){
    }
    public Resolver(Visitor callingVisitor)
      : base(callingVisitor){
    }
    public override void TransferStateTo(Visitor targetVisitor){
      base.TransferStateTo(targetVisitor);
      Resolver target = targetVisitor as Resolver;
      if (target == null) return;
      target.inCompatibilityMode = this.inCompatibilityMode;
    }

    public override Node VisitUnknownNodeType(Node node){
      if (node == null) return null;
      switch (((SpecSharpNodeType)node.NodeType)){
        case SpecSharpNodeType.KeywordList:
          return null;
        default:
          return base.VisitUnknownNodeType(node);
      }
    }

    public override CompilationUnit VisitCompilationUnit(CompilationUnit cUnit){
      if (cUnit == null) return null;
      if (cUnit.Compilation != null){
        SpecSharpCompilerOptions coptions = cUnit.Compilation.CompilerParameters as SpecSharpCompilerOptions;
        if (coptions != null) this.inCompatibilityMode = coptions.Compatibility;
      }
      return base.VisitCompilationUnit(cUnit);
    }
    public override Statement VisitLocalDeclarationsStatement(LocalDeclarationsStatement localDeclarations) {
      if (localDeclarations == null) return null;
      Statement result = base.VisitLocalDeclarationsStatement(localDeclarations);
      if (localDeclarations.Type == null && localDeclarations.TypeExpression is TypeExpression) {
        Identifier id = ((TypeExpression)localDeclarations.TypeExpression).Expression as Identifier;
        if (id != null && id.UniqueIdKey == StandardIds.Var.UniqueIdKey) {
          //Use the inferred type of the initializer of the first declaration as the variable type
          TypeNode t = null;
          LocalDeclarationList decls = localDeclarations.Declarations;
          for (int i = 0, n = decls == null ? 0 : decls.Count; i < n; i++) {
            LocalDeclaration decl = decls[i];
            if (decl == null || decl.Field == null) continue;
            if (t == null) {
              if (decl.InitialValue != null)
                t = decl.InitialValue.Type;
            }
            decl.Field.Type = t;
            localDeclarations.Type = t;
          }
        }
      }
      return result;
    }
    public override Expression VisitQualifiedIdentifierCore(QualifiedIdentifier qualId) {
      Identifier id = qualId.Identifier;
      Expression target = base.VisitQualifiedIdentifierCore(qualId);
      if (target == null && !this.inCompatibilityMode && this.insideAssertion)
        target = Looker.BindPseudoMember(qualId.Qualifier, qualId.Identifier);
      return target;
    }
    public override void DetermineIfNonNullCheckingIsDesired(CompilationUnit cUnit){
      SpecSharpCompilerOptions soptions = this.currentOptions as SpecSharpCompilerOptions;
      if (soptions != null && soptions.Compatibility) return;
      this.NonNullChecking = !(this.currentPreprocessorDefinedSymbols != null && this.currentPreprocessorDefinedSymbols.ContainsKey("NONONNULLTYPECHECK"));
    }
    private void HandleError(Node offendingNode, Error error, params string[] messageParameters){
      if (this.ErrorHandler == null) return;
      ((ErrorHandler)this.ErrorHandler).HandleError(offendingNode, error, messageParameters);
    }
    public override void InferReturnTypeForExpressionFunction(Method meth){
      meth.ReturnType = SystemTypes.Void;
      StatementList statements = meth.Body == null ? null : meth.Body.Statements;
      ExpressionStatement es = statements == null || statements.Count != 1 ? null : statements[0] as ExpressionStatement;
      Comprehension q = es == null ? null : es.Expression as Comprehension;
      if (q != null && q.Type != null && TypeNode.StripModifiers(q.Type).Template == SystemTypes.GenericIEnumerable)
        meth.ReturnType = q.Type;
    }
    public override TypeNode InferTypeOfBinaryExpression(TypeNode t1, TypeNode t2, BinaryExpression binaryExpression){
      if (binaryExpression == null) return SystemTypes.Object;
      bool eligible = false;
      switch (binaryExpression.NodeType){
        case NodeType.Add:
        case NodeType.And:
        case NodeType.Ceq:
        case NodeType.Cgt:
        case NodeType.Cgt_Un:
        case NodeType.Clt:
        case NodeType.Clt_Un:
        case NodeType.Div:
        case NodeType.Ge:
        case NodeType.Gt:
        case NodeType.Mul:
        case NodeType.Le:
        case NodeType.Lt:
        case NodeType.Or:
        case NodeType.Rem:
        case NodeType.Sub:
        case NodeType.Xor:
          eligible = true;
          break;
      }
      bool opnd1IsNullLiteral = Literal.IsNullLiteral(binaryExpression.Operand1);
      bool opnd2IsNullLiteral = Literal.IsNullLiteral(binaryExpression.Operand2);
      if (eligible && opnd1IsNullLiteral && !opnd2IsNullLiteral) {
        TypeNode t = base.InferTypeOfBinaryExpression(t2, t2, binaryExpression);
        if (t != null && t.IsValueType && !this.typeSystem.IsNullableType(t))
          return SystemTypes.GenericNullable.GetTemplateInstance(this.currentType, t);
        return t;
      }else if (eligible && !opnd1IsNullLiteral && opnd2IsNullLiteral){
        TypeNode t = base.InferTypeOfBinaryExpression(t1, t1, binaryExpression);
        if (t != null && t.IsValueType && !this.typeSystem.IsNullableType(t))
          return SystemTypes.GenericNullable.GetTemplateInstance(this.currentType, t);
        return t;
      }else{
        return base.InferTypeOfBinaryExpression(t1, t2, binaryExpression);
      }
    }
  }  
  public class ResolvedReferenceVisitor : Cci.ResolvedReferenceVisitor{
    public ResolvedReferenceVisitor(ResolvedReferenceVisitMethod visitResolvedReference)
      : base(visitResolvedReference){
    }
  }
}
