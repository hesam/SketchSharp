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
  using System.Collections;
  using System.Diagnostics;

  public interface IDataFlowState 
  {
    void Dump();
  }	




  /// <summary>
  /// Implements a pretty standard data flow analysis with merging of data flow states at join
  /// points. 
  /// 
  /// Details:
  /// 
  /// At each block, we maintain two data flow states, pending, and done. Done represents the 
  /// dataflow state under which the block has already been analyzed, whereas Pending represent
  /// the state under which the block still needs to be analyzed.
  /// 
  /// When a block is reached with a state, it is merged into the pending state. This merge can
  /// either be precise, or with weakening (includes now more possibilities than the two states
  /// that were merged).
  /// 
  /// Once a block is dequeued for analysis, we compute the new done state, which is the merge
  /// of the pending state with the	old done state. There are 3 possible outcomes:
  /// 1. The old done state completely supersedes the new pending state: no reanalysis is necessary
  /// 2. The merge is precise, then the block needs to be analyzed only with the pending state 
  ///    (no need to redo all cases in the done state). 
  /// 3. The merge is imprecise (contains more possibilities than in either the pending or the old 
  ///    done state), then we need to analyze the block using this merged state in order to account
  ///    for these new possibilities.
  ///   
  /// Another imprecision arises from the merge of pending states. This merge could be imprecise, and
  /// therefore at the next analysis, we could be analyzing the block under extra cases. Buckets could
  /// be used to avoid this. Buckets just increase the language of formulas for expressing fixpoints to
  /// a finite disjunction.
  /// 
  /// Each block can be in one of 3 states:
  /// a) unenabled (block needs to be reached by more edges before we run it)
  /// b) enabled and in work queue (needs to be rescheduled)
  /// c) enabled but has been processed with latest incoming states.
  /// </summary>
  public abstract class ForwardDataFlowAnalysis
  {
    private static bool _trace = false;

    protected ControlFlowGraph cfg;
    // indexed by CfgBlocks
    private IDataFlowState[] pendingStates;  /* states that have reached the block and need to be classified */
    // indexed by CfgBlocks
    private IDataFlowState[] doneStates;     /* states under which the block has been analyzed */

    public ForwardDataFlowAnalysis () 
    {
    }


    public static bool Tracing
    {
      get { return _trace || Analyzer.DebugDFA; }
      set { _trace = value; }
    }


    private IDataFlowState PendingState (CfgBlock block) 
    {
      return(pendingStates[block.Index]);
    }

    /// <summary>
    /// Like PendingState returns the pending state for the given block, but
    /// it also sets the pending state for this block to null.
    /// </summary>
    /// <returns>old pending state</returns>
    private IDataFlowState PopPendingState (CfgBlock block) 
    {
      IDataFlowState pending = pendingStates[block.Index];

      pendingStates[block.Index] = null;

      if (Tracing) 
      {
        Console.WriteLine("PopPendingState: block {0}", (block).UniqueKey);
      }
      return pending;
    }

    private void SetPendingState (CfgBlock block, IDataFlowState pending) 
    {
      pendingStates[block.Index] = pending;
      if (Tracing) 
      {
        Console.WriteLine("SetPendingState: block {0}", (block).UniqueKey);
      }
    }

    private IDataFlowState DoneState (CfgBlock block) 
    {
      return(doneStates[block.Index]);
    }

    /// <summary>
    /// Merge the new pending state with the old pending states.
    /// </summary>
    /// <returns>merged pending state</returns>
    private IDataFlowState JoinWithPendingState (CfgBlock prev, CfgBlock block, IDataFlowState newState) 
    {
      if (Tracing) 
      {
        Console.WriteLine("JoinWithPendingState: block {0} -> {1}", 
          (prev).UniqueKey, 
          (block).UniqueKey);
      }

      IDataFlowState pending = PendingState(block);
      // note, we call Merge even if old is null.
      bool changed;
      bool precise;

      if (Tracing) {
        SourceContext sc = (block).SourceContext;

        if (sc.Document == null && block.Length > 0) {
          sc = ((Statement)block[0]).SourceContext;
        }
        Console.WriteLine("Join with pending state at line {0}", sc.StartLine);
      }

      IDataFlowState merged = this.Merge(prev, block, pending, newState, out changed, out precise);
	
      if (Tracing) {
         Console.WriteLine("Join changed {0}", changed);
      }
      return merged;
    }

    /// <summary>
    /// Checks if a block needs to be reanalyzed and under what state.
    /// 
    /// Updates the doneState of this block to reflect the pending state
    /// </summary>
    /// <returns>null if no reanalysis necessary, the DFS state if the merge is precise,
    /// the merged state if the merge is imprecise
    /// </returns>
    private IDataFlowState StateToReanalyzeBlock (CfgBlock prev, CfgBlock block, IDataFlowState pending, out bool preciseMerge) {
      IDataFlowState done = DoneState(block);
      // note, we call Merge even if old is null.
      bool changed;

      if (Tracing) {
        Console.WriteLine("StateToReanalyzeBlock: block {0} -> {1}", 
          (prev).UniqueKey, 
          (block).UniqueKey);

        SourceContext sc = (block).SourceContext;

        if (sc.Document == null && block.Length > 0) {
          sc = ((Statement)block[0]).SourceContext;
        }
        Console.WriteLine("StateToReanalyzeBlock at line {0}", sc.StartLine);

      }
      IDataFlowState merged = this.Merge(prev, block, done, pending, out changed, out preciseMerge);
	
      if (merged != null)
        doneStates[block.Index] = merged;

      if ( ! changed ) {

        if (Tracing) {
          Console.WriteLine("Done State");
          done.Dump();
          Console.WriteLine("Pending State");
          pending.Dump();
          Console.WriteLine("StateToReanalyzeBlock result UNchanged");
        }
        return null;
      }
      if ( preciseMerge ) return pending;

      if (Tracing) {
        Console.WriteLine("StateToReanalyzeBlock result CHANGED");
        if (done == null) {
          Console.WriteLine("no done state yet");
        }
        else {
          Console.WriteLine("Done State");
          done.Dump();
        }
        Console.WriteLine("Pending State");
        pending.Dump();
        Console.WriteLine("Merged State");
        merged.Dump();
      }
      return merged;
    }


    private void Reinitialize (ControlFlowGraph cfg) 
    {
      this.cfg = cfg;
      this.pendingStates = new IDataFlowState[cfg.BlockCount];
      this.doneStates = new IDataFlowState[cfg.BlockCount];

      // initialize work queue and disabled queue
      joinWorkItems = new WorkQueue(cfg.PreOrderCompare);
    }



    /// <summary>
    /// Elements can only be once on this queue. Duplicates are removed.
    /// </summary>
    private class WorkQueue : PriorityQueue 
    {
      public WorkQueue(Compare compare) : base(compare)
      {}

      public void Enqueue(CfgBlock b) 
      {
        this.Add(b);
      }

      public CfgBlock Dequeue() 
      {
        return (CfgBlock)this.Pull();
      }

      public void Dump() {
        for (int i=0; i<array.Count; i++) {
          CfgBlock block = (CfgBlock)array[i];
          Console.WriteLine("{0}: b{1} ({2})", i, block.Index, block.UniqueKey);
        }
      }
    }

    WorkQueue/*<Block>*/ joinWorkItems;

    /// <summary>
    /// Starts the analysis from the entry block of the CFG
    /// </summary>
    protected virtual void Run (ControlFlowGraph cfg, IDataFlowState startState) 
    {
      this.Run(cfg, cfg.Entry, startState);
    }


    /// <summary>
    /// Starts the analysis at the first instruction of the given block
    /// </summary>
    protected virtual void Run (ControlFlowGraph cfg, CfgBlock startBlock, IDataFlowState startState) 
    {
      this.Reinitialize(cfg);

      pendingStates[startBlock.Index] = startState;
      joinWorkItems.Enqueue(startBlock);

      while (joinWorkItems.Count > 0) 
      {
        //joinWorkItems.Dump();
        CfgBlock currentBlock = joinWorkItems.Dequeue();	

        if (Analyzer.Debug) 
        {
          Console.WriteLine("\n*** Working on block {0} [{1} statements, line {2}]\n", 
            ((currentBlock).UniqueKey), 
            currentBlock.Length, 
            (currentBlock.Length == 0)? -1 : ((Statement)currentBlock[0]).SourceContext.StartLine);
        }

        // Flow the current state through the block.
        IDataFlowState currentState = PopPendingState(currentBlock);
				
        currentState = VisitBlock(currentBlock, currentState);

        // NOTE: VisitBlock may have explicitly pushed states onto some successors. In that case
        // it should return null to avoid this code pushing the same state onto all successors.

        if (currentState != null) 
        {
          foreach (CfgBlock succ in currentBlock.NormalSuccessors) 
          {
            PushState(currentBlock, succ, currentState);
          }
        }

      } //while
    }


    /// <summary>
    /// Push the given state onto the handler of the block.
    /// This causes call-backs to SplitException in order to correctly distribute
    /// the exception state among different nested handlers.
    /// </summary>
    /// <param name="currentBlock">Block from which exception escapes</param>
    /// <param name="state">state on exception flow</param>
    public void PushExceptionState (
      CfgBlock currentBlock,
      IDataFlowState state
      )
    {
      IDataFlowState currentHandlerState = state;
      CfgBlock currentHandler = currentBlock;
      IDataFlowState nextHandlerState;

      while (currentHandlerState != null)
      {
        Debug.Assert(currentHandler.ExceptionHandler != null, 
          String.Format("block {0} does not have an exception handler",
          (currentHandler).UniqueKey));

        currentHandler = currentHandler.ExceptionHandler;

        if (Tracing) 
        {
          Console.WriteLine("PushExceptionState (in loop): block {0} -> {1}", 
            (currentBlock).UniqueKey,
            (currentHandler).UniqueKey);
        }

        SplitExceptions(currentHandler, ref currentHandlerState, out nextHandlerState);

        /// We allow SplitExceptions to make decisions about not propagating any exceptions
        /// Debug.Assert(currentHandlerState != null || nextHandlerState != null);

        if (currentHandlerState != null)
        {
          PushState(currentBlock, currentHandler, currentHandlerState);
        }

        currentHandlerState = nextHandlerState;
      } 
    }


		
    /// <summary>
    /// Add the given state to the pending states of the target block. If 
    /// the block is enabled (by the pending edge count optimization), add the
    /// block to the worklist.
    /// 
    /// Inv: DoneState => PendingState /\ PendingState != null => InQueue
    /// 
    /// Cases:
    ///   1. Done => new, nothing to do
    ///   2. Done |_| new is precise.  Pend' = Pend |_| new,  Done' = Done |_| new
    ///   3. Done |_| new is imprecise.  Pend' = Done |_| new,  Done' = Done |_| new
    /// </summary>
    public void PushState (
      CfgBlock currentBlock, 
      CfgBlock nextBlock, 
      IDataFlowState state
      ) 
    {
      if (Tracing) 
      {
        Console.WriteLine("PushState: block {0} -> {1}", 
          (currentBlock).UniqueKey,
          (nextBlock).UniqueKey);
      }


      // state == null signals that branch is infeasible
      if (state == null) 
      {
        return;
      }

      bool precise;
      // Add state to done state
      IDataFlowState stillPending = this.StateToReanalyzeBlock(currentBlock, nextBlock, state, out precise);

      if (stillPending == null) 
      {
        if (Tracing) 
        {
          Console.WriteLine("PushState: block {0} no new information for pending state.", 
            (nextBlock).UniqueKey);
        }
        return;
      }

      if (precise) 
      {
        // join stillPending to old pending.
        stillPending = this.JoinWithPendingState(currentBlock, nextBlock, stillPending);
      }
      this.SetPendingState (nextBlock, stillPending);
			
      // when dequeued, the pending state is what the block needs to be analyzed under.
      //
      joinWorkItems.Enqueue(nextBlock);
      if (Tracing) 
      {
        Console.WriteLine("PushState: block {0} put on work queue.", 
          (nextBlock).UniqueKey);
      }
    }



    protected Cci.Block ConvertBlock (CfgBlock block) { return (block); }



    /// <summary>
    /// Default per block visitor. Called from Run.
    /// 
    /// It calls VisitStatement on each statement in a block. 
    /// 
    /// The result of this method is used as the state for all normal control flow successors.
    /// To push state onto an exception handler, use the PushExceptionState method. Furthermore, for
    /// conditional branches, different states can be pushed onto the true and false branches directly
    /// by calling PushPending. In that case, null should be returned from the method in order to avoid pushing
    /// the returned state onto both true and false targets again.
    /// </summary>
    protected virtual IDataFlowState VisitBlock (CfgBlock block, IDataFlowState stateOnEntry) 
    {
      IDataFlowState currentState = stateOnEntry;

      for (int i=0; i<block.Length; i++) 
      {
        if (currentState == null) return null;

        currentState = this.VisitStatement(block, (Statement)block[i], currentState);
      }

      return currentState;
    }

    /// <summary>
    /// Default per statement visitor called from the default VisitBlock.
    /// Does identity transformation. Subclasses either override this method
    /// if the default block handling is sufficient, or they override the Visit method for blocks.
    /// 
    /// The result of this method is used as the state for all normal control flow successors.
    /// To push state onto an exception handler, use the PushExceptionState method. Furthermore, for
    /// conditional branches, different states can be pushed onto the true and false branches directly
    /// by calling PushPending. In that case, null should be returned from the method in order to avoid pushing
    /// the returned state onto both true and false targets again.
    /// </summary>
    protected virtual IDataFlowState VisitStatement (CfgBlock block, Statement statement, IDataFlowState dfstate) 
    {
      // simple case analysis to distinguish throws
      switch (statement.NodeType) 
      {
        case NodeType.Throw:
        case NodeType.Rethrow:
        {
          PushExceptionState(block, dfstate);
          return null;
        }
        default:
          return dfstate;
      }
    }

    /// <summary>
    /// Compute the join of two data flow states at the given block.
    /// </summary>
    /// <param name="previous">Predecessor block for this new state</param>
    /// <param name="joinPoint">Block at which join is computed</param>
    /// <param name="atMerge">Old state at this block. Can be null, in which case the incoming state
    /// is the first non-bottom state. In this case, the method must set changed
    ///  <c>resultDiffersFromPreviousMerge</c> to true.</param>
    /// <param name="incoming">New data flow state flowing to this block.</param>
    /// <param name="resultDiffersFromPreviousMerge">Boolean for fix point. If the state after
    /// the merge is equal to the old <c>atMerge</c> state, set to false, otherwise set to true.</param>
    /// <param name="mergeIsPrecise">can be set to true if the merged result state strictly contains only
    /// information representing either the atMerge or the incoming state, but no extra approximation. If
    /// this information cannot be determined by the merge, it must return false. True can only be returned
    /// if result is truly precise.</param>
    /// <returns>The new merged state.</returns>
    protected abstract IDataFlowState Merge (
      CfgBlock previous, 
      CfgBlock joinPoint, 
      IDataFlowState atMerge, 
      IDataFlowState incoming, 
      out bool resultDiffersFromPreviousMerge,
      out bool mergeIsPrecise
      );

    /// <summary>
    /// Splits the exceptions into the ones that this handler will handle and the ones that should
    /// <code>currentHandlerState</code> and <code>nextHandlerState</code> cannot both be null.
    /// On exit, if <code>currentHandlerState</code> is null, <code>handler</code> handles no exceptions,
    /// and if <code>nextHandlerState</code> is null, <code>handler</code> handles all the exceptions in
    /// the initial exception set of <code>currentHandlerState</code>.
    /// </summary>
    // go on to the next handler. currentHandlerState and next
    protected abstract void SplitExceptions (
      CfgBlock handler, 
      ref IDataFlowState currentHandlerState, out IDataFlowState nextHandlerState
      );
  }


  ///TODO:
  ///
  /// Write a specialized dataflow engine that can keep track of branch condition expressions
  /// It should automatically build up the branch condition expression and keep track of the
  /// dataflow values at the various points when they are used in operations. It should
  /// also keep track of reassignments, so that we know which values are current.
  /// 
  /// At a branch, the analysis can present conditions for the true and false branch that hold.
  /// For example, for the code sequence
  /// 1)  st0 = null;
  /// 2)  st1 = local1;
  /// 3)  st0 = st1 Eq st0;
  /// 4)  branchif st0 Target;
  /// 
  /// The analysis would present for the false branch that:
  ///    st0 (A3[st0]) current is not zero
  ///    local1 (A2[st1]) old is equal to temp1 A2[st0]
  ///    
}