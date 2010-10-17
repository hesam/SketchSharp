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
  using System.Collections;
  using System.Diagnostics;


  /// <summary>
	/// Exception thrown when an unknown quad is encountered.
	/// </summary>
	public class UnknownInstructionException : ApplicationException 
	{
		public UnknownInstructionException(string str) : base(str) {}
	}


	/// <summary>
	/// Decoder for CCI statements after normalization and code flattening. Provides a view of
	/// instructions close to CIL (.NET CLR Partition 3 document).
	/// The Visit method recognizes the CCI encodings for each instruction and calls
	/// the appropriate Visit* method.  Subclassing <c>InstructionVisitor</c> allows writing
	/// visitors that deal directly with only a small set of distinct instructions and without
	/// recursive expressions, since arguments to all instructions are variables.
	/// <p/>
	/// To make decoding simple, some MSIL instructions are split, such as ldtoken, which was
	/// split into three cases: LDTYPETOKEN, LDFIELDTOKEN, LDMETHODTOKEN.
	/// <p/>
	/// On the other hand, some instructions are distinguished by <c>null</c> tests; the MSIL instructions
	/// "ldfld" (load object field) and "ldsfld" (load static field) are modeled by the same visitor
	/// LDFIELD "dest := source.field" with source being <c>null</c> for a static field. Similarly, an unconditional
	/// branch instruction is modelled as an BRANCH with a <c>null</c> condition.
	/// <p/>
	/// The envisioned use of this class is as follows:  subclass <c>InstructionVisitor</c> and override
	/// each Visit* method
	/// (VisitCopy, VisitLoadConstant etc.) to specify the processing that you want for each instruction.
	/// Given a Cci statement (normalized and flattened) stat, do the appropriate processing for it by calling 
	/// Visit(stat, some_arg).
	/// <p/>
	/// Visit acts as a dispatcher: it recognizes the instruction encoded by stat and calls the
	/// appropriate Visit* method with the right arguments.  Visit stands for a top-level transfer function:
	/// its second argument is the argument passed to the appropriate transfer function; its result is the
	/// result of that transfer function.
	/// <p/>
	/// NOTE: With generics, we would have defined InstructionVisitor as Visitor&lt;A,B&gt;, where A and B
	/// are the argument type, respectively the result type of each transfer function.  If your transfer
	/// functions don't take any argument, you can pass <c>null</c>.
	/// Similarly, if they don't return anything, you can return <c>null</c>.
	/// <p/>
	/// See the documentation of each of the Visit* methods for more info on the format of each instruction.
	/// Also see the <c>SampleInstructionVisitor</c> for a full example.
	/// <p>
	/// The following conventions apply for each Visit* method:</p>
	/// 
	/// <ol>
	/// <li>The last two arguments are the original Cci statement and the argument passed to
	/// Visit(Statement stat, object arg) 
	/// </li>
	/// Having the original statements allows you to attach information to it (in some hashtable) or, in rare
	/// cases, to look for features that are not exposed.
	/// <li>Each method returns an object. This is conceptually the result of the transfer function associated with
	/// that instruction.</li>
	/// <li>Each such method is called by the dispatcher Visit(Statement stat, object arg) with all arguments
	/// non-null, unless specified otherwise in the documentation for Visit*.</li>
	/// </ol>
	/// 
	/// <p>
	/// By default, each Visit* method calls the abstract method <c>DefaultVisit</c>.</p>
	/// 
	/// </summary>
	public abstract class InstructionVisitor 
	{

    /// <summary>
    /// Decodes the statement and dispatches to one of the Visit methods below.
    /// </summary>
    /// <param name="stat">Visited statement.</param>
    /// <param name="arg">Argument for the transfer function.</param>
    /// <returns>Result of the appropriate transfer function.</returns>
    /// 
    public object Visit (Statement stat, object arg) {
      if (stat == null) return null;
      switch (stat.NodeType) {
        case NodeType.SwitchCaseBottom:
          return VisitSwitchCaseBottom(stat, arg);
        case NodeType.Nop:
          if(stat is MethodHeader) {
            MethodHeader header = (MethodHeader) stat;
            return VisitMethodEntry(header.method, header.parameters, stat, arg);
          }
          if (stat is Unwind) {
            return VisitUnwind(stat, arg);
          }
          return VisitNop(stat, arg);
        case NodeType.Branch:
          Branch branch = (Branch) stat;
          return VisitBranch((Variable) branch.Condition, branch.Target, stat, arg);
        case NodeType.SwitchInstruction:
          SwitchInstruction sw = (SwitchInstruction) stat;
          return VisitSwitch((Variable) sw.Expression, sw.Targets, stat, arg);
        case NodeType.Return:
          return VisitReturn((Variable) ((Return) stat).Expression, stat, arg);
        case NodeType.Throw: {
          Expression throwarg = ((Throw)stat).Expression;
          System.Diagnostics.Debug.Assert(throwarg != null, "Found throw with null arg.");
          return VisitThrow((Variable) throwarg, stat, arg);
        }
        case NodeType.Rethrow:
          return VisitRethrow(stat, arg);
        case NodeType.Catch:
          Catch c = (Catch) stat;
          return VisitCatch((Variable) c.Variable, c.Type, stat, arg);
        case NodeType.EndFinally:
          // after the stack has been removed and the finally blocks inlined, EndFinally is just a nop.
          return VisitNop(stat, arg);
        case NodeType.ExpressionStatement:
          return VisitExpressionStatement(stat, arg);
        case NodeType.AssignmentStatement:
          return VisitAssignmentStatement(stat, arg);

        case NodeType.Filter:
          return VisitFilter(StackVariable.Stack0, stat, arg);

        case NodeType.EndFilter:
          EndFilter ef = (EndFilter) stat;
          return VisitEndFilter((Variable) ef.Value, stat, arg);

        case NodeType.DebugBreak:
          return VisitBreak(stat, arg);

        case NodeType.Assertion:
          return VisitAssertion((Assertion) stat, arg);

        default:
          throw new UnknownInstructionException("untreated statement " + CodePrinter.StatementToString(stat));					
      }
    }


    #region visitor methods called from Visit()

		/// <summary>
		/// nop -- no operation
		/// </summary>
		protected virtual object VisitNop (Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


		/// <summary>
		/// method entry -- a pseudo instruction that marks the beginning of a method.
		/// </summary>
		/// <param name="method">Method that starts with this instruction.</param>
		/// <param name="parameters">All parameters of this method, including <c>this</c> (order is important).</param>
		protected virtual object VisitMethodEntry (Method method, IEnumerable/*<Parameter>*/ parameters, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


		/// <summary>
		/// unwind -- a pseudo instruction that marks the exceptional exit of a method. 
		/// <p>
		/// Description:
		/// <br/>
    /// This is the point where the currently thrown exception leaves the method.
		/// </p>
		/// </summary>
		protected virtual object VisitUnwind (Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


		/// <summary>
		/// branch cond,target -- branch on condition.
		/// <p>
		/// Description:
		/// <br/>
		/// Branch to target block if condition is true.
		/// </p>
		/// </summary>
		/// <param name="cond">Condition of the branch statement; <c>null</c> for an unconditional jump.</param>
		/// <param name="target">Target of the branching instruction.</param>
		protected virtual object VisitBranch (Variable cond, Block target, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


		/// <summary>
		/// switch selector,targets -- branch to target block indexed by selector
		/// <p>
		/// Description:
		/// <br/>
    /// if <c>selector</c> is between <c>0</c> and <c>targets.Length-1</c>c>
    /// then branch to targets[selector]. Otherwise, fall through.
		/// </p>
		/// </summary>
		/// <param name="selector">Selector variable.</param>
		/// <param name="targets">List of targets.</param>
		protected virtual object VisitSwitch (Variable selector, BlockList targets, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}

		
    /// <summary>
    /// switchcasebottom -- a pseudo instruction equal to nop. Marks the end of a switch case when introduced by
    /// the language parser. Allows checking for fall through from one case to another.
    /// </summary>
    /// <param name="stat">The source context should point at the case.</param>
    protected virtual object VisitSwitchCaseBottom (Statement stat, object arg) {
      return this.DefaultVisit(stat, arg);
    }

    /// <summary>
		/// return -- return from the method
		/// <p>
		/// Description:
		/// <br/>
		/// Return the value in var from the method.
		/// </p>
		/// </summary>
		/// <param name="var">Variable that holds the returned value. <c>null</c> if the method return type
		/// is <c>void</c>.</param>
		protected virtual object VisitReturn ( Variable var, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


		/// <summary>
		/// throw -- throw the exception
		/// </summary>
		/// <param name="var">Variable that holds the thrown value (never <c>null</c>).</param>
		protected virtual object VisitThrow ( Variable var, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}

		
    /// <summary>
    /// rethrow -- rethrow the currently handled exception
    /// <p>
    /// Description:
    /// <br/>
    /// Only appears within handlers. Its semantics is to rethrow the exception that the handler
    /// is currently processing.
    /// </p>
    /// </summary>
    protected virtual object VisitRethrow (Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
		/// var = catch(type) -- catch exception matching type and store in var
		/// <p>
		/// Description:
		/// <br/>
		/// Starts an exception handler and acts as the test whether the handler applies to the caught 
		/// exception given the type. If the exception does not apply, then control goes to the handler
		/// of the current block. Otherwise, control goes to the next instruction.
		/// </p>
		/// 
		/// </summary>
		/// <param name="var">Variable that holds the caught exception.</param>
		/// <param name="type">Type of the exceptions that are caught here.</param>
		protected virtual object VisitCatch (Variable var, TypeNode type, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
    /// dest := filter -- pseudo instruction marking the beginning of a filter handler.
    /// <p>
    /// Description:
    /// <br/>
    /// Semantics: assigns the exception being filtered to the <c>dest</c> variable.
    /// </p>
    /// </summary>
    protected virtual object VisitFilter (Variable dest, Statement stat, object arg) {
      return DefaultVisit(stat, arg);
    }


    /// <summary>
    /// endfilter code -- marks the end of the filter section of a filter handler. 
    /// <p>
    /// Description:
    /// <br/>
    /// tests the code at the end of the filter section of a filter handler. If code is 0, handler
    /// does not apply, and the next handler should be tried. If 1, hander applies and the next instruction
    /// is executed.
    /// 
    /// In our encoding, if the instruction falls through, it must push the implicit exception being 
    /// handled onto the stack, since the next instruction is a catch of the actual handler.
    /// </p>
    /// </summary>
    protected virtual object VisitEndFilter (Variable code, Statement stat, object arg)		{
      return DefaultVisit(stat, arg);
    }


    /// <summary>
    /// dest := null -- ldnull instruction, assigns null to destination.
    /// </summary>
    /// <param name="source">The <c>null</c> literal. Passed here for source context.</param>
    protected virtual object VisitLoadNull (Variable dest, Literal source, Statement stat, object arg) {
      return DefaultVisit(stat, arg);
    }


    /// <summary>
		/// dest := ldc c -- store the constant c in dest
		/// </summary>
		protected virtual object VisitLoadConstant (Variable dest, Literal source, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
		/// dest := arglist -- (corresponds to the MSIL instruction OxFE00 arglist).
		/// </summary>
		protected virtual object VisitArgumentList (Variable dest, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
		/// dest := sizeof(T) -- store the runtime size of type T in dest
		/// </summary>
		protected virtual object VisitSizeOf (Variable dest, TypeNode type, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
    /// dest := Constraint(T).receiver.callee(arguments) -- invoke virtual method with Constrain prefix.
    /// </summary>
    /// <param name="dest">Variable that stores the result of the call. <c>null</c> if the called method
    /// return type is <c>void</c> or ignored.</param>
    /// <param name="receiver">Receiver of the virtual call. Note that the behavior of this instruction depends on the
    /// type of the receiver in the generic instance:
    /// The receiver must have a reference type ref P. If the instance of P is a struct type, then the code becomes
    ///   st0 = box(receiver, T);
    ///   st0.callee(arguments);
    ///   
    /// If the instance of P is a reference type (class or interface), then we simply load the indirect pointer contents:
    ///   st0 = *receiver;
    ///   st0.callee(arguments);
    /// <br/>
    /// </param>
    /// <param name="callee">Compile-time called method; the method that is actually invoked might be different
    /// in the case of a dynamically dispatched call.</param>
    /// <param name="arguments">Call arguments; does not include the value for the "this" argument; that value is
    /// the given by the receiver (if any). All elements of this list are Variables.</param>
    /// <param name="constraint">The type constraint of the receiver</param>
    protected virtual object VisitConstrainedCall
      (Variable dest, Variable receiver, Method callee, ExpressionList/*<Variable>*/ arguments, TypeNode constraint,
      Statement stat, object arg) 
    {
      return DefaultVisit(stat, arg);
    }

		/// <summary>
		/// dest := source -- variable to variable assignment
		/// </summary>
		protected virtual object VisitCopy (Variable dest, Variable source, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}

		
		/// <summary>
		/// dest := new T -- allocate a new object of type T. DOES NOT INCLUDE .CTOR CALL, see below.
		/// 
		/// Called for a new expression. Note that MSIL newobj instructions are broken into 3 steps:
		/// 1. A separate allocation (this method)
		/// 2. A separate constructor call (normal call)
		/// 3. A separate assignment of the newly allocated object to the intended target variable.
		/// 
		/// </summary>
		/// <param name="dest">Temporary to hold raw allocation result</param>
		/// <param name="type">Object type to be allocated</param>
		/// <returns></returns>
		protected virtual object VisitNewObject (Variable dest, TypeNode type, Statement stat, object arg) 
		{
			return DefaultVisit(stat, arg);
		}


		/// <summary>
		/// dest := new T[size] -- allocate a new array of the given size and type
		/// </summary>
		protected virtual object VisitNewArray (Variable dest, TypeNode type, Variable size, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
    /// dest := receiver.callee(arguments) -- invoke method 
    /// </summary>
    /// <param name="dest">Variable that stores the result of the call. <c>null</c> if the called method
    /// return type is <c>void</c>.</param>
    /// <param name="receiver">Receiver for virtual calls. <c>null</c> in the case of a static call
    /// (warning: static call and call to a static method different things: you can call a virtual method
    /// without using dynamic dyspatch).
    /// <br/>
    /// If the <c>callee</c> is a member of a value type T, <c>receiver</c> is of type T&amp; (reference to T).
    /// </param>
    /// <param name="callee">Compile-time called method; the method that is actually invoked might be different
    /// in the case of a dynamically dispatched call.</param>
    /// <param name="arguments">Call arguments; does not include the value for the "this" argument; that value is
    /// the given by the receiver (if any). All elements of this list are Variables.</param>
    /// <param name="virtcall">Indicates whether this is a dynamically dispatched call or not.</param>
    protected virtual object VisitCall
      (Variable dest, Variable receiver, Method callee, ExpressionList/*<Variable>*/ arguments, bool virtcall,
      Statement stat, object arg) {
      return DefaultVisit(stat, arg);
    }


    /// <summary>
    /// dest := (*callee)([receiver], arguments) -- call indirect function pointer.
    /// </summary>
    /// <param name="dest">Variable that stores the result of the call. <c>null</c> if the called method
    /// return type is <c>void</c>.</param>
    /// <param name="callee">Function pointer value.</param>
    /// <param name="receiver">Receiver for virtual calls. <c>null</c> in the case of a static call
    /// (warning: static call and call to a static method are different things: you can call a virtual method
    /// without using dynamic dispatch).</param>
    /// <param name="arguments">Call arguments; does not include the value for the "this" argument; that value is
    /// the given by the receiver (if any). All elements of this list are Variables.</param>
    /// <param name="fp">Function pointer signature.</param>
    protected virtual object VisitCallIndirect
      (Variable dest, Variable callee, Variable receiver, Variable[] arguments, FunctionPointer fp,
      Statement stat, object arg) {
      return DefaultVisit(stat, arg);
    }


		/// <summary>
		/// dest := operand1 op operand2 -- assign result of binary operation to dest
		/// </summary>
		/// <param name="op">Binary operator (e.g. NodeType.Add).</param>
		protected virtual object VisitBinaryOperator
			(NodeType op, Variable dest, Variable operand1, Variable operand2, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


		/// <summary>
		/// dest := op operand -- assign result of unary operation to dest.
		/// </summary>
		/// <param name="op">Unary operator.</param>
		protected virtual object VisitUnaryOperator
			(NodeType op, Variable dest, Variable operand, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


		/// <summary>
		/// dest := (T)source -- cast object source to type T and assign result to dest.
		/// <p>
		/// Description:
		/// <br/>
    /// if type of source is a subtype of <c>type</c>, then assign source to dest;
    /// otherwise throw a <c>System.InvalidCastException</c>.
    /// </p>
		/// </summary>
		protected virtual object VisitCastClass (Variable dest, TypeNode type, Variable source, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
		/// dest := source as T -- istest instruction.
		/// <p>
		/// Description:
		/// <br/>
		/// If type of source is a subtype of T, assign source to dest; otherwise assign <c>null</c> to dest.
		/// </p>
		/// </summary>
		protected virtual object VisitIsInstance (Variable dest, Variable source, TypeNode type, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
		/// dest := box(T) source -- box source object of type T.
		/// <p>
		/// Description:
		/// <br/>
    /// If boxable (T is a value type or type parameter), boxes the source value int a fresh object and assigns
    /// result to dest. If T is an object type, it acts as a no-op.
		/// </p>
		/// </summary>
		protected virtual object VisitBox (Variable dest, Variable source, TypeNode type, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}

		
    /// <summary>
		/// dest := unbox(T) source -- convert boxed value type to its raw (value) form
		/// <p>
		/// Description:
		/// <br/>
    /// The instruction first checks that source is not null, otherwise throws NullReferenceException. Then it checks
    /// that the boxed value actually contains a value of type T, otherwise InvalidCastException is thrown.
    /// Finally, it copies a pointer of type T&amp; (reference to value type) into dest that points at the box 
    /// contents.
    /// </p>
		/// </summary>
		/// <param name="type">Value type of the contents expected in the boxed value.</param>
		protected virtual object VisitUnbox (Variable dest, Variable source, TypeNode type, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
    /// dest := unbox.any(T) source -- convert object to type T.
    /// <p>
    /// Description:
    /// <br/>
    /// <ul>
    /// <li>
    /// If <c>T</c> is a value type, extracts the value from the boxed object <c>source</c> and assigns it to <c>dest</c>. 
    /// In this case, the instruction acts like <c>unbox</c> followed by <c>ldobj</c>.
    /// </li>
    /// <li>
    /// If <c>T</c> is an object type, the instruction acts like castclass.
    /// </li>
    /// <li>
    /// If <c>T</c> is a type parameter, the instruction behaves dependent on the actual runtime type bound to T. 
    /// </li>
    /// </ul>
    /// </p>
    /// </summary>
    /// <param name="source">Object to unbox</param>
    /// <param name="type">Target type of the unbox.any.</param>
    protected virtual object VisitUnboxAny (Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      return DefaultVisit(stat, arg);
    }


    /// <summary>
		/// dest := source.field -- load field and assign to destination.
		/// <p>
		/// Description
		/// <br/>
    /// This instruction covers the cases of instance AND static field read instructions. If source is null, the field
    /// is static, otherwise it is an instance field.
    /// 
    /// In the case where the field is a member of a value type T, then source is of type T&amp; (reference to T).
    /// </p>
		/// </summary>
		/// <param name="source">Variable that points to the object whose field we read.
		/// <c>null</c> if <c>field</c> is static.</param>
		/// <param name="field">Loaded field (can be a static one).</param>
		protected virtual object VisitLoadField (Variable dest, Variable source, Field field, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


		/// <summary>
		/// dest.field := source -- store source into field.
		/// <p>
		/// Description:
		/// <br/>
    /// This instruction covers the cases of instance AND static field store instructions. If dest is null, the field
    /// is static, otherwise it is an instance field.
    /// 
    /// In the case where the field is a member of a value type T, then dest is of type T&amp; (reference to T).
		/// </p>
		/// </summary>
		/// <param name="dest">Variable that points to the object whose field we write. <c>null</c> if
		/// <c>field</c> is static.</param>
		/// <param name="field">Written field.</param>
		protected virtual object VisitStoreField (Variable dest, Field field, Variable source, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
		/// dest := source[index] -- load element from array
		/// </summary>
		/// <param name="source">Variable that points to the array whose element we read.</param>
		protected virtual object VisitLoadElement (Variable dest, Variable source, Variable index, TypeNode elementType, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
		/// dest[index] := source -- store array element
		/// </summary>
		/// <param name="dest">Variable that points to the array whose element we write.</param>
		protected virtual object VisitStoreElement (Variable dest, Variable index, Variable source, TypeNode elementType, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


		/// <summary>
		/// dest := &amp; source -- load address of local variable and store into dest.
		/// </summary>
		/// <param name="source">Variable whose address is taken.</param>
		protected virtual object VisitLoadAddress (Variable dest, Variable source, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


		/// <summary>
		/// dest := &amp; source.field -- load address of field and store into dest.
		/// <p>
		/// Description:
		/// <br/>
		/// The instruction covers both instance and static fields. For static fields, source is null.
		/// 
		/// For fields of reference type T, source is of type T&amp; (reference to T).
		/// </p>
		/// </summary>
		/// <param name="source">Variable pointing to the object that contains <c>field</c>; may be
		/// <c>null</c> is <c>field</c> is static.</param>
		/// <param name="field">Field whose address we load (may be static).</param>
		protected virtual object VisitLoadFieldAddress(Variable dest, Variable source, Field field, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
		/// dest := &amp; array[index] -- load address of array element and store into dest.
		/// <p>
		/// Description:
		/// <br/>
		/// Takes the address of the array element indexed and stores it into dest.
		/// </p>
		/// </summary>
		protected virtual object VisitLoadElementAddress (Variable dest, Variable array, Variable index, TypeNode elementType, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
		/// dest := *((type *) pointer) -- load value stored at pointer and assign to dest
		/// <p>
		/// MSIL instructions: ldind.T, ldobj
		/// <br/>
		/// </p>
		/// </summary>
		/// <param name="type">Type of the loaded value.</param>
		protected virtual object VisitLoadIndirect (Variable dest, Variable pointer, TypeNode elementType, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
		/// *((type *) pointer) := source -- store value at pointer
		/// <p>
		/// MSIL instructions: stind.T, stobj
		/// </p>
		/// </summary>
		/// <param name="type">Type of the stored value.</param>
		protected virtual object VisitStoreIndirect (Variable pointer, Variable source, TypeNode elementType, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


		/// <summary>
		/// dest := &amp;source.method -- load method pointer and store into dest.
		/// <p>
		/// Description:
		/// <br/>
    /// loads the address where a method code starts. This instruction covers both
    /// ldftn and ldvirtftn: for ldvirtftn, the dynamic dispatch algorithm is used to find
    /// the appropriate method.
    /// 
    /// For static functions, source is null.
    /// </p>
		/// </summary>
		/// <param name="source">Address of the object whose dynamically-dispatched method we are
		/// interested in.</param>
		/// <param name="method">If <c>source</c> is <c>null</c>, then the address of this method is loaded,
		/// otherwise, we load the address of the method that is invoked by a call to source.method.</param>
		protected virtual object VisitLoadFunction (Variable dest, Variable source, Method method, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}
	


    /// <summary>
		/// dest := ldtoken token -- load meta data token (type, method, or field) and store into dest.
		/// </summary>
		/// <param name="token">TypeNode / Field / Method whose metadata token is assigned to <c>dest</c>.</param>
		protected virtual object VisitLoadToken (Variable dest, object token, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}



		/// <summary>
		/// memcpy(destaddr, srcaddr, size) -- cpblk instruction, copies data from memory to memory
		/// </summary>
		/// <param name="destaddr">Variable that stores the start address of the destination memory area.</param>
		/// <param name="srcaddrs">Variable that stores the start address of the source memory area.</param>
		/// <param name="size">Variable that stores the number of bytes to copy.</param>
		protected virtual object VisitCopyBlock (Variable destaddr, Variable srcaddr, Variable size, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
		/// initblk addr, value, size -- initblk instruction, initializes memory to a value
		/// </summary>
		/// <param name="addr">Variable that stores the start address of the memory area to be initialized.</param>
		/// <param name="val">Variable that stores the "unsigned int8" value that will be stored in each
		/// memory byte.</param>
		/// <param name="size">Variable that stores the number of bytes to initialize.</param>
		protected virtual object VisitInitializeBlock (Variable addr, Variable val, Variable size, Statement stat, object arg)
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
    /// *dest := initobj T -- initobj instruction, assigns a default value to dest.
    /// <p>
    /// Description:
    /// <br/>
    /// dest is managed or unmanaged pointer to T. If T is a value type, this instruction initializes each field of T
    /// to its default value or zero for primitive types.
    /// If T is an object type, this instruction has the same effect as <c>ldnull</c> followed by <c>stind</c>.
    /// </p>
    /// </summary>
    /// <param name="addr">The pointer to the value type to be initialized. Is either Struct or EnumNode</param>
    protected virtual object VisitInitObj(Variable addr, TypeNode valueType, Statement stat, object arg) {
      return this.DefaultVisit(stat, arg);
    }


		/// <summary>
		/// break -- debugger break instruction, causes the execution to transfer control to a debugger.
		/// </summary>
		protected virtual object VisitBreak (Statement stat, object arg) 
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
    /// dest := mkrefany source,type -- assign typed reference to dest 
    /// </summary>
		protected virtual object VisitMakeRefAny (
			Variable dest, 
			Variable source, 
			TypeNode type, 
			Statement stat, 
			object arg
			)
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
    /// dest := refanyval source,type -- load the address out of a typed reference
    /// <p>
    /// Description:
    /// <br/>
    /// Throws <c>InvalidCastException</c> if typed reference isn't of type <c>type</c>. If it is
    /// extracts the object reference and stores it in dest.
    /// </p>
    /// </summary>
		protected virtual object VisitRefAnyValue (
			Variable dest,
			Variable source,
			TypeNode type,
			Statement stat,
			object arg
			)
		{
			return DefaultVisit(stat, arg);
		}


    /// <summary>
    /// dest := refanytype source --- extracts the type from a typed reference and assigns it to dest.
    /// </summary>
    protected virtual object VisitRefAnyType (
			Variable dest,
			Variable source,
			Statement stat,
			object arg
			)
		{
			return DefaultVisit(stat, arg);
		}


    protected virtual object VisitAssertion (Assertion assertion, object arg)
        {
            return DefaultVisit(assertion, arg);
        }

		/// <summary>
		/// Default visitor called by each non-overridden visitor above.
		/// </summary>
		protected abstract object DefaultVisit (Statement stat, object arg);

    #endregion

    #region private helpers
    private object VisitExpressionStatement (Statement stat, object arg)
    {
      Expression expression = ((ExpressionStatement) stat).Expression;

      if(expression is TernaryExpression)
      {
        TernaryExpression te = (TernaryExpression) expression;
        switch (te.NodeType)
        {
          case NodeType.Initblk:
            return VisitInitializeBlock((Variable) te.Operand1, (Variable) te.Operand2, (Variable) te.Operand3, stat, arg);
          case NodeType.Cpblk:
            return VisitCopyBlock((Variable) te.Operand1, (Variable) te.Operand2, (Variable) te.Operand3, stat, arg);
          default:
            throw new UnknownInstructionException("unknown ternary expression type " + te.NodeType);
        }
      }

      MethodCall call = expression as MethodCall;
      Variable receiver;
      Method callee;
      FunctionPointer fpointer;

      System.Diagnostics.Debug.Assert(expression != null, "should not see expression statement here with null expression");

      if(call == null)
        throw new UnknownInstructionException(expression.NodeType + " expression in " + CodePrinter.StatementToString(stat));
      GetReceiverAndCallee(call, out receiver , out callee, out fpointer);
      if (callee != null) 
      {
        if (call.Constraint == null) 
        {
          return VisitCall(null, receiver, callee, call.Operands, CciHelper.IsVirtual(call), stat, arg);
        }
        else 
        {
          return VisitConstrainedCall(null, receiver, callee, call.Operands, call.Constraint, stat, arg);
        }
      }
      else if (fpointer != null) 
      {
        return visit_calli(null, receiver, call, fpointer, stat, arg);
      }
      throw new NotImplementedException("unknown member destination at call in Quad");
    }



		private object visit_calli (Variable dest, Variable receiver, MethodCall call, FunctionPointer fpointer, Statement stat, object arg)
		{
			Variable[] parameters = new Variable[call.Operands.Count-1];
			// last operand is function pointer.
			for (int i=0; i<call.Operands.Count-1; i++) 
			{
				parameters[i]=(Variable)call.Operands[i];
			}
			Variable fpvar = (Variable)call.Operands[call.Operands.Count-1];

			return VisitCallIndirect(dest, fpvar, receiver, parameters, fpointer, stat, arg);
		}



		private object VisitAssignmentStatement (Statement stat, object arg)
		{
			AssignmentStatement astat = (AssignmentStatement) stat;
			Expression source = astat.Source;
			Expression dest = astat.Target;

			Variable vdest, vsource, vindex;
      TypeNode elemType;
			Field field;
			UnaryExpression unexpr;
			BinaryExpression binexpr;
			Method method;
		
			switch (source.NodeType)
			{
				case NodeType.Literal:
          Literal lit = (Literal)source;
          // TODO: fix me. We must use init obj for all types.


          if (dest.NodeType == NodeType.AddressDereference && lit.Value == null && dest.Type != null &&
            (dest.Type.IsValueType || dest.Type is TypeParameter || dest.Type is ClassParameter)) {
            AddressDereference ad = (AddressDereference)dest;
            return VisitInitObj((Variable)ad.Address, dest.Type, stat, arg);
          }
          if (lit.Value == null) {
            return VisitLoadNull((Variable)dest, lit, stat, arg);
          }
					return VisitLoadConstant((Variable) dest, (Literal) source, stat, arg);
				case NodeType.Arglist:
					return VisitArgumentList((Variable) dest, stat, arg);
				case NodeType.This:
				case NodeType.Parameter:
				case NodeType.Local:
				{
					vsource = (Variable) source;		
					switch(dest.NodeType)
					{
						case NodeType.This:
						case NodeType.Parameter:
						case NodeType.Local:
							return VisitCopy((Variable) dest, vsource, stat, arg);
						case NodeType.Indexer:
							GetArrayAndIndex((Indexer) dest, out vdest, out vindex, out elemType); 
							return VisitStoreElement(vdest, vindex, vsource, elemType, stat, arg);
						case NodeType.MemberBinding:
							GetObjectAndField((MemberBinding) dest, out vdest, out field);
							return VisitStoreField(vdest, field, vsource, stat, arg);
						case NodeType.AddressDereference:
							return VisitStoreIndirect((Variable) ((AddressDereference) dest).Address, vsource, dest.Type, stat, arg);
					}
					break;
				}
				case NodeType.Construct:
					//
					// We assume that the code flatener has broken Construct nodes into 3 parts:
					// 1. a raw construction (this node), intended to model the allocation, but not the constructor call
					// 2. an explicit constructor call
					// 3. an assignment to the intended target.
					//
					Construct constr = (Construct) source;
					Method constructor = (Method) ((MemberBinding) constr.Constructor).BoundMember;
					return VisitNewObject((Variable) dest, source.Type, stat, arg);

				case NodeType.ConstructArray:
					ConstructArray ca = (ConstructArray) source;
					return VisitNewArray((Variable) dest, ca.ElementType, (Variable) ca.Operands[0], stat, arg);

				case NodeType.Ldftn:
					unexpr = (UnaryExpression) source;
					method = (Method) ((MemberBinding) unexpr.Operand).BoundMember;
					return VisitLoadFunction((Variable) dest, null, method, stat, arg);

				case NodeType.Ldvirtftn:
					binexpr = (BinaryExpression) source;
					method = (Method) ((MemberBinding) binexpr.Operand2).BoundMember;
					return VisitLoadFunction((Variable) dest, (Variable) binexpr.Operand1, method, stat, arg);

				case NodeType.Ldtoken:
				{
					Expression token = ((UnaryExpression) source).Operand;
					if(token is Literal)
						return VisitLoadToken((Variable) dest, (TypeNode) ((Literal) token).Value, stat, arg);
					Member member = ((MemberBinding) token).BoundMember;
					if (member is Field)
						return VisitLoadToken((Variable) dest, (Field) member, stat, arg);
          else if (member is TypeNode)
            return VisitLoadToken((Variable) dest, (TypeNode) member, stat, arg);
          else
						return VisitLoadToken((Variable) dest, (Method) member, stat, arg);
				}

				case NodeType.Mkrefany:
				{
					binexpr = (BinaryExpression) source;
					return VisitMakeRefAny((Variable) dest, (Variable) binexpr.Operand1, 
						((Literal) binexpr.Operand2).Value as TypeNode, stat, arg);
				}

				case NodeType.Refanyval:
				{
					binexpr = (BinaryExpression) source;
					return VisitRefAnyValue((Variable) dest, (Variable) binexpr.Operand1,
						((Literal) binexpr.Operand2).Value as TypeNode, stat, arg);
				}

				case NodeType.Refanytype:
				{
					unexpr = (UnaryExpression) source;
					return VisitRefAnyType((Variable) dest, (Variable) unexpr.Operand, stat, arg);
				}

				case NodeType.Sizeof:
          return VisitSizeOf((Variable)dest, this.GetTypeFrom(((UnaryExpression)source).Operand), stat, arg);

				case NodeType.Is:
				case NodeType.Isinst:
					binexpr = (BinaryExpression) source;
          return VisitIsInstance((Variable)dest, (Variable)binexpr.Operand1, this.GetTypeFrom(binexpr.Operand2), stat, arg);

				case NodeType.Box:
					binexpr = (BinaryExpression) source;
          return VisitBox((Variable)dest, (Variable)binexpr.Operand1, this.GetTypeFrom(binexpr.Operand2), stat, arg);

        case NodeType.Unbox:
					binexpr = (BinaryExpression) source;
          return VisitUnbox((Variable)dest, (Variable)binexpr.Operand1, this.GetTypeFrom(binexpr.Operand2), stat, arg);

        case NodeType.UnboxAny:
          binexpr = (BinaryExpression) source;
          return VisitUnboxAny((Variable)dest, (Variable)binexpr.Operand1, this.GetTypeFrom(binexpr.Operand2), stat, arg);

				case NodeType.Castclass:
					binexpr = (BinaryExpression) source;
          return VisitCastClass((Variable)dest, this.GetTypeFrom(binexpr.Operand2), (Variable)binexpr.Operand1, stat, arg);

        case NodeType.Call :
        case NodeType.Calli :
        case NodeType.Callvirt :
        case NodeType.Jmp :
        case NodeType.MethodCall :
        {
          MethodCall call = (MethodCall) source;
          Variable receiver;
          Method callee;
          FunctionPointer fpointer;
          GetReceiverAndCallee(call, out receiver, out callee, out fpointer);
          if (callee != null) 
          {
            if (call.Constraint == null) 
            {
              return VisitCall((Variable) dest, receiver, callee, call.Operands, CciHelper.IsVirtual(call), stat, arg);
            }
            else 
            {
              return VisitConstrainedCall((Variable)dest, receiver, callee, call.Operands, call.Constraint, stat, arg);
            }
          }
          else if (fpointer != null)
          {
            return visit_calli((Variable) dest, receiver, call, fpointer, stat, arg);
          }
          else 
          {
            throw new NotImplementedException("unknown method target in Quad call expression");
          }
        }
				case NodeType.MemberBinding:
					GetObjectAndField((MemberBinding) source, out vsource, out field);
					return VisitLoadField((Variable) dest, vsource, field, stat, arg);
				case NodeType.Indexer:
					GetArrayAndIndex((Indexer) source, out vsource, out vindex, out elemType);
					return VisitLoadElement((Variable) dest, vsource, vindex, elemType, stat, arg);
        case NodeType.ReadOnlyAddressOf:
				case NodeType.AddressOf:
				case NodeType.RefAddress:
				case NodeType.OutAddress:
				{
					Expression esource = ((UnaryExpression) source).Operand;
					switch(esource.NodeType)
					{
						case NodeType.Local:
						case NodeType.Parameter:
							return VisitLoadAddress((Variable) dest, (Variable) esource, stat, arg);
						case NodeType.MemberBinding:
							GetObjectAndField((MemberBinding) esource, out vsource, out field);
							return VisitLoadFieldAddress((Variable) dest, vsource, field, stat, arg);
						case NodeType.Indexer:
							GetArrayAndIndex((Indexer) esource, out vsource, out vindex, out elemType);
							return VisitLoadElementAddress((Variable) dest, vsource, vindex, elemType, stat, arg);
						default:
							throw new UnknownInstructionException("unknown reference instr " + CodePrinter.StatementToString(astat));
					}
				}
				case NodeType.AddressDereference:
					return VisitLoadIndirect((Variable) dest, (Variable) ((AddressDereference) source).Address,
						source.Type, stat, arg);
			}

			binexpr = source as BinaryExpression;
			if(binexpr != null)
				return VisitBinaryOperator(binexpr.NodeType, (Variable) dest,
					(Variable) binexpr.Operand1, (Variable) binexpr.Operand2, stat, arg);
			
			unexpr = source as UnaryExpression;
			if(unexpr != null)
				return VisitUnaryOperator(unexpr.NodeType, (Variable) dest, (Variable) unexpr.Operand, stat, arg);

			throw new UnknownInstructionException("unsupported statement \"" + CodePrinter.StatementToString(astat));
		}



		/// <summary>
		/// Unscrambles a method target. 
		/// </summary>
		/// <param name="call">The method call node to unscramble</param>
		/// <param name="receiver">Returns the receiver expression. Maybe null.</param>
		/// <param name="callee">Returns the method, if target is a method, otherwise null.</param>
		/// <param name="fpointer">Returns the function pointer if target is a function pointer, otherwise null.</param>
		private void GetReceiverAndCallee (
      MethodCall call, 
      out Variable receiver, 
			out Method callee, 
      out FunctionPointer fpointer
      )
		{
			MemberBinding mb = (MemberBinding) call.Callee;
			receiver = (Variable) mb.TargetObject;
			callee   = mb.BoundMember as Method;
			fpointer = mb.BoundMember as FunctionPointer;
		}

		private void GetObjectAndField(MemberBinding mb, out Variable var, out Field field)
		{
      Debug.Assert(mb.BoundMember is Field);
			var   = (Variable) mb.TargetObject;
			if(mb.BoundMember is ClosureClass)
        field=null; //new This();
      else
        field = (Field) mb.BoundMember;
		}
	
		private void GetArrayAndIndex(Indexer indexer, out Variable array, out Variable index, out TypeNode elemType)
		{
			array = (Variable) indexer.Object;
			index = (Variable) indexer.Operands[0];
      TypeNode arrayType = TypeNode.StripModifiers(array.Type);
      ArrayType arrT = arrayType as ArrayType;
      if (arrT != null) {
        elemType = arrT.ElementType;
        return;
      }
      Pointer pt = arrayType as Pointer;
      if (pt != null) {
        elemType = pt.ElementType;
        return;
      }
      elemType = indexer.ElementType;
      return;
		}

    private TypeNode GetTypeFrom(Expression expr)
    {
      Literal lit = expr as Literal;
      if (lit != null) return (TypeNode)lit.Value;
      return (TypeNode)((MemberBinding)expr).BoundMember;
    }

    
    #endregion // private helpers
	}


	/// <summary>
	///  Example use of <c>QuadVisitor</c>.  Generates an appropriate string representation
	///  for each quad.
	/// </summary>
	public class SampleInstructionVisitor : InstructionVisitor
	{
		protected override object VisitNop (Statement stat, object arg)
		{
			return "NOP";
		}

		protected override object VisitMethodEntry (Method method, IEnumerable/*<Variable>*/ parameters, Statement stat, object arg)
		{
			return "HEADER(" + CodePrinter.FullName(method) + ")  " +
				DataStructUtil.IEnum2String(parameters, new DObj2String(CodePrinter.TypedVarPrinter));
		}

		protected override object VisitBranch (Variable cond, Block target, Statement stat, object arg)
		{
			return "JMP " + 
				((cond == null) ?
				("jumpto " + CodePrinter.b2s(target)) :
				("if(" + CciHelper.Name(cond) + ") jumpto " + CodePrinter.b2s(target)));
		}

    protected override object VisitBreak(Statement stat, object arg)
    {
      return "BREAK (debugger)";
    }


    protected override object VisitEndFilter(Variable code, Statement stat, object arg)
    {
      return "ENDFILTER";
    }


    protected override object VisitFilter(Variable dest, Statement stat, object arg)
    {
      return "FILTER";
    }

    protected override object VisitInitObj(Variable addr, TypeNode valueType, Statement stat, object arg) {
      return "INITOBJ " + CciHelper.Name(addr) + " (" + valueType.FullName + ") ";
    }

    protected override object VisitInitializeBlock(Variable addr, Variable val, Variable size, Statement stat, object arg)
    {
      return "INITBLOCK";
    }

    protected override object VisitRethrow(Statement stat, object arg)
    {
      return "RETHROW";
    }

		protected override object VisitSwitch (Variable selector, BlockList Targets, Statement stat, object arg)
		{
			return "SWITCH (" + CciHelper.Name(selector) + ")";
		}


    protected override object VisitSwitchCaseBottom(Statement stat, object arg) {
      return "SWITCHCASEBOTTOM";
    }

		protected override object VisitReturn (Variable var, Statement stat, object arg)
		{
			return "RETURN " + ((var != null) ? CciHelper.Name(var) : "");
		}

		protected override object VisitThrow (Variable var, Statement stat, object arg)
		{
			return "THROW " + CciHelper.Name(var);
		}

		protected override object VisitCatch (Variable var, TypeNode type, Statement stat, object arg)
		{
			return "CATCH " + CciHelper.Name(var) + " := CATCH(" + type.FullName + ")";
		}

    protected override object VisitLoadNull(Variable dest, Literal source, Statement stat, object arg) {
      return "LDNULL " + CciHelper.Name(dest) + " := null";
    }

		protected override object VisitLoadConstant (Variable dest, Literal source, Statement stat, object arg)
		{
			string vs = source.Value.ToString();
			return "CONST " + CciHelper.Name(dest) + " := (" + source.Type.FullName + ")" + vs;
		}

		protected override object VisitArgumentList (Variable dest, Statement stat, object arg)
		{
			return "ARGLIST " + CciHelper.Name(dest) + " := ARGLIST";
		}
		
		protected override object VisitSizeOf (Variable dest, TypeNode value_type, Statement stat, object arg)
		{
			return "SIZEOF " + CciHelper.Name(dest) + " := sizeof(" + value_type.Name + ")";
		}


		protected override object VisitCopy (Variable dest, Variable source, Statement stat, object arg)
		{
			return "COPY " + CciHelper.Name(dest) + " := " + CciHelper.Name(source);
		}

		protected override object VisitCopyBlock(Variable destaddr, Variable srcaddr, Variable size, Statement stat, object arg)
		{
			return "COPYBLOCK(" + CciHelper.Name(destaddr) + ", " + CciHelper.Name(srcaddr) + ", " + CciHelper.Name(size) + ")";
		}


    protected override object VisitConstrainedCall(Variable dest, Variable receiver, Method callee, ExpressionList arguments, TypeNode constraint, Statement stat, object arg)
    {
      return 
        ((arg == null) ? "CONSTRAIN(" + constraint.FullName + ".CALL " : (arg + "; ")) + 
        ((dest != null) ? (CciHelper.Name(dest) + " := ") : "") + 
        (CciHelper.Name(receiver) + ".") +
        CodePrinter.FullName(callee) + "(" +
        CodePrinter.exprlist2str(arguments) + ")";
    }


		protected override object VisitNewObject
			(Variable dest, TypeNode type, Statement stat, object arg)
		{
			return "NEW " + CciHelper.Name(dest) + " := new " + type.FullName;
		}

		protected override object VisitNewArray (Variable dest, TypeNode element_type, Variable size, Statement stat, object arg)
		{
			return "ANEW " + CciHelper.Name(dest) + " := new " + element_type.FullName +  "[" + CciHelper.Name(size) + "]";
		}


		protected override object VisitBinaryOperator 
			(NodeType op, Variable dest, Variable operand1, Variable operand2, Statement stat, object arg)
		{
			return "BINOP " + CciHelper.Name(dest) + " := " + CciHelper.Name(operand1) + " " + op + " " + CciHelper.Name(operand2);
		}

		protected override object VisitUnaryOperator
			(NodeType op, Variable dest, Variable operand, Statement stat, object arg)
		{
			return "UNOP " + CciHelper.Name(dest) + " := " + op + " " + CciHelper.Name(operand);
		}


		protected override object VisitIsInstance (Variable dest, Variable source, TypeNode type, Statement stat, object arg)
		{
			return "ISTEST " + CciHelper.Name(dest) + " := " + CciHelper.Name(source) + " is " + type.FullName;
		}


		protected override object VisitCastClass (Variable dest, TypeNode type, Variable source, Statement stat, object arg)
		{
			return "CAST " + CciHelper.Name(dest) + " := (" + type.FullName + ") " + CciHelper.Name(source);
		}

		protected override object VisitBox (Variable dest, Variable source, TypeNode type, Statement stat, object arg)
		{
			return "BOX " + CciHelper.Name(dest) + " := BOX " + CciHelper.Name(source) + " " + type.FullName;
		}

		protected override object VisitUnbox (Variable dest, Variable source, TypeNode type, Statement stat, object arg)
		{
			return "UNBOX " + CciHelper.Name(dest) + " := UNBOX " + CciHelper.Name(source) + " " + type.FullName;
		}

    protected override object VisitUnboxAny(Variable dest, Variable source, TypeNode type, Statement stat, object arg) {
      return "UNBOX.any" + CciHelper.Name(dest) + " := UNBOX.any " + CciHelper.Name(source) + " " + type.FullName;
    }


		protected override object VisitLoadField (Variable dest, Variable source, Field field, Statement stat, object arg)
		{
      string destName = CciHelper.Name(dest);
			return "GET " + destName + " := " +
				((source != null) ? CciHelper.Name(source) : (" static " + field.DeclaringType.FullName)) + "." + field.Name;
		}

		protected override object VisitStoreField (Variable dest, Field field, Variable source, Statement stat, object arg)
		{
			return "SET " + 
				((dest != null) ? CciHelper.Name(dest) : (" static " + field.DeclaringType.FullName)) +
				"." + field.Name + " := " + CciHelper.Name(source);
		}

		protected override object VisitLoadElement (Variable dest, Variable source, Variable index, TypeNode elementType, Statement stat, object arg)
		{
			return "AGET " + CciHelper.Name(dest) + " := " + CciHelper.Name(source) + "[" + CciHelper.Name(index) + "]";
		}

		protected override object VisitStoreElement (Variable dest, Variable index, Variable source, TypeNode elementType, Statement stat, object arg)
		{
			return "ASET " + CciHelper.Name(dest) + "[" + CciHelper.Name(index) + "] := &" + CciHelper.Name(source);
		}


		protected override object VisitLoadAddress (Variable dest, Variable source, Statement stat, object arg)
		{
			return "GETREF " + CciHelper.Name(dest) + " := &" + CciHelper.Name(source);
		}

		protected override object VisitLoadFieldAddress (Variable dest, Variable source, Field field, Statement stat, object arg)
		{
			return "GETFIELDREF " + CciHelper.Name(dest) + " := &" + 
				( (source != null) ? CciHelper.Name(source) : ("static " + field.DeclaringType.FullName) ) +
				"." + field.Name;
		}

		protected override object VisitLoadElementAddress (Variable dest, Variable array, Variable index, TypeNode elementType, Statement stat, object arg)
		{
			return "GETARRAYELEMREF " + CciHelper.Name(dest) + " := &" + CciHelper.Name(array) + "[" + CciHelper.Name(index) + "]";
		}


		protected override object VisitLoadIndirect (Variable dest, Variable pointer, TypeNode type, Statement stat, object arg)
		{
			return "GETIND " + CciHelper.Name(dest) + " := *((" + type.FullName + " *)" + CciHelper.Name(pointer) + ")";
		}

		protected override object VisitStoreIndirect (Variable pointer, Variable source, TypeNode type, Statement stat, object arg)
		{
			return "SETIND " + "*((" + type.FullName + " *) "+ CciHelper.Name(pointer) + ") := " + CciHelper.Name(source);
		}


		protected override object VisitLoadFunction (Variable dest, Variable source, Method method, Statement stat, object arg)
		{
			if(source != null)
				return "LDFTN " + CciHelper.Name(dest) + " := (Ldvirtftn) " + CciHelper.Name(source) + "." + CodePrinter.FullName(method);
			else
				return "LDFTN " + CciHelper.Name(dest) + " := (Ldftn) " + CodePrinter.FullName(method);
		}


		protected override object VisitCall
			(Variable dest, 
			 Variable receiver, Method callee, ExpressionList/*<Variable>*/ arguments, bool virtcall,
			Statement stat, object arg)
		{
			return 
				((arg == null) ? "CALL " : (arg + "; ")) + 
				((dest != null) ? (CciHelper.Name(dest) + " := ") : "") + 
				(virtcall ? "virtual " : "") + 
				((receiver != null) ? (CciHelper.Name(receiver) + ".") : "static ") +
				CodePrinter.FullName(callee) + "(" +
				CodePrinter.exprlist2str(arguments) + ")";
		}


		protected override object VisitCallIndirect
			(Variable dest, Variable callee, Variable receiver, Variable[] arguments, FunctionPointer fp,
			 Statement stat, object arg)
		{
			return 
				((arg == null) ? "CALLI:       " : (arg + "; ")) + 
				((dest != null) ? (CciHelper.Name(dest) + " := ") : "") + 
				CciHelper.Name(callee) + "(" +
				((receiver != null) ? ("this:" + CciHelper.Name(receiver) + ((arguments.Length>0) ? "," : "")) : "") +
				CodePrinter.vararray2str(arguments) + ")";
		}



		protected override object VisitLoadToken (Variable dest, object token, Statement stat, object arg)
		{
			return String.Format("LDTOKEN {0} := {1}", CciHelper.Name(dest), token);
		}


		protected override object VisitMakeRefAny (Variable dest, Variable source, TypeNode type, Statement stat, object arg)
		{
			return String.Format("MAKEREFANY {0} := {1} {2}", CciHelper.Name(dest), CciHelper.Name(source), type.FullName);
		}


		protected override object VisitRefAnyType(Variable dest, Variable source, Statement stat, object arg)
		{
			return String.Format("REFANYTYPE {0} := {1}", CciHelper.Name(dest), CciHelper.Name(source));
		}


		protected override object VisitRefAnyValue(Variable dest, Variable source, TypeNode type, Statement stat, object arg)
		{
			return String.Format("REFANYVALUE {0} := {1} {2}", CciHelper.Name(dest), CciHelper.Name(source), type.FullName);
		}

    protected override object VisitUnwind(Statement stat, object arg)
    {
      return "UNWIND";
    }






		protected override object DefaultVisit (Statement stat, object arg)
		{
			throw new ApplicationException("Don't know how to process " + CodePrinter.StatementToString(stat));
		}


		/// <summary>
		/// Returns a string representation for each quad.  Throws an exception when encountering
		/// an unknown quad or an unknown CciHelper pattern.
		/// </summary>
		/// <param name="stat"></param>
		/// <returns></returns>
		public string GetStringDesc(Statement stat)
		{
			return (string)this.Visit(stat, null);
		}

		public static readonly SampleInstructionVisitor StringQuad = new SampleInstructionVisitor();

		public static string GetString (Statement stat) 
		{
			return SampleInstructionVisitor.StringQuad.GetStringDesc(stat);
		}
	}
}
