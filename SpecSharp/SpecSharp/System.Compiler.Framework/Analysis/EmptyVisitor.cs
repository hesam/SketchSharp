//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  using System;




  /// <summary>
  /// Subclass of the CCI <c>StandardVisitor</c> that visits the fields of statements in
  /// the same order that is used by the Reader.  This is important if you want to (abstractly)
  /// interpret some code.  Sub-expressions have side effects, hence, the order they are
  /// visited IS important.  Unfortunately, this cannot be part of the CCI because many projects
  /// rely on the semantics (if any) of the current visitor.
  /// </summary>
  public class ProperOrderVisitor: StandardVisitor 
  {

    public override Statement VisitAssignmentStatement(AssignmentStatement assignment)
    {
      Expression source = this.VisitExpression(assignment.Source);
      System.Diagnostics.Debug.Assert(source != null, "VisitExpression must return non-null if passed non-null");
      assignment.Source = source;
      Expression target = this.VisitTargetExpression(assignment.Target);
      System.Diagnostics.Debug.Assert(target != null, "VisitExpression must return non-null if passed non-null");
      assignment.Target = target;
      return assignment;
    }

    public override Expression VisitIndexer(Indexer indexer) 
    {
      ExpressionList ops = this.VisitExpressionList(indexer.Operands);
      System.Diagnostics.Debug.Assert(ops != null, "VisitExpressionList must return non-null if passed non-null");
      indexer.Operands = ops;

      Expression obj = this.VisitExpression(indexer.Object);
      System.Diagnostics.Debug.Assert(obj != null, "VisitExpression must return non-null if passed non-null");
      indexer.Object = obj;
      return indexer;
    }

    public override Expression VisitBinaryExpression(BinaryExpression binaryExpression)
    {
      if (binaryExpression.Operand2 != null) 
      {
        binaryExpression.Operand2 = this.VisitExpression(binaryExpression.Operand2);
      }
      if (binaryExpression.Operand1 != null) 
      {
        binaryExpression.Operand1 = this.VisitExpression(binaryExpression.Operand1);
      }
      return binaryExpression;
    }

    public override Expression VisitMethodCall(MethodCall call)
    {
      call.Operands = this.VisitExpressionList(call.Operands);
      if (call.Callee != null) 
      {
        call.Callee = this.VisitExpression(call.Callee);
      }
      return call;
    }

    public override ExpressionList VisitExpressionList(ExpressionList expressions)
    {
      if (expressions == null) return null;

      for(int i = expressions.Count-1; i >= 0; i--) 
      {
        Expression elem = this.VisitExpression(expressions[i]);
        System.Diagnostics.Debug.Assert(elem != null, "VisitExpression must return non null if passed non null");
        expressions[i] = elem;
      }
      return expressions;
    }

    public override Expression VisitTernaryExpression(TernaryExpression expression)
    {
      if (expression.Operand3 != null) 
      {
        expression.Operand3 = this.VisitExpression(expression.Operand3);
      }
      if (expression.Operand2 != null) 
      {
        expression.Operand2 = this.VisitExpression(expression.Operand2);
      }
      if (expression.Operand1 != null) 
      {
        expression.Operand1 = this.VisitExpression(expression.Operand1);
      }
      return expression;
    }
  }

  
  /// <summary>
  /// Visitor that doesn't implement any Visit method
  /// (throws new ApplicationException("unimplemented") instead.)
  /// Good if all you want to do is dispatch some specific processing for each node type,
  /// without going deep into the recursive data structure.  Throwing an exception for
  /// unimplemented things is also useful for catching the untreated cases.
  /// </summary>
  public abstract class EmptyVisitor: StandardVisitor
  {
    public override Node VisitUnknownNodeType(Node node)
    {
      throw new ApplicationException(String.Format("unexpected node type {0}", node.NodeType));
    }


    public override Node Visit(Node node)
    {
      if (node == null) return null;
      switch (node.NodeType)
      {
        case NodeType.AddressDereference:
          return this.VisitAddressDereference((AddressDereference)node);
        case NodeType.AliasDefinition :
          return this.VisitAliasDefinition((AliasDefinition)node);
        case NodeType.Arglist :
          return this.VisitArglist((Expression)node);
        case NodeType.ArglistExpression:
          return this.VisitArglistExpression((ArglistExpression)node);
        case NodeType.ArglistArgumentExpression:
          return this.VisitArglistArgumentExpression((ArglistArgumentExpression)node);
        case NodeType.ArrayType : 
          System.Diagnostics.Debug.Assert(false); return null;
        case NodeType.Assembly : 
          return this.VisitAssembly((AssemblyNode)node);
        case NodeType.AssemblyReference :
          return this.VisitAssemblyReference((AssemblyReference)node);
        case NodeType.Assertion:
          return this.VisitAssertion((Assertion)node);
        case NodeType.AssignmentExpression:
          return this.VisitAssignmentExpression((AssignmentExpression)node);
        case NodeType.AssignmentStatement : 
          return this.VisitAssignmentStatement((AssignmentStatement)node);
        case NodeType.Attribute :
          return this.VisitAttributeNode((AttributeNode)node);
        case NodeType.Base :
          return this.VisitBase((Base)node);
        case NodeType.Block : 
          return this.VisitBlock((Block)node);
        case NodeType.BlockExpression :
          return this.VisitBlockExpression((BlockExpression)node);
        case NodeType.Branch :
          return this.VisitBranch((Branch)node);
        case NodeType.CompilationUnit:
          return this.VisitCompilationUnit((CompilationUnit)node);
        case NodeType.CompilationUnitSnippet:
          return this.VisitCompilationUnitSnippet((CompilationUnitSnippet)node);
        case NodeType.Continue :
          return this.VisitContinue((Continue)node);
        case NodeType.DebugBreak :
          return node;
        case NodeType.Call :
        case NodeType.Calli :
        case NodeType.Callvirt :
        case NodeType.Jmp :
        case NodeType.MethodCall :
          return this.VisitMethodCall((MethodCall)node);
        case NodeType.Catch :
          return this.VisitCatch((Catch)node);
        case NodeType.Class :
          return this.VisitClass((Class)node);
        case NodeType.Construct :
          return this.VisitConstruct((Construct)node);
        case NodeType.ConstructArray :
          return this.VisitConstructArray((ConstructArray)node);
        case NodeType.ConstructDelegate :
          return this.VisitConstructDelegate((ConstructDelegate)node);
        case NodeType.ConstructIterator :
          return this.VisitConstructIterator((ConstructIterator)node);
        case NodeType.DelegateNode :
          return this.VisitDelegateNode((DelegateNode)node);
        case NodeType.DoWhile:
          return this.VisitDoWhile((DoWhile)node);
        case NodeType.Dup :
          return this.VisitDup((Expression)node);
        case NodeType.EndFilter :
          return this.VisitEndFilter((EndFilter)node);
        case NodeType.EndFinally:
          return node;
        case NodeType.EnumNode :
          return this.VisitEnumNode((EnumNode)node);
        case NodeType.Event: 
          return this.VisitEvent((Event)node);
        case NodeType.Exit :
          return this.VisitExit((Exit)node);
        case NodeType.ExpressionSnippet:
          return this.VisitExpressionSnippet((ExpressionSnippet)node);
        case NodeType.ExpressionStatement :
          return this.VisitExpressionStatement((ExpressionStatement)node);
        case NodeType.FaultHandler :
          return this.VisitFaultHandler((FaultHandler)node);
        case NodeType.Field :
          return this.VisitField((Field)node);
        case NodeType.FieldInitializerBlock:
          return this.VisitFieldInitializerBlock((FieldInitializerBlock)node);
        case NodeType.Finally :
          return this.VisitFinally((Finally)node);
        case NodeType.Filter :
          return this.VisitFilter((Filter)node);
        case NodeType.For :
          return this.VisitFor((For)node);
        case NodeType.ForEach :
          return this.VisitForEach((ForEach)node);
        case NodeType.Goto :
          return this.VisitGoto((Goto)node);
        case NodeType.Identifier :
          return this.VisitIdentifier((Identifier)node);
        case NodeType.If :
          return this.VisitIf((If)node);
        case NodeType.ImplicitThis :
          return this.VisitImplicitThis((ImplicitThis)node);
        case NodeType.Indexer :
          return this.VisitIndexer((Indexer)node);
        case NodeType.InstanceInitializer :
          return this.VisitInstanceInitializer((InstanceInitializer)node);
        case NodeType.StaticInitializer :
          return this.VisitStaticInitializer((StaticInitializer)node);
        case NodeType.Method: 
          return this.VisitMethod((Method)node);
        case NodeType.Interface :
          return this.VisitInterface((Interface)node);
        case NodeType.LabeledStatement :
          return this.VisitLabeledStatement((LabeledStatement)node);
        case NodeType.Literal:
          return this.VisitLiteral((Literal)node);
        case NodeType.Local :
          return this.VisitLocal((Local)node);
        case NodeType.LocalDeclarationsStatement:
          return this.VisitLocalDeclarationsStatement((LocalDeclarationsStatement)node);
        case NodeType.LRExpression:
          return this.VisitLRExpression((LRExpression)node);
        case NodeType.MemberBinding :
          return this.VisitMemberBinding((MemberBinding)node);
        case NodeType.Module :
          return this.VisitModule((Module)node);
        case NodeType.ModuleReference :
          return this.VisitModuleReference((ModuleReference)node);
        case NodeType.NameBinding :
          return this.VisitNameBinding((NameBinding)node);
        case NodeType.NamedArgument :
          return this.VisitNamedArgument((NamedArgument)node);
        case NodeType.Namespace :
          return this.VisitNamespace((Namespace)node);
        case NodeType.Nop :
          return node;
        case NodeType.SwitchCaseBottom:
          return this.VisitSwitchCaseBottom((Statement)node);
        case NodeType.OptionalModifier:
        case NodeType.RequiredModifier:
          return this.VisitTypeModifier((TypeModifier)node);
        case NodeType.Parameter :
          return this.VisitParameter((Parameter)node);
        case NodeType.Pop :
          UnaryExpression unex = node as UnaryExpression;
          if (unex != null) 
          {
            return this.VisitPopExpr(unex);
          }
          else
            return this.VisitPop((Expression)node);
        case NodeType.Property: 
          return this.VisitProperty((Property)node);
        case NodeType.QualifiedIdentifer :
          return this.VisitQualifiedIdentifier((QualifiedIdentifier)node);
        case NodeType.Rethrow :
        case NodeType.Throw :
          return this.VisitThrow((Throw)node);
        case NodeType.Return:
          return this.VisitReturn((Return)node);
        case NodeType.Repeat:
          return this.VisitRepeat((Repeat)node);
        case NodeType.SetterValue:
          return this.VisitSetterValue((SetterValue)node);
        case NodeType.StatementSnippet:
          return this.VisitStatementSnippet((StatementSnippet)node);
        case NodeType.Struct :
          return this.VisitStruct((Struct)node);
        case NodeType.Switch :
          return this.VisitSwitch((Switch)node);
        case NodeType.SwitchInstruction :
          return this.VisitSwitchInstruction((SwitchInstruction)node);
        case NodeType.SwitchCase :
          return this.VisitSwitchCase((SwitchCase)node);
        case NodeType.Typeswitch :
          return this.VisitTypeswitch((Typeswitch)node);
        case NodeType.TypeswitchCase :
          return this.VisitTypeswitchCase((TypeswitchCase)node);
        case NodeType.This :
          return this.VisitThis((This)node);
        case NodeType.Try :
          return this.VisitTry((Try)node);
          /*
        case NodeType.TypeAlias:
          return this.VisitTypeAlias((TypeAlias)node);
          */
        case NodeType.TypeMemberSnippet:
          return this.VisitTypeMemberSnippet((TypeMemberSnippet)node);
        case NodeType.ClassParameter:
        case NodeType.TypeParameter:
          return this.VisitTypeParameter((TypeNode)node);
        case NodeType.UsedNamespace :
          return this.VisitUsedNamespace((UsedNamespace)node);
        case NodeType.VariableDeclaration:
          return this.VisitVariableDeclaration((VariableDeclaration)node);
        case NodeType.While:
          return this.VisitWhile((While)node);
        case NodeType.Yield:
          return this.VisitYield((Yield)node);

        case NodeType.Conditional :
        case NodeType.Cpblk :
        case NodeType.Initblk :
          return this.VisitTernaryExpression((TernaryExpression)node);

        case NodeType.Add : 
        case NodeType.Add_Ovf : 
        case NodeType.Add_Ovf_Un : 
        case NodeType.AddEventHandler :
        case NodeType.And : 
        case NodeType.Box :
        case NodeType.Castclass : 
        case NodeType.Ceq : 
        case NodeType.Cgt : 
        case NodeType.Cgt_Un : 
        case NodeType.Clt : 
        case NodeType.Clt_Un : 
        case NodeType.Div : 
        case NodeType.Div_Un : 
        case NodeType.Eq : 
        case NodeType.Ge : 
        case NodeType.Gt : 
        case NodeType.Is : 
        case NodeType.Isinst : 
        case NodeType.Ldvirtftn :
        case NodeType.Le : 
        case NodeType.LogicalAnd :
        case NodeType.LogicalOr :
        case NodeType.Lt : 
        case NodeType.Mkrefany :
        case NodeType.Mul : 
        case NodeType.Mul_Ovf : 
        case NodeType.Mul_Ovf_Un : 
        case NodeType.NullCoalesingExpression:
        case NodeType.Ne : 
        case NodeType.Or : 
        case NodeType.Refanyval :
        case NodeType.Rem : 
        case NodeType.Rem_Un : 
        case NodeType.RemoveEventHandler :
        case NodeType.Shl : 
        case NodeType.Shr : 
        case NodeType.Shr_Un : 
        case NodeType.Sub : 
        case NodeType.Sub_Ovf : 
        case NodeType.Sub_Ovf_Un : 
        case NodeType.Unbox : 
        case NodeType.UnboxAny:
        case NodeType.Xor : 
          return this.VisitBinaryExpression((BinaryExpression)node);
        
        case NodeType.AddressOf:
        case NodeType.ReadOnlyAddressOf:
        case NodeType.OutAddress:  // alias of AddressOf
        case NodeType.RefAddress:  // alias of AddressOf
        case NodeType.Ckfinite :
        case NodeType.Conv_I :
        case NodeType.Conv_I1 :
        case NodeType.Conv_I2 :
        case NodeType.Conv_I4 :
        case NodeType.Conv_I8 :
        case NodeType.Conv_Ovf_I :
        case NodeType.Conv_Ovf_I1 :
        case NodeType.Conv_Ovf_I1_Un :
        case NodeType.Conv_Ovf_I2 :
        case NodeType.Conv_Ovf_I2_Un :
        case NodeType.Conv_Ovf_I4 :
        case NodeType.Conv_Ovf_I4_Un :
        case NodeType.Conv_Ovf_I8 :
        case NodeType.Conv_Ovf_I8_Un :
        case NodeType.Conv_Ovf_I_Un :
        case NodeType.Conv_Ovf_U :
        case NodeType.Conv_Ovf_U1 :
        case NodeType.Conv_Ovf_U1_Un :
        case NodeType.Conv_Ovf_U2 :
        case NodeType.Conv_Ovf_U2_Un :
        case NodeType.Conv_Ovf_U4 :
        case NodeType.Conv_Ovf_U4_Un :
        case NodeType.Conv_Ovf_U8 :
        case NodeType.Conv_Ovf_U8_Un :
        case NodeType.Conv_Ovf_U_Un :
        case NodeType.Conv_R4 :
        case NodeType.Conv_R8 :
        case NodeType.Conv_R_Un :
        case NodeType.Conv_U :
        case NodeType.Conv_U1 :
        case NodeType.Conv_U2 :
        case NodeType.Conv_U4 :
        case NodeType.Conv_U8 :
        case NodeType.Decrement :
        case NodeType.Increment :
        case NodeType.Ldftn :
        case NodeType.Ldlen :
        case NodeType.Ldtoken :
        case NodeType.Localloc :
        case NodeType.LogicalNot :
        case NodeType.Neg :
        case NodeType.Not :
        case NodeType.Refanytype :
        case NodeType.Sizeof :
        case NodeType.Typeof :
        case NodeType.UnaryPlus :
          return this.VisitUnaryExpression((UnaryExpression)node);

        default:
          return this.VisitUnknownNodeType(node);
      }
    }


    public override Expression VisitAddressDereference(AddressDereference addr)
    {
      throw new ApplicationException("unimplemented");
    }

    public override AliasDefinition VisitAliasDefinition(AliasDefinition aliasDefinition)
    {
      throw new ApplicationException("unimplemented");
    }

    public virtual Expression VisitArglist(Expression expression)
    {
      throw new ApplicationException("unimplemented");
    }

    public override AssemblyNode VisitAssembly(AssemblyNode assembly)
    {
      throw new ApplicationException("unimplemented");
    }

    public override AssemblyReference VisitAssemblyReference(AssemblyReference assemblyReference)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitAssignmentExpression(AssignmentExpression assignment)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitAssignmentStatement(AssignmentStatement assignment)
    {
      throw new ApplicationException("unimplemented");
    }

    public override AttributeList VisitAttributeList(AttributeList attributes)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitBase(Base Base)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitBinaryExpression(BinaryExpression binaryExpression)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Block VisitBlock(Block block)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitBlockExpression(BlockExpression blockExpression)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitBranch(Branch branch)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitCatch(Catch Catch)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Class VisitClass(Class Class)
    {
      throw new ApplicationException("unimplemented");
    }

    public override CompilationUnit VisitCompilationUnit(CompilationUnit cUnit)
    {
      throw new ApplicationException("unimplemented");
    }

    /*
    public override CompilationUnitSnippet VisitCompilationUnitSnippet(CompilationUnitSnippet snippet)
    {
      throw new ApplicationException("unimplemented");
    }
    */

    public override Expression VisitConstruct(Construct cons)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitConstructArray(ConstructArray consArr)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitConstructDelegate(ConstructDelegate consDelegate)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitConstructIterator(ConstructIterator consIterator)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitContinue(Continue Continue)
    {
      throw new ApplicationException("unimplemented");
    }

    public override DelegateNode VisitDelegateNode(DelegateNode delegateNode)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitDoWhile(DoWhile doWhile)
    {
      throw new ApplicationException("unimplemented");
    }

    public virtual Expression VisitDup(Expression expression)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitEndFilter(EndFilter endFilter)
    {
      throw new ApplicationException("unimplemented");
    }

    public override EnumNode VisitEnumNode(EnumNode enumNode)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Event VisitEvent(Event evnt)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitExit(Exit exit)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitExpression(Expression expression)
    {
      return (Expression) this.Visit(expression);
    }

    public override ExpressionList VisitExpressionList(ExpressionList expressions)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitExpressionSnippet(ExpressionSnippet snippet)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitExpressionStatement(ExpressionStatement statement)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitFaultHandler(FaultHandler faultHandler)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Field VisitField(Field field)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Block VisitFieldInitializerBlock(FieldInitializerBlock block)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitFilter(Filter filter)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitFinally(Finally Finally)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitFor(For For)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitForEach(ForEach forEach)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitGoto(Goto Goto)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitIdentifier(Identifier identifier)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitIf(If If)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitImplicitThis(ImplicitThis implicitThis)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitIndexer(Indexer indexer)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Interface VisitInterface(Interface Interface)
    {
      throw new ApplicationException("unimplemented");
    }

    /*
    public override InterfaceList VisitInterfaceList(InterfaceList interfaces)
    {
      throw new ApplicationException("unimplemented");
    }
    */

    public override Interface VisitInterfaceReference(Interface Interface)
    {
      throw new ApplicationException("unimplemented");
    }

    public override InterfaceList VisitInterfaceReferenceList(InterfaceList interfaceReferences)
    {
      throw new ApplicationException("unimplemented");
    }

    public override InstanceInitializer VisitInstanceInitializer(InstanceInitializer cons)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitLabeledStatement(LabeledStatement lStatement)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitLiteral(Literal literal)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitLocal(Local local)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitLRExpression(LRExpression expr)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitMethodCall(MethodCall call)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitMemberBinding(MemberBinding memberBinding)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Method VisitMethod(Method method)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Module VisitModule(Module module)
    {
      throw new ApplicationException("unimplemented");
    }

    public override ModuleReference VisitModuleReference(ModuleReference moduleReference)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitNameBinding(NameBinding nameBinding)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitNamedArgument(NamedArgument namedArgument)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Namespace VisitNamespace(Namespace nspace)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitParameter(Parameter parameter)
    {
      throw new ApplicationException("unimplemented");
    }

    public override ParameterList VisitParameterList(ParameterList parameterList)
    {
      throw new ApplicationException("unimplemented");
    }

    public virtual Expression VisitPop(Expression expression)
    {
      throw new ApplicationException("unimplemented");
    }

    public virtual Expression VisitPopExpr(UnaryExpression unex) {
      Expression operand = (Expression)this.Visit(unex.Operand);
      System.Diagnostics.Debug.Assert(operand != null, "Visit on non-null expression must return non-null expression");
      unex.Operand = operand;
      return unex;
    }

    public override Property VisitProperty(Property property)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitQualifiedIdentifier(QualifiedIdentifier qualifiedIdentifier)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitRepeat(Repeat repeat)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitReturn(Return Return)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitSetterValue(SetterValue value)
    {
      throw new ApplicationException("unimplemented");
    }

    public override StatementList VisitStatementList(StatementList statements)
    {
      throw new ApplicationException("unimplemented");
    }

    public override StatementSnippet VisitStatementSnippet(StatementSnippet snippet)
    {
      throw new ApplicationException("unimplemented");
    }

    public override StaticInitializer VisitStaticInitializer(StaticInitializer cons)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Struct VisitStruct(Struct Struct)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitSwitch(Switch Switch)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitSwitchInstruction(SwitchInstruction switchInstruction) 
    {
      throw new ApplicationException("unimplemented");
    }

    public override SwitchCase VisitSwitchCase(SwitchCase switchCase)
    {
      throw new ApplicationException("unimplemented");
    }

    public abstract Statement VisitSwitchCaseBottom(Statement switchCaseBottom);

    public override SwitchCaseList VisitSwitchCaseList(SwitchCaseList switchCases)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitTypeswitch(Typeswitch Typeswitch)
    {
      throw new ApplicationException("unimplemented");
    }

    public override TypeswitchCase VisitTypeswitchCase(TypeswitchCase typeswitchCase)
    {
      throw new ApplicationException("unimplemented");
    }

    public override TypeswitchCaseList VisitTypeswitchCaseList(TypeswitchCaseList typeswitchCases)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitTargetExpression(Expression expression)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitTernaryExpression(TernaryExpression expression)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitThis(This This)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitThrow(Throw Throw)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitTry(Try Try)
    {
      throw new ApplicationException("unimplemented");
    }

    /*
    public override TypeAlias VisitTypeAlias(TypeAlias tAlias)
    {
      throw new ApplicationException("unimplemented");
    }
    */

    public override TypeMemberSnippet VisitTypeMemberSnippet(TypeMemberSnippet snippet)
    {
      throw new ApplicationException("unimplemented");
    }

    public override TypeModifier VisitTypeModifier(TypeModifier typeModifier)
    {
      throw new ApplicationException("unimplemented");
    }

    public override TypeNode VisitTypeNode(TypeNode typeNode)
    {
      throw new ApplicationException("unimplemented");
    }

    public override TypeNode VisitTypeParameter(TypeNode typeParameter)
    {
      throw new ApplicationException("unimplemented");
    }

    public override TypeNodeList VisitTypeParameterList(TypeNodeList typeParameters)
    {
      throw new ApplicationException("unimplemented");
    }

    public override TypeNode VisitTypeReference(TypeNode type)
    {
      throw new ApplicationException("unimplemented");
    }

    public override TypeNodeList VisitTypeReferenceList(TypeNodeList typeReferences)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Expression VisitUnaryExpression(UnaryExpression unaryExpression)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitVariableDeclaration(VariableDeclaration variableDeclaration)
    {
      throw new ApplicationException("unimplemented");
    }
    
    public override UsedNamespace VisitUsedNamespace(UsedNamespace usedNamespace)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitWhile(While While)
    {
      throw new ApplicationException("unimplemented");
    }

    public override Statement VisitYield(Yield Yield)
    {
      throw new ApplicationException("unimplemented");
    }
  }
}
