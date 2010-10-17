//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------

namespace Microsoft.SpecSharp
{
  using System;
  using System.CodeDom.Compiler;
  using System.Collections;
  using System.Collections.Generic;
  using System.Collections.Specialized;
  using System.Diagnostics;
  using System.Globalization;
  using System.IO;
  using System.Reflection;
  using System.Text;
  using Microsoft.Build.Framework;
  using Microsoft.Build.Utilities;
  using Microsoft.SpecSharp;
  using Cci = System.Compiler;
#if !NoVS
  using Microsoft.Build.Tasks;
  using Microsoft.VisualStudio.CodeTools;
  using Microsoft.VisualStudio.Shell.Interop;
#endif


  public class BuildSite : Cci.CompilerSite {
#if !NoVS
    private ITaskManager taskManager;
#endif
    private TaskLoggingHelper taskLogger;

#if !NoVS
    public BuildSite(ITaskManager taskManager, TaskLoggingHelper taskLogger) {
      this.taskManager = taskManager;
      this.taskLogger = taskLogger;
    }
#endif
    public BuildSite(TaskLoggingHelper taskLogger) {
      this.taskLogger = taskLogger;
    }

    public override void OutputMessage(string message) {
      if (message == null) return;
#if !NoVS
      if (this.taskManager != null)
        this.taskManager.OutputString(message, TaskOutputPane.Build);
      else
#endif
      if (this.taskLogger != null)
        this.taskLogger.LogMessage(message, null);
    }
  }

  public class SpecSharpCompile : Task
  {
    #region Properties
    /// <summary>
    /// Backing store for the properties that are set by MsBuild.
    /// </summary>
    private Hashtable/*!*/ Bag = new Hashtable();

    /// <summary>
    /// Get a bool parameter and return a default if its not present
    /// in the hash table.
    /// </summary>
    protected internal bool GetBoolParameterWithDefault(string parameterName, bool defaultValue)
    {
      object obj = this.Bag[parameterName];
      if (obj == null) {
        return defaultValue;
      }
      return (bool)obj;
    }

    /// <summary>
    /// Get an int parameter and return a default if its not present
    /// in the hash table.
    /// </summary>
    private int GetIntParameterWithDefault(string parameterName, int defaultValue)
    {
      object obj = this.Bag[parameterName];
      if (obj == null) {
        return defaultValue;
      }
      return (int)obj;
    }


    /// <summary>
    /// Corresponds to /lib:&lt;file list&gt; on the command line
    /// </summary>
    public string[] AdditionalLibPaths
    {
      set { this.Bag["AdditionalLibPaths"] = value; }
      get { return (string[])this.Bag["AdditionalLibPaths"]; }
    }

    /// <summary>
    /// Corresponds to /addmodule:&lt;file list&gt; on the command line.
    /// </summary>
    public string[] AddModules
    {
      set { this.Bag["AddModules"] = value; }
      get { return (string[])this.Bag["AddModules"]; }
    }

    /// <summary>
    /// Corresponds to /unsafe[+|-] on the command line.
    /// </summary>
    public bool AllowUnsafeBlocks
    {
      set { this.Bag["AllowUnsafeBlocks"] = value; }
      get { return this.GetBoolParameterWithDefault("AllowUnsafeBlocks", false); }
    }

    /// <summary>
    /// Corresponds to /baseaddress:&lt;address&gt; on the command line.
    /// </summary>
    public string BaseAddress
    {
      set { this.Bag["BaseAddress"] = value; }
      get { return (string)this.Bag["BaseAddress"]; }
    }

    /// <summary>
    /// Corresponds to /checked[+|-] on the command line.
    /// </summary>
    public bool CheckForOverflowUnderflow
    {
      set { this.Bag["CheckForOverflowUnderflow"] = value; }
      get { return this.GetBoolParameterWithDefault("CheckForOverflowUnderflow", false); }
    }

    /// <summary>
    /// Corresponds to /codepage:&lt;n&gt; on the command line.
    /// </summary>
    public int CodePage
    {
      set { this.Bag["CodePage"] = value; }
      get { return this.GetIntParameterWithDefault("CodePage", 0); }
    }

    /// <summary>
    /// Hide usual C# errors?
    /// </summary>
    public int ContractsHideCSharpErrors
    {
      set { this.Bag["HideCSharpErrors"] = value; }
      get { return this.GetIntParameterWithDefault("HideCSharpErrors", 1000); }
    }

    /// <summary>
    /// Corresponds to /debug:{full|pdbonly} on the command line.
    /// </summary>
    public string DebugType
    {
      set { this.Bag["DebugType"] = value; }
      get { return (string)this.Bag["DebugType"]; }
    }

    /// <summary>
    /// Corresponds to /define:&lt;symbol list&gt; on the command line.
    /// </summary>
    public string DefineConstants
    {
      set { this.Bag["DefineConstants"] = value; }
      get { return (string)this.Bag["DefineConstants"]; }
    }

    /// <summary>
    /// Corresponds to /delaysign[+|-] on the command line.
    /// </summary>
    public bool DelaySign
    {
      set { this.Bag["DelaySign"] = value; }
      get { return this.GetBoolParameterWithDefault("DelaySign", false); }
    }

    /// <summary>
    /// Corresponds to /disable:&lt;contract feature list&gt; on the command line. Contains the &lt;contract feature list&gt; part as semicolon separated decimals.
    /// </summary>
    public string DisabledContractFeatures
    {
      set { this.Bag["DisabledContractFeatures"] = value; }
      get { return (string)this.Bag["DisabledContractFeatures"]; }
    }

    /// <summary>
    /// Corresponds to /nowarn:&lt;warn list&gt; on the command line. Contains the &lt;warn list&gt; part as semicolon separated decimals.
    /// </summary>
    public string DisabledWarnings {
      set { this.Bag["DisabledWarnings"] = value; }
      get { return (string)this.Bag["DisabledWarnings"]; }
    }

    /// <summary>
    /// Corresponds to on /doc:&lt;file&gt; the command line.
    /// </summary>
    public string DocumentationFile
    {
      set { this.Bag["DocumentationFile"] = value; }
      get { return (string)this.Bag["DocumentationFile"]; }
    }

