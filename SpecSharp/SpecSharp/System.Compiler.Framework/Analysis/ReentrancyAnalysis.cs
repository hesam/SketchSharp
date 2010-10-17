//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;
using AbstractValue = System.Compiler.MathematicalLattice.Element;
using System.Collections;
using System.Compiler;
using System.Compiler.Diagnostics;
using Microsoft.Contracts;
using System.Compiler.Analysis.PointsTo;
using System.Compiler.WPurity;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler.Analysis{
#endif
    public class Receiver 
    {
        IPTAnalysisNode n;
        bool clausure;
        public Receiver(IPTAnalysisNode n, bool clausure)
        {
            this.n = n;
            this.clausure = clausure;
        }
        public bool IsClausure { get { return clausure; } set { clausure = value; } }
        public IPTAnalysisNode Node { get { return n; } }
        
        public override bool Equals(object o)
        {
            Receiver r = o as Receiver;
            return r!=null && this.Node.Equals(r.Node) && this.IsClausure==r.IsClausure;
        }
        public override int  GetHashCode()
        {
            return this.n.GetHashCode()+this.IsClausure.GetHashCode();
        }
        public override string  ToString()
        {
 	         return "["+ n.ToString()+(IsClausure?"*":"")+"]";
        }
    }
    public class Receivers : Set<Receiver>
    {
        int numLoads = 0;
        public Receivers()
            : base()
        {
        }
        public static new Receivers Empty
        {
            get { return new Receivers(); }
        }
        public void Add(IPTAnalysisNode n, bool isClausure)
        {
            Receiver r = new Receiver(n, isClausure);
            Add(r);
        }
        public Receivers(Receivers rs)
            : base(rs)
        {
            AddRange(rs);
            numLoads = rs.numLoads;
        }
        public void AddOnlyLoads(Receivers rs)
        {
            foreach (Receiver r in rs)
                if (!r.Node.IsInside)
                    Add(r);
        }
        public void AddNodes(Nodes ns)
        {
            foreach (IPTAnalysisNode n in ns)
               Add(n,false);
        }
        public Nodes Nodes 
        {
            get { 
                Nodes res = Nodes.Empty; 
                foreach (Receiver r in this) 
                    res.Add(r.Node); 
                return res; 
            } 
        }
        public override string ToString()
        {
            string res = "{";
            foreach (Receiver r in this)
                res = res + r + " ";
            res = res + "}\n"; ;
            return res;
        }
    }

    interface IReentrancyInfo
    {
        bool IsReentrant(Statement s);
    }
    #region PointsTo and Reentrancy
    /// <summary>
    /// Represents the pointsTo and the write effects of the current block
    /// That is cState = &lt; PtGraph , WriteEffecs , NonAnalyzableCalls &gt; 
    /// It is a semilattice (bottom , &lt;=)
    /// </summary>
    public class Reentrancy : ConsistencyState /*PoinstToState */, IReentrancyInfo, IDataFlowState
    {
        #region Atttibutes
        #region Semilatice
        /// <summary>
        /// This is the Semilattice
        /// </summary>
        // internal Receivers receivers;
        internal Edges callEdges;
        bool reentrantCall = false;
        // Set<Label> ofendingCallees = new Set<Label>();
        //Set<Pair<Label, int>> ofendingCallees = new Set<Pair<Label, int>>();
        Dictionary<Label, int> ofendingCallees = new Dictionary<Label, int>();
        Dictionary<Label, Set<IPTAnalysisNode>> ofendingReceivers = new Dictionary<Label, Set<IPTAnalysisNode>>();


        protected Dictionary<Label, Set<VariableEffect>> inconsistentExpr;
        protected Dictionary<Label, String> problems;
        protected Set<Label> callsToWarn;


        // internal bool inExposedBlock = false;

        #endregion
        /// <summary>
        /// Method under analysis (MUA)
        /// </summary>
        #endregion

        #region Properites
        // REENTRANCY ANALYSIS
        //public Receivers Receivers
        //{
        //    get { return receivers;  }
        //}
        public Edges CallEdges
        {
            get { return callEdges; }
        }
        // public Set<Label> OfendingCallees
        public Dictionary<Label,int> OfendingCallees
        {
            get { return ofendingCallees;  }
        }
        public bool ReentrantCall
        {
            get { return reentrantCall;  }
        }
        #endregion

        #region Constructors
        // Constructor
        public Reentrancy(Method m,PointsToAnalysis pta): base(m,pta)
        {
            // this.receivers = new Receivers();
            this.callEdges = new Edges();
            // this.ofendingCallees = new Set<Label>();
            // this.ofendingCallees = new Set<Pair<Label,int>>();
            this.ofendingCallees = new Dictionary<Label, int>();
            this.ofendingReceivers = new Dictionary<Label, Set<IPTAnalysisNode>>();
            this.reentrantCall = false;
            
            // this.inExposedBlock = false;

            inconsistentExpr = new Dictionary<Label, Set<VariableEffect>>();
            problems = new Dictionary<Label, string>();
            callsToWarn = new Set<Label>();
        }

        // Copy Constructor
        public Reentrancy(Reentrancy cState): base(cState)
        {
            // receivers = cState.receivers;
            callEdges = new Edges(cState.callEdges);
            reentrantCall = cState.reentrantCall;
            ofendingCallees = new Dictionary<Label,int>(cState.ofendingCallees);
            ofendingReceivers = new Dictionary<Label, Set<IPTAnalysisNode>>(cState.ofendingReceivers);
            
            //this.inExposedBlock = cState.inExposedBlock;
            problems = new Dictionary<Label, string>(cState.problems);
            inconsistentExpr = new Dictionary<Label, Set<VariableEffect>>(cState.inconsistentExpr);
            this.callsToWarn = new Set<Label>(cState.callsToWarn);
        }


        public Reentrancy Bottom
        {
            get
            {
                return new Reentrancy(this.Method,this.pta);
            }
        }

        // Get the least elements representing the bottom for the lattice of m
        //public static Reentrancy BottomFor(Method m)
        //{
        //    return new Reentrancy(m);
        //}
        #endregion

        #region SemiLattice Operations (Join, Includes, IsBottom)
        private  void AddOffendingCall(Label lb, int i)
        {
            AddOffendingCall(lb, i, Nodes.Empty);
        }
        private void AddOffendingCall(Label lb, int i, Set<IPTAnalysisNode> receivers)
        {
            ofendingCallees[lb] = i;
            ofendingReceivers[lb] = receivers;
        }
        public override bool IsBottom
        {
            get 
            { return pointsToGraph.IsBottom 
                //&& receivers.Count==0 
                && callEdges.Count==0
                && isDefault; 
            }
        }

        public override bool IsTop
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        public override PointsToState Copy()
        {
            return new Reentrancy(this);
        }       
        
        /// <summary>
        ///  Join two PointsToAndWriteEffects
        /// </summary>
        /// <param name="ptgWe"></param>
        public override void Join(PointsToState ptg)
        {
            base.Join(ptg);
            if (ptg != null)
            {
                Reentrancy ptgR = (Reentrancy)ptg;
                //if (receivers != ptgR.receivers)
                //    receivers.AddRange(ptgR.receivers);

                if (callEdges != ptgR.callEdges)
                    callEdges.AddEdges(ptgR.callEdges);

                reentrantCall = reentrantCall || ptgR.reentrantCall;
                if (ofendingCallees != ptgR.ofendingCallees)
                    JoinOffendingCalless(ptgR.ofendingCallees);
                if (ofendingReceivers != ptgR.ofendingReceivers)
                    JoinOffendingReceivers(ptgR.ofendingReceivers);

                if (inconsistentExpr != ptgR.inconsistentExpr)
                    JoinInconsistentExp(ptgR.inconsistentExpr);
                if (problems != ptgR.problems)
                    JoinProblems(ptgR.problems);


                if (callsToWarn != ptgR.callsToWarn)
                    callsToWarn.AddRange(ptgR.callsToWarn);

                // this.inExposedBlock = this.inExposedBlock && ptgR.inExposedBlock;
            }
        }

        public void JoinOffendingCalless(Dictionary<Label, int> of2)
        {
            //ofendingCallees.AddRange(of2);
            foreach (Label lb in of2.Keys)
            {
                int of2R = of2[lb];
                if (ofendingCallees.ContainsKey(lb))
                {
                    int of1R = this.ofendingCallees[lb];
                    if (of2R < of1R)
                        ofendingCallees[lb] = of2R;
                }
                else
                    ofendingCallees[lb] = of2R;
            }

        }
        public void JoinProblems(Dictionary<Label, String> of2)
        {
            //ofendingCallees.AddRange(of2);
            foreach (Label lb in of2.Keys)
            {
                string of2R = of2[lb];
                if (problems.ContainsKey(lb))
                {
                    string of1R = this.problems[lb];
                    if (!of2R.Equals(of1R))
                        problems[lb] = of1R+";"+of2R;
                }
                else
                    problems[lb] = of2R;
            }

        }

        public void JoinOffendingReceivers(Dictionary<Label, Set<IPTAnalysisNode>> of2)
        {
            foreach (Label lb in of2.Keys)
            {
                Set<IPTAnalysisNode> of2R = of2[lb];
                if (ofendingReceivers.ContainsKey(lb))
                {
                    Set<IPTAnalysisNode> of1R = this.ofendingReceivers[lb];
                    if(of1R!=of2R)
                        of1R.AddRange(of2R);
                    // ofendingReceivers[lb].AddRange(of2R);
                }
                else
                    ofendingReceivers[lb] = of2R;
            }

        }

        public void JoinInconsistentExp(Dictionary<Label, Set<VariableEffect>> of2)
        {
            foreach (Label lb in of2.Keys)
            {
                Set<VariableEffect> of2R = of2[lb];
                if (this.inconsistentExpr.ContainsKey(lb))
                {
                    Set<VariableEffect> of1R = this.inconsistentExpr[lb];
                    if (of1R != of2R)
                        of1R.AddRange(of2R);
                    //inconsistentExpr[lb].AddRange(of2R);
                }
                else
                    inconsistentExpr[lb] = of2R;
            }

        }



        /// <summary>
        ///  Inclusion check for two PointsToAndWriteEffects
        /// </summary>
        /// <param name="ptgWe"></param>

        public override bool Includes(PointsToState cState2)
        {
            bool includes = base.Includes(cState2);
            Reentrancy cR = (Reentrancy)cState2;
            //includes = includes && receivers.Includes(cR.receivers);
            includes = includes && cR.callEdges.IsSubset(callEdges);
            return includes;
        }
        #endregion

        public bool IsReentrant(Statement s)
        {
            Label l = new Label(s, this.Method);
            return ofendingCallees.ContainsKey(l);
        }
        // requires IsReentrant(s)
        public Set<IPTAnalysisNode> OffendingReceivers(Statement s)
        {
            Label l = new Label(s, this.Method);
            return ofendingReceivers[l];
        }
        public int OffendingRule(Statement s)
        {
            Label l = new Label(s, this.Method);
            return ofendingCallees[l];
        }

        #region Transfer Function related methods
        #region Method Calls Support
        /// <summary>
        /// Register effect of the non-analyzable call in the pointsToGraph inferring the information from the callee annotations
        /// </summary>
        /// <param name="vr"></param>
        /// <param name="callee"></param>
        /// <param name="receiver"></param>
        /// <param name="arguments"></param>
        /// <param name="lb"></param>
        public override void ApplyNonAnalyzableCall(Variable vr, Method callee, Variable receiver, 
            ExpressionList arguments, Label lb, out InterProcMapping ipm)
        {
            ApplyNonAnalyzableCallReentrancyUsingEdges(callee, receiver, arguments, lb);
            base.ApplyNonAnalyzableCall(vr, callee, receiver, arguments, lb, out ipm);
        }

        private void ApplyNonAnalyzableCallReentrancyUsingEdges(Method callee, Variable receiver, 
            ExpressionList arguments, Label lb)
        {
            // ** For reentrancy analysis
            Receivers potentialCalleeRecievers = Receivers.Empty;
            Receivers allRecievers = Receivers.Empty;
            
            if (receiver != null)
            {
                
                Nodes values = pointsToGraph.GetValuesIfEmptyLoadNode(receiver, lb);
                potentialCalleeRecievers.AddNodes(values);
                
                allRecievers.AddRange(CalleeNodesReachableObjectsAsReceivers(receiver, lb,
                    PointsToAndEffectsAnnotations.IsDeclaredWriteConfined(callee)));
            }
            if (arguments != null)
            {
                foreach (Expression arg in arguments)
                {
                    if (arg is Variable)
                    {
                        Variable v = arg as Variable;
                        allRecievers.AddRange(CalleeNodesReachableObjectsAsReceivers(v, lb, PointsToAndEffectsAnnotations.IsDeclaredWriteConfined(callee)));
                    }
                }
            }
            // Any object reachable from the parameters or self can be a receiver inside the method
            // So we can check if they don't intersect with "self".
            Nodes thisVariableValues = Nodes.Empty;
            if (!CciHelper.IsConstructor(callee))
            {
                if (!Method.IsStatic)
                {

                    thisVariableValues = pointsToGraph.GetValuesIfEmptyLoadNode(Method.ThisParameter, pointsToGraph.MethodLabel);
                    // if (thisVariableValues.Intersection(potentialCalleeRecievers.Nodes).Count != 0)
                    if (!thisVariableValues.Intersection(potentialCalleeRecievers.Nodes).IsEmpty)
                    {
                        reentrantCall = true;
                        AddOffendingCall(lb, 1,thisVariableValues.Intersection(potentialCalleeRecievers.Nodes));
                    }
                    else
                    {
                        // if (thisVariableValues.Intersection(allRecievers.Nodes).Count != 0)
                        if (!thisVariableValues.Intersection(allRecievers.Nodes).IsEmpty)
                        {
                            // A call to this 
                            reentrantCall = true;
                            AddOffendingCall(lb, 0, thisVariableValues.Intersection(allRecievers.Nodes));
                        }
                    }
                }
            }
            // receivers.AddRange(potentialCalleeRecievers);
            
            // OJO!!!!
            foreach (Receiver r in allRecievers)
            {
                if (/*false && */r.Node.IsLoad)
                {
                    r.IsClausure = true;
                    r.Node.IsClousure = true;
                }
                // if (!r.Node.IsInside)
                //    receivers.Add(r);
                
           }
           Edges auxCallEdges = new Edges(callEdges); 
           AddEdges(potentialCalleeRecievers.Nodes, thisVariableValues,auxCallEdges);
           callEdges = ClausureAndClean(auxCallEdges);
            // ** END Reentrancy
        }


        #region Reentrancy for non analyzable calls using sets (deprecated)
        //private void ApplyNonAnalyzableCallReentrancyUsingSet(Method callee, Variable receiver, 
        //    ExpressionList arguments, Label lb)
        //{
        //    // ** For reentrancy analysis
        //    Receivers potentialCalleeRecievers = Receivers.Empty;
        //    if (receiver != null)
        //    {
        //        potentialCalleeRecievers.AddRange(CalleeNodesReachableObjectsAsReceivers(receiver, lb, callee.IsWriteConfined));
        //    }
        //    if (arguments != null)
        //    {
        //        foreach (Expression arg in arguments)
        //        {
        //            if (arg is Variable)
        //            {
        //                Variable v = arg as Variable;
        //                potentialCalleeRecievers.AddRange(CalleeNodesReachableObjectsAsReceivers(v, lb, callee.IsWriteConfined));
        //            }
        //        }
        //    }
        //    // Any object reachable from the parameters or self can be a receiver inside the method
        //    // So we can check if they don't intersect with "self".
        //    if (!CciHelper.IsConstructor(callee))
        //    {
        //        if (!Method.IsStatic)
        //        {

        //            Nodes thisVariableValues = pointsToGraph.GetValuesIfEmptyLoadNode(Method.ThisParameter, pointsToGraph.MethodLabel);
        //            // if (thisVariableValues.Intersection(potentialCalleeRecievers.Nodes).Count != 0)
        //            if (thisVariableValues.IntersectionNotEmpty(potentialCalleeRecievers.Nodes))
        //            {
        //                // A call to this 
        //                reentrantCall = true;
        //                AddOffendingCall(lb, 0);
        //            }
        //        }
        //    }
        //    // receivers.AddRange(potentialCalleeRecievers);
        //    foreach (Receiver r in potentialCalleeRecievers)
        //    {
        //        if (r.Node.IsLoad)
        //        {
        //            r.IsClausure = true;
        //        }
        //        if (!r.Node.IsInside)
        //            receivers.Add(r);
        //    }

        //    // ** END Reentrancy
        //}
        #endregion

        /// <summary>
        /// Register all nodes reachable from the argument as potencial receivers
        /// </summary>
        /// <param name="v"></param>
        /// <param name="lb"></param>
        /// <param name="confined"></param>
        private Receivers CalleeNodesReachableObjectsAsReceivers(Variable v, Label lb, bool confined)
        {
            Receivers res = Receivers.Empty;
            // Use Filters for !owned!!!!!!
            Nodes reachables = PointsToGraph.NodesForwardReachableFromVariable(v, confined);
            foreach (IPTAnalysisNode n in reachables)
            {
                //if (n is LValueNode || n.IsGlobal)
                if (n.IsObjectAbstraction)
                {
                    res.Add(n,false);
                }
            }
            return res;
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
        public override void  ApplyAnalyzableCall(Variable vr, Method callee, Variable receiver, ExpressionList arguments, PointsToState calleecState, Label lb, out InterProcMapping ipm)
        {
            
            if (callee.Name.Name.Equals("m"))
            {}
            if (callee.Name.Name.Equals("Update"))
            { }

            if (this.Method.Name.Name.StartsWith("Main"))
            { }
            

            
            base.ApplyAnalyzableCall(vr, callee, receiver, arguments, calleecState, lb, out ipm);

            
        }

        protected override void BeforeBindCallWithCallee(PointsToState callerPTS, PointsToState calleePTS, Variable receiver, ExpressionList arguments, Variable vr, Label lb, InterProcMapping ipm)
        {
            base.BeforeBindCallWithCallee(callerPTS, calleePTS, receiver, arguments, vr, lb, ipm);
            Reentrancy reentrancyCalleeState = (Reentrancy)calleePTS;

            Receivers potentialReceivers = Receivers.Empty;
            // ** FOR REENTRANCY ANALYSIS
            if (receiver != null)
            {
                Nodes values = pointsToGraph.GetValuesIfEmptyLoadNode(receiver, lb);
                potentialReceivers.AddNodes(values);
            }
             bool isReentrant = false;
             
             // Check Exposure por every parameters
             if(!IsMethodToIgnoreForReentrancy(reentrancyCalleeState.Method))
             {
                isReentrant= ReentrancyAnalysisForAnalyzableCalls(reentrancyCalleeState.Method, reentrancyCalleeState, lb,
                    potentialReceivers, ipm);

                // CheckExposureForCall(calleePTS, receiver, arguments, lb, isReentrant);
             }
        }

        protected virtual void CheckExposureForCall(PointsToState calleePTS, Variable receiver, ExpressionList arguments, Label lb, bool isReentrant)
        {
            if (!isReentrant)
            {

                // Receiver is not consistent and callee requires it consistent
                if (receiver != null
                    && MethodRequiresExposable(calleePTS.Method, calleePTS.Method.ThisParameter)
                    && !IsExposable(receiver)  /*&& !IsExposed(receiver)*/ )
                {
                    InformNeedConsistencyForVar(lb, receiver);
                }
                for (int i = 0; i < arguments.Count; i++)
                {
                    if (arguments[i] is Variable)
                    {
                        Variable argVar = (Variable)arguments[i];
                        if (MethodRequiresExposable(calleePTS.Method, calleePTS.Method.Parameters[i])
                        && !IsExposable(argVar) /*&& !IsExposed(argVar)*/)
                        {
                            InformNeedConsistencyForVar(lb, argVar);
                        }

                    }
                }
            }
        }

        protected bool IsMethodToIgnoreForReentrancy(Method callee)
        {
            bool res =IsMethodToIgnore(callee);
            res = res || CciHelper.IsConstructor(Method);
            res = res || CciHelper.IsConstructor(callee);
            return res;
        }
        
        protected override void BeforeUpdateConsystency(Variable vr, Method callee, Variable receiver, ExpressionList arguments, 
            PointsToState calleePTS, Label lb, InterProcMapping ipm)
        {
            base.BeforeUpdateConsystency(vr, callee, receiver, arguments, calleePTS, lb,ipm);
        }

        /// <summary>
        /// This is the MAIN reentrancy analysis rutine
        /// </summary>
        /// <param name="callee"></param>
        /// <param name="calleecState"></param>
        /// <param name="lb"></param>
        /// <param name="potentialReceivers"></param>
        /// <param name="ipm"></param>
        /// <returns></returns>
        private bool ReentrancyAnalysisForAnalyzableCalls(Method callee, Reentrancy calleecState,
            Label lb, Receivers potentialReceivers, InterProcMapping ipm)
        {
            // ** FOR REENTRANCY ANALYSIS
            // To see if the reciever or some transitive callee caller may be an alias of "self"
            if (callee.Name.Name.Equals("Update"))
            { }
            if (Method.Name.Name.Equals("m"))
            { }
            if (Method.Name.Name.Equals("testProblem"))
            { }
            if (callee.Name.Name.Equals("n2"))
            { }
            bool rule3b;
            Nodes withnesses;
            rule3b = CheckRule3UsingEdges(calleecState, ipm, out withnesses);

            Nodes bindedPotentialCalleeReceivers = GetBindedCalleePotentialReceiversUsingCallEdges(calleecState, ipm);

            ReentrancyAnalysis ra = (ReentrancyAnalysis)this.pta;
            bool thisCallIsReentrant = false;
           

            Nodes thisVariableValues = Nodes.Empty;

            if (!CciHelper.IsConstructor(callee))
            {
                // Rules 1 && 2

                if (!Method.IsStatic)
                {
                    thisVariableValues =
                        pointsToGraph.GetValuesIfEmptyLoadNode(Method.ThisParameter, pointsToGraph.MethodLabel);

                    // Rule 1 = Inmediate Call
                    // bool rule1 = thisVariableValues.Intersection(potentialReceivers.Nodes).Count != 0;
                    bool rule1 = !thisVariableValues.Intersection(potentialReceivers.Nodes).IsEmpty;

                    bool rule2 = CheckRule2UsingEdges(bindedPotentialCalleeReceivers, thisVariableValues);

                    // A reentrant call to "this" 
                    if (rule1 || rule2)
                    {
                        int rule = 1;
                        Set<IPTAnalysisNode> offendingNodes = Nodes.Empty;
                        if (rule1)
                        {
                            rule = 1;
                            offendingNodes = thisVariableValues.Intersection(potentialReceivers.Nodes);

                        }
                        else
                        {
                            rule = 2;
                            offendingNodes = thisVariableValues.Intersection(bindedPotentialCalleeReceivers);
                        }

                        if (!IsExposable(offendingNodes) 
                            && MethodRequiresInvariant(callee)
                            /*&& !IsExposed(offendingNodes)*/)
                        {
                            //System.Diagnostics.Debugger.Break();
                            problems[lb] = "";
                            Set<VariableEffect> vEffs = this.ComputePathsFromVariableToHeapLocation(offendingNodes, lb);
                            foreach (VariableEffect ve in vEffs)
                            {
                                string msg = string.Format("reentract call on {0} when invariant may not hold", ve.ToString());
                                
                                ra.HandleError(lb.Statement, lb.Statement, Error.GenericWarning, msg);

                                problems[lb] = problems[lb] + msg +";";
                            }
                            callsToWarn.Add(lb);
                            inconsistentExpr[lb] = vEffs;

                            thisCallIsReentrant = true;
                            reentrantCall = true;
                            AddOffendingCall(lb, rule, offendingNodes);
                        }
                        

                    }                    
                }
            }

            if (rule3b && !thisCallIsReentrant/*!reentrantCall*/)
            {
                reentrantCall = true;
                //if (!IsExposed(withnesses))
                {
                    //System.Diagnostics.Debugger.Break();
                    problems[lb] = "";
                    
                    Set<VariableEffect> vEffs = this.ComputePathsFromVariableToHeapLocation(withnesses, lb);
                    /*
                    foreach (VariableEffect ve in vEffs)
                    {
                        string msg = string.Format("{0} has to be consistent", ve.ToString());
                        ra.HandleError(lb.Statement, lb.Statement, Error.GenericWarning, msg);

                        problems[lb] = problems[lb] + msg+";" ;
                    }
                    */
                    string msg = string.Format("{0} may have a reentrant and the invariant may not hold", callee.Name);
                    ra.HandleError(lb.Statement, lb.Statement, Error.GenericWarning, msg);

                    problems[lb] = problems[lb] + msg + ";";
                    callsToWarn.Add(lb);
                    inconsistentExpr[lb] = vEffs;

                    AddOffendingCall(lb, 3, withnesses);
                }
            }

            //if (!reentrantCall)
            if(!thisCallIsReentrant/*!reentrantCall*/)
            {
                
                if (!IsExposable(thisVariableValues) && !IsExposable(potentialReceivers.Nodes))
                {
                    bool rule4 = CheckRule4UsingEdges(calleecState, potentialReceivers.Nodes,
                        thisVariableValues, ipm, out withnesses);
                    if (rule4 && !IsExposed(withnesses))
                    {
                        //System.Diagnostics.Debugger.Break();
                        problems[lb] = "";
                        Set<VariableEffect> vEffs = this.ComputePathsFromVariableToHeapLocation(withnesses, lb);
                        foreach (VariableEffect ve in vEffs)
                        {
                            string msg = string.Format("{0} has to be consistent", ve.ToString());
                            ra.HandleError(lb.Statement, lb.Statement, Error.GenericWarning, msg);

                            problems[lb] = problems[lb] + msg + ";";
                        }
                        reentrantCall = true;
                        AddOffendingCall(lb, 4, withnesses);
                    }
                }

            }
            
            // updating call information
            UpdateCallEdges(thisVariableValues, potentialReceivers.Nodes, calleecState, ipm);


            // ** END Reentrancy
            return thisCallIsReentrant;
        }

        // If we force all to false we assume that calls as a non consistent receiver
        public override bool IsExposable(Set<IPTAnalysisNode> ns)
        {
            if (ReentrancyAnalysis.ForceInconsistency)
                return false;
            return base.IsExposable(ns);
        }
        public override bool IsExposable(Variable v)
        {
            if (ReentrancyAnalysis.ForceInconsistency)
                return false;
            return base.IsExposable(v);
        }
        public override bool IsExposable(IPTAnalysisNode n)
        {
            if (ReentrancyAnalysis.ForceInconsistency)
                return false;
            return base.IsExposable(n);
        }


        /// <summary>
        /// Check for Rule 2: See Manuel's internal Report 
        /// Indirect call to "this"
        /// (I will complete later...)
        /// </summary>
        /// <param name="bindedPotentialCalleeReceivers"></param>
        /// <param name="thisVariableValues"></param>
        /// <returns></returns>
        private static bool CheckRule2UsingEdges(Nodes bindedPotentialCalleeReceivers, Nodes thisVariableValues)
        {

            // bool rule2 = thisVariableValues.Intersection(bindedPotentialCalleeReceivers).Count != 0;
            bool rule2 = !thisVariableValues.Intersection(bindedPotentialCalleeReceivers).IsEmpty;
            return rule2;
        }

        // This uses call edges
        private Nodes GetBindedCalleePotentialReceiversUsingCallEdges(Reentrancy calleecState, InterProcMapping ipm)
        {

            Nodes res = Nodes.Empty;
            // Bind callee potential receivers nodes with the updated caller ptg nodes
            Nodes receivers = Nodes.Empty;
            foreach (Edge e in calleecState.callEdges)
            {
                receivers.Add(e.Src);
            }
            foreach (IPTAnalysisNode n in receivers)
            {
                foreach (IPTAnalysisNode bindNode in ipm.RelatedExtended(n))
                {
                    if (n.IsClousure)
                    {
                        Nodes reachable = this.pointsToGraph.NodesForwardReachableFrom(bindNode);
                        foreach (IPTAnalysisNode nr in reachable)
                            if (nr.IsObjectAbstraction)
                                res.Add(nr);
                    }
                    else
                    {
                        res.Add(bindNode);
                    }
                }
            }
            return res;
        }

        /// <summary>
        /// Rule 3: Reentrant call discovered using caller context 
        /// </summary>
        /// <param name="calleeReentrancy"></param>
        /// <param name="ipm"></param>
        /// <param name="withnesses"></param>
        /// <returns></returns>
        private bool CheckRule3UsingEdges(Reentrancy calleeReentrancy, InterProcMapping ipm,
            out Nodes withnesses)
        {
            bool res = false;
            withnesses = Nodes.Empty;
            foreach (Edge e in calleeReentrancy.callEdges)
            {
                CallEdge ce = (CallEdge)e;
                bool rule3=false;
                if(!ce.ReceiverExposable)
                    rule3 = CheckLoopUsingCallEdgesAndIPM(e.Src, ipm, calleeReentrancy.callEdges, out withnesses);
                res = res || rule3;
                if (res)
                    break;
            }
            return res;
        }

        /// <summary>
        /// Rule 4: Potential reentrant call because of a call in a Load Node (using type information)
        /// Must be refined!
        /// </summary>
        /// <param name="calleeReentrancy"></param>
        /// <param name="potentialReceivers"></param>
        /// <param name="thisValues"></param>
        /// <param name="ipm"></param>
        /// <param name="withnesses"></param>
        /// <returns></returns>
        private bool CheckRule4UsingEdges(Reentrancy calleeReentrancy,
            Nodes potentialReceivers, Nodes thisValues, 
            InterProcMapping ipm, out Nodes withnesses)
        {
            withnesses = Nodes.Empty;

            if(calleeReentrancy.method.Name.Name.Equals("Notify"))
            {}
            // foreach (Edge e in calleeReentrancy.callEdges)
            Set<TypeNode> thisTypes = new Set<TypeNode>();
            Set<TypeNode> receiverTypes = new Set<TypeNode>();
            foreach (IPTAnalysisNode n in thisValues)
            {
                if(n.IsLoad || n.IsParameterValueLNode)
                    thisTypes.Add(n.Type);
            }
            bool rule4 = false;
            foreach (IPTAnalysisNode n in potentialReceivers)
            {
                if (n.IsLoad || n.IsParameterValueLNode)
                {
                    receiverTypes.Add(n.Type);
                    if(CheckType(thisTypes,n.Type))
                    {
                        rule4=true;
                        withnesses.Add(n);
                        break;
                    }
                }
            }
            //bool rule4 = thisTypes.IntersectionNotEmpty(receiverTypes);
            if (!rule4)
            {
                IPTAnalysisNode withness;
                rule4 = MayHaveTheSameType(calleeReentrancy.CallEdges, thisTypes, out withness);
                if (rule4)
                    withnesses.Add(withness);
            }
           
            return rule4;
        }



        public bool CheckLoopUsingCallEdgesAndIPM(IPTAnalysisNode n, InterProcMapping ipm,
            Edges cEdges, out Nodes withnesses)
        {
            // Set<List<IPTAnalysisNode>> res = new Set<List<IPTAnalysisNode>>();
            // List<IPTAnalysisNode> currentPath = new List<IPTAnalysisNode>();
            Nodes visited = Nodes.Empty;
            Nodes visitedMuApplied = Nodes.Empty;
            // bool rule3 = DFSPathFrom(n, cEdges, visited, visitedMuApplied, res, currentPath, ipm);
            
            withnesses= DFSPathFrom(n, cEdges, visited, visitedMuApplied, ipm);
            bool rule3 = withnesses.Count > 0;
            return rule3;
        }

        public Nodes DFSPathFrom(IPTAnalysisNode nr, Edges cEdges,
            Nodes visited, Nodes visitedMuApplied,
            // Set<List<IPTAnalysisNode>> res,
            // List<IPTAnalysisNode> current, 
            InterProcMapping ipm)
        {
            // Set<List<Edge>> paths = new Set<List<Edge>>();
            List<Edge> current = new List<Edge>();
            Nodes w = DFSPathFrom(nr, cEdges, visited, visitedMuApplied, ref current,ipm);
            return w;
        }



        public Nodes DFSPathFrom(IPTAnalysisNode nr, Edges cEdges,
            Nodes visited, Nodes visitedMuApplied,
            // Set<List<IPTAnalysisNode>> res,
            ref List<Edge>  current, 
            InterProcMapping ipm)
        {
            List<Edge> newCurrent = new List<Edge>(current);

            //bool hasLoop = false;
            Nodes withnesses = Nodes.Empty;

            Nodes relatedNr = ipm.RelatedExtended(nr);
            visited.Add(nr);
            visitedMuApplied.AddRange(relatedNr);
            Nodes a = Nodes.Singleton(nr);
            foreach (IPTAnalysisNode n in a)
            {
                //Nodes adjacents = new Nodes();
                //adjacents.AddRange(cEdges.Adjacents(n, true));
                Set<Edge> adjacents = cEdges.EdgesFromSrc(n);

                if (adjacents.Count != 0)
                {
                    //foreach (PTAnalysisNode n2 in adjacents)
                    foreach (Edge e in adjacents)
                    {
                        CallEdge ce = (CallEdge)e;
                        newCurrent.Add(ce);

                        IPTAnalysisNode n2 = ce.Dst;
                        Nodes relatedN2 = ipm.RelatedExtended(n2);

                        //List<IPTAnalysisNode> newCurrent = new List<IPTAnalysisNode>();
                        // newCurrent.AddRange(current);
                        // newCurrent.Add(n2);


                        // BUG BUG: This may not be a loop. It can happen in a DAG.
                        // This is still sound but much more conservative
                        if (!visited.Contains(n2)
                            )
                        {
                            // if (visitedMuApplied.Intersection(relatedN2).Count == 0)
                            if (visitedMuApplied.Intersection(relatedN2).IsEmpty)
                            {
                                //hasLoop = DFSPathFrom(n2, cEdges, visited, visitedMuApplied, ipm);
                                
                                withnesses.AddRange(DFSPathFrom(n2, cEdges, visited, visitedMuApplied, ref newCurrent, ipm));
                            }
                            else
                            {
                                // Loop in resolved nodes
                                //return true;
                                if (!ce.ThisExposable)
                                {
                                    withnesses.AddRange(visitedMuApplied.Intersection(relatedN2));
                                    return withnesses;
                                }
                                else
                                    return Nodes.Empty;
                            }
                        }
                        else
                        {
                            // Direct Loop in calls
                            //return true;
                            //return Nodes.Singleton(n2);
                            if (!ce.ThisExposable)
                                return ipm.RelatedExtended(n2);
                            else
                                return Nodes.Empty;
                        }
                    }
                }
                else
                {
                    // res.Add(current);
                    // return hasLoop;
                    return withnesses;
                }
            }
            //return hasLoop;
            return withnesses;
        }

        // Not used
        public bool CheckLoopUsingTypes(IPTAnalysisNode n, Set<TypeNode> types, 
            Edges cEdges, InterProcMapping ipm)
        {
            Nodes visited = Nodes.Empty;
            bool rule4 = DFSPathFromUsingTypesOLD(n,cEdges,types, visited,ipm);
            

            return rule4;
        }
        public bool MayHaveTheSameType(Edges cEdges,
            Set<TypeNode> types, out IPTAnalysisNode withness)
        {
            bool hasSameType = false;
            withness = null;
            foreach (Edge e in cEdges)
            {
                if (CheckType(types, e.Src.Type))
                {
                    withness = e.Src;
                    return true;
                }
            }
            return hasSameType;
        }
        
        public bool DFSPathFromUsingTypesOLD(IPTAnalysisNode nr, Edges cEdges,
            Set<TypeNode> types, Nodes visited, InterProcMapping ipm)
        {
            bool hasLoop = false;
            Nodes muNr = ipm.RelatedExtended(nr);
            visited.AddRange(muNr);
            foreach (IPTAnalysisNode n in muNr)
            {
                    Nodes adjacents = cEdges.Adjacents(n, true);
                    Nodes muAdj = ipm.RelatedExtended(adjacents);
                    if (muAdj.Count != 0)
                    {
                        foreach (PTAnalysisNode n2 in muAdj)
                        {
                            if (CheckType(types,n2.Type))
                            {
                                return  true;
                            }
                            else
                            {
                                if (!visited.Contains(n2))
                                    hasLoop = DFSPathFromUsingTypesOLD(n2, cEdges, types, visited,ipm);
                            }
                        }
                    }
            }
            return hasLoop;
        }
        
        private bool CheckType(Set<TypeNode> types, TypeNode t)
        {
            bool res = false;
            foreach (TypeNode t2 in types)
            {
                if (t.IsAssignableTo(t2))
                    return true;
            }
            return res;
        }

        /// <summary>
        /// Update call edges with the information about the actual call, translate information 
        /// about call edges in the callee using the interproc mapping and clausure all to keep only load nodes. 
        /// </summary>
        /// <param name="thisVariableValues"></param>
        /// <param name="potentialReceivers"></param>
        /// <param name="calleeReentrancy"></param>
        /// <param name="ipm"></param>
        private void UpdateCallEdges(Nodes thisVariableValues, Nodes potentialReceivers,
                Reentrancy calleeReentrancy, InterProcMapping ipm)
        {
            Edges auxCallEdges = new Edges(callEdges);
            // Add an egde that relate "this" with the receiver
            bool isReceiverOK = IsExposable(potentialReceivers);
            if(!calleeReentrancy.Method.IsStatic)
            {
                isReceiverOK = isReceiverOK || !MethodRequiresExposable(calleeReentrancy.Method, calleeReentrancy.Method.ThisParameter);
            }

            AddEdges(potentialReceivers, thisVariableValues,auxCallEdges,isReceiverOK,IsExposable(thisVariableValues));
            // Add an edge for every egde in the callee but applying the mu mapping
            foreach (Edge e in calleeReentrancy.callEdges)
            {
                CallEdge ce = (CallEdge)e;
                AddEdges(ipm.RelatedExtended(e.Src), ipm.RelatedExtended(e.Dst),auxCallEdges,ce.ReceiverExposable,ce.ThisExposable);
            }
            callEdges = ClausureAndClean(auxCallEdges);
        }

        //private void AddCallEdge(Nodes ns1, Nodes ns2)
        //{

        //    AddEdges(ns1, ns2, callEdges);

        //}

        private void AddEdges(Nodes ns1, Nodes ns2,Edges es, bool isExp1, bool isExp2)
        {
            foreach (IPTAnalysisNode n1 in ns1)
            {
                // if (n1.IsLoad)
                    AddEdge(n1, ns2,es, isExp1, isExp2);
            }
        }

        private void AddEdges(Nodes ns1, Nodes ns2,Edges es)
        {
            AddEdges(ns1,ns2,es, IsExposable(ns1), IsExposable(ns2));
        }
        
        private void AddEdge(IPTAnalysisNode n1, Nodes ns, Edges es, bool isExp1, bool isExp2)
        {
            foreach (IPTAnalysisNode n2 in ns)
            {
                AddCallEdge(n1, n2, isExp1, isExp2, es);
            }
        }

        protected virtual void AddCallEdge(IPTAnalysisNode n1, IPTAnalysisNode n2, 
            bool receiverExposable, bool thisExposable,
            Edges es)
        {
            CallEdge ce = new CallEdge(n1, n2, receiverExposable,thisExposable);
            es.AddEdge(ce);
        }



        /// <summary>
        /// Compute the clousure of Call Edges
        /// </summary>
        /// <param name="egdesToClausure"></param>
        /// <returns></returns>
        private Edges ClausureAndClean(Edges egdesToClausure)
        {
            //transclosure( int adjmat[max][max], int path[max][max])
            //{
            // for(i = 0; i < max; i++)
            //  for(j = 0; j < max; j++)
            //      path[i][j] = adjmat[i][j];

            // for(i = 0;i <max; i++)
            //  for(j = 0;j < max; j++)
            //   if(path[i][j] == 1)
            //    for(k = 0; k < max; k++)
            //      if(path[j][k] == 1)
            //        path[i][k] = 1;
            //}

            Dictionary<int,IPTAnalysisNode> nodes = new Dictionary<int,IPTAnalysisNode>();
            int cNodes = 0;
            
            // Creates and "array" of nodes
            foreach(Edge e in egdesToClausure)
            {
                if(!nodes.ContainsValue(e.Src))
                {
                    nodes[cNodes]=e.Src;
                    cNodes++;
                }
                if(!nodes.ContainsValue(e.Dst))
                {
                    nodes[cNodes]=e.Dst;
                    cNodes++;
                }
            }

            // Computes the Path matrix
            bool[,]  pathMatrix = new bool[cNodes,cNodes];

            Pair<bool, bool>[,] consistencyMatrix = new Pair<bool, bool>[cNodes, cNodes]; 

            for(int i = 0; i < cNodes; i++)
            {
                for(int j = 0; j < cNodes; j++)
                {
                    if (egdesToClausure.Adjacents(nodes[i],true).Contains(nodes[j]))
                    {
                        pathMatrix[i, j] = true;
                        
                        Set<Edge> es = egdesToClausure.EdgesFromSrc(nodes[i]);
                        foreach(Edge e in es)
                        {
                            if(e.Dst.Equals(nodes[j]))
                            {
                                CallEdge ce = (CallEdge)e;
                                consistencyMatrix[i, j] = new Pair<bool, bool>(ce.ReceiverExposable, ce.ThisExposable);
                            }
                        }
                    }
                }
            }
            // Clousure 
            for (int i = 0; i < cNodes; i++)
            {
                for (int j = 0; j < cNodes; j++)
                {
                    if(pathMatrix[i,j])
                    {
                        for (int k = 0; k < cNodes; k++)
                        {
                            if (pathMatrix[j, k])
                            {
                                IPTAnalysisNode n = nodes[j];
                                if (NodeToRemove(n))
                                {
                                    pathMatrix[i, k] = true;
                                    consistencyMatrix[i, k] = new Pair<bool, bool>(
                                        consistencyMatrix[i,j].Fst,consistencyMatrix[j,k].Snd);
                                        
                                }
                            }
                        }
                    }
                }
            }
            

            Edges finalClousure = new Edges();
            for (int i = 0; i < cNodes; i++)
            {
                for (int j = 0; j < cNodes; j++)
                {
                    if (pathMatrix[i, j])
                    {
                        IPTAnalysisNode n = nodes[i];
                        if (!NodeToRemove(n))
                        {
                            AddCallEdge(nodes[i], nodes[j], consistencyMatrix[i, j].Fst, consistencyMatrix[i, j].Snd, finalClousure);

                            //AddCallEdge(nodes[i], nodes[j], finalClousure);
                            
                            
                            //CallEdge ce = new CallEdge(nodes[i], nodes[j]);
                            //finalClousure.AddEdge(ce);
                            
                            // finalClousure.AddEdge(nodes[i], PTGraph.allFields, nodes[j]);
                        }
                    }
                }
            }

            
            return finalClousure;
        }

        private bool NodeToRemove(IPTAnalysisNode n)
        {
            bool res;
            res = !n.IsLoad;
            res = res && !n.IsParameterNode;
            res = res && !n.IsParameterValueLNode;
            return res;
        }
        

        #endregion
        #endregion
        /*
        public override void ApplyStoreField(Variable v1, Field f, Variable v2, Label lb)
        {
            base.ApplyStoreField(v1, f, v2, lb);
            CheckAndInformError(Values(v1), lb);
        }
        public override void ApplyStoreElement(Variable v1, Variable v2, Label lb)
        {
            base.ApplyStoreElement(v1, v2, lb);
            CheckAndInformError(Values(v1), lb);
        }
        public override void ApplyStoreIndirect(Variable v1, Variable v2, Label lb)
        {
            base.ApplyStoreIndirect(v1, v2, lb);
            CheckAndInformError(ValuesIndirect(v1), lb);
        }

        public override void ApplyExpose(Variable varToExpose, Label lb)
        {
            Nodes ns = Values(varToExpose);
            Set<VariableEffect> pathsToNode = ComputePathsFromVariableToHeapLocation(ns, MethodLabel);
            foreach (VariableEffect ve in pathsToNode)
            {
                if (InconsistentAndNotExposed(ve.Variable))
                {
                    Variable v = ve.Variable;    
                    string msg = string.Format("{0} has to be Exposable", v.Name);
                    ((ReentrancyAnalysis)this.pta).HandleError(lb.Statement, lb.Statement, Error.GenericWarning, msg);
                    callsToWarn.Add(lb);
                    inconsistentExpr[lb] = Set<VariableEffect>.Singleton(new VariableEffect(v, lb));
                    problems[lb] = msg;
                    AddOffendingCall(lb, 6, Values(v));
                }
            }
            base.ApplyExpose(varToExpose, lb);
            
        }

        public override void ApplyUnExpose(Variable varToExpose, Label lb)
        {
            base.ApplyUnExpose(varToExpose, lb);
        }
        private void CheckAndInformError(Variable v, Label lb)
        {
            CheckAndInformError(Values(v), lb);
        }

        private void CheckAndInformError(Nodes ns, Label lb)
        {
            Set<VariableEffect> pathsToNode = ComputePathsFromVariableToHeapLocation(ns, MethodLabel);
            foreach (VariableEffect ve in pathsToNode)
            {
                if (InconsistentAndNotExposed(ve.Variable))
                {
                    //Console.Out.Write("Var: {0} Exposed: {1}  Exposable: {2}",
                    //    ve.Variable, IsExposed(ve.Variable), IsExposable(ve.Variable));
                    Variable v = ve.Variable;
                    InformNeedToExposeForVar(lb, v);
                }
            }
        }

        private void InformNeedToExposeForVar(Label lb, Variable v)
        {
            string msg = string.Format("{0} has to be Exposed", v.Name);
            ((ReentrancyAnalysis)this.pta).HandleError(lb.Statement, lb.Statement, Error.GenericWarning, msg);
            callsToWarn.Add(lb);
            inconsistentExpr[lb] = Set<VariableEffect>.Singleton(new VariableEffect(v, lb));
            problems[lb] = msg; 
            AddOffendingCall(lb, 6, Values(v));
        }
        
        private bool InconsistentAndNotExposed(Variable v)
        {
            bool res = !IsExposable(v) 
                // &&  (v is Parameter && MethodRequiresExposable(Method,(Parameter)v))
                && !IsExposed(v); 
            return res;
        }
        */

        private void InformNeedConsistencyForVar(Label lb, Variable var)
        {
            Set<VariableEffect> pathsToNode = ComputePathsFromVariableToHeapLocation(Values(var), MethodLabel);
            foreach (VariableEffect ve in pathsToNode)
            {
                string msg = string.Format("{0} may be inconsistent", ve);
                ((ReentrancyAnalysis)this.pta).HandleError(lb.Statement, lb.Statement, Error.GenericWarning, msg);
                callsToWarn.Add(lb);

                problems[lb] = msg;
            }
            inconsistentExpr[lb] = pathsToNode;
            AddOffendingCall(lb, 6, Values(var));

        }
        private bool HasToExpose()
        {
            bool res = /*!inExposedBlock &&*/ !CciHelper.IsConstructor(this.Method);
            return res;
        }
        #region IDataFlowState Implementation
        /// <summary>
        /// Display the cState
        /// </summary>
        public override void Dump()
        {
            base.Dump();
            Console.Out.WriteLine(ToString());
            //Console.Out.WriteLine(receivers);
            DumpProblems();
        }
        public void DumpProblems()
        {
            foreach (Label lb in this.callsToWarn)
            {
                Statement stat = lb.Statement;

                //Set<IPTAnalysisNode> withnesses = this.OffendingReceivers(stat);
                Console.Out.WriteLine("({0},{1}): Offending statement [{3}] in <{2}>",
                        stat.SourceContext.StartLine,
                        stat.SourceContext.StartColumn,
                        lb.Method.FullName,
                        CodePrinter.StatementToString(stat));
                //     Console.Out.WriteLine("Offending Nodes: {0}", withnesses);

                string msg = problems[lb];
                Console.WriteLine(msg);
                
                /*
                Set<VariableEffect> vEffs = this.inconsistentExpr[lb];
                foreach (VariableEffect ve in vEffs)
                {
                    ObjectExposureAnalysis oea = (ObjectExposureAnalysis)this.pta;
                    string msg = string.Format("{0} has to be Exposed", ve.ToString());
                    Console.WriteLine(msg);
                }
                */

            }
            Console.Out.WriteLine("End Calls to Warn");
        }
        public override void DumpDifference(PointsToState pt2)
        {
            Reentrancy r2 = (Reentrancy)pt2;
            string res="";
            base.DumpDifference(pt2);
            if (!this.callEdges.Equals(r2.callEdges))
            {
                Set<Edge> ceD = callEdges.Difference(r2.callEdges);
                res += string.Format("CallEgdes: {0} {1} \n", ceD.Count, ceD);
            }
            else
                res = "Call Edges OK";
            Console.Out.WriteLine(res);
            return;

        }
        #endregion

        #region Basic object overwritten methods (Equals, Hash, ToString)

        public override string ToString()
        {
            string res = base.ToString();

            //res = res + "Receivers: " ;
            //res = res + receivers;

            res = res + "Call Edges: ";
            res = res + callEdges + "\n";
            
            res = res + "Reentrant Calls: {";
            foreach (Label lb in ofendingCallees.Keys)
            {
                //Label lb = lbr.Fst;
                res = res + lb.ToString()+"(r:)+"+ ofendingCallees[lb] + " ";
            }
            res = res + "}\n"; ;

            /*
            res = res + "Assumptions:" + methodAssumptions.ToString() + "\n";
            res = res + "Calls: " + CallToNonAnalizableString() + "\n";
            res = res + "Default: " + IsDefault.ToString() + "\n";
            */

            return res;
        }

        public override bool Equals(object obj)
        {

            Reentrancy ptRe = obj as Reentrancy;
            bool eqPointsTo =  base.Equals(obj);
            // bool eqReceivers = ptRe != null && receivers.Equals(ptRe.receivers);
            bool eqCalls = ptRe != null && callEdges.Equals(ptRe.callEdges);
            
            bool eq = eqPointsTo /*&& eqReceivers*/ && eqCalls;

            return eq;
        }

        public override int GetHashCode()
        {
            //return pointsToGraph.GetHashCode() + receivers.GetHashCode();
            return base.GetHashCode() + /*receivers.GetHashCode()*/ + callEdges.GetHashCode();
        }
        #endregion
    }
    public class CallEdge : Edge
    {
        Label lb;
        bool srcExposable, dstExposable;
        public CallEdge(IPTAnalysisNode src, IPTAnalysisNode dst):base(src,dst)
        {
            srcExposable = dstExposable = false;
        }
        public CallEdge(IPTAnalysisNode src, IPTAnalysisNode dst, bool sExp, bool dExp)
            : base(src, dst)
        {
            this.lb = null;
            this.srcExposable = sExp;
            this.dstExposable = dExp;
        }

        public CallEdge(IPTAnalysisNode src, IPTAnalysisNode dst, bool sExp, bool dExp, Label lb)
            : base(src, dst)
        {
            this.lb = lb;
            this.srcExposable = sExp;
            this.dstExposable = dExp;
        }
        public bool ReceiverExposable
        {
            get { return srcExposable /*&& false*/;  }
        }
        public bool ThisExposable
        {
            get { return dstExposable /*&& false*/; }
        }
        public override string ToString()
        {
            return string.Format("({0}:{1},{2}:{3})",src,srcExposable,dst,dstExposable);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode()+srcExposable.GetHashCode()+dstExposable.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            CallEdge ce = obj as CallEdge;
            return base.Equals(obj) && (ce!=null && ce.dstExposable==dstExposable && ce.srcExposable==srcExposable);
        }
    }
    #endregion

     

    /// <summary>
    /// The dataflow analysis for the method under analysis 
    /// </summary>
    public class ReentrancyInferer : ObjectConsistencyInferer //PointsToInferer
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="t"></param>
        /// <param name="pta"></param>
        public ReentrancyInferer(TypeSystem t, ReentrancyAnalysis pta): base(t,pta)
        {
        }
        internal override PointsToInstructionVisitor GetInstructionVisitor(PointsToInferer pti)
        {
            return new PointsToAndReenrtancyInstructionVisitor((ReentrancyInferer)pti);
        }

        /// <summary>
        /// Compute the Dataflow analysis for the given method
        /// Returns true if the method is pure
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        /// 
        public bool ComputeReentrancyFor(Method method)
        {
            // return base.ComputePointsToStateFor(method);
            return base.ComputeExposureFor(method);
        }

        /// <summary>
        /// Return the results of the analysis on exit of the CFG
        /// </summary>
        public Reentrancy ReentranceState
        {
            get { return (Reentrancy)exitState; }
        }


        protected override IDataFlowState VisitBlock(CfgBlock block, IDataFlowState stateOnEntry)
        {
            if (IsInstrumentationCode(block))
                return stateOnEntry;

            return base.VisitBlock(block, stateOnEntry);
        }

        internal new static bool IsInstrumentationCode(CfgBlock block)
        {
            if (block.ExceptionHandler != null)
            {
                for (CfgBlock handler = block.ExceptionHandler; handler != null;
                    handler = handler.ExceptionHandler)
                {
                    Catch catchStmt = handler[0] as Catch;
                    if (catchStmt == null) { continue; }
                    TypeNode exnType = catchStmt.Type;
                    if (exnType == null) { continue; }
                    if (Equals(exnType.FullName, "Microsoft.Contracts.ContractMarkerException"))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        

    }

    /// <summary>
    /// Instruction visitor. Implement the transfer function of the dataflow analysis
    /// It is exactly the same as the points to analysis
    /// </summary>
    internal class PointsToAndReenrtancyInstructionVisitor : ObjectConsistencyInstructionVisitor //PointsToInstructionVisitor
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="pta"></param>
        /// 
        int count = 0;
        public PointsToAndReenrtancyInstructionVisitor(PointsToInferer pta): base(pta)
        {
        }
        protected override object VisitStoreField(Variable dest, Field field, Variable source, Statement stat, object arg)
        {
            //Reentrancy r = (Reentrancy)arg;
            //Console.Out.WriteLine("c: {4} m: {3} f:{0} u:{1} s:{2}",field.Name,field.UniqueKey,CodePrinter.StatementToString(stat),r.Method.Name,r.Method.DeclaringType.FullName);
            return base.VisitStoreField(dest, field, source, stat, arg);
        }
        protected override object VisitCall(Variable dest, Variable receiver, Method callee, ExpressionList arguments, bool virtcall, Statement stat, object arg)
        {
            count++;
            /*
            ReentrancyAnalysis ra = (ReentrancyAnalysis)this.PointsToAnalysis;
            Reentrancy r = (Reentrancy)arg;

            Console.Out.WriteLine("{0} in {1}", callee.FullName, r.Method.Name);
            
            Node offendingNode = new Statement(NodeType.Nop);
            offendingNode.SourceContext = stat.SourceContext;
            offendingNode.SourceContext.StartPos = count;
            
            offendingNode.SourceContext.Document = stat.SourceContext.Document;

            ra.HandleError(stat, offendingNode, Error.GenericWarning, callee.FullName + " in " + r.Method.Name);
            */
            object res = base.VisitCall(dest, receiver, callee, arguments, virtcall, stat, arg);

            return res;
        }
        protected override void CheckExposure(ConsistencyState exp, Variable v)
        {
            
        }
        protected override bool PureAsAnalyzable()
        {
            return true;
        }

    }

    /// <summary>
    /// The main analyis class. Entry point to analyze a method, an assembly or 
    /// a compilation unit.
    /// At contruction time, you can define if the analysis is interprocedural or only
    /// intraprocedural. If you choose interprocedural, you can decide for a fix point 
    /// based approach, using a backward traversal over a partial callgraph, or an inlining
    /// simulation (when you can decide the maximun callstack depth)
    /// The analysis has 2 modes of operation. StandAlone or assuminng that is using inside CCI or Boogie.
    /// The main diference is that in the standalone it tries to analyze all method it finds in the assembly
    /// and in the other case it only analyzed the method annotated as [pure] or [confined]. 
    /// The purpose on the StandAlone mode is the INFERENCE, and the other mode is VERIFICATION.
    /// </summary>
    public class ReentrancyElementFactory: ConsistentyElementFactory // ElementFactory
    {
        public ReentrancyElementFactory(PointsToAnalysis pta) : base(pta) { }

        public override PointsToState NewElement(Method m)
        {
            return new Reentrancy(m,pta);
        }
    }
    
    /// <summary>
    /// The interprocedural analysis main class. It is basically as the Points to analysis 
    /// </summary>
    public class ReentrancyAnalysis : ObjectConsistencyAnalysis // PointsToAnalysis
    {
        public static bool ForceInconsistency = false;
        #region Constructors
        public ReentrancyAnalysis(TypeSystem t): base(t)
        {
            //factory = new ReentrancyElementFactory(this);
        }
        
        /// <summary>
        /// Constructor with a given Node. 
        /// We used to compute the set of nodes in this node and (optionally) bound the set of 
        /// analyzable methods in the interprocedural analysis
        /// </summary>
        /// <param name="t"></param>
        /// <param name="node"></param>
        public ReentrancyAnalysis(TypeSystem t, Node node): base(t, node)
        {
         
        }
        public ReentrancyAnalysis(TypeSystem t, Node node, bool interProcedural)
            : base(t,node,interProcedural)
        {
         
        }
        public ReentrancyAnalysis(TypeSystem t, Node node, bool interProcedural, bool fixPoint) 
            : base(t,node,interProcedural,fixPoint)
        {
         
        }

        public ReentrancyAnalysis(TypeSystem t, Node node, bool interProcedural, bool fixpoint, int maxDepth)
            :base(t,node,interProcedural,fixpoint, maxDepth)
        {
         
        }

        protected override void Init(TypeSystem t, Node node, bool interProcedural, bool fixpoint, int maxDepth)
        {
            base.Init(t, node, interProcedural, fixpoint, maxDepth);
            factory = new ReentrancyElementFactory(this);
        }
        /// <summary>
        /// Same constructor but with the analyzer
        /// </summary>
        /// <param name="analyzer"></param>
        /// <param name="t"></param>
        /// <param name="node"></param>
        /// 
        
        public ReentrancyAnalysis(Analyzer analyzer, TypeSystem t, Node node)
            : base(analyzer,t, node)
        {
         
        }
        public ReentrancyAnalysis(Analyzer analyzer, TypeSystem t, Node node, bool interProcedural)
            : base(analyzer, t, node,interProcedural)
        {
         
        }
        public ReentrancyAnalysis(Analyzer analyzer, TypeSystem t, Node node, bool interProcedural, bool fixPoint)
            : base(analyzer, t, node,interProcedural, fixPoint)
        {
         
        }

        public ReentrancyAnalysis(Analyzer analyzer, TypeSystem t, Node node, bool interProcedural, bool fixpoint, int maxDepth)
            : base(analyzer, t, node, interProcedural, fixpoint,maxDepth)
        {
         
        }

        public ReentrancyAnalysis(Visitor callingVisitor)
            : base(callingVisitor) 
        {
         
        }
         
        #endregion
        public override PointsToInferer CreateIntraProcAnalysis()
        {
            return new ReentrancyInferer(this.typeSystem,this);
        }
        #region Visitors (Class, TypeNode, Assembly or Method)
        public override Class VisitClass(Class c)
        {
            if (verbose)
            {
                System.Console.WriteLine("Starting Analysis for:" + c.FullName);
            }
            return base.VisitClass(c);
        }

        public override Method VisitMethod(Method method)
        {
            System.Console.WriteLine("Starting Analysis for:" + method.FullName);
            return base.VisitMethod(method);
        }
        public override bool NeedToVerify(Method method) 
        {
                return true;
        }
        #endregion

        #region Method for registering and querying the analysis results
        public Reentrancy GetReentrancyAnalysis(Method m)
        {
            PointsToState pt = GetSummaryForMethod(m);
            return (Reentrancy)pt;
        }

        public Reentrancy GetReentranceAnalysisWithDefault(Method m)
        {
            PointsToState pt = GetSummaryForMethodWithDefault(m);
            return (Reentrancy)pt;
        }
        #endregion
        
        #region Methods for Registering and querying if a method was already analyzed
        /// <summary>
        /// Determines if the method was analyzed 
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public override bool WasAnalyzed(Method m)
        {
            return base.WasAnalyzed(m);
        }
        #endregion

        #region Methods to determine is a Method is Analyzable
        /// <summary>
        /// Determines whether the method if analyzable or not
        /// for interProc call.
        /// That is, if we can get the method body 
        /// (not abstract, not interface, under our desired analysis scope)
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public override bool IsAnalyzable(Method m)
        {
            return base.IsAnalyzable(m);
        }

        #endregion

        #region Displaying information about the analysis results
        /// <summary>
        /// Show detailed information about the results for the method.
        /// 
        /// </summary>
        /// <param name="m"></param>
        public void ProcessResultsMethod(Method m)
        {
            Reentrancy cState = GetReentranceAnalysisWithDefault(m);
            PTGraph ptg = cState.PointsToGraph;

            if (verbose && !cState.IsDefault)
            {
                Console.Out.WriteLine("***RESULT***");
                Console.Out.WriteLine("Method:{0} Abstract:{2}", m.GetFullUnmangledNameWithTypeParameters(),
                     m.IsAbstract);

                foreach (Parameter p in ptg.ParameterMap.Keys)
                {
                    String refOrOut = "(value)";
                    if (p.IsOut)
                        refOrOut = "(out)";
                    else
                        if (p.Type is Reference)
                            refOrOut = "(ref)";

                    Console.Out.WriteLine("Parameter: {0}  ({1})", p.Name, refOrOut);
                }

                //if (!GetPurityMap(m))
                {
                    Console.Out.WriteLine(cState);
                }
                cState.PointsToGraph.GenerateDotGraph(Console.Out);
                Console.Out.WriteLine("****");
            }

            CheckAttributes(m, cState, ptg);
        }

        private void CheckAttributes(Method m, Reentrancy cState, PTGraph ptg)
        {
            #region Information about Escape, Freshness and use of Globals
            // If it was analyzed
            if (!cState.IsDefault)
            {
                if (PointsToAndEffectsAnnotations.IsDeclaredFresh(m))
                {
                    bool isFreshReturn = cState.CheckFreshness(m);
                    if (!isFreshReturn)
                    {
                        string msg = string.Format("Method {0} is declared fresh but return cannot be proven to be fresh", cState.Method.Name);
                        typeSystem.HandleError(m, Error.GenericWarning, msg);
                    }

                }
             
                foreach (Parameter p in ptg.ParameterMap.Keys)
                {
                    //if (!IsConstructor(cState.Method))
                    {
                        bool captured;
                        if (!PointsToAndEffectsAnnotations.IsDeclaredEscaping(p,out captured))
                        {
                            bool escapes = cState.CheckEscapes(p);
                            if (escapes)
                            {
                                string msg = string.Format("{0} escapes method {1}", p.Name, cState.Method.Name);
                                if(p!=cState.Method.ThisParameter)
                                  typeSystem.HandleError(p, Error.GenericWarning, msg);
                                else
                                  typeSystem.HandleError(cState.Method, Error.GenericWarning, msg);
                            }

                        }
                    }
                    if (PointsToAndEffectsAnnotations.IsDeclaredFresh(p))
                    {
                        bool isFreshParameter = cState.CheckFreshness(p);
                        if (!isFreshParameter)
                        {
                            string msg = string.Format("{0} declared fresh but cannot prove that it is fresh in method {1}", p.Name, cState.Method.Name);
                            typeSystem.HandleError(p, Error.GenericWarning, msg);
                        }

                    }
                }
            }
            #endregion
}
        #endregion
#region Error Handling
/// <summary>
/// Important: This is absolutely necessary, since we are doing fix-point
/// Analysis. Bypass this sometimes means hundred's of the same error messages.
/// </summary>
private Hashtable reportedErrors = new Hashtable();

/// <summary>
/// Error handler. Only file an error if it has not been filed yet. 
/// 
/// Requires: the node has proper source context. Otherwise, it does not help.
/// </summary>
/// <param name="stat"></param>
/// <param name="node"></param>
/// <param name="error"></param>
/// <param name="m"></param>
internal void HandleError(Statement stat, Node node, Error error, params string[] m)
{

    Node offendingNode = node;
    if (offendingNode.SourceContext.Document == null)
    {
        offendingNode = stat;
    }
    else if (node is StackVariable && stat.SourceContext.Document != null && stat.SourceContext.Document != node.SourceContext.Document)
    {
        // might have reused local variable even though it does not correspond to source location of target
        offendingNode = stat;
    }
    if (reportedErrors.Contains(offendingNode.SourceContext))
        return;
    //Analyzer.WriteLine("!!! " + error+ " : "+node);
    if (m == null)
        typeSystem.HandleError(offendingNode, error);
    else
        typeSystem.HandleError(offendingNode, error, m);
    reportedErrors.Add(offendingNode.SourceContext, null);
}
internal void HandleError(Statement stat, Node node, Error error, TypeNode t)
{
    Node offendingNode = node;
    if (offendingNode.SourceContext.Document == null)
    {
        offendingNode = stat;
    }
    // Debug.Assert(t != null);
    if (reportedErrors.Contains(offendingNode.SourceContext))
        return;
    //Analyzer.WriteLine("!!! " + error+ " : "+node);
    typeSystem.HandleError(offendingNode, error, typeSystem.GetTypeName(t));
    reportedErrors.Add(offendingNode.SourceContext, null);
}
#endregion 
    }
   
    
}




