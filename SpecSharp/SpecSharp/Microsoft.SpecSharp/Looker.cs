//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
//
#if CCINamespace
using Microsoft.Cci;
using Cci = Microsoft.Cci;
#else
using System.Compiler;
using Cci = System.Compiler;
#endif
using System;
using System.Diagnostics;

namespace Microsoft.SpecSharp{
  /// <summary>
  /// Walks an IR, mutating it by replacing identifier nodes with the members/locals they resolve to
  /// </summary>
  public sealed class Looker : Cci.Looker{
    private Identifier currentNamespaceURI;
    private Identifier defaultNamespaceURI;
    private Block currentBlock;
    private bool insideAssertion;
    private TrivialHashtable typeParamToClassParamMap = new TrivialHashtable();
    // begin change by drunje
    public bool AllowPointersToManagedStructures;
    // end change by drunje

    public bool DontInjectDefaultConstructors;

    public Looker(Scope scope, Cci.ErrorHandler errorHandler, TrivialHashtable scopeFor)
      : this(scope, errorHandler, scopeFor, null, null){
      this.alreadyReported[StandardIds.Var.UniqueIdKey] = true;
    }
    public Looker(Scope scope, Cci.ErrorHandler errorHandler, TrivialHashtable scopeFor, TrivialHashtable ambiguousTypes, TrivialHashtable referencedLabels)
      : base(scope, errorHandler, scopeFor, new TypeSystem(new ErrorHandler(errorHandler.Errors)), ambiguousTypes, referencedLabels){
      this.alreadyReported[StandardIds.Var.UniqueIdKey] = true;
    }
    public Looker(Visitor callingVisitor)
      : base(callingVisitor){
      this.alreadyReported[StandardIds.Var.UniqueIdKey] = true;
    }
    public override void TransferStateTo(Visitor targetVisitor){
      base.TransferStateTo(targetVisitor);
      Looker target = targetVisitor as Looker;
      if (target == null) return;
      target.currentNamespaceURI = this.currentNamespaceURI;
      target.defaultNamespaceURI = this.defaultNamespaceURI;
      target.currentBlock = this.currentBlock;
      target.typeParamToClassParamMap = this.typeParamToClassParamMap;
      target.DontInjectDefaultConstructors = this.DontInjectDefaultConstructors;
    }

    public override Node VisitUnknownNodeType(Node node){
      if (node == null) return null;
      switch (((SpecSharpNodeType)node.NodeType)){
        case SpecSharpNodeType.KeywordList:
          this.AddNodePositionAndInfo(node, node, IdentifierContexts.AllContext);
          return null;
        default:
          return base.VisitUnknownNodeType(node);
      }
    }
    
    public override Identifier AppendAttributeIfAllowed(Identifier id){
      Debug.Assert(id != null);
      string idText = id.SourceContext.SourceText;
      if (idText == null || idText.Length == 0 || idText[0] == '@') return null;
      Identifier result = new Identifier(id.Name+"Attribute", id.SourceContext);
      result.Prefix = id.Prefix;
      return result;
    }
    public override void ConstructMethodForNestedFunction(Node func, Method method, TypeNode returnType, ParameterList parList, Block body){
      base.ConstructMethodForNestedFunction(func, method, returnType, parList, body);
      if (method != null && body != null)
        method.SourceContext.EndPos = body.SourceContext.EndPos+1;
    }
    private static FieldFlags MapTypeVisibilityToFieldVisibility(TypeNode type){
      TypeFlags flags = type.Flags & TypeFlags.VisibilityMask;
      switch (flags){
        case TypeFlags.Public:
        case TypeFlags.NestedPublic: return FieldFlags.Public;
        case TypeFlags.NotPublic:
        case TypeFlags.NestedAssembly: return FieldFlags.Assembly;
        case TypeFlags.NestedFamANDAssem: return FieldFlags.FamANDAssem;
        case TypeFlags.NestedFamily: return FieldFlags.Family;
        case TypeFlags.NestedFamORAssem: return FieldFlags.FieldAccessMask;
        default: return FieldFlags.Private;
      }
    }
    public override MemberList LookupAnonymousMembers(Identifier identifier, NamespaceScope nsScope){
      // 21 June 2005 -- For now, remove functionality of finding anonyous members: too slow,
      // and not needed for now since we are not allowing general quantifiers in contracts
      // But keep commented code in case we want to go back to it.
      return null;
      //SpecSharpCompilerOptions coptions = this.currentOptions as SpecSharpCompilerOptions;
      //if (coptions != null && coptions.Compatibility) return null;
      //return base.LookupAnonymousMembers(identifier, nsScope);
    }
    public override void LookupAnonymousTypes(Identifier ns, TypeNodeList atypes){
      // 21 June 2005 -- For now, remove functionality of finding anonyous members: too slow,
      // and not needed for now since we are not allowing general quantifiers in contracts
      // But keep commented code in case we want to go back to it.
      return;
      //SpecSharpCompilerOptions coptions = this.currentOptions as SpecSharpCompilerOptions;
      //if (coptions != null && coptions.Compatibility) return;
      //base.LookupAnonymousTypes(ns, atypes);
    }

