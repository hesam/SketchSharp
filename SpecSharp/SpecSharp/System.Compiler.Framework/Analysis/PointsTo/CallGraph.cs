//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Compiler.WPurity;
#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler.Analysis
{
#endif

  /// <summary>
  /// This class is used to compute the set of potentially analyzable methods
  /// I used to filter out the methods in order to limit the interprocedural 
  /// analysis scope
  /// </summary>
  class AnalyzableMethodFinder : StandardVisitor
  {
    private Set<string> analizableMethods = new Set<string>();
    private Set<Method> analyzableMethods = new Set<Method>();
    public CallGraph cg;
    internal PointsToAnalysis ptwe;
    private Node unitUnderAnalysis;
    internal Set<Node> assemblies = new Set<Node>();
    private Method currentMethod = null;

    public AnalyzableMethodFinder(PointsToAnalysis ptwe)
    {
      this.ptwe = ptwe;
      this.unitUnderAnalysis = ptwe.unitUnderAnalysis;
      cg = new CallGraph();
    }

    /// <param name="u">
    /// Must be an assembly (or a compilation unit?)
    /// </param>
    public AnalyzableMethodFinder(Node u)
    {
      this.unitUnderAnalysis = u;
      cg = new CallGraph();
    }

    public override TypeNode VisitTypeNode(TypeNode typeNode)
    {
      //if (WeakPurityAndWriteEffectsAnalysis.classFilter.Length != 0 && !typeNode.FullName.Contains(WeakPurityAndWriteEffectsAnalysis.classFilter))
      if (ptwe != null)
      {
        if (ptwe.moduleFilter.Length != 0
            && !typeNode.GetFullUnmangledNameWithTypeParameters().Contains(ptwe.moduleFilter))
          return typeNode;
      }
      return base.VisitTypeNode(typeNode);
    }

    public override Method VisitMethod(Method method)
    {
      Method m = method;
      if (IsInCurrenModule(m))
      {
        addMethod(m);
        CGGenerator cgGenerator = new CGGenerator(cg, this);
        cgGenerator.Analyze(method);
      }
      currentMethod = method;
      return m;
      //return base.VisitMethod(method);
    }
    // I'm not using this!
    public override StatementList VisitStatementList(StatementList statements)
    {
      //foreach (Statement s in statements)
      //{
      //    if (s is ExpressionStatement)
      //    {
      //        Expression expression = ((ExpressionStatement)s).Expression;

      //        MethodCall call = expression as MethodCall;
      //        Variable receiver;
      //        Method callee;
      //        FunctionPointer fpointer;
      //        if (call != null)
      //        {
      //            MemberBinding mb = (MemberBinding) call.Callee;
      //            // receiver = (Variable) mb.TargetObject;

      //            callee = mb.BoundMember as Method;
      //            if (callee != null)
      //            {
      //                if (IsInterestingCall(callee))
      //                {
      //                    addMethod(callee);
      //                    cg.AddCall(currentMethod, callee, new Label(s, currentMethod),CciHelper.IsVirtual(call));
      //                }
      //            }
      //        }

      //        // Console.Out.WriteLine(s);
      //    }
      //}
      return base.VisitStatementList(statements);
    }

    internal bool IsInterestingCall(Method m)
    {
      bool res = true;
      // DIEGO-CHECK: Why did I remove this..
      //res = res && this.ptwe.IsInCurrenModule(m);

      res = res && this.IsInCurrenModule(m);
      res = res && !m.Name.Name.Equals("set_Item");
      res = res && !m.Name.Name.Equals("get_Item");
      res = res && (m.DeclaringType != null & !m.DeclaringType.FullName.StartsWith("Microsoft.Contracts"));
      Method templateOrMethod = WeakPurityAndWriteEffectsAnalysis.solveMethodTemplate(m);
      res = res && this.ptwe.IsPossibleAnalyzableMethod(templateOrMethod);
      return res;
    }
    
    internal bool IsInCurrenModule(Method m)
    {
      bool res = false;
      Module targetModule = null;
      if (unitUnderAnalysis is Compilation)
      {
        Compilation compilation = (Compilation)unitUnderAnalysis;
        targetModule = compilation.TargetModule;
      }
      if (unitUnderAnalysis is AssemblyNode)
      {
        AssemblyNode assembly = (AssemblyNode)unitUnderAnalysis;
        targetModule = assembly;
      }
      if (targetModule == m.DeclaringType.DeclaringModule)
      {
        res = true;
      }
      //DIEGO-CHECK: 
      if (assemblies.Contains(m.DeclaringType.DeclaringModule))
      {
        res = true;
      }
      else
      { }

      if (ptwe.moduleFilter.Length != 0
              && !m.DeclaringType.FullName.Contains(ptwe.moduleFilter))
      {
        res = false;
      }

      return res;
    }

    public static Set<string> ComputeAnalizableMethods(Node node, PointsToAnalysis ptwe)
    {
      AnalyzableMethodFinder amf = new AnalyzableMethodFinder(ptwe);
      amf.Visit(node);
      return amf.analizableMethods;
    }

    public static CallGraph ComputeAnalyzableMethods(Node node, PointsToAnalysis ptwe)
    {
      Set<AssemblyNode> assemblies = new Set<AssemblyNode>();

      // AnalyzableMethodFinder amf = new AnalyzableMethodFinder(node,);
      AnalyzableMethodFinder amf = new AnalyzableMethodFinder(ptwe);
      if (node is AssemblyNode)
      {
        AssemblyNode assemblyNode = (AssemblyNode)node;
        amf.assemblies.Add(node);
        foreach (AssemblyReference reference in assemblyNode.AssemblyReferences)
        {
          amf.assemblies.Add(reference.Assembly);
        }
      }
      amf.Visit(node);

      /*
      foreach (AssemblyNode an in amf.assemblies)
      {
          Console.Out.WriteLine("*ENTRE*");
          amf.Visit(an);
      }
      */

      // amf.Visit(node);
      if (PointsToAnalysis.verbose)
      {
        Set<CallGraph> cgs;
        cgs = new Set<CallGraph>();
        cgs.Add(amf.cg);
        CallGraph.GenerateDotGraph(Console.Out, cgs);
      }
      return amf.cg;
    }

    public static CallGraph ComputeAnalyzableMethods(Node node)
    {
      Set<AssemblyNode> assemblies = new Set<AssemblyNode>();

      AnalyzableMethodFinder amf = new AnalyzableMethodFinder(node);
      if (node is AssemblyNode)
      {
        AssemblyNode assemblyNode = (AssemblyNode)node;
        amf.assemblies.Add(node);
        foreach (AssemblyReference reference in assemblyNode.AssemblyReferences)
        {
          amf.assemblies.Add(reference.Assembly);
        }
      }
      amf.Visit(node);

      return amf.cg;
    }

    /*
    public static CallGraph ComputeAnalyzableMethods(Node node,  out Set<CallGraph> cgs)
    {
        AnalyzableMethodFinder amf = new AnalyzableMethodFinder(node);
        amf.Visit(node);
        cgs = new Set<CallGraph>();

        cgs.Add(amf.cg);

        if (WeakPurityAndWriteEffectsAnalysis.verbose)
            CallGraph.GenerateDotGraph(Console.Out, cgs);

        // cgs = amf.cg.ConnectedComponents();
        // CallGraph.GenerateDotGraph(Console.Out, cgs);
        return amf.cg;
    }
    */
    private void addMethod(Method m)
    {
      String mName = m.GetFullUnmangledNameWithTypeParameters();
      if (ptwe == null || (ptwe.classFilter.Length == 0 ||
           mName.StartsWith(ptwe.classFilter)))
      {
        analizableMethods.Add(mName);
        analyzableMethods.Add(m);
      }
      //Console.Out.WriteLine("Added: {0}",mName);
    }


  }

  /// <summary>
  /// Instruction visitor used to build the partial call graph
  /// Every method call is registered 
  /// </summary>
  class MyCGGenerator : InstructionVisitor
  {
    CallGraph cg;
    Method current;
    PointsToAnalysis currentPTWE;

    AnalyzableMethodFinder methodsFinder;

    public MyCGGenerator(CallGraph cg, Method m, PointsToAnalysis currentPTWE)
    {
      this.current = m;
      this.cg = cg;
      this.currentPTWE = currentPTWE;
    }

    public MyCGGenerator(CallGraph cg, Method m, AnalyzableMethodFinder methodsFinder)
    {
      this.current = m;
      this.cg = cg;
      this.currentPTWE = methodsFinder.ptwe;
      this.methodsFinder = methodsFinder;
    }


    protected override object DefaultVisit(Statement stat, object arg)
    {
      // throw new Exception("The method or operation is not implemented.");
      return arg;
    }
    protected override object VisitCall(Variable dest, Variable receiver, Method callee, ExpressionList arguments, bool virtcall, Statement stat, object arg)
    {
      //Console.Out.WriteLine("{0} -> {1}", current.FullName, callee.FullName);
      if (IsInterestingCall(callee))
        cg.AddCall(current, callee, new Label(stat, current), virtcall);
      return arg;
    }
    protected override object VisitConstrainedCall(Variable dest, Variable receiver, Method callee, ExpressionList arguments, TypeNode constraint, Statement stat, object arg)
    {
      //Console.Out.WriteLine("{0} -> {1}", current.FullName, callee.FullName);
      // DIEGO-CHECK: I don't remember how this kind of call works...
      if (IsInterestingCall(callee))
        cg.AddCall(current, callee, new Label(stat, current), false);
      return arg;
    }
    private bool IsInterestingCall(Method m)
    {
      bool res = true;
      bool isInModule = methodsFinder != null ? methodsFinder.IsInCurrenModule(m) : currentPTWE.IsInCurrenModule(m);

      res = res && isInModule;
      res = res && !m.Name.Name.Equals("set_Item");
      res = res && !m.Name.Name.Equals("get_Item");
      res = res && (m.DeclaringType != null & !m.DeclaringType.FullName.StartsWith("Microsoft.Contracts"));
      Method templateOrMethod = WeakPurityAndWriteEffectsAnalysis.solveMethodTemplate(m);
      res = res && currentPTWE.IsPossibleAnalyzableMethod(templateOrMethod);
      return res;
    }

  }

  /// <summary>
  /// Small dataflow traversal. Just for building the 
  /// partial callgraph.
  /// Not exception support
  /// </summary>
  class CGGenerator : ForwardDataFlowAnalysis
  {
    class MyState : IDataFlowState
    {
      #region IDataFlowState Members

      void IDataFlowState.Dump()
      {

      }

      #endregion
    }
    CallGraph cg;
    MyCGGenerator iVisitor;
    PointsToAnalysis ptwe;
    AnalyzableMethodFinder amf;
    public CGGenerator(CallGraph cg, PointsToAnalysis ptwe)
    {
      this.cg = cg;
      this.ptwe = ptwe;

    }

    public CGGenerator(CallGraph cg, AnalyzableMethodFinder amf)
    {
      this.cg = cg;
      this.ptwe = amf.ptwe;
      this.amf = amf;

    }
    protected override IDataFlowState Merge(CfgBlock previous, CfgBlock joinPoint, IDataFlowState atMerge,
        IDataFlowState incoming, out bool resultDiffersFromPreviousMerge, out bool mergeIsPrecise)
    {
      resultDiffersFromPreviousMerge = false;
      mergeIsPrecise = true;
      if (atMerge == null)
        resultDiffersFromPreviousMerge = true;
      atMerge = incoming;
      return atMerge;
    }

    protected override void SplitExceptions(CfgBlock handler, ref IDataFlowState currentHandlerState,
        out IDataFlowState nextHandlerState)
    {
      nextHandlerState = null;
    }

    protected override IDataFlowState VisitBlock(CfgBlock block, IDataFlowState stateOnEntry)
    {
      IDataFlowState resultState = stateOnEntry;

      IDataFlowState newState = new MyState();

      if (block.ExceptionHandler != null)
        this.PushExceptionState(block, newState);

      resultState = base.VisitBlock(block, newState);

      if (block.UniqueId == cfg.NormalExit.UniqueId)
      {

      }

      if (block.UniqueId == cfg.ExceptionExit.UniqueId)
      {

      }

      return resultState;
    }

    /// <summary>
    /// Visit the statement. It calls the instruction visitor to perform the transfer function
    /// </summary>
    /// <param name="block"></param>
    /// <param name="statement"></param>
    /// <param name="dfstate"></param>
    /// <returns></returns>
    protected override IDataFlowState VisitStatement(CfgBlock block, Statement statement, IDataFlowState dfstate)
    {

      IDataFlowState result = (IDataFlowState)(iVisitor.Visit(statement, dfstate));

      return result;
    }
    public void Analyze(Method method)
    {
      ControlFlowGraph cfg = this.ptwe.GetCFG(method);
      cg.AddMethod(method);
      if (cfg != null)
      {
        if (amf != null)
          iVisitor = new MyCGGenerator(cg, method, amf);
        else
          iVisitor = new MyCGGenerator(cg, method, this.ptwe);

        Run(cfg, new MyState());
      }
    }
  }

  public class CallGraph
  {
    // temporary public
    public Set<CGNode> cgNodes;
    Set<Method> methods;
    public CGEdges calls;

    private Set<CallGraph> scc;

    #region Constructors
    public CallGraph()
    {
      cgNodes = new Set<CGNode>();
      methods = new Set<Method>();
      calls = new CGEdges();
    }
    #endregion

    #region Properties
    public Set<Method> Methods
    {
      get { return methods; }
    }
    Set<CallGraph> SCC
    {
      get
      {
        if (scc == null)
          scc = ComputeSCC();
        return scc;
      }
    }

    #endregion

    #region Modifiers
    public void AddMethod(Method m1)
    {
      CGNode m1n = new CGNode(m1);
      cgNodes.Add(m1n);
      methods.Add(m1);
    }
    public void AddCall(Method m1, Method m2, Label lb, bool isV)
    {
      CGNode m1n = new CGNode(m1);
      CGNode m2n = new CGNode(m2);
      AddNode(m1n);
      AddNode(m2n);
      calls.AddEdge(m1n, m2n, lb, isV);
      Method template = WeakPurityAndWriteEffectsAnalysis.solveMethodTemplate(m2);
      if (!template.Equals(m2))
      {
        CGNode m2T = new CGNode(template);
        AddNode(m2T);
        calls.AddEdge(m1n, m2T, lb, isV);
      }

    }
    private void AddNode(CGNode mn)
    {
      cgNodes.Add(mn);
      methods.Add(mn.Method);
    }

    private void AddEdge(CGEdge e)
    {
      AddNode(e.Src);
      AddNode(e.Dst);
      calls.AddEdge(e);
    }
    #endregion

    #region Information about callers and callees
    public Set<Method> Callees(Method m1)
    {
      Set<Method> res = new Set<Method>();
      foreach (CGNode nm in calls.Successors(new CGNode(m1)))
      {
        res.Add(nm.Method);
      }
      return res;
    }
    public Dictionary<Label, Set<Method>> Calls(Method m1)
    {
      Dictionary<Label, Set<Method>> res = new Dictionary<Label, Set<Method>>();
      foreach (CGEdge cge in calls.EdgesFrom(new CGNode(m1)))
      {
        Label lb = cge.Label;
        Set<Method> ms = null;
        if (!res.ContainsKey(lb))
        {
          ms = new Set<Method>();
          res[lb] = ms;
        }
        ms = res[lb];
        ms.Add(cge.Dst.Method);
      }
      return res;
    }

    public Set<Method> Callers(Method m1)
    {
      Set<Method> res = new Set<Method>();
      foreach (CGNode nm in calls.Predecessors(new CGNode(m1)))
      {
        res.Add(nm.Method);
      }
      return res;
    }
    #endregion

    #region CallGraph Traversal (BFS)
    public List<Method> BFSTraversal()
    {
      Set<CGNode> roots = Roots();
      if (roots.Count == 0 && cgNodes.Count > 0)
      {
        roots.Add(cgNodes.PickAnElement());
      }
      return BFS(roots);
    }
    public List<Method> BFS(Method m)
    {
      Set<CGNode> roots = new Set<CGNode>();
      roots.Add(new CGNode(m));
      return BFS(roots);
    }
    public Set<CGNode> NodesOutgoingSCC(CallGraph scc)
    {
      Set<CGNode> nodes = new Set<CGNode>();
      foreach (CGNode n in scc.cgNodes)
      {
        Set<CGNode> adj = this.calls.Successors(n);
        foreach (CGNode succ in adj)
        {
          if (scc.cgNodes.Contains(succ))
            nodes.Add(succ);
        }
      }
      return nodes;
    }

    public List<Method> TopologicalSort()
    {
      Set<CallGraph> sccs = this.SCC;
      Set<Method> visited = new Set<Method>();
      Set<CGNode> roots = Roots();
      if (roots.Count == 0)
        roots.Add(cgNodes.PickAnElement());
      // Add one representative for each SCC
      foreach (CallGraph scc in sccs)
      {
        roots.Add(scc.cgNodes.PickAnElement());
      }

      //CGNode m = this.cgNodes.PickOne();
      List<Method> order = new List<Method>();
      foreach (CGNode m in roots)
      {
        if (!visited.Contains(m.Method))
          //order.AddRange(DFS(m, sccs, visited));
          //order.InsertRange(0, DFS(m, sccs, visited));
          DFS(m, visited, order);
      }

      return order;
    }

    public/* List<Method>*/ void DFS(CGNode m, Set<Method> visited, List<Method> res)
    {
      //List<Method> res = new List<Method>();
      //res.Add(m.Method);
      //res.Insert(0, m.Method);
      visited.Add(m.Method);
      Set<CGNode> adj = null;
      CallGraph cg = this.GetSCC(m.Method);
      if (cg != null)
      {
        adj = NodesOutgoingSCC(cg);
      }
      else
      {
        adj = this.calls.Successors(m);
      }
      foreach (CGNode n in adj)
      {
        if (!visited.Contains(n.Method))
        {
          DFS(n, visited, res);
          //List<Method> order = DFS(n, sccs, visited);
          //res.InsertRange(0,order);
          //res.AddRange(order);
        }
      }
      res.Insert(0, m.Method);
      //return res;
    }
    public bool BellongsToSCC(Method m, Set<CallGraph> sccs)
    {
      bool res = false;
      foreach (CallGraph scc in sccs)
      {
        if (scc.Methods.Contains(m))
          return true;
      }
      return res;
    }

    public List<Method> GETSCCMethods(Method m)
    {
      Set<CallGraph> cgs = this.SCC;
      List<Method> res = new List<Method>();
      res.Add(m);
      foreach (CallGraph cg in cgs)
      {
        if (cg.Methods.Contains(m))
          return cg.BFSTraversal();
      }
      return res;

    }

    public CallGraph GetSCC(Method m)
    {
      Set<CallGraph> sccs = this.SCC;
      CallGraph res = null;
      foreach (CallGraph scc in sccs)
      {
        if (scc.Methods.Contains(m))
          return scc;
      }
      return res;
    }

    private List<Method> BFS(Set<CGNode> roots)
    {
      List<Method> res = new List<Method>();
      Set<CGNode> visited = new Set<CGNode>();
      List<CGNode> qeue = new List<CGNode>();
      qeue.AddRange(roots);
      while (qeue.Count > 0)
      {
        CGNode mn = qeue[0];
        visited.Add(mn);
        if (!res.Contains(mn.Method))
          res.Add(mn.Method);

        qeue.RemoveAt(0);
        //foreach(CGNode mn in Callees(mn.Method))
        foreach (CGNode adj in calls.Successors(mn))
        {
          if (!visited.Contains(adj) && !qeue.Contains(adj))
          {
            qeue.Add(adj);
          }

        }
      }
      return res;
    }
    public Set<CallGraph> ComputeSCC()
    {
      Dictionary<int, CGNode> nodesOrder;
      BFSforSCC(out nodesOrder);
      Set<CallGraph> cgs = BFSTranspose(nodesOrder);
      //foreach (CallGraph cg in cgs)
      //{
      //    GenerateDotGraph(Console.Out, cg);
      //    Console.Out.WriteLine("---------------------");
      //}
      return cgs;

    }
    private Set<CallGraph> BFSTranspose(Dictionary<int, CGNode> nodesOrder)
    {
      Set<CallGraph> cgs = new Set<CallGraph>();
      List<CGNode> qeue = new List<CGNode>();
      Set<CGNode> visited = new Set<CGNode>();
      int cNodes = nodesOrder.Count;
      for (int i = cNodes - 1; i >= 0; i--)
      {
        CGNode n = nodesOrder[i];
        CallGraph cg = new CallGraph();
        if (!visited.Contains(n))
        {
          qeue.Add(n);
          visited.Add(n);
          while (qeue.Count > 0)
          {

            CGNode mn = qeue[0];
            visited.Add(mn);
            cg.AddNode(mn);

            qeue.RemoveAt(0);
            foreach (CGEdge e in calls.EdgesTo(mn))
            {
              if (!visited.Contains(e.Src))
              {
                visited.Add(e.Src);
                // DIEGO-CHECK: I dont remember why I inverted this
                cg.AddEdge(new CGEdge(e.Dst, e.Label, e.Src, e.IsVirtual));
                qeue.Add(e.Src);
              }
            }
          }
        }
        if (cg.cgNodes.Count > 1)
          cgs.Add(cg);
      }
      return cgs;
    }
    private void BFSforSCC(out Dictionary<int, CGNode> nodesOrder)
    {
      nodesOrder = new Dictionary<int, CGNode>();
      int order = 0;
      List<CGNode> qeue = new List<CGNode>();
      Set<CGNode> visited = new Set<CGNode>();
      foreach (CGNode n in this.cgNodes)
      {
        if (!visited.Contains(n))
        {
          qeue.Add(n);
          visited.Add(n);
          nodesOrder[order] = n;
          order++;
          while (qeue.Count > 0)
          {

            CGNode mn = qeue[0];
            visited.Add(mn);
            qeue.RemoveAt(0);
            foreach (CGNode adj in calls.Successors(mn))
            {
              if (!visited.Contains(adj))
              {
                visited.Add(adj);
                qeue.Add(adj);
              }
            }
          }
        }
      }
    }

    private Set<CGNode> Roots()
    {
      Set<CGNode> roots = new Set<CGNode>();
      foreach (CGNode mn in cgNodes)
      {
        if (calls.Predecessors(mn).Count == 0)
          roots.Add(mn);
      }
      return roots;
    }
    #endregion




    #region DotGraph Generation
    public static void GenerateDotGraph(System.IO.TextWriter output, Set<CallGraph> cgs)
    {
      output.WriteLine("digraph " + "cg" + " {");
      foreach (CallGraph cg in cgs)
      {
        GenerateDotGraph(output, cg);
      }
      output.WriteLine("}");

    }


    public static void GenerateDotGraph(System.IO.TextWriter output, CallGraph cg)
    {
      foreach (CGNode mn in cg.cgNodes)
      {
        output.WriteLine("\"{0}\" [shape = box]", mn.ToString());
      }
      foreach (CGEdge e in cg.calls)
      {
        EdgeToDot(output, e, true);
      }
      output.WriteLine();
    }

    private static void EdgeToDot(System.IO.TextWriter output, CGEdge e, bool inside)
    {
      string edgeStyle = "style = dotted";
      if (inside)
        edgeStyle = "style = solid";

      edgeStyle += ", color = black";

      output.WriteLine("\"{0}\" -> \"{2}\" [label = \"{1}\",{3}]", e.Src, e.Label.ToStringRed(), e.Dst, edgeStyle);
    }
    #endregion
  }

  public class CGNode
  {
    Method m;

    #region Constructors
    public CGNode(Method m)
    {
      this.m = m;
    }
    #endregion

    #region Properties
    public Method Method
    {
      get { return m; }
    }
    #endregion

    #region Equals, Hash, ToString
    public override bool Equals(object obj)
    {
      CGNode cgm = obj as CGNode;
      return cgm != null && m.Equals(cgm.m);
    }
    public override int GetHashCode()
    {
      return 1 + m.GetHashCode();
    }
    public override string ToString()
    {
      return m.FullName;
    }
    #endregion
  }

  public class CGEdge
  {
    CGNode m1, m2;
    Label lb;
    bool isVirtual;

    #region Constructors
    public CGEdge(CGNode m1, Label lb, CGNode m2, bool isVirtual)
    {
      this.m1 = m1; this.m2 = m2; this.lb = lb;
      this.isVirtual = isVirtual;
    }
    #endregion

    #region Properties
    public CGNode Src
    {
      get { return m1; }
    }
    public CGNode Dst
    {
      get { return m2; }
    }
    public Label Label
    {
      get { return lb; }
    }
    public bool IsVirtual
    {
      get { return isVirtual; }
    }
    #endregion

    #region Equals, Hash, ToStirng
    public override bool Equals(object obj)
    {
      CGEdge e = obj as CGEdge;
      return e != null && lb.Equals(e.lb)
          && m1.Equals(e.m1) && m2.Equals(e.m2)
          && isVirtual == e.isVirtual;
    }
    public override int GetHashCode()
    {
      return m1.GetHashCode() + m2.GetHashCode()
          + lb.GetHashCode() + isVirtual.GetHashCode();
    }
    #endregion
  }

  public class CGEdges : IEnumerable
  {
    #region Attributes
    Set<CGEdge> edges;

    // Mappings for faster access
    Dictionary<CGNode, Set<CGNode>> adjacentsForward;
    Dictionary<CGNode, Set<CGNode>> adjacentsBackward;
    Dictionary<CGNode, Set<CGEdge>> edgesForward;
    Dictionary<CGNode, Set<CGEdge>> edgesBackward;
    #endregion

    #region Constructors
    public CGEdges()
    {
      edges = new Set<CGEdge>();
      adjacentsForward = new Dictionary<CGNode, Set<CGNode>>();
      adjacentsBackward = new Dictionary<CGNode, Set<CGNode>>();
      edgesForward = new Dictionary<CGNode, Set<CGEdge>>();
      edgesBackward = new Dictionary<CGNode, Set<CGEdge>>();
    }
    #endregion

    #region Modifiers
    public void AddEdge(CGNode m1, CGNode m2, Label lb, bool isV)
    {
      CGEdge e = new CGEdge(m1, lb, m2, isV);
      AddEdge(e);
    }
    #endregion

    #region Observers
    public Set<CGNode> Successors(CGNode mn)
    {
      return adjacents(mn, true);
    }
    public Set<CGNode> Predecessors(CGNode mn)
    {
      return adjacents(mn, false);
    }
    public Set<CGEdge> EdgesFrom(CGNode mn)
    {
      return edgesNode(mn, true);
    }
    public Set<CGEdge> EdgesTo(CGNode mn)
    {
      return edgesNode(mn, false);
    }
    #endregion

    #region auxiliar methods

    private void addToMap<T, NS, N>(Dictionary<T, NS> map, T key, N n)
        where NS : Collections.Generic.Set<N>, new()
    {
      NS nodes;
      if (map.ContainsKey(key))
      {
        nodes = map[key];
      }
      else
      {
        nodes = new NS();
        map[key] = nodes;
      }
      nodes.Add(n);
    }

    internal void AddEdge(CGEdge e)
    {
      addToMap(adjacentsForward, e.Src, e.Dst);
      addToMap(adjacentsBackward, e.Dst, e.Src);
      addToMap(edgesForward, e.Src, e);
      addToMap(edgesBackward, e.Dst, e);
      this.edges.Add(e);
    }
    private void removeFromToMap<T, NS, N>(Dictionary<T, NS> map, T key, N n)
        where NS : Collections.Generic.Set<N>
    {
      if (map.ContainsKey(key))
      {
        if (map[key].Contains(n))
          map[key].Remove(n);
      }
    }
    internal void RemoveEdge(CGEdge e)
    {
      removeFromToMap(adjacentsForward, e.Src, e.Dst);
      removeFromToMap(adjacentsBackward, e.Dst, e.Src);
      removeFromToMap(edgesForward, e.Src, e);
      removeFromToMap(edgesBackward, e.Dst, e);
      this.edges.Remove(e);
    }

    private Set<CGNode> adjacents(CGNode mn, bool forward)
    {
      Set<CGNode> res = Set<CGNode>.Empty;
      if (forward)
      {
        if (adjacentsForward.ContainsKey(mn))
          return adjacentsForward[mn];
      }
      else
      {
        if (adjacentsBackward.ContainsKey(mn))
          return adjacentsBackward[mn];
      }
      return res;
    }
    private Set<CGEdge> edgesNode(CGNode mn, bool forward)
    {
      Set<CGEdge> res = Set<CGEdge>.Empty;
      if (forward)
      {
        if (edgesForward.ContainsKey(mn))
          return edgesForward[mn];
      }
      else
      {
        if (edgesBackward.ContainsKey(mn))
          return edgesBackward[mn];
      }
      return res;
    }
    #endregion

    #region IEnumerable Members

    IEnumerator IEnumerable.GetEnumerator()
    {
      return edges.GetEnumerator();
    }

    #endregion
  }
}
