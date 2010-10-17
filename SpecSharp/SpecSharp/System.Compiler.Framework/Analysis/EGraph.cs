//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------

using System.Diagnostics;
using System;
using System.IO;
using System.Collections;


#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif

  using AbstractValue = MathematicalLattice.Element;

  public abstract class MathematicalLattice {
    /// <summary>
    ///  An element of the lattice.  This class should be derived from in any
    ///  implementation of MathematicalLattice.
    /// </summary>
    public abstract class Element {
      public abstract bool IsBottom { get; }
      public abstract bool IsTop { get; }
    }

    public abstract Element Top { get; }
    public abstract Element Bottom { get; }

    public bool IsTop(Element e) { return e.IsTop; }
    public bool IsBottom(Element e) { return e.IsBottom; }

    /// <summary>
    /// Returns true if a &lt;= this.
    /// </summary>
    protected abstract bool AtMost(Element a, Element b)
      /* The following cases are handled elsewhere and need not be considered in subclass. */
      //  requires a.GetType() == b.GetType();
      //  requires ! a.IsTop;
      //  requires ! a.IsBottom;
      //  requires ! b.IsTop;
      //  requires ! b.IsBottom;
      ;

    // Is 'a' better information than 'b'?
    //
    public bool LowerThanOrEqual(Element a, Element b) {
      if (a.GetType() != b.GetType()) {
        throw new System.InvalidOperationException(
          "operands to <= must be of same Element type"
          );
      }
      if (IsBottom(a)) { return true; }
      if (IsTop(b)) { return true; }
      if (IsTop(a)) { return false; }
      if (IsBottom(b)) { return false; }
      return AtMost(a, b);
    }

    // Is 'a' worse information than 'b'?
    //
    public bool HigherThanOrEqual(Element a, Element b) {
      return LowerThanOrEqual(b, a);
    }

    // Are 'a' and 'b' equivalent?
    //
    public bool Equivalent(Element a, Element b) {
      return LowerThanOrEqual(a, b) && LowerThanOrEqual(b, a);
    }

    public abstract Element NontrivialJoin(Element a, Element b)
      /* The following cases are handled elsewhere and need not be considered in subclass. */
      //  requires a.GetType() == b.GetType();
      //  requires ! a.IsTop;
      //  requires ! a.IsBottom;
      //  requires ! b.IsTop;
      //  requires ! b.IsBottom;
      ;

    public Element Join(Element a, Element b) {
      if (a.GetType() != b.GetType()) {
        throw new System.InvalidOperationException(
          "operands to Join must be of same Lattice.Element type"
          );
      }
      if (IsTop(a)) { return a; }
      if (IsTop(b)) { return b; }
      if (IsBottom(a)) { return b; }
      if (IsBottom(b)) { return a; }
      return NontrivialJoin(a, b);
    }

    public abstract Element NontrivialMeet(Element a, Element b)
      /* The following cases are handled elsewhere and need not be considered in subclass. */
      //  requires a.GetType() == b.GetType();
      //  requires ! a.IsTop;
      //  requires ! a.IsBottom;
      //  requires ! b.IsTop;
      //  requires ! b.IsBottom;
      ;

    public Element Meet(Element a, Element b) {
      if (a.GetType() != b.GetType()) {
        throw new System.InvalidOperationException(
          "operands to Meet must be of same Lattice.Element type"
          );
      }
      if (IsTop(a)) { return b; }
      if (IsTop(b)) { return a; }
      if (IsBottom(a)) { return a; }
      if (IsBottom(b)) { return b; }
      return NontrivialMeet(a, b);
    }

    public virtual Element Widen(Element a, Element b) { return Join(a, b); }

    public virtual void Validate() {
      Debug.Assert(IsTop(Top));
      Debug.Assert(IsBottom(Bottom));
      Debug.Assert(!IsBottom(Top));
      Debug.Assert(!IsTop(Bottom));

      Debug.Assert(LowerThanOrEqual(Top, Top));
      Debug.Assert(LowerThanOrEqual(Bottom, Top));
      Debug.Assert(LowerThanOrEqual(Bottom, Bottom));

      Debug.Assert(IsTop(Join(Top, Top)));
      Debug.Assert(IsBottom(Join(Bottom, Bottom)));
    }
  }


  public interface ISymValue : IUniqueKey {}

  public struct EGraphTerm : IUniqueKey {
    public readonly IUniqueKey Function;
    public readonly ISymValue[] Args;
    int id;
    private static int IdGen = 0;

    public EGraphTerm(IUniqueKey fun, params ISymValue[] args) {
      this.Function = fun;
      this.Args = args;
      this.id = IdGen++;
    }

    public int UniqueId {
      get { return this.id; }
    }
  }

  public interface IEGraph : ICloneable {

    /// <summary>
    /// getter returns sv, such that sv == function(args)
    /// 
    /// setter sets function(args) == value, is equivalent to Eliminate(f, args), followed by
    /// assume (f(args) == value)
    /// </summary>
    /// <param name="function"></param>
    /// <param name="args"></param>
    ISymValue this[IUniqueKey function, params ISymValue[] args] {
      get;
      set;
    }

    /// <summary>
    /// returns sv, such that sv == function(args), or null if not mapped
    /// </summary>
    ISymValue TryLookup(IUniqueKey function, params ISymValue[] args);


    /// <summary>
    /// Assumes v1 == v2
    /// </summary>
    void AssumeEqual(ISymValue v1, ISymValue v2);

    /// <summary>
    /// Returns true if v1 == v2
    /// </summary>
    bool IsEqual(ISymValue v1, ISymValue v2);
    
    /// <summary>
    /// Removes the mapping from the egraph. Semantically equivalent to setting 
    /// the corresponding term to a Fresh symbolic value.
    /// </summary>
    void Eliminate(IUniqueKey function, params ISymValue[] args);

    /// <summary>
    /// Removes all mappings from the egraph of the form g(from).
    /// </summary>
    void EliminateAll(ISymValue from);
    
    ISymValue FreshSymbol();

    /// <summary>
    /// Associates symval with an abstract value.
    /// 
    /// getter returns current association or Top
    /// setter sets current association (forgetting old association)
    /// </summary>
    MathematicalLattice.Element this[ISymValue symval] { get; set; }
    
    /// <summary>
    /// Merge two EGraphs. Result is null if result is no different than this.
    /// </summary>
    /// <param name="isIsomorphicToThis">true, if result is isomorphic to this graph</param>
    IEGraph Join(IEGraph incoming, CfgBlock joinPoint, out bool isIsomorphicToThis);

    /// <summary>
    /// Merge two EGraphs. Result is null if result is no different than this.
    /// MergeInfo provides the mapping of symbolic values in the result to values in the
    /// two incoming branches.
    /// </summary>
    IEGraph Join(IEGraph incoming, CfgBlock joinPoint, out IMergeInfo mergeInfo);


    /// <summary>
    /// return the set of constant function symbols in this egraph
    /// </summary>
    ICollection/*IUniqueKey*/ Constants { get; }

    /// <summary>
    /// return the set of unary function symbols f, such that f(symval) = sv' exists in
    /// the egraph.
    /// </summary>
    /// <param name="symval"></param>
    ICollection/*IUniqueKey*/ Functions(ISymValue symval);

    /// <summary>
    /// Returns the set of defined symbolic values in the egraph that have outgoing edges.
    /// </summary>
    ICollection/*ISymValue*/ SymbolicValues { get; }

    /// <summary>
    /// Return set of equivalent terms to this symbolic value
    /// </summary>
    System.Collections.Generic.IEnumerable<EGraphTerm> EqTerms(ISymValue symval);

    void Dump(TextWriter tw);

    IEGraph Clone(CfgBlock at);

    void EmitTrace(TypeSystem ts, int length, int lineEmitted);
  }


  public interface IMergeInfo {
    ICollection/*<ISymValue>*/ Keys1 { get; }

    ICollection/*<ISymValue>*/ Keys2(ISymValue key1);

    ISymValue this[ISymValue key1, ISymValue key2] { get; }

    bool IsCommon(ISymValue sv);

    /// <summary>
    /// Returns true if result is different from G1
    /// </summary>
    bool Changed { get; }
  }
  

  public class EGraph : IEGraph {

    private class SymbolicValue : ISymValue {

      public int UniqueId { get { return this.uniqueId; } }

      private int uniqueId;
      public SymbolicValue(int id) {
        this.uniqueId = id;
      }

      public override string ToString() {
        return "sv" + this.uniqueId.ToString();
      }

    }

    public ISymValue FreshSymbol() {
      return this.FreshSymbolicValue();
    }

    int idCounter = 0;
    private SymbolicValue FreshSymbolicValue() {
      if (this.IsConstant) { Debug.Assert(false, "modifying a locked down egraph"); }
      SymbolicValue v = new SymbolicValue(++idCounter);
      return v;
    }

    private SymbolicValue constRoot;

    private DoubleFunctionalMap/*<SymbolicValue, IUniqueKey, SymbolicValue>*/ termMap;

    private MathematicalLattice elementLattice;

    private IFunctionalMap/*<SymbolicValue, MathematicalLattice.Element>*/ absMap;

    /// <summary>
    /// Used to represent equalities among symbolic values
    /// </summary>
    private IFunctionalMap/*<SymbolicValue, SymbolicValue>*/ forwMap;

    private DoubleFunctionalMap/*<SymbolicValue, EGraphTerm, Unit*/ eqTermMap;

    private SymbolicValue Find(SymbolicValue v) {
      SymbolicValue result = (SymbolicValue)this.forwMap[v];
      if (result == null) return v;
      return Find(result);
    }

    private SymbolicValue Find(ISymValue v) {
      return  this.Find((SymbolicValue)v);
    }

    bool IsConstant { get { return this.constant; } }
    bool constant; // once we make a copy of this, we set it to true to prevent further updates
    EGraph parent;
    EGraph root;
    int historySize;
    CfgBlock Block;

    FList/*<Update>*/updates;

    public EGraph(MathematicalLattice elementLattice) {
      
      this.elementLattice = elementLattice;
      this.constRoot = FreshSymbolicValue();
      this.termMap = DoubleFunctionalMap.Empty;
      this.absMap = FunctionalMap.Empty;
      this.forwMap = FunctionalMap.Empty;
      this.eqTermMap = DoubleFunctionalMap.Empty;
      this.constant = false;
      this.parent = null;
      this.root = this;
      this.historySize = 1;
      this.updates = null;
    }


    /// <summary>
    /// Copy constructor
    /// </summary>
    /// <param name="from"></param>
    private EGraph(EGraph from, CfgBlock at) {
      this.constRoot = from.constRoot;
      this.termMap = from.termMap;
      this.idCounter = from.idCounter;
      this.absMap = from.absMap;
      this.elementLattice = from.elementLattice;
      this.forwMap = from.forwMap;
      this.eqTermMap = from.eqTermMap;
      
      // keep history
      this.updates = from.updates;
      this.parent = from;
      this.root = from.root;
      this.historySize = from.historySize+1;
      this.Block = at;

      // set from to constant
      from.constant = true;
    }

    private int LastSymbolId {
      get {
        Debug.Assert(this.constant, "LastSymbolId only makes sense on locked down egraphs");
        return this.idCounter;
      }
    }

    private bool IsOldSymbol(SymbolicValue sv) {
      EGraph parent = this.parent;
      if (parent == null) return false;
      return (sv.UniqueId <= parent.LastSymbolId);
    }

    private void AddEdgeUpdate(SymbolicValue from, IUniqueKey function) {
      if (IsOldSymbol(from)) {
        AddUpdate(new MergeState.EdgeUpdate(from, function));
      }
    }

    private void AddAValUpdate(SymbolicValue sv) {
      if (IsOldSymbol(sv)) {
        AddUpdate(new MergeState.AValUpdate(sv));
      }
    }

    private void AddEqualityUpdate(SymbolicValue sv1, SymbolicValue sv2) {
      if (IsOldSymbol(sv1) && IsOldSymbol(sv2)) {
        AddUpdate(new MergeState.EqualityUpdate(sv1, sv2));
      }
    }

    private void AddEliminateEdgeUpdate(SymbolicValue from, IUniqueKey function) {
      if (IsOldSymbol(from)) {
        AddUpdate(new MergeState.EliminateEdgeUpdate(from, function));
      }
    }

    private void AddEliminateAllUpdate(SymbolicValue from)
    {
      if (IsOldSymbol(from)) {
        foreach (IUniqueKey function in this.termMap.Keys2(from)) {
          AddUpdate(new MergeState.EliminateEdgeUpdate(from, function));
        }
      }
    }

    private void AddUpdate(Update upd) {
      Debug.Assert(!this.IsConstant, "modification of locked down egraph");
      this.updates = FList.Cons(upd, this.updates);
    }

    private abstract class Update {
      /// <summary>
      /// Replay update on merge state
      /// </summary>
      public abstract void Replay(MergeState merge);
    
      Update next; // during reversal

      public static Update Reverse(FList updates, FList common) {
        Update oldest = null;
        while (updates != common) {
          Update current = (Update)updates.Head;
          current.next = oldest;
          oldest = current;
          updates = updates.Tail;
        }
        return oldest;
      }

      public Update Next {
        get {
          Update next = this.next;
          this.next = null;
          return next;
        }
      }
    }

    public AbstractValue this[ISymValue sym] {
      get {
        sym = Find(sym);
        AbstractValue v = (AbstractValue)this.absMap[sym];
        if (v == null) { v = this.elementLattice.Top; }
        return v;
      }

      set {
        SymbolicValue sv = Find(sym);
        AbstractValue old = this[sym];
        if (old != value) {
          AddAValUpdate(sv);
          if (this.elementLattice.IsTop(value)) {
            this.absMap = this.absMap.Remove(sv);
          }
          else {
            this.absMap = this.absMap.Add(sv, value);
          }
        }
      }
    }

    public ISymValue TryLookup(IUniqueKey function, params ISymValue[] args) 
    {
      if (args.Length == 0) 
      {
        return LookupWithoutManifesting(this.constRoot, function);
      }
      if (args.Length == 1) 
      {
        return LookupWithoutManifesting((SymbolicValue)args[0], function);
      }
      Debug.Assert(false, "EGraph currently only implements unary and nullary function terms");
      return null;
    }

    private SymbolicValue LookupWithoutManifesting(SymbolicValue arg, IUniqueKey function) {

      arg = Find(arg);
      SymbolicValue v = (SymbolicValue)this.termMap[arg, function];
      if (v == null) return v;
      return Find(v);
    }

    private SymbolicValue this[SymbolicValue arg, IUniqueKey function] {
      get {
        arg = Find(arg);
        SymbolicValue v = (SymbolicValue)this.termMap[arg, function];

        if (v == null) {
          v = FreshSymbolicValue();
          this.termMap = this.termMap.Add(arg, function, v);
          this.eqTermMap = this.eqTermMap.Add(v, new EGraphTerm(function, arg), null);
          this.AddEdgeUpdate(arg, function);
        }
        else {
          v = Find(v);
        }
        return v;
      }

      set {
        arg = Find(arg);
        value = Find(value);
        this.termMap = this.termMap.Add(arg, function, value);
        this.eqTermMap = this.eqTermMap.Add(value, new EGraphTerm(function, arg), null);
        this.AddEdgeUpdate(arg, function);
      }
    }


    public ISymValue this[IUniqueKey function, params ISymValue[] args] {
      get {
        if (args.Length == 0) {
          return this[this.constRoot, function];
        }
        if (args.Length == 1) {
          return this[(SymbolicValue)args[0], function];
        }
        Debug.Assert(false, "EGraph currently only implements unary and nullary function terms");
        return null;
      }

      set {
        if (args.Length == 0) {
          this[this.constRoot, function] = (SymbolicValue)value;
          return;
        }
        if (args.Length == 1) {
          this[(SymbolicValue)args[0], function] = (SymbolicValue)value;
          return;
        }
        Debug.Assert(false, "EGraph currently only implements unary and nullary function terms");
      }
    }

    public void Eliminate(IUniqueKey function, params ISymValue[] args) {
      if (args.Length == 0) {
        this.termMap = this.termMap.Remove(this.constRoot, function);
        this.AddEliminateEdgeUpdate(this.constRoot, function);
        return;
      }
      if (args.Length == 1) {
        SymbolicValue sv = Find(args[0]);
        this.termMap = this.termMap.Remove(sv, function);
        this.AddEliminateEdgeUpdate(sv, function);
        return;
      }
      Debug.Assert(false, "EGraph currently only implements unary and nullary function terms");
    }

    public void EliminateAll(ISymValue arg) {
      SymbolicValue sv = Find(arg);
      this.AddEliminateAllUpdate(sv); // must be before RemoveAll, as it reads termMap
      this.termMap = this.termMap.RemoveAll(sv);
    }

    private struct EqPair{
      public readonly SymbolicValue v1;
      public readonly SymbolicValue v2;

      public EqPair(SymbolicValue v1, SymbolicValue v2) {
        this.v1 = v1;
        this.v2 = v2;
      }
    }

    private void PushEquality(WorkList wl, SymbolicValue v1, SymbolicValue v2) {
      if (v1 == v2) return;
      wl.Add(new EqPair(v1, v2));
    }


    public void AssumeEqual(ISymValue v1, ISymValue v2) {

      WorkList wl = new WorkList();
      SymbolicValue v1rep = Find(v1);
      SymbolicValue v2rep = Find(v2);
      PushEquality(wl, v1rep, v2rep);

      if (!wl.IsEmpty()) {
        // TODO: there's an opportunity for optimizing the number
        // of necessary updates that we need to record, since the induced
        // updates of the equality may end up as duplicates.
        AddEqualityUpdate(v1rep, v2rep);
      }
      DrainEqualityWorkList(wl);
    }

    private void DrainEqualityWorkList(WorkList wl) {
      while ( ! wl.IsEmpty() ) {

        EqPair eqpair = (EqPair)wl.Pull();
        SymbolicValue v1rep = Find(eqpair.v1);
        SymbolicValue v2rep = Find(eqpair.v2);
        if (v1rep == v2rep) continue;

        // always map new to older var
        if (v1rep.UniqueId < v2rep.UniqueId) {
          SymbolicValue temp = v1rep;
          v1rep = v2rep;
          v2rep = temp;
        }

        // perform congruence closure here:
        foreach(IUniqueKey f in this.Functions(v1rep)) {
          SymbolicValue target = this.LookupWithoutManifesting(v2rep, f);
          if (target == null) {
            this[v2rep, f] = this[v1rep,f];
          }
          else {
            PushEquality(wl, this[v1rep,f], target);
          }
        }
        MathematicalLattice.Element av1 = this[v1rep];
        MathematicalLattice.Element av2 = this[v2rep];
        // merge term map of v1 into v2
        foreach(IUniqueKey eterm in this.eqTermMap.Keys2(v1rep)) {
          this.eqTermMap = this.eqTermMap.Add(v2rep, eterm, null);
        }
        this.forwMap = this.forwMap.Add(v1rep, v2rep);
        this[v2rep] = this.elementLattice.Meet(av1,av2);
      }
    }


    public bool IsEqual(ISymValue v1, ISymValue v2) {
      return  (Find(v1) == Find(v2));
    }

    public ICollection Constants {
      get { return this.termMap.Keys2(this.constRoot); }
    }


    public ICollection Functions(ISymValue sv) {
      return this.termMap.Keys2(Find(sv));
    }

    public ICollection SymbolicValues {
      get { return this.termMap.Keys1; }
    }

    public System.Collections.Generic.IEnumerable<EGraphTerm> EqTerms(ISymValue sv) {
      foreach (EGraphTerm eterm in this.eqTermMap.Keys2(Find(sv))) {
        // test if it is valid
        if (this.TryLookup(eterm.Function, eterm.Args) == sv) {
          yield return eterm;
        }
      }
    }


    public IEGraph Clone(CfgBlock at) {
      return new EGraph(this, at);
    }

    public object Clone() {
      return new EGraph(this, null);
    }

    #region Join on EGraphs


    public IEGraph Join(IEGraph g2, CfgBlock joinPoint, out bool resultIsomorphicToThis) {
      IMergeInfo minfo;
      IEGraph result = Join(g2, joinPoint, out minfo);
      resultIsomorphicToThis = !minfo.Changed;
      return result;
    }

    public IEGraph Join(IEGraph g2, CfgBlock joinPoint, out IMergeInfo mergeInfo) {
      EGraph eg1 = this;
      EGraph eg2 = (EGraph)g2;

      int updateSize;
      EGraph common = ComputeCommonTail(eg1, eg2, out updateSize);

      EGraph result;
      bool doReplay = true;

      if (common == null) {
        doReplay = false;
        result = new EGraph(eg1.elementLattice);
        result.Block = joinPoint;
      }
      else {
        result = new EGraph(common, joinPoint);
      }

      if (Analyzer.Debug) {
        Console.WriteLine("Last common symbol: {0}", common.idCounter);
      }
      if (Analyzer.Statistics) {
        Console.WriteLine("G1:{0} G2:{1} Tail:{2} UpdateSize:{3}", eg1.historySize, eg2.historySize, result.historySize, updateSize);
      }

      MergeState ms = new MergeState(result, eg1, eg2);

      // Heuristic for using Replay vs. full update
      doReplay &= (common != eg1.root);
      doReplay &= (eg1.historySize > 3);
      doReplay &= (eg2.historySize > 3);

      if (doReplay) {
        ms.Replay(common);
      }
      else {
        ms.AddMapping(eg1.constRoot, eg2.constRoot, result.constRoot);
        ms.JoinSymbolicValue(eg1.constRoot, eg2.constRoot, result.constRoot);
      }
      mergeInfo = ms;
      return result;
    }

    private EGraph ComputeCommonTail(EGraph g1, EGraph g2, out int updateSize) {

      EGraph current1 = g1;
      EGraph current2 = g2;
      if (g1.historySize <= 3 && g2.historySize > 100) { 
        updateSize = g1.historySize + g2.historySize;
        if (g1.root == g2.root) {
          return g1.root; 
        }
        return null;
      }
      if (g2.historySize <= 3 && g1.historySize > 100) { 
        updateSize = g1.historySize + g2.historySize;
        if (g1.root == g2.root) {
          return g1.root; 
        }
        return null;
      }

      while (current1 != current2) {
        if (current1 == null) {
          // no common tail
          current2 = null; break;
        }
        if (current2 == null) {
          // no common tail
          current1 = null; break;
        }
        if (current1.historySize > current2.historySize) {
          current1 = current1.parent; continue;
        }
        if (current2.historySize > current1.historySize) {
          current2 = current2.parent; continue;
        }
        // they have equal size
        current1 = current1.parent;
        current2 = current2.parent;
      }
      // now current1 == current2 == tail
      EGraph tail = current1;
      int tailSize = (tail != null)?tail.historySize:0;
      updateSize = g1.historySize + g2.historySize - tailSize - tailSize;
      return tail;
    }

    private class MergeState : IMergeInfo {

      public readonly EGraph Result;
      public readonly EGraph G1;
      public readonly EGraph G2;
      private DoubleFunctionalMap Map;
      private bool changed;
      public bool Changed { get { return this.changed; } }

      int lastCommonVariable;

      public bool IsCommon(ISymValue sv) {
        return (sv.UniqueId <= lastCommonVariable);
      }

      public MergeState(EGraph result, EGraph g1, EGraph g2) {
        this.Result = result;
        this.G1 = g1;
        this.G2 = g2;
        this.Map = DoubleFunctionalMap.Empty;
        this.changed = false;
        // capture the idCounter before we update the result structure.
        this.lastCommonVariable = result.idCounter;
      }

      public void JoinSymbolicValue(SymbolicValue v1, SymbolicValue v2, SymbolicValue r) {
        if (Analyzer.Debug) {
          Console.WriteLine("JoinSymbolicValue: [{0},{1}] -> {2}", v1, v2, r);
        }
        IEnumerable keys;
        if (G1.termMap.Keys2Count(v1) <= G2.termMap.Keys2Count(v2)) {
          keys = G1.termMap.Keys2(v1);
        }
        else {
          keys = G2.termMap.Keys2(v2);
          this.changed = true; // since we have fewer keys in output
        }
        foreach (IUniqueKey function in keys) {
          SymbolicValue v1target = G1.LookupWithoutManifesting(v1,function);
          SymbolicValue v2target = G2.LookupWithoutManifesting(v2,function);

          if (v1target == null) {
            // no change in output over G1
            continue;
          }
          if (v2target == null) {
            // absence considered Top.
            this.changed |= !(G1.elementLattice.IsTop(G1[v1target]));
            continue;
          }
        
          SymbolicValue rtarget = AddJointEdge(v1target, v2target, function, r);

          if (rtarget != null) { JoinSymbolicValue(v1target, v2target, rtarget); }
        }
      }

      private SymbolicValue AddJointEdge(SymbolicValue v1target, SymbolicValue v2target, IUniqueKey function, SymbolicValue resultRoot) {
        SymbolicValue rtarget = (SymbolicValue)Map[v1target, v2target];
        bool newBinding = false;
        if (rtarget == null) {
          // if we have visited v1target before, then the result graph is not isomorphic to G1
          if (Map.ContainsKey1(v1target) || IsCommon(v1target) && v1target != v2target) { this.changed = true; }
          newBinding = true;
          if (v1target.UniqueId <= lastCommonVariable && v1target == v2target) {
            rtarget = v1target; // reuse old symbol
          }
          else {
            rtarget = Result.FreshSymbolicValue();
          }
          this.Map = this.Map.Add(v1target,v2target,rtarget);
        }
        else {
          // See if info is already present
          SymbolicValue oldTarget = Result.LookupWithoutManifesting(resultRoot, function);
          if (oldTarget == rtarget) {
            // no change, don't record or change anything
            return null;
          }
        }
        Result[resultRoot, function] = rtarget;

        AbstractValue aval1 = G1[v1target];
        AbstractValue aval2 = G2[v2target];
        AbstractValue aresult = G1.elementLattice.Join(aval1, aval2);
        Result[rtarget] = aresult;
        if ( ! G1.elementLattice.LowerThanOrEqual(aresult, aval1)) { this.changed = true; }

        if (Analyzer.Debug) {
          Console.WriteLine("AddJointEdge: {0} -{1}-> [{2},{3},{4}]",
            resultRoot, EGraph.Function2String(function), v1target, v2target, rtarget); 
        }
        return (newBinding)?rtarget:null;
      }

      public void AddMapping(SymbolicValue v1, SymbolicValue v2, SymbolicValue result) {
        this.Map = this.Map.Add(v1, v2, result);
      }


      public void Replay(EGraph common) {
        Replay(this.G1.updates, common.updates);
        Replay(this.G2.updates, common.updates);
      }
      
      private void Replay(FList updates, FList common) {
        // First reverse updates.
        Update oldest = Update.Reverse(updates, common);
        while (oldest != null) {
          oldest.Replay(this);
          oldest = oldest.Next;
        }
      }


      #region IMergeInfo members

      ICollection IMergeInfo.Keys1 { get { return this.Map.Keys1; } }

      ICollection IMergeInfo.Keys2(ISymValue key1) { return this.Map.Keys2(key1); }

      ISymValue IMergeInfo.this[ISymValue key1, ISymValue key2] {
        get {
          return (ISymValue)this.Map[key1, key2];
        }
      }
      #endregion


      #region Updates
      public class EdgeUpdate : Update {
        readonly SymbolicValue from;
        readonly IUniqueKey function;

        public EdgeUpdate(SymbolicValue from, IUniqueKey function) {
          this.from = from;
          this.function = function;
        }

        public override void Replay(MergeState merge) {
          if (!merge.IsCommon(from)) return;

          SymbolicValue v1target = merge.G1.LookupWithoutManifesting(from,function);
          SymbolicValue v2target = merge.G2.LookupWithoutManifesting(from,function);

          if (v1target == null) {
            // no longer in G1
            return;
          }
          if (v2target == null) {
            // no longer in G2
            merge.changed = true; // no longer in result.
            return;
          }
        
          SymbolicValue rtarget = merge.AddJointEdge(v1target, v2target, function, from);

          if (rtarget != null && rtarget.UniqueId > merge.lastCommonVariable) {
            merge.JoinSymbolicValue(v1target, v2target, rtarget); 
          }
        }
      }

      public class AValUpdate : Update {
        readonly SymbolicValue sv;

        public AValUpdate(SymbolicValue sv) {
          this.sv = sv;
        }

        public override void Replay(MergeState merge) {
          if (!merge.IsCommon(this.sv)) return;

          AbstractValue av1 = merge.G1[this.sv];
          AbstractValue av2 = merge.G2[this.sv];

          AbstractValue old = merge.Result[this.sv];

          AbstractValue join = merge.Result.elementLattice.Join(av1, av2);

          if (join != av1 && merge.Result.elementLattice.LowerThanOrEqual(av1, join)) {
            merge.changed = true;
          }
          if (join != old) {
            merge.Result[this.sv] = join;
          }
        }
      }

      public class EqualityUpdate : Update {
        readonly SymbolicValue sv1;
        readonly SymbolicValue sv2;

        public EqualityUpdate(SymbolicValue sv1, SymbolicValue sv2) {
          this.sv1 = sv1;
          this.sv2 = sv2;
        }

        public override void Replay(MergeState merge) {
          if (!merge.IsCommon(this.sv1)) return;
          if (!merge.IsCommon(this.sv2)) return;

          if (merge.G1.IsEqual(this.sv1, this.sv2)) {
            if (merge.Result.IsEqual(this.sv1, this.sv2)) {
              // already present
              return;
            }
            if (merge.G2.IsEqual(this.sv1, this.sv2)) {
              // add equality
              merge.Result.AssumeEqual(this.sv1, this.sv2);
            }
            else {
              // Changed vs G1 (since not present in output)
              merge.changed = true;
            }
          }
        }
      }

      public class EliminateEdgeUpdate : Update {
        readonly SymbolicValue from;
        readonly IUniqueKey function;

        public EliminateEdgeUpdate(SymbolicValue from, IUniqueKey function) {
          this.from = from;
          this.function = function;
        }

        public override void Replay(MergeState merge) {
          if (!merge.IsCommon(this.from)) return;
          SymbolicValue v1target = merge.G1.LookupWithoutManifesting(this.from, this.function);
          SymbolicValue v2target = merge.G2.LookupWithoutManifesting(this.from, this.function);

          if (v1target != null && v2target != null) { 
            // outdated
            return;
          }

          if (v1target != null) { 
            merge.changed = true;
          }
          SymbolicValue rtarget = merge.Result.LookupWithoutManifesting(this.from, this.function);
          if (rtarget == null) {
            // redundant
            return;
          }
          merge.Result.Eliminate(this.function, this.from);
        }
      }

      #endregion
    }  


    #endregion

    public void EmitTrace(TypeSystem ts, int length, int lineEmitted) {

      EGraph current = this;
      while (current != null && length > 0) {
        if (current.Block != null) {
          Node b = current.Block.EndSourceContext();
          int line = b.SourceContext.EndLine;
          if (line != 0 && line != lineEmitted) {
            ts.HandleError(b, Error.RelatedErrorLocation);
            lineEmitted = line;
            length--;
          }
        }
        current = current.parent;
      }
    }

    public void Dump(TextWriter tw) {
      HashSet seen = new HashSet();
      WorkList wl = new WorkList();
    
      Console.WriteLine("LastSymbolId:{0}", this.idCounter);
      foreach(IUniqueKey function in this.termMap.Keys2(this.constRoot)) {
        SymbolicValue target = this[this.constRoot, function];

        tw.WriteLine("{0} = {1}", Function2String(function), target);

        wl.Add(target);
      }

      while ( ! wl.IsEmpty() ) {
        SymbolicValue v = (SymbolicValue)wl.Pull();
        if ( ! seen.Add(v)) continue;

        foreach(IUniqueKey function in this.termMap.Keys2(v)) {
          SymbolicValue target = this[v, function];
          tw.WriteLine("{0}({2}) = {1}", Function2String(function), target, v);

          wl.Add(target);
        }

      }

      tw.WriteLine("**Abstract value map");
      foreach (SymbolicValue v in seen) {
        AbstractValue aval = this[v];
        if (!this.elementLattice.IsTop(aval)) {
          tw.WriteLine("{0} -> {1}", v, aval);
        }
      }
    }

    public static string Function2String(IUniqueKey function) 
    {
      if (function is Variable) 
      {
        Variable v = (Variable)function;
        Identifier name = v.Name;
        string nstr = (name == null)?"":name.Name;
        return String.Format("{0}({1})", nstr, function.UniqueId);
      }
      if (function is Field) 
      {
        Field f = (Field)function;
        Identifier name = f.Name;
        return (name == null)?"":name.Name;
      }
      return function.ToString();
    }
  }
}
