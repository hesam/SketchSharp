//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
#if !NoVS
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.Win32;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Windows.Forms;

#if CCINamespace
using Cci = Microsoft.Cci;
#else
using Cci = System.Compiler;
#endif

namespace Microsoft.SpecSharp{
  [ComVisible(true), GuidAttribute("A0860577-9EF7-4da2-9925-AB85230E8A5A")]
  public sealed class Registrar : RegistrationInfo{
    bool register;
    public Registrar(){
      this.DebuggerEEGuid = "{"+typeof(DebuggerEE).GUID+"}";
      this.DebuggerLanguageGuid = "{"+typeof(DebuggerLanguage).GUID+"}";
      this.LanguageServiceType = typeof(VsLanguageService);
      this.LanguageName = "Microsoft Spec#";
      this.LanguageShortName = "Spec#";
      this.SourceFileExtension = "ssc"; 
      this.UseStockColors = false;
      this.CodeSenseDelay = 1000;
      this.Win32ResourcesDllPath = Path.GetDirectoryName(this.GetType().Assembly.Location)+Path.DirectorySeparatorChar+
        "1033"+Path.DirectorySeparatorChar+"Microsoft.SpecSharp.Resources.dll";
      this.CodeDomProvider = typeof(SpecSharpCodeProvider);
      this.ProjectFactoryType = typeof(ProjectFactory);
      this.Project = typeof(ProjectManager);
      this.ASPExtensionAliases = "ssc;specsharp";
      this.TemplateDirectory = Directory.GetParent(this.GetType().Module.Assembly.Location).Parent.FullName+"\\Templates\\";

      this.PackageType = typeof(Package);

      // NOTE: the following settings must not be changed as they have been
      // encoded into the encrypted package load key for the language package
      // along with the assembly names and MinEdition='standard'.
      this.CompanyName = "Microsoft";
      this.ProductName = "Microsoft Spec#";
      this.ProductVersion = "1.0";
      this.ProductShortName = "Spec#";
      this.ProjectFileExtension = "sscproj"; 

      // Editor
      this.EditorFactoryType = typeof(EditorFactory);
      this.EditorName = "Spec#"; 
    }
    public Registrar(bool register) : this(){
      this.register = register;
    }
    [ComRegisterFunction]
    public static void RegisterFunction(Type t){
      if (t != typeof(Registrar)) return;
      (new Registrar(true)).Register();
    }        
    [ComUnregisterFunction]
    public static void UnregisterFunction(Type t){
      if (t != typeof(Registrar)) return;
      (new Registrar(false)).Unregister();
    }
    public override string GetRegistrationScript(){
#if WHIDBEY
      string packageScript = GetResourceAsString(this.GetType(), "Microsoft.SpecSharp.Install.rgs");
#else
      string packageScript = GetResourceAsString(this.GetType(), "Microsoft.SpecSharp.Install7.rgs");
#endif
      return packageScript;
    }

#if Exp
    public override string GetCurrentVisualStudioVersion(){
      return base.GetCurrentVisualStudioVersion("Exp");
    }
#endif

    public override NameValueCollection GetRegistrationScriptArguments(){
      NameValueCollection args = base.GetRegistrationScriptArguments();
      args.Add("FxCopPackage", "{72391CE3-743A-4a55-8927-4217541F6517}");
      return args;
    }
	}

  [ComVisible(true), Guid("83043D9C-4AAE-4c70-9C52-8AFD6B388CCA")]
  public class Package : Microsoft.VisualStudio.Shell.Package{

    public override Microsoft.VisualStudio.Package.EditorFactory CreateEditorFactory(){
      return new EditorFactory(this);      
    }
    private VsLanguageService specSharpVsLanguageService;
    public override ILanguageService CreateLanguageService(ref Guid guid){
      if (this.specSharpVsLanguageService == null)
        this.specSharpVsLanguageService = new VsLanguageService();
#if Xaml
      if (guid == typeof(Microsoft.XamlCompiler.VsLanguageService).GUID)
        return new Microsoft.XamlCompiler.VsLanguageService(this.specSharpVsLanguageService);
#endif
      return this.specSharpVsLanguageService;
    }
    public override Microsoft.VisualStudio.Package.ProjectFactory CreateProjectFactory(){
      return new ProjectFactory();
    }
  }

  [ComVisible(true), GuidAttribute("A21423E1-6A90-4954-9FB4-9EA1657BA0DE")]
  public class EditorFactory : Microsoft.VisualStudio.Package.EditorFactory{
    
