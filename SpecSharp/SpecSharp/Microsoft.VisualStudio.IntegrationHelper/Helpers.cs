//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Xml;
#if CCINamespace
using Cci = Microsoft.Cci;
#else
using Cci = System.Compiler;
#endif

namespace Microsoft.VisualStudio.IntegrationHelper{
  public class AuthoringScope : Microsoft.VisualStudio.Package.AuthoringScope{
    public Cci.AuthoringScope scAuthoringScope;
    public GlyphProvider glyphProvider;

    public AuthoringScope(Cci.AuthoringScope authoringScope, GlyphProvider glyphProvider){
      this.scAuthoringScope = authoringScope;
      this.glyphProvider = glyphProvider;
    }
    public override string GetDataTipText(int line, int col, out TextSpan span){
      Cci.SourceContext ctx;
      string text = this.scAuthoringScope.GetDataTipText(line, col, out ctx);
      if (text == null) text = "";
      span = new TextSpan();
      if (ctx.Document != null && ctx.StartLine == line+1){
        // This gives the span of the symbol we are providing information about
        // so that the data tip text remains open until the mouse exits the bounds
        // of this span.
        span.iStartIndex = ctx.StartColumn-1;
        span.iStartLine = ctx.StartLine-1;
        span.iEndIndex = ctx.EndColumn-1;
        span.iEndLine = ctx.EndLine-1;
      }else{
        // The authoring scope failed to provide us with a valid source context that spans the current cursor position. 
        // Make up a span.
        span.iStartLine = line;
        span.iStartIndex = col > 0 ? col-1 : col;
        span.iEndIndex = col+2;
        span.iEndLine = line;
      }
      return text;
    }
    public override Microsoft.VisualStudio.Package.Declarations GetDeclarations(IVsTextView view, int line, int col, TokenInfo info){
      Cci.Declarations scDeclarations = this.scAuthoringScope.GetDeclarations(line, col, Cci.ParseReason.MemberSelect);
      return new Declarations(scDeclarations, this.glyphProvider);
    }
    public override Microsoft.VisualStudio.Package.Overloads GetMethods(int line, int col, string name){
      Cci.Overloads scOverloads = this.scAuthoringScope.GetMethods(line, col, Cci.Identifier.For(name));
      if (scOverloads == null) return null;
      return new Overloads(scOverloads);
    }
    public override Microsoft.VisualStudio.Package.Overloads GetTypes(int line, int col, string name){
      Cci.Overloads scOverloads = this.scAuthoringScope.GetTypes(line, col, Cci.Identifier.For(name));
      if (scOverloads == null) return null;
      return new Overloads(scOverloads);
    }
    public override string Goto(VsCommands cmd, IVsTextView textView, int line, int col, out TextSpan span) {
      span = new TextSpan();
      Cci.SourceContext targetPosition = new Cci.SourceContext();
      switch (cmd){
        case VsCommands.GotoDecl:
          targetPosition = this.scAuthoringScope.GetPositionOfDeclaration(line, col);
          break;
        case VsCommands.GotoDefn:
          targetPosition = this.scAuthoringScope.GetPositionOfDefinition(line, col);
          break;
        case VsCommands.GotoRef:
          targetPosition = this.scAuthoringScope.GetPositionOfReference(line, col);
          break;
      }
      if (targetPosition.Document != null){
        span.iEndIndex = targetPosition.EndColumn-1;
        span.iEndLine = targetPosition.EndLine-1;
        span.iStartIndex = targetPosition.StartColumn-1;
        span.iStartLine = targetPosition.StartLine-1;
        return targetPosition.Document.Name;
      }else{
        //TODO: return URL to object browser for imported type information.
      }
      return null;
    }
  }
  public class CollapsibleRegion {
    public Cci.SourceContext SourceContext;
    public bool Collapsed;

    public CollapsibleRegion(Cci.SourceContext sourceContext, bool collapsed) {
      this.SourceContext = sourceContext;
      this.Collapsed = collapsed;
    }
  }
  public class AuthoringSink : Cci.AuthoringSink{
    public Microsoft.VisualStudio.Package.AuthoringSink vsAuthoringSink;
    public ArrayList CollapsibleRegions;

    public AuthoringSink(Microsoft.VisualStudio.Package.AuthoringSink vsAuthoringSink){
      Debug.Assert(vsAuthoringSink != null);
      this.vsAuthoringSink = vsAuthoringSink;
    }

