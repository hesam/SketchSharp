using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Windows.Forms;
using System.Diagnostics;
using System.Globalization;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using System.Net;
using MSBuild = Microsoft.Build.BuildEngine;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using IServiceProvider = System.IServiceProvider;

namespace Microsoft.VisualStudio.Package
{
    /// <include file='doc\Project.uex' path='docs/doc[@for="Project"]/*' />
    /// <summary>
    /// Manages the persistent state of the project (References, options, files, etc.) and deals with user interaction via a GUI in the form a hierarchy.
    /// </summary>
    [CLSCompliant(false)]
    public abstract class Project : HierarchyNode, IVsGetCfgProvider, IVsProject3, IVsCfgProvider2, IVsProjectCfgProvider, IPersistFileFormat
    {
        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.BuildLock"]/*' />
        /// <summary>A project will only try to build if it can obtain a lock on this object</summary>
        public static readonly object BuildLock = new object();

        /// <summary>Maps integer ids to project item instances</summary>
        internal EventSinkCollection ItemIdMap = new EventSinkCollection();

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
        internal protected MSBuild.Project projFile;

        // MSBuild engine we are going to be using
        private MSBuild.Engine myEngine;

        private Microsoft.VisualStudio.Project.IDEBuildLogger buildLogger;

        private MSBuild.PropertyGroup currentConfig = null;

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.ProjectDocument"]/*' />
        [System.ComponentModel.BrowsableAttribute(false)]
        public MSBuild.Project ProjectDocument
        {
            get { return projFile; }
        }

        private ConfigProvider configProvider; //REVIEW: should these be private?

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.taskProvider;"]/*' />
        protected TaskProvider taskProvider;

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.filename;"]/*' />
        protected string filename;

        private Url baseUri;

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.BaseURI;"]/*' />
        public Url BaseURI
        {
            get
            {
                if (baseUri == null && projFile != null)
                {
                    string path = System.IO.Path.GetDirectoryName(projFile.FullFileName);
                    // Uri/Url behave differently when you have trailing slash and when you dont
                    if (!path.EndsWith("\\") && !path.EndsWith("/"))
                        path += "\\";
                    baseUri = new Url(path);
                }
                Debug.Assert(baseUri != null, "Base URL should not be null. Did you call BaseURI before loading the project?");
                return baseUri;
            }
        }

        private bool dirty;

        private bool fNewProject;

        private bool buildIsPrepared;

