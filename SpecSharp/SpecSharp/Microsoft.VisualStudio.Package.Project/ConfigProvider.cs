using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using System.Diagnostics;
using System.Globalization;
using System.Collections;
using System.IO;
using MSBuild = Microsoft.Build.BuildEngine;

/* This file provides a basefunctionallity for IVsCfgProvider2.
   Instead of using the IVsProjectCfgEventsHelper object we have our own little sink and call our own helper methods
   similiar to the interface. But there is no real benefit in inheriting from the interface in the first place. 
   Using the helper object seems to be:  
    a) undocumented
    b) not really wise in the managed world
*/
namespace Microsoft.VisualStudio.Package
{
    /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider"]/*' />
    [CLSCompliant(false), ComVisible(true)]
    public class ConfigProvider : IVsCfgProvider2, IVsProjectCfgProvider
    {
        internal const string configString = " '$(Configuration)' == '{0}' ";

        private ProjectNode Project;
        private EventSinkCollection cfgEventSinks = new EventSinkCollection();

        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.ConfigProvider"]/*' />
        public ConfigProvider(ProjectNode manager)
        {
            this.Project = manager;
        }

        #region IVsProjectCfgProvider methods
        
        public virtual int OpenProjectCfg(string projectCfgCanonicalName, out IVsProjectCfg projectCfg)
        {

            Debug.Assert(projectCfgCanonicalName != null, "Cannot open project configuration for a null configuration");
            
            projectCfg = null;
            
            // Be robust in release
            if (projectCfgCanonicalName == null)
            {
                return NativeMethods.E_INVALIDARG;
            }
            

            Debug.Assert(this.Project != null && this.Project.MSBuildProject != null);

            string[] configs = this.Project.MSBuildProject.GetConditionedPropertyValues("Configuration");


            foreach (string config in configs)
            {
                if (String.Compare(config, projectCfgCanonicalName, true, CultureInfo.CurrentUICulture) == 0)
                {
                    projectCfg = new ProjectConfig(this.Project, config);
                    return NativeMethods.S_OK;
                }
            }
            
            return NativeMethods.E_INVALIDARG;
        }
        
