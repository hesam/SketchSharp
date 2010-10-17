//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;

#if CCINamespace
namespace Microsoft.Cci{
  using System.Compiler.Contracts;
  using Cci = Microsoft.Cci;
#else
namespace System.Compiler{
  using Microsoft.Contracts;
  using Cci = System.Compiler;
#endif
  using AbstractValue = MathematicalLattice.Element;

  class ExposureException:Exception {
    public ExposureException() {
    }
    public ExposureException(string msg):base(msg){
    }
  }

  /// <summary>
  /// Exposure states for variables and Objects.
  /// </summary>
  internal class ExposureState:IDataFlowState{

    internal sealed class Lattice : MathematicalLattice {

      /// <summary>
      /// Ordering:
      /// 
      ///              Top
      ///             /    \
      ///            /      \
      ///           /        \
      ///       IsExposed IsNotExposed
      ///           \        /
      ///            \      /
      ///             \    /
      ///             Bottom
      /// 
      /// IsExposed lt Top
      /// IsNotExposed lt Top
      /// Bottom lt IsExposed
      /// Bottom lt IsNotExposed
      /// 
      /// </summary>
      public class AVal : AbstractValue {

        public TypeNode lowerBound;
        public TypeNode upperBound;

        private AVal(){}

        public readonly static AVal Bottom = new AVal();
        public readonly static AVal Top = new AVal();
        public readonly static AVal IsExposed = new AVal();
        public readonly static AVal IsNotExposed = new AVal();

        internal AVal(TypeNode lb, TypeNode ub){
          this.lowerBound = lb;
          this.upperBound = ub;
        }

        public static AVal Join(AVal a, AVal b) {
          AVal result = null;
          if (a == Top || b == Top) result = Top;
          else if (a == Bottom) result = b;
          else if (b == Bottom) result = a;
          else if (a.lowerBound != null && b.lowerBound != null && a.lowerBound == b.lowerBound) result = a;
          else if (a == b) result = a; // or b...
          else result = Top;
          return result;
        }

        public static AVal Meet(AVal a, AVal b) {
          AVal result = null;
          if (a == Bottom || b == Bottom) result = Bottom;
          else if (a == Top) result = b;
          else if (b == Top) result = a;
          else if (a == b) result = a; // or b...
          else result = Bottom;
          return result;
        }

        public override string ToString() {
          if (this == Bottom)
            return "infeasible";
          else if (this == Top)
            return "don't know";
          else if (this.lowerBound != null && this.upperBound != null){
            return "[" + this.lowerBound.Name + ".." + this.upperBound.Name + "]";
          }
          else{
//            Debug.Assert(false,"supposed to be exhaustive");
            return "[?..?]";
          }
        }

        public override bool IsBottom {
          get { return this == AVal.Bottom; }
        }

        public override bool IsTop {
          get { return this == AVal.Top; }
        }


      }


      protected override bool AtMost(AbstractValue a, AbstractValue b) {
        AVal av = (AVal)a;
        AVal bv = (AVal)b;
        return AVal.Join(av, bv) == bv;
      }

      public override AbstractValue Bottom {
        get {
          return AVal.Bottom;
        }
      }

      public override AbstractValue Top {
        get {
          return AVal.Top;
        }
      }

      public override AbstractValue NontrivialJoin(AbstractValue a, AbstractValue b) {
        return AVal.Join((AVal)a, (AVal)b);
      }

      public override MathematicalLattice.Element NontrivialMeet(MathematicalLattice.Element a, MathematicalLattice.Element b) {
        return AVal.Meet((AVal)a, (AVal)b);
      }


      private Lattice() {}

      public static Lattice It = new Lattice();

    }


    private static Identifier FalseValue = Identifier.For("FalseValue");
    private static Identifier TrueValue = Identifier.For("TrueValue");

    private static Identifier FrameFor = Identifier.For("FrameFor");
    internal static Identifier EqIsExposedId = Identifier.For("EqIsExposed");
    internal static Identifier EqIsExposableId = Identifier.For("EqIsExposable");

    private static Identifier LogicalNegId = Identifier.For("Not");
    private static Identifier NeNullId = Identifier.For("NeNull");

    private static Identifier StaticTypeOf = Identifier.For("StaticTypeOf");

    private IEGraph egraph;
    private TypeSystem typeSystem;
    public TypeNode currentException;

    public ExposureState(ExposureState old){
      this.typeSystem = old.typeSystem;
      this.egraph = (IEGraph)old.egraph.Clone();  
      this.currentException = old.currentException;
    }

    public ExposureState(TypeSystem t) {
      this.typeSystem = t;
      this.egraph = new EGraph(Lattice.It);
      this.currentException = null;
    }

    private ExposureState(IEGraph egraph, TypeNode currentException, TypeSystem t) {
      this.typeSystem = t;
      this.egraph = egraph;
      this.currentException = currentException;
    }