    public override void AddCollapsibleRegion(Cci.SourceContext context, bool collapsed) {
      if (this.CollapsibleRegions == null) this.CollapsibleRegions = new ArrayList();
      this.CollapsibleRegions.Add(new CollapsibleRegion(context, collapsed));
    }
    public override void AddError(Cci.ErrorNode node){
      if (node == null) return;
      this.vsAuthoringSink.AddError(new ErrorNode(node));
    }
    public override void AutoExpression(Cci.SourceContext exprContext){
      this.vsAuthoringSink.AutoExpression(new SourceContext(exprContext));
    }
    public override void CodeSpan(Cci.SourceContext spanContext){
      this.vsAuthoringSink.CodeSpan(new SourceContext(spanContext));
    }
    public override void EndParameters(Cci.SourceContext context){
      this.vsAuthoringSink.EndParameters(new SourceContext(context));
    }
    public override void EndTemplateParameters(System.Compiler.SourceContext context) {
      this.vsAuthoringSink.EndTemplateParameters(new SourceContext(context));
    }
    public override void MatchPair(Cci.SourceContext startContext, Cci.SourceContext endContext) {
      this.vsAuthoringSink.MatchPair(new SourceContext(startContext), new SourceContext(endContext));
    }
    public override void MatchTriple(Cci.SourceContext startContext, Cci.SourceContext middleContext, Cci.SourceContext endContext){
      this.vsAuthoringSink.MatchTriple(new SourceContext(startContext), new SourceContext(middleContext), new SourceContext(endContext));
    }
    public override void NextParameter(Cci.SourceContext context){
      this.vsAuthoringSink.NextParameter(new SourceContext(context));
    }
    public override void NextTemplateParameter(System.Compiler.SourceContext context) {
      this.vsAuthoringSink.NextTemplateParameter(new SourceContext(context));
    }
    public override void QualifyName(Cci.SourceContext selectorContext, Cci.Expression name) {
      if (name == null) return;
      this.vsAuthoringSink.QualifyName(new SourceContext(selectorContext), new SourceContext(name.SourceContext), name.ToString());
    }
    public override void StartName(Cci.Expression name){
      if (name == null) return;
      this.vsAuthoringSink.StartName(new SourceContext(name.SourceContext), name.ToString());
    }
    public override void StartParameters(Cci.SourceContext context){
      this.vsAuthoringSink.StartParameters(new SourceContext(context));
    }
    public override void StartTemplateParameters(System.Compiler.SourceContext context){
      this.vsAuthoringSink.StartTemplateParameters(new SourceContext(context));
    }
  }
  public class CodeWindowManager : Microsoft.VisualStudio.Package.CodeWindowManager{
    public GlyphProvider glyphProvider;
    public CodeWindowManager(LanguageService service, IVsCodeWindow codeWindow, Source source, GlyphProvider glyphProvider)
      : base(service, codeWindow, source){
      this.glyphProvider = glyphProvider;
    }
    public override Microsoft.VisualStudio.Package.TypeAndMemberDropdownBars GetTypeAndMemberDropdownBars(VisualStudio.Package.LanguageService languageService){
      return new TypeAndMemberDropdownBars((LanguageService)languageService, this.glyphProvider);
    }
  }
  public class CompilerOptions : Cci.CompilerOptions{
    public CompilerOptions(Microsoft.VisualStudio.Package.ProjectOptions compilerOptions){
      this.AdditionalSearchPaths = new Cci.StringList(compilerOptions.AdditionalSearchPaths);
      this.AllowUnsafeCode = compilerOptions.AllowUnsafeCode;
      this.BaseAddress = compilerOptions.BaseAddress;
      this.BugReportFileName = compilerOptions.BugReportFileName;
      this.CheckedArithmetic = compilerOptions.CheckedArithmetic;
      this.CodePage = compilerOptions.CodePage;
      this.CompileAndExecute = compilerOptions.CompileAndExecute;
      this.DefinedPreProcessorSymbols = new Cci.StringList(compilerOptions.DefinedPreProcessorSymbols);
      this.DisplayCommandLineHelp = compilerOptions.DisplayCommandLineHelp;
      this.DisableAssumeChecks = compilerOptions.DisableAssumeChecks;
      this.DisableDefensiveChecks = compilerOptions.DisableDefensiveChecks;
      this.DisableGuardedClassesChecks = compilerOptions.DisableGuardedClassesChecks;
      this.DisableInternalChecks = compilerOptions.DisableInternalChecks;
      this.DisableInternalContractsMetadata = compilerOptions.DisableInternalContractsMetadata;
      this.DisablePublicContractsMetadata = compilerOptions.DisablePublicContractsMetadata;
      foreach (string eresource in compilerOptions.EmbeddedResources) this.EmbeddedResources.Add(eresource);
      this.EmitManifest = compilerOptions.EmitManifest;
      this.EncodeOutputInUTF8 = compilerOptions.EncodeOutputInUTF8;
      this.Evidence = compilerOptions.Evidence;
      this.FileAlignment = compilerOptions.FileAlignment;
      this.GenerateExecutable = compilerOptions.GenerateExecutable;
      this.GenerateInMemory = compilerOptions.GenerateInMemory;
      this.HeuristicReferenceResolution = compilerOptions.HeuristicReferenceResolution;
      this.IncludeDebugInformation = compilerOptions.IncludeDebugInformation;
      this.IncrementalCompile = compilerOptions.IncrementalCompile;
      this.IsContractAssembly = compilerOptions.ShadowedAssembly != null && compilerOptions.ShadowedAssembly.Length > 0;
      foreach (string lresource in compilerOptions.LinkedResources) this.LinkedResources.Add(lresource);
      this.MainClass = compilerOptions.MainClass;
      this.ModuleKind = (Cci.ModuleKindFlags)compilerOptions.ModuleKind;
      this.NoStandardLibrary = compilerOptions.NoStandardLibrary;
      this.Optimize = compilerOptions.Optimize;
      this.OutputAssembly = Path.GetFileNameWithoutExtension(compilerOptions.OutputAssembly);
      this.OutputPath = Path.GetDirectoryName(compilerOptions.OutputAssembly); //TODO: look at project code
      this.PDBOnly = compilerOptions.PDBOnly;
      this.RecursiveWildcard = compilerOptions.RecursiveWildcard;
      foreach (string refAssem in compilerOptions.ReferencedAssemblies) this.ReferencedAssemblies.Add(refAssem);
      this.ReferencedModules = new Cci.StringList(compilerOptions.ReferencedModules);
      this.RootNamespace = compilerOptions.RootNamespace;
      this.ShadowedAssembly = compilerOptions.ShadowedAssembly;
      this.StandardLibraryLocation = compilerOptions.StandardLibraryLocation;
      this.SuppressedWarnings = new Cci.Int32List(compilerOptions.SuppressedWarnings);
      this.SuppressLogo = compilerOptions.SuppressLogo;
      this.TargetPlatform = (Cci.PlatformType)compilerOptions.TargetPlatform;
      this.TargetPlatformLocation = compilerOptions.TargetPlatformLocation;
      this.TempFiles = compilerOptions.TempFiles;
      this.TreatWarningsAsErrors = compilerOptions.TreatWarningsAsErrors;
      this.UserLocaleId = compilerOptions.UserLocaleId;
      this.UserToken = compilerOptions.UserToken;
      this.WarningLevel = compilerOptions.WarningLevel;
      this.Win32Icon = compilerOptions.Win32Icon;
      this.Win32Resource = compilerOptions.Win32Resource;
      this.XMLDocFileName = compilerOptions.XMLDocFileName;
    }
  }
  public class Declarations : Microsoft.VisualStudio.Package.Declarations{
    public Cci.Declarations scDeclarations;
    public GlyphProvider glyphProvider;

