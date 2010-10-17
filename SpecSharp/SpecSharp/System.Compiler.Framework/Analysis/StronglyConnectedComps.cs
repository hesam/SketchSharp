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
  using System.Text;
  using System.Collections;
  using System.Diagnostics;


	
	
  /// <summary>
	/// Interface for Strongly Connected Methods.
	/// </summary>
	public interface IStronglyConnectedComponent
	{
		/// <summary>
		/// Returns the nodes contained into <c>this</c> StronglyConnectedComponent.
		/// </summary>
		IEnumerable Nodes { get; }

		bool Contains (object node);

		/// <summary>
		/// Returns the number of nodes in <c>this</c> StronglyConnectedComponent.
		/// </summary>
		/// <returns></returns>
		int Size { get; }

		/// <summary>
		/// Returns the SCCs that are end points of the arcs that starts in
		/// <c>this</c> StronglyConnectedComponent, i.e., the successors of <c>this</c> StronglyConnectedComponent in the
		/// component graph. Does not contain <c>this</c> StronglyConnectedComponent.
		/// </summary>
		IEnumerable NextComponents { get; }

		/// <summary>
		/// Returns the SCCs that are starting points for arcs that end
		/// in <c>this</c> StronglyConnectedComponent, i.e., the predecessors of <c>this</c> StronglyConnectedComponent
		/// in the component graph. Does not contain <c>this</c> StronglyConnectedComponent.
		/// </summary>
		IEnumerable PreviousComponents { get; }
		/// <summary>
		/// Checks whether <c>this</c> StronglyConnectedComponent is a cycle, i.e., if it has more than
		/// one node or it has a single node which points to itself.  The only
		/// StronglyConnectedComponent that does not contain a cycle is a StronglyConnectedComponent composed of a single node
		/// which doesn't point to itself.
		/// </summary>
		/// <returns></returns>
		bool ContainsCycle { get; }

		/// <summary>
		/// Detailed text representation of <c>this</c> StronglyConnectedComponent.
		/// <c>ToString</c> will return just a unique text id of the StronglyConnectedComponent,
		/// while the detailed text representation will be produced by
		/// <c>FullToString</c>
		/// </summary>
		/// <returns></returns>
		string FullToString();
	}



	/// <summary>
	/// StronglyConnectedComponent is a full implementation of the interface <c>ISCC</c>.
	/// It comes with a producer static method that constructs the
	/// component graph for a given graph. 
	/// </summary>
	public sealed class StronglyConnectedComponent: IStronglyConnectedComponent
	{
		// unique numeric id for debug purposes 
		private int id;

		private IMutableSet nodes     = new HashSet();
		private IMutableSet next_SCCs = new HashSet();
		private IMutableSet prev_SCCs = new HashSet();
		private bool contains_cycle = true;


		// SCCs should be created only by Util.ConstructSCCs
		private StronglyConnectedComponent () { }

		private StronglyConnectedComponent (int id)
		{
			this.id = id;
		}


		public IEnumerable Nodes { get { return this.nodes; } }


		public bool Contains (object node)
		{
			return nodes.Contains(node);
		}


		public int Size { get { return this.nodes.Count; } }


		public IEnumerable NextComponents { get { return this.next_SCCs; } }


		public IEnumerable PreviousComponents { get { return this.prev_SCCs; } }


		public bool ContainsCycle { get { return this.contains_cycle; } }


		/// <summary>
		/// Detailed text representation of <c>this</c> StronglyConnectedComponent.
		/// </summary>
		/// <returns></returns>
		public string FullToString ()
		{
			StringBuilder buff = new StringBuilder();
			buff.Append(this);
			buff.Append(" (");
			buff.Append(Size);
			buff.Append(") {\n");
			buff.Append(" Nodes: ");
			buff.Append(Nodes);
			buff.Append("\n");
			buff.Append(" ContainsCycle: ");
			buff.Append(ContainsCycle);
			buff.Append("\n");
			if(next_SCCs.Count > 0)
			{
				buff.Append(" Next: ");
				buff.Append(NextComponents);
				buff.Append("\n");
			}
			if(prev_SCCs.Count > 0)
			{
				buff.Append(" Prev: ");
				buff.Append(PreviousComponents);
				buff.Append("\n");
			}
			buff.Append("}");
			return buff.ToString();
		}

		/// <summary>
		/// Simplified text representation for debug purposes: "StronglyConnectedComponent" + numeric id.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return "StronglyConnectedComponent" + id;
		}


		/// <summary>
		/// Use the <c>nav</c> navigator to explore the graph rooted in the
		/// objects from the <c>roots</c> set, decomposes it into strongly
		/// connected components. Returns the set of strongly connected components.
		/// </summary>
		public static IEnumerable/*<StronglyConnectedComponent>*/ ConstructSCCs(IEnumerable roots, IGraphNavigator navigator)
		{
			return (new SCCFactory(navigator)).ConstructSCCs(roots);
		}

		// private class that does the actual job behind ConstructSCCs
		private sealed class SCCFactory
		{
			public SCCFactory (IGraphNavigator navigator) { this.navigator = navigator; }

			public IEnumerable/*<StronglyConnectedComponent>*/ ConstructSCCs (IEnumerable roots)
			{
				
#if DEBUG_GRAPH
				Console.WriteLine("ConstructSCCs doing 1st dfs...");
#endif
				GraphUtil.SearchDepthFirst(
					roots,
					navigator,
					null,
					null,
					null,
					new DNodeVisitor(first_dfs_end_visitor)
				);

#if DEBUG_GRAPH
				Console.WriteLine("ConstructSCCs doing 2nd dfs...");
#endif

				GraphUtil.SearchDepthFirst(
					nodes_desc_order,
					new BackwardGraphNavigator(navigator),
					new DNodePredicate(second_dfs_avoid),
					new DNodeVisitor(create_new_SCC),
					new DNodeVisitor(second_dfs_begin_visitor),
					null
				);

#if DEBUG_GRAPH
				Console.WriteLine("ConstructSCCs doing put_arcs...");
#endif
				put_arcs();

				return all_SCCs;
			}


			// navigator through the graph
			private IGraphNavigator navigator;
			// the nodes in the subgraph rooted in the roots
			private IMutableSet  nodes_in_graph = new HashSet();
			// holds all the nodes in the explored subgraph; the top of the stack
			// is the node whose dfs traversal finished last 
			private Stack nodes_desc_order = new Stack();

			private StronglyConnectedComponent current_scc = null;
			private ArrayList all_SCCs = new ArrayList();
			private Hashtable/*<object,StronglyConnectedComponent>*/ node2scc = new Hashtable();

			// numeric id used to generate distinct ids for the generated SCCs
			private int scc_id = 0;

			private void first_dfs_end_visitor(object node)
			{
				nodes_in_graph.Add(node);
				nodes_desc_order.Push(node);
			}

			// the second dfs will avoid the nodes that are not in the subgraph rooted
			// in the root nodes; this way, the reversed navigator cannot lead us to
			// unexplored regions.
			private bool second_dfs_avoid (object node)
			{
				return ! nodes_in_graph.Contains(node);
			}

			private void create_new_SCC (object node)
			{
				current_scc = new StronglyConnectedComponent(scc_id++);
				all_SCCs.Add(current_scc);
			}

			private void second_dfs_begin_visitor (object node)
			{
				current_scc.nodes.Add(node);
				node2scc.Add(node, current_scc);
			}


			private void put_arcs()
			{
				foreach(object node in nodes_in_graph)
				{
					StronglyConnectedComponent scc = (StronglyConnectedComponent) node2scc[node];
					Debug.Assert(scc != null);
					// add the arcs from scc to successor SCCs
					foreach(object next_node in navigator.NextNodes(node)) 
						if(nodes_in_graph.Contains(next_node))
						{
							StronglyConnectedComponent next = (StronglyConnectedComponent) node2scc[next_node];
							Debug.Assert(next != null);
							scc.next_SCCs.Add(next);
						}
					// add the arcs from scc to predecessor SCCs
					foreach(object prev_node in navigator.PreviousNodes(node))
						if(nodes_in_graph.Contains(prev_node))
						{
							StronglyConnectedComponent prev = (StronglyConnectedComponent) node2scc[prev_node];
							Debug.Assert(prev != null);
							scc.prev_SCCs.Add(prev);
						}
				}

				foreach(StronglyConnectedComponent scc in all_SCCs)
				{
					scc.contains_cycle = scc.next_SCCs.Contains(scc);
					scc.next_SCCs.Remove(scc);
					scc.prev_SCCs.Remove(scc);
				}
			}
		}

	}

}