    public override TypeNode LookupType(Expression expression, bool preferNestedNamespaceToOuterScopeType, int numTemplateArgs) {
      TypeNode result = base.LookupType(expression, preferNestedNamespaceToOuterScopeType, numTemplateArgs);
      if (this.typeParamToClassParamMap != null && result != null) {
        TypeNode betterResult = this.typeParamToClassParamMap[result.UniqueKey] as TypeNode;
        if (betterResult != null) return betterResult;
      }
      return result;
    }

    public override Block VisitBlock(Block block){
      if (block == null) return null;
      Block savedCurrentBlock = this.currentBlock;
      this.currentBlock = block;
      block = base.VisitBlock(block);
      if (block != null && block.Scope != null){
        MemberList blockLocals = block.Scope.Members;      
        for (int i = 0, n = blockLocals == null ? 0 : blockLocals.Count; i < n; i++){
          Field f = blockLocals[i] as Field;
          if (f == null) continue;
        }
      }
      this.currentBlock = savedCurrentBlock;
      return block;
    }
    public override ParameterList VisitDelegateParameters(ParameterList parameterList) {
      if (parameterList == null) return null;
      if (parameterList.Count > 0) {
        Parameter p = parameterList[parameterList.Count-1];
        if (p != null && p.Name != null) {
          if (p.Name == StandardIds.__Arglist) {
            this.HandleError(p, Error.NoArglistInDelegates);
            parameterList.Count = parameterList.Count-1;
          }
        }
      }
      return base.VisitDelegateParameters (parameterList);
    }

    public override Expression VisitMethodCall(MethodCall mc){
      Expression result = base.VisitMethodCall(mc);
      mc = result as MethodCall;
      // check for Aggregate types
      if (mc != null && mc.Operands != null && mc.Operands.Count == 1) {
        Literal lit = mc.Callee as Literal;
        if (lit != null && lit.Type == SystemTypes.Type) {
          TypeNode type = (TypeNode) lit.Value;
          if (type != null && type.IsAssignableTo(SystemTypes.IAggregateGroup)) {
            QueryAggregate qa = new QueryAggregate();
            qa.Name = type.Name;
            qa.AggregateType = type;
            qa.Expression = mc.Operands[0];
            qa.SourceContext = mc.SourceContext;
            if (this.currentGroup != null) {
              this.currentGroup.AggregateList.Add(qa);
              qa.Group = this.currentGroup;
            }
            return qa;      
          }
        }
      }
      return result;
    }
    static AttributeList NoDefaultExpose() {
      AttributeList list = new AttributeList(2);
      if (SystemTypes.NoDefaultContractAttribute != null) {
        list.Add(new AttributeNode(new Literal(SystemTypes.NoDefaultContractAttribute, SystemTypes.Type), null, AttributeTargets.Method));
      }
      return list;
    }