    public Declarations(Cci.Declarations scDeclarations, GlyphProvider glyphProvider){
      this.scDeclarations = scDeclarations;
      this.glyphProvider = glyphProvider;
    }
    public override void GetBestMatch(string text, out int index, out bool uniqueMatch){
      this.scDeclarations.GetBestMatch(text, out index, out uniqueMatch);
    }
    public override int GetCount(){
      return this.scDeclarations.GetCount();
    }
    public override string GetDescription(int index){
      return this.scDeclarations.GetDescription(index);
    }
    public override string GetInsertionText(int index){
      return this.scDeclarations.GetInsertionText(index);
    }
    public override int GetGlyph(int index){
      Cci.Member member = this.scDeclarations.GetMember(index);
      return this.glyphProvider.GetGlyph(member);
    }
    public override string GetDisplayText(int index){
      return this.scDeclarations.GetDisplayText(index);
    }
    public override bool IsCommitChar(string textSoFar, char commitChar){
      return this.scDeclarations.IsCommitChar(textSoFar, commitChar);
    }
  }
  public class GlyphProvider{
    protected const int IconGroupSize = 6; // does not get imported into the type lib.
    public virtual int GetGlyph(Cci.Member member){
      ScopeIconGroup group;
      if (member is Cci.Method){
        if (member.IsSpecialName && member.IsStatic && member.Name.ToString().StartsWith("op_"))
          group = ScopeIconGroup.IconGroupFormula;
        else
          group = ScopeIconGroup.IconGroupMethod;
      }else if (member is Cci.Property)
        group = ScopeIconGroup.IconGroupProperty;
      else if (member is Cci.Class)
        group = ScopeIconGroup.IconGroupClass;
      else if (member is Cci.DelegateNode)
        group = ScopeIconGroup.IconGroupDelegate;
      else if (member is Cci.Event)
        group = ScopeIconGroup.IconGroupEvent;
      else if (member is Cci.EnumNode)
        group = ScopeIconGroup.IconGroupEnum;
      else if (member is Cci.Interface)
        group = ScopeIconGroup.IconGroupInterface;
      else if (member is Cci.Struct)
        group = ScopeIconGroup.IconGroupStruct;
      else if (member is Cci.Namespace)
        group = ScopeIconGroup.IconGroupNameSpace;
      else if (member is Cci.Field){
        if (member.DeclaringType is Cci.EnumNode)
          group = ScopeIconGroup.IconGroupEnumConst;
        else if (((Cci.Field)member).IsLiteral || ((Cci.Field)member).IsInitOnly)
          group = ScopeIconGroup.IconGroupFieldRed;
        else
          group = ScopeIconGroup.IconGroupFieldBlue;
      }else
        return (int)ScopeIconMisc.IconBlackBox;
      int glyph = (int)group * IconGroupSize;
      if (member.IsPublic)
        glyph += (int)ScopeIconItem.IconItemPublic;
      else if (member.IsFamily || member.IsFamilyAndAssembly || member.IsFamilyOrAssembly)
        glyph += (int)ScopeIconItem.IconItemProtected;
      else if (member.IsAssembly)
        glyph += (int)ScopeIconItem.IconItemInternal;
      else if (!(member is Cci.Namespace))
        glyph += (int)ScopeIconItem.IconItemPrivate;
      return glyph;
    }
  }
  public class ErrorNode : Microsoft.VisualStudio.Package.ErrorNode{
    public Cci.ErrorNode scErrorNode;
    public ErrorNode(Cci.ErrorNode scErrorNode)
      : base(scErrorNode.Code, new SourceContext(scErrorNode.SourceContext)){
      this.scErrorNode = scErrorNode;
    }
    public override string GetMessage(System.Globalization.CultureInfo culture){
      return this.scErrorNode.GetMessage(culture);
    }
    public override Microsoft.VisualStudio.Package.Severity Severity{
      get {
        switch (this.scErrorNode.Severity){
          case 0: return Microsoft.VisualStudio.Package.Severity.SevFatal;
          default: return Microsoft.VisualStudio.Package.Severity.SevWarning;
        }
      }
    }
  }
	public abstract class LanguageService : Microsoft.VisualStudio.Package.LanguageService{
    public Cci.LanguageService scLanguageService;
    public GlyphProvider glyphProvider;
    public LanguagePreferences vsLanguagePreferences; //Set by Init

