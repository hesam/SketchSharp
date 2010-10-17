//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
#if CCINamespace
namespace Microsoft.Cci{
  using Cci = Microsoft.Cci;
#else
namespace System.Compiler{
  using Cci = System.Compiler;
#endif
  using System;
  using System.Collections;
  using System.Diagnostics;
  using System.Text;




	public abstract class CciHelper
	{

		public static bool IsAbstract(TypeNode type_node)
		{
			return (type_node.Flags & TypeFlags.Abstract) == TypeFlags.Abstract;
		}

		public static bool IsInterface(TypeNode type_node)
		{
			return (type_node.Flags & TypeFlags.Interface) == TypeFlags.Interface;
		}

		public static bool IsArrayType(TypeNode type_node)
		{
			return (type_node is Cci.ArrayType);
		}

		public static bool IsStatic(Method method)
		{
			return (method.Flags & MethodFlags.Static) == MethodFlags.Static;
		}
		
		public static bool IsVirtual(Method method)
		{
			return (method.Flags & MethodFlags.Virtual) == MethodFlags.Virtual;
		}

		/// <summary>
		/// Predicate determines if a particular call is virtual or non virtual
		/// </summary>
		/// <param name="call">The call to test</param>
		/// <returns>true if virtual.</returns>
		public static bool IsVirtual(MethodCall call) 
		{
			Method callee = ((MemberBinding)call.Callee).BoundMember as Method;
			if (callee != null) {
				return (callee.IsVirtual && call.NodeType == NodeType.Callvirt);
			}
			return false;
		}

		public static bool IsAbstract(Method method)
		{
			return (method.Flags & MethodFlags.Abstract) == MethodFlags.Abstract;
		}

		public static bool IsConstructor (Method method)
		{
			return method.Name.Name.Equals(".ctor");
		}

		public static bool IsVoid (TypeNode type) 
		{
			TypeModifier mtype = type as TypeModifier;
			if (mtype != null) 
			{
				return IsVoid(mtype.ModifiedType);
			}

			if (type != null && type.Equals(Cci.SystemTypes.Void)) 
			{
				return true;
			}
			return false;
		}

    /// <summary>
    /// Checks whether a specific parameter is an "out" parameter.
    /// </summary>
    /// <param name="parameter">Parameter to test.</param>
    /// <returns>True if "out" parameter.</returns>
    public static bool IsOut(Parameter parameter)
    {
      return (parameter.Flags & ParameterFlags.Out) > 0 && (parameter.Flags & ParameterFlags.In) == 0;
    }



		/// <summary>
		/// Checks whether a given CciHelper statement encodes an MSIL Pop instruction.
		/// The correctness of this method depends heavily on what Herman does in the CciHelper reader ...
		/// </summary>
		/// <param name="stat">Statement to check.</param>
		/// <returns><c>true</c> iff <c>stat</c> encodes an MSIL Pop instruction.</returns>
		public static bool IsPopStatement(Statement stat) 
		{
			if(stat.NodeType == NodeType.Pop)
				return true;
			ExpressionStatement expr_stat = stat as ExpressionStatement;
			if(expr_stat == null) return false;

			Expression expr = expr_stat.Expression;

			if (expr.NodeType != NodeType.Pop) return false;

			UnaryExpression unexpr = expr as UnaryExpression;
			if ((unexpr != null ) && (unexpr.Operand != null) && (unexpr.Operand.NodeType != NodeType.Pop)) return false;

			return true;
		}


		

		/// <summary>
		/// Find the This node in the method body if method is an instance method.
		/// </summary>
		/// <param name="method">Method for which we find the This node</param>
		/// <returns>The This node or null if method is not instance.</returns>
    public static This GetThis (Method method)
    {
      if ((method.CallingConvention & CallingConventionFlags.HasThis) == 0) return null;
      This t = method.ThisParameter;
      if (t == null) 
      {
        if (method.DeclaringType != null && method.DeclaringType.IsValueType) {
            t = new This(method.DeclaringType.GetReferenceType());
        }
        else {
            t = new This(method.DeclaringType);
        }
        method.ThisParameter = t;
      }
      return t;
    }


		
		public static TypeNode LeastCommonAncestor(TypeNode t1, TypeNode t2) 
		{
			if (t1.IsAssignableTo(t2)) 
			{
				return t2;
			}

			// walk up t1 until assignable to t2
			TypeNode frame = t1;
			while (frame != null) 
			{
				if (t2.IsAssignableTo(frame))
				{
					return frame;
				}

				frame = frame.BaseType;
			}

			// if we get here, we haven't found a common basetype. Return object

			return Cci.SystemTypes.Object;
		}


    public static string Name(Variable v) 
    {
      Identifier name = v.Name;
      string nstr = (name == null)?"":name.Name;
      return String.Format("{0}({1})", nstr, v.UniqueKey);
    }

    public static string PrettyName(IUniqueKey function) {
      if (function is Variable) {
        Variable v = (Variable)function;
        Identifier name = v.Name;
        string nstr = (name == null)?("temp("+function.UniqueId.ToString()+")"):name.Name;
        return nstr;
      }
      if (function is Field) {
        Field f = (Field)function;
        Identifier name = f.Name;
        return (name == null)?("field("+function.UniqueId.ToString()+")"):name.Name;
      }
      if (function is Method) {
        Method m = (Method)function;
        Identifier name = m.Name;
        return (name == null)?("method("+function.UniqueId.ToString()+")"):name.Name;
      }
      return function.ToString();
    }

	} // class












}
