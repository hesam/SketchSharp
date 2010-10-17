//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
//
#if CCINamespace
using Microsoft.Cci;
using Cci = Microsoft.Cci;
using SysError = Microsoft.Cci.Error;
#else
using System.Compiler;
using Cci = System.Compiler;
using SysError = System.Compiler.Error;
#endif
using System;
using System.Collections;
using System.Text;
using System.Globalization;

namespace Microsoft.SpecSharp{
  public sealed class PragmaInfo {
    internal readonly int LineNum;
    internal readonly bool Disable;
    internal readonly PragmaInfo Next;
    internal PragmaInfo(
      int lineNum,
      bool disable,
      PragmaInfo next
    ) {
      this.LineNum = lineNum;
      this.Disable = disable;
      this.Next = next;
    }
  }

  internal enum Error {
    None = 0,
    AbstractAndExtern = 180,
    AbstractAndSealed = 502,
    AbstractAttributeClass = 653,
    AbstractBaseCall = 205,
    AbstractEventInitializer = 74,
    AbstractHasBody = 500,
    AbstractInConcreteClass = 513,
    AbstractNotVirtual = 503,
    AbstractSealedStatic = 418,
    AddModuleAssembly = 1542,
    AddOrRemoveExpected = 1055,
    AddRemoveMustHaveBody = 73,
    AliasNotFound = 432,
    AlwaysNull = 458,
    AmbigBinaryOps = 34,
    AmbigCall = 121,
    AmbigContext = 104,
    AmbigQM = 172,
    AmbiguousAttribute = 1614,
    AnonMethNotAllowed = 1706,
    AnonMethToNonDel = 1660,
    ArrayElementCantBeRefAny = 611,
    ArrayInitInBadPlace = 623,
    ArrayInitToNonArrayType = 622,
    ArrayOfStaticClass = 719,
    AsMustHaveReferenceType = 77,
    AsWithTypeVar = 413,
    AssgLvalueExpected = 131,
    AssgReadonly = 191,
    AssgReadonlyLocal = 1604,
    AssgReadonlyLocalCause = 1656,
    AssgReadonlyProp = 200,
    AssgReadonlyStatic = 198,
    AttributeLocationOnBadDeclaration = 657,
    AttributeOnBadSymbolType = 592,
    AttributeUsageOnNonAttributeClass = 641,
    AutoResGen = 1567,
    BadAccess = 122,
    BadArgCount = 1501,
    BadArgExtraRef = 1615,
    BadArgumentToNameAttribute = 633,
    BadArity2 = 305,
    BadArraySyntax = 1552,
    BadAttributeParam = 182,
    BadBaseType = 1521,
    BadBinaryOps = 19,
    BadBinaryOperatorSignature = 563,
    BadBoolOp = 217,
    BadCastInFixed = 254,
    BadCodePage = 2021,
    BadDelArgCount = 1593,
    BadDelegateLeave = 1632,
    BadDirectivePlacement = 1040,
    BadEmbeddedStmt = 1023,
    BadEmptyThrow = 156,
    BadEmptyThrowInFinally = 724,
    BadEventUsage = 70,
    BadEventUsageNoField = 79,
    BadExceptionType = 155,
    BadFinallyLeave = 157,
    BadFixedInitType = 209,
    BadForeachDecl = 230,
    BadGetEnumerator = 202,
    BadIncDecSignature = 559,
    BadIndexCount = 22,
    BadIteratorReturn = 1624,
    BadModifierLocation = 1585,
    BadModifiersOnNamespace = 1671,
    BadNamedAttributeArgument = 617,
    BadNamedAttributeArgumentType = 655,
    BadNewExpr = 1526,
    BadOperatorSyntax = 1553,
    BadOperatorSyntax2 = 1554,
    BadProtectedAccess = 1540,
    BadRefCompareLeft = 252,
    BadRefCompareRight = 253,
    BadSKknown = 118,
    BadSKunknown = 119,
    BadStackAllocExpr = 1575,
    BadTokenInType = 1518,
    BadTypeReference = 572,
    BadVarDecl = 1528,
    BadVisBaseClass = 60,
    BadVisBaseInterface = 61,
    BadVisDelegateParam = 59,
    BadVisDelegateReturn = 58,
    BadVisFieldType = 52,
    BadVisIndexerParam = 55,
    BadVisIndexerReturn = 54,
    BadVisOpParam = 57,
    BadVisOpReturn = 56,
    BadVisParamType = 51,
    BadVisPropertyType = 53,
    BadVisReturnType = 50,
    BadUnaryOp = 23,
    BadUnaryOperatorSignature = 562,
    BadUseOfMethod = 654,
    BadWin32Res = 1583,
    BaseIllegal = 175,
    BaseInBadContext = 1512,
    BadIndexLHS = 21,
    BaseInStaticMeth = 1511,
    BatchFileNotRead = 2003,
    BitwiseOrSignExtend = 675,
    CallingBaseFinalizeDeprecated = 250,
    CallingFinalizeDeprecated = 245,
    CannotMarkOverrideMethodNewOrVirtual = 113,
    CantCallSpecialMethod = 571,
    CantChangeAccessOnOverride = 507,
    CantChangeReturnTypeOnOverride = 508,
    CantConvAnonMethParams = 1661,
    CantConvAnonMethNoParams = 1688,
    CantConvAnonMethReturns = 1662,
    CantDeriveFromSealedClass = 509,
    CantInferMethTypeArgs = 411,
    CantOverrideAccessor = 560,
    CantOverrideNonEvent = 72,
    CantOverrideNonFunction = 505,
    CantOverrideNonProperty = 544,
    CantOverrideNonVirtual = 506,
    CantOverrideSealed = 239,
    CantOverrideSpecialMethod = 561,
    CaseFallThrough = 163,
    CheckedOverflow = 220,
    CircConstValue = 110,
    CircularBase = 146,
    ClassDoesntImplementInterface = 540,
    CloseUnimplementedInterfaceMember = 536,
    CLSNotOnModules = 3012,
    ColColWithTypeAlias = 431,
    ComImportWithoutUuidAttribute = 596,
    ConcreteMissingBody = 501,
    ConditionalMustReturnVoid = 578,
    ConditionalOnInterfaceMethod = 582,
    ConditionalOnOverride = 243,
    ConditionalOnSpecialMethod = 577,
    ConflictAliasAndMember = 576,
    ConflictingProtectionModifier = 107,
    ConstantExpected = 150,
    ConstOutOfRange = 31,
    ConstOutOfRangeChecked = 221,
    ConstraintIsStaticClass = 717,
    ConstructorInStaticClass = 710,
    ConstValueRequired = 145,
    ConversionNotInvolvingContainedType = 556,
    ConversionWithBase = 553,
    ConversionWithDerived = 554,
    ConversionWithInterface = 552,
    ConvertToStaticClass = 716,
    CryptoFailed = 1548,
    CStyleArray = 650,
    CustomAttributeError = 647,
    CycleInInterfaceInheritance = 529,
    DebugInitFile = 42,
    DeprecatedSymbol = 612,
    DeprecatedSymbolError = 613,
    DeprecatedSymbolStr = 618,
    DeprecatedSymbolStrError = 619,
    DeriveFromEnumOrValueType = 644,
    DestructorInStaticClass = 711,
    DllImportOnInvalidMethod = 601,
    DontUseInvoke = 1533,
    DottedTypeNameNotFoundInAgg = 426,
    DuplicateAccessor = 1007,
    DuplicateAlias = 1537,
    DuplicateAttribute = 579,
    DuplicateCaseLabel = 152,
    DuplicateConversionInClass = 557,
    DuplicateInterfaceInBaseList = 528,
    DuplicateLabel = 140,
    DuplicateModifier = 1004,
    DuplicateNamedAttributeArgument = 643,
    DuplicateNameInClass = 102,
    DuplicateNameInNS = 101,
    DuplicateParamName = 100,
    DuplicateResponseFile = 2014,
    DuplicateUsing = 105,
    EmptyCharConst = 1011,
    EmptySwitch = 1522,
    EndifDirectiveExpected = 1027,
    EndOfPPLineExpected = 1025,
    EndRegionDirectiveExpected = 1038,
    EnumeratorOverflow = 543,
    EOFExpected = 1022,
    EqualityOpWithoutEquals = 660,
    EqualityOpWithoutGetHashCode = 661,
    ErrorDirective = 1029,
    EventNeedsBothAccessors = 65,
    EventNotDelegate = 66,
    EventPropertyInInterface = 69,
    ExpectedEndTry = 1524,
    ExpectedIdentifier = 1001,
    ExpectedLeftBrace = 1514,
    ExpectedRightBrace = 1513,
    ExpectedRightParenthesis = 1026,
    ExpectedSemicolon = 1002,
    ExplicitEventFieldImpl = 71,
    ExplicitInterfaceImplementationInNonClassOrStruct = 541,
    ExplicitInterfaceImplementationNotInterface = 538,
    ExplicitMethodImplAccessor = 683,
    ExplicitParamArray = 674,
    ExplicitPropertyAddingAccessor = 550,
    ExplicitPropertyMissingAccessor = 551,
    ExternAfterElements = 439,
    ExternHasBody = 179,
    ExternMethodNoImplementation = 626,
    FeatureNYI2 = 189,
    FieldInitRefNonstatic = 236,
    FieldInitializerInStruct = 573,
    FixedMustInit = 210,
    FixedNeeded = 212,
    FixedNotNeeded = 213,
    FloatOverflow = 594,
    ForEachMissingMember = 1579,
    GenericArgIsStaticClass = 718,
    GenericConstraintNotSatisfied = 309,
    GetOrSetExpected = 1014,
    GlobalSingleTypeNameNotFound = 400,
    HasNoTypeVars = 308,
    HidingAbstractMethod = 533,
    IdentityConversion = 555,
    IllegalEscape = 1009,
    IllegalPointerType=1005,
    IllegalQualifiedNamespace = 134,
    IllegalStatement = 201,
    IllegalUnsafe = 227,
    ImportNonAssembly = 1509,
    InconsistantIndexerNames = 668,
    IdentifierExpectedKW = 1041,
    IndexerInStaticClass = 720,
    IndexerNeedsParam = 1551,
    IndexerWithRefParam = 631,
    InExpected = 1515,
    InstanceMemberInStaticClass = 708,
    InstantiatingStaticClass = 712,
    IntDivByZero = 20,
    IntegralTypeExpected = 1008,
    IntegralTypeValueExpected = 151,
    InterfaceEventInitializer = 68,
    InterfaceImplementedByConditional = 629,
    InterfaceMemberHasBody = 531,
    InterfaceMemberNotFound = 539,
    InterfacesCantContainOperators = 567,
    InterfacesCannotContainConstructors = 526,
    InterfacesCannotContainFields = 525,
    InterfacesCannotContainTypes = 524,
    InternalCompilerError = 1,
    IntOverflow = 1021,
    InvalidArglistConstructContext = 190,
    InvalidArray = 178,
    InvalidAttributeArgument = 591,
    InvalidAttributeLocation = 658,
    InvalidCall = 123,
    InvalidGotoCase = 153,
    InvalidExprTerm = 1525,
    InvalidLineNumber = 1576,
    InvalidMainSig = 28,
    InvalidMemberDecl = 1519,
    InvalidModifier = 106,
    InvalidAddrOp = 211,
    InvalidPreprocExpr = 1517,
    InvalidQM = 173,
    IsAlwaysFalse = 184,
    IsAlwaysTrue = 183,
    IsBinaryFile = 2015,
    LabelNotFound = 159,
    LiteralDoubleCast = 664,
    LocalDuplicate = 128,
    LocalShadowsOuterDeclaration = 136,
    LockNeedsReference = 185,
    LowercaseEllSuffix = 78,
    MainCantBeGeneric = 402,
    ManagedAddr = 208,
    MemberAlreadyExists = 111,
    MemberNameSameAsType = 542,
    MemberNeedsType = 1520,
    MethodArgCantBeRefAny = 1601,
    MethodNameExpected = 149,
    MethodReturnCantBeRefAny = 1564,
    MissingArraySize = 1586,
    MissingPartial = 260,
    MissingPPFile = 1578,
    MissingStructOffset = 625,
    MultipleEntryPoints = 17,
    MultipleTypeDefs = 1595,
    MultiTypeInDeclaration = 1044,
    MustHaveOpTF = 218,
    NamedArgumentExpected = 1016,
    NamespaceUnexpected = 116,
    NameAttributeOnOverride = 609,
    NameNotInContext = 103,
    NegativeArraySize = 248,
    NegativeStackAllocSize = 247,
    NewBoundMustBeLast = 401,
    NewConstraintNotSatisfied=310,
    NewlineInConst = 1010,
    NewNotRequired = 109,
    NewOnNamespaceElement = 1530,
    NewOrOverrideExpected = 114,
    NewRequired = 108,
    NewVirtualInSealed = 549,
    NoArglistInIndexers = 237,
    NoArglistInDelegates = 235,
    NoBreakOrCont = 139,
    NoConstructors = 143,
    NoCommentEnd = 1035,
    NoDefaultArgs = 241,
    NoEntryPoint = 5001,
    NoExplicitBuiltinConv = 39,
    NoExplicitConversion = 30,
    NoGetToOverride = 545,
    NoImplicitConversion = 29,
    NoImplicitConvCast = 266,
    NoModifiersOnAccessor = 1609,
    NoNewAbstract = 144,
    NoSetToOverride = 546,
    NoSources = 2008,
    NoSuchFile = 2005,
    NoSuchMember = 117,
    NoSuchOperator = 187,
    NotAnAttributeClass = 616,
    NotConstantExpression = 133,
    NoVoidHere = 1547,
    NoVoidParameter = 1536,
    NonInterfaceInInterfaceList = 527,
    NonObsoleteOverridingObsolete = 672,
    NullNotValid = 186,
    ObjectProhibited = 176,
    ObjectRequired = 120,
    OnlyClassesCanContainDestructors = 575,
    OperatorCantReturnVoid = 590,
    OperatorInStaticClass = 715,
    OperatorNeedsMatch = 216,
    OperatorsMustBeStatic = 558,
    OpTFRetType = 215,
    OutputWriteFailed = 16,
    OverloadRefOut = 663,
    OverrideFinalizeDeprecated = 249,
    OverrideNotExpected = 115,
    OvlBinaryOperatorExpected = 1020,
    OvlUnaryOperatorExpected = 1019,
    ParameterIsStaticClass = 721,
    ParamsCantBeRefOut = 1611,
    ParamsMustBeArray = 225,
    ParamsOrVarargsMustBeLast = 231,
    ParamUnassigned = 0177, 
    PartialMisplaced = 267,
    PartialModifierConflict = 262,
    PartialMultipleBases = 263,
    PartialTypeKindConflict = 261,
    PointerInAsOrIs = 244,
    PtrIndexSingle = 196,
    PossibleBadNegCast = 75,
    PossibleMistakenNullStatement = 642,
    PPDefFollowsToken = 1032,
    PPDirectiveExpected = 1024,
    PrivateOrProtectedNamespaceElement = 1527,
    PropertyCantHaveVoidType = 547,
    PropertyLacksGet = 154,
    PropertyWithNoAccessors = 548,
    ProtectedInSealed = 628,
    ProtectedInStruct = 666,
    PtrExpected = 193,
    RecursiveConstructorCall = 516,
    RefConstraintNotSatisfied = 452,
    RefLvalueExpected = 1510,
    RefReadonly = 192,
    RefReadonlyLocal = 1605,
    RefReadonlyLocalCause = 1657,
    RefReadonlyProperty = 206,
    RefReadonlyStatic = 199,
    RefValBoundMustBeFirst = 449,
    RelatedErrorLocation = 10002,
    RelatedErrorModule = 10003,
    RelatedWarningLocation = 10004,
    RelatedWarningModule = 10005,
    RetNoObjectRequired = 127,
    RetObjectRequired = 126,
    ReturnExpected = 0161,
    ReturnNotLValue = 1612,
    ReturnTypeIsStaticClass = 722,
    SealedNonOverride = 238,
    SealedStaticClass = 441,
    SingleTypeNameNotFound = 246,
    SizeofUnsafe = 233,
    SourceFileNotRead = 2001,
    StackallocInCatchFinally = 255,
    StaticBaseClass = 709,
    StaticClassInterfaceImpl = 714,
    StaticConstant = 504,
    StaticConstParam = 132,
    StaticConstructorWithAccessModifiers = 515,
    StaticConstructorWithExplicitConstructorCall = 514,
    StaticDerivedFromNonObject = 713,
    StaticNotVirtual = 112,
    StmtNotInCase = 1523,
    StructLayoutCycle = 523,
    StructOffsetOnBadStruct = 636,
    StructOffsetOnBadField = 637,
    StructsCantContainDefaultContructor = 568,
    StructWithBaseConstructorCall = 522,
    SyntaxError = 1003,
    ThisInBadContext = 27,
    ThisInStaticMeth = 26,
    ThisOrBaseExpected = 1018,
    TooManyArgumentsToAttribute = 580,
    TooManyCatches = 1017,
    TooManyCharsInConst = 1012,
    TypeArgsNotAllowed = 307,
    TypeExpected = 1031,
    TypeNameNotFound = 234,
    TypeVarCantBeNull = 403,
    UnassignedThis = 171,
    UnexpectedDirective = 1028,
    UnimplementedAbstractMethod = 534,
    UnimplementedInterfaceMember = 535,
    UnknownOption = 2007,
    UnreachableCatch = 160, 
    UnreachableCode = 162,
    UnreferencedLabel = 164,
    UnreferencedVar = 0168,
    UnreferencedVarAssg = 219,
    UnsafeNeeded = 214,
    UseDefViolation = 165,
    UseDefViolationField = 170, // Not enforced yet.
    UseDefViolationOut = 269,   // partially enforced. ( only usedef, no must-assign-before-exit)
    UseDefViolationThis = 188,  // Not enforced yet.
    UseSwitchInsteadOfAttribute = 1699,
    UsingAfterElements = 1529,
    VacuousIntegralComp = 652,
    ValueCantBeNull = 37,
    ValConstraintNotSatisfied = 453,
    VarDeclIsStaticClass = 723,
    VirtualPrivate = 621,
    VoidError = 242,
    VolatileAndReadonly = 678,
    VolatileByRef = 420,
    VolatileStruct = 677,
    WarningDirective = 1030,
    Win32ResourceFileNotRead = 2002,
    WrongNameForDestructor = 574,
    WrongNestedThis = 38,
    WrongParsForBinOp = 1534,
    WrongParsForUnaryOp = 1535,

