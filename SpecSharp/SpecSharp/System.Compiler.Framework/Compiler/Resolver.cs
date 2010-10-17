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
#else
namespace System.Compiler{
#endif
  /// <summary>
  /// Walks an IR, mutating it by resolving overloads and inferring expression result types
  /// Following this, all Indexers, NameBindings, QualifiedIdentifiers, and non built-in operators have been replaced by MemberBindings or MethodCalls 
  /// and every expression has its Type field filled in.
  /// (Exception 1: Indexers whose objects are tuples or single dimensional zero based arrays are not replaced.)
  /// (Exception 2: When resolution fails the NameBindings and QualifiedIdentifiers are not replaced. Checker uses them to generate appropriate errors.)
  /// </summary>
  public class Resolver : StandardCheckingVisitor{
    public AssemblyNode currentAssembly;
    public Switch currentSwitch;
    public Field currentField;
    public bool currentFieldIsCircularConstant;
    public Method currentMethod;
    public Module currentModule;
    public CompilerOptions currentOptions;
    public TypeNode currentType;
    public TypeNode currentTypeInstance;
    public Hashtable currentPreprocessorDefinedSymbols;
    public ContextScope contextScope;
    public bool hasContextReference;
    public TrivialHashtable composerTypes = new TrivialHashtable();
    public TypeSystem typeSystem;
    public bool NonNullChecking;
    public bool useGenerics;
    /// <summary>
    /// Are we inside an assert statement, assume statement, loop invariant, requires clause, or ensures clause, but not in an object invariant?
    /// </summary>
    public bool insideAssertion;

    public Resolver(ErrorHandler errorHandler, TypeSystem typeSystem)
      : base(errorHandler){
      Debug.Assert(typeSystem != null);
      this.typeSystem = typeSystem;
      this.useGenerics = TargetPlatform.UseGenerics;
    }
    public Resolver(Visitor callingVisitor)
      : base(callingVisitor){
    }
    public override void TransferStateTo(Visitor targetVisitor){
      base.TransferStateTo(targetVisitor);
      Resolver target = targetVisitor as Resolver;
      if (target == null) return;
      target.currentAssembly = this.currentAssembly;
      target.currentField = this.currentField;
      target.currentFieldIsCircularConstant = this.currentFieldIsCircularConstant;
      target.currentMethod = this.currentMethod;
      target.currentModule = this.currentModule;
      target.currentOptions = this.currentOptions;
      target.currentType = this.currentType;
      target.currentTypeInstance = this.currentTypeInstance;
      target.contextScope = this.contextScope;
      target.composerTypes = this.composerTypes;
      target.hasContextReference = this.hasContextReference;
      target.typeSystem = this.typeSystem;
      target.useGenerics = this.useGenerics;
      target.insideAssertion = this.insideAssertion;
    }        

    public readonly static TypeNode ArglistDummyType;
    public readonly static Parameter ArglistDummyParameter;
    public readonly static Method ConstructorNotFound;
    public readonly static Method IndexerNotFound;
    public readonly static Method MethodNotFound;
    public readonly static Method SetterNotFound;
    static Resolver(){
      ConstructorNotFound = new Method();
      ConstructorNotFound.Name = Looker.NotFound;
      ConstructorNotFound.ReturnType = SystemTypes.Object;
      IndexerNotFound = new Method();
      IndexerNotFound.Name = Looker.NotFound;
      IndexerNotFound.ReturnType = SystemTypes.Object;
      MethodNotFound = new Method();
      MethodNotFound.Name = Looker.NotFound;
      MethodNotFound.ReturnType = SystemTypes.Object;
      SetterNotFound = new Method();
      SetterNotFound.Name = Looker.NotFound;
      SetterNotFound.ReturnType = SystemTypes.Void;
      ArglistDummyType = new Struct();
      ArglistDummyType.Name = StandardIds.__Arglist;
      ArglistDummyParameter = new Parameter(ArglistDummyType.Name, ArglistDummyType);
    }

    public override Expression VisitAddressDereference(AddressDereference addr){
      if (addr == null) return null;
      addr.Type = SystemTypes.Object; //TODO: arrange for an error if the type is not determined below
      Expression aExpr = addr.Address = this.VisitExpression(addr.Address);
      if (aExpr != null && aExpr.Type != null){
        TypeNode aExprType = TypeNode.StripModifiers(aExpr.Type);
        Reference refType = aExprType as Reference;
        if (refType != null) aExprType = TypeNode.StripModifiers(refType.ElementType); 
        switch(aExprType.NodeType){
          case NodeType.Pointer: addr.Type = ((Pointer)aExprType).ElementType; break;
          case NodeType.Struct:
            if (aExprType.Template == SystemTypes.GenericBoxed)
              addr.Type = this.typeSystem.GetStreamElementType(aExprType, this.TypeViewer);
            break;
        }
      }
      return addr;
    }
    public override Expression VisitAnonymousNestedFunction(AnonymousNestedFunction func){
      if (func == null) return null;
      Method meth = func.Method = this.VisitMethod(func.Method);
      if (meth == null || meth.DeclaringType == null) return null;
      if (meth.Scope != null) {
        meth.Scope.ThisTypeInstance = meth.DeclaringType;
#if WHIDBEY
        if (meth.DeclaringType is ClosureClass) {
          TypeNodeList tpars = meth.DeclaringType.ConsolidatedTemplateParameters;
          if (tpars != null && tpars.Count > 0) {
            //Create a "fake" dummy instance. Using the real template instantiation code will not work
            //because the closure class has not yet been added to the members of its declaring type.
            TypeNode tinst = (TypeNode)meth.DeclaringType.Clone();
            tinst.Template = meth.DeclaringType;
            meth.Scope.ThisTypeInstance = tinst;
          }
        }
#endif
      }
      if (meth.ReturnType == null){
        //No return type inferred for function. This happens when no return or yield were encountered, or when no return has a value.
        this.InferReturnTypeForExpressionFunction(meth);
      }
      func.Type = FunctionType.For(meth.ReturnType, meth.Parameters, this.currentType);
      //TODO: find all places where control leaves the function after an expression statement and insert return statements
      return func;
    }
    public virtual void InferReturnTypeForExpressionFunction(Method meth){
      meth.ReturnType = SystemTypes.Void;
      //Insert an implicit return if the function body is a single expression
      StatementList statements = meth.Body == null ? null : meth.Body.Statements;
      ExpressionStatement es = statements == null || statements.Count != 1 ? null : statements[0] as ExpressionStatement;
      Expression e = es == null ? null : es.Expression;
      // Comprehensions whose type is IEnumerable should not be wrapped in a return
      Comprehension q = es == null ? null : es.Expression as Comprehension;
      if (q != null && q.Type != null && q.Type.Template == SystemTypes.GenericIEnumerable) {
        e = null;
        meth.ReturnType = q.Type;
      }
      if (e != null && e.Type != null) {
        if (this.typeSystem.IsVoid(e.Type))
          statements.Add(new Return());
        else {
          meth.ReturnType = e.Type;
          statements[0] = new Return(e);
        }
      }
    }
    public override Expression VisitArglistArgumentExpression(ArglistArgumentExpression argexp){
      Expression result = base.VisitArglistArgumentExpression(argexp);
      if (result == null) return null;
      result.Type = Resolver.ArglistDummyType;
      return result;
    }
    public override Expression VisitArglistExpression(ArglistExpression arglistexp){
      if (arglistexp == null) return null;
      arglistexp.Type = SystemTypes.RuntimeArgumentHandle;
      return arglistexp;
    }
    public override Expression VisitRefTypeExpression(RefTypeExpression rtexp){
      if (rtexp == null) return null;
      Expression result = base.VisitRefTypeExpression(rtexp);
      if (result != rtexp) return result;
      result.Type = SystemTypes.Type;
      return result;
    }
    public override Expression VisitRefValueExpression(RefValueExpression rvexp){
      if (rvexp == null) return null;
      Expression result = base.VisitRefValueExpression(rvexp);
      if (result != rvexp) return result;
      result.Type = SystemTypes.Object;
      Literal lit = rvexp.Operand2 as Literal;
      if (lit != null){
        TypeNode t = lit.Value as TypeNode;
        if (t != null) {
          result.Type = t.GetReferenceType();
        }
      }
      return result;
    }
    public override AssemblyNode VisitAssembly(AssemblyNode assembly){
      this.currentModule = this.currentAssembly = assembly;
      return base.VisitAssembly(assembly);
    }
    public override Expression VisitAssignmentExpression(AssignmentExpression assignment){
      if (assignment == null) return null;
      assignment.AssignmentStatement = this.VisitAssignmentStatement((AssignmentStatement)assignment.AssignmentStatement);
      AssignmentStatement aStat = assignment.AssignmentStatement as AssignmentStatement;
      if (aStat != null && aStat.Target != null)
        assignment.Type = aStat.Target.Type;
      else{
        ExpressionStatement eStat = assignment.AssignmentStatement as ExpressionStatement;
        if (eStat != null && eStat.Expression != null) assignment.Type = eStat.Expression.Type;
      }
      // result type does not carry forward the reference type
      Reference rt = assignment.Type as Reference;
      if (rt != null) assignment.Type = rt.ElementType;
      return assignment;
    }
    public override Statement VisitAssignmentStatement(AssignmentStatement assignment){
      if (assignment == null) return null;
      if (assignment.Operator == NodeType.Add || assignment.Operator == NodeType.Sub){
        Expression t = this.VisitExpression(assignment.Target);
        DelegateNode dt = TypeNode.StripModifiers(t.Type) as DelegateNode;
        MemberBinding mb = t as MemberBinding;
        if (dt != null && mb != null){
          Event e = mb.BoundMember as Event;
          if (e != null){
            //There is no backing field for this event, use the add or remove accessor
            ExpressionList arguments = new ExpressionList(1);
            Expression src = this.VisitExpression(assignment.Source);
            if (src is MemberBinding && ((MemberBinding)src).BoundMember is Method)
              src = this.VisitExpression(new Construct(new MemberBinding(null, e.HandlerType), new ExpressionList(assignment.Source)));
            arguments.Add(src);
            Method adderOrRemover = assignment.Operator == NodeType.Add ? e.HandlerAdder : e.HandlerRemover;
            adderOrRemover.ObsoleteAttribute = e.ObsoleteAttribute;
            mb.BoundMember = adderOrRemover;
            MethodCall mc = new MethodCall(mb, arguments, NodeType.Call);
            if (!(mb.TargetObject is Base) && e.HandlerAdder.IsVirtualAndNotDeclaredInStruct) 
              mc.NodeType = NodeType.Callvirt;
            mc.Type = SystemTypes.Void;
            mc.SourceContext = assignment.SourceContext;
            Construct c1 = src as Construct;
            if (c1 != null && c1.Type == null)
              c1.Type = t.Type;
            Identifier id = src as Identifier;
            if (id != null && id.Type == null && t.Type != null && t.Type.IsAssignableTo(SystemTypes.Enum))
              id.Type = t.Type;
            return new ExpressionStatement(mc);
          }else if (mb.BoundMember.DeclaringType != this.currentType && (mb.BoundMember is Field && ((Field)mb.BoundMember).IsPrivate)){
            Expression src = this.VisitExpression(assignment.Source);
            if (src is MemberBinding && ((MemberBinding)src).BoundMember is Method)
              src = this.VisitExpression(new Construct(new MemberBinding(null, dt), new ExpressionList(assignment.Source)));
            BinaryExpression bExpr = new BinaryExpression(assignment.Target, src, NodeType.AddEventHandler);
            bExpr.SourceContext = assignment.SourceContext;
            if (assignment.Operator != NodeType.Add) bExpr.NodeType = NodeType.RemoveEventHandler;
            bExpr.Type = SystemTypes.Void;
            Construct c1 = src as Construct;
            if (c1 != null && c1.Type == null)
              c1.Type = t.Type;
            Identifier id = src as Identifier;
            if (id != null && id.Type == null && t.Type != null && t.Type.IsAssignableTo(SystemTypes.Enum))
              id.Type = t.Type;
            return new ExpressionStatement(this.VisitExpression(bExpr), assignment.SourceContext);
          }
        }
      }
      Expression opnd1 = assignment.Target = this.VisitTargetExpression(assignment.Target);
      TypeNode opnd1Type = opnd1 == null ? null : TypeNode.StripModifiers(opnd1.Type);
      Expression opnd2;
      if (opnd1 != null && opnd1Type is DelegateNode) {
        TemplateInstance ti = assignment.Source as TemplateInstance;
        if (ti != null && !ti.IsMethodTemplate){
          ti.IsMethodTemplate = true;
          opnd2 = assignment.Source = this.VisitExpression(new Construct(new MemberBinding(null, opnd1Type), new ExpressionList(assignment.Source)));
        }else{
          Expression source = assignment.Source;
          AnonymousNestedFunction anonFunc = source as  AnonymousNestedFunction;
          if (anonFunc != null) this.FillInImplicitType(anonFunc, (DelegateNode)opnd1Type);
          opnd2 = assignment.Source = this.VisitExpression(assignment.Source);
          MemberBinding mb = opnd2 as MemberBinding;
          if (mb != null && mb.BoundMember is Method)
            opnd2 = assignment.Source = this.VisitExpression(new Construct(new MemberBinding(null, opnd1Type), new ExpressionList(source)));
        }
      }else{
        opnd2 = assignment.Source = this.VisitExpression(assignment.Source);
      }
      if (assignment.Operator == NodeType.Add && opnd1 != null && opnd2 != null){
        TypeNode t1 = this.typeSystem.Unwrap(opnd1.Type);
        TypeNode t2 = this.typeSystem.Unwrap(opnd2.Type);
        if (t1 == SystemTypes.String){
          if (t2 == SystemTypes.String || this.typeSystem.ImplicitCoercionFromTo(t2, SystemTypes.String, this.TypeViewer))
            assignment.OperatorOverload = Runtime.StringConcatStrings;
          else
            assignment.OperatorOverload = Runtime.StringConcatObjects;
          return assignment;
        }
      }
      if (assignment.Operator != NodeType.Nop){
        BinaryExpression binExpr = new BinaryExpression(assignment.Target, assignment.Source, assignment.Operator);
        assignment.OperatorOverload = this.GetBinaryOperatorOverload(binExpr);
        if (assignment.OperatorOverload == null && opnd1 != null){
          TypeNode t1 = this.typeSystem.Unwrap(opnd1.Type);
          if (t1 != null && !t1.IsPrimitiveNumeric && t1 != SystemTypes.Char && !(t1 is EnumNode)){
            Literal lit2 = opnd2 as Literal;
            if (lit2 != null && lit2.Type != null) {
              TypeNode t2 = this.typeSystem.UnifiedType(lit2, t1, this.TypeViewer);
              if (t1 != t2 && t2 != SystemTypes.Object) assignment.UnifiedType = t2;
            }
          }
        }
      }
      if (assignment != null && assignment.Target != null) {
        TypeNode type = assignment.Target.Type;
        Construct c = assignment.Source as Construct;
        if (c != null && c.Type == null)
          c.Type = type;
        Identifier id = assignment.Source as Identifier;
        if (id != null && id.Type == null && type != null && type.IsAssignableTo(SystemTypes.Enum))
          id.Type = type;
      }
      return assignment;
    }
    public virtual void FillInImplicitType(AnonymousNestedFunction anonFunc, DelegateNode delegateNode) {
      if (anonFunc == null || delegateNode == null) return;
      ParameterList afParams = anonFunc.Parameters;
      ParameterList delParams = delegateNode.Parameters;
      if (afParams == null || delParams == null || afParams.Count != delParams.Count) return;
      for (int i = 0, n = afParams.Count; i < n; i++) {
        Parameter p = afParams[i];
        if (p == null || p.Type != null) continue;
        Parameter q = delParams[i];
        if (q != null) p.Type = q.Type;
      }
    }
    public override AttributeNode VisitAttributeNode(AttributeNode attribute){
      if (attribute == null) return null;
      attribute.Expressions = this.VisitExpressionList(attribute.Expressions);
      Expression constructor = attribute.Constructor;
      if (constructor == null) return null;
      switch (constructor.NodeType){
        case NodeType.Literal:
          Literal consTypeLit = attribute.Constructor as Literal;
          if (consTypeLit != null){
            TypeNode attrType = consTypeLit.Value as TypeNode;
            if (attrType == null){
              this.HandleError(attribute, Error.NoSuchType, consTypeLit.SourceContext.SourceText);
              return null;
            }
            if (!this.GetTypeView(attrType).IsAssignableTo((SystemTypes.Attribute))){
              this.HandleError(attribute, Error.NotAnAttribute, this.GetTypeName(attrType));
              this.HandleRelatedError(attrType);
              return null;
            }
            ExpressionList allArguments = attribute.Expressions;
            int n = allArguments == null ? 0: allArguments.Count;
            ExpressionList positionalArguments = new ExpressionList(n);
            for (int i = 0; i < n; i++){
              Expression arg = allArguments[i];
              NamedArgument narg = arg as NamedArgument;
              if (narg != null) continue;
              if (arg == null) return null;
              if (arg is AnonymousNestedFunction){
                this.HandleError(arg, Error.AnonymousNestedFunctionNotAllowed);
                return null;
              }
              positionalArguments.Add(arg);
            }
            MemberList constrs = this.GetTypeView(attrType).GetConstructors();
            Member cons = this.ResolveOverload(constrs, positionalArguments);
            if (cons != null)
              attribute.Constructor = new MemberBinding(null, cons, consTypeLit.SourceContext, constructor);
            else{
              int m = positionalArguments.Count;
              int max = 0;
              bool noOverload = true;
              for (int i = 0, k = constrs == null ? 0 : constrs.Count; i < k; i++){
                InstanceInitializer constr = constrs[i] as InstanceInitializer;
                if (constr == null) continue;
                int cm = constr.Parameters == null ? 0 : constr.Parameters.Count;
                noOverload = false;
                if (cm > max) max = cm;
              }
              if (noOverload)
                this.HandleError(attribute, Error.NoSuchConstructor, this.GetTypeName(attrType));
              else if (max < m){
                string attrName = attrType.Name.ToString();
                if (attrName.Length > 9 && attrName.EndsWith("Attribute")) 
                  attrName = attrName.Substring(0, attrName.Length-9);
                this.HandleError(positionalArguments[max], Error.TooManyArgumentsToAttribute, attrName);
              }else{
                this.HandleError(attribute, Error.NoOverloadWithMatchingArgumentCount, attrType.Name.ToString(), m.ToString());
                for (int i = 0, k = constrs.Count; i < k; i++)
                  this.HandleRelatedError(constrs[i]);
              }
              return null;
            }
          }
          break;
      }
      return attribute;
    }
    public override Expression VisitBase(Base Base){
      if (Base == null) return null;
      if (this.currentType != null){
        switch(this.currentType.NodeType){
          case NodeType.Class : Base.Type = ((Class)this.currentType).BaseClass; break;
          case NodeType.Struct : Base.Type = SystemTypes.ValueType; break;
        }                                        
      }
      if (Base.Type == null) Base.Type = SystemTypes.Object;
      return Base;
    }
    public override Expression VisitApplyToAll(ApplyToAll applyToAll){
      if (applyToAll == null) return null;
      Expression opnd1 = applyToAll.Operand1 = this.VisitExpression(applyToAll.Operand1);
      if (opnd1 == null) return null;
      TypeNode t1 = opnd1.Type;
      TypeNode elemType = this.typeSystem.GetStreamElementType(t1, this.TypeViewer);
      AnonymousNestedFunction func = applyToAll.Operand2 as AnonymousNestedFunction;
      if (func != null && func.Method != null && func.Method.Parameters != null && func.Method.Parameters.Count == 1){
        if (func.Method.Parameters[0].Type != null) return applyToAll; //Already visited
        func.Method.Parameters[0].Type = elemType;
        applyToAll.Operand2 = this.VisitAnonymousNestedFunction(func);
        if (func.Method != null && func.Method.ReturnType != null){
          if (this.typeSystem.IsVoid(func.Method.ReturnType))
            applyToAll.Type = SystemTypes.Void;
          else{
            elemType = this.typeSystem.GetRootType(func.Method.ReturnType, this.TypeViewer);
            if (elemType != func.Method.ReturnType){
              this.HandleError(func, Error.NotYetImplemented, "applying an iterator to each element of stream");
              return null;
            }else
              applyToAll.Type = SystemTypes.GenericIEnumerable.GetTemplateInstance(this.currentType, func.Method.ReturnType);
          }
          return applyToAll;
        }
      }
      Debug.Assert(false);
      return null;
    }
    public override Expression VisitBinaryExpression(BinaryExpression binaryExpression){
      if (binaryExpression == null) return null;
      switch(binaryExpression.NodeType){
        case NodeType.AddEventHandler: return this.VisitAddEventHandler(binaryExpression);
        case NodeType.RemoveEventHandler: return this.VisitRemoveEventHandler(binaryExpression);
      }
      Expression opnd1 = binaryExpression.Operand1 = this.VisitExpression(binaryExpression.Operand1);
      Expression opnd2 = binaryExpression.Operand2 = this.VisitExpression(binaryExpression.Operand2);
      if (opnd1 == null || opnd2 == null) return null;
      TypeNode t1 = opnd1.Type;
      TypeNode t2 = opnd2.Type;
      t1 = this.typeSystem.Unwrap(t1, false, true);
      t2 = this.typeSystem.Unwrap(t2, false, true);
      bool resultIsNullable = false;
      if (this.typeSystem.IsNullableType(t1)){
        resultIsNullable = true;
        t1 = t1.TemplateArguments[0];
      }
      if (this.typeSystem.IsNullableType(t2)) {
        resultIsNullable = true;
        t2 = t2.TemplateArguments[0];
      }
      Expression result = this.VisitBinaryExpression(binaryExpression, opnd1, opnd2, t1, t2);
      if (resultIsNullable){
        if (result.Type == SystemTypes.Object){
          if (Literal.IsNullLiteral(opnd1))
            result.Type = t2;
          else if (Literal.IsNullLiteral(opnd2))
            result.Type = t1;
          else
            return result;
        }
        switch (binaryExpression.NodeType){
          case NodeType.Add:
          case NodeType.And:
          case NodeType.Ceq:
          case NodeType.Cgt:
          case NodeType.Cgt_Un:
          case NodeType.Clt:
          case NodeType.Clt_Un:
          case NodeType.Div:
          case NodeType.Ge:
          case NodeType.Gt:
          case NodeType.Mul:
          case NodeType.Le:
          case NodeType.Lt:
          case NodeType.Or:
          case NodeType.Rem:
          case NodeType.Sub:
          case NodeType.Xor:
            if (result.Type != null && !result.Type.IsReferenceType && !this.typeSystem.IsNullableType(result.Type))
              result.Type = SystemTypes.GenericNullable.GetTemplateInstance(this.currentType, result.Type);
            break;
        }
      }
      bool doEnumPreselectionOnOp = binaryExpression.NodeType == NodeType.Eq || binaryExpression.NodeType == NodeType.Ne
        || binaryExpression.NodeType == NodeType.Or || binaryExpression.NodeType == NodeType.And || binaryExpression.NodeType == NodeType.Xor;
      Identifier id2 = opnd2 as Identifier;
      if (doEnumPreselectionOnOp && id2 != null && id2.Type == null && t1 != null && t1.IsAssignableTo(SystemTypes.Enum)) {
        id2.Type = t1;
      }
      return result;
    }
    public virtual Expression VisitBinaryExpression(BinaryExpression binaryExpression, Expression opnd1, Expression opnd2, TypeNode t1, TypeNode t2){
      if (binaryExpression == null){Debug.Assert(false); return null;}
      if (t1 == SystemTypes.String || t2 == SystemTypes.String) {
        switch (binaryExpression.NodeType) {
          case NodeType.Add:
            if (opnd1 is Literal && opnd2 is Literal && t1 == SystemTypes.String && t2 == SystemTypes.String) {
              string s1 = ((Literal)opnd1).Value as string;
              string s2 = ((Literal)opnd2).Value as string;
              return new Literal(s1 + s2, SystemTypes.String, binaryExpression.SourceContext);
            }
            Method operatorMethod = this.GetBinaryOperatorOverload(binaryExpression);
            if (operatorMethod != null) break;
            if (this.typeSystem.ImplicitCoercionFromTo(t1, SystemTypes.String, this.TypeViewer) && this.typeSystem.ImplicitCoercionFromTo(t2, SystemTypes.String, this.TypeViewer))
              return new MethodCall(new MemberBinding(null, Runtime.StringConcatStrings), new ExpressionList(opnd1, opnd2),
                NodeType.Call, SystemTypes.String, binaryExpression.SourceContext);
            else
              return new MethodCall(new MemberBinding(null, Runtime.StringConcatObjects), new ExpressionList(opnd1, opnd2),
                NodeType.Call, SystemTypes.String, binaryExpression.SourceContext);
          case NodeType.Eq:
          case NodeType.Ne:
            binaryExpression.Type = SystemTypes.Boolean;
            if (t1 == SystemTypes.String && t2 == SystemTypes.String)
              return this.StringValueComparison(binaryExpression);
            else if (t1 == SystemTypes.Object || t2 == SystemTypes.Object)
              return binaryExpression;
            else
              break;
        }
      }
      if ((t1 is DelegateNode) && (t2 is DelegateNode) && t1 != t2 && (binaryExpression.NodeType == NodeType.Eq || binaryExpression.NodeType == NodeType.Ne)) {
        this.HandleError(binaryExpression, Error.BadBinaryOps,
          binaryExpression.NodeType == NodeType.Eq ? "==" : "!=", this.GetTypeName(t1), this.GetTypeName(t2));
        return null;
      }
      if ((t1 != SystemTypes.Object && t2 != SystemTypes.Object) ||
        !(binaryExpression.NodeType == NodeType.Eq || binaryExpression.NodeType == NodeType.Ne) ||
        (t1.Template == SystemTypes.GenericBoxed || t2.Template == SystemTypes.GenericBoxed)){
        Method operatorMethod = this.GetBinaryOperatorOverload(binaryExpression);
        if (operatorMethod != null) {
          MemberBinding callee = new MemberBinding(null, operatorMethod);
          ExpressionList arguments = new ExpressionList(2);
          if (t1 == SystemTypes.Delegate && t2 is DelegateNode && (binaryExpression.NodeType == NodeType.Eq || binaryExpression.NodeType == NodeType.Ne)) {
            if (opnd1 is MemberBinding && ((MemberBinding)opnd1).BoundMember is Method)
              opnd1 = this.VisitExpression(new Construct(new MemberBinding(null, t2), new ExpressionList(opnd1)));
          }
          arguments.Add(opnd1);
          if (t1 is DelegateNode && t2 == SystemTypes.Delegate && (binaryExpression.NodeType == NodeType.Eq || binaryExpression.NodeType == NodeType.Ne)) {
            if (opnd2 is MemberBinding && ((MemberBinding)opnd2).BoundMember is Method)
              opnd2 = this.VisitExpression(new Construct(new MemberBinding(null, t1), new ExpressionList(opnd2)));
          }
          arguments.Add(opnd2);
          MethodCall call = new MethodCall(callee, arguments);
          call.SourceContext = binaryExpression.SourceContext;
          call.Type = operatorMethod.ReturnType;
          switch (binaryExpression.NodeType) {
            case NodeType.LogicalAnd:
            case NodeType.LogicalOr:
              binaryExpression.Operand1 = new Local(call.Type);
              binaryExpression.Operand2 = call;
              binaryExpression.Type = call.Type;
              return binaryExpression;
          }
          return call;
        }else if ((t1 == SystemTypes.String || t2 == SystemTypes.String) &&
          (binaryExpression.NodeType == NodeType.Eq || binaryExpression.NodeType == NodeType.Ne) &&
          this.typeSystem.ImplicitCoercionFromTo(t1, SystemTypes.String, this.TypeViewer) &&
          this.typeSystem.ImplicitCoercionFromTo(t2, SystemTypes.String, this.TypeViewer)){
          return this.StringValueComparison(binaryExpression);
        }
      }
      if (t1 is DelegateNode || t2 is DelegateNode){
        if (binaryExpression.NodeType == NodeType.Add || binaryExpression.NodeType == NodeType.Sub) {
          binaryExpression.Type = this.typeSystem.ImplicitCoercionFromTo(opnd1, t1, t2, this.TypeViewer) ? t2 : t1;
          return binaryExpression;
        }
      }
      if ((t1 is Pointer || t2 is Pointer) && (binaryExpression.NodeType == NodeType.Add || binaryExpression.NodeType == NodeType.Sub)){
        TypeNode elementType = t1 is Pointer ? ((Pointer)t1).ElementType : ((Pointer)t2).ElementType;
        Expression sizeOf = this.VisitUnaryExpression(new UnaryExpression(new Literal(elementType, SystemTypes.Type), NodeType.Sizeof, SystemTypes.UInt32));
        if (binaryExpression.NodeType == NodeType.Sub) {
          if (elementType == SystemTypes.Void) {
            this.HandleError(binaryExpression, Error.VoidError);
            return null;
          }
          if (t1 is Pointer && t2 is Pointer && ((Pointer)t1).ElementType == ((Pointer)t2).ElementType) {
            binaryExpression.Operand1 = new BinaryExpression(opnd1, new Literal(SystemTypes.Int64, SystemTypes.Type), NodeType.ExplicitCoercion, SystemTypes.Int64, opnd1.SourceContext);
            binaryExpression.Operand2 = new BinaryExpression(opnd2, new Literal(SystemTypes.Int64, SystemTypes.Type), NodeType.ExplicitCoercion, SystemTypes.Int64, opnd2.SourceContext);
            binaryExpression.Type = SystemTypes.Int64;
            return new BinaryExpression(binaryExpression, sizeOf, NodeType.Div, SystemTypes.Int64, binaryExpression.SourceContext);
          }
        }
        if (!(t1 is Pointer && t2 is Pointer)) {
          binaryExpression.Type = t1 is Pointer ? t1 : t2;
          if (elementType == SystemTypes.Void) {
            this.HandleError(binaryExpression, Error.VoidError);
            return null;
          }
          sizeOf.Type = SystemTypes.IntPtr;
          if (t1 is Pointer) {
            Literal lit = binaryExpression.Operand2 as Literal;
            if (lit == null || !(lit.Value is int) || ((int)lit.Value) != 1) {
              if (!(sizeOf is Literal) || !(((Literal)sizeOf).Value is int) || (int)((Literal)sizeOf).Value != 1) {
                if (binaryExpression.Operand2.Type == SystemTypes.Int32) binaryExpression.Operand2.Type = SystemTypes.IntPtr;
                binaryExpression.Operand2 = new BinaryExpression(binaryExpression.Operand2, sizeOf, NodeType.Mul, SystemTypes.IntPtr, binaryExpression.Operand2.SourceContext);
              }
            } else
              binaryExpression.Operand2 = sizeOf;
          } else {
            if (binaryExpression.NodeType == NodeType.Sub) return binaryExpression; //Let Checker issue a message
            Literal lit = binaryExpression.Operand1 as Literal;
            if (lit == null || !(lit.Value is int) || ((int)lit.Value) != 1) {
              if (!(sizeOf is Literal) || !(((Literal)sizeOf).Value is int) || (int)((Literal)sizeOf).Value != 1) {
                if (binaryExpression.Operand1.Type == SystemTypes.Int32) binaryExpression.Operand1.Type = SystemTypes.IntPtr;
                binaryExpression.Operand1 = new BinaryExpression(binaryExpression.Operand1, sizeOf, NodeType.Mul, SystemTypes.IntPtr, binaryExpression.Operand1.SourceContext);
              }
            } else
              binaryExpression.Operand1 = sizeOf;
          }
          return binaryExpression;
        }
      }
      binaryExpression.Type = this.InferTypeOfBinaryExpression(t1, t2, binaryExpression);
      if (binaryExpression.Operand1 == null || binaryExpression.Operand2 == null) binaryExpression.IsErroneous = true;
      try{
        bool savedCheckOverflow = this.typeSystem.checkOverflow;
        this.typeSystem.checkOverflow = !this.typeSystem.suppressOverflowCheck;
        MemberBinding mb = opnd1 as MemberBinding;
        if (mb != null && mb.Type == SystemTypes.Delegate && mb.BoundMember is Method && binaryExpression.NodeType == NodeType.Is) { return Literal.False; }
        Literal lit = PureEvaluator.EvalBinaryExpression(this.EvaluateAsLiteral(opnd1), this.EvaluateAsLiteral(opnd2), binaryExpression, this.typeSystem);
        this.typeSystem.checkOverflow = savedCheckOverflow;
        if (lit != null){
          if (binaryExpression.Type != lit.Type) {
            EnumNode enType = binaryExpression.Type as EnumNode;
            if (enType != null && this.typeSystem.ImplicitCoercionFromTo(lit, lit.Type, enType.UnderlyingType, this.TypeViewer))
              lit.Type = enType;
            else if (binaryExpression.Type == SystemTypes.Single && lit.Type == SystemTypes.Double)
              lit.Type = SystemTypes.Single;
            else if (binaryExpression.Type is EnumNode && ((EnumNode)binaryExpression.Type).UnderlyingType == SystemTypes.UInt32 &&
              lit.Type == SystemTypes.Int64 && ((long)lit.Value) <= uint.MaxValue)
              lit = new Literal((uint)(long)lit.Value, SystemTypes.UInt32);
            else if (binaryExpression.Type == SystemTypes.Int64 && lit.Type == SystemTypes.UInt64)
              binaryExpression.Type = SystemTypes.UInt64;
          }
          lit.SourceExpression = binaryExpression;
          lit.SourceContext = binaryExpression.SourceContext;
          if (binaryExpression.NodeType == NodeType.ExplicitCoercion){
            lit.SourceExpression = opnd1;
            lit.TypeWasExplicitlySpecifiedInSource = true;
          }
          return lit;
        }
      }catch (OverflowException){
        if (binaryExpression.Type is EnumNode && this.currentField != null) {
          this.HandleError(binaryExpression, Error.EnumerationValueOutOfRange, this.GetMemberSignature(this.currentField));
          return Literal.Int32Zero;
        }
        this.HandleError(binaryExpression, Error.CTOverflow);
        return null;
      }catch{} //TODO: be more specific
      return binaryExpression;
    }

