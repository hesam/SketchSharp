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
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using TypeAttributes = System.Reflection.TypeAttributes;

namespace Microsoft.SpecSharp {
  [ComVisible(true),Guid("848b76e1-d650-4625-832e-f9c9eb2fb96f"), System.ComponentModel.DesignerCategory("code")]
  public class SpecSharpCodeProvider: CodeDomProvider{
#if WHIDBEY
    [Obsolete]
    public override ICodeGenerator CreateGenerator(){
      return new Compiler();
    }
    [Obsolete]
    public override ICodeCompiler CreateCompiler(){
      return new Compiler();
    }
    public override string FileExtension{
      get {return "ssc"; }
    }
    [Obsolete]
    public override ICodeParser CreateParser() {
      throw new NotImplementedException(); //TODO: figure out what this is used for
    }
#else
    public override ICodeGenerator CreateGenerator() {
      return new Compiler();
    }
    public override ICodeCompiler CreateCompiler() {
      return new Compiler();
    }
    public override string FileExtension {
      get { return "ssc"; }
    }
    public override ICodeParser CreateParser(){
      throw new NotImplementedException(); //TODO: figure out what this is used for
    }
#endif
    public override LanguageOptions LanguageOptions{
      get {return LanguageOptions.None;}
    }
  }
  public interface IPlugin{
    Visitor CreateVisitor(SpecSharpCompilation ssCompilation, TypeSystem typeSystem);
  }
  /// <summary>
  /// Holds compilation state that doesn't fit into a Cci.Compilation.
  /// </summary>
  public class SpecSharpCompilation{
    /// <summary>
    /// A list of Visitor instances.
    /// </summary>
    ArrayList plugins;
    internal Analyzer analyzer;

    public Cci.Analyzer Analyzer { 
      get { return this.analyzer; } 
    }

    public void AddProgramVerifierPlugin(TypeSystem typeSystem, Compilation compilation){
#if Exp
      string boogieDir = System.Environment.GetEnvironmentVariable("BOOGIE");
      if (boogieDir == null)
        boogieDir = "C:\\boogie";
      string boogiePlugin = boogieDir + "\\Binaries\\BoogiePlugin.dll";
      string errorInfo = boogiePlugin + " (Set BOOGIE environment variable)";
#else
      string codebase = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
      codebase = codebase.Replace("#", "%23");
      Uri codebaseUri = new Uri(codebase);
      Uri uri = new Uri(codebaseUri, "BoogiePlugin.dll");
      string boogiePlugin = uri.LocalPath;
      string errorInfo = boogiePlugin;
#endif
      this.AddPlugin(boogiePlugin, "Microsoft.Boogie.BoogiePlugin", "Microsoft.Boogie.BoogiePlugin from assembly " + errorInfo, typeSystem, compilation);
    }
    public void AddPlugin(string assemblyFile, string typeName, string errorInfo, TypeSystem typeSystem, Node offendingNode){
      try{
        System.Reflection.Assembly assembly = System.Reflection.Assembly.LoadFrom(assemblyFile);
        Type pType = assembly.GetType(typeName);
        if (pType == null){
          ((ErrorHandler)typeSystem.ErrorHandler).HandleError(offendingNode, Error.CouldNotLoadPluginType, errorInfo);
          return;
        }
        this.AddPlugin(pType, typeSystem, offendingNode);
      }catch{
        ((ErrorHandler)typeSystem.ErrorHandler).HandleError(offendingNode, Error.CouldNotInstantiatePluginType, errorInfo);
      }
    }
    public void AddPlugin(TypeNode pluginType, TypeSystem typeSystem, Node offendingNode){
      if (pluginType == null || typeSystem == null || typeSystem.ErrorHandler == null || offendingNode == null){Debug.Assert(false); return;}
      Type pType = pluginType.GetRuntimeType();
      if (pType == null){
        ((ErrorHandler)typeSystem.ErrorHandler).HandleError(offendingNode, Error.CouldNotLoadPluginType, typeSystem.ErrorHandler.GetTypeName(pluginType));
        return;
      }
      this.AddPlugin(pType, typeSystem, offendingNode);
    }
    public void AddPlugin(Type pluginType, TypeSystem typeSystem, Node offendingNode){
      if (pluginType == null || typeSystem == null || typeSystem.ErrorHandler == null || offendingNode == null){Debug.Assert(false); return;}
      if (this.plugins == null) this.plugins = new ArrayList();
      try{
        IPlugin plugin = Activator.CreateInstance(pluginType) as IPlugin;
        if (plugin == null){
          ((ErrorHandler)typeSystem.ErrorHandler).HandleError(offendingNode, Error.PluginTypeMustImplementIPlugin, pluginType.FullName);
          return;
        }
        this.plugins.Add(plugin.CreateVisitor(this, typeSystem));
      }catch{
        ((ErrorHandler)typeSystem.ErrorHandler).HandleError(offendingNode, Error.CouldNotInstantiatePluginType, pluginType.FullName);
      }
    }
    class VisitNodeClosure{
      Visitor visitor;
      Node node;
      public VisitNodeClosure(Visitor visitor, Node node){
        this.visitor = visitor;
        this.node = node;
      }
      public void Run(){
        visitor.Visit(node);
      }
    }
    public void RunPlugins(Node node, ErrorHandler errorHandler){
      if (this.plugins == null) return;
      foreach (Visitor pluginVisitor in this.plugins){
        if (pluginVisitor == null){Debug.Assert(false); continue;}
        try{
          pluginVisitor.Visit(node);
        }catch(Exception e){
          errorHandler.HandleError(node, Error.PluginCrash, pluginVisitor.GetType().ToString(), e.Message, e.StackTrace);
        }
      }
    }
    public void RunPlugins(Compilation compilation, ErrorHandler errorHandler){
      if (this.plugins == null) return;
      foreach (Visitor pluginVisitor in this.plugins){
        if (pluginVisitor == null){Debug.Assert(false); continue;}
        try {
          pluginVisitor.Visit(compilation);
        }catch(Exception e){
          errorHandler.HandleError(compilation, Error.PluginCrash, pluginVisitor.GetType().ToString(), e.Message, e.StackTrace);
        }
      }
    }
  }
  public class Compiler : Cci.Compiler, System.CodeDom.Compiler.ICodeGenerator{
    public bool CompileAsXaml;

    public Compiler(){
    }