    InvalidAssemblyName = 1700,
    UnifyReferenceMajMin = 1701,
    UnifyReferenceBldRev = 1702,
    DuplicateImport = 1703,
    DuplicateImportSimple = 1704,
    AssemblyMatchBadVersion = 1705,
    DelegateNewMethBind = 1707,
    FixedNeedsLvalue = 1708,
    EmptyFileName = 1709,
    DuplicateTypeParamTag = 1710,
    UnmatchedTypeParamTag = 1711,
    MissingTypeParamTag = 1712,
    TypeNameBuilderError = 1713,
    ImportBadBase = 1714,
    CantChangeTypeOnOverride = 1715,
    DoNotUseFixedBufferAttr = 1716,
    AssignmentToSelf = 1717,
    ComparisonToSelf = 1718,
    CantOpenWin32Res = 1719,
    DotOnDefault = 1720,
    NoMultipleInheritance = 1721,
    BaseClassMustBeFirst = 1722,
    BadXMLRefTypeVar = 1723,
    InvalidDefaultCharSetValue = 1724,
    FriendAssemblyBadArgs = 1725,
    FriendAssemblySNReq = 1726,

    BadEscapeSequence = 4000,
    BadHexDigit,
    BadDecimalDigit,
    UnexpectedToken,

    AmbiguousAssignment = 2500,
    CannotMarkAbstractPropertyVirtual,
    CannotMarkOverridePropertyNewOrVitual,
    ClosingTagMismatch,
    DebugContentModel,
    DummyXXXXXXXXX,
    DuplicateAttributeSpecified,
    EntityOverflow,
    ExpectedDoubleQuote,
    ExpectedElement,
    ExpectedExpression,
    ExpectedSingleQuote,
    FactorySignatureHasBody,
    ForwardReferenceToLocal,
    IncompleteContentExpecting,
    InvalidAxisSpecifier,
    InvalidContentExpecting,
    InvalidElementContent,
    InvalidElementContentNone,
    InvalidElementInEmpty,
    InvalidElementInTextOnly,
    InvalidPathExpression,
    InvalidTextInElement,
    InvalidTextInElementExpecting,    
    InvalidWhitespaceInEmpty,
    LocalConstDuplicate,
    NoDefaultConstructor,
    NonDeterministic,
    NonDeterministicAny,
    NonDeterministicAssign,
    NoSuchAttribute,
    NoSuchElement,
    RequiredAttribute,
    UnknownEntity,
    UnescapedSingleQuote,

    Error,
    FatalError,
    Warning,
    
    InvalidTextWithReason, // todo: merge this in above (causes error numbers to change)
    CannotInstantiateTypeConverter, 
    DuplicateMemberInLiteral,
    AssignmentExpressionInTuple,
    TupleIndexExpected,
    SealedTypeIsAlreadyInvariant,
    ValueTypeIsAlreadyInvariant,
    ValueTypeIsAlreadyNonNull,
    RedundantBox,
    BadBox,
    BadStream,
    RedundantStream,
    BadNonNull,
    BadNonNullOnStream,
    RedundantNonNull,
    BadNonEmptyStream,
    BadStreamOnNonNullStream,
    InvalidElementExpression,
    ContentModelNotSupported,
    QueryNotSupported,
    QueryNoMatch,
    QueryAmbiguousContextName,
    QueryBadAggregate,
    QueryBadAggregateForm,
    QueryBadGroupByList,
    QueryBadOrderList,
    QueryBadProjectionList,
    QueryBadQuantifier,
    QueryBadQuantifiedExpression,
    QueryBadDifferenceTypes,
    QueryBadInsertList,
    QueryBadIntersectionTypes,
    QueryBadLimit,
    QueryBadOrderItem,
    QueryBadUnionTypes,
    QueryBadUpdateList,
    QueryBadTypeFilter,
    QueryMissingDefaultConstructor,
    QueryNoContext,
    QueryNotScalar,
    QueryNotStream,
    QueryNotAddStream,
    QueryNotDeleteStream,
    QueryNotInsertStream,
    QueryNotUpdateStream,
    QueryProjectThroughTypeUnion,
    QueryIsCyclic,
    TypeDeclarationInsideElementGroup,
    ConstDeclarationInsideElementGroup,
    AttributeInsideElementGroup,
    QueryNotTransacted,
    QueryNotTransactable,
    QueryNoNestedTransaction,
    FieldInitializerInElementGroup,
    CannotYieldFromCatchClause,
    QueryBadLimitForNotPercent,
    QueryBadLimitNotLiteral,
    BadTupleIndex,
    CannotYieldFromTryBlock,
    BadTypeInferenceToVoid,
    WrongReturnTypeForIterator,
    AttributeInElementGroup,
    AmbiguousLiteralExpression,
    ExpectedRightBracket,
    NotAType,
    AssemblyKeyFileMissing,
    InvalidData,
    Win32IconFileNotRead,
    AbstractMethodTemplate,
    AbstractInterfaceMethod,

    ExpectedLeftParenthesis,
    CheckedExceptionNotInThrowsClause,
    ThrowsEnsuresOnConstructor,

    ExpressionIsAlreadyOfThisType,

    // MB -- 08/11/2004 added because they had been added to the Framework and not here
    CannotCoerceNullToNonNullType,
    CoercionToNonNullTypeMightFail,
    ReceiverMightBeNull,
    OnlyStructsAndClassesCanHaveInvariants,
    UpToMustBeSuperType,
    UpToMustBeClass,
//    ExpectedLeftParenthesis,
    MustSupportComprehension,
    MustSupportReductionXXXXXXXXXXXX,
    MustResolveToType,
//    CheckedExceptionNotInThrowsClause,
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
//    ThrowsEnsuresOnConstructor,

//    UseDefViolation,
//    UseDefViolationOut,   // partially enforced. ( only usedef, no must-assign-before-exit)
//    UseDefViolationField, // Not enforced yet.
//    UseDefViolationThis,  // Not enforced yet.
//    ReturnExpected,
//    ParamUnassigned,
//    UnreferencedVar,
//    UnreferencedVarAssg,
    TemplateTypeRequiresArgs,

    // Nonnull related messages.
    ReceiverCannotBeNull,
    UseOfPossiblyNullPointer,
    UseOfNullPointer,

    InvalidCompilerOptionArgument,
    TypeOfExprMustBeGuardedClass,

    CannotLoadShadowedAssembly,
    TypeMissingInShadowedAssembly,
    MethodMissingInShadowedAssembly,

    NonNullFieldNotInitializedBeforeConstructorCall,
    ModifiesNotAllowedInOverride,

    NoSuchMethod,
    CouldNotLoadPluginType,
    CouldNotInstantiatePluginType,
    PluginTypeMustAlreadyBeCompiled,
    PluginTypeMustImplementIPlugin,
    PluginCrash,

    OtherwiseExpressionMustBeNonNull,
    OtherwiseExpressionMustBeType,

    GeneralComprehensionsNotAllowedInMethodContracts,
    StrictReadonlyNotReadonly,
    StrictReadonlyStatic,
    StrictReadonlyAssignment,
    StrictReadonlyMultipleAssignment,

    WritingPackedObject,
    ExposingExposedObject,
    DontKnowIfCanExposeObject,

    GenericWarning,    

    AccessThroughDelayedReference,
    StoreIntoLessDelayedLocation,
    NonNullFieldNotInitializedAtEndOfDelayedConstructor,

    BaseNotInitialized,
    BaseMultipleInitialization,

    ActualCannotBeDelayed,
    DelayedReferenceByReference,
    DelayedRefParameter,
    DelayedStructConstructor,
    ActualMustBeDelayed,
    ReceiverCannotBeDelayed,
    ReceiverMustBeDelayed,
    NonNullFieldNotInitializedByDefaultConstructor,
    AccessThroughDelayedThisInConstructor,

    MustOverrideMethodMissing,
    MustOverrideMethodNotMarkedAsMustOverride,
    MethodMarkedMustOverrideMustBeVirtual,

    MethodInheritingPurityMustBeMarked,
    StaticMethodCannotBeMarkedWithAttribute,
    OverrideMethodNotMarkedWithAttribute,

    MutableHasImmutableBase,
    ImmutableHasMutableBase,
    ImmutableIfaceImplementedByMutableClass,
    MutableIfaceExtendsImmutableIface,
    RepOrPeerFieldImmutable,
    ImmutableClassHasPeerField,
    ImmutableConstructorBaseNotLast,
    ImmutableConstructorLastStmtMustBeBaseOrThis,

    BadUseOfOwnedOnMethod,
    UseOfRepOnVirtualMethod,

    NotAdmissibleTargetNotAdmissibile,
    NotAdmissibleTargetNotThis,
    NotAdmissibleFirstNonRepChainNotEnded,
    NotAdmissibleNonRepOrPeerChainNotEnded,
    NotAdmissibleBoundCollArrayNotRep,
    NotAdmissibleBoundCollArrayNotOwned,
    NotAdmissibleStaticNonStateIndependent,
    NotAdmissibleFirstAccessOnNonAdditiveField,
    NotAdmissibleFirstAccessOnFieldDeclaredInSubtype,

    StateIndependentSpecNotAdmissible,
    ConfinedSpecContainsPureCall,
    StateIndependentSpecContainsPureOrConfinedCall,
    IsNewExprInPureEnsures,  // deprecated
    DisallowedInConfinedOrStIndepSpec,

    NoReferenceComparisonAttrNotCopied,
    NonPureMarkedNoReferenceComparison,
    ViolatedNoReferenceComparison,
    ResultNotNewlyAllocatedAttrNotCopied,
    NonPureMarkedResultNotNewlyAllocated,
    NonRefTypeMethodMarkedResultNotNewlyAllocated,
    MethodShouldBeMarkedNoReferenceComparison,
    BothOperandsOfReferenceComparisonMightBeNewlyAllocated,

    AttributeAllowedOnlyOnReferenceTypeParameters,
    ConflictingAttributes,
    VirtualMethodWithNonVirtualMethodAttribute,
    AttributeNotAllowedOnInterfaceMethods,
    AttributeNotAllowedOnPrivateMember,
    AttributeNotAllowedOnConstructor,
    AttributeNotAllowedOnlyOnInParameters,

    UnnecessaryNonNullCoercion,
    SideEffectsNotAllowedInContracts,

    ShouldCommit,

    ParameterNoDelayAttribute,
    ParameterExtraDelayAttribute,
    ThisParameterNoDelayAttribute,
    ThisParameterExtraDelayAttribute,

    ReturnOfDelayedValue,
    ShouldCommitOnAllPaths,
    UnboxDelayedValue,
    ThrowsDelayedValue,
    CannotUseDelayedPointer,
    CannotUseDelayedTypedRef,

    //ModelField errors
    SatisfiesInInterface,
    AssignToModelfield,
    InternalFieldInOverridableModelfield,     
    GenericError,    


    InvalidModifiesClause,
    MemberCannotBeAnnotatedAsPure,
    CannotMatchArglist,
    PureMethodWithOutParamUsedInContract,
    PureMethodCannotHaveRefParam,
    ReadsWithoutPure,
    InconsistentPurityAttributes,
    PureOwnedNotAllowed,
    PureMethodCannotHaveModifies,
  }

  internal class SpecSharpErrorNode : ErrorNode{
    private static WeakReference resourceManager = new WeakReference(null);
    internal static System.Resources.ResourceManager ResourceManager {
      get {
        System.Resources.ResourceManager rMgr = (System.Resources.ResourceManager)resourceManager.Target;
        if (rMgr == null) {
          SpecSharpErrorNode.resourceManager =
            new WeakReference(rMgr = new System.Resources.ResourceManager("Microsoft.SpecSharp.ErrorMessages", typeof(SpecSharpErrorNode).Module.Assembly));
        }
        return rMgr;
      }
    }