    /// <summary>
    /// Corresponds to /errorreport:&lt;string&gt; on the command line.
    /// </summary>
    public string ErrorReport
    {
      set { this.Bag["ErrorReport"] = value; }
      get { return (string)this.Bag["ErrorReport"]; }
    }

    /// <summary>
    /// Corresponds to /debug[+|-] on the command line.
    /// </summary>
    public bool EmitDebugInformation
    {
      set { this.Bag["EmitDebugInformation"] = value; }
      get { return this.GetBoolParameterWithDefault("EmitDebugInformation", false); }
    }

    /// <summary>
    /// Corresponds to /filealign:&lt;n&gt; on the command line.
    /// </summary>
    public int FileAlignment
    {
      set { this.Bag["FileAlignment"] = value; }
      get { return this.GetIntParameterWithDefault("FileAlignment", 0); }
    }

    /// <summary>
    /// Corresponds to /fullpaths on the command line.
    /// </summary>
    public bool GenerateFullPaths
    {
      set { this.Bag["GenerateFullPaths"] = value; }
      get { return this.GetBoolParameterWithDefault("GenerateFullPaths", false); }
    }

    /// <summary>
    /// Corresponds to /keycontainer:&lt;string&gt; on the command line.
    /// </summary>
    public string KeyContainer
    {
      set { this.Bag["KeyContainer"] = value; }
      get { return (string)this.Bag["KeyContainer"]; }
    }

    /// <summary>
    /// Corresponds to /keyfile:&lt;file&gt; on the command line.
    /// </summary>
    public string KeyFile
    {
      set { this.Bag["KeyFile"] = value; }
      get { return (string)this.Bag["KeyFile"]; }
    }

    /// <summary>
    /// Corresponds to /langversion:&lt;string&gt; on the command line.
    /// </summary>
    public string LangVersion
    {
      set { this.Bag["LangVersion"] = value; }
      get { return (string)this.Bag["LangVersion"]; }
    }

    /// <summary>
    /// Corresponds to /linkresource:&lt;resinfo&gt; on the command line.
    /// Where the resinfo format is &lt;file&gt;[,&lt;stringname&gt;[,public|private]]
    /// </summary>
    public ITaskItem[] LinkResources
    {
      set { this.Bag["LinkResources"] = value; }
      get { return (ITaskItem[])this.Bag["LinkResources"]; }
    }

    /// <summary>
    /// Corresponds to /main:&lt;type&gt; on the command line.
    /// </summary>
    public string MainEntryPoint
    {
      set { this.Bag["MainEntryPoint"] = value; }
      get { return (string)this.Bag["MainEntryPoint"]; }
    }

    /// <summary>
    /// Corresponds to /noconfig on the command line.
    /// </summary>
    public bool NoConfig
    {
      set { this.Bag["NoConfig"] = value; }
      get { return this.GetBoolParameterWithDefault("NoConfig", false); }
    }

    /// <summary>
    /// Corresponds to /nologo on the command line.
    /// </summary>
    public bool NoLogo
    {
      set { this.Bag["NoLogo"] = value; }
      get { return this.GetBoolParameterWithDefault("NoLogo", false); }
    }

    /// <summary>
    /// Corresponds to /nostdlib[+|-] on the command line.
    /// </summary>
    public bool NoStandardLib
    {
      set { this.Bag["NoStandardLib"] = value; }
      get { return this.GetBoolParameterWithDefault("NoStandardLib", false); }
    }

    /// <summary>
    /// Corresponds to /optimize[+|-] on the command line.
    /// </summary>
    public bool Optimize
    {
      set { this.Bag["Optimize"] = value; }
      get { return this.GetBoolParameterWithDefault("Optimize", false); }
    }

    /// <summary>
    /// Corresponds to /shadow:&lt;file&gt; on the command line. 
    /// This is a Spec# feature that indicates that the assembly being compiled contains the contracts for another assembly.
    /// </summary>
    public string OriginalAssembly
    {
      set { this.Bag["OriginalAssembly"] = value; }
      get { return (string)this.Bag["OriginalAssembly"]; }
    }

    /// <summary>
    /// Corresponds to /out:&lt;file&gt; on the command line.
    /// </summary>
    [Output]
    public string OutputAssembly
    {
      set { this.Bag["OutputAssembly"] = value; }
      get { return (string)this.Bag["OutputAssembly"]; }
    }

    public bool ProduceContractAssembly {
      set { this.Bag["ProduceContractAssembly"] = value; }
      get { return (bool)this.GetBoolParameterWithDefault("ProduceContractAssembly", false); }
    }

    /// <summary>
    /// Corresponds to /platform:&lt;string&gt; on the command line.
    /// Can be x86, Itanium, x64, or anycpu. The default is anycpu.
    /// </summary>
    public string Platform
    {
      set { this.Bag["Platform"] = value; }
      get { return (string)this.Bag["Platform"]; }
    }

    /// <summary>
    /// Corresponds to the IDE ProjectName:&lt;string&gt;. 
    /// </summary>
    private string projectName;
    public string ProjectName
    {
      set { projectName = value; }
      get { return projectName; }
    }

    /// <summary>
    /// Corresponds to /verifyopt:&lt;file list&gt; on the command line.
    /// </summary>
    public string ProgramVerifierCommandLineOptions {
      set { this.Bag["ProgramVerifierCommandLineOptions"] = value; }
      get { return (string)this.Bag["ProgramVerifierCommandLineOptions"]; }
    }

    /// <summary>
    /// Corresponds to /nonnull on the command line.
    /// </summary>
    public bool ReferenceTypesAreNonNullByDefault {
      set { this.Bag["ReferenceTypesAreNonNullByDefault"] = value; }
      get { return this.GetBoolParameterWithDefault("ReferenceTypesAreNonNullByDefault", false); }
    }

    /// <summary>
    /// Corresponds to /checkcontracts on the command line.
    /// </summary>
    public bool CheckContractAdmissibility {
      set { this.Bag["CheckContractAdmissibility"] = value; }
      get { return this.GetBoolParameterWithDefault("CheckContractAdmissibility", false); }
    }
    /// <summary>
    /// Corresponds to /checkpurity on the command line.
    /// </summary>
    public bool CheckPurity {
      set { this.Bag["CheckPurity"] = value; }
      get { return this.GetBoolParameterWithDefault("CheckPurity", false); }
    }