    public object Clone() {
      return new ExposureState(this);
    }

    internal Lattice.AVal GetAVal(Variable v) {
      ISymValue sv = this.egraph[v];
      return (Lattice.AVal)this.egraph[sv];
    }

    internal TypeNode LowerBoundOfObjectPointedToByFrame(Variable guardVariable){
      ISymValue guard = this.egraph[guardVariable];
      ISymValue guardedObject = this.egraph[FrameFor, guard];
      Lattice.AVal invLevel = (Lattice.AVal)this.egraph[guardedObject];
      return invLevel.lowerBound;
    }

    /// <summary>
    /// Set v to a new value that is abstracted by av
    /// </summary>
    /// <param name="v"></param>
    /// <param name="av"></param>
    private void AssignAVal(Variable v, Lattice.AVal av) {
      ISymValue sv = this.egraph.FreshSymbol();
      this.egraph[v] = sv;
      this.egraph[sv] = av;
    }

    
    private bool IsExposed(ISymValue sv) {
      return ((Lattice.AVal)this.egraph[sv]) == Lattice.AVal.IsExposed;
    }
    
    public bool IsExposed(Variable v) {
      return GetAVal(v) == Lattice.AVal.IsExposed;
    }

    public bool IsNotExposed(Variable v){
      Lattice.AVal valueOfV = GetAVal(v);
      return valueOfV == Lattice.AVal.IsNotExposed;
    }
    
    /// <summary>
    /// Adds assumption that sv is exposed
    /// </summary>
    // BUGBUG: Ask Manuel if this is okay
    public void AssumeExposed(ISymValue sv) {
      this.egraph[sv] = Lattice.AVal.IsExposed;
    }
    /// <summary>
    /// Adds assumption that sv is not exposed
    /// </summary>
    public void AssumeNotExposed(ISymValue sv) {
      this.egraph[sv] = Lattice.AVal.IsNotExposed;
    }

    public void AssignExposed(Variable v) {
      AssignAVal(v, Lattice.AVal.IsExposed);
    }

    public void AssignNotExposed(Variable v) {
      AssignAVal(v, Lattice.AVal.IsNotExposed);
    }

    private ISymValue False{
      get{ return this.egraph[FalseValue]; } 
    }
    public void SetToFalse(Variable v) {
      this.egraph[v] = this.False;
    }
    public bool IsFalse(Variable v){
      return this.egraph[v] == this.False;
    }

    public void AssignNonPointer(Variable v) {
      this.egraph.Eliminate(v);
    }

    public void CopyVariable(Variable source, Variable dest) {
      this.egraph[dest] = this.egraph[source];
    }

    public void AssignFrameFor(Variable dest, Variable source, TypeNode t) {
      ISymValue guard = this.egraph.FreshSymbol();
      ISymValue guardedObject = this.egraph[source];
      this.egraph[dest] = guard;
      this.egraph[FrameFor, guard] = guardedObject;
      ISymValue fresh = this.egraph.FreshSymbol();
      this.egraph[fresh] = new Lattice.AVal(t,t);
      this.egraph[StaticTypeOf, guard] = fresh;
    }
    public void AssignFrameForExposed(Variable guardVariable){
      ISymValue guard = this.egraph[guardVariable];
      this.AssignFrameForExposed(guard);
    }
    public void AssignFrameForExposed(ISymValue guard){
      ISymValue guardedObject = this.egraph[FrameFor, guard];
      ISymValue dummy = this.egraph[StaticTypeOf,guard];
      Lattice.AVal guardsType = (Lattice.AVal)this.egraph[dummy];
      this.egraph[guardedObject] = new Lattice.AVal(guardsType.lowerBound.BaseType,guardsType.upperBound.BaseType);
    }
    public void AssignFrameForExposable(Variable guardVariable){
      ISymValue guard = this.egraph[guardVariable];
      this.AssignFrameForExposable(guard);
    }
    public void AssignFrameForExposable(ISymValue guard){
      ISymValue guardedObject = this.egraph[FrameFor, guard];
      ISymValue guardTypeObject = this.egraph[StaticTypeOf,guard];
      Lattice.AVal guardsType = (Lattice.AVal)this.egraph[guardTypeObject];
      this.egraph[guardedObject] = new Lattice.AVal(guardsType.lowerBound,guardsType.upperBound);
    }
    public bool IsFrameExposable(Variable guardVariable){
      ISymValue guard = this.egraph[guardVariable];
      ISymValue guardedObject = this.egraph[FrameFor, guard];
      ISymValue guardTypeObject = this.egraph[StaticTypeOf,guard];
      Lattice.AVal guardsType = (Lattice.AVal)this.egraph[guardTypeObject];
      Lattice.AVal guardedObjectsType = (Lattice.AVal)this.egraph[guardedObject];
      return guardsType.lowerBound == guardedObjectsType.lowerBound;
    }
    public void AssignFrameForNotExposed(Variable guardVariable){
      ISymValue guard = this.egraph[guardVariable];
      ISymValue guardTypeObject = this.egraph[StaticTypeOf,guard];
      Lattice.AVal guardsType = (Lattice.AVal)this.egraph[guardTypeObject];
      ISymValue guardedObject = this.egraph[FrameFor, guard];
      this.egraph[guardedObject] = guardsType;
    }
    public void AssignEqIsExposed(Variable dest, Variable operand) {
      ISymValue opval = this.egraph[operand];
      ISymValue sv = this.egraph.FreshSymbol();
      this.egraph[dest] = sv; // ?? Ask Manuel: Should it be the sv' that dest maps to that sv should be mapped to here?
      this.egraph[EqIsExposedId, opval] = sv;
    }
    public void AssignFunctionLink(Identifier func, Variable dest, Variable operand) {
      ISymValue opval = this.egraph[operand];
      ISymValue sv = this.egraph.FreshSymbol();
      this.egraph[dest] = sv; // ?? Ask Manuel: Should it be the sv' that dest maps to that sv should be mapped to here?
      this.egraph[func, opval] = sv;
    }


