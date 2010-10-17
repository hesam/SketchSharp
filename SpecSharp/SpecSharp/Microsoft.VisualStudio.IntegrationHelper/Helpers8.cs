using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml;

namespace Microsoft.VisualStudio.IntegrationHelper{
  public class AuthoringScope : Microsoft.VisualStudio.Package.AuthoringScope{
    public System.Compiler.AuthoringScope scAuthoringScope;
    public GlyphProvider glyphProvider;

    public AuthoringScope(System.Compiler.AuthoringScope authoringScope, GlyphProvider glyphProvider){
      this.scAuthoringScope = authoringScope;
      this.glyphProvider = glyphProvider;
    }
    public override string GetDataTipText(int line, int col, out TextSpan span){
      System.Compiler.SourceContext ctx;
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
    public override Microsoft.VisualStudio.Package.Declarations GetDeclarations(IVsTextView view, int line, int col, TokenInfo info, ParseReason reason){
      System.Compiler.Declarations scDeclarations = this.scAuthoringScope.GetDeclarations(line, col);
      return new Declarations(scDeclarations, this.glyphProvider);
    }
    public override Microsoft.VisualStudio.Package.Methods GetMethods(int line, int col, string name){
      System.Compiler.Methods scMethods = this.scAuthoringScope.GetMethods(line, col, System.Compiler.Identifier.For(name));
      if (scMethods == null) return null;
      return new Methods(scMethods);
    }
    public override string Goto(VsCommands cmd, IVsTextView textView, int line, int col, out TextSpan span){
      span = new TextSpan();
      System.Compiler.SourceContext targetPosition = new System.Compiler.SourceContext();
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
  public class AuthoringSink : System.Compiler.AuthoringSink{
    public Microsoft.VisualStudio.Package.AuthoringSink vsAuthoringSink;
    public ArrayList CollapsibleRegions;

    public AuthoringSink(Microsoft.VisualStudio.Package.AuthoringSink vsAuthoringSink){
      Debug.Assert(vsAuthoringSink != null);
      this.vsAuthoringSink = vsAuthoringSink;
    }

    public override void AddCollapsibleRegion(System.Compiler.SourceContext context){
      if (this.CollapsibleRegions == null) this.CollapsibleRegions = new ArrayList();
      this.CollapsibleRegions.Add(context);
    }
    public override void AddError(System.Compiler.ErrorNode node){
      if (node == null || node.SourceContext.Document == null) return;
      this.vsAuthoringSink.AddError(node.SourceContext.Document.Name, node.GetMessage(), SourceContext.AsTextSpan(node.SourceContext), this.GetSeverity(node));
    }
    public virtual Severity GetSeverity(System.Compiler.ErrorNode node){
      if (node == null){Debug.Assert(false); return Severity.Fatal;}
      if (node.Severity == 0) return Severity.Error;
      return Severity.Warning;
    }
    public override void AutoExpression(System.Compiler.SourceContext exprContext){
      this.vsAuthoringSink.AutoExpression(SourceContext.AsTextSpan(exprContext));
    }
//    private System.Compiler.SourceContext regionToClear;
//    public override System.Compiler.SourceContext RegionToClear{
//      get{
//        return this.regionToClear;
//      }
//      set{
//        this.vsAuthoringSink.RegionToClear = new SourceContext(value);
//        this.regionToClear = value;
//      }
//    }
    public override void CodeSpan(System.Compiler.SourceContext spanContext){
      this.vsAuthoringSink.CodeSpan(SourceContext.AsTextSpan(spanContext));
    }
    public override void EndParameters(System.Compiler.SourceContext context){
      this.vsAuthoringSink.EndParameters(SourceContext.AsTextSpan(context));
    }
    public override void MatchPair(System.Compiler.SourceContext startContext, System.Compiler.SourceContext endContext){
      this.vsAuthoringSink.MatchPair(SourceContext.AsTextSpan(startContext), SourceContext.AsTextSpan(endContext), 1);
    }
    public override void MatchTriple(System.Compiler.SourceContext startContext, System.Compiler.SourceContext middleContext, System.Compiler.SourceContext endContext){
      this.vsAuthoringSink.MatchTriple(SourceContext.AsTextSpan(startContext), SourceContext.AsTextSpan(middleContext), SourceContext.AsTextSpan(endContext), 1);
    }
    public override void NextParameter(System.Compiler.SourceContext context){
      this.vsAuthoringSink.NextParameter(SourceContext.AsTextSpan(context));
    }
    public override void QualifyName(System.Compiler.SourceContext selectorContext, System.Compiler.Identifier name){
      if (name == null) return;
      this.vsAuthoringSink.QualifyName(SourceContext.AsTextSpan(selectorContext), SourceContext.AsTextSpan(name.SourceContext), name.ToString());
    }
    public override void StartName(System.Compiler.Identifier name){
      if (name == null) return;
      this.vsAuthoringSink.StartName(SourceContext.AsTextSpan(name.SourceContext), name.ToString());
    }
    public override void StartParameters(System.Compiler.SourceContext context){
      this.vsAuthoringSink.StartParameters(SourceContext.AsTextSpan(context));
    }
  }
  public class CodeWindowManager : Microsoft.VisualStudio.Package.CodeWindowManager{
    public GlyphProvider glyphProvider;
    public CodeWindowManager(LanguageService service, IVsCodeWindow codeWindow, Source source, GlyphProvider glyphProvider)
      : base(service, codeWindow, source){
      this.glyphProvider = glyphProvider;
    }
  }
  public class CompilerOptions : System.Compiler.CompilerOptions{
    public CompilerOptions(Microsoft.VisualStudio.Package.ProjectOptions compilerOptions){
      this.AdditionalSearchPaths = new System.Compiler.StringList(compilerOptions.AdditionalSearchPaths);
      this.AllowUnsafeCode = compilerOptions.AllowUnsafeCode;
      this.BaseAddress = compilerOptions.BaseAddress;
      this.BugReportFileName = compilerOptions.BugReportFileName;
      this.CheckedArithmetic = compilerOptions.CheckedArithmetic;
      this.CodePage = compilerOptions.CodePage;
      this.CompileAndExecute = compilerOptions.CompileAndExecute;
      this.DefinedPreProcessorSymbols = new System.Compiler.StringList(compilerOptions.DefinedPreProcessorSymbols);
      this.DisplayCommandLineHelp = compilerOptions.DisplayCommandLineHelp;
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
      foreach (string lresource in compilerOptions.LinkedResources) this.LinkedResources.Add(lresource);
      this.MainClass = compilerOptions.MainClass;
      this.ModuleKind = (System.Compiler.ModuleKindFlags)compilerOptions.ModuleKind;
      this.NoStandardLibrary = compilerOptions.NoStandardLibrary;
      this.Optimize = compilerOptions.Optimize;
      this.OutputAssembly = Path.GetFileNameWithoutExtension(compilerOptions.OutputAssembly);
      this.OutputPath = Path.GetDirectoryName(compilerOptions.OutputAssembly); //TODO: look at project code
      this.PDBOnly = compilerOptions.PDBOnly;
      this.RecursiveWildcard = compilerOptions.RecursiveWildcard;
      foreach (string refAssem in compilerOptions.ReferencedAssemblies) this.ReferencedAssemblies.Add(refAssem);
      this.ReferencedModules = new System.Compiler.StringList(compilerOptions.ReferencedModules);
      this.RootNamespace = compilerOptions.RootNamespace;
//      this.StandardLibraryLocation = compilerOptions.StandardLibraryLocation;
      this.SuppressedWarnings = new System.Compiler.Int32List(compilerOptions.SuppressedWarnings);
      this.SuppressLogo = compilerOptions.SuppressLogo;
      this.TargetPlatform = (System.Compiler.PlatformType)compilerOptions.TargetPlatform;
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
    public System.Compiler.Declarations scDeclarations;
    public GlyphProvider glyphProvider;

    public Declarations(System.Compiler.Declarations scDeclarations, GlyphProvider glyphProvider){
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

    public override string GetDisplayText(int index){
      System.Compiler.Member member = this.scDeclarations.GetMember(index);
      if (member == null) return "";
      return member.Name.ToString();
    }

    public override int GetGlyph(int index){
      System.Compiler.Member member = this.scDeclarations.GetMember(index);
      return this.glyphProvider.GetGlyph(member);
    }
    public override string GetName(int index){
      return this.scDeclarations.GetName(index);
    }
    public override bool IsCommitChar(string textSoFar, int selected, char commitChar) {
      return this.scDeclarations.IsCommitChar(textSoFar, commitChar);
    }
    public override string OnCommit(IVsTextView textView, string textSoFar, char commitCharacter, int index, ref TextSpan initialExtent) {
      return this.scDeclarations.GetName(index);  //REVIEW: huh?
    }
  }
  public class GlyphProvider{
    protected const int IconGroupSize = 6; // does not get imported into the type lib.
    public virtual int GetGlyph(System.Compiler.Member member){
      ScopeIconGroup group;
      if (member is System.Compiler.Method) {
        if (member.IsSpecialName && member.IsStatic && member.Name.ToString().StartsWith("op_"))
          group = ScopeIconGroup.IconGroupFormula;
        else
          group = ScopeIconGroup.IconGroupMethod;
      } else if (member is System.Compiler.Property)
        group = ScopeIconGroup.IconGroupProperty;
      else if (member is System.Compiler.Class)
        group = ScopeIconGroup.IconGroupClass;
      else if (member is System.Compiler.DelegateNode)
        group = ScopeIconGroup.IconGroupDelegate;
      else if (member is System.Compiler.Event)
        group = ScopeIconGroup.IconGroupEvent;
      else if (member is System.Compiler.EnumNode)
        group = ScopeIconGroup.IconGroupEnum;
      else if (member is System.Compiler.Interface)
        group = ScopeIconGroup.IconGroupInterface;
      else if (member is System.Compiler.Struct)
        group = ScopeIconGroup.IconGroupStruct;
      else if (member is System.Compiler.Namespace)
        group = ScopeIconGroup.IconGroupNameSpace;
      else if (member is System.Compiler.Field) {
        if (member.DeclaringType is System.Compiler.EnumNode)
          group = ScopeIconGroup.IconGroupEnumConst;
        else if (((System.Compiler.Field)member).IsLiteral || ((System.Compiler.Field)member).IsInitOnly)
          group = ScopeIconGroup.IconGroupFieldRed;
        else
          group = ScopeIconGroup.IconGroupFieldBlue;
      } else
        return (int)ScopeIconMisc.IconBlackBox;
      int glyph = (int)group * IconGroupSize;
      if (member.IsPublic)
        glyph += (int)ScopeIconItem.IconItemPublic;
      else if (member.IsFamily || member.IsFamilyAndAssembly || member.IsFamilyOrAssembly)
        glyph += (int)ScopeIconItem.IconItemProtected;
      else if (member.IsAssembly)
        glyph += (int)ScopeIconItem.IconItemInternal;
      else if (!(member is System.Compiler.Namespace))
        glyph += (int)ScopeIconItem.IconItemPrivate;
      return glyph;
    }
  }
  public enum ScopeIconGroup {
    IconGroupClass = 0,
    IconGroupType,
    IconGroupDelegate,
    IconGroupEnum,
    IconGroupEnumConst,
    IconGroupEvent,
    IconGroupResource,
    IconGroupFieldBlue,
    IconGroupInterface,
    IconGroupTextLine,
    IconGroupScript,
    IconGroupScript2,
    IconGroupMethod,
    IconGroupMethod2,
    IconGroupDiagram,
    IconGroupNameSpace,
    IconGroupFormula,
    IconGroupProperty,
    IconGroupStruct,
    IconGroupTemplate,
    IconGroupOpenSquare,
    IconGroupBits,
    IconGroupChannel,
    IconGroupFieldRed,
    IconGroupUnion,
    IconGroupForm,
    IconGroupFieldYellow,
    IconGroupMisc1,
    IconGroupMisc2,
    IconGroupMisc3
  }
  public enum ScopeIconItem {
    IconItemPublic,
    IconItemInternal,
    IconItemSpecial,
    IconItemProtected,
    IconItemPrivate,
    IconItemShortCut,
    IconItemNormal = IconItemPublic
  }
  public enum ScopeIconMisc {
    IconBlackBox = 162,   /* (IconGroupMisc1 * IconGroupSize) */
    IconLibrary,
    IconProgram,
    IconWebProgram,
    IconProgramEmpty,
    IconWebProgramEmpty,

    IconComponents,
    IconEnvironment,
    IconWindow,
    IconFolderOpen,
    IconFolder,
    IconArrowRight,

    IconAmbigious,
    IconShadowClass,
    IconShadowMethodPrivate,
    IconShadowMethodProtected,
    IconShadowMethod,
    IconInCompleteSource
  };
  public abstract class LanguageService : Microsoft.VisualStudio.Package.LanguageService{
    public System.Compiler.LanguageService scLanguageService;
    public GlyphProvider glyphProvider;
    public IScanner vsScanner;
    public bool EnableDropDownCombos = true;
    public Hashtable projectManagerFor = new Hashtable();

		public LanguageService(System.Compiler.LanguageService scLanguageService, GlyphProvider glyphProvider)
      : base(){
      this.scLanguageService = scLanguageService;
      this.glyphProvider = glyphProvider;
      this.vsScanner = new Microsoft.VisualStudio.IntegrationHelper.Scanner(this.scLanguageService.GetScanner());
      this.scLanguageService.GetCompilationFor = new System.Compiler.LanguageService.GetCompilation(this.GetCompilationFor);
    }

    public virtual void AssociateFileNameWithProjectManager(IVsCodeWindow codeWindow, string fileName){
      if (codeWindow == null || fileName == null) return;
      IVsTextViewEx tvx = codeWindow as IVsTextViewEx;
      if (tvx == null) return;
      object frame;
      tvx.GetWindowFrame(out frame);
      IVsWindowFrame vswFrame = frame as IVsWindowFrame;
      if (vswFrame == null) return;
      object pmgr;
      vswFrame.GetProperty((int)__VSFPROPID.VSFPROPID_Hierarchy, out pmgr);
      ProjectManager projMgr = pmgr as ProjectManager;
      if (projMgr == null) return;
      this.projectManagerFor[fileName] = new WeakReference(projMgr);
    }
    public override Microsoft.VisualStudio.Package.TypeAndMemberDropdownBars CreateDropDownHelper(IVsTextView forView){
      if (!this.EnableDropDownCombos) return null;
      return new TypeAndMemberDropdownBars(this, this.glyphProvider);
    }

    public override Microsoft.VisualStudio.Package.CodeWindowManager CreateCodeWindowManager(
      Microsoft.VisualStudio.TextManager.Interop.IVsCodeWindow codeWindow,
      Microsoft.VisualStudio.Package.Source source){
      this.AssociateFileNameWithProjectManager(codeWindow, source.GetFilePath());
      return new CodeWindowManager(this, codeWindow, source, glyphProvider);
    }
    public override void GetCommentFormat(ref CommentInfo info) {
      System.Compiler.CommentInfo scCommentInfo = new System.Compiler.CommentInfo();
      this.scLanguageService.GetCommentFormat(scCommentInfo);
      info.BlockEnd = scCommentInfo.blockEnd;
      info.BlockStart = scCommentInfo.blockStart;
      info.LineStart = scCommentInfo.lineStart;
      info.Supported = scCommentInfo.supported;
      info.UseLineComments = scCommentInfo.useLineComments;
    }
    public override int GetItemCount(out int count) {
      count = this.scLanguageService.GetColorCount();
      return Microsoft.VisualStudio.NativeMethods.S_OK;
    }
    public virtual System.Compiler.Compilation GetCompilationFor(string fileName){
      WeakReference wref = (WeakReference)this.projectManagerFor[fileName];
      if (wref != null && wref.IsAlive){
        ProjectManager projectManager = (ProjectManager)wref.Target;
        if (projectManager != null) return projectManager.GetCompilerProject().Compilation;
      }
      return this.scLanguageService.GetDummyCompilationFor(fileName);
    }
    public override IScanner GetScanner(IVsTextLines buffer) {
      return this.vsScanner;
    }

    public override Microsoft.VisualStudio.Package.AuthoringScope ParseSource(ParseRequest req){
      return this.ParseSource(req.Text, req.Line, req.Col, req.FileName, req.Sink, req.Reason);
    }

    public virtual Microsoft.VisualStudio.Package.AuthoringScope ParseSource(string text, int line, int col, string fname, 
      Microsoft.VisualStudio.Package.AuthoringSink aSink, Microsoft.VisualStudio.Package.ParseReason reason){
      System.Compiler.AuthoringSink scAsink = new AuthoringSink(aSink);
      System.Compiler.AuthoringScope scAuthScope = 
        this.scLanguageService.ParseSource(text, line, col, fname, scAsink, (System.Compiler.ParseReason)reason);
      return new AuthoringScope(scAuthScope, this.glyphProvider);
    }
//    public override NewOutlineRegion[] GetCollapsibleRegions(string text, string fname){
//      System.Compiler.ParseReason reason = System.Compiler.ParseReason.CollapsibleRegions;
//      Microsoft.VisualStudio.Package.AuthoringSink aSink = new Microsoft.VisualStudio.Package.AuthoringSink(0, 0, 0);
//      AuthoringSink scAsink = new AuthoringSink(aSink);
//      this.scLanguageService.ParseSource(text, 0, 0, fname, scAsink, reason);
//      if (scAsink.CollapsibleRegions == null) return null;
//      int n = scAsink.CollapsibleRegions.Count;
//      NewOutlineRegion[] result = new NewOutlineRegion[n];
//      for (int i = 0; i < n; i++){
//        System.Compiler.SourceContext ctx = (System.Compiler.SourceContext)scAsink.CollapsibleRegions[i];
//        TextSpan span = new TextSpan();
//        span.iStartIndex = ctx.StartColumn-1;
//        span.iStartLine = ctx.StartLine-1;
//        span.iEndIndex = ctx.EndColumn-1;
//        span.iEndLine = ctx.EndLine-1;
//        NewOutlineRegion region = new NewOutlineRegion();
//        region.tsHiddenText = span;
//        result[i] = region;
//      }
//      return result;
//    }
	}
  public class Methods : Microsoft.VisualStudio.Package.Methods{
    public System.Compiler.Methods scMethods;
    public Methods(System.Compiler.Methods scMethods){
      Debug.Assert(scMethods != null);
      this.scMethods = scMethods;
    }
    
    public override int GetCount(){
      return this.scMethods.GetCount();
    }
    public override string GetDescription(int index){
      return this.scMethods.GetDescription(index);
    }
    public override string GetName(){
      return this.scMethods.GetName();
    }
    public override int GetParameterCount(int index){
      return this.scMethods.GetParameterCount(index);
    }
    public override void GetParameterInfo(int index, int parameter, out string name, out string display, out string description){
      this.scMethods.GetParameterInfo(index, parameter, out name, out display, out description);
    }
    public override string GetType(int index){
      return this.scMethods.GetType(index);
    }
  }
  public class Package : Microsoft.VisualStudio.Shell.Package {
    public virtual void RegisterLanguageService(Microsoft.VisualStudio.IntegrationHelper.LanguageService languageService){
      if (languageService == null) throw new ArgumentNullException();
      languageService.SetSite(this);
      ((IServiceContainer)this).AddService(languageService.GetType(), languageService, true);
      int lcid = this.GetProviderLocale();
      languageService.scLanguageService.culture = lcid == 0 ? CultureInfo.InvariantCulture : new CultureInfo(lcid);
    }
  }
  public class Project {
    /// <summary>
    /// The compilation parameters that are used for this compilation.
    /// </summary>
    public System.CodeDom.Compiler.CompilerParameters CompilerParameters;
    /// <summary>
    /// The paths to the artifacts (eg. files) in which the source texts are stored. Used together with IndexForFullPath.
    /// </summary>
    public System.Compiler.StringList FullPathsToSources;
    /// <summary>
    /// A scope for symbols that belong to the compilation as a whole. No C# equivalent. Null if not applicable.
    /// </summary>
    public System.Compiler.Scope GlobalScope;
    /// <summary>
    /// Set to true if this project has been constructed automatically to contain a single file that is being edited out of context.
    /// In such cases it does not makes sense to produce semantic errors, since assembly references will be missing, as will other
    /// source files that need to be compiled together with the single file in the dummy project.
    /// </summary>
    public bool IsDummy;
    /// <summary>
    /// The source texts to be compiled together with the appropriate parser to call and possibly the resulting analyzed abstract syntax tree.
    /// </summary>
    public System.Compiler.CompilationUnitSnippetList CompilationUnitSnippets;
    /// <summary>
    /// A queriable collection of all the types in the parse trees. When the project is compiled, this also represents
    /// compilation output. 
    /// </summary>
    public System.Compiler.Module SymbolTable;
    public System.Compiler.Compilation Compilation;
    /// <summary>
    /// Helps client code keep track of when the project was last modified.
    /// </summary>
    public DateTime LastModifiedTime = DateTime.Now;
    /// <summary>
    /// An index over FullPathsToSources. Allows quick retrieval of sources and parse trees, given the path.
    /// </summary>
    public System.Compiler.TrivialHashtable IndexForFullPath = new System.Compiler.TrivialHashtable();

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
  public abstract class ProjectManager : Microsoft.VisualStudio.Package.Project{
    protected System.Compiler.Compiler compiler;
    protected Project scProject;
    protected XmlElement lastConfig;
//    protected System.Compiler.Compiler.UpdateEventHandler updateHandler;

    public ProjectManager(){
      this.scProject = new Project(); 
      this.LastModifiedTime = scProject.LastModifiedTime.AddTicks(1);
//      this.updateHandler = new System.Compiler.Compiler.UpdateEventHandler(this.HandleReferencedCompilationUpdate);
    }
//    public override CompilerResults CompileAssemblyFromFile(ProjectOptions options, string filename){
//      return this.compiler.CompileAssemblyFromFile(this.GetCompilerOptions(options), filename);
//    }
//    public override CompilerResults CompileAssemblyFromFileBatch(ProjectOptions options, string[] fileNames){
//      return this.compiler.CompileAssemblyFromFileBatch(this.GetCompilerOptions(options), fileNames);
//    }
    public virtual System.Compiler.CompilerOptions GetCompilerOptions(Microsoft.VisualStudio.Package.ProjectOptions projectOptions){
      return new CompilerOptions(projectOptions);
    }
//    public void HandleReferencedCompilationUpdate(System.Compiler.UpdateSpecification updateSpecification, System.Compiler.MemberList changedMembers){
//    }
//    public virtual bool CompileIfNeeded(AuthoringSink sink){
//      if (sink == null) return true;
//      System.Compiler.ErrorNodeList errors = new System.Compiler.ErrorNodeList();
//      XmlElement config = this.GetActiveConfiguration();
//      Project project = this.GetCompilerProject(config, errors);
//      if (this.ProcessErrors(sink, errors)) return true;
//      if (project == null) return false;
//      if (project.Compilation == null || project.Compilation.TargetModule.IsNormalized) return false;
//      System.Compiler.CompilationList referencedCompilations = this.GetReferencedCompilations(config, errors);
//      if (this.ProcessErrors(sink, errors)) return true;
//      for (int i = 0, n = referencedCompilations == null ? 0 : referencedCompilations.Length; i < n; i++){
//        System.Compiler.Compilation rcomp = referencedCompilations[i];
//        if (rcomp == null) continue;
//        if (rcomp.TargetModule == null || rcomp.TargetModule.IsNormalized) continue;
//        return false; //Caller will eventually compile the referenced compilation
//      }
//      //At this point all the compilations referenced by this one have been compiled already
//      this.CompileProject(project, errors);
//      this.ProcessErrors(sink, errors);
//      return true;
//    }
    public virtual bool ProcessErrors(AuthoringSink sink, System.Compiler.ErrorNodeList errors) {
      if (sink == null) { Debug.Assert(false); return true; }
      if (errors == null || errors.Length <= 0) return false;
      for (int i = 0, n = errors.Length; i < n; i++) sink.AddError(errors[i]);
      return true;
    }
    public virtual Project GetCompilerProject(){
//      return this.GetCompilerProject(this.GetActiveConfiguration(), new System.Compiler.ErrorNodeList());
      return null;
    }
//    public virtual Project GetCompilerProject(XmlElement config, System.Compiler.ErrorNodeList errors){
//      lock(ProjectManager.BuildLock){
//        Project project = this.scProject;
//        if (project == null) {Debug.Assert(false); return null;}
//        bool refresh = this.LastModifiedTime != project.LastModifiedTime || this.lastConfig != config;
//        if (!refresh) return project;
//        System.Compiler.Compilation compilation = project.Compilation = new System.Compiler.Compilation();
//        project.LastModifiedTime = this.LastModifiedTime;
//        this.lastConfig = config;
//        Microsoft.VisualStudio.Package.ProjectOptions vscOptions = this.GetProjectOptions(config);
//        System.Compiler.CompilerOptions options = this.GetCompilerOptions(vscOptions);
//        if (options == null){Debug.Assert(false); return null;}
//        project.CompilerParameters = options;
//        string dir = this.ProjectFolder;
//        XmlDocument doc = this.projFile;
//        System.Compiler.TrivialHashtable indexForFileName = new System.Compiler.TrivialHashtable();
//        System.Compiler.StringList fileNames = new System.Compiler.StringList();
//        System.Compiler.CompilationUnitSnippetList cuSnippets = new System.Compiler.CompilationUnitSnippetList();
//        System.Compiler.CompilationUnitList cUnits = new System.Compiler.CompilationUnitList();
//        foreach (XmlElement e in doc.SelectNodes("//Files/Include/File")){ 
//          if (e == null) continue;
//          string relpath = e.GetAttribute("RelPath");
//          string file = Path.Combine(dir, relpath);
//          System.Compiler.CompilationUnitSnippet snippet = null;
//          int key = System.Compiler.Identifier.For(file).UniqueKey;
//          object index = project.IndexForFullPath[key];
//          if (index is int){
//            snippet = project.CompilationUnitSnippets[(int)index];
//          }else{
//            if (!File.Exists(file)) continue;
//            snippet = this.GetCompilationUnitSnippetFor(file, e.GetAttribute("BuildAction"));
//          }
//          if (snippet == null){Debug.Assert(false); continue;}
//          indexForFileName[key] = fileNames.Length;
//          fileNames.Add(file);
//          cuSnippets.Add(snippet);
//          cUnits.Add(snippet);
//          snippet.Compilation = compilation;
//        }
//        project.CompilationUnitSnippets = cuSnippets;
//        project.FullPathsToSources = fileNames;
//        project.IndexForFullPath = indexForFileName;
//        compilation.CompilationUnits = cUnits;
//        compilation.CompilerParameters = options;
//        compilation.ReferencedCompilations = this.GetReferencedCompilations(config, errors);
//        this.compiler.ConstructSymbolTable(compilation, errors);
//        project.SymbolTable = compilation.TargetModule;
//        return project;
//      }
//    }
//    public virtual System.Compiler.CompilationList GetReferencedCompilations(XmlElement config, System.Compiler.ErrorNodeList errors){
//      ArrayList refProjs = this.GetReferencedProjects(config);
//      int n = refProjs == null ? 0 : refProjs.Count;
//      if (n == 0) return null;
//      System.Compiler.CompilationList result = new System.Compiler.CompilationList(n);
//      for (int i = 0; i < n; i++){
//        ProjectManager proj = refProjs[i] as ProjectManager;
//        if (proj == null){Debug.Assert(false); continue;}
////        proj.compiler.OnUpdate += this.updateHandler;
//        XmlElement pconfig = proj.GetActiveConfiguration();
//        Project cproj = proj.GetCompilerProject(pconfig, errors);
//        if (cproj == null || cproj.Compilation == null){Debug.Assert(false); continue;}
//        result.Add(cproj.Compilation);
//      }
//      return result;
//    }
    public abstract System.Compiler.CompilationUnitSnippet GetCompilationUnitSnippetFor(string fullPathToSource, string buildAction);
//    public override CompilerResults CompileProject(XmlElement config){
//      System.Compiler.ErrorNodeList errors = new System.Compiler.ErrorNodeList();
//      Project project = this.GetCompilerProject(config, errors);
//      if (project == null) return null;
//      return this.CompileProject(project, errors);
//    }
    public virtual CompilerResults CompileProject(Project project, System.Compiler.ErrorNodeList errors){
      CompilerResults results;
      try{
        project.Compilation.TargetModule = this.compiler.CreateModule(project.CompilerParameters, errors);
        if (project.CompilerParameters is System.Compiler.CompilerOptions && 
          ((System.Compiler.CompilerOptions)project.CompilerParameters).EmitManifest)
          results = this.compiler.CompileAssemblyFromIR(project.Compilation, errors);
        else
          results = this.compiler.CompileModuleFromIR(project.Compilation, errors);
      }catch (Exception e){
        results = new CompilerResults(options.TempFiles);
        results.Errors.Add(new CompilerError(project.CompilerParameters.OutputAssembly, 1, 1, "", "Internal Compiler Error: " + e.ToString() + "\n"));
        results.NativeCompilerReturnValue = 1;
      }
      return results;
    }
    public override string GetFullyQualifiedNameForReferencedLibrary(Microsoft.VisualStudio.Package.ProjectOptions options, string rLibraryName){
      return this.compiler.GetFullyQualifiedNameForReferencedLibrary(
        new Microsoft.VisualStudio.IntegrationHelper.CompilerOptions(options), rLibraryName);
    }
//    public override bool MustCopyReferencedAssembly(string path){
//      string assemblyDir = Path.GetDirectoryName(path);
//      if (string.Compare(assemblyDir, Path.GetDirectoryName(System.Compiler.SystemTypes.SystemAssembly.Location), true, System.Globalization.CultureInfo.InvariantCulture) == 0)
//        return false;        
//      Uri uri = null;
//      try{
//        uri = new Uri(path);
//      }catch(Exception){}
//      if (uri != null && System.Compiler.GlobalAssemblyCache.Contains(uri))
//        return false;
//      return true;
//    }
  }
  public class Scanner : IScanner{
    public System.Compiler.Scanner scScanner;

    public Scanner(System.Compiler.Scanner scanner){
      this.scScanner = scanner;
    }

    public virtual void SetSource(string source, int offset){
      this.scScanner.SetSource(source, offset);
    }
    public virtual bool ScanTokenAndProvideInfoAboutIt(TokenInfo tokenInfo, ref int state){
      System.Compiler.TokenInfo scTokenInfo = new System.Compiler.TokenInfo();
      bool result = this.scScanner.ScanTokenAndProvideInfoAboutIt(scTokenInfo, ref state);
      tokenInfo.Color = (TokenColor)scTokenInfo.color;
      tokenInfo.EndIndex = scTokenInfo.endIndex;
      tokenInfo.StartIndex = scTokenInfo.startIndex;
      tokenInfo.Trigger = (TokenTriggers)scTokenInfo.trigger;
      tokenInfo.Type = (TokenType)scTokenInfo.type;
      return result;
    }
  }
  public static class SourceContext{
    public static TextSpan AsTextSpan(System.Compiler.SourceContext scContext){
      TextSpan span;
      span.iStartLine = scContext.StartLine-1;
      span.iStartIndex = scContext.StartColumn-1;
      span.iEndLine = scContext.EndLine-1;
      span.iEndIndex = scContext.EndColumn-1;
      return span;
    }
  }
  public class TypeAndMemberDropdownBars : Microsoft.VisualStudio.Package.TypeAndMemberDropdownBars{
    private System.Compiler.TypeAndMemberDropdownBars scTypeAndMemberDropdownBars;
    private GlyphProvider glyphProvider;

    public TypeAndMemberDropdownBars(LanguageService languageService, GlyphProvider glyphProvider) 
      : base(languageService){
      this.scTypeAndMemberDropdownBars = new System.Compiler.TypeAndMemberDropdownBars(languageService.scLanguageService);
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

    public override bool OnSynchronizeDropdowns(Microsoft.VisualStudio.Package.LanguageService languageService, IVsTextView textView, 
      int line, int col, ArrayList dropDownTypes, ArrayList dropDownMembers, ref int selectedType, ref int selectedMember){
      selectedMember = 0; selectedType = 0;
      string fname = this.GetFileName(textView); if (fname == null) return false;
      this.scTypeAndMemberDropdownBars.SynchronizeDropdowns(fname, line, col);
      this.GetDropDownTypes(dropDownTypes);
      dropDownMembers = new ArrayList(); //this.scTypeAndMemberDropdownBars.sortedDropDownMembers
      selectedMember = this.scTypeAndMemberDropdownBars.selectedMember;
      selectedType = this.scTypeAndMemberDropdownBars.selectedType;
      return true;
    }
    private void GetDropDownTypes(ArrayList dropDownTypes){
      System.Compiler.TypeNodeList types = this.scTypeAndMemberDropdownBars.sortedDropDownTypes;
      int n = types == null ? 0 : types.Length;
      for (int i = 0; i < n; i++) {
        System.Compiler.TypeNode type = types[i];
        if (type == null || type.Name == null) { Debug.Assert(false); continue; }
        dropDownTypes.Add(type.FullName);
      }
    }

//    public override void SynchronizeDropdowns(IVsTextView textView, int line, int col) {
//      string fname = this.GetFileName(textView); if (fname == null) return;
//      this.scTypeAndMemberDropdownBars.SynchronizeDropdowns(fname, line, col);
//      this.textView = textView;
//      this.dropDownMemberSignatures = this.scTypeAndMemberDropdownBars.dropDownMemberSignatures;
//      this.PopulateTypeNamesPositionsAndGlyphs();
//      this.PopulateMemberPositionsAndGlyphs();
//      this.selectedMember = this.scTypeAndMemberDropdownBars.selectedMember;
//      this.selectedType = this.scTypeAndMemberDropdownBars.selectedType;
//    }
//    private void PopulateTypeNamesPositionsAndGlyphs(){
//      System.Compiler.TypeNodeList types = this.scTypeAndMemberDropdownBars.sortedDropDownTypes;
//      int n = types == null ? 0 : types.Length;
//      string[] typeNames = new string[n];
//      int[] startColumns = new int[n];
//      int[] startLines = new int[n];
//      int[] glyphs = new int[n];
//      for (int i = 0; i < n; i++){
//        System.Compiler.TypeNode type = types[i]; 
//        if (type == null || type.Name == null){Debug.Assert(false); continue;}
//        typeNames[i] = type.FullName;
//        startColumns[i] = type.Name.SourceContext.StartColumn-1;
//        startLines[i] = type.Name.SourceContext.StartLine-1;
//        glyphs[i] = this.glyphProvider.GetGlyph(type);
//      }
//      this.dropDownTypeNames = typeNames;
//      this.dropDownTypeStartColumns = startColumns;
//      this.dropDownTypeStartLines = startLines;
//      this.dropDownTypeGlyphs = glyphs;
//    }
//    private void PopulateMemberPositionsAndGlyphs(){
//      System.Compiler.MemberList members = this.scTypeAndMemberDropdownBars.sortedDropDownMembers;
//      int n = members == null ? 0 : members.Length;
//      int[] startColumns = new int[n];
//      int[] startLines = new int[n];
//      int[] glyphs = new int[n];
//      for (int i = 0; i < n; i++){
//        System.Compiler.Member member = members[i]; 
//        if (member == null || member.Name == null) continue;
//        startColumns[i] = member.Name.SourceContext.StartColumn-1;
//        startLines[i] = member.Name.SourceContext.StartLine-1;
//        glyphs[i] = this.glyphProvider.GetGlyph(member);
//      }
//      this.dropDownMemberStartColumns = startColumns;
//      this.dropDownMemberStartLines = startLines;
//      this.dropDownMemberGlyphs = glyphs;
//    }
  }
  [ComVisible(true), Guid("9864D4AD-569A-4daf-8CBC-548F6E24C111")]
  public class GeneralPropertyPage : SettingsPage {
    private string assemblyName;
    private OutputType outputType;
    private string defaultNamespace;
    private string startupObject;
    private string applicationIcon;
//    private string standardLibraryLocation;
    private PlatformType targetPlatform = PlatformType.v11;
    private string targetPlatformLocation;

    public GeneralPropertyPage() {
//      this.strName = SR.GetString(UIStringNames.GeneralCaption);
    }

    public override string GetClassName() {
      return this.GetType().FullName;
    }
    public override void BindProperties() {
//      XmlElement project = this.ProjectMgr.StateElement;
//      if (project == null) { Debug.Assert(false); return; }
//      XmlElement settings = (XmlElement)project.SelectSingleNode("Build/Settings");
//      if (settings == null) { Debug.Assert(false); return; }
//      this.assemblyName = settings.GetAttribute("AssemblyName");
//      string outputType = settings.GetAttribute("OutputType");
//      if (outputType != null && outputType.Length > 0)
//        try { this.outputType = (OutputType)Enum.Parse(typeof(OutputType), outputType); } catch { } //Should only fail if project file is corrupt
//      this.defaultNamespace = settings.GetAttribute("RootNamespace");
//      this.startupObject = settings.GetAttribute("StartupObject");
//      this.applicationIcon = settings.GetAttribute("ApplicationIcon");
//      this.standardLibraryLocation = settings.GetAttribute("StandardLibraryLocation");
//      string targetPlatform = settings.GetAttribute("TargetPlatform");
//      if (targetPlatform != null && targetPlatform.Length > 0)
//        try { this.targetPlatform = (PlatformType)Enum.Parse(typeof(PlatformType), targetPlatform); } catch { }
//      this.targetPlatformLocation = settings.GetAttribute("TargetPlatformLocation");
    }

    public override void ApplyChanges() {
//      XmlElement project = this.ProjectMgr.StateElement;
//      if (project == null) { Debug.Assert(false); return; }
//      XmlElement settings = (XmlElement)project.SelectSingleNode("Build/Settings");
//      if (settings == null) { Debug.Assert(false); return; }
//      settings.SetAttribute("AssemblyName", this.assemblyName);
//      settings.SetAttribute("OutputType", this.outputType.ToString());
//      settings.SetAttribute("RootNamespace", this.defaultNamespace);
//      settings.SetAttribute("StartupObject", this.startupObject);
//      settings.SetAttribute("ApplicationIcon", this.applicationIcon);
//      settings.SetAttribute("StandardLibraryLocation", this.standardLibraryLocation);
//      settings.SetAttribute("TargetPlatform", this.targetPlatform.ToString());
//      settings.SetAttribute("TargetPlatformLocation", this.targetPlatformLocation);
//      this.IsDirty = false;
    }
//    [LocCategory(UIStringNames.Application)]
//    [DisplayName(UIStringNames.AssemblyName)]
//    [LocDescription(UIStringNames.AssemblyNameDescription)]
    public string AssemblyName {
      get { return this.assemblyName; }
      set { this.assemblyName = value; this.IsDirty = true; }
    }
//    [LocCategory(UIStringNames.Application)]
//    [DisplayName(UIStringNames.OutputType)]
//    [LocDescription(UIStringNames.OutputTypeDescription)]
    public OutputType OutputType {
      get { return this.outputType; }
      set { this.outputType = value; this.IsDirty = true; }
    }
//    [LocCategory(UIStringNames.Application)]
//    [DisplayName(UIStringNames.DefaultNamespace)]
//    [LocDescription(UIStringNames.DefaultNamespaceDescription)]
    public string DefaultNamespace {
      get { return this.defaultNamespace; }
      set { this.defaultNamespace = value; this.IsDirty = true; }
    }
//    [LocCategory(UIStringNames.Application)]
//    [DisplayName(UIStringNames.StartupObject)]
//    [LocDescription(UIStringNames.StartupObjectDescription)]
    public string StartupObject {
      get { return this.startupObject; }
      set { this.startupObject = value; this.IsDirty = true; }
    }
//    [LocCategory(UIStringNames.Application)]
//    [DisplayName(UIStringNames.ApplicationIcon)]
//    [LocDescription(UIStringNames.ApplicationIconDescription)]
    public string ApplicationIcon {
      get { return this.applicationIcon; }
      set { this.applicationIcon = value; this.IsDirty = true; }
    }
//    [LocCategory(UIStringNames.Project)]
//    [DisplayName(UIStringNames.ProjectFile)]
//    [LocDescription(UIStringNames.ProjectFileDescription)]
    public string ProjectFile {
      get { return Path.GetFileName(this.ProjectMgr.ProjectFile); }
    }
//    [LocCategory(UIStringNames.Project)]
//    [DisplayName(UIStringNames.ProjectFolder)]
//    [LocDescription(UIStringNames.ProjectFolderDescription)]
    public string ProjectFolder {
      get { return Path.GetDirectoryName(this.ProjectMgr.ProjectFolder); }
    }
//    [LocCategory(UIStringNames.Project)]
//    [DisplayName(UIStringNames.OutputFile)]
//    [LocDescription(UIStringNames.OutputFileDescription)]
    public string OutputFile {
      get {
        switch (this.outputType) {
          case OutputType.Exe: return this.assemblyName + ".exe";
          default: return this.assemblyName + ".dll";
        }
      }
    }
//    [LocCategory(UIStringNames.Project)]
//    [DisplayName(UIStringNames.StandardLibraryLocation)]
//    [LocDescription(UIStringNames.StandardLibraryLocationDescription)]
    public string StandardLibraryLocation {
      get { return this.targetPlatformLocation; }
      set { this.targetPlatformLocation = value; IsDirty = true; }
    }
//    [LocCategory(UIStringNames.Project)]
//    [DisplayName(UIStringNames.TargetPlatform)]
//    [LocDescription(UIStringNames.TargetPlatformDescription)]
    public PlatformType TargetPlatform {
      get { return this.targetPlatform; }
      set { this.targetPlatform = value; IsDirty = true; }
    }
//    [LocCategory(UIStringNames.Project)]
//    [DisplayName(UIStringNames.TargetPlatformLocation)]
//    [LocDescription(UIStringNames.TargetPlatformLocationDescription)]
    public string TargetPlatformLocation {
      get { return this.targetPlatformLocation; }
      set { this.targetPlatformLocation = value; IsDirty = true; }
    }
  }
}
