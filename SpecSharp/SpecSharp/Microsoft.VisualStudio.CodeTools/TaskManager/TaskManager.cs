//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
namespace Microsoft.VisualStudio.CodeTools
{
  using System;
  using System.Runtime.InteropServices;
  using System.Collections.Generic;

  using Microsoft.VisualStudio.Shell.Interop;
  using Microsoft.VisualStudio.TextManager.Interop;
  using Microsoft.VisualStudio.CodeTools;
  using Microsoft.VisualStudio.Shell;
  
  class TaskManager : ITaskManager
                    , IVsTaskProvider, IVsTaskProvider2, IVsTaskProvider3
                    , IVsRunningDocTableEvents
                    , IServiceProvider 
                    , IVsTaskManager
                    , Microsoft.Build.Framework.ITaskHost
  {   
    #region Fields
    
    private List<Task> tasks = new List<Task>();
    private string providerName;
    private bool released;

    public event ClearTasksEvent OnClearTasks;
    #endregion

    #region Construction
    public TaskManager(string providerName)
    {
      this.released = false;
      this.providerName = providerName;

      InitializeTaskProvider();
      InitializeRunningDocTableEvents();
      InitializeOutputWindow();
    }

    ~TaskManager()
    {
      Release();
    }

    internal int sharedCount;

    #endregion

    #region ITaskManager
    public void Release()
    {
      if (!released) {
        released = true;
        ClearTasks();
        DisposeOutputWindow();
        DisposeTaskProvider();
        DisposeRunningDocTableEvents();
      }
    }

 
    public void AddTask(string description, string tipText, string code, string helpKeyword
                       ,TaskPriority priority, TaskCategory category
                       ,TaskMarker marker, Guid outputPane
                       ,string projectName
                       ,string fileName, int startLine, int startColumn, int endLine, int endColumn 
                       ,ITaskCommands commands
                       )
    {
      // Add a new task
      Location span = new Location(projectName,fileName, startLine, startColumn, endLine, endColumn);
      Task task = new Task(this
                          ,description, tipText, code, helpKeyword
                          ,priority, category, marker
                          ,span
                          ,commands);
      tasks.Add(task);

      // show standard error in the build output pane
      if (outputPane != TaskOutputPane.None && description != null) {
        string kind;
        switch (category) {
          case TaskCategory.Error: kind = "error "; break;
          case TaskCategory.Warning: kind = "warning "; break;
          case TaskCategory.Message: kind = "message "; break;
          default: kind = ""; break;
        }
        OutputString(span + ": " + kind + (code != null ? code : "") + ": " + description + "\n", outputPane);
      }
    }

    public void OutputString(string message, Guid outputPane )
    {
      if (message != null) {
        IVsOutputWindowPane pane = GetOutputPane(ref outputPane);
        if (pane != null) {
          pane.OutputString(message);
        }
      }
    }

    public void Refresh()
    {
      TaskListRefresh();
    }

    public void ClearTasks()
    {
      if (OnClearTasks != null) {
        OnClearTasks(this, "");
      }
      foreach (Task t in tasks) {
        if (t != null) {
          t.OnDeleteTask();
        }
      }
      tasks.Clear();
      Refresh();
    }

    public void ClearTasksOnSource(string projectName /* can be null */, string fileName)
    {
      if (fileName != null && fileName.Length > 0) {
        IVsHierarchy project = Common.GetProjectByName(providerName);
        foreach (Task t in tasks) {
          if (t != null && t.IsSameFile(project, fileName)) {
            t.OnDeleteTask();
          }
        }
        tasks.RemoveAll(delegate(Task t) { return (t == null || t.IsSameFile(project, fileName)); });
        Refresh();
      }
    }

    public void ClearTasksOnSourceSpan(string projectName, string fileName
                                      ,int startLine, int startColumn, int endLine, int endColumn)
    {
      if (fileName != null && fileName.Length > 0) {
        Location span = new Location(projectName, fileName, startLine, startColumn, endLine, endColumn);
        foreach (Task t in tasks) {
          if (t != null && t.Overlaps(span)) {
            t.OnDeleteTask();
          }
        }
        tasks.RemoveAll(delegate(Task t) { return (t == null || t.Overlaps(span)); });
        Refresh();
      }
    }

    public void ClearTasksOnProject(string projectName)
    {
      ClearTasksOnHierarchy(Common.GetProjectByName(projectName));
    }

    internal void ClearTasksOnHierarchy(IVsHierarchy hier)
    {
      if (hier != null) {
        if (OnClearTasks != null) {
          OnClearTasks(this, Common.GetProjectName(hier));
        }
        foreach (Task t in tasks) {
          if (t != null && t.IsSameHierarchy(hier)) {
            t.OnDeleteTask();
          }
        }
        tasks.RemoveAll(delegate(Task t) { return (t==null ? false : t.IsSameHierarchy(hier)); });
        Refresh();
      }
    }

    internal void ClearTask(object task)
    {
      if (task != null) {
        int index = tasks.FindIndex(delegate(Task t) { return ((object)t == task); });
        if (index >= 0) {
          Task t = tasks[index];
          t.OnDeleteTask();
          tasks.RemoveAt(index);
          Refresh();
        }
      }
    }

    internal void RefreshTask(object taskObj)
    {
      // Refresh();
      if (taskObj != null) {
        Task task = tasks.Find(delegate(Task t) { return ((object)t == taskObj); });
        if (task != null) {
          TaskListRefreshTask(task);
        }
      }
    }

    #endregion

    #region Output window
    IVsOutputWindow outputWindow; // we cache the output window

    private IVsOutputWindowPane GetOutputPane(ref Guid outputPaneGuid )
    {
      if (outputPaneGuid != Guid.Empty) {
        if (outputWindow != null) {
          IVsOutputWindowPane pane;
          outputWindow.GetPane(ref outputPaneGuid, out pane);
          return pane;
        }
      }
      return null;
    }

    private void InitializeOutputWindow()
    {
      outputWindow = Common.GetService(typeof(IVsOutputWindow)) as IVsOutputWindow;        
    }

    private void DisposeOutputWindow()
    {
      outputWindow = null;
    }

    #endregion

    #region IVsTaskProvider

    private uint taskListCookie;
    private IVsTaskList taskList;

    private void InitializeTaskProvider()
    {
      taskList = Common.GetService(typeof(SVsTaskList)) as IVsTaskList;
      if (taskList != null) {
        taskList.RegisterTaskProvider(this, out taskListCookie);
      }
    }

    private void DisposeTaskProvider()
    {
      ClearTasks();
      OnTaskListFinalRelease(taskList);
    }

    private void TaskListRefresh()
    {
      if (taskList != null && taskListCookie != 0) {
        taskList.RefreshTasks(taskListCookie);
      }
    }

    private void TaskListRefreshTask( Task task)
    {
      if (taskList != null && taskListCookie != 0 && task != null) {
        IVsTaskList2 taskList2 = taskList as IVsTaskList2;
        if (taskList2 != null) {
          IVsTaskItem[] taskItems = { task };
          taskList2.RefreshOrAddTasks(taskListCookie, 1, taskItems);
        }
      }
    }

    public int EnumTaskItems(out IVsEnumTaskItems ppenum)
    {
      // remove user deleted tasks 
      // tasks.RemoveAll( delegate( Task t ) { return (t==null || t.isDeleted); });
      ppenum = new TaskEnumerator(tasks);
      return 0;
    }

    public int ImageList(out IntPtr phImageList)
    {
      phImageList = IntPtr.Zero;
      return VSConstants.E_NOTIMPL;
    }

    public int OnTaskListFinalRelease(IVsTaskList pTaskList)
    {
      if (pTaskList != null) {
        if (taskListCookie != 0) {
          pTaskList.UnregisterTaskProvider(taskListCookie);
          taskListCookie = 0;
          taskList = null;
        }
      }
      return 0;
    }

    public int ReRegistrationKey(out string pbstrKey)
    {
      pbstrKey = null;
      return VSConstants.E_NOTIMPL;
    }

    public int SubcategoryList(uint cbstr, string[] rgbstr, out uint pcActual)
    {
      pcActual = 0;
      if (cbstr != 0 || rgbstr != null) {
        if (rgbstr != null && rgbstr.Length > 0) {
          rgbstr[0] = "Error";
        } 
      }
      return 0;
    }

    #endregion

    #region IVsRunningDocTableEvents Members
    private IVsRunningDocumentTable rdt;
    private uint rdtEventsCookie; // = 0

    private void InitializeRunningDocTableEvents()
    {
      rdt = Common.GetService(typeof(IVsRunningDocumentTable)) as IVsRunningDocumentTable;
      if (rdt != null) {
        rdt.AdviseRunningDocTableEvents(this, out rdtEventsCookie);
      }
    }

    private void DisposeRunningDocTableEvents()
    {
      if (rdt != null && rdtEventsCookie != 0) {
        rdt.UnadviseRunningDocTableEvents(rdtEventsCookie);
        rdtEventsCookie = 0;
      }
      rdt = null;
    }

    public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
    {
      // Common.Trace(string.Format("OnAfterFirstDocumentLock: {0} - {1} - {2} ", dwRDTLockType, dwReadLocksRemaining, dwEditLocksRemaining ));
      if (rdt != null && dwReadLocksRemaining == 1 && dwEditLocksRemaining==1) {
        uint flags;
        uint readLocks;
        uint editLocks;
        string fileName;
        IVsHierarchy hier;
        uint itemId;
        IntPtr docData;
        int hr = rdt.GetDocumentInfo(docCookie, out flags, out readLocks, out editLocks
                                ,out fileName, out hier, out itemId, out docData);
        if (hr == 0 && !docData.Equals(IntPtr.Zero)) {
          IVsTextLines textLines = System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(docData) as IVsTextLines;
          if (textLines != null) {
            foreach (Task t in tasks) {
              t.OnOpenDocument(hier,itemId,fileName,textLines);
            }
          }
        }
      }
      return 0;
    }

    public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
    {
      return 0;
    }

    public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
    {
      return 0;
    }

    public int OnAfterSave(uint docCookie)
    {
      return 0;
    }

    public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
    {
      return 0;
    }

    public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
    {
      return 0;
    }

    #endregion

    #region IVsTaskProvider2

    public int MaintainInitialTaskOrder(out int bMaintainOrder)
    {
      bMaintainOrder = 0;
      return 0;
    }

    #endregion

    #region IVsTaskProvider3 Members

    public int GetColumn(int iColumn, VSTASKCOLUMN[] pColumn)
    {
      return VSConstants.E_NOTIMPL;
    }

    public int GetColumnCount(out int pnColumns)
    {
      pnColumns = 0;
      return VSConstants.E_NOTIMPL;
    }

    public int GetProviderFlags(out uint tpfFlags)
    {
      tpfFlags = 0;
      return VSConstants.E_NOTIMPL;
    }

    public int GetProviderGuid(out Guid pguidProvider)
    {
      pguidProvider = Guid.Empty;
      return VSConstants.E_NOTIMPL;
    }

    public int GetProviderName(out string pbstrName)
    {
      if (providerName != null) {
        pbstrName = providerName;
        return 0;
      }
      else {
        pbstrName = null;
        return VSConstants.E_NOTIMPL;
      }
    }

    public int GetProviderToolbar(out Guid pguidGroup, out uint pdwID)
    {
      pguidGroup = Guid.Empty;
      pdwID = 0;
      return VSConstants.E_NOTIMPL;
    }

    public int GetSurrogateProviderGuid(out Guid pguidProvider)
    {
      pguidProvider = Guid.Empty;
      return VSConstants.E_NOTIMPL;
    }

    public int OnBeginTaskEdit(IVsTaskItem pItem)
    {
      return 0;
    }

    public int OnEndTaskEdit(IVsTaskItem pItem, int fCommitChanges, out int pfAllowChanges)
    {
      pfAllowChanges = 1;
      return VSConstants.E_NOTIMPL;
    }

    #endregion

    #region IServiceProvider Members

    public object GetService(Type serviceType)
    {
      return Common.GetService(serviceType);
    }

    #endregion

    #region IVsTaskManager Members

    public object GetHierarchy(string projectName)
    {
      return Common.GetProjectByName(projectName);
    }
    #endregion
}