    public EditorFactory(Package package)
      : base (package){
    }
  }
  [ComVisible(true), GuidAttribute("9674DDF0-A005-41f6-8E90-4388B15E9D3F")]
  public sealed class VsLanguageService : Microsoft.VisualStudio.IntegrationHelper.LanguageService{      
    public VsLanguageService()
      : base(new LanguageService(), null, new GlyphProvider()){
      this.scLanguageService.GetCompilationFor = new Cci.LanguageService.GetCompilation(this.GetCompilationFor);
    }
    public override int GetColorableItem(int index, out IVsColorableItem item){
      SpecSharpTokenColor xtc = (SpecSharpTokenColor)index;
      string description = xtc.ToString();      
      item = new ColorableItem(description, VsLanguageService.colors[index], COLORINDEX.CI_USERTEXT_BK, FONTFLAGS.FF_DEFAULT);
      return 0;
    }
    private Cci.Compilation GetCompilationFor(string fileName){
      ProjectManager projectManager = (ProjectManager)ProjectManager.For(fileName);
      if (projectManager != null) return projectManager.GetCompilerProject().Compilation;
      return this.scLanguageService.GetDummyCompilationFor(fileName);
    }
    public override IScanner GetScanner(string fileName){
      Cci.Compilation compilation = this.GetCompilationFor(fileName);
      return new VsScanner(compilation.CompilerParameters as SpecSharpCompilerOptions);
    }
    public override void Init(ServiceProvider site, ref Guid languageGuid, uint lcid, string extensions){
      base.Init(site, ref languageGuid, lcid, extensions);
      this.scLanguageService.culture = this.culture;
      this.vsLanguagePreferences = new SpecSharpLanguagePreferences(site, typeof(VsLanguageService).GUID, this.LanguageShortName);
      this.EnableDropDownCombos = true;
    }
    public override string LanguageShortName{
      get{
        return "Spec#";
      }
    }
    public sealed class SpecSharpLanguagePreferences : LanguagePreferences{
      public SpecSharpLanguagePreferences(ServiceProvider site, Guid guid, string editorName) 
        : base(site, guid, editorName){
      }
    }
    static COLORINDEX[] colors = new COLORINDEX[(int)SpecSharpTokenColor.LastColor + 1] {
      COLORINDEX.CI_USERTEXT_FG,  // text
      COLORINDEX.CI_BLUE,         // keyword
      COLORINDEX.CI_DARKGREEN,    // comment
      COLORINDEX.CI_USERTEXT_FG,  // identifier
      COLORINDEX.CI_MAROON,       // string
      COLORINDEX.CI_BLACK,        // number
      COLORINDEX.CI_MAROON, 
      COLORINDEX.CI_RED,
      COLORINDEX.CI_DARKGRAY,
      COLORINDEX.CI_DARKGRAY,
      COLORINDEX.CI_MAGENTA
    };
  }


  [Guid("07C4E3D1-6B67-4060-8A92-940DB82041ED")]
  public class ProjectFactory : Microsoft.VisualStudio.Package.ProjectFactory{     
    protected override Microsoft.VisualStudio.Package.Project CreateProject(){
      return new ProjectManager();
    }
  }
  [Guid("07842A71-F068-473d-A2C3-683FBCA7E877")]
  internal class ProjectManager : Microsoft.VisualStudio.IntegrationHelper.ProjectManager{ 

    internal ProjectManager(){
      Cci.TargetPlatform.DoNotLockFiles = true;
      this.ImageList = new Microsoft.VisualStudio.Package.ImageList();
      this.ImageList.AddImages("Microsoft.SpecSharp.Folders.bmp", 
        typeof(ProjectManager).Assembly, 7, 16, 16, System.Drawing.Color.Magenta);
      this.compiler = new Compiler();
      this.compiler.AssemblyCache = Cci.TargetPlatform.StaticAssemblyCache;
    }

