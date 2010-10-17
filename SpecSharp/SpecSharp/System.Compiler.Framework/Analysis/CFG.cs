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
	using System.Collections;


	public class StronglyConnectedComponents 
	{
		private ControlFlowGraph cfg;
		private IList/*<StronglyConnectedComponent<CfgBlock>>*/ sccs;
		private Hashtable/*<Block,StronglyConnectedComponent>*/ sccMap;


		public StronglyConnectedComponents (ControlFlowGraph cfg)
		{
			this.cfg = cfg;
			this.sccMap = null;

			IEnumerable/*<StronglyConnectedComponent<CfgBlock>>*/ all_sccs =
				StronglyConnectedComponent.ConstructSCCs(this.cfg.Blocks(), new BackwardGraphNavigator(this.cfg));
			this.sccs = GraphUtil.TopologicallySortComponentGraph(DataStructUtil.DefaultSetFactory, all_sccs);
		}


		#region IStronglyConnectedComponents Members

		public IStronglyConnectedComponent SccForBlock (CfgBlock block)
		{
			if (this.sccMap == null)
			{
				this.sccMap = new Hashtable();
				foreach (StronglyConnectedComponent/*<CfgBlock>*/ scc in this.sccs)
				{
					foreach (CfgBlock iblock in scc.Nodes)
					{
						this.sccMap[iblock] = scc;
					}
				}
			}
			return (StronglyConnectedComponent) this.sccMap[(block)];
		}

		#endregion

		#region IList Members

		public bool IsReadOnly
		{
			get
			{
				// TODO:  Add StronglyConnectedComponents.IsReadOnly getter implementation
				return false;
			}
		}

		public object this [int index]
		{
			get { return this.sccs[index]; }
			set { this.sccs[index] = value; }
		}

		public void RemoveAt (int index)
		{
			this.sccs.RemoveAt(index);
		}

		public void Insert (int index, object value)
		{
			this.sccs.Insert(index, value);
		}

		public void Remove (object value)
		{
			this.sccs.Remove(value);
		}

		public bool Contains (object value)
		{
			return this.sccs.Contains(value);
		}

		public void Clear()
		{
			this.sccs.Clear();
		}

		public int IndexOf (object value)
		{
			return this.sccs.IndexOf(value);
		}

		public int Add (object value)
		{
			return this.sccs.Add(value);
		}

		public bool IsFixedSize
		{
			get
			{
				return this.sccs.IsFixedSize;
			}
		}

		#endregion

		#region ICollection Members

		public bool IsSynchronized
		{
			get { return this.sccs.IsSynchronized; }
		}

		public int Count
		{
			get { return this.sccs.Count; }
		}

		public void CopyTo (Array array, int index)
		{
			this.sccs.CopyTo(array, index);
		}

		public object SyncRoot
		{
			get { return this.sccs.SyncRoot; }
		}

		#endregion

		#region IEnumerable Members

		public IEnumerator GetEnumerator()
		{
			return this.GetEnumerator();
		}

		#endregion
	}


	class ArrayIndexable : IIndexable
	{
		object[] members;

		public ArrayIndexable(Cci.StatementList stmts) 
		{
			this.members = new Cci.Statement[stmts.Count];
			for (int i=0; i<stmts.Count; i++) 
			{
				this.members[i] = stmts[i];
			}
		}

		public ArrayIndexable(Cci.BlockList blocks) 
		{
			this.members = new CfgBlock[blocks.Count];

			for (int i=0; i<blocks.Count; i++) 
			{
				this.members[i] = (CfgBlock)blocks[i];
			}
		}



		#region IIndexable Members


		public object this[int index]
		{
			get
			{
				return this.members[index];
			}
		}

		public int Length
		{
			get
			{
				return this.members.Length;
			}
		}


		#endregion


		#region IEnumerable Members


		public System.Collections.IEnumerator GetEnumerator()
		{
			return this.members.GetEnumerator();
		}


		#endregion
	}

  internal sealed class PreOrder {

    /// <summary>
    /// Compute a pre order (ignoring back edges) of the CFG reachable from the entry node
    /// 
    /// As a side effect, assigns each block its DF finishing number.
    /// </summary>
    public static FList/*<BasicBlocks>*/ Compute(ControlFlowGraph cfg) {
      // depth-first search

      bool[] markBit = new bool[cfg.BlockCount];

      FList result = null;
      int DFTime = 0;

      // Use a stack to represent the state of the search.
      // Each stack element consists of a basic block and the
      // the index of the next successor of that block to process.

      Stack stack = new Stack();

      CfgBlock[] blocks = cfg.Blocks();

      CfgBlock start = cfg.Entry;
      // invariant: all blocks pushed on the stack are marked.
      markBit[start.Index] = true;
      stack.Push(new StackElem(start));
      while (stack.Count > 0) {
        StackElem elem = (StackElem) stack.Peek();
        CfgBlock b = elem.node;
        int nextChild = elem.nextChild;
        CfgBlock[] normalNodes = cfg.NormalSucc(b);
        CfgBlock[] exnNodes = cfg.ExcpSucc(b);
        int normalNodesCount = normalNodes.Length;
        int exnNodesCount = exnNodes.Length;
        // Is there another child to process?
        if (nextChild < normalNodesCount + exnNodesCount) {
          // Figure out the actual block.
          CfgBlock child;
          if (nextChild < normalNodesCount) {
            child = normalNodes[nextChild];
          } else {
            child = exnNodes[nextChild-normalNodesCount];
          }
          elem.nextChild = nextChild+1;
          // push the child block on to the stack.
          if (!markBit[child.Index]) {
            markBit[child.Index]=true;
            stack.Push(new StackElem(child));
          }
        } else {
          // After all children are processed, place the block
          // on the result.
          stack.Pop();
          b.priority = ++DFTime;
          result = FList.Cons(b, result);
        }
      }
      return result;
    }

    class StackElem {
      internal StackElem(CfgBlock node) {
        this.node = node;
        this.nextChild = 0;
      }

      internal CfgBlock node;
      internal int nextChild;
    }

  }
}