    #region FrameworkOverrides
    public override void CompileParseTree(Compilation compilation, ErrorNodeList errorNodes){
      if (compilation == null || compilation.CompilationUnits == null || compilation.TargetModule == null){Debug.Assert(false); return;}
      if (compilation.CompilationUnits.Count == 0) return;
      TrivialHashtable ambiguousTypes = new TrivialHashtable();
      TrivialHashtable referencedLabels = new TrivialHashtable();
      TrivialHashtable scopeFor = new TrivialHashtable(64);
      ErrorHandler errorHandler = new ErrorHandler(errorNodes);
      SpecSharpCompilation ssCompilation = new SpecSharpCompilation();
      SpecSharpCompilerOptions options = (SpecSharpCompilerOptions)compilation.CompilerParameters;

      //Attach scopes to namespaces and types so that forward references to base types can be looked up in the appropriate namespace scope
      Scoper scoper = new Scoper(scopeFor);
      scoper.VisitCompilation(compilation);
      scoper = null;

      if (options.NoStandardLibrary && compilation.TargetModule is AssemblyNode) {
        if (compilation.TargetModule.IsValidTypeName(StandardIds.System, StandardIds.CapitalObject)) {
          SystemAssemblyLocation.ParsedAssembly = (AssemblyNode)compilation.TargetModule;
          SystemCompilerRuntimeAssemblyLocation.ParsedAssembly = (AssemblyNode)compilation.TargetModule; //So that mscorlib can have contracts but no reference to another assembly
        } else if (compilation.TargetModule.IsValidTypeName(Identifier.For("System.Compiler"), Identifier.For("ComposerAttribute")))
          SystemCompilerRuntimeAssemblyLocation.ParsedAssembly = (AssemblyNode)compilation.TargetModule;
        else if (compilation.TargetModule.IsValidTypeName(Identifier.For("Microsoft.SpecSharp"), Identifier.For("dummy")))
          RuntimeAssemblyLocation.ParsedAssembly = (AssemblyNode)compilation.TargetModule;
      }
      object ObjectType = SystemTypes.Object;
      if (ObjectType == null) return; //system types did not initialize

      //Walk IR looking up names
      Looker looker = new Looker(compilation.GlobalScope, errorHandler, scopeFor, ambiguousTypes, referencedLabels);
      if (options != null && options.EmitSourceContextsOnly)
      {
        looker.DontInjectDefaultConstructors = true;
      }

      // begin change by drunje
      looker.AllowPointersToManagedStructures = options.AllowPointersToManagedStructures;
      // end change by drunje
      looker.VisitCompilation(compilation);
      looker = null;

      if (options != null && options.EmitSourceContextsOnly) return; // stop after looker to have resolved types

      //Walk IR inferring types and resolving overloads
      TypeSystem typeSystem = new TypeSystem(errorHandler);
      Resolver resolver = new Resolver(errorHandler, typeSystem);
      resolver.VisitCompilation(compilation);
      resolver = null;

      //Walk IR checking for semantic errors and repairing it so that the next walk will work
      Checker checker = new Checker(ssCompilation, errorHandler, typeSystem, scopeFor, ambiguousTypes, referencedLabels);
      checker.VisitCompilation(compilation);
      checker = null;
      scopeFor = null;
      ambiguousTypes = null;
      referencedLabels = null;

      if (!options.IsContractAssembly) {
        if (options.RunProgramVerifier)
          ssCompilation.AddProgramVerifierPlugin(typeSystem, compilation);

        //Allow third party extensions to analyze AST IR for further errors
        ssCompilation.RunPlugins(compilation, errorHandler);
      }

      //Walk IR reducing it to nodes that have predefined mappings to MD+IL
      Normalizer normalizer = new Normalizer(typeSystem);
      normalizer.VisitCompilation(compilation);
      normalizer = null;

      if (options.IsContractAssembly) return;
      //Walk normalized IR instrumenting accesses of fields of guarded classes with checks
      CompilationUnit cu = compilation.CompilationUnits[0];
      if (cu != null && cu.PreprocessorDefinedSymbols != null && cu.PreprocessorDefinedSymbols.ContainsKey("GuardedFieldAccessChecks")){
        if (errorNodes.Count == 0){
          GuardedFieldAccessInstrumenter instrumenter = new GuardedFieldAccessInstrumenter();
          instrumenter.VisitCompilation(compilation);
          instrumenter = null;
        }
      }

      //Walk normalized IR doing code analysis
      Analyzer analyzer = new Analyzer(typeSystem, compilation);
      analyzer.Visit(compilation);

      //Allow third party extensions to analyze normalized IR for further errors
      ssCompilation.analyzer = analyzer; // make the analyzer available to plugins for access to method CFGs
      ssCompilation.RunPlugins(compilation, errorHandler);
      ssCompilation.analyzer = null;
      ssCompilation = null;
      analyzer = null;
      errorHandler = null;

      //Walk IR to optimize code further after analyses were performed, eg. to remove debug only code
      Optimizer optimizer = new Optimizer();
      optimizer.Visit(compilation);
      optimizer = null;
    }
    public override Node CompileParseTree(Node node, Scope scope, Module targetModule, ErrorNodeList errorNodes){
      TrivialHashtable ambiguousTypes = new TrivialHashtable();
      TrivialHashtable referencedLabels = new TrivialHashtable();
      TrivialHashtable scopeFor = new TrivialHashtable();
      ErrorHandler errorHandler = new ErrorHandler(errorNodes);
      SpecSharpCompilation ssCompilation = new SpecSharpCompilation();

      // Setting the state
      TypeNode thisType = null;
      Method   currentMethod = null;
      BlockScope blockScope = scope as BlockScope;
      if (blockScope != null){
        Class baseScope = blockScope;
        MethodScope methodScope = null;
        while (baseScope != null){
          methodScope = baseScope.BaseClass as MethodScope;
          if (methodScope != null) break;
          baseScope = baseScope.BaseClass;
        }
        if (methodScope != null){
          thisType = methodScope.ThisType;
          if (thisType == null && methodScope.BaseClass is TypeScope){
            thisType = ((TypeScope) methodScope.BaseClass).Type;

          }
          currentMethod = methodScope.DeclaringMethod;
        }
      }

      //Attach scope to namespaces and types
      scopeFor[node.UniqueKey] = scope;
      Scoper scoper = new Scoper(scopeFor);
      scoper.currentScope = scope;
      node = scoper.Visit(node);

      //Walk IR looking up names
      Looker looker = new Looker(scope, errorHandler, scopeFor, ambiguousTypes, referencedLabels);
      // begin change by drunje (this is called from debugger only)
      looker.AllowPointersToManagedStructures = true;
      // end change by drunje
      if (blockScope != null)
      {
        looker.currentType = thisType;
        looker.currentMethod = currentMethod;
      }
      looker.currentAssembly = targetModule as AssemblyNode;
      looker.currentModule = targetModule;
      node = looker.Visit(node);
      
      //Walk IR inferring types and resolving overloads
      TypeSystem typeSystem = new TypeSystem(errorHandler);
      Resolver resolver = new Resolver(errorHandler, typeSystem);
      if (blockScope != null){
        resolver.currentType = thisType;
        resolver.currentMethod = currentMethod;
      }
      resolver.currentAssembly = targetModule as AssemblyNode;
      resolver.currentModule = targetModule;
      node = resolver.Visit(node);
      
      //TODO:  Need to set the state of the checker for compiling Expression, STOP using this method when the shift is complete
      //Walk IR checking for semantic errors and repairing it so that the next walk will work
      Checker checker = new Checker(ssCompilation, errorHandler, typeSystem, scopeFor, ambiguousTypes, referencedLabels);
      if (blockScope != null){
        checker.currentType = thisType;
        checker.currentMethod = currentMethod;
      }
      checker.currentAssembly = targetModule as AssemblyNode;
      checker.currentModule = targetModule;
      node = checker.Visit(node);

      //Walk IR reducing it to nodes that have predefined mappings to MD+IL
      Normalizer normalizer = new Normalizer(typeSystem);
      if (blockScope != null){
        normalizer.currentType = thisType;
        normalizer.currentMethod = currentMethod;
        normalizer.WrapToBlockExpression = false;
      }
      normalizer.currentModule = targetModule;
      node = normalizer.Visit(node);

      return node;
    }

