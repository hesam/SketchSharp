//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler {
#endif
  /// <summary>
  /// Walk IR checking for semantic errors and repairing it so that subsequent walks need not do error checking
  /// </summary>
  public class Checker : StandardCheckingVisitor {
    public SwitchCaseList currentSwitchCases;
    public TypeNode currentSwitchGoverningType;
    public bool insideAssertOrAssume;
    public bool insideCatchClause;
    public bool insideFixed;
    public bool insideFixedDeclarator;
    public bool insideTryBlock;
    public bool insideMethodContract;
    public bool insideEnsures; // special rules apply for visibility checks in postconditions
    public bool insidePureContract;
    public bool insideInvariant;
    public bool insideModelfield;
    public bool insideQuantifier;
    public bool insideModifies;
    public bool isCompilingAContractAssembly;
    private bool requireInitializer;
    public BlockScope currentFinallyClause;
    public TrivialHashtable scopeFor;
    public bool MayNotReferenceThisFromFieldInitializer;
    public bool MayReferenceThisAndBase;
    public int loopCount;
    public int switchCaseCount;
    public ErrorNodeList Errors;
    public AssemblyNode currentAssembly;
    public Field currentField;
    public Method currentMethod;
    public Module currentModule;
    public TypeNode currentType;
    public Return returnNode;
    public Yield yieldNode;
    public TrivialHashtable referencedLabels;
    public TrivialHashtable ambiguousTypes;
    public TrivialHashtable visitedCompleteTypes;
    public TrivialHashtable indexerNames;
    public TypeSystem typeSystem;
    public QueryQuantifiedExpression currentQuantifiedExpression;
    public QueryTransact currentTransaction;
    public CompilerOptions currentOptions;
    public Hashtable currentPreprocessorDefinedSymbols;
    public TypeNodeList allowedExceptions;
    public Catch currentCatchClause; // assigned when visiting a catch block in case "throw;" in encountered in the body
    private AssemblyNode shadowedAssembly;
    public bool useGenerics;
    public bool NonNullChecking;
    public bool AllowPropertiesIndexersAsRef;

    public Checker(ErrorHandler errorHandler, TypeSystem typeSystem, TrivialHashtable scopeFor, TrivialHashtable ambiguousTypes, TrivialHashtable referencedLabels)
      : base(errorHandler) {
      this.typeSystem = typeSystem;
      this.Errors = errorHandler == null ? null : errorHandler.Errors;
      this.scopeFor = scopeFor;
      this.ambiguousTypes = ambiguousTypes;
      this.referencedLabels = referencedLabels;
      this.MayNotReferenceThisFromFieldInitializer = true;
      this.allowedExceptions = new TypeNodeList();
      this.useGenerics = TargetPlatform.UseGenerics;
      this.AllowPropertiesIndexersAsRef = true;
    }
    public Checker(Visitor callingVisitor)
      : base(callingVisitor) {
    }
    public override void TransferStateTo(Visitor targetVisitor) {
      base.TransferStateTo(targetVisitor);
      Checker target = targetVisitor as Checker;
      if (target == null) return;
      target.currentSwitchCases = this.currentSwitchCases;
      target.currentSwitchGoverningType = this.currentSwitchGoverningType;
      target.insideAssertOrAssume = this.insideAssertOrAssume;
      target.insideCatchClause = this.insideCatchClause;
      target.insideFixed = this.insideFixed;
      target.insideFixedDeclarator = this.insideFixedDeclarator;
      target.insideInvariant = this.insideInvariant;
      target.insideMethodContract = this.insideMethodContract;
      target.insideEnsures = this.insideEnsures;
      target.insideModifies = this.insideModifies;
      target.insideTryBlock = this.insideTryBlock;
      target.insideQuantifier = this.insideQuantifier;
      target.isCompilingAContractAssembly = this.isCompilingAContractAssembly;
      target.requireInitializer = this.requireInitializer;
      target.currentFinallyClause = this.currentFinallyClause;
      target.scopeFor = this.scopeFor;
      target.MayNotReferenceThisFromFieldInitializer = this.MayNotReferenceThisFromFieldInitializer;
      target.MayReferenceThisAndBase = this.MayReferenceThisAndBase;
      target.loopCount = this.loopCount;
      target.switchCaseCount = this.switchCaseCount;
      target.Errors = this.Errors;
      target.currentAssembly = this.currentAssembly;
      target.currentField = this.currentField;
      target.currentMethod = this.currentMethod;
      target.currentModule = this.currentModule;
      target.currentType = this.currentType;
      target.returnNode = this.returnNode;
      target.yieldNode = this.yieldNode;
      target.referencedLabels = this.referencedLabels;
      target.ambiguousTypes = this.ambiguousTypes;
      target.visitedCompleteTypes = this.visitedCompleteTypes;
      target.indexerNames = this.indexerNames;
      target.typeSystem = this.typeSystem;
      target.currentQuantifiedExpression = this.currentQuantifiedExpression;
      target.currentOptions = this.currentOptions;
      target.currentCatchClause = this.currentCatchClause;
    }

    public readonly static TrivialHashtable OperatorName;
    static Checker() {
      OperatorName = new TrivialHashtable();
      OperatorName[StandardIds.opAddition.UniqueIdKey] = "operator +";
      OperatorName[StandardIds.opBitwiseAnd.UniqueIdKey] = "operator &";
      OperatorName[StandardIds.opBitwiseOr.UniqueIdKey] = "operator |";
      OperatorName[StandardIds.opDecrement.UniqueIdKey] = "operator --";
      OperatorName[StandardIds.opDivision.UniqueIdKey] = "operator /";
      OperatorName[StandardIds.opEquality.UniqueIdKey] = "operator ==";
      OperatorName[StandardIds.opExclusiveOr.UniqueIdKey] = "operator ^";
      OperatorName[StandardIds.opExplicit.UniqueIdKey] = "explicit operator";
      OperatorName[StandardIds.opFalse.UniqueIdKey] = "operator false";
      OperatorName[StandardIds.opGreaterThan.UniqueIdKey] = "operator >";
      OperatorName[StandardIds.opGreaterThanOrEqual.UniqueIdKey] = "operator >=";
      OperatorName[StandardIds.opImplicit.UniqueIdKey] = "implicit operator";
      OperatorName[StandardIds.opIncrement.UniqueIdKey] = "operator ++";
      OperatorName[StandardIds.opInequality.UniqueIdKey] = "operator !=";
      OperatorName[StandardIds.opLeftShift.UniqueIdKey] = "operator <<";
      OperatorName[StandardIds.opLessThan.UniqueIdKey] = "operator <";
      OperatorName[StandardIds.opLessThanOrEqual.UniqueIdKey] = "operator <=";
      OperatorName[StandardIds.opLogicalNot.UniqueIdKey] = "operator !";
      OperatorName[StandardIds.opModulus.UniqueIdKey] = "operator %";
      OperatorName[StandardIds.opMultiply.UniqueIdKey] = "operator *";
      OperatorName[StandardIds.opOnesComplement.UniqueIdKey] = "operator ~";
      OperatorName[StandardIds.opRightShift.UniqueIdKey] = "operator >>";
      OperatorName[StandardIds.opSubtraction.UniqueIdKey] = "operator -";
      OperatorName[StandardIds.opTrue.UniqueIdKey] = "operator true";
      OperatorName[StandardIds.opUnaryNegation.UniqueIdKey] = "operator -";
      OperatorName[StandardIds.opUnaryPlus.UniqueIdKey] = "operator +";
    }

    public override Expression VisitAddressDereference(AddressDereference addr) {
      if (addr == null) return null;
      Expression expr = addr.Address = this.VisitExpression(addr.Address);
      if (expr == null) return null;
      TypeNode exprType = expr.Type;
      if (exprType == null) return null;
      exprType = TypeNode.StripModifiers(exprType);
      Reference refType = exprType as Reference;
      if (refType != null) {
        // Change to highlight an issue: elementType can be an optional modifier, 
        // in which case pointerType will be null. Is this the intention? 
        TypeNode elementType = refType.ElementType;//TypeNode.StripModifiers(refType.ElementType);
        Pointer pointerType = elementType as Pointer;
        if (pointerType != null) {
          exprType = elementType;
          expr = addr.Address = this.typeSystem.AutoDereferenceCoercion(expr);
        }
      }
      if (!(exprType is Pointer || exprType is Reference || exprType.Template == SystemTypes.GenericBoxed ||
        ((expr is Base) && exprType == SystemTypes.ValueType) ||
        ((expr is This || expr is ImplicitThis) && exprType.IsValueType))) {
        this.HandleError(expr, Error.CannotDeferenceNonPointerType, this.GetTypeName(exprType));
        return null;
      }
      if (exprType is Pointer && ((Pointer)exprType).ElementType == SystemTypes.Void) {
        this.HandleError(addr, Error.VoidError);
        return null;
      }
      int i = addr.Alignment;
      if (i > 0 && !(i == 1 || i == 2 || i == 4))
        addr.Alignment = -1; //It is up to the source language to complain.
      return addr;
    }
    public override AssemblyNode VisitAssembly(AssemblyNode assembly) {
      if (assembly == null) return null;
      this.currentModule = this.currentAssembly = assembly;
      assembly.Attributes = this.VisitAttributeList(assembly.Attributes, assembly);
      assembly.ModuleAttributes = this.VisitModuleAttributes(assembly.ModuleAttributes);
      assembly.Types = this.VisitTypeNodeList(assembly.Types);
      return assembly;
    }
    public virtual string GetStringParameter(ExpressionList args) {
      if (args == null || args.Count < 1) return null;
      Literal lit = args[0] as Literal;
      if (lit == null) return null;
      return lit.Value as string;
    }
    public virtual AttributeNode VisitAssemblyAttribute(AttributeNode attr, AssemblyNode target) {
      attr = this.VisitAttributeNode(attr);
      if (attr == null) return null;
      MemberBinding mb = attr.Constructor as MemberBinding;
      if (mb == null) return null;
      InstanceInitializer constr = mb.BoundMember as InstanceInitializer;
      if (constr == null) return null;
      TypeNode attrType = constr.DeclaringType;
      if (this.currentOptions != null) {
        if (attrType == SystemTypes.AssemblyCompanyAttribute)
          this.currentOptions.TargetInformation.Company = this.GetStringParameter(attr.Expressions);
        else if (attrType == SystemTypes.AssemblyConfigurationAttribute)
          this.currentOptions.TargetInformation.Configuration = this.GetStringParameter(attr.Expressions);
        else if (attrType == SystemTypes.AssemblyCopyrightAttribute)
          this.currentOptions.TargetInformation.Copyright = this.GetStringParameter(attr.Expressions);
        else if (attrType == SystemTypes.AssemblyCultureAttribute)
          this.currentOptions.TargetInformation.Culture = this.GetStringParameter(attr.Expressions);
        else if (attrType == SystemTypes.AssemblyDescriptionAttribute)
          this.currentOptions.TargetInformation.Description = this.GetStringParameter(attr.Expressions);
        else if (attrType == SystemTypes.AssemblyFileVersionAttribute)
          this.currentOptions.TargetInformation.Version = this.GetStringParameter(attr.Expressions);
        else if (attrType == SystemTypes.AssemblyInformationalVersionAttribute)
          this.currentOptions.TargetInformation.ProductVersion = this.GetStringParameter(attr.Expressions);
        else if (attrType == SystemTypes.AssemblyProductAttribute)
          this.currentOptions.TargetInformation.Product = this.GetStringParameter(attr.Expressions);
        else if (attrType == SystemTypes.AssemblyTitleAttribute)
          this.currentOptions.TargetInformation.Title = this.GetStringParameter(attr.Expressions);
        else if (attrType == SystemTypes.AssemblyTrademarkAttribute)
          this.currentOptions.TargetInformation.Trademark = this.GetStringParameter(attr.Expressions);
      }
      if (attrType == SystemTypes.AssemblyFlagsAttribute) {
        ExpressionList args = attr.Expressions;
        AssemblyFlags flags = AssemblyFlags.None;
        if (args != null && args.Count > 0) {
          Literal flagsLit = args[0] as Literal;
          if (flagsLit != null && this.currentAssembly != null) {
            if (flagsLit.Value is int)
              flags = (AssemblyFlags)(int)flagsLit.Value;
            else if (flagsLit.Value is uint)
              flags = (AssemblyFlags)(uint)flagsLit.Value;
          }
        }
        this.currentAssembly.Flags = flags;
        attr.IsPseudoAttribute = true;
        return attr;
      }
      if (attrType == SystemTypes.AssemblyKeyFileAttribute) {
        ExpressionList args = attr.Expressions;
        if (args != null && args.Count > 0) {
          Literal keyFilePathLit = args[0] as Literal;
          if (keyFilePathLit != null && this.currentAssembly != null) {
            string keyFilePath = keyFilePathLit.Value as string;
            Document doc = attr.SourceContext.Document;
            if (doc != null && doc.Name != null) {
              string dir = Path.GetDirectoryName(doc.Name);
              keyFilePath = PathWrapper.Combine(dir, keyFilePath);
            }
            if (keyFilePath != null && File.Exists(keyFilePath)) { //TODO: what about additional search paths
              if (this.currentOptions != null)
                this.currentOptions.AssemblyKeyFile = keyFilePath;
            } else {
              if (this.currentOptions != null)
                this.currentOptions.AssemblyKeyFile = keyFilePathLit.Value as string;
            }
            //this.HandleError(attr, Error.UseSwitchInsteadOfAttribute, "keyfile", "AssemblyKeyFileAttribute");
          }
        }
      }
      if (attrType == SystemTypes.AssemblyKeyNameAttribute) {
        ExpressionList args = attr.Expressions;
        if (args != null && args.Count > 0) {
          Literal keyNameLit = args[0] as Literal;
          if (keyNameLit != null && this.currentAssembly != null) {
            string keyName = keyNameLit.Value as string;
            if (keyName != null) {
              if (this.currentOptions != null)
                this.currentOptions.AssemblyKeyName = keyName;
              //this.HandleError(attr, Error.UseSwitchInsteadOfAttribute, "keyname", "AssemblyKeyNameAttribute");
            }
          }
        }
        return attr;
      }
      if (attrType == SystemTypes.AssemblyVersionAttribute || attrType == SystemTypes.SatelliteContractVersionAttribute) {
        ExpressionList args = attr.Expressions;
        string version = null;
        if (args != null && args.Count > 0) {
          Literal titleLit = args[0] as Literal;
          if (titleLit != null && this.currentAssembly != null) {
            version = titleLit.Value as String;
          }
        }
        Version v = null;
        if (version != null) v = this.ParseVersion(version, attrType != SystemTypes.SatelliteContractVersionAttribute);
        if (v == null)
          this.HandleError(mb, Error.CustomAttributeError, this.GetTypeName(attrType), version);
        else if (attrType == SystemTypes.AssemblyVersionAttribute) {
          target.Version = v;
          attr.IsPseudoAttribute = true;
          return attr;
        }
        return attr;
      }
      if (attrType == SystemTypes.AssemblyDelaySignAttribute) {
        ExpressionList args = attr.Expressions;
        if (args != null && args.Count > 0) {
          Literal valueLit = args[0] as Literal;
          if (valueLit != null && valueLit.Value is bool && this.currentAssembly != null && this.currentOptions != null && !this.currentOptions.DelaySign) {
            this.currentOptions.DelaySign = (bool)valueLit.Value;
          }
        }
        return attr;
      }
      if (attrType == SystemTypes.CLSCompliantAttribute) {
        //TODO: enable CLS checking
      }
      return this.VisitUnknownAssemblyAttribute(attrType, attr, target);
    }
    public virtual AttributeNode VisitUnknownAssemblyAttribute(TypeNode attrType, AttributeNode attr, AssemblyNode target) {
      return attr;
    }
    public virtual Version ParseVersion(string vString, bool allowWildcards) {
      ushort major = 1;
      ushort minor = 0;
      ushort build = 0;
      ushort revision = 0;
      try {
        int n = vString.Length;
        int i = vString.IndexOf('.', 0);
        if (i < 0) throw new FormatException();
        major = UInt16.Parse(vString.Substring(0, i), CultureInfo.InvariantCulture);
        int j = vString.IndexOf('.', i+1);
        if (j < i+1)
          minor = UInt16.Parse(vString.Substring(i+1, n-i-1), CultureInfo.InvariantCulture);
        else {
          minor = UInt16.Parse(vString.Substring(i+1, j-i-1), CultureInfo.InvariantCulture);
          if (vString[j+1] == '*' && allowWildcards) {
            if (j+1 < n-1) return null;
            build = Checker.DaysSince2000();
            revision = Checker.SecondsSinceMidnight();
          } else {
            int k = vString.IndexOf('.', j+1);
            if (k < j+1)
              build = UInt16.Parse(vString.Substring(j+1, n-j-1), CultureInfo.InvariantCulture);
            else {
              build = UInt16.Parse(vString.Substring(j+1, k-j-1), CultureInfo.InvariantCulture);
              if (vString[k+1] == '*' && allowWildcards) {
                if (j+1 < n-1) return null;
                revision = Checker.SecondsSinceMidnight();
              } else
                revision = UInt16.Parse(vString.Substring(k+1, n-k-1), CultureInfo.InvariantCulture);
            }
          }
        }
      } catch (FormatException) {
        major = minor = build = revision = UInt16.MaxValue;
      } catch (OverflowException) {
        major = minor = build = revision = UInt16.MaxValue;
      }
      if (major == UInt16.MaxValue && minor == UInt16.MaxValue && build == UInt16.MaxValue && revision == UInt16.MaxValue) {
        return null;
      }
      return new Version(major, minor, build, revision);
    }
    private static ushort DaysSince2000() {
      return (ushort)(DateTime.Now - new DateTime(2000, 1, 1)).Days;
    }
    private static ushort SecondsSinceMidnight() {
      TimeSpan sinceMidnight = DateTime.Now - DateTime.Today;
      return (ushort)((sinceMidnight.Hours*60*60+sinceMidnight.Minutes*60+sinceMidnight.Seconds)/2);
    }
    public override Statement VisitAssertion(Assertion assertion) {
      if (assertion == null) return null;
      this.insideAssertOrAssume = true;
      assertion.Condition = this.VisitBooleanExpression(assertion.Condition);
      this.insideAssertOrAssume = false;
      return assertion;
    }
    public override Statement VisitAssumption(Assumption Assumption) {
      if (Assumption == null) return null;
      this.insideAssertOrAssume = true;
      Assumption.Condition = this.VisitBooleanExpression(Assumption.Condition);
      this.insideAssertOrAssume = false;
      return Assumption;
    }
    public override Expression VisitAssignmentExpression(AssignmentExpression assignment) {
      if (assignment == null) return null;
      AssignmentStatement s = assignment.AssignmentStatement as AssignmentStatement;
      if (s == null && assignment.AssignmentStatement != null && assignment.Type != SystemTypes.Void) {
        this.HandleError(assignment.AssignmentStatement, Error.InternalCompilerError);
        return null;
      }
      assignment.AssignmentStatement = (Statement)this.Visit(assignment.AssignmentStatement);
      return assignment;
    }
    public override Statement VisitAssignmentStatement(AssignmentStatement assignment) {
      if (assignment == null) return null;
      if (this.TypeInVariableContext(assignment.Source as Literal))
        return null;
      if (this.insideMethodContract || this.insideAssertOrAssume || this.insideInvariant) {
        this.HandleError(assignment, Error.SideEffectsNotAllowedInContracts);
        return null;
      }
      Composition comp = assignment.Target as Composition;
      if (comp != null) {
        comp.Expression = this.VisitTargetExpression(comp.Expression);
        if (comp.Expression == null) return null;
      } else {
        bool savedMayReferenceThisAndBase = this.MayReferenceThisAndBase;
        MemberBinding mb = assignment.Target as MemberBinding;
        if (assignment.Operator == NodeType.Nop
          && mb != null
          && (mb.TargetObject is ImplicitThis || mb.TargetObject is This)) {
          this.MayReferenceThisAndBase = true;
        }
        assignment.Target = this.VisitTargetExpression(assignment.Target);
        this.MayReferenceThisAndBase = savedMayReferenceThisAndBase;
        if (assignment.Target == null) return null;
      }
      TypeNode t = assignment.Target.Type;
      if (t == null) assignment.Target.Type = t = SystemTypes.Object;
      Expression source = this.VisitExpression(assignment.Source);
      if (source == null) return null;
      Reference rt = t as Reference;
      NodeType oper = assignment.Operator;
      if (rt != null && oper != NodeType.CopyReference) t = rt.ElementType;
      if (oper != NodeType.Nop && oper != NodeType.CopyReference) {
        this.CheckForGetAccessor(assignment.Target);
        LRExpression e = new LRExpression(assignment.Target);
        assignment.Target = e;
        if (assignment.OperatorOverload == null) {
          BinaryExpression be = new BinaryExpression(e, source, assignment.Operator, t, assignment.SourceContext);
          if (assignment.UnifiedType != t && assignment.UnifiedType != null) be.Type = assignment.UnifiedType;
          Expression pop = new Expression(NodeType.Pop, be.Type);
          Pointer pt = t as Pointer;
          if (pt != null && (assignment.Operator == NodeType.Add || assignment.Operator == NodeType.Sub)) {
            if (pt.ElementType != SystemTypes.Int8 && pt.ElementType != SystemTypes.UInt8) {
              UnaryExpression sizeOf = new UnaryExpression(new Literal(pt.ElementType, SystemTypes.Type), NodeType.Sizeof, SystemTypes.UInt32);
              Expression elemSize = PureEvaluator.EvalUnaryExpression((Literal)sizeOf.Operand, sizeOf);
              if (elemSize == null) elemSize = sizeOf;
              TypeNode offsetType = SystemTypes.Int32;
              if (source.Type != null && source.Type.IsPrimitiveInteger) offsetType = source.Type;
              BinaryExpression offset = new BinaryExpression(source, elemSize, NodeType.Mul, offsetType, source.SourceContext);
              Literal offsetLit = PureEvaluator.TryEvalBinaryExpression(source as Literal, elemSize as Literal, offset, this.typeSystem);
              if (offsetLit == null) {
                if (offsetType == SystemTypes.Int32)
                  offset.Operand1 = new UnaryExpression(source, NodeType.Conv_I);
                else
                  offset.Operand2 = this.typeSystem.ExplicitCoercion(elemSize, offsetType, this.TypeViewer);
                source = offset;
              } else
                source = offsetLit;
            }
            source = this.typeSystem.ExplicitCoercion(source, pt, this.TypeViewer);
            be.Operand2 = source;
            assignment.Source = be;
            return assignment;
          }
          source = this.CoerceBinaryExpressionOperands(be, pop, source);
          if (source == null) return null;
          if (source == pop)
            source = e;
          else if (!(source is Literal)) {
            be.Operand1 = e;
          }
        }
        assignment.Operator = NodeType.Nop;
        if (!t.IsPrimitiveNumeric && t != SystemTypes.Char && t != SystemTypes.Boolean && assignment.OperatorOverload == null &&
          (assignment.UnifiedType == null || !assignment.UnifiedType.IsPrimitiveNumeric) &&
          oper != NodeType.AddEventHandler && oper != NodeType.RemoveEventHandler && 
          !(t is DelegateNode && (source.NodeType == NodeType.Add || source.NodeType == NodeType.Sub)) && !(t is EnumNode)) {
          this.HandleError(assignment, Error.BadBinaryOps,
            this.GetOperatorSymbol(oper), this.GetTypeName(t), this.GetTypeName(assignment.Source.Type));
          return null;
        }
        if (assignment.OperatorOverload != null) {
          if (assignment.OperatorOverload.Parameters == null || assignment.OperatorOverload.Parameters.Count < 2) {
            Debug.Assert(false); return null;
          }
          source = this.typeSystem.ImplicitCoercion(source, assignment.OperatorOverload.Parameters[1].Type, this.TypeViewer);
          ExpressionList arguments = new ExpressionList(e, source);
          source = new MethodCall(new MemberBinding(null, assignment.OperatorOverload), arguments, NodeType.Call, assignment.OperatorOverload.ReturnType);
          assignment.OperatorOverload = null;
        }
      }
      assignment.Source = this.typeSystem.ImplicitCoercion(source, t, this.TypeViewer);
      return assignment;
    }
    public virtual Expression VisitAttributeConstructor(AttributeNode attribute, Node target) {
      if (attribute == null) return null;
      MemberBinding mb = attribute.Constructor as MemberBinding;
      if (mb == null) { Debug.Assert(false); return null; }
      InstanceInitializer cons = mb.BoundMember as InstanceInitializer;
      if (cons == null) return null;
      TypeNode t = cons.DeclaringType;
      if (this.GetTypeView(t).IsAssignableTo(SystemTypes.Attribute)) {
        if (!this.CheckAttributeTarget(attribute, target, mb, t)) return null;
        this.CheckForObsolesence(mb, cons.DeclaringType);
        this.CheckForObsolesence(mb, cons);
        return mb;
      }
      Debug.Assert(false);
      this.HandleError(mb, Error.NotAnAttribute, this.GetTypeName(t));
      this.HandleRelatedError(t);
      return null;
    }
    public virtual bool CheckAttributeTarget(AttributeNode attribute, Node target, Node offendingNode, TypeNode attributeType) {
      Debug.Assert(attribute != null && target != null);
      if (attribute == null || target == null) return false;
      AttributeTargets validTargets = (AttributeTargets)attribute.Target;
      if (attribute.Target == (AttributeTargets)0)
        validTargets = attribute.ValidOn;
      else
        validTargets &= attribute.ValidOn;
      switch (target.NodeType) {
        case NodeType.Assembly:
          if ((validTargets & (AttributeTargets.Assembly|AttributeTargets.Module)) != 0) return true;
          validTargets = AttributeTargets.Assembly;
          break;
        case NodeType.Class:
          if ((validTargets & AttributeTargets.Class) != 0) return true;
          validTargets = AttributeTargets.Class|AttributeTargets.Delegate|AttributeTargets.Enum|AttributeTargets.Interface|AttributeTargets.Struct;
          break;
        case NodeType.InstanceInitializer:
        case NodeType.StaticInitializer:
          if ((validTargets & AttributeTargets.Constructor) != 0) return true;
          validTargets = AttributeTargets.Constructor;
          break;
        case NodeType.DelegateNode:
          if ((validTargets & AttributeTargets.Delegate) != 0) return true;
          if ((attribute.Target & AttributeTargets.ReturnValue) != 0) return true;
          validTargets = AttributeTargets.Class|AttributeTargets.Delegate|AttributeTargets.Enum|AttributeTargets.Interface|AttributeTargets.Struct;
          break;
        case NodeType.EnumNode:
          if ((validTargets & AttributeTargets.Enum) != 0) return true;
          validTargets = AttributeTargets.Class|AttributeTargets.Delegate|AttributeTargets.Enum|AttributeTargets.Interface|AttributeTargets.Struct;
          break;
        case NodeType.Event:
          if ((validTargets & AttributeTargets.Event) != 0) return true;
          if ((validTargets & AttributeTargets.Field) != 0 && ((Event)target).BackingField != null) return true;
          validTargets = AttributeTargets.Event;
          break;
        case NodeType.Field:
          if ((validTargets & AttributeTargets.Field) != 0) return true;
          validTargets = AttributeTargets.Field;
          break;
        case NodeType.Interface:
          if ((validTargets & AttributeTargets.Interface) != 0) return true;
          validTargets = AttributeTargets.Class|AttributeTargets.Delegate|AttributeTargets.Enum|AttributeTargets.Interface|AttributeTargets.Struct;
          break;
        case NodeType.Method:
          if ((validTargets & AttributeTargets.Method) != 0) return true;
          if (attribute.Target == AttributeTargets.ReturnValue && ((attribute.ValidOn & AttributeTargets.ReturnValue) != 0)) return true;
          if ((attribute.Target & AttributeTargets.Parameter) != 0 && ((Method)target).HasCompilerGeneratedSignature) return true;
          validTargets = AttributeTargets.Method;
          break;
        case NodeType.Module:
          if ((validTargets & AttributeTargets.Module) != 0) return true;
          validTargets = AttributeTargets.Module;
          break;
        case NodeType.Parameter:
          if ((validTargets & AttributeTargets.Parameter) != 0) return true;
          validTargets = AttributeTargets.Parameter;
          break;
        case NodeType.Property:
          if ((validTargets & AttributeTargets.Property) != 0) return true;
          validTargets = AttributeTargets.Property;
          break;
        case NodeType.Struct:
          if ((validTargets & AttributeTargets.Struct) != 0) return true;
          validTargets = AttributeTargets.Class|AttributeTargets.Delegate|AttributeTargets.Enum|AttributeTargets.Interface|AttributeTargets.Struct;
          break;
        case NodeType.TypeParameter:
        case NodeType.ClassParameter:
#if WHIDBEY
          if ((validTargets & AttributeTargets.GenericParameter) != 0) return true;
          validTargets = AttributeTargets.GenericParameter;
          break;
#else
          return true;
#endif
        default:
          break;
      }
      if (attribute.Target != (AttributeTargets)0 && ((attribute.Target & attribute.ValidOn) == attribute.Target))
        this.HandleError(attribute, Error.AttributeHasBadTarget, this.GetAttributeTargetName(attribute.Target), this.GetAttributeTargetName(validTargets));
      else
        this.HandleError(offendingNode, Error.AttributeOnBadTarget, this.GetAttributeTypeName(attributeType), this.GetAttributeTargetName(attribute.ValidOn));
      return false;
    }
    public override AttributeList VisitAttributeList(AttributeList attributes) {
      //This gets called by boiler plate code. Do nothing. The next overload takes care of the real work.
      return attributes;
    }
    public virtual AttributeList VisitAttributeList(AttributeList attributes, Node target) {
      if (attributes == null) return null;
      TypeNode targetType = target as TypeNode;
      TrivialHashtable alreadyPresent = null;
      for (int i = 0, n = attributes.Count; i < n; i++) {
        AttributeNode attr = attributes[i];
        if (attr == null) continue;
        if (!attr.AllowMultiple) {
          TypeNode attrType = attr.Type;
          if (attrType == null) continue;
          if (alreadyPresent == null)
            alreadyPresent = new TrivialHashtable();
          else if (alreadyPresent[attrType.UniqueKey] != null) {
            if (attr.Constructor.SourceContext.Document != null) {
              Error e = Error.None;
              AttributeTargets attrTarget = attr.Target;
              for (int j = 0; j < i; j++) {
                AttributeNode a = attributes[j];
                if (a == null) a = alreadyPresent[attrType.UniqueKey] as AttributeNode;
                if (a == null) continue;
                if (a.Type == attr.Type && a.Target == attrTarget) {
                  e = Error.DuplicateAttribute;
                  break;
                }
              }
              if (e != Error.None)
                this.HandleError(attr.Constructor, e, attr.Constructor.SourceContext.SourceText);
            }
            attributes[i] = null;
            continue;
          }
          alreadyPresent[attrType.UniqueKey] = attr;
        }
        attributes[i] = this.VisitAttributeNode(attributes[i], target);
      }
      return attributes;
    }
    public override AttributeNode VisitAttributeNode(AttributeNode attribute) {
      //Next overload does real work
      return attribute;
    }
    public virtual AttributeNode VisitAttributeNode(AttributeNode attribute, Node target) {
      if (attribute == null || target == null) return null;
      attribute.Constructor = this.VisitAttributeConstructor(attribute, target);
      ExpressionList expressions = attribute.Expressions = this.VisitExpressionList(attribute.Expressions);
      MemberBinding mb = attribute.Constructor as MemberBinding;
      if (mb == null || mb.BoundMember == null) {
        Debug.Assert(attribute.Constructor == null);
        return null;
      }
      //Check arguments for validity
      TypeNode attributeType = mb.BoundMember.DeclaringType;
      if (attributeType == null) return null;
      InstanceInitializer ctor = (InstanceInitializer)mb.BoundMember;
      ParameterList pars = ctor.Parameters;
      ExpressionList positionalArgs = new ExpressionList();
      TrivialHashtable alreadySeenNames = new TrivialHashtable();
      for (int i = 0, n = expressions == null ? 0 : expressions.Count; i < n; i++) {
        Expression e = expressions[i];
        this.TypeInVariableContext(e as Literal);
        NamedArgument narg = e as NamedArgument;
        if (narg == null) { positionalArgs.Add(e); expressions[i] = null; continue; }
        if (narg.Name == null) { expressions[i] = null; continue; }
        if (alreadySeenNames[narg.Name.UniqueIdKey] != null) {
          this.HandleError(narg.Name, Error.DuplicateNamedAttributeArgument, narg.Name.ToString());
          expressions[i] = null; continue;
        }
        alreadySeenNames[narg.Name.UniqueIdKey] = narg.Name;
        Member mem = null;
        TypeNode aType = attributeType;
        while (aType != null) {
          MemberList members = this.GetTypeView(aType).GetMembersNamed(narg.Name);
          for (int j = 0, m = members == null ? 0 : members.Count; j < m; j++) {
            mem = members[j];
            if (mem == null) continue;
            switch (mem.NodeType) {
              case NodeType.Field:
                if (!mem.IsPublic) goto error;
                Field f = (Field)mem;
                if (f.IsInitOnly || f.IsLiteral || f.IsStatic) goto error;
                if (!this.IsValidTypeForCustomAttributeParameter(f.Type)) {
                  this.HandleError(narg, Error.BadNamedAttributeArgumentType, this.GetMemberSignature(f));
                  this.HandleRelatedError(f);
                  return null;
                }
                this.CheckForObsolesence(narg, f);
                narg.IsCustomAttributeProperty = false;
                e = this.typeSystem.ImplicitCoercion(narg.Value, narg.Type = f.Type, this.TypeViewer);
                if (!this.IsValidTypeForCustomAttributeArgument(e, narg.Value)) return null;
                if (e is BinaryExpression && e.NodeType == NodeType.Box) {
                  narg.ValueIsBoxed = true;
                  e = ((BinaryExpression)e).Operand1;
                }
                narg.Value = e;
                goto doneWithArg;
              case NodeType.Property:
                if (!mem.IsPublic) goto error;
                Property p = (Property)mem;
                if (!this.IsValidTypeForCustomAttributeParameter(p.Type)) {
                  this.HandleError(narg, Error.BadNamedAttributeArgumentType, this.GetMemberSignature(p));
                  this.HandleRelatedError(p);
                  return null;
                }
                if (p.Setter == null || p.Getter == null || p.IsStatic || !p.Setter.IsPublic || !p.Getter.IsPublic) goto error;
                this.CheckForObsolesence(narg, p);
                narg.IsCustomAttributeProperty = true;
                e = this.typeSystem.ImplicitCoercion(narg.Value, narg.Type = p.Type, this.TypeViewer);
                if (!this.IsValidTypeForCustomAttributeArgument(e, narg.Value)) return null;
                if (e is BinaryExpression && e.NodeType == NodeType.Box) {
                  narg.ValueIsBoxed = true;
                  e = ((BinaryExpression)e).Operand1;
                }
                narg.Value = e;
                goto doneWithArg;
            }
          }
          aType = aType.BaseType;
        }
      error:
        if (mem != null) {
          this.HandleError(narg, Error.BadNamedAttributeArgument, narg.Name.ToString());
          this.HandleRelatedError(mem);
        } else
          this.HandleError(narg, Error.NoSuchMember, this.GetTypeName(attributeType), narg.Name.ToString());
      doneWithArg: ;
      }
      ExpressionList exprs = positionalArgs.Clone();
      this.CoerceArguments(pars, ref positionalArgs, true, ctor.CallingConvention);
      attribute.Expressions = positionalArgs;
      for (int i = 0, n = positionalArgs == null ? 0 : positionalArgs.Count; i < n; i++) {
        Expression e = positionalArgs[i];
        if (e == null) continue;
        if (!this.IsValidTypeForCustomAttributeArgument(e, exprs[i])) return null;
        if (e is BinaryExpression && e.NodeType == NodeType.Box) e = ((BinaryExpression)e).Operand1;
        positionalArgs[i] = e;
      }
      for (int i = 0, n = expressions == null ? 0 : expressions.Count; i < n; i++) {
        Expression e = expressions[i];
        if (e == null) continue;
        positionalArgs.Add(e);
      }
      attribute.Expressions = positionalArgs;
      //Now call specific visitors to deal with any pseudo custom attributes that describe metadata settings for target
      switch (target.NodeType) {
        case NodeType.Assembly: return this.VisitAssemblyAttribute(attribute, (AssemblyNode)target);
        case NodeType.Field: return this.VisitFieldAttribute(attribute, (Field)target);
        case NodeType.InstanceInitializer:
        case NodeType.StaticInitializer:
        case NodeType.Method: return this.VisitMethodAttribute(attribute, (Method)target);
        case NodeType.Property: return this.VisitPropertyAttribute(attribute, (Property)target);
        case NodeType.Parameter: return this.VisitParameterAttribute(attribute, (Parameter)target);
        default:
          TypeNode t = target as TypeNode;
          if (t != null) return this.VisitTypeAttribute(attribute, t);
          break;
      }
      return attribute;
    }
    public virtual bool IsValidTypeForLiteral(TypeNode type) {
      if (type == null) return false;
      return type.IsPrimitive || type == SystemTypes.String || type == SystemTypes.Type || type is EnumNode;
    }
    public virtual bool IsValidTypeForCustomAttributeParameter(TypeNode type) {
      type = TypeNode.StripModifier(type, SystemTypes.NonNullType);
      if (type == null) return false;
      return type.IsPrimitive || type == SystemTypes.String || type == SystemTypes.Type || type is EnumNode ||
        type == SystemTypes.Object || (type is ArrayType && (this.IsValidTypeForLiteral(((ArrayType)type).ElementType) || ((ArrayType)type).ElementType == SystemTypes.Object));
    }
    public virtual bool IsValidTypeForCustomAttributeArgument(Expression e, Node offendingNode) {
      if (e == null) return false;
      if (e.NodeType == NodeType.Typeof) return true;
      Literal lit = e as Literal;
      if (lit != null) {
        if (this.IsValidTypeForLiteral(e.Type) || 
          (e.Type == SystemTypes.Object && lit.Value == null) ||
          (e.Type is ArrayType && ((ArrayType)e.Type).ElementType == SystemTypes.Type && lit.Value is TypeNode[])) return true;
        this.HandleError(offendingNode, Error.BadAttributeParam, this.GetTypeName(e.Type));
        return false;
      }
      BinaryExpression be = e as BinaryExpression;
      if (be != null && be.NodeType == NodeType.Box)
        return this.IsValidTypeForCustomAttributeArgument(be.Operand1, offendingNode);
      ConstructArray consArr = e as ConstructArray;
      if (consArr == null || consArr.ElementType == null || !(this.IsValidTypeForLiteral(consArr.ElementType) || consArr.ElementType == SystemTypes.Object)) {
        this.HandleError(offendingNode, Error.BadAttributeParam, this.GetTypeName(e.Type));
        return false;
      }
      if (consArr != null) {
        ExpressionList exprs = consArr.Initializers;
        for (int i = 0, n = exprs == null ? 0 : exprs.Count; i < n; i++) {
          Expression expr = exprs[i];
          if (expr != null && expr.NodeType == NodeType.Typeof) continue;
          be = exprs[i] as BinaryExpression;
          if (be != null && be.NodeType == NodeType.Box) expr = be.Operand1;
          lit = expr as Literal;
          if (lit != null && (this.IsValidTypeForLiteral(lit.Type) || (lit.Type == SystemTypes.Object && lit.Value == null))) continue;
          consArr = expr as ConstructArray;
          if (consArr != null && consArr.ElementType != null && this.IsValidTypeForLiteral(consArr.ElementType)) continue;
          this.HandleError(offendingNode, Error.BadAttributeParam, this.GetTypeName(e.Type));
          return false;
        }
      }
      return true;
    }
    public override Expression VisitBase(Base Base) {
      if (this.currentMethod == null) {
        this.HandleError(Base, Error.BaseInBadContext);
        return null;
      } else if (this.currentMethod.IsStatic) {
        if (this.currentMethod.NodeType == NodeType.Invariant)
          this.HandleError(Base, Error.BaseInBadContext);
        else
          this.HandleError(Base, Error.BaseInStaticCode);
        return null;
      }
      return Base;
    }
    public override Expression VisitApplyToAll(ApplyToAll applyToAll) {
      if (applyToAll == null) return null;
      Expression collection = applyToAll.Operand1;
      if (collection == null) { Debug.Assert(false); return null; }
      TypeNode elemType = this.typeSystem.GetStreamElementType(collection.Type, this.TypeViewer);
      bool singleTon = elemType == collection.Type;
      Local loc = applyToAll.ElementLocal;
      if (loc == null) loc = new Local(elemType);
      if (singleTon)
        applyToAll.Operand1 = this.VisitExpression(collection);
      else
        applyToAll.Operand1 = this.VisitEnumerableCollection(collection, loc.Type);
      if (applyToAll.Operand1 == null) return null;
      AnonymousNestedFunction func = applyToAll.Operand2 as AnonymousNestedFunction;
      Expression expr = applyToAll.Operand2 as BlockExpression;
      if (func != null || expr != null) {
        this.VisitAnonymousNestedFunction(func);
        if (singleTon || applyToAll.Type == SystemTypes.Void) return applyToAll;
        //Create an iterator to compute stream that results from ApplyToAll
        Class closureClass = this.currentMethod.Scope.ClosureClass;
        Method method = new Method();
        method.Name = Identifier.For("Function:"+method.UniqueKey);
        method.SourceContext = collection.SourceContext;
        method.Flags = MethodFlags.CompilerControlled;
        method.CallingConvention = CallingConventionFlags.HasThis;
        method.InitLocals = true;
        method.DeclaringType = closureClass;
        closureClass.Members.Add(method);
        method.Scope = new MethodScope(new TypeScope(null, closureClass), null);
        Parameter coll = new Parameter(StandardIds.Collection, collection.Type);
        ParameterField fcoll = new ParameterField(method.Scope, null, FieldFlags.CompilerControlled, StandardIds.Collection, collection.Type, null);
        fcoll.Parameter = coll;
        method.Scope.Members.Add(fcoll);
        Parameter closure = new Parameter(StandardIds.Closure, closureClass);
        ParameterField fclosure = new ParameterField(method.Scope, null, FieldFlags.CompilerControlled, StandardIds.Closure, closureClass, null);
        fclosure.Parameter = closure;
        if (func != null) {
          method.Scope.Members.Add(fclosure);
          method.Parameters = new ParameterList(coll, closure);
        } else
          method.Parameters = new ParameterList(coll);
        method.ReturnType = applyToAll.Type;
        ForEach forEach = new ForEach();
        forEach.TargetVariable = loc;
        forEach.TargetVariableType = loc.Type;
        forEach.SourceEnumerable = new MemberBinding(new ImplicitThis(), fcoll);
        if (func != null) {
          MemberBinding mb = new MemberBinding(new MemberBinding(new ImplicitThis(), fclosure), func.Method);
          expr = new MethodCall(mb, new ExpressionList(loc), NodeType.Call, func.Method.ReturnType, func.SourceContext);
        } else
          expr = this.VisitExpression(expr);
        expr = this.typeSystem.ImplicitCoercion(expr, this.typeSystem.GetStreamElementType(applyToAll.Type, this.TypeViewer), this.TypeViewer);
        if (expr == null) return null;
        BlockExpression bExpr = expr as BlockExpression;
        if (bExpr != null && bExpr.Type == SystemTypes.Void)
          forEach.Body = bExpr.Block;
        else
          forEach.Body = new Block(new StatementList(new Yield(expr)));
        forEach.ScopeForTemporaryVariables = new BlockScope(method.Scope, forEach.Body);
        method.Body = new Block(new StatementList(forEach));
        applyToAll.ResultIterator = this.VisitMethod(method);
        return applyToAll;
      }
      Debug.Assert(false);
      return null;
    }
    public override Expression VisitBinaryExpression(BinaryExpression binaryExpression) {
      if (binaryExpression == null) return null;
      Expression opnd1 = binaryExpression.Operand1 = this.VisitExpression(binaryExpression.Operand1);
      Expression opnd2 = binaryExpression.Operand2 = this.VisitExpression(binaryExpression.Operand2);
      return this.CoerceBinaryExpressionOperands(binaryExpression, opnd1, opnd2);
    }
    public virtual TypeNode InferTypeOfIntegerLiteral(Literal lit) {
      if (lit == null) { Debug.Assert(false); return null; }
      if (!(lit.Value is int)) return lit.Type;
      int val = (int)lit.Value;
      if (val >= 0) {
        if (val <= byte.MaxValue) return SystemTypes.UInt8;
        if (val <= ushort.MaxValue) return SystemTypes.UInt16;
      } else {
        if (val >= sbyte.MinValue && val <= sbyte.MaxValue) return SystemTypes.Int8;
        if (val >= short.MinValue && val <= short.MaxValue) return SystemTypes.Int16;
      }
      return SystemTypes.Int32;
    }
    public virtual Expression CoerceBinaryExpressionOperands(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2) {
      if (opnd1 == null) return null;
      if (opnd2 == null) return null;
      Literal lit1 = opnd1 as Literal;
      Literal lit2 = opnd2 as Literal;
      TypeNode opnd1Type = TypeNode.StripModifiers(opnd1.Type);
      TypeNode opnd2Type = TypeNode.StripModifiers(opnd2.Type);
      switch (binaryExpression.NodeType) {
        case NodeType.Add:
          return this.CoerceOperandsForAdd(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.AddEventHandler:
          return this.CoerceOperandsForAddEventHandler(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.And:
          return this.CoerceOperandsForAnd(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.As:
          return this.CoerceOperandsForAs(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Box:
          return this.CoerceOperandForBox(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Castclass: //TODO: at some point is should be a mistake to have one of these in an AST before Checker has run
          return this.CoerceOperandsForExplicitCoercion(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Ceq:
          return this.CoerceOperandsForCeq(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Comma:
          return this.CoerceOperandsForComma(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Cgt:
          return this.CoerceOperandsForCgt(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Cgt_Un:
          return this.CoerceOperandsForCgtUn(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Clt:
          return this.CoerceOperandsForClt(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Clt_Un:
          return this.CoerceOperandsForCltUn(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Div:
          return this.CoerceOperandsForDiv(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Eq:
          return this.CoerceOperandsForEq(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.ExplicitCoercion:
          return this.CoerceOperandsForExplicitCoercion(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Ge:
          return this.CoerceOperandsForGe(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Gt:
          return this.CoerceOperandsForGt(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Iff:
          return this.CoerceOperandsForIff(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Implies:
          return this.CoerceOperandsForImplies(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Is:
          return this.CoerceOperandsForIs(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Isinst:
          return this.CoerceOperandsForIsinst(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Ldvirtftn:
          return this.CoerceOperandsForLdvirtftn(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Le:
          return this.CoerceOperandsForLe(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.LogicalAnd:
          return this.CoerceOperandsForLogicalAnd(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.LogicalOr:
          return this.CoerceOperandsForLogicalOr(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Lt:
          return this.CoerceOperandsForLt(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Maplet:
          return this.CoerceOperandsForMaplet(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Mkrefany:
          return this.CoerceOperandsForMkrefany(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Mul:
          return this.CoerceOperandsForMul(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Ne:
          return this.CoerceOperandsForNe(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.NullCoalesingExpression:
          return this.CoerceOperandsForNullCoalescing(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Or:
          return this.CoerceOperandsForOr(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Range:
          return this.CoerceOperandsForRange(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Refanyval:
          return this.CoerceOperandsForRefanyval(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Rem:
          return this.CoerceOperandsForRem(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.RemoveEventHandler:
          return this.CoerceOperandsForRemoveEventHandler(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Shl:
          return this.CoerceOperandsForShl(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Shr:
          return this.CoerceOperandsForShr(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Shr_Un:
          return this.CoerceOperandsForShrUn(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Sub:
          return this.CoerceOperandsForSub(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Unbox:
          return this.CoerceOperandForUnbox(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        case NodeType.Xor:
          return this.CoerceOperandsForXor(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
        default:
          return this.CoerceOperandsForUnknownOperator(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
      }
    }
    public virtual Expression CoerceOperandsForAnd(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceBitwiseBinaryOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForCeq(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceComparisonOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForCgt(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceComparisonOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForCgtUn(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceComparisonOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForClt(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceComparisonOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForCltUn(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceComparisonOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForEq(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceEqualityOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForGe(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceComparisonOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForGt(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceComparisonOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForLe(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceComparisonOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForLt(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceComparisonOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForNe(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceEqualityOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForNullCoalescing(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      if (this.TypeInVariableContext(lit1)) return null;
      if (this.TypeInVariableContext(lit2)) return null;
      TypeNode nonNullT1 = this.typeSystem.RemoveNullableWrapper(opnd1Type);
      if (this.typeSystem.IsNullableType(opnd1Type) && this.typeSystem.ImplicitCoercionFromTo(opnd2Type, nonNullT1))
        return binaryExpression;
      else if (this.typeSystem.ImplicitCoercionFromTo(opnd2, opnd2Type, opnd1Type)) {
        if (opnd1Type is DelegateNode && opnd2Type is FunctionType)
          binaryExpression.Operand2 = this.typeSystem.ImplicitCoercion(opnd2, opnd1Type);
        return binaryExpression;
      } else if (this.typeSystem.ImplicitCoercionFromTo(nonNullT1, opnd2Type))
        return binaryExpression;
      if (opnd1Type != null && opnd2Type != null)
          {
              this.HandleError(binaryExpression, Error.BadBinaryOps, this.GetOperatorSymbol(binaryExpression.NodeType), this.GetTypeName(opnd1Type), this.GetTypeName(opnd2Type));
          }
      return null;
    }
    public virtual Expression CoerceOperandsForOr(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceBitwiseBinaryOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForXor(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceBitwiseBinaryOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForLogicalAnd(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceShortCircuitOps(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForLogicalOr(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceShortCircuitOps(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForImplies(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceShortCircuitOps(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForIff(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceShortCircuitOps(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForShl(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceShiftOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForShr(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceShiftOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForShrUn(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceShiftOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForUnknownOperator(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      TypeNode unifiedType = binaryExpression.Type;
      return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, unifiedType);
    }
    public virtual Expression CoerceOperandsForAddEventHandler(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return binaryExpression; //TODO: appropriate type checking
    }
    public virtual Expression CoerceOperandsForRemoveEventHandler(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return binaryExpression; //TODO: appropriate type checking
    }
    public virtual Expression CoerceOperandsForComma(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return binaryExpression; //TODO: appropriate type checking
    }
    public virtual Expression CoerceOperandsForLdvirtftn(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return binaryExpression; //TODO: appropriate type checking
    }
    public virtual Expression CoerceOperandsForMkrefany(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return binaryExpression; //TODO: appropriate type checking
    }
    public virtual Expression CoerceOperandsForRefanyval(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return binaryExpression; //TODO: appropriate type checking
    }
    public virtual Expression CoerceShiftOperands(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      if (binaryExpression.NodeType == NodeType.Shr && (opnd1Type == SystemTypes.Char || (opnd1Type != null && opnd1Type.IsUnsignedPrimitiveNumeric)))
        binaryExpression.NodeType = NodeType.Shr_Un;
      if (this.typeSystem.ImplicitCoercionFromTo(opnd1Type, SystemTypes.Int32, this.TypeViewer))
        opnd1 = this.typeSystem.ImplicitCoercion(opnd1, SystemTypes.Int32, this.TypeViewer);
      else if (this.typeSystem.ImplicitCoercionFromTo(opnd1Type, SystemTypes.UInt32, this.TypeViewer))
        opnd1 = this.typeSystem.ImplicitCoercion(opnd1, SystemTypes.UInt32, this.TypeViewer);
      else if (this.typeSystem.ImplicitCoercionFromTo(opnd1Type, SystemTypes.Int64, this.TypeViewer))
        opnd1 = this.typeSystem.ImplicitCoercion(opnd1, SystemTypes.Int64, this.TypeViewer);
      else if (this.typeSystem.ImplicitCoercionFromTo(opnd1Type, SystemTypes.UInt64, this.TypeViewer))
        opnd1 = this.typeSystem.ImplicitCoercion(opnd1, SystemTypes.UInt64, this.TypeViewer);
      else if (opnd1Type is EnumNode && this.currentType is EnumNode)
        opnd1 = this.typeSystem.ExplicitCoercion(opnd1, ((EnumNode)opnd1Type).UnderlyingType, this.TypeViewer);
      else {
        this.ReportBadOperands(binaryExpression, Error.BadBinaryOps, lit1, lit2, opnd1Type, opnd2Type);
        return null;
      }
      if (!this.typeSystem.ImplicitCoercionFromTo(opnd2Type, SystemTypes.Int32, this.TypeViewer)) {
        if (opnd2Type is EnumNode && this.currentType is EnumNode)
          opnd2 = this.typeSystem.ExplicitCoercion(opnd2, ((EnumNode)opnd2Type).UnderlyingType, this.TypeViewer);
        else {
          this.ReportBadOperands(binaryExpression, Error.BadBinaryOps, lit1, lit2, opnd1Type, opnd2Type);
          return null;
        }
      }
      binaryExpression.Operand1 = opnd1;
      binaryExpression.Operand2 = opnd2 = this.typeSystem.ImplicitCoercion(opnd2, SystemTypes.Int32, this.TypeViewer);
      TypeNode targetType = binaryExpression.Type;
      binaryExpression.Type = opnd1.Type;
      return this.typeSystem.ExplicitCoercion(binaryExpression, targetType, this.TypeViewer);
    }
    public virtual Expression CoerceOperandsForRange(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceBinaryOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceOperandsForMaplet(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      binaryExpression.Operand1 = this.typeSystem.ImplicitCoercion(opnd1, SystemTypes.Object, this.TypeViewer);
      binaryExpression.Operand2 = this.typeSystem.ImplicitCoercion(opnd2, SystemTypes.Object, this.TypeViewer);
      return binaryExpression;
    }
    public virtual Expression CoerceOperandsForIs(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      TypeNode unifiedType = binaryExpression.Type;
      if (this.TypeInVariableContext(opnd1 as Literal)) return null;
      Debug.Assert(unifiedType == SystemTypes.Boolean);
      TypeNode opnd2Value = null;
      Literal lit = opnd2 as Literal;
      if (lit != null)
        opnd2Value = lit.Value as TypeNode;
      else {
        MemberBinding mb = opnd2 as MemberBinding;
        if (mb != null)
          opnd2Value = mb.BoundMember as TypeNode;
      }
      opnd2Value = this.VisitTypeReference(opnd2Value);
      bool opnd2IsNonNullType = false;
      if (opnd2Value != null) {
        opnd2IsNonNullType = this.typeSystem.IsNonNullType(opnd2Value);
        opnd2Value = TypeNode.StripModifiers(opnd2Value);
      } else
        return null;
      Reference refType = opnd1Type as Reference;
      if (refType != null) {
        opnd1 = binaryExpression.Operand1 = this.typeSystem.ExplicitCoercion(opnd1, refType.ElementType, this.TypeViewer);
        opnd1Type = TypeNode.StripModifier(refType.ElementType, SystemTypes.NonNullType);
      }
      TypeIntersection tIntersect = opnd1Type as TypeIntersection;
      if (tIntersect != null && tIntersect.Types != null) {
        int i = tIntersect.Types.SearchFor(opnd2Value);
        if (i < 0) {
          this.HandleError(opnd1, Error.IsNeverOfType, this.GetTypeName(opnd2Value));
          return new Literal(false, SystemTypes.Boolean);
        }
        opnd1 = new UnaryExpression(opnd1, NodeType.AddressOf, opnd1.Type.GetReferenceType());
        Expression getValue = new MethodCall(new MemberBinding(opnd1, tIntersect.GetMembersNamed(StandardIds.GetValue)[0]), null);
        getValue.Type = SystemTypes.Object;
        opnd1 = getValue;
        return binaryExpression;
      }
      TypeUnion tUnion = opnd1Type as TypeUnion;
      if (tUnion != null && tUnion.Types != null) {
        int i = tUnion.Types.SearchFor(opnd2Value);
        if (i < 0) {
          this.HandleError(opnd1, Error.IsNeverOfType, this.GetTypeName(opnd2Value));
          return new Literal(false, SystemTypes.Boolean);
        }
        opnd1 = new UnaryExpression(opnd1, NodeType.AddressOf, opnd1.Type.GetReferenceType());
        Expression getValue = new MethodCall(new MemberBinding(opnd1, tUnion.GetMembersNamed(StandardIds.GetValue)[0]), null);
        getValue.Type = SystemTypes.Object;
        binaryExpression.Operand1 = getValue;
        return binaryExpression;
      }
      if (opnd1Type is Pointer) {
        this.HandleError(binaryExpression, Error.PointerInAsOrIs);
        return null;
      }
      if (opnd1Type != null && this.GetTypeView(opnd1Type).IsAssignableTo(opnd2Value) && opnd1Type.IsValueType && opnd1Type.Template != SystemTypes.GenericBoxed) {
        this.HandleError(opnd1, Error.IsAlwaysOfType, this.GetTypeName(opnd2Value));
        return Literal.True;
      }
      if (opnd1Type != null && !(opnd1Type is ITypeParameter) && !(opnd1Type is Interface) && !(opnd2Value is Interface && !opnd1Type.IsSealed) && !this.GetTypeView(opnd1Type).IsAssignableTo(opnd2Value) && !this.GetTypeView(opnd2Value).IsAssignableTo(opnd1Type)) {
        this.HandleError(binaryExpression, Error.IsNeverOfType, this.GetTypeName(opnd2Value));
        return Literal.False;
      }
      if (opnd2IsNonNullType) {
        binaryExpression.Operand2 = new Literal(opnd2Value, SystemTypes.Type);
      }
      return binaryExpression;
      //TODO: check if condition is always true or always false. Give an appropriate message and return literal.
    }
    public virtual Expression CoerceShortCircuitOps(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {

      TypeNode unifiedType = binaryExpression.Type;
      //HS D
      if (opnd1 is Hole || opnd2 is Hole)
          {
              if (opnd1 is Hole)
                  {
                      ((Hole)opnd1).Type = unifiedType;
                      binaryExpression.Operand1 = opnd1;
                  }
              if (opnd2 is Hole)
                  {
                      ((Hole)opnd2).Type = unifiedType;
                      binaryExpression.Operand2 = opnd2;
                  }
              return binaryExpression;
          }
      //HS D
      if (opnd1 is LambdaHole || opnd2 is LambdaHole)
          {
              if (opnd1 is LambdaHole)
                  {
                      if (unifiedType == SystemTypes.Boolean)
                          opnd1 = new LambdaHole(opnd1, new Literal(0), NodeType.Ge, opnd1.SourceContext);                      
                      opnd1.Type = unifiedType;
                      binaryExpression.Operand1 = opnd1;
                  }
              if (opnd2 is LambdaHole)
                  {
                      if (unifiedType == SystemTypes.Boolean)
                          opnd2 = new LambdaHole(opnd2, new Literal(0), NodeType.Ge, opnd2.SourceContext);                      
                      opnd2.Type = unifiedType;
                      binaryExpression.Operand2 = opnd2;
                  }
              return binaryExpression;
          }
      MethodCall mcall = opnd2 as MethodCall;
      if (mcall != null && opnd1 is Local) {
        MemberBinding mb = mcall.Callee as MemberBinding;
        Method oper = mb == null ? null : mb.BoundMember as Method;
        if (oper != null && (oper.Name.UniqueIdKey == StandardIds.opBitwiseAnd.UniqueIdKey || oper.Name.UniqueIdKey == StandardIds.opBitwiseOr.UniqueIdKey)) {
          //Found a user defined overload for & or |, check signature and check for presence of op_True and op_False
          unifiedType = oper.ReturnType;
          if (unifiedType != oper.DeclaringType || unifiedType != oper.Parameters[0].Type || unifiedType != oper.Parameters[1].Type) {
            this.HandleError(binaryExpression, Error.BadBoolOp, this.GetMethodSignature(oper));
            return null;
          }
          if (unifiedType == null) return null;
          if (this.GetTypeView(unifiedType).GetOpTrue() != null) return CoercedBinaryExpression(binaryExpression, opnd1, opnd2, unifiedType);
          this.HandleError(binaryExpression, Error.MustHaveOpTF, this.GetTypeName(unifiedType));
          return null;
        }
      }
      if (!this.typeSystem.ImplicitCoercionFromTo(opnd1Type, SystemTypes.Boolean, this.TypeViewer) || !this.typeSystem.ImplicitCoercionFromTo(opnd2Type, SystemTypes.Boolean, this.TypeViewer)) {
        this.ReportBadOperands(binaryExpression, Error.BadBinaryOps, lit1, lit2, opnd1Type, opnd2Type);
        return null;
      }
      unifiedType = SystemTypes.Boolean;
      return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, unifiedType);
    }
    public virtual Expression CallHasValue(Expression expr) {
      if (expr == null || expr.Type == null || !this.typeSystem.IsNullableType(expr.Type)) {
        Debug.Assert(false); return null;
      }
      Method getHasValue = this.GetTypeView(expr.Type).GetMethod(StandardIds.getHasValue);
      MethodCall mc = new MethodCall(new MemberBinding(expr, getHasValue), null, NodeType.Call, SystemTypes.Boolean, expr.SourceContext);
      return new UnaryExpression(mc, NodeType.LogicalNot, SystemTypes.Boolean, expr.SourceContext);
    }
    public virtual Expression CallGetValueOrDefaultEqualsAndHasValue(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2) {
      if (opnd1 == null || opnd1.Type == null || opnd2 == null || opnd2.Type == null || binaryExpression == null || binaryExpression.Type != SystemTypes.Boolean) {
        Debug.Assert(false); return null;
      }
      StatementList statements = new StatementList(8);
      TypeNode t1 = opnd1.Type;
      TypeNode t2 = opnd2.Type;
      Expression e1 = opnd1;
      Expression e2 = opnd2;
      Local result = new Local(SystemTypes.Boolean);
      if (this.typeSystem.IsNullableType(t1)) {
        e1 = new Local(t1);
        statements.Add(new AssignmentStatement(e1, opnd1));
        Method getValueOrDefault = this.GetTypeView(t1).GetMethod(StandardIds.GetValueOrDefault);
        if (getValueOrDefault == null) { Debug.Assert(false); return null; }
        binaryExpression.Operand1 = new MethodCall(new MemberBinding(e1, getValueOrDefault, opnd1.SourceContext), new ExpressionList(0), NodeType.Call);
      }
      if (this.typeSystem.IsNullableType(t2)) {
        e2 = new Local(t2);
        statements.Add(new AssignmentStatement(e2, opnd2));
        Method getValueOrDefault = this.GetTypeView(t2).GetMethod(StandardIds.GetValueOrDefault);
        if (getValueOrDefault == null) { Debug.Assert(false); return null; }
        binaryExpression.Operand2 = new MethodCall(new MemberBinding(e2, getValueOrDefault, opnd2.SourceContext), new ExpressionList(0), NodeType.Call);
      }
      Block trueBlock = new Block(new StatementList());
      Block falseBlock = new Block(new StatementList());
      Block endBlock = new Block();
      if (binaryExpression.NodeType == NodeType.Eq) {
        Block verifyEqual = new Block(new StatementList());
        statements.Add(new Branch(binaryExpression, verifyEqual));
        statements.Add(new Branch(null, falseBlock));
        statements.Add(verifyEqual);
        if (this.typeSystem.IsNullableType(t1)) {
          Method getHasValue = this.GetTypeView(t1).GetMethod(StandardIds.getHasValue);
          MethodCall hasValue = new MethodCall(new MemberBinding(e1, getHasValue), new ExpressionList(0), NodeType.Call, SystemTypes.Boolean);
          verifyEqual.Statements.Add(new AssignmentStatement(result, hasValue));
        }
        if (this.typeSystem.IsNullableType(t2)) {
          if (this.typeSystem.IsNullableType(t1)) {
            Block label = new Block();
            verifyEqual.Statements.Add(new Branch(result, label));
            verifyEqual.Statements.Add(new Branch(null, falseBlock));
            verifyEqual.Statements.Add(label);
          }
          Method getHasValue = this.GetTypeView(t2).GetMethod(StandardIds.getHasValue);
          MethodCall hasValue = new MethodCall(new MemberBinding(e2, getHasValue), new ExpressionList(0), NodeType.Call, SystemTypes.Boolean);
          verifyEqual.Statements.Add(new AssignmentStatement(result, hasValue));
        }
      }
      statements.Add(trueBlock);
      trueBlock.Statements.Add(new Branch(null, endBlock));
      statements.Add(falseBlock);
      falseBlock.Statements.Add(new AssignmentStatement(result, Literal.False));
      statements.Add(endBlock);
      statements.Add(new ExpressionStatement(result));
      return new BlockExpression(new Block(statements), SystemTypes.Boolean);
    }
    public virtual Expression CoerceEqualityOperands(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      if (lit1 != null && lit2 == null) {
        TypeNode t1 = this.typeSystem.Unwrap(opnd1Type);
        TypeNode t2 = this.typeSystem.Unwrap(opnd2Type);
        if (this.typeSystem.IsNullableType(t2)) {
          if (Literal.IsNullLiteral(lit1))
            return this.CallHasValue(opnd2);
          else if (this.typeSystem.ImplicitLiteralCoercionFromTo(lit1, t1, this.typeSystem.RemoveNullableWrapper(t2)))
            return this.CallGetValueOrDefaultEqualsAndHasValue(binaryExpression, lit1, opnd2);
        }
        if (!this.typeSystem.ImplicitLiteralCoercionFromTo(lit1, t1, t2)) {
          if (Literal.IsNullLiteral(lit1)) {
            if (!t2.IsTemplateParameter || t2.IsValueType) {
              this.ReportBadOperands(binaryExpression, Error.BadBinaryOps, lit1, lit2, opnd1Type, opnd2Type);
            }
            return null;
          }
        }
      } else if (lit1 == null && lit2 != null) {
        TypeNode t1 = this.typeSystem.Unwrap(opnd1Type);
        TypeNode t2 = this.typeSystem.Unwrap(opnd2Type);
        if (this.typeSystem.IsNullableType(t1)) {
          if (Literal.IsNullLiteral(lit2))
            return this.CallHasValue(opnd1);
          else if (this.typeSystem.ImplicitLiteralCoercionFromTo(lit2, t2, this.typeSystem.RemoveNullableWrapper(t1)))
            return this.CallGetValueOrDefaultEqualsAndHasValue(binaryExpression, opnd1, lit2);
        }
        if (!this.typeSystem.ImplicitLiteralCoercionFromTo(lit2, t2, t1)) {
          if (Literal.IsNullLiteral(lit2)) {
            if (!t1.IsTemplateParameter || t1.IsValueType) {
              this.ReportBadOperands(binaryExpression, Error.BadBinaryOps, lit1, lit2, opnd1Type, opnd2Type);
              return null;
            }
          }
        }
      }
      return this.CoerceComparisonOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceComparisonOperands(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceBinaryOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceBitwiseBinaryOperands(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      return this.CoerceBinaryOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type);
    }
    public virtual Expression CoerceBinaryOperands(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      if (this.TypeInVariableContext(lit1)) return null;
      if (this.TypeInVariableContext(lit2)) return null;
      TypeNode unifiedType = binaryExpression.Type;
      bool equalityTest = binaryExpression.NodeType == NodeType.Eq || binaryExpression.NodeType == NodeType.Ne;
      bool bitwiseOperator = binaryExpression.NodeType == NodeType.And || binaryExpression.NodeType == NodeType.Or || binaryExpression.NodeType == NodeType.Xor;
      if (!equalityTest) {
        opnd1Type = this.typeSystem.RemoveNullableWrapper(opnd1Type);
        opnd2Type = this.typeSystem.RemoveNullableWrapper(opnd2Type);
        unifiedType = this.typeSystem.RemoveNullableWrapper(unifiedType);
      }
      Error error = Error.BadBinaryOps;
      TypeNode uselessComparisonType = null;
      if (opnd1Type == opnd2Type)
        unifiedType = this.typeSystem.Unwrap(opnd1Type);
      else {
        if (lit1 != null && lit2 == null) {
          if (!bitwiseOperator) unifiedType = this.typeSystem.Unwrap(opnd2Type);
          TypeNode t1 = this.typeSystem.Unwrap(opnd1Type);
          if (unifiedType == SystemTypes.Char)
            opnd1 = this.typeSystem.ExplicitCoercion(lit1, SystemTypes.Char, this.TypeViewer);
          if (!this.typeSystem.ImplicitLiteralCoercionFromTo(lit1, t1, unifiedType)) {
            if (unifiedType.IsPrimitiveInteger && opnd1Type.IsPrimitiveInteger) {
              TypeNode st1 = this.typeSystem.SmallestIntegerType(lit1.Value);
              Literal lit1Copy = this.typeSystem.ExplicitLiteralCoercion(lit1, t1, st1, this.TypeViewer) as Literal;
              if (lit1Copy == null || !this.typeSystem.ImplicitLiteralCoercionFromTo(lit1Copy, st1, unifiedType))
                uselessComparisonType = unifiedType;
              unifiedType = this.typeSystem.UnifiedPrimitiveType(unifiedType, opnd1Type);
            } else if (this.typeSystem.ImplicitCoercionFromTo(unifiedType, t1, this.TypeViewer))
              unifiedType = t1;
          }
          if (unifiedType.IsPrimitiveInteger && (opnd1Type is EnumNode || opnd2Type is EnumNode)) {
            this.ReportBadOperands(binaryExpression, error, lit1, lit2, opnd1Type, opnd2Type);
            return null;
          }
          if (unifiedType is TypeParameter && this.useGenerics)
            opnd2 = new BinaryExpression(opnd2, new MemberBinding(null, unifiedType), NodeType.Box, unifiedType, opnd2.SourceContext);
        } else if (lit1 == null && lit2 != null) {
          if (!bitwiseOperator) unifiedType = this.typeSystem.Unwrap(opnd1Type);
          TypeNode t2 = this.typeSystem.Unwrap(opnd2Type);
          if (unifiedType == SystemTypes.Char)
            opnd2 = this.typeSystem.ExplicitCoercion(lit2, SystemTypes.Char, this.TypeViewer);
          if (!this.typeSystem.ImplicitLiteralCoercionFromTo(lit2, t2, unifiedType)) {
            if (unifiedType.IsPrimitiveInteger && opnd2Type.IsPrimitiveInteger) {
              TypeNode st2 = this.typeSystem.SmallestIntegerType(lit2.Value);
              Literal lit2Copy = this.typeSystem.ExplicitLiteralCoercion(lit2, t2, st2, this.TypeViewer) as Literal;
              if (lit2Copy == null || !this.typeSystem.ImplicitLiteralCoercionFromTo(lit2Copy, st2, unifiedType))
                uselessComparisonType = unifiedType;
              unifiedType = this.typeSystem.UnifiedPrimitiveType(unifiedType, opnd2Type);
            } else if (this.typeSystem.ImplicitCoercionFromTo(unifiedType, t2, this.TypeViewer))
              unifiedType = t2;
          }
          if (unifiedType.IsPrimitiveInteger && (opnd1Type is EnumNode || opnd2Type is EnumNode)) {
            this.ReportBadOperands(binaryExpression, error, lit1, lit2, opnd1Type, opnd2Type);
            return null;
          }
          if (unifiedType is TypeParameter && this.useGenerics)
            opnd1 = new BinaryExpression(opnd1, new MemberBinding(null, unifiedType), NodeType.Box, unifiedType, opnd1.SourceContext);
        } else
          unifiedType = this.typeSystem.UnifiedPrimitiveType(opnd1Type, opnd2Type);
      }
      if (equalityTest) {
        if (unifiedType == null || !unifiedType.IsPrimitiveComparable) {
          if (this.typeSystem.IsNullableType(unifiedType)) {
            unifiedType = SystemTypes.Object;
            opnd1 = this.typeSystem.ExplicitCoercion(opnd1, SystemTypes.Object);
            opnd2 = this.typeSystem.ExplicitCoercion(opnd2, SystemTypes.Object);
            return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, SystemTypes.Object);
          }
          this.ReportBadOperands(binaryExpression, error, lit1, lit2, opnd1Type, opnd2Type);
          return null;
        }
        if (unifiedType.IsPrimitiveInteger && (opnd1Type is EnumNode || opnd2Type is EnumNode)) {
          this.ReportBadOperands(binaryExpression, error, lit1, lit2, opnd1Type, opnd2Type);
          return null;
        }
        if (unifiedType.IsTemplateParameter && this.useGenerics && unifiedType.IsReferenceType) {
          return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, SystemTypes.Object);
        }
        Expression e = this.GenerateSpecialNullTestIfApplicable(binaryExpression, opnd1, opnd2, lit1, lit2, unifiedType);
        if (e != null) return e;
      }
      if (unifiedType == SystemTypes.Object) {
        TypeNode t1 = opnd1Type == null ? null : this.typeSystem.Unwrap(opnd1Type);
        TypeNode t2 = opnd2Type == null ? null : this.typeSystem.Unwrap(opnd2Type);
        if (equalityTest) {
          if (this.typeSystem.HasValueEquality(t1, this.TypeViewer) && t2 == SystemTypes.Object) {
            if (t1.Template != SystemTypes.GenericBoxed && !(lit2 != null && lit2.Value == null))
              this.HandleError(binaryExpression, Error.BadRefCompareRight, this.GetTypeName(t1));
            return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, unifiedType);
          }
          if (t1 == SystemTypes.Object && this.typeSystem.HasValueEquality(t2, this.TypeViewer)) {
            if (opnd2Type.Template != SystemTypes.GenericBoxed && !(lit1 != null && lit1.Value == null))
              this.HandleError(binaryExpression, Error.BadRefCompareLeft, this.GetTypeName(t2));
            return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, unifiedType);
          }
          if (t1 != null && !t1.IsValueType && t2 != null && !t2.IsValueType) {
            if (this.GetTypeView(t1).IsAssignableTo(t2) || this.GetTypeView(t2).IsAssignableTo(t1) ||
            (t1 is Interface || t2 is Interface))
              return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, unifiedType);
          }
          if (t1 is Pointer && t2 != null && lit2.Type == SystemTypes.Object && lit2.Value == null)
            return binaryExpression;
          if (t2 is Pointer && lit1 != null && lit1.Type == SystemTypes.Object && lit1.Value == null)
            return binaryExpression;
        }
        if (this.typeSystem.IsNullableType(binaryExpression.Type)) {
          //TODO: this C# specific behavior is too controversial for the general framework, factor it out
          if (Literal.IsNullLiteral(opnd1)) {
            if (lit2 == null) this.HandleError(binaryExpression, Error.AlwaysNull, this.GetTypeName(binaryExpression.Type));
            return this.typeSystem.ExplicitCoercion(opnd1, binaryExpression.Type, this.TypeViewer);
            //TODO: arrange a way for variables in opnd2 to be regarded as being referenced
          } else if (Literal.IsNullLiteral(opnd2)) {
            if (lit1 == null) this.HandleError(binaryExpression, Error.AlwaysNull, this.GetTypeName(binaryExpression.Type));
            return this.typeSystem.ExplicitCoercion(opnd2, binaryExpression.Type, this.TypeViewer);
          }
        }
        if (binaryExpression.Type == SystemTypes.Boolean) {
          if (opnd1Type is EnumNode && lit2 != null && this.typeSystem.ImplicitLiteralCoercionFromTo(lit2, opnd2Type, opnd1Type))
            unifiedType = opnd1Type;
          else if (opnd2Type is EnumNode && lit1 != null && this.typeSystem.ImplicitLiteralCoercionFromTo(lit1, opnd1Type, opnd2Type))
            unifiedType = opnd2Type;
        }
        // If one of the types is a type parameter with a reference constraint, then the comparison is allowed
        // We already checked that the reference constraint holds in unified types.
        if (t1 != null && t1.IsTemplateParameter && t1.IsReferenceType) {
          return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, unifiedType);
        }
        if (t2 != null && t2.IsTemplateParameter && t2.IsReferenceType) {
          return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, unifiedType);
        }
        if (unifiedType == SystemTypes.Object) {
          this.ReportBadOperands(binaryExpression, error, lit1, lit2, opnd1Type, opnd2Type);
          return null;
        }
      }
      switch (binaryExpression.NodeType) {
        case NodeType.And:
        case NodeType.Xor:
          if (!unifiedType.IsPrimitiveInteger && unifiedType != SystemTypes.Char && unifiedType != SystemTypes.Boolean && !(unifiedType is EnumNode)) {
            this.ReportBadOperands(binaryExpression, error, lit1, lit2, opnd1Type, opnd2Type);
            return null;
          }
          if (unifiedType.IsPrimitiveInteger) {
            Literal lit = opnd1 as Literal;
            if (lit != null) {
              if (this.IsZeroValue(lit.Value)) {
                if (binaryExpression.NodeType == NodeType.And)
                  return this.typeSystem.ImplicitLiteralCoercion(new Literal(0, SystemTypes.Int32, binaryExpression.SourceContext), SystemTypes.Int32, unifiedType, this.TypeViewer);
                else {
                  Debug.Assert(binaryExpression.NodeType == NodeType.Or || binaryExpression.NodeType == NodeType.Xor);
                  return this.typeSystem.ExplicitCoercion(opnd2, binaryExpression.Type, this.TypeViewer);
                }
              } else if (!this.typeSystem.ImplicitLiteralCoercionFromTo(lit, opnd1Type, unifiedType))
                binaryExpression.Type = this.typeSystem.UnifiedPrimitiveType(this.InferTypeOfIntegerLiteral(lit), opnd2Type);
            }
            lit = opnd2 as Literal;
            if (lit != null) {
              if (this.IsZeroValue(lit.Value)) {
                if (binaryExpression.NodeType == NodeType.And)
                  return this.typeSystem.ImplicitLiteralCoercion(new Literal(0, SystemTypes.Int32, binaryExpression.SourceContext), SystemTypes.Int32, unifiedType, this.TypeViewer);
                else {
                  Debug.Assert(binaryExpression.NodeType == NodeType.Or || binaryExpression.NodeType == NodeType.Xor);
                  return this.typeSystem.ExplicitCoercion(opnd1, binaryExpression.Type, this.TypeViewer);
                }
              } else if (!this.typeSystem.ImplicitLiteralCoercionFromTo(lit, opnd2Type, unifiedType))
                binaryExpression.Type = this.typeSystem.UnifiedPrimitiveType(this.InferTypeOfIntegerLiteral(lit), opnd1Type);
            }
          }
          unifiedType = binaryExpression.Type;
          return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, unifiedType);
        case NodeType.Or:
          if (this.currentType is EnumNode) goto case NodeType.And;
          if (this.typeSystem.CoercionExtendsSign(opnd1Type, unifiedType) && TypeSystem.MostSignificantBitIsOneAndItExtends(lit1) ||
            this.typeSystem.CoercionExtendsSign(opnd2Type, unifiedType) && TypeSystem.MostSignificantBitIsOneAndItExtends(lit2)) {
            this.HandleError(binaryExpression, Error.BitwiseOrSignExtend);
          } else
            goto case NodeType.And;
          return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, unifiedType);

        case NodeType.Cgt:
        case NodeType.Cgt_Un:
        case NodeType.Clt:
        case NodeType.Clt_Un:
        case NodeType.Ge:
        case NodeType.Gt:
        case NodeType.Le:
        case NodeType.Lt:
          if (!unifiedType.IsPrimitiveNumeric && unifiedType != SystemTypes.Char && unifiedType != SystemTypes.Decimal &&
          !(unifiedType is EnumNode) && !(unifiedType is Pointer)) {
            this.ReportBadOperands(binaryExpression, error, lit1, lit2, opnd1Type, opnd2Type);
            return null;
          }
          if ((opnd1Type == SystemTypes.UInt64 && unifiedType == SystemTypes.Int64) ||
            (opnd2Type == SystemTypes.UInt64 && unifiedType == SystemTypes.Int64)) {
            error = Error.AmbiguousBinaryOperation;
            this.ReportBadOperands(binaryExpression, error, lit1, lit2, opnd1Type, opnd2Type);
            return null;
          }
          goto case NodeType.Eq;
        case NodeType.Eq:
        case NodeType.Ne:
          if (
            ((unifiedType is TypeParameter && !((TypeParameter)unifiedType).IsReferenceType) || (unifiedType is ClassParameter && unifiedType.IsValueType)) &&
            !Literal.IsNullLiteral(lit1) && !Literal.IsNullLiteral(lit2)) {
            this.ReportBadOperands(binaryExpression, error, lit1, lit2, opnd1Type, opnd2Type);
            return null;
          }
          if (uselessComparisonType != null)
            this.HandleError(binaryExpression, Error.UselessComparisonWithIntegerLiteral, this.GetTypeName(uselessComparisonType));
          return CoercedBinaryExpression(binaryExpression, opnd1, opnd2, unifiedType);
      }
      return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, unifiedType);
    }

    public virtual Expression GenerateSpecialNullTestIfApplicable(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode unifiedType) {
      //TODO: move this to Comega specific code
      //if (unifiedType.Template == SystemTypes.GenericIEnumerable) {
      //  if ((lit1 != null && lit1.Value == null && lit1.Type == SystemTypes.Object) ||
      //      (lit2 != null && lit2.Value == null && lit2.Type == SystemTypes.Object)) {
      //    TypeNode ety = this.typeSystem.GetStreamElementType(unifiedType);
      //    TypeNode su = SystemTypes.GenericStreamUtility.GetTemplateInstance(this.currentType, ety);
      //    Method isNull = this.GetTypeView(su).GetMethod(StandardIds.IsNull, unifiedType);
      //    Expression xIsNull = new MethodCall(new MemberBinding(null, isNull), new ExpressionList(lit1 == null ? opnd1 : opnd2), NodeType.Call, SystemTypes.Boolean);
      //    if (binaryExpression.NodeType == NodeType.Ne) {
      //      xIsNull = new UnaryExpression(xIsNull, NodeType.LogicalNot, binaryExpression.SourceContext);
      //      xIsNull.Type = SystemTypes.Boolean;
      //    }
      //    return xIsNull;
      //  }
      //}
      return null;
    }

    public virtual bool IsZeroValue(object value) {
      IConvertible ic = value as IConvertible;
      if (ic == null) return false;
      switch (ic.GetTypeCode()) {
        case TypeCode.Boolean: return false;
        case TypeCode.Byte: return ic.ToByte(null) == 0;
        case TypeCode.Char: return ic.ToChar(null) == (char)0;
        case TypeCode.DateTime: return false;
        case TypeCode.DBNull: return false;
        case TypeCode.Decimal: return ic.ToDecimal(null) == 0m;
        case TypeCode.Double: return ic.ToDouble(null) == 0d;
        case TypeCode.Empty: return false;
        case TypeCode.Int16: return ic.ToInt16(null) == 0;
        case TypeCode.Int32: return ic.ToInt32(null) == 0;
        case TypeCode.Int64: return ic.ToInt64(null) == 0;
        case TypeCode.Object: return false;
        case TypeCode.SByte: return ic.ToSByte(null) == 0;
        case TypeCode.Single: return ic.ToSingle(null) == 0f;
        case TypeCode.String: return false;
        case TypeCode.UInt16: return ic.ToUInt16(null) == 0;
        case TypeCode.UInt32: return ic.ToUInt32(null) == 0;
        case TypeCode.UInt64: return ic.ToUInt64(null) == 0;
      }
      return false;
    }
    public virtual Expression CoerceOperandForBox(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      TypeNode resultType = binaryExpression.Type;
      if (this.TypeInVariableContext(opnd1 as Literal)) return null;
      if (resultType != null) {
        resultType = this.VisitTypeReference(resultType);
        if (resultType == null) return null;
      }
      return binaryExpression; //TODO: appropriate type checking
    }
    public virtual Expression CoerceOperandsForIsinst(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      TypeNode resultType = binaryExpression.Type;
      if (this.TypeInVariableContext(opnd1 as Literal)) return null;
      if (resultType != null) {
        resultType = this.VisitTypeReference(resultType);
        if (resultType == null) return null;
      }
      return binaryExpression; //TODO: appropriate type checking
    }
    public virtual Expression CoerceOperandForUnbox(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      TypeNode resultType = binaryExpression.Type;
      if (this.TypeInVariableContext(opnd1 as Literal)) return null;
      if (resultType != null) {
        resultType = this.VisitTypeReference(resultType);
        if (resultType == null) return null;
      }
      return binaryExpression; //TODO: appropriate type checking
    }
    public virtual Expression CoerceOperandsForExplicitCoercion(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      TypeNode unifiedType = binaryExpression.Type;
      if (this.TypeInVariableContext(lit1)) return null;
      unifiedType = this.VisitTypeReference(unifiedType);
      if (unifiedType == null) return null;
      if (lit1 != null) {
        opnd1 = this.typeSystem.ExplicitLiteralCoercion(lit1, lit1.Type, unifiedType, this.TypeViewer);
        if (opnd1 != null) opnd1.SourceContext = binaryExpression.SourceContext;
        if (opnd1 is Literal && opnd1.Type != null && this.GetTypeView(opnd1.Type).IsAssignableTo(unifiedType)) return opnd1;
      } else {
        opnd1.SourceContext = binaryExpression.SourceContext;
        if (opnd1.Type == unifiedType && unifiedType != null && this.typeSystem.IsNonNullType(unifiedType))
          this.HandleError(opnd2, Error.ExpressionIsAlreadyOfThisType, this.ErrorHandler.GetTypeName(unifiedType));
        opnd1 = this.typeSystem.ExplicitCoercion(opnd1, unifiedType, this.TypeViewer);
      }
      if (opnd1 == null) return null;
      //Keep the binary expression in place so that its static type can reflect the result of the coercion for use in later checking.
      //The result type can be a supertype of opnd1.Type and changing opnd1.Type can mess up the code generator.
      binaryExpression.Operand1 = opnd1;
      binaryExpression.NodeType = NodeType.ExplicitCoercion; //Tell this code generator to discard this "wrapper" expression.
      binaryExpression.Type = unifiedType;
      return binaryExpression;
    }
    public virtual Expression CoerceOperandsForAs(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      TypeNode targetType = binaryExpression.Type;
      if (this.TypeInVariableContext(opnd1 as Literal)) return null;
      targetType = this.VisitTypeReference(targetType);
      if (targetType == null || opnd1Type == null) return null;
      Reference refType = opnd1Type as Reference;
      if (refType != null) {
        opnd1 = binaryExpression.Operand1 = this.typeSystem.ExplicitCoercion(opnd1, refType.ElementType, this.TypeViewer);
        opnd1Type = TypeNode.StripModifier(refType.ElementType, SystemTypes.NonNullType);
      }
      TypeUnion tUnion = opnd1Type as TypeUnion;
      if (tUnion != null && tUnion.Types != null) {
        int i = tUnion.Types.SearchFor(targetType);
        if (i < 0) {
          this.HandleError(opnd1, Error.IsNeverOfType, this.GetTypeName(targetType));
          return null;
        }
        opnd1 = new UnaryExpression(opnd1, NodeType.AddressOf, opnd1.Type.GetReferenceType());
        Expression getValue = new MethodCall(new MemberBinding(opnd1, tUnion.GetMembersNamed(StandardIds.GetValue)[0]), null);
        getValue.Type = SystemTypes.Object;
        binaryExpression.Operand1 = getValue;
        binaryExpression.NodeType = NodeType.Isinst;
        return binaryExpression;
      }
      if (opnd1Type is Pointer) {
        this.HandleError(binaryExpression, Error.PointerInAsOrIs);
        return null;
      }
      if (targetType.IsValueType) {
        this.HandleError(binaryExpression, Error.AsMustHaveReferenceType, this.GetTypeName(targetType));
        return null;
      }
      if (targetType is ITypeParameter && !targetType.IsReferenceType) {
        this.HandleError(binaryExpression, Error.AsWithTypeVar, this.GetTypeName(targetType));
        return null;
      }
      if (this.GetTypeView(opnd1Type).IsAssignableTo(targetType)) {
        if (!opnd1Type.IsValueType || targetType.IsValueType) {
          //TODO: C# is silent here, but a warning seems in order anyway
          return opnd1;
        }
      } else if (!this.GetTypeView(targetType).IsAssignableTo(opnd1Type) && !(opnd1Type is ITypeParameter) &&
                 (!(targetType is Interface) && !(opnd1Type is Interface) ||
                   opnd1Type.IsSealed || targetType.IsSealed)) {
        if (opnd1Type.Name != Looker.NotFound)
          this.HandleError(binaryExpression, Error.ImpossibleCast, this.GetTypeName(opnd1Type), this.GetTypeName(targetType));
        return null;
      }
      if (opnd1Type.IsValueType || (opnd1Type is ITypeParameter /*&& !opnd1Type.IsReferenceType*/))
        binaryExpression.Operand1 = new BinaryExpression(opnd1, new MemberBinding(null, opnd1Type), NodeType.Box, SystemTypes.Object);
      binaryExpression.NodeType = NodeType.Isinst;
      if (targetType is ITypeParameter)
        binaryExpression = new BinaryExpression(binaryExpression, new MemberBinding(null, targetType), NodeType.UnboxAny, targetType, binaryExpression.SourceContext);
      return binaryExpression;
    }
    public virtual Expression CoerceOperandsForRem(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      TypeNode resultType = binaryExpression.Type;
      if (resultType.IsUnsignedPrimitiveNumeric)
        binaryExpression.NodeType = NodeType.Rem_Un;
      if (resultType.IsPrimitiveInteger) {
        Literal lit = opnd2 as Literal;
        if (lit != null) {
          if (this.IsZeroValue(lit.Value)) {
            this.HandleError(binaryExpression, Error.IntegerDivisionByConstantZero);
            return null;
          }
        }
        lit = opnd1 as Literal;
        if (lit != null && this.IsZeroValue(lit.Value))
          return opnd1;
      }
      binaryExpression = this.CheckNumericBinaryOperatorOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type, resultType);
      if (binaryExpression == null) return null;
      return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, resultType);
    }
    public virtual Expression CoerceOperandsForDiv(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      TypeNode resultType = binaryExpression.Type;
      if (resultType.IsUnsignedPrimitiveNumeric)
        binaryExpression.NodeType = NodeType.Div_Un;
      if (resultType.IsPrimitiveInteger) {
        Literal lit = opnd2 as Literal;
        if (lit != null) {
          if (this.IsZeroValue(lit.Value)) {
            this.HandleError(binaryExpression, Error.IntegerDivisionByConstantZero);
            return null;
          }
        }
        lit = opnd1 as Literal;
        if (lit != null && this.IsZeroValue(lit.Value))
          return opnd1;
      }
      binaryExpression = this.CheckNumericBinaryOperatorOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type, resultType);
      if (binaryExpression == null) return null;
      return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, resultType);
    }
    public virtual Expression CoerceOperandsForSub(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      TypeNode resultType = binaryExpression.Type;
      if (resultType is DelegateNode) return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, resultType);
      EnumNode opnd1eType = opnd1Type as EnumNode;
      EnumNode opnd2eType = opnd2Type as EnumNode;
      if (opnd1eType != null && opnd2Type != opnd1eType &&
      (opnd2Type == opnd1eType.UnderlyingType || this.typeSystem.ImplicitLiteralCoercionFromTo(opnd2 as Literal, opnd2Type, opnd1eType.UnderlyingType)))
        opnd1 = this.typeSystem.ExplicitCoercion(opnd1, resultType = opnd2Type, this.TypeViewer);
      if (opnd1eType != null && opnd1eType == opnd2eType) {
        opnd1 = this.typeSystem.ExplicitCoercion(opnd1, resultType, this.TypeViewer);
        opnd2 = this.typeSystem.ExplicitCoercion(opnd2, resultType, this.TypeViewer);
      }
      if (this.typeSystem.checkOverflow) {
        if (resultType.IsUnsignedPrimitiveNumeric)
          binaryExpression.NodeType = NodeType.Sub_Ovf_Un;
        else if (resultType.IsPrimitiveInteger)
          binaryExpression.NodeType = NodeType.Sub_Ovf;
      }
      if (resultType == SystemTypes.IntPtr && !(opnd1Type is Pointer) && (opnd2Type is Pointer)) {
        this.ReportBadOperands(binaryExpression, Error.BadBinaryOps, lit1, lit2, opnd1Type, opnd2Type);
        return null;
      }
      binaryExpression = this.CheckNumericBinaryOperatorOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type, resultType);
      if (binaryExpression == null) return null;
      return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, resultType);
    }
    public virtual Expression CoerceOperandsForMul(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      TypeNode resultType = binaryExpression.Type;
      if (this.typeSystem.checkOverflow) {
        if (resultType.IsUnsignedPrimitiveNumeric)
          binaryExpression.NodeType = NodeType.Mul_Ovf_Un;
        else if (resultType.IsPrimitiveInteger)
          binaryExpression.NodeType = NodeType.Mul_Ovf;
      }
      if (resultType.IsPrimitiveInteger) {
        if (lit1 != null && lit2 != null) {
          if (this.IsZeroValue(lit1.Value))
            return this.typeSystem.ImplicitLiteralCoercion(new Literal(0, SystemTypes.Int32, binaryExpression.SourceContext), SystemTypes.Int32, resultType, this.TypeViewer);
          if (this.IsZeroValue(lit2.Value))
            return this.typeSystem.ImplicitLiteralCoercion(new Literal(0, SystemTypes.Int32, binaryExpression.SourceContext), SystemTypes.Int32, resultType, this.TypeViewer);
        }
      }
      binaryExpression = this.CheckNumericBinaryOperatorOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type, resultType);
      if (binaryExpression == null) return null;
      return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, resultType);
    }
    public virtual Expression CoerceOperandsForAdd(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      TypeNode resultType = binaryExpression.Type;
      if (resultType is DelegateNode) return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, resultType);
      if (this.typeSystem.checkOverflow) {
        if (resultType.IsUnsignedPrimitiveNumeric)
          binaryExpression.NodeType = NodeType.Add_Ovf_Un;
        else if (resultType.IsPrimitiveInteger)
          binaryExpression.NodeType = NodeType.Add_Ovf;
      }
      EnumNode opnd1eType = opnd1Type as EnumNode;
      EnumNode opnd2eType = opnd2Type as EnumNode;
      if (opnd1eType != null && this.typeSystem.StandardImplicitCoercionFromTo(opnd2, opnd2Type, opnd1eType.UnderlyingType, this.TypeViewer)) {
        opnd2 = this.typeSystem.ExplicitCoercion(opnd2, resultType = opnd2Type = opnd1Type, this.TypeViewer);
        return binaryExpression;
      }
      if (opnd2eType != null && this.typeSystem.StandardImplicitCoercionFromTo(opnd1, opnd1Type, opnd2eType.UnderlyingType, this.TypeViewer)) {
        opnd1 = this.typeSystem.ExplicitCoercion(opnd1, resultType = opnd1Type = opnd2Type, this.TypeViewer);
        return binaryExpression;
      }
      binaryExpression = this.CheckNumericBinaryOperatorOperands(binaryExpression, opnd1, opnd2, lit1, lit2, opnd1Type, opnd2Type, resultType);
      if (binaryExpression == null) return null;
      return this.CoercedBinaryExpression(binaryExpression, opnd1, opnd2, resultType);
    }
    public virtual Expression CoercedBinaryExpression(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, TypeNode unifiedType) {
      if (unifiedType is Pointer) {
        // just do auto-deref coercions
        binaryExpression.Operand1 = this.typeSystem.AutoDereferenceCoercion(binaryExpression.Operand1);
        binaryExpression.Operand2 = this.typeSystem.AutoDereferenceCoercion(binaryExpression.Operand2);
      }
      else {
        binaryExpression.Operand1 = this.typeSystem.ImplicitCoercion(opnd1, unifiedType, this.TypeViewer);
        binaryExpression.Operand2 = this.typeSystem.ImplicitCoercion(opnd2, unifiedType, this.TypeViewer);
      }
      return binaryExpression;
    }
    public virtual BinaryExpression CheckNumericBinaryOperatorOperands(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type, TypeNode resultType) {
      if (binaryExpression == null) { Debug.Assert(false); return null; }
      if (opnd1 == null || opnd2 == null || opnd1Type == null || opnd2Type == null || resultType == null) return binaryExpression;
      opnd1Type = this.typeSystem.RemoveNullableWrapper(opnd1Type);
      opnd2Type = this.typeSystem.RemoveNullableWrapper(opnd2Type);
      resultType = this.typeSystem.RemoveNullableWrapper(resultType);
      Error error = Error.BadBinaryOps;
      if (!resultType.IsPrimitiveNumeric && 
        !(resultType is EnumNode && !(opnd1Type is EnumNode && opnd2Type is EnumNode)) &&
        !(resultType == SystemTypes.Char && opnd1Type == SystemTypes.Char && opnd2Type == SystemTypes.Char)) {
        if (opnd1 is Literal && opnd1Type == SystemTypes.Type) {
          this.HandleError(opnd1, Error.WrongKindOfMember, binaryExpression.Operand1.SourceContext.SourceText, "class", "variable");
          if (binaryExpression.NodeType == NodeType.Sub || binaryExpression.NodeType == NodeType.Sub_Ovf)
            this.HandleError(binaryExpression, Error.PossibleBadNegCast);
          return null;
        }
        if (opnd2 is Literal && opnd2Type == SystemTypes.Type) {
          this.HandleError(opnd2, Error.WrongKindOfMember, binaryExpression.Operand2.SourceContext.SourceText, "class", "variable");
          return null;
        }
        if (resultType is Pointer) {
          if (binaryExpression.NodeType == NodeType.Add && !(this.typeSystem.Unwrap(opnd1Type) is Pointer && this.typeSystem.Unwrap(opnd2Type) is Pointer))
            return binaryExpression;
          else if (binaryExpression.NodeType == NodeType.Sub && this.typeSystem.Unwrap(opnd1Type) is Pointer)
            return binaryExpression;
        }
        this.ReportBadOperands(binaryExpression, error, lit1, lit2, opnd1Type, opnd2Type);
        return null;
      }
      if ((opnd1Type == SystemTypes.UInt64 && resultType == SystemTypes.Int64) ||
        (opnd2Type == SystemTypes.UInt64 && resultType == SystemTypes.Int64)) {
        error = Error.AmbiguousBinaryOperation;
        this.ReportBadOperands(binaryExpression, error, lit1, lit2, opnd1Type, opnd2Type);
        return null;
      }
      return binaryExpression;
    }
    public virtual void ReportBadOperands(BinaryExpression binaryExpression, Error error, Literal lit1, Literal lit2, TypeNode opnd1Type, TypeNode opnd2Type) {
      if (opnd1Type == null || opnd1Type.Name == Looker.NotFound) return;
      if (opnd2Type == null || opnd2Type.Name == Looker.NotFound) return;
      string opnd1TypeName = this.GetTypeName(opnd1Type);
      string opnd2TypeName = this.GetTypeName(opnd2Type);
      if (opnd1Type == SystemTypes.Object && lit1 != null && lit1.Value == null) opnd1TypeName = "<null>";
      if (opnd2Type == SystemTypes.Object && lit2 != null && lit2.Value == null) opnd2TypeName = "<null>";
      this.HandleError(binaryExpression, error,
        this.GetOperatorSymbol(binaryExpression.NodeType), opnd1TypeName, opnd2TypeName);
    }
    public virtual string GetOperatorSymbol(NodeType oper) {
      string sym = "";
      switch (oper) {
        case NodeType.Add_Ovf:
        case NodeType.Add_Ovf_Un:
        case NodeType.Add: sym = "+"; break;
        case NodeType.Mul_Ovf:
        case NodeType.Mul_Ovf_Un:
        case NodeType.Mul: sym = "*"; break;
        case NodeType.Sub_Ovf:
        case NodeType.Sub_Ovf_Un:
        case NodeType.Sub: sym = "-"; break;
        case NodeType.Div_Un:
        case NodeType.Div: sym = "/"; break;
        case NodeType.Comma: sym = ","; break;
        case NodeType.Rem_Un:
        case NodeType.Rem: sym = "%"; break;
        case NodeType.And: sym = "&"; break;
        case NodeType.Ceq: sym = "=="; break;
        case NodeType.Cgt:
        case NodeType.Cgt_Un: sym = ">"; break;
        case NodeType.Clt:
        case NodeType.Clt_Un: sym = "<"; break;
        case NodeType.Eq: sym = "=="; break;
        case NodeType.Implies: sym = "==>"; break;
        case NodeType.Iff: sym = "<==>"; break;
        case NodeType.Ge: sym = ">="; break;
        case NodeType.Gt: sym = ">"; break;
        case NodeType.Le: sym = "<="; break;
        case NodeType.LogicalAnd: sym = "&&"; break;
        case NodeType.LogicalOr: sym = "||"; break;
        case NodeType.Lt: sym = "<"; break;
        case NodeType.Maplet: sym = "~>"; break;
        case NodeType.NullCoalesingExpression: sym = "??"; break;
        case NodeType.Ne: sym = "!="; break;
        case NodeType.Or: sym = "|"; break;
        case NodeType.Shl: sym = "<<"; break;
        case NodeType.Shr:
        case NodeType.Shr_Un: sym = ">>"; break;
        case NodeType.Range: sym = ":"; break;
        case NodeType.Xor: sym = "^"; break;
      }
      return sym;
    }
    public virtual bool TypeInVariableContext(Literal lit) {
      if (lit == null) return false;
      TypeNode t = lit.Value as TypeNode;
      if (t == null) return false;
      this.HandleError(lit, Error.TypeInVariableContext, this.GetTypeName(t), "class", "variable");
      return true;
    }
    public override Block VisitBlock(Block block) {
      if (block == null) return null;
      bool savedCheckOverflow = this.typeSystem.checkOverflow;
      bool savedSuppressOverflowCheck = this.typeSystem.suppressOverflowCheck;
      bool savedInsideUnsafeCode = this.typeSystem.insideUnsafeCode;
      this.typeSystem.checkOverflow = block.Checked;
      this.typeSystem.suppressOverflowCheck = block.SuppressCheck;
      if (block.IsUnsafe) this.typeSystem.insideUnsafeCode = true;
      BlockScope bscope = block.Scope;
      if (bscope != null) {
        if (bscope.Members != null && bscope.Members.Count > 0)
          bscope.AssociatedBlock.HasLocals = true;
        this.CheckForDuplicateDeclarations(bscope);
      }
      block = base.VisitBlock(block);
      this.typeSystem.checkOverflow = savedCheckOverflow;
      this.typeSystem.suppressOverflowCheck = savedSuppressOverflowCheck;
      this.typeSystem.insideUnsafeCode = savedInsideUnsafeCode;
      return block;
    }
    public override Expression VisitBlockExpression(BlockExpression blockExpression) {
      if (blockExpression == null) return null;
      blockExpression.Block = this.VisitBlock(blockExpression.Block);
      return blockExpression;
      //TODO: give an error. This method should always be overridden
    }
    public virtual ExpressionList VisitBooleanExpressionList(ExpressionList expressions) {
      if (expressions == null) return null;
      for (int i = 0; i < expressions.Count; i++) {
        expressions[i] = this.VisitBooleanExpression(expressions[i]);
      }
      return expressions;
    }
    public virtual Expression VisitBooleanExpression(Expression expr) {
      UnaryExpression uexpr = expr as UnaryExpression;
      if (uexpr != null && uexpr.NodeType == NodeType.LogicalNot) {
        Expression e = uexpr.Operand = this.VisitBooleanExpression(uexpr.Operand);
        if (e == null) return null;
        MethodCall mcall = e as MethodCall;
        if (mcall != null) {
          MemberBinding mb = mcall.Callee as MemberBinding;
          Member m = null;
          if (mb == null || (m = mb.BoundMember) == null) return null;
          if (m == this.GetTypeView(m.DeclaringType).GetOpTrue()) {
            Method meth = this.GetTypeView(m.DeclaringType).GetOpFalse();
            if (meth != null) {
              mb.BoundMember = meth; return mcall;
            }
          } else if (m == this.GetTypeView(m.DeclaringType).GetOpFalse()) {
            Method meth = this.GetTypeView(m.DeclaringType).GetOpTrue();
            if (meth != null) {
              mb.BoundMember = meth; return mcall;
            }
          }
        }
      } else
        expr = this.VisitExpression(expr);
      TypeNode eType = null;
      if (expr == null || (eType = expr.Type) == null) return null;
      if (!this.typeSystem.ImplicitCoercionFromTo(eType, SystemTypes.Boolean, this.TypeViewer)) {
        Method opTrue = this.GetTypeView(eType).GetOpTrue();
        if (opTrue != null) {
          ExpressionList args = new ExpressionList(1);
          args.Add(this.typeSystem.ImplicitCoercion(expr, opTrue.DeclaringType, this.TypeViewer));
          return new MethodCall(new MemberBinding(null, opTrue), args, NodeType.Call, SystemTypes.Boolean, expr.SourceContext);
        }
      }
      return this.typeSystem.ImplicitCoercion(expr, SystemTypes.Boolean, this.TypeViewer);
    }
    public override Statement VisitBranch(Branch branch) {
      if (branch == null) return null;
      branch.Condition = this.VisitExpression(branch.Condition);
      if (branch.Target != null && this.currentFinallyClause != null && this.scopeFor != null && this.currentFinallyClause != (this.scopeFor[branch.Target.UniqueKey] as BlockScope)) {
        this.HandleError(branch, Error.BadFinallyLeave);
        return null;
      }
      return branch;
    }
    public override Statement VisitCatch(Catch Catch) {
      if (Catch == null) return null;
      bool savedInsideCatchClause = this.insideCatchClause;
      this.insideCatchClause = true;
      Catch.Variable = this.VisitTargetExpression(Catch.Variable);
      TypeNode t = Catch.Type = this.VisitTypeReference(Catch.Type);
      if (t != null)
        t = Catch.Type = TypeNode.StripModifiers(t);
      if (t != null && !this.GetTypeView(t).IsAssignableTo(SystemTypes.Exception))
        this.HandleError(Catch.TypeExpression, Error.BadExceptionType);
      Catch savedCatchClause = this.currentCatchClause;
      this.currentCatchClause = Catch;
      Catch.Block = this.VisitBlock(Catch.Block);
      this.currentCatchClause = savedCatchClause;
      this.insideCatchClause = savedInsideCatchClause;
      return Catch;
    }
    public override Class VisitClass(Class Class) {
      if (Class == null) return null;
      if (Class.BaseClass == null && !(Class.Name.UniqueIdKey == StandardIds.CapitalObject.UniqueIdKey &&
                                       Class.Namespace.UniqueIdKey == StandardIds.System.UniqueIdKey))
        Class.BaseClass = SystemTypes.Object;
      else {
        Error e = Error.None;
        TypeNode bclass = Class.BaseClass;
        if (bclass != null && !(bclass is ClassParameter)) {
          if (bclass.IsSealed)
            if (Class.BaseClass.IsAbstractSealedContainerForStatics)
              e = Error.AbstractSealedBaseClass;
            else
              e = Error.CannotDeriveFromSealedType;
          else if (this.IsLessAccessible(bclass, Class))
            e = Error.BaseClassLessAccessible;
          else if (bclass.DeclaringModule != Class.DeclaringModule && (bclass == SystemTypes.Array || bclass == SystemTypes.Enum || bclass == SystemTypes.ValueType || bclass == SystemTypes.Delegate || bclass == SystemTypes.MulticastDelegate))
            e = Error.CannotDeriveFromSpecialType;
          else if (Class.IsAbstractSealedContainerForStatics && bclass != SystemTypes.Object)
            e = Error.AbstractSealedDerivedFromNonObject;
        }
        if (e != Error.None) {
          this.HandleError(Class.Name, e, this.GetTypeName(bclass), this.GetTypeName(Class));
          this.HandleRelatedError(bclass);
        }
      }
      if (Class.IsAbstractSealedContainerForStatics && this.GetTypeView(Class).Interfaces != null && this.GetTypeView(Class).Interfaces.Count > 0)
        this.HandleError(Class.Name, Error.AbstractSealedClassInterfaceImpl, this.GetTypeName(Class));
      this.VisitTypeNode(Class);
      return Class;
    }
    public override Expression VisitAnonymousNestedFunction(AnonymousNestedFunction func) {
      if (func == null) return null;
      func.Method = this.VisitMethod(func.Method);
      return func;
    }
    public override Compilation VisitCompilation(Compilation compilation) {
      if (compilation == null) return null;
      this.currentOptions = compilation.CompilerParameters as CompilerOptions;
      Module module = this.currentModule = compilation.TargetModule;
      if (module != null) {
        AssemblyNode assem = module as AssemblyNode;
        if (assem != null) {
          this.currentAssembly = assem;
          if (this.currentOptions.IsContractAssembly) { // handle compiler option /shadow:<assembly>
            this.shadowedAssembly = AssemblyNode.GetAssembly(this.currentOptions.ShadowedAssembly);
            if (this.shadowedAssembly == null) {
              this.HandleError(compilation, Error.CannotLoadShadowedAssembly, this.currentOptions.ShadowedAssembly);
              return compilation;
            }
            assem.Attributes.Add(ShadowedAssemblyAttribute());
            this.isCompilingAContractAssembly = true;
          }
          assem.Attributes = this.VisitAttributeList(assem.Attributes, assem);
          assem.ModuleAttributes = this.VisitModuleAttributes(assem.ModuleAttributes);
        } else {
          this.currentAssembly = module.ContainingAssembly;
          module.Attributes = this.VisitModuleAttributes(module.Attributes);
        }
      }
      return base.VisitCompilation(compilation);
    }
    private AttributeNode ShadowedAssemblyAttribute() {
      StringBuilder publicKeyBuffer = new StringBuilder();
      if (shadowedAssembly.PublicKeyToken != null) {
        foreach (Byte b in shadowedAssembly.PublicKeyToken) { publicKeyBuffer.Append(b.ToString("X2")); }
      }
      string publicKey = publicKeyBuffer.ToString();
      Debug.Assert(publicKey.Length == (shadowedAssembly.PublicKeyToken == null ? 0 : shadowedAssembly.PublicKeyToken.Length) * 2);
      return new AttributeNode(
          new MemberBinding(null, SystemTypes.ShadowsAssemblyAttribute.GetConstructor(SystemTypes.String, SystemTypes.String, SystemTypes.String)),
          new ExpressionList(new Expression[] { 
              new Literal(publicKey, SystemTypes.String),
              new Literal(shadowedAssembly.Version == null ? "0.0" : shadowedAssembly.Version.ToString(), SystemTypes.String),
              new Literal(shadowedAssembly.Name, SystemTypes.String)
          }));
    }
    public override CompilationUnit VisitCompilationUnit(CompilationUnit cUnit) {
      if (cUnit == null) return null;
      this.currentPreprocessorDefinedSymbols = cUnit.PreprocessorDefinedSymbols;
      this.DetermineIfNonNullCheckingIsDesired(cUnit);
      if (cUnit.Compilation != null) {
        this.currentOptions = cUnit.Compilation.CompilerParameters as CompilerOptions;
        if (this.currentOptions != null)
          this.isCompilingAContractAssembly = this.currentOptions.IsContractAssembly;
      }
      return base.VisitCompilationUnit(cUnit);
    }
    public virtual void DetermineIfNonNullCheckingIsDesired(CompilationUnit cUnit) {
      this.NonNullChecking = false;
    }
    public override Expression VisitConstruct(Construct cons) {
      if (cons == null) return cons;
      MemberBinding mb = cons.Constructor as MemberBinding;
      if (mb == null) {
        Literal lit = cons.Constructor as Literal;
        DelegateNode del = lit == null ? null : lit.Value as DelegateNode;
        if (del != null && cons.Operands != null && cons.Operands.Count == 1) {
          MemberList members = null;
          TypeNode offendingType = this.currentType;
          string offendingName = null;
          Expression e = cons.Operands[0];
          NameBinding nb = e as NameBinding;
          if (nb != null) {
            members = nb.BoundMembers;
            offendingName = nb.Identifier.ToString();
          } else {
            QualifiedIdentifier qualId = e as QualifiedIdentifier;
            if (qualId != null) {
              offendingName = qualId.Identifier.ToString();
              e = qualId.Identifier;
              Expression ob = qualId.Qualifier;
              if (ob is TemplateInstance) {
                this.VisitTemplateInstance((TemplateInstance)ob);
                return null;
              }
              if (ob is Literal && ob.Type == SystemTypes.Type)
                members = this.GetTypeView(offendingType = (ob as Literal).Value as TypeNode).GetMembersNamed(qualId.Identifier);
              else if (ob != null && ob.Type != null)
                members = this.GetTypeView(offendingType = TypeNode.StripModifiers(ob.Type)).GetMembersNamed(qualId.Identifier);
            }
          }
          int n = members == null ? 0 : members.Count;
          Member offendingMember = n > 0 ? members[0] : null;
          for (int i = 0; i < n; i++) {
            Method m = members[i] as Method;
            if (m != null) { offendingMember = m; break; }
          }
          if (offendingMember == null) {
            if (offendingName == null)
              this.HandleError(e, Error.MethodNameExpected);
            else
              this.HandleError(e, Error.NoSuchMember, this.GetTypeName(offendingType), offendingName);
          } else if (offendingMember is Method) {
            this.HandleError(e, Error.NoMethodMatchesDelegate, this.GetDelegateSignature(del), this.GetUnqualifiedMemberName((Method)offendingMember));
            this.HandleRelatedError(del);
            this.HandleRelatedError(offendingMember);
          } else
            this.HandleNonMethodWhereMethodExpectedError(e, offendingMember);
          return null;
        }
        return null;
      }
      InstanceInitializer c = mb.BoundMember as InstanceInitializer;
      if (c == null) {
        TypeNode consType = TypeNode.StripModifiers(cons.Type);
        if (consType != null && consType.IsValueType && (cons.Operands == null || cons.Operands.Count == 0))
          return new Local(StandardIds.NewObj, consType, cons.SourceContext);
        TypeNode t = mb.BoundMember as TypeNode;
        if (t == null)
          this.HandleError(cons, Error.NoSuchConstructor, "");
        else if (t.Name == Looker.NotFound || t.Name == null)
          this.VisitTypeReference(t); //Report appropriate error about type
        else {
          MemberList members = this.GetTypeView(t).GetConstructors();
          int n = members == null ? 0 : members.Count;
          if (n == 0) {
            if ((cons.Operands == null || cons.Operands.Count == 0) && t is ITypeParameter && 
              (((ITypeParameter)t).TypeParameterFlags & (TypeParameterFlags.DefaultConstructorConstraint|TypeParameterFlags.ValueTypeConstraint)) != 0) {
              if (this.useGenerics) {
                Method createInstance = Runtime.GenericCreateInstance.GetTemplateInstance(this.currentType, t);
                return new MethodCall(new MemberBinding(null, createInstance), null, NodeType.Call, t, cons.SourceContext);
              } else {
                if ((((ITypeParameter)t).TypeParameterFlags & TypeParameterFlags.DefaultConstructorConstraint) != 0) {
                  ExpressionList arguments = new ExpressionList(Normalizer.TypeOf(t));
                  MethodCall call = new MethodCall(new MemberBinding(null, Runtime.CreateInstance), arguments, NodeType.Call, SystemTypes.Object, cons.SourceContext);
                  return new BinaryExpression(call, new Literal(t, SystemTypes.Type), NodeType.Castclass, t, cons.SourceContext);
                } else {
                  return new Local(StandardIds.NewObj, consType, cons.SourceContext);
                }
              }
            }
            if (t.IsAbstract)
              if (t.IsSealed)
                this.HandleError(cons, Error.ConstructsAbstractSealedClass, this.GetTypeName(t));
              else
                this.HandleError(cons, Error.ConstructsAbstractClass, this.GetTypeName(t));
            else
              this.HandleError(cons, Error.NoSuchConstructor, this.GetTypeName(t));
          } else {
            this.HandleError(cons, Error.NoOverloadWithMatchingArgumentCount, t.Name.ToString(), (cons.Operands == null ? 0 : cons.Operands.Count).ToString());
          }
          for (int i = 0; i < n; i++) {
            Member m = members[i];
            if (m == null) continue;
            if (m.SourceContext.Document == null) continue;
            this.HandleRelatedError(m);
          }
        }
        return null;
      }
      if (c.DeclaringType != null && c.DeclaringType.IsAbstract) {
        if (c.DeclaringType.IsSealed)
          this.HandleError(cons, Error.ConstructsAbstractSealedClass, this.GetTypeName(c.DeclaringType));
        else
          this.HandleError(cons, Error.ConstructsAbstractClass, this.GetTypeName(c.DeclaringType));
      } else if (this.NotAccessible(c)) {
        this.HandleError(cons, Error.MemberNotVisible, this.GetMemberSignature(c));
        return null;
      }
      this.CheckForObsolesence(cons, c);
      if (!(c.DeclaringType is DelegateNode) || 
        !(cons.Operands != null && cons.Operands.Count == 2 && (cons.Operands[0].NodeType == NodeType.AnonymousNestedFunction)))
        this.CoerceArguments(c.Parameters, ref cons.Operands, false, c.CallingConvention);
      if (cons.Owner != null) {
        this.VisitExpression(cons.Owner);
        TypeNode ownerType = cons.Owner.Type;
        if (ownerType == null) { // must have been an error somewhere else
          cons.Owner = null;
        } else if (ownerType.IsValueType) {
          cons.Owner = this.typeSystem.ImplicitCoercion(cons.Owner, SystemTypes.Object, this.TypeViewer);
        }
      }
      return cons;
    }
    public virtual void CoerceArguments(ParameterList parameters, ref ExpressionList arguments, bool doNotVisitArguments, CallingConventionFlags callingConvention) {
      if (arguments == null) arguments = new ExpressionList();
      int n = arguments.Count;
      int m = parameters == null ? 0 : parameters.Count;
      //if fewer arguments than parameters, supply default values
      for (; n < m; n++) {
        Parameter p = parameters[n];
        TypeNode type = p == null ? null : p.Type;
        if (type == null) type = SystemTypes.Object;
        type = TypeNode.StripModifiers(type);
        if (p.DefaultValue != null)
          arguments.Add(p.DefaultValue);
        else {
          //There should already have been a complaint. Just recover.
          TypeNode elementType = parameters[n].GetParamArrayElementType();
          if (elementType != null) break;
          arguments.Add(new UnaryExpression(new Literal(type, SystemTypes.Type), NodeType.DefaultValue, type));
        }
      }
      if (m > 0) {
        TypeNode elementType = TypeNode.StripModifiers(parameters[m-1].GetParamArrayElementType());
        TypeNode lastArgType = null;
        if (elementType != null && (n > m || (n == m - 1) || n == m
          && (lastArgType = TypeNode.StripModifiers(arguments[m-1].Type)) != null && 
              !this.GetTypeView(lastArgType).IsAssignableTo(TypeNode.StripModifiers(parameters[m-1].Type)) &&
          !(arguments[m-1].Type == SystemTypes.Object && arguments[m-1] is Literal && ((Literal)arguments[m-1]).Value == null))) {
          ExpressionList varargs = new ExpressionList(n-m+1);
          for (int i = m-1; i < n; i++)
            varargs.Add(arguments[i]);
          Debug.Assert(m <= n || m == n+1);
          while (m > n++) arguments.Add(null);
          arguments[m-1] = new ConstructArray(parameters[m-1].GetParamArrayElementType(), varargs);
          arguments.Count = m;
          n = m;
        }
      }
      if (n > m) {
        // Handle Varargs
        Debug.Assert(n == m+1);
        Debug.Assert((callingConvention & CallingConventionFlags.VarArg) != 0);
        ArglistArgumentExpression ale = arguments[n-1] as ArglistArgumentExpression;
        if (ale != null) {
          // rewrite nested arguments to one level.
          // otherwise, the method does not match and I expect the Checker to issue a nice message.
          ExpressionList newArgs = new ExpressionList(n - 1 + ale.Operands.Count);
          for (int i=0; i<n-1; i++) {
            newArgs.Add(arguments[i]);
          }
          for (int i=0; i<ale.Operands.Count; i++) {
            newArgs.Add(ale.Operands[i]);
          }
          arguments = newArgs;
          // adjust formal parameters to actuals
          parameters = (ParameterList)parameters.Clone();
          for (int i=0; i<ale.Operands.Count; i++) {
            if (arguments[i+m] != null) {
              TypeNode pType = arguments[i+m].Type;
              Reference r = pType as Reference;
              if (r != null) pType = r.ElementType;
              parameters.Add(new Parameter(null, pType));
            } else {
              parameters.Add(new Parameter(null, SystemTypes.Object));
            }
          }
          m = arguments.Count;
          n = m;
        } else {
          // leave arguments and let type coercion fail
          // adjust formal parameters to actuals
          parameters = (ParameterList)parameters.Clone();
          parameters.Add(Resolver.ArglistDummyParameter);
          n = parameters.Count;
        }
      }
      if (doNotVisitArguments) {
        for (int i = 0; i < n; i++) {
          Parameter p = this.typeSystem.currentParameter = parameters[i];
          Literal lit = arguments[i] as Literal;
          if (lit != null && lit.Value is TypeNode && p.Type == SystemTypes.Type)
            arguments[i] = lit;
          else {
            if (!this.DoNotVisitArguments(p.DeclaringMethod)) {
              Expression e = arguments[i] = this.typeSystem.ImplicitCoercion(arguments[i], p.Type, this.TypeViewer);
              if (e is BinaryExpression && e.NodeType == NodeType.Box) e = arguments[i] = ((BinaryExpression)e).Operand1;
            }
          }
        }
      } else {
        for (int i = 0; i < n; i++) {

          Parameter p = this.typeSystem.currentParameter = parameters[i];

          bool savedMayReferenceThisAndBase = this.MayReferenceThisAndBase;
          if (p.IsOut
            && this.currentMethod is InstanceInitializer) {
            // allow calls "f(out this.x)" before the explicit base ctor call
            this.MayReferenceThisAndBase = true;
          }

          arguments[i] = this.CoerceArgument(this.VisitExpression(arguments[i]), p);

          this.MayReferenceThisAndBase = savedMayReferenceThisAndBase;
        }
      }
      this.typeSystem.currentParameter = null;
    }

    protected virtual bool DoNotVisitArguments(Method m) {
      return false; 
    }

    public virtual Expression CoerceArgument(Expression argument, Parameter p) {
      return this.typeSystem.ImplicitCoercion(argument, p.Type, this.TypeViewer);
    }
    public override Node VisitComposition(Composition comp) {
      if (comp == null) return null;
      Node result = base.VisitComposition(comp);
      comp = result as Composition;
      if (comp == null) return result;
      if (comp.Expression == null || comp.Expression.Type == null) return null;
      return comp;
    }
    public override Expression VisitConstructArray(ConstructArray consArr) {
      if (consArr == null) return null;
      TypeNode elemType = consArr.ElementType = this.VisitTypeReference(consArr.ElementType);
      elemType = TypeNode.StripModifiers(elemType);
      if (elemType == SystemTypes.DynamicallyTypedReference || elemType == SystemTypes.ArgIterator) {
        this.HandleError(consArr, Error.ArrayElementCannotBeTypedReference, this.GetTypeName(elemType));
        return null;
      }
      ExpressionList sizes = consArr.Operands;
      int n = sizes == null ? 0 : sizes.Count;
      long[] knownSizes = new long[n];
      for (int i = 0; i < n; i++) {
        knownSizes[i] = -1;
        Expression e = this.VisitExpression(sizes[i]);
        MemberBinding mb = e as MemberBinding;
        Field f = mb == null ? null : mb.BoundMember as Field;
        Literal lit;
        if (f != null && f.IsLiteral)
          lit = f.DefaultValue;
        else
          lit = e as Literal;
        if (lit == null)
          sizes[i] = this.typeSystem.CoerceToIndex(e, this.TypeViewer);
        else {
          if (this.typeSystem.ImplicitLiteralCoercionFromTo(lit, lit.Type, SystemTypes.Int32)) {
            lit = this.typeSystem.ImplicitLiteralCoercion(lit, lit.Type, SystemTypes.Int32, this.TypeViewer);
            if (n == 1)
              sizes[i] = this.typeSystem.ExplicitPrimitiveCoercion(lit, SystemTypes.Int32, SystemTypes.IntPtr);
            else
              sizes[i] = lit;
            if (lit == null || !(lit.Value is int)) return null;
            int siz = (int)lit.Value;
            if (siz < 0) {
              this.HandleError(lit, Error.NegativeArraySize);
              return null;
            }
            knownSizes[i] = siz;
          } else if (this.typeSystem.ImplicitLiteralCoercionFromTo(lit, lit.Type, SystemTypes.Int64)) {
            lit = this.typeSystem.ImplicitLiteralCoercion(lit, lit.Type, SystemTypes.Int64, this.TypeViewer);
            sizes[i] = this.typeSystem.ExplicitPrimitiveCoercion(lit, SystemTypes.Int64, SystemTypes.IntPtr);
            //REVIEW: what if array has rank > 1?
            if (lit == null || !(lit.Value is long)) return null;
            long siz = (long)lit.Value;
            if (siz < 0) {
              this.HandleError(lit, Error.NegativeArraySize);
              return null;
            }
            knownSizes[i] = siz;
          } else
            sizes[i] = this.typeSystem.CoerceToIndex(e, this.TypeViewer);
        }
      }
      consArr.Operands = sizes;
      ExpressionList initializers = consArr.Initializers = this.VisitExpressionList(consArr.Initializers);
      int m = initializers == null ? 0 : initializers.Count;
      if (n > 0 && initializers != null) {
        if (knownSizes[0] < 0)
          this.HandleError(sizes[0], Error.ConstantExpected);
        else if (knownSizes[0] != m)
          this.HandleError(consArr, Error.ArrayInitializerLengthMismatch, m.ToString(), knownSizes[0].ToString());
        //TODO: check that nested array initializers match known sizes of other dimensions
      }
      int rank = consArr.Type is OptionalModifier ?  ((ArrayType)((OptionalModifier)consArr.Type).ModifiedType).Rank : ((ArrayType)consArr.Type).Rank;
      if (rank > 1) elemType = elemType.GetArrayType(rank-1);
      for (int i = 0; i < m; i++)
        initializers[i] = this.typeSystem.ImplicitCoercion(initializers[i], elemType, this.TypeViewer);
      //TODO: check that all nested array initializers are of the same size
      if (consArr.Owner != null) {
        this.VisitExpression(consArr.Owner);
        TypeNode ownerType = consArr.Owner.Type;
        if (ownerType == null) { // must have been an error somewhere else
          consArr.Owner = null;
        } else if (ownerType.IsValueType) {
          consArr.Owner = this.typeSystem.ImplicitCoercion(consArr.Owner, SystemTypes.Object, this.TypeViewer);
        }
      }
      return consArr;
    }
    public override Expression VisitConstructFlexArray(ConstructFlexArray consArr) {
      if (consArr == null) return null;
      TypeNode elemType = consArr.ElementType = this.VisitTypeReference(consArr.ElementType);
      consArr.Operands = this.VisitExpressionList(consArr.Operands);
      ExpressionList initializers = consArr.Initializers = this.VisitExpressionList(consArr.Initializers);
      for (int i = 0, n = initializers == null ? 0 : initializers.Count; i < n; i++)
        initializers[i] = this.typeSystem.ImplicitCoercion(initializers[i], elemType, this.TypeViewer);
      return consArr;
    }
    public override Statement VisitContinue(Continue Continue) {
      if (Continue == null) return null;
      int level = Continue.Level != null ? (int)Continue.Level.Value : 1;
      if (level < 0 || level > this.loopCount) {
        this.HandleError(Continue, Error.BadExitOrContinue);
        return null;
      }
      return Continue;
    }
    public override DelegateNode VisitDelegateNode(DelegateNode delegateNode) {
      delegateNode = base.VisitDelegateNode(delegateNode);
      if (delegateNode == null) return null;
      if (this.IsLessAccessible(delegateNode.ReturnType, delegateNode)) {
        this.HandleError(delegateNode.Name, Error.ReturnTypeLessAccessibleThanDelegate,
          this.GetTypeName(delegateNode.ReturnType), this.GetTypeName(delegateNode));
        this.HandleRelatedError(delegateNode.ReturnType);
      }
      this.CheckParameterTypeAccessibility(delegateNode.Parameters, delegateNode);
      return delegateNode;
    }
    public override Statement VisitDoWhile(DoWhile doWhile) {
      if (doWhile == null) return null;
      this.loopCount++;
      doWhile.Invariants = this.VisitLoopInvariantList(doWhile.Invariants);
      doWhile.Body = this.VisitBlock(doWhile.Body);
      doWhile.Condition = this.VisitBooleanExpression(doWhile.Condition);
      this.loopCount--;
      return doWhile;
    }
    public override Statement VisitExit(Exit Exit) {
      if (Exit == null) return null;
      int level = Exit.Level != null ? (int)Exit.Level.Value : 1;
      if (level < 0 || level > (this.loopCount+this.switchCaseCount)) {
        this.HandleError(Exit, Error.BadExitOrContinue);
        return null;
      }
      return Exit;
    }

    public override Statement VisitExpose(Expose Expose) {
      if (Expose == null) return null;
      Expression e = this.VisitExpression(Expose.Instance);
      if (e == null) return null;
      Expression g = Expose.Instance = e;
      TypeNode exprT = this.typeSystem.Unwrap(g.Type);
      if (exprT != null && !(exprT is Class || exprT is Struct)) //WS needs right error code
        this.HandleError(g, Error.OnlyStructsAndClassesCanHaveInvariants, this.GetTypeName(exprT));
      if (exprT == null)
        exprT = SystemTypes.Object;
      Expose.Body = this.VisitBlock(Expose.Body);
      return Expose;
    }

    public override Expression VisitExpression(Expression expression) {
      if (expression == null) return null;
      return base.VisitExpression(expression);
    }
    public override Statement VisitFixed(Fixed Fixed) {
      bool savedInsideFixed = this.insideFixed;
      this.insideFixed = true;
      bool savedInsideFixedDeclarator = this.insideFixedDeclarator;
      this.insideFixedDeclarator = true;
      Fixed.Declarators = (Statement)this.Visit(Fixed.Declarators);
      this.insideFixedDeclarator = savedInsideFixedDeclarator;
      Fixed.Body = this.VisitBlock(Fixed.Body);
      this.insideFixed = savedInsideFixed;
      return Fixed;
    }
    public override Field VisitField(Field field) {
      if (field == null) return null;
      bool savedInsideUnsafeCode = this.typeSystem.insideUnsafeCode;
      this.typeSystem.insideUnsafeCode = field.IsUnsafe;
      if (field.DeclaringType is Interface && !this.InterfaceAllowsThisKindOfField(field))
        this.HandleError(field, Error.InterfaceHasField);
      this.currentField = field;
      field.ImplementedInterfaces = this.VisitInterfaceReferenceList(field.ImplementedInterfaces);
      if (this.currentType != null && (this.currentType.Flags & TypeFlags.ExplicitLayout) != 0 && !field.IsStatic) {
        if ((field.Attributes == null || field.GetAttribute(SystemTypes.FieldOffsetAttribute) == null) && (field.DeclaringType == null || !field.DeclaringType.IsNormalized)) {
          this.HandleError(field.Name, Error.MissingStructOffset, this.GetMemberSignature(field));
        }
      }
      field.Attributes = this.VisitAttributeList(field.Attributes, field);
      field.Type = this.VisitTypeReference(field.Type);
      EnumNode enumType = field.DeclaringType as EnumNode;
      if (enumType == null && this.IsLessAccessible(field.Type, field) && (field.DeclaringType == null || !field.DeclaringType.IsNormalized)) {
        this.HandleError(field.Name, Error.FieldTypeLessAccessibleThanField, this.GetTypeName(field.Type), this.GetTypeName(field.DeclaringType)+"."+field.Name);
        this.HandleRelatedError(field.Type);
      }
      if (field.IsVolatile) {
        if (field.IsInitOnly || field.IsLiteral) {
          this.HandleError(field.Name, Error.VolatileAndReadonly, this.GetMemberSignature(field));
          field.Flags &= ~FieldFlags.InitOnly;
        } else if (field.Type != null && field.Type.IsValueType) {
          switch (this.typeSystem.GetUnderlyingType(field.Type).TypeCode) {
            case TypeCode.Boolean:
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Char:
            case TypeCode.Single:
              break;
            default:
              if (field.Type != SystemTypes.IntPtr && field.Type != SystemTypes.UIntPtr)
                this.HandleError(field.Name, Error.VolatileNonWordSize, this.GetMemberSignature(field), this.GetTypeName(field.Type));
              break;
          }
        }
      }
      if (field.Initializer != null) {
        if (!field.IsStatic && field.DeclaringType is Struct)
          this.HandleError(field.Initializer, Error.InstanceFieldInitializerInStruct, this.GetMemberSignature(field));
        bool savedCheckOverflow = this.typeSystem.checkOverflow;
        this.typeSystem.checkOverflow = true;
        if (field.IsLiteral && enumType != null) {
          field.Initializer = this.VisitExpression(field.Initializer);
          if (field.Initializer != null && field.DefaultValue != null) {
            Literal lit = field.DefaultValue;
            if (lit.Type != null && !this.typeSystem.ImplicitLiteralCoercionFromTo(lit, lit.Type, enumType.UnderlyingType)) {
              if (!enumType.IsErroneous) {
                if ((lit.Type.IsPrimitiveInteger || lit.Type == enumType) && lit.Value is IConvertible && ((IConvertible)lit.Value).ToInt64(null) >= 0)
                  this.HandleError(field.Initializer, Error.EnumerationValueOutOfRange, this.GetMemberSignature(field));
                else
                  this.HandleError(field.Initializer, Error.NoImplicitCoercionFromConstant, lit.SourceContext.SourceText, this.GetTypeName(enumType.UnderlyingType));
              }
              field.DefaultValue = new Literal(0, enumType.UnderlyingType, lit.SourceContext);
              enumType.IsErroneous = true;
            }
          } else {
            field.DefaultValue = new Literal(0, enumType.UnderlyingType);
            enumType.IsErroneous = true;
          }
        } else {
          field.Initializer = this.typeSystem.ImplicitCoercion(this.VisitExpression(field.Initializer), field.Type, this.TypeViewer);
          if (field.IsLiteral) field.DefaultValue = field.Initializer as Literal;
        }
        this.typeSystem.checkOverflow = savedCheckOverflow;
      }
      if (field.IsLiteral && field.DefaultValue == null) {
        if (field.Initializer != null)
          this.HandleError(field.Initializer, Error.NotConstantExpression, this.GetMemberSignature(field));
        field.Flags &= ~(FieldFlags.Literal|FieldFlags.HasDefault);
        field.Flags |= FieldFlags.InitOnly;
      }
      if (field.IsStrictReadonly) {
        if (!field.IsInitOnly)
          this.HandleError(field, Error.StrictReadonlyNotReadonly);
        if (field.IsStatic)
          this.HandleError(field, Error.StrictReadonlyStatic);
      }
      this.currentField = null;
      this.typeSystem.insideUnsafeCode = savedInsideUnsafeCode;
      return field;
    }
    public virtual bool InterfaceAllowsThisKindOfField(Field field) {
      if (field == null) return true;
      return field.IsStatic || field.IsLiteral;
    }
    public virtual AttributeNode VisitFieldAttribute(AttributeNode attr, Field field) {
      if (attr == null || field == null) return null;
      MemberBinding mb = attr.Constructor as MemberBinding;
      if (mb == null || mb.BoundMember == null) return null;
      if (mb.BoundMember.DeclaringType == SystemTypes.FieldOffsetAttribute) {
        if (field.DeclaringType == null) return null;
        if ((field.DeclaringType.Flags & TypeFlags.ExplicitLayout) == 0) {
          this.HandleError(mb, Error.FieldOffsetNotAllowed);
          return null;
        }
        if (field.IsStatic) {
          this.HandleError(mb, Error.FieldOffsetNotAllowedOnStaticField);
          return null;
        }
        ExpressionList args = attr.Expressions;
        if (args == null || args.Count < 1) { Debug.Assert(false); return null; }
        Literal lit = args[0] as Literal;
        if (lit == null || !(lit.Value is int)) return null;
        int offset = (int)lit.Value;
        if (offset < 0) {
          this.HandleError(args[0], Error.CustomAttributeError, this.GetTypeName(SystemTypes.FieldOffsetAttribute), "Value");
          return null;
        }
        field.Offset = offset;
        attr.IsPseudoAttribute = true;
        return attr;
      }
      if (mb.BoundMember.DeclaringType == SystemTypes.NonSerializedAttribute) {
        field.Flags |= FieldFlags.NotSerialized;
        attr.IsPseudoAttribute = true;
        return attr;
      }
      //TODO: deal with MarshalAs

      // ElementsRep and ElementsPeer may only be attached to arrays and subtypes of IEnumerable and IEnumerable-like types     
      TypeNode fieldType = TypeNode.StripModifiers(field.Type);
      if (fieldType != null &&
           (attr.Type == SystemTypes.ElementsRepAttribute || attr.Type == SystemTypes.ElementsPeerAttribute) &&
           !((fieldType is ArrayType) || fieldType.IsAssignableTo(SystemTypes.IEnumerable) || HasEnumerablePattern(fieldType)))
        this.HandleError(field, Error.InvalidUsageOfElementsRepPeer);

      return attr;
    }

    public bool HasEnumerablePattern(TypeNode Type) {
      CollectionEnumerator result = new CollectionEnumerator();
      this.LookForEnumerablePattern(Type, result);
      Method getEnumerator = result.GetEnumerator;
      if (getEnumerator == null)
        this.LookForEnumeratorPattern(Type, result);
      else {
        TypeNode enumeratorType = getEnumerator.ReturnType;
        enumeratorType = TypeNode.StripModifiers(enumeratorType);
        if (!(enumeratorType is Class || enumeratorType is Struct || enumeratorType is Interface))
          return false;
      }
      Method moveNext = result.MoveNext;
      Method getCurrent = result.GetCurrent;
      if (this.NotAccessible(getEnumerator) || (getEnumerator == null && (moveNext == null || getCurrent == null)))
        return false;
      if (getCurrent == null || this.NotAccessible(getCurrent) || !getCurrent.IsSpecialName || getCurrent.IsStatic)
        return false;
      if (moveNext == null || this.NotAccessible(moveNext) || moveNext.ReturnType != SystemTypes.Boolean || moveNext.IsStatic)
        return false;

      return true;
    }

    public override Statement VisitFinally(Finally Finally) {
      if (Finally == null) return null;
      BlockScope savedCurrentFinallyClause = this.currentFinallyClause;
      this.currentFinallyClause = Finally.Block.Scope;
      Finally.Block = this.VisitBlock(Finally.Block);
      this.currentFinallyClause = savedCurrentFinallyClause;
      return Finally;
    }
    public override Statement VisitFor(For For) {
      if (For == null) return null;
      this.loopCount++;
      For.Initializer = this.VisitStatementList(For.Initializer);
      For.Invariants = this.VisitLoopInvariantList(For.Invariants);
      For.Condition = this.VisitBooleanExpression(For.Condition);
      For.Incrementer = this.VisitStatementList(For.Incrementer);
      For.Body = this.VisitBlock(For.Body);
      this.loopCount--;
      return For;
    }
    public override Statement VisitForEach(ForEach forEach) {
      if (forEach == null) return null;
      forEach.InductionVariable = this.VisitTargetExpression(forEach.InductionVariable);
      this.loopCount++;
      forEach.Invariants = this.VisitLoopInvariantList(forEach.Invariants);
      forEach.TargetVariableType = this.VisitTypeReference(forEach.TargetVariableType);
      forEach.TargetVariable = this.VisitTargetExpression(forEach.TargetVariable);
      forEach.SourceEnumerable = this.VisitEnumerableCollection(forEach.SourceEnumerable, forEach.TargetVariableType);
      if (forEach.TargetVariableType == null) {
        this.loopCount--;
        return null;
      }
      forEach.Body = this.VisitBlock(forEach.Body);
      MemberBinding mb = forEach.TargetVariable as MemberBinding;
      if (mb != null) {
        Field f = mb.BoundMember as Field;
        if (f != null)
          f.Flags &= ~FieldFlags.InitOnly;
      }
      this.loopCount--;
      return forEach;
    }
    public override Statement VisitFunctionDeclaration(FunctionDeclaration fDecl) {
      if (fDecl == null) return fDecl;
      fDecl.Method = this.VisitMethod(fDecl.Method);
      return fDecl;
    }
    public virtual CollectionEnumerator VisitEnumerableCollection(Expression collection, TypeNode targetVariableType) {
      if (collection == null) return null;
      collection = this.VisitExpression(collection);
      if (collection == null) return null;
      TypeNode collectionType = TypeNode.StripModifier(collection.Type, SystemTypes.NonNullType);
      if (collectionType == null) return null;
      while (collectionType is TypeAlias) { //HACK
        collectionType = ((TypeAlias)collectionType).AliasedType;
        collection = this.typeSystem.ExplicitCoercion(collection, collectionType, this.TypeViewer);
        collectionType = TypeNode.StripModifiers(collection.Type);
      }
      CollectionEnumerator result = new CollectionEnumerator();
      result.Collection = collection;
      result.SourceContext = collection.SourceContext;
      TypeNode ctype = collectionType;
      Reference r = collectionType as Reference;
      if (r != null) {
        ctype = TypeNode.StripModifiers(r.ElementType);
        result.Collection = this.typeSystem.AutoDereferenceCoercion(result.Collection);
      }
      //Special case for arrays
      ArrayType arrayType = ctype as ArrayType;
      if (arrayType != null && arrayType.IsSzArray()) {
        result.ElementLocal = new Local(Identifier.Empty, arrayType.ElementType, collection.SourceContext);
        result.ElementCoercion = this.typeSystem.ExplicitCoercion(result.ElementLocal, targetVariableType, this.TypeViewer);
        if (result.ElementCoercion == null) return null;
        return result;
      }
      //Special case for pointers
      if (ctype.IsPointerType) {
        if (this.VisitEnumerablePointer(result, ctype, collection, targetVariableType))
          return result;
      }
      //Look for Enumerable pattern. If not present, look for Enumerator pattern.
      this.LookForEnumerablePattern(ctype, result);
      Method getEnumerator = result.GetEnumerator;
      if (getEnumerator == null)
        this.LookForEnumeratorPattern(ctype, result);
      else {
        TypeNode enumeratorType = getEnumerator.ReturnType;
        enumeratorType = TypeNode.StripModifiers(enumeratorType);
        if (!(enumeratorType is Class || enumeratorType is Struct || enumeratorType is Interface)) {
          this.HandleError(collection, Error.BadGetEnumerator, this.GetTypeName(enumeratorType));
          return null;
        }
      }
      Method moveNext = result.MoveNext;
      Method getCurrent = result.GetCurrent;
      if (this.NotAccessible(getEnumerator) || (getEnumerator == null && (moveNext == null || getCurrent == null))) {
        if (collection.Type == SystemTypes.Object && collection is Literal && ((Literal)collection).Value == null)
          this.HandleError(collection, Error.NullNotAllowed);
        else
          this.HandleError(collection, Error.BadForeachCollection, this.GetTypeName(ctype), this.GetTypeName(ctype), "GetEnumerator");
        return null;
      }
      if (getCurrent == null || this.NotAccessible(getCurrent) || !getCurrent.IsSpecialName || getCurrent.IsStatic) {
        this.HandleError(collection, Error.BadForeachCollection, this.GetTypeName(ctype),
          getCurrent == null ? this.GetTypeName(getEnumerator.ReturnType) : this.GetTypeName(getCurrent.DeclaringType), "Current");
        return null;
      }
      if (moveNext == null || this.NotAccessible(moveNext) || moveNext.ReturnType != SystemTypes.Boolean || moveNext.IsStatic) {
        this.HandleError(collection, Error.BadForeachCollection, this.GetTypeName(ctype),
          moveNext == null ? this.GetTypeName(getEnumerator.ReturnType) : this.GetTypeName(moveNext.DeclaringType), "MoveNext");
        return null;
      }
      result.ElementLocal = new Local(Identifier.Empty, getCurrent.ReturnType);
      result.ElementLocal.SourceContext = collection.SourceContext;
      Expression elementLocalExp = result.ElementLocal;
      if (targetVariableType.IsValueType && getCurrent.ReturnType == SystemTypes.Object) {
        // add explicit cast to non-null
        elementLocalExp = this.typeSystem.ExplicitNonNullCoercion(elementLocalExp, OptionalModifier.For(SystemTypes.NonNullType, getCurrent.ReturnType));
      }
      result.ElementCoercion = this.typeSystem.ExplicitCoercion(elementLocalExp, targetVariableType, this.TypeViewer);
      if (result.ElementCoercion == null) return null;
      return result;
    }
    Method LookupMethod(TypeNode type, Identifier name, params TypeNode[] types) {
      Interface iface = type as Interface;
      MemberList ml = null;
      if (iface != null) ml = ((Interface)this.GetTypeView(iface)).GetAllMembersNamed(name);
      ClassParameter cp = type as ClassParameter;
      if ((ml == null || ml.Count == 0)&& cp != null) ml = ((ClassParameter)this.GetTypeView(cp)).GetAllMembersNamed(name);
      if (ml == null || ml.Count == 0) ml = this.GetTypeView(type).GetMembersNamed(name);
      if (ml == null || ml.Count == 0) return null;
      int m = types == null ? 0 : types.Length;
      TypeNodeList typeNodes = m == 0 ? null : new TypeNodeList(types);
      for (int i = 0, n = ml.Count; i < n; i++) {
        Method meth = ml[i] as Method;
        if (meth == null) continue;
        if (meth.ParameterTypesMatch(typeNodes)) return meth;
      }
      return null;
    }
    public virtual void LookForEnumeratorPattern(TypeNode type, CollectionEnumerator result) {
      if (type == null || result == null) return;
      //A type implements the Enumerator pattern if it implements a suitable MoveNext method and Current property
      Method moveNext = LookupMethod(type, StandardIds.MoveNext);
      if (moveNext != null) result.MoveNext = moveNext;
      Method getCurrent = LookupMethod(type, StandardIds.getCurrent);
      if (getCurrent != null) result.GetCurrent = getCurrent;
      if (moveNext != null && !this.NotAccessible(moveNext) && moveNext.ReturnType == SystemTypes.Boolean && !moveNext.IsStatic &&
        getCurrent != null && !this.NotAccessible(getCurrent) && getCurrent.IsSpecialName && !getCurrent.IsStatic) {
        return;
      }
      //A type implements the Enumerator pattern if it implements IEnumerator<T> or IEnumerator
      InterfaceList interfaces = this.GetTypeView(type).Interfaces;
      Interface typedEnumerator = null;
      Interface untypedEnumerator = null;
      for (int i = 0, n = interfaces == null ? 0 : interfaces.Count; i < n; i++) {
        Interface iface = interfaces[i];
        if (iface == null) continue;
        if (iface.Template == SystemTypes.GenericIEnumerator)
          typedEnumerator = iface;
        if (iface == SystemTypes.IEnumerator)
          untypedEnumerator = iface;
      }
      if (typedEnumerator != null) {
        result.MoveNext = this.GetTypeView(typedEnumerator).GetMethod(StandardIds.MoveNext);
        if (untypedEnumerator == null && result.MoveNext == null)
          untypedEnumerator = SystemTypes.IEnumerator;
        result.GetCurrent = this.GetTypeView(typedEnumerator).GetMethod(StandardIds.getCurrent);
        Debug.Assert(result.MoveNext != null);
        Debug.Assert(result.GetCurrent != null && result.GetCurrent.IsSpecialName);
        return;
      }
      if (untypedEnumerator != null && result.MoveNext == null)
        result.MoveNext = this.GetTypeView(untypedEnumerator).GetMethod(StandardIds.MoveNext);
      Class cl = type as Class;
      if (cl != null)
        this.LookForEnumeratorPattern(cl.BaseClass, result);
      //TODO: what if the base class implements only one part of the pattern?
    }
    public virtual void LookForEnumerablePattern(TypeNode type, CollectionEnumerator result) {
      if (type == null || result == null) return;
      //A type implements the Enumerable pattern if it implements a suitable GetEnumerator method
      Method getEnumerator = null;
      MemberList getEnumerators = this.GetTypeView(type).GetMembersNamed(StandardIds.GetEnumerator);
      for (int i = 0, n = getEnumerators == null ? 0 : getEnumerators.Count; i < n; i++) {
        Method getEnumeratorMeth = getEnumerators[i] as Method;
        if (getEnumeratorMeth == null || (getEnumeratorMeth.Parameters != null && getEnumeratorMeth.Parameters.Count != 0)) continue;
        result.GetEnumerator = getEnumeratorMeth;
        if (!this.NotAccessible(getEnumeratorMeth) && !getEnumeratorMeth.IsStatic && !(getEnumeratorMeth.ImplementedTypes != null && getEnumeratorMeth.ImplementedTypes.Count > 0)) {
          TypeNode t = TypeNode.StripModifiers(getEnumeratorMeth.ReturnType);
          this.LookForEnumeratorPattern(t, result);
          Method moveNext = result.MoveNext;
          Method getCurrent = result.GetCurrent;
          if (moveNext != null && !this.NotAccessible(moveNext) && moveNext.ReturnType == SystemTypes.Boolean && !moveNext.IsStatic &&
            getCurrent != null && !this.NotAccessible(getCurrent) && getCurrent.IsSpecialName && !getCurrent.IsStatic) {
            getEnumerator = getEnumeratorMeth;
            if (i >= n-1 || getCurrent.ReturnType != SystemTypes.Object) return;
          }
        } else if (getEnumerator == null)
          getEnumerator = getEnumeratorMeth; //Method is no good, but remember because it might mask a base class method
      }
      //A type implements the Enumerable pattern if it implements IEnumerable<T> or IEnumerable
      InterfaceList interfaces = this.GetTypeView(type).Interfaces;
      Interface enumerable = null;
      for (int i = 0, n = interfaces == null ? 0 : interfaces.Count; i < n; i++) {
        Interface iface = interfaces[i];
        if (iface == null) continue;
        if (iface.Template == SystemTypes.GenericIEnumerable) {
          enumerable = iface; break;
        }
        if (iface == SystemTypes.IEnumerable)
          enumerable = iface; //Keep looking for a typed IEnumerable
      }
      if (enumerable != null) {
        Method getEnumeratorMeth = result.GetEnumerator = this.GetTypeView(enumerable).GetMethod(StandardIds.GetEnumerator);
        Debug.Assert(result.GetEnumerator != null);
        this.LookForEnumeratorPattern(TypeNode.StripModifiers(getEnumeratorMeth.ReturnType), result); //Guaranteed to work
        Debug.Assert(result.MoveNext != null);
        Debug.Assert(result.GetCurrent != null && result.GetCurrent.IsSpecialName);
        return;
      }
      //See if any inherited interface implements the pattern
      for (int i = 0, n = interfaces == null ? 0 : interfaces.Count; i < n; i++) {
        Interface iface = interfaces[i];
        if (iface == null) continue;
        this.LookForEnumerablePattern(iface, result);
        if (result.GetEnumerator != null) return;
      }
      if (getEnumerator != null && !(getEnumerator.ImplementedTypes != null && getEnumerator.ImplementedTypes.Count > 0))
        return; //Any good GetEnumerator on the base type is hidden, so give up.
      Class cl = type as Class;
      if (cl != null)
        this.LookForEnumerablePattern(cl.BaseClass, result);
    }
    public virtual bool VisitEnumerablePointer(CollectionEnumerator result, TypeNode collectionType, Expression collection, TypeNode targetVariableType) {
      return false;
    }
    public override Event VisitEvent(Event evnt) {
      if (evnt == null) return null;
      evnt.Attributes = this.VisitAttributeList(evnt.Attributes, evnt);
      TypeNode ht = evnt.HandlerType = this.VisitTypeReference(evnt.HandlerType);
      if (ht == null) return null;
      if (!(ht is DelegateNode)) {
        this.HandleError(evnt.Name, Error.EventNotDelegate, this.GetMemberSignature(evnt));
        return null;
      }
      if (evnt.InitialHandler != null && evnt.IsAbstract) {
        this.HandleError(evnt.Name, Error.AbstractEventInitializer, this.GetMemberSignature(evnt));
        return null;
      }
      return evnt;
    }
    public override Statement VisitGoto(Goto Goto) {
      if (Goto == null) return null;
      if (Goto.TargetLabel == null) return Goto; //could happen if parser recovered from error.
      this.HandleError(Goto.TargetLabel, Error.NoSuchLabel, Goto.TargetLabel.ToString());
      return null;
    }
    public override Statement VisitGotoCase(GotoCase gotoCase) {
      if (gotoCase == null) return null;
      if (this.switchCaseCount <= 0) {
        this.HandleError(gotoCase, Error.InvalidGotoCase);
        return null;
      }
      Literal lit = gotoCase.CaseLabel as Literal;
      if (lit == null && gotoCase.CaseLabel != null) {
        this.HandleError(gotoCase.CaseLabel, Error.ConstantExpected);
        return null;
      }
      object labelVal = null;
      if (lit != null) {
        if (this.currentSwitchGoverningType != null)
          lit = this.typeSystem.ImplicitLiteralCoercionForLabel(lit, lit.Type, this.currentSwitchGoverningType, this.TypeViewer);
        if (lit == null) return null;
        lit.SourceContext = gotoCase.CaseLabel.SourceContext;
        gotoCase.CaseLabel = lit;
        labelVal = lit.Value;
      }
      SwitchCaseList currentCases = this.currentSwitchCases;
      for (int i = 0, n = currentCases == null ? 0 : currentCases.Count; i < n; i++) {
        SwitchCase scase = currentCases[i];
        if (scase == null) continue;
        if (gotoCase.CaseLabel == null) {
          if (scase.Label != null) continue;
          return new Branch(null, scase.Body, gotoCase.SourceContext);
        }
        Literal caseLit = scase.Label as Literal;
        object caseVal = caseLit == null ? null : caseLit.Value;
        if (caseVal == null || labelVal == null) continue;
        object labelValAsCaseType = labelVal;
        try {
          labelValAsCaseType = System.Convert.ChangeType(labelVal, caseVal.GetType());
        } catch { }
        if (caseVal.Equals(labelValAsCaseType))
          return new Branch(null, scase.Body, gotoCase.SourceContext);
      }
      if (gotoCase.CaseLabel == null)
        this.HandleError(gotoCase, Error.LabelNotFound, "default:");
      else
        this.HandleError(gotoCase, Error.LabelNotFound, "case "+gotoCase.CaseLabel.SourceContext.SourceText+":");
      return null;
    }
    public override Statement VisitIf(If If) {
      if (If == null) return null;
      If.Condition =  this.VisitBooleanExpression(If.Condition);
      If.TrueBlock = this.VisitBlock(If.TrueBlock);
      If.FalseBlock = this.VisitBlock(If.FalseBlock);
      return If;
    }
    public override Expression VisitIndexer(Indexer indexer) {
      if (indexer == null) return null;
      indexer.Object = this.VisitExpression(indexer.Object);
      ExpressionList indices = indexer.Operands = this.VisitExpressionList(indexer.Operands);
      Property prop = indexer.CorrespondingDefaultIndexedProperty;
      if (prop != null) {
        if (prop.Getter == null) {
          this.HandleError(indexer, Error.NoGetter, this.GetMemberSignature(prop));
          return null;
        }
        if (indexer.Object != null && this.NotAccessible(prop.Getter)) {
          this.HandleError(indexer.Object, Error.NotIndexable, this.GetTypeName(indexer.Object.Type));
          return null;
        }
        indexer.Object = this.typeSystem.AutoDereferenceCoercion(indexer.Object);
      } else {
        indexer.Object = this.typeSystem.AutoDereferenceCoercion(indexer.Object);
        //Check that the object is an array or tuple
        if (indexer.Object == null) return null;
        TypeNode obType = this.typeSystem.Unwrap(indexer.Object.Type, true);
        TupleType tupT = obType as TupleType;
        if (tupT != null) {
          if (tupT.Members == null || tupT.Members.Count == 0) {
            this.HandleError(indexer.Object, Error.NotIndexable, this.GetTypeName(indexer.Object.Type));
            return null;
          }
          if (indices != null && indices.Count == 1) {
            if (indices[0] == null) return null;
            Literal lit = indices[0] as Literal;
            if (lit == null || !(lit.Value is int)) {
              this.HandleError(indices[0], Error.TupleIndexExpected, (tupT.Members.Count-3).ToString());
              return null;
            }
            int i = (int)lit.Value;
            if (i < 0 || i > tupT.Members.Count-3) {
              this.HandleError(indices[0], Error.TupleIndexExpected, (tupT.Members.Count-3).ToString());
              return null;
            }
            Field f = tupT.Members[i] as Field;
            if (f != null) {
              indexer.Object = this.typeSystem.ExplicitCoercion(indexer.Object, tupT, this.TypeViewer);
              return indexer;
            }
          } else {
            Expression e = new Expression(NodeType.Undefined);
            e.SourceContext = indexer.SourceContext;
            e.SourceContext.StartPos = indexer.Object.SourceContext.EndPos+1;
            this.HandleError(e, Error.TupleIndexExpected, (tupT.Members.Count-3).ToString());
          }
          return null;
        }
        if (obType.IsPointerType) {
          if (indexer.Type == SystemTypes.Void) {
            this.HandleError(indexer.Object, Error.VoidError);
            return null;
          }
          if (indexer.Operands != null && indexer.Operands.Count != 1) {
            this.HandleError(indexer, Error.PointerMustHaveSingleIndex);
          }
          return indexer;
        }
        ArrayType arr = obType as ArrayType;
        if (arr == null) {
          this.HandleError(indexer.Object, Error.NotIndexable, this.GetTypeName(indexer.Object.Type));
          return null;
        }
        int rank = arr.Rank;
        int n = indices == null ? 0 : indices.Count;
        if (n != rank) {
          this.HandleError(indexer, Error.WrongNumberOfIndices, rank.ToString());
          return null;
        }
        for (int i = 0; i < n; i++)
          indices[i] = this.typeSystem.CoerceToIndex(indices[i], this.TypeViewer);
        indexer.Object = this.typeSystem.ExplicitCoercion(indexer.Object, arr, this.TypeViewer);
      }
      return indexer;
    }
    public override InstanceInitializer VisitInstanceInitializer(InstanceInitializer cons) {
      if (cons == null) return null;
      if (cons.Parameters == null || cons.Parameters.Count == 0) {
        if (cons.DeclaringType != null && cons.DeclaringType.IsValueType)
          this.HandleError(cons.Name, Error.ExplicitDefaultConstructorForValueType);
      }
      if (!cons.HasCompilerGeneratedSignature && cons.DeclaringType is Class && cons.DeclaringType.IsSealed && cons.DeclaringType.IsAbstract)
        this.HandleError(cons.Name, Error.ConstructorInAbstractSealedClass);
      else if (cons.DeclaringType is Interface) {
        this.HandleError(cons.Name, Error.InterfaceHasConstructor);
        return null;
      }
      return (InstanceInitializer)this.VisitMethod(cons);
    }
    public override Invariant VisitInvariant(Invariant @invariant) {
      if (this.currentType != null && this.currentType.Contract != null && this.currentType.Contract.InvariantMethod != null) {
        Method savedCurrentMethod = this.currentMethod;
        this.insideInvariant = true;
        this.currentMethod = this.currentType.Contract.InvariantMethod;
        @invariant.Condition = VisitBooleanExpression(@invariant.Condition);
        this.currentMethod = savedCurrentMethod;
        this.insideInvariant = false;
        return @invariant;
      } else {
        return @invariant;
      }
    }

    public override ModelfieldContract VisitModelfieldContract(ModelfieldContract mfC) {
      if (mfC == null || mfC.SatisfiesList == null) return mfC;
      bool savedInsideModelfield = this.insideModelfield;
      this.insideModelfield = true;
      bool savedMayRef = this.MayReferenceThisAndBase;
      this.MayReferenceThisAndBase = true;  //Allow this and base to be mentioned in witness and satisifes clauses. 
      //If there is a(n explicitly specified) witness, then check that the type of the witness is a subtype of the type of the modelfield      
      if (mfC.Witness != null) {
        this.typeSystem.ImplicitCoercion(mfC.Witness, mfC.ModelfieldType, this.TypeViewer); //perhaps we want a different error? now shows "cannot implicitly convert type A to type B".
        mfC.Witness = this.VisitExpression(mfC.Witness);
      }
      mfC.SatisfiesList = this.VisitBooleanExpressionList(mfC.SatisfiesList); //satisfies expressions must have type boolean.        
      this.MayReferenceThisAndBase = savedMayRef;
      this.insideModelfield = savedInsideModelfield;
      return mfC;
    }

    public override Statement VisitLabeledStatement(LabeledStatement lStatement) {
      if (lStatement == null) return null;
      if (this.referencedLabels != null && this.referencedLabels[lStatement.UniqueKey] == null)
        this.HandleError(lStatement.Label, Error.UnreferencedLabel);
      lStatement.Statement = (Statement)this.Visit(lStatement.Statement);
      return lStatement;
    }
    public override Expression VisitLiteral(Literal literal) {
      if (literal == null) return null;
      object val = literal.Value;
      TypeNode t = val as TypeNode;
      if (t != null && t.Name == Looker.NotFound) {
        this.VisitTypeReference(t);
        return null;
      }
      if (literal.SourceExpression != null) {
        Expression e = literal.SourceExpression;
        literal.SourceExpression = null;
        e = this.VisitExpression(e);
        if (e == null) return null;
      }
      //TODO: check that literal value is a primitive
      return literal;
    }
    public override Statement VisitLocalDeclarationsStatement(LocalDeclarationsStatement localDeclarations) {
      if (localDeclarations == null) return null;
      TypeNode type = localDeclarations.Type = this.VisitTypeReference(localDeclarations.Type);
      TypeNode unwrappedType = this.typeSystem.Unwrap(type);
      LocalDeclarationList decls = localDeclarations.Declarations;
      Pointer declaredPointerType = unwrappedType as Pointer;
      if (this.insideFixedDeclarator && declaredPointerType == null) {
        this.HandleError(localDeclarations.TypeExpression, Error.BadFixedVariableType);
        return null;
      }
      for (int i = 0, n = decls.Count; i < n; i++) {
        LocalDeclaration decl = decls[i];
        if (decl == null) continue;
        Field f = decl.Field;
        if (f == null) continue;
        f.Flags &= ~FieldFlags.NotSerialized; //Remove this flag so that subsequent references are OK
        if (type != null)
          f.Type = type;
        if (this.requireInitializer && f.Initializer == null)
          this.HandleError(decl.Name, Error.FixedMustInit);
        else if (this.insideFixedDeclarator && f.Initializer != null && f.Initializer.NodeType == NodeType.ExplicitCoercion)
          this.HandleError(decl, Error.BadExplicitCoercionInFixed);
        f.Initializer = this.VisitExpression(f.Initializer);
        if (f.Initializer == null || f.Initializer.Type == null) continue;
        if (this.insideFixedDeclarator && declaredPointerType != null && f.IsInitOnly) {
          Expression source = f.Initializer;
          TypeNode unwrappedSource = this.typeSystem.Unwrap(source.Type);
          ArrayType arrType = unwrappedSource as ArrayType;
          if (arrType != null && arrType.ElementType != null) {
            Indexer indexer = new Indexer(source, new ExpressionList(Literal.Int32Zero), arrType.ElementType, source.SourceContext);
            indexer.ArgumentListIsIncomplete = true;
            source = new UnaryExpression(indexer, NodeType.AddressOf, indexer.Type.GetReferenceType(), source.SourceContext);
          } else if (unwrappedSource == SystemTypes.String && declaredPointerType.ElementType == SystemTypes.Char) {
            f.Initializer = this.typeSystem.ImplicitCoercion(source, unwrappedSource, this.TypeViewer);
            continue;
          }
          f.Initializer = this.typeSystem.ImplicitCoercion(source, declaredPointerType, this.TypeViewer);
          continue;
        }
        if (!localDeclarations.Constant) continue;
        if (!(f.Initializer is Literal) /*&& !(f.Initializer is AnonymousNestedFunction)*/) {
          this.HandleError(f.Name, Error.NotConstantExpression, f.Name.ToString());
          decls[i] = null;
        }
        Literal lit = f.Initializer as Literal;
        if (lit != null)
          f.Initializer = this.VisitExpression(this.typeSystem.ImplicitLiteralCoercion(lit, lit.Type, f.Type, this.TypeViewer));
      }
      return localDeclarations;
    }
    public override Statement VisitLock(Lock Lock) {
      if (Lock == null) return null;
      Expression g = Lock.Guard = this.VisitExpression(Lock.Guard);
      TypeNode t = g == null ? SystemTypes.Object : this.typeSystem.Unwrap(g.Type);
      if (t.IsValueType)
        this.HandleError(g, Error.LockNeedsReference, this.GetTypeName(t));
      bool savedInsideTryBlock = this.insideTryBlock;
      this.insideTryBlock = true;
      Lock.Body = this.VisitBlock(Lock.Body);
      this.insideTryBlock = savedInsideTryBlock;
      return Lock;
    }
    public override Expression VisitMemberBinding(MemberBinding memberBinding) {
      return this.VisitMemberBinding(memberBinding, false);
    }
    public virtual Expression VisitMemberBinding(MemberBinding memberBinding, bool isTargetOfAssignment) {
      if (memberBinding == null) return null;
      Member mem = memberBinding.BoundMember;
      if (mem == null) return null;
      TypeNode memType = mem.DeclaringType;
      Expression originalTarget = memberBinding.TargetObject;
      Expression target = originalTarget;
      if (mem.IsStatic) {
        this.VisitTypeReference(mem.DeclaringType);
      }
      if (target != null) {
        QueryContext qc = target as QueryContext;
        if (qc != null && qc.Type == null) {
          this.HandleError(memberBinding, Error.IdentifierNotFound, mem.Name.ToString());
          return null;
        }
        target = memberBinding.TargetObject = this.VisitExpression(target);
        if (target == null) return null;
      }
      Property prop = mem as Property;
      if (prop != null) {
        Error e = Error.None;
        if (isTargetOfAssignment && !(prop.Type is Reference)) {
          Method setter = prop.Setter;
          if (setter == null) setter = prop.GetBaseSetter();
          if (setter == null) {
            e = Error.NoSetter;
          }
          else if (setter.IsAbstract && memberBinding.TargetObject is Base)
            e = Error.AbstractBaseCall;
        } else {
          if (prop.Getter == null) e = Error.NoGetter;
        }
        if (e != Error.None) {
          string typeName = this.GetTypeName(mem.DeclaringType);
          this.HandleError(memberBinding, e, typeName+"."+mem.Name);
          return null;
        }
      }
      TypeNode targetType = target == null ? null : TypeNode.StripModifiers(target.Type);
      bool errorIfCurrentMethodIsStatic = false;
      if (target is ImplicitThis) {
        errorIfCurrentMethodIsStatic = !(memType is BlockScope || memType is MethodScope); //locals and parameters are excempt
        if (errorIfCurrentMethodIsStatic && !this.NotAccessible(mem)) {
          if (this.MayNotReferenceThisFromFieldInitializer && this.currentMethod == null && this.currentField != null && !this.currentField.IsStatic) {
            this.HandleError(memberBinding, Error.ThisReferenceFromFieldInitializer, this.GetMemberSignature(mem));
            return null;
          }
          if (!this.GetTypeView(this.currentType).IsAssignableTo(memType)) {
            this.HandleError(memberBinding, Error.AccessToNonStaticOuterMember, this.GetTypeName(mem.DeclaringType), this.GetTypeName(this.currentType));
            return null;
          }
        }
        if (!this.MayReferenceThisAndBase && !(memType is Scope) && !this.insideInvariant) {
          this.HandleError(memberBinding, Error.ObjectRequired, this.GetMemberSignature(mem));
          return null;
        }
        targetType = null;
      } else if (target is Base || target is This) {
        if (!this.MayReferenceThisAndBase && !(this.insideInvariant && target is This)) {
          if (target is Base)
            this.HandleError(memberBinding, Error.BaseInBadContext, this.GetMemberSignature(mem));
          else
            this.HandleError(memberBinding, Error.ThisInBadContext, this.GetMemberSignature(mem));
          return null;
        }
        targetType = null;
        errorIfCurrentMethodIsStatic = true;
      } else if (!mem.IsStatic && target is Literal) {
        this.HandleError(memberBinding, Error.ObjectRequired, this.GetMemberSignature(mem));
        return null;
      } else if (target != null) {
        target = memberBinding.TargetObject = this.typeSystem.ExplicitCoercion(target, memType, this.TypeViewer);
        if (target == null) return null;
        if (prop != null && memType != null && memType.IsValueType)
          target = memberBinding.TargetObject = new UnaryExpression(target, NodeType.AddressOf, memType.GetReferenceType());
      }
      if (errorIfCurrentMethodIsStatic && 
        ((this.currentMethod != null && this.currentMethod.IsStatic) || (this.currentField != null && this.currentField.IsStatic))) {
        this.HandleError(memberBinding, Error.ObjectRequired, this.GetMemberSignature(mem));
        return null;
      }
      if (this.NotAccessible(mem, ref targetType)) {
        string mname = mem.Name.ToString();
        if (targetType == null && target != null && target.Type != null && !(target is ImplicitThis || target is This || target is Base))
          this.HandleError(memberBinding, Error.NotVisibleViaBaseType, this.GetTypeName(mem.DeclaringType)+"."+mname,
            this.GetTypeName(target.Type), this.GetTypeName(this.currentType));
        else if (mem.IsSpecialName && mem is Field && ((Field)mem).Type is DelegateNode)
          this.HandleError(memberBinding, Error.InaccessibleEventBackingField, this.GetTypeName(mem.DeclaringType) + "." + mname, this.GetTypeName(mem.DeclaringType));
        else {
          Node offendingNode = memberBinding;
          QualifiedIdentifier qi = memberBinding.BoundMemberExpression as QualifiedIdentifier;
          if (qi != null) offendingNode = qi.Identifier;
          this.HandleError(offendingNode, Error.MemberNotVisible, this.GetTypeName(mem.DeclaringType) + "." + mname);
          this.HandleRelatedError(mem);
        }
        return null;
      } else {
        string mname = mem.Name.ToString();
        if (!mem.IsCompilerControlled && (this.insideMethodContract  || this.insideInvariant)) {
          if (!this.AsAccessible(mem, currentMethod) && !this.insideInvariant) {
            this.HandleError(memberBinding, Error.MemberNotVisible, this.GetTypeName(mem.DeclaringType)+"."+mname);
            return null;
          }
          if ((this.insideMethodContract || this.insideAssertOrAssume) && !this.IsTransparentForMethodContract(mem, currentMethod)) {
            this.HandleError(memberBinding, Error.MemberMustBePureForMethodContract, this.GetTypeName(mem.DeclaringType)+"."+mname);
            return null;
          }
          if (this.insideInvariant  && !this.IsTransparentForInvariant(mem, currentMethod)) {
            this.HandleError(memberBinding, Error.MemberMustBePureForInvariant, this.GetTypeName(mem.DeclaringType)+"."+mname);
            return null;
          }
        } else if (!this.IsModelUseOk(mem, currentMethod)) {
          this.HandleError(memberBinding, Error.ModelMemberUseNotAllowedInContext, this.GetTypeName(mem.DeclaringType)+"."+mname);
          return null;
        }
      }
      Method meth = mem as Method;
      if (meth != null && memberBinding.Type != null && memberBinding.SourceContext.Document != null) {
        this.HandleError(memberBinding, Error.BadUseOfMethod, this.GetMethodSignature(meth));
        return null;
      }
      if (target != null) {
        TypeNode nt = mem as TypeNode;
        if (nt != null) {
          this.HandleError(memberBinding, Error.BadNestedTypeReference, mem.Name.ToString(), this.GetMemberSignature(mem));
          return null;
        }
      }
      Field f = mem as Field;
      if (isTargetOfAssignment && prop == null) {
        Error e = Error.BadLHSideForAssignment;
        if (f != null) {
          if (f.IsLiteral) {
            e = Error.AssignmentToLiteral;
          } else if (f.IsInitOnly) {
            e = Error.None;
            if (f.Type is Pointer && this.insideFixed && f.DeclaringType is BlockScope && !this.insideFixedDeclarator)
              e = Error.AssignmentToFixedVariable;
            else if (f.IsCompilerControlled && f.DeclaringType is BlockScope)
              f.Flags |= FieldFlags.Public;  //Do not give error on first assignment to controlled variable (such as foreach target var)
            else if ((f.DeclaringType != this.currentType && f.DeclaringType != null && !this.currentType.IsStructurallyEquivalentTo(f.DeclaringType.Template)) || 
            !((this.currentMethod is InstanceInitializer && (target is This || target is ImplicitThis)) ||
              (this.currentMethod is StaticInitializer && target == null))) {
              if (f.DeclaringType is BlockScope || f.DeclaringType is MethodScope)
                e = Error.AssignmentToReadOnlyLocal;
              else if (f.IsStatic)
                e = Error.AssignmentToReadOnlyStaticField;
              else
                e = Error.AssignmentToReadOnlyInstanceField;
            }
          } else if (target is MethodCall && target.Type.IsValueType) {
            MemberBinding mb = ((MethodCall)target).Callee as MemberBinding;
            if (mb != null) { e = Error.ResultIsNotReference; mem = mb.BoundMember; }
          } else if (isTargetOfAssignment && originalTarget is Construct && target.Type.IsValueType && mem is Field) {
            MemberBinding mb = ((Construct)originalTarget).Constructor as MemberBinding;
            if (mb != null) { e = Error.AssignmentHasNoEffect; mem = mb.BoundMember; }
          } else
            e = Error.None;
        } else if (mem is Event)
          e = Error.AssignmentToEvent;
        if (e != Error.None) {
          this.HandleError(memberBinding, e, this.GetMemberSignature(mem));
          return null;
        }
      }
      if (target != null) {
        if (mem.IsStatic) {
          this.HandleError(memberBinding, Error.TypeNameRequired, this.GetMemberSignature(mem));
          return null;
        }
      }
      this.CheckForObsolesence(memberBinding, mem);
      if (mem is Event) {
        this.HandleError(memberBinding, Error.BadUseOfEvent, this.GetMemberSignature(mem));
        return null;
      }
      if (f != null && f.IsLiteral)
        return this.typeSystem.ExplicitCoercion(f.DefaultValue, f.Type);
      return memberBinding;
    }
    TypeNode GetCorrespondingShadowedTypeNode(TypeNode tn) {
      // Lots of work needed to get this done fully. Will be done on need basis
      if (tn == null) {
        return null;
      }
      Reference refTN = tn as Reference;
      if (refTN != null) {
        TypeNode newElemNode = this.GetCorrespondingShadowedTypeNode(refTN.ElementType);
        return newElemNode.GetReferenceType();
      }
      if (tn.DeclaringModule != this.currentModule || this.shadowedAssembly == null) {
        return tn;
      }
      if (tn.DeclaringType != null) {
        TypeNode outerType = this.GetCorrespondingShadowedTypeNode(tn.DeclaringType);
        return outerType.GetNestedType(tn.Name);
      }
      if (tn.Namespace != null) {
        return this.shadowedAssembly.GetType(tn.Namespace, tn.Name);
      }
      return this.shadowedAssembly.GetType(Identifier.Empty, tn.Name);
    }
    public override Method VisitMethod(Method method) {
      if (method == null) return null;
      if (method.IsNormalized) return method;
      this.MayReferenceThisAndBase = !(method is InstanceInitializer) || method.DeclaringType == null || method.DeclaringType.IsValueType;
      if (method.Name != null && method.Name.UniqueIdKey == StandardIds.Finalize.UniqueIdKey && method.HasCompilerGeneratedSignature &&
        method.DeclaringType is Class && ((Class)method.DeclaringType).IsAbstractSealedContainerForStatics)
        this.HandleError(method.Name, Error.DestructorInAbstractSealedClass);
      method.Attributes = this.VisitAttributeList(method.Attributes, method);
      method.ReturnAttributes = this.VisitAttributeList(method.ReturnAttributes);
      method.SecurityAttributes = this.VisitSecurityAttributeList(method.SecurityAttributes);
      if ((method.ReturnType == SystemTypes.DynamicallyTypedReference || method.ReturnType == SystemTypes.ArgIterator) && 
      (this.currentOptions == null || !this.currentOptions.NoStandardLibrary) ) {
        this.HandleError(method.Name, Error.CannotReturnTypedReference, this.GetTypeName(method.ReturnType));
        method.ReturnType = SystemTypes.Object;
      }
      if (method.Body != null) {
        if (method.DeclaringType is Interface && !method.IsStatic) {
          this.HandleError(method.Name, Error.InterfaceMemberHasBody, this.GetMethodSignature(method));
          method.Body = null;
        } else if (method.IsAbstract) {
          this.HandleError(method.Name, Error.AbstractHasBody, this.GetMethodSignature(method));
          method.Body = null;
        }
      } else if (!method.IsAbstract && !method.IsExtern && !this.isCompilingAContractAssembly) {
        this.HandleError(method.Name, Error.ConcreteMissingBody, this.GetMethodSignature(method));
        return null;
      } else if (method.TemplateParameters != null && method.TemplateParameters.Count > 0 && !this.useGenerics) {
        SourceContext ctx = method.TemplateParameters[0].SourceContext;
        ctx.EndPos = method.TemplateParameters[method.TemplateParameters.Count-1].SourceContext.EndPos;
        Debug.Assert(ctx.EndPos >= ctx.StartPos);
        Node n = new UnaryExpression();
        n.SourceContext = ctx;
        if (method.DeclaringType is Interface)
          this.HandleError(n, Error.AbstractInterfaceMethod);
        else
          this.HandleError(n, Error.AbstractMethodTemplate);
        return null;
      }
      BlockScope savedCurrentFinallyClause = this.currentFinallyClause;
      Method savedCurrentMethod = this.currentMethod;
      Return savedReturnNode = this.returnNode;
      Yield savedYieldNode = this.yieldNode;
      this.currentFinallyClause = null;
      this.currentMethod = method;
      this.returnNode = null;
      this.yieldNode = null;
      MethodScope scope = method.Scope;
      this.CheckForDuplicateDeclarations(scope);

      if ((this.currentPreprocessorDefinedSymbols != null && this.currentPreprocessorDefinedSymbols.ContainsKey("DefaultExposeBlocks")) &&
        !method.IsStatic && !(method is InstanceInitializer) && method.DeclaringType is Class && 
        !method.IsAbstract && method.CciKind == CciMemberKind.Regular && method.ApplyDefaultContract) {
        This thisOb = method.ThisParameter;
        MethodCall thisIsExposable = new MethodCall(
          new MemberBinding(null, SystemTypes.Guard.GetMethod(Identifier.For("FrameIsExposable"),
            SystemTypes.Object, SystemTypes.Type)),
            new ExpressionList(thisOb, new UnaryExpression(new Literal(method.DeclaringType, SystemTypes.Type),
            NodeType.Typeof, OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Type))), NodeType.Call,
            SystemTypes.Boolean, method.Body.SourceContext);
        Assumption assumption = new Assumption(thisIsExposable);
        Expose expose = new Expose(NodeType.Write);
        expose.SourceContext = method.Body.SourceContext;
        expose.Instance = thisOb;
        expose.Body = method.Body;
        if (this.currentOptions != null && this.currentOptions.DisableGuardedClassesChecks)
          method.Body = new Block(new StatementList(assumption, expose));
        else
          method.Body = new Block(new StatementList(expose));
      }


      #region Check contract rules for all interface methods and base methods this method implements/overrides
      bool ok = true;
      if (method.IsVirtual && !method.IsCompilerControlled) {
        // use FindNearest..., can't rely on method.OverriddenMethod since it might not be set further up the chain
        Method overridden = method.DeclaringType.FindNearestOverriddenMethod(method);
        if (overridden != null) {
          ok &= this.CheckContractRules(overridden, method, method.DeclaringType);
        }
        for (int i = 0, n = method.ImplementedInterfaceMethods == null ? 0 : method.ImplementedInterfaceMethods.Count; i < n; i++) {
          Method ifaceMethod = method.ImplementedInterfaceMethods[i];
          ok &= this.CheckContractRules(ifaceMethod, method, method.DeclaringType);
        }
        for (int i = 0, n = method.ImplicitlyImplementedInterfaceMethods == null ? 0 : method.ImplicitlyImplementedInterfaceMethods.Count; i < n; i++) {
          Method ifaceMethod = method.ImplicitlyImplementedInterfaceMethods[i];
          ok &= this.CheckContractRules(ifaceMethod, method, method.DeclaringType);
        }
      }
      #endregion

      #region Contract Inheritance for method overrides and interface implementations (do this somewhere else?)
      // This needs to be done here (and not in VisitMethodContract) because method might not even have a contract
      if (method.IsVirtual && ok && !method.IsCompilerControlled) {
        // use FindNearest..., can't rely on method.OverriddenMethod since it might not be set further up the chain
        Method overridden = method.DeclaringType.FindNearestOverriddenMethod(method);
        // FindNearestOverriddenMethod doesn't care if method is "new" or an "override", so explicity test IsVirtual property
        MethodContract cumulativeContract = method.Contract == null ? new MethodContract(method) : method.Contract;
        bool somethingWasCopied = false;
        while (overridden != null && overridden.IsVirtual) {
          if (overridden.Contract != null) {
            cumulativeContract.CopyFrom(overridden.Contract);
            somethingWasCopied = true;
            break;
          }
          overridden = overridden.DeclaringType.FindNearestOverriddenMethod(overridden);
        }
        // Can inherit from at most one interface method
        bool ifaceContractWasCopied = false;
        for (int i = 0, n = method.ImplementedInterfaceMethods == null ? 0 : method.ImplementedInterfaceMethods.Count; i < n; i++) {
          Method ifaceMethod = method.ImplementedInterfaceMethods[i];
          if (ifaceMethod == null) continue;
          if (ifaceMethod.Contract != null) {
            if (ifaceContractWasCopied) {
              this.HandleError(method, Error.RequiresNotAllowedInInterfaceImplementation, this.GetMethodSignature(ifaceMethod));
              break;
            }
            cumulativeContract.CopyFrom(ifaceMethod.Contract);
            somethingWasCopied = true;
            ifaceContractWasCopied = true;
          }
        }
        for (int i = 0, n = method.ImplicitlyImplementedInterfaceMethods == null ? 0 : method.ImplicitlyImplementedInterfaceMethods.Count; i < n; i++) {
          Method ifaceMethod = method.ImplicitlyImplementedInterfaceMethods[i];
          if (ifaceMethod == null) continue;
          if (ifaceMethod.Contract != null) {
            if (ifaceContractWasCopied) {
              this.HandleError(method, Error.RequiresNotAllowedInInterfaceImplementation, this.GetMethodSignature(ifaceMethod));
              break;
            }
            cumulativeContract.CopyFrom(ifaceMethod.Contract);
            somethingWasCopied = true;
            ifaceContractWasCopied = true;
          }
        }
        if (method.Contract == null && somethingWasCopied) { // otherwise it was already copied into the method's contract 
          method.Contract = cumulativeContract;
        }
      }
      #endregion
      
      // For checked exceptions, the actual exceptions thrown must be a subset of the allowed exceptions
      TypeNodeList aes = new TypeNodeList();
      if (method.Contract != null && method.Contract.Ensures != null) {
        for (int i = 0, n = method.Contract.Ensures.Count; i < n; i++) {
          EnsuresExceptional ee = method.Contract.Ensures[i] as EnsuresExceptional;
          if (ee == null || ee.Inherited) continue;
          aes.Add(ee.Type);
        }
      }
      TypeNodeList saveAllowedExceptions = this.allowedExceptions;
      this.allowedExceptions = aes;
      // don't check method body of proxy methods.
      Method result = (method is ProxyMethod) ? method : base.VisitMethod(method);
      this.allowedExceptions = saveAllowedExceptions;


      if (this.yieldNode != null && TypeNode.StripModifiers(method.ReturnType) is Interface) {
        StatementList statements = new StatementList(1);
        TypeNode elementType = SystemTypes.Object;
        Interface stype = (Interface)TypeNode.StripModifiers(method.ReturnType);
        if (stype.TemplateArguments != null && stype.TemplateArguments.Count == 1) elementType = stype.TemplateArguments[0];
        Class state = scope.ClosureClass;
        elementType = scope.FixTypeReference(elementType);
        state.Flags |= TypeFlags.Abstract; //So that no complaints are given about missing methods added by Normalizer
        state.Interfaces = new InterfaceList(5);
        state.Interfaces.Add(SystemTypes.IEnumerable);
        state.Interfaces.Add((Interface)SystemTypes.GenericIEnumerator.GetTemplateInstance(this.currentType, elementType));
        state.Interfaces.Add(SystemTypes.IEnumerator);
        state.Interfaces.Add(SystemTypes.IDisposable);
        state.Interfaces.Add((Interface)SystemTypes.GenericIEnumerable.GetTemplateInstance(this.currentType, elementType));
        //Add these methods so that Normalizer can find them even when reference to iterator is forward
        Method moveNext = new Method(state, null, StandardIds.MoveNext, null, SystemTypes.Boolean, null);
        moveNext.CallingConvention = CallingConventionFlags.HasThis;
        moveNext.Flags = MethodFlags.Public|MethodFlags.Virtual;
        moveNext.Body = new Block(new StatementList());
        state.Members.Add(moveNext);
        Method getCurrent = new Method(state, null, StandardIds.getCurrent, null, elementType, null);
        getCurrent.CallingConvention = CallingConventionFlags.HasThis;
        getCurrent.Flags = MethodFlags.Public|MethodFlags.Virtual|MethodFlags.SpecialName;
        getCurrent.Body = new Block(new StatementList());
        state.Members.Add(getCurrent);
        Return ret = new Return(new ConstructIterator(state, method.Body, elementType, state));
        if (method.Name.SourceContext.Document != null) {
          ret.SourceContext = method.SourceContext;
          ret.SourceContext.EndPos = method.Name.SourceContext.EndPos;
          Debug.Assert(ret.SourceContext.EndPos >= ret.SourceContext.StartPos);
        }
        statements.Add(ret);
        method.Body = new Block(statements);
        method.Body.Scope = new BlockScope(scope, method.Body);
      }
      if (method.IsStatic && method.IsVirtual && !method.IsSpecialName) {
        method.Flags &= ~MethodFlags.Static;
        this.HandleError(method.Name, Error.StaticNotVirtual, this.GetMethodSignature(method));
      }
      if (!method.OverridesBaseClassMember) {
        if (method.NodeType == NodeType.InstanceInitializer || !method.IsSpecialName) {
          if (!(method.DeclaringType is Interface) && !(method.DeclaringType is DelegateNode)) {
            if (this.IsLessAccessible(method.ReturnType, method)) {
              this.HandleError(method.Name, Error.ReturnTypeLessAccessibleThanMethod, this.GetTypeName(method.ReturnType), this.GetMethodSignature(method));
              this.HandleRelatedError(method.ReturnType);
            }
            this.CheckParameterTypeAccessibility(method.Parameters, method);
          }
        } else {
          if (method.Name != null && Checker.OperatorName[method.Name.UniqueIdKey] != null) {
            if (this.IsLessAccessible(method.ReturnType, method)) {
              this.HandleError(method.Name, Error.ReturnTypeLessAccessibleThanOperator, this.GetTypeName(method.ReturnType), this.GetMethodSignature(method));
              this.HandleRelatedError(method.ReturnType);
            }
            this.CheckParameterTypeAccessibility(method.Parameters, method);
          }
        }
      }

      if (!method.IsSpecialName) {
        TypeNodeList implementedTypes = method.ImplementedTypes;
        if (implementedTypes != null) {
          InterfaceList declaringTypeInterfaces = this.GetTypeView(method.DeclaringType).Interfaces;
          for (int i = 0, n = implementedTypes.Count; i < n; i++) {
            Interface iface = implementedTypes[i] as Interface;
            if (iface == null) continue;
            if (!this.IsAllowedAsImplementedType(declaringTypeInterfaces, iface)) {
              Node offendingNode = method.ImplementedTypeExpressions[i];
              this.HandleError(offendingNode, Error.ContainingTypeDoesNotImplement, this.GetMethodSignature(method), this.GetTypeName(iface));
              this.HandleRelatedError(iface);
              implementedTypes = null;
              break;
            }
          }
        }
        MethodList implementedMethods = method.ImplementedInterfaceMethods;
        for (int i = 0, n = implementedTypes == null ? 0 : implementedTypes.Count; i < n; i++) {
          Interface iface = implementedTypes[i] as Interface;
          if (iface == null) continue;
          Method m = implementedMethods == null ? null : implementedMethods[i];
          if (m == null) {
            this.HandleError(method.Name, Error.InterfaceMemberNotFound, this.GetMemberSignature(method), this.GetTypeName(iface));
            this.HandleRelatedError(iface);
          } else if (m.IsSpecialName)
            this.HandleError(method.Name, Error.CannotExplicitlyImplementAccessor, this.GetMethodSignature(method), this.GetMethodSignature(m));
        }
      }
      if ((method.Flags & MethodFlags.PInvokeImpl) != 0) {
        Error e = Error.None;
        if (this.shadowedAssembly != null) {
          // Make sure this method has a counterpart in the shadowed method
          TypeNode type = this.GetCorrespondingShadowedTypeNode(method.DeclaringType);
          if (type == null) {
            this.HandleError(method.DeclaringType, Error.TypeMissingInShadowedAssembly, this.GetTypeName(method.DeclaringType));
          } else {
            int numParams = method.Parameters == null ? 0 : method.Parameters.Count;
            TypeNode[] types = new TypeNode[numParams];
            for (int i = 0; i < numParams; i++) { types[i] = this.GetCorrespondingShadowedTypeNode(TypeNode.StripModifiers(method.Parameters[i].Type)); }
            if (this.GetTypeView(type).GetMethod(method.Name, types) == null) {
              this.HandleError(method, Error.MethodMissingInShadowedAssembly, this.GetMethodSignature(method));
            }
          }
        } else if (this.isCompilingAContractAssembly)
          e = Error.None;
        else if (method.Body != null)
          e = Error.PInvokeHasBody;
        else if (method.IsAbstract)
          e = Error.AbstractAndExtern;
        else if (method.PInvokeImportName == null || method.PInvokeModule == null) {
          if (method.Attributes == null || method.Attributes.Count == 0)
            e = Error.PInvokeWithoutModuleOrImportName;
          else
            method.Flags &= ~MethodFlags.PInvokeImpl;
        }
        if (e != Error.None)
          this.HandleError(method.Name, e, this.GetMethodSignature(method));
      }
      if (method.IsPropertySetter && (method.IsPure || method.IsConfined || method.IsStateIndependent)) {
        this.HandleError(method, Error.MemberCannotBeAnnotatedAsPure, this.GetMethodSignature(method));
      }
      this.currentFinallyClause = savedCurrentFinallyClause;
      this.currentMethod = savedCurrentMethod;
      this.returnNode = savedReturnNode;
      this.yieldNode = savedYieldNode;
      return result;
    }

    /// <summary>
    /// Need to have this override so that inherited postconditions are not
    /// checked again. 
    /// </summary>
    /// <param name="Ensures"></param>
    /// <returns></returns>
    public override EnsuresList VisitEnsuresList(EnsuresList Ensures) {
      if (Ensures == null) return null;
      for (int i = 0, n = Ensures.Count; i < n; i++) {
        if (Ensures[i] == null || Ensures[i].Inherited) {
          continue;
        }
        Ensures[i] = (Ensures)this.Visit(Ensures[i]);
      }
      return Ensures;
    }
    public override EnsuresNormal VisitEnsuresNormal(EnsuresNormal normal) {
      if (normal == null) return null;
      normal.PostCondition = this.VisitBooleanExpression(normal.PostCondition);
      return normal;
    }

    public override EnsuresExceptional VisitEnsuresExceptional(EnsuresExceptional exceptional) {
      // BUGBUG? How safe is it to assume that the Variable is a NameBinding at this point?
      // But if it isn't what can possibly be done with it? If it isn't resolved to something,
      // the Checker pass will wipe it out.

      NameBinding nb = exceptional.Variable as NameBinding;
      if (nb != null) {
        exceptional.Variable = new Local(nb.Identifier, exceptional.Type);
      }
      exceptional.PostCondition = this.VisitBooleanExpression(exceptional.PostCondition);
      return exceptional;
    }
    public override Expression VisitOldExpression(OldExpression oldExpression) {
      if (oldExpression == null) return null;
      if (insidePureContract && insideEnsures)
        this.HandleError(oldExpression, Error.OldExprInPureEnsures);
      return base.VisitOldExpression(oldExpression);
    }
    public virtual AttributeNode VisitParameterAttribute(AttributeNode attr, Parameter parameter) {
      if (attr == null || parameter == null) return null;
      MemberBinding mb = attr.Constructor as MemberBinding;
      if (mb == null || mb.BoundMember == null) return null;
      if (mb.BoundMember.DeclaringType == SystemTypes.InAttribute) {
        parameter.Flags |= ParameterFlags.In;
        attr.IsPseudoAttribute = true;
        return attr;
      } else if (mb.BoundMember.DeclaringType == SystemTypes.OutAttribute) {
        parameter.Flags |= ParameterFlags.Out;
        attr.IsPseudoAttribute = true;
        return attr;
      } else if (mb.BoundMember.DeclaringType == SystemTypes.OptionalAttribute) {
        parameter.Flags |= ParameterFlags.Optional;
        attr.IsPseudoAttribute = true;
        return attr;
      }
      //TODO: deal with MarshalAs
      return attr;
    }
    public virtual AttributeNode VisitPropertyAttribute(AttributeNode attr, Property property) {
      if (attr == null || property == null) return null;
      ExpressionList args = attr.Expressions;
      MemberBinding mb = attr.Constructor as MemberBinding;
      if (mb == null || mb.BoundMember == null) return null;
      if (mb.BoundMember.DeclaringType == SystemTypes.IndexerNameAttribute) {
        if (property.OverridesBaseClassMember)
          this.HandleError(attr, Error.IndexerNameAttributeOnOverride);
        else if (property.ImplementedTypes != null && property.ImplementedTypes.Count > 0)
          this.HandleError(attr, Error.AttributeOnBadTarget, "IndexerName", "non interface implementation property");
        if (args == null || args.Count < 1) { Debug.Assert(false); return null; }
        Literal lit = args[0] as Literal;
        if (lit == null) return null;
        string indexerName = lit.Value as string;
        if (indexerName == null) {
          this.HandleError(args[0], Error.InvalidAttributeArgument, "IndexerName");
          return null;
        }
        if (!this.IsValidIdentifier(indexerName)) {
          this.HandleError(args[0], Error.IndexerNameNotIdentifier);
          return null;
        }
        Identifier id = new Identifier(indexerName);
        if (this.indexerNames == null) this.indexerNames = new TrivialHashtable();
        Identifier previousName = (Identifier)this.indexerNames[this.currentType.UniqueKey];
        if (previousName == null)
          this.indexerNames[this.currentType.UniqueKey] = id;
        else if (previousName.UniqueIdKey != id.UniqueIdKey) {
          this.HandleError(args[0], Error.InconsistantIndexerNames);
          id = new Identifier(previousName.ToString());
        }
        id.SourceContext = property.Name.SourceContext;
        property.Name = id;
        if (property.Getter != null && property.Getter.Name != null) {
          id = new Identifier("get_" + indexerName);
          id.SourceContext = property.Getter.Name.SourceContext;
          property.Getter.Name = id;
        }
        if (property.Setter != null && property.Setter.Name != null) {
          id = new Identifier("set_" + indexerName);
          id.SourceContext = property.Setter.Name.SourceContext;
          property.Setter.Name = id;
        }
        attr.IsPseudoAttribute = true;
        return attr;
      }
      return attr;
    }
    public virtual bool IsValidIdentifier(string identifier) {
      if (identifier == null || identifier.Length == 0) return false;
      if (Char.IsDigit(identifier, 0)) return false;
      foreach (char c in identifier)
        if (!Char.IsLetterOrDigit(c) && c != '_') return false;
      return true;
    }
    public virtual AttributeNode VisitMethodAttribute(AttributeNode attr, Method method) {
      if (attr == null || method == null) return null;
      ExpressionList args = attr.Expressions;
      MemberBinding mb = attr.Constructor as MemberBinding;
      if (mb == null || mb.BoundMember == null) return null;
      TypeNode attrType = mb.BoundMember.DeclaringType;
      if (attrType == SystemTypes.DllImportAttribute) {
        if ((method.Flags & MethodFlags.PInvokeImpl) == 0 || !method.IsStatic) {
          this.HandleError(attr, Error.DllImportOnInvalidMethod);
          method.Flags |= MethodFlags.PInvokeImpl|MethodFlags.Static;
          method.Body = null;
        }
        if (args == null || args.Count < 1) { Debug.Assert(false); return null; }
        Literal lit = args[0] as Literal;
        if (lit == null) return null;
        string dllName = lit.Value as string;
        if (dllName == null) {
          this.HandleError(args[0], Error.InvalidAttributeArgument, "DllImport");
          return null;
        }
        method.ImplFlags |= MethodImplFlags.PreserveSig;
        method.PInvokeModule = this.GetModuleFor(dllName);
        method.PInvokeFlags = PInvokeFlags.CallConvWinapi|PInvokeFlags.CharSetNotSpec;
        method.PInvokeImportName = method.Name.ToString();
        for (int j = 1, m = args.Count; j < m; j++) {
          NamedArgument nArg = args[j] as NamedArgument;
          if (nArg == null) continue;
          lit = nArg.Value as Literal;
          if (lit == null) continue;
          if (nArg.Name.UniqueIdKey == StandardIds.CallingConvention.UniqueIdKey) {
            if (lit.Value is int) {
              method.PInvokeFlags &= ~PInvokeFlags.CallingConvMask;
              switch ((CallingConvention)(int)lit.Value) {
                case CallingConvention.Cdecl: method.PInvokeFlags |= PInvokeFlags.CallConvCdecl; break;
                case CallingConvention.FastCall: method.PInvokeFlags |= PInvokeFlags.CallConvFastcall; break;
                case CallingConvention.StdCall: method.PInvokeFlags |= PInvokeFlags.CallConvStdcall; break;
                case CallingConvention.ThisCall: method.PInvokeFlags |= PInvokeFlags.CallConvThiscall; break;
                case CallingConvention.Winapi: method.PInvokeFlags |= PInvokeFlags.CallConvWinapi; break;
              }
            }
          } else if (nArg.Name.UniqueIdKey == StandardIds.CharSet.UniqueIdKey) {
            if (lit.Value is int) {
              method.PInvokeFlags &= ~PInvokeFlags.CharSetMask;
              switch ((CharSet)(int)lit.Value) {
                case CharSet.Ansi: method.PInvokeFlags |= PInvokeFlags.CharSetAns; break;
                case CharSet.Auto: method.PInvokeFlags |= PInvokeFlags.CharSetAuto; break;
                case CharSet.None: method.PInvokeFlags |= PInvokeFlags.CharSetNotSpec; break;
                case CharSet.Unicode: method.PInvokeFlags |= PInvokeFlags.CharSetUnicode; break;
              }
            }
          } else if (nArg.Name.UniqueIdKey == StandardIds.EntryPoint.UniqueIdKey) {
            if (lit.Value is string)
              method.PInvokeImportName = (string)lit.Value;
          } else if (nArg.Name.UniqueIdKey == StandardIds.ExactSpelling.UniqueIdKey) {
            if (lit.Value is bool && (bool)lit.Value)
              method.PInvokeFlags |= PInvokeFlags.NoMangle;
          } else if (nArg.Name.UniqueIdKey == StandardIds.SetLastError.UniqueIdKey) {
            if (lit.Value is bool && (bool)lit.Value)
              method.PInvokeFlags |= PInvokeFlags.SupportsLastError;
          } else if (nArg.Name.UniqueIdKey == StandardIds.EntryPoint.UniqueIdKey) {
            if (lit.Value is string)
              method.PInvokeImportName = (string)lit.Value;
          } else if (nArg.Name.UniqueIdKey == StandardIds.PreserveSig.UniqueIdKey) {
            if (lit.Value is bool && !(bool)lit.Value)
              method.ImplFlags &= ~MethodImplFlags.PreserveSig;
          }
          attr.IsPseudoAttribute = true;
        }
        return attr;
      }
      if (attrType == SystemTypes.MethodImplAttribute) {
        if (attr.Expressions != null && attr.Expressions.Count > 0) {
          Literal flags = attr.Expressions[0] as Literal;
          if (flags != null && flags.Value is int) {
            int flagValue = (int)flags.Value;
            flagValue &= 0xFFFF;
            // method.Flags &= ~MethodFlags.PInvokeImpl;
            // method.PInvokeFlags = PInvokeFlags.None;
            // method.ImplFlags |= MethodImplFlags.InternalCall;
            method.ImplFlags |= (MethodImplFlags)flagValue;
          }
        }
        attr.IsPseudoAttribute = true;
        return attr;
      }
      if (attrType == SystemTypes.ConditionalAttribute) {
        if (method.IsSpecialName || (method.HasCompilerGeneratedSignature && method.Name.UniqueIdKey == StandardIds.Finalize.UniqueIdKey)
        || (method.ImplementedTypes != null && method.ImplementedTypes.Count > 0)) {
          this.HandleError(attr, Error.ConditionalOnSpecialMethod, this.GetMethodSignature(method));
          return null;
        }
        if (method.DeclaringType is Interface) {
          this.HandleError(attr, Error.ConditionalOnInterfaceMethod);
          return null;
        }
        if (method.OverridesBaseClassMember) {
          this.HandleError(attr, Error.ConditionalOnOverride, this.GetMethodSignature(method));
          return null;
        }
        if (method.ReturnType != SystemTypes.Void) {
          this.HandleError(attr, Error.ConditionalMustReturnVoid, this.GetMemberSignature(method));
          return null;
        }
        if (args == null || args.Count < 1) { Debug.Assert(false); return null; }
        Literal lit = args[0] as Literal;
        if (lit == null) return null;
        string conditionString = lit.Value as string;
        if (conditionString == null) {
          this.HandleError(args[0], Error.InvalidAttributeArgument, "Conditional");
          return null;
        }
      }
      return attr;
    }
    public virtual Module GetModuleFor(string modName) {
      if (this.currentModule == null) return null;
      ModuleReferenceList rMods = this.currentModule.ModuleReferences;
      ModuleReference rMod = null;
      if (rMods == null)
        rMods = this.currentModule.ModuleReferences = new ModuleReferenceList();
      else {
        for (int i = 0, n = rMods.Count; i < n; i++) {
          rMod = rMods[i];
          if (rMod == null) continue;
          if (rMod.Name == modName) return rMod.Module;
        }
      }
      Module mod = new Module();
      mod.Name = modName;
      mod.Kind = ModuleKindFlags.UnmanagedDynamicallyLinkedLibrary;
      rMod = new ModuleReference(modName, mod);
      rMods.Add(rMod);
      return mod;
    }
    public override Expression VisitMethodCall(MethodCall call) {
      if (call == null) return null;
      NameBinding nb = call.Callee as NameBinding;
      if (nb != null) {      
        if (nb.Identifier != Looker.NotFound) {
          this.VisitNameBinding(nb); //Reports the error
          return null;
        }
        int numPars = call.Operands == null ? 0 : call.Operands.Count;
        bool callOperandsAlreadyNull = (call.Operands == null);
        ExpressionList operands = call.Operands = this.VisitExpressionList(call.Operands);
        if (operands == null && !callOperandsAlreadyNull || operands != null && operands.Count != numPars) return null;
        for (int i = 0; i < numPars; i++)
          if (call.Operands[i] == null) return null; //The error could have been caused by the bad argument.
        bool hasArglist = false;
        int arglistIndex = -1;

        for (int i = 0; i < numPars; i++) {
          if (call.Operands[i] is ArglistArgumentExpression) {
            hasArglist = true;
            arglistIndex = i;
            break;
          }
        }

        MemberList members = nb.BoundMembers;
        int n = members == null ? 0 : members.Count;
        MethodList ambiguousMethods = new MethodList(n);
        Method inaccessibleMethod = null;
        Method templateMethodWithFailedInference = null;
        Method objectMatchArglistMethod = null;
        for (int i = 0; i < n; i++) {
          Method meth = members[i] as Method;
          if (meth == null) continue;
          if ((meth.Parameters == null ? 0 : meth.Parameters.Count) != numPars) continue;
          if (this.NotAccessible(meth)) {
            inaccessibleMethod = meth;
            continue;
          } else {
            string mname = meth.Name != null ? meth.Name.ToString() :
        "";
            if (this.insideMethodContract  || this.insideInvariant) {
              if (!this.AsAccessible(meth, currentMethod) && !this.insideInvariant) {
                this.HandleError(meth, Error.MemberNotVisible, this.GetTypeName(meth.DeclaringType)+"."+mname); // WS better error message
                return null;
              }
              if ((this.insideMethodContract || this.insideAssertOrAssume) && !this.IsTransparentForMethodContract(meth, currentMethod)) {
                this.HandleError(meth, Error.MemberMustBePureForMethodContract, this.GetTypeName(meth.DeclaringType)+"."+mname);
                return null;
              }
              if (this.insideInvariant && !this.IsTransparentForInvariant(meth, currentMethod)) {
                this.HandleError(meth, Error.MemberMustBePureForInvariant, this.GetTypeName(meth.DeclaringType)+"."+mname);
                return null;
              }
            } else if (!this.IsModelUseOk(meth, currentMethod)) {
              this.HandleError(meth, Error.ModelMemberUseNotAllowedInContext, this.GetTypeName(meth.DeclaringType)+"."+mname);
              return null;
            }
          }
          if (meth.TemplateParameters != null && meth.TemplateParameters.Count > 0) {
            templateMethodWithFailedInference = meth;
            continue;
          }
          if (hasArglist && arglistIndex < meth.Parameters.Count && meth.Parameters[arglistIndex].Type == SystemTypes.Object) {
            objectMatchArglistMethod = meth;
          }
          ambiguousMethods.Add(meth);
        }
        if (ambiguousMethods.Count >= 2) {
          this.HandleError(call, Error.AmbiguousCall, this.GetMethodSignature(ambiguousMethods[0]), this.GetMethodSignature(ambiguousMethods[1]));
          return null;
        }
        if (inaccessibleMethod != null) {
          this.HandleError(call, Error.MemberNotVisible, this.GetMethodSignature(inaccessibleMethod));
          return null;
        }
        if (templateMethodWithFailedInference != null) {
          this.HandleError(call, Error.CannotInferMethTypeArgs, this.GetMethodSignature(templateMethodWithFailedInference));
          return null;
        }
        if (objectMatchArglistMethod != null) {
          this.HandleError(call, Error.CannotMatchArglist, objectMatchArglistMethod.Name.Name);
          this.HandleRelatedError(objectMatchArglistMethod);
          return null;
        }
        Identifier id = null;
        for (int i = 0; i < n; i++) {
          Member mem = members[i];
          if (!(mem is Method)) continue;
          if (id == null && mem.Name != null) {
            id = mem.Name;
            string name = id.Name;
            if (mem is InstanceInitializer && mem.DeclaringType != null && mem.DeclaringType.Name != null)
              name = mem.DeclaringType.Name.Name;
            else if (mem.IsSpecialName)
              name = this.GetMemberName(mem);
            this.HandleError(nb, Error.NoOverloadWithMatchingArgumentCount, name, (call.Operands == null ? 0 : call.Operands.Count).ToString());
          }
          this.HandleRelatedError(mem);
        }
        return null;
      }
      MemberBinding mb = call.Callee as MemberBinding;
      if (mb == null) {
        Expression e = this.VisitExpression(call.Callee);
        if (e != null) {
          if (e is Literal && e.Type == SystemTypes.Type)
            this.HandleError(e, Error.TypeInBadContext, this.GetTypeName((TypeNode)((Literal)e).Value));
          else {
            TemplateInstance templInst = e as TemplateInstance;
            if (templInst != null) {
              if (templInst.Expression == null || templInst.TypeArguments == null) return null;
              this.VisitExpression(templInst.Expression);
              return null;
            }
            //HS D
            if (call.Callee is Hole)
               return null;
            this.HandleError(e, Error.NoSuchMethod);
          }
        }
        return null;
      }
      Method method = mb.BoundMember as Method;
      if (method == null) {
        if (mb.BoundMember != null) {
          if (mb.BoundMember is Event)
            this.HandleError(mb, Error.BadCallToEventHandler, this.GetMemberSignature(mb.BoundMember));
          else
            this.HandleNonMethodWhereMethodExpectedError(mb, mb.BoundMember);
        }
        return null;
      }
      bool initialMayReferenceThisAndBase = this.MayReferenceThisAndBase;
      if (mb.TargetObject is ImplicitThis || mb.TargetObject is This || mb.TargetObject is Base) {
        if (method is InstanceInitializer) {
          this.MayReferenceThisAndBase = true;  //Base class constructor has been called
        } else if ((this.currentMethod == null && !this.insideModelfield // this is true when it is visited as part of a field initializer
                    || !this.MayReferenceThisAndBase) // this is true when it is visited as part of a ctor with an explicit call to base in the body
                   && !this.AllowThisTarget(method) && !this.insideInvariant) {
          if (mb.TargetObject is ImplicitThis) {
            this.HandleError(mb, Error.ObjectRequired, this.GetMethodSignature(method, true));
          } else if (mb.TargetObject is Base) {
            this.HandleError(mb.TargetObject, Error.BaseInBadContext);
          } else {
            this.HandleError(mb.TargetObject, Error.ThisInBadContext);
          }
          return null;
        }
      }
      if (method == Resolver.IndexerNotFound) {
        if (mb.TargetObject == null) return null;
        if (this.TypeInVariableContext(mb.TargetObject as Literal)) return null;
        Expression tgtObj = this.VisitExpression(mb.TargetObject);
        if (tgtObj == null) return null;
        this.HandleError(tgtObj, Error.NotIndexable, this.GetTypeName(mb.TargetObject.Type));
        return null;
      }
      if (method.Name == Looker.NotFound) {
        //TODO: report the error
        return null;
      }
      TypeNode obType = mb.TargetObject == null ? null : mb.TargetObject.Type;
      if (mb.TargetObject is Base) obType = this.currentType;
      obType = TypeNode.StripModifiers(obType);
      if (this.NotAccessible(method, ref obType)) {
        if (method.IsSpecialName && method.Parameters != null && method.Parameters.Count > 0 && method.Name.ToString().StartsWith("get_"))
          this.HandleError(call, Error.NotIndexable, this.GetTypeName(mb.TargetObject.Type));
        else if (mb.TargetObject == null || mb.TargetObject.Type == null || this.NotAccessible(method))
          this.HandleError(call, Error.MemberNotVisible, this.GetMethodSignature(method));
        else
          this.HandleError(mb.BoundMemberExpression == null ? mb : mb.BoundMemberExpression, Error.NotVisibleViaBaseType, this.GetMethodSignature(method),
            this.GetTypeName(mb.TargetObject.Type), this.GetTypeName(this.currentType));
        return null;
      } else {
        if (this.insideMethodContract || this.insideInvariant || this.insideAssertOrAssume) {
          string mname = method.Name == null ? "" : method.Name.ToString();
          if (!this.AsAccessible(method, currentMethod) && !this.insideInvariant && !this.insideMethodContract && !this.insideAssertOrAssume) {
            this.HandleError(mb, Error.MemberNotVisible, this.GetTypeName(method.DeclaringType)+"."+mname);
            return null;
          }
          if ((this.insideMethodContract || this.insideAssertOrAssume) && !this.IsTransparentForMethodContract(method, currentMethod)) {
            this.HandleError(mb, Error.MemberMustBePureForMethodContract, this.GetTypeName(method.DeclaringType)+"."+mname);
            return null;
          }
          if (this.insideInvariant && !this.IsTransparentForInvariant(method, currentMethod)) {
            this.HandleError(mb, Error.MemberMustBePureForInvariant, this.GetTypeName(method.DeclaringType)+"."+mname);
            return null;
          }
        } else if (!this.IsModelUseOk(method, currentMethod)) {
          string mname = method.Name == null ? "" : method.Name.ToString();
          this.HandleError(mb, Error.ModelMemberUseNotAllowedInContext, this.GetTypeName(method.DeclaringType)+"."+mname);
          return null;
        }
      }
      this.CheckDirectCallOfFinalize(method, call);
      if (method == this.currentMethod && method is InstanceInitializer)
      {
        this.HandleError(call, Error.RecursiveConstructorCall, this.GetMethodSignature(method));
        return null;
      }
      if (method.IsStatic) {
        Expression targetOb = mb.TargetObject;
        if (targetOb != null) {
          MemberBinding memb = targetOb as MemberBinding;
          if (memb == null || memb.BoundMember == null || memb.Type == null || memb.Type.Name == null ||
            memb.BoundMember.Name.UniqueIdKey != memb.Type.Name.UniqueIdKey) {
            MethodCall mcall = targetOb as MethodCall;
            memb = mcall == null ? null : mcall.Callee as MemberBinding;
            TypeNode mcallType = mcall == null ? null : TypeNode.StripModifiers(mcall.Type);
            if (memb == null || memb.BoundMember == null || !memb.BoundMember.IsSpecialName || mcallType == null ||
              mcallType.Name == null || memb.BoundMember.Name.ToString() != "get_"+mcallType.Name.ToString()) {
              targetOb = this.VisitExpression(targetOb);
              if (targetOb != null)
                this.HandleError(targetOb, Error.TypeNameRequired, this.GetMethodSignature(method));
            }
          }
          mb.TargetObject = targetOb = null;
        }
      } else {
        bool targetObIsNonNull = false;
        bool outerMayReferenceThisAndBase = this.MayReferenceThisAndBase;
        if (this.AllowThisTarget(method)) this.MayReferenceThisAndBase = true;
        Expression targetOb = mb.TargetObject = this.VisitExpression(mb.TargetObject);
        this.MayReferenceThisAndBase = outerMayReferenceThisAndBase;
        if (targetOb != null)
          targetObIsNonNull = this.typeSystem.IsNonNullType(targetOb.Type);
        if (targetOb is Literal && targetOb.Type == SystemTypes.Type) {
          this.HandleError(mb, Error.ObjectRequired, this.GetMethodSignature(method, true));
          return null;
        }
        if (this.currentMethod != null && this.currentMethod.IsStatic) {
          if (targetOb is ImplicitThis || targetOb is This || targetOb is Base) {
            ImplicitThis ithis = targetOb as ImplicitThis;
            if (ithis != null) {
              bool callToNestedFunction = false;
              if (this.currentMethod.Scope.CapturedForClosure) {
                TypeNode t = this.currentMethod.Scope.ClosureClass;
                while (t != null && !callToNestedFunction) {
                  callToNestedFunction = t == method.DeclaringType;
                  t = t.BaseType;
                }
              }
              if (!callToNestedFunction) ithis = null;
            }
            if (ithis == null) {
              this.HandleError(mb, Error.ObjectRequired, this.GetMethodSignature(method));
              return null;
            }
            targetOb = new CurrentClosure(this.currentMethod, this.currentMethod.DeclaringType);
          }
        }
        TypeNode tObType = method.DeclaringType;
        if (tObType == null) return null;
        if (tObType.TemplateParameters != null && tObType.Template == null)
          tObType = tObType.GetTemplateInstance(tObType, tObType.TemplateParameters);
        if ((method.CallingConvention & CallingConventionFlags.ExplicitThis) != 0) {
          if (method.Parameters == null || method.Parameters.Count < 1 || method.Parameters[0] == null)
            Debug.Assert(false);
          else
            tObType = method.Parameters[0].Type;
        }
        if (tObType != null) {
          if (targetOb == null) return null;
          if (tObType.IsValueType) {
            targetOb = this.ConvertValueTypeCallTarget(targetOb, method, tObType);
            if (targetOb == null) return null;
            mb.TargetObject = this.GetAddressOf(targetOb, tObType);
          } else if (targetOb is Base && this.currentType != null && this.currentType.IsValueType) {
            targetOb = new AddressDereference(targetOb, this.currentType);
            mb.TargetObject = new BinaryExpression(targetOb, new MemberBinding(null, this.currentType), NodeType.Box, tObType);
          } else {
            if (tObType != targetOb.Type && !(targetOb is CurrentClosure && tObType is ClosureClass) && !(targetOb is This || targetOb is ImplicitThis)) {
              if (this.useGenerics && targetOb.Type is ITypeParameter && method.IsVirtual) {
                call.Constraint = targetOb.Type;
                if (targetOb.NodeType == NodeType.Indexer)
                  targetOb = mb.TargetObject = new UnaryExpression(targetOb, NodeType.ReadOnlyAddressOf, targetOb.Type.GetReferenceType());
                else
                  targetOb = mb.TargetObject = this.GetAddressOf(targetOb, targetOb.Type);
              } else {
                Reference refType = targetOb.Type as Reference;
                if (this.useGenerics && refType != null && refType.ElementType is ITypeParameter)
                  call.Constraint = refType.ElementType;
                else
                  targetOb = mb.TargetObject = this.typeSystem.ExplicitCoercion(targetOb, tObType, this.TypeViewer);
              }
              if (targetOb == null) return null;
            } else if (targetOb.Type != null && !this.GetTypeView(targetOb.Type).IsAssignableTo(tObType) && targetOb.Type.IsNestedIn(tObType)) {
              this.HandleError(mb, Error.AccessToNonStaticOuterMember, this.GetTypeName(tObType), this.GetTypeName(targetOb.Type));
              return null;
            }
          }
        }
      }
      bool savedMayReferenceThisAndBase = this.MayReferenceThisAndBase;
      this.MayReferenceThisAndBase = initialMayReferenceThisAndBase;
      this.CoerceArguments(method.Parameters, ref call.Operands, this.DoNotVisitArguments(method), method.CallingConvention);
      this.MayReferenceThisAndBase = savedMayReferenceThisAndBase;
      this.CheckForObsolesence(call, method);
      if (call.GiveErrorIfSpecialNameMethod && method.IsSpecialName) {
        string mname = method.Name == null ? null : method.Name.Name;
        if (mname != null && (mname.StartsWith(".") || mname.StartsWith("get_") || mname.StartsWith("set_") || mname.StartsWith("add_") || mname.StartsWith("remove_")))
          this.HandleError(call, Error.CannotCallSpecialMethod, this.GetMethodSignature(method));
      }
      if (method.IsAbstract && call.NodeType != NodeType.Callvirt) {
        Debug.Assert(mb.TargetObject is Base);
        this.HandleError(call, Error.AbstractBaseCall, this.GetMethodSignature(method));
      }
      if (mb != null) {
        if (method != null && method.Contract != null && method.Contract.Ensures != null) {
          // Look at any checked exceptions listed in m's contract and make sure they
          // are covered by the list of allowedExceptions
          for (int i = 0, n = method.Contract.Ensures.Count; i < n; i++) {
            EnsuresExceptional ee = method.Contract.Ensures[i] as EnsuresExceptional;
            if (ee == null)
              continue;
            TypeNode thrownType = ee.Type;
            if (thrownType == null) continue;
            if (!this.GetTypeView(thrownType).IsAssignableTo(SystemTypes.ICheckedException))
              continue;
            int j = 0;
            int p = this.allowedExceptions.Count;
            while (j < p) {
              TypeNode t = this.allowedExceptions[j];
              if (this.GetTypeView(thrownType).IsAssignableTo(t))
                break;
              j++;
            }
            if (j == p) { // no allowed exception covers this one
              this.HandleError(call, Error.CheckedExceptionNotInThrowsClause, this.GetTypeName(thrownType), this.GetMethodSignature(this.currentMethod));
            }
          }
        }
      }
      int numTemplArgs = method.TemplateArguments == null ? 0 : method.TemplateArguments.Count;
      if (method != null && numTemplArgs > 0) {
        // check instantiation constraints
        int len = method.Template.TemplateParameters == null ? 0 : method.Template.TemplateParameters.Count;
        if (numTemplArgs != len) {
          this.HandleError(call, Error.TemplateTypeRequiresArgs, this.GetMethodSignature(method.Template), len.ToString(), "method");
          return call;
        }
        Specializer specializer = len == 0 ? null : new Specializer(method.DeclaringType.DeclaringModule, method.Template.TemplateParameters, method.TemplateArguments);
        if (specializer != null) specializer.CurrentType = this.currentType;
        for (int i=0; i < len; i++) {
          TypeNode formal = method.Template.TemplateParameters[i];
          ITypeParameter formaltp = (ITypeParameter)formal;
          TypeNode actual = method.TemplateArguments[i];
          if (formal == null || actual == null) continue;
          // make sure actual is assignable to base of formal and to each interface
          TypeNode fbaseType = specializer == null ? formal.BaseType : specializer.VisitTypeReference(formal.BaseType);
          if (fbaseType != null && (
                ((formaltp.TypeParameterFlags & TypeParameterFlags.ReferenceTypeConstraint) == TypeParameterFlags.ReferenceTypeConstraint && !actual.IsObjectReferenceType)
                || ((formaltp.TypeParameterFlags & TypeParameterFlags.ValueTypeConstraint) == TypeParameterFlags.ValueTypeConstraint && !actual.IsValueType)
                || !this.typeSystem.AssignmentCompatible(TypeNode.StripModifiers(actual), fbaseType, this.TypeViewer))) {
            Node offNode = call.CalleeExpression is TemplateInstance ? (Node)((TemplateInstance)call.CalleeExpression).TypeArgumentExpressions[i] : (Node)call.CalleeExpression;
            this.HandleError(offNode,
              Error.TypeParameterNotCompatibleWithConstraint, this.GetTypeName(actual),
              this.GetTypeName(fbaseType),
              this.GetTypeName(formal),
              this.GetMethodSignature(method.Template));
            return call;
          }
          InterfaceList formal_ifaces = this.GetTypeView(formal).Interfaces;
          if (formal_ifaces != null) {
            for (int j = 0, n = formal_ifaces.Count; j < n; j++) {
              TypeNode intf = specializer == null ? formal_ifaces[j] : specializer.VisitTypeReference(formal_ifaces[j]);
              intf = TypeNode.StripModifiers(intf);
              if (intf == null) continue;
              if (intf != SystemTypes.ITemplateParameter && !this.typeSystem.AssignmentCompatible(TypeNode.StripModifiers(actual), intf, this.TypeViewer)) {
                Node offNode = call.CalleeExpression is TemplateInstance ? (Node)((TemplateInstance)call.CalleeExpression).TypeArgumentExpressions[i] : (Node)call.CalleeExpression;
                this.HandleError(offNode,
                  Error.TypeParameterNotCompatibleWithConstraint, this.GetTypeName(actual),
                  this.GetTypeName(intf),
                  this.GetTypeName(formal),
                  this.GetMethodSignature(method.Template));
                return call;
              }
            }
          }
        }
        this.CheckGenericMethodSpecialConstraints(method, call);
      }

      return call;
    }

    private Expression GetAddressOf(Expression targetOb, TypeNode tObType) {
      MemberBinding tgtmb = targetOb as MemberBinding;
      if (tgtmb != null && tgtmb.BoundMember is Field && ((Field)tgtmb.BoundMember).IsInitOnly) {
        StatementList stats = new StatementList(2);
        Local loc = new Local(targetOb.Type);
        stats.Add(new AssignmentStatement(loc, targetOb));
        stats.Add(new ExpressionStatement(new UnaryExpression(loc, NodeType.AddressOf, tObType.GetReferenceType())));
        return new BlockExpression(new Block(stats));
      } else
        return new UnaryExpression(targetOb, NodeType.AddressOf, tObType.GetReferenceType(), targetOb.SourceContext);
    }

    // an extension point
    protected virtual bool AllowThisTarget(Method method) {
      return false;
    }
    protected virtual void CheckDirectCallOfFinalize(Method method, MethodCall call) {
      if (method.Name.UniqueIdKey == StandardIds.Finalize.UniqueIdKey && !method.IsStatic && (method.HasCompilerGeneratedSignature || method.DeclaringType == SystemTypes.Object))
        this.HandleDirectCallOfFinalize(call);
    }
    protected virtual void CheckGenericMethodSpecialConstraints(Method method, MethodCall call)
    {
      Method template = method.Template;
      if (template != null && template.TemplateParameters != null && method.TemplateArguments != null && method.TemplateArguments.Count == template.TemplateParameters.Count)
      {
        TemplateInstance ti = call.CalleeExpression as TemplateInstance;
        for (int i = 0, n = method.TemplateArguments.Count; i < n; i++)
        {
          TypeNode arg = method.TemplateArguments[i];
          if (arg == null) return;
          ITypeParameter tpar = template.TemplateParameters[i] as ITypeParameter;
          if (tpar == null) return;
          switch (tpar.TypeParameterFlags & TypeParameterFlags.SpecialConstraintMask)
          {
            case TypeParameterFlags.DefaultConstructorConstraint:
              if (this.GetTypeView(arg).GetConstructors().Count > 0 && this.GetTypeView(arg).GetConstructor() == null)
              {
                Node errorNode = (ti != null) ? ti.TypeArgumentExpressions[i] : method.TemplateArguments[i];
                this.HandleError(errorNode, Error.DefaultContructorConstraintNotSatisfied,
                  this.GetTypeName(arg), method.TemplateParameters[i].Name.ToString(), this.GetMemberName(template));
                this.HandleRelatedError(template);
                this.HandleRelatedError(arg);
                return;
              }
              break;
            case TypeParameterFlags.ReferenceTypeConstraint:
              if (arg.IsValueType)
              {
                Node errorNode = (ti != null) ? ti.TypeArgumentExpressions[i] : method.TemplateArguments[i];
                this.HandleError(errorNode, Error.RefConstraintNotSatisfied,
                  this.GetTypeName(arg), template.TemplateParameters[i].Name.ToString(), this.GetMemberName(template));
                this.HandleRelatedError(template);
                this.HandleRelatedError(arg);
                return;
              }
              break;
            case TypeParameterFlags.ValueTypeConstraint:
              if (!arg.IsValueType)
              {
                Node errorNode = (ti != null) ? ti.TypeArgumentExpressions[i] : method.TemplateArguments[i];
                this.HandleError(errorNode, Error.ValConstraintNotSatisfied,
                  this.GetTypeName(arg), template.TemplateParameters[i].Name.ToString(), this.GetMemberName(template));
                this.HandleRelatedError(template);
                this.HandleRelatedError(arg);
                return;
              }
              if (tpar.IsPointerFree && !arg.IsPointerFree)
              {
                Node errorNode = (ti != null) ? ti.TypeArgumentExpressions[i] : method.TemplateArguments[i];
                this.HandleError(errorNode, Error.PointerFreeConstraintNotSatisfied,
                  this.GetTypeName(arg), template.TemplateParameters[i].Name.ToString(), this.GetMemberName(template));
                this.HandleRelatedError(template);
                this.HandleRelatedError(arg);
                return;
              }
              if (tpar.IsUnmanaged && !arg.IsUnmanaged)
              {
                Node errorNode = (ti != null) ? ti.TypeArgumentExpressions[i] : method.TemplateArguments[i];
                this.HandleError(errorNode, Error.UnmanagedConstraintNotSatisfied,
                  this.GetTypeName(arg), template.TemplateParameters[i].Name.ToString(), this.GetMemberName(template));
                this.HandleRelatedError(template);
                this.HandleRelatedError(arg);
              }
              break;
          }
        }
      }
    }
    protected virtual Expression ConvertValueTypeCallTarget(Expression targetOb, Method method, TypeNode tObType)
    {
      if (tObType != targetOb.Type && tObType == this.typeSystem.Unwrap(targetOb.Type))
        targetOb = this.typeSystem.ExplicitCoercion(targetOb, tObType, this.TypeViewer);
      return targetOb;
    }
    /// <summary>
    /// <para>
    /// Check to make sure that the throws set specified in a subtype (method override or interface
    /// implementation) is a subset of the throws set specified in the supertype (overridden method
    /// or interface method).
    /// </para>
    /// <para>Calls HandleError if the rules are violated.</para>
    /// </summary>
    /// <param name="superTypeMethod">Virtual method from a supertype or interface method</param>
    /// <param name="subTypeMethod">Override of virtual method or implementation of interface method</param>
    /// <param name="type">The type at which the <paramref name="subTypeMethod"/> is being used as an implementation
    /// of <paramref name="superTypeMethod"/>.
    /// This is different from the declaring type of the <paramref name="subTypeMethod"/>
    /// when a type inherits a method that is hijacked as the
    /// implementation of an abstract (interface) method.</param>
    /// <returns>True iff all contract rules between the pair are satisfied.</returns>
    public virtual bool CheckThrows (Method superTypeMethod, Method subTypeMethod, TypeNode type) {
      bool result = true;
      if (subTypeMethod.Contract == null || subTypeMethod.Contract.Ensures == null || !(subTypeMethod.Contract.Ensures.Count > 0))
        // empty set \subset X, \forall X
        return result;
      for (int i = 0, n = subTypeMethod.Contract.Ensures.Count; i < n; i++) {
        EnsuresExceptional subThrows = subTypeMethod.Contract.Ensures[i] as EnsuresExceptional;
        if (subThrows == null) continue;
        // so it is "throws T ensures R", make sure T <: S for some S in the (inherited) throws set
        bool found = false;
        for (int j = 0,
               m = (superTypeMethod.Contract == null || superTypeMethod.Contract.Ensures == null) ?
               0 : superTypeMethod.Contract.Ensures.Count;
          j < m && !found;
          j++) {
          EnsuresExceptional superThrows = superTypeMethod.Contract.Ensures[j] as EnsuresExceptional;
          if (superThrows == null) continue;
          if (this.GetTypeView(subThrows.Type).IsAssignableTo(superThrows.Type)) {
            found = true;
          }
        }
        if (!found) {
          subTypeMethod.Contract.Ensures[i] = null; // so no further errors relative to this are issued
          this.HandleError(subThrows, Error.CannotWeakenThrowsSet, this.GetMethodSignature(subTypeMethod), this.GetTypeName(subThrows.Type));
          result = false;
        }
      }
      return result;
    }
    /// <summary>
    /// <para>
    /// Check contract rules between a "supertype" method (interface method or virtual method in a
    /// super type) and a "subtype" method (interface method implementation or method override).
    /// Note that either method may not have a Contract.
    /// </para>
    /// <para>This is called from VisitMethod for all such pairs, regardless of whether the
    /// methods have a contract or not.
    /// </para>
    /// </summary>
    /// <param name="superTypeMethod">Virtual method from a supertype or interface method</param>
    /// <param name="subTypeMethod">Override of virtual method or implementation of interface method</param>
    /// <param name="type">The type at which the <paramref name="subTypeMethod"/> is being used as an implementation
    /// of <paramref name="superTypeMethod"/>.
    /// This is different from the declaring type of the <paramref name="subTypeMethod"/>
    /// (and is then always a subtype) when a type inherits a method that gets hijacked as the
    /// implementation of an abstract (interface) method.</param>
    /// <returns>True iff all contract rules between the pair are satisfied.</returns>
    public virtual bool CheckContractRules(Method superTypeMethod, Method subTypeMethod, TypeNode type) {
      bool result = true;
      if (superTypeMethod == null || subTypeMethod == null) return result;

      result = CheckThrows(superTypeMethod, subTypeMethod, type);

      Interface iface = superTypeMethod.DeclaringType as Interface;
      if (iface != null) {
        if (subTypeMethod.DeclaringType != type && (superTypeMethod.Contract != null &&
          ((superTypeMethod.Contract.Requires != null && superTypeMethod.Contract.Requires.Count > 0) ||
          (superTypeMethod.Contract.Ensures != null && superTypeMethod.Contract.Ensures.Count > 0)))
          ) { // at this point the interface method has some kind of contract
          // if the subTypeMethod would be used as the subType's implementation of the superTypeMethod, then
          // this method will have been called with "type" being subTypeMethod.DeclaringType and the
          // contract rules would be checked then. And so there can't be any surprises to a client of "type" because
          // "type" must be a subtype of subTypeMethod.DeclaringType. Therefore, everything is okay.
          if (subTypeMethod.DeclaringType != null) {
            if ((!this.GetTypeView(subTypeMethod.DeclaringType).IsAssignableTo(iface)
              || this.GetTypeView(subTypeMethod.DeclaringType).GetImplementingMethod(superTypeMethod, true) != subTypeMethod)
              && subTypeMethod.Template == null) {
              this.HandleError(type.Name, Error.CannotInjectContractFromInterface, this.GetMethodSignature(superTypeMethod));
              result = false;
            }
          }
        }
        if (subTypeMethod.DeclaringType == type){
          if (subTypeMethod.Contract != null && subTypeMethod.Contract.Requires != null && subTypeMethod.Contract.Requires.Count > 0) {
            // then there's no way to attach the contract to the method
            this.HandleError(type.Name, Error.RequiresNotAllowedInInterfaceImplementation, this.GetMethodSignature(superTypeMethod));
            result = false;
          }
          if (subTypeMethod.Contract != null && subTypeMethod.Contract.Modifies != null && 0 < subTypeMethod.Contract.Modifies.Count) {
            this.HandleError(subTypeMethod.Name, Error.ModifiesNotAllowedInOverride, this.GetMethodSignature(subTypeMethod));
            result = false;
          }
        }
    }
    return result;
    }
    /// <summary>
    /// This method checks all of the methods in a class or structure to make sure that
    /// if the method implements an interface method (either explicitly or implicitly),
    /// then the contract inheritance rules are not violated.
    /// 
    /// It also introduces the default expose block around the body of whichever methods
    /// should get the default.
    /// </summary>
    /// <param name="type">The type that will have all of its methods checked.</param>
    public virtual void CheckContractInheritance(TypeNode type) {
      if (type == null || type is Interface) return;
      InterfaceList ifaces = this.GetTypeView(type).Interfaces;
      for (int i = 0, n = ifaces == null ? 0 : ifaces.Count; i < n; i++) {
        Interface iface = ifaces[i];
        if (iface == null) continue;
        TrivialHashtable processedContracts = new TrivialHashtable();
        MemberList members = this.GetTypeView(iface).Members;
        for (int j = 0, m = members == null ? 0 : members.Count; j < m; j++) {
          Method meth = members[j] as Method;
          if (meth == null) continue;
          processedContracts[meth.UniqueKey] = meth;
          Method impl = this.GetTypeView(type).GetImplementingMethod(meth, false); //Look only in type and get explicit implementation
          if (impl == null || impl.ImplementedInterfaceMethods == null || impl.ImplementedInterfaceMethods.Count == 0)
            impl = this.GetTypeView(type).GetImplementingMethod(meth, true); //No explicit interface method was found, look again, this time looking only at public methods, but including base types
          if (impl == null) continue; // If this is an error, then it will get caught in other methods in Checker.
          if (impl.Contract != null && impl.Contract.OriginalDeclaringMethod != null) {
            if (processedContracts[impl.Contract.OriginalDeclaringMethod.UniqueKey] != null) continue;
          }
          bool ifaceHasThrows = false;
          bool overriddenMethodHasThrows = false;
          for (int ensuresIndex = 0, ensuresLength = meth.Contract == null || meth.Contract.Ensures == null ? 0 : meth.Contract.Ensures.Count; ensuresIndex < ensuresLength; ensuresIndex++) {
            EnsuresExceptional e = meth.Contract.Ensures[ensuresIndex] as EnsuresExceptional;
            if (e != null) {
              ifaceHasThrows = true;
              break;
            }
          }
          for (int ensuresIndex = 0, ensuresLength = impl.OverriddenMethod == null || impl.OverriddenMethod.Contract == null || impl.OverriddenMethod.Contract.Ensures == null ? 0 : impl.OverriddenMethod.Contract.Ensures.Count; ensuresIndex < ensuresLength; ensuresIndex++) {
            EnsuresExceptional e = impl.OverriddenMethod.Contract.Ensures[ensuresIndex] as EnsuresExceptional;
            if (e != null) {
              overriddenMethodHasThrows = true;
              break;
            }
          }
          if (impl.OverriddenMethod != null) {
            if (ifaceHasThrows || overriddenMethodHasThrows) {
              this.HandleError(type.Name, Error.ContractInheritanceRulesViolated, this.GetMethodSignature(meth), this.GetMethodSignature(impl));
              continue;
            }
          }
          bool ok = true;
          if (impl.DeclaringType != type) {
            // Then the implementation for the interface method has been inherited. Figure out if
            // there are any contract inheritance violations.
            ok |= this.CheckContractRules(meth, impl, type);
          }
        }
      }
      // MB - 01/06/2005 - commented out, but leave here because at some point we may want to introduce
      // the defaults here again. They are currently introduced in Normalizer.
      //      #region Introduce Specsharp-specific defaults for each method
      //      // REVIEW: Other types besides classes?
      //      // MB: 04 January 2005: Change default expose around body to a default precondition of this.IsConsistent
      //      if (this.GetTypeView(type).IsAssignableTo(SystemTypes.IGuardedObject) && type.NodeType == NodeType.Class
      //        && type.GetAttribute(SystemTypes.NoDefaultActivityAttribute) == null){
      //        MemberList members = this.Members;  // just check the members syntactically in this type or extension
      //        for (int i = 0, n = members == null ? 0 : members.Length; i < n; i++){
      //          Method m = members[i] as Method;
      //          if (m == null) continue;
      //          if (m.IsAbstract || m.IsStatic) continue;
      //          if (m.HasCompilerGeneratedSignature) continue;
      //          if (m.NodeType == NodeType.InstanceInitializer || m.NodeType == NodeType.StaticInitializer)
      //            continue;
      //          if (m.Body == null || m.Body.Statements == null || !(m.Body.Statements.Length > 0))
      //            continue;
      //          if (m.GetAttribute(SystemTypes.NoDefaultActivityAttribute) != null)
      //            continue;
      //          if (m.Contract == null){
      //            m.Contract = new MethodContract();
      //          }
      //          if (m.Contract.Requires == null){
      //            m.Contract.Requires = new RequiresList(1);
      //          }
      //          m.Contract.Requires.Add(new RequiresPlain(
      //            new MethodCall(new MemberBinding(new This(type),SystemTypes.IGuardedObject_IsConsistent.Getter),new ExpressionList(),NodeType.Callvirt,SystemTypes.Boolean)
      //            ));
      //        }
      //      }
      //      #endregion Introduce Specsharp-specific defaults for each method
      return;
    }

    /// <summary>
    /// Need to have this override so that inherited preconditions are not
    /// checked again. 
    /// </summary>
    /// <param name="Requires"></param>
    /// <returns></returns>
    public override RequiresList VisitRequiresList(RequiresList Requires) {
      if (Requires == null) return null;
      for (int i = 0, n = Requires.Count; i < n; i++) {
        if (Requires[i] == null || Requires[i].Inherited) {
          continue;
        }
        Requires[i] = (Requires)this.Visit(Requires[i]);
      }
      return Requires;
    }
    public override RequiresPlain VisitRequiresPlain(RequiresPlain plain) {
      if (plain == null) return null;
      plain.Condition = this.VisitBooleanExpression(plain.Condition);
      return plain;
    }

    public override RequiresOtherwise VisitRequiresOtherwise(RequiresOtherwise otherwise) {
      if (otherwise == null) return null;
      if (otherwise.ThrowException != null) {
        otherwise.ThrowException = this.VisitExpression(otherwise.ThrowException);
      }
      if (otherwise.ThrowException != null && otherwise.ThrowException.Type != null) {
        TypeNode ot = otherwise.ThrowException.Type;
        if (otherwise.ThrowException.NodeType != NodeType.Literal && !this.typeSystem.IsNonNullType(ot)) {
          this.HandleError(otherwise, Error.OtherwiseExpressionMustBeNonNull);
          return null;
        }
        ot = this.typeSystem.Unwrap(ot);
        if (otherwise.ThrowException.NodeType == NodeType.Literal) {
          if (ot != SystemTypes.Type) {
            this.HandleError(otherwise, Error.OtherwiseExpressionMustBeType);
            return null;
          }
          ot = ((Literal)otherwise.ThrowException).Value as TypeNode;
        }
        if (!this.isCompilingAContractAssembly) {
          if (this.GetTypeView(ot).IsAssignableTo(SystemTypes.ICheckedException)) {
            this.HandleError(otherwise, Error.CheckedExceptionInRequiresOtherwiseClause);
            return null;
          } else if (!this.GetTypeView(ot).IsAssignableTo(SystemTypes.Exception)) {
            this.HandleError(otherwise, Error.BadExceptionType);
            return null;
          }
        }
      }
      otherwise.Condition = this.VisitBooleanExpression(otherwise.Condition);
      return otherwise;
    }
    public override MethodContract VisitMethodContract(MethodContract contract) {
      if (contract == null) return null;
      // don't visit contract.DeclaringMethod
      // don't visit contract.OverriddenMethods
      bool savedInsideMethodContract = this.insideMethodContract;
      bool savedUnsafe = this.typeSystem.insideUnsafeCode;
      this.insideMethodContract = true;

      Yield savedYield = this.yieldNode;

      Method method = contract.DeclaringMethod;
      insidePureContract = method.IsPure || method.IsConfined || method.IsStateIndependent;

      if (method != null) {

        this.typeSystem.insideUnsafeCode = method.IsUnsafe;
        if (contract.Ensures != null && contract.Ensures.Count > 0) {
          TrivialHashtable /*TypeNode*/ throwsSet = new TrivialHashtable();
          for (int i = 0, n = contract.Ensures.Count; i < n; i++) {
            EnsuresExceptional e = contract.Ensures[i] as EnsuresExceptional;
            if (e == null) continue;
            if (e.Inherited) continue;
            if (e.Type == null) continue;
            if (!this.GetTypeView(e.Type).IsAssignableTo(SystemTypes.ICheckedException)) {
              this.HandleError(e, Error.UncheckedExceptionInThrowsClause, this.GetTypeName(method.DeclaringType) + "." + method.Name.ToString());
              contract.Ensures[i] = null;
            }
            if (throwsSet[e.Type.UniqueKey] != null) {
              this.HandleError(method, Error.DuplicateThrowsType, this.GetTypeName(method.DeclaringType) + "." + method.Name.ToString());
              contract.Ensures[i] = null;
            } else {
              throwsSet[e.Type.UniqueKey] = e.Type;
            }
          }
        }

        contract.Requires = this.VisitRequiresList(contract.Requires);
        InstanceInitializer ctor = this.currentMethod as InstanceInitializer;
        bool savedMayReferenceThisAndBase = this.MayReferenceThisAndBase;
        if (ctor != null) {
          // method contracts are visited as part of visiting the methods to which they
          // are attached. So their ability to reference "this" is usually the same as
          // the method's ability. But the postcondition of a ctor can mention instance
          // variables.
          this.MayReferenceThisAndBase = true;
        }
        bool savedInsideEnsures = this.insideEnsures;
        this.insideEnsures = true;
        contract.Ensures = this.VisitEnsuresList(contract.Ensures);
        this.insideEnsures = savedInsideEnsures;
        this.MayReferenceThisAndBase = savedMayReferenceThisAndBase;
        bool savedInsideModifies = this.insideModifies;
        this.insideModifies = true;
        contract.Modifies = this.VisitExpressionList(contract.Modifies);
        this.insideModifies = savedInsideModifies;

        //if (method.IsVirtual) {
        //  // use FindNearest..., can't rely on method.OverriddenMethod since it might not be set further up the chain
        //  Method overridden = method.DeclaringType.FindNearestOverriddenMethod(method);
        //  if (overridden != null) {
        //    this.CheckEnsuresListsCompatibility(overridden, method);
        //  }
        //  for (int i = 0, n = method.ImplementedInterfaceMethods == null ? 0 : method.ImplementedInterfaceMethods.Count; i < n; i++) {
        //    Method ifaceMethod = method.ImplementedInterfaceMethods[i];
        //    this.CheckEnsuresListsCompatibility(ifaceMethod, method);
        //  }
        //  for (int i = 0, n = method.ImplicitlyImplementedInterfaceMethods == null ? 0 : method.ImplicitlyImplementedInterfaceMethods.Count; i < n; i++) {
        //    Method ifaceMethod = method.ImplicitlyImplementedInterfaceMethods[i];
        //    this.CheckEnsuresListsCompatibility(ifaceMethod, method);
        //  }
        //}
      }
      this.insideMethodContract = savedInsideMethodContract;
      this.typeSystem.insideUnsafeCode = savedUnsafe;
      this.yieldNode = savedYield;      
      return contract;
    }    
    public override Module VisitModule(Module module) {
      if (module == null) return null;
      this.currentModule = module;
      this.currentAssembly = module.ContainingAssembly;
      module.Attributes = this.VisitModuleAttributes(module.Attributes);
      module.Types = this.VisitTypeNodeList(module.Types);
      return module;
    }
    public virtual AttributeList VisitModuleAttributes(AttributeList attributes) {
      if (attributes == null) return null;
      for (int i = 0, n = attributes.Count; i < n; i++)
        attributes[i] = this.VisitModuleAttribute(attributes[i]);
      return attributes;
    }
    public virtual AttributeNode VisitModuleAttribute(AttributeNode attr) {
      attr = this.VisitAttributeNode(attr, this.currentModule);
      if (attr == null) return null;
      MemberBinding mb = attr.Constructor as MemberBinding;
      if (mb == null) return null;
      InstanceInitializer constr = mb.BoundMember as InstanceInitializer;
      if (constr == null) return null;
      TypeNode attrType = constr.DeclaringType;
      if (attrType == SystemTypes.CLSCompliantAttribute) {
        this.HandleError(mb, Error.CLSNotOnModules);
      }
      return attr;
    }
    public override Expression VisitNameBinding(NameBinding nameBinding) {
      if (nameBinding == null) return null;
      this.HandleError(nameBinding, Error.IdentifierNotFound, nameBinding.Identifier.ToString());
      return null;
    }
    public override Namespace VisitNamespace(Namespace nspace) {
      nspace = base.VisitNamespace(nspace);
      if (nspace == null) return null;
      TypeNodeList types = nspace.Types;
      UsedNamespaceList usedNspaces = nspace.UsedNamespaces;
      if (usedNspaces != null) {
        TrivialHashtable alreadyUsedNamespaces = new TrivialHashtable();
        for (int i = 0, n = usedNspaces.Count; i < n; i++) {
          UsedNamespace uns = usedNspaces[i];
          if (uns == null || uns.Namespace == null) continue;
          if (alreadyUsedNamespaces[uns.Namespace.UniqueIdKey] != null)
            this.HandleError(uns.Namespace, Error.DuplicateUsedNamespace, uns.Namespace.ToString());
          alreadyUsedNamespaces[uns.Namespace.UniqueIdKey] = uns;
          this.VisitUsedNamespace(usedNspaces[i]);
        }
      }
      AliasDefinitionList aliasDefinitions = nspace.AliasDefinitions;
      if (aliasDefinitions != null) {
        TrivialHashtable alreadyUsedAliases = new TrivialHashtable();
        for (int i = 0, n = aliasDefinitions == null ? 0 : aliasDefinitions.Count; i < n; i++) {
          AliasDefinition aliasDef = aliasDefinitions[i];
          if (aliasDef == null) continue;
          AliasDefinition dup = (AliasDefinition)alreadyUsedAliases[aliasDef.Alias.UniqueIdKey];
          if (dup == null)
            alreadyUsedAliases[aliasDef.Alias.UniqueIdKey] = aliasDef;
          else {
            this.HandleError(aliasDef.Alias, Error.DuplicateAliasDefinition, nspace.Name.ToString(), aliasDef.Alias.ToString());
            this.HandleError(dup.Alias, Error.RelatedErrorLocation);
            continue;
          }
          if (aliasDef.ConflictingType != null) {
            string nsName = nspace.Name == null || nspace.Name == Identifier.Empty ? "<global namespace>" : nspace.Name.ToString();
            this.HandleError(aliasDef.Alias, Error.ConflictBetweenAliasAndType, nsName, aliasDef.Alias.ToString());
            this.HandleRelatedError(aliasDef.ConflictingType);
          }
        }
      }
      return nspace;
    }
    public override ParameterList VisitParameterList(ParameterList parameterList) {
      if (parameterList == null) return null;
      if (this.currentMethod != null && this.currentMethod.HasCompilerGeneratedSignature) return parameterList;
      for (int i = 0, n = parameterList.Count; i < n; i++) {
        Parameter p = parameterList[i] = (Parameter)this.VisitParameter(parameterList[i]);
        if (p == null || p.GetParamArrayAttribute() == null) continue;
        if (p.GetParamArrayElementType() == null)
          this.HandleError(p, Error.ParamArrayParameterMustBeArrayType);
        else if (i < n-1)
          this.HandleError(p, Error.ParamArrayMustBeLast);
      }
      return parameterList;
    }
    public override Expression VisitParameter(Parameter parameter) {
      if (parameter == null) return null;
      parameter.Attributes = this.VisitAttributeList(parameter.Attributes, parameter);
      TypeNode pt = parameter.Type = this.VisitTypeReference(parameter.Type);
      parameter.DefaultValue = this.VisitExpression(parameter.DefaultValue);
      Reference pr = pt as Reference;
      if (pr != null) {
        pt = pr.ElementType;
        if (pt == SystemTypes.DynamicallyTypedReference || pt == SystemTypes.ArgIterator) {
          this.typeSystem.currentParameter = parameter;
          this.HandleError(parameter.Name, Error.ParameterTypeCannotBeTypedReference, this.GetTypeName(parameter.Type));
          parameter.Type = pt;
          this.typeSystem.currentParameter = null;
        }
      }
      return parameter;
    }
    public override Expression VisitPostfixExpression(PostfixExpression pExpr) {
      if (pExpr == null) return null;
      Expression e = this.VisitTargetExpression(pExpr.Expression);
      e = this.CheckForGetAccessor(e);
      if (e == null) return null;
      pExpr.Expression = new LRExpression(e);
      TypeNode t = pExpr.Type;
      if (t == null) return null;
      t = this.typeSystem.RemoveNullableWrapper(t);
      if (!t.IsPrimitiveNumeric && t != SystemTypes.Char && !(t is EnumNode) && !(t is Pointer) && pExpr.OperatorOverload == null) {
        this.HandleError(pExpr, Error.NoSuchOperator, pExpr.Operator == NodeType.Add ? "++" : "--", this.GetTypeName(t));
        return null;
      }
      if (t is Pointer && ((Pointer)t).ElementType == SystemTypes.Void) {
        this.HandleError(pExpr, Error.VoidError);
        return null;
      }
      return pExpr;
    }
    public override Expression VisitPrefixExpression(PrefixExpression pExpr) {
      if (pExpr == null) return null;
      Expression e = this.VisitTargetExpression(pExpr.Expression);
      e = this.CheckForGetAccessor(e);
      if (e == null) return null;
      pExpr.Expression = new LRExpression(e);
      TypeNode t = pExpr.Type;
      if (t == null) return null;
      t = this.typeSystem.RemoveNullableWrapper(t);
      if (!t.IsPrimitiveNumeric && t != SystemTypes.Char && !(t is EnumNode) && !(t is Pointer) && pExpr.OperatorOverload == null) {
        this.HandleError(pExpr, Error.NoSuchOperator, pExpr.Operator == NodeType.Add ? "++" : "--", this.GetTypeName(t));
        return null;
      }
      if (t is Pointer && ((Pointer)t).ElementType == SystemTypes.Void) {
        this.HandleError(pExpr, Error.VoidError);
        return null;
      }
      return pExpr;
    }
    public virtual Expression CheckForGetAccessor(Expression e) {
      if (e == null) return null;
      Property prop = null;
      Expression targetObject = null;
      Indexer indexer = e as Indexer;
      if (indexer != null) {
        prop = indexer.CorrespondingDefaultIndexedProperty;
        targetObject = indexer.Object;
      } else {
        MemberBinding mb = e as MemberBinding;
        if (mb != null) {
          prop = mb.BoundMember as Property;
          targetObject = mb.TargetObject;
        }
      }
      if (prop != null) {
        Method getter = prop.Getter;
        if (getter == null) getter = prop.GetBaseGetter();
        if (getter == null) {
          this.HandleError(e, Error.NoGetter, this.GetMemberSignature(prop));
          return null;
        }
        if (targetObject != null && this.NotAccessible(getter)) {
          this.HandleError(targetObject, Error.NotIndexable, this.GetTypeName(indexer.Object.Type));
          return null;
        }
      }
      return e;
    }
    public override Property VisitProperty(Property property) {
      property = base.VisitProperty(property);
      if (property == null) return null;
      property.Attributes = this.VisitAttributeList(property.Attributes, property);
      if (property.Name != null && property.Name.UniqueIdKey == StandardIds.Item.UniqueIdKey &&
        property.Parameters != null && property.Parameters.Count > 0) {
        if (this.indexerNames == null) this.indexerNames = new TrivialHashtable();
        Identifier previousName = (Identifier)this.indexerNames[this.currentType.UniqueKey];
        if (previousName == null)
          this.indexerNames[this.currentType.UniqueKey] = StandardIds.Item;
        else if (previousName.UniqueIdKey != property.Name.UniqueIdKey) {
          this.HandleError(property.Name, Error.InconsistantIndexerNames);
          Identifier id = new Identifier(previousName.ToString());
          id.SourceContext = property.Name.SourceContext;
          property.Name = id;
        }
      }
      if (property.Type == SystemTypes.Void)
        this.HandleError(property.Name, Error.PropertyCantHaveVoidType, this.GetMemberSignature(property));
      else if (this.IsLessAccessible(property.Type, property)) {
        Error e = Error.PropertyTypeLessAccessibleThanProperty;
        if (property.Parameters != null && property.Parameters.Count > 0)
          e = Error.PropertyTypeLessAccessibleThanIndexedProperty;
        this.HandleError(property.Name, e, this.GetTypeName(property.Type), this.GetMemberSignature(property));
        this.HandleRelatedError(property.Type);
      }
      this.CheckParameterTypeAccessibility(property.Parameters, property);
      if (property.Getter == null && property.Setter == null) {
        this.HandleError(property.Name, Error.PropertyWithNoAccessors, this.GetMemberSignature(property));
        return null;
      }
      TypeNodeList implementedTypes = property.ImplementedTypes;
      for (int i = 0, n = implementedTypes == null ? 0 : implementedTypes.Count; i < n; i++) {
        TypeNode t = implementedTypes[i];
        if (t == null) continue;
        MemberList tmems = this.GetTypeView(t).GetMembersNamed(property.Name);
        Property p = null;
        for (int j = 0, m = tmems == null ? 0 : tmems.Count; j < m; j++) {
          p = tmems[j] as Property;
          if (p == null) continue;
          if (p.Type != property.Type) { p = null; continue; }
          if (!p.ParametersMatch(property.Parameters)) { p = null; continue; }
          break;
        }
        if (p == null) {
          this.HandleError(property.Name, Error.InterfaceMemberNotFound, this.GetMemberSignature(property), this.GetTypeName(t));
          this.HandleRelatedError(t);
        } else {
          if (p.Getter == null) {
            if (property.Getter != null) {
              this.HandleError(property.Getter.Name, Error.ExplicitPropertyAddingAccessor, this.GetMethodSignature(property.Getter), this.GetMemberSignature(p));
              this.HandleRelatedError(p);
            }
          } else {
            if (property.Getter == null) {
              this.HandleError(property.Name, Error.ExplicitPropertyMissingAccessor, this.GetMemberSignature(property), this.GetMethodSignature(p.Getter));
              this.HandleRelatedError(p.Getter);
            }
          }
          if (p.Setter == null) {
            if (property.Setter != null) {
              this.HandleError(property.Setter.Name, Error.ExplicitPropertyAddingAccessor, this.GetMethodSignature(property.Setter), this.GetMemberSignature(p));
              this.HandleRelatedError(p);
            }
          } else {
            if (property.Setter == null) {
              this.HandleError(property.Name, Error.ExplicitPropertyMissingAccessor, this.GetMemberSignature(property), this.GetMethodSignature(p.Setter));
              this.HandleRelatedError(p.Setter);
            }
          }
        }
      }
      return property;
    }
    public override Expression VisitQuantifier(Quantifier quantifier) {
      bool savedInsideQuantifier = this.insideQuantifier;
      this.insideQuantifier = true;
      quantifier.Comprehension = (Comprehension)this.VisitComprehension(quantifier.Comprehension);
      this.insideQuantifier = savedInsideQuantifier;
      if (quantifier.Comprehension == null)
        return null; // Signal error?
      TypeNode comprType = TypeNode.StripModifiers(quantifier.Comprehension.Type);
      if (comprType == null)
        return null; // Signal error?
      if (comprType.TemplateArguments == null || comprType.TemplateArguments.Count != 1)
        return null; // Signal error?
      if (!this.GetTypeView(comprType.TemplateArguments[0]).IsAssignableTo(quantifier.SourceType)) {
        this.HandleError(quantifier, Error.ImpossibleCast, this.GetTypeName(quantifier.SourceType), this.GetTypeName(comprType.TemplateArguments[0]));
      }
      return quantifier;
    }
    public override Expression VisitComprehension(Comprehension comprehension) {
      if (comprehension == null) return null;
      if ((this.insideMethodContract || this.insideInvariant) &&
        this.currentMethod != null && this.currentMethod.DeclaringType != null && this.currentMethod.DeclaringType is ClosureClass) {
        // for now we don't allow comprehensions that end up creating closures in contracts because we aren't
        // able to deserialize them for cross-assembly inheritance and for static verification
        this.HandleError(comprehension, Error.GeneralComprehensionsNotAllowedInMethodContracts);
        return null;
      }
      if (comprehension.BindingsAndFilters != null) {
        for (int i = 0; i < comprehension.BindingsAndFilters.Count; i++) {
          Expression bindingOrFilter = comprehension.BindingsAndFilters[i];
          ComprehensionBinding comprehensionBinding = bindingOrFilter as ComprehensionBinding;
          if (comprehensionBinding != null) {
            comprehensionBinding.TargetVariableType = this.VisitTypeReference(comprehensionBinding.TargetVariableType);
            comprehensionBinding.TargetVariable = this.VisitTargetExpression(comprehensionBinding.TargetVariable);
            comprehensionBinding.AsTargetVariableType = this.VisitTypeReference(comprehensionBinding.AsTargetVariableType);
            comprehensionBinding.SourceEnumerable = this.VisitEnumerableCollection(comprehensionBinding.SourceEnumerable, comprehensionBinding.TargetVariableType);
            if (comprehensionBinding.AsTargetVariableType != null) {
              if (comprehensionBinding.AsTargetVariableType.IsValueType) {
                this.HandleError(comprehensionBinding, Error.AsMustHaveReferenceType, this.GetTypeName(comprehensionBinding.AsTargetVariableType));
                return null;
              } else if (!this.GetTypeView(comprehensionBinding.AsTargetVariableType).IsAssignableTo(comprehensionBinding.TargetVariableType)) {
                this.HandleError(comprehensionBinding, Error.ImpossibleCast, this.GetTypeName(comprehensionBinding.TargetVariableType), this.GetTypeName(comprehensionBinding.AsTargetVariableType));
                return null;
              }
            }
            if (comprehensionBinding.TargetVariableType == null) {
              return null; //REVIEW: does Normalizer care about this being non null. Perhaps it should check for it and fail gracefully.
              //If so, there is no neeed to stop the error checking this abruptly
            }
          } else { // it should be a filter
            comprehension.BindingsAndFilters[i] = this.VisitBooleanExpression(bindingOrFilter);
            if (comprehension.BindingsAndFilters[i] == null) {
              // then something went wrong, bail out and null out this entire comprehension
              return null;
            }
          }
        }
      }
      comprehension.Elements = this.VisitExpressionList(comprehension.Elements);
      if (comprehension.Elements == null) return null;
      TypeNode comprehensionType = TypeNode.StripModifiers(comprehension.Type);
      if (comprehensionType == null) return null;
      TypeNode eltType = null;
      if (comprehension.nonEnumerableTypeCtor == null) {
        // then this comprehension is within a quantifier
        // the elements need to be of the type the quantifier consumes
        if (comprehensionType.TemplateArguments ==null || comprehensionType.TemplateArguments.Count ==0) {
          this.HandleError(comprehension, Error.NoSuchType, this.GetTypeName(comprehensionType));
          return null;
        }
        if (comprehensionType.Template != SystemTypes.GenericIEnumerable ||
          comprehensionType.TemplateArguments == null || comprehensionType.TemplateArguments.Count != 1) {
          return null; //Resolver failed, bail out
        }
        eltType = comprehensionType.TemplateArguments[0];
      } else {
        // this comprehension is within a "new T{...}" expression.
        // the resolver put the unified type that was computed for the elements into a temporary place
        // so that the type of the comprehension could be modified to be T since it took the place of
        // the construct node that had been representing the "new T{...}" expression.
        //        if (comprehension.Type.Template != SystemTypes.GenericIEnumerable ||
        //          comprehension.Type.TemplateArguments == null || comprehension.Type.TemplateArguments.Length != 1){
        //          return null; //Resolver failed, bail out
        //        }
        if (comprehension.TemporaryHackToHoldType == null) {
          return null;
        }
        TypeNode temp = TypeNode.StripModifiers(comprehension.TemporaryHackToHoldType);
        if (temp.Template != SystemTypes.GenericIEnumerable ||
          temp.TemplateArguments == null || temp.TemplateArguments.Count != 1) {
          return null; //Resolver failed, bail out
        }
        eltType = temp.TemplateArguments[0];
      }
      if (eltType == null) { Debug.Assert(false); return null; }
      for (int i = 0, n = comprehension.Elements.Count; i < n; i++) {
        Expression e = comprehension.Elements[i];
        if (e == null) continue;
        comprehension.Elements[i] = e = this.typeSystem.ImplicitCoercion(e, eltType, this.TypeViewer);
        if (e == null) return null; //REVIEW: Normalizer should not rely on these elements not being null, so why not just continue?
      }
      if (comprehension.nonEnumerableTypeCtor == null) {
        if (!this.insideMethodContract && !this.insideAssertOrAssume && !this.insideQuantifier) {
          this.yieldNode = new Yield(); //
        }
      } else {
        InstanceInitializer c = comprehension.nonEnumerableTypeCtor as InstanceInitializer;
        if (c != null) {
          if (c.DeclaringType != null && c.DeclaringType.IsAbstract) {
            if (c.DeclaringType.IsSealed)
              this.HandleError(comprehension, Error.ConstructsAbstractSealedClass, this.GetTypeName(c.DeclaringType));
            else
              this.HandleError(comprehension, Error.ConstructsAbstractClass, this.GetTypeName(c.DeclaringType));
          } else if (this.NotAccessible(c)) {
            this.HandleError(comprehension, Error.MemberNotVisible, this.GetMemberSignature(c));
            return null;
          }
          this.CheckForObsolesence(comprehension, c);
          if (this.NotAccessible(comprehension.AddMethod)) {
            this.HandleError(comprehension, Error.MemberNotVisible, this.GetMethodSignature(comprehension.AddMethod));
            return null;
          }
          this.CheckForObsolesence(comprehension, comprehension.AddMethod);
        }

      }
      return comprehension;
    }

    public override Expression VisitQualifiedIdentifier(QualifiedIdentifier qualifiedIdentifier) {
      //At this stage, the only way a quantified identifier can exist without the program being in error is
      //when the reference is to a modelfield of a class C that was inherited from an interface, but not implemented explicity by C  
      //TODO: Check if this is the case, and resolve the qualified identifier to a memberbinding.      
      //Otherwise:
      //Report the error and remove this node from the tree
      if (qualifiedIdentifier == null) return null;
      NameBinding nb = qualifiedIdentifier.Qualifier as NameBinding;
      if (nb != null) {
        MemberList boundMembers = nb.BoundMembers;
        TypeNode t = null;
        for (int i = 0, n = boundMembers == null ? 0 : boundMembers.Count; i < n; i++) {
          t = boundMembers[i] as TypeNode;
          if (t != null) {
            this.HandleError(nb.Identifier, Error.TypeNotAccessible, this.GetTypeName(t));
            return null;
          }
        }
        this.HandleError(nb.Identifier, Error.NoSuchType, nb.Identifier.ToString());
        return null;
      }
      Expression qualifier = this.VisitExpression(qualifiedIdentifier.Qualifier);
      if (qualifier == null) return null; //An error has been reported on the qualifier. Do not compound it.
      TypeNode qtype = qualifier.Type;
      if (qtype == null) return null; //ditto
      if (qtype.Name == Looker.NotFound) return null; //ditto
      if (qtype == SystemTypes.Type) {
        Literal lit = qualifier as Literal;
        if (lit != null && lit.Value is TypeNode)
          qtype = lit.Value as TypeNode;
        else {
          MemberBinding mb = qualifier as MemberBinding;
          if (mb != null && mb.BoundMember is TypeNode)
            qtype = mb.BoundMember as TypeNode;
        }
        if (qtype == null) return null;
      }
      qtype = this.typeSystem.Unwrap(qtype);
      this.HandleError(qualifiedIdentifier.Identifier, Error.NoSuchMember, this.GetTypeName(qtype), qualifiedIdentifier.Identifier.ToString());
      return null;
    }
    public override Node VisitQueryAggregate(QueryAggregate qa) {
      if (qa == null) return null;
      qa.Expression = this.VisitExpression(qa.Expression);
      return this.CheckQueryAggregate(qa);
    }
    private QueryAggregate CheckQueryAggregate(QueryAggregate qa) {
      if (qa == null) return null;
      if (qa.Expression == null || qa.Expression.Type == null) return null;
      if (qa.Context != null && qa.Group == null) {
        Cardinality card = this.typeSystem.GetCardinality(qa.Expression, this.TypeViewer);
        if (card == Cardinality.OneOrMore && card == Cardinality.ZeroOrMore) {
          this.HandleError(qa.Expression, Error.QueryNotScalar);
          return null;
        }
      }
      MemberList members = this.GetTypeView(qa.AggregateType).GetMembersNamed(StandardIds.Add);
      if (members == null || members.Count == 0) {
        this.HandleError(qa, Error.QueryBadAggregateForm, qa.Name.Name);
        return null;
      }
      Method madd = members[0] as Method;
      if (madd == null || madd.Parameters == null || madd.Parameters.Count != 1) {
        this.HandleError(qa, Error.QueryBadAggregateForm, qa.Name.Name);
        return null;
      }
      TypeNode coreType = this.typeSystem.GetStreamElementType(qa.Expression, this.TypeViewer);
      if (!this.typeSystem.ImplicitCoercionFromTo(coreType, madd.Parameters[0].Type, this.TypeViewer)) {
        this.HandleError(qa, Error.QueryBadAggregate, qa.Name.Name, this.GetTypeName(coreType));
        return null;
      }
      Method mgetvalue = qa.AggregateType.GetMethod(StandardIds.GetValue);
      if (mgetvalue == null) {
        this.HandleError(qa, Error.QueryBadAggregateForm, qa.Name.Name);
        return null;
      }
      if (qa.Type == null) return null;
      return qa;
    }
    public override Node VisitQueryAlias(QueryAlias qa) {
      if (qa == null) return null;
      qa.Expression = this.VisitExpression(qa.Expression);
      if (qa.Type == null || qa.Expression == null || qa.Expression.Type == null) return null;
      return qa;
    }
    public override Node VisitQueryAxis(QueryAxis qa) {
      if (qa == null || this.currentMethod == null || this.currentMethod.Scope == null) return null;
      Class cc = this.currentMethod.Scope.ClosureClass;
      qa.Source = this.VisitExpression(qa.Source);
      if (qa.Source == null || qa.Source.Type == null) return null;
      TypeNode elementType = null;
      if (qa.Source is Literal && qa.Source.Type == SystemTypes.Type) {
        elementType = (TypeNode)((Literal)qa.Source).Value;
      } else {
        elementType = (qa.Source.Type is TupleType) ? qa.Source.Type : this.typeSystem.GetStreamElementType(qa.Source, this.TypeViewer);
      }
      if (qa.AccessPlan == null || qa.YieldCount == 0) {
        string name = string.Empty;
        if (qa.TypeTest != null) name = qa.TypeTest.FullName + "::";
        if (qa.Namespace != null && qa.Namespace != Identifier.Empty) {
          name += qa.Namespace.Name + ":";
        }
        name += (qa.Name != null && qa.Name != Identifier.Empty) ? qa.Name.Name : "*";
        this.HandleError(qa, Error.QueryNoMatch, this.GetTypeName(elementType), name);
        return null;
      }
      if (qa.Type == null) return null;
      return qa;
    }
    public override Node VisitQueryCommit(QueryCommit qc) {
      if (qc == null) return null;
      if (this.currentTransaction == null) {
        this.HandleError(qc, Error.QueryNotTransacted);
        return null;
      }
      return base.VisitQueryCommit(qc);
    }
    public override Node VisitQueryContext(QueryContext qc) {
      if (qc == null) return null;
      if (qc.Type == null) {
        this.HandleError(qc, Error.QueryNoContext);
        return null;
      }
      return qc;
    }
    public Expression GetSourceCollection(Expression node, TypeNode elementType) {
      Expression expr = null;
      switch (node.NodeType) {
        case NodeType.QueryFilter:
          QueryFilter qf = node as QueryFilter;
          return this.GetSourceCollection(qf.Source, elementType);
        case NodeType.QueryIterator:
          TypeNode t = this.typeSystem.GetStreamElementType(((QueryIterator)node).Expression, this.TypeViewer);
          if (t == elementType)
            return ((QueryIterator)node).Expression;
          break;
        case NodeType.QueryJoin:
          QueryJoin qj = node as QueryJoin;
          expr = this.GetSourceCollection(qj.LeftOperand, elementType);
          if (expr == null)
            expr = this.GetSourceCollection(qj.RightOperand, elementType);
          break;
        case NodeType.Parentheses:
        case NodeType.SkipCheck:
          return this.GetSourceCollection(((UnaryExpression)node).Operand, elementType);
        default:
          break;
      }
      return expr;
    }
    private static readonly Identifier idRemoveAt = Identifier.For("RemoveAt");
    public override Node VisitQueryDelete(QueryDelete qd) {
      if (qd == null) return null;
      qd.Source = this.VisitExpression(qd.Source);
      if (qd.Source == null || qd.Source.Type == null) return null;
      Cardinality card = this.typeSystem.GetCardinality(qd.Source, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(qd.Source, Error.QueryNotStream);
        return null;
      }
      qd.Target = this.VisitExpression(qd.Target);

      // Target can not be null if there is more than one source
      QueryFilter qf = qd.Source as QueryFilter;
      Expression source = (qf != null) ? qf.Source : qd.Source;
      QueryJoin qj = source as QueryJoin;
      if (qj != null && qd.Target == null) {
        this.HandleError(qd.Source, Error.QueryBadDeleteTarget);
        return null;
      }
      Expression collection = qd.SourceEnumerable = (qd.Target == null) ? ((QueryIterator)source).Expression : 
        GetSourceCollection(source, qd.Target.Type);
      TypeNode elementType = this.typeSystem.GetStreamElementType(collection, this.TypeViewer);

      if (elementType == null) return null;
      if (collection == null || collection.Type == null) return null;
      Method mRemove = this.GetTypeView(collection.Type).GetMethod(StandardIds.Remove, elementType);
      if (mRemove == null) mRemove = this.GetTypeView(collection.Type).GetMethod(StandardIds.Remove, SystemTypes.Object);
      Method mRemoveAt = this.GetTypeView(source.Type).GetMethod(idRemoveAt, SystemTypes.Int32);
      if (mRemoveAt == null) mRemoveAt = this.GetTypeView(collection.Type).GetMethod(idRemoveAt, SystemTypes.Int32);
      if (mRemove == null && mRemoveAt == null) {
        this.HandleError(collection, Error.QueryNotDeleteStream, this.GetTypeName(collection.Type), this.GetTypeName(elementType));
        return null;
      }
      if (qd.Type == null) return null;
      return qd;
    }
    public override Node VisitQueryDifference(QueryDifference qd) {
      if (qd == null) return null;
      qd.LeftSource = this.VisitExpression(qd.LeftSource);
      qd.RightSource = this.VisitExpression(qd.RightSource);
      if (qd.LeftSource == null || qd.LeftSource.Type == null ||
        qd.RightSource == null || qd.RightSource.Type == null) return null;
      Cardinality card = this.typeSystem.GetCardinality(qd.LeftSource, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(qd.LeftSource, Error.QueryNotStream);
        return null;
      }
      card = this.typeSystem.GetCardinality(qd.RightSource, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(qd.RightSource, Error.QueryNotStream);
        return null;
      }
      if (qd.LeftSource.Type.UniqueKey != qd.RightSource.Type.UniqueKey) {
        this.HandleError(qd, Error.QueryBadDifferenceTypes, this.GetTypeName(qd.LeftSource.Type), this.GetTypeName(qd.RightSource.Type));
        return null;
      }
      if (qd.Type == null) return null;
      this.HandleError(qd, Error.QueryNotSupported);
      return null;
    }
    public override Node VisitQueryDistinct(QueryDistinct qd) {
      if (qd == null) return null;
      Class cc = this.currentMethod.Scope.ClosureClass;
      qd.Source = this.VisitExpression(qd.Source);
      return this.CheckQueryDistinct(qd);
    }
    private QueryDistinct CheckQueryDistinct(QueryDistinct qd) {
      if (qd == null) return null;
      if (qd.Source == null || qd.Source.Type == null) return null;
      if (qd.Group == null) {
        Cardinality card = this.typeSystem.GetCardinality(qd.Source, this.TypeViewer);
        if (qd.Context == null && card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
          this.HandleError(qd.Source, Error.QueryNotStream);
          return null;
        } else if (qd.Context != null && (card == Cardinality.OneOrMore || card == Cardinality.ZeroOrMore)) {
          this.HandleError(qd.Source, Error.QueryNotScalar);
          return null;
        }
      }
      if (qd.Type == null) return null;
      return qd;
    }
    public override Node VisitQueryExists(QueryExists qe) {
      if (qe == null) return null;
      qe.Source = this.VisitExpression(qe.Source);
      if (qe.Source == null || qe.Source.Type == null) return null;
      Cardinality card = this.typeSystem.GetCardinality(qe.Source, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(qe.Source, Error.QueryNotStream);
        return null;
      }
      if (qe.Type == null) return null;
      return qe;
    }
    public override Node VisitQueryFilter(QueryFilter qf) {
      if (qf == null) return null;
      Class cc = this.currentMethod.Scope.ClosureClass;
      qf.Source = this.VisitExpression(qf.Source);
      if (qf.Source == null || qf.Source.Type == null) return null;
      Cardinality card = this.typeSystem.GetCardinality(qf.Source, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(qf.Source, Error.QueryNotStream);
        return null;
      }
      qf.Expression = this.VisitBooleanExpression(qf.Expression);
      if (qf.Type == null || qf.Expression == null || qf.Expression.Type == null) return null;
      return qf;
    }
    public override Node VisitQueryGroupBy(QueryGroupBy qgb) {
      if (qgb == null) return null;
      Class cc = this.currentMethod.Scope.ClosureClass;
      qgb.Source = this.VisitExpression(qgb.Source);
      if (qgb.Source == null || qgb.Source.Type == null) return null;
      Cardinality card = this.typeSystem.GetCardinality(qgb.Source, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(qgb.Source, Error.QueryNotStream);
        return null;
      }
      int nGroup = qgb.GroupList == null ? 0 : qgb.GroupList.Count;
      int nAgg = qgb.AggregateList == null ? 0 : qgb.AggregateList.Count;
      if (nGroup + nAgg == 0) {
        this.HandleError(qgb, Error.QueryBadGroupByList);
        return null;
      }
      for (int i = 0, n = qgb.GroupList.Count; i < n; i++) {
        Expression x = this.VisitExpression(qgb.GroupList[i]);
        if (x == null || x.Type == null) return null;
        card = this.typeSystem.GetCardinality(x, this.TypeViewer);
        if (card == Cardinality.OneOrMore || card == Cardinality.ZeroOrMore) {
          this.HandleError(x, Error.QueryNotScalar);
          return null;
        }
        qgb.GroupList[i] = x;
      }
      for (int i = 0, n = qgb.AggregateList.Count; i < n; i++) {
        QueryAggregate qa = qgb.AggregateList[i] as QueryAggregate;
        if (qa == null) continue;
        QueryDistinct qd = qa.Expression as QueryDistinct;
        if (qd != null) {
          qd.Source = this.VisitExpression(qd.Source);
          qa.Expression = this.CheckQueryDistinct(qd);
        } else {
          qa.Expression = this.VisitExpression(qa.Expression);
        }
        qa = this.CheckQueryAggregate(qa);
        if (qa == null) return null;
        qgb.AggregateList[i] = qa;
      }
      if (qgb.Having != null) {
        qgb.Having = this.VisitExpression(qgb.Having);
        if (qgb.Having == null) return null;
      }
      if (qgb.Type == null) return null;
      return qgb;
    }
    private static readonly Identifier idInsert = Identifier.For("Insert");
    public override Node VisitQueryInsert(QueryInsert qi) {
      if (qi == null) return qi;
      qi.Location = this.VisitExpression(qi.Location);
      if (qi.Location == null || qi.Location.Type == null) return null;
      TypeNode sourceElementType = this.typeSystem.GetStreamElementType(qi.Location, this.TypeViewer);
      Cardinality card = this.typeSystem.GetCardinality(qi.Location, this.TypeViewer);
      TypeNode addType = sourceElementType;
      switch (qi.Position) {
        case QueryInsertPosition.In: {
            if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
              this.HandleError(qi.Location, Error.QueryNotStream);
              return null;
            }
            Method m = this.GetTypeView(qi.Location.Type).GetMethod(StandardIds.Insert, SystemTypes.Int32, sourceElementType);
            if (m == null) m = this.GetTypeView(qi.Location.Type).GetMethod(StandardIds.Insert, SystemTypes.Int32, SystemTypes.Object);
            if (m == null) {
              this.HandleError(qi.Location, Error.QueryNotAddStream, this.GetTypeName(qi.Location.Type), this.GetTypeName(sourceElementType));
              return null;
            }
            addType = m.Parameters[1].Type;
            break;
          }
        case QueryInsertPosition.At:
        case QueryInsertPosition.After:
        case QueryInsertPosition.Before:
        case QueryInsertPosition.First:
        case QueryInsertPosition.Last:
          this.HandleError(qi, Error.QueryNotSupported);
          break;
      }
      if (qi.InsertList == null || qi.InsertList.Count == 0) {
        this.HandleError(qi, Error.QueryBadInsertList);
        return null;
      }
      for (int i = 0, n = qi.InsertList.Count; i < n; i++) {
        Expression x = this.VisitExpression(qi.InsertList[i]);
        if (x == null || x.Type == null) {
          return null;
        }
        if (qi.IsBracket && x.NodeType != NodeType.AssignmentExpression) {
          this.HandleError(qi, Error.QueryNotSupported);
          return null;
        }
        qi.InsertList[i] = x;
      }
      if (qi.InsertList.Count == 1) {
        Expression x = qi.InsertList[0];
        if (x.NodeType != NodeType.AssignmentExpression) {
          if (!this.typeSystem.ImplicitCoercionFromTo(x.Type, addType, this.TypeViewer)) {
            this.typeSystem.ImplicitCoercion(x, sourceElementType, this.TypeViewer); // produce error message
            return null;
          }
        }
      } else if (!sourceElementType.IsValueType) {
        if (sourceElementType.GetConstructor() == null) {
          this.HandleError(qi, Error.QueryMissingDefaultConstructor);
          return null;
        }
      }
      qi.HintList = this.VisitExpressionList(qi.HintList);
      if (qi.Type == null) return null;
      return qi;
    }
    public override Node VisitQueryIntersection(QueryIntersection qi) {
      if (qi == null) return null;
      qi.LeftSource = this.VisitExpression(qi.LeftSource);
      qi.RightSource = this.VisitExpression(qi.RightSource);
      if (qi.LeftSource == null || qi.LeftSource.Type == null ||
        qi.RightSource == null || qi.RightSource.Type == null) return null;
      Cardinality card = this.typeSystem.GetCardinality(qi.LeftSource, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(qi.LeftSource, Error.QueryNotStream);
        return null;
      }
      card = this.typeSystem.GetCardinality(qi.RightSource, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(qi.RightSource, Error.QueryNotStream);
        return null;
      }
      if (qi.LeftSource.Type.UniqueKey != qi.RightSource.Type.UniqueKey) {
        this.HandleError(qi, Error.QueryBadIntersectionTypes, this.GetTypeName(qi.LeftSource.Type), this.GetTypeName(qi.RightSource.Type));
        return null;
      }
      if (qi.Type == null) return null;
      this.HandleError(qi, Error.QueryNotSupported);
      return null;
    }
    public override Node VisitQueryIterator(QueryIterator qi) {
      if (qi == null) return null;
      Class cc = this.currentMethod.Scope.ClosureClass;
      qi.Expression = this.VisitExpression(qi.Expression);
      if (qi.Expression == null || qi.Expression.Type == null) return null;
      Cardinality card = this.typeSystem.GetCardinality(qi.Expression, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(qi.Expression, Error.QueryNotStream);
        return null;
      }
      qi.HintList = this.VisitExpressionList(qi.HintList);
      if (qi.Type == null) return null;
      return qi;
    }
    public override Node VisitQueryJoin(QueryJoin qj) {
      if (qj == null) return null;
      Class cc = this.currentMethod.Scope.ClosureClass;
      qj.LeftOperand = this.VisitExpression(qj.LeftOperand);
      qj.RightOperand = this.VisitExpression(qj.RightOperand);
      if (qj.LeftOperand == null || qj.LeftOperand.Type == null ||
        qj.RightOperand == null || qj.RightOperand.Type == null) return null;
      Cardinality card = this.typeSystem.GetCardinality(qj.LeftOperand, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(qj.LeftOperand, Error.QueryNotStream);
        return null;
      }
      card = this.typeSystem.GetCardinality(qj.RightOperand, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(qj.RightOperand, Error.QueryNotStream);
        return null;
      }
      if (qj.JoinExpression != null) {
        qj.JoinExpression = this.VisitBooleanExpression(qj.JoinExpression);
        if (qj.JoinExpression == null || qj.JoinExpression.Type == null) return null;
      }
      if (qj.Type == null) return null;
      return qj;
    }
    public override Node VisitQueryLimit(QueryLimit ql) {
      if (ql == null) return null;
      ql.Source = this.VisitExpression(ql.Source);
      if (ql.Source == null || ql.Source.Type == null) return null;
      Cardinality card = this.typeSystem.GetCardinality(ql.Source, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(ql.Source, Error.QueryNotStream);
        return null;
      }
      ql.Expression = this.VisitExpression(ql.Expression);
      if (ql.Expression == null || ql.Expression.Type == null) return null;

      if (ql.Expression.NodeType != NodeType.Literal) {
        this.HandleError(ql, Error.QueryBadLimitNotLiteral);
        return null;
      } else if (!this.typeSystem.ImplicitCoercionFromTo(ql.Expression.Type, SystemTypes.Int64, this.TypeViewer)) {
        if (!this.typeSystem.ImplicitCoercionFromTo(ql.Expression.Type, SystemTypes.Double, this.TypeViewer) && 
          !this.typeSystem.ImplicitCoercionFromTo(ql.Expression.Type, SystemTypes.Decimal, this.TypeViewer)) {
          this.HandleError(ql.Expression, Error.QueryBadLimit);
          return null;
        } else if (!ql.IsPercent) {
          this.HandleError(ql.Expression, Error.QueryBadLimitForNotPercent);
          return null;
        }
      }
      if (ql.Type == null) return null;
      return ql;
    }
    public override Node VisitQueryOrderBy(QueryOrderBy qob) {
      if (qob == null) return null;
      Class cc = this.currentMethod.Scope.ClosureClass;
      qob.Source = this.VisitExpression(qob.Source);
      if (qob.Source == null || qob.Source.Type == null) return null;
      Cardinality card = this.typeSystem.GetCardinality(qob.Source, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(qob.Source, Error.QueryNotStream);
      }
      if (qob.OrderList == null || qob.OrderList.Count == 0) {
        this.HandleError(qob, Error.QueryBadOrderList);
        return null;
      }
      for (int i = 0, n = qob.OrderList.Count; i < n; i++) {
        Expression x = qob.OrderList[i];
        if (x == null) {
          this.HandleError(qob, Error.QueryBadOrderList);
          return null;
        }
        QueryOrderItem oi = x as QueryOrderItem;
        if (oi != null) {
          oi.Expression = this.VisitExpression(oi.Expression);
          if (oi.Expression == null || oi.Type == null) return null;
        } else {
          x = this.VisitExpression(x);
          if (x == null || x.Type == null) return null;
        }
        card = this.typeSystem.GetCardinality(x, this.TypeViewer);
        if (card == Cardinality.OneOrMore || card == Cardinality.ZeroOrMore) {
          this.HandleError(x, Error.QueryNotScalar);
          return null;
        }
        qob.OrderList[i] = x;
      }
      if (qob.Type == null) return null;
      return qob;
    }
    public override Node VisitQueryOrderItem(QueryOrderItem qoi) {
      // order items can only occur in the OrderBy.OrderList
      if (qoi != null) this.HandleError(qoi, Error.QueryBadOrderItem);
      return null;
    }
    public override Node VisitQueryProject(QueryProject qp) {
      if (qp == null) return null;
      Class cc = this.currentMethod.Scope.ClosureClass;
      qp.Source = this.VisitExpression(qp.Source);
      if (qp.Source == null || qp.Source.Type == null) return null;
      Cardinality card = this.typeSystem.GetCardinality(qp.Source, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(qp.Source, Error.QueryNotStream);
        return null;
      }
      if (qp.ProjectionList == null || qp.ProjectionList.Count == 0) {
        this.HandleError(qp.Source, Error.QueryBadProjectionList);
      }
      for (int i = 0, n = qp.ProjectionList.Count; i < n; i++) {
        Expression x = this.VisitExpression(qp.ProjectionList[i]);
        if (x == null || x.Type == null) return null;
        qp.ProjectionList[i] = x;
      }
      if (qp.Type == null || qp.ProjectedType == null) return null;
      return qp;
    }
    public override Node VisitQueryQuantifier(QueryQuantifier qq) {
      if (qq == null) return null;
      QueryQuantifiedExpression savedQQE = this.currentQuantifiedExpression;
      this.currentQuantifiedExpression = null;
      qq.Expression = this.VisitExpression(qq.Expression);
      this.currentQuantifiedExpression = savedQQE;
      if (qq.Expression == null || qq.Expression.Type == null) return null;
      Cardinality card = this.typeSystem.GetCardinality(qq.Expression, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(qq.Expression, Error.QueryNotStream);
        return null;
      }
      if (this.currentQuantifiedExpression == null) {
        this.HandleError(qq, Error.QueryBadQuantifier);
        return null;
      }
      if (this.currentQuantifiedExpression.Left != qq &&
        this.currentQuantifiedExpression.Right != qq) {
        this.HandleError(qq, Error.QueryBadQuantifier);
        return null;
      }
      if (qq.Type == null) return null;
      return qq;
    }
    public override Node VisitQueryQuantifiedExpression(QueryQuantifiedExpression qqe) {
      if (qqe == null) return null;
      QueryQuantifiedExpression saveQQE = this.currentQuantifiedExpression;
      this.currentQuantifiedExpression = qqe;
      qqe.Expression = this.VisitBooleanExpression(qqe.Expression);
      this.currentQuantifiedExpression = saveQQE;
      if (qqe.Expression == null || qqe.Type == null) return null;
      Cardinality card = this.typeSystem.GetCardinality(qqe.Expression, this.TypeViewer);
      if (card == Cardinality.OneOrMore || card == Cardinality.ZeroOrMore) {
        this.HandleError(qqe.Expression, Error.QueryNotScalar);
        return null;
      }
      if (qqe.Type == null) return null;
      // TODO: we need to insert the same logic as the one used in an if statement
      if (!this.typeSystem.ImplicitCoercionFromTo(qqe.Type, SystemTypes.Boolean, this.TypeViewer) && 
        !this.typeSystem.ImplicitCoercionFromTo(qqe.Type, SystemTypes.SqlBoolean, this.TypeViewer)) {
        this.HandleError(qqe, Error.QueryBadQuantifiedExpression);
        return null;
      }
      return qqe;
    }
    public override Node VisitQueryRollback(QueryRollback qr) {
      if (qr == null) return null;
      if (this.currentTransaction == null) {
        this.HandleError(qr, Error.QueryNotTransacted);
        return null;
      }
      return base.VisitQueryRollback(qr);
    }
    public override Node VisitQuerySelect(QuerySelect qs) {
      if (qs == null) return null;
      qs.Source = this.VisitExpression(qs.Source);
      if (qs.Type == null || qs.Source == null || qs.Source.Type == null) return null;
      return qs;
    }
    public override Node VisitQuerySingleton(QuerySingleton qs) {
      if (qs == null) return null;
      qs.Source = this.VisitExpression(qs.Source);
      if (qs.Type == null || qs.Source == null || qs.Source.Type == null) return null;
      return qs;
    }
    public override Node VisitQueryTransact(QueryTransact qt) {
      if (qt == null) return null;
      if (this.currentTransaction != null) {
        this.HandleError(qt, Error.QueryNoNestedTransaction);
        return null;
      }
      this.currentTransaction = qt;
      base.VisitQueryTransact(qt);
      this.currentTransaction = null;
      if (qt.Source == null || qt.Source.Type == null) return null;
      if (!this.typeSystem.ImplicitCoercionFromTo(qt.Source.Type, SystemTypes.IDbConnection, this.TypeViewer) &&
        !this.typeSystem.ImplicitCoercionFromTo(qt.Source.Type, SystemTypes.IDbTransactable, this.TypeViewer)) {
        this.HandleError(qt.Source, Error.QueryNotTransactable, this.GetTypeName(qt.Source.Type));
        return null;
      }
      if (qt.Isolation != null) {
        if (qt.Isolation.Type == null) return null;
        if (!this.typeSystem.ImplicitCoercionFromTo(qt.Isolation.Type, SystemTypes.IsolationLevel, this.TypeViewer)) {
          this.typeSystem.ImplicitCoercion(qt.Isolation, SystemTypes.IsolationLevel, this.TypeViewer);
          return null;
        }
      }
      return qt;
    }
    public override Node VisitQueryTypeFilter(QueryTypeFilter qtf) {
      if (qtf == null) return null;
      qtf.Source = this.VisitExpression(qtf.Source);
      if (qtf.Source == null || qtf.Source.Type == null) return null;
      Cardinality card = this.typeSystem.GetCardinality(qtf.Source, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(qtf.Source, Error.QueryNotStream);
        return null;
      }
      qtf.Constraint = this.VisitTypeReference(qtf.Constraint);
      if (qtf.Constraint == null) {
        this.HandleError(qtf, Error.QueryBadTypeFilter);
        return null;
      }
      if (qtf.Type == null) return null;
      return qtf;
    }
    public override Node VisitQueryUnion(QueryUnion qu) {
      if (qu == null) return null;
      qu.LeftSource = this.VisitExpression(qu.LeftSource);
      qu.RightSource = this.VisitExpression(qu.RightSource);
      if (qu.LeftSource == null || qu.LeftSource.Type == null ||
        qu.RightSource == null || qu.RightSource.Type == null) return null;
      Cardinality card = this.typeSystem.GetCardinality(qu.LeftSource, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(qu.LeftSource, Error.QueryNotStream);
        return null;
      }
      card = this.typeSystem.GetCardinality(qu.RightSource, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(qu.RightSource, Error.QueryNotStream);
        return null;
      }
      if (qu.LeftSource.Type.UniqueKey != qu.RightSource.Type.UniqueKey) {
        this.HandleError(qu, Error.QueryBadUnionTypes, this.GetTypeName(qu.LeftSource.Type), this.GetTypeName(qu.RightSource.Type));
        return null;
      }
      if (qu.Type == null) return null;
      this.HandleError(qu, Error.QueryNotSupported);
      return null;
    }

    public override Node VisitQueryUpdate(QueryUpdate qu) {
      if (qu == null) return null;
      qu.Source = this.VisitExpression(qu.Source);
      if (qu.Source == null || qu.Source.Type == null) return null;
      Cardinality card = this.typeSystem.GetCardinality(qu.Source, this.TypeViewer);
      if (card != Cardinality.OneOrMore && card != Cardinality.ZeroOrMore) {
        this.HandleError(qu.Source, Error.QueryNotStream);
        return null;
      }
      if (qu.UpdateList == null || qu.UpdateList.Count == 0) {
        this.HandleError(qu, Error.QueryBadUpdateList);
        return null;
      }
      for (int i = 0, n = qu.UpdateList.Count; i < n; i++) {
        Expression x = qu.UpdateList[i];
        if (x == null) {
          this.HandleError(qu, Error.QueryBadUpdateList);
          return null;
        }
        x = this.VisitExpression(x);
        if (x == null || x.Type == null) return null;
        qu.UpdateList[i] = x;
      }
      TypeNode elementType = this.typeSystem.GetStreamElementType(qu.Source, this.TypeViewer);
      if (elementType == null) return null;
      if (elementType.IsValueType) {
        QueryFilter qf = qu.Source as QueryFilter;
        Expression source = (qf != null) ? qf.Source : qu.Source;
        if (source == null || source.Type == null) return null;
        Method mSetItem = this.GetTypeView(source.Type).GetMethod(Identifier.For("set_Item"));
        Method mAdd = this.GetTypeView(source.Type).GetMethod(StandardIds.Add, elementType);
        if (mAdd == null) mAdd = this.GetTypeView(source.Type).GetMethod(StandardIds.Add, SystemTypes.Object);
        Method mRemove = this.GetTypeView(source.Type).GetMethod(StandardIds.Remove, elementType);
        if (mRemove == null) mRemove = this.GetTypeView(source.Type).GetMethod(StandardIds.Remove, SystemTypes.Object);
        if (mSetItem == null && (mAdd == null || mRemove == null)) {
          this.HandleError(source, Error.QueryNotUpdateStream, this.GetTypeName(source.Type), this.GetTypeName(elementType));
          return null;
        }
      }
      return qu;
    }
    public override Statement VisitRepeat(Repeat repeat) {
      if (repeat == null) return null;
      this.loopCount++;
      repeat.Body = this.VisitBlock(repeat.Body);
      repeat.Condition = this.VisitBooleanExpression(repeat.Condition);
      this.loopCount--;
      return repeat;
    }
    public override Statement VisitReturn(Return Return) {
      if (Return == null) return null;
      if (this.currentFinallyClause != null) {
        this.HandleError(Return, Error.BadFinallyLeave);
        return null;
      }
      if (this.yieldNode != null || this.currentMethod == null) {
        this.HandleError(Return, Error.ReturnNotAllowed);
        return null;
      }
      this.returnNode = Return;
      if (this.currentMethod.ReturnType == SystemTypes.Void) {
        if (this.VisitExpression(Return.Expression) != null) {
          this.HandleError(Return.Expression, Error.CannotReturnValue, this.GetMethodSignature(this.currentMethod));
          Return.Expression = null;
        }
        return Return;
      }
      if (Return.Expression == null)
        this.HandleError(Return, Error.ReturnValueRequired, this.GetTypeName(this.currentMethod.ReturnType));
      else {
        TypeNode rtype = this.currentMethod.ReturnType;
        if (rtype is ITypeParameter && this.NonNullChecking && rtype != Return.Expression.Type)
          rtype = OptionalModifier.For(SystemTypes.NonNullType, rtype);
        if (this.currentMethod.ReturnType is DelegateNode && Return.Expression.Type == SystemTypes.Delegate)
          Return.Expression = this.typeSystem.ImplicitCoercion(Return.Expression, rtype, this.TypeViewer);
        else
          Return.Expression = this.typeSystem.ImplicitCoercion(this.VisitExpression(Return.Expression), rtype, this.TypeViewer);
      }
      return Return;
    }
    public override Expression VisitTemplateInstance(TemplateInstance instance) {
      if (instance == null || instance.Expression == null) return null;
      if (instance.IsMethodTemplate) return instance;
      Member offendingMember = null;
      Literal lit = instance.Expression as Literal;
      if (lit != null) {
        offendingMember = lit.Value as Member;
      } else {
        MemberBinding mb = instance.Expression as MemberBinding;
        if (mb != null)
          offendingMember = mb.BoundMember;
        else {
          NameBinding nb = instance.Expression as NameBinding;
          if (nb != null) {
            for (int i = 0, n = nb.BoundMembers == null ? 0 : nb.BoundMembers.Count; i < n; i++) {
              Member mem = nb.BoundMembers[i];
              if (mem is Field || mem is TypeNode || mem is Event || 
                (mem is Property && (((Property)mem).Parameters == null || ((Property)mem).Parameters.Count == 0))) {
                offendingMember = mem; break;
              }
            }
          } else {
            MethodCall mc = instance.Expression as MethodCall; //Can happen when trying to apply type arguments to a property
            if (mc == null) return null;
            mb = mc.Callee as MemberBinding;
            if (mb != null && mb.BoundMember is Method) {
              Property p = ((Method)mb.BoundMember).DeclaringMember as Property;
              if (p != null) offendingMember = p;
            }
          }
        }
      }
      if (offendingMember == null) {
        this.HandleError(instance.Expression, Error.NotAType);
        return null;
      }
      TypeNode offendingType = offendingMember as TypeNode;
      if (offendingType != null) {
        if (offendingType.TemplateParameters == null || offendingType.TemplateParameters.Count == 0) {
          this.HandleError(instance.Expression, Error.NotATemplateType, this.GetTypeName(offendingType), "type");
          this.HandleRelatedError(offendingType);
        } else {
          //TODO: when does this happen?
          return null;
        }
        return null;
      }
      string offendingKind = this.ErrorHandler.GetMemberKind(offendingMember);
      this.HandleError(instance.Expression, Error.TypeArgsNotAllowed, offendingKind, this.GetMemberSignature(offendingMember));
      this.HandleRelatedError(offendingMember);
      return null;
    }
    public virtual void VisitTemplateInstanceTypes(TypeNode t) {
      if (t == null || (t.IsGeneric && this.useGenerics)) return;
      TypeNodeList templateInstances = t.TemplateInstances;
      for (int i = 0, n = templateInstances == null ? 0 : templateInstances.Count; i < n; i++)
        this.Visit(templateInstances[i]);
    }
    public override Expression VisitTernaryExpression(TernaryExpression expression) {
      if (expression == null) return null;
      if (expression.NodeType != NodeType.Conditional)
        return base.VisitTernaryExpression(expression);
      Expression opnd1 = expression.Operand1 = this.VisitBooleanExpression(expression.Operand1);
      Expression opnd2 = expression.Operand2 = this.VisitExpression(expression.Operand2);
      Expression opnd3 = expression.Operand3 = this.VisitExpression(expression.Operand3);
      if (opnd1 == null || opnd2 == null || opnd3 == null) return null;
      TypeNode opnd2Type = opnd2.Type;
      TypeNode opnd3Type = opnd3.Type;
      if (expression.Type == SystemTypes.Object && opnd2Type != SystemTypes.Object && opnd3Type != SystemTypes.Object) {
        //unification failed, give an error
        if (this.typeSystem.ImplicitCoercionFromTo(opnd2Type, opnd3Type, this.TypeViewer))
          this.HandleError(expression, Error.AmbiguousConditional, this.GetTypeName(opnd2Type), this.GetTypeName(opnd3Type));
        else
          this.HandleError(expression, Error.InvalidConditional, this.GetTypeName(opnd2Type), this.GetTypeName(opnd3Type));
        return null;
      }
      if ((expression.Operand2 = this.typeSystem.ImplicitCoercion(opnd2, expression.Type, this.TypeViewer)) == null) return null;
      if ((expression.Operand3 = this.typeSystem.ImplicitCoercion(opnd3, expression.Type, this.TypeViewer)) == null) return null;
      return expression;
    }

    public bool CheckFixed(Expression e) {
      if (e == null) return true;
      if (e is Local || e is Parameter || e is ImplicitThis) {
        if (this.insideFixedDeclarator && e.Type != null && e.Type.IsUnmanaged) {
          this.HandleError(e, Error.FixedNotNeeded);
          return false;
        }
        return true;
      }
      MemberBinding mb = e as MemberBinding;
      if (mb != null) {
        Field f = mb.BoundMember as Field;
        if (f != null) {
          if (f.IsVolatile)
            this.HandleError(e, Error.VolatileByRef, this.GetMemberSignature(f));
          else {
            BlockScope bscope = f.DeclaringType as BlockScope;
            MethodScope mscope = f.DeclaringType as MethodScope;
            if (bscope != null || mscope != null) {
              if (this.insideFixedDeclarator && e.Type != null && e.Type.IsUnmanaged) {
                this.HandleError(e, Error.FixedNotNeeded);
                return false;
              }
              if (bscope != null) return !bscope.CapturedForClosure;
              return !mscope.CapturedForClosure;
            }
          }
        }
        if (!this.CheckFixed(mb.TargetObject)) return false;
        if (this.insideFixedDeclarator || f.DeclaringType is Struct) return true;
        this.HandleError(mb.BoundMemberExpression, Error.FixedNeeded);
        return false;
      } else {
        if (e.NodeType == NodeType.Parentheses)
          return this.CheckFixed(((UnaryExpression)e).Operand);
        AddressDereference adr = e as AddressDereference;
        if (adr != null) {
          if (adr.Explicit) return true;
          return this.CheckFixed(adr.Address);
        }
        Indexer indxr = e as Indexer;
        if (indxr != null) {
          if (this.insideFixedDeclarator)
            return this.CheckFixed(indxr.Object);
          else if (indxr.Type != null && indxr.Type.IsValueType) {
            TypeNode indxrObT = TypeNode.StripModifiers(indxr.Object == null ? null : indxr.Object.Type);
            if (indxrObT is Pointer) return true;
            this.HandleError(e, Error.FixedNeeded);
          } else
            this.HandleError(e, Error.FixedNeeded);
        } else {
          MethodCall mcall = e as MethodCall;
          if (mcall != null && TypeNode.StripModifiers(mcall.Type) is ArrayType) return true;
          this.HandleError(e, Error.InvalidAddressOf);
        }
        return false;
      }
    }
    public bool refOrOutAddress;
    public override Expression VisitUnaryExpression(UnaryExpression unaryExpression) {
      if (unaryExpression == null) return null;
      Expression opnd = null;
      switch (unaryExpression.NodeType) {
        case NodeType.AddressOf:
          opnd = unaryExpression.Operand = this.VisitExpression(unaryExpression.Operand);
          if (opnd != null && this.CheckFixed(opnd) && opnd.Type != null && !opnd.Type.IsUnmanaged) {
            this.HandleError(unaryExpression, Error.ManagedAddr, this.GetTypeName(opnd.Type));
            return null;
          }
          break;
        case NodeType.OutAddress:
        case NodeType.RefAddress: {
            bool savedRefout = this.refOrOutAddress;
            this.refOrOutAddress = true;
            Expression e = unaryExpression.Operand = this.VisitTargetExpression(unaryExpression.Operand);
            this.refOrOutAddress = savedRefout;
            if (e == null) return null;
            else if (!this.AllowPropertiesIndexersAsRef) {
              Indexer eAsIndexer = e as Indexer;
              if (eAsIndexer != null && eAsIndexer.CorrespondingDefaultIndexedProperty != null) {
                this.HandleError(e, Error.NotAssignable);
                return null;
              }
              MemberBinding eAsMB = e as MemberBinding;
              if (eAsMB != null && eAsMB.BoundMember is Property) {
                this.HandleError(e, Error.NotAssignable);
                return null;
              }
            }
            if (unaryExpression.NodeType != NodeType.OutAddress)
              e = this.VisitExpression(e);
            if (e == null) return null;
            MemberBinding mb = e as MemberBinding;
            if (mb != null) {
              Field f = mb.BoundMember as Field;
              if (f != null) {
                if (f.IsVolatile)
                  this.HandleError(unaryExpression.Operand, Error.VolatileByRef, this.GetMemberSignature(f));
              } else if (mb.BoundMember is Property)
                e = new LRExpression(e);
            } else {
              Indexer indxr = e as Indexer;
              if (indxr != null && indxr.CorrespondingDefaultIndexedProperty != null)
                e = new LRExpression(e);
            }
            opnd = unaryExpression.Operand = e;
            break;
          }
        default:
          opnd = unaryExpression.Operand = this.VisitExpression(unaryExpression.Operand);
          break;
      }
      if (opnd == null) return null;
      switch (unaryExpression.NodeType) {
        //TODO: deal with SkipCheck and EnforceCheck
        case NodeType.DefaultValue:
        case NodeType.Sizeof:
        case NodeType.Typeof:
          if (opnd == null) return null;
          Literal lit = opnd as Literal;
          if (lit == null || !(lit.Value is TypeNode)) {
            Debug.Assert(false);
            return null;
          }
          if (unaryExpression.NodeType == NodeType.Sizeof) {
            TypeNode t = (TypeNode)lit.Value;
            if (!t.IsUnmanaged) {
              this.HandleError(unaryExpression.Operand, Error.ManagedAddr, this.GetTypeName(t));
              if (!this.typeSystem.insideUnsafeCode) {
                this.HandleError(unaryExpression, Error.SizeofUnsafe, this.GetTypeName(t));
              }
              return null;
            }
          }
          return unaryExpression;
        case NodeType.LogicalNot: {
            TypeNode t = unaryExpression.Type;
            if (t == null) return null;
            if (this.typeSystem.IsNullableType(t)) t = this.typeSystem.RemoveNullableWrapper(t);
            if (t != SystemTypes.Boolean) {
              this.HandleError(unaryExpression, Error.BadUnaryOp, "!", this.GetTypeName(t));
              return null;
            }
            Expression e = this.typeSystem.ImplicitCoercion(unaryExpression.Operand, SystemTypes.Boolean, this.TypeViewer);
            if (e != null)
              unaryExpression.Operand = e;
            return unaryExpression;
          }
        case NodeType.Neg: {
            TypeNode t =  this.typeSystem.Unwrap(opnd.Type);
            if (t == null) return null;
            if (this.typeSystem.IsNullableType(t)) t = this.typeSystem.RemoveNullableWrapper(t);
            if ((!t.IsPrimitiveNumeric && t != SystemTypes.Char && t != SystemTypes.Decimal) || t.TypeCode == TypeCode.UInt64) {
              this.HandleError(unaryExpression, Error.BadUnaryOp, "-", this.GetTypeName(t));
              return null;
            }
            t = unaryExpression.Type;
            if (t == null) return null;
            opnd = this.typeSystem.TryImplicitCoercion(unaryExpression.Operand, t, this.TypeViewer);
            if (opnd != null)
              unaryExpression.Operand = opnd;
            if (this.typeSystem.checkOverflow && opnd != null && t.IsPrimitiveInteger) {
              BinaryExpression be = new BinaryExpression(Literal.Int32Zero, opnd, NodeType.Sub_Ovf);
              if (t.IsUnsignedPrimitiveNumeric)
                be.NodeType = NodeType.Sub_Ovf_Un;
              if (t.TypeCode == TypeCode.Int64 || t.TypeCode == TypeCode.UInt64)
                be.Operand1 = Literal.Int64Zero;
              be.Type = unaryExpression.Type;
              be.SourceContext = unaryExpression.SourceContext;
              return be;
            }
            goto default;
          }
        default:
          return unaryExpression;
      }
    }
    public override ExpressionList VisitLoopInvariantList(ExpressionList expressions) {
      if (expressions == null) return null;
      for (int i = 0; i < expressions.Count; i++) {
        expressions[i] = this.VisitBooleanExpression(expressions[i]);
      }
      return expressions;
    }
    public override Statement VisitWhile(While While) {
      if (While == null) return null;
      this.loopCount++;
      While.Condition = this.VisitBooleanExpression(While.Condition);
      While.Invariants = this.VisitLoopInvariantList(While.Invariants);
      While.Body = this.VisitBlock(While.Body);
      this.loopCount--;
      return While;
    }
    public override Expression VisitSetterValue(SetterValue value) {
      if (value == null) return null;
      //TODO: complain if current method is not a property setter
      return value;
    }
    public override Statement VisitAcquire(Acquire acquire) {
      if (@acquire == null) return null;
      this.requireInitializer = true;
      @acquire.Target = (Statement)this.Visit(@acquire.Target);
      this.requireInitializer = false;
      TypeNode t = null;
      ExpressionStatement estat = @acquire.Target as ExpressionStatement;
      if (estat != null)
        t = estat.Expression == null ? null : estat.Expression.Type;
      else
        t = ((LocalDeclarationsStatement)@acquire.Target).Type;
      if (t != null) {
        TypeNode tprime = TypeNode.StripModifiers(t);
        if (tprime.Contract == null || tprime.Contract.FramePropertyGetter == null)
          this.HandleError(@acquire.Target, Error.TypeOfExprMustBeGuardedClass);
      }
      @acquire.Condition = this.VisitExpression(@acquire.Condition);
      @acquire.ConditionFunction = this.VisitExpression(@acquire.ConditionFunction);
      bool savedInsideTryBlock = this.insideTryBlock;
      this.insideTryBlock = true;
      @acquire.Body = this.VisitBlock(@acquire.Body);
      this.insideTryBlock = savedInsideTryBlock;
      return @acquire;
    }
    public override Statement VisitResourceUse(ResourceUse resourceUse) {
      if (resourceUse == null) return null;
      this.requireInitializer = true;
      resourceUse.ResourceAcquisition = (Statement)this.Visit(resourceUse.ResourceAcquisition);
      this.requireInitializer = false;
      TypeNode t;
      ExpressionStatement estat = resourceUse.ResourceAcquisition as ExpressionStatement;
      if (estat != null) {
        if (estat.Expression == null) return null;
        t = TypeNode.StripModifier(estat.Expression.Type, SystemTypes.NonNullType);
      } else
        t = TypeNode.StripModifier(((LocalDeclarationsStatement)resourceUse.ResourceAcquisition).Type, SystemTypes.NonNullType);
      if (t != null && !this.typeSystem.ImplicitCoercionFromTo(t, SystemTypes.IDisposable, this.TypeViewer))
        this.HandleError(resourceUse.ResourceAcquisition, Error.NoImplicitCoercion, this.GetTypeName(t), this.GetTypeName(SystemTypes.IDisposable));
      bool savedInsideTryBlock = this.insideTryBlock;
      this.insideTryBlock = true;
      resourceUse.Body = this.VisitBlock(resourceUse.Body);
      this.insideTryBlock = savedInsideTryBlock;
      return resourceUse;
    }
    public override Expression VisitStackAlloc(StackAlloc alloc) {
      if (alloc == null) return null;
      if (this.insideCatchClause || this.currentFinallyClause != null) {
        this.HandleError(alloc, Error.StackallocInCatchFinally);
        return null;
      }
      return base.VisitStackAlloc(alloc);
    }

    public override Struct VisitStruct(Struct Struct) {
      Struct = base.VisitStruct((Struct)Struct);
      if (Struct == null) return null;
      MemberList members = Struct.Members;  // just check the members syntactically in this type or extension
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++) {
        Member mem = members[i];
        if (mem == null) continue;
        if (mem.IsFamily || mem.IsFamilyAndAssembly || mem.IsFamilyOrAssembly) {
          this.HandleProtectedStructMember(Struct, mem);
        }
      }
      // C# (and thus Spec#) marks all structs as having SequentialLayout unless they have been
      // explicitly decorated as having something else.
      if ((Struct.Flags & TypeFlags.LayoutMask) == TypeFlags.AutoLayout &&
          (Struct.Flags & TypeFlags.LayoutOverridden) == 0) {
        Debug.Assert(TypeFlags.AutoLayout == 0); // otherwise we need to &= ~LayoutMask here.
        Struct.Flags |= TypeFlags.SequentialLayout;
      }
      return Struct;
    }
    public virtual void HandleProtectedStructMember(Struct Struct, Member mem) {
      Method meth = mem as Method;
      if (meth != null) {
        if (!meth.OverridesBaseClassMember)
          this.HandleError(mem.Name, Error.FamilyInStruct, this.GetMethodSignature(meth));
      } else {
        this.HandleError(mem.Name, Error.FamilyInStruct, this.GetMemberSignature(mem));
      }
    }
    private TypeNode GetGoverningType(TypeNode type) {
      if (type == null) return null;
      if (type.IsPrimitiveInteger || type == SystemTypes.Char || type == SystemTypes.String || type == SystemTypes.Boolean || type is EnumNode)
        return type;
      if (TypeNode.StripModifiers(type) == SystemTypes.String) { return SystemTypes.String; }
      MemberList coercions = this.GetTypeView(type).ImplicitCoercionMethods;
      Method coercion = null;
      for (int i = 0, n = coercions == null ? 0 : coercions.Count; i < n; i++) {
        Method c = coercions[i] as Method;
        if (c == null) continue;
        if (c.Parameters == null || c.Parameters.Count != 1 || c.Parameters[0].Type != type) continue;
        TypeNode rt = TypeNode.StripModifiers(c.ReturnType);
        if (rt.IsPrimitiveInteger || rt == SystemTypes.Char || rt == SystemTypes.String || type == SystemTypes.Boolean || rt is EnumNode) {
          if (coercion != null) return null;
          coercion = c;
        }
      }
      if (coercion != null) return coercion.ReturnType;
      return null;
    }
    public override Statement VisitSwitch(Switch Switch) {
      if (Switch == null) return null;
      this.CheckForDuplicateDeclarations(Switch.Scope);
      Expression swexpr = this.VisitExpression(Switch.Expression);
      if (swexpr == null) return null;
      TypeNode swexprType = swexpr.Type;
      Reference r = swexprType as Reference;
      if (r != null) swexprType = r.ElementType;
      if (this.typeSystem.IsNullableType(swexprType)) {
        Switch.Nullable = new Local(swexprType);
        Switch.NullableExpression = swexpr;
        swexprType = this.typeSystem.RemoveNullableWrapper(swexprType);
        Switch.Expression = swexpr = this.typeSystem.ExplicitCoercion(Switch.Nullable, swexprType, this.TypeViewer);
      }
      TypeNode govType = this.GetGoverningType(swexprType);
      if (govType == null)
        this.HandleError(swexpr, Error.IntegralTypeValueExpected);
      else {
        if (swexprType != swexpr.Type) swexpr = this.typeSystem.ExplicitCoercion(swexpr, swexprType, this.TypeViewer);
        Switch.Expression = swexpr = this.typeSystem.ImplicitCoercion(swexpr, govType, this.TypeViewer);
      }
      TypeNode savedGoverningType = this.currentSwitchGoverningType;
      this.currentSwitchGoverningType = govType;
      SwitchCaseList cases = Switch.Cases;
      if (swexpr == null || cases == null) return null;
      Literal swlit = swexpr as Literal;
      int n = cases.Count;
      SwitchCaseList savedCases = this.currentSwitchCases;
      this.currentSwitchCases = cases;
      Hashtable alreadySeenLabelValues = new Hashtable();
      for (int i = 0; i < n; i++) {
        SwitchCase scase = cases[i];
        if (scase == null) continue;
        Literal lit = scase.Label as Literal;
        if (lit == null && scase.Label != null) {
          MemberBinding mb = scase.Label as MemberBinding;
          if (mb != null && mb.BoundMember is Field && ((Field)mb.BoundMember).IsLiteral)
            lit = ((Field)mb.BoundMember).DefaultValue;
          if (lit == null) {
            this.HandleError(scase.Label, Error.ConstantExpected);
            cases[i] = null; continue;
          }
        }
        if (lit != null && govType != null) {
          if (Switch.Nullable == null || !Literal.IsNullLiteral(lit))
            lit = this.typeSystem.ImplicitLiteralCoercion(lit, lit.Type, govType, this.TypeViewer);
          if (lit != null && lit.Value != null) {
            Node prev = (Node)alreadySeenLabelValues[lit.Value];
            if (prev != null) {
              this.HandleError(scase.Label, Error.DuplicateCaseLabel, "case "+scase.Label.SourceContext.SourceText+":");
              this.HandleError(prev, Error.RelatedErrorLocation);
            }
            alreadySeenLabelValues[lit.Value] = scase.Label;
            if (swlit != null && swlit.Value != null && lit.Value != null && !swlit.Value.Equals(lit.Value)) {
              this.HandleError(scase, Error.UnreachableCode);
              if (scase.Body != null && scase.Body.Statements != null && scase.Body.Statements.Count > 0 &&
                scase.Body.Statements[scase.Body.Statements.Count-1] != null && 
                scase.Body.Statements[scase.Body.Statements.Count-1].NodeType == NodeType.SwitchCaseBottom)
                scase.Body.Statements[scase.Body.Statements.Count-1] = null;
              scase.IsErroneous = true;
            }
          }
          scase.Label = lit;
        }
      }
      this.switchCaseCount++;
      for (int i = 0; i < n; i++) {
        SwitchCase scase = cases[i];
        if (scase == null) continue;
        scase.Body = this.VisitBlock(scase.Body);
      }
      this.switchCaseCount--;
      this.currentSwitchCases = savedCases;
      this.currentSwitchGoverningType = savedGoverningType;
      return Switch;
    }
    public override SwitchCase VisitSwitchCase(SwitchCase switchCase) {
      if (switchCase == null) return null;
      this.switchCaseCount++;
      switchCase.Body = this.VisitBlock(switchCase.Body);
      this.switchCaseCount--;
      return switchCase;
    } 
    public override Expression VisitTargetExpression(Expression target) {
      if (target == null) return null;
      switch (target.NodeType) {
        case NodeType.AddressDereference:
          return this.VisitAddressDereference((AddressDereference)target);
        case NodeType.MemberBinding:
          return this.VisitMemberBinding((MemberBinding)target, true);
        case NodeType.Indexer:
          Indexer indxr = (Indexer)target;
          indxr.Object = this.VisitExpression(indxr.Object);
          if (indxr.Object == null || indxr.Object.Type == null) return null;
          TypeNode obType = this.typeSystem.Unwrap(indxr.Object.Type, true);
          if (indxr.CorrespondingDefaultIndexedProperty != null) {
            if (indxr.CorrespondingDefaultIndexedProperty.Setter == null && !(indxr.CorrespondingDefaultIndexedProperty.Type is Reference)) {
              this.HandleError(target, Error.NoSetter, this.GetMemberSignature(indxr.CorrespondingDefaultIndexedProperty));
              return null;
            } else if (indxr.Object != null) {
              TypeNode targetType = TypeNode.StripModifiers(indxr.Object.Type);
              if (indxr.Object is Base) targetType = this.currentType;
              if (indxr.Object != null && this.NotAccessible(indxr.CorrespondingDefaultIndexedProperty.Setter, ref targetType)) {
                if (targetType == null && indxr.Object.Type != null)
                  this.HandleError(indxr.Object, Error.NotVisibleViaBaseType, this.GetMemberSignature(indxr.CorrespondingDefaultIndexedProperty),
                    this.GetTypeName(indxr.Object.Type), this.GetTypeName(this.currentType));
                else
                  this.HandleError(indxr.Object, Error.MemberNotVisible, this.GetMemberSignature(indxr.CorrespondingDefaultIndexedProperty));
                return null;
              }
            }
            indxr.Object = this.typeSystem.AutoDereferenceCoercion(indxr.Object);
            this.CoerceArguments(indxr.CorrespondingDefaultIndexedProperty.Parameters, ref indxr.Operands, false, CallingConventionFlags.Default);
          } else {
            if (obType.IsPointerType) {
              if (target.Type == SystemTypes.Void) {
                this.HandleError(indxr.Object, Error.VoidError);
                return null;
              }
              if (indxr.Operands != null) {
                if (indxr.Operands.Count != 1) {
                  this.HandleError(indxr, Error.PointerMustHaveSingleIndex);
                } else {
                  indxr.Operands[0] = this.typeSystem.CoerceToIndex(this.VisitExpression(indxr.Operands[0]), this.TypeViewer);
                }
              }
              return indxr;
            }
            //Check that the object is an array
            ArrayType arr = this.typeSystem.Unwrap(indxr.Object.Type, true) as ArrayType;
            if (arr == null) {
              if (this.TypeInVariableContext(indxr.Object as Literal)) return null;
              this.HandleError(indxr.Object, Error.NotIndexable, this.GetTypeName(indxr.Object.Type));
              return null;
            }
            indxr.Object = this.typeSystem.ExplicitCoercion(indxr.Object, arr, this.TypeViewer);
            int rank = arr.Rank;
            ExpressionList indices = indxr.Operands;
            int n = indices == null ? 0 : indices.Count;
            if (n != rank) {
              this.HandleError(indxr, Error.WrongNumberOfIndices, rank.ToString());
              return null;
            }
            for (int i = 0; i < rank; i++)
              indices[i] = this.typeSystem.CoerceToIndex(this.VisitExpression(indices[i]), this.TypeViewer);
          }
          return target;
        case NodeType.Local:
        case NodeType.Parameter:
          return target;
        case NodeType.Literal:
          object litVal = ((Literal)target).Value;
          TypeNode t = litVal as TypeNode;
          if (t != null) {
            this.HandleError(target, Error.AssignmentToType, this.GetMemberName(t), "type", "variable");
            return null;
          }
          goto default;
        case NodeType.LRExpression:
          this.VisitTargetExpression(((LRExpression)target).Expression);
          return target;
        case NodeType.Base:
          this.HandleError(target, Error.AssignmentToBase);
          return null;
        case NodeType.ConstructTuple:
          ConstructTuple ctup = (ConstructTuple)target;
          FieldList fields = ctup.Fields;
          for (int i = 0, n = fields == null ? 0 : fields.Count; i < n; i++) {
            Field f = fields[i];
            if (f == null) continue;
            if (f.Name != null && !f.IsAnonymous)
              this.HandleError(f, Error.NotAssignable);
            else
              f.Initializer = this.VisitTargetExpression(f.Initializer);
          }
          return ctup;
        case NodeType.Parentheses:
          UnaryExpression uex = (UnaryExpression)target;
          uex.Operand = this.VisitTargetExpression(uex.Operand);
          return uex;
        case NodeType.This:
          This thisTarget = VisitThis((This)target) as This;
          if (thisTarget != null) {
            Reference refType = thisTarget.Type as Reference;
            if (refType == null || !refType.ElementType.IsValueType) { // 2nd test seems redundant
              this.HandleError(thisTarget, Error.NotAssignable);
              return null;
            }
            return thisTarget;
          }
          return null;
        default:
          Expression tgt = this.VisitExpression(target);
          if (tgt != null) {
            if (tgt.Type is Reference) { return tgt; }
            this.HandleError(tgt, Error.NotAssignable);
          }
          return null;
      }
    }
    public override Expression VisitThis(This This) {
      if (!this.MayReferenceThisAndBase &&
        (this.currentMethod == null // this is true when it is visited as part of a field initializer
        ||
        !this.insideInvariant // this is true when it is visited as part of a ctor with an explicit call to base in the body        
        )) {
        this.HandleError(This, Error.ThisInBadContext);
        return null;
      } else if (this.currentMethod != null && this.currentMethod.IsStatic) {
        if (this.currentMethod.NodeType == NodeType.Invariant)
          this.HandleError(This, Error.ThisInBadContext);
        else
          this.HandleError(This, Error.ThisInStaticCode);
        return null;
      }
      if (this.NonNullChecking && (This.Type is Class))
        This.Type = OptionalModifier.For(SystemTypes.NonNullType, This.Type);
      return This;
    }
    private bool Has(StringList s) {
      if (s==null) return false;
      for (int i = 0; i<s.Count; i++)
        if (s[i]=="NonNullTypeChecks")
          return true;
      return false;
    }
    public override Statement VisitThrow(Throw Throw) {
      if (Throw == null) return null;
      TypeNode thrownType = null;
      if (Throw.Expression == null) {
        if (!this.insideCatchClause) {
          this.HandleError(Throw, Error.BadEmptyThrow);
          return null;
        }
        Throw.NodeType = NodeType.Rethrow;
        thrownType = this.currentCatchClause.Type != null ? this.currentCatchClause.Type : SystemTypes.Object;
      } else {
        Expression e = Throw.Expression = this.VisitExpression(Throw.Expression);
        if (e == null || e.Type == null) return null;
        thrownType = e.Type;
        if (thrownType != null)
          thrownType = this.typeSystem.Unwrap(thrownType);
        if (!this.GetTypeView(thrownType).IsAssignableTo(SystemTypes.Exception)) {
          this.HandleError(e, Error.BadExceptionType);
          return null;
        }
        e = Throw.Expression = this.typeSystem.ImplicitCoercion(e, thrownType, this.TypeViewer);
      }
      #region Dealing with checked/unchecked exceptions
      if (this.GetTypeView(thrownType).IsAssignableTo(SystemTypes.ICheckedException)) {
        int i = 0;
        int n = this.allowedExceptions.Count;
        while (i < n) {
          TypeNode t = this.allowedExceptions[i];
          if (this.GetTypeView(thrownType).IsAssignableTo(t))
            break;
          i++;
        }
        if (i == n) // no allowed exception covers this one
          this.HandleError(Throw, Error.CheckedExceptionNotInThrowsClause, this.GetTypeName(thrownType), this.GetMethodSignature(this.currentMethod));
      } else if (
        (this.currentPreprocessorDefinedSymbols == null || !this.currentPreprocessorDefinedSymbols.ContainsKey("compatibility"))
        &&
        !this.currentOptions.DisableDefensiveChecks
        ) {
        // if we can't be sure the thrown exception's *dynamic* type is an unchecked exception, then need to
        // insert a runtime check. Just because its static type is not a subtype of ICheckedException
        // doesn't mean its runtime type isn't, since CheckedException is a subtype of Exception.
        //
        // BUT: do this only if compiler options say to! This is to preserve C# backwards compatibility.
        if (Throw.Expression == null) {
          // if Throw.Expression == null ==> this was "throw;" in a catch block, definitely need the check
          // need to know if there is a variable because it could have been "catch {}" or "catch (Exception){}"
          Block b = new Block(new StatementList(2));
          b.HasLocals = true;
          // now the really bad case: need to transform "catch" or "catch (Exception){}" into "catch (Exception e)"
          if (this.currentCatchClause.Type == null)
            this.currentCatchClause.Type = SystemTypes.Exception;
          if (this.currentCatchClause.Variable == null) {
            Local e = new Local(Identifier.For("SS$exception local"), this.currentCatchClause.Type, b);
            this.currentCatchClause.Variable = e;
          }
          b.Statements.Add(new Assertion(
            new UnaryExpression(
            new BinaryExpression(this.currentCatchClause.Variable, new MemberBinding(null, SystemTypes.ICheckedException), NodeType.Is, SystemTypes.Boolean),
            NodeType.LogicalNot)));
          b.Statements.Add(Throw);
          return b;
        } else if (Throw.Expression.NodeType != NodeType.Construct) {
          // if it is "throw new T" where T is not a checked exception, then we don't need the assert
          Block b = new Block(new StatementList(3));
          b.HasLocals = true;
          // Exception e = Throw.Expression;
          // Assert(!(e is ICheckedException) );
          // throw e;
          Local e = new Local(Identifier.For("SS$exception local"), SystemTypes.Exception, b);
          b.Statements.Add(new AssignmentStatement(e, Throw.Expression));
          b.Statements.Add(new Assertion(
            new UnaryExpression(
            new BinaryExpression(e, new MemberBinding(null, SystemTypes.ICheckedException), NodeType.Is, SystemTypes.Boolean),
            NodeType.LogicalNot)));
          Throw.Expression = e;
          b.Statements.Add(Throw);
          return b;
        }
      }
      #endregion Dealing with checked/unchecked exceptions
      return Throw;
    }
    public override Statement VisitTry(Try Try) {
      if (Try == null) return null;
      bool savedInsideTryBlock = this.insideTryBlock;
      this.insideTryBlock = true;

      // when visiting the try block, use a newly computed set of allowed exceptions.
      // the set is whatever the current set is augmented with the set of exceptions listed in the
      // catch clauses
      TypeNodeList newAllowedExceptions = this.allowedExceptions.Clone();
      CatchList cl = Try.Catchers;
      if (cl != null) {
        for (int i = 0, n = cl.Count; i < n; i++) {
          Catch c = cl[i];
          if (c == null) continue;
          // BUGBUG? In both cases, should we just automatically add to the list or first see if
          // some exception already in the list is a supertype of the one that is currently added?
          if (c.Type == null)
            // then this was "catch { }" meaning catch everything, so all checked exceptions will be caught
            newAllowedExceptions.Add(SystemTypes.ICheckedException);
          else if (this.GetTypeView(c.Type).IsAssignableTo(SystemTypes.ICheckedException))
            newAllowedExceptions.Add(c.Type);
        }
      }
      TypeNodeList saveAllowedExceptions = this.allowedExceptions;
      this.allowedExceptions = newAllowedExceptions;

      /* can't call the base visitor because after visiting the try block, the allowedExceptions
       * need to be restored before visiting the catchers.
       * So this is a copy of the body of StandardVisitor.VisitTry. If that ever changes, this
       * will need to be changed too!
      Statement result = base.VisitTry(Try);
      */
      Try.TryBlock = this.VisitBlock(Try.TryBlock);

      /* restore the list of allowed exceptions */
      this.allowedExceptions = saveAllowedExceptions;

      Try.Catchers = this.VisitCatchList(Try.Catchers);
      Try.Filters = this.VisitFilterList(Try.Filters);
      Try.FaultHandlers = this.VisitFaultHandlerList(Try.FaultHandlers);
      Try.Finally = (Finally)this.VisitFinally(Try.Finally);

      this.insideTryBlock = savedInsideTryBlock;
      CatchList catchers = Try.Catchers;
      if (catchers != null) {
        for (int i = 0, n = catchers.Count; i < n; i++) {
          Catch c = catchers[i];
          if (c == null) continue;
          if (c.Type == null) continue;
          for (int j = 0; j < i; j++) {
            Catch c0 = catchers[j];
            if (c0 == null || c0.Type == null) continue;
            if (this.GetTypeView(c.Type).IsAssignableTo(c0.Type))
              this.HandleError(c.TypeExpression, Error.UnreachableCatch, this.GetTypeName(c0.Type));
          }
        }
      }
      return Try;
    }
    public virtual AttributeNode VisitTypeAttribute(AttributeNode attr, TypeNode type) {
      if (attr == null || type == null) return null;
      MemberBinding mb = attr.Constructor as MemberBinding;
      if (mb == null || mb.BoundMember == null) return null;
      TypeNode attrType = mb.BoundMember.DeclaringType;
      if (attrType == SystemTypes.StructLayoutAttribute) {
        // We have to record that the default layout behavior has been overridden for this type
        // because some languages like C# override the CLR default (e.g. Sequential for structs)
        type.Flags |= TypeFlags.LayoutOverridden;
        ExpressionList args = attr.Expressions;
        if (args == null || args.Count < 1) { Debug.Assert(false); return null; }
        Literal lit = args[0] as Literal;
        if (lit == null || !(lit.Value is int)) return null;
        LayoutKind kind = (LayoutKind)(int)lit.Value;
        type.Flags &= ~TypeFlags.LayoutMask;
        switch (kind) {
          case LayoutKind.Explicit: type.Flags |= TypeFlags.ExplicitLayout; break;
          case LayoutKind.Sequential: type.Flags |= TypeFlags.SequentialLayout; break;
          default: type.Flags |= TypeFlags.AutoLayout; break;
        }
        for (int j = 1, m = args.Count; j < m; j++) {
          NamedArgument nArg = args[j] as NamedArgument;
          if (nArg == null) continue;
          lit = nArg.Value as Literal;
          if (lit == null) continue;
          if (nArg.Name.UniqueIdKey == StandardIds.CharSet.UniqueIdKey) {
            if (lit.Value is int) {
              type.Flags &= ~TypeFlags.StringFormatMask;
              switch ((CharSet)(int)lit.Value) {
                case CharSet.Ansi: type.Flags |= TypeFlags.AnsiClass; break;
                case CharSet.Auto: type.Flags |= TypeFlags.AutoClass; break;
                case CharSet.Unicode: type.Flags |= TypeFlags.UnicodeClass; break;
                default:
                  this.HandleError(nArg, Error.CustomAttributeError, this.GetTypeName(SystemTypes.StructLayoutAttribute), "CharSet");
                  break;
              }
            }
          } else if (nArg.Name.UniqueIdKey == StandardIds.Pack.UniqueIdKey) {
            if (lit.Value is int) {
              int size = (int)lit.Value;
              if (size == 0 || size == 1 || size == 2 || size == 4 || size == 8 || size == 16 || size == 32 || size == 64 || size == 128)
                type.PackingSize = (int)lit.Value;
              else
                this.HandleError(nArg, Error.CustomAttributeError, this.GetTypeName(SystemTypes.StructLayoutAttribute), "Pack");
            }
          } else if (nArg.Name.UniqueIdKey == StandardIds.Size.UniqueIdKey) {
            if (lit.Value is int) {
              int size = (int)lit.Value;
              if (size >= 0)
                type.ClassSize = (int)lit.Value;
              else
                this.HandleError(nArg, Error.CustomAttributeError, this.GetTypeName(SystemTypes.StructLayoutAttribute), "Size");
            }
          }
        }
        return null;
      }
      if (attrType == SystemTypes.AttributeUsageAttribute) {
        if (!this.GetTypeView(type).IsAssignableTo(SystemTypes.Attribute)) {
          this.HandleError(attr.Constructor, Error.AttributeUsageOnNonAttributeClass, "AttributeUsage");
          return null;
        }
        ExpressionList args = attr.Expressions;
        if (args != null && args.Count > 0) {
          Literal lit = args[0] as Literal;
          if (lit != null && lit.Value is int) {
            int flags = (int)lit.Value;
            if (flags == 0 || (flags & ~(int)AttributeTargets.All) != 0) {
              this.HandleError(lit, Error.InvalidAttributeArgument, "AttributeUsage");
              args[0] = lit = new Literal((int)AttributeTargets.All, lit.Type, lit.SourceContext);
            }
          }
        }
      }
      if (attrType == SystemTypes.GuidAttribute) {
        ExpressionList args = attr.Expressions;
        if (args != null && args.Count > 0) {
          Literal lit = args[0] as Literal;
          if (lit != null && lit.Value is string) {
            string guid = (string)lit.Value;
            object g = null;
            if (guid != null) {
              try {
                g = new Guid(guid);
              } catch (FormatException) { }
            }
            if (g == null) {
              this.HandleError(lit, Error.InvalidAttributeArgument, "Guid");
              args[0] = lit = new Literal(Guid.NewGuid().ToString(), lit.Type, lit.SourceContext);
            }
          }
        }
      }
      if (attrType == SystemTypes.InterfaceTypeAttribute) {
        ExpressionList args = attr.Expressions;
        if (args != null && args.Count > 0) {
          Literal lit = args[0] as Literal;
          if (lit != null && lit.Value is int) {
            int val = (int)lit.Value;
            switch ((System.Runtime.InteropServices.ComInterfaceType)val) {
              case System.Runtime.InteropServices.ComInterfaceType.InterfaceIsDual:
              case System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIDispatch:
              case System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown:
                break;
              default:
                this.HandleError(lit, Error.InvalidAttributeArgument, "InterfaceType");
                args[0] = lit = new Literal((int)System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown, lit.Type, lit.SourceContext);
                break;
            }
          }
        }
      }
      if (attrType == SystemTypes.ComImportAttribute) {
        AttributeList attrs = type.Attributes;
        for (int i = 0, n = attrs == null ? 0 : attrs.Count; i < n; i++) {
          attr = attrs[i];
          if (attr == null) continue;
          mb = attr.Constructor as MemberBinding;
          if (mb == null || mb.BoundMember == null) continue;
          attrType = mb.BoundMember.DeclaringType;
          if (attrType != SystemTypes.GuidAttribute) continue;
          return attr;
        }
        this.HandleError(type.Name, Error.ComImportWithoutGuidAttribute);
      }
      if (attrType == SystemTypes.SerializableAttribute) {
        type.Flags |= TypeFlags.Serializable;
        return null;
      }
      return attr;
    }
    public override TypeNode VisitTypeNode(TypeNode typeNode) {
      if (typeNode == null) return null;
      TypeNode savedCurrentType = this.currentType;
      if (typeNode.IsNormalized) {
        this.currentType = this.typeSystem.currentType = typeNode;
        this.VisitMemberList(typeNode.Members);
        this.currentType = this.typeSystem.currentType = savedCurrentType;
        return typeNode;
      }
      if (typeNode.Template == this.currentType && typeNode.IsNotFullySpecialized) return typeNode;
      if (typeNode.PartiallyDefines != null) {
        if (this.visitedCompleteTypes == null) this.visitedCompleteTypes = new TrivialHashtable();
        if (this.visitedCompleteTypes[typeNode.PartiallyDefines.UniqueKey] == null) {
          this.VisitTypeNode(typeNode.PartiallyDefines);
          this.visitedCompleteTypes[typeNode.PartiallyDefines.UniqueKey] = typeNode;
        }
        return typeNode;
      }
      typeNode.Attributes = this.VisitAttributeList(typeNode.Attributes);
      //Flatten interface list
      InterfaceList interfaces = this.GetTypeView(typeNode).Interfaces;
      for (int i = 0, n = interfaces == null ? 0 : interfaces.Count; i < n; i++) {
        Interface iface = interfaces[i];
        if (iface == null || iface is TypeParameter) continue;
        if (this.GetTypeView(iface).IsAssignableTo(typeNode)) {
          this.HandleError(typeNode.Name, Error.CycleInInterfaceInheritance, this.GetTypeName(iface), this.GetTypeName(typeNode));
          if (iface != typeNode) this.HandleRelatedError(iface);
          for (int j = i; j < n-1; j++)
            interfaces[j] = interfaces[j+1];
          interfaces.Count = n-1;
          continue;
        }
        if (typeNode.NodeType == NodeType.Interface) {
          if (this.IsLessAccessible(iface, typeNode)) {
            this.HandleError(typeNode.Name, Error.BaseInterfaceLessAccessible, this.GetTypeName(iface), this.GetTypeName(typeNode));
            this.HandleRelatedError(iface);
          }
        }
        InterfaceList inheritedInterfaces = this.GetTypeView(iface).Interfaces;
        int m = inheritedInterfaces == null ? 0 : inheritedInterfaces.Count;
        for (int j = 0; j < m; j++) {
          Interface iiface = inheritedInterfaces[j];
          if (iiface == null) continue;
          bool mustAddInterface = true;
          for (int k = 0; k < n; k++) {
            if (interfaces[k] == iiface) {
              mustAddInterface = false;
              break;
            }
          }
          if (mustAddInterface) {
            interfaces.Add(iiface);
            n++;
          }
        }
      }
      typeNode.Attributes = this.VisitAttributeList(typeNode.Attributes, typeNode);
      this.currentType = this.typeSystem.currentType = typeNode;
      this.CheckHidingAndOverriding(typeNode);            
      #region Deal with modelfields that are inherited from implemented interfaces.
      if (typeNode is Class) {
        StringCollection implementedModelfields = new StringCollection(); //contains the names of modelfields implemented so far
        foreach (Interface implInterface in typeNode.Interfaces) {
          if (implInterface == null) continue; //Why is Interfaces initialized to a List with a single null element? Means this check is essential.
          if (implInterface.Contract != null)
          {
            foreach (ModelfieldContract mfCToImplement in implInterface.Contract.ModelfieldContracts)
            {
              #region implement mfCToImplement in typeNode
              String fieldnameToImplement = mfCToImplement.Modelfield.Name.Name;
              if (implementedModelfields.Contains(fieldnameToImplement))
              {
                this.HandleError(typeNode, Error.GenericError, "Class " + typeNode.Name.Name + " cannot implement two interfaces that both define a model field " + fieldnameToImplement);
                continue; //ignore this contract
                //Disallowed to prevent the unexpected modification of a modelfield in one interface by changing a modelfield in another interface.              
              }
              else
              {
                implementedModelfields.Add(fieldnameToImplement);
                ModelfieldContract mfCThatImplements = null; //represents the contract that will implement mfCToImplement
                Member implementingMember = null;
                #region if typeNode or a superclass already defines a member named fieldNameToImplement, store it in implementingMember.
                for (TypeNode classWithField = typeNode; classWithField != null && implementingMember == null; classWithField = classWithField.BaseType)
                {
                  MemberList members = this.GetTypeView(classWithField).GetMembersNamed(mfCToImplement.Modelfield.Name);
                  foreach (Member m in members)
                  {
                    if (m.Name.Name == fieldnameToImplement)
                    {
                      implementingMember = m;
                      break; //implementing member found; stop looking
                    }
                  }
                }
                #endregion
                #region if there is an implentingMember: if it is a modelfield in typeNode, then store its contract in mfCThatImplements, else complain
                if (implementingMember != null && implementingMember.DeclaringType != typeNode)
                {
                  this.HandleError(typeNode, Error.GenericError, "Class " + typeNode.Name.Name + " does not define a model field " + fieldnameToImplement + " that implements " + mfCToImplement.Modelfield.FullName + " and hides " + implementingMember.FullName);
                  this.HandleRelatedError(mfCToImplement.Modelfield);
                  this.HandleRelatedError(implementingMember);  //TODO: suppress error typeNode does not implement implInterface.fieldnameToImplement.get
                  continue; //ignore this contract
                  //Disallowed to prevent the unexpected modification of a superclass member by changing a modelfield in an interface/the unexpected hiding of a superclass member by a modelfield in an interface.
                }
                if (implementingMember != null && !(implementingMember is Field && (implementingMember as Field).IsModelfield))
                {
                  this.HandleError(typeNode, Error.GenericError, "Class " + typeNode.Name.Name + " cannot implement " + mfCToImplement.Modelfield.FullName + " as it contains a non-modelfield member of that name");
                  this.HandleRelatedError(mfCToImplement.Modelfield);
                  this.HandleRelatedError(implementingMember); //TODO: suppress error typeNode does not implement implInterface.fieldnameToImplement.get
                  continue; //ignore this contract
                }
                if (implementingMember != null)
                {
                  //typeNode defines a modelfield (i.e., implementingMember) that can implement mfCToImplement
                  Debug.Assert(typeNode.Contract != null); //a class that defines a modelfield must have a modelfieldcontract that applies to it. 
                  foreach (ModelfieldContract mfC in typeNode.Contract.ModelfieldContracts)
                    if (mfC.Modelfield == implementingMember)
                    {
                      mfCThatImplements = mfC;
                      break;
                    }
                  Debug.Assert(mfCThatImplements != null);
                }
                #endregion
                #region if there is no implementingMember: add a new modelfield + contract to typeNode and store contract in mfCThatImplements
                //TODO: Unfortunately, qualified identifiers have already been resolved: currently references to the modelfield will produce an error. 
                if (implementingMember == null)
                {
                  Identifier mfIdent = new Identifier(mfCToImplement.Modelfield.Name.Name);
                  mfCThatImplements = new ModelfieldContract(typeNode, new AttributeList(), mfCToImplement.ModelfieldType, mfIdent, typeNode.SourceContext);
                  Field mf = (mfCThatImplements.Modelfield as Field);
                  mf.SourceContext = mfCToImplement.SourceContext; //the modelfield does not appear in the code but implements mfCToImplement.
                  typeNode.Members.Add(mf);
                  if (typeNode.Contract == null)
                    typeNode.Contract = new TypeContract(typeNode);
                  typeNode.Contract.ModelfieldContracts.Add(mfCThatImplements);
                }
                #endregion
                #region Implement the property and property getter that represent mfCToImplement, let getter return mfCThatImplements.Modelfield
                //assert typeNode.Contract.ModelfieldContracts.Contains(mfCThatImplements); 
                //create Property:
                //  public <mfCThatImplements.ModelfieldType> <mfCThatImplements.Modelfield.Name>
                //    ensures result == <mfCThatImplements.Modelfield>;
                //  { [Confined] get { return <mfCThatImplements.Modelfield>; }  } 
                //  Note that getter needs to be confined because it inherits NoDefaultContract
                MemberBinding thisMf = new MemberBinding(new This(typeNode), mfCThatImplements.Modelfield);
                Statement ret = new Return(thisMf);
                Method getter = new Method(typeNode, new AttributeList(), (mfCToImplement.Modelfield as Property).Getter.Name, new ParameterList(), mfCThatImplements.ModelfieldType, new Block(new StatementList(ret)));
                getter.Flags = MethodFlags.Public;
                getter.CallingConvention = CallingConventionFlags.HasThis;
                if (getter.Contract == null)
                {
                  getter.Contract = new MethodContract(getter);
                }
                Expression resultOfGet = new ReturnValue(getter.ReturnType, mfCToImplement.SourceContext);
                BinaryExpression b = new BinaryExpression(resultOfGet, thisMf, NodeType.Eq, mfCToImplement.SourceContext);
                b.Type = SystemTypes.Boolean;
                //Give getter Confined (as it has NoDefaultContract)
                // That means make it [Pure][Reads(Reads.Owned)]
                InstanceInitializer pCtor = SystemTypes.PureAttribute.GetConstructor();
                if (pCtor != null)
                  getter.Attributes.Add(new AttributeNode(new MemberBinding(null, pCtor), null, AttributeTargets.Method));
                InstanceInitializer rCtor = SystemTypes.ReadsAttribute.GetConstructor(); // can use nullary ctor since default is confined
                if (rCtor != null)
                  getter.Attributes.Add(new AttributeNode(new MemberBinding(null, rCtor), null, AttributeTargets.Method));

                getter.Contract.Ensures.Add(new EnsuresNormal(b));
                Identifier implPropName = new Identifier(mfCToImplement.Modelfield.FullName, mfCToImplement.SourceContext); //use full name as typeNode might define modelfield with this name itself.
                Property implementingProperty = new Property(typeNode, new AttributeList(), PropertyFlags.None, implPropName, getter, null);
                typeNode.Members.Add(implementingProperty);
                typeNode.Members.Add(getter);
                #endregion
                #region Copy the info from mfCToImplement to typeNode's mfCThatImplements
                foreach (Expression satClause in mfCToImplement.SatisfiesList)
                  mfCThatImplements.SatisfiesList.Add(satClause);
                //Don't copy the explicit witness from the implemented contract: can likely infer a better one.
                #endregion
              }
              #endregion
            }
          }
        }
      }
      #endregion //needs to happen after CheckHidingAndOverriding, but before CheckAbstractMethods.
      this.CheckAbstractMethods(typeNode); //TODO: suppress duplicate errors generated by template instances
      this.CheckCircularDependency(typeNode);
      this.CheckForDuplicateDeclarations(typeNode);
      // must do this *after* CheckForDuplicateDeclarations
      this.CheckForInterfaceImplementationsOfOutOfBandContractedMethods(typeNode);      
      this.CheckOperatorOverloads(typeNode);
      if (typeNode is Class && typeNode.IsSealed)
        this.CheckForNewFamilyOrVirtualMembers(typeNode);
      this.VisitTemplateInstanceTypes(typeNode);
      this.CheckContractInheritance(typeNode);      
      TypeNode result = base.VisitTypeNode(typeNode);           
      
      #region infer and serialize witnesses where needed (for modelfields and pure methods). ALSO infers postconditions.
      if (this.currentOptions != null && !this.currentOptions.DisablePublicContractsMetadata && !this.currentOptions.DisableInternalContractsMetadata) {
        //Note that this code could move to the boogie end, except that we need a runtime witness for modelfields.      
        //We need to show that the contract of a modelfield mf is consistent in order to use the associated axioms.
        //An inferred witness is a guess at an expression e that will satisfy the contract, i.e., 
        //an expression e such that for each satisfies clause p, p[e/mf] holds. Checking this witness is left to Boogie.
        if (typeNode.Contract != null) {
          foreach (ModelfieldContract mfC in typeNode.Contract.ModelfieldContracts) {
            if (mfC.ModelfieldType == null) continue; //signals error, but will be reported elsewhere
            Expression satisfies = null;
            foreach (Expression sat in mfC.SatisfiesList) { //construct a single expression to take advantage of the fact that multiple clauses act as an &&
              if (sat == null) continue;
              if (satisfies == null)
                satisfies = sat;
              else
                satisfies = new BinaryExpression(sat, satisfies, NodeType.LogicalAnd, SystemTypes.Boolean);
            }
            WUCs witnesses = Checker.GetWitnesses(satisfies, mfC.Modelfield, mfC.ModelfieldType);
            this.SerializeWUCs(witnesses, mfC); //also serializes explicitly specified witnesses.
            if (mfC.Witness == null) {  //we need to set a witness as runtime witness (do this afterwards as it will be serialized otherwise)
              //But as we have only one runtime witness, we can't guarantuee that we pick the right one. For now, just hope for the best.
              if (witnesses.RuntimeWitness != null)
                mfC.Witness = witnesses.RuntimeWitness;
              else
                mfC.Witness = this.GetInferredWitness(mfC); //this calculates a runtime witness 
            }
          }
        }
        foreach (Member m in typeNode.Members) {
          Method method = m as Method;
          if (method == null || method.ReturnType == null) continue;
          if (!method.IsPure && !method.IsConfined && !method.IsStateIndependent) continue;
          if (method.CciKind != CciMemberKind.Regular) continue; //no need to check consistency of methodology method contracts
          if (method.ReturnType == SystemTypes.Void) continue; //no witness needed for void        
          #region infer potential method contract witnesses and add them as WitnessAttributes
          //A pure method can be used in specifications and assert statements. 
          //Therefore, we need to show that the contract of a pure method is consistent in order to use purity axioms.
          //An inferred witness is a guess at an expression e that will satisfy all postconditions, i.e., 
          //an expression e such that for each postcondition p, p[e/result] holds. Checking this witness is left to Boogie.                
          Expression postcondition = null;
          if (method.Contract != null) {
            foreach (Ensures ens in method.Contract.Ensures) {
              if (ens.PostCondition != null) {
                if (postcondition == null)
                  postcondition = ens.PostCondition;
                else
                  postcondition = new BinaryExpression(ens.PostCondition, postcondition, NodeType.LogicalAnd, SystemTypes.Boolean);
              }
            }
          }
          WUCs witnesses = Checker.GetWitnesses(postcondition, null, method.ReturnType);
          #region find witnesses in method code and infer postconditions
          if (method.Body != null && method.Body.Statements != null) {
            WitnessFromCodeFinderVisitor codeWitnessFinder = new WitnessFromCodeFinderVisitor();
            if (!method.IsVirtual && //don't infer postcondition: the absence might be intentional, to give overriding method more freedom               
                (method.Contract == null || method.Contract.Ensures.Count == 0)) //don't infer post if user specified a postcondition
          { //look for inferred postconditions
              codeWitnessFinder.methodToInferPostFrom = method;
            }
            codeWitnessFinder.VisitStatementList(method.Body.Statements);
            if (method.ReturnType != SystemTypes.Boolean && method.ReturnType.NodeType != NodeType.EnumNode) //check if all possible witnesses have already been added, finder was only run to infer postconditions
              foreach (Expression witness in codeWitnessFinder.Witnesses)
                witnesses.Exact.Add(new WitnessUnderConstruction(witness, null, 0));
          }
          #endregion
          this.SerializeWUCs(witnesses, method);
          #endregion
        }
      }
      #endregion

      // do this here so the serialized contracts reflect any modifications made during Checker
      // serialized contracts should be pre-Normalized ASTs, *but* they should not reflect
      // any contract inheritance that has happened.
      // Note that this (possibly) adds things to the Attributes of the typeNode
      // Those attribute nodes will not be checked as part of Checker
      this.SerializeContracts(typeNode);
      

      this.currentType = this.typeSystem.currentType = savedCurrentType;
      return result;
    }
    
    public virtual void SerializeContracts(TypeNode type) {
      Debug.Assert(type != null);
      if (type.Contract != null && this.currentOptions != null && !this.currentOptions.DisableInternalContractsMetadata) {
        TypeContract contract = type.Contract;
        #region serialize invariants
        for (int i = 0, n = contract.Invariants == null ? 0 : contract.Invariants.Count; i < n; i++) {
          Invariant inv = contract.Invariants[i];
          if (inv == null) continue;
          ExpressionList el = SplitConjuncts(inv.Condition);
          for (int j = 0, m = el.Count; j < m; j++) {
            InstanceInitializer ctor = SystemTypes.InvariantAttribute.GetConstructor(SystemTypes.String);
            AttributeNode a = Checker.SerializeExpression(ctor, el[j], this.currentModule);
            type.Attributes.Add(a);
          }
        }
        #endregion
        #region if type is a class, then serialize its modelfieldcontracts
        foreach (ModelfieldContract mfC in contract.ModelfieldContracts) {
          if (!(type is Class)) break;          
          #region Adorn type with ModelfieldContractAttribute containing serialized modelfield and witness of mfC
          Debug.Assert(mfC != null);  //something's wrong in the code.
          if (mfC.Witness == null) continue; //something's wrong in the input ssc (but should already have dealt with)  
          MemberBinding thisMf = new MemberBinding(new This(mfC.Modelfield.DeclaringType), mfC.Modelfield);
          InstanceInitializer wCtor = SystemTypes.ModelfieldContractAttribute.GetConstructor(SystemTypes.String, SystemTypes.String);
          AttributeNode a = Checker.SerializeExpressions(wCtor, new ExpressionList(thisMf, mfC.Witness), mfC.Witness.SourceContext, this.currentModule);
          type.Attributes.Add(a);
          #endregion
          #region For each satisfies clause of mfC, adorn type with SatisfiesAttributes containing serialized field + clauseConjunct
          foreach (Expression satClause in mfC.SatisfiesList) {
            if (satClause == null) continue; //don't report error, has already been done.
            foreach (Expression satConjunct in SplitConjuncts(satClause)) { //storing the conjuncts individually allows better error reporting 
              InstanceInitializer ctor = SystemTypes.SatisfiesAttribute.GetConstructor(SystemTypes.String, SystemTypes.String);
              AttributeNode attr = Checker.SerializeExpressions(ctor, new ExpressionList(thisMf, satConjunct), satConjunct.SourceContext, this.currentModule);
              type.Attributes.Add(attr);
            }
          }
          #endregion
        }
        #endregion
      }
      foreach (Member m in type.Members) {
        if ((m is Field && (m as Field).IsModelfield) || (m is Property && (m as Property).IsModelfield)) {
          #region Slap on ModelfieldAttribute
          InstanceInitializer mfCtor = SystemTypes.ModelfieldAttribute.GetConstructor();
          MemberBinding attrBinding = new MemberBinding(null, mfCtor);
          m.Attributes.Add(new AttributeNode(attrBinding, null));
          #endregion
        }
        Method meth = m as Method;        
        if (meth != null && meth.Contract != null) {
          if (meth.IsVisibleOutsideAssembly ? !(this.currentOptions != null && this.currentOptions.DisablePublicContractsMetadata) : !(this.currentOptions != null && this.currentOptions.DisableInternalContractsMetadata))
            this.SerializeMethodContract(meth.Contract);
        }        
      }
    }

    #region getWitness from expressions    

    protected static WUCs GetWitnesses(Expression e, Member modelfield, TypeNode/*!*/ witnessType) {
      Debug.Assert(witnessType != null);      
      WUCs result = new WUCs();
      if (witnessType == SystemTypes.Boolean) {
        //enumerate over all possible witness: i.e, true and false
        result.Exact.Add(new WitnessUnderConstruction(new Literal(true, SystemTypes.Boolean), null, 0));
        result.Exact.Add(new WitnessUnderConstruction(new Literal(false, SystemTypes.Boolean), null, 0));
      } else if (witnessType.NodeType == NodeType.EnumNode) {
        //enumerate over all possible witnesses: i.e., the members of the enum          
        for (int i = 0; i < witnessType.Members.Count; i++ )
          result.Exact.Add(new WitnessUnderConstruction(new Literal(i, witnessType), null, 0));
      } else {        
        //search for witnesses in expression e
        if (e != null)
          result = Checker.GetWUCs(e, modelfield, witnessType);
        if (result.Exact.Count > 0)
          result.RuntimeWitness = result.Exact[0].Witness;  //pick one of the exact witnesses as the runtime witness
        //Now need to add the default witness(es) to the mix. 
        //Toss in (WUC.Neqs.Count + 1) defaultWitnesses (note that the nr of duplicates is not useful unless witnessType is numeric, but ok).
        //doesn't really matter if we treat the default as exact, upper or lower bound as it is only aiming at branches that contain no witnesses.
        foreach (Expression defaultWitness in Checker.GetDefaultWitnesses(witnessType))
          result.Exact.Add(new WitnessUnderConstruction(defaultWitness, null, result.Neqs.Count));            
        #region reverse the order of the Exact witnesses
        //want to add defaults as the first elements, as the default witness will not give an exception -> add to exact. Note that that might break the 
        //invariant that exact-elements have NrOfDuplicates == 0.
        List<WitnessUnderConstruction> exactWithDefault = new List<WitnessUnderConstruction>();      
        for (int i = 0; i < result.Exact.Count; i++)
          exactWithDefault.Add(result.Exact[i]);
        result.Exact = exactWithDefault;
        #endregion    
      }
      return result;
    }

    /// <param name="e">the expression for which we are trying to find possible witnesses of consistency</param>
    /// <param name="modelfield">the modelfield for which we are trying to find a witness, or null if we are looking for result in a method postcondition</param>
    /// <param name="witnessType">the type of the witness we are looking for</param>    
    protected static WUCs GetWUCs(Expression e, Member modelfield, TypeNode witnessType) {
      WUCs result = new WUCs();
      BinaryExpression be = e as BinaryExpression;
      TernaryExpression te = e as TernaryExpression;

      NodeType nt = e.NodeType;
      if (nt == NodeType.Eq || nt == NodeType.Iff || nt == NodeType.Ge || nt == NodeType.Gt
        || nt == NodeType.Le || nt == NodeType.Lt || nt == NodeType.Ne) {
        #region don't want to go into either operand, but check if either operand is a witness candidate.
        Expression inferredWitness = Checker.GetWitnessHelper(e, modelfield); //theModelField == null means we are looking for result in method postcondition
        if (inferredWitness != null) {
          WitnessUnderConstruction wuc = new WitnessUnderConstruction(inferredWitness, null, 0);
          if (nt == NodeType.Eq || nt == NodeType.Iff)
            result.Exact.Add(wuc); //found result <==> P. Then P is the only potential witness.            
          else {
            //When we find one of the numeric operators, we can assume that witnessType is numeric,
            //as otherwise the type checker will have produced an error.
            if (nt == NodeType.Ge)
              result.Lowerbounds.Add(wuc);
            else if (nt == NodeType.Gt) {
              wuc.Witness = new BinaryExpression(inferredWitness, new Literal(1, witnessType), NodeType.Add, witnessType, inferredWitness.SourceContext); //witness = witness + 1
              result.Lowerbounds.Add(wuc);
            } else if (nt == NodeType.Le)
              result.Upperbounds.Add(wuc);
            else if (nt == NodeType.Lt) {
              wuc.Witness = new BinaryExpression(inferredWitness, new Literal(1, witnessType), NodeType.Sub, witnessType, inferredWitness.SourceContext); //witness = witness - 1
              result.Upperbounds.Add(wuc);
            } else if (nt == NodeType.Ne)
              result.Neqs.Add(wuc);
          }
        }
        #endregion
      } else if (nt == NodeType.LogicalAnd || nt == NodeType.LogicalOr || nt == NodeType.And || nt == NodeType.Or) {
        #region go into both operands and combine results based on nt
        WUCs lhsWUCs = GetWUCs(be.Operand1, modelfield, witnessType);
        WUCs rhsWUCs = GetWUCs(be.Operand2, modelfield, witnessType);

        if (nt == NodeType.And || nt == NodeType.LogicalAnd) {
          //Todo: Optimization - if lhsWUCs only contains exact witnesses, we can ignore whatever witnesses came from rhsWUCs (but deal with rhsNeq's).
          //we can also take max of all lowerbounds and discard others
          //Reconsider these optimizations: do they work for !(P)?

          //Increase the nr of duplications in non-exact lhsWUCs elements by rhsWUCs.Neqs.Length (and vice versa).
          lhsWUCs.Duplicate(rhsWUCs.Neqs.Count);
          rhsWUCs.Duplicate(lhsWUCs.Neqs.Count);
          //It might be that the right-hand side witnesses are total only given the validity of the be.Operand1.
          //Therefore, we want to guard these witnesses by be.Operand1. However, if be.Operand1 contains the modelfield/result value, 
          //that trick is not going to work: it needs to be evaluated with the witness as its value.
          //i.e., if the witness is only total when be.operand1 holds, then it is not a good witness when be.operand1 requires its evaluation.
          if (nt == NodeType.LogicalAnd && lhsWUCs.IsEmpty)
            rhsWUCs.Guard(be.Operand1);
        } else if (nt == NodeType.LogicalOr) {
          //No need to increase duplicates for || (or |). But we can guard the rhs by !(lhs) if lhs doesn't give witnesses. 
          if (lhsWUCs.IsEmpty)
            rhsWUCs.Guard(new UnaryExpression(be.Operand1, NodeType.Neg, SystemTypes.Boolean));
        }
        result.Add(lhsWUCs);
        result.Add(rhsWUCs);
        #endregion
      } else if (nt == NodeType.Implies) {
        #region Treat A ==> B as !A || B
        Expression notA = new UnaryExpression(be.Operand1, NodeType.Neg, SystemTypes.Boolean);
        Expression notAorB = new BinaryExpression(notA, be.Operand2, NodeType.Or, SystemTypes.Boolean);
        result = GetWUCs(notAorB, modelfield, witnessType);
        #endregion
      } else if (te != null) {
        #region Treat P ? A : B as P ==> A && !P ==> B
        //Need to look into P for witnesses to: for instance, consider the consistent contract ensures result == 5 ? true : false;
        //But also want to pick up witnesses from !P: for instance, consider the consistent contract ensures !(result == 5 ? false : true);
        //In the second case it is essential to have 5 in result.neq so that ! can turn it into an exact witness. 
        Expression PimplA = new BinaryExpression(te.Operand1, te.Operand2, NodeType.Implies);
        Expression notP = new UnaryExpression(te.Operand1, NodeType.Neg, SystemTypes.Boolean);
        Expression notPimplB = new BinaryExpression(notP, te.Operand3, NodeType.Implies);
        Expression and = new BinaryExpression(PimplA, notPimplB, NodeType.And);
        return GetWUCs(and, modelfield, witnessType);
        #endregion
      } else if (nt == NodeType.Neg) {
        #region Treat !(A) by flipping all the results we get from A
        WUCs wucs = GetWUCs((e as UnaryExpression).Operand, modelfield, witnessType);
        result.Exact = wucs.Neqs;
        result.Upperbounds = wucs.Lowerbounds; //Example: !(result > 4). Then we get a lowerbound of 5 in wucs. We need to turn that into an upperbound of 4.
        AddOrSub(result.Upperbounds, 1, NodeType.Add);
        result.Lowerbounds = wucs.Upperbounds;
        AddOrSub(result.Lowerbounds, 1, NodeType.Sub);
        result.Neqs = wucs.Exact;
        #endregion
      }
      return result;
    }
   
    /// <returns>A list of expression that should, by default, be tried as witness for the consistency of an expression of witnessType</returns>
    private static ExpressionList GetDefaultWitnesses(TypeNode witnessType) {
      ExpressionList result = new ExpressionList();

      if (witnessType.IsPrimitiveNumeric || witnessType == SystemTypes.Char) {
        result.Add(new Literal(0, witnessType, new SourceContext()));
        result.Add(new Literal(1, witnessType, new SourceContext()));
      } else if (witnessType is Struct) //for user-defined structs, return new S() as default (null doesn't work).
        //Note that primitive numeric types are also structs, so the order of the if and else brach matters.
        result.Add(new Local(StandardIds.NewObj, witnessType, new SourceContext()));
      else
        result.Add(new Literal(null, witnessType, new SourceContext()));

      //special-case String! and object! (for which we know a consistent constructor)
      OptionalModifier possibleNonNullType = (witnessType as OptionalModifier);
      TypeNode t = (possibleNonNullType != null ? possibleNonNullType.ModifiedType : null);
      if (witnessType == SystemTypes.String || witnessType == SystemTypes.Object || t == SystemTypes.String || t == SystemTypes.Object)
        result.Add(new Literal("Let's Boogie!", witnessType));

      return result;
    }

    public class WUCs {
      public List<WitnessUnderConstruction> Exact = new List<WitnessUnderConstruction>();
      public List<WitnessUnderConstruction> Upperbounds = new List<WitnessUnderConstruction>();
      public List<WitnessUnderConstruction> Lowerbounds = new List<WitnessUnderConstruction>();
      public List<WitnessUnderConstruction> Neqs = new List<WitnessUnderConstruction>();
      //during getWitness(), the following is invariant: forall x in {Exact union Neqs} :: x.nrOfDuplications == 0;

      public Expression RuntimeWitness; //store a suitable runtime witness here

      public bool IsEmpty { get { return Exact.Count == 0 && Upperbounds.Count == 0 && Lowerbounds.Count == 0 && Neqs.Count == 0; } }

      public void Duplicate(int nr) {
        foreach (WitnessUnderConstruction w in this.Upperbounds)
          w.NrOfDuplications = w.NrOfDuplications + nr;
        foreach (WitnessUnderConstruction w in this.Lowerbounds)
          w.NrOfDuplications = w.NrOfDuplications + nr;
      }

      public void Guard(Expression g) {
        List<WitnessUnderConstruction> fullList = new List<WitnessUnderConstruction>();
        fullList.AddRange(this.Exact);
        fullList.AddRange(this.Upperbounds);
        fullList.AddRange(this.Lowerbounds);
        fullList.AddRange(this.Neqs);
        for (int i = 0; i < fullList.Count; i++) {
          WitnessUnderConstruction wuc = fullList[i];
          wuc.Guard = new BinaryExpression(g, wuc.Guard, NodeType.And, SystemTypes.Boolean, g.SourceContext);
        }
      }

      public void Add(WUCs wuscToAdd) {
        this.Exact.AddRange(wuscToAdd.Exact);
        this.Upperbounds.AddRange(wuscToAdd.Upperbounds);
        this.Lowerbounds.AddRange(wuscToAdd.Lowerbounds);
        this.Neqs.AddRange(wuscToAdd.Neqs);
      }

    }

    public class WitnessUnderConstruction {
      public Expression Witness;
      public Expression Guard;
      //public string type;
      public int NrOfDuplications;

      public WitnessUnderConstruction(Expression _witness, Expression _guard, int _nrOfDuplications) {
        Witness = _witness; Guard = _guard; NrOfDuplications = _nrOfDuplications;
      }

      public void Duplicate(int i) { this.NrOfDuplications = this.NrOfDuplications + i; }
    }

    /// <summary>
    /// requires nt to be a type that can go into a BinaryExpression that has i as second operand
    /// </summary>    
    public static void AddOrSub(List<WitnessUnderConstruction> listToOperateOn, int val, NodeType nt) {
      for (int i = 0; i < listToOperateOn.Count; i++) {
        WitnessUnderConstruction wuc = listToOperateOn[i];
        wuc.Witness = new BinaryExpression(wuc.Witness, new Literal(val, wuc.Witness.Type), nt);
      }
    }     
    #endregion

    private class WitnessFromCodeFinderVisitor : StandardVisitor {            
      public ExpressionList Witnesses = new ExpressionList(); //for each statement return E; in the visited code, E in returnExpressions if E is admissible as a witness.       
      public Method methodToInferPostFrom = null;  //if set, Visit tries to infer a postcondition for method. 

      public override StatementList VisitStatementList(StatementList statements) {
        if (statements == null) return null;
        foreach (Statement stat in statements) {
          Return r = stat as Return;
          if (r != null && r.Expression != null) { //Statement return E; found. Test admissibility of E as a witness.
            WitnessFromCodeAdmissibilityVisitor admis = new WitnessFromCodeAdmissibilityVisitor();            
            try {
              admis.VisitExpression(r.Expression);
              this.Witnesses.Add(r.Expression); //witness found, otherwise exception would have been thrown
              
              #region add inferred postconditions if needed
              TypeNode retType = (methodToInferPostFrom == null || methodToInferPostFrom.ReturnType == null) ? null : methodToInferPostFrom.ReturnType;              
              if (r == statements[0] && retType != null && (retType.IsObjectReferenceType || retType.IsPrimitiveComparable)) {
                //the return statement is the first statement of the body.
                //We would like to add ensures result == r.Expression; However, we have to be careful, for 2 reasons.
                //1: == cannot be applied to structs and generic types. Note that we already tested that we do not return a struct.
                //2: result might be the implicitly boxed version of r.Expression (e.g., public object m() {returns 5;}), in which case result == r.Expression does not hold (in particular at runtime)
                //To account for 2, we have to box/unbox result and r.Expression as necessary
                //If r.Expression is a generic type, we have to distinguish cases !(r.Expression is System.ValueType) or (r.Expression is int) and act accordingly                              
                //(We do not (yet) support cases where r.Expression is a valueType other than an int, but they could be handled along the same lines.)

                if (methodToInferPostFrom.Contract == null) {
                  methodToInferPostFrom.Contract = new MethodContract(methodToInferPostFrom);
                }
                SourceContext scInferred = methodToInferPostFrom.Name.SourceContext; //can't set the sourcetext, so error message will be confusing...but then again, the idea is that this can never crash                                                                                
                
                //Duplicate the return expression to avoid sharing problems 
                Duplicator dup = new Duplicator(this.methodToInferPostFrom.DeclaringType.DeclaringModule, this.methodToInferPostFrom.DeclaringType);
                Expression returnInEq = dup.VisitExpression(r.Expression);

                //needed for casting/boxing/unboxing
                MemberBinding intType = new MemberBinding(null, SystemTypes.Int32);
                Literal objectLit = new Literal(SystemTypes.Object, SystemTypes.Type);
                Literal intLit = new Literal(SystemTypes.Int32, SystemTypes.Type);

                Expression resultBinding = new ReturnValue(methodToInferPostFrom.ReturnType, scInferred);
                Expression resultInEq = resultBinding;
                if (r.Expression.NodeType == NodeType.Box) { //if the return expression has been (implicitly) boxed, unbox both it and result
                  returnInEq = (returnInEq as BinaryExpression).Operand1; //as we have the return expression in hand, unboxing is easy
                  //adjust resultInEq to (returnInEq.Type)resultInEq (i.e., add an unbox), using an explicit coercion
                  BinaryExpression resultUnboxed = new BinaryExpression(resultInEq, intType, NodeType.Unbox, SystemTypes.Int32.GetReferenceType(), scInferred); //gets type System.Int32@ 
                  AddressDereference resultDeref = new AddressDereference(resultUnboxed, returnInEq.Type, scInferred);
                  resultInEq = new BinaryExpression(resultDeref, intLit, NodeType.ExplicitCoercion, SystemTypes.Int32, scInferred);
                }
                                
                if (returnInEq.Type != null && (returnInEq.Type.IsObjectReferenceType || returnInEq.Type.IsPrimitiveComparable)) {
                  //returnInEq is not a user-defined struct (to which we can't apply ==)
                  Expression eq = null; //holds the postcondition that is added
                  if (!(isGenericTypeThatCanBeStruct(returnInEq.Type))) {
                    //== can be applied to returnInEq. Therefore, it can also be applied to resultInEq, which is of a supertype (i.e., not generic type, not a struct). 
                    eq = new BinaryExpression(resultInEq, returnInEq, NodeType.Eq, SystemTypes.Boolean, scInferred);
                  } else {
                    #region adjust eq to compensate for generics 
                    //insert  ensures !(returnInEq is System.ValueType) ==> (object)resultInEq == (object)returnInEq;
                    //and     ensures returnInEq is int ==> (int)(object)resultInEq == (int)(object)returnInEq;
                    //the cast to object is needed to allow application of == and the cast to int. 
                    //Note that resultInEq.Type is known to also be a generic type as it is a supertype.
                    //(and note that in this case, there was no unboxing)

                    #region set up the antecedent !(returnInEq is System.ValueType)
                    TypeNode theType = SystemTypes.ValueType;

                    //antecedent: !(r.Expression is System.ValueType)
                    Expression ante = new BinaryExpression(r.Expression, new Literal(theType, SystemTypes.Type), NodeType.Is, SystemTypes.Boolean, scInferred);
                    ante = new UnaryExpression(ante, NodeType.LogicalNot, SystemTypes.Boolean, scInferred);
                    #endregion

                    #region adjust resultInEq and returnInEq to (object)resultInEq and (object)returnInEq
                    //adjust resultInEq to (object)result, so that it can be used in ==
                    //that means Box it to T, then ExplicitCast it to Object.
                    MemberBinding boxType = new MemberBinding(null, returnInEq.Type); //could also use method return type
                    BinaryExpression resultBoxed = new BinaryExpression(resultBinding, boxType, NodeType.Box, returnInEq.Type, scInferred);
                    resultInEq = new BinaryExpression(resultBoxed, objectLit, NodeType.ExplicitCoercion, SystemTypes.Object, scInferred);
                    
                    //adjust returnInEq to (object)returnInEq                    
                    BinaryExpression returnBoxed = new BinaryExpression(returnInEq, boxType, NodeType.Box, returnInEq.Type, scInferred);
                    returnInEq = new BinaryExpression(returnBoxed, objectLit, NodeType.ExplicitCoercion, SystemTypes.Object, scInferred);
                    #endregion

                    //Add first ensures; ensures ante ==> resultInEq == returnInEq;
                    eq = new BinaryExpression(resultInEq, returnInEq, NodeType.Eq, SystemTypes.Boolean, scInferred);
                    Expression impl = new BinaryExpression(ante, eq, NodeType.Implies, scInferred);
                    EnsuresNormal firstPost = new EnsuresNormal(impl);
                    firstPost.SourceContext = scInferred;
                    methodToInferPostFrom.Contract.Ensures.Add(firstPost);

                    //Now add ensures returnInEq is int ==> (int)(object)resultInEq == (int)(object)returnInEq;
                    //antecedent: r.Expression is int                
                    Expression secondAnte = new BinaryExpression(r.Expression, intLit, NodeType.Is, SystemTypes.Boolean, scInferred);

                    #region adjust resultInEq and returnInEq to (int)resultInEq and (int)returnInEq
                    //set resultInSecondEq to (int)resultInEq, i.e., to (int)(object)result
                    //this requires an unbox to int32@, then an adress-deref to int32, then an explicitcast to int32
                    //note that if we unbox to type Int32 directly, we get a "operation could destabilize the runtime" warining from the checkin tests
                    BinaryExpression resultUnboxed = new BinaryExpression(resultInEq, intType, NodeType.Unbox, SystemTypes.Int32.GetReferenceType(), scInferred); //gets type System.Int32@ 
                    AddressDereference resultDeref = new AddressDereference(resultUnboxed, SystemTypes.Int32, scInferred);
                    BinaryExpression resultInSecondEq = new BinaryExpression(resultDeref, intLit, NodeType.ExplicitCoercion, SystemTypes.Int32, scInferred);

                    //adjust returnInEq to (int)returnInEq 
                    BinaryExpression returnUnboxed = new BinaryExpression(returnInEq, intType, NodeType.Unbox, SystemTypes.Int32.GetReferenceType(), scInferred);
                    AddressDereference returnDeref = new AddressDereference(returnUnboxed, SystemTypes.Int32, scInferred);
                    BinaryExpression returnInSecondEq = new BinaryExpression(returnDeref, intLit, NodeType.ExplicitCoercion, SystemTypes.Int32, scInferred);
                    #endregion
                    
                    Expression secondEq = new BinaryExpression(resultInSecondEq, returnInSecondEq, NodeType.Eq, SystemTypes.Boolean, scInferred);
                    eq = new BinaryExpression(secondAnte, secondEq, NodeType.Implies, SystemTypes.Boolean, scInferred); //(Ab)use eq to hold the implication 
                    #endregion
                  }
                  //Add the constructed equality as a postcondition.
                  EnsuresNormal newPost = new EnsuresNormal(eq);
                  newPost.SourceContext = scInferred;
                  methodToInferPostFrom.Contract.Ensures.Add(newPost);  
                } //else don't generate a postcondition, can't apply == to a user-defined struct                 
              }
              #endregion
            }
            catch (ApplicationException e) { ApplicationException dumme = e; } //witness not admissible ('using' e to avoid warning)           
          }
        }
        return statements;
      }
            
      /// <summary>
      /// requires type is ITypeParameter
      /// ensures result == true unless type is known not to be a struct
      /// </summary>        
      private bool isGenericTypeThatCanBeStruct(TypeNode type) {
        if (type is ITypeParameter) {
          TypeParameterFlags flags = ((ITypeParameter)type).TypeParameterFlags;
          //bool v = (flags & TypeParameterFlags.ValueTypeConstraint) == TypeParameterFlags.ValueTypeConstraint;
          bool r = (flags & TypeParameterFlags.ReferenceTypeConstraint) == TypeParameterFlags.ReferenceTypeConstraint;
          if (!r)
            return true;
        }
        return false;
      }
    }

    private class WitnessFromCodeAdmissibilityVisitor : EmptyVisitor {
    //This class could be extended to allow more expressions as a witness
    //Throws ApplicationException if expression not admissible.
      public override Expression VisitMemberBinding(MemberBinding memberBinding) {
        if (memberBinding.BoundMember == null || memberBinding.BoundMember.DeclaringType is BlockScope)
          throw new ApplicationException("unimplemented"); //DeclaringType is BlockScope indicates a local variable 
        return memberBinding;
      }
      public override Expression VisitLiteral(Literal literal){
        return literal;
      }            
      public override Expression VisitTernaryExpression(TernaryExpression expression) {
        this.VisitExpression(expression.Operand1);
        this.VisitExpression(expression.Operand2);
        this.VisitExpression(expression.Operand3);
        return expression;
      }
      public override Expression VisitBinaryExpression(BinaryExpression be) {
        this.VisitExpression(be.Operand1);
        this.VisitExpression(be.Operand2);
        return be;
      }
      public override Statement VisitSwitchCaseBottom(Statement switchCaseBottom) { //have to implement this abstract method 
        throw new ApplicationException("unimplemented");  
      }

    }
   

    /// <summary>
    /// if mfC does not have a witness, infer one and return it, else return mfC.Witness.
    /// </summary>
    /// <param name="mfC"></param>
    /// <returns></returns>
    private Expression GetInferredWitness(ModelfieldContract/*!*/ mfC) {
      //At some point, this method should be replaced with the better inference scheme used for pure method 
      if (mfC.ModelfieldType == null) return mfC.Witness; //something's wrong
      if (mfC.HasExplicitWitness) return mfC.Witness; //don't change user-supplied witness.                  
      //Let P be the conjunction of the expressions in eList.
      //if result != null, then either result witnesses the consistency of P, or P is inconsistent (either not total or P => false)
      //No, we're not gonna get quite that far, for instance on this.mf < i && this.mf != this.i - 1; (atually, we can do that one now)     
      //for now, let's only worry about modelfields as far as this method is concerned.
      return GetInferredWitness(mfC.SatisfiesList, mfC.ModelfieldType, mfC.Modelfield);  
    }
   
    /// <param name="eListIn">An expressionlist containing postconditions</param>
    /// <param name="resultType">The type of the result variable in the postcondtions</param>
    /// <returns>our best guess at an expression e that will satisfy all postconditions, i.e., 
    ///an expression e such that for each postcondition p, p[e/result] holds.</returns>
    public static Expression GetInferredWitness(ExpressionList eListIn, TypeNode resultType) {
      return GetInferredWitness(eListIn, resultType, null);  
    }

    protected static Expression GetInferredWitness(ExpressionList eListIn, TypeNode resultType, Member theModelfield) {
      #region Split Conjunct and Disjuncts into NoAndOrList, then look for this.mf == P or this.mf <==> P, return P if found
      ExpressionList splitAndList = new ExpressionList();            
      foreach (Expression e in eListIn)         
        foreach (Expression e2 in SplitConjuncts(e))
          splitAndList.Add(e2);
      
      Expression inferredWitness = null; //stores the witness that we are constructing.
      //eList is a list of ensures or satisfies clauses.
      //if eList contains an expression "this.theModelfield == B", then either B is a witness or the mfC does not have a witness.      
      foreach (Expression e in splitAndList) {
        if (e.NodeType == NodeType.Eq || e.NodeType == NodeType.Iff) {        
          inferredWitness = Checker.GetWitnessHelper(e, theModelfield);
          if (inferredWitness != null)
            return inferredWitness; //witness found          
        }
      }

      //split conjuncts and disjuncts for better chance of finding witness:
      //ignore, will be replace anyway
      /*
      ExpressionList splitAndOrList = new ExpressionList();
      foreach (Expression e in splitAndList)
        WitnessAndOrSplit(e, splitAndOrList);

       */
      ExpressionList splitAndOrList = splitAndList;

      //try again
      foreach (Expression e in splitAndOrList) {
        if (e.NodeType == NodeType.Eq) {        
          inferredWitness = Checker.GetWitnessHelper(e, theModelfield);
          if (inferredWitness != null)
            return inferredWitness; //witness found          
        }
      }
      #endregion

      /*
      foreach (Expression e in eList) {
        BinaryExpression eBinary = e as BinaryExpression;
        if (eBinary != null && e.NodeType == NodeType.Eq) {
          //equality found. Check if it is of the shape this.mf == P (where mf is the modelfield). If so, either P is a witness, or no witness exists.           
          MemberBinding posMf = eBinary.Operand1 as MemberBinding;
          //can't compare against theModelfieldBinding as they are not the same object. 
          //not completely sure if the This check is good enough (no type info) but I think it should be enough (can't be of the wrong type as type is added automatically)
          if (posMf != null && posMf.TargetObject is This && posMf.BoundMember == theModelfield)
            return eBinary.Operand2;
          posMf = eBinary.Operand2 as MemberBinding;
          if (posMf != null && posMf.TargetObject is This && posMf.BoundMember == theModelfield)
            return eBinary.Operand1;          
        }
      }
      */

      //no equalities.                
      if (resultType.IsPrimitiveNumeric || resultType == SystemTypes.Char) {
        #region try numeric heuristics and return result
        //Lets try our heuristics for >, <, <=, >=, !=.            
        bool greaterOk = true;
        bool smallerOk = true;
        foreach (Expression e in splitAndOrList) {          
          Expression improver = Checker.GetWitnessHelper(e, theModelfield);
          if (improver == null) continue; //Not of the shape "this.themodelfield op B", no heuristic applies: we have to hope that the witness we come up with will satisfy this constraint.
          NodeType addOrSub = NodeType.DefaultValue;
          bool improve = true;
          if (e.NodeType == NodeType.Ge && greaterOk) { //improve witness to max(inferredWitness, improver)
            smallerOk = false;
          } else if (e.NodeType == NodeType.Gt && greaterOk) { //improve witness to max(inferredWitness, improver + 1)
            addOrSub = NodeType.Add;
            smallerOk = false;
          } else if (e.NodeType == NodeType.Le && smallerOk) {//improve witness to min(inferredWitness, improver)
            greaterOk = false;
          } else if (e.NodeType == NodeType.Lt && smallerOk) {//improve witness to min(inferredWitness, improver - 1)
            addOrSub = NodeType.Sub;
            greaterOk = false;
          } else if (e.NodeType == NodeType.Ne) {
            if (greaterOk)
              addOrSub = NodeType.Add; //improve witness to inferredWitness != improver ? inferredWitness : improver + 1)
            else if (smallerOk)
              addOrSub = NodeType.Sub; //improve witness to inferredWitness != improver ? inferredWitness : improver - 1)
            //do not change greaterOk and smallerOk: any value but improver is ok as a witness
          } else
            improve = false;
          if (improve) {
            Expression condition = new BinaryExpression(inferredWitness, improver, e.NodeType, new TypeExpression(new Literal(TypeCode.Boolean), 0));             
            if (addOrSub != NodeType.DefaultValue)
              improver = new BinaryExpression(improver, new Literal(1, resultType), addOrSub, resultType);            
            if (inferredWitness == null)
              inferredWitness = improver;
            else {              
              inferredWitness = new TernaryExpression(condition, inferredWitness, improver, NodeType.Conditional, inferredWitness.Type);
            }              
          }
        }
        if (inferredWitness == null)
          inferredWitness = new Literal(0, resultType); //no witness candidate found, try 0 and hope for the best.        
        return inferredWitness;
        #endregion
      } else if (resultType.TypeCode == System.TypeCode.Boolean) {
        #region try boolean heuristics and return result
        //Try heuristics for ==> and != (note that we already checked for equalities in noAndOrList)
        inferredWitness = new Literal(false, resultType); //false deals with this.theModelfield ==> P
        Expression improver;
        foreach (Expression e in splitAndOrList) {
          if (e.NodeType == NodeType.Ne) {
            improver = Checker.GetWitnessHelper(e, theModelfield);
            if (improver != null) { //this.theModelfield != improver found.
              return new UnaryExpression(improver, NodeType.Neg); //improve witness to !inferredWitness and return.              
            }
          } 
          if (e.NodeType != NodeType.Implies) continue; //no heuristic applies.
          improver = Checker.GetWitnessHelper(e, theModelfield, false);
          if (improver != null) //found improver ==> this.theModelfield. Improve witness to inferredWitness || improver.
            inferredWitness = new BinaryExpression(inferredWitness, improver, NodeType.Or);          
        }        
        return inferredWitness;        
        #endregion
      }
      //no heuristic applies. Return a default witness.
      return Checker.GetDefaultWitnesses(resultType)[0]; //Allowing multiple witnesses for modelfields has not been implemented yet. Just return the first one for now.
       
                 
    }

    private static Expression GetWitnessHelper(Expression e, Member theModelfield) {
      return GetWitnessHelper(e,theModelfield, true);
    }
    

    /// <summary>
    /// either e is a BinaryExpression of the shape "A operator B", and either (B is this.theModelfield and result == A, or considerRhs 
    /// e is a BinaryExpression of the shape "A operator B", then
    ///   (if B is of the shape this.theModelfield, then result == A, else if considerRhs and A is of the shape this.theModelfield then result == B)
    /// else result == null    
    /// when !considerLhs, then only the shape of B is checked.
    /// when theModelfield == null, checks for result instead of this.theModelfield.
    /// </summary>        
    private static Expression GetWitnessHelper(Expression e, Member theModelfield, bool considerLhs) {
      if (!(e is BinaryExpression)) return null;
      BinaryExpression eAsBinary = e as BinaryExpression;
      if (theModelfield != null) {
        MemberBinding posMf = eAsBinary.Operand2 as MemberBinding;
        //can't compare against theModelfieldBinding as they are not the same object. 
        //not completely sure if the This check is good enough (no type info) but I think it should be enough (can't be of the wrong type as type is added automatically)
        if (posMf != null && posMf.TargetObject is This && posMf.BoundMember == theModelfield)
          return eAsBinary.Operand1;
        if (considerLhs) {
          posMf = eAsBinary.Operand1 as MemberBinding;
          if (posMf != null && (posMf.TargetObject is This || posMf.TargetObject is ImplicitThis) && posMf.BoundMember == theModelfield)
            return eAsBinary.Operand2;
        }
      } else {
        if (eAsBinary.Operand2 is ReturnValue) return eAsBinary.Operand1;
        if (considerLhs && eAsBinary.Operand1 is ReturnValue) return eAsBinary.Operand2;
      }
      return null;
    }

    public static ExpressionList/*!*/ SplitConjuncts(Expression e) {
      ExpressionList el = new ExpressionList();
      SplitConjuncts(e, el);
      return el;
    }
    public static void SplitConjuncts(Expression e, ExpressionList el) {
      if (e == null) return;
      BinaryExpression be = e as BinaryExpression;
      if (be == null) { el.Add(e); return; }
      if (be.NodeType != NodeType.LogicalAnd) { el.Add(e); return; }
      SplitConjuncts(be.Operand1, el);
      SplitConjuncts(be.Operand2, el);
    }


    #region serialization (helper) methods

    /// <summary>
    /// <para>
    /// Seralizes the expressions in toSerialize to an attribute determined by ctor.
    /// </para>
    /// <para><code>
    /// requires ctor.DeclaringType &lt;: Microsoft.Contracts.AttributeWithContext;
    /// </code></para>
    /// <para><code>
    /// requires ctor != null &amp;&amp; toSerialize != null &amp;&amp; toSerialize.SourceContext != null;
    /// </code></para>
    /// </summary>            
    public static AttributeNode SerializeExpression(InstanceInitializer/*!*/ ctor, Expression/*!*/ toSerialize, Module containingModule) { 
      return Checker.SerializeExpressions(ctor, null, new ExpressionList(toSerialize), toSerialize.SourceContext, containingModule);
    }

    /// <summary>
    /// Serializes the expressions in toSerialize to an attribute determined by ctor, as if they come from module containingModule.
    /// Uses the SourceContext information of <param name="sc">sc</param> for the source context for the attribute. 
    /// <para><code>
    /// requires ctor != null &amp;&amp; toSerialize != null &amp;&amp; 0 &lt; toSerialize.Count &amp;&amp; sc != null;;
    /// </code></para>
    /// <para><code>
    /// requires ctor.DeclaringType &lt;: Microsoft.Contracts.AttributeWithContext;
    /// </code></para>
    /// </summary>
    /// <returns></returns>
    public static AttributeNode SerializeExpressions (InstanceInitializer/*!*/ ctor, ExpressionList/*!*/ toSerialize, SourceContext/*!*/ sc, Module containingModule) {
      return Checker.SerializeExpressions(ctor, null, toSerialize, sc, containingModule);
    }

    /// <summary>
    /// <para>
    /// Serializes the expressions in toSerialize to an attribute determined by ctor, as if they come from module containingModule.
    /// Uses the SourceContext information of <param name="sc">sc</param> for the source context for the attribute.
    /// </para>
    /// <para><code>
    /// requires ctor != null &amp;&amp; sc != null;
    /// </code></para>
    /// <para><code>
    /// requires ctor.DeclaringType &lt;: Microsoft.Contracts.AttributeWithContext;
    /// </code></para>
    /// </summary>
    /// <returns></returns>
    public static AttributeNode SerializeExpressions(InstanceInitializer/*!*/ ctor, ExpressionList dontSerialize, ExpressionList toSerialize, SourceContext/*!*/ sc, Module containingModule) {
      MemberBinding attrBinding = new MemberBinding(null, ctor);
      ExpressionList args = new ExpressionList();
      if (dontSerialize != null) {
        foreach (Expression e in dontSerialize) {
          args.Add(e);
        }
      }
      if (toSerialize != null) {
        foreach (Expression e in toSerialize) {
          ContractSerializer cs = new ContractSerializer(containingModule);
          cs.Visit(e);
          string val = cs.SerializedContract;
          args.Add(new Literal(val, SystemTypes.String));
        }
      }
      if (sc.SourceText != null) {
        args.Add(new NamedArgument(Identifier.For("Filename"), new Literal(sc.Document.Name, SystemTypes.String)));
        args.Add(new NamedArgument(Identifier.For("StartLine"), new Literal(sc.StartLine, SystemTypes.Int32)));
        args.Add(new NamedArgument(Identifier.For("StartColumn"), new Literal(sc.StartColumn, SystemTypes.Int32)));
        args.Add(new NamedArgument(Identifier.For("EndLine"), new Literal(sc.EndLine, SystemTypes.Int32)));
        args.Add(new NamedArgument(Identifier.For("EndColumn"), new Literal(sc.EndColumn, SystemTypes.Int32)));
        args.Add(new NamedArgument(Identifier.For("SourceText"), new Literal(sc.SourceText, SystemTypes.String)));
      }
      return new AttributeNode(attrBinding, args, (AttributeTargets)0);
    }    

    // TODO: otherwise clauses on requires, exceptional ensures
    public virtual void SerializeMethodContract(MethodContract mc) {
      if (mc.Requires != null) {
        for (int i = 0, n = mc.Requires.Count; i < n; i++) {
          Requires r = mc.Requires[i];
          if (r == null || r.Condition == null || r.Inherited) continue;
          ExpressionList el = SplitConjuncts(r.Condition);
          for (int j = 0, m = el.Count; j < m; j++) {
            Requires r_prime = new RequiresPlain(el[j]);
            InstanceInitializer ctor = SystemTypes.RequiresAttribute.GetConstructor(SystemTypes.String);
            AttributeNode a = Checker.SerializeExpression(ctor, r_prime.Condition, this.currentModule);
            mc.DeclaringMethod.Attributes.Add(a);
          }
        }
      }
      if (mc.Ensures != null) {
        for (int i = 0, n = mc.Ensures.Count; i < n; i++) {
          EnsuresExceptional ee = mc.Ensures[i] as EnsuresExceptional;
          if (ee != null) {
            if (ee.PostCondition == null) {
              if (ee.Inherited || ee.TypeExpression.SourceContext.Document == null) continue;
              // then it is "throws E;"
              InstanceInitializer ctor = SystemTypes.ThrowsAttribute.GetConstructor(SystemTypes.Type);
              AttributeNode a = Checker.SerializeExpressions(ctor, new ExpressionList(new Literal(ee.Type, SystemTypes.Type)), null, ee.TypeExpression.SourceContext, this.currentModule);
              mc.DeclaringMethod.Attributes.Add(a);
            } else {
              // then it is "throws E ensures Q;" or "throws (E e) ensures Q;"
              // don't split Q into its top-level conjuncts because duplicate throws clauses for the same exception type
              // are not allowed.
              if (ee.Inherited || ee.PostCondition.SourceContext.Document == null) continue;
              InstanceInitializer ctor = SystemTypes.ThrowsAttribute.GetConstructor(SystemTypes.Type, SystemTypes.String);
              AttributeNode a = Checker.SerializeExpressions(ctor,
                new ExpressionList(new Literal(ee.Type, SystemTypes.Type)), 
                new ExpressionList(ee.PostCondition),
                ee.PostCondition.SourceContext,
                this.currentModule);
              mc.DeclaringMethod.Attributes.Add(a);
            }

          } else {
            Ensures e = mc.Ensures[i];
            if (e == null || e.PostCondition == null || e.Inherited || e.PostCondition.SourceContext.Document == null) continue;
            ExpressionList el = SplitConjuncts(e.PostCondition);
            for (int j = 0, m = el.Count; j < m; j++) {
              EnsuresNormal e_prime = new EnsuresNormal(el[j]);
              InstanceInitializer ctor = SystemTypes.EnsuresAttribute.GetConstructor(SystemTypes.String);
              AttributeNode a = Checker.SerializeExpression(ctor, e_prime.PostCondition, this.currentModule);
              mc.DeclaringMethod.Attributes.Add(a);
            }
          }
        }
      }
      if (mc.Modifies != null && mc.Modifies.Count > 0) {
        for (int i = 0, n = mc.Modifies.Count; i < n; i++) {                    
          Expression e = mc.Modifies[i];
          if (e == null || e.SourceContext.Document == null) continue;
          InstanceInitializer ctor = SystemTypes.ModifiesAttribute.GetConstructor(SystemTypes.String);
          AttributeNode a = Checker.SerializeExpression(ctor, e, this.currentModule);          
          mc.DeclaringMethod.Attributes.Add(a);
        }
      }
      return;
    }

    /// <summary>
    /// requires serFor is Method || serFor is ModelfieldContract
    /// if serFor is a ModelfieldContract mfC, then mfC.Witness is serialized as well.
    /// </summary>        
    protected virtual void SerializeWUCs(WUCs toSerialize, Node serializeFor) {      
      ModelfieldContract mfC = serializeFor as ModelfieldContract;
      if (mfC != null && mfC.Witness != null)
        toSerialize.Exact.Add(new WitnessUnderConstruction(mfC.Witness, null, 0)); //add user-defined witness (optimization: make it the first witness)
      bool isUpper = false;
      this.SerializeWUCList(toSerialize.Exact, isUpper, serializeFor);
      this.SerializeWUCList(toSerialize.Lowerbounds, isUpper, serializeFor);
      isUpper = true;
      this.SerializeWUCList(toSerialize.Upperbounds, isUpper, serializeFor);
    }

    /// <summary>
    /// requires serFor is Method || serFor is ModelfieldContract
    /// </summary>        
    protected virtual void SerializeWUCList(System.Collections.Generic.List<Checker.WitnessUnderConstruction>/*!*/ toSer, bool isNotLowerbound, Node serFor) {
      ModelfieldContract mfC = serFor as ModelfieldContract;
      foreach (WitnessUnderConstruction wuc in toSer) {
        InstanceInitializer ctor = SystemTypes.WitnessAttribute.GetConstructor(SystemTypes.Boolean, SystemTypes.Int32,
                                                                               SystemTypes.String, SystemTypes.String, SystemTypes.String);
        ExpressionList serList = new ExpressionList(wuc.Guard, wuc.Witness);
        if (mfC != null)
          serList.Add(new MemberBinding(new This(mfC.Modelfield.DeclaringType), mfC.Modelfield)); //add the name of the modelfield
        else
          serList.Add(null); //The witness-attribute will sit on the node, no need to add identifier
        AttributeNode wa = Checker.SerializeExpressions(ctor, serList, wuc.Witness.SourceContext, this.currentModule);
        //Now add the remaining 2 expressions to wa 
        ExpressionList eList = new ExpressionList(new Literal(isNotLowerbound, SystemTypes.Boolean), new Literal(wuc.NrOfDuplications, SystemTypes.Int32));
        for (int i = 0; i < wa.Expressions.Count; i++) //use for loop to make sure the order of the expressions is preserved
          eList.Add(wa.Expressions[i]);
        wa.Expressions = eList;
        if (mfC != null)
          mfC.DeclaringType.Attributes.Add(wa);
        else
          (serFor as Method).Attributes.Add(wa);
      }
    }
    #endregion

    public virtual void CheckAbstractMethods(TypeNode type) {
      if (type == null || type is Interface) return;
      MethodList abstractMethods = new MethodList();
      TypeFlags savedFlags = type.Flags;
      type.Flags |= TypeFlags.Abstract;
      this.GetTypeView(type).GetAbstractMethods(abstractMethods);
      type.Flags = savedFlags;
      int n = abstractMethods.Count;
      //See if any public instance methods can be pressed into service to implement interface methods
      //If not virtual mark them as final virtual
      //If a method is not in the same compilation unit, create a local proxy that forwards the call
      for (int i = 0; i < n; i++) {
        Method meth = abstractMethods[i];
        if (!(meth.DeclaringType is Interface)) continue;
        Method closeMatch = null;
        Method exactMatch = null;
        bool explicitBaseClassImplementation = false;
        TypeNode t = type;
        TypeNode initialTypeSearchStartedAt = type;
        while (t != null) {
          if (this.GetTypeView(t).ImplementsExplicitly(meth)) explicitBaseClassImplementation = true;
          MemberList mems = this.GetTypeView(t).GetMembersNamed(meth.Name);
          for (int j = 0, m = mems == null ? 0 : mems.Count; j < m; j++) {
            Method locMeth = mems[j] as Method;
            if (locMeth == null) continue;
            if (locMeth.ImplementedTypes != null && locMeth.ImplementedTypes.SearchFor(meth.DeclaringType) < 0) continue;
            if (!locMeth.ParametersMatch(meth.Parameters)) {
              if (locMeth.IsSpecialName && locMeth.Name.ToString().StartsWith("set_") && locMeth.ParametersMatchExceptLast(meth.Parameters)) {
                closeMatch = locMeth;
                continue;
              }
              //if (locMeth.TemplateParametersMatch(meth.TemplateParameters)){
              if (!locMeth.ParametersMatchStructurally(meth.Parameters)) {
                if (locMeth.IsSpecialName && locMeth.Name.ToString().StartsWith("set_") && locMeth.ParametersMatchStructurallyExceptLast(meth.Parameters)) {
                  closeMatch = locMeth;
                }
                continue;
              }
              if (locMeth.IsStatic || !locMeth.IsPublic || 
                  (locMeth.ReturnType != meth.ReturnType && locMeth.ReturnType != null && !locMeth.ReturnType.IsStructurallyEquivalentTo(meth.ReturnType)))
                continue;
              exactMatch = locMeth;
              break;
              //}
              //continue;
            }
            closeMatch = locMeth;
            if (locMeth.IsStatic || (!locMeth.IsPublic && locMeth.ImplementedTypes == null) || 
              (locMeth.ReturnType != meth.ReturnType && locMeth.ReturnType != null && !locMeth.ReturnType.IsStructurallyEquivalentTo(meth.ReturnType)))
              continue;
            exactMatch = locMeth;
            break;
          }
          if (exactMatch != null) break;
          t = t.BaseType;
        }
        if (exactMatch == null && (!type.IsAbstract || meth.DeclaringType is Interface)) {
          if (explicitBaseClassImplementation) goto done;
          Interface iface = meth.DeclaringType as Interface;
          if (iface != null) {
            bool implementedByThisType = false;
            InterfaceList ifaces = this.GetTypeView(type).Interfaces;
            for (int j = 0, m = ifaces == null ? 0 : ifaces.Count; j < m; j++) {
              if (ifaces[j] == iface) {
                implementedByThisType = true;
                break;
              }
            }
            if (!implementedByThisType) goto done; //The base class will complain.
          }
          if (closeMatch != null) {
            if (closeMatch.ImplementedTypes != null && closeMatch.IsSpecialName && (closeMatch.ReturnType == SystemTypes.Void ||
              (closeMatch.ReturnType != meth.ReturnType && !closeMatch.ReturnType.IsStructurallyEquivalentTo(meth.ReturnType)))) {
              //Do nothing, VisitProperty will already have complained about this mismatch
            } else {
              this.HandleError(type.Name, Error.CloseUnimplementedInterfaceMember,
                this.GetTypeName(type), this.GetMethodSignature(meth), this.GetMethodSignature(closeMatch));
              this.HandleRelatedError(meth);
              this.HandleRelatedError(closeMatch);
            }
          } else {
            this.HandleError(type.Name, Error.UnimplementedInterfaceMember, this.GetTypeName(type), this.GetMethodSignature(meth));
            this.HandleRelatedError(meth);
          }
        }
        if (exactMatch != null) {
          if (exactMatch.GetAttribute(SystemTypes.ConditionalAttribute) != null) {
            this.HandleError(exactMatch.Name, Error.InterfaceImplementedByConditional, this.GetMethodSignature(exactMatch), this.GetMethodSignature(meth));
            this.HandleRelatedError(meth);
          }
          if (!exactMatch.IsVirtual || meth.HasOutOfBandContract) {
            if ((exactMatch.DeclaringType == null || exactMatch.DeclaringType.DeclaringModule == this.currentModule)
              && !this.NeedsProxy(meth))
              exactMatch.Flags |= MethodFlags.NewSlot|MethodFlags.Virtual|MethodFlags.Final;
            else {
              CreateProxy(type, meth, exactMatch);
            }
          }
          //          REVIEW: We may want this check (or something similar here. Is it better to move everything to CheckContractInheritance?
          //          if (meth.Contract != null && meth.Contract.Ensures != null && meth.Contract.Ensures.Length > 0){
          //              this.HandleError(initialTypeSearchStartedAt, Error.EnsuresInInterfaceNotInMethod, this.GetMethodSignature(meth),this.GetMethodSignature(exactMatch)); 
          //            }
          //          }

        }
      done:
        abstractMethods[i] = null;
      }

      //If abstract methods remain, and class is not marked abstract, give errors
      if (type.IsAbstract) return;
      for (int i = 0; i < n; i++) {
        Method meth = abstractMethods[i];
        if (meth == null) continue;
        if (meth.DeclaringType == type) {
          this.HandleError(meth.Name, Error.AbstractMethodInConcreteType, this.GetMethodSignature(meth), this.GetTypeName(type));
          this.HandleRelatedError(type);
        } else {
          this.HandleError(type.Name, Error.UnimplementedAbstractMethod, this.GetTypeName(type), this.GetMethodSignature(meth));
          this.HandleRelatedError(meth);
        }
      }
    }
    private bool NeedsProxy(Method method) {
      if (method == null || this.isCompilingAContractAssembly) return false;
      if (!method.HasOutOfBandContract) return false;
      if (method.ReturnType != TypeNode.DeepStripModifiers(method.ReturnType, (method.Template != null) ? method.Template.ReturnType : null, SystemTypes.NonNullType)) return true;
      for (int i = 0, n = method.Parameters == null ? 0 : method.Parameters.Count; i < n; i++) {
        if (method.Parameters[i].Type != TypeNode.DeepStripModifiers(method.Parameters[i].Type, (method.Template != null)?method.Template.Parameters[i].Type:null, SystemTypes.NonNullType))
          return true;
      }
      return false;
    }
    /// <summary>
    /// Create a proxy method implementing the abstractMethod and which calls the implementingMethod.
    /// This is needed when the implementingMethod is supposed to be used for the implementation
    /// of the abstractMethod, but cannot be because it lives in another assembly or isn't virtual
    /// or the abstractMethod has an out-of-band contract and the implementingMethod must have
    /// an identical type signature (i.e., no optional type modifiers for the non-null types).
    /// </summary>
    /// <param name="type">The type containing the implementingMethod and to which the
    /// proxy will be added as a member.</param>
    /// <param name="abstractMethod">The abstract method that the proxy is an implementation of.</param>
    /// <param name="implementingMethod">The implementing method that is supposed to implement
    /// the abstractMethod, but is unable to for various reasons.</param>
    /// <returns>The newly created proxy method.</returns>
    private Method CreateProxy(TypeNode type, Method abstractMethod, Method implementingMethod) {
      ParameterList parameters = abstractMethod.Parameters;
      if (parameters == null)
        parameters = new ParameterList(0);
      else
        parameters = parameters.Clone();
      int m = parameters.Count;
      ExpressionList arguments = new ExpressionList(m);
      for (int j = 0; j < m; j++) {
        Parameter p = (Parameter)parameters[j].Clone();
        parameters[j] = p;
        if (this.typeSystem.IsNonNullType(p.Type)) {
          arguments.Add(this.typeSystem.ExplicitNonNullCoercion(p, p.Type));
        } else {
          arguments.Add(p);
        }
        p.Type = TypeNode.DeepStripModifiers(p.Type, (abstractMethod.Template!=null)?abstractMethod.Template.Parameters[j].Type:null, SystemTypes.NonNullType);
      }
      This ThisParameter = new This(this.currentType);
      StatementList statements = new StatementList(2);
      NodeType typeOfCall = type.IsValueType ? NodeType.Call : NodeType.Callvirt;
      MethodCall mCall = new MethodCall(new MemberBinding(ThisParameter, implementingMethod), arguments, typeOfCall, implementingMethod.ReturnType);
      if (implementingMethod.ReturnType != SystemTypes.Void) {
        statements.Add(new Return(mCall));
      } else {
        statements.Add(new ExpressionStatement(mCall));
        statements.Add(new Return());
      }
      TypeNode returnType = TypeNode.DeepStripModifiers(abstractMethod.ReturnType, (abstractMethod.Template != null) ? abstractMethod.Template.ReturnType : null, SystemTypes.NonNullType);
      ProxyMethod proxy = new ProxyMethod(type, null, new Identifier(abstractMethod.DeclaringType.Name + "." + abstractMethod.Name, implementingMethod.Name.SourceContext), parameters, returnType, new Block(statements));
      proxy.ProxyFor = implementingMethod;
      proxy.ThisParameter = ThisParameter;
      proxy.CallingConvention = CallingConventionFlags.HasThis;

      proxy.Flags = MethodFlags.CompilerControlled | MethodFlags.HideBySig | MethodFlags.NewSlot | MethodFlags.Virtual | MethodFlags.Final;
      proxy.ImplementedInterfaceMethods = new MethodList(abstractMethod);
      type.Members.Add(proxy);
      return proxy;
    }
    public virtual void CheckCircularDependency(TypeNode type) {
      Class c = type as Class;
      if (c != null && !(c.BaseClass is ClassParameter) && this.DerivesFrom(c.BaseClass, c)) {
        this.HandleError(type.Name, Error.CircularBase, this.GetTypeName(c.BaseClass), this.GetTypeName(c));
        this.HandleRelatedError(c.BaseClass);
      }
      Struct s = type as Struct;
      if (s != null) {
        MemberList members = s.Members;  // just check the members syntactically in this type or extension
        for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++) {
          Field f = members[i] as Field;
          if (f == null) continue;
          this.CheckForCircularLayout(f.Type as Struct, s, f);
        }
      }
    }
    public virtual void CheckForCircularLayout(Struct s1, Struct s2, Field offendingField) {
      if (s1 == null || s2 == null) return;
      MemberList members = this.GetTypeView(s1).Members;  // need to check all members, right?
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++) {
        Field f = members[i] as Field;
        if (f == null) continue;
        if (f.Type == s1 && s1.Template == s2 && s1.IsNotFullySpecialized) {
          for (int j = 0, m = s1.TemplateArguments == null ? 0 : s1.TemplateArguments.Count; j < m; j++) {
            TypeNode s1arg = s1.TemplateArguments[j];
            if (s1arg != null && s1arg.Template == s2) {
              //Recursive generic struct
              this.HandleError(offendingField.Name, Error.ValueTypeLayoutCycle, this.GetMemberSignature(offendingField), this.GetTypeName(s1));
              break;
            }
          }
        }
        if (f.IsStatic || f.IsLiteral) continue;
        if (s2.IsStructurallyEquivalentTo(f.Type)) {
          // Our primitives tend to hold their state in a field of their own type. For instance
          // the Boolean struct has a field 'bool m_value' which of course is a Boolean struct
          if (s2.IsPrimitiveNumeric || s2 == SystemTypes.Boolean || s2 == SystemTypes.Char) continue;
          this.HandleError(offendingField.Name, Error.ValueTypeLayoutCycle, this.GetMemberSignature(offendingField), this.GetTypeName(s1));
        }
      }
    }
    public virtual bool DerivesFrom(TypeNode baseType, TypeNode derivedType) {
      if (baseType == null) return false;
      for (TypeNode bt = baseType; bt != null; bt = bt.BaseType) if (bt == derivedType) return true;
      return this.DerivesFrom(baseType.DeclaringType, derivedType);
    }
    public virtual void CheckOperatorOverloads(TypeNode type) {
      if (this.isCompilingAContractAssembly) return; // skip these checks for contract assemblies
      if (type == null) return;
      MemberList members = this.GetTypeView(type).Members;  // check all members, since we're checking combinations
      Member opEq = null;
      Member opNe = null;
      Member opTrue = null;
      Member opFalse = null;
      Member opLt = null;
      Member opGt = null;
      Member opLe = null;
      Member opGe = null;
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++) {
        Method m = members[i] as Method;
        if (m == null || m.Name == null) continue;
        if (!m.IsStatic || !m.IsSpecialName) continue;
        TypeNode p0type = m.Parameters != null && m.Parameters.Count > 0 ? m.Parameters[0].Type : null;
        if (p0type == null) continue;
        bool b;
        p0type = p0type.StripOptionalModifiers(out b);
        TypeNode p1type = m.Parameters != null && m.Parameters.Count > 1 ? m.Parameters[1].Type : null;
        if (p1type != null) {
          p1type = p1type.StripOptionalModifiers(out b);
        }
        if (type.IsAbstract && type.IsSealed && m.Name.ToString().StartsWith("op_")) {
          this.HandleError(m.Name, Error.OperatorInAbstractSealedClass, this.GetMethodSignature(m));
          continue;
        }
        int key = m.Name.UniqueIdKey;
        if (key == StandardIds.opImplicit.UniqueIdKey || key == StandardIds.opExplicit.UniqueIdKey) {
          if (m.ReturnType == null) continue;
          if (m.ReturnType != type && m.ReturnType.Template != type) {
            if (p0type != type && p0type != null && p0type.Template != type)
              this.HandleError(m.Name, Error.ConversionNotInvolvingContainedType);
            else if (m.ReturnType.NodeType == NodeType.Interface)
              this.HandleError(m.Name, Error.ConversionWithInterface, this.GetMethodSignature(m));
            else if (this.GetTypeView(type).IsAssignableTo(m.ReturnType))
              this.HandleError(m.Name, Error.ConversionWithBase, this.GetMethodSignature(m));
            else if (this.GetTypeView(m.ReturnType).IsAssignableTo(type))
              this.HandleError(m.Name, Error.ConversionWithDerived, this.GetMethodSignature(m));
          } else {
            if (p0type == type || (p0type != null && p0type.Template == type))
              this.HandleError(m.Name, Error.IdentityConversion);
            else if (p0type.NodeType == NodeType.Interface)
              this.HandleError(m.Name, Error.ConversionWithInterface, this.GetMethodSignature(m));
            else if (this.GetTypeView(type).IsAssignableTo(p0type))
              this.HandleError(m.Name, Error.ConversionWithBase, this.GetMethodSignature(m));
            else if (this.GetTypeView(p0type).IsAssignableTo(type))
              this.HandleError(m.Name, Error.ConversionWithDerived, this.GetMethodSignature(m));
          }
        } else if (key == StandardIds.opUnaryPlus.UniqueIdKey ||
          key == StandardIds.opUnaryNegation.UniqueIdKey ||
          key == StandardIds.opLogicalNot.UniqueIdKey ||
          key == StandardIds.opOnesComplement.UniqueIdKey) {
          if (p0type != type && p0type != null && p0type.Template != type)
            this.HandleError(m.Name, Error.BadUnaryOperatorSignature);
        } else if (key == StandardIds.opDecrement.UniqueIdKey || key == StandardIds.opIncrement.UniqueIdKey) {
          if (p0type != type && p0type != null && p0type.Template != type || 
            m.ReturnType != type && m.ReturnType != null && m.ReturnType.Template != type)
            this.HandleError(m.Name, Error.BadIncDecSignature);
        } else if (key == StandardIds.opTrue.UniqueIdKey || key == StandardIds.opFalse.UniqueIdKey) {
          if (p0type != type && p0type != null && p0type.Template != type)
            this.HandleError(m.Name, Error.BadUnaryOperatorSignature);
          else if (key == StandardIds.opTrue.UniqueIdKey)
            opTrue = m;
          else if (key == StandardIds.opFalse.UniqueIdKey)
            opFalse = m;
          if (m.ReturnType != SystemTypes.Boolean)
            this.HandleError(m.Name, Error.OpTrueFalseMustResultInBool);
        } else if (key == StandardIds.opEquality.UniqueIdKey)
          opEq = m;
        else if (key == StandardIds.opInequality.UniqueIdKey)
          opNe = m;
        else if (key == StandardIds.opLessThan.UniqueIdKey)
          opLt = m;
        else if (key == StandardIds.opGreaterThan.UniqueIdKey)
          opGt = m;
        else if (key == StandardIds.opLessThanOrEqual.UniqueIdKey)
          opLe = m;
        else if (key == StandardIds.opGreaterThanOrEqual.UniqueIdKey)
          opGe = m;
        if (p0type != type && p1type != type && p0type != null && p1type != null && p0type.Template != type && p1type.Template != type)
          this.HandleError(m.Name, Error.BadBinaryOperatorSignature);
      }
      if (opEq != null) {
        if (opNe == null)
          this.HandleError(opEq.Name, Error.OperatorNeedsMatch, this.GetMemberSignature(opEq), "!=");
      } else if (opNe != null) {
        this.HandleError(opNe.Name, Error.OperatorNeedsMatch, this.GetMemberSignature(opNe), "==");
      }
      if (opEq != null || opNe != null) {
        this.CheckForObjectOverride(type, this.GetTypeView(type).GetMembersNamed(StandardIds.Equals), Error.EqualityOpWithoutEquals);
        this.CheckForObjectOverride(type, this.GetTypeView(type).GetMembersNamed(StandardIds.GetHashCode), Error.EqualityOpWithoutGetHashCode);
      }
      if (opTrue != null) {
        if (opFalse == null)
          this.HandleError(opTrue.Name, Error.OperatorNeedsMatch, this.GetMemberSignature(opTrue), "false");
      } else if (opFalse != null) {
        this.HandleError(opFalse.Name, Error.OperatorNeedsMatch, this.GetMemberSignature(opFalse), "true");
      }
      if (opLt != null) {
        if (opGt == null)
          this.HandleError(opLt.Name, Error.OperatorNeedsMatch, this.GetMemberSignature(opLt), ">");
      } else if (opGt != null) {
        this.HandleError(opGt.Name, Error.OperatorNeedsMatch, this.GetMemberSignature(opGt), "<");
      }
      if (opLe != null) {
        if (opGe == null)
          this.HandleError(opLe.Name, Error.OperatorNeedsMatch, this.GetMemberSignature(opLe), ">=");
      } else if (opGe != null) {
        this.HandleError(opGe.Name, Error.OperatorNeedsMatch, this.GetMemberSignature(opGe), "<=");
      }
    }
    public virtual void CheckForObjectOverride(TypeNode t, MemberList members, Error error) {
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++) {
        Method m = members[i] as Method;
        if (m == null || !m.OverridesBaseClassMember) continue;
        ParameterList parameters = m.Parameters;
        if (error == Error.EqualityOpWithoutEquals) {
          if (m.ReturnType != SystemTypes.Boolean) continue;
          if (parameters == null || parameters.Count != 1 || parameters[0].Type != SystemTypes.Object) continue;
          return;
        }
        if (parameters != null && parameters.Count != 0) continue;
        if (m.ReturnType != SystemTypes.Int32) continue;
        return;
      }
      this.HandleError(t.Name, error, this.GetTypeName(t));
    }
    public virtual void CheckForObsolesence(Node errorLocation, Member mem) {
      if (mem == null) return;
      ObsoleteAttribute attr = mem.ObsoleteAttribute;
      if (attr == null) return;
      string message = attr.Message;
      string memSig = this.GetMemberSignature(mem);
      if (attr.IsError)
        if (message == null)
          this.HandleError(errorLocation, Error.ObsoleteError, memSig);
        else
          this.HandleError(errorLocation, Error.ObsoleteErrorWithMessage, memSig, message);
      else
        if (message == null)
          this.HandleError(errorLocation, Error.ObsoleteWarning, memSig);
        else
          this.HandleError(errorLocation, Error.ObsoleteWarningWithMessage, memSig, message);
    }
    public virtual void CheckForNewFamilyOrVirtualMembers(TypeNode type) {
      if (type == null) return;
      Class cl = type as Class;
      bool abstractSealedType = cl != null && cl.IsAbstractSealedContainerForStatics;
      MemberList members = type.Members;  // just check the members syntactically in this type or extension
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++) {
        Member m = members[i];
        if (m == null || m.Name == null) continue;
        if (m.IsSpecialName) continue;
        if (abstractSealedType) {
          if (!m.IsStatic)
            if (m is Property && ((Property)m).Parameters != null && ((Property)m).Parameters.Count > 0)
              this.HandleError(m.Name, Error.IndexerInAbstractSealedClass, this.GetMemberSignature(m));
            else
              this.HandleError(m.Name, Error.InstanceMemberInAbstractSealedClass, this.GetMemberName(m));
          //TODO: special error for protected members?
        }
        if (m.OverridesBaseClassMember) continue;
        if (m.Name.SourceContext.Document != null && (m.IsFamily || m.IsFamilyAndAssembly || m.IsFamilyOrAssembly))
          this.HandleError(m.Name, Error.FamilyInSealed, this.GetMemberSignature(m));
      }
    }
    public override TypeNode VisitTypeParameter(TypeNode typeParameter) {
      if (typeParameter == null) return null;
      ClassParameter cp = typeParameter as ClassParameter;
      if (cp != null) {
        if (cp.BaseClass != null && cp.BaseClass.IsAbstractSealedContainerForStatics) {
          this.HandleError(cp.BaseClassExpression, Error.ConstraintIsAbstractSealedClass, this.GetTypeName(cp.BaseClass));
          cp.BaseClass = SystemTypes.Object;
        } else
          cp.BaseClass = (Class)this.VisitTypeReference(cp.BaseClass);
      }
      typeParameter.Attributes = this.VisitAttributeList(typeParameter.Attributes, typeParameter);
      typeParameter.Interfaces = this.VisitInterfaceReferenceList(typeParameter.Interfaces);
      return typeParameter;
    }
    protected TrivialHashtable badTypes = null;
    public override TypeNode VisitTypeReference(TypeNode type) {
      if (type == null) return null;
      this.VisitTypeReference(type.DeclaringType);
      TrivialHashtable ambiguousTypes = this.ambiguousTypes;
      if (ambiguousTypes != null) {
        object amType = ambiguousTypes[type.UniqueKey];
        if (amType != null) {
          this.HandleError((Node)amType, Error.MultipleTypeImport, this.GetTypeName(type), type.DeclaringModule.Location);
          ambiguousTypes[type.UniqueKey] = null;
        }
      }
      switch (type.NodeType) {
        //Only get to the following cases if there was an error. Looker will already have reported it.
        case NodeType.ArrayTypeExpression:
        case NodeType.ClassExpression:
        case NodeType.FlexArrayTypeExpression:
        case NodeType.InterfaceExpression:
        case NodeType.PointerTypeExpression:
        case NodeType.ReferenceTypeExpression:
        case NodeType.StreamTypeExpression:
        case NodeType.NonEmptyStreamTypeExpression:
        case NodeType.NonNullTypeExpression:
        case NodeType.NonNullableTypeExpression:
        case NodeType.NullableTypeExpression:
        case NodeType.InvariantTypeExpression:
        case NodeType.BoxedTypeExpression:
        case NodeType.TypeIntersectionExpression:
        case NodeType.TypeUnionExpression:
        case NodeType.TypeExpression:
          return null;
        default:
          break;
      }

      //check template instantiation
      if (type.Template != null && type.Template.TemplateParameters != null && type.TemplateArguments != null) {
        int len = type.Template.TemplateParameters.Count;
        if (type.TemplateArguments.Count != len)
          return type; //this error was handled in Looker

        Specializer specializer = len == 0 ? null : new Specializer(type.DeclaringModule, type.Template.TemplateParameters, type.TemplateArguments);
        if (specializer != null) specializer.CurrentType = this.currentType;

        for (int i = 0; i < len; i++) {
          TypeNode formal = TypeNode.StripModifiers(type.Template.TemplateParameters[i]);
          TypeNode actual = TypeNode.StripModifiers(type.TemplateArguments[i]);

          if (formal == null || actual == null) continue;

          //make sure actual is assignable to base of formal and to each interface
          TypeNode fbaseType = TypeNode.StripModifiers(specializer == null ? formal.BaseType : specializer.VisitTypeReference(formal.BaseType));
          if (fbaseType != null && !this.typeSystem.AssignmentCompatible(actual, fbaseType, this.TypeViewer)) {
            Node offendingNode = type;
            if (type.TemplateExpression != null)
              offendingNode = type.TemplateExpression.TemplateArgumentExpressions[i];
            this.HandleError(offendingNode, Error.TypeParameterNotCompatibleWithConstraint,
              this.GetTypeName(actual),
              this.GetTypeName(fbaseType),
              this.GetTypeName(formal),
              this.GetTypeName(type.Template));
            return type;
          }
          InterfaceList formal_ifaces = this.GetTypeView(formal).Interfaces;
          for (int j = 0; j < formal_ifaces.Count; j++) {
            TypeNode intf = specializer == null ? formal_ifaces[j] : specializer.VisitTypeReference(formal_ifaces[j]);
            // intf = TypeNode.StripModifiers(intf);
            // Commented out the line above because we must retain the non-null optional
            // modifier for the AssignmentCompatible() below to work properly
            // Assumption: other modifiers, such as that in T* in ExHeap, are not possible in this context.
            if (intf != SystemTypes.ITemplateParameter && !this.typeSystem.AssignmentCompatible(actual, intf, this.TypeViewer)) {
              Node offendingNode = type;
              if (type.TemplateExpression != null && type.TemplateExpression.TemplateArgumentExpressions != null)
                offendingNode = type.TemplateExpression.TemplateArgumentExpressions[i];
              this.HandleError(offendingNode,
                Error.TypeParameterNotCompatibleWithConstraint, this.GetTypeName(actual),
                this.GetTypeName(intf),
                this.GetTypeName(formal),
                this.GetTypeName(type.Template));
              return type;
            }
          }
        }
      }
      return type;
    }
    public override Statement VisitTypeswitch(Typeswitch Typeswitch) {
      if (Typeswitch == null) return null;
      Expression e = this.VisitExpression(Typeswitch.Expression);
      if (e == null) return null;
      TypeNode t = e.Type; //HACK
      while (t is TypeAlias) {
        t = ((TypeAlias)t).AliasedType;
        e = this.typeSystem.ExplicitCoercion(e, t, this.TypeViewer);
      }
      TypeUnion tUnion = t as TypeUnion;
      if (tUnion == null) {
        this.HandleError(Typeswitch.Expression, Error.TypeSwitchExpressionMustBeUnion);
        return null;
      }
      TypeNodeList types = tUnion.Types;
      if (types == null) return null; //TODO: give an error
      TypeswitchCaseList oldCases = Typeswitch.Cases;
      if (oldCases == null) return null;
      int n = types.Count;
      TypeswitchCaseList newCases = Typeswitch.Cases = new TypeswitchCaseList(n);
      for (int i = 0; i < n; i++) newCases.Add(null);
      this.VisitTypeswitchCaseList(oldCases, newCases, tUnion);
      Typeswitch.Expression = e;
      return Typeswitch;
    }
    public virtual void VisitTypeswitchCaseList(TypeswitchCaseList oldCases, TypeswitchCaseList newCases, TypeUnion tUnion) {
      if (oldCases == null || newCases == null || tUnion == null) { Debug.Assert(false); return; }
      TypeNodeList types = tUnion.Types;
      if (types == null) return;
      bool complainedAboutDefault = false;
      for (int i = 0, n = oldCases.Count; i < n; i++) {
        TypeswitchCase tcase = this.VisitTypeswitchCase(oldCases[i]);
        if (tcase == null) continue;
        TypeNode t = tcase.LabelType;
        if (t == null) {
          if (tcase.LabelTypeExpression == null && !complainedAboutDefault) {
            complainedAboutDefault = true;
            this.HandleError(tcase, Error.DefaultNotAllowedInTypeswitch);
          }
          continue;
        }
        int j = types.SearchFor(t);
        if (j < 0) {
          //TODO: look for a single field tuple with the same type as t and arrange for coercion
          this.HandleError(tcase.LabelTypeExpression, Error.TypeCaseNotFound, this.GetTypeName(t), this.GetTypeName(tUnion));
          continue;
        }
        if (newCases[j] != null) continue; //TODO: give an error
        newCases[j] = tcase;
      }
    }
    public override TypeswitchCase VisitTypeswitchCase(TypeswitchCase typeswitchCase) {
      if (typeswitchCase == null) return null;
      this.switchCaseCount++;
      typeswitchCase.LabelType = this.VisitTypeReference(typeswitchCase.LabelType);
      typeswitchCase.LabelVariable = this.VisitTargetExpression(typeswitchCase.LabelVariable);
      typeswitchCase.Body = this.VisitBlock(typeswitchCase.Body);
      this.switchCaseCount--;
      return typeswitchCase;
    }
    public override Statement VisitYield(Yield Yield) {
      if (Yield == null) return null;
      if (this.currentFinallyClause != null) {
        this.HandleError(Yield, Error.BadFinallyLeave);
        return null;
      }
      if (this.insideCatchClause) {
        this.HandleError(Yield, Error.CannotYieldFromCatchClause);
        return null;
      }
      if (this.returnNode != null) {
        this.HandleError(this.returnNode, Error.ReturnNotAllowed);
        this.returnNode = null;
      }
      if (this.currentMethod == null) {
        Debug.Assert(false);
        return null;
      }
      if (Yield.Expression == null) {
        this.yieldNode = Yield;
        return Yield;
      }
      Expression e = Yield.Expression = this.VisitExpression(Yield.Expression);
      if (e == null) return null;
      TypeNode returnType = TypeNode.StripModifiers(this.currentMethod.ReturnType);
      if (returnType == null) return null;
      if (returnType.Template != SystemTypes.GenericIEnumerable && returnType.Template != SystemTypes.GenericIEnumerator &&
        returnType != SystemTypes.IEnumerable && returnType != SystemTypes.IEnumerator) {
        this.HandleError(Yield, Error.WrongReturnTypeForIterator, this.GetMethodSignature(this.currentMethod), this.GetTypeName(returnType));
        return null;
      }
      this.yieldNode = Yield;
      TypeNode eType = e.Type;
      TypeNode elemType;
      if (returnType == SystemTypes.IEnumerable || returnType == SystemTypes.IEnumerator)
        elemType = SystemTypes.Object;
      else
        elemType = this.typeSystem.GetStreamElementType(returnType, this.TypeViewer);
      Expression testCoercion = this.typeSystem.TryImplicitCoercion(e, elemType, this.TypeViewer);
      if (testCoercion != null || eType is TupleType || elemType == SystemTypes.Object)
        Yield.Expression = this.typeSystem.ImplicitCoercion(e, elemType, this.TypeViewer);
      else {
        e = this.typeSystem.ImplicitCoercion(e, returnType, this.TypeViewer);
        Local loc = new Local(elemType);
        ForEach forEach = new ForEach();
        forEach.TargetVariable = loc;
        forEach.TargetVariableType = loc.Type;
        forEach.SourceEnumerable = this.VisitEnumerableCollection(new Local(returnType), loc.Type);
        ((CollectionEnumerator)forEach.SourceEnumerable).Collection = e;
        forEach.Body = new Block(new StatementList(new Yield(loc)));
        forEach.ScopeForTemporaryVariables = new BlockScope(this.currentMethod.Scope, forEach.Body);
        return forEach;
      }
      return Yield;
    }
    /// <summary>
    /// If type has an explicit or implicit implementation of a method that has an out-of-band contract,
    /// then need to create a proxy that has the same signature as the "real" interface
    /// method and have it call the one the programmer wrote.
    /// </summary>
    /// <param name="type">The type whose members should be checked to find such methods.</param>
    public virtual void CheckForInterfaceImplementationsOfOutOfBandContractedMethods(TypeNode type) {
      MemberList members = this.GetTypeView(type).Members;  // do we need to check all methods?
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++) {
        Method method = members[i] as Method;
        if (method == null) continue;

        // If a method is a proxy (created in CheckAbstractMethods), then it can be ignored.
        ProxyMethod pMethod = method as ProxyMethod;
        if (pMethod != null) continue;

        #region Implicit implementation
        // If the method isn't virtual, then it will have been given a proxy as part of CheckAbstractMethods
        if (method.IsVirtual && method.ImplicitlyImplementedInterfaceMethods != null) {
          MethodList remainingImplicitImplementedInterfaceMethods = new MethodList(method.ImplicitlyImplementedInterfaceMethods.Count);
          for (int j = 0, m = method.ImplicitlyImplementedInterfaceMethods.Count; j < m; j++) {
            Method ifaceMethod = method.ImplicitlyImplementedInterfaceMethods[j];
            if (ifaceMethod != null && ifaceMethod.HasOutOfBandContract) {
              this.CreateProxy(type, ifaceMethod, method);
            } else {
              remainingImplicitImplementedInterfaceMethods.Add(ifaceMethod);
            }
          }
          method.ImplicitlyImplementedInterfaceMethods = remainingImplicitImplementedInterfaceMethods;
        }
        #endregion Implicit implementation

        #region Explicit implementation
        if (method.ImplementedInterfaceMethods != null) {
          MethodList remainingImplementedInterfaceMethods = new MethodList(method.ImplementedInterfaceMethods.Count);
          TypeNodeList remainingImplementedTypes = new TypeNodeList(method.ImplementedTypes.Count);
          for (int j = 0, m = method.ImplementedInterfaceMethods.Count; j < m; j++) {
            Method ifaceMethod = method.ImplementedInterfaceMethods[j];
            TypeNode ifaceType = method.ImplementedTypes[j];
            if (ifaceMethod != null && ifaceMethod.HasOutOfBandContract) {
              this.CreateProxy(type, ifaceMethod, method);
              // We may need to modify the name if there is another method
              // in the type that has the same name. That is, method's name
              // was written by the programmer as I.f where I is the name of
              // the interface. But since it no longer implements the interface
              // (the proxy does instead), its name will be just "f" in the
              // assembly. If there is another method in the same type with
              // that name, the IL will be bad. So just to play it safe, make
              // the name "fully qualified", i.e., I.f.
              method.Name = new Identifier(ifaceMethod.FullName, method.Name.SourceContext);
            } else {
              remainingImplementedInterfaceMethods.Add(ifaceMethod);
              remainingImplementedTypes.Add(ifaceType);
            }
          }
          method.ImplementedInterfaceMethods = remainingImplementedInterfaceMethods;
          method.ImplementedTypes = remainingImplementedTypes;
        }
        #endregion Explicit implementation
      }
    }
    public virtual void CheckForDuplicateDeclarations(TypeNode type) {
      if (type == null) return;
      MemberList members = type.Members;  // just check the members syntactically in this type or extension
      if (members == null) return;
      bool partialType = type.PartiallyDefines != null;
      if (partialType) type = type.PartiallyDefines;
      TrivialHashtable firstDeclaration = new TrivialHashtable();
      for (int i = 0, n = members.Count; i < n; i++) {
        Member mem = members[i];
        if (mem == null) continue;
        Identifier name = mem.Name;
        if (name == null) continue;
        if (partialType) {
          //Do not check injected constructors for duplication
          InstanceInitializer ctor = mem as InstanceInitializer;
          if (ctor != null && ctor.HasCompilerGeneratedSignature && (ctor.Parameters == null || ctor.Parameters.Count == 0)) {
            continue;
          }
          StaticInitializer cctor = mem as StaticInitializer;
          if (cctor != null && cctor.HasCompilerGeneratedSignature) continue;
        }
        int key = name.UniqueIdKey;
        object fmem = firstDeclaration[key];
        if (fmem == null) {
          firstDeclaration[key] = mem;
          // check this type's member against all like-named members everywhere else:
          MemberList potentialDups = this.GetTypeView(type).GetMembersNamed(name);
          if (name.UniqueIdKey == StandardIds.opImplicit.UniqueIdKey) {
            MemberList explicits = this.GetTypeView(type).GetMembersNamed(StandardIds.opExplicit);
            int k = explicits == null ? 0 : explicits.Count;
            if (k > 0) {
              potentialDups = potentialDups.Clone();
              for (int j = 0; j < k; j++)
                potentialDups.Add(explicits[j]);
            }
          }
          this.CheckForDuplicateDeclarations(mem, potentialDups);
        }
      }
    }
    public virtual void CheckForDuplicateDeclarations(Member first, MemberList members) {
      if (members == null) return;
      int n = members.Count;
      if (n < 2) return;
      TypeNode decType = first.DeclaringType;
      Field f = first as Field;
      if (f != null) {
        Error error = Error.None;
        if (decType is BlockScope) {
          if (f.IsLiteral)
            error = Error.ClashWithLocalConstant;
          else
            error = Error.ClashWithLocalVariable;
        } else if (decType is MethodScope)
          error = Error.DuplicateParameterName;
        if (error != Error.None) {
          for (int i = 1; i < n; i++) {
            Member mem = members[i];
            this.HandleError(mem.Name, error, mem.Name.ToString());
          }
          return;
        }
      }
      TypeNodeList firstImplementedTypes = null;
      switch (first.NodeType) {
        case NodeType.Event:
          firstImplementedTypes = ((Event)first).ImplementedTypes;
          break;
        case NodeType.Method:
        case NodeType.InstanceInitializer:
        case NodeType.StaticInitializer:
          firstImplementedTypes = ((Method)first).ImplementedTypes;
          break;
        case NodeType.Property:
          firstImplementedTypes = ((Property)first).ImplementedTypes;
          break;
      }
      for (int i = 0; i < n; i++) {
        Member mem = members[i];
        Method meth = mem as Method;
        if (meth != null) {
          if (first is Method && this.IsLegalDuplicateMethod(first as Method, meth)) {
            // this method could legally duplicate the original method, so ignore it
            continue;
          }
          int numtPars = meth.TemplateParameters == null ? 0 : meth.TemplateParameters.Count;
          //Report the first of the following methods with the same signature as this one.
          for (int j = i+1; j < n; j++) {
            Method meth2 = members[j] as Method;
            if (meth2 == null) continue;
            if (meth.ParametersMatch(meth2.Parameters)) {
              if (this.IsLegalDuplicateMethod(meth, meth2)) {
                // these methods are legally duplicates
                continue;
              }
              if (!this.ImplementedTypesOverlap(meth2.ImplementedTypes, meth.ImplementedTypes)) continue;
              //weed out getters, setters etc. Their property, event, etc. will show up as an error
              if (meth2.IsSpecialName && meth.IsSpecialName && 
              !(meth2 is InstanceInitializer && meth is InstanceInitializer) && !(meth2 is StaticInitializer && meth is StaticInitializer)) {
                if (meth.Name != null && (meth.Name.UniqueIdKey == StandardIds.opExplicit.UniqueIdKey || meth.Name.UniqueIdKey == StandardIds.opImplicit.UniqueIdKey)) {
                  if (meth2.ReturnType != meth.ReturnType) continue;
                  this.HandleError(meth2.Name, Error.DuplicateConversion, this.GetTypeName(decType));
                  this.HandleError(meth.Name, Error.RelatedErrorLocation);
                  continue;
                }
                if (!meth2.Name.ToString().StartsWith("op_")) continue;
              }
              if (meth2.TemplateParameters != null && meth2.TemplateParameters.Count != numtPars) continue;
              if ((meth2.CallingConvention & CallingConventionFlags.VarArg) != (meth.CallingConvention & CallingConventionFlags.VarArg)) continue;
              Error e = Error.DuplicateMethod;
              for (int k = 0, numPars = meth.Parameters == null ? 0 : meth.Parameters.Count; k < numPars; k++) {
                Parameter p1 = meth.Parameters[k];
                Parameter p2 = meth2.Parameters[k];
                if (p1 == null || p2 == null) continue;
                if ((p1.Flags & ParameterFlags.Out) != (p2.Flags & ParameterFlags.Out)) {
                  e = Error.OverloadRefOut;
                  break;
                }
              }
              Node n2 = meth2.Name; if (n2.SourceContext.Document == null) n2 = meth2;
              string name = this.ErrorHandler == null ? "" : this.ErrorHandler.GetUnqualifiedMemberName(meth2);
              this.HandleError(n2, e, name, this.GetTypeName(decType));
              Node nd = meth.Name; if (nd.SourceContext.Document == null) nd = meth;
              this.HandleError(nd, Error.RelatedErrorLocation);
            }
          }
          if (first is Method) continue;
        }
        Property prop = mem as Property;
        if (prop != null) {
          if (first is Property && this.IsLegalDuplicateProperty(first as Property, prop)) {
            // this property could legally duplicate the original property, so ignore it
            continue;
          }
          if (prop.Parameters != null && prop.Parameters.Count > 0) {
            //Report the first of the following indexed properties with the same signature as this one.
            for (int j = i+1; j < n; j++) {
              Property prop2 = members[j] as Property;
              if (prop2 == null) continue;
              if (prop.ParametersMatch(prop2.Parameters)) {
                if (this.IsLegalDuplicateProperty(prop, prop2)) {
                  // these properties are legally duplicates
                  continue;
                }
                if (!this.ImplementedTypesOverlap(prop2.ImplementedTypes, prop.ImplementedTypes)) continue;
                Node n2 = prop2.Name; if (n2.SourceContext.Document == null) n2 = prop2;
                this.HandleError(n2, Error.DuplicateIndexer, this.GetMemberName(prop), this.GetTypeName(decType));
                Node nd = prop.Name; if (nd.SourceContext.Document == null) nd = meth;
                this.HandleError(nd, Error.RelatedErrorLocation);
              }
            }
            if (first is Property) continue;
          }
        }
        if (first is Event) {
          Event ev = mem as Event;
          if (ev != null) {
            if (this.IsLegalDuplicateEvent(first as Event, ev)) {
              // this event could legally duplicate the original event, so ignore it
              continue;
            }
          }
          Field fld = mem as Field;
          if (fld != null && fld.ForEvent != null) {
            if (this.IsLegalDuplicateEvent(first as Event, fld.ForEvent)) {
              // this field's event could legally duplicate the original event, so ignore it
              continue;
            }
          }
        }
        if (first.Name == mem.Name) continue; //Weed out first vs first and event vs backing field
        TypeNodeList memImplementedTypes = null;
        switch (mem.NodeType) {
          case NodeType.Event:
            memImplementedTypes = ((Event)mem).ImplementedTypes;
            break;
          case NodeType.Method:
          case NodeType.InstanceInitializer:
          case NodeType.StaticInitializer:
            memImplementedTypes = ((Method)mem).ImplementedTypes;
            break;
          case NodeType.Property:
            memImplementedTypes = ((Property)mem).ImplementedTypes;
            break;
        }
        if (!this.ImplementedTypesOverlap(firstImplementedTypes, memImplementedTypes)) continue;
        this.HandleError(mem.Name, Error.DuplicateTypeMember, mem.Name.ToString(), this.GetTypeName(decType));
        this.HandleError(first.Name, Error.RelatedErrorLocation);
      }
    }
    protected virtual bool IsLegalDuplicateMethod(Method original, Method potentialDuplicate) {
      // by default, no methods may legally duplicate, but some other languages (e.g. Sing#'s type 
      // extensions) allow it for method extensions
      return false;
    }
    protected virtual bool IsLegalDuplicateProperty(Property original, Property potentialDuplicate) {
      // by default, no properties may legally duplicate, but some other languages (e.g. Sing#'s type 
      // extensions) allow it for property extensions
      return false;
    }
    protected virtual bool IsLegalDuplicateEvent(Event original, Event potentialDuplicate) {
      // by default, no events may legally duplicate, but some other languages (e.g. Sing#'s type 
      // extensions) allow it for event extensions
      return false;
    }
    public virtual bool ImplementedTypesOverlap(TypeNodeList types1, TypeNodeList types2) {
      if (types1 == types2) return true; //usually means they are both null
      if (types1 == null || types2 == null) return false;
      for (int i = 0, n = types1.Count; i < n; i++) {
        TypeNode t1 = types1[i];
        if (t1 == null) continue;
        for (int j = 0, m = types2.Count; j < m; j++) {
          TypeNode t2 = types2[j];
          if (t2 == null) continue;
          if (t1 == t2) return true;
        }
      }
      return false;
    }
    public virtual void CheckHidingAndOverriding(TypeNode type) {
      if (type == null || type is EnumNode || type is DelegateNode) return;
      MemberList members = type.Members;  // just check the members syntactically in this type or extension
      if (members == null) return;
      members = members.Clone();
      #region Set to null each element of members that hides or overrides a baseclass or implemented interface member
      InterfaceList baseInterfaces = this.GetTypeView(type).Interfaces;
      int baseIfaceCount = baseInterfaces == null ? 0 : baseInterfaces.Count;
      if (type is Interface) {
        for (int i = 0; i < baseIfaceCount; i++)
          this.CheckHidingAndOverriding(members, baseInterfaces[i]);
      } else {
        for (TypeNode btype = type.BaseType; btype != null; btype = btype.BaseType)
          this.CheckHidingAndOverriding(members, btype);

        #region Update the modelfield references of overriding ModelfieldContracts
        if (type.Contract != null) {
          foreach (ModelfieldContract mfC in type.Contract.ModelfieldContracts) {
            if (mfC.IsOverride) {
              //search superclasses for the field with this name that is a modelfield (there can be at most one)
              //update mfC's modelfield to that modelfield
              bool matched = false;
              for (TypeNode btype = type.BaseType; btype != null && !matched; btype = btype.BaseType) {
                MemberList baseMembers = this.GetTypeView(btype).GetMembersNamed(mfC.Modelfield.Name);
                foreach (Member possibleMatch in baseMembers) {
                  Field pMatchAsField = possibleMatch as Field;
                  if (pMatchAsField != null && pMatchAsField.IsModelfield) {
                    mfC.UpdateModelfield(pMatchAsField);
                    matched = true;
                    break;
                  }
                }                
              }
              if (!matched)
                this.HandleError(mfC.Modelfield.Name, Error.OverrideNotExpected, mfC.Modelfield.Name.Name);
            }
          }
        }
        #endregion

      }
      #endregion
      
      #region Complain about any element of members that claims to hide or override, but is not set to null above.
      for (int i = 0, n = members.Count; i < n; i++) {
        Member mem = members[i];
        if (mem == null || mem.IsSpecialName) continue;
        if (mem.HidesBaseClassMember && mem.Name != null && mem.Name.SourceContext.Document != null)
          this.HandleError(mem.Name, Error.MemberDoesNotHideBaseClassMember, this.GetMemberSignature(mem));
        else if (mem.OverridesBaseClassMember && mem.Name != null && mem.Name.SourceContext.Document != null)
          this.HandleError(mem.Name, Error.OverrideNotExpected, this.GetMemberSignature(mem));
      }
      #endregion
    }
    public virtual void CheckHidingAndOverriding(MemberList members, TypeNode baseType) {
      if (members == null || baseType == null) return;
      for (int i = 0, n = members.Count; i < n; i++) {
        Member mem = members[i];
        if (mem == null) continue;
        if (mem.IsSpecialName || mem.Name.SourceContext.Document == null) continue;
        ParameterList parameters = null;
        MemberList baseMembers = null;
        switch (mem.NodeType) {
          case NodeType.InstanceInitializer:
          case NodeType.StaticInitializer:
            members[i] = null;
            continue;
          case NodeType.Field:
          case NodeType.Class:
          case NodeType.DelegateNode:
          case NodeType.EnumNode:
          case NodeType.Event:
          case NodeType.Interface:
          case NodeType.Struct: {
              baseMembers = this.GetTypeView(baseType).GetMembersNamed(mem.Name);
              if (baseMembers == null || baseMembers.Count == 0) continue;
              Member bmem = null;
              for (int j = 0, m = baseMembers.Count; j < m; j++) {
                bmem = baseMembers[j];
                if (bmem == null || bmem.Name == null) continue;
                if (this.NotAccessible(bmem)) { bmem = null; continue; }
                break;
              }
              if (bmem == null || bmem.Name == null) continue;
              Method bmeth = null;
              Property bprop = bmem as Property;
              if (bprop != null) {
                if (bprop.Getter != null && bprop.Getter.IsAbstract) bmeth = bprop.Getter;
                else if (bprop.Setter != null && bprop.Setter.IsAbstract) bmeth = bprop.Setter;
              } else {
                bmeth = bmem as Method;
                if (bmeth != null && !bmeth.IsAbstract) {
                  if (bmeth.Name != null && bmeth.Name.UniqueIdKey == StandardIds.Finalize.UniqueIdKey && bmeth.HasCompilerGeneratedSignature) continue;
                  bmeth = null;
                }
              }
              if (bmeth != null) {
                this.HandleError(mem.Name, Error.HidesAbstractMethod, this.GetMemberName(mem), this.GetMethodSignature(bmeth));
                this.HandleRelatedError(bmeth);
              } else if (!mem.HidesBaseClassMember) {
                Event e = mem as Event;
                Event be = bmem as Event;
                if (e != null && e.OverridesBaseClassMember) {
                  if (be == null) {
                    this.HandleError(mem.Name, Error.CannotOverrideNonEvent, this.GetMemberName(mem), this.GetMemberSignature(bmem));
                    this.HandleRelatedError(bmem);
                  } else if (be.ObsoleteAttribute != null && e.ObsoleteAttribute == null) {
                    this.HandleError(mem.Name, Error.NonObsoleteOverridingObsolete, this.GetMemberName(mem), this.GetMemberSignature(bmem));
                    this.HandleRelatedError(bmem);
                  }
                } else {
                  Error err = Error.MemberHidesBaseClassMember;                  
                  if (be != null && be.IsVirtual && !(mem.DeclaringType is Interface)) err = Error.MemberHidesBaseClassOverridableMember;
                  this.HandleError(mem.Name, err, this.GetMemberName(mem), this.GetMemberSignature(bmem));
                  this.HandleRelatedWarning(bmem);                  
                }
              }
              members[i] = null;
              continue;
            }
          case NodeType.Method:
            Method meth = (Method)mem;
            if (meth.ImplementedInterfaceMethods != null && meth.ImplementedInterfaceMethods.Count > 0) continue;
            baseMembers = this.GetTypeView(baseType).GetMembersNamed(mem.Name);
            parameters = meth.Parameters;
            for (int j = 0, m = baseMembers == null ? 0 : baseMembers.Count; j < m; j++) {
              Member bmem = baseMembers[j];
              if (bmem == null || bmem.Name == null) continue;
              Method bmeth = bmem as Method;
              if (bmeth != null && !bmeth.ParametersMatchStructurally(parameters)) continue;
              if (this.NotAccessible(bmem)) continue;
              if (bmeth != null && bmeth.ImplementedTypeExpressions != null && bmeth.ImplementedTypeExpressions.Count > 0) continue;
              bool falseHide = false;
              if (!mem.HidesBaseClassMember && !mem.OverridesBaseClassMember && !this.isCompilingAContractAssembly) { // don't warn for contract assemblies
                if (bmeth == null || bmeth.IsSpecialName == meth.IsSpecialName || bmeth.IsVirtual == meth.IsVirtual)
                  this.HandleNonOverrideAndNonHide(meth, bmem, bmeth); //give an error and set HidesBaseClassMember to true
                else
                  mem.HidesBaseClassMember = true;
                falseHide = true; //record the fact that HideBaseClassMember happened because of an error
              }
              if (bmeth != null) {
                if (mem.HidesBaseClassMember) {
                  if (bmeth.IsAbstract && !falseHide && !(bmeth.DeclaringType is Interface)) {
                    this.HandleError(meth.Name, Error.HidesAbstractMethod, this.GetMethodSignature(meth), this.GetMethodSignature(bmeth));
                    this.HandleRelatedError(bmeth);
                  }
                  if (!meth.IsVirtual && meth.IsPublic)
                    this.MakeMethodVirtualIfThatWouldMakeItImplementAnInterfaceMethod(meth);
                } else if (mem.OverridesBaseClassMember) {
                  if ((meth.Flags & MethodFlags.MethodAccessMask) != (bmeth.Flags & MethodFlags.MethodAccessMask) && 
                    !((meth.Flags & MethodFlags.MethodAccessMask) == MethodFlags.Family && (bmeth.Flags & MethodFlags.MethodAccessMask) == MethodFlags.FamORAssem &&
                    meth.DeclaringType.DeclaringModule != bmeth.DeclaringType.DeclaringModule)) {
                    this.HandleError(mem.Name, Error.OverrideChangesAccess, this.GetMethodSignature(meth), this.GetMethodSignature(bmeth), this.GetMemberAccessString(bmeth));
                    this.HandleRelatedError(bmem);
                    meth.HidesBaseClassMember = true;
                    if ((meth.Flags & MethodFlags.Virtual) != 0) meth.Flags |= MethodFlags.NewSlot;
                  }
                  if (baseType == SystemTypes.Object && !meth.HasCompilerGeneratedSignature && meth.Name.UniqueIdKey == StandardIds.Finalize.UniqueIdKey)
                    this.HandleOverrideOfObjectFinalize(meth);
                  else if (bmeth.IsSpecialName && !meth.IsSpecialName) {
                    this.HandleError(meth.Name, Error.CannotOverrideSpecialMethod, this.GetMethodSignature(meth), this.GetMethodSignature(bmeth));
                    this.HandleRelatedError(bmeth);
                    meth.OverridesBaseClassMember = false;
                    meth.HidesBaseClassMember = true;
                  } else if (bmeth.IsFinal) {
                    this.HandleError(meth.Name, Error.CannotOverrideFinal, this.GetMethodSignature(meth), this.GetMethodSignature(bmeth));
                    this.HandleRelatedError(bmeth);
                  } else if (!bmeth.IsVirtual) {
                    this.HandleError(meth.Name, Error.CannotOverrideNonVirtual, this.GetMethodSignature(meth), this.GetMethodSignature(bmeth));
                    this.HandleRelatedError(bmeth);
                  } else if (bmeth.ObsoleteAttribute != null && meth.ObsoleteAttribute == null) {
                    this.HandleError(meth.Name, Error.NonObsoleteOverridingObsolete, this.GetMethodSignature(meth), this.GetMethodSignature(bmeth));
                    this.HandleRelatedError(bmeth);
                  } else if (!meth.TemplateParametersMatch(bmeth.TemplateParameters)) {
                    this.HandleError(meth.Name, Error.OverrideNotExpected, this.GetMethodSignature(meth));
                  }
                }
                if (meth.OverridesBaseClassMember && bmeth.ReturnType != meth.ReturnType &&
                  !bmeth.ReturnType.IsStructurallyEquivalentTo(meth.ReturnType)) {
                  TypeNode strippedType = TypeNode.DeepStripModifiers(meth.ReturnType, (bmeth.Template != null) ? bmeth.Template.ReturnType : null, SystemTypes.NonNullType);
                  if (!bmeth.ReturnType.IsStructurallyEquivalentTo(strippedType)
                    &&
                    // we allow the return type to be strengthened from T to T! for any T (including T being a type parameter)
                    !bmeth.ReturnType.IsStructurallyEquivalentTo(TypeNode.StripModifier(strippedType, SystemTypes.NonNullType))
                    ) {
                    this.HandleError(meth.Name, Error.OverrideChangesReturnType, this.GetMethodSignature(meth), this.GetMethodSignature(bmeth));
                    this.HandleRelatedError(bmeth);
                    meth.HidesBaseClassMember = true;
                    if ((meth.Flags & MethodFlags.Virtual) != 0) meth.Flags |= MethodFlags.NewSlot;
                  } else {
                    Method proxy = this.CreateProxy(meth.DeclaringType, bmeth, meth);
                    proxy.OverriddenMethod = bmeth;
                  }
                }
                if (meth.Contract != null && meth.Contract.Requires != null && meth.Contract.Requires.Count > 0) {
                  this.HandleError(meth.Name, Error.RequiresNotAllowedInOverride, this.GetMethodSignature(meth));
                }
                if (meth.Contract != null && meth.Contract.Modifies != null && meth.Contract.Modifies.Count > 0
                  && meth.OverridesBaseClassMember){
                  //&& bmeth.Contract != null && bmeth.Contract.Modifies != null && bmeth.Contract.Modifies.Count > 0) {
                  this.HandleError(meth.Name, Error.ModifiesNotAllowedInOverride, this.GetMethodSignature(meth));
                }
              } else {
                if (meth.OverridesBaseClassMember) {
                  this.HandleError(meth.Name, Error.NoMethodToOverride, this.GetMethodSignature(meth), this.GetMemberSignature(bmem));
                  this.HandleRelatedError(bmem);
                }
              }
              members[i] = null;
              if (baseType.NodeType != NodeType.Interface && this.NeedsProxy(bmeth)) {
                Method proxy = this.CreateProxy(meth.DeclaringType, bmeth, meth);
                proxy.OverriddenMethod = bmeth;
              } else {
                meth.OverriddenMethod = bmeth;
              }
              break;
            }
            continue;
          case NodeType.Property:
            Property prop = (Property)mem;
            if (prop.IsStatic) continue;
            if (prop.ImplementedTypes != null && prop.ImplementedTypes.Count > 0) continue;
            baseMembers = this.GetTypeView(baseType).GetMembersNamed(mem.Name);
            parameters = prop.Parameters;
            for (int j = 0, m = baseMembers == null ? 0 : baseMembers.Count; j < m; j++) {
              Member bmem = baseMembers[j];
              if (bmem == null || bmem.Name == null) continue;
              Property bprop = bmem as Property;
              if (bprop != null && !bprop.ParametersMatchStructurally(parameters)) continue;
              if (this.NotAccessible(bprop)) continue;
              if (!mem.HidesBaseClassMember && !mem.OverridesBaseClassMember) {
                Error e = Error.MemberHidesBaseClassMember;
                if (bprop != null && bprop.IsVirtual) e = Error.MemberHidesBaseClassOverridableMember;
                this.HandleError(mem.Name, e, this.GetMemberSignature(mem), this.GetMemberSignature(bmem));
                this.HandleRelatedWarning(bmem);
                mem.HidesBaseClassMember = true;
              }
              if (bprop != null) {
                meth = prop.Getter; if (meth == null) meth = prop.Setter;
                Method bmeth = bprop.Getter; if (bmeth == null) bmeth = bprop.Setter;
                if ((meth.Flags & MethodFlags.MethodAccessMask) != (bmeth.Flags & MethodFlags.MethodAccessMask) && 
                  !((meth.Flags & MethodFlags.MethodAccessMask) == MethodFlags.Family && (bmeth.Flags & MethodFlags.MethodAccessMask) == MethodFlags.FamORAssem &&
                  meth.DeclaringType.DeclaringModule != bmeth.DeclaringType.DeclaringModule) && !mem.HidesBaseClassMember) {
                  this.HandleError(mem.Name, Error.OverrideChangesAccess, this.GetMemberSignature(prop), this.GetMemberSignature(bprop), this.GetMemberAccessString(bmeth));
                  this.HandleRelatedError(bmem);
                }
                if (prop.OverridesBaseClassMember) {
                  Error e = Error.None;
                  if (bprop.Type != prop.Type && !(bprop.Type == null || bprop.Type.IsStructurallyEquivalentTo(prop.Type)))
                    e = Error.OverrideChangesReturnType;
                  else if (bprop.Getter == null && prop.Getter != null)
                    e = Error.NoGetterToOverride;
                  else if (bprop.Setter == null && prop.Setter != null)
                    e = Error.NoSetterToOverride;
                  else {
                    if (bprop.DeclaringType != prop.DeclaringType.BaseType) {
                      //Need to check if intervening types have hidden any of the accessors
                      this.CheckIfBaseAccessorIsHidden(prop.Getter, bprop.Getter);
                      this.CheckIfBaseAccessorIsHidden(prop.Setter, bprop.Setter);
                    }
                    if (prop.Getter != null && !bprop.Getter.IsVirtual) {
                      this.HandleError(prop.Getter, Error.CannotOverrideNonVirtual, this.GetMethodSignature(prop.Getter), this.GetMethodSignature(bprop.Getter));
                      this.HandleRelatedError(bprop.Getter);
                    }
                    if (prop.Setter != null && !bprop.Setter.IsVirtual) {
                      this.HandleError(prop.Setter, Error.CannotOverrideNonVirtual, this.GetMethodSignature(prop.Setter), this.GetMethodSignature(bprop.Setter));
                      this.HandleRelatedError(bprop.Setter);
                    }
                    if (bprop.ObsoleteAttribute != null && prop.ObsoleteAttribute == null)
                      e = Error.NonObsoleteOverridingObsolete;
                  }
                  if (e != Error.None) {
                    this.HandleError(prop.Name, e, this.GetMemberSignature(prop), this.GetMemberSignature(bprop));
                    this.HandleRelatedError(bprop);
                  }
                  if (!prop.HidesBaseClassMember && prop.OverriddenProperty == null) {
                    prop.OverriddenProperty = bprop;
                  }
                }
              } else {
                if (prop.OverridesBaseClassMember) {
                  this.HandleError(prop.Name, Error.NoPropertyToOverride, this.GetMemberSignature(prop), this.GetMemberSignature(bmem));
                  this.HandleRelatedError(bmem);
                }
              }
              members[i] = null;
              break;
            }
            continue;
        }
      }
    }
        
    /// <summary>
    ///The hidden base method may still end up implementing an interface explicitly implemented by this.currentType.
    ///Prevent that by marking meth as virtual (and newslot) if that means it gets to implement a local interface method
    ///</summary>
    public virtual void MakeMethodVirtualIfThatWouldMakeItImplementAnInterfaceMethod(Method meth) {
      if (meth == null || this.currentType == null) { Debug.Assert(false); return; }
      InterfaceList interfaces = this.GetTypeView(this.currentType).Interfaces;
      for (int i = 0, n = interfaces == null ? 0 : interfaces.Count; i < n; i++) {
        Interface iface = interfaces[i];
        if (iface == null) continue;
        if (this.GetTypeView(iface).GetMatchingMethod(meth) != null) {
          meth.Flags |= MethodFlags.Virtual|MethodFlags.NewSlot;
          return;
        }
      }
    }
    public virtual void CheckIfBaseAccessorIsHidden(Method overrider, Method hidden) {
      if (overrider == null || hidden == null) return;
      TypeNode t = overrider.DeclaringType.BaseType;
      TypeNode bt = hidden.DeclaringType;
      while (t != bt) {
        MemberList mems = this.GetTypeView(t).GetMembersNamed(overrider.Name);
        for (int i = 0, n = mems == null ? 0 : mems.Count; i < n; i++) {
          Method hider = mems[i] as Method;
          if (hider == null || !hider.IsVirtual && (this.NotAccessible(hider) || hider.IsSpecialName != overrider.IsSpecialName)
          || !hider.ParametersMatch(overrider.Parameters))
            continue;
          this.HandleError(overrider, Error.CannotOverrideAccessor, this.GetMethodSignature(overrider), this.GetMethodSignature(hidden), this.GetMethodSignature(hider));
          this.HandleRelatedError(hidden);
          this.HandleRelatedError(hider);
          return;
        }
        t = t.BaseType;
        if (t == null) { Debug.Assert(false); break; }
      }
    }
    public virtual void CheckParameterTypeAccessibility(ParameterList parameterList, Member member) {
      if (parameterList == null) return;
      for (int i = 0, n = parameterList.Count; i < n; i++) {
        Parameter parameter = parameterList[i];
        if (parameter == null) continue;
        if (this.IsLessAccessible(TypeNode.StripModifiers(parameter.Type), member)) {
          Error e = Error.None;
          switch (member.NodeType) {
            case NodeType.DelegateNode:
              e = Error.ParameterLessAccessibleThanDelegate;
              break;
            case NodeType.InstanceInitializer:
              e = Error.ParameterLessAccessibleThanMethod;
              break;
            case NodeType.Method:
              if (Checker.OperatorName[member.Name.UniqueIdKey] == null)
                e = Error.ParameterLessAccessibleThanMethod;
              else {
                e = Error.ParameterLessAccessibleThanOperator;
              }
              break;
            case NodeType.Property:
              e = Error.ParameterLessAccessibleThanIndexedProperty;
              break;
          }
          if (parameter.Type.Name == Looker.NotFound) continue;
          this.HandleError(member.Name, e, this.GetParameterTypeName(parameter), this.GetMemberSignature(member));
          TypeNode pt = parameter.Type;
          Reference r = pt as Reference;
          if (r != null) pt = r.ElementType;
          this.HandleRelatedError(pt);
        }
      }
    }
    public virtual bool IsAllowedAsImplementedType(InterfaceList declaringTypeInterfaces, Interface implementedInterface) {
      if (declaringTypeInterfaces == null) return false;
      if (declaringTypeInterfaces.SearchFor(implementedInterface) >= 0) return true;
      for (int i = 0, n = declaringTypeInterfaces.Count; i < n; i++) {
        Interface iface = declaringTypeInterfaces[i];
        if (iface != null && this.GetTypeView(iface).IsAssignableTo(implementedInterface)) return true;
      }
      return false;
    }
    /// <summary>
    /// returns true if t1 is less accessible than t2
    /// </summary>
    public virtual bool IsLessAccessible(TypeNode t1, TypeNode t2) {
      if (t1 == null || t2 == null) return false;
      if (t1.IsTemplateParameter) return false;
      TypeFlags t1Vis = this.GetNestedVisibility(t1);
      TypeFlags t2Vis = this.GetNestedVisibility(t2);
      return Checker.IsLessAccessible(t1Vis, t2Vis);
    }
    public virtual TypeFlags GetNestedVisibility(TypeNode t) {
      if (t == null) return TypeFlags.NestedPrivate;
      TypeFlags tVis = t.Flags & TypeFlags.VisibilityMask;
      while ((t = t.DeclaringType) != null) tVis = TypeNode.GetVisibilityIntersection(tVis, t.Flags & TypeFlags.VisibilityMask);
      return tVis;
    }
    public static bool IsLessAccessible(TypeFlags vis1, TypeFlags vis2) {
      switch (vis1) {
        case TypeFlags.Public:
        case TypeFlags.NestedPublic:
          return false;
        case TypeFlags.NestedFamORAssem:
          switch (vis2) {
            case TypeFlags.Public:
            case TypeFlags.NestedPublic:
              return true;
            default:
              return false;
          }
        case TypeFlags.NotPublic:
        case TypeFlags.NestedAssembly:
          switch (vis2) {
            case TypeFlags.Public:
            case TypeFlags.NestedPublic:
            case TypeFlags.NestedFamORAssem:
            case TypeFlags.NestedFamily:
              return true;
            default:
              return false;
          }
        case TypeFlags.NestedFamANDAssem:
          switch (vis2) {
            case TypeFlags.Public:
            case TypeFlags.NestedPublic:
            case TypeFlags.NestedFamORAssem:
            case TypeFlags.NestedFamily:
            case TypeFlags.NestedAssembly:
            case TypeFlags.NotPublic:
              return true;
            default:
              return false;
          }
        case TypeFlags.NestedFamily:
          switch (vis2) {
            case TypeFlags.Public:
            case TypeFlags.NestedPublic:
            case TypeFlags.NestedFamORAssem:
            case TypeFlags.NestedAssembly:
            case TypeFlags.NotPublic:
              return true;
            default:
              return false;
          }
        default:
          return vis2 != TypeFlags.NestedPrivate;
      }
    }
    public virtual bool IsLessAccessible(TypeNode type, Member member) {
      if (type == null || member == null) return false;
      TypeNode memberType = member.DeclaringType;
      if (!this.IsLessAccessible(type, memberType)) {
        //The type is at least as accessible as all members of the declaring type
        return false;
      }
      //Type is less accessible than the type declaring the member, but may be as accessible than the member itself
      switch (type.Flags & TypeFlags.VisibilityMask) {
        case TypeFlags.Public:
        case TypeFlags.NestedPublic:
          return false;
        case TypeFlags.NestedFamORAssem:
          return member.IsPublic;
        case TypeFlags.NotPublic:
        case TypeFlags.NestedAssembly:
          return member.IsPublic || member.IsFamily || member.IsFamilyOrAssembly;
        case TypeFlags.NestedFamANDAssem:
          if (member.IsPrivate) return false;
          if (member.IsFamily) {
            TypeNode dt = member.DeclaringType;
            while (dt != null) {
              if (dt.IsAssembly) return false;
              dt = dt.DeclaringType;
            }
          }
          return true;
        case TypeFlags.NestedFamily:
          return member.IsPublic || member.IsFamilyOrAssembly || member.IsAssembly;
        case TypeFlags.NestedPrivate:
          return !member.IsPrivate;
      }
      Debug.Assert(false);
      return false;
    }
    public virtual bool NotAccessible(Member member) {
      TypeNode dummy = null;
      return this.NotAccessible(member, ref dummy);
    }

    //member as accesible as the method in which it is used -- used for contracts
    public virtual bool IsInInvariantAllowed(Member member, Method method) {
      if (member == null || method == null) return false;
      if (member is ParameterField) return true;
      switch ((((Method)method).Flags & MethodFlags.MethodAccessMask)) {
        case MethodFlags.Public:
          return member.IsPublic || 
            member.GetAttribute(SystemTypes.SpecPublicAttribute)!= null;
        case MethodFlags.Family:
          return member.IsPublic || member.IsFamily || 
            member.GetAttribute(SystemTypes.SpecPublicAttribute)!= null  || member.GetAttribute(SystemTypes.SpecProtectedAttribute)!= null;
        case MethodFlags.Assembly:
          return member.IsPublic || member.IsAssembly || 
            member.GetAttribute(SystemTypes.SpecPublicAttribute)!= null  || member.GetAttribute(SystemTypes.SpecInternalAttribute)!= null;
        case MethodFlags.FamANDAssem:
          return member.IsPublic || member.IsFamilyAndAssembly ||
            member.GetAttribute(SystemTypes.SpecPublicAttribute)!=null  || 
            (member.GetAttribute(SystemTypes.SpecInternalAttribute)!=null && member.GetAttribute(SystemTypes.SpecProtectedAttribute) != null);
        case MethodFlags.FamORAssem:
          return member.IsPublic || member.IsFamily || member.IsFamilyOrAssembly || member.IsAssembly ||
            member.GetAttribute(SystemTypes.SpecPublicAttribute)!=null  || member.GetAttribute(SystemTypes.SpecInternalAttribute)!=null ||
            member.GetAttribute(SystemTypes.SpecProtectedAttribute)!=null
            ;
        case MethodFlags.Private:
          return member.IsPrivate;
      }
      Debug.Assert(false);
      return false;
    }
    //method contracts can only use pure, confined and state independent functions
    public virtual bool IsTransparentForMethodContract(Member member, Method method) {
      if (member == null || method == null) return false;
      if (this.isCompilingAContractAssembly) return true;
      if (member is ParameterField) return true;
      if (member is Field) return true;
      // Sometimes a "type" is represented as a MemberBinding instead of as a Literal.
      // In such a case the bound member is a TypeNode.
      if (member is TypeNode) {
        TypeNode t = member as TypeNode;
        return t.IsPublic;
      }
      Method meth = member as Method;
      return meth != null && meth.IsPure
        || meth != null && meth.IsConfined
        || meth != null && meth.IsStateIndependent
        || (meth != null && meth.DeclaringType is DelegateNode && meth.Name == StandardIds.Invoke && this.IsTransparentForMethodContract(meth.DeclaringType, method));
      ;
    }
    //invariants can only use confined and state independent functions
    public virtual bool IsTransparentForInvariant(Member member, Method method) {
      if (member == null || method == null) return false;
      if (this.isCompilingAContractAssembly) return true;
      if (member is ParameterField) return true;
      if (member is Field) return true;
      Method meth = member as Method;
      return meth != null && meth.IsConfined
        || meth != null && meth.IsStateIndependent
        || (meth.DeclaringType is DelegateNode && meth.Name == StandardIds.Invoke && this.IsTransparentForMethodContract(meth.DeclaringType, method));
      ;
    }
    //invariants can only use confined and state independent functions
    public virtual bool IsModelUseOk(Member member, Method method) {
      if (member == null || method == null) return true;
      if (member is ParameterField) return true;
      if (member is Field) return true;
      return (member.GetAttribute(SystemTypes.ModelAttribute)!= null) ? method.GetAttribute(SystemTypes.ModelAttribute)!= null: true;
    }

    //member as accesible as the method in which it is used -- used for contracts
    public virtual bool AsAccessible(Member member, Method method) {
      if (member == null || method == null) return false;
      if (member is ParameterField) return true;
      if (method.Flags == MethodFlags.CompilerControlled) return true; // comprehension, but what else?
      if (this.insideModifies) {
        // no restrictions on what can be referred to within a modifies clause, at least
        // not in terms of visibility.
        return true;
      }
      // Relaxing the rules: for postconditions allow anything that all implementations of the
      // method have access to.
      if (this.insideEnsures) {
        // if this method is the only implementation, it can see everything
        if (!method.IsVirtual || method.DeclaringType.IsSealed || method.IsFinal)
          return true;
        // virtual methods can have other implementations in subtypes.
        // Private fields are not allowed.
        // Internal fields (that are not *also* protected) are allowed only if the method
        // is not visible outside of the assembly.
        // Everything else is allowed.
        if (member.GetAttribute(SystemTypes.SpecPublicAttribute)== null
          &&
          (member.IsPrivate || (member.IsAssembly && !member.IsFamily && method.IsVisibleOutsideAssembly)))
          return false;
        return true;
      }
      switch ((((Method)method).Flags & MethodFlags.MethodAccessMask)) {
        case MethodFlags.Public:
          return member.IsPublic || 
            member.GetAttribute(SystemTypes.SpecPublicAttribute)!= null;
        case MethodFlags.Family:
          return member.IsPublic || member.IsFamily || member.IsFamilyOrAssembly ||
            member.GetAttribute(SystemTypes.SpecPublicAttribute)!= null  || member.GetAttribute(SystemTypes.SpecProtectedAttribute)!= null;
        case MethodFlags.Assembly:
          return member.IsPublic || member.IsAssembly || member.IsFamilyOrAssembly ||
            member.GetAttribute(SystemTypes.SpecPublicAttribute)!= null  || member.GetAttribute(SystemTypes.SpecInternalAttribute)!= null;
        case MethodFlags.FamANDAssem:
          return member.IsPublic || member.IsFamilyAndAssembly ||
            member.GetAttribute(SystemTypes.SpecPublicAttribute)!=null  || 
            (member.GetAttribute(SystemTypes.SpecInternalAttribute)!=null && member.GetAttribute(SystemTypes.SpecProtectedAttribute) != null);
        case MethodFlags.FamORAssem:
          return member.IsPublic || member.IsFamily || member.IsFamilyOrAssembly || member.IsAssembly ||
            member.GetAttribute(SystemTypes.SpecPublicAttribute)!=null  || member.GetAttribute(SystemTypes.SpecInternalAttribute)!=null ||
            member.GetAttribute(SystemTypes.SpecProtectedAttribute)!=null
            ;
        case MethodFlags.Private:
          return true;
      }
      Debug.Assert(false);
      return false;
    }
    /*
     *  if (member == null || method == null) return false;
      if (member is ParameterField) return true;
      if (method.Flags == MethodFlags.CompilerControlled) return true; // comprehension, but what else?
      switch ((((Method)method).Flags & MethodFlags.MethodAccessMask)){
        case MethodFlags.Public:
          return member.IsPublic || 
            member.GetAttribute(SystemTypes.SpecPublicAttribute)!= null;
        case MethodFlags.Family:
          return member.IsPublic || member.IsFamily || 
            member.GetAttribute(SystemTypes.SpecPublicAttribute)!= null  || member.GetAttribute(SystemTypes.SpecProtectedAttribute)!= null;
        case MethodFlags.Assembly:
          return member.IsPublic || member.IsAssembly || 
            member.GetAttribute(SystemTypes.SpecPublicAttribute)!= null  || member.GetAttribute(SystemTypes.SpecInternalAttribute)!= null; 
        case MethodFlags.FamANDAssem:
          return member.IsPublic || member.IsFamilyAndAssembly ||
            member.GetAttribute(SystemTypes.SpecPublicAttribute)!=null  || 
            (member.GetAttribute(SystemTypes.SpecInternalAttribute)!=null && member.GetAttribute(SystemTypes.SpecProtectedAttribute) != null);
        case MethodFlags.FamORAssem:
          return member.IsPublic || member.IsFamily || member.IsFamilyOrAssembly || member.IsAssembly ||
            member.GetAttribute(SystemTypes.SpecPublicAttribute)!=null  || member.GetAttribute(SystemTypes.SpecInternalAttribute)!=null ||
            member.GetAttribute(SystemTypes.SpecProtectedAttribute)!=null  
            ;
        case MethodFlags.Private:
          return true;
     */

    private bool IsSpecPublic(Member member) {
      return member.GetAttribute(SystemTypes.SpecPublicAttribute)!= null;
    }
    private bool IsSpecProtected(Member member) {
      return member.GetAttribute(SystemTypes.SpecProtectedAttribute)!= null &&
        this.GetTypeView(this.currentType).IsAssignableTo(member.DeclaringType) && 
        this.currentType.NodeType==NodeType.Class && member.DeclaringType.NodeType==NodeType.Class;
    }
    private bool IsSpecInternal(Member member) {
      return member.GetAttribute(SystemTypes.SpecInternalAttribute)!= null &&
        this.currentType.DeclaringModule == member.DeclaringType.DeclaringModule;
    }
    private bool IsSpecProtectedOrInternal(Member member) {
      return IsSpecProtected(member) || IsSpecInternal(member);
    }

    public virtual bool NotAccessible(Member member, ref TypeNode qualifierType) { //TODO: this ref is too subtle, use a more explicit way
      if (member == null) return false;
      switch (member.NodeType) {
        case NodeType.Field:
          FieldFlags fflags = FieldFlags.None;
          if (this.insideMethodContract  || this.insideInvariant) {
            fflags |= IsSpecPublic(member) ?FieldFlags.Public:FieldFlags.None;
            fflags |= IsSpecProtected(member)?FieldFlags.Family:FieldFlags.None;
            fflags |= IsSpecInternal(member)?FieldFlags.Assembly:FieldFlags.None;
            fflags |= IsSpecProtectedOrInternal(member)?FieldFlags.FamORAssem:FieldFlags.None;
          }
          return this.NotAccessible(member, ref qualifierType, (int)((((Field)member).Flags & FieldFlags.FieldAccessMask) | fflags));
        case NodeType.InstanceInitializer:
        case NodeType.Method:
          MethodFlags mflags = (MethodFlags)0;
          if (this.insideMethodContract  || this.insideInvariant) {
            mflags |= IsSpecPublic(member) ?MethodFlags.Public:(MethodFlags)0;
            mflags |= IsSpecProtected(member)?MethodFlags.Family:(MethodFlags)0;
            mflags |= IsSpecInternal(member)?MethodFlags.Assembly:(MethodFlags)0;
            mflags |= IsSpecProtectedOrInternal(member)?MethodFlags.FamORAssem:(MethodFlags)0;
          }
          return this.NotAccessible(member, ref qualifierType, (int)((((Method)member).Flags & MethodFlags.MethodAccessMask) | mflags));
        case NodeType.Event:
          break; //TODO: handle this case?
        case NodeType.Property:
          Property prop = (Property)member;
          MethodFlags pflags = Method.GetVisibilityUnion(prop.Getter, prop.Setter);
          mflags = (MethodFlags)0;
          if (this.insideMethodContract  || this.insideInvariant) {
            mflags |= IsSpecPublic(member) ?MethodFlags.Public:(MethodFlags)0;
            mflags |= IsSpecProtected(member)?MethodFlags.Family:(MethodFlags)0;
            mflags |= IsSpecInternal(member)?MethodFlags.Assembly:(MethodFlags)0;
            mflags |= IsSpecProtectedOrInternal(member)?MethodFlags.FamORAssem:(MethodFlags)0;
          }
          return this.NotAccessible(member, ref qualifierType, (int)((pflags & MethodFlags.MethodAccessMask) | mflags));
      }
      return false;
    }
    public virtual bool NotAccessible(Member member, ref TypeNode qualifierType, int visibility) {
      return Checker.NotAccessible(member, ref qualifierType, visibility, this.currentModule, this.currentType, this.TypeViewer);
    }
    public static bool NotAccessible(Member member, ref TypeNode qualifierType, Module currentModule, TypeNode currentType, TypeViewer typeViewer) {
      if (member == null) return false;
      switch (member.NodeType) {
        case NodeType.Field:
          return Checker.NotAccessible(member, ref qualifierType, (int)(((Field)member).Flags & FieldFlags.FieldAccessMask), currentModule, currentType, typeViewer);
        case NodeType.InstanceInitializer:
        case NodeType.Method:
          return Checker.NotAccessible(member, ref qualifierType, (int)(((Method)member).Flags & MethodFlags.MethodAccessMask), currentModule, currentType, typeViewer);
        case NodeType.Property:
          Property p = (Property)member;
          return Checker.NotAccessible(member, ref qualifierType, (int)(Method.GetVisibilityUnion(p.Getter, p.Setter) & MethodFlags.MethodAccessMask), currentModule, currentType, typeViewer);
        case NodeType.Event:
          Event e = (Event)member;
          return Checker.NotAccessible(member, ref qualifierType, (int)(Method.GetVisibilityUnion(e.HandlerAdder, e.HandlerRemover) & MethodFlags.MethodAccessMask), currentModule, currentType, typeViewer);
      }
      return false;
    }
    public static bool NotAccessible(Member member, ref TypeNode qualifierType, int visibility, Module currentModule, TypeNode currentType, TypeViewer typeViewer) {
      TypeNode type = member.DeclaringType;
      return NotAccessible(member, type, ref qualifierType, visibility, currentModule, currentType, typeViewer);
    }
    public static bool NotAccessible(Member member, TypeNode type, ref TypeNode qualifierType, int visibility, Module currentModule, TypeNode currentType, TypeViewer typeViewer) {
      if (type == null) return false;
      TypeNode effectiveType = type.EffectiveTypeNode;
      if ((object)effectiveType != (object)type) {
        // the member being accessed is declared in a type extension; also see whether the member
        // would be accessible if it were considered declared in the extendee type
        if (!NotAccessible(member, effectiveType, ref qualifierType, visibility, currentModule, currentType, typeViewer)) {
          return false;
        }
      }
      TypeNode effectiveCurrentType = currentType == null ? null : currentType.EffectiveTypeNode;
      if ((object)effectiveCurrentType != (object)currentType) {
        // the member is being accessed from a type extension; see whether the member
        // would be accessible if it were being accessed from the extendee type
        // [v-craigc-TODO: consider inaccessible any members not visible according to the 
        // accessibility claimed by the extension; the current code implicitly grants private
        // access to extensions]
        if (!NotAccessible(member, type, ref qualifierType, visibility, currentModule, effectiveCurrentType, typeViewer)) {
          return false;
        }
      }
      TypeNode template = type;
      while (template.Template != null) {
        if (template.Template == template) {
          Debug.Assert(false);
          template.Template = null;
          break;
        }
        template = template.Template;
      }
      TypeNode t = currentType;
      while (t != null && t.Template != null) {
        if (t.Template == t) {
          Debug.Assert(false);
          t.Template = null;
          break;
        }
        t = t.Template;
      }
      while (t != null) {
        if (t == template) {
          switch ((FieldFlags)visibility) {
            case FieldFlags.FamANDAssem:
            case FieldFlags.Family:
              while (qualifierType != null && qualifierType.Template != null)
                qualifierType = qualifierType.Template;
              if (qualifierType != null && !TypeViewer.GetTypeView(typeViewer, qualifierType).IsAssignableTo(t) && !TypeViewer.GetTypeView(typeViewer, qualifierType).IsAssignableToInstanceOf(t)) {
                qualifierType = null;
                return true;
              }
              break;
          }
          return false;
        }
        t = t.DeclaringType;
      }
      switch ((FieldFlags)visibility) {
        case FieldFlags.Assembly:
          return !Checker.InternalsAreVisible(currentModule, type.DeclaringModule);
        case FieldFlags.FamANDAssem:
          if (!Checker.InternalsAreVisible(currentModule, type.DeclaringModule)) return true;
          goto case FieldFlags.Family;
        case FieldFlags.Family:
          if (currentType == null || !TypeViewer.GetTypeView(typeViewer, currentType).IsAssignableTo(type)) {
            if (currentType != null && currentType.DeclaringType != null)
              return Checker.NotAccessible(member, ref qualifierType, currentModule, currentType.DeclaringType, typeViewer);
            return true;
          }
          while (qualifierType != null && qualifierType.Template != null)
            qualifierType = qualifierType.Template;
          if (qualifierType != null && !TypeViewer.GetTypeView(typeViewer, qualifierType).IsAssignableTo(currentType) && !TypeViewer.GetTypeView(typeViewer, qualifierType).IsAssignableToInstanceOf(currentType)) {
            qualifierType = null;
            return true;
          }
          return false;
        case FieldFlags.FamORAssem:
          if (Checker.InternalsAreVisible(currentModule, type.DeclaringModule)) return false;
          goto case FieldFlags.Family;
        case FieldFlags.Private:
          return true;
        case FieldFlags.Public:
          return false;
      }
      return false;
    }
    public static bool InternalsAreVisible(Module referringModule, Module declaringModule) {
      if (referringModule == declaringModule) return true;
      if (referringModule == null || declaringModule == null) return false;
      AssemblyNode referringAssembly = referringModule.ContainingAssembly;
      if (referringAssembly == null) referringAssembly = referringModule as AssemblyNode;
      AssemblyNode declaringAssembly = declaringModule.ContainingAssembly;
      if (declaringAssembly == null) declaringAssembly = declaringModule as AssemblyNode;
      if (referringAssembly == declaringAssembly) return true;
      if (referringAssembly == null) return referringModule.ContainsModule(declaringModule);
      if (referringAssembly.ContainsModule(declaringModule)) return true;
      return referringAssembly.MayAccessInternalTypesOf(declaringAssembly);
    }
  }
  public class StandardCheckingVisitor : StandardVisitor {
    [Microsoft.Contracts.Additive]
    public ErrorHandler ErrorHandler;

    public StandardCheckingVisitor(ErrorHandler errorHandler) {
      this.ErrorHandler = errorHandler;
    }
    public StandardCheckingVisitor(Visitor callingVisitor)
      : base(callingVisitor) {
    }
    public override void TransferStateTo(Visitor targetVisitor) {
      base.TransferStateTo(targetVisitor);
      StandardCheckingVisitor target = targetVisitor as StandardCheckingVisitor;
      if (target == null) return;
      target.ErrorHandler = this.ErrorHandler;
    }
    public virtual string GetAttributeTargetName(AttributeTargets targets) {
      if (this.ErrorHandler == null) return "";
      return this.ErrorHandler.GetAttributeTargetName(targets);
    }
    public virtual string GetAttributeTypeName(TypeNode type) {
      if (this.ErrorHandler == null) return "";
      return this.ErrorHandler.GetAttributeTypeName(type);
    }
    public virtual string GetDelegateSignature(DelegateNode del) {
      if (this.ErrorHandler == null) return "";
      return this.ErrorHandler.GetDelegateSignature(del);
    }
    public virtual string GetIndexerGetterName(Method meth) {
      if (this.ErrorHandler == null) return "";
      return this.ErrorHandler.GetIndexerGetterName(meth);
    }
    public virtual string GetIndexerSetterName(Method meth) {
      if (this.ErrorHandler == null) return "";
      return this.ErrorHandler.GetIndexerSetterName(meth);
    }
    public virtual string GetIndexerName(Method meth) {
      if (this.ErrorHandler == null) return "";
      return this.ErrorHandler.GetIndexerName(meth);
    }
    public virtual string GetMemberAccessString(Member mem) {
      if (this.ErrorHandler == null) return "";
      return this.ErrorHandler.GetMemberAccessString(mem);
    }
    public virtual string GetMemberKind(Member member) {
      if (this.ErrorHandler == null) return "";
      return this.ErrorHandler.GetMemberKind(member);
    }
    public virtual string GetMemberName(Member mem) {
      if (this.ErrorHandler == null) return "";
      return this.ErrorHandler.GetMemberName(mem);
    }
    public virtual string GetMemberSignature(Member mem) {
      if (this.ErrorHandler == null) return "";
      return this.ErrorHandler.GetMemberSignature(mem);
    }
    public virtual string GetMethodSignature(Method method) {
      if (this.ErrorHandler == null) return "";
      return this.ErrorHandler.GetMethodSignature(method);
    }
    public virtual string GetMethodSignature(Method method, bool noAccessor) {
      if (this.ErrorHandler == null) return "";
      return this.ErrorHandler.GetMethodSignature(method, noAccessor);
    }
    public virtual string GetParameterTypeName(Parameter parameter) {
      if (this.ErrorHandler == null) return "";
      return this.ErrorHandler.GetParameterTypeName(parameter);
    }
    public virtual string GetSignatureString(string name, ParameterList parameters, string startPars, string endPars, string parSep) {
      if (this.ErrorHandler == null) return "";
      return this.ErrorHandler.GetSignatureString(name, parameters, startPars, endPars, parSep);
    }
    public virtual string GetTypeName(TypeNode type) {
      if (this.ErrorHandler == null) return "";
      return this.ErrorHandler.GetTypeName(type);
    }
    public virtual string GetUnqualifiedMemberName(Member mem) {
      if (this.ErrorHandler == null) return "";
      return this.ErrorHandler.GetUnqualifiedMemberName(mem);
    }
    public virtual void HandleNonOverrideAndNonHide(Method meth, Member bmem, Method bmeth) {
      if (this.ErrorHandler == null) return;
      this.ErrorHandler.HandleNonOverrideAndNonHide(meth, bmem, bmeth);
    }
    public virtual void HandleOverrideOfObjectFinalize(Method method) {
      if (this.ErrorHandler == null) return;
      this.ErrorHandler.HandleOverrideOfObjectFinalize(method);
    }
    public virtual void HandleDirectCallOfFinalize(MethodCall call) {
      if (this.ErrorHandler == null) return;
      this.ErrorHandler.HandleDirectCallOfFinalize(call);
    }
    public virtual void HandleNonMethodWhereMethodExpectedError(Node offendingNode, Member offendingMember) {
      if (this.ErrorHandler == null) return;
      this.ErrorHandler.HandleNonMethodWhereMethodExpectedError(offendingNode, offendingMember);
    }
    public virtual void HandleError(Node offendingNode, Error error, params string[] messageParameters) {
      if (this.ErrorHandler == null) return;
      this.ErrorHandler.HandleError(offendingNode, error, messageParameters);
    }
    public virtual void HandleRelatedError(Member relatedMember) {
      if (this.ErrorHandler == null) return;
      this.ErrorHandler.HandleRelatedError(relatedMember);
    }
    public virtual void HandleRelatedWarning(Member relatedMember) {
      if (this.ErrorHandler == null) return;
      this.ErrorHandler.HandleRelatedWarning(relatedMember);
    }
  }
}