    public override XmlElement GetProjectStateElement(XmlElement project){
      return (XmlElement)project.SelectSingleNode("*");
    }
    public override bool IsCodeFile(string strFileName){
      string strExt = Path.GetExtension(strFileName).ToLower();
#if Xaml
      if (strExt == ".xaml") return true;
#endif
      if (strExt == ".ssc" || strExt == ".cs") return true;
      return false;  
    }
    public override ProjectOptions CreateProjectOptions(){
      return new SpecSharpProjectOptions();
    }
    public override ProjectOptions GetProjectOptions(XmlElement config){
      ProjectOptions result = base.GetProjectOptions(config);
      SpecSharpProjectOptions options = result as SpecSharpProjectOptions;
      if (options != null && config != null){
        options.ReferenceTypesAreNonNullByDefault = this.GetBoolAttr(config, "ReferenceTypesAreNonNullByDefault");
        options.RunProgramVerifier = this.GetBoolAttr(config, "RunProgramVerifier");
        options.RunProgramVerifierWhileEditing = this.GetBoolAttr(config, "RunProgramVerifierWhileEditing");
        options.ProgramVerifierCommandLineOptions = config.GetAttribute("ProgramVerifierCommandLineOptions");
        // begin change by drunje
        options.AllowPointersToManagedStructures = this.GetBoolAttr(config, "AllowPointersToManagedStructures");
        // end change by drunje
        options.CheckContractAccessibility = this.GetBoolAttr(config, "CheckContractAdmissibility");
        options.CheckPurity = this.GetBoolAttr(config, "CheckPurity");
      }else
        Debug.Assert(false);
      return result;
    }
    public override Cci.CompilationUnitSnippet GetCompilationUnitSnippetFor(string fullPathToSource, string buildAction){
      if (fullPathToSource == null || !File.Exists(fullPathToSource)){Debug.Assert(false); return null;}
      StreamReader sr = new StreamReader(fullPathToSource);
      Cci.DocumentText docText = new Cci.DocumentText(new Cci.StringSourceText(sr.ReadToEnd(), true));
      sr.Close();
      Cci.Document doc;
      Cci.IParserFactory parserFactory;
      if (string.Compare(buildAction, "Compile", true, System.Globalization.CultureInfo.InvariantCulture) == 0){
#if Xaml
        if (string.Compare(Path.GetExtension(fullPathToSource), ".xaml", true, System.Globalization.CultureInfo.InvariantCulture) == 0){
          doc = Microsoft.XamlCompiler.Compiler.CreateXamlDocument(fullPathToSource, 1, docText);
          parserFactory = new XamlParserFactory();
        }else{
#endif
          doc = Compiler.CreateSpecSharpDocument(fullPathToSource, 1, docText);
          parserFactory = new ParserFactory();
#if Xaml
        }
#endif
      }else if (string.Compare(buildAction, "EmbeddedResource", true, System.Globalization.CultureInfo.InvariantCulture) == 0){
        if (string.Compare(Path.GetExtension(fullPathToSource), ".resx", true, System.Globalization.CultureInfo.InvariantCulture) == 0 ||
          string.Compare(Path.GetExtension(fullPathToSource), ".txt", true, System.Globalization.CultureInfo.InvariantCulture) == 0){
            RegistrationInfo regInfo = new RegistrationInfo();
            string resgenPath = regInfo.GetResgenPath(); 
            if (resgenPath == null) return null;  
            parserFactory = new Cci.ResgenFactory(resgenPath);
            doc = Compiler.CreateSpecSharpDocument(fullPathToSource, 1, docText);
        }else
          return null;
      }else{
        return null;
      }
      if (doc == null) return null;
      return new Cci.CompilationUnitSnippet(new Cci.Identifier(fullPathToSource), parserFactory, new Cci.SourceContext(doc));
    }
    public override Cci.CompilerOptions GetCompilerOptions(Microsoft.VisualStudio.Package.ProjectOptions projectOptions){
      Cci.CompilerOptions options;
      SpecSharpProjectOptions specSharpOptions = projectOptions as SpecSharpProjectOptions;
      if (specSharpOptions != null)
        options = specSharpOptions.GetCompilerOptions();
      else
        options = base.GetCompilerOptions(projectOptions);
      if (this.BuildSite != null)
        options.Site = new Microsoft.VisualStudio.IntegrationHelper.BuildSiteAdaptor(this.BuildSite);
      return options;
    }
    public override void RefreshPerBuildCompilerParameters()
    {
      Cci.CompilerOptions options = this.scProject.CompilerParameters as Cci.CompilerOptions;
      if (options != null && this.BuildSite != null)
        options.Site = new Microsoft.VisualStudio.IntegrationHelper.BuildSiteAdaptor(this.BuildSite);
    }
    public override Guid GetProjectGuid(){
      return typeof(ProjectFactory).GUID;
    }
    public override string GetProjectType(){
      return "Microsoft SpecSharp";
    }
    public override string GetFullyQualifiedNameForReferencedLibrary(ProjectOptions options, string rLibraryName){
      return this.compiler.GetFullyQualifiedNameForReferencedLibrary(
        new Microsoft.VisualStudio.IntegrationHelper.CompilerOptions(options), rLibraryName);
    }
    public override System.Collections.ArrayList GetRuntimeLibraries() {
      return this.compiler.GetRuntimeLibraries();
    }
    private DateTime lastModifiedTimeOfPlatform = DateTime.MinValue;
    public override void SetTargetPlatform(ProjectOptions options){
      if (this.LastModifiedTime == this.lastModifiedTimeOfPlatform) return;
      //TODO: what about referenced projects with different target platforms? This should probably be a mistake and should generate an error.
      this.lastModifiedTimeOfPlatform = this.LastModifiedTime;
      //TODO: handle errors caused by bad path names and/or missing platform assemblies
      switch (options.TargetPlatform){
        case PlatformType.v1: Microsoft.SpecSharp.TargetPlatform.SetToV1(options.TargetPlatformLocation); break;
        case PlatformType.v11: Microsoft.SpecSharp.TargetPlatform.SetToV1_1(options.TargetPlatformLocation); break;
        case PlatformType.v2: Microsoft.SpecSharp.TargetPlatform.SetToV2(options.TargetPlatformLocation); break;
        case PlatformType.cli1: Microsoft.SpecSharp.TargetPlatform.SetToPostV1_1(options.TargetPlatformLocation); break;
      }
    }
    public override void UpdateRuntimeAssemblyLocations(ProjectOptions options){
      this.compiler.UpdateRuntimeAssemblyLocations(this.GetCompilerOptions(options));
      //TODO: if the runtime assemblies have already been loaded, they need to get re-initialized.
      //However, this means that all projects must be marked dirty
//      Cci.SystemTypes.Initialize(true, false);
//      Cci.Runtime.Initialize();
//      Runtime.Initialize();
    }
    public override Guid GetBuildPropertyPageGuid(XmlElement config){
      return typeof(BuildPropertyPage).GUID;
    }
  }
  [ComVisible(true), Guid("5F6609AD-2EC5-4f95-856B-1C7716687512")]
  public class BuildPropertyPage : Microsoft.VisualStudio.Package.BuildPropertyPage{
    bool referenceTypesAreNonNullByDefault;
    bool runProgramVerifier;
    bool runProgramVerifierWhileEditing;
    // begin change by drunje
    bool allowPointersToManagedStructures;
    // end change by drunje
    bool checkContractAdmissibility = true;
    bool checkPurity;
    string programVerifierCommandLineOptions;

