//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;
using AbstractValue = System.Compiler.MathematicalLattice.Element;
using System.IO;
using System.Compiler.Analysis.PointsTo;
using System.Collections;
using System.Compiler.Diagnostics;
using System.Compiler.Analysis;
#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler
{
#endif

  /// <summary>
  /// Computes the mapping that binds caller's nodes with the callee parameters
  /// It helps computes the pointsToGraph after the call 
  /// </summary>
  public class InterProcMapping
  {
    #region Attributes
    Dictionary<IPTAnalysisNode, Nodes> mapping = new Dictionary<IPTAnalysisNode, Nodes>();
    PTGraph callerPTG, calleePTG;
    // PointsToState callerState, calleeState;
    // PointsToAndWriteEffects ptWEcaller, ptWEcallee;

    Variable callerThisRef = null;
    ExpressionList arguments;

    internal Set<LNode> removedLoadNodes = new Set<LNode>();
    #endregion

    #region Constructors
    InterProcMapping(PTGraph caller, PTGraph callee, Variable thisRef, ExpressionList arguments)
    {
      this.calleePTG = callee;
      this.callerPTG = caller;
      this.callerThisRef = thisRef;
      this.arguments = arguments;
    }

    InterProcMapping(InterProcMapping ipm)
    {
      this.calleePTG = ipm.calleePTG;
      this.callerPTG = ipm.callerPTG;
      this.callerThisRef = ipm.callerThisRef;
      this.arguments = ipm.arguments;
      this.mapping = new Dictionary<IPTAnalysisNode, Nodes>(ipm.mapping);
    }
    #endregion

    #region Binding Mapping modifiers and querys
    /// <summary>
    /// node2 \in mapping[node1]
    /// </summary>
    /// <param name="node1"></param>
    /// <param name="node2"></param>
    public void Relate(IPTAnalysisNode node1, IPTAnalysisNode node2)
    {
      Nodes ns = Nodes.Empty;
      if (!mapping.ContainsKey(node1))
        mapping[node1] = ns;
      else
        ns = mapping[node1];

      // For omega nodes we must add all objects reachable from the caller's node
      // and set load node (and parameters) as omega nodes.
      if (node1.IsOmega || node1.IsOmegaLoad || node1.IsOmegaConfined)
      {
          Nodes reachRef = node1.IsOmegaConfined ? this.callerPTG.getReachRefsOwned(node2) : this.callerPTG.getReachRefs(node2);
          foreach (IPTAnalysisNode nr in reachRef)
          {
              if (nr.IsLoad)
              {
                  ILoadNode iln = (ILoadNode)nr;
                  iln.SetOmegaLoadNode();
                  if(node1.IsOmegaConfined)
                      iln.SetOmegaConfinedLoadNode();
              }
              ns.Add(nr);
          }
      }
      else
      {
          ns.Add(node2);
      }
    }

    /// <summary>
    /// sNodes \in mapping[node1]
    /// </summary>
    /// <param name="node1"></param>
    /// <param name="node2"></param>
    public void Relate(IPTAnalysisNode node1, Nodes sNodes)
    {
        /*
      Nodes ns = Nodes.Empty;
      if (!mapping.ContainsKey(node1))
        mapping[node1] = ns;
      else
        ns = mapping[node1];

      ns.AddRange(sNodes);
         */
        foreach (IPTAnalysisNode n2 in sNodes)
            Relate(node1, n2);
    }
    public void Relate(Nodes ns1, Nodes ns2)
    {
      foreach (IPTAnalysisNode n1 in ns1)
        Relate(n1, ns2);
    }

    /// <summary>
    /// LV[args] \in mapping[n]
    /// </summary>
    /// <param name="n"></param>
    /// <param name="arg"></param>
    public void RelateNodeWithCallerVar(Variable p, Expression arg)
    {
      if (!arg.Type.IsPrimitive)
      {
        Variable v = arg as Variable;
        //PTAnalysisNode addrP = calleePTG.GetAddress(p);
        //PTAnalysisNode addrV = callerPTG.GetAddress(v);
        //Relate(addrP, addrV);
        Relate(calleePTG.GetLocations(p), callerPTG.GetLocations(v));
        Relate(calleePTG.GetValuesIfEmptyLoadNode(p, callerPTG.MethodLabel),
            callerPTG.GetValuesIfEmptyLoadNode(v, callerPTG.MethodLabel));


        /*
        Nodes vNodes = calleePTG.Values(addrP);
        Nodes argNodes = callerPTG.Values(addrV);
        foreach (PTAnalysisNode vn in vNodes)
        {
            Relate(vn, argNodes);
        }
        */
      }
    }
    /// <summary>
    /// return mapping[n]
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public Nodes Related(IPTAnalysisNode n)
    {
      Nodes res = Nodes.Empty;
      if (mapping.ContainsKey(n))
        res.AddRange(mapping[n]);
      return res;
    }

    /// <summary>
    /// return mapping[n] U ({n} - PNode)
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    public Nodes RelatedExtended(IPTAnalysisNode n)
    {
      Nodes res = new Nodes(Related(n));
      // if (!(n.IsParameterNode) && (!n.IsVariableReference) && !IsRemovedLoadNode(n))
      // VariableReference includes ParameterNodes
      if ((!n.IsVariableReference) && !IsRemovedLoadNode(n))
      {
        if (res.Count != 0 && n.IsParameterValueLNode &&
            n.Label.Method.Equals(calleePTG.Method))
        {
        }
        else
          res.Add(n);
      }
      return res;
    }
    public Nodes RelatedExtended(Nodes ns)
    {
      Nodes res = Nodes.Empty;
      foreach (IPTAnalysisNode n in ns)
        res.AddRange(RelatedExtended(n));
      return res;
    }
    #endregion

    #region Fixpoint Algorimth to Build the Mapping
    /// <summary>
    /// Step 1: bind parameters and arguments
    /// </summary>
    void RelateParams()
    {
        
      if (calleePTG.ThisParameterNode != null)
      {
          RelateNodeWithCallerVar(calleePTG.Method.ThisParameter, callerThisRef);
          
          /* DIEGO: Question: Who is self when a delegate is invoked?
          if (!PTGraph.IsDelegateType(calleePTG.Method.ThisParameter.Type))
          {
              RelateNodeWithCallerVar(calleePTG.Method.ThisParameter, callerThisRef);
          }
          else
          {
              if(callerPTG.ThisParameterNode!=null)
                  RelateNodeWithCallerVar(calleePTG.Method.ThisParameter, callerPTG.Method.ThisParameter);
          }
          */
      }
      if (calleePTG.Method.Parameters != null)
      {
        for (int i = 0; i < calleePTG.Method.Parameters.Count; i++)
        {
          Parameter p = calleePTG.Method.Parameters[i];
          Expression arg = arguments[i];
          RelateNodeWithCallerVar(p, arg);
        }
      }

    }

    /// <summary>
    /// Step 2
    /// Match outsides egdes from the callee (reads) with inside egdes from the caller (writes).
    /// Handle cases when callee read data is created by the caller.
    /// </summary>
    void MatchOutsideEdges()
    {
      foreach (Edge oe in calleePTG.O)
      {
        IPTAnalysisNode src = oe.Src;
        Nodes ns = Related(src);
        if (ns.Contains(NullNode.nullNode))
        {
          ns.Remove(NullNode.nullNode);
        }
        foreach (IPTAnalysisNode n in ns)
        {
          //Nodes fAddr = callerPTG.GetFieldAddress(n, oe.Field);
          //Relate(oe.Dst, callerPTG.Values(fAddr));
          Nodes adj = callerPTG.I.Adjacents(n, oe.Field, true);
          Relate(oe.Dst, adj);
        }
      }
    }

    /// <summary>
    /// Step 3: Match Outside Edges with Insides Egdes in Callee using resolved aliasing from calling context
    /// </summary>
    void MatchOutsideWithInsideInCallee()
    {
      foreach (Edge oe in calleePTG.O)
      {
        IPTAnalysisNode n1 = oe.Src;
        IPTAnalysisNode n2 = oe.Dst;

        Nodes ns = Related(n1);

        Set<Edge> iedges = calleePTG.I.EdgesFromField(oe.Field);
        foreach (Edge ie in iedges)
        {
          Nodes ns1 = Nodes.Empty;
          ns1.Add(n1);
          IPTAnalysisNode n3 = ie.Src;
          IPTAnalysisNode n4 = ie.Dst;

          Nodes ns3 = Nodes.Empty;
          ns3.Add(n3);
          ns1.AddRange(Related(n1));
          ns3.AddRange(Related(n3));
          //if (ns1.Intersection(ns3).Count != 0)
          if (!ns1.Intersection(ns3).IsEmpty)
          {
            if (!n1.Equals(n3) || n1.IsLoad)
            {
              Nodes ns4notPNode = new Nodes(Related(n4));
              if (!(n4 is PNode))
                ns4notPNode.Add(n4);
              Relate(n2, ns4notPNode);
            }
          }
        }
        foreach (IPTAnalysisNode n in ns)
        {
          Nodes adj = callerPTG.I.Adjacents(n, oe.Field, true);
          Relate(oe.Dst, adj);
        }
      }
    }

    /// <summary>
    /// Compute the interProc mapping between callee and caller.
    /// This is a fixpoint of steps 1,2 and 3
    /// </summary>
    /// <param name="caller"></param>
    /// <param name="callee"></param>
    /// <param name="thisRef"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    private static InterProcMapping ComputeInterMapping(PTGraph callerPTG, PTGraph calleePTG,
        Variable thisRef, ExpressionList arguments, Variable vr)
    {
      /*
      // Be sure that every parameter has its value or load node
      foreach (Parameter p in callerPTG.ParameterMap.Keys)
      {
          callerPTG.GetValuesIfEmptyLoadNode(p, callerPTG.MethodLabel);
      }
      */
      InterProcMapping ipm = new InterProcMapping(callerPTG, calleePTG, thisRef, arguments);
      ipm.RelateParams();

      InterProcMapping oldImp = new InterProcMapping(ipm);


      do
      {
        oldImp = new InterProcMapping(ipm);
        ipm.MatchOutsideEdges();
        ipm.MatchOutsideWithInsideInCallee();

      } while (change(oldImp, ipm));

      return ipm;
    }

    private static bool change(InterProcMapping ipm1, InterProcMapping ipm2)
    {
      int count1 = 0;
      int count2 = 0;

      foreach (IPTAnalysisNode n in ipm1.mapping.Keys)
      {
        count1 += ipm1.mapping[n].Count;
      }
      foreach (IPTAnalysisNode n in ipm2.mapping.Keys)
      {
        count2 += ipm2.mapping[n].Count;
      }

      return (count1 != count2);
    }
    #endregion

    /// <summary>
    /// Computes the PTGraph that binds caller with callee
    /// </summary>
    /// <param name="caller"></param>
    /// <param name="callee"></param>
    /// <param name="thisRef"></param>
    /// <param name="arguments"></param>
    /// <param name="vr"></param>
    /// <returns></returns>
    public static InterProcMapping ComputeInterProgMapping(PTGraph caller, PTGraph callee, Variable thisRef,
        ExpressionList arguments, Variable vr, Label lb)
    {
      PTGraph calleeCopy = callee.Simplify();

      // InterProcMapping ipm = ComputeInterMapping(caller, callee, thisRef, arguments);
      InterProcMapping ipm = ComputeInterMapping(caller, calleeCopy, thisRef, arguments, vr);
      return ipm;

    }
    public PTGraph ComputeInterProgGraph(PTGraph caller, PTGraph callee, Variable thisRef,
        ExpressionList arguments, Variable vr, Label lb)
    {
      // Compute the new graph using the interproc mapping
      PTGraph newPTG = new PTGraph(caller);

      // Compute a new PointsToGraph from the caller by binding it with the callee
      // and simplifying resolved load nodes
      bindCallerWithCalleePTG(newPTG, callee,  vr);
      return newPTG;
    }

    /// <summary>
    /// Bind the caller with the callee and simplify the resulting pointsToGraph
    /// by removing the load nodes that has been resolved (captured objects)
    /// </summary>
    /// <param name="callerPTG"></param>
    /// <param name="calleePTG"></param>
    /// <param name="vr"></param>
    /// <returns></returns>
    private void bindCallerWithCalleePTG(PTGraph callerPTG, PTGraph calleePTG,  Variable vr)
    {



      // Compute Edges
      Edges Inew = new Edges();
      foreach (Edge ie in calleePTG.I)
      {
        foreach (IPTAnalysisNode nu1 in RelatedExtended(ie.Src))
        {
          foreach (IPTAnalysisNode nu2 in RelatedExtended(ie.Dst))
          {
            Inew.AddIEdge(nu1, ie.Field, nu2);
          }
        }
      }
      // Compute Edges
      Edges Onew = new Edges();
      foreach (Edge oe in calleePTG.O)
      {
        foreach (IPTAnalysisNode nu1 in RelatedExtended(oe.Src))
        {
          if (!nu1.IsNull)
            Onew.AddOEdge(nu1, oe.Field, oe.Dst);
        }
      }


      // Compute Escape
      Nodes eNew = new Nodes();
      foreach (IPTAnalysisNode n in calleePTG.E)
      {
        eNew.AddRange(RelatedExtended(n));
      }

      callerPTG.I.AddEdges(Inew);
      callerPTG.O.AddEdges(Onew);
      callerPTG.E.AddRange(eNew);

      /// Assign vr = related(retValue) 


      Nodes argNodes = Nodes.Empty;
      if (callerThisRef != null)
      {
        // argNodes.Add(callerPTG.GetAddress(callerThisRef));
        argNodes.AddRange(callerPTG.GetLocations(callerThisRef));
      }
      if (calleePTG.Method.Parameters != null)
      {
        for (int i = 0; i < calleePTG.Method.Parameters.Count; i++)
        {
          Parameter p = calleePTG.Method.Parameters[i];
          PNode pn = calleePTG.ParameterMap[p];
          if (!pn.IsByValue)
          {
            if (arguments[i] is Variable)
            {
              Variable vArg = (Variable)arguments[i];
              bindRefOrOutParameter(callerPTG, calleePTG, arguments[i] as Variable, p);
            }
          }
          if (arguments[i] is Variable)
          {
            Variable vArg = (Variable)arguments[i];
            // PTAnalysisNode addrArg = callerPTG.GetAddress(vArg);
            // argNodes.Add(addrArg);
            argNodes.AddRange(callerPTG.GetLocations(vArg));
          }

        }
      }


      if (vr != null)
      {
        if (!vr.Type.IsPrimitive && calleePTG.RetValue != null)
        {
          callerPTG.AddVariable(vr, callerPTG.MethodLabel);

          Nodes relatedValues2 = Nodes.Empty;
          // PTAnalysisNode addrRetValue = calleePTG.GetAddress(calleePTG.RetValue);
          // foreach (PTAnalysisNode n2 in calleePTG.Values(addrRetValue))
          foreach (IPTAnalysisNode n2 in calleePTG.Values(calleePTG.GetLocations(calleePTG.RetValue)))
          {
            relatedValues2.AddRange(RelatedExtended(n2));
          }
          callerPTG.Assign(vr, relatedValues2, calleePTG.MethodLabel);

        }
        else
        {
          callerPTG.ForgetVariable(vr);
        }
      }

      // Now we can remove the load nodes that don't escape
      Set<LNode> lNodes = callerPTG.LoadNodes;
      // B = Nodes forward reachable from from Escaping, Parameters, callee Arguments and Global nodes
      Nodes B = callerPTG.ReachableFromParametersReturnAndGlobalsAnd(argNodes);

      foreach (LNode ln in lNodes)
      {
        Set<Edge> IToRemove = new Set<Edge>();
        Set<Edge> OToRemove = new Set<Edge>();
        // If the LoadNode is not reachable or it is captured 
        if (!B.Contains(ln) || (Related(ln).Count > 0))
        // if (!B.Contains(ln) || (ln.IsParameterValueLNode && (Related(ln).Count > 0 || !Related(ln).Contains(ln))))
        //if (!B.Contains(ln) /*&& (ln is LAddrFieldNode  */
        //    /*&& ln.Label.Method.Equals(callerPTG.Method)*/)
        {
          foreach (Edge ie in callerPTG.I.EdgesFromSrc(ln))
            IToRemove.Add(ie);
          foreach (Edge ie in callerPTG.I.EdgesFromDst(ln))
            IToRemove.Add(ie);
          foreach (Edge oe in callerPTG.O.EdgesFromDst(ln))
            OToRemove.Add(oe);
          foreach (Edge oe in callerPTG.O.EdgesFromSrc(ln))
            OToRemove.Add(oe);
          removedLoadNodes.Add(ln);
        }
        callerPTG.I.RemoveEdges(IToRemove);
        callerPTG.O.RemoveEdges(OToRemove);
      }


    }

    /// <summary>
    /// Bind an output or ref result with the argument
    /// </summary>
    /// <param name="calleePTG"></param>
    /// <param name="vr"></param>
    /// <param name="outParameter"></param>
    /// <param name="ipm"></param>
    /// <returns></returns>
    private void bindRefOrOutParameter(PTGraph callerPTG, PTGraph calleePTG, Variable argVar, Variable outParameter)
    {
      if (argVar != null)
      {
        // This is like a StoreInditect of LoadIndirect but using the mapping 
        // *arg = related(*outP) 

        // PTAnalysisNode addrArg = callerPTG.GetAddress(argVar);
        // Nodes callerValues = callerPTG.Values(addrArg);
        Nodes callerValues = callerPTG.Values(callerPTG.GetLocations(argVar));

        // PTAnalysisNode addrOutP = calleePTG.GetAddress(outParameter);
        // Nodes calleeValues = calleePTG.Values(addrOutP);
        Nodes calleeValues = calleePTG.Values(calleePTG.GetLocations(outParameter));

        Nodes relatedValues2 = Nodes.Empty;
        foreach (IPTAnalysisNode n2 in calleePTG.Values(calleeValues))
        {
          relatedValues2.AddRange(RelatedExtended(n2));
        }
        // I am assuming Strong Update. Adding an aditional argument false
        // it become weak update
        callerPTG.Assign(callerValues, relatedValues2, calleePTG.MethodLabel);
      }
    }

    public override string ToString()
    {
      string res = "";
      foreach (IPTAnalysisNode n in mapping.Keys)
      {
        res += string.Format("mu[{0}]={1}\n", n, RelatedExtended(n));
      }
      return res;
    }

    private bool IsRemovedLoadNode(IPTAnalysisNode n)
    {
      return n.IsLoad && removedLoadNodes.Contains((LNode)n);
    }
  }

  #region PointsTo Analysis
  /// <summary>
  /// Represents the pointsTo and the write effects of the current block
  /// That is cState = &lt; PtGraph , WriteEffecs , NonAnalyzableCalls &gt; 
  /// It is a semilattice (bottom , &lt;=)
  /// </summary>
  public class ElementFactory
  {
    protected PointsToAnalysis pta;
    public ElementFactory(PointsToAnalysis pta)
    {
      this.pta = pta;
    }
    public virtual PointsToState NewElement(Method m)
    {
      return new PointsToState(m, pta);
    }
  }
  public class PointsToState : AbstractValue, IDataFlowState
  {
    #region Atttibutes
    #region Semilatice
    /// <summary>
    /// This is the Semilattice
    /// A PointsToGraph, the WriteEffects and the non-analyzed calls
    /// </summary>
    internal PTGraph pointsToGraph;

    // internal Dictionary<Label, Set<Method>> callToNonAnalyzableMethods;
    internal bool isDefault = true;
    internal TypeNode currentException;
    /// <summary>
    ///  Assumptions made when analyzing virtual calls
    /// </summary>
    internal Set<Method> methodAssumptions;
    #endregion
    /// <summary>
    /// Method under analysis (MUA)
    /// </summary>
    protected Method method;
    protected Label methodLabel;

    protected PointsToAnalysis pta;

    public static Variable StaticScope = PTGraph.GlobalScope;
    // Model the set of potencial receivers in the method calls of the MUA
    #endregion

    #region Properites
    public PTGraph PointsToGraph
    {
      get { return pointsToGraph; }
    }

    //public Dictionary<Label, Set<Method>> CallsToNonAnalyzable
    //{
    //    get { return callToNonAnalyzableMethods; }
    //}
    //// Returns true if the method has non analyzable methods
    //public bool HasNonAnalyzableMethods
    //{
    //    get
    //    {
    //        return callToNonAnalyzableMethods.Count != 0;
    //    }
    //}

    // The method under Analysis   
    public Method Method
    {
      get { return method; }
    }
    public Label MethodLabel
    {
      get { return methodLabel; }
    }
    public bool IsDefault
    {
      get { return isDefault; }
    }

    public Nodes Values(Variable v)
    {
      Nodes res = pointsToGraph.Values(v);
      return res;
    }
    public Nodes ValuesIndirect(Variable v)
    {
      Nodes res = pointsToGraph.Values(v);
      res = pointsToGraph.Values(res);
      return res;
    }
    public Nodes Values(Variable v, Field f)
    {
      Nodes res = pointsToGraph.Values(v, f);
      return res;

    }
    public bool IsReachableFromOnlyOutSide(Nodes who1, Nodes from) {
      return IsReachableFrom(who1, from, true, false, true);
    }

    public bool IsReachableFrom(Nodes who1, Nodes from, bool forward, bool useIE, bool useOE) {
      return PointsToGraph.IsReachableFrom(who1, from, forward, useIE, useOE);
    }

    public IPTAnalysisNode Address(Variable v)
    {
      Nodes res = pointsToGraph.GetLocations(v);
      return res.PickAnElement();
    }
    public bool MayAlias(Variable v1, Variable v2)
    {
      return !Values(v1).Intersection(Values(v2)).IsEmpty;
    }


    public Set<Variable> MayPoint(Set<IPTAnalysisNode> ns)
    {
      Set<Variable> res = new Set<Variable>();
      foreach (PT_Variable ptv in Variables)
      {
        if (!Values(ptv.Variable).Intersection(ns).IsEmpty)
        {
          res.Add(ptv.Variable);
        }
      }
      return res;
    }

    public ICollection Parameters
    {
      get { return this.PointsToGraph.ParameterMap.Keys; }
    }
    public ICollection Variables
    {
      get { return this.PointsToGraph.LV.Keys; }
    }


    #endregion

    #region Constructors
    // Constructor
    public PointsToState(Method m, PointsToAnalysis pta)
    {
      this.pta = pta;

      this.pointsToGraph = new PTGraph(m);

      // this.callToNonAnalyzableMethods = new Dictionary<Label, Set<Method>>();
      this.method = m;
      // this.exceptions = new Set<TypeNode>();
      this.currentException = null;

      this.methodAssumptions = new Set<Method>();

      this.isDefault = true;

    }

    // Copy Constructor
    public PointsToState(PointsToState cState)
    {
      this.pta = cState.pta;
      pointsToGraph = new PTGraph(cState.pointsToGraph);

      // callToNonAnalyzableMethods = new Dictionary<Label, Set<Method>>(cState.callToNonAnalyzableMethods);
      method = cState.method;
      methodLabel = cState.methodLabel;
      //exceptions = cState.exceptions;
      currentException = cState.currentException;

      methodAssumptions = cState.methodAssumptions;
      isDefault = cState.isDefault;

    }

    public virtual PointsToState Copy()
    {
      return new PointsToState(this);
    }
    #endregion

    #region SemiLattice Operations (Join, Includes, IsBottom)
    public override bool IsBottom
    {
      get
      {
        return pointsToGraph.IsBottom && isDefault;
      }
    }
    public override bool IsTop
    {
      get { throw new Exception("The method or operation is not implemented."); }
    }

    /// <summary>
    ///  Join two PointsToAndWriteEffects
    /// </summary>
    /// <param name="ptgWe"></param>
    public virtual void Join(PointsToState ptgWe)
    {
      if (ptgWe != null)
      {
        if (pointsToGraph != ptgWe.pointsToGraph)
          pointsToGraph.Join(ptgWe.pointsToGraph);

        //if (callToNonAnalyzableMethods != ptgWe.callToNonAnalyzableMethods)
        //    joinCalledMethod(callToNonAnalyzableMethods, ptgWe.callToNonAnalyzableMethods);

        //if (exceptions != ptgWe.exceptions)
        //    exceptions.AddRange(ptgWe.exceptions);
        currentException = joinExceptions(currentException, ptgWe.currentException);

        if (methodAssumptions != ptgWe.methodAssumptions)
          methodAssumptions.AddRange(ptgWe.methodAssumptions);
        isDefault = isDefault && ptgWe.isDefault;
      }
    }

    private TypeNode joinExceptions(TypeNode excep1, TypeNode excep2)
    {
      TypeNode newCurrentException = excep1;
      if (excep1 != null)
      {
        if (excep2 != null)
          newCurrentException = CciHelper.LeastCommonAncestor(excep1, excep2);
      }
      else
      {
        newCurrentException = excep2;
      }
      return newCurrentException;
    }

    private void joinCalledMethod(Dictionary<Label, Set<Method>> cm1, Dictionary<Label, Set<Method>> cm2)
    {
      foreach (Label lb in cm2.Keys)
      {
        Set<Method> mths2 = cm2[lb];
        Set<Method> mths1 = new Set<Method>();
        if (cm1.ContainsKey(lb))
        {
          mths1.AddRange(cm1[lb]);
        }
        mths1.AddRange(mths2);
        cm1[lb] = mths1;
      }
    }

    /// <summary>
    ///  Inlusion check for two PointsToAndWriEffects
    /// </summary>
    /// <param name="ptgWe"></param>
    public virtual bool Includes(PointsToState cState2)
    {
      bool includes = pointsToGraph.AtMost(cState2.pointsToGraph, this.pointsToGraph);


      //foreach(Label lb in cState2.callToNonAnalyzableMethods.Keys)
      //{
      //    includes = includes && this.callToNonAnalyzableMethods.ContainsKey(lb) 
      //        && this.callToNonAnalyzableMethods[lb].Includes(cState2.callToNonAnalyzableMethods[lb]);
      //}
      return includes;
    }
    #endregion

    #region Transfer Function related methods
    #region Basic Operations (Init, Copy, Forget values,etc)
    /// <summary>
    /// Transfer function for the Method Header
    /// </summary>
    /// <param name="method"></param>
    /// <param name="parameters"></param>
    /// <param name="lb"></param>
    public virtual void InitMethod(Method method, System.Collections.IEnumerable parameters, Label lb)
    {
      methodLabel = lb;
      pointsToGraph.InitMethod(method, parameters, lb);
      isDefault = false;
    }
    /// <summary>
    /// f(v1 = null, cState), operation only over the pointsToGraph 
    /// </summary>
    /// <param name="v1"></param>
    public virtual void ApplyAssignNull(Variable v1)
    {
      pointsToGraph.ApplyAssignNull(v1);
    }
    /// <summary>
    /// Represent when the value of a variable is not longer valid
    /// This means losing information about which nodes v1 points to
    /// </summary>
    /// <param name="v1"></param>
    public virtual void ForgetVariable(Variable v1)
    {
      pointsToGraph.ForgetVariable(v1);
    }
    public virtual void ForgetField(Variable v1, Field f)
    {
      pointsToGraph.ForgetField(v1, f);
    }

    /// <summary>
    /// A more complex copy operation
    /// f(v1 = v2), operation only over the pointsToGraph
    /// </summary>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    /// <param name="lb"></param>
    public virtual void CopyLocVar(Variable v1, Variable v2)
    {
      pointsToGraph.CopyLocVar(v1, v2);
    }
    /// <summary>
    /// f(v1 = v2), operation only over the pointsToGraph
    /// </summary>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    /// <param name="lb"></param>
    public virtual void CopyLocVar(Variable v1, Variable v2, Label lb)
    {
      pointsToGraph.CopyLocVar(v1, v2, lb);
    }
    #endregion

    #region Object allocation
    /// <summary>
    /// f(new Type, cState) , operation only over the pointsToGraph
    /// </summary>
    /// <param name="v"></param>
    /// <param name="lb"></param>
    /// <param name="type"></param>
    public virtual void ApplyNewStatement(Variable v, Label lb, TypeNode type)
    {
      pointsToGraph.NewInsideNode(v, lb, type);
    }
    #endregion

    #region Store statements support
    /// <summary>
    /// f(v1.f = v2), operates over the pointsToGraph and register the writeEffect
    /// </summary>
    /// <param name="v1"></param>
    /// <param name="f"></param>
    /// <param name="v2"></param>
    /// <param name="lb"></param>
    public virtual void ApplyStoreField(Variable v1, Field f, Variable v2, Label lb)
    {
      PointsToGraph.Store(v1, f, v2, lb);
    }

    /// <summary>
    /// f(v1[.] = v2), operates over the pointsToGraph and register the writeEffect
    /// </summary>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    /// <param name="lb"></param>
    public virtual void ApplyStoreElement(Variable v1, Variable v2, Label lb)
    {
      pointsToGraph.Store(v1, PTGraph.arrayField, v2, lb);
    }

    /// <summary>
    /// f(C.f = v2), operates over the pointsToGraph and register the writeEffect
    /// </summary>
    /// <param name="v2"></param>
    /// <param name="f"></param>
    /// <param name="lb"></param>
    public virtual void ApplyStaticStore(Variable v2, Field f, Label lb)
    {
      PointsToGraph.Store(PTGraph.GlobalScope, f, v2, lb);
    }
    #endregion

    public Variable GetVariableByName(string varName) {
      Variable v = null;
      foreach (PT_Variable v1 in this.PointsToGraph.LV.Keys) {
        if(v1.Variable.Name.Name.Equals(varName))
          return v1.Variable;
      }
      return v;
    }
    #region Load Statements support
    /// <summary>
    /// f(v1 = v2.f), operates over the pointsToGraph and register the read effect
    /// </summary>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    /// <param name="f"></param>
    /// <param name="lb"></param>
    public virtual void ApplyLoadField(Variable v1, Variable v2, Field f, Label lb)
    {
      /*
      if(PointsToAnalysis.IsCompilerGenerated(Method) && this.pta.enclosingState.ContainsKey(Method.DeclaringType))
      {
        PointsToState enclosingState = this.pta.enclosingState[Method.DeclaringType];
        Variable vReal = this.GetVariableByName(f.Name.Name);
        if(vReal!=null)
          this.CopyLocVar(v1, vReal, lb);
      }
      */

      pointsToGraph.Load(v1, v2, f, lb);
    }
    /// <summary>
    /// f(v1 = v2[.]), operates over the pointsToGraph and register the read effect 
    /// </summary>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    /// <param name="lb"></param>
    public virtual void ApplyLoadElement(Variable v1, Variable v2, Label lb)
    {
      pointsToGraph.Load(v1, v2, PTGraph.arrayField, lb);
    }

    /// <summary>
    /// f(v1 = C.f), operates over the pointsToGraph 
    /// </summary>
    /// <param name="v1"></param>
    /// <param name="f"></param>
    /// <param name="lb"></param>
    public virtual void ApplyLoadStatic(Variable v1, Field f, Label lb)
    {
      pointsToGraph.Load(v1, PTGraph.GlobalScope, f, lb);
    }


    #endregion
    #region Properties Support (to implement)
    public virtual void LoadProperty(Variable v1, Variable v2, Property property, Label lb)
    {
    }
    public virtual void StoreProperty(Variable v1, Variable v2, Property property, Label lb)
    {
    }

    #endregion



    #region Indirect memory addressing support (Load/Store Indirect, etc.)
    /// <summary>
    ///  f(v1 = &amp;v2)
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="src"></param>
    /// <param name="lb"></param>
    /// 

    public virtual void ApplyLoadAddress(Variable dest, Variable src, Label lb)
    {
      pointsToGraph.LoadAddress(dest, src, lb);
    }

    public virtual void ApplyLoadMethod(Variable dest, Variable src, Method m, Label lb)
    {
      if (src == null)
        pointsToGraph.LoadMethod(dest, PointsToAnalysis.solveMethodTemplate(m) , lb);
      else
        pointsToGraph.LoadInstanceMethod(dest, src, PointsToAnalysis.solveMethodTemplate(m), lb);
    }

    public virtual void ApplyLoadChachedDelegate(Variable v1, Field field, Label lb)
    {
      Variable v2 = PointsToGraph.GetCachedVariable(field.Name.Name, field.Type);
      if (PointsToGraph.Values(v2).Count != 0)
      {
        PointsToGraph.CopyLocVar(v1, v2, lb);
      }
      else
        ApplyAssignNull(v1);
    }
    /// <summary>
    /// f(v1 = *v2), operation only over the pointsToGraph
    /// // I take it as dest = * pointer
    /// </summary>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    /// <param name="lb"></param>
    public virtual void ApplyLoadIndirect(Variable v1, Variable v2, Label lb)
    {
      PointsToGraph.LoadIndirect(v1, v2, lb);
    }

    /// <summary>
    /// A more complex copy operation
    /// f(*v1 = v2, operation only over the pointsToGraph
    /// </summary>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    /// <param name="lb"></param>
    public virtual void ApplyStoreIndirect(Variable v1, Variable v2, Label lb)
    {
      // pointsToGraph.ApplyStoreIndirect(v1, v2, lb);
      PointsToGraph.StoreIndirect(v1, v2, lb);
    }
    /// <summary>
    ///  f(v1 = &amp; v2.f)
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="src"></param>
    /// <param name="lb"></param>
    public virtual void ApplyLoadFieldAddress(Variable dest, Variable src, Field f, Label lb)
    {
      pointsToGraph.LoadFieldAddress(dest, src, f, lb);
    }
    /// <summary>
    ///  f(v1 = &amp; C.f).  
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="src"></param>
    /// <param name="lb"></param>
    public virtual void ApplyLoadStaticFieldAddress(Variable dest, Field f, Label lb)
    {
      pointsToGraph.LoadFieldAddress(dest, PTGraph.GlobalScope, f, lb);
    }
    #endregion

    #region Method Calls Support

  /// <summary>
  /// Get the attributes of a delegate
  /// DIEGO-TODO: Include code to try to get the attributes from the type itself
  /// </summary>
  /// <param name="v"></param>
  /// <returns></returns>
    protected AttributeList GetDelegateAttributes(Variable v) {
      Nodes ns = this.Values(v);
      AttributeList res = new AttributeList();
      
      // Obtain the set of fields or variables that may refer to the 
      // nodes pointed by v
      Set<Variable> vs = this.MayPoint(ns);
      Set<Node> VarOrFields = this.pointsToGraph.GetReferences(ns);
      foreach (Node vof in VarOrFields) {
        if (vof is Parameter) {
          Parameter p = (Parameter)vof;
          foreach (AttributeNode attr in p.Attributes)
            res.Add(attr);
          }
        // If it is a closure with get the attribute from the enclosing method
        if (vof is Field) {
          Field f = (Field)vof;
          if (PointsToAnalysis.IsCompilerGenerated(this.Method)) {
            PointsToState pts = this.pta.EnclosingState(this.Method.DeclaringType);
            if (pts != null) {
              Variable v1 = pts.GetVariableByName(f.Name.Name);
              if (v1 is Parameter) {
                Parameter p = (Parameter)v1;
                foreach (AttributeNode attr in p.Attributes)
                  res.Add(attr);
                }
              }
            }
          }

        }
      return res;
      }
    
    /// <summary>
    /// NOT USED!
    /// Check if a variavle is annotated as pure
    /// DIEGO-TODO: Include code to try to get the attributes from the type itself
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    protected bool CheckPureDelegate(Variable v) {
      Nodes ns = this.Values(v);
      bool res = false;
      Set<Variable> vs = this.MayPoint(ns);
      Set<Node> VarOrFields = this.pointsToGraph.GetReferences(ns);
      foreach (Node vof in VarOrFields) {
        if (vof is Parameter) {
          Parameter p = (Parameter)vof;
          res = res || PointsToAndEffectsAnnotations.IsAssumedPureDelegate(p);
          }
        if (vof is Field) {
          Field f = (Field)vof;
          if (PointsToAnalysis.IsCompilerGenerated(this.Method)) {
            PointsToState pts = this.pta.EnclosingState(this.Method.DeclaringType);
            if (pts != null) {
              Variable v1 = pts.GetVariableByName(f.Name.Name);
              if (v1 is Parameter) {
                Parameter p = (Parameter)v1;
                res = res || PointsToAndEffectsAnnotations.IsAssumedPureDelegate(p);
                }
              }
            }
          }

        }
      return res;
      }

    // I don't use it. It is similiar to Rinard's approach
    public virtual void ApplyNonAnalyzableCallBasic(Variable vr, Method callee, Variable receiver,
        ExpressionList arguments, Label lb)
    {
      if (arguments != null)
      {
        for (int i = 0; i < arguments.Count; i++)
        {
          Expression a = arguments[i];
          if (a is Variable)
          {
            if (callee != null && !callee.Parameters[i].IsOut)
            {
              PointsToGraph.Store(PTGraph.GlobalScope, PTGraph.allFields, a as Variable, lb);
            }
            PointsToGraph.E.AddRange(PointsToGraph.Values(a as Variable));
          }
        }
      }
      if (receiver != null)
      {
        PointsToGraph.E.AddRange(PointsToGraph.Values(receiver));
        PointsToGraph.Store(PTGraph.GlobalScope, PTGraph.allFields, receiver, lb);
      }

    }

    /// <summary>
    /// Creates a summary from scratch using annotations
    /// </summary>
    /// <param name="vr"></param>
    /// <param name="callee"></param>
    /// <param name="receiver"></param>
    /// <param name="arguments"></param>
    /// <param name="lb"></param>
    /// <returns></returns>
  
      public virtual PointsToState CreateSummaryForFakeCallee(Variable vr, Method callee, Variable receiver,
          ExpressionList arguments, Label lb)
      {
          PointsToState calleeState = new PointsToState(callee, this.pta);
          
          // Create a fake PTG for the calle using annotations...
          PTGraph  calleFakePTG = PTGraph.PTGraphFromAnnotations(callee);

          // calleFakePTG.GenerateDotGraph(callee.FullName + ".dot");      
          calleeState.pointsToGraph = calleFakePTG;

          return calleeState;
      }
      /// <summary>
      /// Register effect of the non-analyzable call in the pointsToGraph inferring the information from the callee annotations
      /// Basicale creates a PTG of the callee using annotations and call the standard binding mechanism
      /// </summary>
      /// <param name="vr"></param>
      /// <param name="callee"></param>
      /// <param name="receiver"></param>
      /// <param name="arguments"></param>
      /// <param name="lb"></param>
      /// 
      public virtual void ApplyNonAnalyzableCall(Variable vr, Method callee, Variable receiver,
          ExpressionList arguments, Label lb, out InterProcMapping ipm)
      {

          if (PointsToAnalysis.debug)
          {
              Console.Out.WriteLine("before NON call to {0}", callee.Name);
              pointsToGraph.Dump();
          }

          PointsToState calleeState = CreateSummaryForFakeCallee(vr, callee, receiver, arguments, lb);

        
          ApplyAnalyzableCall(vr, callee, receiver, arguments, calleeState, lb, out ipm);
          

          
          if (PointsToAnalysis.debug)
          {
              Console.Out.WriteLine("after NON call to {0}", callee.Name);
              pointsToGraph.Dump();
          }
        }


      #region All non-analyzable support (To delete after adpating ObjectConsistency Analysis)
      /// <summary>
    /// The old support for non Analyzables. TO DELETE
    /// </summary>
    /// <param name="vr"></param>
    /// <param name="callee"></param>
    /// <param name="receiver"></param>
    /// <param name="arguments"></param>
    /// <param name="lb"></param>
    /// <param name="ipm"></param>
    public virtual void ApplyNonAnalyzableCallOld(Variable vr, Method callee, Variable receiver,
        ExpressionList arguments, Label lb, out InterProcMapping ipm)
    {
      bool isPure = false;
      bool isReturnFresh = false;
      bool isReadingGlobals = true;
      bool isWritingGlobals = true;
      Set<Parameter> freshOutParameters = new Set<Parameter>();
      Set<Parameter> escapingParameters = new Set<Parameter>();
      Set<Parameter> capturedParameters = new Set<Parameter>();
      Set<Variable> outArguments = new Set<Variable>();

      isPure = PointsToAndEffectsAnnotations.IsAssumedPureMethod(callee);
      isReadingGlobals = PointsToAndEffectsAnnotations.IsDeclaredReadingGlobals(callee);
      isWritingGlobals = PointsToAndEffectsAnnotations.IsDeclaredWritingGlobals(callee);
      if (PointsToAnalysis.debug)
      {
        Console.Out.WriteLine("before NON call to {0}", callee.Name);
        pointsToGraph.Dump();
      }
      if (!callee.ReturnType.IsPrimitive && !CciHelper.IsVoid(callee.ReturnType))
      {
        isReturnFresh = PointsToAndEffectsAnnotations.IsDeclaredFresh(callee);
      }
      else
        if (CciHelper.IsConstructor(callee))
          isReturnFresh = true;

      if (!callee.IsStatic && callee.ThisParameter != null)
      {
        bool captured;
        if (PointsToAndEffectsAnnotations.IsDeclaredEscaping(callee.ThisParameter, out captured))
        {
          escapingParameters.Add(callee.ThisParameter);
          if (captured)
            capturedParameters.Add(callee.ThisParameter);
        }
      }
      if (callee.Parameters != null)
      {
        for (int i = 0; i < callee.Parameters.Count; i++)
        {
          Parameter p = callee.Parameters[i];
          if (p.IsOut)
          {
            if (PointsToAndEffectsAnnotations.IsDeclaredFresh(p))
            {
              freshOutParameters.Add(p);
            }
            outArguments.Add(arguments[i] as Variable);
          }
          bool captured;
          if (PointsToAndEffectsAnnotations.IsDeclaredEscaping(p, out captured))
          {
            escapingParameters.Add(p);
            if (captured)
              capturedParameters.Add(p);
          }
        }
      }

      //if (isWritingGlobals)
      //{
      //    addNonAnalyzableMethod(lb, callee);
      //}

     
      if (callee.Name.Name.Equals("Hola") || callee.Name.Name.Equals("GetEnumerator2"))
      {
      }

        PTGraph calleFakePTG = null;
        // Create a fake PTG for the calle using annotations...
        calleFakePTG = PTGraph.PTGraphFromAnnotations(callee);
        // , isPure, isReturnFresh, isWritingGlobals, isReadingGlobals);


          // calleFakePTG.GenerateDotGraph(callee.FullName + ".dot");      

        NonAnalyzableCallBeforeUpdatingPTG(vr, callee, receiver, arguments, lb,
          isPure, isReturnFresh, isWritingGlobals, isReadingGlobals,
          escapingParameters, capturedParameters, freshOutParameters);


          /*InterProcMapping */ 
           ipm = InterProcMapping.ComputeInterProgMapping(this.pointsToGraph, calleFakePTG,
               receiver, arguments, vr, lb);
            

          PTGraph interProcPTG = ipm.ComputeInterProgGraph(this.pointsToGraph, calleFakePTG,
              receiver, arguments, vr, lb);

          pointsToGraph = interProcPTG;

       
      NonAnalyzableCallAfterUpdatingPTG(vr, callee, receiver, arguments, lb,
           isPure, isReturnFresh, isWritingGlobals, isReadingGlobals,
           escapingParameters, capturedParameters, freshOutParameters);
      if (PointsToAnalysis.debug)
      {
        Console.Out.WriteLine("after NON call to {0}", callee.Name);
        pointsToGraph.Dump();
      }
    }
      #endregion

  protected virtual void NonAnalyzableCallBeforeUpdatingPTG(Variable vr,
        Method callee,
        Variable receiver,
        ExpressionList arguments, Label lb,
        bool isPure, bool isReturnFresh,
        bool modifiesGlobal, bool readsGlobal,
        Set<Parameter> escapingParameters,
        Set<Parameter> capturedParameters,
        Set<Parameter> freshParameters)
    {
    }

    protected virtual void NonAnalyzableCallAfterUpdatingPTG(Variable vr,
        Method callee,
        Variable receiver,
        ExpressionList arguments, Label lb,
        bool isPure, bool isReturnFresh,
        bool modifiesGlobal, bool readsGlobal,
        Set<Parameter> escapingParameters,
        Set<Parameter> capturedParameters,
        Set<Parameter> freshParameters)
    {
    }


    
    /// <summary>
    /// Apply the inter-procedural analysis, binding information from caller and callee
    /// </summary>
    /// <param name="vr"></param>
    /// <param name="callee"></param>
    /// <param name="receiver"></param>
    /// <param name="arguments"></param>
    /// <param name="calleecState"></param>
    /// <param name="lb"></param>
    public virtual void ApplyAnalyzableCall(Variable vr, Method callee, Variable receiver,
        ExpressionList arguments, PointsToState calleecState, Label lb, out InterProcMapping ipm)
    {

      // Apply the binding between the pointsToGraph of the caller and the callee
      // Returns also the mapping used during the parameter-arguments nodes binding 

      if (PointsToAnalysis.debug)
      {
        Console.Out.WriteLine("before call to {0}", callee.Name);
        //pointsToGraph.Dump();
        this.Dump();
      }

      if (this.Method.Name.Name.StartsWith("UsingFoo"))
      {
      }  

      ipm = InterProcMapping.ComputeInterProgMapping(this.pointsToGraph, calleecState.pointsToGraph,
          receiver, arguments, vr, lb);

      BeforeBindCallWithCallee(this, calleecState, receiver, arguments, vr, lb, ipm);

      PTGraph interProcPTG = ipm.ComputeInterProgGraph(this.pointsToGraph, calleecState.pointsToGraph,
          receiver, arguments, vr, lb);

      pointsToGraph = interProcPTG;

      AfterBindCallWithCallee(this, calleecState, receiver, arguments, vr, lb, ipm);


      if (PointsToAnalysis.debug)
      {
        Console.Out.WriteLine("after call to {0}", callee.Name);
        Console.Out.WriteLine(ipm);
        Console.Out.WriteLine("Callee:");
        calleecState.Dump();

        Console.Out.WriteLine("Caller:");
        this.Dump();
      }

      // Update the set of write effects considering the writeffects of the callee
      // in nodes that will stay in the caller
      Set<LNode> removedLoadNodes = ipm.removedLoadNodes;
      // Join the set of non-analyzable calls
      //joinCalledMethod(callToNonAnalyzableMethods, calleecState.callToNonAnalyzableMethods);
    }

    protected virtual void BeforeBindCallWithCallee(PointsToState callerPTS, PointsToState calleePTS, Variable receiver, ExpressionList arguments, Variable vr, Label lb, InterProcMapping ipm)
    {

    }
    protected virtual void AfterBindCallWithCallee(PointsToState callerPTS, PointsToState calleePTS, Variable receiver, ExpressionList arguments, Variable vr, Label lb, InterProcMapping ipm)
    {

    }

    /// <summary>
    /// f(return v, cState), only has effect in the pointsToGraph
    /// </summary>
    /// <param name="v"></param>
    public virtual void ApplyReturn(Variable v, Label lb)
    {
      pointsToGraph.ApplyReturn(v, lb);
    }

    #endregion
    #endregion


    #region Parameter's Escape  check
    public bool CheckEscapes(Parameter p)
    {
      PTGraph ptg = PointsToGraph;
      return ptg.CheckEscape(p);
    }
    #endregion


    #region Freshnesh Check
    public bool CheckFreshness(Method m)
    {
      bool res = true;
      if (CciHelper.IsVoid(m.ReturnType))
        return this.PointsToGraph.CheckMethodFreshness(m);
      return res;
    }
    public bool CheckFreshness(Parameter p)
    {
      bool res = true;
      if (p.IsOut)
        return this.PointsToGraph.CheckParameterFreshness(p);
      return res;
    }
    #endregion

    #region IDataFlowState Implementation
    /// <summary>
    /// Display the cState
    /// </summary>
    public virtual void Dump()
    {
      Console.Out.WriteLine(ToString());
      PointsToGraph.GenerateDotGraph(Console.Out);

      //PointsToGraph.Dump();
      //WriteEffects.Dump();
    }
    public virtual void DumpDifference(PointsToState pt2)
    {
      pointsToGraph.DumpDifference(pt2.pointsToGraph);
    }
    #endregion

    #region Basic object overwritten methods (Equals, Hash, ToString)

    public override string ToString()
    {
      string res = PointsToGraph.ToString();
      res = res + "Assumptions:" + methodAssumptions.ToString() + "\n";
      // res = res + "Calls: " + CallToNonAnalizableString() + "\n";
      res = res + "Default: " + IsDefault.ToString() + "\n";
      return res;
    }

    //private string CallToNonAnalizableString()
    //{
    //    string res = "";
    //    foreach (Label lb in CallsToNonAnalyzable.Keys)
    //    {
    //        Statement stat = lb.Statement;
    //        if (stat != null)
    //            res += string.Format("In ({0},{1}) Statement:{2}\n", stat.SourceContext.StartLine,
    //                stat.SourceContext.StartColumn, stat.SourceContext.SourceText);
    //        foreach (Method m in callToNonAnalyzableMethods[lb])
    //        {
    //            res += string.Format("call to {0}\n", m.GetFullUnmangledNameWithTypeParameters());
    //        }
    //    }
    //    return res;
    //}

    public override bool Equals(object obj)
    {
      PointsToState ptgWeff = obj as PointsToState;
      bool eqPointsTo = ptgWeff != null && pointsToGraph.Equals(ptgWeff.pointsToGraph);

      bool eqExceptions = ptgWeff != null &&
          ((currentException == null && ptgWeff.currentException == null)
              || (currentException != null && currentException.Equals(ptgWeff.currentException)));

      bool eqVirtual = ptgWeff != null && methodAssumptions.Equals(ptgWeff.methodAssumptions);

      bool eq = eqPointsTo;

      return eq;
    }
    public override int GetHashCode()
    {
      return pointsToGraph.GetHashCode();
    }
    #endregion
  }
  #endregion


  /// <summary>
  /// The dataflow analysis for the method under analysis 
  /// </summary>
  public class PointsToInferer : ForwardDataFlowAnalysis
  {

    internal CfgBlock currBlock;
    internal PointsToInstructionVisitor iVisitor;
    internal TypeSystem typeSystem;
    internal IDataFlowState exitState;

    internal int iterationsCounter = 0;

    /// <summary>
    /// A reference to the Main Analysis class
    /// To get information about other methods under analysis
    /// </summary>
    internal PointsToAnalysis pointsToStateAnalysys;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="t"></param>
    /// <param name="pta"></param>
    public PointsToInferer(TypeSystem t, PointsToAnalysis pta)
    {
      this.pointsToStateAnalysys = pta;
      typeSystem = t;
      iVisitor = GetInstructionVisitor(this);
    }

    internal virtual PointsToInstructionVisitor GetInstructionVisitor(PointsToInferer pti)
    {
      return new PointsToInstructionVisitor(pti);
    }


    /// <summary>
    /// Compute the Dataflow analysis for the given method
    /// Returns true if the method is pure
    /// </summary>
    /// <param name="method"></param>
    /// <returns></returns>

    public bool ComputePointsToStateFor(Method method)
    {

      // Get or compute the CFG
      ControlFlowGraph cfg = pointsToStateAnalysys.GetCFG(method);

      //if (PointsToAnalysis.debug)
      //    CodePrinter.PrintMethod(method);

      PointsToState initialState = this.pointsToStateAnalysys.Factory.NewElement(method);


      // If we can compute de CFG and the method is not unsafe
      // We compute the dataflow analysis
      if (cfg != null && !PointsToAnalysis.IsUnsafe(method))
      {

        ComputeBeforeDataflow(method);
        this.Run(cfg, initialState);

        if (exitState == null)
        {
          if (PointsToAnalysis.debug)
              Console.WriteLine("Method {0} exitState NULL", method.GetFullUnmangledNameWithTypeParameters());
        }
        // FIX
        PointsToState PointsToStateAtExit = (PointsToState)exitState;
        ComputeAfterDataflow(method);

        return true;
      }
      else
      {
        exitState = null;
        return false;
      }
    }

    protected virtual void ComputeAfterDataflow(Method m)
    {
    }
    protected virtual void ComputeBeforeDataflow(Method m)
    {
    }
    /// <summary>
    /// Return the results of the analysis on exit of the CFG
    /// </summary>
    public PointsToState ExitState
    {
      get { return (PointsToState)exitState; }
    }

    // Visit the block in the CFG 
    protected override IDataFlowState VisitBlock(CfgBlock block, IDataFlowState stateOnEntry)
    {
      IDataFlowState resultState = stateOnEntry;

      Debug.Assert(block != null);

      currBlock = block;

      Analyzer.Write("---------block: " + block.UniqueId + ";");
      Analyzer.Write("   Exit:");
      foreach (CfgBlock b in block.NormalSuccessors)
        Analyzer.Write(b.UniqueId + ";");

      if (block.UniqueSuccessor != null)
        Analyzer.Write("   FallThrough: " + block.UniqueSuccessor + ";");
      if (block.ExceptionHandler != null)
        Analyzer.Write("   ExHandler: " + block.ExceptionHandler.UniqueId + ";");

      Analyzer.WriteLine("");

      PointsToState newState = ((PointsToState)stateOnEntry).Copy();
      // If there are too many calls to non analyzable methods
      // starts to ignore the statements
      //if (!pointsToStateAnalysys.BoogieMode ||
      //    newState.CallsToNonAnalyzable.Count < pointsToStateAnalysys.maxCallToNonAnalyzable)

      if (HasToVisit(newState))
      {
        if (block.ExceptionHandler != null)
          this.PushExceptionState(block, newState);

        resultState = base.VisitBlock(block, newState);
      }

      if (block.UniqueId == cfg.NormalExit.UniqueId)
      {
        exitState = resultState;
      }

      if (block.UniqueId == cfg.ExceptionExit.UniqueId)
      {
        exitState = resultState;
      }

      return resultState;
    }

    protected virtual bool HasToVisit(PointsToState state)
    {
      bool res = true;
      return res;
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


      if (PointsToAnalysis.debug)
      {
        Console.Out.WriteLine("Before: {0} ({1}) {2}", CodePrinter.StatementToString(statement),
            statement.SourceContext.StartLine, statement.GetType());
        dfstate.Dump();
      }

      PointsToState currentState = dfstate as PointsToState;

      /*
      if (PointsToAnalysis.IsCompilerGenerated(currentState.Method))
        if (!this.pointsToStateAnalysys.enclosingState.ContainsKey(currentState.Method.DeclaringType))
          return currentState;
      */

      if (PointsToAnalysis.verbose)
      {
        iterationsCounter++;
        if (iterationsCounter % 5000 == 0)
        {
          Console.Out.WriteLine("Counter: {3} {4} {0} ({1}) {2}", CodePrinter.StatementToString(statement),
              statement.SourceContext.StartLine, statement.GetType(), iterationsCounter, currentState.Method.FullName);
          dfstate.Dump();

        }
      }
      //if (CodePrinter.StatementToString(statement).Contains("return value := this.f"))
      //    System.Diagnostics.Debugger.Break();

      IDataFlowState result = (IDataFlowState)currentState;


      // For Debug...
      if (currentState.Method.Name.Name.Equals("Push"))
      { }


      // If there are too many calls to non analyzable methods
      // starts to ignore the statements
      // if (!pointsToStateAnalysys.BoogieMode || currentState.CallsToNonAnalyzable.Count < pointsToStateAnalysys.maxCallToNonAnalyzable)
      if (HasToVisit(currentState))
        result = (IDataFlowState)(iVisitor.Visit(statement, dfstate));


      if (PointsToAnalysis.debug)
      {
        Console.Out.WriteLine("After: {0} ({1})", CodePrinter.StatementToString(statement), statement.SourceContext.StartLine);
        dfstate.Dump();
      }

      //return dfstate;
      return result;
    }


    /// <summary>
    /// Merge two cState
    /// </summary>
    /// <param name="previous"></param>
    /// <param name="joinPoint"></param>
    /// <param name="atMerge"></param>
    /// <param name="incoming"></param>
    /// <param name="resultDiffersFromPreviousMerge"></param>
    /// <param name="mergeIsPrecise"></param>
    /// <returns></returns>
    protected override IDataFlowState Merge(CfgBlock previous, CfgBlock joinPoint, IDataFlowState atMerge,
        IDataFlowState incoming, out bool resultDiffersFromPreviousMerge, out bool mergeIsPrecise)
    {
      resultDiffersFromPreviousMerge = false;
      mergeIsPrecise = false;

      // No new states;
      if (incoming == null)
        return atMerge;

      // Initialize states
      if (atMerge == null)
      {
        resultDiffersFromPreviousMerge = true;
        return incoming;
      }

      //if (((PointsToState )atMerge).Equals(incoming))
      //    return atMerge;
      if (((PointsToState)atMerge).Includes((PointsToState)incoming))
        return atMerge;



      // Merge the two.
      PointsToState newState = ((PointsToState)atMerge).Copy();
      newState.Join((PointsToState)incoming);

      //if( newState.Method.FullName.StartsWith("System.Runtime.Remoting.Lifetime.Lease.ProcessNextSponsor"))
      //{
      //    PointsToState oldResult = (PointsToState)incoming;
      //    PointsToState newResult = newState;
      //        Console.Out.WriteLine("DIFERENCE a vs b");
      //        oldResult.PointsToGraph.DumpDifference(newResult.PointsToGraph);
      //        Console.Out.WriteLine("DIFERENCE b vs a");
      //        newResult.PointsToGraph.DumpDifference(oldResult.PointsToGraph);
      //}

      resultDiffersFromPreviousMerge = true;

      return newState;
    }

    /// <summary>
    /// Exception management
    /// Need Checking!
    /// </summary>
    /// <param name="handler"></param>
    /// <param name="currentHandlerState"></param>
    /// <param name="nextHandlerState"></param>
    protected override void SplitExceptions(CfgBlock handler, ref IDataFlowState currentHandlerState, out IDataFlowState nextHandlerState)
    {
      System.Diagnostics.Debug.Assert(currentHandlerState != null, "Internal error in Purity Analysis");

      PointsToState state = (PointsToState)currentHandlerState;

      if (handler == null || handler.Length == 0)
      {
        nextHandlerState = null;
        return;
      }

      if (handler[0] is Unwind)
      {
        nextHandlerState = null;
        currentHandlerState = null;
        return;
      }

      // This is
      if (!(handler[0] is Catch))
      {
        // everything sticks 
        nextHandlerState = null;
        return;
      }

      System.Diagnostics.Debug.Assert(handler[0] is Catch, "Exception Handler does not starts with Catch");

      if (state.currentException == null)
      {
        nextHandlerState = null;
        return;
      }


      System.Diagnostics.Debug.Assert(state.currentException != null, "No current exception to handle");

      Catch c = (Catch)handler[0];

      if (handler.ExceptionHandler != null &&
        !state.currentException.IsAssignableTo(c.Type))
      {
        nextHandlerState = state; ;
      }
      else
      {
        nextHandlerState = null;
      }

      // Compute what trickles through to the next handler
      //  and what sticks to this handler.
      if (state.currentException.IsAssignableTo(c.Type))
      {
        // everything sticks 
        nextHandlerState = null;
      }
      else if (c.Type.IsAssignableTo(state.currentException))
      {
        // c sticks, rest goes to next handler
        nextHandlerState = state;
        // copy state to modify the currentException
        state = (state).Copy();
        state.currentException = c.Type;
        currentHandlerState = state;
      }
      else
      {
        // nothing stick all goes to next handler
        nextHandlerState = state;
        currentHandlerState = null;
      }
      return;
    }

    // Exception management
    internal void PushExceptionWrapper(IDataFlowState state)
    {
      this.PushExceptionState(currBlock, state);
    }
  }

  /// <summary>
  /// Instruction visitor. Implement the transfer function of the dataflow analysis
  /// </summary>
  internal class PointsToInstructionVisitor : InstructionVisitor
  {
    // A reference to the analyzer for the method
    protected PointsToInferer pta;
    // A reference to the global analysis
    protected PointsToAnalysis PointsToAnalysis;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pta"></param>
    public PointsToInstructionVisitor(PointsToInferer pta)
    {
      this.pta = pta;
      PointsToAnalysis = this.pta.pointsToStateAnalysys;
    }


    protected override object DefaultVisit(Statement stat, object arg)
    {
      //Console.Out.WriteLine("Visitando: {0}", CodePrinter.StatementToString(stat));
      return arg;
    }
    #region Method Call Visitors and Methods to support interprocedural analysis

    /// <summary>
    /// We treat this kind of call as non analyzable
    /// Not support for Function Pointers Yet
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="callee"></param>
    /// <param name="receiver"></param>
    /// <param name="arguments"></param>
    /// <param name="fp"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitCallIndirect(Variable dest, Variable callee, Variable receiver,
        Variable[] arguments, FunctionPointer fp, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      Label lb = new Label(stat, cState.Method);
      ExpressionList expList = new ExpressionList();
      foreach (Variable v in arguments)
        expList.Add(v);
      InterProcMapping ipm;
      cState.ApplyNonAnalyzableCall(dest,null, receiver, expList, lb, out ipm);
      //  cState.ApplyNonAnalyzableCallBasic(dest, null, receiver, expList, lb);
      return cState;
      //return base.VisitCallIndirect(dest, callee, receiver, arguments, fp, stat, arg);
    }

    
    protected virtual object AnalyzeClousure(object enclosingMethodState, Method callee) {
      PointsToState enclosingMethodPts = (PointsToState)enclosingMethodState;
      
      Class c = callee.DeclaringType as Class;
      if (c.Template != null)
        c = (Class)c.Template;
      if (this.PointsToAnalysis.EnclosingState(c) == null) {

        this.PointsToAnalysis.enclosingState[c] = enclosingMethodPts;

        this.PointsToAnalysis.Visit(c);

        this.PointsToAnalysis.enclosingState.Remove(c);
      }
      return enclosingMethodPts;
    }
    

    /// <summary>
    /// Visit a call statement. 
    /// It also wraps  set_item (a[i]=...) and get_item (dest = a[i])
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="receiver"></param>
    /// <param name="callee"></param>
    /// <param name="arguments"></param>
    /// <param name="virtcall"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitCall(Variable dest, Variable receiver, Method callee,
        ExpressionList arguments, bool virtcall, Statement stat, object arg)
    {
      PointsToState pts = (PointsToState)arg;
      if (PointsToAnalysis.debug)
      {
        if (pts.Method.Name.Name.StartsWith("get_UnsupportedNumberStyle"))
        {
          //                System.Diagnostics.Debugger.Break();
        }
      }

      PointsToState cState = (PointsToState)arg;
      Label lb = new Label(stat, cState.Method);

      // Check if it is a Delegate assignment
      if (CciHelper.IsConstructor(callee) && PTGraph.IsDelegateType(receiver.Type))
      {
        cState.PointsToGraph.AssignDelegate(receiver, (Variable)arguments[0], (Variable)arguments[1], lb);
        return cState;
      }

      // Special treatment for closure classes
      if (CciHelper.IsConstructor(callee))
        if(PointsToAnalysis.IsCompilerGenerated(callee)) {
          AnalyzeClousure(pts,callee);
          return cState;
      }

      // I consider this call as a array store receiver[i]= a2
      if (callee.Name.Name.Equals("set_Item"))
      {
        Variable a2 = (Variable)arguments[1];
        cState.ApplyStoreElement(receiver, a2, lb);
      }
      else
      {
        // I consider this call as a array load dest = receiver[i]
        if (callee.Name.Name.Equals("get_Item"))
        {
          cState.ApplyLoadElement(dest, receiver, lb);
        }
        else
        {
          //PointsToState calleecState;
          Set<PointsToState> calleecStates;
          // Try to about the callee. It can be analyzable, already analyzed, or annotated as pure
          // If it could, get the dataflow analysis of that calee
          if (TryAnalyzeCallee(cState, receiver, callee, cState.Method, virtcall, lb, out calleecStates))
          {
            // Compute the interProc Analysis in the pointsToGraph
            Variable newReceiver = receiver;
            InterProcMapping ipm;
              /*
            // Delegate invocations has the delegate as receiver in this framework
            // So I need to set the right receiver...
            if (receiver != null && PTGraph.IsDelegateType(receiver.Type))
            {
              newReceiver = cState.Method.IsStatic ? null : cState.Method.ThisParameter;
            }
               */ 
            if (calleecStates.Count == 1)
            {
              PointsToState calleecState = calleecStates.PickAnElement();
              cState.ApplyAnalyzableCall(dest, callee, newReceiver, arguments, calleecState, lb, out ipm);
            }
            else
            {
              // A copy of the original caller state
              PointsToState originalcState = cState.Copy();
              foreach (PointsToState calleecState in calleecStates)
              {
                // Make the interproc for each calle with the original caller state
                PointsToState copyCS = originalcState.Copy();
                //InterProcMapping ipm;
                copyCS.ApplyAnalyzableCall(dest, callee, newReceiver, arguments, calleecState, lb, out ipm);
                cState.Join(copyCS);
              }
            }
          }
          else
          {
            // This is to manage SPEC# generated code
            // For same cases we should just ignored it and in other cases
            // it behaves like a Copy statement
            if (IsACopyLikeCall(callee) && arguments != null)
            {
              cState.CopyLocVar(dest, arguments[0] as Variable, lb);
            }
            else
            {
              if ((callee.FullName.StartsWith("Microsoft.Contracts"))
                  || callee.FullName.EndsWith("get_SpecSharp::FrameGuard"))
              {
                cState.ForgetVariable(dest);
              }
              else
              {
                  if (cState.Method.Name.Name.StartsWith("UsingFoo"))
                  {
                  }
                // If the calle is not analyzable we still update the info in the caller
                  InterProcMapping ipm;
                  if (!this.PointsToAnalysis.WasAnalyzed(callee))
                  {
                      
                      cState.ApplyNonAnalyzableCall(dest, callee, receiver, arguments, lb, out ipm);
                  }
                  else
                  {
                      
                      PointsToState calleecState = PointsToAnalysis.GetSummaryForMethodWithDefault(callee);
                      cState.ApplyAnalyzableCall(dest, callee, receiver, arguments, calleecState, lb, out ipm);
                  }
              }
            }
          }

        }
      }
      // Push possible exceptions to handlers.
      if (callee.Contract != null) {
        for (int i = 0; i < callee.Contract.Ensures.Count; i++) {
          EnsuresExceptional e = callee.Contract.Ensures[i] as EnsuresExceptional;
          if (e != null) {
            PointsToState exnState = cState;
            cState = cState.Copy();
            exnState.currentException = e.Type;
            pta.PushExceptionState(pta.currBlock, exnState);
          }
        }
      }
      return cState;
    }

    private void Break()
    {
      throw new Exception("The method or operation is not implemented.");
    }
    protected virtual bool IsACopyLikeCall(Method callee)
    {
      return callee.FullName.StartsWith("Microsoft.Contracts.NonNullType.IsNonNullGeneric")
            || callee.FullName.StartsWith("Microsoft.Contracts.NonNullType.IsNonNullImplicitGeneric");
    }
    /// <summary>
    /// The treat VisitConstrainedCall as a Std call
    /// Can be improved...
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="receiver"></param>
    /// <param name="callee"></param>
    /// <param name="arguments"></param>
    /// <param name="constraint"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitConstrainedCall(Variable dest, Variable receiver, Method callee, ExpressionList arguments,
        TypeNode constraint, Statement stat, object arg)
    {
      // check typenode
      // if it value => not virtual
      return VisitCall(dest, receiver, callee, arguments, true, stat, arg);
    }

    /// <summary>
    /// Determine if the callee method is analyzable
    /// or pure and gets or compute the callee dataflow information 
    /// </summary>
    /// <param name="callercState"></param>
    /// <param name="receiver"></param>
    /// <param name="callee"></param>
    /// <param name="caller"></param>
    /// <param name="isVirtualCall"></param>
    /// <param name="lb"></param>
    /// <param name="calleecState"></param>
    /// <returns></returns>
    protected virtual bool TryAnalyzeCallee(PointsToState callercState, Variable receiver, Method callee, Method caller,
        bool isVirtualCall, Label lb, out Set<PointsToState> calleecStates)
    {

      calleecStates = new Set<PointsToState>();

      if (caller.FullName.Contains("Except"))
        if (callee.Name.Name.Equals("Add"))
        {
        }
      
      // DIEGO-TODO: Check if is better to replace the calle by its template directly at the beggining
      bool isTemplate = false;
      bool solveInstance = false;


      isTemplate = callee.DeclaringType.IsGeneric && callee.Template == null;

      // verify whether the method is analyzed or not
      // That means, is not unsafe and the statements are available
      // This can be bounded to the compuation unit to reduce 
      // analysis time, still being interprocedural
      bool isAnalyzable = false;


      
      // If it is assumed pure I don't analyzed it
      if ((!PureAsAnalyzable() && PointsToAndEffectsAnnotations.IsAssumedPureMethod(callee)) 
          || ForcedByAnnotations(callee) ) 
      {
        // We treat known pure as non-analyzable but we then rely on the
        // the provided annotations (pure, confined, stateindependend, fresh, etc.)
        isAnalyzable = false;
      }
      else
      {
        //if (callee.Name.Name.Contains("GetEnumerator") && caller.Name.Name.Contains("AddAll"))
        //{
        //    System.Diagnostics.Debugger.Break();
        //}
        if (PointsToAnalysis.IsInterProceduralAnalysys)
        {
          Set<Method> callees = new Set<Method>(); ;

          isAnalyzable = PointsToAnalysis.IsAnalyzable(callee) && (!callee.IsVirtual || !isVirtualCall);
          if (isAnalyzable)
            callees.Add(callee);
          // Set<Method> potencialCalles = PotentialCallees(callercState, callee, receiver);

          // I removed virtual call resolution because it tries to analyze method (eg. Dictionary.Add) 
          // which has calls that is not able to analyze, leading to not desired behaviour.
          if (PointsToAnalysis.tryToSolveCallsToInterfaces) {
            if (!isAnalyzable && callee.IsVirtual && !callee.IsExtern)
              isAnalyzable = TryToSolveVirtualCall(callercState, receiver, callee, isVirtualCall, ref solveInstance, out callees);
          }

          // Try to obtain the delegate that is contained in the node
          if (!isAnalyzable && callee.IsExtern)
            isAnalyzable = TryToSolveDelegate(callercState, receiver, callee, callee.IsExtern, lb, out callees);


          // isAnalyzable = isAnalyzable && wasAnalyzed;
          if (isAnalyzable)
          {
            AnalyzeAnalyzable(ref callee, caller, calleecStates, ref isAnalyzable, callees);
          }

        }
        else
        {
          isAnalyzable = false;
        }

        // If it's a virtual call and we analyze the callee, we add the 
        // asumption about we use that method and not a subclass method
        if (isAnalyzable)
        {
          if ((isVirtualCall && callee.IsVirtual && solveInstance)
              || (isTemplate))
          {
            callercState.methodAssumptions.Add(callee);
            // Console.WriteLine("Added {0} in method assumptions", callee.FullName);
            // calleecState.methodAssumptions.Add(callee);
          }
        }
      }


      return isAnalyzable;
    }

    protected virtual bool PureAsAnalyzable()
    {
      return false;
    }
    
    protected virtual bool ForcedByAnnotations(Method m)
    {
        bool res = PointsToAndEffectsAnnotations.IsAnnotated(m);
        return res;
    }


    protected virtual void AnalyzeAnalyzable(ref Method callee, Method caller, Set<PointsToState> calleecStates, ref bool isAnalyzable, Set<Method> callees)
    {
      foreach (Method c in callees)
      {
        // To deal with template methods...
        // It gets the templete method if it exists
        callee = PointsToAnalysis.solveMethodTemplate(c);
        PointsToState calleecState = null;
        if (PointsToAnalysis.fixPointForMethods)
        {
          #region with recursion support
          // The the previous analysis result. We traverse the methods in 
          // a bottom up fashon using a partial call graph. So we usally
          // get the final result for the calle (except in recursive calls)
          calleecState = PointsToAnalysis.GetSummaryForMethodWithDefault(callee);

          // the caller is registered for reanalysis if callee changes
          this.PointsToAnalysis.AddCallerToCallee(callee, caller);
          #endregion
        }
        else
        {
          // Inlining simulation. Top down analysis.
          #region withOutRecursion
          // Determines the the method was already analyzed
          // That is, there some previous analyzis result
          bool wasAnalyzed = PointsToAnalysis.HasSummary(callee);
          if (!wasAnalyzed)
          {
            // If the call is not recursive we analize the method
            if (!PointsToAnalysis.callStack.Contains(callee)
                && PointsToAnalysis.callStackDepth < PointsToAnalysis.maximumStackDepth)
            {
              // The call is registered to deal with recursion
              PointsToAnalysis.callStack.Push(callee);
              // The method is analyzed
              PointsToAnalysis.callStackDepth++;
              PointsToAnalysis.AnalysisForMethod(callee);
              PointsToAnalysis.callStackDepth--;
              // The call is unregistered to deal with recursion
              PointsToAnalysis.callStack.Pop();
            }
            else
            {
              //if we found a cycle, we assume no result
              //PointsToState previous = weakPurityAnalysis.GetPurityAnalysisWithDefault(callee);
              //weakPurityAnalysis.SetPurityAnalysys(callee,previous);
              isAnalyzable = false;
            }

          }
          calleecState = PointsToAnalysis.GetSummaryForMethodWithDefault(callee);
          #endregion
        }
        calleecStates.Add(calleecState);

      }
    }

    private bool TryToSolveVirtualCall(PointsToState callercState, Variable receiver, Method callee,
        bool isVirtualCall, ref bool solveInstance, out Set<Method> callees)
    {
      callees = Set<Method>.Empty;
      bool isAnalyzable = false;
      if (isVirtualCall)
      {
        Nodes receivers = callercState.PointsToGraph.Values(receiver);
        if (PointsToAnalysis.debug)
        {
          Console.WriteLine("Receivers virtual call:" + receivers);
          if (receivers.Count > 1)
          {
            Console.WriteLine("More than one potential receiver");
          }
        }
        if (receivers.Count > 0)
        {
          bool allInside = true;
          foreach (IPTAnalysisNode r in receivers)
          {
            allInside = allInside && (!CciHelper.IsInterface(r.Type) && r.IsInside);
            if (!allInside) break;
            // Try to get the method from an instance of the passed type
            callees.Add(PointsToAnalysis.solveInstance(r.Type, callee, out solveInstance));

          }
          if (allInside)
          {
            if (PointsToAnalysis.debug)
            {
              Console.WriteLine("Receivers Var Type: {0} Nodes: {1} {2} {3}",
                  receiver.Type, receivers, isVirtualCall, callee.IsVirtual);
            }

            isAnalyzable = true;

          }
        }
      }
      return isAnalyzable;
    }

    private bool TryToSolveDelegate(PointsToState callercState, Variable receiver,
        Method callee, bool isExtern, Label lb, out Set<Method> callees)
    {
      callees = Set<Method>.Empty;
      bool isAnalyzable = false;
      if (isExtern)
      {
        Nodes receivers = Nodes.Empty;
        if (receiver != null)
          receivers.AddRange(callercState.PointsToGraph.GetValuesIfEmptyLoadNode(receiver, lb));
        if (PointsToAnalysis.debug)
        {
          Console.WriteLine("Receivers virtual call:" + receivers);
          if (receivers.Count > 1)
          {
            Console.WriteLine("More than one potential receiver");
          }
        }
        if (receivers.Count > 0)
        {
          bool allInside = true;
          foreach (IPTAnalysisNode r in receivers)
          {
            allInside = allInside && (r.IsMethodDelegate);
            if (!allInside) break;
            MethodDelegateNode mn = (MethodDelegateNode)r;
            callees.Add(mn.Method);

          }

          if (allInside)
          {
            isAnalyzable = true;
          }

        }
      }
      return isAnalyzable;
    }

    #endregion

    /// <summary>
    /// Visitor for copy statements dest = source
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="source"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitCopy(Variable dest, Variable source, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      Label lb = new Label(stat, cState.Method);
      //if (PointsToAnalysis.debug)
      //{
      //    Console.Out.WriteLine("Copy {0}:{1} = {2}:{3}", dest, dest.Type.ToString(), source, source.Type.ToString());
      //}
      cState.CopyLocVar(dest, source, lb);
      return cState;
    }

    protected override object VisitLoadNull(Variable dest, Literal source, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      cState.ApplyAssignNull(dest);
      return cState;
    }

    /// <summary>
    /// Visitor for load field, it computes different values for static and instance fields
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="source"></param>
    /// <param name="field"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitLoadField(Variable dest, Variable source, Field field, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      Label lb = new Label(stat, cState.Method);
      // I had to add this because if find some static load that were reconized as non static field load
      if (field.IsStatic || source == null)
      {
        if (PTGraph.IsDelegateType(field.Type))
        {
          cState.ApplyLoadChachedDelegate(dest, field, lb);
        }
        else
          cState.ApplyLoadStatic(dest, field, lb);

      }
      else
      {
        //if (PTGraph.IsDelegateType(field.Type))
        //{
        //    cState.ApplyLoadChachedDelegate(dest, field, lb);
        //}
        //else
        cState.ApplyLoadField(dest, source, field, lb);
      }
      return cState;
    }

    /// <summary>
    /// Visitor for store field, it computes different values for static and instance fields
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="source"></param>
    /// <param name="field"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitStoreField(Variable dest, Field field, Variable source, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      if (PointsToAnalysis.debug)
      {
        Console.Out.WriteLine("Entre a StoreField en {0}", CodePrinter.StatementToString(stat));
        Console.Out.WriteLine("Antes {0}", cState.PointsToGraph);
      }
      Label lb = new Label(stat, cState.Method);
      // Check if it an assigment to a delegate variable
      if (PTGraph.IsDelegateType(field.Type) && field.IsStatic && field.Name.Name.Contains("CachedAnonymousMethodDelegate"))
      {
        cState.CopyLocVar(cState.PointsToGraph.GetCachedVariable(field.Name.Name, field.Type), source, lb);
      }
      else
      {
        if (field.IsStatic)
        {
          cState.ApplyStaticStore(source, field, lb);
        }
        else
        {

          cState.ApplyStoreField(dest, field, source, lb);
        }
      }
      if (PointsToAnalysis.debug)
        Console.Out.WriteLine("Despues {0}", cState.PointsToGraph);
      return cState;
    }

    /// <summary>
    /// Visitor for dest = source[index]. 
    /// In this case very similar to load field
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="source"></param>
    /// <param name="index"></param>
    /// <param name="elementType"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitLoadElement(Variable dest, Variable source, Variable index, TypeNode elementType,
        Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      cState.ApplyLoadElement(dest, source, new Label(stat, cState.Method));
      return cState;
    }

    /// <summary>
    /// Visitor for dest[index] = source
    /// In this case, veru similar to store field
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="index"></param>
    /// <param name="source"></param>
    /// <param name="elementType"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitStoreElement(Variable dest, Variable index, Variable source, TypeNode elementType,
        Statement stat, object arg)
    {

      PointsToState cState = (PointsToState)arg;
      Label lb = new Label(stat, cState.Method);
      cState.ApplyStoreElement(dest, source, lb);
      return cState;
      // return base.VisitStoreElement(dest, index, source, elementType, stat, arg);
    }

    #region Visitors for Object Allocation
    /// <summary>
    /// Visitor for dest = new type
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="type"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitNewObject(Variable dest, TypeNode type, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      cState.ApplyNewStatement(dest, new Label(stat, cState.Method), type);
      return cState;
    }

    /// <summary>
    /// Visitor for dest = new type[]
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="type"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitNewArray(Variable dest, TypeNode type, Variable size, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      cState.ApplyNewStatement(dest, new Label(stat, cState.Method), type);
      return cState;
      // return base.VisitNewArray(dest, type, size, stat, arg);
    }

    /// <summary>
    /// Visitor for init *addr
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="type"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitInitObj(Variable addr, TypeNode valueType, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      cState.ApplyNewStatement(addr, new Label(stat, cState.Method), valueType);
      return cState;
      //return base.VisitInitObj(addr, valueType, stat, arg);
    }
    #endregion

    protected override object VisitReturn(Variable var, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      cState.ApplyReturn(var, new Label(stat, cState.Method));
      return cState;
    }

    /// <summary>
    /// dest = conts. dest doesn't point to an object
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="source"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitLoadConstant(Variable dest, Literal source, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      cState.ForgetVariable(dest);
      return cState;
    }
    /// <summary>
    /// dest = op1 op op2.  dest doesn't point to an object
    /// </summary>
    /// <param name="op"></param>
    /// <param name="dest"></param>
    /// <param name="operand1"></param>
    /// <param name="operand2"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitBinaryOperator(NodeType op, Variable dest, Variable operand1,
        Variable operand2, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      cState.ForgetVariable(dest);
      return cState;
    }

    /// <summary>
    /// We record the method entry
    /// </summary>
    /// <param name="method"></param>
    /// <param name="parameters"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    /// 
    protected virtual PointsToState EnlargeState(PointsToState initial, PointsToState enclosing) {
      PointsToState cState = initial;
      return cState;
    }
    protected override object VisitMethodEntry(Method method, System.Collections.IEnumerable parameters, Statement stat, object arg)
    {

      PointsToState cState = (PointsToState)arg;
      /*
      if (PointsToAnalysis.IsCompilerGenerated(method) && this.PointsToAnalysis.enclosingState.ContainsKey(method.DeclaringType)) {
        PointsToState enclosingState = this.PointsToAnalysis.enclosingState[method.DeclaringType];
        cState = EnlargeState(cState, enclosingState);
      }
      */
      
      Label lb = new Label(stat, cState.Method);
      cState.InitMethod(method, parameters, lb);
      return cState;
      //return base.VisitMethodEntry(method, parameters, stat, arg);
    }

    /// <summary>
    /// Unwind. No action performed. IT IS OK??
    /// </summary>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitUnwind(Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      return cState;
    }

    /// <summary>
    /// dest = op op1.  dest doesn't point to an object
    /// </summary>
    /// <param name="op"></param>
    /// <param name="dest"></param>
    /// <param name="operand1"></param>
    /// <param name="operand2"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitUnaryOperator(NodeType op, Variable dest, Variable operand, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      cState.ForgetVariable(dest);
      return cState;
      //return base.VisitUnaryOperator(op, dest, operand, stat, arg);
    }

    /// <summary>
    /// dest = sizeof type.  dest doesn't point to an object
    /// </summary>
    /// <param name="op"></param>
    /// <param name="dest"></param>
    /// <param name="operand1"></param>
    /// <param name="operand2"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitSizeOf(Variable dest, TypeNode type, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      cState.ForgetVariable(dest);
      return cState;
      // return base.VisitSizeOf(dest, type, stat, arg);
    }

    /// <summary>
    /// dest := source as T. We assume it as a copy
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="source"></param>
    /// <param name="type"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitIsInstance(Variable dest, Variable source, TypeNode type, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      cState.CopyLocVar(dest, source);
      return cState;
    }

    /// <summary>
    /// dest = (type) source. Assumed as a copy
    /// Have to deal with Exception???
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="source"></param>
    /// <param name="type"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitCastClass(Variable dest, TypeNode type, Variable source, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      cState.CopyLocVar(dest, source);
      return cState;
    }

    /// <summary>
    /// Box We assume it as a copy
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="source"></param>
    /// <param name="type"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitBox(Variable dest, Variable source, TypeNode type, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      cState.CopyLocVar(dest, source);
      return cState;
    }
    /// <summary>
    /// Box We assume it as a copy
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="source"></param>
    /// <param name="type"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitUnbox(Variable dest, Variable source, TypeNode type, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      cState.CopyLocVar(dest, source);
      return cState;
    }

    /// <summary>
    /// dest := refanytype source --- extracts the type from a typed reference and assigns it to dest.
    /// A type is not a reference, so we forget dest
    /// </summary>
    protected override object VisitRefAnyType(Variable dest, Variable source, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      cState.ForgetVariable(dest);
      return cState;
    }
    /// <summary>
    /// dest := refanyval source,type -- load the address out of a typed reference
    /// Assumed as Copy.
    /// HAVE TO DEAL with exceptions
    /// <p>
    /// Description:
    /// <br/>
    /// Throws <c>InvalidCastException</c> if typed reference isn't of type <c>type</c>. If it is
    /// extracts the object reference and stores it in dest.
    /// </p>
    /// </summary>
    protected override object VisitMakeRefAny(Variable dest, Variable source, TypeNode type, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      cState.CopyLocVar(dest, source);
      return cState;
    }

    /// <summary>
    /// We assumed the function as something we don't track
    /// We forget the value of dest
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="source"></param>
    /// <param name="method"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitLoadFunction(Variable dest, Variable source, Method method, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      Label lb = new Label(stat, cState.Method);
      cState.ApplyLoadMethod(dest, source, method, lb);
      // cState.ForgetVariable(dest);
      return cState;
    }

    /// <summary>
    /// var = catch(type) -- catch exception matching type and store in var
    /// <p>
    /// Description:
    /// <br/>
    /// Starts an exception handler and acts as the test whether the handler applies to the caught 
    /// exception given the type. If the exception does not apply, then control goes to the handler
    /// of the current block. Otherwise, control goes to the next instruction.
    /// </p>
    /// We forget the value of var
    /// SHOULD DO SOMETHING WITH THIS!
    /// </summary>
    /// <param name="var">Variable that holds the caught exception.</param>
    /// <param name="type">Type of the exceptions that are caught here.</param>
    protected override object VisitCatch(Variable var, TypeNode type, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      cState.ForgetVariable(var);
      return cState;
    }
    /// <summary>
    /// CHECK
    /// </summary>
    /// <param name="var"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitThrow(Variable var, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      cState.ForgetVariable(var);
      cState.currentException = var.Type;

      if (pta.currBlock == null)
        return cState;

      pta.PushExceptionState(pta.currBlock, cState);
      //pta.exitState = cState;
      //return cState;
      return null;
    }
    /// <summary>
    /// CHECK
    /// </summary>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitRethrow(Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      pta.PushExceptionState(pta.currBlock, cState);
      //pta.exitState = cState;
      //return cState;
      return null;
    }

    // No idea 
    protected override object VisitArgumentList(Variable dest, Statement stat, object arg)
    {
      return base.VisitArgumentList(dest, stat, arg);
    }

    /// <summary>
    /// I take it as dest = * pointer
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="pointer"></param>
    /// <param name="type"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitLoadIndirect(Variable dest, Variable pointer, TypeNode type, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;

      if (PointsToAnalysis.debug)
      {
        Console.Out.WriteLine("Entre a LoadIndirect en {0}", CodePrinter.StatementToString(stat));
        Console.Out.WriteLine("Antes {0}", cState.PointsToGraph);
      }

      Label lb = new Label(stat, cState.Method);
      cState.ApplyLoadIndirect(dest, pointer, lb);

      if (PointsToAnalysis.debug)
        Console.Out.WriteLine("Despues {0}", cState.PointsToGraph);

      return cState;
    }
    /// <summary>
    /// If take it as *pointer = source
    /// </summary>
    /// <param name="pointer"></param>
    /// <param name="source"></param>
    /// <param name="type"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitStoreIndirect(Variable pointer, Variable source, TypeNode type, Statement stat, object arg)
    {

      PointsToState cState = (PointsToState)arg;
      if (PointsToAnalysis.debug)
      {
        Console.Out.WriteLine("Entre a StoreIndirect en {0}", CodePrinter.StatementToString(stat));
        Console.Out.WriteLine("Antes {0}", cState.PointsToGraph);
      }

      Label lb = new Label(stat, cState.Method);
      cState.ApplyStoreIndirect(pointer, source, lb);
      // cState.CopyLocVar(pointer, source, lb);

      if (PointsToAnalysis.debug)
        Console.Out.WriteLine("Despues {0}", cState.PointsToGraph);
      return cState;
      // return base.VisitStoreIndirect(pointer, source, type, stat, arg);
    }
    /// <summary>
    /// dest = &amp; source. 
    /// In most cases the analysis consider this a std copy
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="source"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitLoadAddress(Variable dest, Variable source, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      if (PointsToAnalysis.debug)
      {
        Console.Out.WriteLine("Entre a LoadAddress en {0}", CodePrinter.StatementToString(stat));
        Console.Out.WriteLine("Antes {0}", cState.PointsToGraph);
      }

      Label lb = new Label(stat, cState.Method);
      cState.ApplyLoadAddress(dest, source, lb);

      if (PointsToAnalysis.debug)
        Console.Out.WriteLine("Despues {0}", cState.PointsToGraph);
      return cState;
    }

    /// <summary>
    /// dest = &amp;(source.field). Assumed as std LoadField
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="source"></param>
    /// <param name="field"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitLoadFieldAddress(Variable dest, Variable source, Field field, Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      if (PointsToAnalysis.debug)
      {
        Console.Out.WriteLine("Entre a LoadFieldAddress en {0}", CodePrinter.StatementToString(stat));
        Console.Out.WriteLine("Antes {0}", cState.PointsToGraph);
      }
      Label lb = new Label(stat, cState.Method);
      if (field.IsStatic)
      {
        cState.ApplyLoadStaticFieldAddress(dest, field, lb);
      }
      else
      {
        cState.ApplyLoadFieldAddress(dest, source, field, lb);
      }

      if (PointsToAnalysis.debug)
        Console.Out.WriteLine("Despues {0}", cState.PointsToGraph);
      return cState;
      ///return VisitLoadField(dest, source, field, stat, arg);
    }

    /// <summary>
    /// dest = &amp;array[index]). Assumed as std LoadElement
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="array"></param>
    /// <param name="index"></param>
    /// <param name="elementType"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitLoadElementAddress(Variable dest, Variable array, Variable index, TypeNode elementType,
        Statement stat, object arg)
    {
      PointsToState cState = (PointsToState)arg;
      if (PointsToAnalysis.debug)
      {
        Console.Out.WriteLine("Entre a LoadElementAddress en {0}", CodePrinter.StatementToString(stat));
        Console.Out.WriteLine("Antes {0}", cState.PointsToGraph);
      }
      Label lb = new Label(stat, cState.Method);
      cState.ApplyLoadFieldAddress(dest, array, PTGraph.arrayField, lb);

      if (PointsToAnalysis.debug)
        Console.Out.WriteLine("Despues {0}", cState.PointsToGraph);
      return cState;

      //return VisitLoadElement(dest, array, index, elementType, stat, arg);
    }

  }

  /// <summary>
  /// The main analysis class. Entry point to analyze a method, an assembly or 
  /// a compilation unit.
  /// At construction time, you can define if the analysis is inter-procedural or only
  /// intra-procedural. If you choose inter-procedural, you can choose a fix-point 
  /// based approach, using a backward traversal over a partial call-graph, or an inlining
  /// simulation (by selecting a maximun call-stack depth).
  /// The analysis has 2 modes of operation: StandAlone or inside CCI or Boogie.
  /// The main diference is that in standalone mode, it tries to analyze all methods it finds in the assembly.
  /// In the other case, it only analyzes the methods annotated as [pure] or [confined]. 
  /// The purpose of the StandAlone mode is INFERENCE. The other mode is VERIFICATION.
  /// </summary>
  public class PointsToAnalysis : StandardVisitor
  {

    protected ElementFactory factory;

    #region Attributes
    internal static int counter = 0;

    protected TypeSystem typeSystem;

    private bool interProceduralAnalysis = true;
    internal bool fixPointForMethods = false;
    internal bool standAloneApp = false;
    internal int maximumStackDepth = 3;
    internal int callStackDepth = 0;

    internal bool tryToSolveCallsToInterfaces = false;

    //internal int maxCallToNonAnalyzable = 1;
    //internal int maxWriteEffects = 1;
    internal bool boogieMode = false;


    internal Analyzer analyzer = null;


    private Dictionary<Method, PointsToState> pointsToWEMap = new Dictionary<Method, PointsToState>();

    private Set<Method> alreadyAnalyzedMethods = new Set<Method>();

    internal Node unitUnderAnalysis;

    public static bool debug = false;
    // To see more detailed results (pointstoGraph)
    public static bool verbose = false;



    // For fix point computation
    // Callers that call a given callee
    internal Dictionary<Method, Set<Method>> calleerToCallersMap = new Dictionary<Method, Set<Method>>();
    // methods that needs to be analyzed
    internal List<Method> methodsToAnalyze = new List<Method>();

    // To bound which method we want to analyze
    internal Set<Method> analyzableMethods;
    //internal Set<string> analyzableMethods;


    // Flag to control the the scope of the stand alone application
    // Just for testing
    public static string ClassFilter = "", ModuleFilter = "";
    public string classFilter = "", moduleFilter = "";

    // keep track of the call chain. Used to deal with recursion
    internal Stack<Method> callStack = new Stack<Method>();


    // Cache of computed method's CFGs
    private Hashtable cfgRepository = new Hashtable();

    internal System.Compiler.Analysis.CallGraph cg;

    // Just for statistics
    //public int numberOfPures = 0;
    //public int numberOfDeclaredPure = 0;
    public int numberOfMethods = 0;

    #endregion

    #region Constructors
    public PointsToAnalysis(TypeSystem t)
    {
      typeSystem = t;
      // analyzableMethods = new Set<string>();
      analyzableMethods = new Set<Method>();
    }

    /// <summary>
    /// Constructor with a given Node. 
    /// We used to compute the set of nodes in this node and (optionally) bound the set of 
    /// analyzable methods in the interprocedural analysis
    /// </summary>
    /// <param name="t"></param>
    /// <param name="node"></param>
    public PointsToAnalysis(TypeSystem t, Node node)
    {
      Init(t, node, true, false, 3);
    }
    public PointsToAnalysis(TypeSystem t, Node node, bool interProcedural)
    {
      Init(t, node, interProcedural, false, 3);
    }
    public PointsToAnalysis(TypeSystem t, Node node, bool interProcedural, bool fixPoint)
    {
      Init(t, node, interProcedural, true, 3);
    }

    public PointsToAnalysis(TypeSystem t, Node node, bool interProcedural, bool fixpoint, int maxDepth)
    {
      Init(t, node, interProcedural, fixpoint, maxDepth);
    }

    protected virtual void Init(TypeSystem t, Node node, bool interProcedural, bool fixpoint, int maxDepth)
    {

      factory = new ElementFactory(this);

      classFilter = ClassFilter;
      moduleFilter = ModuleFilter;

      typeSystem = t;
      unitUnderAnalysis = node;
      analyzableMethods = new Set<Method>();

      this.interProceduralAnalysis = interProcedural;
      this.fixPointForMethods = fixpoint;
      this.maximumStackDepth = maxDepth;

      if (verbose)
        Console.Out.WriteLine("MaxDepth: {0}", this.maximumStackDepth);

      // For Inperprocedural FixPoint should be better 
      // to traverse the methdod in a bottom up fashion...
      if (interProceduralAnalysis && fixPointForMethods)
      {
        //Set<System.Compiler.Analysis.CallGraph> CGs;
        // cg = System.Compiler.Analysis.AnalyzableMethodFinder.ComputeAnalyzableMethods(node, out CGs);
        cg = AnalyzableMethodFinder.ComputeAnalyzableMethods(node, this);
        analyzableMethods = cg.Methods;
      }
    }



    /// <summary>
    /// Same constructor but with the analyzer
    /// </summary>
    /// <param name="analyzer"></param>
    /// <param name="t"></param>
    /// <param name="node"></param>
    public PointsToAnalysis(Analyzer analyzer, TypeSystem t, Node node)
    {
      this.analyzer = analyzer;
      Init(t, node, true, false, 3);
    }
    public PointsToAnalysis(Analyzer analyzer, TypeSystem t, Node node, bool interProcedural)
    {
      this.analyzer = analyzer;
      Init(t, node, interProcedural, false, 3);
    }
    public PointsToAnalysis(Analyzer analyzer, TypeSystem t, Node node,
        bool interProcedural, bool fixPoint)
    {
      this.analyzer = analyzer;
      Init(t, node, interProcedural, true, 3);
    }

    public PointsToAnalysis(Analyzer analyzer, TypeSystem t, Node node,
                bool interProcedural, bool fixpoint, int maxDepth)
    {
      this.analyzer = analyzer;
      Init(t, node, interProcedural, fixpoint, maxDepth);
    }

    public PointsToAnalysis(Visitor callingVisitor)
      : base(callingVisitor) { }
    #endregion

    #region Properties
    public ElementFactory Factory
    {
      get { return factory; }
    }
    public bool BoogieMode
    {
      get { return this.boogieMode; }
      set { this.boogieMode = value; }
    }

    public bool StandAloneApp
    {
      get { return this.standAloneApp; }
      set
      {
        this.standAloneApp = value;
        // For Statistics DELETE
        //  assumeMorePures = standAloneApp; 
      }

    }
    public bool IsInterProceduralAnalysys
    {
      get { return interProceduralAnalysis; }
    }
    public System.Compiler.Analysis.CallGraph CallGraph
    {
      get { return cg; }
    }
    #endregion

    #region Visitors (Class, TypeNode, Assembly or Method)
    /// <summary>
    /// We can filter a Node if we don't want to analyze it. 
    /// For the stand alone application...
    /// </summary>
    /// <param name="typeNode"></param>
    /// <returns></returns>
    public override TypeNode VisitTypeNode(TypeNode typeNode)
    {
      if (classFilter.Length != 0
          && !typeNode.FullName.Contains(classFilter))
        return typeNode;
      return base.VisitTypeNode(typeNode);
    }

    internal static TypeNode compilerGeneratedAttributeNode = TypeNode.GetTypeNode(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute));
    internal static bool IsCompilerGenerated(TypeNode t) {
      return t != null && t.GetAttribute(compilerGeneratedAttributeNode) != null;
    }
    internal static bool IsCompilerGenerated(Member m) {
      if (m == null) return false;
      if (m.GetAttribute(compilerGeneratedAttributeNode) != null) return true;
      TypeNode t = m.DeclaringType;
      while (t != null) {
        if (IsCompilerGenerated(t)) return true;
        t = t.DeclaringType;
      }
      return false;
    }
    internal Dictionary<TypeNode, PointsToState> enclosingState = new Dictionary<TypeNode, PointsToState>();
    public PointsToState EnclosingState(TypeNode c) {
      if (c.Template != null)
        c = c.Template;
      if(enclosingState.ContainsKey(c))
        return enclosingState[c];
      return null;
    }

    /// <summary>
    /// Idem previous
    /// </summary>
    /// <param name="c"></param>
    /// <returns></returns>
    public override Class VisitClass(Class c)
    {
      if (classFilter.Length != 0
          && !c.FullName.Contains(classFilter))
      {
        return c;
      }
      // This is to avoid direct analysis of closures classes
      if (IsCompilerGenerated(c)) 
      {
        if (this.EnclosingState(c) == null)
          return c;
      }
      return base.VisitClass(c);
    }
    public override DelegateNode VisitDelegateNode(DelegateNode delegateNode)
    {

      return base.VisitDelegateNode(delegateNode);
    }
    public override Method VisitMethod(Method method)
    {
      
      if (NeedToVerify(method))
      {
          if (!method.IsAbstract)
          {
              if (debug || verbose)
              {
                  Console.Out.WriteLine("Visiting {0}", method.FullName);
              }
              AnalysisForMethod(method);
          }
      }
      numberOfMethods++;

      return method;
    }
    // Overwritten in purity 
    public virtual bool NeedToVerify(Method method)
    {
      bool res = true;
      return res;
    }
    #endregion

    /// <summary>
    /// Entry point to analyze a given method. 
    /// Depending of the type of analysis a call to this method
    /// can lead to a fixpoint computation, the use of a precomputed
    /// intraprocedural analysis or performing the intraprocedural 
    /// analysis for the first time
    /// </summary>
    /// <param name="method"></param>
    /// <returns></returns>
    /// 
    public virtual void WholeProgramAnalysis()
    {
      if (cg != null)
      {
        //Set<CallGraph> cgs = cg.ComputeSCC();
        if (fixPointForMethods)
        {
          // FIXPOINT Computation. Bottom up traversal of the 
          // partial call-graph
          // Get a BFS ordered list of all method reachable
          // from method in the call graph
          // All these methods will be analyzed. 
          //List<Method> traversal = cg.BFSTraversal();
          methodsToAnalyze.Clear();
          // Reverse the traversal to analyze first the leaves
          //for (int i = traversal.Count - 1; i >= 0; i--)
          //    methodsToAnalyze.Add(traversal[i]);
          //FixPoint();
          List<Method> traversal = cg.TopologicalSort();
          for (int i = traversal.Count - 1; i >= 0; i--)
          {
            Method method = traversal[i];
            if (WasAnalyzed(method) /*&& !methodsToAnalyze.Contains(method)*/)
            {
              if (verbose)
                Console.Out.WriteLine("{0} was already analyzed!", method.Name);
              ComputeOnly(method);
            }
            else
            {
              methodsToAnalyze.Clear();
              List<Method> methodsInFixPoint = cg.GETSCCMethods(method);
              //methodsInFixPoint.AddRange(methodsToAnalyze);
              if (verbose && methodsInFixPoint.Count > 1)
                Console.Out.WriteLine("METODOS: {0}", methodsInFixPoint.Count);

              methodsToAnalyze.AddRange(methodsInFixPoint);
              Set<Method> analyzed = FixPoint();
              // Once the fixpoint was reached we register all those 
              // method as analyzed
              RegisterAnalyzedMethods(analyzed);

              //RegisterAnalyzedMethods(methodsInFixPoint);
              //methodsToAnalyze.RemoveAll(new Predicate<Method>(delegate(Method m)
              //    {return methodsInFixPoint.Contains(m);} ));
            }
          }

        }
      }
      else
      {
        throw new Exception("You cannot run a whole program analysis without a call graph...");
      }
    }

    public virtual Method AnalysisForMethod(Method method)
    {
      if (debug || verbose)
      {
        for (int i = 0; i < callStack.Count; i++)
          Console.Out.Write("\t");
        Console.Out.WriteLine("Analyzing {0}", method.FullName);
      }
      if (interProceduralAnalysis)
      {
        // Check if method was allready analyzed in 
        // when computing the analysis for another method
        if (WasAnalyzed(method))
        {
          // No pointsToRequired by maybe other analysis
          if (verbose)
            Console.Out.WriteLine("{0} was already analyzed!", method.FullName);
          ComputeOnly(method);
          return method;
        }
        else
        {
          if (fixPointForMethods)
          {
            // FIXPOINT Computation. Bottom up traversal of the 
            // partial call-graph
            // Get a BFS ordered list of all method reachable
            // from method in the call graph
            // All these methods will be analyzed. 
            List<Method> traversal = cg.BFS(method);
            methodsToAnalyze.Clear();
            // Reverse the traversal to analyze first the leaves
            for (int i = traversal.Count - 1; i >= 0; i--)
              methodsToAnalyze.Add(traversal[i]);

            Set<Method> analyzed = FixPoint();

            // Once the fixpoint was reached we register all those 
            // method as analyzed
            RegisterAnalyzedMethods(analyzed);
            //RegisterAnalyzedMethods(traversal);
          }
          else
          {
            // if is not in a fix point computation 
            // we just compute the analysis for the method
            // (top-down)
            AnalyzeMethod(method);
            RegisterAnalyzedMethod(method);
          }
        }
      }
      else
      {
        // If the analysis is not interprocedural, 
        // we just analyzed the selectec method
        AnalyzeMethod(method);
      }
      return method;
    }
    // This a CallBack for some that implements an analysis using this points-to analysiis
    protected virtual void ComputeOnly(Method method)
    {

    }

    #region FixPoint Computation
    /// <summary>
    /// Perform a fixpoint computation over a ser of methods (in general a strongly connected component).
    /// It perform the interprocedural analysis for each method, reanalysing any callers that require updating. 
    /// </summary>
    protected virtual Set<Method> FixPoint()
    {
      Set<Method> analyzed = new Set<Method>();

      Set<Method> methodsInFixPoint = new Set<Method>(methodsToAnalyze);
      while (methodsToAnalyze.Count != 0)
      {
        Method m = methodsToAnalyze[0];
        if (verbose)
          Console.Out.WriteLine("Now Analyzing {0} left: {2} Unsafe? {1} ", m.GetFullUnmangledNameWithTypeParameters(), IsUnsafe(m), methodsToAnalyze.Count);

        if (PointsToAnalysis.debug)
        {
          if (m.FullName.Contains("PaintDotNet.GradientRenderer.Render"))
          {
            //    System.Diagnostics.Debugger.Break();
          }
        }

        analyzed.Add(m);

        methodsToAnalyze.RemoveAt(0);

        bool hasChanged = false;
        // Perform the IntraProcedural of the Method 
        // if it wasn't analyzed before
        // Analyzed means that it was complety analyzed in a previous 
        // fix point computation, so its value is not going to change
        if (!WasAnalyzed(m))
          hasChanged = AnalyzeMethod(m);
        //else
        //    Console.Out.WriteLine(" was already analyzed!");
        // If a method changes, we have to reanalyze the callers
        if (hasChanged && IsAnalyzable(m))
        {
          // I should change this to cg.Callers(m)....
          foreach (Method caller in GetCallers(m))
          {
            if (!methodsToAnalyze.Contains(caller))
            {
              //if (alreadyAnalyzedMethods.Contains(caller) || methodsInFixPoint.Contains(caller))
              if (methodsInFixPoint.Contains(caller))
              {
                methodsToAnalyze.Add(caller);
                if (verbose)
                  Console.Out.WriteLine("\t reanalyzing {0}", caller.FullName);
              }
            }
          }
          //foreach (Method caller in GetCallers(m))
          //{
          //    Console.Out.WriteLine("\t Reanalyzing {0}", caller.GetFullUnmangledNameWithTypeParameters());
          //}
        }

        #region debugging, Delete this!
        if (verbose || debug)
        {
          counter++;
          if (counter % 1000 == 0)
          {
            Console.Out.WriteLine("Now Analyzing {0} Unsafe? {1} {2} To Analize: {3}",
                m.GetFullUnmangledNameWithTypeParameters(),
                IsUnsafe(m), counter, methodsToAnalyze.Count);
            foreach (Method mt in methodsToAnalyze)
            {
              Console.Out.WriteLine("\t  {0}", mt.GetFullUnmangledNameWithTypeParameters());
            }
          }
        }
        #endregion
      }
      return analyzed;
    }

    /// <summary>
    /// Check whether dataflow analysis changes or not
    /// </summary>
    /// <param name="m"></param>
    /// <param name="newResult"></param>
    /// <returns></returns>
    protected virtual bool AnalysisResultsChanges(Method m, PointsToState newResult)
    {
      bool res = false;
      if (HasSummary(m))
      {
        PointsToState oldResult = GetSummaryForMethod(m);
        //res = !oldResult.Equals(newResult);
        res = !oldResult.Includes(newResult);
        if (verbose && res)
        {
          Console.Out.WriteLine("Method {0} has changed:", m.FullName);
          Console.Out.WriteLine("DIFERENCE old vs new");
          oldResult.DumpDifference(newResult);
          Console.Out.WriteLine("DIFERENCE new vs old");
          newResult.DumpDifference(oldResult);
          //if (oldResult.writeEffects.Equals(newResult.writeEffects))
          //{
          //    Console.Out.WriteLine(oldResult.writeEffects);
          //    Console.Out.WriteLine(newResult.writeEffects);
          //    Console.Out.WriteLine("****");
          //}

        }
      }
      else
      {
        res = newResult != null;
      }

      return res;
    }

    public virtual void AddCallerToCallee(Method callee, Method caller)
    {
      Set<Method> ms;
      if (calleerToCallersMap.ContainsKey(callee))
        ms = calleerToCallersMap[callee];
      else
      {
        ms = new Set<Method>();
        calleerToCallersMap[callee] = ms;
      }
      ms.Add(caller);
    }

    protected virtual Set<Method> GetCallers(Method callee)
    {
      Set<Method> ms;
      if (calleerToCallersMap.ContainsKey(callee))
        ms = calleerToCallersMap[callee];
      else
        ms = new Set<Method>();

      return ms;
    }
    #endregion

    #region Methods for starting IntraProcedural analysis
    /// <summary>
    /// Analyze a given method.
    /// That is, perform the IntraProdecural Dataflow Analysis
    /// </summary>
    /// <param name="method"></param>
    /// <returns></returns>
    public virtual bool AnalyzeMethod(Method method)
    {
      //if (debug || verbose)
      //{
      //    for (int i = 0; i < callStack.Count; i++)
      //        Console.Out.Write("\t");
      //    Console.Out.WriteLine("Analyzing {0}", method.FullName);
      //}
        if (method.FullName.Contains("Linq9"))
        {
        }

      PointsToState cState;
      cState = GetSummaryForMethodWithDefault(method);

      PointsToInferer pta = CreateIntraProcAnalysis();
      Method m = method;
      BeforeComputeInfererForMethod(m, pta);
      // Computes the dataflow analysis for the method
      bool isPure = pta.ComputePointsToStateFor(m);

      /// Join the new computed value with the previous one


      if (pta.ExitState != null)
      {
        pta.ExitState.Join(cState);
        pta.ExitState.pointsToGraph = pta.ExitState.pointsToGraph.Simplify();
      }
      else

        pta.exitState = cState;

      // Check if this round of the analysis generates new information

      AfterComputeInfererForMethod(m, pta);

      bool hasChanged;
      hasChanged = AnalysisResultsChanges(method, pta.ExitState);



      #region only for testing
      //if (hasChanged && PointsToAnalysis.counter>4000)
      //{
      //    Console.Out.WriteLine("NEW");
      //    Console.Out.WriteLine(pta.PointsToWE.pointsToGraph);
      //    Console.Out.WriteLine("OLD");
      //    Console.Out.WriteLine(GetPurityAnalysisWithDefault(method).PointsToGraph);
      //    Console.Out.WriteLine("DIFERENCE a vs b");
      //    pta.PointsToWE.pointsToGraph.DumpDifference(GetPurityAnalysisWithDefault(method).PointsToGraph);
      //    Console.Out.WriteLine("DIFERENCE b vs a");
      //    GetPurityAnalysisWithDefault(method).PointsToGraph.DumpDifference(pta.PointsToWE.pointsToGraph);
      //    Console.Out.WriteLine(pta.PointsToWE.WriteEffects);
      //    Console.Out.WriteLine(GetPurityAnalysisWithDefault(method).WriteEffects);
      //}
      #endregion

      // Save the results
      //SetPurity(m, isPure);
      pta.ExitState.pointsToGraph = pta.ExitState.pointsToGraph.Simplify();
      SetSummaryForMethod(m, pta.ExitState);

      return hasChanged;
    }

    protected virtual void BeforeComputeInfererForMethod(Method m, PointsToInferer pta)
    {

    }

    protected virtual void AfterComputeInfererForMethod(Method m, PointsToInferer pta)
    {

    }
    // This methods act a a factory of the intraproc analysis
    public virtual PointsToInferer CreateIntraProcAnalysis()
    {
      return new PointsToInferer(this.typeSystem, this);
    }
    public virtual ControlFlowGraph GetCFG(Method method)
    {
      ControlFlowGraph cfg;
      if (analyzer != null)
        return analyzer.GetCFG(method);

      if (cfgRepository.Contains(method))
        cfg = (ControlFlowGraph)cfgRepository[method];
      else
      {
        cfg = ControlFlowGraph.For(method);
        cfgRepository.Add(method, cfg);
      }
      return cfg;
    }
    #endregion


    #region Method for registering and querying the analysis results

    public PointsToState GetSummaryForMethod(Method m)
    {
      //String mName = m.GetFullUnmangledNameWithTypeParameters();
      //return pointsToWEMap[mName];
      return pointsToWEMap[m];
    }

    public PointsToState GetSummaryForMethodWithDefault(Method m)
    {
      //String mName = m.GetFullUnmangledNameWithTypeParameters();
      if (HasSummary(m))
        return GetSummaryForMethod(m);
      else
      {
        PointsToState cStatei = factory.NewElement(m);
        return cStatei;
      }
    }

    public void SetSummaryForMethod(Method m, PointsToState cState)
    {
      //String mName = m.GetFullUnmangledNameWithTypeParameters();
      //pointsToWEMap[mName] = cState;
        if(m.FullName.Contains("Where"))
        {
        }
      pointsToWEMap[m] = cState;
    }

    public void SetDefaultSummaryForMethod(Method m)
    {
      SetSummaryForMethod(m, factory.NewElement(m));
    }

    public bool HasSummary(Method m)
    {
      //String mName = m.GetFullUnmangledNameWithTypeParameters();
      //return pointsToWEMap.ContainsKey(mName);
      return pointsToWEMap.ContainsKey(m);
    }
    #endregion

    #region Methods for Registering and querying if a method was already analyzed
    /// <summary>
    /// Determines if the method was analyzed 
    /// </summary>
    /// <param name="m"></param>
    /// <returns></returns>
    public virtual bool WasAnalyzed(Method m)
    {
      // bool wasAnalyzed = PurityAnalysisHasMethod(m);
      bool wasAnalyzed = alreadyAnalyzedMethods.Contains(m);
      return wasAnalyzed;
    }
    protected virtual void RegisterAnalyzedMethods(System.Collections.Generic.ICollection<Method> ms)
    {
      foreach (Method m in ms)
        RegisterAnalyzedMethod(m);
    }

    protected virtual void RegisterAnalyzedMethod(Method m)
    {
        if (m.Name.Name.StartsWith("Bar"))
        {
        }
      alreadyAnalyzedMethods.Add(m);
    }
    public Set<Method> AnalyzedMethods()
    {
      return alreadyAnalyzedMethods;
    }
    #endregion



    #region Methods to determine is a Method is Analyzable
    /// <summary>
    /// Determines whether the method is analyzable or not
    /// for interProc call.
    /// That is, if we can get the method body 
    /// (not abstract, not interface, under our desired analysis scope)
    /// </summary>
    /// <param name="m"></param>
    /// <returns></returns>
    public virtual bool IsAnalyzable(Method m)
    {
      String mName = m.GetFullUnmangledNameWithTypeParameters();
      //bool isAnalyzable = analyzableMethods.Contains(mName);
      bool isAnalyzable = true;
      if (fixPointForMethods)
        isAnalyzable = analyzableMethods.Contains(m);
      else
        isAnalyzable = IsInCurrenModule(m);

      // isAnalyzable = !fixPointForMethods || analyzableMethods.Contains(m);

      Method templateOrMethod = solveMethodTemplate(m);

      isAnalyzable = isAnalyzable
          && IsPossibleAnalyzableMethod(templateOrMethod);

      // Method templateOrMethod = m;

      bool isConstructorTemplateDeclaredPure = IsContructorTemplatePure(m);

      isAnalyzable = isAnalyzable
                  && isConstructorTemplateDeclaredPure
                  && (!templateOrMethod.IsAbstract)
                  && (templateOrMethod.Body != null
                  && templateOrMethod.Body.Statements != null
                  && templateOrMethod.Body.Statements.Count >= 0);

      return isAnalyzable;
    }

    internal virtual bool IsInCurrenModule(Method m)
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

      if (moduleFilter.Length != 0
              && !m.DeclaringType.FullName.Contains(moduleFilter))
      {
        res = false;
      }



      return res;
    }
    internal virtual bool IsPossibleAnalyzableMethod(Method m)
    {
      bool isAnalyzable = true;
      isAnalyzable = isAnalyzable && !IsUnsafe(m) && !m.IsExtern;
      isAnalyzable = isAnalyzable && m.DeclaringType != null && !m.DeclaringType.FullName.StartsWith("System.IO")
          && !m.DeclaringType.FullName.StartsWith("System.Console");

      bool isInterface = m.DeclaringType != null && CciHelper.IsInterface(m.DeclaringType);
      bool isAbstract = CciHelper.IsAbstract(m);

      isAnalyzable = isAnalyzable && !isInterface && !isAbstract;

      return isAnalyzable;
    }

    protected virtual bool IsContructorTemplatePure(Method m)
    {

      if (m.DeclaringType == null || m.Template == null
          || m.Template.DeclaringType == null
          || m.Template.DeclaringType.TemplateParameters == null)
        return true;

      TypeNode templateInstanceType = m.DeclaringType;
      TypeNode templateType = m.Template.DeclaringType;
      bool res = true;

      if (templateInstanceType.TemplateArguments != null)
      {
        for (int i = 0, n = templateInstanceType.TemplateArguments.Count; i < n; i++)
        {
          TypeNode arg = templateInstanceType.TemplateArguments[i];
          if (arg != null)
          {
            if (templateType.TemplateParameters[i] != null)
            {
              ITypeParameter tpar = templateType.TemplateParameters[i] as ITypeParameter;
              if (tpar != null)
              {
                if ((tpar.TypeParameterFlags & TypeParameterFlags.DefaultConstructorConstraint) != 0)
                {
                  Method ctor = arg.GetConstructor();
                  if (!PointsToAndEffectsAnnotations.IsDeclaredPure(ctor))
                  {
                    res = false;
                  }
                }
              }
            }
          }
        }
      }
      //if ((tp.TypeParameterFlags & TypeParameterFlags.DefaultConstructorConstraint) != 0)
      {
        // m.Template.DeclaringType.GetAttribute(Ar
        // m.IsWriteConfined
      }
      return res;
    }

    // Check if method is IsUnsafe 
    public static bool IsUnsafe(Method m)
    {
      bool isUnsafe = m.IsUnsafe;
      if (m.Parameters != null)
        foreach (Parameter p in m.Parameters)
        {
          if (p.Type != null && p.Type.IsPointerType)
          {
            isUnsafe = true;
            break;
          }
        }
      return isUnsafe;
    }
    #endregion

    #region Methods to deal with templates and subclasses
    public Set<Method> GetTemplateInstanceConstructor(Method m)
    {
      Set<Method> constructors = new Set<Method>();
      if (m.DeclaringType == null || m.Template == null || m.Template.DeclaringType == null)
        return constructors;
      TypeNode templateInstanceType = m.DeclaringType;
      TypeNode templateType = m.Template.DeclaringType;

      if (templateInstanceType.TemplateArguments != null)
      {
        for (int i = 0, n = templateInstanceType.TemplateArguments.Count; i < n; i++)
        {
          TypeNode arg = templateInstanceType.TemplateArguments[i];
          if (arg != null)
          {
            if (templateType.TemplateParameters[i] != null)
            {
              ITypeParameter tpar = templateType.TemplateParameters[i] as ITypeParameter;
              if (tpar != null)
              {
                if ((tpar.TypeParameterFlags & TypeParameterFlags.DefaultConstructorConstraint) != 0)
                {
                  Method ctor = arg.GetConstructor();
                  constructors.Add(ctor);
                }
              }
            }
          }
        }
      }
      return constructors;
    }

    // Get the correspoding template from an instance method;

    /// <summary>
    /// Get the method's template if it has it
    /// </summary>
    /// <param name="callee"></param>
    /// <returns></returns>
    internal static Method solveMethodTemplate(Method callee)
    {
      if (callee.Template != null)
        return callee.Template;

      return callee;
    }

    internal static Method solveInstance(TypeNode type, Method callee, out bool solved)
    {

      Method instanceMethod = type.GetImplementingMethod(callee, false);
      if (instanceMethod == null)
      {
        solved = false;
        return callee;
        // Console.Out.WriteLine();
      }
      solved = true;
      return instanceMethod;
    }
    #endregion
  }

}
