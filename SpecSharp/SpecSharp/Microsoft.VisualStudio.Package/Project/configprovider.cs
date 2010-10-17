//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
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
    /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider"]/*' />
    [CLSCompliant(false)]
    public class ConfigProvider : IVsCfgProvider2{
        private Project Project;

        private CookieMap cfgEventSinks = new CookieMap();

        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.ConfigProvider"]/*' />
        public ConfigProvider(Project manager){
            this.Project = manager;
        }

        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.AddCfgsOfCfgName"]/*' />
        public int AddCfgsOfCfgName(string name, string cloneName, int fPrivate){
          XmlElement settings = (XmlElement)this.Project.StateElement.SelectSingleNode("Build/Settings");
          XmlElement newNode;
          if (cloneName != null){
            XmlElement toClone = (XmlElement)settings.SelectSingleNode("Config[@Name='" + cloneName + "']");

            newNode = (XmlElement)toClone.CloneNode(true);
            newNode.SetAttribute("Name", name);
          }else{
            newNode = settings.OwnerDocument.CreateElement("Config");
            newNode.SetAttribute("Name", name);
          }
          settings.AppendChild(newNode);
          NotifyOnCfgNameAdded(name);
          return 0;
        }

        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.AddCfgsOfPlatformName"]/*' />
        public int AddCfgsOfPlatformName(string platformName, string clonePlatformName){
          return (int)HResult.E_NOTIMPL;
        }

        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.DeleteCfgsOfCfgName"]/*' />
        public int DeleteCfgsOfCfgName(string name){
          XmlElement e = (XmlElement)this.Project.StateElement.SelectSingleNode("Build/Settings/Config[@Name='" + name + "']");
          if (e != null){
            e.ParentNode.RemoveChild(e);
            NotifyOnCfgNameDeleted(name);
          }
          return 0;
        }

        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.DeleteCfgsOfPlatformName"]/*' />
        public int DeleteCfgsOfPlatformName(string platName){
          return (int)HResult.E_NOTIMPL;
        }

        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.GetCfgNames"]/*' />
        /// <summary>
        /// Returns the existing configurations stored in the project file.
        /// </summary>
        public int GetCfgNames(uint celt, string[] names, uint[] actual){
          // get's called twice, once for allocation, then for retrieval
          int i = 0;
          foreach (XmlElement e in this.Project.StateElement.SelectNodes("Build/Settings/Config")){
            if (names != null){
              names[i++] = e.GetAttribute("Name");
              if (i == celt)
                  break;
            }else{
              // if no names[] was passed in, this is used for counting the array size for the caller
              i++;
            }
          }
          actual[0] = (uint)i;
          return 0;
        }

        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.GetCfgOfName"]/*' />
        public int GetCfgOfName(string name, string platName, out IVsCfg cfg){
          cfg = null;
          foreach (XmlElement e in this.Project.StateElement.SelectNodes("Build/Settings/Config")){
            if (name == e.GetAttribute("Name")){
              cfg = new ProjectConfig(this.Project, e);
              break;
            }
          }
          return 0;
        }

        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.GetCfgProviderProperty"]/*' />
        public int GetCfgProviderProperty(int propid, out object var){
          var = false;
          switch ((__VSCFGPROPID)propid){
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
          return 0;
        }

        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.GetCfgs"]/*' />
        public int GetCfgs(uint celt, IVsCfg[] a, uint[] actual, uint[] flags){
          if (flags != null) flags[0] = 0;
          int i = 0;
          foreach (XmlElement e in this.Project.StateElement.SelectNodes("Build/Settings/Config")){
              if (a != null)
                  a[i] = new ProjectConfig(this.Project, e);
              i++;
              if (i == celt)
                  break;
          }
          if (actual != null) actual[0] = (uint)i;
          return 0;
        }

        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.GetPlatformNames"]/*' />
        public int GetPlatformNames(uint celt, string[] names, uint[] actual){
          if (names != null) names[0] = ".NET";
          actual[0] = 1;
          return 0;
        }

        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.GetSupportedPlatformNames"]/*' />
        public int GetSupportedPlatformNames(uint celt, string[] names, uint[] actual){
          if (names != null) names[0] = ".NET";
          actual[0] = 1;
          return 0;
        }

        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.RenameCfgsOfCfgName"]/*' />
        public int RenameCfgsOfCfgName(string old, string newname){
          XmlElement e = (XmlElement)this.Project.StateElement.SelectSingleNode("Build/Settings/Config[@Name='" + old + "']");
          if (e != null){
            e.SetAttribute("Name", newname);
            NotifyOnCfgNameRenamed(old, newname);
          }
          return 0;
        }

        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.UnadviseCfgProviderEvents"]/*' />
        public int UnadviseCfgProviderEvents(uint cookie){
          this.cfgEventSinks.RemoveAt(cookie);
          return 0;
        }

        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.AdviseCfgProviderEvents"]/*' />
        public int AdviseCfgProviderEvents(IVsCfgProviderEvents sink, out uint cookie){
          cookie = this.cfgEventSinks.Add(sink);
          return 0;
        }

        // Called when a new config name was added
        void NotifyOnCfgNameAdded(string strCfgName){
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
                sink.OnCfgNameAdded(strCfgName);
        }

        // Called when a config name was deleted
        void NotifyOnCfgNameDeleted(string strCfgName){
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
                sink.OnCfgNameDeleted(strCfgName);
        }

        // Called when a config name was renamed
        void NotifyOnCfgNameRenamed(string strOldName, string strNewName){
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
                sink.OnCfgNameRenamed(strOldName, strNewName);
        }

        // Called when a platform name was added
        void NotifyOnPlatformNameAdded(string strPlatformName){
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
                sink.OnPlatformNameAdded(strPlatformName);
        }

        // Called when a platform name was deleted
        void NotifyOnPlatformNameDeleted(string strPlatformName){
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
                sink.OnPlatformNameDeleted(strPlatformName);
        }
    }
}