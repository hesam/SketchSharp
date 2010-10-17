//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
namespace Microsoft.VisualStudio.CodeTools
{
  using System;
  using System.Collections.Generic;
  using Microsoft.Win32;
  using Microsoft.VisualStudio.Shell.Interop;
  
  internal class Tools 
  {
    #region private class Tool

    // An MSBuild target/build task pair  (ie. <RunFxCop> / FxCopCompiler)
    private class TargetTask
    {
      public string target;      // build target name
      public string buildTask;   // build "tool" name

      public TargetTask(string target, string buildTask)
      {
        this.target = target;
        this.buildTask = buildTask;
      }
    }

    // MSBuild tasks associated per project (ie. C# project)
    private class Host
    {
      public Guid projectType;   // vs project type
      public List<TargetTask> targetTasks;  // associated msbuild target/tasks

      public Host(Guid type)
      {
        projectType = type;
        targetTasks = new List<TargetTask>(1);
      }
    }

    // A tool: gets the TaskManager HostObject for certain MSBuild project/target/task triples.
    // ie. FxCop
    private class Tool 
    {
      public string toolName;
      public TaskManager taskManager; // the associated visualstudio tool manager
      public List<Host> hosts;

      public Tool(string name)
      {
        toolName = name;
        // taskManager = null;
        hosts = new List<Host>(1);
      }

      ~Tool()
      {
        Release();
      }

      public void Release()
      {
        if (taskManager != null) {
          taskManager.Release();
          taskManager = null;
          hosts.Clear();
        }
      }
    }
    #endregion

    #region Private fields
    private List<Tool> tools;  // The registered tools.
    #endregion

    #region Construction
    public Tools()
    {
      tools = new List<Tool>(1);
      InitializeTools();        // Get tools from the registry
      InitializeHostObjects();  // and set the host objects for opened projects
    }

    ~Tools()
    {
      Release();
    }
  
    // Read the build tasks from the registry
    private void InitializeTools()
    {
      string vsRoot = Common.GetLocalRegistryRoot();

      // Open the "CodeTools" key
      RegistryKey root = Registry.LocalMachine.OpenSubKey(vsRoot + "\\CodeTools", false);
      if (root != null) {
        String[] toolKeys = root.GetSubKeyNames();
        foreach (string toolName in toolKeys) {
          RegistryKey toolKey = root.OpenSubKey(toolName, false);
          if (toolKey != null) {
            Tool tool = new Tool(toolName);
            string displayName = (string)toolKey.GetValue("displayName");
            if (displayName != null) {
              tool.toolName = displayName;
            }
            Common.Trace("New tool: " + tool.toolName);

            RegistryKey tasksKey = toolKey.OpenSubKey("Tasks",false);
            if (tasksKey!=null) {
              foreach (string projectType in tasksKey.GetSubKeyNames()) {
                Guid projectGuid = new Guid(projectType);
                RegistryKey projectKey = tasksKey.OpenSubKey(projectType, false);
                if (projectKey != null) {  // Daan: projectGuid can be Guid.Empty
                  Host host = new Host(projectGuid);
                  foreach (string target in projectKey.GetSubKeyNames()) {
                    RegistryKey targetKey = projectKey.OpenSubKey(target, false);
                    if (targetKey != null) {
                      foreach (string buildTask in targetKey.GetValueNames()) {
                        if (buildTask != null && buildTask != null) {
                          TargetTask ttask = new TargetTask(target, buildTask);
                          host.targetTasks.Add(ttask);
                          Common.Trace("+ Target: " + target + ", Task: " + buildTask + ", Project: " + projectGuid.ToString());
                        }
                      }
                    }
                  }
                  if (host.targetTasks.Count > 0) {
                    tool.hosts.Add(host);
                  }
                }
              }
            }
            if (tool.hosts.Count > 0) {
              tool.taskManager = new TaskManager(tool.toolName);
              tools.Add(tool);
            }
          }
        }
      }
    }

    // This method is called at initialization once to set host objects
    // for all currently opened projects. We do this since we are not yet
    // registered for solution events at that initialization.
    private void InitializeHostObjects()
    {
      if (tools.Count == 0) return;
      foreach (IVsHierarchy project in Common.GetProjects()) {
        ProjectSetHostObjects(project);
      }
    }
  

    public void Release()
    {
      foreach (Tool tool in tools) {
        tool.Release();
      }
      tools.Clear();
    }

    #endregion

    #region Tasks
    // Get all task managers
    public IEnumerable<TaskManager> TaskManagers()
    {
      foreach (Tool t in tools) {
        if (t.taskManager != null) {
          yield return t.taskManager;
        }
      }
    }

    // Clear all tasks
    public void ClearTasks()
    {
      foreach (TaskManager t in TaskManagers()) {
        t.ClearTasks();
      }
    }

    // Clear all tasks on a specific project
    public void ClearTasksOnHierarchy(IVsHierarchy hier)
    {
      foreach (TaskManager t in TaskManagers()) {
        t.ClearTasksOnHierarchy(hier);
      }
    }

    // Refresh all tasks
    public void RefreshTasks()
    {
      foreach (TaskManager t in TaskManagers()) {
        t.Refresh();
      }
    }

    #endregion

    #region HostObjects

    // This method is called by solution events to set the host
    // object for newly loaded or opened projects.
    public void ProjectSetHostObjects(IVsHierarchy hierarchy)
    {
      if (hierarchy != null && tools.Count > 0) {
        Guid projectType = Common.GetProjectType(hierarchy);
        IVsProjectBuildSystem pbuilder = hierarchy as IVsProjectBuildSystem;
        if (pbuilder != null) {
          foreach (Tool tool in tools) {
            foreach (Host host in tool.hosts) {
              if (host.projectType == Guid.Empty ||
                  projectType == Guid.Empty ||
                  host.projectType == projectType) {
                // set host objects                  
                foreach (TargetTask ttask in host.targetTasks) {
                  Common.Trace(string.Format("Set host object for {3}: {0}-{1}-{2}", ttask.target, ttask.buildTask, host.projectType, Common.GetProjectName(hierarchy)));
                  pbuilder.SetHostObject(ttask.target, ttask.buildTask, tool.taskManager);
                }
              }
            }
          }
        }
      }
    }
  #endregion
  }
}
