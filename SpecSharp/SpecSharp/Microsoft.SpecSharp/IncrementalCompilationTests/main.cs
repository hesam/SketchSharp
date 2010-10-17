//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.IO;
using System.Text;
using System.Threading;

#if CCINamespace
using Microsoft.Cci;
#else
using System.Compiler;
#endif

public class Test{
  StreamReader reader;
  string inputLine;
  string testName;
  int lineCounter = 0;
  int failures = 0;
  Compilation compilation;
  CompilationList compilations;
  CompilationUnit compilationUnit;
  Compiler compiler;
  ErrorNodeList errors;
  StringBuilder output;
  TextWriter savedConsoleOut;
  System.Diagnostics.TextWriterTraceListener traceListener;
  Thread currentThread;
  Document currentDocument;
  
  static int Main(string[] args){
    if (args == null || args.Length == 0){
      Console.WriteLine("You must specify the name of a test file on the command line.");
      return 1;
    }
    string pathToTestFile = args[0];
    if (!File.Exists(pathToTestFile)){
      Console.WriteLine(pathToTestFile+" does not exist.");
      return 1;
    }
    Test t = new Test();
    t.currentThread = Thread.CurrentThread;
    System.Diagnostics.Debug.Listeners.Remove("Default");
    t.savedConsoleOut = Console.Out;
    Console.SetOut(new StringWriter(t.output = new StringBuilder()));
    t.traceListener = new System.Diagnostics.TextWriterTraceListener(System.Console.Out);
    System.Diagnostics.Debug.Listeners.Add(t.traceListener);
    try{
      t.reader = File.OpenText(pathToTestFile);
      t.ReadNextLine();
      t.compiler = new Microsoft.SpecSharp.Compiler();
      t.compiler.OnSymbolTableUpdate += new Compiler.SymbolTableUpdateEventHandler(t.SymbolTableUpdateEventHandler);
      do{
        t.errors = new ErrorNodeList();
        t.ConstructCompilations();
        t.PerformIncrementalUpdates();
      }while (t.inputLine != null);
    }catch(Exception e){
      Console.SetOut(t.savedConsoleOut);
      Console.WriteLine(e.Message);
      return 2;
    }
    Console.SetOut(t.savedConsoleOut);
    if (t.failures == 0){
      Console.WriteLine(pathToTestFile+" passed");
      return 0;
    }else{
      Console.WriteLine(pathToTestFile+" had {0} failure(s)", t.failures);
      return 1;
    }
  }
  void SymbolTableUpdateEventHandler(Compilation updatedSymbolTable, UpdateSpecification updateSpecification, MemberList changedMembers){
    lock(this){
      Thread savedCurrentThread = this.currentThread;
      if (Thread.CurrentThread == this.currentThread) Console.WriteLine("Update event called on same thread as the one causing the update");
      if (!Thread.CurrentThread.IsThreadPoolThread) Console.WriteLine("Updated event called from a non thread pool thread");
      if (!Thread.CurrentThread.IsBackground) Console.WriteLine("Updated event called from a non background thread");
      this.currentThread = Thread.CurrentThread;
      if (updatedSymbolTable == null){Console.WriteLine("SymbolTable update with null value for updatedSymbolTable"); return;}
      if (updatedSymbolTable.TargetModule == null){Console.WriteLine("SymbolTable update with null value for updatedSymbolTable.TargetModule"); return;}
      Console.WriteLine("Received update event on symbol table: {0}", ((Compilation)updateSpecification.Original).TargetModule.Name);
      for (int i = 0, n = changedMembers == null ? 0 : changedMembers.Count; i < n; i++){
        Member mem = changedMembers[i];
        if (mem == null)
          Console.WriteLine("changedMembers[{0}] == null", i);
        else
          Console.WriteLine("changedMembers[{0}].FullName == {1}", i, mem.FullName);
      }
      for (int i = 0, n = this.compilations.Count; i < n; i++){
        Compilation compilation = this.compilations[i];
        if (compilation == null || compilation == updateSpecification.Original) continue;
        for (int j = 0, m = compilation.ReferencedCompilations == null ? 0 : compilation.ReferencedCompilations.Count; j < m; j++){
          Compilation rComp = compilation.ReferencedCompilations[j];
          if (rComp != updateSpecification.Original) continue;
          Compilation upd = this.compiler.UpdateSymbolTable(compilation, (Compilation)updateSpecification.Original, updatedSymbolTable, changedMembers, this.errors);
          if (upd == null){
            Console.WriteLine("Referenced compilation {0} was not updated", j);
          }else
            this.CheckUpdatedCompilation(compilation, upd);
        }
      }
      this.currentThread = savedCurrentThread;
    }
  }
  void WriteOutAnyErrors(){
    for (int i = 0, n = this.errors == null ? 0 : this.errors.Count; i < n; i++){
      ErrorNode e = this.errors[i];
      if (e != null)
        Console.WriteLine(e.GetMessage());
    }
    this.errors = new ErrorNodeList();
  }
  void ConstructCompilations(){
    this.compilation = null;
    this.compilations = new CompilationList();
    while (this.inputLine != null && this.inputLine.StartsWith("compilation ")){
      this.ConstructCompilation();
    }
    if (this.compilation == null)
      throw new MalformedSuiteException("Line "+this.lineCounter+": Expected a line of the form 'compilation name'");
    Compilation previous = null;
    for (int i = this.compilations.Count-1; i >= 0; i--){
      Compilation comp = this.compilations[i];
      if (previous != null){
        comp.ReferencedCompilations = new CompilationList(previous);
        comp.CompilerParameters.ReferencedAssemblies.Add(previous.CompilerParameters.OutputAssembly+".dll");
      }
      this.compiler.ConstructSymbolTable(comp, this.errors);
      previous = comp;
    }
  }
  void ConstructCompilation(){
    this.testName = this.inputLine.Substring(12);
    this.compilation = new Compilation(null, new CompilationUnitList(), new CompilerOptions(), null);
    this.compilation.CompilerParameters.OutputAssembly = this.testName;
    this.compilations.Add(this.compilation);
    this.ReadNextLine();
    while (this.inputLine != null && this.inputLine.StartsWith("file "))
      this.ConstructCompilationUnit();
  }
  void ConstructCompilationUnit(){
    CompilationUnitSnippet cu = new CompilationUnitSnippet();
    this.compilationUnit = cu;
    this.compilation.CompilationUnits.Add(cu);
    cu.Compilation = this.compilation;
    cu.Name = new Identifier(this.inputLine.Substring(5));
    string snippetText = this.ReadString();
    cu.SourceContext.Document = this.currentDocument = this.compiler.CreateDocument(cu.Name.ToString(), 1, snippetText);
    cu.SourceContext.EndPos = snippetText.Length;
    cu.ParserFactory = new Microsoft.SpecSharp.ParserFactory();
  }
  string ReadString(){
    StringBuilder sb = new StringBuilder();
    int ch = this.reader.Read();
    while (ch > 0 && ch != '`'){
      sb.Append((char)ch);
      ch = this.reader.Read();
      if (ch == 10) 
        this.lineCounter++;
    }
    if (ch == '`'){
      this.ReadNextLine(); //Skip over line with `
      this.ReadNextLine();
    }else
      this.inputLine = null;
    return sb.ToString();
  }
  void PerformIncrementalUpdates(){
    if (this.inputLine != "before") 
      throw new MalformedSuiteException("Line "+this.lineCounter+": Expected a 'before' line");  
    while (this.inputLine == "before"){
      this.PerformIncrementalUpdate();
      //TODO: provide a way to switch the compilation unit that is to be changed
    }
  }
  void PerformIncrementalUpdate(){
    SourceChangeList changes = this.ConstructSourceChangeList();
    string expectedOutput = this.GetExpectedOutput();
    //perform update
    Document originalDocument = this.compilationUnit.SourceContext.Document;
    Document changedDocument = this.currentDocument = this.GetChangedDocument(this.currentDocument, changes);
    lock(this){ //prevent asynchronous symbol table update event handler from producing output until after CheckUpdatedCompilation has run
      Compilation updatedCompilation = this.compiler.UpdateSymbolTable(this.compilation, originalDocument, changedDocument, changes, this.errors);
      this.CheckUpdatedCompilation(this.compilation, updatedCompilation);
      this.compilation = updatedCompilation;
    }
    //Give asynchronous symbol table update events time to complete
    Thread.Sleep(20);
    //Get actual output and compare with expected output
    string actualOutput = this.output.ToString();
    if (expectedOutput != actualOutput){
      Console.SetOut(this.savedConsoleOut);
      Console.WriteLine("Test {0} line {1} failed", this.testName, this.lineCounter-1);
      Console.WriteLine("Actual output:");
      Console.Write(actualOutput);
      Console.WriteLine("Expected output:");
      Console.Write(expectedOutput);
      this.failures++;
    }
    Console.SetOut(new StringWriter(this.output = new StringBuilder()));
    System.Diagnostics.Debug.Listeners.Remove(this.traceListener);
    this.traceListener = new System.Diagnostics.TextWriterTraceListener(System.Console.Out);
    System.Diagnostics.Debug.Listeners.Add(this.traceListener);
  }
  void CheckUpdatedCompilation(Compilation originalCompilation, Compilation updatedCompilation){
    this.WriteOutAnyErrors();
    if (originalCompilation != updatedCompilation){
      Console.WriteLine("update of {0} resulted in a new compilation instance", originalCompilation.TargetModule.Name);
      return;
    }
    CompilationUnit updatedCompilationUnit = null;
    for (int i = 0, n = updatedCompilation.CompilationUnits == null ? 0 : updatedCompilation.CompilationUnits.Count; i < n; i++){
      if (updatedCompilation.CompilationUnits[i] == null)
        Console.WriteLine("updated compilation unit {0} is null", i);
      else if (updatedCompilation.CompilationUnits[i].Name.UniqueIdKey == this.compilationUnit.Name.UniqueIdKey){
        updatedCompilationUnit = updatedCompilation.CompilationUnits[i];
        break;
      }
    }
    StatementVisitor statVis = new StatementVisitor();
    statVis.Visit(updatedCompilationUnit);
  }
  class StatementVisitor : StandardVisitor{
    public override Node Visit(Node node){
      Member mem = node as Member;
      if (mem != null && mem.SourceContext.Document != null){
        Console.WriteLine("{0}: {1}", mem.SourceContext.StartLine, mem.FullName);
      }
      return base.Visit(node);      
    }
  }
  string GetExpectedOutput(){
    if (this.inputLine != "output") 
      throw new MalformedSuiteException("Line "+this.lineCounter+": Expected an 'output' line");  
    return this.ReadString();
  }
  SourceChangeList ConstructSourceChangeList(){
    SourceChangeList result = new SourceChangeList();
    int lastPos = 0;
    do{
      int beforeLine = this.lineCounter;
      string beforeText = this.ReadString();
      if (this.inputLine != "after") 
        throw new MalformedSuiteException("Line "+this.lineCounter+": Expected an 'after' line");
      string afterText = this.ReadString();
      SourceChange sc = new SourceChange();
      sc.ChangedText = afterText;
      sc.SourceContext = this.compilationUnit.SourceContext;
      int beforePos = this.currentDocument.Text.Source.IndexOf(beforeText, lastPos);
      if (beforePos < 0) 
        throw new MalformedSuiteException("Line "+beforeLine+": before text not found");
      sc.SourceContext.StartPos = beforePos;
      sc.SourceContext.EndPos = lastPos = beforePos + beforeText.Length;
      result.Add(sc);
    }while (this.inputLine == "before");
    return result;
  }
  Document GetChangedDocument(Document originalDocument, SourceChangeList changes){
    string originalString = originalDocument.Text.Source;
    StringBuilder sb = new StringBuilder();
    int lastPos = 0;
    for (int i = 0, n = changes.Count; i < n; i++){
      SourceChange sc = changes[i];
      sb.Append(originalString, lastPos, sc.SourceContext.StartPos-lastPos);
      sb.Append(sc.ChangedText);
      lastPos = sc.SourceContext.EndPos;
    }
    sb.Append(originalString, lastPos, originalString.Length-lastPos);
    return this.compiler.CreateDocument(this.compilationUnit.SourceContext.Document.Name, 1, sb.ToString());
  }
  void ReadNextLine(){
    this.inputLine = this.reader.ReadLine();
    this.lineCounter++;
  }
}
class MalformedSuiteException : Exception{
  public MalformedSuiteException(string message)
    : base(message){
  }
}