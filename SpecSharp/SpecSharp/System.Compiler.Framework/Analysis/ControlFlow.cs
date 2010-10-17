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
  using System.IO;
  using System.Collections;


  /// <summary>
  /// A refinement of blocks that the code flattener produces (single entry, single exit at end)
  /// 
  /// The block also serves to cache various information once we build the CFG.
  /// </summary>
  public class CfgBlock : Cci.Block {

    public CfgBlock(StatementList sl) : base(sl) {
    }

    private ControlFlowGraph cfg;
    private int index; // set during Cfg building
    internal int priority; // in pre order, higher means earlier.
    internal int stackDepth; // set in StackRemovalTransform

    /// <summary>
    /// Returns an index of 0..n of this block within the CFG it is part of.
    /// 
    /// Allows using arrays as tables indexed by blocks.
    /// </summary>
    public int Index { get { return this.index; } }

    internal void AssignIndex(ControlFlowGraph cfg, int index) {
      this.cfg = cfg;
      this.index = index;
    }

    /// <summary>
    /// Returns a list of CfgBlock that are handlers of the current block, handling an exception
    /// of the given type, or a subtype thereof.
    /// </summary>
    /// <param name="exception">Type of exception thrown. It is assumed that any actual subtype could be thrown</param>
    /// <returns>All handlers that could apply directly to this exception.
    ///  In addition, if the method might not handle it, then the ExceptionExit block is
    /// part of this list.</returns>
    public System.Collections.IEnumerable/*CfgBlock*/ HandlersMatching(TypeNode exception) {
      ArrayList handlers = new ArrayList();

      CfgBlock currentBlock = this;

      while (currentBlock != null) {
        CfgBlock handler = this.cfg.ExceptionHandler(currentBlock);

        if (handler == null || handler.Statements == null || handler.Statements.Count < 1) break;

        Catch stat = handler.Statements[0] as Catch;

        if (stat != null) {
          if (exception.IsAssignableTo(stat.Type)) {
            // handles exceptions completely
            handlers.Add(handler);
            break;
          }
          if (stat.Type.IsAssignableTo(exception)) {
            // handles part of it
            handlers.Add(handler);
          }
          currentBlock = handler;
        }
        else {
          // must be the Unwind block
          handlers.Add(handler);
          break;
        }
      }
      return handlers;
    }

    public CfgBlock ExceptionHandler {
      get {
        return this.cfg.ExceptionHandler(this);
      }
    }

    public CfgBlock ContainingHandler {
      get {
        return this.cfg.HandlerContainingBlock(this);
      }
    }


    public ContinuationKind Continuation {
      get {
        return this.cfg.GetContinuation(this).Kind;
      }
    }

		
    public System.Collections.IEnumerable NormalSuccessors {
      get {
        Cci.CfgBlock[] blocks = this.cfg.NormalSucc(this);
        return blocks;
      }
    }

		
    public System.Collections.IEnumerable NormalPredecessors {
      get {
        Cci.CfgBlock[] blocks = this.cfg.NormalPred(this);
        return blocks;
      }
    }

		
    public CfgBlock TrueContinuation {
      get {
        ControlFlowGraph.IfContinuation cont = this.cfg.GetContinuation(this) as ControlFlowGraph.IfContinuation;
        if (cont == null) return null;
        return cont.True;
      }
    }

		
    public CfgBlock FalseContinuation {
      get {
        ControlFlowGraph.IfContinuation cont = this.cfg.GetContinuation(this) as ControlFlowGraph.IfContinuation;
        if (cont == null) return null;
        return cont.False;
      }
    }

		
    public IIndexable/*<CfgBlock>*/ SwitchTargets {
      get {
        ControlFlowGraph.SwitchContinuation cont = this.cfg.GetContinuation(this) as ControlFlowGraph.SwitchContinuation;
        if (cont == null) return null;
        return new ArrayIndexable(cont.Targets);
      }
    }

		
    public CfgBlock DefaultBranch {
      get {
        ControlFlowGraph.SwitchContinuation cont = this.cfg.GetContinuation(this) as ControlFlowGraph.SwitchContinuation;
        if (cont == null) return null;
        return cont.Default;
      }
    }
		

    public CfgBlock UniqueSuccessor {
      get {
        ControlFlowGraph.StraightContinuation cont = this.cfg.GetContinuation(this) as ControlFlowGraph.StraightContinuation;

        if(cont==null)
          return null;
        return cont.Next;
      }
    }


    public int UniqueId { get { return this.UniqueKey; } }

    /// <summary>
    /// Returns the stack depth at the beginning of the block
    /// </summary>
    public int StackDepth { 
      get {
        return this.stackDepth;
      }
    }
    /// <summary>
    /// Returns best effort node with source context for the beginning of the block
    /// </summary>
    public Cci.Node BeginSourceContext() {
      CfgBlock current = this;
      while (current != null) {
        if (current.SourceContext.Document != null) return current;
        StatementList sl = current.Statements;
        for (int i=0; i < (sl!=null?sl.Count:0); i++) {
          if (sl[i].SourceContext.Document != null) return sl[i];
        }
        current = current.UniqueSuccessor;
      }
      // sorry
      Block result = new Block();
      result.SourceContext = this.cfg.Method.SourceContext;
      result.SourceContext.StartPos = result.SourceContext.EndPos-1;
      return result;
    }

    /// <summary>
    /// Returns best effort node with source context for the beginning of the block
    /// </summary>
    public Cci.Node EndSourceContext() {
      CfgBlock current = this;
      StatementList sl = current.Statements;
      int length = sl!=null?sl.Count:0;
      for (int i=length-1; i >= 0; i--) {
        if (sl[i].SourceContext.Document != null) return sl[i];
      }
      return current;
    }

    #region IIndexable Members

    /// <summary>
    /// Return statement in block.
    /// </summary>
    public Statement this[int index] {
      get {
        return this.Statements[index];
      }
    }

    /// <summary>
    /// Returns number of statements in block
    /// </summary>
    public int Length {
      get {
        return this.Statements.Count;
      }
    }
		
    #endregion

    [Obsolete("A CfgBlock is already a Block. Cast no longer needed", true)]
    public static Block Cast(CfgBlock b) {
      return b;
    }
  }

	/// <summary>
	/// <c>ICFGFactory</c> with caching.
	/// </summary>
	public class CfgCachingFactory //: ICfgFactory
	{
		private readonly Hashtable/*<Method,CFG>*/ method2cfg = new Hashtable();

		/// <summary>
		/// Get the CFG for a method.  Results are cached, so getting the CFG for the same
		/// method twice will return the same CFG object.
		/// </summary>
		/// <param name="method">Method whose CFG we want to get.</param>
		/// <returns>CFG for <c>method</c>; cached.</returns>
		public virtual ControlFlowGraph ComputeControlFlowGraph (Method method)
		{
			ControlFlowGraph cfg = (ControlFlowGraph) method2cfg[method];
			if (cfg == null)
			{
				cfg = new ControlFlowGraph(method);
				method2cfg.Add(method, cfg);
			}
			return cfg;
		}

		/// <summary>
		/// Flushes the CFG for <c>method</c> from the internal cache.
		/// </summary>
		/// <param name="method">Method whose CFG we want to flush from the cache.</param>
		public virtual void Flush(Method method)
		{
			method2cfg.Remove(method);
		}
	}




	// useful for debugging
	class ReadonlyHashtable: Hashtable
	{
		private readonly bool duringConstruction = true;

		public ReadonlyHashtable (Hashtable t): base(t) { this.duringConstruction = false; }

		public override void Add (object key, object value)
		{
			if (this.duringConstruction) { base.Add(key, value); return; }
			Debug.Assert(false, "cannot add element to readonly hashtable");
		}

		public override void Clear ()
		{
			Debug.Assert(false, "cannot clear readonly hashtable");
		}

		public override void Remove (object key)
		{
			Debug.Assert(false, "cannot remove element from readonly hashtable");
		}

		public override object this[object key]
		{
			get
			{
				return base[key];
			}
			set
			{
				Debug.Assert(false, "cannot set element in readonly hashtable");
			}
		}

	}


	
	
	/// <summary>
	/// 	/// Control Flow Graph (CFG) for a method.  The CFG is an extra layer on top of
	/// the CCI representation; all the CFG related information (flow edges) is maintained into
	/// the CFG object.  Both the normal and the exceptional flows are modeled by the CFG.
	/// 
	/// <p>THE UNDERLYING CCI REPRESENTATION IS MUTATED *A LOT* BY THE "FINALLY" BLOCK DUPLICATION
	/// AND THE STACK REMOVAL TRANSFORMATION.  YOU SHOULD MANUALLY CLONE IT BEFORE CONSTRUCTING
	/// THE CFG IF YOU NEED IT; E.G, IF YOU NEED TO WRITE THE CODE BACK TO DISK.  CFG IS USED FOR
	/// PROGRAM ANALYSIS ONLY, NOT FOR CODE GENERATION.</p>
	/// 
	/// <p>A Control Flow Graph is basically an oriented graph whose nodes are the
	/// basic blocks from the method body; the edges reflect the normal and the exceptional flow
	/// of control (the exceptional flow is the flow that occurs when an exception is raised).
	/// In addition to the basic blocks of the original method, three more blocks are added:</p>
	/// 
	/// <ul>
	/// <li>a special <c>CFG.NormalExitBlock</c> that is a successor for all the basic block
	/// terminated in a return instruction; it is a merge point for all the
	/// paths on the normal (intra-procedural) control flow.</li>
	/// <li>a special <c>CFG.ExcpExitBlock</c> that is the default handler for all the uncaught
	/// exceptions; it is a merge point for all the paths on the intra-procedural execution
	/// paths that may terminate with an uncaught exception.</li>
	/// <li>a special <c>CFG.ExitBlock</c> that is the only successor of the aforementioned
	/// normal and the exception exit.  Its only normal flow predecessor is the special block
	/// for the normal exit and its only exceptional flow predecessor is the special block
	/// for the exceptional exit. </li>
	/// </ul>
	/// 
	/// <p>
	/// If you are given a block and want to know if it's the [normal/excp] exit of its
	/// method, all you have to do is use the appropriate "is" test
	/// (e.g. "block is CFG.NormalExitBlock"). If you have the CFG, and want to know its
	/// [normal/excp] exit, you just have to query the appropriate method.</p>
	/// 
	/// <p>
	/// NOTE:
	/// If an analysis is interested in the result for the normal flow, then it can retrieve
	/// the result of the dataflow equations for the normal exit point. Similarly, if an analysis
	/// is interested in the result for the exceptional flow, then it can retrieve the result
	/// of the dataflow equations for the exceptional exit point.  Finally, if the distinction between
	/// normal/exceptional flow is not important, the "unified" exit point can be used instead.</p>
	/// 
	/// </summary>
  sealed public class ControlFlowGraph : IGraphNavigator {
    /*
    TODO: compute statement -> block map.
    */

    // turns on debug support for finally block duplication
    private static bool FINALLY_CLONE_DEBUG = false;

    private const string FINALLYVARPREFIX = "SS$finallyv";

    private CfgBlock[] all_blocks;          // all blocks from this CFG
    private EntryBlock           entry_point; // method entry point
    private ExitBlock       exit_point;  // method exit point
    private NormalExitBlock normal_exit_point;
    private ExcpExitBlock   excp_exit_point;

    private Hashtable/*<Block,Block>*/ b2next;
    private Hashtable/*<Block,Block[]>*/ b2n_succ;
    private Hashtable/*<Block,Block[]>*/ b2n_pred;
    private Hashtable/*<Block,Block>*/ b2exception_handler;
    private Hashtable/*<Block,FList<ExceptionHandler>>*/ b2_enclosing_finally;
    private Hashtable/*<Block,Block[]>*/ b2e_pred;
    private Hashtable/*<Block,Continuation>*/ b2_cont; // continuations
    private Hashtable/*<Block,Block>*/ e2_next; // for each handler, next enclosing handler.
    private IList/*<SCC<Block>>*/ sorted_sccs = null;

    private Hashtable/*<Statement,Block>*/ stmt2block; // maps each statement to the containing block

    // map block -> exception handler whose body starts with that block (if any)
    private Hashtable/*<Block,ExceptionHandler>*/ handlerThatStartsAtBlock;

    // maps blocks to the handler that contains them directly (null for blocks not part of a handler)
    //
    private Hashtable/*<Block,ExceptionHandler>*/ b2_containing_handler;


    private Method method;
    public Method Method { get { return this.method; } }
    private Method originalMethod;
    public Method OriginalMethod { get { return this.originalMethod; } }
		




    public Node GenericsUse = null;
    public Node PointerUse = null;

    public bool UsesGenerics {
      get { return this.GenericsUse != null; }
    }

    public bool UsesPointers {
      get { return this.PointerUse != null; }
    }

    private bool hasMissingInfo = false;
    public bool HasMissingInfo {
      get { return this.hasMissingInfo; }
      set { this.hasMissingInfo = value; }
    }




    /// <summary>
    /// Exception thrown when trying to construct the CFG
    /// of a method whose code is unavailable.
    /// </summary>
    public class UnavailableCodeException: Exception {
      public UnavailableCodeException(string str) : base(str) {}
      public UnavailableCodeException(Method method) : 
        this(CodePrinter.MethodSignature(method)) {}
    }


    /// <summary>
    /// Exception thrown when trying to construct the CFG
    /// of a method whose code contains some unsupported features,
    /// e.g. Filter exception handlers.
    /// </summary>
    public class UnsupportedCodeException: Exception {
      public UnsupportedCodeException(string str) : base(str) {}
      public UnsupportedCodeException(Method method, string str) : 
        this(CodePrinter.MethodSignature(method) + " : " + str) {}
    }

    /// <summary>
    /// Exception thrown when trying to construct the CFG
    /// of a method without code (e.g., abstract methods).
    /// </summary>
    public class NoCodeException: Exception {
      public NoCodeException(string str) : base(str) {}
      public NoCodeException(Method method) : 
        this(CodePrinter.MethodSignature(method)) {}
    }

    private CodeMap codeMap;
    public CodeMap CodeMap { get { return this.codeMap; } }

    /// <summary>
    /// Constructs the Control Flow Graph for <c>method</c>.
    /// If the code for <c>method</c> is unavailable, throw an <c>UnavailableCodeException</c>
    /// (a constructor cannot return an error code).  If the code for <c>method</c> is empty
    /// (abstract method), thrown a <c>NoCodeException</c>.  Otherwise, examine the CciHelper
    /// representation of the method code and construct the CFG.
    /// </summary>
    /// <param name="method">Method whose CFG will be constructed.</param>
    /// <param name="duplicateFinallyBlocks">If <c>true</c>, the finally blocks will be duplicated and you'll obtain a real
    /// CFG.  Otherwise, you'll have to manually deal with the finally blocks that are traversed by each
    /// "leave" instruction.  HIGHLY RECOMMENDED!</param>
    /// <param name="eliminateEvaluationStack">If <c>true</c>, <c>StackRemovalTransf.Process</c> will be called
    /// to remove the stack manipulating operations. See more commends in the class
    /// <c>StackRemovalTransf</c>. RECOMMENDED!</param>
    public ControlFlowGraph(Method method, bool duplicateFinallyBlocks, bool eliminateEvaluationStack, bool expandAllocations) 
      : this(method, duplicateFinallyBlocks, eliminateEvaluationStack, expandAllocations, true)
    {
    }
    /// <summary>
    /// Constructs the Control Flow Graph for <c>method</c>.
    /// If the code for <c>method</c> is unavailable, throw an <c>UnavailableCodeException</c>
    /// (a constructor cannot return an error code).  If the code for <c>method</c> is empty
    /// (abstract method), thrown a <c>NoCodeException</c>.  Otherwise, examine the CciHelper
    /// representation of the method code and construct the CFG.
    /// </summary>
    /// <param name="method">Method whose CFG will be constructed.</param>
    /// <param name="duplicateFinallyBlocks">If <c>true</c>, the finally blocks will be duplicated and you'll obtain a real
    /// CFG.  Otherwise, you'll have to manually deal with the finally blocks that are traversed by each
    /// "leave" instruction.  HIGHLY RECOMMENDED!</param>
    /// <param name="eliminateEvaluationStack">If <c>true</c>, <c>StackRemovalTransf.Process</c> will be called
    /// to remove the stack manipulating operations. See more commends in the class
    /// <c>StackRemovalTransf</c>. RECOMMENDED!</param>
    /// <param name="constantFoldBranches">When <c>true</c>, use constant folding 
    /// to prune infeasible branches.</param>
    public ControlFlowGraph (Method method, bool duplicateFinallyBlocks, bool eliminateEvaluationStack, bool expandAllocations, bool constantFoldBranches) {
      StatementList blocks = method.Body.Statements;
      if (blocks == null)
        throw new UnavailableCodeException(method);
      if (blocks.Count == 0)
        throw new NoCodeException(method);

      this.originalMethod = method; // Hold onto the original because MakeFlat modifies the method
      // COMMENT NEXT LINE IF YOU HAVE PROBLEMS!
      method = CodeFlattener.MakeFlat(method, expandAllocations, constantFoldBranches, out codeMap);
      this.method = method; // useful for debugging

      // reload blocks, since MakeFlat changed this.
      blocks = method.Body.Statements;


      //Console.WriteLine("Before removing last block...\n");
      //CodePrinter.PrintMethod(Console.Out, method);

      /*
      Block lastblock = (Block)blocks[blocks.Length-1];
      if (lastblock.Statements.Length==0) 
      {
        // dummy block, remove it.
        StatementList oldblocks = blocks;

        blocks = new StatementList(oldblocks.Length-1);
        for (int i=0;i<oldblocks.Length-1; i++) 
        {
          blocks.Add(oldblocks[i]);
        }
        method.Body.Statements = blocks;
      }
      */

      // Debug code with code printer.
      //Console.WriteLine("Before code flattening...\n");
      //CodePrinter.PrintMethod(Console.Out, method);

      CreateEntryAndExitBlocks(method, blocks);

      // 0. build map block2next that gives the fall-through successor of each block
      b2next = new Hashtable();
      for(int i = 0; i < blocks.Count-1; i++) {
        b2next[blocks[i]] = blocks[i+1];
      }
      // add fall through from special entry to real entry block
      b2next[this.Entry] = blocks[0];

      NormalFlowBlockNavigator nfbnav = new NormalFlowBlockNavigator(this, this.b2next);

      HashSet/*<Block>*/ filterblocks = filter_blocks(method,nfbnav);

      IList/*<ExceptionHandler>*/ all_ehs;
      // 1. for each block, find the array of relevant handlers
      // (order consistent with the order of the Exception Handlers)
      bool has_finally = BuildExceptionalFlow(method, blocks, out all_ehs, filterblocks); // SIDE EFFECT: build b2e_succ map

      IList new_blocks = new ArrayList();
      if (has_finally && duplicateFinallyBlocks) {
        // 2. eliminate finallies
        if (FINALLY_CLONE_DEBUG) {
          this.b2cloning_leave = new Hashtable();
        }

        FinallyBlockDuplicator fdc = new FinallyBlockDuplicator(this, method, new_blocks, this.b2next, nfbnav, all_ehs);

        if (FINALLY_CLONE_DEBUG) {
          this.copy2orig = fdc.copy2orig;
        }
      }

      // 3. add catch statements to all handlers
      AddCatchStatements(all_ehs, new_blocks);

      // 4. build the collection all_blocks;
      BuildAllBlockCollection(method, new_blocks, filterblocks, nfbnav);

      // 4a. fixup back jumps in handlers going to the handler head. They must go to the
      //     immediately succeeding block to skip the new "catch" instruction.
      FixupJumpsToHandlerHead();

      // 5. build the map block b -> blocks protected by the handler that starts at b;
      BuildHandlerPredecessorMap();

      // 6. build the normal flow
      BuildNormalFlow(all_blocks, nfbnav, (CfgBlock) blocks[0]);

      // 7. build continuation map
      BuildContinuationMap(all_blocks);

#if DEBUGxxx
      Console.WriteLine();
      Console.WriteLine("=================================================================");
      Console.WriteLine();
      this.Display(Console.Out);
#endif

      if (eliminateEvaluationStack) {
        // sets the stack depth of each block as a side effect.
        StackRemovalTransformation.Process(this);
      }

#if DEBUG_EXPENSIVE
			this.TestInvariants();
			this.Seal();
#endif
    }

    /// <summary>
    /// Convenient CFG constructor: by default, the finally blocks are duplicated to obtain
    /// a real CFG, the stack removal transformation is applied, and constant-folding for
    /// branches is done.
    /// </summary>
    /// <param name="method">Method whose CFG is produced.</param>
    public ControlFlowGraph(Method method) : this(method, true, true, true, true) {}

    /// <summary>
    /// Convenient CFG constructor: by default, the finally blocks are duplicated to obtain
    /// a real CFG and the stack removal transformation is applied.
    /// </summary>
    /// <param name="method">Method whose CFG is produced.</param>
    /// <param name="constantFoldBranches">When true, prune infeasible paths
    /// due to branch conditions that are constants.</param>
    public ControlFlowGraph(Method method, bool constantFoldBranches)
      : this(method, true, true, true, constantFoldBranches) { }


    public static ControlFlowGraph For(Method method) {
      return For(method, true);
    }
    public static ControlFlowGraph For(Method method, bool ConstantFoldBranches) {
      if (method.Body == null) return null;
      if (method.Body.Statements == null || method.Body.Statements.Count == 0) return null;
      return new ControlFlowGraph(method, ConstantFoldBranches);
    }

    // make all the hashtables readonly to help catch bugs
    private void Seal () {
      this.b2next								= new ReadonlyHashtable(this.b2next);
      this.b2n_succ							= new ReadonlyHashtable(this.b2n_succ);
      this.b2n_pred							= new ReadonlyHashtable(this.b2n_pred);
      this.b2exception_handler	= new ReadonlyHashtable(this.b2exception_handler);
      this.b2_enclosing_finally	= new ReadonlyHashtable(this.b2_enclosing_finally);
      this.b2e_pred							= new ReadonlyHashtable(this.b2e_pred);
      this.b2_cont							= new ReadonlyHashtable(this.b2_cont);
      this.e2_next							= new ReadonlyHashtable(this.e2_next);
    }


    private void TestInvariants () {
      foreach (CfgBlock b in this.Blocks()) {
        if (!(b is ISpecialBlock)) {
          Debug.Assert(this.ExceptionHandler(b) != null, String.Format("block {0} has no exception handler", b.UniqueKey));
        }
      }
    }


    // This printer is safe to call early during CFG construction.
    //
    private static void SimpleDisplay (TextWriter tw, Method method) {
      StatementList blocks = method.Body.Statements;

      Hashtable b2id = new Hashtable();
      for (int i = 0; i < blocks.Count; i++)
        b2id[blocks[i]] = i;

      for (int b=0; b<blocks.Count; b++) {
        SimpleDisplay(tw, (Block)blocks[b], b2id);
      }
    }

    private static void SimpleDisplay (TextWriter tw, Block block, Hashtable b2id) {
      tw.WriteLine("BLOCK " + (block is ISpecialBlock ? "*" : "") + CodePrinter.b2s(block, b2id));
      CodePrinter.PrintBlock(tw, block, null, b2id);
      tw.WriteLine();
    }

    private static void SimpleDisplay (TextWriter tw, Block block) {
      SimpleDisplay(tw, block, null);
    }

    private HashSet/*<Block>*/ filter_blocks (Method method, NormalFlowBlockNavigator nfbnav) {
      HashSet/*<Block>*/ blocksToKill = new HashSet();
      /* We no longer remove the filter blocks.
      ExceptionHandlerList ehs = method.ExceptionHandlers;
      for (int i=0; i<ehs.Length; i++)
        if (ehs[i].HandlerType == NodeType.Filter)
          add_block_plus_succ_to_set(blocksToKill, ehs[i].FilterExpression, nfbnav);
      */
      return blocksToKill;
    }


    // Create the special blocks attached to the method entry / exit.
    private void CreateEntryAndExitBlocks(Method method, StatementList blocks) {
      entry_point       = new EntryBlock(new MethodHeader(method));
      exit_point        = new ExitBlock();
      normal_exit_point = new NormalExitBlock();
      excp_exit_point   = new ExcpExitBlock(method);
    }


    public bool HasNoReturn {
      get {
        ISet reachableFromEntry = GraphUtil.ReachableNodes(DataStructUtil.NodeSetFactory, new object[1]{this.Entry}, this);
        return ! reachableFromEntry.Contains(this.NormalExit);
      }
    }



    // Code for duplicating the finally blocks and obtaining a real CFG.
    private class FinallyBlockDuplicator {
			
      private readonly ControlFlowGraph cfg;
      private readonly Method method;
      private readonly IList/*<Block>*/ new_blocks;
      private readonly Hashtable/*<Block,Block>*/ block2next;
      private readonly IGraphNavigator bnav;
      private readonly IList/*<ExceptionHandler>*/ allExceptionHandlers;
      // map duplicate block -> copy source block; copy2orig.Keys = collection of all new blocks
      // null if no duplicate block (i.e., method with no finally handler)
      internal readonly Hashtable/*<Block,Block>*/ copy2orig = new Hashtable();
      // map ExceptionHandler eh -> last block from eh's body
      private readonly Hashtable/*<ExceptionHandler,Block>*/ lastHandledBlock; // only for finally handlers
      // map block -> block index in the method-wide list of blocks
      private readonly Hashtable/*<Block,int>*/ b2index;


      public FinallyBlockDuplicator (
        ControlFlowGraph cfg, 
        Method method, 
        IList/*<Block>*/ new_blocks,
        Hashtable/*<Block,Block>*/ block2next, 
        NormalFlowBlockNavigator nfbnav,
        IList/*<ExceptionHandler>*/ all_ehs
        ) {
        this.dupVisitor = new DupVisitor(method.DeclaringType.DeclaringModule, method.DeclaringType);
        this.cfg        = cfg;
        this.method     = method;
        this.new_blocks = new_blocks;
        this.block2next = block2next;
        this.bnav       = new UnionGraphNavigator(nfbnav, new ControlFlowGraph.ExcpFlowBlockNavigator(cfg));
        this.allExceptionHandlers    = all_ehs;

        // init the block -> index map
        this.b2index = new Hashtable();
        StatementList blocks = method.Body.Statements;
        for(int i = 0; i < blocks.Count; i++) {
          b2index[(Block) blocks[i]] = i;
        }

        // init the exception handler -> last block map
        this.lastHandledBlock = new Hashtable();
        foreach (ExceptionHandler eh in this.allExceptionHandlers) {
          if (eh.HandlerType != NodeType.Finally && eh.HandlerType != NodeType.FaultHandler) { continue; } 
          this.lastHandledBlock[eh] = LastBlockInsideHandler(eh);
        }

        // 2. deal with the "leave" instructions
        TreatLeaveInstructions ();

        // 3. The original finally / fault handlers should be turned into catch handlers
        ConvertFinallyHandlerIntoCatchHandler();
      }




      public void TreatLeaveInstructions () {
        // The order the leave instructions are treated is important: if block B1 is
        // protected by a finally handler that contains block B2, then all "leave"
        // instructions from B1 must be processed AFTER all "leave" instructions from B2.
        // The following code does exactly this.
        StatementList blocks = method.Body.Statements;

        IMutableRelation/*<Block,Block>*/ blockSuccessor = new BlockRelation();
        ExceptionHandlerList ehs = method.ExceptionHandlers;

        for(int i = 0, n = ehs == null ? 0 : ehs.Count; i < n; i++) {
          ExceptionHandler eh = ehs[i];
          if (eh.HandlerType != NodeType.Finally) { continue; }

          int hdlr_start = (int) b2index[eh.HandlerStartBlock];
          int hdlr_end   = (int) b2index[eh.BlockAfterHandlerEnd];
          int try_start  = (int) b2index[eh.TryStartBlock];
          int try_end    = (int) b2index[eh.BlockAfterTryEnd];

          for(int bti = try_start; bti < try_end; bti++) {
            for(int bhi = hdlr_start; bhi < hdlr_end; bhi++) {
              blockSuccessor.Add(/*B1*/(Block) blocks[bti], /*B2*/(Block) blocks[bhi]);
            }
          }
        }

        // gather all original blocks
        IList/*<Block>*/ originalBlocks = new ArrayList();

        originalBlocks.Add(this.cfg.entry_point);
        originalBlocks.Add(this.cfg.normal_exit_point);
        originalBlocks.Add(this.cfg.excp_exit_point);
        originalBlocks.Add(this.cfg.exit_point);

        for(int i = 0; i < blocks.Count; i++) {
          originalBlocks.Add((Block) blocks[i]);
        }

        IList/*<Block>*/ blocksInOrder = 
          GraphUtil.TopologicallySortGraph(DataStructUtil.NodeSetFactory, originalBlocks, new MapBasedNavigator(blockSuccessor));

        // blocksInOrder starts with the blocks b such that blockSuccessor[b] is empty; i.e., they
        // don't have to wait for any other block
        foreach (Block block in blocksInOrder) {
          StatementList stats = block.Statements;
          for(int i = 0; i < stats.Count; i++) {
            Branch leave = stats[i] as Branch;
            if ((leave == null) || ! leave.LeavesExceptionBlock) { continue; }
            ProcessLeave(block, i, leave);
          }
        }

      }



	
      // The "leave" instruction block.Statements[i] is replaced with a branch instruction
      // to a ChainBlocks of duplicated finally blocks (which are now normal blocks) that finally
      // arrives in the original branch target.
      private void ProcessLeave (Block block, int i, Branch leave) {
        Debug.Assert(block != null);
        Debug.Assert(block.Statements[i] is Branch && ((Branch)block.Statements[i]).LeavesExceptionBlock);
        //Console.Out.WriteLine("process leave from " + CodePrinter.b2s(block) + "," + i);

        Block originalLeaveTarget = ((Branch) block.Statements[i]).Target;
        // 1. find the list of all finally blocks "traversed" by the branch
        IEnumerable/*<ExceptionHandler>*/ list_finally = GetFinallyHandlersForLeave(block, i);

        // 2. duplicate and ChainBlocks them with jumps between them
        Block modBlock = block;
        int   modIndex = i; // the modIndex'th instruction of modBlock will be replaced with a "chaining" Branch
        bool isFirstHandler = true;
        Block firstBlockInFinally, lastBlockInFinally;

        foreach(ExceptionHandler eh in list_finally) {
          DuplicateFinallyBody(eh, block, out firstBlockInFinally, out lastBlockInFinally);

          Debug.Assert(firstBlockInFinally != null && lastBlockInFinally != null);

          ChainBlocks(modBlock, modIndex, firstBlockInFinally, isFirstHandler);

          isFirstHandler = false;
          modBlock = lastBlockInFinally;
        }
        ChainBlocks(modBlock, modIndex, originalLeaveTarget, isFirstHandler);

#if DEBUGxxx
        Console.WriteLine();
        Console.WriteLine("-------------------------------------------------------------");
        Console.WriteLine();
        SimpleDisplay(Console.Out, this.method); 
        Console.WriteLine("NEW:");
        foreach (Block b in new_blocks)
        {
          SimpleDisplay(Console.Out, b, null);
        }
#endif        
      }


      private static Branch MakeUnconditionalBranch (Block target) {
        return new Branch(null, target);
      }



      // computes the list of finally handlers that are traversed by the leave instruction 
      // block.Statements[i].  The order is consistent with the branch direction.
      private IEnumerable/*<ExceptionHandler>*/ GetFinallyHandlersForLeave (Block block, int i) {
        IList/*<ExceptionHandler>*/ list_finally = new ArrayList();
        Branch branch = block.Statements[i] as Branch;

        // find the finally handlers that protect the target block
        FList/*<ExceptionHandler>*/ finallies4target = this.cfg.EnclosingFinallies(branch.Target);

        // find the finally handlers that protect the source block, but not the target one
        FList/*<ExceptionHandler>*/ finallies4source = this.cfg.EnclosingFinallies(block);

        while (finallies4source != null && finallies4source != finallies4target) {
          list_finally.Add(finallies4source.Head);
          finallies4source = finallies4source.Tail;
        }
        // here we made an assumption that branches always go to a place that contains a prefix
        // of the current block's finallies. This turns out not to be the case
        // E.g.:
        // try {
        //    ...
        //    leave 0d;
        // }
        // catch (..) { .. }
        // try {
        //   try {
        // 0d:
        //     ...
        //
        // is code in System.ComponentServices.ReflectPropertyDescriptor.SetValue
        //
        // As a result, the following assert is too conservative.
        //
        // Debug.Assert(finallies4source == finallies4target);

        return list_finally;
      }			


      /// <summary>
      /// When we duplicate a finally body and graft it into the normal path,
      /// the jump that goes to this new code must remain a 'leave' rather
      /// than a 'branch', because we must retain the semantics that 'leave'
      /// clears the evaluation stack before jumping. However, to correctly
      /// do recursive copies (i.e. try/finally inside a finally), we need
      /// to remember that these leaves are not actual leaves. To mark them,
      /// we derive a new class from Branch and use an 'is' test to look for 
      /// this class.
      /// </summary>
      class SpecialBranch : Branch {
        public SpecialBranch (Branch oldBranch, Block target) : base(null, target) {
          this.LeavesExceptionBlock = oldBranch.LeavesExceptionBlock;
          this.SourceContext = oldBranch.SourceContext;
        }
      }


      // Chain modBlock with the block that starts at "target" (see comments inside)
      private void ChainBlocks (Block modBlock, int modIndex, Block target, bool first) {
        if (first) {
          // the "leave" instruction that's being processed
          Branch branch = (Branch) modBlock.Statements[modIndex];
          // NOTE: Initially, I wanted to make the leave a pure branch (i.e., set
          // branch.LeaveExceptionBlock to false). However, as leave flushes the stack
          // this "leave" is NOT a pure branch, even after the finally blocks are chained.
          modBlock.Statements[modIndex] = new SpecialBranch(branch, target);
        }
        else {
          modBlock.Statements.Add(new Branch(null, target));
        }
      }
		

      // Duplication visitor used by DuplicateHandlerBody; declaring it
      // here makes sure only one such object is constructed.
      private readonly DupVisitor dupVisitor;

      private void DuplicateFinallyBody (
        ExceptionHandler eh, 
        Block leave_block,
        out Block startBlock,
        out Block lastBlock
        ) {
        Hashtable/*<Block,Block>*/ orig2copy = new Hashtable();

        Block hdlr_last_block;
        ISet/*<Block>*/ handler_body = GetFinallyBody(eh, out hdlr_last_block);

        foreach(Block orig_block in handler_body) {
          Block clone_block = dupVisitor.VisitBlock(orig_block);
          this.copy2orig.Add(clone_block, orig_block);
          orig2copy.Add(orig_block, clone_block);

          if (ControlFlowGraph.FINALLY_CLONE_DEBUG) {
            this.cfg.b2cloning_leave[clone_block] = leave_block;
          }

          /*
          Console.Out.WriteLine("cloning {0} -> {1}",
            CodePrinter.b2s(orig_block), CodePrinter.b2s(clone_block));
            */

          this.new_blocks.Add(clone_block);
        }

        CorrectBranchVisitor correctBranchVisitor = new CorrectBranchVisitor(orig2copy);

        foreach(Block orig_block in handler_body) {
          Block clone_block = (Block) orig2copy[orig_block];

          //Console.Out.WriteLine("Fixing branching instructions from " + CodePrinter.b2s(clone_block));

          correctBranchVisitor.VisitBlock(clone_block);

          //Console.Out.WriteLine("Fixing handlers for " + CodePrinter.b2s(clone_block));

          FixExceptionalSuccessorForClone(orig_block, clone_block, orig2copy);
          AddHandlerIfClonedBlockStartsHandler(eh, orig_block, clone_block, orig2copy);

          // fix_block2next unless it's the last block or a definite branch. Otherwise block2next is not linear
          if (orig_block != hdlr_last_block) {
            Block orig_next = (Block) block2next[orig_block];
            if (orig_next != null) {
              Block newNext = Convert(orig_next, orig2copy);
              if (newNext != orig_next) {
                block2next[clone_block] = newNext;
              }
            }
          }
        }
				
        // 2nd fixup pass to adjust containing_handler map
        // this cannot be done in previous loop, since the previous loop
        // builds the clones of the handlers.
        //
        Block finallyStart = eh.HandlerStartBlock;
        ExceptionHandler origContainingHandlerOfFinally = (ExceptionHandler)this.cfg.b2_containing_handler[finallyStart]; //MayBeNull

        foreach(Block orig_block in handler_body) {
          Block clone_block = (Block) orig2copy[orig_block];

          ExceptionHandler ceh = (ExceptionHandler) this.cfg.b2_containing_handler[orig_block];
          if (ceh != null) {
            ExceptionHandler eh_clone = (ExceptionHandler) orig2copy[ceh];
            if (eh_clone == null) {
              this.cfg.b2_containing_handler[clone_block] = ceh;
            }
            else {
              this.cfg.b2_containing_handler[clone_block] = eh_clone;
            }
          }


          // the orig_blocks will become part of a new exception handler, so
          // we also need to redirect their containing_handler to the eh (currently a finally) 
          // that will be turned into an exception handler
          // We do this by looking up the containing handler of the finally first block, then
          // changing every block in the finally whose containing handler is the same to the new handler.
          if (ceh == origContainingHandlerOfFinally) {
            this.cfg.b2_containing_handler[orig_block] = eh;
          }
        }

        startBlock = (Block) orig2copy[eh.HandlerStartBlock];
        lastBlock = (Block) orig2copy[hdlr_last_block];
        Debug.Assert(startBlock != null && lastBlock != null);

      }

      // I wish C# had native support for tupples ...
      /*
      private class BlockPair 
      {
        public BlockPair(Block first, Block last) 
        {
          this.first = first;
          this.last  = last;
        }
        public Block first;
        public Block last;
      }
      */

      private ISet/*<Block>*/ GetFinallyBody (ExceptionHandler eh, out Block last_block) {
        StatementList blocks = method.Body.Statements;
        int hdlr_end_index = (int) b2index[eh.BlockAfterHandlerEnd];
        last_block = (Block) blocks[hdlr_end_index-1];
        ISet/*<Block>*/ handler_body = GraphUtil.ReachableNodes
          (DataStructUtil.NodeSetFactory, new Block[]{eh.HandlerStartBlock}, bnav,
          new DNodePredicate((new AvoidClosure(this, eh)).Avoid));
        // ^ isn't it ugly ?  what we need are first class functions ...

        /*
        Console.Out.WriteLine("handler_body(" + CodePrinter.b2s(eh.HandlerStartBlock) +
          ") = " + DataStructUtil.IEnum2String(handler_body, CodePrinter.BlockShortPrinter));
          */

        while ( ! handler_body.Contains(last_block)) {
          // Crap! The last syntactic block is dead (unreachable), so now
          // we need a substitute. We search backward until we find one.
          // This loop is guaranteed to terminate, since we'll eventually
          // get back to the handler start block, which is trivially reachable
          // from the handler start block.
          hdlr_end_index --;
          last_block = (Block) blocks[hdlr_end_index-1];
        }

        Debug.Assert(handler_body.Contains(last_block), "last block not in blocks reachable from start");
        return handler_body;
      }



      private class AvoidClosure {
        public AvoidClosure (FinallyBlockDuplicator parent, ExceptionHandler eh) {
          this.parent          = parent;
          this.orig_hdlr_start = (int) parent.b2index[eh.HandlerStartBlock];
          this.orig_hdlr_end   = (int) parent.b2index[eh.BlockAfterHandlerEnd] - 1;
        }
        private readonly FinallyBlockDuplicator parent;
        private readonly int orig_hdlr_start;
        private readonly int orig_hdlr_end;

        public bool Avoid(object node) {
          CfgBlock block = (CfgBlock) node;
          // special ExcpExitBlock
          if (block is ISpecialBlock) return true;
          // find original block for (Block) node
          block = ControlFlowGraph.orig_block(block, parent.copy2orig);
          // index of the original block in the list of blocks
          int index = (int) parent.b2index[block];
          // Valid blocks are those whose original block was in the handler body.
          return !((index >= orig_hdlr_start) && (index <= orig_hdlr_end));
        }
      }


      private void FixExceptionalSuccessorForClone (
        Block orig_block, 
        Block clone_block, 
        Hashtable/*<Block,Block>*/ orig2copy
        ) {
        Block e_handler = (Block) this.cfg.b2exception_handler[orig_block];
        if (e_handler == null) return;

        Block e_handler_copy = Convert(e_handler, orig2copy);
        this.cfg.b2exception_handler.Add(clone_block, e_handler_copy);
      }


      // if a handler starts at orig_block, a cloned handler starts at clone_block.
      //
      private void AddHandlerIfClonedBlockStartsHandler (
        ExceptionHandler current_eh,
        Block originalBlock, 
        Block blockCopy, 
        Hashtable/*<Block,Block>*/ orig2copy
        ) {
        ExceptionHandler eh = (ExceptionHandler) this.cfg.handlerThatStartsAtBlock[originalBlock];
        // The current finally handler is not duplicated (as a handler).  It is now part of
        // the execution path that replaced some leave instruction, not an exception handler.
        if ((eh == null) || (eh == current_eh)) return;

        ExceptionHandler clonedHandler = (ExceptionHandler) eh.Clone();

        // store the mapping from orig handler to cloned handler in orig2copy (yes, we are overloading this block->block map
        // with eh -> eh mappings.
        // We need this so we can adjust the containingHandler of copied blocks.
        orig2copy[eh] = clonedHandler;

        if (eh.FilterExpression != null) {
          clonedHandler.FilterExpression = FindCopyOfBlock(eh.FilterExpression, orig2copy);
        }
        clonedHandler.HandlerStartBlock = FindCopyOfBlock(eh.HandlerStartBlock, orig2copy);
        clonedHandler.TryStartBlock  = FindCopyOfBlock(eh.TryStartBlock, orig2copy);
        /*
         * The problem with the following code was that the eh.BlockAfter(Handler/Try)End might not have
         * been cloned, so there no equivalent of it in the cloned world!  It would have been better
         * to identify a handler by its last block, not the first after last ...
        clonedHandler.BlockAfterHandlerEnd = FindCopyOfBlock(eh.BlockAfterHandlerEnd, orig2copy);
        clonedHandler.BlockAfterTryEnd     = FindCopyOfBlock(eh.BlockAfterTryEnd, orig2copy);
        if (eh.FilterExpresssion != null)
          clonedHandler.FilterExpresssion = ImperativeGetCopy(eh.FilterExpresssion, orig2copy);
        this.b2started_eh[blockCopy] = clonedHandler;
        */
        this.allExceptionHandlers.Add(clonedHandler);
        this.cfg.handlerThatStartsAtBlock[blockCopy] = clonedHandler;
        if (eh.HandlerType == NodeType.Finally) {
          this.lastHandledBlock[clonedHandler] = FindCopyOfBlock((Block) this.lastHandledBlock[eh], orig2copy);
        }

        // Sine we're introducing a new handler, we need to update the e2_next 
        // relation, since that determines how handlers are chained together.

        // Look up the handler invoked after the one being cloned.
        Block nextHandler = (Block) this.cfg.e2_next[eh.HandlerStartBlock];
        if (orig2copy.ContainsKey(nextHandler)) {
          // If that handler is also being cloned, chain to the 
          // clone rather than the original.
          nextHandler = (Block) orig2copy[nextHandler];
        }
        this.cfg.e2_next[clonedHandler.HandlerStartBlock] = nextHandler;
      }


      // get the clone for block "orig", as indicated by orig2copy.  If no such thing exists, complain!
      private Block FindCopyOfBlock (Block orig, Hashtable/*<Block,Block>*/ orig2copy) {
        Block copy = (Block) orig2copy[orig];
        Debug.Assert(copy != null, "No clone for block " + CodePrinter.b2s(orig));
        return copy;
      }

      // if orig2copy[orig] is non-null, return it; otherwise return orig
      private Block Convert (Block orig, Hashtable/*<Block,Block>*/ orig2copy) {
        if (orig == null) return null;
        Block clone = (Block) orig2copy[orig];
        return (clone == null) ? orig : clone;
      }

      // correct the branching instructions from the cloned exception handler body
      // to jump to the cloned blocks instead of the original ones
      private class CorrectBranchVisitor: StandardVisitor {
        private Hashtable/*<Block,Block>*/ orig2copy;

        public CorrectBranchVisitor(Hashtable/*<Block,Block>*/ orig2copy) {
          this.orig2copy = orig2copy;
        }

        public override Statement VisitBranch(Branch branch) {
          // update the branch target for non-leave branches
          if ( ! branch.LeavesExceptionBlock || branch is SpecialBranch) {
            branch.Target = get_new_target(branch.Target);
          }
          return branch;
        }

        public override Statement VisitSwitchInstruction(SwitchInstruction switchInstruction) {
          BlockList targets = switchInstruction.Targets;
          // update the switch targets
          for(int i = 0; i < targets.Count; i++)
            targets[i] = get_new_target(targets[i]);
          return switchInstruction;
        }

        private Block get_new_target(Block target) {
          Block new_target = (Block) orig2copy[target];
          if (new_target == null)
            return target;
          return new_target;
        }

      }
		
      // visitor for duplicating code blocks. It seems that CciHelper's DuplicatingVisitor is
      // good enough; we just have to fix some minor things
      //
      // The DuplicatingVisitor duplicates This nodes and Locals, but not
      // Parameter nodes.
      //
      private class DupVisitor : Duplicator {

        public DupVisitor(Module module, TypeNode type) : base(module, type) {}


        public override Expression VisitExpression(Expression expression) {
          if (expression == null) return null;
          switch(expression.NodeType) {
            case NodeType.Dup: 
            case NodeType.Arglist:
              break;
            case NodeType.Pop:
              UnaryExpression uex = expression as UnaryExpression;
              if (uex != null) {
                uex = (UnaryExpression)uex.Clone();
                uex.Operand = this.VisitExpression(uex.Operand);
                expression = uex;
              }
              break;
            default:
              expression = (Expression)this.Visit(expression);
              break;
          }
          return expression;
        }

        public override Expression VisitUnaryExpression(UnaryExpression unaryExpression) {
          if (unaryExpression == null) return null;
          unaryExpression = (UnaryExpression)unaryExpression.Clone();
          unaryExpression.Operand = this.VisitExpression(unaryExpression.Operand);
          return unaryExpression;
        }

        public override Expression VisitBinaryExpression(BinaryExpression binaryExpression) {
          if (binaryExpression == null) return null;
          binaryExpression = (BinaryExpression)binaryExpression.Clone();
          binaryExpression.Operand1 = this.VisitExpression(binaryExpression.Operand1);
          binaryExpression.Operand2 = this.VisitExpression(binaryExpression.Operand2);
          return binaryExpression;
        }

        public override Expression VisitMemberBinding(MemberBinding memberBinding) {
          if (memberBinding == null) return null;
          memberBinding = (MemberBinding)memberBinding.Clone();
          memberBinding.TargetObject = this.VisitExpression(memberBinding.TargetObject);
          return memberBinding;
        }

        // Leave nodes should not be duplicated. An in principle, we only need
        // to duplicate expressions that have a dup, or pop subexpression.
        //
        public override Expression VisitLocal(Local local) {
          return local;
        }
			
        public override Expression VisitParameter(Parameter parameter) {
          return parameter;
        }

        public override Expression VisitThis(This thisNode) {
          return thisNode;
        }


        public override Expression VisitIdentifier(Identifier identifier) {
          return identifier;
        }

        public override Expression VisitBase(Base Base) {
          return Base;
        }

        public override Expression VisitImplicitThis(ImplicitThis implicitThis) {
          return implicitThis;
        }

        public override Expression VisitLiteral(Literal literal) {
          return literal;
        }



        public override TypeNode VisitTypeReference(TypeNode type) {
          return type;
        }

        // we don't want to duplicate below blocks.
        public override Block VisitBlock(Block block) {
          block = (Block)(block.Clone());
          block.Statements = this.VisitStatementList(block.Statements);
          return block;
        }

        public override Statement VisitBranch(Branch branch) {
          if (branch == null) return null;
          return ((Branch)branch.Clone());
        }

        public override Statement VisitSwitchInstruction(SwitchInstruction switchInstruction) {
          if (switchInstruction == null) return null;
          return ((SwitchInstruction)switchInstruction.Clone());
        }
      }
		


      // After they have been duplicated for each "leave" instruction, the "finally"
      // handlers are transformed into catch handlers that rethrow their exceptions.
      //
      public void ConvertFinallyHandlerIntoCatchHandler () {
        foreach (ExceptionHandler eh in this.allExceptionHandlers) {
          if (eh.HandlerType == NodeType.Finally ||
            eh.HandlerType == NodeType.FaultHandler) {
            // transform the finally / fault handler into a catch handler ...
            eh.HandlerType = NodeType.Catch;
            // ... that catches all exceptions ... (cont'd at alpha)
            // (btw: in MSIL, one can throw any object, including non-Exceptions)
            eh.FilterType = Cci.SystemTypes.Object;

            // variable to store the caught exception (we can't leave it on the stack because the
            // code of the original finally handler must start with an empty stack).
            Variable exceptionVariable = new Local(new Identifier(FINALLYVARPREFIX + (finally_var_count++)), eh.FilterType);

            // the new catch handler must start with FINALLYVARPREFIX<n> := EPOP ...
            Block firstBlock = eh.HandlerStartBlock;
            StatementList stats = firstBlock.Statements;
            StatementList newBlockStatements = new StatementList();

            AssignmentStatement exceptionAssignment = 
              new AssignmentStatement(exceptionVariable, new Expression(NodeType.Pop));
            if (stats.Count > 0) {
              exceptionAssignment.SourceContext = stats[0].SourceContext;
              exceptionAssignment.Source.SourceContext = stats[0].SourceContext;
            }
            newBlockStatements.Add(exceptionAssignment);

            for(int i = 0; i < stats.Count; i++) {
              newBlockStatements.Add(stats[i]);
            }
            firstBlock.Statements = newBlockStatements;

            // (alpha) ... and rethrows the exception.
            // replace the last instruction of eh with a throw exceptionVariable
            Block lastBlock = (Block) this.lastHandledBlock[eh];

            Statement lastStatement = lastBlock.Statements[lastBlock.Statements.Count - 1];
            if (lastStatement is EndFinally) {

              // replace the endfinally with a rethrow; this is OK because (re)throw flushes the stack too.
              Statement throwStatement;
              {
#if EXPLICIT_THROW_OF_CAPTURED_VARIABLE_RATHER_THAN_RETHROW_IN_FINALLY_EXPANDED_CATCH

						if (false) // for now
						{
							throwStatement = new Throw(exceptionVariable);
						}
						else
#endif
                throwStatement = new Throw(null);
                throwStatement.NodeType = NodeType.Rethrow;
              }
              throwStatement.SourceContext = lastBlock.Statements[lastBlock.Statements.Count - 1].SourceContext;
              lastBlock.Statements[lastBlock.Statements.Count - 1] = throwStatement;
            }
            else {
              Debug.Assert(lastStatement is Throw,
                "finally/fault not terminated in endfinally/endfault or throw! " +
                CodePrinter.StatementToString(lastBlock.Statements[lastBlock.Statements.Count - 1]));
            }
          }
        }
      }

      private int finally_var_count = 0;

      // Finds the last block of the ExceptionHandler eh of Method method.
      private Block LastBlockInsideHandler (ExceptionHandler eh) {
        if ( ! this.b2index.ContainsKey(eh.BlockAfterHandlerEnd)) {
          // Apparently, some methods (e.g. System.Management.MTAHelper.WorkerThread 
          // in System.Management.dll) have a handler end block that names
          // a label (instruction number) beyond the last instruction
          // (presumably to refer to the end of the method). Naturally, this block
          // representing this handler end block isn't part of the method body
          // because it doesn't actually exist. So we artificially add it.
          method.Body.Statements.Add(eh.BlockAfterHandlerEnd);
          this.b2index[eh.BlockAfterHandlerEnd] = method.Body.Statements.Count - 1;

          Debug.Assert(eh.BlockAfterHandlerEnd.Statements.Count == 0); 
          // Can't allow completely empty block.
          eh.BlockAfterHandlerEnd.Statements.Add(new Return());
        }

        int i = (int) this.b2index[eh.BlockAfterHandlerEnd];
        return (Block) method.Body.Statements[i-1];
      }

    }



    /// <summary>
    /// Add explicit Catch statements at beginning of each catch handler.
    /// If a Catch handler has a next handler that differs from the ExceptionHandler enclosing the handler block, then we split 
    /// the Catch statement into a separate block.
    /// 
    /// Special case for Finally handlers that have been turned into catch handlers by the finally-elimination:
    /// - Move the special instruction FINALLYVARPREFIX&lt;n&gt; = pop() to the header.
    /// </summary>
    private void AddCatchStatements (IEnumerable/*<ExceptionHandler>*/ all_ehs, IList new_blocks) {
      if (all_ehs == null) return;
      foreach(ExceptionHandler eh in all_ehs) {
        switch (eh.HandlerType) {
          case NodeType.Catch: {
            Block handlerblock = eh.HandlerStartBlock;
            Block nexthandler = (Block)e2_next[handlerblock];
            Block exnhandler = ExceptionHandler(handlerblock);

            Debug.Assert(nexthandler != null);

            // put Catch into separate block.
            Statement catchStatement = new Catch(null, null, eh.FilterType);
            catchStatement.SourceContext = handlerblock.SourceContext;
            link_handler_header_statement_to_block(new_blocks, catchStatement, 
              handlerblock, nexthandler, exnhandler);
            break;
          }

          case NodeType.Filter: {
            // insert a Filter instruction at the beginning of filter block
            Block handlerblock = eh.HandlerStartBlock;
            Block nexthandler = (Block)e2_next[handlerblock];
            Block exnhandler = ExceptionHandler(handlerblock);

            Debug.Assert(nexthandler != null);

            // put Filter into separate block.
            Statement filterStatement = new Filter();
            filterStatement.SourceContext = handlerblock.SourceContext;
            link_handler_header_statement_to_block(new_blocks, filterStatement, 
              handlerblock, nexthandler, exnhandler);

            // Commented out, since we treat filter's differently now.

            // insert a Catch all instruction at the beginning of the handler block
            // prepend_statement_to_block(
            //	new Catch(null, null, System.Compiler.SystemTypes.Exception), 
            //	eh.HandlerStartBlock);
            break;
          }

          default:
            continue;
        }
      }
    }


    private void link_handler_header_statement_to_block (
      IList new_blocks, 
      Statement c, 
      Block catchblock, 
      Block nexthandler, 
      Block exnhandler
      ) {
      // we need to use block for the Catch block, and create a fresh block for the current instructions in the block.
      CfgBlock succ = new CfgBlock(catchblock.Statements);

      new_blocks.Add(succ);

      catchblock.Statements = new StatementList(c);

      // move FINALLYVARPREFIX<n> = pop from succ to catchblock if present
      if (succ.Statements != null && succ.Statements.Count > 0) {
        AssignmentStatement astmt = succ.Statements[0] as AssignmentStatement;
        if (astmt != null) {
          Variable v = astmt.Target as Variable;
          if (v != null && v.Name != null && v.Name.Name.StartsWith(FINALLYVARPREFIX)) {
            catchblock.Statements.Add(astmt);
            StatementList old = succ.Statements;
            succ.Statements = new StatementList(old.Count-1);
            for (int i=1; i<old.Count; i++) {
              succ.Statements.Add(old[i]);
            }
          }
        }
      }
      // fixup block information:

      // add current successors of block to successors of succ
      CfgBlock succ_of_succ = (CfgBlock)this.b2next[catchblock];
      if (succ_of_succ != null) {
        this.b2next.Add(succ, succ_of_succ);
      }
      // set exception handler for new block
      b2exception_handler[succ] = exnhandler;

      // set finally scope
      b2_enclosing_finally[succ] = b2_enclosing_finally[catchblock];

      // set containing handler for copied block.
      b2_containing_handler[succ] = b2_containing_handler[catchblock];

      // fixup exception handler of catch block
      b2exception_handler[catchblock] = nexthandler;

      // fixup normal successor for catch block
      this.b2next[catchblock] = succ;
    }


    private void prepend_statement_to_block (Statement c, CfgBlock block) {
      StatementList stats = block.Statements;
      // the first block of the handler cannot be empty
      c.SourceContext = stats[0].SourceContext;
      StatementList new_stats = new StatementList();
      new_stats.Add(c);
      for(int i = 0; i < stats.Count; i++)
        new_stats.Add(stats[i]);
      block.Statements = new_stats;
    }

    private void AddBlock(CfgBlock bb, CfgBlock[] blocks, ref int index) {
      blocks[index] = bb;
      bb.AssignIndex(this, index);
      index++;
    }

    private void FindReachable(IMutableSet reach, CfgBlock current, NormalFlowBlockNavigator nfnav) {
      if (reach.Contains(current)) return;
      reach.Add(current);
      foreach (CfgBlock next in nfnav.NextNodes(current)) {
        FindReachable(reach, next, nfnav);
      }
      // also follow exceptional path
      CfgBlock handler = (CfgBlock)this.b2exception_handler[current];
      if (handler != null) {
        FindReachable(reach, handler, nfnav);
      }
    }

    // build the array all_blocks: the original blocks + the block clones (if any) + special blocks
    private void BuildAllBlockCollection (
      Method method, 
      IList/*<CfgBlock>*/ new_blocks, 
      ISet/*<CfgBlock>*/ filterblocks,
      NormalFlowBlockNavigator nfnav
      ) {
      IMutableSet reach = new HashSet();
      FindReachable(reach, (CfgBlock)method.Body.Statements[0], nfnav);

      int nb_blocks = reach.Count + 2; // entry and exit
      if (!reach.Contains(this.normal_exit_point)) {
        nb_blocks++;
      }
      if (!reach.Contains(this.excp_exit_point)) {
        nb_blocks++;
      }

      CfgBlock[] all_blocks;
      this.all_blocks = all_blocks = new CfgBlock[nb_blocks];

      int index = 0;

      AddBlock(this.entry_point, all_blocks, ref index);

      foreach (CfgBlock block in reach) {
        AddBlock(block, all_blocks, ref index);
      }

#if false
      int nb_blocks = method.Body.Statements.Count - filterblocks.Count + 4;

      nb_blocks += new_blocks.Count;

      CfgBlock[] all_blocks;
      this.all_blocks = all_blocks = new CfgBlock[nb_blocks];
      StatementList blocks = method.Body.Statements;
      int index = 0;

      AddBlock(this.entry_point, all_blocks, ref index);
      for(int i = 0 ; i < blocks.Count; i++) {
        if ( ! filterblocks.Contains(blocks[i])) {
          AddBlock((CfgBlock)blocks[i], all_blocks, ref index);
        }
      }
      if (new_blocks != null) {
        // adding the block clones to all_blocks
        foreach(CfgBlock block in new_blocks) {
          AddBlock(block, all_blocks, ref index);
        }
      }
#endif

      // adding the three special blocks
      if (!reach.Contains(this.normal_exit_point)) {
        AddBlock(this.normal_exit_point, all_blocks, ref index);
      }
      if (!reach.Contains(this.excp_exit_point)) {
        AddBlock(this.excp_exit_point, all_blocks, ref index);
      }
      AddBlock(this.exit_point, all_blocks, ref index);
    }

    private void build_stmt2block_map () {
      Hashtable map = new Hashtable();
      this.stmt2block = map;

      for (int i=0; i<this.all_blocks.Length; i++) {
        Block b = this.all_blocks[i];
        if (b != null) {
          StatementList sl = b.Statements;

          for (int j=0; j<sl.Count; j++) {
            map.Add(sl[j], b);
          }
        }
      }
    }


    private void BuildNormalFlow (
      CfgBlock[] blocks, 
      NormalFlowBlockNavigator nfbnav, 
      CfgBlock real_entry
      ) {
      IMutableRelation/*<Block,Block>*/ n_succ = new BlockRelation();

      foreach(Block block in blocks) {
        if (block is ISpecialBlock) continue;
        n_succ.AddAll(block, nfbnav.NextNodes(block));
      }
      // common sense rules
      n_succ.Add(entry_point, real_entry);
      n_succ.Add(normal_exit_point, exit_point);
      n_succ.Add(excp_exit_point, exit_point);

      IRelation/*<Block,Block>*/ n_pred = n_succ.Reverse();
      b2n_succ = Relation.Compact(n_succ, typeof(CfgBlock));
      b2n_pred = Relation.Compact(n_pred, typeof(CfgBlock));
    }




    /// <summary>
    /// Construct a continuation description for each block
    /// </summary>
    private void BuildContinuationMap (CfgBlock[] blocks) {
      Hashtable conts = new Hashtable();
			
      conts[this.entry_point] = new StraightContinuation( (CfgBlock) this.b2next[this.entry_point]);
      conts[this.normal_exit_point] = new StraightContinuation(this.exit_point);
      conts[this.excp_exit_point] = new UnwindContinuation();
      conts[this.exit_point] = new DefaultContinuation();

      foreach(CfgBlock block in blocks) {
        // the special (and empty ...) blocks have already been dealt with
        if (block is ISpecialBlock) {
          continue;
        }

        Statement stat = get_last_stat(block);
        if (stat == null) {
          conts[block] = new StraightContinuation((CfgBlock) this.b2next[block]);
          continue;
        }
        switch(stat.NodeType) {
          case NodeType.Return: 
            conts[block] = new ReturnContinuation();
            break;

          case NodeType.Throw:
          case NodeType.Rethrow:
            conts[block] = new ThrowContinuation();
            break;

          case NodeType.Branch:
            Branch branch = (Branch) stat;
            // consider the edge for jump taken
            if (branch.Condition == null) { 
              // uncond. jump
              conts[block] = new StraightContinuation((CfgBlock)branch.Target);
            }
            else {
              conts[block] = new IfContinuation((CfgBlock)branch.Target, (CfgBlock) this.b2next[block]);
            }
            break;

          case NodeType.SwitchInstruction:
            SwitchInstruction swi = (SwitchInstruction)stat;
            conts[block] = new SwitchContinuation(swi.Targets, (CfgBlock) this.b2next[block]);
            break;

          case NodeType.EndFinally:
            conts[block] = new EndFaultContinuation();
            break;

          default:
            conts[block] = new StraightContinuation((CfgBlock) this.b2next[block]);
            break;
        }
      }
      this.b2_cont = conts;
    }


    // return the last statement of block b. If b is empty, returns null
    // If the last stat is a LabeledStatement, elimate all labels
    // and return the real last statement.
    private static Statement get_last_stat(CfgBlock block) {
      StatementList list = block.Statements;
      // empty block "support"
      if (list.Count == 0) return null;
      Statement stat = list[list.Count-1];
      // eliminate the labels to get the real statement
      while(stat is LabeledStatement)
        stat = ((LabeledStatement) stat).Statement;
      return stat;
    }


    private class ExcpFlowBlockNavigator: ForwardOnlyGraphNavigator {
      public ExcpFlowBlockNavigator(ControlFlowGraph parent) {
        this.parent = parent;
      }
      private ControlFlowGraph parent;
      public override IEnumerable NextNodes(object node) {
        Block exn_handler = parent.ExceptionHandler((CfgBlock)node);
        if (exn_handler != null) {
          return new Block[]{exn_handler};
        }
        return new Block[0];
      }
    }

    private class NormalFlowBlockNavigator: ForwardOnlyGraphNavigator {
      public NormalFlowBlockNavigator(ControlFlowGraph parent, Hashtable/*<Block,Block>*/ block2next) {
        this.parent = parent;
        this.block2next = block2next;
      }
      private ControlFlowGraph parent;
      private Hashtable/*<Block,Block>*/ block2next;
      public override IEnumerable NextNodes(object node) {
        CfgBlock block = (CfgBlock) node;
        Statement stat = ControlFlowGraph.get_last_stat(block);
        if (stat != null) {
          switch (stat.NodeType) {
            case NodeType.Return:
              return new Block[] { parent.normal_exit_point };
            case NodeType.Throw:
            case NodeType.Rethrow:
              return new Block[0];
            case NodeType.Branch:
              Branch branch = (Branch)stat;
              if (branch.Condition == null)
                return new Block[] { branch.Target }; // uncond. jump
              else // conditional jump
                return new Block[] { (Block)block2next[block], branch.Target };
            case NodeType.SwitchInstruction:
              BlockList targets = ((SwitchInstruction)stat).Targets;
              Block[] sw_succ = new Block[targets.Count + 1];
              for (int i = 0; i < targets.Count; i++)
                sw_succ[i] = targets[i];
              sw_succ[targets.Count] = (Block)block2next[block];
              return sw_succ;
            case NodeType.EndFinally:
              return new Block[0];

            case NodeType.EndFilter: // end filter looks like a fall through when the filter applies
            default:
              break;
          }
        }
        Block textualNext = (Block)block2next[block];
        if (textualNext != null) {
          return new Block[] { textualNext };
        }
        return new Block[0];
      }
    }


    private bool BuildExceptionalFlow (Method method, StatementList blocks,
      out IList/*<Exceptionhandler>*/ all_ehs, ISet/*<Block>*/ filterblocks) {
      bool has_finally = false;
      ExceptionHandlerList ehs = method.ExceptionHandlers;
      if (ehs == null || ehs.Count == 0) {
        examine_trivial_excp_flow(blocks, filterblocks);
        all_ehs = null;
      }
      else
        has_finally = examine_real_excp_flow(method, ehs, blocks, out all_ehs);

      // add exception handler edge from new entry block to exception exit block
      b2exception_handler[this.entry_point] = this.excp_exit_point;

      return has_finally;
    }

    private void examine_trivial_excp_flow(StatementList blocks, ISet/*<Block>*/ filterblocks) {
      // usual case; efficient
      b2exception_handler = new Hashtable();
      e2_next = new Hashtable(0);
      b2_enclosing_finally = new Hashtable(0);
      b2_containing_handler = new Hashtable(0);

      // the usual case: no handler - all exceptions flow to excp_exit_point
      // \forall block, block --e-> excp_exit_point;
      for(int i = 0; i < blocks.Count; i++)
        // note: this is not entirely correct, since our filterblocks list does not contain all the blocks that are part of the filter.
        if ( ! filterblocks.Contains(blocks[i])) // exceptions thrown from filter blocks are swallowed by the clr!!
          b2exception_handler.Add(blocks[i], excp_exit_point);
    }


    /// <summary>
    /// Computes the ExceptionHandler for each block and the chaining of exception handlers.
    /// Note: 
    ///   Filter handlers are currently treated as starting at the filter expression
    ///   and the endfilter is like a fall through into the actual handler.
    /// </summary>
    private bool examine_real_excp_flow(Method method, ExceptionHandlerList ehs, StatementList blocks,
      out IList/*<ExceptionHandler>*/ all_ehs) {
      bool has_finally = false;
      b2exception_handler = new Hashtable();
      all_ehs      = new ArrayList();
      this.handlerThatStartsAtBlock = new Hashtable();
      Hashtable/*<Block,Stack<ExceptionHandler>>*/ b2start = new Hashtable();
      Hashtable/*<Block,Queue<ExceptionHandler>>*/ b2end   = new Hashtable();
      Hashtable/*<Block,Queue<ExceptionHandler>>*/ b2handler_end = new Hashtable();

      // records for each exception handler, the exception handler tried next
      e2_next = new Hashtable();
      b2_enclosing_finally = new Hashtable();
      b2_containing_handler = new Hashtable();

      for(int i = 0; i < ehs.Count; i++) {
        ExceptionHandler eh = ehs[i];
        all_ehs.Add(eh);
        if (eh.HandlerType == NodeType.Finally)
          has_finally = true;
        if (eh.HandlerType == NodeType.Filter) {
          /*
           * WE NEED TO HANDLE Filter exceptions to do Visual Basic
           * 
          throw new UnsupportedCodeException(method, "Filter exception handler");
          */
          // we pretend the filter block is a catch block
          // for uniform processing while flattening
          // i.e. it's a block that starts with an implicit
          // push of the exception, just like a handler.

          // HACK, we reverse the FilterExpression and HandlerStart so that
          // We treat the entire filter as a handler
          Block filter = eh.FilterExpression;
          eh.FilterExpression = eh.HandlerStartBlock;
          eh.HandlerStartBlock = filter;
        }
        this.handlerThatStartsAtBlock[eh.HandlerStartBlock] = eh;

        add_try_start(eh, b2start);
        add_try_end(eh, b2end);
        add_handler_end(eh, b2handler_end);
      }

      FList/*<ExceptionHandler>*/ handlers = FList.Empty; // protecting the current block
      FList/*<ExceptionHandler>*/ finallies = FList.Empty; // protecting the current block
      FList/*<ExceptionHandler>*/ containingHandlers = FList.Empty; // containing the current block (top) is current (non-finally)

      // Push the implicit exceptional exit handler at the bottom of the stack
      ExceptionHandler handler_around_entire_method = new ExceptionHandler();
      handler_around_entire_method.HandlerStartBlock = excp_exit_point;
      handlers = FList.Cons(handler_around_entire_method, handlers);

      for(int i = 0; i < blocks.Count; i++) {
        Block block = (Block) blocks[i];

        // pop protecting handlers off stack whose scope ends here
        Queue ends = (Queue) b2end[block];
        if (ends != null)
          foreach(ExceptionHandler eh in ends) {
            if (eh == handlers.Head)
              handlers = handlers.Tail; // Pop handler
            else
              throw new ApplicationException("wrong handler");

            if (eh.HandlerType == NodeType.Finally) {
              if (eh == finallies.Head) {
                finallies = finallies.Tail; // Pop finally
              }
              else
                throw new ApplicationException("wrong finally on stack");
            }
          }

        // push protecting handlers on stack whose scope starts here
        Stack starts = (Stack) b2start[block];
        if (starts != null) {
          foreach(ExceptionHandler eh in starts) {
            ExceptionHandler enclosing = (ExceptionHandler)handlers.Head;

            // push this handler on top of current block enclosing handlers
            handlers = FList.Cons(eh, handlers); // Push handler

            // also record the next enclosing handler for this handler.
            e2_next[eh.HandlerStartBlock] = enclosing.HandlerStartBlock;

            // also keep stack of finallies
            if (eh.HandlerType == NodeType.Finally) {
              finallies = FList.Cons(eh, finallies);
            }
          }
        }

        // pop containing handlers off containing stack whose handler scope ends here
        Queue handler_ends = (Queue) b2handler_end[block];
        if (handler_ends != null) {
          foreach(ExceptionHandler eh in handler_ends) {
            if (eh == containingHandlers.Head) {
              containingHandlers = containingHandlers.Tail; // Pop containingHandler
            }
            else {
              throw new ApplicationException("wrong containingHandler on stack");
            }
          }
        }
        // push containing handler on stack whose handler scope starts here
        ExceptionHandler seh = (ExceptionHandler) this.handlerThatStartsAtBlock[block];
        if (seh != null) {
          containingHandlers = FList.Cons(seh, containingHandlers);
        }

        // We now add a single exceptional successor, which is the closest enclosing one.
        ExceptionHandler topeh = (ExceptionHandler)handlers.Head;

        b2exception_handler.Add(block, topeh.HandlerStartBlock);

        // We also store the enclosing finally list
        b2_enclosing_finally.Add(block, finallies);

        // We also store the map block -> exception handler containing block in handler
        if (containingHandlers != null) {
          b2_containing_handler.Add(block, containingHandlers.Head);
        }
      }
      return has_finally;
    }

    private void add_try_start(ExceptionHandler eh, Hashtable/*<Block,Stack<ExceptionHandler>>*/ b2start) {
      Block eh_start = eh.TryStartBlock;
      Stack starts = (Stack) b2start[eh_start];
      if (starts == null)
        b2start.Add(eh_start, starts = new Stack());
      starts.Push(eh);
    }

    private void add_try_end(ExceptionHandler eh, Hashtable/*<Block,Queue<ExceptionHandler>>*/ b2end) {
      Block eh_end = eh.BlockAfterTryEnd;
      Queue ends = (Queue) b2end[eh_end];
      if (ends == null)
        b2end.Add(eh_end, ends = new Queue());
      ends.Enqueue(eh);
    }

    private void add_handler_end(ExceptionHandler eh, Hashtable/*<Block,Queue<ExceptionHandler>>*/ b2end) {
      Block eh_end = eh.BlockAfterHandlerEnd;
      Queue ends = (Queue) b2end[eh_end];
      if (ends == null)
        b2end.Add(eh_end, ends = new Queue());
      ends.Enqueue(eh);
    }


    // fixup jumps to heads of handlers because they need to skip the catch instruction we inserted.
    private void FixupJumpsToHandlerHead() {
      for (int i = 0; i<this.all_blocks.Length; i++) {
        Block b = this.all_blocks[i];
        StatementList sl = b.Statements;
        if (sl != null && sl.Count > 0) {
          Branch branch = sl[sl.Count-1] as Branch;
          if ( branch != null) {
            ExceptionHandler handlerTarget = (ExceptionHandler) this.HandlerThatStartsAtBlock(branch.Target);
            // Assumption: every handler is a catch at this point.
            if (handlerTarget != null) {
              // found branch to head of handler. Fixup to go to successor
              branch.Target = (Block)this.b2next[branch.Target];
              Debug.Assert(branch.Target != null);
            }
          }
        }
      }
    }

    // reverse b2e_succ to construct b2e_pred
    private void BuildHandlerPredecessorMap () {
      Relation e_pred = new BlockRelation();
      foreach(Block block in all_blocks) {
        Block e_block = ExceptionHandler(block);
        if (e_block != null)
          e_pred.Add(e_block, block);
      }
      b2e_pred = Relation.Compact(e_pred, typeof(CfgBlock));
    }
		

    /// <summary>
    /// "Marker" interface: only the three artificial blocks introduced by the CFG implement it.
    /// You should not try to implement it in any of your classes.
    /// </summary>
    public interface ISpecialBlock {}

    public class EntryBlock: CfgBlock, ISpecialBlock {
      public EntryBlock(MethodHeader header) : base(new StatementList(1)) {
        this.Statements.Add(header);
      }
    }

    public class ExitBlock : CfgBlock, ISpecialBlock {
      public ExitBlock() : base(new StatementList()) {}
    }

    public class NormalExitBlock : CfgBlock, ISpecialBlock {
      public NormalExitBlock() : base(new StatementList()) {}
    }

    public class ExcpExitBlock : CfgBlock, ISpecialBlock {
      public ExcpExitBlock(Method method) : base(new StatementList(new Statement[]{ new Unwind(method) })) {
      }
    }


    /// <summary>
    /// Return the entry point of this CFG.  This is the point where the execution
    /// of the underlying method starts.
    /// </summary>
    public EntryBlock Entry {
      get { return entry_point; }
    }
    /// <summary>
    /// Return the special block for the exit point of this CFG.
    /// </summary>
    public ExitBlock Exit {
      get { return exit_point; }
    }
    /// <summary>
    /// Return the special block for the normal exit of this CFG.
    /// </summary>
    public NormalExitBlock NormalExit {
      get {	return normal_exit_point; }
    }
    /// <summary>
    /// Return the special block for the exceptional exit of this CFG.
    /// </summary>
    public ExcpExitBlock ExceptionExit {
      get { return excp_exit_point; }
    }

    /// <summary>
    /// Returns an array containing all the blocks from this CFG.
    /// You should never mutate this array.
    /// </summary>
    public CfgBlock[] Blocks() {
      return all_blocks;
    }


    /// <summary>
    /// Return the total number of blocks in this CFG
    /// </summary>
    public int BlockCount { get { return this.all_blocks.Length; } }

    /// <summary>
    /// If statement is part of this CFG, returns the containing block, otherwise null
    /// </summary>
    public CfgBlock ContainingBlock (Statement stmt) {
      if (this.stmt2block == null) {
        this.build_stmt2block_map();
      }
      return (CfgBlock)this.stmt2block[stmt];
    }

    private readonly static CfgBlock[] emptyBlockArray = new CfgBlock[0];
    private CfgBlock[] get_block_array(Hashtable/*<Block,Block[]>*/ b2array, Block block) {
      CfgBlock[] result = (CfgBlock[]) b2array[block];
      return (result == null) ? emptyBlockArray : result;
    }



    /// <summary>
    /// Returns the topologically sorted list of the strongly connected components of blocks
    /// (according to the control flow).  The first SCC in the returned list is the one that
    /// contains the method entry block.
    /// </summary>
    /// <returns>Top-sort list of SCCs of blocks from <c>this</c> CFG (method entry first).</returns>
    public IList/*<StronglyConnectedComponent<Block>>*/ SortedSCCs() {
      if (sorted_sccs == null) {
        IEnumerable/*<StronglyConnectedComponent<Block>>*/ all_sccs =
          StronglyConnectedComponent.ConstructSCCs(this.Blocks(), new BackwardGraphNavigator(this));
        sorted_sccs = GraphUtil.TopologicallySortComponentGraph(DataStructUtil.DefaultSetFactory, all_sccs);
      }
      return sorted_sccs;
    }



    /// <summary>
    /// Returns the successors of <c>block</c> on both normal and exceptional flow.
    /// Iterating over the returned <c>IEnumerable</c> is equivalent to iterating first
    /// over the normal successors and next over the exception flow successors.
    /// </summary>
    public IEnumerable Succ(CfgBlock block) {
      return new CompoundEnumerable(NormalSucc(block), ExcpSucc(block));
    }

    /// <summary>
    /// Returns the normal successors of <c>block</c>.
    /// You should never mutate this array.
    /// </summary>
    public CfgBlock[] NormalSucc(CfgBlock block) {
      return get_block_array(b2n_succ, block);
    }

    /// <summary>
    /// Returns the closest enclosing handler of a particular block
    /// where control goes if an exception is raised inside <c>block</c>.  
    /// </summary>
    public CfgBlock ExceptionHandler(Block block) {
      return (CfgBlock)b2exception_handler[block];
    }

    /// <summary>
    /// Returns the predecessors of <c>block</c> on both normal and exceptional flow.
    /// Iterating over the returned <c>IEnumerable</c> is equivalent to iterating first
    /// over the normal predecessors and next over the exception flow predecessors.
    /// </summary>
    /// <param name="block"></param>
    /// <returns></returns>
    public IEnumerable Pred(CfgBlock block) {
      return new CompoundEnumerable(NormalPred(block), ExcpPred(block));
    }

    /// <summary>
    /// Returns the normal predecessors of <c>block</c>.
    /// You should never mutate the returned array.
    /// </summary>
    public CfgBlock[] NormalPred(CfgBlock block) {
      return get_block_array(b2n_pred, block);
    }



    /// <summary>
    /// Returns the predecessors of <c>block</c> on the exceptional flow of control.
    /// In C# terms, if <c>block</c> is the beginning of an exception handler, these are
    /// the blocks protected by that exception handler.  Otherwise, this array has
    /// length 0.
    /// You should never mutate the returned array.
    /// </summary>
    public CfgBlock[] ExcpPred(CfgBlock block) {
      return get_block_array(b2e_pred, block);
    }

    /// <summary>
    /// Returns the successor of <c>block</c> on the exceptional flow of control.
    /// You should never mutate the returned array.
    /// </summary>
    public CfgBlock[] ExcpSucc(CfgBlock block) {
      CfgBlock eh = (CfgBlock)this.b2exception_handler[block];
      if (eh == null) return emptyBlockArray;

      return new CfgBlock[1]{eh};
    }


    internal FList EnclosingFinallies(Block block) {
      return (FList)b2_enclosing_finally[block];
    }


    /// <summary>
    /// Returns the ExceptionHandler that starts at <c>block</c>, if any; null otherwise.
    /// This is useful when trying to see what kind of exceptions can arrive in <c>block</c>
    /// </summary>
    public ExceptionHandler HandlerThatStartsAtBlock (Block block) {
      // the method has no exception handlers at all
      if (this.handlerThatStartsAtBlock == null) return null;
      //			Block orig = (Block) copy2orig[block];
      //			if (orig != null) block = orig;
      return (ExceptionHandler) this.handlerThatStartsAtBlock[block];
    }



    /// <summary>
    /// Useful for finding which handler a block belongs to. This may be needed for
    /// determining rethrow information.
    /// </summary>
    /// <remarks>This is NOT the handler protecting the block!</remarks>
    /// <param name="block"></param>
    /// <returns>Start block of handler containing the given block. Otherwise null,
    /// if given block is not part of any handler.</returns>
    public CfgBlock HandlerContainingBlock (CfgBlock block) {
      ExceptionHandler eh = (ExceptionHandler) this.b2_containing_handler[block];

      if (eh == null) return null;

      return (CfgBlock)eh.HandlerStartBlock;
    }

    /// <summary>
    /// FOR DEBUG ONLY:
    /// <p>
    /// Returns the list of "leave" instructions whose processing created a specific block.
    /// Returns an empty <c>IEnumerable</c> if an original block (not a clone) is sent as
    /// argument.  In the enumeration, the innermost leave instructions come first.</p>
    ///
    /// Returns <c>null</c> if block is not a clone.
    /// 
    /// NOTE: as we don't have source context info for branching instructions, we manipulate
    /// the blocks of the leave's instead of the leave instructions themselves.
    /// </summary>
    /// <param name="block"></param>
    /// <returns></returns>
    public IEnumerable/*<CfgBlock>*/ FinallyCloningChain(CfgBlock block) {
      check_finally_debug_support();
      if (this.b2cloning_leave == null) return null;
      Stack stack = new Stack();
      while(true) {
        Block leave_block = (Block) this.b2cloning_leave[block];
        if (leave_block == null) break;
        stack.Push(leave_block);
        block = (CfgBlock) this.copy2orig[block];
      }
      return (stack.Count == 0) ? null : stack;
    }
    private Hashtable/*<CfgBlock,IList<CfgBlock>>*/ b2cloning_leave;

    /// <summary>
    /// FOR DEBUG ONLY:
    /// <p/>
    /// Returns the original block that, possibly through some cloning, produced a specific block.
    /// Transitively walks over the copy2orig map until the original block is found.  Acts as an
    /// identity function for an original block.
    /// </summary>
    /// <param name="block"></param>
    /// <returns></returns>
    public CfgBlock OrigBlock(CfgBlock block) {
      check_finally_debug_support();
      if (this.copy2orig == null) return null;
      return ControlFlowGraph.orig_block(block, this.copy2orig);
    }		
    // map cloned block -> original finally block
    private Hashtable/*<CfgBlock,CfgBlock>*/ copy2orig;

    // this is not only for debug; it's used internally by GetHandlerBody
    private static CfgBlock orig_block(CfgBlock block, Hashtable/*<CfgBlock,CfgBlock>*/ copy2orig) {
      while(true) {
        CfgBlock parent = (CfgBlock) copy2orig[block];
        if (parent == null) return block;
        block = parent;
      }
    }

    private void check_finally_debug_support() {
      if (!FINALLY_CLONE_DEBUG)
        throw new ApplicationException("Debug support for finally duplication is off!");
    }

    // IGraphNavigator methods
    public IEnumerable NextNodes (object node) {
      return Succ((CfgBlock) node);
    }

    public IEnumerable PreviousNodes (object node) {
      return Pred((CfgBlock) node);
    }


    /// <summary>
    /// Block is unreachable from entry
    /// </summary>
    public bool IsDead(CfgBlock block) {
      return (block.StackDepth < 0);
    }

    /// <summary>
    /// Returns the stack depth at the beginning of block <c>block</c>.
    /// </summary>
    /// <param name="block">Block that we are interested in.</param>
    /// <returns>Stack depth at the beginning of <c>block</c>.</returns>
    [Obsolete("CfgBlocks have StackDepth property directly")]
    public int StackDepth(CfgBlock block) {
      try {
        return block.StackDepth;
      }
      catch(NullReferenceException nre) {
        throw new Exception("Possible causes: CFG constructed without stack removal (see constructor) / " +
          CodePrinter.b2s(block) + " is not a block of this CFG", nre);
      }
    }

    /// <summary>
    /// Captures how a block continues. Currently only for normal control flow.
    /// </summary>
    public abstract class Continuation {
      public abstract ContinuationKind Kind { get; }
    }


    public Continuation GetContinuation(CfgBlock b) {
      object cont = b2_cont[b];
      Debug.Assert(cont != null, "continuation of block not found");
      return (Continuation)cont;
    }

    public class StraightContinuation : Continuation {
      CfgBlock target;

      public CfgBlock Next { get {return target; } }
      public StraightContinuation (CfgBlock target) {
        this.target = target;
      }

      public override ContinuationKind   Kind {
        get {
          return ContinuationKind.Unconditional;
        }
      }


    }

    public class ReturnContinuation : Continuation {
      public override ContinuationKind   Kind {
        get {
          return ContinuationKind  .Return;
        }
      }

    }

    public class ThrowContinuation : Continuation {
      public override ContinuationKind   Kind {
        get {
          return ContinuationKind  .Throw;
        }
      }

    }

    public class IfContinuation : Continuation {
      CfgBlock truetarget;
      CfgBlock falsetarget;

      public IfContinuation(CfgBlock truetarget, CfgBlock falsetarget) {
        this.truetarget = truetarget;
        this.falsetarget = falsetarget;
      }

      public CfgBlock True { get { return truetarget; } }
      public CfgBlock False { get { return falsetarget; } }

      public override ContinuationKind   Kind {
        get {
          return ContinuationKind  .Conditional;
        }
      }

    }

    public class SwitchContinuation : Continuation {
      BlockList targets;
      CfgBlock defaulttarget;

      public SwitchContinuation(BlockList targets, CfgBlock defaulttarget) {
        this.targets = targets;
        this.defaulttarget = defaulttarget;
      }


      public BlockList Targets { get { return targets; } }
      public CfgBlock Default { get { return defaulttarget; } }

      public override ContinuationKind   Kind {
        get {
          return ContinuationKind  .Switch;
        }
      }

    }


    public class EndFaultContinuation : Continuation {
      public override ContinuationKind   Kind {
        get {
          return ContinuationKind  .EndFault;
        }
      }

    }

    public class DefaultContinuation : Continuation {
      public override ContinuationKind   Kind {
        get {
          return ContinuationKind  .Unconditional;
        }
      }

    }

    public class UnwindContinuation : Continuation {
      public override ContinuationKind   Kind {
        get {
          return ContinuationKind  .Unwind;
        }
      }

    }


    public void Display() {
      TextWriter tw = new StreamWriter(File.Open(@"c:\temp\logcfg", FileMode.Append, FileAccess.Write, FileShare.Read));

      Display(tw);

      tw.Close();
    }


    /// <summary>
    /// Simplified CFG pretty printer: all the info printers are set to null.
    /// </summary>
    /// <param name="tw"></param>
    public void Display(TextWriter tw) {
      this.Display(tw, null, null, null);
    }


    public void Display(
      TextWriter tw, 
      DGetBlockInfo get_pre_block_info, 
      DGetBlockInfo get_post_block_info, 
      DGetStatInfo get_stat_info
      ) {
      Display(tw, this.Blocks(), get_pre_block_info, get_post_block_info, get_stat_info);
    }

    /// <summary>
    /// CFG pretty-printer.  For each block, this method
    /// calls <c>pre_printer</c>c(if non-null),
    /// prints the normal/excp. successors,
    /// the code of the block, the normal/excp. predecessors and finally calls
    /// <c>post_printer</c>.  A useful use of the pre and post printers is the
    /// debugging of a flow-sensitive analysis.
    /// </summary>
    /// <param name="tw">Where to print.</param>
    public void Display(
      TextWriter tw, 
      CfgBlock[] blocks,
      DGetBlockInfo get_pre_block_info, 
      DGetBlockInfo get_post_block_info, 
      DGetStatInfo get_stat_info
      ) {
      Hashtable b2id = new Hashtable();
      for(int i = 0; i < blocks.Length; i++)
        b2id[blocks[i]] = i;
      for(int i = 0; i < blocks.Length; i++) {
        CfgBlock block = blocks[i];
        tw.WriteLine("BLOCK " + (block is ISpecialBlock ? "*" : "") + CodePrinter.b2s(block, b2id));
        if (get_pre_block_info != null)
          tw.WriteLine(get_pre_block_info(block));
        print_block_array(" Normal pred:      ", NormalPred(block), b2id, tw);
        print_block_array(" Protected blocks: ", ExcpPred(block), b2id, tw);
        if (ExcpPred(block).Length != 0) {
          ExceptionHandler eh = HandlerThatStartsAtBlock(block);
          if (eh != null)
            tw.WriteLine(" Handler {0} [{1},{2})",
              eh.HandlerType,
              CodePrinter.b2s(eh.HandlerStartBlock, b2id),
              CodePrinter.b2s(eh.BlockAfterHandlerEnd, b2id));
        }
        CodePrinter.PrintBlock(tw, block, get_stat_info, b2id);
        print_block_array(" Normal succ:      ", NormalSucc(block), b2id, tw);
        print_block_array(" Excp succ:        ", this.ExcpSucc(block), b2id, tw);
        print_block(" Handler:          ", ExceptionHandler(block), b2id, tw);
        print_block_list(" Finallies:        ", (FList)b2_enclosing_finally[block], b2id, tw);
        ExceptionHandler ceh = (ExceptionHandler)b2_containing_handler[block];
        if (ceh != null) print_block(" Containing handler", ceh.HandlerStartBlock, b2id, tw);

        if (get_post_block_info != null)
          tw.WriteLine(get_post_block_info(block));
        tw.WriteLine();
      }
      tw.WriteLine("Entry      = " + CodePrinter.b2s(Entry, b2id));
      tw.WriteLine("NormalExit = " + CodePrinter.b2s(NormalExit, b2id));
      tw.WriteLine("ExcptExit  = " + CodePrinter.b2s(ExceptionExit, b2id));
      tw.WriteLine("Exit       = " + CodePrinter.b2s(Exit, b2id));
    }


    public void DumpGraph (string filename) {
      TextWriter w = new StreamWriter(filename);
      w.WriteLine("digraph G {");

      CfgBlock[] blocks = this.Blocks();
      Hashtable b2id = new Hashtable();
      for (int i = 0; i < blocks.Length; i++)
        b2id[blocks[i]] = i;

      for (int i=0; i<blocks.Length; i++) {
        StringWriter ss = new StringWriter();
        CodePrinter.PrintBlock(ss, blocks[i], null, b2id);
        string label = ss.ToString().Replace("\"", "\\\"").Replace("\n", "\\n");
        w.WriteLine("block{0} [shape=box, label=\"{1}\"]", blocks[i].UniqueKey, label);
      }

      for (int i=0; i<blocks.Length; i++) {
        Block[] succ = NormalSucc(blocks[i]);
        for (int j=0; j<succ.Length; j++) {
          w.WriteLine("block{0} -> block{1};", blocks[i].UniqueKey, succ[j].UniqueKey);
        }
        Block exn = ExceptionHandler(blocks[i]);
        if (exn != null) {
          w.WriteLine("block{0} -> block{1} [style=dotted];", blocks[i].UniqueKey, exn.UniqueKey);
        }
      }

      w.WriteLine("}");
      w.Close();
    }

    private static void print_block_array(string label, Block[] blocks, Hashtable b2id, TextWriter tw) {
      if (blocks.Length == 0) return;
      tw.Write(label + ": ");
      foreach(Block block in blocks) {
        tw.Write(CodePrinter.b2s(block, b2id));
        tw.Write(" ");
      }
      tw.WriteLine();
    }

    private static void print_block_list(string label, FList/*<ExceptionHandler>*/ finallies, Hashtable b2id, TextWriter tw) {
      if (finallies == null) return;
      tw.Write(label + ": ");
      foreach(ExceptionHandler eh in finallies) {
        tw.Write(CodePrinter.b2s(eh.HandlerStartBlock, b2id));
        tw.Write(" ");
      }
      tw.WriteLine();
    }


    private static void print_block(string label, Block block, Hashtable b2id, TextWriter tw) {
      if (block == null) return;
      tw.Write(label + ": ");
      tw.WriteLine(CodePrinter.b2s(block, b2id));
    }


    private StronglyConnectedComponents sccs;
    private StronglyConnectedComponents StronglyConnectedComponents {
      get {
        if (this.sccs == null)
          this.sccs = new StronglyConnectedComponents(this);
        return this.sccs;
      }
    }

    private FList preorder;
    public IEnumerable PreOrder {
      get {
        if (preorder == null) {
          preorder = Cci.PreOrder.Compute(this);
        }
        return preorder;
      }
    }

    public Compare PreOrderCompare { 
      get { 
        return new Compare(CompareBlockPriority);
      }
    }

    private int CompareBlockPriority(Object o1, Object o2) {
      CfgBlock b1 = (CfgBlock)o1;
      CfgBlock b2 = (CfgBlock)o2;
      return (b1.priority - b2.priority);
    }


    [Obsolete("A ControlFlowGraph is already a control flow graph. Cast no longer needed", true)]
    public static ControlFlowGraph Cast(ControlFlowGraph cfg) {
      return cfg;
    }

  }


	/// <summary>
	/// Special statement to identify the starting point of a method.
	/// This is the definition point for all the method parameters. (this way, each variable is
	/// defined by one or more statements).
	/// </summary>
	public class MethodHeader : Statement
	{
		/// <summary>
		/// Creates the MethodHeader statement for <c>method</c>.
		/// </summary>
		public MethodHeader(Method method) : base(NodeType.Nop)
		{
			this.method     = method;
			IList parameters = new ArrayList();
			if (!CciHelper.IsStatic(method))
			{
				This thisParam = CciHelper.GetThis(method);
				Debug.Assert(thisParam != null);
				parameters.Add(thisParam);
			}
			ParameterList parlist = method.Parameters;
			for(int i = 0, n = parlist == null ? 0 : parlist.Count; i < n; i++)
				parameters.Add(parlist[i]);
			this.parameters = parameters;
		}
		/// <summary>
		/// List of method parameters, including This if the method is non-static.
		/// This is grabbed from the method. If it is not found there, but the method is non static,
		/// we create one to enforce the invariant that each non-static method has a This parameter.
		/// </summary>
		public readonly IEnumerable/*<Parameter>*/ parameters;
		/// <summary>
		/// Method that <c>this</c> MethodHeader belongs to; useful in case you want to grab more information.
		/// </summary>
		public readonly Method method;
	}

	/// <summary>
	/// Special statement to identify the exception exit point of a method.
	/// </summary>
	public class Unwind : Statement
	{
		public Unwind(Method method) : base(NodeType.Nop)
		{
			this.SourceContext = method.SourceContext;
		}
	}


  public enum ContinuationKind {
    Unconditional,
    Conditional,
    Switch,					// switch instruction. UniqueSuccessor gives default branch
    EndFault,				// end of fault handling, continue to enclosing handler (ExceptionHandler)
    EndFilter,			// decide if the handler code (UniqueSuccessor) applies given a value, otherwise the ExceptionHandler is used
    Unwind,					// exceptional exit from method.
    Return,					// normal exit from method.
    Throw,					// block ends in a throw. Has no normal successor. Control transfers to ExceptionHandler
  }



}
