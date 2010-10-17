//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  public class CommentInfo{
    public bool supported;
    public string lineStart;
    public string blockStart;
    public string blockEnd;
    public bool useLineComments;
  }
  public enum ParseReason{
    Colorize,
    Check,
    MemberSelect,   //  Also means Implicit invocation
    CompleteWord,
    QuickInfo,
    MethodTip,
    MatchBraces,  //  Also means parameter help.
    HighlightBraces,
    Autos,
    CodeSpan,
    CollapsibleRegions,
    MemberSelectExplicit,
    //Compile,
  }

  public abstract class LanguageService{
    /// <summary>Tracks the symbol table (Module) associated with the current editor window</summary>
    public Module currentSymbolTable;
    public Node currentAst;
    public CultureInfo culture;
    public ErrorHandler errorHandler;
    public Int32List identifierPositions;
    public Int32List identifierLengths;
    public NodeList identifierInfos;
    public Int32List identifierContexts;
    public ScopeList identifierScopes;
    public ScopeList allScopes;
    /// <summary>Set this to True if you want drop downs showing types and members</summary>
    public bool EnableDropDownCombos; 

    public LanguageService(ErrorHandler errorHandler){
      this.errorHandler = errorHandler;
      this.GetCompilationFor = new LanguageService.GetCompilation(this.GetDummyCompilationFor);
    }

    public abstract Scanner GetScanner();
    public abstract Compilation GetDummyCompilationFor(string fileName);
    public abstract void ParseAndAnalyzeCompilationUnit(string fname, string source, int line, int col, ErrorNodeList errors, Compilation compilation, AuthoringSink sink);
    public abstract CompilationUnit ParseCompilationUnit(string fname, string source, ErrorNodeList errors, Compilation compilation, AuthoringSink sink);
    public abstract void Resolve(Member unresolvedMember, Member resolvedMember);
    public abstract void Resolve(CompilationUnit partialCompilationUnit);

    public virtual MemberList GetTypesNamespacesAndPrefixes(Scope scope, bool constructorMustBeVisible, bool listAllUnderRootNamespace) {
      return null;
    }
    public virtual MemberList GetVisibleNames(Scope scope){
      return null;
    }
    public virtual MemberList GetNestedNamespacesAndTypes(Identifier name, Scope scope){
      return this.GetNestedNamespacesAndTypes(name, scope, null);
    }
    public virtual MemberList GetNestedNamespacesAndTypes(Identifier name, Scope scope, AssemblyReferenceList assembliesToSearch){
      return null;
    }
    public virtual MemberList GetNestedNamespaces(Identifier name, Scope scope) {
      MemberList fullList = this.GetNestedNamespacesAndTypes(name, scope);
      MemberList result = new MemberList();
      for (int i = 0, n = fullList == null ? 0 : fullList.Count; i < n; i++){
        Namespace ns = fullList[i] as Namespace;
        if (ns == null) continue;
        result.Add(ns);
      }
      return result;
    }
    public virtual MemberList GetNamespacesAndAttributeTypes(Scope scope){
      return this.GetTypesNamespacesAndPrefixes(scope, true, false);
    }
    public virtual AuthoringHelper GetAuthoringHelper(){
      return new AuthoringHelper(this.errorHandler, this.culture);
    }

    public virtual AuthoringScope GetAuthoringScope(){
      return new AuthoringScope(this, this.GetAuthoringHelper());
    }

    public virtual void GetCommentFormat(CommentInfo info){      
      info.supported = true;
      info.lineStart = "//";
      info.blockStart = "/*";
      info.blockEnd = "*/";
      info.useLineComments = true;
    }

    public virtual void GetMethodFormat(out string typeStart, out string typeEnd, out bool typePrefixed){
      typeStart = "";
      typeEnd = " ";
      typePrefixed = true;
    }

    public AuthoringScope ParseSource(string text, int line, int col, string fname, AuthoringSink asink, ParseReason reason){
      this.currentAst = null;
      Compilation compilation = this.GetCompilationFor(fname); 
      Debug.Assert(compilation != null, "no compilation for: "+fname);
      this.currentSymbolTable = compilation.TargetModule;
      switch (reason){
        case ParseReason.CollapsibleRegions:
        case ParseReason.CompleteWord:
        case ParseReason.MatchBraces:
        case ParseReason.HighlightBraces:
        case ParseReason.MemberSelect:
        case ParseReason.MemberSelectExplicit:
        case ParseReason.MethodTip: 
        case ParseReason.QuickInfo:
        case ParseReason.Autos:{
          return this.ParsePartialCompilationUnit(fname, text, line, col, asink, reason);
        }
        case ParseReason.Check:{
          ErrorNodeList errors = new ErrorNodeList();
          this.ParseAndAnalyzeCompilationUnit(fname, text, line, col, errors, compilation, asink);
          this.ReportErrors(fname, errors, asink);
          return this.GetAuthoringScope();
        }
      }
      return null;
    }
    public virtual AuthoringScope GetAuthoringScopeForMethodBody(string text, Compilation/*!*/ compilation, Method/*!*/ method, AuthoringSink asink) {
      return null;
    }
    public virtual AuthoringScope ParsePartialCompilationUnit(string fname, string text, int line, int col, AuthoringSink asink, ParseReason reason){
      Compilation compilation = this.GetCompilationFor(fname);
      if (line >= 0 && (reason == ParseReason.MemberSelect || reason == ParseReason.MemberSelectExplicit || reason == ParseReason.CompleteWord))
        text = this.Truncate(text, line, col);
      Module savedSymbolTable = this.currentSymbolTable;
      compilation.TargetModule = this.currentSymbolTable = new Module();
      this.currentSymbolTable.AssemblyReferences = savedSymbolTable.AssemblyReferences;
      CompilationUnit partialCompilationUnit = this.ParseCompilationUnit(fname, text, new ErrorNodeList(), compilation, asink);
      compilation.TargetModule = this.currentSymbolTable = savedSymbolTable;
      if (reason != ParseReason.HighlightBraces && reason != ParseReason.MatchBraces){
        MemberFinder memberFinder = this.GetMemberFinder(line+1, col+1);
        memberFinder.Visit(partialCompilationUnit);
        Member unresolvedMember = memberFinder.Member;
        memberFinder.Member = null;
        CompilationUnit cu = this.GetCompilationUnitSnippet(compilation, fname);
        if (cu != null){
          if (unresolvedMember == null){
            //Dealing with a construct that is not part of a type definition, such as a using statement
            this.Resolve(partialCompilationUnit);
          }else{
            memberFinder.Visit(cu);
            if (memberFinder.Member != null)
              this.Resolve(unresolvedMember, memberFinder.Member);
            else
              this.Resolve(partialCompilationUnit); //Symbol table is out of date
          }
        }
      }
      return this.GetAuthoringScope();
    }
    public virtual CompilationUnitSnippet GetCompilationUnitSnippet(Compilation compilation, string fname){
      if (compilation == null || compilation.CompilationUnits == null) return null;
      for (int i = 0, n = compilation.CompilationUnits.Count; i < n; i++){
        CompilationUnitSnippet cu = compilation.CompilationUnits[i] as CompilationUnitSnippet;
        if (cu == null) continue;
        if (string.Compare(cu.Name.ToString(), fname, true, System.Globalization.CultureInfo.InvariantCulture) != 0) continue;
        return cu;
      }
      return null;
    }
    public virtual MemberFinder GetMemberFinder(int line, int col){
      return new MemberFinder(line, col);
    }
    public virtual string Truncate(string text, int line, int col){
      // If we are just parsing for intellisense then there's no point parsing beyond
      // the caret position, except that we do need to complete the token so that we
      // provide the right intellisense on that token.
      int pos = 0;
      int length = text.Length;
      while ( pos < length && line >= 0 && col >= 0 ){
        char ch = text[pos++];
        if (line == 0 && col == 0){
          // cursor position - finish here            
          if (!Char.IsLetter(ch)){
            pos--;
            break;
          }
        }else{
          // handle the standard ret/lf 
          if (ch == '\r' && pos < length && text[pos] == '\n'){
            // reached a new line
            pos++;
            line --;              
            continue;
          }
          // in case of dangling ret and lf we assume each one symbolises a new line 
          // have not encountered this case, but it is safe to do ...
          if (ch == '\n' || ch == '\r'){line --; continue;}
          if (line == 0){
            // now we are in the correct line, so start counting column position
            col--; 
            continue;
          }
        }
      }
      return text.Substring(0,pos);
    }
    public virtual int GetColorCount(){
      return (int)TokenColor.LastColor;
    }
    protected int lastLine = -1;
    protected int lastCol = -1;
    protected string lastFileName;
    public delegate Compilation GetCompilation(string fileName);
    public GetCompilation GetCompilationFor;

    public virtual CompilerParameters GetDummyCompilerParameters(){
      return new CompilerOptions();
    }
    public virtual void ReportErrors(string fileName, ErrorNodeList errors, AuthoringSink sink){
      if (sink == null) return;
      for (int n = errors.Count, i = n-1; i >= 0; i--){ //Scan backwards so that early errors trump later errors
        ErrorNode enode = errors[i];
        if (enode == null || enode.Severity < 0) continue;
        //TODO: suppress warnings of level > set in options
        SourceContext context = enode.SourceContext;
        if (context.Document == null) continue;
        if (context.Document.Name != fileName) continue;
        sink.AddError(enode);
      }
    }
    public virtual void SearchAstForNodeAtPosition(int line, int col, out Node node, out Scope scope, out int identContext){
      node = null;
      scope = null;
      identContext = IdentifierContexts.NullContext;
    }
    public virtual Scope SearchForLeastEnclosingScope(int line, int col) {
      if (allScopes == null) return null;
      Scope retScope = null;
      for (int i = 0; i < allScopes.Count; i++){
        SourceContext sc = allScopes[i].LexicalSourceExtent;
        if (sc.Encloses(line, col)) {
          if (retScope == null) {
            retScope = allScopes[i];
          }
          else {
            SourceContext scForRetScope = retScope.LexicalSourceExtent;
            if (scForRetScope.Encloses(sc))
              retScope = allScopes[i];
          }
        }
      }
      return retScope;
    }
    public virtual void AddReleventKeywords(MemberList memberList, Node node, Scope scope, int identifierContext) {
      return;
    }
    public virtual void SearchForNodeAtPosition(int line, int col, out Node node, out Scope scope, out int identifierContext) {
      if (this.currentAst != null) {
        this.SearchAstForNodeAtPosition(line, col, out node, out scope, out identifierContext);
        identifierContext = IdentifierContexts.NullContext;
        return;
      }
      node = null;
      scope = null;
      identifierContext = IdentifierContexts.AllContext;
      int pos = line*1000+col;
      Int32List posList = this.identifierPositions;
      Int32List lenList = this.identifierLengths;
      NodeList infos = this.identifierInfos;
      ScopeList scopes = this.identifierScopes;
      Int32List identContexts = this.identifierContexts;
      if (posList == null || infos == null) return;
      int i = 0, n = posList.Count, j = n-1;
      while (i < j){
        int k = (i+j) / 2;
        int m = posList[k] - pos;
        if (m < 0)
          i = k+1;
        else if (m > 0)
          j = k;
        else{
          //  Move ahead to the last index with the same pos.
          while (k < n-1 && posList[k+1] == pos)
            k++;
          int bestMatch = -1;
          while (k >= 0 && posList[k] / 1000 == line) {
            //  Within the tange and smaller in length than best match
            if (((posList[k] % 1000) <= col) && ((posList[k] % 1000) + lenList[k] >= col) && (bestMatch == -1 || lenList[k] < lenList[bestMatch]))
              bestMatch = k;
            k--;
          }
          node = infos[bestMatch];
          identifierContext = identContexts[bestMatch];
          scope = scopes[bestMatch];
          return;
        }
      }
      if (j >= 0){
        if (posList[j] > pos && j > 0) j--;
        if (j >= n - 1 && posList[j] / 1000 < line) {
          node = infos[j];
          identifierContext = identContexts[j];
          if (node != null) {
            if (node.SourceContext.EndLine == line && node.SourceContext.EndColumn == col) {
              scope = scopes[j];
              return;
            }
            node = null;
            identifierContext = IdentifierContexts.AllContext;
          }
          return; //At the last node, but not on the same line. No match.
        }
        int bestMatch = -1;
        while (j >= 0 && posList[j] / 1000 == line){
          //  Within the tange and smaller in length than best match
          if (((posList[j] % 1000) <= col) && ((posList[j] % 1000) + lenList[j] >= col) && (bestMatch == -1 || lenList[j] < lenList[bestMatch]))
            bestMatch = j;
          j--;
        }
        if (bestMatch < 0 || posList[bestMatch] / 1000 != line) return;
        node = infos[bestMatch];
        identifierContext = identContexts[bestMatch];
        scope = scopes[bestMatch];
      }
      return;
    }
    public virtual MemberList GetConstructibleNestedTypes(TypeNode typeNode, TypeNode referringType){
      if (typeNode == null) return null;
      MemberList result = new MemberList();
      TypeNodeList nestedTypes = typeNode.NestedTypes;
      for (int i = 0, n = nestedTypes == null ? 0 : nestedTypes.Count; i < n; i++){
        TypeNode nt = nestedTypes[i];
        if (this.TypeHasNoVisibleConstructorsOrIsAbstract(nt, referringType)) continue;
        result.Add(nt);
      }
      return result;
    }
    public virtual bool TypeHasNoVisibleConstructorsOrIsAbstract(TypeNode type, TypeNode referringType){
      if (type == null || referringType == null || type.IsAbstract) return true;
      if (type is Struct || type is EnumNode) return false;
      TypeNode dummy = referringType;
      MemberList constructors = type.GetConstructors();
      for (int i = 0, n = constructors == null ? 0 : constructors.Count; i < n; i++){
        Member constr = constructors[i];
        if (constr == null) continue;
        if (!Checker.NotAccessible(constr, ref dummy, referringType.DeclaringModule, referringType, null)) return false;
      }
      return true;
    }
  }
  public class AuthoringScope{
    protected LanguageService languageService;
    protected AuthoringHelper helper;
    protected bool suppressAttributeSuffix;

    public AuthoringScope(LanguageService languageService, AuthoringHelper helper){
      this.languageService = languageService;
      this.helper = helper;
    }
    public virtual Member GetMember(int line, int col){
      Node n;
      Scope scope;
      int identContext;
      this.languageService.SearchForNodeAtPosition(line + 1, col + 1, out n, out scope, out identContext);
      if (n == null) return null;
      MemberBinding mb = this.GetMemberBinding(n);
      if (mb != null) return mb.BoundMember;
      TypeNode t = n as TypeNode;
      if (t == null && n is Expression)
        t = ((Expression)n).Type;
      if (t != null) return t;
      t = this.GetQueryResultType(n);
      if (t != null) return t;
      return null;
    }
    public virtual string GetDataTipText(int line, int col, out SourceContext sourceContext){
      Node n;
      Scope scope;
      int identContext;
      this.languageService.SearchForNodeAtPosition(line + 1, col + 1, out n, out scope, out identContext);
      if (n == null){
        sourceContext = new SourceContext(); return null;
      }
      sourceContext = n.SourceContext;
      string helpText = null;
      MemberBinding mb = this.GetMemberBinding(n);
      string memberSignature = "";
      if (mb != null){
        sourceContext = mb.SourceContext;
        Member mem = mb.BoundMember;
        Method m = mem as Method;
        if (m != null && m.IsSpecialName && m.Name.ToString().StartsWith("get_")){
          TypeNode[] types = m.GetParameterTypes();
          Identifier propId = Identifier.For(m.Name.ToString().Substring(4));
          TypeNode ty = m.DeclaringType;
          while (ty != null){
            Property prop = ty.GetProperty(propId, types);
            if (prop != null){
              mem = prop;
              break;
            }
            ty = ty.BaseType;
          }
        }
        helpText = mem.HelpText;
        if (mb.TargetObject is ImplicitThis){
          if (mem.DeclaringType is BlockScope)
            memberSignature = this.languageService.errorHandler.GetLocalSignature(mem as Field);
          else if (mem is ParameterField)
            memberSignature = this.languageService.errorHandler.GetParameterSignature((ParameterField)mem);
          else if (mem is Field || mem is Property)
            memberSignature = this.languageService.errorHandler.GetInstanceMemberSignature(mb.BoundMember);
          else
            memberSignature = this.languageService.errorHandler.GetMemberSignature(mb.BoundMember, true);
        }else
          memberSignature = this.languageService.errorHandler.GetMemberSignature(mb.BoundMember, true);
        if (helpText == "" && mb.BoundMember is InstanceInitializer)
          helpText = mem.DeclaringType.HelpText;
      }else{
        TypeNode t = n as TypeNode;
        if (t == null && n is Expression) 
          t = ((Expression)n).Type;
        if (t != null){
          memberSignature = this.languageService.errorHandler.GetMemberSignature(t, true);
          helpText = t.HelpText;
        }else{
          t = this.GetQueryResultType(n);
          if (t != null)
            memberSignature = this.languageService.errorHandler.GetMemberSignature(t, false);
        }
      }
      if (helpText != null && helpText != "")
        return memberSignature + "\n" + helpText;
      else
        return memberSignature;
    }
    public virtual SourceContext GetPositionOfDeclaration(int line, int col){
      return this.GetPositionOfDefinition(line, col);
    }
    public virtual Node GetDefinition(int line, int col) {
      Node n;
      Scope scope;
      int identContext;
      this.languageService.SearchForNodeAtPosition(line + 1, col + 1, out n, out scope, out identContext);
      if (n is TypeNode)
        return n;
      MemberBinding mb = this.GetMemberBinding(n);
      if (mb != null) {
        Member mem = mb.BoundMember;
        if (mem != null) {
          n = mem;
        }
      }
      return n;
    }
    public virtual SourceContext GetPositionOfDefinition(Node definition) {
      TypeNode type = definition as TypeNode;
      Member member = definition as Member;
      Identifier name = null;
      if (type != null)
        name = type.Name;
      else if (member != null)
        name = member.Name;
      SourceContext result = new SourceContext();
      if (name != null) result = name.SourceContext;
      return result;
    }
    public virtual SourceContext GetPositionOfDefinition(int line, int col) {
      return this.GetPositionOfDefinition(this.GetDefinition(line, col));
    }
    public virtual SourceContext GetPositionOfReference(int line, int col){
      return new SourceContext();
    }
    protected virtual MemberBinding GetMemberBinding(Node n){
      NameBinding nb = n as NameBinding;
      QualifiedIdentifier qualId = n as QualifiedIdentifier;
      Construct cons = n as Construct;
      AttributeNode attr = n as AttributeNode;
      MemberBinding mb = null;
      if (nb != null)
        mb = nb.BoundMember as MemberBinding;
      else if (qualId != null)
        mb = qualId.BoundMember as MemberBinding;
      else if (cons != null)
        mb = cons.Constructor as MemberBinding;
      else if (attr != null){
        Literal lit = attr.Constructor as Literal;
        if (lit == null || !(lit.Value is TypeNode)) return null;
        mb = new MemberBinding(null, (TypeNode)lit.Value);
      }else
        mb = n as MemberBinding;
      return mb;
    }
    protected virtual TypeNode GetQueryResultType(Node n){
      QualifiedIdentifier qualId = n as QualifiedIdentifier;
      if (qualId == null) return null;
      QueryAxis qAxis = qualId.Qualifier as QueryAxis;
      if (qAxis == null) return null;
      return qAxis.Type;
    }
    /// <summary>
    /// Called for completions.
    /// </summary>
    public virtual Declarations GetDeclarations(int line, int col, ParseReason reason){
      Scope scope;
      Node node;
      MemberList members = this.GetMembers(line, col, reason, out node, out scope);
      if (members == null) members = new MemberList(); // return empty list then.
      return new Declarations(members, this.helper, node, scope);
    }
    public virtual MemberList FilterByContext(MemberList originalList, int identContext) {
      if (originalList == null || identContext == IdentifierContexts.AllContext) return originalList;
      MemberList retMemberList = new MemberList();
      for (int i = 0; i < originalList.Count; ++i) {
        Member memb = originalList[i];
        if (!this.MemberSatisfies(memb, identContext)) continue;
        retMemberList.Add(memb);
      }
      return retMemberList;
    }
    public virtual bool MemberSatisfies(Member memb, int identContext) {
      return true;
    }
    //  MemberSelectExplicit =>
    //    if node != null then show rele vent stuff only
    //    else show default list
    //  MemberSelect =>
    //    if node != null then show relevent stuff only
    //    dont show anything
    public virtual MemberList GetMembers(int line, int col, ParseReason reason, out Node node, out Scope scope){
      int identContext;
      this.languageService.SearchForNodeAtPosition(line + 1, col + 1, out node, out scope, out identContext);
      if (identContext == IdentifierContexts.NullContext) return null;
      bool doingCompletion = reason == ParseReason.MemberSelect || reason == ParseReason.MemberSelectExplicit || reason == ParseReason.CompleteWord;
      MemberList retList = null;
      QualifiedIdentifier qi = node as QualifiedIdentifier;
      if (qi != null && qi.Identifier.UniqueIdKey == StandardIds.Ctor.UniqueIdKey && (qi.Qualifier is This || qi.Qualifier is Base) && reason == ParseReason.CompleteWord) {
        node = null;
        retList = this.GetMembers(line, col, node, scope);
      } else if ((node as Scope) == scope && scope != null) {
        retList = this.languageService.GetTypesNamespacesAndPrefixes(scope, false, false);
      } else if (node is AttributeNode) {
        retList = this.GetMembers(line, col, (AttributeNode)node, scope);
      } else if (node is ClassExpression) {
        retList = this.GetMembers(line, col, (ClassExpression)node, scope);
      } else if (node is InterfaceExpression) {
        InterfaceExpression ie = (InterfaceExpression)node;
        if (ie.Expression != null && ie.Expression is QualifiedIdentifier)
          retList = this.GetMembers(line, col, ie.Expression as QualifiedIdentifier, scope);
        else
          retList = this.GetMembers(line, col, (InterfaceExpression)node, scope);
      } else if (node is Construct) {
        retList = this.GetMembers(line, col, (Construct)node, scope, doingCompletion);
      } else if (node is UsedNamespace && (node.SourceContext.StartLine == line + 1)) {
        retList = this.languageService.GetNestedNamespaces(((UsedNamespace)node).Namespace, scope);
      } else if (node is AliasDefinition) {
        retList = this.GetMembers(line, col, (AliasDefinition)node, scope);
      } else if (node is Namespace) {
        Namespace ns = (Namespace)node;
        string name = ns.FullName;
        if (name != null && (name.Equals("global:") || name.Length == 0)) {
          Scope iterScope = scope;
          while (iterScope != null && iterScope.OuterScope != null)
            iterScope = iterScope.OuterScope;
          retList = this.languageService.GetNestedNamespacesAndTypes(Identifier.Empty, iterScope);
          goto returnList;
        }
        if (doingCompletion)
          retList = this.FillWithDefaultList(line, col, node, scope);
        else
          retList = this.GetMembers(line, col, ns, scope);
      } else if (node is QualifiedIdentifier) {
        retList = this.GetMembers(line, col, qi, scope);
      } else if (node is TemplateInstance) {
        retList = this.GetMembers(line, col, (TemplateInstance)node, scope);
      } else if (node is TypeExpression) {
        retList = this.GetMembers(line, col, (TypeExpression)node, scope);
      } else if (node is NameBinding) {
        retList = this.GetMembers(line, col, (NameBinding)node, scope, doingCompletion);
      } else if (node is Event || node is Method || node is Property) {
        retList = this.GetMembers(line, col, (Member)node, scope, doingCompletion);
      } else if (node is This) {
        retList = this.GetMembers(line, col, (This)node, scope, doingCompletion);
      } else if (node is Base) {
        retList = this.GetMembers(line, col, (Base)node, scope, doingCompletion);
      } else if (node is Identifier || node is Class || node is Interface) {
        retList = this.FillWithDefaultList(line, col, node, scope);
      }

      if (node == null && (reason == ParseReason.CompleteWord || reason == ParseReason.MemberSelectExplicit)) {
        retList = this.FillWithDefaultList(line, col, node, scope);
      } else if (reason == ParseReason.MethodTip) {
        Class cl = node as Class;
        if (cl != null && cl.IsAssignableTo(SystemTypes.Attribute)) {
          this.suppressAttributeSuffix = true;
          retList = new MemberList();
          MemberList memList = cl.GetConstructors();
          bool showInternal = this.MayAccessInternals(this.languageService.currentSymbolTable, cl);
          if (memList != null) {
            int n = memList.Count;
            for (int i = 0; i < n; ++i) {
              Member mem = memList[i];
              if (mem == null) continue;
              if (mem.IsCompilerControlled) continue;
              if (mem.IsPrivate) continue;
              if (mem.IsFamily || mem.IsFamilyAndAssembly) continue;
              if ((mem.IsAssembly || mem.IsFamilyOrAssembly) && !showInternal) continue;
              retList.Add(mem);
            }
          }
        }
      }

      if (retList != null && reason != ParseReason.MethodTip) {
        retList = this.FilterByContext(retList, identContext);
        this.languageService.AddReleventKeywords(retList, node, scope, identContext);
      }

returnList:
      return retList;
    }

    private MemberList FillWithDefaultList(int line, int col, Node node, Scope scope) {
      Scope contScope = scope;
      if (contScope == null) contScope = this.languageService.SearchForLeastEnclosingScope(line + 1, col + 1);
      MemberList retList = this.GetMembers(line, col, node, contScope);
      if (retList != null) {
        Identifier id = node as Identifier;
        if (id != null)
          retList.Add(id.Type);
      }
      return retList;
    }
    protected virtual MemberList GetMembers(int line, int col, This thisNode, Scope scope, bool doingCompletion) {
      if (thisNode == null || scope == null || !(thisNode.Type is Class)) return null;
      if (thisNode.IsCtorCall)
        return ((Class)thisNode.Type).GetConstructors();
      else
        return ((Class)thisNode.Type).DefaultMembers;
    }
    protected virtual MemberList GetMembers(int line, int col, Base baseNode, Scope scope, bool doingCompletion) {
      if (baseNode == null || scope == null || !(baseNode.Type is Class)) return null;
      if (baseNode.IsCtorCall)
        return ((Class)baseNode.Type).GetConstructors();
      else
        return ((Class)baseNode.Type).DefaultMembers;
    }
    protected virtual MemberList GetMembers(int line, int col, AttributeNode attrNode, Scope scope) {
      if (attrNode == null || scope == null) return null;
      Literal lit = attrNode.Constructor as Literal;
      if (lit != null && lit.Value is TypeNode) return new MemberList((TypeNode)lit.Value);
      return this.languageService.GetNamespacesAndAttributeTypes(scope);
    }
    protected virtual MemberList GetMembers(int line, int col, AliasDefinition aliasDef, Scope scope){
      if (aliasDef == null || scope == null) return null;
      //deal with case where the alias is being defined, e.g. "using sys = System."
      if (aliasDef.SourceContext.StartLine == line+1)
        return this.languageService.GetNestedNamespacesAndTypes(aliasDef.AliasedExpression as Identifier, scope);
      //deal with the case where the alias is being used, e.g. "sys::"
      MemberList members;
      if (aliasDef.AliasedNamespace != null && aliasDef.AliasedNamespace.Name != null)
        members = this.GetMembers(line, col, new QualifiedIdentifier(aliasDef.AliasedNamespace, Identifier.Empty, aliasDef.SourceContext), scope);
      else
        members = this.languageService.GetNestedNamespacesAndTypes(Identifier.Empty, scope, aliasDef.AliasedAssemblies);
      if (scope is AttributeScope) return members;
      if (aliasDef.RestrictToClassesAndInterfaces)
        members = this.GetClassesAndInterfacesAndNamespacesThatContainThem(members, null);
      else if (aliasDef.RestrictToInterfaces)
        members = this.GetInterfacesAndNamespacesThatContainThem(scope, members);
      return members;
    }
    protected virtual MemberList GetMembers(int line, int col, ClassExpression cExpr, Scope scope){
      if (cExpr == null) return null;
      MemberList members;
      QualifiedIdentifier qual = cExpr.Expression as QualifiedIdentifier;
      if (qual != null)
        members = this.GetMembers(line, col, qual, scope);
      else
        members = this.languageService.GetTypesNamespacesAndPrefixes(scope, false, false);
      if (members == null) return null;
      return this.GetClassesAndInterfacesAndNamespacesThatContainThem(members, cExpr.DeclaringType);
    }
    protected virtual MemberList GetClassesAndInterfacesAndNamespacesThatContainThem(MemberList members, TypeNode classToExclude){
      MemberList result = new MemberList(members.Count);
      for (int i = 0, n = members.Count; i < n; i++) {
        Member mem = members[i];
        if (mem is Class && classToExclude != null && mem.FullName == classToExclude.FullName) continue;
        if (mem is Interface || mem is Namespace)
          result.Add(mem); //TODO: show namespaces only if they have classes or interfaces or types with nested classes/interfaces.
        else{
          Class cl = mem as Class;
          if (cl == null || cl.IsSealed || cl == SystemTypes.Array || cl == SystemTypes.Delegate  || cl == SystemTypes.Enum ||
            cl == SystemTypes.MulticastDelegate || cl == SystemTypes.ValueType) continue;
          result.Add(mem);
        }
      }
      if (result.Count == 0) return null;
      return result;
    }
    protected virtual MemberList GetConstructibleTypesAndAndNamespacesThatContainThem(MemberList members, TypeNode referringType){
      MemberList result = new MemberList(members.Count);
      for (int i = 0, n = members.Count; i < n; i++) {
        Member mem = members[i];
        if (mem is Namespace)
          result.Add(mem); //TODO: show namespaces only if they have classes or interfaces or types with nested classes/interfaces.
        else{
          TypeNode t = mem as TypeNode;
          if (t == null || this.languageService.TypeHasNoVisibleConstructorsOrIsAbstract(t, referringType)) continue;
          result.Add(mem);
        }
      }
      if (result.Count == 0) return null;
      return result;
    }
    protected virtual MemberList GetMembers(int line, int col, InterfaceExpression iExpr, Scope scope) {
      MemberList members = this.languageService.GetTypesNamespacesAndPrefixes(scope, false, false);
      if (members == null) return null;
      return this.GetInterfacesAndNamespacesThatContainThem(scope, members);
    }
    protected virtual MemberList GetInterfacesAndNamespacesThatContainThem(Scope scope, MemberList members){
      MemberList result = new MemberList(members.Count);
      for (int i = 0, n = members.Count; i < n; i++) {
        Member mem = members[i];
        if (mem is Interface || mem is Namespace || (mem is TypeNode && this.HasNestedInterface((TypeNode)mem, scope)))
          result.Add(mem); //TODO: show namespaces only if they have interfaces or types with nested interfaces
      }
      if (result.Count == 0) return null;
      return result;
    }
    private bool HasNestedInterface(TypeNode typeNode, Scope scope){
      if (typeNode == null) return false;
      MemberList members = typeNode.Members;
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
        TypeNode nt = members[i] as TypeNode;
        if (nt == null) continue;
        if (nt.IsPrivate) continue; //TODO: check that other visibilities are visible from given scope
        if (nt is Interface) return true;
        if (this.HasNestedInterface(nt, scope)) return true;
      }
      return false;
    }
    protected virtual MemberList GetMembers(int line, int col, Namespace nspace, Scope scope){
      if (nspace == null || nspace.Name == null || scope == null) return null;
      string nsname = nspace.Name.ToString();
      if (nsname == null || nsname.Length == 0) return null;
      if (nsname[nsname.Length-1] == ':') nsname = nsname.Substring(0, nsname.Length-1) + ".";
      return this.languageService.GetNestedNamespacesAndTypes(Identifier.For(nsname), scope);
    }
    protected virtual MemberList GetMembers(int line, int col, Construct cons, Scope scope, bool doingCompletion){
      if (cons == null) return null;
      MemberBinding mb = cons.Constructor as MemberBinding;
      if (mb != null && !doingCompletion) {
        if (mb.BoundMember is InstanceInitializer && mb.BoundMember.DeclaringType != null)
          return new MemberList(mb.BoundMember.DeclaringType);
        else if (mb.BoundMember is TypeNode) {
          return new MemberList(mb.BoundMember);
        }
      }
      MemberList result = null;
      QualifiedIdentifier qual = cons.Constructor as QualifiedIdentifier;
      if (qual != null){
        if (qual.Qualifier is Literal && ((Literal)qual.Qualifier).Value is TypeNode)
          return this.languageService.GetConstructibleNestedTypes((TypeNode)((Literal)qual.Qualifier).Value, this.GetReferringType(scope));
        result = this.GetMembers(line, col, qual, scope);
        if (result != null)
          return this.GetConstructibleTypesAndAndNamespacesThatContainThem(result, this.GetReferringType(scope));
      }
      if (scope != null) result = this.languageService.GetTypesNamespacesAndPrefixes(scope, true, false);
      if (result != null){
        if (cons.Type != null && !(cons.Type is Reference)) {
          TypeNode type = cons.Type;
          while (type != null) {
            if (type is ArrayType)
              type = ((ArrayType)type).ElementType;
            else if (type is Pointer)
              type = ((Pointer)type).ElementType;
            else
              break;
          }
          if (type != null && (type != cons.Type || !this.languageService.TypeHasNoVisibleConstructorsOrIsAbstract(type, this.GetReferringType(scope))))
            result.Add(type);
          else
            result.Add(null); //  Always add something to the end of the list in the case of constructor. We will remove this later when creating declaration.
        }
        if (result.Count > 0) return result;
      }
      return null;
    }
    protected virtual MemberList FilterOutIncassessibleMembers(MemberList members, Scope scope){
      if (members == null) return null;
      TypeNode referringType = GetReferringType(scope);
      MemberList result = new MemberList(members.Count);
      for (int i = 0, n = members.Count; i < n; i++){
        Member member = members[i];
        if (member == null) continue;
        if (member.IsPrivate && referringType != member.DeclaringType) continue;
        if ((member.IsFamily || member.IsFamilyAndAssembly) && referringType.IsAssignableTo(member.DeclaringType)) continue;
        if (member.IsAssembly && !this.MayAccessInternals(referringType, member.DeclaringType)) continue;
        if (member.IsFamilyOrAssembly && !referringType.IsAssignableTo(member.DeclaringType) && !this.MayAccessInternals(referringType, member.DeclaringType)) continue;
        result.Add(member);
      }
      return result;
    }
    protected virtual TypeNode GetReferringType(Scope scope){
      TypeNode referringType = null;
      while (scope != null) {
        TypeScope tScope = scope as TypeScope;
        if (tScope != null) {
          referringType = tScope.Type;
          break;
        }
        scope = scope.OuterScope;
      }
      return referringType;
    }
    protected virtual MemberList GetMembers(int line, int col, Node node, Scope scope){
      if (node is MemberBinding) return null;
      if (scope != null) return this.languageService.GetVisibleNames(scope);
      return null;
    }
    protected virtual MemberList GetMembers(int line, int col, NameBinding nameBinding, Scope scope, bool doingCompletion) {
      if (nameBinding == null) return null;
      if (nameBinding.BoundMembers != null && !doingCompletion) 
        return this.FilterOutIncassessibleMembers(nameBinding.BoundMembers, scope);
      if (scope != null) return this.languageService.GetVisibleNames(scope);
      return null;
    }
    protected virtual MemberList GetMembers(int line, int col, Member memberNode, Scope scope, bool doingCompletion) {
      if (memberNode == null) return null;
      Interface i = null;
      TypeNode type = memberNode.DeclaringType;
      NodeType toShow = NodeType.Method;
      Event e = memberNode as Event;
      if (e != null){
        if (e.ImplementedTypes == null || e.ImplementedTypes.Count == 0) return null;
        i = e.ImplementedTypes[0] as Interface;
        toShow = NodeType.Event;
      }
      Property p = memberNode as Property;
      if(p != null){
        if (p.ImplementedTypes == null || p.ImplementedTypes.Count == 0) return null;
        i = p.ImplementedTypes[0] as Interface;
        toShow = NodeType.Method;
      }
      Method m = memberNode as Method;
      if(m != null){
        if (m.ImplementedTypes == null || m.ImplementedTypes.Count == 0) return null;
        i = m.ImplementedTypes[0] as Interface;
        toShow = NodeType.Method;
      }
      if (i == null) return null;

      TypeNode referringType = null;
      while (scope != null) {
        TypeScope tScope = scope as TypeScope;
        if (tScope != null) {
          referringType = tScope.Type;
          break;
        }
        scope = scope.OuterScope;
      }
      bool showInternal = this.MayAccessInternals(referringType, type);
      MemberList result = new MemberList();
      this.GetMembers(i, result, showInternal, toShow);
      return result;
    }
    protected virtual MemberList GetMembers(int line, int col, QualifiedIdentifier qualId, Scope scope) {
      if (qualId == null) return null;
      bool staticMembersWanted = true;
      bool allMembersWanted = false;
      Node n = qualId.Qualifier;
      if (n == null || n.IsErroneous) return null;
      if (n is ConstructArray) return null;
      TypeNode type = null;
      MemberBinding mb = this.GetMemberBinding(n);
      if (mb != null){
        staticMembersWanted = false;
        Member mem = mb.BoundMember;
        if (mem is Field)
          type = ((Field)mem).Type;
        else if (mem is Property)
          type = ((Property)mem).Type;
        else if (mem is InstanceInitializer)
          type = mem.DeclaringType;
        //TODO: other stuff? Events?
        allMembersWanted = (mem != null && type != null && mem.Name != null && type.Name != null && mem.Name.UniqueIdKey == type.Name.UniqueIdKey);
      }else{
        if (n is Identifier || n is NameBinding || n is QualifiedIdentifier){
          NameBinding nb = n as NameBinding;
          if (nb != null) n = nb.Identifier;
          return this.languageService.GetNestedNamespacesAndTypes(this.ConvertToNamespaceId((Expression)n), scope);
        }
        type = n as TypeNode;
        if (type == null){
          if (n is Literal){
            if (Literal.IsNullLiteral((Literal)n)) return null;
            type = ((Literal)n).Value as TypeNode;
          }
          Expression expr = n as Expression;
          if (type == null && expr != null){
            if (n is Base && n.SourceContext.Document == null) return null;
            staticMembersWanted = false;
            type = expr.Type;
            MethodCall mc = expr as MethodCall;
            bool useEnum = false;
            if (mc != null && mc.Callee is MemberBinding) {
              Member mem = ((MemberBinding)mc.Callee).BoundMember;
              Method meth = mem as Method;
              if (meth != null) {
                Property p = meth.DeclaringMember as Property;
                useEnum = p == null;
                allMembersWanted = p != null && type != null && p.Name != null && type.Name != null && p.Name.UniqueIdKey == type.Name.UniqueIdKey;
              }
            }
            if (type is EnumNode && useEnum) type = SystemTypes.Enum;
          }
        }
      }
      TypeAlias ta = type as TypeAlias;
      while (ta != null) {
        type = ta.AliasedType;
        ta = type as TypeAlias;
      }
      type = TypeNode.StripModifiers(type);
      if (type is Reference) type = ((Reference)type).ElementType;
      if (type == null || type == SystemTypes.Void) return null;
      TypeNode referringType = null;
      while (scope != null){
        TypeScope tScope = scope as TypeScope;
        if (tScope != null){
          referringType = tScope.Type;
          break;
        }
        scope = scope.OuterScope;
      }
      Debug.Assert(referringType != SystemTypes.Object);
      MemberList result = new MemberList();
      bool showPrivate = referringType == type;
      bool showFamily = referringType == null || type.IsAssignableTo(referringType) || n is Base;
      bool showInternal = this.MayAccessInternals(referringType, type);
      this.GetMembers(type, result, staticMembersWanted, allMembersWanted, showPrivate, showFamily, showInternal);
      return result;
    }
    protected virtual Identifier ConvertToNamespaceId(Expression expression){
      Identifier id = expression as Identifier;
      if (id != null) return Identifier.For(id.Name+".");
      NameBinding nb = expression as NameBinding;
      if (nb != null) return Identifier.For(nb.Identifier.Name + ".");
      QualifiedIdentifier qualId = expression as QualifiedIdentifier;
      if (qualId != null) return Identifier.For(this.ConvertToNamespaceId(qualId.Qualifier)+qualId.Identifier.Name+".");
      return Identifier.Empty;
    }
    protected virtual MemberList GetMembers(int line, int col, TemplateInstance templateInstance, Scope scope){
      if (templateInstance == null) return null;
      return this.FilterOutIncassessibleMembers(templateInstance.BoundMembers, scope);
    }
    protected virtual MemberList GetMembers(int line, int col, TypeExpression tExpr, Scope scope) {
      if (tExpr == null) return null;
      MemberList members;
      QualifiedIdentifier qual = tExpr.Expression as QualifiedIdentifier;
      if (qual != null)
        members = this.GetMembers(line, col, qual, scope);
      else
        members = this.languageService.GetTypesNamespacesAndPrefixes(scope, true, false);
      if (members == null) return null;
      return this.GetTypesAndNamespacesThatContainThem(members);
    }
    protected virtual MemberList GetTypesAndNamespacesThatContainThem(MemberList members) {
      MemberList result = new MemberList(members.Count);
      for (int i = 0, n = members.Count; i < n; i++) {
        Member mem = members[i];
        if (mem is TypeNode || mem is Namespace)
          result.Add(mem);
      }
      if (result.Count == 0) return null;
      return result;
    }
    protected virtual bool MayAccessInternals(TypeNode referringType, TypeNode referredToType) {
      if (referringType == null || referredToType == null) return false;
      Module referringModule = referringType.DeclaringModule;
      return this.MayAccessInternals(referringModule, referredToType);
    }
    protected virtual bool MayAccessInternals(Module referringModule, TypeNode referredToType) {
      if (referringModule == null || referredToType == null) return false;
      Module referredToModule = referredToType.DeclaringModule;
      if (referredToModule == null) return false;
      if (referringModule == referredToModule) return true;
      AssemblyNode referringAssembly = referringModule.ContainingAssembly;
      AssemblyNode referredToAssembly = referredToModule.ContainingAssembly;
      if (referringAssembly == null) referringAssembly = referringModule as AssemblyNode;
      if (referredToAssembly == null) referredToAssembly = referredToModule as AssemblyNode;
      if (referringAssembly == null) return referringModule.ContainsModule(referredToModule);
      if (referringAssembly == referredToAssembly) return true;
      if (referringAssembly.ContainsModule(referringModule)) return true;
      return referringAssembly.MayAccessInternalTypesOf(referredToAssembly);
    }
    protected virtual void GetMembers(Interface type, MemberList memberList, bool showInternal, NodeType memberKind) {
      if (type == null) return;
      MemberList typeMembers = type.Members;
      for (int i = 0, k = typeMembers == null ? 0 : typeMembers.Count; i < k; i++) {
        Member mem = typeMembers[i];
        if (mem == null) continue;
        if (mem.IsCompilerControlled) continue;
        if (mem.IsPrivate) continue;
        if ((mem.IsAssembly || mem.IsFamilyOrAssembly) && !showInternal) continue;
        if (mem.IsSpecialName && !(mem is InstanceInitializer)) continue;
        if (this.SuppressBecauseItIsADefaultMember(mem, type.DefaultMembers)) continue;
        if (mem.IsAnonymous || this.MemberIsOverriddenOrHidden(mem, memberList)) continue;
        if ((memberKind == NodeType.Event && mem is Event) || (memberKind == NodeType.Method && (mem is Property || mem is Method)))
          memberList.Add(mem);
      }
      for (int i = 0, k = type.Interfaces == null ? 0 : type.Interfaces.Count; i < k; i++) {
        this.GetMembers(type.Interfaces[i], memberList, showInternal, memberKind);
      }
    }
    protected virtual void GetMembers(TypeNode type, MemberList members, bool onlyStaticMembersWanted, bool allMembersWanted, bool showPrivate, bool showFamily, bool showInternal) {
      if (type == null || members == null) return;
      if (type is ArrayType){
        this.GetMembers(SystemTypes.Array, members, onlyStaticMembersWanted, allMembersWanted, showPrivate, showFamily, showInternal);
        return;
      }
      TypeUnion tu = type as TypeUnion;
      if (tu != null){
        TypeNodeList tlist = tu.Types;
        for (int i = 0, n = (tlist == null ? 0 : tlist.Count); i < n; i++){
          TypeNode t = tlist[i] as TypeNode;
          if (t == null) continue;
          this.GetMembers(t, members, onlyStaticMembersWanted, allMembersWanted, showPrivate, showFamily, showInternal);
        }
        return;
      }
      TypeAlias ta = type as TypeAlias;
      if (ta != null){
        this.GetMembers(ta.AliasedType, members, onlyStaticMembersWanted, allMembersWanted, showPrivate, showFamily, showInternal);
        return;
      }
      MemberList typeMembers = type.Members;
      for (int i = 0, k = typeMembers == null ? 0 : typeMembers.Count; i < k; i++){
        Member mem = typeMembers[i];
        if (mem == null) continue;
        if (onlyStaticMembersWanted != mem.IsStatic && !allMembersWanted) continue;
        if (mem.IsCompilerControlled) continue;
        if (mem.IsPrivate && !showPrivate) continue;
        if ((mem.IsFamily || mem.IsFamilyAndAssembly) && !showFamily) continue;
        if ((mem.IsAssembly || mem.IsFamilyOrAssembly) && !showInternal) continue;
        if (mem.IsSpecialName && !(mem is InstanceInitializer)) continue;
        if (this.SuppressBecauseItIsADefaultMember(mem, type.DefaultMembers)) continue;
        if (this.IsTransparent(mem)){
          TypeNode mt = this.GetMemberType(mem);
          TypeNode rt = this.GetRootType(mt);
          this.GetMembers(rt, members, onlyStaticMembersWanted, allMembersWanted, false, false, false);
        }else if (!mem.IsAnonymous && !this.MemberIsOverriddenOrHidden(mem, members)){
          members.Add(mem);
        }
      }
      if (type.BaseType != null && !(type is EnumNode && onlyStaticMembersWanted)){
        this.GetMembers(type.BaseType, members, onlyStaticMembersWanted, allMembersWanted, false, showFamily, showInternal && this.MayAccessInternals(type, type.BaseType));
      }
      if (type is Interface){
        for (int i = 0, k = type.Interfaces == null ? 0 : type.Interfaces.Count; i < k; i++){
          this.GetMembers(type.Interfaces[i], members, allMembersWanted, false, false, false, false);
        }
        this.GetMembers(SystemTypes.Object, members, onlyStaticMembersWanted, allMembersWanted, false, showFamily, showInternal);
      }
    }
    protected virtual bool SuppressBecauseItIsADefaultMember(Member m, MemberList defaultMemberList) {
      for (int i = 0, n = defaultMemberList == null ? 0 : defaultMemberList.Count; i < n; i++) {
        if (m == defaultMemberList[i]) return true;
      }
      return false;
    }
    protected virtual bool MemberIsOverriddenOrHidden(Member mem, MemberList members) {
      if (mem == null || mem.Name == null || members == null){Debug.Assert(false); return true;}
      for (int i = 0, n = members.Count; i < n; i++){
        Member m = members[i];
        if (m == null || m.Name == null || m.Name.UniqueIdKey != mem.Name.UniqueIdKey) continue;
        Method meth1 = m as Method;
        Method meth2 = mem as Method;
        if (meth1 == null || meth2 == null) return true;
        if (meth1.ParametersMatchStructurallyIncludingOutFlag(meth2.Parameters) && meth1.TypeParameterCountsMatch(meth2)) return true;
      }
      if (mem is Method && mem.DeclaringType == SystemTypes.Object && mem.Name != null && mem.Name.UniqueIdKey == StandardIds.Finalize.UniqueIdKey)
        return true;
      return false;
    }
    public virtual TypeNode GetMemberType(Member member){
      Field f = member as Field;
      if (f != null) return f.Type;
      Property p = member as Property;
      if (p != null) return p.Type;
      Method mth = member as Method;
      if (mth != null) return mth.ReturnType;
      return null;
    }
    protected virtual TypeNode GetRootType(TypeNode type){
      return type;
    }
    protected virtual bool IsTransparent(Member member) {
      return false;
    }
    protected virtual Overloads GetConstructors(int line, int col, TypeNode type){
      Node node;
      Scope scope;
      int identContext;
      this.languageService.SearchForNodeAtPosition(line+1, col+1, out node, out scope, out identContext);
      TypeNode referringType = null;
      Module referringModule = null;
      while (scope != null){
        TypeScope tScope = scope as TypeScope;
        if (tScope != null){
          referringType = tScope.Type;
          if (referringType != null){
            referringModule = referringType.DeclaringModule;
            break;
          }
        }
        NamespaceScope nScope = scope as NamespaceScope;
        if (nScope != null){
          referringModule = nScope.AssociatedModule;
          break;
        }
        scope = scope.OuterScope;
      }
      bool showPrivate = referringType == type;
      bool showFamily = referringType != null && referringType.IsAssignableTo(type);
      bool showInternal = this.MayAccessInternals(referringType, type) || this.MayAccessInternals(referringModule, type);
      Member selectedMember = this.GetMember(line, col);
      MemberList members = type == null ? null : type.GetConstructors();
      int positionOfSelectedMember = 0;
      MemberList filteredMembers = new MemberList();
      if (type != null && type.IsValueType){
        //Add dummy default constructor
        InstanceInitializer cons = new InstanceInitializer(type, null, null, null);
        cons.Flags |= MethodFlags.Public;
        filteredMembers.Add(cons);
      }
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
        Method meth = members[i] as Method;
        if (meth == null) continue;
        if (meth.IsCompilerControlled) continue;
        if (meth.IsPrivate && !showPrivate) continue;
        if ((meth.IsFamily || meth.IsFamilyAndAssembly) && !showFamily) continue;
        if ((meth.IsAssembly || meth.IsFamilyOrAssembly) && !showInternal) continue;
        if (meth == selectedMember) positionOfSelectedMember = filteredMembers.Count;
        filteredMembers.Add(meth);
      }
      if (filteredMembers.Count == 0) return null;
      return new Overloads(filteredMembers, scope, positionOfSelectedMember, this.helper, OverloadKind.Constructors);
    }
    protected virtual Overloads GetDefaultIndexedProperties(int line, int col, Method selectedPropertyGetter, TypeNode declaringType){
      if (declaringType == null) return null;
      Node node;
      Scope scope;
      int identContext;
      this.languageService.SearchForNodeAtPosition(line+1, col+1, out node, out scope, out identContext);
      int positionOfSelectedProperty = 0;
      MemberList properties = new MemberList();
      while (declaringType != null){
        MemberList defMems = declaringType.DefaultMembers;
        defMems = this.FilterOutIncassessibleMembers(defMems, scope);
        for (int i = 0, n = defMems == null ? 0 : defMems.Count; i < n; i++){
          Property prop = defMems[i] as Property;
          if (prop == null) continue;
          if (prop.Getter == selectedPropertyGetter) positionOfSelectedProperty = properties.Count;
          for (int j = 0, m = properties.Count; j < m; j++){
            Property p = (Property)properties[j];
            if (p.ParametersMatch(prop.Parameters)){
              prop = null; break;
            }
          }
          if (prop == null) continue;
          properties.Add(prop);
        }
        declaringType = declaringType.BaseType;
      }
      return new Overloads(properties, scope, positionOfSelectedProperty, this.helper, OverloadKind.Indexer);
    }
    /// <summary>
    /// Called for parameter help.
    /// </summary>
    public virtual Overloads GetGenericMethods(int line, int col, Expression name){
      if (name == null) return null;
      int key = name is Identifier? ((Identifier)name).UniqueIdKey : -1;
      Scope scope;
      Node node;
      Member selectedMember = this.GetMember(line, col);
      MemberList members = this.GetMembers(line, col, ParseReason.MethodTip, out node, out scope);
      int positionOfSelectedMember = 0;
      MemberList filteredMembers = new MemberList();
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
        Method meth = members[i] as Method;
        if (meth == null) continue;
        if (meth.Template != null) meth = meth.Template;
        if (meth.Name == null) continue;
        if (meth.Name == null || meth.Name.UniqueIdKey != key) continue;
        if (meth.TemplateParameters == null || meth.TemplateParameters.Count == 0) continue;
        if (meth == selectedMember) positionOfSelectedMember = filteredMembers.Count;
        filteredMembers.Add(meth);
      }
      if (filteredMembers.Count == 0) return null;
      return new GenericMethodOverloads(filteredMembers, scope, positionOfSelectedMember, this.helper);
    }
    /// <summary>
    /// Called for parameter help.
    /// </summary>
    public virtual Overloads GetMethods(int line, int col, Expression name) {
      if (name == null) return null;
      string nameStr = null;
      Identifier id = name as Identifier;
      int key = -1;
      if (id != null){
        key = id.UniqueIdKey;
        nameStr = id.Name;
      }
      Scope scope;
      Node node;
      Member selectedMember = this.GetMember(line, col);
      Method selectedMethod = selectedMember as Method;
      MemberList members = this.GetMembers(line, col, ParseReason.MethodTip, out node, out scope);
      int positionOfSelectedMember = 0;
      MemberList filteredMembers = new MemberList();
      OverloadKind olk = (key == StandardIds.Ctor.UniqueIdKey) ? OverloadKind.Constructors : OverloadKind.Methods;
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
        Method meth = members[i] as Method;
        if (meth == null){
          TypeNode t = members[i] as TypeNode;
          if (t != null){
            Identifier tname = Identifier.For(t.GetUnmangledNameWithoutTypeParameters());
            if (tname == null) continue;
            if (tname.UniqueIdKey != key && selectedMember != (object)t) continue;
            return this.GetConstructors(line, col, t);
          }
          Event ev = members[i] as Event;
          if (ev != null){
            t = ev.HandlerType;
            if (t == null || t.Name == null || t.Name.UniqueIdKey != key) continue;
            MemberList invokers = t.GetMembersNamed(StandardIds.Invoke);
            if (invokers != null && invokers.Count > 0)
              meth = invokers[0] as Method;
          }
          Field f = members[i] as Field;
          if (f != null){
            if (f.Name == null || f.Name.UniqueIdKey != key) continue;
            t = f.Type;
            if (t is DelegateNode){
              MemberList invokers = t.GetMembersNamed(StandardIds.Invoke);
              if (invokers != null && invokers.Count > 0)
                meth = invokers[0] as Method;
            }else if (t is ArrayType)
              return new Overloads(new MemberList(t), scope, 0, this.helper, OverloadKind.Indexer);
            else
              return this.GetDefaultIndexedProperties(line, col, selectedMember as Method, t);
          }
          Property p = members[i] as Property;
          if (p != null && p.DeclaringType.DefaultMembers.Contains(p)) {
            return new Overloads(new MemberList(p), scope, 0, this.helper, OverloadKind.Property);
          }
          if (meth == null) continue;
          key = StandardIds.Invoke.UniqueIdKey;
        }
        Identifier methName = meth.Name;
        if (meth.Template != null) methName = meth.Template.Name;
        if (methName == null || methName.UniqueIdKey != key) {
          if (meth is InstanceInitializer && meth.DeclaringType != null && meth.DeclaringType.IsAssignableTo(SystemTypes.Attribute)){
            olk = OverloadKind.AttributeConstructors;
            if (!(this.suppressAttributeSuffix && meth.DeclaringType.Name != null && meth.DeclaringType.Name.Name != null &&
              meth.DeclaringType.Name.Name.EndsWith("Attribute")
              && meth.DeclaringType.Name.Name.Substring(0, meth.DeclaringType.Name.Name.Length - 9).Equals(nameStr)))
              continue;
          } else
            continue;
        }
        if (meth == selectedMethod){
          positionOfSelectedMember = filteredMembers.Count;
        }else if (selectedMethod != null && selectedMethod.Template == meth){
          meth = selectedMethod; positionOfSelectedMember = filteredMembers.Count;
        }
        filteredMembers.Add(meth);
      }
      if (filteredMembers.Count == 0) return null;
      if (key == StandardIds.Ctor.UniqueIdKey) filteredMembers = this.RemoveBaseClassConstructors(filteredMembers);
      return new Overloads(filteredMembers, scope, positionOfSelectedMember, this.helper, olk);
    }
    protected virtual MemberList RemoveBaseClassConstructors(MemberList constructors){
      MemberList resultList = new MemberList();
      TypeNode mostDerivedType = SystemTypes.Object;
      for (int i = 0, n = constructors == null ? 0 : constructors.Count; i < n; i++){
        InstanceInitializer cons = constructors[i] as InstanceInitializer;
        if (cons == null || cons.DeclaringType == null) continue;
        if (cons.DeclaringType.IsAssignableTo(mostDerivedType)){
          if (mostDerivedType != cons.DeclaringType){
            mostDerivedType = cons.DeclaringType;
            resultList = new MemberList();
          }
          resultList.Add(cons);
        }
      }
      return resultList;
    }
    /// <summary>
    /// Called for parameter help.
    /// </summary>
    public virtual Overloads GetTypes(int line, int col, Expression name) {
      if (name == null) return null;
      int key = name is Identifier? ((Identifier)name).UniqueIdKey : -1;
      string strName = name is Identifier ? ((Identifier)name).Name : null;
      Node node;
      Scope scope;
      int identContext;
      this.languageService.SearchForNodeAtPosition(line + 1, col + 1, out node, out scope, out identContext);
      TypeNode selectedType = null;
      Construct cons = node as Construct;
      if (cons != null)
        selectedType = cons.Type;
      else{
        selectedType = node as TypeNode;
        if (selectedType == null) return this.GetGenericMethods(line, col, name);
      }
      if (selectedType == null || scope == null) return null;
      MemberList members = this.languageService.GetTypesNamespacesAndPrefixes(scope, false, true);
      int positionOfSelectedMember = 0;
      MemberList filteredMembers = new MemberList();
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
        TypeNode t = members[i] as TypeNode;
        if (t == null) continue;
        if (t.GetUnmangledNameWithoutTypeParameters() != strName) continue;
        if (t.TemplateParameters == null || t.TemplateParameters.Count == 0) continue;
        if (t == selectedType) positionOfSelectedMember = filteredMembers.Count;
        filteredMembers.Add(t);
      }
      if (filteredMembers.Count == 0) return null;
      return new Overloads(filteredMembers, scope, positionOfSelectedMember, this.helper, OverloadKind.GenericTypes);
    }
  }
  public class Declarations{
    public Member[] members;
    public int[] overloads;
    public AuthoringHelper helper;
    public string displayTextForLastGetBestMatch;
    public bool diplayTextMayDifferFromInsertionText;
    public int initialMatch = -1;
    public Node node;
    public Scope scope;

    public Declarations(MemberList memberList, AuthoringHelper helper, Node node, Scope scope){
      this.node = node;
      this.scope = scope;
      this.displayTextForLastGetBestMatch = "";
      this.helper = helper;
      if (memberList == null || memberList.Count == 0) {
        this.members = new Member[0];
        this.helper = helper;
        return;
      }
      Identifier tName = null;
      TypeNode t = memberList[memberList.Count - 1] as TypeNode;
      if (t != null)
        tName = (t.Template == null) ? t.Name : t.Template.Name;
      Member lastMemb = memberList[memberList.Count - 1];
      if (lastMemb == null)
        memberList.RemoveAt(memberList.Count - 1);
      Member[] members = memberList.ToArray();
      Array.Sort(members, this.GetMemberComparer(helper.culture)); //REVIEW: perhaps get it from helper?
      MemberList memberList1 = new MemberList();
      this.overloads = new int[members.Length];
      String memberName = null;
      bool doneAddingPreselection = !((node is Construct) || (node is Identifier)) || lastMemb == null;
      int j = 0;
      for (int i = 0; i < members.Length;){
        Member mem = members[i];
        if (mem == null){ i++; continue;}
        if (mem.IsSpecialName || mem.FilterPriority == System.ComponentModel.EditorBrowsableState.Never){i++; continue;}
        memberName = helper.GetMemberName(mem);
        if (memberName == null || memberName.Length == 0){i++; continue;}
        if (!doneAddingPreselection && mem.Name != null && tName != null && mem.Name.UniqueIdKey == tName.UniqueIdKey) {
          this.initialMatch = memberList1.Count;
          if (t != null && t.Template != null) { // t is implicitly not null since tName is not null but it's safer to check anyway.
            memberList1.Add(t);
            doneAddingPreselection = true;
            if (t == (mem as TypeNode)) { i++; j++; continue; }
          }
        } else if (t != null && t == (mem as TypeNode) && t.Template != null) { // TODO: Perhaps check doneAddingPreselection here as well?
          if (this.initialMatch >= 0) { i++; continue; }
          this.initialMatch = memberList1.Count;
          doneAddingPreselection = true;
        } else if (!doneAddingPreselection && lastMemb == mem) {
          if (this.initialMatch >= 0) { i++; continue; }
          this.initialMatch = memberList1.Count;
          doneAddingPreselection = true;
        }
        memberList1.Add(mem);
        i++;
        while (i < members.Length && helper.GetMemberName(members[i]) == memberName){
          if (t != (members[i] as TypeNode) || (t == null && members[i] != null)) this.overloads[j]++;
          i++;
        }
        j++;
      }
      members = memberList1.ToArray();
      this.members = members;
    }
    public IComparer GetMemberComparer(CultureInfo culture){
      return new MemberComparer();
    }
    private class MemberComparer : IComparer{
      public int Compare(Object a, Object b){
        TypeNode ta = a as TypeNode;
        TypeNode tb = b as TypeNode;
        string sa = ta == null ? ((Member)a).Name.Name : ta.GetUnmangledNameWithoutTypeParameters();
        string sb = tb == null ? ((Member)b).Name.Name : tb.GetUnmangledNameWithoutTypeParameters();
        int result = string.Compare(sa, sb, false, CultureInfo.InvariantCulture);
        if (result == 0 && ta != null && tb != null) {
          if ((ta.TemplateParameters == null || ta.TemplateParameters.Count == 0) && tb.TemplateParameters != null && tb.TemplateParameters.Count > 0)
            return -1;
          if ((tb.TemplateParameters == null || tb.TemplateParameters.Count == 0) && ta.TemplateParameters != null && ta.TemplateParameters.Count > 0)
            return 1;
        }
        if (result == 0 && a is Method && b is Method) {
          Method ma = (Method)a;
          Method mb = (Method)b;
          if ((ma.TemplateParameters == null || ma.TemplateParameters.Count == 0) && mb.TemplateParameters != null && mb.TemplateParameters.Count > 0)
            return -1;
          if ((mb.TemplateParameters == null || mb.TemplateParameters.Count == 0) && ma.TemplateParameters != null && ma.TemplateParameters.Count > 0)
            return 1;
        }
        return result;
      }
    }
    public virtual int GetCount(){
      return this.members.Length;
    }
    public virtual void GetBestMatch(string text, out int index, out bool uniqueMatch){
      if (this.initialMatch >= 0){
        index = this.initialMatch;
        uniqueMatch = false;
        this.initialMatch = -1;
        return;
      }
      index = 0;
      uniqueMatch = false;
      this.displayTextForLastGetBestMatch = "";
      if (text != null){
        int len = text.Length;
        index = -1;
        for (int i = 0; i < this.members.Length; i++){
          if (string.Compare(this.GetDisplayText(this.members[i]), 0, text, 0, len, true) == 0){
            if (index == -1){
              index = i;
              uniqueMatch = true;
            }else
              uniqueMatch = false;
          }
        }
        if (!uniqueMatch){
          int index2 = -1;
          for (int i = 0; i < this.members.Length; i++){
            if (string.Compare(this.GetDisplayText(this.members[i]), 0, text, 0, len, false) == 0){
              if (index2 == -1){
                index2 = i;
                uniqueMatch = true;
              }else
                uniqueMatch = false;
            }
          }
          if (index2 != -1) index = index2;
        }
        if (index != -1) return;
      }
      if (text == null || text.Length == 0){
        //TODO: provide an extensibility point that can be used to select the most recently used member
        //TODO: find out what algorithm C# uses.
        uniqueMatch = false;
        index = 0;
        // no match found - return S_FALSE
        COMException ce = new COMException("", unchecked((int)0x00000001));
        throw ce;
      }
      //Get here if text is not a prefix to any of the members' display texts
      this.displayTextForLastGetBestMatch = text;
      index = this.members.Length;
      uniqueMatch = true;
      return;
    }
    protected virtual string GetDisplayText(Member m){
      TypeNode t = m as TypeNode;
      if (t != null) {
        if (this.diplayTextMayDifferFromInsertionText || this.helper.SuppressAttributeSuffix)
          return this.helper.GetMemberName(t);
        else if (t.TemplateParameters != null && t.TemplateParameters.Count > 0)
          return t.GetUnmangledNameWithoutTypeParameters() + "<>";
        else if (t.Template != null && (t.TemplateArguments == null || t.TemplateArguments.Count == 0))
          return this.GetDisplayText(t.Template);
        else
          return this.helper.GetMemberName(t);
      }
      Method meth = m as Method;
      if (meth != null) {
        if ((meth.TemplateParameters != null && meth.TemplateParameters.Count > 0) || (meth.TemplateArguments != null && meth.TemplateArguments.Count > 0))
          return meth.GetUnmangledNameWithoutTypeParameters(true) + "<>";
        return meth.GetUnmangledNameWithoutTypeParameters(true);
      }
      if (m.Name == null) return " ";
      return m.Name.ToString();
    }
    public virtual string GetDisplayText(int index){
      if (index == this.members.Length) return this.displayTextForLastGetBestMatch;
      string name = this.GetDisplayText(this.members[index]);
      if (name == null) name = " ";
      return name;
    }   
    public virtual string GetDescription(int index){
      if (index == this.members.Length) return " ";
      return this.helper.GetDescription(this.members[index], this.overloads[index]);
    }
    protected virtual string GetInsertionText(Member m) {
      TypeNode t = m as TypeNode;
      if (t != null && t.TemplateParameters != null && t.TemplateParameters.Count > 0)
        return t.GetUnmangledNameWithoutTypeParameters();
      Method meth = m as Method;
      if (meth != null)
        return meth.GetUnmangledNameWithoutTypeParameters(true);
      return this.GetDisplayText(m);
    }
    public virtual string GetInsertionText(int index){
      if (index == this.members.Length) return this.displayTextForLastGetBestMatch;
      string name = this.GetInsertionText(this.members[index]);
      if (name == null) name = " ";
      return name;
    }
    public virtual Member GetMember(int index){
      if (index == this.members.Length) index = 0;
      return this.members[index];
    }
    public virtual bool IsCommitChar(string textSoFar, char commitChar){
      // if the char is in the list of given member names then obviously it
      // is not a commit char.
      int i = (textSoFar == null) ? 0 : textSoFar.Length;
      for (int j = 0, n = this.members.Length; j < n; j++){
        Member m = this.members[j];
        string name = m.Name.ToString();
        if (name.Length > i+1 && name[i] == commitChar){
          if (i == 0 || String.Compare(name.Substring(0,i), textSoFar, true, this.helper.culture) == 0){
            return false; // cannot be a commit char if it is an expected char in a matching name
          }
        }
      }
      return !(Char.IsLetterOrDigit(commitChar) || commitChar == '_');
    }
  }
  public enum OverloadKind {
    Methods = 0x0001,
    Constructors = 0x0002,
    AttributeConstructors = 0x0004,
    Indexer = 0x0008,
    Property = 0x0010,
    GenericTypes = 0x0020,
    GenericMethods = 0x0040,
    Any = OverloadKind.Methods | OverloadKind.Constructors | OverloadKind.AttributeConstructors | OverloadKind.Indexer
      | OverloadKind.Property | OverloadKind.GenericTypes | OverloadKind.GenericMethods,
  }
  public class Overloads{
    public MemberList members;
    public Scope scope;
    protected AuthoringHelper helper;
    public int positionOfSelectedMember;
    public OverloadKind OverloadKind;

    public Overloads(MemberList members, Scope scope, int positionOfSelectedMember, AuthoringHelper helper, OverloadKind kind){
      this.members = members;
      this.scope = scope;
      this.helper = helper;
      this.positionOfSelectedMember = positionOfSelectedMember;
      this.OverloadKind = kind;
    }
    public Overloads(Overloads overloads)
      : this(overloads.members, overloads.scope, overloads.positionOfSelectedMember, overloads.helper, overloads.OverloadKind) {
    }
    public virtual string GetName(int index){
      TypeNode t = this.members[index] as TypeNode;
      if (t is ArrayType) return "";
      if (t != null) return t.GetFullUnmangledNameWithoutTypeParameters();
      Property p = this.members[index] as Property;
      if (p != null && p.DeclaringType != null) return p.DeclaringType.GetFullUnmangledNameWithTypeParameters();
      string st = this.helper.ErrorHandler.GetMemberName(this.members[index]);
      return st;
    }
    public virtual int GetCount(){
      int ret = this.members == null ? 0 : this.members.Count;
      return ret;
    }
    public virtual int GetPositionOfSelectedMember(){
      return this.positionOfSelectedMember;
    }
    public virtual string GetDescription(int index){
      string st; 
      Member m = this.members[index];
      st = this.helper.GetDescription(m, 0);
      return st;
    }
    public virtual string GetHelpText(int index){
      string st = this.members[index].HelpText;
      return st;
    }
    public virtual string GetSignature(int index) {
      string st = this.helper.GetSignature(this.members[index], this.scope);
      return st;
    }
    public virtual string GetType(int index){
      string st;
      Member m = this.members[index];
      if (m is InstanceInitializer || m is StaticInitializer)
        st = "";
      else if (m is Method)
        st = this.helper.ErrorHandler.GetTypeName(((Method)m).ReturnType);
      else if (m is Property)
        st = this.helper.ErrorHandler.GetTypeName(((Property)m).Type);
      else if (m is ArrayType)
        st = this.helper.ErrorHandler.GetTypeName(((ArrayType)m).ElementType);
      else
        st = "";
      int l = st.Length;
      if (l > 53){
        st = st.Substring(0,30) + "..." + st.Substring(l-20,20);
      }
      return st;
    }
    public virtual int GetParameterCount(int index){
      Member m = this.members[index];
      TypeNode t = m as TypeNode;
      if (t != null && t.TemplateParameters != null)
        return t.TemplateParameters.Count;
      ParameterList parameters = null;
      if (m is Method)
        parameters = ((Method)m).Parameters;
      else if (m is Property)
        parameters = ((Property)m).Parameters;
      else if (m is ArrayType)
        return ((ArrayType)m).Rank;
      if (parameters == null) return 0;
      return parameters.Count;
    }
    public virtual void GetParameterInfo(int index, int parameter, out string name, out string display, out string description){
      name = "";
      description = "";
      display = "";
      Member m = this.members[index];
      TypeNode t = m as TypeNode;
      if (t != null){
        if (t is ArrayType) return;
        if (t.TemplateParameters != null && parameter >= 0 && parameter < t.TemplateParameters.Count){
          TypeNode p = t.TemplateParameters[parameter];
          if (p != null){
            name = p.Name == null ? "" : p.Name.Name;
            display = name;
            description = p.HelpText;
          }
          return;
        }
      }
      ParameterList parameters = null;
      if (m is Method)
        parameters = ((Method)m).Parameters;
      else if (m is Property)
        parameters = ((Property)m).Parameters;
      if (parameters != null && parameter >= 0 && parameter < parameters.Count){
        Parameter p = parameters[parameter];
        name = p.Name == null ? "" : p.Name.Name;
        display = this.helper.GetParameterDescription(p, this.scope);
        description = m.GetParameterHelpText(name);
        if (description != null) description = name + ": " + description;
      }
    }
    public virtual string GetParameterClose(int index){
      Member mem = this.members[index];
      if (mem is Property || mem is ArrayType)
        return "]";
      else if (mem is TypeNode)
        return ">";
      else
        return ")";
    }
    public virtual string GetParameterOpen(int index){
      Member mem = this.members[index];
      if (mem is Property || mem is ArrayType)
        return "[";
      else if (mem is TypeNode)
        return "<";
      else
        return "(";
    }
    public string GetParameterSeparator(int index){
      if (this.members[index] is ArrayType) return ",";
      return ", ";
    }
  }
  public class GenericMethodOverloads : Overloads {
    public GenericMethodOverloads(MemberList members, Scope scope, int positionOfSelectedMember, AuthoringHelper helper) 
      : base(members, scope, positionOfSelectedMember, helper, OverloadKind.GenericMethods)
    {
    }
    public override string GetName(int index){
      Method m = this.members[index] as Method;
      if (m != null)
        return this.helper.GetFullMethodName(m);
      return null;
    }
    public override string GetParameterClose(int index) {
      Method m = this.members[index] as Method;
      if (m != null)
        return ">"+this.helper.GetParameterString(m);
      return null;
    }
    public override int GetParameterCount(int index){
      Member m = this.members[index];
      TypeNodeList tparams = null;
      if (m is Method)
        tparams = ((Method)m).TemplateParameters;
      if (tparams == null) return 0;
      return tparams.Count;
    }
    public override void GetParameterInfo(int index, int parameter, out string name, out string display, out string description){
      name = "";
      description = "";
      display = "";
      TypeNodeList tparams = null;
      Member m = this.members[index];
      if (m is Method)
        tparams = ((Method)m).TemplateParameters;
      if (tparams != null && parameter >= 0 && parameter < tparams.Count){
        TypeNode tp = tparams[parameter];
        if (tp != null){
          name = tp.Name == null ? "" : tp.Name.Name;
          display = name;
          description = tp.HelpText;
        }
      }
    }
    public override string GetParameterOpen(int method){
      return "<";
    }
  }
  public abstract class AuthoringSink{
    public abstract void AddCollapsibleRegion(SourceContext context, bool collapsed);
    /// <summary>
    /// Whenever a matching pair is parsed, e.g. '{' and '}', this method is called
    /// with the text span of both the left and right item. The
    /// information is used when a user types "ctrl-]" in VS
    /// to find a matching brace and when auto-highlight matching
    /// braces is enabled.
    /// </summary>
    public abstract void MatchPair(SourceContext startContext, SourceContext endContext);

    /// <summary>
    /// Matching tripples are used to highlight in bold a completed statement.  For example
    /// when you type the closing brace on a foreach statement VS highlights in bold the statement
    /// that was closed.  The first two source contexts are the beginning and ending of the statement that
    /// opens the block (for example, the span of the "foreach(...){" and the third source context
    /// is the closing brace for the block (e.g., the "}").
    /// </summary>
    public abstract void MatchTriple(SourceContext startContext, SourceContext middleContext, SourceContext endContext);

    /// <summary>
    /// In support of Member Selection, CompleteWord, QuickInfo, 
    /// MethodTip, and Autos, the StartName and QualifyName methods
    /// are called.
    /// StartName is called for each identifier that is parsed (e.g. "Console")
    /// Its type is Expression since it can be this/base etc
    /// </summary>
    public abstract void StartName(Expression name);

    /// <summary>
    /// QualifyName is called for each qualification with both
    /// the text span of the selector (e.g. ".")  and the text span 
    /// of the name ("WriteLine").
    /// </summary>
    public abstract void QualifyName(SourceContext selectorContext, Expression name);

    /// <summary>
    /// AutoExpression is in support of IVsLanguageDebugInfo.GetProximityExpressions.
    /// It is called for each expression that might be interesting for
    /// a user in the "Auto Debugging" window. All names that are
    /// set using StartName and QualifyName are already automatically
    /// added to the "Auto" window! This means that AutoExpression
    /// is rarely used.
    /// </summary>   
    public abstract void AutoExpression(SourceContext exprContext);

    /// <summary>
    /// CodeSpan is in support of IVsLanguageDebugInfo.ValidateBreakpointLocation.
    /// It is called for each region that contains "executable" code.
    /// This is used to validate breakpoints. Comments are
    /// automatically taken care of based on TokenInfo returned from scanner. 
    /// Normally this method is called when a procedure is started/ended.
    /// </summary>
    public abstract void CodeSpan(SourceContext spanContext);

    /// <summary>
    /// The StartParameters, Parameter and EndParameter methods are
    /// called in support of method tip intellisense (ECMD_PARAMINFO).
    /// [StartParameters] is called when the parameters of a method
    /// are started, ie. "(".
    /// [NextParameter] is called on the start of a new parameter, ie. ",".
    /// [EndParameter] is called on the end of the paramters, ie. ")".
    /// REVIEW: perhaps this entire scheme should go away
    /// </summary>
    public abstract void StartParameters(SourceContext context);

    /// <summary>
    /// NextParameter is called after StartParameters on the start of each new parameter, ie. ",".
    /// </summary>
    public abstract void NextParameter(SourceContext context);

    /// <summary>
    /// EndParameter is called on the end of the paramters, ie. ")".
    /// </summary>
    public abstract void EndParameters(SourceContext context);

    public abstract void StartTemplateParameters(SourceContext context);
    public abstract void NextTemplateParameter(SourceContext context);
    public abstract void EndTemplateParameters(SourceContext context);

    /// <summary>
    /// Send a message to the development enviroment. The kind of message
    /// is specified through the given severity. 
    /// </summary>
    public abstract void AddError(ErrorNode node);
//    public abstract SourceContext RegionToClear{get; set;}
  }
  public class AuthoringHelper{
    public ErrorHandler ErrorHandler;
    public CultureInfo culture;
    public bool SuppressAttributeSuffix;

    public AuthoringHelper(ErrorHandler errorHandler, CultureInfo culture){
      this.ErrorHandler = errorHandler;
      this.culture = culture;
    }

    public virtual String GetDescription(Member member){
      return this.GetDescription(member, 0);
    }
    public virtual String GetDescription(Member member, int overloads){
      StringBuilder descr = new StringBuilder(this.ErrorHandler.GetMemberSignature(member, true));
      if (overloads > 0){
        descr.Append(" (+ ");
        descr.Append(overloads.ToString());
        if (member is TypeNode) descr.Append(" generic");
        descr.Append(overloads == 1 ? " overload" : " overloads"); //TODO: globalize this
        descr.Append(")");
      }
      string helpText = member.HelpText;
      if (helpText != null && helpText.Length > 0){
        descr.Append("\n");
        descr.Append(helpText);
      }
      return descr.ToString();
    }
    public virtual string GetSignature(Member member, Scope scope){
      return this.ErrorHandler.GetMemberSignature(member, true);
    }
    public virtual string GetFullMethodName(Method method){
      if (method == null || method.DeclaringType == null) return null;
      return method.DeclaringType.GetFullUnmangledNameWithTypeParameters()+"."+method.Name;
    }
    public virtual string GetFullTypeName(TypeNode type){
      return this.ErrorHandler.GetTypeName(type);
    }
    public virtual string GetMemberName(Member member){
      TypeNode t = member as TypeNode;
      if (t != null){
        string name = t.GetUnmangledNameWithoutTypeParameters();
        if (this.SuppressAttributeSuffix && name.EndsWith("Attribute"))
          return name.Substring(0, name.Length-9);
        else if (t.TemplateParameters != null && t.TemplateParameters.Count > 0)
          return name+"<>";
        else if (t.TemplateArguments != null && t.TemplateArguments.Count > 0)
          return this.ErrorHandler.GetUnqualifiedTypeName(t);
        return name;
      }
      Method meth = member as Method;
      if (meth != null && meth.TemplateParameters != null && meth.TemplateParameters.Count > 0)
        return meth.Name.Name+"<>";
      if (member == null || member.Name == null) return " ";
      return member.Name.Name;
    }
    public virtual string GetParameterDescription(Parameter parameter, Scope scope){
      StringBuilder descr = new StringBuilder(this.ErrorHandler.GetParameterTypeName(parameter));
      descr.Append(' ');
      descr.Append(parameter.Name);
      return descr.ToString();
    }
    public virtual string GetParameterString(Method method){
      return this.ErrorHandler.GetSignatureString("", method.Parameters, "(", ")", ", ", true);
    }
  }
  /// <summary>
  /// Represents the two drop down bars on the top of a text editor window that allow types and type members to be selected by name.
  /// </summary>
  public class TypeAndMemberDropdownBars{
    /// <summary>The language service object that created this object and calls its SynchronizeDropdowns method</summary>
    public LanguageService languageService;
    /// <summary>The list of types that appear in the type drop down list. Sorted by full type name.</summary>
    public TypeNodeList sortedDropDownTypes;
    /// <summary>The list of types that appear in the type drop down list. Textual order.</summary>
    private TypeNodeList dropDownTypes;
    /// <summary>The list of members that appear in the member drop down list. Sorted by name.</summary>
    private MemberList dropDownMembers;
    /// <summary>The list of members that appear in the member drop down list. Textual order.</summary>
    public MemberList sortedDropDownMembers;
    public string[] dropDownMemberSignatures;
//    public int[] dropDownTypeGlyphs;
//    public int[] dropDownMemberGlyphs;
    public int selectedType = -1;
    public int selectedMember = -1;
    private const int DropClasses = 0;
    private const int DropMethods = 1;
   
    public TypeAndMemberDropdownBars(LanguageService languageService){
      this.languageService = languageService;
    }

    /// <summary>
    /// Updates the state of the drop down bars to match the current contents of the text editor window. Call this initially and every time
    /// the cursor position changes.
    /// </summary>
    /// <param name="textView">The editor window</param>
    /// <param name="line">The line on which the cursor is now positioned</param>
    /// <param name="col">The column on which the cursor is now position</param>
    public void SynchronizeDropdowns(string fname, int line, int col){
      Compilation compilation = this.languageService.GetCompilationFor(fname);
      if (compilation == null){Debug.Assert(false); return;}
      CompilationUnit cu = this.languageService.GetCompilationUnitSnippet(compilation, fname);       
      if (cu == null || cu.Nodes == null || cu.Nodes.Count == 0 || !(cu.Nodes[0] is Namespace)) return;
      AuthoringHelper helper = this.languageService.GetAuthoringHelper();
      TypeNodeList types = this.dropDownTypes;
      TypeNodeList sortedTypes = this.sortedDropDownTypes;
      //Need to reconstruct the type lists. First get the types in text order.
      types = this.dropDownTypes = new TypeNodeList();
      this.PopulateTypeList(types, (Namespace)cu.Nodes[0]);
      //Now sort by full text name.
      int n = types.Count;
      if (n == 0) return;
      sortedTypes = this.sortedDropDownTypes = new TypeNodeList(n);
      for (int i = 0; i < n; i++){
        TypeNode t = types[i];
        if (t == null){Debug.Assert(false); continue;}
        string tName =  helper.GetMemberName(t);
        sortedTypes.Add(t);
        for (int j = sortedTypes.Count-2; j >= 0; j--){
          if (string.Compare(tName, helper.GetMemberName(sortedTypes[j]), true, System.Globalization.CultureInfo.InvariantCulture) >= 0) break;
          sortedTypes[j+1] = sortedTypes[j];
          sortedTypes[j] = t;
        }
      }
      this.selectedType = -1;
      //Find the type matching the given source position
      int newType = 0;
      int candidateType = -1;
      for (int i = 0; i < n; i++){
        TypeNode t = types[i];
        if (t == null) continue;
        if (t.SourceContext.Document == null) continue;
        if (t.SourceContext.Encloses(line+1, col+1)) candidateType = i;
        if (t.SourceContext.StartLine > line+1 || (t.SourceContext.StartLine == line+1 && t.SourceContext.StartColumn > col+1)){
          if (candidateType == -1) candidateType = 0;
          t = types[candidateType];
        }else if (i < n-1)
          continue;
        else{
          if (candidateType == -1) candidateType = 0;
          t = types[candidateType];
        }
        for (int j = 0; j < n; j++){
          if (sortedTypes[j] != t) continue;
          newType = j;
          break;
        }
        break;
      }
      MemberList members = this.dropDownMembers;
      MemberList sortedMembers = this.sortedDropDownMembers;
      if (newType != this.selectedType){
        if (newType < 0 || newType > sortedTypes.Count) return;
        TypeNode t = sortedTypes[newType];
        if (t == null || t.Members == null) return;
        //Need to reconstruct the member list. First get the members in text order.
        members = t.Members;
        n = members == null ? 0 : members.Count;
        MemberList newMembers = this.dropDownMembers = new MemberList(n);
        //Now sort them
        sortedMembers = this.sortedDropDownMembers = new MemberList(n);
        string[] memSignatures = this.dropDownMemberSignatures = new string[n];
        for (int i = 0; i < n; i++){
          Member mem = members[i];
          if (mem == null) continue;
          if (mem.SourceContext.Document == null) continue;
          string memSignature = this.languageService.errorHandler.GetUnqualifiedMemberSignature(mem);
          if (memSignature == null) continue;
          memSignatures[sortedMembers.Count] = memSignature;
          newMembers.Add(mem);
          sortedMembers.Add(mem);
          for (int j = sortedMembers.Count - 2; j >= 0; j--){
            if (string.Compare(memSignature, memSignatures[j], true, System.Globalization.CultureInfo.InvariantCulture) >= 0) break;
            memSignatures[j+1] = memSignatures[j];
            memSignatures[j] = memSignature;
            sortedMembers[j+1] = sortedMembers[j];
            sortedMembers[j] = mem;
          }
        }
        this.selectedMember = -1;
      }
      //Find the member matching the given source position
      members = this.dropDownMembers;
      int newMember = 0;
      n = sortedMembers.Count;
      for (int i = 0; i < n; i++){
        Member mem = members[i];
        if (mem == null) continue;
        if (mem.SourceContext.StartLine > line+1 || (mem.SourceContext.StartLine == line+1 && mem.SourceContext.StartColumn > col+1)){
          if (i > 0) mem = members[i-1];
        }else if (i < n-1)
          continue;
        for (int j = 0; j < n; j++){
          if (sortedMembers[j] != mem) continue;
          newMember = j;
          break;
        }
        break;
      }
      this.selectedType = newType;
      this.selectedMember = newMember;
    }
    public void PopulateTypeList(TypeNodeList types, Namespace ns){
      if (types == null || ns == null){Debug.Assert(false); return;}
      if (ns.NestedNamespaces != null)
        this.PopulateTypeList(types, ns.NestedNamespaces);
      TypeNodeList nTypes = ns.Types;
      for (int j = 0, m = nTypes == null ? 0 : nTypes.Count; j < m; j++){
        TypeNode t = nTypes[j];
        if (t == null) continue;
        if ((t.Flags & TypeFlags.SpecialName) != 0) continue;
        this.PopulateTypeList(types, t);
      }
    }
    public void PopulateTypeList(TypeNodeList types, NamespaceList namespaces){
      if (types == null){Debug.Assert(false); return;}
      for (int i = 0, n = namespaces == null ? 0 : namespaces.Count; i < n; i++){
        Namespace ns = namespaces[i];
        if (ns == null) continue;
        if (ns.NestedNamespaces != null)
          this.PopulateTypeList(types, ns.NestedNamespaces);
        TypeNodeList nTypes = ns.Types;
        for (int j = 0, m = nTypes == null ? 0 : nTypes.Count; j < m; j++){
          TypeNode t = nTypes[j];
          if (t == null) continue;
          this.PopulateTypeList(types, t);
        }
      }
    }
    public void PopulateTypeList(TypeNodeList types, TypeNode t){
      if (types == null || t == null){Debug.Assert(false); return;}
      types.Add(t);
      MemberList members = t.Members;
      for (int i = 0, n = members == null ? 0 : members.Count; i < n; i++){
        t = members[i] as TypeNode;
        if (t == null) continue;
        this.PopulateTypeList(types, t);
      }
    }
  }
}