		public LanguageService(Cci.LanguageService scLanguageService, ImageList completionImages, GlyphProvider glyphProvider)
      : base(completionImages){
      this.scLanguageService = scLanguageService;
      this.glyphProvider = glyphProvider;
		}

    public override LanguagePreferences CreateLanguagePreferences(){
      return this.vsLanguagePreferences;
    }
    public override Microsoft.VisualStudio.Package.CodeWindowManager GetCodeWindowManager(
      Microsoft.VisualStudio.Package.LanguageService languageService, Microsoft.VisualStudio.TextManager.Interop.IVsCodeWindow codeWindow, 
      Microsoft.VisualStudio.Package.Source source){
      return new CodeWindowManager((LanguageService)languageService, codeWindow, source, glyphProvider);
    }
    public override void GetCommentFormat(CommentInfo info){
      Cci.CommentInfo scCommentInfo = new Cci.CommentInfo();
      this.scLanguageService.GetCommentFormat(scCommentInfo);
      info.blockEnd = scCommentInfo.blockEnd;
      info.blockStart = scCommentInfo.blockStart;
      info.lineStart = scCommentInfo.lineStart;
      info.supported = scCommentInfo.supported;
      info.useLineComments = scCommentInfo.useLineComments;
    }
    public override int GetItemCount(out int count){
      count = this.scLanguageService.GetColorCount();
      return 0;
    }
    public override void GetMethodFormat(out string typeStart, out string typeEnd, out bool typePrefixed){
      this.scLanguageService.GetMethodFormat(out typeStart, out typeEnd, out typePrefixed);
    }
    public override Microsoft.VisualStudio.Package.AuthoringScope ParseSource(string text, int line, int col, string fname, 
      Microsoft.VisualStudio.Package.AuthoringSink aSink, Microsoft.VisualStudio.Package.ParseReason reason){
      lock (ProjectManager.BuildLock){
        Cci.AuthoringSink scAsink = new AuthoringSink(aSink);
        Cci.AuthoringScope scAuthScope = 
          this.scLanguageService.ParseSource(text, line, col, fname, scAsink, (Cci.ParseReason)reason);
        return new AuthoringScope(scAuthScope, this.glyphProvider);
      }
    }
    public override NewOutlineRegion[] GetCollapsibleRegions(string text, string fname){
      Cci.ParseReason reason = Cci.ParseReason.CollapsibleRegions;
      Microsoft.VisualStudio.Package.AuthoringSink aSink = new Microsoft.VisualStudio.Package.AuthoringSink(0, 0, 0);
      AuthoringSink scAsink = new AuthoringSink(aSink);
      this.scLanguageService.ParseSource(text, 0, 0, fname, scAsink, reason);
      if (scAsink.CollapsibleRegions == null) return null;
      int n = scAsink.CollapsibleRegions.Count;
      NewOutlineRegion[] result = new NewOutlineRegion[n];
      for (int i = 0; i < n; i++){
        CollapsibleRegion cr = (CollapsibleRegion)scAsink.CollapsibleRegions[i];
        if (cr == null) continue;
        TextSpan span = new TextSpan();
        span.iStartIndex = cr.SourceContext.StartColumn-1;
        span.iStartLine = cr.SourceContext.StartLine-1;
        span.iEndIndex = cr.SourceContext.EndColumn-1;
        span.iEndLine = cr.SourceContext.EndLine-1;
        NewOutlineRegion region = new NewOutlineRegion();
        region.tsHiddenText = span;
        region.dwState = (uint)(cr.Collapsed ? HIDDEN_REGION_STATE.hrsDefault : HIDDEN_REGION_STATE.hrsExpanded);
        result[i] = region;
      }
      return result;
    }
	}
  public class Overloads : Microsoft.VisualStudio.Package.Overloads{
    public Cci.Overloads scOverloads;
    public Overloads(Cci.Overloads scOverloads){
      Debug.Assert(scOverloads != null);
      this.scOverloads = scOverloads;
    }
    
