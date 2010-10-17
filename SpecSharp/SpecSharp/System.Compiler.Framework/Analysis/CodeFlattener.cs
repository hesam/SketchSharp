//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
#if CCINamespace
namespace Microsoft.Cci{
  using Cci = Microsoft.Cci;
#else
namespace System.Compiler{
  using Cci = System.Compiler;
#endif
  using System;
  using System.Diagnostics;
  using System.Collections.Generic;

  public class CodeMap {
    private Dictionary<ExpressionStatement, ExpressionStatement> orig2New;

    internal CodeMap(Dictionary<ExpressionStatement, ExpressionStatement> map) {
      this.orig2New = map;
    }

    public ExpressionStatement this[ExpressionStatement key] {
      get {
        ExpressionStatement result;
        this.orig2New.TryGetValue(key, out result);
        return result;
      }
    }
  }


	/// <summary>
	/// Transforms the nested CCI representation into a flat one.
	/// 
	/// More precisely it does 3 things
	/// - It turns nested block structures into a 2 level structure, the method body is a block consisting of blocks. These
	///   2nd level blocks only contain statements, not nested blocks. All branch targets are to a block in the 2nd level.
	///   
	/// - All statements are simplified to three address codes, by splitting complicated expressions so that they push their
	///   result onto the stack, whereas the continuation pops it off the stack.
	///   
	/// - Produces only CfgBlocks
	/// 
	/// In order to correctly deal with nested dup expressions which appear when the input is not read in
	/// from an external DLL, but is the output of the compiler, we need to use an explicit stack model.
	/// Consider the following problem:
	/// 
	///    Construct(Delegate, [ local0, BinaryExpr(ldvirtfn, dup, Test.M) ]
	///    
	/// The dup instruction acts on the local0, which is the first argument to the constructor. Previously,
	/// we would try to use this local0 directly, without stacking it. Now we have to stack everything.
	/// 
	/// Stacking everything has the undesirable side effect that data flow analyses that do branch refinement have
	/// a hard time updating information about tests, since the tests always involve stack variables.
	/// </summary>
	public class CodeFlattener : EmptyVisitor
	{

    This thisNode;

    /// <summary>
    /// When true, an allocation is split into a separate memory alloc, followed by an explicit constructor call.
    /// </summary>
    private bool expandAllocations;

    /// <summary>
    /// When true, use branch conditions that are literals to prune infeasible execution paths.
    /// In general, we always want this to be done. But if it is done for the code that
    /// does the runtime checking of contracts, that would influence whether the "regular"
    /// code downstream of the branch is represented in the CFG or not.
    /// </summary>
    private bool performConstantFoldingOnBranchConditions = true;

    private CodeFlattener(Method method, bool expandAllocations, bool constantFold) 
    {
      this.Method = method;
      this.expandAllocations = expandAllocations;
      this.performConstantFoldingOnBranchConditions = constantFold;
      this.new_stats = new StatementList();
      this.new_blocks = new StatementList();
      this.thisNode = CciHelper.GetThis(method);
      if (thisNode != null && thisNode.Type == null) {
        thisNode.Type = method.DeclaringType;
      }
    }

    private readonly Method Method;

    /// <summary>
    /// Contains mappings from original ExpressionStatement to new ExpressionStatement
    /// </summary>
    private Dictionary<ExpressionStatement, ExpressionStatement> orig2Copy = new Dictionary<ExpressionStatement, ExpressionStatement>();

    /// <summary>
    /// Flattens the code of the method <c>method</c>.  Leaves the CCI representation of <c>method</c> intact.
    /// 
    /// Returns a mutated copy.
    /// 
    /// Important! don't forget to adjust handler block boundaries as well
    /// </summary>
    /// <param name="expandAllocations">When true, then Construct expressions are expanded
    /// into a separate allocation and separate constructor call.</param>
    public static Method MakeFlat(Method method, bool expandAllocations, out CodeMap codeMap) {
      return MakeFlat(method, expandAllocations, true, out codeMap);
    }
    /// <summary>
    /// Flattens the code of the method <c>method</c>.  Leaves the CCI representation of <c>method</c> intact.
    /// 
    /// Returns a mutated copy.
    /// 
    /// Important! don't forget to adjust handler block boundaries as well
    /// </summary>
    /// <param name="expandAllocations">When true, then Construct expressions are expanded into a separate allocation and separate
    /// constructor call.</param>
    /// <param name="constantFoldEvenInContracts">When <c>true</c>, use constant folding 
    /// to prune infeasible branches.</param>
    public static Method MakeFlat(Method method, bool expandAllocations, bool constantFold, out CodeMap codeMap)
    {
      /*
      Console.Out.WriteLine("+++++++++++++++++++++++++++");
      CodePrinter.PrintMethod(Console.Out, method);
      */

      #region Compensate for a bug involving re-initializing a method body
      {
        // We're going to muck with the method's Body. But then at one point the ExceptionHandlers
        // are evaluated and since that is null, it causes the Body to be re-initialized from the IL.
        // So just force some evaluations which fill in the backing stores to an empty list
        // (at least) instead of "null".
        // Do this for Body, Instructions, and ExceptionHandlers (the three things that cause
        // the ProvideBody delegate to execute).
        Block dummyBody = method.Body;
        InstructionList dummyInstructions = method.Instructions;
        ExceptionHandlerList dummyExceptions = method.ExceptionHandlers;
      }
      #endregion Compensate for a bug involving re-initializing a method body

      method = (Method)method.Clone();
      Block body = method.Body;

      if ((method == null) || (body == null)) {
        codeMap = null;
        return method;
      }

      // Add test case by inserting BlockExpressions and nested blocks
      // body = new FlattenerTest().VisitBlock(body);

      if(body.Statements == null) {
        codeMap = null;
        return method;
      }

      body = (Block)body.Clone();
      method.Body = body;

      CodeFlattener flatener = new CodeFlattener(method, expandAllocations, constantFold);

      flatener.FlattenBlock(body);

      flatener.AdjustBranches();

      flatener.AdjustHandlers(method);

      // now store the bodyList in the method body block

      body.Statements = flatener.new_blocks;

      /*
      Console.Out.WriteLine("---------------------------");
      CodePrinter.PrintMethod(Console.Out, method);
      Console.Out.WriteLine();
      */

      /*
      Console.WriteLine("----CodeFlattener on {0}", method.FullName);
      Console.WriteLine("orig blocks: {0}", flatener.orig2newBlocks.Count);
      Console.WriteLine("branches: {0}", flatener.branchInstructions.Length);
      */
      codeMap = new CodeMap(flatener.orig2Copy);
      return method;
    }


