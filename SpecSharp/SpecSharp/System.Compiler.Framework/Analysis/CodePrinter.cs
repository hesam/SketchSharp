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
  using System.Text;
  using System.IO;
  using System.Collections;


  
  /// <summary>
  /// Prints some information attached to a statement.
  /// </summary>
  public delegate string DGetStatInfo(Statement stat);
  /// <summary>
  /// Prints some information attached to a block.
  /// </summary>
  public delegate string DGetBlockInfo(Block block);

  /// <summary>
  /// Helper class for printing an ASCII representation of the representation
  /// produced by CciHelper.
  /// </summary>
  public abstract class CodePrinter {

    public static bool NoUniqueKeys = false;

    /// <summary>
    /// Convenient version of <c>b2s</c>: <c>b2id</c> is null.
    /// </summary>
    public static string b2s(Block block) {
      return b2s(block, null);
    }

    /// <summary>
    /// Convenient wrapping of <c>CodePrinter.b2s</c>; used in conjunction with
    /// <c>DataStructUtil.IEnum2String</c> for printing collections of blocks.
    /// It would have been some much easier to have a nice Block.ToString() method ...
    /// </summary>
    public static DObj2String BlockShortPrinter = new DObj2String(bo2s);
    private static string bo2s(object block) { return CodePrinter.b2s((Block) block); }

    public static DObj2String VariablePrinter = new DObj2String(varo2s);
    private static string varo2s(object var) { return ((Variable) var).Name.ToString(); }

    /// <summary>
    /// Returns a string id for "block" based on the integer identified b2id[block].
    /// If <c>b2id</c> is null, then the block hashcode is used as the block identifier.
    /// </summary>
    /// <returns></returns>
    public static string b2s(Block block, Hashtable/*<Block,int>*/ b2id) {
      string name =  (b2id == null || ! b2id.ContainsKey(block)) ? "b???" : "b" + (int) b2id[block];
      if (NoUniqueKeys) 
      {
        return name;
      }
      else
      {
        return name + "(UKEY=" + block.UniqueKey + ")";
      }
    }

    public static void PrintMethod(Method method) 
    {
      TextWriter tw = new StreamWriter(File.Open(@"c:\temp\cfglog", FileMode.Append, FileAccess.Write, FileShare.Read));

      PrintMethod(tw, method);

      tw.Close();
    }

    /// <summary>
    /// Method pretty-printer.  Note: the method blocks are identified by their 0-based index in the
    /// method list of blocks.
    /// </summary>
    /// <param name="tw">Where to print.</param>
    /// <param name="method">The method to be printed</param>
    public static void PrintMethod(TextWriter tw, Method method) {
      Hashtable/*<Block,int>*/ b2id = get_b2id(method);
      tw.WriteLine(MethodSignature(method));
      show_locals(tw, method);
      show_body(tw, method, b2id);
      show_exception_handlers(tw, method, b2id);
      tw.WriteLine();
    }


    private static Hashtable/*<Block,int>*/ get_b2id(Method method) {
      Hashtable b2id = null;
      if((method.Body != null) && (method.Body.Statements != null)) {
        b2id = new Hashtable();
        StatementList stats = method.Body.Statements;
        for(int i = 0, n = stats.Count; i < n; i++)
          b2id[stats[i]] = i;
      }
      return b2id;
    }


    /// <summary>
    /// Returns a string describing the signature of <c>method</c>: return type, method name, 
    /// argument types and names (param names are not really part of the method signature, but
    /// it's nice to know them anyway). 
    /// </summary>
    public static string MethodSignature(Method method) {
      StringBuilder signature = new StringBuilder();
      if(CciHelper.IsStatic(method)) signature.Append("static ");
      if(CciHelper.IsAbstract(method))
        signature.Append("abstract ");
      else if(CciHelper.IsVirtual(method))
        signature.Append("virtual ");
      signature.Append(method.ReturnType.FullName)
        .Append(" ")
        .Append(method.DeclaringType.FullName).Append(".").Append(method.Name)
        .Append("(");
      for(int i = 0, n = (method.Parameters != null)?method.Parameters.Count:0; i < n; i++) {
        Parameter p = method.Parameters[i];
        if(i != 0) signature.Append(", ");
        signature.Append(p.Type.FullName).Append(" ").Append(p.Name);
      }
      signature.Append(")");
      return signature.ToString();
    }

    /// <summary>
    /// Fully qualified name of <c>method</c>: fullname(declaring_class).method_name
    /// </summary>
    public static string FullName(Method method) {
      return method.DeclaringType.FullName + "." + method.Name;
    }


    public static string FullName(FunctionPointer fp) 
    {
      StringBuilder sb = new StringBuilder();
      // CciHelper's FullName only returns the return type of the function pointer, not the parameters
      sb.Append(fp.FullName);
      sb.Append("(");
      for(int i=0; i < fp.ParameterTypes.Count; i++) 
      {
        sb.Append(fp.ParameterTypes[i].FullName);
        if (i==fp.ParameterTypes.Count-1) 
        {
          sb.Append(")");
        }
        else 
        {
          sb.Append(",");
        }
      }
      return sb.ToString();
    }

    private static void show_locals(TextWriter tw, Method method) {
      // TODO
    }

    private static void show_exception_handlers(TextWriter tw, Method method, Hashtable/*<Block,int>*/ b2id) {
      ExceptionHandlerList ehl = method.ExceptionHandlers;
      if((ehl == null) || (ehl.Count == 0))
        return;

      tw.WriteLine("Exception handlers:");
      for(int i = 0, n = ehl.Count; i < n; i++) {
        ExceptionHandler eh = ehl[i];

        tw.Write("  eh_{0}: ",i);
        switch(eh.HandlerType) {
          case NodeType.Catch:
            tw.WriteLine("catch({0})", eh.FilterType.FullName);
            break;
          case NodeType.Filter:
            tw.WriteLine("filter - filterblock " + b2s(eh.FilterExpression));
            break;
          case NodeType.Finally:
            tw.WriteLine("finally");
            break;
          case NodeType.FaultHandler:
            tw.WriteLine("fault_handler");
            break;
          default:
            tw.WriteLine("UNKNOWN HANDLER TYPE");
            break;
        }

        tw.WriteLine("      body = [{0},{1}); prot = [{2},{3})",
          b2s(eh.HandlerStartBlock, b2id), b2s(eh.BlockAfterHandlerEnd, b2id),
          b2s(eh.TryStartBlock, b2id), b2s(eh.BlockAfterTryEnd, b2id));
      }
    }
    
    private static void show_body(TextWriter tw, Method method, Hashtable/*<Block,int>*/ b2id) {
      if((method.Body == null) || (method.Body.Statements == null)) return;
      StatementList blocks = method.Body.Statements;
      // display blocks
      for(int i = 0, n = blocks.Count; i < n; i++)
        PrintBlock(tw, (Block) blocks[i], b2id);
    }


    /// <summary>
    /// Block pretty-printer.
    /// </summary>
    /// <param name="tw">Where to print.</param>
    /// <param name="block">What to print</param>
    /// <param name="get_stat_info">Provider of statement specific information;
    /// if non-null, it will be called after printing each statement, and its result
    /// printed too.</param>
    /// <param name="b2id">Map block -&gt; int identifier; if <c>null</c>, then the block
    /// UniqueKey is used as block id.</param>
    public static void PrintBlock(TextWriter tw, Block block,
      DGetStatInfo get_stat_info, Hashtable/*<Block,int>*/ b2id) {
      string bn = b2s(block, b2id) + ": ";
      // hack to make it look nicer! tab is more elegant, but it's also too long.
      // we just hope that methods with more that 99 blocks are not that frequent.
      if(bn.Length < 5) bn += " ";
      string buff = "";
      for(int i = 0; i < bn.Length; i++)
        buff += " ";

      if(block.Statements.Count == 0)
        tw.WriteLine(bn + "EMPTY BLOCK");

      for(int i = 0, n = block.Statements.Count; i < n; i++) {
        tw.Write((i == 0) ? bn : buff);
        Statement stat = block.Statements[i];
        if(stat is Block)
          throw new ApplicationException("nested blocks");
        tw.Write(StatementToString(stat, b2id));
        tw.WriteLine(";");
        if(get_stat_info != null)
          tw.WriteLine(get_stat_info(stat));
      }
    }        

    /// <summary>
    /// Convenient version of the block pretty-printer, with no statement
    /// specific information and block.UniqueKey used as block id.
    /// </summary>
    /// <param name="tw">Where to print.</param>
    /// <param name="block">What to print.</param>
    public static void PrintBlock(TextWriter tw, Block block) {
      PrintBlock(tw, block, null, null);
    }

    public static void PrintBlock(TextWriter tw, Block block, DGetStatInfo get_stat_info) {
      PrintBlock(tw, block, get_stat_info, null);
    }

    public static void PrintBlock(TextWriter tw, Block block, Hashtable/*<Block,int>*/ b2id) {
      PrintBlock(tw, block, null, b2id);
    }


    /// <summary>
    /// Convenient statement pretty-printer; the <c>b2id</c> map is <c>null</c>.
    /// </summary>
    public static string StatementToString(Statement stat) {
      return StatementToString(stat, null);
    }


    /// <summary>
    /// Returns a textual representation of the source context info attached to statement <c>stat</c>.
    /// </summary>
    /// <param name="stat">Statement whose source we're interested in.</param>
    /// <returns>Textual representation of the source context info for <c>stat</c>.</returns>
    public static string SourceContextToString(Statement stat)
    {
      SourceContext sc = stat.SourceContext;
      // TODO
      // ideally, you want filename:linenumber
      return "(?,?)";
    }

    /// <summary>
    /// Statement pretty-printer.
    /// </summary>
    /// <param name="stat">Statement to print</param>
    /// <param name="b2id">Map block -> integer id; if <c>null</c>, the block UniqueKey will be used instead.</param>
    /// <returns>String representation of the statement argument.</returns>
    public static string StatementToString(Statement stat, Hashtable b2id) {
      // this primitive, non-OO dispatch should really be done through virtual methods
      // all xxxToString() methods should really be ToString methods of the different
      // Statement subclasses.
      switch(stat.NodeType) {
        case NodeType.Nop:
          if(stat is MethodHeader)
            return MethodHeaderToString((MethodHeader) stat);
          if(stat is Unwind)
            return "unwind";
          return "nop";
        case NodeType.AssignmentStatement:
          return AssignmentStatementToString((AssignmentStatement) stat);
        case NodeType.ExpressionStatement:
          return ExpressionStatementToString((ExpressionStatement) stat);
        case NodeType.Branch:
          return BranchToString((Branch) stat, b2id);
        case NodeType.Return:
          return ReturnToString((Return) stat);
        case NodeType.Throw:
          return ThrowToString((Throw) stat);
        case NodeType.Rethrow:
          return RethrowToString((Throw) stat);
        case NodeType.SwitchInstruction:
          return SwitchInstructionToString((SwitchInstruction) stat, b2id);
        case NodeType.Catch:
          return CatchToString((Catch) stat);
        case NodeType.Filter:
          return FilterToString((Filter)stat);
        case NodeType.EndFinally:
          return EndFinallyToString((EndFinally) stat);
        case NodeType.EndFilter:
          return EndFilterToString((EndFilter) stat);
        case NodeType.DebugBreak:
          return "break";
        case NodeType.SwitchCaseBottom:
          return "switch case bottom";
		  case NodeType.Assertion:
			  return "assert " + expression2str(((Assertion)stat).Condition);
        default:
          return "(unknown statement type: " + stat.NodeType + ")";
      }
    }


    private static string MethodHeaderToString(MethodHeader header)
    {
      return "MethodHeader ("+ header.method.FullName + "); params = " + 
        DataStructUtil.IEnum2String(header.parameters, TypedVarPrinter);
    }

    private static string typedvar2s(object obj)
    {
      Variable var = (Variable) obj;
      return var.Type.FullName + " " + var.Name;
    }
    public static DObj2String TypedVarPrinter = new DObj2String(typedvar2s);

    private static string AssignmentStatementToString(AssignmentStatement stat) {
      if (stat.Target.NodeType == NodeType.AddressDereference && stat.Target.Type != null && stat.Target.Type.IsValueType &&
        stat.Source is Literal && ((Literal)stat.Source).Value == null) {
        return "initobj " + expression2str(stat.Target);
      }
      return 
        expression2str(stat.Target) +
        " " + op2str(stat.Operator) + " " +
        expression2str(stat.Source);
    }

    private static string ExpressionStatementToString(ExpressionStatement stat) {
      if(CciHelper.IsPopStatement(stat))
        return "POP_STAT";
      Expression expr = stat.Expression;
      System.Diagnostics.Debug.Assert(expr != null, "ExpressionStatement should not have null expression here.");
      return expression2str(expr);
    }

    private static string BranchToString(Branch b, Hashtable/*<Block,int>*/ b2id) {
      string instr_name = b.LeavesExceptionBlock ? "leave" : "jumpto";
      Expression condition = b.Condition;
      if(condition == null)
        return instr_name + " " + b2s(b.Target, b2id);
      else
        return "if(" + expression2str(condition) + ") " + instr_name + " " + b2s(b.Target, b2id);
    }

    private static string ReturnToString(Return ret) {
      return
        "return" + ((ret.Expression != null) ? (" " + expression2str(ret.Expression)) : "");
    }

    private static string ThrowToString(Throw thrw) {
      return
        (thrw.Expression != null) ? ("throw " + expression2str(thrw.Expression)) : "rethrow";
    }

    private static string RethrowToString(Throw thrw) 
    {
      return "rethrow";
    }

    private static string SwitchInstructionToString(SwitchInstruction sw, 
      Hashtable/*<Block,int>*/ b2id) 
    {
      StringBuilder sb = new StringBuilder();
      sb.Append("switch (");
      sb.Append(expression2str(sw.Expression, true));
      sb.Append(") [");
      BlockList targets = sw.Targets;
      for(int i = 0, n = targets.Count; i < n; i++) {
        if(i > 0) sb.Append(",");
        sb.Append(b2s(targets[i], b2id));
      }
      sb.Append("]");
      return sb.ToString();
    }

    private static string CatchToString(Catch c) 
    {
      StringBuilder sb = new StringBuilder();
      if(c.Variable == null)
        sb.Append("top_of_stack");
      else
        sb.Append(CodePrinter.expression2str(c.Variable));
      sb.Append(" = catch(");
      if (c.Type != null)
        sb.Append(c.Type.FullName);
      sb.Append(")");
      return sb.ToString();
    }

    private static string FilterToString(Filter f) 
    {
      StringBuilder sb = new StringBuilder();
      sb.Append(" = filter(");
      sb.Append(")");
      return sb.ToString();
    }

    private static string EndFinallyToString(EndFinally endfinally) {
      return "endfinally/endfault";
    }

    private static string EndFilterToString(EndFilter endfilter) {
      string exprString;

      if (endfilter.Value != null) 
      {
        exprString = CodePrinter.expression2str(endfilter.Value);
      }
      else 
      {
        exprString = "";
      }
      return "endfilter " + exprString;
    }


    /// <summary>
    /// Expression pretty-printer.
    /// </summary>
    /// <param name="expr">Expression to print.</param>
    /// <returns>String representation of the expression argument.</returns>
    public static string ExpressionToString(Expression expr) {
      return expression2str(expr, true);
    }


    private static string expression2str(Expression expr, bool top_level) {
      string expr_str = expression2str_aux(expr);
      if(!top_level &&
         (((expr is BinaryExpression) && (expr.NodeType != NodeType.MemberBinding)) || 
          (expr is UnaryExpression) ||
          (expr is Indexer)))
        return "(" + expr_str + ")";
      return expr_str;
    }

    private static string expression2str(Expression expr) {
      return expression2str(expr, true);
    }

    private static string expression2str_aux(Expression expr) {
      if(expr ==null)
        return "NULL";
      if(expr is TernaryExpression)
      {
        switch(expr.NodeType)
        {
          case NodeType.Initblk:
            return initblk2str((TernaryExpression) expr);
          case NodeType.Cpblk:
            return cpblk2str((TernaryExpression) expr);
          case NodeType.Conditional:
            return "(" + expression2str(((TernaryExpression) expr).Operand1) + 
              " ? " + expression2str(((TernaryExpression) expr).Operand2) + 
              " : " + expression2str(((TernaryExpression) expr).Operand3) + ")";
          default:
            return "(unknown ternary expression type " + expr.NodeType + ")";
        }
      }

      if(expr is BinaryExpression)
        return binaryexpr2str((BinaryExpression) expr);

      if(expr is UnaryExpression)
        return unaryexpr2str((UnaryExpression) expr);

      switch(expr.NodeType) {
        case NodeType.Local:
          return local2str((Local) expr);
        case NodeType.Literal:
          return literal2str((Literal) expr);
        case NodeType.Parameter:
        case NodeType.This:
          return parameter2str((Parameter) expr);
        case NodeType.MemberBinding:
          return memberbinding2str((MemberBinding) expr);

        case NodeType.Pop:
          if(expr is UnaryExpression)
            throw new ApplicationException("this should not occur");
          else
            return "EPOP";

        case NodeType.MethodCall:
        case NodeType.Call:
        case NodeType.Calli:
        case NodeType.Callvirt:
          return methodcall2str((MethodCall) expr);

        case NodeType.Indexer:
          return indexer2str((Indexer) expr);

        case NodeType.Construct:      // new
          return construct2str((Construct) expr);
        case NodeType.ConstructArray: // anew
          return constructarray2str((ConstructArray) expr);

        case NodeType.AddressDereference:
          return addrderef2str((AddressDereference) expr);

        default:
          return "expression(" + expr.NodeType + ")";
      }
    }


    private static string initblk2str(TernaryExpression te)
    {
      return "initblk " + 
        expression2str(te.Operand1, false) + "," + 
        expression2str(te.Operand2, false) + "," +
        expression2str(te.Operand3, false);
    }


    private static string cpblk2str(TernaryExpression te)
    {
      return "cpblk " + 
        expression2str(te.Operand1, false) + "," + 
        expression2str(te.Operand2, false) + "," +
        expression2str(te.Operand3, false);
    }


    private static string addrderef2str(AddressDereference ad) {
      return "*" + expression2str(ad.Address, false);
    }

    private static string indexer2str(Indexer i) {
      return 
        expression2str(i.Object, false) +
        "[" + exprlist2str(i.Operands) + "]";
    }

    private static string binaryexpr2str(BinaryExpression be) {
      // special case: Castclass
      if(be.NodeType == NodeType.Castclass)
        return 
          "(" + expression2str(be.Operand2) + ") " +
          expression2str(be.Operand1);
      // special case: Box
      if(be.NodeType == NodeType.Box)
        return
          "(BOX " + expression2str(be.Operand2) + ") " +
          expression2str(be.Operand1);

      // special case: Unbox
      if(be.NodeType == NodeType.Unbox)
        return
          "(UNBOX " + expression2str(be.Operand2) + ") " +
          expression2str(be.Operand1);

      return
        expression2str(be.Operand1) +
        " " + op2str(be.NodeType) + " " + 
        expression2str(be.Operand2);
    }

    private static string unaryexpr2str(UnaryExpression ue) {
      // special case: Ldlen <-> .length
      if(ue.NodeType == NodeType.Ldlen)
        return expression2str(ue.Operand) + ".length";
      return op2str(ue.NodeType) + " " + expression2str(ue.Operand, false);
    }

    private static string op2str(NodeType type) {
      switch(type) {
        case NodeType.Nop: // assignment
          return ":=";
        
        case NodeType.Add:
        case NodeType.Add_Ovf:
        case NodeType.Add_Ovf_Un:
          return "+";
        case NodeType.Sub:
        case NodeType.Sub_Ovf:
        case NodeType.Sub_Ovf_Un:
          return "-";
        case NodeType.Mul:
        case NodeType.Mul_Ovf:
        case NodeType.Mul_Ovf_Un:
          return "*";
        case NodeType.Div:
        case NodeType.Div_Un:
          return "/";
        case NodeType.Ceq:
          return "==";
        case NodeType.Cgt:
          return ">";
        case NodeType.Lt:
          return "<";
        case NodeType.Le:
          return "<=";
        case NodeType.Gt:
          return ">";
        case NodeType.Ge:
          return ">=";
        case NodeType.Eq:
          return "==";
        case NodeType.Ne:
          return "!==";
        case NodeType.And:
          return "and";
        case NodeType.Or:
          return "or";
        case NodeType.Not:
          return "!";
        case NodeType.Pop:
          return "OP_POP";
        case NodeType.Conv_I4:
          return "(int)";
        case NodeType.AddressOf:
        case NodeType.ReadOnlyAddressOf:
        case NodeType.RefAddress: // aliases
        case NodeType.OutAddress: // aliases
          return "&";
        case NodeType.Isinst:
          return "is";
        default:
          return "(" + type + ")";
      }
    }

    private static string local2str(Local local) {
      if (local.Name != null) 
      {
        if (local.Name.Name != "") 
        {
          return local.Name.ToString();
        }
      }
      return String.Format("local<{0}>", local.UniqueKey);
    }

    private static string parameter2str(Parameter param) {
      if(param.Name == null)
        return "this";
      else
        return param.Name.ToString();
    }

    private static string methodcall2str(MethodCall mcall) {
      return 
        expression2str(mcall.Callee, false) +
        "(" + exprlist2str(mcall.Operands) + ")";
    }

    private static string construct2str(Construct c) {
      // the constructor method is
      // (Method) ((MemberBinding) c.Constructor).BoundMember;
      return
        "new " + c.Type.FullName +
        "(" + exprlist2str(c.Operands) + ")";
    }

    private static string constructarray2str(ConstructArray ca) {
      return
        "anew " + ca.ElementType.FullName +
        "[" + exprlist2str(ca.Operands) + "]" +
        ((ca.Initializers == null) ? "" : ( "{" + exprlist2str(ca.Initializers)) + "}"); 
    }

    public static string exprlist2str(ExpressionList el) {
      if (el == null) return "";
      StringBuilder sb = new StringBuilder();
      for(int i = 0, n = el.Count; i < n; i++) {
        if(i != 0)
          sb.Append(", ");
        sb.Append(expression2str(el[i], true));
      }
      return sb.ToString();
    }

    public static string vararray2str(Variable[] va) 
    {
      StringBuilder sb = new StringBuilder();
      for(int i = 0, n = va.Length; i < n; i++) 
      {
        if(i != 0)
          sb.Append(", ");
        sb.Append(expression2str(va[i], true));
      }
      return sb.ToString();
    }

    private static string literal2str(Literal lit) {
      object val = lit.Value;
      string str = null;
      if(val == null)
        str = "NULL";
      else if(val is string)
        str = "\"" + NormalizedString((string) val) + "\"";
      else if(val is TypeNode)
        str = ((TypeNode) val).FullName;
      else
        str= val.ToString();

      return str;// + " (" + lit.Type.FullName + ")";
    }

    // replaces each special character with an escape sequence, e.g. " -> \"
    public static string NormalizedString(string str)
    {
      StringBuilder sb = new StringBuilder();
      foreach(char c in str)
      {
        switch(c)
        {
          case '\"': sb.Append("\\\""); break;
          case '\n': sb.Append("\\n"); break;
          case '\t': sb.Append("\\t"); break;
          case '\\': sb.Append("\\\\"); break;
          default:   
            sb.Append(c); break;
        }
      }
      return sb.ToString();
    }

    private static string memberbinding2str(MemberBinding mb) {
      StringBuilder sb = new StringBuilder();
      if(mb.TargetObject == null) 
      {
        if (mb.BoundMember.DeclaringType != null) 
        {
          sb.Append(mb.BoundMember.DeclaringType.FullName);
        }
        else if (mb.BoundMember is FunctionPointer) 
        {
          FunctionPointer fp = (FunctionPointer)mb.BoundMember;

          return FullName(fp);
        }
        else 
        {
          return "(memberbinding2str found unknown memberbinding)";
        }
      }
      else
        sb.Append(expression2str(mb.TargetObject, false));
      sb.Append(".");
      Member member = mb.BoundMember;
      if((member is Method) && (mb.TargetObject != null))
      {
        sb.Append("{");
        sb.Append(member.DeclaringType.FullName);
        sb.Append("}");
      }
      sb.Append(mb.BoundMember.Name); 
      return sb.ToString();
    }

#if TEST

    /// <summary>
    /// Example use of the CodePrinter class: prints the code of all the methods
    /// from an application rooted in a specific assembly file.
    /// </summary>
    /// <param name="args">Root assembly of the analyzed program.</param>
    public static void TestMain(string[] args) {
      AssemblyNode an = AssemblyNode.GetAssembly(args[0]);
      if(an == null) {
        Console.WriteLine("assembly \"{0}\" not found", args[0]);
        Environment.Exit(1);
      }

      ClassHierarchy ch = new ClassHierarchy(args[0]); //DataStructUtil.singleton(an));
      foreach(Method method in ch.AllMethods()) {
        CodePrinter.PrintMethod(Console.Out, method);
      }
    }
#endif


  }

}