    /// <summary>
    /// Assume all accessible locations in the heap are modified.
    /// </summary>
    public void HavocHeap() {
    }

    /// <summary>
    /// Returns null, if result of Join is the same as atMerge.
    /// </summary>
    public static ExposureState Join(ExposureState atMerge, ExposureState incoming, CfgBlock joinPoint) {

      bool unchanged;
      IEGraph merged = atMerge.egraph.Join(incoming.egraph, joinPoint, out unchanged);

      TypeNode currentException = (atMerge.currentException != null)?
        ((incoming.currentException != null)? CciHelper.LeastCommonAncestor(atMerge.currentException, incoming.currentException) : null) : null;

      if (atMerge.currentException != currentException || !unchanged) {
        return new ExposureState(merged, currentException, atMerge.typeSystem);
      }
      return null;
    }

    public void Dump() {
      this.egraph.Dump(Console.Out);
    }


    public void RefineBranchInformation(Variable cond, out ExposureState trueState, out ExposureState falseState) {

      ISymValue cv = this.egraph[cond];

      trueState = new ExposureState(this);
      falseState = new ExposureState(this);

      AssumeTrue(cv, ref trueState);
      AssumeFalse(cv, ref falseState);
    }


    /// <summary>
    /// Refines the given state according to the knowledge stored in the egraph about sv
    /// 
    /// In addition, the state can be null when the knowledge is inconsistent.
    /// </summary>
    /// <param name="cv">symbolic value we assume to be false</param>
    private static void AssumeFalse(ISymValue cv, ref ExposureState state) {
      if (state == null) return;

      foreach(EGraphTerm t in state.egraph.EqTerms(cv)) {
        if (t.Function == ExposureState.EqIsExposedId) {
          // EqIsExposed(op) == false, therefore op is *not* exposed
          ISymValue op = t.Args[0];
          
//          state.AssignFrameForNotExposed(op);
          ISymValue guardedObject = state.egraph[FrameFor, op];
          state.egraph[guardedObject] = Lattice.AVal.Top; // BUGBUG?? If it isn't exposed at this frame, then what is it?
        }
      }
    }

    /// <summary>
    /// Refines the given state according to the knowledge stored in the egraph about sv
    /// 
    /// In addition, the state can be null when the knowledge is inconsistent.
    /// </summary>
    /// <param name="cv">symbolic value we assume to be non-null (true)</param>
    /// <param name="state">state if sv is non-null (true)</param>
    private static void AssumeTrue(ISymValue cv, ref ExposureState state) {

      if (state == null) return;

      foreach(EGraphTerm t in state.egraph.EqTerms(cv)){
        ISymValue op = t.Args[0];
        if (t.Function == ExposureState.EqIsExposedId){
          // EqIsExposed(op) == true, therefore op *is* exposed
          state.AssignFrameForExposed(op);
        }else if (t.Function == ExposureState.EqIsExposableId){
          state.AssignFrameForExposable(op);
        }
      }
    }
  }

  /// <summary>
  /// The main class for NonNull checking.
  /// </summary>
  internal class ExposureChecker:ForwardDataFlowAnalysis{
    /// <summary>
    /// Current Exposure checking visitor
    /// </summary>
    ExposureInstructionVisitor iVisitor;

    /// <summary>
    /// Current block being analyzed.
    /// </summary>
    internal CfgBlock currBlock;
    internal TypeSystem typeSystem;
    internal Method currentMethod;