    internal SpecSharpErrorNode(Error code, params string[] messageParameters)
      : base((int)code, messageParameters){
    }
    public override string GetErrorNumber(){
      return "CS"+this.Code.ToString("0000");
    }
    public override string GetMessage(System.Globalization.CultureInfo culture){
      return this.GetMessage(((Error)this.Code).ToString(), SpecSharpErrorNode.ResourceManager, culture);
    }
    public override int Severity{
      get{
        switch ((Error)this.Code){
          case Error.AlwaysNull: return 2;
          case Error.AttributeLocationOnBadDeclaration: return 1;
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
          case Error.DeprecatedSymbol: return 2;
          case Error.DeprecatedSymbolStr: return 2;
          case Error.DuplicateUsing: return 3;
          case Error.EmptySwitch: return 1;
          case Error.EqualityOpWithoutEquals: return 3;
          case Error.EqualityOpWithoutGetHashCode: return 3;
          case Error.ExpressionIsAlreadyOfThisType: return 4;
          case Error.ExternMethodNoImplementation: return 1;
          case Error.InvalidAttributeLocation: return 1;
          case Error.InvalidMainSig: return 4;
          case Error.IsAlwaysFalse: return 1;
          case Error.IsAlwaysTrue: return 1;
          case Error.LowercaseEllSuffix: return 4;
          case Error.MainCantBeGeneric: return 4;
          case Error.MultipleTypeDefs: return 1;
          case Error.NewOrOverrideExpected: return 2;
          case Error.NewNotRequired: return 4;
          case Error.NewRequired: return 1;
          case Error.NonObsoleteOverridingObsolete: return 1;
          case Error.PossibleMistakenNullStatement: return 3;
          case Error.ProtectedInSealed: return 4;
          case Error.RedundantBox: return 1;
          case Error.RedundantNonNull: return 1;
          case Error.RedundantStream: return 1;
          case Error.RelatedErrorLocation: return -1;
          case Error.RelatedErrorModule: return -1;
          case Error.RelatedWarningLocation: return -1;
          case Error.RelatedWarningModule: return -1;
          case Error.SealedTypeIsAlreadyInvariant: return 1;
          case Error.UnknownEntity: return 2;
          case Error.UnreachableCode: return 2;
          case Error.UnreferencedLabel: return 2;
          case Error.UnreferencedVarAssg: return 2;
          case Error.UseDefViolationField: return 1;
          case Error.UseSwitchInsteadOfAttribute: return 1;
          case Error.VacuousIntegralComp: return 2;
          case Error.ValueTypeIsAlreadyInvariant: return 1;
          case Error.ValueTypeIsAlreadyNonNull: return 1;
          case Error.VolatileByRef: return 1;
          case Error.WarningDirective: return 2;

          case Error.CoercionToNonNullTypeMightFail: return 2;
          case Error.CannotCoerceNullToNonNullType: return 1;
          case Error.UnnecessaryNonNullCoercion: return 2;
          case Error.ReceiverMightBeNull: return 2;
          case Error.ReceiverCannotBeNull: return 1;
          case Error.UseOfNullPointer: return 1;
          case Error.UseOfPossiblyNullPointer: return 2;

          case Error.CannotLoadShadowedAssembly: return 2;
          case Error.TypeMissingInShadowedAssembly: return 2;
          case Error.MethodMissingInShadowedAssembly: return 2;

          case Error.NonNullFieldNotInitializedBeforeConstructorCall: return 2;
          case Error.AccessThroughDelayedReference: return 2;
          case Error.StoreIntoLessDelayedLocation : return 2;
          case Error.NonNullFieldNotInitializedAtEndOfDelayedConstructor : return 2;
          case Error.BaseNotInitialized : return 2;
          case Error.BaseMultipleInitialization : return 2;
          case Error.ActualCannotBeDelayed : return 2;
          case Error.DelayedReferenceByReference : return 2;
          case Error.DelayedRefParameter : return 2;
          case Error.DelayedStructConstructor : return 2;
          case Error.ActualMustBeDelayed : return 2;
          case Error.ReceiverCannotBeDelayed : return 2;
          case Error.ReceiverMustBeDelayed : return 2;
          case Error.NonNullFieldNotInitializedByDefaultConstructor : return 2;
          case Error.AccessThroughDelayedThisInConstructor : return 2;

          case Error.ShouldCommit: return 2;
          case Error.ShouldCommitOnAllPaths: return 2;
          case Error.UnboxDelayedValue: return 2;
          case Error.ThrowsDelayedValue: return 2;
          case Error.CannotUseDelayedPointer: return 2;
          case Error.CannotUseDelayedTypedRef: return 2;
          case Error.GenericWarning: return 4;
        }
        return 0;
      }
    }
  }
  public class ErrorHandler : Cci.ErrorHandler{
    internal Parameter currentParameter;
    public TypeNode currentType;
    public bool refOrOutAddress;