    public override void AddStandardLibraries(CompilerOptions coptions, Module module, TrivialHashtable alreadyReferencedAssemblies){
      if (coptions.NoStandardLibrary) return;
      base.AddStandardLibraries(coptions, module, alreadyReferencedAssemblies);
      SpecSharpCompilerOptions ssoptions = coptions as SpecSharpCompilerOptions;
      if (ssoptions != null && ssoptions.Compatibility) return;
      if (Microsoft.SpecSharp.Runtime.RuntimeAssembly != null && alreadyReferencedAssemblies[Microsoft.SpecSharp.Runtime.RuntimeAssembly.UniqueKey] == null){
        AssemblyReference aref = new AssemblyReference(Microsoft.SpecSharp.Runtime.RuntimeAssembly);
        module.AssemblyReferences.Add(aref);
        if (aref.PublicKeyOrToken == null || aref.PublicKeyOrToken.Length == 0)
          this.AssemblyCache[aref.Name] = Microsoft.SpecSharp.Runtime.RuntimeAssembly;
      }
    }
    public override void AddExtendedRuntimeLibrary(CompilerOptions coptions, Module module, TrivialHashtable alreadyReferencedAssemblies){
      SpecSharpCompilerOptions ssoptions = coptions as SpecSharpCompilerOptions;
      if (ssoptions != null && ssoptions.Compatibility) return;
      base.AddExtendedRuntimeLibrary(coptions, module, alreadyReferencedAssemblies);
    }
    public override ErrorNode CreateErrorNode(Cci.Error error, params string[] messageParameters){
      Error e = ErrorHandler.MapError(error);
      if (e == Error.None) return null;
      if (e == Error.UnexpectedToken) return base.CreateErrorNode(error, messageParameters);
      return new SpecSharpErrorNode(e, messageParameters);
    }
    public override ErrorNode CreateErrorNode(Cci.Error error, Method method){
      Error e = ErrorHandler.MapError(error);
      if (e == Error.None) return null;
      if (e == Error.UnexpectedToken) return base.CreateErrorNode(error, method);
      ErrorNode result = new SpecSharpErrorNode(e, (new ErrorHandler(new ErrorNodeList())).GetMethodSignature(method));
      result.SourceContext = method.Name.SourceContext;
      return result;
    }
    public override void HandleMultipleMainMethodError(Method mainMethod, CompilerParameters options, CompilerResults results){
      if (!options.GenerateExecutable) return;
      base.HandleMultipleMainMethodError(mainMethod, options, results);
    }
    public override bool IsWarningRelatedMessage(ErrorNode enode){
      if (enode == null) return false;
      return enode.Code == (int)Error.RelatedWarningLocation || enode.Code == (int)Error.RelatedWarningModule;
    }
    public override CompilationUnitSnippet CreateCompilationUnitSnippet(string fileName, int lineNumber, DocumentText text, Compilation compilation){
      if (fileName == null) return null;
      SpecSharpCompilerOptions options = compilation == null ? null : (compilation.CompilerParameters as SpecSharpCompilerOptions);
      if (options != null && options.Compatibility)
        return base.CreateCompilationUnitSnippet(fileName, lineNumber, text, compilation);
#if Xaml
      if (this.CompileAsXaml || string.Compare(Path.GetExtension(fileName), ".xaml", true, CultureInfo.InvariantCulture) == 0){
        Document doc = Microsoft.XamlCompiler.Compiler.CreateXamlDocument(fileName, 1, text);
        CompilationUnitSnippet cu = new CompilationUnitSnippet();
        cu.Name = Identifier.For(doc.Name);
        cu.SourceContext = new SourceContext(doc);
        cu.ParserFactory = new XamlParserFactory();
        cu.Compilation = compilation;
        return cu;
      }else
#endif
        return base.CreateCompilationUnitSnippet(fileName, lineNumber, text, compilation);
    }
    public override CompilerResults CompileAssemblyFromSource(CompilerParameters options, string source, ErrorNodeList errorNodes){
      if (!this.CompileAsXaml)
        return base.CompileAssemblyFromSource(options, source, errorNodes);
      AssemblyNode assem = this.CreateAssembly(options, errorNodes);
      Compilation compilation = new Compilation();
      compilation.TargetModule = assem;
      compilation.CompilerParameters = options;
      compilation.CompilationUnits = new CompilationUnitList(this.CreateCompilationUnitSnippet("", 1, new DocumentText(source), compilation));
      SnippetParser sp = this.CreateSnippetParser(assem, errorNodes, options);
      sp.Visit(compilation);
      this.CompileParseTree(compilation, errorNodes);
      CompilerResults results = new CompilerResults(options.TempFiles);
      this.ProcessErrors(options, results, errorNodes);
      SpecSharpCompilerOptions ssco = options as SpecSharpCompilerOptions;
      if (ssco == null || !ssco.OnlyTypeChecks) {
        if (results.NativeCompilerReturnValue == 0)
          this.SetEntryPoint(compilation, results);
        this.SaveCompilation(compilation, assem, options, results, errorNodes);
      }
      return results;
    }
    public override Document CreateDocument(string fileName, int lineNumber, DocumentText text){
      return Compiler.CreateSpecSharpDocument(fileName, lineNumber, text);
    }
    public static Document CreateSpecSharpDocument(string fileName, int lineNumber, DocumentText text){
      //return new Document(fileName, lineNumber, text, SymDocumentType.Text, typeof(DebuggerLanguage).GUID, SymLanguageVendor.Microsoft);
      return new Document(fileName, lineNumber, text, SymDocumentType.Text, SymLanguageType.CSharp, SymLanguageVendor.Microsoft);
    }
    public override Document CreateDocument(string fileName, int lineNumber, string text){
      //return new Document(fileName, lineNumber, text, SymDocumentType.Text, typeof(DebuggerLanguage).GUID, SymLanguageVendor.Microsoft);
      return new Document(fileName, lineNumber, text, SymDocumentType.Text, SymLanguageType.CSharp, SymLanguageVendor.Microsoft);
    }
    public override IParser CreateParser(string fileName, int lineNumber, DocumentText text, Module symbolTable, ErrorNodeList errors, CompilerParameters options){
      Document document = this.CreateDocument(fileName, lineNumber, text);
      return new Parser(document, errors, symbolTable, options as SpecSharpCompilerOptions);
    }
    public override CompilerOptions CreateCompilerOptions(){
      return new SpecSharpCompilerOptions();
    }
    /// <summary>
    /// Parses all of the CompilationUnitSnippets in the given compilation, ignoring method bodies. Then resolves all type expressions.
    /// The resulting types can be retrieved from the module in compilation.TargetModule. The base types, interfaces and 
    /// member signatures will all be resolved and on an equal footing with imported, already compiled modules and assemblies.
    /// </summary>
    public override void ConstructSymbolTable(Compilation compilation, ErrorNodeList errors){
      this.ConstructSymbolTable(compilation, errors, new TrivialHashtable());
    }
    public virtual void ConstructSymbolTable(Compilation compilation, ErrorNodeList errors, TrivialHashtable scopeFor){
      if (compilation == null || scopeFor == null){Debug.Assert(false); return;}
      this.CurrentCompilation = compilation;
      Module symbolTable = compilation.TargetModule = this.CreateModule(compilation.CompilerParameters, errors, compilation);
      Scoper scoper = new Scoper(scopeFor);
      scoper.currentModule = symbolTable;
      ErrorHandler errorHandler = new ErrorHandler(errors);
      Looker looker = new Looker(this.GetGlobalScope(symbolTable), errorHandler, scopeFor);
      // begin change by drunje
      SpecSharpCompilerOptions options = compilation.CompilerParameters as SpecSharpCompilerOptions;
      if (options != null)
        looker.AllowPointersToManagedStructures = options.AllowPointersToManagedStructures;
      // end change by drunje
      looker.currentAssembly = (looker.currentModule = symbolTable) as AssemblyNode;
      looker.ignoreMethodBodies = true;
      Scope globalScope = compilation.GlobalScope = this.GetGlobalScope(symbolTable);

      CompilationUnitList sources = compilation.CompilationUnits;
      if (sources == null) return;
      int n = sources.Count;
      for (int i = 0; i < n; i++){
        CompilationUnitSnippet compilationUnitSnippet = sources[i] as CompilationUnitSnippet;
        if (compilationUnitSnippet == null){Debug.Assert(false); continue;}
        compilationUnitSnippet.ChangedMethod = null;
        Document doc = compilationUnitSnippet.SourceContext.Document;
        if (doc == null || doc.Text == null){Debug.Assert(false); continue;}
        IParserFactory factory = compilationUnitSnippet.ParserFactory;
        if (factory == null){Debug.Assert(false); return;}
        IParser p = factory.CreateParser(doc.Name, doc.LineNumber, doc.Text, symbolTable, errors, compilation.CompilerParameters);
        if (p is ResgenCompilerStub) continue;
        if (p == null){Debug.Assert(false); continue;}
        Parser specSharpParser = p as Parser;
        if (specSharpParser == null)
          p.ParseCompilationUnit(compilationUnitSnippet);
        else
          specSharpParser.ParseCompilationUnit(compilationUnitSnippet, true, false);
        //TODO: this following is a good idea only if the files will not be frequently reparsed from source
        //StringSourceText stringSourceText = doc.Text.TextProvider as StringSourceText;
        //if (stringSourceText != null && stringSourceText.IsSameAsFileContents)
        //  doc.Text.TextProvider = new CollectibleSourceText(doc.Name, doc.Text.Length);
        //else if (doc.Text.TextProvider != null)
        //  doc.Text.TextProvider.MakeCollectible();
      }
      CompilationUnitList compilationUnits = new CompilationUnitList();
      for (int i = 0; i < n; i++){
        CompilationUnit cUnit = sources[i];
        compilationUnits.Add(scoper.VisitCompilationUnit(cUnit));
      }
      for (int i = 0; i < n; i++){
        CompilationUnit cUnit = compilationUnits[i];
        if (cUnit == null) continue;
        looker.VisitCompilationUnit(cUnit);
      }
      //Run resolver over symbol table so that custom attributes on member signatures are known and can be used
      //to error check the the given file.
      TypeSystem typeSystem = new TypeSystem(errorHandler);
      Resolver resolver = new Resolver(errorHandler, typeSystem);
      resolver.currentAssembly = (resolver.currentModule = symbolTable) as AssemblyNode;
      for (int i = 0; i < n; i++) {
        CompilationUnit cUnit = compilationUnits[i];
        if (cUnit == null) continue;
        resolver.VisitCompilationUnit(cUnit);
      }
      this.CurrentCompilation = null;
    }

      public override void ProcessErrors(CompilerParameters options, CompilerResults results, ErrorNodeList errorNodes)
      {
          if(errorNodes != null &&
            (options is SpecSharpCompilerOptions && 
            ((SpecSharpCompilerOptions)options).RunProgramVerifier))
              errorNodes.Sort(0, errorNodes.Count);  // sort the Boogie errors to make test deterministic
          base.ProcessErrors(options, results, errorNodes);
      }


    /// <summary>
    /// Resolves all type expressions in the given (already parsed) compilation.
    /// The base types, interfaces and member signatures will all be on an equal footing with signatures from imported, 
    /// already compiled modules and assemblies.
    /// </summary>
    public override void ResolveSymbolTable(Compilation/*!*/ parsedCompilation, ErrorNodeList/*!*/ errors){
      TrivialHashtable scopeFor;
      this.ResolveSymbolTable(parsedCompilation, errors, out scopeFor);
    }
    public virtual void ResolveSymbolTable(Compilation/*!*/ parsedCompilation, ErrorNodeList/*!*/ errors, out TrivialHashtable scopeFor){
      scopeFor = new TrivialHashtable();
      if (parsedCompilation == null) { Debug.Assert(false); return; }
      if (errors == null){Debug.Assert(false); return;}
      Scoper scoper = new Scoper(scopeFor);
      scoper.currentModule = parsedCompilation.TargetModule;
      scoper.VisitCompilation(parsedCompilation);
      ErrorHandler errorHandler = new ErrorHandler(errors);
      TrivialHashtable ambiguousTypes = new TrivialHashtable();
      TrivialHashtable referencedLabels = new TrivialHashtable();
      Looker looker = new Looker(null, errorHandler, scopeFor, ambiguousTypes, referencedLabels);
      // begin change by drunje
      SpecSharpCompilerOptions options = parsedCompilation.CompilerParameters as SpecSharpCompilerOptions;
      if (options != null)
        looker.AllowPointersToManagedStructures = options.AllowPointersToManagedStructures;
      // end change by drunje
      looker.currentAssembly = (looker.currentModule = parsedCompilation.TargetModule) as AssemblyNode;
      looker.VisitCompilation(parsedCompilation);
    }
    public override void UpdateRuntimeAssemblyLocations(CompilerOptions coptions){
      if (coptions == null) return;
      base.UpdateRuntimeAssemblyLocations(coptions);
      if (coptions.NoStandardLibrary){
        for (int i = 0, n = coptions.ReferencedAssemblies == null ? 0 : coptions.ReferencedAssemblies.Count; i < n; i++){
          string aref = coptions.ReferencedAssemblies[i];
          if (aref == null) continue;
          aref = Path.GetFileName(aref);
          aref = aref.ToLower(CultureInfo.InvariantCulture);
          if (aref == "microsoft.specsharp.runtime.dll"){
            RuntimeAssemblyLocation.Location = this.GetFullyQualifiedNameForReferencedLibrary(coptions, coptions.ReferencedAssemblies[i]);
            continue;
          }
        }
      }
    }
    public override System.Collections.ArrayList GetRuntimeLibraries() {
      System.Collections.ArrayList result = base.GetRuntimeLibraries();
      result.Add(Microsoft.SpecSharp.Runtime.RuntimeAssembly.Location);
      return result;
    }
    protected override void SaveCompilation(Compilation compilation, Module module, CompilerParameters options, CompilerResults results){
      CompilerOptions ccioptions = options as CompilerOptions;
      if (ccioptions != null && ccioptions.EmitSourceContextsOnly) {
        SourceContextWriter.Write(compilation, module, options);
        if (ccioptions.XMLDocFileName != null && ccioptions.XMLDocFileName.Length > 0)
          module.WriteDocumentation(new StreamWriter(ccioptions.XMLDocFileName));
      }
      else{
        base.SaveCompilation(compilation, module, options, results);
      }
    }
    protected override void SaveCompilation(Compilation compilation, AssemblyNode assem, CompilerParameters options, CompilerResults results, ErrorNodeList errorNodes) {
      CompilerOptions ccioptions = options as CompilerOptions;
      if (ccioptions != null && ccioptions.EmitSourceContextsOnly) {
        SourceContextWriter.Write(compilation, assem, options);
        if (ccioptions.XMLDocFileName != null && ccioptions.XMLDocFileName.Length > 0)
          assem.WriteDocumentation(new StreamWriter(ccioptions.XMLDocFileName));
      }
      else{
        base.SaveCompilation (compilation, assem, options, results, errorNodes);
      }
    }