		/// <summary>
		/// Maintains the mapping from original blocks to new blocks so we can adjust branch targets
		/// </summary>
		TrivialHashtable orig2newBlocks = new TrivialHashtable();


		/// <summary>
		/// Used to build up a list of all branch statements, so that in a post pass we can adjust their targets
		/// using the orig2newBlocks mapping
		/// </summary>
		StatementList branchInstructions = new StatementList();

		/// <summary>
		/// Used to build up a list of all switch statements, so that in a post pass we can adjust their targets
		/// using the orig2newBlocks mapping
		/// </summary>
		StatementList switchInstructions = new StatementList();


		/// <summary>
		/// To accumulate statements from a block. The transformed statements
		/// go onto this list.
		/// </summary>
		private StatementList new_stats;

		/// <summary>
		/// Accumulates the newly created blocks.
		/// 
		/// </summary>
		readonly private StatementList new_blocks;


		/// <summary>
		/// Holds either null, or the current old block that must be mapped to the
		/// next dynamically created new block in the map.
		/// 
		/// Note: Because a nested block can appear as the first statement of
		/// a block, we have to be careful. There can be multiple outstanding oldBlocks
		/// that need to be mapped to the first generated block.
		/// 
		/// We do this by the following invariant:
		/// Every block expansion starts with a call to FlattenBlock
		///  The FlattenBlock stack activation frame keeps track of the prior current_oldBlock 
		///  and stores the new current oldBlock.
		///  At the end of FlattenBlock, the prior_oldBlock is mapped to the same new block
		///  as the oldBlock on which FlattenBlock was called.
		/// </summary>
		private Block current_oldBlock;


		/// <summary>
		/// Invariant: whenever during the traversal, we find that we need to start a new block
		/// (because we reach a block that could be the target of a branch),
		/// we have to take the new_stats StatementList and if non-empty, create a new block from it
		/// and put it onto the new_blocks list. new_stats is then initialized with a new empty list.
		/// 
		/// We also have to update the orig2newblock map, using the current_oldBlock as the key and the
		/// newly created block as the target. If we update the map, we null out the current_oldBlock.
		/// </summary>
		private void EndStatementList() 
		{
			if (this.new_stats.Count != 0) 
			{
				// create block from statements
				CfgBlock b = new CfgBlock(this.new_stats);

				// add block to block list
				this.new_blocks.Add(b);

				// create new statement list for upcoming statements
				this.new_stats = new StatementList();

				// update map
				UpdateBlockMap(ref this.current_oldBlock, b);
			}
		}

		
		
		/// <summary>
		/// if oldBlock != null, then we need to update the blockMap to map oldBlock to newBlock
		/// and set oldBlock to null.
		/// 
		/// POST: oldBlock == null
		/// </summary>
		void UpdateBlockMap(ref Block oldBlock, Block newBlock) 
		{
      if ( oldBlock != null ) {
        // update map from oldBlock to first block in the expansion
        this.orig2newBlocks[oldBlock.UniqueKey] = newBlock;

        // update source context information
        newBlock.SourceContext = oldBlock.SourceContext;

        oldBlock = null;
      }
		}