        public virtual int get_UsesIndependentConfigurations(out int usesIndependentConfigurations)
        {            
            usesIndependentConfigurations = 1;
            return NativeMethods.S_OK;
        }
        #endregion
        #region IVsCfgProvider2 methods
        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.AddCfgsOfCfgName"]/*' />
        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="cloneName"></param>
        /// <param name="fPrivate"></param>
        /// <returns></returns>
        public virtual int AddCfgsOfCfgName(string name, string cloneName, int fPrivate)
        {
            // First create the condition that represent the configuration we want to clone
            string condition = String.Format(CultureInfo.InvariantCulture, configString, name).Trim();

            // Get all configs
            MSBuild.BuildPropertyGroupCollection configGroup = this.Project.MSBuildProject.PropertyGroups;
            MSBuild.BuildPropertyGroup configToClone = null;
            if (cloneName != null)
            {
                // Find the configuration to clone
                foreach (MSBuild.BuildPropertyGroup currentConfig in configGroup)
                {
                    // Only care about conditional property groups
                    if (currentConfig.Condition == null || currentConfig.Condition.Length == 0)
                        continue;

                    // Skip if it isn't the group we want
                    if (String.Compare(currentConfig.Condition.Trim(), condition, true, CultureInfo.InvariantCulture) != 0)
                        continue;

                    configToClone = currentConfig;
                }
            }

            MSBuild.BuildPropertyGroup newConfig = null;
            if (configToClone != null)
            {
                // Clone the configuration settings
                newConfig = this.Project.ClonePropertyGroup(configToClone);
            }
            else
            {
                // no source to clone from, lets just create a new empty config
                newConfig = this.Project.MSBuildProject.AddNewPropertyGroup(true);
            }


            // Set the condition that will define the new configuration
            string newCondition = String.Format(CultureInfo.InvariantCulture, configString, name);
            newConfig.Condition = newCondition;

            NotifyOnCfgNameAdded(name);
            return NativeMethods.S_OK;
        }

        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.AddCfgsOfPlatformName"]/*' />
        public virtual int AddCfgsOfPlatformName(string platformName, string clonePlatformName)
        {
            return NativeMethods.E_NOTIMPL;
        }
        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.DeleteCfgsOfCfgName"]/*' />
        public virtual int DeleteCfgsOfCfgName(string name)
        {
            if (name == null)
            {
                Debug.Fail(String.Format(CultureInfo.CurrentUICulture, "Name of the configuration should not be null if you want to delete it from project: {0}", this.Project.MSBuildProject.FullFileName));
                // The configuration " '$(Configuration)' ==  " does not exist, so technically the goal
                // is achieved so return S_OK
                return NativeMethods.S_OK;
            }
            // Verify that this config exist
            string[] configs = this.Project.MSBuildProject.GetConditionedPropertyValues("Configuration");
            foreach (string config in configs)
            {
                if (String.Compare(config, name, true, CultureInfo.InvariantCulture) == 0)
                {
                    // Create condition of config to remove
                    string condition = String.Format(CultureInfo.InvariantCulture, configString, config);
                    this.Project.MSBuildProject.RemoveAllPropertyGroupsByCondition(condition);

                    NotifyOnCfgNameDeleted(name);
                }
            }

            return NativeMethods.S_OK;
        }
        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.DeleteCfgsOfPlatformName"]/*' />
        public virtual int DeleteCfgsOfPlatformName(string platName)
        {
            return NativeMethods.E_NOTIMPL;
        }
        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.GetCfgNames"]/*' />
        /// <summary>
        /// Returns the existing configurations stored in the project file.
        /// </summary>
        public virtual int GetCfgNames(uint celt, string[] names, uint[] actual)
        {
            // get's called twice, once for allocation, then for retrieval            
            int i = 0;

            string[] configList = this.Project.MSBuildProject.GetConditionedPropertyValues("Configuration");

            if (names != null)
            {
                foreach (string config in configList)
                {
                    names[i++] = config;
                    if (i == celt)
                        break;
                }
            }
            else
                i = configList.Length;

            if (actual != null)
            {
                actual[0] = (uint)i;
            }

            return NativeMethods.S_OK;
        }
        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.GetCfgOfName"]/*' />
        public virtual int GetCfgOfName(string name, string platName, out IVsCfg cfg)
        {
            cfg = null;
            cfg = new ProjectConfig(this.Project, name);

            return NativeMethods.S_OK;
        }
        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.GetCfgProviderProperty"]/*' />
        public virtual int GetCfgProviderProperty(int propid, out object var)
        {
            var = false;
            switch ((__VSCFGPROPID)propid)
            {
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
            return NativeMethods.S_OK;
        }
        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.GetCfgs"]/*' />
        public virtual int GetCfgs(uint celt, IVsCfg[] a, uint[] actual, uint[] flags)
        {
            if (flags != null)
                flags[0] = 0;

            int i = 0;
            string[] configList = this.Project.MSBuildProject.GetConditionedPropertyValues("Configuration");

            if (a != null)
            {
                foreach (string configName in configList)
                {
                    a[i] = new ProjectConfig(this.Project, configName);

                    i++;
                    if (i == celt)
                        break;
                }
            }
            else
                i = configList.Length;

            if (actual != null)
                actual[0] = (uint)i;

            return NativeMethods.S_OK;
        }
        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.GetPlatformNames"]/*' />
        public virtual int GetPlatformNames(uint celt, string[] names, uint[] actual)
        {
            if (names != null)
                names[0] = ".NET";

            if (actual != null)
                actual[0] = 1;

            return NativeMethods.S_OK;
        }
        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.GetSupportedPlatformNames"]/*' />
        public virtual int GetSupportedPlatformNames(uint celt, string[] names, uint[] actual)
        {
            if (names != null)
                names[0] = ".NET";

            if (actual != null)
                actual[0] = 1;
            
            return NativeMethods.S_OK;
        }
        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.RenameCfgsOfCfgName"]/*' />
        public virtual int RenameCfgsOfCfgName(string old, string newname)
        {
            // First create the condition that represent the configuration we want to rename
            string condition = String.Format(CultureInfo.InvariantCulture, configString, old).Trim();

            foreach (MSBuild.BuildPropertyGroup config in this.Project.MSBuildProject.PropertyGroups)
            {
                // Only care about conditional property groups
                if (config.Condition == null || config.Condition.Length == 0)
                    continue;

                // Skip if it isn't the group we want
                if (String.Compare(config.Condition.Trim(), condition, true, CultureInfo.InvariantCulture) != 0)
                    continue;

                // Change the name
                config.Condition = String.Format(CultureInfo.InvariantCulture, configString, newname);

                NotifyOnCfgNameRenamed(old, newname);
            }

            return NativeMethods.S_OK;
        }
        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.UnadviseCfgProviderEvents"]/*' />
        public virtual int UnadviseCfgProviderEvents(uint cookie)
        {
            this.cfgEventSinks.RemoveAt(cookie);
            return NativeMethods.S_OK;
        }
        /// <include file='doc\ConfigProvider.uex' path='docs/doc[@for="ConfigProvider.AdviseCfgProviderEvents"]/*' />
        public virtual int AdviseCfgProviderEvents(IVsCfgProviderEvents sink, out uint cookie)
        {
            cookie = this.cfgEventSinks.Add(sink);
            return NativeMethods.S_OK;
        }
        #endregion

        // Called when a new config name was added
        private void NotifyOnCfgNameAdded(string strCfgName)
        {
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
                NativeMethods.ThrowOnFailure(sink.OnCfgNameAdded(strCfgName));
        }

        // Called when a config name was deleted
        private void NotifyOnCfgNameDeleted(string strCfgName)
        {
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
                NativeMethods.ThrowOnFailure(sink.OnCfgNameDeleted(strCfgName));
        }
        // Called when a config name was renamed
        private void NotifyOnCfgNameRenamed(string strOldName, string strNewName)
        {
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
                NativeMethods.ThrowOnFailure(sink.OnCfgNameRenamed(strOldName, strNewName));
        }
        // Called when a platform name was added
        private void NotifyOnPlatformNameAdded(string strPlatformName)
        {
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
                NativeMethods.ThrowOnFailure(sink.OnPlatformNameAdded(strPlatformName));
        }
        // Called when a platform name was deleted
        private void NotifyOnPlatformNameDeleted(string strPlatformName)
        {
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
                NativeMethods.ThrowOnFailure(sink.OnPlatformNameDeleted(strPlatformName));
        }
    }
}