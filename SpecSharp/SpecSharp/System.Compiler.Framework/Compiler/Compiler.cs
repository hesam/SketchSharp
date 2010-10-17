//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Text;

#if CCINamespace
using Microsoft.Cci.Metadata;
namespace Microsoft.Cci{
#else
using System.Compiler.Metadata;
namespace System.Compiler{
#endif

  public class CompilerErrorEx : CompilerError
  {
    public int EndLine;
    public int EndColumn;
  }

  /// <summary>
  /// This class provides compilation services for various clients such as command line compilers,
  /// in memory compilers hosted by some application such as Visual Studio, and CodeDom clients such
  /// as ASP .NET. 
  /// </summary>
  public abstract class Compiler : ICodeCompiler, IParserFactory{
    public IDictionary AssemblyCache;
    public Compilation CurrentCompilation;

    public Compiler(){
      IContractDeserializer cd = new Omni.Parser.ContractDeserializer();
      ContractDeserializerContainer.ContractDeserializer = cd;
    }

    static Compiler(){
      AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(Compiler.Resolver);
    }
    private static System.Reflection.Assembly Resolver(object sender, ResolveEventArgs args){
      string name = args.Name;
      System.Reflection.Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
      foreach (System.Reflection.Assembly assem in loadedAssemblies)
        if (assem.FullName == name) return assem;
      return null;
    }