  internal class TaskEnumerator : IVsEnumTaskItems
  {
    List<Task> tasks;
    int pos;
    
    public TaskEnumerator(List<Task> tasks)
    {
      this.tasks = tasks;
      // pos = 0;
    }

    public virtual int Clone(out IVsEnumTaskItems ppenum)
    {
      ppenum = new TaskEnumerator(this.tasks);
      return 0;
    }

    public virtual int Next(uint celt, IVsTaskItem[] rgelt, uint[] pceltFetched)
    {
      uint fetched = 0;
      while (pos < tasks.Count && fetched < celt) {
        Task task = tasks[pos];
        if (task != null && task.IsVisible()) {
          if (rgelt != null && rgelt.Length > fetched) {
            rgelt[fetched] = task;
          }
          fetched++;
        }
        pos++;
      }     
      if (pceltFetched != null && pceltFetched.Length > 0) {
        pceltFetched[0] = fetched;
      }
      return (fetched < celt ? VSConstants.S_FALSE : 0);
    }

    public virtual int Reset()
    {
      pos = 0;
      return 0;
    }
    
    public virtual int Skip(uint celt)
    {
      uint skipped = 0;
      while (pos < tasks.Count && skipped < celt) {
        Task task = tasks[pos];
        if (task != null && task.IsVisible()) {
          skipped++;
        }
        pos++;
      }
      return 0;
    }
  }

}