    public override int GetCount(){
      return this.scOverloads.GetCount();
    }
    public override string GetDescription(int index){
      return this.scOverloads.GetHelpText(index);
    }
    public override string GetName(int index){
      return this.scOverloads.GetName(index);
    }
    public override string GetParameterClose(int index){
      return this.scOverloads.GetParameterClose(index);
    }
    public override int GetParameterCount(int index){
      return this.scOverloads.GetParameterCount(index);
    }
    public override void GetParameterInfo(int index, int parameter, out string name, out string display, out string description){
      this.scOverloads.GetParameterInfo(index, parameter, out name, out display, out description);
    }
    public override string GetParameterOpen(int index){
      return this.scOverloads.GetParameterOpen(index);
    }
    public override string GetParameterSeparator(int index){
      return this.scOverloads.GetParameterSeparator(index);
    }
    public override string GetType(int index){
      return this.scOverloads.GetType(index);
    }
    public override int GetPositionOfSelectedMember(){
      return this.scOverloads.GetPositionOfSelectedMember();
    }
  }
  public class Project{
    /// <summary>
    /// The compilation parameters that are used for this compilation.
    /// </summary>
    public System.CodeDom.Compiler.CompilerParameters CompilerParameters;
    /// <summary>
    /// The paths to the artifacts (eg. files) in which the source texts are stored. Used together with IndexForFullPath.
    /// </summary>
    public Cci.StringList FullPathsToSources;
    /// <summary>
    /// A scope for symbols that belong to the compilation as a whole. No C# equivalent. Null if not applicable.
    /// </summary>
    public Cci.Scope GlobalScope;
    /// <summary>
    /// Set to true if this project has been constructed automatically to contain a single file that is being edited out of context.
    /// In such cases it does not makes sense to produce semantic errors, since assembly references will be missing, as will other
    /// source files that need to be compiled together with the single file in the dummy project.
    /// </summary>
    public bool IsDummy;
    /// <summary>
    /// The source texts to be compiled together with the appropriate parser to call and possibly the resulting analyzed abstract syntax tree.
    /// </summary>
    public Cci.CompilationUnitSnippetList CompilationUnitSnippets;
    public Cci.Compilation Compilation;
    /// <summary>
    /// Helps client code keep track of when the project was last modified.
    /// </summary>
    public DateTime LastModifiedTime = DateTime.Now;

