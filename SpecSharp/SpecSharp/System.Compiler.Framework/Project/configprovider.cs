using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.OLE.Interop;
using System.Xml;
using System.Collections;
using System.IO;

/* This file provides a basefunctionallity for IVsCfgProvider2.
   Instead of using the IVsProjectCfgEventsHelper object we have our own little sink and call our own helper methods
   similiar to the interface. But there is no real benefit in inheriting from the interface in the first place. 
   Using the helper object seems to be:  
    a) undocumented
    b) not really wise in the managed world
*/
namespace Microsoft.VisualStudio.Package{
  public class CfgProvider : IVsCfgProvider2{
    private ProjectManager projectManager;
    private CookieMap cfgEventSinks = new CookieMap();

    public CfgProvider(ProjectManager manager){
      this.projectManager = manager;
    }

    public void AddCfgsOfCfgName(string name, string cloneName, int fPrivate){
      CCITracing.TraceCall();
      XmlElement settings = (XmlElement)this.projectManager.StateElement.SelectSingleNode("Build/Settings");
      XmlElement newNode;

      if (cloneName != null){
        XmlElement toClone = (XmlElement)settings.SelectSingleNode("Config[@Name='"+cloneName+"']");
        newNode = (XmlElement)toClone.CloneNode(true);
        newNode.SetAttribute("Name",name);
      } else {
        newNode = settings.OwnerDocument.CreateElement("Config");
        newNode.SetAttribute("Name",name);
      }
      settings.AppendChild(newNode);
      NotifyOnCfgNameAdded(name);
    }
    public void AddCfgsOfPlatformName(string platformName, string clonePlatformName){
      CCITracing.TraceCall();
      throw new NotImplementedException();
    }
    public void DeleteCfgsOfCfgName(string name) {
      CCITracing.TraceCall();
      XmlElement e = (XmlElement)this.projectManager.StateElement.SelectSingleNode("Build/Settings/Config[@Name='"+name+"']");
      if (e != null) {
        e.ParentNode.RemoveChild(e);
        NotifyOnCfgNameDeleted(name);
      }
    }
    public void DeleteCfgsOfPlatformName(string platName) {
      CCITracing.TraceCall();
      throw new NotImplementedException();
    }
    /// <summary>
    /// Returns the existing configurations stored in the project file.
    /// </summary>
    public void GetCfgNames(uint celt, string[] names, uint[] actual){
      // get's called twice, once for allocation, then for retrieval
      CCITracing.TraceCall();
      int i = 0;
      foreach (XmlElement e in this.projectManager.StateElement.SelectNodes("Build/Settings/Config")){
        if (names != null) {
          names[i++] = e.GetAttribute("Name");
          if (i == celt)
            break;
        }else{
          // if no names[] was passed in, this is used for counting the array size for the caller
          i++;
        }
      }
      actual[0] = (uint)i;
    }
    public void GetCfgOfName(string name, string platName, out IVsCfg cfg) { 
      CCITracing.TraceCall();
      cfg = null;
      foreach (XmlElement e in this.projectManager.StateElement.SelectNodes("Build/Settings/Config")) {
        if (name == e.GetAttribute("Name")) {
          cfg = new ProjectConfig(this.projectManager, e);
          break;
        }
      }
    }
    public void GetCfgProviderProperty(int propid, out object var) {
      CCITracing.TraceCall();
      var = false;
      switch ((__VSCFGPROPID)propid) {
        case __VSCFGPROPID.VSCFGPROPID_SupportsCfgAdd:
          var = true;
          break;
        case __VSCFGPROPID.VSCFGPROPID_SupportsCfgDelete:
          var = true;
          break;
        case __VSCFGPROPID.VSCFGPROPID_SupportsCfgRename:
          var = true;
          break;
        case __VSCFGPROPID.VSCFGPROPID_SupportsPlatformAdd:
          var = false;
          break;
        case __VSCFGPROPID.VSCFGPROPID_SupportsPlatformDelete:
          var = false;
          break;
      }      
    }
    public void GetCfgs(uint celt, IVsCfg[] a, uint[] actual, uint[] flags) {
      CCITracing.TraceCall();
      if (flags != null) flags[0] = 0;
      int i = 0;
      foreach (XmlElement e in this.projectManager.StateElement.SelectNodes("Build/Settings/Config")) {
        if (a != null) 
          a[i] = new ProjectConfig(this.projectManager, e); 
        i++; 
        if (i == celt)
          break;
      }
      if (actual != null) actual[0] = (uint)i;
    }
    public void GetPlatformNames(uint celt, string[] names, uint[] actual) {
      CCITracing.TraceCall();
      if (names != null) names[0] = ".NET";
      actual[0] = 1;
    }
    public void GetSupportedPlatformNames(uint celt, string[] names, uint[] actual) {
      CCITracing.TraceCall();
      if (names != null) names[0] = ".NET";
      actual[0] = 1;
    }
    public void RenameCfgsOfCfgName(string old, string newname) {
      CCITracing.TraceCall();
      XmlElement e = (XmlElement)this.projectManager.StateElement.SelectSingleNode("Build/Settings/Config[@Name='"+old+"']");
      if (e != null) {
        e.SetAttribute("Name", newname);
        NotifyOnCfgNameRenamed(old, newname);
      }    
    }
    public void UnadviseCfgProviderEvents(uint cookie) {
      CCITracing.TraceCall();
      this.cfgEventSinks.RemoveAt(cookie);
    }
    public void AdviseCfgProviderEvents(IVsCfgProviderEvents sink, out uint cookie) {
      CCITracing.TraceCall();
      cookie = this.cfgEventSinks.Add(sink);      
    }

    // Called when a new config name was added
    void NotifyOnCfgNameAdded(string strCfgName){
      CCITracing.TraceCall();
      foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
        sink.OnCfgNameAdded(strCfgName);
    }
    // Called when a config name was deleted
    void NotifyOnCfgNameDeleted(string strCfgName){
      CCITracing.TraceCall();
      foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
        sink.OnCfgNameDeleted(strCfgName);
    }
    // Called when a config name was renamed
    void NotifyOnCfgNameRenamed(string strOldName, string strNewName){
      CCITracing.TraceCall();
      foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
        sink.OnCfgNameRenamed(strOldName, strNewName);
    }
    // Called when a platform name was added
    void NotifyOnPlatformNameAdded(string strPlatformName){
      CCITracing.TraceCall();
      foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
        sink.OnPlatformNameAdded(strPlatformName); 
    }
    // Called when a platform name was deleted
    void NotifyOnPlatformNameDeleted(string strPlatformName){
      CCITracing.TraceCall();
      foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
        sink.OnPlatformNameDeleted(strPlatformName); 
    }
  }
}