    /// <summary>
    /// Corresponds to /reference:&lt;file list&gt; and /reference:&lt;alias&gt;=&lt;file&gt; on the command line.
    /// </summary>
    public ITaskItem[] References
    {
      set { this.Bag["References"] = value; }
      get { return (ITaskItem[])this.Bag["References"]; }
    }

    /// <summary>
    /// Corresponds to /resource:&lt;resinfo&gt; on the command line.
    /// Where the resinfo format is &lt;file&gt;[,&lt;stringname&gt;[,public|private]]
    /// </summary>
    public ITaskItem[] Resources
    {
      set { this.Bag["Resources"] = value; }
      get { return (ITaskItem[])this.Bag["Resources"]; }
    }

    /// <summary>
    /// Corresponds to @&lt;file&gt; on the command line.
    /// </summary>
    public string ResponseFiles
    {
      set { this.Bag["ResponseFiles"] = value; }
      get { return (string)this.Bag["ResponseFiles"]; }
    }

    /// <summary>
    /// Corresponds to file names on the command line.
    /// </summary>
    public ITaskItem[] Sources
    {
      set { this.Bag["Sources"] = value; }
      get { return (ITaskItem[])this.Bag["Sources"]; }
    }

    /// <summary>
    /// Corresponds to /stdlib:&lt;file name&gt; on the command line.
    /// Incidates that the assembly at the specified location should be used in the place of mscorlib.dll.
    /// This is a Spec# only option.
    /// </summary>
    public string StandardLibraryLocation
    {
      set { this.Bag["TargetRuntimeLocation"] = value; }
      get { return (string)this.Bag["TargetRuntimeLocation"]; }
    }

    /// <summary>
    /// Corresponds to /platform:&lt;string&gt; on the command line.
    /// The values may be v1, v11, v2 and cli1. 
    /// </summary>
    public string TargetRuntime
    {
      set { this.Bag["TargetRuntime"] = value; }
      get { return (string)this.Bag["TargetRuntime"]; }
    }

    /// <summary>
    /// Corresponds to second (optional) string of /platform:&lt;string&gt;,&lt;path&gt; on the command line.
    /// The values may be v1, v11, v2 and cli1. 
    /// </summary>
    public string TargetRuntimeLocation
    { //TODO: need to rename the command line switch name because it clashes with C#
      set { this.Bag["TargetRuntimeLocation"] = value; }
      get { return (string)this.Bag["TargetRuntimeLocation"]; }
    }

    /// <summary>
    /// Corresponds to /target:&lt;string&gt; on the command line.
    /// The values may be exe, winexe, library or module.
    /// </summary>
    public string TargetType
    {
      set { this.Bag["TargetType"] = value == null ? "" : value.ToLower(CultureInfo.InvariantCulture); }
      get { return (string)this.Bag["TargetType"]; }
    }

    public string ToolPath
    {
      set { this.Bag["ToolPath"] = value == null ? "" : value.ToLower(CultureInfo.InvariantCulture); }
      get { return (string)this.Bag["ToolPath"]; }
    }

    /// <summary>
    /// Corresponds to /warnaserror[+|-] on the command line.
    /// </summary>
    public bool TreatWarningsAsErrors
    {
      set { this.Bag["TreatWarningsAsErrors"] = value; }
      get { return this.GetBoolParameterWithDefault("TreatWarningsAsErrors", false); }
    }

    /// <summary>
    ///   Use message to log errors
    /// </summary>
    public bool UseLogMessage {
      set { this.Bag["UseLogMessage"] = value; }
      get { return this.GetBoolParameterWithDefault("UseLogMessage", true); }
    }

    /// <summary>
    /// Exposed through msbuild only. If true compiler only checks and does not produce the module
    /// </summary>
    public bool OnlyTypeChecks
    {
      set { this.Bag["OnlyTypeChecks"] = value; }
      get { return this.GetBoolParameterWithDefault("OnlyTypeChecks", false); }
    }

    /// <summary>
    /// Indicates that the build task should use the compiler provided in this.HostObject (if specified).
    /// </summary>
    public bool UseHostCompilerIfAvailable
    { //Hook for future use.
      set { this.Bag["UseHostCompilerIfAvailable"] = value; }
      get { return this.GetBoolParameterWithDefault("UseHostCompilerIfAvailable", false); }
    }

    /// <summary>
    /// Corresponds to /utf8output on the command line.
    /// </summary>
    public bool Utf8Output
    {
      set { this.Bag["Utf8Output"] = value; }
      get { return this.GetBoolParameterWithDefault("Utf8Output", false); }
    }

    /// <summary>
    /// Corresponds to /verify on the command line.
    /// </summary>
    public bool Verify
    {
      set { this.Bag["Verify"] = value; }
      get { return this.GetBoolParameterWithDefault("Verify", false); }
    }

    /// <summary>
    /// Corresponds to /warn:&lt;n&gt; on the command line.
    /// </summary>
    public int WarningLevel
    {
      set { this.Bag["WarningLevel"] = value; }
      get { return this.GetIntParameterWithDefault("WarningLevel", 4); }
    }

    /// <summary>
    /// Corresponds to /warnaserror:&lt;warn list&gt; command line flag. Contains the &lt;warn list&gt; part as semicolon separated decimals.
    /// A blank/empty string (i.e. leaving out the &lt;warn list&gt; means) that all warnings are to be treated as errors.
    /// </summary>
    public string WarningsAsErrors
    {
      set { this.Bag["WarningsAsErrors"] = value; }
      get { return (string)this.Bag["WarningsAsErrors"]; }
    }

    /// <summary>
    /// Excempts certain warnings from being treated as errors when /warnaserror has been specified.
    /// Corresponds to /warnaserror-:&lt;warn list&gt; on the command line. Contains the &lt;warn list&gt; part as semicolon separated decimals.
    /// A blank/empty string (i.e. leaving out the &lt;warn list&gt;) means that all warnings are to be excempted from being treated as errors.
    /// </summary>
    public string WarningsNotAsErrors
    {
      set { this.Bag["WarningsNotAsErrors"] = value; }
      get { return (string)this.Bag["WarningsNotAsErrors"]; }
    }