		/// <summary>
		/// Can be called to recursively flatten a block from the following places:
		/// 1. From within a block on a nested block
		/// 2. From within a BlockExpression
		/// 3. From the top-level method body block
		/// 
		/// Because of 2 and 3, we may have a pending list of statements in new_stats that belong
		/// to the previous block. Thus we first need to end that block, then start the new one.
		/// 
		/// Furthermore, since the old block could be a branch target, we also must update the 
		/// orig2newBlock map once we generated the first block within this flattening.
		/// 
		/// Every block expansion starts with a call to FlattenBlock
		///  The FlattenBlock stack activation frame keeps track of the prior current_oldBlock 
		///  and stores the new current oldBlock.
		///  At the end of FlattenBlock, the prior_oldBlock is mapped to the same new block
		///  as the oldBlock on which FlattenBlock was called.
		///  
		///  POST: this.current_oldBlock == null.
		/// </summary>
		/// <param name="block"></param>
		private void FlattenBlock (Block block) 
		{
			// This definitely starts a new block. If the statement list contains any statements, 
			// finish that block.
			EndStatementList();

			// stack the prior oldBlock (this must be done AFTER we call EndStatementList !)
			Block prior_oldBlock = this.current_oldBlock;

			// set the block we are processing as the current old block
			this.current_oldBlock = block;

      StatementList sl = block.Statements;

			// we deal with an empty block on exit of this method.
			try 
			{
				if (sl != null) 
				{
					for (int i = 0; i<sl.Count; i++)
					{
						Statement stmt = sl[i];

            if (stmt == null) continue;

						Block nested = stmt as Block;

						if (nested != null) 
						{
							FlattenBlock(nested);
						}
						else 
						{
							// Statement to be added to current statement sequence.
							Statement newstat = (Statement)this.Visit(stmt);
              if (newstat != null) {
                // null indicates statement was omitted (e.g. branch false)
                if (newstat.SourceContext.Document == null){
                  // MB: Guarantee that every statement coming out of flattener has some kind of source context
                  // REVIEW: It is possible that if newstat happens to be shared (i.e., *not* a copy)
                  // then this will cause more sequence points to be defined in the PDB file than otherwise
                  // which could degrade the debugging experience.
                  newstat.SourceContext = this.current_source_context;
                }
                this.new_stats.Add(newstat);
                if (StatementEndsBlock(newstat)) {
                  EndStatementList();
                }
              }
						}
					} // end for
				} // end if sl != null
			}
			finally 
			{
				// we have to do 2 things here:
				//
				// 1. end the block (there could be outstanding statements that need to be emitted)
				// 2. if we still have this.current_oldBlock != null, then the block contained no statements and we have to insert an empty block
				// 3. if prior_oldBlock is not null, we have to map it to the same new block as the block we processed in this call.
				//
				// The order of these operations is important! Because we side effect this.current_oldBlock and this.orig2newBlock map, these
				// steps CANNOT be reordered.

				// Do 1.
				EndStatementList(); 

				// Do 2. 
				if (this.current_oldBlock != null) {
					// Debug.Assert (sl == null || sl.Length == 0); // too strict. Fires if sl contains null statements.

					// create empty block
					CfgBlock newBlock = new CfgBlock(new StatementList(0));
					// add to block list
					this.new_blocks.Add(newBlock);
					// update map
					this.UpdateBlockMap(ref this.current_oldBlock, newBlock);
				}

				// Do 3.
				if (prior_oldBlock != null) 
				{
					CfgBlock newBlock = (CfgBlock) this.orig2newBlocks[block.UniqueKey];

					this.orig2newBlocks[prior_oldBlock.UniqueKey] = newBlock;
				}
			}
		}


    private static bool StatementEndsBlock(Statement stat) {
      switch(stat.NodeType) {
        case NodeType.Branch:
        case NodeType.Throw:
        case NodeType.Switch:
        case NodeType.Return:
          return true;
        default:
          return false;
      }
    }

		/// <summary>
		/// Called once after we handled all blocks (by MakeFlat)
		/// </summary>
		private void AdjustBranches() 
		{
			for (int i = 0; i<this.branchInstructions.Count; i++) 
			{
				Branch branch = (Branch)this.branchInstructions[i];

				branch.Target = RemapBlock(branch.Target);
			}

      // Clone target list
			for (int i = 0; i<this.switchInstructions.Count; i++) 
			{
				SwitchInstruction sw = (SwitchInstruction)this.switchInstructions[i];

        BlockList targets = sw.Targets;
        BlockList newTargets = new BlockList(targets.Count);
        sw.Targets = newTargets;

				for (int j = 0; j<targets.Count; j++) 
				{
					Block b = targets[j];
					newTargets.Add(RemapBlock(b));
				}
			}

		}

		private void AdjustHandlers(Method method) 
		{
      ExceptionHandlerList oldList = method.ExceptionHandlers;
      if (oldList == null) return;
      ExceptionHandlerList newList = new ExceptionHandlerList(oldList.Count);

			for (int i = 0; i < oldList.Count; i++) 
			{
				ExceptionHandler h = (ExceptionHandler)oldList[i].Clone();
				h.BlockAfterHandlerEnd = RemapBlock(h.BlockAfterHandlerEnd);
				h.BlockAfterTryEnd = RemapBlock(h.BlockAfterTryEnd);
				h.HandlerStartBlock = RemapBlock(h.HandlerStartBlock);
				h.TryStartBlock = RemapBlock(h.TryStartBlock);
				h.FilterExpression = RemapBlock(h.FilterExpression);

        newList.Add(h);
			}
      method.ExceptionHandlers = newList;
		}


		private Block RemapBlock(Block b) 
		{
			if (b == null) return b;

			Block newB = (Block)this.orig2newBlocks[b.UniqueKey];

			Debug.Assert(newB != null, "Can't find mapped to block");
			return newB;
		}



		public override Block VisitBlock(Block block)
		{
			Debug.Assert(false, "should not get here, since that means we found a nested block that we didn't treat using FlattenBlocks");

			// if we get the assertion to fail, check that there are no statements with nested blocks below them.
			return block;
		}