    /// <summary>
    /// Entry point to check a method.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="method"></param>
    public static void Check(TypeSystem t, Method method, Analyzer analyzer) {
      if(method==null) 
        return;
      if (method.HasCompilerGeneratedSignature)
        return; // REVIEW: this means we don't check default ctors, among other things.
      ExposureChecker checker= new ExposureChecker(t,method);
      Analyzer.WriteLine("");
      ControlFlowGraph cfg=analyzer.GetCFG(method);
      if(cfg!=null)
      {
        checker.Run( cfg, new ExposureState(t));
    }
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="t"></param>
    /// <param name="method"></param>
    protected ExposureChecker(TypeSystem t,Method method) {
      typeSystem=t;
      currentMethod=method;
      iVisitor=new ExposureInstructionVisitor(this);
    }
  
    /// <summary>
    /// Merge the two states for current block.
    /// </summary>
    /// <param name="previous"></param>
    /// <param name="joinPoint"></param>
    /// <param name="atMerge"></param>
    /// <param name="incoming"></param>
    /// <param name="resultDiffersFromPreviousMerge"></param>
    /// <param name="mergeIsPrecise"></param>
    /// <returns></returns>
    protected override IDataFlowState Merge(CfgBlock previous, CfgBlock joinPoint, IDataFlowState atMerge, IDataFlowState incoming, out bool resultDiffersFromPreviousMerge, out bool mergeIsPrecise) {

      mergeIsPrecise = false;
    
      // No new states;
      if(incoming==null) {
        resultDiffersFromPreviousMerge = false;
        return atMerge;
      }

      // Initialize states
      if(atMerge==null){
        resultDiffersFromPreviousMerge = true;
        return incoming;
      }

      if (Analyzer.Debug) {
        Console.WriteLine("Merge at Block {0}-----------------", (joinPoint).UniqueKey);
        Console.WriteLine("  State at merge");
        atMerge.Dump();
        Console.WriteLine("  Incoming");
        incoming.Dump();
      }

      // Merge the two.
      
      ExposureState newState = ExposureState.Join((ExposureState)atMerge, (ExposureState)incoming, joinPoint);

      if (newState != null) {
        if (Analyzer.Debug) {
          Console.WriteLine("\n  Merged State");
          newState.Dump();
        }
        resultDiffersFromPreviousMerge = true;
        return newState;
      }

      if (Analyzer.Debug) {
        Console.WriteLine("Merged state same as old.");
      }
      resultDiffersFromPreviousMerge = false;
      return atMerge;
    }

    /// <summary>
    /// Implementation of visit Block. It is called from run.
    /// 
    /// It calls VisitStatement.
    /// </summary>
    /// <param name="block"></param>
    /// <param name="stateOnEntry"></param>
    /// <returns></returns>
    protected override IDataFlowState VisitBlock(CfgBlock block, IDataFlowState stateOnEntry) {
      Debug.Assert(block!=null);

      currBlock=block;

      Analyzer.Write("---------block: "+block.UniqueId+";");
      Analyzer.Write("   Exit:");
      foreach (CfgBlock b in block.NormalSuccessors)
        Analyzer.Write(b.UniqueId+";");
      if (block.UniqueSuccessor!=null)
        Analyzer.Write("   FallThrough: "+block.UniqueSuccessor+";");
      if (block.ExceptionHandler!=null)
        Analyzer.Write("   ExHandler: "+block.ExceptionHandler.UniqueId+";");
      Analyzer.WriteLine("");

      ExposureState newState;
      if(stateOnEntry==null)
        newState=new ExposureState(typeSystem);
      else
        newState=new ExposureState((ExposureState)stateOnEntry);
//      if (block.ExceptionHandler!=null)
//        this.PushExceptionState(block,newState);
      return base.VisitBlock (block, newState);
    }

    /// <summary>
    /// It visits an individual statement. It is called from VisitBlock.
    /// 
    /// It calls NonNullInstructionVisitor
    /// </summary>
    /// <param name="block"></param>
    /// <param name="statement"></param>
    /// <param name="dfstate"></param>
    /// <returns></returns>
    protected override IDataFlowState VisitStatement(CfgBlock block, Statement statement, IDataFlowState dfstate) {
      // For debug purpose
      if (Analyzer.Debug) {
        try{
          Analyzer.WriteLine("\n:::"+new SampleInstructionVisitor().Visit(statement,null)+"   :::   " +statement.SourceContext.SourceText);
        }catch(Exception e){
          Analyzer.WriteLine("Print error: "+statement+": "+e.Message);
        }
      }

      IDataFlowState result=null;
      try{
        result =(IDataFlowState)(iVisitor.Visit(statement,dfstate));
      }catch(Exception e){
        typeSystem.HandleError(statement,Cci.Error.InternalCompilerError,":NonNull:"+e.Message);
		    Console.WriteLine(e.StackTrace);
      }
      if (result != null && Analyzer.Debug){
        result.Dump();
      }
      return result;
    }

    /// <summary>
    /// It split exceptions for current handler and the next chained handler.
    /// 
    /// It will:
    /// 
    ///   If the exception is completely intercepted by current handler, the
    ///   exception will be consumed.
    ///   
    ///   If the exception caught but not completely, both current handler and 
    ///   the next handler will take the states.
    ///   
    ///   If the exception is irrelevant to current caught, the next handler 
    ///   will take over the state. Current handler is then bypassed.
    /// </summary>
    /// <param name="handler"></param>
    /// <param name="currentHandlerState"></param>
    /// <param name="nextHandlerState"></param>
    protected override void SplitExceptions(CfgBlock handler, ref IDataFlowState currentHandlerState, out IDataFlowState nextHandlerState) {

      Debug.Assert(currentHandlerState!=null,"Internal error in NonNull Analysis");

      ExposureState state = (ExposureState)currentHandlerState;

      if(handler==null || handler.Length==0){
        nextHandlerState=null;
        return;
      }

      if(handler[0] is Unwind){
        nextHandlerState=null;
        currentHandlerState=null;
        return;
      }

      Debug.Assert(handler[0] is Catch, "Exception Handler does not starts with Catch");
      
      Debug.Assert(state.currentException!=null,"No current exception to handle");
      
      Catch c=(Catch)handler[0];

      if(handler.ExceptionHandler!=null && 
        !state.currentException.IsAssignableTo(c.Type)) {
        nextHandlerState = state;;
      }
      else {
        nextHandlerState=null;
      }

      // Compute what trickles through to the next handler
      //  and what sticks to this handler.
      if(state.currentException.IsAssignableTo(c.Type)) {
        // everything sticks 
        nextHandlerState = null;
      }
      else if (c.Type.IsAssignableTo(state.currentException)) {
        // c sticks, rest goes to next handler
        nextHandlerState = state;

        // copy state to modify the currentException
        state = new ExposureState(state);
        state.currentException = c.Type;
        currentHandlerState = state;
      }else {
        // nothing stick all goes to next handler
        nextHandlerState = state;
        currentHandlerState = null;
      }
      return;
    }
  }

