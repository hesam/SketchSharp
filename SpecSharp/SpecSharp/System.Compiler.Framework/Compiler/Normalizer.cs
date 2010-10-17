//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Diagnostics;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif

  /// <summary>
  /// Walks an IR, mutating it into a form that can be serialized to IL+MD by Writer
  /// </summary>
  public class Normalizer : StandardVisitor{
    public StatementList exitTargets;
    public StatementList continueTargets;
    public Block currentExceptionBlock;
    public Stack currentTryStatements;
    public TrivialHashtable exceptionBlockFor;
    public TrivialHashtable visitedCompleteTypes;
    public Method currentMethod;
    public Module currentModule;
    public This currentThisParameter;
    public Local currentReturnLocal;
    public Block currentReturnLabel;
    public Local currentClosureLocal;
    public Compilation currentCompilation;
    public Block currentContractPrelude;
    public BlockList currentContractExceptionalTerminationChecks;
    public Local currentExceptionalTerminationException;
    public Block currentContractNormalTerminationCheck;
    public Field currentIteratorValue;
    public Field currentIteratorEntryPoint;
    public Hashtable currentPreprocessorDefinedSymbols;
    public MethodCall currentBaseCtorCall;
    public BlockList iteratorEntryPoints;
    public TrivialHashtable EndIfLabel;
    public int foreachLength;
    public TypeNode currentType;
    public TypeSystem typeSystem;
    public bool foldQuery;
    public QueryTransact currentTransaction;
    public bool WrapToBlockExpression;  // HACK: Need this for compiling expressions, So I can run evaluator over them
    public bool useGenerics;
    public override Expression VisitArglistExpression(ArglistExpression argexp) {
      if (argexp == null) return null;
      return new Expression(NodeType.Arglist, argexp.Type);
    }
    public override Expression VisitRefTypeExpression(RefTypeExpression reftypexp) {
      if (reftypexp == null) return null;
      Expression result = base.VisitRefTypeExpression (reftypexp);
      if (result != reftypexp) return result;
      UnaryExpression refanytype = new UnaryExpression(reftypexp.Operand, NodeType.Refanytype, SystemTypes.RuntimeTypeHandle, reftypexp.SourceContext);
      ExpressionList arguments = new ExpressionList(1);
      arguments.Add(refanytype);
      MemberBinding mb = new MemberBinding(null, Runtime.GetTypeFromHandle);
      return new MethodCall(mb, arguments, NodeType.Call, SystemTypes.Type);
    }
    public override Expression VisitRefValueExpression(RefValueExpression refvalexp) {
      if (refvalexp == null) return null;
      Expression result = base.VisitRefValueExpression (refvalexp);
      if (result != refvalexp) return result;
      return new BinaryExpression(refvalexp.Operand1, refvalexp.Operand2, NodeType.Refanyval, refvalexp.Type, refvalexp.SourceContext);
    }

    public Normalizer(TypeSystem typeSystem){
      this.typeSystem = typeSystem;
      this.exitTargets = new StatementList();
      this.continueTargets = new StatementList();
      this.currentTryStatements = new Stack();
      this.exceptionBlockFor = new TrivialHashtable();
      this.visitedCompleteTypes = new TrivialHashtable();
      this.EndIfLabel = new TrivialHashtable();
      this.foreachLength = 7;
      this.WrapToBlockExpression = true;
      this.useGenerics = TargetPlatform.UseGenerics;
    }
    public Normalizer(Visitor callingVisitor)
      : base(callingVisitor){
    }
    public override void TransferStateTo(Visitor targetVisitor){
      base.TransferStateTo(targetVisitor);
      Normalizer target = targetVisitor as Normalizer;
      if (target == null) return;
      target.exitTargets = this.exitTargets;
      target.continueTargets = this.continueTargets;
      target.currentTryStatements = this.currentTryStatements;
      target.currentExceptionBlock = this.currentExceptionBlock;
      target.exceptionBlockFor = this.exceptionBlockFor;
      target.currentMethod = this.currentMethod;
      target.currentModule = this.currentModule;
      target.currentReturnLocal = this.currentReturnLocal;
      target.currentReturnLabel = this.currentReturnLabel;
      target.currentClosureLocal = this.currentClosureLocal;
      target.currentCompilation = this.currentCompilation;
      target.currentContractPrelude = this.currentContractPrelude;
      target.currentContractExceptionalTerminationChecks = this.currentContractExceptionalTerminationChecks;
      target.currentExceptionalTerminationException = this.currentExceptionalTerminationException;
      target.currentContractNormalTerminationCheck = this.currentContractNormalTerminationCheck;
      target.currentIteratorValue = this.currentIteratorValue;
      target.currentIteratorEntryPoint = this.currentIteratorEntryPoint;
      target.currentThisParameter = this.currentThisParameter;
      target.iteratorEntryPoints = this.iteratorEntryPoints;
      target.EndIfLabel = this.EndIfLabel;
      target.foreachLength = this.foreachLength;
      target.currentType = this.currentType;
      target.typeSystem = this.typeSystem;
      target.foldQuery = this.foldQuery;
      target.currentTransaction = this.currentTransaction;
      target.WrapToBlockExpression = this.WrapToBlockExpression;
      target.visitedCompleteTypes = this.visitedCompleteTypes;
      target.useGenerics = this.useGenerics;
    }

    public override Expression VisitAddressDereference(AddressDereference addr){
      if (addr == null) return null;
      Expression expr = addr.Address = this.VisitExpression(addr.Address);
      // At this point, if addr.Address is a This, and there is a closure,
      // then addr.Address must be a closure reference. If the expr is a
      // ValueType, then because the corresponding field in the 
      if (expr != null && expr.Type != null && expr.Type.IsValueType) {
        MemberBinding mb = expr as MemberBinding;
        if (mb != null) {
          if (mb.TargetObject != null && mb.TargetObject.Type is ClosureClass) {
            expr = addr.Address = new UnaryExpression(addr.Address, NodeType.AddressOf);
          }
        }
      }
      if (expr == null) return null;
      TypeNode exprType = expr.Type;
      if (exprType == null || exprType.Template != SystemTypes.GenericBoxed) return addr;
      Method getValue = this.GetTypeView(exprType).GetMethod(StandardIds.GetValue);
      return new MethodCall(new MemberBinding(new UnaryExpression(expr, NodeType.AddressOf, expr.Type.GetReferenceType()), getValue), null);
    }
    public virtual Statement VisitAndInvertBranchCondition(Expression condition, Block target, SourceContext sctx){
      if (condition == null) return null;
      if (condition.NodeType == NodeType.Parentheses)
        return this.VisitAndInvertBranchCondition(((UnaryExpression)condition).Operand, target, sctx);
      bool unordered = false;
      BinaryExpression bexpr = condition as BinaryExpression;
      if (bexpr != null){
        if (bexpr.Operand1 == null || bexpr.Operand2 == null) return null;
        if (bexpr.NodeType == NodeType.Is){
          bexpr.NodeType = NodeType.Isinst;
          if (this.useGenerics && bexpr.Operand1.Type != null && bexpr.Operand1.Type.IsTemplateParameter)
            bexpr.Operand1 = new BinaryExpression(bexpr.Operand1, new Literal(bexpr.Operand1.Type), NodeType.Box);
          condition = new UnaryExpression(this.VisitExpression(bexpr), NodeType.LogicalNot);
          goto returnBranch;
        }
        NodeType invertedOperator = this.InvertComparisonOperator(bexpr.NodeType);
        if (invertedOperator != bexpr.NodeType){
          TypeNode operand1Type = bexpr.Operand1.Type;
          unordered = operand1Type != null && (operand1Type.IsUnsignedPrimitiveNumeric || operand1Type == SystemTypes.Double || operand1Type == SystemTypes.Single);
          bexpr.Operand1 = this.VisitExpression(bexpr.Operand1);
          bexpr.Operand2 = this.VisitExpression(bexpr.Operand2);
          bexpr.NodeType = invertedOperator;
          condition = bexpr;
          goto returnBranch;
        }
        if (bexpr.NodeType == NodeType.LogicalAnd){
          StatementList statements = new StatementList(2);
          statements.Add(this.VisitAndInvertBranchCondition(bexpr.Operand1, target, sctx));
          statements.Add(this.VisitAndInvertBranchCondition(bexpr.Operand2, target, new SourceContext()));
          return new Block(statements);
        }
        if (bexpr.NodeType == NodeType.LogicalOr){
          Block label = new Block();
          StatementList statements = new StatementList(4);
          statements.Add(this.VisitBranchCondition(bexpr.Operand1, label, sctx));
          statements.Add(this.VisitBranchCondition(bexpr.Operand2, label, new SourceContext()));
          statements.Add(new Branch(null, target));
          statements.Add(label);
          return new Block(statements);
        }
      }
      UnaryExpression uexpr = condition as UnaryExpression;
      if (uexpr != null && uexpr.NodeType == NodeType.LogicalNot)
        return this.VisitBranchCondition(uexpr.Operand, target, sctx);
      else
        condition = new UnaryExpression(this.VisitExpression(condition), NodeType.LogicalNot, condition.SourceContext);
    returnBranch:
      return new Branch(condition, target, sctx, unordered);
    }
    public virtual Statement VisitBranchCondition(Expression condition, Block target, SourceContext sctx){
      if (condition == null) return null;
      if (condition.NodeType == NodeType.Parentheses)
        return this.VisitBranchCondition(((UnaryExpression)condition).Operand, target, sctx);
      BinaryExpression bexpr = condition as BinaryExpression;
      if (bexpr != null){
        if (bexpr.Operand1 == null || bexpr.Operand1.Type == null || bexpr.Operand2 == null) return null;
        if (bexpr.NodeType == NodeType.Is){
          if (this.useGenerics && bexpr.Operand1.Type.IsTemplateParameter)
            bexpr.Operand1 = new BinaryExpression(bexpr.Operand1, new Literal(bexpr.Operand1.Type), NodeType.Box);
          bexpr.NodeType = NodeType.Isinst;
          if (bexpr.Operand2 is Literal)
            bexpr.Type = (TypeNode) ((Literal) bexpr.Operand2).Value;
          else
            bexpr.Type = (TypeNode)((MemberBinding)bexpr.Operand2).BoundMember;
          goto returnBranch;
        }
        if (bexpr.NodeType == NodeType.LogicalAnd){
          Block label = new Block();
          StatementList statements = new StatementList(4);
          statements.Add(this.VisitAndInvertBranchCondition(bexpr.Operand1, label, sctx));
          statements.Add(this.VisitAndInvertBranchCondition(bexpr.Operand2, label, new SourceContext()));
          statements.Add(new Branch(null, target));
          statements.Add(label);
          return new Block(statements);
        }
        if (bexpr.NodeType == NodeType.LogicalOr){
          Block label = new Block();
          StatementList statements = new StatementList(2);
          statements.Add(this.VisitBranchCondition(bexpr.Operand1, target, sctx));
          statements.Add(this.VisitBranchCondition(bexpr.Operand2, target, new SourceContext()));
          return new Block(statements);
        }
      }
      returnBranch:
        return new Branch(this.VisitBranchCondition(condition), target, sctx);
    }
    public override AssemblyNode VisitAssembly(AssemblyNode assembly){
      this.currentModule = assembly;
      assembly = base.VisitAssembly(assembly);
      if (assembly != null) assembly.IsNormalized = true;
      return assembly;
    }
    public override Statement VisitAssertion(Assertion assertion){
      if (assertion == null || assertion.Condition == null) return null;
      StatementList stmts = new StatementList();
      if (!(this.currentCompilation != null && this.currentCompilation.CompilerParameters is CompilerOptions && ((CompilerOptions)this.currentCompilation.CompilerParameters).DisableInternalContractsMetadata)) {
        foreach (Statement s in this.SerializeAssertion(this.currentModule, assertion.Condition, null, assertion.SourceContext, "AssertStatement")) {
          stmts.Add(s);
        }
      }
      if (!(this.currentCompilation != null && this.currentCompilation.CompilerParameters is CompilerOptions && ((CompilerOptions)this.currentCompilation.CompilerParameters).DisableInternalChecks))
        stmts.Add(this.MarkAsInstrumentationCodeNormalized(this.currentMethod, this.CreateAssertionCheckingCode(assertion.Condition, "Assert")));
      return new Block(stmts);
    }
    public virtual Block CreateAssertionCheckingCode(Expression condition, string methodName){
      Method Assertmethod = this.GetTypeView(SystemTypes.AssertHelpers).GetMethod(Identifier.For(methodName), SystemTypes.Boolean);
      SourceContext ctx = condition.SourceContext;
      // old code: just return mcStmt, but with the argument being the normalized form of assertion.Condition
      //        Expression argument = this.VisitExpression(assertion.Condition);
      Expression argument = Literal.False;
      MethodCall mc = new MethodCall(new MemberBinding(null, Assertmethod), new ExpressionList(argument));
      mc.Type = SystemTypes.Void;
      mc.SourceContext = ctx;
      Statement mcStmt = new ExpressionStatement(mc, ctx);
      StatementList stmts = new StatementList(3);
      Block nopBlock = new Block(new StatementList(new Statement(NodeType.Nop)));
      Statement branch = this.VisitBranchCondition(condition, nopBlock, ctx);
      stmts.Add(branch);
      stmts.Add(mcStmt);
      stmts.Add(nopBlock);
      Literal lit = condition as Literal;
      if (lit != null){
        if (object.Equals(lit.Value, false))
          stmts.Add(new Throw(new MemberBinding(null, SystemTypes.PreAllocatedExceptions.GetField(Identifier.For("Unreachable")))));
      }
      return new Block(stmts);
    }
    public override Statement VisitAssumption(Assumption assumption){
      if (assumption == null || assumption.Condition == null) return null;
      StatementList stmts = new StatementList();
      if (!(this.currentCompilation != null && this.currentCompilation.CompilerParameters is CompilerOptions && ((CompilerOptions)this.currentCompilation.CompilerParameters).DisableInternalContractsMetadata)) {
        foreach (Statement s in this.SerializeAssertion(this.currentModule, assumption.Condition, null, assumption.SourceContext, "AssumeStatement")) {
          stmts.Add(s);
        }
      }
      if (!(this.currentCompilation != null && this.currentCompilation.CompilerParameters is CompilerOptions && ((CompilerOptions)this.currentCompilation.CompilerParameters).DisableAssumeChecks))
        stmts.Add(this.MarkAsInstrumentationCodeNormalized(this.currentMethod, this.CreateAssertionCheckingCode(assumption.Condition, "Assume")));
      return new Block(stmts);
    }
    public override Expression VisitAssignmentExpression(AssignmentExpression assignment){
      if (assignment == null) return null;
      StatementList statements = new StatementList();
      BlockExpression result = new BlockExpression(new Block(statements), assignment.Type);
      AssignmentStatement astatement = (AssignmentStatement)assignment.AssignmentStatement;
      if (astatement == null) return null;
      Expression target = this.VisitTargetExpression(astatement.Target);
      LRExpression lrexpr = target as LRExpression;
      if (lrexpr != null){
        LocalList locals = lrexpr.Temporaries;
        ExpressionList subs = lrexpr.SubexpressionsToEvaluateOnce;
        for (int i = 0, n = locals.Count; i < n; i++){
          statements.Add(new AssignmentStatement(locals[i], subs[i]));
          if (i == 0){
            statements[0].SourceContext = assignment.SourceContext;
            assignment.SourceContext.Document = null;
          }
        }
        target = this.VisitTargetExpression(lrexpr.Expression);
      }
      Expression source = this.VisitExpression(astatement.Source);
      if (source == null) return null;
      //TODO: figure out what to do about coercions     
      MethodCall mcall = target as MethodCall;
      if (mcall != null) {
        Local loc = new Local();
        loc.Type = source.Type;
        statements.Add(new AssignmentStatement(loc, source));
        mcall.Operands.Add(loc);
        statements.Add(new ExpressionStatement(mcall));
        statements.Add(new ExpressionStatement(loc));
      }else{        
        Reference r = target.Type as Reference;
        if (r != null) target = new AddressDereference(target, r.ElementType);            
        Local loc = new Local();
        loc.Type = source.Type;
        statements.Add(new AssignmentStatement(loc, source));
        astatement.Source = loc;
        astatement.Target = target;
        statements.Add(astatement);
        statements.Add(new ExpressionStatement(loc));
      }
      result.Type = source.Type;
      return result;
    }
    private AssignmentStatement enclosingAssignmentStatement;
    private Expression VisitAssignmentSource(Expression source, AssignmentStatement assignment) {
      AssignmentStatement savedEnclosingAssignmentStatement = this.enclosingAssignmentStatement;
      this.enclosingAssignmentStatement = assignment;

      source = VisitExpression(source);

      this.enclosingAssignmentStatement = savedEnclosingAssignmentStatement;
      return source;
    }
    public override Statement VisitAssignmentStatement(AssignmentStatement assignment){
      if (assignment == null) return null;
      Expression target = assignment.Target;
      ConstructTuple ctup = target as ConstructTuple;
      if (ctup != null) return VisitAssignmentToTuple(assignment);
      assignment.Target = target = this.VisitTargetExpression(target);
      if (target == null) return null;
      LRExpression lrexpr = target as LRExpression;
      if (lrexpr != null){        
        StatementList statements = new StatementList();
        Block result = new Block(statements);
        LocalList locals = lrexpr.Temporaries;
        ExpressionList subs = lrexpr.SubexpressionsToEvaluateOnce;
        for (int i = 0, n = locals.Count; i < n; i++){
          statements.Add(new AssignmentStatement(locals[i], subs[i]));
          if (i == 0){
            statements[0].SourceContext = assignment.SourceContext;
          }
        }
        target = assignment.Target = this.VisitTargetExpression(lrexpr.Expression);
        Reference r = target.Type as Reference;
        if (r != null) target = assignment.Target = new AddressDereference(target, r.ElementType);            
        Expression source = assignment.Source = this.VisitAssignmentSource(assignment.Source, assignment);
        if (source == null) return null;
        MethodCall mcall = target as MethodCall;
        if (mcall != null){
          Local loc = new Local(); loc.Type = source.Type;
          assignment.Target = loc;
          statements.Add(assignment);
          mcall.Operands.Add(loc);
          statements.Add(new ExpressionStatement(mcall));
        }else
          statements.Add(assignment);
        return result;
      }else{
        MethodCall mcall = target as MethodCall;
        if (target.Type != null && target.Type.IsValueType && assignment.Source is Local && ((Local)assignment.Source).Name == StandardIds.NewObj) {
          if (target is AddressDereference)
            return new AssignmentStatement(target, new Literal(null, SystemTypes.Object), assignment.SourceContext);
          else if (mcall == null) {
            return new AssignmentStatement(new AddressDereference(new UnaryExpression(target, NodeType.AddressOf, target.Type.GetReferenceType()), target.Type),
              new Literal(null, SystemTypes.Object), assignment.SourceContext);
          }
        }
        Expression source = assignment.Source = this.VisitAssignmentSource(assignment.Source, assignment);
        if (source == null) return null;
        if (mcall != null && !(mcall.Type is Reference)){
          mcall.Operands.Add(source);
          return new ExpressionStatement(mcall, assignment.SourceContext);
        }else{
          if (target.Type is Struct && target.Type.Template != null && target.Type.Template == SystemTypes.GenericBoxed && source is Literal){
            Literal lit = (Literal)source;
            if (lit.Value != null){
              Debug.Assert(false); return null;
            }
            Method clear = this.GetTypeView(target.Type).GetMethod(StandardIds.Clear);
            return new ExpressionStatement(new MethodCall(new MemberBinding(new UnaryExpression(target, NodeType.AddressOf, target.Type.GetReferenceType()), clear), null, NodeType.Call, SystemTypes.Void), assignment.SourceContext);
          }
          Reference r = target.Type as Reference;
          if (r != null && assignment.Operator != NodeType.CopyReference)
            assignment.Target = new AddressDereference(target, r.ElementType);
          return assignment;
        }
      }
    }
    public virtual Statement VisitAssignmentToTuple(AssignmentStatement assignment){
      if (assignment == null) return null;
      ConstructTuple target = assignment.Target as ConstructTuple;
      ConstructTuple source = assignment.Source as ConstructTuple;
      if (target == null || source == null || source.Type == null) return null;
      FieldList tfields = target.Fields;
      int n = tfields == null ? 0 : tfields.Count;
      MemberList smembers = this.GetTypeView(source.Type).Members;
      if (smembers == null || smembers.Count != n+2) return null;
      if (n == 0) return null;
      StatementList statements = new StatementList(n+1);
      Local loc = new Local(source.Type);
      statements.Add(new AssignmentStatement(loc, this.VisitExpression(source), assignment.SourceContext));
      for (int i = 0; i < n; i++){
        Field f = tfields[i]; if (f == null) continue;
        Expression tExpr = this.VisitTargetExpression(f.Initializer);
        statements.Add(new AssignmentStatement(tExpr, new MemberBinding(new UnaryExpression(loc, NodeType.AddressOf, loc.Type.GetReferenceType()), smembers[i])));
      }
      return new Block(statements);
    }
    public override Block VisitBlock(Block block){
      if (block == null) return null;
      block.Statements = this.VisitStatementList(block.Statements);
      if (this.currentExceptionBlock != null)
        this.exceptionBlockFor[block.UniqueKey] = this.currentExceptionBlock;
      return block;
    }
    public override Expression VisitBlockExpression(BlockExpression blockExpression){
      if (blockExpression == null) return null;
      Block b = blockExpression.Block = this.VisitBlock(blockExpression.Block);
      StatementList statements = b == null ? null : b.Statements;
      if (statements != null && statements.Count > 0){
        ExpressionStatement es = statements[statements.Count-1] as ExpressionStatement;
        if (es != null && es.Expression != null && es.Expression.Type != SystemTypes.Void){
          UnaryExpression uexpr = es.Expression as UnaryExpression;
          if (uexpr != null && uexpr.NodeType == NodeType.Pop){
            es.Expression = uexpr.Operand;
            if (this.enclosingAssignmentStatement != null) {
              // make it easier for the reader to attach proper source context
              es.SourceContext = this.enclosingAssignmentStatement.SourceContext;
            }
            else {
            es.SourceContext.Document = null;
          }
        }
      }
      }
      return blockExpression;
    }
    public override Statement VisitBranch(Branch branch){
      if (branch == null) return null;
      branch.Condition = this.VisitBranchCondition(branch.Condition);
      BinaryExpression be = branch.Condition as BinaryExpression;
      // Force the unordered/unsigned flag to be set. Note that visitBranch may be bypassed
      // in the normalizer. 
      if (be != null) {
        if (be.Operand1 != null && be.Operand2 != null && be.Operand1.Type!= null && be.Operand2.Type!= null) {
          if (be.Operand1.Type.IsPrimitiveUnsignedInteger && be.Operand2.Type.IsPrimitiveUnsignedInteger)
            branch.BranchIfUnordered = true; 
        }
      }
      branch.LeavesExceptionBlock = branch.LeavesExceptionBlock ||
        this.currentExceptionBlock != null && this.currentExceptionBlock != this.exceptionBlockFor[branch.Target.UniqueKey];
      return branch;
    }
    public virtual Expression VisitBranchCondition(Expression condition){
      if (condition == null) return null;
      BinaryExpression bexpr = condition as BinaryExpression;
      if (bexpr != null){
        if (bexpr.Operand1 == null || bexpr.Operand2 == null) return null;
        switch (bexpr.NodeType){
          case NodeType.Eq:
          case NodeType.Ne:
          case NodeType.Lt:
          case NodeType.Le:
          case NodeType.Gt:
          case NodeType.Ge:
            bexpr.Operand1 = this.VisitExpression(bexpr.Operand1);
            bexpr.Operand2 = this.VisitExpression(bexpr.Operand2);
            return bexpr;
        }
      }
      return this.VisitExpression(condition);
    }
    public virtual Expression VisitShortCircuitBitwiseOp(BinaryExpression binaryExpression){
      if (binaryExpression == null) return null;
      Local loc = (Local)binaryExpression.Operand1;
      MethodCall mcall = (MethodCall)binaryExpression.Operand2;
      StatementList statements = new StatementList(2);
      Expression x = mcall.Operands[0];
      mcall.Operands[0] = loc;
      Method bitwiseOp = (Method)((MemberBinding)mcall.Callee).BoundMember;
      Method op = binaryExpression.NodeType == NodeType.LogicalAnd ? this.GetTypeView(bitwiseOp.DeclaringType).GetOpFalse() : this.GetTypeView(bitwiseOp.DeclaringType).GetOpTrue();
      ExpressionList args = new ExpressionList(1);
      args.Add(loc);
      MethodCall callOp = new MethodCall(new MemberBinding(null, op), args, NodeType.Call, SystemTypes.Boolean);
      TernaryExpression tern = new TernaryExpression(callOp, loc, mcall, NodeType.Conditional, loc.Type);
      statements.Add(new AssignmentStatement(loc, this.VisitExpression(x)));
      statements.Add(new ExpressionStatement(this.VisitTernaryExpression(tern)));
      Block b = new Block(statements);
      return new BlockExpression(new Block(statements), binaryExpression.Type);
    }
    public override AttributeNode VisitAttributeNode(AttributeNode attribute){
      if (attribute == null) return null;
      if (attribute.IsPseudoAttribute) return null;
      attribute.Constructor = this.VisitAttributeConstructor(attribute);
      MemberBinding mb = attribute.Constructor as MemberBinding;
      if (mb == null) return null;
      Method cons = mb.BoundMember as Method;
      if (cons == null) return null;
      TypeNode attributeType = cons.DeclaringType;
      if (attributeType == null) return null;
      AttributeNode condAttr = attributeType.GetAttribute(SystemTypes.ConditionalAttribute);
      if (condAttr != null && condAttr.Expressions != null && condAttr.Expressions.Count > 0){
        Literal lit = condAttr.Expressions[0] as Literal;
        if (lit != null){
          string symbol = lit.Value as string;
          if (symbol != null && this.currentPreprocessorDefinedSymbols != null &&
          !this.currentPreprocessorDefinedSymbols.ContainsKey(symbol))
            return null;
        }
      }
      ExpressionList args = attribute.Expressions;
      for (int i = 0, n = args == null ? 0 : args.Count; i < n; i++){
        Expression arg = args[i];
        if (arg is Literal) continue;
        NamedArgument narg = arg as NamedArgument;
        if (narg != null){
          arg = narg.Value;
          if (arg is Literal) continue;
        }
        ConstructArray consArr = arg as ConstructArray;
        if (consArr != null){
          arg = new Literal(this.ConvertToCompileTimeArray(consArr), TypeNode.StripModifiers(consArr.Type));
          if (arg == null) continue;
          if (narg != null)
            narg.Value = arg;
          else
            args[i] = arg;
          continue;
        }
        arg = this.VisitExpression(arg);
        arg = this.ConvertToCompileTimeType(arg);
        if (narg != null)
          narg.Value = arg;
        else
          args[i] = arg;
      }
      return attribute;
    }
    public virtual Expression ConvertToCompileTimeType(Expression arg){
      if (arg == null) return null;
      MethodCall mcall = arg as MethodCall;
      if (mcall != null && mcall.Callee is MemberBinding && ((MemberBinding)mcall.Callee).BoundMember == Runtime.GetTypeFromHandle &&
        mcall.Operands != null && mcall.Operands.Count > 0 && mcall.Operands[0] is UnaryExpression){
        Literal lit = ((UnaryExpression)mcall.Operands[0]).Operand as Literal;
        if (lit != null){
          TypeNode t = lit.Value as TypeNode;
          if (t != null){
            NamedArgument narg = arg as NamedArgument;
            if (narg != null){
              narg.Value = lit;
              return narg;
            }
            return lit;
          }
        }
      }
      return null;
    }
    public virtual object[] ConvertToCompileTimeArray(ConstructArray consArr){
      if (consArr == null) return null;
      TypeNode elemType = consArr.ElementType;
      ExpressionList elems = consArr.Initializers;
      int n = elems == null ? 0 : elems.Count;
      object[] result = new object[n];
      for (int i = 0; i < n; i++){
        Expression e = this.VisitExpression(elems[i]);
        BinaryExpression binExp = e as BinaryExpression;
        if (binExp != null && binExp.NodeType == NodeType.Box)
          e = binExp.Operand1;
        Literal lit = e as Literal;
        if (lit == null) lit = this.ConvertToCompileTimeType(e) as Literal;
        if (lit == null) continue;
        result[i] = lit.Value;
      }
      return result;
    }
    public override Expression VisitBase(Base Base) {
      if (Base.UsedAsMarker) {
        if (this.currentBaseCtorCall == null) return null;
        return this.currentBaseCtorCall;
      } else {
        return new ThisBinding(this.currentThisParameter, Base.SourceContext);
      }
    }
    public virtual Expression VisitLiftedBinaryExpression(BinaryExpression binaryExpression){
      if (binaryExpression == null || !this.typeSystem.IsNullableType(binaryExpression.Type)){
        Debug.Assert(false); return null;
      }
      Expression opnd1 = binaryExpression.Operand1;
      if (opnd1 == null || opnd1.Type == null) return null;
      if (!this.typeSystem.IsNullableType(opnd1.Type)){Debug.Assert(false); return null;}
      Expression opnd2 = binaryExpression.Operand2;
      if (opnd2 == null || opnd2.Type == null) return null;
      if (!this.typeSystem.IsNullableType(opnd2.Type)){Debug.Assert(false); return null;}

      Local loc1 = new Local(opnd1.Type);
      Local loc2 = new Local(opnd2.Type);
      Local result = new Local(binaryExpression.Type);
      Method hasValue = this.GetTypeView(binaryExpression.Type).GetMethod(StandardIds.getHasValue);
      if (hasValue == null){Debug.Assert(false); return null;}
      Block checkOperand2 = new Block();
      Block doUnliftedOperator = new Block();
      Block returnResult = new Block();
      Block b = new Block(new StatementList(16));
      b.Statements.Add(new AssignmentStatement(loc1, this.VisitExpression(opnd1)));
      b.Statements.Add(new AssignmentStatement(loc2, this.VisitExpression(opnd2)));
      Expression hasValue1 = new MemberBinding(new UnaryExpression(loc1, NodeType.AddressOf, loc1.Type.GetReferenceType()), hasValue);
      Expression loc1HasValue = new MethodCall(hasValue1, null, NodeType.Call, SystemTypes.Boolean);
      b.Statements.Add(new Branch(loc1HasValue, checkOperand2, true, false, false));
      b.Statements.Add(new AssignmentStatement(result, loc1));
      b.Statements.Add(new Branch(null, returnResult, true, false, false));
      b.Statements.Add(checkOperand2);
      Expression hasValue2 = new MemberBinding(new UnaryExpression(loc2, NodeType.AddressOf, loc2.Type.GetReferenceType()), hasValue);
      Expression loc2HasValue = new MethodCall(hasValue2, null, NodeType.Call, SystemTypes.Boolean);
      b.Statements.Add(new Branch(loc2HasValue, doUnliftedOperator, true, false, false));
      b.Statements.Add(new AssignmentStatement(result, loc2));
      b.Statements.Add(new Branch(null, returnResult, true, false, false));
      b.Statements.Add(doUnliftedOperator);
      Expression val1 = this.typeSystem.ExplicitCoercion(loc1, loc1.Type.TemplateArguments[0], this.TypeViewer);
      Expression val2 = this.typeSystem.ExplicitCoercion(loc2, loc2.Type.TemplateArguments[0], this.TypeViewer);
      Expression unlifted = this.VisitBinaryExpression(new BinaryExpression(val1, val2, binaryExpression.NodeType, binaryExpression.Type.TemplateArguments[0]));
      b.Statements.Add(new AssignmentStatement(result, this.typeSystem.ExplicitCoercion(unlifted, result.Type, this.TypeViewer)));
      b.Statements.Add(returnResult);
      b.Statements.Add(new ExpressionStatement(result));
      return new BlockExpression(b);
    }
    public override Expression VisitBinaryExpression(BinaryExpression binaryExpression){
      if (binaryExpression == null) return null;
      if (this.typeSystem.IsNullableType(binaryExpression.Type)){
        switch(binaryExpression.NodeType){
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
            return this.VisitLiftedBinaryExpression(binaryExpression);
        }
      }
      switch(binaryExpression.NodeType){
        case NodeType.Range:
          Expression range = new Construct(new MemberBinding(null,SystemTypes.Range.GetConstructor(SystemTypes.Int32,SystemTypes.Int32)),new ExpressionList(binaryExpression.Operand1, binaryExpression.Operand2),SystemTypes.Range);
          range.SourceContext = binaryExpression.SourceContext;
          return this.VisitExpression(range);       
        case NodeType.Maplet:
          Expression maplet = new Construct(new MemberBinding(null,SystemTypes.DictionaryEntry.GetConstructor(SystemTypes.Object,SystemTypes.Object)),new ExpressionList(binaryExpression.Operand1, binaryExpression.Operand2),SystemTypes.DictionaryEntry);
          maplet.SourceContext = binaryExpression.SourceContext;
          return this.VisitExpression(maplet);       
        case NodeType.Implies:
          Expression implies = new TernaryExpression(binaryExpression.Operand1, binaryExpression.Operand2, new Literal(true, SystemTypes.Boolean), NodeType.Conditional, SystemTypes.Boolean);
          implies.SourceContext = binaryExpression.SourceContext;
          return this.VisitExpression(implies);
        case NodeType.Iff:
          Expression iff = new BinaryExpression(binaryExpression.Operand1, binaryExpression.Operand2, NodeType.Eq, SystemTypes.Boolean);
          iff.SourceContext = binaryExpression.SourceContext;
          return this.VisitExpression(iff);
        case NodeType.LogicalAnd:
          if (binaryExpression.Operand1 is Local && binaryExpression.Operand2 is MethodCall)
            return this.VisitShortCircuitBitwiseOp(binaryExpression);
          Expression e = new TernaryExpression(binaryExpression.Operand1, binaryExpression.Operand2, new Literal(false, SystemTypes.Boolean), NodeType.Conditional, SystemTypes.Boolean);
          e.SourceContext = binaryExpression.SourceContext;
          return this.VisitExpression(e);
        case NodeType.LogicalOr: 
          if (binaryExpression.Operand1 is Local && binaryExpression.Operand2 is MethodCall)
            return this.VisitShortCircuitBitwiseOp(binaryExpression);
          e = new TernaryExpression(binaryExpression.Operand1, new Literal(true, SystemTypes.Boolean), binaryExpression.Operand2, NodeType.Conditional, SystemTypes.Boolean);
          e.SourceContext = binaryExpression.SourceContext;
          return this.VisitExpression(e);
      }
      binaryExpression.Operand1 = this.VisitExpression(binaryExpression.Operand1);
      binaryExpression.Operand2 = this.VisitExpression(binaryExpression.Operand2);
      TypeNode t = binaryExpression.Type;
      switch(binaryExpression.NodeType){
        case NodeType.Add:
        case NodeType.Sub:
          DelegateNode delType = t as DelegateNode;
          if (delType != null){
            ExpressionList args = new ExpressionList(2);
            args.Add(binaryExpression.Operand1);
            args.Add(binaryExpression.Operand2);
            MethodCall mcall = new MethodCall(new MemberBinding(null, Runtime.Combine), args, NodeType.Call, Runtime.Combine.ReturnType, binaryExpression.SourceContext);
            if (binaryExpression.NodeType == NodeType.Sub) 
              ((MemberBinding)mcall.Callee).BoundMember = Runtime.Remove;
            binaryExpression = new BinaryExpression(mcall, new Literal(delType, SystemTypes.Type), NodeType.Castclass, t, binaryExpression.SourceContext);
          }
          break;
        case NodeType.Box:
          Literal lit = binaryExpression.Operand2 as Literal;
          if (lit != null){
            TypeNode bt = lit.Value as TypeNode;
            if (bt != null)
              binaryExpression.Operand2 = lit = new Literal(this.VisitTypeReference(bt), lit.Type, lit.SourceContext);
          }
          break;
        case NodeType.ExplicitCoercion:
          return binaryExpression.Operand1;
        case NodeType.Castclass:
          if (t == null) return null;
          if (t.IsValueType){
            binaryExpression.NodeType = NodeType.Unbox;
            AddressDereference dref = new AddressDereference(binaryExpression, t); 
            dref.Type = t;
            return dref;
          }else if (t is Pointer)
            return this.NormalizeToPointerCoercion(binaryExpression);
          if (binaryExpression.Operand1 != null && binaryExpression.Operand1.Type != null && binaryExpression.Operand1.Type.IsValueType)
            binaryExpression.Operand1 =
              new BinaryExpression(binaryExpression.Operand1, new MemberBinding(null, binaryExpression.Operand1.Type), NodeType.Box, SystemTypes.Object);
          break;
        case NodeType.Comma:{
          StatementList statements = new StatementList(2);
          Expression opnd1 = binaryExpression.Operand1;
          if (opnd1 != null && opnd1.Type != SystemTypes.Void) opnd1 = new UnaryExpression(opnd1, NodeType.Pop, SystemTypes.Void);
          statements.Add(new ExpressionStatement(opnd1));
          statements.Add(new ExpressionStatement(binaryExpression.Operand2));
          return new BlockExpression(new Block(statements), SystemTypes.Boolean);
        }
        case NodeType.Eq :
        case NodeType.Ge : 
        case NodeType.Gt :
        case NodeType.Le : 
        case NodeType.Lt :
        case NodeType.Ne :
          // HACK: This tells normalizer to leave the expression as it is, because I need to run evaluator over it
          if (this.WrapToBlockExpression){
            Block pushTrue = new Block(null);
            Block done = new Block(null);
            StatementList statements = new StatementList(6);
            if (binaryExpression.Operand1.Type != null && binaryExpression.Operand2.Type != null && binaryExpression.Operand1.Type.IsPrimitiveUnsignedInteger && binaryExpression.Operand2.Type.IsPrimitiveUnsignedInteger) {
              statements.Add(new Branch(binaryExpression, pushTrue, binaryExpression.SourceContext, true));
            }
            else statements.Add(new Branch(binaryExpression, pushTrue));
            statements.Add(new ExpressionStatement(new Literal(false, SystemTypes.Boolean)));
            statements.Add(new Branch(null, done));
            statements.Add(pushTrue);
            statements.Add(new ExpressionStatement(new Literal(true, SystemTypes.Boolean)));
            statements.Add(done);
            return new BlockExpression(new Block(statements), SystemTypes.Boolean, binaryExpression.SourceContext);
          }
          return binaryExpression;
        case NodeType.Is :
          if (binaryExpression.Operand2 is Literal)
            binaryExpression.Type = (TypeNode)((Literal) binaryExpression.Operand2).Value;
          else
            binaryExpression.Type = (TypeNode)((MemberBinding)binaryExpression.Operand2).BoundMember;
          binaryExpression.NodeType = NodeType.Isinst;
          if (this.useGenerics && binaryExpression.Operand1 != null && binaryExpression.Operand1.Type != null && binaryExpression.Operand1.Type.IsTemplateParameter)
            binaryExpression.Operand1 = new BinaryExpression(binaryExpression.Operand1, new Literal(binaryExpression.Operand1.Type), NodeType.Box);
          binaryExpression = new BinaryExpression(binaryExpression, new Literal(null, SystemTypes.Object), NodeType.Ne);
          goto case NodeType.Eq;
        case NodeType.NullCoalesingExpression: {
          Expression cachedOperand1 = binaryExpression.Operand1;
          if (cachedOperand1 == null || cachedOperand1.Type == null || binaryExpression.Operand2 == null) return null;
          if (!(cachedOperand1 is Local || cachedOperand1 is Parameter))
            cachedOperand1 = new Local(cachedOperand1.Type);
          Local exprValue = new Local(binaryExpression.Type);
          StatementList statements = new StatementList();
          Block pushValue = new Block();
          Block done = new Block();
          Expression operand1HasValue;
          Method hasValue = this.GetTypeView(binaryExpression.Operand1.Type).GetMethod(StandardIds.getHasValue);
          if (hasValue != null)
            operand1HasValue = new MethodCall(new MemberBinding(new UnaryExpression(cachedOperand1, NodeType.AddressOf), hasValue), null);
          else {
            operand1HasValue = new BinaryExpression(this.typeSystem.ImplicitCoercion(cachedOperand1, SystemTypes.Object), Literal.Null, NodeType.Ne);
          }
          statements.Add(new Branch(operand1HasValue, pushValue));
          Expression operand2 = this.typeSystem.ImplicitCoercion(binaryExpression.Operand2, binaryExpression.Type);
          statements.Add(new AssignmentStatement(exprValue, operand2));
          statements.Add(new Branch(null, done));
          statements.Add(pushValue);
          Expression operand1Value = null;
          if (hasValue != null) {
            Method getValueOrDefault = this.GetTypeView(binaryExpression.Operand1.Type).GetMethod(StandardIds.GetValueOrDefault);
            operand1Value = new MethodCall(new MemberBinding(new UnaryExpression(cachedOperand1, NodeType.AddressOf), getValueOrDefault), null);
            operand1Value.Type = getValueOrDefault.ReturnType;
            operand1Value = this.typeSystem.ImplicitCoercion(operand1Value, binaryExpression.Type);
            statements.Add(new AssignmentStatement(exprValue, operand1Value));
          } else {
            operand1Value = this.typeSystem.ImplicitCoercion(cachedOperand1, binaryExpression.Type);
            statements.Add(new AssignmentStatement(exprValue, operand1Value));
          }
          statements.Add(done);
          statements.Add(new ExpressionStatement(exprValue));
          return new BlockExpression(new Block(statements), binaryExpression.Type, binaryExpression.SourceContext);
        }
        case NodeType.Shr :
          t = binaryExpression.Operand1.Type;
          if (t == SystemTypes.Char || t.IsUnsignedPrimitiveNumeric)
            binaryExpression.NodeType = NodeType.Shr_Un;
          break;

      }
      return binaryExpression;
    }
    /// <summary>
    /// Hook for other languages to do something different. Normal C# or Spec# don't emit any particular coercion to pointer types.
    /// </summary>
    /// <param name="binaryExpr">NodeType.Castclass, Type is Pointer</param>
    protected virtual Expression NormalizeToPointerCoercion(BinaryExpression binaryExpr) {
      if (binaryExpr == null) return null;
      if (binaryExpr.Operand1 != null)
        binaryExpr.Operand1.Type = binaryExpr.Type;
      return binaryExpr.Operand1;
    }

    public override Expression VisitAnonymousNestedFunction(AnonymousNestedFunction func){
      if (func == null) return null;
      Method method = func.Method;
      if (method == null) return null;
      TypeNode[] parameterTypes = null;
      if (this.currentMethod != null && this.currentMethod.Scope != null) {
        int paramCount = method.Parameters == null ? 0 : method.Parameters.Count;
        parameterTypes = new TypeNode[paramCount];
        method.ReturnType = this.currentMethod.Scope.FixTypeReference(method.ReturnType);
        for (int i = 0; i < paramCount; i++) {
          Parameter p = method.Parameters[i];
          if (p == null) continue;
          p.Type = this.currentMethod.Scope.FixTypeReference(p.Type);
          parameterTypes[i] = p.Type;
        }
      }
      Block block = method.Body;
      StatementList statements = block == null ? null : block.Statements;
      if (statements != null && statements.Count == 2 && statements[0] is ExpressionStatement && statements[1] is Return){
        statements[0].SourceContext.Document = null;
        if (method.Attributes == null) method.Attributes = new AttributeList(2);
        method.Attributes.Add(new AttributeNode(new MemberBinding(null, SystemTypes.DebuggerHiddenAttribute.GetConstructor()), null));
        method.Attributes.Add(new AttributeNode(new MemberBinding(null, SystemTypes.DebuggerStepThroughAttribute.GetConstructor()), null));
      }
      this.VisitMethod(method);
      method.DeclaringType.Members.Add(method);
      if (this.currentClosureLocal != null && this.currentClosureLocal.Type != null && this.currentClosureLocal.Type.Template != null) {
        Construct cons = func.Invocation as Construct;
        if (cons != null && cons.Operands != null && cons.Operands.Count >= 2) {
          UnaryExpression ue = cons.Operands[1] as UnaryExpression;
          if (ue != null) {
            MemberBinding mb = ue.Operand as MemberBinding;
            if (mb != null && mb.BoundMember == method) {
              Method specializedMethod = (Method)mb.BoundMember.Clone();
              specializedMethod.Parameters = specializedMethod.Parameters.Clone();
              for (int i = 0, n = specializedMethod.Parameters.Count; i < n; i++) {
                Parameter p = (Parameter)specializedMethod.Parameters[i].Clone();
                p.DeclaringMethod = specializedMethod;
                specializedMethod.Parameters[i] = p;
              }
              mb.BoundMember = specializedMethod;
              specializedMethod.DeclaringType = this.currentClosureLocal.Type;
              this.currentClosureLocal.Type.Members.Add(specializedMethod);
            }
          }
        }
      }
      return this.VisitExpression(func.Invocation);
    }
    public override Expression VisitApplyToAll(ApplyToAll applyToAll){
      if (applyToAll == null) return null;
      CollectionEnumerator cEnumerator = applyToAll.Operand1 as CollectionEnumerator;
      AnonymousNestedFunction func = applyToAll.Operand2 as AnonymousNestedFunction;
      if (func != null) this.VisitAnonymousNestedFunction(func);
      Expression cc = this.currentClosureLocal;
      if (cEnumerator == null) {
        Debug.Assert(cc != null, "VisitApplyToAll: no closure");  
        Debug.Assert(func != null, "VisitApplyToAll: no function");
        if (cc == null || func == null) return null;
        cc = this.GetBindingPath(cc, func.Method.DeclaringType);
        return new MethodCall(new MemberBinding(cc, func.Method), new ExpressionList(this.VisitExpression(applyToAll.Operand1)),
          NodeType.Call, func.Method.ReturnType, cc.SourceContext);
      }
      Debug.Assert(cEnumerator.Collection != null, "VisitApplyToAll: no collection");
      if (cEnumerator.Collection == null) return null;
      Method method = applyToAll.ResultIterator;
      if (method != null) {
        Debug.Assert(cc != null, "VisitApplyToAll: no closure");
        if (cc == null) return null;
        cc = this.GetBindingPath(cc, method.DeclaringType);
        this.VisitMethod(method);
        Expression coll = this.VisitExpression(cEnumerator.Collection);
        if (coll == null) return null;
        if (func != null)
          return new MethodCall(new MemberBinding(cc, method), new ExpressionList(coll, cc), NodeType.Call, applyToAll.Type);
        else
          return new MethodCall(new MemberBinding(cc, method), new ExpressionList(coll), NodeType.Call, applyToAll.Type);
      }
      ForEach forEach = new ForEach();
      forEach.SourceContext = applyToAll.SourceContext;
      forEach.SourceEnumerable = cEnumerator;
      if (func != null) {
        MemberBinding mb = new MemberBinding(cc, func.Method);
        MethodCall call = new MethodCall(mb, new ExpressionList(cEnumerator.ElementLocal), NodeType.Call, func.Method.ReturnType, cc.SourceContext);
        forEach.Body = new Block(new StatementList(new ExpressionStatement(call)));
      }
      else {
        BlockExpression be = applyToAll.Operand2 as BlockExpression;
        Debug.Assert(be != null, "VisitApplyToAll: Bad operand type");
        if (be == null) return null;
        Block b = new Block(new StatementList(2));
        b.Statements.Add(new AssignmentStatement(applyToAll.ElementLocal, cEnumerator.ElementLocal));
        b.Statements.Add(be.Block);
        forEach.Body = b;
      }
      forEach.ScopeForTemporaryVariables = new BlockScope(this.currentMethod.Scope, forEach.Body);
      return this.VisitBlockExpression(new BlockExpression(new Block(new StatementList(forEach)), SystemTypes.Void));
    }
    public override Expression VisitCurrentClosure(CurrentClosure currentClosure){
      if (currentClosure == null) return null;
      if (this.currentClosureLocal == null && this.currentThisParameter != null) 
        return new ThisBinding(this.currentThisParameter, currentClosure.SourceContext);
      return this.currentClosureLocal;
    }
    public override Statement VisitFunctionDeclaration(FunctionDeclaration func){
      if (func == null || func.Method == null) return null;
      TypeNode closure = func.Method.DeclaringType;
      this.VisitMethod(func.Method);
      if (closure != null && closure.Members != null)
        closure.Members.Add(func.Method);
      return null;
    }
    public override Expression VisitCoerceTuple(CoerceTuple coerceTuple){
      if (coerceTuple == null) return null;
      StatementList statements = new StatementList(2);
      statements.Add(new AssignmentStatement(coerceTuple.Temp, this.VisitExpression(coerceTuple.OriginalTuple)));
      statements.Add(new ExpressionStatement(this.VisitConstructTuple(coerceTuple)));
      return new BlockExpression(new Block(statements));
    }
    public override TypeNode VisitConstrainedType(ConstrainedType cType){
      cType = (ConstrainedType)base.VisitConstrainedType(cType);
      if (cType == null) return null;
      cType.ProvideBodiesForMethods();
      cType.NodeType = NodeType.Struct;
      return cType;
    }
    public override Compilation VisitCompilation(Compilation compilation){
      if (compilation == null) return null;
      Compilation savedCompilation = this.currentCompilation;
      this.currentCompilation = compilation;
      this.currentModule = compilation.TargetModule;
      compilation = base.VisitCompilation(compilation);
      if (compilation == null) return null;
      AssemblyNode assem = compilation.TargetModule as AssemblyNode;
      if (assem != null && assem.Attributes != null && assem.Attributes.Count > 0){
        SecurityAttributeList secAttrs = this.currentModule.SecurityAttributes;
        this.ExtractSecurityAttributes(assem.Attributes, ref secAttrs);
      }
      compilation.TargetModule.IsNormalized = true;
      CompilerOptions coptions = compilation.CompilerParameters as CompilerOptions;
      // Add debuggable attributes
      Module module = compilation.TargetModule;
      if (module != null && coptions != null && coptions.IncludeDebugInformation) {
        if (coptions == null || !coptions.Optimize)
          module.Attributes.Add(Runtime.Debuggable);
        else if (coptions.PDBOnly)
          module.Attributes.Add(Runtime.OptimizedWithPDBOnly);
        else
          module.Attributes.Add(Runtime.OptimizedButDebuggable);
      }
      this.currentCompilation = savedCompilation;
      return compilation;
    }
    public override CompilationUnit VisitCompilationUnit(CompilationUnit cUnit){
      if (cUnit == null) return null;
      this.currentPreprocessorDefinedSymbols = cUnit.PreprocessorDefinedSymbols;
      this.currentModule = cUnit.Compilation != null ? cUnit.Compilation.TargetModule : null;
      return base.VisitCompilationUnit(cUnit);
    }
    public void ExtractSecurityAttributes(AttributeList attributes, ref SecurityAttributeList securityAttributes){
      if (attributes == null){Debug.Assert(false); return;}
      TrivialHashtable attributeListFor = null;
      for (int i = 0, n = attributes.Count; i < n; i++){
        AttributeNode attr = attributes[i];
        if (attr == null) continue;
        if (attr.Type == null) continue;
        if (!this.GetTypeView(attr.Type).IsAssignableTo(SystemTypes.SecurityAttribute)) continue;
        attributes[i] = null;
        ExpressionList args = attr.Expressions;
        if (args == null || args.Count < 1) return;
        Literal lit = args[0] as Literal;
        if (lit == null || !(lit.Value is int)) return;
        int action = (int)lit.Value;
        if (attributeListFor == null) attributeListFor = new TrivialHashtable();
        AttributeList attrsForAction = (AttributeList)attributeListFor[action+1];
        if (attrsForAction == null){
          attributeListFor[action+1] = attrsForAction = new AttributeList();
          SecurityAttribute secAttr = new SecurityAttribute();
          secAttr.Action = (System.Security.Permissions.SecurityAction)action;
          secAttr.PermissionAttributes = attrsForAction;
          if (securityAttributes == null) securityAttributes = new SecurityAttributeList();
          securityAttributes.Add(secAttr);
        }
        attrsForAction.Add(attr);
      }
    }
    public override Node VisitComposition(Composition comp){
      if (comp == null) return null;
      if (comp.GetType() == typeof(Composition))
        return this.Visit(comp.Expression);      
      return base.VisitComposition(comp);
    }
    public override Expression VisitConstructArray(ConstructArray consArr){
      if (consArr == null) return null;
      ArrayType arrayType = this.typeSystem.Unwrap(consArr.Type) as ArrayType;
       
      if (arrayType == null){Debug.Assert(consArr.Type == null); return null;}
      consArr.Operands = this.VisitExpressionList(consArr.Operands);
      ExpressionList initializers = consArr.Initializers;
      consArr.Initializers = null;
      int rank = consArr.Rank;
      if (initializers == null){
        if (consArr.Operands == null) return null;
        rank = consArr.Operands.Count;
        if (rank == 1) return CreateOwnerIsMethodCall(consArr);
        if (consArr.Type == null) return null;
        TypeNode[] types = new TypeNode[rank];
        for (int i = 0; i < rank; i++) types[i] = SystemTypes.Int32;
        InstanceInitializer ctor = this.GetTypeView(arrayType).GetConstructor(types);
        if (ctor == null){Debug.Assert(false); return null;}
        return CreateOwnerIsMethodCall(new Construct(new MemberBinding(null, ctor), consArr.Operands));
      }
      if (consArr.Operands == null || consArr.Operands.Count == 0){
        consArr.Operands = new ExpressionList(rank);
        ExpressionList inits = initializers;
        for (int i = 0; i < rank; i++){
          int m = inits == null ? 0 : inits.Count;
          consArr.Operands.Add(new Literal(m, SystemTypes.Int32));
          if (i == rank-1 || m < 1) break;
          ConstructArray cArr = inits[0] as ConstructArray;
          if (cArr == null) return null;
          inits = cArr.Initializers;
        }
      }
      int n = initializers.Count;
      bool isNonNullArray =  this.typeSystem.IsPossibleNonNullType(consArr.ElementType);
      if (n == 0) {
        if (isNonNullArray) {
          StatementList stmts = new StatementList(2);
          stmts.Add(new ExpressionStatement(CreateOwnerIsMethodCall(consArr)));
          stmts.Add(new ExpressionStatement(CreateAssertNonNullArrayCall(arrayType)));
          return new BlockExpression(new Block(stmts), consArr.Type);
          /*
          return CreateOwnerIsMethodCall(consArr);
          */
        }
        else {
          return CreateOwnerIsMethodCall(consArr);
        }
      }
      TypeNode elemType = this.typeSystem.GetUnderlyingType(consArr.ElementType);
      // 
      // If this is a non-null array with initializers, we add a call to 
      // NonNullType.AssertInitialized so that the analysis in definiteassignment
      // will know the "commit point" of the array. 
      
      int numStatements = (isNonNullArray) ? n + 2 : n + 1;
      StatementList statements = new StatementList(numStatements);
      if (rank > 1){
        TypeNode[] types = new TypeNode[rank];
        for (int i = 0; i < rank; i++) types[i] = SystemTypes.Int32;
        InstanceInitializer ctor = this.GetTypeView(arrayType).GetConstructor(types);
        if (ctor == null){Debug.Assert(false); return null;}
        statements.Add(new ExpressionStatement(CreateOwnerIsMethodCall(new Construct(new MemberBinding(null, ctor), consArr.Operands))));
        Int32List indices = new Int32List(rank);
        for (int i = 0; i < rank; i++) indices.Add(0);
        MemberBinding setter = new MemberBinding(new Expression(NodeType.Dup), arrayType.Setter);
        for (int i = 0; i < n; i++){
          indices[0] = i;
          this.InitializeMultiDimArray(rank-1, 1, indices, ((ConstructArray)initializers[i]).Initializers, setter, statements);
        }
        if (isNonNullArray) {
          // insert a call to AssertNonNullInit
          statements.Add(new ExpressionStatement(CreateAssertNonNullArrayCall(arrayType)));
        }
        return new BlockExpression(new Block(statements), consArr.Type);
      }
      statements.Add(new ExpressionStatement(CreateOwnerIsMethodCall(consArr)));
      for (int i = 0; i < n; i++){
        ExpressionList arguments = new ExpressionList(1);
        arguments.Add(new Literal(i, SystemTypes.Int32));
        Expression indexer = new Indexer(new Expression(NodeType.Dup, arrayType), arguments, elemType);
        if (elemType.IsValueType && !elemType.IsPrimitive)
          indexer = new AddressDereference(new UnaryExpression(indexer, NodeType.AddressOf, indexer.Type.GetReferenceType()), elemType);
        statements.Add(new AssignmentStatement(indexer, this.VisitExpression(initializers[i])));
      }
      if (isNonNullArray) {
        // insert a call to AssertNonNullInit
        statements.Add(new ExpressionStatement(CreateAssertNonNullArrayCall(arrayType)));
      }
      return new BlockExpression(new Block(statements), consArr.Type);
    }
    /// <summary>
    /// An auxilary function that returns an expression equavalent to:
    /// Dup;
    /// NonNullType.AssertInitialized();
    /// 
    /// In other words, we assume argument (NNArray being committed)
    /// is the top of the stack and will not consume it. 
    /// </summary>
    /// <param name="arrayType"></param>
    /// <returns></returns>
    private Expression CreateAssertNonNullArrayCall(ArrayType arrayType) {
      if (arrayType == null) return null;
      Method AssertNonNullMethod1 = SystemTypes.NonNullTypeAssertInitializedGeneric;
      Method AssertNonNullMethod;
      if (AssertNonNullMethod1 != null) {
        AssertNonNullMethod = AssertNonNullMethod1.GetTemplateInstance(SystemTypes.NonNullType, arrayType.ElementType);
      }
      else {
        // try to use non-generic method
        AssertNonNullMethod = SystemTypes.NonNullTypeAssertInitialized;
        if (AssertNonNullMethod == null) return null;
      }
      MethodCall mc = new MethodCall(new MemberBinding(null, AssertNonNullMethod), 
        new ExpressionList(new Expression(NodeType.Dup, arrayType)), NodeType.Call, OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Object),
        arrayType.SourceContext);
      return mc;
    }
    public virtual void InitializeMultiDimArray(int rank, int offset, Int32List indices, 
      ExpressionList initializers, MemberBinding setter, StatementList statements){
      for (int i = 0, n = initializers == null ? 0 : initializers.Count; i < n; i++){
        Expression initRow = initializers[i];
        if (initRow == null) continue;
        indices[offset] = i;
        if (rank > 1){
          this.InitializeMultiDimArray(rank-1, offset+1, indices, ((ConstructArray)initializers[i]).Initializers, setter, statements);
        }else{
          ExpressionList arguments = new ExpressionList(offset);
          for (int j = 0; j <= offset; j++) arguments.Add(new Literal(indices[j], SystemTypes.Int32));
          arguments.Add(this.VisitExpression(initializers[i]));
          statements.Add(new ExpressionStatement(new MethodCall(setter, arguments)));
        }
      }
    }
    public override Expression VisitConstructFlexArray(ConstructFlexArray consFlexArr){
      if (consFlexArr == null || consFlexArr.Type == null || consFlexArr.ElementType == null) return null;
      consFlexArr.Operands = this.VisitExpressionList(consFlexArr.Operands);
      ExpressionList initializers = consFlexArr.Initializers = this.VisitExpressionList(consFlexArr.Initializers);
      int initialCapacity = 0;
      int n = 0;
      if (consFlexArr.Operands != null && consFlexArr.Operands.Count > 0){
        Literal lit = consFlexArr.Operands[0] as Literal;
        if (lit != null && lit.Value is int)
          initialCapacity = (int)lit.Value;
      }else if (initializers != null)
        initialCapacity = (n = initializers.Count) * 2;
      InstanceInitializer cons = this.GetTypeView(consFlexArr.Type).GetConstructor(SystemTypes.Int32);
      if (cons == null){Debug.Assert(false); return null;}
      ExpressionList arguments = new ExpressionList(1);
      arguments.Add(new Literal(initialCapacity, SystemTypes.Int32));
      Construct consFlex = new Construct(new MemberBinding(null, cons), arguments);
      if (n == 0) return consFlex;
      StatementList statements = new StatementList(n+1);
      statements.Add(new ExpressionStatement(consFlex));
      Method add = this.GetTypeView(consFlexArr.Type).GetMethod(StandardIds.Add, consFlexArr.ElementType);
      for (int i = 0; i < n; i++){
        arguments = new ExpressionList(1);
        arguments.Add(initializers[i]);
        MethodCall call = new MethodCall(new MemberBinding(new Expression(NodeType.Dup), add), arguments);        
        statements.Add(new ExpressionStatement(call));
      }
      return new BlockExpression(new Block(statements), consFlexArr.Type);
    }
    protected static readonly Identifier IteratorCurrentEntryPont = Identifier.For("current Entry Point: ");
    protected static readonly Identifier IteratorCurrentValue = Identifier.For("current Value");
    public override Expression VisitConstructIterator(ConstructIterator consIterator){
      if (consIterator == null) return null;
      Field savedCurrValue = this.currentIteratorValue;
      Field savedCurrEntryPoint = this.currentIteratorEntryPoint;
      BlockList savedEntryPoints = this.iteratorEntryPoints;
      this.iteratorEntryPoints = new BlockList();
      Class state = consIterator.State;
      state.Flags &= ~TypeFlags.Abstract;
      //this.ClosureClasses[state.UniqueKey] = state;
      //Field for current value
      TypeNode elementType = this.currentMethod.Scope.FixTypeReference(consIterator.ElementType);
      Field currValue = this.currentIteratorValue = new Field(state, null, FieldFlags.Private, Normalizer.IteratorCurrentValue, elementType, null);
      state.Members.Add(currValue);
      //Field for current entrypoint
      Field currEntryPoint = this.currentIteratorEntryPoint = new Field(state, null, FieldFlags.Private, Normalizer.IteratorCurrentEntryPont, SystemTypes.Int32, null);
      state.Members.Add(currEntryPoint);
      //MoveNext
      This ThisParameter = new This(state);
      StatementList statements = new StatementList(4);
      Local savedCurrentClosureLocal = this.currentClosureLocal;
      this.currentClosureLocal = new Local(savedCurrentClosureLocal.Name, state);
      if (state.TemplateParameters != null && state.TemplateParameters.Count > 0)
        this.currentClosureLocal.Type = state.GetTemplateInstance(this.currentType, state.TemplateParameters);
      AssignmentStatement init = new AssignmentStatement(this.currentClosureLocal, ThisParameter);
      init.SourceContext = consIterator.Body.SourceContext;
      init.SourceContext.EndPos = consIterator.Body.SourceContext.StartPos;
      statements.Add(init);
      statements.Add(new SwitchInstruction(new MemberBinding(ThisParameter, this.currentIteratorEntryPoint), this.iteratorEntryPoints));
      this.iteratorEntryPoints.Add(consIterator.Body);
      Local savedCurrentReturnLocal = this.currentReturnLocal;
      this.currentReturnLocal = new Local(SystemTypes.Boolean);
      Block savedCurrentReturnLabel = this.currentReturnLabel;
      this.currentReturnLabel = new Block();
      This savedCurrentThisParameter = this.currentThisParameter;
      this.currentThisParameter = ThisParameter;
      statements.Add(this.VisitBlock(consIterator.Body));
      Expression index = new Literal(this.iteratorEntryPoints.Count, SystemTypes.Int32);
      statements.Add(new AssignmentStatement(new MemberBinding(this.currentThisParameter, this.currentIteratorEntryPoint), index));
      Block b = new Block(new StatementList(0));
      statements.Add(b);
      this.iteratorEntryPoints.Add(b);
      this.currentThisParameter = savedCurrentThisParameter;
      statements.Add(new AssignmentStatement(this.currentReturnLocal, new Literal(false, SystemTypes.Boolean)));
      statements.Add(this.currentReturnLabel);
      Return ret = new Return(this.currentReturnLocal);
      ret.SourceContext = consIterator.SourceContext;
      ret.SourceContext.StartPos = consIterator.SourceContext.EndPos-1;
      statements.Add(ret);
      Method moveNext = (Method)state.GetMembersNamed(StandardIds.MoveNext)[0];
      moveNext.ThisParameter = ThisParameter;
      moveNext.Body = new Block(statements);
      moveNext.Body.HasLocals = true;
      moveNext.ExceptionHandlers = this.currentMethod.ExceptionHandlers;
      this.currentMethod.ExceptionHandlers = null;
      //IDispose.Dispose
      statements = new StatementList(1);
      statements.Add(new Return());
      Method dispose = new Method(state, null, StandardIds.Dispose, null, SystemTypes.Void, new Block(statements));
      dispose.CallingConvention = CallingConventionFlags.HasThis;
      dispose.Flags = MethodFlags.Public|MethodFlags.Virtual;
      state.Members.Add(dispose);
      //IEnumerator.Reset
      statements = new StatementList(1);
      statements.Add(new Return()); //TODO: throw exception
      Method reset = new Method(state, null, StandardIds.Reset, null, SystemTypes.Void, new Block(statements));
      reset.CallingConvention = CallingConventionFlags.HasThis;
      reset.Flags = MethodFlags.Public|MethodFlags.Virtual;
      state.Members.Add(reset);
      //IEnumerator.GetCurrent
      statements = new StatementList(1);
      ThisParameter = new This(state);
      Expression currVal = new MemberBinding(ThisParameter, currValue);    
      switch (elementType.NodeType){
        case NodeType.TypeIntersection:
          currVal = this.typeSystem.CoerceTypeIntersectionToObject(currVal, this.TypeViewer);
          break;
        case NodeType.TypeUnion:
          currVal = this.typeSystem.CoerceTypeUnionToObject(currVal, this.TypeViewer);
          break;
        default:
          if (elementType.IsValueType || (elementType.IsTemplateParameter && this.useGenerics))
            currVal = new BinaryExpression(currVal, new MemberBinding(null, elementType), NodeType.Box, SystemTypes.Object);
          break;
      }
      statements.Add(new Return(currVal));
      Method ieGetCurrent = new Method(state, null, StandardIds.IEnumeratorGetCurrent, null, SystemTypes.Object, new Block(statements));
      ieGetCurrent.ThisParameter = ThisParameter;
      ieGetCurrent.ImplementedInterfaceMethods = new MethodList(Runtime.GetCurrent);
      ieGetCurrent.CallingConvention = CallingConventionFlags.HasThis;
      ieGetCurrent.Flags = MethodFlags.Private|MethodFlags.Virtual|MethodFlags.SpecialName;
      state.Members.Add(ieGetCurrent);
      //get_Current
      //TODO: need to check if MoveNext has been called and did not return false
      statements = new StatementList(1);
      ThisParameter = new This(state);
      statements.Add(new Return(new MemberBinding(ThisParameter, currValue)));
      Method getCurrent = (Method)state.GetMembersNamed(StandardIds.getCurrent)[0];
      getCurrent.ThisParameter = ThisParameter;
      getCurrent.Body = new Block(statements);
      //Current
      Property prop = new Property(state, null, PropertyFlags.None, StandardIds.Current, getCurrent, null);
      state.Members.Add(prop);
      //GetEnumerator
      statements = new StatementList(1);
      ThisParameter = new This(state);
      statements.Add(new Return(new BinaryExpression(new MethodCall(new MemberBinding(ThisParameter, Runtime.MemberwiseClone), null),
        new Literal(state,SystemTypes.Type), NodeType.Castclass)));
      Method getEnumerator = new Method(state, null, StandardIds.GetEnumerator, null, state.Interfaces[1], new Block(statements));
      getEnumerator.ThisParameter = ThisParameter;
      getEnumerator.CallingConvention = CallingConventionFlags.HasThis;
      getEnumerator.Flags = MethodFlags.Public|MethodFlags.Virtual;
      state.Members.Add(getEnumerator);
      //IEnumerable.GetEnumerator
      statements = new StatementList(1);
      ThisParameter = new This(state);
      statements.Add(new Return(new BinaryExpression(new MethodCall(new MemberBinding(ThisParameter, Runtime.MemberwiseClone), null),
        new Literal(state,SystemTypes.Type), NodeType.Castclass)));
      getEnumerator = new Method(state, null, StandardIds.IEnumerableGetEnumerator, null, SystemTypes.IEnumerator, new Block(statements));
      getEnumerator.ThisParameter = ThisParameter;
      getEnumerator.ImplementedInterfaceMethods = new MethodList(Runtime.GetEnumerator);
      getEnumerator.CallingConvention = CallingConventionFlags.HasThis;
      getEnumerator.Flags = MethodFlags.Private|MethodFlags.Virtual|MethodFlags.SpecialName;
      state.Members.Add(getEnumerator);
      if (this.currentClosureLocal != null && this.currentClosureLocal.Type != null && this.currentClosureLocal.Type.Template == state) {
        TypeNode stateInstance = this.currentClosureLocal.Type;
        MemberList members = stateInstance.Members;
        stateInstance.Members = null; //Force specialization of new members
        MemberList newMembers = stateInstance.Members;
        for (int i = 0, n = members.Count; i < n; i++){
          Member oldMember = members[i];
          for (int j = 0, m = newMembers.Count; j < m; j++ ) {
            if (oldMember.Name.UniqueIdKey == newMembers[j].Name.UniqueIdKey) {
              newMembers[j] = oldMember;
              break;
            }
          }
        }
      }
      //exit iteration scope & return
      this.currentClosureLocal = savedCurrentClosureLocal;
      this.currentReturnLabel = savedCurrentReturnLabel;
      this.currentReturnLocal = savedCurrentReturnLocal;
      this.currentIteratorValue = savedCurrValue;
      this.currentIteratorEntryPoint = savedCurrEntryPoint;
      this.iteratorEntryPoints = savedEntryPoints;
      return this.currentClosureLocal;
    }
    public override Expression VisitConstructTuple(ConstructTuple consTuple){
      if (consTuple == null || consTuple.Type == null) return null;
      FieldList fields = consTuple.Fields;
      MemberList members = this.GetTypeView(consTuple.Type).Members;
      int n = fields == null ? 0 : fields.Count;
      StatementList statements = new StatementList(n+1);
      Block b = new Block(statements);
      Local loc = new Local(consTuple.Type);
      for (int i = 0; i < n; i++){
        Field f = fields[i];
        if (f == null) continue;
        Expression init = this.VisitExpression(f.Initializer);
        if (init == null) continue;
        f = members[i] as Field;
        if (f == null) continue;
        statements.Add(new AssignmentStatement(new MemberBinding(new UnaryExpression(loc, NodeType.AddressOf, loc.Type.GetReferenceType()), f), init));
      }
      statements.Add(new ExpressionStatement(loc));
      return new BlockExpression(b, consTuple.Type);
    }
    public override DelegateNode VisitDelegateNode(DelegateNode delegateNode){
      delegateNode = base.VisitDelegateNode (delegateNode);
      if (delegateNode == null) return null;
      MemberList mems = delegateNode.GetMembersNamed(StandardIds.Invoke);
      Method invoke = mems == null ? null : mems.Count != 1 ? null : mems[0] as Method;
      mems = delegateNode.GetMembersNamed(StandardIds.EndInvoke);
      Method endInvoke = mems == null ? null : mems.Count != 1 ? null : mems[0] as Method;
      AttributeList returnAttributes = this.ExtractAttributes(delegateNode.Attributes, AttributeTargets.ReturnValue);
      if (invoke != null) {
        invoke.ReturnAttributes = returnAttributes;
      }
      if (endInvoke != null)
      {
        invoke.ReturnAttributes = returnAttributes;
      }
      return delegateNode;
    }
    public virtual AttributeList ExtractAttributes(AttributeList attributes, AttributeTargets target){
      if (attributes == null){Debug.Assert(false); return null;}
      AttributeList result = null;
      for (int i = 0, n = attributes.Count; i < n; i++){
        AttributeNode attribute = attributes[i];
        if (attribute == null) continue;
        if ((attribute.Target & target) == 0) continue;
        if (result == null) result = new AttributeList();
        result.Add(attribute);
        attributes[i] = null;
      }
      return result;
    }
    public override Statement VisitDoWhile(DoWhile doWhile){
      if (doWhile == null) return null;
      StatementList statements = new StatementList(4);
      Block doWhileBlock = new Block(statements);
      doWhileBlock.SourceContext = doWhile.SourceContext;
      Block endOfLoop = new Block(null);
      Block conditionStart = new Block(null);
      this.continueTargets.Add(conditionStart);
      this.exitTargets.Add(endOfLoop);
      this.VisitBlock(doWhile.Body);
      ExpressionList invariants = doWhile.Invariants;
      if (invariants != null && invariants.Count > 0)
        statements.Add(VisitLoopInvariants(invariants));
      statements.Add(doWhile.Body);
      statements.Add(conditionStart);
      if (doWhile.Condition != null)
        statements.Add(this.VisitBranchCondition(doWhile.Condition, doWhileBlock, doWhile.Condition.SourceContext));
      statements.Add(endOfLoop);
      this.continueTargets.Count--;
      this.exitTargets.Count--;
      return doWhileBlock;
    }
    public override EnsuresExceptional VisitEnsuresExceptional(EnsuresExceptional exceptional) {
      if (exceptional == null) return null;
      {
        #region Generate the runtime code for checking the postcondition
        Expression en = exceptional.PostCondition;
        if (en == null) return exceptional;
        StatementList exsuresStatements = new StatementList();
        Block exsuresBlock = new Block(exsuresStatements);
        exsuresBlock.HasLocals = true;

        // Code generation for postconditions needs to be such that the
        // data flow analysis will "see" the consequences. If the value
        // of the postcondition is assigned to a local, then the information
        // is lost.
        //
        // for the throws clause: throws (E e) ensures Q(e) 
        // generate:
        //
        // ... at beginning of all exceptional postconditions ...
        // ... generated in VisitMethod ...
        // catch (Exception caught_exception){
        //
        //     E e = caught_exception as E;
        //     if (e == null || en) goto L;
        //     throw new PostConditionException(...);
        //     L: nop
        //
        // ... at end of all exceptional postconditions ...
        // ... generated in VisitMethod ...
        // rethrow
        //
        // The reason for generating the if test this way is to avoid trying to branch out
        // of the catch block

        Block post_holds = new Block(new StatementList(new Statement(NodeType.Nop)));

        #region Evaluate the postcondition within a try block. If an exception happens during evaluation, throw a wrapped exception.
        bool noAllocationAllowed = this.currentMethod.GetAttribute(SystemTypes.BartokNoHeapAllocationAttribute) != null;
        Local exceptionDuringPostCondition = new Local(Identifier.For("SS$exceptionDuringPostCondition"),SystemTypes.Exception);
        Local objectExceptionDuringPostCondition = new Local(Identifier.For("SS$objectExceptionDuringPostCondition"),SystemTypes.Exception);
        Expression cond = exceptional.PostCondition;
        string condition = cond != null && cond.SourceContext.SourceText != null && cond.SourceContext.SourceText.Length > 0 ?
          cond.SourceContext.SourceText : "<unknown condition>";
        Expression ec2;
        Expression ec3;
        if (noAllocationAllowed) {
          ec2 = new MemberBinding(null, SystemTypes.PreAllocatedExceptions.GetField(Identifier.For("InvalidContract")));
          ec3 = new MemberBinding(null, SystemTypes.PreAllocatedExceptions.GetField(Identifier.For("InvalidContract")));
        }
        else {
          MemberBinding excBinding2 = new MemberBinding(null, SystemTypes.EnsuresException.GetConstructor(SystemTypes.String, SystemTypes.Exception));
          MemberBinding excBinding3 = new MemberBinding(null, SystemTypes.EnsuresException.GetConstructor(SystemTypes.String));
          string msg2 = "Exception occurred during evaluation of postcondition '" + condition + "' in method '" + currentMethod.FullName + "'";
          ec2 = new Construct(excBinding2, new ExpressionList(new Literal(msg2, SystemTypes.String), exceptionDuringPostCondition));
          ec3 = new Construct(excBinding3, new ExpressionList(new Literal(msg2, SystemTypes.String)));
        }
        #endregion

        #region Throw an exception if the value of the postcondition was false
        Expression thrownException;
        if (noAllocationAllowed) {
          thrownException = new MemberBinding(null, SystemTypes.PreAllocatedExceptions.GetField(Identifier.For("Ensures")));
        }
        else {
          MemberBinding excBinding = new MemberBinding(null, SystemTypes.EnsuresException.GetConstructor(SystemTypes.String));
          Construct ec = new Construct(excBinding, new ExpressionList());
          string msg = "Exceptional postcondition '" + condition + "' violated from method '" + currentMethod.FullName + "'";
          ec.Operands.Add(new Literal(msg, SystemTypes.String));
          thrownException = ec;
        }
        Throw t2 = new Throw(thrownException,exceptional.SourceContext);
        #endregion


        exceptional.Variable = this.VisitExpression(exceptional.Variable);
        #region Create a local to hold the exception
        Local e;
        if (exceptional.Variable == null) {
          e = new Local(Identifier.Empty,exceptional.Type,exsuresBlock);
        } else {
          e = (Local)exceptional.Variable;
        }
        #endregion
        // E e = caught_exception as E;
        AssignmentStatement castToThrowType = new AssignmentStatement(e,
          new BinaryExpression(this.currentExceptionalTerminationException,
          new Literal(exceptional.Type, SystemTypes.Type), NodeType.Isinst, exceptional.Type));
        exsuresStatements.Add(castToThrowType);
        // e == null || Q(e)
        Expression disjunction = new BinaryExpression(
          new BinaryExpression(e, Literal.Null, NodeType.Eq),
          en,
          NodeType.LogicalOr);
        exsuresStatements.Add(new If(disjunction, new Block(new StatementList(new Branch(null, post_holds))), null));
        exsuresStatements.Add(t2);
        exsuresStatements.Add(post_holds);
        exsuresBlock = this.VisitBlock(exsuresBlock);

        this.currentContractExceptionalTerminationChecks.Add(exsuresBlock);
        #endregion
        return exceptional;
      }
    }
    public override EnsuresNormal VisitEnsuresNormal(EnsuresNormal normal) {
      if (normal == null) return null;
      {
        #region Generate the runtime code for checking the postcondition
        Expression en = normal.PostCondition;
        if (en == null) return normal;
        SourceContext sc = en.SourceContext;
    
        // Code generation for postconditions needs to be such that the
        // data flow analysis will "see" the consequences. If the value
        // of the postcondition is assigned to a local, then the information
        // is lost.
        //
        // try {
        //   if en goto post_holds;
        // }
        // catch { throw new ErrorDuringPostConditionEvaluation(...); }
        // throw new PostConditionException(...);
        // post_holds: nop

        Block postConditionBlock = new Block(new StatementList());

        #region Evaluate the postcondition within a try block. If an exception happens during evaluation, throw a wrapped exception.
        bool noAllocationAllowed = this.currentMethod.GetAttribute(SystemTypes.BartokNoHeapAllocationAttribute) != null;
        Local exceptionDuringPostCondition = new Local(Identifier.For("SS$exceptionDuringPostCondition"),SystemTypes.Exception);
        Local objectExceptionDuringPostCondition = new Local(Identifier.For("SS$objectExceptionDuringPostCondition"),SystemTypes.Object);
        Expression cond = normal.PostCondition;
        string condition = cond != null && cond.SourceContext.SourceText != null && cond.SourceContext.SourceText.Length > 0 ?
          cond.SourceContext.SourceText : "<unknown condition>";
        Expression ec2;
        Expression ec3;
        if (noAllocationAllowed) {
          ec2 = new MemberBinding(null, SystemTypes.PreAllocatedExceptions.GetField(Identifier.For("InvalidContract")));
          ec3 = new MemberBinding(null, SystemTypes.PreAllocatedExceptions.GetField(Identifier.For("InvalidContract")));
        }
        else {
          MemberBinding excBinding2 = new MemberBinding(null, SystemTypes.InvalidContractException.GetConstructor(SystemTypes.String, SystemTypes.Exception));
          MemberBinding excBinding3 = new MemberBinding(null, SystemTypes.InvalidContractException.GetConstructor(SystemTypes.String));
          string msg2 = "Exception occurred during evaluation of postcondition '" + condition + "' in method '" + currentMethod.FullName + "'";
          ec2 = new Construct(excBinding2, new ExpressionList(new Literal(msg2, SystemTypes.String), exceptionDuringPostCondition));
          ec3 = new Construct(excBinding3, new ExpressionList(new Literal(msg2, SystemTypes.String)));
        }
        #endregion

        #region Throw an exception if the value of the postcondition was false
        Block post_holds = new Block(new StatementList(new Statement(NodeType.Nop)));

        Expression thrownException;
        if (noAllocationAllowed) {
          thrownException = new MemberBinding(null, SystemTypes.PreAllocatedExceptions.GetField(Identifier.For("Ensures")));
        }
        else {
          MemberBinding excBinding = new MemberBinding(null, SystemTypes.EnsuresException.GetConstructor(SystemTypes.String));
          Construct ec = new Construct(excBinding, new ExpressionList());
          string msg = "Postcondition '" + condition + "' violated from method '" + currentMethod.FullName + "'";
          ec.Operands.Add(new Literal(msg, SystemTypes.String));
          thrownException = ec;
        }
        Throw t = new Throw(thrownException,sc);

        postConditionBlock.Statements.Add(new If(en,new Block(new StatementList(new Branch(null,post_holds))), null));
        postConditionBlock.Statements.Add(t);
        postConditionBlock.Statements.Add(post_holds);
        #endregion
        postConditionBlock = this.VisitBlock(postConditionBlock);
        this.currentContractNormalTerminationCheck.Statements.Add(postConditionBlock);

        #endregion
        return normal;
      }
    }
    public override Statement VisitExpressionStatement(ExpressionStatement statement){
      if (statement == null) return null;
      Expression e = statement.Expression;
      if (e == null) return null;
      if (e.NodeType == NodeType.AssignmentExpression)
        return (Statement)this.Visit(((AssignmentExpression)e).AssignmentStatement);
      statement.Expression = e = this.VisitExpression(statement.Expression);
      if (e == null) return null;
      //TODO: require e.Type to be non null
      if (e.Type != SystemTypes.Void && !(e is UnaryExpression && e.NodeType == NodeType.Pop)) {        
        statement.Expression = new UnaryExpression(e, NodeType.Pop);
      }
      return statement;
    }
    public override Statement VisitIf(If If){
      if (If == null) return null;
      Block endIf = (Block)this.EndIfLabel[If.UniqueKey];
      bool emitLabel = endIf == null;
      if (emitLabel) endIf = new Block(null);
      SourceContext ctx = If.TrueBlock.SourceContext;
      ctx.StartPos = ctx.EndPos;
      if (If.FalseBlock != null && If.FalseBlock.Statements != null && If.FalseBlock.Statements.Count > 0){
        If elseIf = If.FalseBlock.Statements[0] as If;
        if (elseIf != null && If.FalseBlock.Statements.Count == 1){
          this.EndIfLabel[elseIf.UniqueKey] = endIf;
        }
        if (emitLabel){
          Statement nop = new Statement(NodeType.Nop);
          nop.SourceContext = If.EndIfContext;
          If.FalseBlock.Statements.Add(nop);
        }
      }
      Block fBlock = If.FalseBlock;
      if (fBlock == null) fBlock = new Block();
      SourceContext ifctx = If.SourceContext; 
      ifctx.EndPos = If.ConditionContext.EndPos;
      Statement branch = this.VisitAndInvertBranchCondition(If.Condition, fBlock, ifctx);
      this.VisitBlock(If.TrueBlock);
      this.VisitBlock(If.FalseBlock);
      StatementList statements = new StatementList(5);
      Block block = new Block(statements);
      block.SourceContext = If.SourceContext;
      statements.Add(branch);
      statements.Add(If.TrueBlock);
      if (If.FalseBlock != null)
        statements.Add(new Branch(null, endIf, If.ElseContext));
      statements.Add(fBlock);
      if (emitLabel) statements.Add(endIf);
      return block;
    }
    public virtual StatementList SerializeAssertion(Module currentModule, Expression condition, string message, SourceContext sourceContext, string methodName){
      Method method;
      bool useTwoArgumentCalls = true;
      if (methodName == "AssumeStatement") {
        method = this.GetTypeView(SystemTypes.AssertHelpers).GetMethod(Identifier.For(methodName), SystemTypes.String);
      } else {
        method = this.GetTypeView(SystemTypes.AssertHelpers).GetMethod(Identifier.For(methodName), SystemTypes.String, SystemTypes.String);
        #region Delete when LKG > 7301
        if (method == null) {
          method = this.GetTypeView(SystemTypes.AssertHelpers).GetMethod(Identifier.For(methodName), SystemTypes.String);
          useTwoArgumentCalls = false;
        }
        #endregion
      }
      ExpressionList el = Checker.SplitConjuncts(condition);
      StatementList sl = new StatementList();
      if (method == null) return sl;
      for (int j = 0, m = el.Count; j < m; j++) {
        Expression e_prime = el[j];
        ContractSerializer serializer = new ContractSerializer(currentModule, this.currentClosureLocal);
        serializer.Visit(e_prime);
        string conditionText = serializer.SerializedContract;
        ExpressionList args = new ExpressionList(new Literal(conditionText, SystemTypes.String));
        if (methodName != "AssumeStatement" && useTwoArgumentCalls) { // assume is a unary method
          if (string.IsNullOrEmpty(message)){
            // Can't have the source context passed in to the method in case the expression has been
            // split into different top level conjuncts.
            if (e_prime.SourceContext.Document != null) {
              args.Add(new Literal(e_prime.SourceContext.SourceText, SystemTypes.String));
            } else {
              args.Add(new Literal("Unknown source context", SystemTypes.String));
            }
          } else {
            args.Add(new Literal(message, SystemTypes.String));
          }
        }
        sl.Add(new ExpressionStatement(new MethodCall(new MemberBinding(null, method), args, NodeType.Call, SystemTypes.Void, sourceContext), sourceContext));
      }
      return sl;
    }
    public virtual Block VisitLoopInvariants(ExpressionList invariants){
      int m = invariants.Count;
      StatementList statements = new StatementList(m + 1);
      if (!(this.currentCompilation != null && this.currentCompilation.CompilerParameters is CompilerOptions && ((CompilerOptions)this.currentCompilation.CompilerParameters).DisableInternalContractsMetadata)){
        for (int i = 0; i < m; i++) {
          Expression invariant = invariants[i];
          if (invariant != null){
            Assertion assertion = new Assertion(invariant);
            assertion.SourceContext = invariant.SourceContext;
            foreach (Statement s in this.SerializeAssertion(this.currentModule, assertion.Condition, null, assertion.SourceContext, "LoopInvariant")) {
              statements.Add(s);
            }
          }
        }
      }
      if (!(this.currentCompilation != null && this.currentCompilation.CompilerParameters is CompilerOptions && ((CompilerOptions)this.currentCompilation.CompilerParameters).DisableInternalChecks)){
        StatementList checks = new StatementList(m);
        for (int i = 0; i < m; i++){
          Expression invariant = invariants[i];
          if (invariant != null){
            checks.Add(this.CreateAssertionCheckingCode(invariant, "AssertLoopInvariant"));
          }
        }
        statements.Add(this.MarkAsInstrumentationCodeNormalized(this.currentMethod, new Block(checks)));
      }
      return new Block(statements);
    }
    public override Statement VisitFor(For For){
      if (For == null) return null;
      StatementList statements = new StatementList(6);
      Block forBlock = new Block(statements);
      forBlock.SourceContext = For.SourceContext;
      int n = For.Incrementer.Count;
      StatementList incrStatements = new StatementList(n);
      Block incrBlock = new Block(incrStatements);
      this.continueTargets.Add(incrBlock);
      Block endOfLoop = new Block(null);
      this.exitTargets.Add(endOfLoop);
      this.VisitStatementList(For.Initializer);
      Statement forCondition = null;
      if (For.Condition != null)
        forCondition = this.VisitAndInvertBranchCondition(For.Condition, endOfLoop, For.Condition.SourceContext);
      For.Incrementer = this.VisitStatementList(For.Incrementer);
      this.VisitBlock(For.Body);
      statements.Add(new Block(For.Initializer));
      StatementList conditionStatements = new StatementList(1);
      Block condition = new Block(conditionStatements);
      statements.Add(condition);
      ExpressionList invariants = For.Invariants;
      if (invariants != null && invariants.Count > 0)
        conditionStatements.Add(VisitLoopInvariants(invariants));
      if (forCondition != null) conditionStatements.Add(forCondition);
      statements.Add(For.Body);
      for (int i = 0; i < n; i++) incrStatements.Add(For.Incrementer[i]);
      statements.Add(incrBlock);
      statements.Add(new Branch(null, condition));   
      statements.Add(endOfLoop);
      this.continueTargets.Count--;
      this.exitTargets.Count--;
      return forBlock;
    }
    public override Statement VisitForEach(ForEach forEach) {
      if (forEach == null || forEach.Body == null) return null;
      //First transform
      Block continueTarget = new Block(new StatementList(0));
      Block exitTarget = new Block(new StatementList(0));
      StatementList statements = new StatementList(16);
      StatementList generatedInvariants = new StatementList(1);
      Block result = new Block(statements);
      result.HasLocals = true;
      //initialize and update induction variable
      if (forEach.InductionVariable != null) {
        TypeNode inductionVariableType = ((OptionalModifier)forEach.InductionVariable.Type).ModifiedType;
        TypeNode inductionVariableTypeView = this.GetTypeView(inductionVariableType);
        statements.Add(new AssignmentStatement(forEach.InductionVariable, new Construct(new MemberBinding(null, inductionVariableTypeView.GetConstructor()), null, inductionVariableType)));
        forEach.Body.Statements.Add(new ExpressionStatement(new MethodCall(new MemberBinding(forEach.InductionVariable, inductionVariableTypeView.GetMethod(StandardIds.Add, forEach.TargetVariableType)), new ExpressionList(forEach.TargetVariable), NodeType.Call, SystemTypes.Void)));
      }
      //get enumerator. Either call getEnumerator, or use the object itself. 
      CollectionEnumerator cEnumerator = forEach.SourceEnumerable as CollectionEnumerator;
      if (cEnumerator == null || cEnumerator.Collection == null) return null;
      TypeNode tt = forEach.TargetVariable == null || forEach.TargetVariable.Type == null ? null : forEach.TargetVariable.Type.Template;
      bool suppressNullElements = tt != null && tt == SystemTypes.GenericNonNull;
      Expression enumerator;
      Expression length = null;
      Expression index = null;
      BlockScope tempScope = forEach.ScopeForTemporaryVariables;
      Method getEnumerator = cEnumerator.GetEnumerator;
      if (getEnumerator != null && this.currentMethod.Scope != null && this.currentMethod.Scope.CapturedForClosure) {
        TypeNode geDT = this.currentMethod.Scope.FixTypeReference(getEnumerator.DeclaringType);
        if (geDT != getEnumerator.DeclaringType) {
          getEnumerator = cEnumerator.GetEnumerator = geDT.GetMethod(getEnumerator.Name);
        }
      }
      Identifier id = null;

      TypeNode collectionType = cEnumerator.Collection.Type;
      ArrayType arrType = null;
      if (collectionType != null) {
        arrType = this.typeSystem.GetUnderlyingType(collectionType) as ArrayType;
        if (arrType != null) {
          collectionType = arrType;
        }
      }
      if (this.currentMethod.Scope != null && this.currentMethod.Scope.CapturedForClosure) 
        collectionType = this.currentMethod.Scope.FixTypeReference(collectionType);

      if (getEnumerator != null) {
        Local ctemp = new Local(Identifier.Empty, collectionType, result);
        Expression e = this.VisitExpression(cEnumerator.Collection);
        if (e.Type is Reference)
          e = new AddressDereference(e, arrType);
        AssignmentStatement assignCollection = new AssignmentStatement(ctemp, e);
        assignCollection.SourceContext = forEach.SourceContext;
        assignCollection.SourceContext.EndPos = forEach.SourceContext.StartPos + this.foreachLength;
        statements.Add(assignCollection);


        if (!ctemp.Type.IsValueType && forEach.StatementTerminatesNormallyIfEnumerableIsNull)
          statements.Add(new Branch(new UnaryExpression(this.Box(ctemp), NodeType.LogicalNot), exitTarget));
        MemberBinding mb = new MemberBinding(ctemp, getEnumerator);
        if (ctemp.Type.IsValueType)
          mb.TargetObject = new UnaryExpression(ctemp, NodeType.AddressOf, ctemp.Type.GetReferenceType());
        else if (ctemp.Type is ITypeParameter)
          mb.TargetObject = this.Box(ctemp);
        MethodCall callGetEnumerator = new MethodCall(mb, null, getEnumerator.IsVirtualAndNotDeclaredInStruct ? NodeType.Callvirt : NodeType.Call);
        callGetEnumerator.Type = getEnumerator.ReturnType;
        callGetEnumerator.SourceContext = cEnumerator.Collection.SourceContext;
        id = Identifier.For("foreachEnumerator: " +forEach.GetHashCode());
        if (tempScope.CapturedForClosure) {
          Field f = new Field(tempScope, null, FieldFlags.CompilerControlled | FieldFlags.SpecialName, id, this.currentMethod.Scope.FixTypeReference(getEnumerator.ReturnType), null);
          if (f.Type != SystemTypes.String) { // Should be any immutable type, but string is the only one so far!
            f.Attributes.Add(
              new AttributeNode(new MemberBinding(null, SystemTypes.PeerAttribute.GetConstructor()),null, AttributeTargets.Field));
          }
          enumerator = this.VisitMemberBinding(new MemberBinding(new ImplicitThis(), f));
        } else
          enumerator = new Local(id, getEnumerator.ReturnType, result);
        if (enumerator == null) return null;
        statements.Add(new AssignmentStatement(enumerator, callGetEnumerator, NodeType.Nop, callGetEnumerator.SourceContext));
        if (!enumerator.Type.IsValueType && forEach.StatementTerminatesNormallyIfEnumeratorIsNull)
          statements.Add(new Branch(new UnaryExpression(enumerator, NodeType.LogicalNot), exitTarget));

        CompilerOptions options = this.currentCompilation == null ? null : this.currentCompilation.CompilerParameters as CompilerOptions;
        if (options != null && !options.DisableInternalContractsMetadata && !options.DisableGuardedClassesChecks && !tempScope.CapturedForClosure) {
          // Add loop invariants that the compiler knows about because it is generating the code
          // Don't generate any runtime checks for them, just the serialized form the static verification needs to see
          //
          // Don't do this for value types: they can't have invariants anyway (and translation fails for box expressions anyway)
          if (!enumerator.Type.IsValueType) {
            MemberList ms = SystemTypes.Guard.GetMembersNamed(Runtime.IsPeerConsistentId);
            if (ms != null && ms.Count == 1) {
              Method isConsistent = (Method)ms[0];
              Expression arg = enumerator;
              if (arg.NodeType == NodeType.Local) {
                Local l = (Local)arg;
                // all user-written locals are represented as memberbindings of a particular form
                // Boogie has built that assumption in so that the only true "Locals" that it sees
                // it considers special, like "return value".
                Field f = new Field(forEach.ScopeForTemporaryVariables, null, FieldFlags.CompilerControlled, l.Name, l.Type, Literal.Null);
                arg = new MemberBinding(null, f);
              }
              MethodCall mc = new MethodCall(new MemberBinding(null, isConsistent), new ExpressionList(arg), NodeType.Call, SystemTypes.Boolean);
              Assertion assertion = new Assertion(mc);
              assertion.SourceContext = assignCollection.SourceContext;
              foreach (Statement s in this.SerializeAssertion(this.currentModule, mc, "Foreach enumerator must be peer consistent.", assignCollection.SourceContext, "LoopInvariant")) {
                generatedInvariants.Add(s);
              }
            }
          }
        }

      } else if (cEnumerator.MoveNext == null) {
        Identifier indexId = Identifier.For("foreachIndex: " + forEach.GetHashCode());
        if (tempScope.CapturedForClosure) {
          id = Identifier.For("foreachLength: " + forEach.GetHashCode());
          Field f = new Field(tempScope, null, FieldFlags.CompilerControlled, id, SystemTypes.Int32, null);
          length = this.VisitMemberBinding(new MemberBinding(new ImplicitThis(), f));
          f = new Field(tempScope, null, FieldFlags.CompilerControlled, indexId, SystemTypes.Int32, null);
          index = this.VisitMemberBinding(new MemberBinding(new ImplicitThis(), f));
          id = Identifier.For("foreachEnumerator: " + forEach.GetHashCode());
          f = new Field(tempScope, null, FieldFlags.CompilerControlled | FieldFlags.SpecialName, id, collectionType, null);
          enumerator = this.VisitMemberBinding(new MemberBinding(new ImplicitThis(), f, cEnumerator.SourceContext));
          if (enumerator == null) return null;  // this is happening when foreach encloses anon delegate. just fixing the crash.
        } else {
          length = new Local(Identifier.Empty, SystemTypes.Int32);
          index = new Local(indexId, SystemTypes.Int32, result);
          enumerator = new Local(Identifier.Empty, collectionType, cEnumerator.SourceContext);
        }

        if (!(this.currentCompilation != null && this.currentCompilation.CompilerParameters is CompilerOptions && ((CompilerOptions)this.currentCompilation.CompilerParameters).DisableInternalContractsMetadata)) {
          // Add loop invariants that the compiler knows about because it is generating the code
          // Don't generate any runtime checks for them, just the serialized form the static verification needs to see
          //
          Expression arg = index;
          if (index.NodeType == NodeType.Local) {
            Local l = (Local)index;
            // all user-written locals are represented as memberbindings of a particular form
            // Boogie has built that assumption in so that the only true "Locals" that it sees
            // it considers special, like "return value".
            Field f = new Field(forEach.ScopeForTemporaryVariables, null, FieldFlags.CompilerControlled, l.Name, l.Type, Literal.Null);
            arg = new MemberBinding(null, f);
          }
          Assertion assertion = new Assertion(new BinaryExpression(arg, new Literal(0, SystemTypes.Int32), NodeType.Ge, SystemTypes.Boolean));
          assertion.SourceContext = forEach.SourceContext;
          assertion.SourceContext.EndPos = forEach.SourceContext.StartPos + this.foreachLength;
          foreach (Statement s in this.SerializeAssertion(this.currentModule, assertion.Condition, "Foreach loop index must be at least zero.", assertion.SourceContext, "LoopInvariant")) {
            generatedInvariants.Add(s);
          }
        }

        Expression e = this.VisitExpression(cEnumerator.Collection);
        if (e.Type is Reference)
          e = new AddressDereference(e, arrType);
        AssignmentStatement assignEnumerator = new AssignmentStatement(enumerator, e);
        assignEnumerator.SourceContext = forEach.SourceContext;
        if (forEach.SourceContext.StartPos != cEnumerator.SourceContext.StartPos || forEach.SourceContext.EndPos != cEnumerator.SourceContext.EndPos)
          assignEnumerator.SourceContext.EndPos = forEach.SourceContext.StartPos + this.foreachLength;
        statements.Add(assignEnumerator);
        statements.Add(new Branch(new UnaryExpression(enumerator, NodeType.LogicalNot), exitTarget));
        TypeNode et = this.ForeachArrayElementType(cEnumerator);
        if (et != null)
          statements.Add(new AssignmentStatement(length, new UnaryExpression(new UnaryExpression(enumerator, NodeType.Ldlen, SystemTypes.IntPtr), NodeType.Conv_I4,SystemTypes.Int32)));
        else {
          Debug.Assert(false);
          return null;
        }
        statements.Add(new AssignmentStatement(index, new Literal(0, SystemTypes.Int32)));
      } else {
        Field f = null;
        id = Identifier.For("foreachEnumerator: " + forEach.GetHashCode());
        if (tempScope.CapturedForClosure) {
          f = new Field(tempScope, null, FieldFlags.Private | FieldFlags.SpecialName, id, collectionType, null);
          enumerator = this.VisitMemberBinding(new MemberBinding(new ImplicitThis(), f));
          if (enumerator == null) return null;  // this can happen. See above for similar statement
        } else {
          f = new Field(tempScope, null, FieldFlags.Private | FieldFlags.SpecialName, id, collectionType, null);
          enumerator = this.VisitMemberBinding(new MemberBinding(new ImplicitThis(), f));
          //enumerator = new Local(id, collectionType);
        }
        AssignmentStatement assignEnumerator = new AssignmentStatement(enumerator, this.VisitExpression(cEnumerator.Collection));
        assignEnumerator.SourceContext = forEach.SourceContext;
        if (forEach.SourceContext.StartPos != cEnumerator.SourceContext.StartPos || forEach.SourceContext.EndPos != cEnumerator.SourceContext.EndPos)
          assignEnumerator.SourceContext.EndPos = forEach.SourceContext.StartPos + this.foreachLength;
        statements.Add(assignEnumerator);
        if (!enumerator.Type.IsValueType)
          statements.Add(new Branch(new UnaryExpression(enumerator, NodeType.LogicalNot), exitTarget));

        if (!(this.currentCompilation != null && this.currentCompilation.CompilerParameters is CompilerOptions && ((CompilerOptions)this.currentCompilation.CompilerParameters).DisableInternalContractsMetadata)) {
          // Add loop invariants that the compiler knows about because it is generating the code
          // Don't generate any runtime checks for them, just the serialized form the static verification needs to see
        }
      }
      //continueTarget
      statements.Add(continueTarget);

      if (generatedInvariants.Count > 0) {
        foreach (Statement s in generatedInvariants)
          statements.Add(s);
      }
      if (forEach.Invariants != null)
        statements.Add(this.VisitLoopInvariants(forEach.Invariants));

      if (length != null) {
        //if index >= length goto exitTarget
        Branch b = (index.Type.IsPrimitiveUnsignedInteger && length.Type.IsPrimitiveUnsignedInteger)?
          new Branch(new BinaryExpression(index, length, NodeType.Ge), exitTarget, exitTarget.SourceContext, true):
          new Branch(new BinaryExpression(index, length, NodeType.Ge), exitTarget);
        if (forEach.TargetVariable != null) {
          b.SourceContext = forEach.TargetVariable.SourceContext;
          if (forEach.SourceEnumerable.SourceContext.EndPos > b.SourceContext.EndPos)
            b.SourceContext.EndPos = forEach.SourceEnumerable.SourceContext.EndPos;
        }
        statements.Add(b);
        this.ForeachBodyHook(forEach, statements, enumerator, index);
        //target = enumerator[index]
        Debug.Assert(cEnumerator.ElementLocal != null);
        Expression target = cEnumerator.ElementLocal;
        ExpressionList args = new ExpressionList(1);
        args.Add(index);
        TypeNode et = this.ForeachArrayElementType(cEnumerator);
        if (et != null) {
          Expression elem = new Indexer(enumerator, args, et);
          if (et.IsValueType && !et.IsPrimitive)
            elem = new AddressDereference(new UnaryExpression(elem, NodeType.AddressOf, elem.Type.GetReferenceType()), et);
          AssignmentStatement indexIntoArray = new AssignmentStatement(target, elem);
          indexIntoArray.SourceContext = forEach.TargetVariable.SourceContext;
          if (forEach.SourceEnumerable.SourceContext.EndPos > indexIntoArray.SourceContext.EndPos)
            indexIntoArray.SourceContext.EndPos = forEach.SourceEnumerable.SourceContext.EndPos;
          statements.Add(indexIntoArray);
        }else{
          Debug.Assert(false);
          return null;
        }
        statements.Add(new AssignmentStatement(index, new BinaryExpression(index, new Literal(1, SystemTypes.Int32), NodeType.Add)));
      } else {
        //if !enumerator.MoveNext() goto exitTarget
        Method moveNext = cEnumerator.MoveNext;
        MemberBinding mb = new MemberBinding(enumerator, moveNext);
        MethodCall callMoveNext = new MethodCall(mb, null, moveNext.IsVirtualAndNotDeclaredInStruct ? NodeType.Callvirt : NodeType.Call);
        callMoveNext.Type = SystemTypes.Boolean;
        if (this.useGenerics && mb.TargetObject != null && mb.TargetObject.Type is ITypeParameter) {
          callMoveNext.Constraint = mb.TargetObject.Type;
          mb.TargetObject = new UnaryExpression(mb.TargetObject, NodeType.AddressOf, mb.TargetObject.Type.GetReferenceType());
        } else if (mb.TargetObject.Type.IsValueType) {
          mb.TargetObject = new UnaryExpression(mb.TargetObject, NodeType.AddressOf, mb.TargetObject.Type.GetReferenceType());
        }
        Branch b = new Branch(new UnaryExpression(callMoveNext, NodeType.LogicalNot), exitTarget);
        if (forEach.TargetVariable != null) {
          b.SourceContext = forEach.TargetVariable.SourceContext;
          if (forEach.SourceEnumerable.SourceContext.EndPos > b.SourceContext.EndPos)
            b.SourceContext.EndPos = forEach.SourceEnumerable.SourceContext.EndPos;
        }
        statements.Add(b);
        //target = enumerator.Current
        Debug.Assert(cEnumerator.GetCurrent != null);
        Method getCurrent = cEnumerator.GetCurrent;
        if (this.currentMethod.Scope != null && this.currentMethod.Scope.CapturedForClosure) {
          TypeNode gcDT = this.currentMethod.Scope.FixTypeReference(getCurrent.DeclaringType);
          if (gcDT != getCurrent.DeclaringType) {
            getCurrent = cEnumerator.GetCurrent = gcDT.GetMethod(getCurrent.Name);
            cEnumerator.ElementLocal.Type = getCurrent.ReturnType;
          }
        }
        mb = new MemberBinding(enumerator, getCurrent);
        MethodCall callGetCurrent = new MethodCall(mb, null, getCurrent.IsVirtualAndNotDeclaredInStruct ?  NodeType.Callvirt : NodeType.Call);
        if (this.useGenerics && mb.TargetObject != null && mb.TargetObject.Type is ITypeParameter) {
          callGetCurrent.Constraint = mb.TargetObject.Type;
          mb.TargetObject = new UnaryExpression(mb.TargetObject, NodeType.AddressOf, mb.TargetObject.Type.GetReferenceType());
        } else if (mb.TargetObject.Type.IsValueType) {
          mb.TargetObject = new UnaryExpression(mb.TargetObject, NodeType.AddressOf, mb.TargetObject.Type.GetReferenceType());
        }
        Debug.Assert(cEnumerator.ElementLocal != null);
        statements.Add(new AssignmentStatement(cEnumerator.ElementLocal, callGetCurrent, forEach.TargetVariable.SourceContext));
        //loop back if element null
        if (suppressNullElements)
          statements.Add(new Branch(new UnaryExpression(cEnumerator.ElementLocal, NodeType.LogicalNot), continueTarget));
      }
      if (forEach.TargetVariable != null) {
        Debug.Assert(cEnumerator.ElementCoercion != null);
        statements.Add(new AssignmentStatement(this.VisitTargetExpression(forEach.TargetVariable),
          this.VisitExpression(cEnumerator.ElementCoercion),forEach.TargetVariable.SourceContext));
      }
      //body
      this.continueTargets.Add(continueTarget);
      this.exitTargets.Add(exitTarget);
      statements.Add(this.VisitBlock(forEach.Body));
      this.continueTargets.Count--;
      this.exitTargets.Count--;
      //loop back
      statements.Add(new Branch(null, continueTarget));
      //exitTarget
      statements.Add(exitTarget);
      return result;
    }
    protected virtual TypeNode ForeachArrayElementType(CollectionEnumerator cenum) {
      ArrayType arrType = this.typeSystem.GetUnderlyingType(cenum.Collection.Type) as ArrayType;
      if (arrType != null && arrType.IsSzArray()) {
        return this.typeSystem.GetUnderlyingType(arrType.ElementType);
      }
      return null;
    }
    protected virtual void ForeachBodyHook(ForEach forEach, StatementList sl, Expression enumerator, Expression index) {
    }
    public override Statement VisitContinue(Continue Continue){
      if (Continue == null) return null;
      int level = Continue.Level != null ? (int)Continue.Level.Value : 0;
      int n = this.continueTargets.Count;
      bool leavesExceptionBlock = false;
      while (level >= 0 && n > 0){
        Statement et = this.continueTargets[--n];
        switch(et.NodeType){
          case NodeType.Block: 
            if (level-- == 0 || n == 0){
              Branch b = new Branch(null, (Block)et, false, false, leavesExceptionBlock);
              b.SourceContext = Continue.SourceContext;
              return b;
            }
            break;
          case NodeType.Try:
            leavesExceptionBlock = true;
            break;
          case NodeType.Finally: //Should not happen if Checker did its job
            n = 0;
            break;
        }
      }
      return new Statement(NodeType.Nop); //TODO: replace with throw
    }
    public override Event VisitEvent(Event evnt){
      evnt = base.VisitEvent (evnt);
      if (evnt == null) return null;
      if (evnt.ImplementedTypes != null && evnt.ImplementedTypes.Count > 0 && evnt.ImplementedTypes[0] != null){
        string typeName = evnt.ImplementedTypes[0].GetFullUnmangledNameWithTypeParameters();
        evnt.Name = new Identifier(typeName + "." + evnt.Name.ToString(), evnt.Name.SourceContext);
      }
      Field bf = evnt.BackingField;
      if (bf != null){
        bf.ReferenceSemantics = ReferenceFieldSemantics.NotComputed;
        AttributeList al = this.ExtractAttributes(evnt.Attributes, AttributeTargets.Field);
        for (int i = 0, n = al == null ? 0 : al.Count; i < n; i++){
          bf.Attributes.Add(al[i]);
        }
      }
      return evnt;
    }
    public override Statement VisitExit(Exit exit){
      if (exit == null) return null;
      int level = exit.Level != null ? (int)exit.Level.Value : 0;
      int n = this.exitTargets.Count;
      bool leavesExceptionBlock = false;
      while (level >= 0 && n > 0){
        Statement et = this.exitTargets[--n];
        switch(et.NodeType){
          case NodeType.Block: 
            if (level-- == 0 || n == 0){
              Branch b = new Branch(null, (Block)et, false, false, leavesExceptionBlock);
              b.SourceContext = exit.SourceContext;
              return b;
            }
            break;
          case NodeType.Try:
            leavesExceptionBlock = true;
            break;
          case NodeType.Finally: //Should not happen if Checker did its job
            n = 0;
            break;
        }
      }
      return new Statement(NodeType.Nop); //TODO: replace with throw
    }
    public override Statement VisitExpose(Expose Expose){
      if (Expose == null) return null;
      if (Expose.Instance == null)
        return this.VisitBlock(Expose.Body);
      TypeNode exposeInstanceType = TypeNode.StripModifiers(Expose.Instance.Type);
      if (exposeInstanceType == null)
        return this.VisitBlock(Expose.Body);
      SourceContext endContext = new SourceContext(Expose.SourceContext.Document, Expose.SourceContext.EndPos - 1, Expose.SourceContext.EndPos);

      string startMethodName = null;
      string endMethodName = null;
      Method startMethod = null;
      Method endMethod = null;

      InvariantList justToForceDeserialization = exposeInstanceType.Contract != null ? exposeInstanceType.Contract.Invariants : null;
      if (exposeInstanceType.Contract == null
        || exposeInstanceType.Contract.FramePropertyGetter == null
        ) {
        // If we're exposing an expression E of type T where T is not a guarded class,
        // then for the sake of downstream analysis tools (such as the Spec# Program Verifier) we emit the following code:
        // 
        // write|expose (E) S [alternatively, "write|expose (E at T) S"]
        //
        // is translated into
        //
        // T target = E;
        // Guard.StartWritingFrame(target, typeof(T)); [alternatively, Guard.StartWritingAtNop]
        // try{
        //   S
        // }finally{
        //   Guard.EndWritingFrame(target, typeof(T)); [alternatively, Guard.EndWritingAtNop]
        // }
        //
        // These methods are no-ops. For this reason,
        // combined with the fact that Boogie considers unchecked exceptions to be the end of the world,
        // we don't need to distinguish between checked and unchecked exceptions here.

        Block block = new Block(new StatementList());
        Local target = new Local(Identifier.Empty, exposeInstanceType, block);

        Literal typeArgument = null;
        if (Expose.IsLocal) {
          startMethodName = "StartWritingAtNop";
          endMethodName = "EndWritingAtNop";
        } else {
          startMethodName = "StartWritingFrame";
          endMethodName = "EndWritingFrame";
        }
        typeArgument = new Literal(exposeInstanceType, SystemTypes.Type);
        startMethod = 
          SystemTypes.Guard.GetMethod(Identifier.For(startMethodName), OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Object), OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Type));
        endMethod = 
          SystemTypes.Guard.GetMethod(Identifier.For(endMethodName), OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Object), OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Type));

        block.Statements.Add(new AssignmentStatement(target, Expose.Instance, Expose.Instance.SourceContext));
        block.Statements.Add(new ExpressionStatement(
          new MethodCall(new MemberBinding(null, startMethod), new ExpressionList(target, new UnaryExpression(typeArgument, NodeType.Typeof)),
          NodeType.Call, SystemTypes.Void), Expose.Instance.SourceContext));
        block.Statements.Add(new Try(
          Expose.Body,
          null,
          null,
          null,
          new Finally(new Block(new StatementList(new ExpressionStatement(new MethodCall(
           new MemberBinding(null, endMethod), new ExpressionList(target, new UnaryExpression(new Literal(exposeInstanceType, SystemTypes.Type), NodeType.Typeof)),
           NodeType.Call, SystemTypes.Void), endContext))))
        ));
        return (Statement) this.Visit(block);
      }

      // write|additive expose (E) S   [alternatively:  expose (E) S]
      //
      // is translated into
      //
      // Guard! rootFrame = E.FrameGuard.StartWritingTransitively(); [alternatively "StartWritingAtTransitively"]
      // Exception exception = null;
      // try {
      //     S
      // } catch (Exception e) {
      //     exception = e;
      //     throw;
      // } finally {
      //     if (exception == null || exception is ICheckedException)
      //         rootFrame.EndWritingTransitively(); [alternatively "EndWritingAtTransitively"]
      // }
      //
      // This is a hack; it would be better to statically have different code paths for
      // the normal completion case and the exceptional completion case.
      // However, that requires transforming returns, gotos, continues, breaks, etc.
      // Of course, all of the above can first be transformed into gotos.
      //
      // The "throw" in the catch clause is needed to allow the definite assignment
      // analysis to know that things assigned to in S are really assigned to
      // in the code following the finally block.
      // More importantly, who are we to eat up an exception, unless the exception
      // is checked and the object invariant doesn't hold.

      TypeNode staticInstanceType = exposeInstanceType;
      if (Expose.IsLocal) {
        startMethodName = "StartWritingAtTransitively";
        endMethodName = "EndWritingAtTransitively";
        startMethod =
          SystemTypes.Guard.GetMethod(Identifier.For(startMethodName), OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Type));
        endMethod =
          SystemTypes.Guard.GetMethod(Identifier.For(endMethodName), OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Type));
      } else {
        if (Expose.NodeType == NodeType.Read) {
          startMethodName = "StartReadingTransitively";
          endMethodName = "EndReadingTransitively";
        } else {
          startMethodName = "StartWritingTransitively";
          endMethodName = "EndWritingTransitively";
        }
        startMethod = SystemTypes.Guard.GetMethod(Identifier.For(startMethodName));
        endMethod = SystemTypes.Guard.GetMethod(Identifier.For(endMethodName));
      }

      TypeNode guardType = OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Guard);
      Method frameGetter = staticInstanceType.Contract.FramePropertyGetter;

      Block newBody = new Block();
      Local rootFrame = new Local(Identifier.For("SS$rootFrame"), guardType, newBody);
      Expression frameGetterCall = new MethodCall(new MemberBinding(Expose.Instance, frameGetter), null);

      // Need two independent argument lists. Otherwise, the processing of the first one disrupts the
      // second one.
      ExpressionList startFrameGetterExprArgs;
      ExpressionList endFrameGetterExprArgs;
      if (Expose.IsLocal) {
        startFrameGetterExprArgs = new ExpressionList(new UnaryExpression(new Literal(exposeInstanceType, SystemTypes.Type), NodeType.Typeof));
        endFrameGetterExprArgs = new ExpressionList(new UnaryExpression(new Literal(exposeInstanceType, SystemTypes.Type), NodeType.Typeof));
      } else {
        startFrameGetterExprArgs = null;
        endFrameGetterExprArgs = null;
      }
      Expression frameGetterExpr = new MethodCall(new MemberBinding(frameGetterCall, startMethod), startFrameGetterExprArgs, NodeType.Call, startMethod.ReturnType);

      Statement startCall = new AssignmentStatement(rootFrame, frameGetterExpr, Expose.Instance.SourceContext);

      // rootFrame.End(Reading|Writing)Transitively();
      Statement endCall = new ExpressionStatement(new MethodCall(new MemberBinding(rootFrame, endMethod), endFrameGetterExprArgs, NodeType.Call, SystemTypes.Void), endContext);
      Local exception = new Local(SystemTypes.Exception);
      CatchList catchList = new CatchList(1);
      Throw rethrow = new Throw();
      rethrow.NodeType = NodeType.Rethrow;
      Local e = new Local(SystemTypes.Exception);
      catchList.Add(new Catch(new Block(new StatementList(new AssignmentStatement(exception, e), rethrow)), e, SystemTypes.Exception));
      newBody.Statements = new StatementList(
        startCall,
        new AssignmentStatement(exception, new Literal(null, SystemTypes.Exception)),
        new Try(
        Expose.Body,
        catchList,
        null, null,
        new Finally(new Block(new StatementList(
        new If(
          new BinaryExpression(
            new BinaryExpression(exception, new Literal(null, SystemTypes.Exception), NodeType.Eq, SystemTypes.Boolean),
            new BinaryExpression(exception, new Literal(SystemTypes.ICheckedException, SystemTypes.Type), NodeType.Is, SystemTypes.Boolean),
            NodeType.LogicalOr,
            SystemTypes.Boolean),
        new Block(new StatementList(endCall)),
        null))))));
      return this.VisitBlock(newBody);
    }

    public override Field VisitField(Field field){
      if (field == null) return null;
      if (field.CciKind != CciMemberKind.Regular){
        field.Attributes.Add(new AttributeNode(new MemberBinding(null, SystemTypes.CciMemberKindAttribute.GetConstructor(SystemTypes.CciMemberKind)), new ExpressionList(new Literal(field.CciKind, SystemTypes.CciMemberKind)), AttributeTargets.Field));
      }
      field.Attributes = this.VisitAttributeList(field.Attributes);
      TypeNode t = this.VisitTypeReference(field.Type);
      //      if (!field.IsVisibleOutsideAssembly) t = this.typeSystem.StripModifiers(t);
      field.Type = t;
      field.DefaultValue = this.VisitLiteral(field.DefaultValue) as Literal;
      if (field.IsLiteral){
        if (field.DefaultValue == null){
          Literal lit = this.VisitExpression(field.Initializer) as Literal;
          if (lit != null){
            field.Initializer = null;
            field.DefaultValue = lit;
          }else{
            field.Flags &= ~(FieldFlags.Literal | FieldFlags.HasDefault);
            field.Flags |= FieldFlags.InitOnly;
          }
        }else
          field.Initializer = null;
      }
      //      if (field.IsSpecialName && field.IsPrivate && field.Type is DelegateNode) //Uncomment this if exact C# compatibility becomes very important
      //        field.Flags &= ~FieldFlags.SpecialName;
      return field;
    }
    public override Block VisitFieldInitializerBlock(FieldInitializerBlock block){
      if (block == null) return null;
      StatementList statements = block.Statements;
      bool isStatic = block.IsStatic;
      Expression thisOb = isStatic ? null : this.currentThisParameter;
      TypeContract contract = block.Type.Contract;
      if (isStatic && contract != null && contract.FrameField != null){
        // class Foo{
        //   Guard @'SpecSharp::frameGuard';
        //
        //   static Guard @'SpecSharp::GetFrameGuard'(object o){
        //     return ((Foo)o).@'SpecSharp::frameGuard';
        //   }
        //
        //   static Foo(){
        //     Guard.RegisterGuardedClass(typeof(Foo), new FrameGuardGetter(@'SpecSharp::GetFrameGuard'));
        //   }
        // }
        if (statements == null) block.Statements = statements = new StatementList(1);
        
        statements.Add(this.MarkAsInstrumentationCodeNormalized(this.currentMethod, new Block(new StatementList(
          new ExpressionStatement(new MethodCall(
          new MemberBinding(null, SystemTypes.Guard.GetMethod(Identifier.For("RegisterGuardedClass"), SystemTypes.Type, SystemTypes.FrameGuardGetter)),
          new ExpressionList(
          new MethodCall(new MemberBinding(null, Runtime.GetTypeFromHandle), new ExpressionList(new UnaryExpression(new Literal(block.Type), NodeType.Ldtoken, SystemTypes.IntPtr)), NodeType.Call, SystemTypes.Type),
          new Construct(
          new MemberBinding(null, SystemTypes.FrameGuardGetter.GetConstructor(SystemTypes.Object, SystemTypes.IntPtr)),
          new ExpressionList(new Literal(null, SystemTypes.Object), new UnaryExpression(new MemberBinding(null, contract.GetFrameGuardMethod), NodeType.Ldftn)), SystemTypes.FrameGuardGetter)), NodeType.Call, SystemTypes.Void))))));
      }
      // only visit this syntactic type's members, not extensions
      TypeNode type = block.Type;
      if (type.PartiallyDefines != null) type = type.PartiallyDefines;
      MemberList members = type.Members;
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
        Field f = members[i] as Field;
        if (f == null) continue;
        if (f.IsStatic != isStatic) continue;
        if (f.Initializer == null || f.DefaultValue != null) continue;
        if (statements == null) block.Statements = statements = new StatementList(n-i+1);
        Expression initialValue = f.Initializer;
        Statement aStat = null;
        MemberBinding target = new MemberBinding(thisOb, f);
        if (f.Type != null && f.Type.IsValueType && initialValue is Local && ((Local)initialValue).Name == StandardIds.NewObj)
          aStat = new AssignmentStatement(new AddressDereference(new UnaryExpression(target, NodeType.AddressOf, target.Type.GetReferenceType()), target.Type), 
            new Literal(null, SystemTypes.Object), f.SourceContext);
        else{
          if (!isStatic){
            // need to create a copy of the initializer for each ctor, don't want to share it
            InitialValueDuplicator duplicator = new InitialValueDuplicator(this.currentModule, this.currentType);
            //TODO: there should be a factory method that can be overridden so that a language can create its own duplicator
            initialValue = duplicator.VisitExpression(initialValue);
          }
          aStat = new AssignmentStatement(target, this.VisitExpression(initialValue), f.SourceContext);
        }
        statements.Add(aStat);
      }
      block.NodeType = NodeType.Block;
      return block;
    }
    class InitialValueDuplicator : Duplicator{
      internal InitialValueDuplicator(Module module, TypeNode type)
        : base(module, type){
      }
      public override Expression VisitAnonymousNestedFunction(AnonymousNestedFunction func){
        return func;
      }
      public override Node VisitUnknownNodeType(Node node){
        return node;
      }
    }
    public override Statement VisitFinally(Finally Finally){
      if (Finally == null) return null;
      this.continueTargets.Add(Finally);
      this.exitTargets.Add(Finally);
      Finally.Block = this.VisitBlock(Finally.Block);
      this.continueTargets.Count--;
      this.exitTargets.Count--;
      return Finally;
    }
    public override Statement VisitFixed(Fixed Fixed){
      if (Fixed == null) return null;
      Block result = new Block(new StatementList());
      LocalDeclarationsStatement locStat = Fixed.Declarators as LocalDeclarationsStatement;
      if (locStat == null) return null;
      LocalDeclarationList locDecList = locStat.Declarations;
      for (int i = 0, n = locDecList == null ? 0 : locDecList.Count; i < n; i++){
        LocalDeclaration locDec = locDecList[i];
        if (locDec == null) continue;
        Field f = locDec.Field;
        if (f == null || f.Initializer == null) continue;
        TypeNode t = f.Type;
        if (t == null) continue;
        bool noPinning = false;  // NOTE: fixed pointer locals have type pinned T& not T*
        Pointer declaredPointerType = t as Pointer;
        if (declaredPointerType != null) {
          UnaryExpression arrayAddressOfElem0 = f.Initializer as UnaryExpression;
          if (arrayAddressOfElem0 != null && arrayAddressOfElem0.Operand is Indexer) {
            Indexer arrayElem0 = (Indexer)arrayAddressOfElem0.Operand;
            if (arrayElem0.ArgumentListIsIncomplete) {
              // Using IsIncomplete is the hacky way in which the Checker transmits to the normalizer that the right
              // hand of the fixed declaration was just an array and that the compiler added &arr[0].
              // Now we need to do 2 more things: check that arr is not null and that arr.Length > 0.
              Expression array = arrayElem0.Object;
              Local arrayLocal = new Local(array.Type);
              Statement assignment = new AssignmentStatement(arrayLocal, array, array.SourceContext);
              arrayElem0.Object = arrayLocal;
              BinaryExpression arrayIsNull = new BinaryExpression(arrayLocal, Literal.Null, NodeType.Eq);
              Expression arrayLength = new UnaryExpression(arrayLocal, NodeType.Ldlen, SystemTypes.Int32);
              BinaryExpression arrayIsEmpty = new BinaryExpression(arrayLength, Literal.Int32Zero, NodeType.Eq);
              TernaryExpression cond1 = new TernaryExpression(arrayIsEmpty, Literal.Null, arrayAddressOfElem0, NodeType.Conditional, t);
              TernaryExpression cond2 = new TernaryExpression(arrayIsNull, Literal.Null, cond1, NodeType.Conditional, t);
              StatementList statements = new StatementList(assignment, new ExpressionStatement(cond2));
              f.Initializer = new BlockExpression(new Block(statements), t);
            }
          } else if (f.Initializer.Type == SystemTypes.String) {
            Debug.Assert(((Pointer)t).ElementType == SystemTypes.Char);
            Local pinnedString = new Local(SystemTypes.String); pinnedString.Pinned = true;
            Statement assignment = new AssignmentStatement(pinnedString, f.Initializer, f.Initializer.SourceContext);
            BinaryExpression stringIsNull = new BinaryExpression(pinnedString, Literal.Null, NodeType.Eq);
            Expression call = new MethodCall(new MemberBinding(null, Runtime.GetOffsetToStringData), null, NodeType.Call);
            Expression add = new BinaryExpression(new UnaryExpression(pinnedString, NodeType.Conv_I), new UnaryExpression(call, NodeType.Conv_I), NodeType.Add);
            TernaryExpression cond = new TernaryExpression(stringIsNull, Literal.Null, add, NodeType.Conditional, t);
            StatementList statements = new StatementList(assignment, new ExpressionStatement(cond));
            f.Initializer = new BlockExpression(new Block(statements), t);
            noPinning = true;
          }
        }
        f.Flags &= ~FieldFlags.InitOnly;
        AssignmentStatement aStat = new AssignmentStatement(new MemberBinding(new ImplicitThis(), f), f.Initializer);
        aStat.SourceContext = f.Initializer.SourceContext;
        result.Statements.Add((Statement)this.Visit(aStat));
        LocalBinding lb = aStat.Target as LocalBinding;
        if (lb != null && !noPinning) {
          f.Type = declaredPointerType.ElementType.GetReferenceType(); // must be declared as a pinned T&
          lb.BoundLocal.Pinned = true;
          lb.BoundLocal.Type = f.Type;
        }
      }
      result.Statements.Add(this.VisitBlock(Fixed.Body));
      return result;
    }
    public override Statement VisitLabeledStatement(LabeledStatement lStatement){
      if (lStatement == null) return null;
      if (lStatement.Statements == null) lStatement.Statements = new StatementList(1);
      if (lStatement.Statement is Try && lStatement.Label != null) 
        lStatement.Statements.Add(new Statement(NodeType.Nop, lStatement.Label.SourceContext));
      lStatement.Statements.Add((Statement)this.Visit(lStatement.Statement));
      lStatement.NodeType = NodeType.Block;
      return lStatement;
    }
    public override Expression VisitLiteral(Literal literal){
      if (literal == null) return null;
      if (this.currentMethod != null && this.currentMethod.Scope != null && this.currentMethod.Scope.CapturedForClosure && literal.Value is TypeNode) {
        TypeNode type = (TypeNode)literal.Value;
        TypeNode fixedType = this.currentMethod.Scope.FixTypeReference(type);
        if (type != fixedType)
          literal = new Literal(fixedType, literal.Type, literal.SourceContext);
      } 
      TypeNode literalType = TypeNode.StripModifiers(literal.Type);
      if (literalType != null && literalType.Template == SystemTypes.GenericBoxed){
        return new Local(literalType);
      }
      if (literalType != SystemTypes.Decimal) {
        if (literal.Type == literalType) return literal;
        return new Literal(literal.Value, literalType, literal.SourceContext);
      }
      ExpressionList args = new ExpressionList(5);
      int[] bits = Decimal.GetBits((Decimal)literal.Value);
      args.Add(new Literal(bits[0], SystemTypes.Int32));
      args.Add(new Literal(bits[1], SystemTypes.Int32));
      args.Add(new Literal(bits[2], SystemTypes.Int32));
      args.Add(new Literal(bits[3] < 0, SystemTypes.Boolean));
      int scale = (bits[3]&0x7FFFFF)>>16;
      if (scale > 28) scale = 28;
      args.Add(new Literal((byte)(scale), SystemTypes.UInt8));
      InstanceInitializer decimalConstructor = SystemTypes.Decimal.GetConstructor(SystemTypes.Int32, SystemTypes.Int32, SystemTypes.Int32, SystemTypes.Boolean, SystemTypes.UInt8);
      Construct c = new Construct(new MemberBinding(null, decimalConstructor), args);
      c.SourceContext = literal.SourceContext;
      return c;
    }
    public override Expression VisitLocal(Local local){
      if (local == null) return null;
      if (local.Name == StandardIds.NewObj && local.Type != null && local.Type.IsValueType){
        StatementList statements = new StatementList(2);
        statements.Add(new AssignmentStatement(new AddressDereference(new UnaryExpression(local, NodeType.AddressOf, local.Type.GetReferenceType()), local.Type), 
          new Literal(null, SystemTypes.Object)));
        statements.Add(new ExpressionStatement(local));
        return new BlockExpression(new Block(statements), local.Type);
      }
      return base.VisitLocal(local);
    }
    public override Statement VisitLocalDeclarationsStatement(LocalDeclarationsStatement localDeclarations){
      if (localDeclarations == null) return null;
      LocalDeclarationList decls = localDeclarations.Declarations;
      for (int i = 0, n = decls == null ? 0 : decls.Count; i < n; i++){
        LocalDeclaration decl = decls[i];
        if (decl == null) continue;
        decl.InitialValue = null;  // delete the intial value expression so later phases won't get confused in light of comment below.
        Field f = decl.Field;
        if (f == null) continue;
        f.Initializer = this.VisitExpression(f.Initializer);
        //If the method scope is captured for a closure, 
        if (f.Initializer == null && ((BlockScope)f.DeclaringType).CapturedForClosure)
          //Force Normalizer to bind it to the current closure class before any nested closures are visited
          this.VisitMemberBinding(new MemberBinding(new ImplicitThis(), f));
      }
      if (this.currentMethod != null && this.currentMethod.Scope != null && this.currentMethod.Scope.CapturedForClosure) return null;
      return localDeclarations; // leave it for Writer to use for associating debug info
    }    

    protected virtual TypeNode LockGuardType(Lock Lock) {
      return SystemTypes.Object;
    }
    public override Statement VisitLock(Lock Lock){
      if (Lock == null || Lock.Guard == null) return null;
      TypeNode lockGuardType = LockGuardType(Lock);
      Expression temp = new Local(lockGuardType);
      BlockScope tempScope = Lock.ScopeForTemporaryVariable;
      if (tempScope.CapturedForClosure){
        Identifier id = Identifier.For("lockGuard:"+Lock.GetHashCode());
        Field f = new Field(tempScope, null, FieldFlags.CompilerControlled, id, lockGuardType, null);
        temp = new MemberBinding(new ImplicitThis(), f);
      }
      if (Lock.Guard.Type is ITypeParameter && this.useGenerics)
        Lock.Guard = new BinaryExpression(Lock.Guard, new MemberBinding(null, Lock.Guard.Type), NodeType.Box);
      AssignmentStatement aStat = new AssignmentStatement(temp, Lock.Guard);
      aStat.SourceContext = Lock.Guard.SourceContext;
      ExpressionList arguments = new ExpressionList(1);
      arguments.Add(temp);
      MethodCall callEnter = new MethodCall(new MemberBinding(null, Runtime.MonitorEnter), arguments);
      callEnter.Type = SystemTypes.Void;
      ExpressionStatement enterMonitor = new ExpressionStatement(callEnter, aStat.SourceContext);
      MethodCall callExitMonitor = new MethodCall(new MemberBinding(null, Runtime.MonitorExit), arguments);
      callExitMonitor.Type = SystemTypes.Void;
      Block exitMonitor = new Block(new StatementList(1));
      exitMonitor.Statements.Add(new ExpressionStatement(callExitMonitor, aStat.SourceContext));
      Try tryBodyAndExitMonitor = new Try(Lock.Body, null, null, null, new Finally(exitMonitor));
      StatementList statements = new StatementList(3);
      statements.Add(aStat);
      statements.Add(enterMonitor);
      statements.Add(tryBodyAndExitMonitor);
      return this.VisitBlock(new Block(statements));
    }
    public override Expression VisitLRExpression(LRExpression expr){
      if (expr == null) return null;
      Expression e = this.VisitExpression(expr.Expression);
      Reference eRef = e == null ? null : e.Type as Reference;
      if (eRef != null)
        return new AddressDereference(e, eRef.ElementType);
      return e;
    }
    public override Expression VisitThis(This This){
      if (this.currentClosureLocal == null){
        if (This != this.currentThisParameter){
          Field thisVal = this.currentMethod.DeclaringType.GetField(StandardIds.ThisValue);
          if (thisVal == null)
            return new ThisBinding(this.currentThisParameter, This.SourceContext);
          else
            return new MemberBinding(this.currentThisParameter, thisVal);
        }
        return This;
      }
      Expression result = new MemberBinding(this.currentClosureLocal, this.currentMethod.Scope.ThisField);
      // Since this is a closure reference, make certain it is referring to the correct closure.
      while (result.Type != null && This.Type != null && result.Type != This.Type) {
        Field thisVal = result.Type.GetField(StandardIds.ThisValue);
        if (thisVal == null) break;
        result = new MemberBinding(result, thisVal);
        result.Type = thisVal.Type;
      }
      return result;
    }
    public override Expression VisitImplicitThis(ImplicitThis implicitThis){
      Expression result = this.currentClosureLocal;
      if (result == null) return this.currentThisParameter;
      return this.GetBindingPath(result, implicitThis.Type);
    }
    public virtual Expression GetBindingPath(Expression x, TypeNode type) {
      if (type != null && x != null) {
        // if types don't match, look for path to correct instance
        TypeNode xType = TypeNode.StripModifiers(x.Type);
        while (xType != type) {
          Field thisVal = xType.GetField(StandardIds.ThisValue);
          if (thisVal == null) break;
          x = new MemberBinding(x, thisVal);
          x.Type = xType = TypeNode.StripModifiers(thisVal.Type);
        }
      }
      return x;
    }
    public override Expression VisitIndexer(Indexer indexer){
      if (indexer == null) return null;
      bool baseCall = indexer.Object is Base;
      indexer.Object = this.VisitExpression(indexer.Object);
      ExpressionList indices = indexer.Operands = this.VisitExpressionList(indexer.Operands);
      Property property = indexer.CorrespondingDefaultIndexedProperty;
      Method getter = null;
      if (property == null){
        TypeNode obType = indexer.Object.Type;
        TupleType tupT = obType as TupleType;
	MemberList members;
        if (tupT != null && (members=this.GetTypeView(tupT).Members) != null && members.Count > 0){
          if (indices != null && indices.Count > 0){
            Literal lit = indices[0] as Literal;
            if (lit != null && lit.Value is int){
              int i = (int)lit.Value;
              if (i >= 0 && i < members.Count){
                Field f = members[i] as Field;
                if (f != null){
                  return new MemberBinding(new UnaryExpression(indexer.Object, NodeType.AddressOf, indexer.Object.Type.GetReferenceType()), f);
                }
              }
            }
          }
          return null;
        }
        if (obType != null)
          obType = TypeNode.StripModifiers(obType);
        Pointer ptrT = obType as Pointer;
        if (ptrT != null){
          return NormalizeIndexedPointer(indexer, ptrT);
        }
        ArrayType arrT = obType as ArrayType;
        if (arrT == null) return null;
        if (arrT.IsSzArray()){
          TypeNode et = indexer.ElementType = this.typeSystem.GetUnderlyingType(arrT.ElementType);
          if (et.IsValueType && !et.IsPrimitive)
            return new AddressDereference(new UnaryExpression(indexer, NodeType.AddressOf, indexer.Type.GetReferenceType()), et);
          return indexer;
        }
        getter = arrT.Getter;
      }else
        getter = property.Getter;
      if (getter == null) return null;
      if (indexer.Object.Type != null && indexer.Object.Type.IsValueType) {
        AddressDereference ad = indexer.Object as AddressDereference;
        if (ad != null)
          indexer.Object = ad.Address;
        else
          indexer.Object = new UnaryExpression(indexer.Object, NodeType.AddressOf, indexer.Object.Type.GetReferenceType());
      }

      MethodCall call = new MethodCall(new MemberBinding(indexer.Object, getter), indexer.Operands, NodeType.Call, getter.ReturnType);
      if (!baseCall && getter.IsVirtualAndNotDeclaredInStruct){
        call.NodeType = NodeType.Callvirt;
        if (this.useGenerics && indexer.Object != null && indexer.Object.Type is ITypeParameter){
          ((MemberBinding)call.Callee).TargetObject = new UnaryExpression(indexer.Object, NodeType.AddressOf, indexer.Object.Type.GetReferenceType());
          call.Constraint = indexer.Object.Type;
        }
      }
      return call;
    }

    public virtual Expression NormalizeIndexedPointer(Indexer indexer, Pointer ptrT) {
      if (indexer.Operands == null || indexer.Operands.Count != 1 || indexer.Operands[0] == null) return null;
      Expression ptr = (indexer.Object.NodeType != NodeType.Conv_I)? new UnaryExpression(indexer.Object, NodeType.Conv_I, indexer.Object.SourceContext) : indexer.Object;
      UnaryExpression offset = new UnaryExpression(indexer.Operands[0], NodeType.Conv_I, indexer.Operands[0].SourceContext);
      BinaryExpression ptrAdd = new BinaryExpression(ptr, offset, NodeType.Add, ptrT, indexer.SourceContext);
      return new AddressDereference(ptrAdd, ptrT.ElementType, indexer.SourceContext);
    }
    public override Expression VisitMemberBinding(MemberBinding memberBinding){
      if (memberBinding == null) return null;
      TypeNode boundType = memberBinding.BoundMember as TypeNode;
      if (boundType != null) {
        if (this.currentMethod != null && this.currentMethod.Scope != null && this.currentMethod.Scope.CapturedForClosure)
          boundType = this.currentMethod.Scope.FixTypeReference(boundType);
        return new Literal(boundType, SystemTypes.Type, memberBinding.SourceContext);
      }
      // fixup special target objects if member is part of a closure
      if (memberBinding.TargetObject != null && memberBinding.BoundMember != null
        && memberBinding.BoundMember.DeclaringType is ClosureClass && this.currentClosureLocal != null){ 
        switch (memberBinding.TargetObject.NodeType){
          case NodeType.This:
          case NodeType.Base:
          case NodeType.ImplicitThis:
            memberBinding.TargetObject.Type = memberBinding.BoundMember.DeclaringType;
            break;
        }
      }
      Property prop = memberBinding.BoundMember as Property;
      if (prop != null){
        Method getter = prop.Getter;
        if (getter == null) getter = prop.GetBaseGetter();
        if (getter == null) return null;
        bool baseCall = memberBinding.TargetObject is Base;
        memberBinding = new MemberBinding(this.VisitExpression(memberBinding.TargetObject), getter);
        MethodCall call = new MethodCall(memberBinding, null);
        if (!baseCall && getter.IsVirtualAndNotDeclaredInStruct) call.NodeType = NodeType.Callvirt;
        call.Type = prop.Type;
        return call;
      }
      Field f = memberBinding.BoundMember as Field;
      if (f != null && f.IsLiteral){
        if (f.DefaultValue != null) return f.DefaultValue;
        Literal lit = this.VisitExpression(f.Initializer) as Literal;
        if (lit != null){
          f.DefaultValue = lit;
          f.Initializer = null;
          return lit;
        }else{
          f.Flags &= ~(FieldFlags.Literal|FieldFlags.HasDefault);
          f.Flags |= FieldFlags.InitOnly;
        }
      }
      if (memberBinding.TargetObject is ImplicitThis){ //Might be a local or a parameter
        if (f == null) goto done;
        BlockScope bscope = f.DeclaringType as BlockScope;
        if (bscope != null){
          if (!bscope.CapturedForClosure || bscope.MembersArePinned){
            Local loc = this.currentMethod.GetLocalForField(f);
            loc.Pinned = loc.Pinned || bscope.MembersArePinned;
            loc.SourceContext = memberBinding.SourceContext;
            Expression localBinding = new LocalBinding(loc, memberBinding.SourceContext);
            Reference refType = loc.Type as Reference;
            if (refType != null && loc.Pinned) {
              // change type from T& to T* using conv.i
              localBinding = new UnaryExpression(localBinding, NodeType.Conv_I, refType.ElementType.GetPointerType());
            }
            return localBinding;
          }
          if (this.currentClosureLocal == null) return null;
          if (f.DeclaringType != this.currentClosureLocal.Type){ //REVIEW: hasty hack. Go over this and figure out how to do it properly.
            f.DeclaringType = this.currentClosureLocal.Type;
            f.Type = this.currentMethod.Scope.FixTypeReference(f.Type);
            // check to see if we need to mangle the name
            MemberList list = f.DeclaringType.GetMembersNamed(f.Name);
            if (list != null && list.Count > 0) {
              // add offset to name to get a unique name
              f.Name = Identifier.For(f.Name.Name + f.DeclaringType.Members.Count);
            }
            f.DeclaringType.Members.Add(f);
            if (f.DeclaringType.Template != null) {
              Field tf = (Field)f.Clone();
              tf.DeclaringType = f.DeclaringType.Template;
              f.DeclaringType.Template.Members.Add(tf);
            }
            f.Flags &= ~FieldFlags.InitOnly;
          }
          memberBinding.TargetObject = this.currentClosureLocal;
          goto done;
        }
        MethodScope mscope = f.DeclaringType as MethodScope;
        if (mscope != null){
          ParameterField pField = f as ParameterField;
          if (pField != null && (!mscope.CapturedForClosure || this.currentClosureLocal == null || f.Type is Reference))
            return new ParameterBinding(pField.Parameter, memberBinding.SourceContext);
          memberBinding.TargetObject = this.currentClosureLocal;
          goto done;
        }
        ClosureClass closure = f.DeclaringType as ClosureClass;
        if (closure != null){
          ParameterField pField = f as ParameterField;
          if (this.currentClosureLocal != null) {
            memberBinding.TargetObject = this.currentClosureLocal;
            TypeNode currentClosure = this.currentClosureLocal.Type;
            while (!closure.IsStructurallyEquivalentTo(currentClosure) && currentClosure != null){
              Field thisVal = currentClosure.GetField(StandardIds.ThisValue);
              if (thisVal == null) break;
              memberBinding.TargetObject = new MemberBinding(memberBinding.TargetObject, thisVal);
              currentClosure = thisVal.Type;
            }
          }
        }
      }
      Expression tObj = memberBinding.TargetObject;
      if (tObj != null && tObj.Type != null && tObj.Type.IsValueType){
        MemberBinding mb = tObj as MemberBinding;
        if (mb != null && mb.BoundMember is Field && ((Field)mb.BoundMember).IsInitOnly){
          StatementList stats = new StatementList(2);
          Local loc = new Local(tObj.Type);
          stats.Add(new AssignmentStatement(loc, tObj));
          stats.Add(new ExpressionStatement(new UnaryExpression(loc, NodeType.AddressOf, loc.Type.GetReferenceType())));
          memberBinding.TargetObject = new BlockExpression(new Block(stats), loc.Type.GetReferenceType());
        }else
          memberBinding.TargetObject = new UnaryExpression(tObj, NodeType.AddressOf, tObj.Type.GetReferenceType());
      }
    done:
      memberBinding.TargetObject = this.VisitExpression(memberBinding.TargetObject);
      if (memberBinding.TargetObject == this.currentThisParameter && this.currentThisParameter != null) {
        ClosureClass cc = TypeNode.StripModifiers(this.currentThisParameter.Type) as ClosureClass;
        if (cc != null && memberBinding.BoundMember != null && memberBinding.BoundMember.DeclaringType != null && !this.GetTypeView(cc).IsAssignableTo(memberBinding.BoundMember.DeclaringType)){
          memberBinding.TargetObject = this.GetBindingPath(this.currentThisParameter, memberBinding.BoundMember.DeclaringType);
        }
      } else if (memberBinding.TargetObject != null){
        ClosureClass cc = TypeNode.StripModifiers(memberBinding.TargetObject.Type) as ClosureClass;
        if (cc != null && memberBinding.BoundMember != null && memberBinding.BoundMember.DeclaringType != null && !cc.IsAssignableTo(memberBinding.BoundMember.DeclaringType)) {
          memberBinding.TargetObject = this.GetBindingPath(memberBinding.TargetObject, memberBinding.BoundMember.DeclaringType);
        }
        if (memberBinding.TargetObject.Type != null && memberBinding.TargetObject.Type.IsValueType) {
          BlockExpression bexpr = memberBinding.TargetObject as BlockExpression;
          if (bexpr != null) {
            if (bexpr.Block != null && bexpr.Block.Statements != null && bexpr.Block.Statements.Count > 0) {
              ExpressionStatement es = bexpr.Block.Statements[bexpr.Block.Statements.Count-1] as ExpressionStatement;
              if (es != null && es.Expression != null && es.Expression.Type != null && es.Expression.Type.IsValueType)
                es.Expression = new UnaryExpression(memberBinding.TargetObject, NodeType.AddressOf, memberBinding.TargetObject.Type.GetReferenceType());
            }
          } else {
            memberBinding.TargetObject = new UnaryExpression(memberBinding.TargetObject, NodeType.AddressOf, memberBinding.TargetObject.Type.GetReferenceType());
          }
        }
      }
      return memberBinding;
    }
    //WS
    private MethodList AllInheritedMethods(Method method) {
      MethodList ml = new MethodList();
      ml.Add(method);
      if ((method.Flags & MethodFlags.NewSlot) != 0)
        return ml;
      if (!method.IsVirtual)
        return ml;
      if(method.DeclaringType.BaseType == null)
        return ml;
      TypeNode tn = method.DeclaringType.BaseType;
      while(tn != null){
        Method n = this.GetTypeView(tn).GetMethod(method.Name, method.GetParameterTypes());
        if (n!= null)
          ml.Add(n);
        tn = tn.BaseType;
      }
      return ml;
    }
#if true || WHIDBEY
    public void SetUpClosureClass(Method method){
      Class closureClass = method.Scope.ClosureClass;
      if (this.CodeMightBeVerified) {
        // Closure classes contain user-written code, but it doesn't get verified.
        closureClass.Attributes.Add(
          new AttributeNode(new MemberBinding(null, SystemTypes.VerifyAttribute.GetConstructor(SystemTypes.Boolean)),
                new ExpressionList(Literal.False), AttributeTargets.Class)
          );
      }
      this.currentType.Members.Add(closureClass);
      MemberList members = closureClass.Members;
      ParameterList parameters = new ParameterList();
      StatementList statements = new StatementList();
      TypeNode thisType = method.Scope.ThisTypeInstance;
      This thisParameter = new This(closureClass);
      if (thisType != null && !method.IsStatic) {
        if (!thisType.IsValueType)
          thisType = OptionalModifier.For(SystemTypes.NonNullType, thisType);
        Field f = (Field)closureClass.Members[0];
        f.Type = thisType;
        Parameter p = new Parameter(f.Name, f.Type);
        // The captured class object parameters to closure class constructors are delayed
        p.Attributes.Add(new AttributeNode(new MemberBinding(null, ExtendedRuntimeTypes.DelayedAttribute.GetConstructor()), null, AttributeTargets.Parameter));
        method.Scope.ThisField = f;
        parameters.Add(p);
        Expression pval = p;
        if (p.Type.IsValueType) pval = new AddressDereference(p, p.Type);
        statements.Add(new AssignmentStatement(new MemberBinding(thisParameter, f), p));
      }
      MemberList scopeMembers = method.Scope.Members;
      for (int i = 0, n = scopeMembers.Count; i < n; i++){
        Member m = scopeMembers[i];
        Field f = m as Field;
        if (f == null || f.Type is Reference) continue;
        f.Type = method.Scope.FixTypeReference(f.Type);
        members.Add(f);
        if (!(f is ParameterField)) continue;
        Parameter p = new Parameter(f.Name, f.Type);
        parameters.Add(p);
        statements.Add(new AssignmentStatement(new MemberBinding(thisParameter, f), p));
      }
      InstanceInitializer cons = new InstanceInitializer();
      cons.ThisParameter = thisParameter;
      cons.DeclaringType = closureClass;
      cons.Flags |= MethodFlags.CompilerControlled;
      cons.Parameters = parameters;
      cons.Scope = new MethodScope(closureClass, new UsedNamespaceList(0));
      cons.Body = new Block(statements);
      MethodCall mcall = new MethodCall(new MemberBinding(thisParameter, CoreSystemTypes.Object.GetConstructor()),
        new ExpressionList(0), NodeType.Call, CoreSystemTypes.Void);
      statements.Add(new ExpressionStatement(mcall));
      statements.Add(new Return());
      closureClass.Members.Add(cons);
    }
    protected virtual bool CodeMightBeVerified {
      get { return false; }
    }
    public Block CreateClosureClassInstance(Method method) {
      Class closureClassTemplate = method.Scope.ClosureClass;
      if (closureClassTemplate == null) { Debug.Fail("method.Scope.ClosureClass == null"); return null; }
      Block newBlock = new Block();
      StatementList statements = new StatementList(1);
      newBlock.Statements = statements;
      newBlock.HasLocals = true;
      //At this point any base or chained constructors have been called and future references to parameters must happen via the closure
      MemberList scopeMembers = method.Scope.Members;
      for (int i = 0, n = scopeMembers.Count; i < n; i++) {
        Member m = scopeMembers[i];
        Field f = m as Field;
        if (f == null || f.Type is Reference) continue;
        f.DeclaringType = closureClassTemplate; //This signals that the parameter field now must bind to the closure field, not the actual parameter
        closureClassTemplate.Members.Add(f);
      }
      Class closureClass = method.Scope.ClosureClass;
      if (closureClass.TemplateParameters != null && closureClass.TemplateParameters.Count > 0)
        closureClass = (Class)closureClass.GetTemplateInstance(this.currentType, method.TemplateParameters);
      this.currentClosureLocal = new Local(Identifier.For("SS$Closure Class Local"+method.UniqueKey), closureClass, newBlock);
      ExpressionList arguments = new ExpressionList();
      if (method.Scope.ThisField != null){
        Expression thisValue = method.ThisParameter;
        TypeNode t = method.Scope.ThisTypeInstance;
        if (t != null && t.IsValueType) thisValue = new AddressDereference(thisValue, t);
        arguments.Add(thisValue);
      }
      ParameterList pars = method.Parameters;
      for (int i = 0, n = pars == null ? 0 : pars.Count; i < n; i++) {
        Parameter p = pars[i];
        if (p == null || p.Type == null || (p.Type is Reference)) continue;
        arguments.Add(p);
      }
      statements.Add(new AssignmentStatement(this.currentClosureLocal, new Construct(new MemberBinding(null, closureClass.GetConstructors()[0]), arguments)));
      return newBlock;
    }
#else
    public void SetUpClosureClass(Method method){
      Class closureClass = method.Scope.ClosureClass;
      this.currentType.Members.Add(closureClass);
      MemberList members = method.Scope.Members;
      for (int i = 0, n = members.Length; i < n; i++){
        Member m = members[i];
        Field f = m as Field;
        if (f != null && f.Type is Reference) continue;
        if (this.typeSystem.IsNonNullType(f.Type)){
          // the fields in a closure class are initialized after the closure class
          // has been constructed. So if it has any non-null fields, they will get
          // caught by the analysis as not being assigned before the closure class's
          // base ctor is called. But we are *very* careful with our closure classes
          // so the fields do not have to be of a non-null type. We could change this
          // by making each closure class's constructor taking the parameters that it
          // is going to assign to its fields. Is that worthwhile?
          //
          // REVIEW: Should we be removing all transparent wrappers, or just the non-
          // null one?
          f.Type = TypeNode.StripModifiers(f.Type);
        }
        closureClass.Members.Add(m);
      }
      return;
    }
    public Block CreateClosureClassInstance(Method method){
      Class closureClass = method.Scope.ClosureClass;
      Block newBlock = new Block();
      StatementList statements = new StatementList(3);
      newBlock.Statements = statements;
      newBlock.HasLocals = true;
      // can't set currentClosureLocal in SetUpClosureClass because in a ctor, cannot use "closure.this"
      // instead of "this" until after base/self ctor call is made.
      this.currentClosureLocal = new Local(Identifier.For("SS$Closure Class Local"+method.UniqueKey), closureClass,newBlock);
      statements.Add(new AssignmentStatement(this.currentClosureLocal, new Construct(new MemberBinding(null, this.GetTypeView(closureClass).GetConstructors()[0]), new ExpressionList(0))));
      if (method.Scope.ThisField != null){
        Expression thisValue = method.ThisParameter;
        if (this.currentType.IsValueType) thisValue = new AddressDereference(thisValue, this.currentType);
        statements.Add(new AssignmentStatement(new MemberBinding(this.currentClosureLocal, method.Scope.ThisField), thisValue));
      }
      MemberList members = method.Scope.Members;
      for (int i = 0, n = members.Length; i < n; i++){
        Field f = members[i] as Field;
        if (f != null && f.Type is Reference) continue;
        f.DeclaringType = closureClass;
        ParameterField pField = f as ParameterField;
        if (pField == null) continue;
        Parameter p = pField.Parameter;
        statements.Add(new AssignmentStatement(new MemberBinding(this.currentClosureLocal, f), p));
      }
      return newBlock;
    }
#endif
    public override InstanceInitializer VisitInstanceInitializer(InstanceInitializer cons) {
      if (cons == null) return null;
      MethodCall savedCurrentCtorCall = this.currentBaseCtorCall;
      if (cons.ContainsBaseMarkerBecauseOfNonNullFields) {
        if (cons.BaseOrDefferingCallBlock == null) goto ActualVisit;
        if (cons.BaseOrDefferingCallBlock.Statements == null) goto ActualVisit;
        if (cons.BaseOrDefferingCallBlock.Statements.Count != 1) goto ActualVisit;
        ExpressionStatement es = cons.BaseOrDefferingCallBlock.Statements[0] as ExpressionStatement;
        if (es == null) goto ActualVisit;
        MethodCall mc = (MethodCall) es.Expression;
        if (mc == null) goto ActualVisit;
        ExpressionList el = mc.Operands;
        if (el == null) goto ActualVisit;
        int n = el.Count;
        ExpressionList localList = new ExpressionList(n);
        StatementList xs = new StatementList(n);
        if (n > 0) cons.Body.HasLocals = true;
        for (int i = 0; i < n; i++) {
          Expression operand = el[i];
          if (operand == null) continue;
          Local l = new Local(Identifier.For("l" + i.ToString()), el[i].Type, cons.Body);
          localList.Add(l);
          xs.Add(new AssignmentStatement(l, el[i], mc.SourceContext));
        }
        this.currentBaseCtorCall = new MethodCall(mc.Callee, localList, NodeType.Call, SystemTypes.Void);
        cons.BaseOrDefferingCallBlock.Statements = xs;
        cons.BaseOrDefferingCallBlock.HasLocals = true;
      }
      ActualVisit:
      InstanceInitializer res = base.VisitInstanceInitializer(cons);
      this.currentBaseCtorCall = savedCurrentCtorCall;
      return res;
    }
    public override Method VisitMethod(Method method) {
      if (method == null) return null;
      if (method.IsNormalized) return method;
      if (method.CciKind != CciMemberKind.Regular) {
        method.Attributes.Add(new AttributeNode(new MemberBinding(null, SystemTypes.CciMemberKindAttribute.GetConstructor(SystemTypes.CciMemberKind)), new ExpressionList(new Literal(method.CciKind, SystemTypes.CciMemberKind)), AttributeTargets.Method));
      }
      method.IsNormalized = true;
      method.Attributes = this.VisitAttributeList(method.Attributes);
      if (method.ReturnAttributes == null)
        method.ReturnAttributes = new AttributeList();
      else
        method.ReturnAttributes = this.VisitAttributeList(method.ReturnAttributes);
      AttributeList al = this.ExtractAttributes(method.Attributes, AttributeTargets.ReturnValue);
      for (int i = 0, n = al == null ? 0 : al.Count; i < n; i++){
        method.ReturnAttributes.Add(al[i]);
      }
      method.SecurityAttributes = this.VisitSecurityAttributeList(method.SecurityAttributes);
      if (method.Attributes != null && method.Attributes.Count > 0) {
        SecurityAttributeList secAttrs = method.SecurityAttributes;
        this.ExtractSecurityAttributes(method.Attributes, ref secAttrs);
        method.SecurityAttributes = secAttrs;
      }
      if (method.SecurityAttributes != null && method.SecurityAttributes.Count > 0)
        method.Flags |= MethodFlags.HasSecurity;
      Method savedMethod = this.currentMethod;
      This savedThisParameter = this.currentThisParameter;
      Local savedReturnLocal = this.currentReturnLocal;
      Block savedReturnLabel = this.currentReturnLabel;
      Local savedClosureLocal = this.currentClosureLocal;
      Block savedContractPrelude = this.currentContractPrelude;
      BlockList savedContractExceptionalTerminationChecks = this.currentContractExceptionalTerminationChecks;
      Local savedcurrentExceptionalTerminationException = this.currentExceptionalTerminationException;
      Block savedContractNormalTerminationCheck = this.currentContractNormalTerminationCheck;
      Block savedParentBlock = null;
      this.currentMethod = method;
      this.currentThisParameter = method.ThisParameter;
      if (method.ThisParameter != null) {
        method.ThisParameter.DeclaringMethod = method;
        if (method.DeclaringType != null && method.DeclaringType.IsValueType)
          method.ThisParameter.Type = method.DeclaringType.GetReferenceType();
      }
      this.currentReturnLabel = new Block(new StatementList());
      this.currentClosureLocal = null;
      this.currentContractPrelude = new Block(new StatementList());
      this.currentContractExceptionalTerminationChecks = new BlockList();
      this.currentExceptionalTerminationException = new Local(Identifier.For("SS$caught_exception"), SystemTypes.Exception);
      this.currentContractNormalTerminationCheck = new Block(new StatementList());
      if (TypeNode.StripModifiers(method.ReturnType) == SystemTypes.Void)
        this.currentReturnLocal = null;
      else
        this.currentReturnLocal = new Local(Identifier.For("return value"), method.ReturnType);
      method.Parameters = this.VisitParameterList(method.Parameters);
      method.TemplateArguments = this.VisitTypeReferenceList(method.TemplateArguments);
      method.TemplateParameters = this.VisitTypeParameterList(method.TemplateParameters);
      TypeNodeList tpars = method.TemplateParameters;
      if (!method.IsGeneric){
        for (int i = 0, n = tpars == null ? 0 : tpars.Count; i < n; i++){
          TypeNode tpar = tpars[i];
          if (tpar == null) continue;
          tpar.Name = new Identifier(method.Name+":"+tpar.Name, tpar.SourceContext);
          tpar.DeclaringType = method.DeclaringType;
          method.DeclaringType.Members.Add(tpar);
        }
      }
      if (method.Template == null) {
        // skip this part for instance methods.
        Block closureInit = null;
        bool methodHasClosure = method.Scope != null && method.Scope.CapturedForClosure;
        if (methodHasClosure) this.SetUpClosureClass(method);
        // Normalizer.VisitRequiresList needs to be called in case it wants to generate
        // some default preconditions (that are not injected by Checker in order to not
        // have these preconditions seen by any static checking tools). If there is
        // no contract on this method, then it won't get called, so create a dummy
        // contract and Requires list just so it will get a chance to do its stuff.
        if (method.Contract == null) {
          method.Contract = new MethodContract(method);
          method.Contract.Requires = new RequiresList();
        }
        if (!methodHasClosure || (method is InstanceInitializer)) {
          // ctors will construct the closure class instance after they call another ctor, since they need to pass in "this" as a parameter to the closure class constructor
          method.Contract = this.VisitMethodContract(method.Contract);
          method.Body = this.VisitBlock(method.Body);
        }else{
          method.Contract = this.VisitMethodContract(method.Contract);
          Block b = this.CreateClosureClassInstance(method);
          method.Body = this.VisitBlock(method.Body);
          savedParentBlock = method.Body;
          closureInit = b;
          if (method.Body != null){
            closureInit.SourceContext = method.Body.SourceContext;
            method.Body.HasLocals = true;
          }
        } {

        #region If this method has a contract, then modify its body to add the contracts at the right point
        if (method.Body != null && method.Body.Statements != null){
          if (this.currentContractExceptionalTerminationChecks.Count > 0){
            // then wrap the body into a try catch with these checks as the catch blocks
            Block b = new Block(new StatementList(this.currentContractExceptionalTerminationChecks.Count));
            b.HasLocals = true;
            for (int i = 0, n = this.currentContractExceptionalTerminationChecks.Count; i < n; i++) {
              b.Statements.Add(this.currentContractExceptionalTerminationChecks[i]);
            }
            #region Rethrow caught exception
            // last "functional" thing in the block is re-throw whatever exception we caught.
            Throw t = new Throw();
            t.NodeType = NodeType.Rethrow;
            b.Statements.Add(t);
            #endregion

            LocalList ls = new LocalList(1);
            this.currentExceptionalTerminationException.DeclaringBlock = b;
            ls.Add(this.currentExceptionalTerminationException);
            BlockList bs = new BlockList(1);
            bs.Add(b);
            Block newBody = CreateTryCatchBlock(method, method.Body, bs, ls);
            //Block newBody = CreateTryCatchBlock(method,method.Body,this.currentContractExceptionalTerminationChecks,this.currentContractExceptionalLocals);
            method.Body = newBody;
          }
          if (this.currentContractPrelude.Statements.Count > 0){
            Block newBody = new Block(new StatementList(3));
            // Wrap the contract prelude in a block with a dummy catch block.
            // It has a special handler type that lets downstream tools know
            // this block was not part of the original code.
            Throw t = new Throw();
            t.NodeType = NodeType.Rethrow;
            this.currentContractPrelude = CreateTryCatchBlock(
              method, 
              this.currentContractPrelude, // try body
              new Block(new StatementList(t)), // catch: just rethrow
              new Local(Identifier.For("SS$Contract Marker"), SystemTypes.ContractMarkerException)
              );
            newBody.Statements.Add(closureInit);
            closureInit = null;
            newBody.Statements.Add(this.currentContractPrelude);
            newBody.Statements.Add(method.Body);
            method.Body = newBody;
          }
        }
      }
        #endregion
        if (closureInit != null){
          closureInit.Statements.Add(method.Body);
          method.Body = closureInit;
        }

        if (method.Body != null && method.Body.Statements != null){
          Return r = new Return();
          r.Expression = this.currentReturnLocal;
          Block returnBlock = new Block(new StatementList());
          returnBlock.Statements.Add(this.currentReturnLabel); {
          InstanceInitializer ctor = method as InstanceInitializer;
          if (ctor != null && method.DeclaringType.Contract != null && method.DeclaringType.Contract.FrameField != null && !ctor.IsDeferringConstructor){
            // then add a Pack to the end
            // BUGBUG: if the programmer has indicated that this default shouldn't apply, then don't do this!
            SourceContext rightBrace = new SourceContext(method.SourceContext.Document, method.SourceContext.EndPos - 1, method.SourceContext.EndPos);
            returnBlock.Statements.Add(
              new ExpressionStatement(
              new MethodCall(new MemberBinding(new MethodCall(new MemberBinding(method.ThisParameter, method.DeclaringType.Contract.FramePropertyGetter), null, NodeType.Call, OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Guard)), SystemTypes.Guard.GetMethod(Identifier.For("EndWriting"))), null, NodeType.Call, SystemTypes.Void),
              rightBrace
              ));
          }
          if (this.currentContractNormalTerminationCheck.Statements.Count > 0){
            // Wrap the contract normal termination checks in a block with a dummy catch block.
            // It has a special handler type that lets downstream tools know this block was not
            // part of the original code.
            Throw t = new Throw();
            t.NodeType = NodeType.Rethrow;
            this.currentContractNormalTerminationCheck = CreateTryCatchBlock(
              method, 
              this.currentContractNormalTerminationCheck, // try body
              new Block(new StatementList(t)), // catch: just rethrow
              new Local(Identifier.For("SS$Contract Marker"),SystemTypes.ContractMarkerException)
              );
            returnBlock.Statements.Add(this.currentContractNormalTerminationCheck);
          }
        }
          if (this.currentReturnLocal != null){
            Local displayReturnLocal = new Local(Identifier.For("SS$Display Return Local"), method.ReturnType);
            returnBlock.Statements.Add(new AssignmentStatement(displayReturnLocal, this.currentReturnLocal));
            returnBlock.HasLocals = true;
          }
          returnBlock.Statements.Add(r);
          if (method.Body.SourceContext.Document != null){
            r.SourceContext = method.SourceContext;
            r.SourceContext.StartPos = method.Body.SourceContext.EndPos-1;
          }
          if (savedParentBlock != null){
            if (savedParentBlock.Statements == null) savedParentBlock.Statements = new StatementList(1);
            savedParentBlock.Statements.Add(returnBlock);
          }else
            method.Body.Statements.Add(returnBlock);
        }
        if (method.ImplementedInterfaceMethods != null && method.ImplementedInterfaceMethods.Count > 0 && method.ImplementedInterfaceMethods[0] != null){
          string typeName = method.ImplementedInterfaceMethods[0].DeclaringType.GetFullUnmangledNameWithTypeParameters();
          method.Name = new Identifier(typeName + "." + method.Name.ToString(), method.Name.SourceContext);
        }
        if (method.HasCompilerGeneratedSignature && method.Parameters != null){
          if (method.Parameters.Count == 1 && (method.Parameters[0].Attributes == null || method.Parameters[0].Attributes.Count == 0))
            method.Parameters[0].Attributes = this.ExtractAttributes(method.Attributes, AttributeTargets.Parameter);
          else if (method.Parameters.Count == 2 && (method.Parameters[1].Attributes == null || method.Parameters[1].Attributes.Count == 0))
            method.Parameters[1].Attributes = this.ExtractAttributes(method.Attributes, AttributeTargets.Parameter);
        }
      }
      this.currentMethod = savedMethod;
      this.currentThisParameter = savedThisParameter;
      this.currentReturnLocal = savedReturnLocal;
      this.currentReturnLabel = savedReturnLabel;
      this.currentClosureLocal = savedClosureLocal;
      this.currentContractPrelude = savedContractPrelude;
      this.currentContractExceptionalTerminationChecks = savedContractExceptionalTerminationChecks;
      this.currentExceptionalTerminationException = savedcurrentExceptionalTerminationException;
      this.currentContractNormalTerminationCheck = savedContractNormalTerminationCheck;
      return method;
    }
    public Block MarkAsInstrumentationCodeNormalized(Method method, Block normalizedCode) {
      Throw t = new Throw();
      t.NodeType = NodeType.Rethrow;
      return CreateTryCatchBlock(
        method,
        normalizedCode, // try body
        new Block(new StatementList(t)), // catch: just rethrow
        new Local(Identifier.For("SS$Contract Marker"), SystemTypes.ContractMarkerException)
        );
    }
    /// <summary>
    /// Creates a block containing the given tryBlock and catchBlocks and
    /// returns it. The method is modified by having new ExceptionHandlers
    /// added to it which points to the right places in the blocks.
    /// The type of exception caught by each catch block should be the type
    /// of the corresponding local l.
    /// </summary>
    /// <param name="m">The method in which the try-catch block will be
    /// inserted into.</param>
    /// <param name="tryBody">A block of statements that will be the body
    /// of the try-catch statement.</param>
    /// <param name="catchBodies">A sequence of blocks; each one contains the
    /// statements that will be the body of a catch clause on the try-catch statement.
    /// </param>
    /// <param name="l">The local into which the exception will be
    /// assigned. Presumably, the body of the catch clause does something
    /// with this local.</param>
    /// <returns>A single block which must be inserted into m by the client.
    /// </returns>
    internal static Block CreateTryCatchBlock(Method m, Block tryBody, Block catchBody, Local l) {
      BlockList bs = new BlockList(1);
      bs.Add(catchBody);
      LocalList ls = new LocalList(1);
      ls.Add(l);
      return CreateTryCatchBlock(m, tryBody, bs, ls);
    }
    internal static Block CreateTryCatchBlock(Method m, Block tryBody, BlockList catchBodies, LocalList ls) {
      // The tryCatch holds the try block, the catch blocks, and an empty block that is the
      // target of an unconditional branch for normal execution to go from the try block
      // around the catch blocks.
      if (m.ExceptionHandlers == null) m.ExceptionHandlers = new ExceptionHandlerList();
      Block tryCatch = new Block(new StatementList());
      Block tryBlock = new Block(new StatementList());
      Block afterCatches = new Block(new StatementList(new Statement(NodeType.Nop)));
      tryBlock.Statements.Add(tryBody);
      tryBlock.Statements.Add(new Branch(null,afterCatches,false,true,true));
      // the EH needs to have a pointer to this block so the writer can
      // calculate the length of the try block. So it should be the *last*
      // thing in the try body.
      Block blockAfterTryBody  = new Block(null);
      tryBlock.Statements.Add(blockAfterTryBody);
      tryCatch.Statements.Add(tryBlock);
      for (int i = 0, n = catchBodies.Count; i < n; i++) {
        // The catchBlock contains the assignment to the local, the catchBody, and then
        // an empty block that is used in the EH.
        Block catchBlock = new Block(new StatementList());
        Local l = ls[i];
        Block catchBody = catchBodies[i];
        catchBlock.Statements.Add(new AssignmentStatement(l,new Expression(NodeType.Pop)));
        catchBlock.Statements.Add(catchBody);
        // The last thing in each catch block is an empty block that is the target of
        // BlockAfterHandlerEnd in each exception handler.
        // It is used in the writer to determine the length of each catch block
        // so it should be the last thing added to each catch block.
        Block blockAfterHandlerEnd = new Block(new StatementList());
        catchBlock.Statements.Add(blockAfterHandlerEnd);
        tryCatch.Statements.Add(catchBlock);

        // add information to the ExceptionHandlers of this method
        ExceptionHandler exHandler = new ExceptionHandler();
        exHandler.TryStartBlock = tryBody;
        exHandler.BlockAfterTryEnd = blockAfterTryBody;
        exHandler.HandlerStartBlock = catchBlock;
        exHandler.BlockAfterHandlerEnd = blockAfterHandlerEnd;
        exHandler.FilterType = l.Type;
        exHandler.HandlerType = NodeType.Catch;
        m.ExceptionHandlers.Add(exHandler);
      }
      tryCatch.Statements.Add(afterCatches);
      return tryCatch;
    }
    internal static Block CreateIfThenElse(Expression guard, Block thenBody, Block elseBody){
      Block b = new Block(new StatementList());
      Block afterIf = new Block(new StatementList(new Statement(NodeType.Nop)));
      b.Statements.Add(new Branch(guard,thenBody));
      b.Statements.Add(elseBody);
      b.Statements.Add(new Branch(null,afterIf));
      b.Statements.Add(thenBody);
      b.Statements.Add(afterIf);
      return b;
    }
    public static Expression TypeOf(TypeNode t){
      return new MethodCall(
        new MemberBinding(null, Runtime.GetTypeFromHandle),
        new ExpressionList(new UnaryExpression(new Literal(t,SystemTypes.Type),NodeType.Ldtoken)));
    }
    void GatherInheritedInstanceInvariants(Interface iface, InvariantList invs){
      if (iface == null) return;
      if (iface.Contract != null && iface.Contract.InvariantCount > 0){
        for (int i = 0, n = iface.Contract.Invariants.Count; i < n; i++){
          if (!iface.Contract.Invariants[i].IsStatic)
            invs.Add(iface.Contract.Invariants[i]);
        }
      }
//      InterfaceList iface_ifaces = this.GetTypeView(iface).Interfaces;
      InterfaceList iface_ifaces = iface.Interfaces;
      for (int i = 0, n = iface_ifaces == null ? 0 : iface_ifaces.Count; i < n; i++){
        GatherInheritedInstanceInvariants(iface_ifaces[i],invs);
      }
      return;
    }
    public override MethodContract VisitMethodContract(MethodContract contract){
      if (contract == null) return null;
      if (contract.DeclaringMethod == null) return contract;
      // No point normalizing contracts on interfaces; no code should be generated.
      if (contract.DeclaringMethod.DeclaringType is Interface) return contract;

      Duplicator duplicator = new Duplicator(this.currentModule, this.currentType);      
      MethodContract copy = duplicator.VisitMethodContract(contract);            

      if (this.currentMethod.IsVisibleOutsideAssembly
        ? !(this.currentCompilation != null && this.currentCompilation.CompilerParameters is CompilerOptions && ((CompilerOptions)this.currentCompilation.CompilerParameters).DisableDefensiveChecks)
        : !(this.currentCompilation != null && this.currentCompilation.CompilerParameters is CompilerOptions && ((CompilerOptions)this.currentCompilation.CompilerParameters).DisableInternalChecks))
        contract.Requires = this.VisitRequiresList(contract.Requires);
      if (!(this.currentCompilation != null && this.currentCompilation.CompilerParameters is CompilerOptions && ((CompilerOptions)this.currentCompilation.CompilerParameters).DisableInternalChecks))
        contract.Ensures = this.VisitEnsuresList(contract.Ensures);
      // don't visit the Modifies clause since no code is generated for it
      //      contract.Modifies = this.VisitExpressionList(contract.Modifies);
      //      contract = base.VisitMethodContract(contract); don't let base visit it!

      contract.Requires = copy.Requires;
      contract.Ensures = copy.Ensures;

      return contract;

    }
      
    public override Expression VisitMethodCall(MethodCall call){
      if (call == null) return null;
      if (call.NodeType == NodeType.MethodCall){/*Debug.Assert(false);*/ call.NodeType = NodeType.Call;}
      MemberBinding mb = call.Callee as MemberBinding;
      if (mb == null || !(mb.BoundMember is FunctionPointer)) {
        call.Callee = this.VisitExpression(call.Callee);
        mb = call.Callee as MemberBinding;
      }

      #region Special case for System.Array.get_Length
      if (mb != null && mb.TargetObject != null && mb.TargetObject.Type != null) {
        ArrayType at = TypeNode.StripModifiers(mb.TargetObject.Type) as ArrayType;
        if (at != null && at.Rank == 1 && CoreSystemTypes.Array != null &&
            mb.BoundMember == CoreSystemTypes.Array.GetMethod(Identifier.For("get_Length"), null)) {
          return new UnaryExpression(new UnaryExpression(mb.TargetObject, NodeType.Ldlen, SystemTypes.IntPtr), NodeType.Conv_I4, SystemTypes.Int32);
        }
      }
      #endregion
      ExpressionList operands = call.Operands;
      if (operands == null) return call;
      call.Operands = this.VisitExpressionList(operands.Clone());
      StatementList statements = null;
      Local temp = null;
      for (int i = 0, n = operands.Count; i < n; i++){
        UnaryExpression uexpr = operands[i] as UnaryExpression;
        if (uexpr == null) continue;
        LRExpression lrExpr = uexpr.Operand as LRExpression;
        if (lrExpr == null) continue;
        if (statements == null){
          statements = new StatementList(n+2);
          if (call.Type != SystemTypes.Void){
            temp = new Local(Identifier.Empty, call.Type);
            statements.Add(new AssignmentStatement(temp, call));
          }else
            statements.Add(new ExpressionStatement(call));
        }
        MethodCall setterCall = (MethodCall)this.VisitTargetExpression(lrExpr.Expression);
        if (setterCall == null) return null;
        setterCall.Operands.Add(lrExpr.Temporaries[lrExpr.Temporaries.Count-1]);
        statements.Add(new ExpressionStatement(setterCall));        
      }
      #region Closure initialization code in a .ctor can come *only* after another ctor call (base or this)
      if (mb != null){
        Method callee = mb.BoundMember as Method;
        if (callee is InstanceInitializer){
          Method currMethod = this.currentMethod;
          if (currMethod is InstanceInitializer){
            Class thisClass = currMethod.DeclaringType as Class;
            if (thisClass != null){
              Class baseClass = thisClass.BaseClass;
              bool baseCall = (baseClass != null && callee.DeclaringType == baseClass);
              bool selfCall = callee.DeclaringType == thisClass;
              if (baseCall || selfCall){
                if (currMethod.Scope != null && currMethod.Scope.CapturedForClosure){
                  if (statements == null) {
                    statements = new StatementList();
                    statements.Add(new ExpressionStatement(call,call.SourceContext));
                  }
                  statements.Add(CreateClosureClassInstance(currMethod));
                }
                if (baseCall){
                  if (thisClass.Contract != null && thisClass.Contract.FrameField != null){
                    if (statements == null){
                      statements = new StatementList();
                      statements.Add(new ExpressionStatement(call,call.SourceContext));
                    }
                    Expression e1 = new Construct(new MemberBinding(null, SystemTypes.InitGuardSetsDelegate.GetConstructor(SystemTypes.Object, SystemTypes.IntPtr)), new ExpressionList(currMethod.ThisParameter, new UnaryExpression(new MemberBinding(null, thisClass.Contract.InitFrameSetsMethod), NodeType.Ldftn)), SystemTypes.InitGuardSetsDelegate);
                    Expression e2 = new Construct(new MemberBinding(null, SystemTypes.CheckInvariantDelegate.GetConstructor(SystemTypes.Object, SystemTypes.IntPtr)), new ExpressionList(currMethod.ThisParameter, new UnaryExpression(new MemberBinding(null, thisClass.Contract.InvariantMethod), NodeType.Ldftn)), SystemTypes.CheckInvariantDelegate);
                    Expression e3 = new Construct(new MemberBinding(null, SystemTypes.Guard.GetConstructor(SystemTypes.InitGuardSetsDelegate, SystemTypes.CheckInvariantDelegate)), new ExpressionList(e1, e2), SystemTypes.Guard);
                    Statement s = new AssignmentStatement(new MemberBinding(currMethod.ThisParameter, currentType.Contract.FrameField), e3);
                    statements.Add((Statement) this.Visit(this.MarkAsInstrumentationCode(s)));
                  }
                }
              }
            }
          }
        }
      }
      #endregion Closure initialization code in a .ctor can come *only* after another ctor call (base or this)

      if (statements == null) return call;
      if (call.Type != SystemTypes.Void)
        statements.Add(new ExpressionStatement(temp));
      return new BlockExpression(new Block(statements),call.Type);
    }
    /// <summary>
    /// Generates a pre-normalized contract marker block.
    /// </summary>
    public virtual Statement MarkAsInstrumentationCode(Statement s){
      return this.MarkAsInstrumentationCode(new Block(new StatementList(s)));
    }
    public virtual Statement MarkAsInstrumentationCode(Block block){
      Throw rethrow = new Throw();
      rethrow.NodeType = NodeType.Rethrow;
      Catch catchClause = new Catch(new Block(new StatementList(rethrow)), new Local(SystemTypes.ContractMarkerException), SystemTypes.ContractMarkerException);
      CatchList catchers = new CatchList(1);
      catchers.Add(catchClause);
      return new Try(block, catchers, null, null, null);
    }
    public override Module VisitModule(Module module){
      this.currentModule = module;
      module = base.VisitModule(module);
      if (module != null) module.IsNormalized = true;
      return module;
    }

    /// <summary>
    /// On evaluation of old expressions, 
    /// evaluate olds = 
    /// if olde is of form: (cases not mutually exclusive; rules are applied in the order specified below)
    /// old(e), if e doesnt contain a quantified variable, evaluates to a local variable initialized to copy(e, olde.NeedDeepCopy);
    /// old(i), if i is a quantified variable, evaluates to i
    /// old(e1[e2]), if e2 depends on quantified vars, evaluates to evaluate(new old(e1, needDeepCopy= true))[eval(new old(e2))] 
    /// old(e1 op e2) including e1.e2, evaluates to evaluate(new old (e1, olde.needDeepcopy)) op 
    ///                                             evaluate(new old (e2, olde.needDeepcopy)) 
    /// old(uop e), evaluates to uop evaluate(new old (e, olde.needdeepcopy))
    /// old(old(e), evaluates to evaluate(new old(e, olde.needDeepCopy))
    /// old(f(e1, e2, ...)), evaluates to f(evaluate(new old(e1)), ...)
    /// We use a seperate pass to implement this evaluation:
    /// When the normalizer sees an outermost old expression for the first time, it calls a separate pass
    /// to collect a set of subexpressions that contains the quantified variables
    /// </summary>
    private OldExpression toplevelOldExpression;
    private System.Collections.Generic.Stack<ExpressionList> quantifiedVarStack = new System.Collections.Generic.Stack<ExpressionList>();
    private System.Collections.Generic.Dictionary<Expression,bool> subExpsDependentOnQuantifiedVars;

    class DependentSubExpSeeker : StandardVisitor {
      System.Collections.Generic.Dictionary<string,bool> quantifiedVars;
      System.Collections.Generic.Stack<Expression> trace;
      System.Collections.Generic.Dictionary<Expression,bool> dependents;
      public System.Collections.Generic.Dictionary<Expression,bool> Dependents {
        get {
          return dependents;
        }
      }
      public DependentSubExpSeeker(System.Collections.Generic.Dictionary<string,bool> quantifiedVars) {
        this.quantifiedVars = quantifiedVars;
        trace = new System.Collections.Generic.Stack<Expression>();
        dependents = new System.Collections.Generic.Dictionary<Expression,bool>();
      }
      public override Expression VisitMemberBinding(MemberBinding memberBinding) {
        if (memberBinding != null && memberBinding.BoundMember != null) {
          if (this.quantifiedVars.ContainsKey(memberBinding.BoundMember.Name.Name)) {
            foreach (Expression e in this.trace) {
              if (!dependents.ContainsKey(e))
                dependents.Add(e,true);
            }
          }
        }
        return base.VisitMemberBinding(memberBinding);
      }

      public override Expression VisitExpression(Expression expression) {
        trace.Push(expression);
        Expression result = base.VisitExpression(expression);
        trace.Pop();
        return result;
      }
    }

    class OldOperatorDistributor : StandardVisitor {
      public OldOperatorDistributor(System.Collections.Generic.Dictionary<Expression,bool> dependents) {
        this.dependents = dependents;
      }

      System.Collections.Generic.Dictionary<Expression,bool> dependents; 

      public override Expression VisitExpression(Expression expression) 
      {
        Expression e = expression;
        OldExpression oldExpression = expression as OldExpression;
        if (oldExpression != null) {
          // if there is an old operator, remove it unless it is already atomic
          // an atomic old expression is one whose expression does not depend on
          // a quantified variable.
          e = oldExpression.expression;
          if (!dependents.ContainsKey(e)) {
            return expression;
          } 
        }
        if (!dependents.ContainsKey(e)) {
          return new OldExpression(e);
        }
        return base.VisitExpression(e);
      }

      public override Expression VisitMemberBinding(MemberBinding memberBinding) {
        if (memberBinding != null && (memberBinding.TargetObject is This || memberBinding.TargetObject is ImplicitThis)
          && dependents.ContainsKey(memberBinding)) {
          // old(i) where i is an quantified variable
          return memberBinding;
        }
        return base.VisitMemberBinding(memberBinding);
      }

      int currentIndexLevel = 0;

      // TODO: allow arr[i][x][j], where i,j are quantified variables, and x is not
      // TODO: multidimensional array...
      public override Expression VisitIndexer(Indexer indexer) {
        //^ assert this.dependents.Contains(indexer)
        bool operandsDependentOnQuantifiedVar = false;
        foreach (Expression e1 in indexer.Operands) {
          if (this.dependents.ContainsKey(e1)) {
            operandsDependentOnQuantifiedVar = true;
            break;
          }
        }
        bool objectDependentOnQuantifiedVar = this.dependents.ContainsKey(indexer.Object);
        if (operandsDependentOnQuantifiedVar) {
          this.currentIndexLevel ++;
          Expression newArr = this.VisitExpression(indexer.Object);
          OldExpression olde = newArr as OldExpression;
          if (olde == null && !objectDependentOnQuantifiedVar) {
            // If this is an array expression that does not depend on any of the quantified vars
            // create an old expression
            olde = new OldExpression(newArr);
          }
          if (olde != null) {
            olde.ShallowCopyUptoDimension = this.currentIndexLevel;
            indexer.Object = olde;
          } else {
            indexer.Object = newArr;
          }
          this.currentIndexLevel --;
          indexer.Operands = this.VisitExpressionList(indexer.Operands);
          return indexer;
        }
        return base.VisitIndexer(indexer);
      }
    }

    private Expression markOldSubExps(Expression e) {
      OldOperatorDistributor distributor = new OldOperatorDistributor(this.subExpsDependentOnQuantifiedVars);
      return distributor.VisitExpression(e);
    }

    private void collectSubExpsDependentOnQuantifiedVars(Expression e) {
      this.subExpsDependentOnQuantifiedVars = new System.Collections.Generic.Dictionary<Expression,bool>();
      System.Collections.Generic.Dictionary<string,bool> boundNames = new System.Collections.Generic.Dictionary<string,bool>();
      foreach (ExpressionList exps in this.quantifiedVarStack) {
        foreach (Expression exp in exps) {
          ComprehensionBinding cb = exp as ComprehensionBinding;
          if (cb == null) continue;
          MemberBinding mb = cb.TargetVariable as MemberBinding;
          if (mb == null) continue;
          boundNames.Add(mb.BoundMember.Name.Name, true);
        }
      }
      DependentSubExpSeeker seeker = new DependentSubExpSeeker(boundNames);
      seeker.Visit(e);
      this.subExpsDependentOnQuantifiedVars = seeker.Dependents;
    }

    /// <summary>
    /// Generate a statement that keeps an old value an input expression. If the expression is an array,
    /// a certain level of deep copy is performed.
    /// </summary>
    /// <param name="l">The l-value into which the old value of e is going to stored</param>
    /// <param name="e">The expression whose old value is going to keep.</param>
    /// <param name="levelOfDeepCopyForArrayExp">If e is an array (of arrays of arrays ...), the level 
    /// of deep copy we shall make. </param>
    /// <returns></returns>
    private Statement initializeOldExp(Expression l, Expression e, int levelOfDeepCopyForArrayExp) 
      //^ requires levelOfDeepCopyForArrayExp >=0;
      //^ requires (e.Type is ArrayType && !((e.Type as ArrayType).ElementType is ArrayType)) ==> levelOfDeepCopyForArrayExp <=1;
    {
      Statement/*?*/ result;
      bool isNonNull;
      TypeNode eType = e.Type.StripOptionalModifiers(out isNonNull);
      ArrayType arrayType = eType as ArrayType;
      if (arrayType != null && levelOfDeepCopyForArrayExp > 0) {
        ExpressionList sizes = new ExpressionList();
        Member getLength = arrayType.BaseType.GetMethod(Identifier.For("get_Length"));
        Expression arrayLengthMB = new MemberBinding(e, getLength, e.SourceContext);
        Expression arrayLength = new MethodCall(arrayLengthMB, new ExpressionList(), NodeType.Callvirt);
        arrayLength.Type = SystemTypes.Int32;
        sizes.Add(arrayLength);
        Expression callCopy = new ConstructArray(arrayType.ElementType, sizes, null);
        callCopy.Type = arrayType;
        Block b = new Block(new StatementList());
        b.Statements.Add(new AssignmentStatement(l, callCopy, e.SourceContext));
        Block loopBody = new Block(new StatementList());
        Local foreachLocal = new Local(SystemTypes.Int32);
        Expression copyElement = new Indexer(l, new ExpressionList(foreachLocal));
        copyElement.Type = arrayType.ElementType;
        Expression originalElement = new Indexer(e, new ExpressionList(foreachLocal));
        originalElement.Type = arrayType.ElementType;
        Statement copyStatement = this.initializeOldExp(copyElement, originalElement, levelOfDeepCopyForArrayExp - 1);
        // new AssignmentStatement(copyElement, originalElement, oldExpression.SourceContext);
        loopBody.Statements.Add(copyStatement);
        StatementList initializer = new StatementList(new AssignmentStatement(foreachLocal, new Literal(0, SystemTypes.Int32, e.SourceContext)));
        Expression condition = new BinaryExpression(foreachLocal, arrayLength, NodeType.Lt);
        condition.Type = SystemTypes.Boolean;
        Expression foreachLocalPlusOne = new BinaryExpression(foreachLocal, new Literal(1, SystemTypes.Int32), NodeType.Add);
        foreachLocalPlusOne.Type = SystemTypes.Int32;
        StatementList incrementor = new StatementList(new AssignmentStatement(foreachLocal, foreachLocalPlusOne, e.SourceContext));
        b.Statements.Add(new For(initializer, condition, incrementor, loopBody));
        result = this.VisitBlock(b);
      } else
        result = new AssignmentStatement(l, e, e.SourceContext);
      //^ assert result != null;
      return result;
    }

    public override Expression VisitOldExpression(OldExpression oldExp) {
      Expression/*?*/ transformedExpression = oldExp;
      if (this.toplevelOldExpression == null) {
        this.toplevelOldExpression = oldExp;
        this.collectSubExpsDependentOnQuantifiedVars(oldExp);
        transformedExpression = this.markOldSubExps(oldExp);
      }
      if (transformedExpression != oldExp) {
        // if we are not at the toplevel, or the transformed top level is not the old expression, 
        // which means the old operator has been pushed down using the distribution law. 
        Expression result = this.VisitExpression(transformedExpression);
        if (this.toplevelOldExpression == oldExp) {
          // leave top level old expression
          this.toplevelOldExpression = null;
          this.subExpsDependentOnQuantifiedVars = null;
        }
        return result;
      } else {
        OldExpression oldExpression = transformedExpression as OldExpression;
        //^ assert oldExpression != null;
        TypeNode t = oldExpression.Type;
        Reference rt = t as Reference;
        Expression e = oldExpression.expression;
        if (rt != null) {
          e = new AddressDereference(e, rt.ElementType, e.SourceContext);
        }
        string oldName = e.SourceContext.SourceText != null && e.SourceContext.SourceText.Length > 0
          ? "old(" + e.SourceContext.SourceText + ")" : "$SSold" + e.UniqueKey;
        Local l = new Local(Identifier.For(oldName), e.Type);
        #region Add local to method's local list so it gets the right debug scope
        // Since the scope of the "old" local is the entire method body, it suffices
        // to add it to the method's local list. Because of how that is processed in Writer,
        // that means the local's scope will be the entire method body.
        if (this.currentMethod.LocalList == null) {
          this.currentMethod.LocalList = new LocalList();
        }
        this.currentMethod.LocalList.Add(l);
        #endregion
        // normalize the old expression itself
        e = this.VisitExpression(e);
        Statement a = this.initializeOldExp(l, e, oldExpression.ShallowCopyUptoDimension);
        
        this.currentContractPrelude.Statements.Add(a);
        this.currentContractPrelude.HasLocals = true;

        if (this.toplevelOldExpression == oldExpression) {
          this.toplevelOldExpression = null;
          this.subExpsDependentOnQuantifiedVars = null;
        }

        if (rt != null)
          return new UnaryExpression(l, NodeType.AddressOf, t, e.SourceContext);
        else
          return l;
      }
    }
    public override Expression VisitReturnValue(ReturnValue returnValue)
    {
      return this.currentReturnLocal;
    }
    public virtual void VisitTemplateInstanceTypes(TypeNode t){
      if (t == null) return;
      bool nestedTemplate = t.DeclaringType != null;
      TypeNodeList templateInstances = t.TemplateInstances;
      for (int i = 0, n = templateInstances == null ? 0 : templateInstances.Count; i < n; i++){
        TypeNode ti = templateInstances[i];
        if (ti == null) continue;
        if (!t.IsGeneric || !this.useGenerics) this.Visit(ti);
        if (ti.IsNotFullySpecialized){
          MemberList unspecializedMembers = this.GetTypeView(t).Members;
          MemberList specializedMembers = this.GetTypeView(ti).Members;
          if (unspecializedMembers == null || specializedMembers == null) continue;
          int m = unspecializedMembers.Count;
          int offset = m - specializedMembers.Count;
          if (offset < 0){
            if (this.useGenerics){
              Debug.Assert(false); 
              continue;
            }
            int sm = specializedMembers.Count;
            while (sm > 0){
              Member smem = specializedMembers[--sm];
              if (smem == null) break;
              if (smem is ITypeParameter) continue;
              Method smeth = smem as Method;
              if (smeth == null) break;
              if (smeth.TemplateArguments == null || smeth.TemplateArguments.Count == 0) break;
            }
            offset = m - sm;
            if (offset < 0){
              Debug.Assert(false); 
              continue;
            }
          }
          for (int j = offset; j < m; j++){
            Member unspecializedMember = unspecializedMembers[j];
            Member specializedMember = specializedMembers[j-offset];
            if (unspecializedMember == null) continue;
            if (specializedMember == null) continue;
            specializedMember.Attributes = unspecializedMember.Attributes;
            Method unspecializedMethod = unspecializedMember as Method;
            Method specializedMethod = specializedMember as Method;
            if (unspecializedMethod == null || specializedMethod == null) continue;
            specializedMethod.ReturnAttributes = unspecializedMethod.ReturnAttributes;
            ParameterList unspecializedParameters = unspecializedMethod.Parameters;
            ParameterList specializedParameters = specializedMethod.Parameters;
            if (unspecializedParameters == null || specializedParameters == null) continue;
            int np = unspecializedParameters.Count;
            if (np != specializedParameters.Count) continue;
            for (int k = 0; k < np; k++){
              Parameter unspecializedParameter = unspecializedParameters[k];
              Parameter specializedParameter = specializedParameters[k];
              if (unspecializedParameter == null || specializedParameter == null) continue;
              specializedParameter.Attributes = unspecializedParameter.Attributes;
            }
          }
        }
      }
    }
    public virtual Expression VisitLiftedPrefixExpression(PrefixExpression pExpr) {
      if (pExpr == null) return null;
      if (!this.typeSystem.IsNullableType(pExpr.Type)) { Debug.Assert(false); return null; }
      LRExpression lrExpr = this.VisitTargetExpression(pExpr.Expression) as LRExpression;
      if (lrExpr == null) return null;
      TypeNode urType = this.typeSystem.RemoveNullableWrapper(pExpr.Type);
      TypeNode paramType = urType;
      LocalList locals = lrExpr.Temporaries;
      ExpressionList subs = lrExpr.SubexpressionsToEvaluateOnce;

      StatementList statements = new StatementList();
      BlockExpression result = new BlockExpression(new Block(statements));
      for (int i = 0, n = locals.Count; i < n; i++)
        statements.Add(new AssignmentStatement(locals[i], subs[i]));
      EnumNode eType = urType as EnumNode;
      if (eType != null) urType = eType.UnderlyingType;

      Local temp = new Local(Identifier.Empty, pExpr.Type);
      Local tempNew = new Local(Identifier.Empty, pExpr.Type);
      Expression e = this.typeSystem.ExplicitCoercion(lrExpr.Expression, pExpr.Type, this.TypeViewer);
      statements.Add(new AssignmentStatement(temp, this.VisitExpression(e)));

      Method hasValue = this.GetTypeView(pExpr.Type).GetMethod(StandardIds.getHasValue);
      Method getValueOrDefault = this.GetTypeView(pExpr.Type).GetMethod(StandardIds.GetValueOrDefault);
      Method ctor = this.GetTypeView(pExpr.Type).GetMethod(StandardIds.Ctor, paramType);
      Block pushValue = new Block();
      Block done = new Block();

      Expression tempHasValue = new MethodCall(new MemberBinding(new UnaryExpression(temp, NodeType.AddressOf), hasValue), null);
      statements.Add(new Branch(tempHasValue, pushValue));
      statements.Add(new AssignmentStatement(new AddressDereference(new UnaryExpression(tempNew, NodeType.AddressOf), pExpr.Type), new Literal(null, CoreSystemTypes.Object)));
      statements.Add(new Branch(null, done));
      statements.Add(pushValue);
      Expression value = new MethodCall(new MemberBinding(new UnaryExpression(temp, NodeType.AddressOf), getValueOrDefault), null);
      value.Type = paramType;
      Expression one = GetOneOfType(urType);
      Expression newUVal = new BinaryExpression(value, one, pExpr.Operator, urType is Pointer ? urType : one.Type);
      Construct cons = new Construct(new MemberBinding(null, ctor), new ExpressionList(newUVal));
      result.Type = ctor.DeclaringType;
      statements.Add(new AssignmentStatement(tempNew, cons));
      statements.Add(done);

      Expression target = this.VisitTargetExpression(lrExpr.Expression);
      MethodCall mcall = target as MethodCall;
      if (mcall != null) {
        mcall.Operands.Add(tempNew);
        statements.Add(new ExpressionStatement(mcall));
      } else if (target != null) {
        if (target.Type is Reference) {
          Local temp2 = new Local(Identifier.Empty, pExpr.Type);
          statements.Add(new AssignmentStatement(temp2, tempNew));
          tempNew = temp2;
          target = new AddressDereference(target, pExpr.Type);
        }
        statements.Add(new AssignmentStatement(target, tempNew));
      }

      statements.Add(new ExpressionStatement(tempNew));
      result.Type = pExpr.Type;
      result.SourceContext = pExpr.SourceContext;
      result.Block.SourceContext = pExpr.SourceContext;
      return result;
    }
    public override Expression VisitPrefixExpression(PrefixExpression pExpr) {
      if (pExpr == null) return null;
      if (this.typeSystem.IsNullableType(pExpr.Type))
        return VisitLiftedPrefixExpression(pExpr);
      StatementList statements = new StatementList();
      BlockExpression result = new BlockExpression(new Block(statements));
      LRExpression lrExpr = this.VisitTargetExpression(pExpr.Expression) as LRExpression;
      if (lrExpr == null) return null;
      LocalList locals = lrExpr.Temporaries;
      ExpressionList subs = lrExpr.SubexpressionsToEvaluateOnce;
      for (int i = 0, n = locals.Count; i < n; i++)
        statements.Add(new AssignmentStatement(locals[i], subs[i],pExpr.SourceContext));
      TypeNode rType = pExpr.Type;
      EnumNode eType = rType as EnumNode;
      if (eType != null) rType = eType.UnderlyingType;
      Expression newVal = null;
      if (pExpr.OperatorOverload != null){
        ExpressionList arguments = new ExpressionList(1);
        arguments.Add(this.VisitExpression(lrExpr.Expression));
        newVal = new MethodCall(new MemberBinding(null, pExpr.OperatorOverload), arguments, NodeType.Call, rType);
      }else{
        Expression e = this.typeSystem.AutoDereferenceCoercion(lrExpr.Expression);
        Expression one = GetOneOfType(rType);
        newVal = new BinaryExpression(this.VisitExpression(e), one, pExpr.Operator, rType is Pointer ? rType : one.Type);
      }
      Local temp = new Local(Identifier.Empty, newVal.Type);
      statements.Add(new AssignmentStatement(temp, newVal,pExpr.SourceContext));
      Expression target = this.VisitTargetExpression(lrExpr.Expression);
      MethodCall mcall = target as MethodCall;
      if (mcall != null){
        mcall.Operands.Add(temp);
        statements.Add(new ExpressionStatement(mcall));
      }else if (target != null){
        if (target.Type is Reference) {
          target = new AddressDereference(target, rType);
        }
        statements.Add(new AssignmentStatement(target, this.typeSystem.ExplicitCoercion(temp, target.Type, this.TypeViewer),pExpr.SourceContext));
      }
      statements.Add(new ExpressionStatement(temp,pExpr.SourceContext));
      result.Type = pExpr.Type;
      return result;
    }
    public virtual Expression VisitLiftedPostfixExpression(PostfixExpression pExpr) {
      if (pExpr == null) return null;
      if (!this.typeSystem.IsNullableType(pExpr.Type)) { Debug.Assert(false); return null; }
      LRExpression lrExpr = this.VisitTargetExpression(pExpr.Expression) as LRExpression;
      if (lrExpr == null) return null;
      TypeNode urType = this.typeSystem.RemoveNullableWrapper(pExpr.Type);
      TypeNode paramType = urType;
      LocalList locals = lrExpr.Temporaries;
      ExpressionList subs = lrExpr.SubexpressionsToEvaluateOnce;

      StatementList statements = new StatementList();
      BlockExpression result = new BlockExpression(new Block(statements));
      for (int i = 0, n = locals.Count; i < n; i++)
        statements.Add(new AssignmentStatement(locals[i], subs[i]));
      EnumNode eType = urType as EnumNode;
      if (eType != null) urType = eType.UnderlyingType;

      Local temp = new Local(Identifier.Empty, pExpr.Type);
      Local tempNew = new Local(Identifier.Empty, pExpr.Type);
      Expression e = this.typeSystem.ExplicitCoercion(lrExpr.Expression, pExpr.Type, this.TypeViewer);
      statements.Add(new AssignmentStatement(temp, this.VisitExpression(e)));

      Method hasValue = this.GetTypeView(pExpr.Type).GetMethod(StandardIds.getHasValue);
      Method getValueOrDefault = this.GetTypeView(pExpr.Type).GetMethod(StandardIds.GetValueOrDefault);
      Method ctor = this.GetTypeView(pExpr.Type).GetMethod(StandardIds.Ctor, paramType);
      Block pushValue = new Block();
      Block done = new Block();

      Expression tempHasValue = new MethodCall(new MemberBinding(new UnaryExpression(temp, NodeType.AddressOf), hasValue), null);
      statements.Add(new Branch(tempHasValue, pushValue));
      statements.Add(new AssignmentStatement(new AddressDereference(new UnaryExpression(tempNew, NodeType.AddressOf), pExpr.Type), new Literal(null, CoreSystemTypes.Object)));
      statements.Add(new Branch(null, done));
      statements.Add(pushValue);
      Expression value = new MethodCall(new MemberBinding(new UnaryExpression(temp, NodeType.AddressOf), getValueOrDefault), null);
      value.Type = paramType;
      Expression one = GetOneOfType(urType);
      Expression newUVal = new BinaryExpression(value, one, pExpr.Operator, urType is Pointer ? urType : one.Type);
      Construct cons = new Construct(new MemberBinding(null, ctor), new ExpressionList(newUVal));
      result.Type = ctor.DeclaringType;
      statements.Add(new AssignmentStatement(tempNew, cons));
      statements.Add(done);

      Expression target = this.VisitTargetExpression(lrExpr.Expression);
      MethodCall mcall = target as MethodCall;
      if (mcall != null) {
        mcall.Operands.Add(tempNew);
        statements.Add(new ExpressionStatement(mcall));
      } else if (target != null) {
        if (target.Type is Reference) {
          Local temp2 = new Local(Identifier.Empty, pExpr.Type);
          statements.Add(new AssignmentStatement(temp2, tempNew));
          tempNew = temp2;
          target = new AddressDereference(target, pExpr.Type);
        }
        statements.Add(new AssignmentStatement(target, tempNew));
      }

      statements.Add(new ExpressionStatement(temp));
      result.Type = pExpr.Type;
      result.SourceContext = pExpr.SourceContext;
      result.Block.SourceContext = pExpr.SourceContext;
      return result;
    }
    public override Expression VisitPostfixExpression(PostfixExpression pExpr){
      if (pExpr == null) return null;
      if (this.typeSystem.IsNullableType(pExpr.Type))
        return VisitLiftedPostfixExpression(pExpr);
      StatementList statements = new StatementList();
      BlockExpression result = new BlockExpression(new Block(statements));
      LRExpression lrExpr = this.VisitTargetExpression(pExpr.Expression) as LRExpression;
      if (lrExpr == null) return null;
      LocalList locals = lrExpr.Temporaries;
      ExpressionList subs = lrExpr.SubexpressionsToEvaluateOnce;
      for (int i = 0, n = locals.Count; i < n; i++)
        statements.Add(new AssignmentStatement(locals[i], subs[i],pExpr.SourceContext));
      TypeNode rType = pExpr.Type;
      EnumNode eType = rType as EnumNode;
      if (eType != null) rType = eType.UnderlyingType;
      Local temp = new Local(Identifier.Empty, rType);
      Expression e = this.typeSystem.AutoDereferenceCoercion(lrExpr.Expression);
      statements.Add(new AssignmentStatement(temp, this.VisitExpression(e), pExpr.SourceContext));
      Expression newVal = null;
      if (pExpr.OperatorOverload != null){
        ExpressionList arguments = new ExpressionList(1);
        arguments.Add(this.VisitExpression(lrExpr.Expression));
        newVal = new MethodCall(new MemberBinding(null, pExpr.OperatorOverload), arguments, NodeType.Call, rType);
      }else{
        Expression one = Literal.Int32One;
        if (rType == SystemTypes.Int64 || rType == SystemTypes.UInt64)
          one = Literal.Int64One;
        else if (rType == SystemTypes.Double)
          one = Literal.DoubleOne;
        else if (rType == SystemTypes.Single)
          one = Literal.SingleOne;
        else if (rType is Pointer){
          Literal elementType = new Literal(((Pointer)rType).ElementType, SystemTypes.Type);
          UnaryExpression sizeOf = new UnaryExpression(elementType, NodeType.Sizeof, SystemTypes.Int32);
          one = PureEvaluator.EvalUnaryExpression(elementType, sizeOf);
          if (one == null) one = sizeOf;
        }
        newVal = new BinaryExpression(temp, one, pExpr.Operator, rType is Pointer ? rType : one.Type);
        newVal = this.typeSystem.ExplicitCoercion(newVal, lrExpr.Type, this.TypeViewer);
      }
      Expression target = this.VisitTargetExpression(lrExpr.Expression);
      MethodCall mcall = target as MethodCall;
      if (mcall != null){
        mcall.Operands.Add(newVal);
        statements.Add(new ExpressionStatement(mcall));
      }else if (target != null){
        if (target.Type is Reference){
          Local temp2 = new Local(Identifier.Empty, rType);
          statements.Add(new AssignmentStatement(temp2, newVal));
          newVal = temp2;
          target = new AddressDereference(target, rType);
        }
        statements.Add(new AssignmentStatement(target, newVal,pExpr.SourceContext));
      }
      statements.Add(new ExpressionStatement(temp));
      result.Type = pExpr.Type;
      result.SourceContext = pExpr.SourceContext;
      result.Block.SourceContext = pExpr.SourceContext;
      return result;
    }
    private static Expression GetOneOfType(TypeNode rType) {
      Expression one = Literal.Int32One;
      if (rType == SystemTypes.Int64 || rType == SystemTypes.UInt64)
        one = Literal.Int64One;
      else if (rType == SystemTypes.Double)
        one = Literal.DoubleOne;
      else if (rType == SystemTypes.Single)
        one = Literal.SingleOne;
      else if (rType is Pointer) {
        Literal elementType = new Literal(((Pointer)rType).ElementType, SystemTypes.Type);
        UnaryExpression sizeOf = new UnaryExpression(elementType, NodeType.Sizeof, SystemTypes.Int32);
        one = PureEvaluator.EvalUnaryExpression(elementType, sizeOf);
        if (one == null) one = sizeOf;
      }
      return one;
    }
    public override Property VisitProperty(Property property) {
      property = base.VisitProperty(property);
      if (property == null) return null;
      if (property.ImplementedTypes != null && property.ImplementedTypes.Count > 0 && property.ImplementedTypes[0] != null){
        string typeName = property.ImplementedTypes[0].GetFullUnmangledNameWithTypeParameters();
        property.Name = new Identifier(typeName + "." + property.Name.ToString(), property.Name.SourceContext);
      }
      return property;
    }
    public override Node VisitQueryAlias(QueryAlias qa) {
      return this.Visit(qa.Expression);
    }
    public override Node VisitQueryAxis(QueryAxis axis) {
      if (axis.Type == null) return null; // error handling.
      Cardinality tcard = this.typeSystem.GetCardinality(axis, this.TypeViewer);
      switch( tcard ) {
        case Cardinality.None:
        case Cardinality.One:
        case Cardinality.ZeroOrOne: {
          BlockScope scope = this.currentMethod.Body.Scope;
          Block block = new Block(new StatementList(4));
          AxisBuildState state = new AxisBuildState(axis.Type);
          state.YieldBlock = new Block();
          state.YieldTarget = this.NewClosureLocal(axis.Type, scope);
          if (tcard == Cardinality.ZeroOrOne) {
            block.Statements.Add(new AssignmentStatement(state.YieldTarget, this.typeSystem.ImplicitCoercion(Literal.Null, state.YieldType, this.TypeViewer)));
          }
          block.Statements.Add(this.BuildAxisClosure(axis.Source, state, axis.AccessPlan, scope));
          block.Statements.Add(state.YieldBlock);
          block.Statements.Add(new ExpressionStatement(state.YieldTarget));
          BlockExpression be = new BlockExpression(block, state.YieldType);
          return this.VisitBlockExpression(be);
        }
        default:
        case Cardinality.OneOrMore:
        case Cardinality.ZeroOrMore: {
          TypeNode targetType = this.typeSystem.GetStreamElementType(axis, this.TypeViewer);
          Block body = null;
          Node closure = this.StartQueryClosure(targetType, "axis", out body);
          BlockScope scope = body.Scope;
          AxisBuildState state = new AxisBuildState(targetType);
          if (axis.IsCyclic) state.TypeClosures = new Hashtable();
          Cardinality scard = this.typeSystem.GetCardinality(axis.Source, this.TypeViewer);
          switch( scard ) {
            case Cardinality.None:
            case Cardinality.One:
            case Cardinality.ZeroOrOne:
              body.Statements.Add(this.BuildAxisClosure(axis.Source, state, axis.AccessPlan, scope));
              break;
            case Cardinality.OneOrMore:
            case Cardinality.ZeroOrMore:
              Expression feTarget = null;
              Block inner = null;
              body.Statements.Add(this.BuildClosureForEach(axis.Source, ref feTarget, out inner, scope));
              inner.Statements.Add(this.BuildAxisClosure(feTarget, state, axis.AccessPlan, scope));
              break;
          }
          return this.EndQueryClosure(closure, axis.Type);
        }
      }
    }
    public class AxisBuildState {
      public Hashtable TypeClosures;
      public TypeNode YieldType;
      public Expression YieldTarget;
      public Block YieldBlock;
      public AxisBuildState(TypeNode yieldType) {
        this.YieldType = yieldType;
      }
    }
    public virtual Block BuildAxisClosure(Expression source, AxisBuildState state, Accessor acc, BlockScope scope) {
      if (state.TypeClosures != null) {
        TypeNode tn = source.Type;
        CurrentClosure cc = (CurrentClosure) state.TypeClosures[tn.UniqueKey];
        if (cc == null) {
          cc = this.BeginCurrentClosure(state.YieldType, "axis");
          Parameter p = new Parameter(Identifier.For("source"), tn);
          cc.Method.Parameters = new ParameterList(1);
          cc.Method.Parameters.Add(p);
          state.TypeClosures[tn.UniqueKey] = cc;
          ParameterField pf = new ParameterField(cc.Method.Scope, null, FieldFlags.Public, p.Name, p.Type, null);
          pf.Parameter = p;
          cc.Method.Scope.Members.Add(pf);
          MemberBinding mb = new MemberBinding(new ImplicitThis(), pf);
          mb.Type = pf.Type;
          cc.Method.Body.Statements.Add(this.BuildAxisClosureBlock(mb, state, acc, cc.Method.Body.Scope));
          this.EndCurrentClosure(cc);
        }
        Block block = new Block(new StatementList(1));
        ImplicitThis it = new ImplicitThis();
        it.Type = cc.Method.DeclaringType;
        MethodCall mc = new MethodCall(new MemberBinding(it, cc.Method), new ExpressionList(source));
        mc.Type = cc.Method.ReturnType;
        if (cc.Method.IsVirtual) mc.NodeType = NodeType.Callvirt;
        Expression target = null;
        Block inner = null;
        block.Statements.Add(this.BuildClosureForEach(mc, ref target, out inner, scope));
        inner.Statements.Add(new Yield(target));
        return block;
      }
      return this.BuildAxisClosureBlock(source, state, acc, scope);
    }
    public virtual Block BuildAxisClosureBlock(Expression source, AxisBuildState state, Accessor accessor, BlockScope scope) {
      if (accessor == null || source == null || state == null || state.YieldType == null || scope == null)
        return null;
      MemberAccessor ma = accessor as MemberAccessor;
      if (ma != null) {
        Block block = new Block(new StatementList(3));
        this.BuildAxisClosureMember(source, state, ma, block, scope);
        return block;
      }
      SequenceAccessor sqa = accessor as SequenceAccessor;
      if (sqa != null) {
        Block block = new Block(new StatementList(sqa.Accessors.Count + 1));
        this.BuildAxisClosureSequence(source, state, sqa, block, scope);
        return block;
      }
      SwitchAccessor swa = accessor as SwitchAccessor;
      if (swa != null) {
        Block block = new Block(new StatementList());
        this.BuildAxisClosureUnion(source, state, swa, false, block, scope);
        return block;
      }
      // todo: add error message here
      return null;
    }
    public virtual void BuildAxisClosureMember(Expression source, AxisBuildState state, MemberAccessor ma, Block block, BlockScope scope) {
      Block inner = block; 
      bool isStatic = (source is Literal) && (source.Type == SystemTypes.Type);
      if (!this.IsLocal(source)) {
        Expression loc = this.NewClosureLocal(source.Type, scope);
        inner.Statements.Add(new AssignmentStatement(loc, source));
        source = loc;
      }
      // sanity check for member/type mismatch caused by aliases
      if (!isStatic && source.Type != ma.Member.DeclaringType) {
        Expression loc = this.NewClosureLocal(ma.Member.DeclaringType, scope);
        inner.Statements.Add(new AssignmentStatement(loc, this.typeSystem.ExplicitCoercion(source, loc.Type, this.TypeViewer)));
        source = loc;
      }
      Cardinality tcard = this.typeSystem.GetCardinality(state.YieldType, this.TypeViewer);
      Cardinality scard = this.typeSystem.GetCardinality(source, this.TypeViewer);
      if (scard != Cardinality.One) {
        Block b = new Block(new StatementList(3));
        inner.Statements.Add(this.IfNotNull(source, b));
        inner = b;
        source = this.GetNonNull(source, inner, scope);
      }
      TypeNode memberType = this.typeSystem.GetMemberType(ma.Member);
      MemberBinding mb = null;
      if (isStatic) {
        mb = new MemberBinding(null, ma.Member);
      }
      else {
        mb = new MemberBinding(source, ma.Member);
      }
      mb.Type = memberType;
      this.BuildAxisClosureExpression(mb, state, ma.Next, ma.Yield, inner, scope);
    }
    public virtual void BuildAxisClosureExpression(Expression source, AxisBuildState state, Accessor next, bool yieldResult, Block inner, BlockScope scope) {
      Cardinality tcard = this.typeSystem.GetCardinality(state.YieldType, this.TypeViewer);
      Cardinality scard = this.typeSystem.GetCardinality(source, this.TypeViewer);
      // lift over collections
      while (true) {
        if (this.typeSystem.IsStructural(source.Type))
          goto lift_done;
        if (yieldResult && scard == tcard) 
          goto lift_done;        
        switch (scard) {
          case Cardinality.ZeroOrOne:
            Block b = new Block(new StatementList(3));
            inner.Statements.Add(this.IfNotNull(source, b));
            inner = b;
            source = this.GetNonNull(source, inner, scope);
            break;
          case Cardinality.ZeroOrMore:
          case Cardinality.OneOrMore:
            TypeNode elementType = this.typeSystem.GetStreamElementType(source, this.TypeViewer);
            Expression target = null;
            Block newInner = null;
            inner.Statements.Add(this.BuildClosureForEach(source, ref target, out newInner, scope));
            inner = newInner;
            source = target;
            break;
          default:
            goto lift_done;
        }
        scard = this.typeSystem.GetCardinality(source, this.TypeViewer);
      }
      lift_done:
        if (yieldResult) {
          if (scard == Cardinality.None) {
            Block b = new Block(new StatementList(1));
            inner.Statements.Add(this.IfNotNull(source, b));
            inner = b;
            source = this.GetNonNull(source, inner, scope);
          }
          if (state.YieldTarget != null && state.YieldBlock != null) {
            inner.Statements.Add(new AssignmentStatement(state.YieldTarget, this.typeSystem.ExplicitCoercion(source, state.YieldType, this.TypeViewer)));
            inner.Statements.Add(new Branch(Literal.True, state.YieldBlock));
          }
          else {
            inner.Statements.Add(new Yield(this.typeSystem.ExplicitCoercion(source, state.YieldType, this.TypeViewer)));
          }
        }
      if (next != null) {
        inner.Statements.Add(this.BuildAxisClosure(source, state, next, scope));
      }
    }
    public virtual void BuildAxisClosureSequence(Expression source, AxisBuildState state, SequenceAccessor sqa, Block block, BlockScope scope) {
      if (!this.IsLocal(source)) {
        Expression loc = this.NewClosureLocal(source.Type, scope);
        block.Statements.Add(new AssignmentStatement(loc, source));
        source = loc;
      }
      foreach( Accessor acc in sqa.Accessors ) {
        block.Statements.Add(this.BuildAxisClosureBlock(source, state, acc, scope));
      }
    }
    public virtual void BuildAxisClosureUnion(Expression source, AxisBuildState state, SwitchAccessor swa, bool yieldResult, Block block, BlockScope scope) {
      TypeUnion tu = source.Type as TypeUnion;
      Debug.Assert(tu != null, "Switch accessor must have type union");
      if (!this.IsLocal(source)) {
        Expression loc = this.NewClosureLocal(source.Type, scope);
        block.Statements.Add(new AssignmentStatement(loc, source));
        source = loc;
      }
      // determine type union tag and value
      Method mgetvalue = this.GetTypeView(tu).GetMethod(StandardIds.GetValue);
      MethodCall mcgetvalue = new MethodCall(new MemberBinding(source, mgetvalue), null); 
      mcgetvalue.Type = mgetvalue.ReturnType;
      Local locValue = new Local(SystemTypes.Object);
      block.Statements.Add(new AssignmentStatement(locValue, mcgetvalue));
      Method mgettag = this.GetTypeView(tu).GetMethod(StandardIds.GetTag);
      MethodCall mcgettag = new MethodCall(new MemberBinding(source, mgettag), null);
      mcgettag.Type = mgettag.ReturnType;
      Local locTag = new Local(SystemTypes.UInt32);
      block.Statements.Add(new AssignmentStatement(locTag, mcgettag));
      // switch on type union tag
      BlockList blocks = new BlockList(swa.Accessors.Count);
      Block endBlock = new Block(null);
      foreach( int id in swa.Accessors.Keys ) {
        Accessor acc = (Accessor) swa.Accessors[id];
        Block caseBlock = new Block(new StatementList(3));
        blocks.Add(caseBlock);
        block.Statements.Add(new Branch(new BinaryExpression(locTag, new Literal(id, SystemTypes.Int32), NodeType.Eq), caseBlock));
        Expression locvar = this.NewClosureLocal(swa.Type.Types[id], scope);
        caseBlock.Statements.Add(new AssignmentStatement(locvar, this.Unbox(locValue, locvar.Type)));
        this.BuildAxisClosureExpression(locvar, state, acc, yieldResult, caseBlock, scope);
        caseBlock.Statements.Add(new Branch(null, endBlock));
      }
      block.Statements.Add(new Branch(null, endBlock));
      for( int i = 0, n = blocks.Count; i < n; i++ ) {
        block.Statements.Add(blocks[i]);
      }
      block.Statements.Add(endBlock);
    }
    private static int nLocal = 0;
    public virtual Expression NewClosureLocal(TypeNode type, BlockScope scope) {
      if (scope == null) return null;
      Identifier id = Identifier.For("var: "+nLocal); nLocal++;
      Field f = new Field(scope, null, FieldFlags.CompilerControlled|FieldFlags.SpecialName, id, type, null);
      scope.Members.Add(f);
      MemberBinding mb = new MemberBinding(new ImplicitThis(scope,0), f);
      mb.Type = f.Type;
      return mb;
    }
    public virtual bool IsLocal(Expression x){
      if (x is Local) return true;
      if (x is QueryContext) return true;
      if (x is Literal) return true;
      MemberBinding mb = x as MemberBinding;
      if (mb != null && mb.TargetObject != null){
        Expression target = mb.TargetObject;
        if (target.NodeType == NodeType.ImplicitThis || target.NodeType == NodeType.This){
          return true;
        }
        if (this.typeSystem.GetCardinality(target, this.TypeViewer) == Cardinality.One){
          return this.IsLocal(target);
        }
      }
      return false;
    }
    public virtual Expression GetInnerExpression(Expression x){
      if (x == null) return null;
      for (;;){
        switch (x.NodeType){
          case NodeType.Parentheses:
          case NodeType.SkipCheck:
            x = ((UnaryExpression)x).Operand;
            if (x == null) return null;
            continue;
          default:
            return x;
        }
      }
    }
    public virtual Node StartQueryClosure(TypeNode yieldType, string name, out Block body) {
      if (this.foldQuery) {
        this.foldQuery = false;
        body = new Block(new StatementList(1));
        body.Scope = new BlockScope(this.currentMethod.Body.Scope, body);
        return body;
      }
      else {
        CurrentClosure cc = this.BeginCurrentClosure(yieldType, name);
        body = cc.Method.Body;
        return cc;
      }
    }
    public virtual Node EndQueryClosure(Node closure, TypeNode collectionType) {
      CurrentClosure cc = closure as CurrentClosure;
      if (cc != null) {
        this.EndCurrentClosure(cc);
        ImplicitThis it = new ImplicitThis();
        it.Type = cc.Method.DeclaringType;
        MethodCall mc = new MethodCall(new MemberBinding(it, cc.Method), null);
        mc.Type = cc.Method.ReturnType;
        return this.VisitExpression(this.typeSystem.ExplicitCoercion(mc, collectionType, this.TypeViewer));
      }
      return closure;  // don't visit here.  These nodes will be visited by QueryYielder
    }
    public virtual CurrentClosure BeginCurrentClosure(TypeNode yieldType, string name) {
      TypeNode returnType = SystemTypes.GenericIEnumerable.GetTemplateInstance(this.currentType, new TypeNodeList(yieldType));
      Class closureClass = this.currentMethod.Scope.ClosureClassTemplateInstance;
      Method method = new Method();
      method.Name = Identifier.For(name + ":" + method.UniqueKey);
      method.Flags = MethodFlags.CompilerControlled|MethodFlags.Assembly;
      method.CallingConvention = CallingConventionFlags.HasThis;
      method.InitLocals = true;
      method.DeclaringType = closureClass;
      method.Body = new Block(new StatementList());
      method.Scope = new MethodScope(this.currentMethod.Scope, null, method);
      method.Body.Scope = new BlockScope(method.Scope, method.Body);
      method.ReturnType = returnType;
      closureClass.Members.Add(method);
      CurrentClosure cc = new CurrentClosure(method, returnType);
      return cc;
    }
    public virtual void EndCurrentClosure(CurrentClosure cc) {
      if (cc != null){
        //TODO: factor out the code for constructing the closure class, so that this elaborate setup is not necessary
        Checker checker = new Checker(null, this.typeSystem, null, null, null);
        checker.currentType = this.typeSystem.currentType = this.currentType;
        checker.currentMethod = this.currentMethod;
        Block oldBody = cc.Method.Body;
        TypeNode yieldType = this.typeSystem.GetStreamElementType(cc.Type, this.TypeViewer);
        cc.Method.Body = new Block(new StatementList(new Yield(new Literal(null, yieldType))));
        checker.VisitMethod(cc.Method);
        Return ret = cc.Method.Body.Statements[0] as Return;
        Debug.Assert(ret != null);
        if (ret != null) {
          ConstructIterator ci = ret.Expression as ConstructIterator;
          Debug.Assert(ci != null);
          if (ci != null) {
            ci.Body = oldBody;
            cc.Method.Body.Scope = oldBody.Scope;
            this.VisitMethod(cc.Method);
          }
        }
      }
    }
    public virtual Statement BuildClosureForEach(Expression source, ref Expression target, out Block body, BlockScope scope) {
      source = this.GetInnerExpression(source);
      if (source is QueryExpression) {
        QueryYielder yielder = new QueryYielder();
        yielder.Source = source;
        yielder.Body = body = new Block(new StatementList(1));
        if (target == null) target = this.NewClosureLocal(this.typeSystem.GetStreamElementType(source, this.TypeViewer), scope);
        yielder.Target = target;
        yielder.State = this.NewClosureLocal(SystemTypes.Int32, scope);
        return yielder;
      }else{
        if (target == null){
          TypeNode elementType = this.typeSystem.GetStreamElementType(source, this.TypeViewer);
          target = target = this.NewClosureLocal(elementType, scope);
        }
        Literal tmpSource = new Literal(null, source.Type);
        ForEach fe = new ForEach(target.Type, target, tmpSource, new Block(new StatementList(1)));
        fe.ScopeForTemporaryVariables = scope;
        Checker checker = new Checker(null, this.typeSystem, null, null, null);
        checker.currentType = this.typeSystem.currentType = (scope.CapturedForClosure ? scope.ClosureClass : this.currentMethod.DeclaringType);
        checker.VisitForEach(fe);
        Debug.Assert(fe.SourceEnumerable != null);
        if (fe.SourceEnumerable != null){
          Debug.Assert(fe.SourceEnumerable is CollectionEnumerator, "SourceEnumerable == "+fe.SourceEnumerable.GetType());
          CollectionEnumerator ce = (CollectionEnumerator)fe.SourceEnumerable;
          ce.Collection = source;
        }
        body = fe.Body;
        return fe;
      }
    }
    static readonly Identifier idIsNull = Identifier.For("IsNull");
    static readonly Identifier idgetIsNull = Identifier.For("get_IsNull");
    public virtual Statement IfNotNull(Expression x, Statement s) {
      Expression nnx = this.GetIsNullExpression(x);
      if (nnx != null) {
        Block block = new Block(new StatementList(3));
        Block brIsNull = new Block();
        block.Statements.Add(new Branch(nnx, brIsNull));
        block.Statements.Add(s);
        block.Statements.Add(brIsNull);
        return block;
      }
      return s;
    }
    public virtual Expression GetIsNullExpression(Expression x) {
      if (x == null) return null;
      x = this.GetInnerExpression(x);
      if (!x.Type.IsValueType) {
        x = new BinaryExpression(x, Literal.Null, NodeType.Eq);
        x.Type = SystemTypes.Boolean;
        return x;
      }
      else if (x.Type.Template == SystemTypes.GenericBoxed) {        
        Method m = this.GetTypeView(x.Type).GetMethod(idIsNull);
        Debug.Assert(m != null, "Boxed.IsNull()");
        MethodCall mc = new MethodCall(new MemberBinding(x, m), null);
        mc.Type = m.ReturnType;
        return mc;
      }
      else if (this.GetTypeView(x.Type).IsAssignableTo(SystemTypes.INullable)) {
        Method m = this.GetTypeView(x.Type).GetMethod(idgetIsNull);
        Debug.Assert(m != null, "INullable.IsNull");
        MethodCall mc = new MethodCall(new MemberBinding(x, m), null);
        mc.Type = m.ReturnType;
        return mc;
      }
      return null;
    }
    private static readonly Identifier idgetValue = Identifier.For("get_Value");
    public virtual Expression GetNonNull(Expression source, Block block, BlockScope scope) {
      Expression nnx = this.GetNonNullExpression(source);
      if (nnx != source) {
        Expression loc = this.NewClosureLocal(nnx.Type, scope);
        block.Statements.Add(new AssignmentStatement(loc, nnx));
        return loc;
      }
      return source;
    }
    public virtual Expression GetNonNullExpression(Expression source) {
      Expression innerSource = this.GetInnerExpression(source);
      Cardinality card = this.typeSystem.GetCardinality(innerSource, this.TypeViewer);
      if (innerSource.Type.Template == SystemTypes.GenericBoxed || 
        this.GetTypeView(innerSource.Type).IsAssignableTo(SystemTypes.INullable)) {
        return this.GetValueExpression(innerSource);
      }
      return source;
    }
    public virtual Expression GetValueExpression(Expression source) {
      if (source == null) return null;
      Method mgetvalue = this.GetTypeView(source.Type).GetMethod(StandardIds.GetValue);
      if (mgetvalue == null) mgetvalue = this.GetTypeView(source.Type).GetMethod(idgetValue);
      if (mgetvalue != null) {
        MethodCall mcgetvalue = new MethodCall(new MemberBinding(source, mgetvalue), null);
        mcgetvalue.Type = mgetvalue.ReturnType;
        return mcgetvalue;
      }
      return null;
    }
    private Expression Box(Expression x) {
      if (x == null) return null;
      if (!x.Type.IsValueType && !(x.Type is TypeParameter && this.useGenerics)) return x;
      return new BinaryExpression(x, new MemberBinding(null, x.Type), NodeType.Box, SystemTypes.Object);
    }
    private Expression Unbox(Expression x, TypeNode type) {
      if (type.IsValueType) {
        BinaryExpression be = new BinaryExpression(x, new Literal(type, SystemTypes.Type), NodeType.Unbox);
        be.Type = type.GetReferenceType();
        return new AddressDereference(be, type);
      }else if (this.useGenerics && type is ITypeParameter){
        BinaryExpression be = new BinaryExpression(x, new Literal(type, SystemTypes.Type), NodeType.UnboxAny);
        be.Type = type;
        return be;
      }else{
        BinaryExpression be = new BinaryExpression(x, new Literal(type, SystemTypes.Type), NodeType.Castclass);
        be.Type = type;
        return be;
      }
    }
    public override Node VisitQueryContext(QueryContext qc) {
      if (qc == null || qc.Scope == null) return null;
      MemberBinding mb = qc.Scope.Target as MemberBinding;
      if (mb != null) {
        ImplicitThis imp = new ImplicitThis(); imp.Type = mb.BoundMember.DeclaringType;
        MemberBinding cmb = new MemberBinding(imp, mb.BoundMember);
        return this.VisitExpression(cmb);
      }
      return this.VisitExpression(qc.Scope.Target);
    }
    private static readonly Identifier idRemoveAt = Identifier.For("RemoveAt");
    private static readonly Identifier idReverse = Identifier.For("Reverse");
    private static readonly Identifier idGetCount = Identifier.For("get_Count");

    public override Node VisitQueryDelete(QueryDelete qd) {
      if (qd.Type == null || qd.Source == null || qd.Source.Type == null) return null;
      Block block = new Block(new StatementList(10));
      
      // get elementType
      QueryFilter qf = qd.Source as QueryFilter;
      Expression source = (qf != null) ? qf.Source : qd.Source;
      Expression collection = qd.SourceEnumerable;   
      TypeNode elementType = this.typeSystem.GetStreamElementType(collection, this.TypeViewer);

      if (qd.Source is QueryFilter) {        
        // Target can not be null if there is more than one source                
        qf = qd.Source as QueryFilter;
        Local locSource = new Local(collection.Type);
        block.Statements.Add(new AssignmentStatement(locSource, collection));
        collection = locSource;

        Method mRemoveAt = this.GetTypeView(locSource.Type).GetMethod(idRemoveAt, SystemTypes.Int32);
        Method mRemove = this.GetTypeView(locSource.Type).GetMethod(StandardIds.Remove, elementType);
        if (mRemove == null) mRemove = this.GetTypeView(locSource.Type).GetMethod(StandardIds.Remove, SystemTypes.Object);

        // need to create a list of matches to delete, since you can't normally delete from a
        // collection while you are iterating it
        Expression locList = new Local(SystemTypes.ArrayList);
        Construct cons = new Construct();
        cons.Constructor = new MemberBinding(null, SystemTypes.ArrayList.GetConstructor());
        block.Statements.Add(new AssignmentStatement(locList, cons));

        if (mRemoveAt != null) {
          // iterate source and find all items to be deleted, add position to list
          Expression locTarget = new Local(elementType);
          Expression locPos = new Local(SystemTypes.Int32);
          Block inner = null;
          // pre-prepare position info for filter
          qf.Context.Position = locPos;
          qf.Context.PreFilter = new Block(new StatementList(1));
          qf.Context.PreFilter.Statements.Add(new AssignmentStatement(locPos, Literal.Int32Zero));
          qf.Context.PostFilter = new Block(new StatementList(1));
          qf.Context.PostFilter.Statements.Add(new AssignmentStatement(locPos, new BinaryExpression(locPos, Literal.Int32One, NodeType.Add)));
          // iterate over filtered items
          block.Statements.Add(this.BuildClosureForEach(qd.Source, ref locTarget, out inner, this.currentMethod.Body.Scope));
          // add position to list
          Method madd = SystemTypes.ArrayList.GetMethod(StandardIds.Add, SystemTypes.Object);
          MethodCall mcAdd = new MethodCall(new MemberBinding(locList, madd), new ExpressionList(this.Box(locPos)));
          mcAdd.Type = madd.ReturnType;
          inner.Statements.Add(new ExpressionStatement(mcAdd));
          // reverse the positions, so we do last to first
          Method mReverse = SystemTypes.ArrayList.GetMethod(idReverse);
          MethodCall mcReverse = new MethodCall(new MemberBinding(locList, mReverse), null);
          mcReverse.Type = mReverse.ReturnType;
          block.Statements.Add(new ExpressionStatement(mcReverse));
          // loop over items in the list, and remove each by its original position
          block.Statements.Add(this.BuildClosureForEach(locList, ref locPos, out inner, this.currentMethod.Body.Scope));
          MethodCall mcRemoveAt = new MethodCall(new MemberBinding(locSource, mRemoveAt), new ExpressionList(locPos));
          mcRemoveAt.Type = mRemoveAt.ReturnType;
          if ((mRemoveAt.Flags & MethodFlags.Virtual) != 0)
            mcRemoveAt.NodeType = NodeType.Callvirt;
          inner.Statements.Add(new ExpressionStatement(mcRemoveAt));
          // return the number of deletes
          Method mGetCount = SystemTypes.ArrayList.GetMethod(idGetCount);
          MethodCall mcGetCount = new MethodCall(new MemberBinding(locList, mGetCount), null);
          mcGetCount.Type = mGetCount.ReturnType;
          block.Statements.Add(new ExpressionStatement(mcGetCount));
        }
        else if (mRemove != null) {
          // iterate source and find all items to be deleted
          Expression locTarget = new Local(elementType);
          Block inner = null;
          block.Statements.Add(this.BuildClosureForEach(qd.Source, ref locTarget, out inner, this.currentMethod.Body.Scope));
          Method madd = SystemTypes.ArrayList.GetMethod(StandardIds.Add, SystemTypes.Object);
          MethodCall mcAdd = new MethodCall(new MemberBinding(locList, madd), new ExpressionList(this.Box(locTarget)));
          mcAdd.Type = madd.ReturnType;
          inner.Statements.Add(new ExpressionStatement(mcAdd));
          // loop over items in the list
          block.Statements.Add(this.BuildClosureForEach(locList, ref locTarget, out inner, this.currentMethod.Body.Scope));
          MethodCall mcRemove = new MethodCall(new MemberBinding(locSource, mRemove), new ExpressionList(this.Box(locTarget)));
          mcRemove.Type = mRemove.ReturnType;
          if ((mRemove.Flags & MethodFlags.Virtual) != 0)
            mcRemove.NodeType = NodeType.Callvirt;
          inner.Statements.Add(new ExpressionStatement(mcRemove));
          // return the number of deletes
          Method mGetCount = SystemTypes.ArrayList.GetMethod(idGetCount);
          MethodCall mcGetCount = new MethodCall(new MemberBinding(locList, mGetCount), null);
          mcGetCount.Type = mGetCount.ReturnType;
          block.Statements.Add(new ExpressionStatement(mcGetCount));
        }
        else {
          return null;
        }
      }
      else {
        Local locSource = new Local(collection.Type);
        Method mRemove = this.GetTypeView(locSource.Type).GetMethod(StandardIds.Remove, elementType);
        if (mRemove == null) mRemove = this.GetTypeView(locSource.Type).GetMethod(StandardIds.Remove, SystemTypes.Object);
        if (mRemove == null) return null;
        block.Statements.Add(new AssignmentStatement(locSource, collection));        
        if (qd.Target != null && qd.Target.Type != null) {
          MethodCall mcRemove = new MethodCall(new MemberBinding(locSource, mRemove), new ExpressionList(this.typeSystem.ImplicitCoercion(qd.Target, mRemove.Parameters[0].Type, this.TypeViewer)));
          mcRemove.Type = mRemove.ReturnType;
          if ((mRemove.Flags & MethodFlags.Virtual) != 0)
            mcRemove.NodeType = NodeType.Callvirt;
          block.Statements.Add(new ExpressionStatement(mcRemove));
        }
        block.Statements.Add(new ExpressionStatement(new Literal(qd.Target == null ? 0 : 1)));
      }
      return this.VisitBlockExpression(new BlockExpression(block, SystemTypes.Int32));
    }
    public override Node VisitQueryDistinct(QueryDistinct qd) {
      Debug.Assert(qd.Group == null, "QueryDistinct evaluated separately from QueryGroupBy");
      // only product disinct-by-value rows of the stream
      TypeNode resultElementType = this.typeSystem.GetStreamElementType(qd.Type, this.TypeViewer);
      Block block = null;
      Node closure = this.StartQueryClosure(resultElementType, "distinct", out block);
      BlockScope scope = block.Scope;
      Expression locTable = this.NewClosureLocal(SystemTypes.Hashtable, scope);
      // build comparer for key
      if (!this.GetTypeView(resultElementType).IsAssignableTo(SystemTypes.IComparable)) {
        TypeNode comparerType = this.BuildDefaultComparer(resultElementType);
        block.Statements.Add(new QueryGeneratedType(comparerType));
        // create hashtable instance before source iteration
        Construct ccons = new Construct();
        ccons.Constructor = new MemberBinding(null, this.GetTypeView(comparerType).GetConstructor());
        ccons.Type = comparerType;
        Construct tcons = new Construct();
        InstanceInitializer ii = SystemTypes.Hashtable.GetConstructor(SystemTypes.IHashCodeProvider, SystemTypes.IComparer);
        tcons.Constructor = new MemberBinding(null, ii);
        tcons.Operands = new ExpressionList(ccons, new Expression(NodeType.Dup, ccons.Type));
        tcons.Type = SystemTypes.Hashtable;
        block.Statements.Add(new AssignmentStatement(locTable, tcons));
      }
      else {
        Construct tcons = new Construct();
        InstanceInitializer ii = SystemTypes.Hashtable.GetConstructor();
        tcons.Constructor = new MemberBinding(null, ii);
        tcons.Type = SystemTypes.Hashtable;
        block.Statements.Add(new AssignmentStatement(locTable, tcons));
      }
      // iterate source items
      Expression feTarget = null;
      Block inner = null;
      block.Statements.Add(this.BuildClosureForEach(qd.Source, ref feTarget, out inner, scope));
      // add key & row to hashtable
      Expression boxedKey = new Local(SystemTypes.Object);
      // object boxedKey = (object)locKey;
      inner.Statements.Add(new AssignmentStatement(boxedKey, this.Box(feTarget)));
      // locList = ht[boxedKey]
      Method mget = SystemTypes.Hashtable.GetMethod(StandardIds.getItem, SystemTypes.Object);
      MethodCall mcget = new MethodCall(new MemberBinding(locTable, mget), new ExpressionList(boxedKey));
      mcget.Type = mget.ReturnType;
      // if (ht[item] != null) goto brBottom;
      Block brBottom = new Block();
      inner.Statements.Add(new Branch(new BinaryExpression(mcget, Literal.Null, NodeType.Ne), brBottom));
      // ht[item] = item;
      Method mset = SystemTypes.Hashtable.GetMethod(Identifier.For("set_Item"), SystemTypes.Object, SystemTypes.Object);
      MethodCall mcset = new MethodCall(new MemberBinding(locTable, mset), new ExpressionList(boxedKey, boxedKey));
      mcset.Type = mset.ReturnType;
      inner.Statements.Add(new ExpressionStatement(mcset));
      // yield item;
      inner.Statements.Add(new Yield(feTarget));
      inner.Statements.Add(brBottom);
      // get rid of hashtable
      block.Statements.Add(new AssignmentStatement(locTable, Literal.Null));
      // and weez done
      return this.EndQueryClosure(closure, qd.Type);
    }
    public override Node VisitQueryExists(QueryExists qe) {
      // true if the source is not empty
      Block block = new Block(new StatementList(4));
      Block exit = new Block();
      TypeNode targetType = this.typeSystem.GetStreamElementType(qe.Source, this.TypeViewer);
      Expression locval = new Local(SystemTypes.Boolean);
      Expression locTarget = new Local(targetType);
      Block inner = null;
      block.Statements.Add(this.BuildClosureForEach(qe.Source, ref locTarget, out inner, this.currentMethod.Body.Scope));
      inner.Statements.Add(new AssignmentStatement(locval, Literal.True));
      inner.Statements.Add(new Branch(Literal.True, exit));
      block.Statements.Add(new AssignmentStatement(locval, Literal.False));
      block.Statements.Add(exit);
      block.Statements.Add(new ExpressionStatement(locval));
      BlockExpression be = new BlockExpression(block, SystemTypes.Boolean);
      return this.VisitBlockExpression(be);
    }
    public override Node VisitQueryFilter(QueryFilter qf) {
      // yield only the items in the source stream where the filter expression is true
      TypeNode resultElementType = this.typeSystem.GetStreamElementType(qf, this.TypeViewer);
      Block body = null;
      Node closure = this.StartQueryClosure(resultElementType, "filter", out body);
      BlockScope scope = body.Scope;
      if (qf.Context.PreFilter == null) 
        qf.Context.PreFilter = new Block(new StatementList(1));
      if (qf.Context.PostFilter == null)
        qf.Context.PostFilter = new Block(new StatementList(1));
      body.Statements.Add(qf.Context.PreFilter);
      Expression target = null;
      Block feBody = null;
      body.Statements.Add(this.BuildClosureForEach(qf.Source, ref target, out feBody, scope));
      qf.Context.Target = target;
      Block inner = new Block(new StatementList(3));
      feBody.Statements.Add(this.IfNotNull(target, inner));
      inner.Statements.Add(new Branch(new UnaryExpression(qf.Expression, NodeType.LogicalNot), qf.Context.PostFilter));
      inner.Statements.Add(new Yield(target));
      inner.Statements.Add(qf.Context.PostFilter);
      return this.EndQueryClosure(closure, qf.Type);
    }
    public override Statement VisitQueryGeneratedType(QueryGeneratedType qgt) {
      if (qgt == null || qgt.Type == null) return null;
      TypeNode decl = this.currentMethod.DeclaringType;
      decl.Members.Add(qgt.Type);
      qgt.Type.DeclaringType = decl;
      this.VisitTypeNode(qgt.Type);
      return null;
    }

    // TODO: Handle default values in the comprehension
    // Q{U i in enumerable, P(i); T(i)}
    public override Expression VisitQuantifier(Quantifier quantifier) {
      if (quantifier == null) return null;
      Comprehension comprehension = quantifier.Comprehension;
      if (comprehension == null) return null;
      Block block = new Block(new StatementList());
      block.HasLocals = true;
      #region Create local to act as accumulator for the quantifier
      Local b = null;
      switch (quantifier.QuantifierType){
        case NodeType.Forall:
          b = new Local(Identifier.Empty,SystemTypes.Boolean,block);
          break;
        case NodeType.Exists:
          b = new Local(Identifier.Empty,SystemTypes.Boolean,block);
          break;
        case NodeType.ExistsUnique:
        case NodeType.Count:
        case NodeType.Max:
        case NodeType.Min:
        case NodeType.Product:
        case NodeType.Sum:
          b = new Local(Identifier.Empty,SystemTypes.Int32,block);
          break;
        default:
          Debug.Assert(false);
          return null;
      }
      #endregion Create local to act as accumulator for the quantifier
      
      if (comprehension.IsDisplay){
        #region Display: Generate a separate if-statement for each element
        Block endBlock = new Block(new StatementList());
        for(int i = 0, n = comprehension.Elements == null ? 0 : comprehension.Elements.Count; i < n; i++){
          #region assign the value of the term to b
          Statement updateB = null;
          switch (quantifier.QuantifierType){
            case NodeType.Forall:
              updateB = new AssignmentStatement(b,comprehension.Elements[i]);
              break;
            case NodeType.Exists:
              updateB = new AssignmentStatement(b,comprehension.Elements[i]);
              break;
            case NodeType.ExistsUnique:
            case NodeType.Count:
              // b := b + (T(i) ? 1 : 0)
              updateB = new AssignmentStatement(b,
                new BinaryExpression(b,
                new TernaryExpression(comprehension.Elements[i], Literal.Int32One, Literal.Int32Zero, NodeType.Conditional, SystemTypes.Int32),
                NodeType.Add));
              break;
            case NodeType.Product:
              // b := b * T(i)
              updateB = new AssignmentStatement(b, new BinaryExpression(b, comprehension.Elements[i], NodeType.Mul));
              break;
            case NodeType.Sum:
              // b := b + T(i)
              updateB = new AssignmentStatement(b, new BinaryExpression(b, comprehension.Elements[i], NodeType.Add));
              break;
            case NodeType.Max:
              // b := b < T(i) ? T(i) : b
              updateB = new AssignmentStatement(b,
                new TernaryExpression(new BinaryExpression(b, comprehension.Elements[i], NodeType.Lt, SystemTypes.Boolean),
                comprehension.Elements[i], b, NodeType.Conditional, SystemTypes.Int32));
              break;
            case NodeType.Min:
              // b := b > T(i) ? T(i) : b
              updateB = new AssignmentStatement(b,
                new TernaryExpression(new BinaryExpression(b, comprehension.Elements[i], NodeType.Gt, SystemTypes.Boolean),
                comprehension.Elements[i], b, NodeType.Conditional, SystemTypes.Int32));
              break;
            default:
              Debug.Assert(false);
              return null;
          }
          block.Statements.Add(updateB);
          #endregion assign the value of the term to b
          #region Test to see if loop should terminate early
          Expression condition = null;
          switch (quantifier.QuantifierType){
            case NodeType.Forall:
              condition = new UnaryExpression(b, NodeType.LogicalNot, SystemTypes.Boolean);
              block.Statements.Add(new Branch(condition, endBlock));
              break;
            case NodeType.Exists:
              condition = b;
              block.Statements.Add(new Branch(condition, endBlock));
              break;
            case NodeType.ExistsUnique:
              condition = new BinaryExpression(b,Literal.Int32One,NodeType.Gt,SystemTypes.Boolean);
              break;
            case NodeType.Count:
            case NodeType.Max:
            case NodeType.Min:
            case NodeType.Product:
            case NodeType.Sum:
              condition = Literal.False; // no short-circuit!! Need to evaluate all of the terms!
              break;
            default:
              Debug.Assert(false);
              return null;
          }
          #endregion Test to see if loop should terminate early
        }
        block.Statements.Add(endBlock);
        #endregion Display: Generate a separate if-statement for each element
      }else {
        #region "True" comprehension
        #region assign the value of the term to the accumulator
        Statement updateB = null;
        switch (quantifier.QuantifierType){
          case NodeType.Forall:
            // b := T(i);
            updateB = new AssignmentStatement(b,comprehension.Elements[0]);
            break;
          case NodeType.Exists:
            // b := T(i);
            updateB = new AssignmentStatement(b,comprehension.Elements[0]);
            break;
          case NodeType.ExistsUnique:
          case NodeType.Count:
            // b := b + T(i) ? 1 : 0; // TODO: is it better to try and generate "b += ..."?
            updateB = new AssignmentStatement(b,
              new BinaryExpression(b,
              new TernaryExpression(comprehension.Elements[0],Literal.Int32One,Literal.Int32Zero,NodeType.Conditional,SystemTypes.Int32),
              NodeType.Add));
            break;
          case NodeType.Product:
            // b := b * T(i)
            updateB = new AssignmentStatement(b,
              new BinaryExpression(b, comprehension.Elements[0], NodeType.Mul));
            break;
          case NodeType.Sum:
            // b := b + T(i)
            updateB = new AssignmentStatement(b,
              new BinaryExpression(b, comprehension.Elements[0], NodeType.Add));
            break;
          case NodeType.Max:
            // b := b < T(i) ? T(i) : b
            updateB = new AssignmentStatement(b,
              new TernaryExpression(new BinaryExpression(b, comprehension.Elements[0], NodeType.Lt, SystemTypes.Boolean),
              comprehension.Elements[0], b, NodeType.Conditional, SystemTypes.Int32));
            break;
          case NodeType.Min:
            // b := b > T(i) ? T(i) : b
            updateB = new AssignmentStatement(b,
              new TernaryExpression(new BinaryExpression(b, comprehension.Elements[0], NodeType.Gt, SystemTypes.Boolean),
              comprehension.Elements[0], b, NodeType.Conditional, SystemTypes.Int32));
            break;
          default:
            Debug.Assert(false);
            return null;
        }
        block.Statements.Add(updateB);
        #endregion assign the value of the term to the accumulator
        #region Generate the "foreach" and "if P(x)" parts
        for (int i = comprehension.BindingsAndFilters.Count - 1; i >= 0; i--) {
          ComprehensionBinding binding = comprehension.BindingsAndFilters[i] as ComprehensionBinding ;
          if (binding != null){
            #region Test to see if loop should terminate early
            Expression condition = null;
            switch (quantifier.QuantifierType){
              case NodeType.Forall:
                condition = new UnaryExpression(b,NodeType.LogicalNot,SystemTypes.Boolean);
                break;
              case NodeType.Exists:
                condition = b;
                break;
              case NodeType.ExistsUnique:
                condition = new BinaryExpression(b,Literal.Int32One,NodeType.Gt,SystemTypes.Boolean);
                break;
              case NodeType.Count:
              case NodeType.Max:
              case NodeType.Min:
              case NodeType.Product:
              case NodeType.Sum:
                condition = Literal.False; // no short-circuit!! Need to evaluate all of the terms!
                break;
              default:
                Debug.Assert(false);
                return null;
            }
            block.Statements.Add(new If(condition,new Block(new StatementList(new Exit())),null));
            #endregion Test to see if loop should terminate early
            #region Wrap everything so far into a loop (either for or foreach)
            Expression forEachTargetVariable = binding.TargetVariable;
            if (binding.AsTargetVariableType != null){
              Local l = new Local(Identifier.For("SS$dummyForEachVar"),binding.SourceEnumerable.Type,block);
              forEachTargetVariable = l;
              Block b2 = new Block(new StatementList(2));
              b2.Statements.Add(new AssignmentStatement(binding.TargetVariable,
                new BinaryExpression(l,new MemberBinding(null,binding.AsTargetVariableType),NodeType.Isinst,binding.AsTargetVariableType)));
              b2.Statements.Add(new If(new BinaryExpression(binding.TargetVariable,new Literal(null,SystemTypes.Type),NodeType.Ne),
                block,null));
              block = b2;
            }
            if (binding.SourceEnumerable== null) 
              return null;
            CollectionEnumerator ce = binding.SourceEnumerable as CollectionEnumerator;
            UnaryExpression u = ce == null ? null : ce.Collection as UnaryExpression;
            BinaryExpression be = u == null ? null : u.Operand as BinaryExpression;
            if (be != null && be.NodeType == NodeType.Range){
              // implement Range with a for-loop
              AssignmentStatement init = new AssignmentStatement(forEachTargetVariable,be.Operand1);
              AssignmentStatement incr = new AssignmentStatement(forEachTargetVariable,
                new BinaryExpression(forEachTargetVariable,new Literal(1,SystemTypes.Int32),NodeType.Add,SystemTypes.Int32));
              Expression cond = new BinaryExpression(forEachTargetVariable,be.Operand2,NodeType.Le,SystemTypes.Boolean);
              #region Add loop invariant "be.Operand1 <= forEachTargetVariable"
              Block invariantBlock = new Block(new StatementList());
              Assertion assertion = new Assertion(new BinaryExpression(be.Operand1, forEachTargetVariable, NodeType.Le, SystemTypes.Boolean));
              assertion.SourceContext = be.SourceContext;
              foreach (Statement s in this.SerializeAssertion(this.currentModule, assertion.Condition, "For loop index must be at least first operand of range.", assertion.SourceContext, "LoopInvariant")) {
                invariantBlock.Statements.Add(s);
              }
              // need to put the generated invariants in the for-loop's condition because that's where VisitFor
              // puts any user-declared invariants.
              invariantBlock.Statements.Add(new ExpressionStatement(cond, cond.SourceContext));
              cond = new BlockExpression(invariantBlock, SystemTypes.Boolean);
              #endregion
              For forloop = new For(new StatementList(init),cond,new StatementList(incr),block);
              block = new Block(new StatementList(forloop));
            }else{
              // Just use the source enumerable as an IEnumerable in a foreach loop
              ForEach fe = new ForEach(binding.SourceEnumerable.Type,forEachTargetVariable,binding.SourceEnumerable,block);
              fe.ScopeForTemporaryVariables = binding.ScopeForTemporaryVariables;
              block = new Block(new StatementList(fe));
            }
            #endregion Wrap everything so far into a loop (either for or foreach)
          }else{ // it's a filter
            block = new Block(new StatementList(new If(comprehension.BindingsAndFilters[i],block,null)));
          }
        }
        #endregion
        #endregion
      }
      #region Choose initial value for accumulator
      Literal initialValue = null;
      switch (quantifier.QuantifierType){
        case NodeType.Forall:
          initialValue = Literal.True;
          break;
        case NodeType.Exists:
          initialValue = Literal.False;
          break;
        case NodeType.ExistsUnique:
        case NodeType.Count:
        case NodeType.Sum:
          initialValue = Literal.Int32Zero;
          break;
        case NodeType.Product:
          initialValue = Literal.Int32One;
          break;
        case NodeType.Max:
          initialValue = new Literal(Int32.MinValue, SystemTypes.Int32);
          break;
        case NodeType.Min:
          initialValue = new Literal(Int32.MaxValue, SystemTypes.Int32);
          break;
        default:
          Debug.Assert(false);
          return null;
      }
      #endregion Choose initial value for accumulator
      #region Set the return value of the quantifier
      Expression valueToReturn = null;
      switch (quantifier.QuantifierType){
        case NodeType.Forall:
          valueToReturn = b;
          break;
        case NodeType.Exists:
          valueToReturn = b;
          break;
        case NodeType.ExistsUnique:
          valueToReturn = new BinaryExpression(b,Literal.Int32One,NodeType.Eq,SystemTypes.Boolean);
          break;
        case NodeType.Count:
        case NodeType.Max:
        case NodeType.Min:
        case NodeType.Product:
        case NodeType.Sum:
          valueToReturn = b;
          break;
        default:
          Debug.Assert(false);
          return null;
      }
      #endregion Set the boolean to return as the value of the quantifier
      BlockExpression returnBlock = new BlockExpression(
        new Block(new StatementList(
        new AssignmentStatement(b,initialValue),
        block,
        new ExpressionStatement(valueToReturn))),
        SystemTypes.Boolean,comprehension.SourceContext);
      if (this.quantifiedVarStack == null) this.quantifiedVarStack = new System.Collections.Generic.Stack<ExpressionList>();
      this.quantifiedVarStack.Push(comprehension.BindingsAndFilters);
      Expression result = this.VisitBlockExpression(returnBlock);
      this.quantifiedVarStack.Pop();
      return result;
    }
    // {1,2,3} ==>
    //     { [ / (IList/IDictionary) t = new T(); ] 
    //       [ yield return 1 / t.Add(1) ];
    //       ...
    //       [  / return (T) t ];
    //     }
    // { T1 x in A, P(x); B(x) ; default } ==> 
    //     {  [ /(IList/IDictionary) t = new T(); ]
    //        bool empty = true; // only for compr. with default
    //        foreach(T1 x in A) { if P(x){ empty = false; [ yield return B(x) / t.Add(B(x)) ];} }
    //        if (empty) [ yield return default / t.Add(default) ]; // only for compr. with default
    //        [ / return (T)t];
    //     }
    public override Expression VisitComprehension(Comprehension comprehension){
      if (comprehension == null) return null;
      Block b = new Block(new StatementList()); // return value from this visitor
      Expression empty = null; // will be either a local or a field
      #region Local variables used when in a non-Enumerable context.
      // TODO: could be a structure, not a class!! (at least write some test cases)
      TypeNode nonEnumClass = null;
      Local retVal = null;
      Method addMethod = null;
      NodeType addMethodCallType = NodeType.Callvirt; // assume virtual calls
      bool useIListMethods = false;
      // use a local so it is evaluated only once in case of side-effects for get_Key and get_Value
      Local de = null;
      Method keyMethod = null;
      Method valueMethod = null;
      #endregion

      bool defaultIsPresent = !comprehension.IsDisplay && comprehension.Elements.Count > 1;
      bool notEnumContext = comprehension.nonEnumerableTypeCtor != null;

      #region Set non-Enumerable locals to the appropriate values.
      // TODO: Look for these things in Checker and if it can't find them, issue diagnostics
      if (notEnumContext){
        Method tempM = comprehension.nonEnumerableTypeCtor as Method;
        addMethod = comprehension.AddMethod;
        if (!addMethod.IsVirtual)
          addMethodCallType = NodeType.Call;
        keyMethod = SystemTypes.DictionaryEntry.GetMethod(Identifier.For("get_Key"));
        valueMethod = SystemTypes.DictionaryEntry.GetMethod(Identifier.For("get_Value"));
 

        if (tempM != null)
          nonEnumClass = (Class) tempM.DeclaringType;
        else
          nonEnumClass = (TypeNode) comprehension.nonEnumerableTypeCtor;
        TypeNode elementType = TypeNode.StripModifiers(comprehension.TemporaryHackToHoldType).TemplateArguments[0];
        if (this.GetTypeView(nonEnumClass).IsAssignableTo(SystemTypes.IList)){
          retVal = new Local(Identifier.For("SS$retVal"),SystemTypes.IList,b);
          useIListMethods = true;
        } else if ((comprehension.Elements.Count == 0 || this.GetTypeView(elementType).IsAssignableTo(SystemTypes.DictionaryEntry)) && this.GetTypeView(nonEnumClass).IsAssignableTo(SystemTypes.IDictionary)){
          retVal = new Local(Identifier.For("SS$retVal"),SystemTypes.IDictionary,b);
          useIListMethods = false; // means "use IDictionary methods"
          de = new Local(Identifier.For("SS$dictionaryEntry"),SystemTypes.DictionaryEntry,b);
          Debug.Assert(de != null && keyMethod != null && valueMethod != null);
        } else if ((comprehension.Elements.Count == 0 || this.GetTypeView(elementType).IsAssignableTo(SystemTypes.DictionaryEntry)) && addMethod != null && addMethod.GetParameterTypes().Length == 2){
          retVal = new Local(Identifier.For("SS$retVal"),nonEnumClass,b);
          useIListMethods = false; // means "use IDictionary methods"
          de = new Local(Identifier.For("SS$dictionaryEntry"),SystemTypes.DictionaryEntry,b);
          Debug.Assert(de != null && keyMethod != null && valueMethod != null);
        } else if (addMethod != null){
          retVal = new Local(Identifier.For("SS$retVal"),nonEnumClass,b);
          useIListMethods = true;
        }
        Debug.Assert(retVal != null && addMethod != null);
      }
      if (defaultIsPresent){
        if (notEnumContext){
          empty = new Local(Identifier.For("SS$empty"),SystemTypes.Boolean,b);
        }else{
          Field emptyField = new Field(Identifier.Empty);
          Class scope = null;
          // defaultIsPresent ==> comprehension.Elements != null
          for (int i = 0, n = comprehension.Elements.Count; i < n; i++){
            // really it should always be the first one, but better be careful
            ComprehensionBinding cb = comprehension.BindingsAndFilters[i] as ComprehensionBinding;
            if (cb != null){
              scope = cb.ScopeForTemporaryVariables.ClosureClass;
              break;
            }
          }
          Debug.Assert(scope != null); //TODO: this assert actually fires
          emptyField.DeclaringType = scope;
          emptyField.Flags = FieldFlags.CompilerControlled;
          emptyField.Name = Identifier.For("SS$empty: "+comprehension.GetHashCode());
          emptyField.Type = SystemTypes.Boolean;
          scope.Members.Add(emptyField);
          empty = new MemberBinding(new ImplicitThis(scope, 0), emptyField);
        }
      }
      #endregion

      #region retVal := new T();
      if (notEnumContext){
        Method m = comprehension.nonEnumerableTypeCtor as Method;
        if (m != null)
          b.Statements.Add(new AssignmentStatement(retVal,
            new Construct(new MemberBinding(null,m),new ExpressionList(),nonEnumClass)));
        else{
          TypeNode structure = comprehension.nonEnumerableTypeCtor as TypeNode;
          b.Statements.Add(new AssignmentStatement(retVal, new Local(StandardIds.NewObj,nonEnumClass))); // !!!! Local normalizes to a pseudo-ctor call for a structure!
        }
      }
      #endregion
      #region bool empty := true;
      if (defaultIsPresent){
        b.Statements.Add(new AssignmentStatement(empty,new Literal(true,SystemTypes.Boolean)));
      }
      #endregion
      #region Generate code for Displays
      if (comprehension.IsDisplay){
        for (int i = 0, n = comprehension.Elements.Count; i < n; i++){
          //          Statement s =
          //            notEnumContext ?
          //            new ExpressionStatement(new MethodCall(new MemberBinding(retVal,addMethod),new ExpressionList(Box(comprehension.Elements[i])),
          //            (retVal.Type.IsValueType ? NodeType.Call : NodeType.Callvirt),
          //            SystemTypes.Int32,comprehension.SourceContext))
          //            :
          //            new Yield(comprehension.Elements[i])
          //            ;
          if (useIListMethods){
            if (notEnumContext)
              b.Statements.Add(new ExpressionStatement(new MethodCall(new MemberBinding(retVal,addMethod),new ExpressionList(Box(comprehension.Elements[i])),
                addMethodCallType, SystemTypes.Int32,comprehension.SourceContext)));
            else
              b.Statements.Add(new Yield(comprehension.Elements[i]));

          }else{ // assume IDictionary!
            if (notEnumContext) {
              b.Statements.Add(new AssignmentStatement(de,comprehension.Elements[i]));
              //retval.Add(de.Key,de.Value) (actually, it is "reval.Add(de.get_Key(),de.get_Value())")
              b.Statements.Add(
                new ExpressionStatement(
                new MethodCall(new MemberBinding(retVal,addMethod),
                new ExpressionList(
                new MethodCall(new MemberBinding(de,keyMethod),new ExpressionList(),NodeType.Call,SystemTypes.Object),
                new MethodCall(new MemberBinding(de,valueMethod),new ExpressionList(),NodeType.Call,SystemTypes.Object)),
                addMethodCallType,SystemTypes.Void)));
            } else
              b.Statements.Add(new Yield(comprehension.Elements[i]));
          }
        }
        if (notEnumContext){
          if (retVal.Type.IsValueType){
            b.Statements.Add(new ExpressionStatement(retVal));
          }else{
            b.Statements.Add(new ExpressionStatement(new BinaryExpression(retVal,new Literal(nonEnumClass, SystemTypes.Type), NodeType.Castclass,nonEnumClass)));
          }
        }
        if (notEnumContext)
          return this.VisitExpression(new BlockExpression(b,retVal.Type));
        else
          return new BlockExpression(this.VisitBlock(b),SystemTypes.Void);
      }
      #endregion
      #region Generate code for "true" Comprehensions
      Block newBlock = new Block(new StatementList(4));
      newBlock.HasLocals = true;
      TypeNode t = null;

      #region empty := false
      if (defaultIsPresent){
        newBlock.Statements.Add(new AssignmentStatement(empty,new Literal(false,SystemTypes.Boolean)));
      }
      #endregion
      #region either "yield return T(x)" or "t.Add(T(x))"
      if (notEnumContext){
        if (useIListMethods){
          if (comprehension.Elements[0]== null)
            return null;
          newBlock.Statements.Add(
            new ExpressionStatement(
            new MethodCall(new MemberBinding(retVal,addMethod),new ExpressionList(Box(comprehension.Elements[0])),addMethodCallType,SystemTypes.Int32,comprehension.Elements[0].SourceContext)));
        }else{ // assume IDictionary!
          newBlock.Statements.Add(new AssignmentStatement(de,comprehension.Elements[0]));
          //retval.Add(de.Key,de.Value) (actually, it is "reval.Add(de.get_Key(),de.get_Value())")
          newBlock.Statements.Add(
            new ExpressionStatement(
            new MethodCall(new MemberBinding(retVal,addMethod),
            new ExpressionList(
            new MethodCall(new MemberBinding(de,keyMethod),new ExpressionList(),NodeType.Call,SystemTypes.Object),
            new MethodCall(new MemberBinding(de,valueMethod),new ExpressionList(),NodeType.Call,SystemTypes.Object)),
            addMethodCallType,SystemTypes.Void)));
        }
      }else{
        newBlock.Statements.Add(new Yield(comprehension.Elements[0]));
      }
      #endregion
      #region Generate the "foreach" and "if P(x)" parts
      for (int i = comprehension.BindingsAndFilters.Count - 1; i >= 0; i--) {
        ComprehensionBinding qb = comprehension.BindingsAndFilters[i] as ComprehensionBinding ;
        if (qb != null){
          Expression forEachTargetVariable = qb.TargetVariable;
          if (qb.SourceEnumerable== null) 
            return null;
          if (qb.AsTargetVariableType != null){
            Local l = new Local(Identifier.For("SS$dummyForEachVar"),qb.SourceEnumerable.Type,newBlock);
            forEachTargetVariable = l;
            Block b2 = new Block(new StatementList(2));
            b2.Statements.Add(new AssignmentStatement(qb.TargetVariable,
              new BinaryExpression(l,new MemberBinding(null,qb.AsTargetVariableType),NodeType.Isinst,qb.AsTargetVariableType)));
            b2.Statements.Add(new If(new BinaryExpression(qb.TargetVariable,new Literal(null,SystemTypes.Type),NodeType.Ne),
              newBlock,null));
            newBlock = b2;
          }
          CollectionEnumerator ce = qb.SourceEnumerable as CollectionEnumerator;
          UnaryExpression u = ce == null ? null : ce.Collection as UnaryExpression;
          BinaryExpression be = u == null ? null : u.Operand as BinaryExpression;
          if (be != null && be.NodeType == NodeType.Range){
            // implement Range with a for-loop
            AssignmentStatement init = new AssignmentStatement(forEachTargetVariable,be.Operand1);
            AssignmentStatement incr = new AssignmentStatement(forEachTargetVariable,
              new BinaryExpression(forEachTargetVariable,new Literal(1,SystemTypes.Int32),NodeType.Add,SystemTypes.Int32));
            Expression cond = new BinaryExpression(forEachTargetVariable,be.Operand2,NodeType.Le,SystemTypes.Boolean);
            For forloop = new For(new StatementList(init),cond,new StatementList(incr),newBlock);
            newBlock = new Block(new StatementList(forloop));
          }else{
            // Just use the source enumerable as an IEnumerable in a foreach loop
            ForEach fe = new ForEach(qb.SourceEnumerable.Type,forEachTargetVariable,qb.SourceEnumerable,newBlock);
            fe.ScopeForTemporaryVariables = qb.ScopeForTemporaryVariables;
            newBlock = new Block(new StatementList(fe));
          }
        }else{ // it's a filter
          newBlock = new Block(new StatementList(new If(comprehension.BindingsAndFilters[i],newBlock,null)));
        }
      }
      // Need to normalize any foreach loop and if-stmt we just generated
      newBlock = this.VisitBlock(newBlock);
      b.Statements.Add(newBlock);
      #endregion
      if ( comprehension.Mode == ComprehensionMode.Comprehension ) {
        #region if (empty) [ yield return default / t.Add(default) ];
        if (defaultIsPresent){
          Expression addArg = comprehension.Elements[1];
              
          if (useIListMethods){
            if (notEnumContext){
              newBlock.Statements.Add(
                this.VisitIf( // need to normalize it
                new If(new BinaryExpression(empty,new Literal(true,SystemTypes.Boolean),NodeType.Eq),
                new Block(new StatementList(new ExpressionStatement(
                new MethodCall(new MemberBinding(retVal,addMethod),new ExpressionList(Box(addArg)),addMethodCallType,SystemTypes.Int32,comprehension.Elements[0].SourceContext))))
                ,null)));
            }else{
              newBlock.Statements.Add(
                this.VisitIf( // need to normalize it
                new If(new BinaryExpression(empty,new Literal(true,SystemTypes.Boolean),NodeType.Eq),
                new Block(new StatementList(new Yield(addArg))),
                null)));
            }
          } else { //assume IDictionary!
            if (notEnumContext){
              newBlock.Statements.Add(
                this.VisitIf( // need to normalize it
                new If(new BinaryExpression(empty,new Literal(true,SystemTypes.Boolean),NodeType.Eq),
                new Block(new StatementList(new ExpressionStatement(
                new MethodCall(new MemberBinding(retVal,addMethod),
                new ExpressionList(
                new MethodCall(new MemberBinding(addArg,keyMethod),new ExpressionList(),NodeType.Call,SystemTypes.Object),
                new MethodCall(new MemberBinding(addArg,valueMethod),new ExpressionList(),NodeType.Call,SystemTypes.Object)),
                addMethodCallType,SystemTypes.Void)))),
                null)));
            } else {
              newBlock.Statements.Add(
                this.VisitIf( // need to normalize it
                new If(new BinaryExpression(empty,new Literal(true,SystemTypes.Boolean),NodeType.Eq),
                new Block(new StatementList(new Yield(addArg))),
                null)));
            }
          }
        }
        #endregion
        #region [ / return t];
        if (notEnumContext){
          if (retVal.Type.IsValueType)
            b.Statements.Add(new ExpressionStatement(retVal));
          else
            b.Statements.Add(new ExpressionStatement(new BinaryExpression(retVal,new Literal(nonEnumClass, SystemTypes.Type), NodeType.Castclass,nonEnumClass)));
        }
        #endregion
      }else{
        #region Reduction
        Method getValMethod = this.GetTypeView(t).GetMethod(Identifier.For("get_Value"),null);
        MethodCall getValCall = new MethodCall(new MemberBinding(new UnaryExpression(retVal, NodeType.AddressOf, retVal.Type.GetReferenceType()),getValMethod),new ExpressionList(),NodeType.Callvirt,SystemTypes.Object,comprehension.Elements[0].SourceContext);
        Expression e = null;
        if (comprehension.Elements[0].Type.IsValueType) {
          e = Unbox(getValCall,comprehension.Elements[0].Type);
        }else{
          e = new BinaryExpression(getValCall,new Literal(comprehension.Elements[0].Type, SystemTypes.Type), NodeType.Castclass);
        }
        newBlock.Statements.Add(new ExpressionStatement(e));
        #endregion
      }
      if (notEnumContext)
        return this.VisitExpression(new BlockExpression(b,retVal.Type,comprehension.SourceContext));
      else
        return new BlockExpression(b,SystemTypes.Void,comprehension.SourceContext);
      #endregion
    }
    public override Node VisitQueryAggregate(QueryAggregate qa) {
      BlockScope scope = this.currentMethod.Body.Scope;
      TypeNode aggType = qa.AggregateType;
      if (qa.Group != null) {
        // find field offset in result
        Field f = null;
        for (int i = 0, n = qa.Group.AggregateList.Count; i < n; i++) {
          if (qa.UniqueKey == qa.Group.AggregateList[i].UniqueKey) {
            int offset = qa.Group.GroupList.Count + i;
            f = qa.Context.Type.Members[offset] as Field;
            break;
          }
        }
        Expression mb = new MemberBinding(new QueryContext(qa.Context), f);
        mb.Type = f.Type;
        return this.VisitExpression(mb);
      }
      else {
        Block block = new Block(new StatementList(4));
        Local locAgg = new Local(aggType);
        if (!aggType.IsValueType) {
          Construct cons = new Construct();
          cons.Constructor = new MemberBinding(null, this.GetTypeView(aggType).GetConstructor());
          cons.Type = aggType;
          block.Statements.Add(new AssignmentStatement(locAgg, cons));
        }
        Block inner = null;
        Expression val = new Local(this.typeSystem.GetStreamElementType(qa.Expression, this.TypeViewer));
        block.Statements.Add(this.BuildClosureForEach(qa.Expression, ref val, out inner, scope));
        // add target to aggregate
        Method madd = this.GetTypeView(aggType).GetMembersNamed(StandardIds.Add)[0] as Method;
        val = this.typeSystem.ExplicitCoercion(val, madd.Parameters[0].Type, this.TypeViewer);
        MethodCall mcadd = new MethodCall(new MemberBinding(locAgg, madd), new ExpressionList(val));
        mcadd.Type = madd.ReturnType;
        inner.Statements.Add(new ExpressionStatement(mcadd));
        // get result value
        Method mgetval = this.GetTypeView(aggType).GetMethod(StandardIds.GetValue);
        MethodCall mcgetval = new MethodCall(new MemberBinding(locAgg, mgetval), null);
        mcgetval.Type = mgetval.ReturnType;
        block.Statements.Add(new ExpressionStatement(mcgetval));
        BlockExpression be = new BlockExpression(block, qa.Type);
        return this.VisitBlockExpression(be);
      }
    }
    public override Node VisitQueryGroupBy(QueryGroupBy gb) {
      TypeNode groupType = this.typeSystem.GetStreamElementType(gb, this.TypeViewer);
      Block block = null;
      Node closure = this.StartQueryClosure(groupType, "groupby", out block);
      BlockScope scope = block.Scope;
      Expression target = null;
      Block inner = null;
      Statement fe = this.BuildClosureForEach(gb.Source, ref target, out inner, scope);
      gb.GroupContext.Target = target;
      // build comparer for grouping fields
      int nGroupers = (gb.GroupList != null ? gb.GroupList.Count : 0);
      MemberList members = this.typeSystem.GetDataMembers(groupType);
      // create key type
      FieldList keyFields = new FieldList();
      for (int i = 0; i < nGroupers; i++) {
        keyFields.Add((Field)members[i]);
      }
      for (int i = 0; i < gb.AggregateList.Count; i++) {
        QueryAggregate qa = gb.AggregateList[i] as QueryAggregate;
        if (qa == null) continue;
        Field f = new Field(null, null, FieldFlags.Public, Identifier.For("Agg"+i), qa.AggregateType, null);
        keyFields.Add(f);
        if (qa.Expression is QueryDistinct) {
          Field fhash = new Field(null, null, FieldFlags.Public, Identifier.For("Hash"+i), SystemTypes.Hashtable, null);
          keyFields.Add(fhash);
        }
      }
      TypeNode keyType = TupleType.For(keyFields, this.currentType);
      MemberList keyMembers = this.typeSystem.GetDataMembers(keyType);
      Local locTable = null;
      // create the grouping hashtable if we have items expressions to group by
      if (nGroupers > 0) {
        TypeNode comparerType = this.BuildComparer(keyType, keyMembers, null, nGroupers);
        block.Statements.Add(new QueryGeneratedType(comparerType));
        // create hashtable instance before source iteration
        locTable = new Local(SystemTypes.Hashtable);
        Construct ccons = new Construct();
        ccons.Constructor = new MemberBinding(null, this.GetTypeView(comparerType).GetConstructor());
        ccons.Type = comparerType;
        Construct tcons = new Construct();
        InstanceInitializer ii = SystemTypes.Hashtable.GetConstructor(SystemTypes.IHashCodeProvider, SystemTypes.IComparer);
        tcons.Constructor = new MemberBinding(null, ii);
        tcons.Operands = new ExpressionList(ccons, new Expression(NodeType.Dup, ccons.Type));
        tcons.Type = SystemTypes.Hashtable;
        block.Statements.Add(new AssignmentStatement(locTable, tcons));
      }
      // iterate over source items
      block.Statements.Add(fe);
      // compute key values based on group expression
      Expression locKey = new Local(keyType);
      if (locTable != null) {
        Expression locBlank = new Local(keyType);
        inner.Statements.Add(new AssignmentStatement(locKey, locBlank));
      }
      for( int i = 0; i < nGroupers; i++ ) {
        Member m = keyMembers[i];
        Expression x = gb.GroupList[i];
        inner.Statements.Add(new AssignmentStatement(new MemberBinding(locKey, m), x));
      }
      Expression locResult = new Local(SystemTypes.Object);
      Expression locNotFirst = new Local(SystemTypes.Boolean);
      Block brUpdate = new Block();
      Block brNew = new Block();
      if (locTable != null) {
        // locResult = ht[locGroup]
        Method mget = SystemTypes.Hashtable.GetMethod(StandardIds.getItem, SystemTypes.Object);
        MethodCall mcget = new MethodCall(new MemberBinding(locTable, mget), new ExpressionList(this.Box(locKey)));
        mcget.Type = mget.ReturnType;
        inner.Statements.Add(new AssignmentStatement(locResult, mcget));
        // if (locResult == null) goto brNew
        inner.Statements.Add(new Branch(new BinaryExpression(locResult, Literal.Null, NodeType.Eq), brNew));
        // locKey = (keyType)locResult
        inner.Statements.Add(new AssignmentStatement(locKey, this.Unbox(locResult, keyType)));
        inner.Statements.Add(new Branch(null, brUpdate));
      }
      else {
        locNotFirst = new Local(SystemTypes.Boolean);
        inner.Statements.Add(new Branch(locNotFirst, brUpdate));
      }
      // brNew
      inner.Statements.Add(brNew);
      // initialize aggregates
      for (int i = 0, n = gb.AggregateList.Count, k = nGroupers; i < n; i++, k++) {
        Field fAgg = keyMembers[k] as Field;
        QueryAggregate qa = gb.AggregateList[i] as QueryAggregate;
        if (fAgg == null || qa == null) continue;
        if (!qa.AggregateType.IsValueType) {
          Construct cons = new Construct();
          cons.Constructor = new MemberBinding(null, this.GetTypeView(qa.AggregateType).GetConstructor());
          cons.Type = qa.AggregateType;
          MemberBinding mbAgg = new MemberBinding(locKey, fAgg);
          mbAgg.Type = fAgg.Type;
          inner.Statements.Add(new AssignmentStatement(mbAgg, cons));
        }
        if (qa.Expression is QueryDistinct) {
          k++;
          Field fHash = keyMembers[k] as Field;
          Construct cons = new Construct();
          cons.Constructor = new MemberBinding(null, SystemTypes.Hashtable.GetConstructor());
          cons.Type = SystemTypes.Hashtable;
          MemberBinding mbHash = new MemberBinding(locKey, fHash);
          mbHash.Type = fHash.Type;
          inner.Statements.Add(new AssignmentStatement(mbHash, cons));
        }
      }
      // brUpdate
      inner.Statements.Add(brUpdate);
      // add aggregate values to aggregates
      Local locBoxedValue = new Local(SystemTypes.Object);
      for (int i = 0, n = gb.AggregateList.Count, k = nGroupers; i < n; i++, k++) {
        Field fAgg = keyMembers[k] as Field;
        QueryAggregate qa = gb.AggregateList[i] as QueryAggregate;
        if (fAgg == null || qa == null) continue;
        MemberBinding mbAgg = new MemberBinding(locKey, fAgg); mbAgg.Type = fAgg.Type;
        Method madd = this.GetTypeView(qa.AggregateType).GetMembersNamed(StandardIds.Add)[0] as Method;
        TypeNode paramType = madd.Parameters[0].Type;
        // add value to aggregate
        QueryDistinct qd = qa.Expression as QueryDistinct;
        if (qd != null) {
          k++;
          Field fHash = keyMembers[k] as Field;
          if (fHash == null) continue;
          Local locValue = new Local(paramType);
          MemberBinding mbHash = new MemberBinding(locKey, fHash); mbHash.Type = fHash.Type;
          Block brSkip = new Block();
          Block inner2 = new Block(new StatementList());
          inner.Statements.Add(this.IfNotNull(qd.Source, inner2));
          inner2.Statements.Add(new AssignmentStatement(locValue, this.typeSystem.ExplicitCoercion(qd.Source, paramType, this.TypeViewer)));
          inner2.Statements.Add(new AssignmentStatement(locBoxedValue, this.Box(locValue)));
          Method mcontains = SystemTypes.Hashtable.GetMethod(Identifier.For("Contains"), SystemTypes.Object);
          MethodCall mccontains = new MethodCall(new MemberBinding(mbHash, mcontains), new ExpressionList(locBoxedValue));
          mccontains.Type = mcontains.ReturnType;
          // if (hash.Contains(locBoxedValue)) goto brSkip;
          inner2.Statements.Add(new Branch(mccontains, brSkip));
          Method mhashset = SystemTypes.Hashtable.GetMethod(Identifier.For("set_Item"), SystemTypes.Object, SystemTypes.Object);
          MethodCall mchashset = new MethodCall(new MemberBinding(mbHash, mhashset), new ExpressionList(locBoxedValue, locBoxedValue));
          mchashset.Type = mhashset.ReturnType;
          // hash[locBoxedValue] = locBoxedValue;
          inner2.Statements.Add(new ExpressionStatement(mchashset));
          // aggregate.Add(locValue);
          MethodCall mcadd = new MethodCall(new MemberBinding(mbAgg, madd), new ExpressionList(locValue));
          mcadd.Type = madd.ReturnType;
          inner2.Statements.Add(new ExpressionStatement(mcadd));
          inner.Statements.Add(brSkip);
        }
        else {
          // aggregate.Add(value);
          MethodCall mcadd = new MethodCall(new MemberBinding(mbAgg, madd), new ExpressionList(this.typeSystem.ExplicitCoercion(qa.Expression, paramType, this.TypeViewer)));
          mcadd.Type = madd.ReturnType;
          inner.Statements.Add(this.IfNotNull(qa.Expression, new ExpressionStatement(mcadd)));
        }
      }
      Local locGroup = new Local(groupType);
      if (locTable != null) {
        // ht[locKey] = locKey;
        inner.Statements.Add(new AssignmentStatement(locResult, this.Box(locKey)));
        Method mset = SystemTypes.Hashtable.GetMethod(Identifier.For("set_Item"), SystemTypes.Object, SystemTypes.Object);
        MethodCall mcset = new MethodCall(new MemberBinding(locTable, mset), new ExpressionList(locResult, locResult));
        mcset.Type = mset.ReturnType;
        inner.Statements.Add(new ExpressionStatement(mcset));
        // iterate over all items in the hashtable
        Method getvals = SystemTypes.Hashtable.GetMethod(Identifier.For("get_Values"), null);
        MethodCall mcgetvals = new MethodCall(new MemberBinding(locTable, getvals), null);
        mcgetvals.Type = getvals.ReturnType;
        block.Statements.Add(this.BuildClosureForEach(mcgetvals, ref locResult, out inner, scope));
        inner.Statements.Add(new AssignmentStatement(locKey, this.Unbox(locResult, keyType)));
      }
      else {
        inner.Statements.Add(new AssignmentStatement(locNotFirst, Literal.True));
        inner = block;
      }
      // copy key results to return element
      for (int i = 0; i < nGroupers; i++) {
        Field keyField = keyMembers[i] as Field;
        Field groupField = members[i] as Field;
        if (keyField == null || groupField == null) continue;
        MemberBinding mbKey = new MemberBinding(locKey, keyField); mbKey.Type = keyField.Type;
        MemberBinding mbGroup = new MemberBinding(locGroup, groupField); mbGroup.Type = groupField.Type;
        inner.Statements.Add(new AssignmentStatement(mbGroup, mbKey));
      }
      for (int i = 0, n = gb.AggregateList.Count, k = nGroupers; i < n; i++, k++) {
        QueryAggregate qa = gb.AggregateList[i] as QueryAggregate;
        Field aggField = keyMembers[k] as Field;
        Field groupField = members[i + nGroupers] as Field;
        if (qa == null || aggField == null || groupField == null) continue;
        if (qa.Expression is QueryDistinct) k++;
        MemberBinding mbAgg = new MemberBinding(locKey, aggField); mbAgg.Type = aggField.Type;
        MemberBinding mbGroup = new MemberBinding(locGroup, groupField); mbGroup.Type = groupField.Type;
        Method mgetval = this.GetTypeView(qa.AggregateType).GetMethod(StandardIds.GetValue);
        MethodCall mcgetval = new MethodCall(new MemberBinding(mbAgg, mgetval), null);
        mcgetval.Type = mgetval.ReturnType;
        inner.Statements.Add(new AssignmentStatement(mbGroup, mcgetval));
      }
      // do having test
      if (gb.Having != null) {
        gb.HavingContext.Target = locGroup;
        Block brEndHaving = new Block();
        inner.Statements.Add(new Branch(new UnaryExpression(gb.Having, NodeType.LogicalNot), brEndHaving));
        inner.Statements.Add(new Yield(locGroup));
        inner.Statements.Add(brEndHaving);
      }
      else {
        inner.Statements.Add(new Yield(locGroup));
      }
      // normalize 
      return this.EndQueryClosure(closure, gb.Type);
    }
    public override Node VisitQueryInsert(QueryInsert qi) {
      if (qi == null || qi.Type == null || qi.Location == null || qi.Location.Type == null) return null;
      TypeNode sourceElementType = this.typeSystem.GetStreamElementType(qi.Location, this.TypeViewer);
      Block block = new Block(new StatementList(qi.InsertList.Count + 2));
      Expression source = null;
      if (qi.InsertList.Count == 1 && qi.InsertList[0].NodeType != NodeType.AssignmentExpression) {
        source = this.typeSystem.ImplicitCoercion(qi.InsertList[0], sourceElementType, this.TypeViewer);
      }
      else {
        source = qi.Context.Target = new Local(sourceElementType);
        if (!source.Type.IsValueType) {
          Construct cons = new Construct();
          cons.Constructor = new MemberBinding(null, this.GetTypeView(source.Type).GetConstructor());
          block.Statements.Add(new AssignmentStatement(source, cons));
        }
        for( int i = 0, n = qi.InsertList.Count; i < n; i++ ) {
          block.Statements.Add(new ExpressionStatement(qi.InsertList[i]));
        }
      }
      switch( qi.Position ) {
        case QueryInsertPosition.In: {
            Method m = this.GetTypeView(qi.Location.Type).GetMethod(StandardIds.Insert, SystemTypes.Int32, sourceElementType);
            Method mGetCount = (m != null)
              ? this.GetTypeView(
                  SystemTypes.GenericICollection.GetTemplateInstance(
                                                           this.currentType,
                                                           new TypeNodeList(sourceElementType))
                ).GetMethod(StandardIds.getCount)
              : null;

            if (m == null) {
              m = this.GetTypeView(qi.Location.Type).GetMethod(StandardIds.Insert, SystemTypes.Int32, SystemTypes.Object);
              mGetCount = SystemTypes.ICollection.GetMethod(StandardIds.Count);
            }
            if (m != null) {
              MethodCall mcGetCount = new MethodCall(new MemberBinding(qi.Location, mGetCount), new ExpressionList());
              mcGetCount.Type = mGetCount.ReturnType;
              if ((mGetCount.Flags & MethodFlags.Virtual) != 0)
                mcGetCount.NodeType = NodeType.Callvirt;
              MethodCall mc = new MethodCall(new MemberBinding(qi.Location, m), new ExpressionList(mcGetCount, source));
              mc.Type = m.ReturnType;
              if ((m.Flags & MethodFlags.Virtual) != 0)
                mc.NodeType = NodeType.Callvirt;
              block.Statements.Add(new ExpressionStatement(mc));
              block.Statements.Add(new ExpressionStatement(Literal.Int32One));
            }
            break;
          }
        case QueryInsertPosition.After:
        case QueryInsertPosition.At:
        case QueryInsertPosition.Before:
        case QueryInsertPosition.First:
        case QueryInsertPosition.Last:
          break;
      }
      return this.VisitBlockExpression(new BlockExpression(block, SystemTypes.Int32));
    }
    public override Node VisitQueryIterator(QueryIterator qi) {
      // creates an iterator that encapsulates the original items as named members
      Block body = null;
      Node closure = this.StartQueryClosure(qi.ElementType, "iterator", out body);
      Expression target = null;
      Block inner = null;
      body.Statements.Add(this.BuildClosureForEach(qi.Expression, ref target, out inner, body.Scope));
      Local loc = new Local(qi.ElementType);
      Field f = this.GetTypeView(qi.ElementType).GetMembersNamed(qi.Name)[0] as Field;
      MemberBinding mb = new MemberBinding(loc, f); mb.Type = f.Type;
      inner.Statements.Add(new AssignmentStatement(mb, target));
      inner.Statements.Add(new Yield(loc));
      return this.EndQueryClosure(closure, qi.Type);
    }
    public override Node VisitQueryJoin(QueryJoin qj) {
      TypeNode joinedType = this.typeSystem.GetStreamElementType(qj, this.TypeViewer);
      Block block = null;
      Node closure = this.StartQueryClosure(joinedType, "join", out block);
      BlockScope scope = block.Scope;
      Expression leftTarget = null;
      Block leftBody = null;
      Statement feLeft = this.BuildClosureForEach(qj.LeftOperand, ref leftTarget, out leftBody, scope);
      Expression rightTarget = null;
      Block rightBody = null;
      Statement feRight = this.BuildClosureForEach(qj.RightOperand, ref rightTarget, out rightBody, scope);
      Expression joined = this.NewClosureLocal(joinedType, scope);
      MemberList joinedMembers = this.typeSystem.GetDataMembers(joinedType);
      Debug.Assert(joinedMembers.Count > 1);
      Member leftMember = joinedMembers[0];
      Member rightMember = joinedMembers[1];
      MemberBinding mbLeft = new MemberBinding(joined, leftMember);
      MemberBinding mbRight = new MemberBinding(joined, rightMember);
      mbLeft.Type = this.typeSystem.GetMemberType(leftMember);
      mbRight.Type = this.typeSystem.GetMemberType(rightMember);
      Block onFalse = new Block();
      Block onExit = new Block();
      if (qj.JoinContext != null) qj.JoinContext.Target = joined;
      switch( qj.JoinType ) {
        case QueryJoinType.Inner:
          block.Statements.Add(feLeft);
          leftBody.Statements.Add(feRight);
          rightBody.Statements.Add(new AssignmentStatement(mbLeft, this.typeSystem.ImplicitCoercion(leftTarget, mbLeft.Type, this.TypeViewer)));
          rightBody.Statements.Add(new AssignmentStatement(mbRight, this.typeSystem.ImplicitCoercion(rightTarget, mbRight.Type, this.TypeViewer)));
          if (qj.JoinExpression != null) {
            rightBody.Statements.Add(new Branch(new UnaryExpression(qj.JoinExpression, NodeType.LogicalNot), onFalse));
            rightBody.Statements.Add(new Yield(joined));
            rightBody.Statements.Add(onFalse);
          }
          else {
            rightBody.Statements.Add(new Yield(joined));
          }
          break;
        case QueryJoinType.LeftOuter:
          block.Statements.Add(feLeft);
          Expression hadRight = this.NewClosureLocal(SystemTypes.Boolean, scope);
          leftBody.Statements.Add(new AssignmentStatement(hadRight, Literal.False));
          leftBody.Statements.Add(feRight);
          rightBody.Statements.Add(new AssignmentStatement(mbLeft, this.typeSystem.ImplicitCoercion(leftTarget, mbLeft.Type, this.TypeViewer)));
          rightBody.Statements.Add(new AssignmentStatement(mbRight, this.typeSystem.ImplicitCoercion(rightTarget, mbRight.Type, this.TypeViewer)));
          rightBody.Statements.Add(new Branch(new UnaryExpression(qj.JoinExpression, NodeType.LogicalNot), onFalse));
          rightBody.Statements.Add(new AssignmentStatement(hadRight, Literal.True));
          rightBody.Statements.Add(new Yield(joined));
          rightBody.Statements.Add(onFalse);
          leftBody.Statements.Add(new Branch(hadRight, onExit));
          leftBody.Statements.Add(new AssignmentStatement(mbRight, this.typeSystem.ImplicitCoercion(Literal.Null, mbRight.Type, this.TypeViewer)));
          leftBody.Statements.Add(new Yield(joined));
          leftBody.Statements.Add(onExit);
          break;
        case QueryJoinType.RightOuter:
          block.Statements.Add(feRight);
          Expression hadLeft = this.NewClosureLocal(SystemTypes.Boolean, scope);
          rightBody.Statements.Add(new AssignmentStatement(hadLeft, Literal.False));
          rightBody.Statements.Add(feLeft);
          leftBody.Statements.Add(new AssignmentStatement(mbLeft, this.typeSystem.ImplicitCoercion(leftTarget, mbLeft.Type, this.TypeViewer)));
          leftBody.Statements.Add(new AssignmentStatement(mbRight, this.typeSystem.ImplicitCoercion(rightTarget, mbRight.Type, this.TypeViewer)));
          leftBody.Statements.Add(new Branch(new UnaryExpression(qj.JoinExpression, NodeType.LogicalNot), onFalse));
          leftBody.Statements.Add(new AssignmentStatement(hadLeft, Literal.True));
          leftBody.Statements.Add(new Yield(joined));
          leftBody.Statements.Add(onFalse);
          rightBody.Statements.Add(new Branch(hadLeft, onExit));
          rightBody.Statements.Add(new AssignmentStatement(mbLeft, this.typeSystem.ImplicitCoercion(Literal.Null, mbLeft.Type, this.TypeViewer)));
          rightBody.Statements.Add(new Yield(joined));
          rightBody.Statements.Add(onExit);
          break;
        case QueryJoinType.FullOuter:
          break;
      }
      return this.EndQueryClosure(closure, qj.Type);
    }
    public override Node VisitQueryLimit(QueryLimit ql) {
      TypeNode resultElementType = this.typeSystem.GetStreamElementType(ql, this.TypeViewer);
      Block block = null;
      Node closure = this.StartQueryClosure(resultElementType, "top", out block);
      BlockScope scope = block.Scope;
      Expression source = ql.Source;
      Expression locLimit = this.NewClosureLocal(SystemTypes.Int32, scope);
      Expression locPosition = this.NewClosureLocal(SystemTypes.Int32, scope);
      Expression feTarget = null;
      Block inner = null;
      if (ql.IsPercent) {
        Expression locList = new Local(SystemTypes.ArrayList);
        Construct cons = new Construct();
        cons.Constructor = new MemberBinding(null, SystemTypes.ArrayList.GetConstructor());
        cons.Type = SystemTypes.ArrayList;
        block.Statements.Add(new AssignmentStatement(locList, cons));
        block.Statements.Add(this.BuildClosureForEach(source, ref feTarget, out inner, scope));
        // list.Add(item);
        Method madd = SystemTypes.ArrayList.GetMethod(StandardIds.Add, SystemTypes.Object);
        MethodCall mcAdd = new MethodCall(new MemberBinding(locList, madd), new ExpressionList(this.Box(feTarget)));
        mcAdd.Type = madd.ReturnType;
        inner.Statements.Add(new ExpressionStatement(mcAdd));
        source = locList;
        // percent = expr / 100
        BinaryExpression percent = new BinaryExpression(this.typeSystem.ExplicitCoercion(ql.Expression, SystemTypes.Double, this.TypeViewer), new Literal(100.0, SystemTypes.Double), NodeType.Div);
        percent.Type = SystemTypes.Double;
        // limit = count * percent
        Method mgetcount = SystemTypes.ArrayList.GetMethod(Identifier.For("get_Count"));
        MethodCall mcgetcount = new MethodCall(new MemberBinding(null, mgetcount), new ExpressionList(locList));
        mcgetcount.Type = mgetcount.ReturnType;
        BinaryExpression limit = new BinaryExpression(this.typeSystem.ExplicitCoercion(mcgetcount, SystemTypes.Double, this.TypeViewer), percent, NodeType.Mul);
        limit.Type = SystemTypes.Double;        
        block.Statements.Add(new AssignmentStatement(locLimit, this.typeSystem.ExplicitCoercion(limit, SystemTypes.Int32, this.TypeViewer)));
        Block brGoodLimit = new Block();
        block.Statements.Add(new Branch(new BinaryExpression(locLimit, Literal.Int32Zero, NodeType.Gt), brGoodLimit));
        block.Statements.Add(new AssignmentStatement(locLimit, Literal.Int32One));
        block.Statements.Add(brGoodLimit);
      }
      else {
        block.Statements.Add(new AssignmentStatement(locLimit, this.typeSystem.ExplicitCoercion(ql.Expression, SystemTypes.Int32, this.TypeViewer)));
      }
      Block exit = new Block();
      block.Statements.Add(this.BuildClosureForEach(source, ref feTarget, out inner, scope));
      inner.Statements.Add(new Yield(feTarget));
      inner.Statements.Add(new AssignmentStatement(locPosition, new BinaryExpression(locPosition, Literal.Int32One, NodeType.Add)));
      inner.Statements.Add(new Branch(new BinaryExpression(locPosition, locLimit, NodeType.Ge), exit));
      block.Statements.Add(exit);
      return this.EndQueryClosure(closure, ql.Type);
    }
    public override Node VisitQueryOrderBy(QueryOrderBy qo) {
      // generate translation code
      Block block = null;
      Node closure = this.StartQueryClosure(this.typeSystem.GetStreamElementType(qo, this.TypeViewer), "orderby", out block);
      BlockScope scope = block.Scope;

      // construct list for sorting
      Local locList = new Local(SystemTypes.ArrayList);
      Construct cons = new Construct();
      cons.Constructor = new MemberBinding(null, SystemTypes.ArrayList.GetConstructor());
      cons.Type = SystemTypes.ArrayList;
      block.Statements.Add(new AssignmentStatement(locList, cons));
      
      // build key type
      int len = qo.OrderList.Count;
      FieldList fields = new FieldList(len + 1);
      ArrayList orderTypes = new ArrayList(len);
      ExpressionList expressions = new ExpressionList(len);      
      for( int i = 0; i < len; i++ ) {
        Expression x = qo.OrderList[i];
        QueryOrderType ot = QueryOrderType.Ascending;
        QueryOrderItem order = x as QueryOrderItem;
        if (x != null) {
          ot = order.OrderType;
          x = order.Expression;
        }
        Field f = new Field(null, new AttributeList(1), FieldFlags.Public, Identifier.For("Item"+i), x.Type, null);
        f.Attributes.Add(new AttributeNode(new MemberBinding(null, SystemTypes.AnonymousAttribute.GetConstructor()), null));
        fields.Add(f);
        orderTypes.Add(ot);
        expressions.Add(x);
      }
      Field fRow = new Field(null, new AttributeList(1), FieldFlags.Public, Identifier.For("Item"+len), SystemTypes.Object, null);
      fRow.Attributes.Add(new AttributeNode(new MemberBinding(null, SystemTypes.AnonymousAttribute.GetConstructor()), null));
      fields.Add(fRow);
      
      // construct key type
      TypeNode keyType = TupleType.For(fields, this.currentType);
      
      // build comparer for key
      MemberList members = this.typeSystem.GetDataMembers(keyType);
      fRow = (Field)members[len];
      TypeNode comparerType = this.BuildComparer(keyType, members, (QueryOrderType[])orderTypes.ToArray(typeof(QueryOrderType)), len);
      block.Statements.Add(new QueryGeneratedType(comparerType));

      // iterate rows
      Expression feTarget = null;
      Block inner = null;
      block.Statements.Add(this.BuildClosureForEach(qo.Source, ref feTarget, out inner, scope));
      qo.Context.Target = feTarget;
      
      // prepare key instance
      Expression locKey = new Local(keyType);
      for( int i = 0; i < len; i++ ) {
        Expression x = expressions[i];
        Field f = (Field) members[i];
        inner.Statements.Add(new AssignmentStatement(new MemberBinding(locKey, f), x));
      }
      inner.Statements.Add(new AssignmentStatement(new MemberBinding(locKey, fRow), this.Box(feTarget)));

      // add key to the list
      Method madd = SystemTypes.ArrayList.GetMethod(StandardIds.Add, SystemTypes.Object);
      MethodCall mcAdd = new MethodCall(new MemberBinding(locList, madd), new ExpressionList(this.Box(locKey)));
      mcAdd.Type = madd.ReturnType;
      inner.Statements.Add(new ExpressionStatement(mcAdd));

      // sort the list, now outside original foreach
      Method mthSort = SystemTypes.ArrayList.GetMethod(Identifier.For("Sort"), SystemTypes.IComparer);
      Construct consc = new Construct();
      consc.Constructor = new MemberBinding(null, this.GetTypeView(comparerType).GetConstructor());
      consc.Type = comparerType;
      MethodCall mcSort = new MethodCall(new MemberBinding(locList, mthSort), new ExpressionList(consc));
      mcSort.Type = mthSort.ReturnType;
      block.Statements.Add(new ExpressionStatement(mcSort));

      // now iterate over sorted results
      block.Statements.Add(this.BuildClosureForEach(locList, ref locKey, out inner, scope));
      
      // get row out of key
      inner.Statements.Add(new Yield(this.Unbox(new MemberBinding(locKey, fRow), feTarget.Type)));

      return this.EndQueryClosure(closure, qo.Type);
    }
    private TypeNode BuildDefaultComparer(TypeNode type) {
      if (type == null || this.GetTypeView(type).Members == null) return null;
      MemberList dataMembers = this.typeSystem.GetDataMembers(type);
      return this.BuildComparer(type, dataMembers, null);
    }
    private TypeNode BuildComparer(TypeNode type, MemberList members, QueryOrderType[] orders) {
      return this.BuildComparer(type, members, orders, members.Count);
    }
    private TypeNode BuildComparer(TypeNode type, MemberList members, QueryOrderType[] orders, int n) {
      Debug.Assert(members != null && n > 0 && n <= members.Count);
      Identifier name = Identifier.For("comparer"+type.UniqueKey);
      Class cc = new Class();
      cc.DeclaringModule = this.currentModule;
      cc.DeclaringType = this.currentType;
      cc.Flags = TypeFlags.NestedPublic;
      cc.Namespace = Identifier.Empty;
      cc.Name = name;
      cc.BaseClass = SystemTypes.Object;
      cc.Interfaces = new InterfaceList(2);
      cc.Interfaces.Add(SystemTypes.IComparer);
      cc.Interfaces.Add(SystemTypes.IHashCodeProvider);

      // constructor
      Method init = new InstanceInitializer(cc, null, new ParameterList(), new Block(new StatementList(1)));
      init.Flags |= MethodFlags.Public;
      cc.Members.Add(init);
      Method mthBaseCons = SystemTypes.Object.GetMethod(StandardIds.Ctor);
      MethodCall mcBase = new MethodCall(new MemberBinding(init.ThisParameter, mthBaseCons), null);
      mcBase.Type = SystemTypes.Void;
      init.Body.Statements.Add(new ExpressionStatement(mcBase));
      
      // CompareTo
      Method mcomp = new Method(cc, null, Identifier.For("Compare"), new ParameterList(2), SystemTypes.Int32, new Block(new StatementList()));
      mcomp.Flags = MethodFlags.Public|MethodFlags.Virtual|MethodFlags.Final;
      mcomp.CallingConvention = CallingConventionFlags.HasThis;
      mcomp.Parameters.Add(new Parameter(null, ParameterFlags.In, Identifier.For("p1"), SystemTypes.Object, null, null));
      mcomp.Parameters.Add(new Parameter(null, ParameterFlags.In, Identifier.For("p2"), SystemTypes.Object, null, null));
      cc.Members.Add(mcomp);

      // GetHashCode(obj)
      Method mhash = new Method(cc, null, Identifier.For("GetHashCode"), new ParameterList(1), SystemTypes.Int32, new Block(new StatementList()));
      mhash.Flags = MethodFlags.Public|MethodFlags.Virtual|MethodFlags.Final;
      mhash.CallingConvention = CallingConventionFlags.HasThis;
      mhash.Parameters.Add(new Parameter(null, ParameterFlags.In, Identifier.For("p1"), SystemTypes.Object, null, null));
      cc.Members.Add(mhash);

      // call type specific comparison
      Method mcomptype = this.BuildComparisonMethod(cc, type, members, orders, n);
      MethodCall mccomptype = new MethodCall(
        new MemberBinding(null, mcomptype), 
        new ExpressionList(this.Unbox(mcomp.Parameters[0], type), this.Unbox(mcomp.Parameters[1], type))
        );
      mccomptype.Type = mcomptype.ReturnType;
      Block brNotEqual = new Block();
      mcomp.Body.Statements.Add(new Branch(new BinaryExpression(mcomp.Parameters[0], mcomp.Parameters[1], NodeType.Ne), brNotEqual));
      mcomp.Body.Statements.Add(new Return(Literal.Int32Zero));
      mcomp.Body.Statements.Add(brNotEqual);
      mcomp.Body.Statements.Add(new Return(mccomptype));

      this.BuildGetHashCodeBody(mhash, type, members, n);
      return cc;
    }
    private void BuildGetHashCodeBody(Method method, TypeNode type, MemberList members, int n) {
      StatementList stats = method.Body.Statements;
      Parameter p1 = method.Parameters[0];
      Expression loc = null;
      if (method.Parameters[0].Type == SystemTypes.Object) {
        loc = new Local(type);
        stats.Add(new AssignmentStatement(loc, this.Unbox(p1, type)));
      }
      else {
        loc = p1;
      }
      Expression retval = new Local(SystemTypes.Int32);
      stats.Add(new AssignmentStatement(retval, Literal.Int32Zero));
      for(int i = 0; i < n; i++) {
        Member m = members[i];
        TypeNode mtype = this.typeSystem.GetMemberType(m);
        MemberBinding mb = new MemberBinding(loc, m); mb.Type = mtype;
        this.BuildMemberHash(stats, mb, retval);
      }
      stats.Add(new Return(retval));
    }
    private void BuildMemberHash(StatementList stats, Expression v, Expression retval){
      if (this.GetTypeView(v.Type).IsAssignableTo(SystemTypes.IComparable)){  
        Method mhash = this.GetTypeView(v.Type).GetMethod(StandardIds.GetHashCode);
        MethodCall mchash = new MethodCall(new MemberBinding(v, mhash), null);
        mchash.Type = mhash.ReturnType;
        if (!v.Type.IsValueType) mchash.NodeType = NodeType.Callvirt;
        stats.Add(new AssignmentStatement(retval, new BinaryExpression(retval, this.VisitMethodCall(mchash), NodeType.Add)));
      }else{
        // todo: handle recursive case
      }
    }
    private static readonly Identifier idCompareType = Identifier.For("CompareType");
    private Method BuildComparisonMethod(TypeNode host, TypeNode type) {
      Method method = this.GetTypeView(host).GetMethod(idCompareType, type, type);
      if (method != null) return method;
      MemberList members = this.typeSystem.GetDataMembers(type);
      return this.BuildComparisonMethod(host, type, members, null, members.Count);
    }
    private Method BuildComparisonMethod(TypeNode host, TypeNode type, MemberList members, QueryOrderType[] orders, int n) {
      Method method = new Method(host, null, idCompareType, new ParameterList(2), SystemTypes.Int32, new Block(new StatementList()));
      method.Flags = MethodFlags.Private|MethodFlags.Static;
      method.Parameters.Add(new Parameter(null, ParameterFlags.In, Identifier.For("p1"), type, null, null));
      method.Parameters.Add(new Parameter(null, ParameterFlags.In, Identifier.For("p2"), type, null, null));
      host.Members.Add(method);
      StatementList stats = method.Body.Statements;
      Expression loc1 = method.Parameters[0];
      Expression loc2 = method.Parameters[1];
      if (!type.IsValueType) {
        Block brNotEqual = new Block();
        stats.Add(new Branch(new BinaryExpression(loc1, loc2, NodeType.Ne), brNotEqual));
        stats.Add(new Return(Literal.Int32Zero));
        stats.Add(brNotEqual);
      }
      // generate comparison logic for each member in the list
      Local retval = new Local(SystemTypes.Int32);
      for(int i = 0; i < n; i++) {
        Member m = members[i];
        TypeNode mtype = this.typeSystem.GetMemberType(m);
        QueryOrderType ot = (orders != null) ? orders[i] : QueryOrderType.Ascending;
        MemberBinding m1 = new MemberBinding(loc1, m); m1.Type = mtype;
        MemberBinding m2 = new MemberBinding(loc2, m); m2.Type = mtype;
        this.BuildMemberComparison(host, stats, m1, m2, ot, retval);
        Block next = new Block();
        stats.Add(new Branch(new BinaryExpression(retval, Literal.Int32Zero, NodeType.Eq), next));
        if (ot == QueryOrderType.Descending) {
          UnaryExpression ue = new UnaryExpression(retval, NodeType.Neg);
          ue.Type = SystemTypes.Int32;
          stats.Add(new Return(ue));
        }
        else {
          stats.Add(new Return(retval));
        }
        stats.Add(next);
      }
      stats.Add(new Return(Literal.Int32Zero));
      return method;
    }
    private void BuildMemberComparison(TypeNode host, StatementList stats, Expression v1, Expression v2, QueryOrderType ot, Expression retval) {
      Debug.Assert(stats != null && v1 != null && v2 != null && v1.Type != null && v2.Type != null, "BuildComparison");
      Debug.Assert(v1.Type == v2.Type, "Comparison types must match");
      NodeType gt = (ot == QueryOrderType.Ascending) ? NodeType.Gt : NodeType.Lt;
      NodeType lt = (ot == QueryOrderType.Ascending) ? NodeType.Lt : NodeType.Gt;
      // quick check identity equality
      Cardinality card = this.typeSystem.GetCardinality(v1, this.TypeViewer);
      switch (card) {
        case Cardinality.One:
          if (v1.Type.Template == SystemTypes.GenericNonNull) {
            v1 = this.GetValueExpression(v1);
            v2 = this.GetValueExpression(v2);
          }
          break;
        case Cardinality.None:
        case Cardinality.ZeroOrOne:
          Expression v1IsNull = this.GetIsNullExpression(v1);
          Expression v2IsNull = this.GetIsNullExpression(v2);
          Block brV1IsNull = new Block();
          Block brV2IsNull = new Block();
          Block brBothAreNull = new Block();
          Block brNeitherAreNull = new Block();
          stats.Add(new Branch(v1IsNull, brV1IsNull));
          stats.Add(new Branch(v2IsNull, brV2IsNull));
          stats.Add(new Branch(null, brNeitherAreNull));
          stats.Add(brV1IsNull);
          stats.Add(new Branch(v2IsNull, brBothAreNull));
          stats.Add(new Return(Literal.Int32MinusOne));
          stats.Add(brV2IsNull);
          stats.Add(new Return(Literal.Int32One));
          stats.Add(brBothAreNull);
          stats.Add(new Return(Literal.Int32Zero));
          stats.Add(brNeitherAreNull);
          v1 = this.GetNonNullExpression(v1);
          v2 = this.GetNonNullExpression(v2);
          break;
        case Cardinality.OneOrMore:
        case Cardinality.ZeroOrMore:
          // todo: deep comparison of streams
          return;
      }
      if (this.GetTypeView(v1.Type).IsAssignableTo(SystemTypes.IComparable)) {
        Method mth = this.GetTypeView(v1.Type).GetMethod(Identifier.For("CompareTo"), SystemTypes.Object);
        MethodCall mc = new MethodCall(new MemberBinding(v1, mth), new ExpressionList(this.typeSystem.ImplicitCoercion(v2, SystemTypes.Object, this.TypeViewer)));
        mc.Type = mth.ReturnType;
        stats.Add(new AssignmentStatement(retval, this.VisitMethodCall(mc)));
      }else{
        Method m = this.BuildComparisonMethod(host, v1.Type);
        MethodCall mc = new MethodCall(new MemberBinding(null, m), new ExpressionList(v1, v2));
        mc.Type = m.ReturnType;
        stats.Add(new AssignmentStatement(retval, mc));
      }
    }
    public override Node VisitQueryPosition(QueryPosition qp) {
      if (qp.Context != null) {
        if (qp.Context.Position == null && qp.Context.PreFilter != null && qp.Context.PostFilter != null) {
          qp.Context.Position = this.NewClosureLocal(SystemTypes.Int32, this.currentMethod.Body.Scope);
          qp.Context.PreFilter.Statements.Add(new AssignmentStatement(qp.Context.Position, Literal.Int32Zero));
          BinaryExpression badd = new BinaryExpression(qp.Context.Position, Literal.Int32One, NodeType.Add);
          badd.Type = SystemTypes.Int32;
          qp.Context.PostFilter.Statements.Add(new AssignmentStatement(qp.Context.Position, badd));
        }
        return this.VisitExpression(qp.Context.Position);
      }
      return Literal.Int32Zero;
    }
    public override Node VisitQueryProject(QueryProject qp) {
      // generate translation code
      Block block = null;
      Node closure = this.StartQueryClosure(qp.ProjectedType, "select", out block);
      BlockScope scope = block.Scope;
      Expression feTarget = null;
      Block feBody = null;
      block.Statements.Add(this.BuildClosureForEach(qp.Source, ref feTarget, out feBody, scope));
      Block inner = new Block(new StatementList(qp.ProjectionList.Count + 1));
      feBody.Statements.Add(this.IfNotNull(feTarget, inner));
      Expression projectedTarget = this.NewClosureLocal(qp.ProjectedType, scope);
      qp.Context.Target = feTarget;
      if( qp.ProjectionList.Count == 1 && projectedTarget.Type.UniqueKey == qp.ProjectionList[0].Type.UniqueKey ) {
        // case of scalar projection
        Expression x = qp.ProjectionList[0];
        inner.Statements.Add(new AssignmentStatement(projectedTarget, x));
      }
      else if( qp.ProjectionList.Count > 0 ) {
        // create projected instance
        if (!qp.ProjectedType.IsValueType) {
          Construct cons = new Construct();
          cons.Constructor = new MemberBinding(null, this.GetTypeView(qp.ProjectedType).GetConstructor());
          cons.Type = qp.ProjectedType;
          inner.Statements.Add(new AssignmentStatement(projectedTarget, cons));
        }
        // non-scalar projection
        Debug.Assert( qp.Members != null && qp.Members.Count == qp.ProjectionList.Count, "Projection length mismatch" );
        for( int i = 0, n = qp.Members.Count; i < n; i++ ) {
          Member m = qp.Members[i];
          TypeNode mtype = this.typeSystem.GetMemberType(m);
          MemberBinding mb = new MemberBinding(projectedTarget, m); mb.Type = mtype;
          Expression x = qp.ProjectionList[i];
          inner.Statements.Add(new AssignmentStatement(mb, this.typeSystem.ImplicitCoercion(x, mtype, this.TypeViewer)));
        }
      }
      inner.Statements.Add(new Yield(projectedTarget));
      // normalize
      return this.EndQueryClosure(closure, qp.Type);
    }
    public override Node VisitQueryQuantifier(QueryQuantifier qq) {
      return qq.Target;
    }
    public override Node VisitQueryQuantifiedExpression(QueryQuantifiedExpression qqe) {
      Block block = new Block(new StatementList(4));
      Expression locResult = new Local(qqe.Type);
      Expression retTrue = this.typeSystem.ImplicitCoercion(Literal.True, qqe.Type, this.TypeViewer);
      Expression retFalse = this.typeSystem.ImplicitCoercion(Literal.False, qqe.Type, this.TypeViewer);
      Statement feOuter = null;
      Statement feInner = null;
      Block outerBody = null;
      Block innerBody = null;
      BlockScope scope = this.currentMethod.Body.Scope;
      QueryQuantifier outer = null;
      QueryQuantifier inner = null;
      if (qqe.Left != null && qqe.Right != null) {
        outer = qqe.Left;
        inner = qqe.Right;
        outer.Target = new Local(outer.Type);
        feOuter = this.BuildClosureForEach(outer.Expression, ref outer.Target, out outerBody, scope);
        inner.Target = new Local(inner.Type);
        feInner = this.BuildClosureForEach(inner.Expression, ref inner.Target, out innerBody, scope);
        outerBody.Statements.Add(feInner);
        block.Statements.Add(feOuter);
      }
      else if (qqe.Left != null) {
        inner = qqe.Left;
        inner.Target = new Local(inner.Type);
        feInner = this.BuildClosureForEach(qqe.Left.Expression, ref inner.Target, out innerBody, scope);
        block.Statements.Add(feInner);
      }
      else if (qqe.Right != null) {
        inner = qqe.Right;
        inner.Target = new Local(inner.Type);
        feInner = this.BuildClosureForEach(inner.Expression, ref inner.Target, out innerBody, scope);
        block.Statements.Add(feInner);
      }
      else {
        // error condition;
        return Literal.False;
      }
      Block onTrue = new Block(new StatementList(new AssignmentStatement(locResult, retTrue)));
      Block onFalse = new Block(new StatementList(new AssignmentStatement(locResult, retFalse)));
      Block exit = new Block();
      Expression test = qqe.Expression;
      if (outer == null || outer.NodeType == inner.NodeType) {
        if (inner.NodeType == NodeType.QueryAny) {
          innerBody.Statements.Add(new Branch(test, onTrue));
          block.Statements.Add(new Branch(null, onFalse));
        }
        else {
          test = new UnaryExpression(test, NodeType.LogicalNot);
          test.Type = SystemTypes.Boolean;
          innerBody.Statements.Add(new Branch(test, onFalse));
          block.Statements.Add(new Branch(null, onTrue));
        }
      }
      else {
        if (outer.NodeType == NodeType.QueryAll) {
          Block bottom = new Block();
          innerBody.Statements.Add(new Branch(test, bottom));
          outerBody.Statements.Add(new Branch(null, onFalse));
          outerBody.Statements.Add(bottom);
          block.Statements.Add(new Branch(null, onTrue));
        }
        else {
          test = new UnaryExpression(test, NodeType.LogicalNot);
          test.Type = SystemTypes.Boolean;
          Block bottom = new Block();
          innerBody.Statements.Add(new Branch(test, bottom));
          outerBody.Statements.Add(new Branch(null, onTrue));
          outerBody.Statements.Add(bottom);
          block.Statements.Add(new Branch(null, onFalse));
        }
      }
      block.Statements.Add(onTrue);
      block.Statements.Add(new Branch(null, exit));
      block.Statements.Add(onFalse);
      block.Statements.Add(exit);
      block.Statements.Add(new ExpressionStatement(locResult));
      BlockExpression be = new BlockExpression(block, locResult.Type);
      return this.VisitBlockExpression(be);
    }
    public override Node VisitQuerySelect(QuerySelect qs) {
      return this.Visit(qs.Source);
    }
    public override Node VisitQuerySingleton(QuerySingleton qs) {
      Block block = new Block(new StatementList(4));
      Expression feTarget = null;
      Block inner = null;
      BlockScope scope = this.currentMethod.Body.Scope;
      Expression result = new Local(qs.Type);
      Expression hasOne = new Local(SystemTypes.Boolean);
      Block brError = new Block();
      Block brReturn = new Block();
      block.Statements.Add(new AssignmentStatement(hasOne, Literal.False));
      block.Statements.Add(this.BuildClosureForEach(qs.Source, ref feTarget, out inner, scope));
      inner.Statements.Add(new Branch(hasOne, brError));
      inner.Statements.Add(new AssignmentStatement(result, feTarget));
      inner.Statements.Add(new AssignmentStatement(hasOne, Literal.True));
      block.Statements.Add(new Branch(hasOne, brReturn));
      block.Statements.Add(brError);
      Construct cons = new Construct();
      cons.Constructor = new MemberBinding(null, SystemTypes.StreamNotSingletonException.GetConstructor());
      block.Statements.Add(new Throw(cons));
      block.Statements.Add(brReturn);
      block.Statements.Add(new ExpressionStatement(result));
      return this.VisitBlockExpression(new BlockExpression(block, result.Type));
    }
    private static readonly Identifier idSetItem = Identifier.For("set_Item");
    public override Node VisitQueryUpdate(QueryUpdate qu) {
      if (qu == null || qu.Type == null || qu.Source == null || qu.Source.Type == null) return null;
      TypeNode elementType = this.typeSystem.GetStreamElementType(qu.Source, this.TypeViewer);
      Block block = new Block(new StatementList(10));
      Expression setSource = null;
      QueryFilter qf = qu.Source as QueryFilter;
      if (qf != null) {
        if (qf.Source == null || qf.Source.Type == null) return null;
        if (!this.IsLocal(qf.Source)) {
          Local locSource = new Local(qf.Source.Type);
          block.Statements.Add(new AssignmentStatement(locSource, qf.Source));
          qf.Source = locSource;
        }
        setSource = qf.Source;
      }
      else {
        if (!this.IsLocal(qu.Source)) {
          Local locSource = new Local(qu.Source.Type);
          block.Statements.Add(new AssignmentStatement(locSource, qu.Source));
          qu.Source = locSource;
        }
        setSource = qu.Source;
      }
      Method mSetItem = null;
      Method mAdd = null;
      Method mRemove = null;
      Local locPos = null;
      if (elementType.IsValueType) {
        mSetItem = this.GetTypeView(setSource.Type).GetMethod(idSetItem, SystemTypes.Int32, elementType);
        if (mSetItem == null) {
          mAdd = this.GetTypeView(setSource.Type).GetMethod(StandardIds.Add, elementType);
          if (mAdd == null) mAdd = this.GetTypeView(setSource.Type).GetMethod(StandardIds.Add, SystemTypes.Object);
          if (mAdd == null) return null;
          mRemove = this.GetTypeView(setSource.Type).GetMethod(StandardIds.Remove, elementType);
          if (mRemove == null) mRemove = this.GetTypeView(setSource.Type).GetMethod(StandardIds.Remove, SystemTypes.Object);
          if (mRemove == null) return null;
        }
        locPos = new Local(SystemTypes.Int32);
        if (qf != null && mSetItem != null) {
          // pre-prepare position counter for filter
          qf.Context.Position = locPos;
          qf.Context.PreFilter = new Block(new StatementList(1));
          qf.Context.PostFilter = new Block(new StatementList(1));
          qf.Context.PreFilter.Statements.Add(new AssignmentStatement(locPos, Literal.Int32Zero));
          qf.Context.PostFilter.Statements.Add(new AssignmentStatement(locPos, new BinaryExpression(locPos, Literal.Int32One, NodeType.Add)));
        }
      }
      Block inner = null;
      Expression target = null;
      if (mAdd != null) {
        Local locList = new Local(SystemTypes.ArrayList);
        Construct cons = new Construct(new MemberBinding(null, SystemTypes.ArrayList.GetConstructor()), null, SystemTypes.ArrayList);
        block.Statements.Add(new AssignmentStatement(locList, cons));
        // find all matching elements
        block.Statements.Add(this.BuildClosureForEach(qu.Source, ref target, out inner, this.currentMethod.Body.Scope));
        qu.Context.Target = target;
        // remember all the matching elements
        Method mListAdd = SystemTypes.ArrayList.GetMethod(StandardIds.Add, SystemTypes.Object);
        MethodCall mcListAdd = new MethodCall(new MemberBinding(locList, mListAdd), new ExpressionList(this.typeSystem.ImplicitCoercion(target, SystemTypes.Object, this.TypeViewer)));
        mcListAdd.Type = mListAdd.ReturnType;
        inner.Statements.Add(new ExpressionStatement(mcListAdd));
        // re-iterate matching values
        block.Statements.Add(this.BuildClosureForEach(locList, ref target, out inner, this.currentMethod.Body.Scope));
        // remove old value from list
        Expression remArg = (mRemove.Parameters[0].Type == SystemTypes.Object) ? this.Box(target) : target;
        MethodCall mcRemove = new MethodCall(new MemberBinding(setSource, mRemove), new ExpressionList(remArg));
        mcRemove.Type = mRemove.ReturnType;
        if ((mRemove.Flags & MethodFlags.Virtual) != 0) mcRemove.NodeType = NodeType.Callvirt;
        inner.Statements.Add(new ExpressionStatement(mcRemove));
        // apply changes
        for( int i = 0, n = qu.UpdateList.Count; i < n; i++ ) {
          inner.Statements.Add(new ExpressionStatement(qu.UpdateList[i]));
        }
        // add changed item back
        Expression addArg = (mAdd.Parameters[0].Type == SystemTypes.Object) ? this.Box(target) : target;
        MethodCall mcAdd = new MethodCall(new MemberBinding(setSource, mAdd), new ExpressionList(addArg));
        mcAdd.Type = mAdd.ReturnType;
        if ((mAdd.Flags & MethodFlags.Virtual) != 0) mcAdd.NodeType = NodeType.Callvirt;
        inner.Statements.Add(new ExpressionStatement(mcAdd));

        // return update count
        Method mGetCount = SystemTypes.ArrayList.GetMethod(idGetCount);
        MethodCall mcGetCount = new MethodCall(new MemberBinding(locList, mGetCount), null);
        mcGetCount.Type = mGetCount.ReturnType;
        block.Statements.Add(new ExpressionStatement(mcGetCount));
      }
      else {
        Local locCount = new Local(SystemTypes.Int32);
        block.Statements.Add(new AssignmentStatement(locCount, Literal.Int32Zero));
        block.Statements.Add(this.BuildClosureForEach(qu.Source, ref target, out inner, this.currentMethod.Body.Scope));
        qu.Context.Target = target;
        // apply changes
        for( int i = 0, n = qu.UpdateList.Count; i < n; i++ ) {
          inner.Statements.Add(new ExpressionStatement(qu.UpdateList[i]));
        }
        if (mSetItem != null) {
          // put changed item back
          Expression pos = (qf != null) ? locPos : locCount;
          MethodCall mcSetItem = new MethodCall(new MemberBinding(setSource, mSetItem), new ExpressionList(pos, target));
          mcSetItem.Type = mSetItem.ReturnType;
          if ((mSetItem.Flags & MethodFlags.Virtual) != 0)
            mcSetItem.NodeType = NodeType.Callvirt;
          inner.Statements.Add(new ExpressionStatement(mcSetItem));
        }
        inner.Statements.Add(new AssignmentStatement(locCount, new BinaryExpression(locCount, Literal.Int32One, NodeType.Add)));
        block.Statements.Add(new ExpressionStatement(locCount));
      }
      // return count
      return this.VisitBlockExpression(new BlockExpression(block, SystemTypes.Int32));
    }
    public override Node VisitQueryYielder(QueryYielder qy) {
      bool savedFoldQuery = this.foldQuery;
      this.foldQuery = true;
      Block feBlock = (Block) this.Visit(qy.Source);
      this.foldQuery = false;
      Yielder yielder = new Yielder(this.typeSystem, this.TypeViewer);
      return this.Visit(yielder.YieldTo(feBlock, qy.Body, qy.Target, qy.State));
    }
    public class Yielder: Visitor {
      Block yieldToBlock;
      Expression target;
      Expression state;
      BlockList reentryPoints;
      TypeSystem typeSystem;
      TypeViewer typeViewer;
      Passer passer;
      public Yielder(TypeSystem typeSystem, TypeViewer typeViewer) {
        this.typeSystem = typeSystem;
        this.typeViewer = typeViewer;
        this.passer = new Passer(this);
      }
      public Block YieldTo(Block source, Block yieldTo, Expression target, Expression state) {
        this.yieldToBlock = yieldTo;
        this.target = target;
        this.state = state;
        this.reentryPoints = new BlockList(1);
        Block block = new Block(new StatementList(10));
        Block top = new Block();
        Block start = new Block();
        Block end = new Block();
        this.reentryPoints.Add(start);
        block.Statements.Add(new AssignmentStatement(state, Literal.Int32Zero));
        block.Statements.Add(top);
        block.Statements.Add(new SwitchInstruction(state, this.reentryPoints));
        block.Statements.Add(new Branch(null, end));
        block.Statements.Add(start);
        block.Statements.Add((Block) this.Visit(source));
        block.Statements.Add(new Branch(null, end));
        block.Statements.Add(yieldTo);
        block.Statements.Add(new Branch(null, top));
        block.Statements.Add(end);
        return block;
      }
      public override Node Visit(Node node) {
        if (node == null) return null;
        switch (node.NodeType) {
          case NodeType.Composition:
          case NodeType.AnonymousNestedFunction:
            // exclude anything that might have its own yielding scope
            return node;
          case NodeType.Yield:
            Yield _yield = (Yield)node;
            Block block = new Block(new StatementList(4));
            Block reentryPoint = new Block();
            block.Statements.Add(new AssignmentStatement(this.target, this.typeSystem.ImplicitCoercion(_yield.Expression, this.target.Type, this.typeViewer)));
            block.Statements.Add(new AssignmentStatement(this.state, new Literal(this.reentryPoints.Count, SystemTypes.Int32)));
            block.Statements.Add(new Branch(Literal.True, this.yieldToBlock));
            block.Statements.Add(reentryPoint);
            this.reentryPoints.Add(reentryPoint);
            return block;
          default:
            return this.passer.Visit(node);
        }
      }
    }
    public override Node VisitQueryCommit(QueryCommit qc) {
      if (qc == null) return null;
      if (this.currentTransaction == null) return null;
      return this.VisitBranch(new Branch(null, this.currentTransaction.CommitBody));
    }
    public override Node VisitQueryRollback(QueryRollback qr) {
      if (qr == null) return null;
      return this.VisitBranch(new Branch(null, this.currentTransaction.RollbackBody));
    }
    private static readonly Identifier idCommit = Identifier.For("Commit");
    private static readonly Identifier idRollback = Identifier.For("Rollback");
    private static readonly Identifier idBeginTransaction = Identifier.For("BeginTransaction");
    public override Node VisitQueryTransact(QueryTransact qt) {
      if (qt == null || qt.Source == null || qt.Source.Type == null) return null;
      Block block = new Block(new StatementList(10));
      qt.Transaction = this.NewClosureLocal(SystemTypes.IDbTransaction, this.currentMethod.Body.Scope);
      Expression locCommit = this.NewClosureLocal(SystemTypes.Boolean, this.currentMethod.Body.Scope);
      TypeNode txType = null;
      if (this.GetTypeView(qt.Source.Type).IsAssignableTo(SystemTypes.IDbConnection)) {
        txType = SystemTypes.IDbConnection;
      }
      else if (this.GetTypeView(qt.Source.Type).IsAssignableTo(SystemTypes.IDbTransactable)) {
        txType = SystemTypes.IDbTransactable;
      }
      Expression source = this.typeSystem.ExplicitCoercion(qt.Source, txType, this.TypeViewer);
      if (qt.Isolation != null) {
        Method mBegin = this.GetTypeView(txType).GetMethod(idBeginTransaction, SystemTypes.IsolationLevel);
        MethodCall mcBegin = new MethodCall(new MemberBinding(source, mBegin), new ExpressionList(qt.Isolation));
        mcBegin.Type = mBegin.ReturnType;
        mcBegin.NodeType = NodeType.Callvirt;
        block.Statements.Add(new AssignmentStatement(qt.Transaction, mcBegin));
      }
      else {
        Method mBegin = this.GetTypeView(txType).GetMethod(idBeginTransaction);
        MethodCall mcBegin = new MethodCall(new MemberBinding(source, mBegin), null);
        mcBegin.Type = mBegin.ReturnType;
        mcBegin.NodeType = NodeType.Callvirt;
        block.Statements.Add(new AssignmentStatement(qt.Transaction, mcBegin));
      }
      block.Statements.Add(new AssignmentStatement(locCommit, Literal.True));

      // prepare finally block
      Block finBlock = new Block(new StatementList(10));
      Method mRollback = SystemTypes.IDbTransaction.GetMethod(idRollback);
      MethodCall mcRollback = new MethodCall(new MemberBinding(qt.Transaction, mRollback), null);
      mcRollback.Type = mRollback.ReturnType;
      mcRollback.NodeType = NodeType.Callvirt;
      Method mCommit = SystemTypes.IDbTransaction.GetMethod(idCommit);
      MethodCall mcCommit = new MethodCall(new MemberBinding(qt.Transaction, mCommit), null);
      mcCommit.Type = mCommit.ReturnType;
      mcCommit.NodeType = NodeType.Callvirt;
      Method mDispose = SystemTypes.IDisposable.GetMethod(StandardIds.Dispose);
      MethodCall mcDispose = new MethodCall(new MemberBinding(qt.Transaction, mDispose), null);
      mcDispose.Type = mDispose.ReturnType;
      mcDispose.NodeType = NodeType.Callvirt;
      Block bCommitStart = new Block();
      Block bCommitBody = new Block(qt.CommitBody.Statements);
      Block bRollbackBody = new Block(qt.RollbackBody.Statements);
      Block finExit = new Block();
      finBlock.Statements.Add(new Branch(locCommit, bCommitStart));
      finBlock.Statements.Add(new ExpressionStatement(mcRollback));
      finBlock.Statements.Add(bRollbackBody);
      finBlock.Statements.Add(new Branch(null, finExit));
      finBlock.Statements.Add(bCommitStart);
      finBlock.Statements.Add(new ExpressionStatement(mcCommit));
      finBlock.Statements.Add(bCommitBody);
      finBlock.Statements.Add(finExit);
      finBlock.Statements.Add(new ExpressionStatement(mcDispose));
      finBlock.Statements.Add(new AssignmentStatement(qt.Transaction, Literal.Null));

      // prepare catcher
      Local locEx = new Local(SystemTypes.Object);
      Block catchBlock = new Block(new StatementList(2));
      catchBlock.Statements.Add(new AssignmentStatement(locCommit, Literal.False));
      Throw _throw = new Throw(locEx);
      _throw.NodeType = NodeType.Rethrow;
      catchBlock.Statements.Add(_throw);
      CatchList catchers = new CatchList(1);
      catchers.Add(new Catch(catchBlock, locEx, SystemTypes.Object));

      // prepare try block
      Block tryBlock = new Block(new StatementList(4));
      qt.CommitBody.Statements = null;;
      qt.RollbackBody.Statements = null;
      tryBlock.Statements.Add(qt.Body);
      tryBlock.Statements.Add(new Branch(null, qt.CommitBody));
      tryBlock.Statements.Add(qt.RollbackBody);
      tryBlock.Statements.Add(new AssignmentStatement(locCommit, Literal.False));
      tryBlock.Statements.Add(qt.CommitBody);
      this.exceptionBlockFor[qt.CommitBody.UniqueKey] = tryBlock;
      this.exceptionBlockFor[qt.RollbackBody.UniqueKey] = tryBlock;

      // add try-finally to block      
      block.Statements.Add(new Try(tryBlock, catchers, null, null, new Finally(finBlock)));
      this.currentTransaction = qt;
      Node result = this.VisitBlock(block);
      this.currentTransaction = null;
      return result;
    }
    public override Statement VisitRepeat(Repeat repeat){
      if (repeat == null) return null;
      StatementList statements = new StatementList(3);
      Block repeatBlock = new Block(statements);
      repeatBlock.SourceContext = repeat.SourceContext;
      Block endOfLoop = new Block(null);
      this.continueTargets.Add(repeatBlock);
      this.exitTargets.Add(endOfLoop);
      this.VisitBlock(repeat.Body);
      statements.Add(repeat.Body);
      statements.Add(this.VisitAndInvertBranchCondition(repeat.Condition, repeatBlock, repeat.Condition.SourceContext));
      statements.Add(endOfLoop);
      this.continueTargets.Count--;
      this.exitTargets.Count--;
      return repeatBlock;
    }
    // Instead of overriding VisitRequiresPlain or VisitRequiresOtherwise, just process the list.
    // Almost all of the code is the same for both plain and otherwise requires.
    // This could be done by overriding VisitRequires -- but would depend upon introducing
    // a VisitRequires method in StandardVisitor.
    public override RequiresList VisitRequiresList(RequiresList Requires){
      // add a default precondition here and not earlier in the pipeline so it doesn't confuse
      // the contract inheritance checks.
      // REVIEW: probably better to add it earlier and have a HasCompilerGeneratedSignature on it
      // so it can be ignored in the inheritance checks.
      bool addDefaultPrecondition = 
        this.currentMethod != null
        && !this.currentMethod.IsStatic
        && !this.currentMethod.IsAbstract
        && this.currentMethod.Body != null && this.currentMethod.Body.Statements != null
        && !(this.currentMethod.HasCompilerGeneratedSignature && !this.currentMethod.Name.Name.StartsWith("get_") && !this.currentMethod.Name.Name.StartsWith("set_"))
        && this.currentMethod.NodeType != NodeType.InstanceInitializer
        && this.currentMethod.NodeType != NodeType.StaticInitializer
        && (this.currentType != null && this.currentType.Contract != null && this.currentType.Contract.FramePropertyGetter != null)
        && this.currentMethod.GetAttribute(SystemTypes.NoDefaultContractAttribute) == null
        ;
      
      RequiresList newRequires = new RequiresList();

      if (addDefaultPrecondition){
        Method frameGetter = this.currentType.Contract.FramePropertyGetter;
        Method m = null;
        // We used to use "get_CanStartWriting" for non-virtual methods. But Rustan and I decided
        // that it should be fine to use the transitive one for all methods. It is more liberal
        // but still prevents re-entrance into a method in a frame that is exposed.
        m = SystemTypes.Guard.GetMethod(Identifier.For("get_CanStartWritingTransitively"), null);

        // default precondition is (as normalized IR):
        // requires this.get_FrameGuard().get_CanStartWriting(); if it is a non-virtual method
        // requires this.get_FrameGuard().get_CanStartWritingTransitively(); if it is a virtual method
        if (frameGetter != null && m != null){
          SourceContext sc = this.currentMethod.Name.SourceContext;
          MethodCall getFrameGuard = new MethodCall(new MemberBinding(this.currentThisParameter, frameGetter), null, NodeType.MethodCall, SystemTypes.Guard, sc);
          Requires r = new RequiresOtherwise(
            new MethodCall(
            new MemberBinding(getFrameGuard,m),
            null,
            NodeType.MethodCall,
            SystemTypes.Boolean),
            new Construct(new MemberBinding(null, SystemTypes.RequiresException.GetConstructor(SystemTypes.String)), new ExpressionList(new Literal("The target object of this call must be exposable.", SystemTypes.String)), SystemTypes.RequiresException)
            );
          r.SourceContext = sc;
          newRequires.Add(r);
        }
      }

      if ((this.currentMethod.IsPublic || this.currentMethod.IsFamilyOrAssembly) &&
        !(this.currentCompilation != null && this.currentCompilation.CompilerParameters != null &&
        ((CompilerOptions)this.currentCompilation.CompilerParameters).DisableNullParameterValidation)){
        ParameterList parameters = this.currentMethod.Parameters;
        for (int i = 0, n = parameters == null ? 0 : parameters.Count; i < n; i++){
          Parameter parameter = parameters[i];
          if (parameter != null && !parameter.IsOut && this.typeSystem.IsNonNullType(parameter.Type)){
            RequiresOtherwise r;
            Reference rtype = parameter.Type as Reference;
            if (rtype == null) {
              TypeNode parameterType = TypeNode.StripModifier(parameter.Type, SystemTypes.NonNullType);
              Expression e = null;
              if (this.useGenerics && (parameterType is TypeParameter || parameterType is ClassParameter)) {
                e = new BinaryExpression(parameter, new MemberBinding(null, parameterType), NodeType.Box, SystemTypes.Object, parameter.SourceContext);
              } else {
                e = new ParameterBinding(parameter, parameter.SourceContext);
              }
              r =
                new RequiresOtherwise(
                new BinaryExpression(e, new Literal(null, TypeNode.StripModifiers(parameter.Type)), NodeType.Ne, SystemTypes.Boolean, parameter.SourceContext),
                new Construct(new MemberBinding(null, SystemTypes.ArgumentNullException.GetConstructor(SystemTypes.String)), new ExpressionList(new Literal(parameter.Name.Name, SystemTypes.String)), SystemTypes.ArgumentNullException));
            }
            else {
              // have to perform deref
              r =
                new RequiresOtherwise(
                new BinaryExpression(new AddressDereference(new ParameterBinding(parameter, parameter.SourceContext), rtype.ElementType), new Literal(null, TypeNode.StripModifiers(rtype.ElementType)), NodeType.Ne, SystemTypes.Boolean, parameter.SourceContext),
                new Construct(new MemberBinding(null, SystemTypes.ArgumentNullException.GetConstructor(SystemTypes.String)), new ExpressionList(new Literal(parameter.Name.Name, SystemTypes.String)), SystemTypes.ArgumentNullException));
            }
            r.SourceContext = parameter.SourceContext;
            newRequires.Add(r);
          }
        }
      }

      for (int i = 0, n = Requires == null ? 0 : Requires.Count; i < n; i++){
        newRequires.Add(Requires[i]);
      }

      if (newRequires.Count == 0)
          return Requires;

      Block preConditionBlock = new Block(new StatementList());
      preConditionBlock.HasLocals = true;

      for (int i = 0, n = newRequires.Count; i < n; i++)
      {
        Requires r = newRequires[i];
        if (r == null) continue;
        if (r.Condition == null) continue;

        // Code generation for preconditions needs to be such that the
        // data flow analysis will "see" the consequences. If the value
        // of the precondition is assigned to a local, then the information
        // is lost.
        //
        // try {
        //   if re goto pre_i_holds;
        // }
        // catch { throw new ErrorDuringPreConditionEvaluation(...); }
        // throw new PreConditionException(...);
        // pre_i_holds: nop

        bool noAllocationAllowed = this.currentMethod.GetAttribute(SystemTypes.BartokNoHeapAllocationAttribute) != null;
        Local exceptionDuringPreCondition = new Local(Identifier.For("SS$exceptionDuringPreCondition" + i),SystemTypes.Exception);
        Local exceptionDuringPreCondition3 = new Local(Identifier.For("SS$objectExceptionDuringPreCondition" + i),SystemTypes.Object);
        Expression cond = r.Condition;
        string condition = cond != null && cond.SourceContext.SourceText != null && cond.SourceContext.SourceText.Length > 0 ?
          cond.SourceContext.SourceText : "<unknown condition>";
        Expression ec2;
        Expression ec3;
        if (noAllocationAllowed) {
          ec2 = ec3 = new MemberBinding(null, SystemTypes.PreAllocatedExceptions.GetField(Identifier.For("InvalidContract")));
        }
        else {
          MemberBinding excBinding2 = new MemberBinding(null, SystemTypes.InvalidContractException.GetConstructor(SystemTypes.String, SystemTypes.Exception));
          MemberBinding excBinding3 = new MemberBinding(null, SystemTypes.InvalidContractException.GetConstructor(SystemTypes.String));
          string msg2 = "Exception occurred during evaluation of precondition '" + condition + "' in method '" + currentMethod.FullName + "'";
          ec2 = new Construct(excBinding2, new ExpressionList(new Literal(msg2, SystemTypes.String), exceptionDuringPreCondition));
          ec3 = new Construct(excBinding3, new ExpressionList(new Literal(msg2, SystemTypes.String)));
        }

        #region If the precondition fails, throw an exception
        Expression throwExpression = null;
        #region Create the expression to throw. Deal with different subtypes of Requires
        if (noAllocationAllowed) {
          throwExpression = new MemberBinding(null, SystemTypes.PreAllocatedExceptions.GetField(Identifier.For("Requires")));
        }
        else {
          if (r is RequiresPlain) {
            MemberBinding excBinding = new MemberBinding(null, SystemTypes.RequiresException.GetConstructor(SystemTypes.String));
            Construct ec = new Construct(excBinding, new ExpressionList());
            string msg = "Precondition '" + condition + "' violated from method '" + currentMethod.FullName + "'";
            ec.Operands.Add(new Literal(msg, SystemTypes.String));
            throwExpression = ec;
          }
          else if (r is RequiresOtherwise) {
            RequiresOtherwise otherwise = (RequiresOtherwise)r;
            if (otherwise.ThrowException is Literal) {
              // it was "requires P otherwise E" where E is a type name of an exception class
              Literal l = (Literal)otherwise.ThrowException;
              Class exceptionClass = (Class)l.Value;
              MemberBinding excBinding = new MemberBinding(null, this.GetTypeView(exceptionClass).GetConstructor());
              // what to do if there is no nullary constructor? I guess that should have been checked in the context checker
              Construct ec = new Construct(excBinding, new ExpressionList());
              throwExpression = ec;
            }
            else {
              // it was "requires P otherwise new E(...)" (or some other expression whose value is an exception)
              throwExpression = this.VisitExpression(otherwise.ThrowException);
            }
          }
          else {
            Debug.Assert(false, "Expecting only RequiresOtherwise and RequiresPlain as subtypes of Requires");
          }
        }
        #endregion
        Throw t = new Throw(throwExpression,r.SourceContext);
        #endregion

        Block pre_i_holds = new Block();

        //CatchList cl = new CatchList(2);
        //cl.Add(new Catch(new Block(new StatementList(new Throw(ec2,r.Condition.SourceContext))),exceptionDuringPreCondition,SystemTypes.Exception));
        //cl.Add(new Catch(new Block(new StatementList(new Throw(ec3,r.Condition.SourceContext))),exceptionDuringPreCondition3,SystemTypes.Object));

        //Try tryPre = new Try(new Block(new StatementList(new If(r.Condition,new Block(new StatementList(new Branch(null,pre_i_holds))),null))),cl,null,null,null);
        //preConditionBlock.Statements.Add(tryPre);
        preConditionBlock.Statements.Add(new If(r.Condition,new Block(new StatementList(new Branch(null,pre_i_holds))),null));
        preConditionBlock.Statements.Add(t);
        preConditionBlock.Statements.Add(pre_i_holds);

      }

      preConditionBlock = this.VisitBlock(preConditionBlock);
      this.currentContractPrelude.Statements.Add(preConditionBlock);
      return Requires;
    }
    public override Statement VisitReturn(Return Return){
      if (Return == null) return null;
      Branch br = new Branch(null, this.currentReturnLabel);
      if (this.currentExceptionBlock != null) br.LeavesExceptionBlock = true;
      if (this.currentContractExceptionalTerminationChecks != null && this.currentContractExceptionalTerminationChecks.Count > 0 )
        br.LeavesExceptionBlock = true;
      Expression result = this.VisitExpression(Return.Expression);
      if (this.currentReturnLocal == null){
        br.SourceContext = Return.SourceContext;
        return br;
      }
      AssignmentStatement astat = new AssignmentStatement(this.currentReturnLocal, result);
      astat.SourceContext = Return.SourceContext;
      StatementList statements = new StatementList(2);
      statements.Add(astat);
      statements.Add(br);
      return new Block(statements);
    }
    public override Expression VisitConstruct(Construct cons){
      Expression result = null;
      if (cons != null){
        TypeNode consTypeprime = TypeNode.StripModifiers(cons.Type);
        if (consTypeprime == SystemTypes.ThreadStart && cons.Operands.Count == 2){
          Expression target = cons.Operands[0];
          UnaryExpression ldftn = cons.Operands[1] as UnaryExpression;
          if (ldftn != null && ldftn.NodeType == NodeType.Ldftn){
            MemberBinding mb = ldftn.Operand as MemberBinding;
            if (mb != null){
              Method method = mb.BoundMember as Method;
              if (method != null && !method.IsStatic){
                TypeNode type = method.DeclaringType;
                if (type != null){
                  TypeNode tprime = TypeNode.StripModifiers(type);
                  TypeContract contract = tprime.Contract;
                  if (contract != null){
                    Method frameGetter = contract.FramePropertyGetter;
                    if (frameGetter != null){
                      bool requiresImmutable = method.GetAttribute(SystemTypes.RequiresImmutableAttribute) != null;
                      bool requiresLockProtected = method.GetAttribute(SystemTypes.RequiresLockProtectedAttribute) != null;
                      bool requiresCanWrite = method.GetAttribute(SystemTypes.RequiresCanWriteAttribute) != null;
                      string createThreadStartMethodName =
                        requiresImmutable ? "CreateThreadStartForImmutable" : (requiresLockProtected ? "CreateThreadStartForLockProtected" : "CreateThreadStartForOwn");
                      Local targetLocal = new Local(type);
                      Expression call = new MethodCall(
                        new MemberBinding(
                        new MethodCall(new MemberBinding(targetLocal, frameGetter), null, NodeType.Call, SystemTypes.Guard),
                        SystemTypes.Guard.GetMethod(Identifier.For(createThreadStartMethodName), SystemTypes.GuardThreadStart)),
                        new ExpressionList(new Construct(new MemberBinding(null, SystemTypes.GuardThreadStart.GetConstructor(SystemTypes.Object, SystemTypes.IntPtr)),
                        new ExpressionList(targetLocal, ldftn), SystemTypes.GuardThreadStart)),
                        NodeType.Call, SystemTypes.ThreadStart);
                      result = new BlockExpression(new Block(new StatementList(
                        new AssignmentStatement(targetLocal, target, target.SourceContext),
                        new ExpressionStatement(call, cons.SourceContext))), cons.Type, cons.SourceContext);
                    }
                  }
                }
              }
            }
          }
        }
      }
      if (cons.Owner != null) {
        return CreateOwnerIsMethodCall(cons);
      }
      if (result == null)
        return base.VisitConstruct(cons);
      else
        return VisitExpression(result);
    }

    private Expression CreateOwnerIsMethodCall(Construct cons) {
      if (cons == null || cons.Owner == null) return cons;
      Method OwnerIsMethod = this.GetTypeView(SystemTypes.AssertHelpers).GetMethod(Identifier.For("OwnerIs"), SystemTypes.Object, SystemTypes.Object);
      Expression visitedCons = base.VisitConstruct(cons);
      MethodCall mc = new MethodCall(new MemberBinding(null, OwnerIsMethod), new ExpressionList(cons.Owner, visitedCons), NodeType.Call, OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Object), cons.SourceContext);
      cons.Owner = null; // Anything downstream shouldn't even see the owner: it is only for the static analysis
      return this.typeSystem.ExplicitCoercion(mc, cons.Type, this.TypeViewer);
    }
    private Expression CreateOwnerIsMethodCall(ConstructArray consArr) {
      if (consArr == null || consArr.Owner == null) return consArr;
      Method OwnerIsMethod = this.GetTypeView(SystemTypes.AssertHelpers).GetMethod(Identifier.For("OwnerIs"), SystemTypes.Object, SystemTypes.Object);
      Expression visitedConsArr = base.VisitConstructArray(consArr);
      MethodCall mc = new MethodCall(new MemberBinding(null, OwnerIsMethod), new ExpressionList(consArr.Owner, visitedConsArr), NodeType.Call, OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Object), consArr.SourceContext);
      consArr.Owner = null; // Anything downstream shouldn't even see the owner: it is only for the static analysis
      return this.typeSystem.ExplicitCoercion(mc, consArr.Type, this.TypeViewer);
    }
    public override Statement VisitAcquire(Acquire acquire) {
      if (@acquire == null)
        return null;

      Expression target;
      TypeNode targetType;
      Expression initialValue;

      ExpressionStatement expr = @acquire.Target as ExpressionStatement;
      if (expr != null){
        if (expr.Expression == null)
          return null;
        targetType = expr.Expression.Type;
        target = new Local(targetType);
        initialValue = expr.Expression;
      } else {
        LocalDeclarationsStatement declsStmt = (LocalDeclarationsStatement) @acquire.Target;
        LocalDeclaration decl = declsStmt.Declarations[0];
        targetType = decl.Field.Type;
        target = new MemberBinding(new ImplicitThis(), decl.Field);
        initialValue = decl.InitialValue;
        decl.Field.Flags &= ~FieldFlags.InitOnly;
      }

      targetType = this.typeSystem.Unwrap(targetType);
      if (targetType == null || targetType.Contract == null || targetType.Contract.FramePropertyGetter == null)
        return null;

      StatementList statements = new StatementList();
      Block block = new Block(statements);

      statements.Add(new AssignmentStatement(target, initialValue, initialValue.SourceContext));

      SourceContext acquireKeyword = acquire.SourceContext;
      acquireKeyword.EndPos = acquireKeyword.StartPos + "acquire".Length;

      String acquireMethodName;
      String releaseMethodName;
      if (@acquire.ReadOnly){
        acquireMethodName = "AcquireForReading";
        releaseMethodName = "ReleaseForReading";
      } else {
        acquireMethodName = "AcquireForWriting";
        releaseMethodName = "ReleaseForWriting";
      }
      Method acquireMethod = SystemTypes.Guard.GetMethod(Identifier.For(acquireMethodName), SystemTypes.ThreadConditionDelegate);
      Method releaseMethod = SystemTypes.Guard.GetMethod(Identifier.For(releaseMethodName));
      Expression frame = new MethodCall(new MemberBinding(target, targetType.Contract.FramePropertyGetter), null, NodeType.Call, SystemTypes.Guard);
      Expression cond = @acquire.Condition == null ? (Expression) new Literal(null, SystemTypes.ThreadConditionDelegate) : @acquire.ConditionFunction;

      statements.Add(new ExpressionStatement(new MethodCall(new MemberBinding(frame, acquireMethod), new ExpressionList(cond), NodeType.Call, SystemTypes.Void), acquireKeyword));

      statements.Add(new Try(@acquire.Body, null, null, null, new Finally(new Block(new StatementList(
        new ExpressionStatement(new MethodCall(new MemberBinding(frame, releaseMethod), null, NodeType.Call, SystemTypes.Void), acquireKeyword))))));

      return this.VisitBlock(block);
    }
    public override Statement VisitResourceUse(ResourceUse resourceUse){
      if (resourceUse == null) return null;
      StatementList resourceReleasers = new StatementList();
      ExpressionStatement eStat = resourceUse.ResourceAcquisition as ExpressionStatement;
      if (eStat != null){
        if (eStat.Expression == null) return null;
        TypeNode t = eStat.Expression.Type;
        if (t == null) return null;
        Expression temp = new Local(t);
        BlockScope tempScope = resourceUse.ScopeForTemporaryVariable;
        if (tempScope.CapturedForClosure) {
          Identifier id = Identifier.For("usingTemp:"+resourceUse.GetHashCode());
          Field f = new Field(tempScope, null, FieldFlags.CompilerControlled, id, t, null);
          temp = new MemberBinding(new ImplicitThis(), f);
        }
        MethodCall releaseCall = null;
        if (t.IsValueType && !t.ImplementsExplicitly(Runtime.IDisposableDispose)) {
          Method dispose = t.GetImplementingMethod(Runtime.IDisposableDispose, true);
          if (dispose != null)
            releaseCall = new MethodCall(new MemberBinding(new UnaryExpression(temp, NodeType.AddressOf), dispose), null, NodeType.Call);
        }
        AssignmentStatement aStat = new AssignmentStatement(temp, eStat.Expression);
        aStat.SourceContext = eStat.SourceContext;
        resourceUse.ResourceAcquisition = aStat;
        Block skipRelease = null;
        if (!t.IsValueType) {
          skipRelease = new Block();
          resourceReleasers.Add(new Branch(new BinaryExpression(temp, Literal.Null, NodeType.Eq), skipRelease));
        }
        if (releaseCall == null) {
          Expression ob = temp;
          if (t.IsValueType)
            ob = new BinaryExpression(temp, new MemberBinding(null, t), NodeType.Box);
          releaseCall = new MethodCall(new MemberBinding(ob, Runtime.IDisposableDispose), null, NodeType.Callvirt);
        }
        releaseCall.Type = SystemTypes.Void;
        resourceReleasers.Add(new ExpressionStatement(releaseCall));
        if (skipRelease != null) resourceReleasers.Add(skipRelease);
      }else{
        StatementList resourceAcquirers = new StatementList();
        LocalDeclarationsStatement locDecs = (LocalDeclarationsStatement)resourceUse.ResourceAcquisition;
        LocalDeclarationList locDecList = locDecs.Declarations;
        for (int i = 0, n = locDecList == null ? 0 : locDecList.Count; i < n; i++){
          LocalDeclaration locDec = locDecList[i];
          if (locDec == null) continue;
          Field f = locDec.Field;
          if (f == null || f.Initializer == null) continue;
          TypeNode t = f.Type;
          if (t == null || !this.GetTypeView(TypeNode.StripModifiers(t)).IsAssignableTo(SystemTypes.IDisposable)) continue;
          f.Flags &= ~FieldFlags.InitOnly;
          AssignmentStatement aStat = new AssignmentStatement(new MemberBinding(new ImplicitThis(), f), locDec.InitialValue);
          aStat.SourceContext = locDec.InitialValue.SourceContext;
          resourceAcquirers.Add(aStat);
          Local temp = new Local(t);
          resourceReleasers.Add(new AssignmentStatement(temp, new MemberBinding(new ImplicitThis(), f)));
          StatementList stats = new StatementList(1);
          MethodCall releaseCall = new MethodCall(new MemberBinding(temp, Runtime.IDisposableDispose), null, NodeType.Callvirt);
          releaseCall.Type = SystemTypes.Void;
          stats.Add(new ExpressionStatement(releaseCall));
          if (t.IsValueType){
            Method dispose = t.GetImplementingMethod(Runtime.IDisposableDispose, true);
            if (!t.ImplementsExplicitly(Runtime.IDisposableDispose) && dispose != null) {
              ((MemberBinding)releaseCall.Callee).TargetObject = new UnaryExpression(temp, NodeType.AddressOf);
              ((MemberBinding)releaseCall.Callee).BoundMember = dispose;
              releaseCall.NodeType = NodeType.Call;
            } else
              ((MemberBinding)releaseCall.Callee).TargetObject = this.typeSystem.ExplicitCoercion(temp, SystemTypes.IDisposable, this.TypeViewer);
            resourceReleasers.Add(new Block(stats));
          }else
            resourceReleasers.Add(new If(new BinaryExpression(temp, Literal.Null, NodeType.Ne), new Block(stats), null));
        }
        resourceUse.ResourceAcquisition = new Block(resourceAcquirers);
      }
      StatementList statements = new StatementList(2);
      statements.Add(resourceUse.ResourceAcquisition);
      statements.Add(new Try(resourceUse.Body, null, null, null, new Finally(new Block(resourceReleasers))));
      return this.VisitBlock(new Block(statements));
    }
    public override Expression VisitSetterValue(SetterValue value){
      if (value == null) return null;
      if (this.currentMethod != null && this.currentMethod.Parameters != null){
        int n = this.currentMethod.Parameters.Count;
        if (n > 0) return this.currentMethod.Parameters[n-1];
      }
      return null;
    }
    public override Expression VisitStackAlloc(StackAlloc alloc){
      if (alloc == null) return null;
      return new UnaryExpression(this.VisitExpression(alloc.NumberOfElements), NodeType.Localloc, alloc.Type, alloc.SourceContext);
    }
    public override StaticInitializer VisitStaticInitializer(StaticInitializer cons){
      if (cons == null) return null;
      if (!cons.HasCompilerGeneratedSignature && cons.DeclaringType != null)
        cons.DeclaringType.Flags &= ~TypeFlags.BeforeFieldInit;
      return base.VisitStaticInitializer(cons);
    }
    public virtual Statement VisitStringSwitch(Switch Switch){
      Debug.Assert(Switch != null && Switch.Expression != null && Switch.Expression.Type == SystemTypes.String);
      Debug.Assert(this.currentType != null);
      //Check if hashtable is already initialized
      Identifier id = Identifier.For("Switch string table: "+Switch.UniqueKey);
      Field hashtableField = new Field(this.currentType, null, FieldFlags.SpecialName|FieldFlags.CompilerControlled|FieldFlags.Static, id, SystemTypes.Hashtable, null);
      this.currentType.Members.Add(hashtableField);
      StatementList statements = new StatementList();
      Block lookupStringAndSwitch = new Block();
      statements.Add(new Branch(new MemberBinding(null, hashtableField), lookupStringAndSwitch));
      //Initialize hashtable
      statements.Add(lookupStringAndSwitch);
      return new Block(statements);
    }
    public override Statement VisitSwitch(Switch Switch){
      if (Switch == null) return null;
      Expression swexpr = Switch.Expression = this.VisitExpression(Switch.Expression);
      SwitchCaseList cases = Switch.Cases;
      if (swexpr == null || cases == null) return null;
      //if (swexpr.Type == SystemTypes.String) return this.VisitStringSwitch(Switch);
      Local swexLoc = new Local(Identifier.Empty, swexpr.Type);
      AssignmentStatement evalExpr = new AssignmentStatement(swexLoc, swexpr);
      evalExpr.SourceContext = swexpr.SourceContext;
      int n = cases.Count;
      if (n == 0) return this.VisitAssignmentStatement(evalExpr);
      Block endOfSwitch = new Block(null);
      this.exitTargets.Add(endOfSwitch);
      StatementList statements = new StatementList(n*2+1);
      Branch branchToNullcase = null;
      if (Switch.Nullable != null && Switch.Nullable.Type != null && Switch.NullableExpression != null){
        AssignmentStatement evalNullableExpr = new AssignmentStatement(Switch.Nullable, this.VisitExpression(Switch.NullableExpression));
        evalNullableExpr.SourceContext = Switch.NullableExpression.SourceContext;
        statements.Add(evalNullableExpr);
        Expression addrOfNullable = new UnaryExpression(Switch.Nullable, NodeType.AddressOf, Switch.Nullable.Type.GetReferenceType());
        Method hasValue = this.GetTypeView(Switch.Nullable.Type).GetMethod(StandardIds.getHasValue);
        Expression isNull = new MethodCall(new MemberBinding(addrOfNullable, hasValue), null, NodeType.Call, SystemTypes.Boolean);
        branchToNullcase = new Branch(new UnaryExpression(isNull, NodeType.LogicalNot), null);
        statements.Add(branchToNullcase);
      }
      statements.Add(evalExpr);
      Branch branchToDefault = null;
      if (TypeNode.StripModifiers(swexpr.Type) == SystemTypes.String){
        Literal nullLit = new Literal(null, SystemTypes.Object);
        statements.Add(branchToNullcase = new Branch(new BinaryExpression(swexLoc, nullLit, NodeType.Eq), null));
        ExpressionList args = new ExpressionList(1);
        args.Add(swexLoc);
        Local swexLoc2 = new Local(Identifier.Empty, SystemTypes.String);
        statements.Add(new AssignmentStatement(swexLoc2, new MethodCall(new MemberBinding(null, Runtime.IsInterned), args)));
        statements.Add(branchToDefault = new Branch(new BinaryExpression(swexLoc2, nullLit, NodeType.Eq), null));
        statements.Add(new AssignmentStatement(swexLoc, swexLoc2));
      }
      //TODO: analyze the cases and use the Switch instruction to handle dense sequences of constants
      Block defaultBlock = null;
      Block nullBlock = null;
      for (int i = 0; i < n; i++){
        SwitchCase scase = cases[i];
        if (scase == null) continue;
        if (scase.Label == null){
          defaultBlock = scase.Body; continue;
        }else if (Literal.IsNullLiteral(scase.Label)){
          nullBlock = scase.Body; continue;
        }
        Expression condition = new BinaryExpression(swexLoc, this.VisitExpression(scase.Label), NodeType.Eq);
        statements.Add(new Branch(condition, scase.Body)); 
      }
      if (defaultBlock != null)
        statements.Add(new Branch(null, defaultBlock));
      else
        statements.Add(new Branch(null, endOfSwitch));
      for (int i = 0; i < n; i++){
        SwitchCase scase = cases[i];
        if (scase == null) continue;
        statements.Add(this.VisitBlock(scase.Body));
      }
      statements.Add(endOfSwitch);
      if (branchToDefault != null){
        if (defaultBlock != null)
          branchToDefault.Target = defaultBlock;
        else
          branchToDefault.Target = endOfSwitch;
      }
      if (branchToNullcase != null){
        if (nullBlock != null)
          branchToNullcase.Target = nullBlock;
        else if (defaultBlock != null)
          branchToNullcase.Target = defaultBlock;
        else
          branchToNullcase.Target = endOfSwitch;
      }
      this.exitTargets.Count--;
      return new Block(statements);    
    }
    public virtual Expression VisitLiftedUnaryExpression(UnaryExpression unaryExpression) {
      if (unaryExpression == null) return null;
      if (!this.typeSystem.IsNullableType(unaryExpression.Type)) { Debug.Assert(false); return null; }
      TypeNode type = unaryExpression.Type;
      TypeNode urType = this.typeSystem.RemoveNullableWrapper(type);

      TypeNode operType = unaryExpression.Operand.Type;

      Local temp = new Local(operType);
      Expression operand = this.VisitExpression(unaryExpression.Operand);

      StatementList statements = new StatementList();
      BlockExpression result = new BlockExpression(new Block(statements));

      statements.Add(new AssignmentStatement(temp, operand));

      Method hasValue = this.GetTypeView(operType).GetMethod(StandardIds.getHasValue);
      Method getValueOrDefault = this.GetTypeView(operType).GetMethod(StandardIds.GetValueOrDefault);
      Method ctor = this.GetTypeView(type).GetMethod(StandardIds.Ctor, urType);
      Block pushValue = new Block();
      Block done = new Block();

      Expression tempHasValue = new MethodCall(new MemberBinding(new UnaryExpression(temp, NodeType.AddressOf), hasValue), null);
      statements.Add(new Branch(tempHasValue, pushValue));
      statements.Add(new AssignmentStatement(new AddressDereference(new UnaryExpression(temp, NodeType.AddressOf), operType), new Literal(null, CoreSystemTypes.Object)));
      statements.Add(new Branch(null, done));
      statements.Add(pushValue);

      Expression value = new MethodCall(new MemberBinding(new UnaryExpression(temp, NodeType.AddressOf), getValueOrDefault), null);
      value.Type = this.typeSystem.RemoveNullableWrapper(operType);

      Expression newUVal = value;
      switch(unaryExpression.NodeType){
        case NodeType.LogicalNot:
          newUVal = new BinaryExpression(value, Literal.False, NodeType.Ceq, urType);
          break;
        case NodeType.Not:
          newUVal = new UnaryExpression(value, NodeType.Not, urType);
          break;
        case NodeType.Neg:
          newUVal = new UnaryExpression(value, NodeType.Neg, urType);
          break;
      }

      Construct cons = new Construct(new MemberBinding(null, ctor), new ExpressionList(newUVal));
      result.Type = ctor.DeclaringType;
      Local resLoc = new Local(type);
      statements.Add(new AssignmentStatement(resLoc, cons));
      statements.Add(done);
      statements.Add(new ExpressionStatement(resLoc));

      return result;
    }
    public override Expression VisitUnaryExpression(UnaryExpression unaryExpression){
      if (unaryExpression == null) return null;
      switch(unaryExpression.NodeType){
        case NodeType.AddressOf:
        case NodeType.OutAddress:
        case NodeType.RefAddress:
          unaryExpression.Operand = this.VisitTargetExpression(unaryExpression.Operand);
          return this.VisitAddressOfExpression(unaryExpression);
        case NodeType.LogicalNot:
          if (this.typeSystem.IsNullableType(unaryExpression.Type))
            return VisitLiftedUnaryExpression(unaryExpression);
          unaryExpression.Operand = this.VisitExpression(unaryExpression.Operand);
          if (unaryExpression.Operand != null && unaryExpression.Operand.Type != null && unaryExpression.Operand.Type.IsValueType){
            StatementList statements = new StatementList(6);
            Block returnFalse = new Block();
            Block done = new Block();
            statements.Add(new Branch(unaryExpression.Operand, returnFalse, true, false, false));
            statements.Add(new ExpressionStatement(Literal.Int32One));
            statements.Add(new Branch(null, done, true, false, false));
            statements.Add(returnFalse);
            statements.Add(new ExpressionStatement(Literal.Int32Zero));
            statements.Add(done);
            return new BlockExpression(new Block(statements), SystemTypes.Boolean);
          }
          //Debug.Assert(false);
          break;
        case NodeType.DefaultValue: {
          Literal lit = unaryExpression.Operand as Literal;
          if (lit == null) return null;
          TypeNode ty = lit.Value as TypeNode;
          if (ty == null) return null;
          Local loc = new Local(ty);
          if (!ty.IsValueType && !(ty is TypeParameter || ty is ClassParameter)) {
            StatementList statements = new StatementList(2);
            statements.Add(new AssignmentStatement(loc, new Literal(null, SystemTypes.Object)));
            statements.Add(new ExpressionStatement(loc));
            return new BlockExpression(new Block(statements), ty);
          } else {
            //if (!((this.currentMethod != null && this.currentMethod.IsGeneric) || (this.currentType != null && this.currentType.IsGeneric))) return loc;
            UnaryExpression loca = new UnaryExpression(loc, NodeType.AddressOf, loc.Type.GetReferenceType());
            StatementList statements = new StatementList(2);
            statements.Add(new AssignmentStatement(new AddressDereference(loca, ty, false, 0), new Literal(null, SystemTypes.Object)));
            statements.Add(new ExpressionStatement(loc));
            return new BlockExpression(new Block(statements), ty);
          }
        }
        case NodeType.Typeof:
          unaryExpression.Operand = this.VisitExpression(unaryExpression.Operand);
          unaryExpression.NodeType = NodeType.Ldtoken;
          ExpressionList arguments = new ExpressionList(1);
          arguments.Add(unaryExpression);
          MemberBinding mb = new MemberBinding(null, Runtime.GetTypeFromHandle);
          return new MethodCall(mb, arguments);
        case NodeType.UnaryPlus:
        case NodeType.Parentheses:
          return this.VisitExpression(unaryExpression.Operand);
        case NodeType.Neg:
        case NodeType.Not:
          if (this.typeSystem.IsNullableType(unaryExpression.Type))
            return VisitLiftedUnaryExpression(unaryExpression);
          goto default;
        default:
          unaryExpression.Operand = this.VisitExpression(unaryExpression.Operand);
          break;
      }
      return unaryExpression;
    }
    public virtual Expression VisitAddressOfExpression(UnaryExpression unaryExpression){
      if (unaryExpression == null) return null;
      Expression opnd = unaryExpression.Operand;
      if (opnd == null) return null;
      LRExpression lrExpr = opnd as LRExpression;
      if (lrExpr != null){
        LocalList locals = lrExpr.Temporaries;
        int n = locals == null ? 0 : locals.Count;
        ExpressionList subs = lrExpr.SubexpressionsToEvaluateOnce;
        StatementList stats = new StatementList(n);
        for (int i = 0; i < n; i++)
          stats.Add(new AssignmentStatement(locals[i], subs[i]));
        Local temp = new Local(Identifier.Empty, opnd.Type);
        locals.Add(temp);
        if (unaryExpression.NodeType != NodeType.OutAddress)
          stats.Add(new AssignmentStatement(temp, this.VisitExpression(lrExpr.Expression)));
        stats.Add(new ExpressionStatement(new UnaryExpression(temp, NodeType.AddressOf, temp.Type.GetReferenceType())));
        return new BlockExpression(new Block(stats), temp.Type.GetReferenceType());
      }else if (opnd is MethodCall || opnd is Literal || opnd is Construct || opnd is BlockExpression || 
        opnd is UnaryExpression || opnd is BinaryExpression || opnd is TernaryExpression || opnd is NaryExpression ){
        if (opnd is Indexer) return unaryExpression;
        MethodCall call = opnd as MethodCall;
        if (call != null && call.Type == SystemTypes.Void){
          MemberBinding memb = call.Callee as MemberBinding;
          if (memb == null){Debug.Assert(false); return null;}
          Debug.Assert(memb.BoundMember.Name == StandardIds.Set);
          if (memb != null && memb.BoundMember != null && memb.BoundMember.DeclaringType != null)
            memb.BoundMember.DeclaringType = TypeNode.StripModifiers(memb.BoundMember.DeclaringType);
          ArrayType arrT = memb.BoundMember.DeclaringType as ArrayType;
          if (arrT == null){Debug.Assert(false); return null;}
          memb.BoundMember = arrT.Address;
          call.Type = arrT.Address.ReturnType;
          return call;
        }
        StatementList stats = null;
        // optimization: don't create extra local if block expression yields a local
        BlockExpression be = opnd as BlockExpression;
        if (be != null && be.Block != null && be.Block.Statements != null) {
          stats = be.Block.Statements;
          int n = stats.Count;
          if (n > 0) {
            ExpressionStatement s = stats[n - 1] as ExpressionStatement;
            if (s != null && s.Expression is Local) {
              TypeNode refType = s.Expression.Type.GetReferenceType();
              s.Expression = new UnaryExpression(s.Expression, NodeType.AddressOf, refType);
              be.Type = refType; // mark the block expression as returning an address
              return be;
            }
          }
        } 
        // handle ordinary case
        stats = new StatementList(2);
        Local temp = new Local(Identifier.Empty, opnd.Type);
        stats.Add(new AssignmentStatement(temp, opnd));
        stats.Add(new ExpressionStatement(new UnaryExpression(temp, NodeType.AddressOf, temp.Type.GetReferenceType())));
        return new BlockExpression(new Block(stats), temp.Type.GetReferenceType());
      }
      if (opnd != null && opnd.Type is Reference) return opnd;
      if (opnd != null && opnd.NodeType == NodeType.AddressDereference) return ((AddressDereference)opnd).Address;
      MemberBinding mb = opnd as MemberBinding;
      if (mb != null && mb.BoundMember is Field && mb.TargetObject != null && mb.TargetObject.Type != null && mb.TargetObject.Type.IsValueType)
        mb.TargetObject = new UnaryExpression(mb.TargetObject, NodeType.AddressOf, mb.TargetObject.Type.GetReferenceType());
      return unaryExpression;
    }
    public override Statement VisitVariableDeclaration(VariableDeclaration variableDeclaration){
      //TODO: need to make sure locals that are declared only are referenced before any nested closures can reference them
      return null;
    }
    public override Statement VisitWhile(While While){
      if (While == null) return null;
      StatementList statements = new StatementList(5);
      ExpressionList invariants = While.Invariants;
      if (invariants != null && invariants.Count > 0)
        statements.Add(VisitLoopInvariants(invariants));
      Block whileBlock = new Block(statements);
      whileBlock.SourceContext = While.SourceContext;
      Block endOfLoop = new Block(null);
      this.continueTargets.Add(whileBlock);
      this.exitTargets.Add(endOfLoop);
      if (While.Condition == null) return null;
      SourceContext ctx = While.SourceContext; ctx.EndPos = While.Condition.SourceContext.EndPos;
      Statement whileCondition = this.VisitAndInvertBranchCondition(While.Condition, endOfLoop, ctx);
      this.VisitBlock(While.Body);
      statements.Add(whileCondition);
      statements.Add(While.Body);
      ctx = While.SourceContext;
      if (While.Body != null)
        ctx.StartPos = While.Body.SourceContext.EndPos;
      statements.Add(new Branch(null, whileBlock, ctx));
      statements.Add(endOfLoop);
      this.continueTargets.Count--;
      this.exitTargets.Count--;
      return whileBlock;
    }
    public override Expression VisitTargetExpression(Expression target){
      if (target == null) return null;
      switch(target.NodeType){
        case NodeType.AddressDereference:{
          AddressDereference adr = (AddressDereference)target;
          if (adr.Address != null && adr.Address.Type != null && adr.Address.Type.Template == SystemTypes.GenericBoxed){
            Method setValue = this.GetTypeView(adr.Address.Type).GetMethod(StandardIds.SetValue, target.Type);
            return new MethodCall(new MemberBinding(new UnaryExpression(this.VisitExpression(adr.Address), NodeType.AddressOf), setValue), new ExpressionList(1));
          }else
            return this.VisitAddressDereference(adr);}
        case NodeType.LRExpression:
          LRExpression lrexpr = (LRExpression)target;
          LocalList temps = lrexpr.Temporaries = new LocalList();
          ExpressionList exprs = lrexpr.SubexpressionsToEvaluateOnce = new ExpressionList();
          Expression texpr = lrexpr.Expression;
          switch(texpr.NodeType){
            case NodeType.Indexer: 
              Indexer indexer = (Indexer)texpr;
              indexer.ElementType = this.typeSystem.GetUnderlyingType(indexer.ElementType);
              Expression expr = this.VisitExpression(indexer.Object);
              exprs.Add(expr);
              Local loc = new Local(); loc.Type = expr.Type;
              indexer.Object = loc; temps.Add(loc);
              ExpressionList arguments = indexer.Operands;
              if (arguments != null){
                for (int i = 0, n = arguments.Count; i < n; i++){
                  expr = this.VisitExpression(arguments[i]);
                  if (expr == null) continue;
                  exprs.Add(expr);
                  loc = new Local(); loc.Type = expr.Type;
                  arguments[i] =loc; temps.Add(loc);
                }
              }
              break;
            case NodeType.MemberBinding:
              MemberBinding mb = (MemberBinding)texpr;
              bool fGetResult = false;
              // fixup target type
              if (mb.TargetObject != null) {
                switch (mb.TargetObject.NodeType) {
                  case NodeType.ImplicitThis:
                  case NodeType.This:
                  case NodeType.Base:
                    if (mb.BoundMember != null && 
                      (mb.BoundMember.DeclaringType is ClosureClass) &&
                      !(mb.BoundMember is ParameterField)) {
                      fGetResult = true;
                      mb.TargetObject.Type = mb.BoundMember.DeclaringType;
                    }
                    break;
                }
              }
              Expression targetOb = this.VisitExpression(mb.TargetObject);
              if (targetOb != null){
                switch (targetOb.NodeType){
                  case NodeType.Local:
                  case NodeType.Parameter:
                    break;
                  case NodeType.This:
                  case NodeType.Base:
                  case NodeType.ImplicitThis:
                    if (fGetResult) 
                      mb.TargetObject = targetOb;
                    break;
                  case NodeType.AddressDereference:
                    AddressDereference adr = (AddressDereference)targetOb;
                    exprs.Add(adr.Address);
                    loc = new Local(); loc.Type = adr.Address.Type;
                    adr.Address = loc; temps.Add(loc);
                    break;
                  default:
                    if (targetOb.Type.IsValueType && !targetOb.Type.IsPrimitive) {
                      // use address, not value itself
                      targetOb = new UnaryExpression(targetOb, NodeType.AddressOf, targetOb.Type.GetReferenceType(), targetOb.SourceContext);
                    }
                    exprs.Add(targetOb);
                    loc = new Local(); loc.Type = targetOb.Type;
                    mb.TargetObject = loc; temps.Add(loc);
                    break;
                }
              }
              break;
            case NodeType.Local: 
            case NodeType.Parameter: 
              break;
            case NodeType.AddressDereference: {
              AddressDereference adr = (AddressDereference)texpr;
              Expression address = this.VisitExpression(adr.Address);
              exprs.Add(address);
              loc = new Local(); loc.Type = adr.Address.Type;
              adr.Address = loc; temps.Add(loc);
              break;
            }
            default:
              break;
          }
          return target;
        case NodeType.Indexer: { 
          Indexer indexer = (Indexer)target;
          bool baseCall = indexer.Object is Base;
          indexer.Object = this.VisitExpression(indexer.Object);
          indexer.Operands = this.VisitExpressionList(indexer.Operands);
          Property property = indexer.CorrespondingDefaultIndexedProperty;
          Method setter = null;
          if (property == null){
            TypeNode obType = indexer.Object.Type;
            TupleType tupT = obType as TupleType;
            if (tupT != null){
              if (indexer.Operands == null || indexer.Operands.Count != 1) return null;
              Literal lit = indexer.Operands[0] as Literal;
              if (lit == null || lit.Type != SystemTypes.Int32) return null;
              int i = (int) lit.Value;
              if (i < 0 || i >= this.GetTypeView(tupT).Members.Count) return null;
              Field f = this.GetTypeView(tupT).Members[i] as Field;
              if (f == null) return null;
              MemberBinding mb = new MemberBinding(indexer.Object, f);
              mb.SourceContext = indexer.SourceContext;
              mb.Type = f.Type;
              return mb;
            }
            if (obType != null)
              obType = TypeNode.StripModifiers(obType);
            if (obType is Pointer) return this.VisitExpression(indexer);
            ArrayType arrT = obType as ArrayType;
            if (arrT == null) return null;
            if (arrT.IsSzArray()){
              TypeNode et = indexer.ElementType = this.typeSystem.GetUnderlyingType(arrT.ElementType);
              if (et.IsValueType && !et.IsPrimitive)
                return new AddressDereference(new UnaryExpression(indexer, NodeType.AddressOf, indexer.Type.GetReferenceType()), et);
              return indexer;
            }
            setter = arrT.Setter;
          }else
            setter = property.Setter;
          if (setter == null) {
            if (property.Type is Reference) {
              // call the getter instead
              return this.VisitExpression(target);
            }
            return null;
          }
          ExpressionList indexerOperands = indexer.Operands;
          ExpressionList setterArguments = new ExpressionList();
          if (indexerOperands != null)
            for (int i = 0, n = indexerOperands.Count; i < n; i++) setterArguments.Add(indexerOperands[i]);
          Expression targetObject = indexer.Object;
          if (targetObject.Type != null && targetObject.Type.IsValueType){
            AddressDereference ad = targetObject as AddressDereference;
            if (ad != null)
              targetObject = ad.Address;
            else
              targetObject = new UnaryExpression(targetObject, NodeType.AddressOf, targetObject.Type.GetReferenceType());
          }
          MethodCall call = new MethodCall(new MemberBinding(targetObject, setter), setterArguments);
          if (!baseCall && setter.IsVirtualAndNotDeclaredInStruct){
            call.NodeType = NodeType.Callvirt;
            if (this.useGenerics && targetObject != null && targetObject.Type is ITypeParameter){
              ((MemberBinding)call.Callee).TargetObject = new UnaryExpression(targetObject, NodeType.AddressOf, targetObject.Type.GetReferenceType());
              call.Constraint = targetObject.Type;
            }
          }
          call.Type = SystemTypes.Void;
          return call;
        }
        case NodeType.MemberBinding: { 
          MemberBinding mb = (MemberBinding)target;
          Property prop = mb.BoundMember as Property;
          if (prop != null){
            Method setter = prop.Setter;
            if (setter == null) setter = prop.GetBaseSetter();
            if (setter == null) {
              if (prop.Type is Reference) {
                // call the getter instead
                return this.VisitExpression(target);
              }
              return null;
            }
            bool baseCall = mb.TargetObject is Base;
            mb = new MemberBinding(mb.TargetObject, setter);
            mb = (MemberBinding)this.VisitMemberBinding(mb); // force a visit so that singsharp.visitmemberbinding has a chance to run
            ExpressionList setterArguments = new ExpressionList(1);
            MethodCall call = new MethodCall(mb, setterArguments);
            if (!baseCall && setter.IsVirtualAndNotDeclaredInStruct) 
              call.NodeType = NodeType.Callvirt;
            call.Type = SystemTypes.Void;
            return call;
          }
          Expression tObj = mb.TargetObject;
          if (tObj != null && tObj.Type != null && tObj.Type.IsValueType){
            Expression tgt = this.VisitExpression(tObj);
            AddressDereference adr = tgt as AddressDereference;
            if (adr != null)
              tgt = adr.Address;
            else if (!(tgt is This))
              tgt = this.VisitAddressOfExpression(new UnaryExpression(tgt, NodeType.AddressOf, tgt.Type.GetReferenceType()));
            return new MemberBinding(tgt, mb.BoundMember);
          }
          return this.VisitExpression(mb);
        }
      }
      return this.VisitExpression(target);
    }
    public override Expression VisitTernaryExpression(TernaryExpression expression){
      if (expression == null) return null;
      if (expression.NodeType != NodeType.Conditional) return base.VisitTernaryExpression(expression);
      Block falseBranch = new Block(null);
      Block endOfConditional = new Block(null);
      StatementList statements = new StatementList(6);
      BlockExpression bexpression = new BlockExpression(new Block(statements), expression.Type);
      statements.Add(this.VisitAndInvertBranchCondition(expression.Operand1, falseBranch, new SourceContext()));
      statements.Add(new ExpressionStatement(this.VisitExpression(expression.Operand2)));
      statements.Add(new Branch(null, endOfConditional));
      statements.Add(falseBranch);
      statements.Add(new ExpressionStatement(this.VisitExpression(expression.Operand3)));
      statements.Add(endOfConditional);
      return bexpression;
    }
    public override Statement VisitTry(Try Try){
      if (Try == null) return null;
      if (this.currentMethod == null) return null;
      if (this.currentMethod.ExceptionHandlers == null) this.currentMethod.ExceptionHandlers = new ExceptionHandlerList();
      this.continueTargets.Add(Try);
      this.exitTargets.Add(Try);
      int savedIteratorEntryPointsCount = this.iteratorEntryPoints == null ? 0 : this.iteratorEntryPoints.Count;
      this.currentTryStatements.Push(Try);
      Block savedCurrentExceptionBlock = this.currentExceptionBlock;
      this.currentExceptionBlock = Try.TryBlock;
      Block result = this.VisitBlock(Try.TryBlock);
      if (result == null) return null;
      if (result.Statements == null) result.Statements = new StatementList(1);
      if (this.iteratorEntryPoints != null && this.iteratorEntryPoints.Count > savedIteratorEntryPointsCount) return result;
      Block blockAfterHandlers = new Block(null);
      result.Statements.Add(new Branch(null, blockAfterHandlers, false, false, true));
      Block blockAfterTryBody = new Block(null);
      result.Statements.Add(blockAfterTryBody);
      this.AddExceptionHandlers(Try, result, blockAfterTryBody, blockAfterHandlers);
      result.Statements.Add(blockAfterHandlers);
      this.continueTargets.Count--;
      this.exitTargets.Count--;
      this.currentTryStatements.Pop();
      this.currentExceptionBlock = savedCurrentExceptionBlock;
      return result;
    }
    private void AddExceptionHandlers(Try Try, Block tryStartBlock, Block blockAfterTryBody, Block blockAfterHandlers) {
      Block blockAfterLastCatchEnd = blockAfterTryBody;
      for (int i = 0, n = Try.Catchers == null ? 0 : Try.Catchers.Count; i < n; i++) {
        Catch catcher = Try.Catchers[i];
        TypeNode catcherType = this.VisitTypeReference(catcher.Type);
        if (catcherType == null) catcherType = SystemTypes.Object;
        ExceptionHandler cb = new ExceptionHandler();
        cb.TryStartBlock = tryStartBlock;
        cb.BlockAfterTryEnd = blockAfterTryBody;
        cb.HandlerStartBlock = new Block(new StatementList(3));
        if (catcher.Variable == null)
          catcher.Variable = new Local(Identifier.Empty, catcherType);
        else
          catcher.Variable = this.VisitTargetExpression(catcher.Variable);
        if (catcher.Variable is Local)
          cb.HandlerStartBlock.Statements.Add(new AssignmentStatement(catcher.Variable, new Expression(NodeType.Pop)));
        else {
          Local loc = new Local(Identifier.Empty, catcherType);
          if (catcher.Variable == null) catcher.Variable = loc;
          cb.HandlerStartBlock.Statements.Add(new AssignmentStatement(loc, new Expression(NodeType.Pop)));
          cb.HandlerStartBlock.Statements.Add(new AssignmentStatement(catcher.Variable, loc));
        }
        this.currentExceptionBlock = catcher.Block;
        cb.HandlerStartBlock.Statements.Add(this.VisitBlock(catcher.Block));
        cb.HandlerStartBlock.Statements.Add(new Branch(null, blockAfterHandlers, false, false, true));
        cb.BlockAfterHandlerEnd = new Block(null);
        cb.HandlerStartBlock.Statements.Add(cb.BlockAfterHandlerEnd);
        cb.FilterType = catcherType;
        cb.HandlerType = NodeType.Catch;
        this.currentMethod.ExceptionHandlers.Add(cb);
        tryStartBlock.Statements.Add(cb.HandlerStartBlock);
        blockAfterLastCatchEnd = cb.BlockAfterHandlerEnd;
      }
      //TODO: handle filters and fault blocks
      if (Try.Finally != null && Try.Finally.Block != null && Try.Finally.Block.Statements.Count > 0) {
        ExceptionHandler fb = new ExceptionHandler();
        fb.TryStartBlock = tryStartBlock;
        fb.BlockAfterTryEnd = blockAfterLastCatchEnd;
        this.currentExceptionBlock = Try.Finally.Block;
        fb.HandlerStartBlock = this.VisitBlock(Try.Finally.Block);
        fb.HandlerStartBlock.Statements.Add(new EndFinally());
        fb.BlockAfterHandlerEnd = new Block(null);
        fb.HandlerStartBlock.Statements.Add(fb.BlockAfterHandlerEnd);
        fb.HandlerType = NodeType.Finally;
        this.currentMethod.ExceptionHandlers.Add(fb);
        tryStartBlock.Statements.Add(fb.HandlerStartBlock);
      }
    }
    public override TypeContract VisitTypeContract(TypeContract contract) {
      // We *don't* want to walk into the contract; there is no code to generate for the
      // invariants here. That is done in ImplementInvariantHoldsMethod.
      return contract;
    }
    public virtual bool HasInvariant(Interface iface){
      if (iface == null) return false;
      if (iface.Contract != null && iface.Contract.InvariantCount > 0) return true;
      InterfaceList interfaces = this.GetTypeView(iface).Interfaces;
      if (interfaces == null) return false;
      for (int i = 0, n = interfaces.Count; i < n; i++){
        Interface iface2 = interfaces[i];
        if (iface2 == null) continue;
        if (HasInvariant(iface2)) return true;
      }
      return false;
    }
    public override TypeNode VisitTypeNode(TypeNode typeNode){
      if (typeNode == null) return null;

      Struct s = typeNode as Struct;
      if (s != null) {
        // if there are no instance fields, we must adjust the size of this struct
        int numFields = 0;
        for (int i = 0, n = s.Members.Count; i < n; i++) {
          Member m = s.Members[i];
          if (m != null) {
            Field f = m as Field;
            if (f != null && !f.IsStatic && !f.IsLiteral) numFields++;
          }
        }
        if (numFields == 0) {
          if ((s.Flags & TypeFlags.LayoutMask) == TypeFlags.AutoLayout) {
            Debug.Assert(TypeFlags.AutoLayout == 0); // otherwise we need to use &= ~LayoutMask here
            s.Flags |= TypeFlags.SequentialLayout;
          }
          if (s.ClassSize == 0)
            s.ClassSize = 1;
        }
      }
      
      if (typeNode.IsNormalized) {
        this.VisitMemberList(typeNode.Members);
        return typeNode;
      }
      TypeNode savedCurrent = this.currentType;
      this.currentType = this.typeSystem.currentType = typeNode;
      if (typeNode.PartiallyDefines != null){
        if (this.visitedCompleteTypes == null) this.visitedCompleteTypes = new TrivialHashtable();
        if (this.visitedCompleteTypes[typeNode.PartiallyDefines.UniqueKey] == null){
          this.VisitTypeNode(typeNode.PartiallyDefines);
          this.visitedCompleteTypes[typeNode.PartiallyDefines.UniqueKey] = typeNode;
        }
        return typeNode;
      }
      if (typeNode.Attributes != null && typeNode.Attributes.Count > 0){
        SecurityAttributeList secAttrs = typeNode.SecurityAttributes;
        this.ExtractSecurityAttributes(typeNode.Attributes, ref secAttrs);
        typeNode.SecurityAttributes = secAttrs;
      }
      if (typeNode.SecurityAttributes != null && typeNode.SecurityAttributes.Count > 0)
        typeNode.Flags |= TypeFlags.HasSecurity;

      // TODO: What about structures?
      if (typeNode.NodeType == NodeType.Class){
        // This *does not* need to exist at each level of the hierarchy
        Class c = (Class)typeNode;
        ImplementCheckInvariantMethod(c); //Note: Must still be called if there are no invariants, but at least 1 modelfield.
        if (typeNode.Contract != null && typeNode.Contract.FramePropertyGetter != null) 
        AddGetFrameGuardMethod(c);
      }

      TypeNode result = base.VisitTypeNode(typeNode);
      this.VisitTemplateInstanceTypes(typeNode);

#if NOT_THERE_FOR_NOW
      /* why do we need to clear this?  it breaks compilation of extensions, since we forget the
       * initializers after the syntactically first type, but we need them for all the extensions
       */
      MemberList members = this.GetTypeView(typeNode).Members;
      for (int i = 0, n = members == null ? 0 : members.Length; i < n; i++){
        Field f = members[i] as Field;
        if (f == null) continue;
        f.Initializer = null; //These have now been moved into the constructors
      }
#endif

      this.currentType = this.typeSystem.currentType = savedCurrent;
      return result;
    }

    /// <summary>
    /// Implements the runtime check for invariants of class C. Also implements the runtime check for modelfields.
    /// ensures: If c declares invariants or modelfields, then c.Contract.InvariantMethod contains their runtime checking code; 
    /// Adds the CheckInvariant method to c.Members if it wasn't a member already.
    /// </summary>
    void ImplementCheckInvariantMethod(Class c) { // c is the class to which all of this code is going to get attached to.
      Method m = null;
      #region Get a handle m on the invariant method, create one if necessary      
      if (c.Contract == null || c.Contract.InvariantMethod == null) {
        Method invariantMethod = new Method(
          c,
          new AttributeList(),
          Identifier.For("SpecSharp::CheckInvariant"),
          new ParameterList(new Parameter(Identifier.For("throwException"), SystemTypes.Boolean)),
          SystemTypes.Boolean,
          null);
        invariantMethod.CallingConvention = CallingConventionFlags.HasThis;
        invariantMethod.Flags = MethodFlags.Private;
        m = invariantMethod;
      } else 
        m = c.Contract.InvariantMethod;
      #endregion Get a handle on the invariant method, create one if necessary

      StatementList stmts = new StatementList();
      #region Create code for all of the invariants, implicit and explicit. Add that code to stmts.
      Parameter throwException = m.Parameters[0];
      InvariantList consolidatedInvariants = new InvariantList();
      InvariantList invariants = c.Contract == null ? null : c.Contract.Invariants;
      for (int i = 0, n = invariants == null ? 0 : invariants.Count; i < n; i++)
        if (!invariants[i].IsStatic)
          consolidatedInvariants.Add(invariants[i]);
//      InterfaceList ifaces = this.GetTypeView(c).Interfaces;
      InterfaceList ifaces = c.Interfaces;
      for (int i = 0, n = ifaces.Count; i < n; i++){
        Interface iface = ifaces[i];
        if (iface == null) continue;
        GatherInheritedInstanceInvariants(iface, consolidatedInvariants);
      }

      for (int i = 0; i < consolidatedInvariants.Count; i++){
        Invariant inv = consolidatedInvariants[i];
        stmts.Add(new If(new UnaryExpression(inv.Condition, NodeType.LogicalNot, SystemTypes.Boolean),
          new Block(new StatementList(
            new If(throwException,
              new Block(new StatementList(
                new Throw(new Construct(new MemberBinding(null, SystemTypes.ObjectInvariantException.GetConstructor()), null, SystemTypes.ObjectInvariantException), inv.SourceContext)
              )),
              new Block(new StatementList(new Return(Literal.False)))))),
          null));
      }
      #endregion

      #region Create code for all of the modelfields defined by c. Add that code to stmts.
      StatementList mfStats = new StatementList();
      ExpressionList modifiesList = new ExpressionList();  // synthesize modifies clause
      foreach (ModelfieldContract mfC in c.Contract.ModelfieldContracts) {
        //Add the following code to the method:
        //  if (!E) {
        //    <mfC.Modelfield> = value(mfC.Witness);
        //    if (!E) throw exception;
        //  }
        //where E is the conjunction of (1) all satisfies clauses of the contract, and (2) all satisfies clauses of overridden contracts in superclasses.
        //Note that satisifes clauses of contracts implemented by mfC (i.e., contracts in interfaces) have been copied to mfC.
        //Note that if f in C overrides f in D, and f in D overrides f in E, then f in C overrides f in E.
        Expression E = Literal.True;
        for (ModelfieldContract currentMfC = mfC; currentMfC != null; currentMfC = currentMfC.NearestOverriddenContract) {
          foreach (Expression satClause in currentMfC.SatisfiesList) {
            if (satClause == null) continue;  //error will have been dealt with elsewhere             
            E = new BinaryExpression(satClause, E, NodeType.LogicalAnd, SystemTypes.Boolean);
          }
        }
        Expression notE = new UnaryExpression(E, NodeType.LogicalNot, SystemTypes.Boolean);
      
        #region create the if statement
        //Start with the creation of the body of the if.
        MemberBinding lhs = new MemberBinding(new This(c), mfC.Modelfield);
        Statement setF = new AssignmentStatement(lhs, mfC.Witness);
        modifiesList.Add(lhs);  // synthesize modifies clause
        String mfAsString = mfC.Modelfield.FullName;        
        MemberBinding exc = new MemberBinding(null,SystemTypes.ModelfieldException.GetConstructor(SystemTypes.String));
        Construct exception = new Construct(exc, new ExpressionList(new Literal(mfAsString,SystemTypes.String)), SystemTypes.ModelfieldException);
        Block innerIfBody = new Block(new StatementList(
          new If(throwException,
            new Block(new StatementList(
              new Throw(exception, mfC.Modelfield.SourceContext)
            )),
            new Block(new StatementList(new Return(Literal.False))))));        

        Statement innerIf = new If(notE, innerIfBody, null);
        StatementList body = new StatementList();
        body.Add(setF);
        body.Add(innerIf);          
        
        Statement outerIf = new If(notE, new Block(body), null);
        #endregion
        mfStats.Add(outerIf);
      }                                   
      #endregion
                                     
      #region If c declares invariants or modelfields, then add a contract to c if it has none, and make sure that m is c's InvariantMethod.
      if (stmts.Count > 0 || mfStats.Count > 0) {

        Duplicator dup = new Duplicator(this.currentModule, this.currentType);
        dup.DuplicateFor[throwException.UniqueKey] = throwException;
        stmts = dup.VisitStatementList(stmts);
        mfStats = dup.VisitStatementList(mfStats);        

        m.Body = new Block(stmts);
        m.Body.Statements.Add(new Block(mfStats)); //The model field code should be wrapped in a ContractMarkerException block, but I can't get it to work
        m.Body.Statements.Add(new Return(Literal.True));        
        
        m.Body.HasLocals = true;  //who knows? there might be locals in the invariants or model fields (quantifier bound variables).
        
        #region Slap on NoDefaultContract and (what is roughly the equivalent of) a requires this.PreValid. //I doubt if this is still needed
        //No need for a runtime check of this precondition though, so directly add an attribute.
        //Bit of a hack, but there does not seem to be a really good place to do this.        

        InstanceInitializer ndCtor = SystemTypes.NoDefaultContractAttribute.GetConstructor();
        if (ndCtor != null) 
          m.Attributes.Add(new AttributeNode(new MemberBinding(null, ndCtor), null, AttributeTargets.Method));        

        TypeNode guard = SystemTypes.Guard;
        if (guard != null) {
          Method method = guard.GetMethod(Identifier.For("FrameIsPrevalid"), SystemTypes.Object, SystemTypes.Type);
          if (method != null)
          {
              This t = new This(c);
              Expression req = new MethodCall(
                                new MemberBinding(null, method),
                                new ExpressionList(t, new UnaryExpression(new Literal(t.Type, SystemTypes.Type), NodeType.Typeof, OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Type))));

              // Place it in the method contract so downstream tools that are in the compiler pipeline see it
              if (m.Contract == null)
              {
                  m.Contract = new MethodContract(m);
              }
              if (m.Contract.Requires == null)
              {
                  m.Contract.Requires = new RequiresList(1);
              }
              m.Contract.Requires.Add(new RequiresPlain(req));

              m.Contract.Modifies = modifiesList;  // needed for model fields


              // Since this happens after contracts are serialized, serialize the precondition and stick it in the method's attributes.
              ContractSerializer cs = new ContractSerializer(this.currentModule);
              cs.Visit(req);
              string val = cs.SerializedContract;
              InstanceInitializer ctor = SystemTypes.RequiresAttribute.GetConstructor(SystemTypes.String);
              MemberBinding attrBinding = new MemberBinding(null, ctor);
              ExpressionList args = new ExpressionList();
              args.Add(new Literal(val, SystemTypes.String));
              AttributeNode a = new AttributeNode(attrBinding, args, (AttributeTargets)0);
              m.Attributes.Add(a);

              if (modifiesList.Count > 0)
              {
                  ctor = SystemTypes.ModifiesAttribute.GetConstructor(SystemTypes.String);
                  for (int i = 0, n = modifiesList.Count; i < n; i++)
                  {
                      Expression e = modifiesList[i];
                      a = Checker.SerializeExpression(ctor, e, this.currentModule);
                      m.Attributes.Add(a);
                  }
              }
          }
        }

        #endregion
        
        if (c.Contract == null) {
          c.Contract = new TypeContract(c);
          c.Contract.DeclaringType = c;
        }
        if (c.Contract.InvariantMethod == null) {
          c.Contract.InvariantMethod = m;
          c.Members.Add(m);
        } //else assert (m == c.Contract.InvairantMethod)
      }
      #endregion           
    }
    void AddGetFrameGuardMethod(Class c){
      // class Foo{
      //   Guard @'SpecSharp::frameGuard';
      //
      //   static Guard! @'SpecSharp::GetFrameGuard'(object o){
      //     return ((Foo)o).@'SpecSharp::frameGuard';
      //   }
      //
      //   static Foo(){
      //     Guard.RegisterGuardedClass(typeof(Foo), new FrameGuardGetter(@'SpecSharp::GetFrameGuard'));
      //   }
      // }
      Parameter p = new Parameter(Identifier.For("o"), SystemTypes.Object);
      StatementList ss = new StatementList(4);
      Method m = new Method(c, null, Identifier.For("SpecSharp::GetFrameGuard"), new ParameterList(p), 
        SystemTypes.Guard, new Block(ss));
      m.CciKind = CciMemberKind.Auxiliary;
      m.Flags = MethodFlags.Static | MethodFlags.Private;
      Block success = new Block();
      ss.Add(new Branch(p, success));
      ss.Add(new Throw(new Construct(new MemberBinding(null, SystemTypes.ArgumentNullException.GetConstructor()), null, SystemTypes.ArgumentNullException)));
      ss.Add(success);
      ss.Add(new Return(this.typeSystem.ExplicitNonNullCoercion(new MemberBinding(new BinaryExpression(p, new Literal(c), NodeType.Castclass, c), c.Contract.FrameField), m.ReturnType)));
      c.Members.Add(m);
      c.Contract.GetFrameGuardMethod = m;
    }
    public override TypeNode VisitTypeParameter(TypeNode typeParameter)
    {
      if (typeParameter == null) return null;
      if (typeParameter.IsNormalized) return typeParameter;
      typeParameter.IsNormalized = true;
      if (!this.useGenerics)
      {
        TypeParameter tpar = typeParameter as TypeParameter;
        if (tpar != null)
        {
          typeParameter.Attributes.Add(new AttributeNode(new MemberBinding(null, SystemTypes.TemplateParameterFlagsAttribute.GetConstructor(SystemTypes.Int32)), new ExpressionList(new Literal((int)tpar.TypeParameterFlags, SystemTypes.Int32))));
        }
      }
      if (typeParameter.IsPointerFree)
      {
        typeParameter.Attributes.Add(new AttributeNode(new MemberBinding(null, SystemTypes.PointerFreeStructTemplateParameterAttribute.GetConstructor()), null));
      }
      else if (typeParameter.IsUnmanaged)
      {
        typeParameter.Attributes.Add(new AttributeNode(new MemberBinding(null, SystemTypes.UnmanagedStructTemplateParameterAttribute.GetConstructor()), null));
      }
      return base.VisitTypeParameter(typeParameter);
    }
    public override TypeNode VisitTypeReference(TypeNode type){
      if (type == null) return null;
      if (type is TypeExpression){/*Debug.Assert(false);*/ return null;}
      return base.VisitTypeReference(type);
    }
    public override Statement VisitTypeswitch(Typeswitch Typeswitch){
      if (Typeswitch == null) return null;
      Expression e = Typeswitch.Expression = this.VisitExpression(Typeswitch.Expression);
      if (e == null || e.Type == null) return null;
      Method getTag = this.GetTypeView(e.Type).GetMethod(StandardIds.GetTag);
      if (getTag == null) return null;
      Method getValue = this.GetTypeView(e.Type).GetMethod(StandardIds.GetValue);
      if (getValue == null) return null;
      TypeswitchCaseList oldCases = Typeswitch.Cases;
      if (oldCases == null) return null;
      int n = oldCases.Count;
      BlockList targets = new BlockList(n);
      StatementList statements = new StatementList(n+3);
      Block result = new Block(statements);
      Local unionTemp = e as Local;
      if (unionTemp == null){
        unionTemp = new Local(Identifier.Empty, e.Type);
        statements.Add(new AssignmentStatement(unionTemp, e, e.SourceContext));
      }
      Local objectTemp = new Local(Identifier.Empty, SystemTypes.Object);
      SwitchInstruction switchInstruction = new SwitchInstruction(new MethodCall(new MemberBinding(new UnaryExpression(unionTemp, NodeType.AddressOf, unionTemp.Type.GetReferenceType()), getTag), null), targets);
      switchInstruction.SourceContext = Typeswitch.SourceContext;
      Block nextStatement = new Block();
      this.exitTargets.Add(nextStatement);
      statements.Add(new AssignmentStatement(objectTemp, new MethodCall(new MemberBinding(new UnaryExpression(unionTemp, NodeType.AddressOf, unionTemp.Type.GetReferenceType()), getValue), null)));
      statements.Add(switchInstruction);
      this.VisitTypeswitchCaseList(oldCases, targets, statements, nextStatement, objectTemp);
      statements.Add(nextStatement);
      this.exitTargets.Count--;
      return result;
    }
    public virtual void VisitTypeswitchCaseList(TypeswitchCaseList oldCases, BlockList targets, StatementList statements, Block nextStatement, Local temp){
      for (int i = 0, n = oldCases.Count; i < n; i++){
        TypeswitchCase tcase = oldCases[i];
        StatementList stats = new StatementList(3);
        Block b = new Block(stats);
        if (tcase != null){
          Expression expr = null;
          if (tcase.LabelType.IsValueType)
            expr = new AddressDereference(new BinaryExpression(temp, new Literal(tcase.LabelType, SystemTypes.Type), NodeType.Unbox), tcase.LabelType);
          else
            expr = new BinaryExpression(temp, new Literal(tcase.LabelType, SystemTypes.Type), NodeType.Castclass);
          stats.Add(new AssignmentStatement(this.VisitTargetExpression(tcase.LabelVariable), expr));
          stats.Add(this.VisitBlock(tcase.Body));
        }
        stats.Add(new Branch(null, nextStatement));
        statements.Add(b);
        targets.Add(b);
      }
    }
    public override Statement VisitYield(Yield Yield){
      if (Yield == null) return null;
      if (this.iteratorEntryPoints == null) return null;
      object[] trys = this.currentTryStatements.ToArray();
      StatementList statements = new StatementList();
      Block result = new Block(statements);
      if (Yield.Expression == null)
        statements.Add(this.VisitReturn(new Return(Literal.False, Yield.SourceContext)));
      else{
        Statement astat = new AssignmentStatement(new MemberBinding(this.currentThisParameter, this.currentIteratorValue), this.VisitExpression(Yield.Expression));
        astat.SourceContext = Yield.SourceContext;
        statements.Add(astat);
        Expression index = new Literal(this.iteratorEntryPoints.Count, SystemTypes.Int32);
        statements.Add(new AssignmentStatement(new MemberBinding(this.currentThisParameter, this.currentIteratorEntryPoint), index));
        statements.Add(this.VisitReturn(new Return(new Literal(true, SystemTypes.Boolean))));
      }
      Block blockAfterReturn = new Block();
      statements.Add(blockAfterReturn);
      Block blockAfterHandlers = new Block(new StatementList());
      for (int i = trys.Length; i-- > 0; ) {
        Try t = (Try)trys[i];
        //this.AddExceptionHandlers(t, this.currentExceptionBlock, blockAfterReturn, blockAfterHandlers);
      }
      statements.Add(blockAfterHandlers);
      this.iteratorEntryPoints.Add(blockAfterHandlers);
      for (int i = trys.Length; i-- > 0; ){
        //TODO: all but the outermost finally block should itself be a try-finally
        Try t = (Try)trys[i];
        blockAfterHandlers.Statements.Add(this.VisitBlock(t.Finally.Block));
      }
      this.currentExceptionBlock = new Block();
      return result;
    }
    private NodeType InvertComparisonOperator(NodeType Operator){
      switch(Operator){
        case NodeType.Eq: return NodeType.Ne;
        case NodeType.Ge: return NodeType.Lt;
        case NodeType.Gt: return NodeType.Le;
        case NodeType.Le: return NodeType.Gt;
        case NodeType.Lt: return NodeType.Ge;
        case NodeType.Ne: return NodeType.Eq;
      }
      return Operator;
    }
  }
}
