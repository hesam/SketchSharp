//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Text;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  public enum Error{
    None = 0,

    AbstractAndExtern,
    AbstractBaseCall,
    AbstractEventInitializer,
    AbstractHasBody,
    AbstractInterfaceMethod,
    AbstractMethodInConcreteType,
    AbstractMethodTemplate,
    AbstractSealedArrayElementType,
    AbstractSealedBaseClass,
    AbstractSealedFieldType,
    AbstractSealedLocalType,
    AbstractSealedParameterType,
    AbstractSealedReturnType,
    AccessToNonStaticOuterMember,
    AmbiguousBinaryOperation,
    AmbiguousCall,
    AmbiguousConditional,
    AmbiguousTypeReference,
    AnonymousNestedFunctionNotAllowed,
    ArrayElementCannotBeTypedReference,
    ArrayInitializerLengthMismatch,
    AsMustHaveReferenceType,
    AsWithTypeVar,
    AssemblyCouldNotBeSigned,
    AssemblyKeyFileMissing,
    AssignmentHasNoEffect,
    AssignmentToBase,
    AssignmentToEvent,
    AssignmentToLiteral,
    AssignmentToType,
    AssignmentToReadOnlyInstanceField,
    AssignmentToReadOnlyLocal,
    AssignmentToReadOnlyStaticField,
    AttributeHasBadTarget,
    AttributeOnBadTarget,
    BadAttributeParam,
    BadBinaryOperatorSignature,
    BadBinaryOps,
    BadBoolOp,
    BadBox,
    BadCallToEventHandler,
    BadEmptyThrow,
    BadExceptionType,
    BadExitOrContinue,
    BadFinallyLeave,
    BadForeachCollection,
    BadGetEnumerator,
    BadIncDecSignature,
    BadLHSideForAssignment,
    BadNamedAttributeArgument,
    BadNestedTypeReference,
    BadNonEmptyStream,
    BadNonNull,
    BadNonNullOnStream,
    BadRefCompareLeft,
    BadRefCompareRight,
    BadStream,
    BadStreamOnNonNullStream,
    BadTupleIndex,
    BadUseOfEvent,
    BadUseOfMethod,
    BadUnaryOp,
    BadUnaryOperatorSignature,
    BaseClassLessAccessible,
    BaseInBadContext,
    BaseInStaticCode,
    BaseInterfaceLessAccessible,
    BitwiseOrSignExtend,
    CannotCallSpecialMethod,
    CannotCoerceNullToValueType,
    CannotDeferenceNonPointerType,
    CannotDeriveFromInterface,
    CannotDeriveFromSealedType,
    CannotDeriveFromSpecialType,
    CannotExplicitlyImplementAccessor,
    CannotInferMethTypeArgs,
    CannotOverrideAccessor,
    CannotOverrideFinal,
    CannotOverrideNonEvent,
    CannotOverrideNonVirtual,
    CannotOverrideSpecialMethod,
    CannotReadResource,
    CannotReturnTypedReference,
    CannotReturnValue,
    CannotYieldFromCatchClause,
    CannotYieldFromTryBlock,
    CircularBase,
    CircularConstantDefinition,
    ClashWithLocalConstant,
    ClashWithLocalVariable,
    CloseUnimplementedInterfaceMember,
    CLSNotOnModules,
    ComImportWithoutGuidAttribute,
    ConcreteMissingBody,
    ConditionalMustReturnVoid,
    ConditionalOnInterfaceMethod,
    ConditionalOnOverride,
    ConditionalOnSpecialMethod,
    ConflictBetweenAliasAndType,
    ConstantExpected,
    ConstructsAbstractClass,
    ConversionNotInvolvingContainedType,
    ConversionWithBase,
    ConversionWithDerived,
    ConversionWithInterface,
    ContainingTypeDoesNotImplement,
    CTOverflow,
    CustomAttributeError,
    CycleInInterfaceInheritance,
    DefaultNotAllowedInTypeswitch,
    DidNotExpect,
    DllImportOnInvalidMethod,
    DuplicateAliasDefinition,
    DuplicateAssemblyReference,
    DuplicateAttribute,
    DuplicateCaseLabel,
    DuplicateConversion,
    DuplicateIndexer,
    DuplicateInterfaceInBaseList,
    DuplicateMethod,
    DuplicateModuleReference,
    DuplicateNamedAttributeArgument,
    DuplicateParameterName,
    DuplicateResponseFile,
    DuplicateType,
    DuplicateTypeMember,
    DuplicateUsedNamespace,
    EnumerationValueOutOfRange,
    EqualityOpWithoutEquals,
    EqualityOpWithoutGetHashCode,
    Error,
    EventNotDelegate,
    ExplicitDefaultConstructorForValueType,
    ExplicitPropertyAddingAccessor,
    ExplicitPropertyMissingAccessor,
    ExplicitlyImplementedTypeNotInterface,
    ExpressionIsAlreadyOfThisType,
    FamilyInSealed,
    FamilyInStruct,
    FatalError,
    FieldOffsetNotAllowed,
    FieldOffsetNotAllowedOnStaticField,
    FieldTypeLessAccessibleThanField,
    FixedMustInit,
    GotoLeavesNestedMethod,
    HidesAbstractMethod,
    IdentifierNotFound,
    IdentityConversion,
    ImpossibleCast,
    InaccessibleEventBackingField,
    InconsistantIndexerNames,
    IndexerInAbstractSealedClass,
    IndexerNameAttributeOnOverride,
    IndexerNameNotIdentifier,
    InstanceFieldInitializerInStruct,
    IntegerDivisionByConstantZero,
    IntegralTypeValueExpected,
    InterfaceHasConstructor,
    InterfaceHasField,
    InterfaceImplementedByConditional,
    InterfaceLessAccessible,
    InterfaceMemberHasBody,
    InterfaceMemberNotFound,
    InternalCompilerError,
    InvalidAttributeArgument,
    InvalidCodePage,
    InvalidCompilerOption,
    InvalidCompilerOptionArgument,
    InvalidConditional,
    InvalidGotoCase,
    InvalidMainMethodSignature,
    InvalidOutputFile,
    InvalidDebugInformationFile,
    IsAlwaysOfType,
    IsBinaryFile,
    IsNeverOfType,
    LabelIdentiferAlreadyInUse,
    LabelNotFound,
    LockNeedsReference,
    MemberDoesNotHideBaseClassMember,
    MemberHidesBaseClassMember,
    MemberHidesBaseClassOverridableMember,
    MemberNotVisible,
    MethodNameExpected,
    MissingStructOffset,
    MultipleMainMethods,
    MultipleTypeImport,
    MustHaveOpTF,
    NegativeArraySize,
    NestedFunctionDelegateParameterMismatch,
    NestedFunctionDelegateParameterMismatchBecauseOfOutParameter,
    NestedFunctionDelegateReturnTypeMismatch,
    NoExplicitCoercion,
    NoGetter,
    NoGetterToOverride,
    NoImplicitCoercion,
    NoImplicitCoercionFromConstant,
    NoMainMethod,
    NoMethodMatchesDelegate,
    NoMethodToOverride,
    NoOverloadWithMatchingArgumentCount,
    NoPropertyToOverride,
    NoSetter,
    NoSetterToOverride,
    NoSourceFiles,
    NoSuchConstructor,
    NoSuchField,
    NoSuchLabel,
    NoSuchMember,
    NoSuchMethod,
    NoSuchOperator,
    NoSuchQualifiedType,
    NoSuchVariable,
    NoSuchType,
    NonObsoleteOverridingObsolete,
    NotAnAssembly,
    NotAnAttribute,
    NotAnInterface,
    NotAModule,
    NotAssignable,
    NotADelegate,
    NotATemplateType,
    NotAType,
    NotConstantExpression,
    NotIndexable,
    NotVisibleViaBaseType,
    NotYetImplemented,
    NullNotAllowed,
    ObjectRequired,
    ObsoleteWarning,
    ObsoleteError,
    ObsoleteErrorWithMessage,
    ObsoleteWarningWithMessage,
    OperatorNeedsMatch,
    OpTrueFalseMustResultInBool,
    OverloadRefOut,
    OverrideChangesAccess,
    OverrideChangesReturnType,
    OverrideNotExpected,
    ParamArrayMustBeLast,
    ParamArrayParameterMustBeArrayType,
    ParameterLessAccessibleThanDelegate,
    ParameterLessAccessibleThanIndexedProperty,
    ParameterLessAccessibleThanMethod,
    ParameterLessAccessibleThanOperator,
    ParameterTypeCannotBeTypedReference,
    PartialClassesSpecifyMultipleBases,
    PInvokeHasBody,
    PInvokeWithoutModuleOrImportName,
    PointerInAsOrIs,
    PossibleBadNegCast,
    PropertyCantHaveVoidType,
    PropertyTypeLessAccessibleThanIndexedProperty,
    PropertyTypeLessAccessibleThanProperty,
    PropertyWithNoAccessors,
    RecursiveConstructorCall,
    RedundantBox,
    RedundantNonNull,
    RedundantStream,
    RelatedErrorLocation,
    RelatedErrorModule,
    RelatedWarningLocation,
    RelatedWarningModule,
    ResultIsNotReference,
    ReturnNotAllowed,
    ReturnTypeLessAccessibleThanDelegate,
    ReturnTypeLessAccessibleThanMethod,
    ReturnTypeLessAccessibleThanOperator,
    ReturnValueRequired,
    SealedTypeIsAlreadyInvariant,
    SizeofUnsafe,
    SourceFileNotRead,
    SourceFileTooLarge,
    StaticNotVirtual,
    ThisInBadContext,
    ThisInStaticCode,
    ThisReferenceFromFieldInitializer,
    TooManyArgumentsToAttribute,
    TupleIndexExpected,
    TypeCaseNotFound,
    TypeInBadContext,
    TypeInVariableContext,
    TypeNotAccessible,
    TypeNameRequired,
    TypeParameterNotCompatibleWithConstraint,
    TypeSwitchExpressionMustBeUnion,
    TypeVarCantBeNull,
    UnimplementedAbstractMethod,
    UnimplementedInterfaceMember,
    UnknownCryptoFailure,
    UnreachableCatch,
    UnreferencedLabel,
    UselessComparisonWithIntegerLiteral,
    ValueTypeLayoutCycle,
    ValueTypeIsAlreadyInvariant,
    ValueTypeIsAlreadyNonNull,
    VolatileAndReadonly,
    VolatileByRef,
    VolatileNonWordSize,
    Warning,
    WrongKindOfMember,
    WrongNumberOfArgumentsForDelegate,
    WrongNumberOfIndices,
    WrongReturnTypeForIterator,

    InstanceMemberInAbstractSealedClass,
    ConstructorInAbstractSealedClass,
    DestructorInAbstractSealedClass,
    ConstructsAbstractSealedClass,
    AbstractSealedDerivedFromNonObject,
    AbstractSealedClassInterfaceImpl,
    OperatorInAbstractSealedClass,
    AttributeUsageOnNonAttributeClass,
    BadNamedAttributeArgumentType,
    AbstractAttributeClass,
    UseSwitchInsteadOfAttribute,
    BatchFileNotRead,
    Win32ResourceFileNotRead,
    Win32IconFileNotRead,
    NoSuchFile,
    AutoWin32ResGenFailed,
    InvalidData,
    InvalidWin32ResourceFileContent,

    VisualStudioNotFound,

    QueryNotSupported,
    QueryNoMatch,
    QueryAmbiguousContextName,
    QueryBadAggregate,
    QueryBadAggregateForm,
    QueryBadDeleteTarget,
    QueryBadDifferenceTypes,
    QueryBadInsertList,
    QueryBadIntersectionTypes,
    QueryBadLimit,
    QueryBadLimitForNotPercent,
    QueryBadLimitNotLiteral,
    QueryBadGroupByList,
    QueryBadProjectionList,
    QueryBadOrderItem,
    QueryBadOrderList,
    QueryBadQuantifier,
    QueryBadQuantifiedExpression,
    QueryBadTypeFilter,
    QueryBadUnionTypes,
    QueryBadUpdateList,
    QueryMissingDefaultConstructor,
    QueryNoContext,
    QueryNotScalar,
    QueryNotStream,
    QueryNotAddStream,
    QueryNotDeleteStream,
    QueryNotInsertStream,
    QueryNotUpdateStream,
    QueryNotTransacted,
    QueryNotTransactable,
    QueryNoNestedTransaction,
    QueryProjectThroughTypeUnion,
    QueryIsCyclic,

    
    CannotCoerceNullToNonNullType,
    CoercionToNonNullTypeMightFail,
    ReceiverMightBeNull,
    OnlyStructsAndClassesCanHaveInvariants,
    UpToMustBeSuperType,
    UpToMustBeClass,
    ExpectedLeftParenthesis,
    MustSupportComprehension,
    MustSupportReductionXXXXXXXXXXXX,
    MustResolveToType,
    CheckedExceptionNotInThrowsClause,
    MemberMustBePureForMethodContract,
    RequiresNotAllowedInOverride,
    ContractNotAllowedInExplicitInterfaceImplementation,
    CannotAddThrowsSet,
    CannotWeakenThrowsSet,
    DuplicateThrowsType,
    UncheckedExceptionInThrowsClause,
    RequiresNotAllowedInInterfaceImplementation,
    EnsuresInInterfaceNotInMethod,
    ModelMemberUseNotAllowedInContext,
    MemberMustBePureForInvariant,
    TypeMustSupportIntCoercions,
    CannotInjectContractFromInterface,
    CheckedExceptionInRequiresOtherwiseClause,
    ContractInheritanceRulesViolated,
    ThrowsEnsuresOnConstructor,

    UseDefViolation,
    UseDefViolationOut,   // partially enforced. ( only usedef, no must-assign-before-exit)
    UseDefViolationField, // Not enforced yet.
    UseDefViolationThis,  // Not enforced yet.
    ReturnExpected,
    ParamUnassigned,
    UnreferencedVar,
    UnreferencedVarAssg,
    TemplateTypeRequiresArgs,

    // Nonnull related messages.
    //CannotCoerceNullToNonNullType,
    //CoercionToNonNullTypeMightFail,
    //ReceiverMightBeNull, Dup. just for references
    ReceiverCannotBeNull,

    UseOfNullPointer,
    UseOfPossiblyNullPointer,

    CaseFallThrough,
    TypeOfExprMustBeGuardedClass,

    CannotLoadShadowedAssembly,
    TypeMissingInShadowedAssembly,
    MethodMissingInShadowedAssembly,

    UnassignedThis, 
    NonNullFieldNotInitializedBeforeConstructorCall,
    NonNullFieldNotInitializedAtEndOfDelayedConstructor,
    NonNullFieldNotInitializedByDefaultConstructor,
    ModifiesNotAllowedInOverride,

    GenericError,
    GenericWarning,

    OtherwiseExpressionMustBeNonNull,
    OtherwiseExpressionMustBeType,
    DefaultContructorConstraintNotSatisfied,
    ValConstraintNotSatisfied,
    RefConstraintNotSatisfied,
    UnmanagedConstraintNotSatisfied,
    ConstraintIsAbstractSealedClass,
    FixedNeeded,
    VoidError,
    IllegalPointerType,
    ManagedAddr,
    PointerMustHaveSingleIndex,
    UnreachableCode,
    FixedNotNeeded,
    NegativeStackAllocSize,
    StackallocInCatchFinally,
    InvalidAddressOf,
    BadExplicitCoercionInFixed,
    AssignmentToFixedVariable,
    BadFixedVariableType,
    GeneralComprehensionsNotAllowedInMethodContracts,
    AliasNotFound,
    GlobalSingleTypeNameNotFound,
    TypeArgsNotAllowed,
    NoSuchNestedType,
    TypeAliasUsedAsNamespacePrefix,
    StrictReadonlyNotReadonly,
    StrictReadonlyStatic,
    StrictReadonlyAssignment,
    StrictReadonlyMultipleAssignment,
    BaseNotInitialized,
    BaseMultipleInitialization,
    AlwaysNull,

    WritingPackedObject,
    ExposingExposedObject,
    DontKnowIfCanExposeObject,
    MainCantBeGeneric,

    AccessThroughDelayedReference,
    StoreIntoLessDelayedLocation,
    ActualMustBeDelayed,
    ActualCannotBeDelayed,
    ReceiverMustBeDelayed,
    ReceiverCannotBeDelayed,
    DelayedReferenceByReference,
    DelayedRefParameter,
    DelayedStructConstructor,
    AccessThroughDelayedThisInConstructor,
   
    InvalidUsageOfElementsRepPeer,
    OldExprInPureEnsures,
    IsNewExprInPureEnsures,  // deprecated

    UnnecessaryNonNullCoercion,
    SideEffectsNotAllowedInContracts,

    ShouldCommit,
    ReturnOfDelayedValue,
    ShouldCommitOnAllPaths,
    UnboxDelayedValue,
    ThrowsDelayedValue,
    CannotUseDelayedTypedRef,
    CannotUseDelayedPointer,

    InvalidModifiesClause,
    MemberCannotBeAnnotatedAsPure,

    CannotMatchArglist,
    PureMethodWithOutParamUsedInContract,
    PureMethodCannotHaveRefParam,
    ReadsWithoutPure,
    InconsistentPurityAttributes,
    PureOwnedNotAllowed,

    PointerFreeConstraintNotSatisfied,
    PureMethodCannotHaveModifies,
  }

  public class CommonErrorNode : ErrorNode{
    private static WeakReference resourceManager = new WeakReference(null);
    public CommonErrorNode(Error code, params string[] messageParameters)
      : base((int)code, messageParameters){
    }
    public override string GetMessage(System.Globalization.CultureInfo culture){
      System.Resources.ResourceManager rMgr = (System.Resources.ResourceManager)CommonErrorNode.resourceManager.Target;
      if (rMgr == null)
#if CCINamespace
        CommonErrorNode.resourceManager.Target = rMgr = new System.Resources.ResourceManager("Microsoft.Cci.Compiler.ErrorMessages", typeof(CommonErrorNode).Module.Assembly);
#else
        CommonErrorNode.resourceManager.Target = rMgr = new System.Resources.ResourceManager("System.Compiler.Compiler.ErrorMessages", typeof(CommonErrorNode).Module.Assembly);
#endif
        return this.GetMessage(((Error)this.Code).ToString(), rMgr, culture);
    }
    public override int Severity{
      get{
        switch ((Error)this.Code){
          case Error.AlwaysNull: return 2;
          case Error.BadBox: return 1;
          case Error.BadNonEmptyStream: return 1;
          case Error.BadNonNull: return 1;
          case Error.BadNonNullOnStream: return 1;
          case Error.BadRefCompareLeft: return 2;
          case Error.BadRefCompareRight: return 2;
          case Error.BadStream: return 1;
          case Error.BadStreamOnNonNullStream: return 1;
          case Error.BitwiseOrSignExtend: return 3;
          case Error.CLSNotOnModules: return 1;
          case Error.DuplicateUsedNamespace: return 3;
          case Error.EqualityOpWithoutEquals: return 3;
          case Error.EqualityOpWithoutGetHashCode: return 3;
          case Error.ExpressionIsAlreadyOfThisType: return 4;
          case Error.FamilyInSealed: return 4;
          case Error.IsAlwaysOfType: return 1;
          case Error.IsNeverOfType: return 1;
          case Error.MainCantBeGeneric: return 4;
          case Error.MemberDoesNotHideBaseClassMember: return 4;
          case Error.MemberHidesBaseClassMember: return 2;
          case Error.MultipleTypeImport: return 1;
          case Error.NonObsoleteOverridingObsolete: return 1;
          case Error.ObsoleteWarning: return 1;
          case Error.ObsoleteWarningWithMessage: return 1;
          case Error.RedundantBox: return 1;
          case Error.RedundantNonNull: return 1;
          case Error.RedundantStream: return 1;
          case Error.RelatedErrorLocation: return -1;
          case Error.RelatedErrorModule: return -1;
          case Error.RelatedWarningLocation: return -1;
          case Error.RelatedWarningModule: return -1;
          case Error.SealedTypeIsAlreadyInvariant: return 1;
          case Error.UnreferencedLabel: return 2;
          case Error.UnreachableCode: return 2;
          case Error.UselessComparisonWithIntegerLiteral: return 2;
          case Error.UseSwitchInsteadOfAttribute: return 1;
          case Error.ValueTypeIsAlreadyInvariant: return 1;
          case Error.ValueTypeIsAlreadyNonNull: return 1;
          case Error.VolatileByRef: return 1;

          case Error.CoercionToNonNullTypeMightFail: return 1;
          case Error.ReceiverMightBeNull: return 1;

          case Error.ShouldCommit: return 1;
          case Error.ShouldCommitOnAllPaths: return 1;
          case Error.GenericWarning: return 1;
        }
        return 0; //TODO: switch on code and return > 0 for warnings
      }
    }
  }
  public class ErrorHandler{
    public ErrorNodeList Errors;

    public ErrorHandler(ErrorNodeList errors){
      Debug.Assert(errors != null);
      this.Errors = errors;
    }
    public TrivialHashtable PragmaWarnInformation;
    public void SetPragmaWarnInformation(TrivialHashtable pragmaWarnInformation) {
      this.PragmaWarnInformation = pragmaWarnInformation;
    }
    public void ResetPragmaWarnInformation() {
      this.PragmaWarnInformation = null;
    }
    public virtual string GetAttributeTargetName(AttributeTargets targets) {
      StringBuilder sb = new StringBuilder();
      if ((targets & AttributeTargets.Assembly) != 0 || (targets & AttributeTargets.Module) != 0)
        sb.Append("assembly");
      if ((targets & AttributeTargets.Constructor) != 0) {
        if (sb.Length > 0) sb.Append(", ");
        sb.Append("constructor");
      }
      if ((targets & AttributeTargets.Event) != 0) {
        if (sb.Length > 0) sb.Append(", ");
        sb.Append("event");
      }
      if ((targets & AttributeTargets.Field) != 0) {
        if (sb.Length > 0) sb.Append(", ");
        sb.Append("field");
      }
      if ((targets & AttributeTargets.Method) != 0) {
        if (sb.Length > 0) sb.Append(", ");
        sb.Append("method");
      }
      if ((targets & AttributeTargets.Parameter) != 0){
        if (sb.Length > 0) sb.Append(", ");
        sb.Append("param");
      }
      if ((targets & AttributeTargets.Property) != 0){
        if (sb.Length > 0) sb.Append(", ");
        sb.Append("property");
      }
      if ((targets & AttributeTargets.ReturnValue) != 0){
        if (sb.Length > 0) sb.Append(", ");
        sb.Append("return");
      }
      if ((targets & AttributeTargets.Class) != 0 && (targets & AttributeTargets.Delegate) != 0 &&
        (targets & AttributeTargets.Enum) != 0 && (targets & AttributeTargets.Interface) != 0 && (targets & AttributeTargets.Struct) != 0){
        if (sb.Length > 0) sb.Append(", ");
        sb.Append("type");
      }else{
        if ((targets & AttributeTargets.Class) != 0){
          if (sb.Length > 0) sb.Append(", ");
          sb.Append("class");
        }
        if ((targets & AttributeTargets.Delegate) != 0){
          if (sb.Length > 0) sb.Append(", ");
          sb.Append("delegate");
        }
        if ((targets & AttributeTargets.Enum) != 0){
          if (sb.Length > 0) sb.Append(", ");
          sb.Append("enum");
        }
        if ((targets & AttributeTargets.Interface) != 0){
          if (sb.Length > 0) sb.Append(", ");
          sb.Append("interface");
        }
        if ((targets & AttributeTargets.Struct) != 0){
          if (sb.Length > 0) sb.Append(", ");
          sb.Append("struct");
        }
      }
      return sb.ToString();
    }
    public virtual string GetAttributeTypeName(TypeNode type){
      if (type == null || type.Name == Looker.NotFound) return "";
      string name = type.Name.ToString();
      if (name.EndsWith("Attribute")) return name.Substring(0, name.Length-9);
      return name;
    }
    public virtual string GetDelegateSignature(DelegateNode del){
      if (del == null) return "";
      return this.GetTypeName(del.ReturnType)+" "+this.GetSignatureString(del.FullName, del.Parameters, "(", ")", ", ");
    }
    public virtual string GetIndexerGetterName(Method meth){
      return this.GetSignatureString(this.GetIndexerName(meth), meth.Parameters, "[", "]", ", ");
    }
    public virtual string GetIndexerSetterName(Method meth){
      ParameterList pars = meth.Parameters.Clone();
      pars.Count -= 1;
      return this.GetSignatureString(this.GetIndexerName(meth), pars, "[", "]", ", ");
    }
    public virtual string GetIndexerName(Method meth){
      if (meth == null) return "";
      string decTypeName = this.GetTypeName(meth.DeclaringType);
      if (decTypeName == null || decTypeName.Length == 0 || meth.DeclaringType.IsStructural) return meth.Name.ToString();
      return decTypeName+".this";
    }
    public virtual string GetInstanceMemberSignature(Member mem){
      Field f = mem as Field;
      Property p = mem as Property;
      Method m = mem as Method;
      TypeNode mType = f != null ? f.Type : p != null ? p.Type : m != null ? m.ReturnType : null;
      string astr = this.GetMemberAccessString(mem);
      string mName = mem.Name.ToString();
      if (m != null && mName.StartsWith("get_"))
        mName = mName.Substring(4);
      if (astr == null)
        return this.GetTypeName(mType) + " this." + mName;
      else
        return astr + " " + this.GetTypeName(mType) + " this." + mName;
    }
    public virtual string GetLocalSignature(Field f){
      if (f == null) return "";
      return "(local variable) "+this.GetTypeName(f.Type)+" "+f.Name.ToString(); //REVIEW: localize the (local variable) part?
    }
    public virtual string GetParameterSignature(ParameterField f){
      if (f == null) return "";
      return "(parameter) "+this.GetTypeName(f.Type)+" "+f.Name.ToString(); //REVIEW: localize the (parameter) part?
    }
    public virtual string GetMemberAccessString(Member mem){
      if (mem == null || mem is Namespace) return "";
      if (mem.IsAssembly) return "assembly";
      if (mem.IsFamily) return "family";
      if (mem.IsFamilyAndAssembly) return "family and assembly";
      if (mem.IsFamilyOrAssembly) return "family or assembly";
      if (mem.IsPrivate) return "private";
      if (mem.IsPublic) return "public";
      if (mem.IsCompilerControlled) {
        if (mem is Field && mem.DeclaringType is BlockScope) return "(local variable)";
        if (mem is ParameterField) return "(parameter)";
        return null;
      }
      return "";
    }
    public virtual string GetMemberName(Member mem){
      if (mem == null) return "";
      TypeNode t = mem as TypeNode;
      if (t != null) return this.GetTypeName(t);
      Namespace ns = mem as Namespace;
      if (ns != null && ns.FullName != null) return ns.FullName;
      string decTypeName = this.GetTypeName(mem.DeclaringType);
      if (decTypeName == null || decTypeName.Length == 0 || mem.DeclaringType.IsStructural) return mem.Name.ToString();
      return decTypeName+"."+mem.Name;
    }
    public virtual string GetUnqualifiedMemberName(Member mem){
      if (mem == null) return "";
      TypeNode t = mem as TypeNode;
      if (t != null) return this.GetUnqualifiedTypeName(t);
      Method meth = mem as Method;
      if (meth != null) return meth.GetUnmangledNameWithTypeParameters(true);
      return mem.Name.ToString();
    }
    public virtual string GetMemberSignature(Member mem){
      return this.GetMemberSignature(mem, false);
    }
    public virtual string GetMemberSignature(Member mem, bool withFullInformation){
      if (mem == null) return "";
      string name = null;
      TypeNode type = null;
      switch (mem.NodeType){
        case NodeType.InstanceInitializer:
        case NodeType.Method:
          name = this.GetMethodSignature((Method)mem, true, withFullInformation);
          type = ((Method)mem).ReturnType;
          break;
        case NodeType.Property:
          ParameterList pars = ((Property)mem).Parameters;
          if (pars == null || pars.Count == 0)
            name = this.GetMemberName(mem);
          else
            name = this.GetSignatureString(this.GetMemberName(mem), pars, "[", "]", ", ", withFullInformation);
          type = ((Property)mem).Type;
          break;
        case NodeType.Field:
          type = ((Field)mem).Type;
          goto default;
        default:
          if (withFullInformation){
            TypeAlias talias = mem as TypeAlias;
            if (talias != null)
              return this.GetMemberSignature(talias.AliasedType, true);
            ITypeParameter itp = mem as ITypeParameter;
            if (itp != null){
              TypeNode dt = itp.DeclaringMember as TypeNode;
              if (dt != null) return dt.GetFullUnmangledNameWithTypeParameters();
              Method dm = itp.DeclaringMember as Method;
              if (dm != null) return dm.GetFullUnmangledNameWithTypeParameters(true);
            }
          }
          name = this.GetMemberName(mem);
          break;
      }
      if (!withFullInformation) return name;
      string kind = this.GetMemberKindForSignature(mem);
      StringBuilder result = new StringBuilder();
      string astr = this.GetMemberAccessString(mem);
      if (astr != null){
        result.Append(astr);
        result.Append(' ');
      }
      result.Append(kind);
      if (mem.IsStatic && !(mem is TypeNode))
        result.Append("static ");
      if (type != null && !(mem is InstanceInitializer)){
        result.Append(this.GetTypeName(type));
        result.Append(' ');
      }
      result.Append(name);
      return result.ToString();
    }
    public virtual string GetUnqualifiedMemberSignature(Member mem){
      if (mem == null) return "";
      string name = null;
      switch (mem.NodeType){
        case NodeType.InstanceInitializer:
        case NodeType.Method:
          name = this.GetUnqualifiedMethodSignature((Method)mem, true);
          break;
        case NodeType.Property:
          ParameterList pars = ((Property)mem).Parameters;
          if (pars == null || pars.Count == 0)
            name = this.GetUnqualifiedMemberName(mem);
          else
            name = this.GetSignatureString(this.GetUnqualifiedMemberName(mem), pars, "[", "]", ", ");
          break;
        default:
          name = this.GetUnqualifiedMemberName(mem);
          break;
      }
      return name;
    }
    public virtual string GetMemberKindForSignature(Member mem){
      if (mem == null) return "";
      switch (mem.NodeType){
        case NodeType.Class: return "class ";
        case NodeType.DelegateNode: return "delegate ";
        case NodeType.EnumNode: return "enum ";
        case NodeType.Event: return "event ";
        case NodeType.InstanceInitializer: return this.GetMemberKindForSignature(mem.DeclaringType) + "constructor ";
        case NodeType.Interface: return "interface ";
        case NodeType.Namespace: return "namespace ";
        case NodeType.Struct: return "struct ";
      }
      return "";
    }
    public virtual string GetMethodSignature(Method method){
      return this.GetMethodSignature(method, false);
    }
    public virtual string GetMethodSignature(Method method, bool noAccessor){
      return this.GetMethodSignature(method, noAccessor, false);
    }
    public virtual string GetMethodSignature(Method method, bool noAccessor, bool withFullInformation){
      if (method == null) return "";
      if (method.IsSpecialName){
        string methName = method.Name.ToString();
        if (methName.StartsWith("get_")){
          if ((method.Parameters != null && method.Parameters.Count > 0))
            return this.GetIndexerGetterName(method)+(noAccessor?"":".get");
          methName = methName.Substring(4)+(noAccessor?"":".get");
        }else if (methName.StartsWith("set_")){
          if (method.Parameters != null && method.Parameters.Count > 1)
            return this.GetIndexerSetterName(method)+(noAccessor?"":".set");
          methName = methName.Substring(4)+(noAccessor?"":".set");
        }else if (methName.StartsWith("add_") && method.Parameters != null && method.Parameters.Count == 1)
          methName = methName.Substring(4)+(noAccessor?"":".add");
        else if (methName.StartsWith("remove_") && method.Parameters != null && method.Parameters.Count == 1)
          methName = methName.Substring(7)+(noAccessor?"":".remove");
        else
          goto nonAccessorCase;
        string decTypeName = this.GetTypeName(method.DeclaringType);
        if (decTypeName == null || decTypeName.Length == 0 || method.DeclaringType.IsStructural) return methName;
        return decTypeName+"."+methName;
      }
    nonAccessorCase:
      return this.GetSignatureString(this.GetMemberName(method), method.Parameters, "(", ")", ", ");
    }
    public virtual string GetUnqualifiedMethodSignature(Method method, bool noAccessor){
      if (method == null) return "";
      if (method.IsSpecialName){
        string methName = method.Name.ToString();
        if (methName.StartsWith("get_")) return null;
        if (methName.StartsWith("set_")) return null;
        if (methName.StartsWith("add_")) return null;
        if (methName.StartsWith("remove_")) return null;
      }
      return this.GetSignatureString(this.GetUnqualifiedMemberName(method), method.Parameters, "(", ")", ", ", true);
    }
    public virtual string GetParameterTypeName(Parameter parameter){
      if (parameter == null) return "";
      Reference r = parameter.Type as Reference;
      if (r != null){
        if ((parameter.Flags & ParameterFlags.Out) != 0)
          return "out "+this.GetTypeName(r.ElementType);
        else
          return "ref "+this.GetTypeName(r.ElementType);
      }else if (parameter.GetParamArrayElementType() != null)
        return "params "+this.GetTypeName(parameter.Type);
      else
        return this.GetTypeName(parameter.Type);
    }
    public virtual string GetSignatureString(string name, ParameterList parameters, string startPars, string endPars, string parSep){
      return this.GetSignatureString(name, parameters, startPars, endPars, parSep, false);
    }
    public virtual string GetSignatureString(string name, ParameterList parameters, string startPars, string endPars, string parSep, bool addParameterNames){
      StringBuilder sb = new StringBuilder(256);
      sb.Append(name);
      sb.Append(startPars);
      for (int i = 0, n = parameters == null ? 0 : parameters.Count; i < n; i++){
        Parameter p = parameters[i];
        if (p != null){
          if (p.Type != null) sb.Append(this.GetParameterTypeName(p));
        }
        if (addParameterNames && p.Name != null){
          sb.Append(' ');
          sb.Append(p.Name.ToString());
        }
        if (i < n-1) sb.Append(parSep);
      }
      sb.Append(endPars);
      return sb.ToString();
    }
    public virtual string GetTypeName(TypeNode type){
      if (type == null || type.Name == Looker.NotFound || type.Name == null) return "";
      return type.EffectiveTypeNode.FullName;
    }
    public virtual string GetUnqualifiedTypeName(TypeNode type){
      if (type == null || type.Name == Looker.NotFound || type.Name == null) return "";
      return type.EffectiveTypeNode.Name.ToString();
    }
    public virtual void HandleNonOverrideAndNonHide(Method meth, Member bmem, Method bmeth){
      Error e = Error.MemberHidesBaseClassMember;
      string bSig = null;
      if (bmeth != null){
        bSig = this.GetMethodSignature(bmeth);
        if (bmeth.IsVirtual && !(meth.DeclaringType is Interface))
          e = Error.MemberHidesBaseClassOverridableMember;
      }else
        bSig = this.GetMemberSignature(bmem);
      this.HandleError(meth.Name, e, this.GetMethodSignature(meth), bSig);
      this.HandleRelatedWarning(bmem);
      meth.HidesBaseClassMember = true;
    }
    public virtual void HandleOverrideOfObjectFinalize(Method method){
      //Do nothing. This is an extensibility point.
    }
    public virtual void HandleDirectCallOfFinalize(MethodCall call){
      //Do nothing, this is an extensibility point
    }
    public virtual string GetMemberKind(Member offendingMember){
      string offendingKind = "";
      if (offendingMember is Field) offendingKind = "field";
      else if (offendingMember is Event) offendingKind = "event";
      else if (offendingMember is Property) offendingKind = "property";
      else if (offendingMember is DelegateNode) offendingKind = "delegate";
      else if (offendingMember is Method) offendingKind = "method";
      else if (offendingMember is TypeNode) offendingKind = "type";
      return offendingKind;
    }
    public virtual void HandleNonMethodWhereMethodExpectedError(Node offendingNode, Member offendingMember){
      if (offendingMember == null) return;
      string typeName = this.GetTypeName(offendingMember.DeclaringType);
      string memberName = typeName + "." + offendingMember.Name;
      if (offendingMember is Property){
        this.HandleError(offendingNode, Error.NoGetter, memberName);
        return;
      }
      this.HandleError(offendingNode, Error.WrongKindOfMember, memberName, this.GetMemberKind(offendingMember), "method");
    }
    public virtual void HandleError(Node offendingNode, Error error, params string[] messageParameters){
      if (offendingNode == null || offendingNode.IsErroneous) return;
      if (offendingNode is Literal && offendingNode.SourceContext.Document == null) return;
      CommonErrorNode enode = new CommonErrorNode(error, messageParameters);
      enode.SourceContext = offendingNode.SourceContext;
      if (enode.Severity == 0) offendingNode.IsErroneous = true;
      this.Errors.Add(enode);
    }
    public virtual void HandleRelatedError(Member relatedMember){
      if (relatedMember == null || relatedMember.Name == null) return;
      if (relatedMember.Name.SourceContext.Document != null)
        this.HandleError(relatedMember.Name, Error.RelatedErrorLocation);
      else if (relatedMember is InstanceInitializer && relatedMember.SourceContext.Document != null)
        this.HandleError(relatedMember, Error.RelatedErrorLocation);
      else if (relatedMember.DeclaringType != null && relatedMember.DeclaringType.DeclaringModule != null && relatedMember.DeclaringType.DeclaringModule.Location != null)
        this.HandleError(relatedMember.Name, Error.RelatedErrorModule, relatedMember.DeclaringType.DeclaringModule.Location, relatedMember.DeclaringType.FullName);
      else if (relatedMember is TypeNode && ((TypeNode)relatedMember).DeclaringModule != null && ((TypeNode)relatedMember).DeclaringModule.Location != null)
        this.HandleError(relatedMember.Name, Error.RelatedErrorModule, ((TypeNode)relatedMember).DeclaringModule.Location, ((TypeNode)relatedMember).FullName);
    }  
    public virtual void HandleRelatedWarning(Member relatedMember){
      if (relatedMember == null || relatedMember.Name == null) return;
      if (relatedMember.Name.SourceContext.Document != null)
        this.HandleError(relatedMember.Name, Error.RelatedWarningLocation);
      else if (relatedMember is InstanceInitializer && relatedMember.SourceContext.Document != null)
        this.HandleError(relatedMember, Error.RelatedWarningLocation);
      else if (relatedMember.DeclaringType != null && relatedMember.DeclaringType.DeclaringModule != null && relatedMember.DeclaringType.DeclaringModule.Location != null)
        this.HandleError(relatedMember.Name, Error.RelatedWarningModule, relatedMember.DeclaringType.DeclaringModule.Location, relatedMember.DeclaringType.FullName);
      else if (relatedMember is TypeNode && ((TypeNode)relatedMember).DeclaringModule != null && ((TypeNode)relatedMember).DeclaringModule.Location != null)
        this.HandleError(relatedMember.Name, Error.RelatedWarningModule, ((TypeNode)relatedMember).DeclaringModule.Location, ((TypeNode)relatedMember).FullName);
    }
  }
}