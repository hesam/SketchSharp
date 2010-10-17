//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
namespace Microsoft.VisualStudio.CodeTools
{
  using System;
  using System.Runtime.InteropServices; 
  using Microsoft.VisualStudio.Shell.Interop;
  using Microsoft.VisualStudio.Shell;

  using Microsoft.VisualStudio.TextManager.Interop;
  using System.ComponentModel.Design;
  using Microsoft.VisualStudio.OLE.Interop;

  using System.Collections.Generic;
  using System.Threading;

  // The main package class
  #region Attributes
  [Microsoft.VisualStudio.Shell.DefaultRegistryRoot("Software\\Microsoft\\VisualStudio\\9.0")
  ,Microsoft.VisualStudio.Shell.ProvideLoadKey("Standard", "1.0", "Microsoft.VisualStudio.CodeTools.TaskManager", "Microsoft", 1)
  ,Microsoft.VisualStudio.Shell.ProvideMenuResource(1000, 1)
  ,Guid("DA85543E-97EC-4478-90EC-45CBCB4FA5C1")
  ,ComVisible(true)]
  #endregion
  internal sealed class TaskManagerPackage : Microsoft.VisualStudio.Shell.Package
                                , IVsSolutionEvents, IVsUpdateSolutionEvents
                                , ITaskManagerFactory, IOleCommandTarget 
  {
    #region ITaskManagerFactory

    public ITaskManager CreateTaskManager(string providerName)
    {
      return new TaskManager(providerName); 
    }

    private IList<TaskManager> sharedTaskManagers;

    public ITaskManager QuerySharedTaskManager(string providerName, bool createIfAbsent)
    {
      foreach (TaskManager taskManager in sharedTaskManagers) {
        string name;
        taskManager.GetProviderName(out name);
        if (name == providerName) {
          Interlocked.Increment(ref taskManager.sharedCount);
          return taskManager;
        }
      }
      if (createIfAbsent) {
        TaskManager taskManager = new TaskManager(providerName);
        Interlocked.Increment(ref taskManager.sharedCount);
        sharedTaskManagers.Add(taskManager);
        return taskManager;
      }
      else
        return null;
    }

    public void ReleaseSharedTaskManager(ITaskManager itaskManager)
    {
      if (itaskManager == null) return;
      if (!(itaskManager is TaskManager)) return;
      TaskManager taskManager = (TaskManager)itaskManager;
      if (sharedTaskManagers.Contains(taskManager)) {
        Interlocked.Decrement(ref taskManager.sharedCount);
        if (taskManager.sharedCount == 0) {
          taskManager.Release();
          sharedTaskManagers.Remove(taskManager);
        }
      }      
    }

    #endregion

    #region Private fields
    private Tools tools;
    private RootMenu taskListMenu;
    #endregion

    #region Construction
    private IVsSolution solution;
    private uint solutionEventsCookie;
    private IVsSolutionBuildManager buildManager;
    private uint buildManagerCookie;

    protected override void Initialize()
    {
      base.Initialize();
      
      // Initialize common functionality
      Common.Initialize(this);
      taskListMenu = new RootMenu();
      
      // Initialize fields
      tools = new Tools();
      sharedTaskManagers = new List<TaskManager>();

      // Attach to solution events
      solution = GetService(typeof(SVsSolution)) as IVsSolution;
      // if (solution == null) throw new NullReferenceException();
      solution.AdviseSolutionEvents(this as IVsSolutionEvents, out solutionEventsCookie);

      // Attach to build events
      buildManager = GetService(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager;
      // if (buildManager == null) throw new NullReferenceException();
      buildManager.AdviseUpdateSolutionEvents(this, out buildManagerCookie);

      // Add a TaskManagerFactory service
      (this as System.ComponentModel.Design.IServiceContainer).AddService(typeof(ITaskManagerFactory), this, true);
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing) {
        tools.Release();
        tools = null;

        sharedTaskManagers.Clear();
        sharedTaskManagers = null;

        if (buildManager != null) {
          buildManager.UnadviseUpdateSolutionEvents(buildManagerCookie);
          buildManager = null;
        }
        
        if (solution != null) {
          solution.UnadviseSolutionEvents(solutionEventsCookie);
          solution = null;
        }

        taskListMenu.Release();
        taskListMenu = null;
        Common.Release();
        GC.SuppressFinalize(this);
      }
      base.Dispose(disposing);
    } 
    #endregion

    #region IVsSolutionEvents Members

    // We watch solution events to automatically set the host objects
    // for newly loaded/opened projects
    int IVsSolutionEvents.OnAfterCloseSolution(object pUnkReserved)
    {
      return 0;
    }

    int IVsSolutionEvents.OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
    {
      tools.ProjectSetHostObjects(pRealHierarchy);
      return 0;
    }

    int IVsSolutionEvents.OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
    {
      tools.ProjectSetHostObjects(pHierarchy);
      /*
      object objClsids;
      int hr = pHierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID2.VSHPROPID_CfgPropertyPagesCLSIDList, out objClsids);
      ErrorHandler.ThrowOnFailure(hr);
      string clsids = objClsids as String;
      clsids = clsids + ";{35A69422-A11A-4ce8-8962-061DFABB02EB}";
      hr = pHierarchy.SetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID2.VSHPROPID_CfgPropertyPagesCLSIDList, clsids);
      ErrorHandler.ThrowOnFailure(hr);
      */
      return 0;
    }

    int IVsSolutionEvents.OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
    {
      tools.RefreshTasks();
      return 0;
    }

    int IVsSolutionEvents.OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
    {
      tools.ClearTasksOnHierarchy(pHierarchy);
      return 0;
    }

    int IVsSolutionEvents.OnBeforeCloseSolution(object pUnkReserved)
    {
      tools.ClearTasks();
      return 0;
    }

    int IVsSolutionEvents.OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
    {
      return 0;
    }

    int IVsSolutionEvents.OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
    {
      return 0;
    }

    int IVsSolutionEvents.OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
    {
      return 0;
    }

    int IVsSolutionEvents.OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
    {
      return 0;
    }

    #endregion

    #region IVsUpdateSolutionEvents Members

    // We watch build to automatically clear and refresh the tool lists
    public int OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy)
    {
      tools.ClearTasksOnHierarchy(pIVsHierarchy);
      return 0;
    }

    public int UpdateSolution_Begin(ref int pfCancelUpdate)
    {
      pfCancelUpdate = 0;
      tools.ClearTasks();
      return 0;
    }

    public int UpdateSolution_Cancel()
    {
      tools.RefreshTasks(); 
      return 0;
    }

    public int UpdateSolution_Done(int fSucceeded, int fModified, int fCancelCommand)
    {
      tools.RefreshTasks();
      return 0;
    }

    public int UpdateSolution_StartUpdate(ref int pfCancelUpdate)
    {
      return 0;
    }
    #endregion

    #region IOleCommandTarget Members

    public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
    {
      if (taskListMenu != null)
        return taskListMenu.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
      else
        return OLECMDERR.OLECMDERR_E_NOTSUPPORTED;
    }

    public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
    {
      if (taskListMenu != null)
        return taskListMenu.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
      else
        return OLECMDERR.OLECMDERR_E_UNKNOWNGROUP;
    }

    #endregion
  }
}