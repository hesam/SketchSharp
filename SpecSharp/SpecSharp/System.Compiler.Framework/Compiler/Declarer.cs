//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  /// <summary>
  /// Walks the statement list of a Block gathering information from declarations for use by forward references. Does not recurse into nested blocks.
  /// This visitor is instantiated and called by Looker.
  /// </summary>
  public class Declarer : StandardCheckingVisitor{
    /// <summary>Maps identifiers to Metadata nodes (e.g. varDecl.Name -> field).</summary>
    public Class scope;
    /// <summary>Maps labels (Identifiers) to the corresponding labeled blocks (single statements are promoted to blocks for this purpose).
    /// Needed to keep local variable and labels in separate namespaces.</summary>
    public TrivialHashtable targetFor;
    /// <summary>A subset of targetFor that maps only labels (Identifiers) declared in the current block. Needed to check for duplicates.</summary>
    public TrivialHashtable localLabels;
    /// <summary>A list of all the labels encountered by Declarer.</summary>
    public IdentifierList labelList;

    public Declarer(ErrorHandler errorHandler)
      :base(errorHandler){
    }
    public Declarer(Visitor callingVisitor)
      : base(callingVisitor){
    }
    public override void TransferStateTo(Visitor targetVisitor){
      base.TransferStateTo(targetVisitor);
      Declarer target = targetVisitor as Declarer;
      if (target == null) return;
      target.scope = this.scope;
      target.targetFor = this.targetFor;
      target.localLabels = this.localLabels;
      target.labelList = this.labelList;
    }

    public override Block VisitBlock(Block block){
      return block; //Do not recurse into nested blocks
    }
    /// <summary>
    /// Walks the statement list of the given block gathering information from declarations for use by forward references. Does not recurse into nested blocks.
    /// </summary>
    /// <param name="block">The block whose declarations are to be processed</param>
    /// <param name="scope">Maps identifiers to Metadata nodes (e.g. Fields).</param>
    /// <param name="targetFor">Maps labels (Identifiers) to the corresponding labeled blocks (single statements are promoted to blocks for this purpose).</param>
    /// <param name="labelList">A list of all the labels encountered by Declarer.</param>
    public virtual void VisitBlock(Block block, Class scope, TrivialHashtable targetFor, IdentifierList labelList){
      if (block == null) return;
      this.scope = scope;
      this.targetFor = targetFor;
      this.labelList = labelList;
      StatementList statements = block.Statements;
      if (statements == null) return;
      for (int i = 0, n = statements.Count; i < n; i++)
        statements[i] = (Statement)this.Visit(statements[i]);
    }
    public virtual void VisitField(Field field, Class scope){
      if (field == null || scope == null) return;
      this.scope = scope;
      field.Initializer = this.VisitExpression(field.Initializer);
    }
    public override Statement VisitFunctionDeclaration(FunctionDeclaration fdecl){
      if (fdecl == null) return null;
      Method method = fdecl.Method = new Method();
      method.Name = fdecl.Name;
      this.scope.Members.Add(method);
      return fdecl;
    }
    public override Statement VisitLabeledStatement(LabeledStatement lStatement){
      if (lStatement == null) return null;
      this.labelList.Add(lStatement.Label);
      lStatement.Scope = this.scope as BlockScope;
      if (this.localLabels == null) this.localLabels = new TrivialHashtable();
      if (this.localLabels[lStatement.Label.UniqueIdKey] == null){
        this.localLabels[lStatement.Label.UniqueIdKey] = lStatement;
        this.targetFor[lStatement.Label.UniqueIdKey] = lStatement;
      }else
        this.HandleError(lStatement.Label, Error.LabelIdentiferAlreadyInUse, lStatement.Label.ToString());
      lStatement.Statement = (Statement)this.Visit(lStatement.Statement);
      return lStatement;
    }
    public override Statement VisitLocalDeclarationsStatement(LocalDeclarationsStatement localDeclarations){
      if (localDeclarations == null) return null;
      FieldFlags flags = FieldFlags.CompilerControlled|FieldFlags.NotSerialized; //NotSerialized "poisons" the field until Checker clears it
      if (localDeclarations.Constant) flags |= FieldFlags.Literal;
      else if (localDeclarations.InitOnly) flags = FieldFlags.Public|FieldFlags.InitOnly;
      TypeNode type = localDeclarations.Type;
      LocalDeclarationList decls = localDeclarations.Declarations;
      for (int i = 0, n = decls.Count; i < n; i++){
        LocalDeclaration decl = decls[i];
        Field f = new Field(this.scope, null, flags, decl.Name, type, null);
        f.Initializer = this.VisitExpression(decl.InitialValue);
        this.scope.Members.Add(f);
        decl.Field = f;
      }
      return localDeclarations;
    }
    public override Statement VisitResourceUse(ResourceUse resourceUse){
      return resourceUse;
    }
    public virtual Statement VisitAcquire(Acquire @acquire, Class scope){
      if (@acquire == null) return null;
      Class savedScope = this.scope;
      this.scope = scope;
      LocalDeclarationsStatement locDecs = @acquire.Target as LocalDeclarationsStatement;
      if (locDecs != null){
        locDecs.InitOnly = true;
        @acquire.Target = this.VisitLocalDeclarationsStatement(locDecs);
      }
      @acquire.Body = this.VisitBlock(@acquire.Body);
      this.scope = savedScope;
      return @acquire;
    }
    public override Statement VisitFixed(Fixed Fixed) {
      return Fixed;
    }
    public virtual Statement VisitFixed(Fixed Fixed, Class scope) {
      if (Fixed == null) return null;
      Class savedScope = this.scope;
      this.scope = scope;
      LocalDeclarationsStatement locDecs = Fixed.Declarators as LocalDeclarationsStatement;
      if (locDecs != null) {
        locDecs.InitOnly = true;
        Fixed.Declarators = this.VisitLocalDeclarationsStatement(locDecs);
      }
      Fixed.Body = this.VisitBlock(Fixed.Body);
      this.scope = savedScope;
      return Fixed;
    }
    public virtual Statement VisitResourceUse(ResourceUse resourceUse, Class scope) {
      if (resourceUse == null) return null;
      Class savedScope = this.scope;
      this.scope = scope;
      LocalDeclarationsStatement locDecs = resourceUse.ResourceAcquisition as LocalDeclarationsStatement;
      if (locDecs != null){
        locDecs.InitOnly = true;
        resourceUse.ResourceAcquisition = this.VisitLocalDeclarationsStatement(locDecs);
      }
      resourceUse.Body = this.VisitBlock(resourceUse.Body);
      this.scope = savedScope;
      return resourceUse;
    }
    public virtual SwitchCase VisitSwitchCase(SwitchCase switchCase, Class scope, 
      TrivialHashtable targetFor, IdentifierList labelList){
      if (switchCase == null) return null;
      this.VisitBlock(switchCase.Body, scope, targetFor, labelList);
      return switchCase;
    }
    public override Statement VisitVariableDeclaration(VariableDeclaration variableDeclaration){
      if (variableDeclaration == null) return null;
      Field f = new Field(this.scope, null, FieldFlags.CompilerControlled, variableDeclaration.Name, null, null);
      this.scope.Members.Add(f);
      variableDeclaration.Field = f;
      return variableDeclaration;
    }
  }
}
