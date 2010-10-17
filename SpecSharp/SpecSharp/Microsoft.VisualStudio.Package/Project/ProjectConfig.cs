//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Xml;
using System.Threading;
using System.IO;

namespace Microsoft.VisualStudio.Package{
    /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig"]/*' />
    /// <summary>
    /// project config class holds project specific configuration data. It NEEDS to supply ISpecifyPropertyPages
    /// if you want to be able to show config related data in the property pane.
    /// </summary>
    [CLSCompliant(false)]
    public class ProjectConfig : IVsCfg, IVsProjectCfg, IVsProjectCfg2, IVsDebuggableProjectCfg, ISpecifyPropertyPages{
        private Project project;
        private XmlElement node;
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.ProjectConfig"]/*' />
        public ProjectConfig(Project project, XmlElement node){
            this.project = project;
            this.node = node;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.ProjectMgr"]/*' />
        public Project ProjectMgr{
            get{
                return this.project;
            }
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.Node"]/*' />
        public XmlElement Node{
            get{
                return this.node;
            }
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.GetPropertyPageGuids"]/*' />
      public Guid[] GetPropertyPageGuids(){
        return this.project.GetConfigPropertyPageGuids(this.node);
      }

      void ISpecifyPropertyPages.GetPages(CAUUID[] ppages){
        ppages[0] = new CAUUID();
        Guid[] guids = GetPropertyPageGuids();        
        ppages[0].cElems = (uint)guids.Length;
        int size = Marshal.SizeOf(typeof(Guid));
        ppages[0].pElems = Marshal.AllocCoTaskMem(guids.Length*size);
        IntPtr ptr = ppages[0].pElems;
        for (int i = 0; i < guids.Length; i++){      
          Marshal.StructureToPtr(guids[i], ptr, false);
          ptr = new IntPtr(ptr.ToInt64() + size);
        }
      }

      public void PrepareBuild(bool cleanBuild){
        project.PrepareBuild(node, cleanBuild);
      }
      public void Clean(){
        project.Clean(node);
      }
        ////////////////////////////////////////////////////
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_DisplayName"]/*' />
        /// <summary>
        /// The display name is a two part item
        /// first part is the config name, 2nd part is the platform name
        /// </summary>
        #region IVsCfg methods
        public virtual int get_DisplayName(out string name){
          string[] platform = new string[1];
          uint[] actual = new uint[1];
          name = this.node.GetAttribute("Name");
          // currently, we only support one platform, so just add it..
          ((IVsCfgProvider2)project).GetPlatformNames(1, platform, actual);
          name += "|" + platform[0];
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_IsDebugOnly"]/*' />
        public virtual int get_IsDebugOnly(out int fDebug){
          fDebug = 0;
          if (this.node.GetAttribute("Name") == "Debug"){
            fDebug = 1;
          }
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_IsReleaseOnly"]/*' />
        public virtual int get_IsReleaseOnly(out int fRelease){
          fRelease = 0;
          if (this.node.GetAttribute("Name") == "Release"){
            fRelease = 1;
          }
          return 0;
        }
        #endregion 

        #region IVsProjectCfg methods 
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.EnumOutputs"]/*' />
        public virtual int EnumOutputs(out IVsEnumOutputs eo){
          eo = null;
          return (int)HResult.E_NOTIMPL;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_BuildableProjectCfg"]/*' />
        public virtual int get_BuildableProjectCfg(out IVsBuildableProjectCfg pb){
          pb = new BuildableProjectConfig(this);
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_CanonicalName"]/*' />
        public virtual int get_CanonicalName(out string name){
          ((IVsCfg)this).get_DisplayName(out name);
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_IsPackaged"]/*' />
        public virtual int get_IsPackaged(out int pkgd){
          pkgd = 0;
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_IsSpecifyingOutputSupported"]/*' />
        public virtual int get_IsSpecifyingOutputSupported(out int f){
          f = 1;
          return 0;
        }
        ////////////////////////////////////////////////////
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_Platform"]/*' />
        /// <summary>
        /// This method is obsolete, return E_NOTIMPL
        /// </summary>
        public virtual int get_Platform(out Guid platform){
          platform = Guid.Empty;
          return (int)HResult.E_NOTIMPL;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_ProjectCfgProvider"]/*' />
        public virtual int get_ProjectCfgProvider(out IVsProjectCfgProvider p){
          p = (IVsProjectCfgProvider)project;
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_RootURL"]/*' />
        public virtual int get_RootURL(out string root){
          root = null;
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_TargetCodePage"]/*' />
        public virtual int get_TargetCodePage(out uint target){
          target = (uint)System.Text.Encoding.Default.CodePage;
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_UpdateSequenceNumber"]/*' />
        public virtual int get_UpdateSequenceNumber(ULARGE_INTEGER[] li){
          li[0] = new ULARGE_INTEGER();
          li[0].QuadPart = 0;
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.OpenOutput"]/*' />
        public virtual int OpenOutput(string name, out IVsOutput output){
          output = null;
          return (int)HResult.E_NOTIMPL;
        }
        #endregion 

        static Guid CLSID_ComPlusOnlyDebugEngine = new Guid("449EC4CC-30D2-4032-9256-EE18EB41B62B");

        #region IVsDebuggableProjectCfg methods
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.DebugLaunch"]/*' />
        public virtual int DebugLaunch(uint flags){
          try{
            VsDebugTargetInfo info = new VsDebugTargetInfo();
            info.cbSize = (uint)Marshal.SizeOf(info);
            info.dlo = Microsoft.VisualStudio.Shell.Interop.DEBUG_LAUNCH_OPERATION.DLO_CreateProcess;
            if (this.node.HasAttribute("StartProgram") && this.node.GetAttribute("StartProgram").Length > 0){
              info.bstrExe = this.node.GetAttribute("StartProgram");
            }else{
              info.bstrExe = this.project.GetOutputAssembly(node);
            }

            if (this.node.HasAttribute("WorkingDirectory") && this.node.GetAttribute("WorkingDirectory").Length > 0){
              info.bstrCurDir = this.node.GetAttribute("WorkingDirectory");
            }else{
              info.bstrCurDir = Path.GetDirectoryName(info.bstrExe);
            }

            if (this.node.HasAttribute("CmdArgs") && this.node.GetAttribute("CmdArgs").Length > 0){
              info.bstrArg = this.node.GetAttribute("CmdArgs");
            }
            if (this.node.HasAttribute("RemoteDebugMachine") && this.node.GetAttribute("RemoteDebugMachine").Length > 0){
              info.bstrRemoteMachine = this.node.GetAttribute("RemoteDebugMachine");
            }

            info.fSendStdoutToOutputWindow = 0;
            info.clsidCustom = CLSID_ComPlusOnlyDebugEngine;
            info.grfLaunch = flags;
            this.project.LaunchDebugger(info);
          }catch (Exception e){
            throw new SystemException("Could not launch debugger - " + e.Message);
          }
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.QueryDebugLaunch"]/*' />
        public virtual int QueryDebugLaunch(uint flags, out int fCanLaunch){
          string assembly = this.project.GetAssemblyName(node);
          fCanLaunch = (assembly != null && assembly.ToLower().EndsWith(".exe")) ? 1 : 0;
          if (fCanLaunch == 0){
            fCanLaunch = (this.node.HasAttribute("StartProgram") && this.node.GetAttribute("StartProgram").Length > 0) ? 1 : 0;
          }
          return 0;
        }
        #endregion 
    
        #region IVsProjectCfg2 Members

        public int OpenOutputGroup(string szCanonicalName, out IVsOutputGroup ppIVsOutputGroup) {
          if (szCanonicalName == "Built"){
            ppIVsOutputGroup = this.BuiltOutputGroup;
            return (int)HResult.S_OK;
          }
          ppIVsOutputGroup = null;
          return (int)HResult.E_NOTIMPL;
        }

        public int OutputsRequireAppRoot(out int pfRequiresAppRoot) {
          pfRequiresAppRoot = 0;
          return (int)HResult.E_NOTIMPL;
        }

        public int get_CfgType(ref Guid iidCfg, out IntPtr ppCfg) {
          ppCfg = IntPtr.Zero;
          return (int)HResult.E_NOTIMPL;
        }

        public int get_IsPrivate(out int pfPrivate) {
          pfPrivate = 0;
          return (int)HResult.E_NOTIMPL;
        }

        class Output : IVsOutput2{
          string canonicalName;
          string deploySourceURL;
          string displayName;
          string rootRelativeURL;
          
          public Output(string canonicalName, string deploySourceURL, string displayName, string rootRelativeURL){
            this.canonicalName = canonicalName;
            this.deploySourceURL = deploySourceURL;
            this.displayName = displayName;
            this.rootRelativeURL = rootRelativeURL;
          }

          #region IVsOutput2 Members

          public int get_CanonicalName(out string pbstrCanonicalName) {
            pbstrCanonicalName = this.canonicalName;
            return (int)HResult.S_OK;
          }

          public int get_DeploySourceURL(out string pbstrDeploySourceURL) {
            pbstrDeploySourceURL = this.deploySourceURL;
            return (int)HResult.S_OK;
          }

          public int get_DisplayName(out string pbstrDisplayName) {
            pbstrDisplayName = this.displayName;
            return (int)HResult.S_OK;
          }

          public int get_Property(string szProperty, out object pvar) {
            pvar = null;
            return (int)HResult.E_NOTIMPL;
          }

          public int get_RootRelativeURL(out string pbstrRelativePath) {
            pbstrRelativePath = this.rootRelativeURL;
            return (int)HResult.S_OK;
          }

          public int get_Type(out Guid pguidType) {
            pguidType = Guid.Empty;
            return (int)HResult.E_NOTIMPL;
          }

          #endregion
        }

        class BuiltOutputGroupImpl : IVsOutputGroup{
          ProjectConfig config;
          public BuiltOutputGroupImpl(ProjectConfig config){
            this.config = config;
          }

          #region IVsOutputGroup Members

          public int get_CanonicalName(out string pbstrCanonicalName) {
            // C:\Program Files\VSIP 7.1\EnvSDK\BscPrj\VsOutputGroup.cpp:26: #define VS_OUTPUTGROUP_CNAME_Built L"Built"
            const string VS_OUTPUTGROUP_CNAME_Built = "Built";
            pbstrCanonicalName = VS_OUTPUTGROUP_CNAME_Built;
            return (int)HResult.S_OK;
          }

          public int get_DeployDependencies(uint celt, IVsDeployDependency[] rgpdpd, uint[] pcActual) {
            return (int)HResult.E_NOTIMPL;
          }

          public int get_Description(out string pbstrDescription) {
            pbstrDescription = "Built";
            return (int)HResult.S_OK;
          }

          public int get_DisplayName(out string pbstrDisplayName) {
            pbstrDisplayName = "Built";
            return (int)HResult.S_OK;
          }

          public int get_KeyOutput(out string pbstrCanonicalName) {
            pbstrCanonicalName = "";
            return (int)HResult.S_OK;
          }

          public int get_Outputs(uint celt, IVsOutput2[] rgpcfg, uint[] pcActual) {
            bool buildsPdb = this.config.node.HasAttribute("DebugSymbols") && this.config.node.GetAttribute("DebugSymbols") == "True";
            int count = buildsPdb ? 2 : 1;
            if (celt == 0){
              pcActual[0] = checked((uint)count);
              return (int)HResult.S_OK;
            }
            string path = this.config.project.GetOutputAssembly(this.config.node);
            string name = this.config.project.GetAssemblyName(this.config.node);
            Debugger.Log(1, "trace", "IVsOutput path: " + path);
            Debugger.Log(1, "trace", "IVsOutput name: " + name);
            rgpcfg[0] = new Output("", "file:///" + path, name, ".");
            if (buildsPdb){
              rgpcfg[1] = new Output("pdb", "file:///" + Path.ChangeExtension(path, ".pdb"), Path.ChangeExtension(name, ".pdb"), ".");
            }
            if (pcActual != null)
              pcActual[0] = checked((uint)count);
            return (int)HResult.S_OK;
          }

          public int get_ProjectCfg(out IVsProjectCfg2 ppIVsProjectCfg2) {
            ppIVsProjectCfg2 = this.config;
            return (int)HResult.S_OK;
          }

          #endregion
        }

        BuiltOutputGroupImpl builtOutputGroup;

        BuiltOutputGroupImpl BuiltOutputGroup{
          get{
            if (this.builtOutputGroup == null)
              this.builtOutputGroup = new BuiltOutputGroupImpl(this);
            return this.builtOutputGroup;
          }
        }

        public int get_OutputGroups(uint celt, IVsOutputGroup[] rgpcfg, uint[] pcActual) {
          const int groupCount = 1;
          if (celt == 0){
            pcActual[0] = groupCount;
            return (int)HResult.S_OK;
          }
          rgpcfg[0] = this.BuiltOutputGroup;
          if (pcActual != null)
            pcActual[0] = groupCount;
          return (int)HResult.S_OK;
        }

        public int get_VirtualRoot(out string pbstrVRoot) {
          pbstrVRoot = null;
          return (int)HResult.E_NOTIMPL;
        }

        #endregion
    }
    public class BuildSite{
      public virtual void OutputMessage(string message){
      }
      public virtual bool ShouldCancel{
        get{
          return false;
        }
      }
    }
  //=============================================================================
// NOTE: Douglas Hodges advises on out of proc build execution to maximize
// future cross-platform targeting capabilities of the VS tools.
// (Excuse the indenting mess - I made the mistake of using the new Whidbey
// C# editor...)
/// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig"]/*' />
    [CLSCompliant(false)]
    public class BuildableProjectConfig : IVsBuildableProjectCfg{
        ProjectConfig config;
        CookieMap callbacks = new CookieMap();
        IVsOutputWindowPane output;
        uint options;
        Thread thread;
        bool fCleanBuild;
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.BuildableProjectConfig"]/*' />
        public BuildableProjectConfig(ProjectConfig config){
            this.config = config;
        }

        #region IVsBuildableProjectCfg methods
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.AdviseBuildStatusCallback"]/*' />
        public virtual int AdviseBuildStatusCallback(IVsBuildStatusCallback callback, out uint cookie){
          cookie = callbacks.Add(callback);
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.get_ProjectCfg"]/*' />
        public virtual int get_ProjectCfg(out IVsProjectCfg p){
          p = config;
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.QueryStartBuild"]/*' />
        public virtual int QueryStartBuild(uint options, int[] supported, int[] ready){
          config.PrepareBuild(false);
          supported[0] = 1;
          ready[0] = (thread == null) ? 1 : 0;
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.QueryStartClean"]/*' />
        public virtual int QueryStartClean(uint options, int[] supported, int[] ready){
          config.PrepareBuild(false);
          if (supported != null) supported[0] = 1;
          if (ready != null) ready[0] = (thread == null) ? 1 : 0;
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.QueryStartUpToDateCheck"]/*' />
        public virtual int QueryStartUpToDateCheck(uint options, int[] supported, int[] ready){
          config.PrepareBuild(false);
          if (supported != null) supported[0] = 0; // TODO:
          if (ready != null) ready[0] = (thread == null) ? 1 : 0;
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.QueryStatus"]/*' />
        public virtual int QueryStatus(out int done){
          done = (thread != null) ? 0 : 1;
          return 0;
        }
        class MyBuildSite : BuildSite{
          BuildableProjectConfig config;
          public MyBuildSite(BuildableProjectConfig config){
            this.config = config;
          }
          public override void OutputMessage(string message){
            this.config.output.OutputStringThreadSafe(message);
          }
          public override bool ShouldCancel{
            get{
              return !this.config.Tick();
            }
          }
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.StartBuild"]/*' />
        public virtual int StartBuild(IVsOutputWindowPane pane, uint options){
          const uint VS_BUILDABLEPROJECTCFGOPTS_REBUILD = 1;
          bool rebuild = (options & VS_BUILDABLEPROJECTCFGOPTS_REBUILD) != 0;
          if (rebuild) config.Clean();
          config.PrepareBuild(rebuild);
          Debug.Assert(thread == null);
          this.options = options;
          this.output = pane;
          this.fCleanBuild = false;
          if (!this.BuildBegin())
            return 0;
          if (!rebuild && this.config.ProjectMgr.CheckUpToDate(this.config.Node)){
            pane.OutputString("The project is up-to-date.\n");
            this.BuildEnd(true);
            return 0;
          }
          thread = new Thread(new ThreadStart(BuildMain));
          thread.Start();
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.StartClean"]/*' />
        public virtual int StartClean(IVsOutputWindowPane pane, uint options){
          config.Clean();
          Debug.Assert(thread == null);
          this.options = options; // add "clean" option
          this.output = pane;
          this.fCleanBuild = true;
          if (!this.BuildBegin())
            return 0;
          thread = new Thread(new ThreadStart(BuildMain));
          thread.Start();
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.StartUpToDateCheck"]/*' />
        public virtual int StartUpToDateCheck(IVsOutputWindowPane pane, uint options){
          this.output = pane;
          if (!this.BuildBegin())
            return (int)HResult.S_OK;
          bool upToDate = this.config.ProjectMgr.CheckUpToDate(this.config.Node);
          this.BuildEnd(upToDate);
          return (int)HResult.S_OK;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.Stop"]/*' />
        public virtual int Stop(int fsync){
          if (thread != null){
            thread.Abort();
            thread = null;
          }
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.UnadviseBuildStatusCallback"]/*' />
        public virtual int UnadviseBuildStatusCallback(uint cookie){
          callbacks.RemoveAt(cookie);
          return 0;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.Wait"]/*' />
        public virtual int Wait(uint ms, int fTickWhenMessageQNotEmpty){
          return (int)HResult.E_NOTIMPL;
        }
        #endregion 

        static void AssertOK(HResult result){
            if (result != HResult.S_OK)
                throw new COMException("Unexpected error.", (int) result);
        }

        bool BuildBegin(){
            foreach (IVsBuildStatusCallback cb in this.callbacks){
                int fContinue = 1;
                AssertOK((HResult) cb.BuildBegin(ref fContinue));
                if (fContinue == 0)
                    return false;
            }
            return true;
        }

      bool Tick(){
        foreach (IVsBuildStatusCallback cb in this.callbacks){
          int fContinue = 1;
          AssertOK((HResult) cb.Tick(ref fContinue));
          if (fContinue == 0)
            return false;
        }
        return true;
      }

      void BuildEnd(bool success){
            int fSuccess = success ? 1 : 0;
            foreach (IVsBuildStatusCallback cb in this.callbacks){
                AssertOK((HResult) cb.BuildEnd(fSuccess));
            }
            if (this.output != null)
                this.output.FlushToTaskList();
        }

        void BuildMain(){
            bool ok = false;
            try{
                ok = config.ProjectMgr.Build(this.options, this.config.Node, this.output, this.fCleanBuild, new MyBuildSite(this));
            } catch (Exception e){
                output.OutputStringThreadSafe("Unhandled Exception:" + e.Message + "\n");
            }

            this.BuildEnd(ok);
        }
    }
}