    #region ICodeCompiler overrides
    public virtual CompilerResults CompileAssemblyFromDom(CompilerParameters options, CodeCompileUnit compilationUnit){
      return this.CompileAssemblyFromDom(options, compilationUnit, new ErrorNodeList());
    }
    public virtual CompilerResults CompileAssemblyFromDomBatch(CompilerParameters options, CodeCompileUnit[] compilationUnits){
      return this.CompileAssemblyFromDomBatch(options, compilationUnits, new ErrorNodeList());
    }
    public virtual CompilerResults CompileAssemblyFromFile(CompilerParameters options, string fileName){
      return this.CompileAssemblyFromFile(options, fileName, new ErrorNodeList());
    }
    public virtual CompilerResults CompileAssemblyFromFileBatch(CompilerParameters options, string[] fileNames){
      return this.CompileAssemblyFromFileBatch(options, fileNames, new ErrorNodeList());
    }
    public virtual CompilerResults CompileAssemblyFromSource(CompilerParameters options, string source){
      return this.CompileAssemblyFromSource(options, source, new ErrorNodeList());
    }
    public virtual CompilerResults CompileAssemblyFromSourceBatch(CompilerParameters options, string[] sources){
      return this.CompileAssemblyFromSourceBatch(options, sources, new ErrorNodeList());
    }
    #endregion
    #region ICodeCompiler like methods that produce modules not assemblies
    public virtual CompilerResults CompileModuleFromDom(CompilerParameters options, CodeCompileUnit compilationUnit){
      return this.CompileModuleFromDom(options, compilationUnit, new ErrorNodeList());
    }
    public virtual CompilerResults CompileModuleFromDomBatch(CompilerParameters options, CodeCompileUnit[] compilationUnits){
      return this.CompileModuleFromDomBatch(options, compilationUnits, new ErrorNodeList());
    }
    public virtual CompilerResults CompileModuleFromFile(CompilerParameters options, string fileName){
      return this.CompileModuleFromFile(options, fileName, new ErrorNodeList());
    }
    public virtual CompilerResults CompileModuleFromFileBatch(CompilerParameters options, string[] fileNames){
      return this.CompileModuleFromFileBatch(options, fileNames, new ErrorNodeList());
    }
    public virtual CompilerResults CompileModuleFromSource(CompilerParameters options, string source){
      return this.CompileModuleFromSource(options, source, new ErrorNodeList());
    }
    public virtual CompilerResults CompileModuleFromSourceBatch(CompilerParameters options, string[] sources){
      return this.CompileModuleFromSourceBatch(options, sources, new ErrorNodeList());
    }
    #endregion
    #region ICodeCompiler like methods that take IR arguments
    public virtual CompilerResults CompileAssemblyFromIR(Compilation compilation){
      return this.CompileAssemblyFromIR(compilation, new ErrorNodeList());
    }
    public virtual CompilerResults CompileModuleFromIR(Compilation compilation){
      return this.CompileModuleFromIR(compilation, new ErrorNodeList());
    }
    #endregion
    #region ICodeCompiler like methods that expose the ErrorNodeList
    public virtual CompilerResults CompileAssemblyFromDom(CompilerParameters options, CodeCompileUnit compilationUnit, ErrorNodeList errorNodes){
      if (options == null || compilationUnit == null || errorNodes == null){Debug.Assert(false); return null;}
      return this.CompileAssemblyFromDomBatch(options, new CodeCompileUnit[]{compilationUnit}, errorNodes);
    }
    public virtual CompilerResults CompileAssemblyFromDomBatch(CompilerParameters options, CodeCompileUnit[] compilationUnits, ErrorNodeList errorNodes){
      if (options == null){Debug.Assert(false); return null;}
      int n = compilationUnits == null ? 0 : compilationUnits.Length;
      if (options.OutputAssembly == null || options.OutputAssembly.Length == 0){
        for (int i = 0; i < n; i++){
          CodeSnippetCompileUnit csu = compilationUnits[i] as CodeSnippetCompileUnit;
          if (csu == null || csu.LinePragma == null || csu.LinePragma.FileName == null) continue;
          this.SetOutputFileName(options, csu.LinePragma.FileName);
          break;
        }
      }
      CompilerResults results = new CompilerResults(options.TempFiles);
      AssemblyNode assem = this.CreateAssembly(options, errorNodes);
      Compilation compilation = this.CreateCompilation(assem, new CompilationUnitList(n), options, this.GetGlobalScope(assem));
      CodeDomTranslator cdt = new CodeDomTranslator();
      SnippetParser sp = new SnippetParser(this, assem, errorNodes, options);
      for (int i = 0; i < n; i++){
        CompilationUnit cu = cdt.Translate(this, compilationUnits[i], assem, errorNodes);
        sp.Visit(cu);
        compilation.CompilationUnits.Add(cu);
        cu.Compilation = compilation;
      }
      this.CompileParseTree(compilation, errorNodes);
      this.ProcessErrors(options, results, errorNodes);
      if (results.NativeCompilerReturnValue == 0)
        this.SetEntryPoint(compilation, results);
      this.SaveCompilation(compilation, assem, options, results, errorNodes);
      return results;
    }
    public virtual CompilerResults CompileAssemblyFromFile(CompilerParameters options, string fileName, ErrorNodeList errorNodes){
      return this.CompileAssemblyFromFile(options, fileName, errorNodes, false);
    }
    public virtual CompilerResults CompileAssemblyFromFile(CompilerParameters options, string fileName, ErrorNodeList errorNodes, bool canUseMemoryMap){
      if (options == null || fileName == null || !File.Exists(fileName)){Debug.Assert(false); return null;}
      return this.CompileAssemblyFromFileBatch(options, new string[]{fileName}, errorNodes, canUseMemoryMap);
    }
    public virtual CompilerResults CompileAssemblyFromFileBatch(CompilerParameters options, string[] fileNames, ErrorNodeList errorNodes){
      return this.CompileAssemblyFromFileBatch(options, fileNames, errorNodes, false);
    }
    public virtual CompilerResults CompileAssemblyFromFileBatch(CompilerParameters options, string[] fileNames, ErrorNodeList errorNodes, bool canUseMemoryMap){
      if (options == null){Debug.Assert(false); return null;}
      int n = fileNames.Length;
      if (options.OutputAssembly == null || options.OutputAssembly.Length == 0){
        for (int i = 0; i < n; i++){
          if (fileNames[i] == null) continue;
          this.SetOutputFileName(options, fileNames[i]);
          break;
        }
      }
      CompilerResults results = new CompilerResults(options.TempFiles);
      AssemblyNode assem = this.CreateAssembly(options, errorNodes);
      Compilation compilation = this.CreateCompilation(assem, new CompilationUnitList(n), options, this.GetGlobalScope(assem));
      SnippetParser sp = new SnippetParser(this, assem, errorNodes, options);
      for (int i = 0; i < n; i++){
        string fileName = fileNames[i];
        if (fileName == null) continue;
        DocumentText text = this.CreateDocumentText(fileName, results, options, errorNodes, canUseMemoryMap);
        CompilationUnitSnippet cu = this.CreateCompilationUnitSnippet(fileName, 1, text, compilation);
        sp.Visit(cu);
        compilation.CompilationUnits.Add(cu);
        cu.Compilation = compilation;
      }
      this.CompileParseTree(compilation, errorNodes);
      this.ProcessErrors(options, results, errorNodes);
      if (results.NativeCompilerReturnValue == 0)
        this.SetEntryPoint(compilation, results);
      this.SaveCompilation(compilation, assem, options, results, errorNodes);
      return results;
    }
    public virtual CompilerResults CompileAssemblyFromIR(Compilation compilation, ErrorNodeList errorNodes){
      if (compilation == null || compilation.CompilerParameters == null || errorNodes == null){Debug.Assert(false); return null;}
      this.CurrentCompilation = compilation;
      CompilerResults results = new CompilerResults(compilation.CompilerParameters.TempFiles);
      //When producing a library, use the name of the first source file for the name of the library unless otherwise specified.
      //(Executables get the name of the file that contains the main method. This is arranged by SetEntryPoint.)
      if (!compilation.CompilerParameters.GenerateExecutable && 
        (compilation.CompilerParameters.OutputAssembly == null || compilation.CompilerParameters.OutputAssembly.Length == 0)){
        for (int i = 0, n = compilation.CompilationUnits == null ? 0 : compilation.CompilationUnits.Count; i < n; i++){
          CompilationUnit cu = compilation.CompilationUnits[i];
          if (cu == null || cu.Name == null) continue;
          this.SetOutputFileName(compilation.CompilerParameters, cu.Name.ToString()); 
          break;
        }
      }
      SnippetParser sp = this.CreateSnippetParser(compilation.TargetModule, errorNodes, compilation.CompilerParameters);
      sp.Visit(compilation);
      this.CompileParseTree(compilation, errorNodes);
      this.ProcessErrors(compilation.CompilerParameters, results, errorNodes);
      if (results.NativeCompilerReturnValue == 0)
        this.SetEntryPoint(compilation, results);
      AssemblyNode assem = compilation.TargetModule as AssemblyNode;
      this.SaveCompilation(compilation, assem, compilation.CompilerParameters, results, errorNodes);
      assem = null;
      this.CurrentCompilation = null;
      return results;
    }
    public virtual CompilerResults CompileAssemblyFromSource(CompilerParameters options, string source, ErrorNodeList errorNodes){
      if (options == null || source == null){Debug.Assert(false); return null;}
      CodeSnippetCompileUnit cuSnippet = new CodeSnippetCompileUnit(source);
      return this.CompileAssemblyFromDom(options, cuSnippet, errorNodes);
    }
    public virtual CompilerResults CompileAssemblyFromSourceBatch(CompilerParameters options, string[] sources, ErrorNodeList errorNodes){
      if (options == null || sources == null){Debug.Assert(false); return null;}
      int n = sources.Length;
      System.CodeDom.CodeSnippetCompileUnit[] cuSnippets = new System.CodeDom.CodeSnippetCompileUnit[n];
      for (int i = 0; i < n; i++)
        cuSnippets[i] = new CodeSnippetCompileUnit(sources[i]);      
      return this.CompileAssemblyFromDomBatch(options, cuSnippets, errorNodes);
    }
    public virtual CompilerResults CompileModuleFromDom(CompilerParameters options, CodeCompileUnit compilationUnit, ErrorNodeList errorNodes){
      if (options == null || compilationUnit == null || errorNodes == null){Debug.Assert(false); return null;}
      return this.CompileModuleFromDomBatch(options, new CodeCompileUnit[]{compilationUnit}, errorNodes);
    }
    public virtual CompilerResults CompileModuleFromDomBatch(CompilerParameters options, CodeCompileUnit[] compilationUnits, ErrorNodeList errorNodes){
      if (options == null){Debug.Assert(false); return null;}
      int n = compilationUnits == null ? 0 : compilationUnits.Length;
      if (options.OutputAssembly == null || options.OutputAssembly.Length == 0){
        for (int i = 0; i < n; i++){
          CodeSnippetCompileUnit csu = compilationUnits[i] as CodeSnippetCompileUnit;
          if (csu == null || csu.LinePragma == null || csu.LinePragma.FileName == null) continue;
          this.SetOutputFileName(options, csu.LinePragma.FileName);
          break;
        }
      }
      CompilerResults results = new CompilerResults(options.TempFiles);
      Module module = this.CreateModule(options, errorNodes);
      Compilation compilation = this.CreateCompilation(module, new CompilationUnitList(n), options, this.GetGlobalScope(module));
      CodeDomTranslator cdt = new CodeDomTranslator();
      SnippetParser sp = new SnippetParser(this, module, errorNodes, options);
      for (int i = 0; i < n; i++){
        CompilationUnit cu = cdt.Translate(this, compilationUnits[i], module, errorNodes);
        sp.Visit(cu);
        compilation.CompilationUnits.Add(cu);
        cu.Compilation = compilation;
      }
      this.CompileParseTree(compilation, errorNodes);
      this.ProcessErrors(options, results, errorNodes);
      this.SaveCompilation(compilation, module, options, results);
      return results;
    }
    public virtual CompilerResults CompileModuleFromFile(CompilerParameters options, string fileName, ErrorNodeList errorNodes){
      return this.CompileModuleFromFile(options, fileName, errorNodes, false);
    }
    public virtual CompilerResults CompileModuleFromFile(CompilerParameters options, string fileName, ErrorNodeList errorNodes, bool canUseMemoryMap){
      if (options == null || fileName == null || !File.Exists(fileName)){Debug.Assert(false); return null;}
      return this.CompileModuleFromFileBatch(options, new string[]{fileName}, errorNodes, canUseMemoryMap);
    }
    public virtual CompilerResults CompileModuleFromFileBatch(CompilerParameters options, string[] fileNames, ErrorNodeList errorNodes){
      return this.CompileModuleFromFileBatch(options, fileNames, errorNodes, false);
    }
    public virtual CompilerResults CompileModuleFromFileBatch(CompilerParameters options, string[] fileNames, ErrorNodeList errorNodes, bool canUseMemoryMap){
      if (options == null){Debug.Assert(false); return null;}
      int n = fileNames.Length;
      if (options.OutputAssembly == null || options.OutputAssembly.Length == 0){
        for (int i = 0; i < n; i++){
          if (fileNames[i] == null) continue;
          this.SetOutputFileName(options, fileNames[i]);
          break;
        }
      }
      CompilerResults results = new CompilerResults(options.TempFiles);
      Module module = this.CreateModule(options, errorNodes);
      Compilation compilation = this.CreateCompilation(module, new CompilationUnitList(n), options, this.GetGlobalScope(module));
      SnippetParser sp = new SnippetParser(this, module, errorNodes, options);
      for (int i = 0; i < n; i++){
        string fileName = fileNames[i];
        if (fileName == null) continue;
        DocumentText text = this.CreateDocumentText(fileName, results, options, errorNodes, canUseMemoryMap);
        CompilationUnitSnippet cu = this.CreateCompilationUnitSnippet(fileName, 1, text, compilation);
        sp.Visit(cu);
        compilation.CompilationUnits.Add(cu);
        cu.Compilation = compilation;
      }
      this.CompileParseTree(compilation, errorNodes);
      this.ProcessErrors(options, results, errorNodes);
      this.SaveCompilation(compilation, module, options, results);
      return results;
    }
    public virtual CompilerResults CompileModuleFromIR(Compilation compilation, ErrorNodeList errorNodes){
      if (compilation != null && compilation.CompilerParameters != null && !compilation.CompilerParameters.GenerateExecutable && 
        (compilation.CompilerParameters.OutputAssembly == null || compilation.CompilerParameters.OutputAssembly.Length == 0)){
        for (int i = 0, n = compilation.CompilationUnits == null ? 0 : compilation.CompilationUnits.Count; i < n; i++){
          CompilationUnit cu = compilation.CompilationUnits[i];
          if (cu == null || cu.Name == null) continue;
          this.SetOutputFileName(compilation.CompilerParameters, cu.Name.ToString());
          break;
        }
      }
      Module module = compilation.TargetModule;
      if (module == null) module = this.CreateModule(compilation.CompilerParameters, errorNodes);
      CompilerResults results = new CompilerResults(compilation.CompilerParameters.TempFiles);
      compilation.TargetModule = module;
      if (compilation.CompilationUnits == null)
        compilation.CompilationUnits = new CompilationUnitList();
      SnippetParser sp = this.CreateSnippetParser(module, errorNodes, compilation.CompilerParameters);
      sp.Visit(compilation);
      this.CompileParseTree(compilation, errorNodes);
      this.ProcessErrors(compilation.CompilerParameters, results, errorNodes);
      this.SaveCompilation(compilation, module, compilation.CompilerParameters, results);
      module = null;
      return results;
    }
    public virtual CompilerResults CompileModuleFromSource(CompilerParameters options, string source, ErrorNodeList errorNodes){
      if (options == null || source == null){Debug.Assert(false); return null;}
      CodeSnippetCompileUnit cuSnippet = new CodeSnippetCompileUnit(source);
      return this.CompileModuleFromDom(options, cuSnippet, errorNodes);
    }
    public virtual CompilerResults CompileModuleFromSourceBatch(CompilerParameters options, string[] sources, ErrorNodeList errorNodes){
      if (options == null || sources == null){Debug.Assert(false); return null;}
      int n = sources.Length;
      System.CodeDom.CodeSnippetCompileUnit[] cuSnippets = new System.CodeDom.CodeSnippetCompileUnit[n];
      for (int i = 0; i < n; i++)
        cuSnippets[i] = new CodeSnippetCompileUnit(sources[i]); 
      return this.CompileModuleFromDomBatch(options, cuSnippets, errorNodes);
    }
    #endregion
    #region These routines allow tools to work with the metadata generated by the compiler
    /// <summary>
    /// Parses all of the CompilationUnitSnippets in the given compilation, ignoring method bodies. Then resolves all type expressions.
    /// The resulting types can be retrieved from the module in compilation.TargetModule. The base types, interfaces and 
    /// member signatures will all be resolved and on an equal footing with imported, already compiled modules and assemblies.
    /// </summary>
    public virtual void ConstructSymbolTable(Compilation compilation, ErrorNodeList errors){
      Debug.Assert(false); //Subclasses either have to override this, or ensure that their client will never call it.
    }
    /// <summary>
    /// Resolves all type expressions in the given (already parsed) compilation.
    /// The base types, interfaces and member signatures will all be on an equal footing with signatures from imported, 
    /// already compiled modules and assemblies.
    /// </summary>
    public virtual void ResolveSymbolTable(Compilation/*!*/ parsedCompilation, ErrorNodeList/*!*/ errors){
      Debug.Assert(false); //Subclasses either have to override this, or ensure that their client will never call it.
    }
    /// <summary>
    /// Updates the specified symbol table, substituting changedDocument for originalDocument.
    /// Fires the OnSymbolTableUpdate event before returning (provided that changes occurred to member signatures).
    /// </summary>
    /// <param name="symbolTable">The symbol table to update or replace.</param>
    /// <param name="originalDocument">The document of a CompilationUnit instance in compilation.</param>
    /// <param name="changedDocument">A new version of originalDocument.</param>
    /// <param name="changes">A list of the changes made to orignalDocument in order to derive changedDocument.</param>
    /// <param name="errors">A list to which errors detected during the update must be added.</param>
    /// <returns>The given symbol table instance, suitably updated, or a new symbol table that replaces the given table.</returns>
    public virtual Compilation UpdateSymbolTable(Compilation symbolTable, Document originalDocument, Document changedDocument, SourceChangeList changes,
      ErrorNodeList errors){
      if (symbolTable == null || symbolTable.TargetModule == null || originalDocument == null || changedDocument == null || changes == null){
        Debug.Assert(false); return null;
      }
      int changeInLength;
      SourceContext spanningContextForChanges = this.GetSpanningContext(changes, out changeInLength);
      CompilationUnitSnippet compilationUnit = null;      
      for (int i = 0, n = symbolTable.CompilationUnits == null ? 0 : symbolTable.CompilationUnits.Count; i < n; i++){
        CompilationUnitSnippet cu = symbolTable.CompilationUnits[i] as CompilationUnitSnippet;
        if (cu != null && cu.SourceContext.Document == originalDocument){
          compilationUnit = cu;
          break;
        }
      }
      if (compilationUnit == null){Debug.Assert(false); return null;}
      Method meth = compilationUnit.ChangedMethod;
      if (meth != null){
        if (meth.Body != null && meth.Body.SourceContext.Encloses(spanningContextForChanges)){
          SourceContext newCtx = this.GetMethodBodyContextInNewDocument(changedDocument, meth.Body.SourceContext, 
            spanningContextForChanges, changeInLength);
          //Now update the original document so that all nodes that follow method in the source will report their source lines relative to the changedDocument
          int oldNumLines = meth.Body.SourceContext.EndLine - meth.Body.SourceContext.StartLine;
          int newNumLines = newCtx.EndLine - newCtx.StartLine;
          compilationUnit.SourceContext.Document.InsertOrDeleteLines(compilationUnit.OriginalEndPosOfChangedMethod, newNumLines-oldNumLines);
          //Replace the method body context with the new context
          meth.Body.SourceContext = newCtx;
          //Get rid of the body statements (if present) that was constructed from the old context
          meth.Body.Statements = null;
          return symbolTable;
        }
        return this.FullSymbolTableUpdate(symbolTable, originalDocument, changedDocument, errors);
      }
      MemberFinder memFinder = this.CreateMemberFinder(spanningContextForChanges);
      memFinder.Visit(compilationUnit);
      meth = memFinder.Member as Method;
      if (meth != null && meth.Body != null && meth.Body.SourceContext.Encloses(spanningContextForChanges))
        return this.IncrementalSymbolTableUpdate(symbolTable, compilationUnit, changedDocument, meth, spanningContextForChanges, changeInLength);
      else
        return this.FullSymbolTableUpdate(symbolTable, originalDocument, changedDocument, errors);
    }
    /// <summary>
    /// Updates the specified symbol table, given a list of changed members from another Compilation instance
    /// on which it has a dependency. Does nothing if the symbol table does not refer to any of the changed members.
    /// Fires the OnSymbolTableUpdate event before returning (provided that changes occurred to member signatures).
    /// </summary>
    /// <param name="symbolTable">The symbol table to update or replace.</param>
    /// <param name="originalReference">The compilation instance to which the given symbol table currently refers.</param>
    /// <param name="changedReference">The compilation instance to which the updated symbol table must refer to instead of to originalReference.</param>
    /// <param name="changedMembers">A list of the members defined in originalReference that have changed.</param>
    /// <param name="errors">A list to which errors detected during the update must be added.</param>
    /// <returns>The given symbol table instance, suitably updated, or a new symbol table that replaces the given table.</returns>
    public virtual Compilation UpdateSymbolTable(Compilation symbolTable, Compilation originalReference, Compilation changedReference, MemberList changedMembers, ErrorNodeList errors){
      if (symbolTable == null || symbolTable.TargetModule == null || originalReference == null || changedReference == null || changedMembers == null){
        Debug.Assert(false); return null;
      }
      this.UpdateReferencedCompilations(symbolTable, originalReference, changedReference);
      TrivialHashtable membersToFind = new TrivialHashtable();
      for (int i = 0, n = changedMembers.Count; i < n; i++){
        Member mem = changedMembers[i];
        if (mem == null) continue;
        membersToFind[mem.UniqueKey] = mem;
      }
      MemberReferenceFinder mrFinder = this.CreateMemberReferenceFinder(membersToFind, !symbolTable.TargetModule.IsNormalized);
      mrFinder.Visit(symbolTable);
      if (mrFinder.FoundMembers == null || mrFinder.FoundMembers.Count == 0) return symbolTable;
      if (mrFinder.AllReferencesAreConfinedToMethodBodies){
        symbolTable.TargetModule.IsNormalized = false;
        return symbolTable;
      }
      return this.FullSymbolTableUpdate(symbolTable, null, null, errors);
    }
    /// <summary>
    /// Called when a symbol table has been updated. The updateSpecification argument specifies how
    /// the original Compilation instance (symbol table) differs from the updated symbol table. The changedMembers argument
    /// provides a list of all member signatures that have changed as a result of the recompilation.
    /// </summary>
    public delegate void SymbolTableUpdateEventHandler(Compilation updatedSymbolTable, UpdateSpecification updateSpecification, MemberList changedMembers);
    /// <summary>
    /// This event happens just before UpdateSymbolTable returns, provided that UpdateSymbolTable made any changes.
    /// </summary>
    public event SymbolTableUpdateEventHandler OnSymbolTableUpdate;
    public void FireOnSymbolTableUpdate(Compilation updatedSymbolTable, UpdateSpecification updateSpecification, MemberList changedMembers){
      this.OnSymbolTableUpdate.BeginInvoke(updatedSymbolTable, updateSpecification, changedMembers, null, null);
    }
    #endregion
    #region Compiler option parsing
    //TODO: move this to a separate class
    public virtual string[] ParseCompilerParameters(CompilerParameters options, string[] arguments, ErrorNodeList errors){
      return this.ParseCompilerParameters(options, arguments, errors, true);
    }
    public virtual string[] ParseCompilerParameters(CompilerParameters options, string[] arguments, ErrorNodeList errors, bool checkSourceFiles){
      StringCollection filesToCompile = new StringCollection();
      Hashtable alreadySeenResponseFiles = null;
      bool gaveFileNotFoundError = false;
      foreach (string arg in arguments){
        if (arg == null || arg.Length == 0) continue;
        char ch = arg[0];
        if (ch == '@'){
          if (alreadySeenResponseFiles == null) alreadySeenResponseFiles = new Hashtable();
          string[] fileNames = this.ParseOptionBatch(options, arg, errors, alreadySeenResponseFiles);
          if (fileNames != null) filesToCompile.AddRange(fileNames);
        }else if (ch == '/' || ch == '-'){
          if (!this.ParseCompilerOption(options, arg, errors))
            errors.Add(this.CreateErrorNode(Error.InvalidCompilerOption, arg));
        }else {
          // allow URL syntax
          string s = arg.Replace('/', BetterPath.DirectorySeparatorChar);
          // allow wildcards
          string path = (arg.IndexOf("\\")<0) ? ".\\" : BetterPath.GetDirectoryName(arg);
          string pattern = BetterPath.GetFileName(arg);
          string extension = BetterPath.HasExtension(pattern) ? BetterPath.GetExtension(pattern) : "";
          bool notAFile = true;
          if (path != null && Directory.Exists(path)){
            foreach (string file in Directory.GetFiles(path, pattern)){
              string ext = BetterPath.HasExtension(file) ? BetterPath.GetExtension(file) : "";
              if (string.Compare(extension, ext, true, System.Globalization.CultureInfo.InvariantCulture) != 0) continue;
              filesToCompile.Add(file);
              notAFile = false;
            }
          }
          if (notAFile && checkSourceFiles){
            errors.Add(this.CreateErrorNode(Error.SourceFileNotRead, arg, this.CreateErrorNode(Error.NoSuchFile, arg).GetMessage()));
            gaveFileNotFoundError = true;
          }
        }
      }
      if (checkSourceFiles && filesToCompile.Count == 0 && !gaveFileNotFoundError)
        errors.Add(this.CreateErrorNode(Error.NoSourceFiles));
      string[] result = new string[filesToCompile.Count];
      filesToCompile.CopyTo(result, 0);
      return result;
    }
    public virtual bool ParseCompilerOption(CompilerParameters options, string arg, ErrorNodeList errors){
      CompilerOptions coptions = options as CompilerOptions;
      int n = arg.Length;
      if (n <= 1) return false;
      char ch = arg[0];
      if (ch != '/' && ch != '-') return false;
      ch = arg[1];
      switch(Char.ToLower(ch)){
        case 'a':
          if (coptions == null) return false;
          StringCollection referencedModules = this.ParseNamedArgumentList(arg, "addmodule", "addmodule");
          if (referencedModules != null){
            if (coptions.ReferencedModules == null) coptions.ReferencedModules = new StringList(referencedModules.Count);
            foreach (string referencedModule in referencedModules)
              coptions.ReferencedModules.Add(referencedModule);
            return true;
          }
          return false;
        case 'b':
          if (coptions == null) return false;
          string baseAddress = this.ParseNamedArgument(arg, "baseaddress", "baseaddress");
          if (baseAddress != null){
            try{
              coptions.BaseAddress = long.Parse(baseAddress, null); //TODO: figure out acceptable formats
              return true;
            }catch{}
          }
          string bugReportFileName = this.ParseNamedArgument(arg, "bugreport", "bugreport");
          if (bugReportFileName != null){
            coptions.BugReportFileName = bugReportFileName;
            return true;
          }
          if (this.ParseName(arg, "break", "break")) {
              System.Diagnostics.Debugger.Break();
              return true;
          }
          return false;
        case 'c':
          if (coptions == null) return false;
          string codePage = this.ParseNamedArgument(arg, "codepage", "codepage");
          if (codePage != null){
            try{
              coptions.CodePage = int.Parse(codePage, null); //TODO: figure out acceptable formats
              return true;
            }catch{}
          }
          object checkedOn = this.ParseNamedBoolean(arg, "checked", "checked");
          if (checkedOn != null){
            coptions.CheckedArithmetic = (bool)checkedOn;
            return true;
          }
          if (this.ParseName(arg, "compile", "c")){
            coptions.CompileAndExecute = false;
            return true;
          }
          return false;
        case 'd':
          if (coptions != null){
            string debugOption = this.ParseNamedArgument(arg, "debug", "debug");
            if (debugOption != null){
              if (debugOption == "pdbonly"){
                coptions.PDBOnly = true;
                return true;
              }else if (debugOption == "full"){
                coptions.PDBOnly = false;
                return true;
              }else
                return false; 
            }
          }
          object debugOn = this.ParseNamedBoolean(arg, "debug", "debug");
          if (debugOn != null){
            options.IncludeDebugInformation = (bool)debugOn;
            return true;
          }
          if (coptions != null){
            object delaySign = this.ParseNamedBoolean(arg, "delaysign", "delay");
            if (delaySign != null){
              coptions.DelaySign = (bool)delaySign;
              return true;
            }
            string xmlDocFileName = this.ParseNamedArgument(arg, "doc", "doc");
            if (xmlDocFileName != null){
              coptions.XMLDocFileName = xmlDocFileName;
              return true;
            }
            StringCollection definedSymbols = this.ParseNamedArgumentList(arg, "define", "d");
            if (definedSymbols != null){
              if (coptions.DefinedPreProcessorSymbols == null) 
                coptions.DefinedPreProcessorSymbols = new StringList(definedSymbols.Count);
              foreach (string definedSymbol in definedSymbols)
                coptions.DefinedPreProcessorSymbols.Add(definedSymbol);
              return true;
            }
            StringCollection disabledFeatures = this.ParseNamedArgumentList(arg, "disable", "disable");
            if (disabledFeatures != null){
              foreach (string feature in disabledFeatures){
                switch (feature){
                  case "ac":
                  case "assumechecks":
                    coptions.DisableAssumeChecks = true;
                    break;
                  case "dc":
                  case "defensivechecks":
                    coptions.DisableDefensiveChecks = true;
                    break;
                  case "gcc":
                  case "guardedclasseschecks":
                    coptions.DisableGuardedClassesChecks = true;
                    break;
                  case "ic":
                  case "internalchecks":
                    coptions.DisableInternalChecks = true;
                    break;
                  case "icm":
                  case "internalcontractsmetadata":
                    coptions.DisableInternalContractsMetadata = true;
                    break;
                  case "pcm":
                  case "publiccontractsmetadata":
                    coptions.DisablePublicContractsMetadata = true;
                    break;
                  case "npv":
                  case "nullparametervalidation":
                    coptions.DisableNullParameterValidation = true;
                    break;
                  default:
                    errors.Add(this.CreateErrorNode(Error.InvalidCompilerOptionArgument, String.Format("/disable argument '{0}' not recognized", feature)));
                    break;
                }
              }
              return true;
            }
          }
          return false;
        case 'f':
          if (coptions == null) return false;
          if (this.ParseName(arg, "fullpaths", "fullpaths")){
            coptions.FullyQualifyPaths = true;
            return true;
          }
          string fileAlignment = this.ParseNamedArgument(arg, "filealign", "filealign");
          if (fileAlignment != null){
            try{
              coptions.FileAlignment = int.Parse(fileAlignment, null); //TODO: figure out acceptable formats
              return true;
            }catch{}
          }
          return false;
        case 'h':
          if (coptions == null) return false;
          if (this.ParseName(arg, "help", "help")){
            coptions.DisplayCommandLineHelp = true;
            return true;
          }
          return false;
        case '?':
          if (coptions == null) return false;
          coptions.DisplayCommandLineHelp = true;
          return true;
        case 'i':
          if (coptions == null) return false;
          object incremental = this.ParseNamedBoolean(arg, "incremental", "incr");
          if (incremental != null){
            coptions.IncrementalCompile = (bool)incremental;
            return true;
          }
          return false;
        case 'k':
          if (coptions == null) return false;
          string keyFile = this.ParseNamedArgument(arg, "keyfile", "keyf");
          if (keyFile != null){
            coptions.AssemblyKeyFile = keyFile;
            return true;
          }
          string keyName = this.ParseNamedArgument(arg, "keyname", "keyn");
          if (keyName != null){
            coptions.AssemblyKeyName = keyName;
            return true;
          }
          return false;
        case 'l':
          if (coptions == null) return false;
          string linkedResource = this.ParseNamedArgument(arg, "linkresource", "linkres");
          if (linkedResource != null){
            //TODO: check string for validity: <filename>[,<name>[,public|private]]
            coptions.LinkedResources.Add(linkedResource);
            return true;
          }
          string lcid = this.ParseNamedArgument(arg, "lcid", "lcid");
          if (lcid != null){
            try{
              coptions.UserLocaleId = int.Parse(lcid, null); //TODO: figure out acceptable formats
              CultureInfo culture = new CultureInfo((int)coptions.UserLocaleId); //Convert to CultureInfo in order to check validity
              return true;
            }catch{}
          }
          StringCollection searchpaths = this.ParseNamedArgumentList(arg, "lib", "lib");
          if (searchpaths != null){
            if (coptions.AdditionalSearchPaths == null) coptions.AdditionalSearchPaths = new StringList();
            foreach (string searchpath in searchpaths)
              coptions.AdditionalSearchPaths.Add(searchpath);
            return true;
          }
          return false;
        case 'm':
          string mainClass = this.ParseNamedArgument(arg, "main", "m");
          if (mainClass != null){
            options.MainClass = mainClass;
            return true;
          }
          return false;
        case 'n':
          if (coptions != null){
            object nostdlib = this.ParseNamedBoolean(arg, "nostdlib", "nostdlib");
            if (nostdlib != null){
              coptions.NoStandardLibrary = (bool)nostdlib;
              return true;
            }
            if (this.ParseName(arg, "nologo", "nologo")){
              coptions.SuppressLogo = true;
              return true;
            }
            StringCollection noWarnList = this.ParseNamedArgumentList(arg, "nowarn", "nowarn");
            if (noWarnList != null){
              if (coptions.SuppressedWarnings == null)
                coptions.SuppressedWarnings = new Int32List(noWarnList.Count);
              foreach (string noWarning in noWarnList)
                try{
                  coptions.SuppressedWarnings.Add(int.Parse(noWarning, System.Globalization.CultureInfo.InvariantCulture));
                }catch{
                  return false;
                }
              return true;
            }
          }
          if (this.ParseName(arg, "nowarn", "nowarn")){
            options.WarningLevel = 0;
            return true;
          }
          return false;
        case 'o':
          string outputAssembly = this.ParseNamedArgument(arg, "out", "out");
          if (outputAssembly != null) {
#if WHIDBEY
            char[] invalidChars = Path.GetInvalidFileNameChars();
#else
            char[] invalidChars = Path.InvalidPathChars;
#endif
            if (outputAssembly.IndexOfAny(invalidChars) >= 0){
              if (coptions != null){
                coptions.ExplicitOutputExtension = "";
                coptions.OutputPath = Directory.GetCurrentDirectory();
                coptions.OutputAssembly = outputAssembly;
              }else{
                options.OutputAssembly = outputAssembly;
              }
            }else{
              if (coptions != null){
                coptions.ExplicitOutputExtension = Path.GetExtension(outputAssembly);
                coptions.OutputPath = Path.GetDirectoryName(Path.GetFullPath(outputAssembly));
                coptions.OutputAssembly = Path.GetFileName(outputAssembly);
              }else{
                options.OutputAssembly = Path.GetFullPath(outputAssembly);
              }
            }
            return true;
          }
          if (coptions == null) return false;
          object optimize = this.ParseNamedBoolean(arg, "optimize", "o");
          if (optimize != null){
            coptions.Optimize = (bool)optimize;
            return true;
          }
          return false;
        case 'p':
          if (coptions != null){
            StringCollection platformOptions = this.ParseNamedArgumentList(arg, "platform", "p");
            if (platformOptions != null && platformOptions.Count > 0){
              try{
                coptions.TargetPlatform = (PlatformType)Enum.Parse(typeof(PlatformType), platformOptions[0]);
              }catch{
                errors.Add(this.CreateErrorNode(Error.InvalidCompilerOptionArgument, String.Format("Bad /platform type '{0}'", platformOptions[0])));
                return true;
              }
              if (platformOptions.Count > 1){
                coptions.TargetPlatformLocation = platformOptions[1];
                if (!Directory.Exists(platformOptions[1])) {
                  errors.Add(this.CreateErrorNode(Error.InvalidCompilerOptionArgument, String.Format("/platform directory '{0}' does not exist", platformOptions[1])));
                  return true;
                }
              }
              return true;
            }
            if (this.ParseName(arg, "parseonly", "parseonly")) {
              coptions.EmitSourceContextsOnly = true;
              return true;
            }
          }
          return false;
        case 'r':
          if (coptions != null){
            string recurseWildcard = this.ParseNamedArgument(arg, "recurse", "recurse");
            if (recurseWildcard != null){
              coptions.RecursiveWildcard = recurseWildcard;
              return true;
            }
            string embeddedResource = this.ParseNamedArgument(arg, "resource", "res");
            if (embeddedResource != null){
              coptions.EmbeddedResources.Add(embeddedResource);
              return true;
            }
          }
          StringCollection referencedAssemblies = this.ParseNamedArgumentList(arg, "reference", "r");
          if (referencedAssemblies != null){
            if (coptions != null){
              if (coptions.AliasesForReferencedAssemblies == null)
                coptions.AliasesForReferencedAssemblies = new StringCollection();
              foreach (string referencedAssembly in referencedAssemblies){
                if (referencedAssembly == null) continue;
                int eqIndx = referencedAssembly.IndexOf('=');
                if (eqIndx < 0){
                  coptions.AliasesForReferencedAssemblies.Add(string.Empty);
                  options.ReferencedAssemblies.Add(referencedAssembly);
                }else{
                  coptions.AliasesForReferencedAssemblies.Add(referencedAssembly.Substring(0, eqIndx));
                  options.ReferencedAssemblies.Add(referencedAssembly.Substring(eqIndx + 1));
                }
              }
            }else{
              foreach (string referencedAssembly in referencedAssemblies)
                options.ReferencedAssemblies.Add(referencedAssembly);
            }
            return true;
          }
          return false;
        case 's' :
          if (coptions != null){
            string standardLibraryLocation = this.ParseNamedArgument(arg, "stdlib", "s");
            if (standardLibraryLocation != null){
              if (!Path.IsPathRooted(standardLibraryLocation))
              {
                standardLibraryLocation = Path.Combine(coptions.TargetPlatformLocation, standardLibraryLocation);
              }
              if (!File.Exists(standardLibraryLocation)) {
                errors.Add(this.CreateErrorNode(Error.InvalidCompilerOptionArgument, String.Format("/stdlib assembly '{0}' does not exist", standardLibraryLocation)));
                return true;
              }
              coptions.StandardLibraryLocation = standardLibraryLocation;
              return true;
            }
            string shadowedAssembly = this.ParseNamedArgument(arg, "shadow", "shadow");
            if (shadowedAssembly != null){
              if (!File.Exists(shadowedAssembly)){ //TODO: move the error check elsewhere and use probing to find the assembly, just like normal assembly references
                errors.Add(this.CreateErrorNode(Error.InvalidCompilerOptionArgument, String.Format("/shadow assembly '{0}' does not exist", shadowedAssembly)));
                return true;
              }
              coptions.ShadowedAssembly = shadowedAssembly;
              coptions.IsContractAssembly = true;
              return true;
            }
          }
          return false;
        case 't':
          string targetType = this.ParseNamedArgument(arg, "target", "t");
          if (targetType == "exe"){
            options.GenerateExecutable = true;
            return true;
          }
          if (targetType == "library"){
            options.GenerateExecutable = false;
            if (coptions != null) coptions.ModuleKind = ModuleKindFlags.DynamicallyLinkedLibrary;
            return true;
          }
          if (targetType == "winexe"){
            options.GenerateExecutable = true;
            if (coptions != null) coptions.ModuleKind = ModuleKindFlags.WindowsApplication;
            return true;
          }
          if (targetType == "module"){
            options.GenerateExecutable = false;
            if (coptions != null) {
              coptions.ModuleKind = ModuleKindFlags.DynamicallyLinkedLibrary;
              coptions.EmitManifest = false;
            }else
              return false;
            return true;
          }
          return false;
        case 'u':
          if (coptions == null) return false;
          object utf8output = this.ParseNamedBoolean(arg, "utf8output", "utf8output");
          if (utf8output != null){
            coptions.EncodeOutputInUTF8 = (bool)utf8output;
            return true;
          }
          object unsafeOn = this.ParseNamedBoolean(arg, "unsafe", "unsafe");
          if (unsafeOn != null){
            coptions.AllowUnsafeCode = (bool)unsafeOn;
            return true;
          }
          return false;
        case 'w':
          if (coptions != null){
            //TODO: need a ParseNamedNameArgumentListWithBoolen to deal with /warnaserror-:<error list>
            StringCollection warnAsErrorCollection = this.ParseNamedArgumentList(arg, "warnaserror", "warnaserror");
            if (warnAsErrorCollection != null){
              coptions.SpecificWarningsToTreatAsErrors = new Int32List(warnAsErrorCollection.Count);
              foreach (string warn in warnAsErrorCollection)
                try{
                  coptions.SpecificWarningsToTreatAsErrors.Add(int.Parse(warn, System.Globalization.CultureInfo.InvariantCulture));
                }catch{
                  return false;
                }
              return true;
            }
          }
          object warnAsError = this.ParseNamedBoolean(arg, "warnaserror", "warnaserror");
          if (warnAsError != null){
            options.TreatWarningsAsErrors = (bool)warnAsError;
            return true;
          }
          if (coptions != null){
            string win32icon = this.ParseNamedArgument(arg, "win32icon", "win32icon");
            if (win32icon != null){
              coptions.Win32Icon = win32icon;
              return true;
            }
          }
          string win32resource = this.ParseNamedArgument(arg, "win32res", "win32res");
          if (win32resource != null){
            options.Win32Resource = win32resource;
            return true;
          }
          string warningLevel = this.ParseNamedArgument(arg, "warn", "w");
          if (warningLevel != null){
            switch(warningLevel){
              case "0": options.WarningLevel = 0; break;
              case "1": options.WarningLevel = 1; break;
              case "2": options.WarningLevel = 2; break;
              case "3": options.WarningLevel = 3; break;
              case "4": options.WarningLevel = 4; break;
              default: return false;
            }
            return true;
          }
          return false;
      }
      return false;
    }
    public virtual string[] ParseOptionBatch(CompilerParameters options, string arg, ErrorNodeList errors, Hashtable alreadySeenResponseFiles){
      Debug.Assert(arg != null);
      if (arg.Length < 2){
        errors.Add(this.CreateErrorNode(Error.InvalidCompilerOption, arg));
        return null;
      }
      return ParseOptionBatchFile(options, arg.Substring(1), errors, alreadySeenResponseFiles);
    }
    private static char[] spaceNewLineTabQuote = {' ', (char)0x0A, (char)0x0D, '\t', '"'};
    public string[] ParseOptionBatchFile(CompilerParameters options, string batchFileName, ErrorNodeList errors){
      return this.ParseOptionBatchFile(options, batchFileName, errors, new Hashtable());
    }
    public string[] ParseOptionBatchFile(CompilerParameters options, string batchFileName, ErrorNodeList errors, Hashtable alreadySeenResponseFiles){
      try{
        batchFileName = System.IO.Path.GetFullPath(batchFileName);
        if (alreadySeenResponseFiles[batchFileName] != null){
          errors.Add(this.CreateErrorNode(Error.DuplicateResponseFile, batchFileName));
          return null;
        }else
          alreadySeenResponseFiles[batchFileName] = batchFileName;
        string optionBatch = this.ReadSourceText(batchFileName, options, errors);
        StringCollection opts = new StringCollection();
        bool insideQuotedString = false;
        bool insideComment = false;
        for (int i = 0, j = 0, n = optionBatch.Length; j < n; j++){
          switch(optionBatch[j]){
            case (char)0x0A:
            case (char)0x0D:
              insideQuotedString = false;
              if (insideComment){
                insideComment = false;
                i = j+1;
                break;
              }
              goto case ' ';
            case ' ':
            case '\t':
              if (insideQuotedString || insideComment) break;
              if (i < j)
                opts.Add(optionBatch.Substring(i, j-i));
              i = j+1;
              break;
            case '"':
              if (insideQuotedString){
                if (!insideComment)
                  opts.Add(optionBatch.Substring(i, j-i));
                insideQuotedString = false;
              }else
                insideQuotedString = true;
              i = j+1;
              break;
            case '#':
              insideComment = true;
              break;
            default:
              if (j == n-1 && i < j)
                opts.Add(optionBatch.Substring(i, n-i));
              break;
          }
        }
        int c = opts.Count;
        string[] args = new string[c];
        opts.CopyTo(args, 0);
        return this.ParseCompilerParameters(options, args, errors, false);
      }catch(ArgumentException){
      }catch(System.IO.IOException){
      }catch(NotSupportedException){
      }catch(System.Security.SecurityException){
      }catch(UnauthorizedAccessException){
      }
      errors.Add(this.CreateErrorNode(Error.BatchFileNotRead, batchFileName, this.CreateErrorNode(Error.NoSuchFile, batchFileName).GetMessage()));
      return null;
    }
    public virtual bool ParseName(string arg, string name, string shortName){
      arg = arg.ToLower();
      int n = arg.Length;
      int j = name.Length;
      int k = shortName.Length;
      int i = 0;
      if (n > j) i = arg.IndexOf(name, 1, j);
      if (i < 1 && j > k && n > k){
        i = arg.IndexOf(shortName, 1, k);
        j = k;
      }
      if (i < 1) return false;
      if (++j >= n) return true;
      char ch = arg[j];
      if (ch == ' ' || ch == '/' || ch == (char)9 || ch == (char)10 || ch == (char)13) return true;
      return false;
    }
    public virtual object ParseNamedBoolean(string arg, string name, string shortName){
      arg = arg.ToLower();
      int n = arg.Length;
      int j = name.Length;
      int k = shortName.Length;
      int i = 0;
      if (n > j) i = arg.IndexOf(name, 1, j);
      if (i < 1 && j > k && n > k){ 
        i = arg.IndexOf(shortName, 1, k);
        j = k;
      }
      if (i < 1) return null;
      if (++j >= n) return true;
      char ch = arg[j];
      if (ch == '+') return true;
      if (ch == '-') return false;
      if (ch == ' ' || ch == '/' || ch == (char)9 || ch == (char)10 || ch == (char)13) return true;
      return null;
    }
    public virtual string ParseNamedArgument(string arg, string name, string shortName){
      string arg1 = arg.ToLower();
      int n = arg.Length;
      int j = name.Length;
      int k = shortName.Length;
      int i = 0;
      if (n > j) i = arg1.IndexOf(name, 1, j);
      if (i < 1 && j > k && n > k){
        i = arg1.IndexOf(shortName, 1, k);
        j = k;
      }
      if (i < 1) return null;
      if (++j >= n) return null;
      if (arg[j] != ':') return null;
      return arg.Substring(j+1);
    }
    public virtual StringCollection ParseNamedArgumentList(string arg, string name, string shortName){
      string argList = this.ParseNamedArgument(arg, name, shortName);
      if (argList == null || argList.Length == 0) return null;
      StringCollection result = new StringCollection();
      int i = 0;
      for (int n = argList.Length; i < n;){
        int separatorIndex = this.GetArgumentSeparatorIndex(argList, i);
        if (separatorIndex > i){
          result.Add(argList.Substring(i, separatorIndex-i));
          i = separatorIndex+1;
          continue;
        }
        result.Add(argList.Substring(i));
        break;
      }       
      return result;
    }
    public int GetArgumentSeparatorIndex(string argList, int startIndex){
      int commaIndex = argList.IndexOf(",", startIndex);
      int semicolonIndex = argList.IndexOf(";", startIndex);
      if (commaIndex == -1) return semicolonIndex;
      if (semicolonIndex == -1) return commaIndex;
      if (commaIndex < semicolonIndex) return commaIndex;
      return semicolonIndex;
    }
    #endregion
    #region Methods that allow the behavior of Compiler to be specialized. Not expected to be used by client code.
    #region Factory methods that create language specific things
    public virtual AssemblyNode CreateAssembly(CompilerParameters options, ErrorNodeList errorNodes){
      CompilerOptions coptions = options as CompilerOptions;
      if (coptions != null) coptions.EmitManifest = true;
      return (AssemblyNode)this.CreateModule(options, errorNodes);
    }
    public virtual Comparer CreateComparer(){
      return new Comparer();
    }
    public virtual Compilation CreateCompilation(Module targetModule, CompilationUnitList compilationUnits, System.CodeDom.Compiler.CompilerParameters compilerParameters, Scope globalScope){
      return new Compilation(targetModule, compilationUnits, compilerParameters, globalScope);
    }
    public virtual CompilationUnitSnippet CreateCompilationUnitSnippet(string fileName, int lineNumber, DocumentText text, Compilation compilation){
      Document doc = this.CreateDocument(Path.GetFullPath(fileName), 1, text);               
      CompilationUnitSnippet cu = new CompilationUnitSnippet();
      cu.Name = Identifier.For(doc.Name);
      cu.SourceContext = new SourceContext(doc);
      cu.Compilation = compilation;
      return cu;
    }
    public virtual CompilerOptions CreateCompilerOptions(){
      return new CompilerOptions();
    }
    public virtual Document CreateDocument(string fileName, int lineNumber, string text){
      Debug.Assert(false);
      return null;
    }
    public virtual Document CreateDocument(string fileName, int lineNumber, DocumentText text){
      Debug.Assert(false);
      return null;
    }
    public virtual ErrorNode CreateErrorNode(Error error, params string[] messageParameters){
      return new CommonErrorNode(error, messageParameters);
    }
    public virtual ErrorNode CreateErrorNode(Error error, Method method){
      ErrorNode result = new CommonErrorNode(error);
      result.MessageParameters = new string[]{method.FullName};
      result.SourceContext = method.Name.SourceContext;
      return result;
    }
    public virtual MemberFinder CreateMemberFinder(SourceContext spanningContextForChanges){
      return new MemberFinder(spanningContextForChanges);
    }
    public virtual MemberReferenceFinder CreateMemberReferenceFinder(TrivialHashtable membersToFind, bool omitMethodBodies){
      return new MemberReferenceFinder(membersToFind, omitMethodBodies);
    }
    public virtual Module CloneModuleForSymbolTableUse(Module module){
      if (module == null) return null;
      Module result = module is AssemblyNode ? new AssemblyNode() : new Module();
      result.Name = module.Name;
      result.Kind = module.Kind;
      result.ModuleReferences = module.ModuleReferences;
      result.Documentation = new System.Xml.XmlDocument();
      result.AssemblyReferences = module.AssemblyReferences;
      result.Types = new TypeNodeList();
      result.Types.Add(module.Types[0]);
      return result;
    }
    public virtual Module CreateModule(CompilerParameters options, ErrorNodeList errorNodes){
      return this.CreateModule(options, errorNodes, null);
    }
    public virtual Module CreateModule(CompilerParameters options, ErrorNodeList errorNodes, Compilation compilation){
      if (options == null){Debug.Assert(false); return null;}
      Compilation savedCurrentCompilation = this.CurrentCompilation;
      this.CurrentCompilation = compilation;
      CompilerOptions coptions = options as CompilerOptions;
      AssemblyNode assem = null;
      Module module = coptions == null || coptions.EmitManifest ? (assem = new AssemblyNode()) : new Module();
      string ext = Path.GetExtension(options.OutputAssembly);
      if (assem != null && string.Compare(ext, this.GetTargetExtension(options), true, System.Globalization.CultureInfo.InvariantCulture) == 0)
        module.Name = Path.GetFileNameWithoutExtension(options.OutputAssembly);
      else
        module.Name = Path.GetFileName(options.OutputAssembly);
      if (module.Name == null){
        if (assem != null)
          module.Name = "module"+module.GetHashCode();
        else
          module.Name = "module"+module.GetHashCode()+this.GetTargetExtension(options);
      }
      if (coptions == null)
        module.Kind = options.GenerateExecutable ? ModuleKindFlags.ConsoleApplication : ModuleKindFlags.DynamicallyLinkedLibrary;
      else
        module.Kind = coptions.ModuleKind;
      module.PEKind = PEKindFlags.ILonly;
      if (assem != null) assem.Version = new Version(0,0,0,0);
      module.ModuleReferences = new ModuleReferenceList();
      if (coptions != null && coptions.ReferencedModules != null){
        TrivialHashtable alreadyReferencedModules = new TrivialHashtable();
        for (int i = 0, n = coptions.ReferencedModules.Count; i < n; i++)
          this.AddModuleReferenceToModule(coptions, module, coptions.ReferencedModules[i], null, errorNodes, alreadyReferencedModules, true);
      }
      module.Documentation = new System.Xml.XmlDocument();
      module.AssemblyReferences = new AssemblyReferenceList();
      TrivialHashtable alreadyReferencedAssemblies = new TrivialHashtable();
      int numRefs = options.ReferencedAssemblies.Count;
      if (numRefs > 0 && coptions != null) {
        if (coptions.AliasesForReferencedAssemblies == null) coptions.AliasesForReferencedAssemblies = new StringCollection();
        for (int i = 0, n = coptions.AliasesForReferencedAssemblies.Count; i < numRefs; i++) {
          string rAssemblyName = coptions.ReferencedAssemblies[i];
          string rAliases = i >= n ? null : coptions.AliasesForReferencedAssemblies[i];
          this.AddAssemblyReferenceToModule(coptions, module, rAssemblyName, rAliases, errorNodes, alreadyReferencedAssemblies, true);
        }
      }
      this.AddStandardLibraries(coptions, module, alreadyReferencedAssemblies);
      this.ApplyAndRemoveContractAssemblies(coptions, module);
      if (coptions != null && !coptions.NoStandardLibrary) // We're overloading the meaning of this option here...
        this.ApplyDefaultContractAssemblies(module, !coptions.IsContractAssembly && coptions.ShadowedAssembly != null && coptions.ShadowedAssembly.Length > 0);
      if (options.IncludeDebugInformation){
        if (coptions == null || !coptions.Optimize)
          module.TrackDebugData = true;
        else if (!coptions.PDBOnly)
          module.TrackDebugData = true;
      }
      TypeNodeList types = module.Types = new TypeNodeList();
      Class hiddenClass = new Class(module, null, null, TypeFlags.Public, Identifier.Empty, Identifier.For("<Module>"), null, new InterfaceList(), new MemberList(0));
      types.Add(hiddenClass);
      this.CurrentCompilation = savedCurrentCompilation;
      return module;
    }
    protected virtual void ApplyAndRemoveContractAssemblies(CompilerOptions coptions, Module module){
      if (module == null) return;
      AssemblyReferenceList references = module.AssemblyReferences;
      if (references == null) return;
      AssemblyReferenceList keepers = new AssemblyReferenceList();
      for (int i = 0; i < references.Count; i++){
        AssemblyReference aref = references[i];
        if (aref == null) continue;
        AssemblyNode contractAssem = references[i].Assembly;
        if (contractAssem == null) continue;
        if (coptions != null && coptions.NoStandardLibrary && aref.Name != null && !aref.Name.EndsWith("Contracts")) {
          keepers.Add(aref);
          continue;
        }
        AttributeNode attr = contractAssem.GetAttribute(SystemTypes.ShadowsAssemblyAttribute);
        if (attr == null || contractAssem.ContractAssembly != null) {
          keepers.Add(aref);
          continue;
        }
        if (attr.Expressions == null || attr.Expressions.Count < 3) continue; //Bad attribute
        Literal lit = attr.Expressions[0] as Literal;
        if (lit == null) continue; //Bad attribute
        string publicKeyString = lit.Value as String;
        if (publicKeyString == null) continue; //Bad attribute
        int n = publicKeyString.Length;
        if ((n & 1) == 1) continue; //Bad attribute
        byte[] publicKey = new byte[n/2];
        for (int j = 0; j < n; j += 2)
          publicKey[j/2] = (byte)int.Parse(publicKeyString.Substring(j,2), System.Globalization.NumberStyles.HexNumber);
        lit = attr.Expressions[1] as Literal;
        if (lit == null) continue;
        string version = lit.Value as string;
        if (version == null) continue;
        lit = attr.Expressions[2] as Literal;
        if (lit == null) continue;
        string contractAssemName = lit.Value as string;
        if (contractAssemName == null) continue;
        Version contractVersion = null;
        try{
          contractVersion = new Version(version);
        }catch{}
        if (contractVersion == null) continue;

        AssemblyNode matchesExceptForVersion = null;
        AssemblyNode exactMatch = null;
        for (int j = 0; j < references.Count; j++){
          AssemblyReference cref = references[j];
          if (cref == null) continue;
          AssemblyNode codeAssembly = cref.Assembly;
          if (codeAssembly == null || codeAssembly.ContractAssembly != null || codeAssembly.Version == null) continue;
          if (string.Compare(codeAssembly.Name, contractAssemName, true, CultureInfo.InvariantCulture) != 0) continue;
          bool sameKey = true;
          if (publicKey.Length == 0 && codeAssembly.PublicKeyToken == null){
            // this is okay: neither has a public key
            ;
          }else{ // public keys must match
            for (int b = 0; sameKey && b < codeAssembly.PublicKeyToken.Length; b++){
              if (publicKey[b] != codeAssembly.PublicKeyToken[b]){
                sameKey = false;
                break;
              }
            }
          }
          if (!sameKey) continue;
          Version codeAssemblyVersion = codeAssembly.Version;
          if (codeAssemblyVersion == null) codeAssemblyVersion = new Version();
          if (codeAssemblyVersion != contractVersion){
            matchesExceptForVersion = codeAssembly;
            continue;
          }
          exactMatch = codeAssembly;
          break;
        }
        if (exactMatch != null)
          exactMatch.ContractAssembly = contractAssem;
        else if (matchesExceptForVersion != null)
          matchesExceptForVersion.ContractAssembly = contractAssem;
      }
      module.AssemblyReferences = keepers;
    }
    /// <summary>
    /// For each referenced assembly Xyz, looks for a contract assembly named Xyz.Contracts.dll in our installation directory.
    /// Also looks in the same directory as Xyz (just like the xml and pdb files are looked for automatically)
    /// </summary>
    protected virtual void ApplyDefaultContractAssemblies(Module module, bool contractAssembliesAreComplete){
      if (module == null) return;
      AssemblyReferenceList references = module.AssemblyReferences;
      if (references == null) return;
      try{
        string codebase = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
        codebase = codebase.Replace("#", "%23");
        Uri codebaseUri = new Uri(codebase);
        for (int i = 0; i < references.Count; i++) {
          AssemblyReference aref = references[i];
          if (aref == null) continue;
          AssemblyNode refAssem = aref.Assembly;
          if (refAssem == null) continue;
          if (refAssem.ContractAssembly != null) continue;
          string contractName = refAssem.Name + ".Contracts.dll";
          Uri contractUri = new Uri(codebaseUri, contractName);
          string contractPath = contractUri.LocalPath;
          // check if there is one in the installation directory
          if (File.Exists(contractPath)) {
            AssemblyNode contractAssembly = AssemblyNode.GetAssembly(contractPath, true, false, true);
            refAssem.ContractAssembly = contractAssembly;
            continue;
          }
          // check if there is one in the same directory as the referenced assembly
          contractPath = PathWrapper.Combine(refAssem.Directory, refAssem.Name + ".Contracts.dll");
          if (File.Exists(contractPath)) {
            AssemblyNode contractAssembly = AssemblyNode.GetAssembly(contractPath, this.AssemblyCache, true, false, true);
            if (contractAssembliesAreComplete)
              references[i] = new AssemblyReference(contractAssembly);
            else
              refAssem.ContractAssembly = contractAssembly;
            continue;
          }
          // check if there is one in the contracts subdirectory of the referenced assembly
          contractPath = PathWrapper.Combine(refAssem.Directory+"\\Contracts", refAssem.Name + ".Contracts.dll");
          if (File.Exists(contractPath)) {
            AssemblyNode contractAssembly = AssemblyNode.GetAssembly(contractPath, true, false, true);
            refAssem.ContractAssembly = contractAssembly;
            continue;
          }
        }
      }catch{}
    }

