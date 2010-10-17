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
#endif
using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.IO;

namespace Microsoft.SpecSharp {
  /// <summary>
  /// Walks IR checking for semantic errors and repairing it so that subsequent walks need not do error checking
  /// </summary>
  public sealed class Checker : CciChecker {
    public Scope scope;
    public CompilationUnit cUnit;
    public SpecSharpCompilation ssCompilation;

    internal Checker(SpecSharpCompilation ssCompilation, ErrorHandler errorHandler, TypeSystem typeSystem, TrivialHashtable scopeFor, TrivialHashtable ambiguousTypes, TrivialHashtable referencedLabels)
      : base(errorHandler, typeSystem, scopeFor, ambiguousTypes, referencedLabels) {
      this.ssCompilation = ssCompilation;
    }
    public Checker(Visitor callingVisitor)
      : base(callingVisitor) {
    }
    public override void TransferStateTo(Visitor targetVisitor) {
      base.TransferStateTo(targetVisitor);
      Checker target = targetVisitor as Checker;
      if (target == null) return;
      target.scope = this.scope;
      target.cUnit = this.cUnit;
      target.ssCompilation = this.ssCompilation;
    }
    public override Compilation VisitCompilation(Compilation compilation) {
      if (compilation == null) return null;
      SpecSharpCompilerOptions ssco = compilation.CompilerParameters as SpecSharpCompilerOptions;
      this.AllowPropertiesIndexersAsRef = (ssco == null || !ssco.Compatibility);
      return base.VisitCompilation(compilation);
    }
    public override Node VisitUnknownNodeType(Node node) {
      if (node == null) return null;
      switch (((SpecSharpNodeType)node.NodeType)) {
        default:
          return base.VisitUnknownNodeType(node);
      }
    }
    public override Expression VisitAddressDereference(AddressDereference addr) {
      Expression result = base.VisitAddressDereference(addr);
      if (result is AddressDereference) {
        Expression expr = ((AddressDereference)result).Address;
        if (expr != null && expr.Type != null && expr.Type.IsPointerType && !this.typeSystem.insideUnsafeCode){
          this.HandleError(addr, Error.UnsafeNeeded);
          return null;
        }
        if (addr.Explicit && expr != null && expr.Type is Reference) {
          this.HandleError(addr, Error.PtrExpected);
          return null;
        }
      }
      return result;
    }
    public override Expression VisitArglistExpression(ArglistExpression argexp) {
      if (this.currentMethod == null || (this.currentMethod.CallingConvention & CallingConventionFlags.VarArg) == 0) {
        this.HandleError(argexp, Error.InvalidArglistConstructContext);
        return null;
      }
      return base.VisitArglistExpression(argexp);
    }
    public override Expression VisitBinaryExpression(BinaryExpression binaryExpression) {
      Expression result = base.VisitBinaryExpression(binaryExpression);
      SpecSharpCompilerOptions options = this.currentOptions as SpecSharpCompilerOptions;
      if (options != null && options.CheckContractAdmissibility) {
        if ((insideMethodContract || insideInvariant) &&
            (binaryExpression.NodeType == NodeType.Eq || binaryExpression.NodeType == NodeType.Ne) &&
              binaryExpression.Operand1 != null && binaryExpression.Operand1.Type != null &&
              !binaryExpression.Operand1.Type.IsValueType) {
          MightReturnNewlyAllocatedObjectVisitor visitor = new MightReturnNewlyAllocatedObjectVisitor(this);
          this.TransferStateTo(visitor);
          visitor.CurrentMethod = this.currentMethod;
          visitor.VisitExpression(binaryExpression.Operand1);
          if (visitor.IsMRNAO) {
            visitor.IsMRNAO = false;
            visitor.VisitExpression(binaryExpression.Operand2);
            if (visitor.IsMRNAO)
              this.HandleError(binaryExpression, Error.BothOperandsOfReferenceComparisonMightBeNewlyAllocated);
          }
        }
      }
      return result;
    }
    public override Expression VisitRefTypeExpression(RefTypeExpression reftypexp) {
      Expression result = base.VisitRefTypeExpression(reftypexp);
      if (result != reftypexp) { return result; }
      if (reftypexp.Operand == null) return null;
      reftypexp.Operand = this.typeSystem.ImplicitCoercion(reftypexp.Operand, SystemTypes.DynamicallyTypedReference, null);
      return reftypexp;
    }
    public override Expression VisitRefValueExpression(RefValueExpression refvalexp) {
      Expression result = base.VisitRefValueExpression(refvalexp);
      if (result != refvalexp) return result;
      if (refvalexp.Operand1 == null || refvalexp.Operand2 == null) return null;
      refvalexp.Operand1 = this.typeSystem.ImplicitCoercion(refvalexp.Operand1, SystemTypes.DynamicallyTypedReference, null);
      return refvalexp;
    }

    public override CompilationUnit VisitCompilationUnit(CompilationUnit cUnit) {
      this.cUnit = cUnit;
      if (this.ErrorHandler != null)
        this.ErrorHandler.SetPragmaWarnInformation(cUnit.PragmaWarnInformation);
      CompilationUnit retCUnit = base.VisitCompilationUnit(cUnit);
      if (this.ErrorHandler != null)
        this.ErrorHandler.ResetPragmaWarnInformation();
      return retCUnit;
    }
    public override Field VisitField(Field field) {
      Field retVal = base.VisitField(field);
      AttributeNode attrNode = field.GetAttribute(SystemTypes.AdditiveAttribute);
      bool isAdditive = IsAdditive(attrNode);
      if (isAdditive && attrNode != null) {
        if (field.IsPrivate) {
          this.HandleError(attrNode, Error.AttributeNotAllowedOnPrivateMember, this.GetTypeName(attrNode.Type));
        }
      }
      #region Can't have both [Rep] and [Additive] (or Rep's owned synonyms)
      if (MemberIsRep(field) && isAdditive) {
        this.HandleError(field, Error.ConflictingAttributes, this.GetTypeName(SystemTypes.RepAttribute),
          this.GetTypeName(SystemTypes.AdditiveAttribute));
      }
      #endregion

      return retVal;
    }
    /// <summary>
    /// To be C# compliant, we should not allow anything to be the source of a foreach loop (which includes
    /// comprehensions) unless it either has a GetEnumerator() method or if it is an array.
    /// (It used to be that we would happily use something that was already an Enumerator, but that leads
    /// to a problem: once the enumerator is exhausted, there is not necessarily a good, automatic, way to
    /// reset it. And there isn't necessarily a good way to clone it beforehand.)
    /// </summary>
    /// <param name="forEach">The AST that represents the foreach loop</param>
    /// <returns>Either whatever the base visitor returns or else null (if there is an error)</returns>
    public override Statement VisitForEach(ForEach forEach) {
      Statement s = base.VisitForEach(forEach);
      ForEach f = s as ForEach;
      if (f != null) {
        // don't allow a source enumerable that is just an enumerator
        CollectionEnumerator cEnumerator = f.SourceEnumerable as CollectionEnumerator;
        if (cEnumerator != null && cEnumerator.GetEnumerator == null) {
          // Then it is not a type that supports GetEnumerator(), but if it is an array, then we'll let it slide.
          TypeNode possiblyArrayType = this.typeSystem.GetUnderlyingType(cEnumerator.Collection.Type) as ArrayType;
          if (possiblyArrayType == null) {
            this.HandleError(forEach,
              Error.ForEachMissingMember,
              this.GetTypeName(cEnumerator.Collection.Type),
              this.GetTypeName(cEnumerator.Collection.Type),
              "GetEnumerator");
            return null;
          }
        }
      }
      return s;
    }
    public override Class VisitClass(Class Class) {
      if (Class == null) return null;
      if (Class.IsAbstract && Class.IsSealed && !Class.IsSpecialName && !Class.IsAbstractSealedContainerForStatics && !Class.IsNormalized)
        this.HandleError(Class.Name, Error.AbstractAndSealed, this.GetTypeName(Class));
      Class returnClass = base.VisitClass(Class);
      this.CheckMustOverride(Class);
      this.ImmutabilityChecksOnClasses(Class);
      return returnClass;
    }
    public override Interface VisitInterface(Interface Interface) {
      ImmutabilityChecksOnInterfaces(Interface);
      return base.VisitInterface(Interface);
    }

