//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
#if CCINamespace
using Microsoft.Cci;
using Cci = Microsoft.Cci;
#else
using System.Compiler;
using Cci = System.Compiler;
#endif
using System;
using System.Collections;
using System.CodeDom.Compiler;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.XPath;

namespace Microsoft.SpecSharp{
  public sealed class ParserFactory : IParserFactory{
    public IParser CreateParser(string fileName, int lineNumber, DocumentText text, Module symbolTable, ErrorNodeList errorNodes, CompilerParameters options){
      Document document = Compiler.CreateSpecSharpDocument(fileName, lineNumber, text);
      return new Parser(document, errorNodes, symbolTable, options as SpecSharpCompilerOptions);
    }
  }
  public class Parser : IParser{
    private Token currentToken;
    private Scanner scanner;
    private ErrorNodeList errors;
    private AuthoringSink sink;
    private bool insideBlock;
    private bool insideCheckedBlock;
    private bool insideUncheckedBlock;
    private bool insideModifiesClause;
    private bool compatibilityOn;
    private bool allowUnsafeCode;
    private bool inUnsafeCode;
    enum BaseOrThisCallKind {Disallowed, None, ColonThisOrBaseSeen, InCtorBodyBaseSeen, InCtorBodyThisSeen}
    private BaseOrThisCallKind inInstanceConstructor = BaseOrThisCallKind.Disallowed;
    private InstanceInitializer currentCtor;
    internal bool omitBodies;
    private bool unmatchedTry;
    private bool sawReturnOrYield = false;
    private bool useGenerics = TargetPlatform.UseGenerics;
    private SourceContext compoundStatementOpeningContext;
    private object arrayInitializerOpeningContext;
    private Module module;
    private bool parsingContractAssembly = false;
    internal bool parsingStatement = false;
    private SpecSharpCompilerOptions options;


    private static Guid dummyGuid = new Guid();
    private TypeNode currentTypeNode; //HS D: HACK FIXME
    private static Hashtable definedTypeNodes = new Hashtable(); //HS D: HACK FIXME

    public Parser(Module symbolTable){
      this.module = symbolTable;
    }
    public Parser(Document document, ErrorNodeList errors, Module symbolTable, SpecSharpCompilerOptions options){
      this.scanner = new Scanner(document, errors, options);
      this.ProcessOptions(options);
      this.errors = errors;
      this.module = symbolTable;
    }
    private void ProcessOptions(SpecSharpCompilerOptions options){
      if (options == null) return;
      this.insideCheckedBlock = options.CheckedArithmetic;
      this.insideUncheckedBlock = false;
      this.compatibilityOn = options.Compatibility;
      this.allowUnsafeCode = options.AllowUnsafeCode;
      this.parsingContractAssembly = options.IsContractAssembly;
      if (this.parsingContractAssembly) {
        this.omitBodies = true;
      }
      this.options = options;
    }
    private void GetNextToken(){
      Debug.Assert(this.currentToken != Token.EndOfFile);
      this.currentToken = this.scanner.GetNextToken();
    }
    private Token PeekToken() {
      Debug.Assert(this.currentToken != Token.EndOfFile);
      SourceContext sctx = this.scanner.CurrentSourceContext;
      ScannerState ss = this.scanner.state;
      this.GetNextToken();
      Token tk = this.currentToken;
      this.scanner.endPos = sctx.StartPos;
      this.scanner.state = ss;
      this.currentToken = Token.None;
      this.GetNextToken();
      return tk;
    }
    private Token GetTokenFor(string terminator) {
      Guid dummy = Parser.dummyGuid;
      Document document = new Document(null, 1, terminator, dummy, dummy, dummy);
      Scanner scanner = new Scanner(document, new ErrorNodeList(), null);
      this.currentToken = Token.None;
      return scanner.GetNextToken();
    }
    private XmlElement lastDocCommentBackingField;
    private XmlElement LastDocComment{
      get{
        XmlElement result = this.lastDocCommentBackingField;
        this.lastDocCommentBackingField = null;
        return result;
      }
      set{
        this.lastDocCommentBackingField = value;
      }
    }
    public CompilationUnit ParseCompilationUnit(string source, string fname, CompilerParameters parameters, ErrorNodeList errors, AuthoringSink sink){
      Guid dummy = Parser.dummyGuid;
      Document document = new Document(fname, 1, source, dummy, dummy, dummy);
      this.errors = errors;
      this.scanner = new Scanner(document, errors, parameters as SpecSharpCompilerOptions);
      this.currentToken = Token.None;
      this.errors = errors;
      this.ProcessOptions(parameters as SpecSharpCompilerOptions);
      CompilationUnit cu = new CompilationUnit(Identifier.For(fname));
      cu.Compilation = new Compilation(this.module, new CompilationUnitList(cu), parameters, null);
      cu.SourceContext = new SourceContext(document);
      this.ParseCompilationUnit(cu, false, true, sink);//This overload is only called for intellisense, not the background error check.
      cu.PragmaWarnInformation = this.scanner.pragmaInformation;
      this.errors = null;
      this.scanner = null;
      return cu;
    }
    void IParser.ParseCompilationUnit(CompilationUnit cu){
      this.ParseCompilationUnit(cu, false, this.scanner.ignoreDocComments, null);
    }
    public void ParseCompilationUnit(CompilationUnit cu, bool omitBodies, bool ignoreDocComments){
      this.ParseCompilationUnit(cu, omitBodies, ignoreDocComments, null);
    }
    internal void ParseCompilationUnit(CompilationUnit cu, bool omitBodies, bool ignoreDocComments, AuthoringSink sink){
      if (cu == null) throw new ArgumentNullException("cu");
      this.sink = sink;
      Namespace ns = new Namespace(Identifier.Empty, Identifier.Empty, new AliasDefinitionList(), new UsedNamespaceList(), new NamespaceList(), new TypeNodeList());
      ns.SourceContext = cu.SourceContext;
      cu.Nodes = new NodeList(ns);
      this.omitBodies = omitBodies || this.parsingContractAssembly;
      this.scanner.ignoreDocComments = ignoreDocComments;
      this.scanner.sink = sink;
      this.GetNextToken();
      cu.PreprocessorDefinedSymbols = this.scanner.PreprocessorDefinedSymbols;
    tryAgain:
      this.ParseExternalAliasDirectives(ns, Parser.AttributeOrNamespaceOrTypeDeclarationStart|Token.Using|Token.LastIdentifier|Parser.EndOfFile);
      this.ParseUsingDirectives(ns, Parser.AttributeOrNamespaceOrTypeDeclarationStart|Token.Extern|Token.LastIdentifier|Parser.EndOfFile);
      if (this.currentToken == Token.Extern){
        this.HandleError(Error.ExternAfterElements);
        goto tryAgain;
      }
      this.ParseNamespaceMemberDeclarations(ns, Parser.EndOfFile);
      cu.PragmaWarnInformation = this.scanner.pragmaInformation;
      this.scanner.sink = null;
      this.sink = null;
    }
    Expression IParser.ParseExpression(){
      return this.ParseExpression(0, null, null);
    }
    public Expression ParseExpression(int startPosition, string terminator, AuthoringSink sink){
      this.sink = sink;
      TokenSet followers = Parser.EndOfFile;
      if (terminator != null)
        followers |= this.GetTokenFor(terminator);
      this.scanner.endPos = startPosition;
      this.currentToken = Token.None;
      this.GetNextToken();
      return this.ParseExpression(followers);
    }
    public void ParseMethodBody(Method method, int startPosition, AuthoringSink sink){
      this.sink = sink;
      this.scanner.endPos = startPosition;
      this.currentToken = Token.None;
      this.GetNextToken();
      method.Body.Statements = new StatementList();
      method.Body.Statements.Add(this.ParseBlock(Parser.EndOfFile | Parser.TypeMemberStart | Token.RightBrace | Token.Semicolon));
    }
    void IParser.ParseStatements(StatementList statements){
      this.ParseStatements(statements, 0, null, null);
    }
    public int ParseStatements(StatementList statements, int startColumn, string terminator, AuthoringSink sink){
      this.sink = sink;
      TokenSet followers = Parser.EndOfFile;
      if (terminator != null)
        followers |= this.GetTokenFor(terminator);
      this.scanner.endPos = startColumn;
      this.currentToken = Token.None;
      this.GetNextToken();
      this.ParseStatements(statements, followers);
      return this.scanner.CurrentSourceContext.StartPos;
    }
    void IParser.ParseTypeMembers(TypeNode type){
      this.ParseTypeMembers(type, 0, null, null);
    }
    public int ParseTypeMembers(TypeNode type, int startColumn, string terminator, AuthoringSink sink){
      this.sink = sink;
      TokenSet followers = Parser.EndOfFile;
      if (terminator != null)
        followers |= this.GetTokenFor(terminator);
      this.scanner.endPos = startColumn;
      this.currentToken = Token.None;
      this.GetNextToken();
      this.ParseTypeMembers(type, followers);
      return this.scanner.CurrentSourceContext.StartPos;
    }
    public bool CanStartTypeMember(TypeNode type) {
      if (type == null) return false;
      TokenSet followers = Parser.EndOfFile | Token.RightBrace;
      TypeNode tn = new Class();
      tn.Name = type.Name;
      tn.Members = new MemberList();
      this.ParseTypeMembers(tn, followers);
      return tn.Members.Count != 0;
    }
    private void ParseExternalAliasDirectives(Namespace ns, TokenSet followers){
      while (this.currentToken == Token.Extern)
        this.ParseExternalAliasDirective(ns, followers|Token.Extern);
      this.SkipTo(followers);
    }
    private void ParseExternalAliasDirective(Namespace ns, TokenSet followers){
      Debug.Assert(this.currentToken == Token.Extern);
      SourceContext sctx = this.scanner.CurrentSourceContext;
      this.GetNextToken();
      if (this.currentToken == Token.Alias)
        this.GetNextToken();
      else{
        if (!Parser.IdentifierOrNonReservedKeyword[this.currentToken]){
          this.SkipTo(followers, Error.SyntaxError, "alias");
          return;
        }
        this.HandleError(Error.SyntaxError, "alias");
      }
      if (!Parser.IdentifierOrNonReservedKeyword[this.currentToken]){
        this.SkipTo(followers, Error.ExpectedIdentifier);
        return;
      }
      Identifier id = this.scanner.GetIdentifier();
      this.GetNextToken();
      this.SkipSemiColon(followers);
      sctx.EndPos = this.scanner.endPos;
      AliasDefinition aliasD = new AliasDefinition(id, null, sctx);
      aliasD.AliasedAssemblies = new AssemblyReferenceList();
      ns.AliasDefinitions.Add(aliasD);
    }
    private void ParseUsingDirectives(Namespace ns, TokenSet followers){
      while (this.currentToken == Token.Using)        
        this.ParseUsingDirective(ns, followers|Token.Using);
      this.SkipTo(followers);
    }
    private void ParseUsingDirective(Namespace ns, TokenSet followers){
      Debug.Assert(this.currentToken == Token.Using);
      SourceContext sctx = this.scanner.CurrentSourceContext;
      this.GetNextToken();
      Identifier id = this.scanner.GetIdentifier();
      if (!Parser.IdentifierOrNonReservedKeyword[this.currentToken]) {
        this.SkipTo(followers, Error.ExpectedIdentifier);
        if (this.currentToken == Token.EndOfFile){
          id.SourceContext.StartPos -= 2;
          UsedNamespace usedNS = new UsedNamespace(id, sctx);
          ns.UsedNamespaces.Add(usedNS);
        }
        return;
      }
      this.GetNextToken();
      TokenSet followersOrSemicolon = followers|Token.Semicolon;
      if (this.currentToken == Token.Assign){
        this.GetNextToken();
        Expression aliasedId = this.ParseNamespaceOrTypeName(Identifier.Empty, true, followersOrSemicolon);
        sctx.EndPos = this.scanner.endPos;
        ns.AliasDefinitions.Add(new AliasDefinition(id, aliasedId, sctx));
      }else{
      tryAgain:
        if (this.currentToken == Token.Dot){
          this.GetNextToken();
          id = this.ParseNamespaceName(id, false, followersOrSemicolon);
        }else if (id.Prefix == null && this.currentToken == Token.DoubleColon){
          this.GetNextToken();
          Identifier ident = this.scanner.GetIdentifier();
          this.SkipIdentifierOrNonReservedKeyword();
          ident.Prefix = id;
          ident.SourceContext.StartPos = id.SourceContext.StartPos;
          id = ident;
          goto tryAgain;
        }
        UsedNamespace usedNS = new UsedNamespace(id);
        //TODO: used namespaces should use structured expressions so that they can expand root aliases
        sctx.EndPos = this.scanner.endPos;
        usedNS.SourceContext = sctx;
        ns.UsedNamespaces.Add(usedNS);
      }
      this.SkipSemiColon(followers);
    }
    private Identifier ParseNamespaceName(Identifier parentId, bool allowDoubleColon, TokenSet followers){
      if (!Parser.IdentifierOrNonReservedKeyword[this.currentToken] && (this.sink == null || this.currentToken != Token.EndOfFile)){
        this.SkipTo(followers, Error.ExpectedIdentifier);
        return parentId;
      }
      Identifier id = null;
      if (parentId == Identifier.Empty){
        id = this.scanner.GetIdentifier();
      }else{
        id = new Identifier(parentId.Name + "." + this.scanner.GetIdentifierString());
        id.SourceContext = parentId.SourceContext;
        id.SourceContext.EndPos = this.scanner.endPos;
        id.Prefix = parentId.Prefix;
      }
      if (this.currentToken == Token.EndOfFile) return id;
      this.GetNextToken();
    tryAgain:
      if (this.currentToken != Token.Dot){
        if (id.Prefix == null && this.currentToken == Token.DoubleColon && allowDoubleColon){
          this.GetNextToken();
          Identifier ident = this.scanner.GetIdentifier();
          this.SkipIdentifierOrNonReservedKeyword();
          ident.Prefix = id;
          ident.SourceContext.StartPos = id.SourceContext.StartPos;
          id = ident;
          goto tryAgain;
        }
        this.SkipTo(followers);
        return id;
      }
      this.GetNextToken();
      return this.ParseNamespaceName(id, false, followers);
    }
    private Expression ParseNamespaceOrTypeName(Expression root, bool allowDoubleColon, TokenSet followers){
      if (!Parser.IdentifierOrNonReservedKeyword[this.currentToken]){
        if (this.currentToken == Token.EndOfFile){
          root = new QualifiedIdentifier(root, this.scanner.GetIdentifier(), root.SourceContext);
          root.SourceContext.EndPos = this.scanner.endPos;
        }
        this.SkipTo(followers, Error.ExpectedIdentifier);
        return root;
      }
      Identifier id = this.scanner.GetIdentifier();
      if (root == Identifier.Empty){
        root = id;
      }else{
        root = new QualifiedIdentifier(root, id, root.SourceContext);
        root.SourceContext.EndPos = id.SourceContext.EndPos;
      }
      this.GetNextToken();
    tryAgain:
      if (this.currentToken != Token.Dot){
        if (this.currentToken == Token.DoubleColon && allowDoubleColon){
          Debug.Assert(root == id);
          this.GetNextToken();
          Identifier ident = this.scanner.GetIdentifier();
          this.SkipIdentifierOrNonReservedKeyword();
          ident.Prefix = id;
          ident.SourceContext.StartPos = id.SourceContext.StartPos;
          root = ident;
          allowDoubleColon = false;
          goto tryAgain;
        }
        TemplateInstance result = null;
        while (this.currentToken == Token.LessThan) {
          int endCol, arity;
          if (result == null) {
            result = new TemplateInstance();
            result.Expression = root;
            result.SourceContext = root.SourceContext;
          }
          result.TypeArgumentExpressions = this.ParseTypeArguments(false, false, followers|Token.Dot, out endCol, out arity);
          result.TypeArguments = result.TypeArgumentExpressions == null ? null : result.TypeArgumentExpressions.Clone();
          result.SourceContext.EndPos = endCol;
          if (result.TypeArguments == null) {
            result.TypeArguments = new TypeNodeList(1);
            result.TypeArguments.Add(null);
          }
          if (this.currentToken == Token.Dot) {
            root = this.ParseQualifiedIdentifier(result, followers|Token.LessThan, false);
            result = null;
          }
        }
        if (result != null) root = result;
        this.SkipTo(followers);
        return root;
      }
      this.GetNextToken();
      return this.ParseNamespaceOrTypeName(root, false, followers);
    }
    private void ParseNamespaceMemberDeclarations(Namespace/*!*/ parentNamespace, TokenSet followers){
      TokenSet attributeOrNamespaceOrTypeDeclarationStartOrLastIdentifier = Parser.AttributeOrNamespaceOrTypeDeclarationStart|Token.LastIdentifier;
      TokenSet followersOrAttributeOrNamespaceOrTypeDeclarationStartOrLastIdentifier = followers|attributeOrNamespaceOrTypeDeclarationStartOrLastIdentifier;
      while (attributeOrNamespaceOrTypeDeclarationStartOrLastIdentifier[this.currentToken] || this.currentToken == Token.Identifier)
        this.ParseNamespaceOrTypeDeclarations(parentNamespace, followersOrAttributeOrNamespaceOrTypeDeclarationStartOrLastIdentifier);
      //if (this.sink != null && this.currentToken == Token.EndOfFile){
      //  TypeAlias ta = new TypeAlias(new TypeExpression(new Identifier(" ", this.scanner.CurrentSourceContext)), Identifier.Empty);
      //  parentNamespace.Types.Add(ta);
      //}
      Debug.Assert(followers[this.currentToken]);
    }
    private void ParseNamespaceOrTypeDeclarations(Namespace parentNamespace, TokenSet followers){
      if (this.currentToken == Token.Identifier){
        TypeAlias ta = new TypeAlias(new TypeExpression(this.scanner.GetIdentifier()), Identifier.Empty);
        parentNamespace.Types.Add(ta);
        ta.SourceContext = this.scanner.CurrentSourceContext;
        this.GetNextToken();
        return;
      }
      if (this.currentToken == Token.Private || this.currentToken == Token.Protected){
        this.HandleError(Error.PrivateOrProtectedNamespaceElement);
        this.GetNextToken();
      }
      if (this.currentToken == Token.Namespace){
        SourceContext openContext = this.scanner.CurrentSourceContext;
        this.GetNextToken();
        TokenSet nsFollowers = followers|Token.RightBrace;
        Identifier nsId = this.ParseNamespaceName(Identifier.Empty, false, nsFollowers|Token.LeftBrace);
        Identifier nsFullId = parentNamespace.Name == Identifier.Empty ? nsId : Identifier.For(parentNamespace.FullName+"."+nsId);
        Namespace ns = new Namespace(nsId, nsFullId, new AliasDefinitionList(), new UsedNamespaceList(), new NamespaceList(), new TypeNodeList());
        ns.DeclaringNamespace = parentNamespace;
        ns.SourceContext = openContext;
        if (this.currentToken == Token.LeftBrace) openContext.EndPos = this.scanner.endPos;
        SourceContext nsBodyCtx = this.scanner.CurrentSourceContext;
        this.Skip(Token.LeftBrace);
        parentNamespace.NestedNamespaces.Add(ns);
        this.ParseExternalAliasDirectives(ns, nsFollowers|Parser.NamespaceOrTypeDeclarationStart|Token.Using|Token.LastIdentifier);
        this.ParseUsingDirectives(ns, nsFollowers|Token.LastIdentifier|Parser.NamespaceOrTypeDeclarationStart);
        this.ParseNamespaceMemberDeclarations(ns, nsFollowers);
        if (this.currentToken == Token.RightBrace && this.sink != null)
          this.sink.MatchPair(openContext, this.scanner.CurrentSourceContext);
        ns.SourceContext.EndPos = this.scanner.endPos;
        this.Skip(Token.RightBrace);
        if (this.currentToken == Token.Semicolon) this.GetNextToken();
        if (this.sink != null){
          nsBodyCtx.EndPos = this.scanner.endPos;
          this.sink.AddCollapsibleRegion(nsBodyCtx, false);
        }
      }else
        this.ParseTypeDeclarations(parentNamespace, followers);
      if (!followers[this.currentToken])
        this.SkipTo(followers, Error.EOFExpected);
    }
    private void ParseTypeDeclarations(Namespace ns, TokenSet followers){
      for(;;){
        SourceContext sctx = this.scanner.CurrentSourceContext;
        AttributeList attributes = this.ParseAttributes(ns, followers|Parser.AttributeOrTypeDeclarationStart);
        Token tok = this.currentToken;
        this.inUnsafeCode = false;
        TypeFlags flags = this.ParseTypeModifiers();
        switch (this.currentToken){
          case Token.Class:
          case Token.Interface:
          case Token.Struct: this.ParseTypeDeclaration(ns, null, attributes, flags, false, sctx, followers); break;
          case Token.Delegate: this.ParseDelegateDeclaration(ns, null, attributes, flags, sctx, followers); break;
          case Token.Enum: this.ParseEnumDeclaration(ns, null, attributes, flags, sctx, followers); break;
          case Token.Namespace: 
            if (tok != this.currentToken || (attributes != null && attributes.Count > 0)) 
              this.HandleError(Error.BadTokenInType); 
            return;
          case Token.LeftBracket:
            this.HandleError(Error.BadTokenInType); 
            this.GetNextToken();
            return;
          case Token.MultiLineDocCommentStart:
          case Token.SingleLineDocCommentStart:
            this.ParseDocComment(followers);
            break;
          case Token.Partial:
            SourceContext pctx = this.scanner.CurrentSourceContext;
            this.GetNextToken();
            switch (this.currentToken){
              case Token.Class:
              case Token.Struct:
              case Token.Interface:
                this.ParseTypeDeclaration(ns, null, attributes, flags, true, sctx, followers); break;
              case Token.Enum:
                this.HandleError(Error.PartialMisplaced);
                this.ParseEnumDeclaration(ns, null, attributes, flags, sctx, followers); break;
              default:
                if (this.currentToken == Token.Namespace)
                  this.HandleError(Error.BadModifiersOnNamespace);
                else
                  this.HandleError(Error.PartialMisplaced);
                this.SkipTo(followers, Error.None);
                return;
            }
            break;
          default:
            if (!followers[this.currentToken])
              this.SkipTo(followers, Error.BadTokenInType);
            else if (this.currentToken == Token.EndOfFile && attributes != null){
              Class dummy = new Class(this.module, null, attributes, TypeFlags.Public, ns.Name, Identifier.Empty, null, null, new MemberList(0));
              ns.Types.Add(dummy);
              //this.sink = null;
            }
            return;
        }

        this.inUnsafeCode = false;
      }
    }
    private static Identifier assemblyId = Identifier.For("assembly");
    private static Identifier eventId = Identifier.For("event");
    private static Identifier fieldId = Identifier.For("field");
    private static Identifier methodId = Identifier.For("method");
    private static Identifier moduleId = Identifier.For("module");
    private static Identifier paramId = Identifier.For("param");
    private static Identifier propertyId = Identifier.For("property");
    private static Identifier returnId = Identifier.For("return");
    private static Identifier typeId = Identifier.For("type");
    private AttributeList ParseAttributes(Namespace ns, TokenSet followers){
      if (this.currentToken != Token.LeftBracket) return null;
      AttributeList attributes = new AttributeList();
      bool allowGlobalAttributes = ns != null && ns.Name == Identifier.Empty && 
        (ns.NestedNamespaces == null || ns.NestedNamespaces.Count == 0) && (ns.Types == null || ns.Types.Count == 0);
      while(this.currentToken == Token.LeftBracket){
        SourceContext sctx = this.scanner.CurrentSourceContext;
        this.GetNextToken();
        AttributeTargets flags = (AttributeTargets)0;
        Identifier id = this.scanner.GetIdentifier();
        SourceContext attrCtx = this.scanner.CurrentSourceContext;
        switch(this.currentToken){
          case Token.Event:
          case Token.Identifier:
          case Token.Return:
            this.GetNextToken();
            if (this.currentToken == Token.Colon){
              this.GetNextToken();
              int key = id.UniqueIdKey;
              if (key == Parser.assemblyId.UniqueIdKey)
                flags = AttributeTargets.Assembly;
              else if (key == Parser.eventId.UniqueIdKey)
                flags = AttributeTargets.Event;
              else if (key == Parser.fieldId.UniqueIdKey)
                flags = AttributeTargets.Field;
              else if (key == Parser.methodId.UniqueIdKey)
                flags = AttributeTargets.Method|AttributeTargets.Constructor;
              else if (key == Parser.moduleId.UniqueIdKey)
                flags = AttributeTargets.Module;
              else if (key == Parser.paramId.UniqueIdKey)
                flags = AttributeTargets.Parameter;
              else if (key == Parser.propertyId.UniqueIdKey)
                flags = AttributeTargets.Property;
              else if (key == Parser.returnId.UniqueIdKey)
                flags = AttributeTargets.ReturnValue;
              else if (key == Parser.typeId.UniqueIdKey)
                flags = AttributeTargets.Class|AttributeTargets.Delegate|AttributeTargets.Enum|AttributeTargets.Interface|AttributeTargets.Struct;
              else{
                flags = (AttributeTargets)int.MaxValue;
                this.HandleError(Error.InvalidAttributeLocation, id.ToString());
              }
              id = this.scanner.GetIdentifier();
              this.SkipIdentifierOrNonReservedKeyword();
            }
            break;
          default:
            this.SkipIdentifierOrNonReservedKeyword();
            break;
        }
        for(;;){
          AttributeNode attr = new AttributeNode();
          attr.SourceContext = attrCtx;
          attr.Target = flags;       
          if (this.currentToken == Token.DoubleColon){
            Identifier prefix = id;
            this.GetNextToken();
            id = this.scanner.GetIdentifier();
            id.Prefix = prefix;
            id.SourceContext.StartPos = prefix.SourceContext.StartPos;
            this.SkipIdentifierOrNonReservedKeyword();
          }
          if (this.sink != null) this.sink.StartName(id);
          if (this.currentToken == Token.Dot)
            attr.Constructor = this.ParseQualifiedIdentifier(id, followers|Token.Comma|Token.LeftParenthesis|Token.RightBracket);
          else
            attr.Constructor = id;
          this.ParseAttributeArguments(attr, followers|Token.Comma|Token.RightBracket);
          if (flags != (AttributeTargets)int.MaxValue){
            if (allowGlobalAttributes && (flags == AttributeTargets.Assembly || flags == AttributeTargets.Module)){
              if (ns.Attributes == null) ns.Attributes = new AttributeList();
              ns.Attributes.Add(attr);
            }else
              attributes.Add(attr);
          }
          if (this.currentToken != Token.Comma) break;
          this.GetNextToken();
          if (this.currentToken == Token.RightBracket) break;
          id = this.scanner.GetIdentifier();
          this.SkipIdentifierOrNonReservedKeyword();
        }
        this.ParseBracket(sctx, Token.RightBracket, followers, Error.ExpectedRightBracket);      
      }
      this.SkipTo(followers);
      return attributes;
    }
    private void ParseAttributeArguments(AttributeNode attr, TokenSet followers){
      if (this.currentToken != Token.LeftParenthesis) return;
      SourceContext sctx = this.scanner.CurrentSourceContext;
      if (this.sink != null) this.sink.StartParameters(sctx);
      this.GetNextToken();
      ExpressionList expressions = attr.Expressions = new ExpressionList();
      bool hadNamedArgument = false;
      while (this.currentToken != Token.RightParenthesis){
        SourceContext sctx1 = this.scanner.CurrentSourceContext;
        Expression expr = this.ParseExpression(followers|Token.Comma|Token.RightParenthesis);
        if (expr != null){
          if (this.sink != null) {
            sctx1.EndPos = this.scanner.endPos;
            this.sink.NextParameter(sctx1);
          }
          AssignmentExpression aExpr = expr as AssignmentExpression;
          if (aExpr != null){
            AssignmentStatement aStat = (AssignmentStatement)aExpr.AssignmentStatement;
            Identifier id = aStat.Target as Identifier;
            if (id == null){
              this.HandleError(aStat.Target.SourceContext, Error.ExpectedIdentifier);
              expr = null;
            }else
              expr = new NamedArgument(id, aStat.Source, expr.SourceContext);
            hadNamedArgument = true;
          }else if (hadNamedArgument)
            this.HandleError(expr.SourceContext, Error.NamedArgumentExpected);
        }
        expressions.Add(expr);
        if (this.currentToken != Token.Comma) break;
        this.GetNextToken();
      }
      attr.SourceContext.EndPos = this.scanner.endPos;
      if (this.sink != null && this.currentToken != Token.EndOfFile) this.sink.EndParameters(this.scanner.CurrentSourceContext);
      this.ParseBracket(sctx, Token.RightParenthesis, followers, Error.ExpectedRightParenthesis);      
    }
    private TypeFlags ParseTypeModifiers(){
      TypeFlags result = (TypeFlags)0;
      for(;;){
        switch(this.currentToken){
          case Token.New:
            this.HandleError(Error.NewOnNamespaceElement);
            break;
          case Token.Public:
            if ((result & TypeFlags.VisibilityMask) != 0){
              if ((result & TypeFlags.Public) != 0) 
                this.HandleError(Error.DuplicateModifier, "public");
              else
                this.HandleError(Error.ConflictingProtectionModifier);
            }
            result |= TypeFlags.Public;
            break;
          case Token.Internal: 
            if ((result & TypeFlags.VisibilityMask) != 0){
              if ((result & TypeFlags.VisibilityMask) == TypeFlags.NestedAssembly) 
                this.HandleError(Error.DuplicateModifier, "internal");
              else
                this.HandleError(Error.ConflictingProtectionModifier);
            }
            result |= TypeFlags.NestedAssembly; 
            break;
          case Token.Abstract: 
            if ((result & (TypeFlags.Abstract|TypeFlags.Sealed|TypeFlags.SpecialName)) == (TypeFlags.Abstract|TypeFlags.Sealed|TypeFlags.SpecialName)){
              this.HandleError(Error.AbstractSealedStatic);
              break;
            }
            if ((result & TypeFlags.Abstract) != 0) this.HandleError(Error.DuplicateModifier, "abstract");
            result |= TypeFlags.Abstract; 
            break;
          case Token.Sealed: 
            if ((result & (TypeFlags.Abstract|TypeFlags.Sealed|TypeFlags.SpecialName)) == (TypeFlags.Abstract|TypeFlags.Sealed|TypeFlags.SpecialName)){
              this.HandleError(Error.SealedStaticClass);
              break;
            }
            if ((result & TypeFlags.Sealed) != 0) this.HandleError(Error.DuplicateModifier, "sealed");
            result |= TypeFlags.Sealed; 
            break;
          case Token.Static:
            if ((result & (TypeFlags.Abstract|TypeFlags.Sealed|TypeFlags.SpecialName)) == (TypeFlags.Abstract|TypeFlags.Sealed|TypeFlags.SpecialName)){
              this.HandleError(Error.DuplicateModifier, "static");
              break;
            }
            if ((result & TypeFlags.Abstract) != 0){
              this.HandleError(Error.AbstractSealedStatic);
              break;
            }
            if ((result & TypeFlags.Sealed) != 0){
              this.HandleError(Error.SealedStaticClass);
              break;
            }
            result |= TypeFlags.Abstract|TypeFlags.Sealed|TypeFlags.SpecialName; 
            break;
          case Token.Unsafe:
            if (!this.allowUnsafeCode){
              this.allowUnsafeCode = true;
              this.HandleError(Error.IllegalUnsafe);
            }
            this.inUnsafeCode = true;
            this.GetNextToken();
            if (this.currentToken == Token.LeftBrace){
              this.HandleError(Error.TypeExpected);
              if ((result & TypeFlags.VisibilityMask) == 0)
                result |= TypeFlags.RTSpecialName; //Signal absence of any visibility modifier
              goto default;
            }
            continue;
          case Token.Partial:
            if ((result & TypeFlags.VisibilityMask) == 0)
              result |= TypeFlags.RTSpecialName; //Signal absence of any visibility modifier
            goto default;
          default:
            if ((result & TypeFlags.VisibilityMask) != TypeFlags.Public)
              result &= ~TypeFlags.VisibilityMask;
            return result;
        }
        this.GetNextToken();
      }
    }
    private void ParseTypeDeclaration(Namespace ns, TypeNode parentType, AttributeList attributes, TypeFlags flags, bool isPartial, SourceContext sctx, TokenSet followers){
      this.ParseTypeDeclaration(ns, parentType, attributes, null, null, flags, isPartial, sctx, followers);
    }
    private void ParseTypeDeclaration(Namespace ns, TypeNode parentType, AttributeList attributes, TokenList modifierTokens, 
      SourceContextList modifierContexts, bool isPartial, SourceContext sctx, TokenSet followers){
      this.ParseTypeDeclaration(ns, parentType, attributes, modifierTokens, modifierContexts, TypeFlags.None, isPartial, sctx, followers);
    }
    private void ParseTypeDeclaration(Namespace ns, TypeNode parentType, AttributeList attributes, TokenList modifierTokens, 
      SourceContextList modifierContexts, TypeFlags flags, bool isPartial, SourceContext sctx, TokenSet followers){
      if (parentType is Interface){
        this.HandleError(Error.InterfacesCannotContainTypes);
        modifierTokens = null;
      }
      TypeNode t = null;
      InvariantCt = 0;
      switch(this.currentToken){
        case Token.Class: 
          Class c = new Class();
          t = c;
          if (parentType == null)
            t.DeclaringNamespace = ns;
          else
            t.DeclaringType = parentType;
          if (modifierTokens != null)
            t.Flags |= this.NestedTypeFlags(modifierTokens, modifierContexts, t, isPartial)|TypeFlags.BeforeFieldInit; 
          else{
            t.IsUnsafe = this.inUnsafeCode;
            t.Flags |= flags|TypeFlags.BeforeFieldInit;
          }
          if (t.IsAbstract && t.IsSealed && t.IsSpecialName){
            c.IsAbstractSealedContainerForStatics = true;
            c.Flags &= ~TypeFlags.SpecialName;
          }
	  //HS D: HACK FIXME
	  this.currentTypeNode = t;
          break;
        case Token.Interface: 
          t = new Interface();
          if (parentType == null)
            t.DeclaringNamespace = ns;
          else
            t.DeclaringType = parentType;
          if (modifierTokens != null)
            t.Flags |= this.NestedTypeFlags(modifierTokens, modifierContexts, t, isPartial);
          else{
            if ((flags & TypeFlags.Abstract) != 0){
              if ((flags & TypeFlags.Sealed) != 0 && (flags & TypeFlags.SpecialName) != 0){
                this.HandleError(Error.InvalidModifier, "static");
                flags &= ~(TypeFlags.Abstract|TypeFlags.Sealed|TypeFlags.SpecialName);
              }else{
                this.HandleError(Error.InvalidModifier, "abstract");
                flags &= ~TypeFlags.Abstract;
              }
            }else if ((flags & TypeFlags.Sealed) != 0){
              this.HandleError(Error.InvalidModifier, "sealed");
              flags &= ~TypeFlags.Sealed;
            }
            t.IsUnsafe = this.inUnsafeCode;
            t.Flags |= flags|TypeFlags.BeforeFieldInit;
          }
          break;
        case Token.Struct: 
          t = new Struct(); 
          if (parentType == null)
            t.DeclaringNamespace = ns;
          else
            t.DeclaringType = parentType;
          if (modifierTokens != null)
            t.Flags |= this.NestedTypeFlags(modifierTokens, modifierContexts, t, isPartial)|TypeFlags.BeforeFieldInit; 
          else{
            if ((flags & TypeFlags.Abstract) != 0){
              if ((flags & TypeFlags.Sealed) != 0 && (flags & TypeFlags.SpecialName) != 0){
                this.HandleError(Error.InvalidModifier, "static");
                flags &= ~(TypeFlags.Abstract|TypeFlags.Sealed|TypeFlags.SpecialName);
              }else{
                this.HandleError(Error.InvalidModifier, "abstract");
                flags &= ~TypeFlags.Abstract;
              }
            }else if ((flags & TypeFlags.Sealed) != 0){
              this.HandleError(Error.InvalidModifier, "sealed");
            }
            t.IsUnsafe = this.inUnsafeCode;
            t.Flags |= flags|TypeFlags.BeforeFieldInit;
          }
          break;
        default: 
          Debug.Assert(false);
          break;
      }
      t.Attributes = attributes;
      t.SourceContext = sctx;
      t.DeclaringModule = this.module;
      t.Documentation = this.LastDocComment;
      this.GetNextToken();
      t.Name = this.scanner.GetIdentifier();
      if (Parser.IdentifierOrNonReservedKeyword[this.currentToken])
        this.GetNextToken();
      else{
        this.SkipIdentifierOrNonReservedKeyword();
        if (Parser.IdentifierOrNonReservedKeyword[this.currentToken]){
          t.Name = this.scanner.GetIdentifier();
          this.GetNextToken();
        }
      }
      if (this.currentToken == Token.LessThan)
        this.ParseTypeParameters(t, followers|Token.Colon|Token.LeftBrace|Token.Where);
      if (parentType != null){
        t.Namespace = Identifier.Empty;
        if (parentType.IsGeneric) t.IsGeneric = true;
      }else
        t.Namespace = ns.FullNameId;
      Identifier mangledName = t.Name;
      if (Cci.TargetPlatform.GenericTypeNamesMangleChar != 0) {
        int numPars = t.TemplateParameters == null ? 0 : t.TemplateParameters.Count;
        if (numPars > 0){
          mangledName = new Identifier(t.Name.ToString() + Cci.TargetPlatform.GenericTypeNamesMangleChar + numPars.ToString(), t.Name.SourceContext);
          t.IsGeneric = this.useGenerics;
        }
      }
      t.PartiallyDefines = this.GetCompleteType(t, mangledName, isPartial);
      if (isPartial){
        isPartial = t.PartiallyDefines != null;
        if (!isPartial)
          t.Name = new Identifier(t.Name+" "+t.UniqueKey, t.Name.SourceContext);
      }else
        isPartial = t.PartiallyDefines != null;
      if (parentType != null){
        if (!isPartial || parentType.PartiallyDefines != null)
          parentType.Members.Add(t);
      }else{
        ns.Types.Add(t);
        if (!isPartial) this.AddTypeToModule(t);
      }
      if (this.currentToken == Token.Colon){
        this.GetNextToken();
        t.Interfaces = this.ParseInterfaceList(followers|Token.LeftBrace|Token.Where, true); //The first of these might be the base class, but that is a semantic issue
      }else
        t.Interfaces = new InterfaceList(); //TODO: omit this?
      t.InterfaceExpressions = t.Interfaces;
      while (this.currentToken == Token.Where)
        this.ParseTypeParameterConstraint(t, followers|Token.LeftBrace|Token.Where);
      t.SourceContext.EndPos = this.scanner.endPos;
      SourceContext typeBodyCtx = this.scanner.CurrentSourceContext;
      this.Skip(Token.LeftBrace);
    tryAgain:
      this.ParseTypeMembers(t, followers|Token.RightBrace);
      if (this.currentToken == Token.Namespace){
        this.HandleError(Error.InvalidMemberDecl, this.scanner.CurrentSourceContext.SourceText);
        this.currentToken = Token.Class;
        goto tryAgain;
      }
      int endCol = this.scanner.endPos;
      this.ParseBracket(t.SourceContext, Token.RightBrace, followers|Token.Semicolon, Error.ExpectedRightBrace);
      t.SourceContext.EndPos = endCol;
      t.Name = mangledName;
      if (this.currentToken == Token.Semicolon)
        this.GetNextToken();
      if (this.sink != null){
        typeBodyCtx.EndPos = endCol;
        this.sink.AddCollapsibleRegion(typeBodyCtx, false);
      }
      this.SkipTo(followers|Parser.TypeMemberStart);
      if (!followers[this.currentToken])
        this.SkipTo(followers, Error.NamespaceUnexpected);
      if (isPartial) this.MergeWithCompleteType(t);        
    }
    
    private void AddTypeToModule(TypeNode t){
      Identifier name = t.Name;
      if (Cci.TargetPlatform.GenericTypeNamesMangleChar != 0) {
        int numPars = t.TemplateParameters == null ? 0 : t.TemplateParameters.Count;
        if (numPars > 0)
          name = new Identifier(name.ToString() + Cci.TargetPlatform.GenericTypeNamesMangleChar + numPars.ToString(), name.SourceContext);
      }
      TypeNode t1 = this.module.GetType(t.Namespace, name);
      if (t1 == null)
        this.module.Types.Add(t);
      else{
        this.HandleError(t.Name.SourceContext, Error.DuplicateNameInNS, t.Name.ToString(), t.Namespace.ToString());
        if (t1.Name.SourceContext.Document != null)
          this.HandleError(t1.Name.SourceContext, Error.RelatedErrorLocation);
      }
    }
    private void MergeWithCompleteType(TypeNode partialType){
      Debug.Assert(partialType != null && partialType.PartiallyDefines != null);
      TypeNode completeType = partialType.PartiallyDefines;
      AttributeList attributes = partialType.Attributes;
      AttributeList allAttributes = completeType.Attributes; 
      if (allAttributes == null) allAttributes = completeType.Attributes = new AttributeList();
      for (int i = 0, n = attributes == null ? 0 : attributes.Count; i < n; i++){
        AttributeNode attr = attributes[i];
        if (attr == null) continue;
        allAttributes.Add(attr);
      }
      MemberList members = partialType.Members;
      MemberList allMembers = completeType.Members; 
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
        Member mem = members[i];
        if (mem == null) continue;
        mem.DeclaringType = completeType;
        TypeNode nestedType = mem as TypeNode;
        if (nestedType != null && nestedType.PartiallyDefines != null){
          Debug.Assert(nestedType.PartiallyDefines.DeclaringType == completeType);
          continue;
        }
        allMembers.Add(mem);
      }
      completeType.TemplateParameters = partialType.TemplateParameters;
      //TODO: check that type parameters and constraints match
      if (partialType.OverridesBaseClassMember) completeType.OverridesBaseClassMember = true;
      if (partialType.HidesBaseClassMember) completeType.HidesBaseClassMember = true;
      Class partialClass = partialType as Class;
      if (partialClass != null && partialClass.IsAbstractSealedContainerForStatics){
        ((Class)completeType).IsAbstractSealedContainerForStatics = true;
        completeType.Flags |= TypeFlags.Abstract|TypeFlags.Sealed;
      }else{
        if (partialType.IsAbstract){
          if (completeType.IsSealed){
            this.HandleError(partialType.Name.SourceContext, Error.AbstractSealedStatic);
            this.HandleError(completeType.Name.SourceContext, Error.RelatedErrorLocation);
          }else
            completeType.Flags |= TypeFlags.Abstract;
        }
        if (partialType.IsSealed){
          if (completeType.IsAbstract){
            this.HandleError(completeType.Name.SourceContext, Error.AbstractSealedStatic);
            this.HandleError(partialType.Name.SourceContext, Error.RelatedErrorLocation);
          }else
            completeType.Flags |= TypeFlags.Sealed;
        }
      }
      if ((partialType.Flags & TypeFlags.RTSpecialName) == 0){
        //partial type had an explicit visibility modifier
        if ((completeType.Flags & TypeFlags.RTSpecialName) != 0){
          //Complete type has not yet received an explicit visibility modifier
          completeType.Flags &= ~TypeFlags.RTSpecialName;
          completeType.Flags &= ~TypeFlags.VisibilityMask;
          completeType.Flags |= partialType.Flags & TypeFlags.VisibilityMask;
          completeType.Name = partialType.Name;
        }
        if ((completeType.Flags & TypeFlags.VisibilityMask) != (partialType.Flags & TypeFlags.VisibilityMask)){
          this.HandleError(partialType.Name.SourceContext, Error.PartialModifierConflict, new ErrorHandler(this.errors).GetTypeName(partialType));
          this.HandleError(completeType.Name.SourceContext, Error.RelatedErrorLocation);
        }
      }
    }
    private TypeNode GetCompleteType(TypeNode partialType, Identifier mangledName, bool isPartial){
      Debug.Assert(partialType != null);
      TypeNode completeType = null;
      TypeNode declaringType = partialType.DeclaringType;
      if (declaringType == null)
        completeType = this.module.GetType(partialType.Namespace, mangledName);
      else{
        if (declaringType.PartiallyDefines != null)
          declaringType = declaringType.PartiallyDefines;
        completeType = declaringType.GetNestedType(mangledName);
        declaringType.NestedTypes = null;
      }
      if (completeType == null){
        if (!isPartial) return null;
        if (partialType is Class){
          completeType = new Class();
          ((Class)completeType).BaseClass = (Class)partialType.BaseType;
        }else if (partialType is Struct)
          completeType = new Struct();
        else{
          Debug.Assert(partialType is Interface);
          completeType = new Interface();
        }
        completeType.Attributes = new AttributeList();
        completeType.Flags = partialType.Flags;
        completeType.DeclaringModule = this.module;
        completeType.DeclaringType = declaringType;
        completeType.Interfaces = new InterfaceList();
        completeType.Name = mangledName;
        completeType.Namespace = partialType.Namespace;
        //completeType.Documentation = ; //TODO: figure out if documentation gets merged
        if (declaringType == null)
          this.AddTypeToModule(completeType);
        else
          declaringType.Members.Add(completeType);
        completeType.IsDefinedBy = new TypeNodeList();
      }else{
        if (completeType.IsDefinedBy == null){
          if (isPartial)
            this.HandleError(completeType.Name.SourceContext, Error.MissingPartial, completeType.Name.ToString());
          return null;
        }else if (!isPartial){
          this.HandleError(partialType.Name.SourceContext, Error.MissingPartial, partialType.Name.ToString());
        }else if (completeType.NodeType != partialType.NodeType)
          this.HandleError(partialType.Name.SourceContext, Error.PartialTypeKindConflict, partialType.Name.ToString());
      }
      completeType.IsDefinedBy.Add(partialType);
      return completeType;
    }
    private void ParseTypeParameters(Method m, TokenSet followers){
      m.IsGeneric = this.useGenerics;
      this.ParseTypeParameters(m, followers, m.TemplateParameters = new TypeNodeList());
    }
    private void ParseTypeParameters(TypeNode t, TokenSet followers){
      t.IsGeneric = this.useGenerics;
      this.ParseTypeParameters(t, followers, t.TemplateParameters = new TypeNodeList());
    }
    private void ParseTypeParameters(Member parent, TokenSet followers, TypeNodeList parameters){
      TypeNode declaringType = parent as TypeNode;
      if (declaringType == null) declaringType = parent.DeclaringType;
      Debug.Assert(this.currentToken == Token.LessThan);
      this.GetNextToken();
      for(int i = 0; ; i++){
        AttributeList attributes = null;
        if (this.currentToken == Token.LeftBracket)
          attributes = this.ParseAttributes(null, followers|Token.Identifier|Token.Comma|Token.GreaterThan|Token.RightShift);
        if (this.currentToken != Token.Identifier) {
          this.HandleError(Error.ExpectedIdentifier);
          break;
        }
        Identifier id = this.scanner.GetIdentifier();
        TypeParameter param = parent is Method ? new MethodTypeParameter() : new TypeParameter();
        param.Attributes = attributes;
        param.DeclaringMember = parent;
        param.ParameterListIndex = i;
        param.Name = id;
        param.DeclaringModule = declaringType.DeclaringModule;
        if (!this.useGenerics){
          param.DeclaringType = declaringType;
          if (!(parent is Method)) declaringType.Members.Add(param);
        }
        parameters.Add(param);
        param.SourceContext = this.scanner.CurrentSourceContext;
        this.GetNextToken();
        if (this.currentToken == Token.Dot){
          QualifiedIdentifier qualid = (QualifiedIdentifier)this.ParseQualifiedIdentifier(id, followers|Token.Colon|Token.Comma|Token.GreaterThan);
          param.Namespace = Identifier.For(qualid.Qualifier.ToString());
          param.Name = qualid.Identifier;
          param.SourceContext = qualid.SourceContext;
        }
        param.Interfaces = new InterfaceList();
        if (this.currentToken != Token.Comma) break;
        this.GetNextToken();
      }      
      this.Skip(Token.GreaterThan);
      this.SkipTo(followers);
    }
    private void ParseTypeParameterConstraint(Method m, TokenSet followers){
      this.ParseTypeParameterConstraint(m.DeclaringType, followers, m.TemplateParameters);
    }
    private void ParseTypeParameterConstraint(TypeNode t, TokenSet followers){
      this.ParseTypeParameterConstraint(t, followers, t.TemplateParameters);
    }
    private void ParseTypeParameterConstraint(TypeNode t, TokenSet followers, TypeNodeList parameters){
      Debug.Assert(this.currentToken == Token.Where);
      this.GetNextToken();
      //Get parameter name
      Identifier parameterName = this.scanner.GetIdentifier();
      this.SkipIdentifierOrNonReservedKeyword();
      //Get corresponding parameter in parameter list
      TypeNode parameter = null;
      int n = parameters == null ? 0 : parameters.Count;
      int i = 0;
      while (i < n){
        parameter = parameters[i];
        if (parameter != null && parameter.Name != null && parameter.Name.UniqueIdKey == parameterName.UniqueIdKey) break;
        i++;
      }
      if (i >= n){
        //TODO: complain about parameter name not being present
        parameter = new TypeParameter();
        parameter.Name = parameterName;
      }
      this.Skip(Token.Colon);
      TypeParameter tpar = (TypeParameter)parameter;
      //TODO: check if parameter already has a constraint
      //Add constraints
      switch (this.currentToken){
        case Token.Class:
          this.GetNextToken();
          tpar.TypeParameterFlags = TypeParameterFlags.ReferenceTypeConstraint;
        skipComma:
          if (this.currentToken == Token.Comma){
            this.GetNextToken();
            break;
          }
          this.SkipTo(followers);
          return;
        case Token.Struct:
          this.GetNextToken();
          tpar.TypeParameterFlags = TypeParameterFlags.ValueTypeConstraint;
          goto skipComma;
      }
      if (this.currentToken != Token.New)
        parameter.Interfaces = parameter.InterfaceExpressions = this.ParseInterfaceList(followers|Token.New, true);
      switch (this.currentToken){
        case Token.Class:
        case Token.Struct:
          this.HandleError(Error.RefValBoundMustBeFirst);
          this.GetNextToken();
          this.SkipTo(followers, Error.None);
          return;
        case Token.New:
          this.GetNextToken();
          this.Skip(Token.LeftParenthesis);
          this.Skip(Token.RightParenthesis);
          tpar.TypeParameterFlags = TypeParameterFlags.DefaultConstructorConstraint;
          if (!followers[this.currentToken])
            this.SkipTo(followers, Error.NewBoundMustBeLast);
          return;
      }
    }
    private void ParseBracket(SourceContext openingContext, Token token, TokenSet followers, Error error){
      if (this.currentToken == token){
        if (this.sink != null)
          this.sink.MatchPair(openingContext, this.scanner.CurrentSourceContext);
        this.GetNextToken();
        this.SkipTo(followers);
      }else
        this.SkipTo(followers, error);
    }
    private TypeNode ComplainAndReturnRealDeclaringType(TypeNode type, Error error){
      this.HandleError(error);
      while (type is Struct && type.Name == null)
        type = type.DeclaringType;
      return type;
    }
    private void ParseTypeMembers(TypeNode t, TokenSet followers){
      TokenSet followersOrTypeMemberStart = followers|Parser.TypeMemberStart;
      for(;;){
        SourceContext sctx = this.scanner.CurrentSourceContext;
        AttributeList attributes = this.ParseAttributes(null, followersOrTypeMemberStart);
        bool savedInUnsafeCode = this.inUnsafeCode;
        TokenList modifierTokens = new TokenList();
        SourceContextList modifierContexts = new SourceContextList();
        this.ParseModifiers(modifierTokens, modifierContexts, t.NodeType == NodeType.Interface);
        sctx.EndPos = this.scanner.endPos;
        switch(this.currentToken){
          case Token.Class: 
          case Token.Struct: 
          case Token.Interface:
            this.ParseTypeDeclaration(null, t, attributes, modifierTokens, modifierContexts, false, sctx, followersOrTypeMemberStart);
            break;
          case Token.Delegate: 
            TypeNode del = this.ParseDelegateDeclaration(null, t, attributes, modifierTokens, modifierContexts, sctx, followersOrTypeMemberStart);
            if (del is FunctionTypeExpression){
              this.lastDocCommentBackingField = (XmlElement)del.Documentation;
              this.ParseFieldOrMethodOrPropertyOrStaticInitializer(del, t, attributes, modifierTokens, modifierContexts,
                sctx, this.scanner.CurrentSourceContext, followersOrTypeMemberStart); 
            }
            break;
          case Token.Enum: 
            this.ParseEnumDeclaration(null, t, attributes, modifierTokens, modifierContexts, sctx, followersOrTypeMemberStart); 
            break;
          case Token.Const:
            this.ParseConst(t, attributes, modifierTokens, modifierContexts, sctx, followersOrTypeMemberStart);
            break;
          case Token.Invariant:
            this.ParseInvariant(t, attributes, modifierTokens, modifierContexts, sctx, followersOrTypeMemberStart);
            break;
          case Token.Bool:
          case Token.Decimal:
          case Token.Sbyte:
          case Token.Byte:
          case Token.Short:
          case Token.Ushort:
          case Token.Int:
          case Token.Uint:
          case Token.Long:
          case Token.Ulong:
          case Token.Char:
          case Token.Float:
          case Token.Double:
          case Token.Object:
          case Token.String:
          case Token.Void:
          case Token.Identifier:
            NotPartial:
              this.ParseFieldOrMethodOrPropertyOrStaticInitializer(t, attributes, modifierTokens, modifierContexts,
                sctx, followersOrTypeMemberStart); 
            break;
          case Token.Model:
            this.GetNextToken();
            this.ParseModelField(t, attributes, modifierTokens, modifierContexts, sctx, followersOrTypeMemberStart);
            break;
          case Token.Event:
            this.ParseEvent(t, attributes, modifierTokens, modifierContexts, sctx, followersOrTypeMemberStart); break;
          case Token.Operator:
          case Token.Explicit:
          case Token.Implicit:
            this.ParseOperator(t, attributes, modifierTokens, modifierContexts, null, sctx, followersOrTypeMemberStart); break;
          case Token.BitwiseNot:
            this.ParseDestructor(t, attributes, modifierTokens, modifierContexts, sctx, followersOrTypeMemberStart); break;
          case Token.MultiLineDocCommentStart:
          case Token.SingleLineDocCommentStart:
            this.ParseDocComment(followersOrTypeMemberStart);
            break;
          case Token.Partial:
            SourceContext pctx = this.scanner.CurrentSourceContext;
            ScannerState ss = this.scanner.state;
            this.GetNextToken();
            switch (this.currentToken){
              case Token.Class:
              case Token.Struct:
              case Token.Interface:
                this.ParseTypeDeclaration(null, t, attributes, modifierTokens, modifierContexts, true, sctx, followersOrTypeMemberStart);
                break;
              case Token.Enum:
                this.HandleError(Error.PartialMisplaced);
                this.ParseEnumDeclaration(null, t, attributes, modifierTokens, modifierContexts, sctx, followersOrTypeMemberStart);
                break;
              case Token.LeftParenthesis:
              case Token.LeftBrace:
              case Token.LessThan:
                this.scanner.endPos = pctx.StartPos;
                this.scanner.state = ss;
                this.currentToken = Token.None;
                this.GetNextToken();
                goto NotPartial;
              default:
                if (Parser.IdentifierOrNonReservedKeyword[this.currentToken])
                  goto case Token.LeftParenthesis;
                this.HandleError(Error.PartialMisplaced);
                this.SkipTo(followers, Error.None);
                return;
            }
            break;
          default:
            if (Parser.IdentifierOrNonReservedKeyword[this.currentToken]) goto case Token.Identifier;
            if (this.currentToken == Token.EndOfFile && this.sink != null){
              if (attributes != null){
                Class dummy = new Class(this.module, t, attributes, TypeFlags.Public, Identifier.Empty, Identifier.Empty, null, null, new MemberList(0));
                t.Members.Add(dummy);
              //}else{
              //  Field dummy = new Field(t, null, FieldFlags.Public, Identifier.Empty, new TypeExpression(new Identifier(" ", this.scanner.CurrentSourceContext)), null);
              //  t.Members.Add(dummy);
              }
            }
            return;
        }
        this.inUnsafeCode = savedInUnsafeCode;
      }
    }
    private void ParseModifiers(TokenList modifierTokens, SourceContextList modifierContexts, bool parsingInterface){
      if (parsingInterface && this.currentToken == Token.New){
        modifierTokens.Add(this.currentToken);
        modifierContexts.Add(this.scanner.CurrentSourceContext);
        this.GetNextToken();
        modifierTokens.Add(Token.Public);
        modifierContexts.Add(this.scanner.CurrentSourceContext);
        modifierTokens.Add(Token.Abstract);
        modifierContexts.Add(this.scanner.CurrentSourceContext);
        return;
      }
      for(;;){
        switch(this.currentToken){
          case Token.New:
          case Token.Public:
          case Token.Protected:
          case Token.Internal: 
          case Token.Private: 
          case Token.Abstract: 
          case Token.Sealed: 
          case Token.Static:
          case Token.Readonly:
          case Token.Volatile:
          case Token.Virtual:
	      //case Token.Operation: //HS D
	      //case Token.Transformable: //HS D
          case Token.Override:
          case Token.Extern:
            if (parsingInterface && this.currentToken != Token.Readonly && this.currentToken != Token.Static)
              modifierTokens.Add(Token.IllegalCharacter);
            else
              modifierTokens.Add(this.currentToken);
            modifierContexts.Add(this.scanner.CurrentSourceContext);
            break;
          case Token.Unsafe:
            this.inUnsafeCode = true;
            modifierTokens.Add(this.currentToken);
            modifierContexts.Add(this.scanner.CurrentSourceContext);
            this.GetNextToken();
            if (this.currentToken == Token.LeftBrace){
              this.HandleError(Error.TypeExpected);
              this.GetNextToken();
              return;
            }
            continue;
          default:
            if (parsingInterface){
              modifierTokens.Add(Token.Public);
              modifierContexts.Add(new SourceContext());
              modifierTokens.Add(Token.Abstract);
              modifierContexts.Add(new SourceContext());
            }
            return;
        }
        this.GetNextToken();
      }
    }
    private TypeFlags NestedTypeFlags(TokenList modifierTokens, SourceContextList modifierContexts, Member ntype){
      return this.NestedTypeFlags(modifierTokens, modifierContexts, ntype, false);
    }
    private TypeFlags NestedTypeFlags(TokenList modifierTokens, SourceContextList modifierContexts, Member ntype, bool isPartial){
      TypeFlags result = TypeFlags.None;
      for(int i = 0, n = modifierTokens.Length; i < n; i++){
        switch(modifierTokens[i]){

          case Token.New:
            if (ntype.HidesBaseClassMember)
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "new");             
            ntype.HidesBaseClassMember = true;
            break;
          case Token.Public:
            if ((result & TypeFlags.VisibilityMask) != 0){
              if ((result & TypeFlags.VisibilityMask) == TypeFlags.NestedPublic)
                this.HandleError(modifierContexts[i], Error.DuplicateModifier, "public");             
              else
                this.HandleError(modifierContexts[i],Error.ConflictingProtectionModifier);
            }
            result |= TypeFlags.NestedPublic;
            break;
          case Token.Protected:
            if (ntype != null && ntype.DeclaringType is Struct){
              this.HandleError(modifierContexts[i], Error.InvalidModifier, "protected");
              break;
            }
            if ((result & TypeFlags.VisibilityMask) != 0){
              if ((result & TypeFlags.VisibilityMask) == TypeFlags.NestedFamily || (result & TypeFlags.VisibilityMask) == TypeFlags.NestedFamORAssem)
                this.HandleError(modifierContexts[i], Error.DuplicateModifier, "protected");
              else if ((result & TypeFlags.NestedAssembly) != 0){
                result &= ~TypeFlags.NestedAssembly;
                result |= TypeFlags.NestedFamORAssem;
                break;
              }else
                this.HandleError(modifierContexts[i], Error.ConflictingProtectionModifier);
            }
            result |= TypeFlags.NestedFamily; 
            break;
          case Token.Internal: 
            if ((result & TypeFlags.VisibilityMask) != 0){
              if ((result & TypeFlags.VisibilityMask) == TypeFlags.NestedAssembly || (result & TypeFlags.VisibilityMask) == TypeFlags.NestedFamORAssem) 
                this.HandleError(Error.DuplicateModifier, "internal");             
              else if ((result & TypeFlags.NestedFamily) != 0){
                result &= ~TypeFlags.NestedFamily;
                result |= TypeFlags.NestedFamORAssem;
                break;
              }else
                this.HandleError(modifierContexts[i], Error.ConflictingProtectionModifier);
            }
            result |= TypeFlags.NestedAssembly; 
            break;
          case Token.Private: 
            if ((result & TypeFlags.VisibilityMask) != 0){
              if ((result & TypeFlags.VisibilityMask) == TypeFlags.NestedPrivate)
                this.HandleError(modifierContexts[i], Error.DuplicateModifier, "private");             
              else
                this.HandleError(modifierContexts[i], Error.ConflictingProtectionModifier);
            }
            result |= TypeFlags.NestedPrivate; 
            break;
          case Token.Abstract:
            if (!(ntype is Class)){
              this.HandleError(Error.InvalidModifier, "abstract");
              break;
            }
            if ((result & TypeFlags.Abstract) != 0) 
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "abstract");
            result |= TypeFlags.Abstract; 
            break;
          case Token.Sealed: 
            if (!(ntype is Class)){
              this.HandleError(Error.InvalidModifier, "sealed");
              break;
            }
            if ((result & TypeFlags.Sealed) != 0) 
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "sealed");
            result |= TypeFlags.Sealed; 
            break;
          case Token.Static:
            if (!(ntype is Class)){
              this.HandleError(Error.InvalidModifier, "static");
              break;
            }
            if ((result & TypeFlags.Sealed) != 0) 
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "sealed");
            if ((result & TypeFlags.Abstract) != 0) 
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "abstract");
            result |= TypeFlags.Abstract|TypeFlags.Sealed|TypeFlags.SpecialName; 
            break;
          case Token.Readonly:
            this.HandleError(Error.InvalidModifier, "readonly");
            break;
          case Token.Volatile:
            this.HandleError(Error.InvalidModifier, "volatile");
            break;
          case Token.Virtual:
            this.HandleError(Error.InvalidModifier, "virtual");
            break;
          case Token.Override:
            this.HandleError(Error.InvalidModifier, "override");
            break;
          case Token.Extern:
            this.HandleError(Error.InvalidModifier, "extern");
            break;
          case Token.Unsafe:
            if (!this.allowUnsafeCode){
              this.allowUnsafeCode = true;
              this.HandleError(modifierContexts[i], Error.IllegalUnsafe);
            }
            if (ntype is EnumNode){
              this.HandleError(Error.InvalidModifier, "unsafe");
              break;
            }
            this.inUnsafeCode = true;
            break;
          default:
            Debug.Assert(false);
            break;
        }
      }
      ntype.IsUnsafe = this.inUnsafeCode;
      if ((result & TypeFlags.VisibilityMask) == 0){
        result |= TypeFlags.NestedPrivate;
        if (isPartial) result |= TypeFlags.RTSpecialName;
      }
      return result;
    }
    private void ParseFieldOrMethodOrPropertyOrStaticInitializer(TypeNode parentType, AttributeList attributes, TokenList modifierTokens, 
      SourceContextList modifierContexts, object sctx, TokenSet followers){
      SourceContext idCtx = this.scanner.CurrentSourceContext;
      TypeNode t = this.ParseTypeExpression(parentType.ConstructorName, followers|Parser.IdentifierOrNonReservedKeyword|Token.Explicit|Token.Implicit);
      this.ParseFieldOrMethodOrPropertyOrStaticInitializer(t, parentType, attributes, modifierTokens, modifierContexts, sctx, idCtx, followers);
    }
    private void ParseFieldOrMethodOrPropertyOrStaticInitializer(TypeNode t, TypeNode parentType, AttributeList attributes, TokenList modifierTokens, 
      SourceContextList modifierContexts, object sctx, SourceContext idCtx, TokenSet followers){
      if (Parser.IsVoidType(t) && this.currentToken == Token.LeftParenthesis){
        this.ParseConstructor(parentType, attributes, modifierTokens, modifierContexts, sctx, idCtx, followers|Token.Semicolon);
        if (this.currentToken == Token.Semicolon) this.GetNextToken();
        this.SkipTo(followers);
        return;
      }
      bool badModifier = false;
    tryAgain:
      switch (this.currentToken){
        case Token.This:
          Identifier itemId = new Identifier("Item");
          itemId.SourceContext = this.scanner.CurrentSourceContext;
          this.ParseProperty(parentType, attributes, modifierTokens, modifierContexts, sctx, t, null, itemId, followers);
          return;
        case Token.Explicit:
        case Token.Implicit:
        case Token.Operator:
          this.ParseOperator(parentType, attributes, modifierTokens, modifierContexts, t, sctx, followers);
          return;
        case Token.New:
        case Token.Public:
        case Token.Protected:
        case Token.Internal: 
        case Token.Private: 
        case Token.Abstract: 
        case Token.Sealed: 
        case Token.Static:
        case Token.Readonly:
        case Token.Volatile:
        case Token.Virtual:
	    //case Token.Operation: //HS D
	    //case Token.Transformable: //HS D
        case Token.Override:
        case Token.Extern:
        case Token.Unsafe:
          if (this.scanner.TokenIsFirstAfterLineBreak) break;
          if (!badModifier){
            this.HandleError(Error.BadModifierLocation, this.scanner.GetTokenSource());
            badModifier = true;
          }
          this.GetNextToken();
          goto tryAgain;
        case Token.LeftParenthesis:
        case Token.LessThan:
          if (t is TypeExpression && ((TypeExpression)t).Expression is Identifier){
            this.HandleError(t.SourceContext, Error.MemberNeedsType);
            this.ParseMethod(parentType, attributes, modifierTokens, modifierContexts, sctx, 
              this.TypeExpressionFor(Token.Void), null, (Identifier)((TypeExpression)t).Expression, followers);
            return;
          }
          break;
        default:
          if (!Parser.IdentifierOrNonReservedKeyword[this.currentToken]){
            if (followers[this.currentToken])
              this.HandleError(Error.ExpectedIdentifier);
            else
              this.SkipTo(followers);
            this.ParseField(parentType, attributes, modifierTokens, modifierContexts, sctx, t, Identifier.Empty, followers);
            return;
          }
          break;
      }
      TypeExpression interfaceType = null;
      Identifier id = this.scanner.GetIdentifier();
      if (badModifier) id.SourceContext.Document = null; //suppress any further errors involving this member
      this.SkipIdentifierOrNonReservedKeyword(); 
      if (this.currentToken == Token.DoubleColon){
        Identifier prefix = id;
        this.GetNextToken();
        id = this.scanner.GetIdentifier();
        id.Prefix = prefix;
        id.SourceContext.StartPos = prefix.SourceContext.StartPos;
        this.SkipIdentifierOrNonReservedKeyword();
      }
      if (this.currentToken == Token.Dot){
        this.GetNextToken();
        if (this.ExplicitInterfaceImplementationIsAllowable(parentType, id))
          interfaceType = new TypeExpression(id, id.SourceContext);
        if (this.currentToken == Token.This){
          id = new Identifier("Item");
          id.SourceContext = this.scanner.CurrentSourceContext;
        }else{
          id = this.scanner.GetIdentifier();
          this.SkipIdentifierOrNonReservedKeyword();
        }
        if (interfaceType == null) id.SourceContext.Document = null;
      }
    onceMore:
      switch(this.currentToken){
        case Token.This:
          if (interfaceType == null) goto default;
          goto case Token.LeftBrace;
        case Token.LeftBrace:
          this.ParseProperty(parentType, attributes, modifierTokens, modifierContexts, sctx, t, interfaceType, id, followers);
          return;
        case Token.LeftParenthesis:
          this.ParseMethod(parentType, attributes, modifierTokens, modifierContexts, sctx, t, interfaceType, id, followers);
          return;
        case Token.LessThan:
          if (modifierTokens == null || modifierTokens.Length == 0) {
            TypeExpression intfExpr = interfaceType;
            if (intfExpr == null) {
              if (parentType is Class || parentType is Struct)
                intfExpr = new TypeExpression(id, id.SourceContext);
              else
                goto case Token.LeftParenthesis;
            }
            int savedStartPos = this.scanner.startPos;
            ScannerState ss = this.scanner.state;
            int endPos, arity;
            TypeNodeList templateArguments = this.ParseTypeArguments(true, false, followers | Token.Dot | Token.LeftParenthesis | Token.LeftBrace, out endPos, out arity);
            if (templateArguments != null && this.currentToken == Token.Dot) {
              if (intfExpr != null && intfExpr.Expression != id) {
                SourceContext ctx = intfExpr.Expression.SourceContext;
                ctx.EndPos = id.SourceContext.EndPos;
                intfExpr.Expression = new QualifiedIdentifier(intfExpr.Expression, id, ctx);
              }
              intfExpr.TemplateArguments = templateArguments;
              intfExpr.SourceContext.EndPos = endPos;
              interfaceType = intfExpr;
              this.GetNextToken();
              if (this.currentToken == Token.This) {
                id = new Identifier("Item");
                id.SourceContext = this.scanner.CurrentSourceContext;
              } else {
                id = this.scanner.GetIdentifier();
                this.SkipIdentifierOrNonReservedKeyword();
              }
              goto onceMore;
            }
            this.scanner.state = ss;
            this.scanner.endPos = savedStartPos;
            this.currentToken = Token.None;
            this.GetNextToken();
            Debug.Assert(this.currentToken == Token.LessThan);
          }
          goto case Token.LeftParenthesis;
        case Token.Dot:
          if (interfaceType != null){
            this.GetNextToken();
            SourceContext ctx = interfaceType.Expression.SourceContext;
            ctx.EndPos = id.SourceContext.EndPos;
            interfaceType.SourceContext.EndPos = id.SourceContext.EndPos;
            interfaceType.Expression = new QualifiedIdentifier(interfaceType.Expression, id, ctx);
            if (this.currentToken == Token.This) {
              id = new Identifier("Item");
              id.SourceContext = this.scanner.CurrentSourceContext;
            } else {
              id = this.scanner.GetIdentifier();
              this.SkipIdentifierOrNonReservedKeyword();
            }
            goto onceMore;
          }
          goto default;
        default:
          if (interfaceType != null)
            this.ParseMethod(parentType, attributes, modifierTokens, modifierContexts, sctx, t, interfaceType, id, followers);
          else
            this.ParseField(parentType, attributes, modifierTokens, modifierContexts, sctx, t, id, followers);
          return;
      }
    }

    /// <summary>
    /// Parse a modelfield. Requires that the model keyword has been swallowed already.
    /// Adds a new ModelfieldContract to parentType.Contract and, if the modelfield is not an override, adds a Member to parentType that represents the modelfield.
    /// </summary>
    private void ParseModelField(TypeNode parentType, AttributeList attributes, TokenList modifierTokens,
      SourceContextList modifierContexts, object sctx, TokenSet followers)
    {
      //A model field has the shape [new | override | sealed] model T name {ConstraintsAndWitness}.
      //override is not allowed if parentype is Interface.
      //model T name; is accepted as a shorthand for model T name {witness 0;}.
      #region parse type T
      //Expecting type T as currentToken.
      if (this.currentToken == Token.EndOfFile) {
        this.HandleError(Error.ExpectedExpression); return;
      }
      TypeNode type = this.ParseTypeExpression(parentType.ConstructorName, followers | Parser.IdentifierOrNonReservedKeyword);
      #endregion
      #region parse identifier name
      //Expecting identifier f
      if (!Parser.IdentifierOrNonReservedKeyword[this.currentToken]) {
        this.HandleError(Error.ExpectedIdentifier);
        if (this.currentToken != Token.EndOfFile)
          this.SkipTo(followers);
        return;
      }
      Identifier name = this.scanner.GetIdentifier();
      #endregion                
      
      ModelfieldContract mfC = new ModelfieldContract(parentType, attributes, type, name, name.SourceContext);
          
      #region handle modifiers
      bool isNew = false;      
      for (int i = 0, n = modifierTokens.Length; i < n; i++) {
        switch (modifierTokens[i]) {
          case Token.New:
            if (isNew || mfC.IsOverride || parentType is Interface)
              this.HandleError(modifierContexts[i], Error.InvalidModifier, modifierContexts[i].SourceText);
            else {
              isNew = true;
              (mfC.Modelfield as Field).HidesBaseClassMember = true;
            }
            break;
          case Token.Override:
            if (isNew || mfC.IsOverride || parentType is Interface)
              this.HandleError(modifierContexts[i], Error.InvalidModifier, modifierContexts[i].SourceText);
            else {              
              mfC.IsOverride = true;              
            }
            break;
          case Token.Sealed:
            if (mfC.IsSealed)
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, modifierContexts[i].SourceText);
            else
              mfC.IsSealed = true;
            break;
          default:
            this.HandleError(modifierContexts[i], Error.InvalidModifier, modifierContexts[i].SourceText);
            break;
        }
      }
      #endregion
            
      this.GetNextToken(); //now expect either a semicolon, or {ConstraintsAndWitness}     
      if (this.currentToken == Token.Semicolon) {
        this.GetNextToken();
      } else {
        this.Skip(Token.LeftBrace);
        #region Parse zero or more satisfies clauses (satisfies true on zero) and zero or one witness clauses
        while (this.currentToken == Token.Witness || this.currentToken == Token.Satisfies) {
          if (this.currentToken == Token.Witness) {
            if (!mfC.HasExplicitWitness) { //did not parse a witness yet
              this.GetNextToken();
              mfC.Witness = this.ParseExpression(followers | Token.Semicolon);
              mfC.HasExplicitWitness = true;
              this.Skip(Token.Semicolon);
            } else {
              this.HandleError(Error.UnexpectedToken, this.scanner.GetTokenSource()); //there should be at most one witness
              this.GetNextToken(); break;
            }
          } else if (this.currentToken == Token.Satisfies) {
            this.GetNextToken();
            Expression sat = this.ParseExpression(followers | Token.Semicolon);
            if (sat != null && !(parentType is Interface))
              mfC.SatisfiesList.Add(sat);
            else if (parentType is Interface)
              this.HandleError(Error.SatisfiesInInterface, this.scanner.GetTokenSource());
            this.Skip(Token.Semicolon);
          }
        }
        #endregion
        this.Skip(Token.RightBrace);
      }
      if (parentType.Contract == null)
        parentType.Contract = new TypeContract(parentType);
      parentType.Contract.ModelfieldContracts.Add(mfC);
      if (!mfC.IsOverride)
        parentType.Members.Add(mfC.Modelfield);
      //this.SkipSemiColon(followers); a modelfield is not terminated by a ;           
    }

    private void ParseConst(TypeNode parentType, AttributeList attributes, TokenList modifierTokens, SourceContextList modifierContexts, SourceContext sctx, TokenSet followers){
      Debug.Assert(this.currentToken == Token.Const);
      this.GetNextToken();
      TokenSet followersOrCommaOrSemiColon = followers|Token.Comma|Token.Semicolon;
      TypeNode type = this.ParseTypeExpression(null, followersOrCommaOrSemiColon|Parser.IdentifierOrNonReservedKeyword|Token.Assign);
      Identifier name = this.scanner.GetIdentifier();
      Field f = new Field(parentType, attributes, FieldFlags.None, name, type, null);
      f.TypeExpression = type;
      f.SourceContext = sctx;
      FieldFlags flags = this.GetFieldFlags(modifierTokens, modifierContexts, f);
      if (f.IsVolatile){
        f.IsVolatile = false;
        for (int i = 0, n = modifierTokens.Length; i < n; i++){
          Token tok = modifierTokens[i];
          if (tok == Token.Volatile){
            this.HandleError(modifierContexts[i], Error.InvalidModifier, "volatile");
            break;
          }
        }
      }
      f.Documentation = this.LastDocComment;
      f.Flags = flags|FieldFlags.Static|FieldFlags.Literal|FieldFlags.HasDefault;
      if ((flags&FieldFlags.Static) != 0)
        this.HandleError(Error.StaticConstant, parentType.Name+"."+name);
      this.SkipIdentifierOrNonReservedKeyword();
      for(;;){
        if (this.currentToken == Token.Assign)
          this.GetNextToken();
        else if (this.currentToken == Token.LeftBrace && parentType is Interface){
          //might be a mistaken attempt to define a readonly property
          this.HandleError(Error.ConstValueRequired); //TODO: this is as per the C# compiler, but a better message would be nice.
          this.ParseProperty(parentType, attributes, modifierTokens, modifierContexts, sctx, type, null, name, followers);
          return;
        }else{
          this.SkipTo(Parser.UnaryStart|followersOrCommaOrSemiColon, Error.ConstValueRequired);
          if (this.currentToken == Token.Comma) goto carryOn;
          if (!Parser.UnaryStart[this.currentToken]){
            if (followers[this.currentToken]) return;
            break;
          }
        }
        f.Initializer = this.ParseExpression(followersOrCommaOrSemiColon);
        if (this.currentToken != Token.Comma) break;
      carryOn:
        this.GetNextToken();
        parentType.Members.Add(f);
        f = new Field(parentType, attributes, flags|FieldFlags.Static|FieldFlags.Literal|FieldFlags.HasDefault, this.scanner.GetIdentifier(), type, null);
        f.TypeExpression = type;
        this.SkipIdentifierOrNonReservedKeyword();
      }
      parentType.Members.Add(f);
      f.SourceContext.EndPos = this.scanner.endPos;
      this.SkipSemiColon(followers);
      this.SkipTo(followers);
    }
    private Field ParseField(TypeNode parentType, AttributeList attributes, TokenList modifierTokens, 
      SourceContextList modifierContexts, object sctx, TypeNode type, Identifier name, TokenSet followers){
      Field f = new Field(parentType, attributes, FieldFlags.Public, name, type, null);
      f.TypeExpression = type;
      f.Flags = this.GetFieldFlags(modifierTokens, modifierContexts, f);
      f.SourceContext = (SourceContext)sctx;
      f.Documentation = this.LastDocComment;
      TokenSet modifiedFollowers = followers|Token.Comma|Token.Semicolon;
      for (;;){
        if (this.currentToken == Token.Assign){
          bool savedParsingStatement = this.parsingStatement;
          this.parsingStatement = true;
          this.GetNextToken();
          if (this.currentToken == Token.LeftBrace){
            f.Initializer = this.ParseArrayInitializer(type, modifiedFollowers);
          }else
            f.Initializer = this.ParseExpression(modifiedFollowers);
          if (this.currentToken != Token.EndOfFile) this.parsingStatement = savedParsingStatement;
        }
        f.SourceContext.EndPos = this.scanner.endPos;
        parentType.Members.Add(f);
        if (this.currentToken != Token.Comma) break;
        this.GetNextToken();
        name = this.scanner.GetIdentifier();
        f = new Field(parentType, attributes, f.Flags, name, type, null);
        f.TypeExpression = type;
        f.SourceContext = name.SourceContext;
        this.SkipIdentifierOrNonReservedKeyword();
      }
      this.SkipSemiColon(followers);
      return f;
    }
    private FieldFlags GetFieldFlags(TokenList modifierTokens, SourceContextList modifierContexts, Field f){
      FieldFlags result = (FieldFlags)0;
      if (modifierTokens == null) return result;
      for (int i = 0, n = modifierTokens.Length; i < n; i++){
        switch(modifierTokens[i]){
          case Token.New:
            if (f.HidesBaseClassMember)
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "new");             
            f.HidesBaseClassMember = true;
            break;
          case Token.Public:
            FieldFlags access = result & FieldFlags.FieldAccessMask;
            if (access == FieldFlags.Public) 
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "public");             
            else if (access != 0)
              this.HandleError(modifierContexts[i], Error.ConflictingProtectionModifier);
            result |= FieldFlags.Public;
            break;
          case Token.Protected:
            access = result & FieldFlags.FieldAccessMask;
            if (access == FieldFlags.Family || access == FieldFlags.FamORAssem) 
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "protected");
            else if (access == FieldFlags.Assembly){
              result &= ~FieldFlags.Assembly;
              result |= FieldFlags.FamORAssem;
              break;
            }else if (access != 0)
              this.HandleError(modifierContexts[i], Error.ConflictingProtectionModifier);
            result |= FieldFlags.Family; 
            break;
          case Token.Internal: 
            access = result & FieldFlags.FieldAccessMask;
            if (access == FieldFlags.Assembly || access == FieldFlags.FamORAssem) 
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "internal");             
            else if (access == FieldFlags.Family){
              result &= ~FieldFlags.Family;
              result |= FieldFlags.FamORAssem;
              break;
            }else if (access != 0)
              this.HandleError(modifierContexts[i], Error.ConflictingProtectionModifier);
            result |= FieldFlags.Assembly; 
            break;
          case Token.Private: 
            access = result & FieldFlags.FieldAccessMask;
            if (access == FieldFlags.Private) 
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "private");             
            else if (access != 0)
              this.HandleError(modifierContexts[i], Error.ConflictingProtectionModifier);
            result |= FieldFlags.Private; 
            break;
          case Token.Abstract:
            if (modifierContexts[i].Document != null)
              this.HandleError(modifierContexts[i], Error.InvalidModifier, "abstract");
            break;
          case Token.Sealed: 
            this.HandleError(modifierContexts[i], Error.InvalidModifier, "sealed");
            break;
          case Token.Static:
            if ((result & FieldFlags.Static) != 0)
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "static");             
            result |= FieldFlags.Static;
            break;
          case Token.Readonly:
            if ((result & FieldFlags.InitOnly) != 0)
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "readonly");             
            result |= FieldFlags.InitOnly;
            break;
          case Token.Volatile:
            if (f.IsVolatile)
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "volatile");             
            f.IsVolatile = true; 
            break;
          case Token.Virtual:
            this.HandleError(modifierContexts[i], Error.InvalidModifier, "virtual");
            break;
          case Token.Override:
            this.HandleError(modifierContexts[i], Error.InvalidModifier, "override");
            break;
          case Token.Extern:
            this.HandleError(modifierContexts[i], Error.InvalidModifier, "extern");
            break;
          case Token.Unsafe:
            if (!this.allowUnsafeCode){
              this.allowUnsafeCode = true;
              this.HandleError(modifierContexts[i], Error.IllegalUnsafe);
            }
            this.inUnsafeCode = true;
            break;
          default:
            Debug.Assert(f.DeclaringType is Interface);
            this.HandleError(f.Name.SourceContext, Error.InvalidModifier, modifierContexts[i].SourceText);
            break;
        }
      }
      f.IsUnsafe = this.inUnsafeCode;
      if ((result & FieldFlags.FieldAccessMask) == 0)
        result |= FieldFlags.Private;
      return result;
    }
    private TypeNode ParseDelegateDeclaration(Namespace ns, TypeNode parentType, AttributeList attributes, TypeFlags flags, SourceContext sctx, TokenSet followers){
      return this.ParseDelegateDeclaration(ns, parentType, attributes, null, null, flags, sctx, followers);
    }
    private TypeNode ParseDelegateDeclaration(Namespace ns, TypeNode parentType, AttributeList attributes, TokenList modifierTokens, 
      SourceContextList modifierContexts, SourceContext sctx, TokenSet followers){
      return this.ParseDelegateDeclaration(ns, parentType, attributes, modifierTokens, modifierContexts, TypeFlags.None, sctx, followers);
    }
    private TypeNode ParseDelegateDeclaration(Namespace ns, TypeNode parentType, AttributeList attributes, TokenList modifierTokens, 
      SourceContextList modifierContexts, TypeFlags flags, SourceContext sctx, TokenSet followers){
      DelegateNode d = new DelegateNode();
      d.Attributes = attributes;
      if (parentType == null)
        d.DeclaringNamespace = ns;
      else{
        d.DeclaringType = parentType;
        d.IsGeneric = parentType.IsGeneric;
      }
      d.DeclaringModule = this.module;
      d.SourceContext = sctx;
      d.Documentation = this.LastDocComment;
      Debug.Assert(this.currentToken == Token.Delegate);
      this.GetNextToken();
      if (this.currentToken == Token.Void){
        d.ReturnType = this.TypeExpressionFor(Token.Void);
        this.GetNextToken();
      }else
        d.ReturnType = d.ReturnTypeExpression =
          this.ParseTypeOrFunctionTypeExpression(followers|Parser.IdentifierOrNonReservedKeyword|Token.Semicolon, false, false);
      if ((this.currentToken == Token.LeftParenthesis && parentType != null) ||
        (ns == null && parentType == null && attributes == null && modifierTokens == null && flags == TypeFlags.None))
        d.Name = Identifier.Empty;
      else{
        d.Name = this.scanner.GetIdentifier();
        this.SkipIdentifierOrNonReservedKeyword();
        if (modifierTokens != null)
          d.Flags |= this.NestedTypeFlags(modifierTokens, modifierContexts, d)|TypeFlags.Sealed;
        else{
          if ((flags & (TypeFlags.Abstract|TypeFlags.Sealed|TypeFlags.SpecialName)) != 0){
            this.HandleError(d.Name.SourceContext, Error.InvalidModifier, "static");
            flags &= ~(TypeFlags.Abstract|TypeFlags.Sealed|TypeFlags.SpecialName);
          }
          d.IsUnsafe = this.inUnsafeCode;
          d.Flags |= flags|TypeFlags.Sealed;
        }
        if (this.currentToken == Token.LessThan){
          this.ParseTypeParameters(d, followers|Token.LeftParenthesis|Token.RightParenthesis|Token.Semicolon|Token.Where);
          if (Cci.TargetPlatform.GenericTypeNamesMangleChar != 0) {
            int numPars = d.TemplateParameters == null ? 0 : d.TemplateParameters.Count;
            if (numPars > 0)
              d.Name = new Identifier(d.Name.ToString()+Cci.TargetPlatform.GenericTypeNamesMangleChar+numPars.ToString(), d.Name.SourceContext);
          }
        }
        if (parentType != null){
          d.Namespace = Identifier.Empty;
          parentType.Members.Add(d);
        }else{
          d.Namespace = ns.FullNameId;
          ns.Types.Add(d);
          this.AddTypeToModule(d);
        }
      }
      d.Parameters = this.ParseParameters(Token.RightParenthesis, followers|Token.Semicolon|Token.Where, true, false);
      while (this.currentToken == Token.Where)
        this.ParseTypeParameterConstraint(d, followers|Token.Semicolon|Token.Where);
      d.SourceContext.EndPos = this.scanner.endPos;
      if (d.Name != Identifier.Empty){
        this.SkipSemiColon(followers|Parser.TypeMemberStart);
        if (!followers[this.currentToken]) this.SkipTo(followers, Error.NamespaceUnexpected);
        return d;
      }else{
        this.SkipTo(followers|Parser.IdentifierOrNonReservedKeyword);
        return new FunctionTypeExpression(d.ReturnType, d.Parameters);
      }
    }
    private void ParseEnumDeclaration(Namespace ns, TypeNode parentType, AttributeList attributes, TypeFlags flags, SourceContext sctx, TokenSet followers){
      this.ParseEnumDeclaration(ns, parentType, attributes, null, null, flags, sctx, followers);
    }
    private void ParseEnumDeclaration(Namespace ns, TypeNode parentType, AttributeList attributes, TokenList modifierTokens, 
      SourceContextList modifierContexts, SourceContext sctx, TokenSet followers){
      this.ParseEnumDeclaration(ns, parentType, attributes, modifierTokens, modifierContexts, TypeFlags.None, sctx, followers);
    }
    private void ParseEnumDeclaration(Namespace ns, TypeNode parentType, AttributeList attributes, TokenList modifierTokens, 
      SourceContextList modifierContexts, TypeFlags flags, SourceContext sctx, TokenSet followers){
      EnumNode e = new EnumNode();
      e.Attributes = attributes;
      if (parentType == null)
        e.DeclaringNamespace = ns;
      else{
        e.DeclaringType = parentType;
        e.IsGeneric = parentType.IsGeneric;
      }
      if (modifierTokens != null)
        e.Flags |= this.NestedTypeFlags(modifierTokens, modifierContexts, e)|TypeFlags.Sealed;
      else{
        if ((flags & TypeFlags.Abstract) != 0){
          if ((flags & TypeFlags.Sealed) != 0 && (flags & TypeFlags.SpecialName) != 0){
            this.HandleError(Error.InvalidModifier, "static");
            flags &= ~(TypeFlags.Abstract|TypeFlags.Sealed|TypeFlags.SpecialName);
          }else{
            this.HandleError(Error.InvalidModifier, "abstract");
            flags &= ~TypeFlags.Abstract;
          }
        }else if ((flags & TypeFlags.Sealed) != 0){
          this.HandleError(Error.InvalidModifier, "sealed");
          flags &= ~TypeFlags.Sealed;
        }
        e.Flags |= flags|TypeFlags.Sealed;
        e.IsUnsafe = this.inUnsafeCode;
      }
      e.SourceContext = sctx;
      e.DeclaringModule = this.module;
      Debug.Assert(this.currentToken == Token.Enum);
      this.GetNextToken();
      e.Name = this.scanner.GetIdentifier();
      this.SkipIdentifierOrNonReservedKeyword();
      if (parentType != null){
        e.Namespace = Identifier.Empty;
        parentType.Members.Add(e);
      }else{
        e.Namespace = ns.FullNameId;
        ns.Types.Add(e);
        this.AddTypeToModule(e);
        if (this.inUnsafeCode) this.HandleError(e.Name.SourceContext, Error.InvalidModifier, "unsafe");
      }
      TypeNode t = this.TypeExpressionFor(Token.Int);
      if (this.currentToken == Token.Colon){
        this.GetNextToken();
        switch(this.currentToken){
          case Token.Sbyte:
          case Token.Byte:
          case Token.Short:
          case Token.Ushort:
          case Token.Int:
          case Token.Uint:
          case Token.Long:
          case Token.Ulong:
            t = this.TypeExpressionFor(this.currentToken);
            this.GetNextToken();
            break;
          default:
            TypeNode tt = this.ParseTypeExpression(null, followers|Token.LeftBrace);
            if (tt != null)
              this.HandleError(tt.SourceContext, Error.IntegralTypeExpected);
            break;
        }
      }
      e.UnderlyingTypeExpression = t;
      e.SourceContext.EndPos = this.scanner.endPos;
      e.Documentation = this.LastDocComment;
      SourceContext typeBodyCtx = this.scanner.CurrentSourceContext;
      this.Skip(Token.LeftBrace);
      Field prevField = null;
      int offset = 0;
      while (this.currentToken != Token.RightBrace){
        if (this.currentToken == Token.SingleLineDocCommentStart || this.currentToken == Token.MultiLineDocCommentStart)
          this.ParseDocComment(followers|Parser.IdentifierOrNonReservedKeyword|Token.Comma|Token.RightBrace);
        SourceContext ctx = this.scanner.CurrentSourceContext;
        AttributeList attrs = this.ParseAttributes(null, followers|Parser.IdentifierOrNonReservedKeyword|Token.Comma|Token.RightBrace);
        Identifier id = this.scanner.GetIdentifier();
        this.SkipIdentifierOrNonReservedKeyword();
        Field f = new Field(e, attrs, FieldFlags.Public|FieldFlags.Literal|FieldFlags.Static|FieldFlags.HasDefault, id, e, null);
        e.Members.Add(f);
        f.Documentation = this.LastDocComment;
        f.SourceContext = ctx;
        if (this.currentToken == Token.Assign){
          this.GetNextToken();
          if (Parser.UnaryStart[this.currentToken])
            f.Initializer = this.ParseExpression(followers|Token.Comma|Token.RightBrace);
          else{
            this.SkipTo(followers|Token.Comma|Token.RightBrace, Error.ConstantExpected);
            f.Initializer = new Literal(offset++);
          }
          prevField = f;
        }else{
          if (prevField == null)
            f.Initializer = new Literal(offset++);
          else{
            f.Initializer = new BinaryExpression(new MemberBinding(null, prevField), new Literal(1), NodeType.Add, ctx);
            prevField = f;
          }
        }
        if (this.currentToken != Token.Comma){
          if (this.currentToken == Token.Semicolon){
            SourceContext sc = this.scanner.CurrentSourceContext;
            this.GetNextToken();
            if (Parser.IdentifierOrNonReservedKeyword[this.currentToken]){
              this.HandleError(sc, Error.SyntaxError, ",");
              continue;
            }else if (this.currentToken == Token.RightBrace){
              this.HandleError(sc, Error.ExpectedRightBrace);
              break;
            }
          }
          break;
        }
        this.GetNextToken();
      }
      int endCol = this.scanner.endPos;
      if (this.sink != null){
        typeBodyCtx.EndPos = this.scanner.endPos;
        this.sink.AddCollapsibleRegion(typeBodyCtx, false);
      }
      this.ParseBracket(e.SourceContext, Token.RightBrace, followers|Token.Semicolon, Error.ExpectedRightBrace);
      e.SourceContext.EndPos = endCol;
      if (this.currentToken == Token.Semicolon)
        this.GetNextToken();
      this.SkipTo(followers|Parser.TypeMemberStart);
      if (!followers[this.currentToken])
        this.SkipTo(followers, Error.NamespaceUnexpected);
    }
    private void StartTypeName(Token tok){
      string typeName = null;
      switch (tok){
        case Token.Bool: typeName = "Boolean"; break;
        case Token.Decimal: typeName = "Decimal"; break;
        case Token.Sbyte: typeName = "SByte"; break;
        case Token.Byte: typeName = "Byte"; break;
        case Token.Short: typeName = "Int16"; break;
        case Token.Ushort: typeName = "UInt16"; break;
        case Token.Int: typeName = "Int32"; break;
        case Token.Uint: typeName = "UInt32"; break;
        case Token.Long: typeName = "Int64"; break;
        case Token.Ulong: typeName = "UInt64"; break;
        case Token.Char: typeName = "Char"; break;
        case Token.Float: typeName = "Single"; break;
        case Token.Double: typeName = "Double"; break;
        case Token.Object: typeName = "Object"; break;
        case Token.String: typeName = "String"; break;
        case Token.Void: typeName = "Void"; break;
        default: return;
      }
      this.sink.StartName(new Identifier(typeName, this.scanner.CurrentSourceContext));
    }
    private static Expression QualifiedIdentifierFor(params string[] memberNameParts) {
      if (memberNameParts == null || memberNameParts.Length < 1) return null;
      Identifier id = Identifier.For(memberNameParts[0]);
      id.Prefix = Identifier.For("global");
      Expression qualId = id;
      for (int i = 1, n = memberNameParts.Length; i < n; i++)
        qualId = new QualifiedIdentifier(qualId, Identifier.For(memberNameParts[i]));
      return qualId;
    }
    private static TypeExpression TypeExpressionFor(params string[] typeNameParts) {
      return new TypeExpression(QualifiedIdentifierFor(typeNameParts));
    }
    private TypeExpression TypeExpressionFor(Token tok) {
      switch(tok){
        case Token.Bool: return new TypeExpression(new Literal(TypeCode.Boolean), 0, this.scanner.CurrentSourceContext);
        case Token.Decimal: return new TypeExpression(new Literal(TypeCode.Decimal), 0, this.scanner.CurrentSourceContext);
        case Token.Sbyte: return new TypeExpression(new Literal(TypeCode.SByte), 0, this.scanner.CurrentSourceContext);
        case Token.Byte: return new TypeExpression(new Literal(TypeCode.Byte), 0, this.scanner.CurrentSourceContext);
        case Token.Short: return new TypeExpression(new Literal(TypeCode.Int16), 0, this.scanner.CurrentSourceContext);
        case Token.Ushort: return new TypeExpression(new Literal(TypeCode.UInt16), 0, this.scanner.CurrentSourceContext);
        case Token.Int: return new TypeExpression(new Literal(TypeCode.Int32), 0, this.scanner.CurrentSourceContext);
        case Token.Uint: return new TypeExpression(new Literal(TypeCode.UInt32), 0, this.scanner.CurrentSourceContext);
        case Token.Long: return new TypeExpression(new Literal(TypeCode.Int64), 0, this.scanner.CurrentSourceContext);
        case Token.Ulong: return new TypeExpression(new Literal(TypeCode.UInt64), 0, this.scanner.CurrentSourceContext);
        case Token.Char: return new TypeExpression(new Literal(TypeCode.Char), 0, this.scanner.CurrentSourceContext);
        case Token.Float: return new TypeExpression(new Literal(TypeCode.Single), 0, this.scanner.CurrentSourceContext);
        case Token.Double: return new TypeExpression(new Literal(TypeCode.Double), 0, this.scanner.CurrentSourceContext);
        case Token.Object: return new TypeExpression(new Literal(TypeCode.Object), 0, this.scanner.CurrentSourceContext);
        case Token.String: return new TypeExpression(new Literal(TypeCode.String), 0, this.scanner.CurrentSourceContext);
        case Token.Void: return new TypeExpression(new Literal(TypeCode.Empty), 0, this.scanner.CurrentSourceContext);
        default: return null;
      }      
    }
    private InterfaceList ParseInterfaceList(TokenSet followers, bool expectLeftBrace){
      InterfaceList ilist = new InterfaceList();
      TokenSet followersOrComma = followers|Token.Comma;
      for(;;){
        Expression id = this.scanner.GetIdentifier();
        switch(this.currentToken){
          case Token.Bool:
          case Token.Decimal:
          case Token.Sbyte:
          case Token.Byte:
          case Token.Short:
          case Token.Ushort:
          case Token.Int:
          case Token.Uint:
          case Token.Long:
          case Token.Ulong:
          case Token.Char:
          case Token.Float:
          case Token.Double:
          case Token.Object:
          case Token.String:
          case Token.Void:
            TypeExpression texpr = this.TypeExpressionFor(this.currentToken);
            this.GetNextToken();
            ilist.Add(new InterfaceExpression(texpr.Expression, texpr.SourceContext));
            goto lookForComma;
          default:
            bool idOK = Parser.IdentifierOrNonReservedKeyword[this.currentToken];
            if (idOK){
              this.GetNextToken();
              if (this.currentToken == Token.DoubleColon){
                this.GetNextToken();
                Identifier id2 = this.scanner.GetIdentifier();
                id2.Prefix = (Identifier)id;
                id2.SourceContext.StartPos = id.SourceContext.StartPos;
                this.SkipIdentifierOrNonReservedKeyword();
                id = id2;
              }
              if (this.currentToken == Token.Dot)
                id = this.ParseQualifiedIdentifier(id, followersOrComma|Token.LessThan);
            }else{
              int col = this.scanner.endPos;
              this.SkipIdentifierOrNonReservedKeyword(Error.TypeExpected);
              if (col == this.scanner.endPos && this.currentToken != Token.EndOfFile){
                //Did not consume a token, but just gave an error
                if (!followersOrComma[this.currentToken]) this.GetNextToken();
                if (followers[this.currentToken]) return ilist;
                if (this.currentToken != Token.Comma){
                  if (Parser.IdentifierOrNonReservedKeyword[this.currentToken]) continue;
                  break;
                }
                this.GetNextToken();
                continue;
              }
              if (this.currentToken == Token.Dot)
                id = this.ParseQualifiedIdentifier(id, followersOrComma|Token.LessThan);
              if (!idOK) goto lookForComma;
            }
            break;
        }
        //I really want an Identifier here for StartName
        if (this.sink != null) {
          Identifier name = id as Identifier;
          if (id is QualifiedIdentifier) {
            name = ((QualifiedIdentifier)id).Identifier;
          }
          if (name != null) {
            this.sink.StartName(name);
          }
        }
        InterfaceExpression ifaceExpr = new InterfaceExpression(id, id.SourceContext);
        if (this.currentToken == Token.LessThan){
        yetAnotherTypeArgumentList:
          this.GetNextToken();
          TypeNodeList arguments = new TypeNodeList();
          for(;;){
            TypeNode t = this.ParseTypeExpression(null, followers|Token.Comma|Token.GreaterThan);
            arguments.Add(t);
            if (this.currentToken != Token.Comma) break;
            this.GetNextToken();
          }
          ifaceExpr.TemplateArguments = arguments;
          ifaceExpr.TemplateArgumentExpressions = arguments.Clone();
          ifaceExpr.SourceContext.EndPos = this.scanner.endPos;
          this.Skip(Token.GreaterThan);
          if (this.currentToken == Token.Dot) {
            TemplateInstance tempInst = new TemplateInstance(ifaceExpr.Expression, ifaceExpr.TemplateArguments);
            tempInst.TypeArgumentExpressions = ifaceExpr.TemplateArguments == null ? null : ifaceExpr.TemplateArguments.Clone();
            tempInst.SourceContext = ifaceExpr.SourceContext;
            ifaceExpr.Expression = this.ParseQualifiedIdentifier(tempInst, followersOrComma|Token.LessThan);
            ifaceExpr.TemplateArguments = null;
            ifaceExpr.TemplateArgumentExpressions = null;
            if (ifaceExpr.Expression != null) ifaceExpr.SourceContext = ifaceExpr.Expression.SourceContext;
            if (this.currentToken == Token.LessThan) goto yetAnotherTypeArgumentList;
          }
        }
        ilist.Add(ifaceExpr);
      lookForComma:
        if (Parser.TypeOperator[this.currentToken] && !(expectLeftBrace && this.currentToken == Token.LeftBrace)){
          this.HandleError(Error.BadBaseType);
          this.GetNextToken();
          if (this.currentToken == Token.RightBracket || this.currentToken == Token.RightBrace)
            this.GetNextToken();
          this.SkipTo(followersOrComma, Error.None);
        }else if (!followersOrComma[this.currentToken])
          this.SkipTo(followersOrComma, Error.TypeExpected);
        if (this.currentToken == Token.Comma){
          if (followers[Token.Comma] && followers[Token.GreaterThan])
            break; //Parsing the constraint of a type parameter
          this.GetNextToken();
          if (expectLeftBrace && (this.currentToken == Token.Class || this.currentToken == Token.Struct || this.currentToken == Token.New))
            break;
        }else if (!Parser.TypeStart[this.currentToken] || this.currentToken == Token.Where)
          break;
        else if (Parser.ContractStart[this.currentToken])
          break;
      }
      return ilist;
    }   
    private Expression ParseQualifiedIdentifier(MemberBinding mb, TokenSet followers){
      mb.SourceContext = this.scanner.CurrentSourceContext;
      this.GetNextToken();
      if (this.currentToken == Token.Dot)
        return this.ParseQualifiedIdentifier((Expression)mb, followers);
      this.SkipTo(followers, Error.UnexpectedToken); //TODO: better error message
      return mb;
    }
    private Expression ParseQualifiedIdentifier(Expression qualifier, TokenSet followers){
      return this.ParseQualifiedIdentifier(qualifier, followers, false);
    }
    private Expression ParseQualifiedIdentifier(Expression qualifier, TokenSet followers, bool returnNullIfError){
      Debug.Assert(this.currentToken == Token.Dot);
      SourceContext dotContext = this.scanner.CurrentSourceContext;
      this.GetNextToken();
      Expression result = null;
      Identifier id = null;
      if (this.insideModifiesClause && this.currentToken == Token.Multiply) {
        SourceContext sctx = qualifier.SourceContext;
        sctx.EndPos = this.scanner.CurrentSourceContext.EndPos;
        this.GetNextToken();
        if (this.currentToken == Token.Multiply) {
          // Handle code such as
          //
          //    modifies myObject.**;
          //
          // which means that the method may modify all fields of all peers of myObject.
          sctx.EndPos = this.scanner.CurrentSourceContext.EndPos;
          result = new ModifiesPeersClause(qualifier, sctx);
          this.GetNextToken(); // eat the second asterisk
        } else {
          // Handle code such as
          //
          //    modifies myObject.*;
          //
          // which means that the method may modify all fields of myObject.
          result = new ModifiesObjectClause(qualifier, sctx);
        }
        return result;
      }
      if (this.sink != null && Parser.IdentifierOrNonReservedKeyword[this.currentToken])
        this.sink.QualifyName(dotContext, this.scanner.GetIdentifier());
      TypeNode tn = this.TypeExpressionFor(this.currentToken);
      if (tn != null){
        id = this.scanner.GetIdentifier();
        this.GetNextToken();
      }else{
        id = this.scanner.GetIdentifier();
        if (this.currentToken == Token.Dot){
          this.HandleError(Error.ExpectedIdentifier);
          while( this.currentToken == Token.Dot)
            this.GetNextToken();
          return qualifier;
        }else
          this.SkipIdentifierOrNonReservedKeyword();
      }
      result = new QualifiedIdentifier(qualifier, id, id.SourceContext);
      if (qualifier != null)
        result.SourceContext.StartPos = qualifier.SourceContext.StartPos;
      if (this.currentToken == Token.Dot) return this.ParseQualifiedIdentifier(result, followers, returnNullIfError);
      if (returnNullIfError && !followers[this.currentToken]) return null;
      this.SkipTo(followers);
      return result;
    }
    private Identifier ParsePrefixedIdentifier(){
      Identifier id = this.scanner.GetIdentifier();
      if (Parser.IdentifierOrNonReservedKeyword[this.currentToken] && this.scanner.ScannedNamespaceSeparator())
        id = this.ParseNamePart(id);
      if (this.currentToken == Token.Dot){
        this.HandleError(Error.ExpectedIdentifier);
        this.GetNextToken();
      }else
        this.SkipIdentifierOrNonReservedKeyword();
      return id;
    }
    private Identifier ParseNamePart(Identifier prefix){
      this.GetNextToken();
      Identifier qid = this.scanner.GetIdentifier();
      qid.Prefix = prefix;
      qid.SourceContext.StartPos = prefix.SourceContext.StartPos;      
      return qid;
    }
    private void ParseConstructor(TypeNode parentType, AttributeList attributes, TokenList modifierTokens, 
      SourceContextList modifierContexts, object sctx, SourceContext idCtx, TokenSet followers){
      InstanceInitializer c = new InstanceInitializer(parentType, attributes, null, null, this.TypeExpressionFor(Token.Void));
      this.currentCtor = c;
      c.Name = new Identifier(".ctor", idCtx);
      MethodFlags flags = this.GetMethodFlags(modifierTokens, modifierContexts, parentType, c);
      if ((flags & MethodFlags.Static) != 0){
        this.currentCtor = null; // Can you call "base" in a static ctor?
        this.ParseStaticConstructor(parentType, attributes, modifierTokens, modifierContexts, flags, sctx, idCtx, followers);
        return;
      }
      parentType.Members.Add(c);
      c.Flags |= flags|MethodFlags.HideBySig;
      c.Parameters = this.ParseParameters(Token.RightParenthesis, followers|Token.LeftBrace|Token.Semicolon|Token.Colon|Parser.ContractStart|Token.Where);
      c.HasCompilerGeneratedSignature = false;
      c.Documentation = this.LastDocComment;
      QualifiedIdentifier supCons = new QualifiedIdentifier(new Base(), StandardIds.Ctor, this.scanner.CurrentSourceContext);
      MethodCall superConstructorCall = new MethodCall(supCons, null, NodeType.Call);
      superConstructorCall.SourceContext = this.scanner.CurrentSourceContext;
      StatementList slist = new StatementList();
      Block body = new Block(slist, this.insideCheckedBlock, this.insideUncheckedBlock, this.inUnsafeCode);
      body.SourceContext = this.scanner.CurrentSourceContext;
      Block iblock = new Block(new StatementList(), this.insideCheckedBlock, this.insideUncheckedBlock, this.inUnsafeCode);
      if (this.currentToken == Token.Colon){
        this.GetNextToken();
        bool savedParsingStatement = this.parsingStatement;
        this.parsingStatement = true;
        superConstructorCall.SourceContext = this.scanner.CurrentSourceContext;
        supCons.SourceContext = this.scanner.CurrentSourceContext;
        bool init = false;
        this.inInstanceConstructor = BaseOrThisCallKind.ColonThisOrBaseSeen;
        if (this.currentToken == Token.This){
          if (this.sink != null) this.sink.StartName(new Identifier(".ctor", this.scanner.CurrentSourceContext));
          supCons.Qualifier = new This(this.scanner.CurrentSourceContext, true);
          this.GetNextToken();
        }else if (this.currentToken == Token.Base){
          if (parentType.IsValueType)
            this.HandleError(Error.StructWithBaseConstructorCall, new ErrorHandler(this.errors).GetMemberSignature(c));
          else if (this.sink != null)
            this.sink.StartName(new Identifier(".ctor", this.scanner.CurrentSourceContext));
          supCons.Qualifier = new Base(this.scanner.CurrentSourceContext, true);
          this.GetNextToken();
        }else{
          if (!init)
            this.SkipTo(followers|Token.LeftBrace|Token.Semicolon|Parser.ContractStart|Token.Where, Error.ThisOrBaseExpected);
          if (this.currentToken != Token.EndOfFile) this.parsingStatement = savedParsingStatement;
          goto parseBody;
        }
        SourceContext lpCtx = this.scanner.CurrentSourceContext;
        this.Skip(Token.LeftParenthesis);
        superConstructorCall.Operands = this.ParseArgumentList(followers|Token.LeftBrace|Token.Semicolon|Parser.ContractStart|Token.Where, lpCtx, out superConstructorCall.SourceContext.EndPos);
        if (this.currentToken != Token.EndOfFile) this.parsingStatement = savedParsingStatement;
      } else {
        // no colon ==> no "base" or "this" before body of ctor
        if (! parentType.IsValueType)
          this.inInstanceConstructor = BaseOrThisCallKind.None;
      }
    parseBody:
      superConstructorCall.SourceContext.EndPos = this.scanner.endPos;
      supCons.SourceContext.EndPos = this.scanner.endPos;
      bool swallowedSemicolonAlready = false;
      this.ParseMethodContract(c, followers|Token.LeftBrace|Token.Semicolon, ref swallowedSemicolonAlready);
      Block b;
      if (this.parsingContractAssembly)
        b = this.ParseBody(c, sctx, followers, swallowedSemicolonAlready); // only allow semicolon body in contract assemblies
      else
        b = this.ParseBody(c, sctx, followers);
      slist.Add(iblock);
      c.IsDeferringConstructor = supCons.Qualifier is This || this.inInstanceConstructor == BaseOrThisCallKind.InCtorBodyThisSeen;
      if (!c.IsDeferringConstructor){
        slist.Add(new FieldInitializerBlock(parentType,false));
      }
      Block baseOrDeferringCallBlock = new Block(new StatementList(1));
      c.BaseOrDefferingCallBlock = baseOrDeferringCallBlock;
      slist.Add(baseOrDeferringCallBlock);
      if (this.inInstanceConstructor == BaseOrThisCallKind.None || this.inInstanceConstructor == BaseOrThisCallKind.ColonThisOrBaseSeen){
        if (!(parentType.IsValueType || this.TypeIsSystemObject(parentType)) || supCons.Qualifier is This)
          baseOrDeferringCallBlock.Statements.Add(new ExpressionStatement(superConstructorCall, superConstructorCall.SourceContext));
      }
      if (b != null){
        slist.Add(b);
        body.SourceContext.EndPos = b.SourceContext.EndPos;
        if ((c.Flags & MethodFlags.PInvokeImpl) != 0 && b.Statements != null && b.Statements.Count > 0)
          body = null;
        else if (this.omitBodies) 
          b.Statements = null;
      }else if ((c.Flags & MethodFlags.PInvokeImpl) != 0)
        body = null;
      c.Body = body;
      this.inInstanceConstructor = BaseOrThisCallKind.Disallowed;
      this.currentCtor = null;
    }
    private void ParseStaticConstructor(TypeNode parentType, AttributeList attributes, TokenList modifierTokens, 
      SourceContextList modifierContexts, MethodFlags flags, object sctx, SourceContext idCtx, TokenSet followers){
      if (parentType is Interface){
        this.HandleError(idCtx, Error.InterfacesCannotContainConstructors);
      }else{
        for (int i = 0, n = modifierTokens.Length; i < n; i++){
          Token tok = modifierTokens[i];
          if (tok == Token.Static || tok == Token.Extern) continue;
          if (tok == Token.Unsafe){
            if (!this.allowUnsafeCode){
              this.allowUnsafeCode = true;
              this.HandleError(modifierContexts[i], Error.IllegalUnsafe);
            }
            this.inUnsafeCode = true;
            continue;
          }
          this.HandleError(modifierContexts[i], Error.StaticConstructorWithAccessModifiers);
        }
      }
      StaticInitializer c = new StaticInitializer(parentType, attributes, 
        new Block(new StatementList(2), this.insideCheckedBlock, this.insideUncheckedBlock, this.inUnsafeCode), this.TypeExpressionFor(Token.Void));
      c.Name = new Identifier(".cctor", idCtx);
      parentType.Members.Add(c);
      c.Flags |= flags;
      c.HasCompilerGeneratedSignature = false;
      ParameterList plist = c.Parameters = this.ParseParameters(Token.RightParenthesis, followers|Token.LeftBrace|Token.Colon);
      if (plist != null && plist.Count > 0){
        this.HandleError(plist[0].SourceContext, Error.StaticConstParam);
        c.Parameters = null;
      }
      if (this.currentToken == Token.Colon){
        SourceContext ctx = this.scanner.CurrentSourceContext;
        this.GetNextToken();
        if (this.currentToken == Token.This || this.currentToken == Token.Base){
          ctx.EndPos = this.scanner.endPos;
          this.GetNextToken();
          if (this.currentToken == Token.LeftParenthesis){
            SourceContext lpCtx = this.scanner.CurrentSourceContext;
            this.Skip(Token.LeftParenthesis);
            this.ParseArgumentList(followers|Token.LeftBrace, lpCtx, out ctx.EndPos);
          }
        }else
          this.SkipTo(followers|Token.LeftBrace, Error.None);
        this.HandleError(ctx, Error.StaticConstructorWithExplicitConstructorCall);
      }
      Block b = this.ParseBody(c, sctx, followers);
      c.Body.Statements.Add(new FieldInitializerBlock(parentType, true));
      c.Body.Statements.Add(b);
    }
    private void ParseOperator(TypeNode parentType, AttributeList attributes, TokenList modifierTokens,
      SourceContextList modifierContexts, TypeNode resultType, object sctx, TokenSet followers){
      if (parentType is Interface)
        this.HandleError(Error.InterfacesCantContainOperators);
      Identifier opName = null;
      SourceContext ctx = this.scanner.CurrentSourceContext;
      SourceContext symCtx = ctx;
      bool canBeBinary = false;
      bool canBeUnary = false;
      switch(this.currentToken){
        case Token.Explicit:
          opName = new Identifier("op_Explicit", ctx);
          canBeUnary = true;
          this.GetNextToken();
          opName.SourceContext.EndPos = this.scanner.endPos;
          this.Skip(Token.Operator);
          if (resultType != null && this.currentToken == Token.LeftParenthesis)
            this.HandleError(opName.SourceContext, Error.BadOperatorSyntax, "explicit");
          else
            resultType = this.ParseTypeExpression(null, followers|Token.LeftParenthesis);
          break;
        case Token.Implicit:
          opName = new Identifier("op_Implicit", ctx);
          canBeUnary = true;
          this.GetNextToken();
          opName.SourceContext.EndPos = this.scanner.endPos;
          this.Skip(Token.Operator);
          if (resultType != null && this.currentToken == Token.LeftParenthesis)
            this.HandleError(opName.SourceContext, Error.BadOperatorSyntax, "implicit");
          else
            resultType = this.ParseTypeExpression(null, followers|Token.LeftParenthesis);
          break;
        case Token.Operator:{
          this.GetNextToken();
          symCtx = this.scanner.CurrentSourceContext;
          SourceContext opCtxt = ctx;
          opCtxt.EndPos = this.scanner.endPos;
          switch (this.currentToken){
            case Token.Plus:
              canBeBinary = true;
              canBeUnary = true;
              opName = new Identifier("op_Addition", opCtxt);
              break;
            case Token.Subtract:
              canBeBinary = true;
              canBeUnary = true;
              opName = new Identifier("op_Subtraction", opCtxt);
              break;
            case Token.Multiply:
              canBeBinary = true;
              opName = new Identifier("op_Multiply", opCtxt);
              break;
            case Token.Divide:
              canBeBinary = true;
              opName = new Identifier("op_Division", opCtxt);
              break;
            case Token.Remainder:
              canBeBinary = true;
              opName = new Identifier("op_Modulus", opCtxt);
              break;
            case Token.BitwiseAnd:
              canBeBinary = true;
              opName = new Identifier("op_BitwiseAnd", opCtxt);
              break;
            case Token.BitwiseOr:
              canBeBinary = true;
              opName = new Identifier("op_BitwiseOr", opCtxt);
              break;
            case Token.BitwiseXor:
              canBeBinary = true;
              opName = new Identifier("op_ExclusiveOr", opCtxt);
              break;
            case Token.LeftShift:
              canBeBinary = true;
              opName = new Identifier("op_LeftShift", opCtxt);
              break;
            case Token.RightShift:
              canBeBinary = true;
              opName = new Identifier("op_RightShift", opCtxt);
              break;
            case Token.Equal:
              canBeBinary = true;
              opName = new Identifier("op_Equality", opCtxt);
              break;
            case Token.NotEqual:
              canBeBinary = true;
              opName = new Identifier("op_Inequality", opCtxt);
              break;
            case Token.GreaterThan:
              canBeBinary = true;
              opName = new Identifier("op_GreaterThan", opCtxt);
              break;
            case Token.LessThan:
              canBeBinary = true;
              opName = new Identifier("op_LessThan", opCtxt);
              break;
            case Token.GreaterThanOrEqual:
              canBeBinary = true;
              opName = new Identifier("op_GreaterThanOrEqual", opCtxt);
              break;
            case Token.LessThanOrEqual:
              canBeBinary = true;
              opName = new Identifier("op_LessThanOrEqual", opCtxt);
              break;
            case Token.LogicalNot:
              canBeUnary = true;
              opName = new Identifier("op_LogicalNot", opCtxt);
              break;
            case Token.BitwiseNot:
              canBeUnary = true;
              opName = new Identifier("op_OnesComplement", opCtxt);
              break;
            case Token.AddOne:
              canBeUnary = true;
              opName = new Identifier("op_Increment", opCtxt);
              break;
            case Token.SubtractOne:
              canBeUnary = true;
              opName = new Identifier("op_Decrement", opCtxt);
              break;
            case Token.True:
              canBeUnary = true;
              opName = new Identifier("op_True", opCtxt);
              break;
            case Token.False:
              canBeUnary = true;
              opName = new Identifier("op_False", opCtxt);
              break;
            case Token.Hole: //HS D
              canBeUnary = true;
              opName = new Identifier("op_Hole", opCtxt);
              break;
            case Token.Implicit:
              canBeUnary = true;
              opName = new Identifier("op_Implicit", opCtxt);
              this.HandleError(opName.SourceContext, Error.BadOperatorSyntax, "implicit");
              break;
            case Token.Explicit:
              canBeUnary = true;
              opName = new Identifier("op_Explicit", opCtxt);
              this.HandleError(opName.SourceContext, Error.BadOperatorSyntax, "explicit");
              break;
          }
          if (this.currentToken != Token.EndOfFile) this.GetNextToken();
          if (resultType == null){
            this.HandleError(Error.BadOperatorSyntax2, opCtxt.SourceText);
            if (this.currentToken != Token.LeftParenthesis)
              resultType = this.ParseTypeExpression(null, followers|Token.LeftParenthesis);
            else
              resultType = parentType;
          }
          break;}
        default:
          Debug.Assert(false);
          break;
      }
      //Parse the parameter list
      SourceContext pctx = this.scanner.CurrentSourceContext;
      ParameterList parameters = this.ParseParameters(Token.RightParenthesis, followers|Token.LeftBrace|Token.Semicolon|Token.Requires|Token.Modifies|Token.Ensures|Token.Where|Token.Throws);
      switch (parameters.Count){
        case 1:
          if (!canBeUnary)
            this.HandleError(symCtx, Error.OvlUnaryOperatorExpected);
          if (canBeBinary && opName != null){
            if (opName.UniqueIdKey == StandardIds.opAddition.UniqueIdKey) opName = new Identifier("op_UnaryPlus", opName.SourceContext);
            else if (opName.UniqueIdKey == StandardIds.opSubtraction.UniqueIdKey) opName = new Identifier("op_UnaryNegation", opName.SourceContext);
          }
          break;
        case 2:
          if (!canBeBinary)
            if (canBeUnary)
              this.HandleError(pctx, Error.WrongParsForUnaryOp, opName.SourceContext.SourceText);
            else
              this.HandleError(symCtx, Error.OvlBinaryOperatorExpected);
          break;
        default:
          if (canBeBinary)
            this.HandleError(pctx, Error.WrongParsForBinOp, opName.SourceContext.SourceText);
          else if (canBeUnary)
            this.HandleError(pctx, Error.WrongParsForUnaryOp, opName.SourceContext.SourceText);
          else
            this.HandleError(symCtx, Error.OvlBinaryOperatorExpected);
          break;
      }
      ctx.EndPos = this.scanner.endPos;
      MethodFlags flags = MethodFlags.Public;
      if (!(parentType is Interface))
        flags = this.GetOperatorFlags(modifierTokens, modifierContexts, ctx);
      else
        flags |= MethodFlags.SpecialName|MethodFlags.Static;
      Method oper = new Method(parentType, attributes, opName, parameters, resultType, null);
      oper.ReturnTypeExpression = resultType;
      oper.Flags = flags;
      oper.IsUnsafe = this.inUnsafeCode;
      bool swallowedSemicolonAlready = false;
      this.ParseMethodContract(oper, followers|Token.LeftBrace|Token.Semicolon, ref swallowedSemicolonAlready);
      oper.Body = this.ParseBody(oper, sctx, followers, swallowedSemicolonAlready);
      oper.HasCompilerGeneratedSignature = false;
      parentType.Members.Add(oper);
      oper.Documentation = this.LastDocComment;
    }
    private MethodFlags GetOperatorFlags(TokenList modifierTokens, SourceContextList modifierContexts, SourceContext ctx){
      MethodFlags result = (MethodFlags)0;
      for (int i = 0, n = modifierTokens.Length; i < n; i++){
        switch(modifierTokens[i]){
          case Token.Public:
            if ((result & MethodFlags.MethodAccessMask) == MethodFlags.Public) 
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "public");             
            result |= MethodFlags.Public;
            break;
          case Token.Static:
            if ((result & MethodFlags.Static) != 0)
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "static");             
            result |= MethodFlags.Static;
            break;
          case Token.Extern:
            if ((result & MethodFlags.PInvokeImpl) != 0)
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "extern");
            result |= MethodFlags.PInvokeImpl;
            break;
          case Token.Unsafe:
            if (!this.allowUnsafeCode){
              this.allowUnsafeCode = true;
              this.HandleError(modifierContexts[i], Error.IllegalUnsafe);
            }
            this.inUnsafeCode = true;
            break;
          default:
            //TODO: search for duplicate, preferentially complain about that for backwards compat with C#
            this.HandleError(modifierContexts[i], Error.InvalidModifier, modifierContexts[i].SourceText);
            break;
        }
      }
      if (result != (MethodFlags.Public|MethodFlags.Static) &&
          result != (MethodFlags.Public|MethodFlags.Static|MethodFlags.PInvokeImpl)){
        this.HandleError(ctx, Error.OperatorsMustBeStatic, ctx.SourceText);
        result |= MethodFlags.Public|MethodFlags.Static;
      }
      return result|MethodFlags.SpecialName|MethodFlags.HideBySig;
    }
    private bool TypeIsSystemObject(TypeNode type) {
      return this.options != null && this.options.NoStandardLibrary &&
        type.Name.UniqueIdKey == StandardIds.CapitalObject.UniqueIdKey && type.Namespace.UniqueIdKey == StandardIds.System.UniqueIdKey;
    }
    private void ParseDestructor(TypeNode parentType, AttributeList attributes, TokenList modifierTokens,
      SourceContextList modifierContexts, object sctx, TokenSet followers){
      Method meth = new Method(parentType, attributes, new Identifier("Finalize"), null, this.TypeExpressionFor(Token.Void), null);
      meth.OverridesBaseClassMember = !this.TypeIsSystemObject(parentType);
      meth.Documentation = this.LastDocComment;
      meth.Flags = this.GetDestructorFlags(modifierTokens, modifierContexts, meth);
      meth.CallingConvention = CallingConventionFlags.HasThis;
      Debug.Assert(this.currentToken == Token.BitwiseNot);
      this.GetNextToken();
      if (!(parentType is Class)) 
        this.HandleError(Error.OnlyClassesCanContainDestructors);
      else
        parentType.Members.Add(meth);
      Identifier id = this.scanner.GetIdentifier();
      meth.Name.SourceContext = id.SourceContext;
      this.SkipIdentifierOrNonReservedKeyword();
      if (id.UniqueIdKey != parentType.Name.UniqueIdKey)
        this.HandleError(Error.WrongNameForDestructor);
      ParameterList pars = this.ParseParameters(Token.RightParenthesis, followers|Token.LeftBrace);
      if (pars != null && pars.Count > 0 && pars[0] != null)
        this.HandleError(pars[0].SourceContext, Error.ExpectedRightParenthesis);
      Block b = this.ParseBody(meth, sctx, followers);
      if (b != null && !this.TypeIsSystemObject(parentType)) {
        StatementList stats = new StatementList(1);
        stats.Add(new ExpressionStatement(new MethodCall(new QualifiedIdentifier(new Base(), StandardIds.Finalize), null)));
        Try t = new Try(b, null, null, null, new Finally(new Block(stats, this.insideCheckedBlock, this.insideUncheckedBlock, this.inUnsafeCode)));
        stats = new StatementList(1);
        stats.Add(t);
        meth.Body = new Block(stats, this.insideCheckedBlock, this.insideUncheckedBlock, this.inUnsafeCode);
      } else
        meth.Body = b;
    }
    private MethodFlags GetDestructorFlags(TokenList modifierTokens, SourceContextList modifierContexts, Member destructor){
      MethodFlags result = MethodFlags.Family|MethodFlags.HideBySig|MethodFlags.Virtual;
      int n = modifierTokens.Length;
      if (n == 0) return result;
      int firstNonExtern = -1;
      int secondNonExtern = -1;
      for (int i = 0; i < n; i++){
        switch (modifierTokens[i]){
          case Token.Extern:
            if ((result & MethodFlags.PInvokeImpl) != 0){
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "extern");
              return result;
            }
            result |= MethodFlags.PInvokeImpl;
            break;
          case Token.Unsafe:
            if (!this.allowUnsafeCode){
              this.allowUnsafeCode = true;
              this.HandleError(modifierContexts[i], Error.IllegalUnsafe);
            }
            this.inUnsafeCode = true;
            break;
          default:
            if (firstNonExtern == -1) firstNonExtern = i;
            else if (secondNonExtern == -1) secondNonExtern = i;
            break;
        }
      }
      destructor.IsUnsafe = this.inUnsafeCode;
      if (secondNonExtern >= 0 && Parser.ProtectionModifier[modifierTokens[secondNonExtern]])
        this.HandleError(modifierContexts[secondNonExtern], Error.ConflictingProtectionModifier);
      else if (firstNonExtern >= 0)
        this.HandleError(modifierContexts[firstNonExtern], Error.InvalidModifier, modifierContexts[firstNonExtern].SourceText);
      return result;
    }
    private void ParseEvent(TypeNode parentType, AttributeList attributes, TokenList modifierTokens, 
      SourceContextList modifierContexts, object sctx, TokenSet followers){
      Debug.Assert(this.currentToken == Token.Event);
      this.GetNextToken();
      Event e = new Event(parentType, attributes, EventFlags.None, null, null, null, null, null);
      e.DeclaringType = parentType;
      e.Documentation = this.LastDocComment;
      TypeNode t = this.ParseTypeExpression(Identifier.Empty, followers|Parser.IdentifierOrNonReservedKeyword);
      //REVIEW: allow events with anonymous delegate type?
      e.HandlerType = e.HandlerTypeExpression = t;
      TypeExpression interfaceType = null;
      Identifier id = this.scanner.GetIdentifier();
      this.SkipIdentifierOrNonReservedKeyword();
      TypeNodeList templateArguments = null;
      int endPos = 0, arity = 0;
      while (this.currentToken == Token.Dot || this.currentToken == Token.LessThan) {
        if (interfaceType == null && this.ExplicitInterfaceImplementationIsAllowable(parentType, id)) {
          for (int i = 0, n = modifierContexts == null ? 0 : modifierContexts.Length; i < n; i++){
            this.HandleError(modifierContexts[i], Error.InvalidModifier, modifierContexts[i].SourceText);
            modifierTokens = new TokenList(0);
          }
        }
        TypeExpression intfExpr = interfaceType;
        if (intfExpr == null) {
          intfExpr = new TypeExpression(id, id.SourceContext);
        } else if( templateArguments == null) {
          SourceContext ctx = intfExpr.Expression.SourceContext;
          ctx.EndPos = id.SourceContext.EndPos;
          intfExpr.Expression = new QualifiedIdentifier(intfExpr.Expression, id, ctx);
        }
        if (templateArguments != null) {
          intfExpr.TemplateArguments = templateArguments;
          intfExpr.SourceContext.EndPos = endPos;
        }
        if (this.currentToken == Token.LessThan) {
          templateArguments = this.ParseTypeArguments(true, false, followers | Token.Dot | Token.LeftParenthesis | Token.LeftBrace, out endPos, out arity);
        } else {  //  Dot
          templateArguments = null;
          this.GetNextToken();
          id = this.scanner.GetIdentifier();
          this.SkipIdentifierOrNonReservedKeyword();
          if (intfExpr == null) id.SourceContext.Document = null;
        }
        interfaceType = intfExpr;
      }
      e.Name = id;
      MethodFlags mflags = this.GetMethodFlags(modifierTokens, modifierContexts, parentType, e);
      if ((mflags & MethodFlags.Static) != 0 && parentType is Interface){
        this.HandleError(id.SourceContext, Error.InvalidModifier, "static");
        mflags &= ~MethodFlags.Static;
        mflags |= MethodFlags.Abstract;
      }
      e.HandlerFlags = mflags|MethodFlags.SpecialName;
      bool hasAccessors = this.currentToken == Token.LeftBrace || interfaceType != null;
      if (hasAccessors){
        if (interfaceType != null){
          e.ImplementedTypeExpressions = e.ImplementedTypes = new TypeNodeList(interfaceType);
          if (this.currentToken != Token.LeftBrace){
            this.HandleError(Error.ExplicitEventFieldImpl);
            hasAccessors = false;
            goto nextDeclarator;
          }
        }
        this.Skip(Token.LeftBrace);
        TokenSet followersOrRightBrace = followers|Token.RightBrace;
        bool alreadyGivenAddOrRemoveExpectedError = false;
        bool alreadyComplainedAboutAccessors = false;
        for(;;){
          SourceContext sc = this.scanner.CurrentSourceContext;
          AttributeList accessorAttrs = this.ParseAttributes(null, followers|Parser.AddOrRemoveOrModifier|Token.LeftBrace);
          switch (this.currentToken){
            case Token.Add:
              if (parentType is Interface && !alreadyComplainedAboutAccessors){
                this.HandleError(Error.EventPropertyInInterface, parentType.FullName+"."+id);
                alreadyComplainedAboutAccessors = true;
              }else if (e.HandlerAdder != null)
                this.HandleError(Error.DuplicateAccessor);
              SourceContext scntx = this.scanner.CurrentSourceContext;
              this.GetNextToken();
              ParameterList parList = new ParameterList();
              parList.Add(new Parameter(null, ParameterFlags.None, StandardIds.Value, t, null, null));
              Method m = new Method(parentType, accessorAttrs, new Identifier("add_"+id.ToString()), parList, this.TypeExpressionFor(Token.Void), null);
              m.HidesBaseClassMember = e.HidesBaseClassMember;
              m.OverridesBaseClassMember = e.OverridesBaseClassMember;
              m.Name.SourceContext = scntx;
              if ((mflags & MethodFlags.Static) == 0) 
                m.CallingConvention = CallingConventionFlags.HasThis;
              m.Flags = mflags|MethodFlags.HideBySig|MethodFlags.SpecialName;
              if (interfaceType != null){
                m.Flags = MethodFlags.Private|MethodFlags.HideBySig|MethodFlags.NewSlot|MethodFlags.Final|MethodFlags.Virtual|MethodFlags.SpecialName;
                m.ImplementedTypeExpressions = m.ImplementedTypes = new TypeNodeList(interfaceType);
              }
              if (this.currentToken != Token.LeftBrace){
                this.SkipTo(followersOrRightBrace|Token.Remove, Error.AddRemoveMustHaveBody);
                alreadyGivenAddOrRemoveExpectedError = true;
              }else
                m.Body = this.ParseBody(m, sc, followersOrRightBrace|Token.Remove);
              if (!(parentType is Interface)){
                e.HandlerAdder = m;
                m.DeclaringMember = e;
                parentType.Members.Add(m);
              }
              continue;
            case Token.Remove:
              if (parentType is Interface && !alreadyComplainedAboutAccessors){
                this.HandleError(Error.EventPropertyInInterface, parentType.FullName+"."+id);
                alreadyComplainedAboutAccessors = true;
              }else if (e.HandlerRemover != null)
                this.HandleError(Error.DuplicateAccessor);
              scntx = this.scanner.CurrentSourceContext;
              this.GetNextToken();
              parList = new ParameterList();
              parList.Add(new Parameter(null, ParameterFlags.None, StandardIds.Value, t, null, null));
              m = new Method(parentType, accessorAttrs, new Identifier("remove_"+id.ToString()), parList, this.TypeExpressionFor(Token.Void), null);
              m.HidesBaseClassMember = e.HidesBaseClassMember;
              m.OverridesBaseClassMember = e.OverridesBaseClassMember;
              m.Name.SourceContext = scntx;
              if ((mflags & MethodFlags.Static) == 0) 
                m.CallingConvention = CallingConventionFlags.HasThis;
              m.Flags = mflags|MethodFlags.HideBySig|MethodFlags.SpecialName;
              if (interfaceType != null){
                m.Flags = MethodFlags.Private|MethodFlags.HideBySig|MethodFlags.NewSlot|MethodFlags.Final|MethodFlags.Virtual|MethodFlags.SpecialName;
                m.ImplementedTypeExpressions = m.ImplementedTypes = new TypeNodeList(interfaceType);                
              }
              if (this.currentToken != Token.LeftBrace){
                this.SkipTo(followersOrRightBrace|Token.Add, Error.AddRemoveMustHaveBody);
                alreadyGivenAddOrRemoveExpectedError = true;
              }else
                m.Body = this.ParseBody(m, sc, followersOrRightBrace|Token.Add);
              if (!(parentType is Interface)){
                e.HandlerRemover = m;
                m.DeclaringMember = e;
                parentType.Members.Add(m);
              }
              continue;
            case Token.New:
            case Token.Public:
            case Token.Protected:
            case Token.Internal: 
            case Token.Private: 
            case Token.Abstract: 
            case Token.Sealed: 
            case Token.Static:
            case Token.Readonly:
            case Token.Volatile:
            case Token.Virtual:
            case Token.Override:
            case Token.Extern:
            case Token.Unsafe:
              this.HandleError(Error.NoModifiersOnAccessor);
              this.GetNextToken();
              break;
            default:
              if ((e.HandlerAdder == null || e.HandlerRemover == null) && this.sink != null && this.currentToken == Token.Identifier && this.scanner.endPos == this.scanner.maxPos){
                e.SourceContext.EndPos = this.scanner.startPos;
                KeywordCompletionList keywords;
                if (e.HandlerAdder != null)
                  keywords = new KeywordCompletionList(this.scanner.GetIdentifier(), new KeywordCompletion("remove"));
                else if (e.HandlerRemover != null)
                  keywords = new KeywordCompletionList(this.scanner.GetIdentifier(), new KeywordCompletion("add"));
                else
                  keywords = new KeywordCompletionList(this.scanner.GetIdentifier(), new KeywordCompletion("add"), new KeywordCompletion("remove"));
                parentType.Members.Add(keywords);
                this.GetNextToken();
              }
              if (!alreadyGivenAddOrRemoveExpectedError && !alreadyComplainedAboutAccessors && (e.HandlerAdder == null || e.HandlerRemover == null)) {
                if (this.currentToken == Token.RightBrace)
                  this.HandleError(id.SourceContext, Error.EventNeedsBothAccessors, parentType.FullName+"."+id.Name);
                else
                  this.HandleError(Error.AddOrRemoveExpected);
                alreadyGivenAddOrRemoveExpectedError = true;
                if (!(Parser.EndOfFile|Token.LeftBrace|Token.RightBrace)[this.currentToken])
                  this.GetNextToken();
                this.SkipTo(followersOrRightBrace|Token.LeftBrace, Error.None);
                if (this.currentToken == Token.LeftBrace){
                  this.ParseBlock(followersOrRightBrace|Token.Add|Token.Remove);
                  continue;
                }
              }
              break;
          }
          break;
        }
        this.Skip(Token.RightBrace); //TODO: brace matching
      }
      nextDeclarator:
        e.Name = id;
      e.SourceContext = (SourceContext)sctx;
      e.SourceContext.EndPos = this.scanner.endPos;
      parentType.Members.Add(e);
      if (!hasAccessors){
        switch(this.currentToken){
          case Token.Assign:
            this.GetNextToken();
            e.InitialHandler = this.ParseExpression(followers|Token.Semicolon);
            if (parentType is Interface && e.InitialHandler != null){
              this.HandleError(e.InitialHandler.SourceContext, Error.InterfaceEventInitializer, parentType.FullName+"."+id);
              e.InitialHandler = null;
            }
            if (this.currentToken == Token.Comma)
              goto case Token.Comma;
            else
              goto default;
          case Token.Comma:
            this.GetNextToken();
            id = this.scanner.GetIdentifier();
            this.SkipIdentifierOrNonReservedKeyword(); //REVIEW: allow interface name?
            e = new Event(parentType, attributes, (EventFlags)0, null, null, null, null, null);
            e.HandlerFlags = mflags;
            e.HandlerType = e.HandlerTypeExpression = t;
            goto nextDeclarator;
          default: 
            this.Skip(Token.Semicolon);
            break;
        }
      }
    }
    private void ParseMethod(TypeNode parentType, AttributeList attributes, TokenList modifierTokens,       
      SourceContextList modifierContexts, object sctx, TypeNode type,
      TypeNode interfaceType, Identifier name, TokenSet followers){
      Method m = new Method(parentType, attributes, name, null, type, null);
      m.SourceContext = (SourceContext)sctx;
      m.ReturnTypeExpression = type;
      parentType.Members.Add(m);
      if (interfaceType != null){
        m.ImplementedTypeExpressions = m.ImplementedTypes = new TypeNodeList(interfaceType);
        m.Flags = MethodFlags.Private|MethodFlags.HideBySig|MethodFlags.NewSlot|MethodFlags.Final|MethodFlags.Virtual;
        for (int i = 0, n = modifierContexts == null ? 0 : modifierContexts.Length; i < n; i++){
          if (modifierTokens[i] == Token.Extern)
            m.Flags |= MethodFlags.PInvokeImpl;
          else if (modifierTokens[i] == Token.Unsafe) {
            if (!this.allowUnsafeCode) {
              this.allowUnsafeCode = true;
              this.HandleError(modifierContexts[i], Error.IllegalUnsafe);
            }
            this.inUnsafeCode = true;
          } else
            this.HandleError(modifierContexts[i], Error.InvalidModifier, modifierContexts[i].SourceText);
        }
      }else
        m.Flags = this.GetMethodFlags(modifierTokens, modifierContexts, parentType, m)|MethodFlags.HideBySig;
      if ((m.Flags & MethodFlags.Static) == 0)
        m.CallingConvention = CallingConventionFlags.HasThis;
      else if (parentType is Interface){
        this.HandleError(name.SourceContext, Error.InvalidModifier, "static");
        m.Flags &= ~MethodFlags.Static;
        m.Flags |= MethodFlags.Abstract;
      }
      m.Documentation = this.LastDocComment;
      if (this.currentToken == Token.LessThan)
        this.ParseTypeParameters(m, followers|Token.LeftParenthesis|Token.LeftBrace);     
      m.Parameters = this.ParseParameters(Token.RightParenthesis, followers|Token.LeftBrace|Token.Semicolon|Token.Requires|Token.Modifies|Token.Ensures|Token.Where|Token.Throws);
      while (this.currentToken == Token.Where)
        this.ParseTypeParameterConstraint(m, followers|Token.LeftBrace|Token.Semicolon|Token.Requires|Token.Modifies|Token.Ensures|Token.Where|Token.Throws);
      m.HasCompilerGeneratedSignature = false;
      bool swallowedSemicolonAlready = false;
      bool abstractMethod = false;
      if (this.currentToken == Token.Semicolon) {
        // allow for "f(...); requires P; ensures Q;" for interface methods and abstract methods
        // I.e., a semi-colon after the parameter list. The "swallowedSemicolonAlready" flag
        // was an attempt to allow "f(...) requires P;", but for C#-compatibility mode where
        // the contracts are in C# comments, that forces the last semi to be on a separate line
        // and the C# formatter makes it look ugly.
        abstractMethod = true;
        swallowedSemicolonAlready = true;
        m.SourceContext.EndPos = this.scanner.endPos;
        this.GetNextToken();
      }
      this.ParseMethodContract(m, followers|Token.LeftBrace|Token.Semicolon, ref swallowedSemicolonAlready);
      if (!abstractMethod) {
        m.Body = this.ParseBody(m, sctx, followers, swallowedSemicolonAlready);
        if (m.Body != null) {
          m.SourceContext.EndPos = m.Body.SourceContext.EndPos;	  
	}
      } else if (!swallowedSemicolonAlready) {
	  m.SourceContext.EndPos = this.scanner.endPos;
	  this.SkipSemiColon(followers);
      }
    }
    
    static private bool InEnsuresContext = false;
    private void ParseMethodContract(Method m, TokenSet followers, ref bool swallowedSemicolonAlready){
      bool savedParsingStatement = this.parsingStatement;
      if (this.currentToken != Token.EndOfFile) this.parsingStatement = true;
      if (!swallowedSemicolonAlready) m.SourceContext.EndPos = this.scanner.endPos;
      MethodContract mc = new MethodContract(m);
      SourceContext initialSourceContext = this.scanner.CurrentSourceContext;
      while ( Parser.ContractStart[this.currentToken] ) {
        SourceContext ctx = this.scanner.CurrentSourceContext;
        Node n = null;
        int finalPos = 0;
        switch ( this.currentToken ) {
          case Token.Requires: {
            this.GetNextToken();
            if (this.currentToken == Token.LeftBrace){
              this.HandleError(Error.ExpectedExpression);
              break; // without this, the left bracket gets parsed as an anonymous nested function
            }
            Expression e = this.ParseExpression(followers|ContractStart|Token.Otherwise);
            if (mc.Requires == null) mc.Requires = new RequiresList();
            if (this.currentToken != Token.Otherwise) {
              Requires r = new RequiresPlain(e);
              n = r;
              mc.Requires.Add(r);
            }else {
              this.Skip(Token.Otherwise);
              Expression e2 = this.ParseExpression(followers|ContractStart);
              Requires r = new RequiresOtherwise(e,e2);
              n = r;
              mc.Requires.Add(r);
            }
            finalPos = this.scanner.CurrentSourceContext.EndPos;
            swallowedSemicolonAlready= (this.currentToken == Token.Semicolon);
            this.SkipSemiColon(followers|ContractStart);
            break;
          }
          case Token.Modifies: {
            // modifies expressions have their source context set here within this
            // case, so don't use the variable "n" to hold on to the AST otherwise
            // it will have the wrong source context set for it at the end of the switch
            // statement
            n = null;
            this.insideModifiesClause = true;
            list : {
              this.GetNextToken(); // Token.Modifies or Token.Comma
              SourceContext sctx = this.scanner.CurrentSourceContext;
              Expression e = this.ParseExpression(followers | ContractStart | Token.Comma);
              if (mc.Modifies == null) mc.Modifies = new ExpressionList();
              if (e != null) { // REVIEW: does this just silently miss errors?
                sctx.EndPos = e.SourceContext.EndPos;
                ModifiesClause modClause = e as ModifiesClause;
                if (modClause != null) {
                  e.SourceContext = sctx;
                }
                else {
                  e = new UnaryExpression(e, NodeType.RefAddress, sctx);
                }
                mc.Modifies.Add(e);                
              }
              if (this.currentToken == Token.Comma)
                goto list;
            }
            swallowedSemicolonAlready= (this.currentToken == Token.Semicolon);
            finalPos = this.scanner.CurrentSourceContext.EndPos;
            this.SkipSemiColon(followers|ContractStart);
            this.insideModifiesClause = false;
            break;
          }
          case Token.Ensures: {
            InEnsuresContext = true;
            this.GetNextToken();
            if (this.currentToken == Token.LeftBrace){
              this.HandleError(Error.ExpectedExpression);
              break; // without this, the left bracket gets parsed as an anonymous nested function
            }
            Expression e = this.ParseExpression(followers|ContractStart);
            if (mc.Ensures == null) mc.Ensures = new EnsuresList();
            EnsuresNormal en = new EnsuresNormal(e);
            n = en;
            mc.Ensures.Add(en);
            finalPos = this.scanner.CurrentSourceContext.EndPos;
            swallowedSemicolonAlready= (this.currentToken == Token.Semicolon);
            this.SkipSemiColon(followers|ContractStart);
            InEnsuresContext = false;
            break;
          }
          case Token.Throws: {
            this.GetNextToken();
            // throws (E1) ensures P;
            // throws (E1 e) ensures P;
            // throws E1 ensures P;
            // throws E1, E2, ...;
            // Note, for constuctors, only the last of these forms is allowed.
            if (mc.Ensures == null) {
              mc.Ensures = new EnsuresList();
              // Note, this list may be left empty in case of parsing errors below.
            }
            EnsuresExceptional exc = new EnsuresExceptional();
            exc.SourceContext = this.scanner.CurrentSourceContext;

            bool hasLeftParen = false;
            if (this.currentToken == Token.LeftParenthesis) {
              hasLeftParen = true;
              this.Skip(Token.LeftParenthesis);
            }
            exc.Type = exc.TypeExpression = this.ParseTypeExpression(Identifier.Empty, followers|Token.Identifier|Token.RightParenthesis|ContractStart);
            if (hasLeftParen && Parser.IdentifierOrNonReservedKeyword[this.currentToken]) {
              exc.Variable = this.scanner.GetIdentifier();
              exc.Variable.Type = exc.Type;
              this.GetNextToken();
            }else{
              exc.Variable = null; // need to be able to distinguish whether the source contains a variable or not
            }
            if (hasLeftParen) {
              this.Skip(Token.RightParenthesis);
            }

            if (hasLeftParen || this.currentToken == Token.Ensures) {
              // throws (E1) ensures P;
              // throws (E1 e) ensures P;
              // throws E1 ensures P;
              SourceContext ctxEnsures = this.scanner.CurrentSourceContext;
              this.Skip(Token.Ensures);
              InEnsuresContext = true;
              Expression ens = this.ParseExpression(followers|ContractStart);
              InEnsuresContext = false;
              // Do the constructor check now.  This is rather late, since the whole throws...ensures
              // has now been parsed, but this may lead to better parse-error recovery.
              if (m is InstanceInitializer) {
                this.HandleError(ctxEnsures, Error.ThrowsEnsuresOnConstructor);
                // ignore what was parsed
                exc.PostCondition = new Literal(true, null, ctx);
              }else{
                exc.PostCondition = ens;
              }
              mc.Ensures.Add(exc);
            }else{
              // throws E1, E2, ...;
//              exc.PostCondition = new Literal(true, null, ctx);
              mc.Ensures.Add(exc);

              while (this.currentToken == Token.Comma) {
                this.GetNextToken();
                exc = new EnsuresExceptional();
                exc.SourceContext = this.scanner.CurrentSourceContext;
                exc.Type = exc.TypeExpression = this.ParseTypeExpression(Identifier.Empty, followers|Token.Comma|ContractStart);
                exc.Variable = new Local(TypeExpressionFor("System", "Exception"));
                exc.Variable.SourceContext = ctx;
                exc.PostCondition = new Literal(true, null, ctx);
                mc.Ensures.Add(exc);
              }
            }

            finalPos = this.scanner.CurrentSourceContext.EndPos;
            swallowedSemicolonAlready= (this.currentToken == Token.Semicolon);
            this.SkipSemiColon(followers|ContractStart);
            n = exc;
            break;
          }
          
        }
        if (n != null) {
          n.SourceContext= ctx;
          n.SourceContext.EndPos = finalPos ;
        }
        m.SourceContext.EndPos = finalPos;
      }
      // What error to generate here?
      if (!followers[this.currentToken])
        this.SkipTo(followers);
      if (initialSourceContext.EndPos != this.scanner.CurrentSourceContext.EndPos) {
        // then a contract really was parsed
        m.Contract = mc;
      }
      if (this.currentToken != Token.EndOfFile) this.parsingStatement = savedParsingStatement;
    }
    private MethodFlags GetMethodFlags(TokenList modifierTokens, SourceContextList modifierContexts, TypeNode type, Member member){
      MethodFlags result = MethodFlags.HideBySig;
      if (type is Interface){
        result |= MethodFlags.Public|MethodFlags.Abstract|MethodFlags.NewSlot|MethodFlags.Virtual;
        for(int i = 0, n = modifierTokens.Length; i < n; i++){
          MethodFlags access = result & MethodFlags.MethodAccessMask;
          switch(modifierTokens[i]){
            case Token.New:
              for (int j = 0; j < i; j++){
                switch(modifierTokens[j]){
                  case Token.New:
                    this.HandleError(modifierContexts[i], Error.DuplicateModifier, "new");
                    break;
                }
              }
              member.HidesBaseClassMember = true;
              i += 2;
              break;
            case Token.Static:
              if (n == 3) return MethodFlags.Static;
              goto default;
            case Token.Unsafe:
              if (!this.allowUnsafeCode){
                this.allowUnsafeCode = true;
                this.HandleError(modifierContexts[i], Error.IllegalUnsafe);
              }
              this.inUnsafeCode = true;
              break;
            default:
              this.HandleError(modifierContexts[i], Error.InvalidModifier, modifierContexts[i].SourceText);
              break;
          }
        }
        return result;
      }
      for(int i = 0, n = modifierTokens.Length; i < n; i++){
        MethodFlags access = result & MethodFlags.MethodAccessMask;
        switch(modifierTokens[i]){
          case Token.Public:
            if (access != 0){
              result &= ~MethodFlags.MethodAccessMask;
              if (access == MethodFlags.Public) 
                this.HandleError(modifierContexts[i], Error.DuplicateModifier, "public");             
              else
                this.HandleError(modifierContexts[i], Error.ConflictingProtectionModifier);
            }
            result |= MethodFlags.Public;
            break;
          case Token.Protected:
            if (access != 0){
              result &= ~MethodFlags.MethodAccessMask;
              if (access == MethodFlags.Family || access == MethodFlags.FamORAssem) 
                this.HandleError(modifierContexts[i], Error.DuplicateModifier, "protected");
              else if (access == MethodFlags.Assembly){
                result |= MethodFlags.FamORAssem;
                break;
              }else
                this.HandleError(modifierContexts[i], Error.ConflictingProtectionModifier);
            }
            result |= MethodFlags.Family; 
            break;
          case Token.Internal: 
            if (access != 0){
              result &= ~MethodFlags.MethodAccessMask;
              if (access == MethodFlags.Assembly || access == MethodFlags.FamORAssem) 
                this.HandleError(Error.DuplicateModifier, "internal");             
              else if (access == MethodFlags.Family){
                result |= MethodFlags.FamORAssem;
                break;
              }else
                this.HandleError(modifierContexts[i], Error.ConflictingProtectionModifier);
            }
            result |= MethodFlags.Assembly; 
            break;
          case Token.Private: 
            if (access != 0){
              result &= ~MethodFlags.MethodAccessMask;
              if (access == MethodFlags.Private) 
                this.HandleError(modifierContexts[i], Error.DuplicateModifier, "private");             
              else
                this.HandleError(modifierContexts[i], Error.ConflictingProtectionModifier);
            }
            if ((result & MethodFlags.Virtual) != 0){
              string offendingMember = type.FullName;
              if (member != null) 
                offendingMember = offendingMember + "." + member.Name;
              else
                offendingMember = offendingMember + "." + type.Name;
              this.HandleError(modifierContexts[i], Error.VirtualPrivate, offendingMember);
              break;
            }
            result |= MethodFlags.Private; 
            break;
          case Token.Sealed: 
            if ((result & MethodFlags.Final) != 0){
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "sealed");
              break;
            }
            for (int j = 0; j < n; j++){
              switch(modifierTokens[j]){
                case Token.Override:
                  result |= MethodFlags.Final;
                  break;
              }
            }
            if ((result & MethodFlags.Final) == 0){
              if (member != null)
                this.HandleError(modifierContexts[i], Error.SealedNonOverride, type.FullName+"."+member.Name);
            }
            break;
          case Token.Static:
            if ((result & MethodFlags.Static) != 0)
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "static");
            if (member is Property && member.Name.UniqueIdKey == StandardIds.Item.UniqueIdKey)
              this.HandleError(modifierContexts[i], Error.InvalidModifier, "static");
            result |= MethodFlags.Static;
            break;
          case Token.Readonly:
            this.HandleError(Error.InvalidModifier, "readonly");
            break;
          case Token.Volatile:
            this.HandleError(Error.InvalidModifier, "volatile");
            break;
          case Token.New:
            for (int j = 0; j < i; j++){
              switch(modifierTokens[j]){
                case Token.New:
                  this.HandleError(modifierContexts[i], Error.DuplicateModifier, "new");
                  break;
                case Token.Override:
                  string offendingMember = type.FullName;
                  if (member != null) 
                    offendingMember = offendingMember + "." + member.Name;
                  else
                    offendingMember = offendingMember + "." + type.Name;
                  if (member is Property)
                    this.HandleError(modifierContexts[i], Error.CannotMarkOverridePropertyNewOrVitual, offendingMember);
                  else if (member is Method)
                    this.HandleError(modifierContexts[i], Error.CannotMarkOverrideMethodNewOrVirtual, offendingMember);
                  break;
              }
            }
            member.HidesBaseClassMember = true;
            break;
          case Token.Abstract:
            if (type is Struct){
              this.HandleError(modifierContexts[i], Error.InvalidModifier, modifierContexts[i].SourceText);
              break;
            }
            for (int j = 0; j < i; j++){
              switch(modifierTokens[j]){
                case Token.Abstract:
                  this.HandleError(modifierContexts[i], Error.DuplicateModifier, "abstract");
                  break;
                case Token.Virtual:
                  string offendingMember = type.FullName;
                  if (member != null) 
                    offendingMember = offendingMember + "." + member.Name;
                  else
                    offendingMember = offendingMember + "." + type.Name;
                  if (member is Property)
                    this.HandleError(modifierContexts[j], Error.CannotMarkAbstractPropertyVirtual, offendingMember);
                  else
                    this.HandleError(modifierContexts[j], Error.AbstractNotVirtual, offendingMember);
                  break;
                case Token.Private:
                  offendingMember = type.FullName;
                  if (member != null) 
                    offendingMember = offendingMember + "." + member.Name;
                  else
                    offendingMember = offendingMember + "." + type.Name;
                  this.HandleError(modifierContexts[j], Error.VirtualPrivate, offendingMember);
                  break;
              }
            }
            result |= MethodFlags.Abstract | MethodFlags.Virtual | MethodFlags.CheckAccessOnOverride;
            if (!member.OverridesBaseClassMember) { result |= MethodFlags.NewSlot; }
            break;
          case Token.Virtual:
            if (type is Struct){
              this.HandleError(modifierContexts[i], Error.InvalidModifier, modifierContexts[i].SourceText);
              break;
            }
            if (type.IsSealed){
              this.HandleError(modifierContexts[i], Error.NewVirtualInSealed, modifierContexts[i].SourceText);
              break;
            }
            for (int j = 0; j < i; j++){
              switch(modifierTokens[j]){
                case Token.Virtual:
                  this.HandleError(modifierContexts[i], Error.DuplicateModifier, "virtual");
                  break;
                case Token.Abstract:
                  string offendingMember = type.FullName;
                  if (member != null) 
                    offendingMember = offendingMember + "." + member.Name;
                  else
                    offendingMember = offendingMember + "." + type.Name;
                  if (member is Property)
                    this.HandleError(modifierContexts[i], Error.CannotMarkAbstractPropertyVirtual, offendingMember);
                  else
                    this.HandleError(modifierContexts[i], Error.AbstractNotVirtual, offendingMember);
                  break;
                case Token.Override:
                  offendingMember = type.FullName;
                  if (member != null) 
                    offendingMember = offendingMember + "." + member.Name;
                  else
                    offendingMember = offendingMember + "." + type.Name;
                  this.HandleError(modifierContexts[i], Error.CannotMarkOverrideMethodNewOrVirtual, offendingMember);
                  break;
                case Token.Private:
                  offendingMember = type.FullName;
                  if (member != null) 
                    offendingMember = offendingMember + "." + member.Name;
                  else
                    offendingMember = offendingMember + "." + type.Name;
                  this.HandleError(modifierContexts[j], Error.VirtualPrivate, offendingMember);
                  break;
              }
            }
            result |= MethodFlags.Virtual|MethodFlags.NewSlot|MethodFlags.CheckAccessOnOverride;            
            break;
          case Token.Override:
            for (int j = 0; j < i; j++){
              switch(modifierTokens[j]){
                case Token.Override:
                  this.HandleError(modifierContexts[i], Error.DuplicateModifier, "override");
                  break;
                case Token.New:
                case Token.Virtual:
                  string offendingMember = type.FullName;
                  if (member != null) 
                    offendingMember = offendingMember + "." + member.Name;
                  else
                    offendingMember = offendingMember + "." + type.Name;
                  if (member is Property)
                    this.HandleError(modifierContexts[j], Error.CannotMarkOverrideMethodNewOrVirtual, offendingMember);
                  else
                    this.HandleError(modifierContexts[j], Error.CannotMarkOverrideMethodNewOrVirtual, offendingMember);
                  break;
                case Token.Private:
                  offendingMember = type.FullName;
                  if (member != null) 
                    offendingMember = offendingMember + "." + member.Name;
                  else
                    offendingMember = offendingMember + "." + type.Name;
                  this.HandleError(modifierContexts[j], Error.VirtualPrivate, offendingMember);
                  break;
              }
            }
            result &= ~(MethodFlags.VtableLayoutMask);
            result |= MethodFlags.Virtual|MethodFlags.ReuseSlot;
            result &= ~(MethodFlags.NewSlot);
            member.OverridesBaseClassMember = true;
            break;
          case Token.Extern:
            if ((result & MethodFlags.PInvokeImpl) != 0)
              this.HandleError(modifierContexts[i], Error.DuplicateModifier, "extern");             
            result |= MethodFlags.PInvokeImpl;
            break;
          case Token.Unsafe:
            if (!this.allowUnsafeCode){
              this.allowUnsafeCode = true;
              this.HandleError(modifierContexts[i], Error.IllegalUnsafe);
            }
            this.inUnsafeCode = true;
            break;
	  //HS D
          // case Token.Operation:
	  //     result |= MethodFlags.Operation;
	  //     break;
	  //HS D
          // case Token.Transformable:
	  //     result |= MethodFlags.Transformable;
	  //     break;
          default:
            Debug.Assert(false);
            this.HandleError(member.Name.SourceContext, Error.InvalidModifier, modifierContexts[i].SourceText);
            break;
        }
      }
      if ((result & MethodFlags.MethodAccessMask) == 0){
        if ((result & MethodFlags.Virtual) != 0){
          for (int i = 0, n = modifierTokens.Length; i < n; i++){
            switch (modifierTokens[i]){
              case Token.Abstract:
              case Token.Virtual:
                string offendingMember = type.FullName;
                if (member != null) 
                  offendingMember = offendingMember + "." + member.Name;
                else
                  offendingMember = offendingMember + "." + type.Name;
                this.HandleError(modifierContexts[i], Error.VirtualPrivate, offendingMember);
                return result | MethodFlags.Private;
            }
          }
        }
        result |= MethodFlags.Private;
      }
      member.IsUnsafe = this.inUnsafeCode;
      return result;
    }
    /// <summary>
    /// </summary>
    /// <param name="arglist">On exit, it means arglist was parsed</param>
    private ParameterList ParseParameters(Token closingToken, TokenSet followers){
      return this.ParseParameters(closingToken, followers, false, false);
    }
    private ParameterList ParseParameters(Token closingToken, TokenSet followers, bool namesAreOptional, bool typesAreOptional){
      bool arglist = false;
      Debug.Assert(closingToken == Token.RightParenthesis || closingToken == Token.RightBracket);
      SourceContext sctx = this.scanner.CurrentSourceContext;
      if (closingToken == Token.RightParenthesis){
        if (this.currentToken != Token.LeftParenthesis){
          this.SkipTo(followers|Parser.UnaryStart, Error.SyntaxError, "(");
        }
        if (this.currentToken == Token.LeftParenthesis) 
          this.GetNextToken();        
      }else{
        if (this.currentToken != Token.LeftBracket)
          this.SkipTo(followers|Parser.UnaryStart, Error.SyntaxError, "[");
        if (this.currentToken == Token.LeftBracket)
          this.GetNextToken();
      }
      ParameterList result = new ParameterList();
      if (this.currentToken != closingToken && this.currentToken != Token.EndOfFile){
        bool allowRefParameters = closingToken == Token.RightParenthesis;
        TokenSet followersOrCommaOrRightParenthesis = followers|Token.Comma|closingToken|Token.ArgList;
        int counter = 0;
        for (;;){
          if (this.currentToken == Token.ArgList) {
            Parameter ap = new Parameter(StandardIds.__Arglist, this.TypeExpressionFor(Token.Void));
            ap.SourceContext = this.scanner.CurrentSourceContext;
            this.GetNextToken();
            arglist = true;
            result.Add(ap);
            break;
          }
          Parameter p = this.ParseParameter(followersOrCommaOrRightParenthesis, allowRefParameters, namesAreOptional, typesAreOptional);
          if (namesAreOptional && p != null && p.Name == Identifier.Empty){
            p.Name = new Identifier("p"+counter++);
            if (p.Type != null)
              p.Name.SourceContext = p.Type.SourceContext;
          }
          if (typesAreOptional && p != null && p.Type == null) allowRefParameters = false;
          result.Add(p);
          if (this.currentToken == Token.Comma)
            this.GetNextToken();
          else
            break;
        }
      }
      if (closingToken == Token.RightBracket) {
        if (result.Count == 0)
          this.HandleError(Error.IndexerNeedsParam);
        else if (arglist) {
          this.HandleError(result[result.Count-1].SourceContext, Error.NoArglistInIndexers);
          result.Count = result.Count-1;
        }
      }
      if (this.currentToken == closingToken){
        if (this.sink != null)
          this.sink.MatchPair(sctx, this.scanner.CurrentSourceContext);
        this.GetNextToken();
        this.SkipTo(followers);
      }else{
        this.SkipTo(followers, closingToken==Token.RightBracket ? Error.ExpectedRightBracket : Error.ExpectedRightParenthesis);
      }
      return result;
    }
    private Parameter ParseParameter(TokenSet followers, bool allowRefParameters, bool namesAreOptional, bool typesAreOptional){
      Parameter p = new Parameter();
      p.SourceContext = this.scanner.CurrentSourceContext;
      Token tok = this.currentToken;
      p.Attributes = this.ParseAttributes(null, followers|Parser.ParameterTypeStart);
      p.Flags = ParameterFlags.None;
      bool byRef = false;
      switch(this.currentToken){
        case Token.Out: 
          //TODO: error if !allowRefParameters && typesAreOptional
          p.Flags = ParameterFlags.Out;
          goto case Token.Ref;
        case Token.Ref:
          //TODO: error if !allowRefParameters && typesAreOptional
          if (!allowRefParameters)
            this.HandleError(Error.IndexerWithRefParam);
          byRef = true;
          this.GetNextToken();
          if (this.currentToken == Token.Params){
            this.HandleError(Error.ParamsCantBeRefOut);
            this.GetNextToken();
          }
          break;
        case Token.Params:
          //TODO: error if !allowRefParameters && typesAreOptional
          Literal lit = new Literal(TypeExpressionFor("System", "ParamArrayAttribute"), null, this.scanner.CurrentSourceContext);
          AttributeNode paramsAttribute = new AttributeNode(lit, null);
          if (p.Attributes == null) p.Attributes = new AttributeList(1);
          p.Attributes.Add(paramsAttribute);
          this.GetNextToken();
          if (this.currentToken == Token.Out || this.currentToken == Token.Ref){
            this.HandleError(Error.ParamsCantBeRefOut);
            if (this.currentToken == Token.Out) goto case Token.Out;
            goto case Token.Ref;
          }
          break;
	  // //HS D
          // case Token.Transformable:
	  //     p.Flags = ParameterFlags.Transformable;
	  //     this.GetNextToken();
	  //     break;
      }
      bool voidParam = false;
      if (this.currentToken == Token.Void){
        if (this.inUnsafeCode){
          TypeNode voidT = this.TypeExpressionFor(Token.Void);
          SourceContext sctx = this.scanner.CurrentSourceContext;
          this.GetNextToken();
          sctx.EndPos = this.scanner.endPos;
          this.Skip(Token.Multiply);
          p.Type = p.TypeExpression = new PointerTypeExpression(voidT, sctx);
        }else{
          this.HandleError(Error.NoVoidParameter);
          p.Type = this.TypeExpressionFor(Token.Object);
          p.TypeExpression = new TypeExpression(this.scanner.GetIdentifier(), this.scanner.CurrentSourceContext);
          this.GetNextToken();
          voidParam = true;
        }
      }else if (this.currentToken == Token.Delegate)
        p.Type = p.TypeExpression = this.ParseDelegateDeclaration(null, null, null, TypeFlags.None, p.SourceContext, followers);
      else if (p.Type == null){
        p.Type = p.TypeExpression = this.ParseTypeExpression(null, followers|Parser.IdentifierOrNonReservedKeyword);
      }
      if (byRef) p.Type = p.TypeExpression = new ReferenceTypeExpression(p.Type);
      if ((this.currentToken == Token.Comma || this.currentToken == Token.RightParenthesis) && p.TypeExpression is TypeExpression &&
        ((TypeExpression)p.TypeExpression).Expression is Identifier && typesAreOptional) {
        p.Name = (Identifier)((TypeExpression)p.TypeExpression).Expression;
        p.Type = p.TypeExpression = null;
      }else{
        p.Name = this.scanner.GetIdentifier();
      }
      p.SourceContext.EndPos = this.scanner.endPos;
      if (!voidParam || Parser.IdentifierOrNonReservedKeyword[this.currentToken]){
        if (Parser.IdentifierOrNonReservedKeyword[this.currentToken])
          this.GetNextToken();
        else{
          if (namesAreOptional)
            p.Name = Identifier.Empty;
          else
            this.SkipIdentifierOrNonReservedKeyword();
        }
      }
      if (this.currentToken == Token.LeftBracket){
        this.HandleError(Error.BadArraySyntax);
        int endPos = this.scanner.endPos;
        int rank = this.ParseRankSpecifier(true, followers|Token.LeftBracket);
        if (rank > 0)
          p.Type = p.TypeExpression = this.ParseArrayType(rank, p.Type, followers); 
        else{
          this.currentToken = Token.LeftBracket;
          this.scanner.endPos = endPos;
          this.GetNextToken();
          while (!this.scanner.TokenIsFirstAfterLineBreak && 
            this.currentToken != Token.RightBracket && this.currentToken != Token.Comma && this.currentToken != Token.RightParenthesis) 
            this.GetNextToken();
          if (this.currentToken == Token.RightBracket) this.GetNextToken();
        }
      }else if (this.currentToken == Token.Assign){
        this.HandleError(Error.NoDefaultArgs);
        this.GetNextToken();
        if (Parser.UnaryStart[this.currentToken]){
          this.ParseExpression(followers);
          return p;
        }
      }
      this.SkipTo(followers);
      return p;
    }
    private void ParseProperty(TypeNode parentType, AttributeList attributes, TokenList modifierTokens, 
      SourceContextList modifierContexts, object sctx, TypeNode type, TypeNode interfaceType, Identifier name, TokenSet followers){
      SourceContext ctx = (SourceContext)sctx;
      ctx.EndPos = this.scanner.endPos;
      bool isIndexer = this.currentToken == Token.This && name.UniqueIdKey == StandardIds.Item.UniqueIdKey;
      Debug.Assert(this.currentToken == Token.LeftBrace || isIndexer);
      this.GetNextToken();
      ParameterList paramList = null;
      if (isIndexer){
        if (interfaceType == null){
          AttributeNode defaultMember = new AttributeNode();
          defaultMember.Constructor = new Literal(TypeExpressionFor("System", "Reflection", "DefaultMemberAttribute"));
          defaultMember.Expressions = new ExpressionList(1);
          defaultMember.Expressions.Add(new Literal("Item"));
          if (parentType.Attributes == null) parentType.Attributes = new AttributeList(1);
          parentType.Attributes.Add(defaultMember);
        }
        paramList = this.ParseParameters(Token.RightBracket, followers|Token.LeftBrace);
        this.Skip(Token.LeftBrace);
      }
      Property p = new Property(parentType, attributes, PropertyFlags.None, name, null, null);
      parentType.Members.Add(p);
      p.SourceContext = ctx;
      p.Documentation = this.LastDocComment;
      p.Type = p.TypeExpression = type;
      MethodFlags mflags;
      if (interfaceType != null){
        p.ImplementedTypeExpressions = p.ImplementedTypes = new TypeNodeList(interfaceType);
        mflags = MethodFlags.Private|MethodFlags.HideBySig|MethodFlags.NewSlot|MethodFlags.Final|MethodFlags.Virtual|MethodFlags.SpecialName;
        for (int i = 0, n = modifierContexts == null ? 0 : modifierContexts.Length; i < n; i++) {
          if (modifierTokens[i] == Token.Extern)
            mflags |= MethodFlags.PInvokeImpl;
          else if (modifierTokens[i] == Token.Unsafe) {
            if (!this.allowUnsafeCode) {
              this.allowUnsafeCode = true;
              this.HandleError(modifierContexts[i], Error.IllegalUnsafe);
            }
            this.inUnsafeCode = true;
          } else
            this.HandleError(modifierContexts[i], Error.InvalidModifier, modifierContexts[i].SourceText);
        }
      }else
        mflags = this.GetMethodFlags(modifierTokens, modifierContexts, parentType, p)|MethodFlags.SpecialName;
      if ((mflags & MethodFlags.Static) != 0 && parentType is Interface){
        this.HandleError(name.SourceContext, Error.InvalidModifier, "static");
        mflags &= ~MethodFlags.Static;
        mflags |= MethodFlags.Abstract;
      }
      TokenSet followersOrRightBrace = followers|Token.RightBrace;
      bool accessorModifiersAlreadySpecified = false;
      MethodFlags accessorFlags = mflags;
      while (Parser.GetOrLeftBracketOrSetOrModifier[this.currentToken]){
        SourceContext sc = this.scanner.CurrentSourceContext;
        AttributeList accessorAttrs = this.ParseAttributes(null, followers|Token.Get|Token.Set|Token.LeftBrace);
        switch (this.currentToken){
          case Token.Get:{
            if (p.Getter != null)
              this.HandleError(Error.DuplicateAccessor);
            SourceContext scntx = this.scanner.CurrentSourceContext;
            this.GetNextToken();
            Method m = new Method(parentType, accessorAttrs, new Identifier("get_"+name.ToString()), paramList, type, null);
            m.SourceContext = sc;
            m.ReturnTypeExpression = type;
            m.Name.SourceContext = scntx;
            if ((accessorFlags & MethodFlags.Static) == 0) 
              m.CallingConvention = CallingConventionFlags.HasThis;
            parentType.Members.Add(m);
            m.Flags = accessorFlags|MethodFlags.HideBySig;
            if (interfaceType != null)
              m.ImplementedTypeExpressions = m.ImplementedTypes = new TypeNodeList(interfaceType);
            bool swallowedSemicolonAlready = false;
            bool bodyAllowed = true;
            if (this.currentToken == Token.Semicolon){
              m.SourceContext.EndPos = this.scanner.endPos;
              this.GetNextToken();
              bodyAllowed = false;
              swallowedSemicolonAlready = true;
            }
            this.ParseMethodContract(m, followers|Token.LeftBrace|Token.Semicolon, ref swallowedSemicolonAlready);
            if (bodyAllowed)
              m.Body = this.ParseBody(m, sc, followersOrRightBrace|Token.Set, swallowedSemicolonAlready);
            else if (!swallowedSemicolonAlready)
              this.SkipSemiColon(followersOrRightBrace|Token.Set);
            p.Getter = m;
            m.DeclaringMember = p;
            accessorFlags = mflags;
            break;}
          case Token.Set:{
            if (p.Setter != null)
              this.HandleError(Error.DuplicateAccessor);
            SourceContext scntx = this.scanner.CurrentSourceContext;
            this.GetNextToken();
            ParameterList parList = new ParameterList();
            if (paramList != null)
              for (int i = 0, n = paramList.Count; i < n; i++) parList.Add((Parameter)paramList[i].Clone());
            parList.Add(new Parameter(null, ParameterFlags.None, Identifier.For("value"), type, null, null));
            Method m = new Method(parentType, accessorAttrs, new Identifier("set_"+name.ToString()), parList, this.TypeExpressionFor(Token.Void), null);
            m.SourceContext = sc;
            m.Name.SourceContext = scntx;
            if ((accessorFlags & MethodFlags.Static) == 0) 
              m.CallingConvention = CallingConventionFlags.HasThis;
            parentType.Members.Add(m);
            m.Flags = accessorFlags|MethodFlags.HideBySig;
            if (interfaceType != null)
              m.ImplementedTypeExpressions = m.ImplementedTypes = new TypeNodeList(interfaceType);
            bool swallowedSemicolonAlready = false;
            bool bodyAllowed = true;
            if (this.currentToken == Token.Semicolon) {
              m.SourceContext.EndPos = this.scanner.endPos;
              this.GetNextToken();
              bodyAllowed = false;
              swallowedSemicolonAlready = true;
            }
            this.ParseMethodContract(m, followers|Token.LeftBrace|Token.Semicolon, ref swallowedSemicolonAlready);
            if (bodyAllowed)
              m.Body = this.ParseBody(m, sc, followersOrRightBrace|Token.Get, swallowedSemicolonAlready);
            else if (!swallowedSemicolonAlready)
              this.SkipSemiColon(followersOrRightBrace|Token.Get);
            p.Setter = m;
            m.DeclaringMember = p;
            accessorFlags = mflags;
            break;}
          case Token.Protected:
          case Token.Internal: 
          case Token.Private: 
            if (parentType is Interface || interfaceType != null || accessorModifiersAlreadySpecified)
              goto case Token.New;
            accessorFlags = this.ParseAccessorModifiers(mflags);
            accessorModifiersAlreadySpecified = true;
            break;
          case Token.Public:
          case Token.New:
          case Token.Abstract: 
          case Token.Sealed: 
          case Token.Static:
          case Token.Readonly:
          case Token.Volatile:
          case Token.Virtual:
          case Token.Override:
          case Token.Extern:
          case Token.Unsafe:
            this.HandleError(Error.NoModifiersOnAccessor);
            this.GetNextToken();
            break;
          default:
            this.SkipTo(followersOrRightBrace, Error.GetOrSetExpected);
            break;
        }
      }
      p.SourceContext.EndPos = this.scanner.endPos;
      Error e = Error.GetOrSetExpected;
      if (p.Getter == null && p.Setter == null) p.Parameters = paramList;
      if (p.Getter != null && p.Setter != null) e = Error.ExpectedRightBrace;
      if ((p.Getter == null || p.Setter == null) && this.sink != null && this.currentToken == Token.Identifier){
        p.SourceContext.EndPos = this.scanner.startPos-1;
        KeywordCompletionList keywords;
        if (p.Getter != null)
          keywords = new KeywordCompletionList(this.scanner.GetIdentifier(), new KeywordCompletion("set"));
        else if (p.Setter != null)
          keywords = new KeywordCompletionList(this.scanner.GetIdentifier(), new KeywordCompletion("get"));
        else
          keywords = new KeywordCompletionList(this.scanner.GetIdentifier(), new KeywordCompletion("get"), new KeywordCompletion("set"));
        parentType.Members.Add(keywords);
        this.GetNextToken();
        this.SkipTo(followers);
        return;
      }
      this.ParseBracket(ctx, Token.RightBrace, followers, e);
    }
    private MethodFlags ParseAccessorModifiers(MethodFlags commonFlags){
      MethodFlags result = (MethodFlags)0;
      SourceContext sctx = this.scanner.CurrentSourceContext;
      switch(this.currentToken){
        case Token.Internal: 
          result = MethodFlags.Assembly;
          this.GetNextToken();
          if (this.currentToken == Token.Protected){
            result = MethodFlags.FamORAssem;
            this.GetNextToken();
            sctx.EndPos = this.scanner.endPos;
          }
          break;
        case Token.Protected:
          result = MethodFlags.Family;
          this.GetNextToken();
          if (this.currentToken == Token.Internal){
            result = MethodFlags.FamORAssem;
            this.GetNextToken();
            sctx.EndPos = this.scanner.endPos;
          }
          break;
        case Token.Private:
          result = MethodFlags.Private;
          this.GetNextToken();
          break;
        default:
          Debug.Assert(false);
          return commonFlags;
      }
      //TODO: check that result is compatible with commonFlags
      return result | (commonFlags & ~MethodFlags.MethodAccessMask);
    }
    private Block ParseBody(Method m, object sctx, TokenSet followers){
      return ParseBody(m, sctx, followers, false);
    }
    private Block ParseBody(Method m, object sctx, TokenSet followers, bool swallowedSemicolonAlready){
      m.SourceContext = (SourceContext)sctx;
      m.SourceContext.EndPos = this.scanner.endPos;
      Block b = null;
      if (this.currentToken == Token.LeftBrace){
        int startPos = this.scanner.CurrentSourceContext.StartPos;
        b = this.ParseBlock(sctx, followers);
        if (b != null){
          m.SourceContext.EndPos = b.SourceContext.EndPos;
          if (this.omitBodies) b.Statements = null;
        }
        this.SkipTo(followers);
      }else if (this.currentToken == Token.Semicolon || swallowedSemicolonAlready){
        if (this.currentToken == Token.Semicolon)
          this.GetNextToken();
        this.SkipTo(followers);
      }else{
        this.SkipTo(followers, Error.ExpectedLeftBrace);
      }
      if (this.sink != null && b != null)
        this.sink.AddCollapsibleRegion(b.SourceContext, true);
      return b;
    }
    private Block ParseBlock(TokenSet followers){
      Block b = null;
      if (this.currentToken == Token.LeftBrace)
        b = this.ParseBlock(this.scanner.CurrentSourceContext, followers);
      else if (Parser.StatementStart[this.currentToken]){
        this.HandleError(Error.ExpectedLeftBrace);
        Statement s = this.ParseStatement(followers);
        b = new Block(new StatementList(s));
        if (s != null) b.SourceContext = s.SourceContext;
        b.IsUnsafe = this.inUnsafeCode;
      }else
        this.SkipTo(followers, Error.ExpectedLeftBrace);
      return b;
    }
    private Block ParseBlock(object sctx, TokenSet followers){
      bool savedInsideBlock = this.insideBlock;
      this.insideBlock = true;
      Block block = new Block();
      block.Checked = this.insideCheckedBlock;
      block.IsUnsafe = this.inUnsafeCode;
      block.SuppressCheck = this.insideUncheckedBlock;
      SourceContext ctx = (SourceContext)sctx;
      ctx.EndPos = this.scanner.CurrentSourceContext.EndPos;
      Debug.Assert(this.currentToken == Token.LeftBrace);
      block.SourceContext = this.scanner.CurrentSourceContext;
      this.GetNextToken(); 
      block.Statements = this.ParseStatements(followers|Token.RightBrace);
      block.SourceContext.EndPos = this.scanner.CurrentSourceContext.EndPos;
      this.insideBlock = savedInsideBlock;
      this.ParseBracket(ctx, Token.RightBrace, followers, Error.ExpectedRightBrace);
      return block;
    }
    //TODO: get rid of stateAfterBrace
    private Expression ParseBlockExpression(ScannerState stateAfterBrace, object sctx, TokenSet followers){
      bool savedSawReturnOrYield = this.sawReturnOrYield;
      this.sawReturnOrYield = false;
      Block block = new Block();
      block.Checked = this.insideCheckedBlock;
      block.IsUnsafe = this.inUnsafeCode;
      block.SuppressCheck = this.insideUncheckedBlock;
      SourceContext ctx = (SourceContext)sctx;
      ctx.EndPos = this.scanner.CurrentSourceContext.EndPos;
      Debug.Assert(this.currentToken == Token.LeftBrace);
//      this.GetNextToken();
      block.SourceContext = ctx;
      block.Statements = new StatementList(new ExpressionStatement(ParseComprehension(followers)));
//      block.Statements = this.ParseStatementsWithOptionalExpression(followers|Token.RightBrace);
      if (this.arrayInitializerOpeningContext != null && block.Statements == null && this.currentToken == Token.RightBrace){
        this.GetNextToken();
        this.sawReturnOrYield = savedSawReturnOrYield;
        return null;
      }
      block.SourceContext.EndPos = this.scanner.CurrentSourceContext.EndPos;
      this.scanner.state = stateAfterBrace;
//      this.ParseBracket(ctx, Token.RightBrace, followers, Error.ExpectedRightBrace);
      Expression result = null;
      if (sawReturnOrYield)
        result = new AnonymousNestedFunction(new ParameterList(0), block);
      else
        result = new BlockExpression(block);
      result.SourceContext = ctx;
      this.sawReturnOrYield = savedSawReturnOrYield;
      return result;
    }
    private Block ParseStatementAsBlock(TokenSet followers){
      Statement s = this.ParseStatement(followers);
      Block b = s as Block;
      if (s is LabeledStatement) b = null;
      if (b == null && s != null){
        if (s is LocalDeclarationsStatement || s is LabeledStatement)
          this.HandleError(s.SourceContext, Error.BadEmbeddedStmt);
        b = new Block(new StatementList(1), this.insideCheckedBlock, this.insideUncheckedBlock, this.inUnsafeCode);
        b.Statements.Add(s);
        b.SourceContext = s.SourceContext;
      }
      return b;
    }
    private StatementList ParseStatements(TokenSet followers){
      StatementList statements = new StatementList();
      this.ParseStatements(statements, followers);
      return statements;
    }
    private void ParseStatements(StatementList statements, TokenSet followers){
      TokenSet statementFollowers = followers|Parser.StatementStart;
      if (!statementFollowers[this.currentToken])
        this.SkipTo(statementFollowers, Error.InvalidExprTerm, this.scanner.GetTokenSource());
      while (Parser.StatementStart[this.currentToken]){
        Statement s = this.ParseStatement(statementFollowers);
        if (s != null)
          statements.Add(s);
        else
          break;
      }
      //if (this.currentToken == Token.EndOfFile && this.sink != null)
      //  statements.Add(new ExpressionStatement(new Identifier(" ", this.scanner.CurrentSourceContext), this.scanner.CurrentSourceContext));
      this.SkipTo(followers);
    }
    private StatementList ParseStatementsWithOptionalExpression(TokenSet followers){
      StatementList statements = new StatementList();
      TokenSet statementFollowers = followers|Parser.StatementStart;
      while (Parser.StatementStart[this.currentToken]){
        int col = this.scanner.endPos;
        Statement s = this.ParseStatement(statementFollowers, true);
        if (s == null && (this.currentToken == Token.RightBrace && this.arrayInitializerOpeningContext != null || col == this.scanner.endPos))
          return null;
        statements.Add(s);
        this.arrayInitializerOpeningContext = null;
      }
      this.SkipTo(followers);
      return statements;
    }
    private Statement ParseStatement(TokenSet followers){
      if (this.currentToken == Token.EndOfFile) return null;
      bool savedParsingStatement = this.parsingStatement;
      this.parsingStatement = true;
      Statement result = this.ParseStatement(followers, false);
      if (this.currentToken != Token.EndOfFile) this.parsingStatement = savedParsingStatement;
      return result;
    }
    private Statement ParseStatement(TokenSet followers, bool preferExpressionToDeclaration){
      switch(this.currentToken){
        case Token.LeftBrace: return this.ParseBlock(followers);
        case Token.Semicolon: Block b = new Block(null, this.scanner.CurrentSourceContext); this.GetNextToken(); return b;
        case Token.Acquire: return this.ParseAcquire(followers);
        case Token.Assert: return this.ParseAssertion(followers);
        case Token.Assume: return this.ParseAssumption(followers);
        case Token.If: return this.ParseIf(followers);
        case Token.Switch: return this.ParseSwitch(followers);
        case Token.While: return this.ParseWhile(followers);
        case Token.Do: return this.ParseDoWhile(followers);
        case Token.For: return this.ParseFor(followers);
        case Token.Foreach: return this.ParseForEach(followers);
        case Token.Break: return this.ParseBreak(followers);
        case Token.Continue: return this.ParseContinue(followers);
        case Token.Goto: return this.ParseGoto(followers);
        case Token.Return: return this.ParseReturn(followers);
        case Token.Throw: return this.ParseThrow(followers);
        case Token.Yield: return this.ParseYield(followers, preferExpressionToDeclaration);
        case Token.Try: 
        case Token.Catch:
        case Token.Finally:
          return this.ParseTry(followers);
        case Token.Checked: return this.ParseChecked(followers);
        case Token.Unchecked: return this.ParseUnchecked(followers);
        case Token.Read: return this.ParseExpose(followers, NodeType.Read, preferExpressionToDeclaration);
        case Token.Write: return this.ParseExpose(followers, NodeType.Write, preferExpressionToDeclaration);
        case Token.Expose: return this.ParseExpose(followers, NodeType.Write, preferExpressionToDeclaration);
        case Token.Additive: return this.ParseExpose(followers, NodeType.Write, preferExpressionToDeclaration);
        case Token.Fixed: return this.ParseFixed(followers);
        case Token.Lock: return this.ParseLock(followers);
        case Token.Using: return this.ParseUsing(followers);
        case Token.Unsafe: return this.ParseUnsafe(followers);
        case Token.Const: return this.ParseLocalConst(followers);
        case Token.New: return this.ParseNewStatement(followers, preferExpressionToDeclaration);
        case Token.Get:
        case Token.Set:
          if (followers[this.currentToken]){
            //Inside getter or setter. Do not allow get or set to be followed by a {.
            Token nextToken = this.PeekToken();
            if (nextToken == Token.LeftBrace){
              return null;
            }
          }
          goto default;
	//HS D: a block hole...
        case Token.Hole:
	    return this.ParseBlockHole(followers);
        default:
          return this.ParseExpressionStatementOrDeclaration(false, followers, preferExpressionToDeclaration, true);
      }
    }       
    private Assertion ParseAssertion(TokenSet followers){
      Debug.Assert(this.currentToken == Token.Assert);
      Assertion result = new Assertion();
      result.SourceContext = this.scanner.CurrentSourceContext;
      this.GetNextToken();
      result.Condition = this.ParseExpression(followers|Token.Semicolon);
      this.SkipSemiColon(followers);
      return result;
    }

    private Assumption ParseAssumption(TokenSet followers){
      Debug.Assert(this.currentToken == Token.Assume);
      Assumption result = new Assumption();
      result.SourceContext = this.scanner.CurrentSourceContext;
      this.GetNextToken();
      result.Condition = this.ParseExpression(followers|Token.Semicolon);
      this.SkipSemiColon(followers);
      return result;
    }
    
    //HS D
    private BlockHole ParseBlockHole(TokenSet followers){	
	Debug.Assert(this.currentToken == Token.Hole);
	Hashtable templateParams = new Hashtable();
	this.GetNextToken();
	this.Skip(Token.LeftBrace);
	if (this.currentToken != Token.RightBrace){
	    string templateParam = this.scanner.GetTokenSource();
	    this.GetNextToken();
	    this.Skip(Token.Colon);
	    Expression templateParamVal = 
		this.currentToken == Token.IntegerLiteral ? this.ParseIntegerLiteral() : 
		(this.currentToken == Token.RealLiteral ? 
		 this.ParseRealLiteral() : this.ParseCommaSeparetedIdentifierSet());
	    templateParams[templateParam] = templateParamVal;
	    while (this.currentToken == Token.Comma){
		this.GetNextToken();
		templateParam = this.scanner.GetTokenSource();
		this.GetNextToken();
		this.Skip(Token.Colon);
		templateParamVal = 
		    this.currentToken == Token.IntegerLiteral ? this.ParseIntegerLiteral() : 
		    (this.currentToken == Token.RealLiteral ? 
		     this.ParseRealLiteral() : this.ParseCommaSeparetedIdentifierSet());
		templateParams[templateParam] = templateParamVal;
	    }
	}
	this.Skip(Token.RightBrace);
	Literal one = new Literal(1);
	ConstructArray empty = new ConstructArray();
	Literal repeat = templateParams.ContainsKey("repeat") ? (Literal) templateParams["repeat"] : one;
	Literal ifbranches = templateParams.ContainsKey("ifbranches") ? (Literal) templateParams["ifbranches"] : one;
	Literal branchops = templateParams.ContainsKey("branchops") ? (Literal) templateParams["branchops"] : one;
	Literal conjunctions = templateParams.ContainsKey("conjunctions") ? (Literal) templateParams["conjunctions"] : one;
	ConstructArray ops = templateParams.ContainsKey("ops") ? (ConstructArray) templateParams["ops"] : empty;
	ConstructArray condvars = templateParams.ContainsKey("condvars") ? (ConstructArray) templateParams["condvars"] : empty;
	ConstructArray argvars = templateParams.ContainsKey("argvars") ? (ConstructArray) templateParams["argvars"] : empty;
	SourceContext sctx = this.scanner.CurrentSourceContext;
	BlockHole result = new BlockHole(sctx, repeat, ifbranches, branchops, conjunctions, ops, condvars, argvars); //, this.currMethod, opMethods); 
	result.SourceContext = sctx;
	//HS D: HACK FIXME	
	DefineBlockHoleMethod(this.currentTypeNode);

	return result;
    }
    
    //HS D
    //HACK FIXME: define a BlockHole method so can represent block holes by calls to it
    private void DefineBlockHoleMethod(TypeNode t) {
	if (definedTypeNodes.ContainsKey(t))
	    return;
	definedTypeNodes[t] = true;
	ParameterList parList = new ParameterList();
	TypeNode intTp = CoreSystemTypes.Int32;
	TypeNode strTp = CoreSystemTypes.Object.GetArrayType(1);
	parList.Add(new Parameter(null, ParameterFlags.None, new Identifier("repeat"), intTp, null, null));
	parList.Add(new Parameter(null, ParameterFlags.None, new Identifier("ifbranches"), intTp, null, null));
	parList.Add(new Parameter(null, ParameterFlags.None, new Identifier("branchops"), intTp, null, null));
	parList.Add(new Parameter(null, ParameterFlags.None, new Identifier("conjunctions"), intTp, null, null));
	parList.Add(new Parameter(null, ParameterFlags.None, new Identifier("ops"), strTp, null, null));
	parList.Add(new Parameter(null, ParameterFlags.None, new Identifier("condvars"), strTp, null, null));
	parList.Add(new Parameter(null, ParameterFlags.None, new Identifier("argvars"), strTp, null, null));
	Method m = new Method(t, null, new Identifier("BlockHole"), parList, this.TypeExpressionFor(Token.Void), new Block());
	m.Flags |= MethodFlags.Static;
	t.Members.Add(m);	
    }

    //HS D
    private Expression ParseCommaSeparetedIdentifierSet() {
	this.Skip(Token.LeftBrace);
	ExpressionList identifiers = new ExpressionList();
	if (this.currentToken != Token.RightBrace){
	    identifiers.Add(new Literal(this.ParsePrefixedIdentifier().ToString())); //FIXME
	    while (this.currentToken == Token.Comma){	    
		this.GetNextToken();
		identifiers.Add(new Literal(this.ParsePrefixedIdentifier().ToString())); //FIXME
	    }
	}
	this.Skip(Token.RightBrace);
        ConstructArray consArr = new ConstructArray();
        consArr.SourceContext = this.scanner.CurrentSourceContext;
        consArr.Initializers = identifiers;
	//consArr.ElementType = TypeNode.GetTypeNode(??); //FIXME
	return consArr;	
    }

    static int InvariantCt;
    private void ParseInvariant(TypeNode parentType, AttributeList attributes, TokenList modifierTokens, SourceContextList modifierContexts, SourceContext sctx, TokenSet followers){
      Debug.Assert(this.currentToken == Token.Invariant);
      bool savedParsingStatement = this.parsingStatement;
      if (this.currentToken != Token.EndOfFile) this.parsingStatement = true;

      for(int i = 0, n = modifierTokens.Length; i < n; i++){
        switch(modifierTokens[i]){
          case Token.Static: break; 
          default:
           // Token.New, Token.Public, Token.Protected, Token.Internal, Token.Private, Token.Abstract, 
           // Token.Sealed, Token.Readonly, Token.Volatile, Token.Virtual, Token.Override, Token.Extern,
           //Token.Unsafe
            this.HandleError(modifierContexts[i], Error.InvalidModifier, modifierContexts[i].SourceText);
            break;
        }
      }

      SourceContext sctxt = this.scanner.CurrentSourceContext;
      this.GetNextToken();
      Expression condition = this.ParseExpression(followers|Token.Semicolon);
      int endPos = this.scanner.endPos;
      this.SkipSemiColon(followers);

      if (!(parentType is Class || parentType is Struct)){
        this.HandleError(sctxt, Error.OnlyStructsAndClassesCanHaveInvariants);
        if (this.currentToken != Token.EndOfFile) this.parsingStatement = savedParsingStatement;
        return;
      }

      EnsureHasInvariantMethod(parentType);
      TypeContract contract = parentType.Contract;
      Identifier name = Identifier.For("invariant" + (InvariantCt++));
      Invariant inv = new Invariant(parentType, attributes, name);
      inv.SourceContext = sctxt;
      inv.Flags = this.GetMethodFlags(modifierTokens, modifierContexts, parentType, inv)|MethodFlags.HideBySig;

      if ((inv.Flags & MethodFlags.Static) != 0 && parentType is Interface){
        this.HandleError(sctx, Error.InvalidModifier, "static");
        inv.Flags &= ~MethodFlags.Static;
      }
      if ((inv.Flags & MethodFlags.Static) == 0)
        inv.CallingConvention = CallingConventionFlags.HasThis;
      inv.Documentation = this.LastDocComment;
      inv.SourceContext.EndPos = endPos;
      inv.Condition = condition;
      contract.Invariants.Add(inv);
      if (this.currentToken != Token.EndOfFile) this.parsingStatement = savedParsingStatement;
    }
    private void EnsureHasInvariantMethod(TypeNode typeNode) {
      TypeContract contract = typeNode.Contract;
      if (contract.Invariants == null)
        contract.Invariants = new InvariantList();
      if (contract.InvariantMethod == null) {
        // MB 10/21/04: BUGBUG??
        // If DEBUG isn't defined, should this method even be created?
        // Give the method a dummy body that will get overwritten if DEBUG is defined.
        // Otherwise, method will exist, but will never be called.
        Method invariantMethod = new Method(
          typeNode,
          new AttributeList(),
          Identifier.For("SpecSharp::CheckInvariant"),
          new ParameterList(new Parameter(Identifier.For("throwException"), TypeExpressionFor(Token.Bool))),
          TypeExpressionFor(Token.Bool),
          new Block(new StatementList(new Return(new Literal(true))), false, false, false));
        invariantMethod.CallingConvention = CallingConventionFlags.HasThis;
        invariantMethod.Flags = MethodFlags.Public;
        contract.InvariantMethod = invariantMethod;
        typeNode.Members.Add(invariantMethod);
      }
    }
    private If ParseIf(TokenSet followers){
      If If = new If();
      If.SourceContext = this.scanner.CurrentSourceContext;
      Debug.Assert(this.currentToken == Token.If);
      this.GetNextToken();
      If.Condition = this.ParseParenthesizedExpression(followers|Parser.StatementStart);
      if (If.Condition != null) If.ConditionContext = If.Condition.SourceContext;
      Block b = this.ParseStatementAsBlock(followers|Token.Else);
      If.TrueBlock = b;
      if (b != null){
        if (b.SourceContext.EndPos > If.SourceContext.EndPos)
          If.SourceContext.EndPos = b.SourceContext.EndPos;
        if (b.Statements == null && !(b is LabeledStatement))
          this.HandleError(b.SourceContext, Error.PossibleMistakenNullStatement);
      }
      if (this.currentToken == Token.Else){
        If.ElseContext = this.scanner.CurrentSourceContext;
        this.GetNextToken();
        b = this.ParseStatementAsBlock(followers);
        If.FalseBlock = b;
        if (b != null){
          if (b.SourceContext.EndPos > If.SourceContext.EndPos)
            If.SourceContext.EndPos = b.SourceContext.EndPos;
          if (b.Statements == null)
            this.HandleError(b.SourceContext, Error.PossibleMistakenNullStatement);
        }
      }
      If.EndIfContext = If.SourceContext;
      If.EndIfContext.StartPos = If.EndIfContext.EndPos-1;
      if (b != null){
        string text = b.SourceContext.SourceText;
        if (text != null && text.EndsWith("}"))
          If.EndIfContext = this.scanner.CurrentSourceContext;
      }
      return If;
    }
    private Cci.Switch ParseSwitch(TokenSet followers){
      Cci.Switch Switch = new Cci.Switch();
      Switch.SourceContext = this.scanner.CurrentSourceContext;
      Debug.Assert(this.currentToken == Token.Switch);
      this.GetNextToken();
      Switch.Expression = this.ParseParenthesizedExpression(followers|Token.LeftBrace);
      Switch.Cases = new SwitchCaseList();
      Switch.SourceContext.EndPos = this.scanner.endPos;
      this.Skip(Token.LeftBrace);
      TokenSet followersOrCaseOrColonOrDefaultOrRightBrace = followers|Parser.CaseOrColonOrDefaultOrRightBrace;
      TokenSet followersOrCaseOrDefaultOrRightBrace = followers|Parser.CaseOrDefaultOrRightBrace;
      SwitchCase scase = new SwitchCase();
      scase.SourceContext = this.scanner.CurrentSourceContext;
      for(;;){
        switch(this.currentToken){
          case Token.Case:
            this.GetNextToken();
            if (this.currentToken == Token.Colon) 
              this.HandleError(Error.ConstantExpected);
            else
              scase.Label = this.ParseExpression(followersOrCaseOrColonOrDefaultOrRightBrace);
            break;
          case Token.Default: //Parse these as many times as they occur. Checker will report the error.
            this.GetNextToken();
            scase.Label = null;
            break;
          default: 
            if (Parser.StatementStart[this.currentToken]){
              this.HandleError(Error.StmtNotInCase);
              this.ParseStatement(followersOrCaseOrColonOrDefaultOrRightBrace);
              continue;
            }
            goto done;
        }
        this.Skip(Token.Colon);
        scase.Body = this.ParseSwitchCaseStatementBlock(followersOrCaseOrDefaultOrRightBrace);
        if (scase.Body != null && scase.Body.Statements != null && scase.Body.Statements.Count > 0) {
          Statement swbottom = new Statement(NodeType.SwitchCaseBottom);
          swbottom.SourceContext = scase.SourceContext;
          scase.Body.Statements.Add(swbottom);
        }
        Switch.Cases.Add(scase);
        scase = new SwitchCase();
        scase.SourceContext = this.scanner.CurrentSourceContext;
      }
    done:
      if (Switch.Cases.Count == 0) {
        this.HandleError(Error.EmptySwitch);
      }
      else {
        // add SwitchCaseBottom to last case if it happened to have no statements.
        SwitchCase lastCase = Switch.Cases[Switch.Cases.Count-1];
        if (lastCase != null && lastCase.Body.Statements.Count == 0) {
          Statement swbottom = new Statement(NodeType.SwitchCaseBottom);
          swbottom.SourceContext = lastCase.SourceContext;
          lastCase.Body.Statements.Add(swbottom);
        }
      }
      SourceContext sctx = Switch.SourceContext;
      Switch.SourceContext.EndPos = this.scanner.CurrentSourceContext.EndPos;
      this.ParseBracket(Switch.SourceContext, Token.RightBrace, followers, Error.ExpectedRightBrace);
      return Switch;
    }
    private Block ParseSwitchCaseStatementBlock(TokenSet followers){
      StatementList statements = new StatementList();
      Block b = new Block(statements, this.insideCheckedBlock, this.insideUncheckedBlock, this.inUnsafeCode);
      b.SourceContext = this.scanner.CurrentSourceContext;
      SourceContext savedCompoundStatementOpeningContext = this.compoundStatementOpeningContext;
      this.compoundStatementOpeningContext = b.SourceContext;
      while (Parser.StatementStart[this.currentToken]){
        if (this.currentToken == Token.Default){
          bool carryOn = this.PeekToken() == Token.LeftParenthesis;
          if (this.PeekToken() != Token.LeftParenthesis) break;
        }
        statements.Add(this.ParseStatement(followers));
      }
      this.compoundStatementOpeningContext = savedCompoundStatementOpeningContext;
      return b;
    }
    private While ParseWhile(TokenSet followers){
      While While = new While();
      While.SourceContext = this.scanner.CurrentSourceContext;
      Debug.Assert(this.currentToken == Token.While);
      this.GetNextToken();
      While.Condition = this.ParseParenthesizedExpression(followers);
      While.Invariants = ParseLoopInvariants(followers);
      SourceContext savedCompoundStatementOpeningContext = this.compoundStatementOpeningContext;
      While.SourceContext.EndPos = this.scanner.endPos;
      this.compoundStatementOpeningContext = While.SourceContext;
      Block b = this.ParseStatementAsBlock(followers);
      While.Body = b;
      if (b != null)
        While.SourceContext.EndPos = b.SourceContext.EndPos;
      this.SkipTo(followers);
      this.compoundStatementOpeningContext = savedCompoundStatementOpeningContext;
      return While;
    }
    private DoWhile ParseDoWhile(TokenSet followers){
      DoWhile doWhile = new DoWhile();
      doWhile.SourceContext = this.scanner.CurrentSourceContext;
      Debug.Assert(this.currentToken == Token.Do);
      this.GetNextToken();
      doWhile.Invariants = ParseLoopInvariants(followers);
      SourceContext savedCompoundStatementOpeningContext = this.compoundStatementOpeningContext;
      doWhile.SourceContext.EndPos = this.scanner.endPos;
      this.compoundStatementOpeningContext = doWhile.SourceContext;
      Block b = doWhile.Body = this.ParseStatementAsBlock(followers|Token.While);
      if (b != null && b.Statements == null)
        this.HandleError(b.SourceContext, Error.PossibleMistakenNullStatement);
      this.Skip(Token.While);
      doWhile.Condition = this.ParseParenthesizedExpression(followers|Token.Semicolon);
      if (doWhile.Condition != null)
        doWhile.SourceContext.EndPos = doWhile.Condition.SourceContext.EndPos;
      this.SkipSemiColon(followers);
      this.compoundStatementOpeningContext = savedCompoundStatementOpeningContext;
      return doWhile;
    }
    private ExpressionList ParseLoopInvariants(TokenSet followers){
      if (this.currentToken == Token.Invariant){
        ExpressionList invariants = new ExpressionList();
        while (this.currentToken == Token.Invariant){
          this.GetNextToken();
          invariants.Add(this.ParseExpression(followers));
          this.SkipSemiColon(followers);
        }
        return invariants;
      }
      return null;
    }
    private Block ParseFor(TokenSet followers){
      For For = new For();
      For.SourceContext = this.scanner.CurrentSourceContext;
      Debug.Assert(this.currentToken == Token.For);
      this.GetNextToken();
      SourceContext sctx = this.scanner.CurrentSourceContext;
      this.Skip(Token.LeftParenthesis);
      TokenSet followersOrRightParenthesisOrSemicolon = followers|Parser.RightParenthesisOrSemicolon;
      For.Initializer = this.ParseForInitializer(followersOrRightParenthesisOrSemicolon);
      For.Condition = this.currentToken == Token.Semicolon ? null : this.ParseExpression(followersOrRightParenthesisOrSemicolon);
      this.Skip(Token.Semicolon);
      For.Incrementer = this.ParseForIncrementer(followers|Token.RightParenthesis);
      this.ParseBracket(sctx, Token.RightParenthesis, followers|Parser.StatementStart, Error.ExpectedRightParenthesis);
      For.SourceContext.EndPos = this.scanner.endPos;
      For.Invariants = ParseLoopInvariants(followers);
      SourceContext savedCompoundStatementOpeningContext = this.compoundStatementOpeningContext;
      For.SourceContext.EndPos = this.scanner.endPos;
      this.compoundStatementOpeningContext = For.SourceContext;
      Block b = this.ParseStatementAsBlock(followers);
      For.Body = b;
      if (b != null)
        For.SourceContext.EndPos = b.SourceContext.EndPos;
      this.compoundStatementOpeningContext = savedCompoundStatementOpeningContext;
      StatementList statements = new StatementList(1);
      statements.Add(For);
      return new Block(statements, For.SourceContext, this.insideCheckedBlock, this.insideUncheckedBlock, this.inUnsafeCode);
    }
    private StatementList ParseForInitializer(TokenSet followers){
      StatementList statements = new StatementList();
      if (this.currentToken == Token.Semicolon){
        this.GetNextToken();
        return statements;
      }
      if (this.currentToken == Token.RightParenthesis){
        this.Skip(Token.Semicolon);
        return statements;
      }
      TokenSet followerOrComma = followers|Token.Comma;
      for(;;){
        statements.Add(this.ParseExpressionStatementOrDeclaration(true, followerOrComma, false, true));
        if (this.currentToken != Token.Comma) break;
        this.GetNextToken();
      }
      return statements;
    }
    private StatementList ParseForIncrementer(TokenSet followers){
      StatementList statements = new StatementList();
      if (this.currentToken == Token.RightParenthesis)
        return statements;
      TokenSet followerOrComma = followers|Token.Comma;
      for(;;){
        Expression e = this.ParseExpression(followerOrComma);
        if (e == null) return statements;
        if (!(e is AssignmentExpression || e is MethodCall || e is PostfixExpression || e is PrefixExpression || e is Construct))
          this.HandleError(e.SourceContext, Error.IllegalStatement);
        Statement s = new ExpressionStatement(e);
        s.SourceContext = e.SourceContext;
        statements.Add(s);
        if (this.currentToken != Token.Comma) break;
        this.GetNextToken();
      }
      this.SkipTo(followers);
      return statements;
    }
    private ForEach ParseForEach(TokenSet followers){
      ForEach forEach = new ForEach();
      forEach.StatementTerminatesNormallyIfEnumerableIsNull = false;
      forEach.StatementTerminatesNormallyIfEnumeratorIsNull = false;
      forEach.SourceContext = this.scanner.CurrentSourceContext;
      Debug.Assert(this.currentToken == Token.Foreach);
      this.GetNextToken();
      SourceContext sctx = this.scanner.CurrentSourceContext;
      this.Skip(Token.LeftParenthesis);
      forEach.TargetVariableType = forEach.TargetVariableTypeExpression = 
        this.ParseTypeExpression(null, followers|Parser.IdentifierOrNonReservedKeyword|Token.In|Token.RightParenthesis);
      forEach.TargetVariable = this.scanner.GetIdentifier();
      if (this.currentToken == Token.In)
        this.HandleError(Error.BadForeachDecl);
      else
        this.SkipIdentifierOrNonReservedKeyword();
      this.Skip(Token.In);
      forEach.SourceEnumerable = this.ParseExpression(followers|Token.RightParenthesis);
      this.ParseBracket(sctx, Token.RightParenthesis, followers|Token.ElementsSeen|Token.Invariant|Parser.StatementStart, Error.ExpectedRightParenthesis);
      if (this.currentToken == Token.ElementsSeen){
        this.GetNextToken();
        forEach.InductionVariable = this.scanner.GetIdentifier();
        this.SkipIdentifierOrNonReservedKeyword();
        this.SkipSemiColon(followers);
      }
      forEach.Invariants = this.ParseLoopInvariants(followers);
      SourceContext savedCompoundStatementOpeningContext = this.compoundStatementOpeningContext;
      forEach.SourceContext.EndPos = this.scanner.endPos;
      this.compoundStatementOpeningContext = forEach.SourceContext;
      Block b = this.ParseStatementAsBlock(followers);
      forEach.Body = b;
      if (b != null)
        forEach.SourceContext.EndPos = b.SourceContext.EndPos;
      this.compoundStatementOpeningContext = savedCompoundStatementOpeningContext;
      return forEach;
    }
    private Exit ParseBreak(TokenSet followers){
      Exit Break = new Exit();
      Break.SourceContext = this.scanner.CurrentSourceContext;
      if (this.sink != null && this.compoundStatementOpeningContext.Document != null)
        this.sink.MatchPair(this.compoundStatementOpeningContext, Break.SourceContext);
      Debug.Assert(this.currentToken == Token.Break);
      this.GetNextToken();
      this.SkipSemiColon(followers);
      return Break;
    }
    private Continue ParseContinue(TokenSet followers){
      Continue Continue = new Continue();
      Continue.SourceContext = this.scanner.CurrentSourceContext;
      if (this.sink != null && this.compoundStatementOpeningContext.Document != null)
        this.sink.MatchPair(this.compoundStatementOpeningContext, Continue.SourceContext);
      Debug.Assert(this.currentToken == Token.Continue);
      this.GetNextToken();
      this.SkipSemiColon(followers);
      return Continue;
    }
    private Statement ParseGoto(TokenSet followers){
      SourceContext sctx = this.scanner.CurrentSourceContext;
      Debug.Assert(this.currentToken == Token.Goto);
      this.GetNextToken();
      Statement result = null;
      switch(this.currentToken){
        case Token.Case:
          this.GetNextToken();          
          result = new GotoCase(this.ParseExpression(followers|Token.Semicolon));
          break;
        case Token.Default:
          result = new GotoCase(null);
          this.GetNextToken();
          break;
        default:
          result = new Goto(this.scanner.GetIdentifier());
          this.SkipIdentifierOrNonReservedKeyword();
          break;
      }
      sctx.EndPos = this.scanner.endPos;
      result.SourceContext = sctx;
      this.SkipSemiColon(followers);
      return result;
    }
    private Return ParseReturn(TokenSet followers){
      this.sawReturnOrYield = true;
      Return Return = new Return();
      Return.SourceContext = this.scanner.CurrentSourceContext;
      Debug.Assert(this.currentToken == Token.Return);
      this.GetNextToken();
      if (this.currentToken != Token.Semicolon){
        Expression expr = Return.Expression = this.ParseExpression(followers|Token.Semicolon);
        if (expr != null)
          Return.SourceContext.EndPos = expr.SourceContext.EndPos;
      }
      this.SkipSemiColon(followers);
      return Return;
    }
    private Throw ParseThrow(TokenSet followers){
      Throw Throw = new Throw();
      Throw.SourceContext = this.scanner.CurrentSourceContext;
      Debug.Assert(this.currentToken == Token.Throw);
      this.GetNextToken();
      if (this.currentToken != Token.Semicolon){
        Expression expr = Throw.Expression = this.ParseExpression(followers|Token.Semicolon);
        if (expr != null)
          Throw.SourceContext.EndPos = expr.SourceContext.EndPos;
      }
      this.SkipSemiColon(followers);
      return Throw;
    }
    private Statement ParseYield(TokenSet followers, bool preferExpressionToDeclaration){
      this.sawReturnOrYield = true;
      Yield Yield = new Yield();
      Yield.SourceContext = this.scanner.CurrentSourceContext;
      ScannerState ss = this.scanner.state;
      Debug.Assert(this.currentToken == Token.Yield);
      this.GetNextToken();
      bool breakOutOfIterator = false;
      if (this.currentToken == Token.Break){
        this.GetNextToken();
        breakOutOfIterator = true;
      }else if (this.currentToken != Token.Return){
        //Restore prior state and reparse as expression
        this.scanner.endPos = Yield.SourceContext.StartPos;
        this.scanner.state = ss;
        this.currentToken = Token.None;
        this.GetNextToken();
        return this.ParseExpressionStatementOrDeclaration(false, followers, preferExpressionToDeclaration, true);
      }else
        this.GetNextToken();
      Expression expr = Yield.Expression = breakOutOfIterator ? null : this.ParseExpression(followers|Token.Semicolon);
      if (expr != null)
        Yield.SourceContext.EndPos = expr.SourceContext.EndPos;
      this.SkipSemiColon(followers);
      return Yield;
    }
    private Try ParseTry(TokenSet followers){
      object catchContext = null;
      object finallyContext = null;
      bool savedUnmatchedTry = this.unmatchedTry;
      Try Try = new Try();
      Try.SourceContext = this.scanner.CurrentSourceContext;
      Debug.Assert(this.currentToken == Token.Try || this.currentToken == Token.Catch || this.currentToken == Token.Finally);
      TokenSet tryBlockFollowers = followers|Parser.CatchOrFinally;
      if (this.currentToken == Token.Try){
        this.unmatchedTry = true;
        this.GetNextToken();
        if (this.currentToken == Token.LeftBrace)
          Try.TryBlock = this.ParseBlock(tryBlockFollowers);
        else{
          this.HandleError(Error.ExpectedLeftBrace);
          if (Parser.StatementStart[this.currentToken]){
            Block block = new Block();
            block.Checked = this.insideCheckedBlock;
            block.IsUnsafe = this.inUnsafeCode;
            block.SuppressCheck = this.insideUncheckedBlock;
            SourceContext ctx = this.scanner.CurrentSourceContext;
            block.SourceContext = this.scanner.CurrentSourceContext;
            block.Statements = this.ParseStatements(tryBlockFollowers|Token.RightBrace);
            block.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
            Try.TryBlock = block;
            this.Skip(Token.RightBrace);
          }
        }
      }else{
        if (savedUnmatchedTry && ((this.currentToken == Token.Catch && followers[Token.Catch]) || (this.currentToken == Token.Finally && followers[Token.Finally])))
          return null;
        else
          this.HandleError(Error.SyntaxError, "try");
      }
      CatchList catchers = new CatchList();
      bool seenEmptyCatch = false;
      while (this.currentToken == Token.Catch){
        if (seenEmptyCatch) this.HandleError(Error.TooManyCatches);
        catchContext = this.scanner.CurrentSourceContext;
        Catch c = this.ParseCatch(tryBlockFollowers);
        if (c == null) continue;
        if (c.TypeExpression == null) seenEmptyCatch = true;
        catchers.Add(c);
      }
      Try.Catchers = catchers;
      if (this.currentToken == Token.Finally){
        finallyContext = this.scanner.CurrentSourceContext;
        this.GetNextToken();
        Try.Finally = new Finally(this.ParseBlock(followers));
      }else if (catchers.Count == 0)
        this.SkipTo(followers, Error.ExpectedEndTry);
      if (this.sink != null){
        if (finallyContext != null)
          if (catchContext != null)
            this.sink.MatchTriple(Try.SourceContext, (SourceContext)catchContext, (SourceContext)finallyContext);
          else
            this.sink.MatchPair(Try.SourceContext, (SourceContext)finallyContext);
        else if (catchContext != null)
          this.sink.MatchPair(Try.SourceContext, (SourceContext)catchContext);
      }
      this.unmatchedTry = savedUnmatchedTry;
      return Try;
    }
    private Catch ParseCatch(TokenSet followers){
      Catch Catch = new Catch();
      Catch.SourceContext = this.scanner.CurrentSourceContext;
      Debug.Assert(this.currentToken == Token.Catch);
      this.GetNextToken();
      if (this.currentToken == Token.LeftParenthesis){
        this.Skip(Token.LeftParenthesis);
        Catch.Type = Catch.TypeExpression = this.ParseTypeExpression(Identifier.Empty, followers|Token.Identifier|Token.RightParenthesis);
        if (Parser.IdentifierOrNonReservedKeyword[this.currentToken]){
          Catch.Variable = this.scanner.GetIdentifier();
          this.GetNextToken();
        }
        this.Skip(Token.RightParenthesis);
      }
      Catch.Block = this.ParseBlock(followers);
      return Catch;
    }
    private Block ParseChecked(TokenSet followers){
      Debug.Assert(this.currentToken == Token.Checked);
      this.GetNextToken();
      bool savedInsideCheckedBlock = this.insideCheckedBlock;
      bool savedInsideUncheckedBlock = this.insideUncheckedBlock;
      this.insideCheckedBlock = true;
      this.insideUncheckedBlock = false;
      Block b = this.ParseBlock(followers);
      this.insideCheckedBlock = savedInsideCheckedBlock;
      this.insideUncheckedBlock = savedInsideUncheckedBlock;
      return b;
    }
    private Block ParseUnchecked(TokenSet followers){
      Debug.Assert(this.currentToken == Token.Unchecked);
      this.GetNextToken();
      bool savedInsideCheckedBlock = this.insideCheckedBlock;
      bool savedInsideUncheckedBlock = this.insideUncheckedBlock;
      this.insideCheckedBlock = false;
      this.insideUncheckedBlock = true;
      Block b = this.ParseBlock(followers);
      this.insideCheckedBlock = savedInsideCheckedBlock;
      this.insideUncheckedBlock = savedInsideUncheckedBlock;
      return b;
    }
    private Fixed ParseFixed(TokenSet followers){
      Fixed Fixed = new Fixed();
      Fixed.SourceContext = this.scanner.CurrentSourceContext;
      Debug.Assert(this.currentToken == Token.Fixed);
      this.GetNextToken();
      Fixed.Declarators = this.ParseFixedPointerDeclarators(followers|Parser.StatementStart);
      Block b = this.ParseStatementAsBlock(followers);
      Fixed.Body = b;
      if (b != null){
        Fixed.SourceContext.EndPos = b.SourceContext.EndPos;
        if (b.Statements == null)
          this.HandleError(b.SourceContext, Error.PossibleMistakenNullStatement);
      }
      return Fixed;
    }
    private Statement ParseFixedPointerDeclarators(TokenSet followers){
      SourceContext sctx = this.scanner.CurrentSourceContext;
      this.Skip(Token.LeftParenthesis);
      TypeNode bt = this.ParseTypeExpression(null, followers|Token.Identifier|Token.Assign|Token.Comma, false); 
      Statement declarator = this.ParseLocalDeclarations(bt, sctx, false, true, false, false, followers|Token.RightParenthesis);     
      this.ParseBracket(sctx, Token.RightParenthesis, followers, Error.ExpectedRightParenthesis);
      return declarator;
    }
    private Lock ParseLock(TokenSet followers){
      Lock Lock = new Lock();
      Lock.SourceContext = this.scanner.CurrentSourceContext;
      Debug.Assert(this.currentToken == Token.Lock);
      this.GetNextToken();
      Lock.Guard = this.ParseParenthesizedExpression(followers|Parser.StatementStart);
      Block b = this.ParseStatementAsBlock(followers);
      Lock.Body = b;
      if (b != null){
        Lock.SourceContext.EndPos = b.SourceContext.EndPos;
        if (b.Statements == null)
          this.HandleError(b.SourceContext, Error.PossibleMistakenNullStatement);
      }
      return Lock;
    }
    private Block ParseUnsafe(TokenSet followers){
      Debug.Assert(this.currentToken == Token.Unsafe);
      if (!this.allowUnsafeCode){
        this.allowUnsafeCode = true;
        this.HandleError(Error.IllegalUnsafe);
      }
      bool savedInUnsafeCode = this.inUnsafeCode;
      this.inUnsafeCode = true;
      this.GetNextToken();
      Block b = this.ParseBlock(followers);
      Debug.Assert(b == null || b.IsUnsafe);
      this.inUnsafeCode = savedInUnsafeCode;
      return b;
    }
    private ResourceUse ParseUsing(TokenSet followers){
      ResourceUse resourceUse = new ResourceUse();
      resourceUse.SourceContext = this.scanner.CurrentSourceContext;
      Debug.Assert(this.currentToken == Token.Using);
      this.GetNextToken();
      SourceContext sctx = this.scanner.CurrentSourceContext;
      if (Parser.IdentifierOrNonReservedKeyword[this.currentToken]){
        this.HandleError(Error.SyntaxError, "(");
        this.GetNextToken();
        if (this.currentToken == Token.Semicolon)
          this.GetNextToken();
        this.SkipTo(followers);
        return null;
      }
      this.Skip(Token.LeftParenthesis);
      resourceUse.ResourceAcquisition = this.ParseExpressionStatementOrDeclaration(false, followers|Token.RightParenthesis|Parser.StatementStart, false, false);
      this.ParseBracket(sctx, Token.RightParenthesis, followers|Parser.StatementStart, Error.ExpectedRightParenthesis);
      Block b = this.ParseStatementAsBlock(followers);
      resourceUse.Body = b;
      if (b != null)
        resourceUse.SourceContext.EndPos = b.SourceContext.EndPos;
      return resourceUse;
    }
    private Statement ParseLocalConst(TokenSet followers){
      Debug.Assert(this.currentToken == Token.Const);
      SourceContext sctx = this.scanner.CurrentSourceContext;
      this.GetNextToken();
      TypeNode bt = this.ParseBaseTypeExpression(null, followers|Token.Identifier|Token.Assign|Token.Comma, false, false);
      return this.ParseLocalDeclarations(bt, sctx, true, false, false, true, followers);
    }
    private Statement ParseNewStatement(TokenSet followers, bool preferExpressionToDeclaration){
      Debug.Assert(this.currentToken == Token.New);
      Expression e = this.ParseExpression(followers);
      Statement s = new ExpressionStatement(e);
      s.SourceContext = e.SourceContext;
      if (this.currentToken == Token.Semicolon || !preferExpressionToDeclaration)
        this.SkipSemiColon(followers);
      this.SkipTo(followers);
      return s;
    }
    private Statement ParseObjectLiteralStatement(TokenSet followers, bool preferExpressionToDeclaration){
      Expression e = this.ParseObjectLiteral(followers|Token.Semicolon);
      ExpressionStatement es = new ExpressionStatement(e);
      es.SourceContext = e.SourceContext;
      if (this.currentToken == Token.Semicolon || !preferExpressionToDeclaration)
        this.SkipSemiColon(followers);
      this.SkipTo(followers);
      return es;
    }
    private Statement ParseExpressionStatementOrDeclaration(bool acceptComma, TokenSet followers, bool preferExpressionToDeclaration, bool skipSemicolon){
      SourceContext startingContext = this.scanner.CurrentSourceContext;
      ScannerState ss = this.scanner.state;
      Token tok = this.currentToken;
      TypeNode t = this.ParseTypeOrFunctionTypeExpression(followers|Parser.IdentifierOrNonReservedKeyword, true, false);
      if (t is PointerTypeExpression && !this.inUnsafeCode)
        this.HandleError(t.SourceContext, Error.UnsafeNeeded);
      if (t == null || (!Parser.IdentifierOrNonReservedKeyword[this.currentToken] && 
          (this.sink == null || this.currentToken == Token.LeftParenthesis || 
          (this.currentToken == Token.EndOfFile && (t.TemplateArguments == null || t.TemplateArguments.Count == 0))))){
        //Tried to parse a type expression and failed, or clearly not dealing with a declaration.
        //Restore prior state and reparse as expression
        this.scanner.state = ss;
        this.scanner.endPos = startingContext.StartPos;
        this.currentToken = Token.None;
        this.GetNextToken();
        TokenSet followersOrCommaOrColon = followers|Token.Comma|Token.Colon;
        Expression e = this.ParseExpression(followersOrCommaOrColon);
        ExpressionStatement eStat = new ExpressionStatement(e, startingContext);
        if (e != null) eStat.SourceContext = e.SourceContext;
        Identifier id = null;
        if (this.currentToken == Token.Colon && !acceptComma && (id = e as Identifier) != null)
          return this.ParseLabeledStatement(id, followers);
        if (!acceptComma || this.currentToken != Token.Comma){
          if (!preferExpressionToDeclaration){
            if (!(e == null || e is AssignmentExpression || e is QueryExpression ||
              e is MethodCall || e is PostfixExpression || e is PrefixExpression || 
              e is Construct || followers[Token.RightParenthesis] ||
              (e is Base && this.currentCtor != null && this.inInstanceConstructor == BaseOrThisCallKind.ColonThisOrBaseSeen)
              )
              )
              this.HandleError(e.SourceContext, Error.IllegalStatement);
            if (this.currentToken == Token.Semicolon)
              this.GetNextToken();
            else if (skipSemicolon)
              this.SkipSemiColon(followers);
          }else if (skipSemicolon && (this.currentToken != Token.RightBrace || !followers[Token.RightBrace])){
            if (this.currentToken == Token.Semicolon && followers[Token.RightBrace]){
              //Dealing with an expression block.
              this.GetNextToken();
              if (this.currentToken != Token.RightBrace){
                //Not the last expression in the block. Complain if it is not a valid expression statement.
                if (!(e == null || e is AssignmentExpression || e is MethodCall || e is PostfixExpression || e is PrefixExpression || e is Construct || followers[Token.RightParenthesis]))
                  this.HandleError(e.SourceContext, Error.IllegalStatement);
              }
            }else{
              if (this.currentToken == Token.Comma && this.arrayInitializerOpeningContext != null){
                startingContext = (SourceContext)this.arrayInitializerOpeningContext;
                this.scanner.endPos = startingContext.StartPos;
                this.scanner.state = ss;
                this.currentToken = Token.None;
                this.GetNextToken();
                this.HandleError(Error.ArrayInitToNonArrayType);
                this.ParseArrayInitializer(1, this.TypeExpressionFor(Token.Object), followers, true);
                return null;
              }
              if (this.currentToken == Token.Comma){
                this.HandleError(Error.ExpectedSemicolon);
                this.GetNextToken();
              }else 
                this.SkipSemiColon(followers);
            }
          }
          this.SkipTo(followers);
        }
        return eStat;
      }
      return this.ParseLocalDeclarations(t, startingContext, false, false, preferExpressionToDeclaration, skipSemicolon, followers|Parser.UnaryStart);
    }
    private LabeledStatement ParseLabeledStatement(Identifier label, TokenSet followers){
      LabeledStatement result = new LabeledStatement();
      result.SourceContext = label.SourceContext;
      result.Label = label;
      Debug.Assert(this.currentToken == Token.Colon);
      this.GetNextToken();
      if (Parser.StatementStart[this.currentToken]){
        result.Statement = this.ParseStatement(followers);
        result.SourceContext.EndPos = this.scanner.endPos;
      }else
        this.SkipTo(followers, Error.ExpectedSemicolon);
      return result;
    }
    private Statement ParseLocalDeclarations(TypeNode t, SourceContext ctx, bool constant, bool initOnly, bool preferExpressionToDeclaration, bool skipSemicolon, TokenSet followers){
      TypeNode firstT = t;
      LocalDeclarationsStatement result = new LocalDeclarationsStatement();
      result.SourceContext = ctx;
      result.Constant = constant;
      result.InitOnly = initOnly;
      ScannerState oss = this.scanner.state;
      LocalDeclarationList locList = result.Declarations = new LocalDeclarationList();
      result.Type = result.TypeExpression = t;
      for(;;){
        LocalDeclaration loc = new LocalDeclaration();
        loc.SourceContext = this.scanner.CurrentSourceContext;
        locList.Add(loc);
        loc.Name = this.scanner.GetIdentifier();
        this.SkipIdentifierOrNonReservedKeyword();
        if (this.currentToken == Token.LeftBracket){
          this.HandleError(Error.CStyleArray);
          int endPos = this.scanner.endPos;
          int rank = this.ParseRankSpecifier(true, followers|Token.RightBracket|Parser.IdentifierOrNonReservedKeyword|Token.Assign|Token.Semicolon|Token.Comma);
          if (rank > 0)
            t = result.Type = result.TypeExpression =
              this.ParseArrayType(rank, t, followers|Token.RightBracket|Parser.IdentifierOrNonReservedKeyword|Token.Assign|Token.Semicolon|Token.Comma); 
          else{
            this.currentToken = Token.LeftBracket;
            this.scanner.endPos = endPos;
            this.GetNextToken();
            while (!this.scanner.TokenIsFirstAfterLineBreak && 
              this.currentToken != Token.RightBracket && this.currentToken != Token.Assign && this.currentToken != Token.Semicolon) 
              this.GetNextToken();
            if (this.currentToken == Token.RightBracket) this.GetNextToken();
          }
        }
        if (this.currentToken == Token.LeftParenthesis){
          this.HandleError(Error.BadVarDecl);
          int dummy;
          SourceContext lpCtx = this.scanner.CurrentSourceContext;
          this.GetNextToken();
          this.ParseArgumentList(followers|Token.LeftBrace|Token.Semicolon|Token.Comma, lpCtx, out dummy);
        }else if (this.currentToken == Token.Assign || constant){
          this.Skip(Token.Assign);
          if (this.currentToken == Token.LeftBrace)
            loc.InitialValue = this.ParseArrayInitializer(t, followers|Token.Semicolon|Token.Comma);
          else
            loc.InitialValue = this.ParseExpression(followers|Token.Semicolon|Token.Comma);
        }
        if (loc.InitialValue != null)
          loc.SourceContext.EndPos = loc.InitialValue.SourceContext.EndPos;
        else
          loc.SourceContext.EndPos = this.scanner.endPos;
        if (this.currentToken != Token.Comma) break;
        this.GetNextToken();
        SourceContext sctx = this.scanner.CurrentSourceContext;
        ScannerState ss = this.scanner.state;
        TypeNode ty = this.ParseTypeExpression(null, followers|Token.Identifier|Token.Comma|Token.Semicolon, true);
        if (ty == null || this.currentToken != Token.Identifier){
          this.scanner.endPos = sctx.StartPos;
          this.scanner.state = ss;
          this.currentToken = Token.None;
          this.GetNextToken();
        }else
          this.HandleError(sctx, Error.MultiTypeInDeclaration);
      }
      if (Parser.IsVoidType(firstT)){
        this.HandleError(ctx, Error.NoVoidHere);
        result.Type = this.TypeExpressionFor(Token.Object);
        result.Type.SourceContext = firstT.SourceContext;
      }
      if (preferExpressionToDeclaration && this.currentToken != Token.Semicolon && locList.Count == 1 && locList[0].InitialValue == null){
        //The parse as a declaration is going to fail. Since an expression is preferred, restore the state and reparse as an expression
        this.scanner.endPos = ctx.StartPos;
        this.scanner.state = oss;
        this.currentToken = Token.None;
        this.GetNextToken();
        ExpressionStatement eStat = new ExpressionStatement(this.ParseExpression(followers));
        if (eStat.Expression != null) eStat.SourceContext = eStat.Expression.SourceContext;
        return eStat;
      }
      if (skipSemicolon) this.SkipSemiColon(followers);
      this.SkipTo(followers);
      return result;
    }
    private static bool IsVoidType(TypeNode type){
      TypeExpression tExpr = type as TypeExpression;
      if (tExpr == null) return false;
      Literal lit = tExpr.Expression as Literal;
      if (lit == null) return false;
      if (!(lit.Value is TypeCode)) return false;
      return TypeCode.Empty == (TypeCode)lit.Value;
    }
    private TypeNode ParseTypeOrFunctionTypeExpression(TokenSet followers, bool returnNullIfError, bool asOrIs){
      if (this.currentToken == Token.Delegate)
        return this.ParseDelegateDeclaration(null, null, null, TypeFlags.None, this.scanner.CurrentSourceContext, followers);
      else
        return this.ParseTypeExpression(Identifier.Empty, followers, returnNullIfError, asOrIs, false);
    }
    /// <summary>
    /// The id parameter matters when the start of a type expression is ambiguous
    /// with the start of a constructor.
    /// </summary>
    private TypeNode ParseTypeExpression(Identifier id, TokenSet followers){
      return this.ParseTypeExpression(id, followers, false, false, false);
    }
    /// <summary>
    /// The id parameter matters when the start of a type expression is ambiguous
    /// with the start of a constructor. The returnNullIfError flag allows speculative parsing of the token stream when it is not
    /// clear if an expression or a type expression is expected.
    /// </summary>
    private TypeNode ParseTypeExpression(Identifier id, TokenSet followers, bool returnNullIfError){
      return this.ParseTypeExpression(id, followers, returnNullIfError, false, false);
    }
    private TypeNode ParseTypeExpression(Identifier id, TokenSet followers, bool returnNullIfError, bool AsOrIs, bool Typeof){
      TokenSet followersOrTypeOperator = followers|Parser.TypeOperator;
      TypeNode type = this.ParseBaseTypeExpression(id, followersOrTypeOperator, returnNullIfError, Typeof);
      if (type == null){
        if (!returnNullIfError) this.SkipTo(followers, Error.None);
        return null;
      }
      TypeNode baseType = type;
      int rank = this.ParseRankSpecifier(returnNullIfError, followersOrTypeOperator);
      if (rank > 0){
        returnNullIfError = false; //No longer ambiguous
        type = this.ParseArrayType(rank, type, followersOrTypeOperator);
      }else if (this.currentToken == Token.LeftBracket && returnNullIfError)
        return null;
      else if (rank == -1){
        this.currentToken = Token.LeftBracket;
        this.scanner.endPos = type.SourceContext.EndPos;
        this.GetNextToken();
        goto done;
      }
      for(;;){
        switch(this.currentToken){
          case Token.BitwiseAnd:
            if (returnNullIfError) goto done;
            this.HandleError(Error.ExpectedIdentifier); //TODO: this matches C#, but a better error would be nice
            this.GetNextToken();
            break;
          case Token.LeftBracket:
            returnNullIfError = false;
            rank = this.ParseRankSpecifier(false, followersOrTypeOperator);
            if (rank > 0) type = this.ParseArrayType(rank, type, followersOrTypeOperator); 
            break;
          case Token.Multiply:{
            TypeNode t = new PointerTypeExpression(type);
            t.DeclaringModule = this.module;
            t.SourceContext = type.SourceContext;
            t.SourceContext.EndPos = this.scanner.endPos;
            type = t;
            this.GetNextToken();
            if (!this.inUnsafeCode){
              if (!returnNullIfError)
                this.HandleError(t.SourceContext, Error.UnsafeNeeded);
            }
            break;}
          case Token.LogicalNot:{
            TypeNode t = new NonNullableTypeExpression(type);
            t.SourceContext = type.SourceContext;
            t.SourceContext.EndPos = this.scanner.endPos;
            type = t;
            this.GetNextToken();
            break;}
          case Token.Conditional:{
            if (AsOrIs && Parser.NullableTypeNonFollower[PeekToken()]) goto done;
            TypeNode t = new NullableTypeExpression(type);
            t.SourceContext = type.SourceContext;
            t.SourceContext.EndPos = this.scanner.endPos;
            type = t;
            this.GetNextToken();
            break;}
          default:
            goto done;
        }
      }
    done:
      if (returnNullIfError && !followers[this.currentToken]) return null;
      this.SkipTo(followers);
      return type;
    }
    private TypeNode ParseArrayType(int rank, TypeNode elementType, TokenSet followers){
      SourceContext sctx = elementType.SourceContext;
      Int32List rankList = new Int32List();
      do{
        rankList.Add(rank);
        rank = this.ParseRankSpecifier(false, followers);
      }while (rank > 0);
      for (int i = rankList.Count; i > 0; i--)
        elementType = new ArrayTypeExpression(elementType, rankList[i-1]);
      elementType.SourceContext = sctx;
      return elementType;
    }
    private int ParseRankSpecifier(bool returnZeroIfError, TokenSet followers){
      int rank = 0;
      if (this.currentToken == Token.LeftBracket){
        int startPos = this.scanner.CurrentSourceContext.StartPos;
        ScannerState ss = this.scanner.state;
        rank++;
        SourceContext ctx = this.scanner.CurrentSourceContext;
        this.GetNextToken();
        while (this.currentToken == Token.Comma){
          rank++;
          this.GetNextToken();
        }
        if (returnZeroIfError && this.currentToken != Token.RightBracket && this.currentToken != Token.RightParenthesis){
          this.scanner.endPos = startPos;
          this.scanner.state = ss;
          this.currentToken = Token.None;
          this.GetNextToken();
          return 0;
        }
        if (rank == 1 && this.currentToken != Token.RightBracket)
          return -1;
        this.ParseBracket(ctx, Token.RightBracket, followers, Error.ExpectedRightBracket);
      }else if (!returnZeroIfError)
        this.SkipTo(followers);
      return rank;
    }
    private TypeNode ParseBaseTypeExpression(Identifier className, TokenSet followers, bool returnNullIfError, bool Typeof){
      TypeNode result = null;
      switch(this.currentToken){
        case Token.Bool:
        case Token.Decimal:
        case Token.Sbyte:
        case Token.Byte:
        case Token.Short:
        case Token.Ushort:
        case Token.Int:
        case Token.Uint:
        case Token.Long:
        case Token.Ulong:
        case Token.Char:
        case Token.Float:
        case Token.Double:
        case Token.Object:
        case Token.String:
          result = this.TypeExpressionFor(this.currentToken);
          if (this.sink != null) this.StartTypeName(this.currentToken);
          //TODO: if sink != null start a name (Looker also has to co-operate)
          this.GetNextToken();
          if (returnNullIfError && !followers[this.currentToken]) return null;
          this.SkipTo(followers);
          return result;
        case Token.Void:
          result = this.TypeExpressionFor(this.currentToken);
          SourceContext sctx = this.scanner.CurrentSourceContext;
          this.GetNextToken();
          if (!Parser.IdentifierOrNonReservedKeyword[this.currentToken] && !(this.currentToken == Token.Multiply && this.inUnsafeCode)){
            if (this.currentToken == Token.Operator || this.currentToken == Token.Implicit || this.currentToken == Token.Explicit)
              this.HandleError(sctx, Error.OperatorCantReturnVoid);
            else
              this.HandleError(sctx, Error.NoVoidHere);
            result = this.TypeExpressionFor(Token.Object);
            result.SourceContext = sctx;
          }
          this.SkipTo(followers);
          return result;
        default:
          if (this.currentToken == Token.EndOfFile) {
            this.returnedEmptyIdentForEOF = true;
          }
          Identifier id = this.scanner.GetIdentifier();
          TypeExpression te = null;
          if (className != null && Parser.IdentifierOrNonReservedKeyword[this.currentToken] && className.UniqueIdKey == id.UniqueIdKey){
            this.GetNextToken();
            if (this.currentToken == Token.LeftParenthesis) return this.TypeExpressionFor(Token.Void);
          }else if (returnNullIfError && !Parser.IdentifierOrNonReservedKeyword[this.currentToken])
            return null;
          else if (Parser.IdentifierOrNonReservedKeyword[this.currentToken]){
            this.GetNextToken();
            if (this.currentToken == Token.DoubleColon){
              this.GetNextToken();
              Identifier id2 = this.scanner.GetIdentifier();
              this.SkipIdentifierOrNonReservedKeyword();
              id2.Prefix = id;
              id2.SourceContext.StartPos = id.SourceContext.StartPos;
              te = new TypeExpression(id2);
              id = id2;
            }
            if (this.sink != null)
              this.sink.StartName(id);
          }else{
            this.SkipIdentifierOrNonReservedKeyword(Error.TypeExpected);
          }
          if (this.currentToken == Token.Dot){
            te = new TypeExpression(this.ParseQualifiedIdentifier(id, followers|Token.LessThan, returnNullIfError));
            if (returnNullIfError && te.Expression == null) return null;
          }else if (te == null)
            te = new TypeExpression(id);
          te.SourceContext = te.Expression.SourceContext;
          result = te;
          break;
      }
      while (this.currentToken == Token.LessThan){
        int endCol, arity;
        TypeExpression te = result as TypeExpression;
        result.TemplateArgumentExpressions = this.ParseTypeArguments(returnNullIfError, Typeof && te != null, followers|Token.Dot, out endCol, out arity);
        if (Typeof && te != null && arity > 0)
          te.Arity = arity;
        else{
          result.TemplateArguments = result.TemplateArgumentExpressions == null ? null : result.TemplateArgumentExpressions.Clone();
          if (returnNullIfError && result.TemplateArguments == null && this.sink == null) return null;
          if (result.TemplateArguments == null){
            result.TemplateArguments = new TypeNodeList(1);
            result.TemplateArguments.Add(null);
          }
        }
        if (this.currentToken == Token.Dot) {
          MemberBinding mb = new MemberBinding(null, result, result.SourceContext);
          te = new TypeExpression(this.ParseQualifiedIdentifier(mb, followers|Token.LessThan, returnNullIfError));
          if (returnNullIfError && te.Expression == null) return null;
          te.SourceContext = te.Expression.SourceContext;
          result = te;
        }
      }
      if (returnNullIfError && !followers[this.currentToken]) return null;
      if (className != null){
        if (followers[this.currentToken]) return result;
        this.SkipTo(followers, Error.InvalidMemberDecl, this.scanner.GetTokenSource());
      }else
        this.SkipTo(followers);
      return result;
    }
    private TypeNodeList ParseTypeArguments(bool returnNullIfError, bool Typeof, TokenSet followers, out int endCol, out int arity){
      Debug.Assert(this.currentToken == Token.LessThan);
      if (this.sink != null) this.sink.StartTemplateParameters(this.scanner.CurrentSourceContext);
      arity = 1;
      this.GetNextToken();
      if (Typeof && this.currentToken == Token.GreaterThan){
        endCol = this.scanner.endPos;
        this.GetNextToken();
        if (this.sink != null) this.sink.EndTemplateParameters(this.scanner.CurrentSourceContext); 
        return null;
      }
      SourceContext commaContext = this.scanner.CurrentSourceContext;
      TypeNodeList arguments = new TypeNodeList();
      for(;;){
        if (arity > 0 && Typeof && this.currentToken == Token.Comma){
          arity++;
          this.GetNextToken();
          continue;
        }
        if (arity > 1){
          if (Typeof && this.currentToken == Token.GreaterThan) break;
          this.HandleError(commaContext, Error.TypeExpected);
        }
        arity = 0;
        SourceContext sctx = this.scanner.CurrentSourceContext;
        TypeNode t = this.ParseTypeExpression(null, followers|Token.Comma|Token.GreaterThan|Token.RightShift, true);
        if (this.sink != null && t != null) {
          sctx.EndPos = this.scanner.endPos;
          this.sink.NextTemplateParameter(sctx);
        }
        endCol = this.scanner.endPos;
        if (returnNullIfError && t == null) {
            if (this.sink != null) this.sink.EndTemplateParameters(this.scanner.CurrentSourceContext);
            return null;
        }
        arguments.Add(t);
        if (this.currentToken != Token.Comma) break;
        this.GetNextToken();
      }
      if (this.sink != null) this.sink.EndTemplateParameters(this.scanner.CurrentSourceContext);
      endCol = this.scanner.endPos;
      if (returnNullIfError && this.currentToken != Token.GreaterThan && this.currentToken != Token.RightShift && this.currentToken != Token.EndOfFile) 
        return null;
      if (this.currentToken == Token.RightShift)
        this.currentToken = Token.GreaterThan;
      else if (this.currentToken == Token.GreaterThan)
        this.Skip(Token.GreaterThan);
      return arguments;
    }
    private Statement ParseExpose(TokenSet followers, NodeType nodeType, bool preferExpressionToDeclaration){
      Token tok = this.currentToken;
      int tokStartPos = this.scanner.CurrentSourceContext.StartPos;
      ScannerState ss = this.scanner.state;
      Expose expose = new Expose(nodeType);
      if (tok == Token.Expose) expose.IsLocal = true;
      expose.SourceContext = this.scanner.CurrentSourceContext;
      this.GetNextToken();
      if (tok == Token.Read || tok == Token.Write) {
        if (this.currentToken != Token.Lock) goto AbandonExpose;
        this.GetNextToken();
      } else if (tok == Token.Additive) {
        if (this.currentToken != Token.Expose) goto AbandonExpose;
        this.GetNextToken();
      } else if (this.currentToken != Token.LeftParenthesis)
        goto AbandonExpose;
      SourceContext sctx = this.scanner.CurrentSourceContext;
      this.Skip(Token.LeftParenthesis);
      expose.Instance = this.ParseExpression(followers | Token.RightParenthesis | Parser.StatementStart);
      this.ParseBracket(sctx, Token.RightParenthesis, followers|Parser.StatementStart, Error.ExpectedRightParenthesis);
      
      Block b = this.ParseStatementAsBlock(followers);
      expose.Body = b;
      if (b != null){
        expose.SourceContext.EndPos = b.SourceContext.EndPos;
        if (b.Statements == null)
          this.HandleError(b.SourceContext, Error.PossibleMistakenNullStatement);
      }
      return expose;
    AbandonExpose:
      this.scanner.endPos = tokStartPos;
      this.scanner.state = ss;
      this.currentToken = Token.None;
      this.GetNextToken();
      return this.ParseExpressionStatementOrDeclaration(false, followers, preferExpressionToDeclaration, true);
    }
    private Acquire ParseAcquire(TokenSet followers){
      Acquire acquire = new Acquire();
      acquire.SourceContext = this.scanner.CurrentSourceContext;
      this.GetNextToken();
      SourceContext sctx = this.scanner.CurrentSourceContext;
      if (this.currentToken == Token.Readonly){
        acquire.ReadOnly = true;
        this.GetNextToken();
      }
      this.Skip(Token.LeftParenthesis);

      acquire.Target = this.ParseExpressionStatementOrDeclaration(false, followers|Token.RightParenthesis|Parser.StatementStart|Token.Semicolon, false, false);
      if (this.currentToken == Token.Semicolon){
        this.GetNextToken();
        acquire.Condition = this.ParseExpression(followers|Token.RightParenthesis|Parser.StatementStart);
      }
      this.ParseBracket(sctx, Token.RightParenthesis, followers|Parser.StatementStart, Error.ExpectedRightParenthesis);

      Block b = this.ParseStatementAsBlock(followers);
      acquire.Body = b;
      if (b != null){
        acquire.SourceContext.EndPos = b.SourceContext.EndPos;
        if (b.Statements == null)
          this.HandleError(b.SourceContext, Error.PossibleMistakenNullStatement);
      }
      return acquire;
    }
    bool returnedEmptyIdentForEOF = false;
    private Expression ParseExpression(TokenSet followers){
      if (this.currentToken == Token.EndOfFile) {
        if (this.returnedEmptyIdentForEOF)
          return null;
        else {
          this.returnedEmptyIdentForEOF = true;
          return this.scanner.GetIdentifier();
        }
      }
      TokenSet followersOrInfixOperators = followers|Parser.InfixOperators;
      followersOrInfixOperators[Token.Private] = false;
      Expression operand1 = this.ParseUnaryExpression(followersOrInfixOperators);
      if (followers[this.currentToken] && !Parser.InfixOperators[this.currentToken]) return operand1;
      if (this.currentToken == Token.Conditional)
        return this.ParseConditional(operand1, followers);
      else if (this.currentToken == Token.Private)
        return operand1;
      else
        return this.ParseAssignmentExpression(operand1, followers);
    }
    private Expression ParseParenthesizedExpression(TokenSet followers){
      return this.ParseParenthesizedExpression(followers, false);
    }
    private Expression ParseParenthesizedExpression(TokenSet followers, bool keepParentheses){
      SourceContext sctx = this.scanner.CurrentSourceContext;
      if (this.currentToken == Token.LeftBrace){
        this.SkipTo(followers, Error.SyntaxError, "(");
        return null;
      }
      this.Skip(Token.LeftParenthesis);
      Expression result1 = this.ParseExpression(followers|Token.RightParenthesis|Token.Colon);
      if (this.currentToken == Token.Colon){
        this.GetNextToken();
        Expression result2 = this.ParseExpression(followers|Token.RightParenthesis);
        if (result2 == null) return null;
        result1 = new BinaryExpression(result1, 
          new BinaryExpression(result2, new Literal(1, null, result2.SourceContext), NodeType.Sub), NodeType.Range);
      }
      int bracketPos = this.scanner.endPos;
      this.ParseBracket(sctx, Token.RightParenthesis, followers, Error.ExpectedRightParenthesis);
      if (keepParentheses){
        sctx.EndPos = bracketPos;
        return new UnaryExpression(result1, NodeType.Parentheses, sctx);
      }else
        return result1;
    }
    private Expression ParseAssignmentExpression(Expression operand1, TokenSet followers){
      Debug.Assert(Parser.InfixOperators[this.currentToken]);
      Debug.Assert(this.currentToken != Token.Conditional);
      switch (this.currentToken){
        case Token.AddAssign:
        case Token.Assign:
        case Token.BitwiseAndAssign:
        case Token.BitwiseOrAssign:
        case Token.BitwiseXorAssign:
        case Token.DivideAssign:
        case Token.LeftShiftAssign:
        case Token.MultiplyAssign:
        case Token.RemainderAssign:
        case Token.RightShiftAssign:
        case Token.SubtractAssign:
          Token assignmentOperator = this.currentToken;
          this.GetNextToken();
          Expression operand2 = this.ParseExpression(followers);
          if (operand1 == null || operand2 == null) return null;
          AssignmentStatement statement = new AssignmentStatement(operand1, operand2, Parser.ConvertToBinaryNodeType(assignmentOperator));
          statement.SourceContext = operand1.SourceContext;
          statement.SourceContext.EndPos = operand2.SourceContext.EndPos;
          Expression expression = new AssignmentExpression(statement);
          expression.SourceContext = statement.SourceContext;
          return expression;
        default:
          operand1 = this.ParseBinaryExpression(operand1, followers|Token.Conditional);
          if (operand1 != null && this.currentToken == Token.Conditional)
            return this.ParseConditional(operand1, followers);
          return operand1;
      }
    }
    private Expression ParseConditional(Expression condition, TokenSet followers){
      Debug.Assert(this.currentToken == Token.Conditional);
      this.GetNextToken();
      Expression trueExpr = this.ParseExpression(followers|Token.Colon);
      if (trueExpr == null) //Supply a dummy
        trueExpr = new Literal(null, null, this.scanner.CurrentSourceContext);
      Expression falseExpr;
      if (this.currentToken == Token.Colon){
        this.GetNextToken();
        falseExpr = this.ParseExpression(followers);
      }else{
        this.Skip(Token.Colon); //gives appropriate error message
        if (!followers[this.currentToken])
          //Assume that only the : is missing. Go ahead as if it were specified.
          falseExpr = this.ParseExpression(followers);
        else
          falseExpr = null;
      }
      if (falseExpr == null) //Supply a dummy
        falseExpr = new Literal(null, null, this.scanner.CurrentSourceContext);
      TernaryExpression result = new TernaryExpression(condition, trueExpr, falseExpr, NodeType.Conditional, null);
      result.SourceContext = condition.SourceContext;
      result.SourceContext.EndPos = falseExpr.SourceContext.EndPos;
      this.SkipTo(followers);
      return result;
    }
    private Expression ParseBinaryExpression(Expression operand1, TokenSet followers){
      TokenSet unaryFollowers = followers|Parser.InfixOperators;
      Expression expression;
      switch(this.currentToken){
        case Token.Plus:
        case Token.As:
        case Token.BitwiseAnd:
        case Token.BitwiseOr:
        case Token.BitwiseXor:
        case Token.Divide:
        case Token.Equal:
        case Token.GreaterThan:
        case Token.GreaterThanOrEqual:
        case Token.Iff:
        case Token.In:
        case Token.Implies:
        case Token.Is:
        case Token.LeftShift:
        case Token.LessThan:
        case Token.LessThanOrEqual:
        case Token.LogicalAnd:
        case Token.LogicalOr:
        case Token.Maplet:
        case Token.Multiply:
        case Token.NotEqual:
        case Token.NullCoalescingOp:
        case Token.Range:
        case Token.Remainder:
        case Token.RightShift:
        case Token.Subtract:
          Token operator1 = this.currentToken;
          this.GetNextToken();
          Expression operand2 = null;
          if (operator1 == Token.Is || operator1 == Token.As){
            SourceContext ctx = this.scanner.CurrentSourceContext;
            TypeNode te = this.ParseTypeOrFunctionTypeExpression(unaryFollowers, false, true);
            operand2 = new MemberBinding(null, te);
            if (te is TypeExpression)
              operand2.SourceContext = te.SourceContext;
            else
              operand2.SourceContext = ctx;
          }else
            operand2 = this.ParseUnaryExpression(unaryFollowers);
          switch(this.currentToken){
            case Token.Plus:
            case Token.As:
            case Token.BitwiseAnd:
            case Token.BitwiseOr:
            case Token.BitwiseXor:
            case Token.Divide:
            case Token.Equal:
            case Token.GreaterThan:
            case Token.GreaterThanOrEqual:
            case Token.Iff:
            case Token.Implies:
            case Token.In:
            case Token.Is:
            case Token.LeftShift:
            case Token.LessThan:
            case Token.LessThanOrEqual:
            case Token.LogicalAnd:
            case Token.LogicalOr:
            case Token.Maplet:
            case Token.Multiply:
            case Token.NotEqual:
            case Token.NullCoalescingOp:
            case Token.Range:
            case Token.Remainder:
            case Token.RightShift:
            case Token.Subtract:
              expression = this.ParseComplexExpression(Token.None, operand1, operator1, operand2, followers, unaryFollowers);
              break;
            default:
              expression = new BinaryExpression(operand1, operand2, Parser.ConvertToBinaryNodeType(operator1));
              if (operand1 != null && operand2 != null){
                expression.SourceContext = operand1.SourceContext;
                expression.SourceContext.EndPos = operand2.SourceContext.EndPos;
              }else
                expression = null;
              break;
          }
          break;
        default:
          expression = operand1;
          break;
      }
      return expression;
    }
    private Expression ParseComplexExpression(Token operator0, Expression operand1, Token operator1, Expression operand2, TokenSet followers, TokenSet unaryFollowers){
    restart:
      Token operator2 = this.currentToken;
      this.GetNextToken();
      Expression expression = null;
      Expression operand3 = null;
      if (operator2 == Token.Is || operator2 == Token.As){
        TypeNode type3 = this.ParseTypeExpression(Identifier.Empty, unaryFollowers);
        if (type3 != null)
          operand3 = new MemberBinding(null, type3, type3.SourceContext);
      }else
        operand3 = this.ParseUnaryExpression(unaryFollowers);
      if (Parser.LowerPriority(operator1, operator2)){
        switch(this.currentToken){
          case Token.Plus:
          case Token.As:
          case Token.BitwiseAnd:
          case Token.BitwiseOr:
          case Token.BitwiseXor:
          case Token.Divide:
          case Token.Equal:
          case Token.GreaterThan:
          case Token.GreaterThanOrEqual:
          case Token.Iff:
          case Token.Implies:
          case Token.In:
          case Token.Is:
          case Token.LeftShift:
          case Token.LessThan:
          case Token.LessThanOrEqual:
          case Token.LogicalAnd:
          case Token.LogicalOr:
          case Token.Maplet:
          case Token.Multiply:
          case Token.NotEqual:
          case Token.NullCoalescingOp:
          case Token.Range:
          case Token.Remainder:
          case Token.RightShift:
          case Token.Subtract:
            if (Parser.LowerPriority(operator2, this.currentToken))
              //Can't reduce just operand2 op2 operand3 because there is an op3 with priority over op2
              operand2 = this.ParseComplexExpression(operator1, operand2, operator2, operand3, followers, unaryFollowers); //reduce complex expression
              //Now either at the end of the entire expression, or at an operator that is at the same or lower priority than op1
              //Either way, operand2 op2 operand3 op3 ... has been reduced to just operand2 and the code below will
              //either restart this procedure to parse the remaining expression or reduce operand1 op1 operand2 and return to the caller
            else
              goto default;
            break;
          default:
            //Reduce operand2 op2 operand3. There either is no further binary operator, or it does not take priority over op2.
            expression = new BinaryExpression(operand2, operand3, Parser.ConvertToBinaryNodeType(operator2)); 
            if (operand2 != null && operand3 != null){
              expression.SourceContext = operand2.SourceContext;
              expression.SourceContext.EndPos = operand3.SourceContext.EndPos;
            }else
              expression = null;
            operand2 = expression;
            //The code following this will reduce operand1 op1 operand2 and return to the caller
            break;
        }
      }else{
        Expression opnd1 = new BinaryExpression(operand1, operand2, Parser.ConvertToBinaryNodeType(operator1));
        if (operand1 != null && operand2 != null){
          opnd1.SourceContext = operand1.SourceContext;
          opnd1.SourceContext.EndPos = operand2.SourceContext.EndPos;
        }else
          opnd1 = null;
        operand1 = opnd1;
        operand2 = operand3;
        operator1 = operator2;
      }
      //At this point either operand1 op1 operand2 has been reduced, or operand2 op2 operand3 .... has been reduced, so back to just two operands
      switch(this.currentToken){
        case Token.Plus:
        case Token.As:
        case Token.BitwiseAnd:
        case Token.BitwiseOr:
        case Token.BitwiseXor:
        case Token.Divide:
        case Token.Equal:
        case Token.GreaterThan:
        case Token.GreaterThanOrEqual:
        case Token.In:
        case Token.Iff:
        case Token.Implies:
        case Token.Is:
        case Token.LeftShift:
        case Token.LessThan:
        case Token.LessThanOrEqual:
        case Token.LogicalAnd:
        case Token.LogicalOr:
        case Token.Maplet:
        case Token.Multiply:
        case Token.NotEqual:
        case Token.NullCoalescingOp:
        case Token.Range:
        case Token.Remainder:
        case Token.RightShift:
        case Token.Subtract:
          if (operator0 == Token.None || Parser.LowerPriority(operator0, this.currentToken))
            //The caller is not prepared to deal with the current token, go back to the start of this routine and consume some more tokens
            goto restart;
          else
            goto default; //Let the caller deal with the current token
        default:
          //reduce operand1 op1 operand2 and return to caller
          expression = new BinaryExpression(operand1, operand2, Parser.ConvertToBinaryNodeType(operator1)); 
          if (operand1 != null && operand2 != null){
            expression.SourceContext = operand1.SourceContext;
            expression.SourceContext.EndPos = operand2.SourceContext.EndPos;
          }else
            expression = null;
          break;
      }
      return expression;
    }
    private Expression ParseUnaryExpression(TokenSet followers){
      if (this.currentToken == Token.EndOfFile) {
        if (this.returnedEmptyIdentForEOF)
          return null;
        else {
          this.returnedEmptyIdentForEOF = true;
          return this.scanner.GetIdentifier();
        }
      }
      Expression expression;
      switch(this.currentToken){
        case Token.Plus:
        case Token.BitwiseNot:
        case Token.LogicalNot:
        case Token.Subtract:
        case Token.BitwiseAnd:
          UnaryExpression uexpr = new UnaryExpression();
          uexpr.SourceContext = this.scanner.CurrentSourceContext;
          uexpr.NodeType = Parser.ConvertToUnaryNodeType(this.currentToken);
          this.GetNextToken();
          uexpr.Operand = this.ParseUnaryExpression(followers);
          if (uexpr.Operand == null) return null;
          uexpr.SourceContext.EndPos = uexpr.Operand.SourceContext.EndPos;
          expression = uexpr;
          break;
        case Token.Multiply:
          AddressDereference adref = new AddressDereference();
          adref.SourceContext = this.scanner.CurrentSourceContext;
          adref.ExplicitOperator = AddressDereference.ExplicitOp.Star;
          this.GetNextToken();
          adref.Address = this.ParseUnaryExpression(followers);
          if (adref.Address == null) return null;
          adref.SourceContext.EndPos = adref.Address.SourceContext.EndPos;
          expression = adref;
          break;
        case Token.AddOne:
        case Token.SubtractOne:
          PrefixExpression prefixExpr = new PrefixExpression();
          prefixExpr.SourceContext = this.scanner.CurrentSourceContext;
          prefixExpr.Operator = Parser.ConvertToBinaryNodeType(this.currentToken);
          this.GetNextToken();
          prefixExpr.Expression = this.ParseUnaryExpression(followers);
          if (prefixExpr.Expression == null) return null;
          prefixExpr.SourceContext.EndPos = prefixExpr.Expression.SourceContext.EndPos;
          expression = prefixExpr;
          break;
        case Token.LeftParenthesis:
          expression = this.ParseCastExpression(followers);
          break;
        case Token.LeftBrace:
          expression = this.ParseBlockExpression(ScannerState.Code, this.scanner.CurrentSourceContext, followers);
          break;
        case Token.Exists:
        case Token.Forall:
        case Token.Count:
        case Token.Sum:
        case Token.Max:
        case Token.Min:
        case Token.Product:
          Quantifier q = new Quantifier();
          q.SourceContext = this.scanner.CurrentSourceContext;
          ScannerState ss = this.scanner.state;
          Token quantType = this.currentToken;
          SourceContext sctx = this.scanner.CurrentSourceContext;
          this.GetNextToken();
          if (this.currentToken == Token.Unique){
            quantType = this.currentToken;
            this.GetNextToken();
          }
          // not necessarily a quantifier unless there is an open curly!!
          // REVIEW: Is that enough of a test? Or do we need to try parsing the comprehension?
          if (this.currentToken != Token.LeftBrace) {
            //Restore prior state and reparse as expression
            this.scanner.endPos = q.SourceContext.StartPos;
            this.scanner.state = ss;
            this.currentToken = Token.None;
            this.GetNextToken();
            goto default;
          }
          q.QuantifierType = Parser.ConvertToQuantifierType(quantType);
          this.Skip(Token.LeftBrace);
          Comprehension c = this.ParseComprehension(followers, sctx);
          if (c == null){
            this.GetNextToken();
            return null;
          }
          q.Comprehension = c;
          q.SourceContext.EndPos = c.SourceContext.EndPos;
          expression = q;
          break;
        default:
          expression = this.ParsePrimaryExpression(followers);
          break;
      }
      return expression;
    }
    private Expression ParseCastExpression(TokenSet followers){
      SourceContext sctx = this.scanner.CurrentSourceContext;
      ScannerState ss = this.scanner.state;
      Debug.Assert(this.currentToken == Token.LeftParenthesis);
      this.GetNextToken();
      Token tok;
      TypeNode t;
      if (this.currentToken == Token.LogicalNot) {
        // non-null cast (!)
        this.GetNextToken();
        t = TypeExpressionFor("Microsoft", "Contracts", "NonNullType");
        tok = this.currentToken;
      }else{
        t = this.ParseTypeExpression(null, followers|Token.RightParenthesis, true);
        tok = this.currentToken;
      }
      if (t != null && tok == Token.RightParenthesis){
        this.GetNextToken(); //TODO: bracket matching
        bool wasCast = this.inUnsafeCode && (this.currentToken == Token.BitwiseAnd || this.currentToken == Token.Multiply) && t is PointerTypeExpression;
        if (wasCast || (this.currentToken == Token.Default || Parser.UnaryStart[this.currentToken]) &&
          (!Parser.InfixOperators[this.currentToken] || this.TypeExpressionIsUnambiguous(t as TypeExpression))){
          BinaryExpression result = new BinaryExpression(this.ParseUnaryExpression(followers), new MemberBinding(null, t, t.SourceContext), NodeType.ExplicitCoercion);
          result.SourceContext = sctx;
          if (result.Operand1 != null) result.SourceContext.EndPos = result.Operand1.SourceContext.EndPos;
          return result;
        }
      }
      //Tried to parse a parenthesized type expression followed by a unary expression, but failed. 
      //Reset the scanner to the state at the start of this routine and parse as a parenthesized expression
      bool isLambda = this.currentToken == Token.Lambda;
      this.scanner.endPos = sctx.StartPos;
      this.scanner.state = ss;
      this.currentToken = Token.None;
      this.GetNextToken();
      if (isLambda) return this.ParseLambdaExpression(followers);
      return this.ParsePrimaryExpression(followers);
    }
    private Expression ParseLambdaExpression(TokenSet followers) {
      SourceContext sctx = this.scanner.CurrentSourceContext;
      ParameterList pars = this.ParseParameters(Token.RightParenthesis, followers|Token.LeftBrace, false, true);
      return ParseLambdaExpression(sctx, pars, followers);
    }
    private Expression ParseLambdaExpression(SourceContext sctx, ParameterList pars, TokenSet followers) {
      Debug.Assert(this.currentToken == Token.Lambda);
      int lambdaPos = this.scanner.endPos;
      Block body;
      this.GetNextToken();
      if (this.currentToken == Token.LeftBrace)
        body = this.ParseBlock(followers);
      else{
        Expression expr = this.ParseExpression(followers);
        if (expr == null) return null;
        body = new Block(new StatementList(new Return(expr, expr.SourceContext)), expr.SourceContext);
      }
      AnonymousNestedFunction func = new AnonymousNestedFunction(pars, body);
      func.SourceContext = sctx;
      func.SourceContext.EndPos = lambdaPos;
      return func;
    }
    private bool TypeExpressionIsUnambiguous(TypeExpression texpr){
      if (texpr == null) return false;
      if (texpr.Expression is Literal && ((Literal)texpr.Expression).Value is TypeCode) return true;
      Expression e = texpr.Expression;
      for (QualifiedIdentifier qual = e as QualifiedIdentifier; qual != null; qual = qual.Qualifier as QualifiedIdentifier);
      Identifier id = e as Identifier;
      if (id != null && id.Prefix != null) return true;
      return false;
    }
    private Expression ParsePrimaryExpression(TokenSet followers){
      Expression expression = null;
      SourceContext sctx = this.scanner.CurrentSourceContext;
      switch(this.currentToken){
        case Token.ArgList:
          this.GetNextToken();
          expression = new ArglistExpression(sctx);
          break;
        case Token.Delegate:{
          this.GetNextToken();
          ParameterList parameters = null;
          if (this.currentToken == Token.LeftParenthesis)
            parameters = this.ParseParameters(Token.RightParenthesis, followers|Token.LeftBrace);
          Block block = null;
          if (this.currentToken == Token.LeftBrace)
            block = this.ParseBlock(this.scanner.CurrentSourceContext, followers);
          else
            this.SkipTo(followers, Error.ExpectedLeftBrace);
          sctx.EndPos = this.scanner.endPos;
          return new AnonymousNestedDelegate(parameters, block, sctx);}
        case Token.New:
          expression = this.ParseNew(followers|Token.Dot|Token.LeftBracket|Token.Arrow);
          break;
        case Token.Identifier:
          expression = this.scanner.GetIdentifier();
          if (this.sink != null) {
            this.sink.StartName((Identifier)expression);
          }
          this.GetNextToken();
          if (this.currentToken == Token.DoubleColon){
            this.GetNextToken();
            Identifier id = this.scanner.GetIdentifier();
            id.Prefix = (Identifier)expression;
            id.SourceContext.StartPos = expression.SourceContext.StartPos;
            expression = id;
            if (this.currentToken != Token.EndOfFile)
              this.GetNextToken();
          }else if (this.currentToken == Token.Lambda){
            Parameter par = new Parameter((Identifier)expression, null);
            par.SourceContext = expression.SourceContext;
            return this.ParseLambdaExpression(par.SourceContext, new ParameterList(par), followers);
          }
          break;          
        case Token.Null:
          expression = new Literal(null, null, sctx);
          this.GetNextToken();
          break;
        case Token.True:
          expression = new Literal(true, null, sctx);
          this.GetNextToken();
          break;
        case Token.False:
          expression = new Literal(false, null, sctx);
          this.GetNextToken();
          break;
        case Token.Hole: //HS D
          expression = new Hole(sctx);
          this.GetNextToken();
          break;
        case Token.CharLiteral:
          expression = new Literal(this.scanner.charLiteralValue, null, sctx);
          this.GetNextToken();
          break;
        case Token.HexLiteral:
          expression = this.ParseHexLiteral();
          break;
        case Token.IntegerLiteral:
          expression = this.ParseIntegerLiteral();
          break;
        case Token.RealLiteral:
          expression = this.ParseRealLiteral();
          break;
        case Token.StringLiteral:
          expression = this.scanner.GetStringLiteral();
          this.GetNextToken();
          break;
        case Token.This:
          expression = new This(sctx, false);
          if (this.sink != null) {
            this.sink.StartName(expression);
          }
          this.GetNextToken();
          if (this.currentToken == Token.LeftParenthesis
            && (this.inInstanceConstructor==BaseOrThisCallKind.None
            || this.inInstanceConstructor==BaseOrThisCallKind.InCtorBodyThisSeen)){
            QualifiedIdentifier thisCons = new QualifiedIdentifier(expression, StandardIds.Ctor, this.scanner.CurrentSourceContext);
            MethodCall thisConstructorCall = new MethodCall(thisCons, null, NodeType.Call);
            thisConstructorCall.SourceContext = sctx;
            SourceContext lpCtx = this.scanner.CurrentSourceContext;
            this.Skip(Token.LeftParenthesis);
            thisConstructorCall.Operands = this.ParseArgumentList(followers|Token.LeftBrace|Token.Semicolon, lpCtx, out thisConstructorCall.SourceContext.EndPos);
            expression = thisConstructorCall;
            this.inInstanceConstructor=BaseOrThisCallKind.InCtorBodyThisSeen;
            goto done;
          }
          break;
        case Token.Base:
          Base ba = new Base(sctx, false);
          expression = ba;
          if (this.sink != null) {
            this.sink.StartName(expression);
          }
          this.GetNextToken();
          if (this.currentToken == Token.Semicolon &&
            (this.inInstanceConstructor == BaseOrThisCallKind.ColonThisOrBaseSeen
            || this.inInstanceConstructor == BaseOrThisCallKind.None)) {
            // When there are non-null fields, then the base ctor call can happen only after they are
            // initialized.
            // In Spec#, we allow a base ctor call in the body of the ctor. But if someone is using
            // the C# comment convention, then they cannot do that.
            // So allow "base;" as a marker to indicate where the base ctor call should happen.
            // There may be an explicit "colon base call" or it may be implicit.
            //
            // Just leave expression as a bare "Base" node; later pipeline stages will all have
            // to ignore it. Mark the current ctor as having (at least) one of these bad boys
            // in it.
            ba.UsedAsMarker = true;
            this.currentCtor.ContainsBaseMarkerBecauseOfNonNullFields = true;
            goto done;
          }
          if (this.currentToken == Token.LeftParenthesis
            && (this.inInstanceConstructor==BaseOrThisCallKind.None
              || this.inInstanceConstructor==BaseOrThisCallKind.InCtorBodyBaseSeen)){
            QualifiedIdentifier supCons = new QualifiedIdentifier(expression, StandardIds.Ctor, this.scanner.CurrentSourceContext);
            MethodCall superConstructorCall = new MethodCall(supCons, null, NodeType.Call);
            superConstructorCall.SourceContext = sctx;
            SourceContext lpCtx = this.scanner.CurrentSourceContext;
            this.Skip(Token.LeftParenthesis);
            superConstructorCall.Operands = this.ParseArgumentList(followers|Token.LeftBrace|Token.Semicolon, lpCtx, out superConstructorCall.SourceContext.EndPos);
            expression = superConstructorCall;
            this.inInstanceConstructor=BaseOrThisCallKind.InCtorBodyBaseSeen;
            goto done;
          }
          break;
        case Token.Typeof:
        case Token.Sizeof:
        case Token.Default:{
          //if (this.currentToken == Token.Sizeof && !this.inUnsafeCode)
            //this.HandleError(Error.SizeofUnsafe);
          UnaryExpression uex = new UnaryExpression(null, 
            this.currentToken == Token.Typeof ? NodeType.Typeof : this.currentToken == Token.Sizeof ? NodeType.Sizeof : NodeType.DefaultValue);
          uex.SourceContext = sctx;
          this.GetNextToken();
          this.Skip(Token.LeftParenthesis);
          TypeNode t = null;
          if (this.currentToken == Token.Void && uex.NodeType == NodeType.Typeof){
            t = this.TypeExpressionFor(Token.Void); this.GetNextToken();
          }else
            t = this.ParseTypeExpression(null, followers|Token.RightParenthesis, false, false, uex.NodeType == NodeType.Typeof);
          if (t == null){this.SkipTo(followers); return null;}
          uex.Operand = new MemberBinding(null, t, t.SourceContext, null);
          uex.Operand.SourceContext = t.SourceContext;
          uex.SourceContext.EndPos = this.scanner.endPos;
          this.Skip(Token.RightParenthesis);
          expression = uex;
          break;}
        case Token.Stackalloc:{
          this.GetNextToken();
          TypeNode elementType = this.ParseBaseTypeExpression(null, followers|Token.LeftBracket, false, false);
          if (elementType == null){this.SkipTo(followers); return null;}
          Token openingDelimiter = this.currentToken;
          if (this.currentToken != Token.LeftBracket){
            this.HandleError(Error.BadStackAllocExpr);
            if (this.currentToken == Token.LeftParenthesis) this.GetNextToken();
          }else
            this.GetNextToken();
          Expression numElements = this.ParseExpression(followers|Token.RightBracket|Token.RightParenthesis);
          sctx.EndPos = this.scanner.endPos;
          if (this.currentToken == Token.RightParenthesis && openingDelimiter == Token.LeftParenthesis)
            this.GetNextToken();
          else
            this.Skip(Token.RightBracket);
          this.SkipTo(followers);
          return new StackAlloc(elementType, numElements, sctx);}
        case Token.Checked:
        case Token.Unchecked:
          //TODO: use NodeType.SkipCheck and NodeType.EnforceCheck
          Block b = new Block(new StatementList(1), this.currentToken == Token.Checked, this.currentToken == Token.Unchecked, this.inUnsafeCode);
          b.SourceContext = sctx;
          this.GetNextToken();
          this.Skip(Token.LeftParenthesis);
          b.Statements.Add(new ExpressionStatement(this.ParseExpression(followers|Token.RightParenthesis)));
          this.Skip(Token.RightParenthesis);
          expression = new BlockExpression(b);
          expression.SourceContext = b.SourceContext;
          break;
        case Token.RefType:{
          this.GetNextToken();
          this.Skip(Token.LeftParenthesis);
          Expression e = this.ParseExpression(followers|Token.RightParenthesis);
          this.Skip(Token.RightParenthesis);
          expression = new RefTypeExpression(e, sctx);
          break;
        }
        case Token.RefValue:{
          this.GetNextToken();
          this.Skip(Token.LeftParenthesis);
          Expression e = this.ParseExpression(followers|Token.Comma);
          this.Skip(Token.Comma);
          TypeNode te = this.ParseTypeOrFunctionTypeExpression(followers|Token.RightParenthesis, false, true);
          Expression operand2 = new MemberBinding(null, te);
          if (te is TypeExpression)
            operand2.SourceContext = te.SourceContext;
          else
            operand2.SourceContext = sctx;
          this.Skip(Token.RightParenthesis);
          expression = new RefValueExpression(e, operand2, sctx);
          break;
        }
        case Token.Bool:
        case Token.Decimal:
        case Token.Sbyte:
        case Token.Byte:
        case Token.Short:
        case Token.Ushort:
        case Token.Int:
        case Token.Uint:
        case Token.Long:
        case Token.Ulong:
        case Token.Char:
        case Token.Float:
        case Token.Double:
        case Token.Object:
        case Token.String:
          MemberBinding mb = new MemberBinding(null, this.TypeExpressionFor(this.currentToken), sctx);
          this.GetNextToken();
          expression = this.ParseIndexerCallOrSelector(mb, followers);

          goto done;
        case Token.LeftParenthesis:
          expression = this.ParseParenthesizedExpression(followers|Token.Dot|Token.LeftBracket|Token.Arrow, true);
          break;
        default:
          if (Parser.IdentifierOrNonReservedKeyword[this.currentToken]) goto case Token.Identifier;
          if (Parser.InfixOperators[this.currentToken]){
            this.HandleError(Error.InvalidExprTerm, this.scanner.GetTokenSource());
            this.GetNextToken();
          }else
            this.SkipTo(followers|Parser.PrimaryStart, Error.InvalidExprTerm, this.scanner.GetTokenSource());
          if (Parser.PrimaryStart[this.currentToken]) return this.ParsePrimaryExpression(followers);
          goto done;
      }
      if (expression is Base && this.currentToken != Token.Dot && this.currentToken != Token.LeftBracket){
        this.HandleError(expression.SourceContext, Error.BaseIllegal);
        expression = null;
      }
      

      expression = this.ParseIndexerCallOrSelector(expression, followers|Token.AddOne|Token.SubtractOne);
      for(;;){
        switch(this.currentToken){
          case Token.AddOne:
          case Token.SubtractOne:
            SourceContext ctx = expression.SourceContext;
            ctx.EndPos = this.scanner.endPos;
            PostfixExpression pex = new PostfixExpression(expression, Parser.ConvertToBinaryNodeType(this.currentToken), ctx);
            this.GetNextToken();
            expression = pex;
            break;
          case Token.Dot:
            expression = this.ParseIndexerCallOrSelector(expression, followers|Token.AddOne|Token.SubtractOne);
            break;
          default:
            goto done;
        }
      }
      done:
        this.SkipTo(followers);
      return expression;
    }
    private Literal ParseHexLiteral(){
      Debug.Assert(this.currentToken == Token.HexLiteral);
      string tokStr = this.scanner.GetTokenSource();
      SourceContext ctx = this.scanner.CurrentSourceContext;
      TypeCode tc = this.scanner.ScanNumberSuffix();
      Literal result;
      try{
        switch(tc){
          case TypeCode.Single:
          case TypeCode.Double:
          case TypeCode.Decimal:
            this.HandleError(Error.ExpectedSemicolon);
            goto default;
          default:
            ulong ul = UInt64.Parse(tokStr.Substring(2), System.Globalization.NumberStyles.HexNumber, null);
            if (ul <= int.MaxValue && tc == TypeCode.Empty)
              result = new Literal((int)ul);
            else if (ul <= uint.MaxValue && (tc == TypeCode.Empty || tc == TypeCode.UInt32))
              result = new Literal((uint)ul);
            else if (ul <= long.MaxValue && (tc == TypeCode.Empty || tc == TypeCode.Int64))
              result = new Literal((long)ul);
            else
              result = new Literal(ul);
            break;
        }
      }catch(OverflowException){
        this.HandleError(ctx, Error.IntOverflow);
        result = new Literal(0);
      }
      ctx.EndPos = this.scanner.endPos;
      result.SourceContext = ctx;
      result.TypeWasExplicitlySpecifiedInSource = tc != TypeCode.Empty;
      this.GetNextToken();
      return result;
    }
    private Literal ParseIntegerLiteral(){
      Debug.Assert(this.currentToken == Token.IntegerLiteral);
      string tokStr = this.scanner.GetTokenSource();
      SourceContext ctx = this.scanner.CurrentSourceContext;
      TypeCode tc = this.scanner.ScanNumberSuffix();
      ctx.EndPos = this.scanner.endPos;
      Literal result;
      try{
        switch(tc){
          case TypeCode.Single:
            result = new Literal(Single.Parse(tokStr, null));
            break;
          case TypeCode.Double:
            result = new Literal(Double.Parse(tokStr, null));
            break;
          case TypeCode.Decimal:
            result = new Literal(Decimal.Parse(tokStr, null));
            break;
          default:
            ulong ul = UInt64.Parse(tokStr, null);
            if (ul <= int.MaxValue && tc == TypeCode.Empty)
              result = new Literal((int)ul);
            else if (ul <= uint.MaxValue && (tc == TypeCode.Empty || tc == TypeCode.UInt32))
              result = new Literal((uint)ul);
            else if (ul <= long.MaxValue && (tc == TypeCode.Empty || tc == TypeCode.Int64))
              result = new Literal((long)ul);
            else
              result = new Literal(ul);
            break;
        }
      }catch(OverflowException){
        this.HandleError(ctx, Error.IntOverflow);
        result = new Literal(0);
      }catch(ExecutionEngineException){
        this.HandleError(ctx, Error.IntOverflow);
        result = new Literal(0);
      }
      result.SourceContext = ctx;
      result.TypeWasExplicitlySpecifiedInSource = tc != TypeCode.Empty;
      this.GetNextToken();
      return result;
    }
    private static char[] nonZeroDigits = {'1','2','3','4','5','6','7','8','9'};
    private Literal ParseRealLiteral(){
      Debug.Assert(this.currentToken == Token.RealLiteral);
      string tokStr = this.scanner.GetTokenSource();
      SourceContext ctx = this.scanner.CurrentSourceContext;
      TypeCode tc = this.scanner.ScanNumberSuffix();
      ctx.EndPos = this.scanner.endPos;
      Literal result;
      string typeName = null;
      try{
        switch(tc){
          case TypeCode.Single:
            typeName = "float";
            float fVal = Single.Parse(tokStr, NumberStyles.Any, CultureInfo.InvariantCulture);
            if (fVal == 0f && tokStr.IndexOfAny(nonZeroDigits) >= 0)
              this.HandleError(ctx, Error.FloatOverflow, typeName);
            result = new Literal(fVal);
            break;
          case TypeCode.Empty:
          case TypeCode.Double:
            typeName = "double";
            double dVal = Double.Parse(tokStr, NumberStyles.Any, CultureInfo.InvariantCulture);
            if (dVal == 0d && tokStr.IndexOfAny(nonZeroDigits) >= 0)
              this.HandleError(ctx, Error.FloatOverflow, typeName);
            result = new Literal(dVal);
            break;
          case TypeCode.Decimal:
            typeName = "decimal";
            decimal decVal = Decimal.Parse(tokStr, NumberStyles.Any, CultureInfo.InvariantCulture);
            result = new Literal(decVal);
            break;
          default:
            //TODO: give an error message
            goto case TypeCode.Empty;
        }
      }catch(OverflowException){
        this.HandleError(ctx, Error.FloatOverflow, typeName);
        result = new Literal(0);
      }
      result.SourceContext = ctx;
      result.TypeWasExplicitlySpecifiedInSource = tc != TypeCode.Empty;
      this.GetNextToken();
      return result;
    }
    private Expression ParseNew(TokenSet followers){
      SourceContext ctx = this.scanner.CurrentSourceContext;
      Debug.Assert(this.currentToken == Token.New);
      this.GetNextToken();
      TypeNode allocator = null;
      if (this.currentToken == Token.LeftBracket) {
        this.GetNextToken();
        if (this.currentToken == Token.RightBracket) {
          return this.ParseNewImplicitlyTypedArray(ctx, followers);
        }
        // parse [Delayed] annotation (or allocator)
        allocator = this.ParseBaseTypeExpression(null, followers|Token.RightBracket, false, false);
        if (allocator == null){this.SkipTo(followers, Error.None); return null;}
        this.Skip(Token.RightBracket);
      }
      if (this.currentToken == Token.LeftBrace)
        return this.ParseNewAnonymousTypeInstance(ctx, followers);
      Expression owner = null;
      // Allow owner argument for each constructor: "new <ow> ..."
      if (this.currentToken == Token.LessThan) {
        this.GetNextToken();
        owner = this.ParsePrimaryExpression(followers | Token.GreaterThan);
        if (this.currentToken == Token.GreaterThan)
          this.GetNextToken();
      }
      // Make it explicit that the base type stops at "!", which is handled by
      // the code below.
      TypeNode t = this.ParseBaseTypeExpression(null, followers|Parser.InfixOperators|Token.LeftBracket|Token.LeftParenthesis|Token.RightParenthesis|Token.LogicalNot, false, false);
      if (t == null){this.SkipTo(followers, Error.None); return null;}
      if (this.currentToken == Token.Conditional) {
        TypeNode type = t;
        t = new NullableTypeExpression(type);
        t.SourceContext = type.SourceContext;
        t.SourceContext.EndPos = this.scanner.endPos;
        this.GetNextToken();
      }else if (this.currentToken == Token.LogicalNot){
        TypeNode type = t;
        t = new NonNullableTypeExpression(type);
        t.SourceContext = type.SourceContext;
        t.SourceContext.EndPos = this.scanner.endPos;
        this.GetNextToken();
      }else if (this.currentToken == Token.Multiply){
        this.GetNextToken();
        t = new PointerTypeExpression(t, t.SourceContext);
        t.SourceContext.EndPos = this.scanner.endPos;
        if (!this.inUnsafeCode)
          this.HandleError(t.SourceContext, Error.UnsafeNeeded);
      }
      ctx.EndPos = t.SourceContext.EndPos;
      // special hack [Delayed] in custom allocator position is used to mark the array as
      // a delayed construction. This annotation is used in the Definite Assignment analysis.
      //
      TypeExpression allocatorExp = allocator as TypeExpression;
      if (allocatorExp != null) {
        Identifier allocId = allocatorExp.Expression as Identifier;
        if (allocId != null && allocId.Name == "Delayed") {
          t = new RequiredModifierTypeExpression(t, TypeExpressionFor("Microsoft","Contracts","DelayedAttribute"));
          allocator = null; // not really a custom allocation
        }
      }
      int rank = this.ParseRankSpecifier(false, followers|Token.LeftBrace|Token.LeftBracket|Token.LeftParenthesis|Token.RightParenthesis);
      SourceContext lbCtx = ctx;
      while (rank > 0 && this.currentToken == Token.LeftBracket){
        lbCtx = this.scanner.CurrentSourceContext;
        t = new ArrayTypeExpression(t, rank);
        rank = this.ParseRankSpecifier(false, followers|Token.LeftBrace|Token.LeftBracket);
      }
      if (rank > 0){
        //new T[] {...} or new T[,] {{..} {...}...}, etc where T can also be an array type
        ConstructArray consArr = new ConstructArray();
        consArr.SourceContext = ctx;
        consArr.ElementType = consArr.ElementTypeExpression = t;
        consArr.Rank = rank;
        if (this.currentToken == Token.LeftBrace)
          consArr.Initializers = this.ParseArrayInitializer(rank, t, followers);
        else{
          if (Parser.UnaryStart[this.currentToken])
            this.HandleError(Error.ExpectedLeftBrace);
          else
            this.HandleError(Error.MissingArraySize);
          while (Parser.UnaryStart[this.currentToken]){
            this.ParseExpression(followers|Token.Comma|Token.RightBrace);
            if (this.currentToken != Token.Comma) break;
            this.GetNextToken();
          }
          if (this.currentToken == Token.RightBrace) this.GetNextToken();
          this.SkipTo(followers);
        }
        if (owner != null) {
          consArr.Owner = owner;
        }
        return consArr;
      }
      if (rank < 0){
        //new T[x] or new T[x,y] etc. possibly followed by an initializer or element type rank specifier
        ConstructArray consArr = new ConstructArray();
        consArr.SourceContext = ctx;
        consArr.Operands = this.ParseIndexList(followers|Token.LeftBrace|Token.LeftBracket, lbCtx, out consArr.SourceContext.EndPos);
        rank = consArr.Operands.Count;
        if (this.currentToken == Token.LeftBrace)
          consArr.Initializers = this.ParseArrayInitializer(rank, t, followers);
        else{
          int elementRank = this.ParseRankSpecifier(true, followers);
        tryAgain:
          if (elementRank > 0) t = this.ParseArrayType(elementRank, t, followers);
          if (this.currentToken == Token.LeftBrace)
            consArr.Initializers = this.ParseArrayInitializer(rank, t, followers);
          else{
            if (this.currentToken == Token.LeftBracket){ //new T[x][y] or something like that
              lbCtx = this.scanner.CurrentSourceContext;
              this.GetNextToken();
              SourceContext sctx = this.scanner.CurrentSourceContext;
              this.ParseIndexList(followers, lbCtx, out sctx.EndPos);
              this.HandleError(sctx, Error.InvalidArray);
              elementRank = 1;
              goto tryAgain;
            }else
              this.SkipTo(followers);
          }
        }
        consArr.ElementType = consArr.ElementTypeExpression = t;
        consArr.Rank = rank;
        if (owner != null) {
          consArr.Owner = owner;
        }
        return consArr;
      }
      ExpressionList arguments = null;
      SourceContext lpCtx = this.scanner.CurrentSourceContext;
      bool sawLeftParenthesis = false;
      if (this.currentToken == Token.LeftParenthesis){
        if (rank == 0 && t is NonNullableTypeExpression) {
          this.SkipTo(followers, Error.BadNewExpr);
          return null;
        }
        sawLeftParenthesis = true;
        this.GetNextToken();
        arguments = this.ParseArgumentList(followers, lpCtx, out ctx.EndPos);
      }else if (this.currentToken == Token.LeftBrace){
        Expression quant = this.ParseComprehension(followers);
        arguments = new ExpressionList(quant);
      }else{
        this.SkipTo(followers, Error.BadNewExpr);
        Construct c = new Construct();
        if (t is TypeExpression)
          c.Constructor = new MemberBinding(null, t, t.SourceContext);
        c.SourceContext = ctx;
        return c;
      }
      if (sawLeftParenthesis && this.currentToken == Token.LeftBrace){
      }
      Construct cons = new Construct(new MemberBinding(null, t), arguments);
      cons.SourceContext = ctx;
      if (owner != null)
        cons.Owner = owner;
      return cons;
    }
    private Expression ParseNewAnonymousTypeInstance(SourceContext ctx, TokenSet followers) {
      SourceContext sctx = this.scanner.CurrentSourceContext;
      Debug.Assert(this.currentToken == Token.LeftBrace);
      this.GetNextToken();
      ConstructTuple result = new ConstructTuple();
      FieldList fields = result.Fields = new FieldList();
      TokenSet followersOrCommaOrRightBrace = followers|Token.Comma|Token.RightBrace;
      while (Parser.UnaryStart[this.currentToken]) {
        Field f = new Field();
        fields.Add(f);
        f.Flags = FieldFlags.Public;
        f.SourceContext = this.scanner.CurrentSourceContext;
        Expression e = this.ParseUnaryExpression(followersOrCommaOrRightBrace|Parser.InfixOperators);
        if (this.currentToken == Token.Assign) {
          Identifier id = e as Identifier;
          if (id == null) {
            if (e != null)
              this.HandleError(e.SourceContext, Error.ExpectedIdentifier);
          } else {
            f.Name = id;
            this.GetNextToken();
            e = this.ParseExpression(followersOrCommaOrRightBrace);
          }
        } else {
          Identifier id = e as Identifier;
          if (id != null)
            f.Name = id;
          else {
            QualifiedIdentifier qualId = e as QualifiedIdentifier;
            if (qualId != null)
              f.Name = qualId.Identifier;
            else {
              //TODO: error message
            }
          }
        }
        f.Initializer = e;
        if (e != null) f.SourceContext.EndPos = e.SourceContext.EndPos;
        if (this.currentToken != Token.Comma) break;
        this.GetNextToken();
      }
      int endCol = this.scanner.endPos;
      this.ParseBracket(sctx, Token.RightBrace, followers, Error.ExpectedRightBracket);
      result.SourceContext = sctx;
      result.SourceContext.EndPos = endCol;
      return result;
    }
    private Expression ParseNewImplicitlyTypedArray(SourceContext ctx, TokenSet followers) {
      Debug.Assert(this.currentToken == Token.RightBracket);
      ctx.EndPos = this.scanner.endPos;
      this.Skip(Token.RightBracket);
      ConstructArray consArr = new ConstructArray();
      consArr.SourceContext = ctx;
      consArr.Rank = 1;
      if (this.currentToken == Token.LeftBrace)
        consArr.Initializers = this.ParseArrayInitializer(1, null, followers);
      else
        this.Skip(Token.LeftBrace);
      this.SkipTo(followers);
      return consArr;
    }    
    private Comprehension ParseComprehension(TokenSet followers){
      SourceContext sctx = this.scanner.CurrentSourceContext;
      Debug.Assert(this.currentToken == Token.LeftBrace);
      this.GetNextToken();
      return this.ParseComprehension(followers, sctx);
    }      
    private Comprehension ParseComprehension(TokenSet followers, SourceContext sctx){
      bool isDisplay = true;
      ExpressionList bindingAndFilterList = new ExpressionList();
      ExpressionList elementList = new ExpressionList();
      Expression e = null;
      
      if (this.currentToken != Token.RightBrace){
        e = this.ParseIteratorOrExpression(followers|Token.RightBrace|Token.Semicolon|Token.Comma);
        if (e == null){
          this.HandleError(sctx, Error.SyntaxError,"iterator or expression");
          return null;
        }else{
          bindingAndFilterList.Add(e);
        }
        isDisplay = !(e is ComprehensionBinding);
        while (this.currentToken == Token.Comma){
          this.GetNextToken(); 
          e = this.ParseIteratorOrExpression(followers|Token.RightBrace|Token.Semicolon|Token.Comma);
          if (e == null){
            this.HandleError(sctx, Error.SyntaxError,"iterator or expression");
            return null;
          } else {
            bindingAndFilterList.Add(e);
          }
          if (isDisplay)
            isDisplay = !(e is ComprehensionBinding);
        }
        if (!isDisplay) {
          if (this.currentToken != Token.Semicolon) {
            this.SkipTo(followers, Error.InvalidExprTerm, this.scanner.GetTokenSource());
          } else {
            this.GetNextToken();
            Expression temp = this.ParseExpression(followers | Token.RightBrace | Token.Semicolon);
            if (temp != null)
              elementList.Add(temp);
            if (this.currentToken == Token.Semicolon) {
              this.GetNextToken();
              this.Skip(Token.Default);
              temp = this.ParseExpression(followers | Token.RightBrace);
              if (temp != null)
                elementList.Add(temp);
            }
          }
        }
      }
      sctx.EndPos = this.scanner.endPos;
      this.Skip(Token.RightBrace);
      if (isDisplay){ // display, what we thought were bindings/filters are actually all elements
        elementList = bindingAndFilterList;
        bindingAndFilterList = null;
      }
        
      Comprehension comp = new Comprehension();
      if (bindingAndFilterList != null && bindingAndFilterList.Count > 0)
        comp.BindingsAndFilters = bindingAndFilterList;
      comp.Elements = elementList;

      this.sawReturnOrYield = true;

      comp.SourceContext = sctx;
      comp.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos;
      this.SkipTo(followers);
      return comp;
    }

    private Expression ParseIteratorOrExpression( TokenSet followers ) {      
      SourceContext sctx = this.scanner.CurrentSourceContext;
      ScannerState ss = this.scanner.state;
      // shortcut for ( <expression>)  
      if( this.currentToken == Token.LeftParenthesis ) {
        // then it can't be a binding, it must be a filter.
        Expression e = ParseExpression(followers );
        e.SourceContext = sctx;
        e.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos; 
        return e;
      }      
      // try fully formed syntax:  <type> <name> in <expression>
      ComprehensionBinding q = new ComprehensionBinding();
      q.TargetVariableType = q.TargetVariableTypeExpression = this.ParseTypeExpression(null, 
        followers | Parser.IdentifierOrNonReservedKeyword |Token.In|Token.RightBrace|Token.Semicolon|Token.Comma, true );
      if (q.TargetVariableType != null && Parser.IdentifierOrNonReservedKeyword[this.currentToken] ) {
        q.TargetVariable = this.scanner.GetIdentifier();
        q.SourceContext = this.scanner.CurrentSourceContext;
        this.SkipIdentifierOrNonReservedKeyword();
        // after the identifier, look for 'in' keyword
        if( this.currentToken == Token.In ) {
          this.Skip(Token.In);
          q.SourceEnumerable = this.ParseExpression(followers);
          q.SourceContext = sctx;
          q.SourceContext.EndPos = this.scanner.CurrentSourceContext.StartPos; 
          return q;
        } 
      

      }
      // back up and try as untyped iterator:  <name> in <expression>
      q.TargetVariableType = q.TargetVariableTypeExpression = null;
      this.scanner.endPos = sctx.StartPos;
      this.scanner.state = ss;
      this.currentToken = Token.None;
      this.GetNextToken();
      if (Parser.IdentifierOrNonReservedKeyword[this.currentToken] ) {
        q.TargetVariable = this.scanner.GetIdentifier();
        q.SourceContext = this.scanner.CurrentSourceContext;
        this.SkipIdentifierOrNonReservedKeyword();
        // after the identifier, look for 'in' keyword
        if( this.currentToken == Token.In ) {
          this.Skip(Token.In);
          q.SourceEnumerable = this.ParseExpression(followers);
          return q;
        } else if (this.currentToken == Token.As) {
          this.Skip(Token.As);
          q.AsTargetVariableType = q.AsTargetVariableTypeExpression = this.ParseTypeExpression(null,
            followers|Token.In|Token.RightBrace|Token.Semicolon|Token.Comma, false); 
          this.Skip(Token.In);
          q.SourceEnumerable = this.ParseExpression(followers);
          return q;
        }
      }
      // back up and try as unnamed expression:  <expression>
      this.scanner.endPos = sctx.StartPos;
      this.scanner.state = ss;
      this.currentToken = Token.None;
      this.GetNextToken();
      return  this.ParseExpression(followers);
    }
    private Expression ParseArrayInitializer(TypeNode type, TokenSet followers){
      NonNullableTypeExpression nnte = type as NonNullableTypeExpression;
      if (nnte != null) type = nnte.ElementType;
      NullableTypeExpression nute = type as NullableTypeExpression;
      if (nute != null) type = nute.ElementType;
      ArrayTypeExpression aType = type as ArrayTypeExpression;
      if (aType != null){
        ConstructArray consArr = new ConstructArray();
        consArr.SourceContext = this.scanner.CurrentSourceContext;
        consArr.ElementType = consArr.ElementTypeExpression = aType.ElementType;
        consArr.Rank = aType.Rank;
        consArr.Initializers = this.ParseArrayInitializer(aType.Rank, aType.ElementType, followers);
        consArr.SourceContext.EndPos = this.scanner.endPos;
        return consArr;
      }
      object savedArrayInitializerOpeningContext = this.arrayInitializerOpeningContext;
      this.arrayInitializerOpeningContext = this.scanner.CurrentSourceContext;
      Expression e = this.ParseBlockExpression(ScannerState.Code, this.scanner.CurrentSourceContext, followers);
      this.arrayInitializerOpeningContext = savedArrayInitializerOpeningContext;
      return e;
    }
    private ExpressionList ParseArrayInitializer(int rank, TypeNode elementType, TokenSet followers){
      return this.ParseArrayInitializer(rank, elementType, followers, false);
    }
    private ExpressionList ParseArrayInitializer(int rank, TypeNode elementType, TokenSet followers, bool doNotSkipClosingBrace){
      Debug.Assert(this.currentToken == Token.LeftBrace);
      this.GetNextToken();
      ExpressionList initialValues = new ExpressionList();
      if (this.currentToken == Token.RightBrace){
        this.GetNextToken();
        return initialValues;
      }
      for(;;){
        if (rank > 1){
          ConstructArray elemArr = new ConstructArray();
          elemArr.ElementType = elemArr.ElementTypeExpression = elementType;
          elemArr.Rank = rank-1;
          initialValues.Add(elemArr);
          if (this.currentToken == Token.LeftBrace)
            elemArr.Initializers = this.ParseArrayInitializer(rank-1, elementType, followers|Token.Comma|Token.LeftBrace);
          else
            this.SkipTo(followers|Token.Comma|Token.LeftBrace, Error.ExpectedLeftBrace);
        }else{
          if (this.currentToken == Token.LeftBrace){
            this.HandleError(Error.ArrayInitInBadPlace);
            ConstructArray elemArr = new ConstructArray();
            elemArr.ElementType = elemArr.ElementTypeExpression = elementType;
            elemArr.Rank = 1;
            initialValues.Add(elemArr);
            elemArr.Initializers = this.ParseArrayInitializer(1, elementType, followers|Token.Comma|Token.LeftBrace);
          }else
            initialValues.Add(this.ParseExpression(followers|Token.Comma|Token.RightBrace));
        }
        if (this.currentToken != Token.Comma) break;
        this.GetNextToken();
        if (this.currentToken == Token.RightBrace) break;
      }
      if (doNotSkipClosingBrace) return null;
      this.Skip(Token.RightBrace);
      this.SkipTo(followers);
      return initialValues;
    }
    private Expression ParseIndexerCallOrSelector(Expression expression, TokenSet followers){
      TokenSet followersOrContinuers = followers|Token.LeftBracket|Token.LeftParenthesis|Token.Dot;
      for(;;){
        switch (this.currentToken){
          case Token.LeftBracket:
            SourceContext lbCtx = this.scanner.CurrentSourceContext;
            this.GetNextToken();
            if (this.insideModifiesClause && this.currentToken == Token.Multiply){
              // Handle code such as
              //
              //     modifies myArray[*];
              //
              // which means that the method may modify all elements of myArray.
              int savedStartPos = this.scanner.startPos;
              int savedEndPos = this.scanner.endPos;
              this.GetNextToken();
              if (this.currentToken == Token.RightBracket){
                SourceContext sctxt = this.scanner.CurrentSourceContext;
                sctxt.StartPos = lbCtx.StartPos;
                this.GetNextToken();
                return new ModifiesArrayClause(expression, sctxt);
              }
              this.scanner.startPos = savedStartPos;
              this.scanner.endPos = savedEndPos;
            }	    
            int endCol;
            ExpressionList indices = this.ParseIndexList(followersOrContinuers, lbCtx, out endCol);
            Indexer indexer = new Indexer(expression, indices);
            indexer.SourceContext = expression.SourceContext;
            indexer.SourceContext.EndPos = endCol;
            indexer.ArgumentListIsIncomplete = this.scanner.GetChar(endCol-1) != ']';
            expression = indexer;
            break;
          case Token.LessThan:
            SourceContext ltCtx = this.scanner.CurrentSourceContext;
            ScannerState ss = this.scanner.state;
            int arity;
            TypeNodeList typeArguments = this.ParseTypeArguments(true, false, followers|Token.LeftParenthesis, out endCol, out arity);
            if (typeArguments == null || (typeArguments.Count > 1 && Parser.TypeArgumentListNonFollower[this.currentToken])) {
              this.scanner.endPos = ltCtx.StartPos;
              this.scanner.state = ss;
              this.currentToken = Token.None;
              this.GetNextToken();
              return expression;
            }
            TemplateInstance instance = new TemplateInstance(expression, typeArguments);
            instance.TypeArgumentExpressions = typeArguments == null ? null : typeArguments.Clone();
            instance.SourceContext = expression.SourceContext;
            instance.SourceContext.EndPos = endCol;
            expression = instance;
            break;
          case Token.LeftParenthesis:
            SourceContext lpCtx = this.scanner.CurrentSourceContext;
            this.GetNextToken();
            ExpressionList arguments = this.ParseArgumentList(followersOrContinuers, lpCtx, out endCol);
            if (expression == null) return null;
            if (expression is Identifier && arguments.Count == 1 && ((Identifier)expression).Name == "old" && InEnsuresContext){
              OldExpression old = new OldExpression(arguments[0]);
              typeArguments = null;
              old.SourceContext = expression.SourceContext;
              old.SourceContext.EndPos = endCol;
              expression = old;
              break;
            }
            if (expression is TemplateInstance)
              ((TemplateInstance)expression).IsMethodTemplate = true;
            //HS D: a lambda hole... : make a linear comb of args for hole
            if (expression is Hole) {
                SourceContext sctx = expression.SourceContext;
                Expression res = expression;
                if (arguments.Count > 0)
                    {
                        res = new BinaryExpression(expression, arguments[0], NodeType.Mul, sctx);
                        for (int i = 1; i < arguments.Count; i++)
                            res = new BinaryExpression(res, new BinaryExpression(new Hole(sctx), arguments[i], NodeType.Mul, sctx), NodeType.Add, sctx);
                        res = new LambdaHole(res, new Hole(sctx), NodeType.Add, sctx);
                    }
                expression = res;
               break;
            }
            MethodCall mcall = new MethodCall(expression, arguments);
            typeArguments = null;
            mcall.GiveErrorIfSpecialNameMethod = true;
            mcall.SourceContext = expression.SourceContext;
            mcall.SourceContext.EndPos = endCol;
            mcall.ArgumentListIsIncomplete = this.scanner.GetChar(endCol-1) != ')';
            expression = mcall;
            break;
          case Token.LeftBrace:
            if (this.compatibilityOn || this.scanner.TokenIsFirstAfterLineBreak) goto default;
            Expression quant = this.ParseComprehension(followers);
            if (quant == null) { break; }
            Block argBlock = new Block(new StatementList(new ExpressionStatement(quant)),quant.SourceContext,
              this.insideCheckedBlock, this.insideUncheckedBlock, this.inUnsafeCode);
            argBlock.IsUnsafe = this.inUnsafeCode;
            argBlock.SourceContext = quant.SourceContext;
            ExpressionList arguments2 = new ExpressionList(new AnonymousNestedFunction(new ParameterList(0), argBlock, quant.SourceContext));
            MethodCall mcall2 = new MethodCall(expression, arguments2);
            typeArguments = null;
            mcall2.GiveErrorIfSpecialNameMethod = true;
            mcall2.SourceContext = expression.SourceContext;
            mcall2.SourceContext.EndPos = this.scanner.endPos;
            expression = mcall2;
            break;
          case Token.Dot:            
            expression = this.ParseQualifiedIdentifier(expression, followersOrContinuers);
            break;
          case Token.RealLiteral:
            string tokStr = this.scanner.GetTokenSource();
            if (this.insideModifiesClause && tokStr == ".0") {
              // this case is here only for parsing ".0" while parsing a modifies clause
              // e.g., "modifies this.0;"
              this.GetNextToken(); // eat the ".0"
              return new ModifiesNothingClause(expression, this.scanner.CurrentSourceContext);
            } else {
              return expression;
            }
          case Token.Arrow:
            if (!this.allowUnsafeCode){
              this.HandleError(Error.IllegalUnsafe);
              this.allowUnsafeCode = true;
            }
            this.currentToken = Token.Dot;
            AddressDereference ad = new AddressDereference();
            ad.Address = expression;
            ad.ExplicitOperator = AddressDereference.ExplicitOp.Arrow;
            ad.SourceContext = expression.SourceContext;
            expression = this.ParseQualifiedIdentifier(ad, followersOrContinuers);
            break;
          default:
            return expression;
        }
      }
    }
    private ExpressionList ParseIndexList(TokenSet followers, SourceContext ctx, out int endCol){
      if (this.sink != null && ctx.StartPos < ctx.EndPos) this.sink.StartParameters(ctx);
      TokenSet followersOrCommaOrRightBracket = followers|Token.Comma|Token.RightBracket;
      ExpressionList result = new ExpressionList();
      if (this.currentToken != Token.RightBracket){
        SourceContext sctx = this.scanner.CurrentSourceContext;
        Expression index = this.ParseExpression(followersOrCommaOrRightBracket);
        if (this.sink != null && index != null){
          sctx.EndPos = this.scanner.endPos;
          this.sink.NextParameter(sctx);
        }
        result.Add(index);
        while (this.currentToken == Token.Comma){
          sctx = this.scanner.CurrentSourceContext;
          sctx.StartPos++;
          this.GetNextToken();
          index = this.ParseExpression(followersOrCommaOrRightBracket);
          if (this.sink != null){
            sctx.EndPos = this.scanner.endPos;
            this.sink.NextParameter(sctx);
          }
          result.Add(index);
        }
      }
      endCol = this.scanner.endPos;
      if (this.sink != null && this.currentToken != Token.EndOfFile) this.sink.EndParameters(this.scanner.CurrentSourceContext);
      this.Skip(Token.RightBracket);
      this.SkipTo(followers);
      return result;
    }
    private ExpressionList ParseExpressionList(TokenSet followers, ref SourceContext sctx){
      this.Skip(Token.LeftParenthesis);
      ExpressionList result = new ExpressionList();
      if (this.currentToken != Token.RightParenthesis){
        Expression argument = this.ParseExpression(followers);
        result.Add(argument);
        while (this.currentToken == Token.Comma){
          this.GetNextToken();
          argument = this.ParseExpression(followers);
          result.Add(argument);
        }
      }
      this.Skip(Token.RightParenthesis);
      sctx.EndPos = this.scanner.endPos;
      this.SkipTo(followers);
      return result;
    }
    private ExpressionList ParseArgumentList(TokenSet followers, SourceContext ctx, out int endCol){
      if (this.sink != null && ctx.StartPos < ctx.EndPos) this.sink.StartParameters(ctx);
      TokenSet followersOrCommaOrRightParenthesis = followers|Token.Comma|Token.RightParenthesis;
      ExpressionList result = new ExpressionList();
      if (this.currentToken != Token.RightParenthesis){
        SourceContext sctx = this.scanner.CurrentSourceContext;
        Expression argument = this.ParseArgument(followersOrCommaOrRightParenthesis);
        if (this.sink != null && argument != null){
          sctx.EndPos = this.scanner.endPos;
          this.sink.NextParameter(sctx);
        }
        result.Add(argument);
        while (this.currentToken == Token.Comma){
          sctx = this.scanner.CurrentSourceContext;
          sctx.StartPos++;
          this.GetNextToken();
          argument = this.ParseArgument(followersOrCommaOrRightParenthesis);
          if (this.sink != null){
            sctx.EndPos = this.scanner.endPos;
            this.sink.NextParameter(sctx);
          }
          result.Add(argument);
        }
      }
      endCol = this.scanner.endPos;
      if (this.sink != null && this.currentToken != Token.EndOfFile) this.sink.EndParameters(this.scanner.CurrentSourceContext);
      this.Skip(Token.RightParenthesis);
      this.SkipTo(followers);
      return result;
    }
    private Expression ParseArgument(TokenSet followers){
      switch(this.currentToken){
        case Token.Ref:
          SourceContext sctx = this.scanner.CurrentSourceContext;
          this.GetNextToken();
          Expression expr = this.ParseExpression(followers);
          if (expr == null) return null;
          sctx.EndPos = expr.SourceContext.EndPos;
          return new UnaryExpression(expr, NodeType.RefAddress, sctx);
        case Token.Out:
          sctx = this.scanner.CurrentSourceContext;
          this.GetNextToken();
          expr = this.ParseExpression(followers);
          if (expr == null) return null;
          sctx.EndPos = expr.SourceContext.EndPos;
          return new UnaryExpression(expr, NodeType.OutAddress, sctx);
        case Token.ArgList:
          sctx = this.scanner.CurrentSourceContext;
          this.GetNextToken();
          if (this.currentToken == Token.LeftParenthesis) {
            ExpressionList el = this.ParseExpressionList(followers, ref sctx);
            return new ArglistArgumentExpression(el, sctx);
          }
          return new ArglistExpression(sctx);
        default:
          return this.ParseExpression(followers);
      }
    }
    private XmlElement GetXmlElement(LiteralElement lit){ 
      try {
        XmlElement result = null;
        result = this.module.Documentation.CreateElement(lit.Name.Name);
        IdentifierList attrNames = lit.AttributeNames;
        ExpressionList attrValues = lit.AttributeValues;
        for (int i = 0, n = attrNames == null ? 0 : attrNames.Count; i < n; i++){
          Identifier attrName = attrNames[i]; if (attrName == null) continue;
          Expression attrValue = attrValues[i]; if (attrValue == null) continue;
          XmlAttribute attr = result.SetAttributeNode(attrName.Name, null);
          Literal attrLit = attrValue as Literal;
          if (attrLit != null && attrLit.Value is string)
            attr.Value = (string)attrLit.Value;
          else
            attr.Value = attrValue.SourceContext.SourceText;
        }
        ExpressionList contents = lit.Contents;
        Int32List contentsType = lit.ContentsType;
        for (int i = 0, n = contents == null ? 0 : contents.Count; i < n; i++){
          Expression content = contents[i]; if (content == null) continue;
          switch ((Token)contentsType[i]){
            case Token.StartOfTag:
              XmlElement childElement = this.GetXmlElement((LiteralElement)content);
              if (childElement == null) continue;
              result.AppendChild(childElement);
              break;
            case Token.LiteralContentString:
              result.AppendChild(result.OwnerDocument.CreateTextNode((string)((Literal)content).Value));
              break;
            case Token.CharacterData:
              result.AppendChild(result.OwnerDocument.CreateCDataSection((string)((Literal)content).Value));
              break;
            case Token.LiteralComment:
              result.AppendChild(result.OwnerDocument.CreateComment((string)((Literal)content).Value));
              break;
            case Token.ProcessingInstructions:
              string piTagAndBody = (string)((Literal)content).Value;
              int len = piTagAndBody == null ? 0 : piTagAndBody.Length;
              int firstBlank = piTagAndBody.IndexOf(' ');
              if (firstBlank > 2 && firstBlank < len-3)
                result.AppendChild(result.OwnerDocument.CreateProcessingInstruction(
                  piTagAndBody.Substring(2, firstBlank-2), piTagAndBody.Substring(firstBlank+1,len-3-firstBlank)));
              break;
          }
        }
        return result;
      } catch (Exception e) {
        this.HandleError(lit.SourceContext, Error.InternalCompilerError, e.Message);
        return null;
      }
    }
    private void ParseDocComment(TokenSet followers){
      Token tok = this.currentToken;
      Debug.Assert(tok == Token.SingleLineDocCommentStart || tok == Token.MultiLineDocCommentStart);
      this.scanner.state = ScannerState.XML;
      this.scanner.RestartStateHasChanged = true;
      LiteralElement lit = null;
      lit = this.ParseObjectLiteral(followers|Token.DocCommentEnd);
      if (this.scanner.docCommentStart == Token.MultiLineDocCommentStart)
        this.Skip(Token.DocCommentEnd);
      if (lit != null){
        lit.Name = Identifier.For("member");
        this.lastDocCommentBackingField = this.GetXmlElement(lit);
      }
      this.scanner.docCommentStart = Token.None;
      this.scanner.RestartStateHasChanged = true;
      this.SkipTo(followers);
    }
    private LiteralElement ParseObjectLiteral(TokenSet followers){
      Token tok = this.currentToken;
      Debug.Assert(tok == Token.ObjectLiteralStart || tok == Token.MultiLineDocCommentStart || tok == Token.SingleLineDocCommentStart);
      Debug.Assert(this.scanner.state == ScannerState.XML);
      this.GetNextToken();
      if (this.currentToken != Token.StartOfTag){
        this.SkipTo(followers|Token.StartOfTag);
        if (this.currentToken != Token.StartOfTag) return null;
      }
      bool junk;
      LiteralElement result = this.ParseLiteralElement(Identifier.Empty, followers, out junk);
      if (result == null){
        while (this.currentToken == Token.SingleLineDocCommentStart) this.GetNextToken();
        return result; // error handling.
      }
      if (tok == Token.MultiLineDocCommentStart || tok == Token.SingleLineDocCommentStart ||
        this.currentToken == Token.LessThan){
        LiteralElement element = result;
        result = new LiteralElement();
        result.SourceContext = element.SourceContext;
        result.Contents.Add(element);
        result.ContentsType.Add((int)Token.StartOfTag);
        for(;;){
          switch(this.currentToken){
            case Token.Multiply:
              this.GetNextToken();
              continue;
            case Token.ObjectLiteralStart:
            case Token.SingleLineDocCommentStart:
              this.GetNextToken();
              goto case Token.LessThan;
            case Token.LessThan:
              this.currentToken = Token.StartOfTag;
              LiteralElement xelem = this.ParseLiteralElement(Identifier.Empty, followers, out junk);
              result.Contents.Add(xelem);
              result.ContentsType.Add((int)Token.StartOfTag);
              break;
            default: 
              goto done;
          }
        }
      }
      done:
        this.SkipTo(followers);
      return result;
    }
    private LiteralElement ParseLiteralElement(Identifier parentTagName, TokenSet followers, out bool parentMustExpectClosingTag){
      Debug.Assert(this.currentToken == Token.StartOfTag);
      parentMustExpectClosingTag = true;
      this.scanner.state = ScannerState.Tag;
      LiteralElement result = new LiteralElement();
      result.SourceContext = this.scanner.CurrentSourceContext;
      this.GetNextToken();
      bool expectClosingTag;
      SourceContext sctx = this.scanner.CurrentSourceContext;
      ScannerState ss = this.scanner.state;
      this.ParseLiteralElementOpeningTag(result, out expectClosingTag, ref sctx);
      if (expectClosingTag){
        Token tokenFollowingOpeningTag = this.currentToken;
        for(;;){
          switch(this.currentToken){
            case Token.LiteralContentString:
            case Token.LiteralComment:
            case Token.CharacterData:
              result.Contents.Add(this.scanner.GetStringLiteral());
              result.ContentsType.Add((int)this.currentToken);
              if (this.scanner.state != ScannerState.Code)
                this.scanner.state = ScannerState.XML;
              this.GetNextToken();
              break;
            case Token.ProcessingInstructions:
              result.Contents.Add(new Literal(this.scanner.CurrentSourceContext.SourceText));
              result.ContentsType.Add((int)this.currentToken);
              this.scanner.state = ScannerState.XML;
              this.GetNextToken();
              break;
            case Token.StartOfTag:
              bool nestedElementDidNotEndWithMyTag = true;
              LiteralElement xelem = this.ParseLiteralElement(result.Name, followers, out nestedElementDidNotEndWithMyTag);
              xelem.ParentLiteral = result;
              result.Contents.Add(xelem);
              result.ContentsType.Add((int)Token.StartOfTag);
              if (!nestedElementDidNotEndWithMyTag){
                result.SourceContext.EndPos = xelem.SourceContext.EndPos;
                if (parentTagName == Identifier.Empty)
                  this.scanner.state = ScannerState.Code;
                this.GetNextToken();
                return result;
              }else if (this.currentToken == Token.EndOfFile && xelem.Name == Identifier.Empty)
                return result;
              break;
            default:
              goto checkForClosingTag;
          }
        }
      checkForClosingTag:
        if (this.currentToken != Token.StartOfClosingTag){
          this.scanner.state = ScannerState.Code;
          Identifier tagId = result.Name;
          if (tokenFollowingOpeningTag == Token.LiteralContentString && parentTagName == Identifier.Empty
            && sctx.SourceText.Trim().Length != 0){
            this.scanner.endPos = sctx.StartPos;
            this.scanner.state = ss;
            this.currentToken = Token.None;
            this.GetNextToken();
            this.scanner.state = ScannerState.Code;
            Debug.Assert(this.currentToken != Token.ObjectLiteralStart);
            result = null;
          }
          if (tagId != null)
            this.HandleError(Error.SyntaxError, "</"+tagId.ToString()+">");
          else
            this.HandleError(Error.SyntaxError, "<");
          this.SkipTo(followers, Error.None);
          return result;
        }
        this.scanner.state = ScannerState.Tag;
        this.Skip(Token.StartOfClosingTag);
        Identifier closingId = this.ParsePrefixedIdentifier();
        if (parentTagName == Identifier.Empty)
          this.scanner.state = ScannerState.Code;
        else
          this.scanner.state = ScannerState.XML;
        Identifier openingId = result.Name;
        bool matchesStartId = closingId.UniqueIdKey == openingId.UniqueIdKey && 
          !(closingId.Prefix != null && (openingId.Prefix == null || closingId.Prefix.UniqueIdKey != openingId.Prefix.UniqueIdKey));
        if (matchesStartId)
          this.Skip(Token.EndOfTag);
        else{
          bool matchesParentId = closingId.UniqueIdKey == parentTagName.UniqueIdKey && 
            !(closingId.Prefix != null && (parentTagName.Prefix == null || closingId.Prefix.UniqueIdKey != parentTagName.Prefix.UniqueIdKey));
          if (matchesParentId){
            parentMustExpectClosingTag = false;
            this.HandleError(closingId.SourceContext, Error.ClosingTagMismatch, result.Name.ToString());
          }else{
            this.Skip(Token.EndOfTag);
            this.HandleError(closingId.SourceContext, Error.ClosingTagMismatch, result.Name.ToString());
          }
        }
      }else{
        if (parentTagName == Identifier.Empty)
          this.scanner.state = ScannerState.Code;
        if (this.currentToken == Token.EndOfSimpleTag)
          this.GetNextToken();
      }
      return result;
    }
 
    private void ParseLiteralElementOpeningTag(LiteralElement element, out bool expectClosingTag, ref SourceContext context){
      SourceContext ctx = this.scanner.CurrentSourceContext;
      Identifier tag = element.Name = this.ParsePrefixedIdentifier();
      if (tag.Prefix != null) {
        ctx = tag.Prefix.SourceContext;
        ctx.EndPos = this.scanner.endPos;
      }
      element.Type = new TypeExpression(element.Name);
      if (this.sink != null)
        this.sink.QualifyName(element.SourceContext, tag);
      while (Parser.IdentifierOrNonReservedKeyword[this.currentToken]){
        element.AttributeNames.Add(this.ParsePrefixedIdentifier());
        this.Skip(Token.Assign);
        switch(this.currentToken){
          case Token.IntegerLiteral:
            element.AttributeValues.Add(this.ParseIntegerLiteral());
            break;
          default:
            element.AttributeValues.Add(this.scanner.GetStringLiteral());
            this.Skip(Token.StringLiteral);
            break;
        }
      }
      this.scanner.state = ScannerState.XML;
      element.SourceContext.EndPos = this.scanner.endPos;
      if (this.currentToken == Token.EndOfSimpleTag)
        expectClosingTag = false;
      else{
        if (this.currentToken == Token.EndOfTag){
          this.GetNextToken();
          context = this.scanner.CurrentSourceContext;
        }else{
          this.HandleError(Error.SyntaxError, ">");
          context = this.scanner.CurrentSourceContext;
        }
        expectClosingTag = true;
      }
    }
    private void Skip(Token token){
      if (this.currentToken == token)
        this.GetNextToken();
      else{
        switch(token){
          case Token.Alias: this.HandleError(Error.SyntaxError, "alias"); break;
          case Token.Colon: this.HandleError(Error.SyntaxError, ":"); break;
          case Token.Identifier: this.HandleError(Error.ExpectedIdentifier); break;
          case Token.In: this.HandleError(Error.InExpected); break;
          case Token.LeftBrace: this.HandleError(Error.ExpectedLeftBrace); break;
          case Token.LeftParenthesis: this.HandleError(Error.SyntaxError, "("); break;
          case Token.RightBrace: this.HandleError(Error.ExpectedRightBrace); break;
          case Token.RightParenthesis: this.HandleError(Error.ExpectedRightParenthesis); break;
          case Token.Semicolon: this.HandleError(Error.ExpectedSemicolon); break;
          default: this.HandleError(Error.UnexpectedToken, this.scanner.GetTokenSource()); break;
        }
      }
    }
    private void SkipIdentifierOrNonReservedKeyword(){
      this.SkipIdentifierOrNonReservedKeyword(Error.ExpectedIdentifier);
    }
    private void SkipIdentifierOrNonReservedKeyword(Error error){
      if (Parser.IdentifierOrNonReservedKeyword[this.currentToken])
        this.GetNextToken();
      else{
        if (error == Error.ExpectedIdentifier){
          string tokSource = this.scanner.GetTokenSource();
          if (tokSource.Length > 0 && this.scanner.IsIdentifierStartChar(tokSource[0]))
            this.HandleError(Error.IdentifierExpectedKW, this.scanner.GetTokenSource());
          else
            this.HandleError(error);
        }else
          this.HandleError(error);
        string nameString = this.scanner.GetTokenSource();
        if (this.currentToken != Token.EndOfFile && nameString != null && nameString.Length > 0 &&  Scanner.IsAsciiLetter(nameString[0]))
          this.GetNextToken();
      }
    }
    private void SkipTo(TokenSet followers){
      if (followers[this.currentToken]) return;
      Error error = Error.InvalidExprTerm;
      if (this.currentToken == Token.Using)
        error = Error.UsingAfterElements;
      else if (!this.insideBlock)
        error = Error.InvalidMemberDecl;
      this.HandleError(error, this.scanner.GetTokenSource());
      if (this.currentToken == Token.Identifier && this.scanner.endPos >= this.scanner.maxPos && followers[Token.LastIdentifier])
        return;
      while(!followers[this.currentToken]){
        this.GetNextToken();
      }
    }
    private void SkipTo(TokenSet followers, Error error, params string[] messages){
      if (error != Error.None)
        this.HandleError(error, messages);
      if (this.currentToken == Token.Identifier && this.scanner.endPos >= this.scanner.maxPos && followers[Token.LastIdentifier])
        return;
      while (!followers[this.currentToken])
        this.GetNextToken();
    }
    private void SkipSemiColon(TokenSet followers){
      if (this.currentToken == Token.Semicolon){
        this.GetNextToken();
        this.SkipTo(followers);
      }else{
        this.Skip(Token.Semicolon);
        if (this.currentToken == Token.Identifier && this.scanner.endPos >= this.scanner.maxPos && followers[Token.LastIdentifier])
          return;
        while (!this.scanner.TokenIsFirstAfterLineBreak && this.currentToken != Token.Semicolon && this.currentToken != Token.RightBrace
          && (this.currentToken != Token.LeftBrace || !followers[Token.LeftBrace]))
          this.GetNextToken();
        if (this.currentToken == Token.Semicolon){
          this.GetNextToken();
          this.SkipTo(followers);
        }
      }
    }
    /// <summary>
    /// returns true if opnd1 operator1 opnd2 operator2 opnd3 implicitly brackets as opnd1 operator1 (opnd2 operator2 opnd3)
    /// </summary>
    private static bool LowerPriority(Token operator1, Token operator2){
      switch(operator1){
        case Token.Divide:
        case Token.Multiply:
        case Token.Remainder:
          switch(operator2){
            default:
              return false;
          }
        case Token.Plus:
        case Token.Subtract:
        switch(operator2){
          case Token.Divide:
          case Token.Multiply:
          case Token.Remainder:
            return true;
          default:
            return false;
        }
        case Token.LeftShift:
        case Token.RightShift:
          switch(operator2){
            case Token.Divide:
            case Token.Multiply:
            case Token.Remainder:
            case Token.Plus:
            case Token.Subtract:
              return true;
            default:
              return false;
          }
        case Token.As:
        case Token.GreaterThan:
        case Token.GreaterThanOrEqual:
        case Token.Is:
        case Token.LessThan:
        case Token.LessThanOrEqual:
          switch(operator2){
            case Token.Divide:
            case Token.Multiply:
            case Token.Remainder:
            case Token.Plus:
            case Token.Subtract:
            case Token.LeftShift:
            case Token.RightShift:
              return true;
            default:
              return false;
          }
        case Token.Equal:
        case Token.NotEqual:
        case Token.Maplet:
        case Token.Range:
          switch(operator2){
            case Token.Divide:
            case Token.Multiply:
            case Token.Remainder:
            case Token.Plus:
            case Token.Subtract:
            case Token.LeftShift:
            case Token.RightShift:
            case Token.As:
            case Token.GreaterThan:
            case Token.GreaterThanOrEqual:
            case Token.Is:
            case Token.LessThan:
            case Token.LessThanOrEqual:
              return true;
            default:
              return false;
          }
        case Token.BitwiseAnd:
          switch(operator2){
            case Token.Divide:
            case Token.Multiply:
            case Token.Remainder:
            case Token.Plus:
            case Token.Subtract:
            case Token.LeftShift:
            case Token.RightShift:
            case Token.As:
            case Token.GreaterThan:
            case Token.GreaterThanOrEqual:
            case Token.Is:
            case Token.LessThan:
            case Token.LessThanOrEqual:
            case Token.Maplet:
            case Token.Range:
            case Token.Equal:
            case Token.NotEqual:
              return true;
            default:
              return false;
          }
        case Token.BitwiseXor:
          switch(operator2){
            case Token.Divide:
            case Token.Multiply:
            case Token.Remainder:
            case Token.Plus:
            case Token.Subtract:
            case Token.LeftShift:
            case Token.RightShift:
            case Token.As:
            case Token.GreaterThan:
            case Token.GreaterThanOrEqual:
            case Token.Is:
            case Token.LessThan:
            case Token.LessThanOrEqual:
            case Token.Maplet:
            case Token.Range:
            case Token.Equal:
            case Token.NotEqual:
            case Token.BitwiseAnd:
              return true;
            default:
              return false;
          }
        case Token.BitwiseOr:
          switch(operator2){
            case Token.Divide:
            case Token.Multiply:
            case Token.Remainder:
            case Token.Plus:
            case Token.Subtract:
            case Token.LeftShift:
            case Token.RightShift:
            case Token.As:
            case Token.GreaterThan:
            case Token.GreaterThanOrEqual:
            case Token.Is:
            case Token.LessThan:
            case Token.LessThanOrEqual:
            case Token.Range:
            case Token.Maplet:
            case Token.Equal:
            case Token.NotEqual:
            case Token.BitwiseAnd:
            case Token.BitwiseXor:
              return true;
            default:
              return false;
          }
        case Token.LogicalAnd:
          switch(operator2){
            case Token.Divide:
            case Token.Multiply:
            case Token.Remainder:
            case Token.Plus:
            case Token.Subtract:
            case Token.LeftShift:
            case Token.RightShift:
            case Token.As:
            case Token.GreaterThan:
            case Token.GreaterThanOrEqual:
            case Token.Is:
            case Token.LessThan:
            case Token.LessThanOrEqual:
            case Token.Maplet:
            case Token.Equal:
            case Token.NotEqual:
            case Token.BitwiseAnd:
            case Token.BitwiseXor:
            case Token.BitwiseOr:
              return true;
            default:
              return false;
          }
        case Token.LogicalOr:
          switch(operator2){
            case Token.Divide:
            case Token.Multiply:
            case Token.Remainder:
            case Token.Plus:
            case Token.Subtract:
            case Token.LeftShift:
            case Token.RightShift:
            case Token.As:
            case Token.GreaterThan:
            case Token.GreaterThanOrEqual:
            case Token.Is:
            case Token.LessThan:
            case Token.LessThanOrEqual:
            case Token.Maplet:
            case Token.Range:
            case Token.Equal:
            case Token.NotEqual:
            case Token.BitwiseAnd:
            case Token.BitwiseXor:
            case Token.BitwiseOr:
            case Token.LogicalAnd:
              return true;
            default:
              return false;
          }
        case Token.NullCoalescingOp:
          switch (operator2) {
            case Token.Divide:
            case Token.Multiply:
            case Token.Remainder:
            case Token.Plus:
            case Token.Subtract:
            case Token.LeftShift:
            case Token.RightShift:
            case Token.As:
            case Token.GreaterThan:
            case Token.GreaterThanOrEqual:
            case Token.Is:
            case Token.LessThan:
            case Token.LessThanOrEqual:
            case Token.Maplet:
            case Token.Range:
            case Token.Equal:
            case Token.NotEqual:
            case Token.BitwiseAnd:
            case Token.BitwiseXor:
            case Token.BitwiseOr:
            case Token.LogicalAnd:
            case Token.LogicalOr:
            case Token.NullCoalescingOp:
              return true;
            default:
              return false;
          }
        case Token.Implies:
          switch(operator2){
            case Token.Iff:
                  return false;
            default:
              return true;
          }
        case Token.Iff:
          return true;
      }
      Debug.Assert(false);
      return false;
    }
    private void HandleError(Error error, params string[] messageParameters){
      ErrorNode enode = new SpecSharpErrorNode(error, messageParameters);
      enode.SourceContext = this.scanner.CurrentSourceContext;
      if (error == Error.ExpectedSemicolon && this.scanner.TokenIsFirstAfterLineBreak){
        int i = this.scanner.eolPos;
        if (i > 1)
          enode.SourceContext.StartPos = i-2; //Try to have a place for the cursor to hover
        else
          enode.SourceContext.StartPos = 0;
        enode.SourceContext.EndPos = i;
      }
      if (this.errors == null) return;
      this.errors.Add(enode);
    }
    private void HandleError(SourceContext ctx, Error error, params string[] messageParameters){
      if (ctx.Document == null) return;
      ErrorNode enode = new SpecSharpErrorNode(error, messageParameters);
      enode.SourceContext = ctx;
      if (this.errors == null) return;
      this.errors.Add(enode);
    }
    private static NodeType ConvertToQuantifierType(Token op){
      switch(op){
        case Token.Exists: return NodeType.Exists;
        case Token.Unique: return NodeType.ExistsUnique;
        case Token.Forall: return NodeType.Forall;
        case Token.Count: return NodeType.Count;
        case Token.Sum: return NodeType.Sum;
        case Token.Max: return NodeType.Max;
        case Token.Min: return NodeType.Min;
        case Token.Product: return NodeType.Product;
        default: return NodeType.Nop;
      }
    }
    private static NodeType ConvertToBinaryNodeType(Token op){
      switch(op){
        case Token.AddAssign:
        case Token.AddOne:
        case Token.Plus: return NodeType.Add;
        case Token.As: return NodeType.As;
        case Token.BitwiseAndAssign:
        case Token.BitwiseAnd: return NodeType.And;
        case Token.BitwiseOrAssign:
        case Token.BitwiseOr: return NodeType.Or;
        case Token.BitwiseXorAssign:
        case Token.BitwiseXor: return NodeType.Xor;
        case Token.DivideAssign:
        case Token.Divide: return NodeType.Div;
        case Token.Equal: return NodeType.Eq;
        case Token.GreaterThan: return NodeType.Gt;
        case Token.GreaterThanOrEqual: return NodeType.Ge;
        case Token.Is: return NodeType.Is;
        case Token.Iff: return NodeType.Iff;
        case Token.Implies: return NodeType.Implies;
        case Token.LeftShiftAssign:
        case Token.LeftShift: return NodeType.Shl;
        case Token.LessThan: return NodeType.Lt;
        case Token.LessThanOrEqual: return NodeType.Le;
        case Token.LogicalAnd: return NodeType.LogicalAnd;
        case Token.LogicalOr: return NodeType.LogicalOr;
        case Token.Maplet: return NodeType.Maplet;
        case Token.MultiplyAssign:
        case Token.Multiply: return NodeType.Mul;
        case Token.NotEqual: return NodeType.Ne;
        case Token.NullCoalescingOp: return NodeType.NullCoalesingExpression;
        case Token.Range: return NodeType.Range;
        case Token.RemainderAssign:
        case Token.Remainder: return NodeType.Rem;
        case Token.RightShiftAssign:
        case Token.RightShift: return NodeType.Shr;
        case Token.SubtractAssign:
        case Token.SubtractOne:
        case Token.Subtract: return NodeType.Sub;
        default: return NodeType.Nop;
      }
    }
    private static NodeType ConvertToUnaryNodeType(Token op){
      switch(op){
        case Token.Plus: return NodeType.UnaryPlus;
        case Token.BitwiseNot: return NodeType.Not;
        case Token.Subtract: return NodeType.Neg;
        case Token.LogicalNot: return NodeType.LogicalNot;
        case Token.AddOne: return NodeType.Increment;
        case Token.SubtractOne: return NodeType.Decrement;
        case Token.BitwiseAnd: return NodeType.AddressOf;
        default: return NodeType.Nop;
      }
    }
    private bool ExplicitInterfaceImplementationIsAllowable(TypeNode parentType, Identifier id){
      if (parentType is Class || parentType is Struct) return true;
      this.HandleError(id.SourceContext, Error.ExplicitInterfaceImplementationInNonClassOrStruct, 
        id.ToString()+"."+this.scanner.GetTokenSource());
      return false;
    }

    private static readonly TokenSet AddOneOrSubtractOne;
    private static readonly TokenSet AddOrRemoveOrModifier;
    private static readonly TokenSet AssignmentOperators;
    private static readonly TokenSet AttributeOrNamespaceOrTypeDeclarationStart;
    private static readonly TokenSet AttributeOrTypeDeclarationStart;
    private static readonly TokenSet CaseOrDefaultOrRightBrace;
    private static readonly TokenSet CaseOrColonOrDefaultOrRightBrace;
    private static readonly TokenSet CatchOrFinally;
    private static readonly TokenSet EndOfFile;
    private static readonly TokenSet GetOrLeftBracketOrSetOrModifier;
    private static readonly TokenSet IdentifierOrNonReservedKeyword;
    private static readonly TokenSet InfixOperators;
    private static readonly TokenSet ParameterTypeStart;
    private static readonly TokenSet PrimaryStart;
    private static readonly TokenSet ProtectionModifier;
    private static readonly TokenSet NamespaceOrTypeDeclarationStart;
    private static readonly TokenSet RightParenthesisOrSemicolon;
    private static readonly TokenSet StatementStart;
    private static readonly TokenSet TypeArgumentListNonFollower;
    private static readonly TokenSet TypeMemberStart;
    private static readonly TokenSet ContractStart;
    private static readonly TokenSet TypeStart;
    private static readonly TokenSet TypeOperator;
    private static readonly TokenSet UnaryStart;
    private static readonly TokenSet LiteralElementStart;
    private static readonly TokenSet Term; //  Token belongs to first set for term-or-unary-operator (follows casts), but is not a predefined type.
    private static readonly TokenSet Predefined; // Token is a predefined type
    private static readonly TokenSet UnaryOperator; //  Token belongs to unary operator
    private static readonly TokenSet NullableTypeNonFollower;
    
    static Parser(){
      AddOneOrSubtractOne = new TokenSet();
      AddOneOrSubtractOne |= Token.AddOne;
      AddOneOrSubtractOne |= Token.SubtractOne;

      AddOrRemoveOrModifier = new TokenSet();
      AddOrRemoveOrModifier |= Token.Add;
      AddOrRemoveOrModifier |= Token.Remove;
      AddOrRemoveOrModifier |= Token.New;
      AddOrRemoveOrModifier |= Token.Public;
      AddOrRemoveOrModifier |= Token.Protected;
      AddOrRemoveOrModifier |= Token.Internal;
      AddOrRemoveOrModifier |= Token.Private;
      AddOrRemoveOrModifier |= Token.Abstract;
      AddOrRemoveOrModifier |= Token.Sealed;
      AddOrRemoveOrModifier |= Token.Static;
      AddOrRemoveOrModifier |= Token.Readonly;
      AddOrRemoveOrModifier |= Token.Volatile;
      AddOrRemoveOrModifier |= Token.Virtual;
      //AddOrRemoveOrModifier |= Token.Operation; //HS D
      //AddOrRemoveOrModifier |= Token.Transformable; //HS D
      AddOrRemoveOrModifier |= Token.Override;
      AddOrRemoveOrModifier |= Token.Extern;
      AddOrRemoveOrModifier |= Token.Unsafe;

      AssignmentOperators = new TokenSet();      
      AssignmentOperators |= Token.AddAssign;
      AssignmentOperators |= Token.Assign;
      AssignmentOperators |= Token.BitwiseAndAssign;
      AssignmentOperators |= Token.BitwiseOrAssign;
      AssignmentOperators |= Token.BitwiseXorAssign;
      AssignmentOperators |= Token.DivideAssign;
      AssignmentOperators |= Token.LeftShiftAssign;
      AssignmentOperators |= Token.MultiplyAssign;
      AssignmentOperators |= Token.RemainderAssign;
      AssignmentOperators |= Token.RightShiftAssign;
      AssignmentOperators |= Token.SubtractAssign;

      AttributeOrTypeDeclarationStart = new TokenSet();
      AttributeOrTypeDeclarationStart |= Token.LeftBracket;
      AttributeOrTypeDeclarationStart |= Token.New;
      AttributeOrTypeDeclarationStart |= Token.Partial;
      AttributeOrTypeDeclarationStart |= Token.Unsafe;
      AttributeOrTypeDeclarationStart |= Token.Public;
      AttributeOrTypeDeclarationStart |= Token.Internal;
      AttributeOrTypeDeclarationStart |= Token.Abstract;
      AttributeOrTypeDeclarationStart |= Token.Sealed;
      AttributeOrTypeDeclarationStart |= Token.Static;
      AttributeOrTypeDeclarationStart |= Token.Class;
      AttributeOrTypeDeclarationStart |= Token.Delegate;
      AttributeOrTypeDeclarationStart |= Token.Enum;
      AttributeOrTypeDeclarationStart |= Token.Interface;
      AttributeOrTypeDeclarationStart |= Token.Struct;
      AttributeOrTypeDeclarationStart |= Token.MultiLineDocCommentStart;
      AttributeOrTypeDeclarationStart |= Token.SingleLineDocCommentStart;

      CaseOrDefaultOrRightBrace = new TokenSet();
      CaseOrDefaultOrRightBrace |= Token.Case;
      CaseOrDefaultOrRightBrace |= Token.Default;
      CaseOrDefaultOrRightBrace |= Token.RightBrace;

      CaseOrColonOrDefaultOrRightBrace = CaseOrDefaultOrRightBrace;
      CaseOrColonOrDefaultOrRightBrace |= Token.Colon;

      CatchOrFinally = new TokenSet();
      CatchOrFinally |= Token.Catch;
      CatchOrFinally |= Token.Finally;

      ContractStart = new TokenSet();
      ContractStart |= Token.Requires;
      ContractStart |= Token.Modifies;
      ContractStart |= Token.Ensures;
      ContractStart |= Token.Throws;

      EndOfFile = new TokenSet();
      EndOfFile |= Token.EndOfFile;

      GetOrLeftBracketOrSetOrModifier = new TokenSet();
      GetOrLeftBracketOrSetOrModifier |= Token.Get;
      GetOrLeftBracketOrSetOrModifier |= Token.LeftBracket;
      GetOrLeftBracketOrSetOrModifier |= Token.Set;
      GetOrLeftBracketOrSetOrModifier |= Token.New;
      GetOrLeftBracketOrSetOrModifier |= Token.Public;
      GetOrLeftBracketOrSetOrModifier |= Token.Protected;
      GetOrLeftBracketOrSetOrModifier |= Token.Internal;
      GetOrLeftBracketOrSetOrModifier |= Token.Private;
      GetOrLeftBracketOrSetOrModifier |= Token.Abstract;
      GetOrLeftBracketOrSetOrModifier |= Token.Sealed;
      GetOrLeftBracketOrSetOrModifier |= Token.Static;
      GetOrLeftBracketOrSetOrModifier |= Token.Readonly;
      GetOrLeftBracketOrSetOrModifier |= Token.Volatile;
      GetOrLeftBracketOrSetOrModifier |= Token.Virtual;
      //GetOrLeftBracketOrSetOrModifier |= Token.Operation; //HS D
      //GetOrLeftBracketOrSetOrModifier |= Token.Transformable; //HS D
      GetOrLeftBracketOrSetOrModifier |= Token.Override;
      GetOrLeftBracketOrSetOrModifier |= Token.Extern;
      GetOrLeftBracketOrSetOrModifier |= Token.Unsafe;
      
      IdentifierOrNonReservedKeyword = new TokenSet();
      IdentifierOrNonReservedKeyword |= Token.Identifier;
      IdentifierOrNonReservedKeyword |= Token.Acquire;
      IdentifierOrNonReservedKeyword |= Token.Add;
      IdentifierOrNonReservedKeyword |= Token.Additive;
      IdentifierOrNonReservedKeyword |= Token.Alias;
      IdentifierOrNonReservedKeyword |= Token.Assert;
      IdentifierOrNonReservedKeyword |= Token.Assume;
      IdentifierOrNonReservedKeyword |= Token.Count;
      IdentifierOrNonReservedKeyword |= Token.Ensures;
      IdentifierOrNonReservedKeyword |= Token.Exists;
      IdentifierOrNonReservedKeyword |= Token.Expose;
      IdentifierOrNonReservedKeyword |= Token.Forall;
      IdentifierOrNonReservedKeyword |= Token.Get;
      IdentifierOrNonReservedKeyword |= Token.Max;
      IdentifierOrNonReservedKeyword |= Token.Min;
      IdentifierOrNonReservedKeyword |= Token.Model;
      IdentifierOrNonReservedKeyword |= Token.Modifies;
      IdentifierOrNonReservedKeyword |= Token.Old;
      IdentifierOrNonReservedKeyword |= Token.Otherwise;
      IdentifierOrNonReservedKeyword |= Token.Partial;
      IdentifierOrNonReservedKeyword |= Token.Product;
      IdentifierOrNonReservedKeyword |= Token.Read;
      IdentifierOrNonReservedKeyword |= Token.Remove;
      IdentifierOrNonReservedKeyword |= Token.Requires;
      IdentifierOrNonReservedKeyword |= Token.Satisfies;
      IdentifierOrNonReservedKeyword |= Token.Set;
      IdentifierOrNonReservedKeyword |= Token.Sum;
      IdentifierOrNonReservedKeyword |= Token.Throws;
      IdentifierOrNonReservedKeyword |= Token.Unique;
      IdentifierOrNonReservedKeyword |= Token.Value;
      IdentifierOrNonReservedKeyword |= Token.Var;
      IdentifierOrNonReservedKeyword |= Token.Witness;
      IdentifierOrNonReservedKeyword |= Token.Write;
      IdentifierOrNonReservedKeyword |= Token.Yield;
      IdentifierOrNonReservedKeyword |= Token.Where;      

      InfixOperators = new TokenSet();      
      InfixOperators |= Token.AddAssign;
      InfixOperators |= Token.As;
      InfixOperators |= Token.Assign;
      InfixOperators |= Token.BitwiseAnd;
      InfixOperators |= Token.BitwiseAndAssign;
      InfixOperators |= Token.BitwiseOr;
      InfixOperators |= Token.BitwiseOrAssign;
      InfixOperators |= Token.BitwiseXor;
      InfixOperators |= Token.BitwiseXorAssign;
      InfixOperators |= Token.Conditional;
      InfixOperators |= Token.Divide;
      InfixOperators |= Token.DivideAssign;
      InfixOperators |= Token.Equal;
      InfixOperators |= Token.GreaterThan;
      InfixOperators |= Token.GreaterThanOrEqual;
      InfixOperators |= Token.Is;
      InfixOperators |= Token.Iff;
      InfixOperators |= Token.Implies;
      InfixOperators |= Token.LeftShift;
      InfixOperators |= Token.LeftShiftAssign;
      InfixOperators |= Token.LessThan;
      InfixOperators |= Token.LessThanOrEqual;
      InfixOperators |= Token.LogicalAnd;
      InfixOperators |= Token.LogicalOr;
      InfixOperators |= Token.Maplet; 
      InfixOperators |= Token.Multiply;
      InfixOperators |= Token.MultiplyAssign;
      InfixOperators |= Token.NotEqual;
      InfixOperators |= Token.NullCoalescingOp;
      InfixOperators |= Token.Plus;
      InfixOperators |= Token.Range; 
      InfixOperators |= Token.Remainder;
      InfixOperators |= Token.RemainderAssign;
      InfixOperators |= Token.RightShift;
      InfixOperators |= Token.RightShiftAssign;
      InfixOperators |= Token.Subtract;
      InfixOperators |= Token.SubtractAssign;
      InfixOperators |= Token.Arrow;

      TypeStart = new TokenSet();
      TypeStart |= Parser.IdentifierOrNonReservedKeyword;
      TypeStart |= Token.Bool;
      TypeStart |= Token.Decimal;
      TypeStart |= Token.Sbyte;
      TypeStart |= Token.Byte;
      TypeStart |= Token.Short;
      TypeStart |= Token.Ushort;
      TypeStart |= Token.Int;
      TypeStart |= Token.Uint;
      TypeStart |= Token.Long;
      TypeStart |= Token.Ulong;
      TypeStart |= Token.Char;
      TypeStart |= Token.Float;
      TypeStart |= Token.Double;
      TypeStart |= Token.Object;
      TypeStart |= Token.String;
      TypeStart |= Token.MultiLineDocCommentStart;
      TypeStart |= Token.SingleLineDocCommentStart;
      TypeStart |= Token.LeftBracket;
      TypeStart |= Token.LeftParenthesis;

      ParameterTypeStart = new TokenSet();
      ParameterTypeStart |= Parser.TypeStart;
      ParameterTypeStart |= Token.Ref;
      ParameterTypeStart |= Token.Out;
      ParameterTypeStart |= Token.Params;
      //ParameterTypeStart |= Token.Transformable; //HS D

      PrimaryStart = new TokenSet();
      PrimaryStart |= Parser.IdentifierOrNonReservedKeyword;
      PrimaryStart |= Token.This;
      PrimaryStart |= Token.Base;
      PrimaryStart |= Token.Value;
      PrimaryStart |= Token.New;
      PrimaryStart |= Token.Typeof;
      PrimaryStart |= Token.Sizeof;
      PrimaryStart |= Token.Stackalloc;
      PrimaryStart |= Token.Checked;
      PrimaryStart |= Token.Unchecked;
      PrimaryStart |= Token.HexLiteral;
      PrimaryStart |= Token.IntegerLiteral;
      PrimaryStart |= Token.StringLiteral;
      PrimaryStart |= Token.CharLiteral;
      PrimaryStart |= Token.RealLiteral;
      PrimaryStart |= Token.Null;
      PrimaryStart |= Token.False;
      PrimaryStart |= Token.Hole; //HS D
      PrimaryStart |= Token.True;
      PrimaryStart |= Token.Bool;
      PrimaryStart |= Token.Decimal;
      PrimaryStart |= Token.Sbyte;
      PrimaryStart |= Token.Byte;
      PrimaryStart |= Token.Short;
      PrimaryStart |= Token.Ushort;
      PrimaryStart |= Token.Int;
      PrimaryStart |= Token.Uint;
      PrimaryStart |= Token.Long;
      PrimaryStart |= Token.Ulong;
      PrimaryStart |= Token.Char;
      PrimaryStart |= Token.Float;
      PrimaryStart |= Token.Double;
      PrimaryStart |= Token.Object;
      PrimaryStart |= Token.String;
      PrimaryStart |= Token.LeftParenthesis;

      ProtectionModifier = new TokenSet();
      ProtectionModifier |= Token.Public;
      ProtectionModifier |= Token.Protected;
      ProtectionModifier |= Token.Internal;
      ProtectionModifier |= Token.Private;

      NamespaceOrTypeDeclarationStart = new TokenSet();
      NamespaceOrTypeDeclarationStart |= Token.Namespace;
      NamespaceOrTypeDeclarationStart |= Token.Class;
      NamespaceOrTypeDeclarationStart |= Token.Delegate;
      NamespaceOrTypeDeclarationStart |= Token.Enum;
      NamespaceOrTypeDeclarationStart |= Token.Interface;
      NamespaceOrTypeDeclarationStart |= Token.Struct;

      AttributeOrNamespaceOrTypeDeclarationStart = AttributeOrTypeDeclarationStart;
      AttributeOrNamespaceOrTypeDeclarationStart |= Token.Namespace;
      AttributeOrNamespaceOrTypeDeclarationStart |= Token.Private; //For error recovery
      AttributeOrNamespaceOrTypeDeclarationStart |= Token.Protected; //For error recovery

      RightParenthesisOrSemicolon = new TokenSet();
      RightParenthesisOrSemicolon |= Token.RightParenthesis;
      RightParenthesisOrSemicolon |= Token.Semicolon;

      TypeMemberStart = new TokenSet();
      TypeMemberStart |= Token.LeftBracket;
      TypeMemberStart |= Token.LeftParenthesis;
      TypeMemberStart |= Token.LeftBrace;
      TypeMemberStart |= Token.New;
      TypeMemberStart |= Token.Partial;
      TypeMemberStart |= Token.Public;
      TypeMemberStart |= Token.Protected;
      TypeMemberStart |= Token.Internal;
      TypeMemberStart |= Token.Private;
      TypeMemberStart |= Token.Abstract;
      TypeMemberStart |= Token.Sealed;
      TypeMemberStart |= Token.Static;
      TypeMemberStart |= Token.Readonly;
      TypeMemberStart |= Token.Volatile;
      TypeMemberStart |= Token.Virtual;
      //TypeMemberStart |= Token.Operation; //HS
      //TypeMemberStart |= Token.Transformable; //HS
      TypeMemberStart |= Token.Override;
      TypeMemberStart |= Token.Extern;
      TypeMemberStart |= Token.Unsafe;
      TypeMemberStart |= Token.Const;
      TypeMemberStart |= Parser.IdentifierOrNonReservedKeyword;
      TypeMemberStart |= Token.Event;
      TypeMemberStart |= Token.This;
      TypeMemberStart |= Token.Operator;
      TypeMemberStart |= Token.BitwiseNot;
      TypeMemberStart |= Token.Static;
      TypeMemberStart |= Token.Class;
      TypeMemberStart |= Token.Delegate;
      TypeMemberStart |= Token.Enum;
      TypeMemberStart |= Token.Interface;
      TypeMemberStart |= Token.Struct;
      TypeMemberStart |= Token.Bool;
      TypeMemberStart |= Token.Decimal;
      TypeMemberStart |= Token.Sbyte;
      TypeMemberStart |= Token.Byte;
      TypeMemberStart |= Token.Short;
      TypeMemberStart |= Token.Ushort;
      TypeMemberStart |= Token.Int;
      TypeMemberStart |= Token.Uint;
      TypeMemberStart |= Token.Long;
      TypeMemberStart |= Token.Ulong;
      TypeMemberStart |= Token.Char;
      TypeMemberStart |= Token.Float;
      TypeMemberStart |= Token.Double;
      TypeMemberStart |= Token.Object;
      TypeMemberStart |= Token.String;
      TypeMemberStart |= Token.MultiLineDocCommentStart;
      TypeMemberStart |= Token.SingleLineDocCommentStart;
      TypeMemberStart |= Token.Void;
      TypeMemberStart |= Token.Invariant;
      TypeMemberStart |= Token.Model;

      TypeOperator = new TokenSet();
      TypeOperator |= Token.LeftBracket;
      TypeOperator |= Token.Multiply;
      TypeOperator |= Token.Plus;
      TypeOperator |= Token.Conditional;
      TypeOperator |= Token.LogicalNot;
      TypeOperator |= Token.BitwiseAnd;

      UnaryStart = new TokenSet();
      UnaryStart |= Parser.IdentifierOrNonReservedKeyword;
      UnaryStart |= Token.LeftParenthesis;
      UnaryStart |= Token.LeftBracket;
      UnaryStart |= Token.This;
      UnaryStart |= Token.Base;
      UnaryStart |= Token.Value;
      UnaryStart |= Token.AddOne;
      UnaryStart |= Token.SubtractOne;
      UnaryStart |= Token.New;
      UnaryStart |= Token.Default;
      UnaryStart |= Token.Typeof;
      UnaryStart |= Token.Sizeof;
      UnaryStart |= Token.Stackalloc;
      UnaryStart |= Token.Delegate;
      UnaryStart |= Token.Checked;
      UnaryStart |= Token.Unchecked;
      UnaryStart |= Token.HexLiteral;
      UnaryStart |= Token.IntegerLiteral;
      UnaryStart |= Token.StringLiteral;
      UnaryStart |= Token.CharLiteral;
      UnaryStart |= Token.RealLiteral;
      UnaryStart |= Token.Null;
      UnaryStart |= Token.False;
      UnaryStart |= Token.Hole; //HS D
      UnaryStart |= Token.True;
      UnaryStart |= Token.Bool;
      UnaryStart |= Token.Decimal;
      UnaryStart |= Token.Sbyte;
      UnaryStart |= Token.Byte;
      UnaryStart |= Token.Short;
      UnaryStart |= Token.Ushort;
      UnaryStart |= Token.Int;
      UnaryStart |= Token.Uint;
      UnaryStart |= Token.Long;
      UnaryStart |= Token.Ulong;
      UnaryStart |= Token.Char;
      UnaryStart |= Token.Float;
      UnaryStart |= Token.Double;
      UnaryStart |= Token.Object;
      UnaryStart |= Token.String;
      UnaryStart |= Token.Plus;
      UnaryStart |= Token.BitwiseNot;
      UnaryStart |= Token.LogicalNot;
      UnaryStart |= Token.Multiply;
      UnaryStart |= Token.Subtract;
      UnaryStart |= Token.AddOne;
      UnaryStart |= Token.SubtractOne;
      UnaryStart |= Token.Multiply;
      UnaryStart |= Token.BitwiseAnd;

      StatementStart = new TokenSet();
      StatementStart |= Parser.UnaryStart;
      StatementStart |= Token.LeftBrace;
      StatementStart |= Token.Semicolon;
      StatementStart |= Token.Acquire;
      StatementStart |= Token.Additive;
      StatementStart |= Token.Assert;
      StatementStart |= Token.Assume;
      StatementStart |= Token.If;
      StatementStart |= Token.Switch;
      StatementStart |= Token.While;
      StatementStart |= Token.Do;
      StatementStart |= Token.For;
      StatementStart |= Token.Foreach;
      StatementStart |= Token.While;
      StatementStart |= Token.Break;
      StatementStart |= Token.Continue;
      StatementStart |= Token.Goto;
      StatementStart |= Token.Return;
      StatementStart |= Token.Throw;
      StatementStart |= Token.Yield;
      StatementStart |= Token.Try;
      StatementStart |= Token.Catch; //Not really, but helps error recovery
      StatementStart |= Token.Finally; //Not really, but helps error recovery
      StatementStart |= Token.Checked;
      StatementStart |= Token.Unchecked;
      StatementStart |= Token.Read;
      StatementStart |= Token.Write;
      StatementStart |= Token.Expose;
      StatementStart |= Token.Fixed;
      StatementStart |= Token.Lock;
      StatementStart |= Token.Unsafe;
      StatementStart |= Token.Using;
      StatementStart |= Token.Const;
      StatementStart |= Token.Delegate;
      StatementStart |= Token.Void;

      LiteralElementStart = new TokenSet();
      LiteralElementStart |= Token.LiteralComment;
      LiteralElementStart |= Token.CharacterData;
      LiteralElementStart |= Token.LiteralContentString;
      LiteralElementStart |= Token.StartOfClosingTag;
      LiteralElementStart |= Token.StartOfTag;
      LiteralElementStart |= Token.EndOfSimpleTag;
      LiteralElementStart |= Token.EndOfTag;
      LiteralElementStart |= Token.ProcessingInstructions;
      LiteralElementStart |= Token.LeftBrace;

      Term = new TokenSet();
      Term |= Token.ArgList;
      Term |= Token.MakeRef;
      Term |= Token.RefType;
      Term |= Token.RefValue;
      Term |= Token.Base;
      Term |= Token.Checked;
      Term |= Token.Default;
      Term |= Token.Delegate;
      Term |= Token.False;
      Term |= Token.Hole; //HS D 
      Term |= Token.New;
      Term |= Token.Null;
      Term |= Token.Sizeof;
      Term |= Token.This;
      Term |= Token.True;
      Term |= Token.Typeof;
      Term |= Token.Unchecked;
      Term |= Token.Identifier;
      Term |= Token.IntegerLiteral;
      Term |= Token.RealLiteral;
      Term |= Token.StringLiteral;
      Term |= Token.CharLiteral;
      Term |= Token.LeftParenthesis;

      Predefined = new TokenSet();
      Predefined |= Token.Bool;
      Predefined |= Token.Decimal;
      Predefined |= Token.Sbyte;
      Predefined |= Token.Byte;
      Predefined |= Token.Short;
      Predefined |= Token.Ushort;
      Predefined |= Token.Int;
      Predefined |= Token.Uint;
      Predefined |= Token.Long;
      Predefined |= Token.Ulong;
      Predefined |= Token.Char;
      Predefined |= Token.Float;
      Predefined |= Token.Double;
      Predefined |= Token.Object;
      Predefined |= Token.String;
      Predefined |= Token.Void;

      UnaryOperator = new TokenSet();
      UnaryOperator |= Token.Base;
      UnaryOperator |= Token.Default;
      UnaryOperator |= Token.Sizeof;
      UnaryOperator |= Token.This;
      UnaryOperator |= Token.Typeof;
      UnaryOperator |= Token.BitwiseAnd;
      UnaryOperator |= Token.Plus;
      UnaryOperator |= Token.Subtract;
      UnaryOperator |= Token.Multiply;
      UnaryOperator |= Token.BitwiseNot;
      UnaryOperator |= Token.LogicalNot;
      UnaryOperator |= Token.AddOne;
      UnaryOperator |= Token.SubtractOne;

      NullableTypeNonFollower = Term | Predefined | UnaryOperator;

      TypeArgumentListNonFollower = NullableTypeNonFollower;
      TypeArgumentListNonFollower[Token.LeftParenthesis] = false;
    }

    private struct TokenSet{
      private ulong bits0, bits1, bits2, bits3;
      public static TokenSet operator |(TokenSet ts, Token t){
        TokenSet result = new TokenSet();
        int i = (int)t;
        if (i < 64){
          result.bits0 = ts.bits0 | (1ul << i);
          result.bits1 = ts.bits1;
          result.bits2 = ts.bits2;
          result.bits3 = ts.bits3;
        }else if (i < 128){
          result.bits0 = ts.bits0;
          result.bits1 = ts.bits1 | (1ul << (i-64));
          result.bits2 = ts.bits2;
          result.bits3 = ts.bits3;
        }else if (i < 192){
          result.bits0 = ts.bits0;
          result.bits1 = ts.bits1;
          result.bits2 = ts.bits2 | (1ul << (i-128));
          result.bits3 = ts.bits3;
        }else{
          result.bits0 = ts.bits0;
          result.bits1 = ts.bits1;
          result.bits2 = ts.bits2;
          result.bits3 = ts.bits3 | (1ul << (i-192));
        }
        return result;
      }
      public static TokenSet operator |(TokenSet ts1, TokenSet ts2){
        TokenSet result = new TokenSet();
        result.bits0 = ts1.bits0 | ts2.bits0;
        result.bits1 = ts1.bits1 | ts2.bits1;
        result.bits2 = ts1.bits2 | ts2.bits2;
        result.bits3 = ts1.bits3 | ts2.bits3;
        return result;
      }
      internal bool this[Token t]{
        get{
          int i = (int)t;
          if (i < 64)
            return (this.bits0 & (1ul << i)) != 0;
          else if (i < 128)
            return (this.bits1 & (1ul << (i-64))) != 0;
          else if (i < 192)
            return (this.bits2 & (1ul << (i-128))) != 0;
          else
            return (this.bits3 & (1ul << (i-192))) != 0;
        }
        set{
          int i = (int)t;
          if (i < 64)
            if (value)
              this.bits0 |= (1ul << i);
            else
              this.bits0 &= ~(1ul << i);
          else if (i < 128)
            if (value)
              this.bits1 |= (1ul << (i-64));
            else
              this.bits1 &= ~(1ul << (i-64));
          else if (i < 192)
            if (value)
              this.bits2 |= (1ul << (i-128));
            else
              this.bits2 &= ~(1ul << (i-128));
          else
            if (value)
              this.bits3 |= (1ul << (i-192));
            else
              this.bits3 &= ~(1ul << (i-192));
        }
      }
      static TokenSet(){
        int i = (int)Token.EndOfFile;
        Debug.Assert(0 <= i && i <= 255);
      }
    }
  }
  internal sealed class TokenList{
    private Token[] elements;
    private int length = 0;
    internal TokenList(){
      this.elements = new Token[4];
    }
    internal TokenList(int capacity){
      this.elements = new Token[capacity];
    }
    internal void Add(Token element){
      int n = this.elements.Length;
      int i = this.length++;
      if (i == n){
        int m = n*2; if (m < 4) m = 4;
        Token[] newElements = new Token[m];
        for (int j = 0; j < n; j++) newElements[j] = elements[j];
        this.elements = newElements;
      }
      this.elements[i] = element;
    }
    internal int Length{
      get{return this.length;}
    }
    internal Token this[int index]{
      get{
        return this.elements[index];
      }
      set{
        this.elements[index] = value;
      }
    }
  }
  internal sealed class SourceContextList{
    private SourceContext[] elements;
    private int length = 0;
    internal SourceContextList(){
      this.elements = new SourceContext[4];
    }
    internal SourceContextList(int capacity){
      this.elements = new SourceContext[capacity];
    }
    internal void Add(SourceContext element){
      int n = this.elements.Length;
      int i = this.length++;
      if (i == n){
        int m = n*2; if (m < 4) m = 4;
        SourceContext[] newElements = new SourceContext[m];
        for (int j = 0; j < n; j++) newElements[j] = elements[j];
        this.elements = newElements;
      }
      this.elements[i] = element;
    }
    internal int Length{
      get{return this.length;}
    }
    internal SourceContext this[int index]{
      get{
        return this.elements[index];
      }
      set{
        this.elements[index] = value;
      }
    }
  }
#if Xaml
  public sealed class XamlParserFactory : IParserFactory{
    public XamlParserFactory(){
    }
    public IParser CreateParser(string fileName, int lineNumber, DocumentText text, Module symbolTable, ErrorNodeList errorNodes, CompilerParameters options){
      return new XamlParserStub(errorNodes, options as CompilerOptions);
    }
  }
  public sealed class XamlParserStub : IParser{
    private ErrorNodeList errorNodes;
    private CompilerOptions options;

    public XamlParserStub(ErrorNodeList errorNodes, CompilerOptions options){
      this.errorNodes = errorNodes;
      this.options = options;
    }
    public void ParseStatements(StatementList statements){
      Debug.Assert(false);
    }
    public void ParseCompilationUnit(CompilationUnit compilationUnit){
      CompilationUnitSnippet cuSnippet = compilationUnit as CompilationUnitSnippet;
      if (cuSnippet == null || cuSnippet.Compilation == null){Debug.Assert(false); return;}
      XamlSnippet xamlSnippet = new XamlSnippet();
      xamlSnippet.CodeModule = cuSnippet.Compilation.TargetModule;
      xamlSnippet.ErrorHandler = new Microsoft.XamlCompiler.ErrorHandler(this.errorNodes);
      xamlSnippet.Options = this.options;
      xamlSnippet.ParserFactory = new ParserFactory();
      xamlSnippet.XamlDocument = cuSnippet.SourceContext.Document;
      cuSnippet.Nodes = new NodeList(xamlSnippet);
    }
    public void ParseTypeMembers(TypeNode type){
      Debug.Assert(false);
    }
    public Expression ParseExpression(){
      Debug.Assert(false);
      return null;
    }
  }
#endif
}
