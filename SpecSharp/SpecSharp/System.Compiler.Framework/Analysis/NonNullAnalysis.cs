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
  using Cci = Microsoft.Cci;
#else
namespace System.Compiler{
  using Cci = System.Compiler;
#endif
  using Microsoft.Contracts;
  using AbstractValue = MathematicalLattice.Element;


  public interface INonNullState 
  {
    /// <summary>
    /// If variable is not a reference type, the nullness applies to the variable itself.
    /// If it is a Reference type however, the nullness applies to the contents of the reference.
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    bool IsNull(Variable v);
    /// <summary>
    /// If variable is not a reference type, the nullness applies to the variable itself.
    /// If it is a Reference type however, the nullness applies to the contents of the reference.
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    bool IsNonNull(Variable v);


    ISymValue Address(Variable v);
    ISymValue Value(ISymValue loc);
    ISymValue Field(Field f, ISymValue sv);
    bool IsNull(ISymValue sv);
    bool IsNonNull(ISymValue sv);
  }

 

  /// <summary>
  /// Exposes computed non-null information for a method
  /// </summary>
  public interface INonNullInformation {

    /// <summary>
    /// Provides the non-null information for the state on edge (from, to)
    /// </summary>
    INonNullState OnEdge(CfgBlock from, CfgBlock to);
  }


  /// <summary>
  /// NonNull states for variables and Objects.
  /// </summary>
  internal class NonNullState:IDataFlowState, INonNullState {

    internal sealed class Lattice : MathematicalLattice {

      /// <summary>
      /// Ordering:
      /// 
      ///   A lt B   iff
      ///   
      ///   !A.NonNull implies !B.NonNull
      ///   
      /// </summary>
      public class AVal : AbstractValue {
        public readonly bool IsNonNull;

        private AVal(bool nonnull) {
          this.IsNonNull = nonnull; 
        }

        public static AVal Bottom = new AVal(true);
        public static AVal Top = new AVal(false);
        public static AVal NonNull = Bottom;
        public static AVal MayBeNull = Top;

        private static AVal For(bool nonnull) {
          if (nonnull) return NonNull;
          else return MayBeNull;
        }

        public static AVal Join(AVal a, AVal b) {
          bool nonnull = a.IsNonNull && b.IsNonNull;
          return AVal.For(nonnull);
        }

        public static AVal Meet(AVal a, AVal b) {
          bool nonnull = a.IsNonNull || b.IsNonNull;
          return AVal.For(nonnull);
        }