    public bool GetBooleanConfigProperty(string path){
      string text = GetConfigProperty(path);
      return text != null && text.Trim().ToLower(System.Globalization.CultureInfo.InvariantCulture) == "true";
    }

    public override void BindProperties(){
      base.BindProperties();
      referenceTypesAreNonNullByDefault = GetBooleanConfigProperty("@ReferenceTypesAreNonNullByDefault");
      runProgramVerifier = GetBooleanConfigProperty("@RunProgramVerifier");
      runProgramVerifierWhileEditing = GetBooleanConfigProperty("@RunProgramVerifierWhileEditing");
      programVerifierCommandLineOptions = GetConfigProperty("@ProgramVerifierCommandLineOptions");
      // begin change by drunje
      allowPointersToManagedStructures = GetBooleanConfigProperty("@AllowPointersToManagedStructures");
      // end change by drunje
      checkContractAdmissibility = GetBooleanConfigProperty("@CheckContractAdmissibility");
      checkPurity = GetBooleanConfigProperty("@CheckPurity");
    }

    public override void ApplyChanges(){
      base.ApplyChanges();
      SetConfigProperty("ReferenceTypesAreNonNullByDefault", referenceTypesAreNonNullByDefault.ToString());
      SetConfigProperty("RunProgramVerifier", runProgramVerifier.ToString());
      SetConfigProperty("RunProgramVerifierWhileEditing", runProgramVerifierWhileEditing.ToString());
      SetConfigProperty("ProgramVerifierCommandLineOptions", ProgramVerifierCommandLineOptions);
      // begin change by drunje
      SetConfigProperty("AllowPointersToManagedStructures", allowPointersToManagedStructures.ToString());
      // end change by drunje
      SetConfigProperty("CheckContractAdmissibility", this.checkContractAdmissibility.ToString());
      SetConfigProperty("CheckPurity", this.checkPurity.ToString());
    }

    [System.ComponentModel.Description("Treat all references types as non nullable by default.")]
    public bool ReferenceTypesAreNonNullByDefault {
      get {
        return referenceTypesAreNonNullByDefault;
      }
      set {
        referenceTypesAreNonNullByDefault = value; IsDirty = true;
      }
    }

    [System.ComponentModel.Description("Check contracts to make sure all member references are admissible.")]
    public bool CheckContractAdmissibility {
      get {
        return checkContractAdmissibility;
      }
      set {
        checkContractAdmissibility = value; IsDirty = true;
      }
    }
    [System.ComponentModel.Description("Check methods marked pure to ensure that they are.")]
    public bool CheckPurity {
      get {
        return checkPurity;
      }
      set {
        checkPurity= value; IsDirty = true;
      }
    }

    [System.ComponentModel.Description("Run the Spec# Program Verifier to verify exception safety and compliance with method contracts, object invariants, and in-line assertions.")]
    public bool RunProgramVerifier{
      get{
        return runProgramVerifier;
      }
      set{
        runProgramVerifier = value; IsDirty = true;
      }
    }
    [System.ComponentModel.Description("Run the Spec# Program Verifier while editing to verify exception safety and compliance with method contracts, object invariants, and in-line assertions.")]
    public bool RunProgramVerifierWhileEditing {
      get {
        return runProgramVerifierWhileEditing;
      }
      set {
        runProgramVerifierWhileEditing = value; IsDirty = true;
      }
    }
    // begin change by drunje
    [System.ComponentModel.Description("Allow pointers to structures containing managed objects.")]
    public bool AllowPointersToManagedStructures
    {
        get
        {
            return allowPointersToManagedStructures;
        }
        set
        {
            allowPointersToManagedStructures = value; IsDirty = true;
        }
    }
    // end change by drunje