    public virtual IParser CreateParser(string fileName, int lineNumber, DocumentText text, Module symbolTable, ErrorNodeList errorNodes, CompilerParameters options){
      Debug.Assert(false);
      return null;
    }
    public virtual SnippetParser CreateSnippetParser(Module symbolTable, ErrorNodeList errorNodes, CompilerParameters options){ 
      return new SnippetParser(this, symbolTable, errorNodes, options);
    }
    #endregion
    #region Error Reporting and Formatting
    protected string fatalErrorString = null;
    public virtual string GetFatalErrorString(){
      if (this.fatalErrorString == null)
        this.fatalErrorString = this.CreateErrorNode(Error.FatalError).GetMessage();
      return this.fatalErrorString;
    }
    public virtual string GetFormattedErrorMessage(CompilerError e, bool omitSourceFilePath){
      if (e == null) return "";
      string errCode = (e.IsWarning) ? this.GetWarningString() : this.GetErrorString();
      string fileRef = e.FileName;
      if (fileRef == null)
        fileRef = "";
      else if (fileRef != ""){
        if (omitSourceFilePath) fileRef = Path.GetFileName(fileRef);
        fileRef += "(" + e.Line + "," + e.Column + "): ";
      }
      return fileRef + string.Format(errCode, e.ErrorNumber) + ": " + e.ErrorText;
    }
    protected string errorString = null;
    public virtual string GetErrorString(){
      if (this.errorString == null)
        this.errorString = this.CreateErrorNode(Error.Error).GetMessage();
      return this.errorString;
    }
    protected string warningString = null;
    public virtual string GetWarningString(){
      if (this.warningString == null)
        this.warningString = this.CreateErrorNode(Error.Warning).GetMessage();
      return this.warningString;
    }
    public virtual void HandleGenericMainMethodError(Method mainMethod, CompilerParameters options, CompilerResults results) {
      ErrorNode enode = this.CreateErrorNode(Error.MainCantBeGeneric, mainMethod);
      ErrorNodeList errorNodes = new ErrorNodeList(1);
      errorNodes.Add(enode);
      this.ProcessErrors(options, results, errorNodes);
    }
    public virtual void HandleMultipleMainMethodError(Method mainMethod, CompilerParameters options, CompilerResults results) {
      ErrorNode enode = this.CreateErrorNode(Error.MultipleMainMethods, mainMethod);
      string[] mpars = new String[2];
      mpars[0] = enode.MessageParameters[0];
      mpars[1] = options.OutputAssembly;
      enode.MessageParameters = mpars;
      ErrorNodeList errorNodes = new ErrorNodeList(1);
      errorNodes.Add(enode);
      this.ProcessErrors(options, results, errorNodes);
    }
    public virtual bool IsWarningRelatedMessage(ErrorNode enode){
      if (enode == null) return false;
      return enode.Code == (int)Error.RelatedWarningLocation || enode.Code == (int)Error.RelatedWarningModule;
    }
    public virtual void ProcessErrors(CompilerParameters options, CompilerResults results, ErrorNodeList errorNodes){
      if (results == null || errorNodes == null) return;
      CompilerOptions coptions = options as CompilerOptions;
      CultureInfo userCulture = null;
      if (coptions != null && coptions.UserLocaleId != null)
        userCulture = new CultureInfo((int)coptions.UserLocaleId);
      CompilerErrorCollection errors = results.Errors;
      int prevSev = 0;
      bool suppressRelated = false;
      for (int i = 0, n = errorNodes.Count; i < n; i++){
        ErrorNode enode = errorNodes[i];
        if (enode == null) continue;
        if (suppressRelated && this.IsWarningRelatedMessage(enode)) continue;
        suppressRelated = false;
        CompilerErrorEx error = new CompilerErrorEx();
        Document doc = enode.SourceContext.Document;
        if (doc != null) error.FileName = doc.Name;
        error.Line = enode.SourceContext.StartLine;
        error.Column = enode.SourceContext.StartColumn;
        error.EndLine = enode.SourceContext.EndLine;
        error.EndColumn = enode.SourceContext.EndColumn;
        error.ErrorText = enode.GetMessage(userCulture);
        error.ErrorNumber = enode.GetErrorNumber();
        int endLine = enode.SourceContext.EndLine;
        int endCol = enode.SourceContext.EndColumn;
        Debug.Assert(endLine >= error.Line || endCol >= error.Column || doc == null);
        int severity = enode.Severity;
        if (!enode.DoNotSuppress && severity > 0 && coptions != null && coptions.SuppressedWarnings != null){
          for (int j = 0, m = coptions.SuppressedWarnings.Count; j < m; j++){
            if (coptions.SuppressedWarnings[j] == enode.Code){
              suppressRelated = true;
              goto nextError;
            }
          }
        }
        if (severity < 0){
          if (prevSev <= options.WarningLevel || options.WarningLevel < 1){
            errors.Add(error);
            error.IsWarning = !(severity == 0 || (options.TreatWarningsAsErrors && (severity <= options.WarningLevel || options.WarningLevel < 1)));
          }
          continue;
        }
        if (severity == 0 || (options.TreatWarningsAsErrors && (severity <= options.WarningLevel || options.WarningLevel < 1)))
          results.NativeCompilerReturnValue = 1;
        else
          error.IsWarning = true;
        if (severity <= options.WarningLevel || options.WarningLevel < 1)
          errors.Add(error);
        prevSev = severity;
      nextError:;
      }
    }
    #endregion
    #region Assembly and Module reference handling
    public virtual string GetFullyQualifiedNameForReferencedLibrary(CompilerOptions options, string rLibraryName){
      if (File.Exists(rLibraryName)) return rLibraryName;
      rLibraryName = Path.GetFileName(rLibraryName);
      string mscorlibLocation = SystemAssemblyLocation.Location;
      if (mscorlibLocation == null) mscorlibLocation = typeof(object).Assembly.Location;
      string rtfName = Path.GetDirectoryName(mscorlibLocation) + Path.DirectorySeparatorChar + rLibraryName;
      if (File.Exists(rtfName)) return rtfName;
      StringList searchPaths = null;
      if (options != null && (searchPaths = options.AdditionalSearchPaths) != null){
        for (int i = 0, n = searchPaths.Count; i < n; i++){
          string path = searchPaths[i];
          string fname = path + Path.DirectorySeparatorChar + rLibraryName;
          if (File.Exists(fname)) return fname;
        }
      }
      return null;
    }
    public virtual System.Collections.ArrayList GetRuntimeLibraries() {
      System.Collections.ArrayList result = new System.Collections.ArrayList(1);
      result.Add(SystemCompilerRuntimeAssemblyLocation.Location);
      return result;
    }
    public virtual void UpdateRuntimeAssemblyLocations(CompilerOptions coptions){
      if (coptions == null) return;
      if (coptions.NoStandardLibrary){
        for (int i = 0, n = coptions.ReferencedAssemblies == null ? 0 : coptions.ReferencedAssemblies.Count; i < n; i++){
          string aref = coptions.ReferencedAssemblies[i];
          if (aref == null) continue;
          aref = Path.GetFileName(aref);
          aref = aref.ToLower(CultureInfo.InvariantCulture);
          if (aref == "mscorlib.dll"){
            SystemAssemblyLocation.Location = this.GetFullyQualifiedNameForReferencedLibrary(coptions, coptions.ReferencedAssemblies[i]);
            continue;
          }
          if (aref == "system.compiler.runtime.dll"){
            SystemCompilerRuntimeAssemblyLocation.Location = this.GetFullyQualifiedNameForReferencedLibrary(coptions, coptions.ReferencedAssemblies[i]);
            continue;
          }
          if (aref == "microsoft.cci.runtime.dll"){
            SystemCompilerRuntimeAssemblyLocation.Location = this.GetFullyQualifiedNameForReferencedLibrary(coptions, coptions.ReferencedAssemblies[i]);
            continue;
          }
#if !NoData
          if (aref == "system.data.dll"){
            SystemDataAssemblyLocation.Location = this.GetFullyQualifiedNameForReferencedLibrary(coptions, coptions.ReferencedAssemblies[i]);
            continue;
          }
#endif
#if !NoXml && !NoRuntimeXml
          if (aref == "system.xml.dll"){
            SystemXmlAssemblyLocation.Location = this.GetFullyQualifiedNameForReferencedLibrary(coptions, coptions.ReferencedAssemblies[i]);
            continue;
          }
#endif
        }
      }
      if (coptions.StandardLibraryLocation != null && coptions.StandardLibraryLocation.Length > 0){
        SystemAssemblyLocation.Location = coptions.StandardLibraryLocation;
      }
    }
    public virtual void AddStandardLibraries(CompilerOptions coptions, Module module, TrivialHashtable alreadyReferencedAssemblies){
      this.AllocateAssemblyCacheIfNeeded();
      if ((coptions == null || !coptions.NoStandardLibrary) && alreadyReferencedAssemblies[SystemTypes.SystemAssembly.UniqueKey] == null){
        AssemblyReference aref = new AssemblyReference(SystemTypes.SystemAssembly);
        module.AssemblyReferences.Add(aref);
        alreadyReferencedAssemblies[SystemTypes.SystemAssembly.UniqueKey] = SystemTypes.SystemAssembly;
        if (aref.PublicKeyOrToken == null || aref.PublicKeyOrToken.Length == 0)
          this.AssemblyCache[aref.Name] = SystemTypes.SystemAssembly;
        //TODO: also need to do this if the static cache is used. (REVIEW: is this possible?)
      }
      this.AddExtendedRuntimeLibrary(coptions, module, alreadyReferencedAssemblies);
    }
    public virtual void AddExtendedRuntimeLibrary(CompilerOptions coptions, Module module, TrivialHashtable alreadyReferencedAssemblies) {
      if ((coptions == null || !coptions.NoStandardLibrary) && alreadyReferencedAssemblies[SystemTypes.SystemCompilerRuntimeAssembly.UniqueKey] == null) {
        AssemblyReference aref = new AssemblyReference(SystemTypes.SystemCompilerRuntimeAssembly);
        module.AssemblyReferences.Add(aref);
        alreadyReferencedAssemblies[SystemTypes.SystemCompilerRuntimeAssembly.UniqueKey] = SystemTypes.SystemCompilerRuntimeAssembly;
        if (aref.PublicKeyOrToken == null || aref.PublicKeyOrToken.Length == 0)
          this.AssemblyCache[aref.Name] = SystemTypes.SystemCompilerRuntimeAssembly;
      }
    }
    public virtual void AddAssemblyReferenceToModule(CompilerOptions options, Module module, string name, string aliases, ErrorNodeList errorNodes, TrivialHashtable alreadyReferencedAssemblies,
      bool giveErrorsForDuplicateReferences){
      this.AddReferenceToModule(options, module, name, aliases, errorNodes, alreadyReferencedAssemblies, giveErrorsForDuplicateReferences, true);
    }
    public virtual void AddModuleReferenceToModule(CompilerOptions options, Module module, string name, string aliases, ErrorNodeList errorNodes, TrivialHashtable alreadyReferencedModules,
      bool giveErrorsForDuplicateReferences){
      this.AddReferenceToModule(options, module, name, aliases, errorNodes, alreadyReferencedModules, giveErrorsForDuplicateReferences, false);
    }
    protected virtual void AddReferenceToModule(CompilerOptions options, Module module, string name, string aliases, ErrorNodeList errorNodes, TrivialHashtable previousReferences,
      bool giveErrorsForDuplicateReferences, bool assemblyReference){
      this.AllocateAssemblyCacheIfNeeded();
      Error e = assemblyReference ? Error.NotAnAssembly : Error.NotAModule;
      string aname = name;
      string fname = System.IO.Path.GetFileName(aname);
      string ext = System.IO.Path.GetExtension(fname);
      bool providedDefaultExtension = false;
      if (assemblyReference){
        if (ext.ToLower(CultureInfo.InvariantCulture) != ".dll" && ext.ToLower(CultureInfo.InvariantCulture) != ".exe"){ 
          aname = aname+".dll";
          fname = fname+".dll";
          providedDefaultExtension = true;
        }
      }else{
        if (ext.ToLower(CultureInfo.InvariantCulture) != ".netmodule"){ 
          aname = aname+".netmodule";
          fname = fname+".netmodule";
          providedDefaultExtension = true;
        }
      }
      Module rModule = null;
      if (providedDefaultExtension)
        rModule = this.GetModuleFromReferencedCompilation(name);
      if (rModule == null)
        rModule = this.GetModuleFromReferencedCompilation(aname);
      if (rModule == null){
        e = Error.NoSuchFile;
        if (fname == aname){//Simple name, let compiler probe for it
          if (providedDefaultExtension) {
            aname = this.GetFullyQualifiedNameForReferencedLibrary(options, name);
            if (aname == null)
              aname = this.GetFullyQualifiedNameForReferencedLibrary(options, aname);
          }else
            aname = this.GetFullyQualifiedNameForReferencedLibrary(options, aname);
          if (aname == null) goto error;
        }else{
          if (!System.IO.File.Exists(aname)){
            if (!System.IO.File.Exists(name)) goto error;
            aname = name;
          }
        }
        rModule = Module.GetModule(aname, this.AssemblyCache, !options.MayLockFiles, options.LoadDebugSymbolsForReferencedAssemblies, true);
        if (rModule == null) goto error;
        e = assemblyReference ? Error.NotAnAssembly : Error.NotAModule;
      }
      AssemblyNode rAssem = rModule as AssemblyNode;
      if (rAssem != null){
        if (!assemblyReference) goto error;
      }else{
        if (assemblyReference) goto error;
      }
      Node prevRef = previousReferences[rModule.UniqueKey] as Node;
      AssemblyReference aref = prevRef as AssemblyReference;
      if (prevRef != null) {
        if (aref == null || aliases == null || aliases.Length == 0){
          if (!giveErrorsForDuplicateReferences) return;
          e = assemblyReference ? Error.DuplicateAssemblyReference : Error.DuplicateModuleReference;
          goto error;
        }
      }
      if (assemblyReference){
        if (aref == null) aref = new AssemblyReference(rAssem);
        if (aliases != null && aliases.Length > 0){
          if (aref.Aliases == null) aref.Aliases = new IdentifierList();
          string[] aliasArr = aliases.Split(',');
          foreach (string alias in aliasArr)
            if (alias != null && alias.Length > 0) aref.Aliases.Add(Identifier.For(alias));
        }
        module.AssemblyReferences.Add(aref);
        previousReferences[rModule.UniqueKey] = aref;
      }else{
        ModuleReference mref = new ModuleReference(name, rModule);
        module.ModuleReferences.Add(mref);
        previousReferences[rModule.UniqueKey] = mref;
      }
      return;
    error:
      errorNodes.Add(this.CreateErrorNode(e, name));
    }