    #endregion FrameworkOverrides
    #region CodeDomCodeGenerator
    //TODO: translate CodeDom tree to IR using System.Compiler.CodeDomTranslator. Translate the result to a source string using a Visitor.
    private const GeneratorSupport LanguageSupport = 
      GeneratorSupport.ArraysOfArrays |
      GeneratorSupport.EntryPointMethod |
      GeneratorSupport.GotoStatements |
      GeneratorSupport.MultidimensionalArrays |
      GeneratorSupport.StaticConstructors |
      GeneratorSupport.TryCatchStatements |
      //GeneratorSupport.ReturnTypeAttributes |
      GeneratorSupport.AssemblyAttributes |
      GeneratorSupport.DeclareValueTypes |
      GeneratorSupport.DeclareEnums | 
      GeneratorSupport.DeclareEvents | 
      GeneratorSupport.DeclareDelegates |
      GeneratorSupport.DeclareInterfaces |
      //GeneratorSupport.ParameterAttributes |
      GeneratorSupport.ReferenceParameters |
      GeneratorSupport.ChainedConstructorArguments |
      GeneratorSupport.NestedTypes |
      GeneratorSupport.MultipleInterfaceMembers |
      GeneratorSupport.PublicStaticMembers |
      GeneratorSupport.ComplexExpressions;

    private TextWriter writer;
    private CodeGeneratorOptions cgOptions;
    private int level;
    private bool inNestedBinary;
    private bool inForLoopHeader;