        // The icons used in the hierarchy view
        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.ImageList;"]/*' />
        public ImageList ImageList;

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.referencesFolder;"]/*' />
        public HierarchyNode referencesFolder;

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames"]/*' />
        public enum ImageNames { Folder, OpenFolder, ReferenceFolder, OpenReferenceFolder, Reference, Project, File }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetProjectGuid"]/*' />
        /// <summary>
        /// This Guid must match the Guid you registered under
        /// HKLM\Software\Microsoft\VisualStudio\%version%\Projects.
        /// Among other things, the Project framework uses this 
        /// guid to find your project and item templates.
        /// </summary>
        public abstract Guid GetProjectGuid();

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetCompiler"]/*' />
        public virtual ICodeCompiler GetCompiler()
        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames1"]/*' />
        {
            /// <include file='doc\Project.uex' path='docs/doc[@for="Project.return null;"]/*' />
            return null;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.CreateProjectOptions"]/*' />
        /// <summary>
        /// Override this method if you have your own project specific
        /// subclass of ProjectOptions
        /// </summary>
        /// <returns>This method returns a new instance of the ProjectOptions base class.</returns>
        public virtual ProjectOptions CreateProjectOptions()
        {
            return new ProjectOptions();
        }
        internal Automation.OAProject automation;
        internal static ArrayList ProjectList = new ArrayList();

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.Project"]/*' />
        public Project()
        {
            this.automation = new Automation.OAProject(this);
            Project.ProjectList.Add(automation);
            this.hierarchyId = NativeMethods.VSITEMID_ROOT;
            // Load the hierarchy icoBns... //TODO: call a virtual routine
            this.ImageList = GetImageList();
            this.configProvider = new ConfigProvider(this);
            this.Tracker = new TrackDocumentsHelper(this);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetImageList"]/*' />
        public virtual ImageList GetImageList()
        {
            ImageList ilist = new ImageList();
            ilist.ImageSize = new Size(16, 16);
            Stream stm = typeof(Microsoft.VisualStudio.Package.Project).Assembly.GetManifestResourceStream("Resources.Folders.bmp");
            ilist.Images.AddStrip(new Bitmap(stm));
             ilist.TransparentColor = Color.Magenta;
            return ilist;
        }

        /// <summary>
        /// Called by the project to know if the item is a file (that is part of the project)
        /// or an intermediate file used by the MSBuild tasks/targets
        /// Override this method if your project has more types or different ones
        /// </summary>
        /// <param name="type">Type name</param>
        /// <returns>True = items of this type should be included in the project</returns>
        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.IsItemTypeFileType"]/*' />
        public virtual bool IsItemTypeFileType(string type)
        {
            if (String.Compare(type, "Compile", true, CultureInfo.InvariantCulture) == 0
                || String.Compare(type, "Content", true, CultureInfo.InvariantCulture) == 0
                || String.Compare(type, "EmbeddedResource", true, CultureInfo.InvariantCulture) == 0
                || String.Compare(type, "None", true, CultureInfo.InvariantCulture) == 0)
                return true;

            // we don't know about this type, so ignore it.
            return false;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.Load"]/*' />
        public virtual void Load(string filename, string location, string name, uint flags, ref Guid iidProject, out int canceled)
        {

            // set up internal members and icons
            canceled = 0;
            this.projectMgr = this;
            this.filename = filename;
            this.fNewProject = false;
            // based on the passed in flags, this either reloads/loads a project, or tries to create a new one
            if (flags == (uint)__VSCREATEPROJFLAGS.CPF_CLONEFILE)
            {
                if (this.myEngine == null || this.myEngine.BinPath == null)
                {
                    // Create the Engine
                    this.myEngine = new MSBuild.Engine();

                    // We must set the MSBuild path prior to calling CreateNewProject or we it will fail
                    this.myEngine.BinPath = GetMsBuildPath();
                }
                // now we create a new project... we do that by loading the template and then saving under a new name
                // we also need to copy all the associated files with it.
                this.projFile = myEngine.CreateNewProject();
                this.projFile.LoadFromFile(this.filename);
                // we need to generate a new guid for the project
                this.ProjectIDGuid = Guid.NewGuid();
                // set the name of the project and the assembly name
                // the passed in name is a full filename
                string saveFileName = Path.Combine(location, name);
                string projectName = Path.GetFileNameWithoutExtension(saveFileName);
                SetProjectProperty("AssemblyName", projectName);
                SetProjectProperty("Name", projectName);
                SetProjectProperty("RootNamespace", projectName);

                IPersistFileFormat x = this;

                NativeMethods.ThrowOnFailure(x.Save(saveFileName, 1, 0));
                // now we do have the project file saved. we need to create embedded files.
                MSBuild.ItemGroup projectFiles = this.projFile.EvaluatedItems;
                foreach (MSBuild.Item item in projectFiles)
                {
                    // Ignore the item if it is a reference or folder
                    if (String.Compare(item.Type, "Reference", true, CultureInfo.InvariantCulture) == 0
                        || String.Compare(item.Type, "Folder", true, CultureInfo.InvariantCulture) == 0
                        || String.Compare(item.Type, "ProjectReference", true, CultureInfo.InvariantCulture) == 0
                        || String.Compare(item.Type, "WebReference", true, CultureInfo.InvariantCulture) == 0
                        || String.Compare(item.Type, "WebReferenceFolder", true, CultureInfo.InvariantCulture) == 0)
                        continue;

                    // MSBuilds tasks/targets can create items (such as object files),
                    // such items are not part of the project per say, and should not be displayed.
                    // so ignore those items.
                    if (!this.IsItemTypeFileType(item.Type))
                        continue;

                    string strRelFilePath = item.FinalItemSpec;
                    string basePath = Path.GetDirectoryName(filename);
                    string strPathToFile;
                    string strNewFileName;
                    // taking the base name from the project template + the relative pathname,
                    // and you get the filename
                    strPathToFile = Path.Combine(basePath, strRelFilePath);
                    // the new path should be the base dir of the new project (location) + the rel path of the file
                    strNewFileName = Path.Combine(location, strRelFilePath);
                    // now the copy file
                    AddFileFromTemplate(strPathToFile, strNewFileName);
                }
            }
            // now reload to fix up references
            Reload();
            // if we are a new project open up default file
            // which we delay until setsite happened
            if (flags == (uint)__VSCREATEPROJFLAGS.CPF_CLONEFILE)
            {
                this.fNewProject = true;
            }
        }

        /// <summary>
        /// Look in the registry under the current hive for the path
        /// of MSBuild
        /// </summary>
        /// <returns></returns>
        private string GetMsBuildPath()
        {
            string registryPath;
            // first, we need the registry hive currently in use
            ILocalRegistry3 localRegistry = (ILocalRegistry3)this.GetService(typeof(SLocalRegistry));
            NativeMethods.ThrowOnFailure(localRegistry.GetLocalRegistryRoot(out registryPath));
            // now that we have it, append the subkey we are interested in to it
            if (!registryPath.EndsWith("\\"))
                registryPath += '\\';
            registryPath += "MSBuild";
            // finally, get the value from the registry
            Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(registryPath, false);
            string msBuildPath = (string)key.GetValue("MSBuildBinPath", null);
            if (msBuildPath == null || msBuildPath.Length<=0)
            {
                string error = SR.GetString(SR.ErrorMsBuildRegistration);
                throw new FileLoadException(error);
            }
            return msBuildPath;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.AddFileFromTemplate"]/*' />
        /// <summary>
        /// Called to add a file to the project from a template.
        /// Override to do it yourself if you want to customize the file
        /// </summary>
        /// <param name="source">Full path of template file</param>
        /// <param name="target">Full path of file once added to the project</param>
        public virtual void AddFileFromTemplate(string source, string target)
        {
            FileInfo fiOrg = new FileInfo(source);
            FileInfo fiNew = fiOrg.CopyTo(target, true);

            fiNew.Attributes = FileAttributes.Normal; // remove any read only attributes.
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.OnOpenItem"]/*' />
        /// <summary>
        /// Called when the project opens an editor window for the given file
        /// </summary>
        public virtual void OnOpenItem(string fullPathToSourceFile)
        {
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
        public virtual Guid ProjectIDGuid
        {
            get
            {
                return this.projectGuid;
            }
            set
            {
                this.projectGuid = value;
                if (this.projFile != null)
                {
                    this.SetProjectProperty("ProjectGuid", this.projectGuid.ToString("B"));
                }
            }
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetSystemAssemblyPath"]/*' />
        public virtual string GetSystemAssemblyPath()
        {
            return Path.GetDirectoryName(typeof(object).Assembly.Location);
#if SYSTEM_COMPILER 
      // To support true cross-platform compilation we really need to use
      // the System.Compiler.dll SystemTypes class which statically loads
      // mscorlib type information from "TargetPlatform" location.
      return Path.GetDirectoryName(SystemTypes.SystemAssembly.Location);
#endif

        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetFullyQualifiedNameForReferencedLibrary"]/*' />
        public virtual string GetFullyQualifiedNameForReferencedLibrary(ProjectOptions options, string rLibraryName)
        {
            if (File.Exists(rLibraryName)) return rLibraryName;

            string rtfName = Path.Combine(GetSystemAssemblyPath(), rLibraryName);

            if (File.Exists(rtfName)) return rtfName;

            StringCollection searchPaths = null;

            if (options != null && (searchPaths = options.AdditionalSearchPaths) != null)
            {
                for (int i = 0, n = searchPaths.Count; i < n; i++)
                {
                    string path = searchPaths[i];
                    string fname = path + Path.DirectorySeparatorChar + rLibraryName;
                    if (File.Exists(fname)) return fname;
                }
            }

            return null;
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////    
        // This is called from the main thread before the background build starts.
        //    fCleanBuild is not part of the vsopts, but passed down as the callpath is differently
        //    PrepareBuild mainly creates directories and cleans house if fCleanBuild is true
        //
        //////////////////////////////////////////////////////////////////////////////////////////////////    
        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.PrepareBuild"]/*' />
        public virtual void PrepareBuild(string config, bool fCleanBuild)
        {
            if (this.buildIsPrepared && !fCleanBuild) return;

            ProjectOptions options = this.GetProjectOptions(config);
            string outputPath = Path.GetDirectoryName(options.OutputAssembly);
            // hackhack... for now, just clean the output dir
            if (fCleanBuild)
            {
                try
                {
                    Directory.Delete(outputPath, true);
                }
                catch {}
            }

            EnsureOutputPath(outputPath);
            if (options.XMLDocFileName != null && options.XMLDocFileName != "")
            {
                EnsureOutputPath(Path.GetDirectoryName(options.XMLDocFileName));
            }
            // Find out which referenced assemblies are "Private" and require a local copy.
            foreach (MSBuild.Item reference in this.projFile.GetEvaluatedItemsByType("Reference"))
            {
                // assume a local copy is needed unless Private is set to false.
                string localCopy = reference.GetAttribute("Private");

                if (localCopy != null)
                    localCopy = localCopy.Trim().ToLower();

                if (localCopy != "false")
                {
                    string fullPath = reference.GetAttribute("AssemblyName") + ".dll";
                    string hint = reference.GetAttribute("HintPath");

                    if (hint != null && hint != "")
                    {
                        // probably relative to the current file...
                        hint = hint.Replace(Path.DirectorySeparatorChar, '/');
                        Url url = new Url(this.BaseURI, hint);
                        fullPath = url.AbsoluteUrl;                        
                    }

                    // todo: what about http based references?
                    fullPath = GetFullyQualifiedNameForReferencedLibrary(options, fullPath);
                    if (File.Exists(fullPath))
                    {
                        string assemblyDir = Path.GetDirectoryName(fullPath);
                        string outputDir = Path.GetDirectoryName(outputPath);
                        string localPath = Path.Combine(outputPath, Path.GetFileName(fullPath));

                        if (localCopy == null || localCopy == "")
                        {
                            // didn't specify the Private attribute, so the default value is based on 
                            // whether the assembly is in the GAC or not.
                            InitPrivateAttribute(fullPath, new ProjectElement(this, reference, false));
                            localCopy = reference.GetAttribute("Private");
                            if (localCopy != null)
                                localCopy = localCopy.Trim().ToLower();
                        }
                        // See if the assembly is not already in out output path or it needs updating...
                        if (localCopy != "false"
                            && !NativeMethods.IsSamePath(assemblyDir, outputDir)
                            && (!File.Exists(localPath) || File.GetLastWriteTime(fullPath) > File.GetLastWriteTime(localPath)))
                        {
                            File.Copy(fullPath, localPath, true);

                            FileAttributes attrs = File.GetAttributes(localPath);

                            if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                            {
                                File.SetAttributes(localPath, attrs & ~FileAttributes.ReadOnly);
                            }
                        }
                    }
                }
            }

            this.buildIsPrepared = true;
        }


        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.EnsureOutputPath"]/*' />
        public static void EnsureOutputPath(string path)
        {
            if (path != "" && !Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch {}
            }
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.LaunchDebugger"]/*' />
        public virtual void LaunchDebugger(VsDebugTargetInfo info)
        {
            info.cbSize = (uint)Marshal.SizeOf(info);
            IntPtr ptr = Marshal.AllocCoTaskMem((int)info.cbSize);
            Marshal.StructureToPtr(info, ptr, false);
            try
            {
                IVsDebugger d = this.GetService(typeof(IVsDebugger)) as IVsDebugger;
                NativeMethods.ThrowOnFailure(d.LaunchDebugTargets(1, ptr));
            }
            finally
            {
                Marshal.FreeCoTaskMem(ptr);
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////    
        // This is called from the compiler background thread.
        //    fCleanBuild is not part of the vsopts, but passed down as the callpath is differently
        //
        //////////////////////////////////////////////////////////////////////////////////////////////////    
        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.ObsoleteBuild"]/*' />
        [System.Obsolete("Do not use as this will be removed. Use Build() instead", false)]
        public virtual bool ObsoleteBuild(uint vsopts, string config, IVsOutputWindowPane output, bool fCleanBuild)
        {
            if (fCleanBuild)
            {
                // we are done
                return true;
            }

            lock (Project.BuildLock)
            {
                ProjectOptions options = this.GetProjectOptions(config);
                CompilerResults results;
                ArrayList files = new ArrayList();
/*UNDONE: need to get this to use MSBuild
                foreach (XmlElement e in doc.SelectNodes("//Files/Include/File"))
                {
                    //TODO: Support other "BuildActions" like "EmbeddedResource"...
                    if (e.GetAttribute("BuildAction") == "Compile")
                    {
                        string rel = e.GetAttribute("RelPath");
                        Url url = new Url(new Url(doc.BaseURI), rel);
                        files.Add(url.AbsoluteUrl);
                    }
                }
*/
                try
                {
                    ICodeCompiler compiler = this.GetCompiler();
                    if (files.Count == 1)
                    {
                        string filename = (string)files[0];
                        results = compiler.CompileAssemblyFromFile(options, filename);
                    }
                    else
                    {
                        string[] fileNames = (string[])files.ToArray(typeof(string));
                        results = compiler.CompileAssemblyFromFileBatch(options, fileNames);
                    }
                }
                catch (Exception e)
                {
                    results = new CompilerResults(options.TempFiles);
                    results.Errors.Add(new CompilerError(options.OutputAssembly, 1, 1, "", "Internal Compiler Error: " + e.ToString() + "\n"));
                    results.NativeCompilerReturnValue = 1;
                }
                taskProvider.Tasks.Clear();

                int errorCount = 0;
                int warningCount = 0;

                foreach (CompilerError e in results.Errors)
                {
                    if (e.IsWarning) warningCount++;
                    else
                        errorCount++;

                    NativeMethods.ThrowOnFailure(output.OutputTaskItemString(GetFormattedErrorMessage(e, false) + "\n", VSTASKPRIORITY.TP_HIGH, VSTASKCATEGORY.CAT_BUILDCOMPILE, "", -1, e.FileName, (uint)e.Line - 1, e.ErrorText));
                }

                NativeMethods.ThrowOnFailure(output.OutputStringThreadSafe("Build complete -- " + errorCount + " errors, " + warningCount + " warnings")); //TODO: globalize
                NativeMethods.ThrowOnFailure(output.FlushToTaskList()); 
                return results.NativeCompilerReturnValue == 0;
            }
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////    
        // This is called from the compiler background thread.
        //    fCleanBuild is not part of the vsopts, but passed down as the callpath is differently
        //
        //////////////////////////////////////////////////////////////////////////////////////////////////    
        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.Build"]/*' />
        public virtual bool Build(uint vsopts, string config, IVsOutputWindowPane output, bool fCleanBuild)
        {
            if (fCleanBuild)
            {
                // we are done
                return true;
            }

            lock (Project.BuildLock)
            {
                ProjectOptions options = this.GetProjectOptions(config);
                string target = null;
                if (fCleanBuild)
                    target = "Rebuild";
                else
                    target = "Build";

                // Create our logger
                buildLogger = new Microsoft.VisualStudio.Project.IDEBuildLogger(output);
                myEngine.UnregisterAllLoggers();
                myEngine.RegisterLogger(buildLogger);
                buildLogger.ErrorString = GetWarningString();
                buildLogger.WarningString = GetWarningString();

                // Set level of details we want to log
                buildLogger.Verbosity = GetVerbosityLevel();

                // Make sure the project configuration is set properly
                this.projFile.GlobalProperties.SetProperty("Configuration", config);

                // Do the actual Build
                IDictionary buildOutputs = new Hashtable();
                bool success = projFile.BuildTarget(target, buildOutputs);

                return success;
            }
        }

        private Microsoft.Build.Framework.LoggerVerbosity GetVerbosityLevel()
        {
            string verbosity = this.GetProjectProperty("BuildVerbosity");
            if (verbosity == null
                || String.Compare(verbosity, "Normal", true, CultureInfo.InvariantCulture) == 0)
                return Microsoft.Build.Framework.LoggerVerbosity.Normal;

            if (String.Compare(verbosity, "Detailed", true, CultureInfo.InvariantCulture) == 0)
                return Microsoft.Build.Framework.LoggerVerbosity.Detailed;

            if (String.Compare(verbosity, "Diagnostic", true, CultureInfo.InvariantCulture) == 0)
                return Microsoft.Build.Framework.LoggerVerbosity.Diagnostic;

            if (String.Compare(verbosity, "Minimal", true, CultureInfo.InvariantCulture) == 0)
                return Microsoft.Build.Framework.LoggerVerbosity.Minimal;

            if (String.Compare(verbosity, "Quiet", true, CultureInfo.InvariantCulture) == 0)
                return Microsoft.Build.Framework.LoggerVerbosity.Quiet;

            Trace.WriteLine(String.Format("'{0}' is not a known verbosity level. Using Normal verbosity", verbosity));
            Debug.Fail(String.Format("'{0}' is not a known verbosity level. Using Normal verbosity", verbosity));
            return Microsoft.Build.Framework.LoggerVerbosity.Normal;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetFormattedErrorMessage"]/*' />
        public virtual string GetFormattedErrorMessage(CompilerError e, bool omitSourceFilePath)
        {
            if (e == null) return "";

            string errCode = (e.IsWarning) ? this.GetWarningString() : this.GetErrorString();
            string fileRef = e.FileName;

            if (fileRef == null)
                fileRef = "";
            else if (fileRef != "")
            {
                if (omitSourceFilePath) fileRef = Path.GetFileName(fileRef);

                fileRef += "(" + e.Line + "," + e.Column + "): ";
            }

            return fileRef + string.Format(errCode, e.ErrorNumber) + ": " + e.ErrorText;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.errorString"]/*' />
        protected string errorString = null;

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetErrorString"]/*' />
        public virtual string GetErrorString()
        {
            if (errorString == null)
            {
                this.errorString = SR.GetString(SR.Error);
            }

            return errorString;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.warningString"]/*' />
        protected string warningString = null;

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetWarningString"]/*' />
        public virtual string GetWarningString()
        {
            if (this.warningString == null)
                this.warningString = SR.GetString(SR.Warning);

            return this.warningString;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetOutputPath"]/*' />
        public string GetOutputPath(string config)
        {
            this.projFile.GlobalProperties.SetProperty("Configuration", config);
            MSBuild.PropertyGroup properties = this.projFile.EvaluatedProperties;

            return this.GetOutputPath(properties);
        }

        private string GetOutputPath(MSBuild.PropertyGroup properties)
        {
            this.currentConfig = properties;
            string outputPath = GetProjectProperty("OutputPath");

            if (outputPath != null && outputPath != "")
            {
                outputPath = outputPath.Replace('/', Path.DirectorySeparatorChar);
                if (outputPath[outputPath.Length - 1] != Path.DirectorySeparatorChar)
                    outputPath += Path.DirectorySeparatorChar;
            }

            return outputPath;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.options"]/*' />
        protected ProjectOptions options = null;

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetProjectOptions"]/*' />
        public virtual ProjectOptions GetProjectOptions(string config)
        {
            if (this.options != null)
                return this.options;

            ProjectOptions options = this.options = CreateProjectOptions();

            if (config == null)
                return options;

            options.GenerateExecutable = true;

            // Set the active configuration
            this.projFile.GlobalProperties.SetProperty("Configuration", config);
            this.currentConfig = this.projFile.EvaluatedProperties;

            string outputPath = this.GetOutputPath(this.currentConfig);
            // absolutize relative to project folder location
            outputPath = Path.Combine(this.ProjectFolder, outputPath);

            // Set some default values
            options.OutputAssembly = outputPath + this.Caption + ".exe";
            options.ModuleKind = ModuleKindFlags.ConsoleApplication;

            options.RootNamespace = GetProjectProperty("RootNamespace", false);
            options.OutputAssembly = outputPath + this.GetAssemblyName(config);

            string outputtype = GetProjectProperty("OutputType", false).ToLower();

            if (outputtype == "library")
            {
                options.ModuleKind = ModuleKindFlags.DynamicallyLinkedLibrary;
                options.GenerateExecutable = false; // DLL's have no entry point.
            }
            else if (outputtype == "winexe")
                options.ModuleKind = ModuleKindFlags.WindowsApplication;
            else
                options.ModuleKind = ModuleKindFlags.ConsoleApplication;

            options.Win32Icon = GetProjectProperty("ApplicationIcon", false);
            options.MainClass = GetProjectProperty("StartupObject", false);

            string targetPlatform = GetProjectProperty("TargetPlatform", false);

            if (targetPlatform != null && targetPlatform.Length > 0)
            {
                try { options.TargetPlatform = (PlatformType)Enum.Parse(typeof(PlatformType), targetPlatform); } catch { }
                options.TargetPlatformLocation = GetProjectProperty("TargetPlatformLocation", false);
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

            // transfer all config build options...
            if (GetBoolAttr(this.currentConfig, "AllowUnsafeBlocks"))
            {
                options.AllowUnsafeCode = true;
            }

            if (GetProjectProperty("BaseAddress", false) != null)
            {
                try { options.BaseAddress = Int64.Parse(GetProjectProperty("BaseAddress", false)); } catch { }
            }

            if (GetBoolAttr(this.currentConfig, "CheckForOverflowUnderflow"))
            {
                options.CheckedArithmetic = true;
            }

            if (GetProjectProperty("DefineConstants", false) != null)
            {
                options.DefinedPreProcessorSymbols = new StringCollection();
                foreach (string s in GetProjectProperty("DefineConstants", false).Replace(" \t\r\n", "").Split(';'))
                {
                    options.DefinedPreProcessorSymbols.Add(s);
                }
            }

            string docFile = GetProjectProperty("DocumentationFile", false);
            if (docFile != null && docFile != "")
            {
                options.XMLDocFileName = Path.Combine(this.ProjectFolder, docFile);
            }

            if (GetBoolAttr(this.currentConfig, "DebugSymbols"))
            {
                options.IncludeDebugInformation = true;
            }

            if (GetProjectProperty("FileAlignment", false) != null)
            {
                try { options.FileAlignment = Int32.Parse(GetProjectProperty("FileAlignment", false)); } catch { }
            }

            if (GetBoolAttr(this.currentConfig, "IncrementalBuild"))
            {
                options.IncrementalCompile = true;
            }

            if (GetBoolAttr(this.currentConfig, "Optimize"))
            {
                options.Optimize = true;
            }

            if (GetBoolAttr(this.currentConfig, "RegisterForComInterop"))
            {
            }

            if (GetBoolAttr(this.currentConfig, "RemoveIntegerChecks"))
            {
            }

            if (GetBoolAttr(this.currentConfig, "TreatWarningsAsErrors"))
            {
                options.TreatWarningsAsErrors = true;
            }

            if (GetProjectProperty("WarningLevel", false) != null)
            {
                try { options.WarningLevel = Int32.Parse(GetProjectProperty("WarningLevel", false)); } catch { }
            }

            foreach (MSBuild.Item reference in this.projFile.GetEvaluatedItemsByType("Reference"))
            {
                string file = reference.GetAttribute("AssemblyName") + ".dll";
                string hint = reference.GetAttribute("HintPath");

                if (hint != null && hint != "")
                {
                    // probably relative to the current file...
                    hint = hint.Replace(Path.DirectorySeparatorChar, '/');
                    Url url = new Url(this.BaseURI, hint);
                    string path = url.AbsoluteUrl;
                    file = path;
                }

                if (!options.ReferencedAssemblies.Contains(file))
                    options.ReferencedAssemblies.Add(file);
            }

            return options;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.SetTargetPlatform"]/*' />
        public virtual void SetTargetPlatform(ProjectOptions options)
        {
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetBoolAttr"]/*' />
        private bool GetBoolAttr(MSBuild.PropertyGroup properties, string name)
        {
            this.currentConfig = properties;
            string s = GetProjectProperty(name);

            return (s != null && s.ToLower().Trim() == "true");
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetBoolAttr"]/*' />
        public virtual bool GetBoolAttr(string config, string name)
        {
            // Set the active configuration
            this.projFile.GlobalProperties.SetProperty("Configuration", config);
            MSBuild.PropertyGroup properties = this.projFile.EvaluatedProperties;

            return this.GetBoolAttr(properties, name);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.Plural"]/*' />
        public virtual string Plural(int i)
        {
            return (i == 1) ? "" : "s";
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetConfigPropertyPageGuids"]/*' />
        public virtual Guid[] GetConfigPropertyPageGuids(string config)
        {
            return new Guid[0];
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetAssemblyName"]/*' />
        public virtual string GetAssemblyName(string config)
        {
            // Set the active configuration
            this.projFile.GlobalProperties.SetProperty("Configuration", config);
            MSBuild.PropertyGroup properties = this.projFile.EvaluatedProperties;

            return GetAssemblyName(properties);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetAssemblyName"]/*' />
        private string GetAssemblyName(MSBuild.PropertyGroup properties)
        {
            this.currentConfig = properties;
            string name = null;

            name = GetProjectProperty("AssemblyName");
            if (name == null)
                name = this.Caption;

            string outputtype = GetProjectProperty("OutputType", false);

            if (outputtype == "library")
            {
                outputtype = outputtype.ToLower();
                name += ".dll";
            }
            else
            {
                name += ".exe";
            }

            return name;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.IsCodeFile"]/*' />
        public virtual bool IsCodeFile(string strFileName)
        {
            return true;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetAutomationObject"]/*' />
        public override object GetAutomationObject()
        {
            return this.automation;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.CreateFileNode"]/*' />
        public ProjectElement CreateFileNode(string file, string fileType)
        {
            return new ProjectElement(this, file, fileType);
        }

        //private static Guid projItemDlgSID = new Guid("90394EB5-5D76-484e-B316-65DD4FDC944A");
        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.AddFileNode"]/*' />
        public virtual ProjectElement AddFileNode(string file)
        {
            ProjectElement newItem;

            string itemPath = file;
            if (Path.IsPathRooted(itemPath))
            {
                // If this is in the project "cone" (same folder then project or below)
                // make it relative to the current project
                string path = new Url(itemPath).AbsoluteUrl;
                string basePath = this.BaseURI.AbsoluteUrl;
                if (path.StartsWith(basePath, true, CultureInfo.InvariantCulture))
                    itemPath = path.Substring(basePath.Length);
            }

            Debug.Assert(!Path.IsPathRooted(itemPath), "Linked item not currently supported.");

            if (this.IsCodeFile(itemPath))
            {
                newItem = this.CreateFileNode(itemPath, "Compile");
                newItem.SetAttribute("SubType", "Code");
            }
            else
            {
                newItem = this.CreateFileNode(itemPath, "Content");
                newItem.SetAttribute("SubType", "Content");
            }

            return newItem;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.AddFolderNodeToProject"]/*' />
        /// <summary>
        /// Create a node in the Project of the following kind:
        /// <Folder RelPath = "folderName\" />
        /// </summary>
        /// <param name="foldernName"></param>
        /// <returns></returns>
        public virtual ProjectElement AddFolderNodeToProject(string folderName)
        {
            ProjectElement newItem = new ProjectElement(this,  null, true);

            return newItem;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.AddReference"]/*' />
        public virtual void AddReference(string name, string path)
        {
            string newFile = name;
            int extensionStart = newFile.LastIndexOf('.');
            if (extensionStart>0 && String.Compare(".dll", newFile.Substring(extensionStart), true, CultureInfo.InvariantCulture) == 0)
                newFile = newFile.Substring(0, extensionStart);

            // first check if this guy is already in it...
            foreach (MSBuild.Item item in this.projFile.GetEvaluatedItemsByType("Reference"))
            {
                ProjectElement reference = new ProjectElement(this, item, false);
                string file = reference.GetAttribute("AssemblyName");
                if (file == null && file.Length == 0)
                    file = reference.GetAttribute("Include");

                if (String.Compare(newFile, file) == 0)
                {
                    //TODO: provide a way for an extender to issue a message here
                    return;
                }
            }
            // need to insert the reference into the reference list of the project document for persistence
            ProjectElement newItem = this.CreateFileNode(name, "Reference");

            // extract the assembly name out of the absolute filename
            string assemblyNameNoExtension = name;
            try
            {
                assemblyNameNoExtension = Path.GetFileNameWithoutExtension(name);
            }
            catch {}

            newItem.SetAttribute("Name", assemblyNameNoExtension);
            newItem.SetAttribute("AssemblyName", name);

            string hintPath = null;
            InitPrivateAttribute(path, newItem);
            try
            {
                // create the hint path. If that throws, no harm done....
                Uri hintURI = new Uri(path);
                Uri baseURI = this.BaseURI.Uri;
                string diff = baseURI.MakeRelative(hintURI);
                // MakeRelative only really works if on the same drive.
                if (hintURI.Segments[1] == baseURI.Segments[1])
                {
                    diff = diff.Replace('/', '\\');
                }
                else
                {
                    diff = hintURI.LocalPath;
                }

                hintPath = diff;
            }
            catch (System.Exception ex)
            {
                CCITracing.Trace(ex);
                CCITracing.TraceData(path);
                CCITracing.TraceData(this.BaseURI.AbsoluteUrl);
            }
            if (hintPath != null)
                newItem.SetAttribute("HintPath", hintPath);

            // At this point force the item to be refreshed
            newItem.RefreshProperties();

            // need to put it into the visual list
            HierarchyNode node = CreateNode(this.projectMgr, HierarchyNodeType.Reference, newItem);

            referencesFolder.AddChild(node);
            this.OnAddReferenceNode(node);
        }

        /// <summary>
        /// Initializes the Private attribute of e depending on whether the assembly at path is in the GAC or not.
        /// </summary>
        private void InitPrivateAttribute(string path, ProjectElement item)
        {
            // Figure out whether to make a local copy based on whether the assembly is in the GAC or not.
            // User will be able to override this using assembly reference node properties.
            string localCopy = "true";
            string assemblyDir = Path.GetDirectoryName(path);

            if (NativeMethods.IsSamePath(assemblyDir, GetSystemAssemblyPath()))
            {
                localCopy = "false";
            }
            else
            {
                Uri uri = null;

                try
                {
                    uri = new Uri(path);
                }
                catch (Exception) { }
                
                if (uri != null && GlobalAssemblyCache.Contains(uri))
                {
                    localCopy = "false";
                }
            }

            item.SetAttribute("Private", localCopy);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.AddReference1"]/*' />
        /// <summary>
        /// Get's called from the hierarchy node implementation of AddComponent. 
        /// purpose: analyze the selectorData
        /// -> add component to persistence data
        /// -> if successfull, add to visual rep
        /// </summary>
        /// <param name="selectorData"></param>
        public void AddReference(Microsoft.VisualStudio.Shell.Interop.VSCOMPONENTSELECTORDATA selectorData)
        {
            this.AddReference(selectorData.bstrTitle, selectorData.bstrFile);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.CreateNode"]/*' />
        protected virtual HierarchyNode CreateNode(Project root, HierarchyNodeType type, ProjectElement item)
        {
            if (type == HierarchyNodeType.File)
            {
                HierarchyItemNode hi = new HierarchyItemNode(this, type, item);

                if (NodeHasDesigner(item.GetAttribute("Include")))
                {
                    hi.HasDesigner = true;
                }

                return hi;
            }

            return new HierarchyNode(root, type, item);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.CreateNode1"]/*' />
        protected virtual HierarchyNode CreateNode(Project root, HierarchyNodeType type, string direcoryPath)
        {
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
        public virtual bool NodeHasDesigner(string itemPath)
        {
            return false;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.Reload"]/*' />
        public virtual void Reload()
        {
            if (this.myEngine == null || this.myEngine.BinPath == null)
            {
                this.myEngine = new MSBuild.Engine();
                // We must set the MSBuild path prior to calling CreateNewProject or we it will fail
                this.myEngine.BinPath = GetMsBuildPath();
            }
            this.projFile = myEngine.CreateNewProject();
            this.projFile.LoadFromFile(this.filename);
            MSBuild.PropertyGroup projectProperties = projFile.EvaluatedProperties;

            // load the guid
            if (projectProperties != null)
            {
                try
                {
                    this.projectGuid = new Guid(this.GetProjectProperty("ProjectGuid"));
                }
                catch
                {
                    this.projectGuid = Guid.NewGuid();
                }
            }

            // Process References
            MSBuild.ItemGroup references = this.projFile.GetEvaluatedItemsByType("Reference");
            referencesFolder = CreateNode(this, HierarchyNodeType.RefFolder, "References");
            AddChild(referencesFolder);
            foreach (MSBuild.Item reference in references)
            {
                HierarchyNode node = CreateNode(this, HierarchyNodeType.Reference, new ProjectElement(this, reference, false));

                referencesFolder.AddChild(node);
            }

            // Process Files
            MSBuild.ItemGroup projectFiles = this.projFile.EvaluatedItems;
            foreach (MSBuild.Item item in projectFiles)
            {
                // Ignore the item if it is a reference or folder
                if (String.Compare(item.Type, "Reference", true, CultureInfo.InvariantCulture) == 0
                    || String.Compare(item.Type, "Folder", true, CultureInfo.InvariantCulture) == 0
                    || String.Compare(item.Type, "ProjectReference", true, CultureInfo.InvariantCulture) == 0
                    || String.Compare(item.Type, "WebReference", true, CultureInfo.InvariantCulture) == 0
                    || String.Compare(item.Type, "WebReferenceFolder", true, CultureInfo.InvariantCulture) == 0)
                    continue;

                // MSBuilds tasks/targets can create items (such as object files),
                // such items are not part of the project per say, and should not be displayed.
                // so ignore those items.
                if (!this.IsItemTypeFileType(item.Type))
                    continue;


                string strPath = item.FinalItemSpec;
                HierarchyNode currentParent = this;

                strPath = Path.GetDirectoryName(strPath);
                if (strPath.Length > 0)
                {
                    // use the relative to verify the folders...
                    currentParent = CreateFolderNodes(strPath);
                }

                currentParent.AddChild(this.CreateNode(this, HierarchyNodeType.File, new ProjectElement(this, item, false)));
            }

            // Process Folders (useful to persist empty folder)
            MSBuild.ItemGroup folders = this.projFile.GetEvaluatedItemsByType("Folder");
            foreach (MSBuild.Item folder in folders)
            {
                string strPath = folder.FinalItemSpec;
                CreateFolderNodes(strPath);
            }

            SetProjectFileDirty(false);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.CreateFolderNodes"]/*' />
        /// <summary>
        /// walks the subpaths of a project relative path
        /// and checks if the folder nodes hierarchy is already there, if not creates it...
        /// </summary>
        /// <param name="strPath"></param>
        public virtual HierarchyNode CreateFolderNodes(string strPath)
        {
            string[] parts;
            HierarchyNode curParent;

            parts = strPath.Split(Path.DirectorySeparatorChar);
            strPath = "";
            curParent = this;
            // now we have an array of subparts....
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                {
                    strPath += parts[i];
                    curParent = VerifySubFolderExists(curParent, strPath);
                    strPath += "\\";
                }
            }
            return curParent;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.VerifySubFolderExists"]/*' />
        /// <summary>
        /// takes a path and verifies that we have a node with that name, if not, add it to this node.
        /// </summary>
        public HierarchyNode VerifySubFolderExists(HierarchyNode parent, string strPath)
        {
            HierarchyNode folderNode = null;
            uint uiItemId;
            Url url = new Url(this.BaseURI, strPath);
            string strFullPath = url.AbsoluteUrl; 
            // folders end in our storage with a backslash, so add one...
            NativeMethods.ThrowOnFailure(this.ParseCanonicalName(strFullPath, out uiItemId));
            if (uiItemId == 0)
            {
                // folder does not exist yet...
                folderNode = CreateNode(this, HierarchyNodeType.Folder, strPath);
                parent.AddChild(folderNode);
            }
            else
            {
                folderNode = this.NodeFromItemId(uiItemId);
            }

            return folderNode;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.SetProjectFileDirty"]/*' />
        public void SetProjectFileDirty(bool value)
        {
            this.options = null;
            if (this.dirty = value)
            {
                this.LastModifiedTime = DateTime.Now;
                this.buildIsPrepared = false;
            }
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.ProjectFolder"]/*' />
        public string ProjectFolder
        {
            get { return Path.GetDirectoryName(this.filename); }
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.ProjectFile"]/*' />
        public string ProjectFile
        {
            get { return Path.GetFileName(this.filename); }
            set { this.SetEditLabel(value); }
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetOutputAssembly"]/*' />
        public string GetOutputAssembly(string config)
        {
            ProjectOptions options = this.GetProjectOptions(config);

            return options.OutputAssembly;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.NodeFromItemId"]/*' />
        public HierarchyNode NodeFromItemId(uint itemId)
        {
            if (NativeMethods.VSITEMID_ROOT == itemId)
            {
                return this;
            }
            else if (NativeMethods.VSITEMID_NIL == itemId)
            {
                return null;
            }
            else if (NativeMethods.VSITEMID_SELECTION == itemId)
            {
                throw new NotImplementedException();
            }

            return (HierarchyNode)this.ItemIdMap[itemId];
        }

        //================== Ported from _VxModule ==========================
        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetIVsUIHierarchyWindow"]/*' />
        public IVsUIHierarchyWindow GetIVsUIHierarchyWindow(Guid guidPersistenceSlot)
        {
            IVsWindowFrame frame;

            NativeMethods.ThrowOnFailure(this.UIShell.FindToolWindow(0, ref guidPersistenceSlot, out frame));

            object pvar;

            NativeMethods.ThrowOnFailure(frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out pvar));
            if (pvar != null)
            {
                IVsWindowPane pane = (IVsWindowPane)pvar;

                return (IVsUIHierarchyWindow)pane;
            }
            return null;
        }
    
        #region IVsGetCfgProvider methods
        //=================================================================================
        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetCfgProvider"]/*' />
        public virtual int GetCfgProvider(out IVsCfgProvider p) {
            CCITracing.TraceCall();
            p = ((IVsCfgProvider)this);
            return NativeMethods.S_OK;
        }
        #endregion

        #region IVsCfgProvider2 methods
        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.AddCfgsOfCfgName"]/*' />
        public virtual int AddCfgsOfCfgName(string name, string cloneName, int fPrivate) {
            return this.configProvider.AddCfgsOfCfgName(name, cloneName, fPrivate);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.AddCfgsOfPlatformName"]/*' />
        public virtual int AddCfgsOfPlatformName(string platformName, string clonePlatformName) {
            return this.configProvider.AddCfgsOfPlatformName(platformName, clonePlatformName);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.AdviseCfgProviderEvents"]/*' />
        public virtual int AdviseCfgProviderEvents(IVsCfgProviderEvents sink, out uint cookie) {
            return this.configProvider.AdviseCfgProviderEvents(sink, out cookie);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.DeleteCfgsOfCfgName"]/*' />
        public virtual int DeleteCfgsOfCfgName(string name) {
            return this.configProvider.DeleteCfgsOfCfgName(name);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.DeleteCfgsOfPlatformName"]/*' />
        public virtual int DeleteCfgsOfPlatformName(string platName) {
            return this.configProvider.DeleteCfgsOfPlatformName(platName);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetCfgNames"]/*' />
        public virtual int GetCfgNames(uint celt, string[] names, uint[] actual) {
            return this.configProvider.GetCfgNames(celt, names, actual);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetCfgOfName"]/*' />
        public virtual int GetCfgOfName(string name, string platName, out IVsCfg cfg) {
            return this.configProvider.GetCfgOfName(name, platName, out cfg);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetCfgProviderProperty"]/*' />
        public virtual int GetCfgProviderProperty(int propid, out object var) {
            return this.configProvider.GetCfgProviderProperty(propid, out var);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetCfgs"]/*' />
        public virtual int GetCfgs(uint celt, IVsCfg[] a, uint[] actual, uint[] flags) {
            return this.configProvider.GetCfgs(celt, a, actual, flags);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetPlatformNames"]/*' />
        public virtual int GetPlatformNames(uint celt, string[] names, uint[] actual) {
            return this.configProvider.GetPlatformNames(celt, names, actual);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetSupportedPlatformNames"]/*' />
        public virtual int GetSupportedPlatformNames(uint celt, string[] names, uint[] actual) {
            return this.configProvider.GetSupportedPlatformNames(celt, names, actual);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.RenameCfgsOfCfgName"]/*' />
        public virtual int RenameCfgsOfCfgName(string old, string newname) {
            return this.configProvider.RenameCfgsOfCfgName(old, newname);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.UnadviseCfgProviderEvents"]/*' />
        public virtual int UnadviseCfgProviderEvents(uint cookie) {
            return this.configProvider.UnadviseCfgProviderEvents(cookie);
        }
        #endregion

        #region IVsProjectCfgProvider methods
        //==============================================================================
        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.get_UsesIndependentConfigurations"]/*' />
        public virtual int get_UsesIndependentConfigurations(out int pf)
        {
            pf = 0;
            return NativeMethods.S_OK;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.OpenProjectCfg"]/*' />
        public virtual int OpenProjectCfg(string name, out  IVsProjectCfg cfg)
        {
            cfg = null;
            string[] configurations = this.projFile.GetConditionedPropertyValues("Configuration");
            foreach(string config in configurations)
            {
                if (String.Compare(name, config, true, CultureInfo.InvariantCulture) == 0)
                {
                    cfg = new ProjectConfig(this, config);
                    break;
                }
            }

            return cfg == null ? NativeMethods.E_INVALIDARG : NativeMethods.S_OK;
        }
        #endregion

        #region IPersist
        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetClassID"]/*' />
        int IPersist.GetClassID(out Guid clsid) {
            clsid = this.GetProjectGuid();
            return NativeMethods.S_OK;
        }
        #endregion 

        #region IPersistFileFormat methods
        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetClassID"]/*' />
        int IPersistFileFormat.GetClassID(out Guid clsid)
        {
            clsid = this.GetProjectGuid();
            return NativeMethods.S_OK;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetCurFile"]/*' />
        public virtual int GetCurFile(out string name, out uint formatIndex)
        {
            name = this.filename;
            formatIndex = 0;
            return NativeMethods.S_OK;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetFormatList"]/*' />
        public virtual int GetFormatList(out string formatlist)
        {
            formatlist = "XML";
            return NativeMethods.S_OK;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.InitNew"]/*' />
        public virtual int InitNew(uint formatIndex)
        {
            return NativeMethods.S_OK;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.IsDirty"]/*' />
        public virtual int IsDirty(out int isDirty)
        {
            isDirty = 0;
            if (this.projFile.IsDirty || dirty)
                isDirty = 1;
            return NativeMethods.S_OK;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.Load1"]/*' />
        public virtual int Load(string filename, uint mode, int readOnly)
        {
            this.filename = filename;
            this.Reload();
            return NativeMethods.S_OK;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.Save"]/*' />
        public virtual int Save(string filename, int fremember, uint formatIndex)
        {
            int rc = NativeMethods.S_OK;
            SuspendWatchingProjectFileChanges();
            try
            {
                this.projFile.SaveToFile(filename);
                SetProjectFileDirty(false);
            }
            catch (Exception e)
            {
                string caption = SR.GetString(SR.ErrorSaving);
                RTLAwareMessageBox.Show(null, e.Message, caption, MessageBoxButtons.OK,
                    MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, 0);
            }
            ResumeWatchingProjectFileChanges();
            if (fremember != 0)
                this.filename = filename;
            return rc;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.SaveCompleted"]/*' />
        public virtual int SaveCompleted(string filename)
        {
            // TODO: turn file watcher back on.
            return NativeMethods.S_OK;
        }   
        #endregion 

        bool _suspended;

        IVsDocDataFileChangeControl _ddfcc;

        void SuspendWatchingProjectFileChanges()
        {
            if (_suspended || this.Site == null) return;

            IVsRunningDocumentTable pRDT = this.Site.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;

            if (pRDT == null)
                return;

            IVsHierarchy ppHier;
            uint pid;
            IntPtr docData;
            uint docCookie;

            NativeMethods.ThrowOnFailure(pRDT.FindAndLockDocument((uint)_VSRDTFLAGS.RDT_NoLock, filename, out ppHier, out pid, out docData, out docCookie));
            if (docCookie == 0 || docData == IntPtr.Zero)
                return;

            IVsFileChangeEx fce = this.Site.GetService(typeof(SVsFileChangeEx)) as IVsFileChangeEx;

            if (fce != null)
            {
                _suspended = true;
                NativeMethods.ThrowOnFailure(fce.IgnoreFile(0, filename, 1));
                try
                {
                    _ddfcc = Marshal.GetTypedObjectForIUnknown(docData, typeof(IVsDocDataFileChangeControl)) as IVsDocDataFileChangeControl;
                    if (_ddfcc != null)
                    {
                        NativeMethods.ThrowOnFailure(_ddfcc.IgnoreFileChanges(1));
                    }
                }
                catch (Exception) {}
            }
            // bugbug: do I need to "release" the docData IUnknown pointer
            // or does interop take care of it?
        }

        void ResumeWatchingProjectFileChanges()
        {
            if (!_suspended)
                return;

            IVsFileChangeEx fce = this.Site.GetService(typeof(SVsFileChangeEx)) as IVsFileChangeEx;

            if (fce != null)
            {
                NativeMethods.ThrowOnFailure(fce.IgnoreFile(0, filename, 0));
            }

            if (_ddfcc != null)
            {
                NativeMethods.ThrowOnFailure(_ddfcc.IgnoreFileChanges(0));
                _ddfcc = null;
            }

            _suspended = false;
        }

        //===================================================================
        // HierarchyNode overrides
        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.Url"]/*' />
        public override string Url
        {
            get {return filename;}
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.Close"]/*' />
        public override int Close()
        {
            Project.ProjectList.Remove(this.automation);
            this.automation = null;
            this.ClearLibraryReferences();
            NativeMethods.ThrowOnFailure(base.Close());
            if (this.taskProvider != null)
            {
                this.taskProvider.Dispose();
                this.taskProvider = null;
            }

            this.Site = null;
            this.UIShell = null;
            return NativeMethods.S_OK;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.SetSite"]/*' />
        public override int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider site)
        {
            CCITracing.TraceCall();
            this.Site = new ServiceProvider(site);

            this.UIShell = this.Site.GetService(typeof(SVsUIShell)) as IVsUIShell;
            // if we are a new project open up default file
            if (this.fNewProject)
            {
                HierarchyNode child = this.FirstChild; // that should be the reference folder....
                IVsUIHierarchyWindow uiWindow = this.GetIVsUIHierarchyWindow(HierarchyNode.Guid_SolutionExplorer);

                this.fNewProject = false;
                while (child != null && child.NodeType != HierarchyNodeType.File)
                {
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
            LoadLibrary();
            return NativeMethods.S_OK;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetProperty"]/*' />
        public override object GetProperty(int propId)
        { // __VSHPROPID_HandlesOwnReload during open?
            __VSHPROPID id = (__VSHPROPID)propId;

            switch (id)
            {
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

            }//__VSHPROPID_DefaultEnableBuildProjectCfg, __VSHPROPID_CanBuildFromMemory, __VSHPROPID_DefaultEnableDeployProjectCfg
            return base.GetProperty(propId);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetGuidProperty"]/*' />
        public override int GetGuidProperty(int propid, out Guid guid)
        {
            guid = Guid.Empty;
            switch ((__VSHPROPID)propid)
            {
                case __VSHPROPID.VSHPROPID_ProjectIDGuid:
                    guid = ProjectIDGuid;
                    break;

                case __VSHPROPID.VSHPROPID_CmdUIGuid:
                    guid = VsMenus.guidStandardCommandSet2K;
                    break;
            }  //-2054, PreferredLanguageSID?
            CCITracing.TraceCall(String.Format("Property {0} not found", propid));
            if (guid.CompareTo(Guid.Empty) == 0)
                return NativeMethods.DISP_E_MEMBERNOTFOUND;
            return NativeMethods.S_OK;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.SetGuidProperty"]/*' />
        public override int SetGuidProperty(int propid, ref Guid guid)
        {
            switch ((__VSHPROPID)propid)
            {
                case __VSHPROPID.VSHPROPID_ProjectIDGuid:
                    ProjectIDGuid = guid;
                    return NativeMethods.S_OK;
            }
            CCITracing.TraceCall(String.Format("Property {0} not found", propid));
            return NativeMethods.DISP_E_MEMBERNOTFOUND;
        }

        string MakeRelative(string filename, string filename2)
        {
            string[] parts = filename.Split(Path.DirectorySeparatorChar);
            string[] parts2 = filename2.Split(Path.DirectorySeparatorChar);

            if (parts.Length == 0 || parts2.Length == 0 || parts[0] != parts2[0])
            {
                return filename2; // completely different paths.
            }

            int i;

            for (i = 1; i < parts.Length && i < parts2.Length; i++)
            {
                if (parts[i] != parts2[i]) break;
            }

            StringBuilder sb = new StringBuilder();

            for (int j = i; j < parts.Length - 1; j++)
            {
                sb.Append("..");
                sb.Append(Path.DirectorySeparatorChar);
            }

            for (int j = i; j < parts2.Length; j++)
            {
                sb.Append(parts2[j]);
                if (j < parts2.Length - 1)
                    sb.Append(Path.DirectorySeparatorChar);
            }

            return sb.ToString();
        }

        internal bool IsReadOnly
        {
            get
            {
                return (File.GetAttributes(filename) & FileAttributes.ReadOnly) != 0;
            }
        }

        internal HierarchyNode AddExistingFile(string file)
        {
            ProjectElement item = AddFileNode(file);
            HierarchyNode child = this.CreateNode(this, HierarchyNodeType.File, item);

            return child;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //
        //  removes items from the hierarchy. Project overwrites this
        //
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.Remove"]/*' />
        public override void Remove(bool removeFromStorage)
        {
            // the project will not be deleted from disk, just removed      
            if (removeFromStorage)
                return;

            // Remove the entire project from the solution
            IVsSolution solution = this.Site.GetService(typeof(SVsSolution)) as IVsSolution;
            uint iOption = 1; // SLNSAVEOPT_PromptSave
            NativeMethods.ThrowOnFailure(solution.CloseSolutionElement(iOption, this, 0));
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////////
        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.GetMkDocument"]/*' />
        /// <summary>
        /// allback from the additem dialog. Deals with adding new and existing items
        /// </summary>
        #region IVsProject3 methods

        public virtual int GetMkDocument(uint itemId, out string mkDoc)
        {
            HierarchyNode n = this.projectMgr.NodeFromItemId(itemId);
            mkDoc = (n != null) ? n.Url : null;
            return NativeMethods.S_OK;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.IVsProject3.AddItem"]/*' />
        public virtual int AddItem(uint itemIdLoc, VSADDITEMOPERATION op, string itemName, uint filesToOpen, string[] files, IntPtr dlgOwner, VSADDRESULT[] result)
        {
            Guid empty = Guid.Empty;

            return AddItemWithSpecific(itemIdLoc, op, itemName, filesToOpen, files, dlgOwner, 0, ref empty, null, ref empty, result);
        }
        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.IVsProject3.AddItemWithSpecific"]/*' />
        public virtual int AddItemWithSpecific(uint itemIdLoc, VSADDITEMOPERATION op, string itemName, uint filesToOpen, string[] files, IntPtr dlgOwner, uint editorFlags, ref Guid editorType, string physicalView, ref Guid logicalView, VSADDRESULT[] result)
        {
            CCITracing.TraceCall();
            result[0] = VSADDRESULT.ADDRESULT_Failure;
            //if (this.IsReadOnly) return; <-- bogus, the user can always change the project to non-readonly later when they choose to save it, or they can do saveas...
            HierarchyNode n = NodeFromItemId(itemIdLoc);
            if (n == null) return NativeMethods.E_INVALIDARG;

            string[] actualFiles = files;
            if (n.NodeType == HierarchyNodeType.Root || n.NodeType == HierarchyNodeType.Folder)
            {
                if (!this.projectMgr.Tracker.CanAddFiles(files))
                {
                    return NativeMethods.E_FAIL;
                }

                int index = 0;
                foreach (string file in files)
                {
                    HierarchyNode child;
                    bool fFileAdded = false;
                    bool fOverwrite = false;
                    string strBaseDir = Path.GetDirectoryName(this.filename);
                    string strNewFileName = "";

                    if (n.NodeType == HierarchyNodeType.Folder)
                    {
                        // add the folder to the path....
                        strBaseDir = n.Url;
                    }

                    switch (op)
                    {
                        case VSADDITEMOPERATION.VSADDITEMOP_CLONEFILE:
                            // new item added. Need to copy template to new location and then add new location 
                            strNewFileName = Path.Combine(strBaseDir, itemName);
                            break;

                        case VSADDITEMOPERATION.VSADDITEMOP_LINKTOFILE:// TODO: VSADDITEMOP_LINKTOFILE
                            // we do not support this right now
                            throw new NotImplementedException("VSADDITEMOP_LINKTOFILE");

                        case VSADDITEMOPERATION.VSADDITEMOP_OPENFILE: {
                                string strFileName = Path.GetFileName(file);
                                strNewFileName = Path.Combine(strBaseDir, strFileName);
                            }
                            break;

                        case VSADDITEMOPERATION.VSADDITEMOP_RUNWIZARD: // TODO: VSADDITEMOP_RUNWIZARD
                            throw new NotImplementedException("VSADDITEMOP_RUNWIZARD");
                    }
                    child = this.FindChild(strNewFileName);
                    if (child != null)
                    {
                        // file already exists in project... message box
                        actualFiles[index] = strNewFileName;
                        string msg = SR.GetString(SR.FileAlreadyInProject);
                        string caption = SR.GetString(SR.FileAlreadyInProjectCaption);

                        if (RTLAwareMessageBox.Show(null, msg, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1, 0) == DialogResult.Yes)
                        {
                            child = null;
                            fOverwrite = true;
                        }
                    }

                    if (child == null)
                    {
                        // the next will be equal if file is already in the project DIR
                        if (NativeMethods.IsSamePath(file, strNewFileName) == false)
                        {
                            if (!fOverwrite && File.Exists(strNewFileName))
                            {
                                string msg = String.Format(SR.GetString(SR.FileAlreadyExists), strNewFileName);
                                string caption = SR.GetString(SR.FileAlreadyExistsCaption);
                                if (RTLAwareMessageBox.Show(null, msg, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1, 0) != DialogResult.Yes)
                                {
                                    return NativeMethods.S_OK;
                                }
                            }

                            try {
                                this.CopyUrlToLocal(file, strNewFileName, op == VSADDITEMOPERATION.VSADDITEMOP_CLONEFILE);
                                actualFiles[index] = strNewFileName;
                            }
                            catch (Exception e)
                            {
                                string caption = SR.GetString(SR.FileCopyError);
                                RTLAwareMessageBox.Show(null, e.Message, caption, MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, 0);
                                return NativeMethods.S_OK;
                            }
                        }

                        fFileAdded = true;
                    }

                    if (fFileAdded && !fOverwrite)
                    {
                        // now add the new thing to the project
                        child = this.projectMgr.AddExistingFile(strNewFileName);
                        n.AddChild(child);
                        this.projectMgr.Tracker.OnAddFile(strNewFileName);
                    }
                }

                n.OnItemsAppended(n);
                result[0] = VSADDRESULT.ADDRESULT_Success;
            }
            for (int i = 0; i < filesToOpen; i++)
            {
                string name = actualFiles[i];
                HierarchyNode child = this.FindChild(name);
                Debug.Assert(child != null, "We should have been able to find the new element in the hierarchy");
                if (child != null)
                {
                    IVsWindowFrame frame;
                    if (editorType == Guid.Empty)
                    {
                        Guid view = Guid.Empty;
                        NativeMethods.ThrowOnFailure(this.OpenItem(child.ID, ref view, IntPtr.Zero, out frame));
                    }
                    else
                    {
                        NativeMethods.ThrowOnFailure(this.OpenItemWithSpecific(child.ID, editorFlags, ref editorType, physicalView, ref logicalView, IntPtr.Zero, out frame));
                    }
                }
            }
            result[0] = VSADDRESULT.ADDRESULT_Success;
            return NativeMethods.S_OK;
        }
        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.CopyUrlToLocal"]/*' />
        /// <devdoc>
        /// Copy the specified file to the local project directory.  Also supports downloading
        /// of HTTP resources (so be prepared for a delay in that case!).
        /// </devdoc>
        public void CopyUrlToLocal(string file, string local, bool fromTemplate)
        {
            Uri uri = new Uri(file);
            if (uri.IsFile)
            {
                // now copy file
                if (fromTemplate)
                    AddFileFromTemplate(file, local);
                else
                {
                    FileInfo fiOrg = new FileInfo(file);
                    FileInfo fiNew = fiOrg.CopyTo(local, true);
                }
            }
            else
            {
                FileStream localFile = new FileStream(local, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                try {
                    WebRequest wr = WebRequest.Create(uri);
                    wr.Timeout = 10000;
                    wr.Credentials = CredentialCache.DefaultCredentials;
                    WebResponse resp = wr.GetResponse();
                    Stream s = resp.GetResponseStream();
                    byte[] buffer = new byte[16000];
                    int len;
                    while ((len = s.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        localFile.Write(buffer, 0, len);
                    }
                }
                finally
                {
                    localFile.Close();
                }
            }
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.IVsProject3.GenerateUniqueItemName"]/*' />
        /// <summary>
        /// for now used by add folder. Called on the ROOT, as only the project should need
        /// to implement this.
        /// for folders, called with parent folder, blank extension and blank suggested root
        /// </summary>
        public virtual int GenerateUniqueItemName(uint itemIdLoc, string ext, string suggestedRoot, out string itemName)
        {
            string rootName = "";
            string extToUse;
            int cb = 0;
            bool found = false;
            bool fFolderCase = false;
            HierarchyNode parent = this.projectMgr.NodeFromItemId(itemIdLoc);

            extToUse = ext.Trim();
            suggestedRoot = suggestedRoot.Trim();
            if (suggestedRoot.Length == 0)
            {
                // foldercase, we assume... 
                suggestedRoot = "NewFolder";
                fFolderCase = true;
            }

            while (!found)
            {
                rootName = suggestedRoot;
                if (cb > 0)
                    rootName += cb.ToString();

                if (extToUse.Length > 0)
                {
                    rootName += extToUse;
                }

                cb++;
                found = true;
                for (HierarchyNode n = parent.FirstChild; n != null; n = n.NextSibling)
                {
                    if (rootName == n.GetEditLabel())
                    {
                        found = false;
                        break;
                    }

                    string checkFile = Path.Combine(Path.GetDirectoryName(parent.Url), rootName);

                    if (fFolderCase)
                    {
                        if (Directory.Exists(checkFile))
                        {
                            found = false;
                            break;
                        }
                    }
                    else
                    {
                        if (File.Exists(checkFile))
                        {
                            found = false;
                            break;
                        }
                    }
                }
            }

            itemName = rootName;
            return NativeMethods.S_OK;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetItemContext"]/*' />
        public virtual int GetItemContext(uint itemId, out Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp)
        {
            CCITracing.TraceCall();
            psp = null; 
            HierarchyNode child = this.projectMgr.NodeFromItemId(itemId);
            if (child != null)
            {
                NativeMethods.ThrowOnFailure(child.GetSite(out psp));
            }
        return NativeMethods.S_OK;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.IsDocumentInProject"]/*' />
        public virtual int IsDocumentInProject(string mkDoc, out int pfFound, VSDOCUMENTPRIORITY[] pri, out uint itemId)
        {
            CCITracing.TraceCall();
            if (pri != null && pri.Length >= 1)
            {
                pri[0] = VSDOCUMENTPRIORITY.DP_Unsupported;
            }
            pfFound = 0;
            itemId = 0;
            HierarchyNode child = this.FindChild(mkDoc);
            if (child != null)
            {
                pfFound = 1;
                if (pri != null && pri.Length >= 1)
                {
                    pri[0] = VSDOCUMENTPRIORITY.DP_Standard;
                }
                itemId = child.ID;
            }
            return NativeMethods.S_OK;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.OpenItem"]/*' />
        public virtual int OpenItem(uint itemId, ref Guid logicalView, IntPtr punkDocDataExisting, out IVsWindowFrame ppWindowFrame)
        {
            CCITracing.TraceCall();
            HierarchyNode n = this.projectMgr.NodeFromItemId(itemId);
            if (n == null)
            {
                throw new ArgumentException("Unknown itemid");
            }
            n.OpenItem(false, false, ref logicalView, punkDocDataExisting, out ppWindowFrame);

            return NativeMethods.S_OK;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.OpenItemWithSpecific"]/*' />
        public virtual int OpenItemWithSpecific(uint itemId, uint editorFlags, ref Guid editorType, string physicalView, ref Guid logicalView, IntPtr docDataExisting, out IVsWindowFrame frame)
        {
            CCITracing.TraceCall();
            HierarchyNode n = this.projectMgr.NodeFromItemId(itemId);
            if (n == null)
            {
                throw new ArgumentException("Unknown itemid");
            }
            n.OpenItemWithSpecific(editorFlags, ref editorType, physicalView, ref logicalView, docDataExisting, out frame);
            return NativeMethods.S_OK;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.RemoveItem"]/*' />
        public virtual int RemoveItem(uint reserved, uint itemId, out int result)
        {
            CCITracing.TraceCall();
            HierarchyNode n = this.projectMgr.NodeFromItemId(itemId);
            if (n == null)
            {
                throw new ArgumentException("Unknown itemid");
            }
            n.Remove(true);
            result = 1;
            return NativeMethods.S_OK;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.ReopenItem"]/*' />
        public virtual int ReopenItem(uint itemId, ref Guid editorType, string physicalView, ref Guid logicalView, IntPtr docDataExisting, out IVsWindowFrame frame)
        {
            CCITracing.TraceCall();
            HierarchyNode n = this.projectMgr.NodeFromItemId(itemId);
            if (n == null)
            {
                throw new ArgumentException("Unknown itemid");
            }
            n.OpenItemWithSpecific(0, ref editorType, physicalView, ref logicalView, docDataExisting, out frame);
            return NativeMethods.S_OK;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.TransferItem"]/*' />
        public virtual int TransferItem(string oldMkDoc, string newMkDoc, IVsWindowFrame frame)
        {
            CCITracing.TraceCall(); 
            return NativeMethods.E_NOTIMPL;
        }
   
        #endregion

        string GetReferencedLibraryName(ProjectElement assemblyRefNode)
        {
            string assembly = assemblyRefNode.GetAttribute("AssemblyName");

            if (!assembly.ToLower().EndsWith(".dll"))
                assembly += ".dll";

            string hint = assemblyRefNode.GetAttribute("HintPath");

            if (hint != "" && hint != null)
            {
                Url url = new Url(new Url(this.ProjectFolder + "/"), hint);
                assembly = url.AbsoluteUrl;

                if (!File.Exists(assembly))
                {
                    url = new Url(new Url(GetSystemAssemblyPath() + "/"), hint);
                    assembly = url.AbsoluteUrl;
                }
            }

            if (!File.Exists(assembly))
            {
                assembly = this.GetFullyQualifiedNameForReferencedLibrary(this.GetProjectOptions(null), assembly);
            }

            return assembly;
        }
        
        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.guidCOMPLUSLibrary"]/*' />
        public static Guid guidCOMPLUSLibrary = new Guid(0x1ec72fd7, 0xc820, 0x4273, 0x9a, 0x21, 0x77, 0x7a, 0x5c, 0x52, 0x2e, 0x03);
        
        // Object Browser
        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.ShowObjectBrowser"]/*' />
        public int ShowObjectBrowser(ProjectElement assemblyRefNode)
        {
            string assembly = GetReferencedLibraryName(assemblyRefNode);
            IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(guidCOMPLUSLibrary.ToByteArray().Length);

            System.Runtime.InteropServices.Marshal.StructureToPtr(guidCOMPLUSLibrary, ptr, false);
            try
            {
                this.AddLibraryReference(assembly); // make sure it's in the library.

                VSOBJECTINFO[] objInfo = new VSOBJECTINFO[1];

                objInfo[0].pguidLib = ptr;
                objInfo[0].pszLibName = assembly;

                IVsObjBrowser objBrowser = this.projectMgr.Site.GetService(typeof(SVsObjBrowser)) as IVsObjBrowser;

                NativeMethods.ThrowOnFailure(objBrowser.NavigateTo(objInfo, 0));
            }
            catch (Exception e)
            {
                CCITracing.TraceCall(e.Message);
            }
            System.Runtime.InteropServices.Marshal.FreeCoTaskMem(ptr);
            return 0;
        }

        Hashtable libraryList = new Hashtable();

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.AddLibraryReference"]/*' />
        public void AddLibraryReference(string assembly)
        {
            try
            {
                string key = assembly.ToLower();

                if (!libraryList.Contains(key))
                {
                    IVsLibraryReferenceManager vlrm = this.projectMgr.Site.GetService(typeof(IVsLibraryReferenceManager)) as IVsLibraryReferenceManager;
                    IVsLibrary library = (IVsLibrary)vlrm;

                    NativeMethods.ThrowOnFailure(vlrm.AddComponentReference(key, library));
                    libraryList[key] = assembly;
                }
            }
            catch (Exception e)
            {
                CCITracing.TraceCall(e.Message);
            }
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.RemoveLibraryReference"]/*' />
        public void RemoveLibraryReference(string assembly)
        {
            try
            {
                if (assembly == null) return; // error case.

                string key = assembly.ToLower();

                if (libraryList.Contains(key))
                {
                    IVsLibraryReferenceManager vlrm = this.projectMgr.Site.GetService(typeof(IVsLibraryReferenceManager)) as IVsLibraryReferenceManager;
                    IVsLibrary library = (IVsLibrary)vlrm;

                    NativeMethods.ThrowOnFailure(vlrm.RemoveComponentReference(key, library));
                    libraryList.Remove(key);
                }
            }
            catch (Exception e)
            {
                CCITracing.TraceCall(e.Message);
            }
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.ClearLibraryReferences"]/*' />
        public void ClearLibraryReferences()
        {
            Hashtable libraryList2 = (Hashtable)libraryList.Clone();

            foreach (string key in libraryList2.Keys)
            {
                RemoveLibraryReference(key);
            }
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.OnRemoveReferenceNode"]/*' />
        public void OnRemoveReferenceNode(HierarchyNode node)
        {
            RemoveLibraryReference(this.GetReferencedLibraryName(node.ItemNode));
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.OnAddReferenceNode"]/*' />
        public void OnAddReferenceNode(HierarchyNode node)
        {
            AddLibraryReference(GetReferencedLibraryName(node.ItemNode));
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="Project.LoadLibrary"]/*' />
        public void LoadLibrary()
        {
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

        /// <summary>
        /// For internal use only.
        /// This creates a copy of an existing configuration and add it to the project.
        /// Caller should change the condition on the PropertyGroup.
        /// If derived class want to accomplish this, they should call ConfigProvider.AddCfgsOfCfgName()
        /// It is expected that in the future MSBuild will have support for this so we don't have to
        /// do it manually.
        /// </summary>
        /// <param name="group">PropertyGroup to clone</param>
        /// <returns></returns>
        internal MSBuild.PropertyGroup ClonePropertyGroup(MSBuild.PropertyGroup group)
        {
            // Create a new (empty) PropertyGroup
            MSBuild.PropertyGroup newPropertyGroup = this.projFile.AddNewPropertyGroup(true);

            // Now copy everything from the group we are trying to clone to the group we are creating
            if (group.Condition != "")
                newPropertyGroup.Condition = group.Condition;
            foreach (MSBuild.Property prop in group)
            {
                MSBuild.Property newProperty = newPropertyGroup.AddNewProperty(prop.Name, prop.Value);
                if (prop.Condition != "")
                    newProperty.Condition = prop.Condition;
            }

            return newPropertyGroup;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetProjectProperty"]/*' />
        public string GetProjectProperty(string propertyName)
        {
            return this.GetProjectProperty(propertyName, true);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.GetProjectProperty1"]/*' />
        /// <summary>
        /// Return the value of a project property
        /// </summary>
        /// <param name="propertyName">Name of the property to get</param>
        /// <param name="resetCache">True to avoid using the cache</param>
        /// <returns>null if property does not exist, otherwise value of the property</returns>
        public virtual string GetProjectProperty(string propertyName, bool resetCache)
        {
            MSBuild.Property property = GetMsBuildProperty(propertyName, resetCache);
            if (property == null)
                return null;

            return property.Value;
        }

        private MSBuild.Property GetMsBuildProperty(string propertyName, bool resetCache)
        {
            if (resetCache || this.currentConfig == null)
            {
                // Get properties from project file and cache it
                // TODO: track the current active configuration and set that value before evaluating properties
                //       in theory, this should only be called for global property.
                //this.ProjectMgr.projFile.GlobalProperties.SetProperty("Configuration", this.ConfigName);
                this.currentConfig = this.ProjectMgr.projFile.EvaluatedProperties;
            }

            if (this.currentConfig == null)
                throw new Exception("Failed to retrive properties");

            // return property asked for
            return this.currentConfig[propertyName];
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ImageNames.SetProjectProperty"]/*' />
        public virtual void SetProjectProperty(string propertyName, string propertyValue)
        {
            if (propertyName == null)
                throw new ArgumentNullException("propertyName", "Cannot set a null project property");

            if (propertyValue == null)
            {
                // if property already null, do nothing
                if (GetMsBuildProperty(propertyName, true) == null)
                    return;
                // otherwise, set it to empty
                propertyValue = String.Empty;
            }
            this.projFile.SetProperty(propertyName, propertyValue, null);

            // property cache will need to be updated
            this.currentConfig = null;
            this.SetProjectFileDirty(true);

            return;
        }

    } // end of class

    /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions"]/*' />
    public class ProjectOptions : System.CodeDom.Compiler.CompilerParameters
    {
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.ModuleKind"]/*' />
        public ModuleKindFlags ModuleKind = ModuleKindFlags.ConsoleApplication;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.EmitManifest"]/*' />
        public bool EmitManifest = true;
        //public bool IsModule; // difference between .dll and .netmodule.
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.DefinedPreProcessorSymbols;"]/*' />
        public StringCollection DefinedPreProcessorSymbols;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.XMLDocFileName;"]/*' />
        public string XMLDocFileName;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.RecursiveWildcard;"]/*' />
        public string RecursiveWildcard;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.ReferencedModules;"]/*' />
        public StringCollection ReferencedModules;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.Win32Icon;"]/*' />
        public string Win32Icon;
#if !WHIDBEY
    /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.EmbeddedResources"]/*' />
    public StringCollection EmbeddedResources = new StringCollection();
    /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.LinkedResources"]/*' />
    public StringCollection LinkedResources = new StringCollection();
#endif 
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.PDBOnly;"]/*' />
        public bool PDBOnly;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.Optimize;"]/*' />
        public bool Optimize;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.IncrementalCompile;"]/*' />
        public bool IncrementalCompile;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.SuppressedWarnings;"]/*' />
        public int[] SuppressedWarnings;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.CheckedArithmetic;"]/*' />
        public bool CheckedArithmetic;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.AllowUnsafeCode;"]/*' />
        public bool AllowUnsafeCode;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.DisplayCommandLineHelp;"]/*' />
        public bool DisplayCommandLineHelp;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.SuppressLogo;"]/*' />
        public bool SuppressLogo;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.BaseAddress;"]/*' />
        public long BaseAddress;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.BugReportFileName;"]/*' />
        public string BugReportFileName;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.CodePage;"]/*' />
        public object CodePage; //must be an int if not null
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.EncodeOutputInUTF8;"]/*' />
        public bool EncodeOutputInUTF8;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.FullyQualifiyPaths;"]/*' />
        public bool FullyQualifiyPaths;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.FileAlignment;"]/*' />
        public int FileAlignment;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.NoStandardLibrary;"]/*' />
        public bool NoStandardLibrary;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.AdditionalSearchPaths;"]/*' />
        public StringCollection AdditionalSearchPaths;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.HeuristicReferenceResolution;"]/*' />
        public bool HeuristicReferenceResolution;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.RootNamespace;"]/*' />
        public string RootNamespace;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.CompileAndExecute;"]/*' />
        public bool CompileAndExecute;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.UserLocaleId;"]/*' />
        public object UserLocaleId; //must be an int if not null
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.TargetPlatform;"]/*' />
        public PlatformType TargetPlatform;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.TargetPlatformLocation;"]/*' />
        public string TargetPlatformLocation;
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ProjectOptions.GetOptionHelp"]/*' />
        public virtual string GetOptionHelp()
        {
            return null;
        }
    }

    //===========================================================================
    // This custom writer puts attributes on a new line which makes the 
    // .xsproj files easier to read.
    internal class MyXmlWriter : XmlTextWriter
    {
        int depth;

        TextWriter tw;

        string indent;

        public MyXmlWriter(TextWriter tw) : base(tw)
        {
            this.tw = tw;
            this.Formatting = Formatting.Indented;
        }

        string IndentString
        {
            get
            {
                if (this.indent == null)
                {
                    StringBuilder sb = new StringBuilder();
                    for (int j = 0; j < this.Indentation; j++)
                    {
                        sb.Append(this.IndentChar);
                    }
                    this.indent = sb.ToString();
                }
                return this.indent;
            }
        }

        public override void WriteEndAttribute()
        {
            base.WriteEndAttribute();
            this.tw.WriteLine();
            for (int i = 0; i < depth; i++)
            {
                this.tw.Write(this.IndentString);
            }
        }

        public override void WriteStartElement(string prefix, string localName, string ns)
        {
            base.WriteStartElement(prefix, localName, ns);
            this.depth++;
        }

        public override void WriteEndElement()
        {
            base.WriteEndElement();
            this.depth--;
        }
    }

    /// <include file='doc\Project.uex' path='docs/doc[@for="ProjectElement"]/*' />
    /// <summary>
    /// This class represent a project item (usualy a file) and allow getting and
    /// setting attribute on it.
    /// This class allow us to keep the internal details of our items hidden from
    /// our derived classes.
    /// While the class itself is public so it can be manipulated by derived classes,
    /// its internal constructors make sure it can only be created from within the assembly.
    /// </summary>
    public sealed class ProjectElement
    {
        private MSBuild.Item item;
        private Project itemProject;
        private bool deleted = false;
        private bool isVirtual = false;

        /// <summary>
        /// Constructor to create a new MSBuild.Item and add it to the project
        /// Only have internal constructors as the only one who should be creating
        /// such object is the project itself (see Project.CreateFileNode()).
        /// </summary>
        internal ProjectElement(Project project, string itemPath, string itemType)
        {
            if (project == null)
                throw new ArgumentNullException("project", String.Format(SR.GetString(SR.AddToNullProjectError), itemPath));

            itemProject = project;

            // create and add the item to the project
            item = project.projFile.AddNewItem(itemType, itemPath);
            project.SetProjectFileDirty(true);
            this.RefreshProperties();
        }

        /// <summary>
        /// Constructor to Wrap an existing MSBuild.Item
        /// Only have internal constructors as the only one who should be creating
        /// such object is the project itself (see Project.CreateFileNode()).
        /// </summary>
        /// <param name="project">Project that owns this item</param>
        /// <param name="existingItem">an MSBuild.Item; can be null if virtualFolder is true</param>
        /// <param name="virtualFolder">Is this item virtual (such as reference folder)</param>
        internal ProjectElement(Project project, MSBuild.Item existingItem, bool virtualFolder)
        {
            if (project == null)
                throw new ArgumentNullException("project", String.Format(SR.GetString(SR.AddToNullProjectError), existingItem.Include));
            if (!virtualFolder && existingItem == null)
                throw new ArgumentNullException("existingItem");

            // Keep a reference to project and item
            itemProject = project;
            item = existingItem;
            isVirtual = virtualFolder;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ProjectElement.SetAttribute"]/*' />
        /// <summary>
        /// Set an attribute on the project element
        /// </summary>
        /// <param name="attributeName">Name of the attribute to set</param>
        /// <param name="attributeValue">Value to give to the attribute</param>
        public void SetAttribute(string attributeName, string attributeValue)
        {
            ThrowIfDeleted();

            Debug.Assert(String.Compare(attributeName, "Include", true, CultureInfo.InvariantCulture) != 0, "Use rename as this won't work");

            // Build Action is the type, not a property, so intercept
            if (String.Compare(attributeName, "BuildAction", true, CultureInfo.InvariantCulture) == 0)
            {
                item.Type = attributeValue;
                return;
            }

            item.SetAttribute(attributeName, attributeValue);
            itemProject.SetProjectFileDirty(true);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ProjectElement.GetAttribute"]/*' />
        /// <summary>
        /// Get the value of an attribute on a project element
        /// </summary>
        /// <param name="attributeName">Name of the attribute to get the value for</param>
        /// <returns>Value of the attribute</returns>
        public string GetAttribute(string attributeName)
        {
            ThrowIfDeleted();

            // cannot ask MSBuild for Include, so intercept it and return the corresponding property
            if (String.Compare(attributeName, "Include", true, CultureInfo.InvariantCulture) == 0)
                return item.FinalItemSpec;

            // Build Action is the type, not a property, so intercept this one as well
            if (String.Compare(attributeName, "BuildAction", true, CultureInfo.InvariantCulture) == 0)
                return item.Type;

            return item.GetAttribute(attributeName);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ProjectElement.Rename"]/*' />
        public void Rename(string newPath)
        {
            item.Include = newPath;
            this.RefreshProperties();
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ProjectElement.RefreshProperties"]/*' />
        /// <summary>
        /// Reevaluate all properties for the current item
        /// This should be call if you believe the property for this item
        /// may have changed since it was created/refreshed, or global properties
        /// this items depends on have changed.
        /// Be aware that there is a perf cost in calling this function.
        /// </summary>
        public void RefreshProperties()
        {
            ThrowIfDeleted();
            MSBuild.ItemGroup items = itemProject.projFile.EvaluatedItems;
            foreach (MSBuild.Item projectItem in items)
            {
                if (projectItem.Include == item.Include)
                {
                    item = projectItem;
                    return;
                }
            }
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ProjectElement.operatorEQ"]/*' />
        public static bool operator ==(ProjectElement element1, ProjectElement element2)
        {
            element1.ThrowIfDeleted();
            element2.ThrowIfDeleted();
            // Do they reference the same element?
            if (Object.ReferenceEquals(element1, element2))
                return true;

            // Do they reference the same project?
            if (!element1.itemProject.Equals(element2.itemProject))
                return false;

            // Do both item have the same full path?
            if (String.Compare(element1.GetAttribute("Include"), element2.GetAttribute("Include"), true, CultureInfo.CurrentUICulture) == 0)
                return true;

            // If we got here, they are different
            return false;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ProjectElement.operatorNE"]/*' />
        public static bool operator !=(ProjectElement element1, ProjectElement element2)
        {
            return !(element1 == element2);
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ProjectElement.Equals"]/*' />
        public override bool Equals(object obj)
        {
            ProjectElement element2 = obj as ProjectElement;
            if (element2 == null)
                return false;

            return this == element2;
        }

        /// <include file='doc\Project.uex' path='docs/doc[@for="ProjectElement.GetHashCode"]/*' />
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }


        /// <include file='doc\Project.uex' path='docs/doc[@for="ProjectElement.RemoveFromProjectFile"]/*' />
        /// <summary>
        /// Calling this method remove this item from the project file.
        /// Once the item is delete, you should not longer be using it.
        /// Note that the item should be removed from the hierarchy prior to this call.
        /// </summary>
        public void RemoveFromProjectFile()
        {
            ThrowIfDeleted();
            deleted = true;
            itemProject.projFile.RemoveItem(item);
            itemProject = null;
            item = null;
        }

        /// <summary>
        /// Make sure we are not trying to use a deleted item
        /// </summary>
        private void ThrowIfDeleted()
        {
            if (deleted || isVirtual)
                throw new Exception(String.Format(SR.GetString(SR.UseOfDeletedItemError)));
        }

    }
}
