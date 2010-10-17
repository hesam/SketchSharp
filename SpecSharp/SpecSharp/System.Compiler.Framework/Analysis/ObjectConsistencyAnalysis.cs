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
using System.Compiler.WPurity;
#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler.Analysis {
#endif
    public class ObjectConsistencyAnalysis : PointsToAnalysis //ReentrancyAnalysis
    {
        // public static bool verbose = false;
        protected Dictionary<Method, INonNullInformation> nonNullInfoCache = new Dictionary<Method, INonNullInformation>();
        #region Constructors
        public ObjectConsistencyAnalysis(TypeSystem t): base(t)
        {
            // factory = new ExposureElementFactory(this);
        }
        
        /// <summary>
        /// Constructor with a given Node. 
        /// We used to compute the set of nodes in this node and (optionally) bound the set of 
        /// analyzable methods in the interprocedural analysis
        /// </summary>
        /// <param name="t"></param>
        /// <param name="node"></param>
        public ObjectConsistencyAnalysis(TypeSystem t, Node node): base(t, node)
        {
            
        }
        public ObjectConsistencyAnalysis(TypeSystem t, Node node, bool interProcedural)
            : base(t,node,interProcedural)
        {
            
        }
        public ObjectConsistencyAnalysis(TypeSystem t, Node node, bool interProcedural, bool fixPoint) 
            : base(t,node,interProcedural,fixPoint)
        {
            
        }

        public ObjectConsistencyAnalysis(TypeSystem t, Node node, bool interProcedural, bool fixpoint, int maxDepth)
            :base(t,node,interProcedural,fixpoint, maxDepth)
        {
            
        }
        protected override void Init(TypeSystem t, Node node, bool interProcedural, bool fixpoint, int maxDepth)
        {
            base.Init(t, node, interProcedural, fixpoint, maxDepth);
            factory = new ConsistentyElementFactory(this);
            if (ContractDeserializerContainer.ContractDeserializer == null)
            {
                IContractDeserializer cd = new Omni.Parser.ContractDeserializer();
                ContractDeserializerContainer.ContractDeserializer = cd;
            }
        }
        /// <summary>
        /// Same constructor but with the analyzer
        /// </summary>
        /// <param name="analyzer"></param>
        /// <param name="t"></param>
        /// <param name="node"></param>
        /// 
        
        public ObjectConsistencyAnalysis(Analyzer analyzer, TypeSystem t, Node node)
            : base(analyzer,t, node)
        {
            
        }
        public ObjectConsistencyAnalysis(Analyzer analyzer, TypeSystem t, Node node, bool interProcedural)
            : base(analyzer, t, node,interProcedural)
        {
            
        }
        public ObjectConsistencyAnalysis(Analyzer analyzer, TypeSystem t, Node node, bool interProcedural, bool fixPoint)
            : base(analyzer, t, node,interProcedural, fixPoint)
        {
            
        }

        public ObjectConsistencyAnalysis(Analyzer analyzer, TypeSystem t, Node node, bool interProcedural, bool fixpoint, int maxDepth)
            : base(analyzer, t, node, interProcedural, fixpoint,maxDepth)
        {
            
        }

        public ObjectConsistencyAnalysis(Visitor callingVisitor)
            : base(callingVisitor) 
        {
            
        }
         
        #endregion
        public override PointsToInferer  CreateIntraProcAnalysis()
        {
            return new ObjectConsistencyInferer(this.typeSystem,this);
        }
        public override Method VisitMethod(Method method)
        {
            if (this.analyzer != null)
            {
                INonNullInformation nonNullInfo = this.analyzer.NonNullInfo;
                if (nonNullInfo == null)
                {
                    nonNullInfo = NonNullChecker.Check(this.typeSystem, method,
                        this.analyzer);
                    nonNullInfoCache[method] = nonNullInfo;
                }
            }

            // To be sure that requires are computed
            // as I am bypassing that blocks of code
            if (method.Contract != null)
            {
                RequiresList requiresL = method.Contract.Requires;
                EnsuresList ensuresL = method.Contract.Ensures;
            }
            return base.VisitMethod(method);
        }
        public INonNullInformation GetNonNullInfo(Method m)
        {
            INonNullInformation nonNullInfo = null;
            if (nonNullInfoCache.ContainsKey(m))
                return nonNullInfoCache[m];

            if (this.analyzer != null)
            {
                nonNullInfo = NonNullChecker.Check(this.typeSystem, m,
                            this.analyzer);
                nonNullInfoCache[m] = nonNullInfo;

            }
            return nonNullInfo;
        }
        

    }
    interface ConsistencyInfo
    {
        bool IsExposable(Variable v);
        bool IsExposable(IPTAnalysisNode n);
        bool IsExposable(Set<IPTAnalysisNode> ns);
        // Set<VariableEffect> InconsistentPaths(Statement s);
    }
    public enum ObjectStatus { no, exposable, consistent, peerconsistent } ;
    public class ContractConsistencyInfo
    {
        
        static Dictionary<Method, ContractConsistencyInfo> cache = new Dictionary<Method,ContractConsistencyInfo>();
        Dictionary<Parameter, ObjectStatus> requiresExposable;
        Dictionary<Parameter, ObjectStatus> ensuresExposable;
        public ContractConsistencyInfo()
        {
            requiresExposable = new Dictionary<Parameter, ObjectStatus>();
            ensuresExposable = new Dictionary<Parameter, ObjectStatus>();
        }
        
        private void Init(Method m, Dictionary<Parameter, ObjectStatus> exposable, bool value)
        {
            if (m.Parameters != null)
            {
                foreach (Parameter p in m.Parameters)
                {
                    exposable[p] = value? ObjectStatus.exposable: ObjectStatus.no;
                }
            }
            if (!m.IsStatic)
                exposable[m.ThisParameter] = value ? ObjectStatus.exposable : ObjectStatus.no;
        }
        public bool RequiresExposable(Parameter p)
        {
            return requiresExposable.ContainsKey(p) && requiresExposable[p] != ObjectStatus.no; ;
        }
        public bool EnsuresExposable(Parameter p)
        {
            return ensuresExposable.ContainsKey(p) && ensuresExposable[p]!=ObjectStatus.no;
        }
        private static ContractConsistencyInfo ComputeExposureForMethod(Method m)
        {
            ContractConsistencyInfo ei = null;
            if (!cache.ContainsKey(m))
            {
                ei = new ContractConsistencyInfo();
                MethodContract contract = m.Contract;
                if (!m.ApplyDefaultContract && !m.IsPropertyGetter)
                {
                    ei.Init(m, ei.requiresExposable, false);
                    ei.Init(m, ei.ensuresExposable, false);
                    ContractVisitor requiresVisitor = new ContractVisitor(ei.requiresExposable, m);
                    // requiresVisitor.VisitMethodContract(contract);
                    requiresVisitor.VisitRequiresList(contract.Requires);
                    ContractVisitor ensuresVisitor = new ContractVisitor(ei.ensuresExposable, m);
                    ensuresVisitor.VisitEnsuresList(contract.Ensures);

                }
                else
                {
                    ei.Init(m, ei.requiresExposable, true);
                    ei.Init(m, ei.ensuresExposable, true);
                }
                
            }
            else
            {
                ei = cache[m];
            }
            return ei;
        }
        public static bool RequiresExposable(Method m, Parameter p)
        {
            ContractConsistencyInfo ei = ComputeExposureForMethod(m);
            return ei.RequiresExposable(p);
        }
        public static  bool EnsuresExposable(Method m, Parameter p)
        {
            ContractConsistencyInfo ei = ComputeExposureForMethod(m);
            return ei.EnsuresExposable(p);
        }

    }

    internal class ContractVisitor: StandardVisitor
    {
        
        Dictionary<Parameter, ObjectStatus> exposable;
        Method mua;

        public ContractVisitor(Dictionary<Parameter, ObjectStatus> ei, Method mua)
            : base()
        {
            this.exposable = ei;
            this.mua = mua; 
        }
        public override RequiresPlain VisitRequiresPlain(RequiresPlain plain)
        {
            plain.Condition = this.VisitBooleanExpression(plain.Condition);
            return plain;
        }
        public override EnsuresNormal VisitEnsuresNormal(EnsuresNormal normal)
        {
            normal.PostCondition = this.VisitBooleanExpression(normal.PostCondition);
            return normal;
        }
        public override Expression VisitMethodCall(MethodCall call)
        {
            if (call != null)
            {
                CheckMethodCall(call,false);
            }
            return call;
        }
        private void CheckMethodCall(MethodCall call, bool negate)
        {
            ObjectStatus status = ObjectStatus.no;

            if (call != null)
            {
                MemberBinding mb = (MemberBinding)call.Callee;
                if (mb.BoundMember != null)
                {
                    // Variable receiver = (Variable)mb.TargetObject;
                    Variable receiver = null;
                    Method callee = mb.BoundMember as Method;
                    if (IsMatchingExposableMethod(callee))
                    {
                        status = ObjectStatus.exposable;
                        //if (IsFrameExposedMethod(callee) || IsFrameExposableMethod(callee))
                        if (IsFrameExposableMethod(callee))
                        {
                            receiver = mua.ThisParameter;
                     
                        }
                    }
                    if (IsMatchingExposableMethod(callee))
                    {
                        status = ObjectStatus.exposable;
                        if (IsFrameConsistentMethod(callee))
                        {
                          receiver = mua.ThisParameter;
                                
                        }
                    }
                    if (receiver != null)
                    {
                         Parameter p = (Parameter)receiver;
                         if (!negate)
                         {
                             this.exposable[p] = status;
                         }
                         else
                         {
                             this.exposable[p] = status == ObjectStatus.no ? ObjectStatus.exposable : ObjectStatus.no;   
                         }
                         
                    }
                }
            }
        }
        private bool IsMatchingExposableMethod(Method callee)
        {
            bool res = false;
            res = res || callee.Equals(IsExposableMethod);
            res = res || callee.Equals(IsExposedMethod);
            res = res || callee.Equals(IsConsistenMethod);
            res = res || callee.Equals(FrameExposableMethod);
            res = res || callee.Equals(FramesConsistenMethod);
            // For some reason SystemTypes.Guard fails to bring GetMethod(Identifier.For("FrameIsExposable"))
            res = res || IsFrameExposableMethod(callee);
            //res = res || IsFrameExposedMethod(callee);
            return res;

        }
        private bool IsMatchingConsistentMethod(Method callee)
        {
            bool res = false;
            res = res || callee.Equals(IsConsistenMethod);
            res = res || callee.Equals(FramesConsistenMethod);
            // For some reason SystemTypes.Guard fails to bring GetMethod(Identifier.For("FrameIsExposable"))
            res = res || IsFrameConsistentMethod(callee);
            return res;

        }


        private bool IsFrameConsistentMethod(Method callee)
        {
            return callee.Name.Name.Equals("FrameIsConsistent");
        }

        private bool IsFrameExposableMethod(Method callee)
        {
            return  callee.Name.Name.Equals("FrameIsExposable");
        }
        private bool IsFrameExposedMethod(Method callee)
        {
            return callee.Name.Name.Equals("FrameIsExposed");
        }
        public virtual Expression VisitBooleanExpression(Expression expr)
        {
            UnaryExpression uexpr = expr as UnaryExpression;
            // if !
            if (uexpr != null && uexpr.NodeType == NodeType.LogicalNot)
            {
                Expression e = uexpr.Operand = this.VisitBooleanExpression(uexpr.Operand);
                if (e == null) return null;
                MethodCall call = e as MethodCall;
                if (call != null)
                {
                    CheckMethodCall(call, true);
                }
                else
                    return null;
            }
            else
                expr = this.VisitExpression(expr);
            return expr;
        }
        public override MethodContract VisitMethodContract(MethodContract contract)
        {
            return base.VisitMethodContract(contract);
        }

        private Method UnpackMethod = SystemTypes.Guard.GetMethod(Identifier.For("StartWritingTransitively"));
        private Method PackMethod = SystemTypes.Guard.GetMethod(Identifier.For("EndWritingTransitively"));
        private Method IsExposedMethod = SystemTypes.Guard.GetMethod(Identifier.For("FrameIsExposed"));
        private Method IsExposableMethod = SystemTypes.Guard.GetMethod(Identifier.For("get_IsExposable"));
        private Method IsConsistenMethod = SystemTypes.Guard.GetMethod(Identifier.For("get_IsConsistent"));
        private Method FrameExposableMethod = SystemTypes.Guard.GetMethod(Identifier.For("FrameIsExposable"));
        private Method FramesConsistenMethod = SystemTypes.Guard.GetMethod(Identifier.For("FrameIsConsistent"));
        // {Microsoft.Contracts.Guard.FrameIsExposable}
    }

    public class ConsistencyState : PointsToState, ConsistencyInfo
    {
        internal sealed class ConsistencyLattice : MathematicalLattice
        {

            /// <summary>
            /// Ordering:
            /// 
            ///   A lt B   iff
            ///   
            ///   !A.NonNull implies !B.NonNull
            ///   
            /// </summary>
            public class AVal : AbstractValue
            {
                public readonly bool IsExposable;

                private AVal(bool consistent)
                {
                    this.IsExposable = consistent;
                }


                public static AVal Bottom = new AVal(true);
                public static AVal Top = new AVal(false);
                public static AVal Consistent = Bottom;
                public static AVal MaybeInconsistent = Top;

                private static AVal For(bool consistent)
                {
                    if (consistent) return Consistent;
                    else return MaybeInconsistent;
                }

                public static AVal Join(AVal a, AVal b)
                {
                    bool consistent = a.IsExposable && b.IsExposable;
                    return AVal.For(consistent);
                }

                public static AVal Meet(AVal a, AVal b)
                {
                    bool consistent = a.IsExposable || b.IsExposable;
                    return AVal.For(consistent);
                }

                public override string ToString()
                {
                    if (this.IsExposable) return "Consistent";
                    else return "MaybeInconsistent";
                }

                public override bool IsBottom
                {
                    get { return this == AVal.Bottom; }
                }

                public override bool IsTop
                {
                    get { return this == AVal.Top; }
                }


            }


            protected override bool AtMost(AbstractValue a, AbstractValue b)
            {
                AVal av = (AVal)a;
                AVal bv = (AVal)b;

                return (av.IsExposable || !bv.IsExposable);
            }

            public override AbstractValue Bottom
            {
                get
                {
                    return AVal.Bottom;
                }
            }

            public override AbstractValue Top
            {
                get
                {
                    return AVal.Top;
                }
            }

            public override AbstractValue NontrivialJoin(AbstractValue a, AbstractValue b)
            {
                return AVal.Join((AVal)a, (AVal)b);
            }

            public override MathematicalLattice.Element NontrivialMeet(MathematicalLattice.Element a, MathematicalLattice.Element b)
            {
                return AVal.Meet((AVal)a, (AVal)b);
            }


            private ConsistencyLattice() { }

            public static ConsistencyLattice It = new ConsistencyLattice();

        }

        protected Dictionary<IPTAnalysisNode, AbstractValue> consistency;
        internal Set<IPTAnalysisNode> exposedNodes;
        
        

        INonNullState nonNullState;

        public ConsistencyState(Method m, PointsToAnalysis pta)
            : base(m,pta)
        {
            consistency = new Dictionary<IPTAnalysisNode, AbstractValue>();
            
            
            exposedNodes = new Set<IPTAnalysisNode>();
            this.nonNullState = null;
        }
        public ConsistencyState(ConsistencyState exp)
            : base(exp)
        {
            this.consistency = new Dictionary<IPTAnalysisNode, AbstractValue>(exp.consistency);
            
            
            exposedNodes = new Set<IPTAnalysisNode>(exp.exposedNodes);
            this.nonNullState = exp.nonNullState;
        }
        public INonNullState NonNullState
        {
            get { return nonNullState; }
            set { nonNullState = value; }
        }
        #region SemiLattice Operations (Join, Includes, IsBottom)
        public override bool IsBottom
        {
            get
            {
                return base.IsBottom && consistency.Count == 0;
            }
        }

        public override bool IsTop
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        public override PointsToState Copy()
        {
            return new ConsistencyState(this);
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
                ConsistencyState ptgE = (ConsistencyState)ptg;
                if (consistency!= ptgE.consistency)
                    JoinConsistencyNodes(ptgE.consistency);
                
                
                
                //if (exposedNodes != ptgE.exposedNodes)
                //    exposedNodes.AddRange(ptgE.exposedNodes);
            }
        }

        public void JoinConsistencyNodes(Dictionary<IPTAnalysisNode, AbstractValue > consis2)
        {
            foreach (IPTAnalysisNode n in consis2.Keys)
            {
                AbstractValue c2 = consis2[n];
                if (consistency.ContainsKey(n))
                {
                    AbstractValue c1 = consistency[n];
                    if (!c1.Equals(c2))
                    { }

                    AbstractValue c12 = ConsistencyLattice.It.Join(c1, c2);
                    consistency[n] = c12;
                }
                else
                    consistency[n] = c2;
            }

        }

        

        /// <summary>
        ///  Inclusion check for two PointsToAndWriteEffects
        /// </summary>
        /// <param name="ptgWe"></param>

        public override bool Includes(PointsToState cState2)
        {
            bool includes = base.Includes(cState2);
            ConsistencyState pE = (ConsistencyState)cState2;
            includes = includes && IncludesConsistency(pE.consistency);
            return includes;
        }
        private bool IncludesConsistency(Dictionary<IPTAnalysisNode, AbstractValue> consis2)
        {
            bool includes = consistency.Count >= consis2.Count;
            if (includes)
            {
                foreach (IPTAnalysisNode n in consis2.Keys)
                {
                    if (consistency.ContainsKey(n))
                    {
                        AbstractValue c1 = consistency[n];
                        AbstractValue c2 = consis2[n];
                        if (!ConsistencyLattice.It.LowerThanOrEqual(c2, c1))
                            return false;
                    }
                    else
                        return false; 
                }
            }
            return includes;
        }
        #endregion

        public void UpdateNode(IPTAnalysisNode n, AbstractValue a)
        {
            consistency[n] = a;
        }
        public void WeakUpdateNode(IPTAnalysisNode n, AbstractValue a)
        {
            if (consistency.ContainsKey(n))
                consistency[n] = ConsistencyLattice.It.Join(a, consistency[n]);
            else
                UpdateNode(n, a);
        }
        public void AssignConsistency(Variable v, AbstractValue a)
        {
            AssignConsistency(v, a, true);
        }
        public void AssignConsistency(Variable v, AbstractValue a, bool strong)
        {
            Nodes locs = Values(v);
            AssignConsistency(locs, a, strong);
        }
        public void AssignConsistency(Variable v, Field f, AbstractValue a,bool strong)
        {
            Nodes locs = Values(v,f);
            AssignConsistency(locs, a, strong);
        }
        public void AssignConsistency(Nodes ns, AbstractValue a, bool strong)
        {
            foreach (IPTAnalysisNode n in ns)
            {
                if (!strong)
                    WeakUpdateNode(n, a);
                else
                    UpdateNode(n, a);
            }
        }


        public void AssumeConsistent(Variable v)
        {
            AssignConsistency(v, ConsistencyLattice.AVal.Consistent,true);
        }
        public void AssumeNonConsistent(Variable v)
        {
            AssignConsistency(v, ConsistencyLattice.AVal.MaybeInconsistent, true);
        }
        public void AssumeConsistent(Variable v, Field f)
        {
            AssignConsistency(v, f, ConsistencyLattice.AVal.Consistent, true);
        }
        public void AssumeNonConsistent(Variable v, Field f)
        {
            AssignConsistency(v, f, ConsistencyLattice.AVal.MaybeInconsistent, true);
        }
        protected void AssumeConsistent(Nodes ns)
        {
            AssignConsistency(ns, ConsistencyLattice.AVal.Consistent, true);
        }
        protected void AssumeNonConsistent(Nodes ns)
        {
            AssignConsistency(ns, ConsistencyLattice.AVal.MaybeInconsistent, true);
        }

        protected virtual AbstractValue ConsistencyFor(Variable v)
        {
            AbstractValue c1 = ConsistencyLattice.AVal.Bottom;
            Nodes locs = Values(v);
            c1 = ConsistencyFor(locs);
            return c1;
        }
        protected virtual AbstractValue ConsistencyFor(Set<IPTAnalysisNode> ns)
        {
            if (ns.Count == 0)
                return ConsistencyLattice.AVal.MaybeInconsistent;

            AbstractValue c1 = ConsistencyLattice.AVal.Bottom;
            foreach (IPTAnalysisNode n in ns)
            {
                c1 = ConsistencyLattice.It.Join(c1, ConsistencyFor(n));
            }
            return c1;
        }

        protected virtual AbstractValue ConsistencyFor(IPTAnalysisNode n)
        {
            if (consistency.ContainsKey(n))
                return consistency[n];
            return ConsistencyLattice.AVal.MaybeInconsistent;
            //return ConsistencyLattice.AVal.Consistent;
        }
        public virtual bool IsExposable(Variable v)
        {
            //return false;
            ConsistencyLattice.AVal a = (ConsistencyLattice.AVal) ConsistencyFor(v);
            return a.IsExposable;
        }
        public virtual bool IsExposable(IPTAnalysisNode n)
        {
            ConsistencyLattice.AVal a = (ConsistencyLattice.AVal)ConsistencyFor(n);
            return a.IsExposable;
        }
        public virtual bool IsExposable(Set<IPTAnalysisNode> ns)
        {
            ConsistencyLattice.AVal a = (ConsistencyLattice.AVal)ConsistencyFor(ns);
            return a.IsExposable;
        }

        internal void AssumeExposed(Variable v)
        {
            AssumeExposed(Values(v));
        }
        internal void AssumeExposed(Nodes ns)
        {
            exposedNodes.AddRange(ns);
        }

        internal void AssumeUnExposed(Variable v)
        {
            AssumeUnExposed(Values(v));
        }
        internal void AssumeUnExposed(Nodes ns)
        {
            Set<IPTAnalysisNode> copy = new Set<IPTAnalysisNode>();
            foreach(IPTAnalysisNode  n in exposedNodes)
            {
                if(!ns.Contains(n))
                    copy.Add(n);
            }
            exposedNodes = copy;
        }
        public bool IsExposed(Variable v)
        {
            return IsExposed(Values(v));
        }
        public bool IsExposed(Set<IPTAnalysisNode> ns)
        {
            bool res = ns.IsSubset(exposedNodes);
            return res;
        }


        public override void Dump()
        {
            base.Dump();
            DumpConsistency();
        }
        public void DumpConsistency()
        {
            //for (int i = 0; i < Method.LocalList.Count; i++)
            Console.Out.WriteLine("Consistency for Variables:");
            foreach(PT_Variable v in this.Variables)
            {
                //Variable v = Method.LocalList[i];
                Console.WriteLine("{0}:{1}", v.Variable, ConsistencyFor(v.Variable));
            }
            
            Console.Out.WriteLine("Calls to Warn:");
            
            
        }

        public override void InitMethod(Method method, IEnumerable parameters, Label lb)
        {
            base.InitMethod(method, parameters, lb);
            if (this.Parameters != null)
            {
                foreach (Parameter p in this.Parameters)
                {
                    if (this.MethodRequiresExposable(method, p))
                        AssumeConsistent(p);
                    else
                        AssumeNonConsistent(p);
                }
            }
            if (CciHelper.IsConstructor(Method))
            {
                exposedNodes.AddRange(Values(Method.ThisParameter));
            }

        }

        public override void ApplyStoreField(Variable v1, Field f, Variable v2, Label lb)
        {
            if (f.IsRep || f.Type.IsValueType)
                this.AssumeNonConsistent(v1);
            base.ApplyStoreField(v1, f, v2, lb);
            
        }
        public override void ApplyStoreElement(Variable v1, Variable v2, Label lb)
        {
            Set<VariableEffect> pathsToNode = ComputePathsFromVariableToHeapLocation(Values(v1), MethodLabel);
            foreach (VariableEffect ve in pathsToNode)
            {
                if (this.HasInvariantField(ve))
                {
                    this.AssumeNonConsistent(ve.Variable);
                }
            }
            base.ApplyStoreElement(v1, v2, lb);
        }

        public override void ApplyStoreIndirect(Variable v1, Variable v2, Label lb)
        {
            Set<VariableEffect> pathsToNode = ComputePathsFromVariableToHeapLocation(ValuesIndirect(v1), MethodLabel);
            foreach (VariableEffect ve in pathsToNode)
            {
                if (this.HasInvariantField(ve))
                {
                    this.AssumeNonConsistent(ve.Variable);
                }
            }
            base.ApplyStoreIndirect(v1, v2, lb);
        }
        public override void ApplyLoadField(Variable v1, Variable v2, Field f, Label lb)
        {
            Nodes ns = Values(v2, f);
            AbstractValue consistencyForSource = ConsistencyFor(ns);
            base.ApplyLoadField(v1, v2, f, lb);
            AssignConsistency(v1, consistencyForSource);
        }
        public override void CopyLocVar(Variable v1, Variable v2, Label lb)
        {
            AbstractValue consistencyForSource = ConsistencyFor(v2);
            base.CopyLocVar(v1, v2, lb);
            this.AssignConsistency(v1, consistencyForSource);

        }
        
        public override void CopyLocVar(Variable v1, Variable v2)
        {
            AbstractValue consistencyForSource = ConsistencyFor(v2);
            base.CopyLocVar(v1, v2);
            this.AssignConsistency(v1, consistencyForSource);
        }
        public override void ApplyAnalyzableCall(Variable vr, Method callee, Variable receiver, ExpressionList arguments, PointsToState calleecState, Label lb, out InterProcMapping ipm)
        {
            Nodes receiverValues = Nodes.Empty;
            bool IsReceiverConsistent = false;
                
            if (receiver != null)
            {
                receiverValues = Values(receiver);
                IsReceiverConsistent = IsExposable(receiver);
               
            }

            base.ApplyAnalyzableCall(vr, callee, receiver, arguments, calleecState, lb, out ipm);
            
            if(receiver==null)
                return;

            BeforeUpdateConsystency(vr, callee, receiver, arguments, calleecState, lb,ipm);

            ConsistencyForCalls(callee, lb, receiverValues, IsReceiverConsistent,
                receiver,arguments);

            AfterUpdateConsystency(vr, callee, receiver, arguments, calleecState, lb,ipm);

        }

        protected virtual void  AfterUpdateConsystency(Variable vr, Method callee, Variable receiver, 
            ExpressionList arguments, PointsToState calleecState, Label lb, InterProcMapping ipm)
        {
            
        }

        protected virtual void BeforeUpdateConsystency(Variable vr, Method callee, Variable receiver, 
            ExpressionList arguments, PointsToState calleecState, Label lb, InterProcMapping ipm)
        {
            
        }

        
        public override void ApplyNonAnalyzableCall(Variable vr, Method callee, Variable receiver, ExpressionList arguments, 
            Label lb, out InterProcMapping ipm)
        {
            Nodes receiverValues = Nodes.Empty;
            bool IsReceiverConsistent = false;

            if (receiver != null)
            {
                receiverValues = Values(receiver);
                IsReceiverConsistent = IsExposable(receiver);
            }
            ConsistencyForCalls(callee, lb, receiverValues, IsReceiverConsistent,
                receiver,arguments);

            // OJO!!!!!!
            base.ApplyNonAnalyzableCallOld(vr, callee, receiver, arguments, lb, out ipm);
            
        }
        protected virtual bool IsMethodToIgnore(Method callee)
        {
            bool res = callee.FullName.EndsWith("get_SpecSharp::FrameGuard");
            res = res || IsUnPackMethod(callee);
            res = res || IsPackMethod(callee);
            //res = res || CciHelper.IsConstructor(callee);
            //res = res || CciHelper.IsConstructor(Method);
            return res;
        }
        internal bool IsUnPackMethod(Method callee)
        {
            bool res = callee.Equals(PackMethod);
            res = res || callee.Name.Name.Equals("StartWritingAtTransitively");
            res = res || callee.Name.Name.Equals("StartWritingAtNop");
            return res;
        }
        internal bool IsPackMethod(Method callee)
        {
            bool res = callee.Equals(UnpackMethod);
            res = res || callee.Name.Name.Equals("EndWritingAtTransitively");
            res = res || callee.Name.Name.Equals("EndWritingAtNop");
            return res;
        }

        private Method PackMethod = SystemTypes.Guard.GetMethod(Identifier.For("StartWritingAtTransitively"));
        private Method UnpackMethod = SystemTypes.Guard.GetMethod(Identifier.For("EndWriting"));
        private void ConsistencyForCalls(Method callee, Label lb, Nodes receiverValues, 
            bool IsReceiverConsistent, Variable receiver, ExpressionList arguments)
        {
            // OJO!: I Have to do the same for Arguments
            if (callee.Name.Name.Equals("Push"))
            { }

            if (IsMethodToIgnore(callee))
                return;

            if (callee.IsPure)
                return;

            if (MethodEnsuresInvariant(callee))
            {
                // AssumeConsistent(receiverValues);
                AssumeConsistent(receiver);
            }
            else
            {
                AssumeNonConsistent(receiver);
            }
            if (arguments != null)
            {
                for (int i = 0; i < arguments.Count; i++)
                {
                    if (arguments[i] is Variable)
                    {
                        Variable argVar = (Variable)arguments[i];
                        if (argVar.Type.IsValueType || MethodEnsuresExposable(callee, callee.Parameters[i]))
                        {
                            AssumeConsistent(argVar);
                        }
                        else
                        {
                            AssumeNonConsistent(argVar);
                        }

                    }
                }
            }

            //else
            //{
            //    AssumeNonConsistent(receiverValues);
            //}
        }


        protected override void NonAnalyzableCallBeforeUpdatingPTG(Variable vr, Method callee, Variable receiver, ExpressionList arguments, Label lb, bool isPure, bool isReturnFresh, bool modifiesGlobal, bool readsGlobal, Set<Parameter> escapingParameters, Set<Parameter> capturedParameters, Set<Parameter> freshParameters)
        {
            base.NonAnalyzableCallBeforeUpdatingPTG(vr, callee, receiver, arguments, lb, isPure, isReturnFresh, modifiesGlobal, readsGlobal, escapingParameters, capturedParameters, freshParameters);
        }

        public virtual void ApplyExpose(Variable varToExpose, Label lb)
        {
            Set<VariableEffect> pathsToNode = ComputePathsFromVariableToHeapLocation(Values(varToExpose), MethodLabel);
            foreach (VariableEffect ve in pathsToNode)
            {
                if (ve.FieldChain.Count == 1)
                {

                    AssumeExposed(ve.Variable);

                }
            }
            

        }
        public virtual void ApplyUnExpose(Variable varToExpose, Label lb)
        {
            Set<VariableEffect> pathsToNode = ComputePathsFromVariableToHeapLocation(Values(varToExpose), MethodLabel);
            foreach (VariableEffect ve in pathsToNode)
            {
                if (ve.FieldChain.Count == 1)
                {
                    AssumeUnExposed(ve.Variable);
                    //                        ForgetField(ve.Variable, ve.FieldChain[0]);
                    AssumeConsistent(ve.Variable);
                }

            }
        }


        public bool MethodRequiresInvariant(Method m)
        {
            //if (ConsistentByDefault(m))
            //    return true;
            //bool res = true;
            //if (m.IsStatic || m.IsPrivate)
            //    res = false;
            //return res;
            bool res = m.IsStatic;
            res = res || MethodEnsuresExposable(m, m.ThisParameter);
            return res;
        }

        public bool MethodEnsuresInvariant(Method m)
        {
            //if (m.IsPure)
            //    return false;
            //if (ConsistentByDefault(m))
            //    return true;

            //if (CciHelper.IsConstructor(m))
            //{
             
            //}
            bool res = m.IsStatic;
            res = res || MethodEnsuresExposable(m, m.ThisParameter);
            return res;
        }
        public bool ConsistentByDefault(Method m)
        {
            if (m.ApplyDefaultContract)
                return true;

            if (CciHelper.IsConstructor(m))
            {
                return false;
            }
            return true;
        }
        public bool MethodRequiresExposable(Method m, Parameter p)
        {
            return ContractConsistencyInfo.RequiresExposable(m, p);
        }
        public bool MethodEnsuresExposable(Method m, Parameter p)
        {
            return ContractConsistencyInfo.EnsuresExposable(m,p);
        }

        

        public Set<VariableEffect> ComputePathsFromVariableToHeapLocation(Set<IPTAnalysisNode> ns, Label lb)
        {
            Set<VariableEffect> variableEffects = new Set<VariableEffect>();

            //if (WriteEffects.HasWriteEffects)
            //    Console.Out.WriteLine("Modifies:");

            // Traverse every write effect
            foreach (IPTAnalysisNode n in ns)
            {
                // Get the fields that are backward reachable from the modified field
                Set<List<Edge>> paths = PointsToGraph.DFSPathFrom(n, false, true, true);
                foreach (List<Edge> currentPath in paths)
                {
                    currentPath.Reverse();

                    IPTAnalysisNode rootNode;

                    if (currentPath.Count > 0)
                        rootNode = currentPath[0].Src;
                    else
                        rootNode = n;

                    Variable v = null;
                    
                    if (rootNode.IsVariableReference)
                    {
                        IVarRefNode vrNode = rootNode as IVarRefNode;
                        v = vrNode.ReferencedVariable;
                        if (!IsLocal(v))
                            continue;
                        //if (!(v is Parameter) && !v.Equals(PTGraph.GlobalScope))
                        //    continue;
                    }

                    if (rootNode.Equals(GNode.nGBL))
                    {
                        v = PTGraph.GlobalScope;
                    }
                    
                    /*
                    if (rootNode.IsParameterNode && ((PNode)rootNode).IsByValue)
                    {
                        bool fieldUpdate = !n.Field.Equals(PTGraph.asterisk);
                        foreach (Edge e in currentPath)
                            fieldUpdate = fieldUpdate || !e.Field.Equals(PTGraph.asterisk);
                        if (!fieldUpdate)
                            continue;
                    }
                    */
                    string nodeName = rootNode.Name;

                    if (v != null)
                    {
                        VariableEffect vEffect = new VariableEffect(v, lb);
                        foreach (Edge e in currentPath)
                        {
                            if (!e.Field.Equals(PTGraph.asterisk))
                            {
                                vEffect.AddField(e.Field);
                                // lastField = e.Field;
                            }
                        }
                        variableEffects.Add(vEffect);
                    }

                }
            }
            return variableEffects;
            
        }
        private bool HasInvariantField(IPTAnalysisNode n)
        {
            var x = new Nodes();
            x.Add(n);
            return HasInvariantField(x);
        }

        private bool HasInvariantField(Set<IPTAnalysisNode> ns)
        {
            bool res = false;
            Set<VariableEffect> pathsToNode = ComputePathsFromVariableToHeapLocation(ns, MethodLabel);
            res = HasInvariantField(pathsToNode);
            return res;
        }

        private bool HasInvariantField(Set<VariableEffect> pathsToNode)
        {
            bool res = false;
            foreach (VariableEffect p in pathsToNode)
            {
                res = HasInvariantField(p);
                if (res)
                    return true;
            }
            return res;
        }
        private bool HasInvariantField(VariableEffect p)
        {
            if (p.FieldChain.Count > 0)
            {
                Field f = p.FieldChain[p.FieldChain.Count - 1];
                if (f.IsRep || (!(f is PTExtraField) && f.Type.IsValueType))
                    return true;
            }
            return false;
        }

        private bool IsLocal(Variable v)
        {
            //bool res = v.GetHashCode() > 0;
            bool res = !(v is StackVariable);
            return res;
        }

        //public Set<VariableEffect> InconsistentPaths(Statement s)
        //{
        //    Label lb = new Label(s,Method);
        //    if (this.inconsistentExpr.ContainsKey(lb))
        //        return this.inconsistentExpr[lb];
        //    return new Set<VariableEffect>();
        //}
    }
    
    

    
    public class ConsistentyElementFactory : ElementFactory //ReentrancyElementFactory
    {
        public ConsistentyElementFactory(PointsToAnalysis pta) : base(pta) { }
        
        public override PointsToState NewElement(Method m)
        {
            return new ConsistencyState(m,this.pta);
        }
    }
    public class ObjectConsistencyInferer: PointsToInferer //ReentrancyInferer
    {
        internal INonNullInformation nonNullInfo;
        //internal INonNullState nonNullstateForCurrentBlock;

        public ObjectConsistencyInferer(TypeSystem t, PointsToAnalysis pta)
            : base(t, pta)
        {
        }
        internal override PointsToInstructionVisitor GetInstructionVisitor(PointsToInferer pti)
        {
            return new ObjectConsistencyInstructionVisitor ((ObjectConsistencyInferer)pti);
        }

        /// <summary>
        /// Compute the Dataflow analysis for the given method
        /// Returns true if the method is pure
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        /// 
        public bool ComputeExposureFor(Method method)
        {
            //return base.ComputeReentrancyFor(method);
            return base.ComputePointsToStateFor(method);
        }

        /// <summary>
        /// Return the results of the analysis on exit of the CFG
        /// </summary>
        public ConsistencyState ExposureState
        {
            get { return (ConsistencyState)exitState; }
        }

        protected override IDataFlowState VisitBlock(CfgBlock block, IDataFlowState stateOnEntry)
        {
            //if (IsInstrumentationCode(block))
            //    return stateOnEntry;
            return base.VisitBlock(block, stateOnEntry);
        }

        internal static bool IsInstrumentationCode(CfgBlock block)
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

        protected override void ComputeBeforeDataflow(Method m)
        {
            ObjectConsistencyAnalysis oea = (ObjectConsistencyAnalysis)this.pointsToStateAnalysys;
            nonNullInfo = oea.GetNonNullInfo(m);
            base.ComputeBeforeDataflow(m);
        }

        protected override IDataFlowState Merge(CfgBlock previous, CfgBlock joinPoint, IDataFlowState atMerge, IDataFlowState incoming, out bool resultDiffersFromPreviousMerge, out bool mergeIsPrecise)
        {
            if (nonNullInfo != null)
            {
                INonNullState nns = nonNullInfo.OnEdge(previous, joinPoint);
                if (nns == null && atMerge!=null)
                    incoming = null;
            }
            return base.Merge(previous, joinPoint, atMerge, incoming, out resultDiffersFromPreviousMerge, out mergeIsPrecise);
        }
        

    }

    /// <summary>
    /// Instruction visitor. Implement the transfer function of the dataflow analysis
    /// It is exactly the same as the points to analysis
    /// </summary>
    internal class ObjectConsistencyInstructionVisitor : PointsToInstructionVisitor
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="pta"></param>
        public ObjectConsistencyInstructionVisitor (PointsToInferer pta): base(pta)
        {
        }
        protected override object VisitCall(Variable dest, Variable receiver, Method callee, ExpressionList arguments, bool virtcall, Statement stat, object arg)
        {
           

            ConsistencyState exp = (ConsistencyState)arg;
            Label lb = new Label(stat, exp.Method);
            // Console.Out.WriteLine("Calling: {0}", callee.FullName);
            
            if (exp.IsUnPackMethod(callee))
            {
                Variable varToExpose = null;
                if (receiver != null)
                {
                    varToExpose = receiver;

                }
                else
                {
                    if (arguments != null && arguments.Count > 0 && arguments[0] is Variable)
                    {
                        varToExpose = (Variable)arguments[0];
                    }
                }
                if (varToExpose != null)
                {
                    exp.ApplyExpose(varToExpose,lb);
                    // 
                    if(dest!=null)
                        exp.pointsToGraph.CopyLocVar(dest, varToExpose);
                }
                return exp;
            }
            if (exp.IsPackMethod(callee))
            {
                Variable varToExpose = null;
                if (receiver != null)
                {
                    varToExpose = receiver;
                    
                }
                else
                {
                    if (arguments != null && arguments.Count > 0 && arguments[0] is Variable)
                    {
                        varToExpose = (Variable)arguments[0];
                    }
                }
                if (varToExpose != null)
                {
                    exp.ApplyUnExpose(varToExpose,lb);
                    
                }
                return exp;
            }
            return base.VisitCall(dest, receiver, callee, arguments, virtcall, stat, arg);
        }
        
        protected virtual void CheckExposure(ConsistencyState exp, Variable v)
        {
            
        }
        
        //  Guard guard = this.SpecSharp::FrameGuard.StartWritingAtTransitively(typeof(IntStack));
        protected override object VisitBranch(Variable cond, Block target, Statement stat, object arg)
        {
            /*
            if (cond == null)
                return arg;

            Exposure exp = (Exposure)arg;
            Exposure trueState, falseState;

            exp.RefineBranchInformation(cond, out trueState, out falseState);

            if (trueState != null)
            {
                this.pta.PushState(this.pta.currBlock, this.pta.currBlock.TrueContinuation, trueState);
            }
            if (falseState != null)
            {
                this.pta.PushState(this.pta.currBlock, this.pta.currBlock.FalseContinuation, falseState);
            }
            return null;
            */
            return base.VisitBranch(cond, target, stat, arg);
        }
  
    }


}