    bool ICodeGenerator.Supports(GeneratorSupport support){
      return (Compiler.LanguageSupport & support) != 0;
    }
    void ICodeGenerator.GenerateCodeFromExpression(CodeExpression e, TextWriter w, CodeGeneratorOptions o){
      if (e == null || w == null) throw new ArgumentException();
      if (o == null) o = new CodeGeneratorOptions();
      this.writer = w;
      this.cgOptions = o;
      this.level = 0;
      this.Write(e);
    }
    void ICodeGenerator.GenerateCodeFromCompileUnit(CodeCompileUnit e, TextWriter w, CodeGeneratorOptions o){
      if (e == null || w == null) throw new ArgumentException();
      if (o == null) o = new CodeGeneratorOptions();
      this.writer = w;
      this.cgOptions = o;
      this.level = 0;
      this.Write(e);
    }
    void ICodeGenerator.GenerateCodeFromNamespace(CodeNamespace e, TextWriter w, CodeGeneratorOptions o){
      if (e == null || w == null) throw new ArgumentException();
      if (o == null) o = new CodeGeneratorOptions();
      this.writer = w;
      this.cgOptions = o;
      this.level = 0;
      this.Write(e);
    }
    void ICodeGenerator.GenerateCodeFromStatement(CodeStatement e, TextWriter w, CodeGeneratorOptions o){
      if (e == null || w == null) throw new ArgumentException();
      if (o == null) o = new CodeGeneratorOptions();
      this.writer = w;
      this.cgOptions = o;
      this.level = 0;
      this.Write(e);
    }
    void ICodeGenerator.GenerateCodeFromType(CodeTypeDeclaration e, TextWriter w, CodeGeneratorOptions o){
      if (e == null || w == null) throw new ArgumentException();
      if (o == null) o = new CodeGeneratorOptions();
      this.writer = w;
      this.cgOptions = o;
      this.level = 0;
      this.Write(e.Comments);
      this.Write(e.CustomAttributes, false, null);
      if (e is CodeTypeDelegate){
        this.Write((CodeTypeDelegate)e);
        return;
      }
      this.Write(e);
    }
    private Scanner scanner = new Scanner();
    bool ICodeGenerator.IsValidIdentifier(string value){
      if (value == null) return false;
      this.scanner.SetSource(value, 0);
      Token tok = scanner.GetNextToken();
      return tok == Token.Identifier;
    }
    void ICodeGenerator.ValidateIdentifier(string value){
      if (!((ICodeGenerator)this).IsValidIdentifier(value)) throw new ArgumentException();
    }
    string ICodeGenerator.CreateEscapedIdentifier(string value){
      if (value == null) throw new ArgumentException();
      if (!((ICodeGenerator)this).IsValidIdentifier(value)) return "@" + value;
      return value;
    }
    string ICodeGenerator.CreateValidIdentifier(string value){
      if (value == null) throw new ArgumentException();
      if (!((ICodeGenerator)this).IsValidIdentifier(value)) return "_" + value;
      return value;
    }
    string ICodeGenerator.GetTypeOutput(CodeTypeReference type){
      if (type == null) throw new ArgumentException();
      int rank = type.ArrayRank;
      if (rank < 0) throw new ArgumentException();
      if (rank == 0) return this.GetBaseTypeOutput(type.BaseType);
      StringBuilder sb = new StringBuilder(((ICodeGenerator)this).GetTypeOutput(type.ArrayElementType));
      sb.Append('[');
      for (int i = 1; i < rank; i++) sb.Append(',');
      sb.Append(']');
      return sb.ToString();
    }
    private string GetBaseTypeOutput(string s){
      if (s == null) return "void";
      if (s.Length == 0) return "void";
      if (string.Compare(s, "System.Int16", true, CultureInfo.InvariantCulture) == 0) return "short";
      if (string.Compare(s, "System.Int32", true, CultureInfo.InvariantCulture) == 0) return "int";
      if (string.Compare(s, "System.Int64", true, CultureInfo.InvariantCulture) == 0) return"long";
      if (string.Compare(s, "System.String", true, CultureInfo.InvariantCulture) == 0) return "string";
      if (string.Compare(s, "System.Object", true, CultureInfo.InvariantCulture) == 0) return "object";     
      if (string.Compare(s, "System.Boolean", true, CultureInfo.InvariantCulture) == 0) return "bool";
      if (string.Compare(s, "System.Void", true, CultureInfo.InvariantCulture) == 0) return "void";
      if (string.Compare(s, "System.Char", true, CultureInfo.InvariantCulture) == 0) return "char";
      return ((ICodeGenerator)this).CreateEscapedIdentifier(s).Replace('+', '.'); //nested classes
    }
    private void Write(char c) {
      TextWriter w = this.writer;
      w.Write('\'');
      switch (c) {
        case '\r': w.Write("\\r"); break;
        case '\t': w.Write("\\t"); break;
        case '\"': w.Write("\\\""); break;
        case '\'': w.Write("\\\'"); break;
        case '\\': w.Write("\\\\"); break;
        case '\0': w.Write("\\0"); break;
        case '\n': w.Write("\\n"); break;
        case '\u2028': w.Write("\\u2028"); break;
        case '\u2029': w.Write("\\u2029"); break;     
        default: w.Write(c); break;
      }
      w.Write('\'');
    }
    private void Write(CodeArrayCreateExpression e){
      TextWriter w = this.writer;
      w.Write("new ");
      CodeExpressionCollection init = e.Initializers;
      int n = init == null ? 0 : init.Count;
      if (n > 0){
        this.Write(e.CreateType);
        if (e.CreateType.ArrayElementType == null)
          w.Write("[]");
        this.WriteStartingBrace();
        this.level++;
        foreach (CodeExpression cexpr in init){
          this.WriteIndent();
          this.Write(cexpr);
          if (--n > 0) w.WriteLine(",");
        }
        this.level--;
        this.WriteIndent();
        this.writer.Write('}');
      }else{
        w.Write(this.GetBaseTypeOutput(e.CreateType.BaseType));
        w.Write("[");
        if (e.SizeExpression != null) 
          this.Write(e.SizeExpression);
        else
          w.Write(e.Size);
        w.Write("]");
      }
    }
    private void Write(CodeArrayIndexerExpression e){
      TextWriter w = this.writer;
      this.Write(e.TargetObject);
      w.Write('[');
      bool first = true;
      foreach (CodeExpression i in e.Indices){
        if (first) first = false; else w.Write(", ");
        this.Write(i);
      }
      w.Write(']');
    }
    private void Write(CodeAssignStatement e){
      TextWriter w = this.writer;
      this.Write(e.Left);
      w.Write(" = ");
      this.Write(e.Right);
      if (!this.inForLoopHeader) w.WriteLine(';');
    }
    private void Write(CodeAttachEventStatement e){
      TextWriter w = this.writer;
      this.Write(e.Event);
      w.Write(" += ");
      this.Write(e.Listener);
      w.WriteLine(';');
    }
    private void Write(CodeAttributeDeclarationCollection attributes, bool inLine, string prefix){
      if (attributes == null || attributes.Count == 0) return;
      if (!inLine) this.WriteIndent();
      TextWriter w = this.writer;
      w.Write('[');
      if (prefix != null) w.Write(prefix);
      bool first = true;
      foreach (CodeAttributeDeclaration a in attributes){
        if (first) 
          first = false; 
        else if (inLine) 
          w.Write(", "); 
        else{
          w.WriteLine(']'); this.WriteIndent(); w.Write('[');
          if (prefix != null) w.Write(prefix);
        };
        w.Write(this.GetBaseTypeOutput(a.Name));
        if (a.Arguments == null || a.Arguments.Count == 0) continue;
        w.Write('(');
        bool firstArg = true;
        foreach (CodeAttributeArgument arg in a.Arguments){
          if (firstArg) firstArg = false; else w.Write(", ");
          if (arg.Name != null && arg.Name.Length > 0){
            w.Write(arg.Name);
            w.Write(" = ");
          }
          this.Write(arg.Value);
        }
        w.Write(')');
      }
      if (inLine) w.Write("] "); else w.WriteLine(']');
    }
    private void Write(CodeBaseReferenceExpression e){
      this.writer.Write("base");
    }
    private void Write(CodeBinaryOperatorExpression e){
      TextWriter w = this.writer;
      bool increasedIndentLevel = false;
      w.Write("(");
      this.Write(e.Left);
      w.Write(" ");
      if (e.Left is CodeBinaryOperatorExpression || e.Right is CodeBinaryOperatorExpression) {
        if (!this.inNestedBinary) {
          increasedIndentLevel = true;
          this.inNestedBinary = true;
          this.level += 3; //Increase the indent level only the first time indent is called
        }
        w.WriteLine();
        this.WriteIndent();
      }
      this.Write(e.Operator);
      w.Write(" ");
      this.Write(e.Right);
      w.Write(")");
      if (increasedIndentLevel) {
        this.inNestedBinary = false;
        this.level -= 3;
      }
    }
    private void Write(CodeBinaryOperatorType e){
      string op = "";
      switch (e){
        case CodeBinaryOperatorType.Add: op = "+"; break;
        case CodeBinaryOperatorType.Assign: op = "="; break;
        case CodeBinaryOperatorType.BitwiseAnd: op = "&"; break;
        case CodeBinaryOperatorType.BitwiseOr: op = "|"; break;
        case CodeBinaryOperatorType.BooleanAnd: op = "&&"; break;
        case CodeBinaryOperatorType.BooleanOr: op = "||"; break;
        case CodeBinaryOperatorType.Divide: op = "/"; break;
        case CodeBinaryOperatorType.GreaterThan: op = ">"; break;
        case CodeBinaryOperatorType.GreaterThanOrEqual: op = ">="; break;
        case CodeBinaryOperatorType.IdentityEquality: op = "=="; break;
        case CodeBinaryOperatorType.IdentityInequality: op = "!="; break;
        case CodeBinaryOperatorType.LessThan: op = "<"; break;
        case CodeBinaryOperatorType.LessThanOrEqual: op = "<="; break;
        case CodeBinaryOperatorType.Modulus: op = "%"; break;
        case CodeBinaryOperatorType.Multiply: op = "*"; break;
        case CodeBinaryOperatorType.Subtract: op = "-"; break;
        case CodeBinaryOperatorType.ValueEquality: op = "=="; break;
      }
      this.writer.Write(op);
    }
    private void Write(CodeCastExpression e){
      TextWriter w = this.writer;
      w.Write("((");
      this.Write(e.TargetType);
      w.Write(")(");
      this.Write(e.Expression);
      w.Write("))");
    }
    private void Write(CodeCommentStatementCollection e){
      if (e == null) return;
      foreach (CodeCommentStatement comment in e)
        this.Write(comment);
    }
    private void Write(CodeCommentStatement e){
      if (e == null) return;
      this.WriteLinePragmaStart(e.LinePragma);
      this.Write(e.Comment);
      this.writer.WriteLine();
      this.WriteLinePragmaEnd(e.LinePragma);
    }
    private void Write(CodeComment e){
      if (e == null) return;
      TextWriter w = this.writer;
      this.WriteIndent();
      if (e.DocComment) w.Write("/// "); else w.Write("// ");
      string str = e.Text;
      int n = str == null ? 0 : str.Length;
      for (int i=0; i < n; i++){
        char ch = str[i];
        if (ch == 0) continue;
        switch (ch){
          case '\r':
            if (i < n-1 && str[i+1] == '\n'){
              w.Write(ch);
              i++; ch = '\n';
            }
            goto case '\n';
          case '\n':
          case '\u2028':
          case '\u2029':
            w.Write(ch);
            w.Write("//"); 
            break;
          default:
            w.Write(ch); 
            break;
        }
      }
    }
    private void Write(CodeCompileUnit e){
      TextWriter w = this.writer;
      w.WriteLine("//------------------------------------------------------------------------------");
      w.WriteLine("// <autogenerated>");
      w.WriteLine("//     This code was generated by the Spec# Code Generator.");
      w.WriteLine("//     Runtime Version: " + System.Environment.Version.ToString());
      w.WriteLine("//");
      w.WriteLine("//     Changes to this file may cause incorrect behavior and will be lost if ");
      w.WriteLine("//     the code is regenerated.");
      w.WriteLine("// </autogenerated>");
      w.WriteLine("//------------------------------------------------------------------------------");
      w.WriteLine();
      if (e.AssemblyCustomAttributes != null && e.AssemblyCustomAttributes.Count > 0) {
        this.Write(e.AssemblyCustomAttributes, false, "assembly: ");
        w.WriteLine();
      }
      if (e.Namespaces != null)
        foreach (CodeNamespace ns in e.Namespaces)
          this.Write(ns);

      if (e is CodeSnippetCompileUnit)
      {
        CodeSnippetCompileUnit ce = e as CodeSnippetCompileUnit;
        this.WriteLinePragmaStart(ce.LinePragma);
        w.WriteLine(ce.Value);
      }
    }
    private void Write(CodeConditionStatement e){
      TextWriter w = this.writer;
      w.Write("if (");
      this.Write(e.Condition);
      w.Write(')');
      this.WriteStartingBrace();            
      this.Write(e.TrueStatements);
      CodeStatementCollection falseStatements = e.FalseStatements;
      if (falseStatements != null && falseStatements.Count > 0) {
        w.Write("}");
        if (!this.cgOptions.ElseOnClosing){ w.WriteLine(); this.WriteIndent();}
        w.Write("else");
        this.WriteStartingBrace();
        this.Write(falseStatements);
      }
      this.WriteClosingBrace();
    }
    private void Write(CodeConstructor e, string typeName){
      TextWriter w = this.writer;
      this.WriteIdentifier(typeName);
      this.Write(e.Parameters);
      if (e.BaseConstructorArgs != null && e.BaseConstructorArgs.Count > 0){
        w.WriteLine(" :");
        this.level++;
        this.WriteIndent();
        w.Write("base");
        this.Write(e.BaseConstructorArgs);
        this.level--;
      }else if (e.ChainedConstructorArgs != null && e.ChainedConstructorArgs.Count > 0){
        w.WriteLine(" :");
        this.level++;
        this.WriteIndent();
        w.Write("this");
        this.Write(e.ChainedConstructorArgs);
        this.level--;
      }
      this.WriteStartingBrace();
      this.Write(e.Statements);
      this.WriteClosingBrace();
    }
    private void Write(CodeDelegateCreateExpression e){
      TextWriter w = this.writer;
      w.Write("new ");
      this.Write(e.DelegateType);
      w.Write('(');
      this.Write(e.TargetObject);
      w.Write('.');
      this.WriteIdentifier(e.MethodName);
      w.Write(')');
    }
    private void Write(CodeDelegateInvokeExpression e){
      TextWriter w = this.writer;
      this.Write(e.TargetObject);
      this.Write(e.Parameters);
    }
    private void Write(CodeDirectionExpression e){
      TextWriter w = this.writer;
      switch (e.Direction){
        case FieldDirection.Out: w.Write("out "); break;
        case FieldDirection.Ref: w.Write("ref "); break;
      }
      this.Write(e.Expression);
    }
    private void Write(CodeEventReferenceExpression e){
      if (e.TargetObject != null) {
        this.Write(e.TargetObject);
        this.writer.Write('.');
      }
      this.WriteIdentifier(e.EventName);
    }
    private void Write(CodeExpression e){
      if (e is CodeArgumentReferenceExpression) this.WriteIdentifier(((CodeArgumentReferenceExpression)e).ParameterName);
      else if (e is CodeArrayCreateExpression) this.Write((CodeArrayCreateExpression)e);
      else if (e is CodeArrayIndexerExpression) this.Write((CodeArrayIndexerExpression)e);
      else if (e is CodeBaseReferenceExpression) this.Write((CodeBaseReferenceExpression)e);
      else if (e is CodeBinaryOperatorExpression) this.Write((CodeBinaryOperatorExpression)e);
      else if (e is CodeCastExpression) this.Write((CodeCastExpression)e);
      else if (e is CodeDelegateCreateExpression) this.Write((CodeDelegateCreateExpression)e);
      else if (e is CodeDelegateInvokeExpression) this.Write((CodeDelegateInvokeExpression)e);
      else if (e is CodeDirectionExpression) this.Write((CodeDirectionExpression)e);
      else if (e is CodeEventReferenceExpression) this.Write((CodeEventReferenceExpression)e);
      else if (e is CodeFieldReferenceExpression) this.Write((CodeFieldReferenceExpression)e);
      else if (e is CodeIndexerExpression) this.Write((CodeIndexerExpression)e);
      else if (e is CodeMethodInvokeExpression) this.Write((CodeMethodInvokeExpression)e);
      else if (e is CodeMethodReferenceExpression) this.Write((CodeMethodReferenceExpression)e);
      else if (e is CodeObjectCreateExpression) this.Write((CodeObjectCreateExpression)e);
      else if (e is CodePrimitiveExpression) this.Write((CodePrimitiveExpression)e);
      else if (e is CodePropertyReferenceExpression) this.Write((CodePropertyReferenceExpression)e);
      else if (e is CodePropertySetValueReferenceExpression) this.Write((CodePropertySetValueReferenceExpression)e);
      else if (e is CodeSnippetExpression) this.Write((CodeSnippetExpression)e);
      else if (e is CodeThisReferenceExpression) this.Write((CodeThisReferenceExpression)e);
      else if (e is CodeTypeOfExpression) this.Write((CodeTypeOfExpression)e);
      else if (e is CodeTypeReferenceExpression) this.Write((CodeTypeReferenceExpression)e);
      else if (e is CodeVariableReferenceExpression) this.Write((CodeVariableReferenceExpression)e);
    }
    private void Write(CodeExpressionCollection e){
      TextWriter w = this.writer;
      if (e == null){ w.Write("()"); return;}
      w.Write('(');
      bool first = true;
      foreach (CodeExpression expr in e){
        if (first) first = false; else w.Write(", ");
        this.Write(expr);
      }
      w.Write(')');
    }
    private void Write(CodeExpressionStatement e){
      this.Write(e.Expression);
      if (!this.inForLoopHeader) this.writer.WriteLine(';');
    }
    private void Write(CodeFieldReferenceExpression e){
      if (e.TargetObject != null) {
        this.Write(e.TargetObject);
        this.writer.Write('.');
      }
      this.WriteIdentifier(e.FieldName);
    }
    private void Write(CodeGotoStatement e){
      TextWriter w = this.writer;
      w.Write("goto ");
      w.Write(e.Label);
      w.WriteLine(";");
    }
    private void Write(CodeIndexerExpression e){
      TextWriter w = this.writer;
      this.Write(e.TargetObject);
      w.Write('[');
      bool first = true;
      foreach (CodeExpression exp in e.Indices){            
        if (first) first = false; else w.Write(", ");
        this.Write(exp);
      }
      w.Write(']');
    }
    private void Write(CodeIterationStatement e){
      TextWriter w = this.writer;
      int savedLevel = this.level;
      this.level = 0;
      this.inForLoopHeader = true;
      w.Write("for (");
      this.Write(e.InitStatement);
      w.Write("; ");
      this.Write(e.TestExpression);
      w.Write("; ");
      this.Write(e.IncrementStatement);
      w.Write(')');
      this.level = savedLevel;
      this.WriteStartingBrace();
      this.inForLoopHeader = false;
      this.Write(e.Statements);
      this.WriteClosingBrace();
    }
    private void Write(CodeLabeledStatement e){
      TextWriter w = this.writer;
      w.Write(e.Label);
      w.Write(':');
      if (e.Statement != null){
        w.WriteLine();
        this.level++;
        this.WriteIndent();
        this.level--;
        this.Write(e.Statement);
      }else
        w.WriteLine(';');
    }
    private void Write(CodeMemberEvent e){
      TextWriter w = this.writer;
      w.Write("event ");
      this.Write(e.Type);
      w.Write(' ');
      if (e.PrivateImplementationType != null){
        w.Write(this.GetBaseTypeOutput(e.PrivateImplementationType.BaseType));
        w.Write('.');
      }
      this.WriteIdentifier(e.Name);
      w.WriteLine(';');
    }
    private void Write(CodeMemberField e, bool partOfEnum){
      TextWriter w = this.writer;
      if (partOfEnum)
        this.WriteIndent();
      else{
        this.Write(e.Type);
        w.Write(' ');
      }
      this.WriteIdentifier(e.Name);
      if (e.InitExpression != null){
        w.Write(" = ");
        this.Write(e.InitExpression);
      }
      if (partOfEnum)
        w.WriteLine(',');
      else
        w.WriteLine(';');
    }
    private void Write(CodeMemberMethod e){
      TextWriter w = this.writer;
      this.Write(e.ReturnType);
      w.Write(' ');
      if (e.PrivateImplementationType != null){
        w.Write(this.GetBaseTypeOutput(e.PrivateImplementationType.BaseType));
        w.Write('.');
      }
      this.WriteIdentifier(e.Name);
      this.Write(e.Parameters);
      if ((e.Attributes & MemberAttributes.ScopeMask) == MemberAttributes.Abstract)
        w.WriteLine(';');
      else{
        this.WriteStartingBrace();
        this.Write(e.Statements);
        this.WriteClosingBrace();
      }
    }
    private void Write(CodeMethodInvokeExpression e){
      this.Write(e.Method);
      this.Write(e.Parameters);
    }
    private void Write(CodeMethodReferenceExpression e){
      TextWriter w = this.writer;
      if (e.TargetObject != null){
        if (e.TargetObject is CodeBinaryOperatorExpression){
          w.Write('(');
          this.Write(e.TargetObject);
          w.Write(')');
        }else
          this.Write(e.TargetObject);
        w.Write('.');
      }
      this.WriteIdentifier(e.MethodName);
    }
    private void Write(CodeMethodReturnStatement e){
      TextWriter w = this.writer;
      w.Write("return");
      if (e.Expression != null) {
        w.Write(' ');
        this.Write(e.Expression);
      }
      w.WriteLine(';');
    }
    private void Write(CodeMemberProperty e){
      TextWriter w = this.writer;
      this.Write(e.Type);
      w.Write(' ');
      if (e.PrivateImplementationType != null){
        w.Write(this.GetBaseTypeOutput(e.PrivateImplementationType.BaseType));
        w.Write('.');
      }
      if (e.Parameters.Count > 0){
        w.Write("this");
        this.Write(e.Parameters, "[", "]");
      }else
        this.WriteIdentifier(e.Name);      
      this.WriteStartingBrace();
      this.level++;
      if (e.HasGet){
        this.WriteIndent();
        if ((e.Attributes & MemberAttributes.ScopeMask) == MemberAttributes.Abstract){
          w.WriteLine("get;");
        }else{
          w.Write("get");
          this.WriteStartingBrace();
          this.Write(e.GetStatements);
          this.WriteClosingBrace();
        }
      }
      if (e.HasSet) {
        this.WriteIndent();
        if ((e.Attributes & MemberAttributes.ScopeMask) == MemberAttributes.Abstract){
          w.WriteLine("set;");
        }else{
          w.Write("set");
          this.WriteStartingBrace();
          this.Write(e.SetStatements);
          this.WriteClosingBrace();
        }
      }
      this.level--;
      this.WriteClosingBrace();
    }
    private void Write(CodeNamespace e){
      TextWriter w = this.writer;
      this.Write(e.Comments);
      if (e.Name != null && e.Name.Length > 0) {
        w.Write("namespace ");
        this.WriteIdentifier(e.Name);
        this.WriteStartingBrace();
        this.level++;
      }
      if (e.Imports != null)
      foreach (CodeNamespaceImport i in e.Imports)
        this.Write(i);
      if (this.cgOptions.BlankLinesBetweenMembers && e.Imports != null && e.Imports.Count > 0)
        w.WriteLine();
      if (e.Types != null) foreach (CodeTypeDeclaration t in e.Types){
        this.Write(t.Comments);
        this.Write(t.CustomAttributes, false, null);
        if (t is CodeTypeDelegate)
          this.Write((CodeTypeDelegate)t);
        else
          this.Write(t);
        if (this.cgOptions.BlankLinesBetweenMembers) w.WriteLine();
      }
      if (e.Name != null && e.Name.Length > 0) {
        this.level--;
        this.WriteClosingBrace();
      }
    }
    private void Write(CodeNamespaceImport e){
      TextWriter w = this.writer;
      this.WriteIndent();
      w.Write("using ");
      w.Write(e.Namespace);
      w.WriteLine(';');
    }
    private void Write(CodeObjectCreateExpression e){
      this.writer.Write("new ");
      this.Write(e.CreateType);
      this.Write(e.Parameters);
    }
    private void Write(CodeParameterDeclarationExpressionCollection e){
      this.Write(e, "(", ")");
    }
    private void Write(CodeParameterDeclarationExpressionCollection e, string openDelim, string closeDelim){
      TextWriter w = this.writer;
      if (e == null){ w.Write(openDelim+closeDelim); return;}
      w.Write(openDelim);
      bool first = true;
      foreach (CodeParameterDeclarationExpression p in e){
        if (first) first = false; else w.Write(", ");
        this.Write(p.CustomAttributes, true, null);
        switch (p.Direction){
          case FieldDirection.Out: w.Write("out "); break;
          case FieldDirection.Ref: w.Write("ref "); break;
        }
        this.Write(p.Type);
        w.Write(' ');
        this.WriteIdentifier(p.Name);
      }
      w.Write(closeDelim);
    }
    private void Write(CodePrimitiveExpression e){
      TextWriter w = this.writer;
      IConvertible ic = e.Value as IConvertible;
      if (ic == null){w.Write("null"); return;}
      switch (ic.GetTypeCode()){
        case TypeCode.Boolean: w.Write(ic.ToBoolean(CultureInfo.InvariantCulture) ? "true" : "false"); break;
        case TypeCode.Byte: w.Write("((byte)"+ic.ToString(CultureInfo.InvariantCulture)+")"); break;
        case TypeCode.Char: this.Write(ic.ToChar(CultureInfo.InvariantCulture)); break;
        case TypeCode.Decimal: w.Write(ic.ToString(CultureInfo.InvariantCulture)+"M"); break;
        case TypeCode.Double: w.Write(ic.ToString(CultureInfo.InvariantCulture)+"D"); break; 
        case TypeCode.Int16: w.Write("((short)"+ic.ToString(CultureInfo.InvariantCulture)+")"); break;
        case TypeCode.Int32: w.Write(ic.ToString(CultureInfo.InvariantCulture)); break; 
        case TypeCode.Int64: w.Write(ic.ToString(CultureInfo.InvariantCulture)+"L"); break; 
        case TypeCode.SByte: w.Write("((sbyte)"+ic.ToString(CultureInfo.InvariantCulture)+")"); break;
        case TypeCode.Single: w.Write(ic.ToString(CultureInfo.InvariantCulture)+"F"); break; 
        case TypeCode.String: this.Write(ic.ToString(CultureInfo.InvariantCulture)); break;
        case TypeCode.UInt16: w.Write("((ushort)"+ic.ToString(CultureInfo.InvariantCulture)+")"); break;
        case TypeCode.UInt32: w.Write(ic.ToString(CultureInfo.InvariantCulture)+"U"); break;
        case TypeCode.UInt64: w.Write(ic.ToString(CultureInfo.InvariantCulture)+"UL"); break;
        default: w.Write(ic.ToString(CultureInfo.InvariantCulture)); break;
      }
    }
    private void Write(CodePropertyReferenceExpression e){
      if (e.TargetObject != null) {
        this.Write(e.TargetObject);
        this.writer.Write('.');
      }
      this.WriteIdentifier(e.PropertyName);
    }
    private void Write(CodePropertySetValueReferenceExpression e){
      this.writer.Write("value");
    }
    private void Write(CodeRemoveEventStatement e){
      TextWriter w = this.writer;
      this.Write(e.Event);
      w.Write(" -= ");
      this.Write(e.Listener);
      w.WriteLine(';');
    }
    private void Write(CodeSnippetExpression e){
      this.writer.Write(e.Value);
    }
    private void Write(CodeSnippetStatement e){
      this.writer.WriteLine(e.Value);
    }
    private void Write(CodeSnippetTypeMember e){
      this.writer.WriteLine(e.Text);
    }
    private void Write(CodeStatement e){
      this.WriteLinePragmaStart(e.LinePragma);
      this.WriteIndent();
      if (e is CodeCommentStatement) this.Write((CodeCommentStatement)e);
      else if (e is CodeMethodReturnStatement) this.Write((CodeMethodReturnStatement)e);
      else if (e is CodeConditionStatement) this.Write((CodeConditionStatement)e);
      else if (e is CodeTryCatchFinallyStatement) this.Write((CodeTryCatchFinallyStatement)e);
      else if (e is CodeAssignStatement) this.Write((CodeAssignStatement)e);
      else if (e is CodeExpressionStatement) this.Write((CodeExpressionStatement)e);
      else if (e is CodeIterationStatement) this.Write((CodeIterationStatement)e);
      else if (e is CodeThrowExceptionStatement) this.Write((CodeThrowExceptionStatement)e);
      else if (e is CodeSnippetStatement) this.Write((CodeSnippetStatement)e);
      else if (e is CodeVariableDeclarationStatement) this.Write((CodeVariableDeclarationStatement)e);
      else if (e is CodeAttachEventStatement) this.Write((CodeAttachEventStatement)e);
      else if (e is CodeRemoveEventStatement) this.Write((CodeRemoveEventStatement)e);
      else if (e is CodeGotoStatement) this.Write((CodeGotoStatement)e);
      else if (e is CodeLabeledStatement) this.Write((CodeLabeledStatement)e);
      else throw new ArgumentException();
      this.WriteLinePragmaEnd(e.LinePragma);
    }
    private void Write(CodeStatementCollection e){
      if (e == null) return;
      this.level++;
      foreach (CodeStatement s in e){
        this.WriteLinePragmaStart(s.LinePragma);
        this.Write(s);
        this.WriteLinePragmaEnd(s.LinePragma);
      }
      this.level--;
    }
    private void Write(CodeThisReferenceExpression e){
      this.writer.Write("this");
    }
    private void Write(CodeThrowExceptionStatement e){
      TextWriter w = this.writer;
      w.Write("throw");
      if (e.ToThrow != null) {
        w.Write(' ');
        this.Write(e.ToThrow);
      }
      w.WriteLine(';');
    }
    private void Write(CodeTryCatchFinallyStatement e){
      TextWriter w = this.writer;
      w.Write("try");
      this.WriteStartingBrace();
      this.Write(e.TryStatements);
      CodeCatchClauseCollection catches = e.CatchClauses;
      if (catches != null && catches.Count > 0){
        foreach (CodeCatchClause c in catches){
          this.WriteIndent();
          w.Write('}');
          if (!this.cgOptions.ElseOnClosing) {w.WriteLine(); this.WriteIndent();}
          w.Write("catch (");
          this.Write(c.CatchExceptionType);
          w.Write(' ');
          this.WriteIdentifier(c.LocalName);
          w.Write(')');
          this.WriteStartingBrace();
          this.Write(c.Statements);
        }
      }
      CodeStatementCollection finallyStatements = e.FinallyStatements;
      if (finallyStatements != null && finallyStatements.Count > 0){
        w.Write("}");
        if (!this.cgOptions.ElseOnClosing) {w.WriteLine(); this.WriteIndent();}
        w.Write("finally");
        this.WriteStartingBrace();
        this.Write(finallyStatements);
      }
      this.WriteClosingBrace();
    }
    private void Write(CodeTypeConstructor e, string typeName){
      TextWriter w = this.writer;
      this.WriteIndent();
      w.Write("static ");
      this.WriteIdentifier(typeName);
      w.Write("() ");
      this.WriteStartingBrace();
      this.Write(e.Statements);
      this.WriteClosingBrace();
    }
    private void Write(CodeTypeDeclaration e){
      this.WriteIndent();
      TextWriter w = this.writer;
      switch((e.TypeAttributes & TypeAttributes.VisibilityMask)){
        case TypeAttributes.NotPublic:
        case TypeAttributes.NestedAssembly:
          w.Write("internal ");
          break;
        case TypeAttributes.NestedFamANDAssem:
          w.Write("/*FamANDAssem*/ internal ");
          break;
        case TypeAttributes.NestedFamily:
          w.Write("protected ");
          break;
        case TypeAttributes.NestedFamORAssem:
          w.Write("protected internal ");
          break;
        case TypeAttributes.NestedPrivate:
          w.Write("private ");
          break;
        case TypeAttributes.Public:
        case TypeAttributes.NestedPublic:
          w.Write("public ");
          break;
      }
      bool isEnum = e.IsEnum;
      bool isInterface = e.IsInterface;
      bool isStruct = e.IsStruct;
      if (e.IsClass){
        if ((e.TypeAttributes & TypeAttributes.Sealed) != 0) w.Write("sealed ");
        else if ((e.TypeAttributes & TypeAttributes.Abstract) != 0) w.Write("abstract ");
        w.Write("class ");
      }else if (isEnum) w.Write("enum ");
      else if (isInterface) w.Write("interface ");
      else if (isStruct) w.Write("struct ");
      else throw new ArgumentException();
      w.Write(e.Name);
      if (e.BaseTypes != null && e.BaseTypes.Count > 0){
        w.Write(" : ");
        bool first = true;
        foreach (CodeTypeReference tref in e.BaseTypes){
          if (first) first = false; else w.Write(", ");
          this.Write(tref);
        }
      }
      this.WriteStartingBrace();
      if (this.cgOptions.BlankLinesBetweenMembers) w.WriteLine();
      this.level++;
      if (e.Members != null)
      foreach (CodeTypeMember mem in e.Members){
        this.Write(mem.Comments);
        this.Write(mem.CustomAttributes, false, null);
        if (mem is CodeTypeConstructor){this.Write((CodeTypeConstructor)mem, e.Name); goto nextMem;}
        CodeEntryPointMethod ceMethod = mem as CodeEntryPointMethod;
        if (ceMethod != null){
          mem.Name = "Main";
          mem.Attributes = MemberAttributes.Static|MemberAttributes.Public;
        }
        CodeMemberMethod meth =  mem as CodeMemberMethod;
        if (meth != null){
          this.Write(meth.ReturnTypeCustomAttributes, false, "return: ");
          if (meth.PrivateImplementationType != null){
            meth.Attributes &= ~MemberAttributes.AccessMask;
          }            
        }
        if (!isEnum && !(mem is CodeTypeDeclaration)) this.Write(mem.Attributes, isInterface);
        if (isInterface){ 
          mem.Attributes &= ~MemberAttributes.ScopeMask;
          mem.Attributes |= MemberAttributes.Abstract;
        }else{
          if ((mem is CodeMemberField && !((mem.Attributes & MemberAttributes.ScopeMask) == MemberAttributes.Const ||
            (mem.Attributes & MemberAttributes.ScopeMask) == MemberAttributes.Static)) ||
            mem is CodeMemberEvent){
            mem.Attributes &= ~MemberAttributes.ScopeMask;
            mem.Attributes |= MemberAttributes.Final;
          }
          if (isStruct && (mem.Attributes & MemberAttributes.ScopeMask) != MemberAttributes.Static) 
            mem.Attributes &= ~MemberAttributes.ScopeMask;
          if (!isEnum && ((meth != null && !(meth is CodeConstructor || meth is CodeTypeConstructor)) || 
            mem is CodeMemberField || mem is CodeMemberEvent || mem is CodeMemberProperty))
            this.WriteScopeModifier(mem.Attributes, (e.TypeAttributes & TypeAttributes.Sealed) != 0 || isStruct);
        }
        if (mem is CodeConstructor) this.Write((CodeConstructor)mem, e.Name);
        else if (meth != null) this.Write(meth);
        else if (mem is CodeTypeDelegate) this.Write((CodeTypeDelegate)mem);
        else if (mem is CodeMemberEvent) this.Write((CodeMemberEvent)mem);
        else if (mem is CodeMemberField) {if (!isInterface) this.Write((CodeMemberField)mem, isEnum);}
        else if (mem is CodeMemberProperty) {if (!isEnum) this.Write((CodeMemberProperty)mem);}
        else if (mem is CodeSnippetTypeMember) this.Write((CodeSnippetTypeMember)mem);
        else if (mem is CodeTypeDeclaration) this.Write((CodeTypeDeclaration)mem);
        else throw new ArgumentException();
      nextMem:
        if (this.cgOptions.BlankLinesBetweenMembers) w.WriteLine();
      }
      this.level--;
      this.WriteClosingBrace();
    }
    private void Write(CodeTypeDelegate e){
      this.WriteIndent();
      TextWriter w = this.writer;
      switch((e.TypeAttributes & TypeAttributes.VisibilityMask)){
        case TypeAttributes.NotPublic:
        case TypeAttributes.NestedAssembly:
          w.Write("internal ");
          break;
        case TypeAttributes.NestedFamANDAssem:
          w.Write("/*FamANDAssem*/ internal ");
          break;
        case TypeAttributes.NestedFamily:
          w.Write("protected ");
          break;
        case TypeAttributes.NestedFamORAssem:
          w.Write("protected internal ");
          break;
        case TypeAttributes.NestedPrivate:
          w.Write("private ");
          break;
        case TypeAttributes.Public:
        case TypeAttributes.NestedPublic:
          w.Write("public ");
          break;
      }
      w.Write("delegate ");
      this.Write(e.ReturnType);
      w.Write(' ');
      this.WriteIdentifier(e.Name);
      this.Write(e.Parameters);
      w.WriteLine(';');
    }
    private void Write(CodeTypeOfExpression e){
      TextWriter w = this.writer;
      w.Write("typeof(");
      this.Write(e.Type);
      w.Write(')');
    }
    private void Write(CodeTypeReferenceExpression e) {
      this.Write(e.Type);
    }
    private void Write(CodeTypeReference e){
      this.writer.Write(((ICodeGenerator)this).GetTypeOutput(e));
    }
    private void Write(CodeVariableDeclarationStatement e){
      TextWriter w = this.writer;
      this.Write(e.Type);
      w.Write(' ');
      this.WriteIdentifier(e.Name);
      if (e.InitExpression != null){
        w.Write(" = ");
        this.Write(e.InitExpression);
      }
      if (!this.inForLoopHeader) w.WriteLine(';');
    }
    private void Write(CodeVariableReferenceExpression e){
      if (e.VariableName == "value")
        this.writer.Write("value");
      else
        this.WriteIdentifier(e.VariableName);
    }
    private void Write(MemberAttributes attributes, bool isInterface){
      TextWriter w = this.writer;
      this.WriteIndent();
      if (!isInterface){
        switch (attributes & MemberAttributes.AccessMask) {
          case MemberAttributes.Assembly:
            w.Write("internal ");
            break;
          case MemberAttributes.FamilyAndAssembly:
            w.Write("/*FamANDAssem*/ internal ");
            break;
          case MemberAttributes.Family:
            w.Write("protected ");
            break;
          case MemberAttributes.FamilyOrAssembly:
            w.Write("protected internal ");
            break;
          case MemberAttributes.Private:
            w.Write("private ");
            break;
          case MemberAttributes.Public:
            w.Write("public ");
            break;
        }
      }
      switch (attributes & MemberAttributes.VTableMask){
        case MemberAttributes.New:
          w.Write("new ");
          break;
      }
    }
    private void WriteScopeModifier(MemberAttributes attributes, bool sealedDeclaringType){
      TextWriter w = this.writer;
      switch (attributes & MemberAttributes.ScopeMask) {
        case MemberAttributes.Abstract:
          w.Write("abstract ");
          break;
        case MemberAttributes.Const:
          w.Write("const ");
          break;
        case MemberAttributes.Final:
          break;
        case MemberAttributes.Static:
          w.Write("static ");
          break;
        case MemberAttributes.Override:
          if ((attributes & MemberAttributes.Final) != 0) w.Write("sealed ");
          w.Write("override ");
          break;
        default:
          if (!sealedDeclaringType){
            switch (attributes & MemberAttributes.AccessMask) {
              case MemberAttributes.Family:
              case MemberAttributes.Public:
                w.Write("virtual ");
                break;
            }
          }
          break;
      }
    }
    private void Write(string str){
      TextWriter w = this.writer;
      w.Write('"');
      for (int i = 0, n = str == null ? 0 : str.Length; i < n; i++){
        char ch = str[i];
        switch (ch){
          case '\r': w.Write("\\r"); break;
          case '\t': w.Write("\\t"); break;
          case '\"': w.Write("\\\""); break;
          case '\'': w.Write("\\\'"); break;
          case '\\': w.Write("\\\\"); break;
          case '\0': w.Write("\\0"); break;
          case '\n': w.Write("\\n"); break;
          case '\u2028': w.Write("\\u2028"); break;
          case '\u2029': w.Write("\\u2029"); break;     
          default: w.Write(ch); break;
        }
        if (i > 0 && i % 80 == 0)
          w.Write("\" +\r\n\"");
      }
      w.Write('"');
    }
    private void WriteClosingBrace(){
      this.WriteIndent();
      this.writer.WriteLine('}');
    }
    private void WriteIndent(){
      TextWriter w = this.writer;
      string indent = this.cgOptions.IndentString;
      int level = this.level;
      while (level-- > 0) w.Write(indent);
    }
    private void WriteIdentifier(string id){
      this.writer.Write(((ICodeGenerator)this).CreateEscapedIdentifier(id));
    }
    private void WriteLinePragmaStart(CodeLinePragma e){
      if (e == null) return;
      TextWriter w = this.writer;
      w.WriteLine();
      w.WriteLine("#line {0} \"{1}\"", e.LineNumber, e.FileName);
    }
    private void WriteLinePragmaEnd(CodeLinePragma e){
      if (e == null) return;
      TextWriter w = this.writer;
      w.WriteLine("#line default");
      w.WriteLine("#line hidden");
      w.WriteLine();
    }
    private void WriteStartingBrace(){
      TextWriter w = this.writer;
      if (this.cgOptions.BracingStyle == "C"){
        w.WriteLine();
        this.WriteIndent();
        w.WriteLine("{");
      }else
        w.WriteLine("{");
    }
    private void WriteTypeModifiers(CodeTypeDeclaration e){
      TextWriter w = this.writer;
      TypeAttributes attributes = e.TypeAttributes;
      switch(attributes & TypeAttributes.VisibilityMask){
        case TypeAttributes.Public:                  
          w.Write("public ");
          break;
        default:
          break;
      }
    }
    #endregion CodeDomCodeGenerator
    #region CompilerParameters
    public override bool ParseCompilerOption(CompilerParameters options, string arg, ErrorNodeList errors){
      bool result = base.ParseCompilerOption(options, arg, errors);
      SpecSharpCompilerOptions xoptions = options as SpecSharpCompilerOptions;
      if (!result){
        //See if Spec# specific option
        CompilerOptions coptions = options as CompilerOptions;
        if (coptions == null) return false;
        int n = arg.Length;
        if (n <= 1) return false;
        char ch = arg[0];
        if (ch != '/' && ch != '-') return false;
        ch = arg[1];
        switch(Char.ToLower(ch)){
          case 'c':
            if (this.ParseName(arg, "compatibility", "compatibility")){
              if (xoptions == null) break;
              xoptions.Compatibility = true;
              xoptions.ReferenceTypesAreNonNullByDefault = false;
              return true;
            }
            object checkContracts = this.ParseNamedBoolean(arg, "checkcontracts", "cc");
            if (checkContracts != null) {
              xoptions.CheckContractAdmissibility = (bool) checkContracts;
              return true;
            }
            object checkPurity = this.ParseNamedBoolean(arg, "checkpurity", "cp");
            if (checkPurity != null) {
              xoptions.CheckPurity = (bool)checkPurity;
              return true;
            }
            break;
          case 'n':
            if (this.ParseName(arg, "noconfig", "noconfig")) return true;
            object nonNullTypesByDefault = this.ParseNamedBoolean(arg, "nonnull", "nn");
            if (nonNullTypesByDefault != null) {
              xoptions.ReferenceTypesAreNonNullByDefault = (bool)nonNullTypesByDefault;
              return true;
            }
            break;
          case 'v':
            if (this.ParseName(arg, "verify", "verify")){
              if (xoptions == null) break;
              xoptions.RunProgramVerifier = true;
              return true;
            }
            System.Collections.Specialized.StringCollection sc = this.ParseNamedArgumentList(arg, "verifyopt", "vo");
            if (sc != null) {
              if (xoptions == null) break;
              if (xoptions.ProgramVerifierCommandLineOptions == null && sc.Count > 0)
                xoptions.ProgramVerifierCommandLineOptions = new StringList(sc.Count);
              foreach (string s in sc) {
                xoptions.ProgramVerifierCommandLineOptions.Add(s);
              }
              return true;
            }
            break;
        }
        return false; //TODO: give an error message
      }
      return result;
    }   
    #endregion CompilerParameters

  }
  public enum LanguageVersionType{
    Default,
    ISO_1,
    CSharpVersion2,
  }
  public class SpecSharpCompilerOptions : CompilerOptions{
    public bool Compatibility; //TODO: make this go away. Use LanguageVersion.
    public bool DummyCompilation;
    public LanguageVersionType LanguageVersion;
    public bool ReferenceTypesAreNonNullByDefault;
    public bool RunProgramVerifier;
    public bool RunProgramVerifierWhileEditing;
    public StringList ProgramVerifierCommandLineOptions; // things to pass through to the static verifier
    public bool OnlyTypeChecks;
    public bool CheckContractAdmissibility = true; // default is to perform the checks
    public bool CheckPurity;