  /// <summary>
  /// Visit each instruction, check whether the modification is authorized.
  /// </summary>
  internal class ExposureInstructionVisitor:InstructionVisitor {


    /// <summary>
    /// Current ExposureChecker
    /// </summary>
    private ExposureChecker ExposureChecker;

    private TypeSystem ts;

    /// <summary>
    /// Used to avoid repeated error/warning report for the same Node.
    /// 
    /// Important: This is absolutely necessary, since we are doing fix-point
    /// Analysis. Bypass this sometimes means hundred's of the same error messages.
    /// </summary>
    private Hashtable reportedErrors;

    /// <summary>
    /// Error handler. Only file an error if it has not been filed yet. 
    /// 
    /// Requires: the node has proper source context. Otherwise, it does not help.
    /// </summary>
    /// <param name="stat"></param>
    /// <param name="node"></param>
    /// <param name="error"></param>
    /// <param name="m"></param>
    private void HandleError(Statement stat, Node node, Error error, params string[] m){

      Node offendingNode = node;
      if (offendingNode.SourceContext.Document == null)
      {
        offendingNode = stat;
      }
      if(reportedErrors.Contains(offendingNode.SourceContext))
        return;
      //Analyzer.WriteLine("!!! " + error+ " : "+node);
      if(m==null) 
        ts.HandleError(offendingNode,error);
      else
        ts.HandleError(offendingNode,error,m);
      reportedErrors.Add(offendingNode.SourceContext,null);
    }
    private void HandleError(Statement stat, Node node, Error error, TypeNode t){
      Node offendingNode = node;
      if (offendingNode.SourceContext.Document == null) 
      {
        offendingNode = stat;
      }
      Debug.Assert(t!=null);
      if(reportedErrors.Contains(offendingNode.SourceContext))
        return;
      //Analyzer.WriteLine("!!! " + error+ " : "+node);
      ts.HandleError(offendingNode,error,ts.GetTypeName(t));
      reportedErrors.Add(offendingNode.SourceContext,null);
    }


    /// <summary>
    /// For the possible receiver v, check if it is nonnull. if no, file an proper
    /// error/warning.
    /// </summary>
    private void CheckReceiver(Statement stat, Variable v, ExposureState estate)
    {
      Node offendingNode = v;
      if (v == null) return;

      if(estate.IsNotExposed(v))
      {
        HandleError(stat, offendingNode, Error.WritingPackedObject, v.Name.Name);
        //estate.AssignExposed(v);
      }
      else if(!estate.IsExposed(v))
      {
        HandleError(stat, offendingNode, Error.WritingPackedObject, v.Name.Name);
        //estate.AssumeNonNull(v);
      }
    }

    public ExposureInstructionVisitor(ExposureChecker c)
    {
      ExposureChecker=c;
      ts=c.typeSystem;
      reportedErrors=new Hashtable();
    }