    public ErrorHandler(ErrorNodeList errors)
      : base(errors){
    }
    internal static Error MapError(SysError error){
      switch(error){
        case SysError.AbstractAndExtern: return Error.AbstractAndExtern;
        case SysError.AbstractAttributeClass: return Error.AbstractAttributeClass;
        case SysError.AbstractBaseCall: return Error.AbstractBaseCall;
        case SysError.AbstractEventInitializer: return Error.AbstractEventInitializer;
        case SysError.AbstractHasBody: return Error.AbstractHasBody;
        case SysError.AbstractInterfaceMethod: return Error.AbstractInterfaceMethod;
        case SysError.AbstractMethodInConcreteType: return Error.AbstractInConcreteClass;
        case SysError.AbstractMethodTemplate: return Error.AbstractMethodTemplate;
        case SysError.AbstractSealedArrayElementType: return Error.ArrayOfStaticClass;
        case SysError.AbstractSealedBaseClass: return Error.StaticBaseClass;
        case SysError.AbstractSealedClassInterfaceImpl: return Error.StaticClassInterfaceImpl;
        case SysError.AbstractSealedDerivedFromNonObject: return Error.StaticDerivedFromNonObject;
        case SysError.AbstractSealedFieldType: return Error.VarDeclIsStaticClass;
        case SysError.AbstractSealedLocalType: return Error.VarDeclIsStaticClass;
        case SysError.AbstractSealedParameterType: return Error.ParameterIsStaticClass;
        case SysError.AbstractSealedReturnType: return Error.ReturnTypeIsStaticClass;
        case SysError.AccessThroughDelayedReference: return Error.AccessThroughDelayedReference;
        case SysError.AccessThroughDelayedThisInConstructor: return Error.AccessThroughDelayedThisInConstructor;
        case SysError.AccessToNonStaticOuterMember: return Error.WrongNestedThis;
        case SysError.ActualCannotBeDelayed: return Error.ActualCannotBeDelayed;
        case SysError.ActualMustBeDelayed: return Error.ActualMustBeDelayed;
        case SysError.AliasNotFound: return Error.AliasNotFound;
        case SysError.AlwaysNull: return Error.AlwaysNull;
        case SysError.AmbiguousBinaryOperation: return Error.AmbigBinaryOps;
        case SysError.AmbiguousCall: return Error.AmbigCall;
        case SysError.AmbiguousConditional: return Error.AmbigQM;
        case SysError.AmbiguousTypeReference: return Error.AmbigContext;
        case SysError.AnonymousNestedFunctionNotAllowed: return Error.AnonMethNotAllowed;
        case SysError.ArrayElementCannotBeTypedReference: return Error.ArrayElementCantBeRefAny;
        case SysError.ArrayInitializerLengthMismatch: return Error.InvalidArray;
        case SysError.AsMustHaveReferenceType: return Error.AsMustHaveReferenceType;
        case SysError.AssemblyCouldNotBeSigned: return Error.CryptoFailed;
        case SysError.AssemblyKeyFileMissing: return Error.AssemblyKeyFileMissing;
        case SysError.AssignmentHasNoEffect: return Error.AssgLvalueExpected;
        case SysError.AssignmentToBase: return Error.BaseIllegal;
        case SysError.AssignmentToEvent: return Error.BadEventUsageNoField;
        case SysError.AssignmentToFixedVariable: return Error.AssgReadonlyLocalCause;
        case SysError.AssignmentToLiteral: return Error.AssgLvalueExpected;
        case SysError.AssignmentToReadOnlyInstanceField: return Error.AssgReadonly;
        case SysError.AssignmentToReadOnlyLocal: return Error.AssgReadonlyLocal;
        case SysError.AssignmentToReadOnlyStaticField: return Error.AssgReadonlyStatic;
        case SysError.AssignmentToType: return Error.BadSKknown;
        case SysError.AsWithTypeVar: return Error.AsWithTypeVar;
        case SysError.AttributeOnBadTarget: return Error.AttributeOnBadSymbolType;
        case SysError.AttributeHasBadTarget: return Error.AttributeLocationOnBadDeclaration;
        case SysError.AttributeUsageOnNonAttributeClass: return Error.AttributeUsageOnNonAttributeClass;
        case SysError.AutoWin32ResGenFailed: return Error.AutoResGen;
        case SysError.BadAttributeParam: return Error.BadAttributeParam;
        case SysError.BadBinaryOperatorSignature: return Error.BadBinaryOperatorSignature;
        case SysError.BadBinaryOps: return Error.BadBinaryOps;
        case SysError.BadBox: return Error.BadBox;
        case SysError.BadBoolOp: return Error.BadBoolOp;
        case SysError.BadCallToEventHandler: return Error.BadEventUsageNoField;
        case SysError.BadExplicitCoercionInFixed: return Error.BadCastInFixed;
        case SysError.BadEmptyThrow: return Error.BadEmptyThrow;
        case SysError.BadExceptionType: return Error.BadExceptionType;
        case SysError.BadExitOrContinue: return Error.NoBreakOrCont;
        case SysError.BadFinallyLeave: return Error.BadFinallyLeave;
        case SysError.BadFixedVariableType: return Error.BadFixedInitType;
        case SysError.BadForeachCollection: return Error.ForEachMissingMember;
        case SysError.BadGetEnumerator: return Error.BadGetEnumerator;
        case SysError.BadIncDecSignature: return Error.BadIncDecSignature;
        case SysError.BadNamedAttributeArgument: return Error.BadNamedAttributeArgument;
        case SysError.BadNamedAttributeArgumentType: return Error.BadNamedAttributeArgumentType;
        case SysError.BadNestedTypeReference: return Error.BadTypeReference;
        case SysError.BadNonEmptyStream: return Error.BadNonEmptyStream;
        case SysError.BadNonNull: return Error.BadNonNull;
        case SysError.BadNonNullOnStream: return Error.BadNonNullOnStream;
        case SysError.BadRefCompareLeft: return Error.BadRefCompareLeft;
        case SysError.BadRefCompareRight: return Error.BadRefCompareRight;
        case SysError.BadStream: return Error.BadStream;
        case SysError.BadStreamOnNonNullStream: return Error.BadStreamOnNonNullStream;
        case SysError.BadTupleIndex : return Error.BadTupleIndex;
        case SysError.BadUnaryOp: return Error.BadUnaryOp;
        case SysError.BadUnaryOperatorSignature: return Error.BadUnaryOperatorSignature;
        case SysError.BadUseOfEvent: return Error.BadEventUsageNoField;
        case SysError.BadUseOfMethod: return Error.BadUseOfMethod;
        case SysError.BaseClassLessAccessible: return Error.BadVisBaseClass;
        case SysError.BaseInBadContext: return Error.BaseInBadContext;
        case SysError.BaseInStaticCode: return Error.BaseInStaticMeth;
        case SysError.BaseInterfaceLessAccessible: return Error.BadVisBaseInterface;
        case SysError.BaseMultipleInitialization: return Error.BaseMultipleInitialization;
        case SysError.BaseNotInitialized: return Error.BaseNotInitialized;
        case SysError.BatchFileNotRead: return Error.BatchFileNotRead;
        case SysError.BitwiseOrSignExtend: return Error.BitwiseOrSignExtend;
        case SysError.CannotCallSpecialMethod: return Error.CantCallSpecialMethod;
        case SysError.CannotCoerceNullToValueType: return Error.ValueCantBeNull;
        case SysError.CannotDeferenceNonPointerType: return Error.PtrExpected;
        case SysError.CannotDeriveFromSealedType: return Error.CantDeriveFromSealedClass;
        case SysError.CannotDeriveFromSpecialType: return Error.DeriveFromEnumOrValueType;
        case SysError.CannotExplicitlyImplementAccessor: return Error.ExplicitMethodImplAccessor;
        case SysError.CannotInferMethTypeArgs: return Error.CantInferMethTypeArgs;
        case SysError.CannotMatchArglist: return Error.CannotMatchArglist;
        case SysError.CannotOverrideAccessor: return Error.CantOverrideAccessor;
        case SysError.CannotOverrideFinal: return Error.CantOverrideSealed;
        case SysError.CannotOverrideNonEvent: return Error.CantOverrideNonEvent;
        case SysError.CannotOverrideNonVirtual: return Error.CantOverrideNonVirtual;
        case SysError.CannotOverrideSpecialMethod: return Error.CantOverrideSpecialMethod;
        case SysError.CannotReturnTypedReference: return Error.MethodReturnCantBeRefAny;
        case SysError.CannotReturnValue: return Error.RetNoObjectRequired;
        case SysError.CannotYieldFromCatchClause: return Error.CannotYieldFromCatchClause;
        case SysError.CannotYieldFromTryBlock: return Error.CannotYieldFromTryBlock;
        case SysError.CaseFallThrough: return Error.CaseFallThrough;
        case SysError.CheckedExceptionNotInThrowsClause: return Error.CheckedExceptionNotInThrowsClause;
        case SysError.CircularBase: return Error.CircularBase;
        case SysError.CircularConstantDefinition: return Error.CircConstValue;
        case SysError.ClashWithLocalConstant: return Error.LocalConstDuplicate;
        case SysError.ClashWithLocalVariable: return Error.LocalDuplicate;
        case SysError.CloseUnimplementedInterfaceMember: return Error.CloseUnimplementedInterfaceMember;
        case SysError.CLSNotOnModules: return Error.CLSNotOnModules;
        case SysError.ComImportWithoutGuidAttribute: return Error.ComImportWithoutUuidAttribute;
        case SysError.ConcreteMissingBody: return Error.ConcreteMissingBody;
        case SysError.ConditionalMustReturnVoid: return Error.ConditionalMustReturnVoid;
        case SysError.ConditionalOnInterfaceMethod: return Error.ConditionalOnInterfaceMethod;
        case SysError.ConditionalOnOverride: return Error.ConditionalOnOverride;
        case SysError.ConditionalOnSpecialMethod: return Error.ConditionalOnSpecialMethod;
        case SysError.ConflictBetweenAliasAndType: return Error.ConflictAliasAndMember;
        case SysError.ConstantExpected: return Error.ConstantExpected;
        case SysError.ConstraintIsAbstractSealedClass: return Error.ConstraintIsStaticClass;
        case SysError.ConstructsAbstractClass: return Error.NoNewAbstract;
        case SysError.ConstructsAbstractSealedClass: return Error.InstantiatingStaticClass;
        case SysError.ConstructorInAbstractSealedClass: return Error.ConstructorInStaticClass;
        case SysError.ContainingTypeDoesNotImplement: return Error.ClassDoesntImplementInterface;
        case SysError.ConversionNotInvolvingContainedType: return Error.ConversionNotInvolvingContainedType;
        case SysError.ConversionWithBase: return Error.ConversionWithBase;
        case SysError.ConversionWithDerived: return Error.ConversionWithDerived;
        case SysError.ConversionWithInterface: return Error.ConversionWithInterface;
        case SysError.CTOverflow: return Error.CheckedOverflow;
        case SysError.CustomAttributeError: return Error.CustomAttributeError;
        case SysError.CycleInInterfaceInheritance: return Error.CycleInInterfaceInheritance;
        case SysError.DestructorInAbstractSealedClass: return Error.DestructorInStaticClass;
        case SysError.DefaultContructorConstraintNotSatisfied: return Error.NewConstraintNotSatisfied;
        case SysError.DelayedReferenceByReference: return Error.DelayedReferenceByReference;
        case SysError.DelayedRefParameter: return Error.DelayedRefParameter;
        case SysError.DelayedStructConstructor: return Error.DelayedStructConstructor;
        case SysError.DllImportOnInvalidMethod: return Error.DllImportOnInvalidMethod;
        case SysError.DuplicateAliasDefinition: return Error.DuplicateAlias;
        case SysError.DuplicateAssemblyReference: return Error.None;
        case SysError.DuplicateAttribute: return Error.DuplicateAttribute;
        case SysError.DuplicateCaseLabel: return Error.DuplicateCaseLabel;
        case SysError.DuplicateConversion: return Error.DuplicateConversionInClass;
        case SysError.DuplicateIndexer: return Error.MemberAlreadyExists;
        case SysError.DuplicateInterfaceInBaseList: return Error.DuplicateInterfaceInBaseList;
        case SysError.DuplicateMethod: return Error.MemberAlreadyExists;
        case SysError.DuplicateModuleReference: return Error.None;
        case SysError.DuplicateNamedAttributeArgument: return Error.DuplicateNamedAttributeArgument;
        case SysError.DuplicateParameterName: return Error.DuplicateParamName;
        case SysError.DuplicateResponseFile: return Error.DuplicateResponseFile;
        case SysError.DuplicateType: return Error.DuplicateNameInNS;
        case SysError.DuplicateTypeMember: return Error.DuplicateNameInClass;
        case SysError.DuplicateUsedNamespace: return Error.DuplicateUsing;
        case SysError.EnumerationValueOutOfRange: return Error.EnumeratorOverflow;
        case SysError.EventNotDelegate: return Error.EventNotDelegate;
        case SysError.EqualityOpWithoutEquals: return Error.EqualityOpWithoutEquals;
        case SysError.EqualityOpWithoutGetHashCode: return Error.EqualityOpWithoutGetHashCode;
        case SysError.Error: return Error.Error;
        case SysError.ExplicitDefaultConstructorForValueType: return Error.StructsCantContainDefaultContructor;
        case SysError.ExplicitlyImplementedTypeNotInterface: return Error.ExplicitInterfaceImplementationNotInterface;
        case SysError.ExplicitPropertyAddingAccessor: return Error.ExplicitPropertyAddingAccessor;
        case SysError.ExplicitPropertyMissingAccessor: return Error.ExplicitPropertyMissingAccessor;
        case SysError.ExpressionIsAlreadyOfThisType: return Error.ExpressionIsAlreadyOfThisType;
        case SysError.FamilyInSealed: return Error.ProtectedInSealed;
        case SysError.FamilyInStruct: return Error.ProtectedInStruct;
        case SysError.FatalError: return Error.FatalError;
        case SysError.FieldOffsetNotAllowed: return Error.StructOffsetOnBadStruct;
        case SysError.FieldOffsetNotAllowedOnStaticField: return Error.StructOffsetOnBadField;
        case SysError.FieldTypeLessAccessibleThanField: return Error.BadVisFieldType;
        case SysError.FixedMustInit: return Error.FixedMustInit;
        case SysError.FixedNeeded: return Error.FixedNeeded;
        case SysError.FixedNotNeeded: return Error.FixedNotNeeded;
        case SysError.GeneralComprehensionsNotAllowedInMethodContracts: return Error.GeneralComprehensionsNotAllowedInMethodContracts;
        case SysError.GenericWarning: return Error.GenericWarning;        
        case SysError.GlobalSingleTypeNameNotFound: return Error.GlobalSingleTypeNameNotFound;
        case SysError.GotoLeavesNestedMethod: return Error.BadDelegateLeave;
        case SysError.HidesAbstractMethod: return Error.HidingAbstractMethod;
        case SysError.IntegerDivisionByConstantZero: return Error.IntDivByZero;
        case SysError.IdentifierNotFound: return Error.NameNotInContext;
        case SysError.IdentityConversion: return Error.IdentityConversion;
        case SysError.IllegalPointerType: return Error.IllegalPointerType;
        case SysError.ImpossibleCast: return Error.NoExplicitBuiltinConv;
        case SysError.InaccessibleEventBackingField: return Error.BadEventUsage;
        case SysError.InconsistantIndexerNames: return Error.InconsistantIndexerNames;
        case SysError.IndexerInAbstractSealedClass: return Error.IndexerInStaticClass;
        case SysError.IndexerNameAttributeOnOverride: return Error.NameAttributeOnOverride;
        case SysError.IndexerNameNotIdentifier: return Error.BadArgumentToNameAttribute;
        case SysError.InstanceFieldInitializerInStruct: return Error.FieldInitializerInStruct;
        case SysError.InstanceMemberInAbstractSealedClass: return Error.InstanceMemberInStaticClass;
        case SysError.IntegralTypeValueExpected: return Error.IntegralTypeValueExpected;
        case SysError.InterfaceHasConstructor: return Error.InterfacesCannotContainConstructors;
        case SysError.InterfaceHasField: return Error.InterfacesCannotContainFields;
        case SysError.InterfaceImplementedByConditional: return Error.InterfaceImplementedByConditional;
        case SysError.InterfaceMemberHasBody: return Error.InterfaceMemberHasBody;
        case SysError.InterfaceMemberNotFound: return Error.InterfaceMemberNotFound;
        case SysError.InternalCompilerError: return Error.InternalCompilerError;
        case SysError.InvalidAddressOf: return Error.InvalidAddrOp;
        case SysError.InvalidAttributeArgument: return Error.InvalidAttributeArgument;
        case SysError.InvalidCodePage: return Error.BadCodePage;
        case SysError.InvalidCompilerOption: return Error.UnknownOption;
        case SysError.InvalidCompilerOptionArgument: return Error.InvalidCompilerOptionArgument;
        case SysError.InvalidConditional: return Error.InvalidQM;
        case SysError.InvalidData: return Error.InvalidData;
        case SysError.InvalidDebugInformationFile: return Error.DebugInitFile;
        case SysError.InvalidGotoCase: return Error.InvalidGotoCase;
        case SysError.InvalidMainMethodSignature: return Error.InvalidMainSig;
        case SysError.InvalidOutputFile: return Error.OutputWriteFailed;
        case SysError.InvalidWin32ResourceFileContent: return Error.BadWin32Res;
        case SysError.IsAlwaysOfType: return Error.IsAlwaysTrue;
        case SysError.IsBinaryFile: return Error.IsBinaryFile;
        case SysError.IsNeverOfType: return Error.IsAlwaysFalse;
        case SysError.LabelIdentiferAlreadyInUse: return Error.DuplicateLabel;
        case SysError.LabelNotFound: return Error.LabelNotFound;
        case SysError.LockNeedsReference: return Error.LockNeedsReference;
        case SysError.MainCantBeGeneric: return Error.MainCantBeGeneric;
        case SysError.ManagedAddr: return Error.ManagedAddr;
        case SysError.MemberDoesNotHideBaseClassMember: return Error.NewNotRequired;
        case SysError.MemberHidesBaseClassMember: return Error.NewRequired;
        case SysError.MemberHidesBaseClassOverridableMember: return Error.NewOrOverrideExpected;
        case SysError.MemberNotVisible: return Error.BadAccess;
        case SysError.MethodNameExpected: return Error.MethodNameExpected;
        case SysError.MissingStructOffset: return Error.MissingStructOffset;
        case SysError.MultipleMainMethods: return Error.MultipleEntryPoints;
        case SysError.MultipleTypeImport: return Error.MultipleTypeDefs;
        case SysError.MustHaveOpTF: return Error.MustHaveOpTF;
        case SysError.NegativeArraySize: return Error.NegativeArraySize;
        case SysError.NegativeStackAllocSize: return Error.NegativeStackAllocSize;
        case SysError.NestedFunctionDelegateParameterMismatch: return Error.CantConvAnonMethParams;
        case SysError.NestedFunctionDelegateParameterMismatchBecauseOfOutParameter: return Error.CantConvAnonMethNoParams;
        case SysError.NestedFunctionDelegateReturnTypeMismatch: return Error.CantConvAnonMethReturns;
        case SysError.NoExplicitCoercion: return Error.NoExplicitConversion;
        case SysError.NoImplicitCoercion: return Error.NoImplicitConversion;
        case SysError.NoImplicitCoercionFromConstant: return Error.ConstOutOfRange;
        case SysError.NoGetter: return Error.PropertyLacksGet;
        case SysError.NoGetterToOverride: return Error.NoGetToOverride;
        case SysError.NoMainMethod: return Error.NoEntryPoint;
        case SysError.NoMethodMatchesDelegate: return Error.InvalidCall;
        case SysError.NoMethodToOverride: return Error.CantOverrideNonFunction;
        case SysError.NonNullFieldNotInitializedBeforeConstructorCall: return Error.NonNullFieldNotInitializedBeforeConstructorCall;
        case SysError.NonNullFieldNotInitializedAtEndOfDelayedConstructor: return Error.NonNullFieldNotInitializedAtEndOfDelayedConstructor;
        case SysError.NonNullFieldNotInitializedByDefaultConstructor: return Error.NonNullFieldNotInitializedByDefaultConstructor;
        case SysError.NoOverloadWithMatchingArgumentCount: return Error.BadArgCount;
        case SysError.NoPropertyToOverride: return Error.CantOverrideNonProperty;
        case SysError.NoSetter: return Error.AssgReadonlyProp;
        case SysError.NoSetterToOverride: return Error.NoSetToOverride;
        case SysError.NoSourceFiles: return Error.NoSources;
        case SysError.NoSuchConstructor: return Error.NoConstructors;
        case SysError.NoSuchFile: return Error.NoSuchFile;
        case SysError.NoSuchLabel: return Error.LabelNotFound;
        case SysError.NoSuchMember: return Error.NoSuchMember;
        case SysError.NoSuchMethod: return Error.NoSuchMethod;
        case SysError.NoSuchNestedType: return Error.DottedTypeNameNotFoundInAgg;
        case SysError.NoSuchOperator: return Error.NoSuchOperator;
        case SysError.NoSuchQualifiedType: return Error.TypeNameNotFound;
        case SysError.NoSuchType: return Error.SingleTypeNameNotFound;
        case SysError.NonObsoleteOverridingObsolete: return Error.NonObsoleteOverridingObsolete;
        case SysError.NotAnAssembly: return Error.ImportNonAssembly;
        case SysError.NotAnAttribute: return Error.NotAnAttributeClass;
        case SysError.NotAnInterface: return Error.NonInterfaceInInterfaceList;
        case SysError.NotAModule: return Error.AddModuleAssembly;
        case SysError.NotAssignable: return Error.AssgLvalueExpected;
        case SysError.NotATemplateType : return Error.HasNoTypeVars;
        case SysError.NotAType: return Error.NotAType;
        case SysError.NotConstantExpression: return Error.NotConstantExpression;
        case SysError.NotIndexable: return Error.BadIndexLHS;
        case SysError.NotVisibleViaBaseType: return Error.BadProtectedAccess;
        case SysError.NotYetImplemented: return Error.FeatureNYI2;
        case SysError.NullNotAllowed: return Error.NullNotValid;
        case SysError.ObjectRequired: return Error.ObjectRequired;
        case SysError.ObsoleteError: return Error.DeprecatedSymbolError;
        case SysError.ObsoleteErrorWithMessage: return Error.DeprecatedSymbolStrError;
        case SysError.ObsoleteWarning: return Error.DeprecatedSymbol;
        case SysError.ObsoleteWarningWithMessage: return Error.DeprecatedSymbolStr;
        case SysError.OperatorInAbstractSealedClass: return Error.OperatorInStaticClass;
        case SysError.OperatorNeedsMatch: return Error.OperatorNeedsMatch;
        case SysError.OpTrueFalseMustResultInBool: return Error.OpTFRetType;
        case SysError.OverloadRefOut: return Error.OverloadRefOut;
        case SysError.OverrideChangesAccess: return Error.CantChangeAccessOnOverride;
        case SysError.OverrideChangesReturnType: return Error.CantChangeReturnTypeOnOverride;
        case SysError.OverrideNotExpected: return Error.OverrideNotExpected;
        case SysError.ParamArrayMustBeLast: return Error.ParamsOrVarargsMustBeLast;
        case SysError.ParamArrayParameterMustBeArrayType: return Error.ParamsMustBeArray;
        case SysError.ParameterLessAccessibleThanDelegate: return Error.BadVisDelegateParam;
        case SysError.ParameterLessAccessibleThanIndexedProperty: return Error.BadVisIndexerParam;
        case SysError.ParameterLessAccessibleThanMethod: return Error.BadVisParamType;
        case SysError.ParameterLessAccessibleThanOperator: return Error.BadVisOpParam;
        case SysError.ParameterTypeCannotBeTypedReference: return Error.MethodArgCantBeRefAny;
        case SysError.PartialClassesSpecifyMultipleBases: return Error.PartialMultipleBases;
        case SysError.PInvokeHasBody: return Error.ExternHasBody;
        case SysError.PInvokeWithoutModuleOrImportName: return Error.ExternMethodNoImplementation;
        case SysError.PointerInAsOrIs: return Error.PointerInAsOrIs;
        case SysError.PointerMustHaveSingleIndex: return Error.PtrIndexSingle;
        case SysError.PossibleBadNegCast: return Error.PossibleBadNegCast;
        case SysError.PropertyCantHaveVoidType: return Error.PropertyCantHaveVoidType;        
        case SysError.PropertyTypeLessAccessibleThanIndexedProperty: return Error.BadVisIndexerReturn;
        case SysError.PropertyTypeLessAccessibleThanProperty: return Error.BadVisPropertyType;
        case SysError.PropertyWithNoAccessors: return Error.PropertyWithNoAccessors;
        case SysError.QueryNotSupported: return Error.QueryNotSupported;
        case SysError.QueryNoMatch: return Error.QueryNoMatch;
        case SysError.QueryAmbiguousContextName: return Error.QueryAmbiguousContextName;
        case SysError.QueryBadAggregate: return Error.QueryBadAggregate;
        case SysError.QueryBadAggregateForm: return Error.QueryBadAggregateForm;
        case SysError.QueryBadGroupByList: return Error.QueryBadGroupByList;
        case SysError.QueryBadOrderList: return Error.QueryBadOrderList;
        case SysError.QueryBadProjectionList: return Error.QueryBadProjectionList;
        case SysError.QueryBadQuantifier: return Error.QueryBadQuantifier;
        case SysError.QueryBadQuantifiedExpression: return Error.QueryBadQuantifiedExpression;
        case SysError.QueryBadDifferenceTypes: return Error.QueryBadDifferenceTypes;
        case SysError.QueryBadInsertList: return Error.QueryBadInsertList;
        case SysError.QueryBadIntersectionTypes: return Error.QueryBadIntersectionTypes;
        case SysError.QueryBadLimit: return Error.QueryBadLimit;
        case SysError.QueryBadLimitNotLiteral: return Error.QueryBadLimitNotLiteral;
        case SysError.QueryBadLimitForNotPercent: return Error.QueryBadLimitForNotPercent;
        case SysError.QueryBadOrderItem: return Error.QueryBadOrderItem;
        case SysError.QueryBadUnionTypes: return Error.QueryBadUnionTypes;
        case SysError.QueryBadUpdateList: return Error.QueryBadUpdateList;
        case SysError.QueryBadTypeFilter: return Error.QueryBadTypeFilter;
        case SysError.QueryMissingDefaultConstructor: return Error.QueryMissingDefaultConstructor;
        case SysError.QueryNoContext: return Error.QueryNoContext;
        case SysError.QueryNotAddStream: return Error.QueryNotAddStream;
        case SysError.QueryNotDeleteStream: return Error.QueryNotDeleteStream;
        case SysError.QueryNotInsertStream: return Error.QueryNotInsertStream;
        case SysError.QueryNotScalar: return Error.QueryNotScalar;
        case SysError.QueryNotStream: return Error.QueryNotStream;
        case SysError.QueryNotTransactable: return Error.QueryNotTransactable;
        case SysError.QueryNotTransacted: return Error.QueryNotTransacted;
        case SysError.QueryNoNestedTransaction: return Error.QueryNoNestedTransaction;
        case SysError.QueryNotUpdateStream: return Error.QueryNotUpdateStream;
        case SysError.QueryProjectThroughTypeUnion: return Error.QueryProjectThroughTypeUnion;
        case SysError.QueryIsCyclic: return Error.QueryIsCyclic;
        case SysError.ReceiverCannotBeDelayed: return Error.ReceiverCannotBeDelayed;
        case SysError.ReceiverMustBeDelayed: return Error.ReceiverMustBeDelayed;
        case SysError.RecursiveConstructorCall: return Error.RecursiveConstructorCall;
        case SysError.RefConstraintNotSatisfied: return Error.RefConstraintNotSatisfied;
        case SysError.RedundantBox: return Error.RedundantBox;
        case SysError.RedundantNonNull: return Error.RedundantNonNull;
        case SysError.RedundantStream: return Error.RedundantStream;
        case SysError.RelatedErrorLocation: return Error.RelatedErrorLocation;
        case SysError.RelatedErrorModule: return Error.RelatedErrorModule;
        case SysError.RelatedWarningLocation: return Error.RelatedWarningLocation;
        case SysError.RelatedWarningModule: return Error.RelatedWarningModule;
        case SysError.ResultIsNotReference: return Error.ReturnNotLValue;
        case SysError.ReturnTypeLessAccessibleThanDelegate: return Error.BadVisDelegateReturn;
        case SysError.ReturnOfDelayedValue: return Error.ReturnOfDelayedValue;
        case SysError.ReturnTypeLessAccessibleThanMethod: return Error.BadVisReturnType;
        case SysError.ReturnTypeLessAccessibleThanOperator: return Error.BadVisOpReturn;
        case SysError.ReturnValueRequired: return Error.RetObjectRequired;
        case SysError.SealedTypeIsAlreadyInvariant: return Error.SealedTypeIsAlreadyInvariant;
        case SysError.SourceFileNotRead: return Error.SourceFileNotRead;
        case SysError.SizeofUnsafe: return Error.SizeofUnsafe;
        case SysError.StackallocInCatchFinally: return Error.StackallocInCatchFinally;
        case SysError.StaticNotVirtual: return Error.StaticNotVirtual;
        case SysError.TemplateTypeRequiresArgs: return Error.BadArity2;
        case SysError.ThisInBadContext: return Error.ThisInBadContext;
        case SysError.ThisInStaticCode: return Error.ThisInStaticMeth;
        case SysError.ThisReferenceFromFieldInitializer: return Error.FieldInitRefNonstatic;
        case SysError.ThrowsEnsuresOnConstructor: return Error.ThrowsEnsuresOnConstructor;
        case SysError.TooManyArgumentsToAttribute: return Error.TooManyArgumentsToAttribute;
        case SysError.TypeAliasUsedAsNamespacePrefix: return Error.ColColWithTypeAlias;
        case SysError.TypeArgsNotAllowed: return Error.TypeArgsNotAllowed;
        case SysError.TypeInBadContext: return Error.BadSKunknown;
        case SysError.TypeInVariableContext: return Error.BadSKknown;
        case SysError.TypeMissingInShadowedAssembly: return Error.TypeMissingInShadowedAssembly;
        case SysError.MethodMissingInShadowedAssembly: return Error.MethodMissingInShadowedAssembly;
        case SysError.TypeNameRequired: return Error.ObjectProhibited;
        case SysError.TypeNotAccessible: return Error.BadAccess;
        case SysError.TypeParameterNotCompatibleWithConstraint: return Error.GenericConstraintNotSatisfied;
        case SysError.TypeVarCantBeNull: return Error.TypeVarCantBeNull;
        case SysError.UnassignedThis: return Error.UnassignedThis;
        case SysError.UnimplementedAbstractMethod: return Error.UnimplementedAbstractMethod;
        case SysError.UnimplementedInterfaceMember: return Error.UnimplementedInterfaceMember;
        case SysError.UnreachableCatch: return Error.UnreachableCatch;
        case SysError.UnreachableCode: return Error.UnreachableCode;
        case SysError.UnreferencedLabel: return Error.UnreferencedLabel;
        case SysError.UselessComparisonWithIntegerLiteral: return Error.VacuousIntegralComp;
        case SysError.UseOfNullPointer: return Error.UseOfNullPointer;
        case SysError.UseOfPossiblyNullPointer: return Error.UseOfPossiblyNullPointer;
        case SysError.UseSwitchInsteadOfAttribute: return Error.UseSwitchInsteadOfAttribute;
        case SysError.ValConstraintNotSatisfied: return Error.ValConstraintNotSatisfied;
        case SysError.ValueTypeLayoutCycle: return Error.StructLayoutCycle;
        case SysError.ValueTypeIsAlreadyInvariant: return Error.ValueTypeIsAlreadyInvariant;
        case SysError.ValueTypeIsAlreadyNonNull: return Error.ValueTypeIsAlreadyNonNull;
        case SysError.VoidError: return Error.VoidError;
        case SysError.VolatileAndReadonly: return Error.VolatileAndReadonly;
        case SysError.VolatileByRef: return Error.VolatileByRef;
        case SysError.VolatileNonWordSize: return Error.VolatileStruct;
        case SysError.Warning: return Error.Warning;
        case SysError.Win32ResourceFileNotRead: return Error.Win32ResourceFileNotRead;
        case SysError.Win32IconFileNotRead: return Error.Win32IconFileNotRead;
        case SysError.WrongKindOfMember: return Error.BadSKknown;
        case SysError.WrongNumberOfArgumentsForDelegate: return Error.BadDelArgCount;
        case SysError.WrongNumberOfIndices: return Error.BadIndexCount;
        case SysError.WrongReturnTypeForIterator: return Error.BadIteratorReturn;

        case SysError.UseDefViolation: return Error.UseDefViolation;
        case SysError.UseDefViolationOut: return Error.UseDefViolationOut;
        case SysError.UseDefViolationField: return Error.UseDefViolationField;
        case SysError.UseDefViolationThis: return Error.UseDefViolationThis;
        case SysError.ReturnExpected: return Error.ReturnExpected;
        case SysError.ParamUnassigned: return Error.ParamUnassigned;
        case SysError.UnreferencedVar: return Error.UnreferencedVar;
        case SysError.UnreferencedVarAssg: return Error.UnreferencedVarAssg;
        case SysError.StoreIntoLessDelayedLocation: return Error.StoreIntoLessDelayedLocation;

          // MB -- 09/11/2004
        case SysError.CannotCoerceNullToNonNullType: return Error.CannotCoerceNullToNonNullType;
        case SysError.CoercionToNonNullTypeMightFail: return Error.CoercionToNonNullTypeMightFail;
        case SysError.ReceiverMightBeNull: return Error.ReceiverMightBeNull;
        case SysError.UnnecessaryNonNullCoercion: return Error.UnnecessaryNonNullCoercion;
        case SysError.OnlyStructsAndClassesCanHaveInvariants: return Error.OnlyStructsAndClassesCanHaveInvariants;
        case SysError.UpToMustBeSuperType: return Error.UpToMustBeSuperType;
        case SysError.UpToMustBeClass: return Error.UpToMustBeClass;
        case SysError.ExpectedLeftParenthesis: return Error.ExpectedLeftParenthesis;
        case SysError.MustSupportComprehension: return Error.MustSupportComprehension;
        case SysError.MustSupportReductionXXXXXXXXXXXX: return Error.MustSupportReductionXXXXXXXXXXXX;
        case SysError.MustResolveToType: return Error.MustResolveToType;
        case SysError.MemberMustBePureForMethodContract: return Error.MemberMustBePureForMethodContract;
        case SysError.RequiresNotAllowedInOverride: return Error.RequiresNotAllowedInOverride;
        case SysError.ContractNotAllowedInExplicitInterfaceImplementation: return Error.ContractNotAllowedInExplicitInterfaceImplementation;
        case SysError.CannotAddThrowsSet: return Error.CannotAddThrowsSet;
        case SysError.CannotWeakenThrowsSet: return Error.CannotWeakenThrowsSet;
        case SysError.DuplicateThrowsType: return Error.DuplicateThrowsType;
        case SysError.UncheckedExceptionInThrowsClause: return Error.UncheckedExceptionInThrowsClause;
        case SysError.RequiresNotAllowedInInterfaceImplementation: return Error.RequiresNotAllowedInInterfaceImplementation;
        case SysError.EnsuresInInterfaceNotInMethod: return Error.EnsuresInInterfaceNotInMethod;
        case SysError.ModelMemberUseNotAllowedInContext: return Error.ModelMemberUseNotAllowedInContext;
        case SysError.MemberMustBePureForInvariant: return Error.MemberMustBePureForInvariant;
        case SysError.TypeMustSupportIntCoercions: return Error.TypeMustSupportIntCoercions;
        case SysError.CannotInjectContractFromInterface: return Error.CannotInjectContractFromInterface;
        case SysError.CheckedExceptionInRequiresOtherwiseClause: return Error.CheckedExceptionInRequiresOtherwiseClause;
        case SysError.ContractInheritanceRulesViolated: return Error.ContractInheritanceRulesViolated;
        case SysError.ModifiesNotAllowedInOverride: return Error.ModifiesNotAllowedInOverride;
        case SysError.GenericError: return Error.GenericError;

        case SysError.ReceiverCannotBeNull: return Error.ReceiverCannotBeNull;
        case SysError.OtherwiseExpressionMustBeNonNull : return Error.OtherwiseExpressionMustBeNonNull;
        case SysError.OtherwiseExpressionMustBeType : return Error.OtherwiseExpressionMustBeType;

        case SysError.StrictReadonlyNotReadonly: return Error.StrictReadonlyNotReadonly;
        case SysError.StrictReadonlyStatic: return Error.StrictReadonlyStatic;
        case SysError.StrictReadonlyAssignment: return Error.StrictReadonlyAssignment;
        case SysError.StrictReadonlyMultipleAssignment: return Error.StrictReadonlyMultipleAssignment;
        case SysError.ShouldCommit: return Error.ShouldCommit;
        case SysError.ShouldCommitOnAllPaths: return Error.ShouldCommitOnAllPaths;
        case SysError.UnboxDelayedValue: return Error.UnboxDelayedValue;
        case SysError.ThrowsDelayedValue: return Error.ThrowsDelayedValue;
        case SysError.CannotUseDelayedPointer: return Error.CannotUseDelayedPointer;
        case SysError.CannotUseDelayedTypedRef: return Error.CannotUseDelayedTypedRef;

        case SysError.InvalidModifiesClause: return Error.InvalidModifiesClause;
        case SysError.PureMethodWithOutParamUsedInContract: return Error.PureMethodWithOutParamUsedInContract;
        case SysError.PureMethodCannotHaveRefParam: return Error.PureMethodCannotHaveRefParam;
        case SysError.ReadsWithoutPure: return Error.ReadsWithoutPure;
        case SysError.InconsistentPurityAttributes: return Error.InconsistentPurityAttributes;
        case SysError.PureOwnedNotAllowed: return Error.PureOwnedNotAllowed;

      }
      return Error.UnexpectedToken;
    }    
    public override string GetIndexerName(Method meth){
      if (meth == null) return "";
      string methName = "this";
      if (meth.ImplementedTypes != null && meth.ImplementedTypes.Count > 0 && meth.ImplementedTypes[0] != null)
        methName = this.GetTypeName(meth.ImplementedTypes[0])+"."+methName;
      string decTypeName = this.GetTypeName(meth.DeclaringType);
      if (decTypeName == null || decTypeName.Length == 0) return methName;
      return decTypeName+"."+methName;
    }
    public override string GetMemberAccessString(Member mem){
      if (mem == null || mem is Namespace) return "";
      if (mem.IsAssembly) return "internal";
      if (mem.IsFamily) return "protected";
      if (mem.IsFamilyAndAssembly) return "protected and internal";
      if (mem.IsFamilyOrAssembly) return "protected internal";
      if (mem.IsPrivate) return "private";
      if (mem.IsPublic) return "public";
      if (mem.IsCompilerControlled){
        if (mem is Field && mem.DeclaringType is BlockScope) return "(local variable)";
        if (mem is ParameterField) return "(parameter)";
        return null;
      }
      return "";
    }
    public override string GetMemberName(Member mem){
      if (mem == null) return "";
      string memName = mem.Name == null ? "" : mem.Name.ToString();
      switch (mem.NodeType){
        case NodeType.Property:
          Property prop = (Property)mem;
          ParameterList pars = prop.Parameters;
          if (pars != null && pars.Count > 0)
            return this.GetTypeName(mem.DeclaringType)+".this";
          if (prop.ImplementedTypes != null && prop.ImplementedTypes.Count > 0 && prop.ImplementedTypes[0] != null)
            memName = this.GetTypeName(prop.ImplementedTypes[0])+"."+memName;
          goto default;
        case NodeType.Method:
          Method meth = (Method)mem;
          if (meth.IsSpecialName){
            string opName = meth.Name == null ? null : (string)Cci.Checker.OperatorName[meth.Name.UniqueIdKey];
            if (opName != null){
              string name = this.GetTypeName(meth.DeclaringType)+"."+opName;
              if (meth.Name.UniqueIdKey == StandardIds.opExplicit.UniqueIdKey || meth.Name.UniqueIdKey == StandardIds.opImplicit.UniqueIdKey)
                name = name + " "+this.GetTypeName(meth.ReturnType);
              return name;
            }
          }
          if (meth.HasCompilerGeneratedSignature){
            if (meth.Name.UniqueIdKey == StandardIds.Finalize.UniqueIdKey)
              return this.GetTypeName(meth.DeclaringType) + ".~" + meth.DeclaringType.Name;
            if (meth.Name.UniqueIdKey == StandardIds.Invoke.UniqueIdKey)
              return this.GetTypeName(meth.DeclaringType);
          }
          if (meth.ImplementedTypes != null && meth.ImplementedTypes.Count > 0 && meth.ImplementedTypes[0] != null)
            memName = this.GetTypeName(meth.ImplementedTypes[0])+"."+memName;
          if (meth.TemplateParameters != null && meth.TemplateParameters.Count > 0){
            StringBuilder sb = new StringBuilder(memName);
            sb.Append('<');
            for (int i = 0, n = meth.TemplateParameters == null ? 0 : meth.TemplateParameters.Count; i < n; i++){
              sb.Append(ErrorHandler.GetTypeNameFor(meth.TemplateParameters[i]));
              if (i < n-1) sb.Append(',');
            }
            sb.Append('>');
            memName = sb.ToString();
          }else if (meth.Template != null && meth.TemplateArguments != null && meth.TemplateArguments.Count > 0){
            StringBuilder sb = new StringBuilder(meth.Template.Name == null ? memName : meth.Template.Name.Name);
            sb.Append('<');
            for (int i = 0, n = meth.TemplateArguments == null ? 0 : meth.TemplateArguments.Count; i < n; i++) {
              sb.Append(ErrorHandler.GetTypeNameFor(meth.TemplateArguments[i]));
              if (i < n-1) sb.Append(',');
            }
            sb.Append('>');
            memName = sb.ToString();
          }
          goto default;
        case NodeType.InstanceInitializer:
          return this.GetTypeName(mem.DeclaringType) + "." + mem.DeclaringType.GetUnmangledNameWithoutTypeParameters();
        case NodeType.Namespace:
          return ((Namespace)mem).FullName;
        default:
          TypeNode typ = mem as TypeNode;
          if (typ != null) return this.GetTypeName(typ);
          if (mem.DeclaringType != null && !mem.DeclaringType.IsStructural){
            string decTypeName = ErrorHandler.GetTypeNameFor(mem.DeclaringType);
            if (decTypeName == null || decTypeName.Length == 0) return memName;
            return decTypeName+"."+memName;
          }else if (mem.DeclaringType != null && mem.DeclaringType.Template != null){
            string decTypeName = ErrorHandler.GetTypeNameFor(mem.DeclaringType);
            if (mem.DeclaringType.Template == SystemTypes.GenericNullable && 
            mem.DeclaringType.TemplateArguments != null && mem.DeclaringType.TemplateArguments.Count == 1){
              decTypeName = "System.Nullable<"+ErrorHandler.GetTypeNameFor(mem.DeclaringType.TemplateArguments[0])+">";
            }
            if (decTypeName == null || decTypeName.Length == 0) return memName;
            return decTypeName+"."+memName;
          }
          return memName;
      }
    }
    public override string GetUnqualifiedMemberName(Member mem){
      if (mem == null) return "";
      string memName = mem.Name == null ? "" : mem.Name.ToString();
      switch (mem.NodeType){
        case NodeType.Property:
          Property prop = (Property)mem;
          ParameterList pars = prop.Parameters;
          if (pars != null && pars.Count > 0)
            return "this";
          if (prop.ImplementedTypes != null && prop.ImplementedTypes.Count > 0 && prop.ImplementedTypes[0] != null)
            memName = this.GetTypeName(prop.ImplementedTypes[0])+"."+memName;
          goto default;
        case NodeType.Method:
          Method meth = (Method)mem;
          if (meth.IsSpecialName){
            string opName = (string)Cci.Checker.OperatorName[meth.Name.UniqueIdKey];
            if (opName != null){
              string name = opName;
              if (meth.Name.UniqueIdKey == StandardIds.opExplicit.UniqueIdKey || meth.Name.UniqueIdKey == StandardIds.opImplicit.UniqueIdKey)
                name = name + " "+this.GetTypeName(meth.ReturnType);
              return name;
            }
          }
          if (meth.HasCompilerGeneratedSignature && meth.Name.UniqueIdKey == StandardIds.Finalize.UniqueIdKey)
            return "~" + meth.DeclaringType.Name;
          if (meth.ImplementedTypes != null && meth.ImplementedTypes.Count > 0 && meth.ImplementedTypes[0] != null)
            return this.GetTypeName(meth.ImplementedTypes[0])+"."+meth.GetUnmangledNameWithTypeParameters(true);
          return meth.GetUnmangledNameWithTypeParameters(true);
        case NodeType.InstanceInitializer:
        case NodeType.StaticInitializer:
          return mem.DeclaringType.Name.ToString();
        default:
          TypeNode typ = mem as TypeNode;
          if (typ != null) return this.GetTypeName(typ);
          return memName;
      }
    }
    public override string GetMethodSignature(Method method, bool noAccessor, bool withFullInformation){
      if (method == null) return "";
      if (method is InstanceInitializer && withFullInformation){
        if (method.DeclaringType is DelegateNode) return this.GetDelegateConstructorSignature(method);
        return this.GetSignatureString(this.GetMemberName(method.DeclaringType), method.Parameters, "(", ")", ", ", withFullInformation);
      }
      if (method.IsSpecialName){
        string methName = method.Name == null ? "" : method.Name.ToString();
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
        if (method.ImplementedTypes != null && method.ImplementedTypes.Count > 0 && method.ImplementedTypes[0] != null)
          methName = this.GetTypeName(method.ImplementedTypes[0])+"."+methName;
        string decTypeName = this.GetTypeName(method.DeclaringType);
        if (decTypeName == null || decTypeName.Length == 0) return methName;
        return decTypeName+"."+methName;
      }
    nonAccessorCase:
      return this.GetSignatureString(this.GetMemberName(method), method.Parameters, "(", ")", ", ", withFullInformation);
    }
    public virtual string GetDelegateConstructorSignature(Method method){
      if (method == null) return "";
      DelegateNode del = method.DeclaringType as DelegateNode;
      if (del == null) return "";
      StringBuilder sb = new StringBuilder();
      sb.Append(this.GetMemberName(del));
      sb.Append('(');
      sb.Append(this.GetMemberName(del.ReturnType));
      sb.Append(this.GetSignatureString(" ", del.Parameters, "(", ")", ", ", false));
      sb.Append(") target)");
      return sb.ToString();
    }
    public override void HandleDirectCallOfFinalize(MethodCall call){
      if (call == null) return;
      if (call.SourceContext.Document != null){
        if (call.Callee != null && call.Callee is MemberBinding && ((MemberBinding)call.Callee).TargetObject is Base)
          this.HandleError(call, Error.CallingBaseFinalizeDeprecated);
        else
          this.HandleError(call, Error.CallingFinalizeDeprecated);
      }
    }
    public override void HandleNonOverrideAndNonHide(Method meth, Member bmem, Method bmeth){
      if (meth.Name.UniqueIdKey == StandardIds.Finalize.UniqueIdKey && bmem.DeclaringType == SystemTypes.Object){
        meth.HidesBaseClassMember = true;
        return;
      }
      base.HandleNonOverrideAndNonHide(meth, bmem, bmeth);
    }
    public override void HandleOverrideOfObjectFinalize(Method method){
      this.HandleError(method.Name, Error.OverrideFinalizeDeprecated);
    }
    internal void HandleError(Node offendingNode, Error error, params string[] messageParameters){
      if (offendingNode == null || (offendingNode is Literal && offendingNode.SourceContext.Document == null)) return;
      PragmaInfo pragmaInfo = null;
      PragmaInfo matchingPragmaInfo = null;
      if (this.PragmaWarnInformation != null) {
        pragmaInfo = (PragmaInfo)this.PragmaWarnInformation[(int)error];
        if (pragmaInfo != null) {
          int lineNum = offendingNode.SourceContext.StartLine;          
          while (pragmaInfo != null) {
            if (pragmaInfo.LineNum < lineNum) {
              matchingPragmaInfo = pragmaInfo;
              break;
            }
            pragmaInfo = pragmaInfo.Next;
          }
          if (matchingPragmaInfo != null && matchingPragmaInfo.Disable)
            return;
        }
      }
      SpecSharpErrorNode enode = new SpecSharpErrorNode(error, messageParameters);
      if (pragmaInfo != null && !pragmaInfo.Disable && pragmaInfo == matchingPragmaInfo)
        enode.DoNotSuppress = true;
      enode.SourceContext = offendingNode.SourceContext;
      this.Errors.Add(enode);
    }
    public override void HandleError(Node offendingNode, SysError error, params string[] messageParameters){
      Error e = ErrorHandler.MapError(error);
      if (e == Error.None) return;
      if (e == Error.UnexpectedToken){
        base.HandleError(offendingNode, error, messageParameters);
        return;
      }
      if (e == Error.NameNotInContext){
        if (messageParameters != null && messageParameters.Length == 1){
          string[] mpars = new string[2];
          mpars[0] = messageParameters[0];
          if (this.currentType != null)
            mpars[1] = this.GetTypeName(this.currentType);
          else
            mpars[1] = "";
          messageParameters = mpars;
        }
      }
      if (e == Error.AssgReadonlyLocalCause){
        string[] mpars = new string[2];
        mpars[0] = messageParameters[0];
        if (error == SysError.AssignmentToFixedVariable)
          mpars[1] = "fixed variable"; //TODO: this string should be localized
        else
          mpars[1] = "";
        messageParameters = mpars;
      }
      if (this.refOrOutAddress){
        switch (e){
          case Error.AssgLvalueExpected: e = Error.RefLvalueExpected; break;
          case Error.AssgReadonly: e = Error.RefReadonly; break;
          case Error.AssgReadonlyLocal: e = Error.RefReadonlyLocal; break;
          case Error.AssgReadonlyLocalCause: e = Error.RefReadonlyLocalCause; break;
          case Error.AssgReadonlyProp: e = Error.RefReadonlyProperty; break;
          case Error.AssgReadonlyStatic: e = Error.RefReadonlyStatic; break;
        }
      }
      this.HandleError(offendingNode, e, messageParameters);
    }
    public override string GetDelegateSignature(DelegateNode del){
      if (del == null) return "";
      string delName = del.Name.ToString();
      if (del.DeclaringType != null)
        delName = this.GetTypeName(del.DeclaringType)+"."+delName;
      return this.GetTypeName(del.ReturnType)+" "+this.GetSignatureString(delName, del.Parameters, "(", ")", ", ");
    }    
    public override string GetTypeName(TypeNode type){
      if (type is Reference && this.currentParameter != null && (this.currentParameter.Flags & ParameterFlags.Out) != 0)
        return "out "+ErrorHandler.GetTypeNameFor(((Reference)type).ElementType);
      return ErrorHandler.GetTypeNameFor(type);
    }
    public override string GetUnqualifiedTypeName(TypeNode type){
      if (type is Reference && this.currentParameter != null && (this.currentParameter.Flags & ParameterFlags.Out) != 0)
        return "out "+ErrorHandler.GetTypeNameFor(((Reference)type).ElementType, false, false);
      return ErrorHandler.GetTypeNameFor(type, false, false);
    }
    public override string GetParameterSignature(ParameterField f){
      if (f == null) return "";
      StringBuilder result = new StringBuilder("(parameter) "); //REVIEW: localize the (parameter) part?
      this.currentParameter = f.Parameter;
      result.Append(this.GetTypeName(f.Type)+" "+f.Name.ToString()); 
      return result.ToString();
    }