    private void PrepareGuardedClass(TypeNode typeNode) {
      SpecSharpCompilerOptions options = this.currentOptions as SpecSharpCompilerOptions;
      if (!(options != null && (options.DisableGuardedClassesChecks || options.Compatibility))) {
        if (typeNode is Class && typeNode.Contract != null && (typeNode.Contract.InvariantCount > 0 || typeNode.Contract.ModelfieldContractCount > 0) ||
          typeNode is Class && this.currentPreprocessorDefinedSymbols != null && this.currentPreprocessorDefinedSymbols.ContainsKey("GuardAllClasses")) {
          if (typeNode.Interfaces == null) {
            typeNode.Interfaces = new InterfaceList();
          }
          if (typeNode.Template == null) { //we have to be careful when we are passed a typeNode of a specialized generic type as it shares the contract of the 'real' generic typeNode
            #region Add the field "frame" to the class.
            Field frameField = new Field(typeNode, null, FieldFlags.Public, Identifier.For("SpecSharp::frameGuard"), SystemTypes.Guard, null);
            frameField.CciKind = CciMemberKind.Auxiliary;
            typeNode.Contract.FrameField = frameField;
            typeNode.Members.Add(frameField);
            This thisParameter = new This(typeNode);
            Method frameGetter = new Method(typeNode, NoDefaultExpose(), Identifier.For("get_SpecSharp::FrameGuard"), null, OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Guard),
              new Block(new StatementList(new Return(new BinaryExpression(new MemberBinding(thisParameter, frameField), new Literal(SystemTypes.NonNullType, SystemTypes.Type), System.Compiler.NodeType.ExplicitCoercion, OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Guard))))));
            // Pretend this method is [Delayed] so that we can call it from a delayed constructor.
            frameGetter.Attributes.Add(new AttributeNode(new Literal(ExtendedRuntimeTypes.DelayedAttribute, SystemTypes.Type), null, AttributeTargets.Method));
            frameGetter.CciKind = CciMemberKind.FrameGuardGetter;
            frameGetter.Attributes.Add(new AttributeNode(new Literal(SystemTypes.PureAttribute, SystemTypes.Type), null, AttributeTargets.Method));
            frameGetter.Flags = MethodFlags.Public | MethodFlags.HideBySig | MethodFlags.SpecialName;
            frameGetter.CallingConvention = CallingConventionFlags.HasThis;
            frameGetter.ThisParameter = thisParameter;
            typeNode.Contract.FramePropertyGetter = frameGetter;
            typeNode.Members.Add(frameGetter);
            Property frameProperty = new Property(typeNode, null, PropertyFlags.None, Identifier.For("SpecSharp::FrameGuard"), frameGetter, null);
            typeNode.Members.Add(frameProperty);
            typeNode.Contract.FrameProperty = frameProperty;
            #endregion
            typeNode.Contract.InitFrameSetsMethod = new Method(typeNode, NoDefaultExpose(), Identifier.For("SpecSharp::InitGuardSets"), null, SystemTypes.Void, null);
            typeNode.Contract.InitFrameSetsMethod.CciKind = CciMemberKind.Auxiliary;
            typeNode.Contract.InitFrameSetsMethod.Flags = MethodFlags.Public | MethodFlags.HideBySig | MethodFlags.SpecialName;
          }
        }
      }
    }

    public override Class VisitClass(Class Class) {
      PrepareGuardedClass(Class);
      return base.VisitClass(Class);
    }
    public override TypeNode VisitTypeNode(TypeNode typeNode) {
      if (typeNode == null) return null;
      typeNode = base.VisitTypeNode(typeNode);
      if (typeNode == null) return null;
      this.InjectStaticInitializerIfNoneSpecified(typeNode);
      TypeNode completeType = typeNode.PartiallyDefines;
      if (completeType == null) return typeNode;
      completeType.Flags &= ~TypeFlags.RTSpecialName;
      if (typeNode.BaseType != completeType.BaseType){
        if (completeType.BaseType == null)
          ((Class)completeType).BaseClass = ((Class)typeNode).BaseClass;
        //TODO: else give an error
      }
      //TODO: check interfaces etc.
      return typeNode;
    }

    public override TypeNodeList VisitTypeParameterList(TypeNodeList typeParameters) {
      typeParameters = base.VisitTypeParameterList(typeParameters);
      for (int i = 0, n = typeParameters == null ? 0 : typeParameters.Count; i < n; i++) {
        TypeNode tp = typeParameters[i];
        if (tp == null) continue;
        ClassParameter cp = tp as ClassParameter;
        if (cp != null) {
          if (cp.BaseClass == null) cp.BaseClass = this.VisitTypeReference(cp.BaseClassExpression) as Class;
        }
        typeParameters[i] = base.VisitTypeParameter(tp);
      }
      return typeParameters;
    }

    public override TypeNode VisitTypeParameter(TypeNode typeParameter){
      if (typeParameter == null) return null;
      InterfaceList interfaces = typeParameter.Interfaces;
      if (interfaces != null && interfaces.Count > 0){
        Class baseClass = null;
        ClassExpression cExpr = null;
        Interface iface = interfaces[0];
        InterfaceExpression ifExpr = iface as InterfaceExpression;
        if (ifExpr != null && ifExpr.Expression != null){
          cExpr = new ClassExpression(ifExpr.Expression, ifExpr.TemplateArguments, ifExpr.Expression.SourceContext);
          baseClass = this.VisitClassExpression(cExpr);
          if (baseClass != null && baseClass.Name == Looker.NotFound){
            baseClass = null; cExpr = null;
          }else if (baseClass == null) {
            TypeParameter tp = this.LookupType(ifExpr.Expression, false, 0) as TypeParameter;
            if (tp != null && (tp.TypeParameterFlags & TypeParameterFlags.ReferenceTypeConstraint) != 0)
              baseClass = cExpr;
            else if ((((ITypeParameter)typeParameter).TypeParameterFlags & TypeParameterFlags.ReferenceTypeConstraint) != 0) {
              baseClass = SystemTypes.Object; cExpr = null;
            } else if ((((ITypeParameter)typeParameter).TypeParameterFlags & TypeParameterFlags.ValueTypeConstraint) != 0) {
              baseClass = SystemTypes.ValueType; cExpr = null;
            } else {
              cExpr = null;
            }
          }
        }
        if (baseClass != null)
          typeParameter = this.ConvertToClassParameter(typeParameter, interfaces, baseClass, cExpr);
        else if ((((ITypeParameter)typeParameter).TypeParameterFlags & TypeParameterFlags.ValueTypeConstraint) != 0)
          typeParameter = this.ConvertToClassParameter(typeParameter, interfaces, SystemTypes.ValueType, null);
      }else if ((((ITypeParameter)typeParameter).TypeParameterFlags & TypeParameterFlags.ReferenceTypeConstraint) != 0)
        typeParameter = this.ConvertToClassParameter(typeParameter, interfaces, null, null);
      else if ((((ITypeParameter)typeParameter).TypeParameterFlags & TypeParameterFlags.ValueTypeConstraint) != 0)
        typeParameter = this.ConvertToClassParameter(typeParameter, interfaces, SystemTypes.ValueType, null);
      return typeParameter;
    }

    private ClassParameter ConvertToClassParameter(TypeNode typeParameter, InterfaceList interfaces, Class baseClass, ClassExpression cExpr){
      ClassParameter cParam = typeParameter is MethodTypeParameter ? new MethodClassParameter() : new ClassParameter();
      this.typeParamToClassParamMap[typeParameter.UniqueKey] = cParam;
      cParam.SourceContext = typeParameter.SourceContext;
      cParam.TypeParameterFlags = ((ITypeParameter)typeParameter).TypeParameterFlags;
      if (typeParameter.IsUnmanaged) { cParam.SetIsUnmanaged(); }
      cParam.Name = typeParameter.Name;
      cParam.Namespace = StandardIds.ClassParameter;
      cParam.BaseClass = baseClass == null ? SystemTypes.Object : baseClass;
      cParam.BaseClassExpression = cExpr;
      cParam.DeclaringMember = ((ITypeParameter)typeParameter).DeclaringMember;
      cParam.DeclaringModule = typeParameter.DeclaringModule;
      cParam.DeclaringType = typeParameter is MethodTypeParameter ? null : typeParameter.DeclaringType;
      cParam.Flags = typeParameter.Flags & ~TypeFlags.Interface;
      cParam.ParameterListIndex = ((ITypeParameter)typeParameter).ParameterListIndex;
      MemberList mems = cParam.DeclaringType == null ? null : cParam.DeclaringType.Members;
      int n = mems == null ? 0 : mems.Count;
      for (int i = 0; i < n; i++){
        if ((mems[i] as TypeNode) == typeParameter){
          mems[i] = cParam;
          break;
        }
      }
      if (cExpr != null){
        n = interfaces.Count - 1;
        InterfaceList actualInterfaces = new InterfaceList(n);
        for (int i = 0; i < n; i++)
          actualInterfaces.Add(interfaces[i + 1]);
        cParam.Interfaces = actualInterfaces;
      }else
        cParam.Interfaces = interfaces;
      if (cExpr != null) cParam.BaseClass = this.VisitClassExpression(cExpr);
      return cParam;
    }
    public override TypeNode VisitTypeReference(TypeNode type) {
      if (type == null) return null;
      TypeNode t = (TypeNode)this.typeParamToClassParamMap[type.UniqueKey];
      if (t != null) return t;

      return base.VisitTypeReference(type);
    }

    public override TypeNode VisitTypeReference(TypeNode type, bool addNonNullWrapperIfNeeded){
      if (type == null) return null;

      // begin change by drunje (allow pointers to managed structures)
      if (this.AllowPointersToManagedStructures && (type.NodeType == NodeType.PointerTypeExpression))
      {
          PointerTypeExpression ptrExpr = (PointerTypeExpression)type;
          TypeNode et = this.VisitTypeReference(ptrExpr.ElementType);
          if ((et != null) && (et.Name != Looker.NotFound) && (!et.IsUnmanaged) && (et.IsValueType))
              return et.GetPointerType();
      }
      // end of change by drunje

      ClassParameter cp = this.typeParamToClassParamMap[type.UniqueKey] as ClassParameter;
      if (cp != null) return cp;

      return base.VisitTypeReference(type, addNonNullWrapperIfNeeded);
    }

    public override TypeNode AddNonNullWrapperIfNeeded(TypeNode typeNode) {
      if (typeNode == null || typeNode is ITypeParameter) return typeNode;
      SpecSharpCompilerOptions options = this.currentOptions as SpecSharpCompilerOptions;
      if (options != null && !options.Compatibility && options.ReferenceTypesAreNonNullByDefault && !typeNode.IsValueType) {
        if (this.typeSystem != null && !this.typeSystem.IsNonNullType(typeNode))
          typeNode = OptionalModifier.For(SystemTypes.NonNullType, typeNode);
      }
      return typeNode;
    }
    public override Expression VisitUnaryExpression(UnaryExpression unaryExpression){
      if (unaryExpression == null) return null;
      if (unaryExpression.NodeType == NodeType.Typeof) this.AbstractSealedUsedAsType = Cci.Error.None;
      Expression result = base.VisitUnaryExpression(unaryExpression);
      this.AbstractSealedUsedAsType = Cci.Error.NotAType;
      return result;
    }
    private static bool ClassHasNoExplicitConstructors(TypeNode type) {
      MemberList members = type.Members;
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++) {
        if (members[i] is InstanceInitializer) return false;
      }
      return true;
    }
    public override InstanceInitializer InjectDefaultConstructor(TypeNode typeNode) {
      if (this.DontInjectDefaultConstructors || typeNode.IsNormalized) return null;
      Class Class = typeNode as Class;
      if (Class != null && Class.Name != null && !(Class is ClassParameter) && ClassHasNoExplicitConstructors(typeNode)) {
        if (Class.IsAbstractSealedContainerForStatics) return null;
        if (Class.PartiallyDefines != null){
          this.InjectDefaultConstructor(Class.PartiallyDefines);
          InstanceInitializer defCons = Class.PartiallyDefines.GetConstructor();
          if (defCons != null && !defCons.HasCompilerGeneratedSignature) 
            defCons = null; //Not an orphan
          if (defCons != null){
            //This is an injected default constructor that is an orphan, adopt it
            defCons.HasCompilerGeneratedSignature = false; //abuse this flag to stop other partial types from adopting it
            Class.Members.Add(defCons);
            Class.BaseClass = ((Class)Class.PartiallyDefines).BaseClass;
          }
          return defCons; //Ok if defCons null, this type should not show up in inheritance chains
        }else{
          //Inject a default constructor
          This thisParameter = new This(Class);
          Class baseClass = Class.BaseClass;
          StatementList statements = new StatementList(2);
          statements.Add(new FieldInitializerBlock(typeNode, false));
          if (baseClass != null) {
            MethodCall mcall = new MethodCall(new QualifiedIdentifier(new Base(), StandardIds.Ctor, typeNode.Name.SourceContext), null);
            mcall.SourceContext = typeNode.Name.SourceContext;
            ExpressionStatement callSupCons = new ExpressionStatement(mcall);
            callSupCons.SourceContext = typeNode.Name.SourceContext;
            statements.Add(callSupCons);
          }
          InstanceInitializer defCons = new InstanceInitializer(typeNode, null, null, new Block(statements));
          defCons.Name = new Identifier(".ctor", typeNode.Name.SourceContext);
          defCons.SourceContext = typeNode.Name.SourceContext;
          defCons.ThisParameter = thisParameter;
          if (typeNode.IsAbstract)
            defCons.Flags |= MethodFlags.Family|MethodFlags.HideBySig;
          else
            defCons.Flags |= MethodFlags.Public|MethodFlags.HideBySig;
          defCons.CallingConvention = CallingConventionFlags.HasThis;
          defCons.IsCompilerGenerated = true;
          typeNode.Members.Add(defCons);
          return defCons;
        }
      }
      return null;
    }
    private void InjectStaticInitializerIfNoneSpecified(TypeNode typeNode){
      if (typeNode.NodeType == NodeType.EnumNode) return;
      MemberList staticCons = typeNode.GetMembersNamed(StandardIds.CCtor);
      if (staticCons != null && staticCons.Count > 0) return;
      StatementList statements = null;
      MemberList members = typeNode.Members;
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
        Field f = members[i] as Field;
        if (f == null) continue;
        if (!f.IsStatic) continue;
        if (f.IsLiteral) continue;
        if (f.Initializer == null) continue;
        statements = new StatementList(1);
        break;
      }
      if (statements == null && typeNode.Contract != null && typeNode.Contract.FrameField != null)
        statements = new StatementList(1);
      if (statements != null){
        FieldInitializerBlock finitBlock = new FieldInitializerBlock(typeNode, true);
        statements.Add(finitBlock);
        StaticInitializer cctor = new StaticInitializer(typeNode, null, new Block(statements));
        typeNode.Members.Add(cctor);
        if (typeNode.PartiallyDefines != null){
          staticCons = typeNode.PartiallyDefines.GetMembersNamed(StandardIds.CCtor);
          if (staticCons == null || staticCons.Count == 0){
            finitBlock.Type = typeNode.PartiallyDefines;
            cctor.DeclaringType = typeNode.PartiallyDefines;
            typeNode.PartiallyDefines.Members.Add(cctor);
          }
        }
      }
    }
    public override void DetermineIfNonNullCheckingIsDesired(CompilationUnit cUnit){
      SpecSharpCompilerOptions soptions = this.currentOptions as SpecSharpCompilerOptions;
      if (soptions != null && soptions.Compatibility) return;
      this.NonNullChecking = !(this.currentPreprocessorDefinedSymbols != null && this.currentPreprocessorDefinedSymbols.ContainsKey("NONONNULLTYPECHECK"));
    }
    public override bool CheckForAttributeAmbiguity(Identifier attributeId){
      if (attributeId != null && attributeId.SourceContext.SourceText != null && attributeId.SourceContext.SourceText.StartsWith("@"))
        return false;
      return true;
    }
    public override void HandleAmbiguousAttributeError(Identifier attributeId, TypeNode t, TypeNode tAttribute){
      this.HandleError(attributeId, Error.AmbiguousAttribute, attributeId.Name, this.GetTypeName(t), this.GetTypeName(tAttribute));
    }
    public override Cci.Declarer GetDeclarer(){
      return new Declarer(this.ErrorHandler);
    }
    private TrivialHashtable uriNamespaceFor = new TrivialHashtable();
    public override Identifier GetUriNamespaceFor(TypeNode type){
      Identifier ns = (Identifier)this.uriNamespaceFor[type.UniqueKey];
      if (ns == null){
        //TODO: look for the attribute.
        //TODO: cache fact that a type does not have the attribute.
      }
      return ns;
    }
    public override string GetTypeName(TypeNode type){
      if (this.ErrorHandler == null) return "";
      ((ErrorHandler)this.ErrorHandler).currentParameter = this.currentParameter;
      return this.ErrorHandler.GetTypeName(type);
    }

    internal void HandleError(Node offendingNode, Error error, params string[] messageParameters){
      if (this.ErrorHandler == null) return;
      ((ErrorHandler)this.ErrorHandler).HandleError(offendingNode, error, messageParameters);
    }
    public static Expression BindPseudoMember(Expression qualifier, Identifier identifier) {
      if (qualifier != null){
        SourceContext fullSourceContext = identifier.SourceContext;
        if (qualifier.SourceContext.Document != null) {
          fullSourceContext.StartPos = qualifier.SourceContext.StartPos;
        }
        TypeNode qualifierType = qualifier.Type;
        if (identifier.UniqueIdKey == Cci.Runtime.IsConsistentId.UniqueIdKey) {
          if (qualifier is Base){
            if (qualifierType != null){
              qualifierType = TypeNode.StripModifiers(qualifierType);
              return new MethodCall(new MemberBinding(null, SystemTypes.Guard.GetMethod(Identifier.For("FrameIsExposable"), SystemTypes.Object, SystemTypes.Type)),
                new ExpressionList(qualifier, new UnaryExpression(new Literal(qualifierType, SystemTypes.Type), NodeType.Typeof, OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Type))), NodeType.Call, SystemTypes.Boolean, fullSourceContext);
            }
          }else{
            return new MethodCall(
              new MemberBinding(null, SystemTypes.Guard.GetMethod(Cci.Runtime.IsConsistentId,
              OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Object))),
              new ExpressionList(qualifier), NodeType.Call, SystemTypes.Boolean, fullSourceContext);
          }
        }
        if (identifier.UniqueIdKey == Cci.Runtime.IsPeerConsistentId.UniqueIdKey) {
          return new MethodCall(
            new MemberBinding(null, SystemTypes.Guard.GetMethod(Cci.Runtime.IsPeerConsistentId,
            OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Object))),
            new ExpressionList(qualifier), NodeType.Call, SystemTypes.Boolean, fullSourceContext);
        }
        if (qualifierType != null) {
          qualifierType = TypeNode.StripModifiers(qualifierType);
          if (identifier.UniqueIdKey == Cci.Runtime.IsVirtualConsistentId.UniqueIdKey) {
            if(qualifier is This || qualifier is ImplicitThis) {
              SourceContext sc = identifier.SourceContext;
              identifier = Cci.Runtime.IsExposableId;
              identifier.SourceContext = sc;
            }
          }
          TypeNode guard = SystemTypes.Guard;
          if (guard != null){
            Property property = guard.GetProperty(identifier);
            if (property != null && property.IsPublic && !property.IsStatic){
              Method method = guard.GetMethod(Identifier.For("Frame" + identifier.Name), SystemTypes.Object, SystemTypes.Type);
              if (method != null && method.IsPublic && method.IsStatic){
                return
                  new MethodCall(
                  new MemberBinding(null, method),
                  new ExpressionList(qualifier, new UnaryExpression(new Literal(qualifierType, SystemTypes.Type), NodeType.Typeof, OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Type))),
                  NodeType.Call,
                  method.ReturnType,
                  fullSourceContext);
              }
            }
          }
        }
        {
          Method method = SystemTypes.Guard.GetMethod(identifier, SystemTypes.Object);
          if (method != null && method.IsPublic && method.IsStatic && method.ReturnType != SystemTypes.Void){
            return new MethodCall(new MemberBinding(null, method), new ExpressionList(qualifier), NodeType.Call, method.ReturnType, fullSourceContext);
          }
        }
      }
      return null;
    }
    public override Expression BindPseudoMember(TypeNode type, Identifier identifier) {
      if (this.insideAssertion && !this.currentMethod.IsStatic && !(this.currentType is Struct)){
        return Looker.BindPseudoMember(this.currentMethod.ThisParameter, identifier);
      }
      return null;
    }
    public override Statement VisitAssertion(Assertion assertion) {
      bool savedInsideAssertion = this.insideAssertion;
      this.insideAssertion = true;
      try {
        return base.VisitAssertion(assertion);
      } finally {
        this.insideAssertion = savedInsideAssertion;
      }
    }
    public override Statement VisitAssumption(Assumption assumption) {
      bool savedInsideAssertion = this.insideAssertion;
      this.insideAssertion = true;
      try {
        return base.VisitAssumption(assumption);
      } finally {
        this.insideAssertion = savedInsideAssertion;
      }
    }
    public override MethodContract VisitMethodContract(MethodContract contract) {
      bool savedInsideAssertion = this.insideAssertion;
      this.insideAssertion = true;
      try {
        return base.VisitMethodContract(contract);
      } finally {
        this.insideAssertion = savedInsideAssertion;
      }
    }
    public override ExpressionList VisitLoopInvariantList(ExpressionList expressions) {
      bool savedInsideAssertion = this.insideAssertion;
      this.insideAssertion = true;
      try{
        return base.VisitLoopInvariantList(expressions);
      }finally{
        this.insideAssertion = savedInsideAssertion;
      }
    }
    public override bool NonNullGenericIEnumerableIsAllowed{
      get {return true;}
    }
    public override LocalDeclaration VisitLocalDeclaration(LocalDeclaration localDeclaration){
      if (localDeclaration == null) return null;
      this.CheckForShadowDeclaration(localDeclaration);
      return base.VisitLocalDeclaration(localDeclaration);
    }
    public override TypeAlias VisitTypeAlias(TypeAlias tAlias) {
      if (tAlias == null) return tAlias;
      this.AddNodePositionAndInfo(tAlias, tAlias, IdentifierContexts.TypeContext);
      return tAlias;
    }
    public void CheckForShadowDeclaration(LocalDeclaration decl){
      Field f = decl.Field;
      if (f == null) return;      
      Scope scope = f.DeclaringType as BlockScope;
      if (scope == null) return;
      scope = scope.OuterScope; // Start search in previous scope
      while (scope != null){
        if (scope is TypeScope) break;
        if (scope is NamespaceScope) break;
        if (scope == null) return;
        MemberList members = scope.GetMembersNamed(f.Name);
        for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
          Field outer = members[i] as Field;
          if (outer == null) continue;
          this.HandleError(decl, Error.LocalShadowsOuterDeclaration, f.Name.Name);
          return;
        }
        scope = scope.OuterScope;
      }
    }
    public override bool LanguageSpecificSupression(Member m){
      Debug.Assert(m != null);
      Method meth = m as Method;
      if (meth != null && meth.Name.UniqueIdKey == StandardIds.Finalize.UniqueIdKey && meth.ReturnType == SystemTypes.Void && (meth.Parameters == null || meth.Parameters.Count == 0))
        return true;
      return false;
    }
  }
}