    [System.ComponentModel.Description("Command Line Arguments to pass to the Spec# Program Verifier.")]
    public string ProgramVerifierCommandLineOptions {
      get {
        return programVerifierCommandLineOptions;
      }
      set {
        programVerifierCommandLineOptions = value; IsDirty = true;
      }
    }
  }
  internal class SpecSharpProjectOptions : Microsoft.VisualStudio.Package.ProjectOptions{
    internal bool Compatibility = false;
    internal bool ReferenceTypesAreNonNullByDefault = true;
    internal bool RunProgramVerifier = false;
    internal bool RunProgramVerifierWhileEditing = false;
    // begin change by drunje
    internal bool AllowPointersToManagedStructures = false;
    // end change by drunje
    internal bool CheckContractAccessibility = true;
    internal bool CheckPurity;
    internal string ProgramVerifierCommandLineOptions = null;
    internal Cci.CompilerOptions GetCompilerOptions(){
      SpecSharpCompilerOptions coptions = new SpecSharpCompilerOptions(new Microsoft.VisualStudio.IntegrationHelper.CompilerOptions(this));
      coptions.Compatibility = this.Compatibility;
      coptions.ReferenceTypesAreNonNullByDefault = this.ReferenceTypesAreNonNullByDefault;
      coptions.RunProgramVerifier = this.RunProgramVerifier;
      coptions.RunProgramVerifierWhileEditing = this.RunProgramVerifierWhileEditing;
      string s = this.ProgramVerifierCommandLineOptions;
      if (s == null){
        coptions.ProgramVerifierCommandLineOptions = new System.Compiler.StringList();
      } else {
        coptions.ProgramVerifierCommandLineOptions = new System.Compiler.StringList(s.Split(' '));
      }
      // begin change by drunje
      coptions.AllowPointersToManagedStructures = this.AllowPointersToManagedStructures;
      // end change by drunje
      coptions.CheckContractAdmissibility = this.CheckContractAccessibility;
      coptions.CheckPurity = this.CheckPurity;
      return coptions;
    }
  }
  public class GlyphProvider : Microsoft.VisualStudio.IntegrationHelper.GlyphProvider{
    public override int GetGlyph(Cci.Member member){
      Cci.Field f = member as Cci.Field;
      if (f != null && f.DeclaringType == null && f.IsCompilerControlled)
        return (int)ScopeIconGroup.IconGroupFieldYellow+(int)ScopeIconItem.IconItemPublic;
      return base.GetGlyph(member);
    }
  }
  [ComVisible(true), GuidAttribute("39B2605F-B083-4c69-AE34-71C9A769724C")]
  public class DebuggerEE : Microsoft.VisualStudio.IntegrationHelper.BaseExpressionEvaluator {
    public DebuggerEE() {
      this.cciEvaluator.ExprCompiler = new Microsoft.SpecSharp.Compiler();
      this.cciEvaluator.ExprErrorHandler = new Microsoft.SpecSharp.ErrorHandler(new Cci.ErrorNodeList());
    }
  }
  public class VsScanner : IScanner{
    private Scanner scanner;
    private ArrayList stateList = new ArrayList();
    private bool startExcludingAfterEndOfLine;

    internal VsScanner(SpecSharpCompilerOptions options){
      this.scanner = new Scanner(false, false, false, true, false);
      this.scanner.SetOptions(options);
    }  

    internal VsScanner(bool ignoreWhitespace){
      this.scanner = new Scanner(false, false, true, ignoreWhitespace, false);
    }

