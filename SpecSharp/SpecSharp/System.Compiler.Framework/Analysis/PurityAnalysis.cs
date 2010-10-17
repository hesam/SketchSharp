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
using System.Compiler.Diagnostics;
using System.Compiler.Analysis;
using Microsoft.Contracts;
using System.Compiler.Analysis.PointsTo;
#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler.WPurity
{
#endif

    #region PointsTo and Read/Write Effects Semillatice

    /// <summary>
    /// Represents the pointsTo and the write effects of the current block
    /// That is PtWe = &lt; PtGraph , WriteEffecs , NonAnalyzableCalls &gt; 
    /// It is a semilattice (bottom , &lt;=)
    /// </summary>
    public class PointsToAndWriteEffects : PointsToState  //AbstractValue, IDataFlowState
    {
        
        #region Atttibutes
        #region Semilatice
        /// <summary>
        /// This is the Semilattice
        /// A PointsToGraph, the WriteEffects and the non-analyzed calls
        /// </summary>
        internal AbstractEffects writeEffects;
        internal AbstractEffects readEffects;
        #endregion
        // Model the set of potencial receivers in the method calls of the MUA
        // For debugging purposes. Keep Track of ALL effects
        internal AbstractEffects allWriteEffects;
        internal AbstractEffects allReadEffects;

        internal Dictionary<Label, Set<Method>> callToNonAnalyzableMethods;
        #endregion

        // INonNullState nonNullState;

        #region Properites
        public AbstractEffects WriteEffects
        {
            get { return writeEffects; }
        }
        public AbstractEffects ReadEffects
        {
            get { return readEffects; }
        }
        
        // The projection of the nodes that have a write effect
        public Set<IPTAnalysisNode> ModifiedNodes
        {
            get
            {
                return writeEffects.ModifiedNodes;
            }
        }

        public Dictionary<Label, Set<Method>> CallsToNonAnalyzable
        {
            get { return callToNonAnalyzableMethods; }
        }
        // Returns true if the method has non analyzable methods
        public bool HasNonAnalyzableMethods
        {
            get
            {
                return callToNonAnalyzableMethods.Count != 0;
            }
        }

        #endregion

        #region Constructors
        // Constructor
        public PointsToAndWriteEffects(Method m, PointsToAnalysis pta) : base(m,pta)
        {
            this.writeEffects = new AbstractEffects();
            this.allWriteEffects = new AbstractEffects();
            this.readEffects = new AbstractEffects();
            this.allReadEffects = new AbstractEffects();
            this.callToNonAnalyzableMethods = new Dictionary<Label, Set<Method>>();
            // this.nonNullState = null;
        }
        

        // Copy Constructor
        public PointsToAndWriteEffects(PointsToAndWriteEffects ptWE): base(ptWE)
        {
            writeEffects = new AbstractEffects(ptWE.writeEffects);
            allWriteEffects = ptWE.allWriteEffects;
            readEffects = new AbstractEffects(ptWE.readEffects); ;
            allReadEffects = ptWE.allReadEffects;
            callToNonAnalyzableMethods = new Dictionary<Label, Set<Method>>(ptWE.callToNonAnalyzableMethods);
            
            //if(ptWE.nonNullState!=null)
            //    nonNullState = new NonNullState((NonNullState)ptWE.nonNullState);
        }
        public PointsToAndWriteEffects(PointsToState ptWE)
            : base(ptWE)
        {
            this.writeEffects = new AbstractEffects();
            this.allWriteEffects = new AbstractEffects();
            this.readEffects = new AbstractEffects();
            this.allReadEffects = new AbstractEffects();
            this.callToNonAnalyzableMethods = new Dictionary<Label, Set<Method>>();
            

            //if(ptWE.nonNullState!=null)
            //    nonNullState = new NonNullState((NonNullState)ptWE.nonNullState);
        }

        public PointsToAndWriteEffects Bottom
        {
            get
            {
                return new PointsToAndWriteEffects(this.Method,this.pta);
            }
        }

        // Get the least elements representing the bottom for the lattice of m
        //public static PointsToAndWriteEffects BottomFor(Method m)
        //{
        //    return new PointsToAndWriteEffects(m);
        //}
        
        public override PointsToState Copy()
        {
            return new PointsToAndWriteEffects(this);
        }

        //public INonNullState NonNullState
        //{
        //    get { return nonNullState; }
        //    set { nonNullState = value; }
        //}
        #endregion

        #region SemiLattice Operations (Join, Includes, IsBottom)

        public override bool IsBottom
        {
            get { return pointsToGraph.IsBottom && writeEffects.IsBottom && callToNonAnalyzableMethods.Count == 0 && isDefault; }
        }

        public override bool IsTop
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        /// <summary>
        ///  Join two PointsToAndWriteEffects
        /// </summary>
        /// <param name="ptgWe"></param>
        public override void Join(PointsToState pts)
        {
            PointsToAndWriteEffects ptgWe = (PointsToAndWriteEffects)pts;
            if (ptgWe != null)
            {
                base.Join(ptgWe);
                if (writeEffects != ptgWe.writeEffects)
                    writeEffects.Join(ptgWe.writeEffects);
                
                if (allWriteEffects != ptgWe.allWriteEffects)
                    allWriteEffects.Join(ptgWe.allWriteEffects);

                if (readEffects != ptgWe.readEffects)
                    readEffects.Join(ptgWe.readEffects);

                if (allReadEffects != ptgWe.allReadEffects)
                    allReadEffects.Join(ptgWe.allReadEffects);

                if (callToNonAnalyzableMethods != ptgWe.callToNonAnalyzableMethods)
                    joinCalledMethod(callToNonAnalyzableMethods, ptgWe.callToNonAnalyzableMethods);
            }
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
        ///  Inclusion check for two PointsToAndWriteEffects
        /// </summary>
        /// <param name="ptgWe"></param>
        public override bool Includes(PointsToState pts)
        {
            PointsToAndWriteEffects ptwe2 = (PointsToAndWriteEffects)pts;
            bool includes = base.Includes(ptwe2);
            includes = includes && this.WriteEffects.Includes(ptwe2.WriteEffects);
            includes = includes && this.ReadEffects.Includes(ptwe2.ReadEffects);

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
        public override void InitMethod(Method method, System.Collections.IEnumerable parameters, Label lb)
        {
            base.InitMethod(method,parameters,lb);
        }

        /// <summary>
        /// f(v1 = null, ptwe), operation only over the pointsToGraph 
        /// </summary>
        /// <param name="v1"></param>
        public override  void ApplyAssignNull(Variable v1)
        {
            base.ApplyAssignNull(v1);
        }

        /// <summary>
        /// Represent when the value of a variable is not longer valid
        /// This means losing information about which nodes v1 points to
        /// </summary>
        /// <param name="v1"></param>
        public override void ForgetVariable(Variable v1)
        {
            base.ForgetVariable(v1);            
        }
        /// <summary>
        /// A more complex copy operation
        /// f(v1 = v2), operation only over the pointsToGraph
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="lb"></param>
        public override void CopyLocVar(Variable v1, Variable v2)
        {
            base.CopyLocVar(v1,v2);
        }

        /// <summary>
        /// f(v1 = v2), operation only over the pointsToGraph
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="lb"></param>
        public override void CopyLocVar(Variable v1, Variable v2, Label lb)
        {
            base.CopyLocVar(v1, v2, lb);
        }
        #endregion

        #region Object allocation
        /// <summary>
        /// f(new Type, ptwe) , operation only over the pointsToGraph
        /// </summary>
        /// <param name="v"></param>
        /// <param name="lb"></param>
        /// <param name="type"></param>
        public override void ApplyNewStatement(Variable v, Label lb, TypeNode type)
        {
            base.ApplyNewStatement(v, lb, type);
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
        public override void ApplyStoreField(Variable v1, Field f, Variable v2, Label lb)
        {
            bool wasNull = false;
            RegisterWriteEffect(v1, f, lb,wasNull);
            base.ApplyStoreField(v1, f, v2, lb);
        }
        

        /// <summary>
        /// f(v1[.] = v2), operates over the pointsToGraph and register the writeEffect
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="lb"></param>
        public override void ApplyStoreElement(Variable v1, Variable v2, Label lb)
        {
            RegisterWriteEffect(v1, PTGraph.arrayField, lb,false);
            base.ApplyStoreElement(v1, v2, lb);
        }

        /// <summary>
        /// f(C.f = v2), operates over the pointsToGraph and register the writeEffect
        /// </summary>
        /// <param name="v2"></param>
        /// <param name="f"></param>
        /// <param name="lb"></param>
        public override void ApplyStaticStore(Variable v2, Field f, Label lb)
        {
            RegisterWriteEffect(PTGraph.GlobalScope, f, lb,false);
            base.ApplyStaticStore(v2, f, lb);
        }
        #endregion

        #region Load Statements support
        /// <summary>
        /// f(v1 = v2.f), operates over the pointsToGraph and register the read effect
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="f"></param>
        /// <param name="lb"></param>
        public override void ApplyLoadField(Variable v1, Variable v2, Field f, Label lb)
        {
            RegisterReadFields(v2, f, lb);
            base.ApplyLoadField(v1, v2, f, lb);
        }

        /// <summary>
        /// f(v1 = v2[.]), operates over the pointsToGraph and register the read effect 
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="lb"></param>
        public override void ApplyLoadElement(Variable v1, Variable v2, Label lb)
        {
            RegisterReadFields(v2, PTGraph.arrayField, lb);
            base.ApplyLoadElement(v1, v2, lb);
        }

        /// <summary>
        /// f(v1 = C.f), operates over the pointsToGraph 
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="f"></param>
        /// <param name="lb"></param>
        public override void ApplyLoadStatic(Variable v1, Field f, Label lb)
        {
            RegisterReadFields(PTGraph.GlobalScope, f, lb);
            base.ApplyLoadStatic(v1,f,lb);
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
        public override void ApplyLoadAddress(Variable dest, Variable src, Label lb)
        {
            base.ApplyLoadAddress(dest, src, lb);
        }

        /// <summary>
        /// f(v1 = *v2), operation only over the pointsToGraph
        /// // I take it as dest = * pointer
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="lb"></param>
        public override void ApplyLoadIndirect(Variable v1, Variable v2, Label lb)
        {
            RegisterReadFields(v2, PTGraph.asterisk, lb);
            base.ApplyLoadIndirect(v1, v2, lb);
        }
        
        /// <summary>
        /// A more complex copy operation
        /// f(*v1 = v2, operation only over the pointsToGraph
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="lb"></param>
        public override void ApplyStoreIndirect(Variable v1, Variable v2, Label lb)
        {
            RegisterWriteEffect(v1, PTGraph.asterisk, lb,false);
            base.ApplyStoreIndirect(v1, v2, lb);
        }
        /// <summary>
        ///  f(v1 = &amp; v2.f)
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="src"></param>
        /// <param name="lb"></param>
        public override  void ApplyLoadFieldAddress(Variable dest, Variable src, Field f, Label lb)
        {
            RegisterReadFields(src, f , lb);
            base.ApplyLoadFieldAddress(dest, src, f, lb);
        }
        /// <summary>
        ///  f(v1 = &amp; C.f).  
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="src"></param>
        /// <param name="lb"></param>
        public override void ApplyLoadStaticFieldAddress(Variable dest, Field f, Label lb)
        {
            RegisterReadFields(PTGraph.GlobalScope, f, lb);
            base.ApplyLoadStaticFieldAddress(dest, f, lb);

        }
        #endregion

        #region Method Calls Support

      
        
        /// <summary>
        /// Create an PTG and effect information for a non analyzable method
        /// </summary>
        /// <param name="vr"></param>
        /// <param name="callee"></param>
        /// <param name="receiver"></param>
        /// <param name="arguments"></param>
        /// <param name="lb"></param>
        /// 
        public override PointsToState CreateSummaryForFakeCallee(Variable vr, Method callee, Variable receiver, ExpressionList arguments, Label lb)
        {
            Variable newReceiver = receiver;

            if(receiver!=null && PTGraph.IsDelegateType(receiver.Type))
            {
                 AttributeList delegateAttributes = GetDelegateAttributes(receiver);
                /*  
                bool isPureDelegate = CheckPureDelegate(receiver);
                
                if (isPureDelegate)
                {
                    PointsToState calleeStateP = new PointsToState(callee, this.pta);
                    PTGraph ptg = PTGraph.CreatePTGraphForStronglyPureMethod(callee);
                    calleeStateP.pointsToGraph = ptg;
                    PointsToAndWriteEffects calleeS = new PointsToAndWriteEffects(calleeStateP);
                    return calleeS;
                }
                */
                DelegateNode dn = receiver.Type as DelegateNode;

                //Method fakeMethod = new Method(Method.DeclaringType, delegateAttributes,
                //                        new Identifier(receiver.Name.Name), dn.Parameters, dn.ReturnType, null);
                // callee = fakeMethod;
                callee.Attributes = delegateAttributes;

            }
            
            PointsToState calleeStatePT = base.CreateSummaryForFakeCallee(vr, callee, receiver, arguments, lb);
            PointsToAndWriteEffects calleeState = new PointsToAndWriteEffects(calleeStatePT);
            
            foreach (Parameter p in calleeState.Parameters)
            {
                bool isWrite = PointsToAndEffectsAnnotations.IsWriteParameter(callee, p);
                bool isWriteConfined = PointsToAndEffectsAnnotations.IsWriteConfinedParameter(callee, p);

                // Faltan los READ EFFECTS!!!
                if (isWrite || isWriteConfined)
                {
                    foreach(IPTAnalysisNode n in calleeState.pointsToGraph.NonAnalyzableLeaves(p))
                    {
                        calleeState.writeEffects.AddEffect(n, PTGraph.allFields, lb);
                        calleeState.allWriteEffects.AddEffect(n, PTGraph.allFields, lb);
                        if (PTGraph.isRef(p.Type))
                        {
                            foreach (IPTAnalysisNode valP in calleeState.pointsToGraph.Values(p))
                            {
                                calleeState.writeEffects.AddEffect(valP, PTGraph.asterisk, lb);
                                calleeState.allWriteEffects.AddEffect(valP, PTGraph.asterisk, lb);
                            }

                        }
                    }
                }
                if (p.IsOut)
                {
                    foreach (IPTAnalysisNode valP in calleeState.pointsToGraph.Values(p))
                    {
                        calleeState.writeEffects.AddEffect(valP, PTGraph.asterisk, lb);
                        calleeState.allWriteEffects.AddEffect(valP, PTGraph.asterisk, lb);
                    }

                }
                
            }
            if (PointsToAndEffectsAnnotations.IsDeclaredWritingGlobals(callee))
            {
                addNonAnalyzableMethod(lb, callee);
                calleeState.writeEffects.AddEffect(GNode.nGBL, PTGraph.allFields, lb);
                calleeState.allWriteEffects.AddEffect(GNode.nGBL, PTGraph.allFields, lb);
                calleeState.readEffects.AddEffect(GNode.nGBL, PTGraph.allFields, lb);
                calleeState.allReadEffects.AddEffect(GNode.nGBL, PTGraph.allFields, lb);
            }
            /*
            if (PointsToAndEffectsAnnotations.IsDeclaredReadingGlobals(callee))
            {
                calleeState.readEffects.AddEffect(GNode.nGBL, PTGraph.asterisk, lb);
                calleeState.allReadEffects.AddEffect(GNode.nGBL, PTGraph.asterisk, lb);
            }
            */
            

            return calleeState;
        }

        
        /// <summary>
        /// Register effect of the non-analyzable call in the pointsToGraph inferring the information from the callee annotations
        /// </summary>
        /// <param name="vr"></param>
        /// <param name="callee"></param>
        /// <param name="receiver"></param>
        /// <param name="arguments"></param>
        /// <param name="lb"></param>
        /// 
        public override void ApplyNonAnalyzableCall(Variable vr, Method callee, Variable receiver, 
            ExpressionList arguments, Label lb, out InterProcMapping ipm)
        {
            if (this.method.FullName.Contains("<Except"))
            {
                if (callee.Name.Name.Contains("Reverse"))
                { }
            }
            if (this.method.FullName.Contains("Except"))
            {
            }
            if (this.method.Name.Name.Contains("MoveNext")) 
            {
            }
            if (callee.Name.Name.Contains("MoveNext"))
            {
            }
            if (callee.Name.Name.Contains("GetEnumerator2")) {
            }
            if (receiver != null)
            {
                bool isDelegate = PTGraph.IsDelegateType(receiver.Type);
                if (isDelegate)
                {
                    bool isPureDelegate = PointsToAndEffectsAnnotations.IsAssumedPureDelegate(callee.ThisParameter)
                        || callee.FullName.Contains("Linq.Enumerable");
                    if (isPureDelegate)
                    {
                        ForceCheckForDelegate(lb, receiver);
                    }
                }
            }
            // PointsToAndEffectsAnnotations.WorstCase = true;
            if (callee.Parameters != null)
            {
                for (int i = 0; i < callee.Parameters.Count; i++)
                {
                    Parameter p = callee.Parameters[i];
                    bool isDelegate = PTGraph.IsDelegateType(p.Type);
                    if (isDelegate)
                    {
                        bool isPureDelegate = PointsToAndEffectsAnnotations.IsAssumedPureDelegate(p)
                            || callee.FullName.Contains("Linq.Enumerable");
                        if (isPureDelegate)
                        {
                            Variable v = (Variable)arguments[i];
                            ForceCheckForDelegate(lb, v);
                        }
                    }
                }
            }
            
            base.ApplyNonAnalyzableCall(vr, callee, receiver, arguments, lb, out ipm);

            
            // PointsToAndEffectsAnnotations.WorstCase = false;

        }

        private void ForceCheckForDelegate(Label lb, Variable v)
        {
            Nodes pValues = Values(v);
            foreach (PTAnalysisNode val in pValues)
            {
                if (val.IsMethodDelegate)
                {
                    MethodDelegateNode mn = val as MethodDelegateNode;
                    Method m = mn.Method;
                    // OJO: I may have to create a new method that send this method to analyze according 
                    // to the type of interprocedural analysis
                    WeakPurityAndWriteEffectsAnalysis wPta = (WeakPurityAndWriteEffectsAnalysis)this.pta;
                    // Copy source context information to the method to show the caller context of method is not pure
                    m.SourceContext = lb.Statement.SourceContext;
                    wPta.needToBePure.Add(m);
                    wPta.AnalysisForMethod(m);

                    //  wPta.VisitMethod(m); // computes points-to and effects
                    // processes results from VisitMethod: issues errors and potentially prints out detailed info.
                    if (!pta.StandAloneApp)
                        wPta.ProcessResultsMethod(m);
                }

            }
        }

        /*
        protected override void NonAnalyzableCallBeforeUpdatingPTG(Variable vr, Method callee, 
            Variable receiver, ExpressionList arguments, Label lb, 
            bool isPure, bool isReturnFresh, bool isWritingGlobals, bool isReadingGlobals, 
            Set<Parameter> escapingParameters, Set<Parameter> capturedParameters, Set<Parameter> freshParameters)
        {
            if (isWritingGlobals)
            {
                addNonAnalyzableMethod(lb, callee);
                RegisterWriteEffect(PTGraph.GlobalScope, PTGraph.allFields, lb,false);
                RegisterReadFields(PTGraph.GlobalScope, PTGraph.allFields, lb);
            }
            if (isReadingGlobals)
            {
                RegisterReadFields(PTGraph.GlobalScope, PTGraph.allFields, lb);
            }
            if (!isPure)
            {
                if (receiver != null)
                {
                    // RegisterWriteEffect(receiver, PTGraph.allFields, lb);
                    RegisterModifiesInReachableObjects(receiver, lb, callee.IsWriteConfined);
                    RegisterReadInReachableObjects(receiver, lb, callee.IsWriteConfined);
                    // RegisterReadFields(receiver, PTGraph.allFields, lb);
                }
                if (arguments != null)
                {
                    foreach (Expression arg in arguments)
                    {
                        if (arg is Variable)
                        {
                            Variable v = arg as Variable;
                            //RegisterWriteEffect(v, PTGraph.allFields, lb);
                            //RegisterReadFields(v, PTGraph.allFields, lb);
                            RegisterModifiesInReachableObjects(v, lb, callee.IsWriteConfined);
                            RegisterReadInReachableObjects(v, lb, callee.IsWriteConfined);
                        }
                    }
                }
            }
            base.NonAnalyzableCallBeforeUpdatingPTG(vr, callee, receiver, arguments, lb, 
                isPure, isReturnFresh, isWritingGlobals , isReadingGlobals, escapingParameters, capturedParameters, freshParameters);
        }
        */
        private void addNonAnalyzableMethod(Label lb, Method m)
        {
            Set<Method> mths = new Set<Method>();
            if (callToNonAnalyzableMethods.ContainsKey(lb))
                mths = callToNonAnalyzableMethods[lb];
            mths.Add(m);
            callToNonAnalyzableMethods[lb] = mths;
        }
        /* This was related with the old way of dealing with non analyzable calls
        private void RegisterModifiesInReachableObjects(Variable v, Label lb, bool confined)
        {
            // Use Filters for !owned!!!!!!
            Nodes reachables = PointsToGraph.NodesForwardReachableFromVariable(v, confined);
            foreach (IPTAnalysisNode n in reachables)
            {
                //if (n is LValueNode || n.IsGlobal)
                if(n.IsObjectAbstraction)
                {
                    RegisterWriteEffect(n, PTGraph.allFields, lb,false);    
                }
            }
        }
        private void RegisterReadInReachableObjects(Variable v, Label lb, bool confined)
        {
            // Use Filters for !owned!!!!!!
            Nodes reachables = PointsToGraph.NodesForwardReachableFromVariable(v,confined);
            foreach (IPTAnalysisNode n in reachables)
            {
                // Add Filters!!!!!!
                //if (n is LValueNode || n.IsGlobal)
                if (n.IsObjectAbstraction)
                {
                    RegisterReadEffect(n, PTGraph.allFields, lb);
                }
            }
        }
        */

        /// <summary>
        /// Apply the inter-procedural analysis, binding information from caller and callee
        /// </summary>
        /// <param name="vr"></param>
        /// <param name="callee"></param>
        /// <param name="receiver"></param>
        /// <param name="arguments"></param>
        /// <param name="calleePTWE"></param>
        /// <param name="lb"></param>
        /// 
        public override void ApplyAnalyzableCall(Variable vr, Method callee, Variable receiver, 
            ExpressionList arguments, PointsToState calleecState, Label lb, out InterProcMapping ipm)
        {
            
            if (this.method.FullName.Contains("Count"))
            {
                if (callee.Name.Name.Contains("Reverse"))
                { }
                if (callee.Name.Name.Contains("Dispose"))
                { }
            }
            if (callee.Name.Name.Contains("MoveNext2")) 
              { }

            base.ApplyAnalyzableCall(vr, callee, receiver, arguments, calleecState, lb, out ipm);
            PointsToAndWriteEffects calleePTWE = (PointsToAndWriteEffects)calleecState;

            // Join the set of non-analyzable calls
            joinCalledMethod(callToNonAnalyzableMethods, calleePTWE.callToNonAnalyzableMethods);
  
            
            // For each write effect in the calee
            foreach (AField af in calleePTWE.writeEffects)
            {
                
                // Get the related caller's nodes
                foreach (IPTAnalysisNode n in ipm.RelatedExtended(af.Src))
                {

                    if (IsVisibleEffect(n, af.Field))
                    {
                        writeEffects.AddEffect(n, af.Field, lb);
                    }
                    allWriteEffects.AddEffect(n, af.Field, lb);
                }
            }
            BindCallerReadEffects(calleePTWE, lb, ipm);
        }


        private void BindCallerReadEffects(PointsToAndWriteEffects calleePTWE, Label lb, InterProcMapping ipm)
        {
            foreach (AField af in calleePTWE.readEffects)
            {
                // Get the related caller's nodes
                foreach (IPTAnalysisNode n in ipm.RelatedExtended(af.Src))
                {
                    if (IsVisibleEffect(n, af.Field))
                    {
                        readEffects.AddEffect(n, af.Field, lb);
                    }
                    allReadEffects.AddEffect(n, af.Field, lb);
                }
            }
        }

        /// <summary>
        /// f(return v, ptwe), only has effect in the pointsToGraph
        /// </summary>
        /// <param name="v"></param>
        public override void ApplyReturn(Variable v, Label lb)
        {
            base.ApplyReturn(v, lb);
        }

        #endregion
        #endregion

        #region Write and Read Effects related methods
        /// <summary>
        /// Register the write effect in all nodes pointed by v
        /// </summary>
        /// <param name="v"></param>
        /// <param name="f"></param>
        /// <param name="lb"></param>
        private void RegisterWriteEffect(Variable v, Field f, Label lb,bool wasNull)
        {
            // PTAnalysisNode vAddr = PointsToGraph.GetAddress(v);
            // Nodes values = PointsToGraph.Values(vAddr);
            // Diego 12/8/2007 Nodes values = PointsToGraph.Values(PointsToGraph.GetLocations(v));
            Nodes values = this.Values(v);
            PointsToGraph.CheckValues(values, lb, v);
            foreach (IPTAnalysisNode n in values)
            {
                RegisterWriteEffect(n,f,lb,wasNull);
            }
        }

        private void RegisterWriteEffect(IPTAnalysisNode n, Field f, Label lb, bool wasNull)
        {
            if (IsVisibleEffect(n, f))
            {
                writeEffects.AddEffect(n, f, lb,wasNull);
            }
            allWriteEffects.AddEffect(n, f, lb,wasNull);
        }
        /// <summary>
        /// Register the write effect in all nodes pointed by v
        /// </summary>
        /// <param name="v"></param>
        /// <param name="f"></param>
        /// <param name="lb"></param>
        private void RegisterReadFields(Variable v, Field f, Label lb)
        {
            //if(pointsToGraph.Method.Name.Name.Contains("MoveNext"))
            //{
            //    System.Diagnostics.Debugger.Break();
            //}
            // PTAnalysisNode vAddr = PointsToGraph.GetAddress(v);
            //Nodes values = PointsToGraph.Values(vAddr);
            // Diego 12/8/2007 Nodes values = PointsToGraph.Values(PointsToGraph.GetLocations(v));
            Nodes values = this.Values(v);
            PointsToGraph.CheckValues(values, lb, v);
            foreach (IPTAnalysisNode n in values)
            {
                RegisterReadEffect(n, f, lb);
            }
        }
        private void RegisterReadEffect(IPTAnalysisNode n, Field f, Label lb)
        {
            if (IsVisibleEffect(n, f))
            {
                readEffects.AddEffect(n, f, lb);
            }
            allReadEffects.AddEffect(n, f, lb);
        }
        private bool IsVisibleNode(IPTAnalysisNode n)
        {
            if (n.IsInside)
                return false;
            bool isVisible = true;


            // Nodes B = ReachablesFromOutside();
            isVisible = isVisible && IsReachableFromOutside(n);
            return isVisible;
        }

        private bool IsReachableFromOutside(IPTAnalysisNode n)
        {
            Nodes Esc = new Nodes(PointsToGraph.E);
            Esc.Add(GNode.nGBL);
            foreach (PNode pn in PointsToGraph.AddrPNodes)
            {
                Esc.Add(pn);
            }
            return PointsToGraph.IsReachableFrom(n,Esc,true,true,true);

        }

        bool IsVisibleEffect(IPTAnalysisNode n, Field f)
        {
            bool isVisibleEffect = true;
            if (IsVisibleNode(n))
            {
                if (n.IsParameterNode)
                {
                    PNode pn = (PNode)n;
                    if (pn.IsByValue && (f.Equals(PTGraph.asterisk) || pn.IsStruct))
                        isVisibleEffect = false;
                }
            }
            else
                isVisibleEffect = false;
            return isVisibleEffect;

        }

        public Set<VariableEffect> ComputeWriteEffects()
        {
            return ComputeEffects(WriteEffects);
        }

        /// <summary>
        /// Given a Set of effects over abstract heap locations,
        /// computes a set of effects over the method parameters
        /// or in the global scope
        /// </summary>
        /// <param name="Effects"></param>
        /// <returns></returns>
        public Set<VariableEffect> ComputeEffects(AbstractEffects Effects)
        {
            Set<VariableEffect> variableEffects = new Set<VariableEffect>();

            // Traverse every write effect
            foreach (AField af in Effects)
              variableEffects.AddRange(EffectsInVariables(af));
            return variableEffects;
        }
        /// <summary>
        /// Computes the effect ogf "af" over the parameters and globals
        /// DIEGO-TODO: I need to improve this part since it is very slow and repeitive
        /// </summary>
        /// <param name="af"></param>
        /// <returns></returns>
        public Set<VariableEffect> EffectsInVariables(AField af)
        {
            Set<VariableEffect> variableEffects = new Set<VariableEffect>();
            // Builds a sort of tree with all nodes backward reacheacle from the node 
            IPTAnalysisNode affectectNode = af.Src;
            Edges tree = this.pointsToGraph.EdgesReachableFrom(affectectNode, null, false, true, true);
            
            foreach (Parameter p in this.Parameters)
            {
                CheckAndAddEffect(af, variableEffects, tree, p);
            }
            CheckAndAddEffect(af, variableEffects, tree, PTGraph.GlobalScope);
            return variableEffects;

        }
        /// <summary>
        /// Add all the effects of "af" over the given variable
        /// </summary>
        /// <param name="af"></param>
        /// <param name="variableEffects"></param>
        /// <param name="tree"></param>
        /// <param name="p"></param>
        private void CheckAndAddEffect(AField af, Set<VariableEffect> variableEffects, Edges tree, Variable p)
        {
            IPTAnalysisNode from = this.Address(p);
            // Compute all paths from the variable to the affected node 
            // DIEGO-TODO: this is terribly slow. I have to make it much more faster.
            IEnumerable<IEnumerable<Edge>> paths = PointsToGraph.DFSPathFromTo(from, af.Src, tree);
            foreach (List<Edge> currentPath in paths)
            {
                IPTAnalysisNode rootNode = null;
                if (currentPath.Count > 0)
                    rootNode = currentPath[0].Src;
                else
                    rootNode = af.Src;
                if (rootNode.IsVariableReference)
                {
                    IVarRefNode vrNode = rootNode as IVarRefNode;
                    Variable v = vrNode.ReferencedVariable;
                    if (v != null)
                    {
                        VariableEffect vEffect = ComputeOneEffectPath(af, currentPath, v);
                        variableEffects.Add(vEffect);
                    }
                }
            }
        }


        #region To Delete - Old way to conver from PTG effect to effects over variables
        // It has a bug since it doesn't compute some paths until the root (i.e. the variable) 
        public Set<VariableEffect> EffectsInVariables2(AField af) {
        Set<VariableEffect> variableEffects = new Set<VariableEffect>();
        // Get the fields that are backward reachable from the modified field
        Set<List<Edge>> paths = PointsToGraph.DFSPathFrom(af.Src, false, true, true);
        //IEnumerable<IEnumerable<Edge>> paths2 = PointsToGraph.DFSPath(af.Src, false, true, true);
        
        foreach (List<Edge> currentPath in paths) {
          currentPath.Reverse();

          IPTAnalysisNode rootNode;

          //foreach (Edge e in currentPath)
          //    Console.Out.Write("{0} ", e);
          //Console.Out.WriteLine();

          if (currentPath.Count > 0)
            rootNode = currentPath[0].Src;
          else
            rootNode = af.Src;

          // we ignore newly allocated objetcs
          if (rootNode.IsInside)
            continue;
          Variable v = null;
          if (rootNode.IsVariableReference) {
            IVarRefNode vrNode = rootNode as IVarRefNode;
            v = vrNode.ReferencedVariable;
            if (!(v is Parameter) && !v.Equals(PTGraph.GlobalScope))
              continue;
          }
          if (rootNode.Equals(GNode.nGBL)) {
            v = PTGraph.GlobalScope;
          }

          if (rootNode.IsParameterNode && ((PNode)rootNode).IsByValue) {
            bool fieldUpdate = !af.Field.Equals(PTGraph.asterisk);
            foreach (Edge e in currentPath) {
              if (fieldUpdate) break;
              fieldUpdate = fieldUpdate || !e.Field.Equals(PTGraph.asterisk);
            }
            if (!fieldUpdate)
              continue;
          }

          string nodeName = rootNode.Name;

          if (af.Field.IsStatic)
            nodeName = af.Field.DeclaringType.Name.Name;

          if (v != null) {
            VariableEffect vEffect = ComputeOneEffectPath(af, currentPath, v);
            variableEffects.Add(vEffect);
          }

        }
        return variableEffects;
      }

        /// <summary>
        /// Compute one Effects using one path from a variable an effect in 
        /// an abstract heap location
        /// </summary>
        /// <param name="af"></param>
        /// <param name="currentPath"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        private static VariableEffect ComputeOneEffectPath(AField af, List<Edge> currentPath, Variable v)
        {
            if (WeakPurityAndWriteEffectsAnalysis.debug)
            {
                if (v == null)
                    System.Diagnostics.Debugger.Break();
            }
            VariableEffect vEffect = new VariableEffect(v, af.Label);
            foreach (Edge e in currentPath)
            {
                if (!e.Field.Equals(PTGraph.asterisk))
                {
                    vEffect.AddField(e.Field);
                    // lastField = e.Field;
                    // DIEGO-CHECK: If I have an effect on a omega node i can reach all nodes
                    if (e.Src.IsOmega && (e.Field.Equals(PTGraph.allFields) || e.Field.Equals(PTGraph.allFieldsNotOwned)))
                      break;
                }
            }
            if (!af.Field.Equals(PTGraph.asterisk) || vEffect.FieldChain.Count == 0)
                vEffect.AddField(af.Field,af.WasNull);
            return vEffect;
        }
        #endregion
        
        /// <summary>
        /// Verify if the parameter is read-only. That means it cannot reach a modified node
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public bool IsReadOnly(Parameter p)
        {
            bool res = false;
            PointsToAndWriteEffects ptwe = this;
            PNode pn = ptwe.PointsToGraph.ParameterMap[p];
            Set<IPTAnalysisNode> modifiedNodes = ptwe.ModifiedNodes;

            
            res = ptwe.PointsToGraph.IsReachableFrom(modifiedNodes, Nodes.Singleton(pn), true, false, true);
            return res;
        }
        #endregion


        #region Globals Writing/Reading Check
        public bool CheckGlobalWriting(Method m)
        {
            Set<VariableEffect> writeEffects = ComputeEffects(WriteEffects);
            return CheckGlobalEffect(writeEffects);
        }
        public bool CheckGlobalEffect(Set<VariableEffect> effects)
        {
            bool res = false;
            foreach (VariableEffect effect in effects)
            {
                if (effect.Variable.Equals(PTGraph.GlobalScope))
                    return true;
            }
            return res;
        }
        public bool CheckGlobalReading(Method m)
        {
            Set<VariableEffect> readEffects = ComputeEffects(ReadEffects);
            foreach (VariableEffect effect in readEffects) {
              if (effect.Variable.Equals(PTGraph.GlobalScope)) {
                if (effect.FieldChain.Count == 1) {
                  Field f = effect.FieldChain[0];
                  if (f is PTExtraField) continue; //REVIEW: what is an extra field?
                  if (f.IsOnce || f.IsInitOnly) continue;
                }
                return true;
              }
            }
            return false;
          }
        #endregion 
       
        #region Purity Computation
        /// <summary>
        /// Generate the set with all the non-purity warnings using writEffects and call information.
        /// This set is used to send information to the compiler about the non-purity warnings.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Pair<Statement, string>> ComputePurityWarnings()
        {
            // List<Pair<Statement, string>> res = new List<Pair<Statement, string>>();
            ICollection<Pair<Statement, string>> res = new Set<Pair<Statement, string>>();

            IDictionary<Statement, string> purityWarnings = new Dictionary<Statement, string>();

            // It also presents information about non analyzable calls
            

            Set<VariableEffect> writeEffectsforVariables = ComputeWriteEffects();
            if (this.Method.IsPropertyGetter)
            {
                writeEffectsforVariables = ValidatePurityForProperty(writeEffectsforVariables, this.Method);
            }
            foreach (VariableEffect vEffect in writeEffectsforVariables)
            {
                Pair<Statement,string> purityWarning =  PreparePurityWarning(vEffect);
                if(purityWarning!=null)
                    res.Add(purityWarning);
                    // AddPurityWarning(purityWarnings, purityWarning);

            }

            if (HasNonAnalyzableMethods)
            {
                foreach (Label lb in CallsToNonAnalyzable.Keys)
                {
                    Statement stat = lb.Statement;
                    //if (stat != null)
                    //    Console.Out.WriteLine("In ({0},{1}) Statement:{2}", stat.SourceContext.StartLine,
                    //        stat.SourceContext.StartColumn, stat.SourceContext.SourceText);
                    foreach (Method m in callToNonAnalyzableMethods[lb])
                    {
                        // Console.Out.WriteLine("call to {0}", m.GetFullUnmangledNameWithTypeParameters());
                        string msg = string.Format("Call to potentially impure method {0} in {1}", m.FullName, Method.Name);
                        Pair<Statement, string> purityWarning = new Pair<Statement, string>(stat, msg);
                        // res.Add(new Pair<Statement, string>(stat, msg));
                        res.Add(purityWarning);
                        // AddPurityWarning(purityWarnings, purityWarning);
                    }
                }
            }
            /*
            foreach (Statement s in purityWarnings.Keys)
                res.Add(new Pair<Statement, string>(s, purityWarnings[s]));
            */ 
            return res;
        }

        private static void AddPurityWarning(IDictionary<Statement, string> purityWarnings, Pair<Statement,string> pw)
        {
             if (!purityWarnings.ContainsKey(pw.Fst))
                purityWarnings[pw.Fst] = pw.Snd;
        }

        private Set<VariableEffect> ValidatePurityForProperty(Set<VariableEffect> writeEffectsforVariables, Method m)
        {
            Set<VariableEffect> filteredEffects = new Set<VariableEffect>();
            Set<Field> fields = new Set<Field>();
            Field f = null;
            foreach (VariableEffect vEffect in writeEffectsforVariables)
            {
              if (vEffect.Variable.Equals(m.ThisParameter) || m.IsStatic) {
                f = vEffect.FieldChain[vEffect.FieldChain.Count - 1];
                if (f.IsPrivate && f.IsOnce /*&& vEffect.FieldWasNull*/)
                  fields.Add(f);
                else {
                  filteredEffects.Add(vEffect);
                }
              }
             }
             return filteredEffects;
        }

        private Pair<Statement, string> PreparePurityWarning(VariableEffect vEffect)
        {
            Pair<Statement, string> res = null;

            // Add the path (decorated with text) to the set of warnings to inform to the developer
            string path = vEffect.ToString();
            Label lb = vEffect.Label;
            Variable v = vEffect.Variable;
            Statement stat = lb.Statement;
            if (stat != null)
            {
                if (vEffect.Variable is Parameter)
                {
                    Parameter p = (Parameter)vEffect.Variable;
                    if (p.IsOut)
                    {
                        return res;
                    }
                }

                string msg;
                if (method.Equals(Method))
                    msg = string.Format("Cannot prove this statement does not modify {0} in declared pure method {1}", path, Method.Name);
                else
                    msg = string.Format("Cannot prove this statement does not (indirectly) modify {0} in declared pure method {1}", path, Method.Name);

                // Constructor are allowed to modify the "this" parameter
                if (!isConstructor(Method) || !v.Equals(Method.ThisParameter))
                {
                    res = new Pair<Statement, string>(stat, msg);
                }

                string statementString = stat.SourceContext.SourceText;
                if (stat.SourceContext.SourceText == null)
                {
                    statementString = CodePrinter.StatementToString(stat);
                }

                //Console.Out.WriteLine("({0},{1}) {2} {3}. Statement:{4}",
                //    stat.SourceContext.StartLine,
                //    stat.SourceContext.StartColumn,
                //    method.Equals(Method) ? "modifies" : "is modified in this call",
                //    path,
                //    statementString);
            }
            return res;
        }

        /// <summary>
        /// Check whether a method is pure or not by analyzing the pointsTo and writeEffects info.
        /// To be pure, the parameters (or locations reachable from them) cannot be modified 
        /// or accessed via escaping locations, and it cannot call non-analyzable methods.
        /// </summary>
        /// <param name="ptwe"></param>
        /// <returns></returns>
        public bool VerifyPurity()
        {
            PointsToAndWriteEffects ptwe = this;

            if (WeakPurityAndWriteEffectsAnalysis.IsUnsafe(ptwe.Method))
                return false;
            PTGraph ptg = ptwe.PointsToGraph;
            bool res = false;
            Nodes pNodes = Nodes.Empty;
            if (!ptwe.HasNonAnalyzableMethods)
            {
                /* Diego 12/8/2007 I replace it 
                // Compute the Parameter nodes to analyze
                foreach (PNode pn in ptg.AddrPNodes)
                {
                    // For constructor the "this parameter is ignored
                    // TO CHECK. Maybe we can directly say pNodes = ptg.PNodes
                    // if (!isConstructor(ptg.Method) || pn != ptg.ThisParameterNode)
                    {
                        pNodes.Add(pn);
                    }
                }
                For this: */
                pNodes = ptg.PVNodes;

                // A = Nodes forward reachable from the parameters (including this) 
                Nodes A = ptg.NodesForwardReachableFromOnlyOEdges(pNodes);
                // Diego 12/8/2007 A = ptg.Values(A);
                
                // B = Nodes forward reachable from from Escaping and Global nodes
                Nodes Esc = new Nodes(ptg.E);
                Esc.Add(GNode.nGBL);
                Nodes B = ptg.NodesForwardReachableFromNodes(Esc);

                // Diego 12/8/2007 B = ptg.Values(B);

                // TO CHECK. Maybe remove
                //if (isConstructor(ptg.Method))
                //{
                //    B.Remove(ptg.ThisParameterNode);
                //}

                // A ^ B == Empty means that (forall n \in A => n \notin B) 
                // if (A.Intersection(B).Count == 0)
                if (A.Intersection(B).IsEmpty)
                {
                    Set<VariableEffect> writeEffectsforVariables = ptwe.ComputeWriteEffects();
                    Nodes modifiedNodes = Nodes.Empty;
                    foreach (VariableEffect ve in writeEffectsforVariables)
                    {
                        // Diego 12/8/2007 modifiedNodes.AddRange(ptg.GetLocations(ve.Variable));
                        modifiedNodes.AddRange(this.Values(ve.Variable));
                    }

                    // Get the set of modified nodes = map (\(n,f) => f) ptwe.Writeffects
                    //Set<PTAnalysisNode> modifiedNodes = ptwe.ModifiedNodes;

                    // We consider the constructor as Pure if they only affect the variable "this" (JML)
                    if (isConstructor(ptg.Method))
                    {
                        // modifiedNodes.Remove(ptg.ThisParameterNode);
                        // Diego 8/12/2007 Added value of the paramater addr
                        modifiedNodes.Remove(ptg.Values(ptg.ThisParameterNode));
                    }
                    // We ignore parameter nodes is they are passed by value
                    // or they are out parameters
                    foreach (PNode pn in ptg.AddrPNodes)
                    {
                        //if(p.n.IsOut || p.Type.IsValueType)
                        if (pn.IsOut || (pn.IsByValue && PTGraph.isValueType(pn.Type)))
                            // modifiedNodes.Remove(pn);
                            // Diego 8/12/2007 Added value of the paramater addr
                            modifiedNodes.Remove(ptg.Values(pn));
                    }

                    // Check if the method has modified a global node or one reachable from a parameter
                    if ((!modifiedNodes.Contains(GNode.nGBL) 
                        /* DELETE && !modifiedNodes.Contains(ptg.GetAddress(PTGraph.GlobalScope)))*/
                        //&& modifiedNodes.Intersection(A).Count == 0))
                        && modifiedNodes.Intersection(A).IsEmpty))
                        res = true;
                }
                else
                {
                    // This means that the method make a global or escaping node reachable from a parameter
                    // i.e. a node reachable from parameters escapes globaly
                }
            }

            return res;
        }

        private static bool isConstructor(Method m)
        {
            bool isCtor = m is InstanceInitializer;
            return isCtor;
        }
        #endregion

        #region IDataFlowState Implementation
        /// <summary>
        /// Display the ptwe
        /// </summary>
        public override void Dump()
        {
            Console.Out.WriteLine(ToString());
            base.Dump();
            //PointsToGraph.Dump();
            //WriteEffects.Dump();
        }
        #endregion

        #region Basic object overwritten methods (Equals, Hash, ToString)

        public override string ToString()
        {
            string res = base.ToString();
            res = res + "\n";
            res = res + "Write " + writeEffects.ToString();
            res = res + "All write " + allWriteEffects.ToString();
            res = res + "Read " + readEffects.ToString();
            res = res + "All read " + allReadEffects.ToString();
            res = res + "Calls: " + CallToNonAnalizableString() + "\n";

            //res = res + "Assumptions:" + methodAssumptions.ToString() + "\n";
            
            //res = res + "Default: " + IsDefault.ToString() + "\n";
            return res;
        }

        private string CallToNonAnalizableString()
        {
            string res = "";
            foreach (Label lb in CallsToNonAnalyzable.Keys)
            {
                Statement stat = lb.Statement;
                if (stat != null)
                    res += string.Format("In ({0},{1}) Statement:{2}\n", stat.SourceContext.StartLine,
                        stat.SourceContext.StartColumn, stat.SourceContext.SourceText);
                foreach (Method m in callToNonAnalyzableMethods[lb])
                {
                    res += string.Format("call to {0}\n", m.GetFullUnmangledNameWithTypeParameters());
                }
            }
            return res;
        }

        public override bool Equals(object obj)
        {
            PointsToAndWriteEffects ptgWeff = obj as PointsToAndWriteEffects;
            bool eqPointsTo = base.Equals(obj);
            
            bool eqWriteEfetcs = ptgWeff != null && writeEffects.Equals(ptgWeff.writeEffects);
            
            //bool eqCalls = ptgWeff != null && callToNonAnalyzableMethods.Equals(ptgWeff.callToNonAnalyzableMethods);
            //bool eqExceptions = ptgWeff != null &&
            //    ((currentException == null && ptgWeff.currentException == null)
            //        || (currentException != null && currentException.Equals(ptgWeff.currentException)));

            //bool eqVirtual = ptgWeff != null && methodAssumptions.Equals(ptgWeff.methodAssumptions);

            bool eqReadEffects = ptgWeff != null & readEffects.Equals(ptgWeff.readEffects);


            bool eq = eqPointsTo && eqWriteEfetcs;

            return eq;
        }

        public override int GetHashCode()
        {
            // return pointsToGraph.GetHashCode() + writeEffects.GetHashCode();
            return base.GetHashCode() + writeEffects.GetHashCode();
        }
        #endregion
    }
    #endregion

    #region Read/Write Effect Representation
    /// <summary>
    /// Effects: Represent the semilattice of read/write effects
    /// It it a Set of &lt; PTAnalysisNode, Field &gt; Elements
    /// with Join and Include functions
    /// </summary>
    public class AbstractEffects : AbstractValue, IDataFlowState, System.Collections.IEnumerable
    {
        Set<AField> modifies;

        #region Semilattice Properties
        public Set<AField> Modifies
        {
            get { return modifies; }
        }

        public bool HasWriteEffects
        {
            get { return modifies.Count != 0; }
        }

        public Set<IPTAnalysisNode> ModifiedNodes
        {
            get
            {
                Set<IPTAnalysisNode> modifiedNodes = Modifies.ConvertAll((new Converter<AField, IPTAnalysisNode>(getSrc)));
                return modifiedNodes;
            }
        }

        private static IPTAnalysisNode getSrc(AField a)
        {
            return a.Src;
        }
        #endregion

        #region Constructors
        public AbstractEffects()
        {
            modifies = new Set<AField>();
        }
        public AbstractEffects(AbstractEffects w)
        {
            modifies = new Set<AField>(w.modifies);
        }
        #endregion

        #region Semilattice operations (Add,Join, Includes, IsBottom)
        public void AddWriteEffect(AField af)
        {
            modifies.Add(af);

        }
        public void AddEffect(IPTAnalysisNode n, Field f, Label lb)
        {
            AField af = new AField(n, f, lb);
            AddWriteEffect(af);
        }
        public void AddEffect(IPTAnalysisNode n, Field f, Label lb,bool wasNull)
        {
            AField af = new AField(n, f, lb,wasNull);
            AddWriteEffect(af);
        }
       
        public void Join(AbstractEffects we)
        {
            if (modifies != we.modifies)
            {
                modifies.AddRange(we.modifies);
            }
        }

        public bool Includes(AbstractEffects we)
        {
          return we.modifies.IsSubset(modifies);
        }
        
        public override bool IsBottom
        {
            get { return modifies.Count == 0; }
        }

        public override bool IsTop
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }
        #endregion

        #region Equals, Hash, ToStrig
        public override bool Equals(object obj)
        {
            AbstractEffects we = obj as AbstractEffects;

            return we != null && modifies.Equals(we.modifies);
        }

        public override int GetHashCode()
        {
            return modifies.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("effects: {0}\n", Modifies);
        }
        #endregion

        #region IDataFlowState implementation
        public void Dump()
        {
            Console.Out.WriteLine(ToString());
        }
        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return modifies.GetEnumerator();
        }

        #endregion
    }

    
    /// <summary>
    /// VariableEffect: Represent a Read/Write effects over a variable
    /// That is, the variable and a chain of fields until the 
    /// (read) written field
    /// </summary>
    public class VariableEffect
    {
        Variable v;
        List<Field> path;
        bool fieldWasNull;
        Label lb;
        // bool wasNull;

        #region Contstructors
        public VariableEffect(Variable v, Label lb)
        {
            this.v = v;
            this.lb = lb;
            this.path = new List<Field>();
        }
        #endregion 

        #region Properties
        public Variable Variable
        {
            get { return v; }
        }
        public Label Label
        {
            get { return lb; }
        }
        public List<Field> FieldChain
        {
            get { return path; }
        }
        public bool FieldWasNull
        {
            get { return fieldWasNull; }
        }

        #endregion

        #region Modifiers
        public void AddField(Field f)
        {
            path.Add(f);
            fieldWasNull = false;
        }
        public void AddField(Field f, bool wasbool)
        {
            path.Add(f);
            fieldWasNull = wasbool;
        }
        #endregion

        #region ToString, Equals, Hash
        public override string ToString()
        {
            string res = v.Name.Name;
            foreach (Field f in path)
                res = ApplyFieldName(res, f);
            return res;
        }
        /// <summary>
        /// Used to give format to a field dereference (.f, [.], *)
        /// </summary>
        /// <param name="pathString"></param>
        /// <param name="f"></param>
        /// <returns></returns>
        private static string ApplyFieldName(string pathString, Field f)
        {
            if (f.Equals(PTGraph.arrayField))
            {
                pathString += "[.]";
            }
            else
            {
                if (f.Equals(PTGraph.allFields))
                {
                    pathString = pathString + "." + "*";
                }
                else if (f.Equals(PTGraph.allFieldsNotOwned))
                {
                    pathString = pathString + "." + "$";
                }
                else
                {
                    if (!f.Equals(PTGraph.asterisk))
                    {
                        pathString = pathString + "." + f.Name.Name;
                    }
                    else
                    {
                        pathString = "*(" + pathString + ")";
                    }
                }
            }
            return pathString;
        }
        public override bool Equals(object obj)
        {
            bool res;
            VariableEffect ve = obj as VariableEffect;
            res = ve != null && this.v.Equals(ve.v)
                && this.lb.Equals(ve.lb);
            res = res && path.Count == ve.path.Count;
            {
                for (int i = 0; i < this.path.Count;i++ )
                {
                    if (!this.path[i].Name.Name.Equals(ve.path[i].Name.Name))
                        return false;
                }
            }
            return res;
        }
        public override int GetHashCode()
        {
            return this.v.GetHashCode()+this.lb.GetHashCode()+path.Count;
        }
        #endregion
    }
    /// <summary>
    /// Represents the RetValue of a Method
    /// </summary>
    #endregion
    

    /// <summary>
    /// The dataflow analysis for the method under analysis 
    /// </summary>
    public class PointsToAndWriteEffectsInferer : PointsToInferer // ForwardDataFlowAnalysis
    {

        //internal CfgBlock currBlock;
        //PointsToAndWriteEffectsInstructionVisitor iVisitor;
        //internal TypeSystem typeSystem;
        //internal IDataFlowState exitState;

        //internal int iterationsCounter=0;

        // internal INonNullInformation nonNullInfo;

        internal bool isPure;

        /// <summary>
        /// A reference to the Main Analysis class
        /// To get information about other methods under analysis
        /// </summary>
        //internal WeakPurityAndWriteEffectsAnalysis pointsToWEAnalysis;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="t"></param>
        /// <param name="pta"></param>
        public PointsToAndWriteEffectsInferer(TypeSystem t, WeakPurityAndWriteEffectsAnalysis pta)
            : base(t,pta)
        {
        }
        
        internal override PointsToInstructionVisitor GetInstructionVisitor(PointsToInferer pti)
        {
            return new PointsToAndWriteEffectsInstructionVisitor((PointsToAndWriteEffectsInferer)pti); 
          
        }

        public bool IsPure { get { return isPure;  } }

        protected override void ComputeBeforeDataflow(Method m)
        {
            /*
            if (this.pointsToStateAnalysys.analyzer != null)
                //nonNullInfo = NonNullChecker.Check(typeSystem, method, this.pointsToStateAnalysys.analyzer);
                nonNullInfo = this.pointsToStateAnalysys.analyzer.NonNullInfo;
            else
                nonNullInfo = null;
            */ 
            base.ComputeBeforeDataflow(m);
        }
        /// <summary>
        /// This method is called when at the end of method dataflow analysis. 
        /// We add an special treatment from methods that created closures.
        /// If an effect is over a field that corresponds to a variable of the enclosing 
        /// method we add the effect also in the enclosing method.
        /// DIEGO-TODO: EffectsInVariables is VERY SLOW. Try to improve it
        /// </summary>
        /// <param name="m"></param>
      protected override void ComputeAfterDataflow(Method m) {
        // OJO!: I should also record read effects
        if (exitState != null) {
          PointsToAndWriteEffects ptwe = (PointsToAndWriteEffects)exitState;

          // If it is a closure method...
          if (PointsToAnalysis.IsCompilerGenerated(m)) {
            // Get enclosing method
            PointsToAndWriteEffects enclosingState = (PointsToAndWriteEffects)this.pointsToStateAnalysys.EnclosingState(m.DeclaringType);
            if (enclosingState != null) {
              // For each write effect
              foreach (AField af in ptwe.WriteEffects) {
                // Get the associated write effect in proram variables
                Set<VariableEffect> writeEffectsInVariables = ptwe.EffectsInVariables(af);

                foreach (VariableEffect vEffect in writeEffectsInVariables) {
                  // If the effect is over a this.field (this.f.g => |vEffec|>=2) 
                  // Or directly this.? (meaning all reacheacle)
                  if (m.ThisParameter != null
                     && vEffect.Variable.Equals(m.ThisParameter)
                     && vEffect.FieldChain.Count >= 2
                       || (vEffect.FieldChain.Count >= 1 && vEffect.FieldChain[0].Equals(PTGraph.allFields))) {
                    // Get the field to see if it is actually a variable in the enclosing method
                    Field f = vEffect.FieldChain[0];
                    Variable enclosingMethodVar = enclosingState.GetVariableByName(f.Name.Name);
                    if (enclosingMethodVar != null) {
                      // Set the write effect in the enclosing method
                      foreach (IPTAnalysisNode n in enclosingState.Values(enclosingMethodVar)) {
                        Label lb = enclosingState.MethodLabel;
                        lb.Statement.SourceContext = enclosingState.Method.SourceContext;
                        AField newEffect = new AField(n, vEffect.FieldChain[1], af.Label);
                        enclosingState.WriteEffects.AddWriteEffect(newEffect);
                        enclosingState.allWriteEffects.AddWriteEffect(newEffect);
                        }
                      }
                    }
                  }
                }
              }

            }
          }
        isPure = this.VerifyPurity();
        base.ComputeAfterDataflow(m);
        }
        
        /// <summary>
        /// Return the results of the analysis on exit of the CFG
        /// </summary>
        public PointsToAndWriteEffects PointsToWE
        {
            get { return (PointsToAndWriteEffects)exitState; }
        }

        /// <summary>
        /// Verify Purity in the Final State
        /// </summary>
        /// <returns></returns>
        public bool VerifyPurity()
        {
            if (PointsToWE == null)
                return false;
            PointsToAndWriteEffects ptwe = (PointsToAndWriteEffects)exitState;
            return ptwe.VerifyPurity();
        }

        // Visit the block in the CFG 
        protected override IDataFlowState VisitBlock(CfgBlock block, IDataFlowState stateOnEntry)
        {
            return base.VisitBlock(block, stateOnEntry);
        }
        protected override bool HasToVisit(PointsToState state)
        {
            // If there are too many calls to non analyzable methods
            // starts to ignore the statements
            PointsToAndWriteEffects ptwe = (PointsToAndWriteEffects)state;
            
            WeakPurityAndWriteEffectsAnalysis wpea = (WeakPurityAndWriteEffectsAnalysis) this.pointsToStateAnalysys;
            bool res = (!pointsToStateAnalysys.BoogieMode 
                        ||ptwe.CallsToNonAnalyzable.Count < wpea.maxCallToNonAnalyzable);

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
            PointsToAndWriteEffects ptwe = (PointsToAndWriteEffects)dfstate;
            // PointsToAndWriteEffects ptweOld = new PointsToAndWriteEffects(ptwe);
            IDataFlowState res = base.VisitStatement(block, statement, dfstate);
            return res;
            
        }

        /// <summary>
        /// Merge two PtWe
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
            return base.Merge(previous,joinPoint,atMerge,incoming,out resultDiffersFromPreviousMerge, out mergeIsPrecise);
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
            base.SplitExceptions(handler, ref currentHandlerState, out nextHandlerState);
            return;
        }
    }

    /// <summary>
    /// Instruction visitor. Implement the transfer function of the dataflow analysis
    /// </summary>
    internal class PointsToAndWriteEffectsInstructionVisitor : PointsToInstructionVisitor //InstructionVisitor
    {
        // A reference to the analyzer for the method
        //private PointsToAndWriteEffectsInferer pta;
        // A reference to the global analysis
        //private WeakPurityAndWriteEffectsAnalysis weakPurityAnalysis;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="pta"></param>
        public PointsToAndWriteEffectsInstructionVisitor(PointsToAndWriteEffectsInferer pta): base(pta)
        {
            //this.pta = pta;
            //weakPurityAnalysis = this.pta.pointsToWEAnalysis;
        }
        

        protected override object DefaultVisit(Statement stat, object arg)
        {
            return base.DefaultVisit(stat, arg);

        }
        protected override bool ForcedByAnnotations(Method m)
        {
            bool res = PointsToAndEffectsAnnotations.IsAnnotated(m);
            return res;
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
    public class WriteEffectsElementFactory : ElementFactory
    {
        public WriteEffectsElementFactory(PointsToAnalysis pta) : base(pta) { }
        public override PointsToState NewElement(Method m)
        {
            return new PointsToAndWriteEffects(m,this.pta);
        }
    }
    public class WeakPurityAndWriteEffectsAnalysis : PointsToAnalysis // StandardVisitor
    {
        public static bool checkAllMethods = false;
        #region Attributes
        //internal static int counter = 0;

        //private TypeSystem typeSystem;
        //private bool interProceduralAnalysis = true;
        //internal bool fixPointForMethods= false;
        //internal  bool standAloneApp = false;
        //internal int maximumStackDepth = 3;
        //internal int callStackDepth = 0;
        
        internal int maxCallToNonAnalyzable = 1;
        internal int maxWriteEffects = 1;
        //internal bool boogieMode = false;


        //internal Analyzer analyzer = null;

        private Dictionary<Method, Boolean> purityMap = new Dictionary<Method, bool>();
        //private Dictionary<Method, PointsToAndWriteEffects> pointsToWEMap = new Dictionary<Method, PointsToAndWriteEffects>();

        //private Set<Method> alreadyAnalyzedMethods = new Set<Method>();

        //internal Node unitUnderAnalysis;

        
        // Just for statistics
        public int numberOfPures = 0;
        public int numberOfDeclaredPure = 0;

        internal Set<Method> needToBePure = new Set<Method>();
        
        // private static bool assumeMorePures = false;
        #endregion

        #region Constructors
        public WeakPurityAndWriteEffectsAnalysis(TypeSystem t):base(t)
        {
            
        }

        /// <summary>
        /// Constructor with a given Node. 
        /// We used to compute the set of nodes in this node and (optionally) bound the set of 
        /// analyzable methods in the interprocedural analysis
        /// </summary>
        /// <param name="t"></param>
        /// <param name="node"></param>
        public WeakPurityAndWriteEffectsAnalysis(TypeSystem t, Node node): base(t,node)
        {
            
        }
        public WeakPurityAndWriteEffectsAnalysis(TypeSystem t, Node node, bool interProcedural): base(t,node,interProcedural)
        {
            
        }
        public WeakPurityAndWriteEffectsAnalysis(TypeSystem t, Node node, bool interProcedural, bool fixPoint): base(t,node,interProcedural, fixPoint)
        {
            
        }

        public WeakPurityAndWriteEffectsAnalysis(TypeSystem t, Node node, bool interProcedural, bool fixpoint, int maxDepth)
            :base(t,node,interProcedural, fixpoint, maxDepth) 
        {
            
        }

        protected override void Init(TypeSystem t, Node node, bool interProcedural, bool fixpoint, int maxDepth)
        {
            base.Init(t,node, interProcedural, fixpoint, maxDepth);
            factory = new WriteEffectsElementFactory(this);
        }

        /// <summary>
        /// Same constructor but with the analyzer
        /// </summary>
        /// <param name="analyzer"></param>
        /// <param name="t"></param>
        /// <param name="node"></param>
        public WeakPurityAndWriteEffectsAnalysis(Analyzer analyzer, TypeSystem t, Node node)
            :base(analyzer,t,node)
        {
            
        }
        public WeakPurityAndWriteEffectsAnalysis(Analyzer analyzer, TypeSystem t, Node node, bool interProcedural)
            :base(analyzer,t,node,interProcedural)
        {
         
        }
        public WeakPurityAndWriteEffectsAnalysis(Analyzer analyzer, TypeSystem t, Node node, bool interProcedural, bool fixPoint)
            :base(analyzer,t,node,interProcedural, fixPoint)
        {
         
        }

        public WeakPurityAndWriteEffectsAnalysis(Analyzer analyzer, TypeSystem t, Node node, bool interProcedural, bool fixpoint, int maxDepth)
            :base(analyzer,t,node,interProcedural,fixpoint,maxDepth)
        {
         
        }

        public WeakPurityAndWriteEffectsAnalysis(Visitor callingVisitor)
            : base(callingVisitor) 
        {
            
        }
        
        #endregion

        #region Properties
        public Set<Method> MethodsRequiredToBePure
        {
            get { return needToBePure; }
        }
        public TypeSystem TypeSystem
        {
            get { return typeSystem; }
        }
        //public bool BoogieMode
        //{
        //    get { return this.boogieMode; }
        //    set { this.boogieMode = value; }
        //}
        //public bool StandAloneApp
        //{
        //    get { return this.standAloneApp; }
        //    set 
        //    { 
        //        this.standAloneApp = value; 
        //        // For Statistics DELETE
        //        //  assumeMorePures = standAloneApp; 
        //    }
          
        //}
        //public bool IsInterProceduralAnalysys
        //{
        //    get { return interProceduralAnalysis; }
        //}
        public int MaxCallsToNonAnalyzable
        {
            get { return this.maxCallToNonAnalyzable; }
            set { this.maxCallToNonAnalyzable = value; }
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
             return base.VisitTypeNode(typeNode);
        }

        /// <summary>
        /// Idem previous
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public override Class VisitClass(Class c)
        {
      
            return base.VisitClass(c);
        }

        public override Method VisitMethod(Method method)
        {
            return base.VisitMethod(method);
        }
      public override bool NeedToVerify(Method method) {
        bool res = false;
        res = res || WeakPurityAndWriteEffectsAnalysis.checkAllMethods;
        res = res || (PointsToAndEffectsAnnotations.IsDeclaredPure(method) && !PointsToAndEffectsAnnotations.IsPurityForcedByUser(method)) 
                  || needToBePure.Contains(method);
        res = res || HasAnnotationsToCheck(method);
        return res;
      }
        
        private static bool HasAnnotationsToCheck(Method method)
        {
            bool res = !PointsToAndEffectsAnnotations.IsDeclaredWritingGlobals(method);
            res = res || !PointsToAndEffectsAnnotations.IsDeclaredReadingGlobals(method);
            res = res || !PointsToAndEffectsAnnotations.IsDeclaredAccessingGlobals(method);
            res = res || PointsToAndEffectsAnnotations.IsDeclaredWriteConfined(method);
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
        public override Method AnalysisForMethod(Method method)
        {
            return base.AnalysisForMethod(method);
        }
        // This is used when the method was already analyzed for points to
        protected override void ComputeOnly(Method method)
        {
            ComputeOnlyPurity(method);
            base.ComputeOnly(method);
        }

        
        #region FixPoint Computation
        /// <summary>
        /// Perform a fixpoint computation over a ser of methods (in general a strongly connected component).
        /// It perform the interprocedural analysis for each method, reanalysing any callers that require updating. 
        /// </summary>
        protected override Set<Method> FixPoint()
        {
            return base.FixPoint();
        }

        /// <summary>
        /// Check whether dataflow analysis changes or not
        /// </summary>
        /// <param name="m"></param>
        /// <param name="newResult"></param>
        /// <returns></returns>
        protected override bool AnalysisResultsChanges(Method m, PointsToState newResult)
        {

            bool res = base.AnalysisResultsChanges(m,newResult);
            return res;
        }
        #endregion 

        #region Methods for starting IntraProcedural analysis
        /// <summary>
        /// Analyze a given method.
        /// That is, perform the IntraProdecural Dataflow Analysis
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public override bool AnalyzeMethod(Method method)
        {
            bool hasChanged = base.AnalyzeMethod(method);

            
            return hasChanged;
        }
        protected override void AfterComputeInfererForMethod(Method m, PointsToInferer pta)
        {
            //Save the results
            PointsToAndWriteEffectsInferer ptwe = (PointsToAndWriteEffectsInferer)pta;
            SetPurity(m, ptwe.IsPure);
            base.AfterComputeInfererForMethod(m, pta);
        }
        
        public override  PointsToInferer CreateIntraProcAnalysis()
        {
            return new PointsToAndWriteEffectsInferer(this.typeSystem, this);
        }
        public override ControlFlowGraph GetCFG(Method method)
        {
            return base.GetCFG(method);

        }
        #endregion 

        
        /// <summary>
        /// Check if we already cumputed the purity of the method under analysis
        /// If not, the methdo purity is computed and saved
        /// </summary>
        /// <param name="m"></param>
        private void ComputeOnlyPurity(Method m)
        {
            if (!PurityHasMethod(m))
            {
                PointsToAndWriteEffects ptwe = GetPurityAnalysis(m);
                bool isPure = ptwe.VerifyPurity();
                SetPurity(m, isPure);
            }
        }

        

        #region Method for registering and querying the analysis results
        public bool GetPurity(Method m)
        {

            //String mName = m.GetFullUnmangledNameWithTypeParameters();
            //return purityMap[mName];
            return purityMap[m];
        }

        public bool GetPurityWithDefault(Method m)
        {
            //String mName = m.GetFullUnmangledNameWithTypeParameters();
            //if(purityMap.ContainsKey(mName))
            //    return purityMap[mName];
            if (purityMap.ContainsKey(m))
                return purityMap[m];
            return false;
        }

        public void SetPurity(Method m, bool isPure)
        {
            //String mName = m.GetFullUnmangledNameWithTypeParameters();
            //purityMap[mName] = isPure;
            purityMap[m] = isPure;
        }

        public bool PurityHasMethod(Method m)
        {
            //String mName = m.GetFullUnmangledNameWithTypeParameters();
            //return purityMap.ContainsKey(mName);
            return purityMap.ContainsKey(m);
        }

        public PointsToAndWriteEffects GetPurityAnalysis(Method m)
        {
            return (PointsToAndWriteEffects)base.GetSummaryForMethod(m);
            
        }

        public PointsToAndWriteEffects GetPurityAnalysisWithDefault(Method m)
        {

            return (PointsToAndWriteEffects)base.GetSummaryForMethodWithDefault(m);
        }

        public void SetPurityAnalysys(Method m, PointsToAndWriteEffects ptwe)
        {
            base.SetSummaryForMethod(m, ptwe);
        }

        public void SetDefaultPurityAnalysys(Method m)
        {
            base.SetDefaultSummaryForMethod(m);
        }

        public bool PurityAnalysisHasMethod(Method m)
        {
            return base.HasSummary(m);
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
        /// Determines whether the method is analyzable or not
        /// for interProc call.
        /// That is, if we can get the method body 
        /// (not abstract, not interface, under our desired analysis scope)
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        
        public override bool IsAnalyzable(Method m)
        {
            return base.IsAnalyzable(m); // && !PointsToAndEffectsAnnotations.IsAssumedPureMethod(m);
       }
       
       protected override bool IsContructorTemplatePure(Method m)
       {
           return base.IsContructorTemplatePure(m);

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
            PointsToAndWriteEffects ptwe = GetPurityAnalysisWithDefault(m);
            PTGraph ptg = ptwe.PointsToGraph;
            bool isPure = GetPurityWithDefault(m);

            #region Display Results (if verbose)
            if (verbose && !ptwe.IsDefault)
            {
                Console.Out.WriteLine("***RESULT***");
                Console.Out.WriteLine("Method:{0}\nPure:{1} Abstract:{2}", m.GetFullUnmangledNameWithTypeParameters(),
                    isPure, m.IsAbstract);

                foreach (Parameter p in ptg.ParameterMap.Keys)
                {
                    String refOrOut = "(value)";
                    if (p.IsOut)
                        refOrOut = "(out)";
                    else
                        if (p.Type is Reference)
                            refOrOut = "(ref)";

                    Console.Out.WriteLine("Parameter: {0} {2} ({1})", p.Name, ptwe.IsReadOnly(p) ? "ReadOnly" : "RW", refOrOut);
                    
                }
            
                Set<VariableEffect> writeEffectsforVariables = ptwe.ComputeWriteEffects();
                foreach (VariableEffect vEffect in writeEffectsforVariables)
                {
                    Statement stat = vEffect.Label.Statement;
                    if (stat != null)
                        Console.Out.WriteLine("{3} In ({0},{1}) Statement:{2}", stat.SourceContext.StartLine,
                            stat.SourceContext.StartColumn, stat.SourceContext.SourceText, vEffect.ToString());
                }
                Console.Out.WriteLine();

                Set<VariableEffect> readEffects = ptwe.ComputeEffects(ptwe.ReadEffects);
                Console.Out.WriteLine("Reads:");
                foreach (VariableEffect readEffect in readEffects)
                {
                    Statement stat = readEffect.Label.Statement;
                    Console.Out.WriteLine("{3} In ({0},{1}) Statement:{2}", stat.SourceContext.StartLine,
                            stat.SourceContext.StartColumn, stat.SourceContext.SourceText, readEffect.ToString());

                }
                Console.Out.WriteLine();

                //if (!GetPurityMap(m))
                {
                    Console.Out.WriteLine(ptwe);
                }
                ptwe.PointsToGraph.GenerateDotGraph(Console.Out);

                Console.Out.WriteLine("****");
            }
            #endregion
            
            #region Information about Escape, Freshness and use of Globals
            // If it was analyzed
            if (!ptwe.IsDefault)
            {
                if (PointsToAndEffectsAnnotations.IsDeclaredFresh(m))
                {
                    bool isFreshReturn = ptwe.CheckFreshness(m);
                    if (!isFreshReturn)
                    {
                        string msg = string.Format("Method {0} is declared fresh but return cannot be proven to be fresh", ptwe.Method.Name);
                        typeSystem.HandleError(m.Name, Error.GenericWarning, msg);
                    }

                }
                if (!PointsToAndEffectsAnnotations.IsDeclaredWritingGlobals(m) && !(m is StaticInitializer)
                  && !(m.IsStatic && m.DeclaringMember is Property))
                {
                    bool isWritingGlobal = ptwe.CheckGlobalWriting(m);
                    if (isWritingGlobal)
                    {
                        string msg = string.Format("Cannot prove that Method {0} does not write global variables", ptwe.Method.Name);
                        typeSystem.HandleError(m.Name, Error.GenericWarning, msg);
                    }
                }
                if (!PointsToAndEffectsAnnotations.IsDeclaredReadingGlobals(m))
                {
                    bool isReadingGlobal = ptwe.CheckGlobalReading(m);
                    if (isReadingGlobal)
                    {
                        string msg = string.Format("Cannot prove that method {0} does not read global variables", ptwe.Method.Name);
                        typeSystem.HandleError(m.Name, Error.GenericWarning, msg);
                    }
                }


              
                foreach (Parameter p in ptg.ParameterMap.Keys)
                {
                    //if (!TypeNode.IsImmutable(p.Type)) 
                    //{
                    //    bool captured;
                    //    if (!PointsToAndEffectsAnnotations.IsDeclaredEscaping(p, out captured))
                    //    {
                    //        bool escapes = ptwe.CheckEscapes(p);
                    //        if (escapes)
                    //        {
                    //            string msg = string.Format("{0} escapes method {1}", p.Name, ptwe.Method.Name);
                    //            if(p!=ptwe.Method.ThisParameter)
                    //              typeSystem.HandleError(p.Name, Error.GenericWarning, msg);
                    //            else
                    //              typeSystem.HandleError(ptwe.Method.Name, Error.GenericWarning, msg);
                    //        }

                    //    }
                    //}
                    if (PointsToAndEffectsAnnotations.IsDeclaredFresh(p))
                    {
                        bool isFreshParameter = ptwe.CheckFreshness(p);
                        if (!isFreshParameter)
                        {
                            string msg = string.Format("{0} declared fresh but cannot prove that it is fresh in method {1}", p.Name, ptwe.Method.Name);
                            typeSystem.HandleError(p.Name, Error.GenericWarning, msg);
                        }

                    }
                }
            }
            #endregion


            
            if (PointsToAndEffectsAnnotations.IsDeclaredPure(m) || this.needToBePure.Contains(m))
            {
                IEnumerable<Pair<Statement, string>> problems = ptwe.ComputePurityWarnings();
                foreach (Pair<Statement, string> problem in problems)
                {
                    if (problem.Fst.SourceContext.StartLine == 0)
                    {
                        problem.Fst.SourceContext = m.SourceContext;
                    }
                    
                    typeSystem.HandleError(problem.Fst, Error.GenericWarning, problem.Snd);
                }
                numberOfDeclaredPure++;
                if (isPure)
                    numberOfPures++;
            }
        }


        /// <summary>
        /// Used for test purposes
        /// </summary>
        /// <param name="m"></param>
        private void printStatements(Method m)
        {
            StatementList stats = m.Body.Statements;
            for (int i = 0; i < stats.Count; i++)
            {
                if (stats[i] is Block)
                {
                    Block block = (Block)stats[i];
                    StatementList stats2 = block.Statements;
                    if (stats2 != null)
                        for (int j = 0; j < stats2.Count; j++)
                        {
                            Console.Out.WriteLine("{0} {1}", CodePrinter.StatementToString(stats2[j]), stats2[j].NodeType);
                        }
                }
                else
                {
                    if (stats[i] != null)
                        Console.Out.WriteLine("{0} {1}", CodePrinter.StatementToString(stats[i]), stats[i].NodeType);
                }
            }
        }
        #endregion

    }


 
    
}