    // begin change by drunje
    public bool AllowPointersToManagedStructures;
    // end change by drunje

    public SpecSharpCompilerOptions(){
      if (base.DefinedPreProcessorSymbols == null) {
        base.DefinedPreProcessorSymbols = new StringList(4);
      }
      base.DefinedPreProcessorSymbols.Add("SPECSHARP");
    }

    public SpecSharpCompilerOptions(CompilerOptions options)
      : base(options){
      SpecSharpCompilerOptions coptions = options as SpecSharpCompilerOptions;
      if (coptions == null) return;
      this.Compatibility = coptions.Compatibility;
      this.ReferenceTypesAreNonNullByDefault = coptions.ReferenceTypesAreNonNullByDefault;
      this.RunProgramVerifier = coptions.RunProgramVerifier;
      this.RunProgramVerifierWhileEditing = coptions.RunProgramVerifierWhileEditing;
      this.ProgramVerifierCommandLineOptions = coptions.ProgramVerifierCommandLineOptions;
      this.OnlyTypeChecks = false;
      this.CheckContractAdmissibility = coptions.CheckContractAdmissibility;
      this.CheckPurity = coptions.CheckPurity;
    }

    public override string GetOptionHelp() {
#if CCINamespace
      System.Resources.ResourceManager rm = new System.Resources.ResourceManager("Microsoft.Cci.Compiler.ErrorMessages", typeof(CommonErrorNode).Module.Assembly);
#else
      System.Resources.ResourceManager rm = new System.Resources.ResourceManager("System.Compiler.Compiler.ErrorMessages", typeof(CommonErrorNode).Module.Assembly);
#endif
      string baseOptionHelp = rm.GetString("Usage");
      rm = new System.Resources.ResourceManager("Microsoft.SpecSharp.ErrorMessages", typeof(SpecSharpErrorNode).Module.Assembly);
      return baseOptionHelp + rm.GetString("Usage");
    }
  }
}
