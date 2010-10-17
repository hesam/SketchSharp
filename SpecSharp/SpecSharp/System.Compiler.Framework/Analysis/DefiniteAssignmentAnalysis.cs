//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
//////////////////////////////////////////////////////////////////////////////////////////////
//
//  Definite assignment analysis
//  ----------------------------
//
//  1) Checks C# definite assignment rules
//  2) Checks Spec# stronger definite assignment and usage rules
//  3) Guarantees that non-null fields are initialized in constructors at the appropriate time
//  4) Checks delayed reference semantics for circular structure initializations.
//
//
//
//////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  using Microsoft.Contracts;

  using AbstractValue = MathematicalLattice.Element;

  public interface IDelayState
  {
    bool IsDelayed(Variable v);
  }

  /// <summary>
  /// Exposes computed existential delay information for a method
  /// </summary>
  /// 
  public interface IDelayInfo
  {
    IDelayState WhenAccessLocation(Statement position);
    int Count ();
  }

  public class ExSet:IDelayState
  {
    private ArraySet s;
    public ExSet()
    {
      s = new ArraySet();
    }

    public void Add(Variable v)
    {
      s.Add(v);
    }

    public bool IsDelayed(Variable v)
    {
      return s.Contains(v);
    }

    public void printAll()
    {
      IEnumerator ie = s.GetEnumerator();
      while (ie.MoveNext())
      {
        object o = ie.Current;
        if (o is LocalBinding) {
          System.Console.WriteLine(" --- local binding: {0}", (o as LocalBinding).BoundLocal.Name);
        }
        else {
          System.Console.WriteLine(" --- {0}", ie.Current as Variable);
        }
      }
    }
  }

  internal class NodeMaybeExistentialDelayInfo : IDelayInfo 
  {
    Hashtable table = new Hashtable();

    public IDelayState WhenAccessLocation(Statement pos)
    {
      ExSet result;

      try
      {
        result = (ExSet)table[pos.UniqueKey];
      }
      catch (Exception)
      {
        result = null;
      }

      return result;
    }

    public void Add(Statement pos, ExSet existentials)
    {
      //System.Console.WriteLine("Adding existentials at position:{0}:{1}", pos, pos.GetHashCode());
      //if (pos != null) System.Console.WriteLine("  (line: {0}, column {1})", pos.SourceContext.StartLine, pos.SourceContext.StartColumn);
      //existentials.printAll();
      if (pos == null) return;
      //if (pos.SourceContext == null) return;
      if (pos.UniqueKey == 0) return;
      if (!table.ContainsKey(pos.UniqueKey))
          table.Add(pos.UniqueKey, existentials);
    }

    public int Count()
    {
      return table.Count; 
    }
  }

  /// <summary>
  /// Encapsulation for the variable initialization state.
  /// The state is an equality graph between locations (for tracking refs) and
  /// each location is mapped to a two point lattice (top unassigned) (bot assigned).
  ///   
  /// For References, we keep track of the assignment status of the contents of the location
  /// by mapping terms of the form
  /// 
  ///    ValueOf(s) to assignment lattice elements as well.
  ///    
  /// For structs, we additionally keep track of field individual assignment status by tracking
  /// 
  ///    Field(Value(s))
  ///    
  /// In addition, we keep a single set (not program point specific) of
  /// variables that were assigned, and one set of variables that were 
  /// referenced. If we find variables at the end that were assigned, but not
  /// referenced, we issue the C# warning.
  /// </summary>
  internal class InitializedVariables : IDataFlowState{
    Analyzer analyzer;

    enum RStatus {
      Assigned,
      Referenced
    }

    /// <summary>
    /// Due to generics, we see different fields for the same field
    /// </summary>
    public static TypeNode Canonical(TypeNode t) {
      if (t == null) return null;
      while (t.Template != null) {
        t = t.Template;
      }
      return t;
    }
    private Field CanonicalField(Field f) {
      TypeNode current = f.DeclaringType;
      if (current.Template == null) return f;

      current = Canonical(current);
      return this.analyzer.GetTypeView(current).GetField(f.Name);
    }

    private IMap/*Variable,RStatus*/ referenceStatus;

    private IEGraph egraph;
   
    /// <summary>
    /// Copy Constructor
    /// </summary>
    /// <param name="old"></param>
    public InitializedVariables(InitializedVariables old) {
      this.analyzer = old.analyzer;
      this.egraph = (IEGraph)old.egraph.Clone();
      this.referenceStatus = old.referenceStatus;
    }

    public static Identifier ValueOf = Identifier.For("Value");
    public static Identifier Base = Identifier.For(":base:"); // Make sure it's not a valid identifier.

    public ISymValue Value(Variable v) {
      ISymValue addr = this.egraph[v];
      return this.egraph[ValueOf, addr];
    }
    private ISymValue Address(Variable v) {
      return this.egraph[v];
    }
    public bool IsEqual(Variable v1, Variable v2){
      return this.egraph.IsEqual(this.egraph[ValueOf, this.egraph[v1]], this.egraph[ValueOf, this.egraph[v2]]);
    }

    /// <summary>
    /// Use only for non-struct values.
    /// </summary>
    private bool IsAssigned(ISymValue sv) {
      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[sv];

      if (aval.Assigned) return true;

      return false;
    }

    /// <summary>
    /// Returns null if all fields of the struct are fully assigned, otherwise the field that is not.
    /// </summary>
    private Field IsAssigned(ISymValue sv, Struct structType) {
      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[sv];

      if (aval.Assigned) return null;

      Debug.Assert(structType != null);

      // check if all fields are assigned.
      MemberList ml = this.analyzer.GetTypeView(structType).Members;
      if (ml == null) return null;
      for (int i = 0; i < ml.Count; i++) {
        Field f = ml[i] as Field;
        if (f == null) continue;
        if (f.IsLiteral || f.IsStatic) continue;

        if ( ! IsAssignedField(sv, f)) return f;
      }
      // all fields are assigned. remember that
      this.egraph[sv] = aval.SameButAssigned;
      return null;
    }

    public bool IsAssigned(Variable v) {
      ISymValue sv = this.egraph[v];

      Struct s = v.Type as Struct;
      if (s != null) {
        return IsAssigned(sv, s) == null;
      }
      else {
        return IsAssigned(sv);
      }
    }

    public bool IsBaseAssigned() {
      ISymValue sv = this.egraph[InitializedVariables.Base];
      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[sv];
      return aval.Assigned;
    }

    public bool IsBaseUnassigned() {
      ISymValue sv = this.egraph[InitializedVariables.Base];
      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[sv];
      return aval.Unassigned;
    }

    public bool IsUnassigned(Field f) {
      f = CanonicalField(f);
      ISymValue sv = this.egraph[f];
      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[sv];
      return aval.Unassigned;
    }


    private bool IsAssignedField(ISymValue svderef, Field f) {
      f = CanonicalField(f);
      ISymValue svfield = this.egraph[f, svderef];

      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[svfield];
      if (aval.Assigned) return true;
  
      Struct s = f.Type as Struct;
      if (s == null || s.IsPrimitive) return false;
      return IsAssigned(svfield, s) == null;
    }

    public bool IsAssignedStructField(Variable v, Field f) {
      ISymValue sv = this.egraph[v];

      ISymValue svderef = this.egraph[ValueOf, sv];

      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[svderef];

      if (aval.Assigned) return true; // all fields are assigned

      return IsAssignedField(svderef, f);
    }
    public bool IsAssignedClassField(Variable v, Field f) {
      ISymValue sv = this.egraph[v];
      ISymValue svderef = this.egraph[ValueOf, sv];
      return IsAssignedField(svderef, f);
    }

    /// <summary>
    /// </summary>
    /// <param name="structType">null if no struct type</param>
    public bool IsAssignedRef(Variable v, Struct structType) {

      ISymValue sv = this.egraph[v];

      ISymValue svderef = this.egraph[ValueOf, sv];

      if (structType == null) {
        return IsAssigned(svderef);
      }

      return IsAssigned(svderef, structType) == null;

    }

    /// <summary>
    /// Like IsAssignedRef, but returns null if true, and a witness field if false.
    /// </summary>
    /// <param name="structType">Non-null struct type of variable</param>
    public Field NonAssignedRefField(Variable v, Struct structType) {

      ISymValue sv = this.egraph[v];

      ISymValue svderef = this.egraph[ValueOf, sv];

      return IsAssigned(svderef, structType);

    }

    /// <summary>
    /// Returns a canonically sorted list of non-temp variables.
    /// </summary>
    public IWorkList/*Variable*/ Variables { 
      get { 
        PriorityQueue q = new PriorityQueue(new Compare(VariableSourceLocComparer));

        foreach(Variable v in this.referenceStatus.Keys) {
          if (isTemp(v)) continue;
          q.Add(v);
        }
        return q;
      } 
    }

    // existential delay
    // in collecting delayed info, we keep track of a set of program and stack 
    // variables that are known to be existentially delayed
    private IWorkList ProgramAndStackVariables
    {
      get
      {
        PriorityQueue q = new PriorityQueue(new Compare(VariableSourceLocComparer));

        foreach (Variable v in this.referenceStatus.Keys)
        {
          if (isTemp(v) && !(v is StackVariable)) continue;
          q.Add(v);
        }
        return q;
      }
    }
    // end existential

    private int VariableSourceLocComparer(object o1, object o2) {
      Variable v1 = o1 as Variable;
      Variable v2 = o2 as Variable;
      if (o1 == null || o2 == null) return 0;
      
      int result = v1.SourceContext.StartLine - v2.SourceContext.StartLine;
      if (result == 0) {
        result = v1.UniqueKey - v2.UniqueKey;
      }
      return -result;
    }

    private static bool isTemp(Variable v){
      if (
        v.SourceContext.Document == null ||
        v.Name == null ||
        v.Name.Name=="" ||
        v.Name.Name.StartsWith("$finally") ||
        v.Name.Name.StartsWith("Closure Class Local") ||
        v.Name.Name.Equals("Display Return Local") ||
        v.Name.Name.Equals("ContractMarker") ||
        v is StackVariable 
        )
        return true;
      return false;
    }

    public void EmitAssignedButNotReferencedErrors(TypeSystem ts) {
      IWorkList wl = this.Variables;
      while ( !wl.IsEmpty()) {
        Variable v = (Variable)wl.Pull();
        if (v is Local && v.Name != StandardIds.NewObj && ((RStatus)this.referenceStatus[v]) == RStatus.Assigned) {
          ts.HandleError(v,Error.UnreferencedVarAssg,v.Name.Name);
        }
      }
    }

    public void SetReferencedStatus(Variable v) {
      this.referenceStatus[v] = RStatus.Referenced;
    }

    public void SetReferencedAfterError(Variable v) {
      SetReferenced(v);
      SetValueNonDelayed(v);
    }

    public void SetReferenced(Variable v) {
      if (v == null) return;
      SetReferencedStatus(v);
      // also set assigned to avoid followup errors.
      ISymValue sv = this.egraph[v];
      this.egraph[sv] = DefAssignLattice.AVal.AssignedNoDelay; // okay, as variables cannot be delayed locations
    }

    private void SetAssignedStatus(Variable v) {
      if ( ! this.referenceStatus.Contains(v)) {
        this.referenceStatus[v] = RStatus.Assigned;
      }
    }
    
    private void SetAssigned(Variable v, DefAssignLattice.AVal aval) {
      if (v == null) return;

      SetAssignedStatus(v);

      ISymValue sv = this.egraph[v];

      // assignment makes value unknown
      this.egraph.Eliminate(ValueOf, sv);

      this.egraph[sv] = aval;
    }

    public void SetLocationAssigned(Variable v) {
      SetAssigned(v, DefAssignLattice.AVal.AssignedNoDelay);
    }

    public void SetUniversallyDelayed(Variable v, bool outParam) {
      if (v.Type.IsValueType) return;
      
      ISymValue value = Value(v);
      DefAssignLattice.AVal aval = outParam ? DefAssignLattice.AVal.UnassignedUniversalDelay : DefAssignLattice.AVal.AssignedUniversalDelay;
      this.egraph[value] = aval;
    }
   
    // delay type
    // this property gathers the existential delayed (program and stack) variables whose
    // existential delay status may reach this point by checking whether their delay value
    // is TOP
    // 
    public ExSet  MayHaveExistentialDelayedVars {
      get {
        IWorkList vars = this.ProgramAndStackVariables;
        ExSet result = new ExSet();

        while (!vars.IsEmpty ()) {
          Variable var = (Variable) vars.Pull();
          if (this.IsExistentialDelayed (var)) {
            if (Analyzer.Debug)
            {
              System.Console.WriteLine("   --- found existential delay for node: {0}", var);
            };
            result.Add(var);
          }
        }
        return result;
      }
    }
    // end existential delay

    private void SetDelay(ISymValue sv, DefAssignLattice.Delay delay) {
      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[sv];
      this.egraph[sv] = aval.SameButWith(delay);
    }

    public void SetValueNonDelayed(Variable v) {
      if (v.Type.IsValueType)
      {
        return;
      }
      ISymValue value = Value(v);
      SetDelay(value, DefAssignLattice.Delay.NoDelay);
      // System.Console.WriteLine("Delay of {0} is set to {1}", v, this.egraph[value]);
    }
    public void SetValueExistentialDelayed(Variable v) {
      if (v.Type.IsValueType) {
        return;
      }
      ISymValue value = Value(v);
      SetDelay(value, DefAssignLattice.Delay.ExistentialDelay);
      // System.Console.WriteLine("Delay of {0} is set to {1}", v, this.egraph[value]);
    }
    public void SetValueAssignedAndNonDelayed(Variable v) {
      if (v.Type.IsValueType) return;
      ISymValue value = Value(v);
      this.egraph[value] = DefAssignLattice.AVal.AssignedNoDelay;
    }

    public void SetValueAssignedAndBottomDelayed(Variable v) {
      if (v.Type.IsValueType) return;
      ISymValue value = Value(v);
      this.egraph[value] = DefAssignLattice.AVal.Bottom;
    }

    public void SetBottomDelayed(Variable v) {
      ISymValue value = Value(v);
      this.egraph[value] = DefAssignLattice.AVal.Bottom;
    }

    public void SetBottomDelayedClassField(Variable v, Field f) {
      f = CanonicalField(f);
      ISymValue sv = this.egraph[ValueOf, this.egraph[v]];
      ISymValue svfield = this.egraph[f, sv];
      this.egraph[svfield] = DefAssignLattice.AVal.Bottom;
      return;
    }

    public bool IsNotDelayedReference(Variable v) {
      if (v.Type.IsValueType) return true;
      ISymValue value = Value(v);
      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[value];
      return aval.IsNotDelayed;
    }

    public bool TargetDelayLongerThanSource(Variable pointer, Variable source) {
      // pointer == null means static field
      if (pointer == null) {
        return CanAssumeNotDelayed(source);
      }
      // existential delay
      if (IsBottom(pointer)) {
        if (IsUniversallyDelayed(source)) {
          return CanAssumeDelayed(pointer);
        }
        return CanAssumeNotDelayed(pointer) && CanAssumeNotDelayed(source);
      }
      if (IsExistentialDelayed(pointer) && IsDelayed(source)) return false; // existential delay does not equal to any one
      if (IsUniversallyDelayed(pointer)) {// universally delayed
        if (IsExistentialDelayed(source)) return false;
        if (IsUniversallyDelayed(source)) return true; //universal delayed 
      }
      // all other cases, source must be not delayed
      return CanAssumeNotDelayed(source);
    }

    /// <summary>
    /// Check that value in v is (universally) delayed (and if bottom, make it delayed)
    /// </summary>
    /// <param name="v"> </param>
    /// <returns>true if value is delayed from here on.</returns>
    public bool CanAssumeDelayed(Variable v)
    {
      ISymValue sv = Value(v);
      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[sv];
     
      if (aval.IsUniversallyDelayed) return true; // can't be existentially delayed
      if (aval.IsBottom)
      {
        this.egraph[sv] = DefAssignLattice.AVal.AssignedUniversalDelay;
        return true;
      }
      return false;
    }
   
    public void PrintStatus(Variable v)
    {
      if (v.Type.IsValueType) return;
      ISymValue sv = Value(v);
      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[sv];
      System.Console.WriteLine("Variable {0} status is {1} -- {2}", v, aval, sv);
    }

    /// <summary>
    /// Check that value in v is NOT delayed (and if bottom, make it not delayed)
    /// </summary>
    /// <param name="v"></param>
    /// <returns>true if value is not delayed from here on.</returns>
    public bool CanAssumeNotDelayed(Variable v) {
      if (v.Type.IsValueType) return true;
      ISymValue sv = Value(v);
      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[sv];
      // System.Console.WriteLine("Variable {0} ({1}) delay status: {2}", v, sv, aval);
      if (aval.IsExactlyNotDelayed) return true;
      if (aval.IsBottom) {
        this.egraph[sv] = DefAssignLattice.AVal.AssignedNoDelay;
        return true;
      }
      return false;
    }
    
    public bool IsDelayed(Variable v) {
      ISymValue sv = Value(v);
      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[sv];
      return aval.IsExactlyDelayed;
    }

    // existential delay
    public bool IsUniversallyDelayed(Variable v)
    {
      ISymValue sv = Value(v);
      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[sv];
      return aval.IsExactlyDelayed && !aval.IsExistentialDelay;
    }

    public bool IsTopDelayed(Variable v) {
      ISymValue sv = Value(v);
      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[sv];
      return aval.IsTopDelay;
    }
    public bool IsExistentialDelayed(Variable v)
    {
      ISymValue sv = Value(v);
      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[sv];
      return aval.IsExistentialDelay;
    }

    /// <summary>
    /// Determine if two variables are (definite) aliases.
    /// </summary>
    /// <returns>
    /// True if v1 and v2 are the same variable, or that they are
    /// both contained in the table and their svalue is the same.
    /// </returns>
    public bool HasSameValue(Variable v1, Variable v2) {
      if (v1.UniqueKey == v2.UniqueKey) {
        return true;
      }
      if (IsContained(v1) && IsContained(v2)) {
        ISymValue sv1 = Value(v1);
        ISymValue sv2 = Value(v2);

        return (sv1 == sv2); // must be the same thing. 
      }
      return false;
    }

    public bool FieldHasSameValue(Variable v1, Field f, Variable v2) {
      if (this.IsContainedClassField(v1, f) && IsContained(v2)) {
        ISymValue sv1 = this.ValueOfClassField(v1, f);
        ISymValue sv2 = Value(v2);

        return (sv1 == sv2); // must be the same thing. 
      }
      return false;
    }
    // end existential delay
    private DefAssignLattice.AVal GetAVal(ISymValue sv) {
      return (DefAssignLattice.AVal)this.egraph[sv];
    }
    public bool IsDelayedRefContents(Variable v) {
      ISymValue pointer = Value(v);
      ISymValue value = this.egraph[ValueOf, pointer];
      DefAssignLattice.AVal aval = GetAVal(value);
      return aval.IsExactlyDelayed;
    }
    public bool IsBottom(Variable v) {
      ISymValue sv = Value(v);
      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[sv];
      return aval.IsBottom;
    }
    public void SetBaseUnassigned() {
      ISymValue sv = this.egraph[InitializedVariables.Base];
      this.egraph[sv] = DefAssignLattice.AVal.UnassignedOnly;
    }

    public void SetBaseAssigned() {
      ISymValue sv = this.egraph[InitializedVariables.Base];
      this.egraph[sv] = DefAssignLattice.AVal.AssignedNoDelay;
    }

    public void SetUnassigned(Field f) {
      f = CanonicalField(f);
      ISymValue sv = this.egraph[f];
      this.egraph[sv] = DefAssignLattice.AVal.UnassignedOnly;
    }

    public void SetAssigned(Field f) {
      f = CanonicalField(f);
      ISymValue sv = this.egraph[f];
      this.egraph[sv] = DefAssignLattice.AVal.AssignedNoDelay;
    }

 

    // existential delay
    public void SetExistentialDelay(Variable v)
    {
      ISymValue sv = Value(v);
      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[sv];
      this.egraph[sv] = aval.SameButWith(DefAssignLattice.Delay.ExistentialDelay);
    }
    // end existential delay

    /// <summary>
    /// Returns element type of reference or pointer, otherwise null
    /// </summary>
    private TypeNode ElementType(TypeNode t) {
      Reference r = t as Reference;
      if (r != null) {
        return r.ElementType;
      }
      Pointer p = t as Pointer;
      if (p != null) {
        return p.ElementType;
      }
      return null;
    }

    public void SetAssignedRef(Variable v) {
      SetAssignedRef(v, false);
    }

    /// <summary>
    /// Value(Value(v1)) = (v2)
    /// </summary>
    public void SetRef(Variable v1, Variable v2) {
      //this.egraph[ValueOf, this.egraph[v1]] = this.egraph[v2];
      ISymValue sv = this.egraph[ValueOf, this.egraph[v1]];
      this.egraph[ValueOf, sv] = this.egraph[v2];
    }
    public void SetAssignedRef(Variable v, bool maintainDelay) {
      // set indirect target as assigned 
      ISymValue sv = this.egraph[ValueOf, this.egraph[v]];

      DefAssignLattice.AVal aval = DefAssignLattice.AVal.AssignedNoDelay;
      // maintain delay
      if (maintainDelay) {
          DefAssignLattice.AVal av = (DefAssignLattice.AVal)this.egraph[sv];
          DefAssignLattice.Delay delay = av.Delay;
          aval = aval.SameButWith(delay);
      }
      this.egraph[sv] = aval;

      Variable starV = this.GetLocalMappingToRef(v);
      if (starV != null) {
        this.SetAssignedStatus(starV);
      }
    }
    public void SetNonDelayedRef(Variable v) {
      TypeNode elementType = ElementType(v.Type);
      if (elementType == null) return;
      if (elementType.IsValueType) return;
      ISymValue ptr = Value(v);
      ISymValue contents = this.egraph[ValueOf, ptr];
      this.egraph[contents] = DefAssignLattice.AVal.AssignedNoDelay;
    }

    public void SetUsedRef(Variable v) {
      Variable starV = this.GetLocalMappingToRef(v);
      if (starV != null) {
        this.SetReferencedStatus(starV);
      }
    }

    /// <summary>
    /// Given a field (v.f) (where v we know is likely "this"), see if the egraph currently
    /// contains a value for v.f. This is useful when you want to look up a value but do not
    /// want to modify the egraph. (Normal lookup in an egraph will return a fresh value if
    /// none is found.)
    /// </summary>
    /// <param name="v"></param>
    /// <param name="f"></param>
    /// <returns></returns>
    public bool IsContainedClassField(Variable v, Field f) {
      Field f1= CanonicalField(f);
      ISymValue loc = this.egraph.TryLookup(v);
      if (loc == null) return false;

      loc = this.egraph.TryLookup(ValueOf, loc);
      if (loc == null) return false;

      loc = this.egraph.TryLookup(f, loc);
      if (loc == null) return false;

      return true;
    }

    /// <summary>
    /// Similar to above, but test a variable to see it has an entry in this.egraph.
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    public bool IsContained(Variable v) {
      ISymValue loc = this.egraph.TryLookup(v);
      if (loc == null) return false;

      loc = this.egraph.TryLookup(ValueOf, loc);
      if (loc == null) return false;

      return true;
    }

    public void SetUsedRefDeep(Variable v) {
      ISymValue loc = this.egraph.TryLookup(ValueOf, this.egraph[v]);
 
      while (loc != null) {
        Variable w = GetLocalMappingToLoc(loc);
        if (w != null) {
          this.SetReferencedStatus(w);
        }
        loc = this.egraph.TryLookup(ValueOf, loc);
      }
    }

    public void SetAssignedStructField(Variable v, Field f) {
      // set field of indirect target as assigned unless entire struct already is
      ISymValue sv = this.egraph[ValueOf, this.egraph[v]];

      DefAssignLattice.AVal structaval = (DefAssignLattice.AVal)this.egraph[sv];
      if (structaval.Assigned) return; // entire struct already assigned

      f = CanonicalField(f);
      ISymValue svfield = this.egraph[f, sv];
      this.egraph[svfield] = DefAssignLattice.AVal.AssignedNoDelay;

      // if all fields are assigned now, consider struct assigned. We need to do this here
      // because at Joins, we don't have enough information to detect that a struct that is
      // fully assigned (but no field details) is as assigned as a struct that has all its fields
      // assigned.
      foreach (Member m in f.DeclaringType.Members) {
        Field g = m as Field;
        if (g == null) continue;
        ISymValue gfield = this.egraph.TryLookup(g, sv);
        if (gfield == null) return; // no info.
        DefAssignLattice.AVal fieldaval = (DefAssignLattice.AVal)this.egraph[gfield];
        if (!fieldaval.Assigned) return; // no info.
      }
      // now all fields are assigned. Treat entire struct as assigned.
      this.egraph[sv] = structaval.SameButAssigned;
    }
    public void SetAssignedClassField(Variable v, Field f){
      f = CanonicalField(f);
      ISymValue sv = this.egraph[ValueOf, this.egraph[v]];
      ISymValue svfield = this.egraph[f, sv];
      this.egraph[svfield] = DefAssignLattice.AVal.AssignedNoDelay;
      return;
    }

    public void SetAssignedBottomDelayedClassField(Variable v, Field f) {
      f = CanonicalField(f);
      ISymValue sv = this.egraph[ValueOf, this.egraph[v]];
      ISymValue svfield = this.egraph[f, sv];
      this.egraph[svfield] = DefAssignLattice.AVal.AssignedBottomDelay;
      return;
    }
    public bool IsDelayedClassField(Variable v, Field f) {
      f = CanonicalField(f);
      ISymValue sv = this.egraph[ValueOf, this.egraph[v]];
      ISymValue svfield = this.egraph[f, sv];
      return (this.egraph[svfield] == DefAssignLattice.AVal.AssignedUniversalDelay);
    }

    public bool IsTopDelayedClassField(Variable v, Field f) {
      f = CanonicalField(f);
      ISymValue sv = this.egraph[ValueOf, this.egraph[v]];
      ISymValue svfield = this.egraph[f, sv];
      DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[svfield];
      return (aval.IsTopDelay);
    }

    public ISymValue ValueOfClassField(Variable v, Field f) {
      f = CanonicalField(f);
      ISymValue sv = this.egraph[ValueOf, this.egraph[v]];
      ISymValue svfield = this.egraph[f, sv];
      return svfield;
    }

    public void SetUniversalDelayedClassField(Variable v, Field f) {
      f = CanonicalField(f);
      ISymValue sv = this.egraph[ValueOf, this.egraph[v]];
      ISymValue svfield = this.egraph[f, sv];
      this.egraph[svfield] = DefAssignLattice.AVal.AssignedUniversalDelay;
      return;
    }

    /// <summary>
    /// For an assignment: d = s.f, 
    /// assign the status of s.f to d.
    /// </summary>
    public void CopyFieldStatus(Variable dest, Field f, Variable source) {
      f = CanonicalField(f);
      ISymValue sv = this.egraph[ValueOf, this.egraph[source]];
      ISymValue svfield = this.egraph[f, sv];
      //DefAssignLattice.AVal aval = (DefAssignLattice.AVal) this.egraph[svfield];
      ISymValue svdest = this.egraph[dest];
      this.egraph[ValueOf, svdest] =  svfield; //DefAssignLattice.AVal)this.egraph[svfield];
    }

    public void AssignFieldAddress(Variable dest, Field field, Variable source, Method currentMethod, TypeSystem typeSystem) {
      // We capture the correct field address only when necessary. Currently, if the field is a non-static
      // struct field, or if we are in the constructor and the field is a non-null field assigned on this.
      if (!field.IsStatic && field.Type != null)
      {
        field = CanonicalField(field);
        ISymValue objectLoc = this.egraph[ValueOf, this.egraph[source]];
        DefAssignLattice.AVal sourceAVal = (DefAssignLattice.AVal)this.egraph[objectLoc];
        ISymValue fieldLoc = this.egraph[field, objectLoc];
        this.egraph[ValueOf, this.egraph[dest]] = fieldLoc;
        // check if objectLoc is fully assigned. If so, set fieldLoc as assigned too.
        if (this.IsAssigned(objectLoc)) {
          this.egraph[fieldLoc] = DefAssignLattice.AVal.AssignedNoDelay;
        }
        // existential delay
        else if (!sourceAVal.IsBottom && !sourceAVal.IsNotDelayed)
        {
          this.egraph[fieldLoc] = ((DefAssignLattice.AVal)this.egraph[fieldLoc]).SameButWith(sourceAVal.Delay);
        }
        // end existential delay
        else
        {
          SetDelay(fieldLoc, DefAssignLattice.Delay.NoDelay);
        }
      }
      else {
        if (source != null) {
          this.SetAssignedAndCopyDelay(dest, source);
        }
        else {
          this.SetLocationAssigned(dest);
          this.SetAssignedRef(dest);
        }
      }
    }

    public void CopyVariable(Variable dest, Variable source) {
      // if the source/dest copies pointers (references), then we need
      // to make their values equal.
      if (!dest.Equals(source)) { this.SetLocationAssigned(dest); }
      if ((dest.Type != null && !dest.Type.IsValueType) || (source.Type != null && !source.Type.IsValueType)) {
        ISymValue svalue = this.egraph[ValueOf, this.egraph[source]];
        this.egraph[ValueOf, this.egraph[dest]] = svalue;
      }
      // if one of the two is a pointer type, we furthermore make the target assigned
      if ((source.Type != null && source.Type.IsPointerType) || (dest.Type != null && dest.Type.IsPointerType)) {
        this.SetAssignedRef(source);
      }
    }

    public void PureCopy(Variable dest, Variable source) {
      if ((dest.Type != null && !dest.Type.IsValueType) || (source.Type != null && !source.Type.IsValueType)) {
        ISymValue svalue = this.egraph[ValueOf, this.egraph[source]];
        this.egraph[ValueOf, this.egraph[dest]] = svalue;
      }
    }

    public void SetAssignedAndCopyDelay(Variable dest, Variable source) {
      if (source.Type != null && source.Type.IsValueType) {
        this.SetLocationAssigned(dest);
        ISymValue destValue = Value(dest);
        this.egraph[destValue] = DefAssignLattice.AVal.AssignedNoDelay;
      }
      else {
        ISymValue sourceValue = Value(source);
        DefAssignLattice.AVal aval = (DefAssignLattice.AVal)this.egraph[sourceValue];
        this.SetLocationAssigned(dest);
        ISymValue destValue = Value(dest);
        this.egraph[destValue] = this.egraph[sourceValue];
      }
    }

    public void CopyAddress(Variable dest, Variable source) {
      this.SetLocationAssigned(dest);
      ISymValue svalue = this.egraph[source];
      this.egraph[ValueOf, this.egraph[dest]] = svalue;
      // If the target is a pointer, assume the location is initialized
      if (dest.Type != null && dest.Type.IsPointerType) {
        this.SetUsedRefDeep(dest);
      }
      // assume reference is non-delayed
      this.SetDelay(svalue, DefAssignLattice.Delay.NoDelay);
    }

    public void LoadIndirect(Variable dest, Variable pointer) {
      SetUsedRef(pointer);
      SetLocationAssigned(dest);
      SetValueAssignedAndNonDelayed(dest);
    }

    /// <summary>
    /// Must follow field edges of value types backwards as well
    /// </summary>
    public Variable GetLocalMappingToLoc(ISymValue loc) {
      WorkList refparams = new WorkList();
      WorkList fields = new WorkList();
      fields.Add(loc);
      while (! fields.IsEmpty()) {
        ISymValue sv = (ISymValue)fields.Pull();
        foreach (EGraphTerm eterm in egraph.EqTerms(sv)) {
          if ( !(eterm.Function is StackVariable)) {
            Variable v = eterm.Function as Variable;
            if (v != null) {
              return v;
            }
            Field f = eterm.Function as Field;
            if (f != null && f.DeclaringType.IsValueType) {
              if (eterm.Args.Length>0) {
                fields.Add(eterm.Args[0]);
              }
            }
            if (eterm.Function == ValueOf && eterm.Args.Length>0) {
              // could be that we are looking at a ref parameter
              refparams.Add(eterm.Args[0]);
            }
          }
        }
      }
      while (! refparams.IsEmpty()) {
        ISymValue sv = (ISymValue)refparams.Pull();
        foreach (EGraphTerm eterm in egraph.EqTerms(sv)) {
          if ( !(eterm.Function is StackVariable)) {
            Variable v = eterm.Function as Variable;
            if (v != null && (v.Type is Reference || v.Type is Pointer)) {
              return v;
            }
          }
        }
      }
      return null;
    }

    public string GetPathMappingToRef(Variable r, out bool localVar) {
      return GetPathMappingToLocation(Value(r), out localVar);
    }
    private string GetPathMappingToLocation(ISymValue loc, out bool localVar) {
      Variable v = GetLocalMappingToLoc(loc);
      if (v != null) {
        localVar = true;
        return v.Name.Name;
      }
      localVar = false;
      // search for fields of possibly unassigned locals, i.e. This and out parameters.
      foreach (IUniqueKey c in egraph.Constants) {
        Parameter p = c as Parameter;
        if (p == null) continue;
        if (p.IsOut || (p is This && p.DeclaringMethod is InstanceInitializer)) {
          ISymValue lv = Value(p);
          if (this.IsAssigned(lv)) continue;
          // candidate
          foreach (IUniqueKey f in egraph.Functions(lv)) {
            Field fld = f as Field;
            if (fld == null) continue;
            ISymValue floc = egraph[f, lv];
            if (floc == loc) {
              return p.Name.Name + "." + fld.Name.Name;
            }
          }
        }
      }
      return null;
    }

    public Variable GetLocalMappingToRef(Variable r) {
      ISymValue loc = this.egraph[ValueOf, this.egraph[r]];
      return GetLocalMappingToLoc(loc);
    }

    /// <summary>
    /// Constructor
    /// </summary>
    public InitializedVariables(Analyzer analyzer) {
      this.analyzer = analyzer;
      this.egraph = new EGraph(DefAssignLattice.It);
      this.referenceStatus = new HashedMap();
    }

    private InitializedVariables(Analyzer analyzer, IEGraph egraph, IMap referenceStatus) {
      this.analyzer = analyzer;
      this.egraph = egraph;
      this.referenceStatus = referenceStatus;
    }

    /// <summary>
    /// Required by IDataFlowAnalysis interface.
    /// </summary>
    public void Dump(){
      this.egraph.Dump(Console.Out);
    }


    /// <summary>
    /// Called when a merge point is first encountered. Provides the ability to remove dead variables from
    /// the state to keep its size down.
    /// </summary>
    /// <param name="stackdepth">depth of stack at the beginning of the block</param>
    public void FilterStackAndTemporaryLocals(int stackdepth) {
      // filter out all temporary locals
      // we assume (for now) that they have a lifetime of 1 block
      /*
      foreach (IUniqueKey key in this.egraph.Constants) {
        Local l = key as Local;
        if (l == null) continue;
        if (l.Name == null || l.Name == Identifier.Empty) {
          // a temp local
          egraph.Eliminate(l);
        }
      }
      */
    }

    public static InitializedVariables Merge(InitializedVariables atMerge, InitializedVariables incoming, CfgBlock joinPoint) {
      bool unchanged;
      IEGraph result = atMerge.egraph.Join(incoming.egraph, joinPoint, out unchanged);

      if (unchanged) return null;
      return new InitializedVariables(incoming.analyzer, result, atMerge.referenceStatus);
    }
  }


  /// <summary>
  /// Definite assignment and usage analysis.
  /// 
  /// Definite assignment is about locations, not values. Thus, we use the egraph as follows:
  /// 
  /// Variables represent locations, thus the symbolic values in the egraph represent location addresses.
  /// 
  /// We track the status of these locations (assigned, referenced)
  /// 
  /// To track pointers to locations, we also keep track of terms of the form Value(loc), representing
  /// the value stored in the location loc.
  /// 
  /// Thus, an assignment x = y is represented as Value(sym(x)) := Value(sym(y))
  /// 
  /// 
  /// DELAYED REFERENCES
  ///
  /// In order to allow static checking of initialization of circular structures, we introduce the
  /// notion of a delayed reference. A delayed reference D(T) is a pointer whose type invariants may
  /// not be valid (and thus observed) until time T. The CLR already has such a notion in the fact that
  /// "this" is delayed in constructors until the base call.
  /// We add to this "universally quantified" delays for "This" and other parameters. We track that
  /// no reads are performed on universally delayed references and that they are stored only in other
  /// universally delayed references.
  /// 
  /// Reference types T<amp/> that may point to fields of delayed objects are treated specially. Intuitively,
  /// these references would have to have the same delay as the pointed to object field.
  /// Within a method, that is the case. However, the common cases of ref and out parameters that create
  /// such pointers in regular C#/Spec# code can be handled slightly better.
  /// Essentially, a delayed reference or a pointer to a field of a delayed reference should never be passed as
  /// a ref parameter, since it cannot be read prior to initialization.
  /// On the other hand, passing such pointers as Out parameters seems valid and desirable and in fact does
  /// not require the parameter to be marked [Delayed], since the out aspect already guarantees initialization
  /// prior to use. This assumes that the type of the out parameter captures all the invariants that the 
  /// field should ultimately possess.
  /// </summary>
  internal sealed class DefAssignLattice : MathematicalLattice {

    public struct Delay {
      public enum DelayStatus {
        Top = 0,
        NoDelay,
        UniversalDelay,
        ExistentialDelay,
        Bottom,
      }
      public readonly DelayStatus delay;

      private Delay(DelayStatus st) {
        this.delay = st;
      }

      public static Delay Top = new Delay(DelayStatus.Top);
      public static Delay NoDelay = new Delay(DelayStatus.NoDelay);
      public static Delay UniversalDelay = new Delay(DelayStatus.UniversalDelay);
      public static Delay Bottom = new Delay(DelayStatus.Bottom);
      public static Delay ExistentialDelay = new Delay(DelayStatus.ExistentialDelay);

      public static Delay Join(Delay a, Delay b) {
        if (a.delay == DelayStatus.Bottom) return b;
        if (b.delay == DelayStatus.Bottom) return a;
        // existential delay
        if (a.delay == DelayStatus.ExistentialDelay && b.delay == DelayStatus.UniversalDelay)
          return ExistentialDelay;
        if (a.delay == DelayStatus.UniversalDelay && b.delay == DelayStatus.ExistentialDelay)
          return ExistentialDelay;
        if (a.delay == DelayStatus.ExistentialDelay && b.delay == DelayStatus.NoDelay)
          return ExistentialDelay;
        if (a.delay == DelayStatus.NoDelay && b.delay == DelayStatus.ExistentialDelay)
          return ExistentialDelay;
        // end existential delay
        if (a.delay != b.delay) return Top;
        return a;
      }

      public static Delay Meet(Delay a, Delay b) {
        if (a.delay == DelayStatus.Top) return b;
        if (b.delay == DelayStatus.Top) return a;
        if (a.delay != b.delay) return Bottom; // keep this intact with existential delay
        return a; 
      }

      public bool IsNotDelayed {
        get { return this.delay == DelayStatus.Bottom || this.delay == DelayStatus.NoDelay; }
      }
      public bool IsExactlyNotDelayed {
        get { return this.delay == DelayStatus.NoDelay; }
      }
      public bool IsExactlyDelayed {
        get { return this.delay == DelayStatus.UniversalDelay || this.delay == DelayStatus.ExistentialDelay;
        }
      }
      public bool IsUniversallyDelayed
      {
        get
        {
          return this.delay == DelayStatus.UniversalDelay;
        }
      }
      public bool IsExistentialDelay
      {
        get { return this.delay == DelayStatus.ExistentialDelay; }
      }

      public bool IsTop {
        get { return this.delay == DelayStatus.Top; }
      }

      //

      public override string ToString() {
        return this.delay.ToString();
      }
    }

    /// <summary>
    /// Ordering:
    /// 
    ///   A lt B   iff
    ///   
    ///   !A.Assigned implies !B.Assigned
    ///   and 
    ///   !A.Unassigned implies !B.Unassigned
    /// </summary>
    public class AVal : AbstractValue {
      public readonly bool Unassigned; // definitely not assigned
      public readonly bool Assigned;   // definitely assigned
      public readonly Delay Delay;

      private AVal(bool unassigned, bool assigned, Delay delay) {
        this.Unassigned = unassigned;
        this.Assigned = assigned; 
        this.Delay = delay;
      }

      public static AVal Bottom                     = new AVal(true, true, Delay.Bottom);
      public static AVal Top                        = new AVal(false, false, Delay.Top);

      public static AVal UnassignedBottomDelay      = new AVal(true, false, Delay.Bottom);
      public static AVal UnassignedOnly             = new AVal(true, false, Delay.NoDelay);
      public static AVal UnassignedUniversalDelay   = new AVal(true, false, Delay.UniversalDelay);
      public static AVal UnassignedExistentialDelay = new AVal(true, false, Delay.ExistentialDelay);
      public static AVal UnassignedTopDelay         = new AVal(true, false, Delay.Top);

      public static AVal AssignedBottomDelay        = new AVal(false, true, Delay.Bottom);
      public static AVal AssignedNoDelay            = new AVal(false, true, Delay.NoDelay);
      public static AVal AssignedUniversalDelay     = new AVal(false, true, Delay.UniversalDelay);
      public static AVal AssignedExistentialDelay   = new AVal(false, true, Delay.ExistentialDelay);
      public static AVal AssignedTopDelay           = new AVal(false, true, Delay.Top);

      public static AVal BottomDelayOnly            = new AVal(false, false, Delay.NoDelay);
      public static AVal NoDelayOnly                = new AVal(false, false, Delay.NoDelay);
      public static AVal UniversalDelayOnly         = new AVal(false, false, Delay.UniversalDelay);
      public static AVal ExistentialDelayOnly       = new AVal(false, false, Delay.ExistentialDelay);

      /// <summary>
      /// Can round up value.
      /// </summary>
      private static AVal For(bool unassigned, bool assigned, Delay delay) {
        if (unassigned) {
          if (assigned) {
            return Bottom; // These all map to bottom
          }
          else {
            switch (delay.delay) {
              case Delay.DelayStatus.NoDelay:
                return UnassignedOnly;
              case Delay.DelayStatus.UniversalDelay:
                return UnassignedUniversalDelay;
              case Delay.DelayStatus.ExistentialDelay:
                return UnassignedExistentialDelay;
              case Delay.DelayStatus.Bottom:
                return UnassignedBottomDelay;
              default:
                return UnassignedTopDelay;
            }
          }
        } else { // not definitely unassigned
          if (assigned) {
            switch (delay.delay) {
              case Delay.DelayStatus.NoDelay:
                return AssignedNoDelay;
              case Delay.DelayStatus.UniversalDelay:
                return AssignedUniversalDelay;
              case Delay.DelayStatus.ExistentialDelay:
                return AssignedExistentialDelay;
              case Delay.DelayStatus.Bottom:
                return AssignedBottomDelay;
              default:
                return AssignedTopDelay;
            }
          }
          else { // neither definitely assigned, nor definitely unassigned
            switch (delay.delay) {
              case Delay.DelayStatus.NoDelay:
                return NoDelayOnly;
              case Delay.DelayStatus.UniversalDelay:
                return UniversalDelayOnly;
              case Delay.DelayStatus.ExistentialDelay:
                return ExistentialDelayOnly;
              case Delay.DelayStatus.Bottom:
                return BottomDelayOnly;
              default:
                return Top;
            }
          }
        }
      }

      public static AVal Join(AVal a, AVal b) {
        bool unassigned = a.Unassigned && b.Unassigned;
        bool assigned = a.Assigned && b.Assigned;
        return AVal.For(unassigned, assigned, Delay.Join(a.Delay, b.Delay));
      }


      public static AVal Meet(AVal a, AVal b) {
        bool unassigned = a.Unassigned || b.Unassigned;
        bool assigned = a.Assigned || b.Assigned;
        return AVal.For(unassigned, assigned, Delay.Meet(a.Delay, b.Delay));
      }

      public AVal SameButWith(Delay delay) {
        return For(this.Unassigned, this.Assigned, delay);
      }
      public AVal SameButAssigned {
        get {
          return AVal.For(this.Unassigned, true, this.Delay);
        }
      }

      public override string ToString() {
        return String.Format("U:{0},A:{1},D:{2}", this.Unassigned, this.Assigned, this.Delay.ToString());
      }

      public override bool IsTop { get { return this.Equals(Top); } }
      public override bool IsBottom { get { return this.Equals(Bottom); } }
      public bool IsNotDelayed {
        get { return this.Delay.IsNotDelayed; }
      }
      // existential delay
      public bool IsExactlyDelayed {
        get { return this.Delay.IsExactlyDelayed; }
      }
      public bool IsExistentialDelay
      {
        get { return this.Delay.IsExistentialDelay; }
      }
      public bool IsUniversallyDelayed
      {
        get { return this.Delay.IsUniversallyDelayed; }
      }
      // end existential delay
      public bool IsExactlyNotDelayed {
        get { return this.Delay.IsExactlyNotDelayed; }
      }
      public bool IsTopDelay {
        get { return this.Delay.IsTop; }
      }
    }


    protected override bool AtMost(AbstractValue a, AbstractValue b) {
      AVal av = (AVal)a;
      AVal bv = (AVal)b;

      return (av.Assigned || !bv.Assigned) && (av.Unassigned || !bv.Unassigned);
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


    private DefAssignLattice() {}

    public static DefAssignLattice It = new DefAssignLattice();

  }
  /// <summary>
  /// Definite assignment checker.
  ///
  /// This checker also makes sure that all non-null fields are properly initialized prior to any possible read accesses.
  /// There are two cases: 
  ///  1) if the constructor is non-delayed (i.e., the this parameter is not delayed), then the fields must be initialized prior to the 
  ///     base .ctor call
  ///  2) if the constructor is delayed (guaranteeing that during the constructor execution, no fields of this are ever consulted), then
  ///     the fields must be initialized by the end of the constructor.
  /// </summary>
  internal class MethodDefiniteAssignmentChecker:ForwardDataFlowAnalysis{
    
    /// <summary>
    /// Current instruction visitor.
    /// </summary>
    DefiniteAssignmentInstructionVisitor iVisitor;
    
    /// <summary>
    /// Current Block under analysis.
    /// </summary>
    internal CfgBlock currBlock;

    /// <summary>
    /// typeSystem. Only used to file errors and warnings.
    /// </summary>
    internal TypeSystem typeSystem;

    /// <summary>
    /// Current method being analyzed.
    /// </summary>
    internal Method currentMethod;

    // Data structure that contains the variables that are known to be existential delayed
    // at a particular statement.
    // A record in the table is of the form <statement, set of existentially delayed vars>
    private NodeMaybeExistentialDelayInfo nodeExistentialTable;
    public NodeMaybeExistentialDelayInfo NodeExistentialTable
    {
      get
      {
        return nodeExistentialTable;
      }
    }

    public void CollectExistentialVars(Statement position, InitializedVariables iv)
    {
      // add to the (static) table one line (stmt, existential delayed variables that reach "stmt")
      nodeExistentialTable.Add(position, iv.MayHaveExistentialDelayedVars);
    }

    // end existential delay

    /// <summary>
    /// This is a repository that stores errors that has been reported.
    /// 
    /// It is used basically as a set. Only used in DefiniteAssignmentInstructorVisitor.check. 
    /// If the invariant that all variables have their source context holds, then the key in this set 
    /// could be individual variables. 
    /// </summary>
    internal Hashtable reportedErrors; 

    /// <summary>
    /// The exit state. Used to check assigned but not referenced variables.
    /// </summary>
    internal IDataFlowState exitState;

    Analyzer analyzer;

    /// <summary>
    /// Entry point of the check. 
    /// 
    /// It create a new instance of the checker, and run the checker on the given method.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="method"></param>
    public static IDelayInfo  Check(TypeSystem t, Method method, Analyzer analyzer, PreDAStatus preResult) {
      Debug.Assert(method!=null && analyzer != null);
      MethodDefiniteAssignmentChecker checker= new MethodDefiniteAssignmentChecker(t, analyzer, preResult);
      checker.currentMethod=method;
      ControlFlowGraph cfg=analyzer.GetCFG(method);

      if(cfg==null)
        return null;

      InitializedVariables iv = new InitializedVariables(analyzer);
      checker.Run(cfg,iv);
      if (checker.exitState == null) {
        // Method has no normal return. We should consider having such method as annotated with [NoReturn].
        return checker.NodeExistentialTable;
      }
      InitializedVariables onExit = new InitializedVariables((InitializedVariables)checker.exitState);

      // check for unassigned Out parameters.
      ParameterList pl=checker.currentMethod.Parameters;
      if(pl!=null){
        for(int i=0; i < pl.Count; i++){
          Parameter p = pl[i];
          if(p.Type is Reference && p.IsOut && !onExit.IsAssignedRef(p, ((Reference)p.Type).ElementType as Struct))
            checker.typeSystem.HandleError(method.Name,Error.ParamUnassigned,p.Name.Name);
        }
      }
      // check for struct fields in constructor
      if (method is InstanceInitializer && method.DeclaringType != null && method.DeclaringType.IsValueType) {
        Field unassignedfield = onExit.NonAssignedRefField(method.ThisParameter, (Struct)method.DeclaringType);
        if (unassignedfield != null) {
          checker.typeSystem.HandleError(method, Error.UnassignedThis, checker.typeSystem.GetTypeName(unassignedfield.DeclaringType)+"."+unassignedfield.Name.Name);
        }
      }
      // if delayed constructor, check for field initializations here
      if (method is InstanceInitializer && checker.iVisitor.IsUniversallyDelayed(method.ThisParameter)) {
        checker.iVisitor.CheckCtorNonNullFieldInitialization(method.ThisParameter, method.Name, onExit, Error.NonNullFieldNotInitializedAtEndOfDelayedConstructor);
      }

      // Check for uninitialized base class
      if (method is InstanceInitializer
        && method.DeclaringType is Class
        && method.DeclaringType.BaseType != null
        && method.DeclaringType.BaseType != SystemTypes.MulticastDelegate
        && !onExit.IsBaseAssigned())
        checker.typeSystem.HandleError(method, Error.BaseNotInitialized);

      // Check for assigned but unreferenced variables.
      //TODO: this check should be moved to Looker so that constant folding does not affect it
      onExit.EmitAssignedButNotReferencedErrors(checker.typeSystem);

      return (checker.NodeExistentialTable);
    }

    protected MethodDefiniteAssignmentChecker(TypeSystem t, Analyzer analyzer, PreDAStatus preResult) {
      typeSystem=t;
      iVisitor=new DefiniteAssignmentInstructionVisitor(analyzer, this, preResult);
      reportedErrors=new Hashtable();
      this.analyzer = analyzer;
      this.nodeExistentialTable = new NodeMaybeExistentialDelayInfo ();
    }
    protected override IDataFlowState Merge(CfgBlock previous, CfgBlock joinPoint, IDataFlowState atMerge, IDataFlowState incoming, out bool resultDiffersFromPreviousMerge, out bool mergeIsPrecise) {

      resultDiffersFromPreviousMerge = false;
      mergeIsPrecise = false;

      // No new states;
      if (incoming == null)
        return atMerge;

      // Initialize states
      if (atMerge == null) {
        resultDiffersFromPreviousMerge = true;
        //
        ((InitializedVariables)incoming).FilterStackAndTemporaryLocals(joinPoint.StackDepth);
        return incoming;
      }

      if (Analyzer.Debug) {
        Console.WriteLine("DefAssign merge at block {0}", (joinPoint).UniqueKey);
        Console.WriteLine("atMerge:\n---------");
        atMerge.Dump();
        Console.WriteLine("incoming:\n---------");
        incoming.Dump();
      }
      // Merge the two.
      InitializedVariables newState = InitializedVariables.Merge((InitializedVariables)atMerge, (InitializedVariables)incoming, joinPoint);
      if (newState == null) {
        if (Analyzer.Debug) {
          Console.WriteLine("result UNchanged");
        }
        return atMerge;
      }
      resultDiffersFromPreviousMerge = true;
      if (Analyzer.Debug) {
        Console.WriteLine("Result of merge\n---------");
        newState.Dump();
      }
      return newState;
    }

    protected override IDataFlowState VisitBlock(CfgBlock block, IDataFlowState state) {
      Debug.Assert(block!=null);

      InitializedVariables onEntry = (InitializedVariables)state;
      currBlock=block;

      if (Analyzer.Debug) {
        Analyzer.Write("---------block: "+block.UniqueId+";");
        Analyzer.Write("   Exit:");
        foreach (CfgBlock b in block.NormalSuccessors)
          Analyzer.Write(b.UniqueId+";");
        if (block.UniqueSuccessor!=null)
          Analyzer.Write("   FallThrough: "+block.UniqueSuccessor+";");
        if (block.ExceptionHandler!=null)
          Analyzer.Write("   ExHandler: "+block.ExceptionHandler.UniqueId+";");
        Analyzer.WriteLine("");
        state.Dump();
      }
      if (block.ExceptionHandler!=null) {
        InitializedVariables exnState = onEntry;
        onEntry = new InitializedVariables(onEntry); // Copy state, since common ancestor cannot be modified any longer
        PushExceptionState(block, exnState);
      }

      InitializedVariables resultState = (InitializedVariables)base.VisitBlock(block, onEntry);
      if(block.UniqueId== cfg.NormalExit.UniqueId) {
        exitState=resultState;
      }
      return resultState;
      
    }

    protected override IDataFlowState VisitStatement(CfgBlock block, Statement statement, IDataFlowState dfstate) 
    {
      // For debug purpose
      if (Analyzer.Debug)
      {
        try
        {
          Analyzer.WriteLine(new SampleInstructionVisitor().Visit(statement, null) + "   :::   " + statement.SourceContext.SourceText);
        }
        catch (Exception)
        {
          Analyzer.WriteLine("Print error: " + statement);
        }
      }
     
      IDataFlowState result=null;
      try{
        result =(IDataFlowState)(iVisitor.Visit(statement,dfstate));
        if (Analyzer.Debug && result != null) {
          result.Dump();
       }
      }catch(Exception e){
        typeSystem.HandleError(statement,Error.InternalCompilerError,"Definite Assignement: "+e.Message);
      }
      return result;
    }


    /// <summary>
    /// Since we push the state from each block to the exception handler, we don't need to chain them here.
    /// </summary>
    protected override void SplitExceptions(CfgBlock handler, ref IDataFlowState currentHandlerState, out IDataFlowState nextHandlerState) {
      nextHandlerState=null;
      return;
    }
  }

  /// <summary>
  /// This is the main class for the pre-analysis, which determines at each statement,
  /// the set of created but not committed arrays, the commited arrays, and if the 
  /// statement is a commit, whether it is ok to lift the status of the array to
  /// non-delay. 
  /// </summary>
  internal class MethodReachingDefNNArrayChecker: ForwardDataFlowAnalysis  {
    public TypeSystem TypeSystem;
    Analyzer analyzer;
    ReachingDefNNArrayInstructionVisitor iVisitor;
    // A line of this table: <statement, created_arrays>
    TrivialHashtable table = new TrivialHashtable ();

    // A line of this table: <statement, committed_arrays>
    TrivialHashtable tableCommitted = new TrivialHashtable();

    // A line of this table: <statement, bool>
    Hashtable okTable;
    Hashtable nonDelayArrayTable;

    public Hashtable OKTable {
      get {
        return okTable;
      }
    }

    public Hashtable NonDelayArrayTable {
      get {
        return nonDelayArrayTable;
      }
    }
    internal Block currBlock;
    internal IDataFlowState exitState;

    public TrivialHashtable NotCommitted {
      get {
        return table;
      }
    }

    public TrivialHashtable Committed {
      get {
        return tableCommitted;
      }
    }

    private MethodReachingDefNNArrayChecker(TypeSystem ts, Analyzer analyzer, Method method) {
      TypeSystem = ts;
      this.analyzer = analyzer;
      currentMethod = method;
      iVisitor = new ReachingDefNNArrayInstructionVisitor(analyzer, this);
      okTable = new Hashtable();
      nonDelayArrayTable = new Hashtable();
      NNArrayStatus.OKAtCommit = okTable;
      NNArrayStatus.NonDelayByCreation = nonDelayArrayTable;
    }

    Method currentMethod;

    public Method CurrentMethod {
      get {
        return currentMethod;
      }
    }

    ArrayList errors;
    public void HandleError(Node offendingNode, Error error, params string [] messages) {
      if (errors == null) {
        errors = new ArrayList();
      }
      errors.Add(new ErrorReport<Error>(offendingNode, error, messages));
    }

    public void HandleError() {
      errors.Sort();
      foreach (ErrorReport<Error> en in errors) {
        this.TypeSystem.HandleError(en.OffendingNode, en.Error, en.Messages);
      }
    }
    /// <summary>
    /// Standard check function, except that it returns a PreDAStatus data structure
    /// that contains the three tables mentioned above.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="method"></param>
    /// <param name="analyzer"></param>
    /// <returns>A PreDAStatus data structure, or a null if error occurs. </returns>
    public static PreDAStatus Check(TypeSystem t, Method method, Analyzer analyzer) {
      Debug.Assert(method != null && analyzer != null);
      MethodReachingDefNNArrayChecker checker = new MethodReachingDefNNArrayChecker(t, analyzer, method);
      ControlFlowGraph cfg = analyzer.GetCFG(method);

      if (cfg == null)
        return null;

      InitializedVariables iv = new InitializedVariables(analyzer);
      NNArrayStatus status = new NNArrayStatus(iv);
      NNArrayStatus.Checker = checker;
      checker.Run(cfg, status);
      
      // Check whether there are arrays that have been created but not committed
      NNArrayStatus exitStatus2 = checker.exitState as NNArrayStatus;
      if (exitStatus2 != null) {
        if (Analyzer.Debug) {
          Console.WriteLine("exit state of {0} is", method.FullName);
          exitStatus2.Dump();
        }
        ArrayList notCommitted = exitStatus2.CreatedButNotInitedArrays();
        if (notCommitted != null && notCommitted.Count != 0) {
          foreach (object o in notCommitted) {
            string offendingString = "A non null element array";
            Node offendingNode = method;
            if (o is Variable) {
              offendingString = "Variable \'" + o + "\'";
              offendingNode = o as Variable;
            }
            if (o is Pair<Variable, Field>) {
              Pair<Variable, Field> pair = o as Pair<Variable, Field>;
              Variable var = pair.Fst;
              Field fld = pair.Snd;
              offendingString = "Field \'" + var + "." + fld + "\'";
              if (NNArrayStatus.FieldsToMB.ContainsKey(pair)) {
                offendingNode = NNArrayStatus.FieldsToMB[pair] as MemberBinding;
                if (offendingNode == null) {
                  offendingNode = fld;
                } else {
                  MemberBinding mb = offendingNode as MemberBinding;
                  if (mb.TargetObject == null || mb.SourceContext.SourceText == null) {
                    offendingNode = fld;
                  }
                }
              }
            }
            checker.HandleError(offendingNode, Error.ShouldCommit, offendingString);
          }
        }

        ArrayList notCommittedOnAllPaths = exitStatus2.CreatedButNotFullyCommittedArrays();
        if (notCommittedOnAllPaths != null && notCommittedOnAllPaths.Count != 0) {
          foreach (object o in notCommittedOnAllPaths) {
            string offendingString = "A non-null element array";
            Node offendingNode = method;
            if (o is Variable) {
              offendingString = "variable \'" + o + "\'";
              offendingNode = o as Variable;
            } else if (o is Pair<Variable, Field>) {
              Pair<Variable, Field> pair = o as Pair<Variable, Field>;
              Variable var = pair.Fst;
              Field fld = pair.Snd;
              if (NNArrayStatus.FieldsToMB.ContainsKey(pair)) {
                offendingNode = NNArrayStatus.FieldsToMB[pair] as MemberBinding;
                if (offendingNode == null) {
                  offendingNode = fld;
                } else {
                  MemberBinding mb = offendingNode as MemberBinding;
                  if (mb.TargetObject == null || mb.SourceContext.SourceText==null) {
                    offendingNode = fld;
                  }
                }
              }
              offendingString = "field \'" + var + "." + fld + "\'";
            }

            checker.HandleError(offendingNode, Error.ShouldCommitOnAllPaths, offendingString);
          }
        }
        if (checker.errors != null && checker.errors.Count != 0) {
          checker.HandleError();
          checker.errors.Clear();
          return null;
        }
      }
      return new PreDAStatus(checker.NotCommitted, checker.Committed, checker.OKTable, checker.NonDelayArrayTable);
    }

    /// <summary>
    /// merge the two state atMerge and incoming.
    /// </summary>
    /// <param name="previous"></param>
    /// <param name="joinPoint"></param>
    /// <param name="atMerge"></param>
    /// <param name="incoming"></param>
    /// <param name="resultDiffersFromPreviousMerge"></param>
    /// <param name="mergeIsPrecise"></param>
    /// <returns></returns>
    protected override IDataFlowState Merge(CfgBlock previous, CfgBlock joinPoint, IDataFlowState atMerge, IDataFlowState incoming, out bool resultDiffersFromPreviousMerge, out bool mergeIsPrecise) {
      resultDiffersFromPreviousMerge = false;
      mergeIsPrecise = false;

      // No new states;
      if (incoming == null)
        return atMerge;

      // Initialize states
      if (atMerge == null) {
        resultDiffersFromPreviousMerge = true;
        //
        //((NNArrayStatus)incoming).FilterStackAndTemporaryLocals(joinPoint.StackDepth);
        return incoming;
      }

      if (Analyzer.Debug ) {
        Console.WriteLine("Array Reachable Def merge at block {0}", (joinPoint).UniqueKey);
        Console.WriteLine("atMerge:\n---------");
        atMerge.Dump();
        Console.WriteLine("incoming:\n---------");
        incoming.Dump();
      }
      // Merge the two.
      NNArrayStatus newState = NNArrayStatus.Merge((NNArrayStatus)atMerge, (NNArrayStatus)incoming, joinPoint);
      if (newState == null) {
        if (Analyzer.Debug) {
          Console.WriteLine("result UNchanged");
        }
        return atMerge;
      }
      resultDiffersFromPreviousMerge = true;
      if (Analyzer.Debug) {
        Console.WriteLine("Result of merge\n---------");
        newState.Dump();
      }
      return newState;
    }

    protected override void Run(ControlFlowGraph cfg, CfgBlock startBlock, IDataFlowState startState) {
      base.Run(cfg, startBlock, startState);
    }

    protected override void Run(ControlFlowGraph cfg, IDataFlowState startState) {
      base.Run(cfg, startState);
    }

    protected override IDataFlowState VisitBlock(CfgBlock block, IDataFlowState state) {
      Debug.Assert(block != null);

      NNArrayStatus onEntry = (NNArrayStatus)state;
      currBlock = block;

      if (Analyzer.Debug) {
        Analyzer.Write("---------block: " + block.UniqueId + ";");
        Analyzer.Write("   Exit:");
        foreach (CfgBlock b in block.NormalSuccessors)
          Analyzer.Write(b.UniqueId + ";");
        if (block.UniqueSuccessor != null)
          Analyzer.Write("   FallThrough: " + block.UniqueSuccessor + ";");
        if (block.ExceptionHandler != null)
          Analyzer.Write("   ExHandler: " + block.ExceptionHandler.UniqueId + ";");
        Analyzer.WriteLine("");
        state.Dump();
      }
      if (block.ExceptionHandler != null) {
        NNArrayStatus exnState = onEntry;
        onEntry = new NNArrayStatus(onEntry); // Copy state, since common ancestor cannot be modified any longer
        PushExceptionState(block, exnState);
      }

      NNArrayStatus resultState = (NNArrayStatus)base.VisitBlock(block, onEntry);
      if (block.UniqueId == cfg.NormalExit.UniqueId) {
        exitState = resultState;
      }
      if (resultState == null) {
        return null;
      }
      return new NNArrayStatus(resultState);

    }

    /// <summary>
    /// Record the incoming status's sets of created and committed NN Arrays.
    /// </summary>
    /// <param name="block"></param>
    /// <param name="statement"></param>
    /// <param name="dfstate"></param>
    /// <returns></returns>
    protected override IDataFlowState VisitStatement(CfgBlock block, Statement statement, IDataFlowState dfstate) {
      //IDataFlowState result =  base.VisitStatement(block, statement, dfstate);
      table[statement.UniqueKey] = (dfstate as NNArrayStatus).CreatedButNotInitedArrays();
      tableCommitted[statement.UniqueKey] = (dfstate as NNArrayStatus).CommittedArrays();
 
      IDataFlowState result = null;
      //try {
        result = (IDataFlowState)(iVisitor.Visit(statement, dfstate));
        if (Analyzer.Debug && result != null) {
          result.Dump();
        }
      //}
      //catch (Exception e) {
      //  this.TypeSystem.HandleError(statement, Error.InternalCompilerError, "NNArray Pre Analysis " + e.Message);
      //}
      return result;
    }

    protected override void SplitExceptions(CfgBlock handler, ref IDataFlowState currentHandlerState, out IDataFlowState nextHandlerState) {
      nextHandlerState = null;
      return;
    }
  }

  /// <summary>
  /// This stack is part of the NNArrayStatus, the data structure used in the
  /// pre-DA analysis. It keeps track of variables of different delay time on 
  /// a stack. Elements on the stack are equivalent classes of variables of known 
  /// same delay. The deeper on the stack an equivalent class, the longer 
  /// its delay. 
  /// 
  /// When a statement suggests that two variables should have the same delay, we combine
  /// their equivalent classes together with all the equivalence class inbetween.
  /// This operation is called "crushing" the stack (to the level where the variable
  /// has previously a longer delay).
  /// </summary>
  internal class InitializedVarStack {
    Stack layers;
    public InitializedVarStack() {
      layers = new Stack();
    }

    /// <summary>
    /// copy constructor. We need to copy the set.
    /// </summary>
    /// <param name="stack"></param>
    public InitializedVarStack(InitializedVarStack stack) {
      layers = new Stack();

      object[] arr = stack.layers.ToArray();
      for (int i = arr.Length - 1; i >= 0; i--) {
        HashSet set = arr[i] as HashSet;
        if (set == null) {
          // this is an internal error
          continue;
        }
        layers.Push(set.Clone());
      }
    }

    /// <summary>
    /// Test if the top of the stack is an empty set.
    /// </summary>
    public bool EmptyTop {
      get {
        if (layers.Count == 0) return false;
        return (((HashSet)layers.Peek()).Count ==0); 
      }
    }

    public HashSet Pop() {
      return (HashSet)layers.Pop();
    }

    /// <summary>
    /// create a new equivalent class that contains a sole element of variable
    /// v and push this class onto the stack.
    /// </summary>
    /// <param name="v"></param>
    public void Push(Variable v) {
      HashSet set = new HashSet();
      set.Add(v);
      layers.Push(set);
    }

    /// <summary>
    /// push an existing equivalent class to the stack.
    /// </summary>
    /// <param name="h"></param>
    public void Push(HashSet h) {
      layers.Push(h);
    }

    public void Dump() {
      Console.WriteLine("stack depth = {0}", layers.Count);
      for (int i = 0; i < layers.Count; i++) {
        Console.WriteLine("   layer {0}: {1}", i, layers.ToArray()[i] as HashSet);
      }
    }

    /// <summary>
    /// Find out which layer (top: level 0) contains the value of variable v.
    /// If v is not on the stack, return -1;
    /// </summary>
    /// <param name="v"></param>
    /// <param name="iv"></param>
    /// <returns></returns>
    public int LevelOf(Variable v, InitializedVariables iv) {
      int depth = layers.Count;
      int i = 0;
      while (i < depth) {
        if (ContainsValueOf(v, iv, this[i])) {
          return i;
        }
        i++;
      }

      return -1;
    }

    /// <summary>
    /// Merges two initialization stacks. 
    /// </summary>
    /// <param name="stack1">Input stack 1.</param>
    /// <param name="stack2">Input stack 2.</param>
    /// <returns>
    /// A new InitializedVarStack retvalue, such that:
    /// retvalue = {v | v in union(stack1, stack2) and v not in common trunk}
    /// "common trunk" = CDR^n (stack1) where n is the smallest non-negative integer such that
    /// there exists a non-negative m such that CDR ^m (stack2) = CDR ^n (stack1);
    /// </returns>
    public static InitializedVarStack Merge(InitializedVarStack stack1, InitializedVarStack stack2, InitializedVariables iv) {
      InitializedVarStack retvalue;

      // shallow one's depth
      int depth1 = stack1.layers.Count;
      int depth2 = stack2.layers.Count;

      // they may both be zero, which is contained by this case
      if (depth1 == depth2) {
        if (Same(stack1, stack2, iv)) {
          retvalue = new InitializedVarStack(stack1);
          return retvalue;
        }
        // if not the same, go on
      }

      // if one of them is zero, return the other
      if (depth1 == 0 ) {
        retvalue = new InitializedVarStack(stack2); 
        return retvalue;
      }
      if (depth2 == 0) {
        retvalue = new InitializedVarStack(stack1);
        return retvalue;
      }

      retvalue = new InitializedVarStack();

      int depth = depth1 > depth2 ? depth1 : depth2;
      InitializedVarStack shallowone = (depth1 < depth2) ? stack1 : stack2;
      InitializedVarStack deepone = (depth1 < depth2) ? stack2 : stack1;

      // shallow one's top must be the crush of the deep one's top |depth1-depth2| + 1 layers. 
      HashSet set = new HashSet();
      object[] array = deepone.layers.ToArray();
      for (int i = 0; i < Math.Abs(depth1 - depth2) +1 ; i++) {
        set = (HashSet)Set.Union(set, (HashSet)array[i]);
      }

      if (!ContainSameValue((HashSet)shallowone.layers.Peek(), set, iv)) {
        //Leave this print-out here to signal possible unknown problems.  
        if (Analyzer.Debug) {
          Console.WriteLine("New delayed value created not in all paths");
        }
        retvalue = new InitializedVarStack(shallowone);
        return retvalue;
      }

      // make a copy of shallow one and return
      retvalue = new InitializedVarStack(shallowone);
      
      // shallow one's CDR must be the same as the deeper one's 
      object[] array2 = shallowone.layers.ToArray();
      int diff = Math.Abs(depth1 - depth2); 
      for (int j = 1; j < array2.Length; j++) {
        if (!ContainSameValue((HashSet)array2[j], (HashSet)array[j + diff], iv)) {
          // TODO: This is unlikely to happen. Still should consider to report 
          // an error. 
        }
      }
      return retvalue;
    }

    // equals for two hash set of variables
    static bool ContainSameValue(HashSet s1, HashSet s2, InitializedVariables iv) {
      foreach (Variable v in s1) {
        if (!ContainsValueOf(v, iv, s2)) {
          return false;
        }
      }

      foreach (Variable v in s2) {
        if (!ContainsValueOf(v, iv, s1)) {
          return false;
        }
      }

      return true;
    }

    /// <summary>
    /// Whether two IVStacks are the same, alias wise.
    /// </summary>
    /// <returns></returns>
    static bool Same(InitializedVarStack stack1, InitializedVarStack stack2, InitializedVariables iv) {
      if (stack1.layers.Count != stack2.layers.Count) {
        return false;
      }
      object[] array1 = stack1.layers.ToArray();
      object[] array2 = stack2.layers.ToArray();
      for (int i = 0; i < array1.Length; i++) {
        if (!ContainSameValue((HashSet)array1[i], (HashSet)array2[i], iv)) {
          return false;
        }
      }

      return true;
    }

    /// <summary>
    /// Whether a value or its alias is contained in a set, according to iv
    /// 
    /// iv contains alias information. 
    /// </summary>
    static bool ContainsValueOf(Variable v, InitializedVariables iv, HashSet set) {
      foreach (Variable u in set) {
        if (iv.HasSameValue(u, v)) {
          return true;
        }
      }

      return false;
    }

    public HashSet this[int i] {
      get {
        return (HashSet)layers.ToArray()[i];
      }
    }

    /// <summary>
    /// Crush the first "depth" number of layers into one
    /// </summary>
    /// <param name="depth">
    /// </param>
    public void CrushToLevel(int depth) {
      if (depth <= 0 || depth > layers.Count) {
        // 0 is possible
      }
      else {
        HashSet h = new HashSet();
        for (int j = depth; j > 0; j--) {
          h = (HashSet) Set.Union(h, (HashSet)layers.Pop());
        }

        layers.Push(h);
      }
    }

    /// <summary>
    /// For a variable v, if, according to iv, its value is the same as one of the 
    /// variables kept on the top of stack, remove that value from the top of the stack.
    /// 
    /// </summary>
    /// <param name="v"> The variable.</param>
    /// <param name="iv"> An InitializedVariables data structure, where an EGraph supposedly
    /// maintains the alias information.
    /// </param>
    public void RemoveVar(Variable v, InitializedVariables iv) {

      if (layers!= null && layers.Count != 0) {
        HashSet set = (HashSet)layers.Peek();

        ArrayList toDelete = new ArrayList();

        foreach (Variable u in set) {
          if (iv.HasSameValue(u, v)) {
            toDelete.Add(u);
          }
        }

        if (toDelete.Count != 0) {
          foreach (object o in toDelete) {
            set.Remove(o);
          }
        }
      }
    }
  }

  internal class VariableValueNotFoundOnStackTop : Exception {
    public VariableValueNotFoundOnStackTop(string varname) : base (varname) {
    }
  }

  internal class NNArrayStatus : IDataFlowState {
    InitializedVariables ivs;
    public InitializedVariables Ivs {
      get {
        return ivs;
      }
    }
    static public MethodReachingDefNNArrayChecker Checker;
    // A hashtable in which (key: Variable, value: bool) represents that if 
    // a variable is ok to be nondelay at commit point
    static public Hashtable OKAtCommit;
    static public Hashtable NonDelayByCreation;

    HashSet vars;
    HashSet fields;
    static Hashtable fieldsToMB = new Hashtable();
    static public Hashtable FieldsToMB {
      get { return fieldsToMB; }
    }
    HashSet zeroInts;

    InitializedVarStack stack;

    public NNArrayStatus(NNArrayStatus nnas) {
      this.ivs = new InitializedVariables(nnas.ivs);
      this.vars = nnas.vars;
      this.fields = nnas.fields;
      this.zeroInts = nnas.zeroInts;
      stack = new InitializedVarStack(nnas.stack);
    }
    /// <summary>
    /// This constructor is called at the beginning of checking a method.
    /// </summary>
    /// <param name="old"></param>
    public NNArrayStatus(InitializedVariables old) {
      this.ivs = old;
      vars = new HashSet();
      fields = new HashSet();
      fieldsToMB.Clear();
      this.zeroInts = new HashSet();
      stack = new InitializedVarStack();
    }

    public NNArrayStatus(InitializedVariables ivs, HashSet vars, HashSet fields, HashSet zeroInts, InitializedVarStack stack) {
      this.ivs = ivs;
      this.vars = vars;
      this.fields = fields;
      this.zeroInts = zeroInts;
      this.stack = stack;
    }

    public void Add(Variable v) {
      stack.Push(v);
    }

    public void Add(HashSet h) {
      stack.Push(h);
    }

    public void SetNonDelayByCreation(Statement stat) {
      NonDelayByCreation[stat.UniqueKey] = true;
    }

    public void RemoveVarAtCommit(Variable v, Statement s) {
      stack.RemoveVar(v, ivs);

      if (stack.EmptyTop) {
        stack.Pop();
        OKAtCommit[s.UniqueKey] = true;
      }
      else {
        OKAtCommit[s.UniqueKey] = false; 
      }
    }

    public bool ContainsValueAtTop(Variable v) {
      return (stack.LevelOf(v, ivs) == 0);
    }

    /// <summary>
    /// Given a variable, if its value is contained on the stack,
    /// crush the stack to the level that contains the variable. 
    /// That is, if the variable is on level 2 (top level being 0),
    /// we union the variable sets on layer 0, 1, and 2 and replace
    /// current layer 2 with this union. 
    /// 
    /// If the variable's value is not contained, do nothing.
    /// </summary>
    /// <param name="v"></param>
    public void CrushStackToLevelContaining(Variable v) {
      int depth = stack.LevelOf(v, ivs) + 1;
      // depth will be zero if v is not on the stack.
      stack.CrushToLevel(depth); 
    }

    public static bool CanBeNondelayed(NNArrayStatus status) {
      if (status == null) return false;
      int rs = status.NumOfCreatedButNotInitedArrays();
      return (rs == 1); // has only one delayed arrays.
    }

    public bool IsCreatedButNotCommittedArray(Variable v) {
      ArrayList created = this.CreatedButNotInitedArrays();
      foreach (object o in created) {
        if (o is Variable) {
          if (HasSameValue(o as Variable, v)) {
            return true;
          }
        }

        if (o is Pair<Variable, Field>) {
          Pair<Variable, Field> pair = o as Pair<Variable, Field>;
          if (FieldHasSameValue(pair.Fst, pair.Snd, v)) {
            return true;
          }
        }
      }

      return false;
    }

    public bool HasSameValue(Variable p, Variable v) {
      return ivs.HasSameValue(p, v);
    }

    public bool FieldHasSameValue(Variable ths, Field f, Variable v) {
      return ivs.FieldHasSameValue(ths, f, v);
    }

    /// <summary>
    /// TODO: The set of vars/fields that are created only/committed must agree
    /// </summary>
    /// <returns></returns>
    public static NNArrayStatus Merge(NNArrayStatus atMerge, NNArrayStatus incoming, CfgBlock joinPoint) {
      Debug.Assert(atMerge != null && incoming != null);   
      // if (atMerge == incoming) return null;
      InitializedVariables result = InitializedVariables.Merge(atMerge.ivs, incoming.ivs, joinPoint);
      if (result == null
        && atMerge.vars.Equals(incoming.vars)
        && atMerge.fields.Equals(incoming.fields)
        && atMerge.zeroInts.Equals(incoming.zeroInts)
        ) 
        return null;

      if (result == null) result = atMerge.ivs; 

      return new NNArrayStatus(result, (HashSet)Set.Union(atMerge.vars, incoming.vars),  
        Set.Union(atMerge.fields,incoming.fields) as HashSet, 
        Set.Intersect(atMerge.zeroInts, incoming.zeroInts) as HashSet,
        mergeStack(atMerge.stack, incoming.stack, result));
    }

    static InitializedVarStack mergeStack(InitializedVarStack stack1, InitializedVarStack stack2, InitializedVariables ivs) {
      return InitializedVarStack.Merge(stack1, stack2, ivs);
    }

    public void SetRef(Variable dest, Variable source) {
      ivs.SetRef(dest, source);
    }

    /// <summary>
    /// For a variable v that represents an NN array, set its status as Created but not initialized
    /// </summary>
    public void SetCreated(Variable v) {
      vars.Add(v);
      if (this.isCreatedOnly(v)) {
        // TODO: report an error: created twice
      }
      ivs.SetUniversallyDelayed(v, false); // internally as assigned but universal delayed
    }
    /// <summary>
    /// For a variable v that represents an NN array, set its status as Initialized.
    /// </summary>
    public void SetInitialized(Variable v) {
      if (this.isCreatedOnly(v)) {
        ivs.SetValueAssignedAndNonDelayed(v);// internally as assigned but non-delayed
      }
      else {
        // may consider a warning 
        ivs.SetValueAssignedAndNonDelayed(v);
      }
    }

    public void CopyStatusField(Variable dest, Field field, Variable source, Node expr) {
      if (this.isCreatedOnly(source)) {
        ivs.SetUniversalDelayedClassField(dest, field);
      }
      if (this.isInitialized (source) && dest != null) {
        ivs.SetAssignedClassField(dest, field);
      }

      Pair<Variable, Field> pair = new Pair<Variable, Field>(dest, field);

      if (expr != null && expr is MemberBinding) {
        if (!fieldsToMB.ContainsKey(pair)) {
          fieldsToMB.Add(pair, expr);
        }
      }
      
      fields.Add(pair);
    }

    public void CopyFieldStatus(Variable dest, Field field, Variable source) {
      ivs.CopyFieldStatus(dest, field, source);
    }

    public void CopyStatus(Variable dest, Variable source) {
      ivs.CopyVariable(dest, source);
    }

    public void Copy(Variable dest, Variable source) {
      ivs.CopyVariable(dest, source);
    }

    public void SetToZero(Variable dest) {
      zeroInts.Add(dest);
    }

    public void SetToNotZero(Variable dest) {
      zeroInts.Remove(dest);
    }

    public void CopyZero(Variable dest, Variable source) {
      if (zeroInts.Contains(source)) {
        zeroInts.Add(dest);
      } else {
        zeroInts.Remove(dest);
      }
    }

    public bool IsZero(Variable v) {
      return zeroInts.Contains(v);
    }

    public void PureCopy(Variable dest, Variable source) {
      ivs.PureCopy(dest, source);
    }
    /// <summary>
    /// Find all the program (not temporary or stack) variable that has been new-ed (created)
    /// but not committed (initialized). Note that an NN array is not considered initialized until
    /// commited.
    /// </summary>
    /// <returns></returns>
    public int NumOfCreatedButNotInitedArrays() {
      int i=0;
      foreach (Variable v in vars) {
        if (ivs.IsDelayed(v) && !isTemp(v)) {
          i++;
        }
      }

      foreach (Pair<Variable, Field> pair in fields) {
        if (ivs.IsDelayedClassField(pair.Fst, pair.Snd)) {
          i++;
        }
      }
      return i; 
    }

    /// <summary>
    /// Collect NN Array variables that are created but not committed.
    /// One variable per svalue. For example, if array1 and array2 are aliases
    /// of the same array, we choose one of them as an representative in the
    /// set. 
    /// 
    /// </summary>
    /// <returns></returns>
    public ArrayList CreatedButNotInitedArrays() {
      ArrayList arr = new ArrayList();

      HashSet DelayedSValues = new HashSet();

      foreach (Variable v in vars) {
        if (ivs.IsContained(v)) {
          if (!isTemp(v)) {
            if (ivs.IsUniversallyDelayed(v)) {
              if (!DelayedSValues.Contains(ivs.Value(v))) {
                DelayedSValues.Add(ivs.Value(v));
                arr.Add(v);
              }
            }
          }
        }
      }

      foreach (Pair<Variable, Field> pair in fields) {
        if (ivs.IsContainedClassField(pair.Fst, pair.Snd)) {
          if (ivs.IsDelayedClassField(pair.Fst, pair.Snd)) {
            if (!DelayedSValues.Contains(ivs.ValueOfClassField(pair.Fst, pair.Snd))) {
              arr.Add(pair);
            }
          }
        }
      }

      return arr;
    }

    public ArrayList CreatedButNotFullyCommittedArrays() {
      ArrayList arr = new ArrayList();

      HashSet DelayedSValues = new HashSet();

      foreach (Variable v in vars) {
        if (ivs.IsContained(v)) {
          if (!isTemp(v)) {
            if (ivs.IsTopDelayed(v)) {
              if (!DelayedSValues.Contains(ivs.Value(v))) {
                DelayedSValues.Add(ivs.Value(v));
                arr.Add(v);
              }
            }
          }
        }
      }

      foreach (Pair<Variable, Field> pair in fields) {
        if (ivs.IsContainedClassField(pair.Fst, pair.Snd)) {
          if (ivs.IsTopDelayedClassField(pair.Fst, pair.Snd)) {
            if (!DelayedSValues.Contains(ivs.ValueOfClassField(pair.Fst, pair.Snd))) {
              DelayedSValues.Add(ivs.ValueOfClassField(pair.Fst, pair.Snd));
              arr.Add(pair);
            }
          }
        }
      }

      return arr;
    }

    public ArrayList CommittedArrays() {
      ArrayList arr = new ArrayList();
      foreach (Variable v in vars) {
        if (this.isInitialized(v) && !isTemp(v)) {
          arr.Add(v);
        }
      }

      foreach (Pair<Variable, Field> pair in fields) {
        if (ivs.IsContainedClassField(pair.Fst, pair.Snd)) {
          if (ivs.IsAssignedClassField(pair.Fst, pair.Snd)) {
            arr.Add(pair);
          }
        }
      }
      return arr;
    }



    private static bool isTemp(Variable v) {
      if (
        v.SourceContext.Document == null ||
        v.Name == null ||
        v.Name.Name == "" ||
        v.Name.Name.StartsWith("$finally") ||
        v.Name.Name.StartsWith("Closure Class Local") ||
        v.Name.Name.Equals("Display Return Local") ||
        v.Name.Name.Equals("ContractMarker") ||
        v is StackVariable
        )
        return true;
      return false;
    }

    public void PrintStatus (Variable v){
      ivs.PrintStatus(v);
    }

    public void PrintStatus() {
      Console.WriteLine("committed are:");
      foreach (object o in this.CommittedArrays()) {
        Variable v = o as Variable;
        if (v != null) {
          ivs.PrintStatus(v);
          continue;
        }
        Pair<Variable, Field> pair = o as Pair<Variable, Field>;
        if (pair != null) {
          Console.WriteLine("a pair {0}.{1}", pair.Fst, pair.Snd);
        }
      }

      Console.WriteLine("created are:");
      foreach (object o in this.CreatedButNotInitedArrays()) {
        Variable v = o as Variable;
        if (v != null) {
          ivs.PrintStatus(v);
          continue;
        }
        Pair<Variable, Field> pair = o as Pair<Variable, Field>;
        if (pair != null) {
          Console.WriteLine("a pair {0}.{1}", pair.Fst, pair.Snd);
        }
      }
    }
    /// <summary>
    /// test to see if a NNarray is created but not initialized
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    public bool isCreatedOnly(Variable v) {
      return ivs.IsDelayed(v);
    }
    /// <summary>
    /// test to see if a NNarray is initialized
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    public bool isInitialized(Variable v) {
      if (ivs.IsContained(v)) {
        return ivs.IsNotDelayedReference(v);
      }

      return false;
    }

    public void Dump() {
      ivs.Dump();
      stack.Dump();
    }
  }

  /// <summary>
  /// This instruction visitor collects reaching definition of an ANNE
  /// </summary>
  internal class ReachingDefNNArrayInstructionVisitor : InstructionVisitor {
    private MethodReachingDefNNArrayChecker checker;
    Analyzer analyzer;

    private ArrayList delayedParameters;
    private ArrayList DelayedParameters() {
      ArrayList result = new ArrayList();

      if (this.checker.CurrentMethod == null || this.checker.CurrentMethod.Parameters == null) {
        return result;
      }

      foreach (Parameter p in this.checker.CurrentMethod.Parameters) {
        if (p.IsUniversallyDelayed) {
          result.Add(p);
        }
      }

      if (this.checker.CurrentMethod.ThisParameter != null &&
        this.checker.CurrentMethod.ThisParameter.IsUniversallyDelayed) {
        result.Add(this.checker.CurrentMethod.ThisParameter);
      }

      return result;

    }

    private bool IsNotTemp(Variable v) {
      if (
        v.SourceContext.Document == null ||
        v.Name == null ||
        v.Name.Name=="" ||
        v.Name.Name.StartsWith("$finally") ||
        v.Name.Name.StartsWith("Closure Class Local") ||
        v.Name.Name.Equals("Display Return Local") ||
        v.Name.Name.Equals("ContractMarker") ||
        v is StackVariable 
        )
        return false;
      return true;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="analyzer"></param>
    /// <param name="m"></param>
    public ReachingDefNNArrayInstructionVisitor(Analyzer analyzer, MethodReachingDefNNArrayChecker m) {
      this.analyzer = analyzer;
      checker = m;
      delayedParameters = DelayedParameters();
    }

    // make sure we handle every case.
    protected override object DefaultVisit(Statement stat, object arg) {   
      checker.TypeSystem.HandleError(stat, Error.InternalCompilerError, "NN Array Pre Analysis: Instruction not implemented yet: " + stat.NodeType + ":" + stat.SourceContext.SourceText);
      return null;
    }

    protected override object VisitNop(Statement stat, object arg) {
      return arg;
    }
    protected override object VisitArgumentList(Variable dest, Statement stat, object arg) {
      return arg;
    }
    protected override object VisitAssertion(Assertion assertion, object arg) {
      return arg;
    }
    protected override object VisitBinaryOperator(NodeType op, Variable dest, Variable operand1, Variable operand2, Statement stat, object arg) {
      return arg;
    }
    protected override object VisitBox(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      return arg;
    }
    protected override object VisitBranch(Variable cond, Block target, Statement stat, object arg) {
      return arg;
    }
    protected override object VisitBreak(Statement stat, object arg) {
      return arg;
    }
    protected override object VisitCallIndirect(Variable dest, Variable callee, Variable receiver, Variable[] arguments, FunctionPointer fp, Statement stat, object arg) {
      CheckVariable(receiver, arg as NNArrayStatus);
      CheckArguments(arguments, arg as NNArrayStatus);
      return arg;
    }
    protected override object VisitCastClass(Variable dest, TypeNode type, Variable source, Statement stat, object arg) {
      return arg;
    }
    /// <summary>
    /// Visit an array creation
    /// If the element type is non-null, set dest's status as created
    /// </summary>
    /// <returns></returns>
    protected override object VisitNewArray(Variable dest, TypeNode type, Variable size, Statement stat, object arg) {
      NNArrayStatus status = arg as NNArrayStatus;
      if (dest != null && this.checker.TypeSystem.IsPossibleNonNullType(type)) {
        if (!status.IsZero(size)) {
          status.SetCreated(dest);
          status.Add(dest);
        } else {
          status.SetNonDelayByCreation(stat);
        }
      }
      
      return arg;
    }

    /// <summary>
    /// if the method call is NonNullType.AssertInitialized, change the status arguments[0] to initialized
    /// </summary>
    /// <returns></returns>
    protected override object VisitCall(Variable dest, Variable receiver, Method callee, ExpressionList arguments, bool virtcall, Statement stat, object arg) {
      NNArrayStatus status = arg as NNArrayStatus;
      if (dest == null) {
        if (receiver == null && callee != null && 
          ( callee == SystemTypes.NonNullTypeAssertInitialized
          || (callee.Template!= null && callee.Template == SystemTypes.NonNullTypeAssertInitializedGeneric)
          )
          ) {
          Variable v = arguments[0] as Variable;
          // Console.WriteLine("visit call,  type is {0}", v.Type);
          if (v != null) {
            if (Analyzer.Debug) {
              ArrayList arr = status.CreatedButNotInitedArrays();
              Console.WriteLine("before set init, the number of created but not committed: {0}", arr.Count);
              status.Dump();
            }
            status.RemoveVarAtCommit(v, stat);
            status.SetInitialized(v);
            if (Analyzer.Debug) {
              ArrayList arr = status.CreatedButNotInitedArrays();
              Console.WriteLine("after set init, the number of created but not committed: {0}", arr.Count);
              status.Dump();
            }
          }

          return arg;
        }
      }

      // If the return type of the callee is non null, and the method call is not delayed
      // then we consider the destination as initialized.
      // But currently we have no way to tell whether the result of the method call is delayed
      // or not. We have to enforce stricter rules. 
      // Temporary Rules: must be a static call and takes no arguments
      // (similar to the static field rule)
      if (isNNArrayType(callee.ReturnType)) {
          if (receiver == null && (callee.Parameters == null || callee.Parameters.Count ==0)) {
            status.SetInitialized(dest);
        }
      }

      CheckVariable(receiver, status);
      CheckArguments(arguments, status);
      //Console.WriteLine("at call, status contains {0} committed vars", status.CommittedArrays().Count);
      return arg;
    }

    private bool isNNArrayType(TypeNode tn) {
      if (tn is ArrayType) {
        ArrayType at = tn as ArrayType;
        TypeNode elementType = at.ElementType;
        return this.checker.TypeSystem.IsPossibleNonNullType(elementType);
      }

      return false;
    }

    /// <summary>
    /// Field Access: we do not trace value that goes to a field. Whenever a delayed
    /// parameter goes to a field, we consider it escaped. That means, even if the escape
    /// does not happen between the commitment and the creation, the commitment cannot
    /// be considered safe. As a result, a delayed type will be retained (this happens in
    /// DefAssign pass). 
    /// </summary>
    
    protected override object VisitStoreField(Variable dest, Field field, Variable source, Statement stat, object arg) {
      NNArrayStatus status = arg as NNArrayStatus;
      Debug.Assert(stat is AssignmentStatement);
      if (isNNArrayType(source.Type)) {
        if (dest == null) {
          // if dest is null, then it is a static field
          // TODO: really depends on what initialization policy we have for static non-null element arrays.
          //       For example, do we have a speical syntax? 
          status.Ivs.SetAssigned(field);
        }
        else {
          AssignmentStatement astat = stat as AssignmentStatement;
          if (astat != null && astat.Target != null) {
            status.CopyStatusField(dest, field, source, astat.Target);
          } else {
            status.CopyStatusField(dest, field, source, null);
          }
        }
      }
      CheckVariable(source, status); // see if source is a delayed parameter or its alias
      return arg;
    }

    protected override object VisitLoadField(Variable dest, Variable source, Field field, Statement stat, object arg) {
      NNArrayStatus status = arg as NNArrayStatus;
      if (isNNArrayType(dest.Type)) {
        // if source is null, then this is a static field, which hopefully dynamic checking
        // will guarantee its non-null/non-null-element invariants, if any. 
        if (source == null) {
          status.SetInitialized(dest);
        }
        else {
          status.CopyFieldStatus(dest, field, source);
        }
      }
      if (dest.Type.IsAssignableTo(SystemTypes.IntPtr)) {
        status.SetToNotZero(dest);
      }
      CheckVariable(source, status); 
      return arg;
    }

    /// <summary>
    /// See if the value of this v is the same as the set of delayed Parameters.
    /// If so, set the status to be true;
    /// </summary>
    /// <param name="v"></param>
    public void CheckVariable(Variable v, NNArrayStatus status) {

      if (v == null) return;

      if (isNNArrayType(v.Type)) {
        if (status.ContainsValueAtTop(v)) {
          // do nothing
        }
        else {
          status.CrushStackToLevelContaining(v);
        }
      }
      //foreach (Parameter p in delayedParameters) {
      //  if (isSameVar(p,v) || status.HasSameValue(p, v)) {
      //    status.SetEscaped(); 
      //  }
      //}
    }

    private bool isSameVar(Variable v, Variable p) {
      
      Variable v1 = v;
      Variable v2 = p;
      while (v1 is ParameterBinding || v1 is ThisBinding) {
        if (v1 is ParameterBinding) v1 = (v1 as ParameterBinding).BoundParameter;
        if (v1 is ThisBinding) v1 = (v1 as ThisBinding).BoundThis;
      }

      while (v2 is ParameterBinding || v2 is ThisBinding) {
        if (v2 is ParameterBinding) v2 = (v2 as ParameterBinding).BoundParameter;
        if (v2 is ThisBinding) v2 = (v2 as ThisBinding).BoundThis;
      }

      if (v1 == v2) return true;
      if (v1.UniqueKey == v2.UniqueKey) return true;
      return false;
    }

    public void CheckArguments(ExpressionList args, NNArrayStatus status) {
      if (args == null) return;
      foreach (Expression e in args) {
        Variable v = e as Variable;
        if (v != null) {
          CheckVariable(v, status);
        }
      }
    }

    public void CheckArguments(Variable [] args, NNArrayStatus status) {
      if (args == null) return;
      foreach (Variable v in args) {
          CheckVariable(v, status);
      }
    }

    /// <summary>
    /// Decide if a variable is a valid nn array upon entrance to a method. 
    /// To do so, the variable must not be a non-delayed parameter,
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    private bool isValidNNArray(Variable v) {
      if (v is Parameter) {
        Parameter p = v as Parameter;
        if (p.IsUniversallyDelayed) {
          return false;
        } else {
          return true;
        }
      }
      return false;
    }

    protected override object VisitCopy(Variable dest, Variable source, Statement stat, object arg) {
      NNArrayStatus status = (arg as NNArrayStatus);

      if (IsNotTemp(dest)) {
        CheckVariable(source, status);
      }
      if (isNNArrayType(dest.Type)) {
        if (isValidNNArray(source)) {
          status.SetInitialized(source);
        }
        status.CopyStatus(dest, source);
      }
      else {
        status.Copy(dest, source);
        if (dest.Type.IsAssignableTo(SystemTypes.IntPtr)) {
          status.CopyZero(dest, source);
        }
      }
      return arg;
    }

    protected override object VisitCatch(Variable var, TypeNode type, Statement stat, object arg) {
      return arg;
    }

    protected override object VisitConstrainedCall(Variable dest, Variable receiver, Method callee, ExpressionList arguments, TypeNode constraint, Statement stat, object arg) {
      CheckArguments(arguments, arg as NNArrayStatus);
      return arg;
    }

    protected override object VisitCopyBlock(Variable destaddr, Variable srcaddr, Variable size, Statement stat, object arg) {
      CheckVariable(srcaddr, arg as NNArrayStatus); // Is this necessary?
      return arg;
    }

    protected override object VisitEndFilter(Variable code, Statement stat, object arg) {
      return arg;
    }

    protected override object VisitFilter(Variable dest, Statement stat, object arg) {
      return arg;
    }

    protected override object VisitInitializeBlock(Variable addr, Variable val, Variable size, Statement stat, object arg) {
      CheckVariable(val, arg as NNArrayStatus);
      return arg;
    }

    protected override object VisitInitObj(Variable addr, TypeNode valueType, Statement stat, object arg) {
      return arg;
    }

    protected override object VisitIsInstance(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      return arg;
    }

    protected override object VisitLoadAddress(Variable dest, Variable source, Statement stat, object arg) {
      if (IsNotTemp(dest)) {
        CheckVariable(source, arg as NNArrayStatus);
      }
      else {
        ((NNArrayStatus)arg).SetRef(dest, source);
      }
      return arg;
    }

    protected override object VisitLoadConstant(Variable dest, Literal source, Statement stat, object arg) {
      if (dest.Type.IsAssignableTo(SystemTypes.IntPtr)) {
        if ((int)source.Value == 0) {
          ((NNArrayStatus)arg).SetToZero(dest);
        }
      }
      return arg;
    }

    protected override object VisitLoadElement(Variable dest, Variable source, Variable index, TypeNode elementType, Statement stat, object arg) {
      NNArrayStatus status = arg as NNArrayStatus;
      if (dest.Type.IsAssignableTo(SystemTypes.IntPtr)) {
        status.SetToNotZero(dest);
      }
      CheckVariable(source, arg as NNArrayStatus);
      return arg;
    }

    protected override object VisitLoadElementAddress(Variable dest, Variable array, Variable index, TypeNode elementType, Statement stat, object arg) {
      CheckVariable(array, arg as NNArrayStatus);
      return arg;
    }

    protected override object VisitLoadFieldAddress(Variable dest, Variable source, Field field, Statement stat, object arg) {
      CheckVariable(source, arg as NNArrayStatus);
      return arg;
    }

    protected override object VisitLoadFunction(Variable dest, Variable source, Method method, Statement stat, object arg) {
      CheckVariable(source, arg as NNArrayStatus);
      return arg;
    }
    protected override object VisitLoadIndirect(Variable dest, Variable pointer, TypeNode elementType, Statement stat, object arg) {
      CheckVariable(pointer, arg as NNArrayStatus);
      return arg;
    }

    protected override object VisitLoadNull(Variable dest, Literal source, Statement stat, object arg) {
      return arg;
    }

    protected override object VisitLoadToken(Variable dest, object token, Statement stat, object arg) {
      return arg;
    }

    protected override object VisitMakeRefAny(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      return arg;
    }

    protected override object VisitMethodEntry(Method method, IEnumerable parameters, Statement stat, object arg) {
      HashSet parameterSet = new HashSet();
      foreach (Variable v in delayedParameters) {
        parameterSet.Add(v);
      };

      if (parameterSet.Count != 0) {
        ((NNArrayStatus)arg).Add(parameterSet);
      }
      return arg;
    }

    protected override object VisitNewObject(Variable dest, TypeNode type, Statement stat, object arg) {
      return arg;
    }

    protected override object VisitRefAnyType(Variable dest, Variable source, Statement stat, object arg) {
      if (IsNotTemp(dest)) {
        CheckVariable(source, arg as NNArrayStatus);
      }
      else {
        copy(dest, source, arg);
      }
      return arg;
    }

    protected override object VisitRefAnyValue(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      if (IsNotTemp(dest)) {
        CheckVariable(source, arg as NNArrayStatus);
      }
      else {
        copy(dest, source, arg);
      }
      return arg;
    }

    protected override object VisitRethrow(Statement stat, object arg) {
      return arg;
    }

    protected override object VisitReturn(Variable var, Statement stat, object arg) {
      return arg;
    }

    protected override object VisitSizeOf(Variable dest, TypeNode type, Statement stat, object arg) {
      return arg;
    }

    protected override object VisitStoreElement(Variable dest, Variable index, Variable source, TypeNode elementType, Statement stat, object arg) {
      CheckVariable(source, arg as NNArrayStatus );
      return arg;
    }

    protected override object VisitStoreIndirect(Variable pointer, Variable source, TypeNode elementType, Statement stat, object arg) {
      CheckVariable(source, arg as NNArrayStatus);
      return arg;
    }

    protected override object VisitSwitch(Variable selector, BlockList targets, Statement stat, object arg) {
      return arg;
    }

    protected override object VisitSwitchCaseBottom(Statement stat, object arg) {
      return arg;
    }

    protected override object VisitThrow(Variable var, Statement stat, object arg) {
      CheckVariable(var, arg as NNArrayStatus); // can we write "throw this?"
      return arg;
    }
    protected override object VisitUnaryOperator(NodeType op, Variable dest, Variable operand, Statement stat, object arg) {
      if (IsNotTemp(dest)) {
        CheckVariable(operand, arg as NNArrayStatus);
      }
      else {
        copy(dest, operand, arg);
      }
      return arg;
    }

    protected override object VisitUnbox(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      if (IsNotTemp(dest)) {
        CheckVariable(source, arg as NNArrayStatus);
      }
      else {
        copy(dest, source, arg);
      }
      return arg;
    }

    protected override object VisitUnboxAny(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      if (IsNotTemp(dest)) {
        CheckVariable(source, arg as NNArrayStatus);
      }
      else {
        copy(dest, source, arg);
      }
      return arg;
    }

    private void copy(Variable dest, Variable source, object status) {
      ((NNArrayStatus)status).PureCopy(dest, source);
    }

    protected override object VisitUnwind(Statement stat, object arg) {
      return arg;
    }
  }

  internal class PreDAStatus {
    TrivialHashtable createdButNotCommitted;
    TrivialHashtable committed;
    Hashtable okTable;
    Hashtable nonDelayArrayTable;

    public PreDAStatus(TrivialHashtable created, TrivialHashtable committed, Hashtable oktable, Hashtable nonDelayArrayTable) {
      this.createdButNotCommitted = created;
      this.committed = committed;
      this.okTable = oktable;
      this.nonDelayArrayTable = nonDelayArrayTable;
    }

    public bool OKToCommit(Statement s) {
      return (bool)okTable[s.UniqueKey];
    }

    /// <summary>
    /// A new-array statement has created a non-delayed nn array, for example, by
    /// have zero length.
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    public bool IsNonDelayedArray(Statement s) {
      if (s == null) return false;
      if (nonDelayArrayTable == null) return false;
      if (!nonDelayArrayTable.ContainsKey(s.UniqueKey)) return false;
      return (bool)nonDelayArrayTable[s.UniqueKey];
    }

    public ArrayList committedPriorTo(Statement s) {
      return (ArrayList)committed[s.UniqueKey];
    }

    public ArrayList createdButNotCommittedPriorTo(Statement s) {
      return (ArrayList)createdButNotCommitted[s.UniqueKey];
    }
  }

  internal class DefiniteAssignmentInstructionVisitor:InstructionVisitor {

    private MethodDefiniteAssignmentChecker mpc;
    //private Hashtable reportedErrors;

    Analyzer analyzer;
    PreDAStatus nns;
    public DefiniteAssignmentInstructionVisitor(Analyzer analyzer, MethodDefiniteAssignmentChecker m,
      PreDAStatus preResult){
      mpc=m;
      //reportedErrors=new Hashtable();
      this.analyzer = analyzer;
      nns = preResult;
    }

    private void CheckNonDelay(InitializedVariables iv, Variable var, Node context){
      if (var==null) 
        return;
      if( ! iv.CanAssumeNotDelayed(var)) {
        if(!mpc.reportedErrors.Contains(var) && var.Name != null && var.Name.Name!=""){
          // Ugly hack to get delayed analysis past non-conformant use of get_FrameGuard.
          if (!(mpc.currentMethod.Name != null && mpc.currentMethod.Name.Name.StartsWith("get_SpecSharp::FrameGuard"))) {
            Error errorCode = Error.AccessThroughDelayedReference;
            if (this.mpc.currentMethod is InstanceInitializer && this.CallOnThis(iv, var)) {
              errorCode = Error.AccessThroughDelayedThisInConstructor;
            }
            Node offendingNode = (var.SourceContext.Document == null) ? context : var;
            mpc.typeSystem.HandleError(offendingNode, errorCode, var.Name.Name);
            mpc.reportedErrors.Add(var, null);
          }
        }
      }
      iv.SetValueNonDelayed(var);
    }

    private void CheckNonDelay(InitializedVariables iv, Variable var, Node context, Error errorCode) {
      if (var == null)
        return;
      if (!iv.CanAssumeNotDelayed(var)) {
        if (!mpc.reportedErrors.Contains(var) && var.Name != null && var.Name.Name != "") {
          // Ugly hack to get delayed analysis past non-conformant use of get_FrameGuard.
          if (!(mpc.currentMethod.Name != null && mpc.currentMethod.Name.Name.StartsWith("get_SpecSharp::FrameGuard"))) {
            Node offendingNode = (var.SourceContext.Document == null) ? context : var;
            mpc.typeSystem.HandleError(offendingNode, errorCode, var.Name.Name);
            mpc.reportedErrors.Add(var, null);
          }
        }
      }
      iv.SetValueNonDelayed(var);
    }

    private void CheckReturnNotDelay(InitializedVariables iv, Variable var, Node context) {
      if (var == null)
        return;
      if (!iv.CanAssumeNotDelayed(var) && !this.mpc.currentMethod.IsPropertyGetter) {
        if (!mpc.reportedErrors.Contains(var) && var.Name != null && var.Name.Name != "") {
          Error errorCode = Error.ReturnOfDelayedValue;
          Node offendingNode = (var.SourceContext.Document == null) ? context : var;
          mpc.typeSystem.HandleError(offendingNode, errorCode, var.Name.Name);
          mpc.reportedErrors.Add(var, null);
        }
      }
      iv.SetValueNonDelayed(var);
    }


    private void CheckUse(InitializedVariables iv, Variable var, Node context){
      if (var==null) 
        return;
      if(var is This)
        return;
      // HS D 
      if(var is Hole) 
        return;
      else if(! iv.IsAssigned(var)) {
        if(!mpc.reportedErrors.Contains(var) && var.Name != null && var.Name.Name!=""){
          if (var.Name.Name.Equals("return value")){
            mpc.typeSystem.HandleError(mpc.currentMethod.Name, Error.ReturnExpected, mpc.typeSystem.GetMemberSignature(mpc.currentMethod));
            mpc.reportedErrors.Add(var,null);
          }else{
            Node offendingNode = (var.SourceContext.Document == null)?context:var;
            mpc.typeSystem.HandleError(offendingNode, Error.UseDefViolation, var.Name.Name);
            mpc.reportedErrors.Add(var, null);
          }
          iv.SetReferencedAfterError(var);
          return;
        }
        iv.SetValueAssignedAndNonDelayed(var);
      }
      iv.SetReferenced(var);
    }

    private void CheckTargetDelayLongerThanSource(InitializedVariables iv, Variable pointer, Variable source, TypeNode type, Node context) {
      if (type.IsValueType) return;
      if (iv.TargetDelayLongerThanSource(pointer, source)) return;
      Node offendingNode = (source.SourceContext.Document == null) ? context : source;
      if ((pointer == null || !mpc.reportedErrors.Contains(pointer)) && !mpc.reportedErrors.Contains(source)) {
        if (source.Name != null) {
          mpc.typeSystem.HandleError(offendingNode, Error.StoreIntoLessDelayedLocation, source.Name.Name);
        }
      }
    }


    public bool IsUniversallyDelayed(Parameter p) {
      /// BUG BUG BUG: Some methods have no This parameter set where they should have one.
      if (p == null) return false;
      return p.IsUniversallyDelayed;
    }

    protected override object DefaultVisit(Statement stat, object arg) {
      // For debugging.
      mpc.typeSystem.HandleError(stat,Error.InternalCompilerError,"Definite Assignement: Instruction not implemented yet: "+stat.NodeType+":"+stat.SourceContext.SourceText);
      return null;
    }

    protected override object VisitMethodEntry(Method method, IEnumerable parameters, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      if (method is InstanceInitializer){
        iv.SetBaseUnassigned();
        MemberList members = this.analyzer.GetTypeView(InitializedVariables.Canonical(method.DeclaringType)).Members;
        for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++) {
          Field field = members[i] as Field;
          if (field != null && field.IsStrictReadonly)
            iv.SetUnassigned(field);
        }
      }
      foreach (Parameter p in parameters) {
        Debug.Assert(p != null);
        iv.SetLocationAssigned(p);
        TypeNode pType = TypeNode.StripModifiers(p.Type);
        if (IsUniversallyDelayed(p)) {
          if (pType is Reference && !method.IsFieldInitializerMethod) {
            if (p is This) {
              this.mpc.typeSystem.HandleError(method, Error.DelayedStructConstructor);
            }
            else {
              this.mpc.typeSystem.HandleError(p, Error.DelayedRefParameter);
            }
          }
          else {
            //System.Console.WriteLine(" considering {0}: Isout: {1}   isThis:{2}    method name:{3}", p, p.IsOut, (p is This), method.Name);
            iv.SetUniversallyDelayed(p,
              p.IsOut ||
              (p is This && (method is InstanceInitializer || method.IsFieldInitializerMethod)));
          }
        }
        else {
          if (p.IsOut || (p is This && (method is InstanceInitializer || method.IsFieldInitializerMethod))) {
            iv.SetValueNonDelayed(p);
          }
          else {
            iv.SetValueAssignedAndNonDelayed(p);
          }
        }
        // all parameters are assigned. Byref and out are addreses which are assigned.
        // For out parameters, their Value(loc) is not assigned, but for refs it is
        // Except, This parameter of struct constructors also need to be left unassigned!
        if ((pType is Reference && !(p.IsOut || p is This && method is InstanceInitializer && method.DeclaringType != null && method.DeclaringType.IsValueType))
          || (pType != null && pType.IsPointerType)) {
          iv.SetAssignedRef(p);
          iv.SetNonDelayedRef(p);
        }
      }
      return arg;
    }
    protected override object VisitLoadField(Variable dest, Variable source, Field field, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables) arg;
      CheckUse(iv, source, ((AssignmentStatement)stat).Source);

      // check special case for value type fields
      if (source != null && field.DeclaringType != null && field.DeclaringType.IsValueType) {
        // source is by ref pointer to value struct. Check AssignedRef as well
        if ( source.Type.NodeType == NodeType.Reference && !iv.IsAssignedStructField(source,field)) {
          Node offendingNode = (source.SourceContext.Document ==
      null)?(Node)((AssignmentStatement)stat).Source:(Node)source;
          mpc.typeSystem.HandleError(offendingNode, Error.UseDefViolationField, field.Name.Name);
          // set used and assigned
          iv.SetAssignedRef(source);
        }
        iv.SetUsedRef(source);
      }
      // if source is null, then this is static field
      if (source == null)
      {
        iv.SetLocationAssigned(dest);
        iv.SetValueAssignedAndNonDelayed(dest);
        return arg;
      }

      // Basically, if the field is a ref type, and the source is delayed (of any sort)
      // the dest will be an existential delayed type, which we cannot pass to a function
      // call because the parameter can never be existential quantified.
      // There are two possible usages of the existentially delayed pointer, use it to 
      // access a field or test its nulliness. The latter is always safe. The former should
      // cause a warning (of possible null pointer as receiver). If the field again is a pointer, then
      // the dest will get a new existential delay type.
      // 
      // Another issue is when an existential delayed pointer joins with another pointer. For example, a
      // pointer is existential delayed in one branch of an if and a non-delayed in the other, the result 
      // should be also existentially delayed. That is why in the current implementation, existential delayed
      // are treated the same as top. THIS DECISION (using top for existential delay) MUST BE KEPT AN EYE ON. 
      
      if (!(field.Type.IsPrimitive && field.Type.IsValueType))
      {
        // We will allow the source to be delayed. 
        if (iv.CanAssumeNotDelayed(source))
        {
          iv.SetLocationAssigned(dest);
          iv.SetValueAssignedAndNonDelayed(dest);
        }
        else
        {
          // note that the checking of possible null receiver happens with non-null analysis, that
          // is, if the non-null analysis notices that a ref is not null, then no warning will be
          // issued even if it is an existential delayed type and we access its member.
          Parameter p = source as Parameter;
          bool sourceIsParameter = (p != null);
          bool IsInSpecSharpGetFrameGuard = 
              (mpc.currentMethod.Name != null && 
              mpc.currentMethod.Name.Name.StartsWith("get_SpecSharp::FrameGuard"));
          if (sourceIsParameter && IsInSpecSharpGetFrameGuard)
          {
              iv.SetLocationAssigned(dest);
              iv.SetValueAssignedAndNonDelayed(dest); 
          }
          else if (iv.IsDelayed(source))
          {
            iv.SetLocationAssigned(dest);
            // iv.SetValueAssignedAndBottomDelayed(dest);
            // System.Console.WriteLine("Set {0} to existential delay. HashCode: {1}", dest, dest.GetHashCode());
            iv.SetExistentialDelay(dest);
          }
          else
          {
            // dubious possibility -- unlikely to happen
            // System.Console.WriteLine("---- {0} is neither delayed, nor not delayed", source);
            // Debug.Assert(false);
            iv.SetLocationAssigned(dest);
            iv.SetValueAssignedAndNonDelayed(dest);
          }
        }
      }
      else
      {
        // dest is assigned, it is a value, of course no delayed.
        iv.SetLocationAssigned(dest);
        iv.SetValueAssignedAndNonDelayed(dest);
      }

      // in non-null analysis, when we reach this point, we shall see if the source is an existential type
      // which may lead to a warning "receiver might be null". We ask the checker to collect existential typed
      // terms known at this location and put it in a table: (node, set of Variables) where node is source
      // here, while set of Variables are the existential typed (program and stack) variables.
      // for example, a.b.c will be: (1) loadfield stack0 a.b
      //                             (2) loadfield stack1 stack0.c
      // at (1), stack0 will be existential typed
      // at (2), stack0 is still be existential typed, if previously we have no knowledge (from non-null analysis) that stack0 is definitely
      // non null, we will raise a warning at (2)

      // TODO: what if a variable is top because it is assigned existential in one branch and something else in another?
      // it is possible: T t= ()? a.b: e.f; where a.b may be an existential type while e.f be a non-delay type?
      // if non-null analysis only looks at whether a variable is EXISTENTIAL delayed, that might not be enough
      // OR: we may simply signal an error when an unmatchable delay happens?
      
      mpc.CollectExistentialVars(stat, iv); // so that it can be used by non-null analysis
      
      return arg;
    }
    protected override object VisitStoreField(Variable dest, Field field, Variable source, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables) arg;
      CheckUse(iv, source, stat);
      CheckUse(iv, dest, ((AssignmentStatement)stat).Source);
      if (dest != null) {
        if (field.DeclaringType != null && field.DeclaringType.IsValueType) {
          // set field status as well.
          iv.SetAssignedStructField(dest, field);
        }
          // We care about tracking assignments to fields only when the method being
          // checked is a ctor. We also care only about fields on "this".
        else if (this.mpc.currentMethod is InstanceInitializer
          && !field.IsStatic
          && iv.IsAssigned(source)
          && iv.IsEqual(dest,this.mpc.currentMethod.ThisParameter)) {
          iv.SetAssignedClassField(dest,field);
        }
      }
      if (this.mpc.currentMethod is InstanceInitializer
        && field.IsStrictReadonly) {
        if (!iv.IsBaseUnassigned())
          this.mpc.typeSystem.HandleError(stat, Error.StrictReadonlyAssignment);
        if (!iv.IsUnassigned(field))
          this.mpc.typeSystem.HandleError(stat, Error.StrictReadonlyMultipleAssignment);
        iv.SetAssigned(field);
      }

      CheckTargetDelayLongerThanSource(iv, dest, source, field.Type, stat);

      // If we assign into a pointer typed location, we assume the address escaped and is assigned
      if (field.Type != null && field.Type.IsPointerType && source.Type != null && source.Type != null && !source.Type.IsValueType) {
        iv.SetAssignedRef(source);
      }
      return arg;
    }
    /// <summary>
    /// Loading an element of an array is like accessing a field of an object. 
    /// </summary>
    /// <returns></returns>
    protected override object VisitLoadElement(Variable dest, Variable source, Variable index, TypeNode elementType, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables) arg;
      CheckUse(iv, source, ((AssignmentStatement)stat).Source);
      CheckUse(iv, index, ((AssignmentStatement)stat).Source);
     
      Debug.Assert(elementType != null);
      if (!(elementType.IsPrimitive && elementType.IsValueType)) {
        if (iv.CanAssumeNotDelayed(source)) {
          iv.SetLocationAssigned(dest);
          iv.SetValueAssignedAndNonDelayed(dest);
        }
        else {
          if (iv.IsDelayed(source)) {
            iv.SetLocationAssigned(dest);
            iv.SetExistentialDelay(dest);
          }
          else {
            iv.SetLocationAssigned(dest);
            iv.SetValueAssignedAndNonDelayed(dest);
          }
        }
      }
      else {
        iv.SetLocationAssigned(dest);
        iv.SetValueAssignedAndNonDelayed(dest);
      }

      mpc.CollectExistentialVars(stat, iv);

      return arg;
    }

    protected override object VisitStoreElement(Variable dest, Variable index, Variable source, TypeNode elementType, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables) arg;
      CheckUse(iv, source, stat);
      CheckTargetDelayLongerThanSource(iv, dest, source, elementType, stat);
      CheckUse(iv, index, stat);
      CheckUse(iv, dest, stat);
      return arg;
    }

    protected override object VisitCastClass(Variable dest, TypeNode type, Variable source, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables) arg;
      CheckUse(iv, source, stat);

      iv.CopyVariable(dest, source);
      return arg;
    }

    protected override object VisitBox(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables) arg;
      CheckUse(iv, source, stat);
      iv.SetLocationAssigned(dest);
      iv.SetValueAssignedAndNonDelayed(dest);
      return arg;
    }

    protected override object VisitUnbox(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables) arg;
      CheckUse(iv, source, stat);
      CheckNonDelay(iv, source, stat, Error.UnboxDelayedValue);
      iv.SetLocationAssigned(dest);
      iv.SetValueAssignedAndNonDelayed(dest);
      // since result is a pointer that we will deref, mark it as assigned.
      iv.SetAssignedRef(dest);
      return arg;
    }

    protected override object VisitUnboxAny(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables) arg;
      CheckUse(iv, source, stat);
      CheckNonDelay(iv, source, stat, Error.UnboxDelayedValue);
      iv.SetLocationAssigned(dest);
      iv.SetValueAssignedAndNonDelayed(dest);
      return arg;
    }

    /// <summary>
    /// Figure out how to instantiate the quantified delay of the method parameters (if any).
    /// We assume that each method has the form \forall T. where T is a delay. Parameters with the "Delayed" attribute
    /// are assumed to have type Delay(T). 
    /// Given the concrete arguments, this method determines if any parameter whose formal is delayed is actually delayed.
    /// [existential delay change]
    /// In addition, put the checking of existential delayed actuals here
    /// For error diagnostics purpose, we also note the position of the first delay-matched argument
    /// </summary>
    /// <param name="receiver"></param>
    /// <param name="callee"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    public bool MethodCallDelayInstance(InitializedVariables iv, Variable receiver, Method callee, 
      ExpressionList arguments, Node context, out int witness)
    {
      Node off = receiver;
      witness = -1;

      if (receiver != null && this.IsUniversallyDelayed(callee.ThisParameter) && iv.IsDelayed(receiver)) {
        if (witness == -1) witness = 0;
      }
      if (callee.Parameters == null || arguments == null)
      {
        // dont have more parameters, not possible to have MoreThan2ExistentialDelay
        return (witness != -1);
      }
      for (int i = 0; i<callee.Parameters.Count; i++) {
        Parameter p = callee.Parameters[i];
        Variable arg = (Variable)arguments[i];
        if (this.IsUniversallyDelayed(p) && iv.IsDelayed(arg)) {
          if (iv.IsExistentialDelayed(arg))
          {
            if (witness != -1)
            {
              // report error
              Node offendingNode = (arg.SourceContext.Document != null) ? arg : context;
              mpc.typeSystem.HandleError(offendingNode, Error.ActualMustBeDelayed, (i+1).ToString(), 
                (witness == 0) ? "this(receiver)" : "No." + witness.ToString());
            };
            off = arg;
          }
          if (witness == -1) witness = i + 1;
        }
      }

      // System.Console.WriteLine("result is:{0}, numDelayMatch is {1}", result, numDelayMatch);
      return (witness != -1);
    }
    private bool ThisIsSoleDelayedParameter(Method callee) {
      if (callee.ThisParameter == null || !this.IsUniversallyDelayed(callee.ThisParameter)) return false;
      for (int i = 0; callee.Parameters != null && i < callee.Parameters.Count; i++) {
        if (this.IsUniversallyDelayed(callee.Parameters[i])) return false;
      }
      return true;
    }
    /// <summary>
    /// The instantiated formal type is non-delayed unless the parameter is marked delayed and "delayedInstance" is true.
    /// [Existential Delay Change] When there is only one delayed argument, we allow it to be existential delayed
    /// This is done by keeping track of the number of possible delay matches. 
    /// if actual/formal is universal/universal, then this number (possibleDelayMatch) is not changed, we allow it only when
    ///      the number is not zero (the only way it can be zero at this point (delayinstance is true) is because
    ///      an existential/universal delay match happened.
    /// if actual/formal is existential/universal, then match happens only when the number is 1
    ///      number is changed to zero to disallow future existential/universal match
    /// Delay match error will be reported as delay not compatible with the witness. If the witness does not have an appropriate delay
    /// we do not report an error, which will be safe (see comments below).
    /// </summary>
    private void CheckParameterDelay(InitializedVariables iv, Variable actual, Method callee, Parameter p, 
      bool delayedInstance, Node context,
      int witness, int num) {
      if (actual == null) return;
      if (p == null) return;

      // Special check that we don't pass pointers to delayed objects by reference
      Reference r = p.Type as Reference;
      if (r != null && !r.ElementType.IsValueType) {
        if (iv.IsDelayed(actual) || iv.IsDelayedRefContents(actual)) {
          Node offendingNode = (actual.SourceContext.Document != null) ? actual : context;
          mpc.typeSystem.HandleError(offendingNode, Error.DelayedReferenceByReference);
          return;
        }
      }

      // delayed instance requires that for every delayed formal, the corresponding actual must
      // be committed to an appropriate delay type. 
      if (delayedInstance && this.IsUniversallyDelayed(p)) {
        // CanAssumeDelay Decides whether the actual 1) is of the same delay (universal delay) as the formal
        //                                        or 2) is bottom, thus can be assumed to be universal delay afterwards
        // CanAssumeDelay cannot decide, and thus returns false when actual is existentially delayed
        bool actualIsSameDelayAsFormal = iv.CanAssumeDelayed(actual);

        if (actualIsSameDelayAsFormal) { 
          return;
        }

        if (iv.IsExistentialDelayed(actual))
        {
          // errors, if any, has been handled already in MethodCallDelayInstance
          // return here to avoid falling into the error reporting below. 
          return;
        }

        // if p is a universally delayed parameter, while actual cannot take a delayed type, 
        // (e.g., it is top), report error
        Node offendingNode = (actual.SourceContext.Document != null) ? actual : context;
        if (p is This) {
          mpc.typeSystem.HandleError(offendingNode, Error.ReceiverMustBeDelayed);
        }
        else {
          if (num != witness) // note this is not an extra check, the fact that witness is the position of the
            // first known delayed actual doesnt mean it will match correctly (i.e., possibly delay match might
            // not be right. 
            // We are not losing warnings either because if num == witness, only if possiblyDelayMatch == 1
            // we will have no warning, in which case, we are guaranteed to have a delay match and won't reach here
          {
            mpc.typeSystem.HandleError(offendingNode, Error.ActualMustBeDelayed, num.ToString(), (witness==0)?"this(receiver)": "No."+witness.ToString());
          }
        }
        return;
      }
      else {
        /* 
        if (callee is InstanceInitializer && p is This && iv.IsBottom(actual)) {
          if (ThisIsSoleDelayedParameter(callee)) {
            // leave undetermined status of this.
            return;
          }
        }
        */
        // actual is known to be not delayed. CanAssumeNotDelay has this side effect to change
        // actual's delay type from bottom to nondelay if its type is not already nondelay
        if (iv.CanAssumeNotDelayed(actual)) {
          return;
        }
        // if its type is delayed, or top, report error... it is assumed that actual's type cannot 
        // be top, which, when violated (likely to happen), leads to inaccurate error message.
        // TODO: distinguish between actual's type being delayed or being top.
        Node offendingNode = (actual.SourceContext.Document != null) ? actual : context;
        if (p is This) {
        //  System.Console.WriteLine("p is Universally delayed: {0}", this.IsUniversallyDelayed(p));
        //  System.Console.WriteLine("delayInstance is {0}", delayedInstance);
        //  System.Console.WriteLine("{0} violates delay of {1}", actual, p);
        //  System.Console.WriteLine("Value of possible delay match:{0} ", possibleDelayMatch);
          mpc.typeSystem.HandleError(offendingNode, Error.ReceiverCannotBeDelayed);
        }
        else {
          //System.Console.WriteLine("{0} is offending {1} for function {2}", actual, p, callee.Name);
          //System.Console.WriteLine("delayinstance:{0}; possibleDelayMatch:{1}", delayedInstance, possibleDelayMatch);
          //iv.PrintStatus(actual);
          //System.Console.WriteLine("p is universally delayed: {0}", this.IsUniversallyDelayed(p));
          mpc.typeSystem.HandleError(offendingNode, Error.ActualCannotBeDelayed);
        }
        return;
      }
    }
    private void FixupNewlyConstructedObjectDelay(InitializedVariables iv, Variable receiver, InstanceInitializer ctor) {
      if (receiver == null) return;
      if (ctor == null) return;
    }

    /// <summary>
    /// Returns an arraylist of delayed parameters of the current method, based on their status in the analysis
    /// (not based on whether they are declared as delayed).  
    /// </summary>
    /// <param name="ivs"> the state of variable-initialization (def-assign) analysis </param>
    /// <returns>Returns an arraylist of program variables that are delayed, according to a particular var-initialization
    /// analysis state</returns>
    private ArrayList delayedParameters (InitializedVariables ivs) {
      ArrayList result = new ArrayList();
      if (ivs == null) return result;
      if (this.mpc.currentMethod == null) return result;
      if (this.mpc.currentMethod.Parameters == null) return result;
      foreach (Parameter p in this.mpc.currentMethod.Parameters) {
        if (ivs.IsDelayed(p)) {
          result.Add(p);
        }
      }
      if (!this.mpc.currentMethod.IsStatic) {
        if (ivs.IsDelayed(this.mpc.currentMethod.ThisParameter)) {
          result.Add(this.mpc.currentMethod.ThisParameter);
        }
      }
      return result;
    }

    /// <summary>
    /// See if a type (and all its base types) contains a non-null field.
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    private bool hasNonNullFields(TypeNode t) {
      MemberList ml = t.Members;
      foreach (Member m in ml) {
        if (m is Field) {
          Field f = m as Field;
          TypeNode fieldType = f.Type;
          if (this.mpc.typeSystem.IsNonNullType(fieldType)) {
            return true;
          }
        }
      }

      TypeNode parentType = t.BaseType;
      while (parentType != null) {
        if (hasNonNullFields(parentType))
          return true;
        parentType = parentType.BaseType;
      }

      return false;
    }

    /// <summary>
    /// See if any of the constructors of a given type (possibly) accepts a delayed parameter. 
    /// If a class is not sealed, it may have a derived class that, for example, has a constructor
    /// accepting a delayed parameter.
    /// We conservatively consider this case as "may have" and return true.
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    private bool hasConstructorAcceptingDelay(TypeNode t) {
      MemberList ml = t.GetConstructors();
      if (!t.IsSealed) return true; 
      if (ml.Count == 0) return false;
      foreach (Member m in ml) {
        if (m is InstanceInitializer) {
          InstanceInitializer ii = m as InstanceInitializer;
          
          ParameterList pl = ii.Parameters;
          foreach (Parameter p in pl) {
            if (p is This) continue; // will "this" show up here?
            if (p.IsUniversallyDelayed) {
              return true;
            }
          }

          // no need to look at base types
          /*
          TypeNode parentType = t.BaseType;
          while (parentType != null) {
            if (hasConstructorAcceptingDelay(parentType)) {
              return true;
            }
            parentType = parentType.BaseType;
          }
          */
          
        }
      }
      return false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <returns>
    /// true if 1) current method deos not contain a delayed parameter
    /// or 2) the type of concern does not contain a constructor that accepts delayed parameters otherthan 
    /// "this"
    /// </returns>
    private bool delayedParametersNoEffect(TypeNode type, InitializedVariables ivs, PreDAStatus nns, Statement s) {
      if (type == null) return false;
      bool result = false;
      // see if this element type can possibly 
      // have a constructor that accepts delayed parameters
      result = result || (!hasConstructorAcceptingDelay(type));
      result = result || nns.OKToCommit(s);
      return result;
    }

    protected override object VisitCall(Variable dest, Variable receiver, Method callee, ExpressionList arguments, bool virtcall, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables) arg;
 
      Method calleeTemplate = (callee.Template != null)?callee.Template:callee;
      int witness;
      bool delayedInstance = this.MethodCallDelayInstance(iv, receiver, calleeTemplate, arguments, stat,
         out witness);

      CheckUse(iv, receiver, stat);

      // if the current method call is NonNullType.AssertInitialized, we check to see
      // if there are no non-delayed values around, including other committed NN element arrays.
      // if so, we set all these arrays to be non-delayed, meaning that their initialization is finished.
      if (callee != null && 
          (callee == SystemTypes.NonNullTypeAssertInitialized
          || (callee.Template!= null && callee.Template == SystemTypes.NonNullTypeAssertInitializedGeneric)
          )
        ) {
        ArrayType at = arguments[0].Type as ArrayType;
        if (delayedParametersNoEffect(at.ElementType, iv, nns, stat)) {
          Variable v = arguments[0] as Variable;

          iv.SetBottomDelayed(v);

          // foreach of those committed array vars, set them to be assigned but not delayed.
          foreach (object o in nns.committedPriorTo(stat)) {
            if (o is Variable) {
              Variable var = o as Variable;
              iv.SetBottomDelayed(var);
              continue;
            }

            if (o is Pair<Variable, Field>) {
              Pair<Variable, Field> pair = o as Pair<Variable, Field>;
              iv.SetBottomDelayedClassField(pair.Fst, pair.Snd);
            }
          }
          return arg;
        }
        else {
        }
      }
    
      if (callee.DeclaringType != null && callee.DeclaringType.IsValueType && receiver != null && receiver.Type.NodeType == NodeType.Reference && !(callee is InstanceInitializer)) {
        // receiver passed by ref, check RefAssigned.
        if ( !iv.IsAssignedRef(receiver, callee.DeclaringType as Struct)) {
          bool local;
          string unassignedLocationPath = iv.GetPathMappingToRef(receiver, out local);
          if (unassignedLocationPath == null) {
            Debug.Assert(false, "couldn't resolve unassigned location");
          }
          else {
            Error errorCode = local ? Error.UseDefViolation : Error.UseDefViolationField;
            mpc.typeSystem.HandleError(receiver, errorCode, unassignedLocationPath);
          }
          // avoid followup errors
          iv.SetAssignedRef(receiver);
        }
        // set indirect used 
        iv.SetUsedRefDeep(receiver);
      }
      // Only check delay after assignment status.

      if (!virtcall && callee.DeclaringType == SystemTypes.Object) {
        // Special case for non-virtual calls on Object. These methods (such as GetHashCode) treat the object delayed
      }
      else {
        CheckParameterDelay(iv, receiver, calleeTemplate, calleeTemplate.ThisParameter, delayedInstance, stat, witness, 0);
      }

      // Parameter matching.
      if (arguments!=null && calleeTemplate.Parameters != null) {
        for(int j=0;j<calleeTemplate.Parameters.Count;j++) {
          Variable v = arguments[j] as Variable;
          if (v == null) continue;
          
          CheckUse(iv, v, stat);
          Parameter p = calleeTemplate.Parameters[j];
          CheckParameterDelay(iv, v, calleeTemplate, p, delayedInstance, stat, witness, j+1);
          if (p == null || p.Type == null) continue;
          if (! p.IsOut && p.Type.NodeType == NodeType.Reference) {
            // by ref
            // check that value is assigned
            Variable actual = (Variable)arguments[j];

            if ( actual.Type.NodeType == NodeType.Reference && ! iv.IsAssignedRef(actual, ((Reference)p.Type).ElementType as Struct)) {
              bool localVar;
              string unassignedLocPath = iv.GetPathMappingToRef(actual, out localVar);
              if (unassignedLocPath == null) {
                Debug.Assert(false, "couldn't resolve unassigned location");
              }
              else {
                Error errorCode = localVar ? Error.UseDefViolation : Error.UseDefViolationField;
                mpc.typeSystem.HandleError(actual, errorCode, unassignedLocPath);
              }
            }
          }
        }
      }

      //Updating all reference parameter to be assigned and used
      if (arguments!=null && calleeTemplate.Parameters != null) {
        for(int j=0;j<calleeTemplate.Parameters.Count;j++) {
          Parameter p = calleeTemplate.Parameters[j];
          if (p == null || p.Type == null) continue;
          if (p.Type != null && (p.Type.NodeType == NodeType.Reference || p.Type.IsPointerType)) {
            Variable actual = (Variable)arguments[j];
            iv.SetAssignedRef(actual);
            iv.SetNonDelayedRef(actual);
            iv.SetUsedRefDeep(actual);
          }
        }
      }
      // Dest Variable.
      if (dest != null) {
        iv.SetLocationAssigned(dest);
        // If we call a getter and the getter receiver is delayed, treat it as a field. (Ignore getFrameGuard)
        if (callee.IsPropertyGetter && receiver != null && iv.IsDelayed(receiver) && callee.Name != null && !callee.Name.Name.StartsWith("get_SpecSharp::FrameGuard")) {
          // treat result delayed
          iv.SetExistentialDelay(dest);
        }
        else {
          iv.SetValueAssignedAndNonDelayed(dest);
        }
      }
      // Special handling of base and this calls in constructors
      if (InConstructor && CallOnThis(iv, receiver)) {
        if (BaseCtorCall(callee)) {
          // Make sure a constructor calls this or base only once
          if (!iv.IsBaseUnassigned())
            this.mpc.typeSystem.HandleError(stat, Error.BaseMultipleInitialization);
          iv.SetBaseAssigned();
          // Check that fields are initialized unless we are in a delayed ctor
          if (!this.IsUniversallyDelayed(this.mpc.currentMethod.ThisParameter)) {
            CheckCtorNonNullFieldInitialization(receiver, stat, iv, Error.NonNullFieldNotInitializedBeforeConstructorCall);
          }
        }
        else if (ThisCtorCall(callee)) {
          // update this if we are in a value type constructor and calling a this(.) other constructor.
          // Special case: if the class is sealed and the constructor takes not other delayed
          // parameters, then after the This call, the object is fully initialized, so we can 
          // clear the delay status.
          iv.SetAssignedRef(this.mpc.currentMethod.ThisParameter, 
                            !this.mpc.currentMethod.DeclaringType.IsSealed &&
                            this.ThisIsSoleDelayedParameter(this.mpc.currentMethod));
          // Make sure a constructor calls this or base only once
          if (!iv.IsBaseUnassigned())
            this.mpc.typeSystem.HandleError(stat, Error.BaseMultipleInitialization);
          iv.SetBaseAssigned();
        }
      }

      if (dest != null) {
        TypeNode bareReturnType = TypeNode.StripModifiers(callee.ReturnType);
        // special case for weird framework functions that return references (like Array.Address)
        if (bareReturnType != null && bareReturnType.NodeType == NodeType.Reference) {
          iv.SetAssignedRef(dest);
          iv.SetNonDelayedRef(dest);
        }

        // assume returned pointer types point to something initialized
        if (bareReturnType != null && bareReturnType.IsPointerType) {
          iv.SetAssignedRef(dest);
          iv.SetNonDelayedRef(dest);
        }
      }
      return arg;
    }
    private bool InConstructor {
      get { return this.mpc.currentMethod is InstanceInitializer; }
    }
    private bool CallOnThis(InitializedVariables iv, Variable receiver) {
      if (receiver == null) return false;
      if (this.mpc.currentMethod.ThisParameter != null
          && iv.IsEqual(receiver, this.mpc.currentMethod.ThisParameter)) {
        return true;
      }
      return false;
    }
    private bool ThisCtorCall(Method callee) {
      if (callee is InstanceInitializer) {
        TypeNode calleeType = InitializedVariables.Canonical(callee.DeclaringType);
        TypeNode thisType = InitializedVariables.Canonical(this.mpc.currentMethod.DeclaringType);
        if (thisType != null && thisType == calleeType) {
          return true;
        }
      }
      return false;
    }
    private bool BaseCtorCall(Method callee) {
      if (callee is InstanceInitializer) {
        Class thisClass = InitializedVariables.Canonical(this.mpc.currentMethod.DeclaringType).EffectiveTypeNode as Class;
        if (thisClass != null && thisClass.BaseClass == callee.DeclaringType) {
          return true;
        }
      }
      return false;
    }
    public void CheckCtorNonNullFieldInitialization(Variable receiver, Node context, InitializedVariables iv, Error errorCode) {
      TypeSystem ts = this.mpc.typeSystem;
      Method currMethod = this.mpc.currentMethod;
      MemberList members = this.analyzer.GetTypeView(InitializedVariables.Canonical(currMethod.DeclaringType)).Members;
      for (int i = 0, n = members.Count; i < n; i++) {
        Field f = members[i] as Field;
        if (f == null) continue;
        if (f.IsStatic) continue; // don't worry about static fields here, this is only for instance fields
        if (!ts.IsNonNullType(f.Type)) continue;
        //if (f.IsModelfield) continue; //modelfields are assigned to implicitly, by a pack. But what about non-null modelfields and explicit superclass constructor calls?
        if (!iv.IsAssignedStructField(receiver, f)) {
          if (currMethod.HasCompilerGeneratedSignature) {
            // then it was a default ctor created for the class and the error message
            // should point at the field's declaration
            if (!(currMethod.DeclaringType is ClosureClass))
              ts.HandleError(f, Error.NonNullFieldNotInitializedByDefaultConstructor, ts.GetMemberSignature(f));
          }
          else {
            // the error message should point at the explicit base call that the user wrote or the one
            // inserted by the compiler
            ts.HandleError(context, errorCode, ts.GetMemberSignature(f));
          }
        }
      }
    }


    protected override object VisitConstrainedCall(Variable dest, Variable receiver, Method callee, ExpressionList arguments, TypeNode constraint, Statement stat, object arg) {
      InitializedVariables iv = (InitializedVariables)arg;

      CheckUse(iv, receiver, stat);

      // receiver passed by ref, check RefAssigned.
      if (!iv.IsAssignedRef(receiver, callee.DeclaringType as Struct))
      {
        bool localVar;
        string unassignedLocPath = iv.GetPathMappingToRef(receiver, out localVar);
        if (unassignedLocPath == null)
        {
          Debug.Assert(false, "couldn't resolve unassigned location");
        }
        else
        {
          Error errorCode = localVar ? Error.UseDefViolation : Error.UseDefViolationField;
          mpc.typeSystem.HandleError(receiver, errorCode, unassignedLocPath);
        }
        // avoid followup errors
        iv.SetAssignedRef(receiver);
      }
      // set indirect used 
      iv.SetUsedRefDeep(receiver);
      return VisitCall(dest, receiver, callee, arguments, true, stat, arg);
    }

    protected override object VisitCopy(Variable dest, Variable source, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables) arg;
      CheckUse(iv, source, stat);
      iv.CopyVariable(dest,source);
      return arg;
    }

    protected override object VisitLoadAddress(Variable dest, Variable source, Statement stat, object arg) {
      InitializedVariables iv = (InitializedVariables)arg;
      iv.CopyAddress(dest, source);
      return arg;
    }

    protected override object VisitThrow(Variable var, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, var, stat);
      CheckNonDelay(iv, var, stat, Error.ThrowsDelayedValue);
      return null;
    }

    protected override object VisitCatch(Variable var, TypeNode type, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;

      iv.SetLocationAssigned(var);
      iv.SetValueAssignedAndNonDelayed(var);
      return arg;
    }

    protected override object VisitBinaryOperator(NodeType op, Variable dest, Variable operand1, Variable operand2, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, operand1, operand1);
      CheckUse(iv, operand2, operand2);
      if (operand1.Type != null && !operand1.Type.IsValueType) {
        iv.SetAssignedAndCopyDelay(dest, operand1);
      }
      else if (operand2.Type != null && !operand2.Type.IsValueType) {
        iv.SetAssignedAndCopyDelay(dest, operand2);
      }
      else {
        iv.SetLocationAssigned(dest);
      }
      return arg;
    }

    protected override object VisitInitObj(Variable addr, TypeNode valueType, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, addr, stat);
      iv.SetAssignedRef(addr);
      iv.SetNonDelayedRef(addr);
      return arg;
    }

    protected override object VisitArgumentList(Variable dest, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      iv.SetLocationAssigned(dest);
      return arg;
    }

    protected override object VisitBreak(Statement stat, object arg) {
      return arg;
    }

    protected override object VisitEndFilter(Variable code, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, code, stat);
      return arg;
    }

    protected override object VisitFilter(Variable dest, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      iv.SetLocationAssigned(dest);
      return arg;
    }

    protected override object VisitMakeRefAny(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, source, stat);
      CheckNonDelay(iv, source, stat, Error.CannotUseDelayedPointer);
      iv.SetLocationAssigned(dest);
      iv.SetValueAssignedAndNonDelayed(dest);
      return arg;
    }

    protected override object VisitRefAnyType(Variable dest, Variable source, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, source, stat);
      CheckNonDelay(iv, source, stat, Error.CannotUseDelayedTypedRef);
      iv.SetLocationAssigned(dest);
      iv.SetValueAssignedAndNonDelayed(dest);
      return arg;
    }

    protected override object VisitRefAnyValue(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, source, stat);
      CheckNonDelay(iv, source, stat, Error.CannotUseDelayedTypedRef);
      iv.SetLocationAssigned(dest);
      iv.SetValueAssignedAndNonDelayed(dest);
      iv.SetAssignedRef(dest);
      iv.SetNonDelayedRef(dest);
      return arg;
    }


    protected override object VisitInitializeBlock(Variable addr, Variable val, Variable size, Statement stat, object arg) {
      InitializedVariables iv = (InitializedVariables)arg;
      CheckUse(iv, addr, stat);
      CheckUse(iv, val, stat);
      CheckUse(iv, size, stat);
      return arg;
    }

    protected override object VisitLoadElementAddress(Variable dest, Variable array, Variable index, TypeNode elementType, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, array, stat);
      CheckUse(iv, index, stat);

      // changes... accessing an array is like acessing a field. If this element is delayed, we 
      // need to note this. If non-null analysis chooses to use this information, it will be available.
      // Currently, this is mainly for ref parameters.
      if (iv.IsDelayed(array)) {
        iv.SetLocationAssigned(dest);
        iv.SetAssignedRef(dest);
        iv.SetValueExistentialDelayed(dest);
      }
      else {
        iv.SetLocationAssigned(dest);
        iv.SetValueNonDelayed(dest);
        iv.SetAssignedRef(dest);
      }
      return arg;
    }

    protected override object VisitSizeOf(Variable dest, TypeNode value_type, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      iv.SetLocationAssigned(dest);
      return arg;
    }

    protected override object VisitCallIndirect(Variable dest, Variable callee, Variable receiver, Variable[] arguments, FunctionPointer fp, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, callee, stat);
      CheckUse(iv, receiver, stat);
      foreach (Variable v in arguments) {
        CheckUse(iv, v, stat);
      }
      iv.SetLocationAssigned(dest);
      iv.SetNonDelayedRef(dest);
      return arg;
    }

    protected override object VisitIsInstance(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, source, stat);
      iv.SetAssignedAndCopyDelay(dest, source);
      return arg;
    }

    protected override object VisitLoadFieldAddress(Variable dest, Variable source, Field field, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, source, stat);
      // We care about tracking field addresses of non-null fields only when the method being
      // checked is a ctor because we are tracking it just to make sure they are initialized
      // before the base ctor is called. We also care only about fields on "this".
      // We also track addresses of struct fields
      iv.AssignFieldAddress(dest, field, source, this.mpc.currentMethod, this.mpc.typeSystem);
      
      return arg;
    }

    protected override object VisitLoadFunction(Variable dest, Variable source, Method method, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, source, stat);
      iv.SetLocationAssigned(dest);
      iv.SetValueNonDelayed(dest);
      return arg;
    }

    protected override object VisitLoadIndirect(Variable dest, Variable pointer, TypeNode type, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, pointer, stat);
      if (pointer.Type is Reference) {
        // With pointers, we are having problems, since they materialize out of thin air.
        CheckNonDelay(iv, pointer, stat, Error.CannotUseDelayedPointer);
      }

      // check that value of cell is assigned
      if ( pointer.Type != null && !pointer.Type.IsPointerType && pointer.Type != SystemTypes.IntPtr && pointer.Type != SystemTypes.UIntPtr && !iv.IsAssignedRef(pointer, type as Struct)) {
        bool localVar;
        string unassignedLocPath = iv.GetPathMappingToRef(pointer, out localVar);
        if (unassignedLocPath == null) {
          Debug.Assert(false, "couldn't resolve unassigned location");
        }
        else {
          Error errorCode = localVar ? Error.UseDefViolation : Error.UseDefViolationField;
          mpc.typeSystem.HandleError(pointer, errorCode, unassignedLocPath);
        }
      }

      iv.LoadIndirect(dest, pointer);
      return arg;
    }

    protected override object VisitNewArray(Variable dest, TypeNode type, Variable size, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;

      CheckUse(iv, size, stat);
      iv.SetLocationAssigned(dest);
      if (type.IsValueType) {
        iv.SetValueAssignedAndNonDelayed(dest);
      }
      else {
        RequiredModifier rm = type as RequiredModifier;
        if (rm != null && rm.Modifier == ExtendedRuntimeTypes.DelayedAttribute) {
          iv.SetUniversallyDelayed(dest, false);
        }
        else {
          // if the array being constructed is a non-null element array, we set this array to be
          // universally delayed, meaning that we cannot safely access its elements until proper
          // initialization (signalled by a commitment: call to NonNullType.AssertInitialized(array)).
          if (mpc.typeSystem.IsPossibleNonNullType(type)) {
            // add a small provision that if the size is zero, then it is definitely
            // NonDelayed
            if (nns.IsNonDelayedArray(stat)) {
              iv.SetValueNonDelayed(dest);
            } else {
              iv.SetUniversallyDelayed(dest, false);
            }
          }
          else {
            iv.SetValueNonDelayed(dest);
          }
        }
      }
      return arg;
    }

    protected override object VisitNewObject(Variable dest, TypeNode type, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      iv.SetLocationAssigned(dest);
      iv.SetBottomDelayed(dest);
      
      return arg;
    }

    protected override object VisitStoreIndirect(Variable pointer, Variable source, TypeNode type, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, source, stat);
      CheckUse(iv, pointer, stat); // for debugging, should always be assigned in managed code
      this.CheckTargetDelayLongerThanSource(iv, pointer, source, type, stat);
      iv.SetAssignedRef(pointer);
      return arg;
    }

    protected override object VisitUnaryOperator(NodeType op, Variable dest, Variable operand, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, operand, stat);
      if (operand.Type != null && (operand.Type.IsPointerType || (operand.Type is Reference))) {
        switch (op) {
          case NodeType.Conv_I:
          case NodeType.Conv_U:
            // consider ref/ptr assigned to and used
            iv.SetAssignedRef(operand);
            iv.SetUsedRef(operand);
            break;
        }
      }
      if (dest.Type != null && !dest.Type.IsValueType) {
        iv.SetAssignedAndCopyDelay(dest, operand);
      }
      else {
        iv.SetLocationAssigned(dest);
      }
      return arg;
    }

    protected override object VisitLoadNull(Variable dest, Literal source, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      iv.SetLocationAssigned(dest);
      iv.SetValueAssignedAndBottomDelayed(dest);
      return arg;
    }

    protected override object VisitLoadConstant(Variable dest, Literal source, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      iv.SetLocationAssigned(dest);
      iv.SetValueAssignedAndNonDelayed(dest);
      return arg;
    }

    protected override object VisitRethrow(Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      return null;
    }

    protected override object VisitCopyBlock(Variable destaddr, Variable srcaddr, Variable size, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, srcaddr, stat);
      CheckUse(iv, size, stat);
      CheckUse(iv, destaddr, stat);
      return arg;
    }

    protected override object VisitLoadToken(Variable dest, object token, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      iv.SetLocationAssigned(dest);
      iv.SetValueAssignedAndNonDelayed(dest);
      return arg;
    }
    protected override object VisitBranch(Variable cond, Block target, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, cond, stat);
      return arg;
    }

    protected override object VisitAssertion(Assertion assertion, object arg) {
      return arg;
    }
    protected override object VisitSwitch(Variable selector, BlockList targets, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, selector, stat);
      return arg;
    }

    protected override object VisitSwitchCaseBottom(Statement stat, object arg) {
      this.mpc.typeSystem.HandleError(stat, Error.CaseFallThrough);
      return null;
    }

    protected override object VisitReturn(Variable var, Statement stat, object arg) {
      InitializedVariables iv=(InitializedVariables)arg;
      CheckUse(iv, var, stat);
      CheckReturnNotDelay(iv, var, stat);
      ArrayList notCommitted = nns.createdButNotCommittedPriorTo(stat);
      if (notCommitted.Count != 0) {
        if (Analyzer.Debug) {
          Console.WriteLine("There are {0} uncommitted arrays in method {1}", notCommitted.Count, 
            this.mpc.currentMethod.FullName);
          for (int i = 0; i < notCommitted.Count; i++) {
            Variable v = notCommitted[i] as Variable;
            if (v != null) {
              Console.WriteLine("it is var {0}", v);
            }
            else {
              Console.WriteLine("it is a field");
            }
          }
        }
      }
      return arg;
    }

    protected override object VisitNop(Statement stat, object arg) {
      return arg;
    }
    protected override object VisitUnwind(Statement stat, object arg) {
      return arg;
    }
  }
}

