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
using System.CodeDom.Compiler;
using System.Collections;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Text;

class main{
  /// <summary>
  /// The main entry point for the application.
  /// </summary>
  [STAThread]
  static int Main(string[] args){
    if (0 < args.Length && args[0] == "/break"){
      string[] newArgs = new string[args.Length - 1];
      Array.Copy(args, 1, newArgs, 0, newArgs.Length);
      args = newArgs;
      System.Diagnostics.Debugger.Break();
    }
    int rc = 0;
    bool includeStandardResponseFile = true;
    string fileName = null;
    int n = args == null ? 0 : args.Length;
    bool testsuite = false;
    for (int i = 0; i < n; i++){
      string arg = args[i];
      if (arg == null || arg.Length < 1) continue;
      char ch = arg[0];
      if (ch == '/'){
        if (arg == "/noconfig" || arg == "/nostdlib")
          includeStandardResponseFile = false;
      }else if (ch == '-'){
        if (arg == "-noconfig" || arg == "-nostdlib")
          includeStandardResponseFile = false;
      }else if (ch != '@'){
        fileName = arg;
        if (BetterPath.GetExtension(fileName) == ".suite") 
          testsuite = true;
      }
    }
    if (includeStandardResponseFile){
      int nFiles = 2;
      string globalPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
      string globalResponseFile = Path.Combine(globalPath, "csc.rsp");
      if (!File.Exists(globalResponseFile)){
        globalResponseFile = null; nFiles--;
      }
      string localPath = Directory.GetCurrentDirectory();
      string localResponseFile = Path.Combine(localPath, "csc.rsp");
      if (!File.Exists(localResponseFile)){
        localResponseFile = null;
        nFiles--;
      }
      if (nFiles > 0){
        string[] newArgs = new string[n+nFiles];
        int i = 0;
        if (globalResponseFile != null) newArgs[i++] = "@"+globalResponseFile;
        if (localResponseFile != null) newArgs[i++] = "@"+localResponseFile;
        for (int j = 0; j < n; j++) newArgs[i+j] = args[j];
        args = newArgs;
      }
    }
    Microsoft.SpecSharp.SpecSharpCompilerOptions options = new Microsoft.SpecSharp.SpecSharpCompilerOptions();
    options.TempFiles = new TempFileCollection(Directory.GetCurrentDirectory(), true);
    options.GenerateExecutable = true;
    options.MayLockFiles = true;
    options.CompileAndExecute = testsuite;
    ErrorNodeList errors = new ErrorNodeList(0);
    Microsoft.SpecSharp.Compiler compiler = new Microsoft.SpecSharp.Compiler();
    string[] fileNames = compiler.ParseCompilerParameters(options, args, errors);
    if (options.DisplayCommandLineHelp){
      System.Resources.ResourceManager rm = new System.Resources.ResourceManager("ssc.Messages", typeof(main).Module.Assembly);      
      Console.WriteLine(rm.GetString("UsageTitle", null));
      Console.WriteLine(options.GetOptionHelp());
      return 0;
    }
    if (testsuite){
      // fileNames has expanded wildcards.
      bool suiteSuccess = true;
      foreach (string file in fileNames)
        suiteSuccess &= main.RunSuite(file);

      if (suiteSuccess) {
        return 0;
      }
      return 1;
    }    
    string fatalErrorString = null;
    n = errors.Count;
    for (int i = 0; i < n; i++){
      ErrorNode e = errors[i];
      if (e == null) continue;
      rc++;
      if (fatalErrorString == null) fatalErrorString = compiler.GetFatalErrorString();
      Console.Write(fatalErrorString, e.Code.ToString("0000"));
      Console.WriteLine(e.GetMessage());
    }
    if (rc > 0) return 1;
    switch (options.TargetPlatform){
      case PlatformType.notSpecified: 
        if (options.NoStandardLibrary && (options.StandardLibraryLocation == null || options.StandardLibraryLocation.Length == 0))
          Microsoft.SpecSharp.TargetPlatform.SetToV2(options.TargetPlatformLocation);
        break;
      case PlatformType.v1: Microsoft.SpecSharp.TargetPlatform.SetToV1(options.TargetPlatformLocation); break;
      case PlatformType.v11: Microsoft.SpecSharp.TargetPlatform.SetToV1_1(options.TargetPlatformLocation); break;
      case PlatformType.v2: Microsoft.SpecSharp.TargetPlatform.SetToV2(options.TargetPlatformLocation); break;
      default: 
        if (options.TargetPlatformLocation != null) //TODO: assert not null
          Microsoft.SpecSharp.TargetPlatform.SetToPostV1_1(options.TargetPlatformLocation);
        break;
    }
    compiler.UpdateRuntimeAssemblyLocations(options);
    CompilerResults results;
    if (fileNames.Length == 1){
      if (options.EmitManifest)
        results = compiler.CompileAssemblyFromFile(options, fileNames[0], new ErrorNodeList(), true);
      else
        results = compiler.CompileModuleFromFile(options, fileNames[0]);
    }else{
      if (options.EmitManifest)
        results = compiler.CompileAssemblyFromFileBatch(options, fileNames);
      else
        results = compiler.CompileModuleFromFileBatch(options, fileNames);
    }
    string errorString = null;
    string warningString = null;
    foreach (CompilerError e in results.Errors){
      if (e.FileName != null && e.FileName.Length > 0){
        Console.Write(main.GetPath(e.FileName, options));
        Console.Write('(');
        Console.Write(e.Line);
        Console.Write(',');
        Console.Write(e.Column);
        Console.Write("): ");
      }
      if (e.IsWarning){
        if (!e.ErrorNumber.StartsWith("CS") || e.ErrorNumber.Length == 6){
          if (warningString == null) warningString = compiler.GetWarningString();
          Console.Write(warningString, e.ErrorNumber);
        }
      }else{
        if (!e.ErrorNumber.StartsWith("CS") || e.ErrorNumber.Length == 6){
          rc++; //REVIEW: include related location errors?
          if (errorString == null) errorString = compiler.GetErrorString();
          Console.Write(errorString, e.ErrorNumber);
        }
      }
      Console.WriteLine(e.ErrorText);
    }
    if (rc > 0) return 1;
    if ((rc = results.NativeCompilerReturnValue) == 0 && options.CompileAndExecute && 
    results.CompiledAssembly != null && results.CompiledAssembly.EntryPoint != null){
      if (results.CompiledAssembly.EntryPoint.GetParameters().Length == 0)
        results.CompiledAssembly.EntryPoint.Invoke(null, null);
      else
        results.CompiledAssembly.EntryPoint.Invoke(null, new object[]{new string[0]});
    }      
    if (rc > 0) return 1;
    return 0;
  }
  private static string GetPath(string path, Microsoft.SpecSharp.SpecSharpCompilerOptions options)
  {
    if (options.FullyQualifyPaths) {
      return path;
    }
    else {
      return GetRelativePath(path);
    }
  }
  private static string GetRelativePath(string fullPath){
    string currentDir = Directory.GetCurrentDirectory();
    if (currentDir == null || currentDir.Length == 0) return fullPath;
    System.Globalization.CompareInfo compInfo = System.Globalization.CultureInfo.InvariantCulture.CompareInfo;
    if (compInfo.IsPrefix(fullPath, currentDir, System.Globalization.CompareOptions.IgnoreCase))
      return fullPath.Substring(currentDir.Length+1);
    return fullPath;
  }

