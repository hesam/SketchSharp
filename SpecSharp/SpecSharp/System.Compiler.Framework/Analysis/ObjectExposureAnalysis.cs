//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;
using System.Compiler.WPurity;

namespace System.Compiler.Analysis
{
    public class ExposureElementFactory: ReentrancyElementFactory // ElementFactory
    {
        public ExposureElementFactory(PointsToAnalysis pta) : base(pta) { }

        public override PointsToState NewElement(Method m)
        {
            return new Exposure(m,pta);
        }
    }
    public class ObjectExposureAnalysis : ReentrancyAnalysis
    {
        #region Constructors
        protected override void Init(TypeSystem t, Node node, bool interProcedural, bool fixpoint, int maxDepth)
        {
            ReentrancyAnalysis.ForceInconsistency = false;
            base.Init(t, node, interProcedural, fixpoint, maxDepth);
            factory = new ExposureElementFactory(this);
        }
        public ObjectExposureAnalysis(TypeSystem t): base(t)
        {
        }
        
        /// <summary>
        /// Constructor with a given Node. 
        /// We used to compute the set of nodes in this node and (optionally) bound the set of 
        /// analyzable methods in the interprocedural analysis
        /// </summary>
        /// <param name="t"></param>
        /// <param name="node"></param>
        public ObjectExposureAnalysis (TypeSystem t, Node node): base(t, node)
        {
         
        }
        public ObjectExposureAnalysis (TypeSystem t, Node node, bool interProcedural)
            : base(t,node,interProcedural)
        {
         
        }
        public ObjectExposureAnalysis (TypeSystem t, Node node, bool interProcedural, bool fixPoint) 
            : base(t,node,interProcedural,fixPoint)
        {
         
        }

        public ObjectExposureAnalysis (TypeSystem t, Node node, bool interProcedural, bool fixpoint, int maxDepth)
            :base(t,node,interProcedural,fixpoint, maxDepth)
        {
         
        }

        
        /// <summary>
        /// Same constructor but with the analyzer
        /// </summary>
        /// <param name="analyzer"></param>
        /// <param name="t"></param>
        /// <param name="node"></param>
        /// 
        
        public ObjectExposureAnalysis(Analyzer analyzer, TypeSystem t, Node node)
            : base(analyzer,t, node)
        {
         
        }
        public ObjectExposureAnalysis(Analyzer analyzer, TypeSystem t, Node node, bool interProcedural)
            : base(analyzer, t, node,interProcedural)
        {
         
        }
        public ObjectExposureAnalysis(Analyzer analyzer, TypeSystem t, Node node, bool interProcedural, bool fixPoint)
            : base(analyzer, t, node,interProcedural, fixPoint)
        {
         
        }

        public ObjectExposureAnalysis(Analyzer analyzer, TypeSystem t, Node node, bool interProcedural, bool fixpoint, int maxDepth)
            : base(analyzer, t, node, interProcedural, fixpoint,maxDepth)
        {
         
        }

        public ObjectExposureAnalysis(Visitor callingVisitor)
            : base(callingVisitor) 
        {
         
        }
         
        #endregion
        public override PointsToInferer CreateIntraProcAnalysis()
        {
            return new ExposureInferer(this.typeSystem,this);
        }
    }
    public class ExposureInferer : ReentrancyInferer
    {
        public ExposureInferer(TypeSystem t, ReentrancyAnalysis pta)
            : base(t, pta)
        {
        }
        internal override PointsToInstructionVisitor GetInstructionVisitor(PointsToInferer pti)
        {
            return new ExposureVisitor((ExposureInferer )pti);
        }
    }
    class Exposure : Reentrancy
    {
        //protected Dictionary<Label, Set<VariableEffect>> inconsistentExpr;
        //protected Dictionary<Label, String> problems;
        //protected Set<Label> callsToWarn;


        #region Constructors
        // Constructor
        public override PointsToState Copy()
        {
            return new Exposure(this);
        }
        public Exposure(Method m,PointsToAnalysis pta): base(m,pta)
        {
            
            //inconsistentExpr = new Dictionary<Label, Set<VariableEffect>>();
            //problems = new Dictionary<Label, string>();
            //callsToWarn = new Set<Label>();
        }