    /// <summary>
    /// A lot of the pointers are not supported.
    /// </summary>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object DefaultVisit(Statement stat, object arg) {
      throw new NotImplementedException("Instruction "+stat.NodeType.ToString()+" not implemented yet");
    }

    /// <summary>
    /// Method entry. Need to add This pointer. 
    /// 
    /// Does not have to deal with parameters.
    /// </summary>
    /// <param name="method"></param>
    /// <param name="parameters"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitMethodEntry(Method method, IEnumerable parameters, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;

      foreach (Parameter p in parameters) {
        // TODO: look at attributes or somewhere else (??) to see if parameter is exposed or not.
//        estate.AssignAccordingToType(p, p.Type);
        if (p == null) continue;
      }
      if (!method.IsStatic){
        // TODO: decide whether "this" is exposed or not
//        estate.AssignNonNull(CciHelper.GetThis(method));
      }
      return arg;
    }

    /// <summary>
    /// Copy the source to dest.
    /// 
    /// If source is nonnull, no problem.
    /// If source is null and dest is nonnulltype, Error
    /// If source is possible null and dest is nonnulltype, warning.
    /// Else, nothing.
    /// 
    /// Need to maintain proper heap transformation.
    /// </summary>
    /// <param name="dest"></param>
    /// <param name="source"></param>
    /// <param name="stat"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    protected override object VisitCopy(Variable dest, Variable source, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;

      estate.CopyVariable(source, dest);

      return arg;
    }

    protected override object VisitLoadFunction(Variable dest, Variable source, Method method, Statement stat, object arg)
    {
      ExposureState estate=(ExposureState)arg;

      if (method.IsVirtual) 
      {
        // Check for Receiver non-nullness
        CheckReceiver(stat,source,estate);
      }

      return arg;

    }

    protected override object VisitLoadNull(Variable dest, Literal source, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;

      return arg;
    }


    protected override object VisitLoadConstant(Variable dest, Literal source, Statement stat, object arg) 
    {
      ExposureState estate=(ExposureState)arg;

      if (source == Literal.False){
        estate.SetToFalse(dest);
      }

      return arg;
    }


    /// <summary>
    /// Note: casts don't require a non-null argument. null value casts always succeed.
    /// </summary>
    protected override object VisitCastClass(Variable dest, TypeNode type, Variable source, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;

      // acts like a copy retaining null status
      estate.CopyVariable(source, dest);
      return arg;
    }

    private Method AssumeMethod = SystemTypes.AssertHelpers.GetMethod(Identifier.For("Assume"),SystemTypes.Boolean);
    private Method AssertMethod = SystemTypes.AssertHelpers.GetMethod(Identifier.For("Assert"),SystemTypes.Boolean);
    private Method IsNonNullMethod = SystemTypes.NonNullType.GetMethod(Identifier.For("IsNonNull"),SystemTypes.Object);
    private Method IsNonNullImplicitMethod = SystemTypes.NonNullType.GetMethod(Identifier.For("IsNonNullImplicit"),SystemTypes.Object);
    private Method AssertNotNullMethod = SystemTypes.NonNullType.GetMethod(Identifier.For("AssertNotNull"),SystemTypes.UIntPtr);
    private Method AssertNotNullImplicitMethod = SystemTypes.NonNullType.GetMethod(Identifier.For("AssertNotNullImplicit"),SystemTypes.UIntPtr);
    private Method GetTypeFromHandleMethod = (Method)SystemTypes.Type.GetMembersNamed(Identifier.For("GetTypeFromHandle"))[0];

    private Method UnpackMethod = SystemTypes.Guard.GetMethod(Identifier.For("StartWritingTransitively"));
    private Method PackMethod = SystemTypes.Guard.GetMethod(Identifier.For("EndWritingTransitively"));
    private Method IsExposedMethod = SystemTypes.Guard.GetMethod(Identifier.For("get_IsExposed"));
    private Method IsExposableMethod = SystemTypes.Guard.GetMethod(Identifier.For("get_IsExposable"));