  /// <summary>
  /// Run a suite
  /// </summary>
  /// <returns>true if suite succeeds, false if it fails.</returns>
  private static bool RunSuite(string suiteName){
    System.Diagnostics.Debug.Listeners.Remove("Default");
    StringBuilder source = null;
    StringBuilder expectedOutput = null;
    StringBuilder actualOutput = null;
    ArrayList suiteParameters = new ArrayList();
    ArrayList compilerParameters = null;
    ArrayList testCaseParameters = null;
    bool xamlSuite = false;
    int errors = 0;
    main.assemblyNameCounter = 0;
    try{
      StreamReader instream = File.OpenText(suiteName);
      int ch = instream.Read();
      int line = 1;
      while (ch >= 0){
        compilerParameters = (ArrayList)suiteParameters.Clone();
        bool skipTest = false;
        if (ch == '`'){
          ch = instream.Read();
          bool parametersAreForEntireSuite = false;
          if (ch == '`'){
            parametersAreForEntireSuite = true; 
            ch = instream.Read();
            if (ch == 'x'){
              xamlSuite = true;
              while (ch != 13) ch = instream.Read();
            }
          }
          while (ch == '/'){
            //compiler parameters
            StringBuilder cParam = new StringBuilder();
            do{
              cParam.Append((char)ch);
              ch = instream.Read();
            }while(ch != '/' && ch != 0 && ch != 10 && ch != 13);
            for (int i = cParam.Length-1; i >= 0; i--){
              if (!Char.IsWhiteSpace(cParam[i])) break;
              cParam.Length = i;
            }
            string cp = cParam.ToString();
            if (cp == "/p:v2" && TargetPlatform.TargetVersion.Major < 2) skipTest = true;
            compilerParameters.Add(cp);
          }
          if (parametersAreForEntireSuite)
            suiteParameters.AddRange(compilerParameters);
          if (ch == 13) ch = instream.Read();
          if (ch == 10){
            line++;
            ch = instream.Read();
            if (parametersAreForEntireSuite && ch == '`') continue;
          }         
        }
        if (ch == ':'){
          ch = instream.Read();
          while (ch == '='){
            //test case parameters
            StringBuilder tcParam = new StringBuilder();
            ch = instream.Read(); //discard =
            while(ch != '=' && ch != 0 && ch != 10 && ch != 13){
              tcParam.Append((char)ch);
              ch = instream.Read();
            }
            for (int i = tcParam.Length-1; i >= 0; i--){
              if (!Char.IsWhiteSpace(tcParam[i])) break;
              tcParam.Length = i;
            }
            if (testCaseParameters == null) testCaseParameters = new ArrayList();
            testCaseParameters.Add(tcParam.ToString());
          }
          if (ch == 13) ch = instream.Read();
          if (ch == 10){
            ch = instream.Read();
            line++;
          }
        }
        source = new StringBuilder();
        while (ch >= 0 && ch != '`'){
          source.Append((char)ch);
          ch = instream.Read();
          if (ch == 10) line++;
        }
        if (ch < 0){
          Console.WriteLine("The last test case in the suite has not been provided with expected output");
          errors++;
          break;
        }
        ch = instream.Read();
        if (ch == 13) ch = instream.Read();
        if (ch == 10){
          line++;
          ch = instream.Read();
        }        
        int errLine = line;
        expectedOutput = new StringBuilder();
        while (ch >= 0 && ch != '`'){
          expectedOutput.Append((char)ch);
          ch = instream.Read();
          if (ch == 10) line++;
        }
        if (expectedOutput.Length > 0 && expectedOutput[expectedOutput.Length-1] == 10)
          expectedOutput.Length -= 1;
        if (expectedOutput.Length > 0 && expectedOutput[expectedOutput.Length-1] == 13)
          expectedOutput.Length -= 1;
        ch = instream.Read();
        if (ch == 13) ch = instream.Read();
        if (ch == 10){ 
          ch = instream.Read(); 
          line++; 
        }                
        if (skipTest) continue;
        actualOutput = new StringBuilder();
        TextWriter savedOut = Console.Out;
        Console.SetOut(new StringWriter(actualOutput));
        System.Diagnostics.TextWriterTraceListener myWriter = new System.Diagnostics.TextWriterTraceListener(System.Console.Out);
        System.Diagnostics.Debug.Listeners.Add(myWriter);
        try{
          int returnCode = RunTest(Path.GetFileNameWithoutExtension(suiteName), source.ToString(), actualOutput, compilerParameters, testCaseParameters, xamlSuite);
          if (returnCode != 0)
            actualOutput.Append("Non zero return code: "+returnCode);
        }catch(Exception e){
          actualOutput.Append(e.Message);
        }
        compilerParameters = null;
        testCaseParameters = null;
        Console.SetOut(savedOut);
        System.Diagnostics.Debug.Listeners.Remove(myWriter);
        if (actualOutput.Length > 0 && actualOutput[actualOutput.Length - 1] == 10)
          actualOutput.Length -= 1;
        if (actualOutput.Length > 0 && actualOutput[actualOutput.Length - 1] == 13)
          actualOutput.Length -= 1;
        if (!expectedOutput.ToString().Equals(actualOutput.ToString())) {
          if (errors++ == 0) Console.WriteLine(suiteName+" failed");
          Console.WriteLine("source({0}):", errLine);
          if (source != null)
            Console.WriteLine(source);
          Console.WriteLine("actual output:");
          Console.WriteLine(actualOutput);
          Console.WriteLine("expected output:");
          if (expectedOutput != null)
            Console.WriteLine(expectedOutput);
        }
      }
      instream.Close();
      if (errors == 0) {
        Console.WriteLine(suiteName + " passed");
        return true;
      }
      else {
        Console.WriteLine(suiteName + " had " + errors + (errors > 1 ? " failures" : " failure"));
        return false;
      }
    }catch{
      Console.WriteLine(suiteName+" failed");
      Console.WriteLine("source:");
      if (source != null)
        Console.WriteLine(source);
      Console.WriteLine("actual output:");
      Console.WriteLine(actualOutput);
      Console.WriteLine("expected output:");
      if (expectedOutput != null)
        Console.WriteLine(expectedOutput);
      return false;
    }
  }
  