        public override string ToString() {
          if (this.IsNonNull) return "NonNull";
          else return "MaybeNull";
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

        return (av.IsNonNull || !bv.IsNonNull);
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


    private static Identifier NullValue = Identifier.For("NullValue");
    private static Identifier ValueOf = Identifier.For("Value");

    private static Identifier IsInstId = Identifier.For("IsInst");
    private static Identifier LogicalNegId = Identifier.For("Not");
    private static Identifier EqNullId = Identifier.For("EqNull");
    private static Identifier NeNullId = Identifier.For("NeNull");

    private IEGraph egraph;
    private TypeSystem typeSystem;
    public TypeNode currentException;
    private static IDelayInfo existDelayInfo;

    public static IDelayInfo ExistDelayInfo
    {
      get
      {
        return existDelayInfo;
      }
    }

    public NonNullState(NonNullState old){
      this.typeSystem = old.typeSystem;
      this.egraph = (IEGraph)old.egraph.Clone();  
      this.currentException = old.currentException;
      // this.existDelayInfo = old.ExistDelayInfo;
    }

    public NonNullState(TypeSystem t, IDelayInfo ieinfo) {
      this.typeSystem = t;
      this.egraph = new EGraph(Lattice.It);
      this.currentException = null;
      existDelayInfo = ieinfo;

      // materialize the null symbol early on so 
      // all derived states share it.
      ISymValue nullsym = this.Null;
    }


    private NonNullState(IEGraph egraph, TypeNode currentException, TypeSystem t) {
      this.typeSystem = t;
      this.egraph = egraph;
      this.currentException = currentException;
    }


    private ISymValue Null {
      get {
        return this.egraph[NullValue];
      }
    }

    private ISymValue Address(Variable v) {
      return this.egraph[v];
    }

    private ISymValue TryAddress(Variable v) {
      return this.egraph.TryLookup(v);
    }

    private ISymValue Value(ISymValue loc) {
      return this.egraph[ValueOf, loc];
    }

    private ISymValue Value(Variable v) {
      return Value(Address(v));
    }

    private ISymValue TryValue(ISymValue loc) {
      return this.egraph.TryLookup(ValueOf, loc);
    }

    #region INonNullState members
    ISymValue INonNullState.Address(Variable v) {
      return TryAddress(v);
    }

    ISymValue INonNullState.Value(ISymValue loc) {
      if (loc == null) return null;
      return TryValue(loc);
    }

    ISymValue INonNullState.Field(Field f, ISymValue sv) {
      if (sv == null) return null;
      return egraph.TryLookup(f, sv);
    }

    bool INonNullState.IsNonNull(Variable v) {
      if (v.Type is Reference) {
        ISymValue val = TryValueValue(v);
        if (val == null) return false;
        return IsNonNull(val);
      }
      return IsNonNull(v);
    }

    bool INonNullState.IsNull(Variable v) {
      if (v.Type is Reference) {
        ISymValue val = TryValueValue(v);
        if (val == null) return false;
        return IsNull(val);
      }
      return IsNull(v);
    }

    bool INonNullState.IsNull(ISymValue sv) {
      if (sv == null) return false;
      return IsNull(sv);
    }

    bool INonNullState.IsNonNull(ISymValue sv) {
      if (sv == null) return false;
      return IsNonNull(sv);
    }

    #endregion

    private ISymValue TryValue(Variable v) {
      ISymValue loc = TryAddress(v);
      if (loc == null) return null;
      return TryValue(loc);
    }

    /// <summary>
    /// For indirect pointers (refs)
    /// </summary>
    private ISymValue TryValueValue(Variable v) {
      ISymValue addr = TryValue(v);
      if (addr == null) return null;
      return TryValue(addr);
    }

    /// <summary>
    /// Returns a set of definitely null variables and a set of definitely non-null variables
    /// </summary>
    public IFunctionalSet NullVariableSet(out IFunctionalSet nonNullResult) {
      ISymValue nullSym = this.Null;
      IFunctionalSet nullSet = FunctionalSet.Empty;
      IFunctionalSet nonNullSet = FunctionalSet.Empty;
      foreach (IUniqueKey key in this.egraph.Constants) {
        Variable v = key as Variable;
        if (v == null) continue;
        if (v is StackVariable) continue;
        ISymValue varSym = Value(v);
        if (varSym == nullSym) {
          nullSet = nullSet.Add(v);
          continue;
        }
        if (IsNonNull(varSym)) { nonNullSet = nonNullSet.Add(v); }
      }
      nonNullResult = nonNullSet;
      return nullSet;
    }

    private Lattice.AVal GetAVal(Variable v) {
      ISymValue val = TryValue(v);
      if (val == null) return Lattice.AVal.Top;

      return (Lattice.AVal)this.egraph[val];
    }

    /// <summary>
    /// Set v to a new value that is abstracted by av
    /// </summary>
    /// <param name="v"></param>
    /// <param name="av"></param>
    private void AssignAVal(Variable v, Lattice.AVal av) {
      ISymValue sv = this.egraph.FreshSymbol();
      ISymValue addr = Address(v);
      this.egraph[ValueOf, addr] = sv;
      this.egraph[sv] = av;
    }

    private bool IsNonNull(ISymValue sv) {
      return ((Lattice.AVal)this.egraph[sv]).IsNonNull;
    }

    public bool IsNonNull(Variable v) {
      if (IsNonNullType(v.Type)) return true;
      return GetAVal(v).IsNonNull;
    }

    public bool IsNonNullByAnalysis(Variable v)
    {
      return GetAVal(v).IsNonNull;
    }

    private bool IsNull(ISymValue sv) {
      return this.egraph.IsEqual(sv, this.Null);
    }
    
    public bool IsNull(Variable v) {
      ISymValue sv = TryValue(v);
      if (sv == null) return false;
      return IsNull(sv);
    }


    /// <summary>
    /// Adds assumption that sv == null
    /// </summary>
    public void AssumeNull(ISymValue sv) {
      this.egraph.AssumeEqual(sv, this.Null);
      this.egraph[this.Null] = Lattice.AVal.Top;
    }



    /// <summary>
    /// </summary>
    public void AssumeNull(Variable v) {
      ISymValue addr = Address(v);
      this.egraph[ValueOf, addr] = this.Null;
    }

    /// <summary>
    /// Adds assumption that v != null
    /// </summary>
    public void AssumeNonNull(Variable v) {
      ISymValue val = Value(v);
      if (val == this.Null) {
        // don't change meaning of null.
        this.AssignNonNull(v);
      }
      else {
        this.egraph[val] = Lattice.AVal.NonNull;
      }
    }


    public void AssignNonNull(Variable v) {
      AssignAVal(v, Lattice.AVal.NonNull);
    }

    public void AssignMaybeNull(Variable v) {
      AssignAVal(v, Lattice.AVal.MayBeNull);
    }

    public void SetToNull(Variable v) {
      ISymValue addr = Address(v);
      this.egraph[ValueOf, addr] = this.Null;
    }

    public void AssignNonPointer(Variable v) {
      ISymValue addr = TryAddress(v);
      if (addr == null) return;
      this.egraph.Eliminate(ValueOf, addr);
    }

    private void CopyValue(ISymValue destAddr, ISymValue srcAddr, TypeNode type) {
      Struct s = type as Struct;
      if (s != null && !type.IsPrimitive) {
        CopyStructValue(destAddr, srcAddr, s);
      }
      else {
        ISymValue svalue = this.egraph[ValueOf, srcAddr];
        this.egraph[ValueOf, destAddr] = svalue;
      }
    }


    private void CopyStructValue(ISymValue destAddr, ISymValue srcAddr, Struct type) {
      if (destAddr == null) return;

      foreach (IUniqueKey key in this.egraph.Functions(srcAddr)) {
        Field field = key as Field;
        Member member = null;
        TypeNode membertype = null;
        if (field != null) {
          member = field;
          membertype = field.Type;
        }
        else {
          Property prop = key as Property;
          if (prop == null) continue;
          member = prop;
          membertype = prop.Type;
        }
        if (member == null) continue;
        ISymValue destFld = this.egraph[member, destAddr];
        ISymValue srcFld = this.egraph[member, srcAddr];
        Struct memberStruct = type as Struct;
        if (memberStruct != null && !memberStruct.IsPrimitive) {
          // nested struct copy
          CopyStructValue(destFld, srcFld, memberStruct);
        }
        else {
          // primitive|pointer copy
          this.egraph[ValueOf, destFld] = this.egraph[ValueOf, srcFld];
        }
      }
    }

    public void CopyVariable(Variable source, Variable dest) {
      ISymValue srcAddr = Address(source);
      ISymValue destAddr = Address(dest);
      CopyValue(destAddr, srcAddr, source.Type);
    }

    public void AssignIsInstance(Variable dest, Variable source) {
      ISymValue sv = this.egraph.FreshSymbol();
      ISymValue sourceval = Value(source);
      ISymValue destaddr = Address(dest);
      this.egraph[ValueOf, destaddr] = sv;
      this.egraph[sv] = Lattice.AVal.MayBeNull;

      this.egraph[IsInstId, sourceval] = sv;
    }

    public void AssignBinary(Variable dest, NodeType op, Variable op1, Variable op2) {
      switch (op) {
        case NodeType.Eq:
          if (IsNull(op1)) {
            AssignEqNull(dest, op2);
            return;
          }
          if (IsNull(op2)) {
            AssignEqNull(dest, op1);
            return;
          }
          goto default;

        case NodeType.Ne:
          if (IsNull(op1)) {
            AssignNeNull(dest, op2);
            return;
          }
          if (IsNull(op2)) {
            AssignNeNull(dest, op1);
            return;
          }
          goto default;

        case NodeType.Add:
        case NodeType.Sub:
          // if exactly one of the operands is a pointer type
          if (op1.Type.IsPointerType && !op2.Type.IsPointerType) {
            CopyVariable(op1, dest);
            return;
          }
          else if (!op1.Type.IsPointerType && op2.Type.IsPointerType) {
            CopyVariable(op2, dest);
            return;
          }
          goto default;

        default:
          AssignNonPointer(dest);
          return;
      }
    }

    private void AssignEqNull(Variable dest, Variable operand) {
      ISymValue opval = Value(operand);
      ISymValue sv = this.egraph.FreshSymbol();
      ISymValue destaddr = Address(dest);
      this.egraph[ValueOf, destaddr] = sv;
      this.egraph[EqNullId, opval] = sv;
    }

    private void AssignNeNull(Variable dest, Variable operand) {
      ISymValue opval = Value(operand);
      ISymValue sv = this.egraph.FreshSymbol();
      ISymValue destaddr = Address(dest);
      this.egraph[ValueOf, destaddr] = sv;
      this.egraph[NeNullId, opval] = sv;
    }

    public void AssignUnary(Variable dest, NodeType op, Variable source) {
      if (op != NodeType.LogicalNot) {
        AssignNonPointer(dest);
      }
      ISymValue sv = this.egraph.FreshSymbol();
      ISymValue sourceval = Value(source);
      ISymValue destaddr = Address(dest);
      this.egraph[ValueOf, destaddr] = sv;
      this.egraph[LogicalNegId, sourceval] = sv;
    }

    // existential delay
    public bool isExistentialAtLocation(Variable dest, Statement position)
    {
      if (existDelayInfo != null)
      {
        IDelayState state;
        try
        {
          state = ExistDelayInfo.WhenAccessLocation(position);
        }
        catch (Exception)
        {
          return false;
        }
        if (state != null)
        {
          bool isTargetExistentialDelay = state.IsDelayed(dest); 
          if (isTargetExistentialDelay)
          {
            return true;
          }
        }
      }
      return false;
    }
    // end existential delay

    /// <summary>
    /// Check whether a given object is of nonnull type. It will check:
    /// Variable, Field, return type of Method.
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    public bool IsNonNullType(TypeNode t){
      if(t == null) {
        return false;
      }
      if (t is Reference) return true; // reference pointers are always non-null
      return typeSystem.IsNonNullType(t);
    }

    public void AssignAccordingToType(Variable v, TypeNode t) {
      if (!t.IsValueType) {
        if (IsNonNullType(t)) {
          this.AssignNonNull(v);
        }
        else {
          this.AssignMaybeNull(v);
        }
      }
      else {
        this.egraph.EliminateAll(Address(v));
      }
    }

    public void Manifest(Variable v) {
      ISymValue sv = this.egraph.TryLookup(v);
      if (sv == null) {
        AssignAccordingToType(v, v.Type);
      }
    }

    public Lattice.AVal FieldNullness(NonNullChecker checker, Field field, TypeSystem ts){
      if (IsNonNullType(field.Type)) {
        return Lattice.AVal.NonNull;
      }
      else if (field.Type != null && field.Type.IsValueType) {
        return Lattice.AVal.MayBeNull;
      }
      else {
        if (ts.FieldReadAsNonNull(checker.analyzer.CompilerOptions, field)) {
          // HACK HACK for now we assume fields of reference type are always non-null when read
          return Lattice.AVal.NonNull;
        }
        else {
          return Lattice.AVal.MayBeNull;
        }
      }

    }

    public Lattice.AVal PropertyNullness(Property property, TypeSystem ts){
      if (IsNonNullType(property.Type)) {
        return Lattice.AVal.NonNull;
      }
      else if (property.Type != null && property.Type.IsValueType) {
        return Lattice.AVal.MayBeNull;
      }
      else {
        return Lattice.AVal.MayBeNull;
      }
    }

    /// <summary>
    /// Assume all accessible locations in the heap are modified.
    /// </summary>
    public void HavocHeap() {

    }

    public void HavocFields(Field f) {
      foreach(ISymValue sv in this.egraph.SymbolicValues) {
        ISymValue loc = egraph.TryLookup(f, sv);
        if (loc != null) {
          egraph.Eliminate(ValueOf, loc);
        }
      }
    }

    /// <summary>
    /// Returns null, if result of Join is the same as atMerge.
    /// </summary>
    public static NonNullState Join(NonNullState atMerge, NonNullState incoming, CfgBlock joinPoint) {

      bool unchanged;
      IEGraph merged = atMerge.egraph.Join(incoming.egraph, joinPoint, out unchanged);

      TypeNode currentException = (atMerge.currentException != null)?
        ((incoming.currentException != null)? CciHelper.LeastCommonAncestor(atMerge.currentException, incoming.currentException) : null) : null;

      if (atMerge.currentException != currentException || !unchanged) {
        return new NonNullState(merged, currentException, atMerge.typeSystem);
      }
      return null;
    }

    public void Dump() {
      this.egraph.Dump(Console.Out);
    }


    public void RefineBranchInformation(Variable cond, out NonNullState trueState, out NonNullState falseState) {

      ISymValue cv = Value(cond);

      trueState = new NonNullState(this);
      falseState = new NonNullState(this);

      AssumeTrue(cv, ref trueState);
      AssumeFalse(cv, ref falseState);
    }


    /// <summary>
    /// Refines the given state according to the knowledge stored in the egraph about sv
    /// 
    /// In addition, the state can be null when the knowledge is inconsistent.
    /// </summary>
    /// <param name="cv">symbolic value we assume to be null (false)</param>
    private static void AssumeFalse(ISymValue cv, ref NonNullState state) {
      if (state == null) return;

      if (state.IsNonNull(cv)) {
        // infeasible
        // but we still want to go on analyzing this branch.

        state = null;
        return;
      }

      if (state.IsNull(cv)) return;

      foreach(EGraphTerm t in state.egraph.EqTerms(cv)) {
        if (t.Function == NonNullState.EqNullId) {
          // EqNull(op) == false, therefore op != null
          ISymValue op = t.Args[0];
          AssumeTrue(op, ref state);
        }
        if (t.Function == NonNullState.NeNullId) {
          // NeNull(op) == false, therefore op == null
          ISymValue op = t.Args[0];
          AssumeFalse(op, ref state);
        }
        if (t.Function == NonNullState.LogicalNegId) {
          // Not(op) == false, therefore op == true
          ISymValue op = t.Args[0];
          AssumeTrue(op, ref state);
        }
        if (t.Function == NonNullState.IsInstId) {
          // IsInst(op) == null, cannot deduce anything about op
        }
      }
      // needs to be after we check EqTerms, as they verify mapping is current.
      if (state != null) state.AssumeNull(cv);
    }

    /// <summary>
    /// Refines the given state according to the knowledge stored in the egraph about sv
    /// 
    /// In addition, the state can be null when the knowledge is inconsistent.
    /// </summary>
    /// <param name="cv">symbolic value we assume to be non-null (true)</param>
    /// <param name="state">state if sv is non-null (true)</param>
    private static void AssumeTrue(ISymValue cv, ref NonNullState state) {

      if (state == null) return;

      if (state.IsNull(cv)) {
        // infeasible
        state = null;
        return;
      }

      if (state.IsNonNull(cv)) return;

      state.egraph[cv] = Lattice.AVal.NonNull;

      foreach(EGraphTerm t in state.egraph.EqTerms(cv)) {
        if (t.Function == NonNullState.EqNullId) {
          ISymValue op = t.Args[0];
          AssumeFalse(op, ref state);
        }
        if (t.Function == NonNullState.NeNullId) {
          ISymValue op = t.Args[0];
          AssumeTrue(op, ref state);
        }
        if (t.Function == NonNullState.LogicalNegId) {
          ISymValue op = t.Args[0];
          AssumeFalse(op, ref state);
        }
        if (t.Function == NonNullState.IsInstId) {
          ISymValue op = t.Args[0];
          AssumeTrue(op, ref state);
        }
      }
    }


    public void StoreProperty(Variable dest, Property property, Variable source) {
      ISymValue srcAddr = Address(source);
      ISymValue destobj;
      if (dest != null) {
        destobj = Value(dest);
      }
      else {
        destobj = Null;
      }
      ISymValue loc = this.egraph[property, destobj];
      CopyValue(loc, srcAddr, property.Type);
    }

    public bool LoadProperty(Variable source, Property property, Variable dest) {
      // property.Type is not very right at the momment, passing in t as a temp work around
      ISymValue destAddr = Address(dest);
      ISymValue sourceobj;
      if (source != null) {
        sourceobj = Value(source);
      }
      else {
        sourceobj = Null;
      }
      ISymValue loc = GetPropertyAddress(sourceobj, property);
      if (IsNonNullType(property.Type))
      {  // t should really be prop.Type, but it doesnt work, temp work around
        ISymValue val = this.egraph.TryLookup(ValueOf, loc);
        if (val == null)
        {
          // manifest and set abstract value according to type.
          val = this.egraph[ValueOf, loc];
          this.egraph[val] = Lattice.AVal.NonNull; // t is non null
          //PropertyNullness(prop, this.typeSystem);
        }
      }
      CopyValue(destAddr, loc, property.Type);
      if (IsNonNullType(property.Type)) {
        return true;
      }
      else {
        if (property.Type.IsValueType) {
          return true;
        }
        else {
          ISymValue val = this.egraph[ValueOf, destAddr];
          return IsNonNull(val);
        }
      }
    }
    private ISymValue GetPropertyAddress(ISymValue sourceobj, Property prop) {
      ISymValue loc = this.egraph[prop, sourceobj];      
      return loc;
    }


    public void StoreField(Variable dest, Field field, Variable source) {
      HavocFields(field);
      ISymValue srcAddr = Address(source);
      ISymValue destobj;
      if (dest != null) {
        destobj = Value(dest);
      }
      else {
        destobj = Null;
      }
      ISymValue loc = this.egraph[field, destobj];
      CopyValue(loc, srcAddr, field.Type);
    }

    public bool LoadField(NonNullChecker checker, Variable source, Field field, Variable dest, 
      bool isDestExistentialDelayed)
    {
      ISymValue destAddr = Address(dest);
      ISymValue sourceobj;
      if (source != null)
      {
        if (source.Type.IsValueType)
        {
          sourceobj = Address(source);
        }
        else
        {
          sourceobj = Value(source);
        }
      }
      else
      {
        sourceobj = Null;
      }
      // existential delay
      ISymValue loc = GetFieldAddress(checker, sourceobj, field, !isDestExistentialDelayed);
      // end exitential delay
      CopyValue(destAddr, loc, field.Type);
      if (field.Type.IsValueType)
      {
        return true;
      }
      else
      {
        ISymValue val = this.egraph[ValueOf, destAddr];
        if (IsNonNull(val)) return true;
      }
      if (isDestExistentialDelayed)
      { 
        return false;
      }
      if (IsNonNullType(field.Type))
      {
        return true;
      }
      else
      {
        return false;
      }
    }

    public void LoadAddress(Variable source, Variable dest) {
      ISymValue sourceaddr = Address(source);
      ISymValue destaddr = Address(dest);
      this.egraph[ValueOf, destaddr] = sourceaddr;
      this.egraph[sourceaddr] = Lattice.AVal.NonNull;
    }

    public bool IsFieldNull(NonNullChecker checker, Variable source, Field field)
    {
      ISymValue sourceobj;
      if (source != null) {
        sourceobj = Value(source);
      }
      else {
        sourceobj = Null;
      }
      ISymValue addr = GetFieldAddress(checker, sourceobj, field, true);
      ISymValue fieldValue = Value(addr);
      return this.IsNull(fieldValue);
    }

    public void LoadFieldAddress(NonNullChecker checker, Variable source, Field field, Variable dest) {
      ISymValue destaddr = Address(dest);
      ISymValue sourceobj;
      if (source != null) {
        sourceobj = Value(source);
      }
      else {
        sourceobj = Null;
      }
      ISymValue addr = GetFieldAddress(checker, sourceobj, field, true);
      this.egraph[addr] = Lattice.AVal.NonNull;
      this.egraph[ValueOf, destaddr] = addr;
    }

    private ISymValue GetFieldAddress(NonNullChecker checker, ISymValue sourceobj, 
      Field f, bool setFieldNullnessAccordingToType) 
    {
      ISymValue loc = this.egraph[f, sourceobj];
      if (!f.Type.IsValueType) {
        ISymValue val = this.egraph.TryLookup(ValueOf, loc);
        if (val == null) {
          // manifest and set abstract value according to type.
          // existential type: dont trust type if existential type is involved... we may do nothing
          // may assigning its own type. 
          if (setFieldNullnessAccordingToType)
          {
            val = this.egraph[ValueOf, loc];
            this.egraph[val] = FieldNullness(checker, f, this.typeSystem);
          }
        }
      }
      return loc;
    }


    public bool LoadIndirect(Variable source, Variable dest) {
      ISymValue refptr = Value(source);
      ISymValue destaddr = Address(dest);
      ISymValue contents = Value(refptr);
      this.egraph[ValueOf, destaddr] = contents;
      return IsNonNull(contents);
    }

    public void StoreIndirect(Variable source, Variable dest) {
      ISymValue refptr = Value(dest);
      ISymValue contents = Value(source);
      this.egraph[ValueOf, refptr] = contents;
    }

    /// <summary>
    /// Havoc the contents of the pointed to locations, but 
    /// if non-null, reestablish that invariant.
    /// </summary>
    /// <param name="pointer"></param>
    /// <param name="type"></param>
    public void HavocIndirect(Variable pointer, Reference type) {
      if (type == null) return; // indicates not a reference type

      ISymValue refptr = Value(pointer);
      if (type.ElementType.IsValueType) {
        this.egraph.EliminateAll(refptr);
      }
      else {
        this.egraph.Eliminate(ValueOf, refptr);
        if (IsNonNullType(type.ElementType)) {
          this.egraph[Value(refptr)] = Lattice.AVal.NonNull;
        }
      }
    }
  }


