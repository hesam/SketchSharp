//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
#if CCINamespace
using Microsoft.Cci;
using SysError = Microsoft.Cci.Error;
using CciChecker = Microsoft.Cci.Checker;
#else
using System.Compiler;
using SysError = System.Compiler.Error;
using CciChecker = System.Compiler.Checker;
using System.Compiler.WPurity;
using System.Collections.Generic;
using Microsoft.Contracts;
#endif
using System;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace Microsoft.SpecSharp {
#endif

  
  #region AdmissibilityChecker
  /// <summary>
  /// This class performs admissibility checks on invariants, specifications of pure methods and satisfies clauses of model fields.   
  /// </summary>
  public class AdmissibilityChecker : StandardVisitor {
    ErrorHandler ErrorHandler;
    Member DeclaringMember; // the inv or modelfield(representation) that we check
    Method DeclaringMethod;          // the method which declares the specs that we check
    ModelfieldContract DeclaringMfC;  //the modelfield contract that we check (null when not checking a modelfieldcontract)
    //^ invariant (DeclaringMember != null XOR DeclaringMethod != null);
    System.Compiler.Checker checker;

    System.Collections.Stack StateStack;
    AdmissibilityState st; // current state
    bool hasError;
    bool DontReportError;  // don't report error at all: we just want to check admissibility and record result in HasError
    bool ReportWFErrorOnly; // report only well-foundedness problem: this is what we need for Pure methods
    bool QuantifierBinding;
    bool IndexerAccess;
    bool TargetOutsideOwnedCone;

    public AdmissibilityChecker(System.Compiler.Checker callingChecker)
      : base(callingChecker) {
      this.ErrorHandler = (ErrorHandler)callingChecker.ErrorHandler;
      this.checker = callingChecker;
    }

    public void CheckInvariantAdmissibility(Invariant inv) {
      DeclaringMember = inv;
      StateStack = new System.Collections.Stack();
      ResetCurrentState();
      this.VisitExpression(inv.Condition);
    }

    public void CheckModelfieldAdmissibility(ModelfieldContract mfC) {
      this.DeclaringMfC = mfC;
      this.DeclaringMember = mfC.Modelfield;
      if (this.DeclaringMember is Property)
        this.DeclaringMember = (this.DeclaringMember as Property).Getter; //references to the modelfield have been resolved as bindings to this getter
      foreach (Expression satExpr in mfC.SatisfiesList) {
        StateStack = new System.Collections.Stack();
        ResetCurrentState();
        this.VisitExpression(satExpr);
      }
    }

    public void CheckMethodSpecAdmissibility(Expression exp, Method method, bool reportWFonly, bool dontReport) {
      DeclaringMethod = method;
      ReportWFErrorOnly = reportWFonly; // true for Pure methods: we only want to enforce well-foundedness on them
      DontReportError = dontReport;     
      StateStack = new System.Collections.Stack();
      ResetCurrentState();
      this.VisitExpression(exp);
    }

    public override Expression VisitMemberBinding(MemberBinding binding) {
      if (binding == null) return null;

      bool isIndexerAccess = IndexerAccess; // saving flag for this visit
      IndexerAccess = false;

      if (st.OuterMostBinding == null) st.OuterMostBinding = binding;

      Method method = binding.BoundMember as Method;
      Field field = binding.BoundMember as Field;
      if (method == null && field == null) return binding; // got e.g. a type when visiting a type-cast 
      String accessedMemberName = field != null ? field.Name.ToString() : method.Name.ToString();
     
      //In a satisifies clause of a modelfield f defined in class C, a field f' is only allowed if it is visible in any subclass that can define an overriding modelfield.
      //When f is sealed (i.e., f or C has sealed keyword), no subclass can define an overriding modelfield.  
      if (this.DeclaringMfC != null && !this.DeclaringMfC.IsSealed && field != null) {  //f is not sealed        
        if (field.IsPrivate  ||  !field.IsVisibleOutsideAssembly && this.DeclaringMfC.Modelfield.IsVisibleOutsideAssembly)
          this.HandleError(binding, Error.InternalFieldInOverridableModelfield, field.Name.Name, this.DeclaringMember.Name.Name, this.DeclaringMfC.DeclaringType.Name.Name); //f' is not visible outside C's assembly, but f can be overridden in an external subclass                  
          //Field f' occurs in a satisfies of modelfield f, but f can be overridden in a subclass to which f' is not visible.
          //Possible solutions: make f' more visible, make C less visible, or make either f or C sealed.        
          //Todo: handle more cases in this check
      }  
      
      Expression targetObject = binding.TargetObject;
      // we do not allow arbitrary expressions as target, but do allow type and non-null casts (HACK)
      if ((targetObject is UnaryExpression || targetObject is BinaryExpression) &&
          !(targetObject.Type is Interface || targetObject.Type is Class ||
            (targetObject.Type is OptionalModifier && ((OptionalModifier)targetObject.Type).Modifier == SystemTypes.NonNullType)
            ))
        this.HandleError(st.OuterMostBinding, Error.NotAdmissibleTargetNotAdmissibile, accessedMemberName);

      // BASE CALL: so that we walk through access chain in order as appear in code
      base.VisitMemberBinding(binding);

      // if got an [Element] or [ElementCollection] method then setting element-level for this visit (rep:2, peer:1, none:0) 
      byte elementLevel = 0;
      bool previousWasElementCollection = st.PreviousWasElementCollection;
      st.PreviousWasElementCollection = false;
      if (MethodIsElementOrElementCollection(method))
        elementLevel = st.ElementLevel;
      // if got an [ElementRep] or [ElementPeer] field then saving element-level for next visit (rep:2, peer:1, none:0) 
      st.ElementLevel = 0;
      if (Util.FieldIsElementsRepOrPeer(field))
        st.ElementLevel = (byte)(Util.FieldIsElementsRep(field) ? 2 : 1);
      // if got [ElementCollection] method then keeping element-level where it was before for next visit
      else if (method != null && Util.MethodIsElementCollection(method)) {
        st.ElementLevel = elementLevel;
        st.PreviousWasElementCollection = true;
      }

      // access on 'result' in a method spec
      if (DeclaringMethod != null && targetObject is ReturnValue) {
        st.FirstBoundMember = false;
        st.LastAccessedName = "result";
        if (!Util.MethodIsRep(DeclaringMethod)) { // 'result' is non-rep
          st.OutsideOwnedCone = true;
          st.FirstNonRep = true;
          if (QuantifierBinding || isIndexerAccess)
            this.HandleError(st.OuterMostBinding, Error.NotAdmissibleBoundCollArrayNotRep);
        }
        else st.ContextDepth = 1;
      }

      // access on bound variable: treated as rep 
      if (field != null && field.DeclaringType as BlockScope != null) {
        st.FirstBoundMember = false;
        st.ContextDepth = 1;
        // st.ReportOnlyWarning = true;
      }
      // first access in chain
      else if (st.FirstBoundMember) {
        st.FirstBoundMember = false;
        // initial target must be 'this', 'result' (if in method spec) or static (target null and bound member not constructor)
        if (!((targetObject == null && !(binding.BoundMember is InstanceInitializer)) ||
                (targetObject is This || targetObject is ImplicitThis ||
                 (DeclaringMethod != null && targetObject is ReturnValue)))) {
          // might be hidden by type-cast or non-null cast
          if ((targetObject is UnaryExpression || targetObject is BinaryExpression) &&
              (targetObject.Type is Interface || targetObject.Type is Class ||
               (targetObject.Type is OptionalModifier && ((OptionalModifier)targetObject.Type).Modifier == SystemTypes.NonNullType
              ))) {
            Expression wrappedTarget = targetObject;
            while (wrappedTarget is UnaryExpression || wrappedTarget is BinaryExpression) {
              if (wrappedTarget is UnaryExpression)
                wrappedTarget = ((UnaryExpression)wrappedTarget).Operand;
              else if (wrappedTarget is BinaryExpression)
                wrappedTarget = ((BinaryExpression)wrappedTarget).Operand1;
            }
            if (!(wrappedTarget is This))
              this.HandleError(st.OuterMostBinding, Error.NotAdmissibleTargetNotThis);
          }
          else
            this.HandleError(st.OuterMostBinding, Error.NotAdmissibleTargetNotThis);
        }
        // if access is in StateIndependent method then has to be readonly or a non-modelfield of a struct or an immutable type
        if (DeclaringMethod != null && DeclaringMethod.IsStateIndependent &&
               (field != null && (field.IsModelfield || !(field is ParameterField) && !(field.IsInitOnly || field.DeclaringType.IsValueType || field.DeclaringType.GetAttribute(SystemTypes.ImmutableAttribute) != null))))
          this.HandleError(st.OuterMostBinding, Error.StateIndependentSpecNotAdmissible);
        
        // if first access not on locally declared field then must be from supertype and
        //either field is Additive, or field is a modelfield and we are checking a overriding modelfield contract for field.
        if (DeclaringMember != null && field != null && field.DeclaringType != DeclaringMember.DeclaringType)
          if (DeclaringMember.DeclaringType.IsAssignableTo(field.DeclaringType) && !Util.FieldIsAdditive(field) && (!field.IsModelfield || DeclaringMfC == null || DeclaringMfC.Modelfield != field))
            this.HandleError(st.OuterMostBinding, Error.NotAdmissibleFirstAccessOnNonAdditiveField, accessedMemberName);
          else if (field.DeclaringType.IsAssignableTo(DeclaringMember.DeclaringType))
            this.HandleError(st.OuterMostBinding, Error.NotAdmissibleFirstAccessOnFieldDeclaredInSubtype, accessedMemberName);        

        // non-rep: we go out of owned cone
        if ((field != null && !(field.IsRep || (field.Type != null && field.Type.IsValueType))) || // note: handling value types (e.g. structs) as owned
            (method != null && !Util.MethodIsRep(method))) {
          st.OutsideOwnedCone = true;
          st.FirstNonRep = true;
          // binding: must be rep (no visibility-based) 
          // array access: must be rep (cannot be visibility-based as not expressible on indexed element)
          if (QuantifierBinding || isIndexerAccess)
            this.HandleError(st.OuterMostBinding, Error.NotAdmissibleBoundCollArrayNotRep);        
        }
        else { // rep or struct
          if (!(field != null && (field.Type != null && field.Type.IsValueType))) // field of value-type (e.g. structs) does not lead into the owned cone
            st.ContextDepth = 1;
        }
      }
      // if access is in StateIndependent method then has to be readonly or field of a struct or immutable type
      else if (DeclaringMethod != null && DeclaringMethod.IsStateIndependent &&
          (field != null && !(field.IsInitOnly || field.DeclaringType.IsValueType || field.DeclaringType.GetAttribute(SystemTypes.ImmutableAttribute) != null)))
        this.HandleError(st.OuterMostBinding, Error.StateIndependentSpecNotAdmissible);
      // chain should have ended already
      else if (st.OutsideOwnedCone) {
        // access may be any of: visible field, readonly field, field of an immutable type, state-independent method
        // modelfields are currently not allowed: the implementation of the visibility-based technique can't deal with them (yet).
        if (!(DeclaringMethod == null && FieldIsVisible(field)) &&   // vis-based access not allowed in Confined or StateIndep. method specs
            !(field != null && !field.IsModelfield && (field.IsInitOnly || field.DeclaringType.GetAttribute(SystemTypes.ImmutableAttribute) != null)) &&
            !(method != null && (method.IsStateIndependent || (method.IsConfined && TypeNode.IsImmutable(method.DeclaringType))))) {
          //Only invariants can be visibility-based (not yet implemented for modelfields)
          String str = (DeclaringMethod == null && DeclaringMfC == null) ? " it is not visibility-based, and" : "";
          if (st.FirstNonRep)
            this.HandleError(st.OuterMostBinding, Error.NotAdmissibleFirstNonRepChainNotEnded, st.LastAccessedName, str);
          else
            this.HandleError(st.OuterMostBinding, Error.NotAdmissibleNonRepOrPeerChainNotEnded, st.LastAccessedName, str);
        }
      }
      // access is rep or struct
      else if ((field != null && (field.IsRep || (field.Type != null && field.Type is Struct))) || Util.MethodIsRep(method)) {
        if (field != null && field.Type != null && !(field.Type is Struct)) // if struct then do not go down (structs cannot be owned)
          st.ContextDepth++;
      }
      // array access is non-rep and non-peer 
      // handled separately as no Element or ElementCollection notation is possible on array elements (considered always Element)
      else if (isIndexerAccess && !(field != null && field.IsPeer)) {
        st.OutsideOwnedCone = true;
      }
      // access on an Element/ElementCollection of an ElementPeer collection or array (which means -1 level access relative to coll./array)
      else if (elementLevel == 1 &&
               !previousWasElementCollection) { // unless previous was ElementCollection then do not step up one level as we stay at same context
        st.ContextDepth--;
        if (st.ContextDepth < 1) {
          st.OutsideOwnedCone = true;
          // binding: must be owned (no visibility-based)
          if (QuantifierBinding)
            this.HandleError(st.OuterMostBinding, Error.NotAdmissibleBoundCollArrayNotOwned);
        }
      }
      // NOTE: elementLevel==2 need not be handled as we stay at the same context-depth
      // access is not owned: we go outside of owned cone
      else if (elementLevel != 2 &&
           ((field != null && !field.IsPeer) || (method != null && !Util.MethodIsOwned(method)))) {
        st.OutsideOwnedCone = true;
        // binding or array access: must be owned (no visibility-based)
        if (isIndexerAccess || QuantifierBinding)
          this.HandleError(st.OuterMostBinding, Error.NotAdmissibleBoundCollArrayNotOwned);
      }

      // leaving subexpression -> last access
      if (st.OuterMostBinding == binding) {
        // binding: last access must be (1) owned and (2) ElementsRep/Peer and deep enough (no visibility-based)
        if (QuantifierBinding) {
          if (st.OutsideOwnedCone)
            this.HandleError(st.OuterMostBinding, Error.NotAdmissibleBoundCollArrayNotOwned);

          if (!((field != null && Util.FieldIsElementsRep(field)) ||
                 (st.ContextDepth > 1 && Util.FieldIsElementsPeer(field)) ||
                 (st.ContextDepth > 0 && elementLevel > 0) ))
            this.HandleError(st.OuterMostBinding, Error.NotAdmissibleBoundCollArrayNotOwned, accessedMemberName);
        }
        // array access: last access must be owned (rep or peer)
        if (isIndexerAccess && field != null && !field.IsOwned)
          this.HandleError(st.OuterMostBinding, Error.NotAdmissibleBoundCollArrayNotOwned, accessedMemberName);
        TargetOutsideOwnedCone = st.OutsideOwnedCone;
        ResetCurrentState();
      }
      else
        st.LastAccessedName = accessedMemberName;
      return binding;
    }

    public override Expression VisitIndexer(Indexer indexer) {
      if (indexer == null) return null;
      // saving outermost binding sub-expression (in order to reset state when leaving it)
      if (st.OuterMostBinding == null) st.OuterMostBinding = indexer;
      IndexerAccess = true;
      // BASE CALL on array-object
      this.VisitExpression(indexer.Object);

      byte elementLevel = st.ElementLevel;
      if (st.OutsideOwnedCone)
        this.HandleError(st.OuterMostBinding, Error.NotAdmissibleBoundCollArrayNotOwned, st.LastAccessedName);
      else if (elementLevel == 0 || (elementLevel == 1 && st.ContextDepth < 2))
        st.OutsideOwnedCone = true;
      else if (elementLevel == 1)
        st.ContextDepth--;
      // NOTE: with elementsLevel==2 we just stay at same level

      st.LastAccessedName = "on element of " + st.LastAccessedName;

      // visiting index-expressions
      PushState();
      foreach (Expression param in indexer.Operands) {
        ResetCurrentState();
        this.VisitExpression(param);
      }
      PopState();

      if (st.OuterMostBinding == indexer) ResetCurrentState();
      return indexer;
    }

    public override Expression VisitQuantifier(Quantifier quantifier) {
      if (quantifier == null) return null;
      if (quantifier.Comprehension == null) return quantifier;
      ExpressionList bindfilter = quantifier.Comprehension.BindingsAndFilters;
      for (int i = 0; i < bindfilter.Count; i++) {
        ComprehensionBinding binding = bindfilter[i] as ComprehensionBinding;
        if (binding == null)   // it's a filter
          this.VisitExpression(bindfilter[i]);
        else {   // it's a binding
          Expression sourceEnum = binding.SourceEnumerable;
          if (sourceEnum == null) continue;
          if (sourceEnum.Type == SystemTypes.Range)
            this.VisitExpression(sourceEnum);
          else {
            QuantifierBinding = true;
            this.VisitExpression(sourceEnum);
            QuantifierBinding = false;
          }
        }
      }
      ExpressionList elems = quantifier.Comprehension.Elements;
      for (int i = 0; i < elems.Count; i++)
        this.VisitExpression(elems[i]);
      return quantifier;
    }

    public override Expression VisitMethodCall(MethodCall call) {
      if (call == null) return null;
      MemberBinding binding = call.Callee as MemberBinding;
      if (binding == null) return call;
      Method method = binding.BoundMember as Method;
      if (method == null) return call;

      Node errorNode = (st.OuterMostBinding == null ? binding : st.OuterMostBinding);

      // certain methods may not occur in satisfies clauses and specs of Confined and StateIndependent methods (occurence of these are checked elsewhere in invariants)
      if ((DeclaringMethod != null || DeclaringMfC != null) && Util.IsMethodologyMethod(method))
        this.HandleError(errorNode, Error.DisallowedInConfinedOrStIndepSpec, method.Name.ToString());

      // admissibility of method-calls in method specs
      if (DeclaringMethod != null) { // we are in method spec
        // forbiding "less" pure method calls than the one being specified
        if (DeclaringMethod.IsConfined && method.IsPure)
          this.HandleError(errorNode, Error.ConfinedSpecContainsPureCall);
        else if (DeclaringMethod.IsStateIndependent && !method.IsStateIndependent)
          this.HandleError(errorNode, Error.StateIndependentSpecContainsPureOrConfinedCall);

        this.ReportWFErrorOnly = false; // we want error msg even if method being specified is Pure
        // purity level is the same -> recursion value must decrease or receiver must be rep
        if ((DeclaringMethod.IsPure && method.IsPure) ||
             (DeclaringMethod.IsConfined && !method.IsStateIndependent) ||   // HACK: need to use !IsStateIndependent as getters are Confined by default thus IsConfined is true even if it's annotated with IsStateIndependent
             (DeclaringMethod.IsStateIndependent && method.IsStateIndependent)) {
          if (RecursionTerminationValue(DeclaringMethod, false) <= RecursionTerminationValue(method, true)) {
              if ( (binding.TargetObject is This) || (binding.TargetObject is ImplicitThis))
              this.HandleWarning(errorNode, "Method call '" + method.Name.ToString() +  "' not admissible: could not find decreasing measure based on purity-level, receiver, and recursion termination value.");
            else {
              this.ReportWFErrorOnly = true; // switch off reporting while deducing repness of reciever
              this.VisitExpression(binding.TargetObject);
              this.ReportWFErrorOnly = false;
              if (TargetOutsideOwnedCone)
                this.HandleWarning(errorNode, "Method call '" + method.Name.ToString() + "' not admissible: could not find decreasing measure based on purity-level, receiver, and recursion termination value.");                             
            }
          }
        }
        this.ReportWFErrorOnly = DeclaringMethod.IsPure; // switch off reporting if Pure as this was the only check we wanted on it
      }
      // builtIn: static(?) method generated by compiler for special cases (e.g. non-null cast)
      //          we want to skip visiting the callee and preserve AdmissibilityState as was before
      //          interesting part should be in the parameters of method
      bool builtIn = false; 
      if (Util.IsBuiltInMethodCall(method)) builtIn = true;
      if (DeclaringMember!= null && (method.IsStatic && !builtIn) && !method.IsStateIndependent) // static Confined method call is not allowed in invariants 
        this.HandleError(errorNode, Error.NotAdmissibleStaticNonStateIndependent);
      // visiting callee if needed (no restriction on static and StateIndepedent methods)
      if (!(method.IsStatic || builtIn || method.IsStateIndependent))
        this.VisitExpression(binding);
      else {  // we still need to visit the expression to have a correct state for further checks
        ReportWFErrorOnly = true;
        this.VisitExpression(binding);
        if (!(DeclaringMethod == null || !DeclaringMethod.IsPure))
          ReportWFErrorOnly = false;
      }
      ExpressionList parameters = call.Operands;
      // visiting parameters if needed (no restrictions on static and StateIndependent methods)
      if (!(method.IsStatic || method.IsStateIndependent) ||
          method.FullName.Equals("System.String.Equals(System.String,System.String)")) {
        if (!builtIn) PushState();
        foreach (Expression param in parameters) {
          if (!builtIn) ResetCurrentState();
          this.VisitExpression(param);
        }
        if (!builtIn) PopState();
      }
      return call;
    }

    #region Helpers
    private void ResetCurrentState() {
      st = new AdmissibilityState();
    }
    private void PushState() {
      StateStack.Push(st);
    }
    private void PopState() {
      st = (AdmissibilityState) StateStack.Pop();
    }

    /// <summary>
    /// Checks if field access is visibility-admissible. 
    /// </summary>
    /// <returns></returns>
    private bool FieldIsVisible(Field f) {
      if (f == null) return false;
      TypeNode type = DeclaringMember.DeclaringType;

      for (int a = 0; a < f.Attributes.Count; a++) {
        AttributeNode attr = f.Attributes[a];
        if (attr == null || attr.Type != SystemTypes.DependentAttribute) continue;
        ExpressionList exprs = attr.Expressions;
        if (exprs == null || exprs.Count == 0) continue;
        for (int i = 0; i < exprs.Count; i++) {
          UnaryExpression uexp = exprs[i] as UnaryExpression;
          if (uexp == null) continue;
          if (uexp.Operand.ToString().Equals(type.FullName))
            return true;
        }
      }
      return false;
    }

    // Note: second parameter is just optimiziation for the case when we do know that m's spec contains method call(s)
    private int RecursionTerminationValue(Method m, bool doMethodCallCheck) {
      if (m == null) return System.Int32.MaxValue;
      AttributeNode attr = m.GetAttribute(SystemTypes.RecursionTerminationAttribute);
      if (attr == null) { // recursion termination not specified
        if (!doMethodCallCheck)
          return System.Int32.MaxValue;
        else {
          CallAndResultVisitor visitor = new CallAndResultVisitor(this.checker);
          if (m.Contract != null) {
            foreach (Ensures ens in m.Contract.Ensures) {
              visitor.HasCall = false;
              visitor.VisitExpression(ens.PostCondition);
              if (visitor.HasCall)
                return System.Int32.MaxValue; // method spec contains call
            }
            foreach (Requires req in m.Contract.Requires) {
              visitor.HasCall = false;
              visitor.VisitExpression(req.Condition);
              if (visitor.HasCall)
                return System.Int32.MaxValue; // method spec contains call
            }
          }
          return 0; // did not find call in method spec
        }
      }
      ExpressionList exprs = attr.Expressions;
      if (exprs == null || exprs.Count == 0) return 0; // default value for attribute w/o param
      Expression arg = exprs[0];
      Literal lit = arg as Literal;
      if (lit != null && lit.Value is int)
        return (int)lit.Value;
      return System.Int32.MaxValue;
    }

    /// <summary>
    /// Returns true iff method is marked [Element] or is the [] getter or method is marked ElementCollection.
    /// </summary>
    private bool MethodIsElementOrElementCollection(Method method) {
      if (method == null) return false;
      if (method.GetAttribute(SystemTypes.ElementAttribute) != null)
        return true;
      if ((method.DeclaringType != null && (method.DeclaringType.IsAssignableTo(SystemTypes.IEnumerable) || checker.HasEnumerablePattern(method.DeclaringType))) &&
           method.IsPropertyGetter && method.Name.ToString().Equals(StandardIds.getItem.ToString()))
        return true;
      if (method.GetAttribute(SystemTypes.ElementCollectionAttribute) != null)
        return true;
      return false;
    }
    #endregion

    internal void HandleError(Node offendingNode, Error error, params string[] messageParameters) {
      hasError = true;
      if (st.ReportedError || ReportWFErrorOnly || DontReportError) return;
      ((ErrorHandler)this.ErrorHandler).HandleError(offendingNode, error, messageParameters);
      st.ReportedError = true;
    }
    internal void HandleWarning(Node offendingNode, string str) {
      hasError = true;
      if (st.ReportedError || ReportWFErrorOnly || DontReportError) return;
      ((ErrorHandler)this.ErrorHandler).HandleError(offendingNode, Error.GenericWarning, str);
      // st.ReportedError = true;
    }

    public bool HasError() { 
      return hasError; 
    }
  }

  /// <summary>
  /// State we need to keep track of to check the admissibility of a sub-expression
  /// </summary>
  class AdmissibilityState {
    public bool OutsideOwnedCone;
    public bool FirstNonRep;
    public bool FirstBoundMember = true;
    public bool ReportedError;
    public bool PreviousWasElementCollection;
    public int ContextDepth;
    public byte ElementLevel;
    public Node OuterMostBinding;
    public String LastAccessedName;
  }
  #endregion

  #region ReadEffectAdmissibilityChecker
  /// <summary>
  /// This class checks whether the read-effects of a method comply with the Confined or StateIndependent annotation.
  /// </summary>
  class ReadEffectAdmissibilityChecker {
    Method Method;
    System.Compiler.ErrorHandler ErrorHandler;
    VariableEffect currentEffect;

    int previousElementLevel; // stores whether "previous" array access was ElementsRep or ElementsPeer
    int currentElementLevel;
    int contextDepth;
    bool firstBoundMember;
    bool outsideOwnedCone;
    bool ReportedError;

    public ReadEffectAdmissibilityChecker(Method method, System.Compiler.ErrorHandler handler) {
      this.Method = method;
      this.ErrorHandler = handler;
    }

    void ResetState() {
      previousElementLevel = 0;
      currentElementLevel = 0;
      contextDepth = 0;
      firstBoundMember = true;
      outsideOwnedCone = false;
      ReportedError = false;
    }

    public void CheckReadEffectAdmissibility(Set<VariableEffect> readEffects) {
      foreach (VariableEffect readEffect in readEffects) {
        currentEffect = readEffect;
        List<Field> chain = readEffect.FieldChain;
        Variable v = readEffect.Variable;

        if (chain == null) return;
        if (v.Name.ToString().Equals("Global")) {  // Global read is not admitted
          if (chain.Count != 1 || !GlobalFieldIsPrivateOrImmutable(chain[0]))
            this.IssueWarning();
        }
        ResetState();
        if (v is Parameter && !(v is This)) {  // chain accesses parameter
          firstBoundMember = false;
          outsideOwnedCone = true; // since we do not allow rep annotation on parameters
        }
        for (int i = 0; i < chain.Count; i++)
          CheckField(chain[i]);
      }
    }

    private static bool GlobalFieldIsPrivateOrImmutable(Field field) {
      if (field == null) return false;
      if (field.IsPrivate) return true;
      if (field.IsInitOnly) return TypeNode.IsImmutable(field.Type);
      return false;
    }

    private void CheckField(Field field) {
      if (field == null) return;

      // if method is StateIndependent then has to be readonly or field of an immutable type
      if (Method.IsStateIndependent &&
          !(field.IsInitOnly || (field.DeclaringType != null && field.DeclaringType.GetAttribute(SystemTypes.ImmutableAttribute) != null)))
        this.IssueWarning();

      if (field.Equals(PTGraph.allFields) || field.Equals(PTGraph.allFieldsNotOwned)) // reading all reachable fields not admissible
        this.IssueWarning(); // NOTE: this could be refined by analysing whether only rep fields are reachable

      // if got an [ElementRep] or [ElementPeer] field then saving element-level for next visit (rep:2, peer:1, none:0) 
      currentElementLevel = previousElementLevel;
      if (Util.FieldIsElementsRepOrPeer(field))
        previousElementLevel = (byte)(Util.FieldIsElementsRep(field) ? 2 : 1);

      // first access in chain
      if (firstBoundMember) {
        firstBoundMember = false;
        // non-rep: we go out of owned cone
        if (!(field.IsRep || (field.Type != null && field.Type.IsValueType))) { // note: handling value types (e.g. structs) as owned             
          outsideOwnedCone = true;
        }
        else { // rep or struct
          if (!(field.Type != null && field.Type.IsValueType)) // field of value-type (e.g. structs) does not lead into the owned cone
            contextDepth = 1;
        }
      }
      // chain should have ended already
      else if (outsideOwnedCone) {
        // access may be any of: visible field, readonly field, field of an immutable type, state-independent method
        if (!(field.IsInitOnly || (field.DeclaringType != null && field.DeclaringType.GetAttribute(SystemTypes.ImmutableAttribute) != null)))
          this.IssueWarning();
      }
      // access is rep or struct
      else if (field.IsRep || (field.Type != null && field.Type is Struct)) {
        if (field.Type != null && !(field.Type is Struct)) // if struct then do not go down (structs cannot be owned)
          contextDepth++;
      }
      // access on an element of an ElementsPeer array (which means -1 level access relative to coll./array)
      else if (currentElementLevel == 1) { 
        contextDepth--;
        if (contextDepth < 1)
          outsideOwnedCone = true;
      }
      // NOTE: elementLevel==2 need not be handled as we stay at the same context-depth
      // access is not owned: we go outside of owned cone
      else if (currentElementLevel != 2 && !field.IsPeer) {
        outsideOwnedCone = true;
      }
    }

    internal void IssueWarning() {
      if (ReportedError) return;
      string s = Method.IsConfined ? "Confined" : "StateIndependent";
      string msg = string.Format("Read-effect {0} is not admitted for {1} method {2}.", currentEffect.ToString(), s, Method.Name.ToString());
      ((ErrorHandler)this.ErrorHandler).HandleError(currentEffect.Label.Statement, Error.GenericWarning, msg);
      ReportedError = true;
    }
  }
  #endregion

  #region Utility class
  /// <summary>
  /// Utility class for simple queries mainly about certain attributes of fields and methods.
  /// </summary>
  public class Util {

    public static bool MethodIsConfinedOrStateIndependent(Method m) {
      if (m == null) return false;
      return m.IsConfined || m.IsStateIndependent;
    }

    public static bool FieldIsElementsRepOrPeer(Field f) {
      if (f == null) return false;
      return (f.GetAttribute(SystemTypes.ElementsRepAttribute) != null ||
              f.GetAttribute(SystemTypes.ElementsPeerAttribute) != null);
    }

    public static bool FieldIsElementsRep(Field f) {
      if (f == null) return false;
      return f.GetAttribute(SystemTypes.ElementsRepAttribute) != null;
    }

    public static bool FieldIsElementsPeer(Field f)
    {
        if (f == null) return false;
        return f.GetAttribute(SystemTypes.ElementsPeerAttribute) != null;
    }
    
      /* This method finds all positions of a type that can have an ElementsRep or ElementsPeer attribute.
     * These positions are: 
     *   -1 if the type is an array
     * or
     *   i if the type is generic and the i-th type argument is a reference type
     */
      public static List<int> ElementsPositions(TypeNode t)
      {
          List<int> res = new List<int>();
          t = TypeNode.StripModifiers(t);
          if (t is ArrayType)
          {
              res.Add(-1);
          }
          else if (t.IsGeneric || t.Template != null)
          {
              TypeNodeList tnl = t.TemplateArguments;
              for (int i = 0; i < tnl.Count; i++)
              {
                  if (tnl[i].IsReferenceType)
                  {
                      res.Add(i);
                  }
              }
          }
          return res;
      }

    public static List<int> FieldElementsRepPositions(Field f)
    {
      List<int> res = new List<int>();
      if (f == null) return res;
      AttributeList al = f.GetAllAttributes(SystemTypes.ElementsRepAttribute);
      if (al == null) return res;
      foreach (AttributeNode attr in al)
      {
          ExpressionList exprs = attr.Expressions;
          int value = -10;
          if (exprs == null || exprs.Count == 0) 
              value = -1; // default value for attribute w/o param, in particular, for arrays
          else
          {
              Expression arg = exprs[0];
              Literal lit = arg as Literal;
              if (lit != null && lit.Value is int)
                  value = (int)lit.Value;
          }
          if (value == -1)  // all positions are ElementsRep
          {
              return ElementsPositions(f.Type);
          }
          else // a specific type argument is ElementsRep
          {
              res.Add(value);
          }
      }
      return res;
    }
      public static List<int> FieldElementsPeerPositions(Field f)
      {
          List<int> res = new List<int>();
          if (f == null) return res;
          AttributeList al = f.GetAllAttributes(SystemTypes.ElementsPeerAttribute);
          if (al == null) return res;
          foreach (AttributeNode attr in al)
          {
              ExpressionList exprs = attr.Expressions;
              int value = -10;
              if (exprs == null || exprs.Count == 0) 
                  value = -1; // default value for attribute w/o param
              else
              {
                  Expression arg = exprs[0];
                  Literal lit = arg as Literal;
                  if (lit != null && lit.Value is int)
                      value = (int)lit.Value;
              }
              if (value == -1)  // all positions are ElementsPeer
              {
                  return ElementsPositions(f.Type);
              }
              else // a specific type argument is ElementsPeer
              {
                  res.Add(value);
              }
          }
          return res;
      }


    public static bool MethodIsElementCollection(Method m) {
      if (m == null) return false;
      return m.GetAttribute(SystemTypes.ElementCollectionAttribute) != null;
    }

    public static bool MethodIsRep(Method m) {
      if (m == null) return false;
      AttributeNode attr = m.GetAttribute(SystemTypes.RepAttribute);
      return (attr != null);
    }

    public static bool MethodIsOwned(Method m) {
      if (m == null) return false;
      return m.GetAttribute(SystemTypes.RepAttribute) != null || m.GetAttribute(SystemTypes.PeerAttribute) != null;
    }

    public static bool FieldIsAdditive(Field field) {
      if (field == null) return false;
      AttributeNode attr = field.GetAttribute(SystemTypes.AdditiveAttribute);
      if (attr == null) return false;
      ExpressionList exprs = attr.Expressions;
      if (exprs == null || exprs.Count != 1) return true;
      Literal lit = exprs[0] as Literal;
      if (lit != null && (lit.Value is bool)) return (bool)lit.Value;
      else return false;
    }
    
    public static bool IsBuiltInMethodCall(Method m) {
      if (m == null) return false;
      return (m.FullName.LastIndexOf("IsNonNullGeneric") >= 0);
    }

    // returns true if method is a special one used for the methodology
    public static bool IsMethodologyMethod(Method m) {
      switch (m.FullName) {
        case "Microsoft.Contracts.Guard.IsConsistent(optional(Microsoft.Contracts.NonNullType) System.Object)":
        case "Microsoft.Contracts.Guard.IsPeerConsistent(optional(Microsoft.Contracts.NonNullType) System.Object)":
        case "Microsoft.Contracts.Guard.get_IsExposable()":
        case "Microsoft.Contracts.Guard.FrameIsExposable(optional(Microsoft.Contracts.NonNullType) System.Object,optional(Microsoft.Contracts.NonNullType) System.Type)":
        case "Microsoft.Contracts.Guard.get_IsExposed()":
        case "Microsoft.Contracts.Guard.FrameIsExposed(optional(Microsoft.Contracts.NonNullType) System.Object,optional(Microsoft.Contracts.NonNullType) System.Type)":
        case "Microsoft.Contracts.Guard.get_IsValid()":
        case "Microsoft.Contracts.Guard.FrameIsValid(optional(Microsoft.Contracts.NonNullType) System.Object,optional(Microsoft.Contracts.NonNullType) System.Type)":
        case "Microsoft.Contracts.Guard.get_IsPrevalid()":
        case "Microsoft.Contracts.Guard.FrameIsPrevalid(optional(Microsoft.Contracts.NonNullType) System.Object,optional(Microsoft.Contracts.NonNullType) System.Type)":
          return true;         
      }
      return false;
    }

  }
  #endregion

  #region Simple specialized visitors: CallAndResultVisitor, NoReferenceComparisonVisitor and MightReturnNewlyAllocatedObjectVisitor
  /// <summary>
  /// Visits an expression to check if expression contains (1) method or constructor call 
  /// or (2) 'result' - queried by (1) HasCall and (2) HasResult.
  /// Used for determining (1) whether an expression in a spec can be used 
  /// for axiom generation in a sound way; (2) the RecursionTermination value of a method.
  /// </summary>
  public class CallAndResultVisitor : StandardVisitor {
    protected bool hasCall = false;
    protected bool hasResult = false;

    public CallAndResultVisitor(System.Compiler.Checker callingVisitor)
      : base(callingVisitor) { }
    public bool HasResult {
      get { return hasResult; }
      set { hasResult = value; }
    }
    public bool HasCall {
      get { return hasCall; }
      set { hasCall = value; }
    }
    public override Expression VisitConstruct(Construct cons) {
      hasCall = true;
      return cons;
    }
    public override InstanceInitializer VisitInstanceInitializer(InstanceInitializer cons) {
      hasCall = true;
      return cons;
    }
    public override Expression VisitMethodCall(MethodCall call) {
      hasCall = true;
      return call;
    }
    public override Expression VisitReturnValue (ReturnValue returnValue) {
      hasResult = true;
      return base.VisitReturnValue(returnValue);
    }
  }
  

  /// <summary>
  /// Visits a method body to check if there is any "==" or "!=" operation on reference-type operands.
  /// Used when checking body of methods marked as NoReferenceComparison. 
  /// </summary>
  public class NoReferenceComparisonVisitor : StandardVisitor {
    bool noRefComparison = true;

    public NoReferenceComparisonVisitor(System.Compiler.Checker callingVisitor)
      : base(callingVisitor) { }

    public bool HasNoReferenceComparison {
      get { return noRefComparison; }
      set { noRefComparison = value; }
    }
    public override Expression VisitBinaryExpression(BinaryExpression binaryExpression) {
      if (binaryExpression == null) return null;
      // comparison of reference-types and none of the operands is "null"
      if ( (binaryExpression.NodeType == NodeType.Eq || binaryExpression.NodeType == NodeType.Ne) &&
            binaryExpression.Operand1 != null && binaryExpression.Operand1.Type != null &&
            !binaryExpression.Operand1.Type.IsValueType) {
        Literal lit1 = (binaryExpression.Operand1 as Literal);
        Literal lit2 = (binaryExpression.Operand2 as Literal);
        if (! ( (lit1 != null && lit1.ToString().Equals(Literal.Null.ToString())) ||
                (lit2 != null && lit2.ToString().Equals(Literal.Null.ToString())))) {
          noRefComparison = false;
          return binaryExpression;
        }
      }
      binaryExpression.Operand1 = this.VisitExpression(binaryExpression.Operand1);
      binaryExpression.Operand2 = this.VisitExpression(binaryExpression.Operand2);
      return binaryExpression;
    }
    public override Expression VisitMethodCall(MethodCall call){
      if (call == null) return null;
      MemberBinding binding = call.Callee as MemberBinding;
      if (binding == null) return call;
      Method method = binding.BoundMember as Method;
      if (method == null) return call;
      if (method.GetAttribute(SystemTypes.NoReferenceComparisonAttribute) == null) { // call to non-NRC
        noRefComparison = false;
        return call;
      }
      call.Callee = this.VisitExpression(call.Callee);
      call.Operands = this.VisitExpressionList(call.Operands);
      call.Constraint = this.VisitTypeReference(call.Constraint);
      return call;
    }
  }

  /// <summary>
  /// Visits an expression to check if it might return a newly allocated object.
  /// Used when analysing specification of pure methods to exclude situations when
  /// two references possibly refering to newly created objects are compared. 
  /// </summary>
  public class MightReturnNewlyAllocatedObjectVisitor : StandardVisitor {
    bool isMRNAO = false;
    Method currentMethod;

    public MightReturnNewlyAllocatedObjectVisitor(System.Compiler.Checker callingVisitor)
      : base(callingVisitor) { }
    public bool IsMRNAO {
      get { return isMRNAO; }
      set { isMRNAO = value; }
    }
    public Method CurrentMethod {
      set { currentMethod = value; }
    }
    public override Expression VisitMethodCall(MethodCall call) {
      if (call == null) return null;
      MemberBinding binding = call.Callee as MemberBinding;
      if (binding == null) return call;
      Method method = binding.BoundMember as Method;
      if (method == null) return call;
      if (!method.ReturnType.IsValueType && method.GetAttribute(SystemTypes.ResultNotNewlyAllocatedAttribute) == null) {
        isMRNAO = true;
        return call;
      }
      call.Callee = this.VisitExpression(call.Callee);
      call.Operands = this.VisitExpressionList(call.Operands);
      call.Constraint = this.VisitTypeReference(call.Constraint);
      return call;
    }
    public override Expression VisitConstruct(Construct cons) {
      isMRNAO = true;
      return cons;
    }
    public override InstanceInitializer VisitInstanceInitializer(InstanceInitializer cons) {
      isMRNAO = true;
      return cons;
    }
    public override Expression VisitReturnValue(ReturnValue value) {
      if (value == null) return null;
      if (currentMethod != null) // found 'result'
        isMRNAO = !currentMethod.ReturnType.IsValueType && currentMethod.GetAttribute(SystemTypes.ResultNotNewlyAllocatedAttribute) == null;
      return value;
    }
  }
  #endregion
}