    /// <summary>
    /// Corresponds to /win32icon:&lt;file&gt; on the command line.
    /// </summary>
    public string Win32Icon
    {
      set { this.Bag["Win32Icon"] = value; }
      get { return (string)this.Bag["Win32Icon"]; }
    }

    /// <summary>
    /// Corresponds to /win32res:&lt;file&gt; on the command line.
    /// </summary>
    public string Win32Resource
    {
      set { this.Bag["Win32Resource"] = value; }
      get { return (string)this.Bag["Win32Resource"]; }
    }
    #endregion

#if !NoVS
    private ITaskManager taskManager = null;
#endif

    public override bool Execute() {
      //System.Diagnostics.Debugger.Launch();
#if !NoVS
      this.taskManager = this.HostObject as ITaskManager;
#endif
      try {
        Cci.ErrorNodeList errors = new Cci.ErrorNodeList();
        Compiler specSharpCompiler = new Compiler();
        SpecSharpCompilerOptions options = this.GetCompilerOptions(specSharpCompiler);
        //TODO: give errors if the target locations are null.
        switch (options.TargetPlatform) {
          case Cci.PlatformType.notSpecified: break;
          case Cci.PlatformType.v1: Microsoft.SpecSharp.TargetPlatform.SetToV1(options.TargetPlatformLocation); break;
          case Cci.PlatformType.v11: Microsoft.SpecSharp.TargetPlatform.SetToV1_1(options.TargetPlatformLocation); break;
          case Cci.PlatformType.v2: Microsoft.SpecSharp.TargetPlatform.SetToV2(options.TargetPlatformLocation); break;
          default:
            if (options.TargetPlatformLocation != null)
              Microsoft.SpecSharp.TargetPlatform.SetToPostV1_1(options.TargetPlatformLocation);
            break;
        }
        LogRunSpecSharp(options);
        specSharpCompiler.UpdateRuntimeAssemblyLocations(options);
        Cci.TargetPlatform.DoNotLockFiles = true;
        Cci.TargetPlatform.GetDebugInfo = false;
        string[] sourceFilePaths = this.GetSourceFilePaths();
        if (this.ProduceContractAssembly && this.CompilationOutputIsUpToDate(options, sourceFilePaths)) {
          this.LogMessage("Compilation is up to date");
        } else {
          CompilerResults results =
          specSharpCompiler.CompileAssemblyFromFileBatch(options, sourceFilePaths, errors, false);
          this.LogErrorsAndWarnings(errors, options);
          if (results == null || results.NativeCompilerReturnValue > 0) return false;
        }
        return true;
      } catch (Exception e) {
        this.Log.LogErrorFromException(e);
        return false;
      }
    }

    private bool CompilationOutputIsUpToDate(SpecSharpCompilerOptions options, string[] sourceFilePaths) {
      string targetPath = Path.Combine(options.OutputPath, options.OutputAssembly);
      if (!File.Exists(targetPath) || !File.Exists(options.ShadowedAssembly)) return false;
      DateTime outputTimeStamp = File.GetLastWriteTime(targetPath);
      if (File.GetLastWriteTime(options.ShadowedAssembly) > outputTimeStamp) return false;
      foreach (string sourceFilePath in sourceFilePaths) {
        if (!File.Exists(sourceFilePath)) return false;
        if (File.GetLastWriteTime(sourceFilePath) > outputTimeStamp) return false;
      }
      if (options.ReferencedAssemblies != null) {
        foreach (string refPath in options.ReferencedAssemblies) {
          if (!File.Exists(refPath)) return false;
          if (File.GetLastWriteTime(refPath) > outputTimeStamp) return false;
        }
      }
      if (options.ReferencedModules != null) {
        foreach (string refPath in options.ReferencedModules) {
          if (!File.Exists(refPath)) return false;
          if (File.GetLastWriteTime(refPath) > outputTimeStamp) return false;
        }
      }
      return true;
    }