    public void CheckMustOverride(Class Class) {
      if (Class == null) return;
      Class baseClass = Class.BaseClass;
      if (baseClass == null) return;
      while (baseClass != null) {
        MemberList members = baseClass.Members;
        for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++) {
          Method method = members[i] as Method;
          if (method == null) continue;
          if (method.IsVirtual && method.GetAttribute(Runtime.MustOverrideAttribute) != null) {
            // then Class had better have an implementation *and* it must also be marked
            // as MustOverride
            Method subTypeMethod = Class.GetMatchingMethod(method);
            if (subTypeMethod == null) {
              // Abstract classes are not required to implement a method marked as [MustOverride]
              if (!Class.IsAbstract) {
                this.HandleError(Class.Name, Error.MustOverrideMethodMissing, this.GetTypeName(Class), this.GetMethodSignature(method));
                this.HandleRelatedError(method);
              }
            } else if (!Class.IsSealed && subTypeMethod.GetAttribute(Runtime.MustOverrideAttribute) == null) {
              // Both concrete and abstract classes are required to mark a method as [MustOverride]
              // if they do implement it.
              this.HandleError(Class.Name, Error.MustOverrideMethodNotMarkedAsMustOverride, this.GetMethodSignature(subTypeMethod));
            }
          }
        }
        if (baseClass.IsAbstract) {
          baseClass = baseClass.BaseClass;
        } else {
          baseClass = null; // stop after first non-abstract base class is checked
        }
      }
      return;
    }
    private void ImmutabilityChecksOnClasses(Class Class) {
      if (Class == null) return;
      TypeNode baseType = Class.BaseType;

      bool classIsImmutable = Class.GetAttribute(SystemTypes.ImmutableAttribute) != null;
      String ClassName = Class.Name.ToString();

      if (baseType != null) { // otherwise, there are other errors.
        // A class _can_ be declared [Immutable] iff its superclass is immutable or if its superclass is System.Object.
        if (classIsImmutable &&
            baseType != SystemTypes.Object &&
            baseType.GetAttribute(SystemTypes.ImmutableAttribute) == null)
          this.HandleError(Class.Name, Error.ImmutableHasMutableBase, ClassName, baseType.Name.ToString());

        // A class _must_ be declared [Immutable] if its superclass is immutable (except if superclass is Object)
        if (!classIsImmutable &&
            baseType != SystemTypes.Object &&
            baseType.GetAttribute(SystemTypes.ImmutableAttribute) != null)
          this.HandleError(Class.Name, Error.MutableHasImmutableBase, ClassName, baseType.Name.ToString());
      }

      foreach (Member mem in Class.Members) {
        Field f = mem as Field;
        if (f == null) continue;

        // An immutable class cannot have peer fields
        if (classIsImmutable && f.IsPeer)
          this.HandleError(mem.Name, Error.ImmutableClassHasPeerField, ClassName, mem.ToString());

        // Peer fields and rep fields cannot be of immutable type
        if ((f.IsOwned || f.IsPeer) && f.Type.GetAttribute(SystemTypes.ImmutableAttribute) != null)
          this.HandleError(mem.Name, Error.RepOrPeerFieldImmutable, ClassName, mem.ToString());
      }

      // In constructors of immutable classes, base() must be called as the very last statement
      if (classIsImmutable) {
        MemberList constructors = Class.GetConstructors();
        foreach (Member constructor in constructors) {
          if (constructor is InstanceInitializer && ((InstanceInitializer)constructor).IsCompilerGenerated) continue;
          if (constructor.GetAttribute(ExtendedRuntimeTypes.NotDelayedAttribute) == null) continue;
          StatementList stmts = ((Method)constructor).Body.Statements;
          if (stmts == null) { continue; }
          Statement lastStmt = stmts[stmts.Count - 1];
          if (lastStmt is Block) {
            stmts = ((Block)lastStmt).Statements;
            lastStmt = stmts[stmts.Count - 1];
          }
          // last stmt must be base() or this()
          bool error = false;
          if (!(lastStmt is ExpressionStatement)) error = true;            
          else {
            Expression exp = ((ExpressionStatement)lastStmt).Expression;
            if (!(exp is MethodCall)) error = true;              
            else {
              Expression callee = ((MethodCall)exp).Callee;
              if (callee == null || !(callee is MemberBinding)) error = true;                
              else {
                Member mb = ((MemberBinding)callee).BoundMember;
                if (mb == null || !(mb is InstanceInitializer) || 
                    !(Class.BaseClass == ((InstanceInitializer)mb).DeclaringType ||
                      Class == ((InstanceInitializer)mb).DeclaringType)) {
                  error = true;
                }
              }
            }
          }
          if (error)
            this.HandleError(constructor.Name, Error.ImmutableConstructorLastStmtMustBeBaseOrThis, ClassName);
          // other stmts may not be base
          for (int i = 0; i < stmts.Count - 1; i++) {
            if (!(stmts[i] is ExpressionStatement)) continue;
            Expression exp = ((ExpressionStatement)stmts[i]).Expression;
            if (!(exp is MethodCall)) continue;
            Expression callee = ((MethodCall)exp).Callee;
            if (callee == null || !(callee is MemberBinding)) continue;
            Member mb = ((MemberBinding)callee).BoundMember;
            if (mb != null && mb is InstanceInitializer && Class.BaseClass == ((InstanceInitializer)mb).DeclaringType) {
              this.HandleError(constructor.Name, Error.ImmutableConstructorBaseNotLast, ClassName);
              break;
            }
          }
        }
      }

      // An immutable interface can only be implemented by an immutable class
      foreach (Interface iface in Class.Interfaces) {
        if (iface == null) continue;
        if (iface.GetAttribute(SystemTypes.ImmutableAttribute) != null && !classIsImmutable)
          this.HandleError(Class.Name, Error.ImmutableIfaceImplementedByMutableClass, ClassName, iface.Name.ToString());
      }
    }
    private void ImmutabilityChecksOnInterfaces(Interface Interface) {
      if (Interface == null) return;
      bool ifaceIsImmutable = Interface.GetAttribute(SystemTypes.ImmutableAttribute) != null;

      // A mutable interface cannot extend an immutable interface
      foreach (Interface iface in Interface.Interfaces) {
        if (iface == null) continue;
        if (iface.GetAttribute(SystemTypes.ImmutableAttribute) != null && !ifaceIsImmutable)
          this.HandleError(Interface.Name, Error.MutableIfaceExtendsImmutableIface);
      }
    }
    private AttributeNode GetPurityAttribute(Method method) {
      if (method == null || method.Attributes == null) return null;
      return method.GetAttributeFromSelfOrDeclaringMember(SystemTypes.PureAttribute);
    }
    private bool PurityLessThanOrEqualTo(Method a, Method b) {
      if (b.IsStateIndependent) return true;
      if (b.IsConfined) return !a.IsStateIndependent;
      if (b.IsPure) return !(a.IsStateIndependent || a.IsConfined);
      return !(a.IsStateIndependent || a.IsConfined || a.IsPure);
    }
    public override Invariant VisitInvariant(Invariant inv) {
      if (inv == null) return null;
      SpecSharpCompilerOptions options = this.currentOptions as SpecSharpCompilerOptions;
      if (options != null && options.CheckContractAdmissibility) {
        AdmissibilityChecker checker = new AdmissibilityChecker(this);
        this.TransferStateTo(checker);
        checker.CheckInvariantAdmissibility(inv);
      }
      return base.VisitInvariant(inv);
    }
    public override ModelfieldContract VisitModelfieldContract(ModelfieldContract mfC) {
      if (mfC == null) return null;
      SpecSharpCompilerOptions options = this.currentOptions as SpecSharpCompilerOptions;
      if (options != null && options.CheckContractAdmissibility) {
        AdmissibilityChecker checker = new AdmissibilityChecker(this);
        this.TransferStateTo(checker);
        checker.CheckModelfieldAdmissibility(mfC);
      }
      return base.VisitModelfieldContract(mfC);
    }
    public override Method VisitMethod(Method method) {
      if (method == null) return null;

      // Need to call base visitor so that contract inheritance has already happened
      Method result = base.VisitMethod(method);

      #region MustOverride
      if (method.GetAttribute(Runtime.MustOverrideAttribute) != null
        && !method.IsVirtual) {
        this.HandleError(method.Name, Error.MethodMarkedMustOverrideMustBeVirtual, this.GetMethodSignature(method));
      }
      #endregion
      #region Check constraints when the method overrides a virtual method or implements an interface method
      if ((method.ImplementedInterfaceMethods != null && method.ImplementedInterfaceMethods.Count > 0)
            || (method.ImplicitlyImplementedInterfaceMethods != null && method.ImplicitlyImplementedInterfaceMethods.Count > 0)
            || method.OverriddenMethod != null
            || (method.IsPropertyGetter && method.DeclaringMember.OverriddenMember != null)
        ) {
        #region Purity must be consistent
        AttributeNode purityMarker = null;
        Method otherMethod = null;
        for (int i = 0, n = method.ImplementedInterfaceMethods == null ? 0 : method.ImplementedInterfaceMethods.Count; i < n; i++) {
          Method m = method.ImplementedInterfaceMethods[i];
          if (m == null) continue;
          purityMarker = GetPurityAttribute(m);
          if (purityMarker != null) {
            otherMethod = m;
            break; // FIXME! if one is marked, all should be marked the same!!
          }
        }
        if (otherMethod == null) {
          for (int i = 0, n = method.ImplicitlyImplementedInterfaceMethods == null ? 0 : method.ImplicitlyImplementedInterfaceMethods.Count; i < n; i++) {
            Method m = method.ImplicitlyImplementedInterfaceMethods[i];
            if (m == null) continue;
            purityMarker = GetPurityAttribute(m);
            if (purityMarker != null) {
              otherMethod = m;
              break; // FIXME! if one is marked, all should be marked the same!!
            }
          }
        }
        if (otherMethod == null){
          if (method.OverriddenMethod != null) {
            purityMarker = GetPurityAttribute(method.OverriddenMethod);
            if (purityMarker != null) {
              otherMethod = method.OverriddenMethod;
            }
          }
        }
        if (otherMethod == null) {
          if (method.IsPropertyGetter && method.DeclaringMember.OverriddenMember != null) {
            Property p = method.DeclaringMember.OverriddenMember as Property;
            if (p != null) {
              purityMarker = GetPurityAttribute(p.Getter);
              if (purityMarker != null) {
                otherMethod = p.Getter;
              }
            }
          }
        }
        Method methodToComplainAbout = method;
        ProxyMethod pm = methodToComplainAbout as ProxyMethod;
        if (pm != null) {
          methodToComplainAbout = pm.ProxyFor;
        }
        if (purityMarker != null) {
          if (!PurityLessThanOrEqualTo(otherMethod, methodToComplainAbout)) {
            string marking;
            if (otherMethod.IsPure)
              marking = "[Pure]";
            else if (otherMethod.IsConfined)
              marking = "[Pure]";
            else //(otherMethod.IsStateIndependent)
              marking = "[Pure][Reads(ReadsAttribute.Reads.Nothing)]";
            this.HandleError(methodToComplainAbout.Name, Error.MethodInheritingPurityMustBeMarked,
              this.GetMethodSignature(methodToComplainAbout),
              this.GetMethodSignature(otherMethod),
              marking);
          }
        }
        #endregion Purity must be consistent
      }
      #endregion Check constraints when the method overrides a virtual method or implements an inteface method

      #region Additive
      AttributeNode attrNode = method.GetAttribute(SystemTypes.AdditiveAttribute);
      AttributeNode overridenAttr = null;
      bool isAdditive = IsAdditive(attrNode);
      if (isAdditive && attrNode != null) {
        if (attrNode.Target == AttributeTargets.ReturnValue) {
          if (method.ReturnType.IsValueType) {
            this.HandleError(attrNode, Error.AttributeAllowedOnlyOnReferenceTypeParameters, this.GetTypeName(attrNode.Type));
          }
        } else if (method.DeclaringType.IsValueType) {
          this.HandleError(attrNode, Error.AttributeAllowedOnlyOnReferenceTypeParameters, this.GetTypeName(attrNode.Type));
        } else if (method.IsStatic) {
          // don't need to check for whether method is a ctor (which would be an error) because
          // if the method is a ctor, then an error will already have been generated since
          // ctors are not listed as a valid target for [Additive]
          this.HandleError(method, Error.StaticMethodCannotBeMarkedWithAttribute, this.GetTypeName(SystemTypes.AdditiveAttribute));
        }
      }
      Method overriden = method.OverriddenMethod;
      if (overriden != null) {
        overridenAttr = overriden.GetAttribute(SystemTypes.AdditiveAttribute);
        if (IsAdditive(overridenAttr) != isAdditive)
          this.HandleError(method, Error.OverrideMethodNotMarkedWithAttribute, this.GetTypeName(SystemTypes.AdditiveAttribute));
      }
      #endregion
      #region Inside
      attrNode = method.GetAttribute(SystemTypes.InsideAttribute);
      overridenAttr = null;
      bool isInside = IsInside(attrNode);
      if (isInside && attrNode != null) {
        if (attrNode.Target == AttributeTargets.ReturnValue) {
          if (method.ReturnType.IsValueType) {
            this.HandleError(attrNode, Error.AttributeAllowedOnlyOnReferenceTypeParameters, this.GetTypeName(attrNode.Type));
          }
        } else if (method.DeclaringType.IsValueType) {
          this.HandleError(attrNode, Error.AttributeAllowedOnlyOnReferenceTypeParameters, this.GetTypeName(attrNode.Type));
        } else if (method.DeclaringType is Interface) {
          this.HandleError(attrNode, Error.AttributeNotAllowedOnInterfaceMethods, this.GetTypeName(attrNode.Type));
        } else if (method.IsVirtual) {
          this.HandleError(method, Error.VirtualMethodWithNonVirtualMethodAttribute, this.GetTypeName(SystemTypes.InsideAttribute));
        }
      }
      #endregion
      #region Captured
      attrNode = method.GetAttribute(SystemTypes.CapturedAttribute);
      overridenAttr = null;
      bool isCaptured = attrNode != null;
      if (isCaptured) {
        if (attrNode.Target != AttributeTargets.ReturnValue && method.DeclaringType.IsValueType) {
          // if its target is "return", then it will already have gotten an error for that
          // just because that isn't included in the valid targets for that attribute
          this.HandleError(attrNode, Error.AttributeAllowedOnlyOnReferenceTypeParameters, this.GetTypeName(attrNode.Type));
        }
      }
      overriden = method.OverriddenMethod;
      if (overriden != null) {
        overridenAttr = overriden.GetAttribute(SystemTypes.CapturedAttribute);
        if (overridenAttr != null && !isCaptured)
          this.HandleError(method, Error.OverrideMethodNotMarkedWithAttribute, this.GetTypeName(SystemTypes.CapturedAttribute));
      }
      #endregion
      #region Can't have both [Rep] and [Peer]
      bool isRep = method.GetAttribute(SystemTypes.RepAttribute) != null;
      bool isPeer = method.GetAttribute(SystemTypes.PeerAttribute) != null;
      if (isRep && isPeer) {
        this.HandleError(method, Error.ConflictingAttributes, this.GetTypeName(SystemTypes.RepAttribute),
          this.GetTypeName(SystemTypes.PeerAttribute));
      }
      #endregion

      #region NoReferenceComparison
      attrNode = method.GetAttribute(SystemTypes.NoReferenceComparisonAttribute);
      overridenAttr = null;
      if (overriden != null)
        overridenAttr = overriden.GetAttribute(SystemTypes.NoReferenceComparisonAttribute);
      if (attrNode == null && overridenAttr != null)
        this.HandleError(method, Error.NoReferenceComparisonAttrNotCopied, method.Name.ToString());
      else if (attrNode != null && !(method.IsPure || method.IsConfined || method.IsStateIndependent))
        this.HandleError(method, Error.NonPureMarkedNoReferenceComparison);
      else if (attrNode != null) {
        NoReferenceComparisonVisitor visitor = new NoReferenceComparisonVisitor(this);
        this.TransferStateTo(visitor);
        visitor.Visit(method.Body);
        if (!visitor.HasNoReferenceComparison)
          this.HandleError(method, Error.ViolatedNoReferenceComparison, method.Name.ToString());
      }
      #endregion
      #region ResultNotNewlyAllocated
      attrNode = method.GetAttribute(SystemTypes.ResultNotNewlyAllocatedAttribute);
      overridenAttr = null;
      if (overriden != null)
        overridenAttr = overriden.GetAttribute(SystemTypes.ResultNotNewlyAllocatedAttribute);
      if (attrNode == null && overridenAttr != null)
        this.HandleError(method, Error.ResultNotNewlyAllocatedAttrNotCopied, method.Name.ToString());
      else if (attrNode != null && !(method.IsPure || method.IsConfined || method.IsStateIndependent))
        this.HandleError(method, Error.NonPureMarkedResultNotNewlyAllocated);
      else if (attrNode != null && (method.ReturnType == null || method.ReturnType.IsValueType))
        this.HandleError(method, Error.NonRefTypeMethodMarkedResultNotNewlyAllocated);
      #endregion
      #region Pure methods cannot have ref parameters
      if (!method.IsPropertyGetter && (method.IsPure || method.IsConfined || method.IsStateIndependent)) {
        // don't check property getters: they already will have an error if they have out or ref parameters.
        for (int i = 0, n = method.Parameters == null ? 0 : method.Parameters.Count; i < n; i++) {
          Parameter p = method.Parameters[i];
          if (p.IsOut) continue;
          Reference r = p.Type as Reference;
          if (r != null) {
            this.HandleError(p, Error.PureMethodCannotHaveRefParam);
          }
        }
      }
      #endregion
      #region Pure methods cannot have modifies clauses
      if ((method.IsPure || method.IsConfined || method.IsStateIndependent) && method.Contract != null && method.Contract.Modifies != null && 0 < method.Contract.Modifies.Count) {
        this.HandleError(method, Error.PureMethodCannotHaveModifies);
      }
      #endregion Pure methods cannot have modifies clauses

      bool b;
      TypeNode returnType = method.ReturnType;
      if (returnType != null)
        returnType = returnType.StripOptionalModifiers(out b);
      if ((method.GetAttribute(SystemTypes.RepAttribute) != null || method.GetAttribute(SystemTypes.PeerAttribute) != null) && 
          !(returnType.IsReferenceType || (method.IsPropertyGetter && ((Property)method.DeclaringMember).Type.IsReferenceType)))
        this.HandleError(method, Error.BadUseOfOwnedOnMethod);
      else if (method.IsVirtual && !method.IsPropertyGetter && MemberIsRep(method)) // note: second conjunct mainly to get Boogie through
        this.HandleError(method, Error.UseOfRepOnVirtualMethod);

      #region Make sure purity attributes are used correctly
      AttributeNode pureAttr = method.GetAttributeFromSelfOrDeclaringMember(SystemTypes.PureAttribute);
      AttributeNode readsAttr = method.GetAttributeFromSelfOrDeclaringMember(SystemTypes.ReadsAttribute);
      if (readsAttr != null) {
        if (pureAttr == null) {
          this.HandleError(method, Error.ReadsWithoutPure);
        }
        Literal l = readsAttr.GetPositionalArgument(0) as Literal;
        // default ctor for Reads sets it to Owned
        Microsoft.Contracts.ReadsAttribute.Reads r =
          l == null ?
          Microsoft.Contracts.ReadsAttribute.Reads.Owned :
          (Microsoft.Contracts.ReadsAttribute.Reads)l.Value;
        switch (r) {
          case Microsoft.Contracts.ReadsAttribute.Reads.Everything:
            if (!method.IsPure)
              this.HandleError(method, Error.InconsistentPurityAttributes);
            break;
          case Microsoft.Contracts.ReadsAttribute.Reads.Nothing:
            if (!method.IsStateIndependent)
              this.HandleError(method, Error.InconsistentPurityAttributes);
            break;
          case Microsoft.Contracts.ReadsAttribute.Reads.Owned:
            if (!method.IsConfined)
              this.HandleError(method, Error.PureOwnedNotAllowed);
            break;
        }
      }
      #endregion Make sure purity attributes are used correctly

      return result;
    }
    private bool MemberIsRep (Member m) {
      if (m == null) return false;
      return m.GetAttribute(SystemTypes.RepAttribute) != null;
    }
    private bool IsAdditive(AttributeNode attr) {
      if (attr == null || attr.Type != SystemTypes.AdditiveAttribute) return false;
      ExpressionList exprs = attr.Expressions;
      if (exprs == null || exprs.Count != 1) return true;
      Literal lit = exprs[0] as Literal;
      if (lit != null && (lit.Value is bool)) return (bool)lit.Value;
      else return false;
    }
    private bool IsInside(AttributeNode attr) {
      if (attr == null || attr.Type != SystemTypes.InsideAttribute) return false;
      ExpressionList exprs = attr.Expressions;
      if (exprs == null || exprs.Count != 1) return true;
      Literal lit = exprs[0] as Literal;
      if (lit != null && (lit.Value is bool)) return (bool)lit.Value;
      else return false;
    }
    private bool MethodIsOwned(Method m) {
      if (m == null) return false;
      return m.GetAttribute(SystemTypes.RepAttribute) != null || m.GetAttribute(SystemTypes.PeerAttribute) != null;
    }
    public override Namespace VisitNamespace(Namespace nspace) {
      if (nspace == null) return null;
      Scope savedScope = this.scope;
      NamespaceScope ns = new NamespaceScope();
      ns.AssociatedNamespace = nspace;
      this.scope = ns;
      Namespace result = base.VisitNamespace(nspace);
      this.scope = savedScope;
      return result;
    }
    public override Expression VisitMemberBinding(MemberBinding memberBinding, bool isTargetOfAssignment) {
      if (memberBinding == null) return null;
      if (memberBinding.Type is Pointer && !this.typeSystem.insideUnsafeCode && !this.insideInvariant) {
        this.HandleError(memberBinding, Error.UnsafeNeeded);
        return null;
      }
      Field f = memberBinding.BoundMember as Field;
      if (f != null && (f.Flags & FieldFlags.NotSerialized) != 0 && (f.Flags & FieldFlags.FieldAccessMask) == FieldFlags.CompilerControlled &&
        memberBinding.TargetObject is ImplicitThis) {
        this.HandleError(memberBinding, Error.ForwardReferenceToLocal, f.Name.ToString());
      }
      if (!insideModifies && f != null && f.IsModelfield && isTargetOfAssignment) //modelfields should not be assigned to
        this.HandleError(memberBinding, Error.AssignToModelfield, null);
      return base.VisitMemberBinding(memberBinding, isTargetOfAssignment);
    }
    public override Expression VisitMethodCall(MethodCall call) {
      if (call == null) return null;
      MemberBinding mb = call.Callee as MemberBinding;
      Method m = mb == null ? null : mb.BoundMember as Method;
      if (m != null && !this.typeSystem.insideUnsafeCode) {
        bool badMethod = m.ReturnType is Pointer;
        for (int i = 0, n = m.Parameters == null ? 0 : m.Parameters.Count; i < n; i++) {
          Parameter p = m.Parameters[i];
          if (p == null) continue;
          if (p.Type is Pointer) { badMethod = true; break; }
        }
        if (badMethod && call.SourceContext.Document != null) {
          this.HandleError(call, Error.UnsafeNeeded);
          return null;
        }
      }
      #region Pure methods that have an out parameter cannot be used in a contract
      // This is checked here and not where ref parameters are checked (VisitMethod)
      // because we want to allow pure methods to have out parameters for use in purity
      // analysis. It is just that Boogie doesn't generate the right axioms, etc. for
      // pure methods that have out parameters. So for now, just don't allow them in
      // contracts.
      if ((insideMethodContract || insideInvariant || insideAssertOrAssume)
        && m != null && !m.IsPropertyGetter
        && (m.IsPure || m.IsConfined || m.IsStateIndependent)) {
        // don't check property getters: they already will have an error if they have out parameters.
        for (int i = 0, n = m.Parameters == null ? 0 : m.Parameters.Count; i < n; i++) {
          Parameter p = m.Parameters[i];
          if (p.IsOut) {
            this.HandleError(p, Error.PureMethodWithOutParamUsedInContract);
          }
        }
      }
      #endregion


      // if any two arguments Might Return Newly Allocated Object then method must be marked NoReferenceComparison
      if ((insideMethodContract || insideInvariant || insideAssertOrAssume) && m != null && mb != null) {
        int MRNAOargs = 0;
        MightReturnNewlyAllocatedObjectVisitor visitor = new MightReturnNewlyAllocatedObjectVisitor(this);
        this.TransferStateTo(visitor);
        visitor.CurrentMethod = this.currentMethod;
        // receiver
        visitor.Visit(mb);
        if (visitor.IsMRNAO)
          MRNAOargs++;
        else // parameters          
          for (int i = 0, n = call.Operands == null ? 0 : call.Operands.Count; i < n; i++) {
            Expression exp = call.Operands[i];
            visitor.IsMRNAO = false;
            visitor.Visit(exp);
            if (visitor.IsMRNAO) {
              MRNAOargs++;
            }
          }
        if (MRNAOargs > 1 && m.GetAttribute(SystemTypes.NoReferenceComparisonAttribute) == null) {
          this.HandleError(call, Error.MethodShouldBeMarkedNoReferenceComparison, m.Name.ToString());
          return null;
        }

      }

      return base.VisitMethodCall(call);
    }
    private bool IsReferenceTypeButNotArray(TypeNode t) {
      t = this.typeSystem.Unwrap(t) as TypeNode;
      return !t.IsValueType && (!(t is ArrayType));
    }
    public override MethodContract VisitMethodContract (MethodContract contract) {
      if (contract == null) return null;
      ExpressionList modifies = contract.Modifies;
      if (modifies != null) {
        for (int i = 0, n = modifies.Count; i < n; i++) {
          #region modifies g, E.x, E[E0, E1, ... ]
          UnaryExpression ue = modifies[i] as UnaryExpression;
          if (ue != null) {
            Expression operand = ue.Operand;
            if (operand == null) continue; // something else is wrong: let other parts of the compiler warn about it
            MemberBinding mb = operand as MemberBinding;
            if (mb != null && mb.BoundMember is Field) {
              Field field = mb.BoundMember as Field;
              #region modifies g
              if (field != null && field.IsStatic) {
                if (mb.TargetObject != null) {
                  this.HandleError(modifies[i], Error.InvalidModifiesClause, ": invalid static field reference");
                  modifies[i] = null;
                  continue;
                }
                continue; // OK
              }
              #endregion
              #region modifies E.x
              // last conjunct is because at this stage, parameters look like "<implicit this>.p"
              if (field != null && !field.IsStatic && IsReferenceTypeButNotArray(mb.TargetObject.Type) && (!(field is ParameterField))) continue;
              this.HandleError(modifies[i], Error.InvalidModifiesClause, ": 'E.x' is allowed only when E has a reference type, not '" + this.GetTypeName(mb.TargetObject.Type) + "'");
              modifies[i] = null;
              continue;
            #endregion
            }
            #region modifies E[E0, E1, ... ]
            Indexer ie = ue.Operand as Indexer;
            if (ie != null) continue; // other checks will make sure that E is an array with the right rank and the indices are the right type
            #endregion
            #region Exhausted all possibilities, but don't issue an error.
            // Assume (!!?!) that some other check will complain, e.g., "modifies o.f" where the type of o doesn't have a field f
            //this.HandleError(modifies[i], Error.InvalidModifiesClause, "");
            //modifies[i] = null;
            continue;
            #endregion
          }
          #endregion
          #region modifies E*, E.**, E.0, E[*]
          ModifiesClause mc = modifies[i] as ModifiesClause;
          if (mc == null || mc.Operands == null || mc.Operands.Count != 1) {
            // Assume (!!?!) that some other check will complain, e.g., "modifies o.f" where the type of o doesn't have a field f
            //this.HandleError(mc.Operands[0], Error.InvalidModifiesClause, "");
            //modifies[i] = null;
            continue;
          }
          TypeNode operandType = mc.Operands[0].Type;
          TypeNode unwrapped = this.typeSystem.Unwrap(operandType);
          #region modifies E.*
          if (mc is ModifiesObjectClause && !IsReferenceTypeButNotArray(operandType)) {
            this.HandleError(mc.Operands[0], Error.InvalidModifiesClause, ": 'E.*' is allowed only when E has a reference type, not '" + this.GetTypeName(operandType) + "'");
            modifies[i] = null;
            continue;
          }
          #endregion
          #region modifies E.**
          if (mc is ModifiesPeersClause && unwrapped.IsValueType) {
            this.HandleError(mc.Operands[0], Error.InvalidModifiesClause, ": 'E.**' is allowed only when E has a reference type, not '" + this.GetTypeName(operandType) + "'");
            modifies[i] = null;
            continue;
          }
          #endregion
          #region modifies E.0
          if (mc is ModifiesNothingClause && unwrapped is Reference || unwrapped.IsValueType) { // a Reference should be created only when the type of the thing is *not* a reference type
            this.HandleError(mc.Operands[0], Error.InvalidModifiesClause, ": 'E.0' is allowed only when E has a reference type, not '" + this.GetTypeName(operandType) + "'");
            modifies[i] = null;
            continue;
          }
          #endregion
          #region modifies E[*]
          if (mc is ModifiesArrayClause && (!(unwrapped is ArrayType))) {
            this.HandleError(mc.Operands[0], Error.InvalidModifiesClause, ": E[*] is allowed only when E has an array type, not " + this.GetTypeName(operandType));
            modifies[i] = null;
            continue;
          }
          #endregion
          #endregion
        }
      }
      SpecSharpCompilerOptions options = this.currentOptions as SpecSharpCompilerOptions;
      if (options != null && options.CheckContractAdmissibility) {
        AdmissibilityChecker checker = new AdmissibilityChecker(this);
        this.TransferStateTo(checker);
        Method method = contract.DeclaringMethod;
        if (method == contract.OriginalDeclaringMethod) {
          bool isPure = method.IsPure;
          if (method.IsPure || method.IsConfined || method.IsStateIndependent) {
            foreach (Requires r in contract.Requires)
              checker.CheckMethodSpecAdmissibility(r.Condition, method, isPure, false);
            foreach (Ensures e in contract.Ensures)
              checker.CheckMethodSpecAdmissibility(e.PostCondition, method, isPure, false);
          }
        }
      }
      return base.VisitMethodContract(contract);
    }