    /// <summary>
    /// Helps clients keep track of the Project instance that caused the environment to open the specified file
    /// </summary>
    public static Project For(string sourceFileUri){
      WeakReference weakRef = (WeakReference)Project.projects[sourceFileUri];
      if (weakRef == null || !weakRef.IsAlive) return null;
      return (Project)weakRef.Target;
    }
    private static Hashtable projects = Hashtable.Synchronized(new Hashtable());
  }
  public class BuildSiteAdaptor : Cci.CompilerSite{
    BuildSite buildSite;
    public BuildSiteAdaptor(BuildSite buildSite){
      this.buildSite = buildSite;
    }
    public override void OutputMessage(string message) {
      this.buildSite.OutputMessage (message);
    }
    public override bool ShouldCancel {
      get {
        return this.buildSite.ShouldCancel;
      }
    }
  }
  public abstract class ProjectManager : Microsoft.VisualStudio.Package.Project{
    protected Cci.Compiler compiler;
    protected Project scProject;

    public ProjectManager(){
      this.scProject = new Project(); 
      this.LastModifiedTime = scProject.LastModifiedTime.AddTicks(10);
    }
    public override CompilerResults CompileAssemblyFromFile(ProjectOptions options, string filename){
      return this.compiler.CompileAssemblyFromFile(this.GetCompilerOptions(options), filename, new Cci.ErrorNodeList(), false);
    }
    public override CompilerResults CompileAssemblyFromFileBatch(ProjectOptions options, string[] fileNames){
      return this.compiler.CompileAssemblyFromFileBatch(this.GetCompilerOptions(options), fileNames, new Cci.ErrorNodeList(), false);
    }
    public virtual Cci.CompilerOptions GetCompilerOptions(Microsoft.VisualStudio.Package.ProjectOptions projectOptions){
      return new CompilerOptions(projectOptions);
    }
    public virtual Project GetCompilerProject(){
      return this.GetCompilerProject(this.GetActiveConfiguration(), new Cci.ErrorNodeList());
    }
    public virtual void RefreshPerBuildCompilerParameters()
    {
    }
    public virtual Project GetCompilerProject(XmlElement config, Cci.ErrorNodeList errors){
      if (config == null) return null;
      lock(ProjectManager.BuildLock){
        Project project = this.scProject;
        if (project == null) {Debug.Assert(false); return null;}
        bool refresh = this.LastModifiedTime != project.LastModifiedTime || this.lastConfig != config;
        if (!refresh){
          Debug.Assert(project.CompilerParameters != null);
          this.RefreshPerBuildCompilerParameters();
          return project;
        }
        Cci.Compilation compilation = project.Compilation = this.compiler.CreateCompilation(null, null, null, null);
        this.compiler.CurrentCompilation = compilation;
        project.LastModifiedTime = this.LastModifiedTime;
        Microsoft.VisualStudio.Package.ProjectOptions vscOptions = this.GetProjectOptions(config);
        Cci.CompilerOptions options = this.GetCompilerOptions(vscOptions);
        if (options == null){Debug.Assert(false); return null;}
        project.CompilerParameters = options;
        string dir = this.ProjectFolder;
        XmlDocument doc = this.projFile;
        Cci.StringList fileNames = new Cci.StringList();
        Cci.CompilationUnitSnippetList cuSnippets = new Cci.CompilationUnitSnippetList();
        Cci.CompilationUnitList cUnits = new Cci.CompilationUnitList();
        foreach (XmlElement e in doc.SelectNodes("//Files/Include/File")){ 
          if (e == null) continue;
          string relpath = e.GetAttribute("RelPath");
          string file = Path.Combine(dir, relpath);
          Cci.CompilationUnitSnippet snippet = null;
          int key = Cci.Identifier.For(file).UniqueIdKey;
          if (!File.Exists(file)){
            errors.Add(this.compiler.CreateErrorNode(Cci.Error.NoSuchFile,file));
            continue;
          }
          snippet = this.GetCompilationUnitSnippetFor(file, e.GetAttribute("BuildAction"));
          if (snippet == null) continue;
          fileNames.Add(file);
          cuSnippets.Add(snippet);
          cUnits.Add(snippet);
          snippet.Compilation = compilation;
        }
        project.CompilationUnitSnippets = cuSnippets;
        project.FullPathsToSources = fileNames;
        compilation.CompilationUnits = cUnits;
        compilation.CompilerParameters = options;
        compilation.ReferencedCompilations = this.GetReferencedCompilations(config, errors);
        this.compiler.ConstructSymbolTable(compilation, errors);
        this.compiler.CurrentCompilation = null;
        return project;
      }
    }
    public virtual Cci.CompilationList GetReferencedCompilations(XmlElement config, Cci.ErrorNodeList errors){
      ArrayList refProjs = this.GetReferencedProjects(config);
      int n = refProjs == null ? 0 : refProjs.Count;
      if (n == 0) return null;
      Cci.CompilationList result = new Cci.CompilationList(n);
      for (int i = 0; i < n; i++){
        ProjectManager proj = refProjs[i] as ProjectManager;
        if (proj == null){Debug.Assert(false); continue;}
//        proj.compiler.OnUpdate += this.updateHandler;
        XmlElement pconfig = proj.GetActiveConfiguration();
        Project cproj = proj.GetCompilerProject(pconfig, errors);
        if (cproj == null || cproj.Compilation == null){Debug.Assert(false); continue;}
        result.Add(cproj.Compilation);
      }
      return result;
    }
    public abstract Cci.CompilationUnitSnippet GetCompilationUnitSnippetFor(string fullPathToSource, string buildAction);
    public override CompilerResults CompileProject(XmlElement config){
      Cci.ErrorNodeList errors = new Cci.ErrorNodeList();
      Project project = this.GetCompilerProject(config, errors);
      if (project == null) return null;
      if (errors.Count > 0 && project.CompilerParameters != null){
        CompilerResults results = new CompilerResults(project.CompilerParameters.TempFiles);
        this.compiler.ProcessErrors(project.CompilerParameters, results, errors);
        return results;
      }
      return this.CompileProject(project, errors);
    }
    public virtual CompilerResults CompileProject(Project project, Cci.ErrorNodeList errors){
      if (project == null){Debug.Assert(false); return null;}
      CompilerResults results;
      Cci.Compilation symbolTableCompilation = project.Compilation;
      try {
        Cci.Compilation buildCompilation = this.compiler.CurrentCompilation = symbolTableCompilation.CloneCompilationUnits();
        buildCompilation.TargetModule = this.compiler.CreateModule(project.CompilerParameters, errors);
        this.compiler.CurrentCompilation = buildCompilation;
        if (project.CompilerParameters is Cci.CompilerOptions && 
          ((Cci.CompilerOptions)project.CompilerParameters).EmitManifest)
          results = this.compiler.CompileAssemblyFromIR(buildCompilation, errors);
        else
          results = this.compiler.CompileModuleFromIR(buildCompilation, errors);
        if (buildCompilation.TargetModule is Cci.AssemblyNode && symbolTableCompilation.TargetModule is Cci.AssemblyNode)
          ((Cci.AssemblyNode)symbolTableCompilation.TargetModule).Version = ((Cci.AssemblyNode)buildCompilation.TargetModule).Version;
        buildCompilation.TargetModule = null;
        buildCompilation.CompilationUnits = null;
        buildCompilation = null;
      }catch (Exception e){
        results = new CompilerResults(options.TempFiles);
        string offendingAssembly = project.CompilerParameters == null ? "" : project.CompilerParameters.OutputAssembly;
        results.Errors.Add(new CompilerError(offendingAssembly, 1, 1, "", "Internal Compiler Error: " + e.ToString() + "\n"));
        results.NativeCompilerReturnValue = 1;
      }finally{
        this.compiler.CurrentCompilation = symbolTableCompilation;
      }
      return results;
    }
    public override string GetFullyQualifiedNameForReferencedLibrary(Microsoft.VisualStudio.Package.ProjectOptions options, string rLibraryName){
      return this.compiler.GetFullyQualifiedNameForReferencedLibrary(
        new Microsoft.VisualStudio.IntegrationHelper.CompilerOptions(options), rLibraryName);
    }
    public override bool MustCopyReferencedAssembly(string path){
      string assemblyDir = Path.GetDirectoryName(path);
      if (string.Compare(assemblyDir, Path.GetDirectoryName(Cci.SystemTypes.SystemAssembly.Location), true, System.Globalization.CultureInfo.InvariantCulture) == 0)
        return false;        
      Uri uri = null;
      try{
        uri = new Uri(path);
      }catch(Exception){}
      if (uri != null && Cci.GlobalAssemblyCache.Contains(uri))
        return false;
      return true;
    }
  }
  public class Scanner : IScanner{
    public Cci.Scanner scScanner;

