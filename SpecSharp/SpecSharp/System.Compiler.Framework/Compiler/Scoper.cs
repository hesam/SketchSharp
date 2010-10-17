//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections;
using System.Diagnostics;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  /// <summary>
  /// Walks a CompilationUnit and creates a scope for each namespace and each type.
  /// The scopes are attached to the corresponding instances via the ScopeFor hash table.
  /// </summary>
  public class Scoper : StandardVisitor{
    public TrivialHashtable ScopeFor;
    public Scope currentScope;
    public Module currentModule;

    public Scoper(TrivialHashtable scopeFor){
      this.ScopeFor = scopeFor;
    }
    public Scoper(Visitor callingVisitor)
      : base(callingVisitor){
    }
    public override void TransferStateTo(Visitor targetVisitor){
      base.TransferStateTo(targetVisitor);
      Scoper target = targetVisitor as Scoper;
      if (target == null) return;
      target.ScopeFor = this.ScopeFor;
      target.currentScope = this.currentScope;
      target.currentModule = this.currentModule;
    }

    public override Compilation VisitCompilation(Compilation compilation){
      if (compilation == null) return null;
      this.currentModule = compilation.TargetModule;
      this.currentScope = compilation.GlobalScope;
      return base.VisitCompilation(compilation);
    }

    public override Namespace VisitNamespace(Namespace nspace){
      if (nspace == null) return null;
      Scope savedCurrentScope = this.currentScope;
      Scope outerScope = savedCurrentScope;
      //Insert dummy outer namespace scopes for any prefixes. Ie. if this namespace has Name == foo.bar, insert an outer
      //namespace scope for the "foo" prefix.
      string id = nspace.Name.ToString();
      int firstDot = id.IndexOf('.');
      while (firstDot > 0){
        outerScope = new NamespaceScope(outerScope, new Namespace(Identifier.For(id.Substring(0, firstDot))), this.currentModule);
        firstDot = id.IndexOf('.', firstDot+1);
      }
      this.ScopeFor[nspace.UniqueKey] = this.currentScope = new NamespaceScope(outerScope, nspace, this.currentModule);
      nspace = base.VisitNamespace(nspace);
      this.currentScope = savedCurrentScope;
      return nspace;
    }
    public override TypeNode VisitTypeNode(TypeNode typeNode){
      if (typeNode == null) return null;
      Scope savedCurrentScope = this.currentScope;
      this.ScopeFor[typeNode.UniqueKey] = this.currentScope = new TypeScope(savedCurrentScope, typeNode);
      Debug.Assert((this.ScopeFor[typeNode.UniqueKey] as Scope) == this.currentScope);
      MemberList members = typeNode.Members;
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
        TypeNode nestedType = members[i] as TypeNode;
        if (nestedType == null) continue;
        members[i] = this.VisitTypeNode(nestedType);
      }
      this.currentScope = savedCurrentScope;
      return typeNode;
    }
  }
}
