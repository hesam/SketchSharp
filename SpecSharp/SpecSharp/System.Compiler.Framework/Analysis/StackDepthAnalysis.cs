//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  using System;
  using System.IO;
  using System.Collections;




  /// <summary>
  /// This class is meant to be used only by the StackDepthAnalysis and the
  /// StackRemovalTransformation: given a stack depth right before a statement,
  /// it walks over the statement, update the depth and calls some code transformers
  /// to modify the code (by default, they don't do anything, StackRemoval overrides
  /// them.
  /// </summary>
  internal class StackDepthVisitor : ProperOrderVisitor 
  {
    // this acts at both an input and an out (i.e., result) parameter.
    // initially, it holds the stack depth before the execution of the statement;
    // after Visit(Statement stat) finishes, it holds the stack depth after the
    // execution of the statement.
    public int depth;

    private void decrement_depth () 
    {
      if (depth == 0)
        //throw new WrongStackDepthException("negative stack depth!");
        return;
      depth--;
    }

    class WrongStackDepthException : ApplicationException {
      public WrongStackDepthException(string msg) : base(msg) {}
    }

    /// <summary>
    /// Override this if you want to replace a Pop expression with some other expression.
    /// </summary>
    /// <param name="expression">Expression to replace; must have type NodeType.Pop.</param>
    /// <param name="depth">Stack Depth right before evaluating the pop.</param>
    /// <returns>Any valid expression; by default, it returns the argument, unchanged.</returns>
    protected virtual Expression PopTransformer (Expression expression, int depth) 
    {
      return expression;
    }

    /// <summary>
    /// Override this if you want to replace a Dup statement with some other expression.
    /// </summary>
    /// <param name="statement">Statement to replace; must be an ExpressionStatement with the type
    /// of the expression equal to NodeType.Dup.</param>
    /// <param name="depth">Stack depth right before executing the dup.</param>
    /// <returns>Any valid statement; by default, it returns the argument, unchanged.</returns>
    protected virtual Statement DupTransformer(ExpressionStatement statement, int depth) 
    {
      return statement;
    }

    /// <summary>
    /// Override this if you want to replace a Call(...); Pop sequence (which is modeled
    /// by CCI as a unary expression with operator Pop and a MethodCall as its only operand).
    /// </summary>
    /// <param name="expr_stat">ExpressionStatement to replace.</param>
    /// <param name="depth">Stack depth right before the push;</param>
    /// <returns>Any valid statement; by default, it returns the argument, unchanged.</returns>
    protected virtual Statement PopExprTransformer(ExpressionStatement expr_stat, int depth) 
    {
      return expr_stat;
    }

    /// <summary>
    /// Override this if you want to replace an [implicit] Push expression expression statement
    /// with something else.
    /// </summary>
    /// <param name="expr_stat">Expression to replace.</param>
    /// <param name="depth">Stack depth right before the expression statement.</param>
    /// <returns>Any valid expression; by default, it returns the argument, unchanged.</returns>
    protected virtual Statement PushExprTransformer(ExpressionStatement expr_stat, int depth) 
    {
      return expr_stat;
    }

    /// <summary>
    /// Override this if you want to replace a Pop statement with something else.
    /// </summary>
    /// <param name="expr_stat">Expression to replace.</param>
    /// <param name="depth">Stack depth right before the Pop statement.</param>
    /// <returns>Any valid expression; by default, it returns the argument, unchanged.</returns>
    protected virtual Statement PopStatTransformer(ExpressionStatement expr_stat, int depth) 
    {
      return expr_stat;
    }



    // will be called by VisitExpressionStatement
    public override Expression VisitExpression(Expression expression) 
    {
      if (expression == null) return null;

      switch (expression.NodeType) 
      {
        case NodeType.Dup:
          throw new ApplicationException("dup should not be an expression!");
        case NodeType.Pop:
          try 
          {
            decrement_depth();
          }
          catch(Exception excp) 
          { // debug suport
            Console.Out.WriteLine("negative stack depth in " + CodePrinter.ExpressionToString(expression));
            throw excp;
          }
          return PopTransformer(expression, depth+1);
        default:
          // recursively visit the sub-expressions to search for dup and pop.
          return (Expression) base.VisitExpression(expression);
      }
    }

    public override Statement VisitExpressionStatement(ExpressionStatement expr_stat) 
    {
      Expression expr = expr_stat.Expression;

      System.Diagnostics.Debug.Assert(expr != null, "Expression statement with null expression not expected");

      // special cases: pop and dup.
      if (expr.NodeType == NodeType.Pop)
        return treat_pop_stat(expr_stat);
      if (expr.NodeType == NodeType.Dup) 
      {
        depth++;
        return DupTransformer(expr_stat, depth-1);
      }

      // recursively explore the expression of this statement
      expr_stat.Expression = expr = (Expression) Visit(expr);
      // the [transformed] statement to be returned from this method
      // by default, it's the original statement.
      Statement new_stat = expr_stat;

      switch (expr.NodeType) 
      {
        case NodeType.Jmp : // TODO: what's this?	
          throw new NotImplementedException("NodeType.Jmp in StackDepthVisitor");

        case NodeType.Calli :	
        case NodeType.Call :
        case NodeType.Callvirt :
        case NodeType.MethodCall :
          Member bm = ((MemberBinding) ((MethodCall) expr).Callee).BoundMember;

          if (bm is Method) 
          {
            Method callee = (Method)bm;
            TypeNode ret_type = callee.ReturnType;
            if (! CciHelper.IsVoid(ret_type)) 
            {
              new_stat = PushExprTransformer(expr_stat, depth);
              // the called method pushed its non-void result on the evaluation stack
              depth++;
            }
          }
          else if (bm is FunctionPointer) 
          {
            FunctionPointer callee = (FunctionPointer)bm;
            TypeNode ret_type = callee.ReturnType;
            if (! CciHelper.IsVoid(ret_type))
            {
              new_stat = PushExprTransformer(expr_stat, depth);
              // the called method pushed its non-void result on the evaluation stack
              depth++;
            }
          }
          else 
          {
            throw new NotImplementedException("StackDepthVisitor: Call case: bound member is " + bm.GetType().Name);
          }

          break;
        case NodeType.Initblk:
        case NodeType.Cpblk:
          // Unfortunately, Herman's ternary expressions are not expressions: the reader uses them
          // to model Cpblk and Initblk.  As these "expressions" don't push anything on the stack,
          // they we don't do anything for them.
          break;
        default:
          new_stat = PushExprTransformer(expr_stat, depth);
          // the value of the expression was pushed on the stack
          depth++;
          break;
      }
      return new_stat;
    }

    // deals with the special case of a pop statement
    private Statement treat_pop_stat(ExpressionStatement expr_stat) 
    {
      Statement new_stat = expr_stat;
      if (CciHelper.IsPopStatement(expr_stat)) 
      {
        // real pop statement!
        new_stat = PopStatTransformer(expr_stat, depth);
        decrement_depth();
      }
      else 
      {
        // expr followed by a pop that removes the result of the expr
        // the stack depth remains unchanged: +1-1 = 0.
        // WARNING: if Herman changes his stuff, this might break ...
        UnaryExpression unexpr = (UnaryExpression) expr_stat.Expression;

        System.Diagnostics.Debug.Assert(unexpr != null, "Expected non-null expression in expression statement.");

        Expression unarg = (Expression) Visit(unexpr.Operand);
        System.Diagnostics.Debug.Assert(unarg!=null, "Visit must return non-null if passed non-null");
        unexpr.Operand =  unarg;
        new_stat = PopExprTransformer(expr_stat, depth);
      }
      return new_stat;
    }

    public override Statement VisitReturn(Return ret) 
    {
      base.VisitReturn(ret);
      if (depth != 0)
        return null;
        //throw new WrongStackDepthException
          //("return: evaluation stack should be empty except for the value to be returned, not " + depth);
      return ret;
    }

    public override Statement VisitThrow(Throw thrw) 
    {
      base.VisitThrow(thrw);
      // when a throw occurs, the entire evaluation stack is flushed out.
      depth = 0;
      return thrw;
    }

    public override Statement VisitBranch(Branch br) 
    {
      base.VisitBranch(br);
      // when leaving a block, the entire evaluation stack is flushed out
      if (br.LeavesExceptionBlock)
        depth = 0;
      return br;
    }

    public override Statement VisitEndFinally(EndFinally endFinally) 
    {
      //base.VisitEndFinally(endFinally);
      // endfinally / endfault flushes the evaluation stack
      depth = 0;
      return endFinally;
    }

    public override Statement VisitEndFilter(EndFilter endFilter) 
    {
      base.VisitEndFilter(endFilter); // endfilter has an argument!

      if (depth != 0)
        throw new WrongStackDepthException("after endfilter, stack should be empty");

			// We put the depth at 1, since we treat it as a fall through, and in the
			// handler, we have the exception on the stack.
			depth = 1;
      return endFilter;
    }
  }


  
  
  /// <summary>
  /// Computes stack depth for each basic block and whether it is reachable from the entry.
  /// 
  /// Also changes all pop, dup and push instructions into corresponding actions on stack temporary locals.
  /// </summary>
  public class StackRemovalTransformation
  {
    public readonly int[] block2depth;
    readonly bool[] visited;

    private StackRemovalTransformation(ControlFlowGraph cfg) {
      this.block2depth = new int[cfg.BlockCount];
      this.visited = new bool[cfg.BlockCount];
  
      // initialize everything to unreachable
      for (int j=0; j<block2depth.Length; j++) { block2depth[j] = -1; }

    }

    private void PushDepth(CfgBlock succ, int depth) {
      int olddepth = block2depth[succ.Index];

      if (visited[succ.Index]) {
        //System.Diagnostics.Debug.Assert( olddepth == depth, "Stack depth differs" );
        return;
      }
      visited[succ.Index] = true;
      block2depth[succ.Index] = depth;
    }

    private static readonly StackDepthVisitor sdv = new StackDepthVisitor();

    private int InitialDepthOfBlock(CfgBlock block, ControlFlowGraph cfg) {
      int sd = block2depth[block.Index];

      if (this.visited[block.Index]) return sd;

      this.visited[block.Index] = true;
      int depth;

      ExceptionHandler eh = cfg.HandlerThatStartsAtBlock(block);
      if (eh == null) {
        // if we haven't seen this block and it is not the entry block
        // nor the Exception Exit of the method
        // it is unreachable
        if (block == cfg.Entry) {
          depth = 0;
        }
        else if (block == cfg.ExceptionExit) {
          depth = 0;
        }
        else {
          depth = -1;
        }
      }
      else {
        switch (eh.HandlerType) {
          case NodeType.Catch:
          case NodeType.FaultHandler:
          case NodeType.Filter:
            depth = 1;
            break;
          case NodeType.Finally:
            depth = 0;
            break;
          default:
            throw new ApplicationException("unknown handler type");
        }
      }
      block2depth[block.Index] = depth;
      return depth;
    }

    /// <summary>
    /// Examines a CFG and removes the stack manipulating instructions by introducing
    /// some explicit variables for stack locations.  After this transformation, no more
    /// Pop, Dup etc.
    /// </summary>
    /// <param name="cfg">Control Flow Graph that is modified by the transformation.
    /// This argument WILL be mutated.</param>
    /// <returns>A map that assigns to each block from <c>cfg</c> the stack depth at its
    /// beginning.  Useful as a pseudo-liveness information.</returns>
    public static int[] Process(ControlFlowGraph cfg) 
    {
      StackRemovalTransformation sd = new StackRemovalTransformation(cfg);

			StackRemovalVisitor srv = new StackRemovalVisitor();

      // should be in order.
      foreach (CfgBlock block in cfg.PreOrder) {
        int depth = sd.InitialDepthOfBlock(block, cfg);

        if (depth < 0) { 
          continue;
        }

        // set block starting depth
        srv.depth = depth;

        StatementList stats = block.Statements;
        for(int i = 0, n = stats.Count; i < n; i++) {
          stats[i] = (Statement) srv.Visit(stats[i]);
          if (cfg.GenericsUse == null) { cfg.GenericsUse = srv.GenericsUse; }
          if (cfg.PointerUse == null) { cfg.PointerUse = srv.PointerUse; }
          if (srv.SawMissingInfo) { cfg.HasMissingInfo = true; }
        }

        // push final depth onto successors.
        foreach (CfgBlock succ in cfg.NormalSucc(block)) {
          sd.PushDepth(succ, srv.depth);
        }
      }

      // finalize stack depth info on each block
      foreach (CfgBlock block in cfg.Blocks()) 
      {
        int depth = sd.block2depth[block.Index];
        // cache depth on block
        block.stackDepth = depth;

        if (depth < 0) {
          // unreachable
          // set statementlist to null in case some backward analysis or other code gets to it
          if (block.Statements.Count > 0) {
            block.Statements = new StatementList(0);
          }
        }
      }
      return sd.block2depth;
    }

		
    private class StackRemovalVisitor : StackDepthVisitor 
    {
			private Node genericsUse = null;
			public Node GenericsUse { get { return this.genericsUse; } }

			private Node pointerUse = null;
			public Node PointerUse { get { return this.pointerUse; } }

			private bool sawMissingInfo = false;
			public bool SawMissingInfo { get { return this.sawMissingInfo; } }


			private static bool ContainsPointer (TypeNode ty)
			{
				if (ty is Pointer) { return true; }
				//if (ty == SystemTypes.IntPtr) { return true; }
				if (ty is Reference) { return ContainsPointer( ((Reference)ty).ElementType); }
				if (ty is ArrayType) { return ContainsPointer( ((ArrayType)ty).ElementType); }
				return false;
			}


			private void Inspect (TypeNode ty, Node node)
			{
				if (ty == null) { this.sawMissingInfo = true; return; }
				if (ty.IsGeneric) { this.genericsUse = node; }
				if (ContainsPointer(ty)) { this.pointerUse = node; }
			}


			public override Expression VisitParameter (Parameter parameter)
			{
				Inspect(parameter.Type, parameter);
				return base.VisitParameter(parameter);
			}


			public override Expression VisitLocal (Local local)
			{
				Inspect(local.Type, local);
				return base.VisitLocal(local);
			}


			public override Expression VisitThis (This This)
			{
				Inspect(This.Type, This);
				return base.VisitThis(This);
			}

			
			public override Expression VisitMethodCall (MethodCall call)
			{
				Method callee = ((MemberBinding)call.Callee).BoundMember as Method;
        if (callee == null) 
        { 
          // call to function pointer
          this.pointerUse = call; 
        }
        else 
        {
          if (callee.IsGeneric) { this.genericsUse = call; }
          if (callee.Parameters!=null)
            for (int i = 0; i < callee.Parameters.Count; i++)
            {
              Inspect(callee.Parameters[i].Type, call);
            }
          Inspect(callee.ReturnType, call);
        }
				return base.VisitMethodCall(call);
			}


			public override Method VisitMethod (Method method)
			{
				if (method.IsGeneric) { this.genericsUse = method; }
				return base.VisitMethod(method);
			}


			public override Expression VisitMemberBinding (MemberBinding memberBinding)
			{
				Inspect(memberBinding.Type, memberBinding);
				return base.VisitMemberBinding(memberBinding);
			}





      /// <summary>
      /// Replace a pop expression with the appropriate stack variable.
      /// </summary>
      protected override Expression PopTransformer(Expression expression, int depth) 
      {
        return get_stack_var(depth-1, expression);
      }

      /// <summary>
      /// Replace a dup expression with the appropriate stack variable.
      /// </summary>
      protected override Statement DupTransformer(ExpressionStatement expr_stat, int depth) 
      {
        Statement new_stat = new AssignmentStatement(get_stack_var(depth, expr_stat.Expression), get_stack_var(depth-1, expr_stat.Expression));
        new_stat.SourceContext = expr_stat.SourceContext;
        return new_stat;
      }

      /// <summary>
      /// Remove the Pop from a CciHelper statement "sequence" of the form "Pop expr".
      /// </summary>
      protected override Statement PopExprTransformer(ExpressionStatement expr_stat, int depth) 
      {
        Expression real_expr = ((UnaryExpression) expr_stat.Expression).Operand;
        Statement new_stat;
        new_stat = new AssignmentStatement(get_stack_var(depth, expr_stat.Expression), real_expr);
        real_expr.SourceContext = expr_stat.SourceContext;
        new_stat.SourceContext  = expr_stat.SourceContext;
        return new_stat;
      }

      /// <summary>
      /// Replace an implicit Push with an assignment to the appropriate stack variable.
      /// </summary>
      protected override Statement PushExprTransformer(ExpressionStatement expr_stat, int depth) 
      {
        Statement new_stat = new AssignmentStatement(get_stack_var(depth, expr_stat.Expression, ref expr_stat.SourceContext), expr_stat.Expression);
        new_stat.SourceContext = expr_stat.SourceContext;
        return new_stat;
      }

      /// <summary>
      /// Replace an explicit Pop statement with a nop: stack is modeled by stack vars now.
      /// </summary>
      protected override Statement PopStatTransformer(ExpressionStatement expr_stat, int depth) 
      {
        Statement new_stat = new Statement(NodeType.Nop);
        new_stat.SourceContext = expr_stat.SourceContext;
        return new_stat;
      }


      // TODO: in addition to the treatment done by base.VisitThrow, transform rethrown -> throw stack_0.
      public override Statement VisitThrow(Throw thrw)
      {
        Statement stat = base.VisitThrow(thrw);
        thrw = stat as Throw;
        if (thrw == null) return stat;
        /* TODO: see if the following lines are correct
        if (thrw.Expression == null)
          thrw.Expression = get_stack_var(0);
          */
        return thrw;
      }

      public override Statement VisitCatch(Catch c) 
      {
        c.Variable = get_stack_var(0, SystemTypes.Object);
        return c;
      }

      // returns the local variable for a specific stack depth
      private Variable get_stack_var(int depth, Expression expr, ref SourceContext ctxt) {
        System.Diagnostics.Debug.Assert(depth >= 0);
        return StackVariable.For(depth, expr, ref ctxt);
      }

      // returns the local variable for a specific stack depth
      private Variable get_stack_var(int depth, Expression expr) {
        System.Diagnostics.Debug.Assert(depth >= 0);
        return StackVariable.For(depth, expr);
      }

      // returns the local variable for a specific stack depth
      private Variable get_stack_var(int depth, TypeNode type) 
      {
        System.Diagnostics.Debug.Assert(depth >= 0);
				return StackVariable.For(depth, type);
#if OLD_SHARED_STACK_VARIABLES_WITHOUT_TYPE_INFORMATION
        if (depth2stack_var == null)
          depth2stack_var = new Hashtable();
        Variable var = (Variable) depth2stack_var[depth];
        if (var == null) 
        {
          var = StackVariable.For(depth);
          depth2stack_var[depth] = var;
        }
        return var;
#endif
      }

#if OLD_SHARED_STACK_VARIABLES_WITHOUT_TYPE_INFORMATION
			// map depth -> corresponding stack variable
      [MayBeNull]
      private Hashtable/*<int,StackVariable>*/ depth2stack_var;
#endif
    }
  }


}