    private static Identifier elementNameId = Identifier.For("ElementName");
    public static string GetTypeNameFor(TypeNode type){
      return ErrorHandler.GetTypeNameFor(type, false, true);
    }
    public static string GetTypeNameFor(TypeNode type, bool suppressTemplateParameters, bool fullName){
      if (type == null || type.Name == Looker.NotFound) return "";
      switch(type.TypeCode){
        case TypeCode.Boolean: return "bool";
        case TypeCode.Byte: return "byte";
        case TypeCode.Char: return "char";
        case TypeCode.Decimal: return "decimal";
        case TypeCode.Double: return "double";
        case TypeCode.Int16: return "short";
        case TypeCode.Int32: return "int";
        case TypeCode.Int64: return "long";
        case TypeCode.SByte: return "sbyte";
        case TypeCode.Single: return "float";
        case TypeCode.String: return "string";
        case TypeCode.UInt16: return "ushort";
        case TypeCode.UInt32: return "uint";
        case TypeCode.UInt64: return "ulong";
      }
      if (type == SystemTypes.Object) return "object";
      if (type == SystemTypes.Void) return "void";
      if (type.Template == SystemTypes.GenericNullable){
        if (type.TemplateArguments != null && type.TemplateArguments.Count > 0)
          return ErrorHandler.GetTypeNameFor(type.TemplateArguments[0]) + "?";
      }
      switch (type.NodeType){
        case NodeType.ArrayType:
          ArrayType aType = (ArrayType)type;
          StringBuilder sb = new StringBuilder(ErrorHandler.GetTypeNameFor(aType.ElementType));
          sb.Append('[');
          for (int i = 0, n = aType.Rank; i < n; i++){
            if (i == 0 && n > 1) sb.Append('*');
            if (i < n-1){
              sb.Append(',');
              if (n > 1) sb.Append('*');
            }
          }
          sb.Append(']');
          return sb.ToString();
        case NodeType.Class:
          FunctionType fType = type as FunctionType;
          if (fType != null){
            ErrorHandler eh = new ErrorHandler(new ErrorNodeList(0));
            return "delegate " + eh.GetTypeName(fType.ReturnType)+" "+eh.GetSignatureString("", fType.Parameters, "(", ")", ", ");
          }
          ClosureClass cClass = type as ClosureClass;
          if (cClass != null){
            MemberList mems = cClass.Members;
            for (int i = 0, n = mems == null ? 0 : mems.Count; i < n; i++){
              Method meth = mems[i] as Method;
              if (meth == null || meth is InstanceInitializer || (meth.Parameters != null && meth.Parameters.Count != 0)) continue;
              return ErrorHandler.GetTypeNameFor(meth.ReturnType);
            }
          }
          if (type.Template != null && type.TemplateArguments != null && type.TemplateArguments.Count > 0){
            sb = new StringBuilder(ErrorHandler.GetTypeNameFor(type.Template, true, fullName));
            for (int i = 0, n = type.TemplateArguments == null ? 0 : type.TemplateArguments.Count; i < n; i++){
              if (i == 0) sb.Append('<');
              sb.Append(ErrorHandler.GetTypeNameFor(type.TemplateArguments[i]));
              if (i < n - 1) sb.Append(','); else sb.Append('>');
            }
            return sb.ToString();
          }
          if (type.DeclaringType != null)
            sb = new StringBuilder(ErrorHandler.GetTypeNameFor(type.DeclaringType)+"."+type.GetUnmangledNameWithoutTypeParameters());
          else
            sb = new StringBuilder(type.GetFullUnmangledNameWithoutTypeParameters());
          if (!suppressTemplateParameters && type.TemplateParameters != null){
            for (int i = 0, n = type.TemplateParameters == null ? 0 : type.TemplateParameters.Count; i < n; i++){
              if (i == 0) sb.Append('<');
              sb.Append(ErrorHandler.GetTypeNameFor(type.TemplateParameters[i], false, fullName));
              if (i < n - 1) sb.Append(','); else sb.Append('>');
            }
            return sb.ToString();
          }
          return sb.ToString();
        case NodeType.ConstrainedType:
        case NodeType.DelegateNode:
        case NodeType.EnumNode:
        case NodeType.Interface:
        case NodeType.TypeAlias:
          goto case NodeType.Class;
        case NodeType.OptionalModifier:
          if (((OptionalModifier)type).Modifier == SystemTypes.NonNullType)
            return ErrorHandler.GetTypeNameFor(((OptionalModifier)type).ModifiedType)+"!";
          if (((OptionalModifier)type).Modifier == SystemTypes.NullableType)
            return ErrorHandler.GetTypeNameFor(((OptionalModifier)type).ModifiedType) + "?";
          goto case NodeType.RequiredModifier;
        case NodeType.RequiredModifier:
          return ErrorHandler.GetTypeNameFor(((TypeModifier)type).ModifiedType);
        case NodeType.Pointer:
          return ErrorHandler.GetTypeNameFor(((Pointer)type).ElementType)+"*";
        case NodeType.Reference:
          return "ref "+ErrorHandler.GetTypeNameFor(((Reference)type).ElementType);
        case NodeType.Refanytype:
        case NodeType.Struct:
          goto case NodeType.Class;
        case NodeType.ClassParameter:
        case NodeType.TypeParameter:
          return type.Name.ToString();
        case NodeType.ClassExpression : 
        case NodeType.InterfaceExpression : 
        case NodeType.TypeExpression : 
        case NodeType.ArrayTypeExpression:
        case NodeType.FlexArrayTypeExpression:
        case NodeType.FunctionTypeExpression:
        case NodeType.PointerTypeExpression:
        case NodeType.ReferenceTypeExpression:
        case NodeType.StreamTypeExpression:
        case NodeType.NonEmptyStreamTypeExpression:
        case NodeType.NonNullTypeExpression:
        case NodeType.NonNullableTypeExpression:
        case NodeType.BoxedTypeExpression:
        case NodeType.NullableTypeExpression:
        case NodeType.InvariantTypeExpression:
        case NodeType.TupleTypeExpression:
        case NodeType.TypeIntersectionExpression:
        case NodeType.TypeUnionExpression:
          return type.SourceContext.SourceText;
      }
      if (fullName) return type.FullName;
      if (type.Name == null) return "";
      return type.Name.ToString();
    }
    public static string[] GetSortedTypeNames(TypeNodeList types){
      int n = types == null ? 0 : types.Count;
      string[] result = new string[n];
      for (int i = 0; i < n; i++)
        result[i] = ErrorHandler.GetTypeNameFor(types[i]);
      Array.Sort(result);
      return result;
    }
  }

