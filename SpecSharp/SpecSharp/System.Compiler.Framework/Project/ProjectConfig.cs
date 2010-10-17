//#define ConfigTrace
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

  /// <summary>
  /// project config class holds project specific configuration data. It NEEDS to supply ISpecifyPropertyPages
  /// if you want to be able to show config related data in the property pane.
  /// </summary>
  public class ProjectConfig : IVsCfg, IVsProjectCfg, IVsDebuggableProjectCfg, ISpecifyPropertyPages
  {
    private ProjectManager project;
    private XmlElement node;

    public  ProjectConfig(ProjectManager project, XmlElement node) {
      this.project = project;
      this.node = node;
    }

    public ProjectManager ProjectMgr  {
      get  {
        return this.project; 
      }
    }

    public XmlElement Node  {
      get  {
        return this.node; 
      }
    }

    public Guid[] GetPropertyPageGuids() {
      Guid[] result = new Guid[3];
      result[0] = typeof(BuildPropertyPage).GUID;
      result[1] = typeof(DebugPropertyPage).GUID;
      result[2] = typeof(AdvancedPropertyPage).GUID;
      return result;
    }

    void ISpecifyPropertyPages.GetPages(CAUUID[] ppages) {
      ppages[0] = new CAUUID();
      Guid[] guids = GetPropertyPageGuids();        
      ppages[0].cElems = (uint)guids.Length;
      int size = Marshal.SizeOf(typeof(Guid));
      ppages[0].pElems = Marshal.AllocCoTaskMem(guids.Length*size);
      IntPtr ptr = ppages[0].pElems;
      for (int i = 0; i < guids.Length; i++) {      
        Marshal.StructureToPtr(guids[i], ptr, false);
        ptr = new IntPtr(ptr.ToInt64() + size);
      }
    }

    public void PrepareBuild() {
      project.PrepareBuild(node, false);
    }

  ////////////////////////////////////////////////////
  /// <summary>
  /// The display name is a two part item
  /// first part is the config name, 2nd part is the platform name
  /// </summary>
  ////////////////////////////////////////////////////
  #region IVsCfg methods
  public virtual void get_DisplayName(out string name) {
      CCITracing.TraceCall();

      string [] platform = new string[1]; 
      uint[]  actual = new uint[1];
      name = this.node.GetAttribute("Name");
      // currently, we only support one platform, so just add it..
      ((IVsCfgProvider2)project).GetPlatformNames(1, platform, actual); 
      name += "|" + platform[0]; 
    }

    public virtual void get_IsDebugOnly(out int fDebug) {
      CCITracing.TraceCall();
      fDebug = 0;
      if (this.node.GetAttribute("Name") == "Debug") {
        fDebug = 1;
      }
    }


    public virtual void get_IsReleaseOnly(out int fRelease) {
      CCITracing.TraceCall();
      fRelease = 0;
      if (this.node.GetAttribute("Name") == "Release") {
       fRelease = 1;
      }
    }
    #endregion 

    #region IVsProjectCfg methods 
    public virtual void EnumOutputs(out IVsEnumOutputs eo) {
      CCITracing.TraceCall();
      throw new NotImplementedException();
    }

    public virtual void get_BuildableProjectCfg(out IVsBuildableProjectCfg pb) {

      CCITracing.TraceCall();

      pb = new BuildableProjectConfig(this);
    }

    public virtual void get_CanonicalName(out string name) {
      ((IVsCfg)this).get_DisplayName(out name);
    }

    public virtual void get_IsPackaged(out int pkgd) {

      CCITracing.TraceCall();

      pkgd = 0;
    }
   
    public virtual void get_IsSpecifyingOutputSupported(out int f) {

      CCITracing.TraceCall();

      f = 1;
    }

    ////////////////////////////////////////////////////
    /// <summary>
    /// This method is obsolete, return E_NOTIMPL
    /// </summary>
    ////////////////////////////////////////////////////
    public virtual void get_Platform(out Guid platform) {
      CCITracing.TraceCall();
//      platform = Guid.Empty;
      throw new NotImplementedException();
    }

    public virtual void get_ProjectCfgProvider(out IVsProjectCfgProvider p) {

      CCITracing.TraceCall();

      p = (IVsProjectCfgProvider)project;
    }

    public virtual void get_RootURL(out string root) {

      CCITracing.TraceCall();

      root = null;
    }

    public virtual void get_TargetCodePage(out uint target) {

      CCITracing.TraceCall();

      target = (uint)System.Text.Encoding.Default.CodePage;
    }

    public virtual void get_UpdateSequenceNumber(ULARGE_INTEGER[] li){

      CCITracing.TraceCall();

      li[0] = new ULARGE_INTEGER();
      li[0].QuadPart = 0;
    }

    public virtual void OpenOutput(string name, out IVsOutput output) {

      CCITracing.TraceCall();

      throw new NotImplementedException();
    }
    #endregion 

    static Guid CLSID_ComPlusOnlyDebugEngine = new Guid("449EC4CC-30D2-4032-9256-EE18EB41B62B");

    #region IVsDebuggableProjectCfg methods
    public virtual void DebugLaunch(uint flags) {     

      CCITracing.TraceCall();


      try {
        IVsDebugger d = (IVsDebugger)project.QueryService(typeof(IVsDebugger).GUID, typeof(IVsDebugger));
			        
        VsDebugTargetInfo info = new VsDebugTargetInfo();
        info.cbSize =(uint) Marshal.SizeOf(info);
        info.dlo = Microsoft.VisualStudio.Shell.Interop.DEBUG_LAUNCH_OPERATION.DLO_CreateProcess;
        if (this.node.HasAttribute("StartProgram") && this.node.GetAttribute("StartProgram").Length > 0) {
          info.bstrExe = this.node.GetAttribute("StartProgram");
        }
        else {
          info.bstrExe =  this.project.GetOutputAssembly(node);   
        }
			
        if (this.node.HasAttribute("WorkingDirectory") && this.node.GetAttribute("WorkingDirectory").Length > 0) {
          info.bstrCurDir = this.node.GetAttribute("WorkingDirectory");
        }
        else {
          info.bstrCurDir = Path.GetDirectoryName(info.bstrExe);  
        }

        if (this.node.HasAttribute("CmdArgs") && this.node.GetAttribute("CmdArgs").Length > 0) {
          info.bstrArg = this.node.GetAttribute("CmdArgs");
        }			
        if (this.node.HasAttribute("RemoteDebugMachine") && this.node.GetAttribute("RemoteDebugMachine").Length > 0) {
          info.bstrRemoteMachine = this.node.GetAttribute("RemoteDebugMachine");
        }			

        info.fSendStdoutToOutputWindow = 0;
        info.clsidCustom = CLSID_ComPlusOnlyDebugEngine;
        info.grfLaunch = flags; 

        IntPtr ptr = Marshal.AllocCoTaskMem((int)info.cbSize);
        Marshal.StructureToPtr(info, ptr, false);
        try {
          d.LaunchDebugTargets(1, ptr);
        } finally{
          Marshal.FreeCoTaskMem(ptr);
        }
      }
      catch (Exception e) {
        throw new SystemException("Could not launch debugger - " +e.Message);
      }
    }

    public virtual void QueryDebugLaunch(uint flags, out int fCanLaunch) {
      CCITracing.TraceCall();
      string assembly = this.project.GetAssemblyName(node);
      fCanLaunch = (assembly != null && assembly.ToLower().EndsWith(".exe")) ? 1 : 0;
      if (fCanLaunch==0) {
        fCanLaunch = (this.node.HasAttribute("StartProgram") && this.node.GetAttribute("StartProgram").Length > 0) ? 1 : 0;
      }
    }
    #endregion 
  }  

  //=============================================================================
  // NOTE: Douglas Hodges advises on out of proc build execution to maximize
  // future cross-platform targeting capabilities of the VS tools.
  public class BuildableProjectConfig : IVsBuildableProjectCfg {
    ProjectConfig config;
    CookieMap callbacks = new CookieMap();
    IVsOutputWindowPane output;
    uint options;
    Thread thread;
    bool  fCleanBuild; 

    public BuildableProjectConfig(ProjectConfig config) {
      this.config = config;
    }

    #region IVsBuildableProjectCfg methods
    public virtual void AdviseBuildStatusCallback(IVsBuildStatusCallback callback, out uint cookie) {

      CCITracing.TraceCall();

      cookie = callbacks.Add(callback);      
    }

    public virtual void  get_ProjectCfg(out IVsProjectCfg p) {

      CCITracing.TraceCall();

      p = config;
    }

    public virtual void QueryStartBuild(uint options, int[] supported, int[] ready) {

      CCITracing.TraceCall();
      config.PrepareBuild();
      supported[0] = 1;
      ready[0] = (thread == null) ? 1 : 0;
    }

    public virtual void QueryStartClean(uint options, int[]  supported, int[]  ready) {

      CCITracing.TraceCall();
      config.PrepareBuild();
      if (supported != null) supported[0] = 1;
      if (ready != null) ready[0] = (thread == null) ? 1 : 0;
    }

    public virtual void QueryStartUpToDateCheck(uint options, int[]  supported, int[]  ready) {

      CCITracing.TraceCall();
      config.PrepareBuild();
      if (supported != null) supported[0] = 0; // TODO:
      if (ready != null) ready[0] = (thread == null) ? 1 : 0;
    }

    public virtual void QueryStatus(out int done) {

      CCITracing.TraceCall();

      done = (thread != null) ? 0 : 1;
    }

    public virtual void StartBuild(IVsOutputWindowPane pane, uint options) {

      CCITracing.TraceCall();

      Debug.Assert(thread == null);
      this.options = options;
      this.output = pane;
      this.fCleanBuild = false; 
      thread = new Thread(new ThreadStart(BuildMain));
      thread.Start();
    }

    public virtual void StartClean(IVsOutputWindowPane pane, uint options) {

      CCITracing.TraceCall();

      Debug.Assert(thread == null);
      this.options = options; // add "clean" option
      this.output = pane;
      this.fCleanBuild = true; 
      thread = new Thread(new ThreadStart(BuildMain));
      thread.Start();
    }

   public virtual void StartUpToDateCheck(IVsOutputWindowPane pane, uint options) {

      CCITracing.TraceCall();

      throw new NotImplementedException();
    }

    public virtual void Stop(int fsync) {

      CCITracing.TraceCall();

      if (thread != null) {
        thread.Abort();
        thread = null;
      }
    }

    public virtual void UnadviseBuildStatusCallback(uint cookie) {

      CCITracing.TraceCall();

      callbacks.RemoveAt(cookie);
    }

    public virtual void Wait(uint ms, int fTickWhenMessageQNotEmpty) {

      CCITracing.TraceCall();

      throw new NotImplementedException();
    }
    #endregion 

    void BuildMain() {
      
      int fContinue = 1;
      foreach (IVsBuildStatusCallback cb in callbacks) {
        cb.BuildBegin(ref fContinue);
        if (fContinue == 0) 
          return;
      }

      bool ok = false;
      try {
        ok = config.ProjectMgr.Build(this.options, this.config.Node, this.output, this.fCleanBuild);
      } catch (Exception e) {
        output.OutputStringThreadSafe ("Unhandled Exception:"+e.Message+"\n");
      }

      int fSuccess = ok? 1 : 0;
      foreach (IVsBuildStatusCallback cb in callbacks) {
        cb.BuildEnd(fSuccess);
      }

      output.FlushToTaskList();

    }

  }
}