    public virtual void AllocateAssemblyCacheIfNeeded(){
      if (this.AssemblyCache == null) 
        this.AssemblyCache = new Hashtable();
    }
    public Module GetModuleFromReferencedCompilation(string path){
      if (path == null){Debug.Assert(false); return null;}
      if (this.CurrentCompilation == null) return null;
      string dir = Path.GetDirectoryName(path); if (dir == null) dir = string.Empty;
      string name = Path.GetFileNameWithoutExtension(path); if (name == null) name = string.Empty;
      CompilationList referencedCompilations = this.CurrentCompilation.ReferencedCompilations;
      for (int i = 0, n = referencedCompilations == null ? 0 : referencedCompilations.Count; i < n; i++){
        Compilation rcomp = referencedCompilations[i];
        if (rcomp == null) continue;
        CompilerOptions options = rcomp.CompilerParameters as CompilerOptions;
        if (options == null){Debug.Assert(false); continue;}
        string odir = options.OutputPath; if (odir == null) odir = string.Empty;
        int len = odir.Length;
        if (len > 0 && odir[len-1] == Path.DirectorySeparatorChar) len--;
        if (string.Compare(odir, 0, dir, 0, len, true, System.Globalization.CultureInfo.InvariantCulture) != 0) continue;
        string oname = options.OutputAssembly; if (oname == null) oname = string.Empty;
        if (string.Compare(oname, name, true, System.Globalization.CultureInfo.InvariantCulture) != 0) continue;
        if (rcomp.TargetModule == null) this.ConstructSymbolTable(rcomp, new ErrorNodeList());
        return rcomp.TargetModule;
      }
      return null;
    }
    public void UpdateReferencedCompilations(Compilation compilation, Compilation originalReference, Compilation updatedReference){
      if (compilation == null || originalReference == null || updatedReference == null){Debug.Assert(false); return;}
      for (int i = 0, n = compilation.ReferencedCompilations == null ? 0 : compilation.ReferencedCompilations.Count; i < n; i++){
        if (compilation == compilation.ReferencedCompilations[i]){
          compilation.ReferencedCompilations[i] = updatedReference;
          break;
        }
      }
    }
    #endregion
    #region Helper methods that get things
    public virtual Scope GetGlobalScope(Module mod){
      return null;
    }
    public virtual Method GetMainMethod(TypeNode type, CompilerParameters options, CompilerResults results){
      MemberList members = type.Members;
      Method mainMethod = null;
      bool multipleMainMethods = false;
      for (int i = 0, n = members.Count; i < n; i++){
        Member mem = members[i];
        Method m = mem as Method;
        if (m == null){
          TypeNode nt = mem as TypeNode;
          if (nt == null) continue;
          m = this.GetMainMethod(nt, options, results);
          if (m == null) continue;
          if (type.IsGeneric || m.IsGeneric){
            this.HandleGenericMainMethodError(m, options, results);
            continue;
          }
          if (mainMethod != null){
            multipleMainMethods = true;
            this.HandleMultipleMainMethodError(mainMethod, options, results);
          }
          mainMethod = m;
          continue;
        }
        if (m.Name.UniqueIdKey != StandardIds.Main.UniqueIdKey || !m.IsStatic) continue;
        if (m.ReturnType != SystemTypes.Void && m.ReturnType != SystemTypes.Int32) goto warning;
        int np = m.Parameters == null ? 0 : m.Parameters.Count;
        if (np > 1) goto warning;
        if (np == 1){
          bool nonNullParameter;
          TypeNode t = m.Parameters[0].Type.StripOptionalModifiers(out nonNullParameter);
          ArrayType pt = t as ArrayType;
          if (pt == null) goto warning;
          if (pt.Rank != 1 || TypeNode.StripModifier(pt.ElementType, SystemTypes.NonNullType) != SystemTypes.String) goto warning;
          m.Parameters[0].Type = SystemTypes.String.GetArrayType(1); // set it just in case it was string![] or string![]!
          if (pt.ElementType != SystemTypes.String) {
            // then it was "non-null string" because of call to StripModifier above
            // so stick an attribute on so type can be reconstructed
            InstanceInitializer nnAECtor = SystemTypes.NotNullArrayElementsAttribute.GetConstructor();
            if (m.Parameters[0].Attributes == null)
              m.Parameters[0].Attributes = new AttributeList(1);
            m.Parameters[0].Attributes.Add(new AttributeNode(new MemberBinding(null, nnAECtor), null, AttributeTargets.Parameter));
          }
          if (nonNullParameter) {
            // stick an attribute on so type can be reconstructed
            InstanceInitializer nnCtor = SystemTypes.NotNullAttribute.GetConstructor();
            if (m.Parameters[0].Attributes == null)
              m.Parameters[0].Attributes = new AttributeList(1);
            m.Parameters[0].Attributes.Add(new AttributeNode(new MemberBinding(null, nnCtor), null, AttributeTargets.Parameter));
          }
        }
        if (type.IsGeneric || m.IsGeneric) {
          this.HandleGenericMainMethodError(m, options, results);
          continue;
        }
        if (mainMethod != null && !mainMethod.ParametersMatch(m.Parameters)) { //second condition avoid redundant message
          multipleMainMethods = true;
          this.HandleMultipleMainMethodError(mainMethod, options, results);
        }
        mainMethod = m;
        continue;
      warning:
        ErrorNodeList errorNodes = new ErrorNodeList(1);
        errorNodes.Add(this.CreateErrorNode(Error.InvalidMainMethodSignature, m));
        this.ProcessErrors(options, results, errorNodes);
      }
      if (multipleMainMethods)
        this.HandleMultipleMainMethodError(mainMethod, options, results);
      return mainMethod;
    }
    public virtual SourceContext GetSpanningContext(SourceChangeList changes, out int changedLength){
      changedLength = 0;
      int startPos = int.MaxValue;
      int endPos = int.MinValue;
      SourceContext result = new SourceContext();
      if (changes == null || changes.Count == 0) return result;
      result = changes[0].SourceContext;
      for (int i = 0, n = changes.Count; i < n; i++){
        SourceContext ctx = changes[i].SourceContext;
        if (ctx.StartPos < startPos) startPos = ctx.StartPos;
        if (ctx.EndPos > endPos) endPos = ctx.EndPos;          
        int originalLength = ctx.EndPos - ctx.StartPos;
        int newLength = changes[i].ChangedText == null ? 0 : changes[i].ChangedText.Length;
        changedLength += newLength - originalLength;
      }
      result.StartPos = startPos;
      result.EndPos = endPos;
      return result;
    }
    public virtual string GetTargetExtension(CompilerParameters options) {
      if (options == null || options.GenerateExecutable) return ".exe";
      CompilerOptions coptions = options as CompilerOptions;
      if (coptions == null || coptions.EmitManifest) return ".dll";
      return ".netmodule";
    }
    public virtual string GetTargetFileName(CompilerParameters options, ErrorNodeList errorNodes){
      if (options == null){Debug.Assert(false); return null;}
      CompilerOptions coptions = options as CompilerOptions;
      string fileName = options.OutputAssembly; //TODO: Path will throw exceptions if fileName contains invalid characters (which can happen, since this is user input).
      if (coptions != null && coptions.OutputPath != null)
        fileName = PathWrapper.Combine(coptions.OutputPath, fileName);
      string ext = this.GetTargetExtension(options);
      string fExt = Path.GetExtension(fileName);
      if (coptions.ExplicitOutputExtension == null && (fExt == null || fExt == string.Empty || 
      string.Compare(ext, fExt, true, System.Globalization.CultureInfo.InvariantCulture) != 0))
        fileName = fileName + ext;
      Error err = Error.InvalidOutputFile;
      if (fileName != null && fileName.Length > 0){
        try{
          string fname = Path.GetFullPath(fileName);
          if (File.Exists(fname))
            File.Delete(fname);
          else if (!Directory.Exists(Path.GetDirectoryName(fname)))
            File.Delete(fname); //Force an error. Use the exception to get a localized message.
          if (options.IncludeDebugInformation){
            err = Error.InvalidDebugInformationFile;
            fileName = Path.ChangeExtension(fileName, "pdb");
            string pdbName = Path.GetFullPath(fileName);
            if (File.Exists(pdbName))
              File.Delete(pdbName);
          }
          return fname;
        }catch(ArgumentException e){
          if (errorNodes == null) errorNodes = new ErrorNodeList();
          errorNodes.Add(this.CreateErrorNode(err, fileName, e.Message));
        }catch(System.IO.IOException e){
          if (errorNodes == null) errorNodes = new ErrorNodeList();
          errorNodes.Add(this.CreateErrorNode(err, fileName, e.Message));
        }catch(NotSupportedException e){
          if (errorNodes == null) errorNodes = new ErrorNodeList();
          errorNodes.Add(this.CreateErrorNode(err, fileName, e.Message));
        }catch(System.Security.SecurityException e){
          if (errorNodes == null) errorNodes = new ErrorNodeList();
          errorNodes.Add(this.CreateErrorNode(err, fileName, e.Message));
        }catch(UnauthorizedAccessException e){
          if (errorNodes == null) errorNodes = new ErrorNodeList();
          errorNodes.Add(this.CreateErrorNode(err, fileName, e.Message));
        }
      }
      TempFileCollection tempFiles = options.TempFiles;
      if (tempFiles != null)
        fileName = tempFiles.AddExtension(this.GetTargetExtension(options).Substring(1));
      else
        fileName = System.IO.Path.GetTempFileName()+this.GetTargetExtension(options);
      if (coptions != null)
        coptions.OutputPath = Path.GetDirectoryName(fileName);
      options.OutputAssembly = Path.GetFileNameWithoutExtension(fileName);
      return fileName;
    }
    public virtual string ReadSourceText(string fileName, CompilerParameters options, ErrorNodeList errorNodes){
      CompilerOptions coptions = options as CompilerOptions;
      try{
        using (System.IO.FileStream inputStream = new System.IO.FileStream(fileName, 
                 System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read)){
          // get the file size
          long size = inputStream.Seek(0, System.IO.SeekOrigin.End);
          if (size > int.MaxValue){
            errorNodes.Add(this.CreateErrorNode(Error.SourceFileTooLarge, fileName));
            return "";
          }
          inputStream.Seek(0, System.IO.SeekOrigin.Begin);
          int b1 = inputStream.ReadByte();
          int b2 = inputStream.ReadByte();
          if (b1 == 'M' && b2 == 'Z'){
            errorNodes.Add(this.CreateErrorNode(Error.IsBinaryFile, System.IO.Path.GetFullPath(fileName)));
            return "";
          }
                
          inputStream.Seek(0, System.IO.SeekOrigin.Begin);
          Encoding encoding = Encoding.Default;
          if (coptions != null && coptions.CodePage != null){
            try{
              encoding = Encoding.GetEncoding((int)coptions.CodePage);
            }catch(System.ArgumentException){
              errorNodes.Add(this.CreateErrorNode(Error.InvalidCodePage, coptions.CodePage.ToString()));
              return "";
            }
          }
          System.IO.StreamReader reader = new System.IO.StreamReader(inputStream, encoding, true); //last param allows markers to override encoding

          //Read the contents of the file into an array of char and return as a string
          char[] sourceText = new char[(int)size];
          int length = reader.Read(sourceText, 0, (int)size);
          return new String(sourceText, 0, length);
        }
      }catch(Exception e){
        errorNodes.Add(this.CreateErrorNode(Error.SourceFileNotRead, fileName, e.Message));
        return "";
      }
    }
    #endregion
    #region Helper methods that set things
    public virtual void AddResourcesAndIcons(Module module, CompilerParameters options, ErrorNodeList errors){
      if (options == null) return;
      CompilerOptions coptions = options as CompilerOptions;
      string win32ResourceFilePath = options.Win32Resource;
      if (win32ResourceFilePath != null && win32ResourceFilePath.Length > 0){
        if (!File.Exists(win32ResourceFilePath) && options.OutputAssembly != null)
          win32ResourceFilePath = PathWrapper.Combine(Path.GetDirectoryName(options.OutputAssembly), win32ResourceFilePath);
        try{
          module.AddWin32ResourceFile(win32ResourceFilePath);
        }catch(OutOfMemoryException){
          errors.Add(this.CreateErrorNode(Error.InvalidWin32ResourceFileContent, win32ResourceFilePath));
        }catch(NullReferenceException){
          errors.Add(this.CreateErrorNode(Error.InvalidWin32ResourceFileContent, win32ResourceFilePath));
        }catch(Exception e){
          errors.Add(this.CreateErrorNode(Error.Win32ResourceFileNotRead, win32ResourceFilePath, e.Message));
        }
      }
      if (coptions != null && coptions.Win32Icon != null && coptions.Win32Icon.Length > 0){
        string win32iconFilePath = coptions.Win32Icon;
        if (!File.Exists(win32iconFilePath) && options.OutputAssembly != null)
          win32iconFilePath = PathWrapper.Combine(Path.GetDirectoryName(options.OutputAssembly), win32iconFilePath);
        try{
          module.AddWin32Icon(win32iconFilePath);
        }catch(OutOfMemoryException){
          errors.Add(this.CreateErrorNode(Error.AutoWin32ResGenFailed,
            this.CreateErrorNode(Error.Win32IconFileNotRead, win32iconFilePath,
            this.CreateErrorNode(Error.InvalidData).GetMessage()).GetMessage()));
        }catch(NullReferenceException){
          errors.Add(this.CreateErrorNode(Error.AutoWin32ResGenFailed,
            this.CreateErrorNode(Error.Win32IconFileNotRead, win32iconFilePath,
            this.CreateErrorNode(Error.InvalidData).GetMessage()).GetMessage()));
        }catch(Exception e){
          errors.Add(this.CreateErrorNode(Error.AutoWin32ResGenFailed, 
            this.CreateErrorNode(Error.Win32IconFileNotRead, win32iconFilePath, e.Message).GetMessage()));
        }
      }
      if (coptions != null && (win32ResourceFilePath == null || win32ResourceFilePath.Length == 0))
        module.AddWin32VersionInfo(coptions);
      if (coptions != null && coptions.EmbeddedResources != null && coptions.EmbeddedResources.Count > 0){
        if (module.Resources == null) module.Resources = new ResourceList();
        for (int i = 0, n = coptions.EmbeddedResources.Count; i < n; i++){
          string resource = coptions.EmbeddedResources[i];
          if (resource == null) continue;
          string resourceFileName = resource;
          string resourceName = Path.GetFileName(resource);
          int firstComma = resource.IndexOf(',');
          if (firstComma > 0){
            resourceFileName = resource.Substring(0, firstComma);
            resourceName = resource.Substring(firstComma+1);
          }else if (coptions.RootNamespace != null && coptions.RootNamespace.Length > 0){
            resourceName = coptions.RootNamespace + "." + resourceName;
          }
          byte[] resourceContents = null;
          resourceFileName = PathWrapper.Combine(Path.GetDirectoryName(options.OutputAssembly), resourceFileName);
          try{
            using (System.IO.FileStream resStream = File.OpenRead(resourceFileName)){
              long size = resStream.Length;
              if (size > int.MaxValue) continue; //TODO: error message
              int len = (int)size;
              resourceContents = new byte[len];
              resStream.Read(resourceContents, 0, len);
            }
          }catch(Exception e){
            errors.Add(this.CreateErrorNode(Error.CannotReadResource, Path.GetFileName(resourceFileName), e.Message));
          }
          Resource res = new Resource();
          res.Name = resourceName;
          res.IsPublic = true; //TODO: get this value from the resource string
          res.DefiningModule = module;
          res.Data = resourceContents;
          module.Resources.Add(res);
        }
      }
      if (coptions != null && coptions.LinkedResources != null && coptions.LinkedResources.Count > 0){
        if (module.Resources == null) module.Resources = new ResourceList();
        for (int i = 0, n = coptions.LinkedResources.Count; i < n; i++){
          string resource = coptions.LinkedResources[i];
          if (resource == null) continue;
          string resourceFileName = resource;
          string resourceName = resource;
          int firstComma = resource.IndexOf(',');
          if (firstComma > 0){
            resourceFileName = resource.Substring(0, firstComma);
            resourceName = resource.Substring(firstComma+1);
          }
          Resource res = new Resource();
          res.Name = resourceName;
          res.IsPublic = true;
          res.DefiningModule = new Module();
          res.DefiningModule.Kind = ModuleKindFlags.ManifestResourceFile;
          res.DefiningModule.Name = resourceFileName;
          res.DefiningModule.Location = PathWrapper.Combine(Path.GetDirectoryName(options.OutputAssembly), resourceFileName); ;
          res.Data = null;
          module.Resources.Add(res);
        }
      }
    }
    public void SetEntryPoint(Compilation compilation, CompilerResults results){
      Method mainMethod = null;
      string mainClassOrFileName = null;
      if (compilation.CompilerParameters.MainClass != null && compilation.CompilerParameters.MainClass.Length > 0){
        mainClassOrFileName = compilation.CompilerParameters.MainClass;
        int nsSeparatorPos = mainClassOrFileName.LastIndexOf('.');
        Identifier ns = Identifier.For(mainClassOrFileName.Substring(0, nsSeparatorPos == -1 ? 0 : nsSeparatorPos));
        Identifier id = Identifier.For(mainClassOrFileName.Substring(nsSeparatorPos+1));
        TypeNode mainClass = compilation.TargetModule.GetType(ns, id);
        if (mainClass != null)
          mainMethod = this.GetMainMethod(mainClass, compilation.CompilerParameters, results);
      }else{
        TypeNodeList types = compilation.TargetModule.Types;
        bool firstTime = true;
        for (int i = 0, n = types.Count; i < n; i++){
          TypeNode t = types[i];
          if (t == null) continue;
          Method m = this.GetMainMethod(t, compilation.CompilerParameters, results);
          if (m != null){
            if (compilation.CompilerParameters.OutputAssembly == null || compilation.CompilerParameters.OutputAssembly.Length == 0){
              Debug.Assert(compilation.CompilerParameters.GenerateExecutable);
              if (m.SourceContext.Document != null)
                this.SetOutputFileName(compilation.CompilerParameters, m.SourceContext.Document.Name);
            }
            if (mainMethod == null)
              mainMethod = m;
            else if (compilation.CompilerParameters.GenerateExecutable){
              if (firstTime){
                this.HandleMultipleMainMethodError(mainMethod, compilation.CompilerParameters, results);
                firstTime = false;
              }
              this.HandleMultipleMainMethodError(m, compilation.CompilerParameters, results);
            }
          }
        }
      }
      if (!compilation.CompilerParameters.GenerateExecutable) return;
      if (mainMethod == null){
        ErrorNodeList errorNodes = new ErrorNodeList(1);
        if (mainClassOrFileName == null) mainClassOrFileName = compilation.CompilerParameters.OutputAssembly;
        if (mainClassOrFileName == null){
          for (int i = 0, n = compilation.CompilationUnits == null ? 0 : compilation.CompilationUnits.Count; i < n; i++){
            CompilationUnit cu = compilation.CompilationUnits[i];
            if (cu == null || cu.Name == null) continue;
            this.SetOutputFileName(compilation.CompilerParameters, cu.Name.ToString());
            break;
          }
          mainClassOrFileName = compilation.CompilerParameters.OutputAssembly;
        }
        errorNodes.Add(this.CreateErrorNode(Error.NoMainMethod, mainClassOrFileName == null ? "" : mainClassOrFileName));
        this.ProcessErrors(compilation.CompilerParameters, results, errorNodes);
      }
      compilation.TargetModule.EntryPoint = mainMethod;
      if (mainMethod != null && mainMethod.SourceContext.Document != null &&
        (compilation.CompilerParameters.OutputAssembly == null || compilation.CompilerParameters.OutputAssembly.Length == 0)){
        this.SetOutputFileName(compilation.CompilerParameters, mainMethod.SourceContext.Document.Name);
      }
    }
    public virtual void SetOutputFileName(CompilerParameters options, string fileName){
      //TODO: since path insists on throwing exceptions for what it thinks are invalid path characters
      //there is a need for Path wrappers that will tolerate such paths

      if (options == null){Debug.Assert(false); return;}
      CompilerOptions coptions = options as CompilerOptions;
      if (options.OutputAssembly != null && options.OutputAssembly.Length > 0){
        //Strip extension if present and make sure OutputPath is set
        string path = Path.GetDirectoryName(options.OutputAssembly);
        string ext = Path.GetExtension(options.OutputAssembly);
        if (string.Compare(ext, this.GetTargetExtension(options), true, System.Globalization.CultureInfo.InvariantCulture) == 0)
          options.OutputAssembly = Path.GetFileNameWithoutExtension(options.OutputAssembly);
        if (coptions == null)
          options.OutputAssembly = PathWrapper.Combine(path, options.OutputAssembly);
        else{
          if (coptions.OutputPath == null || coptions.OutputPath.Length == 0)
            coptions.OutputPath = path;
          else if (path != null && path.Length > 0)
            coptions.OutputPath = PathWrapper.Combine(coptions.OutputPath, path);
        }
        return;
      }
      if (options.TempFiles != null && (options.TempFiles.TempDir != Directory.GetCurrentDirectory() || !options.TempFiles.KeepFiles)){
        //Get here when invoked from typical CodeDom client, such as ASP .Net
        fileName = options.TempFiles.AddExtension(this.GetTargetExtension(options).Substring(1));
      }else{
        //Get here when invoked from a command line compiler, or a badly behaved CodeDom client
        if (fileName == null) fileName = "noname";
        string ext = Path.GetExtension(fileName);
        if (string.Compare(ext, this.GetTargetExtension(options), true, System.Globalization.CultureInfo.InvariantCulture) != 0)
          fileName = Path.GetFileNameWithoutExtension(fileName) + this.GetTargetExtension(options);
      }
      options.OutputAssembly = Path.GetFileNameWithoutExtension(fileName);
      if (coptions != null)
        coptions.OutputPath = Path.GetDirectoryName(fileName);
    }
    #endregion
    #region Helper methods that save or load
    public virtual void SaveOrLoadAssembly(AssemblyNode assem, CompilerParameters options, CompilerResults results, ErrorNodeList errorNodes){
      if (assem == null) return;
      AssemblyReferenceList arefs = assem.AssemblyReferences;
      //TODO: give the error in the context of the member that made the reference
      for (int i = 0, n = arefs == null ? 0 : arefs.Count; i < n; i++){
        AssemblyReference aref = arefs[i];
        if (aref == null || aref.Assembly == null) continue;
        ArrayList metadataErrors = aref.Assembly.MetadataImportErrors;
        if (metadataErrors == null) continue;
        foreach (Exception mdErr in metadataErrors)
          if (mdErr.Message.StartsWith("Assembly reference not resolved"))
            results.Errors.Add(new CompilerError(aref.Assembly.Name+".dll", 0, 0, "0", mdErr.Message));
      }
      if (results.NativeCompilerReturnValue != 0) return; //TODO: allow option to override this
      if (options.GenerateInMemory){
        System.Security.Policy.Evidence evidence = options.Evidence;
        CompilerOptions cOptions = options as CompilerOptions;
        System.AppDomain targetDomain = cOptions == null ? null : cOptions.TargetAppDomain;
        if (targetDomain == null) targetDomain = AppDomain.CurrentDomain;
        for (int i = 0, n = arefs == null ? 0 : arefs.Count; i < n; i++){
          AssemblyReference aref = arefs[i];
          if (aref == null || aref.Assembly == null) continue;
          aref.Assembly.GetRuntimeAssembly(evidence, targetDomain);
        }
        results.CompiledAssembly = assem.GetRuntimeAssembly(evidence, targetDomain);
      }else{
        ErrorNodeList errors = new ErrorNodeList(0);
        string fileName = this.GetTargetFileName(options, errors);
        this.AddResourcesAndIcons(assem, options, errors);
        if (errors.Count == 0){
          try{
            assem.WriteModule(fileName, options);
          }catch(KeyFileNotFoundException){
            ErrorNode keyFileMissing = this.CreateErrorNode(Error.AssemblyKeyFileMissing, ((CompilerOptions)options).AssemblyKeyFile);
            errors.Add(this.CreateErrorNode(Error.AssemblyCouldNotBeSigned, assem.Location, keyFileMissing.GetMessage()));
            errorNodes.Add(errors[0]);
            this.ProcessErrors(options, results, errors);
          }catch(AssemblyCouldNotBeSignedException){
            ErrorNode unknownCryptoFailure = this.CreateErrorNode(Error.UnknownCryptoFailure);
            errors.Add(this.CreateErrorNode(Error.AssemblyCouldNotBeSigned, assem.Location, unknownCryptoFailure.GetMessage()));
            errorNodes.Add(errors[0]);
            this.ProcessErrors(options, results, errors);
          }catch(Exception e){
            errors.Add(this.CreateErrorNode(Error.InternalCompilerError, e.Message));
            errorNodes.Add(errors[0]);
            this.ProcessErrors(options, results, errors);
          }
          CompilerOptions coptions = options as CompilerOptions;
          if (coptions != null && coptions.XMLDocFileName != null && coptions.XMLDocFileName.Length > 0)
            assem.WriteDocumentation(new StreamWriter(coptions.XMLDocFileName));
          results.PathToAssembly = fileName;
        }else{
          this.ProcessErrors(options, results, errors);
          for (int i = 0, n = errors.Count; i < n; i++)
            errorNodes.Add(errors[i]);
        }
      }
    }
    /// <summary>
    /// Provides a hook to save things other than just the module.
    /// </summary>
    protected virtual void SaveCompilation(Compilation compilation, Module module, CompilerParameters options, CompilerResults results){
      this.SaveModule(module, options, results);
    }
    protected virtual void SaveCompilation(Compilation compilation, AssemblyNode assem, CompilerParameters options, CompilerResults results, ErrorNodeList errorNodes){
      this.SaveOrLoadAssembly(assem, options, results, errorNodes);
    }
    public virtual void SaveModule(Module module, CompilerParameters options, CompilerResults results){
      if (module == null || options == null || results == null){Debug.Assert(false); return;}
      AssemblyReferenceList arefs = module.AssemblyReferences;
      //TODO: give the error in the context of the member that made the reference
      for (int i = 0, n = arefs == null ? 0 : arefs.Count; i < n; i++){
        AssemblyReference aref = arefs[i];
        if (aref == null || aref.Assembly == null) continue;
        ArrayList metadataErrors = aref.Assembly.MetadataImportErrors;
        if (metadataErrors == null) continue;
        foreach (Exception mdErr in metadataErrors)
          if (mdErr.Message.StartsWith("Assembly reference not resolved"))
            results.Errors.Add(new CompilerError(aref.Assembly.Name+".dll", 0, 0, "0", mdErr.Message));
      }
      if (results.NativeCompilerReturnValue != 0) return; //TODO: allow option to override this
      ErrorNodeList errors = new ErrorNodeList(0);
      string fileName = this.GetTargetFileName(options, errors);
      this.AddResourcesAndIcons(module, options, errors);
      if (errors.Count == 0){
        module.WriteModule(fileName, options);
        CompilerOptions coptions = options as CompilerOptions;
        if (coptions != null && coptions.XMLDocFileName != null && coptions.XMLDocFileName.Length > 0)
          module.WriteDocumentation(new StreamWriter(coptions.XMLDocFileName));
        results.PathToAssembly = fileName;
      }else{
        this.ProcessErrors(options, results, errors);
      }
    }
    #endregion
    #region Helper methods that do the actual compilation
    public virtual DocumentText CreateDocumentText(string fileName, CompilerResults results, CompilerParameters options, ErrorNodeList errorNodes, bool canUseMemoryMap){
#if !ROTOR
      if (canUseMemoryMap){
        int applicableCodePage = System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ANSICodePage;
        CompilerOptions coptions = options as CompilerOptions;
        if (coptions != null && coptions.CodePage != null) applicableCodePage = (int)coptions.CodePage;
        int asciiCodePage = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ANSICodePage;
        if (applicableCodePage == asciiCodePage){
          //If there is no unicode signature at the start of the file, it seems reasonably safe to assume that 1 byte == 1 char
          //In that case we can bypass the overhead of BCL file classes and use a memory mapped file instead.
          unsafe{
            try{
              MemoryMappedFile mmFile = new MemoryMappedFile(fileName);
              try{
                byte b0 = *mmFile.Buffer;
                if (b0 == 'M' && *(mmFile.Buffer+1) == 'Z'){
                  //This is a binary file. Give an appropriate error.
                  errorNodes.Add(this.CreateErrorNode(Error.IsBinaryFile, System.IO.Path.GetFullPath(fileName)));
                  this.ProcessErrors(options, results, errorNodes);
                  mmFile.Dispose();
                  return null;
                }else if (b0 != 0xff && b0 != 0xfe && b0 != 0xef){
                  // No unicode signature, it seems. Go ahead and compile using the memory mapped file.
                  return new DocumentText(mmFile);
                } 
              }catch(Exception e){
                errorNodes.Add(this.CreateErrorNode(Error.InternalCompilerError, e.Message));
                this.ProcessErrors(options, results, errorNodes);
                return new DocumentText("");
              }
            }catch{}
          }
        }
      }
#endif
      return new DocumentText(this.ReadSourceText(fileName, options, errorNodes));
    }
    /// <summary>
    /// Translates the given parse tree into a normalized form that is suitable for writing out as CLI IL.
    /// This translation process is normally accomplished
    /// by a series of visitors that are language specific derivations of base class visitors provided by
    /// the Cci code generation framework. The base Compiler class does not call the visitors directly, in
    /// order to provide language implementations with the opportunity to add or replace visitors.
    /// </summary>
    /// <param name="compilation">An IR tree that represents the parse tree for the entire compilation.</param>
    /// <param name="errorNodes">Errors encountered during the compilation are appended to this list.</param>
    public virtual void CompileParseTree(Compilation compilation, ErrorNodeList errorNodes){
      Debug.Assert(false); //Subclasses either have to override this, or ensure that their client will never call it.
    }
    /// <summary>
    /// Translates the given parse tree node into a corresponding normalized node.
    /// If the node is a type node or contains type nodes, the normalized versions of the type nodes
    /// are added to the target module. Expected to be mainly useful for compiling expressions. 
    /// </summary>
    /// <param name="node">An IR tree that represents a parse tree</param>
    /// <param name="scope">A symbol table for resolving free variables in the parse tree</param>
    /// <param name="targetModule">A module to which types found in the IR tree are added</param>
    /// <param name="errorNodes">Errors encountered during the compilation should be appended to this list</param>
    public virtual Node CompileParseTree(Node node, Scope scope, Module targetModule, ErrorNodeList errorNodes){
      Debug.Assert(false); //Subclasses either have to override this, or ensure that their client will never call it.
      return null;
    }
    #endregion
    #region Symbol table update methods
    public virtual Compilation IncrementalSymbolTableUpdate(Compilation compilation, CompilationUnitSnippet compilationUnit, Document changedDocument, 
      Method method, SourceContext spanningContextForChanges, int changeInLength){
      if (compilation == null || compilationUnit == null || changedDocument == null || method == null || method.Body == null){Debug.Assert(false); return null;}
      //Record the fact that method has changed, so that subsequent edits do not have to search for it 
      //in an AST that now has source contexts with out of date positions (the line numbers are up to date, but not the positions).
      compilationUnit.ChangedMethod = method;
      compilationUnit.OriginalEndPosOfChangedMethod = method.SourceContext.EndPos;
      if (method.SourceContext.Document != null){
        //Contruct a new context for the method that refers to the new document
        SourceContext newCtx = this.GetMethodBodyContextInNewDocument(changedDocument, method.Body.SourceContext, spanningContextForChanges, changeInLength);
        //Now update the original document so that all nodes that follow method in the source will report their source lines relative to the changedDocument
        int oldNumLines = method.Body.SourceContext.EndLine - method.Body.SourceContext.StartLine;
        int newNumLines = newCtx.EndLine - newCtx.StartLine;
        method.SourceContext.Document.InsertOrDeleteLines(method.SourceContext.EndPos, newNumLines-oldNumLines);
        //Replace the method body context with the new context
        method.Body.SourceContext = newCtx;
      }
      //Get rid of the body statements (if present) that was constructed from the old context
      method.Body.Statements = null;
      return compilation;
    }
    //This does not always produce the same context as would be produced by the parser had it parsed the entire changed document
    //since the edit could introduce an open multi-line comment or an open brace. This may be a feature rather than a mistake,
    //since it is not the common case for this erasing of subsequent members to be intentional. When the new method is asked for its body,
    //it will produce whatever will be produced by parsing the changed document. However, any "erased" members will still show up
    //until an edit occurs that causes a full symbol table update. For now, my guess is that this is acceptable.
    public virtual SourceContext GetMethodBodyContextInNewDocument(Document changedDocument, SourceContext methodBodyContext, 
      SourceContext spanningContextForChanges, int changeInLength){
      //get the lengths of the prefix and suffix that expand the spanning context into the full source context of the spanning method
      int originalLength = spanningContextForChanges.EndPos - spanningContextForChanges.StartPos;
      int prefixLength = spanningContextForChanges.StartPos - methodBodyContext.StartPos;
      if (prefixLength < 0){Debug.Assert(false); return methodBodyContext;}
      int suffixLength = methodBodyContext.EndPos - spanningContextForChanges.EndPos;
      if (suffixLength < 0){Debug.Assert(false); return methodBodyContext;}
      SourceContext newContext = new SourceContext();
      newContext.Document = changedDocument;
      newContext.StartPos = methodBodyContext.StartPos;
      newContext.EndPos = methodBodyContext.StartPos+prefixLength+originalLength+changeInLength+suffixLength;
      return newContext;
    }
    public virtual Compilation FullSymbolTableUpdate(Compilation compilation, Document originalDocument, Document changedDocument, ErrorNodeList errors){
      if (compilation == null || compilation.CompilationUnits == null || (originalDocument == null && changedDocument != null)){
        Debug.Assert(false); return null;
      }
      Compilation result = this.CreateCompilation(null, new CompilationUnitList(compilation.CompilationUnits.Count), 
        compilation.CompilerParameters, null);
      result.ReferencedCompilations = compilation.ReferencedCompilations;
      for (int i = 0, n = compilation.CompilationUnits == null ? 0 : compilation.CompilationUnits.Count; i < n; i++){
        CompilationUnitSnippet cu = compilation.CompilationUnits[i] as CompilationUnitSnippet;
        if (cu == null){Debug.Assert(false); continue;}
        cu = (CompilationUnitSnippet)cu.Clone();
        cu.Compilation = result;
        cu.Nodes = null;
        if (cu.SourceContext.Document == originalDocument)
          cu.SourceContext.Document = changedDocument;
        result.CompilationUnits.Add(cu);
      }
      this.ConstructSymbolTable(result, errors);
      if (this.OnSymbolTableUpdate != null){
        Comparer comparer = this.CreateComparer();
        comparer.DoNotCompareBodies = true;
        UpdateSpecification updateSpecification = comparer.Visit(compilation, result);
        if (updateSpecification == null){Debug.Assert(false); return null;}
        if (comparer.MembersThatHaveChanged != null && comparer.MembersThatHaveChanged.Count > 0)
          this.FireOnSymbolTableUpdate(result, updateSpecification, comparer.MembersThatHaveChanged);
      }
      return result;
    }
    #endregion
    #endregion
  }
  public class ResgenFactory : IParserFactory{
    private string pathToResgen;
    public ResgenFactory(string pathToResgen){
      Debug.Assert(File.Exists(pathToResgen));
      this.pathToResgen = pathToResgen;
    }
    public IParser CreateParser(string fileName, int lineNumber, DocumentText text, Module symbolTable, ErrorNodeList errorNodes, CompilerParameters options){
      return new ResgenCompilerStub(errorNodes, options as CompilerOptions, this.pathToResgen);
    }
  }
  public class ResgenCompilerStub : IParser{
    private ErrorNodeList errorNodes;
    private CompilerOptions options;
    private string pathToResgen;