    /// <summary>
    /// Allocates new SpecSharpCompilerOptions object and initializes it using the values of the properties that were initialized
    /// by MsBuild. The resulting object is used to present a parsed command line to the compiler. The compiler itself is used
    /// to parse any response files.
    /// </summary>
    /// <returns></returns>
    private SpecSharpCompilerOptions/*!*/ GetCompilerOptions(Compiler/*!*/ specSharpCompiler)
    {
      SpecSharpCompilerOptions options = new SpecSharpCompilerOptions();
      if (this.AdditionalLibPaths != null)
        options.AdditionalSearchPaths = new Cci.StringList(this.AdditionalLibPaths);
      options.AllowUnsafeCode = this.AllowUnsafeBlocks;
      options.AssemblyKeyFile = this.KeyFile;
      options.AssemblyKeyName = this.KeyContainer;
      options.BaseAddress = this.GetBaseAddress(specSharpCompiler);
      if (this.ProgramVerifierCommandLineOptions == null)
        options.ProgramVerifierCommandLineOptions = new System.Compiler.StringList(0);
      else
        options.ProgramVerifierCommandLineOptions = new System.Compiler.StringList(this.ProgramVerifierCommandLineOptions.Split(' '));
      options.BugReportFileName = this.ErrorReport;
      options.CheckedArithmetic = this.CheckForOverflowUnderflow;
      options.OnlyTypeChecks = this.OnlyTypeChecks;
      options.CodePage = this.CodePage;
#if NoVS
      options.Site = new BuildSite(this.Log);
#else    
      options.Site = new BuildSite(this.taskManager, this.Log);
#endif
      options.DefinedPreProcessorSymbols = this.GetDefinedPreProcessorSymbols(specSharpCompiler);
      options.DelaySign = this.DelaySign;
      this.DisableContractFeatures(options, this.DisabledContractFeatures);
      this.PopulateResourceStringCollection(options.EmbeddedResources, this.Resources);
      options.EmitManifest = string.Compare(this.TargetType, "module", true, CultureInfo.InvariantCulture) != 0;
      options.EncodeOutputInUTF8 = this.Utf8Output;
      options.FileAlignment = this.FileAlignment;
      options.FullyQualifyPaths = this.GenerateFullPaths;
      options.GenerateExecutable = options.EmitManifest && string.Compare(this.TargetType, "library", true, CultureInfo.InvariantCulture) != 0;
      options.IncludeDebugInformation = this.EmitDebugInformation;
      options.LanguageVersion = this.GetLanguageVersion(specSharpCompiler);
      this.PopulateResourceStringCollection(options.LinkedResources, this.LinkResources);
      options.MainClass = this.MainEntryPoint;
      options.ModuleKind = this.GetModuleKind();
      options.UseStandardConfigFile = !this.NoConfig;
      options.NoStandardLibrary = this.NoStandardLib;
      options.Optimize = this.Optimize;
      options.ShadowedAssembly = this.OriginalAssembly;
      if (options.ShadowedAssembly != null && options.ShadowedAssembly.Length > 0)
        options.IsContractAssembly = true;
      string outputAssembly = this.OutputAssembly;
      if (outputAssembly != null && outputAssembly.Length > 0) {
        options.ExplicitOutputExtension = Path.GetExtension(this.OutputAssembly);
        options.OutputAssembly = Path.GetFileName(this.OutputAssembly);
        options.OutputPath = Path.GetDirectoryName(this.OutputAssembly);
        if (this.ProduceContractAssembly) {
          options.IsContractAssembly = false;
          options.ShadowedAssembly = Path.Combine(options.OutputPath, options.OutputAssembly);
          options.OutputAssembly = Path.GetFileNameWithoutExtension(this.OutputAssembly) + ".Contracts" + Path.GetExtension(this.OutputAssembly);
          this.OutputAssembly = Path.Combine(options.OutputPath, options.OutputAssembly);
        }
      }
      options.PDBOnly = string.Compare(this.DebugType, "pdbonly", true, CultureInfo.InvariantCulture) == 0;
      this.PopulateReferencesCollection(options);
      if (this.AddModules != null)
        options.ReferencedModules = new Cci.StringList(this.AddModules);
      options.ReferenceTypesAreNonNullByDefault = this.ReferenceTypesAreNonNullByDefault;
      options.CheckContractAdmissibility = this.CheckContractAdmissibility;
      options.CheckPurity = this.CheckPurity;
      options.RootNamespace = null;
      options.RunProgramVerifier = this.Verify;
      options.SpecificWarningsToTreatAsErrors = this.GetIntList(this.WarningsAsErrors);
      options.SpecificWarningsNotToTreatAsErrors = this.GetIntList(this.WarningsNotAsErrors);
      options.StandardLibraryLocation = this.StandardLibraryLocation;
      options.SuppressedWarnings = this.GetIntList(this.DisabledWarnings);
      options.SuppressLogo = this.NoLogo;
      options.TargetPlatform = this.GetRuntimeType();
      options.TargetPlatformLocation = this.TargetRuntimeLocation;
      options.TargetProcessor = this.GetProcessorType(specSharpCompiler);
      options.TreatWarningsAsErrors = this.TreatWarningsAsErrors;
      options.UserToken = IntPtr.Zero;
      options.WarningLevel = this.WarningLevel;
      options.Win32Icon = this.Win32Icon;
      options.Win32Resource = this.Win32Resource;
      options.XMLDocFileName = this.DocumentationFile;

      //TODO: deal with response files. Use the compiler object passed in as parameter.
      //TODO: what about the standard response files?
      return options;
    }

    private void DisableContractFeatures(SpecSharpCompilerOptions options, string p) {
      if (p == null) return;
      string[] disabledFeatures = p.Split(';');
      foreach (string feature in disabledFeatures) {
        switch (feature) {
          case "ac":
          case "assumechecks":
            options.DisableAssumeChecks = true;
            break;
          case "dc":
          case "defensivechecks":
            options.DisableDefensiveChecks = true;
            break;
          case "gcc":
          case "guardedclasseschecks":
            options.DisableGuardedClassesChecks = true;
            break;
          case "ic":
          case "internalchecks":
            options.DisableInternalChecks = true;
            break;
          case "icm":
          case "internalcontractsmetadata":
            options.DisableInternalContractsMetadata = true;
            break;
          case "pcm":
          case "publiccontractsmetadata":
            options.DisablePublicContractsMetadata = true;
            break;
          default:
            break;
        }
      }
    }

    /// <summary>
    /// Converts the string in this.BaseAddress to a long integer. Returns 0x400000 if the string is empty.
    /// Corresponds to /baseaddress:&lt;address&gt; on the command line.
    /// </summary>
    private long GetBaseAddress(Compiler/*!*/ specSharpCompiler)
    {
      string baseAddress = this.BaseAddress;
      if (baseAddress != null) baseAddress = baseAddress.Trim();
      if (baseAddress == null || baseAddress.Length == 0) return 0x400000;
#if WHIDBEY
      long result;
      if (long.TryParse(baseAddress, out result)) return result;
      //TODO: log error. Use compiler to get localized error message.
      return 0x400000;
#else
      try {
        return long.Parse(baseAddress);
      }
      catch {
        //TODO: log error. Use compiler to get localized error message.
        return 0x400000;
      }
#endif
    }

    /// <summary>
    /// Returns a list of strings corresponding to defined pre processor symbols.
    /// Extracted from value of this.DefineConstants, which correponds to /define:&lt;symbol list&gt; on the command line.
    /// </summary>
    private Cci.StringList GetDefinedPreProcessorSymbols(Compiler/*!*/ specSharpCompiler)
    {
      Cci.StringList result = new Cci.StringList();
      result.Add("SPECSHARP");
      string definedSymbols = this.DefineConstants;
      if (definedSymbols != null) definedSymbols = definedSymbols.Trim();
      if (definedSymbols == null || definedSymbols.Length == 0) return result;
      ICodeGenerator validator = (ICodeGenerator)specSharpCompiler;
      string[] symbols = definedSymbols.Split(',', ';', ' ');
      foreach (string symbol in symbols) {
        if (symbol == null || symbol.Length == 0) continue;
        if (validator.IsValidIdentifier(symbol))
          result.Add(symbol);
        //else
        //  this.Log.LogWarningWithCodeFromResources("Csc.InvalidParameterWarning", "/define:", singleIdentifier);
        //TODO: use the compiler to get a localized message
      }
      if (result.Count == 0) return null;
      return result;
    }