		// the source context of the currently simplified top level statement
		private SourceContext current_source_context;

    private Expression simplify(Expression expression)
    {
      return simplify(expression, false);
    }

		private Expression simplify(Expression expression, bool leaveVars)
		{
      if (expression == null) {
        //Debug.Assert(false); //Bad code was generated, but this somehow escaped an earlier error checking pass
        return new Local(SystemTypes.Object);
      }

			// simplify the sub-expressions of expression
			Expression newExpression = this.VisitExpression(expression);

			if (newExpression == null) 
			{
				// must be turned into a pop
				return Pop(expression.Type);
			}

			if(will_be_var(newExpression, leaveVars) || is_type_literal(newExpression))
				return newExpression;
			else
			{
				return pushPop(newExpression);
			}
		}

		private Expression pushPop(Expression expression) 
		{
      SourceContext ctxt = expression.SourceContext.Document != null? expression.SourceContext:this.current_source_context;

			ExpressionStatement new_stat = new ExpressionStatement(expression, ctxt);
			Debug.Assert(new_stats != null, "must have been initialized by now.");

			new_stats.Add(new_stat);
      return Pop(expression.Type, ctxt);
		}

		private Expression Pop(TypeNode type, SourceContext ctxt) 
		{
//      Debug.Assert(type != null);
//      Debug.Assert(this.current_source_context.Document != null);
			Expression new_expr = new Expression(NodeType.Pop);
      new_expr.SourceContext = ctxt;
			new_expr.Type = type;
			return new_expr;
		}

    private Expression Pop(TypeNode type) {
      return Pop(type, this.current_source_context);
    }

		private static bool will_be_var(Expression expression, bool leaveVars)
		{
			return
				(expression == null) ||
				// if it is a stack variable, then it is the special NEW temp. It must be copied 
				// immediately.
        // Stack everything if !leaveVars
				(leaveVars && expression is Variable && (!(expression is StackVariable))) ||
				(expression.NodeType == NodeType.Pop);
		}

		private static bool is_type_literal(Expression expression)
		{
			Literal literal = expression as Literal;
			if (literal != null) return (literal.Value is TypeNode);
      MemberBinding mb = expression as MemberBinding;
      if (mb != null) return mb.BoundMember is TypeNode;
      return false;
		}

    /// <summary>
    /// 
    /// </summary>
		private Expression simplify_addressof_operand(Expression operand)
		{

      switch(operand.NodeType)
			{
				case NodeType.This:
				case NodeType.Parameter:
				case NodeType.Local:
					// 1. & variable;
              ParameterBinding pb = operand as ParameterBinding;
              if (pb != null) {
                This tp = pb.BoundParameter as This;
                if (tp != null) {
                  operand = pb = (ParameterBinding)pb.Clone();

                  pb.BoundParameter = (Parameter)this.VisitThis(tp);
                }
              }
					break;
				case NodeType.Indexer:
					// 2. & array[index]
          operand = (Expression)operand.Clone();
          Indexer indexer = (Indexer) operand;
					indexer.Object = simplify(indexer.Object);
					ExpressionList ops = VisitExpressionList(indexer.Operands);
					System.Diagnostics.Debug.Assert(ops != null, "VisitExpressionList must return non-null if arg non-null");
					indexer.Operands = ops;
					break;
				case NodeType.MemberBinding:
					// 3. & object.field
          operand = (Expression)operand.Clone();
          MemberBinding mb = (MemberBinding) operand;
					if (mb.TargetObject != null) 
					{
						mb.TargetObject = simplify(mb.TargetObject);
					}
					break;
				default:
					throw new ApplicationException("Strange AddressOf expression: ");
			}
      return operand;
		}


		public override Expression VisitBlockExpression(BlockExpression blockExpression)
		{
			// recursively flatten the block(s)
			this.FlattenBlock(blockExpression.Block);

			// the code generated from the blocks leaves the expression value on the evaluation stack
			// So we return a pop expression here.

      return Pop(blockExpression.Type);
		}



		public override Statement VisitEndFilter(EndFilter endFilter)
		{
      endFilter = (EndFilter)endFilter.Clone();

			endFilter.Value = simplify(endFilter.Value);
			return endFilter;
		}

		public override Statement  VisitEndFinally(EndFinally endfinally)
		{
			return endfinally;
		}

    /// <summary>
    /// Unify all occurrences of This
    /// </summary>
    /// <param name="This"></param>
    /// <returns></returns>
		public override Expression VisitThis(This This)
		{
      if (This is ThisBinding) return This;
			return this.thisNode;
		}

    /// <summary>
    /// BIG FIXME for CCI. Base should not occur here, since it just means "this" after
    /// normalization.
    /// </summary>
    public override Expression VisitBase(Base Base)
    {
      // hack around Herman's problem with Base in the normalized code.
      return this.thisNode;
    }

    public override Expression VisitLocal(Local local)
		{
			return local;
		}
    public override Statement VisitLocalDeclarationsStatement(LocalDeclarationsStatement localDeclarations) {
      // This node just represents the declaration of a local, e.g., "int x;"
      // No code is generated for it; it is passed in post-normalized code just
      // so the Writer can use it to associate debug info with the block containing
      // the declaration.
      return null;
    }

		public override Expression VisitParameter(Parameter parameter)
		{
			return parameter;
		}

