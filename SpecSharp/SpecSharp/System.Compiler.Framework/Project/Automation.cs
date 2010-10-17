using System;
using Microsoft.VisualStudio.Shell.Interop;
using System.Runtime.InteropServices;
using System.Collections;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.Package.Automation {

  [ComVisible(true)]
  [Serializable]
  public class Projects : EnvDTE.Projects, ISerializable {

    VsPackage package;

    public Projects(VsPackage pkg) {
      this.package = pkg;
    }

    // A method called when serializing a Singleton.
    void ISerializable.GetObjectData(
      SerializationInfo info, StreamingContext context) {      
      //info.SetType(typeof(ProjectsSerializationHelper));
      // No other values need to be added.
    }


    public virtual int Count {
      get { return ProjectManager.ProjectList.Count; }
    }
    public virtual IEnumerator GetEnumerator() {
      return ProjectManager.ProjectList.GetEnumerator();
    }
    public virtual EnvDTE.Project Item(object index) {
      int i = (int)index;
      return (Project)ProjectManager.ProjectList[i];      
    }
    public virtual EnvDTE.DTE DTE {
      get { return null; }
    }
    public virtual string Kind {
      get { return this.package.GetType().GUID.ToString(); }
    }
    public virtual EnvDTE.DTE Parent {
      get { return null; }
    }
    public virtual EnvDTE.Properties Properties {
      get { return null; }
    }
  }

  [ComVisible(true)]
  public class Project : EnvDTE.Project {
    ProjectManager project;

    public Project(ProjectManager project) {
      this.project = project;
    }

    public virtual string Name {
      get {
        return project.Caption;
      }
      set {
        project.Caption = value;
      }
    }

    public virtual string FileName {
      get {
        return project.ProjectFile;
      }
    }
       
    public virtual bool IsDirty {
      get {
        int dirty;
        project.IsDirty(out dirty);
        return dirty!=0;
      }
      set {
        project.SetProjectFileDirty(value);
      }
    }

    public virtual EnvDTE.Projects Collection {
      get { return null; }
    }
       
    public virtual EnvDTE.DTE  DTE {
      get { return null; }
    }

    public virtual string Kind {
      get { return project.GetType().GUID.ToString(); }
    }
        
    public virtual EnvDTE.ProjectItems ProjectItems {
      get { return null; }
    }

    public virtual EnvDTE.Properties Properties {
      get { return null; }
    }
        
    public virtual string UniqueName {
      get { return project.Caption; }
    }
        
    public virtual object Object {
      get { return null; }
    }

    public virtual object get_Extender(string name) {
      return null; 
    }

    public virtual object ExtenderNames {
      get { return null; }
    }
        
    public virtual string ExtenderCATID {
      get { return ""; }
    }
        
    public virtual string FullName {
      get { 
        string filename;
        uint format;
        project.GetCurFile(out filename, out format);
        return filename;
      }
    }
        
    public virtual bool Saved {
      get { return ! this.IsDirty; }
      set { project.SetProjectFileDirty(!value); }
    }
       
    public virtual EnvDTE.ConfigurationManager ConfigurationManager {
      get { return null; }
    }
        
    public virtual EnvDTE.Globals Globals {
      get { return null; }
    }     
        
    public virtual void SaveAs(string filename) {
      project.Save(filename, 1, 0);
    }

    public virtual void Save(string filename) {
      project.Save(filename, 0, 0);
    }
        
    public virtual EnvDTE.ProjectItem ParentProjectItem {
     get { return null; }
    }
        
    public virtual EnvDTE.CodeModel CodeModel {
      get { return null; }
    }
        
    public virtual void Delete() {
      this.project.Remove(true);
    }       

	}
}