    public virtual void SetSource(string source, int offset){
      this.scanner.SetSource(source, offset);
    }
    public virtual bool ScanTokenAndProvideInfoAboutIt(TokenInfo tokenInfo, ref int stateIndex){
      ScannerRestartState restartState = this.GetRestartStateFor(stateIndex);
      restartState.EndPos = this.scanner.endPos;
      this.scanner.Restart(restartState);
      bool noMemberSelection = this.scanner.state == ScannerState.LastTokenDisablesMemberSelection;
      if (noMemberSelection){
        this.scanner.state = ScannerState.Code;
        this.scanner.RestartStateHasChanged = true;
      }
      SpecSharpTokenInfo specSharpTokenInfo = tokenInfo as SpecSharpTokenInfo;
      tokenInfo.trigger = TokenTrigger.None;
      Token tok;
      this.scanner.stillInsideToken = false;
      ScannerState state = this.scanner.state;
      switch (state){
        case ScannerState.CData:
          if (this.scanner.endPos >= this.scanner.maxPos) return false;
          this.scanner.ScanXmlCharacterData();
          if (this.scanner.stillInsideToken)
            this.scanner.stillInsideToken = false;
          else
            state = ScannerState.XML;
          tok = Token.CharacterData;
          break;
        case ScannerState.MLComment:
          if (this.scanner.endPos >= this.scanner.maxPos) return false;
          this.scanner.SkipMultiLineComment();
          if (this.scanner.stillInsideToken)
            this.scanner.stillInsideToken = false;
          else
            state = ScannerState.Code;
          tok = Token.MultiLineComment;
          break;
        case ScannerState.MLString:
          if (this.scanner.endPos >= this.scanner.maxPos) return false;
          this.scanner.ScanVerbatimString();
          if (this.scanner.stillInsideToken)
            this.scanner.stillInsideToken = false;
          else
            state = ScannerState.Code;
          tok = Token.LiteralContentString;
          break;
        case ScannerState.PI:
          if (this.scanner.endPos >= this.scanner.maxPos) return false;
          this.scanner.ScanXmlProcessingInstructionsTag();
          if (this.scanner.stillInsideToken)
            this.scanner.stillInsideToken = false;
          else
            state = ScannerState.XML;
          tok = Token.ProcessingInstructions;
          break;
        case ScannerState.Text:
          if (this.scanner.endPos >= this.scanner.maxPos) return false;
          this.scanner.ScanXmlText();
          if (this.scanner.stillInsideToken)
            this.scanner.stillInsideToken = false;
          else
            state = ScannerState.XML;
          tok = Token.LiteralContentString;
          break;
        case ScannerState.LiteralComment:
          if (this.scanner.endPos >= this.scanner.maxPos) return false;
          this.scanner.ScanXmlComment();
          if (this.scanner.stillInsideToken)
            this.scanner.stillInsideToken = false;
          else
            state = ScannerState.XML;
          tok = Token.LiteralComment;
          break;
        case ScannerState.XmlAttr1:
        case ScannerState.XmlAttr2:
          if (this.scanner.endPos >= this.scanner.maxPos) return false;
          this.scanner.ScanXmlString(state == ScannerState.XmlAttr1 ? '"' : '\'');
          if (this.scanner.stillInsideToken)
            this.scanner.stillInsideToken = false;
          else
            state = ScannerState.Tag; 
          tok = Token.StringLiteral;
          break;
        default:
          tok = this.scanner.GetNextToken();
          if (tok != Token.LeftBrace)
            state = this.scanner.state;
          break;
      }
      if (specSharpTokenInfo != null)
        specSharpTokenInfo.extendedType = tok;
      switch(tok){
        case Token.AddAssign:
        case Token.AddOne:
        case Token.Arrow:
        case Token.BitwiseAnd:
        case Token.BitwiseAndAssign:
        case Token.BitwiseNot:
        case Token.BitwiseOr:
        case Token.BitwiseOrAssign:
        case Token.BitwiseXor:
        case Token.BitwiseXorAssign:
        case Token.Divide:
        case Token.DivideAssign:
        case Token.Equal:
        case Token.GreaterThan:
        case Token.GreaterThanOrEqual:
        case Token.Iff:
        case Token.Implies:
        case Token.LeftShift:
        case Token.LeftShiftAssign:
        case Token.LessThanOrEqual:
        case Token.LogicalAnd:
        case Token.LogicalNot:
        case Token.LogicalOr:
        case Token.Maplet:
        case Token.Multiply:
        case Token.MultiplyAssign:
        case Token.NotEqual:
        case Token.Plus:
        case Token.Remainder:
        case Token.RemainderAssign:
        case Token.RightShift:
        case Token.RightShiftAssign:
        case Token.Subtract:
        case Token.SubtractAssign:
        case Token.SubtractOne:
          tokenInfo.color = TokenColor.Text;
          tokenInfo.type = TokenType.Operator;
          break;
        case Token.Assign:
          if (state == ScannerState.Tag)
            tokenInfo.color = (TokenColor)SpecSharpTokenColor.ProcessingInstruction;
          else
            tokenInfo.color = TokenColor.Text;
          tokenInfo.type = TokenType.Operator;
          break;
        case Token.Abstract:
        case Token.Acquire:
        case Token.Add:
        case Token.Additive:
        case Token.Alias:
        case Token.As:
        case Token.Assert:
        case Token.Assume:
        case Token.Base:
        case Token.Bool:
        case Token.Break:
        case Token.Byte:
        case Token.Case:
        case Token.Catch:
        case Token.Char:
        case Token.Checked:
        case Token.Class:
        case Token.Const:
        case Token.Continue:
        case Token.Count:
        case Token.Decimal:
        case Token.Default:
        case Token.Delegate:
        case Token.Do:
        case Token.Double:
        case Token.ElementsSeen:
        case Token.Else:
        case Token.Ensures:
        case Token.Enum:
        case Token.Event:
        case Token.Exists:
        case Token.Explicit:
        case Token.Expose:
        case Token.Extern: 
        case Token.False:
        case Token.Finally:
        case Token.Fixed:
        case Token.Float:
        case Token.For:
        case Token.Forall:
        case Token.Foreach:
        case Token.Get:
        case Token.Goto:
        case Token.If:
        case Token.Implicit:
        case Token.In:
        case Token.Invariant:
        case Token.Int:
        case Token.Interface:
        case Token.Internal:
        case Token.Is:
        case Token.Lock:
        case Token.Long:
        case Token.Max:        
        case Token.Min:
        case Token.Model:
        case Token.Namespace:
        case Token.New:
        case Token.Modifies:
        case Token.Object:
        case Token.Old:
        case Token.Operator:
        case Token.Out:
        case Token.Otherwise:
        case Token.Override:
        case Token.Params:
        case Token.Partial:
        case Token.Private:
        case Token.Product:
        case Token.Protected:
        case Token.Public:
        case Token.Read:
        case Token.Readonly:
        case Token.Ref:
        case Token.Remove:
        case Token.Requires:
        case Token.Return:
        case Token.Satisfies:
        case Token.Sbyte:
        case Token.Sealed:
        case Token.Set:
        case Token.Short:
        case Token.Sizeof:
        case Token.Stackalloc:
        case Token.Static:
        case Token.String:
        case Token.Struct:
        case Token.Sum:
        case Token.Switch:
        case Token.This:
        case Token.Throw:
        case Token.Throws:
        case Token.True:
        case Token.Try:
        case Token.Typeof:
        case Token.Uint:
        case Token.Ulong:
        case Token.Unchecked:
        case Token.Unique:
        case Token.Unsafe:
        case Token.Ushort:
        case Token.Using:
        case Token.Value:
        case Token.Virtual:
        case Token.Void:
        case Token.Volatile:
        case Token.Witness:
        case Token.Where:
        case Token.While:
        case Token.Write:
        case Token.Yield:
          tokenInfo.color = TokenColor.Keyword;
          tokenInfo.type = TokenType.Keyword;
          break;
        case Token.CharacterData:
          tokenInfo.color = (TokenColor)SpecSharpTokenColor.CData;
          tokenInfo.type = TokenType.Literal;
          if (this.scanner.stillInsideToken){
            this.scanner.stillInsideToken = false;
            state = ScannerState.CData;
          }
          break;
        case Token.Comma:
          tokenInfo.trigger = TokenTrigger.ParamNext;
          tokenInfo.color = TokenColor.Text;
          tokenInfo.type = TokenType.Delimiter;
          break;
        case Token.Conditional:
        case Token.Colon:
        case Token.Semicolon:
          tokenInfo.color = TokenColor.Text;
          tokenInfo.type = TokenType.Delimiter;
          break;
        case Token.DoubleColon:
        case Token.Dot:
          if (!noMemberSelection) tokenInfo.trigger = TokenTrigger.MemberSelect;
          tokenInfo.color = TokenColor.Text;
          tokenInfo.type = TokenType.Delimiter;
          break;
        case Token.EndOfSimpleTag:
          state = ScannerState.XML;
          tokenInfo.trigger = TokenTrigger.MatchBraces;
          tokenInfo.color = (TokenColor)SpecSharpTokenColor.ProcessingInstruction;
          tokenInfo.type = TokenType.Delimiter;
          break;
        case Token.EndOfTag:
          state = ScannerState.XML;
          tokenInfo.color = (TokenColor)SpecSharpTokenColor.ProcessingInstruction;
          tokenInfo.type = TokenType.Delimiter;
          break;
        case Token.HexLiteral:
        case Token.IntegerLiteral:
        case Token.RealLiteral:
          state = ScannerState.LastTokenDisablesMemberSelection;
          tokenInfo.color = TokenColor.Number;
          tokenInfo.type = TokenType.Literal;
          break;
        case Token.Identifier:
          if (state == ScannerState.Code){
            //if (this.scanner.startPos + 1 == this.scanner.endPos) {
            //  if (noMemberSelection)
            //    state = ScannerState.LastTokenDisablesMemberSelection;
            //  else
            //    tokenInfo.trigger = TokenTrigger.MemberSelect;
            //}
            tokenInfo.color = TokenColor.Identifier;
          }else
            tokenInfo.color = (TokenColor)SpecSharpTokenColor.ProcessingInstruction;
          tokenInfo.type = TokenType.Identifier;
          break;
        case Token.LeftBrace:
          tokenInfo.trigger = TokenTrigger.MatchBraces;
          tokenInfo.color = TokenColor.Text;
          tokenInfo.type = TokenType.Delimiter;
          break;
        case Token.LeftBracket:
        case Token.LeftParenthesis:
          tokenInfo.trigger = TokenTrigger.ParamStart|TokenTrigger.MatchBraces;
          tokenInfo.color = TokenColor.Text;
          tokenInfo.type = TokenType.Delimiter;
          break;
        case Token.LessThan:
          tokenInfo.trigger = TokenTrigger.ParamStart;
          tokenInfo.color = TokenColor.Text;
          tokenInfo.type = TokenType.Delimiter;
          break;
        case Token.MultiLineComment:
          tokenInfo.color = TokenColor.Comment;
          tokenInfo.type = TokenType.Comment;
          if (this.scanner.stillInsideToken){
            this.scanner.stillInsideToken = false;
            state = ScannerState.MLComment;
          }
          break;
        case Token.Null:
          state = ScannerState.LastTokenDisablesMemberSelection;
          tokenInfo.color = TokenColor.Keyword;
          tokenInfo.type = TokenType.Keyword;
          break;
        case Token.PreProcessorDirective:
          tokenInfo.color = TokenColor.Keyword;
          tokenInfo.type = TokenType.Delimiter;
          if (this.scanner.state == ScannerState.ExcludedCode){
            this.startExcludingAfterEndOfLine = true;
            state = ScannerState.Code;
          }
          break;
        case Token.PreProcessorExcludedBlock:
          tokenInfo.color = (TokenColor)SpecSharpTokenColor.ProcessingInstruction;
          tokenInfo.type = TokenType.Literal;
          break;
        case Token.ProcessingInstructions:
          tokenInfo.color = (TokenColor)SpecSharpTokenColor.ProcessingInstruction;
          tokenInfo.type = TokenType.Literal;
          if (this.scanner.stillInsideToken){
            this.scanner.stillInsideToken = false;
            state = ScannerState.PI;
          }
          break;
        case Token.RightBrace:
          tokenInfo.trigger = TokenTrigger.MatchBraces;
          tokenInfo.color = TokenColor.Text;
          tokenInfo.type = TokenType.Delimiter;
          break;
        case Token.RightBracket:
        case Token.RightParenthesis:
          tokenInfo.trigger = TokenTrigger.ParamEnd|TokenTrigger.MatchBraces;
          tokenInfo.color = TokenColor.Text;
          tokenInfo.type = TokenType.Delimiter;
          break;
        case Token.SingleLineComment:
          tokenInfo.color = TokenColor.Comment;
          tokenInfo.type = TokenType.LineComment;
          break;
        case Token.SingleLineDocCommentStart:
          tokenInfo.color = (TokenColor)SpecSharpTokenColor.ProcessingInstruction;
          tokenInfo.type = TokenType.LineComment;
          break;
        case Token.StartOfClosingTag:
          state = ScannerState.Tag;
          tokenInfo.color = (TokenColor)SpecSharpTokenColor.ProcessingInstruction;
          tokenInfo.type = TokenType.Delimiter;
          break;
        case Token.StartOfTag:
          state = ScannerState.Tag;
          tokenInfo.trigger = TokenTrigger.MemberSelect;
          tokenInfo.color = (TokenColor)SpecSharpTokenColor.ProcessingInstruction;
          tokenInfo.type = TokenType.Delimiter;
          break;
        case Token.StringLiteral:
          if (state == ScannerState.Code || state == ScannerState.MLString)
            tokenInfo.color = TokenColor.String;
          else
            tokenInfo.color = (TokenColor)SpecSharpTokenColor.ProcessingInstruction;
          tokenInfo.type = TokenType.String;
          break;
        case Token.LiteralComment:
          tokenInfo.color = TokenColor.Comment;
          tokenInfo.type = TokenType.Comment;
          if (this.scanner.stillInsideToken){
            this.scanner.stillInsideToken = false;
            state = ScannerState.LiteralComment;
          }
          break;
        case Token.LiteralContentString:
          tokenInfo.color = TokenColor.Comment;
          tokenInfo.type = TokenType.String;
          break;
        case Token.MultiLineDocCommentStart:
          tokenInfo.color = (TokenColor)SpecSharpTokenColor.ProcessingInstruction;
          tokenInfo.type = TokenType.Delimiter;
          break;
        case Token.ObjectLiteralStart: // this is a code to object literal switch by definition
          state = ScannerState.XML;
          tokenInfo.color = TokenColor.Keyword;
          tokenInfo.type = TokenType.Delimiter;
          break;
        case Token.DocCommentEnd:
          tokenInfo.color = (TokenColor)SpecSharpTokenColor.ProcessingInstruction;
          tokenInfo.type = TokenType.Delimiter;
          state = ScannerState.Code;
          break;
        case Token.EndOfFile:
          if (this.startExcludingAfterEndOfLine){
            this.startExcludingAfterEndOfLine = false;
            stateIndex = this.GetRestartStateIndex(ScannerState.ExcludedCode, stateIndex);
          }else if (this.scanner.state == ScannerState.XML && this.scanner.docCommentStart == Token.SingleLineDocCommentStart){
            stateIndex = this.GetRestartStateIndex(ScannerState.Code, stateIndex);
          }
          return false;
        case Token.WhiteSpace:
          tokenInfo.color = TokenColor.Text;
          tokenInfo.type = TokenType.WhiteSpace;
          break;
        default:
          tokenInfo.color = TokenColor.Text;
          tokenInfo.type = TokenType.Delimiter;
          break;
      }
      tokenInfo.startIndex = this.scanner.startPos;
      tokenInfo.endIndex = this.scanner.endPos-1;
      if (state != this.scanner.state || this.scanner.RestartStateHasChanged)
        stateIndex = this.GetRestartStateIndex(state, stateIndex);
      return true;
    }
    private ScannerRestartState GetRestartStateFor(int stateIndex){
      if (stateIndex < 0){Debug.Assert(false); stateIndex = 0;}
      if (this.stateList.Count <= stateIndex){
        Debug.Assert(stateIndex == 0);
        ScannerRestartState restartState = new ScannerRestartState();
        this.stateList.Add(restartState);
        this.scanner.InitializeRestartState(restartState);
        return restartState;
      }
      return (ScannerRestartState)this.stateList[stateIndex];
    }
    private int GetRestartStateIndex(ScannerState state, int stateIndex){
      ScannerRestartState restartState = new ScannerRestartState();
      this.scanner.InitializeRestartState(restartState);
      restartState.State = state;
      for (int i = stateIndex-1; i >= 0; i--){
        ScannerRestartState rstate = (ScannerRestartState)this.stateList[i];
        if (rstate == restartState) return i;
      }
      int n = this.stateList.Count;
      for (int i = stateIndex+1; i < n; i++){
        ScannerRestartState rstate = (ScannerRestartState)this.stateList[i];
        if (rstate == restartState) return i;
      }
      this.stateList.Add(restartState);
      return n;
    }
  }
  // [vijayeg] Extend the TokenInfo class to provide more spec-sharp specific token information
  public class SpecSharpTokenInfo : TokenInfo{
    public Token extendedType;
  }
}
#endif