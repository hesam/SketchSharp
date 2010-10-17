//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using Microsoft.Contracts;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler {
#endif


  abstract public class Analyzer : StandardVisitor {

    /// <summary>
    /// Current compilation being analyzed.
    /// </summary>
    protected Compilation compilation;

    /// <summary>
    /// Type system for the compilation unit.
    /// </summary>
    protected TypeSystem typeSystem;

    /// <summary>
    /// For checking once fields
    /// </summary>
    protected IMutableSet fieldUsage;

    /// <summary>
    /// Turn the debug information on
    /// </summary>
    private static bool debug = false;

    /// <summary>
    /// Turn the detailed DFA debug information on
    /// </summary>
    private static bool debugDFA = false;



    /// <summary>
    /// Turn on statistics
    /// </summary>
    private static bool stats = false;

    public static bool Debug {
      get { return debug; }
    }

    public static bool DebugDFA {
      get { return debugDFA; }
    }

    public static bool Statistics {
      get { return stats; }
    }

    /// <summary>
    /// Debugging switch.
    /// </summary>
    private string analyzerDebugSymbol = "ADEBUG";
    private string statisticsDebugSymbol = "ASTATS";
    private string DFADebugSymbol = "ADEBUGDFA";

    /// <summary>
    /// Command line switches.
    /// </summary>
    protected Hashtable PreprocessorDefinedSymbols;

    /// <summary>
    /// Implications from command line switches (with defaults)
    /// </summary>
    protected bool NonNullChecking = true;
    protected bool DefiniteAssignmentChecking = true;
    protected bool ExposureChecking = false;

    /// <summary>
    /// Repository that stores the CFGs that have been built.
    /// 
    /// Since building Control Flow Graph is destructive, we need to keep it for later use.
    /// </summary>
    private Hashtable cfgRepository = new Hashtable();

    /// <summary>
    /// Get the correct CFG for the method.
    /// </summary>
    /// <param name="method"></param>
    /// <returns></returns>
    public ControlFlowGraph GetCFG(Method method) {
      ControlFlowGraph cfg;
      if (cfgRepository.Contains(method))
        cfg = (ControlFlowGraph)cfgRepository[method];
      else {
        cfg = ControlFlowGraph.For(method);
        cfgRepository.Add(method, cfg);
      }
      return cfg;
    }

    /// <summary>
    /// return the level of the most severe error. 
    /// </summary>
    /// <param name="errors"></param>
    /// <returns></returns>
    static public int ErrorSeverity(ErrorNodeList errors) {
      int severity = int.MaxValue;
      for (int i = 0; i < errors.Count; i++) {
        ErrorNode e = errors[i];
        if (e == null) continue;
        if (e.Severity < 0) continue;
        severity = severity > e.Severity ? e.Severity : severity;
      }
      return severity;
    }

    public static void WriteLine() {
      if (debug)
        Console.WriteLine();
    }
    public static void WriteLine(Object o) {
      if (debug)
        Console.WriteLine(o);
    }
    public static void Write(Object o) {
      if (debug)
        Console.Write(o);
    }


    /// <summary>
    /// True if compilation had no errors so far, meaning the trees are well formed and we 
    /// can build CFGs.
    /// </summary>
    protected readonly bool CodeIsWellFormed;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="t">The type system for the compilation.</param>
    /// <param name="c">The complication being analyzed.</param>
    public Analyzer(TypeSystem t, Compilation c) {
      this.typeSystem = t;
      this.compilation = c;
      PreprocessorDefinedSymbols = null;
      if (c != null && c.CompilationUnits[0] != null)
        PreprocessorDefinedSymbols = c.CompilationUnits[0].PreprocessorDefinedSymbols;
      if (PreprocessorDefinedSymbols != null) {
        if (PreprocessorDefinedSymbols.ContainsKey("NONULLCHECK")) {
          this.NonNullChecking = false;
        }
        if (PreprocessorDefinedSymbols.ContainsKey("NONONNULLTYPECHECK")) {
          this.NonNullChecking = false;
        }
        if (PreprocessorDefinedSymbols.ContainsKey("NODEFASSIGN")) {
          this.DefiniteAssignmentChecking = false;
        }
        if (PreprocessorDefinedSymbols.ContainsKey("EXPOSURECHECK")) {
          this.ExposureChecking = true;
        }
        if (PreprocessorDefinedSymbols.ContainsKey("NOEXPOSURECHECK")) {
          this.ExposureChecking = false;
        }
        if (PreprocessorDefinedSymbols.ContainsKey(analyzerDebugSymbol)) {
          Analyzer.debug = true;
        }
        if (PreprocessorDefinedSymbols.ContainsKey(DFADebugSymbol)) {
          Analyzer.debugDFA = true;
        }
        if (PreprocessorDefinedSymbols.ContainsKey(statisticsDebugSymbol)) {
          Analyzer.stats = true;
        }
      }

      this.CodeIsWellFormed = ErrorSeverity(typeSystem.Errors) > 0;
    }

    public System.Compiler.CompilerOptions CompilerOptions {
      get {
        if (this.compilation != null) return this.compilation.CompilerParameters as System.Compiler.CompilerOptions;
        return null;
      }
    }

    /// <summary>
    /// Analyze the given method.
    /// </summary>
    /// <param name="method"></param>
    public virtual void Analyze(Method method) {
      GeneralAnalysis(method);
      LanguageSpecificAnalysis(method);
    }

    /// <summary>
    /// Language specific flow analysis.
    /// </summary>
    /// <param name="method"></param>
    protected virtual void LanguageSpecificAnalysis(Method method) {
    }

    private INonNullInformation nonNullInfo;
    // existential delay info
    private IDelayInfo delayInfo;
    
    // end existential delay

    public INonNullInformation NonNullInfo {
      get {
        return this.nonNullInfo;
      }
    }

    // existential delay
    public IDelayInfo DelayInfo {
      get {
        return this.delayInfo;
      }
    }
    // end existential delay

    public IMutableSet FieldUsage {
      get { return fieldUsage; }
    }


    /// <summary>
    /// Put general analysis targeting to general IL properties.
    /// </summary>
    /// <param name="method"></param>
    protected void GeneralAnalysis(Method method) {

      nonNullInfo = null;

      if (!this.CodeIsWellFormed)
        return;

      if (debug) {
        ControlFlowGraph cfg = GetCFG(method);
        if (cfg != null) cfg.Display(Console.Out);
      }

      if (!method.Name.Name.StartsWith("Microsoft.Contracts")) {
        // Definite assignment checking

        //System.Console.WriteLine("--------------- Analyzing Method: {0}", method.Name);

        
        if (this.DefiniteAssignmentChecking) {
          // For every statement, three things are returned from this pre- stage analysis
          // 1) which program vars (that represent NN arrays) are created but not committed
          // 2) which program vars (that represent NN arrays) are created and committed between
          //    the creation and commitment of current array. 
          // 3) if the statement is a commitment call for an NN array, whether it 
          //    is ok to lift the array to be non-delayed. 
          PreDAStatus preAnalysisResult = MethodReachingDefNNArrayChecker.Check(typeSystem, method, this);
          if (preAnalysisResult != null) {
            delayInfo = MethodDefiniteAssignmentChecker.Check(typeSystem, method, this, preAnalysisResult);
          }
        }
        if (Analyzer.Debug) {
          if (delayInfo != null)
            System.Console.WriteLine("----- delay info count: {0}", delayInfo.Count());
        }

        if (this.ExposureChecking)
          ExposureChecker.Check(typeSystem, method, this);

        // NonNull checking
        if (this.NonNullChecking)
          nonNullInfo = NonNullChecker.Check(typeSystem, method, this);

        //System.Console.WriteLine("---------------- Finished Analyzing Method:{0}", method.Name);

      }
    }
    public override Class VisitClass(Class Class) {
      fieldUsage = new NodeSet();
      return base.VisitClass(Class);
    }

    public override CompilationUnit VisitCompilationUnit(CompilationUnit cUnit) {
      this.typeSystem.ErrorHandler.SetPragmaWarnInformation(cUnit.PragmaWarnInformation);
      CompilationUnit retCUnit = base.VisitCompilationUnit(cUnit);
      this.typeSystem.ErrorHandler.ResetPragmaWarnInformation();
      return retCUnit;
    }
    /// <summary>
    /// Visit each method, start the analysis.  
    /// </summary>
    /// <param name="method"></param>
    /// <returns></returns>
    public override Method VisitMethod(Method method) {
      if (method != null) {
        WriteLine("\n----------Analyzer: visiting " + method.FullName + "\n");
        Analyze(method);
      }
      return method;
    }
  }

  /// <summary>
  /// Useful for temporary storage of errors and sorting prior to emitting the errors.
  /// </summary>
  public class ErrorReport<T> : System.IComparable<ErrorReport<T>>, IComparable {
    public readonly Node OffendingNode;
    public readonly T Error;
    public readonly string[] Messages;
    public ErrorReport(Node offendingNode, T error, params string[] messages) {
      Debug.Assert(offendingNode != null); // always be the method just analyzed, shouldnt be null
      OffendingNode = offendingNode;
      Error = error;
      if (messages != null) {
        Messages = messages;
      }
      else {
        Messages = new string[0];
      }
    }
    public int CompareTo(ErrorReport<T> that) {
      int result = this.OffendingNode.SourceContext.StartLine.CompareTo(that.OffendingNode.SourceContext.StartLine);
      if (result != 0) return result;
      result = this.OffendingNode.SourceContext.StartColumn.CompareTo(that.OffendingNode.SourceContext.StartColumn);
      if (result != 0) return result;
      if (this.Error is IComparable) {
        result = ((IComparable)this.Error).CompareTo(that.Error);
      }
      if (result != 0) return result;
      result = this.Messages.Length.CompareTo(that.Messages.Length);
      if (result != 0) return result;
      if (this.Messages.Length == 0) { return 0; }
      for (int i = 0; i < this.Messages.Length; i++) {
        string s1 = (string)this.Messages[i];
        result = s1.CompareTo(that.Messages[i]);
        if (result != 0) return result;
      }
      return 0;
    }
    public int CompareTo(object obj) {
      ErrorReport<T> that = obj as ErrorReport<T>;
      if (that == null) return -1;
      return this.CompareTo(that);
    }
  }


}
