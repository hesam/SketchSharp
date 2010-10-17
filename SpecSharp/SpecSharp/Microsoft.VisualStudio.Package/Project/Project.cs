//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Globalization;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using IServiceProvider = System.IServiceProvider;
#if WHIDBEY
using Microsoft.VisualStudio.CodeTools;
#endif

namespace Microsoft.VisualStudio.Package{

  /// <include file='doc\Project.uex' path='docs/doc[@for="Project"]/*' />
  /// <summary>
  /// Manages the persistent state of the project (References, options, files, etc.) and deals with user interaction via a GUI in the form of a hierarchy.
  /// </summary>
  [CLSCompliant(false)]
  public abstract class Project : HierarchyNode, IVsGetCfgProvider, IVsProject3, IVsCfgProvider2, IVsProjectCfgProvider, IPersistFileFormat, IVsDependencyProvider, IVsSccProject2{
    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.BuildLock"]/*' />
    /// <summary>A project will only try to build if it can obtain a lock on this object</summary>
    public static readonly object BuildLock = new object();

    /// <summary>Maps integer ids to project item instances</summary>
    internal CookieMap ItemIdMap = new CookieMap();

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.UIShell;"]/*' />
    /// <summary>An object representing the IDE hosting the project manager</summary>
    public IVsUIShell UIShell;

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.Site;"]/*' />
    /// <summary>A service provider call back object provided by the IDE hosting the project manager</summary>
    public IServiceProvider Site;