		public override Expression VisitLiteral(Literal literal)
		{
			return literal;
		}

    public override Expression VisitQualifiedIdentifier (QualifiedIdentifier qualifiedIdentifier)
    {
      return qualifiedIdentifier;
    }


		public override Expression VisitPop(Expression Pop)
		{
			return Pop;
		}

    public override Expression VisitPopExpr(UnaryExpression unex) {
      Expression operand = (Expression)this.Visit(unex.Operand);

      unex = (UnaryExpression)unex.Clone();
      unex.Operand = operand;
      return unex;
    }


		public override Expression VisitDup(Expression Dup)
		{
			return Dup;
		}

		public override Expression VisitArglist(Expression Arglist)
		{
			return Arglist;
		}



    public override Node Visit(Node node) {
      if (node == null) return null;

      if (node.SourceContext.Document != null) {
        SourceContext saved = this.current_source_context;
        this.current_source_context = node.SourceContext;
        Node result = base.Visit(node);
        this.current_source_context = saved;
        return result;
      }

      return base.Visit(node);
    }



		public override Expression VisitAddressDereference(AddressDereference addr)
		{
      addr = (AddressDereference)addr.Clone();
			addr.Address = simplify(addr.Address);
			return addr;
		}

		public override Statement VisitAssignmentStatement(AssignmentStatement assignment)
		{
      assignment = (AssignmentStatement)assignment.Clone();

			Expression target = assignment.Target;
			Expression source = assignment.Source;

      if (assignment.SourceContext.Document == null) {
        assignment.SourceContext = this.current_source_context;
      }

			if((target == null) || (source == null))
				throw new ApplicationException("Strange CCI format " + CodePrinter.StatementToString(assignment));

			switch(target.NodeType)
			{
				case NodeType.AddressDereference:
          assignment.Target = (Expression)this.Visit(target);
          if (source is Literal && ((Literal)source).Value == null && 
            ((assignment.Target.Type != null && assignment.Target.Type.IsValueType) || (assignment.Target.Type is TypeParameter) ||
              (assignment.Target.Type is ClassParameter)))
          {
              // initobj encoding.
              return assignment;
          }
          assignment.Source = simplify(source, true);
          return assignment;

				case NodeType.MemberBinding:
				case NodeType.Indexer:
					assignment.Target = (Expression)this.Visit(target);
					assignment.Source = simplify(source, true);
					return assignment;

				case NodeType.Local:
				case NodeType.Parameter:
					// target is a Variable; we can be more relaxed on the right side
					// Note: VS has a strange indentation for switch inside a switch ...
				switch(source.NodeType)
				{
						// (source is MethodCall) ||
					case NodeType.Call :
					case NodeType.Calli :
					case NodeType.Callvirt :
					case NodeType.Jmp :
					case NodeType.MethodCall :
						// (source is ArrayConstruct) ||
					case NodeType.ConstructArray:
						// (source is AddressDereference) ||
					case NodeType.AddressDereference:
						// (source is MemberBinding) ||
					case NodeType.MemberBinding:
						// (source is Indexer)
					case NodeType.Indexer:
						assignment.Source = (Expression)this.Visit(source);
						break;
					case NodeType.Literal:
						break;

					// (source is Construct)
					case NodeType.Construct:
					default:
						assignment.Source = simplify(source, true);
						break;
				}
					return assignment;

				default:
					throw new ApplicationException("Strange CCI format " + CodePrinter.StatementToString(assignment));
			}
		}

    private static Literal AsConstant(Expression e) {
      Literal l = e as Literal;
      if (l != null) return l;

      switch(e.NodeType) {
        case NodeType.LogicalNot:
          UnaryExpression uexp = (UnaryExpression)e;
          Literal arg = AsConstant(uexp.Operand);
          if (arg != null) {
            if (Equals(arg.Value, false) || Equals(arg.Value, 0)) { return new Literal(true, e.Type, e.SourceContext); }
            if (Equals(arg.Value, true) || Equals(arg.Value, 1)) { return new Literal(false, e.Type, e.SourceContext); }
          }
          return null;
      }

      return null;
    }

		public override Statement  VisitBranch(Branch branch)
		{
      branch = (Branch)branch.Clone();

			// remember this statement on the list to adjust branches
			this.branchInstructions.Add(branch);

			if (branch.Condition == null) return branch;

      if (this.performConstantFoldingOnBranchConditions) {
        Literal l = AsConstant(branch.Condition);
        if (l != null) {
          if (Equals(l.Value, true) || Equals(l.Value, 1)) {
            branch.Condition = null;
            return branch;
          }
          if (Equals(l.Value, false) || Equals(l.Value, 0)) {
            return null;
          }
        }
      }
			branch.Condition = simplify(branch.Condition);
			return branch;
		}

		public override Expression VisitBinaryExpression(BinaryExpression binaryExpression)
		{
      binaryExpression = (BinaryExpression)binaryExpression.Clone();

			if(binaryExpression.NodeType == NodeType.Ldvirtftn)
			{
				binaryExpression.Operand1 = simplify(binaryExpression.Operand1, true);
				return binaryExpression;
			}
			binaryExpression.Operand1 = simplify(binaryExpression.Operand1, true);
			binaryExpression.Operand2 = simplify(binaryExpression.Operand2, true);
			return binaryExpression;
		}