  /// <summary>
  /// The main class for NonNull checking.
  /// </summary>
  internal class NonNullChecker:ForwardDataFlowAnalysis, INonNullInformation {

    /// <summary>
    /// Current NonNull checking visitor
    /// </summary>
    NonNullInstructionVisitor iVisitor;

    /// <summary>
    /// Current block being analyzed.
    /// </summary>
    internal CfgBlock currBlock;
    internal TypeSystem typeSystem;
    internal Method currentMethod;
    internal Analyzer analyzer;

    #region Identifying not-null assert methods
    Identifier AssertNotNullImplicit = Identifier.For("AssertNotNullImplicit");
    Identifier AssertNotNull = Identifier.For("AssertNotNull");
    Identifier AssertNotNullImplicitGeneric = Identifier.For("AssertNotNullImplicitGeneric");
    Identifier AssertNotNullGeneric = Identifier.For("AssertNotNullGeneric");

    internal bool IsAssertNotNullMethod(Method m) {
      if (m == null) return false;
      return
         m.Name.UniqueIdKey == AssertNotNull.UniqueIdKey
      || m.Template != null && m.Template.Name.UniqueIdKey == AssertNotNullGeneric.UniqueIdKey;
    }

    internal bool IsAssertNotNullImplicitMethod(Method m) {
      if (m == null) return false;
      return
         m.Name.UniqueIdKey == AssertNotNullImplicit.UniqueIdKey
      || m.Template != null && m.Template.Name.UniqueIdKey == AssertNotNullImplicitGeneric.UniqueIdKey;
    }
    #endregion