    /// <summary>
    /// Returns a list of integers corresponding the given string. Splits the given string on , ; and space.
    /// </summary>
    private Cci.Int32List GetIntList(string list)
    {
      if (list != null) list = list.Trim();
      if (list == null || list.Length == 0) return null;
      Cci.Int32List result = new Cci.Int32List();
      string[] intStrings = list.Split(',', ';', ' ');
      foreach (string intString in intStrings) {
        if (intString == null || intString.Length == 0) continue;
        try {
          result.Add(int.Parse(intString));
        }
        catch {
          //TODO: log an error about the bad string.
          //TODO: need a parameter that will identify the option.
        }
      }
      return result;
    }

    /// <summary>
    /// Maps the string in this.LangVersion to a value of the LanguageVersionType enumeration.
    /// </summary>
    private LanguageVersionType GetLanguageVersion(Compiler/*!*/ specSharpCompiler)
    {
      string langVersion = this.LangVersion;
      if (langVersion != null) langVersion = langVersion.Trim();
      if (langVersion == null || langVersion.Length == 0) return LanguageVersionType.Default;
      if (string.Compare(langVersion, "c#v2", true, CultureInfo.InvariantCulture) == 0) return LanguageVersionType.CSharpVersion2;
      if (string.Compare(langVersion, "default", true, CultureInfo.InvariantCulture) == 0) return LanguageVersionType.Default;
      if (string.Compare(langVersion, "ISO_1", true, CultureInfo.InvariantCulture) == 0) return LanguageVersionType.ISO_1;
      //TODO: log message about invalid option in build file if the string is not empty/blank
      return LanguageVersionType.Default;
    }

    /// <summary>
    /// Maps the string in this.TargetType to a value of the ModuleKindFlags enumeration.
    /// </summary>
    private Cci.ModuleKindFlags GetModuleKind()
    {
      string targetType = this.TargetType;
      if (targetType == null || targetType.Length == 0) return Cci.ModuleKindFlags.ConsoleApplication;
      if (string.Compare(targetType, "library", true, CultureInfo.InvariantCulture) == 0) return Cci.ModuleKindFlags.DynamicallyLinkedLibrary;
      if (string.Compare(targetType, "module", true, CultureInfo.InvariantCulture) == 0) return Cci.ModuleKindFlags.DynamicallyLinkedLibrary;
      if (string.Compare(targetType, "winexe", true, CultureInfo.InvariantCulture) == 0) return Cci.ModuleKindFlags.WindowsApplication;
      if (string.Compare(targetType, "exe", true, CultureInfo.InvariantCulture) == 0) return Cci.ModuleKindFlags.ConsoleApplication;
      //TODO: log message about invalid option in build file
      return Cci.ModuleKindFlags.ConsoleApplication;
    }

    /// <summary>
    /// Maps the string in this.Platform to a value of the ProcessorType enumeration.
    /// </summary>
    private Cci.ProcessorType GetProcessorType(Compiler/*!*/ specSharpCompiler)
    {
      string processorType = this.Platform;
      if (processorType != null) processorType = processorType.Trim();
      if (processorType == null || processorType.Length == 0) return Cci.ProcessorType.Any;
      if (string.Compare(processorType, "x86", true, CultureInfo.InvariantCulture) == 0) return Cci.ProcessorType.x86;
      if (string.Compare(processorType, "Itanium", true, CultureInfo.InvariantCulture) == 0) return Cci.ProcessorType.Itanium;
      if (string.Compare(processorType, "x64", true, CultureInfo.InvariantCulture) == 0) return Cci.ProcessorType.x64;
      if (string.Compare(processorType, "anycpu", true, CultureInfo.InvariantCulture) == 0) return Cci.ProcessorType.Any;
      //TODO: log message about invalid option in build file if the string is not empty/blank
      return Cci.ProcessorType.Any;
    }

    /// <summary>
    /// Maps the string in this.TargetRuntimeType to a value of the PatformType enumeration.
    /// </summary>
    private Cci.PlatformType GetRuntimeType()
    {
      string targetType = this.TargetRuntime;
      if (targetType != null) targetType = targetType.Trim();
      if (targetType == null || targetType.Length == 0) return Cci.PlatformType.notSpecified;
      if (string.Compare(targetType, "v1", true, CultureInfo.InvariantCulture) == 0) return Cci.PlatformType.v1;
      if (string.Compare(targetType, "v11", true, CultureInfo.InvariantCulture) == 0) return Cci.PlatformType.v11;
      if (string.Compare(targetType, "v2", true, CultureInfo.InvariantCulture) == 0) return Cci.PlatformType.v2;
      if (string.Compare(targetType, "cli1", true, CultureInfo.InvariantCulture) == 0) return Cci.PlatformType.cli1;
      //TODO: log message about invalid option in build file if the string is not empty/blank
      return Cci.PlatformType.notSpecified;
    }

    /// <summary>
    /// Return a non null, possibly empty array of strings corresponding to the ItemSpec property of each of the elements of the ITaskItem collection that
    /// is the value of the this.Sources property.
    /// </summary>
    private string[]/*!*/ GetSourceFilePaths()
    {
      ITaskItem[] sourcePathItems = this.Sources;
      if (sourcePathItems == null) return new string[0];
      int n = sourcePathItems.Length;
      string[] sourcePaths = new string[n];
      for (int i = 0; i < n; i++) {
        ITaskItem sourcePathItem = sourcePathItems[i];
        if (sourcePathItem == null) continue;
        sourcePaths[i] = sourcePathItem.ItemSpec;
      }
      return sourcePaths;
    }