    public Scanner(Cci.Scanner scanner){
      this.scScanner = scanner;
    }

    public virtual void SetSource(string source, int offset){
      this.scScanner.SetSource(source, offset);
    }
    public virtual bool ScanTokenAndProvideInfoAboutIt(TokenInfo tokenInfo, ref int state){
      Cci.TokenInfo scTokenInfo = new Cci.TokenInfo();
      bool result = this.scScanner.ScanTokenAndProvideInfoAboutIt(scTokenInfo, ref state);
      tokenInfo.color = (TokenColor)scTokenInfo.color;
      tokenInfo.endIndex = scTokenInfo.endIndex;
      tokenInfo.startIndex = scTokenInfo.startIndex;
      tokenInfo.trigger = (TokenTrigger)scTokenInfo.trigger;
      tokenInfo.type = (TokenType)scTokenInfo.type;
      return result;
    }
  }
  public class SourceContext : Microsoft.VisualStudio.Package.SourceContext{
    public SourceContext(Cci.SourceContext scContext)
      : base(scContext.StartLine-1, scContext.StartColumn-1, scContext.EndLine-1, scContext.EndColumn-1, 
        scContext.Document == null ? null : scContext.Document.Name){
    }
  }
  public class TypeAndMemberDropdownBars : Microsoft.VisualStudio.Package.TypeAndMemberDropdownBars{
    private Cci.TypeAndMemberDropdownBars scTypeAndMemberDropdownBars;
    private GlyphProvider glyphProvider;

