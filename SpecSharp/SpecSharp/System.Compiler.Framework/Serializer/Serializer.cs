//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Text;
using System.Diagnostics;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
	/// <summary>
	/// ContractSerializer is used to write a serialized form of method and type contracts
	/// into custom attributes. Currently, it just creates the serialized form as a string
	/// which must be retrieved externally and stored in a custom attribute.
	/// Note that the serialization should happen before the contracts are normalized.
	/// Eventually, I suppose this should be called in a separate pass before Normalizer
	/// is called. Currently, Checker serializes each contract (and stores it in a custom
	/// attribute).
	/// </summary>
  public class ContractSerializer : StandardVisitor {
    private StringBuilder _string = new StringBuilder();
    private Module currentModule;
    private Local currentClosureLocal;

    public string SerializedContract {
      get { return _string == null ? null : _string.ToString(); } 
    }
    public ContractSerializer(Module module) { this.currentModule = module; }
    public ContractSerializer(Module module, Local closureLocal) { this.currentModule = module; this.currentClosureLocal = closureLocal; }

    private static string OperatorString (NodeType nt) {
      string name;
      switch (nt) {
        case NodeType.Lt:
          name = "<";
          break;

        case NodeType.Le:
          name = "<=";
          break;

        case NodeType.Ge:
          name = ">=";
          break;

        case NodeType.Gt:
          name = ">";
          break;

        case NodeType.Eq:
          name = "==";
          break;

        case NodeType.Ne:
          name = "!=";
          break;

        case NodeType.LogicalOr:
          name = "||";
          break;

        case NodeType.LogicalAnd:
          name = "&&";
          break;

        case NodeType.LogicalNot:
          name = "!";
          break;

        case NodeType.Or:
          name = "|";
          break;

        case NodeType.And:
          name = "&";
          break;
					
        case NodeType.Not:
          name = "~";
          break;

        case NodeType.Xor:
          name = "^";
          break;

        case NodeType.Mul:
        case NodeType.Mul_Ovf:
        case NodeType.Mul_Ovf_Un:
          name = "*";
          break;

        case NodeType.Add:
        case NodeType.Add_Ovf:
        case NodeType.Add_Ovf_Un:
          name = "+";
          break;

        case NodeType.Sub:
        case NodeType.Sub_Ovf:
        case NodeType.Sub_Ovf_Un:
          name = "-";
          break;

        case NodeType.Div:
        case NodeType.Div_Un:
          name = "/";
          break;

        case NodeType.Rem:
        case NodeType.Rem_Un:
          name = "%";
          break;

        case NodeType.Neg:
          name = "0-";
          break;

        case NodeType.UnaryPlus:
          name = "0+";
          break;

        case NodeType.Shl:
          name = "<<";
          break;

        case NodeType.Shr:
          name = "1>>";
          break;

        case NodeType.Shr_Un:
          name = "0>>";
          break;

        //case NodeType.Typeof:
        //  name = "$typeof";
        //  break;

        case NodeType.As:
          name = "as";
          break;
          
//        case NodeType.Is:
//          name = "$typetest";
//          break;

        case NodeType.RefAddress:
          name = "ref";
          break;

        case NodeType.OutAddress:
          name = "out";
          break;

        case NodeType.Implies:
          name = "==>";
          break;

        case NodeType.Range:
          name = ":";
          break;

//        case NodeType.ExplicitCoercion:
//          name = "&coerce";
//          break;


        case NodeType.UnboxAny:
          name = "$unboxAny";
          break;
          
        case NodeType.DefaultValue:
          name = "$defaultValue";
          break;

        case NodeType.Iff:
          name = "<==>";
          break;

        default:
          name = "$" + nt;
          break;
      }

      return "::" + name;
    }

    private void AddArgumentTypeSig(params TypeNode[] args) {
      if (this._string == null){ this._string = new StringBuilder(); }
      this._string.Append('(');

      for (int i=0; i<args.Length; i++) {
        this.AddType(args[i]);
        if (i != args.Length-1) {
          this._string.Append(",");
        }
      }
      this._string.Append(')');
    }

    // TODO: Remove this? There shouldn't be any ANFs that need to be serialized in a contract
    public override Expression VisitAnonymousNestedFunction(AnonymousNestedFunction anf) {
      // we can serialize an ANF only if it is really just a wrapper around a comprehension
      // in that case, just serialize the comprehension and let the deserializer
      // reconstruct the ANF.
      if (anf == null) return null;
      if (anf.Body == null) return anf;
      if (anf.Body.Statements == null) return anf;
      if (anf.Body.Statements.Count != 1) return anf;
      ExpressionStatement es = anf.Body.Statements[0] as ExpressionStatement;
      if (es == null) return anf;
      Comprehension q = es.Expression as Comprehension;
      if (q == null) return anf;
      this.VisitComprehension(q);
      return anf;
    }
    public override Expression VisitBlockExpression(BlockExpression blockExpression) {
      if (blockExpression == null) return null;
      // This is either
      // 1) a call of the form {newLocal = e; &newLocal; }.M(...)
      //    where M.DeclaringType is Struct
      //    We simply serialize e
      // or
      // 2) of the form { newLocal = e; AssertNotNull(newLocal); newLocal; }
      // or
      // 3) of the form { AssertNotNull(e); e; } // where e is a variable
      //    We simply serialize e
      // or
      // 4) of the form { Exception e; <boolean expression involving e> }
      //    This arises from "throws (E e) ensures Q" clauses. Q sits in an ExpressionStatement
      //     with e as a bound variable of the block scope.
      Block block = blockExpression.Block;
      if (block == null || block.Statements == null || 3 < block.Statements.Count) return blockExpression;
      //Debug.Assert(block.Statements.Count == 2 || block.Statements.Count == 3);
      AssignmentStatement a = block.Statements[0] as AssignmentStatement;
      if (a != null) { // case (1) or (2)
        ExpressionStatement es = (ExpressionStatement)block.Statements[1];
        Debug.Assert(es.Expression.NodeType == NodeType.AddressOf || es.Expression.NodeType == NodeType.Call);
        this.Visit(a.Source);
      } else if (block.Statements.Count == 1) { // case (4) (is a stronger test needed here?
        // overload the comprehension serialization and compensate after it has been deserialized as a comprehension
        // NB: this code now lives in two places (here and VisitComprehension)!
        this._string.Append("{|");
        if (0 < block.Scope.Members.Count) {
          this._string.Append("{");
          Field f = block.Scope.Members[0] as Field; // what if there is more than one?
          this.AddType(f.Type);
          this._string.Append(",");
          this.VisitField(f);
          this._string.Append(",null}");

        }
        this._string.Append(";");
        ExpressionStatement es = block.Statements[0] as ExpressionStatement;
        this.VisitExpression(es.Expression);
        this._string.Append("|}");
      }else { // case (3)
        ExpressionStatement es = (ExpressionStatement)block.Statements[1];
        this.Visit(es.Expression);
      }
      return blockExpression;
    }
    public override Expression VisitAddressDereference(AddressDereference addr) {
      if (this._string == null){ this._string = new StringBuilder(); }
      this._string.Append("::$Deref{");
      Expression e = base.VisitAddressDereference (addr);
      this._string.Append("}");
      return e;
    }


    public override Expression VisitBinaryExpression (BinaryExpression binaryExpression) {
      if (this._string == null){ this._string = new StringBuilder(); }
      if (binaryExpression == null || binaryExpression.Operand1 == null || binaryExpression.Operand1.Type == null
        || binaryExpression.Operand2 == null || binaryExpression.Operand2.Type == null){
        return null;
      }
      // Special handling for operators that take types as arguments: they need to
      // be serialized in a different way than "regular" operators since they must
      // be deserialized specially.
      // TODO: What about serializing user-defined pure methods that take a type
      // as an argument?
      if (binaryExpression.NodeType == NodeType.ExplicitCoercion
        ||binaryExpression.NodeType == NodeType.Is
        ||binaryExpression.NodeType == NodeType.Castclass
        ||binaryExpression.NodeType == NodeType.Box
        || binaryExpression.NodeType == NodeType.Unbox
        ) {
        if (binaryExpression.NodeType == NodeType.ExplicitCoercion)
          this._string.Append("$coerce(");
        else if (binaryExpression.NodeType == NodeType.Is)
          this._string.Append("$typetest(");
        else if (binaryExpression.NodeType == NodeType.Castclass)
          this._string.Append("$castclass(");
        else if (binaryExpression.NodeType == NodeType.Box)
          this._string.Append("$box(");
        else if (binaryExpression.NodeType == NodeType.Unbox)
          this._string.Append("$unbox(");
        else {
          Debug.Assert(false,"should be exhaustive");
        }
        this.VisitExpression(binaryExpression.Operand1);
        this._string.Append(",");
        // Types can be represented either as Literals or as MemberBindings.
        Literal l = binaryExpression.Operand2 as Literal;
        TypeNode t = null;
        if (l != null)
          t = l.Value as TypeNode;
        else{
          MemberBinding mb = binaryExpression.Operand2 as MemberBinding;
          t = mb.BoundMember as TypeNode;
          if (t == null)
            t = binaryExpression.Operand2.Type;
        }
        Debug.Assert(t != null);
        this.AddType(t);
        this._string.Append(")");
        return binaryExpression;
      }
      this._string.Append(OperatorString(binaryExpression.NodeType));
      this.AddArgumentTypeSig(binaryExpression.Operand1.Type, binaryExpression.Operand2.Type);
      this._string.Append("{");
      this.VisitExpression(binaryExpression.Operand1);
      this._string.Append(",");
      this.VisitExpression(binaryExpression.Operand2);
      this._string.Append("}");
      return binaryExpression;
    }


    public override Expression VisitUnaryExpression (UnaryExpression unaryExpression) {
      if (this._string == null){ this._string = new StringBuilder(); }
      if (unaryExpression == null || unaryExpression.Operand == null || unaryExpression.Operand.Type == null){
        return null;
      }
      switch (unaryExpression.NodeType) {
        case NodeType.AddressOf:
          this.VisitExpression(unaryExpression.Operand);
          break;
        case NodeType.Parentheses:
          this._string.Append("(");
          this.VisitExpression(unaryExpression.Operand);
          this._string.Append(")");
          break;
        case NodeType.Typeof:
            this._string.Append("$typeof(");
            // Types can be represented either as Literals or as MemberBindings.
            Literal l = unaryExpression.Operand as Literal;
            TypeNode t = null;
            if (l != null)
              t = l.Value as TypeNode;
            else {
              MemberBinding mb = unaryExpression.Operand as MemberBinding;
              t = mb.BoundMember as TypeNode;
              if (t == null)
                t = unaryExpression.Operand.Type;
            }
            Debug.Assert(t != null);
            this.AddType(t);
            this._string.Append(")");
            break;
        case NodeType.RefAddress:
          this._string.Append("$ref(");
          this.Visit(unaryExpression.Operand);
          this._string.Append(")");
          break;
        default:
          this._string.Append(OperatorString(unaryExpression.NodeType));
          this.AddArgumentTypeSig(unaryExpression.Operand.Type);
          this._string.Append("{");
          this.VisitExpression(unaryExpression.Operand);
          this._string.Append("}");
          break;
      }
      return unaryExpression;
    }

    public override Expression VisitTernaryExpression(TernaryExpression ternaryExpr)
    {
      if (this._string == null){ this._string = new StringBuilder(); }
      if (ternaryExpr == null
        || ternaryExpr.Operand1 == null || ternaryExpr.Operand1.Type == null
        || ternaryExpr.Operand2 == null || ternaryExpr.Operand2.Type == null
        || ternaryExpr.Operand3 == null || ternaryExpr.Operand3.Type == null) 
      {
        return null;
      }
      if (ternaryExpr.NodeType == NodeType.Conditional) 
      {
        this._string.Append("$ite");
        this._string.Append("(");
        this.VisitExpression(ternaryExpr.Operand1);
        this._string.Append(",");
        this.VisitExpression(ternaryExpr.Operand2);
        this._string.Append(",");
        this.VisitExpression(ternaryExpr.Operand3);
        this._string.Append(",");
        this.AddType(ternaryExpr.Type);
        this._string.Append(")");
      } 
      else 
      {
        this._string.Append(OperatorString(ternaryExpr.NodeType));
        this.AddArgumentTypeSig(ternaryExpr.Operand1.Type, ternaryExpr.Operand2.Type, ternaryExpr.Operand3.Type);
        this._string.Append("{");
        this.VisitExpression(ternaryExpr.Operand1);
        this._string.Append(",");
        this.VisitExpression(ternaryExpr.Operand2);
        this._string.Append(",");
        this.VisitExpression(ternaryExpr.Operand3);
        this._string.Append("}");
      }
      return ternaryExpr;
    }


    // forall{int i in xs; i > 0};
    // ==>
    // "forall{| {i32, $block::a, $1}; ::>(i32,i32){$block::a, 0} |}"
    public override Expression VisitQuantifier(Quantifier quantifier) {
      if (this._string == null){ this._string = new StringBuilder(); }
      switch (quantifier.QuantifierType){
        case NodeType.Forall:
          this._string.Append("$_forall");
          break;
        case NodeType.Exists:
          this._string.Append("$_exists");
          break;
        case NodeType.ExistsUnique:
          this._string.Append("$_exists1");
          break;
        case NodeType.Count:
          this._string.Append("$_count");
          break;
        case NodeType.Max:
          this._string.Append("$_max");
          break;
        case NodeType.Min:
          this._string.Append("$_min");
          break;
        case NodeType.Product:
          this._string.Append("$_product");
          break;
        case NodeType.Sum:
          this._string.Append("$_sum");
          break;
        default:
          this._string.Append("??"); // REVIEW: What would be a good default?
          break;
      }
      this.VisitComprehension(quantifier.Comprehension);
      return quantifier;
    }
    public override Expression VisitComprehension(Comprehension comprehension) 
    {
      if (comprehension == null) return null;
      if (this._string == null){ this._string = new StringBuilder(); }

      if (comprehension.nonEnumerableTypeCtor != null){
        // then this comprehension represents an expression of the form "new T{...}"
        this._string.Append("new");
        this.AddType(comprehension.Type);
        this._string.Append(",");
        this.Visit(comprehension.AddMethod);
        this._string.Append(",");
        this.Visit(comprehension.nonEnumerableTypeCtor);
        this._string.Append(",");
      }

      if (comprehension.IsDisplay){
        this._string.Append("(|");
        TypeNode t = comprehension.nonEnumerableTypeCtor == null ? comprehension.Type : comprehension.TemporaryHackToHoldType;
        t = TypeNode.StripModifiers(t);
        Debug.Assert(t != null && t.TemplateArguments != null && t.TemplateArguments.Count == 1, "ContractSerializer: bad display");
        this.AddType(t.TemplateArguments[0]);
        this._string.Append(",");
        this.VisitExpressionList(comprehension.Elements);
        this._string.Append("|)");
      }else{
        if (comprehension.BindingsAndFilters == null) return null;
        this._string.Append("{|");
        for (int i = 0, n = comprehension.BindingsAndFilters.Count; i < n; i++){
          ComprehensionBinding b = comprehension.BindingsAndFilters[i] as ComprehensionBinding;
          if (b != null){
            this._string.Append("{");
            this.AddType(b.TargetVariableType);
            this._string.Append(",");
            this.VisitExpression(b.TargetVariable);
            this._string.Append(",");
            this.VisitExpression(b.SourceEnumerable);
            this._string.Append("}");
          }
          else
          { // filter
            this._string.Append("(");
            this.VisitExpression(comprehension.BindingsAndFilters[i]);
            this._string.Append(")");
          }
        }
        this._string.Append(";");
        this.VisitExpression(comprehension.Elements[0]);
        if (comprehension.Elements.Count > 1){
          this._string.Append(";");
          this.VisitExpression(comprehension.Elements[1]);
        }
        this._string.Append("|}");
      }
      return comprehension;
    }

    public override ExpressionList VisitExpressionList (ExpressionList expressions) {
      if (expressions == null) return null;
      if (this._string == null){ this._string = new StringBuilder(); }
      for (int i = 0, n = expressions.Count; i < n; i++) {
        if (i > 0)
          this._string.Append(",");
        expressions[i] = this.VisitExpression(expressions[i]);
      }
      return expressions;
    }

    public override ParameterList VisitParameterList (ParameterList pl) {
      if (pl == null) return null;
      if (this._string == null){ this._string = new StringBuilder(); }
      this._string.Append("(");
      for (int i = 0, n = pl.Count; i < n; i++) {
        if (i > 0)
          this._string.Append(",");
        this.AddType(pl[i].Type);
      }
      this._string.Append(")");
      return pl;
    }

		
    public override Expression VisitLiteral (Literal literal) {
      if (this._string == null){ this._string = new StringBuilder(); }
      if (literal.Value == null) {
        this._string.Append("null");
      }
      else if (literal.Value is string) {
        this._string.AppendFormat("\"{0}\"", literal.Value.ToString());
      }
      else if (literal.Value is char) {
        char c = (char)literal.Value;
        if ((int)c <= 16384) {
          // This number must match the one in Tab.cs in our version of
          // Coco as the value of the field "charSetSize" in the class
          // CharClass.
          this._string.AppendFormat("'{0}'", literal.Value.ToString());
        } else {
          int cValue = c;
          this._string.AppendFormat("'\\u{0}'", cValue.ToString());
        }
      }
      else if (literal.Value is string) {
        this._string.AppendFormat("\"{0}\"", literal.Value.ToString());
      }
      else if (literal.Value is TypeNode){
        TypeNode t = literal.Value as TypeNode;
        this.AddType(t);
      }else{
        this._string.Append(literal.ToString());
      }
      return literal;
    }


    public override Expression VisitImplicitThis(ImplicitThis implicitThis) {
      if (this._string == null){ this._string = new StringBuilder(); }
      if (this.currentClosureLocal == null) {
        this._string.Append("this");
      } else {
        this.Visit(this.currentClosureLocal);
      }
      return implicitThis;
    }

    public override Expression VisitLocal(Local local) {
      if (this._string == null){ this._string = new StringBuilder(); }
      // must be special names. Encoding is ${<type>, "<name>"}
      this._string.Append("${");
      this.AddType(local.Type);
      this._string.Append(",\"");
      if (local.Name != null && local.Name.Name != null)
        this._string.Append(local.Name.Name);
      this._string.Append("\"}");
      return local;
    }

    public override Expression VisitReturnValue(ReturnValue value)
    {
      if (this._string == null) { this._string = new StringBuilder(); }
      // must be special names. Encoding is ${<type>, "<name>"}
      this._string.Append("${");
      this.AddType(value.Type);
      this._string.Append(",\"return value\"}");
      return value;
    }



    public override Expression VisitOldExpression(OldExpression oldExpression) {
      if (oldExpression == null || oldExpression.expression == null) return null;
      if (this._string == null){ this._string = new StringBuilder(); }
      this._string.Append("\\old(");
      this.VisitExpression(oldExpression.expression);
      this._string.Append(")");
      return oldExpression;
    }

    public override Expression VisitMemberBinding (MemberBinding memberBinding) {
      if (memberBinding == null || memberBinding.BoundMember == null) return null;
      if (this._string == null){ this._string = new StringBuilder(); }
      ParameterField pf = memberBinding.BoundMember as ParameterField;
      if (pf != null){
        this._string.Append("$");
        this._string.Append(pf.Parameter.ArgumentListIndex);
      }
      else {
        if (memberBinding.BoundMember != null && memberBinding.BoundMember.DeclaringType is BlockScope){
          this.Visit(memberBinding.BoundMember);
        }else if (memberBinding.TargetObject != null && memberBinding.BoundMember != null){
          this.VisitExpression(memberBinding.TargetObject);
          this._string.Append("@");
          this.Visit(memberBinding.BoundMember);
        }else if (memberBinding.TargetObject == null){
          if (memberBinding.BoundMember.IsStatic){
            this.Visit(memberBinding.BoundMember);
            return memberBinding;
          } else if (memberBinding.BoundMember is InstanceInitializer) {
            this.VisitMethod((InstanceInitializer)memberBinding.BoundMember); //dealing with constructor no different from dealing with other calls.
          }
        }
      }
      return memberBinding;
    }


    /// <summary>
    /// Used to print the field name from a member binding, nothing more
    /// </summary>
    public override Field VisitField (Field field) {
      if (field == null || field.DeclaringType == null || field.Name == null || field.Name.Name == null)
        return null;
      if (this._string == null){ this._string = new StringBuilder(); }
      if (field.DeclaringType is BlockScope)
      {
        this._string.Append("$blockVar(");
        this.AddType(field.Type);
        this._string.Append("){");
        this.AddIdentifier(field.Name.Name);
        //this._string.Append(field.Name.Name);
        this._string.Append("}");
      }
      else 
      {
        this.AddType(field.DeclaringType);
        this._string.Append("::");
        this.AddIdentifier(field.Name.Name);
        //this._string.Append(field.Name.Name);
      }
      return field;
    }
    /// <summary>
    /// Used to print the method name, nothing more
    /// </summary>
    public override Method VisitMethod (Method method) {
      if (method == null || method.DeclaringType == null || method.Name == null || method.Name.Name == null)
        return null;
      if (this._string == null){ this._string = new StringBuilder(); }
      this.AddType(method.DeclaringType);
      this._string.Append("::");
      if (method.IsGeneric) {
        this._string.Append(method.Template.Name);
        this._string.Append("<");
        for (int i = 0, n = method.TemplateArguments.Count; i < n; i++) {
          if (i > 0)
            this._string.Append(",");
          this.AddType(method.TemplateArguments[i]);
        }
        this._string.Append(">");
      } else {
        this._string.Append(method.Name.Name);
      }
      this.VisitParameterList(method.Parameters);
      return method;
    }
    public override Expression VisitConstruct(Construct cons) {
      if (cons == null || cons.Constructor == null) return null;      
      if (this._string == null){ this._string = new StringBuilder(); }
      this.VisitExpression(cons.Constructor);
      this._string.Append("{");
      this.VisitExpressionList(cons.Operands);
      this._string.Append("}");
      return cons;
    }
    public override Expression VisitMethodCall (MethodCall call) {
      if (call == null || call.Callee == null) return null;

      #region Special case for System.Array.get_Length
      MemberBinding mb = call.Callee as MemberBinding;
      if (mb != null && mb.TargetObject != null && mb.TargetObject.Type != null)
      {
        ArrayType at = TypeNode.StripModifiers(mb.TargetObject.Type) as ArrayType;
        if (at != null && at.Rank == 1 && CoreSystemTypes.Array != null &&
            mb.BoundMember == CoreSystemTypes.Array.GetMethod(Identifier.For("get_Length"), null))
        {
          this.VisitUnaryExpression(new UnaryExpression(new UnaryExpression(mb.TargetObject, NodeType.Ldlen, SystemTypes.IntPtr), NodeType.Conv_I4, SystemTypes.Int32));
          return call;
        }
      }
      #endregion

      if (this._string == null) { this._string = new StringBuilder(); }
      this.VisitExpression(call.Callee);
      this._string.Append("{");
      this.VisitExpressionList(call.Operands);
      this._string.Append("}");
      return call;
    }

    public override Expression VisitParameter (Parameter parameter) {
      if (parameter == null || parameter.Name == null) return null;
      if (this._string == null){ this._string = new StringBuilder(); }
      this._string.Append("$");
      this._string.Append(parameter.ArgumentListIndex);
      return parameter;
    }


    public override Expression VisitBase(Base Base) {
      if (this._string == null) { this._string = new StringBuilder(); }
      this._string.Append("this");
      return Base;
    }
    public override Expression VisitThis(This This) {
      if (this._string == null){ this._string = new StringBuilder(); }
      this._string.Append("this");
      return This;
    }

    public override TypeModifier VisitTypeModifier(TypeModifier typeModifier) {
      if (this._string == null){ this._string = new StringBuilder(); }
      this._string.Append(typeModifier.TypeCode.ToString());
      this._string.Append("(");
      this.Visit(typeModifier.Modifier);
      this._string.Append(",");
      this.Visit(typeModifier.ModifiedType);
      this._string.Append(")");
      return typeModifier; // base.VisitTypeModifier (typeModifier);
    }


    public override Expression VisitIndexer(Indexer indexer) {
      if (indexer == null) return null;
      if (this._string == null){ this._string = new StringBuilder(); }
      if (indexer.CorrespondingDefaultIndexedProperty != null) {
        this.VisitExpression(indexer.Object);
        this._string.Append("@");
        this.VisitMethod(indexer.CorrespondingDefaultIndexedProperty.Getter);
        this._string.Append("{");
        this.VisitExpressionList(indexer.Operands);
        this._string.Append("}");
      }
      else {
        this.VisitExpression(indexer.Object);
        this._string.Append("[");
        this.VisitExpressionList(indexer.Operands);
        this._string.Append("](");
        if (indexer.ElementType != null)
        {
          this.AddType(indexer.ElementType); // Due to implicit coercions, don't use indexer.Type
        }
        else
        {
          this.AddType(indexer.Type);
        }
        this._string.Append(")");

      }
      return indexer;
    }


    private void AddType (TypeNode tn) {
      if (this._string == null){ this._string = new StringBuilder(); }
      if (tn == null) return;
      OptionalModifier tnm = tn as OptionalModifier;
      if (tnm != null) {
        this._string.Append("optional(");
        this.AddType(tnm.Modifier);
        this._string.Append(",");
        AddType(tnm.ModifiedType);
        this._string.Append(")");
        return;
      }

      if (tn == SystemTypes.Object) {
        this._string.Append("object");
      } else if (tn == SystemTypes.String) {
        this._string.Append("string");
      } else if (tn == SystemTypes.Char) {
        this._string.Append("char");
      } else if (tn == SystemTypes.Void) {
        this._string.Append("void");
      } else if (tn == SystemTypes.Boolean) {
        this._string.Append("bool");
      } else if (tn == SystemTypes.Int8) {
        this._string.Append("i8");
      } else if (tn == SystemTypes.Int16) {
        this._string.Append("i16");
      } else if (tn == SystemTypes.Int32) {
        this._string.Append("i32");
      } else if (tn == SystemTypes.Int64) {
        this._string.Append("i64");
      } else if (tn == SystemTypes.UInt8) {
        this._string.Append("u8");
      } else if (tn == SystemTypes.UInt16) {
        this._string.Append("u16");
      } else if (tn == SystemTypes.UInt32) {
        this._string.Append("u32");
      } else if (tn == SystemTypes.UInt64) {
        this._string.Append("u64");
      } else if (tn == SystemTypes.Single) {
        this._string.Append("single");
      } else if (tn == SystemTypes.Double) {
        this._string.Append("double");
      } else if (tn.IsTemplateParameter) {
        if (tn is MethodTypeParameter || tn is MethodClassParameter) {
          this._string.Append("$methodTypeParam(");
        } else {
          this._string.Append("$typeParam(");
        }
        this._string.Append(tn.Name);
        this._string.Append(")");
      } else if (tn is ArrayType) {
        ArrayType arrayType = (ArrayType)tn;
        this.AddType(arrayType.ElementType);
        this._string.Append("[]");
      } else if (tn is Reference) {
        Reference reference = (Reference)tn;
        this._string.Append("&");
        this.AddType(reference.ElementType);
      } else if (tn is Pointer) {
        Pointer pointer = (Pointer)tn;
        this.AddType(pointer.ElementType);
        this._string.Append("*");
      } else {
        if (this.currentModule != tn.DeclaringModule
          && tn.DeclaringModule != null
          && tn.DeclaringModule.ContainingAssembly != null
          && tn.DeclaringModule.ContainingAssembly.Name != null)
          this._string.AppendFormat("[{0}]", tn.DeclaringModule.ContainingAssembly.Name);

        TypeNode template = tn.Template == null ? tn : tn.Template;
        if (template.FullName != null)
          this._string.Append(template.FullName);
        if (tn.Template != null) {
          this._string.Append("<");
          TypeNodeList args = tn.ConsolidatedTemplateArguments;
          for (int i = 0; i < args.Count; i++) {
            if (0 < i)
              this._string.Append(",");
            this.AddType(args[i]);
          }
          this._string.Append(">");
        }
      }

    }

    void AddIdentifier(string name) {
      bool weird = name.IndexOf(':') >= 0 || name.IndexOf(' ') >= 0;
      if (!weird)
        this._string.Append(name);
      else {
        this._string.Append("$weirdIdent\"");
        this._string.Append(name);
        this._string.Append('"');
      }
    }
  }
}