    public override Expression CoerceArgument(Expression argument, Parameter parameter) {
      Expression result = base.CoerceArgument(argument, parameter);
      if (result != null) {
        Reference r = parameter.Type as Reference;
        if (r != null) {
          UnaryExpression unexp = argument as UnaryExpression;
          if (parameter.IsOut) {
            if (unexp == null) {
              if (argument.SourceContext.Document != null)
                this.HandleError(argument, Error.NoImplicitConversion, this.typeSystem.GetTypeName(r.ElementType), this.typeSystem.GetTypeName(r));
            } else {
              if (unexp.NodeType == NodeType.RefAddress)
                this.HandleError(argument, Error.NoImplicitConversion, "ref " + this.typeSystem.GetTypeName(r.ElementType), this.typeSystem.GetTypeName(r));
              else if (unexp.NodeType != NodeType.OutAddress)
                this.HandleError(argument, Error.NoImplicitConversion, this.typeSystem.GetTypeName(r.ElementType), this.typeSystem.GetTypeName(r));
            }
          } else {
            if (unexp == null) {
              if (argument.SourceContext.Document != null)
                this.HandleError(argument, Error.NoImplicitConversion, this.typeSystem.GetTypeName(r.ElementType), this.typeSystem.GetTypeName(r));
            } else {
              if (unexp.NodeType == NodeType.OutAddress)
                this.HandleError(argument, Error.NoImplicitConversion, "out " + this.typeSystem.GetTypeName(r.ElementType), this.typeSystem.GetTypeName(r));
              else if (unexp.NodeType != NodeType.RefAddress)
                this.HandleError(argument, Error.NoImplicitConversion, this.typeSystem.GetTypeName(r.ElementType), this.typeSystem.GetTypeName(r));
            }
          }
        } else if (!(parameter.Type is Reference)) {
          if (argument.NodeType == NodeType.OutAddress)
            this.HandleError(((UnaryExpression)argument).Operand, Error.BadArgExtraRef, (parameter.ParameterListIndex + 1).ToString(), "out");
          else if (argument.NodeType == NodeType.RefAddress)
            this.HandleError(((UnaryExpression)argument).Operand, Error.BadArgExtraRef, (parameter.ParameterListIndex + 1).ToString(), "ref");
        }
      }
      // Special case to handle arglist when the parameter is a params object[]
      // and the argument constains __arglist(). We will flatten that __arglist. 
      // This is an effort to simulate CSC. 
      // Note, if passing an __arglist and an object for params object[], CSC generates
      // invalid code while we are ok. See SingSharpConformance.Suite. 
      TypeNode maybeObjectType = parameter.GetParamArrayElementType();
      if (maybeObjectType == SystemTypes.Object) {
        result = FlattenArglist(result);
      }
      return result;
    }

