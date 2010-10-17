//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
//
#if CCINamespace
using Microsoft.Cci;
using Cci = Microsoft.Cci;
#else
using System.Compiler;
using Cci = System.Compiler;
#endif
using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.SpecSharp{

  internal enum SpecSharpTokenColor{
    Text,
    Keyword,
    Comment,
    Identifier,
    String,
    Number,
    Name,
    Literal, 
    CData,
    ProcessingInstruction,
    LastColor
  }

  public sealed class LanguageService : Cci.LanguageService{
    public TrivialHashtable scopeFor;
    private Hashtable dummyCompilationFor;
    internal CompilationUnit partialCompilationUnit;
    internal bool parsingStatement;
    private bool allowSpecSharpExtensions;

    public LanguageService()
      : base(new ErrorHandler(new ErrorNodeList(0))){
    }

    public override Cci.AuthoringScope GetAuthoringScope() {
      return new AuthoringScope(this, new AuthoringHelper((ErrorHandler)this.errorHandler, this.culture));
    }
    public override int GetColorCount(){
      return (int)SpecSharpTokenColor.LastColor;
    }
    public override System.CodeDom.Compiler.CompilerParameters GetDummyCompilerParameters(){
      SpecSharpCompilerOptions options = new SpecSharpCompilerOptions();
      options.DummyCompilation = true;
      return options;
    }
    public override Cci.MemberFinder GetMemberFinder(int line, int col){
      return new MemberFinder(line, col);
    }
    public override Cci.Scanner GetScanner(){
      return null;
    }
    public override Compilation GetDummyCompilationFor(string fileName){
      if (this.dummyCompilationFor == null) this.dummyCompilationFor = new Hashtable();
      WeakReference wref = (WeakReference)this.dummyCompilationFor[fileName];
      if (wref != null && wref.IsAlive) return (Compilation)wref.Target;
      string fContents = null;
      if (File.Exists(fileName)){
        StreamReader sr = new StreamReader(fileName);
        fContents = sr.ReadToEnd(); sr.Close();
      }
      Compilation compilation = new Compilation();
      compilation.CompilerParameters = this.GetDummyCompilerParameters();
      compilation.TargetModule = new Module();
      DocumentText docText = new DocumentText(new StringSourceText(fContents, true));
      SourceContext sctx = new SourceContext(Compiler.CreateSpecSharpDocument(fileName, 0, docText));
      compilation.CompilationUnits = new CompilationUnitList(new CompilationUnitSnippet(new Identifier(fileName), new ParserFactory(), sctx));
      compilation.CompilationUnits[0].Compilation = compilation;
      this.dummyCompilationFor[fileName] = new WeakReference(compilation);
      return compilation;
    }
    public override MemberList GetTypesNamespacesAndPrefixes(Scope scope, bool constructorMustBeVisible, bool listAllUnderRootNamespace) {
      MemberList result = new MemberList();
      while (scope != null && !(scope is TypeScope || scope is NamespaceScope)) scope = scope.OuterScope;
      if (scope == null) return result;
      TypeNode currentType = scope is TypeScope ? ((TypeScope)scope).Type : null;
      if (!(scope is NamespaceScope) && (currentType == null || currentType.DeclaringModule == null)) return result;
      ErrorHandler errorHandler = new ErrorHandler(new ErrorNodeList(0));
      TrivialHashtable ambiguousTypes = new TrivialHashtable();
      TrivialHashtable referencedLabels = new TrivialHashtable();
      Looker looker = new Looker(null, errorHandler, null, ambiguousTypes, referencedLabels);
      looker.currentType = currentType;
      looker.currentModule = this.currentSymbolTable;
      looker.currentAssembly = looker.currentModule as AssemblyNode;
      result = looker.GetVisibleTypesNamespacesAndPrefixes(scope, constructorMustBeVisible, listAllUnderRootNamespace);
      return result;
    }
    public override MemberList GetNamespacesAndAttributeTypes(Scope scope){
      MemberList result = new MemberList();
      while (scope != null && !(scope is TypeScope) && !(scope is NamespaceScope)) scope = scope.OuterScope;
      if (scope == null) return result;
      TypeNode currentType = scope is TypeScope ? ((TypeScope)scope).Type : null;
      ErrorHandler errorHandler = new ErrorHandler(new ErrorNodeList(0));
      TrivialHashtable ambiguousTypes = new TrivialHashtable();
      TrivialHashtable referencedLabels = new TrivialHashtable();
      Looker looker = new Looker(null, errorHandler, null, ambiguousTypes, referencedLabels);
      looker.currentType = currentType;
      looker.currentModule = this.currentSymbolTable;
      if (looker.currentModule == null) return result;
      return looker.GetNamespacesAndAttributeTypes(scope);
    }
    public override MemberList GetVisibleNames(Scope scope){
      MemberList result = new MemberList();
      if (scope == null) return result;
      Scope sc = scope;
      if (sc is MethodScope) return this.GetTypesNamespacesAndPrefixes(scope, false, false); //inside a parameter list.
      while (sc is BlockScope) sc = sc.OuterScope;
      MethodScope mscope = sc as MethodScope;
      while (sc != null && !(sc is TypeScope || sc is NamespaceScope)) sc = sc.OuterScope;
      TypeNode currentType = sc is TypeScope ? ((TypeScope)sc).Type : null;
      ErrorHandler errorHandler = new ErrorHandler(new ErrorNodeList(0));
      TrivialHashtable ambiguousTypes = new TrivialHashtable();
      TrivialHashtable referencedLabels = new TrivialHashtable();
      Looker looker = new Looker(null, errorHandler, null, ambiguousTypes, referencedLabels);
      if (mscope != null) looker.currentMethod = mscope.DeclaringMethod;
      looker.currentType = currentType;
      looker.currentModule = this.currentSymbolTable;
      looker.currentAssembly = looker.currentModule as AssemblyNode;
      return looker.GetVisibleNames(scope);
    }
    public override MemberList GetNestedNamespacesAndTypes(Identifier name, Scope scope, AssemblyReferenceList assembliesToSearch){
      MemberList result = new MemberList();
      ErrorHandler errorHandler = new ErrorHandler(new ErrorNodeList(0));
      TrivialHashtable scopeFor = new TrivialHashtable();
      TrivialHashtable factoryMap = new TrivialHashtable();
      TrivialHashtable ambiguousTypes = new TrivialHashtable();
      TrivialHashtable referencedLabels = new TrivialHashtable();
      Looker looker = new Looker(null, errorHandler, null, ambiguousTypes, referencedLabels);
      looker.currentModule = this.currentSymbolTable;
      return looker.GetNestedNamespacesAndTypes(name, scope, assembliesToSearch);
    }
    private static MemberList NamespaceStartKeywords = LanguageService.GetNamespaceStartKeywords();
    private static MemberList GetNamespaceStartKeywords(){
      MemberList members = new MemberList();
      members.Add(new KeywordCompletion("abstract"));
      members.Add(new KeywordCompletion("class"));
      members.Add(new KeywordCompletion("delegate"));
      members.Add(new KeywordCompletion("enum"));
      members.Add(new KeywordCompletion("interface"));
      members.Add(new KeywordCompletion("internal"));
      members.Add(new KeywordCompletion("namespace"));
      members.Add(new KeywordCompletion("partial"));
      members.Add(new KeywordCompletion("public"));
      members.Add(new KeywordCompletion("private"));
      members.Add(new KeywordCompletion("protected"));
      members.Add(new KeywordCompletion("sealed"));
      members.Add(new KeywordCompletion("static"));
      members.Add(new KeywordCompletion("struct"));
      members.Add(new KeywordCompletion("unsafe"));
      members.Add(new KeywordCompletion("using"));
      return members;
  }
    private static MemberList TypeMemberKeywords = LanguageService.GetTypeMemberKeywords();
    private static MemberList GetTypeMemberKeywords() {
      MemberList members = new MemberList();
      members.Add(new KeywordCompletion("abstract"));
      members.Add(new KeywordCompletion("class"));
      members.Add(new KeywordCompletion("const"));
      members.Add(new KeywordCompletion("delegate"));
      members.Add(new KeywordCompletion("enum"));
      members.Add(new KeywordCompletion("event"));
      members.Add(new KeywordCompletion("explicit"));
      members.Add(new KeywordCompletion("extern"));
      members.Add(new KeywordCompletion("implicit"));
      members.Add(new KeywordCompletion("interface"));
      members.Add(new KeywordCompletion("internal"));
      members.Add(new KeywordCompletion("namespace"));
      members.Add(new KeywordCompletion("new"));
      members.Add(new KeywordCompletion("operator"));
      members.Add(new KeywordCompletion("override"));
      members.Add(new KeywordCompletion("partial"));
      members.Add(new KeywordCompletion("public"));
      members.Add(new KeywordCompletion("private"));
      members.Add(new KeywordCompletion("protected"));
      members.Add(new KeywordCompletion("readonly"));
      members.Add(new KeywordCompletion("sealed"));
      members.Add(new KeywordCompletion("static"));
      members.Add(new KeywordCompletion("struct"));
      members.Add(new KeywordCompletion("unsafe"));
      members.Add(new KeywordCompletion("using"));
      members.Add(new KeywordCompletion("virtual"));
      members.Add(new KeywordCompletion("void"));
      members.Add(new KeywordCompletion("volatile"));
      members.Add(new KeywordCompletion("where"));
      LanguageService.AddTypeKeywords(members, null);
      return members;
    }
    private void AddTypeMemberKeywords(MemberList/*!*/ members) {
      foreach (KeywordCompletion keyword in LanguageService.TypeMemberKeywords)
        members.Add(keyword);
      if (this.allowSpecSharpExtensions){
        members.Add(new KeywordCompletion("invariant"));
      }
    }
    private static MemberList statementKeywords = LanguageService.GetStatementKewords();
    private static MemberList GetStatementKewords() {
      MemberList members = new MemberList();
      members.Add(new KeywordCompletion("as"));
      members.Add(new KeywordCompletion("base"));
      members.Add(new KeywordCompletion("break"));
      members.Add(new KeywordCompletion("case"));
      members.Add(new KeywordCompletion("catch"));
      members.Add(new KeywordCompletion("checked"));
      members.Add(new KeywordCompletion("const"));
      members.Add(new KeywordCompletion("continue"));
      members.Add(new KeywordCompletion("default"));
      members.Add(new KeywordCompletion("delegate"));
      members.Add(new KeywordCompletion("do"));
      members.Add(new KeywordCompletion("else"));
      members.Add(new KeywordCompletion("false"));
      members.Add(new KeywordCompletion("finally"));
      members.Add(new KeywordCompletion("fixed"));
      members.Add(new KeywordCompletion("for"));
      members.Add(new KeywordCompletion("foreach"));
      members.Add(new KeywordCompletion("goto"));
      members.Add(new KeywordCompletion("if"));
      members.Add(new KeywordCompletion("in"));
      members.Add(new KeywordCompletion("is"));
      members.Add(new KeywordCompletion("lock"));
      members.Add(new KeywordCompletion("new"));
      members.Add(new KeywordCompletion("null"));
      members.Add(new KeywordCompletion("out"));
      members.Add(new KeywordCompletion("ref"));
      members.Add(new KeywordCompletion("return"));
      members.Add(new KeywordCompletion("sizeof"));
      members.Add(new KeywordCompletion("stackalloc"));
      members.Add(new KeywordCompletion("switch"));
      members.Add(new KeywordCompletion("true"));
      members.Add(new KeywordCompletion("typeof"));
      members.Add(new KeywordCompletion("unchecked"));
      members.Add(new KeywordCompletion("throw"));
      members.Add(new KeywordCompletion("try"));
      members.Add(new KeywordCompletion("unsafe"));
      members.Add(new KeywordCompletion("using"));
      members.Add(new KeywordCompletion("while"));
      members.Add(new KeywordCompletion("yield"));
      LanguageService.AddTypeKeywords(members, null);
      return members;
    }
    private void AddStatementKeywords(MemberList/*!*/ members, Scope scope) {
      foreach (KeywordCompletion keyword in LanguageService.statementKeywords)
        members.Add(keyword);
      if (this.allowSpecSharpExtensions){
        members.Add(new KeywordCompletion("additive"));
        members.Add(new KeywordCompletion("assert"));
        members.Add(new KeywordCompletion("assume"));
        members.Add(new KeywordCompletion("ensures"));
        members.Add(new KeywordCompletion("expose"));
        members.Add(new KeywordCompletion("invariant"));
        members.Add(new KeywordCompletion("requires"));
      }
      while (scope != null && !(scope is MethodScope)) {
        scope = scope.OuterScope;
      }
      MethodScope ms = scope as MethodScope;
      if (ms != null && ms.DeclaringMethod != null && !ms.DeclaringMethod.IsStatic) {
        members.Add(new KeywordCompletion("this"));
      }
    }
    private static MemberList typeKeywords = LanguageService.GetTypeKeywords();
    private static KeywordCompletion[] keywordArray;
    private static MemberList GetTypeKeywords() {
      MemberList members = new MemberList();
      keywordArray = new KeywordCompletion[(int)TypeCode.String+1];
      members.Add(keywordArray[(int)TypeCode.Boolean] = new KeywordCompletion("bool"));
      members.Add(keywordArray[(int)TypeCode.Byte] = new KeywordCompletion("byte"));
      members.Add(keywordArray[(int)TypeCode.Char] = new KeywordCompletion("char"));
      members.Add(keywordArray[(int)TypeCode.Decimal] = new KeywordCompletion("decimal"));
      members.Add(keywordArray[(int)TypeCode.Double] = new KeywordCompletion("double"));
      members.Add(keywordArray[(int)TypeCode.Single] = new KeywordCompletion("float"));
      members.Add(new KeywordCompletion("global"));
      members.Add(keywordArray[(int)TypeCode.Int32] = new KeywordCompletion("int"));
      members.Add(keywordArray[(int)TypeCode.Int64] = new KeywordCompletion("long"));
      members.Add(keywordArray[(int)TypeCode.Object] = new KeywordCompletion("object"));
      members.Add(keywordArray[(int)TypeCode.SByte] = new KeywordCompletion("sbyte"));
      members.Add(keywordArray[(int)TypeCode.Int16] = new KeywordCompletion("short"));
      members.Add(keywordArray[(int)TypeCode.String] = new KeywordCompletion("string"));
      members.Add(keywordArray[(int)TypeCode.UInt32] = new KeywordCompletion("uint"));
      members.Add(keywordArray[(int)TypeCode.UInt64] = new KeywordCompletion("ulong"));
      members.Add(keywordArray[(int)TypeCode.UInt16] = new KeywordCompletion("ushort"));
      members.Add(keywordArray[(int)TypeCode.Empty] = new KeywordCompletion("void"));
      return members;
    }
    private static Member AddTypeKeywords(MemberList/*!*/ members, TypeNode preSelected){
      if (typeKeywords == null) typeKeywords = LanguageService.GetTypeKeywords();
      KeywordCompletion kc = null;
      if (preSelected != null) {
        TypeCode tc = preSelected.TypeCode;
        if ( tc != TypeCode.Object && tc != TypeCode.Empty )
          kc = keywordArray[(int)tc];
        else if (preSelected == SystemTypes.Object)
          kc = keywordArray[(int)TypeCode.Object];
        else if (preSelected == SystemTypes.Void)
          kc = keywordArray[(int)TypeCode.Empty];
      }
      foreach (KeywordCompletion keyword in LanguageService.typeKeywords) {
        if ( keyword != kc )
          members.Add(keyword);
      }
      return kc == null ? (Member)preSelected : (Member)kc;
    }
    private static MemberList attributeContextKeywords = LanguageService.GetAttributeContextKeywords();
    private static MemberList GetAttributeContextKeywords() {
      MemberList members = new MemberList();
      members.Add(new KeywordCompletion("assembly"));
      members.Add(new KeywordCompletion("class"));
      members.Add(new KeywordCompletion("event"));
      members.Add(new KeywordCompletion("field"));
      members.Add(new KeywordCompletion("method"));
      members.Add(new KeywordCompletion("module"));
      members.Add(new KeywordCompletion("param"));
      members.Add(new KeywordCompletion("property"));
      members.Add(new KeywordCompletion("return"));
      members.Add(new KeywordCompletion("type"));
      return members;
    }
    private static void AddAttributeContextKeywords(MemberList/*!*/ members) {
      if (attributeContextKeywords == null) attributeContextKeywords = LanguageService.GetAttributeContextKeywords();
      foreach (KeywordCompletion keyword in LanguageService.attributeContextKeywords)
        members.Add(keyword);
    }
    private static MemberList parameterContextKeywords = LanguageService.GetParameterContextKeywords();
    private static MemberList GetParameterContextKeywords() {
      MemberList members = new MemberList();
      members.Add(new KeywordCompletion("out"));
      members.Add(new KeywordCompletion("params"));
      members.Add(new KeywordCompletion("ref"));
      return members;
    }
    private static void AddParameterContextKeywords(MemberList/*!*/ members) {
      if (parameterContextKeywords == null) parameterContextKeywords = LanguageService.GetParameterContextKeywords();
      foreach (KeywordCompletion keyword in LanguageService.parameterContextKeywords)
        members.Add(keyword);
      AddTypeKeywords(members, null);
    }
    public override void ParseAndAnalyzeCompilationUnit(string fname, string text, int line, int col, ErrorNodeList errors, Compilation compilation, AuthoringSink sink) {
      if (fname == null || text == null || errors == null || compilation == null){Debug.Assert(false); return;}
      if (compilation != null && compilation.CompilerParameters is SpecSharpCompilerOptions)
        this.allowSpecSharpExtensions = !((SpecSharpCompilerOptions)compilation.CompilerParameters).Compatibility;
      CompilationUnitList compilationUnitSnippets = compilation.CompilationUnits;
      if (compilationUnitSnippets == null){Debug.Assert(false); return;}
      //Fix up the CompilationUnitSnippet corresponding to fname with the new source text
      CompilationUnitSnippet cuSnippet = this.GetCompilationUnitSnippet(compilation, fname);
      if (cuSnippet == null) return;
      Compiler compiler = new Compiler();
      compiler.CurrentCompilation = compilation;
      cuSnippet.SourceContext.Document = compiler.CreateDocument(fname, 1, new DocumentText(text));
      cuSnippet.SourceContext.EndPos = text.Length;
      //Parse all of the compilation unit snippets
      Module symbolTable = compilation.TargetModule = compiler.CreateModule(compilation.CompilerParameters, errors);
      AttributeList assemblyAttributes = symbolTable is AssemblyNode ? symbolTable.Attributes : null;
      AttributeList moduleAttributes = symbolTable is AssemblyNode ? ((AssemblyNode)symbolTable).ModuleAttributes : symbolTable.Attributes;
      int n = compilationUnitSnippets.Count;
      for (int i = 0; i < n; i++){
        CompilationUnitSnippet compilationUnitSnippet = compilationUnitSnippets[i] as CompilationUnitSnippet;
        if (compilationUnitSnippet == null){Debug.Assert(false); continue;}
        Document doc = compilationUnitSnippet.SourceContext.Document;
        doc = compilationUnitSnippet.SourceContext.Document;
        if (doc == null || doc.Text == null){Debug.Assert(false); continue;}
        IParserFactory factory = compilationUnitSnippet.ParserFactory;
        if (factory == null) continue;
        compilationUnitSnippet.Nodes = null;
        compilationUnitSnippet.PreprocessorDefinedSymbols = null;
        IParser p = factory.CreateParser(doc.Name, doc.LineNumber, doc.Text, symbolTable, compilationUnitSnippet == cuSnippet ? errors : new ErrorNodeList(), compilation.CompilerParameters);
        if (p == null){Debug.Assert(false); continue;}
        if (p is ResgenCompilerStub) continue;
        Parser specSharpParser = p as Parser;
        if (specSharpParser == null)
          p.ParseCompilationUnit(compilationUnitSnippet);
        else
          specSharpParser.ParseCompilationUnit(compilationUnitSnippet, compilationUnitSnippet != cuSnippet, false);
        //TODO: this following is a good idea only if the files will not be frequently reparsed from source
        //StringSourceText stringSourceText = doc.Text.TextProvider as StringSourceText;
        //if (stringSourceText != null && stringSourceText.IsSameAsFileContents)
        //  doc.Text.TextProvider = new CollectibleSourceText(doc.Name, doc.Text.Length);
      }
      //Construct symbol table for entire project
      ErrorHandler errorHandler = new ErrorHandler(errors);
      SpecSharpCompilation ssCompilation = new SpecSharpCompilation();
      TrivialHashtable ambiguousTypes = new TrivialHashtable();
      TrivialHashtable referencedLabels = new TrivialHashtable();
      TrivialHashtable scopeFor = this.scopeFor = new TrivialHashtable();
      Scoper scoper = new Scoper(scopeFor);
      scoper.currentModule = symbolTable;
      Looker symLooker = new Looker(null, new ErrorHandler(new ErrorNodeList(0)), scopeFor, ambiguousTypes, referencedLabels);
      symLooker.currentAssembly = (symLooker.currentModule = symbolTable) as AssemblyNode;
      Looker looker = new Looker(null, errorHandler, scopeFor, ambiguousTypes, referencedLabels);
      looker.currentAssembly = (looker.currentModule = symbolTable) as AssemblyNode;
      looker.VisitAttributeList(assemblyAttributes);
      bool dummyCompilation = compilation.CompilerParameters is SpecSharpCompilerOptions && ((SpecSharpCompilerOptions)compilation.CompilerParameters).DummyCompilation;
      if (dummyCompilation){
        //This happens when there is no project. In this case, semantic errors should be ignored since the references and options are unknown.
        //But proceed with the full analysis anyway so that some measure of Intellisense can still be provided.
        errorHandler.Errors = new ErrorNodeList(0);
      }
      for (int i = 0; i < n; i++){
        CompilationUnit cUnit = compilationUnitSnippets[i];
        scoper.VisitCompilationUnit(cUnit);
      }
      for (int i = 0; i < n; i++){
        CompilationUnit cUnit = compilationUnitSnippets[i];
        if (cUnit == cuSnippet)
          looker.VisitCompilationUnit(cUnit); //Uses real error message list and populate the identifier info lists
        else
          symLooker.VisitCompilationUnit(cUnit); //Errors are discarded
      }
      //Run resolver over symbol table so that custom attributes on member signatures are known and can be used
      //to error check the the given file.
      TypeSystem typeSystem = new TypeSystem(errorHandler);
      Resolver resolver = new Resolver(errorHandler, typeSystem);
      resolver.currentAssembly = (resolver.currentModule = symbolTable) as AssemblyNode;
      Resolver symResolver = new Resolver(new ErrorHandler(new ErrorNodeList(0)), typeSystem);
      symResolver.currentAssembly = resolver.currentAssembly;
      symResolver.VisitAttributeList(assemblyAttributes);
      for (int i = 0; i < n; i++) {
        CompilationUnit cUnit = compilationUnitSnippets[i];
        if (cUnit == cuSnippet)
          resolver.VisitCompilationUnit(cUnit); //Uses real error message list and populate the identifier info lists
        else
          symResolver.VisitCompilationUnit(cUnit); //Errors are discarded
      }
      if (dummyCompilation) return;
      //Now analyze the given file for errors
      Checker checker = new Checker(ssCompilation, errorHandler, typeSystem, scopeFor, ambiguousTypes, referencedLabels);
      checker.currentAssembly = (checker.currentModule = symbolTable) as AssemblyNode;
      checker.VisitAttributeList(assemblyAttributes, checker.currentAssembly);
      checker.VisitModuleAttributes(moduleAttributes);
      checker.VisitCompilationUnit(cuSnippet);
       
      MemberFinder finder = new MemberFinder(line, col);
      finder.VisitCompilationUnit(cuSnippet);
      Node node = finder.Member;
      if (node == null){
        if (line == 0 && col == 0) 
          node = cuSnippet;
        else
          return;
      }

      SpecSharpCompilerOptions options = (SpecSharpCompilerOptions) compilation.CompilerParameters;
      if (options.IsContractAssembly) return;
      ssCompilation.RunPlugins(node, errorHandler);
      Normalizer normalizer = new Normalizer(typeSystem);
      normalizer.Visit(node);
      Analyzer analyzer = new Analyzer(typeSystem, compilation);
      analyzer.Visit(node);

      if (options.RunProgramVerifierWhileEditing)
        ssCompilation.AddProgramVerifierPlugin(typeSystem, compilation);
      ssCompilation.analyzer = analyzer; // make the analyzer available to plugins for access to method CFGs
      ssCompilation.RunPlugins(node, errorHandler);
      ssCompilation.analyzer = null;
      analyzer = null;
    }
    public override CompilationUnit ParseCompilationUnit(string fname, string source, ErrorNodeList errors, Compilation compilation, AuthoringSink sink){
      this.parsingStatement = false;
      if (fname == null || source == null || errors == null || compilation == null){Debug.Assert(false); return null;}
      if (compilation != null && compilation.CompilerParameters is SpecSharpCompilerOptions)
        this.allowSpecSharpExtensions = !((SpecSharpCompilerOptions)compilation.CompilerParameters).Compatibility;
      CompilationUnit cu;
#if Xaml
      if (fname.Length > 5 && string.Compare(fname, fname.Length-5, ".xaml", 0, 5, true, CultureInfo.InvariantCulture) == 0){
        Document xamlDocument = Microsoft.XamlCompiler.Compiler.CreateXamlDocument(fname, 1, new DocumentText(source));
        Microsoft.XamlCompiler.ErrorHandler xamlErrorHandler = new Microsoft.XamlCompiler.ErrorHandler(errors);
        Microsoft.XamlCompiler.Compiler xamlCompiler =
          new Microsoft.XamlCompiler.Compiler(xamlDocument, compilation.TargetModule, xamlErrorHandler, new ParserFactory(), compilation.CompilerParameters as CompilerOptions);
        cu = xamlCompiler.GetCompilationUnit();
      }else{
#endif
        Parser p = new Parser(compilation.TargetModule);
        cu = p.ParseCompilationUnit(source, fname, compilation.CompilerParameters, errors, sink);
        if (cu != null) cu.Compilation = compilation;
        this.parsingStatement = p.parsingStatement;
#if Xaml
      }
#endif
      this.partialCompilationUnit = cu;
      return cu;
    }
    public override Cci.AuthoringScope GetAuthoringScopeForMethodBody(string text, Compilation/*!*/ compilation, Method/*!*/ method, AuthoringSink asink) {
      this.parsingStatement = true;
      if (text == null || compilation == null || method == null || method.Body == null || method.Body.SourceContext.Document == null)
        throw new ArgumentNullException();
      if (compilation != null && compilation.CompilerParameters is SpecSharpCompilerOptions)
        this.allowSpecSharpExtensions = !((SpecSharpCompilerOptions)compilation.CompilerParameters).Compatibility;
      this.currentSymbolTable = compilation.TargetModule;
      SourceContext sctx = method.Body.SourceContext;
      DocumentText docText = new DocumentText(text);
      Document doc = Compiler.CreateSpecSharpDocument(sctx.Document.Name, 1, docText);
      ErrorNodeList errors = new ErrorNodeList(0);
      Parser p = new Parser(doc, errors, compilation.TargetModule, compilation.CompilerParameters as SpecSharpCompilerOptions);
      p.ParseMethodBody(method, sctx.StartPos, asink);
      ErrorHandler errorHandler = new ErrorHandler(errors);
      TrivialHashtable ambiguousTypes = new TrivialHashtable();
      TrivialHashtable referencedLabels = new TrivialHashtable();
      Looker looker = new Looker(null, errorHandler, this.scopeFor, ambiguousTypes, referencedLabels);
      looker.currentAssembly = (looker.currentModule = compilation.TargetModule) as AssemblyNode;
      TypeNode currentType = method.DeclaringType;
      looker.currentType = currentType;
      looker.scope = method.Scope;
      if (looker.scope != null) looker.scope = looker.scope.OuterScope;
      looker.identifierInfos = this.identifierInfos = new NodeList();
      looker.identifierPositions = this.identifierPositions = new Int32List();
      looker.identifierLengths = this.identifierLengths = new Int32List();
      looker.identifierContexts = this.identifierContexts = new Int32List();
      looker.identifierScopes = this.identifierScopes = new ScopeList();
      looker.allScopes = this.allScopes = new ScopeList();
      looker.Visit(method);
      Resolver resolver = new Resolver(errorHandler, new TypeSystem(errorHandler));
      resolver.currentAssembly = (resolver.currentModule = this.currentSymbolTable) as AssemblyNode;
      resolver.currentType = currentType;
      if (currentType != null) {
        if (resolver.currentType.Template == null && resolver.currentType.ConsolidatedTemplateParameters != null && resolver.currentType.ConsolidatedTemplateParameters.Count > 0)
          resolver.currentTypeInstance = resolver.GetDummyInstance(resolver.currentType);
        else
          resolver.currentTypeInstance = resolver.currentType;
      }
      resolver.Visit(method);
      method.Body.Statements = null;
      return this.GetAuthoringScope();
    }
    public override void Resolve(Member unresolvedMember, Member resolvedMember){
      if (unresolvedMember == null || resolvedMember == null) return;
      if (this.scopeFor == null) return;
      ErrorHandler errorHandler = new ErrorHandler(new ErrorNodeList(0));
      TrivialHashtable ambiguousTypes = new TrivialHashtable();
      TrivialHashtable referencedLabels = new TrivialHashtable();
      Looker looker = new Looker(null, errorHandler, this.scopeFor, ambiguousTypes, referencedLabels);
      looker.currentAssembly = (looker.currentModule = this.currentSymbolTable) as AssemblyNode;
      TypeNode currentType = resolvedMember.DeclaringType;
      if (resolvedMember is TypeNode && unresolvedMember.DeclaringType != null && 
        ((TypeNode)resolvedMember).FullName == unresolvedMember.DeclaringType.FullName){
        unresolvedMember.DeclaringType = (TypeNode)resolvedMember;
        currentType = (TypeNode)resolvedMember;
        looker.scope = this.scopeFor[resolvedMember.UniqueKey] as Scope;
      }else if (unresolvedMember is TypeNode && resolvedMember.DeclaringType != null){
        if (((TypeNode)unresolvedMember).FullName != resolvedMember.DeclaringType.FullName) return; //Too many changes since last time entire file was compiled
        resolvedMember = resolvedMember.DeclaringType;
        currentType = resolvedMember.DeclaringType;
        Scope scope = this.scopeFor[resolvedMember.UniqueKey] as Scope;
        if (scope != null) this.scopeFor[unresolvedMember.UniqueKey] = scope;
        looker.scope = scope;
      }else if (resolvedMember.DeclaringType != null){
        unresolvedMember.DeclaringType = resolvedMember.DeclaringType;
        looker.scope = this.scopeFor[resolvedMember.DeclaringType.UniqueKey] as Scope;
        if (looker.scope == null && resolvedMember.DeclaringType.IsDefinedBy != null && resolvedMember.DeclaringType.IsDefinedBy.Count > 0)
          looker.scope = this.GetScopeFromPartialType(resolvedMember.DeclaringType, resolvedMember);
        Scope scope = this.scopeFor[resolvedMember.UniqueKey] as Scope;
        if (scope != null) this.scopeFor[unresolvedMember.UniqueKey] = scope;
      }else if (resolvedMember.DeclaringNamespace != null){
        unresolvedMember.DeclaringNamespace = resolvedMember.DeclaringNamespace;
        looker.scope = this.scopeFor[resolvedMember.DeclaringNamespace.UniqueKey] as Scope;
        Scope scope = this.scopeFor[resolvedMember.UniqueKey] as Scope;
        if (scope != null) this.scopeFor[unresolvedMember.UniqueKey] = scope;
      }
      if (looker.scope == null) return;
      looker.currentType = currentType;
      looker.identifierInfos = this.identifierInfos = new NodeList();
      looker.identifierPositions = this.identifierPositions = new Int32List();
      looker.identifierLengths = this.identifierLengths = new Int32List();
      looker.identifierContexts = this.identifierContexts = new Int32List();
      looker.identifierScopes = this.identifierScopes = new ScopeList();
      looker.allScopes = this.allScopes = new ScopeList();
      looker.Visit(unresolvedMember);
      //Walk IR inferring types and resolving overloads
      Resolver resolver = new Resolver(errorHandler, new TypeSystem(errorHandler));
      resolver.currentAssembly = (resolver.currentModule = this.currentSymbolTable) as AssemblyNode;
      resolver.currentType = currentType;
      if (currentType != null){
        if (resolver.currentType.Template == null && resolver.currentType.ConsolidatedTemplateParameters != null && resolver.currentType.ConsolidatedTemplateParameters.Count > 0)
          resolver.currentTypeInstance = resolver.GetDummyInstance(resolver.currentType);
        else
          resolver.currentTypeInstance = resolver.currentType;
      }
      resolver.Visit(unresolvedMember);
    }
    private Scope GetScopeFromPartialType(TypeNode completeType, Member member){
      if (completeType == null || member == null){Debug.Assert(false); return null;}
      for (int i = 0, n = completeType.IsDefinedBy == null ? 0 : completeType.IsDefinedBy.Count; i < n; i++){
        TypeNode partialType = completeType.IsDefinedBy[i];
        if (partialType == null) continue;
        MemberList members = partialType.Members;
        for (int j = 0, m = members == null ? 0 : members.Count; j < m; j++){
          if (members[j] == member) return this.scopeFor[partialType.UniqueKey] as Scope;
        }
      }
      return null;
    }
    public override void Resolve(CompilationUnit partialCompilationUnit){
      if (partialCompilationUnit == null){Debug.Assert(false); return;}
      TrivialHashtable scopeFor = new TrivialHashtable();
      Scoper scoper = new Scoper(scopeFor);
      scoper.currentModule = this.currentSymbolTable;
      scoper.VisitCompilationUnit(partialCompilationUnit);

      ErrorHandler errorHandler = new ErrorHandler(new ErrorNodeList(0));
      TrivialHashtable ambiguousTypes = new TrivialHashtable();
      TrivialHashtable referencedLabels = new TrivialHashtable();
      Looker looker = new Looker(null, errorHandler, scopeFor, ambiguousTypes, referencedLabels);
      looker.currentAssembly = (looker.currentModule = this.currentSymbolTable) as AssemblyNode;
      looker.identifierInfos = this.identifierInfos = new NodeList();
      looker.identifierPositions = this.identifierPositions = new Int32List();
      looker.identifierLengths = this.identifierLengths = new Int32List();
      looker.identifierContexts = this.identifierContexts = new Int32List();
      looker.identifierScopes = this.identifierScopes = new ScopeList();
      looker.allScopes = this.allScopes = new ScopeList();
      looker.VisitCompilationUnit(partialCompilationUnit);
      //Walk IR inferring types and resolving overloads
      Resolver resolver = new Resolver(errorHandler, new TypeSystem(errorHandler));
      resolver.currentAssembly = (resolver.currentModule = this.currentSymbolTable) as AssemblyNode;
      resolver.Visit(partialCompilationUnit);
    }
    public override void AddReleventKeywords(MemberList memberList, Node node, Scope scope, int identifierContext) {
      if (memberList == null) return;
      if (node is AttributeNode ){
        LanguageService.AddAttributeContextKeywords(memberList);
        return;
      }
      if (node is TypeAlias) {
        memberList.AddList(LanguageService.GetNamespaceStartKeywords());
        return;
      }
      Construct cons = node as Construct;
      if (cons != null){
        Member lastMember = memberList.Count > 0 ? memberList[memberList.Count - 1] : null;
        memberList.RemoveAt(memberList.Count - 1);
        if (!(cons.Constructor is QualifiedIdentifier)) {
          lastMember = LanguageService.AddTypeKeywords(memberList, lastMember as TypeNode);
        }
        if(lastMember!= null) memberList.Add(lastMember);
        return;
      }
      Identifier id = node as Identifier;
      if (id != null) {
        bool lastMemberPresent = memberList.Count > 0;
        Member lastMember = memberList.Count > 0 ? memberList[memberList.Count - 1] : null;
        memberList.RemoveAt(memberList.Count - 1);
        lastMember = LanguageService.AddTypeKeywords(memberList, lastMember as TypeNode);
        if (lastMemberPresent) memberList.Add(lastMember);
        return;
      }
      TypeExpression tExpr = node as TypeExpression;
      if (tExpr != null && !(tExpr.Expression is QualifiedIdentifier)) {
        LanguageService.AddTypeKeywords(memberList, null);
        return;
      }
      if (node is NameBinding) {
        if (identifierContext == IdentifierContexts.ParameterContext)
          LanguageService.AddParameterContextKeywords(memberList);
        else if (scope is NamespaceScope || scope is TypeScope) {
          if (identifierContext == IdentifierContexts.TypeContext)
            LanguageService.AddTypeKeywords(memberList, null);
          else if(identifierContext != IdentifierContexts.EventContext)
            this.AddTypeMemberKeywords(memberList);
        } else if (identifierContext == IdentifierContexts.TypeContext && !((scope is BlockScope) && (scope.OuterScope is TypeScope))) //  type but not member decl scope...
          LanguageService.AddTypeKeywords(memberList, null);
        else if (this.parsingStatement || scope is AttributeScope)
          this.AddStatementKeywords(memberList, scope);
        else if (identifierContext != IdentifierContexts.EventContext)
          this.AddTypeMemberKeywords(memberList);
        return;
      }
      if ((node == null || node is Namespace) && identifierContext == IdentifierContexts.AllContext){
        if(this.parsingStatement)
          this.AddStatementKeywords(memberList, scope);
        else
          this.AddTypeMemberKeywords(memberList);
      }
    }
  }
  public class MemberFinder : Cci.MemberFinder{
    public MemberFinder(int line, int column)
      : base(line, column){
    }
    public MemberFinder(SourceContext sourceContext)
      : base(sourceContext){
    }
    public override Node VisitUnknownNodeType(Node node){
      if (node == null) return null;
      switch (((SpecSharpNodeType)node.NodeType)){
        case SpecSharpNodeType.KeywordList:
          return this.VisitField((Field)node);
        default:
          return base.VisitUnknownNodeType(node);
      }
    }
    public override NodeList VisitNodeList(NodeList nodes){
      if (nodes == null) return null;
      for (int i = 0, n = nodes.Count; i < n; i++){
        if (!(nodes[i] is Namespace)) continue;
        this.Visit(nodes[i]);
      }
      return nodes;
    }
  }

  public sealed class AuthoringScope : Cci.AuthoringScope{
    private Resolver resolver;
    private TypeSystem typeSystem;

    public AuthoringScope(LanguageService languageService, AuthoringHelper helper)
      : base(languageService, helper){
      ErrorHandler errorHandler = new ErrorHandler(new ErrorNodeList(0));
      this.typeSystem = new TypeSystem(errorHandler);
      this.resolver = new Resolver(errorHandler, this.typeSystem);
    }
    protected override MemberBinding GetMemberBinding(Node n){
      MemberBinding mb = base.GetMemberBinding(n);
      if (mb != null) return mb;
      return null;
    }
    public override Cci.Declarations GetDeclarations(int line, int col, ParseReason reason){
      this.suppressAttributeSuffix = false;
      Scope scope;
      Node node;
      MemberList members = this.GetMembers(line, col, reason, out node, out scope);
#if Xaml
      if (members == null || members.Length == 0){
        LanguageService ls = this.languageService as LanguageService;
        if (ls == null) return null;
        CompilationUnit cu = ls.partialCompilationUnit;
        if (cu != null && cu.Nodes != null && cu.Nodes.Length == 2 && cu.Nodes[1] is Microsoft.XamlCompiler.XamlElement)          
          return new Microsoft.XamlCompiler.Declarations(line, col, ls.partialCompilationUnit, ls.culture);
      }
#endif
      if (members == null) members = new MemberList();
      Cci.AuthoringHelper helper = this.helper;
      if (this.suppressAttributeSuffix){
        helper = this.languageService.GetAuthoringHelper();
        if (helper != null) helper.SuppressAttributeSuffix = true;
      }
      return new Declarations(members, helper, node, scope);
    }

    public override bool MemberSatisfies(Member memb, int identContext) {
      if (memb is Namespace)
        return IdentifierContexts.IsActive(identContext, IdentifierContexts.TypeContext | IdentifierContexts.AttributeContext | IdentifierContexts.ParameterContext | IdentifierContexts.EventContext);
      if (memb is TypeNode) {
        if (IdentifierContexts.IsActive(identContext, IdentifierContexts.TypeContext|IdentifierContexts.ParameterContext))
          return true;
        TypeNode typeNode = memb as TypeNode;
        if (typeNode.IsAssignableTo(SystemTypes.Attribute))
          return IdentifierContexts.IsActive(identContext, IdentifierContexts.AttributeContext);
        if (typeNode.IsAssignableTo(SystemTypes.Delegate))
          return IdentifierContexts.IsActive(identContext, IdentifierContexts.EventContext);
        return false;
      }
      if (memb is Field)
        return IdentifierContexts.IsActive(identContext, IdentifierContexts.VariableContext);
      if (memb is Method)
        return IdentifierContexts.IsActive(identContext, IdentifierContexts.VariableContext);
      if (memb is Event)
        return IdentifierContexts.IsActive(identContext, IdentifierContexts.VariableContext);
      if (memb is Property)
        return IdentifierContexts.IsActive(identContext, IdentifierContexts.VariableContext);
      return identContext == IdentifierContexts.AllContext;
    }
    protected override MemberList GetMembers(int line, int col, AttributeNode attrNode, Scope scope) {
      if (attrNode == null || scope == null) return null;
      this.suppressAttributeSuffix = true;
      return base.GetMembers(line, col, attrNode, scope);
    }
    protected override MemberList GetMembers(int line, int col, Node node, Scope scope){
      KeywordCompletionList keywordList = node as KeywordCompletionList;
      if (keywordList != null)
        return new MemberList(keywordList.KeywordCompletions);
      else
        return base.GetMembers(line, col, node, scope);
    }
    protected override MemberList GetMembers(int line, int col, QualifiedIdentifier qualId, Scope scope){
      this.suppressAttributeSuffix = scope is AttributeScope;
      return base.GetMembers(line, col, qualId, scope);
    }
    protected override TypeNode GetRootType(TypeNode type) {
      return this.typeSystem.GetRootType(type, null);
    }
    public override Overloads GetMethods(int line, int col, Expression name) {
      Overloads ol = base.GetMethods(line, col, name);
      if (ol != null && ol.GetCount() > 0 && ol.OverloadKind == OverloadKind.AttributeConstructors) {
        ol = new AttributeOverloads(ol);
      }
      return ol;
    }
  }
  public class AttributeOverloads : Cci.Overloads {
    public AttributeOverloads(Cci.Overloads overloads)
      : base(overloads){
    }
    MemberList GetNamedParameterList(int index) {
      InstanceInitializer ii = this.members[index] as InstanceInitializer;
      return ii == null ? null : ii.GetAttributeConstructorNamedParameters();
    }
    public override int GetParameterCount(int index) {
      MemberList ml = this.GetNamedParameterList(index);
      return base.GetParameterCount(index) + ((ml != null && ml.Count > 0)? 1:0);
    }
    public override void GetParameterInfo(int index, int parameter, out string name, out string display, out string description) {
      int realParameterCount = base.GetParameterCount(index);
      if (parameter == realParameterCount) {
        MemberList ml = this.GetNamedParameterList(index);
        Debug.Assert(ml != null && ml.Count > 0);
        name = SpecSharpErrorNode.ResourceManager.GetString("NamedParameters", CultureInfo.CurrentCulture);
        display = name;
        StringBuilder sb = new StringBuilder();
        int n = ml.Count;
        for (int i = 0; i < n; ++i) {
          Member m = ml[i];
          if (m == null) continue;
          TypeNode t = null;
          Field f = m as Field;
          if ( f != null )
            t = f.Type;
          Property p = m as Property;
          if ( p != null )
            t = p.Type;
          string descr = MemberNameBuilder.GetMemberName(t,
            MemberNameOptions.Namespace | MemberNameOptions.EnclosingType | MemberNameOptions.Keywords
            | MemberNameOptions.SmartNamespaceName | MemberNameOptions.TemplateArguments | MemberNameOptions.TemplateParameters,
            this.scope);
          if (descr == null || descr.Equals(""))
            descr = m.HelpText;
          sb.Append(m.Name + " = " + descr+"\n");
        }
        description = sb.ToString();
        return;
      }
      base.GetParameterInfo(index, parameter, out name, out display, out description);
    }
  }
  public sealed class Declarations : Cci.Declarations{
    public Declarations(MemberList memberList, Cci.AuthoringHelper helper, Node node, Scope scope) 
      : base(memberList, helper, node, scope){
    }
    protected override string GetDisplayText(Member m) {
      MemberNameOptions mno = MemberNameOptions.SmartNamespaceName | MemberNameOptions.SmartClassName | MemberNameOptions.TemplateArguments | MemberNameOptions.TemplateInfo | MemberNameOptions.AtPrefix;
      Construct cons = node as Construct;
      if (cons != null) {
        QualifiedIdentifier qual = cons.Constructor as QualifiedIdentifier;
        //  not a nested type
        if (qual == null)
          mno |= MemberNameOptions.Namespace | MemberNameOptions.EnclosingType;
      }
      Identifier id = node as Identifier;
      if (id != null) {
        mno |= MemberNameOptions.Namespace | MemberNameOptions.EnclosingType;
      }
      if (this.helper.SuppressAttributeSuffix)
        mno |= MemberNameOptions.SupressAttributeSuffix;
      return MemberNameBuilder.GetMemberName(m, mno, scope);
    }
    protected override string GetInsertionText(Member m) {
      MemberNameOptions mno = MemberNameOptions.SmartNamespaceName | MemberNameOptions.SmartClassName | MemberNameOptions.TemplateArguments | MemberNameOptions.AtPrefix;
      Construct cons = node as Construct;
      if (cons != null) {
        QualifiedIdentifier qual = cons.Constructor as QualifiedIdentifier;
        //  not a nested type
        if (qual == null)
          mno |= MemberNameOptions.Namespace | MemberNameOptions.EnclosingType;
      }
      Identifier id = node as Identifier;
      if (id != null) {
        mno |= MemberNameOptions.Namespace | MemberNameOptions.EnclosingType;
      }
      if (this.helper.SuppressAttributeSuffix)
        mno |= MemberNameOptions.SupressAttributeSuffix;
      return MemberNameBuilder.GetMemberName(m, mno, scope);
    }
  }
  public sealed class AuthoringHelper : Cci.AuthoringHelper{

    public AuthoringHelper(ErrorHandler errorHandler, CultureInfo culture)
      : base(errorHandler, culture){
    }

    public override string GetParameterDescription(Parameter parameter, Scope scope) {
      MemberNameOptions options =
        MemberNameOptions.PutSignature
        | MemberNameOptions.PutReturnType
        | MemberNameOptions.PutParameterName
        | MemberNameOptions.PutParameterModifiers
        | MemberNameOptions.Keywords
        | MemberNameOptions.EnclosingType
        | MemberNameOptions.Namespace
        | MemberNameOptions.SmartNamespaceName
        | MemberNameOptions.TemplateArguments
        | MemberNameOptions.TemplateParameters
        | MemberNameOptions.AtPrefix
        | MemberNameOptions.PutMethodConstraints;
      return MemberNameBuilder.GetParameterTypeName(parameter, options, scope);
    }
    public override string GetSignature(Member member, Scope scope) {
      MemberNameOptions options =
        MemberNameOptions.PutSignature
        | MemberNameOptions.PutReturnType
        | MemberNameOptions.PutParameterName
        | MemberNameOptions.PutParameterModifiers
        | MemberNameOptions.Keywords
        | MemberNameOptions.EnclosingType
        | MemberNameOptions.Namespace
        | MemberNameOptions.SmartNamespaceName
        | MemberNameOptions.TemplateArguments
        | MemberNameOptions.TemplateParameters
        | MemberNameOptions.AtPrefix
        | MemberNameOptions.PutMethodConstraints;
      return MemberNameBuilder.GetMemberName(member, options, scope);
    }
    public override String GetDescription(Member member, int overloads) {
      Field f = member as Field;
      if (f != null && f.DeclaringType == null && f.IsCompilerControlled) return "";
      return base.GetDescription(member, overloads);
    }
  }
}

