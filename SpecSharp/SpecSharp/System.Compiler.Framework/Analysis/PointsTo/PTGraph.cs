//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;
using AbstractValue = System.Compiler.MathematicalLattice.Element;
using System.IO;
using System.Compiler.Analysis.PointsTo;
#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler
{
#endif
    #region PointsTo Analysis Label (Program Location)
    public class Label
    {
        int id;
        Statement s;
        private Method m;

        #region Constructors
        public Label(Statement s, Method m)
        {
            //this.id = s.UniqueKey;
            this.id = s.SourceContext.StartPos; // s.SourceContext.StartLine + s.SourceContext.StartColumn;

            if (this.id == 0)
            {
                this.id = s.UniqueKey;
            }
            this.s = s;
            this.m = m;
        }
        public Label(int id, Method m)
        {
            this.m = m;
            this.id = id;
        }
        public Label Next()
        {
            return new Label(id + 1, m);
        }
        #endregion

        #region Properties
        public Statement Statement
        {
            get { return s; }
        }
        public Method Method
        {
            get { return m; }
            set { m = value; }
        }
        #endregion

        #region Equals, Hash, ToString
        public override bool Equals(object obj)
        {
            Label lb = obj as Label;
            return lb != null && id == lb.id;
        }

        public override int GetHashCode()
        {
            return id;
        }

        public override string ToString()
        {
            string res = "";
            if (PTGraph.ExpandedToString)
                res = string.Format("({0},{1}) {2} Id={3}", s.SourceContext.StartLine,
                    s.SourceContext.StartColumn, CodePrinter.StatementToString(s), id);
            else
                if (s != null)
                    res = string.Format("({0},{1},Id={2})", s.SourceContext.StartLine,
                    s.SourceContext.StartColumn, id);
                else
                    res = id.ToString();
            return res;
        }

        public string ToStringRed()
        {
            string res = "";
            if (s != null)
            {
                res = string.Format("({0},{1},Id={2})", s.SourceContext.StartLine,
                s.SourceContext.StartColumn, id);
            }
            return res;
        }
        #endregion

        // Just a test....
        private static int getStatementIndex(Method m, Statement s)
        {
            int count = 0;
            StatementList stats = m.Body.Statements;
            for (int i = 0; i < stats.Count; i++)
            {
                if (stats[i] is Block)
                {
                    Block block = (Block)stats[i];
                    StatementList stats2 = block.Statements;
                    for (int j = 0; j < stats2.Count; j++)
                    {
                        if (stats2[j].Equals(s))
                            return count;
                        count++;
                    }
                }
                if (stats[i].Equals(s))
                    return count;
                count++;
            }
            return -1;
        }
    }
    #endregion

    #region PointsTo Analysis Node's Representation
    /// <summary>
    /// Is the base for all nodes in the pointsToGraph
    /// It has a Label representing its position in code and the Type of the heap
    /// location it represents
    /// </summary>
    #region Interfaces for Principal Node Types
    public interface IPTAnalysisNode
    {
        // A Global Node
        bool IsGlobal { get; }
        // An addr node
        bool IsAddrNode { get; }
        // An newly allocated node
        bool IsInside { get; }
        // A load Node
        bool IsLoad { get; }
        // A heap object abstraction
        bool IsOmega { get; }
        bool IsOmegaLoad { get; }
        bool IsOmegaConfined { get; }
        
        bool IsObjectAbstraction { get; }
        // Null
        bool IsNull { get; }
        // A struct node
        bool IsStruct { get; }
        // A parameter node
        bool IsParameterNode { get; }
        
        // A node representing the value that the parameter references
        bool IsParameterValueLNode { get; }

        // A reference to a program variable
        bool IsVariableReference { get; }
        // A reference tp a field
        bool IsReferenceFieldNode { get; }

        bool IsMethod { get; }
        bool IsMethodDelegate { get; }

        bool IsClousure { get; set; }
        
        // 
        string Name { get; }
        TypeNode Type { get; }
        Label Label { get; }
        //
        bool Equals(object obj);
        int GetHashCode();
        
    }

        public interface ILoadNode : IPTAnalysisNode 
        {
            void SetOmegaConfinedLoadNode();
            void ResetOmegaConfinedLoadNode();
            void SetOmegaLoadNode();
            void ResetOmegaLoadNode();
        }
        public interface IObjectAbsNode : IPTAnalysisNode { }
   
            public interface IINode : IObjectAbsNode { }
            public interface IGNode : IObjectAbsNode { }
        public interface IParameterValueNode: IINode
        {
            void SetOmegaNode();
            void ResetOmegaNode();
            void SetOmegaConfined();
            void ResetOmegaConfined();
            IPTAnalysisNode ParameterReference { get; }
        }

        public interface IStructNode : IPTAnalysisNode { }

        public interface IAddrNode : IPTAnalysisNode { }
            public interface IVarRefNode : IAddrNode 
    {
        Variable ReferencedVariable { get; }
    }
            public interface IParameterNode : IVarRefNode 
            {
                int ParameterNumber { get; }
                Method Method { get; }

                bool IsByValue { get ; }
                bool IsByRef { get; }
                bool IsOut { get; }
                bool IsReforOutParamNode { get; }
            }

    #endregion
    
    /// <summary>
    /// PtAnalysisNode: Is the base class for all nodes in the pointsToGraph
    /// It has a Label representing its position in code and the Type of the heap
    /// location it represents
    /// </summary>
    public abstract class PTAnalysisNode : IPTAnalysisNode
    {
        protected TypeNode nodeType;
        protected Label lb;
        protected bool isClousure = false;
        protected bool isExposed = false;

        public PTAnalysisNode(Label lb, TypeNode t)
        {
            this.nodeType = t;
            this.lb = lb;
        }

        // A Global Node
        public virtual bool IsGlobal
        {
            get { return false; }
        }
        // An node representing and address
        public virtual bool IsAddrNode
        {
            get { return false; }
        }
        // An newly allocated node
        public virtual bool IsInside
        {
            get { return false; }
        }
        // A load Node
        public virtual bool IsLoad
        {
            get { return false; }
        }
        // An omega node 
        public virtual bool IsOmega
        {
            get { return false; }
        }
        public virtual bool IsOmegaLoad
        {
            get { return false; }
        }
        public virtual bool IsOmegaConfined
        {
            get { return false; }
        }
        public virtual bool IsObjectAbstraction
        {
            get { return false; }
        }
        public virtual bool IsNull
        {
            get { return false; }
        }
        // A struct node
        public virtual bool IsStruct
        {
            //get { return false;  }
            //get { return nodeType is Struct && !nodeType.IsPrimitive; }
            get { return PTGraph.IsStructType(nodeType); }
        }
        // A parameter node
        public virtual bool IsParameterNode
        {
            get { return false; }
        }
        // The value assigned to a parameter node
        public virtual bool IsParameterValueLNode
        {
            get { return false; }
        }
        // A reference to a program variable
        public virtual bool IsVariableReference
        {
            get { return false; }
        }
        // A reference tp a field
        public virtual bool IsReferenceFieldNode
        {
            get { return false; }
        }
        public virtual bool IsMethod
        {
            get { return false; }
        }
        public virtual bool IsMethodDelegate
        {
            get { return false; }
        }

        public virtual bool IsClousure
        {
            get { return isClousure; }
            set { isClousure = value; }
        }

        public virtual string Name
        {
            get { return lb.ToString(); }
        }

        public TypeNode Type
        {
            get
            {
                if (nodeType == null)
                    nodeType = TypeNode.GetTypeNode(System.Type.GetType("System.Object"));
                return nodeType;
            }
        }
        public Label Label
        {
            get { return lb; }
        }
        public override bool Equals(object obj)
        {
            PTAnalysisNode n = obj as PTAnalysisNode;
            return n != null && nodeType.Equals(n.Type) &&
                ((n.lb != null && lb != null && lb.Equals(n.lb))
                  || (n.lb == null && lb == null));
        }
        public override int GetHashCode()
        {
            if (lb != null && nodeType != null)
                return lb.GetHashCode() + nodeType.GetHashCode();
            //System.Diagnostics.Debugger.Break();
            return 0;
        }
        /*
        public virtual Variable ReferencedVariable
        {
            get
            {
                return null;
            }
        }
         * */
    }

    public abstract class ObjectAbsNode : PTAnalysisNode, IObjectAbsNode
    {
        
        public ObjectAbsNode(Label lb, TypeNode t)
            : base(lb, t)
        {
        }
        public override string ToString()
        {
            return "<OBJ:" + nodeType + " " + lb.ToString() + ">";
        }
        public override bool IsObjectAbstraction
        {
            get
            {
                return true;
            }
        }
        public override bool Equals(object obj)
        {
            ObjectAbsNode objnode = obj as ObjectAbsNode;
            return objnode != null && base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
    
    public class AddrNode : PTAnalysisNode, IAddrNode
    {
        public AddrNode(Label lb, TypeNode t)
            : base(lb, t)
        {
        }
        public override string ToString()
        {
            return "<ADDR:" + nodeType + " " + lb.ToString() + ">";
        }
        public override bool Equals(object obj)
        {
            AddrNode anode = obj as AddrNode;
            return anode != null && base.Equals(obj);
        }
        public override bool IsAddrNode
        {
            get { return true; }
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class MethodNode : PTAnalysisNode
    {
        protected Method m;

        public MethodNode(Label lb, TypeNode t, Method m)
            : base(lb, t)
        {
            this.m = m;
        }
        public override string ToString()
        {
            string mName = "";
            if (m != null)
                mName = m.Name.Name;
            return "<Method:" + mName + " - " + nodeType + " " + lb.ToString() + ">";
        }
        public Method Method
        {
            get { return m; }
            set { m = value; }
        }

        public override bool IsMethod
        {
            get
            {
                return true;
            }
        }
        public override bool Equals(object obj)
        {
            MethodNode mnode = obj as MethodNode;
            return (mnode != null) && this.m == mnode.m && base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }


    public class MethodDelegateNode : PTAnalysisNode
    {
        protected Method m;
        protected Nodes thisValues = Nodes.Empty;
        //protected TypeNode rtype;

        public MethodDelegateNode(Label lb, TypeNode t)
            : base(lb, t)
        {
            this.m = null;
        }

        public MethodDelegateNode(Label lb, TypeNode t, Method m)
            : base(lb, t)
        {
            this.m = m;
        }
        public override string ToString()
        {
            string mName = "";
            if (m != null)
                mName = m.Name.Name;
            return "<Delegate:" + mName + " - "+ nodeType + " " + lb.ToString() + ">";
        }
        public Method Method
        {
            get { return m; }
            set { m = value; }
        }

        public Nodes ThisValues
        {
            get { return thisValues; }
            set { thisValues = value; }
        }
        public override bool IsMethodDelegate
        {
            get
            {
                return true;
            }
        }
        public override bool IsInside
        {
            get
            {
                return true;
            }
        }
        public override bool Equals(object obj)
        {
            MethodDelegateNode mnode = obj as MethodDelegateNode;
            return (mnode != null) && this.m==mnode.m && base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }


    // public class ReferenceVariable : PTAnalysisNode
    public class VariableAddrNode : AddrNode, IVarRefNode
    {
        protected PT_Variable v;

        public VariableAddrNode(Label lb, PT_Variable v)
            : base(lb, v.Type)
        {
            this.v = v;
        }

        public PT_Variable Variable
        {
            get { return v; }
        }
        public override bool IsVariableReference
        {
            get
            {
                return true;
            }
        }
        public override string Name
        {
            get
            {
                if (Variable.Name != null)
                    return Variable.Name.Name;
                else
                    return "NullName";
            }
        }
        public override string ToString()
        {
            // return "<Ref: " + v.Name.Name + " " + lb.ToString() + ">";
            return "<" + this.Name +", " + this.Type.ToString()+ ">";
        }
        public override bool Equals(object obj)
        {
            VariableAddrNode rn = obj as VariableAddrNode;
            return rn != null && v.Equals(rn.v);

            //return this.v.Name.Name.Equals(this.v.Name.Name);
            //return rn!=null && v.UniqueKey.Equals(rn.v.UniqueKey) && 
            //    ((Type==null && rn.Type==null) || (Type.Equals(rn.Type)));
            //return rn != null && v.Equals(rn.v)
            //    && ((lb != null && rn.lb != null && lb.Equals(rn.lb)) ||
            //        (lb == null && rn.lb == null));
            
        }
        public override int GetHashCode()
        {
            //return v.GetHashCode();
            //return v.UniqueKey;
            //return v.Name.ToString().GetHashCode();
            // return v.UniqueKey + this.Type.GetHashCode();
            return v.GetHashCode();
        }

        #region IVarRefNode Members

        Variable IVarRefNode.ReferencedVariable
        {
            get
            {
                return Variable.Variable;
            }
        }

        #endregion


    }

    /// <summary>
    /// GNode: Represents Global (static) nodes
    /// It has neither Label nor Type
    /// </summary>
    public class GNode : ObjectAbsNode, IGNode
    {
        public static IGNode nGBL = new GNode();

        public GNode()
            : base(null, null)
        {
            lb = null;
        }

        public override string ToString()
        {
            string res = "nGBL";
            if (lb != null)
                res += " " + lb.ToString();
            return res;
        }
        public override string Name
        {
            get { return "nGBL"; }
        }
        public override bool IsGlobal
        {
            get { return true; }
        }
        public override bool Equals(object obj)
        {
            GNode g = obj as GNode;
            return g != null;
        }
        public override int GetHashCode()
        {
            return 0;
        }

    }

    public class NullNode : ObjectAbsNode
    {
        public static NullNode nullNode = new NullNode();
        public NullNode()
            : base(null, null)
        {
            lb = null;
        }
        public override bool Equals(object obj)
        {
            NullNode n = obj as NullNode;
            return n != null;
        }
        public override bool IsNull
        {
            get
            {
                return true;
            }
        }
        public override int GetHashCode()
        {
            return 1;
        }
        public override string Name
        {
            get { return "null"; }
        }
        public override string ToString()
        {
            return Name;
        }
    }

    /// INode: Represents objects created by the method under analysis 
    public class InsideNode : ObjectAbsNode, IINode
    {
        public InsideNode(Label lb, TypeNode t) : base(lb, t) { }
        public override bool IsInside
        {
            get
            {
                return true;
            }
        }
        public override string ToString()
        {
            return "<IN:" + nodeType + " " + lb.ToString() + ">";
        }
        public override bool Equals(object obj)
        {
            InsideNode inode = obj as InsideNode;
            return inode != null && base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

    }

    /// <summary>
    ///  An Inside Node representing the returned expression 
    /// </summary>
    public class RNode : InsideNode
    {
        public RNode(TypeNode t, Method m, Label lb) : base(lb, t) { }
        public override string ToString()
        {
            return "<RN:" + nodeType + " " + lb + ":" + lb.Method.Name + ">";
        }
    }

    /// <summary>
    ///  An Inside Node representing a struct  
    /// </summary>
    public class StructNode : /*InsideNode */ PTAnalysisNode, IStructNode
    {
        int count;
        static int counter = 0;
        public StructNode(Label lb, TypeNode t)
            : base(lb, t)
        {
            //counter++;
            count = counter;
        }
        public override bool IsStruct
        {
            get
            {
                return true;
            }
        }
        public override string ToString()
        {
            return "<StrN:" + nodeType + " " + lb + ":" + count.ToString() + ">";
        }
        public override string Name
        {
            get { return "Str:" + count.ToString(); }
        }
        public override bool Equals(object obj)
        {
            StructNode snode = obj as StructNode;
            return snode != null && base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    /// <summary>
    /// PNode: Represents objects pointed by a parameter
    /// </summary>
    public abstract class PNode : VariableAddrNode, IParameterNode
    {
        protected Method m;
        protected int i;
        
        public PNode(Method m, int i)
            : base(null, new PT_Variable(getParameter(m, i)))
        {
            this.m = m;
            this.i = i;
            // DIEGO: I get the reference type
            this.nodeType = getParamType().GetReferenceType();
            this.v = new PT_Variable(getParameter(m, i));
        }
        public override bool IsParameterNode
        {
            get
            {
                return true;
            }
        }
        private static Parameter getParameter(Method m, int i)
        {
            Parameter p;
            if (i == 0)
                p = m.ThisParameter;
            else
                p = m.Parameters[i - 1];
            return p;
        }

        #region IParameterNode Members
        public int ParameterNumber
        {
            get { return i; }
        }
        public Method Method
        {
            get { return this.m; }
        }
        #endregion
        
        public override bool Equals(object obj)
        {
            PNode pn = obj as PNode;
            return pn != null && this.i.Equals(pn.i) && this.m.Equals(pn.m);
        }
        public override int GetHashCode()
        {
            return this.m.GetHashCode() + i.GetHashCode();
        }
        public override string ToString()
        {
            string paraName = getParamName();
            return string.Format("<PN:{3} ({0}) {1} ({2})>", passingStyle(), paraName, i.ToString(),this.Type);
        }
        private String getParamName()
        {
            string paraName = "";
            if (i == 0)
                paraName = "this";
            else
                paraName = m.Parameters[i - 1].Name.Name;
            return paraName;
        }
        private TypeNode getParamType()
        {
            TypeNode t;
            if (i == 0)
                t = m.ThisParameter.Type;
            else
                t = m.Parameters[i - 1].Type;
            return t;
        }
        public override string Name
        {
            get { return getParamName(); }
        }
        public virtual bool IsByValue
        {
            get { return false; }
        }
        public virtual bool IsByRef
        {
            get { return false; }
        }
        public virtual bool IsOut
        {
            get { return false; }
        }
        public virtual bool IsReforOutParamNode
        {
            get { return IsOut || IsByRef; }
        }

        protected abstract string passingStyle();



    }

    /// <summary>
    /// PRefNode: Represents a parameter passed by Reference
    /// </summary>
    public class PRefNode : PNode
    {
        public PRefNode(Method m, int i) : base(m, i) { }

        public override bool IsByRef
        {
            get
            {
                return true;
            }
        }

        protected override string passingStyle() { return "ref"; }

        
        public override bool IsVariableReference
        {
            get
            {
                return true;
            }
        }

    }
    /// <summary>
    /// POutNode: Represents an out  parameter
    /// </summary>
    public class POutNode : PRefNode
    {
        public POutNode(Method m, int i) : base(m, i) { }
        public override bool IsOut
        {
            get
            {
                return true;
            }
        }

        protected override string passingStyle() { return "out"; }
    }

    /// <summary>
    /// PByValueNode: Represents a parameter passed by Value
    /// </summary>
    public class PByValueNode : PNode
    {
        public PByValueNode(Method m, int i) : base(m, i) { }

        public override bool IsByValue
        {
            get
            {
                return true;
            }
        }

        protected override string passingStyle() { return "value"; }
    }

    /// <summary>
    /// LNode: Represents a placeholder  for an unknown node(s) read by the instruction
    /// Typically from instructions like a = b.f with b escaping
    /// </summary>
    public abstract class LNode : PTAnalysisNode, ILoadNode
    {
        private bool isOmegaLoad = false;
        private bool isOmegaConfinedLoad = false;
        public LNode(Label lb, TypeNode type)
            : base(lb, type)
        {
            
        }
        public override bool IsLoad
        {
            get
            {
                return true;
            }
        }
        public override string ToString()
        {
            return "<LN:" + Type.ToString() + " " + lb.ToString() + ">";
        }
        public override bool Equals(object obj)
        {
            LNode ln = obj as LNode;
            return ln != null && base.Equals(ln); ;
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        public void SetOmegaConfinedLoadNode()
        {
            isOmegaConfinedLoad = true;
        }
        public void ResetOmegaConfinedLoadNode()
        {
            isOmegaConfinedLoad = false;
        }

        public void SetOmegaLoadNode()
        {
            isOmegaLoad = true;
        }
        public void ResetOmegaLoadNode()
        {
            isOmegaLoad = false;
        }
        public override bool IsOmegaLoad
        {
            get
            {
                return isOmegaLoad || isOmegaConfinedLoad;
            }
        }
        public override bool IsOmegaConfined
        {
            get
            {
                return isOmegaConfinedLoad;
            }
        }
      public override bool IsOmega {
        get {return IsOmegaLoad; }  
        }
    }

    public class LAddrNode : LNode, IAddrNode
    {
        public LAddrNode(Label lb, TypeNode type)
            : base(lb, type)
        {
        }
        public override string ToString()
        {
            return "<LAddr:" + Type.ToString() + " " + lb.ToString() + ">";
        }
        public override bool Equals(object obj)
        {
            LNode ln = obj as LAddrNode;
            return ln != null && base.Equals(ln); ;
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class LAddrParamNode : LAddrNode, IParameterNode
    {
        IParameterNode pn;
        public LAddrParamNode(Label lb, TypeNode type, IParameterNode pn)
            : base(lb, type)
        {
            this.pn = pn;
        }
        public override bool Equals(object obj)
        {
            LAddrParamNode lapn = obj as LAddrParamNode;
            return lapn!=null && base.Equals(lapn) && pn.Equals(lapn.pn);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode() + pn.GetHashCode(); ;
        }
        public override string ToString()
        {
            return "<LPAddr:" + ReferencedVariable + " " + lb.ToString() + ">";
        }

        #region IParameterNode Members

        public int ParameterNumber
        {
            get { return pn.ParameterNumber; }
        }
        public Method Method
        {
            get { return pn.Method; }
        }

        public bool IsByValue
        {
            get { return pn.IsByValue; }
        }

        public bool IsByRef
        {
            get { return pn.IsByRef; }
        }

        public bool IsOut
        {
            get { return pn.IsOut; }
        }
        public bool IsReforOutParamNode
        {
            get { return pn.IsReforOutParamNode; }
        }

        #endregion

        #region IVarRefNode Members

        public Variable ReferencedVariable
        {
            get { return pn.ReferencedVariable; }
        }

        #endregion
    }
    public class LValueNode : LNode, IObjectAbsNode
    {
        public LValueNode(Label lb, TypeNode type)
            : base(lb, type)
        {
        }

        public override bool IsObjectAbstraction
        {
            get { return true; }
        }
        public override string ToString()
        {
            return "<LVN:" + Type.ToString() + " " + lb.ToString() + ">";
        }
        public override bool Equals(object obj)
        {
            LNode ln = obj as LValueNode;
            return ln != null && base.Equals(ln); ;
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    #region LFieldAddrNode (to delete)
    /*
    public class LFieldAddrNode : LAddrNode
    {
        public LFieldAddrNode(Label lb, TypeNode type)
            : base(lb, type)
        {
        }
        public override string ToString()
        {
            return "<LFN:" + Type.ToString() + " " + lb.ToString() + ">";
        }
        public override bool Equals(object obj)
        {
            LNode ln = obj as LFieldAddrNode;
            return ln != null && base.Equals(ln); ;
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
    */ 
    #endregion

    // This kind of node is to refer the value pointed by a parameter (by value)
    public class PLNode : LValueNode, IParameterValueNode, IAddrNode
    {
        bool isOmegaConfined = false;
        bool isOmega = false;

        IAddrNode pn;
        public PLNode(Label lb, TypeNode type, IAddrNode pn)
            : base(lb, type)
        {
            this.pn = pn;
        }

        #region IParameterValueNode Members
        public IPTAnalysisNode ParameterReference
        {
            get { return pn; }
        }
        #endregion
        
        public override bool IsParameterValueLNode
        {
            get { return true;}
        }
        public void SetOmegaNode()
        {
            isOmega = true;
        }
        public void ResetOmegaNode()
        {
            isOmega = false;
        }
        public void SetOmegaConfined()
        {
            isOmegaConfined = true;
        }
        public void ResetOmegaConfined()
        {
            isOmegaConfined = false;
        }
        public override bool IsOmega
        {
            get
            {
                return isOmega || IsOmegaConfined || IsOmegaLoad;
            }
        }
        public override bool IsOmegaConfined
        {
            get
            {
                return isOmegaConfined;
            }
        }
        
        public override string ToString()
        {
            //return "<PLN:" + Type.ToString() + " " + lb.ToString() + ">";
            return "<PLN:" + pn.ToString() + ">";
        }
        public override bool Equals(object obj)
        {
            PLNode ln = obj as PLNode;
            //return ln != null && base.Equals(ln); ;
            return ln != null && pn.Equals(ln.pn);
        }
        public override int GetHashCode()
        {
            return 1 + pn.GetHashCode();
        }
    }

    /// <summary>
    /// Set of nodes
    /// </summary>
    
    public class Nodes : Set<IPTAnalysisNode>
    {
        public Nodes()
            : base()
        {
        }
        public Nodes(Nodes ns)
            : base(ns)
        {
        }
        public void Remove(Nodes ns)
        {
            foreach (IPTAnalysisNode n in ns)
            {
                Remove(n);
            }
        }
        public static new Nodes Empty
        {
            get { return new Nodes(); }
        }

        public static Nodes Singleton(IPTAnalysisNode node) {
          var x = new Nodes();
          x.Add(node);
          return x;
        }
        
        
    }
    
    
    #endregion

    #region PointsTo Analysis Edges Representation
    #region Edge's Type
    /// <summary>
    /// Edge: An edge in the pointsTo graph
    /// </summary>
    public class Edge
    {
        protected IPTAnalysisNode src, dst;
        protected Field f;
        
        public Edge(IPTAnalysisNode src, Field f, IPTAnalysisNode dst)
        {
            this.src = src;
            this.dst = dst;
            this.f = f;
        }
        public Edge(IPTAnalysisNode src, IPTAnalysisNode dst)
        {
            this.src = src;
            this.dst = dst;
            this.f = PTGraph.allFields;
        }
        public IPTAnalysisNode Src
        {
            get { return src; }
            set { src = value; }
        }
        public IPTAnalysisNode Dst
        {
            get { return dst; }
            set { dst = value; }
        }
        public Field Field
        {
            get { return f; }
            set { Field = value; }
        }
        public override bool Equals(object obj)
        {
            Edge ie = obj as Edge;
            return ie != null && this.dst.Equals(ie.dst) && this.src.Equals(ie.src) && this.f.Equals(ie.f);
        }
        public override int GetHashCode()
        {
            return this.src.GetHashCode() + this.dst.GetHashCode() + this.f.GetHashCode();
        }
        public override string ToString()
        {
            return "[" + src.ToString() + "," + f.Name.Name + "," + dst.ToString() + "]";
        }
        public void EdgeToDot(System.IO.TextWriter output, bool inside)
        {
            Edge e = this;
            string fString = "";
            if (e.Field.Equals(PTGraph.asterisk))
                fString = "*";
            else if (e.Field.Equals(PTGraph.arrayField))
                fString = "[.]";
            else fString = e.Field.Name.Name;

            string edgeStyle = "style = dotted";
            if (inside)
                edgeStyle = "style = solid";

            if (e is StructEdge)
                edgeStyle += ", color = green";
            else
                edgeStyle += ", color = black";

            string source = e.Src.ToString();
            if (e.Src.IsClousure)
                source += "(*)";
            string dest = e.Dst.ToString();
            if (e.Dst.IsClousure)
                dest += "(*)";
            output.WriteLine("\"{0}\" -> \"{2}\" [label = \"{1}\",{3}]", source, fString, dest, edgeStyle);
        }
        public virtual void EdgeToDot(System.IO.TextWriter output)
        {
            EdgeToDot(output,false);
        }
    }
    /// <summary>
    /// IEdge: An internal edge. Represents a reference created by the analyzed method (writes)
    /// </summary>
    public class IEdge : Edge
    {
        public IEdge(IPTAnalysisNode src, Field f, IPTAnalysisNode dst)
            : base(src, f, dst)
        {
        }
        public override void  EdgeToDot(TextWriter output)
        {
 	         base.EdgeToDot(output,true);
        }
        public override string ToString()
        {
            return "[IE:" + src.ToString() + "," + f.Name.Name + "," + dst.ToString() + "]";
        }
    }
    /// <summary>
    /// OEdge: An outside edge. Represents a reference to a node outside the analyzed method (reads) 
    /// </summary>
    public class OEdge : Edge
    {
        public OEdge(IPTAnalysisNode src, Field f, LNode dst)
            : base(src, f, dst)
        {
        }
        public OEdge(IPTAnalysisNode src, Field f, IPTAnalysisNode dst)
            : base(src, f, dst)
        {
        }
        public override void  EdgeToDot(TextWriter output)
        {
 	         base.EdgeToDot(output,false);
        }
        public override string ToString()
        {
            return "[OE:" + src.ToString() + "," + f.Name.Name + "," + dst.ToString() + "]";
        }

    }
    
    /// <summary>
    /// StructEdge: An edge that connects an Struct Type with another element. 
    /// It is used to know which elements has to be copied from a struct 
    /// </summary>
    public class StructEdge : Edge
    {
        public StructEdge(IPTAnalysisNode src, Field f, IPTAnalysisNode dst)
            : base(src, f, dst)
        {
        }

        public override string ToString()
        {
            return "[StrE:" + src.ToString() + "," + f.Name.Name + "," + dst.ToString() + "]";
        }
    }
    #endregion

    #region Edges Set
    /// <summary>
    /// Set of edges
    /// </summary>
    public class Edges : Set<Edge>
    {
        private Dictionary<IPTAnalysisNode, Nodes> adjacentsForward =
            new Dictionary<IPTAnalysisNode, Nodes>();
        private Dictionary<IPTAnalysisNode, Nodes> adjacentsBackward =
            new Dictionary<IPTAnalysisNode, Nodes>();
        private Dictionary<IPTAnalysisNode, Edges> edgesBySrc
            = new Dictionary<IPTAnalysisNode, Edges>();
        private Dictionary<IPTAnalysisNode, Edges> edgesByDst
            = new Dictionary<IPTAnalysisNode, Edges>();
        private Dictionary<Field, Edges> edgesByField
            = new Dictionary<Field, Edges>();
        private Dictionary<Pair<IPTAnalysisNode, Field>, Edges> adjacentsEdgesForwardByNodeAndField
            = new Dictionary<Pair<IPTAnalysisNode, Field>, Edges>();
        private Dictionary<Pair<IPTAnalysisNode, Field>, Edges> adjacentsEdgesBackwardByNodeAndField
            = new Dictionary<Pair<IPTAnalysisNode, Field>, Edges>();


        public Edges()
            : base()
        {
        }
        public Edges(Edges IEs)
            : base((Set<Edge>)IEs)
        {
            rebuildMaps();
        }

        private void rebuildMaps()
        {
            adjacentsBackward.Clear();
            adjacentsForward.Clear();
            foreach (Edge e in this)
            {
                addToMap(adjacentsForward, e.Src, e.Dst);
                addToMap(adjacentsBackward, e.Dst, e.Src);
                addToMap(edgesBySrc, e.Src, e);
                addToMap(edgesByDst, e.Dst, e);
                addToMap(edgesByField, e.Field, e);
                addToMap(adjacentsEdgesForwardByNodeAndField, new Pair<IPTAnalysisNode, Field>(e.Src, e.Field), e);
                addToMap(adjacentsEdgesBackwardByNodeAndField, new Pair<IPTAnalysisNode, Field>(e.Dst, e.Field), e);
            }
        }


        private void addToMap<T, NS, N>(Dictionary<T, NS> map, T key, N n)
            where NS : Collections.Generic.Set<N>, new()
        {
            NS nodes;
            if (map.ContainsKey(key))
            {
                nodes = map[key];
            }
            else
            {
                nodes = new NS();
                map[key] = nodes;
            }
            nodes.Add(n);
        }

        public void AddEdge(Edge e)
        {
            addToMap(adjacentsForward, e.Src, e.Dst);
            addToMap(adjacentsBackward, e.Dst, e.Src);
            addToMap(edgesBySrc, e.Src, e);
            addToMap(edgesByDst, e.Dst, e);
            addToMap(edgesByField, e.Field, e);
            addToMap(adjacentsEdgesForwardByNodeAndField, new Pair<IPTAnalysisNode, Field>(e.Src, e.Field), e);
            addToMap(adjacentsEdgesBackwardByNodeAndField, new Pair<IPTAnalysisNode, Field>(e.Dst, e.Field), e);


            this.Add(e);
        }
        private void removeFromToMap<T, NS, N>(Dictionary<T, NS> map, T key, N n)
            where NS : Collections.Generic.Set<N>
        {
            if (map.ContainsKey(key))
            {
                if (map[key].Contains(n))
                    map[key].Remove(n);
            }
        }
        public void RemoveEdge(Edge e)
        {
            Remove(e);
            removeFromToMap(adjacentsForward, e.Src, e.Dst);
            removeFromToMap(adjacentsBackward, e.Dst, e.Src);
            removeFromToMap(edgesBySrc, e.Src, e);
            removeFromToMap(edgesBySrc, e.Dst, e);
            removeFromToMap(edgesByField, e.Field, e);
            removeFromToMap(adjacentsEdgesForwardByNodeAndField, new Pair<IPTAnalysisNode, Field>(e.Src, e.Field), e);
            removeFromToMap(adjacentsEdgesBackwardByNodeAndField, new Pair<IPTAnalysisNode, Field>(e.Dst, e.Field), e);
        }

        public void AddEdge(IPTAnalysisNode src, Field f, IPTAnalysisNode dst)
        {
            Edge e = new Edge(src, f, dst);
            AddEdge(e);
        }
        public void AddIEdge(IPTAnalysisNode src, Field f, IPTAnalysisNode dst)
        {
            IEdge e = new IEdge(src, f, dst);
            AddEdge(e);
        }
        public void AddOEdge(IPTAnalysisNode src, Field f, LNode dst)
        {
            OEdge e = new OEdge(src, f, dst);
            AddEdge(e);
        }
        public void AddOEdge(IPTAnalysisNode src, Field f, IPTAnalysisNode dst)
        {
            OEdge e = new OEdge(src, f, dst);
            AddEdge(e);
        }
        public void AddSEdge(IPTAnalysisNode src, Field f, IPTAnalysisNode dst)
        {
            StructEdge e = new StructEdge(src, f, dst);
            AddEdge(e);
        }
        public void AddEdges(Edges es)
        {
            foreach (Edge e in es)
                AddEdge(e);
        }
        public void AddEdges(Set<Edge> es)
        {
            foreach (Edge e in es)
                AddEdge(e);
        }


        public Nodes Adjacents(IPTAnalysisNode n, bool forward)
        {
            Nodes adjs = Nodes.Empty;
            if (forward && adjacentsForward.ContainsKey(n))
                adjs = adjacentsForward[n];
            if (!forward && adjacentsBackward.ContainsKey(n))
                adjs = adjacentsBackward[n];
            return adjs;
        }
        public IPTAnalysisNode Next(IPTAnalysisNode n)
        {
            Nodes adjs = Adjacents(n, true);
            return adjs.PickAnElement();
        }
        public IPTAnalysisNode Prev(IPTAnalysisNode n)
        {
            Nodes adjs = Adjacents(n, false);
            return adjs.PickAnElement();
        }
        public AField NextWithField(IPTAnalysisNode n)
        {
            Set<AField> adjs = AdjancentsWithField(n, true);
            return adjs.PickAnElement();
        }

        private Edges edgesByNode(IPTAnalysisNode n, bool forward)
        {
            Edges edges = new Edges();
            if (forward)
            {
                if (edgesBySrc.ContainsKey(n))
                    edges = edgesBySrc[n];
            }
            else
            {
                if (edgesByDst.ContainsKey(n))
                    edges = edgesByDst[n];
            }
            return edges;
        }
        private Edges edgesByNodeAndField(IPTAnalysisNode n, Field f, bool forward)
        {
            Pair<IPTAnalysisNode, Field> pNF = new Pair<IPTAnalysisNode, Field>(n, f);

            Edges edges = new Edges();
            if (forward)
            {
                if (adjacentsEdgesForwardByNodeAndField.ContainsKey(pNF))
                    edges = adjacentsEdgesForwardByNodeAndField[pNF];
            }
            else
            {
                if (adjacentsEdgesBackwardByNodeAndField.ContainsKey(pNF))
                    edges = adjacentsEdgesBackwardByNodeAndField[pNF];
            }
            return edges;

        }

        public Set<AField> AdjancentsWithField(IPTAnalysisNode n, bool forward)
        {
            Set<AField> adjs = new Set<AField>();
            Edges edges = edgesByNode(n, forward);
            foreach (Edge ie in edges)
            {
                if (forward)
                    adjs.Add(new AField(ie.Dst, ie.Field));
                if (!forward)
                    adjs.Add(new AField(ie.Src, ie.Field));
            }
            return adjs;
        }

        public Set<AField> AdjancentsWithField(IPTAnalysisNode n, Field f, bool forward)
        {
            Set<AField> adjs = new Set<AField>();
            Edges edges = edgesByNodeAndField(n, f, forward);
            foreach (Edge ie in edges)
            {
                if (forward)
                    adjs.Add(new AField(ie.Dst, ie.Field));
                if (!forward)
                    adjs.Add(new AField(ie.Src, ie.Field));

            }
            return adjs;
        }
        public Edges EdgesByNodeAndField(IPTAnalysisNode n, Field f, bool forward)
        {
            Edges edges = edgesByNodeAndField(n, f, forward);
            //foreach (Edge ie in edges)
            //{
            //    if (ie.Field.Equals(f))
            //    {
            //        if (forward && n.Equals(ie.Src))
            //            adjs.Add(ie);
            //        if (!forward && n.Equals(ie.Dst))
            //            adjs.Add(ie);
            //    }
            //}
            //return adjs;
            return edges;
        }


        public Nodes Adjacents(IPTAnalysisNode n, Field f, bool forward)
        {
            Nodes adjs = Nodes.Empty;
            Edges edges = edgesByNodeAndField(n, f, forward);
            foreach (Edge ie in edges)
            {
                if (forward)
                    adjs.Add(ie.Dst);
                if (!forward)
                    adjs.Add(ie.Src);
            }
            return adjs;
        }
        public Set<Edge> EdgesFromSrc(IPTAnalysisNode src)
        {
            Edges edges = edgesByNode(src, true);
            return edges;
        }

        public Set<Edge> EdgesFromDst(IPTAnalysisNode dst)
        {
            Edges edges = edgesByNode(dst, false);
            return edges;
        }

        public Set<Edge> EdgesFromField(Field f)
        {
            Set<Edge> edges = new Set<Edge>();
            if (edgesByField.ContainsKey(f))
                edges = edgesByField[f];
            return edges;
        }

        public Set<Edge> EdgesFromSrcAndField(IPTAnalysisNode src, Field f)
        {
            Edges edges = edgesByNodeAndField(src, f, true);
            return edges;
        }


        public void RemoveEdges(IPTAnalysisNode src, Field f)
        {
            Set<Edge> edges = EdgesFromSrcAndField(src, f);
            foreach (Edge e in edges)
                RemoveEdge(e);
        }
        public void RemoveEdges(Edges es)
        {
            foreach (Edge e in es)
                RemoveEdge(e);
        }
        public void RemoveEdges(Set<Edge> es)
        {
            foreach (Edge e in es)
                RemoveEdge(e);
        }
        #region DOT Grraph
        public void GenerateDotGraph(string filename)
        {
            using (StreamWriter sw = new StreamWriter(filename))
            {
                GenerateDotGraph(sw);
            }
        }
        public void GenerateDotGraph(System.IO.TextWriter output)
        {
            GenerateDotGraph(output, false);
        }
        public void GenerateDotGraph(System.IO.TextWriter output, bool reduced)
        {

            output.WriteLine("digraph " + "fromEdges" + " {");
            /*
            foreach (PT_Variable v in LV.Keys)
            {
                Nodes varAddr = LV[v];
                string varName = "NullName";

                if (v != null & v.Name != null)
                    //varName = v.Name.Name;
                    varName = reduced ? v.Name.Name : v.ToString();

                output.WriteLine("\"{0}\" [shape = none]", varName);

                foreach (IPTAnalysisNode n in varAddr)
                {
                    output.WriteLine("\"{0}\" -> \"{2}\" [label = \"{1}\"]", varName, "", n);
                }
            }
            */

            Nodes ns = Nodes.Empty;
            foreach (Edge e in this)
            {
                ns.Add(e.Src);
                ns.Add(e.Dst);
            }
            
            int cont = 0;
            foreach (IPTAnalysisNode n in ns)
            {
                cont++;
                string name = reduced ? cont.ToString() : n.ToString();

                // output.WriteLine("\nnode [shape = ellipse]");
                string shape = "box";
                if (n.IsAddrNode)
                {
                    shape = "ellipse";
                    if (n.IsVariableReference)
                        name = reduced ? n.Name : n.ToString();
                }

                if (n.IsOmega || n.IsOmegaConfined || n.IsOmegaLoad)
                {
                    string oStr = "W";
                    if (n.IsOmegaConfined)
                        oStr += "C";
                    if (n.IsOmegaLoad)
                        oStr += "L";
                    name = "(" + oStr + ")" + name;

                }
                if (n.IsGlobal)
                    shape = "pentagon";
                string style = "solid";
                if (n.IsLoad || n.IsParameterValueLNode)
                    style = "dotted";

                String nodeToStr = n.ToString();
                string dotNode = String.Format("\"{0}\" [label = \"{1}\", shape = {2}, style = {3} ]",
                    nodeToStr, name, shape, style);
                output.WriteLine(dotNode);
            }




            foreach (Edge e in this)
            {
                e.EdgeToDot(output, true);
            }
            
            output.WriteLine("}");
        }

        
        #endregion

    }
    #endregion

    #endregion

    #region PointsTo Analysis Variable-Address Mapping
    /// <summary>
    /// LocVar: A mapping between variables and nodes it may point to.
    /// This mapping is used to record points to information
    /// </summary>
    public class LocVar : Dictionary<PT_Variable, Nodes>
    {
        public LocVar() : base() { }
        public LocVar(LocVar lv)
            : base(lv)
        {
        }
        public bool Includes(LocVar lv2)
        {
            foreach (PT_Variable v in lv2.Keys)
            {
                
                Nodes locs2 = lv2[v];
                if (locs2.Count != 0)
                {
                    if (this.ContainsKey(v))
                    {
                        Nodes locs1 = this[v];
                        if (!locs2.IsSubset(locs1))
                            return false;
                    }
                    else
                        return false;
                }
            }
            return true;
        }
        public static bool AtMost(LocVar lv1, LocVar lv2)
        {
            return lv2.Includes(lv1);
        }
        public void Join(LocVar lv2)
        {
            foreach (PT_Variable v in lv2.Keys)
            {
                Nodes locs2 = lv2[v];
                Nodes locs1 = Nodes.Empty;
                

                if (this.ContainsKey(v))
                {
        
                    locs1.AddRange(this[v]);
                }

                locs1.AddRange(locs2);
                this[v] = locs1;
            }
        }

        public void Add(PT_Variable v, IPTAnalysisNode n)
        {
            Nodes ns = Nodes.Empty;
            if (this.ContainsKey(v))
            {
                ns = this[v];
            }
            else
            {
                this[v] = ns;
            }
            ns.Add(n);
        }
        public void AddRange(PT_Variable v, Nodes ns)
        {
            if (this.ContainsKey(v))
            {
                this[v].AddRange(ns);
            }
            else
            {
                this[v] = ns;
            }
        }


        public override string ToString()
        {
            string res = "{";
            bool first = true;
            foreach (PT_Variable v in this.Keys)
            {
                if (!first)
                    res += ", ";
                if(v!=null && v.Name!=null)
                    res += string.Format("{0} -> {1}", v.Name.Name, this[v]);
                else
                    res += string.Format("{0} -> {1}", "NullName" , this[v]);
                first = false;
            }

            res += "}";
            return res;
        }
        public override bool Equals(object obj)
        {
            LocVar lv = obj as LocVar;

            return lv != null && this.Includes(lv) && lv.Includes(this);
        }
        public override int GetHashCode()
        {
            return Keys.GetHashCode() + Values.GetHashCode();
        }
    }
    #endregion

    #region Variables, Fields, Properties
    public class PT_Variable
    {
        Variable v;
        TypeNode type;
        public PT_Variable(Variable v):this(v, v.Type)
        {
            
        }
 
        public PT_Variable(Variable v, TypeNode t)
        {
            this.v = v;
            this.type = t;
        }
        public Variable Variable
        {
            get { return v; }
        }
        public TypeNode Type
        {
            get { return type; }
        }
    
        public override bool Equals(object obj)
        {
            PT_Variable ptv = obj as PT_Variable;
            //bool eq1 = ptv != null && this.v.Name.Name.Equals(ptv.v.Name.Name);
            bool eq2 = ptv != null && ((type == null && ptv.type == null) || type.Equals(ptv.type)); 
            bool eq3 = ptv != null && v.UniqueKey == ptv.v.UniqueKey;
            bool eq4 = ptv != null && this.v.GetHashCode() == ptv.v.GetHashCode();
            //if (eq1 && !eq3 /*&& !v.Name.Name.StartsWith("stack")*/)
            { 
               //System.Diagnostics.Debugger.Break();
            }
            //if (eq1 && !eq4)
            {
                //System.Diagnostics.Debugger.Break();
            }
            return eq4;
            //return ptv != null && this.v.Name.Name.Equals(ptv.v.Name.Name) 

        }
        public override int GetHashCode()
        {
            return v.GetHashCode();
            //return v.Name.ToString().GetHashCode();// +(type!=null?type.GetHashCode():0); 
            //return v.UniqueKey + type.GetHashCode();
        }
        public Identifier Name
        {
            get { return v.Name;  }
        }
        public override string ToString()
        {
            return "<" + v.Name + "," + v.Type + ">";
        }

    }
    #endregion


    #region PoinsTo Analysis Heap Location Update Representation
    /// <summary>
    /// AField: A field update. An element of a write effect
    /// </summary>
    public class AField
    {
        IPTAnalysisNode n;
        Field f;
        Label lb;
        bool wasNull;

        #region Properties
        public IPTAnalysisNode Src
        {
            get { return n; }
        }
        public Field Field
        {
            get { return f; }
        }
        public Label Label
        {
            get { return lb; }
        }
        public bool WasNull
        {
            get { return wasNull; }
        }
        #endregion

        #region Constructors
        public AField(IPTAnalysisNode n, Field f, Label lb)
        {
            this.n = n;
            this.f = f;
            this.lb = lb;
        }
        public AField(IPTAnalysisNode n, Field f, Label lb, bool wasNull)
        {
            this.n = n;
            this.f = f;
            this.lb = lb;
            this.wasNull = wasNull;
        }


        public AField(IPTAnalysisNode n, Field f)
        {
            this.n = n;
            this.f = f;

        }
        #endregion

        #region Equals, Hash, ToString
        public override bool Equals(object obj)
        {
            
            AField af = obj as AField;
            return af != null && this.n.Equals(af.n) && this.f.Equals(af.f)
                && ((lb == null && af.lb == null) || ( lb!=null && lb.Equals(af.lb) ));
        }
        public override int GetHashCode()
        {
            if (lb == null)
                return n.GetHashCode() + f.GetHashCode();
            else
                return n.GetHashCode() + f.GetHashCode() + lb.GetHashCode();
        }
        public override string ToString()
        {
            string nodeString = n.ToString();
            if (n.IsGlobal)
            {
                if (f.DeclaringType != null)
                    nodeString = "nGBL: " + f.DeclaringType.Name.Name;
                else
                    nodeString = "nGBL";
            }
            string res;
            if (lb == null)
                res = string.Format("<{0},{1}>", nodeString, f.Name.Name);
            else
                res = string.Format("<{0},{1}:{2}>", nodeString, f.Name.Name, lb.ToString());
            return res;
        }
        #endregion
    }
    #endregion

    /// <summary>
    /// A points-to Graph. This is the structure that represents the points-to information for a program point.
    /// The model supports Reference types (object subclasses) and struct types. 
    /// Every program variable has a related node that represents its address. 
    /// An address node can point to one of several locations (the object/s in the heap). 
    /// We say that the value/s of an Address are the heap locations that the variable (or address)
    /// can point to.
    /// That means, given a = Addr(v), *a represent the actual object abstraction in the heap.
    /// For struct types Addr(v) directly contains the struct element
    /// For fields the reasoning is the same. A heap location represent an object (or several) that
    /// can have several attributes (fields). If the field type is a reference type, it will have
    /// an address, and the address will point to the heap location representing the field dereference.
    /// Example: <c>v.f = *((*Addr(v)).f)</c> for v and v.f being reference types. 
    /// For struct type the reasoning is similar as the addr/value mentioned before. 
    /// A pointsToGraph G = &lt; I &#44; O &#44; LV &#44; E &gt;
    /// I = set of inside edges: references generated by the method under analysis
    /// O = set of outside edges: read refences to elements not known at that program point
    /// LV = a mapping from variables to the heap locations it may point to (in particular 
    /// it is a one to one mapping between a variable and its address or associated struct)
    /// 
    /// There are different kind of nodes. 
    /// AddrNodes: An address of a variable or field
    /// LAddrNode: An address node create to reflect a loaded (and unknown) address 
    /// LNode: A node that represents a loaded (and unknown) object. There are subclasses
    /// to represent whether the unknown node is a value (content of an address) like LValueNode 
    /// (and PLNode for parameters values) or a field value (content of a field dereference 
    /// address) like LFNode
    /// PNode: A node that represents the address of a parameter. There are subclasses to represent 
    /// the parameter passing style (PByValue, PByRef, POut)
    /// InsideNode: A newly created object 
    /// StructNode: A node that represent a struct value type
    /// </summary>
    public class PTExtraField : Field
    {
        public PTExtraField(Identifier name)
            : base(name)
        { }
        public override bool Equals(object obj)
        {
            Field f = obj as Field;
            if (f == null)
                return false;
            if (this == f)
                return true;
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            if (this == PTGraph.allFields) return 1;
            if (this == PTGraph.allFieldsNotOwned) return 2;
            if (this == PTGraph.arrayField) return 3;
            if (this == PTGraph.asterisk) return 4;
            return base.GetHashCode()+5;
        }
    }
  
    public class PTGraph : AbstractValue, IDataFlowState
    {
        #region Attributes
        #region Semilattices Attributes
        Edges iEdges;
        Edges oEdges;
        LocVar locVar;
        Nodes escapingNodes;
        public static bool ExpandedToString = false;
        
        
        #endregion

        #region Special Fields ([.], Pointer, AllFields)
        /// <summary>
        /// Special fields used for array indexing and pointer 
        /// </summary>
        public static readonly Field allFields = new PTExtraField(new Identifier("?"));
        public static readonly Field allFieldsNotOwned = new PTExtraField(new Identifier("$"));
        public static readonly Field asterisk = new PTExtraField(new Identifier("*"));
        public static readonly Field arrayField = new PTExtraField(new Identifier("__arr__"));
        #endregion

        /// <summary>
        ///  The set of parameter nodes
        /// </summary>
        Set<PNode> addrParameterNodes;
        /// <summary>
        /// The method under analysis
        /// </summary>
        Method m;
        /// <summary>
        /// The return value of the method
        /// </summary>
        Variable vRet;

        // Used for Delegates
        internal Dictionary<string, Variable> cachedVariables = new Dictionary<string, Variable>();

        /// <summary>
        /// This variable represent a global variable 
        /// </summary>
        public static readonly Variable GlobalScope = new Local(new Identifier("Global")
            , TypeNode.GetTypeNode(Type.GetType("System.Object")));

        /// <summary>
        /// Represent the location pointed by this 
        /// </summary>
        PNode thisParameterNode;
        /// <summary>
        /// A mapping to access the parameter nodes using parameters
        /// </summary>
        Dictionary<Parameter, PNode> parameterMap;
        Dictionary<Parameter, IPTAnalysisNode> parameterOldValue;

        // Used for nonAnalyzable methods only
        private Dictionary<Parameter, Nodes> leaves = new Dictionary<Parameter,Nodes>();

        public Nodes NonAnalyzableLeaves(Parameter p)
        {
            return leaves[p];
        }

        Label methodLabel;
        #endregion

        #region Properties
        #region Semilattice properties
        public Edges I
        {
            get { return iEdges; }
            set { iEdges = value; }
        }

        public Edges O
        {
            get { return oEdges; }
            set { oEdges = value; }
        }

        public Nodes E
        {
            get { return escapingNodes; }
            set { E.Clear(); E.AddRange(value); }
        }
        public LocVar LV
        {
            get { return locVar; }
            set { locVar = value; }
        }
        #endregion
        
        public Nodes PVNodes
        {
            get {   
                    return this.Values(AddrPNodes); 
                }
        }

        public Set<PNode> AddrPNodes
        {
            get { return addrParameterNodes; }
            set { addrParameterNodes = value; }
        }
        public Label MethodLabel
        {
            get { return methodLabel; }
        }
        public Method Method
        {
            get { return m; }
        }

        public PNode ThisParameterNode
        {
            get { return thisParameterNode; }
        }

        /// <summary>
        /// Get the parameter represented by a PNode
        /// </summary>
        /// <param name="pn"></param>
        /// <returns></returns>
        public Parameter Parameter(PNode pn)
        {
            Parameter p;
            if (pn.ParameterNumber == 0)
                p = Method.ThisParameter;
            else
            {
                p = Method.Parameters[pn.ParameterNumber - 1];
            }
            return p;
        }

        public Dictionary<Parameter, PNode> ParameterMap
        {
            get { return parameterMap; }
        }

        public Variable RetValue
        {
            get { return vRet; }
        }

        public Set<LNode> LoadNodes
        {
            get
            {
                Set<LNode> res = new Set<LNode>();
                foreach (Edge ie in I)
                {
                    //if (!ie.Field.Equals(PTGraph.asterisk))
                    {
                        if (ie.Src.IsLoad)
                            res.Add((LNode)ie.Src);
                        if (ie.Dst.IsLoad)
                            res.Add((LNode)ie.Dst);
                    }
                }
                foreach (Edge ie in O)
                {
                    //if (!ie.Field.Equals(PTGraph.asterisk))
                    {
                        if (ie.Src.IsLoad)
                            res.Add((LNode)ie.Src);
                        if (ie.Dst.IsLoad)
                            res.Add((LNode)ie.Dst);
                    }
                }
                return res;
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Construct an empty pointsToGraph for the method
        /// </summary>
        /// <param name="m"></param>
        public PTGraph(Method m)
        {
            initPTGraphAttributes(m);

            InitParams(m, methodLabel,false);

            //AddVariable(GlobalScope,methodLabel);
            //SetLoadField(GetLocations(GlobalScope), PTGraph.asterisk, nGBL);

            
        }
        private void initPTGraphAttributes(Method m)
        {
            this.iEdges = new Edges();
            this.oEdges = new Edges();
            this.escapingNodes = Nodes.Empty;
            this.locVar = new LocVar();
            this.addrParameterNodes = new Set<PNode>();
            this.parameterMap = new Dictionary<Parameter, PNode>();
            this.parameterOldValue = new Dictionary<Parameter, IPTAnalysisNode>();
            this.m = m;
            this.methodLabel = new Label(m.GetHashCode(), m);
            Assign(GlobalScope, GNode.nGBL, methodLabel);
            if (!CciHelper.IsVoid(m.ReturnType) && !CheckPrimitive(m.ReturnType))
                this.vRet = new RetValue(m);
        }

        Label NextMethodLabel()
        {
            methodLabel = methodLabel.Next();
            return methodLabel;
        }
        
        public static PTGraph CreatePTGraphForStronglyPureMethod(Method m)
        {
            PTGraph res = new PTGraph(m);
            if (res.vRet != null)
            {
                res.Assign(res.vRet, new RNode(res.vRet.Type, m, res.methodLabel), res.methodLabel);
            }
            return res;
        }

        public PTGraph(Method m, bool fromNonAnalizable)
        {
            
            initPTGraphAttributes(m);
            InitParams(m, methodLabel, fromNonAnalizable);

            
        }
        // Get reference node (object, structRef) reachble from a node
        public Nodes getReachRefs(Nodes ns)
        {
            Nodes res = new Nodes();
            foreach (IPTAnalysisNode n in ns)
	        {
		        res.AddRange(getReachRefs(n));
	        }
            
            return res;
        }

        // Get reference node (object, structRef) reachble from a node
        public Nodes getReachRefs(IPTAnalysisNode n)
        {
            Nodes res = NodesReachableFrom(n, true);
            Nodes resFiltered = Nodes.Empty;
            foreach (IPTAnalysisNode n2 in res)
            {
                if (isStructRef(n2.Type) || isObject(n2.Type))
                    resFiltered.Add(n2);
            }
            return resFiltered;
        }

        public Nodes getReachRefsOwned(IPTAnalysisNode n)
        {
            Nodes res = NodesReachableFromNodesWithOwnership(Nodes.Singleton(n), true);
            Nodes resFiltered = Nodes.Empty;
            foreach (IPTAnalysisNode n2 in res)
            {
                if (isStructRef(n2.Type) || isObject(n2.Type))
                    resFiltered.Add(n2);
            }
            return resFiltered;
        }

        /// <summary>
        /// Create the parameter nodes for each method's parameter
        /// It consider the type of parameter passing (value, out, or ref)
        /// </summary>
        /// <param name="m"></param>
        private void InitParams(Method m, Label lb, bool fromNonAnalizable)
        {
            if (!m.IsStatic)
            {
                Nodes nPThis = Nodes.Empty;
                PNode pThis = new PByValueNode(m, 0);
                PT_Variable thisParameter = new PT_Variable(m.ThisParameter);
                LV[thisParameter] = Nodes.Singleton(pThis);
                //LV[m.ThisParameter] = Nodes.Singleton(pThis);
                
                thisParameterNode = pThis;
                parameterMap[m.ThisParameter] = pThis;
                leaves[m.ThisParameter] = Nodes.Empty;
                addrParameterNodes.Add(pThis);

                /* Diego 12/8/2007 
                this.GetValuesIfEmptyLoadNode(m.ThisParameter, methodLabel);
                Nodes ns = Values(m.ThisParameter);
                parameterOldValue[m.ThisParameter] = ns.PickOne();
                I replace this with this */
                bool isWriteConfined = PointsToAndEffectsAnnotations.IsWriteConfinedParameter(m,m.ThisParameter);
                // IParameterValueNode pthisV = this.attachValueforParameter(m.ThisParameter, pThis, methodLabel, fromNonAnalizable,isWriteConfined);
                IPTAnalysisNode pthisV = this.attachValueforParameter(m.ThisParameter, pThis, methodLabel, fromNonAnalizable, isWriteConfined);
                parameterOldValue[m.ThisParameter] = pthisV;
                
            }
            if (m.Parameters != null)
            {
                for (int i = 0; i < m.Parameters.Count; i++)
                {
                    Parameter p = m.Parameters[i];
                    leaves[p] = Nodes.Empty;
                    IParameterNode pAddr = initParameter(p, i);
                    /* Diego 12/8/2007
                    this.GetValuesIfEmptyLoadNode(p, methodLabel);
                    parameterOldValue[p] = Values(p).PickOne();
                     */
                    bool isWriteConfined = PointsToAndEffectsAnnotations.IsWriteConfinedParameter(m,p);

                    // IParameterValueNode pv = this.attachValueforParameter(m.ThisParameter, pAddr, methodLabel, fromNonAnalizable,isWriteConfined);
                    IPTAnalysisNode pv = this.attachValueforParameter(p, pAddr, methodLabel, fromNonAnalizable, isWriteConfined);
                    parameterOldValue[p] = pv;
                    
                }
            }
            if (this.vRet != null )
            {
              // DIEGO-COMMENT: We need allways to assign a fresh object at the beginning. 
              // If the method doesn't return a fresh object has to assign it to vRet
              // This is because after binding load nodes not reacheable from outside 
              // are descarted and we may loose and effect over a reachable object. 
              // I'd like to change this to create another kind of "load node" to handle this case
                this.Assign(this.vRet, new RNode(vRet.Type, m, methodLabel), methodLabel);
                //if (PointsToAndEffectsAnnotations.IsDeclaredFresh(m))
                //{
                //    this.Assign(this.vRet, new RNode(vRet.Type,m,methodLabel), methodLabel);
                //}
                //else
                //{
                //    this.Assign(this.vRet, new LValueNode(lb, m.ReturnType), methodLabel);
                //}
            }
        }

        // Create a value Node for a parameter and link it with the parameter's addrnode
        private IPTAnalysisNode attachValueforParameter(Parameter p, IParameterNode pn, 
            Label lb, bool fromNonAnalizable, bool confined)
        {
            
            IPTAnalysisNode val = null;
            if (!CheckPrimitive(p.Type))
            {
                // IParameterValueNode vn;
                
                if ((pn.IsByRef || pn.IsOut) && (p.Type  is Reference))
                    {
                    IPTAnalysisNode vn;
                    // IAddrNode an = new AddrNode(lb, p.Type);
                    

                    Reference refType = (Reference)p.Type;
                    TypeNode concreteType = refType.ElementType;
                    // IAddrNode an = new PLNode(lb, p.Type,pn);
                    IAddrNode an = new PLNode(lb, concreteType, pn);
                    SetLoadField(pn, PTGraph.asterisk, an);
                    
                    if (fromNonAnalizable && PointsToAndEffectsAnnotations.IsDeclaredFresh(p))
                    {
                        Label lbParam = new Label(lb.GetHashCode() + p.GetHashCode(), lb.Method);
                        InsideNode in1 = new InsideNode(lbParam, ((Reference)p.Type).ElementType);
                        vn = in1;
                    }
                    else
                    {
                        IParameterValueNode vn2 = new PLNode(lb, ((Reference)p.Type).ElementType, an);
                        if (fromNonAnalizable) 
                        {
                            vn2.SetOmegaNode();
                            if (confined) vn2.SetOmegaConfined();
                        }
                        vn = vn2;
                    }
                    SetLoadField(an, PTGraph.asterisk, vn);
                    val = an; 
                    //val = vn; 
                }
                else
                {
                    IParameterValueNode vn = new PLNode(lb, p.Type, pn);
                    SetLoadField(pn, PTGraph.asterisk, vn);
                    if (fromNonAnalizable)
                    {
                        vn.SetOmegaNode();
                        if (confined) vn.SetOmegaConfined();
                    }
                    val = vn;
                }
                addLeaftoParameterLeavesMap(p, val);
            }
            
            return val;
        }
        void addLeaftoParameterLeavesMap(Parameter p, IPTAnalysisNode n)
        {
            leaves[p].Add(n);
        }

        private IParameterNode initParameter(Parameter p, int i)
        {
            PNode pPar;
            if (p.IsOut)
                pPar = new POutNode(m, i + 1);
            else
                if (p.Type is Reference && !(p.Type is ArrayType))
                    pPar = new PRefNode(m, i + 1);
                else
                    pPar = new PByValueNode(m, i + 1);
            PT_Variable parameter = new PT_Variable(p);
            LV[parameter] = Nodes.Singleton(pPar);
            //LV[p] = Nodes.Singleton(pPar);
            parameterMap[p] = pPar;
            addrParameterNodes.Add(pPar);
            return pPar;
        }

        /// <summary>
        /// Copy Constructor
        /// </summary>
        /// <param name="ptg"></param>
        public PTGraph(PTGraph ptg)
        {
            this.iEdges = new Edges(ptg.iEdges);
            this.oEdges = new Edges(ptg.oEdges);
            this.escapingNodes = new Nodes(ptg.escapingNodes);
            this.locVar = new LocVar(ptg.locVar);
            this.addrParameterNodes = new Set<PNode>(ptg.addrParameterNodes);
            this.parameterOldValue = ptg.parameterOldValue;
            this.m = ptg.m;
            this.vRet = ptg.vRet;
            this.thisParameterNode = ptg.thisParameterNode;
            this.parameterMap = ptg.parameterMap;
            this.methodLabel = ptg.methodLabel;
            this.cachedVariables = ptg.cachedVariables;
            this.leaves = ptg.leaves;

        }
      public PTGraph(PTGraph ptg, PTGraph enclosing) {
        this.iEdges = new Edges(ptg.iEdges);
        this.iEdges.AddRange(enclosing.iEdges);

        this.oEdges = new Edges(ptg.oEdges);
        this.oEdges.AddRange(enclosing.oEdges);

        this.escapingNodes = new Nodes(ptg.escapingNodes);

        this.locVar = new LocVar(ptg.locVar);
        foreach (PT_Variable v in enclosing.locVar.Keys) {
          this.locVar[v] = enclosing.locVar[v];
        }
        
        this.addrParameterNodes = new Set<PNode>(ptg.addrParameterNodes);
        this.addrParameterNodes.AddRange(enclosing.addrParameterNodes);
        

        this.parameterOldValue = ptg.parameterOldValue;
        foreach (Parameter p in enclosing.parameterOldValue.Keys)
          this.parameterOldValue[p] = enclosing.parameterOldValue[p];

        this.m = ptg.m;
        this.vRet = ptg.vRet;
        this.thisParameterNode = ptg.thisParameterNode;
        
        
        this.parameterMap = ptg.parameterMap;
        foreach (Parameter p in enclosing.parameterMap.Keys)
          this.parameterMap[p] = enclosing.parameterMap[p];



        this.methodLabel = ptg.methodLabel;
        
        this.cachedVariables = ptg.cachedVariables;
        this.leaves = ptg.leaves;
        foreach (Parameter p in enclosing.leaves.Keys) {
          this.leaves[p] = enclosing.leaves[p];
        }

      }
        #endregion

        #region Lattice Operations (Join, AtMost, IsBottom )
        /// <summary>
        ///  Inclusion test
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public bool AtMost(PTGraph a, PTGraph b)
        {
            // LV is not more considered part of the lattice. It is just a way of getting the addr of a variable
            // It should be fixed unless the variable is temporal
            return a.iEdges.IsSubset(b.iEdges) && a.oEdges.IsSubset(b.oEdges) 
                // && b.escapingNodes.Includes(a.escapingNodes)
                ;// && LocVar.AtMost(a.locVar, b.locVar);
        }

        public override bool IsBottom
        {
            get { return iEdges.Count == 0 && oEdges.Count == 0 && locVar.Count == 0 && escapingNodes.Count == 0; }
        }

        public override bool IsTop
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        /// <summary>
        /// Join to PointsTo Graphs
        /// </summary>
        /// <param name="ptG2"></param>
        public void Join(PTGraph ptG2)
        {
            if (ptG2 != this)
            {
                if (iEdges != ptG2.iEdges)
                    iEdges.AddEdges(ptG2.iEdges);
                if (oEdges != ptG2.oEdges)
                    oEdges.AddEdges(ptG2.oEdges);
                if (escapingNodes != ptG2.escapingNodes)
                    escapingNodes.AddRange(ptG2.escapingNodes);
                if (locVar != ptG2.locVar)
                {
                    // locVar.Join(ptG2.locVar);
                    JoinLocVar(ptG2.locVar);
                }
                if (cachedVariables != ptG2.cachedVariables)
                {
                    foreach(string vn in ptG2.cachedVariables.Keys)
                    {
                        cachedVariables[vn] = ptG2.cachedVariables[vn];
                    }
                }
            }
        }
        public void JoinLocVar(LocVar lv2)
        {
            foreach (PT_Variable v in lv2.Keys)
            {
                Nodes locs2 = lv2[v];
                Nodes locs1 = Nodes.Empty;
                if (this.LV.ContainsKey(v))
                {
                    Set<IPTAnalysisNode> dif = locs2.Difference(this.LV[v]);
                    // If a variable of type struct has 2 different addr (it happens with stack_ vars)
                    // I convert to one struct
                    if (dif.Count > 0)
                    {
                        // System.Diagnostics.Debugger.Break();
                        foreach (IPTAnalysisNode d in dif)
                        {
                            if (d.IsStruct)
                            {
                                foreach (Edge e in I.EdgesFromSrc(d))
                                {
                                    foreach (IPTAnalysisNode t in this.LV[v])
                                    {
                                        if (t.IsStruct)
                                        {
                                            // System.Diagnostics.Debugger.Break();
                                            AssignStructField(t, e.Field, e.Dst);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    locs1.AddRange(this.LV[v]);
                }
                //if(locs1.Count==0)
                //    locs1.AddRange(locs2);
                //this.LV[v] = locs1;
                if(locs2.Count!=0)
                    this.LV[v] = locs2;
                if (PointsToAnalysis.debug)
                {
                    if (locs1.Count > 1)
                        System.Diagnostics.Debugger.Break();
                }
            }
        }
        #endregion

        /// <summary>
        /// f(methodHeader)
        /// </summary>
        /// <param name="method"></param>
        /// <param name="parameters"></param>
        /// <param name="lb"></param>
        public void InitMethod(Method method, System.Collections.IEnumerable parameters, Label lb)
        {
            methodLabel = lb;

        }

        #region Graph Search Operations (Forward/Backward Reacheability, Path Computation)
        public Nodes ReachableFromParametersReturnAndGlobals()
        {
            Nodes args = Nodes.Empty;
            
            return ReachableFromParametersReturnAndGlobalsAnd(args);
        }
        public Nodes ReachableFromParametersReturnAndGlobalsAnd(Nodes args)
        {
            Nodes argsExt = new Nodes(args);
            foreach (IPTAnalysisNode pn in AddrPNodes)
                argsExt.Add(pn);
            return ReachableFromReturnAndGlobalsAnd(argsExt);
        }
        public Nodes ReachableFromReturnAndGlobalsAnd(Nodes args)
        {
            Nodes Esc = new Nodes(E);
            Esc.AddRange(GetLocations(GlobalScope));
            if (RetValue != null)
            {
                Esc.AddRange(GetLocations(RetValue));
            }
            Esc.AddRange(args);
            Nodes B = NodesForwardReachableFromNodes(Esc);
            return B;
        }
        private Nodes ExternalNodes()
        {
            Nodes res = Nodes.Empty;
            foreach (IPTAnalysisNode pn in AddrPNodes)
            {
                res.AddRange(Values(pn));
                //res.AddRange(pn);
            }
            // res.AddRange(GetLocations(GlobalScope));
            res.AddRange(Values(GlobalScope));
            if (RetValue != null)
            {
                res.AddRange(Values(GetLocations(RetValue)));
            }
            return res;
        }


        /// <summary>
        /// Compute the set of backward reachable nodes from n(forward or backward) using I and O edges
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public Nodes NodesBackwardReachableFrom(IPTAnalysisNode n)
        {
            return NodesReachableFrom(n, false, true, true);
        }

        /// <summary>
        /// Compute the set of forward reachable nodes from n(forward or backward) using I and O edges
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public Nodes NodesForwardReachableFrom(IPTAnalysisNode n)
        {
            return NodesReachableFrom(n, true, true, true);
        }

        // Compute the set of reachable nodes from every element of set ns
        public Nodes NodesForwardReachableFromNodes(Nodes ns)
        {
            return NodesReachableFromNodes(ns, true, true, true);
        }
        // Compute the set of reachable nodes from a variable 
        public Nodes NodesForwardReachableFromVariable(Variable v)
        {
            return NodesReachableFrom(GetLocations(v), true, true, true);
        }

      /// <summary>
      /// Used to get only the nodes reachable using owned fields
      /// The problem is that it is not working because of the "allField" 
      /// special field.
      /// </summary>
      /// <param name="v"></param>
      /// <param name="onlyOwned"></param>
      /// <returns></returns>
      public Nodes NodesForwardReachableFromVariable(Variable v, bool onlyOwned) {
          if (!onlyOwned)
            return NodesForwardReachableFromVariable(v);
        return NodesReachableFromNodesWithOwnership(GetLocations(v), onlyOwned);
      }
        public Nodes NodesReachableFromNodesWithOwnership(Nodes locs, bool onlyOwned)
      {
          Nodes res = Nodes.Empty;
          List<IPTAnalysisNode> qeue = new List<IPTAnalysisNode>();
          Nodes visited = Nodes.Empty;
          // Nodes locs = GetLocations(v);
          qeue.AddRange(locs);
          visited.AddRange(locs);

          while (qeue.Count != 0) {
            IPTAnalysisNode e = qeue[0];
            qeue.Remove(e);
            visited.Add(e);
            res.Add(e);
            Nodes adjacents = Nodes.Empty;
            Set<Edge> adjacentWithEdges = new Set<Edge>();
            adjacentWithEdges.AddRange(I.EdgesFromSrc(e));
            adjacentWithEdges.AddRange(O.EdgesFromSrc(e));
            foreach(Edge adEdge in adjacentWithEdges)
            {
              if (adEdge.Field.IsOwned || adEdge.Field.Equals(asterisk) 
                || adEdge.Field.Equals(allFields) || adEdge.Field.Equals(arrayField))
                    adjacents.Add(adEdge.Dst);
            }
            foreach (IPTAnalysisNode ai in adjacents) {
                if (!visited.Contains(ai))
                {
                    visited.Add(ai);
                    res.Add(ai);
                    qeue.Add(ai);
                }
            }
          }
          return res;

          
        }

        /// <summary>
        /// Compute the set of forward reachable nodes from n(forward or backward) using O edges
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public Nodes NodesForwardReachableFromOnlyOEdges(IPTAnalysisNode n)
        {
            return NodesReachableFrom(n, true, false, true);
        }
        public Nodes NodesForwardReachableFromOnlyOEdges(Nodes ns)
        {
            return NodesReachableFromNodes(ns, true, false, true);
        }

        private Nodes NodesReachableFrom(IPTAnalysisNode n, bool forward)
        {
            return NodesReachableFrom(n, forward, true, true);
        }

        private Nodes NodesReachableFrom(IPTAnalysisNode n, bool forward, bool useIEdges, bool useOEdges)
        {
            Nodes ns = new Nodes();
            ns.Add(n);
            return NodesReachableFrom(ns, forward, useIEdges, useOEdges);
        }
        /// <summary>
        /// Compute the set of reachable nodes using a set of starting nodes
        /// </summary>
        /// <param name="n"></param>
        /// <param name="forward"></param>
        /// <param name="useIEdges"></param>
        /// <param name="useOEdges"></param>
        /// <returns></returns>
        private Nodes NodesReachableFrom(Nodes ns, bool forward, bool useIEdges, bool useOEdges)
        {
            Nodes res = Nodes.Empty;
            List<IPTAnalysisNode> qeue = new List<IPTAnalysisNode>();
            Nodes visited = Nodes.Empty;
            qeue.AddRange(ns);
            visited.AddRange(ns);
            bool debugOn = false;
            while (qeue.Count != 0)
            {
                if (qeue.Count > 3000 & !debugOn)
                {
                    debugOn = true;
                    System.Diagnostics.Debugger.Break();
                }
                IPTAnalysisNode e = qeue[0];
                visited.Add(e);
                res.Add(e);
                qeue.RemoveAt(0);
                
                Nodes adjacents = Nodes.Empty;
                if (useIEdges)
                    adjacents.AddRange(I.Adjacents(e, forward));
                if (useOEdges)
                    adjacents.AddRange(O.Adjacents(e, forward));
                
                if (adjacents.Count > 100)
                {
                 //   System.Diagnostics.Debugger.Break();
                }
                foreach (IPTAnalysisNode ai in adjacents)
                {
                    if (!visited.Contains(ai) /*&& !qeue.Contains(ai)*/)
                    {
                        visited.Add(ai);
                        //res.Add(ai);
                        qeue.Add(ai);
                    }
                    else { }
                }
            }
            return res;
        }
        public bool IsReachableFrom(IPTAnalysisNode who, Nodes ns, bool forward, bool useIEdges, bool useOEdges)
        {
            var x = new Nodes();
            x.Add(who);
            return IsReachableFrom(x, ns, forward, useIEdges, useOEdges);
        }

        public bool IsReachableFrom(Set<IPTAnalysisNode> who, Nodes ns, bool forward, bool useIEdges, bool useOEdges)
        {
            if (!who.Intersection(ns).IsEmpty)
                return true;
            bool res = false;
            
            List<IPTAnalysisNode> qeue = new List<IPTAnalysisNode>();
            Nodes visited = Nodes.Empty;
            
            qeue.AddRange(ns);
            visited.AddRange(ns);
            
            // bool debugOn = false;
            
            while (qeue.Count != 0)
            {
                
                IPTAnalysisNode e = qeue[0];
                visited.Add(e);
                
                qeue.RemoveAt(0);

                Nodes adjacents = Nodes.Empty;
                if (useIEdges)
                    adjacents.AddRange(I.Adjacents(e, forward));
                if (useOEdges)
                    adjacents.AddRange(O.Adjacents(e, forward));

                if (!who.Intersection(adjacents).IsEmpty)
                    return true;

                if (adjacents.Count > 100)
                {
                    //   System.Diagnostics.Debugger.Break();
                }
                foreach (IPTAnalysisNode ai in adjacents)
                {
                    if (!visited.Contains(ai) /*&& !qeue.Contains(ai)*/)
                    {
                        visited.Add(ai);
                        qeue.Add(ai);
                    }
                    else { }
                }
            }
            return res;
        }

        // Compute the set of reachable nodes from every element of set ns
        private Nodes NodesReachableFromNodes(Nodes ns, bool forward, bool useIEdges, bool useOEdges)
        {
            Nodes res = Nodes.Empty;
            foreach (IPTAnalysisNode n in ns)
            {
                Nodes reachables = NodesReachableFrom(n, forward, useIEdges, useOEdges);
                res.AddRange(reachables);
            }
            return res;
        }

        /// <summary>
        /// Compute the set of maximal paths from n
        /// </summary>
        /// <param name="n"></param>
        /// <param name="forward"></param>
        /// <param name="useIEdges"></param>
        /// <param name="useOEdges"></param>
        /// <returns></returns>
        public Set<List<Edge>> DFSPathFrom(IPTAnalysisNode n, bool forward, bool useIEdges, bool useOEdges)
        {
            Set<List<Edge>> res = new Set<List<Edge>>();
            Nodes visited = Nodes.Empty;
            List<Edge> currentPath = new List<Edge>();
            DFSPathFrom(n, null, forward, useIEdges, useOEdges, visited, res, currentPath);
            // Console.Out.WriteLine("Paths");
            //foreach (List<Edge> path in res)
            //{
            //    string pathString = n.ToString();
            //    foreach (Edge e in path)
            //    {
            //        if(!e.Field.Equals(PTGraph.asterisk))
            //        {
            //            pathString = pathString  + "." + e.Field.Name.Name;
            //        }
            //        else
            //        {
            //            pathString = "*(" + pathString + ")";
            //        }
            //    }
            //    Console.Out.WriteLine(pathString);
            //}
            return res;
        }

        /// <summary>
        /// The actual computation of the path
        /// </summary>
        /// <param name="n"></param>
        /// <param name="f"></param>
        /// <param name="forward"></param>
        /// <param name="useIEdges"></param>
        /// <param name="useOEdges"></param>
        /// <param name="visited"></param>
        /// <param name="res"></param>
        /// <param name="current"></param>
        public void DFSPathFrom(IPTAnalysisNode nr, Field f, bool forward, bool useIEdges, bool useOEdges,
            Nodes visited, Set<List<Edge>> res, List<Edge> current)
        {
            visited.Add(nr);

            Nodes a = Nodes.Singleton(nr);

            foreach (IPTAnalysisNode n in a)
            {
                Set<AField> adjacents = new Set<AField>();
                if (f == null)
                {
                    if (useIEdges)
                        adjacents.AddRange(I.AdjancentsWithField(n, forward));
                    if (useOEdges)
                        adjacents.AddRange(O.AdjancentsWithField(n, forward));
                }
                else
                {
                    if (useIEdges)
                        adjacents.AddRange(I.AdjancentsWithField(n, f, forward));
                    if (useOEdges)
                        adjacents.AddRange(O.AdjancentsWithField(n, f, forward));
                }
                if (adjacents.Count != 0) // && !n.IsParameterNode)
                {
                    foreach (AField ai in adjacents)
                    {
                        List<Edge> newCurrent = new List<Edge>();
                        newCurrent.AddRange(current);
                        Edge e = null;
                        if (forward)
                            e= new Edge(n, ai.Field, ai.Src);
                        else
                            e=new Edge(ai.Src, ai.Field, n);
                          newCurrent.Add(e);
                        if (!visited.Contains(ai.Src)){
                        //  if (!current.Contains(e)) {
                            
                            DFSPathFrom(ai.Src, null, forward, useIEdges, useOEdges, visited, res, newCurrent);
                          }
                          else {
                            if(newCurrent[newCurrent.Count-1].Src.IsVariableReference)
                              res.Add(newCurrent);
                          }
                    }
                }
                else
                {
                    res.Add(current);
                    return;
                }
            }
        }

      public Edges EdgesReachableFrom(IPTAnalysisNode n, Field f, bool forward, bool useIEdges, bool useOEdges) {
        Edges res = new Edges();
        Nodes visited = Nodes.Empty;
        List<IPTAnalysisNode> qeue = new List<IPTAnalysisNode>();
        qeue.Add(n);
        visited.Add(n);
        while (qeue.Count > 0) {
          IPTAnalysisNode n2 = qeue[0];
          qeue.RemoveAt(0);
          // visited.Add(n2);
          Set<AField> adjacents = new Set<AField>();
          if (f == null) {
            if (useIEdges)
              adjacents.AddRange(I.AdjancentsWithField(n2, forward));
            if (useOEdges)
              adjacents.AddRange(O.AdjancentsWithField(n2, forward));
          }
          else {
            if (useIEdges)
              adjacents.AddRange(I.AdjancentsWithField(n2, f, forward));
            if (useOEdges)
              adjacents.AddRange(O.AdjancentsWithField(n2, f, forward));
          }
          if (adjacents.Count != 0) // && !n.IsParameterNode)
          {
            foreach (AField ai in adjacents) {
              Edge e = null;
              if (forward)
                e = new Edge(n2, ai.Field, ai.Src);
              else
                e = new Edge(ai.Src, ai.Field, n2);
              res.AddEdge(e);
              if (!visited.Contains(ai.Src))
              {
                  visited.Add(ai.Src);
                  qeue.Add(ai.Src);
              }
            }
          }

        }
        
        return res;
      }
      public IEnumerable<IEnumerable<Edge>> DFSPathFromTo(IPTAnalysisNode from, IPTAnalysisNode to, Edges tree) {
        
        
        Nodes visited = Nodes.Empty;
        List<Edge> currentPath = new List<Edge>();
        Set<IEnumerable<Edge>> res =  new Set<IEnumerable<Edge>>();
        DFSPaths(from,to, tree, currentPath, res, visited);
         /*
        Console.Out.WriteLine("Paths");
        foreach (IEnumerable<Edge> path in res) {
          string pathString = from.ToString();
          foreach (Edge e in path) {
            if (!e.Field.Equals(PTGraph.asterisk)) {
              pathString = pathString + "." + e.Field.Name.Name;
            }
            else {
              pathString = "*(" + pathString + ")";
            }
          }
          Console.Out.WriteLine(pathString);
        }
         */
        return res;
      }
        public void DFSPaths(IPTAnalysisNode from, IPTAnalysisNode to, Edges tree, List<Edge> current, Set<IEnumerable<Edge>> res, Nodes visited)
        {
            visited.Add(from);
            if (current.Count > 100)
            {
            }
            
        Set<AField> adjacents = new Set<AField>();
        adjacents.AddRange(tree.AdjancentsWithField(from, true));
        if (adjacents.Count != 0) {
          foreach (AField ai in adjacents) {
            IPTAnalysisNode newFrom = ai.Src;
            List<Edge> newCurrent = new List<Edge>();
            newCurrent.AddRange(current);

            Edge e = null;
            Field f = ai.Field;
            e = new Edge(from, f, newFrom);
            newCurrent.Add(e);
            // If from is an OmegaNode (non omega confined by the moment)
            // we can reduce the search
            if(from.IsOmega) {
              Edge e1 = null;
              if ( /*case 1 */ 
                  (!from.IsOmegaConfined && (f.Equals(PTGraph.allFields) || f.Equals(PTGraph.allFieldsNotOwned))) ||
                  /*case 2 */ 
                  (from.IsOmegaConfined && (f.Equals(PTGraph.allFieldsNotOwned)))) {
                    e1 = new Edge(newFrom, f, to); // e = new Edge(from, f, to);
                    newCurrent.Add(e1);
                    newFrom = to;
                }
            }
             
            if (newFrom.Equals(to))
            {
                res.Add(newCurrent);
            }
            else
            {

                if (!visited.Contains(newFrom))
                {
                    DFSPaths(newFrom, to, tree, newCurrent, res, visited);
                }
                else
                {
                    bool haveToAdd = false;
                    foreach (IEnumerable<Edge> p in res)
                    {
                        List<Edge> path = (List<Edge>)p;
                        int pos = 0;
                        while (pos < path.Count && !path[pos].Src.Equals(newFrom))
                            pos++;
                        if (pos < path.Count)
                        {
                            for (int i = pos; i < path.Count; i++)
                                newCurrent.Add(path[i]);
                            haveToAdd = true;
                        }
                        if (haveToAdd)
                        {
                            res.Add(newCurrent);
                            break;
                        }
                    }
                    
                }
            }
          }
        }
        else {
            return;
        }
      }
      



        


        #endregion

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private bool CheckPrimitive(TypeNode t)
        {
            return t.IsPrimitive;
        }

        /// <summary>
        /// Add v1 variable to the mapping LV. If v1 is a reference type, a node representing the reference 
        /// is added. If it is a struct type, the node directly represent the value
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="lb"></param>
        /// <returns></returns>
        public IPTAnalysisNode AddVariable(Variable v1, Label lb)
        {
            PT_Variable ptv_v1 = new PT_Variable(v1);
            IPTAnalysisNode rv = null;
            //if (v1.Type is Struct && !v1.Type.IsPrimitive)
            if (IsStructType(v1.Type))
                rv = new StructNode(lb, v1.Type);
            else rv = AddRefVariable(ptv_v1, MethodLabel);
                //rv = new VariableAddrNode(MethodLabel, v1);

            LV[ptv_v1] = Nodes.Singleton(rv);
            //LV[v1] = Nodes.Singleton(rv);
            return rv;
        }
        public IPTAnalysisNode AddRefVariable(PT_Variable v1, Label lb)
        {
            IPTAnalysisNode rv = null;
            rv = new VariableAddrNode(lb, v1);
            return rv;
        }


        public Variable GetCachedVariable(String cacheVar, TypeNode t)
        {
            Variable v1 = null;
            if (cachedVariables.ContainsKey(cacheVar))
            {
                v1 = cachedVariables[cacheVar];
            }
            else
            {
                v1 = new Variable(t.NodeType);
                v1.Name = new Identifier(cacheVar);
                cachedVariables[cacheVar] = v1;
            }
            return v1;
        }


        /// <summary>
        /// Add a variable and its value to the PTG. 
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="n"></param>
        /// <param name="lb"></param>
        /*
        public void AddNode(Variable v1, PTAnalysisNode n, Label lb)
        {
            if (!LV.ContainsKey(v1))
            {
                AddVariable(v1, lb);
            }
            PTAnalysisNode addr = GetAddress(v1);
            if (!addr.IsStruct)
            {
                if (!isStructRef(addr.Type))
                    I.AddIEdge(addr, PTGraph.asterisk, n);
                else
                {
                    Reference refType = (Reference)addr.Type;
                    StructNode sn = new StructNode(lb, refType.ElementType);
                    I.AddSEdge(addr, PTGraph.asterisk, sn);
                }
            }
        }
        */

        public void VerifyAndSetLocationForAssigment(Variable v1, Label lb)
        {
            if(v1!=null)
                if (!(v1 is Parameter) || !LV.ContainsKey(new PT_Variable(v1)))
                    AddVariable(v1, lb);
        }


        /// <summary>
        /// Assign a potencial values of v2 to v1
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="lb"></param>
        public void Assign(Variable v1, Variable v2, Label lb)
        {
            // TAKE CARE
            VerifyAndSetLocationForAssigment(v1, lb);
            // PTAnalysisNode addrV2 = GetAddress(v2);
            // Nodes values = Values(addrV2);
            Nodes values = Values(GetLocations(v2));
            // If v2 has no values, create a value representing (*v2)
            CheckValues(values, lb, v2);
            Assign(GetLocations(v1), values, lb);
        }
        /// <summary>
        /// Check values (nodes pointed by the node(s) that v references) is not empty. If it's empty create a 
        /// "Value node" representing a read (*loc(v)) 
        /// </summary>
        /// <param name="values"></param>
        /// <param name="lb"></param>
        /// <param name="v"></param>
        public void CheckValues(Nodes values, Label lb, Variable v)
        {
            if (values.Count == 0 && !CheckPrimitive(v.Type))
            {
                foreach (IPTAnalysisNode addr in GetLocations(v))
                {
                    IPTAnalysisNode vn = NewValueNode(lb, addr, v.Type);
                    // PTAnalysisNode vn = NewLoadOrStructNode(lb, t);
                    SetLoadField(addr, PTGraph.asterisk, vn);
                    // AssignValue(GetAddress(v), vn);
                    values.Add(vn);
                }
            }
        }
        /// <summary>
        /// Check values (nodes pointed by addr) is not empty. If it's empty create a 
        /// "Value node" representing a read (*addr) 
        /// </summary>
        /// <param name="values"></param>
        /// <param name="lb"></param>
        /// <param name="addr"></param>
        public void CheckValues(Nodes values, Label lb, IPTAnalysisNode addr)
        {
            if (values.Count == 0 && !CheckPrimitive(addr.Type))
            {
                IPTAnalysisNode vn = NewValueNode(lb, addr, addr.Type);
                SetLoadField(addr, PTGraph.asterisk, vn);
                // AssignValue(GetAddress(v), vn);
                values.Add(vn);
            }
        }
        /// <summary>
        /// Compute Check Values to a set of references. That is, check if every reference points to a value.
        /// If not, create a Value node to represent a value
        /// </summary>
        /// <param name="values"></param>
        /// <param name="lb"></param>
        /// <param name="addrs"></param>
        public void CheckValues(Nodes values, Label lb, Nodes addrs)
        {
            foreach (IPTAnalysisNode addr in addrs)
                CheckValues(values, lb, addr);
        }
        /// <summary>
        /// Assign a value to a variable (that means, if it's a reference a value to the reference node
        /// or directly to the variable if it's a struct)
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="n"></param>
        /// <param name="lb"></param>
        public void Assign(Variable v1, IPTAnalysisNode n, Label lb)
        {
            // TAKE CARE
            VerifyAndSetLocationForAssigment(v1, lb);

            Assign(GetLocations(v1), Nodes.Singleton(n), lb);
        }
        /// <summary>
        /// Idem previous but for a set of values 
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="ns"></param>
        /// <param name="lb"></param>
        public void Assign(Variable v1, Nodes ns, Label lb)
        {
            // TAKE CARE
            VerifyAndSetLocationForAssigment(v1, lb);

            Assign(GetLocations(v1), ns, lb);
        }
        
        #region Assignments of nodes to values
        public void Assign(Nodes n1s, Nodes n2s, Label lb)
        {
            Assign(n1s, n2s, lb, true);
        }
        
        public void Assign(Nodes n1s, Nodes n2s, Label lb, bool strong)
        {
            foreach (IPTAnalysisNode n1 in n1s)
                Assign(n1, n2s, lb, strong);
        }

        public void Assign(IPTAnalysisNode n1, Nodes n2s, Label lb)
        {
            Assign(n1, n2s, lb, true);
        }
        public void Assign(IPTAnalysisNode n1, Nodes n2s, Label lb, bool strong)
        {
            if (strong)
                RemoveValues(n1);
            foreach (IPTAnalysisNode n2 in n2s)
            {
                Assign(n1, n2, lb, false);
            }
        }

        public void Assign(IPTAnalysisNode n1, IPTAnalysisNode n2, Label lb)
        {
            Assign(n1, n2, lb, true);
        }
        
        public void Assign(IPTAnalysisNode n1, IPTAnalysisNode n2, Label lb, bool strong)
        {
            if (n1.IsStruct)
            {
                //StructNode sn = (StructNode)n1;
                RemoveValues(n1);
                CopyStruct(n1, n2,lb);
            }
            else
            {
                if (strong)
                    RemoveValues(n1);
                AssignValue(n1, n2);
            }
        }

        public void AssignValues(Nodes ns1, Nodes ns2, Label lb)
        {
            foreach (IPTAnalysisNode n in ns1)
            {
                AssignValues(n, ns2);
            }
        }
        public void AssignValues(IPTAnalysisNode n1, Nodes ns2)
        {
            foreach (IPTAnalysisNode n2 in ns2)
                I.AddEdge(n1, PTGraph.asterisk, n2);
        }
        public void AssignValue(IPTAnalysisNode n1, IPTAnalysisNode n2)
        {
            //if (n2.IsStruct)
            //    I.AddSEdge(n1, PTGraph.asterisk, n2);
            //else
                I.AddIEdge(n1, PTGraph.asterisk, n2);
        }
        #endregion 
        public Nodes GetValuesIfEmptyLoadNode(Variable v, Label lb)
        {
            Nodes values = Values(v);
            CheckValues(values, lb, v);
            return values;
        }
        public Nodes Values(Variable v)
        {
            return Values(GetLocations(v));
        }
        public Nodes Values(Variable v, Field f)
        {
            Nodes vValues = Values(v);
            Nodes fAddrs = Nodes.Empty;
            foreach (IPTAnalysisNode loc in vValues)
            {
                if (!loc.IsNull)
                    fAddrs.AddRange(GetFieldAddress(loc, f));
            }
            return Values(fAddrs);
        }

        /// <summary>
        /// Get the set of values pointed by the node
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public Nodes Values(IPTAnalysisNode n)
        {
            Nodes values = Nodes.Empty;
            Nodes valuesI = I.Adjacents(n, PTGraph.asterisk, true);
            // There is a problem con the LoadAddress Visitor
            // Sometimes when v1 = &v2 and v2 is Struct it decides that v1 is struct and this is not true
            if (n.IsStruct && valuesI.Count==0)
            {
                values.Add(n);
            }
            else
            {
                values.AddRange(valuesI);
                values.AddRange(O.Adjacents(n, PTGraph.asterisk, true));
            }
            return values;
        }
        


        /// <summary>
        /// Get the union of set of values pointed by the nodes
        /// </summary>
        /// <param name="ns"></param>
        /// <returns></returns>
        public Nodes Values(Set<PNode> ns)
        {
            Nodes res = Nodes.Empty;
            foreach (IPTAnalysisNode n in ns)
            {
                res.AddRange(Values(n));
            }
            return res;
        }

        public Nodes Values(Nodes ns)
        {
            Nodes res = Nodes.Empty;
            foreach (IPTAnalysisNode n in ns)
            {
                res.AddRange(Values(n));
            }
            return res;
        }

      public Set<Node> GetReferences(Set<IPTAnalysisNode> ns) {
        Set<Node> res = new Set<Node>();
        Set<Edge> edges = Adyacents(ns, false, true, true);
        foreach (Edge e in edges) {
          if (e.Src.IsVariableReference) {
            IVarRefNode vn = (IVarRefNode)e.Src;
            res.Add(vn.ReferencedVariable);
          }
          else {
            Set<Edge> edges2 = Adyacents(e.Src, false, true, true);
            foreach (Edge e2 in edges2) {
              if (!e2.Field.Equals(PTGraph.asterisk))
                res.Add(e2.Field);
            }
          }
        }
        return res;
      }
      public Set<Edge> Adyacents(Set<IPTAnalysisNode> ns, bool forward, bool useIEdges, bool useOEdges) {
        Set<Edge> res = new Set<Edge>();
        foreach (IPTAnalysisNode n in ns)
          res.AddRange(Adyacents(n, forward, useIEdges, useOEdges));
        return res;
      }

      public Set<Edge> Adyacents(IPTAnalysisNode n, bool forward, bool useIEdges, bool useOEdges) {
        Set<Edge> res = new Set<Edge>();
        if (useIEdges)
          if(forward) res.AddRange(I.EdgesFromSrc(n));
          else res.AddRange(I.EdgesFromDst(n));
        if (useOEdges)
          if (forward) res.AddRange(O.EdgesFromSrc(n));
          else res.AddRange(O.EdgesFromDst(n));
        return res;
      }


        /// <summary>
        /// A variable is a name for a location. It the type is a reference, 
        /// then the location containts a reference. 
        /// This method get that reference. 
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public Nodes GetLocations(Variable v)
        {
            Nodes res = Nodes.Empty;
            if (v != null)
            {
                PT_Variable ptv = new PT_Variable(v);
                if (!LV.ContainsKey(ptv))
                {
                    AddVariable(v, methodLabel);
                }
                res = LV[ptv];
            }
            return res;
        }
        public Nodes GetLocationsRef(Variable v)
        {
            Nodes res = Nodes.Empty;
            if (v != null)
            {
                PT_Variable ptv = new PT_Variable(v);
                if (!LV.ContainsKey(ptv))
                {
                    LV[ptv] = Nodes.Singleton(AddRefVariable(ptv, methodLabel));
                }
                res = LV[ptv];
            }
            return res;
        }

        public Nodes GetFieldAdress(Variable v, Field f, Label lb, out Nodes escapingFieldAddresses, TypeNode targetType)
        {
            Nodes fAddrs = Nodes.Empty;
            Nodes vValues = Values(GetLocations(v));
            CheckValues(vValues, lb, v);

            Nodes B = Nodes.Empty;
            Nodes escapingNodes2 = ReachableFromParametersReturnAndGlobals();
            Nodes escapingNodes = NodesForwardReachableFromOnlyOEdges(ExternalNodes());

            foreach (IPTAnalysisNode loc in vValues)
            {
                if (escapingNodes.Contains(loc) && !loc.IsNull)
                {
                    B.Add(loc);
                }
            }

            foreach (IPTAnalysisNode loc in vValues)
            {
                if(!loc.IsNull)
                    fAddrs.AddRange(GetFieldAddress(loc, f, lb));
            }
            // Add a load Edge for the potential mutation outside the method scope
            // First look if there is already one.
            escapingFieldAddresses = Nodes.Empty;
            foreach (IPTAnalysisNode loc in B)
            {
                
                Nodes escFaddrs = GetFieldAddress(loc, f, lb);
                escapingFieldAddresses.AddRange(escFaddrs);
            }
            if (escapingFieldAddresses.Count == 0 && B.Count>0)
            {
                IPTAnalysisNode addr = NewLoadAddressOrStructNode(lb, targetType.GetReferenceType());
                escapingFieldAddresses.Add(addr);
                SetLoadField(B, f, addr);
            }

            return fAddrs;

        }
        public Nodes GetFieldAddress(IPTAnalysisNode n, Field f)
        {
            return GetFieldAddress(n, f, MethodLabel);
        }
        public Nodes GetFieldAddressStore(IPTAnalysisNode n, Field f)
        {
            Nodes fa = Nodes.Empty;
            fa.AddRange(I.Adjacents(n, f, true));
            fa.AddRange(O.Adjacents(n, f, true));
            return fa;
        }

        public Nodes GetFieldAddressStore(IPTAnalysisNode n, Field f, Label lb)
        {
            Nodes fa = Nodes.Empty;
            fa.AddRange(I.Adjacents(n, f, true));
            fa.AddRange(O.Adjacents(n, f, true));
            /*
            fa.AddRange(I.Adjacents(n, allFields, true));
            fa.AddRange(O.Adjacents(n, allFields, true));
            fa.AddRange(I.Adjacents(n, allFieldsNotOwned, true));
            fa.AddRange(O.Adjacents(n, allFieldsNotOwned, true));
            */
            return fa;
        }

        public Nodes GetFieldAddress(IPTAnalysisNode n, Field f, Label lb)
        {
            Nodes fa = Nodes.Empty;
            fa.AddRange(I.Adjacents(n, f, true));
            fa.AddRange(O.Adjacents(n, f, true));

            fa.AddRange(I.Adjacents(n, allFields, true));
            fa.AddRange(O.Adjacents(n, allFields, true));
            fa.AddRange(I.Adjacents(n, allFieldsNotOwned, true));
            fa.AddRange(O.Adjacents(n, allFieldsNotOwned, true));

            return fa;
        }

        public void Store(Variable v1, Field f, Variable v2, Label lb)
        {
            if (CheckPrimitive(v2.Type))
                return;
            Nodes v1Values = Values(GetLocations(v1));
            CheckValues(v1Values, lb, v1);
            Nodes v2Values = Values(GetLocations(v2));
            CheckValues(v2Values, lb, v2);
               
            foreach (IPTAnalysisNode obj in v1Values)
            {
                 Store(obj, f, v2Values, lb);
            }
        }

        public void Store(IPTAnalysisNode n1, Field f, Nodes n2s, Label lb)
        {
            foreach (IPTAnalysisNode n2 in n2s)
                Store(n1, f, n2, lb);
        }

        public void Store(IPTAnalysisNode n1, Field f, IPTAnalysisNode n2, Label lb)
        {
            if (CheckPrimitive(n2.Type))
                return;

            
            // DIEGO-CHECK: I change this 
            // Nodes fAddrs = GetFieldAddress(n1, f, lb);
            // GetFieldAddressStore doesn't consired ? and $ field as wilcards.
            // I want store to operate over real 
            
            Nodes fAddrs = GetFieldAddressStore(n1, f, lb);
            if (fAddrs.Count == 0)
            {
                //IAddrNode addr = new AddrNode(lb, n1.Type.GetReferenceType());
                IAddrNode addr = new AddrNode(lb, n2.Type.GetReferenceType());
                fAddrs.Add(addr);
                AssignField(n1, f, addr);

                /*
                if (IsStructType(n1.Type))
                //if (isStruct(n2.Type)) (BUG????)
                {
                    AssignStructField(n1, f, n2);
                }
                else
                {
                    //PTAnalysisNode addr = new LAddrNode(lb, n1.Type.GetReferenceType());
                    IAddrNode addr = new AddrNode(lb, n1.Type.GetReferenceType());
                    fAddrs.Add(addr);
                    //SetLoadField(n1, f, addr);
                    AssignField(n1, f, addr);
                }
                 */ 
            }
            foreach (IPTAnalysisNode addr in fAddrs)
            {
                Assign(addr, n2, lb, false);
            }
        }

        private void AssignField(IPTAnalysisNode n1, Field f, IPTAnalysisNode n2)
        {
            //foreach (PTAnalysisNode loc in n1)
            //    I.AddIEdge(loc, f, n2);
            if (!n1.IsStruct)
                I.AddIEdge(n1, f, n2);
            else
                I.AddSEdge(n1, f, n2);
        }

        private void AssignStructField(IPTAnalysisNode n1, Field f, IPTAnalysisNode n2)
        {
            //foreach (PTAnalysisNode loc in n1)
            //    I.AddIEdge(loc, f, n2);
            I.AddSEdge(n1, f, n2);
        }

        public void StoreIndirect(Variable v1, Variable v2, Label lb)
        {
            if (CheckPrimitive(v2.Type))
                return;

            Nodes values1 = Values(GetLocations(v1));
            CheckValues(values1, lb, v1);
            Nodes values2 = Values(GetLocations(v2));
            CheckValues(values2, lb, v2);
            // For being conservative is better to use false
            // Diego 12/8/2007 This one was unsound: Assign(values1, values2, lb, true);
            // Diego 12/8/2007 I can do a strong update only if I don't load nodes.
            bool weakUpdate = hasLoadNode(Values(values1));
            Assign(values1, values2, lb, false);
        }
        // Todo: Check for load of a field 
        private bool hasLoadNode(Nodes vals)
        {
            bool res = false;
            foreach (IPTAnalysisNode n  in vals)
            {
                if (n.IsLoad)
                    return true;
            }
            return res;
        }
        public void LoadIndirect(Variable v1, Variable v2, Label lb)
        {
            if (CheckPrimitive(v1.Type))
                return;

            Nodes values2 = Values(GetLocations(v2));
            CheckValues(values2, lb, v2);

            Nodes valuesOfValues2 = Values(values2);
            if (valuesOfValues2.Count == 0)
            {
                // PTAnalysisNode ln = new LNode(lb, v1.Type);

                IPTAnalysisNode ln = NewLoadOrStructNode(lb, v1.Type);

                AssignLoadField(GetLocations(v2), PTGraph.asterisk, ln);

                valuesOfValues2.Add(ln);
            }
            Assign(GetLocations(v1), valuesOfValues2, lb);
        }

        public void LoadAddress(Variable v1, Variable v2, Label lb)
        {
            /*
            PTAnalysisNode nAddrV2 = GetAddress(v2);
            PTAnalysisNode nAddrV1 = GetAddress(v1);
            RemoveValues(nAddrV1);
            I.AddIEdge(nAddrV1, PTGraph.asterisk, nAddrV2);
            */
            // Console.Out.WriteLine("v1:{0} {2} v2:{1} {3}",v1.Type,v2.Type,v1.Type.IsReferenceType,v2.Type.IsReferenceType);
            // There is a problem con the LoadAddress Visitor
            // Sometimes when v1 = &v2 and v2 is Struct it decides that v1 is struct and this is not true
            RemoveValues(GetLocationsRef(v1));
            AssignValues(GetLocationsRef(v1), GetLocations(v2),lb);
        }
        internal void AssignDelegate(Variable v1, Variable thisVar, Variable delRef, Label lb)
        {
            // Replace this by
            // Store(v1,"this",thisVar)
            // Store(v1,",ethod",delRef)
            Nodes delegates = Values(delRef);
            if (delegates.Count == 1)
            {
                MethodNode mn2 = (MethodNode)delegates.PickAnElement();
                
                foreach (IPTAnalysisNode n in Values(v1))
                {
                    if (n.IsMethodDelegate)
                    {
                        MethodDelegateNode mn = (MethodDelegateNode)n;
                        if (thisVar != null)
                            mn.ThisValues = Values(thisVar);
                        else
                            mn.ThisValues = null;
                        
                        mn.Method = mn2.Method;
                    }
                }
            }
            else
            {
                ApplyAssignNull(v1);
            }
             
        }

        internal void LoadMethod(Variable v1, Method m, Label lb)
        {
            IPTAnalysisNode mNode = new MethodNode(lb, m.ReturnType, m);
            Assign(v1, mNode, lb);
        }

        internal void LoadInstanceMethod(Variable v1, Variable v2, Method m, Label lb)
        {
            //System.Diagnostics.Debugger.Break();
            IPTAnalysisNode mNode = new MethodNode(lb, m.ReturnType, m);
            Assign(v1, mNode, lb);
        }


        public void Load(Variable v1, Variable v2, Field f, Label lb)
        {
            if (CheckPrimitive(v1.Type))
            {
                ForgetVariable(v1);
                return;
            }

            Nodes A = GetLoadNodes(v1.Type, v2, f, lb);
            Assign(v1, A, lb);
        }

        /// <summary>
        /// </summary>
        /// <param name="v1Type"></param>
        /// <param name="v2"></param>
        /// <param name="f"></param>
        /// <param name="lb"></param>
        /// <returns></returns>
        protected Nodes GetLoadNodes(TypeNode v1Type, Variable v2, Field f, Label lb)
        {
            Nodes A = Nodes.Empty;
            Nodes escapeFieldAddresses;
            Nodes fAddr = GetFieldAdress(v2, f, lb, out escapeFieldAddresses,v1Type);
            
            // It should not happen.
            if (fAddr.Count == 0 && escapeFieldAddresses.Count == 0)
            {
                // PTAnalysisNode ln = new LNode(lb, v1Type);
                IPTAnalysisNode ln = NewLoadOrStructNode(lb, v1Type);

                IPTAnalysisNode addr = null;
  
                //if (v2.Type is Struct && && !v1.Type.IsPrimitive)
                if(IsStructType(v2.Type))
                {
                    addr = ln;
                }
                else
                {
                    addr = new LAddrNode(lb, v1Type.GetReferenceType());
                    O.AddOEdge(addr, PTGraph.asterisk, ln);
                }

                Nodes valuesV2 = Values(GetLocations(v2));
                CheckValues(valuesV2, lb, v2);
                // AssignLoadField(GetLocations(v2), f, addr);
                SetLoadField(valuesV2, f, addr);
                fAddr.Add(addr);
            }
            A = Values(fAddr);
            if (escapeFieldAddresses.Count != 0)
            {
                if (Values(escapeFieldAddresses).Count == 0)
                {
                    IPTAnalysisNode ln = NewLoadOrStructNode(lb, v1Type);
                    foreach (IPTAnalysisNode addr in escapeFieldAddresses)
                    {
                        O.AddOEdge(addr, PTGraph.asterisk, ln);
                    }
                    A.Add(ln);
                }
            }
            return A;
        }

        private void AssignLoadField(Nodes addrs, Field f, IPTAnalysisNode n2)
        {
            foreach (IPTAnalysisNode loc in Values(addrs))
            {
                SetLoadField(loc, f, n2);
            }
        }
        private void AssignLoadField(IPTAnalysisNode addrN1, Field f, IPTAnalysisNode n2)
        {
            AssignLoadField(Nodes.Singleton(addrN1), f, n2);
        }

        private void SetLoadField(Nodes locs, Field f, IPTAnalysisNode n2)
        {
            foreach (IPTAnalysisNode loc in locs)
                SetLoadField(loc, f, n2);
        }

        private void SetLoadField(IPTAnalysisNode loc, Field f, IPTAnalysisNode n2)
        {
            if (!loc.IsStruct)
                O.AddOEdge(loc, f, n2);
            else
                O.AddSEdge(loc, f, n2);
        }

        public void LoadFieldAddress(Variable v1, Variable v2, Field f, Label lb)
        {
            if (CheckPrimitive(((Reference)v1.Type).ElementType))
                return;

            // PTAnalysisNode nAddrV1 = GetAddress(v1);
            Nodes escapingFieldAddresses;
            Nodes fAddr = GetFieldAdress(v2, f, lb, out escapingFieldAddresses,v1.Type);

            if (fAddr.Count == 0 && escapingFieldAddresses.Count == 0)
            {
                IPTAnalysisNode addr = NewLoadAddressOrStructNode(lb, v1.Type);
                Nodes valuesV2 = Values(GetLocations(v2));
                CheckValues(valuesV2, lb, v2);
                AssignLoadField(GetLocations(v2), f, addr);
                fAddr.Add(addr);
            }
            // Assign(v1, fAddr, lb);
            AssignValues(GetLocationsRef(v1), fAddr, lb);
        }

        public IPTAnalysisNode NewValueNode(Label lb, IPTAnalysisNode addr, TypeNode t)
        {
           
            /*
            if (addr.IsParameterNode && ((PNode)addr).IsByValue)
            {
                return new PLNode(lb, t, (PNode)addr);
            }
            return NewValueOrStructNode(lb, t);
            */

            
            if (addr.IsParameterNode)
            {
                IParameterNode pn = addr as PNode;
                if (pn.IsByValue)
                    return new PLNode(lb, t, pn);
                else { }
                    if (!isStructRef(t))
                        return new LAddrParamNode(lb, t,pn);
                    /*
                    if (pn.IsByRef)
                        return new VariableAddrNode(LNode, pn.ReferencedVariable);
                    else
                        return new VariableAddrNode(LNode, pn.ReferencedVariable);
                     */
            }
            return NewValueOrStructNode(lb, t);
            
        }

        private IPTAnalysisNode NewValueOrStructNode(Label lb, TypeNode t)
        {
            if (isStructRef(t))
                return new StructNode(lb, t);
            else
                return new LValueNode(lb, t);
        }

        private IPTAnalysisNode NewLoadOrStructNode(Label lb, TypeNode t)
        {
            if (IsStructType(t))
                return new StructNode(lb, t);
            else
                return new LValueNode(lb, t);
                // return new LAddrNode(lb, t);
                //return new LFieldAddrNode(lb, t);
        }

        private IPTAnalysisNode NewLoadAddressOrStructNode(Label lb, TypeNode t)
        {
            if (IsStructType(t))
                return new StructNode(lb, t);
            else
                return new LAddrNode(lb, t);
        }


        #region IDataFlow Implementation
        public void Dump()
        {
            Console.Out.WriteLine(ToString());
            GenerateDotGraph(Console.Out);
        }
        #endregion

        // Internal method for debug purposes
        public void DumpDifference(PTGraph ptg)
        {
            string res = "";
            if (!this.I.Equals(ptg.I))
            {
                Set<Edge> iD = I.Difference(ptg.I);
                res += string.Format("IEdges: {0} {1} \n", iD.Count, iD);
            }
            if (!this.O.Equals(ptg.O))
            {
                Set<Edge> oD = O.Difference(ptg.O);
                res += string.Format("OEdges: {0} {1}\n",oD.Count, oD);
            }
            if (!this.LV.Equals(ptg.LV))
            {
                res += string.Format("LocVar:");
                foreach (PT_Variable v in LV.Keys)
                {
                    if (ptg.LV.ContainsKey(v) && LV[v].Equals(ptg.LV[v]))
                    {
                    }
                    else
                    {
                        if (ptg.LV.ContainsKey(v) && !LV[v].Equals(ptg.LV[v]))
                        {
                            res += string.Format("{0} -> {1}\n", v.Name, LV[v].Difference(ptg.LV[v]));
                        }
                        else
                        {
                            res += string.Format("{0} -> doesn't exists\n", v.Name);
                        }
                    }

                }
            }

            if (!this.E.Equals(ptg.E))
            {
                res += string.Format("Esc: {0}\n", E.Difference(ptg.E));
            }
            Console.Out.WriteLine(res);
        }

        #region Equals, Hash, ToString
        public override bool Equals(object obj)
        {
            PTGraph ptg = (PTGraph)obj;
            bool res = iEdges.Equals(ptg.iEdges) && oEdges.Equals(ptg.oEdges) && locVar.Equals(ptg.locVar)
                && escapingNodes.Equals(ptg.escapingNodes);
            return res;
        }

        public override int GetHashCode()
        {
            return iEdges.GetHashCode() + oEdges.GetHashCode() + locVar.GetHashCode()
                + escapingNodes.GetHashCode();
        }

        public override string ToString()
        {
            string res = "";
            res += string.Format("PTGraph for {0}\n", m.Name.Name);
            res += string.Format("PNodes: {0}\n", AddrPNodes);
            res += string.Format("IEdges: {0}\n", I);
            res += string.Format("OEdges: {0}\n", O);
            res += string.Format("LocVar: {0}\n", LV);
            res += string.Format("Escape: {0}\n", E);

            return res;
        }
        #endregion

        #region Graph Simplification
        public PTGraph Simplify()
        {
            PTGraph callerCopy = new PTGraph(this);
            Set<Edge> iToRemove = new Set<Edge>();
            Set<Edge> oToRemove = new Set<Edge>();

            foreach (Parameter p in callerCopy.ParameterMap.Keys)
            {
                if (!CheckPrimitive(p.Type))
                {
                    IPTAnalysisNode oldValue = callerCopy.parameterOldValue[p];
                    if(oldValue!=null) callerCopy.Assign(p, oldValue, methodLabel);
                }
            }

            Set<PT_Variable> varsToRemove = new Set<PT_Variable>();
            foreach (PT_Variable ptv in callerCopy.LV.Keys)
            {
                Variable v = ptv.Variable;
                if (v is Parameter || v.Equals(GlobalScope)|| v.Equals(RetValue))
                { }
                else
                {
                    varsToRemove.Add(ptv);
                }
            }
            foreach (PT_Variable ptv in varsToRemove)
                callerCopy.LV.Remove(ptv);
            
            
            Nodes B = callerCopy.ReachableFromParametersReturnAndGlobals();
            foreach (Edge e in callerCopy.I)
            {
                if (!B.Contains(e.Src) || !B.Contains(e.Dst))
                    iToRemove.Add(e);
            }
            foreach (Edge e in callerCopy.O)
            {
                if (!B.Contains(e.Src) || !B.Contains(e.Dst))
                    oToRemove.Add(e);
            }
            callerCopy.I.RemoveEdges(iToRemove);
            callerCopy.O.RemoveEdges(oToRemove);
            
            
            return callerCopy;
        }
        #endregion

        #region Auxilary Methods for type testing
        
        public static bool IsDelegateType(TypeNode t)
        {
            bool res = t is DelegateNode;
            return res;
        }
        public static bool IsStructType(TypeNode t)
        {
            bool res = t is Struct && !t.IsPrimitive;
            return res;
        }
       
        public static bool isValueType(TypeNode t)
        {
            bool res = t.IsValueType;
            return res;
        }

        public static bool isObject(TypeNode t)
        {
            return t.IsObjectReferenceType;
        }
        public static bool isStructRef(TypeNode t)
        {
            bool res = false;
            if (t is Reference)
            {
                Reference refType = (Reference)t;
                res = (IsStructType(refType.ElementType));
            }
            return res;
        }

        public static bool isRef(TypeNode t)
        {
            // bool res = !isValueType(t);
            bool res = t is Reference;
            res = res && !(t is ArrayType);
            return res;

        }
        
        #endregion

        #region Struct Values Support (Copying)
        private Nodes CopyStructNodes(TypeNode t, Nodes v2Nodes, Label lb)
        {
            Nodes sNodes = Nodes.Empty;
            foreach (IPTAnalysisNode n in v2Nodes)
            {
                IPTAnalysisNode vNode = n;
                // Creates a struct node to reflect that it is a newly
                // created location of type Struct
                StructNode sNode = new StructNode(lb, t);
                CopyStruct(sNode, vNode,lb);
                sNodes.Add(sNode);
            }
            return sNodes;
        }

        /// <summary>
        /// Copy contents of Struct srcSn in Struct destSn
        /// </summary>
        /// <param name="destSn"></param>
        /// <param name="srcSn"></param>
        private void CopyStruct(IPTAnalysisNode destSn, IPTAnalysisNode srcSn, Label lb)
        {
            if(Method.Name.Name.Equals("AddAll"))
            {}
            if (PointsToAnalysis.debug)
            {
                Console.Out.WriteLine("Entre a Struct Copy");
            }
            Nodes visited = Nodes.Empty;
            CopyStruct(destSn, srcSn, lb, visited);

        }
        private void CopyStruct(IPTAnalysisNode destSn, IPTAnalysisNode srcSn, Label lb, Nodes visited)
        {
            CopyStructForEdges(destSn, srcSn, I, lb, visited);
            CopyStructForEdges(destSn, srcSn, O, lb, visited);
        }

        /// <summary>
        /// Copy the contents pointed by a set of edges
        /// </summary>
        /// <param name="destSn"></param>
        /// <param name="srcSn"></param>
        /// <param name="edges"></param>
        private void CopyStructForEdges(IPTAnalysisNode destSn, IPTAnalysisNode srcSn, Edges edges, Label lb, Nodes visited)
        {
            visited.Add(srcSn);
            Set<Edge> edgesFromSrc = new Set<Edge>();
            edgesFromSrc.AddRange(edges.EdgesFromSrc(srcSn));
            foreach (Edge e in edgesFromSrc)
            {
                //if (e is StructEdge)
                {
                    IPTAnalysisNode n = e.Dst;
                    if (!visited.Contains(n))
                    {
                        //if (IsStructType(n.Type))
                        {
                            Nodes edgesInDstSn = edges.Adjacents(destSn, e.Field, true);
                            //StructNode sn = null;
                            IPTAnalysisNode sn = null;
                            // If we haven't add this field to the dst struc, we add it
                            if (edgesInDstSn.Count == 0)
                            {
                                // sn = new StructNode(destSn.Label, n.Type);
                                // sn = new StructNode(lb, n.Type);
                                sn = new AddrNode(lb, n.Type);
                                edges.AddSEdge(destSn, e.Field, sn);
                            }
                            else
                            {
                                // else we use the already added node
                                //sn = (StructNode)edgesInDstSn.PickOne();
                                sn = edgesInDstSn.PickAnElement();
                            }
                            //if (!visited.Contains(sn))
                            if (IsStructType(n.Type))
                            {
                                CopyStruct(sn, n, lb,visited);
                            }
                            else
                            {
                                AssignValues(sn, Values(n));
                                //edges.AddIEdge(sn, e.Field, n);
                            }

                        }
                        //else
                        //{
                        //    edges.AddSEdge(destSn, e.Field, n);
                        //}
                    }
                }
            }
        }
        #endregion


        #region Basic operations (Copy, Null Assigment, Forget variable, RemoveValues pointed by a Variable or Addr)
        /// <summary>
        /// f(v1 = null). v1 points to nothing (is deleted from LV)
        /// </summary>
        /// <param name="v1"></param>
        public void ApplyAssignNull(Variable v1)
        {
            Assign(v1, NullNode.nullNode, MethodLabel);
            // ForgetVariable(v1);
        }
        /// <summary>
        /// f(v1 = v2) or f(v1 = *v2) or f(v1= &amp;v2) 
        /// or copy of struct type
        /// It is an strong update
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="lb"></param>
        public void CopyLocVar(Variable v1, Variable v2, Label lb)
        {
            Assign(v1, v2, lb);
            return;
        }
        public void CopyLocVar(Variable v1, Variable v2)
        {
            Assign(v1, v2, methodLabel);
            return;
        }

        /// <summary>
        /// Make p1 points to nothing
        /// </summary>
        /// <param name="v1"></param>
        public void ForgetVariable(Variable v1)
        {
            if (v1 != null)
            {
                PT_Variable ptv_v1 = new PT_Variable(v1);
                RemoveValues(v1);
                //LV[v1].Clear();
                LV.Remove(ptv_v1);
            }
        }
        /// <summary>
        /// Remove the values assigned to a variable
        /// </summary>
        /// <param name="v1"></param>
        public void RemoveValues(Variable v1)
        {
            //PTAnalysisNode addr = GetAddress(v1);
            //RemoveValues(addr);
            RemoveValues(GetLocations(v1));
        }
        public void RemoveValues(Nodes ns)
        {
            foreach (IPTAnalysisNode n in ns)
                RemoveValues(n);
        }
        /// <summary>
        /// Remove the values assigned to an addr
        /// </summary>
        /// <param name="addr"></param>
        public void RemoveValues(IPTAnalysisNode addr)
        {
            Set<Edge> es = new Set<Edge>();
            es.AddRange(I.EdgesByNodeAndField(addr, PTGraph.asterisk, true));
            foreach (Edge e in es)
                I.RemoveEdge(e);

            es.Clear();
            es.AddRange(O.EdgesByNodeAndField(addr, PTGraph.asterisk, true));
            foreach (Edge e in es)
                O.RemoveEdge(e);
        }
        #endregion

        #region Method Calls Support
        /// <summary>
        /// Bind pointsTo graph of the caller with the pointsTo graph of the callee.
        /// This is the most expensive operation.
        /// Can be skiped assuming that all calls are non-analyzable.
        /// </summary>
        /// <param name="vr"></param>
        /// <param name="callee"></param>
        /// <param name="receiver"></param>
        /// <param name="arguments"></param>
        /// <param name="calleePTGraph"></param>
        /// <param name="lb"></param>
        /// <param name="imp"></param>
        /// <returns></returns>
        public PTGraph ApplyAnalyzableCall(Variable vr, Method callee, Variable receiver,
            ExpressionList arguments, PTGraph calleePTGraph, Label lb, out InterProcMapping imp)
        {
            
            //Console.Out.WriteLine("Analyzable");
            imp = InterProcMapping.ComputeInterProgMapping(this, calleePTGraph,
                receiver, arguments, vr, lb);
            PTGraph interProcPTG = imp.ComputeInterProgGraph(this, calleePTGraph,
                receiver, arguments, vr, lb);
            //Console.Out.WriteLine(imp);
            //Console.Out.WriteLine(calleePTGraph);
            return interProcPTG;
        }
        /// <summary>
        /// Builds a PTG from user provided annotations according(see internal report)
        /// </summary>
        /// <param name="callee"></param>
        /// <param name="isPure"></param>
        /// <param name="isReturnFresh"></param>
        /// <param name="modifiesGlobal"></param>
        /// <param name="readsGlobal"></param>
        /// <param name="escapingParameters"></param>
        /// <param name="capturedParameters"></param>
        /// <param name="freshParameters"></param>
        /// <param name="readParameters"></param>
        /// <param name="writeParameters"></param>
        /// <param name="writeConfinedParameters"></param>
        /// <returns></returns>
        public static PTGraph PTGraphFromAnnotations(Method callee)
        {
            // Creates an empty PTG using omega nodes (To-do improve 
            PTGraph res = new PTGraph(callee, true);
            Label lb = res.NextMethodLabel();

            Nodes CaptureTargets = Nodes.Empty;
            Nodes NonCaptureTargets = Nodes.Empty;
            Nodes ReadTargets = Nodes.Empty;
            // Compute the set of target nodes 

            foreach (Parameter p in res.ParameterMap.Keys)
            {
                bool isRead = PointsToAndEffectsAnnotations.IsDeclaredRead(p);
                IParameterNode pn = res.ParameterMap[p];
                bool captured = false;
                Nodes reachRefs = res.getReachRefs(pn);
                if (PointsToAndEffectsAnnotations.IsDeclaredEscaping(p, out captured))
                {
                    if (!captured)
                    {
                        NonCaptureTargets.AddRange(reachRefs);
                    }
                    else
                    {
                        CaptureTargets.AddRange(reachRefs);
                    }
                }
                if (isRead)
                {
                    ReadTargets.AddRange(reachRefs);
                }
            }
            if (PointsToAndEffectsAnnotations.IsDeclaredReadingGlobals(callee))
            {
                NonCaptureTargets.AddRange(res.Values(PTGraph.GlobalScope));
            }

            if (res.RetValue != null)
            {

                {
                    foreach (IPTAnalysisNode an in res.Values(res.RetValue))
                    {
                        res.Store(an, PTGraph.allFieldsNotOwned, NonCaptureTargets, res.NextMethodLabel());
                        res.Store(an, PTGraph.allFields, CaptureTargets, res.NextMethodLabel());
                        res.Store(an, PTGraph.allFieldsNotOwned, ReadTargets, res.NextMethodLabel());
                    }
                }

                if (!PointsToAndEffectsAnnotations.IsDeclaredFresh(callee))
                {
                    // If ret value is not Fresh we apply a weak assign to model the potential assigment 
                    // to escaping objects
                    res.Assign(res.GetLocations(res.RetValue), NonCaptureTargets, res.methodLabel, false);
                    res.Assign(res.GetLocations(res.RetValue), CaptureTargets, res.methodLabel, false);
                }
                NonCaptureTargets.AddRange(res.Values(res.RetValue));
            }

            // Now, we add the edges according to the read/write specification
            foreach (Parameter p in res.ParameterMap.Keys)
            {
                IParameterNode pn = res.ParameterMap[p];

                bool isRead = PointsToAndEffectsAnnotations.IsDeclaredRead(p);

                bool isWrite = PointsToAndEffectsAnnotations.IsWriteParameter(callee, p);
                bool isWriteConfined = PointsToAndEffectsAnnotations.IsWriteConfinedParameter(callee, p);
                
                /*
                bool isWrite = (!PointsToAndEffectsAnnotations.IsDeclaredPure(m) 
                                  && PointsToAndEffectsAnnotations.IsDeclaredWrite(p)) 
                               || p.IsOut;
                bool isWriteConfined = !PointsToAndEffectsAnnotations.IsDeclaredPure(m)
                                        && (PointsToAndEffectsAnnotations.IsDeclaredWriteConfined(m)
                                            || PointsToAndEffectsAnnotations.IsDeclaredWriteConfined(p));
                */

                if (isWrite || isWriteConfined)
                {
                    if (isRef(p.Type))
                    {
                        if (!PointsToAndEffectsAnnotations.IsDeclaredFresh(p))
                        {
                            // This is an store Indirect of p with Cap U NonCap
                            res.AssignValues(res.Values(p), CaptureTargets, lb);
                            res.AssignValues(res.Values(p), NonCaptureTargets, lb);
                        }
                    }
                }
                
                foreach (IPTAnalysisNode n1 in res.leaves[p])
                {
                    if(isWrite)
                    {
                        // Add egdes  (n1,?, na) and (na, Captured U reachRef(LV(p)
                        res.Store(n1, PTGraph.allFields, CaptureTargets, res.NextMethodLabel());
                        res.Store(n1, PTGraph.allFields, res.getReachRefs(n1), res.NextMethodLabel());
                        // Add egdes  (n1,$, na) and (na, NonCaptured)
                        res.Store(n1, PTGraph.allFieldsNotOwned, NonCaptureTargets, res.NextMethodLabel());
                        res.Store(n1, PTGraph.allFieldsNotOwned, ReadTargets, res.NextMethodLabel());
                    }
                    
                    

                    if (isWriteConfined)
                    {
                        // Add a new omega node to indicate all node outside the ownership cone can be accessed but not written
                        ILoadNode nOmegaLoad = new LValueNode(res.NextMethodLabel(), TypeNode.GetTypeNode(Type.GetType("System.Object")));
                        nOmegaLoad.SetOmegaLoadNode();
                        res.Store(n1, PTGraph.allFieldsNotOwned, nOmegaLoad, res.NextMethodLabel());
                    }
                    /*
                    if (isRead)
                    {
                        ILoadNode nOmegaRLoad = new LValueNode(res.NextMethodLabel(), TypeNode.GetTypeNode(Type.GetType("System.Object")));
                        nOmegaRLoad.SetOmegaLoadNode();
                        Nodes fAddrs = res.GetFieldAddress(n1, PTGraph.allFieldsNotOwned, res.methodLabel);
                        if (fAddrs.Count == 0)
                        {
                            IAddrNode addr = new AddrNode(res.NextMethodLabel(), p.Type.GetReferenceType());
                            res.SetLoadField(n1, PTGraph.allFieldsNotOwned, addr);
                            fAddrs.Add(addr);
                        }
                        //foreach (IPTAnalysisNode n in ReadTargets)
                        //    res.SetLoadField(fAddrs, PTGraph.asterisk, n);

                        res.SetLoadField(fAddrs, PTGraph.asterisk, nOmegaRLoad);

                    }
                    */

                }
                
                
                
            }

            if (PointsToAndEffectsAnnotations.IsDeclaredWritingGlobals(callee))
            {
                res.Store(GNode.nGBL, PTGraph.allFields, CaptureTargets, res.NextMethodLabel());
                res.Store(GNode.nGBL, PTGraph.allFieldsNotOwned, NonCaptureTargets, res.MethodLabel);
            }
            

            return res;
        }
    

        // Assign to the special variable vRet the nodes pointed by v
        public void ApplyReturn(Variable v, Label lb)
        {
            if (v != null && !CheckPrimitive(v.Type))
            {
                Assign(RetValue, v, lb);
            }
        }
        #endregion

        public void NewInsideNode(Variable v, Label lb, TypeNode type)
        {
            IPTAnalysisNode n = null;
            if (IsDelegateType(type))
            {
                DelegateNode dn = (DelegateNode)type;
                MethodDelegateNode mnode = new MethodDelegateNode(lb,type,null);
                n = mnode;
            }
            else
            {
                IINode inode = new InsideNode(lb, type);
                n = inode;
            }
            Assign(v, n, lb);
        }

        #region Escape Checking for Parameters
        public bool CheckEscape(Parameter p)
        {
            //if (Method.Name.Name.Equals("AddAll"))
            //{
            //    System.Diagnostics.Debugger.Break();
            //}
            bool res = false;
            Nodes locs = GetLocations(p);
            Nodes pValues = Values(locs);
            Nodes args = Nodes.Empty;
            foreach (IPTAnalysisNode pn in AddrPNodes)
                //if (!pn.Equals(GetAddress(p)))
                if(!locs.Contains(pn))
                    args.Add(pn);

            //Nodes B = ReachableFromReturnAndGlobalsAnd(args);
            //res = B.IntersectionNotEmpty(pValues);
            res = IsReachableFromReturnGlobalAnd(pValues, args);
            return res;
          }

            private bool IsReachableFromReturnGlobalAnd( Nodes pValues, Nodes args)
            {
                Nodes Esc = new Nodes(E);
                Esc.AddRange(GetLocations(GlobalScope));
                if (RetValue != null)
                {
                    Esc.AddRange(GetLocations(RetValue));
                }
                Esc.AddRange(args);
                bool res = IsReachableFrom(pValues,Esc,true,true,true);
                return res;
            }
        
        #endregion

            
        #region Dot Graph Generation
        public void GenerateDotGraph(string filename)
        {
            using (StreamWriter sw = new StreamWriter(filename)) 
            {
                GenerateDotGraph(sw);       
            }
        }
        public void GenerateDotGraph(System.IO.TextWriter output)
        {
            GenerateDotGraph(output,false);
        }
          public void GenerateDotGraph(System.IO.TextWriter output, bool reduced)
        {
        
            // output.WriteLine("digraph " + Method.Name.Name + " {");
            output.WriteLine("digraph " + "PTG" + " {");
            foreach (PT_Variable v in LV.Keys)
            {
                Nodes varAddr = LV[v];
                string varName = "NullName";

                if (v != null & v.Name != null)
                    //varName = v.Name.Name;
                    varName = reduced?v.Name.Name:v.ToString();

                output.WriteLine("\"{0}\" [shape = none]", varName);
                
                foreach (IPTAnalysisNode n in varAddr)
                {
                    output.WriteLine("\"{0}\" -> \"{2}\" [label = \"{1}\"]", varName, "", n);
                }
            }
            
            Nodes ns = Nodes.Empty;
            foreach (Edge e in I)
            {
                ns.Add(e.Src);
                ns.Add(e.Dst);
            }
            foreach (Edge e in O)
            {
                ns.Add(e.Src);
                ns.Add(e.Dst);
            }
            int cont = 0;
            foreach (IPTAnalysisNode n in ns)
            {
                cont++;
                string name = reduced ? cont.ToString() : n.ToString();

                // output.WriteLine("\nnode [shape = ellipse]");
                string shape = "box";
                if (n.IsAddrNode)
                {
                    shape = "ellipse";
                    if(n.IsVariableReference)
                        name = reduced ? n.Name : n.ToString();
                }

                if (n.IsOmega || n.IsOmegaConfined || n.IsOmegaLoad)
                {
                    string oStr = "W";
                    if (n.IsOmegaConfined)
                        oStr += "C";
                    if (n.IsOmegaLoad)
                        oStr += "L";
                    name = "("+oStr+")" + name;

                }
                if(n.IsGlobal)
                    shape = "pentagon";
                string style = "solid";
                if (n.IsLoad || n.IsParameterValueLNode)
                    style = "dotted";

                String nodeToStr = n.ToString();
                string dotNode = String.Format("\"{0}\" [label = \"{1}\", shape = {2}, style = {3} ]",
                    nodeToStr, name, shape, style);
                output.WriteLine(dotNode);
            }




            foreach (Edge e in I)
            {
                e.EdgeToDot(output, true);
            }
            output.WriteLine();
            foreach (Edge e in O)
            {
                e.EdgeToDot(output, false);
            }
            output.WriteLine("}");
        }

        
        #endregion

        #region Freshness Checking
        public bool CheckMethodFreshness(Method m)
        {
            bool res = true;
            Nodes retValues = Values(GetLocations(RetValue));
            foreach (IPTAnalysisNode addr in AddrPNodes)
            {
                //if (retValues.Intersection(Values(addr)).Count!=0)
                if (!retValues.Intersection(Values(addr)).IsEmpty)
                    return false;
            }
            if (retValues.Contains(GNode.nGBL))
                return false;
            return res;
        }

        public bool CheckParameterFreshness(Parameter p)
        {
            bool res = true;
            Nodes referencedVariables = Values(GetLocations(p));
            Nodes referencedVariablesValues = Values(referencedVariables);
            foreach (IPTAnalysisNode addr in AddrPNodes)
            {
                // if (referencedVariablesValues.Intersection(Values(addr)).Count != 0)
                if (!referencedVariablesValues.Intersection(Values(addr)).IsEmpty)
                    return false;
            }
            if (referencedVariablesValues.Contains(GNode.nGBL))
                return false;
            return res;
          }
        #endregion



          public virtual  void ForgetField(Variable v1, Field f)
          {
              Set<Edge> eToRemove = new Set<Edge>();
              foreach (IPTAnalysisNode n in Values(v1))
              {
                  eToRemove.AddRange(I.EdgesFromSrcAndField(n,f));
              }
              I.RemoveEdges(eToRemove);
              eToRemove = new Set<Edge>();
              foreach (IPTAnalysisNode n in Values(v1))
              {
                  eToRemove.AddRange(O.EdgesFromSrcAndField(n,f));
              }
              O.RemoveEdges(eToRemove);

          }
      }
    public class RetValue : Local
    {
        private Method m;

        #region Constructors
        public RetValue(Method m)
            : base(new Identifier("vRet"), m.ReturnType)
        {
            this.m = m;
        }
        #endregion

        #region Equals, Hash
        public override bool Equals(object obj)
        {
            RetValue rv2 = obj as RetValue;
            return rv2 != null & this.m.Equals(m);
        }
        public override int GetHashCode()
        {
            return m.GetHashCode();
        }
        public override int UniqueKey
        {
            get
            {
                return m.UniqueKey;
            }
        }
        #endregion
    }


    /// <summary>
    /// A Generic Pair
    /// </summary>
    /// <typeparam name="T1"></typeparam>
    /// <typeparam name="T2"></typeparam>
    public class Pair<T1, T2>
    {
        private T2 t2;
        private T1 t1;

        #region
        public Pair(T1 t1, T2 t2)
        {
            this.t1 = t1;
            this.t2 = t2;
        }
        #endregion

        #region Properties
        public T1 Fst
        {
            get { return t1; }
            set { t1 = value; }
        }
        public T2 Snd
        {
            get { return t2; }
            set { t2 = value; }
        }
        #endregion

        #region
        public override bool Equals(object obj)
        {
            Pair<T1, T2> p = obj as Pair<T1, T2>;
            return p != null && t1.Equals(p.t1) && t2.Equals(p.t2);
        }
        public override int GetHashCode()
        {
            return t1.GetHashCode() + t2.GetHashCode();
        }
        public override string ToString()
        {
            return "<" + t1 + "," + t2 + ">";
        }
        #endregion
    }
    
}