    internal TrackDocumentsHelper Tracker; //TODO: figure out what this does

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.LastModifiedTime;"]/*' />
    /// <summary>
    /// This property returns the time of the last change made to this project.
    /// It is not the time of the last change on the project file, but actually of
    /// the in memory project settings.  In other words, it is the last time that 
    /// SetProjectDirty was called.
    /// </summary>
    public DateTime LastModifiedTime;

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.projFile;"]/*' />
    internal protected XmlDocument projFile; //REVIEW: Shouldn't the project manager abstract away the fact that it uses an XML DOM to manage its state?

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.ProjectDocument"]/*' />
    [System.ComponentModel.BrowsableAttribute(false)]
    public XmlDocument ProjectDocument{
      get{ return projFile; }
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.StateElement;"]/*' />
    public XmlElement StateElement;

    private ConfigProvider configProvider; //REVIEW: should these be private?

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.taskProvider;"]/*' />
    protected TaskProvider taskProvider;

#if WHIDBEY
    protected ITaskManager taskManager;      // for errors that are also detected by codesense
    protected ITaskManager taskManagerBuild; // for build-only errors, i.e. usually verifier errors 
#endif

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.filename;"]/*' />
    protected string filename;

    private bool dirty;

    private bool fNewProject;

    // The icons used in the hierarchy view
    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.ImageList;"]/*' />
    public ImageList ImageList;

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.referencesFolder;"]/*' />
    public HierarchyNode referencesFolder;

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames"]/*' />
    public enum ImageNames{ Folder, OpenFolder, ReferenceFolder, OpenReferenceFolder, Reference, Project, File }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetProjectGuid"]/*' />
    /// <summary>
    /// This Guid must match the Guid you registered under
    /// HKLM\Software\Microsoft\VisualStudio\%version%\Projects.
    /// Among other things, the Project framework uses this 
    /// guid to find your project and item templates.
    /// </summary>
    public abstract Guid GetProjectGuid();

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetCompiler"]/*' />
    public virtual ICodeCompiler GetCompiler(){
      /// <include file='doc\Project.uex' path='docs/doc[@for="Project.return null;"]/*' />
      return null;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.CreateProjectOptions"]/*' />
    /// <summary>
    /// Override this method if you have your own project specific
    /// subclass of ProjectOptions
    /// </summary>
    /// <returns>This method returns a new instance of the ProjectOptions base class.</returns>
    public virtual ProjectOptions CreateProjectOptions(){
      return new ProjectOptions();
    }

    private Automation.OAProject automation;

    internal static ArrayList ProjectList = new ArrayList();

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.Project"]/*' />
    public Project(){
      this.automation = new Automation.OAProject(this);
      Project.ProjectList.Add(automation);
      this.hierarchyId = VsConstants.VSITEMID_ROOT;
      // Load the hierarchy icoBns... //TODO: call a virtual routine
      this.ImageList = GetImageList();
      this.configProvider = new ConfigProvider(this);
      this.Tracker = new TrackDocumentsHelper(this);
    }

		/// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetImageList"]/*' />
    public virtual ImageList GetImageList(){
      ImageList ilist = new ImageList();
      ilist.AddImages("Microsoft.VisualStudio.Package.Project.folders.bmp", 
        typeof(Project).Assembly, 7, 16, 16, Color.Magenta);
      return ilist;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.Load"]/*' />
    public void Load(string filename, string location, string name, uint flags, ref Guid iidProject, out int canceled){
      // strip any extension off name and keep it.
      string ext = "";
      if (name != null && Path.HasExtension(name)){
          ext = Path.GetExtension(name);
          name = Path.GetFileNameWithoutExtension(name);
      }
      // set up internal members and icons
      canceled = 0;
      this.projectMgr = this;
      this.filename = filename;
      this.fNewProject = false;
      // based on the passed in flags, this either reloads/loads a project, or tries to create a new one
      if (flags == (uint)__VSCREATEPROJFLAGS.CPF_CLONEFILE){
        this.projFile = new XmlDocument();
        this.projFile.Load(this.filename);
        // now we create a new project... we do that by loading the template and then saving under a new name
        // we also need to copy all the associated files with it.
        // we need to generate a new guid for the project
        this.ProjectIDGuid = new Guid();
        // and insert it into the persisted tree...  -0> GET/SET
        // set the name of the project and the assembly name
        // the passed in name is afull filename
        try{
          XmlElement proj = this.projFile.DocumentElement;
          XmlElement languageNode = this.StateElement = this.GetProjectStateElement(proj);
          Debug.Assert(languageNode != null);
          languageNode.SetAttribute("Name", name);
          XmlElement settingsNode = languageNode.SelectSingleNode("Build/Settings") as XmlElement;
          Debug.Assert(settingsNode != null);
          settingsNode.SetAttribute("AssemblyName", name);
          settingsNode.SetAttribute("RootNamespace", name);
        } catch{
        }

        IPersistFileFormat x = this;
 
        x.Save(Path.Combine(location, name + ext), 1, 0);
        // now we do have the project file saved. we need to create embedded files now. 
        foreach (XmlElement e in this.projFile.SelectNodes("//Files/Include/File")){
          string strRelFilePath = e.GetAttribute("RelPath");
          string strPathToFile;
          string basePath = Path.GetDirectoryName(filename);
          string strNewFileName;
          // taking the base name from the project template + the relative pathname in there,
          // and you get the filename
          strPathToFile = Path.Combine(basePath, strRelFilePath);
          // now the new path should be the base dir of the new project (location) + the rel path of the file
          strNewFileName = Path.Combine(location, strRelFilePath);
          // now copy file
          FileInfo fiOrg = new FileInfo(strPathToFile);
          FileInfo fiNew = fiOrg.CopyTo(strNewFileName, true);

          fiNew.Attributes = FileAttributes.Normal; // remove any read only attributes.
        }
      }
      // now reload to fix up references
      Reload();
      // if we are a new project open up default file
      // which we delay until setsite happened
      // MB - 01/20/05 - It looks ot me as if setsite happens before this is ever reached
      // and that it is never reached again. So open the file right here.
      if (flags == (uint)__VSCREATEPROJFLAGS.CPF_CLONEFILE){
        this.fNewProject = true; // MB - 01/20/05 - kind of pointless now that we open the file right here.
        HierarchyNode child = this.FirstChild; // that should be the reference folder....
        // find the first child that is a file that doesn't have the name "AssemblyInfo.ssc". This should be
        // the default file. REVIEW: I'm sure there's a better way to find this!
        while (child != null && (child.NodeType != HierarchyNodeType.File || child.Caption == "AssemblyInfo.ssc")){
          child = child.NextSibling;
        }
        child.OpenItem(false,false);
        this.fNewProject = false; // MB - 01/20/05 - remove this if the setting it to true is removed.
      }
    }

    internal static Hashtable projects = Hashtable.Synchronized(new Hashtable());

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.OnOpenItem"]/*' />
    /// <summary>
    /// Called when the project opens an editor window for the given file
    /// </summary>
    public virtual void OnOpenItem(string fullPathToSourceFile){
      Project.projects[fullPathToSourceFile] = new WeakReference(this);
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.For"]/*' />
    /// <summary>
    /// Returns the Project instance that caused the IDE to open the specified file
    /// </summary>
    public static Project For(string fullPathToSourceFile){
      //TODO: if a project gets refreshed, first clear out this table
      WeakReference weakRef = (WeakReference)Project.projects[fullPathToSourceFile];

      if (weakRef == null || !weakRef.IsAlive) return null;

      return (Project)weakRef.Target;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.ClearStaticReferencesToThis"]/*' />
    /// <summary>
    /// Clear out references to this project manager (because we're being closed).
    /// This also clears out stale references.
    /// </summary>
    public void ClearStaticReferencesToThis(){
      Hashtable newTable = new Hashtable();

      lock (Project.projects){
        IDictionaryEnumerator de = Project.projects.GetEnumerator();

        while (de.MoveNext()){
          WeakReference wr = (WeakReference)de.Value;

          if (wr.IsAlive){
            Project target = (Project)wr.Target;

            if (target != this){
              newTable.Add(de.Key, wr);
            }
          }
        }
      }

      Project.projects = newTable;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetProjectStateElement"]/*' />
    /// <summary>
    /// Gets the element that holds the actual project system state. The root element of the project is typically tagged VisualStudioProject
    /// and is not interesting to the project manager.
    /// </summary>
    public virtual XmlElement GetProjectStateElement(XmlElement project){
      return (XmlElement)project.SelectSingleNode("*");
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetProjectType"]/*' />
    /// <summary>
    /// Returns a caption for VSHPROPID_TypeName.
    /// </summary>
    /// <returns></returns>
    public abstract string GetProjectType();

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.projectGuid;"]/*' />
    protected Guid projectGuid;

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.ProjectIDGuid"]/*' />
    [System.ComponentModel.BrowsableAttribute(false)]
    public virtual Guid ProjectIDGuid{
      get{
        return this.projectGuid;
      }
      set{
        this.projectGuid = value;
        if (this.StateElement != null){
          string s = this.projectGuid.ToString();
          if (this.StateElement.GetAttribute("ProjectGuid") != s)
            this.StateElement.SetAttribute("ProjectGuid", s);
        }
      }
    }


    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetFullyQualifiedNameForReferencedLibrary"]/*' />
    public abstract string GetFullyQualifiedNameForReferencedLibrary(ProjectOptions options, string rLibraryName);

    public abstract System.Collections.ArrayList GetRuntimeLibraries();

    public virtual void Clean(XmlElement config){
      this.PrepareBuild(config, true, true);
    }
    //////////////////////////////////////////////////////////////////////////////////////////////////    
    // This is called from the main thread before the background build starts.
    //    PrepareBuild mainly creates directories and cleans house if fCleanBuild is true
    //
    //  TODO: This should do check if a build is needed.
    //
    //////////////////////////////////////////////////////////////////////////////////////////////////    
    public virtual void PrepareBuild(XmlElement config, bool fCleanBuild){
      this.PrepareBuild(config, fCleanBuild, false);
    }
    public virtual void PrepareBuild(XmlElement config, bool fCleanBuild, bool doNotCopyReferences){
      this.lastConfig = null; //Force GetProjectOptions to refresh (in case missing references have since been built).
      ProjectOptions options = this.GetProjectOptions(config);
      string outputPath = Path.GetDirectoryName(options.OutputAssembly);

      EnsureOutputPath(outputPath);
      if (options.XMLDocFileName != null && options.XMLDocFileName != ""){
        EnsureOutputPath(Path.GetDirectoryName(options.XMLDocFileName));
      }
      if (doNotCopyReferences){
        if (File.Exists(options.OutputAssembly)) try{File.Delete(options.OutputAssembly);} catch{}
        string pdb = Path.ChangeExtension(options.OutputAssembly, "pdb");
        if (File.Exists(pdb)) try{File.Delete(pdb);} catch{}
        if (File.Exists(options.XMLDocFileName)) try{File.Delete(options.XMLDocFileName);} catch{}
      }

      // Find out which referenced assemblies are "Private" and require a local copy.
      XmlDocument doc = config.OwnerDocument;

      //TODO: this is needed for cleaning the project, but the rest of the logic is already duplicated in GetProjectOptions.
      foreach (XmlElement e in doc.SelectNodes("//Reference")){
        // assume a local copy is needed unless Private is set to false or the reference is to another project.
        string localCopy = e.GetAttribute("Private");
        if (localCopy != null) localCopy = localCopy.Trim().ToLower();
        string fullPath = this.GetReferencedLibraryName(e);

        if (File.Exists(fullPath)){
          string assemblyDir = Path.GetDirectoryName(fullPath);
          string localPath = Path.Combine(outputPath, Path.GetFileName(fullPath));

          if (localCopy == null || localCopy == ""){
            // didn't specify the Private attribute, so the default value is based on 
            // whether the assembly is in the GAC or not.
            InitPrivateAttribute(fullPath, e);
            localCopy = e.GetAttribute("Private");
            if (localCopy != null) localCopy = localCopy.Trim().ToLower();
          }
          // See if the assembly is not already in out output path or it needs updating...
          if (localCopy != "false" && !VsShell.IsSamePath(assemblyDir, outputPath) && 
            (!File.Exists(localPath) || File.GetLastWriteTime(fullPath) > File.GetLastWriteTime(localPath) || doNotCopyReferences)){
            if (doNotCopyReferences){
              if (File.Exists(localPath)) try{File.Delete(localPath);}catch{}
            }else{
              File.Copy(fullPath, localPath, true);
              FileAttributes attrs = File.GetAttributes(localPath);
              if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly){
                File.SetAttributes(localPath, attrs & ~FileAttributes.ReadOnly);
              }
            }
            if (options.IncludeDebugInformation){
              string pdb = Path.ChangeExtension(fullPath, "pdb");
              string localPdb = Path.ChangeExtension(localPath, "pdb");
              if (doNotCopyReferences){
                if (File.Exists(localPdb)) try{File.Delete(localPdb);} catch{}
              }else{
                if (File.Exists(pdb)){
                  File.Copy(pdb, localPdb, true);
                  FileAttributes attrs = File.GetAttributes(localPdb);
                  if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly){
                    File.SetAttributes(localPdb, attrs & ~FileAttributes.ReadOnly);
                  }
                }
              }
            }
          }
        }
      }

      // For whatever runtimes there are, if they are not in the GAC,
      // then copy them into the output directory too.
      // BUT: only if NoStandardLibaries is false
      if (!options.NoStandardLibrary) {
        System.Collections.ArrayList runtimes = this.GetRuntimeLibraries();
        foreach (string runtimePath in runtimes) {
          if (runtimePath == null) continue;
          if (this.MustCopyReferencedAssembly(runtimePath)) {
            string runtimeFileName = Path.GetFileName(runtimePath);
            if (!File.Exists(runtimePath)) continue;
            string localPath = Path.Combine(outputPath, runtimeFileName);
            if (File.Exists(localPath)) {
              try { File.Delete(localPath); }
              catch { }
            }
            File.Copy(runtimePath, localPath, true);
            FileAttributes attrs = File.GetAttributes(localPath);
            if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly) {
              File.SetAttributes(localPath, attrs & ~FileAttributes.ReadOnly);
            }
          }
        }
      }
    }


    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.EnsureOutputPath"]/*' />
    public static void EnsureOutputPath(string path){
      if (path != "" && !Directory.Exists(path)){
        try{
          Directory.CreateDirectory(path);
        } catch{
        }
      }
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.LaunchDebugger"]/*' />
    public virtual void LaunchDebugger(VsDebugTargetInfo info){
      info.cbSize = (uint)Marshal.SizeOf(info);
      IntPtr ptr = Marshal.AllocCoTaskMem((int)info.cbSize);
      Marshal.StructureToPtr(info, ptr, false);
      try{
        IVsDebugger d = this.GetService(typeof(IVsDebugger)) as IVsDebugger;
        d.LaunchDebugTargets(1, ptr);
      } finally{
        Marshal.FreeCoTaskMem(ptr);
      }
    }

    public abstract CompilerResults CompileAssemblyFromFile(ProjectOptions options, string filename);

    public abstract CompilerResults CompileAssemblyFromFileBatch(ProjectOptions options, string[] fileNames);

    public virtual CompilerResults CompileProject(XmlElement config){
      ProjectOptions options = this.GetProjectOptions(config);
      CompilerResults results;
      ArrayList files = new ArrayList();
      System.Xml.XmlDocument doc = config.OwnerDocument;
      foreach (XmlElement e in doc.SelectNodes("//Files/Include/File")){
        //TODO: Support other "BuildActions" like "EmbeddedResource"...
        if (e.GetAttribute("BuildAction") == "Compile"){
          string rel = e.GetAttribute("RelPath");
#if WHIDBEY
          Uri uri = new Uri(new Uri(doc.BaseURI), rel);
#else
          Uri uri = new Uri(new Uri(doc.BaseURI), rel, true);
#endif
          if (uri.IsFile){
            files.Add(uri.LocalPath);
          } else{
            files.Add(uri.AbsoluteUri);
          }
        }
      }
        
      try{
        if (files.Count == 1){
          string filename = (string)files[0];
          results = this.CompileAssemblyFromFile(options, filename);
        } else{
          string[] fileNames = (string[])files.ToArray(typeof(string));
          results = this.CompileAssemblyFromFileBatch(options, fileNames);
        }
      }catch (Exception e){
        results = new CompilerResults(options.TempFiles);
        results.Errors.Add(new CompilerError(options.OutputAssembly, 1, 1, "", "Internal Compiler Error: " + e.ToString() + "\n"));
        results.NativeCompilerReturnValue = 1;
      }
      return results;
    }

    /// <summary>
    /// Finds out if the project output is up-to-date. 
    /// </summary>
    /// <returns>True if the project output is up-to-date; false otherwise.</returns>
    public virtual bool CheckUpToDate(XmlElement config){
      string output = GetOutputAssembly(config);
      if (!File.Exists(output))
        return false;
      DateTime outputTime = File.GetLastWriteTimeUtc(output);
      DateTime projectTime = File.GetLastWriteTimeUtc(this.FullPath);
      if (outputTime < projectTime)
        return false;
      foreach (XmlElement file in this.projFile.SelectNodes("//Files/Include/File")){
        string relPath = file.GetAttribute("RelPath");
        string fullPath = Path.Combine(this.ProjectFolder, relPath);
        DateTime inputTime = File.GetLastWriteTimeUtc(fullPath);
        if (outputTime < inputTime)
          return false;
      }
      foreach (XmlElement reference in this.projFile.SelectNodes("//References/Reference")){
        string fullPath = this.GetReferencedLibraryName(reference);
        DateTime referenceTime = File.GetLastWriteTimeUtc(fullPath);
        if (outputTime < referenceTime)
          return false;
      }
      return true;
    }

    /// <summary>
    /// The current build can use this object to output messages and check for cancellation requests.
    /// Only meaningful during a build.
    /// </summary>
    public BuildSite BuildSite;

    public bool Build(uint vsopts, XmlElement config, IVsOutputWindowPane output, bool fCleanBuild, BuildSite site){
      this.BuildSite = site;
      try{
        return Build(vsopts, config, output, fCleanBuild);
      }finally{
        this.BuildSite = null;
      }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////    
    // This is called from the compiler background thread.
    //    fCleanBuild is not part of the vsopts, but passed down as the callpath is differently
    //
    //////////////////////////////////////////////////////////////////////////////////////////////////    
    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.Build"]/*' />
    public virtual bool Build(uint vsopts, XmlElement config, IVsOutputWindowPane output, bool fCleanBuild){
      if (fCleanBuild){
        // we are done
        return true;
      }
      lock (Project.BuildLock){
#if LookForMemoryLeaks
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        long usedMemoryBeforeBuild = System.GC.GetTotalMemory(true);
#endif        
        int errorCount = 0;
        int warningCount = 0;
        CompilerResults results = this.CompileProject(config);

#if WHIDBEY
        if (this.taskManager != null && this.taskManagerBuild != null) {
          taskManager.ClearTasksOnProject(this.Caption);
          taskManagerBuild.ClearTasksOnProject(this.Caption);
          bool runVerifierBuildOnly = this.GetBoolAttr(config, "RunProgramVerifier") && !this.GetBoolAttr(config, "RunProgramVerifierWhileEditing");
          string verifierCode = ((int)System.Compiler.Error.GenericWarning).ToString("0000");
          
          foreach (CompilerError e in results.Errors) {
            if (e.IsWarning)
              warningCount++;
            else
              errorCount++;

            // output.OutputTaskItemString(GetFormattedErrorMessage(e, false) + "\n", VSTASKPRIORITY.TP_HIGH, VSTASKCATEGORY.CAT_BUILDCOMPILE, "", -1, e.FileName, (uint)e.Line - 1, e.ErrorText);
            int endLine;
            int endColumn;
            if (e is System.Compiler.CompilerErrorEx) {
              System.Compiler.CompilerErrorEx errorEx = (System.Compiler.CompilerErrorEx)e;
              endLine = errorEx.EndLine;
              endColumn = errorEx.EndColumn;
            }
            else {
              endLine = e.Line;
              endColumn = e.Column + 1;
            }

            bool isBuildOnly = runVerifierBuildOnly && e.ErrorNumber == verifierCode;
            
            (isBuildOnly ? taskManagerBuild : taskManager).AddTask(e.ErrorText, null, e.ErrorNumber, "CS" + e.ErrorNumber,
              TaskPriority.Normal, (e.IsWarning ? TaskCategory.Warning : TaskCategory.Error),
              (isBuildOnly ? TaskMarker.Error : (e.IsWarning ? TaskMarker.Warning : TaskMarker.CodeSense)),
              TaskOutputPane.Build, this.Caption, e.FileName,
              e.Line, e.Column, endLine, endColumn, null);
          }
          taskManager.OutputString("Build complete -- " + errorCount + " errors, " + warningCount + " warnings\n", TaskOutputPane.Build);
          taskManager.Refresh();
          taskManagerBuild.Refresh();
        }
        else {
#endif        
          this.taskProvider.ClearErrors();
          foreach (CompilerError e in results.Errors) {
            if (e.IsWarning) warningCount++;
            else
              errorCount++;
            output.OutputTaskItemString(GetFormattedErrorMessage(e, false) + "\n", VSTASKPRIORITY.TP_HIGH, VSTASKCATEGORY.CAT_BUILDCOMPILE, "", -1, e.FileName, (uint)e.Line - 1, e.ErrorText);
          }
          output.OutputStringThreadSafe("Build complete -- " + errorCount + " errors, " + warningCount + " warnings\n"); //TODO: globalize
          output.FlushToTaskList();
#if WHIDBEY
        }
#endif
        bool success = results.NativeCompilerReturnValue == 0;
#if LookForMemoryLeaks
        results = null;
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        long usedMemoryAfterBuild = System.GC.GetTotalMemory(true);
        output.OutputStringThreadSafe("Build leaked "+(usedMemoryAfterBuild-usedMemoryBeforeBuild)+" bytes");
#endif
        return success;
      }
    }
    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetFormattedErrorMessage"]/*' />
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

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.errorString"]/*' />
    protected string errorString = null;

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetErrorString"]/*' />
    public virtual string GetErrorString(){
      if (errorString == null){
        this.errorString = SR.GetString(SR.Error);
      }

      return errorString;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.warningString"]/*' />
    protected string warningString = null;

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetWarningString"]/*' />
    public virtual string GetWarningString(){
      if (this.warningString == null)
        this.warningString = SR.GetString(SR.Warning);

      return this.warningString;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetOutputPath"]/*' />
    public string GetOutputPath(XmlElement config){
      string outputPath = config.GetAttribute("OutputPath");

      if (outputPath != null && outputPath != ""){
        outputPath = outputPath.Replace('/', Path.DirectorySeparatorChar);
        if (outputPath[outputPath.Length - 1] != Path.DirectorySeparatorChar)
          outputPath += Path.DirectorySeparatorChar;
      }

      return outputPath;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.options"]/*' />
    protected ProjectOptions options = null;
    protected XmlElement lastConfig = null;

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetProjectOptions"]/*' />
    public virtual ProjectOptions GetProjectOptions(XmlElement config){
      if (this.options != null && this.lastConfig == config) return this.options;

      ProjectOptions options = this.options = this.CreateProjectOptions();
      this.lastConfig = config;

      if (config == null) return options;

      options.GenerateExecutable = true;

      string outputPath = this.GetOutputPath(config);
      // absolutize relative to project folder location
      outputPath = Path.Combine(this.ProjectFolder, outputPath);
      EnsureOutputPath(outputPath);
      // transfer configuration independent settings.
      XmlElement settings = (XmlElement)config.ParentNode;

      if (settings.Name == "Settings"){
        options.RootNamespace = settings.GetAttribute("RootNamespace");
        options.OutputAssembly = outputPath + this.GetAssemblyName(config);

        string outputtype = settings.GetAttribute("OutputType").ToLower();

        if (outputtype == "library"){
          options.ModuleKind = ModuleKindFlags.DynamicallyLinkedLibrary;
          options.GenerateExecutable = false; // DLL's have no entry point.
        } else if (outputtype == "winexe")
          options.ModuleKind = ModuleKindFlags.WindowsApplication;
        else
          options.ModuleKind = ModuleKindFlags.ConsoleApplication;

        options.Win32Icon = settings.GetAttribute("ApplicationIcon");
        options.MainClass = settings.GetAttribute("StartupObject");

        options.ShadowedAssembly = settings.GetAttribute("ShadowedAssembly");
        if (options.ShadowedAssembly != null && options.ShadowedAssembly.Length > 0) {
          options.ShadowedAssembly = Environment.ExpandEnvironmentVariables(options.ShadowedAssembly);
          options.ShadowedAssembly = Path.Combine(this.ProjectFolder, options.ShadowedAssembly);
        }

        options.StandardLibraryLocation = settings.GetAttribute("StandardLibraryLocation");
        if (options.StandardLibraryLocation != null && options.StandardLibraryLocation.Length > 0)
          options.StandardLibraryLocation = Path.Combine(this.ProjectFolder, options.StandardLibraryLocation);        

        string targetPlatform = settings.GetAttribute("TargetPlatform");

        if (targetPlatform != null && targetPlatform.Length > 0){
          try{ options.TargetPlatform = (PlatformType)Enum.Parse(typeof(PlatformType), targetPlatform); } catch{ }
          options.TargetPlatformLocation = settings.GetAttribute("TargetPlatformLocation");
          if (options.TargetPlatformLocation != null && options.TargetPlatformLocation.Length > 0)
            options.TargetPlatformLocation = Path.Combine(this.ProjectFolder, options.TargetPlatformLocation);
          this.SetTargetPlatform(options);
        }

        /*
         * other settings from CSharp we may want to adopt at some point...
        AssemblyKeyContainerName = ""  //This is the key file used to sign the interop assembly generated when importing a com object via add reference
        AssemblyOriginatorKeyFile = ""
        DelaySign = "false"
        DefaultClientScript = "JScript"
        DefaultHTMLPageLayout = "Grid"
        DefaultTargetSchema = "IE50"
        PreBuildEvent = ""
        PostBuildEvent = ""
        RunPostBuildEvent = "OnBuildSuccess"
        */
      } else{
        options.OutputAssembly = outputPath + this.Caption + ".exe";
        options.ModuleKind = ModuleKindFlags.ConsoleApplication;
      }
      // transfer all config build options...
      if (GetBoolAttr(config, "AllowUnsafeBlocks")){
        options.AllowUnsafeCode = true;
      }

      if (config.GetAttribute("BaseAddress") != null){
        try{ options.BaseAddress = Int64.Parse(config.GetAttribute("BaseAddress")); } catch{ }
      }

      if (GetBoolAttr(config, "CheckForOverflowUnderflow")){
        options.CheckedArithmetic = true;
      }

      if (config.GetAttribute("DefineConstants") != null){
        options.DefinedPreProcessorSymbols = new StringCollection();
        foreach (string s in config.GetAttribute("DefineConstants").Replace(" \t\r\n", "").Split(';')){
          options.DefinedPreProcessorSymbols.Add(s);
        }
      }

      if (config.GetAttribute("DocumentationFile") != null && config.GetAttribute("DocumentationFile") != ""){
        options.XMLDocFileName = Path.Combine(this.ProjectFolder, config.GetAttribute("DocumentationFile"));
      }

      if (GetBoolAttr(config, "DebugSymbols")){
        options.IncludeDebugInformation = true;
      }

      if (config.GetAttribute("FileAlignment") != null){
        try{ options.FileAlignment = Int32.Parse(config.GetAttribute("FileAlignment")); } catch{ }
      }

      if (GetBoolAttr(config, "IncrementalBuild")){
        options.IncrementalCompile = true;
      }
      
      if (GetBoolAttr(config, "Optimize")){
        options.Optimize = true;
      }

      if (GetBoolAttr(config, "RegisterForComInterop")){
      }

      if (GetBoolAttr(config, "RemoveIntegerChecks")){
        options.CheckedArithmetic = false;
      }

      if (GetBoolAttr(config, "TreatWarningsAsErrors")){
        options.TreatWarningsAsErrors = true;
      }

      if (config.GetAttribute("WarningLevel") != null){
        try{ options.WarningLevel = Int32.Parse(config.GetAttribute("WarningLevel")); } catch{ }
      }

      if (GetBoolAttr(config, "DisableAssumeChecks"))
        options.DisableAssumeChecks = true;
      if (GetBoolAttr(config, "DisableDefensiveChecks"))
        options.DisableDefensiveChecks = true;
      if (GetBoolAttr(config, "DisableGuardedClassesChecks"))
        options.DisableGuardedClassesChecks = true;
      if (GetBoolAttr(config, "DisableInternalChecks"))
        options.DisableInternalChecks = true;
      if (GetBoolAttr(config, "DisableInternalContractsMetadata"))
        options.DisableInternalContractsMetadata = true;
      if (GetBoolAttr(config, "DisablePublicContractsMetadata"))
        options.DisablePublicContractsMetadata = true;
      // Add the References
      XmlDocument doc = config.OwnerDocument;

      foreach (XmlElement e in doc.SelectNodes("//Reference")){
        string projectGuid = e.GetAttribute("Project");
        if (projectGuid != null && projectGuid.Length > 0){
          if (projectGuid[0] == '{') projectGuid = projectGuid.Trim('{','}');
          string file = this.GetOutputPathFromReferencedProject(projectGuid);
          if (file != null && !options.ReferencedAssemblies.Contains(file)){
            string localPath = Path.Combine(outputPath, Path.GetFileName(file));
            if (File.Exists(file)){
              try{
                File.Copy(file, localPath, true);
                if (options.IncludeDebugInformation){
                  string pdb = Path.ChangeExtension(file, "pdb");
                  if (File.Exists(pdb))
                    File.Copy(pdb, Path.ChangeExtension(localPath, "pdb"), true);
                }
              }catch{
                //TODOO: log the error
              }
            }
            options.ReferencedAssemblies.Add(file);
          }else
            this.options = null;
          continue;
        }else{
          string file = e.GetAttribute("AssemblyName") + ".dll";
          string hint = e.GetAttribute("HintPath");

          if (hint != null && hint != ""){// probably relative to the current file...
            hint = hint.Replace(Path.DirectorySeparatorChar, '/');
            hint = hint.Replace("#", "%23");
            string s = doc.BaseURI;
            s = s.Replace("#", "%23");
#if WHIDBEY
            Uri baseUri = new Uri(s);
            Uri uri = new Uri(baseUri, hint);
#else
            Uri baseUri = new Uri(s, true);
            Uri uri = new Uri(baseUri, hint, true);
#endif
            if (File.Exists(uri.LocalPath)){
              file = uri.LocalPath;
            }
          }

          if (!options.ReferencedAssemblies.Contains(file))
            options.ReferencedAssemblies.Add(file);
        }
      }
      if (GetBoolAttr(settings, "NoStandardLibraries"))
        options.NoStandardLibrary = true;
      if (options.NoStandardLibrary || (options.StandardLibraryLocation != null && options.StandardLibraryLocation.Length > 0))
        this.UpdateRuntimeAssemblyLocations(options);

      //Deal with Embedded Resources
      foreach (XmlElement e in doc.SelectNodes("//Files/Include/File")){
        if (e == null) continue;
        string buildAction = e.GetAttribute("BuildAction");
        if (string.Compare(buildAction, "EmbeddedResource", true, System.Globalization.CultureInfo.InvariantCulture) == 0){
          string relpath = e.GetAttribute("RelPath");
          string ext = Path.GetExtension(relpath);
          if (string.Compare(ext, ".resx", true, System.Globalization.CultureInfo.InvariantCulture) == 0 ||
            string.Compare(ext, ".txt", true, System.Globalization.CultureInfo.InvariantCulture) == 0){
            relpath = outputPath + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(relpath)+".resources";
          }
          options.EmbeddedResources.Add(relpath);
        }
      }
      return options;
    }

    public ArrayList GetReferencedProjects(XmlElement config){
      ArrayList result = new ArrayList();
      XmlDocument doc = config.OwnerDocument;
      foreach (XmlElement e in doc.SelectNodes("//Reference")){
        string projectGuid = e.GetAttribute("Project");
        if (projectGuid != null && projectGuid.Length > 0){
          if (projectGuid[0] == '{') projectGuid = projectGuid.Trim('{','}');
          Project referencedProject = null;
          for (int i = 0, n = Project.ProjectList == null ? 0 : Project.ProjectList.Count; i < n; i++){
            Automation.OAProject proj = Project.ProjectList[i] as Automation.OAProject;
            if (proj == null) continue;
            referencedProject = proj.Object as Project;
            if (referencedProject == null) continue;
            if (string.Compare(projectGuid, referencedProject.projectGuid.ToString(), true, System.Globalization.CultureInfo.InvariantCulture) == 0)
              break;
            referencedProject = null;
          }
          if (referencedProject != null){
            result.Add(referencedProject);
          }
        }
      }
      return result;
    }
//    public static ArrayList GetProjects(){
//      ArrayList result = new ArrayList();
//      for (int i = 0, n = Project.ProjectList == null ? 0 : Project.ProjectList.Count; i < n; i++){
//        Automation.OAProject proj = Project.ProjectList[i] as Automation.OAProject;
//        if (proj == null) continue;
//        result.Add(proj.Object);
//      }
//      return result;
//    }
    public virtual string GetOutputPathFromReferencedProject(string projectGuid){
      Project referencedProject = null;
      for (int i = 0, n = Project.ProjectList == null ? 0 : Project.ProjectList.Count; i < n; i++){
        Automation.OAProject proj = Project.ProjectList[i] as Automation.OAProject;
        if (proj == null) continue;
        referencedProject = proj.Object as Project;
        if (referencedProject == null) continue;
        if (string.Compare(projectGuid, referencedProject.projectGuid.ToString(), true, System.Globalization.CultureInfo.InvariantCulture) == 0)
          break;
        referencedProject = null;
      }
      if (referencedProject != null){
        return referencedProject.GetOutputAssembly(referencedProject.GetActiveConfiguration());
      }
      return null; //TODO: use VSIP interfaces to get the info
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.SetTargetPlatform"]/*' />
    public virtual void SetTargetPlatform(ProjectOptions options){
    }

    public virtual void UpdateRuntimeAssemblyLocations(ProjectOptions options){
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetBoolAttr"]/*' />
    public virtual bool GetBoolAttr(XmlElement e, string name){
      string s = e.GetAttribute(name);

      return (s != null && s.ToLower().Trim() == "true");
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.Plural"]/*' />
    public virtual string Plural(int i){
      return (i == 1) ? "" : "s";
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetConfigPropertyPageGuids"]/*' />
    public virtual Guid[] GetConfigPropertyPageGuids(XmlElement config){
      Guid[] result = new Guid[3];
      result[0] = GetBuildPropertyPageGuid(config);
      result[1] = GetDebugPropertyPageGuid(config);
      result[2] = GetAdvancedPropertyPageGuid(config);
      return result;
    }

    public virtual Guid GetBuildPropertyPageGuid(XmlElement config){
      return typeof(BuildPropertyPage).GUID;
    }

    public virtual Guid GetDebugPropertyPageGuid(XmlElement config){
      return typeof(DebugPropertyPage).GUID;
    }

    public virtual Guid GetAdvancedPropertyPageGuid(XmlElement config){
      return typeof(AdvancedPropertyPage).GUID;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetAssemblyName"]/*' />
    public virtual string GetAssemblyName(XmlElement config){
      string name = null;
      XmlElement settings = (XmlElement)config.ParentNode;

      if (settings.Name == "Settings"){
        name = settings.GetAttribute("AssemblyName");
        if (name == null) name = this.Caption;

        string outputtype = settings.GetAttribute("OutputType").ToLower();

        if (outputtype == "library"){
          name += ".dll";
        } else{
          name += ".exe";
        }
      }

      return name;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.IsCodeFile"]/*' />
    public virtual bool IsCodeFile(string strFileName){
      return true;
    }

    public virtual bool IsEmbeddedResource(string strFileName){
      if (strFileName == null) return false;
      string strExt = Path.GetExtension(strFileName);
      return string.Compare(strExt, ".ResX", true, System.Globalization.CultureInfo.InvariantCulture) == 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetAutomationObject"]/*' />
    public virtual Automation.OAProject GetAutomationObject(){
      return this.automation;
    }

    //private static Guid projItemDlgSID = new Guid("90394EB5-5D76-484e-B316-65DD4FDC944A");
    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.AddFileNode"]/*' />
    public virtual XmlElement AddFileNode(string file){
      XmlElement parent = (XmlElement)this.projFile.SelectSingleNode("//Files/Include");
      XmlElement e = this.projFile.CreateElement("File");

      if (this.IsCodeFile(file)){
        e.SetAttribute("BuildAction", "Compile");
        e.SetAttribute("SubType", "Code");
      } else if (this.IsEmbeddedResource(file)){
        e.SetAttribute("BuildAction", "EmbeddedResource");
      } else{
        e.SetAttribute("BuildAction", "None");
        e.SetAttribute("SubType", "Content");
      }//TODO: add case for embedded resources

      e.SetAttribute("RelPath", file);
      parent.AppendChild(e);
      return e;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.AddFolderNodeToProject"]/*' />
    /// <summary>
    /// Create a node in the XML Project doc of the following kind:
    /// <Folder RelPath = "folderName\" />
    /// </summary>
    /// <param name="foldernName"></param>
    /// <returns></returns>
    //
    public virtual XmlElement AddFolderNodeToProject(string folderName){
      XmlElement parent = (XmlElement)this.projFile.SelectSingleNode("//Files/Include");
      XmlElement e = this.projFile.CreateElement("Folder");

      e.SetAttribute("RelPath", folderName + "\\");
      parent.AppendChild(e);
      return e;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.AddReference"]/*' />
    public virtual void AddReference(string name, string path, string projectReference){
      string projectGuid = null;
      if (projectReference != null){
        int rbi = projectReference.IndexOf('}');
        if (rbi > 0){
          projectGuid = projectReference.Substring(0, rbi+1);
        }
      }
      // first check if this guy is already in it...
      foreach (XmlElement f in this.projFile.SelectNodes("//References/Reference")){
        if (projectGuid != null){
          string pguid = f.GetAttribute("Project");
          if (string.Compare(projectGuid, pguid, true, System.Globalization.CultureInfo.InvariantCulture) == 0){
            return;
          }
        }else{
          string file = f.GetAttribute("AssemblyName") + ".dll";

          if (VsShell.IsSamePath(name, file)){
            //TODO: provide a way for an extender to issue a message here
            return;
          }
        }
      }
      // need to insert the reference into the reference list of the project document for persistence
      XmlNode parent = this.projFile.SelectSingleNode("//References");
      XmlElement e = this.projFile.CreateElement("Reference");
      if (projectGuid != null){
        e.SetAttribute("Name", name);
        e.SetAttribute("Project", projectGuid);
        e.SetAttribute("Private", "true");
      }else{
        // extract the assembly name out of the absolute filename
        try{
//          e.SetAttribute("Name", Path.GetFileNameWithoutExtension(name));
          e.SetAttribute("Name", Path.GetFileNameWithoutExtension(path));
        }
        catch {
          e.SetAttribute("Name", name);
        }
        e.SetAttribute("AssemblyName", Path.GetFileNameWithoutExtension(path));
        InitPrivateAttribute(path, e);
        try{
          // create the hint path. If that throws, no harm done....
          string path2 = path.Replace("#","%23");
#if WHIDBEY
          Uri hintURI = new Uri(path2);
          string baseUriString = this.projFile.BaseURI.Replace("#","%23");
          Uri baseURI = new Uri(baseUriString);
          string diff = baseURI.MakeRelativeUri(hintURI).ToString();
#else
          Uri hintURI = new Uri(path2, true);
          string baseUriString = this.projFile.BaseURI.Replace("#","%23");
          Uri baseURI = new Uri(baseUriString, true);
          string diff = baseURI.MakeRelative(hintURI);
#endif
          e.SetAttribute("HintPath", diff);
        } catch{
        }
      }
      parent.AppendChild(e);
      // need to put it into the visual list
      HierarchyNode node = CreateNode(this.projectMgr, HierarchyNodeType.Reference, e);

      // MB - 01/21/05 - leaving this here in case I can find a way to do this.
//      if (projectGuid != null){
//        // For projects in the solution, set a project dependence so the
//        // current project depends on the referenced project.
//        IVsSolution solution = this.Site.GetService(typeof(SVsSolution)) as IVsSolution;
//        object pvar;
//        int i = solution.GetProperty(__VSPROPID_ProjectCount, out pvar);
//      }

      referencesFolder.AddChild(node);
      this.OnAddReferenceNode(node);
    }

    /// <summary>
    /// Initializes the Private attribute of e depending on whether the assembly at path is in the GAC or not.
    /// </summary>
    private void InitPrivateAttribute(string path, XmlElement e){     
      // Figure out whether to make a local copy based on whether the assembly is in the GAC or not.
      // User will be able to override this using assembly reference node properties.
      string localCopy = "false";
      if (this.MustCopyReferencedAssembly(path)) localCopy = "true";
      e.SetAttribute("Private", localCopy);
    }
    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.MustCopyReferencedAssembly"]/*' />
    public abstract bool MustCopyReferencedAssembly(string path);
    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.AddReference1"]/*' />
    /// <summary>
    /// Get's called from the hierarchy node implementation of AddComponent. 
    /// purpose: analyze the selectorData
    /// -> add component to persistence data
    /// -> if successfull, add to visual rep
    /// </summary>
    /// <param name="selectorData"></param>
    public void AddReference(Microsoft.VisualStudio.Shell.Interop.VSCOMPONENTSELECTORDATA selectorData){
      this.AddReference(selectorData.bstrTitle, selectorData.bstrFile, selectorData.bstrProjRef);
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.CreateNode"]/*' />
    protected virtual HierarchyNode CreateNode(Project root, HierarchyNodeType type, XmlElement projNode){
      if (type == HierarchyNodeType.File){
        HierarchyItemNode hi = new HierarchyItemNode(this, type, projNode);

        if (NodeHasDesigner(projNode)){
          hi.HasDesigner = true;
        }

        return hi;
      }

      return new HierarchyNode(root, type, projNode);
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.CreateNode1"]/*' />
    protected virtual HierarchyNode CreateNode(Project root, HierarchyNodeType type, string direcoryPath){
      return new HierarchyNode(root, type, direcoryPath);
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetNodeProperties"]/*' />        
    public virtual object GetNodeProperties(HierarchyNode node)
 {
      switch (node.NodeType)
   {
        case HierarchyNodeType.Reference:
          return new ReferenceProperties(node);

        case HierarchyNodeType.File:
          return new FileProperties(node);

        case HierarchyNodeType.Folder:
          return new FolderProperties(node);

        case HierarchyNodeType.Root:
          return new ProjectProperties(this);
      }
      return null;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.NodeHasDesigner"]/*' />
    public virtual bool NodeHasDesigner(XmlElement projNode){
      return false;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.Reload"]/*' />
    public virtual void Reload(){
      this.projFile = new XmlDocument();
      this.projFile.Load(this.filename);
      this.xmlNode = this.projFile.DocumentElement;
      // load the guid
      XmlElement state = this.StateElement = (XmlElement)this.GetProjectStateElement(this.xmlNode);

      if (state != null){
        string projectGuid = state.GetAttribute("ProjectGuid");
        this.projectGuid = projectGuid == string.Empty ? new Guid() : new Guid(projectGuid);
      }

      XmlElement refNode = (XmlElement)projFile.SelectSingleNode("//References");

      if (refNode != null){
        referencesFolder = CreateNode(this, HierarchyNodeType.RefFolder, refNode);
        AddChild(referencesFolder);
        foreach (XmlElement e in refNode.SelectNodes("Reference")){
          HierarchyNode node = CreateNode(this, HierarchyNodeType.Reference, e);

          referencesFolder.AddChild(node);
        }
      }

      foreach (XmlElement e in this.projFile.SelectNodes("//Files/Include/File")){
        string strPath = e.GetAttribute("RelPath");
        uint itemId;
        HierarchyNode currentParent = this;

        strPath = Path.GetDirectoryName(strPath);
        if (strPath.Length > 0){
          // use the relative to verify the folders...
          CreateFolderNodes(strPath);
          // now create an absolute ouf of it
#if WHIDBEY
          Uri uri = new Uri(new Uri(this.projFile.BaseURI), strPath);
#else
          Uri uri = new Uri(new Uri(this.projFile.BaseURI), strPath, true);
#endif

          strPath = uri.LocalPath; //??? 
          strPath += "\\";
          this.ParseCanonicalName(strPath, out itemId);
          currentParent = this.NodeFromItemId(itemId);
        }

        currentParent.AddChild(this.CreateNode(this, HierarchyNodeType.File, e));
      }

      foreach (XmlElement e in this.projFile.SelectNodes("//Files/Include/Folder")){
        // so we do have some empty folders....
        string strPath = e.GetAttribute("RelPath");

        CreateFolderNodes(strPath);
      }

      SetProjectFileDirty(false);
      this.projFile.NodeChanged += new XmlNodeChangedEventHandler(OnNodeChanged);
      this.projFile.NodeInserted += new XmlNodeChangedEventHandler(OnNodeChanged);
      this.projFile.NodeRemoved += new XmlNodeChangedEventHandler(OnNodeChanged);

      RegisterSccProject();
    }

    /// <summary>
    /// walks the subpaths of a project relative path
    /// and checks if the folder nodes hierarchy is already there, if not creates it...
    /// </summary>
    /// <param name="strPath"></param>
    void CreateFolderNodes(string strPath){
      string[] parts;
      HierarchyNode curParent;

      parts = strPath.Split(Path.DirectorySeparatorChar);
      strPath = "";
      curParent = this;
      // now we have an array of subparts....
      for (int i = 0; i < parts.Length; i++){
        if (parts[i].Length > 0){
          strPath += parts[i];
          curParent = VerifySubFolderExists(curParent, strPath);
          strPath += "\\";
        }
      }
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.VerifySubFolderExists"]/*' />
    /// <summary>
    /// takes a path and verifies that we have a node with that name, if not, add it to this node.
    /// </summary>
    public HierarchyNode VerifySubFolderExists(HierarchyNode parent, string strPath){
      HierarchyNode folderNode = null;
      uint uiItemId;
#if WHIDBEY
      Uri uri = new Uri(new Uri(this.projFile.BaseURI), strPath);
#else
      Uri uri = new Uri(new Uri(this.projFile.BaseURI), strPath, true);
#endif

      strPath = uri.LocalPath; //??? 
      // folders end in our storage with a backslash, so add one...
      strPath += "\\";
      this.ParseCanonicalName(strPath, out uiItemId);
      if (uiItemId == 0){
        // folder does not exist yet...
        folderNode = CreateNode(this, HierarchyNodeType.Folder, strPath);
        parent.AddChild(folderNode);
      } else{
        folderNode = this.NodeFromItemId(uiItemId);
      }

      return folderNode;
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////
    //  uses the solution build manager interface to get the active config object
    //    
    //////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetActiveConfiguration"]/*' />
    public XmlElement GetActiveConfiguration(){
      IVsSolutionBuildManager pSolutionBuildManger = this.Site.GetService(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager;
      if (pSolutionBuildManger == null) return null;

      IVsProjectCfg[] pProjectCfg = new IVsProjectCfg[1];
      HResult result = (HResult) pSolutionBuildManger.FindActiveProjectCfg(IntPtr.Zero, IntPtr.Zero, this, pProjectCfg);
      if (result == HResult.S_OK){
        ProjectConfig current = pProjectCfg[0] as ProjectConfig;
        if (current != null)
          return current.Node;
      }
      return (XmlElement)this.projFile.SelectSingleNode("*/*/Build/Settings/Config");
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.OnNodeChanged"]/*' />
    protected void OnNodeChanged(object sender, XmlNodeChangedEventArgs a){
      // This check stops SelectSingleNode from causing the project
      // to get dirty.  Walking up the parent chain to see if the node
      // is actually in the tree or not.
      if (sender == this.projFile && (IsInTree(this.projFile, a.OldParent) || IsInTree(this.projFile, a.NewParent) || a.Action == XmlNodeChangedAction.Change)){
        SetProjectFileDirty(true);
      }
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.SetProjectFileDirty"]/*' />
    public void SetProjectFileDirty(bool value){
      this.options = null;
      if (this.dirty = value){
        this.LastModifiedTime = DateTime.Now;
      }
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.ProjectFolder"]/*' />
    public string ProjectFolder{
      get{ return Path.GetDirectoryName(this.filename); }
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.ProjectFile"]/*' />
    public string ProjectFile{
      get{ return Path.GetFileName(this.filename); }
      set{ this.SetEditLabel(value); }
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetOutputAssembly"]/*' />
    public string GetOutputAssembly(XmlElement config){
      ProjectOptions options = this.GetProjectOptions(config);

      return options.OutputAssembly;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.IsInTree"]/*' />
    protected bool IsInTree(XmlDocument d, XmlNode n){
      while (n != null){
        if (n == d){ return true; }

        n = n.ParentNode;
      }

      return false;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.NodeFromItemId"]/*' />
    public HierarchyNode NodeFromItemId(uint itemId){
      if (VsConstants.VSITEMID_ROOT == itemId){
        return this;
      } else if (VsConstants.VSITEMID_NIL == itemId){
        return null;
      } else if (VsConstants.VSITEMID_SELECTION == itemId){
        throw new NotImplementedException();
      }

      return (HierarchyNode)this.ItemIdMap.FromCookie(itemId);
    }

    //================== Ported from _VxModule ==========================
    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetIVsUIHierarchyWindow"]/*' />
    public IVsUIHierarchyWindow GetIVsUIHierarchyWindow(Guid guidPersistenceSlot){
      IVsWindowFrame frame;

      this.UIShell.FindToolWindow(0, ref guidPersistenceSlot, out frame);

      object pvar;

      frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out pvar);
      if (pvar != null){
        IVsWindowPane pane = (IVsWindowPane)pvar;

        return (IVsUIHierarchyWindow)pane;
      }

      return null;
    }
    
    #region IVsGetCfgProvider methods
    //=================================================================================
    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetCfgProvider"]/*' />
    public virtual int GetCfgProvider(out IVsCfgProvider p){
      p = ((IVsCfgProvider)this);
      return 0;
    }
    #endregion

    #region IVsCfgProvider2 methods
    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.AddCfgsOfCfgName"]/*' />
    public virtual int AddCfgsOfCfgName(string name, string cloneName, int fPrivate){
      this.configProvider.AddCfgsOfCfgName(name, cloneName, fPrivate);
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.AddCfgsOfPlatformName"]/*' />
    public virtual int AddCfgsOfPlatformName(string platformName, string clonePlatformName){
      this.configProvider.AddCfgsOfPlatformName(platformName, clonePlatformName);
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.AdviseCfgProviderEvents"]/*' />
    public virtual int AdviseCfgProviderEvents(IVsCfgProviderEvents sink, out uint cookie){
      this.configProvider.AdviseCfgProviderEvents(sink, out cookie);
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.DeleteCfgsOfCfgName"]/*' />
    public virtual int DeleteCfgsOfCfgName(string name){
      this.configProvider.DeleteCfgsOfCfgName(name);
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.DeleteCfgsOfPlatformName"]/*' />
    public virtual int DeleteCfgsOfPlatformName(string platName){
      this.configProvider.DeleteCfgsOfPlatformName(platName);
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetCfgNames"]/*' />
    public virtual int GetCfgNames(uint celt, string[] names, uint[] actual){
      this.configProvider.GetCfgNames(celt, names, actual);
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetCfgOfName"]/*' />
    public virtual int GetCfgOfName(string name, string platName, out IVsCfg cfg){
      this.configProvider.GetCfgOfName(name, platName, out cfg);
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetCfgProviderProperty"]/*' />
    public virtual int GetCfgProviderProperty(int propid, out object var){
      this.configProvider.GetCfgProviderProperty(propid, out var);
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetCfgs"]/*' />
    public virtual int GetCfgs(uint celt, IVsCfg[] a, uint[] actual, uint[] flags){
      this.configProvider.GetCfgs(celt, a, actual, flags);
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetPlatformNames"]/*' />
    public virtual int GetPlatformNames(uint celt, string[] names, uint[] actual){
      this.configProvider.GetPlatformNames(celt, names, actual);
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetSupportedPlatformNames"]/*' />
    public virtual int GetSupportedPlatformNames(uint celt, string[] names, uint[] actual){
      this.configProvider.GetSupportedPlatformNames(celt, names, actual);
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.RenameCfgsOfCfgName"]/*' />
    public virtual int RenameCfgsOfCfgName(string old, string newname){
      this.configProvider.RenameCfgsOfCfgName(old, newname);
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.UnadviseCfgProviderEvents"]/*' />
    public virtual int UnadviseCfgProviderEvents(uint cookie){
      this.configProvider.UnadviseCfgProviderEvents(cookie);
      return 0;
    }
    #endregion

    #region IVsProjectCfgProvider methods
    //==============================================================================
    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.get_UsesIndependentConfigurations"]/*' />
    public virtual int get_UsesIndependentConfigurations(out int pf){
      pf = 0;
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.OpenProjectCfg"]/*' />
    public virtual int OpenProjectCfg(string name, out  IVsProjectCfg cfg){
      cfg = null;
      foreach (XmlElement e in this.projFile.SelectNodes("//Build/Settings/Config")){
        if (name == e.GetAttribute("Name")){
          cfg = new ProjectConfig(this, e);
          break;
        }
      }
      return 0;
    }
    #endregion

    #region IPersist
    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetClassID"]/*' />
    public virtual int GetClassID(out Guid clsid){
      clsid = this.GetProjectGuid();
      return 0;
    }
    #endregion 

    #region IPersistFileFormat methods
    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetCurFile"]/*' />
    public virtual int GetCurFile(out string name, out uint formatIndex){
      name = this.filename;
      formatIndex = 0;
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetFormatList"]/*' />
    public virtual int GetFormatList(out string formatlist){
      formatlist = "XML";
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.InitNew"]/*' />
    public virtual int InitNew(uint formatIndex){
      // TODO
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.IsDirty"]/*' />
    public virtual int IsDirty(out int isDirty){
      isDirty = (dirty ? 1 : 0);
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.Load1"]/*' />
    public virtual int Load(string filename, uint mode, int readOnly){
      this.filename = filename;
      this.Reload();
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.Save"]/*' />
    public virtual int Save(string filename, int fremember, uint formatIndex){
      SuspendWatchingProjectFileChanges();
      try{
        StreamWriter sw = new StreamWriter(filename);
        this.projFile.Save(new MyXmlWriter(sw));
        sw.Flush();
        sw.Close();
        SetProjectFileDirty(false);
      }catch (Exception e){
        string caption = SR.GetString(SR.ErrorSaving);
        MessageBox.Show(e.Message, caption);
      }
      if (fremember != 0) this.filename = filename;
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.SaveCompleted"]/*' />
    public virtual int SaveCompleted(string filename){
      ResumeWatchingProjectFileChanges();
      return 0;
    }    
    #endregion 

    bool _suspended;

    IVsDocDataFileChangeControl _ddfcc;

    void SuspendWatchingProjectFileChanges(){
      if (_suspended || this.Site == null) return;

      IVsRunningDocumentTable pRDT = this.Site.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;

      if (pRDT == null) return;

      IVsHierarchy ppHier;
      uint pid;
      IntPtr docData;
      uint docCookie;

      pRDT.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, filename, out ppHier, out pid, out docData, out docCookie);
      if (docCookie == 0 || docData == IntPtr.Zero)
        return;

      IVsFileChangeEx fce = this.Site.GetService(typeof(SVsFileChangeEx)) as IVsFileChangeEx;

      if (fce != null){
        _suspended = true;
        fce.IgnoreFile(0, filename, 1);
        try{
          _ddfcc = Marshal.GetTypedObjectForIUnknown(docData, typeof(IVsDocDataFileChangeControl)) as IVsDocDataFileChangeControl;
          if (_ddfcc != null){
            _ddfcc.IgnoreFileChanges(1);
          }
        } catch (Exception){
        }
      }
      // bugbug: do I need to "release" the docData IUnknown pointer
      // or does interop take care of it?
    }

    void ResumeWatchingProjectFileChanges(){
      if (!_suspended)
        return;

      IVsFileChangeEx fce = this.Site.GetService(typeof(SVsFileChangeEx)) as IVsFileChangeEx;

      if (fce != null){
        fce.IgnoreFile(0, filename, 0);
      }

      if (_ddfcc != null){
        _ddfcc.IgnoreFileChanges(0);
        _ddfcc = null;
      }

      _suspended = false;
    }

    //===================================================================
    // HierarchyNode overrides
    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.FullPath"]/*' />
    public override string FullPath{
      get{
        return filename;
      }
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.Close"]/*' />
    public override int Close(){
      Project.ProjectList.Remove(this.automation);
      automation = null;
      this.ClearLibraryReferences();
      this.ClearStaticReferencesToThis();
      UnregisterSccProject();
#if WHIDBEY
      // release the task manager
      if ((this.taskManager != null || this.taskManagerBuild != null) && Site != null) {
        ITaskManagerFactory taskManagerFactory = (ITaskManagerFactory)Site.GetService(typeof(ITaskManagerFactory));
        if (taskManagerFactory != null) {
          if (this.taskManager != null)      taskManagerFactory.ReleaseSharedTaskManager(this.taskManager);
          if (this.taskManagerBuild != null) taskManagerFactory.ReleaseSharedTaskManager(this.taskManagerBuild);
        }
      }
      this.taskManager = null;
      this.taskManagerBuild = null;
#endif
      
      base.Close();
      if (this.taskProvider != null){
        this.taskProvider.Dispose();
        this.taskProvider = null;
      }
      this.Site = null;
      this.UIShell = null;
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.SetSite"]/*' />
    public override int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider site){
      this.Site = new ServiceProvider(site);
      this.UIShell = this.Site.GetService(typeof(SVsUIShell)) as IVsUIShell;
      // if we are a new project open up default file
      if (this.fNewProject){
        HierarchyNode child = this.FirstChild; // that should be the reference folder....
        IVsUIHierarchyWindow uiWindow = this.GetIVsUIHierarchyWindow(VsConstants.Guid_SolutionExplorer);
        this.fNewProject = false;
        while (child != null && child.NodeType != HierarchyNodeType.File){
          child = child.NextSibling;
        }
        /*
        //  BUGBUG: that should work in my opinion..... but throws OLE exceptions in the interops... need to check with next drop
        if (uiWindow != null && child!=null){
          object dummy = null; 
          uiWindow.ExpandItem(this.projectMgr, child.ID, __EXPANDFLAGS.EXPF_ExpandParentsToShowItem); 
          uiWindow.ExpandItem(this.projectMgr, child.ID, __EXPANDFLAGS.EXPF_SelectItem); 
          child.OpenItem(false, false); 
        }
        */

      }
      if (taskProvider != null) taskProvider.Dispose();
      taskProvider = new TaskProvider(this.Site);
      
#if WHIDBEY
      // create a task manager
      if (taskManager == null || taskManagerBuild == null) {
        ITaskManagerFactory taskManagerFactory = (ITaskManagerFactory)this.Site.GetService(typeof(ITaskManagerFactory));
        if (taskManagerFactory != null) {
          if (taskManager==null)        taskManager = taskManagerFactory.QuerySharedTaskManager("SpecSharp", true);
          if (taskManagerBuild == null) taskManagerBuild = taskManagerFactory.QuerySharedTaskManager("SpecSharp Build", true);
        }
      }
#endif
      LoadLibrary();
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetProperty"]/*' />
    public override object GetProperty(int propId){ // __VSHPROPID_HandlesOwnReload during open?
      __VSHPROPID id = (__VSHPROPID)propId;

      switch (id){
        case __VSHPROPID.VSHPROPID_SaveName:
          /// save name is NOT the savefile, but just the user friendly name
          //           return filename;
          return this.Caption;

        case __VSHPROPID.VSHPROPID_ConfigurationProvider:
          return Marshal.GetIUnknownForObject(this);

        case __VSHPROPID.VSHPROPID_ProjectName:
          return this.Caption;

        case __VSHPROPID.VSHPROPID_ProjectDir:
          return this.ProjectFolder;

        case __VSHPROPID.VSHPROPID_TypeName:
          return GetProjectType();

          //                case __VSHPROPID.VSHPROPID_SelContainer:
          //                    return new SelectionContainer(this);

        case __VSHPROPID.VSHPROPID_ExtObject:
          return GetAutomationObject();

        case __VSHPROPID.VSHPROPID_IconImgList:
          return (int) this.ImageList.GetNativeImageList();

        case __VSHPROPID.VSHPROPID_IconIndex:
          return (int) ImageNames.Project;

        case __VSHPROPID.VSHPROPID_ShowProjInSolutionPage:
          return true;
      }//__VSHPROPID_DefaultEnableBuildProjectCfg, __VSHPROPID_CanBuildFromMemory, __VSHPROPID_DefaultEnableDeployProjectCfg
      return base.GetProperty(propId);
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetGuidProperty"]/*' />
    public override Guid GetGuidProperty(int propid){
      switch ((__VSHPROPID)propid){
        case __VSHPROPID.VSHPROPID_ProjectIDGuid:
          return ProjectIDGuid;

        case __VSHPROPID.VSHPROPID_CmdUIGuid:
          return VsConstants.guidStandardCommandSet2K;
      }  //-2054, PreferredLanguageSID?
      return base.GetGuidProperty(propid);
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.SetGuidProperty"]/*' />
    public override void SetGuidProperty(int propid, ref Guid guid){
      switch ((__VSHPROPID)propid){
        case __VSHPROPID.VSHPROPID_ProjectIDGuid:
          ProjectIDGuid = guid;
          return;
      }
      throw new COMException("", unchecked((int)OleDispatchErrors.DISP_E_MEMBERNOTFOUND));
    }

    string MakeRelative(string filename, string filename2){
      string[] parts = filename.Split(Path.DirectorySeparatorChar);
      string[] parts2 = filename2.Split(Path.DirectorySeparatorChar);

      if (parts.Length == 0 || parts2.Length == 0 || parts[0] != parts2[0]){
        return filename2; // completely different paths.
      }

      int i;

      for (i = 1; i < parts.Length && i < parts2.Length; i++){
        if (parts[i] != parts2[i]) break;
      }

      StringBuilder sb = new StringBuilder();

      for (int j = i; j < parts.Length - 1; j++){
        sb.Append("..");
        sb.Append(Path.DirectorySeparatorChar);
      }

      for (int j = i; j < parts2.Length; j++){
        sb.Append(parts2[j]);
        if (j < parts2.Length - 1)
          sb.Append(Path.DirectorySeparatorChar);
      }

      return sb.ToString();
    }

    internal bool IsReadOnly{
      get{
        return (File.GetAttributes(filename) & FileAttributes.ReadOnly) != 0;
      }
    }

    internal HierarchyNode AddExistingFile(string file){
      string relfile = MakeRelative(filename, file);
      XmlElement e = AddFileNode(relfile);
      HierarchyNode child = this.CreateNode(this, HierarchyNodeType.File, e);

      return child;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //
    //  removes items from the hierarchy. Project overwrites this
    //
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.Remove"]/*' />
    public override void Remove(bool removeFromStorage){
      // the project will not be deleted from disk, just removed      
      if (removeFromStorage)
        return;
      // Remove the entire project from the solution
      IVsSolution solution = this.Site.GetService(typeof(SVsSolution)) as IVsSolution;
      uint iOption = 1; // SLNSAVEOPT_PromptSave

      solution.CloseSolutionElement(iOption, this, 0);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //////////////////////////////////////////////////////////////////////////////////////////
    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetMkDocument"]/*' />
    /// <summary>
    /// allback from the additem dialog. Deals with adding new and existing items
    /// </summary>
    #region IVsProject3 methods

    public virtual int GetMkDocument(uint itemId, out string mkDoc){
      HierarchyNode n = this.projectMgr.NodeFromItemId(itemId);
      mkDoc = (n != null) ? n.FullPath : null;
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.AddItem"]/*' />
    public virtual int AddItem(uint itemIdLoc, VSADDITEMOPERATION op, string itemName, uint filesToOpen, string[] files, IntPtr dlgOwner, VSADDRESULT[] result){
      Guid empty = Guid.Empty;
      AddItemWithSpecific(itemIdLoc, op, itemName, filesToOpen, files, dlgOwner, 0, ref empty, null, ref empty, result);
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.AddItemWithSpecific"]/*' />
    public virtual int AddItemWithSpecific(uint itemIdLoc, VSADDITEMOPERATION op, string itemName, uint filesToOpen, string[] files, IntPtr dlgOwner, uint editorFlags, ref Guid editorType, string physicalView, ref Guid logicalView, VSADDRESULT[] result){
      result[0] = VSADDRESULT.ADDRESULT_Failure;
      //if (this.IsReadOnly) return; <-- bogus, the user can always change the project to non-readonly later when they choose to save it, or they can do saveas...
      HierarchyNode n = NodeFromItemId(itemIdLoc);
      if (n == null) return 0;

      if (n.NodeType == HierarchyNodeType.Root || n.NodeType == HierarchyNodeType.Folder){
        if (!this.projectMgr.Tracker.CanAddFiles(files)){
          return 0;
        }

        foreach (string file in files){
          HierarchyNode child;
          bool fFileAdded = false;
          bool fOverwrite = false;
          string strBaseDir = Path.GetDirectoryName(this.filename);
          string strNewFileName = "";

          if (n.NodeType == HierarchyNodeType.Folder){
            // add the folder to the path....
            strBaseDir = Path.Combine(strBaseDir, n.XmlNode.GetAttribute("RelPath"));
          }

          switch (op){
            case VSADDITEMOPERATION.VSADDITEMOP_CLONEFILE:
              // new item added. Need to copy template to new location and then add new location 
              strNewFileName = Path.Combine(strBaseDir, itemName);
              break;

            case VSADDITEMOPERATION.VSADDITEMOP_LINKTOFILE:// TODO: VSADDITEMOP_LINKTOFILE
              // we do not support this right now
              throw new NotImplementedException("VSADDITEMOP_LINKTOFILE");

            case VSADDITEMOPERATION.VSADDITEMOP_OPENFILE:{
              string strFileName = Path.GetFileName(file);
              strNewFileName = Path.Combine(strBaseDir, strFileName);
            }
              break;

            case VSADDITEMOPERATION.VSADDITEMOP_RUNWIZARD: // TODO: VSADDITEMOP_RUNWIZARD
              throw new NotImplementedException("VSADDITEMOP_RUNWIZARD");
          }
          child = this.FindChild(strNewFileName);
          if (child != null){
            // file already exists in project... message box
            string msg = SR.GetString(SR.FileAlreadyInProject);
            string caption = SR.GetString(SR.FileAlreadyInProjectCaption);

            if (MessageBox.Show(msg, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes){
              child = null;
              fOverwrite = true;
            }
          }

          if (child == null){
            // the next will be equal if file is already in the project DIR
            if (VsShell.IsSamePath(file, strNewFileName) == false){
              // now copy file
              FileInfo fiOrg = new FileInfo(file);
              fiOrg.Attributes &= ~FileAttributes.ReadOnly;

              try{
                FileInfo fiNew = fiOrg.CopyTo(strNewFileName, fOverwrite);
              } catch{
                string msg = SR.GetString(SR.FileAlreadyExists);
                string caption = SR.GetString(SR.FileAlreadyExistsCaption);

                if (MessageBox.Show(msg, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes){
                  FileInfo fiNew = fiOrg.CopyTo(strNewFileName, true);
                }
              }
            }

            fFileAdded = true;
          }

          if (fFileAdded && !fOverwrite){
            // now add the new thing to the project
            child = this.projectMgr.AddExistingFile(strNewFileName);
            n.AddChild(child);
            if (op == VSADDITEMOPERATION.VSADDITEMOP_OPENFILE){
              IVsWindowFrame frame;
              if (editorType == Guid.Empty){
                Guid view = Guid.Empty;
                this.OpenItem(child.ID, ref view, IntPtr.Zero, out frame);
              } else{
                this.OpenItemWithSpecific(child.ID, editorFlags, ref editorType, physicalView, ref logicalView, IntPtr.Zero, out frame);
              }
            }
            this.projectMgr.Tracker.OnAddFile(strNewFileName);
          }
        }

        n.OnItemsAppended(n);
        result[0] = VSADDRESULT.ADDRESULT_Success;
      }

      result[0] = VSADDRESULT.ADDRESULT_Success;
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GenerateUniqueItemName"]/*' />
    /// <summary>
    /// for now used by add folder. Called on the ROOT, as only the project should need
    /// to implement this.
    /// for folders, called with parent folder, blank extension and blank suggested root
    /// </summary>
    public virtual int GenerateUniqueItemName(uint itemIdLoc, string ext, string suggestedRoot, out string itemName){

      string rootName = "";
      string extToUse;
      int cb = 0;
      bool found = false;
      bool fFolderCase = false;
      HierarchyNode parent = this.projectMgr.NodeFromItemId(itemIdLoc);

      extToUse = ext.Trim();
      suggestedRoot = suggestedRoot.Trim();
      if (suggestedRoot.Length == 0){
        // foldercase, we assume... 
        suggestedRoot = "NewFolder";
        fFolderCase = true;
      }

      while (!found){
        rootName = suggestedRoot;
        if (cb > 0)
          rootName += cb.ToString();

        if (extToUse.Length > 0){
          rootName += extToUse;
        }

        cb++;
        found = true;
        for (HierarchyNode n = parent.FirstChild; n != null; n = n.NextSibling){
          if (rootName == n.GetEditLabel()){
            found = false;
            break;
          }

          string checkFile = Path.Combine(Path.GetDirectoryName(parent.FullPath), rootName);

          if (fFolderCase){
            if (Directory.Exists(checkFile)){
              found = false;
              break;
            }
          } else{
            if (File.Exists(checkFile)){
              found = false;
              break;
            }
          }
        }
      }

      itemName = rootName;
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetItemContext"]/*' />
    public virtual int GetItemContext(uint itemId, out Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp){
      psp = null; 
      HierarchyNode child = this.projectMgr.NodeFromItemId(itemId);
      if (child != null){
        child.GetSite(out psp);
      }
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.IsDocumentInProject"]/*' />
    public virtual int IsDocumentInProject(string mkDoc, out int pfFound, VSDOCUMENTPRIORITY[] pri, out uint itemId){
      pri[0] = VSDOCUMENTPRIORITY.DP_Unsupported;
      pfFound = 0;
      itemId = 0;
      HierarchyNode child = mkDoc == this.FullPath ? this : this.FindChild(mkDoc);
      if (child != null){
        pfFound = 1;
        pri[0] = VSDOCUMENTPRIORITY.DP_Standard;
        itemId = child.ID;
      }
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.OpenItem"]/*' />
    public virtual int OpenItem(uint itemId, ref Guid logicalView, IntPtr punkDocDataExisting, out IVsWindowFrame ppWindowFrame){
      HierarchyNode n = this.projectMgr.NodeFromItemId(itemId);
      if (n == null){
        throw new ArgumentException("Unknown itemid");
      }
      n.OpenItem(false, false, ref logicalView, punkDocDataExisting, out ppWindowFrame);
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.OpenItemWithSpecific"]/*' />
    public virtual int OpenItemWithSpecific(uint itemId, uint editorFlags, ref Guid editorType, string physicalView, ref Guid logicalView, IntPtr docDataExisting, out IVsWindowFrame frame){
      HierarchyNode n = this.projectMgr.NodeFromItemId(itemId);
      if (n == null){
        throw new ArgumentException("Unknown itemid");
      }
      n.OpenItemWithSpecific(editorFlags, ref editorType, physicalView, ref logicalView, docDataExisting, out frame);
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.RemoveItem"]/*' />
    public virtual int RemoveItem(uint reserved, uint itemId, out int result){
      HierarchyNode n = this.projectMgr.NodeFromItemId(itemId);
      if (n == null){
        throw new ArgumentException("Unknown itemid");
      }
      n.Remove(true);
      result = 1;
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.ReopenItem"]/*' />
    public virtual int ReopenItem(uint itemId, ref Guid editorType, string physicalView, ref Guid logicalView, IntPtr docDataExisting, out IVsWindowFrame frame){
      HierarchyNode n = this.projectMgr.NodeFromItemId(itemId);
      if (n == null){
        throw new ArgumentException("Unknown itemid");
      }
      n.OpenItemWithSpecific(0, ref editorType, physicalView, ref logicalView, docDataExisting, out frame);
      return 0;
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.TransferItem"]/*' />
    public virtual int TransferItem(string oldMkDoc, string newMkDoc, IVsWindowFrame frame){
      return (int)HResult.E_NOTIMPL;
    }
   
    #endregion

    string GetReferencedLibraryName(XmlElement assemblyRefNode){
      string assembly = assemblyRefNode.GetAttribute("AssemblyName");

      if (assembly == null || assembly == ""){
        // try to see if it is a project reference
        string projectGuid = assemblyRefNode.GetAttribute("Project");
        if (projectGuid != null && projectGuid.Length > 0){
          if (projectGuid[0] == '{') projectGuid = projectGuid.Trim('{','}');
          string file = this.GetOutputPathFromReferencedProject(projectGuid);
          if (file != null && file != ""){
            assembly = file;
          }
        }
      }else{

        if (!assembly.ToLower().EndsWith(".dll"))
          assembly += ".dll";

        string hint = assemblyRefNode.GetAttribute("HintPath");

        if (hint != "" && hint != null){
          hint = hint.ToLower(CultureInfo.InvariantCulture);
          if (hint.StartsWith("file:///"))
            hint = hint.Substring(8);
          assembly = hint;
          if (!PathWrapper.IsPathRooted(hint))
            assembly = PathWrapper.Combine(this.ProjectFolder, hint);
        }

        if (!File.Exists(assembly)){
          assembly = this.GetFullyQualifiedNameForReferencedLibrary(this.GetProjectOptions(this.GetActiveConfiguration()), assembly);
        }
      }

      return assembly;
    }

    // Object Browser
    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.ShowObjectBrowser"]/*' />
    public int ShowObjectBrowser(XmlElement assemblyRefNode){
      string assembly = GetReferencedLibraryName(assemblyRefNode);
      IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(VsConstants.guidCOMPLUSLibrary.ToByteArray().Length);

      System.Runtime.InteropServices.Marshal.StructureToPtr(VsConstants.guidCOMPLUSLibrary, ptr, false);
      try{
        this.AddLibraryReference(assembly); // make sure it's in the library.

        VSOBJECTINFO[] objInfo = new VSOBJECTINFO[1];

        objInfo[0].pguidLib = ptr;
        objInfo[0].pszLibName = assembly;

        IVsObjBrowser objBrowser = this.projectMgr.Site.GetService(typeof(SVsObjBrowser)) as IVsObjBrowser;

        objBrowser.NavigateTo(objInfo, 0);
      } catch{
      }
      System.Runtime.InteropServices.Marshal.FreeCoTaskMem(ptr);
      return 0;
    }

    Hashtable libraryList = new Hashtable();

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.AddLibraryReference"]/*' />
    public void AddLibraryReference(string assembly){
      try{
        string key = assembly.ToLower();

        if (!libraryList.Contains(key)){
          IVsLibraryReferenceManager vlrm = this.projectMgr.Site.GetService(typeof(IVsLibraryReferenceManager)) as IVsLibraryReferenceManager;
          IVsLibrary library = (IVsLibrary)vlrm;

          vlrm.AddComponentReference(key, library);
          libraryList[key] = assembly;
        }
      } catch{
      }
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.RemoveLibraryReference"]/*' />
    public void RemoveLibraryReference(string assembly){
      try{
        if (assembly == null) return; // error case.

        string key = assembly.ToLower();

        if (libraryList.Contains(key)){
          IVsLibraryReferenceManager vlrm = this.projectMgr.Site.GetService(typeof(IVsLibraryReferenceManager)) as IVsLibraryReferenceManager;
          IVsLibrary library = (IVsLibrary)vlrm;

          vlrm.RemoveComponentReference(key, library);
          libraryList.Remove(key);
        }
      } catch{
      }
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.ClearLibraryReferences"]/*' />
    public void ClearLibraryReferences(){
      Hashtable libraryList2 = (Hashtable)libraryList.Clone();

      foreach (string key in libraryList2.Keys){
        RemoveLibraryReference(key);
      }
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.OnRemoveReferenceNode"]/*' />
    public void OnRemoveReferenceNode(HierarchyNode node){
      RemoveLibraryReference(this.GetReferencedLibraryName(node.XmlNode));
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.OnAddReferenceNode"]/*' />
    public void OnAddReferenceNode(HierarchyNode node){
      AddLibraryReference(GetReferencedLibraryName(node.XmlNode));
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="Project.LoadLibrary"]/*' />
    public void LoadLibrary(){
      return;
      // Loading the library on startup this way is causing VS to crash sometimes when 
      // exploring these components in the object browser.  Not doing this and instead
      // using "View In Object Browser" on the project references seems to work fine.
      // So at this point we probably need to implement IVsLibrary and IVsLibraryMgr
      // and so on which is definitely Post April.  The release notes will just have to
      // point out that "View In Object Browser" is the way to go.
      /*
      HierarchyNode node = this.referencesFolder.FirstChild;
      while (node != null){
        this.OnAddReferenceNode(node);
        node = node.NextSibling;
      }
      */
    }
    #region IVsDependencyProvider Members

    public int EnumDependencies(out IVsEnumDependencies ppIVsEnumDependencies){
      ppIVsEnumDependencies = new DependencyEnumerator(GetReferencedProjects((XmlElement) this.projFile.SelectSingleNode("VisualStudioProject")));
      return 0;
    }

    public int OpenDependency(string szDependencyCanonicalName, out IVsDependency ppIVsDependency){
      ppIVsDependency = null;
      return (int)HResult.E_NOTIMPL;
    }

    #endregion
  
    #region IVsSccProject2 Members

    public int SccGlyphChanged(int cAffectedNodes, uint[] rgitemidAffectedNodes, VsStateIcon[] rgsiNewGlyphs, uint[] rgdwNewSccStatus) {
      if (cAffectedNodes == 0 || rgitemidAffectedNodes == null || rgsiNewGlyphs == null || rgdwNewSccStatus == null){
        // This means VS wants us to call StateIconIndex property change listeners for all nodes in the hierarchy.
        this.OnPropertyChanged(this, (int) __VSHPROPID.VSHPROPID_StateIconIndex, 0);
        // Walk the project node's descendants depth-first
        HierarchyNode n = this.firstChild;
        for (;;){
          if (n is HierarchyItemNode)
            n.OnPropertyChanged(n, (int) __VSHPROPID.VSHPROPID_StateIconIndex, 0);
          if (n.firstChild != null)
            n = n.firstChild;
          else{
            for (;;){
              if (n == this)
                goto done;
              if (n.nextSibling != null){
                n = n.nextSibling;
                break;
              }
              n = n.parentNode;
            }
          }
        }
      done:;
      }else{
        for (int i = 0; i < cAffectedNodes; i++){
          HierarchyNode node = this.NodeFromItemId(rgitemidAffectedNodes[i]);
          node.OnPropertyChanged(node, (int) __VSHPROPID.VSHPROPID_StateIconIndex, 0);
        }
      }
      return (int)HResult.S_OK;
    }

    public int GetSccFiles(uint itemid, CALPOLESTR[] pCaStringsOut, CADWORD[] pCaFlagsOut) {
      HierarchyNode node = this.NodeFromItemId(itemid);
      if (node is Project || node is HierarchyItemNode){
        string url = node.FullPath;
        pCaStringsOut[0].cElems = 1;
        IntPtr pElems = Marshal.AllocCoTaskMem(IntPtr.Size);
        IntPtr pElem = Marshal.StringToCoTaskMemAuto(url);
        Marshal.WriteIntPtr(pElems, pElem);
        pCaStringsOut[0].pElems = pElems;
        pCaFlagsOut[0].cElems = 0;
        return (int)HResult.S_OK;
      }
      return (int)HResult.E_NOTIMPL;
    }

    static void SetOrRemoveAttribute(XmlElement element, string name, string value){
      if (value == null || value == string.Empty){
        if (element.HasAttribute(name))
          element.RemoveAttribute(name);
      }else{
        if (element.GetAttribute(name) != value)
          element.SetAttribute(name, value);
      }
    }

    public int SetSccLocation(string pszSccProjectName, string pszSccAuxPath, string pszSccLocalPath, string pszSccProvider) {
      SetOrRemoveAttribute(this.StateElement, "SccProjectName", pszSccProjectName);
      SetOrRemoveAttribute(this.StateElement, "SccAuxPath", pszSccAuxPath);
      SetOrRemoveAttribute(this.StateElement, "SccLocalPath", pszSccLocalPath);
      SetOrRemoveAttribute(this.StateElement, "SccProvider", pszSccProvider);
      return (int)HResult.S_OK;
    }

    void RegisterSccProject(){
      string sccProjectName = this.StateElement.GetAttribute("SccProjectName");
      string sccAuxPath = this.StateElement.GetAttribute("SccAuxPath");
      string sccLocalPath = this.StateElement.GetAttribute("SccLocalPath");
      string sccProvider = this.StateElement.GetAttribute("SccProvider");
      if (sccProjectName != string.Empty || sccAuxPath != string.Empty || sccLocalPath != string.Empty || sccProvider != string.Empty){
        IVsSccManager2 sccManager = Site.GetService(typeof(SVsSccManager)) as IVsSccManager2;
        if (sccManager != null){
          sccManager.RegisterSccProject(this, sccProjectName, sccAuxPath, sccLocalPath, sccProvider);
        }
      }
    }

    void UnregisterSccProject(){
      IVsSccManager2 sccManager = Site.GetService(typeof(SVsSccManager)) as IVsSccManager2;
      if (sccManager != null){
        sccManager.UnregisterSccProject(this);
      }
    }

    public int GetSccSpecialFiles(uint itemid, string pszSccFile, CALPOLESTR[] pCaStringsOut, CADWORD[] pCaFlagsOut) {
      return (int)HResult.E_NOTIMPL;
    }

    #endregion
  } // end of class

  internal class BuildDependency : IVsBuildDependency{
    const int FALSE = 0;
    const int TRUE = 1;
    const string GUID_VS_DEPTYPE_BUILD_PROJECT = "707d11b6-91ca-11d0-8a3e-00a0c91e2acd";

    Project project;

    public BuildDependency(Project project){
      this.project = project;
    }

    #region IVsBuildDependency Members

    public int get_MustUpdateBefore(out int pfMustUpdateBefore){
      pfMustUpdateBefore = TRUE;
      return 0;
    }

    public int get_ReferredProject(out object ppIUnknownProject){
      ppIUnknownProject = project;
      return 0;
    }

    public int get_HelpFile(out string pbstrHelpFile){
      pbstrHelpFile = null;
      return (int)HResult.E_NOTIMPL;
    }

    public int get_HelpContext(out uint pdwHelpContext){
      pdwHelpContext = 0;
      return (int)HResult.E_NOTIMPL;
    }

    public int get_CanonicalName(out string pbstrCanonicalName){
      pbstrCanonicalName = null;
      return 0;
    }

    public int get_Description(out string pbstrDescription){
      pbstrDescription = null;
      return (int)HResult.E_NOTIMPL;
    }

    public int get_Type(out Guid pguidType){
      pguidType = new Guid(GUID_VS_DEPTYPE_BUILD_PROJECT);
      return 0;
    }

    #endregion

  }

  internal class DependencyEnumerator: IVsEnumDependencies{
    const int S_OK = 0;
    const int S_FALSE = 1;

    IList/*<Project>*/ referencedProjects;
    int currentIndex;

    public DependencyEnumerator(IList/*<Project>*/ referencedProjects){
      this.referencedProjects = referencedProjects;
    }

    DependencyEnumerator(IList/*<Project>*/ referencedProjects, int currentIndex){
      this.referencedProjects = referencedProjects;
      this.currentIndex = currentIndex;
    }

    #region IVsEnumDependencies Members

    public int Skip(uint cElements){
      if (referencedProjects.Count - currentIndex < cElements)
        return S_FALSE;
      currentIndex += (int) cElements;
      return S_OK;
    }

    public int Clone(out IVsEnumDependencies ppIVsEnumDependencies){
      ppIVsEnumDependencies = new DependencyEnumerator(referencedProjects, currentIndex);
      return 0;
    }

    public int Reset(){
      currentIndex = 0;
      return S_OK;
    }

    public int Next(uint cElements, IVsDependency[] rgpIVsDependency, out uint pcElementsFetched){
      int n = referencedProjects.Count - currentIndex;
      int m = n < cElements ? n : (int) cElements;
      for (int i = 0; i < m; i++)
        rgpIVsDependency[i] = new BuildDependency((Project) referencedProjects[currentIndex++]);
      pcElementsFetched = (uint) m;
      return m == cElements ? S_OK : S_FALSE;
    }

    #endregion

  }
  public class ProjectOptions : System.CodeDom.Compiler.CompilerParameters{
    public ModuleKindFlags ModuleKind = ModuleKindFlags.ConsoleApplication;
    public bool EmitManifest = true;
    public StringCollection DefinedPreProcessorSymbols;
    public string XMLDocFileName;
    public string RecursiveWildcard;
    public StringCollection ReferencedModules;
    public string Win32Icon;
#if !WHIDBEY
    private StringCollection embeddedResources = new StringCollection();
    public StringCollection EmbeddedResources{
      get{return this.embeddedResources;}
    }
    private StringCollection linkedResources = new StringCollection();
    public StringCollection LinkedResources{
      get{return this.linkedResources;}
    }
#endif
    public bool PDBOnly;
    public bool Optimize;
    public bool IncrementalCompile;
    public int[] SuppressedWarnings;
    public bool CheckedArithmetic;
    public bool AllowUnsafeCode;
    public bool DisplayCommandLineHelp;
    public bool SuppressLogo;
    public long BaseAddress;
    public string BugReportFileName;
    public object CodePage; //must be an int if not null
    public bool EncodeOutputInUTF8;
    public bool FullyQualifiyPaths;
    public int FileAlignment;
    public bool NoStandardLibrary;
    public StringCollection AdditionalSearchPaths;
    public bool HeuristicReferenceResolution;
    public string RootNamespace;
    public bool CompileAndExecute;
    public object UserLocaleId; //must be an int if not null
    public string ShadowedAssembly;
    public string StandardLibraryLocation;
    public PlatformType TargetPlatform;
    public string TargetPlatformLocation;
    public string AssemblyKeyFile;
    public string AssemblyKeyName;
    public bool DelaySign;
    public bool DisableInternalChecks;
    public bool DisableAssumeChecks;
    public bool DisableDefensiveChecks;
    public bool DisableGuardedClassesChecks;
    public bool DisableInternalContractsMetadata;
    public bool DisablePublicContractsMetadata;
    public virtual string GetOptionHelp() {
      return null;
    }
  }
}
internal sealed class PathWrapper {
  public static string Combine(string path1, string path2) {
    if (path1 == null || path1.Length == 0) return path2;
    if (path2 == null || path2.Length == 0) return path1;
    char ch = path2[0];
    if (ch == System.IO.Path.DirectorySeparatorChar || ch == System.IO.Path.AltDirectorySeparatorChar || (path2.Length >= 2 && path2[1] == System.IO.Path.VolumeSeparatorChar))
      return path2;
    ch = path1[path1.Length - 1];
    if (ch != System.IO.Path.DirectorySeparatorChar && ch != System.IO.Path.AltDirectorySeparatorChar && ch != System.IO.Path.VolumeSeparatorChar)
      return (path1 + System.IO.Path.DirectorySeparatorChar + path2);
    return path1 + path2;
  }
  public static string GetExtension(string path) {
    if (path == null) return null;
    int length = path.Length;
    for (int i = length; --i >= 0; ) {
      char ch = path[i];
      if (ch == '.') {
        if (i != length - 1)
          return path.Substring(i, length - i);
        else
          return String.Empty;
      }
      if (ch == System.IO.Path.DirectorySeparatorChar || ch == System.IO.Path.AltDirectorySeparatorChar || ch == System.IO.Path.VolumeSeparatorChar)
        break;
    }
    return string.Empty;
  }
  public static String GetFileName(string path) {
    if (path == null) return null;
    int length = path.Length;
    for (int i = length; --i >= 0; ) {
      char ch = path[i];
      if (ch == System.IO.Path.DirectorySeparatorChar || ch == System.IO.Path.AltDirectorySeparatorChar || ch == System.IO.Path.VolumeSeparatorChar)
        return path.Substring(i+1);
    }
    return path;
  }
  public static String GetDirectoryName(string path) {
    if (path == null) return null;
    int length = path.Length;
    for (int i = length; --i >= 0; ) {
      char ch = path[i];
      if (ch == System.IO.Path.DirectorySeparatorChar || ch == System.IO.Path.AltDirectorySeparatorChar || ch == System.IO.Path.VolumeSeparatorChar)
        return path.Substring(0, i);
    }
    return path;
  }
  public static bool IsPathRooted(string path) {
    if (path != null) {
      int num1 = path.Length;
      if ((num1 >= 1 && (path[0] == System.IO.Path.DirectorySeparatorChar || path[0] == System.IO.Path.AltDirectorySeparatorChar)) || 
          (num1 >= 2 && path[1] == System.IO.Path.VolumeSeparatorChar)) {
        return true;
      }
    }
    return false;
  }
}
