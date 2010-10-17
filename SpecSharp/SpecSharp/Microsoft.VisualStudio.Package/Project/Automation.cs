//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;
using System.Collections;
using System.Runtime.Serialization;
using System.Reflection;
using IServiceProvider = System.IServiceProvider;

namespace Microsoft.VisualStudio.Package.Automation {
    /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects"]/*' />
    [ComVisible(true), CLSCompliant(false)]
    [Serializable]
    public class OAProjects : EnvDTE.Projects, ISerializable {
        internal Microsoft.VisualStudio.Shell.Package package;

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects.OAProjects"]/*' />
        public OAProjects(Microsoft.VisualStudio.Shell.Package pkg) {
            this.package = pkg;
        }

        // A method called when serializing a Singleton.
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects.ISerializable.GetObjectData"]/*' />
        /// <internalonly/>
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context) {
            //info.SetType(typeof(ProjectsSerializationHelper));
            // No other values need to be added.
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects.Count"]/*' />
        public virtual int Count {
            get { return Project.ProjectList.Count; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects.GetEnumerator"]/*' />
        public virtual IEnumerator GetEnumerator() {
            return Project.ProjectList.GetEnumerator();
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects.Item"]/*' />
        public virtual EnvDTE.Project Item(object index) {
            int i = (int)index;

            return (OAProject)Project.ProjectList[i];
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects.DTE"]/*' />
        public virtual EnvDTE.DTE DTE {
            get {
                IServiceProvider sp = (IServiceProvider)this.package;
                EnvDTE.DTE dte = (EnvDTE.DTE)sp.GetService(typeof(EnvDTE.DTE));
                return dte;
            }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects.Kind"]/*' />
        public virtual string Kind {
            get { return this.package.GetType().GUID.ToString(); }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects.Parent"]/*' />
        public virtual EnvDTE.DTE Parent {
            get { return null; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects.Properties"]/*' />
        public virtual EnvDTE.Properties Properties {
            get { return null; }
        }
    }

    /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject"]/*' />
    [ComVisible(true),CLSCompliant(false)]
    public class OAProject : EnvDTE.Project {
        Project project;

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.OAProject"]/*' />
        public OAProject(Project project) {
            this.project = project;
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.Name"]/*' />
        public virtual string Name {
            get {
                return project.Caption;
            }
			set
			{
				project.SetEditLabel(value);
			}
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.FileName"]/*' />
        public virtual string FileName {
            get {
                return project.ProjectFile;
            }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.IsDirty"]/*' />
        public virtual bool IsDirty {
            get {
                int dirty;

                project.IsDirty(out dirty);
                return dirty != 0;
            }
            set {
                project.SetProjectFileDirty(value);
            }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.Collection"]/*' />
        public virtual EnvDTE.Projects Collection {
            get { return null; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.DTE"]/*' />
        public virtual EnvDTE.DTE DTE {
            get { 
                return (EnvDTE.DTE)this.project.Site.GetService(typeof(EnvDTE.DTE));
            }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.Kind"]/*' />
        public virtual string Kind {
            get { return project.GetType().GUID.ToString(); }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.ProjectItems"]/*' />
        public virtual EnvDTE.ProjectItems ProjectItems {
            get { return null; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.Properties"]/*' />
        public virtual EnvDTE.Properties Properties {
            get { return null; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.UniqueName"]/*' />
        public virtual string UniqueName {
            get { return project.Caption; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.Object"]/*' />
        public virtual object Object {
            get { return this.project; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.get_Extender"]/*' />
        public virtual object get_Extender(string name) {
            return null;
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.ExtenderNames"]/*' />
        public virtual object ExtenderNames {
            get { return null; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.ExtenderCATID"]/*' />
        public virtual string ExtenderCATID {
            get { return ""; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.FullName"]/*' />
        public virtual string FullName {
            get {
                string filename;
                uint format;

                project.GetCurFile(out filename, out format);
                return filename;
            }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.Saved"]/*' />
        public virtual bool Saved {
            get { return !this.IsDirty; }
            set { project.SetProjectFileDirty(!value); }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.ConfigurationManager"]/*' />
        public virtual EnvDTE.ConfigurationManager ConfigurationManager {
            get { return null; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.Globals"]/*' />
        public virtual EnvDTE.Globals Globals {
            get { return null; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.SaveAs"]/*' />
        public virtual void SaveAs(string filename) {
            project.Save(filename, 1, 0);
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.Save"]/*' />
        public virtual void Save(string filename) {
            project.Save(filename, 0, 0);
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.ParentProjectItem"]/*' />
        public virtual EnvDTE.ProjectItem ParentProjectItem {
            get { return null; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.CodeModel"]/*' />
        public virtual EnvDTE.CodeModel CodeModel {
            get { return null; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.Delete"]/*' />
        public virtual void Delete() {
            this.project.Remove(true);
        }
    }

    /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperties"]/*' />
    [CLSCompliant(false), ComVisible(true)]
    public class OAProperties : EnvDTE.Properties {
        internal object target;
        internal ArrayList properties;

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperties.OAProperties"]/*' />
        public OAProperties(object target) {
            this.target = target;
            properties = new ArrayList();
            foreach (PropertyInfo pi in target.GetType().GetProperties(BindingFlags.Public)) {
                bool visible = true;
                foreach (ComVisibleAttribute cva in pi.GetCustomAttributes(typeof(ComVisibleAttribute), true)) {
                    if (!cva.Value) {
                        visible = false;
                        break;
                    }
                }
                if (visible) {
                    properties.Add(new OAProperty(this, pi));
                }
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperties.Application"]/*' />
        public object Application {
            get { return null; }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperties.Count"]/*' />
        public int Count {
            get { return properties.Count; }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperties.DTE"]/*' />
        public EnvDTE.DTE DTE {
            get {
                return null;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperties.GetEnumerator"]/*' />
        public IEnumerator GetEnumerator() {
            return properties.GetEnumerator();
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperties.Item"]/*' />
        public EnvDTE.Property Item(object index) {
            return (EnvDTE.Property)properties[(int)index];
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperties.Parent"]/*' />
        public object Parent {
            get { return null; }
        }
    }

    /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty"]/*' />
    [CLSCompliant(false), ComVisible(true)]
    public class OAProperty : EnvDTE.Property {
        OAProperties parent;
        PropertyInfo pi;
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.OAProperty"]/*' />
        public OAProperty(OAProperties parent, PropertyInfo pi) {
            this.parent = parent;
            this.pi = pi;
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.Application"]/*' />
        public object Application {
            get { return null; }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.Collection"]/*' />
        public EnvDTE.Properties Collection {
            get {
                //todo: EnvDTE.Property.Collection
                return null;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.DTE"]/*' />
        public EnvDTE.DTE DTE {
            get { return null; }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.get_IndexedValue"]/*' />
        public object get_IndexedValue(object index1, object index2, object index3, object index4) {
            ParameterInfo[] par = pi.GetIndexParameters();
            int len = Math.Min(par.Length, 4);
            if (len == 0) return this.Value;
            object[] index = new object[len];
            Array.Copy(new object[4] { index1, index2, index3, index4 }, index, len);            
            return this.pi.GetValue(this.parent.target, index);
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.let_Value"]/*' />
        public void let_Value(object value) {
            //todo: let_Value
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.Name"]/*' />
        public string Name {
            get { return pi.Name;  }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.NumIndices"]/*' />
        public short NumIndices {
            get { return (short)pi.GetIndexParameters().Length; }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.Object"]/*' />
        public object Object {
            get { return parent.target; }
            set { //???
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.Parent"]/*' />
        public EnvDTE.Properties Parent {
            get { return this.parent; }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.set_IndexedValue"]/*' />
        public void set_IndexedValue(object index1, object index2, object index3, object index4, object value) {
            ParameterInfo[] par = pi.GetIndexParameters();
            int len = Math.Min(par.Length, 4);
            if (len == 0) {
                this.Value = value;
            }  else {
                object[] index = new object[len];
                Array.Copy(new object[4] { index1, index2, index3, index4 }, index, len);
                this.pi.SetValue(this.parent.target, value, index);
            }

        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.Value"]/*' />
        public object Value {
            get { return pi.GetValue(this.parent.target, null);  }
            set { pi.SetValue(this.parent.target, value, null); }
        }       
    }
}