        // Copy Constructor
        public Exposure(Exposure cState): base(cState)
        {
            //problems = new Dictionary<Label, string>(cState.problems);
            //inconsistentExpr = new Dictionary<Label, Set<VariableEffect>>(cState.inconsistentExpr);
            //this.callsToWarn = new Set<Label>(cState.callsToWarn);
        }


        public new  Exposure Bottom
        {
            get
            {
                return new Exposure(this.Method,this.pta);
            }
        }

        // Get the least elements representing the bottom for the lattice of m
        //public static Reentrancy BottomFor(Method m)
        //{
        //    return new Reentrancy(m);
        //}
        #endregion

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
            if (!IsMethodToIgnoreForReentrancy(reentrancyCalleeState.Method))
            {
                //isReentrant = ReentrancyAnalysisForAnalyzableCalls(reentrancyCalleeState.Method, reentrancyCalleeState, lb,
                //    potentialReceivers, ipm);
                isReentrant = false;
                CheckExposureForCall(calleePTS, receiver, arguments, lb, isReentrant);
            }
        }

        /*
        private void CheckExposureForCall(PointsToState calleePTS, Variable receiver, ExpressionList arguments, Label lb, bool isReentrant)
        {
            if (!isReentrant)
            {

                // Receiver is not consistent and callee requires it consistent
                if (receiver != null
                    && MethodRequiresExposable(calleePTS.Method, calleePTS.Method.ThisParameter)
                    && !IsExposable(receiver) )  //&& !IsExposed(receiver) )
                {
                    InformNeedConsistencyForVar(lb, receiver);
                }
                for (int i = 0; i < arguments.Count; i++)
                {
                    if (arguments[i] is Variable)
                    {
                        Variable argVar = (Variable)arguments[i];
                        if (MethodRequiresExposable(calleePTS.Method, calleePTS.Method.Parameters[i])
                        && !IsExposable(argVar))  //&& !IsExposed(argVar)
                        {
                            InformNeedConsistencyForVar(lb, argVar);
                        }

                    }
                }
            }
        }
        */

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
                    inconsistentExpr[lb] = new Set<VariableEffect>(new VariableEffect(v, lb));
                    problems[lb] = msg;
                    // AddOffendingCall(lb, 6, Values(v));
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
            if(v.Name.Name.Contains("SS$Closure"))
                return;
            string msg = string.Format("{0} has to be Exposed", v.Name);
            ((ReentrancyAnalysis)this.pta).HandleError(lb.Statement, lb.Statement, Error.GenericWarning, msg);
            callsToWarn.Add(lb);
            inconsistentExpr[lb] = new Set<VariableEffect>(new VariableEffect(v, lb));
            problems[lb] = msg;
            // AddOffendingCall(lb, 6, Values(v));
        }
                private bool InconsistentAndNotExposed(Variable v)
        {
            bool res = !IsExposable(v)
                // &&  (v is Parameter && MethodRequiresExposable(Method,(Parameter)v))
                && !IsExposed(v);
            return res;
        }

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
            // AddOffendingCall(lb, 6, Values(var));

        }
        private bool HasToExpose()
        {
            bool res = /*!inExposedBlock &&*/ !CciHelper.IsConstructor(this.Method);
            return res;
        }
    }
    class ExposureVisitor: PointsToAndReenrtancyInstructionVisitor
    {
        public ExposureVisitor(PointsToInferer pta)
            : base(pta)
        {
        }
        protected override object VisitStoreField(Variable dest, Field field, Variable source, Statement stat, object arg)
        {
            return base.VisitStoreField(dest, field, source, stat, arg);
        }
        protected override object VisitCall(Variable dest, Variable receiver, Method callee, ExpressionList arguments, bool virtcall, Statement stat, object arg)
        {
            
            object res = base.VisitCall(dest, receiver, callee, arguments, virtcall, stat, arg);
            return res;
        }
        protected override bool PureAsAnalyzable()
        {
            return true;
        }
    }
}
