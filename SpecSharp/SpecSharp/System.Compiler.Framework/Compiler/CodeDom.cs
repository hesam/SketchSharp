//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Specialized;
using System.CodeDom;
using System.Diagnostics;

#if CCINamespace
using Microsoft.Cci.Metadata;
namespace Microsoft.Cci{
#else
using System.Compiler.Metadata;
namespace System.Compiler{
#endif
  /// <summary>
  /// Walks a System.CodeDom.CodeCompileUnit and produces a corresponding CompilationUnit.
  /// </summary>
  public sealed class CodeDomTranslator{
    private Compiler compiler;
    private ErrorNodeList errorNodes;
    private Module targetModule;

    public CodeDomTranslator(){
    }
    /// <summary>
    /// Walks the supplied System.CodeDom.CodeCompileUnit and produces a corresponding CompilationUnit.
    /// Enters declarations into the supplied Module and errors into the supplied ErrorNodeList. 
    /// Calls back to the supplied compiler to resolve assembly references and to create appropriate documents for code snippets.
    /// </summary>
    /// <param name="compiler">Called upon to resolve assembly references and to create Documents for snippets.</param>
    /// <param name="compilationUnit">The root of the CodeDOM tree to be translated into an IR CompileUnit.</param>
    /// <param name="targetModule">The module or assembly to which the compilation unit will be compiled.</param>
    /// <param name="errorNodes">Errors in the CodeDOM tree that are found during translation are added to this list.</param>
    /// <returns></returns>
    public CompilationUnit Translate(Compiler compiler, CodeCompileUnit compilationUnit, Module targetModule, ErrorNodeList errorNodes){
      Debug.Assert(compiler != null); 
      Debug.Assert(compilationUnit != null); 
      Debug.Assert(targetModule != null); 
      Debug.Assert(errorNodes != null);
      this.compiler = compiler;
      this.errorNodes = errorNodes;
      this.targetModule = targetModule;
      CodeSnippetCompileUnit cscu = compilationUnit as CodeSnippetCompileUnit;
      CompilationUnit cunit = cscu != null ? new CompilationUnitSnippet() : new CompilationUnit();
      this.Translate(compilationUnit.AssemblyCustomAttributes, targetModule.Attributes);
      StringCollection references = compilationUnit.ReferencedAssemblies;
      if (references != null && references.Count > 0){
        AssemblyReferenceList arefs = targetModule.AssemblyReferences;
        TrivialHashtable alreadyReferencedAssemblies = new TrivialHashtable();
        for (int i = 0, n = arefs.Count; i < n; i++)
          alreadyReferencedAssemblies[arefs[i].Assembly.UniqueKey] = this;
        foreach (string rAssemblyName in references)
          compiler.AddAssemblyReferenceToModule(null, targetModule, rAssemblyName, null, errorNodes, alreadyReferencedAssemblies, false);
      }
      Namespace defaultNamespace = new Namespace(Identifier.Empty, Identifier.Empty, null, null, new NamespaceList(), null);
      NamespaceList nspaceList = defaultNamespace.NestedNamespaces;
      CodeNamespaceCollection nspaces = compilationUnit.Namespaces;
      if (nspaces != null) 
        foreach (CodeNamespace cns in nspaces) 
          nspaceList.Add(this.Translate(cns));
      if (cscu == null) return cunit;
      Document doc = null;
      if (cscu.LinePragma == null)
        doc = compiler.CreateDocument(targetModule.Name, 1, cscu.Value);
      else{
        doc = compiler.CreateDocument(cscu.LinePragma.FileName, cscu.LinePragma.LineNumber, cscu.Value);
        cunit.Name = Identifier.For(cscu.LinePragma.FileName);
      }
      cunit.SourceContext = new SourceContext(doc);
      defaultNamespace.SourceContext = cunit.SourceContext;
      return cunit;
    }
    private AttributeList Translate(CodeAttributeDeclarationCollection attributes, AttributeList attributeList){
      if (attributes == null) return null;
      int n = attributes.Count;
      if (attributeList == null)
        if (n == 0) return null; else attributeList = new AttributeList(n);
      for (int i = 0; i < n; i++)
        attributeList.Add(this.Translate(attributes[i]));
      return attributeList;
    }
    private AttributeNode Translate(CodeAttributeDeclaration attribute){
      if (attribute == null) return null;
      AttributeNode anode = new AttributeNode();
      anode.Constructor = this.TranslateToSimpleIdentifierOrQualifiedIdentifier(attribute.Name);
      CodeAttributeArgumentCollection codeArguments = attribute.Arguments;
      if (codeArguments != null){
        int n = codeArguments.Count;
        if (n > 0){
          ExpressionList arguments = anode.Expressions = new ExpressionList(n);
          for (int i = 0; i < n; i++){
            CodeAttributeArgument arg = codeArguments[i];
            string name = arg.Name;
            Expression argValue = this.Translate(arg.Value);
            if (name == null || name.Length == 0)
              arguments.Add(argValue);
            else
              arguments.Add(new NamedArgument(Identifier.For(name), argValue));
          }
        }
      }
      return anode;
    }
    private Expression Translate(CodeExpression expr){
      if (expr == null) return null;
      if (expr is CodeArgumentReferenceExpression) return this.Translate((CodeArgumentReferenceExpression)expr);
      if (expr is CodeArrayCreateExpression) return this.Translate((CodeArrayCreateExpression)expr);
      if (expr is CodeArrayIndexerExpression) return this.Translate((CodeArrayIndexerExpression)expr);
      if (expr is CodeBaseReferenceExpression) return this.Translate((CodeBaseReferenceExpression)expr);
      if (expr is CodeBinaryOperatorExpression) return this.Translate((CodeBinaryOperatorExpression)expr);
      if (expr is CodeCastExpression) return this.Translate((CodeCastExpression)expr);
      if (expr is CodeDelegateCreateExpression) return this.Translate((CodeDelegateCreateExpression)expr);
      if (expr is CodeDelegateInvokeExpression) return this.Translate((CodeDelegateInvokeExpression)expr);
      if (expr is CodeDirectionExpression) return this.Translate((CodeDirectionExpression)expr);
      if (expr is CodeEventReferenceExpression) return this.Translate((CodeEventReferenceExpression)expr);
      if (expr is CodeFieldReferenceExpression) return this.Translate((CodeFieldReferenceExpression)expr);
      if (expr is CodeIndexerExpression) return this.Translate((CodeIndexerExpression)expr);
      if (expr is CodeMethodInvokeExpression) return this.Translate((CodeMethodInvokeExpression)expr);
      if (expr is CodeMethodReferenceExpression) return this.Translate((CodeMethodReferenceExpression)expr);
      if (expr is CodeObjectCreateExpression) return this.Translate((CodeObjectCreateExpression)expr);
      if (expr is CodePrimitiveExpression) return this.Translate((CodePrimitiveExpression)expr);
      if (expr is CodePropertyReferenceExpression) return this.Translate((CodePropertyReferenceExpression)expr);
      if (expr is CodePropertySetValueReferenceExpression) return this.Translate((CodePropertySetValueReferenceExpression)expr);
      if (expr is CodeSnippetExpression) return this.Translate((CodeSnippetExpression)expr);
      if (expr is CodeThisReferenceExpression) return this.Translate((CodeThisReferenceExpression)expr);
      if (expr is CodeTypeOfExpression) return this.Translate((CodeTypeOfExpression)expr);
      if (expr is CodeTypeReferenceExpression) return this.Translate((CodeTypeReferenceExpression)expr);
      if (expr is CodeVariableReferenceExpression) return this.Translate((CodeVariableReferenceExpression)expr);
      Debug.Assert(false);
      this.HandleError(Error.DidNotExpect, expr.GetType().FullName);
      return null;
    }
    private Expression Translate(CodeArgumentReferenceExpression expr){
      if (expr == null) return null;
      return Identifier.For(expr.ParameterName);
    }
    private Expression Translate(CodeArrayCreateExpression expr){
      if (expr == null) return null;
      ConstructArray cons = new ConstructArray();
      cons.ElementType = this.TranslateToTypeNode(expr.CreateType);
      ExpressionList initializers = cons.Initializers = this.Translate(expr.Initializers);
      cons.Operands = new ExpressionList(1);
      if (expr.SizeExpression != null)
        cons.Operands.Add(this.Translate(expr.SizeExpression));
      else{
        int size = 0;
        if (initializers != null) size = initializers.Count;
        if (expr.Size > size) size = expr.Size;
        cons.Operands.Add(new Literal(size, SystemTypes.Int32));
      }
      return cons;
    }
    private Expression Translate(CodeArrayIndexerExpression expr){
      if (expr == null) return null;
      Indexer indexer = new Indexer();
      indexer.Object = this.Translate(expr.TargetObject);
      indexer.Operands = this.Translate(expr.Indices);
      return indexer;
    }
    private Expression Translate(CodeBaseReferenceExpression expr){
      if (expr == null) return null;
      return new Base();
    }
    private Expression Translate(CodeBinaryOperatorExpression expr){
      if (expr == null) return null;
      Expression operand1 = this.Translate(expr.Left);
      Expression operand2 = this.Translate(expr.Right);
      switch(expr.Operator){
        case CodeBinaryOperatorType.Assign:
          return new AssignmentExpression(new AssignmentStatement(operand1, operand2, NodeType.Nop));
        default:
          return new BinaryExpression(operand1, operand2, this.Translate(expr.Operator));
      }
    }
    private NodeType Translate(CodeBinaryOperatorType oper){
      switch(oper){
        case CodeBinaryOperatorType.Add : return NodeType.Add;
        case CodeBinaryOperatorType.BitwiseAnd : return NodeType.And;
        case CodeBinaryOperatorType.BitwiseOr : return NodeType.Or;
        case CodeBinaryOperatorType.BooleanAnd : return NodeType.LogicalAnd;
        case CodeBinaryOperatorType.BooleanOr : return NodeType.LogicalOr;
        case CodeBinaryOperatorType.Divide : return NodeType.Div;
        case CodeBinaryOperatorType.GreaterThan : return NodeType.Gt;
        case CodeBinaryOperatorType.GreaterThanOrEqual : return NodeType.Ge;
        case CodeBinaryOperatorType.IdentityEquality: return NodeType.Eq;
        case CodeBinaryOperatorType.IdentityInequality: return NodeType.Ne;
        case CodeBinaryOperatorType.LessThan : return NodeType.Lt;
        case CodeBinaryOperatorType.LessThanOrEqual : return NodeType.Le;
        case CodeBinaryOperatorType.Modulus : return NodeType.Rem;
        case CodeBinaryOperatorType.Multiply : return NodeType.Mul;
        case CodeBinaryOperatorType.Subtract : return NodeType.Sub;
        case CodeBinaryOperatorType.ValueEquality : return NodeType.Eq;
      }
      Debug.Assert(false);
      this.HandleError(Error.DidNotExpect, "CodeBinaryOperatorType value == "+oper.ToString());
      return NodeType.Nop;
    }
    private Expression Translate(CodeCastExpression expr){
      if (expr == null) return null;
      Expression e = this.Translate(expr.Expression);
      MemberBinding t = this.Translate(expr.TargetType);
      return new BinaryExpression(e, t, NodeType.ExplicitCoercion);
    }
    private Expression Translate(CodeDelegateCreateExpression expr){
      if (expr == null) return null;
      TypeNode delegateType = this.TranslateToTypeNode(expr.DelegateType);
      Identifier methodName = Identifier.For(expr.MethodName);
      Expression targetObject = this.Translate(expr.TargetObject);
      return new ConstructDelegate(delegateType, targetObject, methodName);
    }
    private Expression Translate(CodeDelegateInvokeExpression expr){
      if (expr == null) return null;
      QualifiedIdentifier qualId = new QualifiedIdentifier(this.Translate(expr.TargetObject), StandardIds.Invoke);
      ExpressionList arguments = this.Translate(expr.Parameters);
      return new MethodCall(qualId, arguments, NodeType.Call);
    }
    private Expression Translate(CodeDirectionExpression expr){
      if (expr == null) return null;
      if (expr.Direction == FieldDirection.In)
        return this.Translate(expr.Expression);
      else
        return new UnaryExpression(this.Translate(expr.Expression), NodeType.AddressOf);
    }
    private Expression Translate(CodeEventReferenceExpression expr){
      if (expr == null) return null;
      return new QualifiedIdentifier(this.Translate(expr.TargetObject), Identifier.For(expr.EventName));
    }
    private ExpressionList Translate(CodeExpressionCollection exprs){
      if (exprs == null) return null;
      int n = exprs.Count;
      if (n == 0) return null;
      ExpressionList exprList = new ExpressionList(n);
      for (int i = 0; i < n; i++) exprList.Add(this.Translate(exprs[i]));
      return exprList;
    }
    private Expression Translate(CodeFieldReferenceExpression expr){
      if (expr == null) return null;
      Identifier fieldName = Identifier.For(expr.FieldName);
      CodeExpression targetObject = expr.TargetObject;
      if (targetObject == null) return fieldName;
      return new QualifiedIdentifier(this.Translate(targetObject), fieldName);
    }
    private Expression Translate(CodeMethodInvokeExpression expr){
      if (expr == null) return null;
      return new MethodCall(this.Translate(expr.Method), this.Translate(expr.Parameters));
    }
    private Expression Translate(CodeMethodReferenceExpression expr){
      if (expr == null) return null;
      Identifier methodName = Identifier.For(expr.MethodName);
      CodeExpression targetObject = expr.TargetObject;
      if (targetObject == null) return methodName;
      return new QualifiedIdentifier(this.Translate(targetObject), methodName);
    }
    private Expression Translate(CodeObjectCreateExpression expr){
      if (expr == null) return null;
      Construct cons = new Construct();
      cons.Constructor = this.Translate(expr.CreateType);
      cons.Operands = this.Translate(expr.Parameters);
      return cons;
    }
    private Expression Translate(CodePrimitiveExpression expr){
      if (expr == null) return null;
      Literal lit = new Literal(expr.Value);
      switch(Convert.GetTypeCode(expr.Value)){
        case TypeCode.Boolean: lit.Type = SystemTypes.Boolean; break;
        case TypeCode.Byte: lit.Type = SystemTypes.UInt8; break;
        case TypeCode.Char: lit.Type = SystemTypes.Char; break;
        case TypeCode.DateTime: lit.Type = SystemTypes.DateTime; break;
        case TypeCode.DBNull: lit.Type = SystemTypes.DBNull; break;
        case TypeCode.Decimal: lit.Type = SystemTypes.Decimal; break;
        case TypeCode.Double: lit.Type = SystemTypes.Double; break;
        case TypeCode.Empty: lit.Type = SystemTypes.Object; break;
        case TypeCode.Int16: lit.Type = SystemTypes.Int16; break;
        case TypeCode.Int32: lit.Type = SystemTypes.Int32; break;
        case TypeCode.Int64: lit.Type = SystemTypes.Int64; break;
        case TypeCode.SByte: lit.Type = SystemTypes.Int8; break;
        case TypeCode.Single: lit.Type = SystemTypes.Single; break;
        case TypeCode.String: lit.Type = SystemTypes.String; break;
        case TypeCode.UInt16: lit.Type = SystemTypes.UInt16; break;
        case TypeCode.UInt32: lit.Type = SystemTypes.UInt32; break;
        case TypeCode.UInt64: lit.Type = SystemTypes.UInt64; break;
      }
      return lit;
    }
    private Expression Translate(CodePropertyReferenceExpression expr){
      if (expr == null) return null;
      QualifiedIdentifier qualId = new QualifiedIdentifier();
      qualId.Qualifier = this.Translate(expr.TargetObject);
      qualId.Identifier = Identifier.For(expr.PropertyName);
      return qualId;
    }
    private Expression Translate(CodePropertySetValueReferenceExpression expr){
      return new SetterValue();
    }
    private Expression Translate(CodeSnippetExpression expr){
      if (expr == null) return null;
      ExpressionSnippet snippet = new ExpressionSnippet();
      Document doc = this.compiler.CreateDocument(null, 1, expr.Value);
      snippet.SourceContext = new SourceContext(doc);
      return snippet;
    }
    private Expression Translate(CodeThisReferenceExpression expr){
      if (expr == null) return null;
      return new This();
    }
    private Expression Translate(CodeTypeOfExpression expr){
      if (expr == null) return null;
      return new UnaryExpression(this.Translate(expr.Type), NodeType.Typeof);
    }
    private Expression Translate(CodeTypeReferenceExpression expr){
      if (expr == null) return null;
      return this.Translate(expr.Type);
    }
    private MemberBinding Translate(CodeTypeReference expr){
      if (expr == null) return null;
      return new MemberBinding(null, this.TranslateToTypeNode(expr));
    }
    private Expression Translate(CodeVariableReferenceExpression expr){
      if (expr == null) return null;
      return Identifier.For(expr.VariableName);
    }
    private Namespace Translate(CodeNamespace cNamespace){
      if (cNamespace == null) return null;
      Namespace ns = new Namespace(Identifier.For(cNamespace.Name));
      ns.Types = this.Translate(cNamespace.Types, ns.Name);
      ns.UsedNamespaces = this.Translate(cNamespace.Imports);
      return ns;
    }
    private TypeNodeList Translate(CodeTypeDeclarationCollection typeDecs, Identifier nameSpace){
      int n = typeDecs == null ? 0 : typeDecs.Count;
      TypeNodeList types = new TypeNodeList(n);
      for (int i = 0; i < n; i++)
        types.Add(this.Translate(typeDecs[i], nameSpace, null));
      return types;
    }
    private UsedNamespaceList Translate(CodeNamespaceImportCollection imports){
      int n = imports == null ? 0 : imports.Count;
      UsedNamespaceList usedNspaces = new UsedNamespaceList(n);
      for (int i = 0; i < n; i++)
        usedNspaces.Add(this.Translate(imports[i]));
      return usedNspaces;
    }
    private UsedNamespace Translate(CodeNamespaceImport nsImport){
      if (nsImport == null) return null;
      UsedNamespace usedNS = new UsedNamespace();
      usedNS.Namespace = Identifier.For(nsImport.Namespace);
      return usedNS;
    }
    private TypeNode Translate(CodeTypeDeclaration typeDec, Identifier nameSpace, TypeNode declaringType){
      if (typeDec == null) return null;
      if (typeDec.IsClass){
        CodeTypeDelegate d = typeDec as CodeTypeDelegate;
        if (d != null)
          return this.TranslateToDelegate(d, nameSpace, declaringType);
        else
          return this.TranslateToClass(typeDec, nameSpace, declaringType);
      }
      if (typeDec.IsEnum) return this.TranslateToEnum(typeDec, nameSpace, declaringType);
      if (typeDec.IsInterface) return this.TranslateToInterface(typeDec, nameSpace, declaringType);
      if (typeDec.IsStruct) return this.TranslateToStruct(typeDec, nameSpace, declaringType);
      throw new ArgumentException("Unknown CodeTypeDeclaration");
    }
    private void SetTypeFlags(TypeNode t, System.Reflection.TypeAttributes attrs){
      Debug.Assert(t != null);
      switch(attrs&System.Reflection.TypeAttributes.VisibilityMask){
        case System.Reflection.TypeAttributes.NestedAssembly: t.Flags |= TypeFlags.NestedAssembly; break;
        case System.Reflection.TypeAttributes.NestedFamily: t.Flags |= TypeFlags.NestedFamily; break;
        case System.Reflection.TypeAttributes.NestedFamANDAssem: t.Flags |= TypeFlags.NestedFamANDAssem; break;
        case System.Reflection.TypeAttributes.NestedFamORAssem: t.Flags |= TypeFlags.NestedFamORAssem; break;
        case System.Reflection.TypeAttributes.NestedPublic: t.Flags |= TypeFlags.NestedPublic; break;
        case System.Reflection.TypeAttributes.NestedPrivate: t.Flags |= TypeFlags.NestedPrivate; break;
        case System.Reflection.TypeAttributes.Public: t.Flags |= TypeFlags.Public; break;
        default: t.Flags |= TypeFlags.NotPublic; break;
      }
      switch(attrs&System.Reflection.TypeAttributes.LayoutMask){
        case System.Reflection.TypeAttributes.AutoLayout: t.Flags |= TypeFlags.AutoLayout; break;
        case System.Reflection.TypeAttributes.ExplicitLayout: t.Flags |= TypeFlags.ExplicitLayout; break;
        case System.Reflection.TypeAttributes.SequentialLayout: t.Flags |= TypeFlags.SequentialLayout; break;
      }
      switch(attrs&System.Reflection.TypeAttributes.StringFormatMask){
        case System.Reflection.TypeAttributes.AnsiClass: t.Flags |= TypeFlags.AnsiClass; break;
        case System.Reflection.TypeAttributes.AutoClass: t.Flags |= TypeFlags.AutoClass; break;
        case System.Reflection.TypeAttributes.UnicodeClass: t.Flags |= TypeFlags.UnicodeClass; break;
      }
      if ((attrs&System.Reflection.TypeAttributes.Abstract) != 0) t.Flags |= TypeFlags.Abstract;
      if ((attrs&System.Reflection.TypeAttributes.BeforeFieldInit) != 0) t.Flags |= TypeFlags.BeforeFieldInit;
      if ((attrs&System.Reflection.TypeAttributes.HasSecurity) != 0) t.Flags |= TypeFlags.HasSecurity;
      if ((attrs&System.Reflection.TypeAttributes.Import) != 0) t.Flags |= TypeFlags.Import;
      if ((attrs&System.Reflection.TypeAttributes.RTSpecialName) != 0) t.Flags |= TypeFlags.RTSpecialName;
      if ((attrs&System.Reflection.TypeAttributes.Sealed) != 0) t.Flags |= TypeFlags.Sealed;
      if ((attrs&System.Reflection.TypeAttributes.Serializable) != 0) t.Flags |= TypeFlags.Serializable;    
      if ((attrs&System.Reflection.TypeAttributes.SpecialName) != 0) t.Flags |= TypeFlags.SpecialName;
    }
    private Class TranslateToClass(CodeTypeDeclaration typeDec, Identifier nameSpace, TypeNode declaringType){
      Debug.Assert(typeDec != null);
      Class c = new Class();
      c.Attributes = this.Translate(typeDec.CustomAttributes, null);
      c.DeclaringModule = this.targetModule;
      if (declaringType == null) this.targetModule.Types.Add(c);
      c.DeclaringType = declaringType;
      c.Members = new MemberList();
      this.Translate(typeDec.Members, c);
      c.Name = Identifier.For(typeDec.Name);
      c.Namespace = nameSpace;
      this.SetTypeFlags(c, typeDec.TypeAttributes);
      c.Interfaces = this.TranslateToInterfaceList(typeDec.BaseTypes);
      MemberList constructors = c.GetConstructors();
      if (constructors.Count == 0){
        //Add default constructor
        QualifiedIdentifier supCons = new QualifiedIdentifier(new Base(), StandardIds.Ctor);
        MethodCall superConstructorCall = new MethodCall(supCons, new ExpressionList(0), NodeType.Call);
        StatementList body = new StatementList(2);
        body.Add(new ExpressionStatement(superConstructorCall));
        InstanceInitializer cons = new InstanceInitializer(c, null, new ParameterList(0), new Block(body));
        cons.CallingConvention = CallingConventionFlags.HasThis;
        cons.Flags |= MethodFlags.Public;
        c.Members.Add(cons);
      }
      if (declaringType != null) declaringType.Members.Add(c);
      return c;
    }
    private DelegateNode TranslateToDelegate(CodeTypeDelegate typeDec, Identifier nameSpace, TypeNode declaringType){
      Debug.Assert(typeDec != null);
      DelegateNode d = new DelegateNode();
      d.Attributes = this.Translate(typeDec.CustomAttributes, null);
      d.DeclaringModule = this.targetModule;
      d.DeclaringType = declaringType;
      d.Name = Identifier.For(typeDec.Name);
      d.Namespace = nameSpace;
      d.Parameters = this.Translate(typeDec.Parameters);
      d.ReturnType = this.TranslateToTypeNode(typeDec.ReturnType);
      this.SetTypeFlags(d, typeDec.TypeAttributes);
      d.Flags |= TypeFlags.Sealed;
      if (declaringType != null) declaringType.Members.Add(d);
      return d;
    }
    private EnumNode TranslateToEnum(CodeTypeDeclaration typeDec, Identifier nameSpace, TypeNode declaringType){
      Debug.Assert(typeDec != null);
      EnumNode e = new EnumNode();
      e.Attributes = this.Translate(typeDec.CustomAttributes, null);
      e.DeclaringModule = this.targetModule;
      if (declaringType == null) this.targetModule.Types.Add(e);
      e.DeclaringType = declaringType;
      e.Name = Identifier.For(typeDec.Name);
      e.Namespace = nameSpace;
      TypeNode underlyingType = SystemTypes.Int32;
      this.SetTypeFlags(e, typeDec.TypeAttributes);
      if (typeDec.BaseTypes != null && typeDec.BaseTypes.Count > 0){
        MemberBinding texpr = this.Translate(typeDec.BaseTypes[0]);
        if (texpr != null && texpr.BoundMember is TypeNode)
          underlyingType = (TypeNode)texpr.BoundMember;
      }
      e.Members = new MemberList();
      e.UnderlyingType = underlyingType;
      this.Translate(typeDec.Members, e);
      if (declaringType != null) declaringType.Members.Add(e);
      return e;
    }
    private Interface TranslateToInterface(CodeTypeDeclaration typeDec, Identifier nameSpace, TypeNode declaringType){
      Interface iface = new Interface();
      iface.Attributes = this.Translate(typeDec.CustomAttributes, null);
      iface.DeclaringModule = this.targetModule;
      if (declaringType == null) this.targetModule.Types.Add(iface);
      iface.DeclaringType = declaringType;
      iface.Interfaces = this.TranslateToInterfaceList(typeDec.BaseTypes);
      iface.Members = new MemberList();
      this.Translate(typeDec.Members, iface);
      iface.Name = Identifier.For(typeDec.Name);
      iface.Namespace = nameSpace;
      this.SetTypeFlags(iface, typeDec.TypeAttributes);
      if (declaringType != null) declaringType.Members.Add(iface);
      return iface;
    }
    private Interface TranslateToInterface(CodeTypeReference expr){
      if (expr == null) return null;
      if (expr.ArrayElementType != null) throw new ArgumentException("Not an interface");
      return new InterfaceExpression(this.TranslateToSimpleIdentifierOrQualifiedIdentifier(expr.BaseType));
    }
    private InterfaceList TranslateToInterfaceList(CodeTypeReferenceCollection interfaces){
      int n = interfaces == null ? 0 : interfaces.Count;
      InterfaceList interfaceList = new InterfaceList(n);
      for (int i = 0; i < n; i++)
        interfaceList.Add(this.TranslateToInterface(interfaces[i]));
      return interfaceList;
    }
    private Struct TranslateToStruct(CodeTypeDeclaration typeDec, Identifier nameSpace, TypeNode declaringType){
      Struct s = new Struct();
      s.Attributes = this.Translate(typeDec.CustomAttributes, null);
      s.DeclaringModule = this.targetModule;
      if (declaringType == null) this.targetModule.Types.Add(s);
      s.DeclaringType = declaringType;
      s.Interfaces = this.TranslateToInterfaceList(typeDec.BaseTypes);
      s.Members = new MemberList();
      this.Translate(typeDec.Members, s);
      s.Name = Identifier.For(typeDec.Name);
      s.Namespace = nameSpace;
      this.SetTypeFlags(s, typeDec.TypeAttributes);
      if (declaringType != null) declaringType.Members.Add(s);
      return s;
    }
    private TypeNode TranslateToTypeNode(CodeTypeReference expr){
      if (expr == null) return null;
      if (expr.ArrayElementType != null)
        return new ArrayTypeExpression(this.TranslateToTypeNode(expr.ArrayElementType), expr.ArrayRank);
      return new TypeExpression(this.TranslateToSimpleIdentifierOrQualifiedIdentifier(expr.BaseType));
    }
    private Expression TranslateToSimpleIdentifierOrQualifiedIdentifier(string dottedName){
      if (dottedName == null) return Identifier.Empty;
      int dotPos = dottedName.LastIndexOf('.');
      if (dotPos > 0){
        Expression ns = TranslateToSimpleIdentifierOrQualifiedIdentifier(dottedName.Substring(0, dotPos));
        Identifier id = Identifier.For(dottedName.Substring(dotPos+1));
        return new QualifiedIdentifier(ns, id);
      }else
        return Identifier.For(dottedName);
    }
    private TypeNodeList Translate(CodeTypeReferenceCollection types){
      int n = types == null ? 0 : types.Count;
      TypeNodeList typeList = new TypeNodeList(n);
      for (int i = 0; i < n; i++)
        typeList.Add(this.TranslateToTypeNode(types[i]));
      return typeList;
    }
    private void Translate(CodeTypeMemberCollection members, TypeNode declaringType){
      int n = members == null ? 0 : members.Count;
      for (int i = 0; i < n; i++)
        this.Translate(members[i], declaringType);
    }
    private void Translate(CodeTypeMember member, TypeNode declaringType){
      if (member == null) return;
      else if (member is CodeMemberEvent) this.Translate((CodeMemberEvent)member, declaringType);
      else if (member is CodeMemberField) this.Translate((CodeMemberField)member, declaringType);
      else if (member is CodeMemberMethod) this.Translate((CodeMemberMethod)member, declaringType);
      else if (member is CodeMemberProperty) this.Translate((CodeMemberProperty)member, declaringType);
      else if (member is CodeSnippetTypeMember) this.Translate((CodeSnippetTypeMember)member, declaringType);
      else if (member is CodeTypeDeclaration) this.Translate((CodeTypeDeclaration)member, null, declaringType);
      else throw new ArgumentException("unknown type member", member.GetType().FullName);
    }
    private void SetMethodFlags(MemberAttributes attrs, Method m){
      switch(attrs&MemberAttributes.AccessMask){
        case MemberAttributes.Assembly: m.Flags |= MethodFlags.Assembly; break;
        case MemberAttributes.Family: m.Flags |= MethodFlags.Family; break;
        case MemberAttributes.FamilyAndAssembly: m.Flags |= MethodFlags.FamANDAssem; break;
        case MemberAttributes.FamilyOrAssembly: m.Flags |= MethodFlags.FamORAssem; break;
        case MemberAttributes.Public: m.Flags |= MethodFlags.Public; break;
        default: m.Flags |= MethodFlags.Private; break;
      }
      switch(attrs&MemberAttributes.ScopeMask){
        case MemberAttributes.Abstract: m.Flags |= MethodFlags.Abstract|MethodFlags.Virtual; break;
        case MemberAttributes.Final: break;
        case MemberAttributes.Static: m.Flags |= MethodFlags.Static; break;
        case MemberAttributes.Override: m.Flags |= MethodFlags.ReuseSlot|MethodFlags.Virtual; break;
        default: m.Flags |= MethodFlags.Virtual; break; //TODO: figure out how a non virtual instance method is encoded
      }
      if ((attrs&MemberAttributes.New) != 0) m.Flags |= MethodFlags.NewSlot;
      if (!m.IsStatic) m.CallingConvention = CallingConventionFlags.HasThis;
    }
    private void Translate(CodeMemberEvent cmEvent, TypeNode declaringType){
      if (cmEvent == null) return;
      Event e = new Event();
      e.Attributes = this.Translate(cmEvent.CustomAttributes, null);
      e.DeclaringType = declaringType;
      Method m = new Method(); this.SetMethodFlags(cmEvent.Attributes, m);
      e.HandlerFlags = m.Flags|MethodFlags.SpecialName;
      e.HandlerType = this.TranslateToTypeNode(cmEvent.Type);
      e.Name = Identifier.For(cmEvent.Name);
      if (cmEvent.PrivateImplementationType != null){
        e.ImplementedTypes = new TypeNodeList(1);
        e.ImplementedTypes.Add(this.TranslateToTypeNode(cmEvent.PrivateImplementationType));
      }else if (cmEvent.ImplementationTypes != null)
        e.ImplementedTypes = this.Translate(cmEvent.ImplementationTypes);
      declaringType.Members.Add(e);
    }
    private void Translate(CodeMemberField field, TypeNode declaringType){
      if (field == null) return;
      Field f = new Field();
      f.Attributes = this.Translate(field.CustomAttributes, null);
      f.DeclaringType = declaringType;
      f.Name = Identifier.For(field.Name);
      EnumNode eType = declaringType as EnumNode;
      if (eType != null){
        f.Type = eType;
        if (field.InitExpression != null)
          f.DefaultValue = this.Translate(field.InitExpression) as Literal;
        //TODO: if (f.DefaultValue == null) make it one more than the last literal field
        f.Flags = FieldFlags.Literal|FieldFlags.Static|FieldFlags.HasDefault;
      }else{
        f.Type = this.TranslateToTypeNode(field.Type);
        if (field.InitExpression != null)
          f.Initializer = this.Translate(field.InitExpression);
        if ((field.Attributes&MemberAttributes.Const) == MemberAttributes.Const) f.Flags |= FieldFlags.Literal;
        else if ((field.Attributes&MemberAttributes.Static) == MemberAttributes.Static) f.Flags |= FieldFlags.Static;
      }
      switch(field.Attributes&MemberAttributes.AccessMask){
        case MemberAttributes.Assembly: f.Flags |= FieldFlags.Assembly; break;
        case MemberAttributes.Family: f.Flags |= FieldFlags.Family; break;
        case MemberAttributes.FamilyAndAssembly: f.Flags |= FieldFlags.FamANDAssem; break;
        case MemberAttributes.FamilyOrAssembly: f.Flags |= FieldFlags.FamORAssem; break;
        case MemberAttributes.Public: f.Flags |= FieldFlags.Public; break;
        default: f.Flags |= FieldFlags.Private; break;
      }
      declaringType.Members.Add(f);
    }
    private void Translate(CodeMemberMethod method, TypeNode declaringType){
      if (method == null) return;
      CodeEntryPointMethod ceMethod = method as CodeEntryPointMethod;
      if (ceMethod != null){
        method.Name = "Main";
        method.Attributes = MemberAttributes.Static|MemberAttributes.Public;
      }
      CodeConstructor cons = method as CodeConstructor;
      if (cons != null) {this.Translate(cons, declaringType); return;}
      Method m = new Method();
      m.Attributes = this.Translate(method.CustomAttributes, null);
      m.DeclaringType = declaringType;
      this.SetMethodFlags(method.Attributes, m);
      m.Name = Identifier.For(method.Name);
      m.Parameters = this.Translate(method.Parameters);
      m.ReturnAttributes = this.Translate(method.ReturnTypeCustomAttributes, null);
      m.ReturnType = this.TranslateToTypeNode(method.ReturnType);
      if (method.PrivateImplementationType != null){
        m.ImplementedTypes = new TypeNodeList(1);
        m.ImplementedTypes.Add(this.TranslateToTypeNode(method.PrivateImplementationType));
      }else if (method.ImplementationTypes != null)
        m.ImplementedTypes = this.Translate(method.ImplementationTypes);
      if (declaringType.NodeType != NodeType.Interface || m.IsStatic)
        m.Body = new Block(this.Translate(method.Statements));
      declaringType.Members.Add(m);
    }
    private void Translate(CodeConstructor cons, TypeNode declaringType){
      InstanceInitializer c = new InstanceInitializer();
      c.Attributes = this.Translate(cons.CustomAttributes, null);
      c.DeclaringType = declaringType;
      this.SetMethodFlags(cons.Attributes, c);
      c.Flags |= MethodFlags.SpecialName|MethodFlags.RTSpecialName;
      c.Parameters = this.Translate(cons.Parameters);
      StatementList statements = this.Translate(cons.Statements);
      int n = statements.Count;
      StatementList stats = new StatementList(n+1);
      if (cons.ChainedConstructorArgs != null && cons.ChainedConstructorArgs.Count > 0)
        stats.Add(new ExpressionStatement(new MethodCall(new QualifiedIdentifier(new This(), StandardIds.Ctor), this.Translate(cons.ChainedConstructorArgs), NodeType.Call)));
      else if (cons.BaseConstructorArgs != null && cons.BaseConstructorArgs.Count > 0)
        stats.Add(new ExpressionStatement(new MethodCall(new QualifiedIdentifier(new Base(), StandardIds.Ctor), this.Translate(cons.BaseConstructorArgs), NodeType.Call)));
      else
        stats.Add(new ExpressionStatement(new MethodCall(new QualifiedIdentifier(new Base(), StandardIds.Ctor), new ExpressionList(0), NodeType.Call)));
      for (int i = 0; i < n; i++) stats.Add(statements[i]);
      statements = stats;
      c.Body = new Block(statements);      
      declaringType.Members.Add(c);
    }
    private void Translate(CodeMemberProperty property, TypeNode declaringType){
      if (property == null) return;
      Property p = new Property();
      p.Attributes = this.Translate(property.CustomAttributes, null);
      p.DeclaringType = declaringType;
      p.Name = Identifier.For(property.Name);
      if (property.PrivateImplementationType != null){
        p.ImplementedTypes = new TypeNodeList(1);
        p.ImplementedTypes.Add(this.TranslateToTypeNode(property.PrivateImplementationType));
      }else if (property.ImplementationTypes != null)
        p.ImplementedTypes = this.Translate(property.ImplementationTypes);
      ParameterList parameters = p.Parameters = this.Translate(property.Parameters);
      TypeNode propertyType = p.Type = this.TranslateToTypeNode(property.Type);
      if (property.HasGet){
        Method getter = p.Getter = new Method();
        getter.ImplementedTypes = p.ImplementedTypes;
        this.SetMethodFlags(property.Attributes, getter);
        getter.Flags |= MethodFlags.SpecialName;
        getter.Name = Identifier.For("get_"+property.Name);
        getter.Parameters = parameters;
        getter.ReturnType = propertyType;
        getter.DeclaringType = declaringType;
        if (declaringType.NodeType != NodeType.Interface || getter.IsStatic)
          getter.Body = new Block(this.Translate(property.GetStatements));
        declaringType.Members.Add(getter);
      }
      if (property.HasSet){
        ParameterList setterPars = parameters;
        if (property.HasGet){
          int n = parameters.Count;
          setterPars = new ParameterList(n+1);
          for (int i = 0; i < n; i++) setterPars.Add(parameters[i]);
        }
        Parameter valuePar = new Parameter();
        valuePar.Name = StandardIds.Value;
        valuePar.Type = propertyType;
        setterPars.Add(valuePar);

        Method setter = p.Setter = new Method();
        setter.ImplementedTypes = p.ImplementedTypes;
        this.SetMethodFlags(property.Attributes, setter);
        setter.Flags |= MethodFlags.SpecialName;
        setter.Name = Identifier.For("set_"+property.Name);
        setter.Parameters = setterPars;
        setter.ReturnType = SystemTypes.Void;
        setter.DeclaringType = declaringType;
        if (declaringType.NodeType != NodeType.Interface || setter.IsStatic)
          setter.Body = new Block(this.Translate(property.SetStatements));
        declaringType.Members.Add(setter);
      }
      declaringType.Members.Add(p);
    }
    private ParameterList Translate(CodeParameterDeclarationExpressionCollection parameters){
      int n = parameters == null ? 0 : parameters.Count;
      ParameterList parameterList = new ParameterList(n);
      for (int i = 0; i < n; i++)
        parameterList.Add(this.Translate(parameters[i]));
      return parameterList;
    }
    private Parameter Translate(CodeParameterDeclarationExpression parameter){
      if (parameter == null) return null;
      Parameter p = new Parameter();
      p.Attributes = this.Translate(parameter.CustomAttributes, null);
      p.Name = Identifier.For(parameter.Name);
      p.Type = this.TranslateToTypeNode(parameter.Type);
      if (parameter.Direction != FieldDirection.In)
        p.Type = new ReferenceTypeExpression(p.Type);
      return p;
    }
    private void Translate(CodeSnippetTypeMember tmSnippet, TypeNode declaringType){
      if (tmSnippet == null) return;
      TypeMemberSnippet snippet = new TypeMemberSnippet();
      snippet.DeclaringType = declaringType;
      Document doc = null;
      if (tmSnippet.LinePragma == null)
        doc = this.compiler.CreateDocument(null, 1, tmSnippet.Text);
      else
        doc = this.compiler.CreateDocument(tmSnippet.LinePragma.FileName, tmSnippet.LinePragma.LineNumber, tmSnippet.Text);
      snippet.SourceContext = new SourceContext(doc);
      declaringType.Members.Add(snippet);
    }
    private StatementList Translate(CodeStatementCollection statements){
      int n = statements == null ? 0 : statements.Count;
      StatementList statementList = new StatementList(n);
      for (int i = 0; i < n; i++){
        Statement s = this.Translate(statements[i]);
        if (s != null) statementList.Add(s);
      }
      return statementList;
    }
    private Statement Translate(CodeStatement statement){
      if (statement == null) return null;
      if (statement is CodeAssignStatement) return this.Translate((CodeAssignStatement)statement);
      if (statement is CodeAttachEventStatement) return this.Translate((CodeAttachEventStatement)statement);
      if (statement is CodeCommentStatement) return this.Translate((CodeCommentStatement)statement);
      if (statement is CodeConditionStatement) return this.Translate((CodeConditionStatement)statement);
      if (statement is CodeExpressionStatement) return this.Translate((CodeExpressionStatement)statement);
      if (statement is CodeGotoStatement) return this.Translate((CodeGotoStatement)statement);
      if (statement is CodeIterationStatement) return this.Translate((CodeIterationStatement)statement);
      if (statement is CodeLabeledStatement) return this.Translate((CodeLabeledStatement)statement);
      if (statement is CodeMethodReturnStatement) return this.Translate((CodeMethodReturnStatement)statement);
      if (statement is CodeRemoveEventStatement) return this.Translate((CodeRemoveEventStatement)statement);
      if (statement is CodeSnippetStatement) return this.Translate((CodeSnippetStatement)statement);
      if (statement is CodeThrowExceptionStatement) return this.Translate((CodeThrowExceptionStatement)statement);
      if (statement is CodeTryCatchFinallyStatement) return this.Translate((CodeTryCatchFinallyStatement)statement);
      if (statement is CodeVariableDeclarationStatement) return this.Translate((CodeVariableDeclarationStatement)statement);
      throw new ArgumentException("unknown statement", statement.GetType().FullName);
    }
    private Statement Translate(CodeAssignStatement statement){
      if (statement == null) return null;
      AssignmentStatement aStatement = new AssignmentStatement();
      aStatement.Operator = NodeType.Nop;
      aStatement.Target = this.Translate(statement.Left);
      aStatement.Source = this.Translate(statement.Right);
      return aStatement;
    }
    private Statement Translate(CodeAttachEventStatement statement){
      if (statement == null) return null;
      Expression e = this.Translate(statement.Event);
      Expression h = this.Translate(statement.Listener);
      return new ExpressionStatement(new BinaryExpression(e, h, NodeType.AddEventHandler));
    }
    private Statement Translate(CodeCommentStatement statement){
      return null;
    }
    private Statement Translate(CodeConditionStatement statement){
      if (statement == null) return null;
      return new If(this.Translate(statement.Condition),
        new Block(this.Translate(statement.TrueStatements)), 
        new Block(this.Translate(statement.FalseStatements)));
    }
    private Statement Translate(CodeExpressionStatement statement){
      if (statement == null) return null;
      return new ExpressionStatement(this.Translate(statement.Expression));
    }
    private Statement Translate(CodeGotoStatement statement){
      if (statement == null) return null;
      return new Goto(Identifier.For(statement.Label));
    }
    private Expression Translate(CodeIndexerExpression expr){
      if (expr == null) return null;
      Indexer indexer = new Indexer();
      indexer.Object = this.Translate(expr.TargetObject);
      indexer.Operands = this.Translate(expr.Indices);
      return indexer;
    }
    private Statement Translate(CodeIterationStatement statement){
      if (statement == null) return null;
      StatementList initializers = new StatementList(1);
      initializers.Add(this.Translate(statement.InitStatement));
      Expression test = this.Translate(statement.TestExpression);
      StatementList incrementers = new StatementList(1);
      incrementers.Add(this.Translate(statement.IncrementStatement));
      Block body = new Block(this.Translate(statement.Statements));
      return new For(initializers, test, incrementers, body);
    }
    private Statement Translate(CodeLabeledStatement statement){
      if (statement == null) return null;
      LabeledStatement ls = new LabeledStatement();
      ls.Label = Identifier.For(statement.Label);
      ls.Statement = this.Translate(statement.Statement);
      return ls;
    }
    private Statement Translate(CodeMethodReturnStatement statement){
      if (statement == null) return null;
      return new Return(this.Translate(statement.Expression));
    }
    private Statement Translate(CodeRemoveEventStatement statement){
      if (statement == null) return null;
      Expression e = this.Translate(statement.Event);
      Expression h = this.Translate(statement.Listener);
      return new ExpressionStatement(new BinaryExpression(e, h, NodeType.RemoveEventHandler));
    }
    private Statement Translate(CodeSnippetStatement statement){
      if (statement == null) return null;
      StatementSnippet snippet = new StatementSnippet();
      Document doc = null;
      if (statement.LinePragma == null)
        doc = this.compiler.CreateDocument(null, 1, statement.Value);
      else
        doc = this.compiler.CreateDocument(statement.LinePragma.FileName, statement.LinePragma.LineNumber, statement.Value);
      snippet.SourceContext = new SourceContext(doc, 0, doc.Text.Length);
      return snippet;
    }
    private Statement Translate(CodeThrowExceptionStatement statement){
      if (statement == null) return null;
      return new Throw(this.Translate(statement.ToThrow));
    }
    private Statement Translate(CodeTryCatchFinallyStatement statement){
      if (statement == null) return null;
      Try Try = new Try();
      Try.TryBlock = new Block(this.Translate(statement.TryStatements));
      Try.Catchers = this.Translate(statement.CatchClauses);
      if (statement.FinallyStatements != null && statement.FinallyStatements.Count > 0)
        Try.Finally = new Finally(new Block(this.Translate(statement.FinallyStatements)));
      return Try;
    }
    private CatchList Translate(CodeCatchClauseCollection catchers){
      if (catchers == null) return null;
      int n = catchers.Count;
      CatchList catchList = new CatchList(n);
      for (int i = 0; i < n; i++)
        catchList.Add(this.Translate(catchers[i]));
      return catchList;
    }
    private Catch Translate(CodeCatchClause catcher){
      if (catcher == null) return null;
      Catch c = new Catch();
      c.Block = new Block(this.Translate(catcher.Statements));
      c.Type = this.TranslateToTypeNode(catcher.CatchExceptionType);
      c.Variable = Identifier.For(catcher.LocalName);
      return c;
    }
    private Statement Translate(CodeVariableDeclarationStatement statement){
      if (statement == null) return null;
      return new VariableDeclaration(Identifier.For(statement.Name), this.TranslateToTypeNode(statement.Type), this.Translate(statement.InitExpression));
    }
    private void HandleError(Error error, string argument){
      this.errorNodes.Add(new CommonErrorNode(error, argument));
    }
   }
}