  static int assemblyNameCounter = 0;
  private static int RunTest(string suiteName, string test, StringBuilder actualOutput, ArrayList compilerParameters, ArrayList testCaseParameters, bool xamlSuite){
    Microsoft.SpecSharp.Compiler compiler = new Microsoft.SpecSharp.Compiler();
    compiler.CompileAsXaml = xamlSuite;
    CompilerOptions options = new Microsoft.SpecSharp.SpecSharpCompilerOptions();
    if (compilerParameters != null){
      ErrorNodeList compilerParameterErrors = new ErrorNodeList(0);
      compiler.ParseCompilerParameters(options, (string[])compilerParameters.ToArray(typeof(string)), compilerParameterErrors, false);
      for (int i = 0, n = compilerParameterErrors.Count; i < n; i++){
        ErrorNode err = compilerParameterErrors[i];
        Console.WriteLine(err.GetMessage());
      }
    }
    options.OutputAssembly = "assembly for suite " + suiteName + " test case "+main.assemblyNameCounter++;
    options.GenerateExecutable = true;
    options.MayLockFiles = true;
    options.GenerateInMemory = true;
    // Code that is not marked as unsafe should be verifiable. This catches cases where the compiler generates bad IL.
    if (!options.AllowUnsafeCode)
      options.Evidence = new System.Security.Policy.Evidence(new object[] {new System.Security.Policy.Zone(System.Security.SecurityZone.Internet)}, null);
    CompilerResults results = compiler.CompileAssemblyFromSource(options, test);
    foreach (CompilerError e in results.Errors){
      Console.Write('(');
      Console.Write(e.Line);
      Console.Write(',');
      Console.Write(e.Column);
      Console.Write("): ");
      string warningString = null;
      string errorString = null;
      if (e.IsWarning){
        if (!e.ErrorNumber.StartsWith("CS") || e.ErrorNumber.Length == 6){
          if (warningString == null) warningString = compiler.GetWarningString();
          Console.Write(warningString, e.ErrorNumber);
        }
      }else{
        if (!e.ErrorNumber.StartsWith("CS") || e.ErrorNumber.Length == 6){
          if (errorString == null) errorString = compiler.GetErrorString();
          Console.Write(errorString, e.ErrorNumber);
        }
      }
      Console.WriteLine(e.ErrorText);
    }
    if (results.NativeCompilerReturnValue != 0) return 0;
    object returnVal = null;
    try{
      if (testCaseParameters == null){
        if (results.CompiledAssembly.EntryPoint.GetParameters().Length == 0)
          returnVal = results.CompiledAssembly.EntryPoint.Invoke(null, null);
        else
          returnVal = results.CompiledAssembly.EntryPoint.Invoke(null, new object[]{new string[0]});
      }else
        returnVal = results.CompiledAssembly.EntryPoint.Invoke(null, new object[]{(string[])testCaseParameters.ToArray(typeof(string))});
    }catch(System.Reflection.TargetInvocationException e){
      throw e.InnerException;
    }
    if (returnVal is int) return (int)returnVal;
    return 0;
  }
}