    /// <summary>
    /// Populates the ReferencedAssemblies collection on the given options object with strings
    /// corresponding to the paths of the referenced assemblies. TODO: also populate a still to be 
    /// defined collection of aliases for the assemblies. (AliasesForReferencedAssemblies)
    /// </summary>
    private void PopulateReferencesCollection(SpecSharpCompilerOptions/*!*/ options)
    {
      ITaskItem[] taskItems = this.References;
      if (taskItems == null || taskItems.Length == 0) return;
      StringCollection referencedAssemblies = options.ReferencedAssemblies;
      //^ assume referencedAssemblies != null;
      StringCollection aliasesForReferencedAssemblies = options.AliasesForReferencedAssemblies;
      if (aliasesForReferencedAssemblies == null)
        aliasesForReferencedAssemblies = options.AliasesForReferencedAssemblies = new StringCollection();
      foreach (ITaskItem taskItem in taskItems) {
        if (taskItem == null) continue;
        string referencedAssemblyPath = taskItem.ItemSpec;
        referencedAssemblies.Add(referencedAssemblyPath);
        string aliases = taskItem.GetMetadata("Aliases");
        aliasesForReferencedAssemblies.Add(aliases);
      }
    }

    /// <summary>
    /// Populates the given string collection with strings
    /// corresponding to the given taskItem collection. Each of the resulting strings is concatenation of
    /// ItemSpec property of the corresponding task item, as well as the values of the LogicalName and Access attributes (if present).
    /// </summary>
    private void PopulateResourceStringCollection(System.Collections.Specialized.StringCollection collection, ITaskItem[] taskItems)
    {
      if (collection == null || taskItems == null || taskItems.Length == 0) return;
      foreach (ITaskItem taskItem in taskItems) {
        if (taskItem == null) continue;
        string resourceFilePath = taskItem.ItemSpec;
        string resourceName = taskItem.GetMetadata("LogicalName");
        string resourceAccess = taskItem.GetMetadata("Access");
        if (resourceName != null && resourceName.Length > 0) {
          resourceFilePath += "," + resourceName;
          if (resourceAccess != null && resourceAccess.Length > 0)
            resourceFilePath += "," + resourceAccess;
        }
        collection.Add(resourceFilePath);
      }
    }

    private void LogMessage(string message)
    {
#if !NoVS
      if (this.taskManager != null) {
        this.taskManager.OutputString(message + "\n", TaskOutputPane.Build);
      }else 
#endif
      if (Log != null) {
        Log.LogMessage(message, null);
      }      
    }

    private void LogRunSpecSharp(SpecSharpCompilerOptions options)
    {
      /* Show the commandline invokation in the build output pane */
      StringBuilder message = new StringBuilder();
      String[] sources = GetSourceFilePaths();
      message.Append("Ssc <options>");
      message.Append(" /out:");
      message.Append(options.OutputAssembly);
      if( this.OnlyTypeChecks )
        message.Append(" /onlychecks");
      foreach (String refassem in options.ReferencedAssemblies) {
        message.Append(" /r:");
        message.Append(refassem);
      }
      foreach (String source in sources) {
        message.Append(" ");
        message.Append(source);
      }
      LogMessage(message.ToString());
    }

    private void LogTaskError(string subCategory, int code, string helpKeyword
                             , string fileName, string fullPath
                             , int startLine, int startColumn, int endLine, int endColumn
                             , string description
                             , bool warning, bool isSpecSharpSpecific )
    {     
#if !NoVS
      if (this.taskManager != null && 
          (isSpecSharpSpecific || code < this.ContractsHideCSharpErrors))
      {
        /* show a full task item for this error */
        string help = (helpKeyword != null ? helpKeyword : subCategory + code.ToString());
        TaskCategory category = (warning ? TaskCategory.Warning : TaskCategory.Error);
        TaskMarker marker = ((warning | isSpecSharpSpecific) ? TaskMarker.Other : TaskMarker.Error);
        TaskPriority priority = warning ? TaskPriority.Normal : TaskPriority.High;

        this.taskManager.AddTask(
            description, null,
            subCategory + code.ToString(), help,
            priority, category, marker, TaskOutputPane.Build,
            projectName, fullPath,
            startLine, startColumn, endLine, endColumn,
            null // new TaskCommand(code % 2 == 1) 
            );
      }else 
#endif
      if (Log != null) {
        if (this.UseLogMessage) {
          string message = fullPath +
                            "(" + startLine.ToString() +
                            "," + startColumn.ToString() +
                            "): " + (warning ? "warning " : "error ") +
                            subCategory + code.ToString() +
                            ": " + description;
          LogMessage(message);
        } else {
          if (warning)
            Log.LogWarning(subCategory, "SS" + code.ToString("D4"), helpKeyword, fileName,
                            startLine, startColumn, endLine, endColumn,
                            description, null);
          else
            Log.LogError(subCategory, "SS" + code.ToString("D4"), helpKeyword, fileName,
                          startLine, startColumn, endLine, endColumn,
                          description, null);
        }
      }
    }

    private void LogErrorsAndWarnings(Cci.ErrorNodeList/*!*/ errors, SpecSharpCompilerOptions/*!*/ options)
    {
      int errorCount = 0;
      int warningCount = 0;
      int prevSev = 0;
      int n = errors.Count;
      if (n > 100) n = 100;
      for (int i = 0; i < n; i++) {
        Cci.ErrorNode error = errors[i];
        if (error != null) {
          int suppressedWarningSeverity = this.GetLevelAtWhichWarningsAreSuppressed(error, options);
          int errorSeverity = (options.TreatWarningsAsErrors ?
                                suppressedWarningSeverity - 1 :
                                this.GetSeverityAtWhichWarningsBecomeErrors(error, options));
          int severity = error.Severity;
          if (severity < 0)
            severity = prevSev;
          else
            prevSev = severity;
          bool isWarning = severity > errorSeverity;        
          bool isSpecSharpSpecific = error.Code >= 2500 && error.Code < 3000;
          if (!isWarning || severity < suppressedWarningSeverity) {
            LogTaskError("CS", error.Code, null,
              this.GetRelativePath(error.SourceContext.Document),
              this.GetFullPath(error.SourceContext.Document),
              error.SourceContext.StartLine, error.SourceContext.StartColumn,
              error.SourceContext.EndLine, error.SourceContext.EndColumn,
              error.GetMessage(CultureInfo.CurrentCulture), 
              isWarning, isSpecSharpSpecific );
          }
          if (isWarning)
            warningCount++;
          else
            errorCount++;
        }
      }
      LogMessage("Compile complete -- " + 
                 errorCount.ToString() + " errors, " +
                 warningCount.ToString() + " warnings\n" 
                ); 
    }