    Expression FlattenArglist(Expression result) {
      ConstructArray consArr = result as ConstructArray;
      if (consArr != null) {
        ExpressionList el = new ExpressionList();
        bool hasArglist = false;
        foreach (Expression e in consArr.Initializers) {
          BinaryExpression binary = e as BinaryExpression;
          if (binary != null) {
            ArglistArgumentExpression aae = binary.Operand1 as ArglistArgumentExpression;
            if (aae != null) {
              hasArglist = true;
              foreach (Expression arg in aae.Operands) {
                // Elements in the arglist are not yet coerced. For example, a number 3 must be
                // boxed to be an object. 
                Expression coercedArg = this.typeSystem.ImplicitCoercion(arg, SystemTypes.Object);
                el.Add(coercedArg);
              }
              continue;
            }
          }
          el.Add(e);
        }
        if (hasArglist) consArr.Initializers = el;
        return consArr;
      }
      return result;
    }

    public override AttributeNode VisitParameterAttribute(AttributeNode attr, Parameter parameter) {
      attr = base.VisitParameterAttribute(attr, parameter);
      if (attr == null) return null;
      if (attr.Type == SystemTypes.ParamArrayAttribute && attr.SourceContext.Document != null)
        this.HandleError(attr, Error.ExplicitParamArray, this.GetTypeName(SystemTypes.ParamArrayAttribute));
      if ((IsAdditive(attr) || IsInside(attr) || attr.Type == SystemTypes.CapturedAttribute || attr.Type == SystemTypes.NoDefaultContractAttribute)
          && parameter.Type != null && parameter.Type.IsValueType) {
        this.HandleError(attr, Error.AttributeAllowedOnlyOnReferenceTypeParameters, this.GetTypeName(attr.Type));
      }
      if (attr.Type == SystemTypes.CapturedAttribute && parameter.IsOut) {
        this.HandleError(attr, Error.AttributeNotAllowedOnlyOnInParameters, this.GetTypeName(attr.Type));
      }
      return attr;
    }
    public override Property VisitProperty(Property property) {
      property = base.VisitProperty(property);
      if (property == null) return null;
      Method m = property.Getter;
      if (m == null) m = property.Setter;
      if (m != null && m.IsStatic && m.IsVirtual)
        this.HandleError(property.Name, Error.StaticNotVirtual, this.GetMemberSignature(property));
      return property;
    }
    public override TypeNode VisitTypeNode(TypeNode typeNode) {
      if (typeNode == null) return null;
      TypeNode result = base.VisitTypeNode(typeNode);
      if (result == null) return null;
      if (result.Name == null) return result;
      if (!(typeNode is EnumNode)) {
        MemberList members = result.Members;
        for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++) {
          Member m = members[i];
          if (m == null || m.Name == null) continue;
          if (m.Name.UniqueIdKey == result.Name.UniqueIdKey) {
            Method meth = m as Method;
            if (meth != null && meth.ImplementedTypes != null && meth.ImplementedTypes.Count > 0)
              continue;
            Property p = m as Property;
            if (p != null && p.ImplementedTypes != null && p.ImplementedTypes.Count > 0)
              continue;
            this.HandleError(m.Name, Error.MemberNameSameAsType, result.Name.ToString());
            this.HandleRelatedError(result);
          }
        }
      }
      return result;
    }
    public override Expression VisitUnaryExpression(UnaryExpression unaryExpression) {
      if (unaryExpression == null) return null;
      switch (unaryExpression.NodeType) {
        case NodeType.AddressOf:
          if (unaryExpression.Type is Reference) return unaryExpression;
          if (!this.typeSystem.insideUnsafeCode && unaryExpression.SourceContext.Document != null) {
            this.HandleError(unaryExpression, Error.UnsafeNeeded);
            return null;
          }

          // change by drunje: you can obtain a pointer to managed structure
          Expression opnd = unaryExpression.Operand = this.VisitExpression(unaryExpression.Operand);
          if (opnd != null && this.CheckFixed(opnd) && opnd.Type != null && !opnd.Type.IsUnmanaged) {
            // this is not compatible with C#
            SpecSharpCompilerOptions options = this.currentOptions as SpecSharpCompilerOptions;
            if (options != null && options.AllowPointersToManagedStructures) {

              // pointers to all structures are allowed now
              if (opnd.Type.IsValueType)
                return unaryExpression;

              // we can convert a reference on any structure to a pointer as well
              if ((opnd.Type is Reference) && ((opnd.Type as Reference).ElementType.IsValueType))
                return unaryExpression;
            }

            if ((opnd.Type is Reference) && ((opnd.Type as Reference).ElementType.IsUnmanaged))
              return unaryExpression;

            this.HandleError(unaryExpression, Error.ManagedAddr, this.GetTypeName(opnd.Type));
            return null;
          }
          // end of change by drunje

          return unaryExpression;
        default:
          break;
      }
      return base.VisitUnaryExpression(unaryExpression);
    }
    public override AttributeNode VisitUnknownAssemblyAttribute(TypeNode attrType, AttributeNode attr, AssemblyNode target) {
      if (attrType == Runtime.PostCompilationPluginAttributeType) {
        SpecSharpCompilation ssCompilation = this.ssCompilation;
        if (ssCompilation == null) { Debug.Assert(false); return null; }
        //        if (this.currentPreprocessorDefinedSymbols == null || !this.currentPreprocessorDefinedSymbols.ContainsKey("CODE_ANALYSIS"))
        //          return null;
        ExpressionList args = attr.Expressions;
        if (args != null && args.Count > 0) {
          UnaryExpression expr = args[0] as UnaryExpression;
          NamedArgument nArg = args[0] as NamedArgument;
          if (nArg != null) expr = nArg.Value as UnaryExpression;
          if (expr == null || expr.Type != SystemTypes.Type) return null;
          Literal typeLit = expr.Operand as Literal;
          if (typeLit == null) return null;
          TypeNode pluginType = (TypeNode)typeLit.Value;
          if (!pluginType.IsNormalized) {
            this.HandleError(typeLit, Error.PluginTypeMustAlreadyBeCompiled, this.GetTypeName(pluginType));
            return null;
          }
          ssCompilation.AddPlugin(pluginType, (TypeSystem)this.typeSystem, typeLit);
        }
        attr.IsPseudoAttribute = true;
        return attr;
      }
      return base.VisitUnknownAssemblyAttribute(attrType, attr, target);
    }

    public override bool InterfaceAllowsThisKindOfField(Field field) {
      if (field == null) return true;
      return false;
    }
    internal static TypeNode GetMemberType(Member mem) {
      if (mem == null) return null;
      Field f = mem as Field;
      if (f != null)
        return f.Type;

      Property p = mem as Property;
      if (p != null)
        return p.Type;

      //todo: other possibilities?
      return null;
    }

    static string GetNamedArgument(Identifier name, AttributeNode a) {
      NamedArgument na = GetNamedArgument(name, a.Expressions);
      if (na != null && na.Value is Literal) {
        Literal lit = (Literal)na.Value;
        return lit.Value.ToString();
      }
      if (a.Expressions.Count == 1 && a.Expressions[0] is Literal) {
        Literal lit = (Literal)a.Expressions[0];
        return lit.Value.ToString();
      }
      return null;
    }

    static NamedArgument GetNamedArgument(Identifier id, ExpressionList list) {
      for (int i = 0, n = list.Count; i < n; i++) {
        Expression e = list[i];
        if (e is NamedArgument) {
          NamedArgument na = (NamedArgument)e;
          if (na.Name.UniqueIdKey == id.UniqueIdKey)
            return na;
        }
      }
      return null;
    }
    public override void DetermineIfNonNullCheckingIsDesired(CompilationUnit cUnit) {
      SpecSharpCompilerOptions soptions = this.currentOptions as SpecSharpCompilerOptions;
      if (soptions != null && soptions.Compatibility) return;
      this.NonNullChecking = !(this.currentPreprocessorDefinedSymbols != null && this.currentPreprocessorDefinedSymbols.ContainsKey("NONONNULLTYPECHECK"));
    }
    internal void HandleError(Node offendingNode, Error error, params string[] messageParameters) {
      if (this.ErrorHandler == null) return;
      ((ErrorHandler)this.ErrorHandler).HandleError(offendingNode, error, messageParameters);
    }
    public override void HandleError(Node offendingNode, SysError error, params string[] messageParameters) {
      if (this.ErrorHandler == null) return;
      ((ErrorHandler)this.ErrorHandler).currentType = this.currentType;
      ((ErrorHandler)this.ErrorHandler).refOrOutAddress = this.refOrOutAddress;
      this.ErrorHandler.HandleError(offendingNode, error, messageParameters);
    }
    public override string GetTypeName(TypeNode type) {
      if (this.ErrorHandler == null) { return ""; }
      ((ErrorHandler)this.ErrorHandler).currentParameter = this.typeSystem.currentParameter;
      return this.ErrorHandler.GetTypeName(type);
    }

    public override void CheckHidingAndOverriding(MemberList members, TypeNode baseType) {
      foreach (Member mem in members) {
        if (mem is Method && (mem as Method).OverridesBaseClassMember) {
          Method meth = mem as Method;
          MemberList baseMembers = this.GetTypeView(baseType).GetMembersNamed(mem.Name);
          ParameterList parameters = meth.Parameters;

          for (int j = 0, m = baseMembers == null ? 0 : baseMembers.Count; j < m; j++) {
            Method baseMethod = baseMembers[j] as Method;
            if (baseMethod != null && baseMethod.ParametersMatchStructurally(parameters)) {
              ParameterList bparams = baseMethod.Parameters;
              for (int i = 0, num = parameters == null ? 0 : parameters.Count; i < num; i++) {
                Parameter pmeth = parameters[i];
                Parameter pBaseMeth = bparams[i];

                AttributeNode an1 = pmeth.GetAttribute(ExtendedRuntimeTypes.DelayedAttribute);
                AttributeNode an2 = pBaseMeth.GetAttribute(ExtendedRuntimeTypes.DelayedAttribute);

                if (an2 != null && an1 == null) {
                  this.HandleError(pmeth, Error.ParameterNoDelayAttribute, pmeth.Name.Name);
                  // report an error: override method should explicitly write
                  // [Delayed] for parameter
                }

                if (an1 != null && an2 == null) {
                  this.HandleError(pmeth, Error.ParameterExtraDelayAttribute, pmeth.Name.Name);
                  // report an error: override method should not invent the
                  // [Delayed] attribute for parameter
                }
              }


              AttributeNode anode1 = meth.GetAttribute(ExtendedRuntimeTypes.DelayedAttribute);
              AttributeNode anode2 = baseMethod.GetAttribute(ExtendedRuntimeTypes.DelayedAttribute);

              if (anode2 != null && anode1 == null) {
                // report an error: override method should explicitly write
                // [Delayed] for this
                this.HandleError(meth, Error.ThisParameterNoDelayAttribute, meth.Name.Name);
              }

              if (anode1 != null && anode2 == null) {
                this.HandleError(meth, Error.ThisParameterExtraDelayAttribute, meth.Name.Name);
              }

            }
          }
        }
      } 
      base.CheckHidingAndOverriding(members, baseType);
    }    
  }
}
