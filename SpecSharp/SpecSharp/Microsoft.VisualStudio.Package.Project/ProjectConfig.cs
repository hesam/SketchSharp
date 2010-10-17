//#define ConfigTrace
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.IO;
using MSBuild = Microsoft.Build.BuildEngine;

namespace Microsoft.VisualStudio.Package
{
    /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig"]/*' />
    /// <summary>
    /// project config class holds project specific configuration data. It NEEDS to supply ISpecifyPropertyPages
    /// if you want to be able to show config related data in the property pane.
    /// </summary>
    [CLSCompliant(false), ComVisible(true)]
    public class ProjectConfig : IVsCfg, IVsProjectCfg, IVsDebuggableProjectCfg, ISpecifyPropertyPages
    {
        private ProjectNode project;
        private string configName;
        private MSBuild.BuildPropertyGroup currentConfig = null;

        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.ProjectConfig"]/*' />
        public ProjectConfig(ProjectNode project, string configuration)
        {
            this.project = project;
            this.configName = configuration;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.ProjectMgr"]/*' />
        public ProjectNode ProjectMgr
        {
            get
            {
                return this.project;
            }
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.Node"]/*' />
        public string ConfigName
        {
            get
            {
                return this.configName;
            }
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.ISpecifyPropertyPages.GetPages"]/*' />
        /// <internalonly/>
        void ISpecifyPropertyPages.GetPages(CAUUID[] ppages)
        {
            ppages[0] = new CAUUID();
            Guid[] guids = project.GetConfigPropertyPageGuids(this.configName);
            ppages[0].cElems = (uint)guids.Length;
            int size = Marshal.SizeOf(typeof(Guid));
            ppages[0].pElems = Marshal.AllocCoTaskMem(guids.Length * size);
            IntPtr ptr = ppages[0].pElems;
            for (int i = 0; i < guids.Length; i++)
            {
                Marshal.StructureToPtr(guids[i], ptr, false);
                ptr = new IntPtr(ptr.ToInt64() + size);
            }
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.PrepareBuild"]/*' />
        public void PrepareBuild(bool clean)
        {
            project.PrepareBuild(this.configName, clean);
        }

        ////////////////////////////////////////////////////
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_DisplayName"]/*' />
        /// <summary>
        /// The display name is a two part item
        /// first part is the config name, 2nd part is the platform name
        /// </summary>
        #region IVsCfg methods
        public virtual int get_DisplayName(out string name)
        {
            string[] platform = new string[1];
            uint[] actual = new uint[1];
            name = this.configName;
            // currently, we only support one platform, so just add it..
            NativeMethods.ThrowOnFailure(((IVsCfgProvider2)project).GetPlatformNames(1, platform, actual));
            name += "|" + platform[0];
            return NativeMethods.S_OK;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_IsDebugOnly"]/*' />
        public virtual int get_IsDebugOnly(out int fDebug)
        {
            fDebug = 0;
            if (this.configName == "Debug")
            {
                fDebug = 1;
            }
            return NativeMethods.S_OK;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_IsReleaseOnly"]/*' />
        public virtual int get_IsReleaseOnly(out int fRelease)
        {
            CCITracing.TraceCall();
            fRelease = 0;
            if (this.configName == "Release")
            {
                fRelease = 1;
            }
            return NativeMethods.S_OK;
        }
        #endregion 

        #region IVsProjectCfg methods 
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.EnumOutputs"]/*' />
        public virtual int EnumOutputs(out IVsEnumOutputs eo)
        {
            CCITracing.TraceCall();
            eo = null;
            return NativeMethods.E_NOTIMPL;
        }

        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_BuildableProjectCfg"]/*' />
        public virtual int get_BuildableProjectCfg(out IVsBuildableProjectCfg pb)
        {
            CCITracing.TraceCall();
            pb = new BuildableProjectConfig(this);
            return NativeMethods.S_OK;
        }

        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_CanonicalName"]/*' />
        public virtual int get_CanonicalName(out string name)
        {
            return ((IVsCfg)this).get_DisplayName(out name);
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_IsPackaged"]/*' />
        public virtual int get_IsPackaged(out int pkgd)
        {
            CCITracing.TraceCall();
            pkgd = 0;
            return NativeMethods.S_OK;
        }

        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_IsSpecifyingOutputSupported"]/*' />
        public virtual int get_IsSpecifyingOutputSupported(out int f)
        {
            CCITracing.TraceCall();
            f = 1;
            return NativeMethods.S_OK;
        }

        ////////////////////////////////////////////////////
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_Platform"]/*' />
        /// <summary>
        /// This method is obsolete, return E_NOTIMPL
        /// </summary>
        public virtual int get_Platform(out Guid platform)
        {
            CCITracing.TraceCall();
            platform = Guid.Empty;
            return NativeMethods.E_NOTIMPL;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_ProjectCfgProvider"]/*' />
        public virtual int get_ProjectCfgProvider(out IVsProjectCfgProvider p)
        {
            CCITracing.TraceCall();

            p = (IVsProjectCfgProvider)project;
            return NativeMethods.S_OK;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_RootURL"]/*' />
        public virtual int get_RootURL(out string root)
        {
            CCITracing.TraceCall();
            root = null;
            return NativeMethods.S_OK;
        }

        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_TargetCodePage"]/*' />
        public virtual int get_TargetCodePage(out uint target)
        {
            CCITracing.TraceCall();
            target = (uint)System.Text.Encoding.Default.CodePage;
            return NativeMethods.S_OK;
        }

        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.get_UpdateSequenceNumber"]/*' />
        public virtual int get_UpdateSequenceNumber(ULARGE_INTEGER[] li)
        {
            CCITracing.TraceCall();
            li[0] = new ULARGE_INTEGER();
            li[0].QuadPart = 0;
            return NativeMethods.S_OK;
        }

        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.OpenOutput"]/*' />
        public virtual int OpenOutput(string name, out IVsOutput output)
        {
            CCITracing.TraceCall();
            output = null;
            return NativeMethods.E_NOTIMPL;
        }
        #endregion 

        static Guid CLSID_ComPlusOnlyDebugEngine = new Guid("449EC4CC-30D2-4032-9256-EE18EB41B62B");

        #region IVsDebuggableProjectCfg methods
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.DebugLaunch"]/*' />
        public virtual int DebugLaunch(uint flags)
        {
            CCITracing.TraceCall();

            try
            {
                VsDebugTargetInfo info = new VsDebugTargetInfo();
                info.cbSize = (uint)Marshal.SizeOf(info);
                info.dlo = Microsoft.VisualStudio.Shell.Interop.DEBUG_LAUNCH_OPERATION.DLO_CreateProcess;

                // On first call, reset the cache, following calls will use the cached values
                string property = GetConfigurationProperty("StartProgram", true);
                if (property != null && property.Length > 0)
                {
                    info.bstrExe = property;
                }
                else
                {
                    info.bstrExe = this.project.GetOutputAssembly(this.ConfigName);
                }

                property = GetConfigurationProperty("WorkingDirectory", false);
                if (property != null && property.Length > 0)
                {
                    info.bstrCurDir = property;
                }
                else
                {
                    info.bstrCurDir = Path.GetDirectoryName(info.bstrExe);
                }

                property = GetConfigurationProperty("CmdArgs", false);
                if (property != null && property.Length > 0)
                {
                    info.bstrArg = property;
                }

                property = GetConfigurationProperty("RemoteDebugMachine", false);
                if (property != null && property.Length > 0)
                {
                    info.bstrRemoteMachine = property;
                }

                info.fSendStdoutToOutputWindow = 0;
                info.clsidCustom = CLSID_ComPlusOnlyDebugEngine;
                info.grfLaunch = flags;
                this.project.LaunchDebugger(info);
            }
            catch
            {
                return NativeMethods.E_FAIL;
            }

            return NativeMethods.S_OK;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.QueryDebugLaunch"]/*' />
        public virtual int QueryDebugLaunch(uint flags, out int fCanLaunch)
        {
            CCITracing.TraceCall();
            string assembly = this.project.GetAssemblyName(this.ConfigName);
            fCanLaunch = (assembly != null && assembly.ToLower(CultureInfo.InvariantCulture).EndsWith(".exe")) ? 1 : 0;
            if (fCanLaunch == 0)
            {
                string property = GetConfigurationProperty("StartProgram", true);
                fCanLaunch = (property != null && property.Length > 0) ? 1 : 0;
            }
            return NativeMethods.S_OK;
        }
        #endregion 

        private MSBuild.BuildProperty GetMsBuildProperty(string propertyName, bool resetCache)
        {
            if (resetCache || this.currentConfig == null)
            {
                // Get properties for current configuration from project file and cache it
                this.ProjectMgr.MSBuildProject.GlobalProperties.SetProperty("Configuration", this.ConfigName);
                this.currentConfig = this.ProjectMgr.MSBuildProject.EvaluatedProperties;
            }

            if (this.currentConfig == null)
                throw new Exception("Failed to retrive properties");

            // return property asked for
            return this.currentConfig[propertyName];
        }

        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.GetConfigurationProperty"]/*' />
        /// <summary>
        /// Return the value of the property in this configuration
        /// The implementation does not need to use a cache. If it does
        /// resetCache==true should cause the content of the cache to be
        /// ignored.
        /// </summary>
        /// <param name="propertyName">Name of the property to get</param>
        /// <param name="resetCache">true = force the cache to be invalidated; false = cached value acceptable</param>
        /// <returns>Value of the property. null if property does not exist in this configuration</returns>
        public virtual string GetConfigurationProperty(string propertyName, bool resetCache)
        {
            MSBuild.BuildProperty property = GetMsBuildProperty(propertyName, resetCache);
            if (property == null)
                return null;

            return property.Value;
        }

        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="ProjectConfig.SetConfigurationProperty"]/*' />
        public virtual void SetConfigurationProperty(string propertyName, string propertyValue)
        {
            this.ProjectMgr.SetProjectFileDirty(true);
            string condition = String.Format(CultureInfo.InvariantCulture, ConfigProvider.configString, this.ConfigName);
            this.ProjectMgr.MSBuildProject.SetProperty(propertyName, propertyValue, condition);

            // property cache will need to be updated
            this.currentConfig = null;

            return;
        }
    }

//=============================================================================
// NOTE: advises on out of proc build execution to maximize
// future cross-platform targeting capabilities of the VS tools.
/// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig"]/*' />
    [CLSCompliant(false)]
    public class BuildableProjectConfig : IVsBuildableProjectCfg
    {
        ProjectConfig config;
        EventSinkCollection callbacks = new EventSinkCollection();
        IVsOutputWindowPane output;
        uint options;
        Thread thread;
        bool fCleanBuild;
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.BuildableProjectConfig"]/*' />
        public BuildableProjectConfig(ProjectConfig config)
        {
            this.config = config;
        }

        #region IVsBuildableProjectCfg methods
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.AdviseBuildStatusCallback"]/*' />
        public virtual int AdviseBuildStatusCallback(IVsBuildStatusCallback callback, out uint cookie)
        {
            CCITracing.TraceCall();

            cookie = callbacks.Add(callback);
            return NativeMethods.S_OK;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.get_ProjectCfg"]/*' />
        public virtual int get_ProjectCfg(out IVsProjectCfg p)
        {
            CCITracing.TraceCall();

            p = config;
            return NativeMethods.S_OK;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.QueryStartBuild"]/*' />
        public virtual int QueryStartBuild(uint options, int[] supported, int[] ready)
        {
            CCITracing.TraceCall();
            config.PrepareBuild(false);
            supported[0] = 1;
            ready[0] = (thread == null) ? 1 : 0;
            return NativeMethods.S_OK;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.QueryStartClean"]/*' />
        public virtual int QueryStartClean(uint options, int[] supported, int[] ready)
        {
            CCITracing.TraceCall();
            config.PrepareBuild(false);
            if (supported != null) supported[0] = 1;
            if (ready != null) ready[0] = (thread == null) ? 1 : 0;
            return NativeMethods.S_OK;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.QueryStartUpToDateCheck"]/*' />
        public virtual int QueryStartUpToDateCheck(uint options, int[] supported, int[] ready)
        {
            CCITracing.TraceCall();
            config.PrepareBuild(false);
            if (supported != null) supported[0] = 0; // TODO:
            if (ready != null) ready[0] = (thread == null) ? 1 : 0;
            return NativeMethods.S_OK;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.QueryStatus"]/*' />
        public virtual int QueryStatus(out int done)
        {
            CCITracing.TraceCall();

            done = (thread != null) ? 0 : 1;
            return NativeMethods.S_OK;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.StartBuild"]/*' />
        public virtual int StartBuild(IVsOutputWindowPane pane, uint options)
        {
            CCITracing.TraceCall();
            config.PrepareBuild(false);
            Debug.Assert(thread == null);
            this.options = options;
            this.output = pane;
            this.fCleanBuild = false;
            // Current version of MSBuild wish to be called in an STA
            BuildMain();
//            thread = new Thread(new ThreadStart(BuildMain));
//            thread.Start();
            return NativeMethods.S_OK;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.StartClean"]/*' />
        public virtual int StartClean(IVsOutputWindowPane pane, uint options)
        {
            CCITracing.TraceCall();
            config.PrepareBuild(true);
            Debug.Assert(thread == null);
            this.options = options; // add "clean" option
            this.output = pane;
            this.fCleanBuild = true;
            // Current version of MSBuild wish to be called in an STA
            BuildMain();
//            thread = new Thread(new ThreadStart(BuildMain));
//            thread.Start();
            return NativeMethods.S_OK;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.StartUpToDateCheck"]/*' />
        public virtual int StartUpToDateCheck(IVsOutputWindowPane pane, uint options)
        {
            CCITracing.TraceCall();

            return NativeMethods.E_NOTIMPL;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.Stop"]/*' />
        public virtual int Stop(int fsync)
        {
            CCITracing.TraceCall();

            if (thread != null)
            {
                thread.Abort();
                thread = null;
            }
            return NativeMethods.S_OK;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.UnadviseBuildStatusCallback"]/*' />
        public virtual int UnadviseBuildStatusCallback(uint cookie)
        {
            CCITracing.TraceCall();


            callbacks.RemoveAt(cookie);
            return NativeMethods.S_OK;
        }
        /// <include file='doc\ProjectConfig.uex' path='docs/doc[@for="BuildableProjectConfig.Wait"]/*' />
        public virtual int Wait(uint ms, int fTickWhenMessageQNotEmpty)
        {
            CCITracing.TraceCall();

            return NativeMethods.E_NOTIMPL;
        }
        #endregion 

        void BuildMain()
        {
            int fContinue = 1;
            foreach (IVsBuildStatusCallback cb in callbacks)
            {
                try
                {
                    NativeMethods.ThrowOnFailure(cb.BuildBegin(ref fContinue));
                    if (fContinue == 0)
                        return;
                }
                catch (Exception e)
                {
                    Debug.Fail(String.Format("Exception was thrown during BuildBegin event\n{0}", e.Message));
                }
            }
            bool ok = false;
            try
            {
                string target = null;
                if (this.fCleanBuild)
                    target = "Rebuild";
                else
                    target = "Build";

                ok = config.ProjectMgr.Build(this.options, this.config.ConfigName, this.output, target);
            }
            catch (Exception e)
            {
                NativeMethods.ThrowOnFailure(output.OutputStringThreadSafe("Unhandled Exception:" + e.Message + "\n"));
            }
            int fSuccess = ok ? 1 : 0;
            foreach (IVsBuildStatusCallback cb in callbacks)
            {
                try
                {
                    NativeMethods.ThrowOnFailure(cb.BuildEnd(fSuccess));
                }
                catch(Exception e)
                {
                    Debug.Fail(String.Format("Exception was thrown during BuildEnd event\n{0}", e.Message));
                }
            }
            NativeMethods.ThrowOnFailure(output.FlushToTaskList());
        }
    }
}