    private String GetFullPath(Cci.Document document)
    {
      if (document != null && document.Name != null) {
        string relativePath = document.Name.Trim();
        if (relativePath.Length > 0) {
          string fullPath = Path.GetFullPath(relativePath);
          if (fullPath != null) {
            return fullPath;
          }
        }
      }
      return "";
    }

    /// <summary>
    /// Returns a path to the given document that is relative to the current directory. Returns an absolute path if the path
    /// to the given document does not share a common prefix with the current directory.
    /// </summary>
    private string GetRelativePath(Cci.Document document)
    {
      if (document == null) return "";
      string relativePath = document.Name;
      if (relativePath != null) relativePath = relativePath.Trim();
      if (relativePath == null || relativePath.Length == 0) return null;
      string fullPath = Path.GetFullPath(relativePath);
      //^ assume fullPath != null;
      string currentDir = Directory.GetCurrentDirectory();
      //^ assume currentDir != null && currentDir.Length > 0;


      //TODO: get the length of the longest common prefix
      //if this is shorter than currentDir, compute the number of directories to go up
      //and append ..\ sequences to relative path.

      //TODO: put this logic in the compiler and just reuse it here.


      if (fullPath.Length <= currentDir.Length)
        return fullPath;
      //TODO: make relative if fullPath and currentDir share a common prefix
      System.Globalization.CultureInfo invariantCulture = System.Globalization.CultureInfo.InvariantCulture;
      //^ assume invariantCulture != null;
      System.Globalization.CompareInfo compInfo = invariantCulture.CompareInfo;
      //^ assume compInfo != null;
      if (compInfo.IsPrefix(fullPath, currentDir, System.Globalization.CompareOptions.IgnoreCase))
        return fullPath.Substring(currentDir.Length + 1);
      return relativePath;
    }

    private int GetSeverityAtWhichWarningsBecomeErrors(Cci.ErrorNode/*!*/ error, SpecSharpCompilerOptions/*!*/ options)
    {
      int severity = error.Severity;
      if (severity == 0 || options.TreatWarningsAsErrors) return severity;
      int errorCode = error.Code;
      Cci.Int32List severeWarnings = options.SpecificWarningsToTreatAsErrors;
      for (int i = 0, n = severeWarnings == null ? 0 : severeWarnings.Count; i < n; i++) {
        if (severeWarnings[i] == errorCode) return severity;
      }
      return 0;
    }

    private int GetLevelAtWhichWarningsAreSuppressed(Cci.ErrorNode/*!*/ error, SpecSharpCompilerOptions/*!*/ options)
    {
      int severity = error.Severity;
      int errorCode = error.Code;
      Cci.Int32List suppressedWarnings = options.SuppressedWarnings;
      for (int i = 0, n = suppressedWarnings == null ? 0 : suppressedWarnings.Count; i < n; i++) {
        if (suppressedWarnings[i] == errorCode) return severity;
      }
      return options.WarningLevel + 1;
    }
  }

  /*
   * The following class is an example of how to attach context menu
   * commands to a task item. They are just here to show the capabilities
   * of menus and submenus.
  */
  /*
  class TaskCommand : Microsoft.VisualStudio.CodeTools.ITaskCommands
  {
    #region ITaskCommands Members

    private static ITaskMenuItem
      menuCheckable = new TaskMenuItem("Checkable", TaskMenuKind.Checkable),
      menuDelete = new TaskMenuItem("Delete?", TaskMenuKind.Normal),
      menuEven = new TaskMenuItem("Even", TaskMenuKind.Normal),
      menuOdd = new TaskMenuItem("Odd", TaskMenuKind.Normal),
      menuSub1 = new TaskMenuItem("SubMenu1", TaskMenuKind.SubMenu),
      menuSub2 = new TaskMenuItem("SubMenu2", TaskMenuKind.SubMenu),
      menuSubSub = new TaskMenuItem("SubSubMenu", TaskMenuKind.SubMenu);

    private static ITaskMenuItem[]
      groupEven = { menuCheckable, menuDelete, menuEven, menuSub1, menuSub2 },
      groupOdd = { menuCheckable, menuDelete, menuEven, menuOdd, menuSub1 },
      groupSub1 = { menuDelete, menuSubSub },
      groupSub2 = { menuCheckable, menuDelete },
      groupSubSub = { menuCheckable, menuDelete };

    private bool odd;
    private bool isChecked;

    public TaskCommand(bool isOdd )
    {
      odd = isOdd;
      isChecked = false;
    }

    public ITaskMenuItem[] GetMenuItems( ITaskMenuItem parent ) {
      if (parent ==null)
        return (odd ? groupOdd : groupEven);
      else if (parent == menuSub1) {
        return groupSub1;
      }
      else if (parent == menuSub2 ) {
        return groupSub2;
      }
      else if (parent == menuSubSub ) {
        return groupSubSub;
      }
      else {
        return null;
      }
    }

    public bool IsChecked(ITaskMenuItem menuItem)
    {
      Debug.Assert(menuItem.Kind == TaskMenuKind.Checkable);
      return isChecked;
    }

    public bool IsEnabled(ITaskMenuItem menuItem)
    {
      if (menuItem == menuOdd)
        return odd;
      else if (menuItem == menuEven)
        return !odd;
      else
        return true;
    }

    public void OnContextMenu(Microsoft.VisualStudio.CodeTools.ITask task, ITaskMenuItem menuItem)
    {
      Trace.WriteLine("Contracts menu: " + menuItem.Caption);
      if (task != null) {
        if (menuItem == menuDelete) {
          task.Remove();
        }
        else if (menuItem == menuCheckable) {
          isChecked = task.Checked = !task.Checked;
          if (task.Checked) {
            task.Marker = TaskMarker.Invisible;
          }
          else {
            task.Marker = TaskMarker.Error;
          }
        }
      }
    }

    public void OnDoubleClick(Microsoft.VisualStudio.CodeTools.ITask task)
    {
      Trace.WriteLine("Contracts on double click");      
    }

    public void OnHover(Microsoft.VisualStudio.CodeTools.ITask task)
    {
      Trace.WriteLine("Contracts on hover");
    }

    #endregion

    #region IDisposable Members

    public void Dispose()
    {
      return;
    }

    #endregion
  }
  */
}