    public TypeAndMemberDropdownBars(LanguageService languageService, GlyphProvider glyphProvider) 
      : base(languageService){
      this.scTypeAndMemberDropdownBars = new Cci.TypeAndMemberDropdownBars(languageService.scLanguageService);
      this.glyphProvider = glyphProvider;
    }
    public string GetFileName(IVsTextView view){
      if (view == null) return null;
      string fname = null;
      try{
        uint formatIndex;
        IVsTextLines pBuffer;
        view.GetBuffer(out pBuffer);
        IPersistFileFormat pff = (IPersistFileFormat)pBuffer;
        pff.GetCurFile(out fname, out formatIndex);
        pff = null;
        pBuffer = null;
      }catch{}
      return fname;
    }
    public override void SynchronizeDropdowns(IVsTextView textView, int line, int col){
      string fname = this.GetFileName(textView); if (fname == null) return;
      this.scTypeAndMemberDropdownBars.SynchronizeDropdowns(fname, line, col);
      this.textView = textView;
      this.dropDownMemberSignatures = this.scTypeAndMemberDropdownBars.dropDownMemberSignatures;
      this.PopulateTypeNamesPositionsAndGlyphs();
      this.PopulateMemberPositionsAndGlyphs();
      this.selectedMember = this.scTypeAndMemberDropdownBars.selectedMember;
      this.selectedType = this.scTypeAndMemberDropdownBars.selectedType;
    }
    private void PopulateTypeNamesPositionsAndGlyphs(){
      Cci.TypeNodeList types = this.scTypeAndMemberDropdownBars.sortedDropDownTypes;
      Cci.AuthoringHelper helper = this.scTypeAndMemberDropdownBars.languageService.GetAuthoringHelper();
      int n = types == null ? 0 : types.Count;
      string[] typeNames = new string[n];
      int[] startColumns = new int[n];
      int[] startLines = new int[n];
      int[] glyphs = new int[n];
      for (int i = 0; i < n; i++){
        Cci.TypeNode type = types[i]; 
        if (type == null || type.Name == null){Debug.Assert(false); continue;}
        typeNames[i] = helper.GetFullTypeName(type);
        startColumns[i] = type.Name.SourceContext.StartColumn-1;
        startLines[i] = type.Name.SourceContext.StartLine-1;
        glyphs[i] = this.glyphProvider.GetGlyph(type);
      }
      this.dropDownTypeNames = typeNames;
      this.dropDownTypeStartColumns = startColumns;
      this.dropDownTypeStartLines = startLines;
      this.dropDownTypeGlyphs = glyphs;
    }
    private void PopulateMemberPositionsAndGlyphs(){
      Cci.MemberList members = this.scTypeAndMemberDropdownBars.sortedDropDownMembers;
      int n = members == null ? 0 : members.Count;
      int[] startColumns = new int[n];
      int[] startLines = new int[n];
      int[] glyphs = new int[n];
      for (int i = 0; i < n; i++){
        Cci.Member member = members[i]; 
        if (member == null || member.Name == null) continue;
        startColumns[i] = member.Name.SourceContext.StartColumn-1;
        startLines[i] = member.Name.SourceContext.StartLine-1;
        glyphs[i] = this.glyphProvider.GetGlyph(member);
      }
      this.dropDownMemberStartColumns = startColumns;
      this.dropDownMemberStartLines = startLines;
      this.dropDownMemberGlyphs = glyphs;
    }
  }
}