    #region Error Reporting
    /// <summary>
    /// Used to avoid repeated error/warning report for the same Node.
    /// 
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
    internal void HandleError(Statement stat, Node node, Error error, params string[] m) {

      Node offendingNode = node;
      if (offendingNode.SourceContext.Document == null) {
        offendingNode = stat;
      }
      else if (node is StackVariable && stat.SourceContext.Document != null && stat.SourceContext.Document != node.SourceContext.Document) {
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
    internal void HandleError(Statement stat, Node node, Error error, TypeNode t) {
      Node offendingNode = node;
      if (offendingNode.SourceContext.Document == null) {
        offendingNode = stat;
      }
      Debug.Assert(t != null);
      if (reportedErrors.Contains(offendingNode.SourceContext))
        return;
      //Analyzer.WriteLine("!!! " + error+ " : "+node);
      typeSystem.HandleError(offendingNode, error, typeSystem.GetTypeName(t));
      reportedErrors.Add(offendingNode.SourceContext, null);
    }
    #endregion 

    #region INonNullInformation members and helpers


    private IFunctionalMap/*Block,NonNullState*/[] nonNullEdgeInfo; // indexed by from block index

    INonNullState INonNullInformation.OnEdge(CfgBlock from, CfgBlock to) 
    {
      IFunctionalMap toMap = this.nonNullEdgeInfo[from.Index];
      if (toMap == null) {
        return null;
      }
      return (INonNullState)toMap[to];
    }
    #endregion

    #region Non-Null Optimization

    /// <summary>
    /// This map keeps track of which expression statements representing a non null assertion check can
    /// be eliminated. Statements not in the map cannot be eliminated, others according to the stored value.
    /// </summary>
    private System.Collections.Generic.Dictionary<ExpressionStatement, bool> eliminateCheck = new System.Collections.Generic.Dictionary<ExpressionStatement, bool>();

    internal void RecordUnnecessaryCheck(ExpressionStatement es) {
      if (es == null) return;
      eliminateCheck[es] = true;
    }

    internal void RecordNecessaryCheck(ExpressionStatement es) {
      if (es == null || !eliminateCheck.ContainsKey(es)) return;
      eliminateCheck.Remove(es);
    }

    private class NonNullOptimizer : StandardVisitor {
      private System.Collections.Generic.Dictionary<ExpressionStatement, bool> eliminateCheck = new System.Collections.Generic.Dictionary<ExpressionStatement, bool>();
      private NonNullChecker checker;

      public NonNullOptimizer(NonNullChecker checker, System.Collections.Generic.Dictionary<ExpressionStatement, bool> eliminateCheck) {
        this.eliminateCheck = eliminateCheck;
        this.checker = checker;
      }

      private bool Eliminate(Statement st) {
        ExpressionStatement es = st as ExpressionStatement;
        if (es == null) return false;
        ExpressionStatement copy = this.checker.cfg.CodeMap[es];
        if (copy == null) return false;
        bool elim;
        if (this.eliminateCheck.TryGetValue(copy, out elim)) {
          return elim;
        }
        return false;
      }

      private void WarnIfExplicit(ExpressionStatement es) {
        if (es == null) return;
        MethodCall mcall = es.Expression as MethodCall;
        if (mcall == null) return;
        MemberBinding mb = mcall.Callee as MemberBinding;
        if (mb == null) return;
        if (checker.IsAssertNotNullMethod(mb.BoundMember as Method)) {
          checker.HandleError(es, mcall, Error.UnnecessaryNonNullCoercion);
        }
      }

      public override Expression VisitBlockExpression(BlockExpression blockExpression) {
        Block block = blockExpression.Block;
        if (block == null) return blockExpression;
        StatementList sl = block.Statements;
        if (sl == null) return blockExpression;
        if (sl.Count == 2 && this.Eliminate(sl[0])) {
          // the entire expression turns into the expression of the expression statement at sl[1]
          ExpressionStatement es = (ExpressionStatement)sl[1];
          WarnIfExplicit(sl[0] as ExpressionStatement);
          return VisitExpression(es.Expression);
        }
        if (sl.Count == 3 && this.Eliminate(sl[1])) {
          // the entire expression turns into the Source of the assignment at sl[0]
          AssignmentStatement asg = (AssignmentStatement)sl[0];
          WarnIfExplicit(sl[1] as ExpressionStatement);
          return VisitExpression(asg.Source);
        }
        return base.VisitBlockExpression(blockExpression);
      }
    }

    private void OptimizeMethodBody(Method method) {
      if (this.eliminateCheck.Count == 0) return;

      NonNullOptimizer nnopt = new NonNullOptimizer(this, this.eliminateCheck);

      nnopt.VisitMethod(method);
    }

    #endregion

    /// <summary>
    /// Entry point to check a method.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="method"></param>
    public static INonNullInformation Check(TypeSystem t, Method method, Analyzer analyzer) 
    {
      if(method==null) 
        return null;
      NonNullChecker checker = new NonNullChecker(t, method, analyzer);
      ControlFlowGraph cfg = analyzer.GetCFG(method);
      if (cfg != null) {
        checker.nonNullEdgeInfo = new IFunctionalMap[cfg.BlockCount];
        checker.Run(cfg, new NonNullState(t, analyzer.DelayInfo));

        checker.OptimizeMethodBody(method);
        return checker;
      }
      return null;
    }

    private void RecordEdgeInfo(CfgBlock from, CfgBlock to, IDataFlowState state) {
      IFunctionalMap old = this.nonNullEdgeInfo[from.Index];
      if (old == null) old = FunctionalMap.Empty;

      this.nonNullEdgeInfo[from.Index] = old.Add(to, state);
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="t"></param>
    /// <param name="method"></param>
    protected NonNullChecker(TypeSystem t, Method method, Analyzer analyzer) {
      this.typeSystem = t;
      this.analyzer = analyzer;
      this.currentMethod=method;
      this.iVisitor=new NonNullInstructionVisitor(this);
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

      // record edge information
      RecordEdgeInfo(previous, joinPoint, incoming);

      // Initialize states
      if(atMerge==null)
      {
        resultDiffersFromPreviousMerge = true;
        return incoming;
      }

      if (Analyzer.Debug) {
        Console.WriteLine("Merge at Block {0}-----------------", joinPoint.UniqueKey);
        Console.WriteLine("  State at merge");
        atMerge.Dump();
        Console.WriteLine("  Incoming");
        incoming.Dump();
      }

      // Merge the two.
      
      NonNullState newState = NonNullState.Join((NonNullState)atMerge, (NonNullState)incoming, joinPoint);

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

      NonNullState newState; 
      if(stateOnEntry==null)
        newState=new NonNullState(typeSystem,null);
      else
        newState=new NonNullState((NonNullState)stateOnEntry);

      return base.VisitBlock (block, newState);
    }

    /// <summary>
    /// It visit individual statement. It is called from VisitBlock.
    /// 
    /// It will call NonNullInstructionVisitor
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

      NonNullState state = (NonNullState)currentHandlerState;

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
        state = new NonNullState(state);
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
  internal class NonNullInstructionVisitor:InstructionVisitor {


    /// <summary>
    /// Current NonNullChecker
    /// </summary>
    private NonNullChecker NNChecker;

    private TypeSystem ts;

    private void HandleError(Statement stat, Node node, Error error, params string[] m) {
      NNChecker.HandleError(stat, node, error, m);
    }
    private void HandleError(Statement stat, Node node, Error error, TypeNode t) {
      NNChecker.HandleError(stat, node, error, t);
    }

    /// <summary>
    /// For the possible receiver v, check if it is nonnull. if no, file an proper
    /// error/warning.
    /// </summary>
    /// 
    private void CheckReceiver(Statement stat, Variable v, NonNullState nn)
    {
      Node offendingNode = v;
      if (v == null) return;
      if (v.Type.IsValueType) return;

      // Create a better source context for receiver null errors.
      offendingNode = new Statement(NodeType.Nop);
      offendingNode.SourceContext = v.SourceContext;
      //     offendingNode.SourceContext.StartPos = offendingNode.SourceContext.EndPos;
      //     offendingNode.SourceContext.EndPos++;

      if (nn.IsNull(v))
      {
        HandleError(stat, offendingNode, Error.ReceiverCannotBeNull, this.ts.GetTypeName(v.Type));
        nn.AssignNonNull(v);
      }
      else
      {
        if (!nn.IsNonNull(v))
        {
          HandleError(stat, offendingNode, Error.ReceiverMightBeNull, this.ts.GetTypeName(v.Type));
          nn.AssumeNonNull(v);
        }
      }
    }
    private void CheckReceiver(Statement stat, Variable v, NonNullState nn, Node node)
    {
      Node offendingNode = v;
      if (v == null) return;
      if (v.Type.IsValueType) return;

      // Create a better source context for receiver null errors.
      offendingNode = new Statement(NodeType.Nop);
      offendingNode.SourceContext = v.SourceContext;
 //     offendingNode.SourceContext.StartPos = offendingNode.SourceContext.EndPos;
 //     offendingNode.SourceContext.EndPos++;

      if(nn.IsNull(v))
      {
        HandleError(stat, offendingNode, Error.ReceiverCannotBeNull, this.ts.GetTypeName(v.Type));
        nn.AssignNonNull(v);
      }
      else if(!nn.IsNonNull(v))
      {
        HandleError(stat, offendingNode, Error.ReceiverMightBeNull, this.ts.GetTypeName(v.Type));
        nn.AssumeNonNull(v);
      }
    }

     
    private void CheckPointerUse(Statement stat, Variable v, NonNullState nn, string purpose)
    {
      if (v == null) return;

      if(nn.IsNull(v))
      {
        HandleError(stat, v, Error.UseOfNullPointer, purpose);
        nn.AssignNonNull(v);
      }
      else if(!nn.IsNonNull(v))
      {
        HandleError(stat, v, Error.UseOfPossiblyNullPointer, purpose);
        nn.AssumeNonNull(v);
      }
    }


    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="c"></param>
    public NonNullInstructionVisitor(NonNullChecker c)
    {
      NNChecker=c;
      ts=c.typeSystem;
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
      NonNullState nn=(NonNullState)arg;

      foreach (Parameter p in parameters) {
        nn.AssignAccordingToType(p, p.Type);
      }
      if (!method.IsStatic)
        nn.AssignNonNull(CciHelper.GetThis(method));
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
      NonNullState nn=(NonNullState)arg;

      if(nn.IsNonNullType(dest.Type))
      {
          if(nn.IsNull(source))
        {
          HandleError(stat, source, Error.CannotCoerceNullToNonNullType);
          return arg;
        }
        else if (!nn.IsNonNull(source))
        {
          //System.Console.WriteLine("visit copy warning: from {0} to {1}", source, dest);
          HandleError(stat, source, Error.CoercionToNonNullTypeMightFail, dest.Type);
          nn.AssumeNonNull(dest);
          return arg;
        }
      }

      nn.CopyVariable(source, dest);

      // After making sure nullness of source and target is fine, we can move
      // the type information of the source to analysis result.
      if (nn.IsNonNullType(source.Type) && !nn.IsNonNull(dest))
      {
       // System.Console.WriteLine(" -------- adopt source's type information");
       //nn.AssumeNonNull(dest);
      }

      return arg;
    }

    protected override object VisitLoadFunction(Variable dest, Variable source, Method method, Statement stat, object arg)
    {
      NonNullState nn=(NonNullState)arg;

      if (method.IsVirtual) 
      {
        // Check for Receiver non-nullness
        CheckReceiver(stat,source,nn);
      }

      // always produces non-null value
      nn.AssignNonNull(dest);
  
      return arg;

    }

    protected override object VisitLoadNull(Variable dest, Literal source, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;

      if(ts.IsNonNullType(dest.Type)) {
        HandleError(stat, source, Error.CannotCoerceNullToNonNullType);
      }
      else {
        nn.SetToNull(dest);
      }
      return arg;
    }


    protected override object VisitLoadConstant(Variable dest, Literal source, Statement stat, object arg) 
    {
      NonNullState nn=(NonNullState)arg;

      if (source.Value is string) {
        nn.AssignNonNull(dest);
      }
      else {
        // not a pointer
        nn.AssignNonPointer(dest);
      }
      return arg;
    }


    /// <summary>
    /// Note: casts don't require a non-null argument. null value casts always succeed.
    /// </summary>
    protected override object VisitCastClass(Variable dest, TypeNode type, Variable source, Statement stat, object arg) {
      NonNullState nn = (NonNullState)arg;

      if ( ! nn.IsNull(source)) {
        NonNullState exnState = nn;
        nn = new NonNullState(nn); // make copy of current state to continue modify
        exnState.currentException=SystemTypes.InvalidCastException;
        NNChecker.PushExceptionState(NNChecker.currBlock, exnState);
      }

      // acts like a copy retaining null status
      nn.CopyVariable(source, dest);

      if(nn.IsNonNullType(dest.Type) && ! nn.IsNonNull(source)) {
        if(nn.IsNull(source))
          HandleError(stat, source, Error.CannotCoerceNullToNonNullType);
        else {
          //System.Console.WriteLine("visit cast class: dest:{0}, source:{1}", dest, source);
          HandleError(stat, source, Error.CoercionToNonNullTypeMightFail,dest.Type);
        };
        // mask future errors
        nn.AssignNonNull(dest);
      }
      return nn;
    }

    private Method AssumeMethod = SystemTypes.AssertHelpers.GetMethod(Identifier.For("Assume"),SystemTypes.Boolean);
    private Method AssertMethod = SystemTypes.AssertHelpers.GetMethod(Identifier.For("Assert"),SystemTypes.Boolean);
    private Method AssertLoopInvariantMethod = SystemTypes.AssertHelpers.GetMethod(Identifier.For("AssertLoopInvariant"),SystemTypes.Boolean);
    private Method GetTypeFromHandleMethod = (Method)SystemTypes.Type.GetMembersNamed(Identifier.For("GetTypeFromHandle"))[0];

    /// <summary>
    /// The checker assumes that all methods in AssertHelpers that have a boolean as their
    /// first argument are assertions that are called with false and will not return.
    /// </summary>
    private bool IsAssertionMethodThatDoesNotReturn(Method callee) {
      if (callee == null) return false;
      if (callee.DeclaringType == SystemTypes.AssertHelpers &&
          callee.Parameters[0].Type == SystemTypes.Boolean) return true;
      return false;
    }
    private NonNullState PushNullException(NonNullState nn) {
      NonNullState exnState = nn;
      nn = new NonNullState(nn);
      exnState.currentException=SystemTypes.NullReferenceException;
      NNChecker.PushExceptionState(NNChecker.currBlock, exnState);
      return nn;
    }
    private void RecordUnnecessaryCheck(Statement stat) {
      this.NNChecker.RecordUnnecessaryCheck(stat as ExpressionStatement);
    }
    private void RecordNecessaryCheck(Statement stat) {
      this.NNChecker.RecordNecessaryCheck(stat as ExpressionStatement);
    }
    protected override object VisitCall(Variable dest, Variable receiver, Method callee, ExpressionList arguments, bool virtcall, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;

      bool resultIsNonNull = false;

      // Check for Receiver
      if (!callee.IsStatic)
        CheckReceiver(stat,receiver,nn);


      // Check for parameter matching.
      if (arguments!=null && callee.Parameters != null){
        for(int i=0;i<callee.Parameters.Count;i++){
          Variable actual = (Variable)arguments[i];
          if (ts.IsNonNullType(callee.GetParameterTypes()[i])) {
            if(nn.IsNull(actual)) {
              HandleError(stat, actual, Error.CannotCoerceNullToNonNullType);
            }
            else if(!nn.IsNonNull(actual)) {
              //System.Console.WriteLine("visit call, argument: {0}", actual);
              HandleError(stat, actual, Error.CoercionToNonNullTypeMightFail, callee.GetParameterTypes()[i]);
            }
          }
          nn.HavocIndirect(actual, callee.Parameters[i].Type as Reference);
        }
      }

      // special case some methods
      if (this.IsAssertionMethodThatDoesNotReturn(callee)) {
        // we assume that all assertion methods are called with false.
        return null;
      }
      else if (this.NNChecker.IsAssertNotNullImplicitMethod(callee)) {
        if (arguments.Count == 1){
          Variable source = (Variable)arguments[0]; // guaranteed by CodeFlattener
          // compiler inserts this test throughout, so let's warn here.
          if (nn.IsNonNull(source)) {
            // opportunity to optimize away check.
            RecordUnnecessaryCheck(stat);
            // Console.WriteLine("Could optimize IsNonNull check at line {0}", stat.SourceContext.StartLine);
          }
          else {
            RecordNecessaryCheck(stat);
            if (nn.IsNull(source)) {
              HandleError(stat, source, Error.CannotCoerceNullToNonNullType);
              // do not explore this path further, except for exceptional path
              PushNullException(nn);
              return null;
            }
            else {
              //System.Console.WriteLine("visit call and callee is assertnotnullimplicitmethod");
              HandleError(stat, source, Error.CoercionToNonNullTypeMightFail, 
                OptionalModifier.For(SystemTypes.NonNullType, source.Type));
            }
          }
          nn = PushNullException(nn);
          nn.AssumeNonNull(source);
        }
      }
      else if (NNChecker.IsAssertNotNullMethod(callee)) {
        if (arguments.Count == 1){
          Variable source = (Variable)arguments[0]; // guaranteed by CodeFlattener
          // User inserted cast, so let's warn if it is unnecessary.
          if (nn.IsNonNull(source)) {
            // Let user know his check is useless here.
            RecordUnnecessaryCheck(stat);
            // Console.WriteLine("Could optimize IsNonNull check at line {0}", stat.SourceContext.StartLine);
          }
          else {
            RecordNecessaryCheck(stat);
            if (nn.IsNull(source)) {
              // Error already emitted at checker time. HandleError(stat, source, Error.CannotCoerceNullToNonNullType);
              // do not explore this path further except for exceptional path
              PushNullException(nn);
              return null;
            }
          }

          nn = PushNullException(nn);
          nn.AssumeNonNull(source);
        }
      }
      else if ( callee.Name.Name == "GetEnumerator" ) {
        // special case assume result is non-null because it is in generated code and confuses.
        if (dest != null) {
          nn.AssignNonNull(dest);
          dest = null; // avoid setting dest again below.
        }
      }
      else if (callee == GetTypeFromHandleMethod) {
        // special case that should be handled by annotating mscorlib
        if (dest != null) {
          nn.AssignNonNull(dest);
          dest = null; // avoid setting dest again below
        }
      }
      else {
        Property pget = IsPropertyGetter(callee);
        if (pget != null) {
          resultIsNonNull = nn.LoadProperty(receiver, pget, dest);
          dest = null; // prevent remaining code to overwrite dest
        }
        else {
          Property pset = IsPropertySetter(callee);
          if (pset != null) {
            nn.StoreProperty(receiver, pset, (Variable)arguments[0]);
          }
          else if (!callee.IsPure) {
            // Non-pure method: assume it destroy all heap patterns.
            nn.HavocHeap();
          }
        }
      }

      // Push possible exceptions to handlers.
      if (callee.Contract != null) {
        for (int i = 0; i < callee.Contract.Ensures.Count; i++) {
          EnsuresExceptional e = callee.Contract.Ensures[i] as EnsuresExceptional;
          if (e != null) {
            NonNullState exnState = nn;
            nn = new NonNullState(nn);
            exnState.currentException = e.Type;
            NNChecker.PushExceptionState(NNChecker.currBlock, exnState);
          }
        }
      }


      // Return type matching.
      if(dest!=null){
        if (nn.IsNonNullType(dest.Type) && ! (resultIsNonNull || nn.IsNonNullType(callee.ReturnType))) {
          if (!(callee.DeclaringMember is Property))
            HandleError(stat, stat, Error.CoercionToNonNullTypeMightFail,dest.Type);
          nn.AssignNonNull(dest);
        }
        else {
            nn.AssignAccordingToType(dest, callee.ReturnType);
        }
      }
      return nn;
    }

    private Property IsPropertyGetter(Method m) {
      if (m.Parameters != null && m.Parameters.Count != 0) return null;
      Property p = m.DeclaringMember as Property;
      if (p != null && m.Name.Name.StartsWith("get_")) {
        return p;
      }
      return null;
    }

    private Property IsPropertySetter(Method m) {
      if (m.Parameters == null || m.Parameters.Count != 1) return null;
      Property p = m.DeclaringMember as Property;
      if (p != null && m.Name.Name.StartsWith("set_")) {
        return p;
      }
      return null;
    }

    protected override object VisitConstrainedCall(Variable dest, Variable receiver, Method callee, ExpressionList arguments, TypeNode constraint, Statement stat, object arg) {
      Reference rtype = receiver.Type as Reference;
      if (rtype != null && rtype.ElementType != null && !rtype.ElementType.IsValueType) {
        // instance could be a reference type that could be null.

        // BUGBUG: when we track address of, we need to check here that target is indeed non-null
      }
      return VisitCall(dest, receiver, callee, arguments, true, stat, arg);
    }

    protected override object VisitLoadField(Variable dest, Variable source, Field field, Statement stat, object arg)
    {
      NonNullState nn = (NonNullState)arg;

      CheckReceiver(stat, source, nn);

      bool destIsExistential = nn.isExistentialAtLocation(dest, stat/* source.SourceContext.StartPos*/);
      //if (dest is LocalBinding)
      //  System.Console.WriteLine("{0} is existential: {1}", (dest as LocalBinding).BoundLocal.Name,
      //    destIsExistential);
      bool fieldIsNonNull = nn.LoadField(this.NNChecker, source, field, dest, destIsExistential);

      if (nn.IsNonNullType(dest.Type) && !fieldIsNonNull)
      {
        //System.Console.WriteLine("load field: {0}, source {1}, dest:{2}", field,source,dest);
        HandleError(stat, dest, Error.CoercionToNonNullTypeMightFail, dest.Type);
        nn.AssignNonNull(dest);
      }
      return arg;
    }

    protected override object VisitStoreField(Variable dest, Field field, Variable source, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;

      // static Fields
      if(field.IsStatic){
      }

      CheckReceiver(stat,dest,nn);

      if (nn.IsNonNullType(field.Type)) 
      {
        if(nn.IsNull(source))
          HandleError(stat, source, Error.CannotCoerceNullToNonNullType);
        else if ( ! nn.IsNonNull(source)) 
          HandleError(stat, source, Error.CoercionToNonNullTypeMightFail,field.Type);
      }
        // Diego: Enforcement of once attributes
      CheckFieldOnceness(dest, field, stat, nn);

      nn.StoreField(dest, field, source);
      return arg;
    }
      /// <summary>
      /// Enforcement of once attributes: Check if the field is written more than one
      /// and also allow to write it if it was null before
      /// If required a to register the field mutated at the class level (FieldUsage in Analyzer)
      /// </summary>
      /// <param name="dest"></param>
      /// <param name="field"></param>
      /// <param name="stat"></param>
      /// <param name="nn"></param>
    private void CheckFieldOnceness(Variable dest, Field field, Statement stat, NonNullState nn)
    {
      IMutableSet fieldUsage = this.NNChecker.analyzer.FieldUsage;
      if (field.IsOnce && fieldUsage != null) {
        if (!fieldUsage.Contains(field)) {
          fieldUsage.Add(field);

          if (!nn.IsFieldNull(this.NNChecker, dest, field) && !this.ts.IsNullableType(field.Type)) { //hack
            // Need to add a new type of error
            HandleError(stat, stat, Error.GenericWarning, "[Once] fields can be written only if null");
          }
        }
        else {
          // Need to add a new type of error
          HandleError(stat, stat, Error.GenericWarning, "[Once] fields can be written only once.");
        }

      }
    }


    protected override object VisitReturn(Variable var, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;

      if(nn.IsNonNullType(NNChecker.currentMethod.ReturnType)){
        if (nn.IsNull(var))
          HandleError(stat,var,Error.CannotCoerceNullToNonNullType);
        else if(!nn.IsNonNull(var)) {
          //System.Console.WriteLine("return var:{0}", var);
          HandleError(stat,var,Error.CoercionToNonNullTypeMightFail, NNChecker.currentMethod.ReturnType);
        }
      }
      return arg;
    }
  
    protected override object VisitUnwind(Statement stat, object arg) {
      return arg;
    }
    protected override object VisitNop(Statement stat, object arg) {
      return arg;
    }

    protected override object VisitBranch(Variable cond, Block target, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;

      if(cond==null)
        return arg;

      NonNullState trueState, falseState;

      nn.RefineBranchInformation(cond, out trueState, out falseState);

      if ( trueState != null ) {
        NNChecker.PushState(NNChecker.currBlock, NNChecker.currBlock.TrueContinuation, trueState);
      }
      if ( falseState != null ) {
        NNChecker.PushState(NNChecker.currBlock, NNChecker.currBlock.FalseContinuation, falseState);
      }
      return null;
    }

    protected override object VisitBinaryOperator(NodeType op, Variable dest, Variable operand1, Variable operand2, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg; 

      nn.AssignBinary(dest, op, operand1, operand2);

      return arg;
    }

    protected override object VisitUnaryOperator(NodeType op, Variable dest, Variable operand, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg; 

      if (op == NodeType.Ldlen) {
        CheckPointerUse(stat, operand, nn, " to get array length");
      }
      nn.AssignUnary(dest, op, operand);

      return arg;
    }
    protected override object VisitSizeOf(Variable dest, TypeNode value_type, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg; 
      
      nn.AssignNonPointer(dest);
      return arg;
    }

    protected override object VisitBox(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;

      if (type.IsTemplateParameter) {
        // Box on generic variables is a no-op if interpreted as a pointer.
        return VisitCopy(dest, source, stat, arg);
      }
      nn.AssignNonNull(dest);
      return arg;
    }

    protected override object VisitUnbox(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;

      CheckPointerUse(stat, source, nn, " to unbox");
      nn.AssignNonNull(dest); // result is a reference to unboxed value
      return arg;
    }

    protected override object VisitUnboxAny(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;

      TypeNode strippedType = TypeNode.StripModifiers(type);
      if (!strippedType.IsValueType || (strippedType is ClassParameter || strippedType is TypeParameter)) {
        if (nn.IsNonNull(source))
          nn.AssignNonNull(dest);
        else if (nn.IsNull(source))
          nn.SetToNull(dest);
        else
          nn.AssignMaybeNull(dest);
      }else{
        CheckPointerUse(stat, source, nn, " to unbox");
        nn.AssignNonPointer(dest);
      }
      return arg;
    }

    protected override object VisitIsInstance(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg; 

      if (nn.IsNonNullType(dest.Type)) 
      {
        //System.Console.WriteLine("is instance: dest:{0}, source:{1}", dest, source);
        HandleError(stat, source, Error.CoercionToNonNullTypeMightFail,dest.Type);
        nn.AssignNonNull(dest); // hide further errors
      }
      else 
      {
        nn.AssignIsInstance(dest, source);
      }
      return arg;
    }

    protected override object VisitNewObject(Variable dest, TypeNode type, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg; 

      nn.AssignNonNull(dest);

      return arg;
    }
    protected override object VisitNewArray(Variable dest, TypeNode type, Variable size, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg; 

      nn.AssignNonNull(dest);

      return arg;
    }

    protected override object VisitLoadElement(Variable dest, Variable source, Variable index, TypeNode elementType, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;
    
      Indexer indexer = (Indexer)((AssignmentStatement)stat).Source;

      CheckPointerUse(stat, source, nn, " as array");

      nn.AssignAccordingToType(dest, elementType);
      //nn.AssignMaybeNull(dest); //Until such time, if ever, that we have CLR support in place (or can trust Boogie to know all) it is safer to always assume that an array element may be null.
      // Note the above comment is right. Changed back to AssignAccordingToType. It is an unsafe temp fix to
      // allow the use this type information in Boogie programs. 


      return arg;
    }
    protected override object VisitLoadElementAddress(Variable dest, Variable array, Variable index, TypeNode elementType, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;


      CheckPointerUse(stat, array, nn, " as array");

      nn.AssignNonNull(dest);
      return arg;
    }

    protected override object VisitLoadFieldAddress(Variable dest, Variable source, Field field, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;

      CheckReceiver(stat,source,nn);

      nn.LoadFieldAddress(this.NNChecker, source, field, dest);

      return arg;
    }

    /// <summary>
    /// Perform 2 checks
    /// 1) array is non-null
    /// 2) if array element type is non-null type, then the new value written must be too.
    /// </summary>
    protected override object VisitStoreElement(Variable dest, Variable index, Variable source, TypeNode elementType, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;

      Indexer indexer = (Indexer)((AssignmentStatement)stat).Target;

      CheckPointerUse(stat, dest, nn, " as array");

      // Checking.
      if(ts.IsNonNullType(elementType)) {
        if(nn.IsNull(source))
          HandleError(stat,source,Error.CannotCoerceNullToNonNullType);
        else if(!nn.IsNonNull(source))
          HandleError(stat,source,Error.CoercionToNonNullTypeMightFail, elementType);
      }

      return arg;
    }

    protected override object VisitCatch(Variable dest, TypeNode type, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;
      
      nn.AssignNonNull(dest);
      return arg;
    }

    protected override object VisitThrow(Variable var, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;

      CheckPointerUse(stat, var, nn, " to throw");

      nn.currentException=var.Type;
      if(NNChecker.currBlock.ExceptionHandler==null)
        return arg;

      NNChecker.PushExceptionState(NNChecker.currBlock,nn);
      return null;
    }

    protected override object VisitArgumentList(Variable dest, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;

      nn.AssignNonNull(dest);
      return arg;
    }

    protected override object VisitBreak(Statement stat, object arg) {
      return arg;
    }

    protected override object VisitFilter(Variable dest, Statement stat, object arg) {
      return arg;
    }

    protected override object VisitLoadToken(Variable dest, object token, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;

      nn.AssignNonNull(dest);
      return arg;
    }

    protected override object VisitSwitch(Variable selector, BlockList targets, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;
      foreach (CfgBlock target in NNChecker.currBlock.NormalSuccessors)
        NNChecker.PushState(NNChecker.currBlock,target,nn);
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
      NonNullState nn=(NonNullState)arg;
      NNChecker.PushExceptionState(NNChecker.currBlock,nn);
      return null;
    }
    protected override object VisitEndFilter(Variable code, Statement stat, object arg) {
      return arg;
    }

    //----------------------------------------------
    // for unmanaged pointer manipulations.
    protected override object VisitLoadAddress(Variable dest, Variable source, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;
      nn.Manifest(source);
      nn.LoadAddress(source, dest);
      return arg;
    }
    protected override object VisitStoreIndirect(Variable pointer, Variable source, TypeNode targetType, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;

      // no need to check pointer, since managed code guarantees them not to be null.

      // but we need to check pointer target type.
      Debug.Assert(targetType != null);

      if (nn.IsNonNullType(targetType)) 
      {
        if(nn.IsNull(source))
          HandleError(stat,source,Error.CannotCoerceNullToNonNullType);
        else if (!nn.IsNonNull(source))
        {
          //System.Console.WriteLine("store indirect: source :{0}, pointer:{1}", source, pointer);
          HandleError(stat, source, Error.CoercionToNonNullTypeMightFail, targetType);
        }
      }
      nn.StoreIndirect(source, pointer);
      return arg;
    }

    /// <summary>
    /// Note that the type argument is the element type, or the type of the result of the load.
    /// </summary>
    protected override object VisitLoadIndirect(Variable dest, Variable pointer, TypeNode targetType, Statement stat, object arg) {
      NonNullState nn=(NonNullState)arg;

      // no need to check pointer, since managed code guarantees them not to be null.

      // but we need to check pointer target type.
      bool isNonNull = nn.LoadIndirect(pointer, dest);

      if (!isNonNull) {
        Debug.Assert(targetType != null);

        if (nn.IsNonNullType(targetType)) {
          // Error is emitted by inserted calls to IsNonNullImplicit
          nn.AssumeNonNull(dest);
        }
      }
      return nn;
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
      NonNullState nn=(NonNullState)arg;

      nn.AssignNonNull(dest);
      return arg;
    }

    protected override object VisitRefAnyType(Variable dest, Variable source, Statement stat, object arg)
    {
      NonNullState nn=(NonNullState)arg;

      nn.AssignNonNull(dest);
      return arg;
    }

    protected override object VisitRefAnyValue(Variable dest, Variable source, TypeNode type, Statement stat, object arg)
    {
      NonNullState nn=(NonNullState)arg;
      nn.AssignMaybeNull(dest);
      return arg;
    }


    protected override object VisitInitObj(Variable addr, TypeNode valueType, Statement stat, object arg) {
      return arg;
    }

  }
}