    public virtual Literal EvaluateAsLiteral(Expression expression){
      MemberBinding mb = expression as MemberBinding;
      if (mb != null){
        Field f = mb.BoundMember as Field;
        if (f != null && f.IsLiteral){
          if (f.DefaultValue != null) return f.DefaultValue;
          if (f == this.currentField){
            if (f.Initializer != null)
              this.HandleError(f.Name, Error.CircularConstantDefinition, this.GetMemberSignature(f));
            this.currentFieldIsCircularConstant = true;
            f.Initializer = null;
            return null;
          }
          if (this.currentField == null) this.currentField = f;
          f.DefaultValue = this.VisitExpression(f.Initializer) as Literal;
          if (this.currentFieldIsCircularConstant)
            f.Initializer = null;
          if (this.currentField == f) this.currentField = null;
          if (f.DefaultValue != null) return f.DefaultValue;
        }
      }
      bool savedCheckOverflow = this.typeSystem.checkOverflow;
      this.typeSystem.checkOverflow = true;
      Literal lit = null;
      BinaryExpression binExpr = expression as BinaryExpression;
      if (binExpr != null) {
        lit = PureEvaluator.TryEvalBinaryExpression(this.EvaluateAsLiteral(binExpr.Operand1),
          this.EvaluateAsLiteral(binExpr.Operand2), binExpr, this.typeSystem);
      }
      UnaryExpression unExpr = expression as UnaryExpression;
      if (unExpr != null){
        lit = PureEvaluator.TryEvalUnaryExpression(this.EvaluateAsLiteral(unExpr.Operand), unExpr);
      }
      this.typeSystem.checkOverflow = savedCheckOverflow;
      if (lit != null) {
        lit.SourceExpression = expression;
        return lit;
      }
      return expression as Literal;
    }
    public virtual Expression StringValueComparison(BinaryExpression binaryExpression){
      if (binaryExpression == null) return null;
      ExpressionList arguments = new ExpressionList(2);
      arguments.Add(binaryExpression.Operand1);
      arguments.Add(binaryExpression.Operand2);
      MethodCall mcall = new MethodCall(new MemberBinding(null, Runtime.StringEquals), arguments, NodeType.Call);
      mcall.Type = SystemTypes.Boolean;
      mcall.SourceContext = binaryExpression.SourceContext;
      if (binaryExpression.NodeType == NodeType.Eq) return mcall;
      UnaryExpression uex = new UnaryExpression(mcall, NodeType.LogicalNot);
      uex.Type = SystemTypes.Boolean;
      uex.SourceContext = binaryExpression.SourceContext;
      return uex;
    }
    public override Block VisitBlock(Block block){
      if (block == null) return null;
      bool savedCheckOverflow = this.typeSystem.checkOverflow;
      bool savedSuppressOverflowCheck = this.typeSystem.suppressOverflowCheck;
      bool savedInsideUnsafeCode = this.typeSystem.insideUnsafeCode;
      this.typeSystem.checkOverflow = block.Checked;
      this.typeSystem.suppressOverflowCheck = block.SuppressCheck;
      if (block.IsUnsafe) this.typeSystem.insideUnsafeCode = true;
      block = base.VisitBlock(block);
      this.typeSystem.checkOverflow = savedCheckOverflow;
      this.typeSystem.suppressOverflowCheck = savedSuppressOverflowCheck;
      this.typeSystem.insideUnsafeCode = savedInsideUnsafeCode;
      return block;
    }
    public override Expression VisitBlockExpression(BlockExpression blockExpression){
      if (blockExpression == null) return null;
      Block b = blockExpression.Block = this.VisitBlock(blockExpression.Block);
      blockExpression.Type = SystemTypes.Void;
      StatementList statements = b == null ? null : b.Statements;
      if (statements != null && statements.Count > 0){
        ExpressionStatement es = statements[statements.Count-1] as ExpressionStatement;
        if (es != null && es.Expression != null && es.Expression.Type != null) {
          blockExpression.Type = es.Expression.Type;
          if (es.Expression is Literal && statements.Count == 1){
            return es.Expression;
          }
        }
      }
      return blockExpression;
    }
    public override Compilation VisitCompilation(Compilation compilation){
      if (compilation == null || compilation.TargetModule == null) return null;
      this.currentOptions = compilation.CompilerParameters as CompilerOptions;
      this.currentModule = compilation.TargetModule;
      this.currentAssembly = this.currentModule.ContainingAssembly;
      return base.VisitCompilation(compilation);
    }
    public override CompilationUnit VisitCompilationUnit(CompilationUnit cUnit){
      if (cUnit == null) return null;
      if (this.ErrorHandler != null)
        this.ErrorHandler.SetPragmaWarnInformation(cUnit.PragmaWarnInformation);
      this.currentPreprocessorDefinedSymbols = cUnit.PreprocessorDefinedSymbols;
      this.DetermineIfNonNullCheckingIsDesired(cUnit);
      CompilationUnit retCUnit = base.VisitCompilationUnit(cUnit);
      if (this.ErrorHandler != null)
        this.ErrorHandler.ResetPragmaWarnInformation();
      return retCUnit;
    }
    public virtual void DetermineIfNonNullCheckingIsDesired(CompilationUnit cUnit){
      this.NonNullChecking = false; //!(this.currentPreprocessorDefinedSymbols != null && this.currentPreprocessorDefinedSymbols.ContainsKey("NONONNULLTYPECHECK"));
    }
    public override Expression VisitConstruct(Construct cons){
      if (cons == null) return cons;
      cons.Owner = this.VisitExpression(cons.Owner);
      cons.Constructor = this.VisitExpression(cons.Constructor);
      MemberBinding mb = cons.Constructor as MemberBinding;
      if (mb == null){
        Literal literal = cons.Constructor as Literal;
        if (literal == null) return cons;
        TypeNode t = literal.Value as TypeNode;
        if (t == null) return cons;
        cons.Type = t;
        cons.Constructor = mb = new MemberBinding(null, t);
        mb.SourceContext = literal.SourceContext;
      }else{
        TypeNode t = mb.BoundMember as TypeNode;
        if (t == null) return cons; //TODO: if the bound member is an instance initializer, use it.
        cons.Type = t;
      }
      AnonymousNestedFunction func = null;
      DelegateNode delType = cons.Type as DelegateNode;
      if (delType != null && cons.Operands != null && cons.Operands.Count == 1){
        Method meth = null;
        Expression ob = Literal.Null;
        Expression e = cons.Operands[0];
        MemberBinding emb = e as MemberBinding;
        if (emb != null) e = emb.BoundMemberExpression;
        TemplateInstance instance = e as TemplateInstance;
        TypeNodeList typeArguments = instance == null ? null : instance.TypeArguments;
        if (instance != null) e = instance.Expression;
        NameBinding nb = e as NameBinding;
        if (nb != null){
          meth = this.ChooseMethodMatchingDelegate(nb.BoundMembers, delType, typeArguments);
          if (meth != null && !meth.IsStatic)
            ob = new ImplicitThis();
          else if (meth == null){
            e = this.VisitExpression(e);
            if (e.Type is DelegateNode){
              meth = this.ChooseMethodMatchingDelegate(this.GetTypeView(e.Type).GetMembersNamed(StandardIds.Invoke), delType, typeArguments);
              if (meth != null)
                ob = e;
            }
          }
        }else{
          QualifiedIdentifier qualId = e as QualifiedIdentifier;
          if (qualId != null){
            ob = qualId.Qualifier = this.VisitExpression(qualId.Qualifier);
            if (ob is Literal && ob.Type == SystemTypes.Type)
              meth = this.ChooseMethodMatchingDelegate(this.GetTypeView((ob as Literal).Value as TypeNode).GetMembersNamed(qualId.Identifier), delType, typeArguments);
            else if (ob == null)
              return null;
            else if (ob != null && ob.Type != null){
              TypeNode oT = TypeNode.StripModifiers(ob.Type);
              Reference rT = oT as Reference;
              if (rT != null) oT = rT.ElementType;
              while (oT != null){
                meth = this.ChooseMethodMatchingDelegate(this.GetTypeView(oT).GetMembersNamed(qualId.Identifier), delType, typeArguments);
                if (meth != null) break;
                oT = oT.BaseType;
              }
            }
            if (meth == null){
              e = this.VisitExpression(e);
              if (e.Type is DelegateNode){
                meth = this.ChooseMethodMatchingDelegate(this.GetTypeView(e.Type).GetMembersNamed(StandardIds.Invoke), delType, typeArguments);
                if (meth != null){
                  qualId.BoundMember = new MemberBinding(e, meth, qualId.Identifier);
                  ob = e;
                }
              }
            }else
              qualId.BoundMember = new MemberBinding(ob, meth, qualId.Identifier);
          }else{
            func = e as AnonymousNestedFunction;
            if (func != null){
              meth = func.Method;
              if (meth != null){
                meth.ReturnType = delType.ReturnType;
                ParameterList mParams = meth.Parameters;
                ParameterList dParams = delType.Parameters;
                int n = mParams == null ? 0 : mParams.Count;
                int m = dParams == null ? 0 : dParams.Count;
                for (int i = 0; i < n; i++){
                  Parameter mPar = mParams[i];
                  if (mPar == null) return null;
                  if (i >= m){
                    if (mPar.Type == null) mPar.Type = SystemTypes.Object;
                    continue;
                  }
                  Parameter dPar = dParams[i];
                  if (mPar.Type == null){
                    if (dPar != null)
                      mPar.Type = dPar.Type;
                    if (mPar.Type == null)
                      mPar.Type = SystemTypes.Object;
                  }
                }
                if (n != m){
                  Node nde = new Expression(NodeType.Nop);
                  if (n == 0)
                    nde.SourceContext = cons.Constructor.SourceContext;
                  else{
                    nde.SourceContext = mParams[0].SourceContext;
                    nde.SourceContext.EndPos = mParams[n-1].SourceContext.EndPos;
                  }
                  this.HandleError(nde, Error.WrongNumberOfArgumentsForDelegate, this.GetTypeName(delType), n.ToString());
                  return null;
                }
                MemberList mems = meth.Scope == null ? null : meth.Scope.Members;
                n = mems == null ? 0 : mems.Count;
                for (int i = 0; i < n; i++){
                  ParameterField f = mems[i] as ParameterField;
                  if (f == null) continue;
                  Parameter p = f.Parameter;
                  if (p != null) f.Type = p.Type;
                }
                func = this.VisitAnonymousNestedFunction(func) as AnonymousNestedFunction;
                if (func == null) return null;
                meth = func.Method;
                if (meth == null || meth.DeclaringType == null) return null;
                ob = new CurrentClosure(meth, meth.DeclaringType);
              }
            }
          }
        }
        if (meth != null){
          Expression ldftn = null;
          MemberBinding memb = new MemberBinding(null, meth, e);
          memb.Type = null; //Signal to Checker not to complain about this reference to a method without parenthesis
          if (meth.IsVirtualAndNotDeclaredInStruct)
            ldftn = new BinaryExpression(new Expression(NodeType.Dup), memb, NodeType.Ldvirtftn);
          else{
            if (meth.IsStatic) ob = Literal.Null;
            ldftn = new UnaryExpression(memb, NodeType.Ldftn);
          }
          ldftn.Type = SystemTypes.IntPtr;
          ExpressionList arguments = cons.Operands = new ExpressionList(2);
          arguments.Add(ob);
          arguments.Add(ldftn);
          if (ob is ImplicitThis && this.currentMethod != null && this.currentMethod.IsStatic &&
            !(this.currentMethod.Scope.CapturedForClosure && meth.DeclaringType == this.currentMethod.Scope.ClosureClass)){
            this.HandleError(e, Error.ObjectRequired, this.GetMemberSignature(meth));
            return null;
          }
        }else{
          cons.Constructor = new Literal(delType);
          return cons;
        }
      }else{
        cons.Operands = this.VisitExpressionList(cons.Operands);
        UnaryExpression op2nd = cons.Operands != null && cons.Operands.Count > 1 ?
          cons.Operands[1] as UnaryExpression : null;
        if (op2nd != null){
          MemberBinding mb2nd = op2nd.Operand as MemberBinding;
          if (mb2nd != null && mb2nd.BoundMember is Method) mb2nd.Type = null;
        }
      }
      
      MemberList members = this.GetTypeView(cons.Type).GetConstructors();
      Method method = this.ResolveOverload(members, cons.Operands) as Method;
     
      if (method == null && cons.Operands != null && cons.Operands.Count == 1){
        Comprehension q = cons.Operands[0] as Comprehension;
        if (q == null) goto End;
        
        Method m2 = this.ResolveOverload(members, new ExpressionList()) as Method;
        //Method m2 = this.GetTypeView(cons.Type).GetConstructor(); // see if there is a nullary .ctor
        if (m2 == null && cons.Type.NodeType == NodeType.Class) goto End;
        TypeNode qType = TypeNode.StripModifiers(q.Type);
        if (q.Elements == null || qType== null || qType.TemplateArguments==null || qType.TemplateArguments.Count==0) goto End;

        if (this.GetTypeView(cons.Type).IsAssignableTo(SystemTypes.IList)){
          method = m2;
          q.AddMethod = SystemTypes.IList.GetMethod(StandardIds.Add,SystemTypes.Object);
        } else if ((q.Elements.Count == 0 || this.GetTypeView(qType.TemplateArguments[0]).IsAssignableTo(SystemTypes.DictionaryEntry)) && this.GetTypeView(cons.Type).IsAssignableTo(SystemTypes.IDictionary)) {
          method = m2;
          q.AddMethod = SystemTypes.IDictionary.GetMethod(StandardIds.Add,SystemTypes.Object,SystemTypes.Object);
        } else if (((q.Elements.Count == 0 || this.GetTypeView(qType.TemplateArguments[0]).IsAssignableTo(SystemTypes.DictionaryEntry)) && 
          (q.AddMethod = this.GetTypeView(cons.Type).GetMethod(StandardIds.Add,SystemTypes.Object, SystemTypes.Object)) != null) && 
          q.AddMethod.ReturnType == SystemTypes.Void){
          method = m2;
        } else if ((q.AddMethod = this.GetTypeView(cons.Type).GetMethod(StandardIds.Add,SystemTypes.Object)) != null &&
          q.AddMethod.ReturnType == SystemTypes.Int32){
          method = m2;
        } else
          q.AddMethod = null;

        // NB: if m2 is assigned to method, then the actual .ctor does *not* match the operands
        // but the Normalizer will compensate for it.
        // 2nd disjunct: don't need a .ctor method to construct a struct

        if ((method != null || cons.Type.NodeType == NodeType.Struct) && q.AddMethod!= null){
          // The Comprehension is going to replace the expression "new T{...}",
          // so it better have the same type
          // But Checker needs the T in the IEnumerable<T> that is sitting in q.Type, so
          // need a place to put it so Checker can find it. REVIEW!!!
          q.TemporaryHackToHoldType = q.Type;
          q.Type = cons.Type;
          if (method != null)
            q.nonEnumerableTypeCtor = method;
          else
            q.nonEnumerableTypeCtor = cons.Type;
          return q;
        }
      }
    End:
      
      if (method != null && method.DeclaringType == cons.Type){
        cons.Constructor = mb;
        mb.BoundMember = method;
      }
      if (cons != null) {
        Method m = method;
        if (m == null && members != null && members.Count > 0)
          m = members[0] as Method;
        if(m != null)
          this.ParameterPreselectionProcessing(m.Parameters, cons.Operands);
      }
      if (func != null){
        func.Invocation = cons;
        func.Type = cons.Type;
        return func;
      }
      if (cons.Type != null && !cons.Type.IsValueType && this.NonNullChecking)
        cons.Type = OptionalModifier.For(SystemTypes.NonNullType, cons.Type);
      return cons;
    }
    public override Expression VisitConstructArray(ConstructArray consArr){
      if (consArr == null) return consArr;
      TypeNode et = consArr.ElementType = this.VisitTypeReference(consArr.ElementType);
      ExpressionList dims = consArr.Operands = this.VisitExpressionList(consArr.Operands);
      consArr.Initializers = this.VisitExpressionList(consArr.Initializers);
      if (et == null && consArr.ElementTypeExpression == null) {
        TypeNodeList tl = new TypeNodeList();
        for (int i = 0, n = consArr.Initializers == null ? 0 : consArr.Initializers.Count; i < n; i++) {
          Expression e = consArr.Initializers[i];
          if (e == null || e.Type == null) continue;
          Literal lit = e as Literal;
          if (lit != null && lit.Value == null) continue; //This prevents null from participating in the type unification, which is by design.
          if (e.Type == null) continue; //e is a bad expression
          tl.Add(e.Type);
        }
        et = this.typeSystem.UnifiedType(tl, this.TypeViewer);
        if (et == null) et = SystemTypes.Object;
        consArr.ElementType = et;
      }
      if (et is DelegateNode) {
        for (int i = 0, n = consArr.Initializers == null ? 0 : consArr.Initializers.Count; i < n; i++) {
          Expression e = consArr.Initializers[i];
          if (e is MemberBinding && ((MemberBinding)e).BoundMember is Method)
            consArr.Initializers[i] = this.VisitExpression(new Construct(new MemberBinding(null, et), new ExpressionList(e)));
        }
      }
      consArr.Owner = this.VisitExpression(consArr.Owner);
      consArr.Type = SystemTypes.Object;
      if (et == null) return null;
      consArr.Type = et.GetArrayType(consArr.Rank);
      if (this.currentPreprocessorDefinedSymbols != null && this.NonNullChecking)
        consArr.Type = OptionalModifier.For(SystemTypes.NonNullType, consArr.Type);
      return consArr;
    }
    public override Expression VisitConstructFlexArray(ConstructFlexArray consArr){
      if (consArr == null) return consArr;
      TypeNode et = consArr.ElementType = this.VisitTypeReference(consArr.ElementType);
      consArr.Operands = this.VisitExpressionList(consArr.Operands);
      consArr.Initializers = this.VisitExpressionList(consArr.Initializers);
      consArr.Type = SystemTypes.Object;
      if (et != null && !(et is TypeExpression)) //TODO: report such errors in Looker
        consArr.Type = SystemTypes.GenericList.GetTemplateInstance(this.currentType, et);
      return consArr;
    }
    public override Expression VisitConstructTuple(ConstructTuple consTuple){
      if (consTuple == null) return null;
      FieldList fields = consTuple.Fields;
      for (int i = 0, n = fields == null ? 0 : fields.Count; i < n; i++){
        Field f = fields[i];
        if (f == null) continue;
        Expression init = f.Initializer = this.VisitExpression(f.Initializer);
        if (init == null) continue;
        f.Type = init.Type;
      }
      consTuple.Type = TupleType.For(consTuple.Fields, this.currentType);
      return consTuple;
    }
    public virtual Method ChooseMethodMatchingDelegate(MemberList members, DelegateNode dt, TypeNodeList typeArguments){
      if (members == null) return null;
      TypeNode drt = dt.ReturnType;
      ParameterList paramList = dt.Parameters;
      int numPars = paramList == null ? 0 : paramList.Count;
      MemberList eligibleMethods = new MemberList();
      for (int i = 0, n = members.Count; i < n; i++){
        Method m = members[i] as Method;
        if (m == null) continue;
        if (m.Parameters == null || m.Parameters.Count != numPars) continue;
        if (typeArguments != null){
          if (m.TemplateParameters == null || m.TemplateParameters.Count == 0) continue;
          m = m.GetTemplateInstance(this.currentType, typeArguments);
        }
        if (numPars > 0 && m.TemplateParameters != null && m.TemplateParameters.Count > 0){
          Method mti = this.InferMethodTemplateArgumentsAndReturnTemplateInstance(m, dt.Parameters);
          if (mti != null) m = mti;
        }
        if (this.NotEligible(m)) continue;
        if (!m.ParametersMatchStructurallyIncludingOutFlag(paramList, true)) continue;
        if (m.ReturnType == null || !(m.ReturnType.IsStructurallyEquivalentTo(drt) || 
          (!m.ReturnType.IsValueType && this.GetTypeView(m.ReturnType).IsAssignableTo(drt)))) continue;
        eligibleMethods.Add(m);
      }
      if (eligibleMethods.Count == 0) return null;
      if (eligibleMethods.Count == 1) return (Method)eligibleMethods[0];
      ExpressionList dummyArgs = new ExpressionList(dt.Parameters.Count);
      for (int i = 0, n = dt.Parameters.Count; i < n; i++) {
        Parameter par = dt.Parameters[i];
        if (par == null || par.Type == null) continue;
        dummyArgs.Add(new Expression(NodeType.Nop, par.Type));
      }
      return (Method)this.ResolveOverload(eligibleMethods, dummyArgs);
    }
    public override Expression VisitConstructDelegate(ConstructDelegate consDelegate){
      if (consDelegate == null) return null;
      consDelegate.DelegateType = TypeNode.StripModifiers(this.VisitTypeReference(consDelegate.DelegateType));
      if (consDelegate.DelegateType == null) return null;
      DelegateNode dt = consDelegate.DelegateType as DelegateNode;
      if (dt == null){
        Node badNode = consDelegate.DelegateTypeExpression;
        if (badNode == null) badNode = consDelegate;
        this.HandleError(badNode, Error.NotADelegate, this.GetTypeName(consDelegate.DelegateType));
        return null;
      }
      TypeNode drt = TypeNode.StripModifiers(dt.ReturnType);
      Expression ob = consDelegate.TargetObject = this.VisitExpression(consDelegate.TargetObject);
      TypeNode ot = ob == null ? SystemTypes.Object : ob.Type;
      if (ot == null) ot = SystemTypes.Object;
      ot = TypeNode.StripModifiers(ot);
      if (ot == SystemTypes.Type){
        MemberBinding memb = ob as MemberBinding;
        if (memb != null && memb.BoundMember is TypeNode) 
          ot = (TypeNode)memb.BoundMember;
        else{
          Literal lit = ob as Literal;
          if (lit != null && lit.Value is TypeNode)
            ot = (TypeNode)lit.Value;
        }
        Literal nullLit = new Literal(null, SystemTypes.Object);
        if (ob != null) nullLit.SourceContext = ob.SourceContext;
        ob = nullLit;
      }
      Method meth = null;
      for (; ; ) {
        MemberList members = this.GetTypeView(ot).GetMembersNamed(consDelegate.MethodName);
        meth = this.ChooseMethodMatchingDelegate(members, dt, null);
        if (meth != null) break;
        if (ot.BaseType == null) {
          //Let Checker issue a message
          return new Construct(new Literal(dt, SystemTypes.Type), new ExpressionList(consDelegate.MethodName), consDelegate.SourceContext);
        }
        ot = ot.BaseType;
      }
      MemberBinding mb = new MemberBinding(null, meth, consDelegate.MethodName);
      Expression ldftn = null;
      if (meth.IsVirtualAndNotDeclaredInStruct)
        ldftn = new BinaryExpression(new Expression(NodeType.Dup), mb, NodeType.Ldvirtftn);
      else {
        if (meth.IsStatic) ob = Literal.Null;
        ldftn = new UnaryExpression(mb, NodeType.Ldftn);
      }
      ldftn.Type = SystemTypes.IntPtr;
      ExpressionList arguments = new ExpressionList(2);
      arguments.Add(ob);
      arguments.Add(ldftn);
      MemberList constructors = dt.GetConstructors();
      Method method = this.ResolveOverload(constructors, arguments) as Method;
      if (meth == null) {
        //Let Checker issue a message
        return new Construct(new Literal(dt, SystemTypes.Type), new ExpressionList(consDelegate.MethodName), consDelegate.SourceContext);
      }
      return new Construct(new MemberBinding(null, method), arguments, dt, consDelegate.SourceContext);
    }
    public override Expression VisitImplicitThis(ImplicitThis This){
      if (This == null) return null;
      TypeNode currentType = this.currentTypeInstance;
      if (currentType == null)
        This.Type = SystemTypes.Object;
      else{
        if (currentType.IsValueType)
          This.Type = currentType.GetReferenceType();
        else
          This.Type = currentType;
      }
      return This;
    }
    public virtual Expression VisitIndexerOperands(Indexer indexer){
      if (indexer == null) return indexer;
      indexer.Operands = this.VisitExpressionList(indexer.Operands);
      return indexer;
    }
    public virtual Expression VisitIndexerCore(Indexer indexer){
      if (indexer == null) return null;
      indexer.Object = this.VisitExpression(indexer.Object);
      return this.VisitIndexerOperands(indexer);
    }
    public override Expression VisitIndexer(Indexer indexer){
      if (indexer == null) return null;
      Expression indexerObject = indexer.Object;
      Expression result = this.VisitIndexerCore(indexer);
      if (result == null) return indexer;
      if (!(result is Indexer)) return result;
      return VisitIndexerResolve((Indexer)result, indexerObject);
    }
    public virtual Expression VisitIndexerResolve(Indexer indexer, Expression originalObject){
      ExpressionList indices = indexer.Operands;
      Method method = Resolver.IndexerNotFound;
      if (indexer.Object != null){
        TypeNode obType = this.typeSystem.Unwrap(indexer.Object.Type, true);
        TupleType tupT = obType as TupleType;
        if (tupT != null && tupT.Members != null && tupT.Members.Count > 2){
          indexer.Type = SystemTypes.Object;
          if (indices != null && indices.Count > 0){
            Literal lit = indices[0] as Literal;
            if (lit != null && lit.Value is int){
              int i = (int)lit.Value;
              if (i >= 0 && i <= tupT.Members.Count-3){
                Field f = tupT.Members[i] as Field;
                if (f != null) indexer.Type = f.Type;
              }else{
                this.HandleError(lit, Error.BadTupleIndex, (tupT.Members.Count-3).ToString());
                return null;
              }
            }
          }
          return indexer;
        }
        ArrayType arrT = obType as ArrayType;
        if (arrT != null){
          int rank = arrT.Rank;
          int n = indices == null ? 0 : indices.Count;
          if (rank != n){
            this.HandleError(indexer, Error.WrongNumberOfIndices, rank.ToString());
            return null;
          }
          indexer.Type = arrT.ElementType;
          return indexer;
        }else{
          if (obType.IsPointerType){
            return ResolveIndexedPointer(indexer, obType);
          }else{
            Property p = null;
            Interface iface = obType as Interface;
            if (iface != null) {
              Property prop = this.ResolveOverload(((Interface)this.GetTypeView(iface)).GetAllDefaultMembers(), indexer.Operands, indexer.ArgumentListIsIncomplete) as Property;
              if (prop != null) {
                p = prop;
                if (prop.Getter != null) { method = prop.Getter; }
              }
            } else {
              while (obType != null) {
                Property prop = this.ResolveOverload(this.GetTypeView(obType).DefaultMembers, indexer.Operands, indexer.ArgumentListIsIncomplete) as Property;
                if (prop != null) {
                  p = prop;
                  if (prop.Getter != null) { method = prop.Getter; break; }
                }
                obType = obType.BaseType;
              }
            }
            if (p != null && method == Resolver.IndexerNotFound){
              //Found a property with matching operands, but it has no getter
              indexer.CorrespondingDefaultIndexedProperty = p;
              return indexer;
            }
          }
        }
      }
      MemberBinding mb = new MemberBinding(indexer.Object, method);
      if (indexer.Object != null) mb.SourceContext = indexer.Object.SourceContext;
      if (originalObject is NameBinding)
        ((NameBinding)originalObject).BoundMember = mb;
      else if (originalObject is QualifiedIdentifier)
        ((QualifiedIdentifier)originalObject).BoundMember = mb;
      MethodCall call = new MethodCall(mb, indexer.Operands);
      if (!(indexer.Object is Base) && method.IsVirtualAndNotDeclaredInStruct)
        call.NodeType = NodeType.Callvirt;
      else
        call.NodeType = NodeType.Call;
      call.Type = method.ReturnType;
      call.SourceContext = indexer.SourceContext;
      return call;
    }
    public virtual Expression ResolveIndexedPointer(Indexer indexer, TypeNode type) 
      // requires ptrT.IsPointerType;
    {
      Pointer ptrT = (Pointer)TypeNode.StripModifiers(type);
      indexer.Type = indexer.ElementType = ptrT.ElementType;
      Expression opnd0;
      if (indexer.Operands != null && indexer.Operands.Count == 1 && (opnd0 = this.typeSystem.CoerceToIndex(indexer.Operands[0], this.TypeViewer)) != null){
        Expression elemSize = this.VisitUnaryExpression(new UnaryExpression(new Literal(indexer.Type, SystemTypes.Type), NodeType.Sizeof));
        if (opnd0.Type != SystemTypes.Int32) elemSize = this.typeSystem.ExplicitCoercion(elemSize, opnd0.Type, this.TypeViewer);
        BinaryExpression index = new BinaryExpression(opnd0, elemSize, NodeType.Mul, opnd0.Type, opnd0.SourceContext);
        if (opnd0 is Literal) 
          indexer.Operands[0] = this.VisitBinaryExpression(index); //Fold expression to constant
        else
          indexer.Operands[0] = index;
      }
      return indexer;
    }
    public override Statement VisitLocalDeclarationsStatement(LocalDeclarationsStatement localDeclarations){
      if (localDeclarations == null) return null;
      TypeNode type = localDeclarations.Type = this.VisitTypeReference(localDeclarations.Type);
      if (!localDeclarations.Constant && !localDeclarations.InitOnly) return localDeclarations;
      LocalDeclarationList decls = localDeclarations.Declarations;
      for (int i = 0, n = decls.Count; i < n; i++){
        LocalDeclaration decl = decls[i];
        Field f = decl.Field;
        if (type != null)
          f.Type = type;
        f.Initializer = decl.InitialValue = this.VisitExpression(f.Initializer);
        Construct c = decl.InitialValue as Construct;
        if (c != null && c.Type == null)
          c.Type = type;
        Identifier id = decl.InitialValue as Identifier;
        if (id != null && id.Type == null && type != null && type.IsAssignableTo(SystemTypes.Enum))
          id.Type = type;
      }
      return localDeclarations;
    }
    public override Expression VisitMemberBinding(MemberBinding memberBinding){
      if (memberBinding == null) return null;
      Field f = memberBinding.BoundMember as Field;
      if (f != null && f.IsLiteral){
        Literal defaultValue = f.DefaultValue;
        if (defaultValue == null) defaultValue = f.DefaultValue = this.EvaluateAsLiteral(f.Initializer);
        if (defaultValue != null){
          Literal lit = (Literal)defaultValue.Clone();
          lit.SourceContext = memberBinding.SourceContext;
          lit = this.CoerceLiteral(memberBinding, f, lit);
          if (lit != null) return lit;
        }
      }
      memberBinding.TargetObject = this.VisitExpression(memberBinding.TargetObject);
      Member boundMember = memberBinding.BoundMember;
      if (memberBinding.Type == null && boundMember != null){
        switch(boundMember.NodeType){
          case NodeType.Field : memberBinding.Type = ((Field)boundMember).Type; break;
          case NodeType.Method : memberBinding.Type = ((Method)boundMember).ReturnType; break;
          case NodeType.Event : memberBinding.Type = ((Event)boundMember).HandlerType; break;
          default : memberBinding.Type = boundMember as TypeNode; break;
        }
      }
      return memberBinding;
    }
    public virtual Literal CoerceLiteral(Expression expr, Field f, Literal lit){
      if (expr == null || f == null || lit == null){Debug.Assert(false); return null;}
      EnumNode en = f.Type as EnumNode;
      if (en != null && f.DeclaringType == en)
        lit = this.typeSystem.ImplicitLiteralCoercion(lit, lit.Type, en.UnderlyingType, this.TypeViewer);
      if (lit != null)
        lit = this.typeSystem.ExplicitLiteralCoercion(lit, lit.Type, f.Type, this.TypeViewer) as Literal;
      if (lit != null) {
        lit.SourceExpression = expr;
        expr.Type = lit.Type;
        return lit;
      }
      return null;
    }
    public override Method VisitMethod(Method method){
      if (method == null) return null;
      if (method.IsNormalized) return method;
      if (method.Scope != null) method.Scope.ThisTypeInstance = this.currentTypeInstance;
      method.Attributes = this.VisitAttributeList(method.Attributes);
      method.ReturnAttributes = this.VisitAttributeList(method.ReturnAttributes);
      method.SecurityAttributes = this.VisitSecurityAttributeList(method.SecurityAttributes);
      Method savedCurrentMethod = this.currentMethod;
      this.currentMethod = method;
      method.ReturnType = this.VisitTypeReference(method.ReturnType);
      TypeNodeList implementedTypes = method.ImplementedTypes = this.VisitTypeReferenceList(method.ImplementedTypes);
      method.Parameters = this.VisitParameterList(method.Parameters);
      method.TemplateArguments = this.VisitTypeReferenceList(method.TemplateArguments);
      method.TemplateParameters = this.VisitTypeParameterList(method.TemplateParameters);
      method.Contract = this.VisitMethodContract(method.Contract);
      method = this.CheckMethodProperties(method);
      if (method == null) {
        this.currentMethod = savedCurrentMethod;
        return method;
      }
      method.Body = this.VisitBlock(method.Body);
      StreamTypeExpression stExpr = method.ReturnType as StreamTypeExpression;
      TypeUnionExpression tuExpr = stExpr != null ? (TypeUnionExpression)stExpr.ElementType : (method.ReturnType as TypeUnionExpression);
      if (tuExpr != null){
        TypeNodeList types = new TypeNodeList();
        TrivialHashtable alreadyPresent = new TrivialHashtable();
        //REVIEW: this seems redundant
        for (int i = 0, m = tuExpr.Types == null ? 0 : tuExpr.Types.Count; i < m; i++){
          TypeNode t = tuExpr.Types[i];
          if (t == null) continue;
          if (alreadyPresent[t.UniqueKey] != null) continue;
          types.Add(t);
          alreadyPresent[t.UniqueKey] = t;
        }
        if (types.Count == 1) 
          method.ReturnType = types[0];
        else
          method.ReturnType = TypeUnion.For(types, this.currentType);
      }
      if (stExpr != null)
        method.ReturnType = SystemTypes.GenericIEnumerable.GetTemplateInstance(this.currentType, method.ReturnType);
      int n = implementedTypes == null ? 0 : implementedTypes.Count;
      MethodList implementedInterfaceMethods = method.ImplementedInterfaceMethods = n == 0 ? null : new MethodList(n);
      for (int i = 0; i < n; i++){
        Interface iface = implementedTypes[i] as Interface;
        Method meth = null;
        if (iface != null){
          MemberList members = this.GetTypeView(iface).GetMembersNamed(method.Name);
          for (int j = 0, m = members.Count; j < m; j++){
            Method im = members[j] as Method;
            if (im == null) continue;
            if (im.ReturnType == null || !im.ReturnType.IsStructurallyEquivalentTo(method.ReturnType)) continue;
            if (!im.ParametersMatchStructurally(method.Parameters)) continue;
            meth = im;
            break;
          }
        }
        implementedInterfaceMethods.Add(meth);
      }
      this.currentMethod = savedCurrentMethod;
      return method;
    }
    // an extension point
    protected virtual Method CheckMethodProperties(Method method) {
      return method;
    }
    public override Expression VisitMethodCall(MethodCall call){
      if (call == null || call.Callee == null) return null;
      MemberList members = this.GetMembers(call.Callee);
      this.InferAnonymousMethodParameterTypes(members, call.Operands);
      call.Operands = this.VisitExpressionList(call.Operands);
      call.Type = SystemTypes.Void;
      Expression thisob = null;
      MemberList result = this.InferMethodTemplateArguments(members, call.Operands, call.ArgumentListIsIncomplete);
      if (result.Count != 0)
        members = result;
      Method method = this.ResolveOverload(members, call.Operands, call.ArgumentListIsIncomplete) as Method;
      if (method == null){
        SourceContext sctx = call.Callee.SourceContext;
        QualifiedIdentifier qualId = call.Callee as QualifiedIdentifier;
        TemplateInstance templInst = call.Callee as TemplateInstance;
        if (templInst != null) qualId = templInst.Expression as QualifiedIdentifier;
        Expression callee = this.VisitExpression(call.Callee);
        TypeNode calleeType = callee == null ? null : callee.Type;
        if (calleeType is Reference) calleeType = ((Reference)calleeType).ElementType;
        calleeType = TypeNode.StripModifiers(calleeType);
        DelegateNode delType = calleeType as DelegateNode;
        if (delType != null){
          members = this.GetTypeView(delType).GetMembersNamed(StandardIds.Invoke);
          method = this.ResolveOverload(members, call.Operands) as Method;
          if (method == null){
            call.Callee = callee;
            MemberBinding memb = callee as MemberBinding;
            if (memb != null && memb.BoundMember is Event) return call;
            for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
              if (members[i] is Method){
                int numArgs = call.Operands == null ? 0 : call.Operands.Count;
                this.HandleError(call, Error.WrongNumberOfArgumentsForDelegate, this.GetTypeName(delType), numArgs.ToString());
                this.HandleRelatedError(delType);
                return null;
              }
            }
            return call;
          }
          if (!method.IsStatic) thisob = callee;
          MemberBinding mb = new MemberBinding(thisob, method);
          mb.SourceContext = callee.SourceContext;
          if (call.Callee is QualifiedIdentifier) 
            mb.BoundMemberExpression = ((QualifiedIdentifier)call.Callee).Identifier;
          this.UpdateCallee(call, mb);
          call.NodeType = NodeType.Callvirt;
          call.Type = method.ReturnType;
          return call;
        }
        //Special case check for case where there are methods to call, but none match the argument list
        for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
          Method m = members[i] as Method;
          if (m != null){
            if (call.Callee != null && m.ImplementedTypes != null && m.ImplementedTypes.Count > 0){
              call.Callee.SourceContext = sctx;
              thisob = this.GetObject(call.Callee);
              if (thisob is ImplicitThis)
                this.HandleError(call.Callee, Error.IdentifierNotFound, m.Name.ToString(), this.GetTypeName(this.currentType));
              else if (thisob != null && thisob.Type != null) {
                Node offNode = call.Callee is QualifiedIdentifier ? ((QualifiedIdentifier)call.Callee).Identifier : call.Callee;
                if (offNode == null) offNode = call.Callee;
                this.HandleError(offNode, Error.NoSuchMember, this.GetTypeName(thisob.Type), m.Name.ToString());
              }
              members = null;
            }
            call.Callee = new NameBinding(Looker.NotFound, members, null, 0);
            call.Callee.SourceContext = sctx;
            this.ParameterPreselectionProcessing(m.Parameters, call.Operands);
            return call;
          }
        }
        if (qualId != null)
          sctx = qualId.Identifier.SourceContext;
        if (callee != null) callee.SourceContext = sctx;
        call.Callee = callee;
        return call;
      }
      if (method != null){
        if (method.IsStatic) {
          thisob = this.GetObject(call.Callee);
          if (thisob != null && (thisob.Type == SystemTypes.Type || thisob is ImplicitThis))
            thisob = null;
        }else
          thisob = this.GetObject(call.Callee);
        MemberBinding mb = new MemberBinding(thisob, method);
        mb.SourceContext = call.Callee.SourceContext;
        if (call.Callee is QualifiedIdentifier) 
          mb.BoundMemberExpression = ((QualifiedIdentifier)call.Callee).Identifier;
        this.UpdateCallee(call, mb);
        if (!(thisob is Base) && method.IsVirtualAndNotDeclaredInStruct)
          call.NodeType = NodeType.Callvirt;
        else
          call.NodeType = NodeType.Call;
        call.Type = method.ReturnType;
        this.ParameterPreselectionProcessing(method.Parameters, call.Operands);
      }
      return call;
    }
    public virtual void ParameterPreselectionProcessing(ParameterList paramList, ExpressionList exprList) {
      if (paramList == null || exprList == null) return;
      int len = Math.Min(paramList.Count, exprList.Count);
      for (int i = 0; i < len; ++i) {
        Parameter p = paramList[i];
        if (p == null)
          continue;
        TypeNode type = p.Type;
        if (type == null)
          continue;
        Construct c = exprList[i] as Construct;
        if (c != null && c.Type == null)
          c.Type = type;
        Identifier id = exprList[i] as Identifier;
        if (id != null && id.Type == null && type.IsAssignableTo(SystemTypes.Enum))
          id.Type = type;
      }
    }
    public virtual void InferAnonymousMethodParameterTypes(MemberList members, ExpressionList expressionList) {
      if (members == null || expressionList == null) return;
      for (int i = 0, n = expressionList.Count; i < n; i++) {
        AnonymousNestedFunction anonFunc = expressionList[i] as AnonymousNestedFunction;
        if (anonFunc == null || anonFunc.Parameters == null) continue;
        for (int j = 0, m = members.Count; j < m; j++) {
          Method meth = members[i] as Method;
          if (meth == null || meth.Parameters == null || meth.Parameters.Count <= i) continue;
          Parameter p = meth.Parameters[i];
          if (p == null) continue;
          DelegateNode del = p.Type as DelegateNode;
          if (del == null) continue;
          if (del.Parameters == null || del.Parameters.Count != anonFunc.Parameters.Count) continue;
          this.FillInImplicitType(anonFunc, del);
          break;
        }
      }
    }
    public virtual MemberList InferMethodTemplateArguments(MemberList members, ExpressionList arguments, bool doNotDiscardOverloadsWithMoreParameters){
      if (arguments == null) return members;
      int numArgs = arguments.Count;
      if (numArgs == 0) return members;
      MemberList result = null;
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
        Method m = members[i] as Method;
        if (m == null || m.Parameters == null) continue;
        int numPars = m.Parameters.Count;
        if (m.Parameters.Count == 0) continue;
        TypeNode paramArrayElemType =  m.Parameters[numPars-1] == null ? null : m.Parameters[numPars-1].GetParamArrayElementType();
        if (numPars > numArgs){
          if (!doNotDiscardOverloadsWithMoreParameters && (numPars != numArgs+1 || paramArrayElemType == null)) continue;
        }else if (numPars < numArgs && paramArrayElemType == null) {
          continue;
        }
        if (m.TemplateParameters == null || m.TemplateParameters.Count == 0) continue;
        Method mti = this.InferMethodTemplateArgumentsAndReturnTemplateInstance(m, arguments, doNotDiscardOverloadsWithMoreParameters, paramArrayElemType);
        if (mti == null || mti == m) continue;
        if (result == null) result = members.Clone();
        result[i] = mti;
      }
      if (result == null) return members;
      return result;
    }
    public virtual Method InferMethodTemplateArgumentsAndReturnTemplateInstance(Method method, ExpressionList arguments, bool allowPartialInference, TypeNode paramArrayElemType){
      if (method == null || method.Parameters == null || method.Parameters.Count == 0 || 
        method.TemplateParameters == null || method.TemplateParameters.Count == 0){Debug.Assert(false); return method;}
      if (arguments == null) return method;
      int numArgs = arguments.Count;
      int numPars = method.Parameters.Count;
      if (numArgs == 0 || (numArgs != numPars && paramArrayElemType == null && (!allowPartialInference || numArgs > numPars))){
        Debug.Assert(false); 
        return method;
      }
      TrivialHashtable inferredTypeFor = new TrivialHashtable();
      for (int i = 0; i < numArgs; i++){
        Expression arg = arguments[i]; if (arg == null) continue;
        TypeNode argType = TypeNode.StripModifiers(arg.Type);
        if (arg is Literal && argType == SystemTypes.Object && ((Literal)arg).Value == null) continue;
        if (arg is AnonymousNestedFunction) continue;
        Parameter par = method.Parameters[i]; if (par == null) continue;
        TypeNode parType = TypeNode.StripModifiers(par.Type);
        Reference reft = argType as Reference;
        if (reft != null && !(arg is UnaryExpression)) argType = reft.ElementType;
        if (i == numPars - 1 && paramArrayElemType != null)
          if (!(argType is ArrayType && parType is ArrayType))
            parType = paramArrayElemType;
        if (!this.InferMethodTemplateArguments(argType, arg as MemberBinding, parType, inferredTypeFor)) return method;
        if (i == numPars-1) break;
      }
      int numTypeArgs = method.TemplateParameters.Count;
      TypeNodeList typeArguments = new TypeNodeList(numTypeArgs);
      for (int i = 0; i < numTypeArgs; i++){
        TypeNode templPar = method.TemplateParameters[i]; 
        if (templPar == null) return method;
        TypeNode templArg = inferredTypeFor[templPar.UniqueKey] as TypeNode;
        if (templArg == null && !allowPartialInference) return method;
        if (templArg == null) templArg = templPar;
        typeArguments.Add(templArg);
      }
      return method.GetTemplateInstance(this.currentType, typeArguments);
    }
    public virtual Method InferMethodTemplateArgumentsAndReturnTemplateInstance(Method method, ParameterList delegateParameters){
      if (method == null || method.Parameters == null || method.Parameters.Count == 0 || 
        method.TemplateParameters == null || method.TemplateParameters.Count == 0){Debug.Assert(false); return method;}
      if (delegateParameters == null) return method;
      int numParams = delegateParameters.Count;
      if (numParams == 0 || numParams != method.Parameters.Count){Debug.Assert(false); return method;}
      TrivialHashtable inferredTypeFor = new TrivialHashtable();
      for (int i = 0; i < numParams; i++){
        Parameter dpar = delegateParameters[i]; if (dpar == null || dpar.Type == null) continue;
        Parameter mpar = method.Parameters[i]; if (mpar == null) continue;
        if (!this.InferMethodTemplateArguments(dpar.Type, null, mpar.Type, inferredTypeFor)) return method;
      }
      int numTypeArgs = method.TemplateParameters.Count;
      TypeNodeList typeArguments = new TypeNodeList(numTypeArgs);
      for (int i = 0; i < numTypeArgs; i++){
        TypeNode templPar = method.TemplateParameters[i]; 
        if (templPar == null) return method;
        TypeNode templArg = inferredTypeFor[templPar.UniqueKey] as TypeNode;
        if (templArg == null) return method;
        typeArguments.Add(templArg);
      }
      return method.GetTemplateInstance(this.currentType, typeArguments);
    }
    /// <summary>
    /// We pass the argument expression in case we are dealing with an implicit delegate construction
    /// </summary>
    /// <param name="argExpr">This is the actual argument expression</param>
    public virtual bool InferMethodTemplateArguments(TypeNode argType, MemberBinding argExpr, TypeNode parType, TrivialHashtable inferredTypeFor){
      if (argType == null || parType == null) return false;
      if (inferredTypeFor == null){Debug.Assert(false); return false;}
      TypeNode modifiedArgType = argType;
      TypeNode modifiedParType = parType;
      argType = TypeNode.StripModifiers(argType);
      parType = TypeNode.StripModifiers(parType);
      if (parType is MethodTypeParameter || parType is MethodClassParameter){
        TypeNode prevInference = inferredTypeFor[parType.UniqueKey] as TypeNode;
        if (prevInference != null) {
          if (!prevInference.IsStructurallyEquivalentTo(modifiedArgType)) {
            if (!TypeNode.StripModifiers(prevInference).IsStructurallyEquivalentTo(argType)) return false;
            if (!this.typeSystem.ImplicitCoercionFromTo(argType, prevInference)) return false;
          }
        } else
          inferredTypeFor[parType.UniqueKey] = modifiedArgType;
        return true;
      }
      ArrayType pArrT = parType as ArrayType;
      ArrayType aArrT = argType as ArrayType;
      if (pArrT != null){
        if (aArrT == null || aArrT.Rank != pArrT.Rank) return false; //TODO: param arrays
        return this.InferMethodTemplateArguments(aArrT.ElementType, null, pArrT.ElementType, inferredTypeFor);
      }
      Reference pRefT = parType as Reference;
      Reference aRefT = argType as Reference;
      if (pRefT != null) {
        if (aRefT == null) return false;
        return this.InferMethodTemplateArguments(aRefT.ElementType, null, pRefT.ElementType, inferredTypeFor);
      }
      if (parType.IsStructural && argType.IsStructural){
        TypeNodeList parElemTypes = parType.StructuralElementTypes;
        TypeNodeList argElemTypes = argType.StructuralElementTypes;
        int n = parElemTypes == null ? 0 : parElemTypes.Count;
        int m = argElemTypes == null ? 0 : argElemTypes.Count;
        if (parType.Template != null && argType.Template != null && parType.Template == argType.Template) {
          for (int i = 0; i < n; i++) {
            TypeNode peType = parElemTypes[i]; if (peType == null) return false;
            TypeNode aeType = argElemTypes[i]; if (aeType == null) return false;
            if (!this.InferMethodTemplateArguments(aeType, null, peType, inferredTypeFor)) return false;
          }
          if (parType.DeclaringType == null) return true;
          if (argType == parType) return true;
          if (argType.DeclaringType != null && parType.DeclaringType != null && this.InferMethodTemplateArguments(argType.DeclaringType, null, parType.DeclaringType, inferredTypeFor)) {
            if (argType.Template != null) argType = argType.Template;
            if (parType.Template != null) parType = parType.Template;
            return argType.Name != null && parType.Name != null && argType.Name.UniqueIdKey == parType.Name.UniqueIdKey;
          }
          return true;
        } else {
          for (int i = 0, c = argType.Interfaces == null ? 0 : argType.Interfaces.Count; i < c; i++) {
            Interface aintf = argType.Interfaces[i];
            if (aintf == null) return false;
            if (this.InferMethodTemplateArguments(aintf, null, parType, inferredTypeFor)) return true;
          }
          Class cl = argType as Class;
          if (cl != null)
            return this.InferMethodTemplateArguments(cl.BaseClass, null, parType, inferredTypeFor);
        }
      }
      if (argType == parType) return true;
      if (argType.DeclaringType != null && parType.DeclaringType != null && this.InferMethodTemplateArguments(argType.DeclaringType, null, parType.DeclaringType, inferredTypeFor))
        return argType.Name != null && parType.Name != null && argType.Name.UniqueIdKey == parType.Name.UniqueIdKey;
      if (argExpr != null && argType == SystemTypes.Delegate) {
        DelegateNode parDelegate = parType as DelegateNode;
        Method meth = argExpr.BoundMember as Method;
        if (meth != null && parDelegate != null) {
          // match up parameters and results
          int numArgs1 = meth.Parameters == null        ? 0 : meth.Parameters.Count;
          int numArgs2 = parDelegate.Parameters == null ? 0 : parDelegate.Parameters.Count;
          if (numArgs1 == numArgs2) {
            for (int j = 0; j < numArgs1; j++) {
              if (!InferMethodTemplateArguments(meth.Parameters[j].Type, null, parDelegate.Parameters[j].Type, inferredTypeFor)) return false;
            }
            // do result type
            return InferMethodTemplateArguments(meth.ReturnType, null, parDelegate.ReturnType, inferredTypeFor);
          }
        }
      }
      return this.typeSystem.ImplicitCoercionFromTo(argType, parType);
    }
    private void UpdateCallee(MethodCall mCall, MemberBinding mb){
      Expression callee = mCall.Callee;
    tryAgain:
      NameBinding nb = callee as NameBinding;
      if (nb != null)
        nb.BoundMember = mb;
      else{
        QualifiedIdentifier qualId = callee as QualifiedIdentifier;
        if (qualId != null)
          qualId.BoundMember = mb;
        else{
          TemplateInstance templInst = callee as TemplateInstance;
          if (templInst != null){
            callee = templInst.Expression;
            goto tryAgain;
          }
        }
      }
      mCall.Callee = mb;
    }
    public override Module VisitModule(Module module){
      if (module == null) return null;
      this.currentModule = module;
      this.currentAssembly = module.ContainingAssembly;
      return base.VisitModule(module);
    }
    public virtual Expression GetContextBinding(NameBinding nameBinding){
      if (nameBinding == null) return null;
      bool savedCR = this.hasContextReference;
      QueryContext qc = new QueryContext();
      qc.SourceContext = nameBinding.SourceContext;
      for (ContextScope scope = this.contextScope; scope != null; scope = scope.Previous){
        qc.Type = scope.Type;
        qc.Scope = scope;
        QualifiedIdentifier qi = new QualifiedIdentifier(qc, nameBinding.Identifier);
        Expression result = this.VisitQualifiedIdentifier(qi);
        if (result != null && result != qi){
          result.SourceContext = nameBinding.SourceContext;
          return result;
        }
      }
      this.hasContextReference = savedCR;
      return null;
    }
    public virtual Expression VisitNameBindingCore(NameBinding nameBinding){
      if (nameBinding == null) return null;
      MemberBinding mb = this.GetMemberBinding(nameBinding);
      Expression cb = this.GetContextBinding(nameBinding);
      if (mb == null){
        if (cb != null){
          nameBinding.BoundMember = cb;
          return cb;
        }
        return null;
      }
      bool isQueryBinding = (mb.BoundMember != null && mb.BoundMember.DeclaringType is QueryScope);
      if (cb != null){
        if (isQueryBinding) return cb;
        this.HandleError(nameBinding, Error.QueryAmbiguousContextName, nameBinding.Identifier.Name);
      }
      if (isQueryBinding) return nameBinding;
      return mb;
    }
    public override Expression VisitNameBinding(NameBinding nameBinding){
      if (nameBinding == null) return null;
      Expression result = this.VisitNameBindingCore(nameBinding);
      if (result == null) return nameBinding;
      MemberBinding mb = result as MemberBinding;
      if (mb == null) return result;
      Property p = mb.BoundMember as Property;
      if (p != null){
        Method getter = p.Getter;
        if (getter == null) return mb;
        mb.BoundMember = getter;
        MethodCall mc = new MethodCall(mb, new ExpressionList());
        if (!(mb.TargetObject is Base) && getter.IsVirtualAndNotDeclaredInStruct) 
          mc.NodeType = NodeType.Callvirt;
        mc.SourceContext = nameBinding.SourceContext;
        mc.Type = p.Type;
        return mc;
      }
      Field f = mb.BoundMember as Field;
      if (f != null){
        if (f != null && f.IsLiteral){
          Literal defaultValue = f.DefaultValue;
          if (defaultValue == null) defaultValue = f.DefaultValue = this.EvaluateAsLiteral(f.Initializer);
          if (defaultValue != null){
            Literal lit = (Literal)defaultValue.Clone();
            lit.SourceContext = nameBinding.SourceContext;
            lit = this.CoerceLiteral(mb, f, lit);
            if (lit != null) return lit;
          }
        }
      }
      return mb;
    }
    public override Expression VisitOldExpression(OldExpression old){
      if (old == null) return null;
      old.expression = this.VisitExpression(old.expression);
      if (old.expression == null) return null;
      old.Type = old.expression.Type;
      return old;
    }
    public override Expression VisitReturnValue(ReturnValue returnValue)
    {
      returnValue.Type = this.currentMethod.ReturnType;
      return returnValue;
    }
    public virtual void VisitTemplateInstanceTypes(TypeNode t){
      if (t == null || (t.IsGeneric && this.useGenerics)) return;
      TypeNodeList templateInstances = t.TemplateInstances;
      for (int i = 0, n = templateInstances == null ? 0 : templateInstances.Count; i < n; i++)
        this.Visit(templateInstances[i]);
    }
    public override Expression VisitPrefixExpression(PrefixExpression pExpr){
      if (pExpr == null) return null;
      Expression e = pExpr.Expression = this.VisitTargetExpression(pExpr.Expression);
      if (e == null) return null;
      pExpr.Type = this.typeSystem.Unwrap(e.Type);
      pExpr.OperatorOverload = this.GetPrefixOrPostfixOperatorOverload(e, pExpr.Type, pExpr.Operator);
      return pExpr;
    }
    public virtual Method GetPrefixOrPostfixOperatorOverload(Expression e, TypeNode t, NodeType oper){
      if (e == null || t == null) return null;
      Identifier operatorId = this.GetUnaryOperatorOverloadName(oper);
      if (operatorId != null){
        MemberList operatorOverloads = this.GetOperatorOverloadsNamed(operatorId, t);
        for (int i = 0, n = operatorOverloads == null ? 0 : operatorOverloads.Count; i < n; i++){
          Method operatorMethod = operatorOverloads[i] as Method;
          if (operatorMethod == null || (operatorMethod.Flags & MethodFlags.SpecialName) == 0) continue;
          return operatorMethod;
        }
      }
      return null;
    }
    public override Expression VisitPostfixExpression(PostfixExpression pExpr){
      if (pExpr == null) return null;
      Expression e = pExpr.Expression = this.VisitTargetExpression(pExpr.Expression);
      if (e == null) return null;
      pExpr.Type = this.typeSystem.Unwrap(e.Type);
      Method overload = pExpr.OperatorOverload = this.GetPrefixOrPostfixOperatorOverload(e, pExpr.Type, pExpr.Operator);
      if (overload != null)
        pExpr.Type = overload.ReturnType;
      return pExpr;
    }
    public virtual Expression VisitQualifiedIdentifierCore(QualifiedIdentifier qualifiedIdentifier){
      MemberBinding result = this.GetMemberBinding(qualifiedIdentifier);
      return result;
    }
    public override Expression VisitQuantifier(Quantifier quantifier) {
      if (quantifier == null) return null;
      quantifier.Comprehension = (Comprehension)this.VisitComprehension(quantifier.Comprehension);
      switch (quantifier.QuantifierType){
        case NodeType.Forall:
        case NodeType.Exists:
        case NodeType.ExistsUnique:
          // Quantifiers that are of type bool --> bool
          quantifier.Type = SystemTypes.Boolean;
          quantifier.SourceType = SystemTypes.Boolean;
          break;
        case NodeType.Count:
          // Quantifiers that are of type bool --> int
          quantifier.Type = SystemTypes.Int32;
          quantifier.SourceType = SystemTypes.Boolean;
          break;
        case NodeType.Max:
        case NodeType.Min:
        case NodeType.Product:
        case NodeType.Sum:
          // Quantifiers that are of type int --> int
          quantifier.Type = SystemTypes.Int32;
          quantifier.SourceType = SystemTypes.Int32;
          break;
        default:
          quantifier.Type = null; // REVIEW: What would be a good default?
          quantifier.SourceType = null;
          break;
      }
      return quantifier;
    }
     public override Expression VisitComprehension(Comprehension comprehension){
      if (comprehension == null) return null;
      if (comprehension.Elements == null) return null;
      if (comprehension.IsDisplay){
        if (comprehension.Elements.Count == 0) {
          comprehension.Type = SystemTypes.GenericIEnumerable.GetTemplateInstance(this.currentType,SystemTypes.Object);
          return comprehension;
        }
      }else{
        if (comprehension.Elements.Count != 1 && comprehension.Elements.Count != 2)
          return null;
      }
      comprehension.BindingsAndFilters = this.VisitExpressionList(comprehension.BindingsAndFilters);
      comprehension.Elements = this.VisitExpressionList(comprehension.Elements);
      TypeNode unifiedType = null;
      TypeNodeList tl = new TypeNodeList();
      for (int i = 0, n = comprehension.Elements == null ? 0 : comprehension.Elements.Count; i < n; i++){
        Expression e = comprehension.Elements[i];
        if (e == null || e.Type == null) continue;
        Literal lit = e as Literal;
        if (lit != null && lit.Value == null) continue; //This prevents null from participating in the type unification, which is by design.
        if (e.Type == null) continue; //e is a bad expression
        tl.Add(e.Type);
      }
      unifiedType = this.typeSystem.UnifiedType(tl, this.TypeViewer);
      if (unifiedType == null) unifiedType = SystemTypes.Object;
      comprehension.Type = OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.GenericIEnumerable.GetTemplateInstance(this.currentType, unifiedType));
      return comprehension;
    }
    public override ComprehensionBinding VisitComprehensionBinding(ComprehensionBinding comprehensionBinding){
      comprehensionBinding.TargetVariableType = this.VisitTypeReference(comprehensionBinding.TargetVariableType);
      comprehensionBinding.TargetVariable = this.VisitTargetExpression(comprehensionBinding.TargetVariable);
      comprehensionBinding.AsTargetVariableType = this.VisitTypeReference(comprehensionBinding.AsTargetVariableType);
      Expression e = comprehensionBinding.SourceEnumerable = this.VisitExpression(comprehensionBinding.SourceEnumerable);
      //comprehensionBinding.Filter = this.VisitExpression(comprehensionBinding.Filter);

      if (e != null && e.NodeType == NodeType.AnonymousNestedFunction){
        AnonymousNestedFunction func = (AnonymousNestedFunction)e;
        Method invoker = func.Method;
        if (invoker != null){
          Expression ob = invoker.IsStatic ? null : new CurrentClosure(invoker, invoker.DeclaringType);
          e = new MethodCall(new MemberBinding(ob, invoker), null, NodeType.Call, invoker.ReturnType, e.SourceContext);
          func.Invocation = e;
          func.Type = invoker.ReturnType;
        }
      }
      if (comprehensionBinding.TargetVariableType == null){
        MemberBinding mb = comprehensionBinding.TargetVariable as MemberBinding;
        if (mb != null){
          Field f = mb.BoundMember as Field;
          if (f != null && e != null){
            TypeNode st = e.Type; //HACK
            while (st is TypeAlias) st = ((TypeAlias)st).AliasedType;
            if (st != e.Type)
              f.Type = this.typeSystem.GetStreamElementType(st, this.TypeViewer);
            else
              f.Type = this.typeSystem.GetStreamElementType(e, this.TypeViewer);
            mb.Type = f.Type;
            comprehensionBinding.TargetVariableType = f.Type;
          }
        }
      }
      return comprehensionBinding;
    }
    public override Expression VisitQualifiedIdentifier(QualifiedIdentifier qualifiedIdentifier){
      if (qualifiedIdentifier == null) return null;
      Expression result = this.VisitQualifiedIdentifierCore(qualifiedIdentifier);
      if (result == null){
        qualifiedIdentifier.Type = SystemTypes.Object;
        return qualifiedIdentifier;
      }
      MemberBinding mb = result as MemberBinding;
      if (mb == null) return result;
      switch(mb.BoundMember.NodeType){
        case NodeType.Property:
          Property p = (Property)mb.BoundMember;
          Method getter = p.Getter;
          if (getter == null) getter = p.GetBaseGetter();
          if (getter == null)
            return mb;
          if (getter.ObsoleteAttribute == null) getter.ObsoleteAttribute = p.ObsoleteAttribute;
          mb.BoundMember = getter;
          MethodCall mc = new MethodCall(mb, new ExpressionList());
          if (!(mb.TargetObject is Base) && getter.IsVirtualAndNotDeclaredInStruct) 
            mc.NodeType = NodeType.Callvirt;
          mc.SourceContext = qualifiedIdentifier.SourceContext;
          mc.Type = p.Type;
          return mc;
        case NodeType.Field:
          Field f = (Field)mb.BoundMember;
          if (f.IsLiteral){
            Literal defaultValue = f.DefaultValue;
            if (defaultValue == null) defaultValue = f.DefaultValue = this.EvaluateAsLiteral(f.Initializer);
            if (defaultValue != null) {
              Literal lit = (Literal)defaultValue.Clone();
              lit.SourceContext = mb.SourceContext;
              lit = this.CoerceLiteral(mb, f, lit);
              if (lit != null) return lit;
            }
            if (f.DeclaringType is EnumNode)
              mb.TargetObject = null;
          }
          break;
          //TODO: what about types?
      }
      return mb;
    }
    public virtual Expression VisitAddEventHandler(BinaryExpression expr){
      Event e = null;
      Expression ob = null;
      QualifiedIdentifier qualId = expr.Operand1 as QualifiedIdentifier;
      if (qualId != null){
        ob = this.VisitExpression(qualId.Qualifier);
        TypeNode t = ob == null ? null : TypeNode.StripModifiers(ob.Type);
        if (ob is Literal && t == SystemTypes.Type){
          t = ((Literal)ob).Value as TypeNode;
          ob = null;
        }
        while (t != null){
          MemberList mems = this.GetTypeView(t).GetMembersNamed(qualId.Identifier);
          for (int i = 0, n = mems.Count; i < n; i++)
            if ((e = mems[i] as Event) != null) goto done;
          t = t.BaseType;
        }
      }else{
        NameBinding nb = expr.Operand1 as NameBinding;
        if (nb != null){
          ob = this.GetObject(nb);
          MemberList mems = nb.BoundMembers;
          for (int i = 0, n = mems.Count; i < n; i++)
            if ((e = mems[i] as Event) != null) goto done;
        }
      }      
    done:
      if (e != null && e.HandlerAdder != null){
        ExpressionList arguments = new ExpressionList(1);
        arguments.Add(this.VisitExpression(expr.Operand2));
        e.HandlerAdder.ObsoleteAttribute = e.ObsoleteAttribute;
        if (e.HandlerAdder.IsStatic) ob = null;
        MethodCall mc = new MethodCall(new MemberBinding(ob, e.HandlerAdder), arguments, NodeType.Call);
        if (!(ob is Base) && e.HandlerAdder.IsVirtualAndNotDeclaredInStruct) 
          mc.NodeType = NodeType.Callvirt;
        mc.Type = SystemTypes.Void;
        mc.SourceContext = expr.SourceContext;
        return mc;
      }
      return base.VisitBinaryExpression(expr);
    }
    public virtual Expression VisitRemoveEventHandler(BinaryExpression expr){
      Event e = null;
      Expression ob = null;
      QualifiedIdentifier qualId = expr.Operand1 as QualifiedIdentifier;
      if (qualId != null){
        ob = this.VisitExpression(qualId.Qualifier);
        TypeNode t = ob == null ? null : TypeNode.StripModifiers(ob.Type);
        if (ob is Literal && t == SystemTypes.Type){
          t = ((Literal)ob).Value as TypeNode;
          ob = null;
        }
        while (t != null){
          MemberList mems = this.GetTypeView(t).GetMembersNamed(qualId.Identifier);
          for (int i = 0, n = mems.Count; i < n; i++)
            if ((e = mems[i] as Event) != null) goto done;
          t = t.BaseType;
        }
      }else{
        NameBinding nb = expr.Operand1 as NameBinding;
        if (nb != null){
          ob = this.GetObject(nb);
          MemberList mems = nb.BoundMembers;
          for (int i = 0, n = mems.Count; i < n; i++)
            if ((e = mems[i] as Event) != null) goto done;
        }
      }
    done:
      if (e != null && e.HandlerAdder != null){
        ExpressionList arguments = new ExpressionList(1);
        arguments.Add(this.VisitExpression(expr.Operand2));
        e.HandlerRemover.ObsoleteAttribute = e.ObsoleteAttribute;
        if (e.HandlerRemover.IsStatic) ob = null;
        MethodCall mc = new MethodCall(new MemberBinding(ob, e.HandlerRemover), arguments, NodeType.Call);
        if (!(ob is Base) && e.HandlerRemover.IsVirtualAndNotDeclaredInStruct) 
          mc.NodeType = NodeType.Callvirt;
        mc.Type = SystemTypes.Void;
        mc.SourceContext = expr.SourceContext;
        return mc;
      }
      return base.VisitBinaryExpression(expr);
    }
    public override Statement VisitResourceUse(ResourceUse resourceUse){
      if (resourceUse == null) return null;
      LocalDeclarationsStatement locDecls = resourceUse.ResourceAcquisition as LocalDeclarationsStatement;
      LocalDeclarationList locDecList = locDecls == null ? null : locDecls.Declarations;
      int n = locDecList == null ? 0 : locDecList.Count;
      resourceUse.ResourceAcquisition = (Statement)this.Visit(resourceUse.ResourceAcquisition);
      resourceUse.Body = this.VisitBlock(resourceUse.Body);
      return resourceUse;
    }       
    public override Statement VisitReturn(Return Return){
      if (Return == null) return null;
      TypeNode rType = this.currentMethod == null ? null : TypeNode.StripModifiers(this.currentMethod.ReturnType);
      if (rType is DelegateNode) {
        TemplateInstance ti = Return.Expression as TemplateInstance;
        if (ti != null && !ti.IsMethodTemplate) {
          ti.IsMethodTemplate = true;
          Return.Expression = this.VisitExpression(new Construct(new MemberBinding(null, rType), new ExpressionList(Return.Expression)));
        } else {
          AnonymousNestedFunction anonFunc = Return.Expression as AnonymousNestedFunction;
          if (anonFunc != null) this.FillInImplicitType(anonFunc, (DelegateNode)rType);
          Return.Expression = this.VisitExpression(Return.Expression);
          MemberBinding mb = Return.Expression as MemberBinding;
          if (mb != null && mb.BoundMember is Method)
            Return.Expression = this.VisitExpression(new Construct(new MemberBinding(null, rType), new ExpressionList(Return.Expression)));
        }
        return Return;
      } 
      Expression e = Return.Expression = this.VisitExpression(Return.Expression);
      if (e == null) return Return;
      AnonymousNestedFunction func = e as AnonymousNestedFunction;
      if (func != null){
        Method invoker = func.Method;
        if (invoker != null && !(rType is DelegateNode)){
          Expression ob = invoker.IsStatic ? null : new CurrentClosure(invoker, invoker.DeclaringType);
          func.Invocation = new MethodCall(new MemberBinding(ob, invoker), null, NodeType.Call, invoker.ReturnType, e.SourceContext);
        }
      }
      if (e.Type != null) {
        if (rType == null)
          this.currentMethod.ReturnType = new TypeUnionExpression(new TypeNodeList(e.Type));
        else if (rType.NodeType == NodeType.TypeUnionExpression)
          ((TypeUnionExpression)rType).Types.Add(e.Type);
      }
      Construct c = e as Construct;
      if (c != null && c.Type == null)
        c.Type = rType;
      Identifier id = e as Identifier;
      if (id != null && id.Type == null && rType != null && rType.IsAssignableTo(SystemTypes.Enum))
        id.Type = rType;
      return Return;
    }
    public override Expression VisitSetterValue(SetterValue value){
      if (value == null) return null;
      if (this.currentMethod != null && this.currentMethod.Parameters != null){
        int n = this.currentMethod.Parameters.Count;
        if (n > 0 && this.currentMethod.Parameters[n-1] != null)
          value.Type = this.currentMethod.Parameters[n-1].Type;
      }
      return value;
    }
    public override Expression VisitStackAlloc(StackAlloc alloc){
      if (alloc == null) return null;
      if (alloc.ElementType == null) return null;
      if (!alloc.ElementType.IsUnmanaged){
        this.HandleError(alloc.ElementTypeExpression, Error.ManagedAddr, this.GetTypeName(alloc.ElementType));
        return null;
      }
      alloc.Type = alloc.ElementType.GetPointerType();
      Expression numElements = this.VisitExpression(alloc.NumberOfElements);
      if (numElements == null) return null;
      numElements = this.typeSystem.CoerceToIndex(numElements, this.TypeViewer);
      if (numElements == null) return null;
      Expression elemSize = this.VisitUnaryExpression(new UnaryExpression(new Literal(alloc.ElementType, SystemTypes.Type), NodeType.Sizeof));
      if (numElements.Type != SystemTypes.Int32) elemSize = this.typeSystem.ExplicitCoercion(elemSize, numElements.Type, this.TypeViewer);
      BinaryExpression index = new BinaryExpression(numElements, elemSize, NodeType.Mul, numElements.Type, numElements.SourceContext);
      alloc.NumberOfElements = index;
      if (numElements is Literal && elemSize is Literal){ 
        alloc.NumberOfElements = this.VisitBinaryExpression(index); //Constant folding 
        Literal lit = alloc.NumberOfElements as Literal;
        if (lit != null && this.typeSystem.ImplicitCoercionFromTo(lit, lit.Type, SystemTypes.Int64, this.TypeViewer) && ((IConvertible)lit.Value).ToInt64(null) < 0){
          this.HandleError(lit, Error.NegativeStackAllocSize);
          return null;
        }
      }
      return alloc;
    }
    public override Expression VisitTargetExpression(Expression target){
      if (target == null) return null;
      Expression result = null;
      switch(target.NodeType){
        case NodeType.Indexer:
          Indexer indexer = (Indexer)target;
          result = this.VisitIndexerCore(indexer);
          if (result == null) return indexer;
          if (!(result is Indexer)) return result;
          indexer = (Indexer) result;
          target = VisitTargetIndexerResolve(indexer);
          break;
        case NodeType.NameBinding:
          result = this.VisitNameBindingCore((NameBinding)target);
          if (result != null) target = result;
          break;
        case NodeType.QualifiedIdentifer:
          result = this.VisitQualifiedIdentifierCore((QualifiedIdentifier)target);
          if (result != null) target = result;
          break;
        case NodeType.ConstructTuple:
          ConstructTuple ctup = (ConstructTuple)target;
          FieldList fields = ctup.Fields;
          for (int i = 0, n = fields == null ? 0 : fields.Count; i < n; i++){
            Field f = fields[i];
            if (f == null) continue;
            Expression e = f.Initializer = this.VisitTargetExpression(f.Initializer);
            if (e != null) f.Type = e.Type;
          }
          ctup.Type = TupleType.For(fields, this.currentType);
          break;
        default: 
          target = this.VisitExpression(target);
          break;
      }
      return target;
    }

    public virtual Expression VisitTargetIndexerResolve(Indexer indexer) {
      Property defaultIndexedProperty = null;
      ExpressionList indexerOperands = indexer.Operands;
      if (indexer.Object != null){
        TypeNode obType = this.typeSystem.Unwrap(indexer.Object.Type, true);
        ArrayType arrT = obType as ArrayType;
        if (arrT != null){
          int rank = arrT.Rank;
          int n = indexerOperands == null ? 0 : indexer.Operands.Count;
          if (rank != n){
            this.HandleError(indexer, Error.WrongNumberOfIndices, rank.ToString());
            return null;
          }
          indexer.Type = arrT.ElementType;
        }else{
          if (obType.IsPointerType){
            return ResolveIndexedPointer(indexer, obType);
          }
          Interface iface = obType as Interface;
          if (iface != null) {
            defaultIndexedProperty = this.ResolveOverload(((Interface)this.GetTypeView(iface)).GetAllDefaultMembers(), indexer.Operands, indexer.ArgumentListIsIncomplete) as Property;
            if (defaultIndexedProperty != null) {
              indexer.CorrespondingDefaultIndexedProperty = defaultIndexedProperty;
              indexer.Type = defaultIndexedProperty.Type;
            }
          } else {
            while (obType != null) {
              defaultIndexedProperty = this.ResolveOverload(this.GetTypeView(obType).DefaultMembers, indexerOperands) as Property;
              if (defaultIndexedProperty != null) {
                indexer.CorrespondingDefaultIndexedProperty = defaultIndexedProperty;
                indexer.Type = defaultIndexedProperty.Type;
                break;
              }
              obType = obType.BaseType;
            }
          }
        }      
      }
      return indexer;
    }
    public override Expression VisitTernaryExpression(TernaryExpression expression){
      if (expression == null) return null;
      expression.Operand1 = this.VisitExpression(expression.Operand1);
      expression.Operand2 = this.VisitExpression(expression.Operand2);
      expression.Operand3 = this.VisitExpression(expression.Operand3);
      TypeNode t1 = expression.Operand1 != null ? expression.Operand1.Type : SystemTypes.Object;
      TypeNode t2 = expression.Operand2 != null ? expression.Operand2.Type : SystemTypes.Object;
      TypeNode t3 = expression.Operand3 != null ? expression.Operand3.Type : SystemTypes.Object;
      if (t1 == null) t1 = SystemTypes.Object;
      if (t2 == null) t2 = SystemTypes.Object;
      if (t3 == null) t3 = SystemTypes.Object;
      expression.Type = this.InferTypeOfTernaryExpression(t1, t2, t3, expression);
      if (expression.Operand1 == null || expression.Operand2 == null || expression.Operand3 == null) expression.IsErroneous = true;
      if (expression.NodeType == NodeType.Conditional) {
        Literal lit1 = expression.Operand1 as Literal;
        Literal lit2 = expression.Operand2 as Literal;
        Literal lit3 = expression.Operand3 as Literal;
        if (lit1 != null && lit1.Value is bool && lit2 != null && lit3 != null) {
          return ((bool)lit1.Value) ? lit2 : lit3;
        }
      }
      return expression;
    }
    public override Expression VisitThis(This This){
      if (This == null) return null;
      TypeNode currentType = this.currentTypeInstance;
      if (currentType == null)
        This.Type = SystemTypes.Object;
      else{
        if (currentType.IsValueType)
          This.Type = currentType.GetReferenceType();
        else
          This.Type = currentType;
      }
      return This;
    }
    static AttributeList CreateAttributeList(params TypeNode[] attributeTypes){
      return CreateAttributeList(attributeTypes, AttributeTargets.Method);
    }
    static AttributeList CreateAttributeList(TypeNode[] attributeTypes, AttributeTargets target){
      AttributeList list = new AttributeList(1);
      foreach (TypeNode t in attributeTypes){
        if (t != null){
          InstanceInitializer ctor = t.GetConstructor();
          if (ctor != null){
            list.Add(new AttributeNode(new MemberBinding(null, ctor), null, target));
          }
        }
      }
      return list;
    }
    public virtual TypeNode GetDummyInstance(TypeNode t){
      if (t == null){Debug.Assert(false); return null;}
      if (this.currentTypeInstance != null){
        TypeNode nt = this.GetTypeView(this.currentTypeInstance).GetNestedType(t.Name);
        if (nt == null){Debug.Assert(false); return null;}
        if (nt.TemplateParameters == null || nt.TemplateParameters.Count == 0) return nt;
        t = nt;
      }
      return t.GetTemplateInstance(this.currentModule, null, t.DeclaringType, t.TemplateParameters);
    }
    public override TypeNode VisitTypeNode(TypeNode typeNode){
      if (typeNode == null) return null;
      if (typeNode == this.currentTypeInstance) return typeNode;
      if (typeNode.Template == this.currentType && typeNode.IsNotFullySpecialized) return typeNode;
      TypeNode savedCurrentType = this.currentType;
      TypeNode savedCurrentTypeInstance = this.currentTypeInstance;
      this.currentType = typeNode;
      if (typeNode.PartiallyDefines != null) 
        this.currentType = typeNode.PartiallyDefines;
      if (this.currentType.Template == null && this.currentType.ConsolidatedTemplateParameters != null && this.currentType.ConsolidatedTemplateParameters.Count > 0)
        this.currentTypeInstance = this.GetDummyInstance(this.currentType);
      else
        this.currentTypeInstance = this.currentType;

      TypeNode result = typeNode;
      if (typeNode.IsNormalized) {
        this.VisitMemberList(typeNode.Members);
      } else {
        if (typeNode.Contract != null && typeNode.Contract.FramePropertyGetter != null) {
          Resolver.ImplementInitFrameSetsMethod(typeNode);
        }
        result = base.VisitTypeNode(typeNode);
        this.VisitTemplateInstanceTypes(typeNode);
      }
      this.currentType = savedCurrentType;
      this.currentTypeInstance = savedCurrentTypeInstance;
      return result;
    }
    static void ImplementInitFrameSetsMethod(TypeNode outerType){
      // private void InitFrameSets(){
      //   #if some base type T implements a Frame getter
      //     this.frame.AddRepFrame(this.T::get_Frame());
      //   #endif
      //   #foreach non-static field f declared in this type
      //     #if f is a rep field
      //     #if f.Type is a possibly-null reference type
      //       if (this.f != null)
      //         this.frame.AddRepObject(this.f);
      //     #elseif f.Type is a non-null reference type
      //       this.frame.AddRepObject(this.f);
      //     #elseif f.Type is a value type that implements IEnumerable
      //       foreach (object o in this.f)
      //         this.frame.AddRepObject(o);
      //     #endif
      //     #elseif f carries a SharedAttribute
      //     #if f.Type is a possibly-null reference type
      //       if (this.f != null)
      //         this.frame.AddSharedObject(this.f);
      //     #elseif f.Type is a non-null reference type
      //       this.frame.AddSharedObject(this.f);
      //     #elseif f.Type is a value type that implements IEnumerable
      //       foreach (object o in this.f)
      //         this.frame.AddSharedObject(o);
      //     #endif
      //     #endif
      //   #endforeach
      // }
      
      // Possible extension:
      // We might choose to support fields f annotated with an OwnedAttribute(Depth=n)
      // indicating that the references contained in f up to depth n are rep references of this.
      // references contained in f at depth 0 = references contained in f
      // references contained in f at depth n + 1 = the union of, for each reference r contained in f at depth n,
      // the set of rep references of object r.

      Method m = outerType.Contract.InitFrameSetsMethod;
      Method frameGetter = outerType.Contract.FramePropertyGetter;
      Expression frame = new MethodCall(new MemberBinding(m.ThisParameter, frameGetter), null, NodeType.Call, frameGetter.ReturnType);

      StatementList statements = new StatementList();

      if (outerType.BaseType != SystemTypes.Object){
        statements.Add(
          new ExpressionStatement(new MethodCall(new MemberBinding(frame, SystemTypes.Guard.GetMethod(Identifier.For("AddRepFrame"), SystemTypes.Object, SystemTypes.Type)),
          new ExpressionList(m.ThisParameter, new UnaryExpression(new Literal(outerType.BaseType, SystemTypes.Type), NodeType.Typeof, SystemTypes.Type)))));
      }

      Method addRepObjectMethod = SystemTypes.Guard.GetMethod(Identifier.For("AddRepObject"), SystemTypes.Object);
      Method addLockProtectedObjectMethod = SystemTypes.Guard.GetMethod(Identifier.For("AddObjectLockProtectedCertificate"), SystemTypes.Object);
      Method addImmutableObjectMethod = SystemTypes.Guard.GetMethod(Identifier.For("AddObjectImmutableCertificate"), SystemTypes.Object);

      MemberList members = outerType.Members;
      for (int i = 0; i < members.Count; i++){
        Member member = members[i];
        Field field = member as Field;
        if (field != null && !field.IsStatic){
          Method addMethod;
          switch (field.ReferenceSemantics & ReferenceFieldSemantics.SemanticsMask){
            case ReferenceFieldSemantics.Rep: addMethod = addRepObjectMethod; break;
            case ReferenceFieldSemantics.LockProtected: addMethod = addLockProtectedObjectMethod; break;
            case ReferenceFieldSemantics.Immutable: addMethod = addImmutableObjectMethod; break;
            default: addMethod = null; break;
          }
          if (addMethod != null){
            MemberBinding addMethodBinding = new MemberBinding(frame, addMethod);
            MemberBinding fieldBinding = new MemberBinding(m.ThisParameter, field);
            TypeNode fieldType = field.Type == null ? SystemTypes.Object : field.Type;
            if (fieldType.NodeType == NodeType.NonNullTypeExpression){
              statements.Add(new ExpressionStatement(new MethodCall(addMethodBinding, new ExpressionList(fieldBinding))));
            } else if (fieldType.IsValueType){
              // TODO: Check that this.GetTypeView(field.Type).IsAssignableTo(SystemTypes.IEnumerable); otherwise, generate a compiler error.
              Local repRef = new Local(SystemTypes.Object);
              ForEach forEach = new ForEach(SystemTypes.Object, repRef, fieldBinding,
                new Block(new StatementList(
                new ExpressionStatement(new MethodCall(addMethodBinding,
                new ExpressionList(repRef))))));
              forEach.StatementTerminatesNormallyIfEnumerableIsNull = false;
              forEach.StatementTerminatesNormallyIfEnumerableIsNull = false;
              forEach.ScopeForTemporaryVariables = new BlockScope();
              statements.Add(forEach);
            } else {
              statements.Add(new ExpressionStatement(new MethodCall(addMethodBinding, new ExpressionList(fieldBinding))));
            }
          }
        }
      }

      m.CallingConvention = CallingConventionFlags.HasThis;
      m.Flags = MethodFlags.Private;
      m.Body = new Block(statements);
      outerType.Members.Add(m);
    }
    //TODO: emit internal IGuardedObject interface per assembly for classes that don't want to support inter-assembly inheritance with guarded classes checks
    //static void ImplementGetMostDerivedFrameMethod(TypeNode outerType){
    //  // IFrame IGuardedObject.get_MostDerivedFrameProperty(){
    //  //   return frame;
    //  // }
    //  if (Runtime.IGuardedObject_ObjectGuard != null && Runtime.IGuardedObject_ObjectGuard.Getter != null){
    //    if (outerType.Contract.FrameField != null){
    //      Method m = Runtime.IGuardedObject_ObjectGuard.Getter.CreateExplicitImplementation(outerType, null, new StatementList(
    //        new Return(new MemberBinding(new This(), outerType.Contract.FrameField))));
    //      if (m != null){
    //        m.CciKind = CciMemberKind.Auxiliary;
    //        m.Attributes = CreateAttributeList(SystemTypes.NoDefaultActivityAttribute,SystemTypes.NoDefaultContractAttribute,SystemTypes.PureAttribute);
    //        m.Flags |= MethodFlags.SpecialName;
    //        outerType.Members.Add(m);
    //        Property p = new Property(outerType, null, PropertyFlags.None, Identifier.For("ObjectGuard"), m, null);
    //        outerType.Members.Add(p);
    //      }
    //    }
    //  }
    //}
    public override Expression VisitUnaryExpression(UnaryExpression unaryExpression){
      if (unaryExpression == null) return null;
      switch (unaryExpression.NodeType){
        case NodeType.OutAddress:
        case NodeType.RefAddress:
          unaryExpression.Operand = this.VisitTargetExpression(unaryExpression.Operand);
          break;
        default:
          unaryExpression.Operand = this.VisitExpression(unaryExpression.Operand);
          break;
      }
      TypeNode t = unaryExpression.Operand != null ? unaryExpression.Operand.Type : SystemTypes.Object;
      Reference r = t as Reference;
      if (r != null) t = r.ElementType;
      if (t == null) t = SystemTypes.Object;
      Identifier operatorId = this.GetUnaryOperatorOverloadName(unaryExpression.NodeType);
      if (operatorId != null && (t != SystemTypes.Decimal || !(unaryExpression.Operand is Literal))){
        MemberList operatorOverloads = this.GetOperatorOverloadsNamed(operatorId, t);
        for (int i = 0, n = operatorOverloads == null ? 0 : operatorOverloads.Count; i < n; i++){
          Method operatorMethod = operatorOverloads[i] as Method;
          if (operatorMethod == null || (operatorMethod.Flags & MethodFlags.SpecialName) == 0) continue;
          MemberBinding callee = new MemberBinding(null, operatorMethod);
          ExpressionList arguments = new ExpressionList(1);
          arguments.Add(unaryExpression.Operand);
          MethodCall call = new MethodCall(callee, arguments);
          call.SourceContext = unaryExpression.SourceContext;
          call.Type = operatorMethod.ReturnType;
          return call;
        }
      }
      if (unaryExpression.NodeType == NodeType.Neg && unaryExpression.Operand is Literal){
        //special case treatment for -(int.MaxValue+1) and -(long.MaxValue+1)
        //Do this here so that type inference matches common expectations
        Object litVal = ((Literal)unaryExpression.Operand).Value;
        Literal lit = null;
        unchecked{
          if (litVal is uint){
            if (((uint)litVal) == (uint)int.MinValue) lit = new Literal(int.MinValue, SystemTypes.Int32);
          }else if (litVal is ulong){
            if (((ulong)litVal) == (ulong)long.MinValue) lit = new Literal(long.MinValue, SystemTypes.Int64);
          }
        }
        if (lit != null){
          lit.SourceContext = unaryExpression.SourceContext;
          return lit;
        }
      }
      unaryExpression.Type = this.InferTypeOfUnaryExpression(t, unaryExpression);
      Literal litop = PureEvaluator.TryEvalUnaryExpression(this.EvaluateAsLiteral(unaryExpression.Operand), unaryExpression);
      if (litop != null) {
        litop.Type = unaryExpression.Type;
        litop.SourceExpression = unaryExpression;
        return litop;
      }
      return unaryExpression;
    }
    public virtual MemberBinding GetMemberBinding(NameBinding nameBinding){
      Member target = null;
      TypeNode targetType = SystemTypes.Object;
      Expression thisob = null;
      if (nameBinding == null) goto done;
      MemberList members = nameBinding.BoundMembers;
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
        target = members[i];
        switch(target.NodeType){
          case NodeType.Field: 
            Field f = (Field)target;
            thisob = this.GetObject(nameBinding);
            if (f.IsStatic && (thisob is ImplicitThis || (thisob != null && thisob.Type == SystemTypes.Type && thisob is Literal)))
              thisob = null;
            targetType = f.Type;
            if (targetType == null && f is ParameterField)
              targetType = f.Type = ((ParameterField)f).Parameter.Type;
            goto done;
          case NodeType.Property: 
            Property p = (Property)target;
            if (p.ImplementedTypeExpressions != null && p.ImplementedTypeExpressions.Count > 0) {
              target = null; continue;
            }
            Method g = p.Getter;
            Method s = p.Setter;
            if (g != null && g.Parameters != null && g.Parameters.Count != 0) continue;
            if (s != null && (s.Parameters == null || s.Parameters.Count != 1)) continue;
            thisob = this.GetObject(nameBinding);
            if (p.IsStatic && (thisob is ImplicitThis || (thisob != null && thisob.Type == SystemTypes.Type && thisob is Literal)))
              thisob = null;
            targetType = p.Type; 
            goto done;
          case NodeType.Method:
            Method m = (Method)target;
            if (m.ImplementedTypeExpressions != null && m.ImplementedTypeExpressions.Count > 0) {
              target = null; continue;
            }
            thisob = this.GetObject(nameBinding);
            targetType = SystemTypes.Delegate;
            goto done;
          case NodeType.Event:
            //Can get here if there is a forward reference to a field backed event
            targetType = target.DeclaringType;
            if (targetType == null) break;
            target = this.GetTypeView(targetType).GetField(target.Name);
            if (target != null) goto case NodeType.Field;
            target = members[i];
            targetType = ((Event)target).HandlerType;
            if (!target.IsStatic)
              thisob = this.GetObject(nameBinding);
            break;
        }
      }
      if (!(target is Event)) target = null;
    done:
      if (target == null) return null;
      MemberBinding result = new MemberBinding(thisob, target, nameBinding);
      result.Type = targetType;
      result.SourceContext = nameBinding.SourceContext;
      nameBinding.BoundMember = result;
      return result;
    }
    public virtual MemberBinding GetMemberBinding(QualifiedIdentifier qualifiedIdentifier){
      Member target = null;
      TypeNode targetType = SystemTypes.Object;
      Expression thisob = null;
      if (qualifiedIdentifier == null) goto done;
      NameBinding nb = qualifiedIdentifier.Qualifier as NameBinding;
      MemberList members = this.GetMembers(qualifiedIdentifier);
      Event e = null;
      for (int i = 0, n = members.Count; i < n; i++){
        target = members[i];
        switch(target.NodeType){
          case NodeType.Field: 
            Field f = (Field)target;
            thisob = this.GetObject(qualifiedIdentifier);
            if (f.IsStatic && thisob != null){
              if (thisob.Type == SystemTypes.Type && thisob is Literal) 
                thisob = null;
              else if (TypeNode.StripModifiers(thisob.Type) == f.DeclaringType){
                if (nb != null && f.DeclaringType.Name != null && nb.Identifier.UniqueIdKey == f.DeclaringType.Name.UniqueIdKey) {
                  thisob = null;
                }
              }
            }
            targetType = f.Type; 
            goto done;
          case NodeType.Property: 
            Property p = (Property)target;
            if (p.ImplementedTypeExpressions != null && p.ImplementedTypeExpressions.Count > 0) {
              target = null; continue;
            }
            Method g = p.Getter;
            Method s = p.Setter;
            if (g != null && g.Parameters != null && g.Parameters.Count != 0) continue;
            if (s != null && (s.Parameters == null || s.Parameters.Count != 1)) continue;
            thisob = this.GetObject(qualifiedIdentifier);
            if (p.IsStatic && thisob != null && thisob.Type == SystemTypes.Type && thisob is Literal) thisob = null;
            if (p.IsStatic && thisob != null){
              if (thisob.Type == SystemTypes.Type && thisob is Literal) 
                thisob = null;
              else if (TypeNode.StripModifiers(thisob.Type) == p.DeclaringType) {
                if (nb != null && p.DeclaringType.Name != null && nb.Identifier.UniqueIdKey == p.DeclaringType.Name.UniqueIdKey) {
                  thisob = null;
                }
              }
            }
            targetType = p.Type; 
            goto done;
          case NodeType.Method:
            Method m = (Method)target;
            if (m.ImplementedTypeExpressions != null && m.ImplementedTypeExpressions.Count > 0) {
              target = null; continue;
            }
            thisob = this.GetObject(qualifiedIdentifier);
            targetType = SystemTypes.Delegate;
            qualifiedIdentifier.SourceContext = qualifiedIdentifier.Identifier.SourceContext;
            goto done;
          case NodeType.Event:
            // Remember that we found an event, but don't return it yet in case there's
            // also a field backing that event, in which case we want to return the field.
            e = (Event)target;
            break;
        }
      }
      if (e != null){
        if (!e.IsStatic) thisob = this.GetObject(qualifiedIdentifier);
        targetType = e.HandlerType;
        goto done;
      }
      if (target != null){
        thisob = qualifiedIdentifier.Qualifier;
        targetType = SystemTypes.Void;
        qualifiedIdentifier.SourceContext = qualifiedIdentifier.Identifier.SourceContext;
      }
    done:
      if (target == null) return null;
      MemberBinding result = new MemberBinding(thisob, target, qualifiedIdentifier.Identifier);
      result.Type = targetType;
      result.SourceContext = qualifiedIdentifier.SourceContext;
      qualifiedIdentifier.BoundMember = result;
      return result;
    }
    public virtual Expression GetObject(Expression expression){
      switch(expression.NodeType){
        case NodeType.Indexer:
          return ((Indexer)expression).Object;
        case NodeType.TemplateInstance:
          TemplateInstance instance = (TemplateInstance)expression;
          return this.GetObject(instance.Expression);
        case NodeType.MemberBinding:
          return ((MemberBinding)expression).TargetObject;
        case NodeType.NameBinding:
          NameBinding nb = (NameBinding)expression;
          return this.VisitImplicitThis(new ImplicitThis(nb.MostNestedScope, nb.LexLevel));
        case NodeType.QualifiedIdentifer:
          return ((QualifiedIdentifier)expression).Qualifier;
      }
      return this.VisitImplicitThis(new ImplicitThis());
    }
    public virtual MemberList GetMembers(Expression expression){
      MemberList backupReturn = new MemberList(0);
      switch(expression.NodeType){
        case NodeType.Composition:
          Composition c = (Composition)expression;
          return this.GetMembers(c.Expression);
        case NodeType.Indexer:
          Indexer indxr = (Indexer)expression;
          Expression obj = this.VisitExpression(indxr.Object);
          indxr.Operands = this.VisitExpressionList(indxr.Operands);
          if (obj != null){
            TypeNode t = this.typeSystem.Unwrap(obj.Type);
            if (t != null) return this.GetTypeView(t).DefaultMembers;
          }
          break;
        case NodeType.MemberBinding:{
          MemberBinding mb = (MemberBinding)expression;
          mb.TargetObject = this.VisitExpression(mb.TargetObject);
          MemberList members = new MemberList(1);
          if (mb.BoundMember != null && mb.BoundMember.Name != Looker.NotFound)
            members.Add(mb.BoundMember);
          return members;}
        case NodeType.TemplateInstance:{
          TemplateInstance instance = (TemplateInstance)expression;
          MemberList templates = this.GetMembers(instance.Expression);
          MemberList instances = new MemberList();
          TypeNodeList tArgs = this.VisitTypeReferenceList(instance.TypeArguments);
          int numtArgs = tArgs == null ? 0 : tArgs.Count;
          for (int i = 0, n = templates == null ? 0 : templates.Count; i < n; i++) {
            Method meth = templates[i] as Method;
            if (meth == null) continue;
            if (meth.TemplateParameters == null || meth.TemplateParameters.Count != numtArgs) continue;
            instances.Add(meth.GetTemplateInstance(this.currentType, tArgs));
          }
          if (instances.Count != 0) return instance.BoundMembers = instances;
          if (templates != null) templates  = (MemberList)templates.Clone();
          for (int i = 0, n = templates == null ? 0 : templates.Count; i < n; i++){
            Method meth = templates[i] as Method;
            if (meth == null) continue;
            if (meth.TemplateParameters == null || meth.TemplateParameters.Count == 0) continue;
            templates[i] = meth.GetTemplateInstance(this.currentType, tArgs);
          }
          return instance.BoundMembers = templates;}
        case NodeType.NameBinding:
          return ((NameBinding)expression).BoundMembers;
        case NodeType.QualifiedIdentifer:
          QualifiedIdentifier qual = (QualifiedIdentifier)expression;
          Identifier id = qual.Identifier;
          Identifier prefix = id.Prefix;
          qual.Qualifier = this.VisitExpression(qual.Qualifier);
          if (qual.Qualifier == null) break;
          TypeNode qualifierType = TypeNode.StripModifiers(qual.Qualifier.Type);
          TypeNode lookupType = qualifierType;
          if (lookupType == SystemTypes.Type) {
            Literal literal = qual.Qualifier as Literal;
            if (literal != null) lookupType = (TypeNode)literal.Value;
          }
          lookupType = this.typeSystem.UnwrapForMemberLookup(lookupType);
          Interface iface = lookupType as Interface;
          if (iface != null) return ((Interface)this.GetTypeView(iface)).GetAllMembersNamed(id);
          ClassParameter cp = lookupType as ClassParameter;
          if (cp != null) return ((ClassParameter)this.GetTypeView(cp)).GetAllMembersNamed(id);
          while (lookupType != null){
            lookupType = this.typeSystem.UnwrapForMemberLookup(lookupType);
            MemberList result = this.GetTypeView(lookupType).GetMembersNamed(id);
            if (result != null){
              int n = result.Count;
              if (n > 0 && backupReturn.Count == 0) backupReturn = result;
              if (prefix != null){
                for (int i = 0, m = n; i < m; i++){
                  Member mem = result[i];
                  Identifier uri = this.GetUriNamespaceFor(mem);
                  if (uri == null || uri.UniqueIdKey != prefix.UniqueIdKey) n--;
                }
              }
              for (int i = 0, m = n; i < m; i++) {
                Member mem = result[i];
                TypeNode tn = qualifierType;
                Reference rtn = tn as Reference;
                if (rtn != null) {
                  tn = rtn.ElementType;
                }
                if (!(mem is InstanceInitializer) && Checker.NotAccessible(mem, ref tn, this.currentModule, this.currentType, this.TypeViewer)) {
                  n--; continue;
                }
                Method meth = mem as Method;
                if (meth != null && meth.ImplementedTypeExpressions != null && meth.ImplementedTypeExpressions.Count > 0)
                  n--;
                else {
                  Property prop = mem as Property;
                  if (prop != null && prop.ImplementedTypeExpressions != null && prop.ImplementedTypeExpressions.Count > 0)
                    n--;
                }
              }
              if (n > 0) return result;
            }
            lookupType = lookupType.BaseType;
          }
          break;
      }
      return backupReturn;
    }
    public bool considerInaccessibleMethods = false;
    public virtual Member ResolveOverload(MemberList members, ExpressionList arguments){
      return this.ResolveOverload(members, arguments, false);
    }
    public virtual Member ResolveOverload(MemberList members, ExpressionList arguments, bool doNotDiscardOverloadsWithMoreParameters){
      if (members == null || members.Count == 0) return null;
      TypeNode declaringType = members[0].DeclaringType;
      Identifier id = members[0].Name;
      int n = arguments == null ? 0 : arguments.Count;
      TypeNode[] argTypes = new TypeNode[n];
      for (int i = 0; i < n; i++){
        Expression arg = arguments[i];
        if (arg == null){
          argTypes[i] = SystemTypes.Object;
        }else{
          Literal lit = arg as Literal;
          if (lit != null && lit.Type != null && lit.Type.IsPrimitiveInteger && !lit.TypeWasExplicitlySpecifiedInSource)
            argTypes[i] = this.typeSystem.SmallestIntegerType(lit.Value);
          else
            argTypes[i] = TypeNode.StripModifiers(arg.Type);
        }
      }
      TypeNodeList bestParamTypes = new TypeNodeList(n);
      for (int i = 0; i < n; i++) bestParamTypes.Add(TypeSystem.DoesNotMatchAnyType);
      TypeNode bestElementType = null;
      Member bestMember = this.GetBestMatch(members, arguments, argTypes, bestParamTypes, null, ref bestElementType, doNotDiscardOverloadsWithMoreParameters);
      if (bestMember == Resolver.MethodNotFound) return null;
      if (!(members[0] is InstanceInitializer || members[0] is StaticInitializer)){
        while (declaringType != SystemTypes.Object && (declaringType = declaringType.BaseType) != null){
          MemberList baseMembers = this.GetTypeView(declaringType).GetMembersNamed(id);
          if (baseMembers.Count == 0) continue;
          bestMember = this.GetBestMatch(baseMembers, arguments, argTypes, bestParamTypes, bestMember, ref bestElementType);
        }
      }
      if (bestMember == null){
        //Search again, but consider inaccessible members. Otherwise the error message does not distinguish between no member no accessible member.
        this.considerInaccessibleMethods = true;
        declaringType = members[0].DeclaringType;
        for (int i = 0; i < n; i++) bestParamTypes[i] = TypeSystem.DoesNotMatchAnyType; 
        bestElementType = null;
        bestMember = this.GetBestMatch(members, arguments, argTypes, bestParamTypes, null, ref bestElementType);
        if (bestMember == Resolver.MethodNotFound) return null;
        if (!(members[0] is InstanceInitializer || members[0] is StaticInitializer)){
          while ((declaringType = declaringType.BaseType) != null){
            MemberList baseMembers = this.GetTypeView(declaringType).GetMembersNamed(id);
            if (baseMembers.Count == 0) continue;
            Member bestMember2 = this.GetBestMatch(baseMembers, arguments, argTypes, bestParamTypes, bestMember, ref bestElementType);
            if (bestMember2 != null) bestMember = bestMember2;
          }
        }
        this.considerInaccessibleMethods = false;
      }
      return bestMember;
    }
    /// <summary>
    /// Go through eligible members (+ bestSoFar) returning the one with the best match to argTypes.
    /// Returns null if there is no single best match. Sets bestParamTypes to a signature
    /// that any other member (from a base class) has to equal or better to best overall.
    /// </summary>
    public virtual Member GetBestMatch(MemberList members, ExpressionList arguments, TypeNode[] argTypes, 
      TypeNodeList bestParamTypes, Member bestSoFar, ref TypeNode bestElementType){
      return this.GetBestMatch(members, arguments, argTypes, bestParamTypes, bestSoFar, ref bestElementType, false);
    }
    public virtual Member GetBestMatch(MemberList members, ExpressionList arguments, TypeNode[] argTypes,
      TypeNodeList bestParamTypes, Member bestSoFar, ref TypeNode bestElementType, bool doNotDiscardOverloadsWithMoreParameters){
      int m = members.Count;
      int n = argTypes.Length;
      if (m == 1 && bestSoFar == null){ //The usual case, so special case it. (Avoids calls to BetterMatch.)
        Member member = members[0];
        ParameterList parameters = this.GetParamsIfEligible(member, n, arguments, argTypes);
        if (parameters == null){
          switch (member.NodeType){
            case NodeType.Method:
            case NodeType.InstanceInitializer:
            case NodeType.StaticInitializer: // this can happen in type extensions in Sing#
            case NodeType.Property:
              return null;
            default:
              //Non method found. Proceed as if the current class has no methods and all methods in base classes are hidden.
              //Return the best so far, if not null. Otherwise return MethodNotFound so that the caller knows not to search base classes.
              return bestSoFar == null ? Resolver.MethodNotFound : bestSoFar;
          }
        }
        int k = parameters.Count;
        TypeNode elementType = k < 1 || parameters[k-1] == null ? null : parameters[k-1].GetParamArrayElementType();
        int knm = k < n ? k : n;
        if (knm == k && elementType != null) knm--;
        for (int i = 0; i < knm; i++) 
          bestParamTypes[i] = TypeNode.StripModifiers(parameters[i].Type);
        if (knm < n){ //More arguments than processed above, can only happen if there is a param array
          Debug.Assert(k > 0); //should be ensured by GetParamsIfEligible
          Debug.Assert(elementType != null); //should be ensured by GetParamsIfEligible
          for (int i = knm; i < n; i++) 
            bestParamTypes[i] = bestElementType; 
        }
        return member;
      }
      for (int j = 0; j < m; j++){
        Member member = members[j];
        ParameterList parameters = this.GetParamsIfEligible(member, n, arguments, argTypes, doNotDiscardOverloadsWithMoreParameters);
        if (parameters == null){
          switch (member.NodeType) {
            case NodeType.Method:
            case NodeType.InstanceInitializer:
            case NodeType.StaticInitializer: // this can happen in type extensions in Sing#
            case NodeType.Property:
              continue;
            default:
              //This class may not have methods with the same name, so just give up.
              if (member.DeclaringType is Interface)
                continue;
              return bestSoFar == null ? Resolver.MethodNotFound : bestSoFar;
          }
        }
        int k = parameters.Count;
        if (k == 0 && n == 0 && bestSoFar == null) return member; //Method with no params is perfect match for no args
        TypeNode lastArgType = n < 1 ? null : argTypes[n-1];
        TypeNode lastParType = k < 1 || parameters[k-1] == null ? null : TypeNode.StripModifiers(parameters[k-1].Type);
        TypeNode elementType = k < 1 || parameters[k-1] == null ? null : parameters[k-1].GetParamArrayElementType();
        elementType = TypeNode.StripModifier(elementType, SystemTypes.NonNullType);
        if (k == 1 && n == 0 && bestSoFar == null && elementType != null){
          bestSoFar = member; //Have not yet seen a method with no params, so a method with a param array will beat anything else seen so far
          continue;
        }
        bool better = false;
        bool worse = false;
        bool identical = true;
        bool betterMatchNotInvolvingObject = false;
        bool worseMatchNotInvolvingObject = false;
        //Look at arguments that match parameters (not param arrays)
        int knm = k < n ? k : n;
        if (knm == k && elementType != null && !(lastArgType is ArrayType && this.GetTypeView(lastArgType).IsAssignableTo(lastParType))) knm--;
        for (int i = 0; i < knm; i++){
          TypeNode currParType = TypeNode.StripModifiers(parameters[i].Type);
          TypeNode bestParType = bestParamTypes[i];
          TypeNode argType = argTypes[i];
          if (currParType != bestParType){
            identical = false;
            if (this.typeSystem.IsBetterMatch(currParType, bestParType, argType, this.TypeViewer)){
              if (bestParType != SystemTypes.Object){
                if (Literal.IsNullLiteral(arguments[i]) && !bestParType.IsValueType) {
                  worse = true;
                  continue;
                }
                betterMatchNotInvolvingObject = currParType != SystemTypes.Object;
                bestParamTypes[i] = currParType;
              }
              better = true;
            } else if (this.typeSystem.IsBetterMatch(bestParType, currParType, argType, this.TypeViewer)) {
              if (Literal.IsNullLiteral(arguments[i]) && currParType != null && !currParType.IsValueType) {
                continue;
              }
              worse = true;
              if (currParType != SystemTypes.Object)
                worseMatchNotInvolvingObject = true;
            } else if (Literal.IsNullLiteral(arguments[i])) {
              if (currParType is Pointer && bestParType is Reference)
                better = true;
              else if (currParType is Reference && bestParType is Pointer)
                worse = true;
            }
          }else if (i == k-1 && bestElementType != null)
            better = true; //Best match so far has a param array, this match does not, so it wins
        }
        //Look at arguments that match up to param array, if any
        if (knm < n){
          Debug.Assert(k > 0); //should be ensured by GetParamsIfEligible
          Debug.Assert(elementType != null); //should be ensured by GetParamsIfEligible
          for (int i = knm; i < n; i++){
            TypeNode bestParType = bestParamTypes[i];
            TypeNode argType = argTypes[i];
            if (elementType != bestParType){
              identical = false;
              if (this.typeSystem.IsBetterMatch(elementType, bestParType, argType, this.TypeViewer)){
                better = true;
                if (bestParType != SystemTypes.Object){
                  betterMatchNotInvolvingObject = true;
                  bestParamTypes[i] = elementType;
                }
              }else if (this.typeSystem.IsBetterMatch(bestParType, elementType, argType, this.TypeViewer)){
                worse = true;
                if (elementType != SystemTypes.Object)
                  worseMatchNotInvolvingObject = true;
              }else if (bestElementType == null)
                worse = true; //The existing best match does not have a param array, so it wins
            }
          }
        }
        if (better || (!worse && bestElementType != null && elementType == null)){ //The current best match must go (except if object is involved)
          if (worse && worseMatchNotInvolvingObject){
            if (betterMatchNotInvolvingObject) //The current member is not eligble either
              bestSoFar = null;
          }else{ //Make the current member the best so far
            for (int i = 0; i < knm; i++) bestParamTypes[i] = TypeNode.StripModifiers(parameters[i].Type);
            for (int i = knm; i < n; i++) bestParamTypes[i] = elementType;
            bestSoFar = member;
            bestElementType = elementType;
          }
        }else if (identical){
          if (bestSoFar != null && bestSoFar.IsSpecialName && !member.IsSpecialName)
            bestSoFar = member;
          else {
            Method bsf = bestSoFar as Method;
            Method mem = member as Method;
            if (bsf != null && mem != null) {
              if (bsf.IsGeneric && !mem.IsGeneric) //  Give preference to non generic method
                bestSoFar = member;
            }
          }
        }else if (!worse){ //The current best match must go, but member is not eligible either
          bestSoFar = null;
        }
      }
      return bestSoFar;
    }
    public virtual ParameterList GetParamsIfEligible(Member member, int numActualParams, ExpressionList actualPars, TypeNode[] actualParTypes){
      return this.GetParamsIfEligible(member, numActualParams, actualPars, actualParTypes, false);
    }
    bool IsProperInstantiation(Method method) {
      if (method == null) return false;
      if (method.Template == null) return true;
      int len = method.Template.TemplateParameters == null ? 0 : method.Template.TemplateParameters.Count;
      int numTemplArgs = method.TemplateArguments == null ? 0 : method.TemplateArguments.Count;
      if (numTemplArgs != len) return false;
      Specializer specializer = len == 0 ? null : new Specializer(method.DeclaringType.DeclaringModule, method.Template.TemplateParameters, method.TemplateArguments);
      if (specializer != null) specializer.CurrentType = this.currentType;
      for (int i = 0; i < len; i++) {
        TypeNode formal = method.Template.TemplateParameters[i];
        ITypeParameter formaltp = (ITypeParameter)formal;
        TypeNode actual = method.TemplateArguments[i];
        if (formal == null || actual == null) continue;
        // make sure actual is assignable to base of formal and to each interface
        TypeNode fbaseType = specializer == null ? formal.BaseType : specializer.VisitTypeReference(formal.BaseType);
        if (fbaseType != null && (
          ((formaltp.TypeParameterFlags & TypeParameterFlags.ReferenceTypeConstraint) == TypeParameterFlags.ReferenceTypeConstraint && !actual.IsObjectReferenceType)
          || ((formaltp.TypeParameterFlags & TypeParameterFlags.ValueTypeConstraint) == TypeParameterFlags.ValueTypeConstraint && !actual.IsValueType)
          || !this.typeSystem.AssignmentCompatible(TypeNode.StripModifiers(actual), fbaseType, this.TypeViewer))){
          return false;
        }
        InterfaceList formal_ifaces = this.GetTypeView(formal).Interfaces;
        if (formal_ifaces != null){
          for (int j = 0, n = formal_ifaces.Count; j < n; j++) {
            TypeNode intf = specializer == null ? formal_ifaces[j] : specializer.VisitTypeReference(formal_ifaces[j]);
            if (intf == null) continue;
            if (intf != SystemTypes.ITemplateParameter &&
                !this.typeSystem.AssignmentCompatible(TypeNode.StripModifiers(actual), intf, this.TypeViewer)){
              return false;
            }
          }
        }
      }
      return true;
    }
    public virtual ParameterList GetParamsIfEligible(Member member, int numActualParams, ExpressionList actualPars, TypeNode[] actualParTypes, bool doNotDiscardOverloadsWithMoreParameters) {
      bool varargMethod = false;
      if (member == null) return null;
      ParameterList parameters = null;
      switch(member.NodeType){
        case NodeType.Method:
        case NodeType.InstanceInitializer:
        case NodeType.StaticInitializer: // this can happen in type extensions in Sing#
          Method meth = (Method)member;
          if (meth.ImplementedTypes != null && meth.ImplementedTypes.Count > 0) return null;
          if (this.NotEligible(meth)) return null;
          parameters = meth.Parameters;
          varargMethod = (meth.CallingConvention == CallingConventionFlags.VarArg);
          if (varargMethod){
            parameters = (ParameterList)parameters.Clone();
            parameters.Add(Resolver.ArglistDummyParameter);
          }
          break;
        case NodeType.Property:
          Property prop = (Property)member;
          if (prop.ImplementedTypes != null && prop.ImplementedTypes.Count > 0) return null;
          if (this.NotEligible(prop.Getter) && this.NotEligible(prop.Setter)) return null;
          parameters = prop.Parameters; 
          break;
        default:
          return null;
      }
      int n = parameters == null ? 0 : parameters.Count;
      TypeNode elementType = n == 0 ? null : parameters[n-1] == null ? null : parameters[n-1].GetParamArrayElementType();
      if (n < numActualParams || (n == numActualParams + 1 && !doNotDiscardOverloadsWithMoreParameters))
        if (elementType == null) return null; //last param is not a param array, so this cannot be a valid match      
      if ((n > numActualParams+1 || (n > numActualParams && elementType == null)) && !doNotDiscardOverloadsWithMoreParameters) 
        //Consider only if all extra parameters are optional
        for (int i = numActualParams; i < n; i++) if (parameters[i].DefaultValue == null) return null;
      if (parameters == null) return ParameterList.Empty;
      if (this.considerInaccessibleMethods) {
        // also check whether there is a matching problem 
        for (int i = 0; i < numActualParams; i++) {
          if (i < n) {
            if (parameters[i] == null || parameters[i].Type == null)
              continue;
            TypeNode pT = TypeNode.StripModifiers(parameters[i].Type);
            if (actualPars[i] is ArglistArgumentExpression && pT == SystemTypes.Object) {
              return null;
            }
          } else continue;
        }
        return parameters;
      }
      //Check that actual parameters can be converted to corresponding formal parameter types
      for (int i = 0; i < numActualParams; i++){
        if (i < n){
          Parameter p = parameters[i];
          if (p == null || p.Type == null) return null;
          TypeNode pType = TypeNode.StripModifiers(p.Type);
          if (!this.typeSystem.ImplicitCoercionFromTo(actualPars[i], actualParTypes[i], pType, this.TypeViewer)){
            if (actualPars[i] is ArglistArgumentExpression) {
              if (pType == SystemTypes.Object) {
                return null;
              }
            }
            if (actualParTypes[i] == SystemTypes.Delegate && pType is DelegateNode) {
              MemberBinding mb = actualPars[i] as MemberBinding;
              Method m = mb == null ? null : mb.BoundMember as Method;
              Expression ob = mb == null ? null : mb.TargetObject;
              if (m != null) {
                Expression arg = this.VisitConstructDelegate(
                  new ConstructDelegate(pType, ob, new Identifier(m.Name.Name, mb.SourceContext), mb.SourceContext));
                if (arg != null) {
                  Construct cons = arg as Construct;
                  if (cons != null && cons.Constructor is Literal) return null; //dummy construct node
                  actualPars[i] = arg; continue;
                }
              }
            }
            if (i < n-1 || elementType == null) return null;
          }else
            continue;
        }
        if (!this.typeSystem.ImplicitCoercionFromTo(actualParTypes[i], TypeNode.StripModifiers(elementType), this.TypeViewer)) return null;
      }
      return parameters;
    }
    public virtual bool NotEligible(Method meth){
      if (meth == null) return true;
      if (meth.TemplateParameters != null && meth.TemplateParameters.Count > 0) return true;
      if (meth.CallingConvention == CallingConventionFlags.VarArg) return false;
      if ((meth.CallingConvention & CallingConventionFlags.ArgumentConvention) != CallingConventionFlags.Default) return true;
      TypeNode dummy = null;
      if (this.considerInaccessibleMethods) return false;
      //TODO: add more conditions
      if (!this.IsProperInstantiation(meth)) return true;
      if (Checker.NotAccessible(meth, ref dummy, (int)(meth.Flags & MethodFlags.MethodAccessMask), this.currentModule, this.currentType, this.TypeViewer)) return true;
      return false;
    }
    public virtual Method GetBinaryOperatorOverload(BinaryExpression binaryExpression){
      Identifier operatorId = this.GetBinaryOperatorOverloadName(binaryExpression.NodeType);
      if (operatorId == null || binaryExpression.Operand1 == null || binaryExpression.Operand2 == null) return null;
      TypeNode t1 = TypeNode.StripModifiers(binaryExpression.Operand1.Type);
      TypeNode t2 = TypeNode.StripModifiers(binaryExpression.Operand2.Type);
      Reference r1 = t1 as Reference;
      if (r1 != null) t1 = r1.ElementType;
      Reference r2 = t2 as Reference;
      if (r2 != null) t2 = r2.ElementType;
      if (t1 == null || t2 == null) return null;
      MemberList overloads1 = t1 == SystemTypes.String ? null : this.GetOperatorOverloadsNamed(operatorId, t1);
      MemberList overloads2 = t2 == SystemTypes.String ? null : this.GetOperatorOverloadsNamed(operatorId, t2);
      //TODO: filter out non special names
      if ((overloads1 == null || overloads1.Count == 0) && (overloads2 == null || overloads2.Count == 0)) return null;
      ExpressionList arguments = new ExpressionList(2);
      arguments.Add(binaryExpression.Operand1);
      arguments.Add(binaryExpression.Operand2);
      Method operator1 = this.ResolveOverload(overloads1, arguments) as Method;
      Method operator2 = this.ResolveOverload(overloads2, arguments) as Method;
      if (operator1 != null && !this.IsOverloadEligible(operator1, arguments)) operator1 = null;
      if (operator2 != null && !this.IsOverloadEligible(operator2, arguments)) operator2 = null;
      if (operator2 == null) return operator1;
      if (operator1 == null || operator1 == operator2) return operator2;
      //Both types have defined an overload for this operator. Choose the closest match.
      TypeNode m1t1 = operator1.Parameters[0].Type;
      TypeNode m1t2 = operator1.Parameters[1].Type;
      TypeNode m2t1 = operator2.Parameters[0].Type;
      TypeNode m2t2 = operator2.Parameters[1].Type;
      bool m1Better = this.typeSystem.IsBetterMatch(m1t1, m2t1, t1, this.TypeViewer) || this.typeSystem.IsBetterMatch(m1t2, m2t2, t2, this.TypeViewer);
      bool m2Better = this.typeSystem.IsBetterMatch(m2t1, m1t1, t1, this.TypeViewer) || this.typeSystem.IsBetterMatch(m2t2, m1t2, t2, this.TypeViewer);
      if (m1Better && !m2Better) return operator1;
      if (m2Better && !m1Better) return operator2;
      if (t2 == SystemTypes.Decimal) return operator1;
      if (t1 == SystemTypes.Decimal) return operator2;
      //Ambiguous match.
      //TODO: return a method that indicates there is a problem
      return null;
    }
    public virtual bool IsOverloadEligible(Method m, ExpressionList args){
      int nArgs = args == null ? 0 : args.Count;
      TypeNode[] types = new TypeNode[nArgs];
      for (int i = 0; i < nArgs; i++){
        Expression x = args[i];
        if (x == null) continue;
        types[i] = TypeNode.StripModifiers(args[i].Type);
      }
      return this.GetParamsIfEligible(m, nArgs, args, types) != null;
    }
    public virtual Identifier GetBinaryOperatorOverloadName(NodeType operatorType){
      switch(operatorType){
        case NodeType.Add : return StandardIds.opAddition;
        case NodeType.And:
        case NodeType.LogicalAnd : return StandardIds.opBitwiseAnd;
        case NodeType.Comma: return StandardIds.opComma;
        case NodeType.Div : return StandardIds.opDivision; 
        case NodeType.Eq : return StandardIds.opEquality; 
        case NodeType.Ge : return StandardIds.opGreaterThanOrEqual; 
        case NodeType.Gt : return StandardIds.opGreaterThan;
        case NodeType.Le : return StandardIds.opLessThanOrEqual; 
        case NodeType.Lt : return StandardIds.opLessThan;
        case NodeType.Mul : return StandardIds.opMultiply;
        case NodeType.Ne : return StandardIds.opInequality;
        case NodeType.LogicalOr:
        case NodeType.Or : return StandardIds.opBitwiseOr;
        case NodeType.Rem : return StandardIds.opModulus;
        case NodeType.Shl : return StandardIds.opLeftShift;
        case NodeType.Shr : return StandardIds.opRightShift;
        case NodeType.Sub : return StandardIds.opSubtraction;
        case NodeType.Xor : return StandardIds.opExclusiveOr;
        default: return null;
      }
    }
    public virtual Identifier GetUnaryOperatorOverloadName(NodeType operatorType){
      switch(operatorType){
        case NodeType.Add : return StandardIds.opIncrement;
        case NodeType.Decrement : return StandardIds.opDecrement;
        case NodeType.Increment : return StandardIds.opIncrement;
        case NodeType.LogicalNot : return StandardIds.opLogicalNot;
        case NodeType.Not : return StandardIds.opOnesComplement;
        case NodeType.Neg : return StandardIds.opUnaryNegation;
        case NodeType.Sub : return StandardIds.opDecrement;
        case NodeType.UnaryPlus : return StandardIds.opUnaryPlus;
        default: return null;
      }
    }
    public virtual MemberList GetOperatorOverloadsNamed(Identifier id, TypeNode t){
      while (t != null){
        MemberList members = this.GetTypeView(t).GetMembersNamed(id);
        int n = members == null ? 0 : members.Count;
        if (n > 0){
          int eligibleMembers = 0;
          for (int i = 0; i < n; i++){
            Method m = members[i] as Method;
            if (m == null || !m.IsSpecialName) continue;
            eligibleMembers++;
          }
          if (eligibleMembers == n) return members;
          if (eligibleMembers > 0){
            MemberList filteredMembers = new MemberList(eligibleMembers);
            for (int i = 0; i < n; i++){
              Method m = members[i] as Method;
              if (m == null || !m.IsSpecialName) continue;
              filteredMembers.Add(m);
            }
            return filteredMembers;
          }
        }
        t = t.BaseType;
      }
      return null;
    }
    public virtual TypeNode InferTypeOfBinaryExpression(TypeNode t1, TypeNode t2, BinaryExpression binaryExpression){
      if (t1 == null || t2 == null || binaryExpression == null) return SystemTypes.Object;
      if (this.currentType is EnumNode && 
      (binaryExpression.NodeType == NodeType.Or || binaryExpression.NodeType == NodeType.And)) {
        if (t1 is EnumNode && binaryExpression.Operand1 is Literal) {
          binaryExpression.Operand1 = this.typeSystem.ExplicitLiteralCoercion(
            (Literal)binaryExpression.Operand1, t1, ((EnumNode)t1).UnderlyingType, null);
          t1 = ((EnumNode)t1).UnderlyingType;
        }
        if (t2 is EnumNode && binaryExpression.Operand2 is Literal) {
          binaryExpression.Operand2 = this.typeSystem.ExplicitLiteralCoercion(
            (Literal)binaryExpression.Operand2, t1, ((EnumNode)t2).UnderlyingType, null);
          t2 = ((EnumNode)t2).UnderlyingType;
        }
      }
      Literal lit1 = binaryExpression.Operand1 as Literal;
      Literal lit2 = binaryExpression.Operand2 as Literal;
      switch (binaryExpression.NodeType){
        case NodeType.Add:
        case NodeType.Sub:
          EnumNode e1 = t1 as EnumNode;
          EnumNode e2 = t2 as EnumNode;
          if (e1 != null && this.typeSystem.ImplicitCoercionFromTo(lit2, t2, e1.UnderlyingType, this.TypeViewer))
            return e1;
          if (e2 != null && this.typeSystem.ImplicitCoercionFromTo(lit1, t1, e2.UnderlyingType, this.TypeViewer))
            return e2;
          if (e1 != null && e1 == e2 && binaryExpression.NodeType == NodeType.Sub)
            return e1.UnderlyingType;
          goto case NodeType.And;

        case NodeType.And:
        case NodeType.Or:
        case NodeType.Xor:
        case NodeType.Div:
        case NodeType.Mul:
        case NodeType.Rem:
          if (lit1 != null && t1.IsPrimitiveNumeric){
            if (t2.IsPrimitiveNumeric &&
              this.typeSystem.ImplicitLiteralCoercionFromTo(lit1, t1, t2) && !this.typeSystem.ImplicitCoercionFromTo(t2, t1, this.TypeViewer))
              return t2;
            return this.typeSystem.UnifiedType(lit1, t2, this.TypeViewer);
          }else if (lit2 != null && t2.IsPrimitiveNumeric){
            if (t1.IsPrimitiveNumeric &&
              this.typeSystem.ImplicitLiteralCoercionFromTo(lit2, t2, t1) && !this.typeSystem.ImplicitCoercionFromTo(t1, t2, this.TypeViewer))
              return t1;
            return this.typeSystem.UnifiedType(lit2, t1, this.TypeViewer);
          }
          //TODO: if t1 or t2 is not primitive numeric, but has a coercion to one of those use that type in the inference
          return this.typeSystem.UnifiedType(t1, t2);

        case NodeType.AddEventHandler: 
        case NodeType.RemoveEventHandler:
          return SystemTypes.Void;

        case NodeType.Box: 
        case NodeType.Ldvirtftn: 
        case NodeType.Mkrefany: 
        case NodeType.Refanyval:
          return SystemTypes.Object;

        case NodeType.NullCoalesingExpression: {
          t1 = TypeNode.StripModifier(binaryExpression.Operand1.Type, SystemTypes.NonNullType);
          t2 = TypeNode.StripModifier(binaryExpression.Operand2.Type, SystemTypes.NonNullType);
          TypeNode nonNullT1 = this.typeSystem.RemoveNullableWrapper(t1);
          if (this.typeSystem.IsNullableType(t1) && this.typeSystem.ImplicitCoercionFromTo(binaryExpression.Operand2, t2, nonNullT1))
            return nonNullT1;
          else if (this.typeSystem.ImplicitCoercionFromTo(binaryExpression.Operand2, t2, t1)) {
            if (this.typeSystem.IsNonNullType(binaryExpression.Operand2.Type))
              return OptionalModifier.For(SystemTypes.NonNullType, t1);
            else
              return t1;
          } else if (this.typeSystem.ImplicitCoercionFromTo(nonNullT1, t2)) {
            return binaryExpression.Operand2.Type;
          }
          return SystemTypes.Object;
        }

        case NodeType.As:
        case NodeType.Castclass:
        case NodeType.ExplicitCoercion: 
        case NodeType.Isinst: 
        case NodeType.Unbox: 
          Literal lit = binaryExpression.Operand2 as Literal;
          if (lit != null){
            TypeNode t = lit.Value as TypeNode;
            if (t != null) {
              if (binaryExpression.NodeType == NodeType.As && this.typeSystem.IsNonNullType(t)){
                // "e as T!" can return null, so it is really of type T, not T!
                OptionalModifier om = t as OptionalModifier;
                return om.ModifiedType;
              }
              // adjust if it is a nonnull coercion (!)
              TypeNode oprnd1Type = binaryExpression.Operand1.Type;
              if (t == SystemTypes.NonNullType && oprnd1Type != null) {
                if (oprnd1Type.IsValueType){
                  this.HandleError(binaryExpression, Error.ValueTypeIsAlreadyNonNull, this.GetTypeName(oprnd1Type));
                  return oprnd1Type;
                }
                // TODO: Should issue a warning that the (!) is redundant!!!
                if (this.typeSystem.IsNonNullType(oprnd1Type)){
                  // avoid wrapping it twice since T!! is not the same as T! (inside the compiler)
                  // BUT IT SHOULD BE!!
                  return oprnd1Type;
                }
                t = OptionalModifier.For(t, oprnd1Type);
                binaryExpression.Operand2 = new Literal(t, lit.Type, lit.SourceContext);
              }
              return t;
            }
          }
          MemberBinding mb = binaryExpression.Operand2 as MemberBinding;
          if (mb != null){
            TypeNode t = mb.BoundMember as TypeNode;
            if (t != null) {
              // adjust if it is a nonnull coercion (!)
              if (t == SystemTypes.NonNullType && binaryExpression.Operand1 != null && binaryExpression.Operand1.Type != null) {
                t = OptionalModifier.For(t, binaryExpression.Operand1.Type);
                mb.BoundMember = t;
              }
              return t;
            }
          }
          return SystemTypes.Object;

        case NodeType.Comma:
          return binaryExpression.Operand2.Type;

        case NodeType.Ceq: 
        case NodeType.Cgt: 
        case NodeType.Cgt_Un: 
        case NodeType.Clt: 
        case NodeType.Clt_Un: 
        case NodeType.Eq: 
        case NodeType.Ge: 
        case NodeType.Gt: 
        case NodeType.Is: 
        case NodeType.Iff: 
        case NodeType.Implies: 
        case NodeType.Le: 
        case NodeType.LogicalAnd:
        case NodeType.LogicalOr:
        case NodeType.Lt: 
        case NodeType.Ne: 
          return SystemTypes.Boolean;

        case NodeType.Range:
          TypeNode unifiedType = this.typeSystem.UnifiedType(t1, t2);
          if (unifiedType == null || (! (unifiedType==SystemTypes.Int32 || unifiedType is EnumNode))) {
            this.HandleError(binaryExpression, Error.TypeMustSupportIntCoercions, binaryExpression.SourceContext.SourceText);
            return null;
          }
          return  SystemTypes.Range; 
          
        case NodeType.Maplet:
          return SystemTypes.DictionaryEntry;
          
        case NodeType.Shl:
        case NodeType.Shr:
        case NodeType.Shr_Un:
          if (t1 != null) {
            switch(t1.TypeCode){
              case TypeCode.Byte:
              case TypeCode.UInt16:
              case TypeCode.SByte:
              case TypeCode.Int16:
              case TypeCode.Char:
                return SystemTypes.Int32;
              case TypeCode.UInt32:
                return SystemTypes.UInt32;
              case TypeCode.Int64:
                return SystemTypes.Int64;
              case TypeCode.UInt64:
                return SystemTypes.UInt64;
              case TypeCode.Object:
                if (this.typeSystem.ImplicitCoercionFromTo(t1, SystemTypes.Int32))
                  return SystemTypes.Int32;
                if (this.typeSystem.ImplicitCoercionFromTo(t1, SystemTypes.UInt32))
                  return SystemTypes.UInt32;
                if (this.typeSystem.ImplicitCoercionFromTo(t1, SystemTypes.Int64))
                  return SystemTypes.Int64;
                if (this.typeSystem.ImplicitCoercionFromTo(t1, SystemTypes.UInt64))
                  return SystemTypes.UInt64;
                break;
            }
          }
          return t1;

        default:
          return this.typeSystem.UnifiedType(t1, t2);
      }
    }
    public virtual TypeNode InferTypeOfTernaryExpression(TypeNode t1, TypeNode t2, TypeNode t3, TernaryExpression ternaryExpression){
      if (t1 == null || t2 == null || t3 == null || ternaryExpression == null || ternaryExpression.NodeType != NodeType.Conditional)
        return SystemTypes.Object;
      return this.InferTypeOfThenElse(t2, t3, ternaryExpression.Operand2, ternaryExpression.Operand3);
    }
    public virtual TypeNode InferTypeOfThenElse(TypeNode t2, TypeNode t3, Expression e2, Expression e3){
      if (t2 == t3) return t2;
      if (this.typeSystem.ImplicitCoercionFromTo(e2, t2, t3, this.TypeViewer)){
        if (this.typeSystem.ImplicitCoercionFromTo(e3, t3, t2, this.TypeViewer)) {
          if (t3 == SystemTypes.Object){
            Literal lit = e3 as Literal;
            if (lit != null && lit.Value == null) 
              return this.typeSystem.Unwrap(t2);
          }
          if (t2 == SystemTypes.Object){
            Literal lit = e2 as Literal;
            if (lit != null && lit.Value == null)
              return this.typeSystem.Unwrap(t3);
          }
          Literal lit2 = e2 as Literal;
          Literal lit3 = e3 as Literal;
          if (lit2 != null && !lit2.TypeWasExplicitlySpecifiedInSource){
            if (lit3 == null || lit3.TypeWasExplicitlySpecifiedInSource) return t3;
            if (t2.IsPrimitiveUnsignedInteger) return t2;
            if (t3.IsPrimitiveUnsignedInteger) return t3;
          }
          if (lit3 != null && !lit3.TypeWasExplicitlySpecifiedInSource) {
            if (lit2 == null || lit2.TypeWasExplicitlySpecifiedInSource) return t2;
            if (t2.IsPrimitiveUnsignedInteger) return t2;
            if (t3.IsPrimitiveUnsignedInteger) return t3;
          }
          return SystemTypes.Object;
        }
        return t3;
      }
      if (this.typeSystem.ImplicitCoercionFromTo(e3, t3, t2, this.TypeViewer))
        return t2;
      if (this.typeSystem.IsNonNullType(t2))
        return this.InferTypeOfThenElse(TypeNode.StripModifiers(t2), t3, e2, e3);
      else if (this.typeSystem.IsNonNullType(t3))
        return this.InferTypeOfThenElse(t2, TypeNode.StripModifiers(t3), e2, e3);
      return SystemTypes.Object;
    }
    public virtual TypeNode InferTypeOfUnaryExpression(TypeNode origType, UnaryExpression unaryExpression){
      if (unaryExpression == null) return origType;
      TypeNode t = this.typeSystem.Unwrap(origType);
      if (this.typeSystem.IsNullableType(t)) return this.typeSystem.CreateNullableWrapper(this.currentType, InferTypeOfUnaryExpression(this.typeSystem.RemoveNullableWrapper(t), unaryExpression));
      switch (unaryExpression.NodeType){
        case NodeType.AddressOf: return origType.GetPointerType();
        case NodeType.OutAddress: 
        case NodeType.RefAddress: return origType.GetReferenceType();
        case NodeType.Ldftn: return SystemTypes.IntPtr;
        case NodeType.LogicalNot: return SystemTypes.Boolean;
        case NodeType.UnaryPlus:
        case NodeType.Neg:
        case NodeType.Not:
          switch(t.TypeCode){
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Char:
              return SystemTypes.Int32;
            case TypeCode.UInt32:
              if (unaryExpression.NodeType == NodeType.Neg)
                return SystemTypes.Int64;
              break;
          }
          break;
        case NodeType.Parentheses:
        case NodeType.SkipCheck:
        case NodeType.Ckfinite:
          if (unaryExpression.Operand == null){
            unaryExpression.IsErroneous = true;
            return t;
          }
          return unaryExpression.Operand.Type;
        case NodeType.DefaultValue:
          Literal lit = unaryExpression.Operand as Literal;
          if (lit == null){
            unaryExpression.IsErroneous = true;
            return SystemTypes.Object;
          }
          TypeNode lt = lit.Value as TypeNode;
          if (lt == null){
            unaryExpression.IsErroneous = true;
            return SystemTypes.Object;
          }
          return lt;
        case NodeType.Sizeof:
          return SystemTypes.Int32;
        case NodeType.Typeof:
          return !this.NonNullChecking ? SystemTypes.Type : (TypeNode)OptionalModifier.For(SystemTypes.NonNullType, SystemTypes.Type);
      }
      return t;
    }
    public override Statement VisitYield(Yield Yield){
      if (Yield == null) return null;
      Expression e = Yield.Expression = this.VisitExpression(Yield.Expression);
      if (e == null) return Yield;
      TypeNode eType = e.Type;
      if (eType == null) return Yield;
      TypeNode elemType = this.typeSystem.GetStreamElementType(eType, this.TypeViewer);
      if (eType is TupleType) elemType = eType;
      TypeNode rType = this.currentMethod.ReturnType;
      if (rType == null){ //REVIEW: what happens when rType is null because of an earlier error?
        this.currentMethod.ReturnType = 
          new StreamTypeExpression(new TypeUnionExpression(new TypeNodeList(elemType)));
      }else if (rType is StreamTypeExpression)
        ((TypeUnionExpression)((StreamTypeExpression)rType).ElementType).Types.Add(elemType);
      return Yield;
    }
    public override Field VisitField(Field field){
      this.currentField = field;
      this.currentFieldIsCircularConstant = false;
      bool savedCheckOverflow = this.typeSystem.checkOverflow;
      this.typeSystem.checkOverflow = true;
      field = base.VisitField(field);
      if (this.currentFieldIsCircularConstant) field.Initializer = null;
      this.currentField = null;
      this.typeSystem.checkOverflow = savedCheckOverflow;
      if (field == null) return null;
      if (field.IsLiteral && field.DefaultValue == null)
        field.DefaultValue = this.EvaluateAsLiteral(field.Initializer);
      this.InjectRedirectorMethods(field); //Do this now so that the interface methods do not show up as unimplemented
      Construct c = field.Initializer as Construct;
      if (c != null && c.Type == null)
        c.Type = field.Type;
      Identifier id = field.Initializer as Identifier;
      if (id != null && id.Type == null && field.Type!= null && field.Type.IsAssignableTo(SystemTypes.Enum))
        id.Type = field.Type;
      return field;
    }

    public virtual void InjectRedirectorMethods(Field f){
      TypeNode declaringType = f.DeclaringType;
      if (declaringType == null) return;
      InterfaceList interfaces = f.ImplementedInterfaces;
      if (interfaces == null) return;
      for (int i = 0, n = interfaces.Count; i < n; i++){
        Interface iface = interfaces[i];
        if (iface == null) continue;
        MemberList members = iface.Members;
        if (members == null) continue;
        for (int j = 0, m = members.Count; j < m; j++){
          Method imeth = members[j] as Method;
          if (imeth == null) continue;
          Method rmeth = new Method();
          rmeth.Flags = MethodFlags.Private|MethodFlags.HideBySig|MethodFlags.NewSlot|MethodFlags.Final|MethodFlags.Virtual;
          rmeth.CallingConvention = CallingConventionFlags.HasThis;
          rmeth.Name = Identifier.For(iface.Name.ToString()+"."+imeth.Name.ToString());
          rmeth.ReturnType = imeth.ReturnType;
          ParameterList parameters = rmeth.Parameters = imeth.Parameters == null ? null : imeth.Parameters.Clone();
          rmeth.ImplementedInterfaceMethods = new MethodList(imeth);
          int npars = parameters == null ? 0 : parameters.Count;
          ExpressionList args = new ExpressionList(npars);
          for (int k = 0; k < npars; k++){
            Parameter par = parameters[k];
            if (par == null) continue;
            parameters[k] = par = (Parameter)par.Clone();
            par.DeclaringMethod = rmeth;
            args.Add(par);
          }
          StatementList statements = new StatementList();
          MethodCall mcall = new MethodCall(new MemberBinding(new MemberBinding(rmeth.ThisParameter, f), imeth), args, NodeType.Callvirt, imeth.ReturnType);
          statements.Add(new Return(mcall));
          rmeth.Body = new Block(statements);
          rmeth.DeclaringType = declaringType;
          declaringType.Members.Add(rmeth);
        }
      }
      //TODO: give errors when no method to redirect to
    }
    public override Statement VisitForEach(ForEach forEach){
      if (forEach == null) return null;
      forEach.InductionVariable = this.VisitTargetExpression(forEach.InductionVariable);
      forEach.Invariants = this.VisitLoopInvariantList(forEach.Invariants);
      forEach.TargetVariableType = this.VisitTypeReference(forEach.TargetVariableType);
      forEach.TargetVariable = this.VisitTargetExpression(forEach.TargetVariable);
      Expression e = forEach.SourceEnumerable = this.VisitExpression(forEach.SourceEnumerable);
      if (e != null && e.NodeType == NodeType.AnonymousNestedFunction){
        AnonymousNestedFunction func = (AnonymousNestedFunction)e;
        Method invoker = func.Method;
        if (invoker != null){
          Expression ob = invoker.IsStatic ? null : new CurrentClosure(invoker, invoker.DeclaringType);
          e = new MethodCall(new MemberBinding(ob, invoker), null, NodeType.Call, invoker.ReturnType, e.SourceContext);
          func.Invocation = e;
          func.Type = invoker.ReturnType;
        }
      }
      if (forEach.TargetVariableType == null){
        MemberBinding mb = forEach.TargetVariable as MemberBinding;
        if (mb != null){
          Field f = mb.BoundMember as Field;
          if (f != null && e != null){
            TypeNode st = TypeNode.StripModifiers(e.Type); //HACK
            while (st is TypeAlias) st = TypeNode.StripModifiers(((TypeAlias)st).AliasedType);
            if (st != e.Type)
              f.Type = this.typeSystem.GetStreamElementType(st, this.TypeViewer);
            else
              f.Type = this.typeSystem.GetStreamElementType(e, this.TypeViewer);
            mb.Type = f.Type;
            forEach.TargetVariableType = f.Type;
          }
        }
      }
      forEach.Body = this.VisitBlock(forEach.Body);
      return forEach;
    }
    public override Statement VisitFunctionDeclaration(FunctionDeclaration fDecl){
      if (fDecl == null) return null;
      fDecl.Method = this.VisitMethod(fDecl.Method);
      return fDecl;
    }
    public override Expression VisitTemplateInstance(TemplateInstance instance){
      if (instance == null) return null;
      if (instance.IsMethodTemplate) return instance;
      return base.VisitTemplateInstance(instance);
    }
    public virtual Identifier GetUriNamespaceFor(Member member){
      return null;
    }
    // Query nodes
    public virtual TypeNode GetResultType(Expression source, TypeNode elementType, Cardinality card){
      if (source == null) return null;
      TypeNode stype = source.Type;
      if (stype == null) return null;
      if (elementType == null){
        if (source is Literal && stype == SystemTypes.Type){
          elementType = (TypeNode)((Literal)source).Value;
        }
        else{
          elementType = (stype is TupleType) ? stype : this.typeSystem.GetStreamElementType(source, this.TypeViewer);
        }
      }
      Cardinality resultCard = this.typeSystem.GetCardinalityOr(this.typeSystem.GetCardinality(source, this.TypeViewer), card);
      Cardinality elementCard = this.typeSystem.GetCardinality(elementType, this.TypeViewer);
      switch( resultCard ){
        case Cardinality.None:
          return elementType;
        case Cardinality.ZeroOrOne:
          if (this.typeSystem.GetCardinality(elementType, this.TypeViewer) == Cardinality.ZeroOrOne){
            return elementType;
          }
          else if(elementType.Template == SystemTypes.GenericNonNull){
            return this.typeSystem.GetStreamElementType(elementType, this.TypeViewer);
          }
          else{
            return SystemTypes.GenericBoxed.GetTemplateInstance(this.currentType, elementType);
          }
        default:
        case Cardinality.ZeroOrMore:
          return SystemTypes.GenericIEnumerable.GetTemplateInstance(this.currentType, elementType);
        case Cardinality.One:
          if (this.typeSystem.GetCardinality(elementType, this.TypeViewer) == Cardinality.One){
            return elementType;
          }
          else if (elementType.Template == SystemTypes.GenericBoxed){
            return this.typeSystem.GetStreamElementType(elementType, this.TypeViewer);
          }
          else{
            return SystemTypes.GenericNonNull.GetTemplateInstance(this.currentType, elementType);
          }
        case Cardinality.OneOrMore:
          return SystemTypes.GenericNonEmptyIEnumerable.GetTemplateInstance(this.currentType, elementType);
      }
    }
    public override Node VisitQueryAlias(QueryAlias alias){
      if (alias == null) return null;
      alias.Expression = this.VisitExpression(alias.Expression);
      alias.Type = alias.Expression.Type;
      return alias;
    } 
    public override Node VisitQueryAxis(QueryAxis axis){
      if (axis == null) return null;
      axis.Source = this.VisitExpression(axis.Source);
      return this.ResolveAxis(axis);
    }
    public virtual Expression ResolveAxis(QueryAxis axis){
      if (axis == null) return null;
      if (axis.Source == null) return axis;
      TypeNode stype = axis.Source.Type;
      bool isStatic = false;
      if (axis.Source is Literal && stype == SystemTypes.Type){
        stype = (TypeNode)((Literal)axis.Source).Value;
        isStatic = true;
      }
      if (stype == null) return axis;
      TypeNode elementType = (stype is TupleType) ? stype : this.typeSystem.GetStreamElementType(stype, this.TypeViewer);
      if (elementType == null) return null;
      Accessor acc = this.BuildAccessGraph(elementType, axis.IsDescendant, isStatic);
      axis.AccessPlan = this.ReduceAccessGraph(axis, acc);
      axis.YieldCount = 0;
      axis.YieldTypes = new TypeNodeList();
      axis.Cardinality = Cardinality.One;
      if (axis.AccessPlan != null){
        axis.Cardinality = this.AnalyzeAccessGraph(axis, axis.AccessPlan, elementType, new Hashtable());
        if (axis.TypeTest != null){
          axis.Type = this.GetResultType(axis.Source, axis.TypeTest, axis.Cardinality);
        }
        else{
          axis.YieldTypes = TypeUnion.Normalize(axis.YieldTypes);
          if (axis.YieldTypes.Count > 1){
            TypeNode resultElementType = TypeUnion.For(axis.YieldTypes, this.currentType);
            axis.Type = this.GetResultType(axis.Source, resultElementType, axis.Cardinality);
          }
          else if (axis.YieldCount > 0){
            axis.Type = this.GetResultType(axis.Source, axis.YieldTypes[0], axis.Cardinality);
          }
        }
        if (axis.YieldCount == 1 && !axis.IsIterative){
          Cardinality scard = this.typeSystem.GetCardinality(axis.Source, this.TypeViewer);
          Cardinality acard = this.typeSystem.GetCardinality(axis, this.TypeViewer);
          if ((scard == Cardinality.One || scard == Cardinality.None) &&
            (acard == Cardinality.One || acard == Cardinality.None || acard == Cardinality.ZeroOrOne)){
            Expression simple = this.GetSimpleAxis(axis.Source, axis.AccessPlan);
            if (simple != null) 
              return simple;
          }
        }
      }
      return axis;
    }
    private Expression GetSimpleAxis(Expression source, Accessor accessor){
      if (source == null || accessor == null) return null;
      MemberAccessor ma = accessor as MemberAccessor;
      if (ma != null){
        if (ma.Yield && ma.Next != null) return null; // multiple yields == not simple
        Field f = ma.Member as Field;
        if (f == null) return null;
        MemberBinding mb = null;
        if (source is Literal && source.Type == SystemTypes.Type){
          mb = new MemberBinding(null, f);
        }
        else{
          if (f.DeclaringType != source.Type) return null;
          mb = new MemberBinding(source, f);
        }
        mb.Type = f.Type;
        if (ma.Next != null) return this.GetSimpleAxis(mb, ma.Next);
        return mb;
      }
      return null;
    }
    public virtual bool Matches(MemberAccessor ma, QueryAxis axis){
      bool hasNameTest = !((axis.Name == null || axis.Name == Identifier.Empty) && (axis.Namespace == null || axis.Namespace == Identifier.Empty));
      bool hasTypeTest = axis.TypeTest != null;
      if (hasNameTest && !XmlHelper.Matches(ma.Member, axis.Name, axis.Namespace)) return false;
      if (hasTypeTest && !this.HasTypeMatch(ma.Type, axis.TypeTest)) return false;
      if (!hasNameTest && !hasTypeTest && this.IsTransparent(ma.Member)) return false;
      return true;
    }

    public virtual bool HasTypeMatch(TypeNode from, TypeNode to){
      if (this.typeSystem.ImplicitCoercionFromTo(from, to, this.TypeViewer)) return true;
      for (TypeAlias ta = from as TypeAlias; ta != null; from = ta.AliasedType, ta = from as TypeAlias){};
      TypeUnion tu = from as TypeUnion;
      if (tu != null){
        for (int i = 0, n = tu.Types.Count; i < n; i++){
          if (this.typeSystem.ImplicitCoercionFromTo(tu.Types[i], to, this.TypeViewer)) return true;
        }
      }
      return false;
    }
    private Cardinality AnalyzeAccessGraph(QueryAxis axis, Accessor accessor, TypeNode type, Hashtable visited){
      if (accessor != null){
        if (visited[accessor] != null){
          axis.IsCyclic = true;
          return this.typeSystem.GetCardinality(type, this.TypeViewer);
        }
        visited[accessor] = true;
        SwitchAccessor swa = accessor as SwitchAccessor;
        if (swa != null && swa.Accessors != null && swa.Accessors.Count > 0){
          ArrayList accessors = new ArrayList(swa.Accessors.Values);
          Cardinality c = this.AnalyzeAccessGraph(axis, (Accessor)accessors[0], swa.Type.Types[0], visited);
          for (int i = 1, n = accessors.Count; i < n; i++){
            c = this.typeSystem.GetCardinalityOr(c, this.AnalyzeAccessGraph(axis, (Accessor)accessors[i], swa.Type.Types[i], visited));
          }
          if (swa.Accessors.Count < swa.Type.Types.Count){
            c = this.typeSystem.GetCardinalityOr(c, Cardinality.ZeroOrOne);
          }
          return c;
        }
        SequenceAccessor sqa = accessor as SequenceAccessor;
        if (sqa != null && sqa.Accessors != null && sqa.Accessors.Count > 0){
          Cardinality c = this.AnalyzeAccessGraph(axis, (Accessor)sqa.Accessors[0], type, visited);
          for (int i = 1, n = sqa.Accessors.Count; i < n; i++){
            c = this.typeSystem.GetCardinalityAnd(c, this.AnalyzeAccessGraph(axis, (Accessor)sqa.Accessors[i], type, visited));
          }
          return c;
        }
        MemberAccessor ma = accessor as MemberAccessor;
        if (ma != null){
          if (ma.Yield){
            axis.YieldCount++;
            axis.YieldTypes.Add(ma.Type);
          }
          TypeNode mt = this.typeSystem.GetMemberType(ma.Member);
          Cardinality c = this.typeSystem.GetCardinality(mt, this.TypeViewer);
          if (c == Cardinality.OneOrMore || c == Cardinality.ZeroOrMore){
            axis.IsIterative = true; // member refers to a collection
          }
          if (ma.Next != null){
            c = this.typeSystem.GetCardinalityOr(c, this.AnalyzeAccessGraph(axis, ma.Next, ma.Type, visited));
          }
          return c;
        }
      }
      return Cardinality.None;
    }
    private Accessor ReduceAccessGraph(QueryAxis axis, Accessor accessor){
      return this.ReduceAccessGraph(axis, accessor, new Hashtable());
    }
    private Accessor ReduceAccessGraph(QueryAxis axis, Accessor accessor, Hashtable visited){
      if (accessor == null) return null;
      if (visited[accessor] != null) return accessor;
      visited[accessor] = true;
      if (!this.AnyPathMatches(axis, accessor)) return null;
      SwitchAccessor swa = accessor as SwitchAccessor;
      if (swa != null && swa.Accessors != null && swa.Accessors.Count > 0){
        ArrayList list = new ArrayList(swa.Accessors.Keys);
        foreach( object key in list ){
          Accessor acc = (Accessor) swa.Accessors[key];
          acc = this.ReduceAccessGraph(axis, acc, visited);
          if (acc != null){
            swa.Accessors[key] = acc;
          }
          else{
            swa.Accessors.Remove(key);
          }
        }
        if (swa.Accessors.Count == 0) return null;
        return swa;
      }
      SequenceAccessor sqa = accessor as SequenceAccessor;
      if (sqa != null && sqa.Accessors != null && sqa.Accessors.Count > 0){
        for( int i = sqa.Accessors.Count - 1; i >= 0; i-- ){
          Accessor acc = (Accessor) sqa.Accessors[i];
          if (this.ReduceAccessGraph(axis, acc, visited) == null){
            sqa.Accessors.RemoveAt(i);
          }
        }
        if (sqa.Accessors.Count == 0) return null;
        if (sqa.Accessors.Count == 1) return (Accessor) sqa.Accessors[0];
        return sqa;
      }
      MemberAccessor ma = accessor as MemberAccessor;
      if (ma != null){
        ma.Yield = this.Matches(ma, axis);
        ma.Next = this.ReduceAccessGraph(axis, ma.Next, visited);
        return ma;
      }
      return null;
    }
    private bool AnyPathMatches(QueryAxis axis, Accessor accessor){
      return this.AnyPathMatches( axis, accessor, new Hashtable() );
    }
    private bool AnyPathMatches(QueryAxis axis, Accessor accessor, Hashtable visited){
      if (accessor == null || visited[accessor] != null) return false;
      visited[accessor] = true;
      SwitchAccessor swa = accessor as SwitchAccessor;
      if (swa != null){
        foreach( Accessor acc in swa.Accessors.Values ){
          if (this.AnyPathMatches(axis, acc, visited)) return true;
        }
        return false;
      }
      SequenceAccessor sqa = accessor as SequenceAccessor;
      if (sqa != null){
        foreach( Accessor acc in sqa.Accessors ){
          if (this.AnyPathMatches(axis, acc, visited)) return true;
        }
        return false;
      }
      MemberAccessor ma = accessor as MemberAccessor;
      if (ma != null){
        if (this.Matches(ma, axis)) return true;
        if (ma.Next != null) return this.AnyPathMatches(axis, ma.Next, visited);
        return false;
      }
      return false;
    }
    private Accessor BuildAccessGraph(TypeNode type, bool deep, bool isStatic){
      return this.BuildAccessGraph(type, deep, isStatic, new Hashtable());
    }
    private Accessor BuildAccessGraph(TypeNode type, bool deep, bool isStatic, Hashtable visited){
      Accessor acc = (Accessor) visited[type];
      if (acc != null) return acc;
      TypeUnion tu = type as TypeUnion;
      if (tu != null){
        SwitchAccessor sw = new SwitchAccessor();
        visited[type] = sw;
        sw.Type = tu;
        for( int i = 0, n = tu.Types.Count; i < n; i++ ){
          TypeNode tn = this.typeSystem.GetRootType(tu.Types[i], this.TypeViewer);
          if (tn == null) continue;
          sw.Accessors.Add(i, this.BuildAccessGraph(tn, deep, isStatic, visited));
        }
        return sw;
      }
      // queries don't see through intersection types, 
      // assume intersection type is a derived class w/ all private members
      TypeIntersection ti = type as TypeIntersection;
      if (ti != null){
        return null;
      }
      // examine members
      SequenceAccessor accessor = new SequenceAccessor();
      visited[type] = accessor;
      this.BuildMemberAccess(type, accessor, deep, isStatic, visited);
      return accessor;
    }
    public virtual bool IsTransparent(Member m){
      TypeNode rootType = this.typeSystem.GetRootType(this.typeSystem.GetMemberType(m), this.TypeViewer);
      return m.Anonymity == Anonymity.Full || (m.Anonymity == Anonymity.Structural && this.typeSystem.IsStructural(rootType));
    }
    private void BuildMemberAccess(TypeNode type, SequenceAccessor acc, bool deep, bool isStatic, Hashtable visited){
      TypeAlias ta = type as TypeAlias;
      if (ta != null) type = ta.AliasedType;
      if (type.BaseType != null && type.BaseType != SystemTypes.Object){
        this.BuildMemberAccess(type.BaseType, acc, deep, isStatic, visited);
      }
      MemberList members = type.Members;
      for( int i = 0, n = members.Count; i < n; i++ ){
        Member m = members[i];
        if (m == null) continue;
        if (!(m is Property || m is Field)) continue;
        if (!m.IsPublic) continue;
        if (m.IsStatic != isStatic) continue;
        // don't add additional references to overridden members
        if (m.OverridesBaseClassMember) continue; 
        // hidden members are not visible, remove hidden ones
        if (m.HidesBaseClassMember){
          for( int j = acc.Accessors.Count - 1; j >= 0; j-- ){
            MemberAccessor mac = (MemberAccessor)acc.Accessors[j];
            if (mac.Member.Name.Name == m.Name.Name){
              acc.Accessors.RemoveAt(j);
            }
          }
        }
        Property p = m as Property;
        if (p != null && p.Parameters != null && p.Parameters.Count > 0) continue;
        MemberAccessor ma = new MemberAccessor(m);
        TypeNode mtype = this.typeSystem.GetMemberType(ma.Member);
        ma.Type = this.typeSystem.GetRootType(mtype, this.TypeViewer);
        if (ma.Type != null && !ma.Type.IsPrimitive){
          if (deep || this.IsTransparent(ma.Member)){
            ma.Next = this.BuildAccessGraph(ma.Type, deep, isStatic, visited);
          }          
        }
        acc.Accessors.Add(ma);
      }
    }
    public override Node VisitQueryContext(QueryContext context){
      this.hasContextReference = true;
      if (context.Scope == null) 
        context.Scope = this.contextScope;
      if (context.Scope != null) 
        context.Type = context.Scope.Type;
      return context;
    }    
    public override Node VisitQueryDelete(QueryDelete delete){
      if (delete == null) return null;
      delete.Source = this.VisitExpression(delete.Source);
      if (delete.Source == null || delete.Source.Type == null) return delete;
      TypeNode sourceElementType = this.typeSystem.GetStreamElementType(delete.Source, this.TypeViewer);
      delete.Context = this.contextScope = new ContextScope(this.contextScope, sourceElementType);
      delete.Target = this.VisitExpression(delete.Target);
      this.contextScope = this.contextScope.Previous;
      delete.Type = SystemTypes.Int32;
      return delete;
    }
    public override Node VisitQueryDistinct(QueryDistinct qd){
      if (qd == null) return null;
      qd.Context = this.contextScope;
      if (qd.Group == null){
        qd.Source = this.VisitExpression(qd.Source);
        if (qd.Source != null && qd.Source.Type != null){
          qd.Type = this.GetResultType(qd.Source, null, Cardinality.ZeroOrMore);
        }
      }
      return qd;
    }
    public override Node VisitQueryDifference(QueryDifference diff){
      if (diff == null) return null;
      diff.LeftSource = this.VisitExpression(diff.LeftSource);
      diff.RightSource = this.VisitExpression(diff.RightSource);
      if (diff.LeftSource != null && diff.RightSource != null &&
        diff.LeftSource.Type != null && diff.RightSource.Type != null ){
        diff.Type = diff.LeftSource.Type; // hack for now
      }
      return diff;
    }
    public override Node VisitQueryExists(QueryExists exists){
      if (exists == null) return null;
      exists.Source = this.VisitExpression(exists.Source);
      if (exists.Source != null && exists.Source.Type != null){
        exists.Type = SystemTypes.Boolean;
      }
      return exists;
    }
    public override Node VisitQueryFilter(QueryFilter filter){
      if (filter == null) return null;
      filter.Source = this.VisitExpression(filter.Source);
      if (filter.Source == null || filter.Source.Type == null) return filter;
      TypeNode sourceElementType = this.typeSystem.GetStreamElementType(filter.Source, this.TypeViewer);
      if (sourceElementType != null){
        filter.Context = this.contextScope = new ContextScope(this.contextScope, sourceElementType);
        filter.Expression = this.VisitExpression(filter.Expression);
        this.contextScope = this.contextScope.Previous;
        if (filter.Expression != null && filter.Expression.Type != null){
          filter.Type = this.GetResultType(filter.Source, null, Cardinality.ZeroOrOne);
        }
      }
      return filter;
    }    
    public virtual TypeNode GetAggregateSubType(TypeNode type, TypeNode elementType){
      if (type == null || elementType == null) return null;
      if (this.GetTypeView(type).IsAssignableTo(SystemTypes.IAggregate)){
        return type;
      }
      else if (this.GetTypeView(type).IsAssignableTo(SystemTypes.IAggregateGroup)){
        MemberList members = this.GetTypeView(type).Members;
        if (members == null || members.Count == 0) return null;
        TypeNode anAggregate = null;
        TypeNode okayAggregate = null;
        for( int i = 0, n = members.Count; i < n; i++ ){
          type = members[i] as TypeNode;
          if (type == null) continue;
          if (!type.IsPublic || !(type is Class || type is Struct)) continue;
          if (!this.GetTypeView(type).IsAssignableTo(SystemTypes.IAggregate)) continue;
          anAggregate = type;
          MemberList adds = this.GetTypeView(type).GetMembersNamed(StandardIds.Add);
          if (adds.Count != 1) continue;
          Method m = adds[0] as Method;
          if (m == null) continue;
          if (m.Parameters == null || m.Parameters.Count != 1) continue;
          TypeNode ptype = m.Parameters[0].Type;
          if (elementType != null){
            if (elementType.UniqueKey == ptype.UniqueKey) return type; // best match
            if (okayAggregate == null && this.typeSystem.ImplicitCoercionFromTo(elementType, ptype, this.TypeViewer)){
              okayAggregate = type;
            }
          }
        }
        if (okayAggregate != null) return okayAggregate;
        if (anAggregate != null) return anAggregate;
      }
      return null;
    }
    public override Node VisitQueryAggregate(QueryAggregate qa){
      if (qa == null) return null;
      qa.Context = this.contextScope;
      if (qa.Group == null){
        qa.Expression = this.VisitExpression(qa.Expression);
        TypeNode inputType = this.typeSystem.GetStreamElementType(qa.Expression, this.TypeViewer);
        TypeNode aggType = this.GetAggregateSubType(qa.AggregateType, inputType);
        if (aggType == null) return qa;
        qa.AggregateType = aggType;
        Method mgetval = this.GetTypeView(aggType).GetMethod(StandardIds.GetValue);
        if (mgetval != null){
          qa.Type = mgetval.ReturnType;
        }
      }
      return qa;
    }
    public override Node VisitQueryGroupBy(QueryGroupBy groupby){
      if (groupby == null) return null;
      groupby.Source = this.VisitExpression(groupby.Source);
      if (groupby.Source == null || groupby.Source.Type == null) return groupby;
      TypeNode sourceElementType = this.typeSystem.GetStreamElementType(groupby.Source, this.TypeViewer);
      groupby.GroupContext = this.contextScope = new ContextScope(this.contextScope, sourceElementType);
      groupby.GroupList = this.VisitExpressionList(groupby.GroupList);
      this.contextScope = this.contextScope.Previous;
      // create result type
      FieldList fields = new FieldList();
      int cn = 0;
      for (int i = 0, n = groupby.GroupList.Count; i < n; i++){
        Expression x = groupby.GroupList[i];
        if (x != null && x.Type != null){
          Identifier name = this.GetExpressionName(x);
          Field f = new Field(null, new AttributeList(1), FieldFlags.Public, name, x.Type, null);
          if (name == null || name == Identifier.Empty){
            f.Name = Identifier.For("Item"+cn); cn++;
            f.Attributes.Add(new AttributeNode(new MemberBinding(null, SystemTypes.AnonymousAttribute.GetConstructor()), null));
          }
          fields.Add(f);
        }
      }
      for (int i = 0, n = groupby.AggregateList.Count; i < n; i++){
        QueryAggregate qa = groupby.AggregateList[i] as QueryAggregate;
        if (qa != null){
          qa.Group = groupby;
          this.contextScope = groupby.GroupContext;
          TypeNode aggType = null;
          QueryDistinct qd = qa.Expression as QueryDistinct;
          if (qd != null){
            qd.Group = groupby;
            qd.Source = this.VisitExpression(qd.Source);
            if (qd.Source == null) continue;
            qd.Type = this.GetResultType(qd.Source, null, Cardinality.ZeroOrMore);
            aggType = this.GetAggregateSubType(qa.AggregateType, this.typeSystem.GetStreamElementType(qd.Source, this.TypeViewer));
          }
          else{
            qa.Expression = this.VisitExpression(qa.Expression);
            if (qa.Expression == null) continue;
            aggType = this.GetAggregateSubType(qa.AggregateType, this.typeSystem.GetStreamElementType(qa.Expression, this.TypeViewer));
          }
          this.contextScope = this.contextScope.Previous;
          if (aggType == null) continue;
          qa.AggregateType = aggType;
          Method mgetval = this.GetTypeView(aggType).GetMethod(StandardIds.GetValue);
          if (mgetval != null){
            qa.Type = mgetval.ReturnType;
          }
          Identifier name = Identifier.For("Item"+cn); cn++;
          Field f = new Field(null, new AttributeList(1), FieldFlags.Public, name, qa.Type, null);
          f.Attributes.Add(new AttributeNode(new MemberBinding(null, SystemTypes.AnonymousAttribute.GetConstructor()), null));
          fields.Add(f);
        }
      }
      if (fields.Count == groupby.GroupList.Count + groupby.AggregateList.Count){
        TypeNode groupType = TupleType.For(fields, this.currentType);
        if (groupby.Having != null){
          groupby.HavingContext = this.contextScope = new ContextScope(this.contextScope, groupType);
          groupby.Having = this.VisitExpression(groupby.Having);
          this.contextScope = this.contextScope.Previous;
        }
        groupby.Type = this.GetResultType(groupby.Source, groupType, Cardinality.One);
      }
      return groupby;
    }
    public override Node VisitQueryInsert(QueryInsert insert){
      if (insert == null) return null;
      insert.Location = this.VisitExpression(insert.Location);
      if (insert.Location == null || insert.Location.Type == null) return insert;
      TypeNode sourceElementType = this.typeSystem.GetStreamElementType(insert.Location, this.TypeViewer);
      insert.Context = this.contextScope = new ContextScope(this.contextScope, sourceElementType);
      this.VisitExpressionList(insert.InsertList);
      this.contextScope = this.contextScope.Previous;
      insert.Type = SystemTypes.Int32;
      insert.HintList = this.VisitExpressionList(insert.HintList);        
      return insert;
    }    
    public override Node VisitQueryIntersection(QueryIntersection intersection){
      if (intersection == null) return null;
      intersection.LeftSource = this.VisitExpression(intersection.LeftSource);
      intersection.RightSource = this.VisitExpression(intersection.RightSource);
      if (intersection.LeftSource != null && intersection.RightSource != null &&
        intersection.LeftSource.Type != null && intersection.RightSource.Type != null){
        intersection.Type = intersection.LeftSource.Type;
      }
      return intersection;
    }
    private static readonly Identifier idAnonymity = Identifier.For("Anonymity");
    private Field NewField(Identifier name, TypeNode type, Anonymity anon){
      AttributeList attrs = null;
      if (anon != Anonymity.Unknown && anon != Anonymity.None){
        attrs = new AttributeList(1);
        MemberBinding cons = new MemberBinding(null, SystemTypes.AnonymousAttribute.GetConstructor());
        TypeNode tn = SystemTypes.AnonymityEnum;
        AttributeNode attr = new AttributeNode(cons, new ExpressionList(new NamedArgument(idAnonymity, new Literal(anon, SystemTypes.AnonymityEnum))));
        attrs.Add(attr);
      }
      return new Field(null, attrs, FieldFlags.Public, name, type, null);
    }
    public override Node VisitQueryIterator(QueryIterator xiterator){
      if (xiterator == null) return null;
      xiterator.Expression = this.VisitExpression(xiterator.Expression);
      if (xiterator.Expression != null && xiterator.Expression.Type != null){
        if (xiterator.ElementType == null){
          xiterator.ElementType = this.typeSystem.GetStreamElementType(xiterator.Expression, this.TypeViewer);
        }
        if (xiterator.Name == null) xiterator.Name = Identifier.For("Item0");
        xiterator.ElementType = TupleType.For(new FieldList(this.NewField(xiterator.Name, xiterator.ElementType, Anonymity.Full)), this.currentType);
        xiterator.Type = this.GetResultType(xiterator.Expression, xiterator.ElementType, Cardinality.One);
        xiterator.HintList = this.VisitExpressionList(xiterator.HintList);        
      }
      return xiterator;      
    }    
    public override Node VisitQueryJoin(QueryJoin join){
      if (join == null) return null;
      join.LeftOperand = this.VisitExpression(join.LeftOperand);
      join.RightOperand = this.VisitExpression(join.RightOperand);
      TypeNode leftType = this.typeSystem.GetStreamElementType(join.LeftOperand, this.TypeViewer);
      TypeNode rightType = this.typeSystem.GetStreamElementType(join.RightOperand, this.TypeViewer);
      if (join.LeftOperand != null && leftType != null && join.RightOperand != null && rightType != null){
        if (join.JoinType == QueryJoinType.RightOuter || join.JoinType == QueryJoinType.FullOuter){
          leftType = SystemTypes.GenericBoxed.GetTemplateInstance(this.currentType, leftType);
        }
        if (join.JoinType == QueryJoinType.LeftOuter || join.JoinType == QueryJoinType.FullOuter){
          rightType = SystemTypes.GenericBoxed.GetTemplateInstance(this.currentType, rightType);
        }
        Field leftField = this.NewField(Identifier.For("Item0"), leftType, Anonymity.Full);
        Field rightField = this.NewField(Identifier.For("Item1"), rightType, Anonymity.Full);
        TypeNode joinedType = TupleType.For(new FieldList(leftField, rightField), this.currentType);
        if (join.JoinExpression != null){
          join.JoinContext = this.contextScope = new ContextScope(this.contextScope, joinedType);
          join.JoinExpression = this.VisitExpression(join.JoinExpression);
          this.contextScope = this.contextScope.Previous;
        }
        join.Type = SystemTypes.GenericIEnumerable.GetTemplateInstance(this.currentType, joinedType);
      }
      return join;
    } 
    public override Node VisitQueryLimit(QueryLimit limit){
      if (limit == null) return null;
      limit.Source = this.VisitExpression(limit.Source);
      if (limit.Source != null && limit.Source.Type != null){
        limit.Expression = this.VisitExpression(limit.Expression);
        if (limit.Expression != null && limit.Expression.Type != null){
          limit.Type = this.GetResultType(limit.Source, null, Cardinality.One);
        }
      }
      return limit;
    }
    public override Node VisitQueryOrderBy(QueryOrderBy orderby){
      if (orderby == null) return null;
      orderby.Source = this.VisitExpression(orderby.Source);
      if (orderby.Source == null || orderby.Source.Type == null) return orderby;
      TypeNode sourceElementType = this.typeSystem.GetStreamElementType(orderby.Source, this.TypeViewer);
      orderby.Context = this.contextScope = new ContextScope(this.contextScope, sourceElementType);
      orderby.OrderList = this.VisitExpressionList(orderby.OrderList);
      this.contextScope = this.contextScope.Previous;
      orderby.Type = this.GetResultType(orderby.Source, null, Cardinality.One);
      return orderby;
    }    
    public override Node VisitQueryOrderItem(QueryOrderItem item){
      if (item == null) return null;
      item.Expression = this.VisitExpression(item.Expression);
      if (item.Expression != null && item.Expression.Type != null){
        item.Type = item.Expression.Type;
      }
      return item;
    }    
    public override Node VisitQueryPosition(QueryPosition position){
      this.hasContextReference = true;
      return position;
    }
    public override Node VisitQueryProject(QueryProject project){
      if (project == null) return null;
      project.Source = this.VisitExpression(project.Source);
      if (project.Source == null || project.Source.Type == null) return project;
      TypeNode sourceElementType = this.typeSystem.GetStreamElementType(project.Source, this.TypeViewer);
      project.Context = this.contextScope = new ContextScope(this.contextScope, sourceElementType);
      ExpressionList list = new ExpressionList();
      for (int i=0; i < project.ProjectionList.Count; i++){
        QueryAxis axis = project.ProjectionList[i] as QueryAxis;
        if (axis != null && axis.Name == Identifier.Empty){
          axis.Source = this.VisitExpression(axis.Source);
          this.ResolveAxis(axis);
          this.GetProjectionList(axis.AccessPlan, axis.Source, list);
        }
        else{
          list.Add(this.VisitExpression(project.ProjectionList[i]));
        }
      }      
      project.ProjectionList = list;
      this.contextScope = this.contextScope.Previous;
      if (project.ProjectedType == null){
        int len = project.ProjectionList.Count;
        if (len == 1 && this.GetExpressionName(project.ProjectionList[0]) == null){
          Expression x = project.ProjectionList[0];
          if (x != null && x.Type != null) 
            project.ProjectedType = x.Type;
        }
        else{
          FieldList fields = new FieldList();
          for( int i = 0, cn = 0; i < len; i++ ){
            Expression x = project.ProjectionList[i];
            if (x != null && x.Type != null){
              Identifier name = this.GetExpressionName(x);
              Field f = new Field(null, new AttributeList(1), FieldFlags.Public, name, x.Type, null);
              if (name == null || name == Identifier.Empty){
                f.Name = Identifier.For("Item"+cn); cn++;
                f.Attributes.Add(new AttributeNode(new MemberBinding(null, SystemTypes.AnonymousAttribute.GetConstructor()), null));
              }
              fields.Add(f);
            }
          }
          if (fields.Count == len){
            project.ProjectedType = TupleType.For(fields, this.currentType);
            project.Members = this.typeSystem.GetDataMembers(project.ProjectedType);
          }
        }
      }
      if (project.ProjectedType != null){
        project.Type = this.GetResultType(project.Source, project.ProjectedType, Cardinality.One);
      }
      return project;
    }
    public virtual void GetProjectionList(Accessor accessor, Expression source, ExpressionList list){
      if (accessor == null) return;
      MemberAccessor ma = accessor as MemberAccessor;
      if (ma != null){
        MemberBinding mb = new MemberBinding(source, ma.Member);
        mb.SourceContext = source.SourceContext;
        if (ma.Yield){
          list.Add(mb);
        }
        if (ma.Next != null){
          this.GetProjectionList(ma.Next, mb, list);
        }
        return;
      }
      SequenceAccessor sa = accessor as SequenceAccessor;
      if (sa != null){
        foreach( Accessor acc in sa.Accessors ){
          this.GetProjectionList(acc, source, list);
        }
        return;
      }
      this.HandleError(source, Error.QueryProjectThroughTypeUnion);
    }
    public virtual Identifier GetExpressionName(Expression x){
      if (x == null) return null;
      switch (x.NodeType){
        case NodeType.MemberBinding: 
          return ((MemberBinding)x).BoundMember.Name;
        case NodeType.QueryAlias:
          return ((QueryAlias)x).Name;
        case NodeType.QueryAxis:
          return ((QueryAxis)x).Name;
        default:
          return null;
      }      
    }
    public override Node VisitQueryQuantifier(QueryQuantifier qq){
      if (qq == null) return null;
      qq.Expression = this.VisitExpression(qq.Expression);
      if (qq.Expression != null && qq.Expression.Type != null){
        qq.Type = this.typeSystem.GetStreamElementType(qq.Expression, this.TypeViewer);
      }
      return qq;
    }
    public override Node VisitQueryQuantifiedExpression(QueryQuantifiedExpression qqe){
      if (qqe == null) return null;
      qqe.Expression = this.VisitExpression(qqe.Expression);
      if (qqe.Expression != null && qqe.Expression.Type != null){
        qqe.Type = qqe.Expression.Type;
      }
      return qqe;
    }
    public override Node VisitQuerySelect( QuerySelect select ){
      if (select == null) return null;
      select.Source = (Expression) this.Visit(select.Source);
      if (select.Source != null && select.Source.Type != null){
        select.Type = select.Source.Type;
      }
      return select;
    }
    public override Node VisitQuerySingleton( QuerySingleton singleton ){
      if (singleton == null) return null;
      singleton.Source = (Expression) this.Visit(singleton.Source);
      if (singleton.Source != null && singleton.Source.Type != null){
        singleton.Type = this.typeSystem.GetStreamElementType(singleton.Source, this.TypeViewer);
      }
      return singleton;
    }
    public override Node VisitQueryTypeFilter( QueryTypeFilter qtf ){
      if (qtf == null) return null;
      qtf.Source = this.VisitExpression(qtf.Source);
      return qtf;
    }    
    public override Node VisitQueryUnion( QueryUnion union ){
      if (union == null) return null;
      union.LeftSource = this.VisitExpression(union.LeftSource);
      union.RightSource = this.VisitExpression(union.RightSource);
      if (union.LeftSource != null && union.RightSource != null &&
        union.LeftSource.Type != null && union.RightSource.Type != null ){
        union.Type = union.LeftSource.Type; // hack for now
      }
      return union;
    }
    public override Node VisitQueryUpdate( QueryUpdate update ){
      if (update == null) return null;
      update.Source = this.VisitExpression(update.Source);
      if (update.Source == null || update.Source.Type == null) return update;
      TypeNode sourceElementType = this.typeSystem.GetStreamElementType(update.Source, this.TypeViewer);
      update.Context = this.contextScope = new ContextScope(this.contextScope, sourceElementType);
      update.UpdateList = this.VisitExpressionList(update.UpdateList);
      this.contextScope = this.contextScope.Previous;
      update.Type = SystemTypes.Int32;
      return update;
    }

    public virtual Expression OnInvalidNameBinding(NameBinding nb, NameBinding nameBinding){
      return nameBinding;
    }

    public virtual Expression OnInvalidQualifiedIdentifier(QualifiedIdentifier qualifiedIdentifier, Identifier id){
      return qualifiedIdentifier;
    }
    public virtual Expression OnInvalidMemberBinding(MemberBinding mb, QualifiedIdentifier qualId){
      return mb;
    }    
    public override Statement VisitAssertion(Assertion assertion){
      bool savedInsideAssertion = this.insideAssertion;
      this.insideAssertion = true;
      try{
        return base.VisitAssertion(assertion);
      }finally{
        this.insideAssertion = savedInsideAssertion;
      }
    }
    public override Statement VisitAssumption(Assumption assumption){
      bool savedInsideAssertion = this.insideAssertion;
      this.insideAssertion = true;
      try{
        return base.VisitAssumption(assumption);
      }finally{
        this.insideAssertion = savedInsideAssertion;
      }
    }
    public override MethodContract VisitMethodContract(MethodContract contract){
      bool savedInsideAssertion = this.insideAssertion;
      this.insideAssertion = true;
      try{
        return base.VisitMethodContract(contract);
      }finally{
        this.insideAssertion = savedInsideAssertion;
      }
    }
    public override ExpressionList VisitLoopInvariantList(ExpressionList expressions){
      bool savedInsideAssertion = this.insideAssertion;
      this.insideAssertion = true;
      try{
        return base.VisitLoopInvariantList(expressions);
      }finally{
        this.insideAssertion = savedInsideAssertion;
      }
    }
    public override Statement VisitSwitch(Switch Switch) {
      if (Switch == null)
        return Switch;
      Switch prevSwitch = this.currentSwitch;
      this.currentSwitch = Switch;
      Switch retSwitch = (Switch)base.VisitSwitch(Switch);
      this.currentSwitch = prevSwitch;
      return retSwitch;
    }
    public override SwitchCase VisitSwitchCase(SwitchCase switchCase) {
      SwitchCase retSwitchCase = base.VisitSwitchCase(switchCase);
      if (retSwitchCase == null || retSwitchCase.Label == null) return retSwitchCase;
      TypeNode t = null;
      Identifier id = retSwitchCase.Label as Identifier;
      if (this.currentSwitch != null && this.currentSwitch.Expression != null) t = this.currentSwitch.Expression.Type;
      if (id == null || id.Type != null || t == null)
        return retSwitchCase;
      if (t.IsAssignableTo(SystemTypes.Enum))
        id.Type = t;
      return retSwitchCase;
    }
  }
  public class ResolvedReferenceVisitor : StandardVisitor{
    public delegate void ResolvedReferenceVisitMethod(Member resolvedReference, Node reference);
    public ResolvedReferenceVisitMethod VisitResolvedReference;

    public ResolvedReferenceVisitor(ResolvedReferenceVisitMethod visitResolvedReference){
      this.VisitResolvedReference = visitResolvedReference;
    }

    public override AliasDefinition VisitAliasDefinition(AliasDefinition aliasDefinition){
      if (aliasDefinition == null) return null;
      this.VisitResolvedTypeReference(aliasDefinition.AliasedType);
      return base.VisitAliasDefinition (aliasDefinition);
    }
    public override Statement VisitCatch(Catch Catch){
      if (Catch == null) return null;
      this.VisitResolvedTypeReference(Catch.Type, Catch.TypeExpression);
      return base.VisitCatch(Catch);
    }
    public override Class VisitClass(Class Class){
      if (Class == null) return null;
      this.VisitResolvedTypeReference(Class.BaseClass, Class.BaseClassExpression);
      return base.VisitClass(Class);
    }
    public override Expression VisitConstructArray(ConstructArray consArr){
      if (consArr == null) return null;
      this.VisitResolvedTypeReference(consArr.ElementType, consArr.ElementTypeExpression);
      return base.VisitConstructArray(consArr);
    }
    public override Expression VisitConstructDelegate(ConstructDelegate consDelegate){
      if (consDelegate == null) return null;
      this.VisitResolvedTypeReference(consDelegate.DelegateType, consDelegate.DelegateTypeExpression);
      return base.VisitConstructDelegate(consDelegate);
    }
    public override Expression VisitConstructFlexArray(ConstructFlexArray consArr){
      if (consArr == null) return null;
      this.VisitResolvedTypeReference(consArr.ElementType, consArr.ElementTypeExpression);
      return base.VisitConstructFlexArray(consArr);
    }
    public override TypeNode VisitConstrainedType(ConstrainedType cType){
      if (cType == null) return null;
      this.VisitResolvedTypeReference(cType.UnderlyingType, cType.UnderlyingTypeExpression);
      return base.VisitConstrainedType(cType);
    }
    public override DelegateNode VisitDelegateNode(DelegateNode delegateNode){
      if (delegateNode == null) return null;
      this.VisitResolvedTypeReference(delegateNode.ReturnType, delegateNode.ReturnTypeExpression);
      return base.VisitDelegateNode(delegateNode);
    }
    public override Event VisitEvent(Event evnt){
      if (evnt == null) return null;
      this.VisitResolvedTypeReference(evnt.HandlerType, evnt.HandlerTypeExpression);
      return base.VisitEvent(evnt);
    }
    public override EnsuresExceptional VisitEnsuresExceptional(EnsuresExceptional exceptional){
      if (exceptional == null) return null;
      this.VisitResolvedTypeReference(exceptional.Type, exceptional.TypeExpression);
      return base.VisitEnsuresExceptional(exceptional);
    }
    public override Field VisitField(Field field){
      if (field == null) return null;
      this.VisitResolvedTypeReference(field.Type, field.TypeExpression);
      this.VisitResolvedInterfaceReferenceList(field.ImplementedInterfaces, field.ImplementedInterfaceExpressions);
      return base.VisitField(field);
    }
    public override Statement VisitForEach(ForEach forEach){
      if (forEach == null) return null;
      this.VisitResolvedTypeReference(forEach.TargetVariableType, forEach.TargetVariableTypeExpression);
      return base.VisitForEach(forEach);
    }
    public override Statement VisitFunctionDeclaration(FunctionDeclaration functionDeclaration){
      if (functionDeclaration == null) return null;
      this.VisitResolvedTypeReference(functionDeclaration.ReturnType, functionDeclaration.ReturnTypeExpression);
      return base.VisitFunctionDeclaration(functionDeclaration);
    }
    public override Expression VisitLocal(Local local){
      if (local == null) return null;
      this.VisitResolvedTypeReference(local.Type, local.TypeExpression);
      return base.VisitLocal(local);
    }
    public override Statement VisitLocalDeclarationsStatement(LocalDeclarationsStatement localDeclarations){
      if (localDeclarations == null) return null;
      this.VisitResolvedTypeReference(localDeclarations.Type, localDeclarations.TypeExpression);
      return base.VisitLocalDeclarationsStatement(localDeclarations);
    }
    public override Expression VisitMemberBinding(MemberBinding memberBinding){
      if (memberBinding == null) return null;
      Member member = memberBinding.BoundMember;
      if (member == null) return memberBinding;
      Method method = member as Method;
      if (method != null && method.Template != null && memberBinding.BoundMemberExpression is TemplateInstance){
        this.VisitResolvedReference(method, ((TemplateInstance)memberBinding.BoundMemberExpression).Expression);
        TypeNodeList templateArguments = ((TemplateInstance)memberBinding.BoundMemberExpression).TypeArgumentExpressions;
        this.VisitResolvedTypeReferenceList(method.TemplateArguments, templateArguments);
      }else
        this.VisitResolvedReference(memberBinding.BoundMember, memberBinding.BoundMemberExpression);
      return base.VisitMemberBinding(memberBinding);
    }
    public override Method VisitMethod(Method method){
      if (method == null) return null;
      this.VisitResolvedTypeReference(method.ReturnType, method.ReturnTypeExpression);
      this.VisitResolvedTypeReferenceList(method.ImplementedTypes, method.ImplementedTypeExpressions);
      return base.VisitMethod(method);
    }
    public override Expression VisitParameter(Parameter parameter){
      if (parameter == null) return null;
      this.VisitResolvedTypeReference(parameter.Type, parameter.TypeExpression);
      return base.VisitParameter(parameter);
    }
    public override Property VisitProperty(Property property){
      if (property == null) return null;
      this.VisitResolvedTypeReference(property.Type, property.TypeExpression);
      return base.VisitProperty(property);
    }
    public override ComprehensionBinding VisitComprehensionBinding(ComprehensionBinding comprehensionBinding){
      if (comprehensionBinding == null) return null;
      this.VisitResolvedTypeReference(comprehensionBinding.TargetVariableType, comprehensionBinding.TargetVariableTypeExpression);
      return base.VisitComprehensionBinding(comprehensionBinding);
    }
    public override Node VisitQueryIterator(QueryIterator xiterator){
      if (xiterator == null) return null;
      this.VisitResolvedTypeReference(xiterator.Type, xiterator.TypeExpression);
      return base.VisitQueryIterator(xiterator);
    }
    public override Expression VisitTemplateInstance(TemplateInstance templateInstance){
      if (templateInstance == null) return null;
      this.VisitResolvedTypeReferenceList(templateInstance.TypeArguments, templateInstance.TypeArgumentExpressions);
      return base.VisitTemplateInstance(templateInstance);
    }
    public override TypeswitchCase VisitTypeswitchCase(TypeswitchCase typeswitchCase){
      if (typeswitchCase == null) return null;
      this.VisitResolvedTypeReference(typeswitchCase.LabelType, typeswitchCase.LabelTypeExpression);
      return base.VisitTypeswitchCase(typeswitchCase);
    }
    public override TypeAlias VisitTypeAlias(TypeAlias tAlias){
      if (tAlias == null) return null;
      this.VisitResolvedTypeReference(tAlias.AliasedType, tAlias.AliasedTypeExpression);
      return base.VisitTypeAlias(tAlias);
    }
    public override TypeModifier VisitTypeModifier(TypeModifier typeModifier){
      if (typeModifier == null) return null;
      this.VisitResolvedTypeReference(typeModifier.ModifiedType, typeModifier.ModifiedTypeExpression);
      this.VisitResolvedTypeReference(typeModifier.Modifier, typeModifier.ModifierExpression);
      return base.VisitTypeModifier(typeModifier);
    }
    public override TypeNode VisitTypeNode(TypeNode typeNode){
      if (typeNode == null) return null;
      this.VisitResolvedInterfaceReferenceList(typeNode.Interfaces, typeNode.InterfaceExpressions);
      this.VisitResolvedTypeReferenceList(typeNode.TemplateArguments, typeNode.TemplateArgumentExpressions);
      return base.VisitTypeNode(typeNode);
    }
    public override Statement VisitVariableDeclaration(VariableDeclaration variableDeclaration){
      if (variableDeclaration == null) return null;
      this.VisitResolvedTypeReference(variableDeclaration.Type, variableDeclaration.TypeExpression);
      return base.VisitVariableDeclaration(variableDeclaration);
    }
    public virtual void VisitResolvedInterfaceReferenceList(InterfaceList resolvedInterfaceList, InterfaceList interfaceExpressionList){
      if (resolvedInterfaceList == null || interfaceExpressionList == null) return;
      int n = resolvedInterfaceList.Count;
      if (n > interfaceExpressionList.Count){Debug.Assert(false); n = interfaceExpressionList.Count;}
      for (int i = 0; i < n; i++){
        Interface resolvedInterface = resolvedInterfaceList[i];
        if (resolvedInterface == null) continue;
        Interface reference = interfaceExpressionList[i];
        if (reference == null) continue;
        this.VisitResolvedTypeReference(resolvedInterface, reference);
      }
    }
    public virtual void VisitResolvedTypeReference(TypeReference typeReference){
      if (typeReference == null) return;
      this.VisitResolvedTypeReference(typeReference.Type, typeReference.Expression);
    }
    public virtual void VisitResolvedTypeReference(TypeNode type, TypeNode typeExpression){
      if (typeExpression == null) return;
      if (type == null){Debug.Assert(false); return;}
      switch (type.NodeType){
        case NodeType.ArrayType:
          ArrayType arrType = (ArrayType)type;
          ArrayTypeExpression arrTypeExpr = (ArrayTypeExpression)typeExpression;
          this.VisitResolvedTypeReference(arrType.ElementType, arrTypeExpr.ElementType);
          return;
        case NodeType.DelegateNode:{
          FunctionType ftype = type as FunctionType;
          if (ftype == null) goto default;
          FunctionTypeExpression ftypeExpr = (FunctionTypeExpression)typeExpression;
          this.VisitResolvedTypeReference(ftype.ReturnType, ftypeExpr.ReturnType);
          this.VisitParameterList(ftype.Parameters);
          return;}
        case NodeType.Pointer:
          Pointer pType = (Pointer)type;
          PointerTypeExpression pTypeExpr = (PointerTypeExpression)typeExpression;
          this.VisitResolvedTypeReference(pType.ElementType, pTypeExpr.ElementType);
          return;
        case NodeType.Reference:
          Reference rType = (Reference)type;
          ReferenceTypeExpression rTypeExpr = (ReferenceTypeExpression)typeExpression;
          this.VisitResolvedTypeReference(rType.ElementType, rTypeExpr.ElementType);
          return;
        case NodeType.TupleType:{
          TupleType tType = (TupleType)type;
          MemberList members = tType.Members;
          int n = members == null ? 0 : members.Count;
          for (int i = 0; i < n; i++){
            Field f = members[i] as Field;
            if (f == null) continue;
            this.VisitResolvedTypeReference(f.Type, f.TypeExpression);
          }
          return;}
        case NodeType.TypeIntersection:
          TypeIntersection tIntersect = (TypeIntersection)type;
          TypeIntersectionExpression tIntersectExpr = (TypeIntersectionExpression)typeExpression;
          this.VisitResolvedTypeReferenceList(tIntersect.Types, tIntersectExpr.Types);
          return;
        case NodeType.TypeUnion:
          TypeUnion tUnion = (TypeUnion)type;
          TypeUnionExpression tUnionExpression = (TypeUnionExpression)typeExpression;
          this.VisitResolvedTypeReferenceList(tUnion.Types, tUnionExpression.Types);
          return;
        case NodeType.ClassParameter:
        case NodeType.TypeParameter:
          this.VisitResolvedReference(type, typeExpression);
          return;
        case NodeType.ConstrainedType:
          ConstrainedType conType = (ConstrainedType)type;
          this.VisitResolvedTypeReference(conType.UnderlyingType, conType.UnderlyingTypeExpression);
          this.VisitExpression(conType.Constraint);
          return;
        case NodeType.OptionalModifier:
        case NodeType.RequiredModifier:
          TypeModifier modType = (TypeModifier)type;
          this.VisitResolvedTypeReference(modType.ModifiedType, modType.ModifiedTypeExpression);
          this.VisitResolvedTypeReference(modType.Modifier, modType.ModifierExpression);
          return;
        case NodeType.ArrayTypeExpression:
        case NodeType.BoxedTypeExpression:
        case NodeType.ClassExpression:
        case NodeType.FlexArrayTypeExpression:
        case NodeType.FunctionTypeExpression:
        case NodeType.InvariantTypeExpression:
        case NodeType.InterfaceExpression:
        case NodeType.NonEmptyStreamTypeExpression:
        case NodeType.NonNullTypeExpression:
        case NodeType.NonNullableTypeExpression:
        case NodeType.NullableTypeExpression:
        case NodeType.PointerTypeExpression:
        case NodeType.ReferenceTypeExpression:
        case NodeType.StreamTypeExpression:
        case NodeType.TupleTypeExpression:
        case NodeType.TypeExpression:
        case NodeType.TypeIntersectionExpression:
        case NodeType.TypeUnionExpression:
          //Failed to resolve, hence there is an error here, not a reference to a resolved type.
          return;
        default:
          if (type.Template != null && type.TemplateArguments != null){
            if (type.TemplateExpression != null) //Could be null if special syntax exists
              this.VisitResolvedReference(type, type.TemplateExpression);
            this.VisitResolvedTypeReferenceList(type.TemplateArguments, type.TemplateArgumentExpressions);
            return;
          }
          this.VisitResolvedReference(type, typeExpression);
          return;
      }
    }
    public virtual void VisitResolvedTypeReferenceList(TypeNodeList resolvedTypeList, TypeNodeList typeExpressionList){
      if (resolvedTypeList == null || typeExpressionList == null) return;
      int n = resolvedTypeList.Count;
      if (n > typeExpressionList.Count){Debug.Assert(false); n = typeExpressionList.Count;}
      for (int i = 0; i < n; i++){
        TypeNode resolvedType = resolvedTypeList[i];
        if (resolvedType == null) continue;
        TypeNode reference = typeExpressionList[i];
        if (reference == null) continue;
        this.VisitResolvedTypeReference(resolvedType, reference);
      }
    }
  }
}