    protected override object VisitCall(Variable dest, Variable receiver, Method callee, ExpressionList arguments, bool virtcall, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;

      if (callee.CciKind == Cci.CciMemberKind.FrameGuardGetter){
        // associate dest with receiver, because unpack is going to happen with dest as receiver
        estate.AssignFrameFor(dest,receiver,callee.DeclaringType); // receiver could be a subtype of the type that the frame guard is for
      }else if (callee == UnpackMethod){
        if(estate.IsFrameExposable(receiver)) {
          // BUGBUG: Using CopyVariable encodes the assumption that StartWritingTransitively returns itself!!! It may not!
          estate.CopyVariable(receiver,dest);
          estate.AssignFrameForExposed(dest);
        }else{
          TypeNode t = estate.LowerBoundOfObjectPointedToByFrame(receiver);
          if (t == null){ // BUGBUG: is this the same as it being Top?
            HandleError(stat, stat, Error.DontKnowIfCanExposeObject);
          }else{
            HandleError(stat, stat, Error.ExposingExposedObject);
          }
          return null;
        }
      }else if (callee == PackMethod){
        estate.AssignFrameForNotExposed(receiver);
      }else if (callee == IsExposableMethod){
        estate.AssignFunctionLink(ExposureState.EqIsExposableId,dest,receiver);
      }else if (callee == IsExposedMethod){
        estate.AssignEqIsExposed(dest,receiver);
      }else if (callee == AssertMethod){
        Variable v = arguments[0] as Variable;
        if (v != null && estate.IsFalse(v))
          return null;
      }

      // Push possible exceptions to handlers.
      for(int i=0;i<callee.Contract.Ensures.Count;i++){
        EnsuresExceptional e=callee.Contract.Ensures[i] as EnsuresExceptional;
        if(e!=null){
          ExposureState newnn=new ExposureState(estate);
          newnn.currentException=e.Type;
          ExposureChecker.PushExceptionState(ExposureChecker.currBlock,newnn);
        }
      }

      return arg;
    }

    protected override object VisitConstrainedCall(Variable dest, Variable receiver, Method callee, ExpressionList arguments, TypeNode constraint, Statement stat, object arg) {
      Reference rtype = receiver.Type as Reference;
      if (rtype != null && rtype.ElementType != null && !rtype.ElementType.IsValueType) {
        // instance could be a reference type that could be null.

        // BUGBUG: when we track address of, we need to check here that target is indeed non-null
      }
      return VisitCall(dest, receiver, callee, arguments, true, stat, arg);
    }

    protected override object VisitLoadField(Variable dest, Variable source, Field field, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;

      // Check the receiver here only if one needs to be unpacked for read access
      //CheckReceiver(stat,source,estate);

      return arg;
    }

    protected override object VisitStoreField(Variable dest, Field field, Variable source, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;

      // static Fields
      if(field.IsStatic){
      }

      // BUGBUG!!
      // It seems that it would be better to start off ctors with the method's "this" object
      // in the Exposed state, but I'm not sure how to do that.
      This t = null;
      ThisBinding tb = dest as ThisBinding;
      if (tb != null){
        t = tb.BoundThis;
      }else{
        t = dest as This;
      }
      if (t != null &&
        this.ExposureChecker.currentMethod.NodeType == NodeType.InstanceInitializer
        && this.ExposureChecker.currentMethod.ThisParameter == t){
        ; // skip
      }else{
        ExposureState.Lattice.AVal valueOfdest = estate.GetAVal(dest);
        if (valueOfdest.lowerBound == null || valueOfdest.upperBound == null){
          HandleError(stat, stat, Error.WritingPackedObject, dest.Name.Name);
          return arg;
        }
        if (valueOfdest.lowerBound.IsAssignableTo(field.DeclaringType)){
          HandleError(stat, stat, Error.WritingPackedObject, dest.Name.Name);
          return arg;
        }
      }

      return arg;
    }


    protected override object VisitReturn(Variable var, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;

      // TODO: see if returned value is supposed to be exposed or not and then what do we know about it?
      return arg;
    }
  
    protected override object VisitUnwind(Statement stat, object arg) {
      return arg;
    }
    protected override object VisitNop(Statement stat, object arg) {
      return arg;
    }

    protected override object VisitBranch(Variable cond, Block target, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;

      if(cond==null)
        return arg;

      ExposureState trueState, falseState;

      estate.RefineBranchInformation(cond, out trueState, out falseState);

      if ( trueState != null ) {
        ExposureChecker.PushState(ExposureChecker.currBlock, ExposureChecker.currBlock.TrueContinuation, trueState);
      }
      if ( falseState != null ) {
        ExposureChecker.PushState(ExposureChecker.currBlock, ExposureChecker.currBlock.FalseContinuation, falseState);
      }
      return null;
    }

    protected override object VisitBinaryOperator(NodeType op, Variable dest, Variable operand1, Variable operand2, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg; 

//      estate.AssignBinary(dest, op, operand1, operand2);

      return arg;
    }

    protected override object VisitUnaryOperator(NodeType op, Variable dest, Variable operand, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg; 
      return arg;
    }
    protected override object VisitSizeOf(Variable dest, TypeNode value_type, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg; 
      estate.AssignNonPointer(dest);
      return arg;
    }

    protected override object VisitBox(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;
      return arg;
    }

    protected override object VisitUnbox(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;
      return arg;
    }

    protected override object VisitUnboxAny(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;
      return arg;
    }

    protected override object VisitIsInstance(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg; 
      return arg;
    }

