//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif

  public class IdentifierContexts {
    public const int NullContext = 0x00000000;
    public const int TypeContext = 0x00000001;
    public const int VariableContext = 0x00000002;
    public const int AttributeContext = 0x00000004;
    public const int ParameterContext = 0x00000008;
    public const int EventContext = 0x00000010;
    public const int AllContext = 0x7FFFFFFF;
    public static bool IsActive(int identContext, int identContextFlags) {
      return (identContext & identContextFlags) != 0;
    }
  }

  /// <summary>
  /// Walks an IR, mutating it by replacing identifier nodes with NameBinding/Block nodes representing the
  /// members/labels the identifiers resolve to, and replacing type expressions with the types they refer to.
  /// Most of the logic here deals with maintaining and querying the scope chain.
  /// </summary>
  public class Looker : StandardCheckingVisitor{
    public Scope scope;
    /// <summary>
    /// Used to track the appropriate scope for each type
    /// </summary>
    public TrivialHashtable scopeFor;
    public UsedNamespaceList UsedNamespaces;
    public TrivialHashtable targetFor;
    public TrivialHashtable outerTargetFor;
    public IdentifierList labelList;
    public TrivialHashtable referencedLabels;
    public TrivialHashtable ambiguousTypes;
    public TrivialHashtable alreadyReported;
    public TrivialHashtable hasExplicitBaseClass;
    public TrivialHashtable typesToKeepUninstantiated;
    public AssemblyNode currentAssembly;
    public Compilation currentCompilation;
    public Method currentMethod;
    public Module currentModule;
    public CompilerOptions currentOptions;
    public Parameter currentParameter;
    public Hashtable currentPreprocessorDefinedSymbols;
    public TypeNode currentType;
    public Int32List identifierPositions;
    public Int32List identifierLengths;
    public NodeList identifierInfos;
    public Int32List identifierContexts;
    public ScopeList identifierScopes;
    public ScopeList allScopes;
    public QueryGroupBy currentGroup;
    public Error AbstractSealedUsedAsType;
    public TypeNodeList templateInstances;
    public TrivialHashtable templateInstanceTable;
    public bool ignoreMethodBodies;
    public TypeSystem typeSystem;
    public bool useGenerics;
    public bool NonNullChecking;
    public bool inMethodParameter;
    public bool inEventContext;
    protected bool InFirstAttributeVisit;
    bool visitingSecondaryInstances;

    public Looker(Scope scope, ErrorHandler errorHandler, TrivialHashtable scopeFor, TypeSystem typeSystem, TrivialHashtable ambiguousTypes, TrivialHashtable referencedLabels)
      : base(errorHandler){
      //TODO: verify that crucial system types have either been imported or defined by the Parser
      this.scope = scope;
      this.AddToAllScopes(this.scope);
      this.scopeFor = scopeFor;
      this.ambiguousTypes = ambiguousTypes;
      this.referencedLabels = referencedLabels;
      this.alreadyReported = new TrivialHashtable();
      this.hasExplicitBaseClass = new TrivialHashtable();
      this.typesToKeepUninstantiated = new TrivialHashtable();
      this.UsedNamespaces = new UsedNamespaceList();
      this.targetFor = new TrivialHashtable();
      this.labelList = new IdentifierList();
      this.AbstractSealedUsedAsType = Error.NotAType;
      Debug.Assert(typeSystem != null);
      this.typeSystem = typeSystem;
      this.useGenerics = TargetPlatform.UseGenerics;
      this.inMethodParameter = false;
      this.inEventContext = false;
    }
    public Looker(Visitor callingVisitor)
      : base(callingVisitor){
    }
    public override void TransferStateTo(Visitor targetVisitor){
      base.TransferStateTo(targetVisitor);
      Looker target = targetVisitor as Looker;
      if (target == null) return;
      target.AbstractSealedUsedAsType = this.AbstractSealedUsedAsType;
      target.ambiguousTypes = this.ambiguousTypes;
      target.alreadyReported = this.alreadyReported;
      target.currentAssembly = this.currentAssembly;
      target.currentCompilation = this.currentCompilation;
      target.currentGroup = this.currentGroup;
      target.currentMethod = this.currentMethod;
      target.currentModule = this.currentModule;
      target.currentOptions = this.currentOptions;
      target.currentParameter = this.currentParameter;
      target.currentType = this.currentType;
      target.hasExplicitBaseClass = this.hasExplicitBaseClass;
      target.identifierPositions = this.identifierPositions;
      target.identifierLengths = this.identifierLengths;
      target.identifierInfos = this.identifierInfos;
      target.identifierContexts = this.identifierContexts;
      target.identifierScopes = this.identifierScopes;
      target.allScopes = this.allScopes;
      target.labelList = this.labelList;
      target.outerTargetFor = this.outerTargetFor;
      target.referencedLabels = this.referencedLabels;
      target.scope = this.scope;
      target.scopeFor = this.scopeFor;
      target.targetFor = this.targetFor;
      target.templateInstances = this.templateInstances;
      target.templateInstanceTable = this.templateInstanceTable;
      target.typesToKeepUninstantiated = this.typesToKeepUninstantiated;
      target.typeSystem = this.typeSystem;
      target.useGenerics = this.useGenerics;
      target.UsedNamespaces = this.UsedNamespaces;
      target.inMethodParameter = this.inMethodParameter;
      target.inEventContext = this.inEventContext;
    }

    public override Expression VisitAnonymousNestedFunction(AnonymousNestedFunction func){
      if (func == null) return null;
      if (!(this.scope is BlockScope || this.scope is MethodScope)){
        this.HandleError(func, Error.AnonymousNestedFunctionNotAllowed);
        return null;
      }
      Method method = func.Method = new Method();
      method.Name = Identifier.For("Function:"+func.UniqueKey);
      this.ConstructMethodForNestedFunction(func, func.Method, null, func.Parameters, func.Body);
      return func;
    }
    public override AssemblyNode VisitAssembly(AssemblyNode assembly){
      this.currentModule = this.currentAssembly = assembly;
      return base.VisitAssembly(assembly);
    }
    public override Expression VisitAttributeConstructor(AttributeNode attribute){
      if (attribute == null) return null;
      Expression constructor = attribute.Constructor;
      if (constructor == null) return null;
      if (constructor is MemberBinding) return constructor;
      Identifier consId = constructor as Identifier;
      TypeNode t = null;
      if (consId != null){
        this.AddNodePositionAndInfo(consId, attribute, IdentifierContexts.AttributeContext);
        t = this.LookupType(consId);
        Class c = t as Class;
        if (c != null && c.DeclaringModule == this.currentModule) this.VisitBaseClassReference(c);
        if (!this.TypeIsAttribute(c)){
          Identifier consIdAttribute = this.AppendAttributeIfAllowed(consId);
          if (consIdAttribute != null){
            TypeNode tt = this.LookupType(consIdAttribute);
            if (tt != null) t = tt;
          }
        }else if (this.CheckForAttributeAmbiguity(consId)){
          Identifier consIdAttribute = this.AppendAttributeIfAllowed(consId);
          if (consIdAttribute != null){
            TypeNode tAttribute = this.LookupType(consIdAttribute);
            if (tAttribute != null)
              //REVIEW: should this give an error if tAttribute is NOT an attribute?
              this.HandleAmbiguousAttributeError(consId, t, tAttribute);
          }
        }
      }else{
        t = this.LookupType(constructor);
        Class c = t as Class;
        if (c != null && c.DeclaringModule == this.currentModule) this.VisitBaseClassReference(c);
        if (!this.TypeIsAttribute(c)){
          QualifiedIdentifier qual = constructor as QualifiedIdentifier;
          if (qual != null){
            qual = new QualifiedIdentifier(qual.Qualifier, this.AppendAttributeIfAllowed(qual.Identifier));
            TypeNode tt = this.LookupType(qual, false, 0);
            if (tt != null) t = tt;
          }
        }
      }
      if (t == null){
        if (this.currentOptions == null || !this.currentOptions.IsContractAssembly)
          this.HandleTypeExpected(constructor);
        return null;
      }else if (t.IsAbstract){
        this.HandleError(constructor, Error.AbstractAttributeClass, this.GetTypeName(t));
      }
      return new Literal(t, SystemTypes.Type, constructor.SourceContext);
    }
    public virtual Identifier AppendAttributeIfAllowed(Identifier id){
      Identifier result = new Identifier(id.Name+"Attribute", id.SourceContext);
      result.Prefix = id.Prefix;
      return result;
    }
    public virtual bool TypeIsAttribute(TypeNode t){
      if (t == null) return false;
      while (t.BaseType != null){
        if (t.BaseType == SystemTypes.Attribute) return true;
        t = t.BaseType;
      }
      return false;
    }
    public virtual bool CheckForAttributeAmbiguity(Identifier attributeId){
      return false;
    }
    public virtual void HandleAmbiguousAttributeError(Identifier attributeId, TypeNode t, TypeNode tAttribute){
    }
    public override AttributeNode VisitAttributeNode(AttributeNode attribute){
      if (attribute == null) return null; 
      Scope savedScope = this.scope;
      this.scope = new AttributeScope(savedScope, attribute);
      this.AddToAllScopes(this.scope);
      attribute.Constructor = this.VisitAttributeConstructor(attribute);
      if (attribute.Expressions != null && attribute.Expressions.Count > 0 && attribute.Expressions[0] != null)
        this.AddNodePositionAndInfo(attribute, attribute, IdentifierContexts.AttributeContext);
      ExpressionList expressions = attribute.Expressions = this.VisitExpressionList(attribute.Expressions);
      for (int i = 0, n = expressions == null ? 0 : expressions.Count; i < n; i++){
        Expression e = expressions[i];
        Literal lit = e as Literal;
        if (lit != null && lit.Type == SystemTypes.Type){
          TypeNode t = lit.Value as TypeNode;
          if (t != null)
            expressions[i] = e = lit = new Literal(this.VisitTypeReference(t), lit.Type, lit.SourceContext);
        }
        NamedArgument na = e as NamedArgument;
        if (na != null && na.Name == StandardIds.Namespace){
          Literal prefix = na.Value as Literal; 
          if (prefix != null){
            Identifier p = Identifier.For(prefix.Value.ToString());
            AliasDefinition aliasDef = this.LookupAlias(p);
            if (aliasDef != null){
              if (aliasDef.AliasedUri != null) 
                na.Value = prefix = new Literal(aliasDef.AliasedUri.ToString(), prefix.Type, prefix.SourceContext);
              else if (aliasDef.AliasedExpression != null)
                na.Value = prefix = new Literal(aliasDef.AliasedExpression.ToString(), prefix.Type, prefix.SourceContext);
            }
          }
        }
      }
      this.scope = savedScope;
      return attribute;
    }
    public override Block VisitBlock(Block block){
      if (block == null) return null;
      Scope savedScope = this.scope;
      Scope scope = this.scope = new BlockScope(savedScope, block);
      this.AddToAllScopes(this.scope);
      TrivialHashtable savedTargetFor = this.targetFor;
      TrivialHashtable targetFor = this.targetFor = new TrivialHashtable();
      IdentifierList labelList = this.labelList;
      int n = labelList.Count;
      for (int i = 0; i < n; i++){
        Identifier label = labelList[i];
        targetFor[label.UniqueIdKey] = savedTargetFor[label.UniqueIdKey];
      }
      Declarer declarer = this.GetDeclarer();
      declarer.VisitBlock(block, scope, targetFor, labelList);
      block = base.VisitBlock(block);
      this.scope = savedScope;
      this.targetFor = savedTargetFor;
      labelList.Count = n;
      return block;
    }
    public override Statement VisitSwitch(Switch Switch){
      if (Switch == null) return null;
      Scope savedScope = this.scope;
      Scope scope = this.scope = Switch.Scope = new BlockScope(savedScope, null);
      this.AddToAllScopes(this.scope);
      TrivialHashtable savedTargetFor = this.targetFor;
      TrivialHashtable targetFor = this.targetFor = new TrivialHashtable();
      IdentifierList labelList = this.labelList;
      int n = labelList.Count;
      for (int i = 0; i < n; i++){
        Identifier label = labelList[i];
        targetFor[label.UniqueIdKey] = savedTargetFor[label.UniqueIdKey];
      }
      Declarer declarer = this.GetDeclarer();
      SwitchCaseList switchCases = Switch.Cases;
      for (int i = 0, m = switchCases == null ? 0 : switchCases.Count; i < m; i++)
        declarer.VisitSwitchCase(switchCases[i], scope, targetFor, labelList);
      Switch = (Switch)base.VisitSwitch(Switch);
      this.scope = savedScope;
      this.targetFor = savedTargetFor;
      labelList.Count = n;
      return Switch;
    }
    public override Statement VisitCatch(Catch Catch){
      if (Catch == null) return null;
      Catch.Type = this.VisitTypeReference(Catch.Type);
      Scope savedScope = this.scope;
      Scope scope = this.scope = new BlockScope(savedScope, Catch.Block);
      this.AddToAllScopes(this.scope);
      TrivialHashtable savedTargetFor = this.targetFor;
      TrivialHashtable targetFor = this.targetFor = new TrivialHashtable();
      IdentifierList labelList = this.labelList;
      int n = labelList.Count;
      for (int i = 0; i < n; i++){
        Identifier label = labelList[i];
        targetFor[label.UniqueIdKey] = savedTargetFor[label.UniqueIdKey];
      }
      Identifier catchId = Catch.Variable as Identifier;
      if (catchId != null){
        Field f = new Field();
        f.DeclaringType = scope;
        f.Flags = FieldFlags.CompilerControlled;
        f.Name = catchId;
        f.Type = Catch.Type;
        scope.Members.Add(f);
        Catch.Variable = new MemberBinding(new ImplicitThis(scope, 0), f,catchId.SourceContext);
      }
      this.scope = scope;
      this.AddToAllScopes(this.scope);
      Declarer declarer = this.GetDeclarer();
      declarer.VisitBlock(Catch.Block, scope, targetFor, labelList);
      Catch.Block = base.VisitBlock(Catch.Block);
      this.scope = savedScope;
      this.targetFor = savedTargetFor;
      labelList.Count = n;
      return Catch;
    }
    public override Compilation VisitCompilation(Compilation compilation){
      if (compilation == null || compilation.TargetModule == null) return null;
      this.currentCompilation = compilation;
      this.currentModule = compilation.TargetModule;
      this.currentAssembly = compilation.TargetModule.ContainingAssembly;
      this.currentOptions = compilation.CompilerParameters as CompilerOptions;
      compilation = base.VisitCompilation(compilation);
      this.VisitTemplateInstances();
      return compilation;
    }
    public override CompilationUnit VisitCompilationUnit(CompilationUnit cUnit){
      if (cUnit == null) return null;
      this.currentPreprocessorDefinedSymbols = cUnit.PreprocessorDefinedSymbols;
      if (this.ErrorHandler != null)
        this.ErrorHandler.SetPragmaWarnInformation(cUnit.PragmaWarnInformation);
      this.DetermineIfNonNullCheckingIsDesired(cUnit);
      if (cUnit.Compilation != null)
        this.currentOptions = cUnit.Compilation.CompilerParameters as CompilerOptions;
      cUnit = base.VisitCompilationUnit(cUnit);
      if (this.currentCompilation == null) this.VisitTemplateInstances();
      if (this.ErrorHandler != null)
        this.ErrorHandler.ResetPragmaWarnInformation();
      return cUnit;
    }

    private void VisitTemplateInstances() {
      if (this.templateInstances != null) {
        int initialCount = this.templateInstances.Count;
        ErrorHandler savedErrorHandler = this.ErrorHandler;
        for (int i = 0; i <  this.templateInstances.Count; i++) {
          TypeNode ti = this.templateInstances[i];
          if (ti == null) continue;
          TypeNode template = ti.Template;
          if (template == null) continue;
          while (template.Template != null) template = template.Template;
          TypeScope clScope = this.scopeFor[template.UniqueKey] as TypeScope;
          if (clScope == null) continue; //The template is external to the compilation
          this.scope = clScope.OuterScope;
          this.AddToAllScopes(this.scope);
          template.NewTemplateInstanceIsRecursive = true;
          if (i >= initialCount) {
            this.visitingSecondaryInstances = true;
            this.ErrorHandler = null;
          }
          try {
            this.Visit(ti);
          } finally {
            this.visitingSecondaryInstances = false;
            template.NewTemplateInstanceIsRecursive = false;
          }
        }
        this.ErrorHandler = savedErrorHandler;
        this.templateInstances = null;
      }
    }
    public virtual void DetermineIfNonNullCheckingIsDesired(CompilationUnit cUnit){
      this.NonNullChecking = false; //!(this.currentPreprocessorDefinedSymbols != null && this.currentPreprocessorDefinedSymbols.ContainsKey("NONONNULLTYPECHECK"));
    }
    public override TypeNode VisitConstrainedType(ConstrainedType cType){
      if (cType == null) return null;
      TypeNode uType = cType.UnderlyingType = this.VisitTypeReference(cType.UnderlyingType);
      if (uType == null) return null;
      cType.ProvideMembers();
      Method toConstrained = this.GetTypeView(cType).GetMethod(StandardIds.opImplicit, cType.UnderlyingType);
      Parameter param = toConstrained.Parameters[0];
      this.scope = new MethodScope(this.scope, null);
      this.AddToAllScopes(this.scope);
      ParameterField pField = new ParameterField(this.scope, null, FieldFlags.CompilerControlled, StandardIds.Value, cType.UnderlyingType, null);
      pField.Parameter = param;
      this.scope.Members.Add(pField);
      cType.Constraint = this.VisitExpression(cType.Constraint);
      this.scope = this.scope.OuterScope;
      this.AddToAllScopes(this.scope);
      return cType;
    }
    public override Expression VisitConstruct(Construct cons){
      if (cons == null) return null;
      this.AddNodePositionAndInfo(cons, cons, IdentifierContexts.AllContext);
      if (cons.Constructor == null) return null;
      MemberBinding mb = cons.Constructor as MemberBinding;
      TypeNode t = null;
      if (mb != null && (t = mb.BoundMember as TypeNode) != null)
        cons.Constructor = new Literal(this.VisitTypeReference(t), SystemTypes.Type, mb.SourceContext);
      else
        cons.Constructor = this.VisitExpression(cons.Constructor);
      if (this.identifierInfos != null){
        int n = this.identifierInfos.Count;
        while (n > 0){
          if (this.identifierPositions[n-1] < cons.SourceContext.StartPos) break;
          t = this.identifierInfos[n-1] as TypeNode;
          if (t != null && cons.Constructor is Literal && (((Literal)cons.Constructor).Value as TypeNode) == t)
            this.identifierInfos[n-1] = cons;
          else{
            NameBinding nb = this.identifierInfos[n-1] as NameBinding;
            if (nb != null && (cons.SourceContext.Encloses(nb.SourceContext) || 
              (nb.SourceContext.StartPos == nb.SourceContext.EndPos && cons.SourceContext.EndPos == nb.SourceContext.StartPos))){
              this.identifierInfos[n-1] = cons;
            }else{
              QualifiedIdentifier qualId = this.identifierInfos[n-1] as QualifiedIdentifier;
              if (qualId != null && (cons.SourceContext.Encloses(qualId.SourceContext) ||
                (qualId.SourceContext.StartPos == qualId.SourceContext.EndPos && cons.SourceContext.EndPos == qualId.SourceContext.StartPos))){
                this.identifierInfos[n-1] = cons;
                cons.Constructor = qualId;
              }
            }
          }
          n--;
        }
      }
      cons.Operands = this.VisitExpressionList(cons.Operands);
      cons.Owner = this.VisitExpression(cons.Owner);
      return cons;
    }
    public override Expression VisitConstructArray(ConstructArray consArr){
      if (consArr == null) return null;
      this.AbstractSealedUsedAsType = Error.AbstractSealedArrayElementType;
      consArr.ElementType = this.VisitTypeReference(consArr.ElementType, true);
      this.AbstractSealedUsedAsType = Error.NotAType;
      consArr.Operands = this.VisitExpressionList(consArr.Operands);
      consArr.Initializers = this.VisitExpressionList(consArr.Initializers);
      consArr.Owner = this.VisitExpression(consArr.Owner);
      return consArr;
    }
    public override DelegateNode VisitDelegateNode(DelegateNode delegateNode){
      DelegateNode dn = this.VisitTypeNode(delegateNode) as DelegateNode;
      return dn;

    }
    public virtual ParameterList VisitDelegateParameters(ParameterList parameterList){
      TrivialHashtable namesAlreadyEncountered = new TrivialHashtable();
      this.inMethodParameter = true;
      for (int i = 0, n = parameterList == null ? 0 : parameterList.Count; i < n; i++){
        Parameter parameter = parameterList[i];
        if (parameter == null) continue;
        parameter.Attributes = this.VisitAttributeList(parameter.Attributes);
        TypeNode pt = this.VisitTypeReference(parameter.Type, true);
        parameter.DefaultValue = this.VisitExpression(parameter.DefaultValue);
        Reference pr = pt as Reference;
        if (pr != null && (pr.ElementType == SystemTypes.DynamicallyTypedReference || pr.ElementType == SystemTypes.ArgIterator)){
          this.currentParameter = parameter;
          this.HandleError(parameter.Name, Error.ParameterTypeCannotBeTypedReference, this.GetTypeName(pr));
          this.currentParameter = null;
          pt = SystemTypes.Object;
        }
        parameter.Type = pt;
        if (parameter.Name == null) continue;
        if (namesAlreadyEncountered[parameter.Name.UniqueIdKey] != null)
          this.HandleError(parameter.Name, Error.DuplicateParameterName, parameter.Name.ToString());
        else
          namesAlreadyEncountered[parameter.Name.UniqueIdKey] = parameter.Name;
      }
      this.inMethodParameter = false;
      return parameterList;
    }
    public override EnumNode VisitEnumNode(EnumNode enumNode){
      if (enumNode == null) return null;
      if (enumNode.UnderlyingTypeExpression != null)
        enumNode.UnderlyingType = this.VisitTypeReference(enumNode.UnderlyingTypeExpression);
      else
        enumNode.UnderlyingType = this.VisitTypeReference(enumNode.UnderlyingType);
      return base.VisitEnumNode(enumNode);
    }
    public override Event VisitEvent(Event e){
      //TODO: move parts of this into Normalizer
      if (e == null) return null;
      e.Attributes = this.VisitAttributeList(e.Attributes);
      TypeNode t = e.DeclaringType = this.VisitTypeReference(e.DeclaringType);
      this.inEventContext = true;
      e.HandlerType = this.VisitTypeReference(e.HandlerType);
      this.inEventContext = false;
      e.ImplementedTypes = this.VisitImplementedTypeList(e.ImplementedTypes);
      this.AddNodePositionAndInfo(e.Name, e, IdentifierContexts.AllContext);
      e.InitialHandler = this.VisitExpression(e.InitialHandler);
      if (!(e.HandlerType is DelegateNode)) return e;
      if (e.HandlerAdder != null && e.HandlerRemover != null) return e;
      //If only a handler, or only a remover is specified, that too is an error, but the Parser (or override or this method) is expected to deal with that
      //Provide default implementations for HandlerAdder and HandlerRemover
      //Field to store delegate
      Field f = null;
      if ((e.HandlerFlags & (MethodFlags.Abstract|MethodFlags.PInvokeImpl)) == 0){
        f = e.BackingField = new Field();
        f.ForEvent = e;
        f.DeclaringType = t;
        f.Name = e.Name;
        f.Type = e.HandlerType;
        f.TypeExpression = e.HandlerTypeExpression;
        f.Flags = FieldFlags.Private|FieldFlags.SpecialName;
        if (e.IsStatic) f.Flags |= FieldFlags.Static;
        f.Initializer = e.InitialHandler;
        f.HidesBaseClassMember = e.HidesBaseClassMember || e.OverridesBaseClassMember;
        t.Members.Add(f);
      }
      //HandlerAdder default implementation
      if (e.HandlerAdder == null){
        ParameterList parameters = new ParameterList(1);
        Parameter p = new Parameter();
        p.Name = StandardIds.Value;
        p.Type = this.AddNonNullWrapperIfNeeded(this.VisitTypeReference(e.HandlerType));        
        p.TypeExpression = e.HandlerTypeExpression;
        parameters.Add(p);
        Method adder = new Method();
        adder.Flags = e.HandlerFlags;
        if (!e.IsStatic)
          adder.CallingConvention = CallingConventionFlags.HasThis;
        if (!t.IsValueType) adder.ImplFlags = MethodImplFlags.Synchronized;
        adder.DeclaringMember = e;
        adder.DeclaringType = t;
        adder.ImplementedTypes = e.ImplementedTypes;
        adder.ImplementedTypeExpressions = e.ImplementedTypeExpressions;
        adder.Name = new Identifier("add_"+e.Name);
        adder.Name.SourceContext = e.Name.SourceContext;
        adder.Parameters = parameters;
        adder.ReturnType = SystemTypes.Void;
        adder.HidesBaseClassMember = e.HidesBaseClassMember;
        adder.OverridesBaseClassMember = e.OverridesBaseClassMember;
        if (!(adder.IsAbstract || adder.IsExtern)){
          StatementList adderStatements = new StatementList(1);
          MemberBinding combineMethod = new MemberBinding(null, Runtime.Combine);
          MemberBinding fBinding = new MemberBinding(adder.ThisParameter, f);
          ExpressionList arguments = new ExpressionList(2);
          arguments.Add(fBinding);
          arguments.Add(p);
          MethodCall combineCall = new MethodCall(combineMethod, arguments, NodeType.Call, Runtime.Combine.ReturnType,  e.SourceContext);
          BinaryExpression cc = new BinaryExpression(combineCall, new Literal(e.HandlerType, SystemTypes.Type), NodeType.ExplicitCoercion, e.SourceContext);
          adderStatements.Add(new AssignmentStatement(fBinding, cc, e.SourceContext));
          adder.Body = new Block(adderStatements);
        }
        e.HandlerAdder = adder;
        t.Members.Add(adder);
      }
      //HandlerRemover default implementation
      if (e.HandlerRemover == null){
        ParameterList parameters = new ParameterList(1);
        Parameter p = new Parameter();
        p.Name = StandardIds.Value;
        p.Type = this.AddNonNullWrapperIfNeeded(this.VisitTypeReference(e.HandlerType));
        p.TypeExpression = e.HandlerTypeExpression;
        parameters.Add(p);
        Method remover = new Method();
        remover.Flags = e.HandlerFlags;
        if (!e.IsStatic)
          remover.CallingConvention = CallingConventionFlags.HasThis;
        if (!t.IsValueType) remover.ImplFlags = MethodImplFlags.Synchronized;
        remover.DeclaringMember = e;
        remover.DeclaringType = t;
        remover.ImplementedTypes = e.ImplementedTypes;
        remover.ImplementedTypeExpressions = e.ImplementedTypeExpressions;
        remover.Name = new Identifier("remove_"+e.Name);
        remover.Name.SourceContext = e.Name.SourceContext;
        remover.Parameters = parameters;
        remover.ReturnType = SystemTypes.Void;
        remover.HidesBaseClassMember = e.HidesBaseClassMember;
        remover.OverridesBaseClassMember = e.OverridesBaseClassMember;
        if (!(remover.IsAbstract || remover.IsExtern)){
          StatementList removerStatements = new StatementList();
          MemberBinding removeMethod = new MemberBinding(null, Runtime.Remove);
          MemberBinding fBinding = new MemberBinding(remover.ThisParameter, f);
          ExpressionList arguments = new ExpressionList(2);
          arguments.Add(fBinding);
          arguments.Add(p);
          MethodCall removeCall = new MethodCall(removeMethod, arguments, NodeType.Call, Runtime.Remove.ReturnType, e.SourceContext);
          BinaryExpression rc = new BinaryExpression(removeCall, new Literal(e.HandlerType, SystemTypes.Type), NodeType.ExplicitCoercion, e.SourceContext);
          removerStatements.Add(new AssignmentStatement(fBinding, rc, e.SourceContext));
          remover.Body = new Block(removerStatements);
        }
        e.HandlerRemover = remover;
        if (e.HandlerAdder != null) remover.Attributes = e.HandlerAdder.Attributes;
        t.Members.Add(remover);
      }
      AttributeList methAttributes = null;
      AttributeList attributes = e.Attributes;
      for (int i = 0, n = attributes == null ? 0 : attributes.Count; i < n; i++){
        AttributeNode attr = this.VisitAttributeNode(attributes[i]);
        if (attr == null) continue;
        attributes[i] = null;
        if ((attr.Target & AttributeTargets.Method) != 0){
          if (methAttributes == null) methAttributes = new AttributeList(n);
          methAttributes.Add(attr);
        }else
          attributes[i] = attr;
      }
      if (methAttributes != null){
        e.HandlerAdder.Attributes = methAttributes.Clone();
        e.HandlerRemover.Attributes = methAttributes.Clone();
        if (e.HandlerCaller != null)
          e.HandlerCaller.Attributes = methAttributes.Clone();
      }
      return e;
    }
    public override Statement VisitExpose(Expose Expose){
      if (Expose == null) return null;
      Expose.Instance = this.VisitExpression(Expose.Instance);
      Expose.Body = this.VisitBlock(Expose.Body);
      return Expose;
    }
    class ReplaceInductionVariable : StandardVisitor{
      Local inductionVariable;
      public ReplaceInductionVariable(Local inductionVariable){
        this.inductionVariable = inductionVariable;
      }
      public override Expression VisitIdentifier(Identifier identifier){
        if (identifier == null) return null;
        if (identifier.Name == inductionVariable.Name.Name)
          return new LocalBinding(inductionVariable, identifier.SourceContext);
        return identifier;
      }
    }
    public override Statement VisitForEach(ForEach forEach){
      if (forEach == null) return null;
      Scope savedScope = this.scope;
      Scope scope = this.scope = forEach.ScopeForTemporaryVariables = new BlockScope(savedScope, forEach.Body);
      this.AddToAllScopes(this.scope);
      forEach.TargetVariableType = this.VisitTypeReference(forEach.TargetVariableType, true);
      forEach.SourceEnumerable = this.VisitExpression(forEach.SourceEnumerable);
      Identifier inductionVariableId = forEach.InductionVariable as Identifier;
      if (inductionVariableId != null){
        Local inductionVariable =
          new Local(
          inductionVariableId,
          OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.GenericList.GetGenericTemplateInstance(this.currentModule, new TypeNodeList(forEach.TargetVariableType))),
          inductionVariableId.SourceContext);
        forEach.InductionVariable = inductionVariable;
        if (forEach.Invariants != null){
          StandardVisitor visitor = new ReplaceInductionVariable(inductionVariable);
          forEach.Invariants = visitor.VisitLoopInvariantList(forEach.Invariants);
        }
      }
      forEach.Invariants = this.VisitLoopInvariantList(forEach.Invariants);
      Identifier targetId = forEach.TargetVariable as Identifier;
      if (targetId != null){
        Field f = new Field();
        f.DeclaringType = scope;
        f.Flags = FieldFlags.CompilerControlled|FieldFlags.InitOnly;
        f.Name = targetId;
        f.Type = forEach.TargetVariableType;
        scope.Members.Add(f);
        forEach.TargetVariable = new MemberBinding(new ImplicitThis(scope, 0), f);
        forEach.TargetVariable.SourceContext = targetId.SourceContext;
        this.AddNodePositionAndInfo(targetId, forEach.TargetVariable, IdentifierContexts.AllContext);
      }
      forEach.Body = this.VisitBlock(forEach.Body);
      if (forEach.Body != null) forEach.Body.HasLocals = true;
      this.scope = savedScope;
      return forEach;
    }
    public override Statement VisitFunctionDeclaration(FunctionDeclaration fDecl){
      if (fDecl == null) return fDecl;
      this.ConstructMethodForNestedFunction(fDecl, fDecl.Method, fDecl.ReturnType, fDecl.Parameters, fDecl.Body);
      return fDecl;
    }
    public virtual void ConstructMethodForNestedFunction(Node func, Method method, TypeNode returnType, ParameterList parList, Block body){
      if (!(this.scope is BlockScope || this.scope is MethodScope)){Debug.Assert(false); return;}
      Scope savedScope = this.scope;
      TrivialHashtable savedOuterTargetFor = this.outerTargetFor;
      this.outerTargetFor = this.targetFor;
      this.targetFor = new TrivialHashtable();
      IdentifierList savedLabelList = this.labelList;
      this.labelList = new IdentifierList();
      Class closureClass = savedScope is BlockScope ? ((BlockScope)savedScope).ClosureClass : ((MethodScope)savedScope).ClosureClass;
      method.SourceContext = func.SourceContext;
      if (body != null && body.SourceContext.Document != null) {
        method.SourceContext.EndPos = body.SourceContext.EndPos;
        Debug.Assert(method.SourceContext.EndPos >= method.SourceContext.StartPos);
      }
      method.Flags = MethodFlags.CompilerControlled;
      // It turns out that nested functions might be nested within a class and not within a method
      // This happens when, e.g., a comprehension appears in a field initializer.
      // The resulting anonymous nested function must be static to generate verifiable code.
      if (this.currentMethod != null){
        method.CallingConvention = CallingConventionFlags.HasThis;
      }else{
        method.Flags |= MethodFlags.Static;
      }
      method.InitLocals = true;
      method.DeclaringType = closureClass;
      if (closureClass == null) method.DeclaringType = this.currentType;
      TypeNodeList methodTemplParams = this.currentMethod == null ? null : this.currentMethod.TemplateParameters;
      TypeNodeList closureTemplParams = closureClass == null ? null : closureClass.TemplateParameters;
      int numTemplateParams = methodTemplParams == null ? 0 : methodTemplParams.Count;
      if (closureTemplParams == null)
        numTemplateParams = 0;
      else if (closureTemplParams.Count < numTemplateParams)
        numTemplateParams = closureTemplParams.Count;
      Class scope = this.scope = method.Scope = new MethodScope(this.scope, this.UsedNamespaces, method);
      this.AddToAllScopes(this.scope);
      this.inMethodParameter = true;
      ParameterList methPars = new ParameterList();
      for (int i = 0, n = parList == null ? 0 : parList.Count; i < n; i++){
        Parameter p = parList[i];
        if (p == null) continue;
        methPars.Add(p);
        TypeNode pType = p.Type = this.VisitTypeReference(p.Type, true);
        if (pType == null) continue;
        if (pType is MethodTypeParameter || pType is MethodClassParameter) {
          for (int j = 0; j < numTemplateParams; j++) {
            if (methodTemplParams[j] == pType) {
              //pType = closureTemplParams[j];
              //Parameter mp = (Parameter)p.Clone();
              //mp.Type = pType;
              //methPars[methPars.Count-1] = mp;
              break;
            }
          }
        }
        ParameterField f = new ParameterField();
        f.Parameter = p;
        f.Flags = FieldFlags.CompilerControlled;
        f.Name = p.Name;
        f.Type = pType;
        f.DeclaringType = scope;
        scope.Members.Add(f);
      }
      this.inMethodParameter = false;
      method.Parameters = methPars;
      method.ReturnType = this.VisitTypeReference(returnType, true);
      Method savedMethod = this.currentMethod;
      this.currentMethod = method;
      method.Body = this.VisitBlock(body);
      if (returnType == null) method.ReturnType = null;
      this.currentMethod = savedMethod;
      this.scope = savedScope;
      this.targetFor = this.outerTargetFor;
      this.outerTargetFor = savedOuterTargetFor;
      this.labelList = savedLabelList;
    }
    public override Statement VisitGoto(Goto Goto){
      if (Goto == null) return null;
      if (Goto.TargetLabel == null) return Goto; //could happen if parser recovered from error.
      Block target = this.targetFor[Goto.TargetLabel.UniqueIdKey] as Block;
      if (target == null){
        if (this.outerTargetFor != null){
          target = this.outerTargetFor[Goto.TargetLabel.UniqueIdKey] as Block;
          if (target != null){
            this.HandleError(Goto, Error.GotoLeavesNestedMethod);
            if (this.referencedLabels != null)
              this.referencedLabels[target.UniqueKey] = Goto.TargetLabel;
            return null;
          }
        }
        return Goto;
      }
      if (this.referencedLabels != null)
        this.referencedLabels[target.UniqueKey] = Goto.TargetLabel;
      Branch br = new Branch(null, target);
      br.SourceContext = Goto.SourceContext;
      return br;
    }
    public virtual MemberList GetNestedNamespacesAndTypes(Identifier name, Scope scope, AssemblyReferenceList assembliesToSearch){
      if (name == null || scope == null || this.currentModule == null) return null;
      MemberList result = new MemberList();
      TrivialHashtable alreadyInList = new TrivialHashtable();
      this.GetNestedNamespacesAndTypes(name, scope, scope is AttributeScope, result, alreadyInList, assembliesToSearch, false);
      return result;
    }
    protected virtual bool GetNestedTypesIfNameResolvesToType(string name, Scope scope, MemberList list, TrivialHashtable alreadyInList){
      int pos = 0;
      Expression nameExpr = null;
      do{
        pos = name.IndexOf('.');
        if (pos > 0) {
          string id = name.Substring(0, pos);
          name = name.Substring(pos+1);
          if (nameExpr == null)
            nameExpr = Identifier.For(id);
          else
            nameExpr = new QualifiedIdentifier(nameExpr, Identifier.For(id));
        }
      }while (pos > 0);
      if (nameExpr == null)
        nameExpr = Identifier.For(name);
      else
        nameExpr = new QualifiedIdentifier(nameExpr, Identifier.For(name));
      this.scope = scope;
      this.AddToAllScopes(this.scope);
      TypeNode t = this.LookupType(nameExpr);
      bool result = t != null;
      while (t != null){
        MemberList members = this.GetTypeView(t).Members;
        for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
          TypeNode nt = members[i] as TypeNode;
          if (nt == null) continue;
          if (nt.Name == null || alreadyInList[nt.Name.UniqueIdKey] != null) continue;
          list.Add(nt);
        }
        t = t.BaseType;
      }
      return result;
    }
    protected virtual void GetNestedNamespacesAndTypes(Identifier name, Scope scope, bool constructorMustBeVisible, MemberList list, 
      TrivialHashtable alreadyInList, AssemblyReferenceList assembliesToSearch, bool listAllUnderRootNamespace) {
      if (name == null || scope == null || this.currentModule == null) return;
      bool mustBeAttribute = scope is AttributeScope;
      string nameStr = name.ToString();
      int nameLen = nameStr.Length;
      if (nameLen > 0){
        int i = nameStr.LastIndexOf('.');
        if (i < 0){
          nameLen = 0;
          nameStr = "";
        }else{
          nameLen = i;
          nameStr = nameStr.Substring(0, i);
          if (this.GetNestedTypesIfNameResolvesToType(nameStr, scope, list, alreadyInList))
            return;
        }
      }
      string fullNameStr = null;
      int fullNameLen = 0;
      alreadyInList[StandardIds.StructuralTypes.UniqueIdKey] = this;
      if (assembliesToSearch != null && assembliesToSearch.Count > 0) {
        for (int i = 0, n = assembliesToSearch == null ? 0 : assembliesToSearch.Count; i < n; i++) {
          AssemblyReference aRef = assembliesToSearch[i];
          if (aRef == null || aRef.Assembly == null) continue;
          if (fullNameStr != null)
            this.GetNestedNamespacesAndTypes(aRef.Assembly, constructorMustBeVisible, mustBeAttribute, fullNameStr, fullNameLen, list, alreadyInList, listAllUnderRootNamespace);
          this.GetNestedNamespacesAndTypes(aRef.Assembly, constructorMustBeVisible, mustBeAttribute, nameStr, nameLen, list, alreadyInList, listAllUnderRootNamespace);
        }
        return;
      }
      while (scope != null && !(scope is NamespaceScope)) scope = scope.OuterScope;
      NamespaceScope nsScope = scope as NamespaceScope;
      if (nsScope != null){
        Namespace nSpace = nsScope.AssociatedNamespace;
        if (nSpace != null && nSpace.FullName != null && nSpace.FullName.Length > 0){
          fullNameStr = nSpace.FullName;
          if (nameLen > 0) fullNameStr += "."+nameStr;
          fullNameLen = fullNameStr.Length;
        }
      }
      if (fullNameStr != null)
        this.GetNestedNamespacesAndTypes(this.currentModule, constructorMustBeVisible, mustBeAttribute, fullNameStr, fullNameLen, list, alreadyInList, listAllUnderRootNamespace);
      this.GetNestedNamespacesAndTypes(this.currentModule, constructorMustBeVisible, mustBeAttribute, nameStr, nameLen, list, alreadyInList, listAllUnderRootNamespace);
      ModuleReferenceList mRefs = this.currentModule.ModuleReferences;
      for (int i = 0, n = mRefs == null ? 0 : mRefs.Count; i < n; i++){
        ModuleReference mRef = mRefs[i];
        if (mRef == null || mRef.Module == null) continue;
        if (fullNameStr != null)
          this.GetNestedNamespacesAndTypes(mRef.Module, constructorMustBeVisible, mustBeAttribute, fullNameStr, fullNameLen, list, alreadyInList, listAllUnderRootNamespace);
        this.GetNestedNamespacesAndTypes(mRef.Module, constructorMustBeVisible, mustBeAttribute, nameStr, nameLen, list, alreadyInList, listAllUnderRootNamespace);
      }
      AssemblyReferenceList aRefs = this.currentModule.AssemblyReferences;
      for (int i = 0, n = aRefs == null ? 0 : aRefs.Count; i < n; i++){
        AssemblyReference aRef = aRefs[i];
        if (aRef == null || aRef.Assembly == null ) continue;
        if (fullNameStr != null)
          this.GetNestedNamespacesAndTypes(aRef.Assembly, constructorMustBeVisible, mustBeAttribute, fullNameStr, fullNameLen, list, alreadyInList, listAllUnderRootNamespace);
        this.GetNestedNamespacesAndTypes(aRef.Assembly, constructorMustBeVisible, mustBeAttribute, nameStr, nameLen, list, alreadyInList, listAllUnderRootNamespace);
      }
    }
    protected virtual void GetNestedNamespacesAndTypes(Module module, bool constructorMustBeVisible, bool mustBeAttribute, string nameStr, int nameLen, MemberList members, TrivialHashtable alreadyInList, bool listAllUnderRootNamespace) {
      NamespaceList nSpaces = module.GetNamespaceList();
      for (int j = 0, m = nSpaces == null ? 0 : nSpaces.Count; j < m; j++){
        Namespace ns = nSpaces[j];
        if (ns == null) continue;
        if (!ns.IsPublic && module != this.currentModule && !this.InternalsAreVisible(this.currentModule, module))
          continue;
        //TODO: would be nice if namespaces with no attribute types can be eliminated if mustBeAttribute is true.
        //(This is hard. The Whidbey C# language service does not manage it either.)
        string nsStr = ns.Name.ToString();
        if (nsStr.Length <= nameLen) continue;
        if (!nsStr.StartsWith(nameStr)) continue;
        if (nsStr.StartsWith("<")) continue;
        if (nameLen > 0 && nsStr[nameLen] != '.') continue;
        int pos = nsStr.IndexOf('.', nameLen+1);
        if (pos < 0) pos = nsStr.Length;
        if (nameLen == 0) nameLen = -1;
        Identifier id = new Identifier(nsStr.Substring(nameLen+1, pos-nameLen-1));
        if (alreadyInList[id.UniqueIdKey] != null) continue;
        ns = new Namespace(id, Identifier.For(nsStr.Substring(0, pos)), null, null, null, null);
        members.Add(ns);
        alreadyInList[id.UniqueIdKey] = id;
      }
      int key = Identifier.For(nameStr).UniqueIdKey;
      TypeNodeList types = module.Types;
      bool addBecauseUnderRoot = listAllUnderRootNamespace && nameStr != null && nameStr.Length == 0;
      for (int i = 1, n = types == null ? 0 : types.Count; i < n; i++){
        TypeNode t = types[i];
        if (t == null || t.Name == null || t.Namespace == null || (t.Namespace.UniqueIdKey != key && !addBecauseUnderRoot)) continue;
        if (t.Template != null) continue;
        if (!t.IsPublic && module != this.currentModule && !this.InternalsAreVisible(this.currentModule, module))
          continue;
        if (t.BaseType == null && t.Namespace == Identifier.Empty) continue;
        if (constructorMustBeVisible && this.TypeHasNoVisibleConstructorsOrIsAbstract(t)) continue;
        if (mustBeAttribute && !this.GetTypeView(t).IsAssignableTo(SystemTypes.Attribute)) continue;
        if (t == SystemTypes.Void) continue;
        if (alreadyInList[t.Name.UniqueIdKey] != null) continue;
        members.Add(t);
        alreadyInList[t.Name.UniqueIdKey] = t;
      }
    }
    public virtual MemberList GetVisibleNames(Scope scope){
      Scope mostNestedScope = scope;
      TrivialHashtable alreadyInList = new TrivialHashtable();
      MemberList list = new MemberList();
      for (; scope != null && !(scope is NamespaceScope); scope = scope.OuterScope){
        BlockScope bscope = scope as BlockScope;
        if (bscope != null){
          MemberList scopeMems = bscope.Members;
          for (int i = 0, n = scopeMems == null ? 0 : scopeMems.Count; i < n; i++){
            Field f = scopeMems[i] as Field;
            if (f == null || f.Name == null) continue;
            if (alreadyInList[f.Name.UniqueIdKey] != null) continue;
            list.Add(f);
            alreadyInList[f.Name.UniqueIdKey] = f;
          }
          continue;
        }
        MethodScope mscope = scope as MethodScope;
        if (mscope != null){
          if (mscope != mostNestedScope){
            MemberList scopeMems = mscope.Members;
            for (int i = 0, n = scopeMems == null ? 0 : scopeMems.Count; i < n; i++){
              ParameterField p = scopeMems[i] as ParameterField;
              if (p == null || p.Name == null) continue;
              if (alreadyInList[p.Name.UniqueIdKey] != null) continue;
              list.Add(p);
              alreadyInList[p.Name.UniqueIdKey] = p;
            }
          }
          if (mscope.DeclaringMethod != null){
            TypeNodeList tparams = mscope.DeclaringMethod.TemplateParameters;
            for (int i = 0, n = tparams == null ? 0 : tparams.Count; i < n; i++){
              TypeNode tp = tparams[i];
              if (tp == null || tp.Name == null) continue;
              if (alreadyInList[tp.Name.UniqueIdKey] != null) continue;
              list.Add(tp);
              alreadyInList[tp.Name.UniqueIdKey] = tp;
            }
          }
          continue;
        }
        AttributeScope ascope = scope as AttributeScope;
        if (ascope != null){
          AttributeNode attr = ascope.AssociatedAttribute;
          if (attr == null) continue;
          TypeNode atype = attr.Type;
          if (atype == null) continue;
          MemberList members = this.GetTypeView(atype).Members;
          for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
            Property prop = members[i] as Property;
            if (prop == null || prop.Setter == null) continue;
            TypeNode dummy = null;
            if (Checker.NotAccessible(prop.Setter, ref dummy, this.currentModule, this.currentType, this.TypeViewer)) continue;
            if (alreadyInList[prop.Name.UniqueIdKey] != null) continue;
            list.Add(prop);
            alreadyInList[prop.Name.UniqueIdKey] = prop;
          }
        }
        if (scope is TypeScope){
          scope = this.GetVisibleTypeMembers(scope, false, list, alreadyInList, false);
          break;
        }
      }
      this.GetVisibleTypesNamespacesAndPrefixes(scope, false, false, list, alreadyInList, false);
      return list;
    }
    public virtual MemberList GetNamespacesAndAttributeTypes(Scope scope){
      MemberList list = new MemberList();
      TrivialHashtable alreadyInList = new TrivialHashtable();
      this.GetVisibleTypesNamespacesAndPrefixes(scope, true, true, list, alreadyInList, false);
      return list;
    }
    public virtual MemberList GetVisibleTypesNamespacesAndPrefixes(Scope scope, bool constructorMustBeVisible, bool listAllUnderRootNamespace) {
      MemberList list = new MemberList();
      TrivialHashtable alreadyInList = new TrivialHashtable();
      this.GetVisibleTypesNamespacesAndPrefixes(scope, constructorMustBeVisible, false, list, alreadyInList, listAllUnderRootNamespace);
      return list;
    }
    protected virtual void GetVisibleTypesNamespacesAndPrefixes(Scope scope, bool constructorMustBeVisible, bool mustBeAttribute, MemberList list, TrivialHashtable alreadyInList, bool listAllUnderRootNamespace) {
      this.GetNestedNamespacesAndTypes(Identifier.Empty, scope, constructorMustBeVisible, list, alreadyInList, null, listAllUnderRootNamespace);
      scope = this.GetVisibleTypeMembers(scope, constructorMustBeVisible, list, alreadyInList, true);
      for (NamespaceScope nsScope = scope as NamespaceScope; nsScope != null; nsScope = (scope = scope.OuterScope) as NamespaceScope) {
        //Add types declared in the namespace
        Namespace nSpace = nsScope.AssociatedNamespace; //TODO: use nsScope.Members
        if (nSpace == null) continue;
        this.AddCurrentModuleTypesInNamespace(nSpace.Name, constructorMustBeVisible, mustBeAttribute, list, alreadyInList);
        //Add imported types with the same namespace as the current namespace
        this.AddAllImportedTypesInNamespace(nSpace.Name, constructorMustBeVisible, mustBeAttribute, list, alreadyInList);
        //Add types in used namespaces
        UsedNamespaceList usedNamespaces = nSpace.UsedNamespaces;
        for (int i = 0, n = usedNamespaces == null ? 0 : usedNamespaces.Count; i < n; i++){
          UsedNamespace unSpace = usedNamespaces[i];
          if (unSpace == null || unSpace.Namespace == null) continue; //TODO: deal with URIs
          this.AddCurrentModuleTypesInNamespace(unSpace.Namespace, constructorMustBeVisible, mustBeAttribute, list, alreadyInList);
          this.AddAllImportedTypesInNamespace(unSpace.Namespace, constructorMustBeVisible, mustBeAttribute, list, alreadyInList);
        }
        //Add prefixes
        AliasDefinitionList aliasDefinitions = nSpace.AliasDefinitions;
        for (int i = 0, n = aliasDefinitions == null ? 0 : aliasDefinitions.Count; i < n; i++){
          AliasDefinition aliasDef = aliasDefinitions[i];
          if (aliasDef == null) continue;
          if (alreadyInList[aliasDef.Alias.UniqueIdKey] == null){
            if (aliasDef.AliasedType != null)
              list.Add(new TypeAlias((TypeNode)aliasDef.AliasedType, aliasDef.Alias));
            else if (aliasDef.AliasedNamespace != null)
              list.Add(new Namespace(aliasDef.Alias, aliasDef.AliasedNamespace, null, null, null, null));
            else if (aliasDef.AliasedAssemblies != null)
              list.Add(new Namespace(aliasDef.Alias, Identifier.Empty, null, null, null, null));
            alreadyInList[aliasDef.Alias.UniqueIdKey] = aliasDef;
          }
        }
      }
    }
    public virtual bool TypeHasNoVisibleConstructorsOrIsAbstract(TypeNode type){
      if (type == null || type.IsAbstract) return true;
      if (type is Struct) return false;
      TypeNode dummy = this.currentType;
      MemberList constructors = this.GetTypeView(type).GetConstructors();
      for (int i = 0, n = constructors == null ? 0 : constructors.Count; i < n; i++){
        Member constr = constructors[i];
        if (constr == null) continue;
        if (!Checker.NotAccessible(constr, ref dummy, this.currentModule, this.currentType, this.TypeViewer)) return false;
      }
      return true;
    }
    public virtual Scope GetVisibleTypeMembers(Scope scope, bool constructorMustBeVisible, MemberList list, TrivialHashtable alreadyInList, bool restrictToNestedTypes){
      bool omitInstanceMembers = this.currentMethod == null || (this.currentMethod != null && this.currentMethod.IsStatic);
      TypeScope tScope = scope as TypeScope;
      while (tScope != null){
        TypeNode type = tScope.Type;
        TypeNodeList tparams = tScope.TemplateParameters;
        for (int i = 0, n = tparams == null ? 0 : tparams.Count; i < n; i++){
          TypeNode t = tparams[i];
          if (t == null || t.Name == null) continue;
          if (alreadyInList[t.Name.UniqueIdKey] == null){
            list.Add(t);
            alreadyInList[t.Name.UniqueIdKey] = t;
          }
        }
        while (type != null){
          MemberList members = this.GetTypeView(type).Members;
          for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
            if (restrictToNestedTypes){
              TypeNode t = members[i] as TypeNode;
              if (t == null || t.Name == null) continue;
              if (!this.TypeIsVisible(t)) continue;
              if (constructorMustBeVisible && this.TypeHasNoVisibleConstructorsOrIsAbstract(t)) continue;
              if (alreadyInList[t.Name.UniqueIdKey] == null){
                list.Add(t);
                alreadyInList[t.Name.UniqueIdKey] = t;
              }
            }else{
              Member m = members[i];
              if (m == null || m.Name == null) continue;
              if ((m as TypeNode) == SystemTypes.Void) continue;
              TypeNode dummy = this.currentType;
              if (omitInstanceMembers && !m.IsStatic) continue;
              if (Checker.NotAccessible(m, ref dummy, this.currentModule, this.currentType, this.TypeViewer)) continue;
              if (constructorMustBeVisible && m is TypeNode && this.TypeHasNoVisibleConstructorsOrIsAbstract((TypeNode)m)) continue;
              if (this.SuppressBecauseItIsADefaultMember(m, this.GetTypeView(type).DefaultMembers)) continue;
              if (this.LanguageSpecificSupression(m)) continue;
              if (alreadyInList[m.Name.UniqueIdKey] == null){
                list.Add(m);
                alreadyInList[m.Name.UniqueIdKey] = m;
              }else if (m is Method)
                list.Add(m);
            }
          }
          type = type.BaseType;
        }
        tScope = (scope = scope.OuterScope) as TypeScope;
      }
      return scope;
    }
    public virtual bool SuppressBecauseItIsADefaultMember(Member m, MemberList defaultMemberList) {
      for (int i = 0, n = defaultMemberList == null ? 0 : defaultMemberList.Count; i < n; i++){
        if (m == defaultMemberList[i]) return true;
      }
      return false;
    }
    public virtual bool LanguageSpecificSupression(Member m) {
      return false;
    }
    public virtual void AddAllImportedTypesInNamespace(Identifier nameSpace, bool constructorMustBeVisible, bool mustBeAttribute, MemberList list, TrivialHashtable alreadyInList) {
      if (this.currentModule == null) return;
      AssemblyReferenceList aRefs = this.currentModule.AssemblyReferences;
      for (int i = 0, n = aRefs == null ? 0 : aRefs.Count; i < n; i++){
        AssemblyReference aRef = aRefs[i];
        if (aRef == null || aRef.Assembly == null) continue;
        TypeNodeList types = aRef.Assembly.Types;
        for (int j = 1, m = types == null ? 0 : types.Count; j < m; j++){
          TypeNode t = types[j];
          if (t == null) continue;
          if (t.Namespace == null || t.Namespace.UniqueIdKey != nameSpace.UniqueIdKey) continue;
          if (!t.IsPublic && (this.currentAssembly == null || !this.currentAssembly.MayAccessInternalTypesOf(aRef.Assembly))) continue;
          if (mustBeAttribute && !this.GetTypeView(t).IsAssignableTo(SystemTypes.Attribute)) continue;
          if (constructorMustBeVisible && this.TypeHasNoVisibleConstructorsOrIsAbstract(t)) continue;
          if (t == SystemTypes.Void) continue;
          if (alreadyInList[t.Name.UniqueIdKey] != null) continue;
          list.Add(t);
        }
      }
    }
    public virtual void AddCurrentModuleTypesInNamespace(Identifier nameSpace, bool constructorMustBeVisible, bool mustBeAttribute, MemberList list, TrivialHashtable alreadyInList) {
      TypeNodeList types = this.currentModule.Types;
      for (int j = 1, m = types == null ? 0 : types.Count; j < m; j++) {
        TypeNode t = types[j];
        if (t == null) continue;
        if (t.Namespace == null || t.Namespace.UniqueIdKey != nameSpace.UniqueIdKey) continue;
        if (mustBeAttribute && !this.GetTypeView(t).IsAssignableTo(SystemTypes.Attribute)) continue;
        if (constructorMustBeVisible && this.TypeHasNoVisibleConstructorsOrIsAbstract(t)) continue;
        if (t == SystemTypes.Void) continue;
        if (alreadyInList[t.Name.UniqueIdKey] != null) continue;
        list.Add(t);
      }
    }
    public virtual Expression BindPseudoMember(TypeNode type, Identifier identifier){
      return null;
    }
    public class AliasBinding : Expression{
      public AliasDefinition AliasDefinition;
      public AliasBinding(AliasDefinition aliasDefinition)
        : base(NodeType.Nop){
        this.AliasDefinition = aliasDefinition;
      }
    }
    public override Expression VisitIdentifier(Identifier identifier){
      if (identifier == null) return null;
      AliasDefinition aliasDef = null;
      TypeNode t = null;
      TypeNode inaccessibleType = null;
      Namespace nspace = null;
      MemberList members = null;
      MemberList nameBindingMembers = null;
      int lexLevel = 0;
      Identifier prefix = identifier.Prefix;
      if (prefix != null) {
        Node n = this.LookupTypeOrNamespace(prefix, identifier, false, 0);
        if (this.identifierInfos != null)
          aliasDef = this.LookupAlias(prefix);
        t = n as TypeNode;
        if (t != null) goto returnT;
        if (n == null){
          aliasDef = this.LookupAlias(prefix);
          if (aliasDef != null && aliasDef.AliasedType != null) {
            this.HandleError(identifier.Prefix, Error.TypeAliasUsedAsNamespacePrefix, aliasDef.Alias.Name);
            this.HandleError(aliasDef, Error.RelatedErrorLocation);
            return null;
          }
          n = identifier;
        }else if (this.identifierInfos != null)
          aliasDef = this.LookupAlias(prefix);
        Debug.Assert(n is Identifier);
        nspace = new Namespace((Identifier)n);
        goto returnNspace;
      }
      if (identifier.UniqueIdKey == Identifier.Empty.UniqueIdKey) {
        this.AddNodePositionAndInfo(identifier, identifier, IdentifierContexts.AllContext);
        return identifier;
      }
      Scope scope = this.scope;
      while (scope != null){
        NamespaceScope nsScope = scope as NamespaceScope;
        if (nsScope != null){
          Node node = this.LookupTypeOrNamespace(null, identifier, false, nsScope, 0);
          t = node as TypeNode;
          if (t != null) goto returnT;
          if (node != null){
            aliasDef = node as AliasDefinition;
            if (aliasDef != null)
              nspace = new Namespace(identifier);
            else{
              Debug.Assert(node is Identifier);
              nspace = new Namespace((Identifier)node);
            }
            goto returnNspace;
          }
          MemberList anonymousMembers = this.LookupAnonymousMembers(identifier, nsScope);
          if (anonymousMembers != null && anonymousMembers.Count > 0){
            nameBindingMembers = anonymousMembers;
            goto done;
          }
        }else{
          TypeScope tScope = scope as TypeScope;
          if (tScope != null){
            TypeNode type = tScope.Type;
            TypeNodeList tparams = tScope.TemplateParameters;
            for (int i = 0, n = tparams == null ? 0 : tparams.Count; i < n; i++){
              t = tparams[i];
              if (t == null) continue;
              if (t.Name.UniqueIdKey == identifier.UniqueIdKey) goto returnT;
            }
            while (type != null){
              members = this.GetTypeView(type).GetMembersNamed(identifier);
              if (nameBindingMembers == null || nameBindingMembers.Count == 0) nameBindingMembers = members;
              for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
                Member mem = members[i];
                TypeNode dummy = this.currentType;
                if (Checker.NotAccessible(mem, ref dummy, this.currentModule, this.currentType, this.TypeViewer)) {
                  if (mem is TypeNode) inaccessibleType = (TypeNode)mem;
                  continue;
                }
                t = mem as TypeNode;
                if (t != null){
                  if (this.TypeIsVisible(t)) goto returnT;
                  inaccessibleType = t;
                }
                goto done;
              }
              Expression pseudoMember = this.BindPseudoMember(type, identifier);
              if (pseudoMember != null)
                return pseudoMember;
              Class c = type as Class;
              if (c != null && c.BaseClass == null && c.DeclaringModule == this.currentModule)
                this.VisitBaseClassReference(c);
              if (type == SystemTypes.Object) break;
              type = type.BaseType;
            }
          }else{
            members = scope.GetMembersNamed(identifier);
            if (nameBindingMembers == null || nameBindingMembers.Count == 0) nameBindingMembers = members;
            if (members != null && members.Count > 0){
              t = members[0] as TypeNode;
              if (t != null && members.Count == 1) goto returnT;
              goto done;
            }
            TypeNodeList tparams = scope.TemplateParameters;
            for (int i = 0, n = tparams == null ? 0 : tparams.Count; i < n; i++){
              t = tparams[i];
              if (t.Name.UniqueIdKey == identifier.UniqueIdKey) goto returnT;
            }
          }
        }
        scope = scope.OuterScope;
        lexLevel++;
      }
      returnT:
        if (t != null){
          this.AddNodePositionAndInfo(identifier, t, IdentifierContexts.AllContext);
          return new Literal(t, SystemTypes.Type, identifier.SourceContext);
        }
      returnNspace:
        if (nspace != null){
          if (identifier == nspace.Name && aliasDef != null){
            this.AddNodePositionAndInfo(identifier, aliasDef, IdentifierContexts.AllContext);
            return new AliasBinding(aliasDef);
          }else{
            this.AddNodePositionAndInfo(identifier, nspace, IdentifierContexts.AllContext);
            return nspace.Name;
          }
        }
    done:
      if (inaccessibleType != null) nameBindingMembers = new MemberList(inaccessibleType);
      else if (nameBindingMembers == null) nameBindingMembers = new MemberList(0);
      NameBinding result = new NameBinding(identifier, nameBindingMembers, this.scope, lexLevel, identifier.SourceContext);
      this.AddNodePositionAndInfo(identifier, result, IdentifierContexts.AllContext);
      return result;
    }
    protected TrivialHashtable anonTableForNamespace;
    public virtual MemberList LookupAnonymousMembers(Identifier identifier, NamespaceScope nsScope){
      if (identifier == null || nsScope == null){Debug.Assert(false); return null;}
      MemberList result = null;
      Namespace nspace = nsScope.AssociatedNamespace;
      if (nspace == null){Debug.Assert(false); return null;}
      TypeNodeList anonTypes = null;
      if (this.anonTableForNamespace == null) this.anonTableForNamespace = new TrivialHashtable();
      TrivialHashtable anonTable = (TrivialHashtable)this.anonTableForNamespace[nspace.UniqueKey];
      if (anonTable == null) this.anonTableForNamespace[nspace.UniqueKey] = anonTable = new TrivialHashtable();
      anonTypes = (TypeNodeList)anonTable[identifier.UniqueIdKey];
      if (anonTypes == null) 
        anonTable[identifier.UniqueIdKey] = anonTypes = new TypeNodeList(1);
      else if (anonTypes.Count == 0) 
        return null;
      this.LookupAnonymousTypes(nspace.Name, anonTypes);
      UsedNamespaceList usedNamespaces = nspace.UsedNamespaces;
      for (int i = 0, n = usedNamespaces == null ? 0 : usedNamespaces.Count; i < n; i++){
        UsedNamespace uns = usedNamespaces[i];
        if (uns == null) continue;
        this.LookupAnonymousTypes(uns.Namespace, anonTypes);
      }
      for (int i = 0, n = anonTypes.Count; i < n; i++){
        TypeNode type = anonTypes[i];
        if (type == null) continue;
        MemberList members = this.GetTypeView(type).GetMembersNamed(identifier);
        for (int j = 0, m = members == null ? 0 : members.Count; j < m; j++){
          Member member = members[j];
          if (member == null || member is TypeNode || !member.IsStatic) continue;
          TypeNode dummy = this.currentType;
          if (Checker.NotAccessible(member, ref dummy, this.currentModule, this.currentType, this.TypeViewer)) continue;
          if (result == null) result = new MemberList(m-j);
          result.Add(member);
        }
      }
      return result;
    }
    public virtual void AddNodePositionAndInfo(Node node, Node info, int identContext){
      if (node == null || info == null || this.identifierInfos == null) return;
      ClassExpression cExpr = info as ClassExpression;
      InterfaceExpression iExpr = info as InterfaceExpression;
      if (info is TypeExpression || cExpr != null || iExpr != null){
        if (node is Identifier){
          Identifier id = (Identifier)node;
          if (id.Prefix != null){
            AliasDefinition aliasDef = this.LookupAlias(id.Prefix);
            if (aliasDef != null){
              if (cExpr != null) aliasDef.RestrictToClassesAndInterfaces = true;
              else if (iExpr != null) aliasDef.RestrictToInterfaces = true;
              this.AddPositionAndInfo(id.SourceContext, aliasDef, identContext);
              return;
            }
          }
          if (cExpr != null || iExpr != null)
            this.AddPositionAndInfo(node.SourceContext, info, identContext);
          else
            this.AddPositionAndInfo(node.SourceContext, new NameBinding((Identifier)node, null, node.SourceContext), identContext);
          return;
        }else if (node is QualifiedIdentifier){
          QualifiedIdentifier qual = (QualifiedIdentifier)node;
          AliasDefinition aliasDef = this.LookupAlias(qual.Qualifier as Identifier);
          if (aliasDef != null){
            if (cExpr != null) aliasDef.RestrictToClassesAndInterfaces = true;
            else if (iExpr != null) aliasDef.RestrictToInterfaces = true;
            this.AddPositionAndInfo(qual.SourceContext, aliasDef, identContext);
          }else{
            Scope scope = this.scope;
            while (!(scope is NamespaceScope) && scope != null) scope = scope.OuterScope;
            if (scope is NamespaceScope) this.ExpandRootIfAnAlias(ref qual.Qualifier, (NamespaceScope)scope);
            if (cExpr != null || iExpr != null)
              this.AddPositionAndInfo(qual.SourceContext, info, identContext);
            else
              this.AddPositionAndInfo(qual.SourceContext, qual, identContext);
          }
          return;
        }
      }
      this.AddPositionAndInfo(node.SourceContext, info, identContext);
    }
    public virtual void AddPositionAndInfo(SourceContext sctx, Node info, int identContext) {
      Int32List posList = this.identifierPositions;
      Int32List lenList = this.identifierLengths;
      NodeList infos = this.identifierInfos;
      ScopeList scopes = this.identifierScopes;
      Int32List contexts = this.identifierContexts;
      if (identContext == IdentifierContexts.TypeContext) {
        if (this.inMethodParameter) identContext = IdentifierContexts.ParameterContext;
        else if (this.inEventContext) identContext = IdentifierContexts.EventContext;
      }
      if (infos == null || posList == null || info == null || sctx.Document == null || sctx.Document.Text == null)
        return;
      if (sctx.StartPos > sctx.Document.Text.Length) return;
      int pos = sctx.StartLine*1000;
      pos += sctx.StartColumn;
      int len = sctx.EndPos - sctx.StartPos;
      int n = posList.Count;
      int i = n-1;
      if (i >= 0){
        for (; i > 0; i--){
          if (posList[i] <= pos) break;
        }
        if (posList[i] == pos && lenList[i] == len){
          lenList[i] = len;
          infos[i] = info;
          scopes[i] = this.scope;
          contexts[i] = identContext;
          return;
        }
        if (posList[i] > pos){
          Debug.Assert(i == 0);
          i--;
        }
      }
      posList.Add(pos);
      lenList.Add(len);
      infos.Add(info);
      scopes.Add(this.scope);
      contexts.Add(identContext);
      if (i == n-1) return;
      for (int j = n-1; j >= (i+1); j--){
        posList[j+1] = posList[j];
        lenList[j+1] = lenList[j];
        infos[j+1] = infos[j];
        scopes[j+1] = scopes[j];
        contexts[j+1] = contexts[j];
      }
      posList[i+1] = pos;
      lenList[i+1] = len;
      infos[i+1] = info;
      scopes[i+1] = this.scope;
      contexts[i+1] = identContext;
    }
    public virtual TypeNodeList VisitImplementedTypeList(TypeNodeList typeReferences){
      if (typeReferences == null) return null;
      for (int i = 0, n = typeReferences.Count; i < n; i++){
        TypeNode te = typeReferences[i];
        TypeNode t = typeReferences[i] = this.VisitTypeReference(te);
        if (t != null && t.NodeType != NodeType.Interface) {
          this.HandleError(te, Error.ExplicitlyImplementedTypeNotInterface, te.SourceContext.SourceText);
          this.HandleRelatedError(t);
        }
      }
      return typeReferences;
    }
    public override Statement VisitFixed(Fixed Fixed){
      if (Fixed == null) return null;
      Scope savedScope = this.scope;
      BlockScope scope = Fixed.ScopeForTemporaryVariables = new BlockScope(savedScope, Fixed.Body);
      scope.MembersArePinned = true;
      this.scope = scope;
      this.AddToAllScopes(scope);
      Declarer declarer = new Declarer(this.ErrorHandler);
      declarer.VisitFixed(Fixed, scope);
      Statement result = base.VisitFixed(Fixed);
      this.scope = savedScope;
      return result;
    }
    public override Statement VisitLock(Lock Lock) {
      if (Lock == null) return null;
      Scope savedScope = this.scope;
      this.scope = Lock.ScopeForTemporaryVariable = new BlockScope(savedScope, Lock.Body);
      this.AddToAllScopes(this.scope);
      Lock.Guard = this.VisitExpression(Lock.Guard);
      Lock.Body = this.VisitBlock(Lock.Body);
      this.scope = savedScope;
      return Lock;
    }
    //At this stage, a MemberBinding can already be in the tree because a type expression needs to appear in an expression context.
    //For example typeof(foo) is modelled as a unary expression with operator typeof and a MemberBinding to a TypeExpression as operand.
    public override Expression VisitMemberBinding(MemberBinding memberBinding){
      if (memberBinding == null) return null;
      memberBinding.TargetObject = this.VisitExpression(memberBinding.TargetObject);
      TypeNode t = memberBinding.BoundMember as TypeNode;
      if (t != null){
        TypeNode t1 = this.VisitTypeReference(t, false /* [maf] */);
        if (t1 == null){
          if (this.identifierInfos != null)
            this.AddNodePositionAndInfo(memberBinding, t, IdentifierContexts.AllContext);
          return null;
        }
        return new Literal(t1, SystemTypes.Type, memberBinding.SourceContext);
      }
      return memberBinding;
    }
    public override Method VisitMethod(Method method){
      if (method == null) return null;
      if (method.IsNormalized) return method;
      method.Attributes = this.VisitAttributeList(method.Attributes);
      method.ReturnAttributes = this.VisitAttributeList(method.ReturnAttributes);
      this.AddNodePositionAndInfo(method.Name, method, IdentifierContexts.AllContext);
      method.SecurityAttributes = this.VisitSecurityAttributeList(method.SecurityAttributes);
      Scope savedScope = this.scope;
      Scope scope = this.scope = method.Scope = new MethodScope(this.scope, this.UsedNamespaces, method);
      this.AddToAllScopes(this.scope);
      if (!this.visitingSecondaryInstances || !this.useGenerics)
        scope.TemplateParameters = method.TemplateParameters;
      Method savedMethod = this.currentMethod;
      this.currentMethod = method;
      this.AbstractSealedUsedAsType = Error.AbstractSealedReturnType;
      method.TemplateArguments = this.VisitTypeReferenceList(method.TemplateArguments);
      method.TemplateParameters = this.VisitTypeParameterList(method.TemplateParameters);
      method.ReturnType = this.VisitReturnTypeReference(method.ReturnType);
      
      this.AbstractSealedUsedAsType = Error.NotAType;
      method.Parameters = this.VisitParameterList(method.Parameters);
      TypeNodeList implementedTypes = method.ImplementedTypes;
      if (implementedTypes != null)
        method.ImplementedTypes = this.VisitImplementedTypeList(implementedTypes.Clone());
      method.Contract = this.VisitMethodContract(method.Contract);
      if (!this.ignoreMethodBodies && (!this.useGenerics || method.Template == null)){
        this.VisitBlock(method.Body);
        if (method.Body != null){
          this.scope = method.Body.Scope; //REVIEW: huh? This gets clobbered below
          this.AddToAllScopes(this.scope);
        }
      }
      if (method.ReturnType == null) method.ReturnType = SystemTypes.Void;
      this.scope = savedScope;
      this.currentMethod = savedMethod;
      #region Calculate parameter indices for serializers. Otherwise this doesn't happen until Writer
      // Also, MethodContract.CopyFrom depends on this having been already computed.
      if (method.Parameters != null){
        int index = method.IsStatic ? 0 : 1;
        for (int i2 = 0, n2 = method.Parameters.Count; i2 < n2; i2++){
          Parameter par = method.Parameters[i2];
          if (par == null) continue;
          par.ParameterListIndex = i2;
          par.ArgumentListIndex = index;
          index++;
        }
      }
      #endregion Calculate parameter indices for serializers. Otherwise this doesn't happen until Writer
      return method;
    }
    public virtual TypeNode VisitReturnTypeReference(TypeNode typeNode) {
      TypeNode result = this.VisitTypeReference(typeNode, true);
      return result;
    }
    public override ParameterList VisitParameterList(ParameterList pl) {
      this.inMethodParameter = true;
      ParameterList result = base.VisitParameterList(pl);
      this.inMethodParameter = false;
      if (result == null) return null;
      if (result.Count > 0) {
        Parameter last = result[result.Count-1];
        if (last != null && last.Name == StandardIds.__Arglist) {
          if (this.currentMethod != null) {
            this.currentMethod.CallingConvention |= CallingConventionFlags.VarArg;
            pl.Count = pl.Count-1;
          }
        }
      }
      return result;
    }

    /// <summary>
    /// Used to Visit Ensures clause and replace "result" identifier with a ResultValue expression.
    /// Has no state, so we could have a singleton instance.
    /// </summary>
    public sealed class ReplaceResult : StandardVisitor{
      public ReplaceResult(){
      }
      public override Expression VisitIdentifier(Identifier identifier){
        if ( identifier == null ) return null;
        if ( identifier.Name == "result" )
          return new ReturnValue(identifier.SourceContext);
        else
          return base.VisitIdentifier(identifier);
      }
    }
    public override EnsuresExceptional VisitEnsuresExceptional (EnsuresExceptional exceptional) {
      if (exceptional == null) return null;
      if (exceptional.PostCondition == null || exceptional.Variable == null) {
        return base.VisitEnsuresExceptional(exceptional);
      }
      // Create the scoping structure only if there is a user-defined variable to bind to
      // the exception and there is a condition. (This code was copied from VisitCatch.)
      exceptional.Type = this.VisitTypeReference(exceptional.Type);
      SourceContext sctx = exceptional.PostCondition.SourceContext;
      Scope savedScope = this.scope;
      Block b = new Block(new StatementList(new ExpressionStatement(exceptional.PostCondition)));
      Scope scope = this.scope = new BlockScope(savedScope, b);
      this.AddToAllScopes(this.scope);
      TrivialHashtable savedTargetFor = this.targetFor;
      TrivialHashtable targetFor = this.targetFor = new TrivialHashtable();
      IdentifierList labelList = this.labelList;
      int n = labelList.Count;
      for (int i = 0; i < n; i++) {
        Identifier label = labelList[i];
        targetFor[label.UniqueIdKey] = savedTargetFor[label.UniqueIdKey];
      }
      Identifier throwsId = exceptional.Variable as Identifier;
      if (throwsId != null) {
        Field f = new Field();
        f.DeclaringType = scope;
        f.Flags = FieldFlags.CompilerControlled;
        f.Name = throwsId;
        f.Type = exceptional.Type;
        scope.Members.Add(f);
        exceptional.Variable = new MemberBinding(new ImplicitThis(scope, 0), f, throwsId.SourceContext);
      }
      this.scope = scope;
      this.AddToAllScopes(this.scope);
      Declarer declarer = this.GetDeclarer();
      declarer.VisitBlock(b, scope, targetFor, labelList);
      b = base.VisitBlock(b);
      exceptional.PostCondition = new BlockExpression(b, SystemTypes.Boolean, sctx);
      this.scope = savedScope;
      this.targetFor = savedTargetFor;
      labelList.Count = n;
      return exceptional;
    }
    // TODO: A local won't work for iterators.
    public override MethodContract VisitMethodContract(MethodContract contract){
      if ( contract == null ) return null;
      Method m = contract.DeclaringMethod;
      if (m == null) return contract;
      if ( m.ReturnType != null && m.ReturnType != SystemTypes.Void && contract.Ensures != null && contract.Ensures.Count > 0 ){
        ReplaceResult rr = new ReplaceResult();
        for ( int i = 0, n = contract.Ensures.Count; i < n; i++ ){
          if ( contract.Ensures[i] is EnsuresNormal ){
            contract.Ensures[i].PostCondition = (Expression) rr.Visit(contract.Ensures[i].PostCondition);
          }
        }
      }
      if (contract.DeclaringMethod != null && contract.DeclaringMethod.Template != null &&
        !contract.DeclaringMethod.IsNormalized) return contract;
      return base.VisitMethodContract(contract);
    }

    public override Field VisitField(Field field){
      if (field == null) return null;
      Scope savedScope = this.scope;
      BlockScope scope = new BlockScope(savedScope, null);
      scope.LexicalSourceExtent = field.SourceContext;
      this.scope = scope;
      this.AddToAllScopes(this.scope);
      Declarer declarer = this.GetDeclarer();
      declarer.VisitField(field, scope);
      field.Attributes = this.VisitAttributeList(field.Attributes);
      this.AbstractSealedUsedAsType = Error.AbstractSealedFieldType;
      field.Type = this.VisitTypeReference(field.Type, true);
      this.AbstractSealedUsedAsType = Error.NotAType;
      field.Initializer = this.VisitExpression(field.Initializer);
      if (field.IsLiteral){
        Literal lit = field.Initializer as Literal;
        if (lit != null) {
          field.DefaultValue = lit;
        }else{
          MemberBinding mb = field.Initializer as MemberBinding;
          if (mb != null){
            Field f = mb.BoundMember as Field;
            if (f != null && f.IsLiteral){
              field.DefaultValue = f.DefaultValue;
            }
          }
        }
      }
      this.AddNodePositionAndInfo(field.Name, new MemberBinding(null, field), IdentifierContexts.AllContext);
      Identifier prefix = field.Name.Prefix;
      if (prefix != null){
        AliasDefinition aliasDef = this.LookupAlias(prefix);
        if (aliasDef != null){
          prefix = aliasDef.AliasedUri;
          if (prefix == null) prefix = (Identifier)aliasDef.AliasedExpression;
        }
        if (prefix != null)
          field.Name.Prefix = prefix;
      }
      field.ImplementedInterfaces = this.VisitInterfaceReferenceList(field.ImplementedInterfaces);
      this.scope = savedScope;
      return field;
    }
    public override Expression VisitLiteral(Literal literal){
      if (literal == null) return null;
      literal.Type = this.VisitTypeReference(literal.Type);
      if (literal.Type == null) {
        switch (Convert.GetTypeCode(literal.Value)) {
          case TypeCode.Boolean: literal.Type = SystemTypes.Boolean; break;
          case TypeCode.Byte: literal.Type = SystemTypes.UInt8; break;
          case TypeCode.Char: literal.Type = SystemTypes.Char; break;
          case TypeCode.Decimal: literal.Type = SystemTypes.Decimal; break;
          case TypeCode.Double: literal.Type = SystemTypes.Double; break;
          case TypeCode.Empty: literal.Type = SystemTypes.Object; break;
          case TypeCode.Int16: literal.Type = SystemTypes.Int16; break;
          case TypeCode.Int32: literal.Type = SystemTypes.Int32; break;
          case TypeCode.Int64: literal.Type = SystemTypes.Int64; break;
          case TypeCode.Object: literal.Type = SystemTypes.Type; break;
          case TypeCode.SByte: literal.Type = SystemTypes.Int8; break;
          case TypeCode.Single: literal.Type = SystemTypes.Single; break;
          case TypeCode.String: literal.Type = SystemTypes.String; break;
          case TypeCode.UInt16: literal.Type = SystemTypes.UInt16; break;
          case TypeCode.UInt32: literal.Type = SystemTypes.UInt32; break;
          case TypeCode.UInt64: literal.Type = SystemTypes.UInt64; break;
        }
      }
      return literal;
    }
    public override Namespace VisitNamespace(Namespace nspace){
      if (nspace == null) return null;
      Identifier nsId = nspace.URI == null ? nspace.FullNameId : nspace.URI;
      Debug.Assert(nsId != null);
      if (nsId == null) nsId = nspace.Name;
      if (nsId == null) nsId = Identifier.Empty;
      Scope savedScope = this.scope;
      NamespaceScope nsscope = (NamespaceScope)this.scopeFor[nspace.UniqueKey];
      if (nsscope == null || nsscope.AssociatedNamespace == null || nsscope.AssociatedModule == null){Debug.Assert(false); return null;}
      this.scope = nsscope;
      this.AddToAllScopes(this.scope);
      this.AddNodePositionAndInfo(nsId, nspace, IdentifierContexts.NullContext);
      AliasDefinitionList aliases = nspace.AliasDefinitions;
      TrivialHashtable aliasedType = nsscope.AliasedType = new TrivialHashtable();
      TrivialHashtable aliasedNamespace = nsscope.AliasedNamespace = new TrivialHashtable();
      for (int i = 0, n = aliases == null ? 0 : aliases.Count; i < n; i++){
        AliasDefinition aliasDef = aliases[i];
        if (aliasDef == null || aliasDef.Alias == null) continue;
        int aliasIdKey = aliasDef.Alias.UniqueIdKey;
        if (aliasDef.AliasedAssemblies != null){
          AssemblyReferenceList arefs = this.currentModule == null ? null : this.currentModule.AssemblyReferences;
          for (int j = 0, m = arefs == null ? 0 : arefs.Count; j < m; j++){
            AssemblyReference aref = arefs[j];
            if (aref == null) continue;
            for (int jj = 0, nn = aref.Aliases == null ? 0 : aref.Aliases.Count; jj < nn; jj++){
              Identifier aliasId = aref.Aliases[jj];
              if (aliasId == null) continue;
              if (aliasId.UniqueIdKey == aliasIdKey){
                aliasDef.AliasedAssemblies.Add(aref);
                break;
              }
            }
          }
          continue;
        }
        if (aliasDef.AliasedExpression == null) continue;
        TypeNodeList duplicates = null;
        TypeNode aType = this.LookupType(aliasDef.AliasedExpression, nsscope, out duplicates);
        if (aType != null){
          aliasDef.AliasedType = new TypeReference(new TypeExpression(aliasDef.AliasedExpression, 0, aType.SourceContext), aType);
          aliasedType[aliasDef.Alias.UniqueIdKey] = aType;
          if (duplicates != null && duplicates.Count > 1){
            this.HandleError(aliasDef.AliasedExpression, Error.AmbiguousTypeReference, aliasDef.AliasedExpression.ToString());
            for (int j = 0, m = duplicates.Count; j < m; j++){
              TypeNode dup = duplicates[j];
              if (dup == null || dup == aliasDef.AliasedType) continue;
              this.HandleRelatedError(dup);
            }
          }
        }else{
          //TODO: need to leave the AliasedExpression alone (but check it)
          //Other parts of the Looker must change to stop expecting namespace qualifiers to be just dotted identifiers
          this.ExpandRootIfAnAlias(ref aliasDef.AliasedExpression, nsscope);
          aliasDef.AliasedExpression = this.ConvertToIdentifier(aliasDef.AliasedExpression);
          aliasDef.AliasedNamespace = this.GetNamespaceIfValidAndComplainIfNeeded(aliasDef.AliasedExpression);
          if (aliasDef.AliasedNamespace != null) 
            aliasedNamespace[aliasDef.Alias.UniqueIdKey] = aliasDef.AliasedNamespace;
          this.AddNodePositionAndInfo(aliasDef.AliasedExpression, aliasDef, IdentifierContexts.AllContext);
        }
      }
      UsedNamespaceList savedUsedNamespaces = this.UsedNamespaces;
      UsedNamespaceList usedNamespaces = this.UsedNamespaces = nspace.UsedNamespaces;
      for (int i = 0, n = usedNamespaces == null ? 0 : usedNamespaces.Count; i < n; i++){
        UsedNamespace unSpace = usedNamespaces[i];
        if (unSpace == null || unSpace.Namespace == null || unSpace.SourceContext.StartPos < nspace.SourceContext.StartPos) continue;
        if (unSpace.Namespace.Prefix == null){
          Identifier fullName = nsscope.GetNamespaceFullNameFor(unSpace.Namespace);
          if (fullName == null && nspace.Name != null && nspace.Name != Identifier.Empty){
            string nsName = nspace.Name.Name;
            string nsFullName = nspace.FullName;
            if (nsFullName != null && nsName != null && nsFullName.Length > nsName.Length) {
              string nsEnclosingName = nsFullName.Substring(0, nsFullName.Length - nsName.Length);
              Identifier id = new Identifier(nsEnclosingName + unSpace.Namespace);
              fullName = nsscope.GetNamespaceFullNameFor(id);
            }
          }
          if (fullName != null)
            unSpace.Namespace = new Identifier(fullName.Name, unSpace.Namespace.SourceContext);
          else
            this.GetNamespaceIfValidAndComplainIfNeeded(unSpace.Namespace);
        }else if (!this.IsPrefixForTheGlobalNamespace(unSpace.Namespace.Prefix)){
          Identifier expandedPrefix = nsscope.GetNamespaceFullNameFor(unSpace.Namespace.Prefix);
          if (expandedPrefix != null){
            expandedPrefix = new Identifier(expandedPrefix.Name, unSpace.Namespace.Prefix.SourceContext);
            QualifiedIdentifier qual = new QualifiedIdentifier(expandedPrefix, unSpace.Namespace, unSpace.Namespace.SourceContext);
            qual.Identifier = this.GetUnprefixedIdentifier(unSpace.Namespace);
            unSpace.Namespace = this.GetNamespaceIfValidAndComplainIfNeeded(qual);
          }else
            this.HandleError(unSpace.Namespace.Prefix, Error.AliasNotFound, unSpace.Namespace.Prefix.Name);
        }else
          this.GetNamespaceIfValidAndComplainIfNeeded(unSpace.Namespace);
        this.AddNodePositionAndInfo(unSpace.Namespace, unSpace, IdentifierContexts.AllContext);
      }
      AttributeList attributes = nspace.Attributes;
      if (attributes != null){
        nspace.Attributes = null;
        int n = attributes.Count;
        AttributeList assemblyAttributes = null;
        AttributeList moduleAttributes = null;
        AssemblyNode assem = this.currentModule as AssemblyNode;
        if (assem != null){
          assemblyAttributes = assem.Attributes;
          if (assemblyAttributes == null) assem.Attributes = assemblyAttributes = new AttributeList(n);
          moduleAttributes = assem.ModuleAttributes;
          if (moduleAttributes == null) assem.ModuleAttributes = moduleAttributes = new AttributeList(n);
        }else{
          moduleAttributes = this.currentModule.Attributes;
          if (moduleAttributes == null) this.currentModule.Attributes = moduleAttributes = new AttributeList(n);
        }
        for (int i = 0; i < n; i++){
          AttributeNode attr = this.VisitAttributeNode(attributes[i]);
          if (attr == null) continue;
          attributes[i] = null;
          if (attr.Target == AttributeTargets.Assembly && assem != null)
            assemblyAttributes.Add(attr);
          else if (attr.Target == AttributeTargets.Module)
            moduleAttributes.Add(attr);
          else
            attributes[i] = attr;
        }
      }
      TypeNodeList types = nspace.Types;
      for (int i = 0, n = types == null ? 0 : types.Count; i < n; i++)
        this.Visit(types[i]);
      NamespaceList nestedNamespaces = nspace.NestedNamespaces;
      for (int i = 0, n = nestedNamespaces == null ? 0 : nestedNamespaces.Count; i < n; i++)
        this.VisitNamespace(nestedNamespaces[i]);
      this.scope = savedScope;
      this.UsedNamespaces = savedUsedNamespaces;
      return nspace;
    }
    public virtual TypeNode LookupType(Expression name, NamespaceScope nsscope, out TypeNodeList duplicates){
      duplicates = null;
      if (name == null || nsscope == null){Debug.Assert(false); return null;}
      Identifier id = name as Identifier;
      if (id != null){
        if (id.Prefix != null){
          if (this.IsPrefixForTheGlobalNamespace(id.Prefix)){
            while (nsscope.Name != Identifier.Empty && nsscope.OuterScope is NamespaceScope)
              nsscope = (NamespaceScope)nsscope.OuterScope;
            return nsscope.GetType(id, out duplicates);
          }
          return this.LookupType(id.Prefix, id, nsscope, out duplicates);
        }
        AliasDefinition rootAlias = this.GetAliasDefinitionForRoot(id, nsscope);
        if (rootAlias != null) return (TypeNode)rootAlias.AliasedType;
        return nsscope.GetType(id, out duplicates);
      }
      QualifiedIdentifier qual = name as QualifiedIdentifier;
      if (qual == null){Debug.Assert(name is TemplateInstance); return this.LookupType(name as TemplateInstance);}
      return this.LookupType(qual.Qualifier, qual.Identifier, nsscope, out duplicates);
    }
    public virtual TypeNode LookupType(Expression qualifier, Identifier name, NamespaceScope nsscope, out TypeNodeList duplicates){
      duplicates = null;
      TypeNode declarer;
      TypeNodeList declarerDups;
      AliasDefinition rootAlias = this.GetAliasDefinitionForRoot(qualifier, nsscope);
      if (rootAlias != null){
        if (rootAlias.AliasedAssemblies != null){
          qualifier = this.ReplaceRoot(qualifier, rootAlias.Alias, null);
          if (qualifier != null){
            declarer = this.LookupType(qualifier, rootAlias.AliasedAssemblies, out declarerDups);
            return this.LookupNestedType(declarer, name, out duplicates, declarerDups);
          }else
            return this.LookupType(name, rootAlias.AliasedAssemblies, out declarerDups);
        }
        if (rootAlias.AliasedType != null){
          qualifier = this.ReplaceRoot(qualifier, rootAlias.Alias, null);
          if (qualifier != null){
            declarer = this.LookupNestedType((TypeNode)rootAlias.AliasedType, qualifier, out declarerDups, null);
            return this.LookupNestedType(declarer, name, out duplicates, declarerDups);
          }else
            return this.LookupNestedType((TypeNode)rootAlias.AliasedType, name, out duplicates, null);
        }
        if (rootAlias.AliasedExpression != null){
          qualifier = this.ReplaceRoot(qualifier, rootAlias.Alias, rootAlias.AliasedExpression);
          if (qualifier != null){
            return this.LookupType(new QualifiedIdentifier(qualifier, name), nsscope, out duplicates);
          }else
            return this.LookupType(name, nsscope, out duplicates);
        }
      }
      //First try to treat qualifier as just a namespace
      Identifier ns = this.ConvertToIdentifier(qualifier);
      if (ns == null){Debug.Assert(false); return null;}
      if (this.IsPrefixForTheGlobalNamespace(ns.Prefix)){
        while (nsscope.Name != Identifier.Empty && nsscope.OuterScope is NamespaceScope)
          nsscope = (NamespaceScope)nsscope.OuterScope;
      }
      TypeNode result = nsscope.GetType(ns, name, out duplicates);
      if (result != null) return result;
      //Now try to treat qualifier as a type
      declarer = this.LookupType(qualifier, nsscope, out declarerDups);
      return this.LookupNestedType(declarer, name, out duplicates, declarerDups);
    }
    public virtual TypeNode LookupType(Expression tname, AssemblyReferenceList arefs, out TypeNodeList duplicates){
      duplicates = null;
      Identifier name = tname as Identifier;
      Identifier nspace = Identifier.Empty;
      QualifiedIdentifier qual = tname as QualifiedIdentifier;
      if (qual != null){
        name = qual.Identifier;
        nspace = this.ConvertToIdentifier(qual.Qualifier);
      }
      TypeNode result = null;
      for (int i = 0, n = arefs == null ? 0 : arefs.Count; i < n; i++){
        AssemblyReference aref = arefs[i];
        if (aref == null) continue;
        AssemblyNode assem = aref.Assembly;
        if (assem == null) continue;
        TypeNode t = assem.GetType(nspace, name);
        if (t == null) continue;
        if (result != null){
          if (duplicates == null){
            duplicates = new TypeNodeList();
            duplicates.Add(result);
          }
          duplicates.Add(t);
          continue;
        }
        result = t;
      }
      return result;
    }
    public virtual TypeNode LookupNestedType(TypeNode declarer, Expression name, out TypeNodeList duplicates, TypeNodeList declarerDups){
      duplicates = null;
      if (declarer == null) return null;
      Identifier id = name as Identifier;
      QualifiedIdentifier qual = name as QualifiedIdentifier;
      if (qual != null){
        declarer = this.LookupNestedType(declarer, qual.Qualifier, out declarerDups, declarerDups);
        if (declarer == null) return null;
        id = qual.Identifier;
      }
      if (declarerDups == null) return this.GetTypeView(declarer).GetNestedType(id);
      TypeNode result = null;
      duplicates = new TypeNodeList();
      for (int i = 0, n = declarerDups.Count; i < n; i++){
        declarer = declarerDups[i];
        if (declarer == null) continue;
        result = this.GetTypeView(declarer).GetNestedType(id);
        if (result != null) duplicates.Add(result);
      }
      if (duplicates.Count == 0)
        duplicates = null;
      else{
        result = duplicates[0];
        if (duplicates.Count == 1) duplicates = null;
      }
      return result;
    }
    public virtual void ExpandRootIfAnAlias(ref Expression qualOrId, NamespaceScope nsscope){
      if (nsscope == null) return;
      if (qualOrId == null){Debug.Assert(false); return;}
      QualifiedIdentifier qual = qualOrId as QualifiedIdentifier;
      if (qual != null){this.ExpandRootIfAnAlias(ref qual.Qualifier, nsscope); return;}
      Identifier id = qualOrId as Identifier;
      if (id == null) return;
      NamespaceScope outerScope = nsscope.OuterScope as NamespaceScope;
      if (id.Prefix != null){
        AliasDefinition aliasDef = nsscope.GetAliasFor(id.Prefix);
        if (aliasDef != null && aliasDef.AliasedAssemblies != null){
          //TODO: pass it back up to the caller
          return;
        }
        if (outerScope == null) return;
        aliasDef = outerScope.GetAliasFor(id.Prefix);
        if (aliasDef != null){
          qualOrId = new QualifiedIdentifier(aliasDef.AliasedExpression, this.GetUnprefixedIdentifier(id), id.SourceContext);
          return;
        }
      }else{
        AliasDefinition aliasDef = nsscope.GetAliasFor(id);
        if (aliasDef != null && aliasDef.AliasedAssemblies != null){
          //TODO: pass it back up to the caller
          return;
        }
        if (outerScope == null) return;
        aliasDef = outerScope.GetAliasFor(id);
        if (aliasDef != null){
          qualOrId = aliasDef.AliasedExpression;
          return;
        }
      }
    }
    public virtual AliasDefinition GetAliasDefinitionForRoot(Expression qualOrId, NamespaceScope nsscope){
      if (nsscope == null) return null;
      if (qualOrId == null){Debug.Assert(false); return null;}
      QualifiedIdentifier qual = qualOrId as QualifiedIdentifier;
      if (qual != null) return this.GetAliasDefinitionForRoot(qual.Qualifier, nsscope);
      Identifier id = qualOrId as Identifier;
      if (id == null){Debug.Assert(false); return null;}
      NamespaceScope outerScope = nsscope.OuterScope as NamespaceScope;
      if (id.Prefix != null){
        AliasDefinition aliasDef = nsscope.GetAliasFor(id.Prefix);
        if (aliasDef != null && aliasDef.AliasedAssemblies != null) return aliasDef;
        if (outerScope == null) return null;
        return outerScope.GetAliasFor(id.Prefix);
      }else{
        AliasDefinition aliasDef = nsscope.GetAliasFor(id);
        if (aliasDef != null && aliasDef.AliasedAssemblies != null) return aliasDef;
        if (outerScope == null) return null;
        return outerScope.GetAliasFor(id);
      }
    }
    public virtual Expression ReplaceRoot(Expression root, Identifier rootIdOrPrefix, Expression replacement){
      if (root == null || rootIdOrPrefix == null){Debug.Assert(false); return null;}
      QualifiedIdentifier qualId = root as QualifiedIdentifier;
      if (qualId != null)
        return this.ReplaceRoot(qualId, rootIdOrPrefix, replacement);
      else
        return this.ReplaceRoot((Identifier)root, rootIdOrPrefix, replacement);
    }
    public virtual Expression ReplaceRoot(Identifier rootId, Identifier rootIdOrPrefix, Expression replacement){
      if (rootId == null || rootIdOrPrefix == null){Debug.Assert(false); return null;}
      if (rootId.UniqueIdKey == rootIdOrPrefix.UniqueIdKey) return replacement;
      if (rootId.Prefix == null || rootId.Prefix.UniqueIdKey != rootIdOrPrefix.UniqueIdKey){
        Debug.Assert(false); return null;
      }
      Identifier unprefixedRootId = this.GetUnprefixedIdentifier(rootId);
      if (replacement == null) return unprefixedRootId;
      return new QualifiedIdentifier(replacement, unprefixedRootId, rootId.SourceContext);
    }
    public virtual Expression ReplaceRoot(QualifiedIdentifier qual, Identifier rootIdOrPrefix, Expression replacement){
      if (qual == null || rootIdOrPrefix == null){Debug.Assert(false); return null;}
      QualifiedIdentifier qual2 = qual.Qualifier as QualifiedIdentifier;
      if (qual2 != null){
        Expression expr2 = this.ReplaceRoot(qual2, rootIdOrPrefix, replacement);
        return new QualifiedIdentifier(expr2, qual.Identifier, qual.SourceContext);
      }
      Expression newQual = this.ReplaceRoot((Identifier)qual.Qualifier, rootIdOrPrefix, replacement);
      return new QualifiedIdentifier(newQual, qual.Identifier, qual.SourceContext);
    }
    public override Expression VisitParameter(Parameter p){
      if (p == null) return null;
      p.Attributes = this.VisitAttributeList(p.Attributes);
      this.AbstractSealedUsedAsType = Error.AbstractSealedParameterType;
      p.Type = this.VisitTypeReference(p.Type, true);
      this.AbstractSealedUsedAsType = Error.NotAType;
      p.DefaultValue = this.VisitExpression(p.DefaultValue);
      ParameterField f = new ParameterField();
      f.Parameter = p;
      f.Flags = FieldFlags.CompilerControlled;
      f.Name = p.Name;
      f.Type = p.Type;
      f.DeclaringType = this.scope;
      this.scope.Members.Add(f);
      return p;
    }
    public override Property VisitProperty(Property property){
      if (property == null) return null;
      property.Attributes = this.VisitAttributeList(property.Attributes);
      property.Type = this.VisitTypeReference(property.Type, true);
      property.ImplementedTypes = this.VisitImplementedTypeList(property.ImplementedTypes);
      this.AddNodePositionAndInfo(property.Name, property, IdentifierContexts.AllContext);
      AttributeList methAttributes = null;
      AttributeList attributes = property.Attributes;
      for (int i = 0, n = attributes == null ? 0 : attributes.Count; i < n; i++){
        AttributeNode attr = this.VisitAttributeNode(attributes[i]);
        if (attr == null) continue;
        attributes[i] = null;
        if (attr.Target == AttributeTargets.Method){
          if (methAttributes == null) methAttributes = new AttributeList(n);
          methAttributes.Add(attr);
        }else
          attributes[i] = attr;
      }
      if (methAttributes != null){
        if (property.Getter != null)
          property.Getter.Attributes = methAttributes.Clone();
        if (property.Setter != null)
          property.Setter.Attributes = methAttributes.Clone();
        for (int i = 0, n = property.OtherMethods == null ? 0 : property.OtherMethods.Count; i < n; i++){
          Method m = property.OtherMethods[i];
          if (m == null) continue;
          m.Attributes = methAttributes.Clone();
        }
      }
      if (property.Getter == null && property.Setter == null)
        property.Parameters = this.VisitParameterList(property.Parameters);
      else
        property.Parameters = null; //Force property to regenerate from getter/setter
      return property;
    }
    public override Expression VisitThis(This This) {
      this.AddNodePositionAndInfo(This, This, IdentifierContexts.AllContext);
      return base.VisitThis(This);
    }
    public override Expression VisitBase(Base Base) {
      this.AddNodePositionAndInfo(Base, Base, IdentifierContexts.AllContext);
      return base.VisitBase(Base);
    }
    public override Expression VisitQualifiedIdentifier(QualifiedIdentifier qualifiedIdentifier){
      return this.VisitQualifiedIdentifier(qualifiedIdentifier, true);
    }
    public virtual Expression VisitQualifiedIdentifier(QualifiedIdentifier qualifiedIdentifier, bool outerQualifier){
      if (qualifiedIdentifier == null || qualifiedIdentifier.Identifier == null) return null;
      Identifier prefix = qualifiedIdentifier.Identifier.Prefix;
      if (prefix != null){
        AliasDefinition aliasDef = this.LookupAlias(prefix);
        if (aliasDef != null){
          prefix = aliasDef.AliasedUri;
          if (prefix == null) prefix = (Identifier)aliasDef.AliasedExpression;
        }
        if (prefix != null)
          qualifiedIdentifier.Identifier.Prefix = prefix;
      }
      Expression qualifier = qualifiedIdentifier.Qualifier;
      Identifier id = qualifier as Identifier;
      if (id != null)
        qualifier = this.VisitIdentifier(id); //TODO: alias binding
      else if (qualifier is QualifiedIdentifier)
        qualifier = this.VisitQualifiedIdentifier((QualifiedIdentifier)qualifier, false);
      else
        qualifier = this.VisitExpression(qualifier);
      AliasBinding aliasBinding = qualifier as AliasBinding;
      if (aliasBinding != null){
        this.AddNodePositionAndInfo(qualifiedIdentifier, aliasBinding.AliasDefinition, IdentifierContexts.AllContext);
        qualifier = id;
      }else
        this.AddNodePositionAndInfo(qualifiedIdentifier, qualifiedIdentifier, IdentifierContexts.AllContext);
      if (qualifier is Identifier || qualifier is QualifiedIdentifier || qualifier is NameBinding || (qualifier is Literal && ((Literal)qualifier).Value is TypeNode)) {
        //The qualifier got bound to a namespace or a type, so the qualified identifier itself may bind
        //to a namespace or type. Do the binding here rather than in Resolver.
        Int32List idPos = this.identifierPositions;
        this.identifierPositions = null; //Do not rebind a new info object to the identifier position
        TypeNode t = this.LookupType(qualifiedIdentifier, true, 0);
        this.identifierPositions = idPos;
        if (t != null){
          NameBinding nb = qualifier as NameBinding;
          if (this.BoundNameHides(nb, t.DeclaringType))
            t = null;
          else
            return new Literal(t, SystemTypes.Type, qualifiedIdentifier.SourceContext);
        }else if (outerQualifier){
          Identifier ns = this.GetNamespaceIfValidAndComplainIfNeeded(qualifier);
          if (ns != null)
            this.HandleError(qualifiedIdentifier, Error.NoSuchQualifiedType, ns.ToString(), qualifiedIdentifier.Identifier.ToString());
          else if (qualifier is Literal){
            t = (TypeNode)((Literal)qualifier).Value;
            MemberBinding mb = this.LookupLiteralBinding(qualifiedIdentifier, qualifiedIdentifier.Identifier, t);
            if (mb != null) return mb;
          }
        }
      }
      if (id != null && id.Prefix == null && qualifier is Literal && ((Literal)qualifier).Value is TypeNode){
        //Look for literal field
        TypeNode t = (TypeNode)((Literal)qualifier).Value;
        MemberBinding mb = this.LookupLiteralBinding(qualifiedIdentifier, id, t);
        if (mb != null) return mb;
      }
      if (qualifier != null)
        qualifiedIdentifier.Qualifier = qualifier;
      return qualifiedIdentifier;
    }
    public virtual bool BoundNameHides(NameBinding nb, TypeNode t) {
      if (nb == null) return false;
      for (int i = 0, n = nb.BoundMembers == null ? 0 : nb.BoundMembers.Count; i < n; i++) {
        Member mem = nb.BoundMembers[i];
        if (mem == null) continue;
        Property prop = mem as Property;
        if (prop != null) return TypeNode.StripModifiers(prop.Type) != t;
        Field f = mem as Field;
        if (f != null) return TypeNode.StripModifiers(f.Type) != t;
      }
      return false;
    }
    public virtual MemberBinding LookupLiteralBinding(QualifiedIdentifier qualifiedIdentifier, Identifier id, TypeNode t) {
      MemberList members = this.GetMatchingMembers(t, qualifiedIdentifier.Identifier);
      Field field = null;
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++) {
        Field f = members[i] as Field;
        if (f == null || !f.IsLiteral) continue;
        field = f;
        break;
      }
      if (field != null){
        MemberBinding mb = new MemberBinding(null, field, qualifiedIdentifier.SourceContext, id);
        mb.Type = null; //Force Resolver to fill in the type
        mb.BoundMemberExpression = qualifiedIdentifier;
        qualifiedIdentifier.Qualifier = new Literal(t, SystemTypes.Type, qualifiedIdentifier.Qualifier.SourceContext);
        return mb;
      }
      return null;
    }
    public virtual MemberList GetMatchingMembers(TypeNode t, Identifier id){
      Interface iface = t as Interface;
      if (iface != null) return ((Interface)this.GetTypeView(iface)).GetAllMembersNamed(id);
      while (t != null){
        MemberList result = this.GetTypeView(t).GetMembersNamed(id);
        if (result != null && result.Count > 0) return result;
        t = t.BaseType;
      }
      return null;
    }
    public override Expression VisitComprehension(Comprehension comprehension){
      if (comprehension == null) return null;
      if (comprehension.IsDisplay){
        comprehension.Elements = this.VisitExpressionList(comprehension.Elements);
        return comprehension;
      }
      Scope savedScope = this.scope;
      Scope scope = savedScope;
      Block filter = new Block(new StatementList());
      for (int i = 0, n = comprehension.BindingsAndFilters == null ? 0 : comprehension.BindingsAndFilters.Count; i < n; i++){
        ComprehensionBinding comprehensionBinding = comprehension.BindingsAndFilters[i] as ComprehensionBinding;
        if (comprehensionBinding != null){
          filter = new Block(new StatementList());
          scope = this.scope = comprehensionBinding.ScopeForTemporaryVariables = new BlockScope(scope, filter);
          this.AddToAllScopes(this.scope);
          comprehensionBinding.TargetVariableType = this.VisitTypeReference(comprehensionBinding.TargetVariableType, true);
          comprehensionBinding.AsTargetVariableType = this.VisitTypeReference(comprehensionBinding.AsTargetVariableType);
          comprehensionBinding.SourceEnumerable = this.VisitExpression(comprehensionBinding.SourceEnumerable);
          Identifier targetId = comprehensionBinding.TargetVariable as Identifier;
          if (targetId != null){
            Field f = new Field();
            f.DeclaringType = scope;
            f.Flags = FieldFlags.CompilerControlled|FieldFlags.InitOnly;
            f.Name = targetId;
            f.Type = comprehensionBinding.TargetVariableType;
            scope.Members.Add(f);
            comprehensionBinding.TargetVariable = new MemberBinding(new ImplicitThis(scope, 0), f);
            comprehensionBinding.TargetVariable.SourceContext = targetId.SourceContext;
            this.AddNodePositionAndInfo(targetId, comprehensionBinding.TargetVariable, IdentifierContexts.AllContext);
          }
        }else{ // it's a filter
          Expression qf = comprehension.BindingsAndFilters[i] = this.VisitExpression(comprehension.BindingsAndFilters[i]);
          if (qf != null)
            filter.Statements.Add(new ExpressionStatement(qf)); //REVIEW: why do this here? 
          //Also, does this assumes that qf will not be changed by a later visitor?
        }
      }
      if (comprehension.Elements != null && comprehension.Elements.Count > 0)
        comprehension.Elements[0] = this.VisitExpression(comprehension.Elements[0]);
      this.scope = savedScope;
      if (comprehension.Elements != null && comprehension.Elements.Count > 1)
        comprehension.Elements[1] = this.VisitExpression(comprehension.Elements[1]);
      return comprehension;
    }
    public virtual Identifier GetNamespaceIfValidAndComplainIfNeeded(Expression expression){
      Scope sc = this.scope;
      NamespaceScope nsSc = sc as NamespaceScope;
      while (sc != null && nsSc == null){
        sc = sc.OuterScope;
        nsSc = sc as NamespaceScope;
      }
      if (nsSc == null) return null;
      Identifier id = expression as Identifier;
      if (id != null){
        id = nsSc.GetNamespaceFullNameFor(id);
        if (id != null) return id;
        id = (Identifier)expression;
        if (id.Prefix != null){
          Node errorContext = this.GetUnprefixedIdentifier(id);
          if (this.IsPrefixForTheGlobalNamespace(id.Prefix))
            this.HandleError(errorContext, Error.GlobalSingleTypeNameNotFound, id.Name);
          else
            this.HandleError(errorContext, Error.NoSuchQualifiedType, id.Prefix.Name, id.Name);
        }
        return null;
      }
      QualifiedIdentifier qual = expression as QualifiedIdentifier;
      if (qual != null){
        id = this.GetNamespaceIfValidAndComplainIfNeeded(qual.Qualifier);
        if (id != null){
          if (this.IsPrefixForTheGlobalNamespace(id.Prefix)){
            while (nsSc.AssociatedNamespace != null && nsSc.AssociatedNamespace.Name != Identifier.Empty && nsSc.OuterScope is NamespaceScope)
              nsSc = (NamespaceScope)nsSc.OuterScope;
          }
          Identifier id2 = nsSc.GetNamespaceFullNameFor(Identifier.For(id.Name+"."+qual.Identifier.Name));
          if (id2 != null) return id2;
          this.HandleError(qual.Identifier, Error.NoSuchQualifiedType, id.Name, qual.Identifier.Name);
          return null;
        }
        return null;
      }
      return null;
    }
    public virtual Identifier GetUnprefixedIdentifier(Identifier id){
      if (id == null || id.Prefix == null) return id;
      Identifier result = (Identifier)id.Clone();
      result.Prefix = null;
      result.SourceContext = id.SourceContext;
      result.SourceContext.StartPos = id.Prefix.SourceContext.EndPos+2;
      return result;
    }
    public override Statement VisitReturn(Return Return){
      if (Return == null) return null;
      Return.Expression = this.VisitExpression(Return.Expression);
      if (Return.Expression != null && this.currentMethod.ReturnType == null) 
        this.currentMethod.ReturnType = SystemTypes.Object;
      return Return;
    }
    public override Statement VisitAcquire(Acquire acquire){
      if (@acquire == null) return null;
      if (@acquire.Condition != null)
        @acquire.ConditionFunction = new Construct(new Literal(SystemTypes.ThreadConditionDelegate, SystemTypes.Type), new ExpressionList(new AnonymousNestedFunction(null, new Block(new StatementList(new Return(@acquire.Condition))))));
      Scope savedScope = this.scope;
      Scope scope = this.scope = @acquire.ScopeForTemporaryVariable = new BlockScope(savedScope, @acquire.Body);
      this.AddToAllScopes(this.scope);
      Declarer declarer = new Declarer(this.ErrorHandler);
      declarer.VisitAcquire(@acquire, scope);
      @acquire.Target = (Statement) this.Visit(@acquire.Target);
      @acquire.Condition = this.VisitExpression(@acquire.Condition);
      @acquire.ConditionFunction = this.VisitExpression(@acquire.ConditionFunction);
      @acquire.Body = this.VisitBlock(@acquire.Body);
      this.scope = savedScope;
      return @acquire;
    }
    public override Statement VisitResourceUse(ResourceUse resourceUse){
      if (resourceUse == null) return null;
      Scope savedScope = this.scope;
      Scope scope = this.scope = resourceUse.ScopeForTemporaryVariable = new BlockScope(savedScope, resourceUse.Body);
      this.AddToAllScopes(this.scope);
      Declarer declarer = new Declarer(this.ErrorHandler);
      declarer.VisitResourceUse(resourceUse, scope);
      resourceUse.ResourceAcquisition = (Statement)this.Visit(resourceUse.ResourceAcquisition);
      resourceUse.Body = this.VisitBlock(resourceUse.Body);
      this.scope = savedScope;
      return resourceUse;
    }
    public override SwitchCase VisitSwitchCase(SwitchCase switchCase){
      if (switchCase == null) return null;
      switchCase.Label = this.VisitExpression(switchCase.Label);
      switchCase.Body = base.VisitBlock(switchCase.Body);
      return switchCase;
    }
    public override Statement VisitVariableDeclaration(VariableDeclaration variableDeclaration){
      if (variableDeclaration == null) return null;
      Field f = variableDeclaration.Field;
      this.AbstractSealedUsedAsType = Error.AbstractSealedLocalType;
      f.Type = this.VisitTypeReference(variableDeclaration.Type, true);
      this.AbstractSealedUsedAsType = Error.NotAType;
      Expression i = this.VisitExpression(variableDeclaration.Initializer);
      if (i == null) return null;
      return new AssignmentStatement(new MemberBinding(new ImplicitThis(this.scope, 0), f), i, variableDeclaration.SourceContext);
      //TODO: should not throw away the declaration since it makes source to source translatation more difficult
    }
    public override  Statement VisitLocalDeclarationsStatement(LocalDeclarationsStatement localDeclarations){
      if (localDeclarations == null) return null;
      this.AbstractSealedUsedAsType = Error.AbstractSealedLocalType;
      TypeNode type = localDeclarations.Type = this.VisitTypeReference(localDeclarations.Type, true);
      if (type == null){
        TypeExpression texpr = localDeclarations.TypeExpression as TypeExpression;
        if (texpr != null && texpr.ConsolidatedTemplateArguments != null && this.identifierInfos != null &&
          texpr.SourceContext.Document != null && texpr.SourceContext.Document.Text != null && texpr.SourceContext.EndPos == texpr.SourceContext.Document.Text.Length-1) {
          //Could be a call to a generic method.
          TemplateInstance ti = new TemplateInstance(texpr.Expression, texpr.ConsolidatedTemplateArguments);
          ti.SourceContext = texpr.SourceContext;
          ti.IsMethodTemplate = true;
          MethodCall mcall = new MethodCall(ti, new ExpressionList(0));
          mcall.SourceContext = texpr.SourceContext;
          return (Statement)this.Visit(new ExpressionStatement(mcall, mcall.SourceContext));
        }
      }
      this.AbstractSealedUsedAsType = Error.NotAType;
      LocalDeclarationList decls = localDeclarations.Declarations;
      int n = decls.Count;
      if (localDeclarations.Constant || localDeclarations.InitOnly){
        for (int i = 0; i < n; i++){
          LocalDeclaration decl = decls[i];
          if (decl == null) continue;
          decl = this.VisitLocalDeclaration(decl);
          Field f = decl.Field;
          if (f == null) continue;
          f.Type = type;
          f.Initializer = decl.InitialValue = this.VisitExpression(f.Initializer);
        }
        return localDeclarations;
      }else{
        StatementList statements = new StatementList(n+1);
        statements.Add(localDeclarations);
        for (int i = 0; i < n; i++){
          LocalDeclaration decl = decls[i];
          if (decl == null) continue;
          decl = this.VisitLocalDeclaration(decl);
          Field f = decl.Field;
          if (f == null) continue;
          f.Type = type;
          f.Initializer = null;
          MemberBinding mb = new MemberBinding(new ImplicitThis(this.scope, 0), f);
          mb.Type = type;
          mb.SourceContext = decl.Name.SourceContext;
          Expression initVal = decl.InitialValue = this.VisitExpression(decl.InitialValue);
          if (initVal == null) continue;
          AssignmentStatement aStat = new AssignmentStatement(mb, initVal, decl.AssignmentNodeType);
          aStat.SourceContext = localDeclarations.SourceContext;
          if (decl.SourceContext.Document != null) {
            aStat.SourceContext = decl.SourceContext;
            Debug.Assert(aStat.SourceContext.EndPos >= aStat.SourceContext.StartPos);
          }
          statements.Add(aStat);
        }
        Block b = new Block(statements);
        b.SourceContext = localDeclarations.SourceContext;
        return b;
      }
    }
    public override LocalDeclaration VisitLocalDeclaration(LocalDeclaration decl) {
      return decl;
    }    
    public override TypeNode VisitTypeNode(TypeNode typeNode){
      if (typeNode == null) return null;
      typeNode.Attributes = this.VisitAttributeList(typeNode.Attributes);
      typeNode.SecurityAttributes = this.VisitSecurityAttributeList(typeNode.SecurityAttributes);
      Scope savedScope = this.scope;
      TypeNode savedCurrentType = this.currentType;
      this.currentType = typeNode;
      if (typeNode.TemplateParameters != null || (typeNode.Template != null && typeNode.Template.TemplateParameters != null)){
        this.scope = new BlockScope(savedScope, null);
        this.AddToAllScopes(this.scope);
        if (typeNode.TemplateParameters != null)
          this.scope.TemplateParameters = typeNode.TemplateParameters;
        else
          this.scope.TemplateParameters = typeNode.Template.TemplateParameters;
      }
      typeNode.TemplateArguments = this.VisitTypeArgumentList(typeNode.TemplateArguments);
      typeNode.TemplateParameters = this.VisitTypeParameterList(typeNode.TemplateParameters);
      Class c = typeNode as Class;
      if (c != null) this.VisitBaseClassReference(c);
      typeNode.Interfaces = this.VisitInterfaceReferenceList(typeNode.Interfaces, c);
      this.scope = new TypeScope(savedScope, typeNode);
      this.AddToAllScopes(this.scope);
      this.scope.TemplateParameters = typeNode.TemplateParameters;
      if (typeNode.PartiallyDefines != null){
        this.currentType = typeNode.PartiallyDefines;
        if (c != null){
          if (this.currentType.BaseType == null)
            ((Class)this.currentType).BaseClass = c.BaseClass;
          else if (this.currentType.BaseType != c.BaseClass && this.hasExplicitBaseClass[c.UniqueKey] != null)
            this.HandleError(c.Name, Error.PartialClassesSpecifyMultipleBases, this.GetTypeName(this.currentType));
        }
        InterfaceList partialInterfaces = typeNode.Interfaces;
        if (partialInterfaces != null){
          InterfaceList completeInterfaces = this.currentType.Interfaces;
          if (completeInterfaces == null) this.currentType.Interfaces = completeInterfaces = new InterfaceList();
          TrivialHashtable completeInterfaceTable = new TrivialHashtable();
          for (int i = 0, n = completeInterfaces.Count; i < n; i++){
            Interface iface = completeInterfaces[i];
            if (iface == null) continue;
            completeInterfaceTable[iface.UniqueKey] = iface;
          }
          for (int i = 0, n = partialInterfaces.Count; i < n; i++){
            Interface iface = partialInterfaces[i];
            if (iface == null) continue;
            if (completeInterfaceTable[iface.UniqueKey] != null) continue;
            completeInterfaces.Add(iface);
            completeInterfaceTable[iface.UniqueKey] = iface;
          }
        }
      }
      this.InjectDefaultConstructor(typeNode);
      MemberList mems = typeNode.Members; // just visit this declaration's members, not extensions' (so don't do this.GetTypeView(typeNode)...)
      if (mems != null){
        for (int i = 0, m = mems.Count; i < m; i++){
          Member mem = mems[i];
          if (mem == null) continue;
          InFirstAttributeVisit = true;
          mem.Attributes = this.VisitAttributeList(mem.Attributes);
          InFirstAttributeVisit = false;
        }
        for (int i = 0, m = mems.Count; i < m; i++){
          Member mem = mems[i];
          if (mem == null || mem is ITypeParameter) continue;
          this.Visit(mems[i]);
        }
      }
      DelegateNode delegateNode = typeNode as DelegateNode;
      if (delegateNode != null){
        delegateNode.Parameters = this.VisitDelegateParameters(delegateNode.Parameters);
        TypeNode rType = this.VisitTypeReference(delegateNode.ReturnType, true);      
        if (rType == SystemTypes.DynamicallyTypedReference || rType == SystemTypes.ArgIterator){
          this.HandleError(delegateNode.ReturnType, Error.CannotReturnTypedReference, this.GetTypeName(rType));
          rType = SystemTypes.Object;
        }
        delegateNode.ReturnType = rType;
        if (delegateNode.ProvideTypeMembers == null) delegateNode.ProvideMembers();
      }
      typeNode.Contract = this.VisitTypeContract(typeNode.Contract);
      this.scope = savedScope;
      this.currentType = savedCurrentType;
      return typeNode;
    }
    public virtual InstanceInitializer InjectDefaultConstructor(TypeNode typeNode){
      //This is an extension point
      return null;
    }
    public override InterfaceList VisitInterfaceReferenceList(InterfaceList interfaces){
      return this.VisitInterfaceReferenceList(interfaces, null);
    }
    public virtual InterfaceList VisitInterfaceReferenceList(InterfaceList interfaces, Class declaringClass){
      if (interfaces == null) return null;
      bool allowClass = declaringClass != null && declaringClass.BaseClassExpression == null;
      TrivialHashtable interfaceAlreadySeen = new TrivialHashtable();
      for (int i = 0, n = interfaces.Count; i < n; i++) {
        Interface iface = (Interface)this.VisitInterfaceReference(interfaces[i]);
        if (iface != null && interfaceAlreadySeen[iface.UniqueKey] != null){
          this.HandleError(interfaces[i], Error.DuplicateInterfaceInBaseList, this.GetTypeName(iface));
          interfaces[i] = null;
        }else{
          if (iface != null && iface.ObsoleteAttribute != null)
            this.CheckForObsolesence(interfaces[i], iface);
          InterfaceExpression ifExpr = interfaces[i] as InterfaceExpression;
          if (ifExpr != null){
            if (i == 0 && allowClass){
              ClassExpression cExpr = new ClassExpression(ifExpr.Expression, ifExpr.TemplateArguments, ifExpr.SourceContext);
              cExpr.DeclaringType = declaringClass;
              this.AddNodePositionAndInfo(ifExpr.Expression, cExpr, IdentifierContexts.TypeContext);
            }else
              this.AddNodePositionAndInfo(ifExpr.Expression, ifExpr, IdentifierContexts.TypeContext);
          }
          interfaces[i] = iface;
          if (iface == null) continue;
          interfaceAlreadySeen[iface.UniqueKey] = iface;
        }
      }
      return interfaces;
    }
    public override Invariant VisitInvariant(Invariant @invariant){
      Scope savedScope = this.scope;
      Method savedMethod = this.currentMethod;
      this.currentMethod = this.currentType.Contract.InvariantMethod;
      this.scope = this.currentMethod.Scope;
      this.AddToAllScopes(this.scope);
      Invariant inv = base.VisitInvariant (@invariant);
      this.scope = savedScope;
      this.currentMethod = savedMethod;
      return inv;
    }
    public override Statement VisitLabeledStatement(LabeledStatement lStatement) {
      this.scopeFor[lStatement.UniqueKey] = this.scope;
      return base.VisitLabeledStatement(lStatement);
    }
    public virtual void VisitBaseClassReference(Class Class){
      if (Class == null) return;
      if (Class.Name.UniqueIdKey == StandardIds.CapitalObject.UniqueIdKey && Class.Namespace.UniqueIdKey == StandardIds.System.UniqueIdKey) {
        Class.BaseClass = null; Class.BaseClassExpression = null;
        return;
      }
      if (Class.PartiallyDefines is Class && ((Class)Class.PartiallyDefines).BaseClass == null) 
        this.VisitBaseClassReference((Class)Class.PartiallyDefines);
      TypeNodeList partialTypes = Class.IsDefinedBy;
      if (partialTypes != null){
        for (int i = 0, n = partialTypes == null ? 0 : partialTypes.Count; i < n; i++){
          Class partialClass = partialTypes[i] as Class;
          if (partialClass == null || partialClass.BaseClass == SystemTypes.Object) continue;
          partialClass.PartiallyDefines = null; //Stop recursion
          this.VisitBaseClassReference(partialClass);
          partialClass.PartiallyDefines = Class;
          if (partialClass.BaseClass == SystemTypes.Object) continue;
          Class.BaseClass = partialClass.BaseClass;
          return;
        }
      }
      //Visit Class.BaseClass, but guard against doing it twice 
      //(this routine can get called when visiting a derived class that occurs lexically before Class)
      if (Class.BaseClassExpression == Class) return; //Still resolving the base class. This is a recursive call due to a recursive type argument.
      if (Class.BaseClass != null) {
        ClassExpression cExpr = Class.BaseClass as ClassExpression;
        if (cExpr == null) return; //Been here before and found a base class
        this.VisitBaseClassReference(Class, true);
        return;
      }
      //Leaving the BaseClass null is the convention for asking that the first expression in the interface list be treated as the base class, if possible.
      //If not possible, the convention is to set BaseClass equal to SystemTypes.Object.
      InterfaceList interfaces = Class.Interfaces;
      InterfaceList interfaceExpressions = Class.InterfaceExpressions;
      if (interfaces != null && interfaces.Count > 0){
        Interface iface = interfaces[0];
        InterfaceExpression ifExpr = iface as InterfaceExpression;
        if (ifExpr != null){
          ClassExpression cExpr = new ClassExpression(ifExpr.Expression, ifExpr.TemplateArguments, ifExpr.SourceContext);
          Class.BaseClass = Class.BaseClassExpression = cExpr;
          if (this.VisitBaseClassReference(Class, false)){
            //The first expression is not meant as an interface, remove it from the list
            int n = interfaces.Count-1;
            InterfaceList actualInterfaces = new InterfaceList(n);
            InterfaceList actualInterfaceExpressions = new InterfaceList(n);
            for (int i = 0; i < n; i++) {
              actualInterfaces.Add(interfaces[i+1]);
              if (interfaceExpressions != null)
                actualInterfaceExpressions.Add(interfaceExpressions[i+1]);
            }
            Class.Interfaces = actualInterfaces;
            if (interfaceExpressions != null)
              Class.InterfaceExpressions = actualInterfaceExpressions;
          }else{
            Class.BaseClass = SystemTypes.Object; Class.BaseClassExpression = null;
          }
        }else{
          Class.BaseClass = SystemTypes.Object; Class.BaseClassExpression = null;
        }
      }else{
        Class.BaseClass = SystemTypes.Object; Class.BaseClassExpression = null;
      }
      if (Class.BaseClass != null && Class.BaseClass != SystemTypes.Object) {
        this.VisitBaseClassReference(Class.BaseClass.Template as Class);
        this.VisitBaseClassReference(Class.BaseClass);
      }
    }
    public virtual bool FirstTypeDependsOnSecond(TypeNode t1, TypeNode t2){
      if (t1 == null || t2 == null) return false;
      if (t1 == t2) return true;
      TypeNode t = t1.BaseType;
      while (t != null && t != SystemTypes.Object && t != t1){
        if (t == t2) return true;
        t = t.BaseType;
      }
      if (t1 is ITypeParameter) return false;
      t = t1.DeclaringType;
      while (t != null){
        if (this.FirstTypeDependsOnSecond(t, t2)) return true;
        t = t.DeclaringType;
      }
      return false;
    }
    /// <summary>
    /// Turns ClassExpression Class.BaseClass into a real class, with substitution of template parameters.
    /// Returns false if the ClassExpression does not bind to a class. 
    /// Passing false for interfaceIsError will suppress the generation of an error message.
    /// This can be used to check if the first expression in an interface list is a base class.
    /// </summary>
    public virtual bool VisitBaseClassReference(Class Class, bool interfaceIsError){
      if (Class == null){Debug.Assert(false); return true;}
      Scope savedScope = this.scope;
      TypeScope clScope = null;
      if (Class.Template != null)
        clScope = this.scopeFor[Class.Template.UniqueKey] as TypeScope;
      else
        clScope = this.scopeFor[Class.UniqueKey] as TypeScope;
      if (clScope == null){return true;} //Can happen if an old scopeFor table is used together with a new source that has only just had Class added to it.
      this.scope = clScope.OuterScope;
      this.AddToAllScopes(this.scope);
      if (Class.TemplateParameters != null){
        this.scope = new BlockScope(this.scope, null);
        this.AddToAllScopes(this.scope);
        this.scope.TemplateParameters = Class.TemplateParameters;
      }
      ClassExpression cExpr = Class.BaseClass as ClassExpression;
      if (cExpr != null){
        Class.BaseClass = Class;
        TypeNode t = this.LookupType(cExpr.Expression, false, cExpr.TemplateArguments == null ? 0 : cExpr.TemplateArguments.Count);
        Class.BaseClass = null;
        if (t is Class){
          this.hasExplicitBaseClass[Class.UniqueKey] = t;
          if (this.FirstTypeDependsOnSecond(t, Class)){
            this.HandleError(Class.Name, Error.CircularBase, this.GetTypeName(t), this.GetTypeName(Class));
            this.HandleRelatedError(t);
            t = SystemTypes.Object;
          }else{
            Class.BaseClassExpression = Class;
            cExpr.TemplateArguments = this.VisitTypeReferenceList(cExpr.TemplateArguments);
            Class.BaseClassExpression = cExpr;
            if (cExpr.TemplateArguments != null){
              t = t.GetTemplateInstance(this.currentType, cExpr.TemplateArguments);
              t.TemplateExpression = cExpr;
              this.AddTemplateInstanceToList(t);
            }
          }
          if (!this.TypeIsVisible(t))
            t = SystemTypes.Object;
          if (t.ObsoleteAttribute != null)
            this.CheckForObsolesence(cExpr, t);      
          Class.BaseClass = (Class)t;
        }else if (t != null){
          if (!(t is Interface)){
            this.HandleError(cExpr, Error.CannotDeriveFromSealedType, this.GetTypeName(t), this.GetTypeName(Class));
            this.HandleRelatedError(t);
            Class.BaseClass = SystemTypes.Object;
          }else if (interfaceIsError){
            this.HandleError(cExpr, Error.CannotDeriveFromInterface, this.GetTypeName(t), this.GetTypeName(Class));
            this.HandleRelatedError(t);
          }
        }
      }
      this.scope = savedScope;
      return Class.BaseClass != null;
    }
    public override Expression VisitTemplateInstance(TemplateInstance templateInstance){
      if (templateInstance == null) return null;
      int n = templateInstance.TypeArgumentExpressions == null ? 0 : templateInstance.TypeArgumentExpressions.Count;
      TypeNode template = this.LookupType(templateInstance.Expression, false, n);
      if (template == null)
        templateInstance.Expression = this.VisitExpression(templateInstance.Expression);
      templateInstance.TypeArguments = this.VisitTypeReferenceList(templateInstance.TypeArguments);
      this.AddNodePositionAndInfo(templateInstance.Expression, templateInstance, templateInstance.IsMethodTemplate ? IdentifierContexts.AllContext : IdentifierContexts.TypeContext);
      this.AddNodePositionAndInfo(templateInstance, templateInstance, templateInstance.IsMethodTemplate ? IdentifierContexts.AllContext : IdentifierContexts.TypeContext);
      if (template != null) {
        if (template.TemplateParameters != null && template.TemplateParameters.Count == n) {
          TypeNode instance = template.GetTemplateInstance(this.currentType, templateInstance.TypeArguments);
          this.AddTemplateInstanceToList(instance);
          return new Literal(instance, SystemTypes.Type, templateInstance.SourceContext);
        }
        templateInstance.Expression = new Literal(template, SystemTypes.Type, templateInstance.Expression.SourceContext);
      }
      return templateInstance;
    }
    public override TypeAlias VisitTypeAlias(TypeAlias tAlias){
      if (this.currentType == null)
        this.currentType = tAlias;
      TypeAlias result = (TypeAlias)base.VisitTypeAlias(tAlias);
      if (this.currentType == tAlias)
        this.currentType = null;
      if (result == null) return null;
      result.ProvideMembers();
      return result;
    }
    public override TypeNode VisitTypeParameter(TypeNode typeParameter){
      typeParameter = base.VisitTypeParameter(typeParameter);
      if (typeParameter == null) return null;
      InterfaceList interfaces = this.GetTypeView(typeParameter).Interfaces;
      MemberList defaultMembers = null;
      for (int i = 0, n = interfaces == null ? 0 : interfaces.Count; i < n; i++){
        Interface iface = interfaces[i];
        if (iface == null) continue;
        MemberList defMembers = this.GetTypeView(iface).DefaultMembers;
        for (int j = 0, m = defMembers == null ? 0 : defMembers.Count; j < m; j++){
          Member defMember = defMembers[j];
          if (defMember == null) continue;
          if (defaultMembers == null) defaultMembers = new MemberList();
          defaultMembers.Add(defMember);
        }
      }
      typeParameter.DefaultMembers = defaultMembers;
      return typeParameter;
    }
    public TrivialHashtable alreadyWarnedAboutTrivialInvariance;
    public TrivialHashtable alreadyWarnedAboutTrivialNonNull;
    public override TypeNode VisitTypeReference(TypeNode type) {
      return this.VisitTypeReference(type, false);
    }
    public virtual TypeNode VisitTypeReference(TypeNode type, bool addNonNullWrapperIfNeeded){
      if (type == null) return null;
      TypeNode t = null;
      switch (type.NodeType){
        case NodeType.ClassExpression : 
          return this.VisitClassExpression((ClassExpression)type);
        case NodeType.InterfaceExpression : 
          return this.LookupInterface((InterfaceExpression)type);
        case NodeType.TypeExpression :
          if (addNonNullWrapperIfNeeded)
            return this.AddNonNullWrapperIfNeeded(this.VisitTypeExpression((TypeExpression)type));
          else
            return this.VisitTypeExpression((TypeExpression)type);
        case NodeType.ArrayTypeExpression:
          if (addNonNullWrapperIfNeeded)
            return this.AddNonNullWrapperIfNeeded(this.VisitArrayTypeExpression((ArrayTypeExpression)type));
          else
            return this.VisitArrayTypeExpression((ArrayTypeExpression)type);
        case NodeType.FlexArrayTypeExpression:
          return this.VisitFlexArrayTypeExpression((FlexArrayTypeExpression)type);
        case NodeType.FunctionTypeExpression:
          Scope savedScope = this.scope;
          this.scope = new MethodScope(savedScope.BaseClass, null);
          this.AddToAllScopes(this.scope);
          FunctionTypeExpression ftExpr = (FunctionTypeExpression)type;
          t = this.VisitTypeReference(ftExpr.ReturnType, true);
          ParameterList pars = this.VisitParameterList(ftExpr.Parameters);
          t = FunctionType.For(t, pars, this.currentType);
          this.scope = savedScope;
          return t;
        case NodeType.PointerTypeExpression:
          PointerTypeExpression ptrExpr = (PointerTypeExpression)type;
          TypeNode et = this.VisitTypeReference(ptrExpr.ElementType, true);
          if (et == null) return null;
          type = et.GetPointerType();
          if (et.Name == Looker.NotFound){
            ptrExpr.Name = Looker.NotFound;
            ptrExpr.SourceContext = ptrExpr.ElementType.SourceContext;
            return ptrExpr;
          }else if (!et.IsUnmanaged && (this.currentOptions == null || !this.currentOptions.NoStandardLibrary)){ //Hack
            //TODO: move this to Checker
            this.HandleError(ptrExpr, Error.ManagedAddr, this.GetTypeName(et));
            return null;
          }
          return type;
        case NodeType.ReferenceTypeExpression:
          ReferenceTypeExpression rExpr = (ReferenceTypeExpression)type;
          et = this.VisitTypeReference(rExpr.ElementType, true);
          if (et == null) return null;
          type = et.GetReferenceType();
          if (et.Name == Looker.NotFound){
            rExpr.Name = Looker.NotFound;
            rExpr.SourceContext = rExpr.ElementType.SourceContext;
            return rExpr;
          }
          return type;
        case NodeType.StreamTypeExpression:
          StreamTypeExpression sExpr = (StreamTypeExpression)type;
          et = this.VisitTypeReference(sExpr.ElementType);
          if (et == null) return null;
          if (et.Name == Looker.NotFound) return sExpr;
          TypeNode template = et.Template;
          if (template == SystemTypes.GenericBoxed){
            this.HandleError(sExpr, Error.RedundantStream, this.GetTypeName(et));
            return SystemTypes.GenericIEnumerable.GetTemplateInstance(this.currentType, this.GetStreamElementType(et));
          }
          if (template == SystemTypes.GenericIEnumerable){
            this.HandleError(sExpr, Error.RedundantStream, this.GetTypeName(et));
            return et;
          }
          if (template == SystemTypes.GenericNonEmptyIEnumerable){
            this.HandleError(sExpr, Error.BadStreamOnNonNullStream, this.GetTypeName(et));
            return SystemTypes.GenericIEnumerable.GetTemplateInstance(this.currentType, this.GetStreamElementType(et));
          }
          if (template == SystemTypes.GenericNonNull || template == SystemTypes.GenericInvariant){
            this.HandleError(sExpr, Error.BadStream, this.GetTypeName(et));
            return SystemTypes.GenericIEnumerable.GetTemplateInstance(this.currentType, this.GetStreamElementType(et));
          }
          return SystemTypes.GenericIEnumerable.GetTemplateInstance(this.currentType, et);
        case NodeType.NonEmptyStreamTypeExpression:
          NonEmptyStreamTypeExpression nsExpr = (NonEmptyStreamTypeExpression)type;
          et = this.VisitTypeReference(nsExpr.ElementType);
          if (et == null) return null;
          if (et.Name == Looker.NotFound) return nsExpr;
          template = et.Template;
          if (template == SystemTypes.GenericBoxed){
            this.HandleError(nsExpr, Error.BadNonNull, this.GetTypeName(et));
            return SystemTypes.GenericNonEmptyIEnumerable.GetTemplateInstance(this.currentType, this.GetStreamElementType(et));
          }
          if (template == SystemTypes.GenericIEnumerable){
            this.HandleError(nsExpr, Error.BadNonEmptyStream, this.GetTypeName(et));
            return SystemTypes.GenericNonEmptyIEnumerable.GetTemplateInstance(this.currentType, this.GetStreamElementType(et));
          }
          if (template == SystemTypes.GenericNonEmptyIEnumerable){
            this.HandleError(nsExpr, Error.RedundantNonNull, this.GetTypeName(et));
            return et;
          }
          if (template == SystemTypes.GenericNonNull || template == SystemTypes.GenericInvariant){
            //TODO: think of better error message
            this.HandleError(nsExpr, Error.RedundantNonNull, this.GetTypeName(et));
            return SystemTypes.GenericNonEmptyIEnumerable.GetTemplateInstance(this.currentType, this.GetStreamElementType(et));
          }
          return SystemTypes.GenericNonEmptyIEnumerable.GetTemplateInstance(this.currentType, et);
        case NodeType.NonNullableTypeExpression:
          NonNullableTypeExpression nnExpr = (NonNullableTypeExpression)type;
          et = this.VisitTypeReference(nnExpr.ElementType);
          if (et == null) return null;
          if (et.Name == Looker.NotFound) return nnExpr;
          if (this.NonNullChecking && nnExpr.ElementType.NodeType == NodeType.NonNullableTypeExpression){
            this.HandleError(nnExpr, Error.RedundantNonNull, this.GetTypeName(et));
            return et; // Just silently normalize it to one bang
          }
          template = et.Template;
          if (template == SystemTypes.GenericBoxed){
            this.HandleError(nnExpr, Error.BadNonNull, this.GetTypeName(et));
            return SystemTypes.GenericNonNull.GetTemplateInstance(this.currentType, this.GetStreamElementType(et));
          }
          if (template == SystemTypes.GenericIEnumerable && !this.NonNullGenericIEnumerableIsAllowed){
            this.HandleError(nnExpr, Error.BadNonNullOnStream, this.GetTypeName(et));
            return SystemTypes.GenericNonEmptyIEnumerable.GetTemplateInstance(this.currentType, this.GetStreamElementType(et));
          }
          if (template == SystemTypes.GenericInvariant){
            this.HandleError(nnExpr, Error.RedundantNonNull, this.GetTypeName(et));
            return SystemTypes.GenericNonNull.GetTemplateInstance(this.currentType, this.GetStreamElementType(et));
          }
          if (template == SystemTypes.GenericNonNull || template == SystemTypes.GenericNonEmptyIEnumerable){
            this.HandleError(nnExpr, Error.RedundantNonNull, this.GetTypeName(et));
            return et;
          }
          if (et.IsValueType && (this.currentType == null || this.currentType.Template == null)){
            if (this.alreadyWarnedAboutTrivialNonNull == null) this.alreadyWarnedAboutTrivialNonNull = new TrivialHashtable();
            if (this.alreadyWarnedAboutTrivialNonNull[et.UniqueKey] == null){
              this.HandleError(nnExpr, Error.ValueTypeIsAlreadyNonNull, this.GetTypeName(et));
              this.alreadyWarnedAboutTrivialNonNull[et.UniqueKey] = et;
            }
            return et;
          }
          if (this.NonNullChecking){
            et = TypeNode.StripModifier(et, SystemTypes.NonNullType);
            return OptionalModifier.For(SystemTypes.NonNullType, et);
          }
          else return et;
        case NodeType.NullableTypeExpression:
          NullableTypeExpression nuExpr = (NullableTypeExpression)type;
          et = this.VisitTypeReference(nuExpr.ElementType);
          if (et == null) return null;
          if (et.Name == Looker.NotFound) return nuExpr;
          template = et.Template;
          if (et.IsValueType)
            return SystemTypes.GenericNullable.GetTemplateInstance(this.currentType, et);
          else {
            if (et.IsTemplateParameter) {
              return OptionalModifier.For(SystemTypes.NullableType, et);
            }
            return et;
          }
        case NodeType.NonNullTypeExpression:
          NonNullTypeExpression nbExpr = (NonNullTypeExpression)type;
          et = this.VisitTypeReference(nbExpr.ElementType);
          if (et == null) return null;
          if (et.Name == Looker.NotFound) return nbExpr;
          template = et.Template;
          if (template == SystemTypes.GenericBoxed){
            this.HandleError(nbExpr, Error.BadNonNull, this.GetTypeName(et));
            return SystemTypes.GenericNonNull.GetTemplateInstance(this.currentType, this.GetStreamElementType(et));
          }
          if (template == SystemTypes.GenericIEnumerable){
            this.HandleError(nbExpr, Error.BadNonNullOnStream, this.GetTypeName(et));
            return SystemTypes.GenericNonEmptyIEnumerable.GetTemplateInstance(this.currentType, this.GetStreamElementType(et));
          }
          if (template == SystemTypes.GenericInvariant){
            this.HandleError(nbExpr, Error.RedundantNonNull, this.GetTypeName(et));
            return SystemTypes.GenericNonNull.GetTemplateInstance(this.currentType, this.GetStreamElementType(et));
          }
          if (template == SystemTypes.GenericNonNull || template == SystemTypes.GenericNonEmptyIEnumerable){
            this.HandleError(nbExpr, Error.RedundantNonNull, this.GetTypeName(et));
            return et;
          }
          if (et.IsValueType && (this.currentType == null || this.currentType.Template == null)) {
            if (this.alreadyWarnedAboutTrivialNonNull == null) this.alreadyWarnedAboutTrivialNonNull = new TrivialHashtable();
            if (this.alreadyWarnedAboutTrivialNonNull[et.UniqueKey] == null){
              this.HandleError(nbExpr, Error.ValueTypeIsAlreadyNonNull, this.GetTypeName(et));
              this.alreadyWarnedAboutTrivialNonNull[et.UniqueKey] = et;
            }
            return et;
          }
          return SystemTypes.GenericNonNull.GetTemplateInstance(this.currentType, et);
        case NodeType.BoxedTypeExpression:
          //TODO: distribute over tupleExpression before visiting element type reference
          BoxedTypeExpression bExpr = (BoxedTypeExpression)type;
          et = this.VisitTypeReference(bExpr.ElementType);
          if (et == null) return null;
          if (et.Name == Looker.NotFound) return bExpr;
          template = et.Template;
          if (template == SystemTypes.GenericBoxed || template == SystemTypes.GenericIEnumerable){
            this.HandleError(bExpr, Error.RedundantBox, this.GetTypeName(et));
            return et;
          }
          if (template == SystemTypes.GenericNonNull){
            this.HandleError(bExpr, Error.BadBox, this.GetTypeName(et));
            return SystemTypes.GenericBoxed.GetTemplateInstance(this.currentType, this.GetStreamElementType(et));
          }
          if (template == SystemTypes.GenericNonEmptyIEnumerable){
            this.HandleError(bExpr, Error.BadBox, this.GetTypeName(et));
            return SystemTypes.GenericIEnumerable.GetTemplateInstance(this.currentType, this.GetStreamElementType(et));
          }
          return SystemTypes.GenericBoxed.GetTemplateInstance(this.currentType, et);
        case NodeType.InvariantTypeExpression:
          InvariantTypeExpression invExpr = (InvariantTypeExpression)type;
          et = this.VisitTypeReference(invExpr.ElementType);
          if (et == null) return null;
          if (et.Name == Looker.NotFound) return invExpr;
          if (et.IsValueType){
            if (this.alreadyWarnedAboutTrivialInvariance == null) this.alreadyWarnedAboutTrivialInvariance = new TrivialHashtable();
            if (this.alreadyWarnedAboutTrivialInvariance[et.UniqueKey] == null){
              this.HandleError(invExpr, Error.ValueTypeIsAlreadyInvariant, this.GetTypeName(et));
              this.alreadyWarnedAboutTrivialInvariance[et.UniqueKey] = et;
            }
            return et;
          }
          if (et.IsSealed){
            if (this.alreadyWarnedAboutTrivialInvariance == null) this.alreadyWarnedAboutTrivialInvariance = new TrivialHashtable();
            if (this.alreadyWarnedAboutTrivialInvariance[et.UniqueKey] == null){
              this.HandleError(invExpr, Error.SealedTypeIsAlreadyInvariant, this.GetTypeName(et));
              this.alreadyWarnedAboutTrivialInvariance[et.UniqueKey] = et;
            }
            return SystemTypes.GenericNonNull.GetTemplateInstance(this.currentType, et);
          }
          return SystemTypes.GenericInvariant.GetTemplateInstance(this.currentType, et);
        case NodeType.TupleTypeExpression:
          TupleTypeExpression tupExpr = (TupleTypeExpression)type;
          FieldList domains = tupExpr.Domains;
          int n = domains == null ? 0 : domains.Count;
          for (int i = 0; i < n; i++){
            Field f = domains[i];
            if (f == null) continue;
            et = this.VisitTypeReference(f.Type, true);
            if (et == null) return null;
            f.Type = et;
          }
          return TupleType.For(domains, this.currentType);
        case NodeType.TypeAlias:
          TypeAlias tAlias = (TypeAlias)type;
          tAlias.AliasedType = this.VisitTypeReference(tAlias.AliasedType, true);
          return tAlias;
        case NodeType.TypeIntersectionExpression:
          TypeIntersectionExpression tix = (TypeIntersectionExpression)type;
          //TODO: check that all types are interfaces, except the first, which may be an unsealed class
          this.VisitTypeReferenceList(tix.Types);
          return TypeIntersection.For(tix.Types, this.currentType);
        case NodeType.TypeUnionExpression:
          TypeUnionExpression tux = (TypeUnionExpression)type;
          this.VisitTypeReferenceList(tux.Types);
          //TODO: give errors if types are duplicated or if supertype and subtype both appear
          if (tux.Types == null || tux.Types.Count == 0)
            return null; //TODO: give an error
          return TypeUnion.For(tux.Types, this.currentType);
        case NodeType.OptionalModifierTypeExpression:{
          OptionalModifierTypeExpression omExpr = (OptionalModifierTypeExpression)type;
          TypeNode modType = this.VisitTypeReference(omExpr.ModifiedType);
          TypeNode modifier = this.VisitTypeReference(omExpr.Modifier);
          if (modType == null) return null;
          if (modifier == null) return null;
          if (modType.Name == Looker.NotFound) return omExpr;
          if (modifier.Name == Looker.NotFound) return omExpr;
          return OptionalModifier.For(modifier, modType);
        }
        case NodeType.RequiredModifierTypeExpression:{
          RequiredModifierTypeExpression rmExpr = (RequiredModifierTypeExpression)type;
          TypeNode modType = this.VisitTypeReference(rmExpr.ModifiedType);
          TypeNode modifier = this.VisitTypeReference(rmExpr.Modifier);
          if (modType == null) return null;
          if (modifier == null) return null;
          if (modType.Name == Looker.NotFound) return rmExpr;
          if (modifier.Name == Looker.NotFound) return rmExpr;
          return RequiredModifier.For(modifier, modType);
        }
        case NodeType.Class:
        case NodeType.Struct:
          return type;

        case NodeType.OptionalModifier:
        case NodeType.RequiredModifier:
          TypeModifier tmod = (TypeModifier)type;
          tmod.ModifiedType = this.VisitTypeReference(tmod.ModifiedType);
          tmod.Modifier = this.VisitTypeReference(tmod.Modifier);
          return tmod;

        default: return VisitUnknownTypeReference(type);
      }
    }
    public virtual TypeNode VisitUnknownTypeReference(TypeNode type){
      return type;
    }
    public virtual TypeNode AddNonNullWrapperIfNeeded(TypeNode typeNode) {
      return typeNode;
    }
    public virtual bool NonNullGenericIEnumerableIsAllowed {
      get{
        return false;
      }
    }

    public virtual TypeNode VisitArrayTypeExpression(ArrayTypeExpression atExpr){
      if (atExpr == null) return null;
      this.AbstractSealedUsedAsType = Error.AbstractSealedArrayElementType;
      TypeNode et = this.VisitTypeReference(atExpr.ElementType, true);
      this.AbstractSealedUsedAsType = Error.NotAType;
      if (et == null) return null;
      if (et == SystemTypes.DynamicallyTypedReference || et == SystemTypes.ArgIterator){
        this.HandleError(atExpr.ElementType, Error.ArrayElementCannotBeTypedReference, this.GetTypeName(et));
        return null;
      }
      if (atExpr.Rank == 1 && (atExpr.Sizes == null || atExpr.Sizes.Length == 0) && (atExpr.LowerBounds == null || atExpr.LowerBounds.Length == 0))
        return et.GetArrayType(atExpr.Rank);
      else
        return et.GetArrayType(atExpr.Rank, atExpr.Sizes, atExpr.LowerBounds);
    }
    public virtual Class VisitClassExpression(ClassExpression cexpr){
      if (cexpr == null) return null;
      int numTemplArgs = cexpr.TemplateArgumentExpressions == null ? 0 : cexpr.TemplateArgumentExpressions.Count;
      TypeNode bt = this.LookupType(cexpr.Expression, false, numTemplArgs);
      if (bt == null){
        this.HandleTypeExpected(cexpr.Expression);
        return null;
      }
      Class c = bt as Class;
      if (c != null){
        if (cexpr.TemplateArguments != null){
          cexpr.TemplateArguments = this.VisitTypeReferenceList(cexpr.TemplateArguments);
          TypeNode ti = c.GetTemplateInstance(this.currentType, cexpr.TemplateArguments);
          ti.TemplateExpression = cexpr;
          this.AddTemplateInstanceToList(ti);
          return (Class)ti;
        }else
          return c;
      }else{
        return null;
      }
    }
    public virtual TypeNode VisitFlexArrayTypeExpression(FlexArrayTypeExpression flex){
      if (flex == null) return null;
      TypeNode et = this.VisitTypeReference(flex.ElementType);
      if (et == null) return null;
      return SystemTypes.GenericIList.GetTemplateInstance(this.currentType, et);
    }
    public virtual TypeNodeList VisitTypeArgumentList(TypeNodeList typeArguments){
      return this.VisitTypeReferenceList(typeArguments);
    }
    public override TypeNodeList VisitTypeReferenceList(TypeNodeList typeReferences) {
      if (typeReferences == null) return null;
      for (int i = 0, n = typeReferences.Count; i < n; i++)
        typeReferences[i] = this.VisitTypeReference(typeReferences[i], true);
      return typeReferences;
    } 
    public virtual TypeNode VisitTypeExpression(TypeExpression texpr){
      if (texpr == null) return null;
      Literal lit = texpr.Expression as Literal;
      if (lit != null && lit.Value is TypeNode) return this.VisitTypeReference((TypeNode)lit.Value);
      texpr.TemplateArguments = this.VisitTypeArgumentList(texpr.TemplateArguments);
      TypeNode t = this.LookupType(texpr.Expression, false, texpr.TemplateArguments == null ? texpr.Arity : texpr.TemplateArguments.Count);
      if (t == null){
        //Search for a type with same name, but disregarding the generic parameter count. (Improves error message.)
        for (int i = 0; i < 10 && t == null; i++)
          t = this.LookupType(texpr.Expression, false, i);
        if (t == null){
          if (this.identifierInfos != null){
            this.AddNodePositionAndInfo(texpr.Expression, texpr, IdentifierContexts.TypeContext);
            return null;
          }
          this.HandleTypeExpected(texpr.Expression);
          return null;
        }
      }
      if (t is Class && ((Class)t).IsAbstractSealedContainerForStatics && this.AbstractSealedUsedAsType != Error.None)
        this.HandleError(texpr, this.AbstractSealedUsedAsType, this.GetTypeName(t));
      else if (t.ObsoleteAttribute != null)
        this.CheckForObsolesence(texpr, t);      
      if (texpr.TemplateArguments != null){
        if (t.TemplateParameters != null && t.TemplateParameters.Count == texpr.TemplateArguments.Count){
          for (int i = 0, n = texpr.TemplateArguments.Count; i < n; i++){
            TypeNode arg = texpr.TemplateArguments[i];
            if (arg == null) return null;
            ITypeParameter tpar = t.TemplateParameters[i] as ITypeParameter;
            if (tpar == null) return null;
            switch (tpar.TypeParameterFlags & TypeParameterFlags.SpecialConstraintMask){
              case TypeParameterFlags.DefaultConstructorConstraint:
                if (this.GetTypeView(arg).GetConstructors().Count > 0 && this.GetTypeView(arg).GetConstructor() == null){
                  this.HandleError(texpr.TemplateArgumentExpressions[i], Error.DefaultContructorConstraintNotSatisfied,
                    this.GetTypeName(arg), t.TemplateParameters[i].Name.ToString(), this.GetTypeName(t));
                  this.HandleRelatedError(t);
                  this.HandleRelatedError(arg);
                  return null;
                }
                break;
              case TypeParameterFlags.ReferenceTypeConstraint:
                if (arg.IsValueType){
                  this.HandleError(texpr.TemplateArgumentExpressions[i], Error.RefConstraintNotSatisfied,
                    this.GetTypeName(arg), t.TemplateParameters[i].Name.ToString(), this.GetTypeName(t));
                  this.HandleRelatedError(t);
                  this.HandleRelatedError(arg);
                  return null;
                }
                break;
              case TypeParameterFlags.ValueTypeConstraint:
                if (!arg.IsValueType){
                  this.HandleError(texpr.TemplateArgumentExpressions[i], Error.ValConstraintNotSatisfied,
                    this.GetTypeName(arg), t.TemplateParameters[i].Name.ToString(), this.GetTypeName(t));
                  this.HandleRelatedError(t);
                  this.HandleRelatedError(arg);
                  return null;
                }
                if (tpar.IsPointerFree && !arg.IsPointerFree)
                {
                  this.HandleError(texpr.TemplateArgumentExpressions[i], Error.PointerFreeConstraintNotSatisfied,
                    this.GetTypeName(arg), t.TemplateParameters[i].Name.ToString(), this.GetTypeName(t));
                  this.HandleRelatedError(t);
                  this.HandleRelatedError(arg);
                  return null;
                }
                if (tpar.IsUnmanaged && !arg.IsUnmanaged)
                {
                  this.HandleError(texpr.TemplateArgumentExpressions[i], Error.UnmanagedConstraintNotSatisfied,
                    this.GetTypeName(arg), t.TemplateParameters[i].Name.ToString(), this.GetTypeName(t));
                  this.HandleRelatedError(t);
                  this.HandleRelatedError(arg);
                }
                break;
            }
          }
          TypeNode ti = t.GetTemplateInstance(this.currentType, texpr.TemplateArguments);
          if (ti == t) return ti;
          this.AddNodePositionAndInfo(texpr.Expression, ti, IdentifierContexts.TypeContext);
          ti.TemplateExpression = texpr;
          this.AddTemplateInstanceToList(ti);
          return ti;
        }else{
          if (t.TemplateParameters == null || t.TemplateParameters.Count == 0)
            this.HandleError(texpr, Error.NotATemplateType, this.GetTypeName(t), "type");
          else
            this.HandleError(texpr, Error.TemplateTypeRequiresArgs, this.GetTypeName(t), t.TemplateParameters.Count.ToString(), "type");
          this.HandleRelatedError(t);
        }
      }else if (texpr.Arity == 0 && t.TemplateParameters != null && t.TemplateParameters.Count > 0){
        this.HandleError(texpr, Error.TemplateTypeRequiresArgs, this.GetTypeName(t), t.TemplateParameters.Count.ToString(), "type");
        this.HandleRelatedError(t);
      }else if (texpr.Arity == 0 && !(t is ITypeParameter) && t.Template == null && 
        t.DeclaringType != null && t.ConsolidatedTemplateParameters != null && t.ConsolidatedTemplateParameters.Count > 0){
        if (t.DeclaringType != null && this.typesToKeepUninstantiated[t.DeclaringType.UniqueKey] == null) {
          TypeNode declaringTypeInstance = this.GetDummyInstance(t.DeclaringType);
          if (declaringTypeInstance != null) return this.GetTypeView(declaringTypeInstance).GetNestedType(t.Name);
        }
      }else if (t.TemplateParameters != null && t.TemplateParameters.Count > 0 && texpr.TemplateArguments == null)
        this.typesToKeepUninstantiated[t.UniqueKey] = t;
      return t;
    }
    public virtual TypeNode GetDummyInstance(TypeNode t){
      if (t == null || t.ConsolidatedTemplateParameters == null || t.ConsolidatedTemplateParameters.Count == 0) return t;
      if (t.DeclaringType != null){
        TypeNode dt = this.GetDummyInstance(t.DeclaringType);
        if (dt != null){
          t = this.GetTypeView(dt).GetNestedType(t.Name);
          if (t == null || t.TemplateParameters == null || t.TemplateParameters.Count == 0) return t;
        }
      }
      t = t.GetTemplateInstance(this.currentType, t.TemplateParameters);
      this.AddTemplateInstanceToList(t);
      return t;
    }
    public virtual void AddTemplateInstanceToList(TypeNode t){
      if (t == null || t.IsNormalized || t.Template == null) return;
      if (t.DeclaringType != null && t.DeclaringType.Template != null) {
        this.AddTemplateInstanceToList(t.DeclaringType);
        return;
      }
      if (this.templateInstances == null) this.templateInstances = new TypeNodeList();
      if (this.templateInstanceTable == null) this.templateInstanceTable = new TrivialHashtable();
      if (this.templateInstanceTable[t.UniqueKey] != null) return;
      this.templateInstanceTable[t.UniqueKey] = t;
      this.templateInstances.Add(t);
      for (int i = 0, n = t.ReferencedTemplateInstances == null ? 0 : t.ReferencedTemplateInstances.Count; i < n; i++){
        TypeNode rti = t.ReferencedTemplateInstances[i];
        if (rti == null) continue;
        this.AddTemplateInstanceToList(rti);
      }
    }
    public virtual int GetHashKeyThatUnifiesIdentifiers(Node offendingNode) {
      Identifier id = offendingNode as Identifier;
      if (id != null) return id.UniqueIdKey;
      return offendingNode.UniqueKey;
    }
    public virtual void HandleTypeExpected(Node offendingNode){
      if (offendingNode == null) return;
      if (this.alreadyReported[this.GetHashKeyThatUnifiesIdentifiers(offendingNode)] != null) return;
      QualifiedIdentifier qual = offendingNode as QualifiedIdentifier;
      while (qual != null){
        if (qual.Qualifier == null) return;
        if (this.alreadyReported[this.GetHashKeyThatUnifiesIdentifiers(qual.Qualifier)] != null) return;
        TypeNode qt = this.LookupType(qual.Qualifier);
        if (qt != null){
          MemberList members = this.GetTypeView(qt).GetMembersNamed(qual.Identifier);
          if (members == null || members.Count == 0)
            this.HandleError(qual.Identifier, Error.NoSuchNestedType, qual.Identifier.ToString(), this.GetTypeName(qt));
          else
            this.HandleError(qual.Identifier, Error.WrongKindOfMember, this.GetMemberSignature(members[0]), this.GetMemberKind(members[0]), "type");
          return;
        }
        Identifier id = qual.Qualifier as Identifier;
        if (id != null){
          AliasDefinition aliasDef = this.LookupAlias(id);
          if (aliasDef != null)
            this.HandleError(qual.Identifier, Error.NoSuchQualifiedType, aliasDef.Alias.ToString(), qual.Identifier.ToString());
          else
            this.HandleError(id, Error.NoSuchType, id.ToString());
          this.alreadyReported[this.GetHashKeyThatUnifiesIdentifiers(id)] = qual;
          qual = (QualifiedIdentifier)offendingNode;
          qual.Type = SystemTypes.Type;
          if (aliasDef != null)
            this.AddNodePositionAndInfo(qual, aliasDef, IdentifierContexts.AllContext);
          else
            this.AddNodePositionAndInfo(qual, qual, IdentifierContexts.AllContext);
          return;
        }
        qual = qual.Qualifier as QualifiedIdentifier;
      }
      Identifier ident = offendingNode as Identifier;
      if (ident != null && ident.Prefix != null){
        AliasDefinition aliasDef = this.LookupAlias(ident.Prefix);
        if (aliasDef != null)
          this.AddNodePositionAndInfo(offendingNode, aliasDef, IdentifierContexts.AllContext);
      }
      if (this.alreadyReported[this.GetHashKeyThatUnifiesIdentifiers(offendingNode)] != null) return;
      this.alreadyReported[this.GetHashKeyThatUnifiesIdentifiers(offendingNode)] = offendingNode;
      if (this.ambiguousTypes != null && this.ambiguousTypes[this.GetHashKeyThatUnifiesIdentifiers(offendingNode)] != null)
        this.HandleError(offendingNode, Error.AmbiguousTypeReference, offendingNode.SourceContext.SourceText);
      else {
        if (offendingNode is Identifier)
          this.HandleError(offendingNode, Error.NoSuchType, offendingNode.ToString());
        else
          this.HandleError(offendingNode, Error.NoSuchType, offendingNode.SourceContext.SourceText);
      }
    }
    public virtual TypeNode GetStreamElementType(TypeNode t){
      if (t == null) return null;
      TypeNode template = t.Template;
      if (template == SystemTypes.GenericIEnumerable ||  template == SystemTypes.GenericNonEmptyIEnumerable ||
        template == SystemTypes.GenericBoxed || template == SystemTypes.GenericNonNull || template == SystemTypes.GenericInvariant){
        if (t.TemplateArguments != null && t.TemplateArguments.Count > 0)
          return t.TemplateArguments[0];
      }
      return t;
    }
    public virtual void CheckForObsolesence(Node errorLocation, Member mem){
      if (this.currentOptions != null && this.currentOptions.IsContractAssembly) return;
      //TODO: move this to Checker so that forward references work.
      //But before doing this, change Looker to preserve type expressions (for source context)
      //and change the type inferencer/checker to deal with type expressions
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
    public override TypeswitchCase VisitTypeswitchCase(TypeswitchCase typeswitchCase){
      if (typeswitchCase == null) return null;
      typeswitchCase.LabelType = this.VisitTypeReference(typeswitchCase.LabelType);
      Identifier targetId = typeswitchCase.LabelVariable as Identifier;
      if (targetId != null){
        Field f = new Field();
        f.DeclaringType = this.scope;
        f.Flags = FieldFlags.CompilerControlled|FieldFlags.InitOnly;
        f.Name = targetId;
        f.Type = typeswitchCase.LabelType;
        scope.Members.Add(f);
        typeswitchCase.LabelVariable = new MemberBinding(new ImplicitThis(this.scope, 0), f);
        typeswitchCase.LabelVariable.SourceContext = targetId.SourceContext;
      }
      typeswitchCase.Body = this.VisitBlock(typeswitchCase.Body);
      return typeswitchCase;
    }
    public override Expression VisitUnaryExpression(UnaryExpression unaryExpression){
      if (unaryExpression == null) return null;
      if (unaryExpression.NodeType == NodeType.DefaultValue){
        unaryExpression.Operand = this.VisitExpression(unaryExpression.Operand);
        Literal lit = unaryExpression.Operand as Literal;
        if (lit != null){
          TypeNode t = lit.Value as TypeNode;
          if (t != null){
            if (t.IsPrimitiveNumeric || t is EnumNode)
              return new BinaryExpression(new Literal(0, SystemTypes.Int32, lit.SourceContext), lit, NodeType.ExplicitCoercion, unaryExpression.SourceContext);
            if (!t.IsValueType && t is Class)
              return new BinaryExpression(new Literal(null, SystemTypes.Object, lit.SourceContext), lit, NodeType.ExplicitCoercion, unaryExpression.SourceContext);
            return unaryExpression;
          }
        }
      }
      return base.VisitUnaryExpression (unaryExpression);
    }

    public virtual Declarer GetDeclarer(){
      return new Declarer(this.ErrorHandler);
    }
    public static Identifier NotFound = Identifier.For("&*(Error@#@# Not found");
    public virtual Interface LookupInterface(InterfaceExpression iexpr){
      if (iexpr == null) return null;
      iexpr.TemplateArguments = this.VisitTypeReferenceList(iexpr.TemplateArguments);
      int numTemplArgs = iexpr.TemplateArgumentExpressions == null ? 0 : iexpr.TemplateArgumentExpressions.Count;
      TypeNode t = this.LookupType(iexpr.Expression, false, numTemplArgs);
      if (t == null){
        this.HandleTypeExpected(iexpr.Expression);
        return null;
      }
      Interface i = t as Interface;
      if (i != null){
        if (iexpr.TemplateArguments != null){           
          for (int j = 0, n = iexpr.TemplateArguments.Count; j < n; j++){
            TypeNode arg = iexpr.TemplateArguments[j];
            if (arg == null) return null;
          }
          t = i.GetTemplateInstance(this.currentType, iexpr.TemplateArguments);
          t.TemplateExpression = iexpr;
          this.AddTemplateInstanceToList(t);
          return (Interface)t;
        }else
          return i;
      }
      this.HandleError(iexpr, Error.NotAnInterface, this.GetTypeName(t));
      this.HandleRelatedError(t);
      return null;
    }
    public virtual TypeNode LookupType(Expression expression){
      return this.LookupType(expression, false, 0);
    }
    public virtual TypeNode LookupType(Expression expression, bool preferNestedNamespaceToOuterScopeType, int numTemplateArgs){
      Identifier identifier = expression as Identifier;
      if (identifier != null) return this.LookupType(identifier, numTemplateArgs);
      QualifiedIdentifier qual = expression as QualifiedIdentifier;
      if (qual != null) return this.LookupType(qual, preferNestedNamespaceToOuterScopeType, numTemplateArgs);
      NameBinding nb = expression as NameBinding;
      if (nb != null && nb.BoundMembers != null){
        //This happens when the expression was an identifier that serves as the qualifier of a QualifiedIdentifier
        //If one of the bound members is a type, the identifier should resolve to the type at this stage.
        for (int i = 0, n = nb.BoundMembers.Count; i < n; i++){
          TypeNode t = nb.BoundMembers[i] as TypeNode;
          if (t != null) return t;
        }
      }
      MemberBinding mb = expression as MemberBinding;
      if (mb != null && mb.BoundMember is TypeNode)
        return (TypeNode)mb.BoundMember;  //Can happen when CodeDom is translated to IR
      Literal literal = expression as Literal;
      if (literal != null && literal.Value is TypeCode){
        TypeNode t = null;
        switch ((TypeCode)literal.Value){
          case TypeCode.Boolean: t = SystemTypes.Boolean; break;
          case TypeCode.Byte: t = SystemTypes.UInt8; break;
          case TypeCode.Char: t = SystemTypes.Char; break;
          case TypeCode.DateTime: t = SystemTypes.DateTime; break;
          case TypeCode.DBNull: t = SystemTypes.DBNull; break;
          case TypeCode.Decimal: t = SystemTypes.Decimal; break;
          case TypeCode.Double: t = SystemTypes.Double; break;
          case TypeCode.Empty: t = SystemTypes.Void; break;
          case TypeCode.Int16: t = SystemTypes.Int16; break;
          case TypeCode.Int32: t = SystemTypes.Int32; break;
          case TypeCode.Int64: t = SystemTypes.Int64; break;
          case TypeCode.Object: t = SystemTypes.Object; break;
          case TypeCode.SByte: t = SystemTypes.Int8; break;
          case TypeCode.Single: t = SystemTypes.Single; break;
          case TypeCode.String: t = SystemTypes.String; break;
          case TypeCode.UInt16: t = SystemTypes.UInt16; break;
          case TypeCode.UInt32: t = SystemTypes.UInt32; break;
          case TypeCode.UInt64: t = SystemTypes.UInt64; break;
          default: Debug.Assert(false); break;
        }
        if (t != null) this.AddNodePositionAndInfo(literal, t, IdentifierContexts.AllContext);
        return t;
      }
      if (literal != null && (literal.Type == SystemTypes.Type || literal.Type == null)){ 
        TypeNode t = this.VisitTypeReference(literal.Value as TypeNode);
        if (t != null) this.AddNodePositionAndInfo(literal, t, IdentifierContexts.AllContext);
        return t;
      }
      return this.LookupType(expression as TemplateInstance);
    }
    public virtual TypeNode LookupType(TemplateInstance instance){
      if (instance == null) return null;
      if (instance.Expression == null) return null;
      int numArgs = instance.TypeArgumentExpressions == null ? 0 : instance.TypeArgumentExpressions.Count;
      TypeNode template = this.LookupType(instance.Expression, false, numArgs);
      if (template == null) return null;
      if (template.TemplateParameters == null || template.TemplateParameters.Count == 0){
        this.HandleError(instance.Expression, Error.NotATemplateType, this.GetTypeName(template), "type");
        this.HandleRelatedError(template);
        return null;
      }
      instance.TypeArguments = this.VisitTypeReferenceList(instance.TypeArguments);
      TypeNode result = template.GetTemplateInstance(this.currentModule, this.currentType, template.DeclaringType, instance.TypeArguments);
      this.AddTemplateInstanceToList(result);
      result.TemplateExpression = new TypeExpression(instance.Expression, instance.Expression.SourceContext);
      result.TemplateExpression.TemplateArgumentExpressions = instance.TypeArgumentExpressions;
      return result;
    }
    public virtual TypeNode LookupType(Identifier identifier){
      return this.LookupTypeOrNamespace(identifier, true, 0) as TypeNode;
    }
    public virtual TypeNode LookupType(Identifier identifier, int numTemplateArgs){
      return this.LookupTypeOrNamespace(identifier, true, numTemplateArgs) as TypeNode;
    }
    public virtual TypeNode LookupType(QualifiedIdentifier qual, bool preferNestedNamespaceToOuterScopeType, int numTemplateArgs){
      Identifier ns = null;
      Node q = null;
      if (qual.Qualifier is TemplateInstance)
        q = this.LookupType((TemplateInstance)qual.Qualifier);
      else
        q = this.LookupTypeOrNamespace(qual.Qualifier, preferNestedNamespaceToOuterScopeType);
      Identifier qualId = qual.Identifier;
      if (qualId == null) return null;
      if (numTemplateArgs != 0 && TargetPlatform.GenericTypeNamesMangleChar != 0)
        qualId = new Identifier(qualId.ToString()+TargetPlatform.GenericTypeNamesMangleChar+numTemplateArgs, qualId.SourceContext);
      TypeNode t = q as TypeNode;
      TypeNode dt = t;
      while (dt != null){
        Class c = dt as Class;
        if (c != null && c.DeclaringModule == this.currentModule) this.VisitBaseClassReference(c);
        if (!this.TypeIsVisible(dt) && this.alreadyReported[dt.UniqueKey] == null){
          this.alreadyReported[dt.UniqueKey] = dt;
          this.alreadyReported[this.GetHashKeyThatUnifiesIdentifiers(qual)] = qual;
          this.HandleError(qual, Error.TypeNotAccessible, this.GetTypeName(dt));
        }
        MemberList tmembers = this.GetTypeView(dt).GetMembersNamed(qualId);
        for (int i = 0, n = tmembers.Count; i < n; i++){
          t = tmembers[i] as TypeNode;
          if (t != null)
            if (this.TypeIsVisible(t))
              goto returnT;
            else{
              if (this.alreadyReported[t.UniqueKey] != null) return t;
              this.alreadyReported[t.UniqueKey] = t;
              this.alreadyReported[this.GetHashKeyThatUnifiesIdentifiers(qual)] = qual;
              this.HandleError(qual, Error.TypeNotAccessible, this.GetTypeName(t));
              goto returnT;
            }
        }
        if (dt == SystemTypes.Object) break;
        dt = dt.BaseType;
      }
      if (t != null){
        Identifier rootId = qual.Qualifier as Identifier;
        if (rootId != null && this.IsPrefixForTheGlobalNamespace(rootId.Prefix)){
          q = this.GetUnprefixedIdentifier(rootId);
        }else{
          if (this.identifierInfos != null){
            qual.Qualifier = new Literal(t, SystemTypes.Type);
            this.AddNodePositionAndInfo(qual, qual, IdentifierContexts.AllContext);
          }
          return null;
        }
      }
      TypeNodeList duplicates = null;
      ns = q as Identifier;
      if (ns != null){
        Scope scope = this.scope;
        NamespaceScope nsScope = null;
        while (scope != null){
          nsScope = scope as NamespaceScope;
          if (nsScope != null && nsScope.AssociatedNamespace != null && nsScope.AssociatedNamespace.FullNameId != null &&
            nsScope.AssociatedNamespace.FullNameId.UniqueIdKey == Identifier.Empty.UniqueIdKey)
            break;
          scope = scope.OuterScope;
        }
        if (nsScope == null) return null;
        t = nsScope.GetType(ns, qualId, out duplicates);
        if (this.NoDuplicatesAfterOverloadingOnTemplateParameters(duplicates, numTemplateArgs, ref t))
          duplicates = null;
      }else{
        AliasDefinition aliasDef = q as AliasDefinition;
        if (aliasDef != null){
          Debug.Assert(aliasDef.AliasedAssemblies != null);
          t = this.LookupType(qual.Identifier, aliasDef.AliasedAssemblies, out duplicates);
        }
      }
      if (duplicates != null && duplicates.Count > 1){
        if (t == null){ Debug.Assert(false); return null; }
        bool allDuplicatesHaveTheSameName = true;
        for (int i = 0; i < duplicates.Count && allDuplicatesHaveTheSameName; i++){
          TypeNode dup = duplicates[i];
          if (dup == null) continue;
          allDuplicatesHaveTheSameName = dup.FullName == t.FullName;
        }
        if (t.DeclaringModule != this.currentModule && allDuplicatesHaveTheSameName){
          this.HandleError(qual, Error.MultipleTypeImport, this.GetTypeName(t), t.DeclaringModule.Location);
          goto returnT;
        }
        this.HandleError(qual, Error.AmbiguousTypeReference, qual.ToString());
        for (int i = 0, n = duplicates.Count; i < n; i++){
          TypeNode dup = duplicates[i];
          if (dup == null) continue;
          this.HandleRelatedError(dup);
        }
      }
      returnT:
        if (t != null){
          this.AddNodePositionAndInfo(qual, t, IdentifierContexts.AllContext);
          return t;
        }
      return null;
    }
    public virtual Node LookupTypeOrNamespace(Expression expression, bool preferNestedNamespaceToOuterScopeType){
      Identifier identifier = expression as Identifier;
      if (identifier != null) return this.LookupTypeOrNamespace(identifier, false, 0);
      MemberBinding mb = expression as MemberBinding;
      if (mb != null && mb.BoundMember is TypeExpression)
        return this.VisitTypeReference((TypeExpression)mb.BoundMember);
      QualifiedIdentifier qualId = expression as QualifiedIdentifier;
      if (qualId == null) return null;
      Node node = this.LookupTypeOrNamespace(qualId.Qualifier, preferNestedNamespaceToOuterScopeType);
      TypeNode t = node as TypeNode;
      if (t != null) return this.GetTypeView(t).GetNestedType(qualId.Identifier);
      Identifier ns = node as Identifier;
      if (ns != null){
        node = this.LookupTypeOrNamespace(ns, qualId.Identifier, false, 0);
        if (node != null) return node;
        return Identifier.For(ns+"."+qualId.Identifier);
      }
      ns = this.ConvertToIdentifier(qualId);
      if (ns != null){
        node = this.LookupTypeOrNamespace(ns, false, 0);
        return node as Identifier;
      }
      return null;
    }
    public virtual Identifier ConvertToIdentifier(Expression qualOrId){
      Identifier id = qualOrId as Identifier;
      if (id != null) return id;
      QualifiedIdentifier qual = qualOrId as QualifiedIdentifier;
      if (qual == null) return null;
      StringBuilder idBuilder = new StringBuilder();
      Identifier prefix = null;
      if (this.BuildIdentifier(qual, idBuilder, ref prefix)){
        id = new Identifier(idBuilder.ToString(), qualOrId.SourceContext);
        id.Prefix = prefix;
        return id;
      }
      return null;
    }
    public virtual bool BuildIdentifier(QualifiedIdentifier qualId, StringBuilder idBuilder, ref Identifier prefix){
      prefix = null;
      if (qualId == null || qualId.Identifier == null || idBuilder == null){Debug.Assert(false); return false;}
      NameBinding nb = qualId.Qualifier as NameBinding;
      if (nb != null) 
        idBuilder.Append(nb.Identifier.ToString());
      else{
        Identifier id = qualId.Qualifier as Identifier;
        if (id != null){
          idBuilder.Append(id.Name);
          prefix = id.Prefix;
        }else{
          QualifiedIdentifier qId = qualId.Qualifier as QualifiedIdentifier;
          if (qId == null){
            return false;
          }else
            if (!this.BuildIdentifier(qId, idBuilder, ref prefix)) return false;
        }
      }
      idBuilder.Append('.');
      idBuilder.Append(qualId.Identifier.ToString());
      return true;
    }
    public virtual Node LookupTypeOrNamespace(Identifier identifier){
      return this.LookupTypeOrNamespace(identifier, false, 0);
    }
    public virtual Node LookupTypeOrNamespace(Identifier identifier, bool typeOnly, int numTemplateArgs){
      if (identifier == null){Debug.Assert(false); return null;}
      if (identifier.Prefix != null) return this.LookupTypeOrNamespace(identifier.Prefix, identifier, typeOnly, numTemplateArgs);
      TypeNode t = null;
      TypeNode tt = null;
      Scope scope = this.scope;
      while (scope != null){
        NamespaceScope nsScope = scope as NamespaceScope;
        if (nsScope != null){
          Node n = this.LookupTypeOrNamespace(null, identifier, typeOnly, nsScope, numTemplateArgs);
          if (n != null) return n;
        }else{
          TypeScope tScope = scope as TypeScope;
          if (tScope != null){
            t = this.LookupTypeOrTypeParameter(identifier, tScope, numTemplateArgs, out tt);
            if (t != null) goto returnT;
          }else{
            TypeNodeList tparams = scope.TemplateParameters;
            int key = identifier.UniqueIdKey;
            for (int i = 0, n = tparams == null ? 0 : tparams.Count; i < n; i++){
              t = tparams[i];
              if (t.Name != null && t.Name.UniqueIdKey == key) goto returnT;
            }
            t = null;
          }
        }
        scope = scope.OuterScope;
      }
      returnT:
        if (t != null){
          this.AddNodePositionAndInfo(identifier, t, IdentifierContexts.AllContext);
          return t;
        }
      if (tt != null){
        if (this.alreadyReported[tt.UniqueKey] != null) return tt;
        this.alreadyReported[tt.UniqueKey] = tt;
        this.HandleError(identifier, Error.TypeNotAccessible, this.GetTypeName(tt));
        return tt;
      }
      return null;
    }
    public virtual Node LookupTypeOrNamespace(Identifier prefix, Identifier identifier, bool typeOnly, int numTemplateArgs){
      if (prefix == null || identifier == null){Debug.Assert(false); return null;}
      Identifier globalPrefix = null;
      Scope scope = this.scope;
      NamespaceScope nsScope = null;
      if (this.IsPrefixForTheGlobalNamespace(prefix)) {
        globalPrefix = prefix;
        prefix = null;
        while (scope != null) {
          nsScope = scope as NamespaceScope;
          if (nsScope != null && nsScope.AssociatedNamespace != null && nsScope.AssociatedNamespace.Name != null &&
            nsScope.AssociatedNamespace.Name.UniqueIdKey == Identifier.Empty.UniqueIdKey) {
            break;
          }
          scope = scope.OuterScope;
        }
      } else {
        while (scope != null && (nsScope = scope as NamespaceScope) == null) 
          scope = scope.OuterScope;
      }
      if (nsScope == null){Debug.Assert(false); return null;}
      Node result = this.LookupTypeOrNamespace(prefix, identifier, typeOnly, nsScope, numTemplateArgs);
      if ((result == null || result == identifier) && globalPrefix != null && !typeOnly)
        result = new Identifier(identifier.Name);
      return result;
    }
    public virtual Node LookupTypeOrNamespace(Identifier identifier, bool typeOnly, NamespaceScope nsScope, out TypeNodeList duplicates){
      duplicates = null;
      if (identifier == null || nsScope == null){Debug.Assert(false); return null;}
      TypeNode t = nsScope.GetType(identifier, out duplicates, true);
      if (t != null && t.Name != null && t.Name.UniqueIdKey == identifier.UniqueIdKey){
        AliasDefinition aliasDef = nsScope.GetConflictingAlias(identifier);
        if (aliasDef != null) aliasDef.ConflictingType = t;
      }else if (!typeOnly){
        TrivialHashtable alreadySeenAliases = new TrivialHashtable();
        AliasDefinition aliasDef = nsScope.GetAliasFor(identifier);
        while (aliasDef != null){
          if (aliasDef.AliasedType != null) return (TypeNode)aliasDef.AliasedType;
          if (alreadySeenAliases[aliasDef.UniqueKey] != null) break; //TODO: error?
          alreadySeenAliases[aliasDef.UniqueKey] = aliasDef;
          if (aliasDef.AliasedAssemblies != null) return aliasDef;
          if (aliasDef.AliasedExpression is Identifier)
            aliasDef = nsScope.GetAliasFor(identifier = (Identifier)aliasDef.AliasedExpression);
          else if (aliasDef.AliasedExpression is QualifiedIdentifier)
            return this.LookupTypeOrNamespace(aliasDef.AliasedExpression, false);
          else
            aliasDef = null; 
        }
        Identifier ns = nsScope.GetNamespaceFullNameFor(identifier);
        if (ns != null) return ns;
      }
      return t;
    }
    public virtual Node LookupTypeOrNamespace(Identifier prefix, Identifier identifier, bool typeOnly, NamespaceScope nsScope, int numTemplateArgs){
      if (identifier == null || nsScope == null){Debug.Assert(false); return null;}
      if (numTemplateArgs != 0 && TargetPlatform.GenericTypeNamesMangleChar != 0)
        identifier = new Identifier(identifier.ToString()+TargetPlatform.GenericTypeNamesMangleChar+numTemplateArgs, identifier.SourceContext);
      TypeNodeList duplicates = null;
      TypeNode t = null;
      if (prefix == null){
        Node n = this.LookupTypeOrNamespace(identifier, typeOnly, nsScope, out duplicates);
        t = n as TypeNode;
        if (t == null) return n;
      }else{
        Node tOrn = this.LookupTypeOrNamespace(prefix, false, nsScope, out duplicates);
        AliasDefinition aliasDef = tOrn as AliasDefinition;
        if (aliasDef != null && aliasDef.AliasedAssemblies != null){
          //The prefix just restricts the assemblies in which to look for identifier
          t = this.LookupType(identifier, aliasDef.AliasedAssemblies, out duplicates);
          if (t != null) goto returnT;
          return identifier;
        }
        Identifier nestedNamespaceFullName = nsScope.GetNamespaceFullNameFor(prefix);
        if (nestedNamespaceFullName != null){
          if (this.IsPrefixForTheGlobalNamespace(nestedNamespaceFullName.Prefix)){
            while (nsScope.Name != Identifier.Empty && nsScope.OuterScope is NamespaceScope)
              nsScope = (NamespaceScope)nsScope.OuterScope;
          }else if (nestedNamespaceFullName.Prefix != null){
            Node n = this.LookupTypeOrNamespace(nestedNamespaceFullName.Prefix, nestedNamespaceFullName, false, 0);
            if (n is TypeNode){
              //TODO: error
              return null;
            }else{
              Identifier nestedNamespaceFullName2 = n as Identifier;
              if (nestedNamespaceFullName2 == null){
                //TODO: error
                return null;
              }else{
                nestedNamespaceFullName = nestedNamespaceFullName2;
              }
            }
          }
          t = nsScope.GetType(nestedNamespaceFullName, identifier, out duplicates);
          if (t == null && !typeOnly){
            nestedNamespaceFullName = nsScope.GetNamespaceFullNameFor(Identifier.For(nestedNamespaceFullName.Name+ "." + identifier.Name));
            if (nestedNamespaceFullName != null) return nestedNamespaceFullName;
          }
        }else{
          Identifier Uri = nsScope.GetUriFor(prefix);
          if (Uri != null){
            t = nsScope.GetType(identifier, out duplicates);
            int numDups = duplicates == null ? 0 : duplicates.Count;
            if (numDups > 1){
              t = null;
              for (int i = 0, n = numDups; i < n; i++){
                TypeNode dup = duplicates[i];
                if (dup == null){numDups--; continue;}
                Identifier dupUri = this.GetUriNamespaceFor(dup);
                if (dupUri != null && dupUri.UniqueIdKey == Uri.UniqueIdKey){
                  if (t != null) t = dup;
                }else{
                  numDups--;
                }
              }
            }
            if (numDups <= 1 && t != null){
              duplicates = null;
            }
          }else{
            t = this.LookupType(prefix, identifier, nsScope, out duplicates);
          }
        }
      }
      if (this.NoDuplicatesAfterOverloadingOnTemplateParameters(duplicates, numTemplateArgs, ref t))
        duplicates = null;
      if (duplicates != null && duplicates.Count > 1){
        if (t == null){Debug.Assert(false); return null;}
        bool allDuplicatesHaveTheSameName = true;
        for (int i = 0; i < duplicates.Count && allDuplicatesHaveTheSameName; i++){
          TypeNode dup = duplicates[i];
          if (dup == null) continue;
          allDuplicatesHaveTheSameName = dup.FullName == t.FullName;
        }
        if (t.DeclaringModule != this.currentModule && allDuplicatesHaveTheSameName){
          this.HandleError(identifier, Error.MultipleTypeImport, this.GetTypeName(t), t.DeclaringModule.Location);
          goto returnT;
        }
        this.HandleError(identifier, Error.AmbiguousTypeReference, identifier.ToString());
        for (int i = 0, n = duplicates.Count; i < n; i++){
          TypeNode dup = duplicates[i];
          if (dup == null) continue;
          this.HandleRelatedError(dup);
        }
        goto returnT;
      }else if (t != null){
        if (!this.TypeIsVisible(t)) goto done;
      }
      returnT:
        if (t != null){
          this.AddNodePositionAndInfo(identifier, t, IdentifierContexts.AllContext);
          return t;
        }
      done:
        if (t != null){
          if (this.alreadyReported[t.UniqueKey] != null) return t;
          this.alreadyReported[t.UniqueKey] = t;
          this.HandleError(identifier, Error.TypeNotAccessible, this.GetTypeName(t));
          return t;
        }
      return null;
    }
    public virtual bool NoDuplicatesAfterOverloadingOnTemplateParameters(TypeNodeList duplicates, int numTemplateArgs, ref TypeNode type){
      if (duplicates == null || duplicates.Count == 1) return true;
      if (type == null){Debug.Assert(false); return true;}
      TypeNode t = null;
      bool noDuplicateAfterAll = true;
      for (int i = 0; i < duplicates.Count; i++){
        TypeNode dup = duplicates[i];
        if (dup == null) continue;
        int numTpars = dup.TemplateParameters == null ? 0 : dup.TemplateParameters.Count;
        if (numTpars != numTemplateArgs) continue;
        if (t == null)
          t = type = dup;
        else
          noDuplicateAfterAll = false;
      }
      return noDuplicateAfterAll;
    }
    public virtual bool IsPrefixForTheGlobalNamespace(Identifier prefix){
      return prefix != null && prefix.UniqueIdKey == StandardIds.Global.UniqueIdKey;
    }
    public virtual TypeNode LookupTypeOrTypeParameter(Identifier identifier, TypeScope parentTypeScope, int numTemplateArgs, out TypeNode inaccessibleNestedType){
      inaccessibleNestedType = null;
      if (identifier == null || parentTypeScope == null) return null;
      TypeNode parentType = parentTypeScope.Type;
      if (parentType == null) return null;
      TypeNodeList tparams = parentType.TemplateParameters;
      if (parentType.Template != null) tparams = parentType.Template.TemplateParameters;
      for (int i = 0, n = tparams == null ? 0 : tparams.Count; i < n; i++){
        TypeNode t = tparams[i];
        if (t == null) continue;
        if (t.Name.UniqueIdKey == identifier.UniqueIdKey){
          if (tparams == parentType.TemplateParameters) return t;
          if (parentType.TemplateArguments == null || parentType.TemplateArguments.Count <= i) return t;
          return parentType.TemplateArguments[i];
        }
      }
      if (numTemplateArgs > 0 && TargetPlatform.GenericTypeNamesMangleChar != 0)
        identifier = new Identifier(identifier.ToString() + TargetPlatform.GenericTypeNamesMangleChar + numTemplateArgs.ToString());
      while (parentType != null){
        MemberList members = this.GetTypeView(parentType).GetMembersNamed(identifier);
        for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
          Member mem = members[i];
          if (mem is TypeParameter || mem is ClassParameter) continue;
          TypeNode dummy = this.currentType;
          if (Checker.NotAccessible(mem, ref dummy, this.currentModule, this.currentType, this.TypeViewer)){
            if (mem is TypeNode) inaccessibleNestedType = (TypeNode)mem;
            continue;
          }
          TypeNode t = mem as TypeNode;
          if (t != null){
            if (this.TypeIsVisible(t)) return t;
            inaccessibleNestedType = t;
          }
        }
        Class c = parentType as Class;
        if (c != null && c.BaseClass == null && c.DeclaringModule == this.currentModule)
          this.VisitBaseClassReference(c);
        if (parentType == SystemTypes.Object) break;
        parentType = parentType.BaseType;
      }
      return null;
    }
    protected TrivialHashtable anonTableForModule;
    public virtual void LookupAnonymousTypes(Identifier ns, TypeNodeList atypes){
      Scope scope = this.scope;
      while (scope.OuterScope != null) scope = scope.OuterScope;
      Module mod = scope.DeclaringModule;
      if (mod == null){Debug.Assert(false); return;}
      if (this.anonTableForModule == null) this.anonTableForModule = new TrivialHashtable();
      TrivialHashtable anonTable = (TrivialHashtable)this.anonTableForModule[mod.UniqueKey];
      if (anonTable == null) this.anonTableForModule[mod.UniqueKey] = anonTable = new TrivialHashtable();
      //TODO: cache this on the module. Recomputing this for every imported module is too expensive in incremental compilation scenarios
      int nsIdKey = ns.UniqueIdKey;
      TypeNodeList anonTypes = (TypeNodeList)anonTable[nsIdKey];
      if (anonTypes == null) 
        anonTable[nsIdKey] = anonTypes = new TypeNodeList();
      else if (anonTypes.Count == 0) 
        return;
      if (mod.ContainingAssembly == null) return;
      TypeNodeList types2 = mod.ContainingAssembly.Types;
      for (int j = 0, m = types2.Count; j < m; j++){
        TypeNode type = types2[j];
        if (type == null || type.Namespace == null) continue;
        if (type.Namespace.UniqueIdKey != nsIdKey) continue;
        // when looking for SystemTypes.AnonymousAttribute in the same module that is being compiled,
        // the custom attribute will *not* have been resolved yet.
        // name
        AttributeList list = type.Attributes;
        for (int i = 0, n = list == null ? 0 : list.Count; i < n; i++ ){
          AttributeNode an = list[i];
          if (an == null) continue;
          Literal lit = an.Constructor as Literal;
          if (lit == null) continue;
          Class c = lit.Value as Class;
          if (c == null) continue;
          if (c == SystemTypes.AnonymousAttribute)
            anonTypes.Add(type);
        }
      }
      for (int i = 0, n = mod.AssemblyReferences == null ? 0 : mod.AssemblyReferences.Count; i < n; i++) {
        TypeNodeList types = mod.AssemblyReferences[i].Assembly.Types;
        if (types == null) continue;
        for (int j = 0, m = types.Count; j < m; j++){
          TypeNode type = types[j];
          if (type == null || type.Namespace == null) continue;
          if (type.Namespace.UniqueIdKey != nsIdKey) continue;
          if (MetadataHelper.GetCustomAttribute(type, SystemTypes.AnonymousAttribute) == null) continue;
          anonTypes.Add(type);
        }
      }
      for (int i = 0, n = mod.ModuleReferences == null ? 0 : mod.ModuleReferences.Count; i < n; i++) {
        TypeNodeList types = mod.ModuleReferences[i].Module.Types;
        if (types == null) continue;
        for (int j = 0, m = types.Count; j < m; j++){
          TypeNode type = types[j];
          if (type == null || type.Namespace == null) continue;
          if (type.Namespace.UniqueIdKey != nsIdKey) continue;
          if (MetadataHelper.GetCustomAttribute(type, SystemTypes.AnonymousAttribute) == null) continue;
          anonTypes.Add(type);
        }
      }
      for (int i = 0, n = anonTypes.Count; i < n; i++)
        atypes.Add(anonTypes[i]);
    }
    public virtual Identifier GetUriNamespaceFor(TypeNode type){
      return null;
    }
    public virtual AliasDefinition LookupAlias(Identifier id){
      //run up the scope chain, looking at namespace scopes and looking for a nested namespace with name id
      if (id == null) return null;
      Scope scope = this.scope;
      while (scope != null){
        NamespaceScope nsScope = scope as NamespaceScope;
        if (nsScope != null) return nsScope.GetAliasFor(id);
        scope = scope.OuterScope;
      }
      return null;
    }
    public virtual bool TypeIsVisible(TypeNode type){
      if (type == null) return false;
      if (type.IsGeneric && type.TemplateArguments != null && type.TemplateArguments.Count > 0) {
        if (!this.TypeIsVisible(type.Template)) return false;
        for (int i = 0, n = type.TemplateArguments.Count; i < n; i++) {
          TypeNode t = type.TemplateArguments[i];
          if (t != null && !this.TypeIsVisible(t)) return false;
        }
        return true;
      }
      if (type.DeclaringType != null){
        if (this.scope is AttributeScope) return true;
        TypeFlags visibility = type.Flags & TypeFlags.VisibilityMask;
        type = type.DeclaringType;
        TypeNode t = this.currentType;
        while (t != null){
          if (t == type || t == type.Template) return true;
          t = t.DeclaringType;
        }
        switch (visibility) {
          case TypeFlags.NestedAssembly:
            return this.InternalsAreVisible(this.currentModule, type.DeclaringModule);
          case TypeFlags.NestedFamANDAssem:
            if (!this.FamilyMembersAreVisible(this.currentType, type)) return false;
            return this.InternalsAreVisible(this.currentModule, type.DeclaringModule);
          case TypeFlags.NestedFamily:
            return this.FamilyMembersAreVisible(this.currentType, type);
          case TypeFlags.NestedFamORAssem:
            if (this.FamilyMembersAreVisible(this.currentType, type)) return true;
            return this.InternalsAreVisible(this.currentModule, type.DeclaringModule);
          case TypeFlags.NestedPrivate:
            TypeNode ctype = this.currentType;
            while (ctype != null) {
              if (ctype == type || ctype.Template == type) return true;
              ctype = ctype.DeclaringType;
            }
            return false;
          case TypeFlags.NestedPublic: 
            return true;
        }
      }
      if (type.IsPublic) return true;
      return this.InternalsAreVisible(this.currentModule, type.DeclaringModule);
    }
    public virtual bool FamilyMembersAreVisible(TypeNode referringType, TypeNode declaringType) {
      if (this.GetTypeView(referringType).IsInheritedFrom(declaringType)) return true;
      if (referringType.DeclaringType == null) return false;
      return this.FamilyMembersAreVisible(referringType.DeclaringType, declaringType);
    }
    public virtual bool InternalsAreVisible(Module referringModule, Module declaringModule){
      return Checker.InternalsAreVisible(referringModule, declaringModule);
    }
    // query nodes
    public override Node VisitQueryAxis(QueryAxis axis){
      base.VisitQueryAxis(axis);
      axis.TypeTest = this.VisitTypeReference(axis.TypeTest);
      // resolve namespace from prefix
      if (axis.Namespace == null && axis.Name != null && axis.Name.Prefix != null){
        Identifier prefix = axis.Name.Prefix;
        AliasDefinition aliasDef = this.LookupAlias(prefix);
        if (aliasDef != null){
          axis.Namespace = aliasDef.AliasedUri;
          if (axis.Namespace == null)
            axis.Namespace = (Identifier)aliasDef.AliasedExpression;
        }
      }
      return axis;
    }
    public override Node VisitQueryIterator(QueryIterator xiterator){
      base.VisitQueryIterator(xiterator);
      if (xiterator.ElementType != null){
        xiterator.ElementType = this.VisitTypeReference(xiterator.ElementType);
      }
      // add to query scope
      if (this.scope is QueryScope){
        Field f = new Field();
        f.DeclaringType = this.scope;
        f.Flags = FieldFlags.CompilerControlled|FieldFlags.InitOnly;
        f.Name = xiterator.Name;
        f.Type = null;
        this.scope.Members.Add(f);
      }
      return xiterator; 
    }
    public override Node VisitQueryJoin( QueryJoin join ){
      Scope scope = this.scope;
      Scope leftScope = this.scope = new QueryScope(scope);
      this.AddToAllScopes(this.scope);
      join.LeftOperand = this.VisitExpression(join.LeftOperand);
      Scope rightScope = this.scope = new QueryScope(scope);
      this.AddToAllScopes(this.scope);
      join.RightOperand = this.VisitExpression(join.RightOperand);
      this.MoveMembers(scope, leftScope.Members);
      this.MoveMembers(scope, rightScope.Members);
      this.scope = scope;
      join.JoinExpression = this.VisitExpression(join.JoinExpression);
      return join;
    }
    private void VisitScopedExpressionList( ExpressionList list ){
      Scope scope = this.scope;
      for( int i = 0, n = list.Count; i < n; i++ ){
        Expression e = list[i];
        Class tempScope = this.scope = new BlockScope(scope, null);
        this.AddToAllScopes(this.scope);
        this.Visit(e);
        this.MoveMembers(scope, tempScope.Members);
      }
      this.scope = scope;
    }
    private void MoveMembers( Class scope, MemberList members ){
      for( int i = 0, n = members.Count; i < n; i++ ){
        Member m = members[i];
        m.DeclaringType = scope;
        scope.Members.Add( m );
      }
    }
    public override Node VisitQueryAggregate( QueryAggregate qa ){
      base.VisitQueryAggregate(qa);
      if (qa.Group != null && this.currentGroup != null){
        this.currentGroup.AggregateList.Add(qa);
        qa.Group = this.currentGroup;
      }
      return qa;
    }
    public override Node VisitQueryDistinct( QueryDistinct qd ){
      base.VisitQueryDistinct(qd);
      if (qd.Group == null && this.currentGroup != null){
        qd.Group = this.currentGroup;
      }
      return qd;
    }
    public override Node VisitQueryGroupBy( QueryGroupBy qgb ){
      if (qgb == null) return null;
      qgb.Source = this.VisitExpression(qgb.Source);
      this.VisitExpressionList(qgb.GroupList);
      if (qgb.Having != null){
        QueryGroupBy save = this.currentGroup;
        this.currentGroup = qgb;
        qgb.Having = this.VisitExpression(qgb.Having);
        this.currentGroup = save;
      }
      return qgb;
    }
    public virtual QueryGroupBy GetGroupBySource(Expression source){
      if (source == null) return null;
      switch (source.NodeType){
        case NodeType.QueryGroupBy: return (QueryGroupBy)source;
        case NodeType.QueryOrderBy: return this.GetGroupBySource(((QueryOrderBy)source).Source);
        case NodeType.QueryProject: return this.GetGroupBySource(((QueryProject)source).Source);
        case NodeType.QueryDistinct: return this.GetGroupBySource(((QueryDistinct)source).Source);
        case NodeType.QueryLimit: return this.GetGroupBySource(((QueryLimit)source).Source);
        case NodeType.QuerySingleton: return this.GetGroupBySource(((QuerySingleton)source).Source);
        default: return null;
      }
    }
    public virtual Expression InjectGroupBy(Expression source, QueryGroupBy gb){
      if (source == null || gb == null) return null;
      switch (source.NodeType){
        case NodeType.QueryOrderBy:
          QueryOrderBy ob = (QueryOrderBy)source;
          ob.Source = this.InjectGroupBy(ob.Source, gb);
          return ob;
        case NodeType.QueryProject: 
          QueryProject qp = (QueryProject)source;
          qp.Source = this.InjectGroupBy(qp.Source, gb);
          return qp;
        case NodeType.QueryDistinct:
          QueryDistinct qd = (QueryDistinct)source;
          qd.Source = this.InjectGroupBy(qd.Source, gb);
          return qd;
        case NodeType.QuerySingleton:
          QuerySingleton qs = (QuerySingleton)source;
          qs.Source = this.InjectGroupBy(qs.Source, gb);
          return qs;
        default:
          gb.Source = source;
          return gb;
      }
    }
    public override Node VisitQueryOrderBy( QueryOrderBy qob ){
      if (qob == null) return null;
      qob.Source = this.VisitExpression(qob.Source);
      QueryGroupBy save = this.currentGroup;
      this.currentGroup = this.GetGroupBySource(qob.Source);
      if (this.currentGroup == null){
        this.currentGroup = new QueryGroupBy();
      }
      this.VisitExpressionList(qob.OrderList);
      if (this.currentGroup.Source == null && this.currentGroup.AggregateList.Count > 0){
        qob.Source = this.InjectGroupBy(qob.Source, this.currentGroup);
      }
      this.currentGroup = save;
      return qob;
    }
    public override Node VisitQueryProject( QueryProject qp ){
      if (qp == null) return null;
      qp.Source = this.VisitExpression(qp.Source);
      QueryGroupBy save = this.currentGroup;
      this.currentGroup = this.GetGroupBySource(qp.Source);
      if (this.currentGroup == null){
        this.currentGroup = new QueryGroupBy();
      }
      this.VisitExpressionList(qp.ProjectionList);
      if (this.currentGroup.Source == null && this.currentGroup.AggregateList.Count > 0){
        qp.Source = this.InjectGroupBy(qp.Source, this.currentGroup);
      }
      this.currentGroup = save;
      if (qp.ProjectedType != null){
        qp.ProjectedType = this.VisitTypeReference(qp.ProjectedType);
      }
      return qp;
    }
    public override Node VisitQueryDelete( QueryDelete delete ){
      Scope savedScope = this.scope;
      Scope scope = this.scope = new QueryScope(savedScope);
      this.AddToAllScopes(this.scope);
      delete.Source = (Expression)this.Visit(delete.Source);
      delete.Target = this.VisitExpression( delete.Target );
      this.scope = savedScope;
      return delete;
    }
    public override Node VisitQuerySelect( QuerySelect select ){
      Scope savedScope = this.scope;
      Scope scope = this.scope = new QueryScope(savedScope);
      this.AddToAllScopes(this.scope);
      QueryGroupBy saveGroup = this.currentGroup;
      this.currentGroup = null;
      select.Source = (Expression) this.Visit( select.Source );
      this.currentGroup = saveGroup;
      this.scope = savedScope;
      return select;
    }
    public override Expression VisitBinaryExpression(BinaryExpression be){
      Expression result = base.VisitBinaryExpression(be);
      if ( (be.Operand1 != null && (be.Operand1.NodeType == NodeType.QueryAny || be.Operand1.NodeType == NodeType.QueryAll))
        || (be.Operand2 != null && (be.Operand2.NodeType == NodeType.QueryAny || be.Operand2.NodeType == NodeType.QueryAll))){
        QueryQuantifiedExpression qq = new QueryQuantifiedExpression();
        qq.Left = be.Operand1 as QueryQuantifier;
        qq.Right = be.Operand2 as QueryQuantifier;
        qq.Expression = result;
        result = qq;
      }
      return result;
    }
    void AddToAllScopes(Scope scope){
      if (allScopes != null)
        allScopes.Add(scope);
    }
  }
}
