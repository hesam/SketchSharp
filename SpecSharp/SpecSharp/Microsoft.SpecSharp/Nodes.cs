//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
#if CCINamespace
using Microsoft.Cci;
#else
using System.Compiler;
#endif
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Microsoft.SpecSharp{

  public enum SpecSharpNodeType{
    None = 6000,
    KeywordList,
    XamlSnippet,
  }

  public class AnonymousNestedDelegate : AnonymousNestedFunction {
    public AnonymousNestedDelegate(ParameterList parameters, Block body, SourceContext sctx)
      : base(parameters, body, sctx) {
    }
  }

  /// <summary>
  /// Only used by the Language service to populate completion lists with "members" representing keywords.
  /// </summary>
  public class KeywordCompletion : Field{
    public KeywordCompletion(string keyword)
      : base(Identifier.For(keyword)){
    }
  }
  /// <summary>
  /// A pseudo member that is inserted into the AST by the parser in order to instruct Looker to provide the language service with a list of keywords
  /// for a completion list at the source location represented by the SourceContext of this node.
  /// </summary>
  public class KeywordCompletionList : Field{
    public KeywordCompletion[] KeywordCompletions;
    public KeywordCompletionList(Identifier/*!*/ identifierToComplete, params KeywordCompletion[] keywordCompletions) 
      : base(identifierToComplete){
      this.SourceContext = identifierToComplete.SourceContext;
      this.NodeType = (NodeType)SpecSharpNodeType.KeywordList;
      this.KeywordCompletions = keywordCompletions;
    }
  }
  /// <summary>
  /// Only used during parsing of documentation comments. Does not appear in Spec# AST.
  /// </summary>
  public class LiteralElement : Expression{
    public Identifier Name;
    public IdentifierList AttributeNames;
    public ExpressionList AttributeValues;
    public ExpressionList Contents; //Not just elements, but also comments, strings, CharacterData and Processing Instructions
    public Int32List ContentsType; //The kind of token corresponding to this element    
    public BlockExpression Code; // the actual code the literal element is compiled down to.
    public MemberBinding BoundMember;
    public LiteralElement ParentLiteral;
    public MemberList Completions;

    public LiteralElement()
      : base((NodeType)SpecSharpNodeType.None){
      this.AttributeNames = new IdentifierList();
      this.AttributeValues = new ExpressionList();
      this.Contents = new ExpressionList();
      this.ContentsType = new Int32List();
    }
  }
  /// <summary>
  /// Only used during parsing of documentation comments. Does not appear in Spec# AST.
  /// </summary>
  public class WhitespaceLiteral : Literal{
    public WhitespaceLiteral(object value, TypeNode type, SourceContext sourceContext)
      : base(value, type, sourceContext) {
    }
  }

  /// <summary>
  /// Special subclass type so we can specialize a path in the kernel.
  /// </summary>
  internal abstract class ModifiesClause : MethodCall {
    public static Expression QualifiedIdentifierFor(params string[] memberNameParts) {
      if (memberNameParts == null || memberNameParts.Length < 1) return null;
      Identifier id = Identifier.For(memberNameParts[0]);
      id.Prefix = Identifier.For("global");
      Expression qualId = id;
      for (int i = 1, n = memberNameParts.Length; i < n; i++)
        qualId = new QualifiedIdentifier(qualId, Identifier.For(memberNameParts[i]));
      return qualId;
    }

    protected ModifiesClause(Expression qualifiedId, Expression expression, SourceContext sctx)
      : base(qualifiedId, new ExpressionList(expression), NodeType.Call, null, sctx) {
    }
  }

  internal class ModifiesObjectClause : ModifiesClause {
    public ModifiesObjectClause(Expression expression, SourceContext sctx)
      : base(QualifiedIdentifierFor("Microsoft", "Contracts", "Guard", "ModifiesObject"), expression, sctx) {
    }
  }

  internal class ModifiesNothingClause : ModifiesClause {
    public ModifiesNothingClause(Expression expression, SourceContext sctx)
      : base(QualifiedIdentifierFor("Microsoft", "Contracts", "Guard", "ModifiesNothing"), expression, sctx) {
    }
  }

  internal class ModifiesArrayClause : ModifiesClause {
    public ModifiesArrayClause(Expression expression, SourceContext sctx)
      : base(QualifiedIdentifierFor("Microsoft", "Contracts", "Guard", "ModifiesArray"), expression, sctx) {
    }

  }

  internal class ModifiesPeersClause : ModifiesClause {
    public ModifiesPeersClause(Expression expression, SourceContext sctx)
      : base(QualifiedIdentifierFor("Microsoft", "Contracts", "Guard", "ModifiesPeers"), expression, sctx) {
    }

  }


#if Xaml
  /// <summary>
  /// A snippet of Xaml code to be compiled by the Xaml compiler.
  /// </summary>
  public class XamlSnippet : Node{
    public Module CodeModule;
    public Microsoft.XamlCompiler.ErrorHandler ErrorHandler;
    public CompilerOptions Options;
    public IParserFactory ParserFactory;
    public Document XamlDocument;

    public XamlSnippet()
      : base((NodeType)SpecSharpNodeType.XamlSnippet){
    }
  }
#endif
}