    protected override object VisitNewObject(Variable dest, TypeNode type, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg; 
      return arg;
    }
    protected override object VisitNewArray(Variable dest, TypeNode type, Variable size, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg; 
      return arg;
    }

    protected override object VisitLoadElement(Variable dest, Variable source, Variable index, TypeNode elementType, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;
      Indexer indexer = (Indexer)((AssignmentStatement)stat).Source;
      return arg;
    }
    protected override object VisitLoadElementAddress(Variable dest, Variable array, Variable index, TypeNode elementType, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;
      return arg;
    }

    protected override object VisitLoadFieldAddress(Variable dest, Variable source, Field field, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;
      CheckReceiver(stat,source,estate);
      return arg;
    }

    /// <summary>
    /// Perform 2 checks
    /// 1) array is non-null
    /// 2) if array element type is non-null type, then the new value written must be too.
    /// </summary>
    protected override object VisitStoreElement(Variable dest, Variable index, Variable source, TypeNode elementType, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;
      Indexer indexer = (Indexer)((AssignmentStatement)stat).Target;
      return arg;
    }

    protected override object VisitCatch(Variable dest, TypeNode type, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;
      return arg;
    }

    protected override object VisitThrow(Variable var, Statement stat, object arg) {

      return null; // BUGBUG?? Is this the right thing to do? DefAssign does this, but NonNull does what is below here.

      //ExposureState estate=(ExposureState)arg;

      //estate.currentException=var.Type;
      //if(ExposureChecker.currBlock.ExceptionHandler==null)
      //  return arg;

      //ExposureChecker.PushExceptionState(ExposureChecker.currBlock,estate);
      //return null;
    }

    protected override object VisitArgumentList(Variable dest, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;
      return arg;
    }

    protected override object VisitBreak(Statement stat, object arg) {
      return arg;
    }

    protected override object VisitFilter(Variable dest, Statement stat, object arg) {
      return arg;
    }

    protected override object VisitLoadToken(Variable dest, object token, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;
      return arg;
    }

    protected override object VisitSwitch(Variable selector, BlockList targets, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;
      foreach (CfgBlock target in ExposureChecker.currBlock.NormalSuccessors)
        ExposureChecker.PushState(ExposureChecker.currBlock,target,estate);
      return null;
    }

    /// <summary>
    /// Shouldn't reach this point. The error is handled by the definite assignment analysis. So
    /// here we treat it as assume(false)
    /// </summary>
    protected override object VisitSwitchCaseBottom(Statement stat, object arg) {
      return null;
    }

    protected override object VisitRethrow(Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;
      ExposureChecker.PushExceptionState(ExposureChecker.currBlock,estate);
      return null;
    }
    protected override object VisitEndFilter(Variable code, Statement stat, object arg) {
      return arg;
    }

    //----------------------------------------------
    // for unmanaged pointer manipulations.
    protected override object VisitLoadAddress(Variable dest, Variable source, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;
      return arg;
    }
    protected override object VisitStoreIndirect(Variable pointer, Variable source, TypeNode targetType, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;

      // no need to check pointer, since managed code guarantees them not to be null.


      return arg;
    }


    protected override object VisitLoadIndirect(Variable dest, Variable pointer, TypeNode targetType, Statement stat, object arg) {
      ExposureState estate=(ExposureState)arg;

      // no need to check pointer, since managed code guarantees them not to be null.

      // but we need to check pointer target type.

      // BUGBUG: temporary fix until we have better handling of address of
      //estate.SetAccordingToType(dest, targetType);
//      estate.AssignNonNull(dest);

      return estate;
    }

    protected override object VisitCallIndirect(Variable dest, Variable callee, Variable receiver, Variable[] arguments, FunctionPointer fp, Statement stat, object arg)
    {
      // don't do anything here, since this is not verifiable code
      return arg;
    }

    protected override object VisitCopyBlock(Variable destaddr, Variable srcaddr, Variable size, Statement stat, object arg)
    {
      // don't do anything here, since this is not verifiable code
      return arg;
    }

    protected override object VisitInitializeBlock(Variable addr, Variable val, Variable size, Statement stat, object arg)
    {
      // don't do anything here, since this is not verifiable code
      return arg;
    }

    protected override object VisitMakeRefAny(Variable dest, Variable source, TypeNode type, Statement stat, object arg)
    {
      ExposureState estate=(ExposureState)arg;

      return arg;
    }

    protected override object VisitRefAnyType(Variable dest, Variable source, Statement stat, object arg)
    {
      ExposureState estate=(ExposureState)arg;

      return arg;
    }

    protected override object VisitRefAnyValue(Variable dest, Variable source, TypeNode type, Statement stat, object arg)
    {
      ExposureState estate=(ExposureState)arg;
      return arg;
    }


    protected override object VisitInitObj(Variable addr, TypeNode valueType, Statement stat, object arg) {
      return arg;
    }

  }
}