		public override Expression VisitTernaryExpression(TernaryExpression ternaryExpression)
		{
      ternaryExpression = (TernaryExpression)ternaryExpression.Clone();

			ternaryExpression.Operand1 = simplify(ternaryExpression.Operand1);
			ternaryExpression.Operand2 = simplify(ternaryExpression.Operand2);
			ternaryExpression.Operand3 = simplify(ternaryExpression.Operand3);
			return ternaryExpression;
		}


    /// <summary>
    /// </summary>
    /// <param name="cons">Cloned</param>
    /// <returns></returns>
		public override Expression VisitConstruct(Construct cons)
		{
			ExpressionList operands = this.VisitExpressionList(cons.Operands);
			MemberBinding mb = this.VisitExpression(cons.Constructor) as MemberBinding;
      if (mb == null) return null;

			Debug.Assert(mb.TargetObject == null, "constructor target not null!");

			if ( this.expandAllocations ) 
			{
				// Now split the expression into 3:
				//  allocTemp = new T;
				//  allocTemp..ctor(args...);
				//  allocTemp

				// For value types, the construction is even more involved:
				//  valTemp = new VT;
				//  allocTemp = &valTemp;
				//  allocTemp..ctor(args...);
				//  valTemp

				if (mb.BoundMember != null && mb.BoundMember.DeclaringType != null && mb.BoundMember.DeclaringType.IsValueType) 
				{
					Variable valTemp = StackVariable.NEWValueTemp(mb.BoundMember.DeclaringType);

					Construct newcons = new Construct(new MemberBinding(null, mb.BoundMember), new ExpressionList(0), mb.BoundMember.DeclaringType);
					AssignmentStatement new_stat = new AssignmentStatement(valTemp, newcons);
					new_stat.SourceContext = this.current_source_context;
					new_stats.Add(new_stat);

          Variable allocTemp = StackVariable.NEWTemp(mb.BoundMember.DeclaringType);
          new_stats.Add(new AssignmentStatement(allocTemp, new UnaryExpression(valTemp, NodeType.AddressOf, mb.BoundMember.DeclaringType.GetReferenceType()), NodeType.Nop));
          mb.TargetObject = allocTemp;

					ExpressionStatement call_stat = new ExpressionStatement(new MethodCall(mb, operands, NodeType.Call, Cci.SystemTypes.Void));
					call_stat.SourceContext = this.current_source_context;
					new_stats.Add(call_stat);

					return valTemp;
				}
				else 
				{
					Variable vtemp = StackVariable.NEWTemp(mb.BoundMember.DeclaringType);

					Construct newcons = new Construct(new MemberBinding(null, mb.BoundMember), new ExpressionList(0), mb.BoundMember.DeclaringType);
					AssignmentStatement new_stat = new AssignmentStatement(vtemp, newcons);
					new_stat.SourceContext = this.current_source_context;
					new_stats.Add(new_stat);

					mb.TargetObject = vtemp;

					ExpressionStatement call_stat = new ExpressionStatement(new MethodCall(mb, operands, NodeType.Call, Cci.SystemTypes.Void));
					call_stat.SourceContext = this.current_source_context;
					new_stats.Add(call_stat);

					return vtemp;
				}
			}
			else 
			{
				Construct newcons = new Construct(mb, operands, mb.BoundMember.DeclaringType);
				newcons.SourceContext = this.current_source_context;

				return newcons;
			}
		}


    /// <summary>
    /// </summary>
    /// <param name="consArr">Cloned</param>
    /// <returns></returns>
		public override Expression VisitConstructArray(ConstructArray consArr)
		{
			if(consArr.Initializers != null)
				throw new ApplicationException("ConstructArray Initializers field non-null!");

      consArr = (ConstructArray)consArr.Clone();
			consArr.Operands = this.VisitExpressionList(consArr.Operands);
			return consArr;
		}

    /// <summary>
    /// </summary>
    /// <param name="expr_stat">Cloned</param>
    /// <returns></returns>
    public override Statement VisitExpressionStatement(ExpressionStatement expr_stat) {
      // The AST may have shared nodes. 
      if (this.orig2Copy.ContainsKey(expr_stat)) {
        return this.orig2Copy[expr_stat];
      }
      Expression newexpr = this.VisitExpression(expr_stat.Expression);
      if (newexpr == null) {
        // turn into nop statement
        return new Statement(NodeType.Nop);
      }
      // check for special case where we now have a PopExpression as the expression.
      // this corresponds to a push(pop), which is a noop and we need to represent it that way.
      if (newexpr.NodeType == NodeType.Pop && !(newexpr is UnaryExpression)) {
        return new Statement(NodeType.Nop);
      }
      ExpressionStatement copy = (ExpressionStatement)expr_stat.Clone();
      copy.Expression = newexpr;
      copy.SourceContext = this.current_source_context;
      this.orig2Copy.Add(expr_stat, copy);

      return copy;
    }

    /// <summary>
    /// </summary>
    /// <param name="expressions">Cloned</param>
    /// <returns></returns>
		public override ExpressionList VisitExpressionList(ExpressionList expressions)
		{
			if (expressions == null) return null;

      ExpressionList newexpressions = new ExpressionList(expressions.Count);

			for(int i = 0, n = expressions.Count; i < n; i++)
				newexpressions.Add(simplify(expressions[i]));
			return newexpressions;
		}