    public ResgenCompilerStub(ErrorNodeList errorNodes, CompilerOptions options, string pathToResgen){
      this.errorNodes = errorNodes;
      this.options = options;
      this.pathToResgen = pathToResgen;
    }
    public void ParseStatements(StatementList statements){
      Debug.Assert(false);
    }
    public void ParseCompilationUnit(CompilationUnit compilationUnit){
      if (compilationUnit == null){Debug.Assert(false); return;}
      if (this.options == null || this.options.EmbeddedResources == null) return;
      Document doc = compilationUnit.SourceContext.Document;
      if (doc == null){Debug.Assert(false); return;}
      string sourcePath = doc.Name;
      if (sourcePath == null){Debug.Assert(false); return;}
      if (!File.Exists(sourcePath)) return; //TODO: add error
      string resourceName = Path.GetFileNameWithoutExtension(sourcePath) + ".resources";
      string targetPath = null;
      foreach (string resource in this.options.EmbeddedResources){
        if (resource == null){Debug.Assert(false); continue;}
        int commaPos = resource.IndexOf(',');
        targetPath = commaPos < 0 ? resource : resource.Substring(0, commaPos);
        if (!targetPath.EndsWith(resourceName)) continue;
        if (File.Exists(targetPath) && File.GetLastWriteTime(targetPath) > File.GetLastWriteTime(sourcePath)) return;
        break;
      }
      if (targetPath == null){Debug.Assert(false); return;}
      try{
        ProcessStartInfo pinfo = new ProcessStartInfo(this.pathToResgen, "\""+sourcePath+"\" \""+targetPath+"\"");
        pinfo.CreateNoWindow = true;
        pinfo.UseShellExecute = false;
        pinfo.RedirectStandardOutput = true;
        pinfo.RedirectStandardError = true;
        Process resgenProcess = Process.Start(pinfo);
        string output = resgenProcess.StandardOutput.ReadToEnd();
        string err = resgenProcess.StandardError.ReadToEnd();
        if (resgenProcess != null)
          resgenProcess.WaitForExit();
        if (resgenProcess.ExitCode > 0){
          //TODO: add an error
        }
      }catch{
        //TODO: need an error handler
      }
    }
    public void ParseTypeMembers(TypeNode type){
      Debug.Assert(false);
    }
    public Expression ParseExpression(){
      Debug.Assert(false);
      return null;
    }
  }
}
  