  //  Over time move to unified way of naming.
  //  Use this class from other places by setting proper options.
  enum MemberNameOptions: uint{
    Namespace = 0x00000001,
    EnclosingType = Namespace << 1,
    ImplementInterface = EnclosingType << 1,
    Keywords = ImplementInterface << 1,  //  takes highest precedence
    AtPrefix = Keywords << 1, //  TODO..
    TemplateParameters = AtPrefix << 1, //  List<T>
    TemplateArguments = TemplateParameters << 1,  //  Foo<int, List<K>>
    TemplateInfo = TemplateArguments << 1,  //  just <> not <T,U>
    ExpandNullable = TemplateInfo << 1, //  System.Nullable<int> not int?
    SmartNamespaceName = ExpandNullable << 1,  //  TODO: This should be honored for the Enclosing type also...
    SupressAttributeSuffix = SmartNamespaceName << 1,
    PutSignature = SupressAttributeSuffix << 1, //  Parameter info etc for methos and properties...
    PutParameterName = PutSignature << 1,
    PutParameterModifiers = PutParameterName << 1,
    PutReturnType = PutParameterModifiers << 1,
    Access = PutReturnType << 1,
    Modifiers = Access << 1,
    PutMethodConstraints = Modifiers << 1,
    SmartClassName = PutMethodConstraints << 1,
  }
  class MemberNameBuilder {
    internal static bool IsOptionActive(MemberNameOptions givenOpt, MemberNameOptions opt) {
      return (givenOpt & opt) == opt;
    }
    internal static string GetAtPrefixedIfRequired(string name, MemberNameOptions opt) {
      if (name == null)
        return name;
      if ((opt & MemberNameOptions.AtPrefix) == MemberNameOptions.AtPrefix && Scanner.IsKeyword(name, false)) {
        return "@" + name;
      }
      return name;
    }
    internal static bool IsNamespaceImportedByScope(Identifier nspName, Scope scope) {
      if (scope == null) return false;
      if (nspName == null) return true;
      while (scope != null && !(scope is NamespaceScope)) {
        scope = scope.OuterScope;
      }
      for (NamespaceScope nsScope = scope as NamespaceScope; nsScope != null; nsScope = nsScope.OuterScope as NamespaceScope) {
        Namespace ns = nsScope.AssociatedNamespace;
        if (ns != null) {
          if (ns.Name == null) continue;
          if (ns.Name.UniqueIdKey == nspName.UniqueIdKey) return true;
          UsedNamespaceList nsList = ns.UsedNamespaces;
          if (nsList == null || nsList.Count == 0) continue;
          for (int i = 0; i < nsList.Count; ++i) {
            if (nsList[i] == null) continue;
            if (nsList[i].Namespace == null) continue;
            if (nsList[i].Namespace.UniqueIdKey == nspName.UniqueIdKey) {
              return true;
            }
          }
        }
      }
      return false;
    }
    internal static bool IsAggregateVisibleIn(TypeNode typeNode, Scope scope) {
      if (scope == null) return false;
      if (typeNode == null) return true;
      while (scope != null && !(scope is TypeScope)) {
        scope = scope.OuterScope;
      }
      for (TypeScope tScope = scope as TypeScope; tScope != null; tScope = tScope.OuterScope as TypeScope) {
        if (tScope.Type == null) continue;
        if (typeNode == tScope.Type || tScope.Type.IsDerivedFrom(typeNode)) {
          return true;
        }
      }
      return false;
    }
    internal static string GetDelegateConstructorSignature(Method method, MemberNameOptions givenOptions, Scope scope) {
      DelegateNode del = method.DeclaringType as DelegateNode;
      if (del == null) return "";
      StringBuilder sb = new StringBuilder();
      sb.Append(MemberNameBuilder.GetMemberNameRaw(del, givenOptions, scope));
      MemberNameOptions mask = MemberNameOptions.Keywords | MemberNameOptions.TemplateInfo | MemberNameOptions.TemplateArguments | MemberNameOptions.TemplateParameters | MemberNameOptions.Namespace | MemberNameOptions.EnclosingType;
      string methName = MemberNameBuilder.GetMemberNameRaw(method.DeclaringType, givenOptions & ~mask, scope);
      sb.Append('.');
      sb.Append(methName);
      sb.Append('(');
      sb.Append(MemberNameBuilder.GetMemberNameRaw(del.ReturnType, givenOptions, scope));
      sb.Append(MemberNameBuilder.GetSignatureString(del.Parameters, "(", ")", ", ", givenOptions&~MemberNameOptions.PutParameterName, scope, false));
      sb.Append(") target)");
      return sb.ToString();
    }
    internal static string GetMemberAccessString(Member mem) {
      if (mem == null || mem is Namespace) return "";
      if (mem.IsAssembly) return "internal";
      if (mem.IsFamily) return "protected";
      if (mem.IsFamilyAndAssembly) return "protected and internal";
      if (mem.IsFamilyOrAssembly) return "protected internal";
      if (mem.IsPrivate) return "private";
      if (mem.IsPublic) return "public";
      if (mem.IsCompilerControlled) {
        if (mem is Field && mem.DeclaringType is BlockScope) return "(local variable)";
        if (mem is ParameterField) return "(parameter)";
        return null;
      }
      return "";
    }
    public static string GetParameterTypeName(Parameter parameter, MemberNameOptions givenOptions, Scope scope) {
      if (parameter == null) return "";
      Reference r = parameter.Type as Reference;
      TypeNode typeNode = null;
      StringBuilder sb = new StringBuilder();
      string backUpName = null;
      if (r != null) {
        if ((parameter.Flags & ParameterFlags.Out) != 0) {
          if ( IsOptionActive(givenOptions, MemberNameOptions.PutParameterModifiers) ) sb.Append("out ");
          typeNode = r.ElementType;
        } else {
          if (IsOptionActive(givenOptions, MemberNameOptions.PutParameterModifiers)) sb.Append("ref ");
          typeNode = r.ElementType;
        }
      } else if (parameter.GetParamArrayElementType() != null) {
        if (IsOptionActive(givenOptions, MemberNameOptions.PutParameterModifiers)) sb.Append("params ");
        typeNode = parameter.Type;
      } else {
        typeNode = parameter.Type;
        if (typeNode == null && parameter.TypeExpression != null)
            backUpName = parameter.TypeExpression.SourceContext.SourceText;
      }
      sb.Append(backUpName != null ? MemberNameBuilder.GetAtPrefixedIfRequired(backUpName, givenOptions) : GetMemberNameRaw(typeNode, givenOptions, scope));
      if (IsOptionActive(givenOptions, MemberNameOptions.PutParameterName) && parameter.Name!= null) {
        sb.Append(' ');
        sb.Append(MemberNameBuilder.GetAtPrefixedIfRequired(parameter.Name.ToString(), givenOptions));
      }
      return sb.ToString();
    }
    internal static string GetSignatureString(ParameterList parameters, string startPars, string endPars, string parSep, MemberNameOptions givenOpts, Scope scope, bool addNamedParameters) {
      StringBuilder sb = new StringBuilder(256);
      sb.Append(startPars);
      int n = parameters == null ? 0 : parameters.Count;
      for (int i = 0; i < n; i++) {
        Parameter p = parameters[i];
        sb.Append(GetParameterTypeName(p, givenOpts, scope));
        if (i < n - 1) sb.Append(parSep);
      }
      if (addNamedParameters) {
        string namedParamters = SpecSharpErrorNode.ResourceManager.GetString("NamedParameters", CultureInfo.CurrentCulture);
        if ( n > 0 ){
          sb.Append(parSep);
          sb.Append(namedParamters);
        }else{
          sb.Append(namedParamters);
        }
      }
      sb.Append(endPars);
      return sb.ToString();
    }
    internal static string GetMethodName(Method method, MemberNameOptions givenOptions, Scope scope, out bool isIndexer) {
      string methName = null;
      isIndexer = false;
      if ( method.Template != null)
        methName = MemberNameBuilder.GetAtPrefixedIfRequired(method.Template.GetUnmangledNameWithoutTypeParameters(true), givenOptions);
      else
        methName = MemberNameBuilder.GetAtPrefixedIfRequired(method.GetUnmangledNameWithoutTypeParameters(true), givenOptions);
      if (method is InstanceInitializer) {
        MemberNameOptions mask = MemberNameOptions.Keywords | MemberNameOptions.TemplateInfo | MemberNameOptions.TemplateArguments | MemberNameOptions.TemplateParameters | MemberNameOptions.Namespace | MemberNameOptions.EnclosingType;
        methName = MemberNameBuilder.GetMemberNameRaw(method.DeclaringType, givenOptions & ~mask, scope);
      } else if (method.IsSpecialName) {
        if (methName.StartsWith("get_") || methName.StartsWith("set_")) {
          if ((method.Parameters != null && method.Parameters.Count > 0)) {
            //  In this case enclosing type is not really an option. its part of name.
            methName = MemberNameBuilder.GetMemberNameRaw(method.DeclaringType, givenOptions, scope);
            if (methName == null)
              methName = "";
            isIndexer = true;
          }
          methName = methName.Substring(4);
        } else if (methName.StartsWith("add_") && method.Parameters != null && method.Parameters.Count == 1)
          methName = methName.Substring(4);
        else if (methName.StartsWith("remove_") && method.Parameters != null && method.Parameters.Count == 1)
          methName = methName.Substring(7);
        else {
          string opName = method.Name == null ? null : (string)Cci.Checker.OperatorName[method.Name.UniqueIdKey];
          if (opName != null) {
            //  In this case Enclosing type is not really an option. its part of name.
            string name = MemberNameBuilder.GetMemberNameRaw(method.DeclaringType, givenOptions, scope) + "." + opName;
            if (method.Name.UniqueIdKey == StandardIds.opExplicit.UniqueIdKey || method.Name.UniqueIdKey == StandardIds.opImplicit.UniqueIdKey)
              name = name + " " + MemberNameBuilder.GetMemberNameRaw(method.ReturnType, givenOptions, scope);
            return name;
          }
        }
      } else {
        if (method.HasCompilerGeneratedSignature) {
          if (method.Name.UniqueIdKey == StandardIds.Finalize.UniqueIdKey)
            methName = "~" + MemberNameBuilder.GetAtPrefixedIfRequired(method.DeclaringType.Name.Name, givenOptions);
        }
      }
      if (method.ImplementedTypes != null && method.ImplementedTypes.Count > 0 && method.ImplementedTypes[0] != null && IsOptionActive(givenOptions, MemberNameOptions.ImplementInterface))
        methName = MemberNameBuilder.GetMemberNameRaw(method.ImplementedTypes[0], givenOptions, scope) + "." + methName;
      if (IsOptionActive(givenOptions, MemberNameOptions.EnclosingType) && !isIndexer) {
        string decTypeName = null;
        if (!IsOptionActive(givenOptions, MemberNameOptions.SmartClassName) || !MemberNameBuilder.IsAggregateVisibleIn(method.DeclaringType, scope))
          decTypeName = MemberNameBuilder.GetMemberNameRaw(method.DeclaringType, givenOptions, scope); ;
        if (decTypeName != null)
          methName = decTypeName + "." + methName;
      }
      return methName;
    }
    static bool HasConstraints(TypeNode templateParameter, ITypeParameter iTypeParameter) {
      if (templateParameter == null || iTypeParameter == null || templateParameter.Name == null)
        return false;
      if ((iTypeParameter.TypeParameterFlags & TypeParameterFlags.SpecialConstraintMask) != TypeParameterFlags.NoSpecialConstraint)
        return true;
      if (templateParameter.BaseType != null && templateParameter.BaseType != SystemTypes.Object)
        return true;
      InterfaceList interfaceList = templateParameter.Interfaces;
      if (interfaceList != null && interfaceList.Count > 0)
        return true;
      return false;
    }
    internal static string GetMemberNameRaw(Member m, MemberNameOptions givenOptions, Scope scope) {
      TypeNode typeNode = m as TypeNode;
      if (typeNode != null) {
        if (typeNode.Name == Looker.NotFound) return "";
        if (IsOptionActive(givenOptions, MemberNameOptions.ExpandNullable) && typeNode.Template == SystemTypes.GenericNullable && typeNode.TemplateArguments != null
          && typeNode.TemplateArguments.Count > 0) {
          return MemberNameBuilder.GetMemberNameRaw(typeNode.TemplateArguments[0], givenOptions, scope) + "?";
        }
        if (IsOptionActive(givenOptions, MemberNameOptions.Keywords)) {
          switch (typeNode.TypeCode) {
            case TypeCode.Boolean: return "bool";
            case TypeCode.Byte: return "byte";
            case TypeCode.Char: return "char";
            case TypeCode.Decimal: return "decimal";
            case TypeCode.Double: return "double";
            case TypeCode.Int16: return "short";
            case TypeCode.Int32: return "int";
            case TypeCode.Int64: return "long";
            case TypeCode.SByte: return "sbyte";
            case TypeCode.Single: return "float";
            case TypeCode.String: return "string";
            case TypeCode.UInt16: return "ushort";
            case TypeCode.UInt32: return "uint";
            case TypeCode.UInt64: return "ulong";
          }
          if (typeNode == SystemTypes.Object) return "object";
          if (typeNode == SystemTypes.Void) return "void";
        }
        switch (typeNode.NodeType) {
          case NodeType.ArrayType: {
              ArrayType aType = (ArrayType)typeNode;
              StringBuilder sb = new StringBuilder(MemberNameBuilder.GetMemberNameRaw(aType.ElementType, givenOptions, scope));
              sb.Append('[');
              for (int i = 0, n = aType.Rank; i < n; i++) {
                if (i == 0 && n > 1) sb.Append('*');
                if (i < n - 1) {
                  sb.Append(',');
                  if (n > 1) sb.Append('*');
                }
              }
              sb.Append(']');
              return sb.ToString();
          }
          case NodeType.ConstrainedType:
          case NodeType.DelegateNode:
          case NodeType.EnumNode:
          case NodeType.Interface:
          case NodeType.TypeAlias:
          case NodeType.Class:{
            FunctionType fType = typeNode as FunctionType;
            if (fType != null) {
              return "delegate " + MemberNameBuilder.GetMemberNameRaw(fType.ReturnType, givenOptions, scope)
                + " " + MemberNameBuilder.GetSignatureString(fType.Parameters, "(", ")", ", ", givenOptions, scope, false);
            }
            ClosureClass cClass = typeNode as ClosureClass;
            if (cClass != null) {
              MemberList mems = cClass.Members;
              for (int i = 0, n = mems == null ? 0 : mems.Count; i < n; i++) {
                Method meth = mems[i] as Method;
                if (meth == null || meth is InstanceInitializer || (meth.Parameters != null && meth.Parameters.Count != 0)) continue;
                return MemberNameBuilder.GetMemberNameRaw(meth.ReturnType, givenOptions, scope);
              }
            }
            StringBuilder sb = new StringBuilder();
            if (IsOptionActive(givenOptions, MemberNameOptions.Namespace) && typeNode.Namespace != null) {
              string prefix = typeNode.Namespace.Name + ".";
              if (IsOptionActive(givenOptions, MemberNameOptions.SmartNamespaceName) && MemberNameBuilder.IsNamespaceImportedByScope(typeNode.Namespace, scope))
                prefix = "";
              sb.Append(MemberNameBuilder.GetAtPrefixedIfRequired(prefix, givenOptions));
            }
            if (IsOptionActive(givenOptions, MemberNameOptions.EnclosingType) && m.DeclaringType != null) {
              string prefix = "";
              if (!IsOptionActive(givenOptions, MemberNameOptions.SmartClassName) || !MemberNameBuilder.IsAggregateVisibleIn(m.DeclaringType, scope))
                prefix = MemberNameBuilder.GetMemberNameRaw(typeNode.DeclaringType, givenOptions, scope) + "."; ;
              sb.Append(MemberNameBuilder.GetAtPrefixedIfRequired(prefix, givenOptions));
            }
            string typeName = MemberNameBuilder.GetAtPrefixedIfRequired(typeNode.GetUnmangledNameWithoutTypeParameters(), givenOptions);
            if (IsOptionActive(givenOptions, MemberNameOptions.SupressAttributeSuffix) && typeName.EndsWith("Attribute")) {
              typeName = MemberNameBuilder.GetAtPrefixedIfRequired(typeName.Substring(0, typeName.Length - 9), givenOptions);
            }
            sb.Append(typeName);
            if (typeNode.TemplateParameters != null && typeNode.TemplateParameters.Count > 0) {
              if (IsOptionActive(givenOptions, MemberNameOptions.TemplateInfo))
                sb.Append("<>");
              else if(IsOptionActive(givenOptions, MemberNameOptions.TemplateParameters)){
                sb.Append('<');
                int n = typeNode.TemplateParameters.Count;
                for (int i = 0; i < n; i++) {
                  sb.Append(MemberNameBuilder.GetMemberNameRaw(typeNode.TemplateParameters[i], givenOptions, scope));
                  if (i < n - 1) sb.Append(',');
                }
                sb.Append('>');
              }
            } else if (typeNode.Template != null && typeNode.TemplateArguments != null && typeNode.TemplateArguments.Count > 0
              && IsOptionActive(givenOptions, MemberNameOptions.TemplateArguments)) {
              sb.Append('<');
              int n = typeNode.TemplateArguments.Count;
              for (int i = 0; i < n; i++) {
                sb.Append(MemberNameBuilder.GetMemberNameRaw(typeNode.TemplateArguments[i], givenOptions | MemberNameOptions.Keywords, scope));
                if (i < n - 1) sb.Append(',');
              }
              sb.Append('>');
            }
            return sb.ToString();
          }
          case NodeType.OptionalModifier:
            if (((OptionalModifier)typeNode).Modifier == SystemTypes.NonNullType)
              return MemberNameBuilder.GetMemberNameRaw(((OptionalModifier)typeNode).ModifiedType, givenOptions, scope) + "!";
            goto case NodeType.RequiredModifier;
          case NodeType.RequiredModifier:
            return MemberNameBuilder.GetMemberNameRaw(((TypeModifier)typeNode).ModifiedType, givenOptions, scope);
          case NodeType.Pointer:
            return MemberNameBuilder.GetMemberNameRaw(((Pointer)typeNode).ElementType, givenOptions, scope) + "*";
          case NodeType.Reference:
            return "ref " + MemberNameBuilder.GetMemberNameRaw(((Reference)typeNode).ElementType, givenOptions, scope);
          case NodeType.Refanytype:
          case NodeType.Struct:
            goto case NodeType.Class;
          case NodeType.ClassParameter:
          case NodeType.TypeParameter:
            return MemberNameBuilder.GetAtPrefixedIfRequired(typeNode.Name.ToString(), givenOptions);
          case NodeType.ClassExpression:
          case NodeType.InterfaceExpression:
          case NodeType.TypeExpression:
          case NodeType.ArrayTypeExpression:
          case NodeType.FlexArrayTypeExpression:
          case NodeType.FunctionTypeExpression:
          case NodeType.PointerTypeExpression:
          case NodeType.ReferenceTypeExpression:
          case NodeType.StreamTypeExpression:
          case NodeType.NonEmptyStreamTypeExpression:
          case NodeType.NonNullTypeExpression:
          case NodeType.NonNullableTypeExpression:
          case NodeType.BoxedTypeExpression:
          case NodeType.NullableTypeExpression:
          case NodeType.InvariantTypeExpression:
          case NodeType.TupleTypeExpression:
          case NodeType.TypeIntersectionExpression:
          case NodeType.TypeUnionExpression:
            return typeNode.SourceContext.SourceText;
        }
      }
      Method method = m as Method;
      if (method != null) {
        if (method is InstanceInitializer && method.DeclaringType is DelegateNode) return MemberNameBuilder.GetDelegateConstructorSignature(method, givenOptions, scope);
        StringBuilder sb = new StringBuilder();
        if (IsOptionActive(givenOptions, MemberNameOptions.PutReturnType) && !(method is InstanceInitializer)) {
          sb.Append(MemberNameBuilder.GetMemberNameRaw(method.ReturnType, givenOptions, scope));
          sb.Append(' ');
        }
        bool isIndexer;
        sb.Append(MemberNameBuilder.GetMethodName(method, givenOptions, scope, out isIndexer));
        if (method.TemplateParameters != null && method.TemplateParameters.Count > 0) {
          if (IsOptionActive(givenOptions, MemberNameOptions.TemplateInfo))
            sb.Append("<>");
          else if (IsOptionActive(givenOptions, MemberNameOptions.TemplateParameters)) {
            sb.Append('<');
            int n = method.TemplateParameters.Count;
            for (int i = 0; i < n; i++) {
              sb.Append(MemberNameBuilder.GetMemberNameRaw(method.TemplateParameters[i], givenOptions, scope));
              if (i < n - 1) sb.Append(',');
            }
            sb.Append('>');
          }
        } else if (method.Template != null && method.TemplateArguments != null && method.TemplateArguments.Count > 0
          && IsOptionActive(givenOptions, MemberNameOptions.TemplateArguments)) {
          sb.Append('<');
          int n = method.TemplateArguments.Count;
          for (int i = 0; i < n; i++) {
            sb.Append(MemberNameBuilder.GetMemberNameRaw(method.TemplateArguments[i], givenOptions | MemberNameOptions.Keywords, scope));
            if (i < n - 1) sb.Append(',');
          }
          sb.Append('>');
        }
        if (IsOptionActive(givenOptions, MemberNameOptions.PutSignature)) {
          InstanceInitializer ctor = method as InstanceInitializer;
          bool addNamedParameters = false;
          if (ctor != null) {
            MemberList ml = ctor.GetAttributeConstructorNamedParameters();
            addNamedParameters = ml != null && ml.Count > 0;
          }
          sb.Append(MemberNameBuilder.GetSignatureString(method.Parameters, isIndexer ? "[" : "(", isIndexer ? "]" : ")", ", ", givenOptions, scope, addNamedParameters));
        }
        if (IsOptionActive(givenOptions, MemberNameOptions.PutMethodConstraints) && method.TemplateParameters != null && method.TemplateParameters.Count > 0) {
          TypeNodeList templParameterList = method.TemplateParameters;
          int n = templParameterList.Count;
          for (int i = 0; i < n; i++) {
            TypeNode templParameter = templParameterList[i];
            ITypeParameter tpar = templParameter as ITypeParameter;
            if (!HasConstraints(templParameter, tpar) ) continue;
            sb.AppendFormat(" where {0} :", templParameter.Name.Name);
            bool isFirst = true;
            switch (tpar.TypeParameterFlags & TypeParameterFlags.SpecialConstraintMask) {
              case TypeParameterFlags.DefaultConstructorConstraint:
                sb.Append(" new()");
                isFirst = false;
                break;
              case TypeParameterFlags.ReferenceTypeConstraint:
                sb.Append(" class");
                isFirst = false;
                break;
              case TypeParameterFlags.ValueTypeConstraint:
                sb.Append(" struct");
                isFirst = false;
                break;
            }
            if (templParameter.BaseType != null && templParameter.BaseType != SystemTypes.Object) {
              if (isFirst)
                sb.Append(' ');
              else
                sb.Append(", ");
              sb.Append(MemberNameBuilder.GetMemberNameRaw(templParameterList[i].BaseType, givenOptions, scope));
              isFirst = false;
            }
            if (templParameter.Interfaces != null && templParameter.Interfaces.Count > 0) {
              InterfaceList interfaceList = templParameter.Interfaces;
              int n2 = interfaceList.Count;
              for (int j = 0; j < n2; ++j) {
                if (interfaceList[i] == null) continue;
                if (isFirst)
                  sb.Append(' ');
                else
                  sb.Append(", ");
                sb.Append(MemberNameBuilder.GetMemberNameRaw(interfaceList[i], givenOptions, scope));
                isFirst = false;
              }
            }
          }
        }
        return sb.ToString();
      }
      Property p = m as Property;
      if (p != null) {
        StringBuilder sb = new StringBuilder();
        if (IsOptionActive(givenOptions, MemberNameOptions.PutReturnType)) {
          sb.Append(MemberNameBuilder.GetMemberNameRaw(p.Type, givenOptions, scope));
          sb.Append(' ');
        }
        string name = null;
        bool isIndexer = false;
        if (p.DeclaringType.DefaultMembers.Contains(p)) {
          name = MemberNameBuilder.GetMemberNameRaw(p.DeclaringType, givenOptions, scope);
          isIndexer = true;
        } else
          name = MemberNameBuilder.GetAtPrefixedIfRequired(p.Name.ToString(), givenOptions);
          if (IsOptionActive(givenOptions, MemberNameOptions.EnclosingType) && !isIndexer) {
            string decTypeName = null;
            if (!IsOptionActive(givenOptions, MemberNameOptions.SmartClassName) || !MemberNameBuilder.IsAggregateVisibleIn(p.DeclaringType, scope))
              decTypeName = MemberNameBuilder.GetMemberNameRaw(p.DeclaringType, givenOptions, scope); ;
            if (decTypeName != null) {
              sb.Append(decTypeName);
              sb.Append(".");
            }
        }
        sb.Append(name);
        if (IsOptionActive(givenOptions, MemberNameOptions.PutSignature))
          sb.Append(MemberNameBuilder.GetSignatureString(p.Parameters, isIndexer ? "[" : "(", isIndexer ? "]" : ")", ", ", givenOptions, scope, false));
        return sb.ToString();
      }
      if (m==null || m.Name == null) return " ";
      return MemberNameBuilder.GetAtPrefixedIfRequired(m.Name.ToString(), givenOptions);
    }
    public static string GetMemberName(Member m, MemberNameOptions givenOptions, Scope scope) {
      if (m is KeywordCompletion)
        return m.Name.Name;
      StringBuilder sb = new StringBuilder();
      if (IsOptionActive(givenOptions, MemberNameOptions.Access)){
        sb.Append(MemberNameBuilder.GetMemberAccessString(m));
        sb.Append(' ');
      }
      if (IsOptionActive(givenOptions, MemberNameOptions.Modifiers)) {
        if (m.IsStatic)
          sb.Append("static ");
      }
      sb.Append(MemberNameBuilder.GetMemberNameRaw(m, givenOptions, scope));
      return sb.ToString();
    }
  }
}