    /// <summary>
    /// </summary>
    /// <param name="indexer">Cloned</param>
    /// <returns></returns>
		public override Expression VisitIndexer(Indexer indexer)
		{
      // copy indexer, since CCI shares them in post increments
      indexer = (Indexer)indexer.Clone();
      indexer.Operands = (ExpressionList)indexer.Operands.Clone();

			indexer.Object   = simplify(indexer.Object);
			ExpressionList ops = this.VisitExpressionList(indexer.Operands);
			System.Diagnostics.Debug.Assert(ops != null, "VisitExpressionList must return non-null if arg is non-null");
			indexer.Operands = ops;
			return indexer;
		}

    /// <summary>
    /// </summary>
    /// <param name="memberBinding">Cloned</param>
    /// <returns></returns>
		public override Expression VisitMemberBinding(MemberBinding memberBinding)
		{
      // dup the Memberbinding, since CCI shares them in pre/postfix expressions
      memberBinding = (MemberBinding)memberBinding.Clone();

      if (memberBinding.TargetObject == null) return memberBinding;

      memberBinding.TargetObject = simplify(memberBinding.TargetObject, true);
			return memberBinding;
		}

    /// <summary>
    /// 
    /// </summary>
    /// <param name="call">Cloned</param>
    /// <returns></returns>
		public override Expression VisitMethodCall(MethodCall call)
		{
      call = (MethodCall)call.Clone();
			if(call.Callee is MemberBinding)
				call.Callee = this.VisitMemberBinding((MemberBinding) call.Callee);
			else
				call.Callee   = simplify(call.Callee);

			call.Operands = this.VisitExpressionList(call.Operands);

			return call;
		}

    /// <summary>
    /// 
    /// </summary>
    /// <param name="Return">Cloned</param>
    /// <returns></returns>
		public override Statement  VisitReturn(Return Return)
		{
			if (Return.Expression == null) return Return;

      Return = (Return)Return.Clone();
			Return.Expression = simplify(Return.Expression);
			return Return;
		}

		public override StatementList VisitStatementList(StatementList stats)
		{
			Debug.Assert(false, "should never get here. If we do, there are nested blocks or statement lists that are below a non-block statement");	
			return stats;
		}
			
    /// <summary>
    /// 
    /// </summary>
    /// <param name="switchInstruction">Cloned</param>
    /// <returns></returns>
		public override Statement  VisitSwitchInstruction(SwitchInstruction switchInstruction)
		{
      switchInstruction = (SwitchInstruction)switchInstruction.Clone();
			switchInstruction.Expression = simplify(switchInstruction.Expression);
			this.switchInstructions.Add(switchInstruction);
			return switchInstruction;
		}

    public override Statement VisitSwitchCaseBottom(Statement switchCaseBottom) {
      return switchCaseBottom;
    }

    /// <param name="Throw">Cloned</param>
		public override Statement  VisitThrow(Throw Throw)
		{
      Throw = (Throw)Throw.Clone();
      if (Throw.Expression != null) 
			{
				Throw.Expression = simplify(Throw.Expression);
			}
			return Throw;
		}

    /// <summary>
    /// </summary>
    /// <param name="expression">Cloned</param>
    public override Expression VisitUnaryExpression(UnaryExpression expression)
    {
      expression = (UnaryExpression)expression.Clone();
      switch(expression.NodeType)
      {
        case NodeType.Ldtoken:
          return expression;
        case NodeType.Ldftn:
          expression.Operand = (Expression)this.Visit(expression.Operand);
          return expression;
        case NodeType.AddressOf:
        case NodeType.ReadOnlyAddressOf:
        case NodeType.OutAddress: // alias of AddressOf
        case NodeType.RefAddress: // alias of AddressOf
          expression.Operand = simplify_addressof_operand(expression.Operand);
          return expression;
      }
      expression.Operand = simplify(expression.Operand);
      return expression;
    }

      public override Statement VisitAssertion(Assertion assertion) {
        // When this statement appears in normalized code, it is for consumption by Boogie only, not for static analysis algorithms.
        // Static analysis algorithms get the information in this statement from the normalized version of it that always follows it.
        // Therefore, simply pass this statement on unchanged.
        return assertion;
      }

	}




	public class StackVariableEnumerable : System.Collections.IEnumerable
	{
		public StackVariableEnumerable(int depth)
		{
			this.depth = depth;
		}
		private int depth;

		public System.Collections.IEnumerator GetEnumerator()
		{
			return new Enumerator(this.depth);
		}

		private class Enumerator : System.Collections.IEnumerator
		{
			public Enumerator(int depth)
			{
				this.depth = depth;
				this.index = -1;
			}

			readonly private int depth;
			private int index;

			private Variable currentVar;

			#region IEnumerator Members

			public void Reset()
			{
				this.index = -1;
			}

			public object Current
			{
				get
				{
					return this.currentVar;
				}
			}

			public bool MoveNext()
			{
				this.index++;

				if (this.index < depth) 
				{
					this.currentVar = StackVariable.For(this.index);
					return true;
				}
				return false;
			}

			#endregion
		}
	}



	/// <summary>
	/// Class for the stack variables introduced by the stack removal transformation.
	/// </summary>
	public class StackVariable : Local, IUniqueKey
	{
		/// <summary>
		/// Create a stack variable for a given stack depth and a specific type.
		/// </summary>
		/// <param name="depth"></param>
		/// <param name="type"></param>
		private StackVariable(int depth, TypeNode type) : base(new Identifier("stack" + depth), type)
		{
			this.depth = depth;
		}


		public readonly int depth;
		public override string ToString() 
		{
			return Name.ToString();
		}

    public static Variable For(int depth, Expression node, ref SourceContext ctxt) {
      TypeNode type = node.Type;
      Variable sv = For(depth, type);
      if (type != null) {
        // Debug.Assert(ctxt.Document != null);
        sv.SourceContext = ctxt;
      }
      return sv;
    }

    public static Variable For(int depth, Expression node) {
      TypeNode type = node.Type;
      Variable sv = For(depth, type);
      if (type != null) {
        // Debug.Assert(node.SourceContext.Document != null);
        sv.SourceContext = node.SourceContext;
      }
      return sv;
    }

    // exitential delay
    // The following function relaxes a non-null type to be normal type.
    public static TypeNode relaxNonNull(TypeNode type)
    {
      if (type == null) return null;
      return TypeNode.StripModifier(type, SystemTypes.NonNullType);
    }
    // end exitential delay

		public static Variable For(int depth, TypeNode type) 
		{
      if (depth == 0 && type == null) {
        return Stack0;
      }
			return new StackVariable(depth, relaxNonNull(type));
		}

		public static Variable For(int depth) 
		{
			return For(depth, (TypeNode)null);
		}


		public override bool Equals(object obj)
		{
			StackVariable other = obj as StackVariable;
			if (other == null) return false;
			return (other.depth == this.depth);
		}

		public override int GetHashCode()
		{
			return this.depth;
		}


        public static Variable Stack0
        {
          get
          {
            return new StackVariable(0, null);
          }
        }
		//public static readonly Variable Stack0 = new StackVariable(0, null);

		public const int NEWTEMPDEPTH = 50000;

		/// <summary>
		/// Bogus stack variable useful for desugaring of NEWwithCONSTRUCT.
		/// We hope we'll never go beyond 50000 stack vars.  Actually, it would be great if we could analyze
		/// method with >50000 stack variables !
		/// </summary>
		public static Variable NEWTemp(TypeNode typ) 
		{
			return new StackVariable(NEWTEMPDEPTH, typ);
		}


		/// <summary>
		/// Bogus stack variable useful for desugaring of NEWwithCONSTRUCT for Value types.
		/// This variable represents the contents of the newly created value object, which does not
		/// get allocated in the heap.
		/// </summary>
		public static Variable NEWValueTemp(TypeNode typ) 
		{
			return new StackVariable(NEWTEMPDEPTH+1, typ);
		}

		public static bool operator == (StackVariable v1, StackVariable v2) 
		{
			int d1 = (Object.ReferenceEquals(v1, null))?-1:v1.depth;
			int d2 = (Object.ReferenceEquals(v2, null))?-1:v2.depth;
			return (d1 == d2);
		}

		public static bool operator != (StackVariable v1, StackVariable v2)
		{
			return !(v1 == v2);
    }

    #region IUniqueKey Members

    public int UniqueId {
      get {
        return -this.depth;
      }
    }

    #endregion
  }



	/// <summary>
	/// To test the code flattener, this visitor takes a method and expands the body expressions to 
	/// add a nested BlockExpression around every sub expression it encounters.
	/// 
	/// It also adds a nested block for the first statement in each block.
	/// 
	/// The idea is that we can then undo this expansion with the CodeFlattener.
	/// </summary>
	public class FlattenerTest : StandardVisitor 
	{

		public override Statement VisitExpressionStatement(ExpressionStatement statement)
		{
			// skip VisitExpression because these could be of a form that do not produce a value, in which case we don't want to introduce a BlockExpression
			statement.Expression = (Expression)this.Visit(statement.Expression);

			return statement;
		}

		public override Expression VisitExpression(Expression expression)
		{
			if (expression == null) return null;

			Expression expr = base.VisitExpression (expression);

			if (expr.NodeType == NodeType.Pop ||
				  expr.NodeType == NodeType.Dup || 
					expr.NodeType == NodeType.Call || 
				  expr.NodeType == NodeType.Callvirt ||
					expr.NodeType == NodeType.Calli) return expr;

			// avoid inserting block expression on the left side of an assignment
			if (expr is Variable || expr is MemberBinding || expr is AddressDereference || expr is Indexer || expr is Literal) return expr;

			// we cannot create a block expression if the expression doesn't actually produce anything 
			// e.g. Call to a void function.
			return new BlockExpression(new Block(new StatementList(new ExpressionStatement(expr, expression.SourceContext))), expression.Type, expression.SourceContext);
		}

		public override AttributeNode VisitAttributeNode(AttributeNode attribute)
		{
			return attribute;
		}


		public override Block VisitBlock(Block block)
		{
			Block bl = base.VisitBlock (block);

			// insert a nested block for the first statement
			StatementList sl = bl.Statements;
			if (sl != null && sl.Count > 0) 
			{
				sl[0] = new Block(new StatementList(sl[0]));
			}
			return bl;
		}


	}
}
