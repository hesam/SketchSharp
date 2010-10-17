using System;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;
using System.Collections;
using System.Runtime.Serialization;
using System.Reflection;
using IServiceProvider = System.IServiceProvider;

namespace Microsoft.VisualStudio.Package.Automation
{
    /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects"]/*' />
    [ComVisible(true), CLSCompliant(false)]
    [Serializable]
    public class OAProjects : EnvDTE.Projects, ISerializable
    {
        internal Microsoft.VisualStudio.Shell.Package package;

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects.OAProjects"]/*' />
        public OAProjects(Microsoft.VisualStudio.Shell.Package pkg)
        {
            this.package = pkg;
        }

        protected OAProjects(SerializationInfo info, StreamingContext context)
        {
            ((ISerializable)this).GetObjectData(info, context);
        }

        // A method called when serializing a Singleton.
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects.ISerializable.GetObjectData"]/*' />
        /// <internalonly/>
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            //info.SetType(typeof(ProjectsSerializationHelper));
            // No other values need to be added.
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects.Count"]/*' />
        public virtual int Count
        {
            get { return ProjectNode.ProjectList.Count; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects.GetEnumerator"]/*' />
        public virtual IEnumerator GetEnumerator()
        {
            return ProjectNode.ProjectList.GetEnumerator();
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects.Item"]/*' />
        public virtual EnvDTE.Project Item(object index)
        {
            int i = (int)index;

            return (OAProject)ProjectNode.ProjectList[i];
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects.DTE"]/*' />
        public virtual EnvDTE.DTE DTE
        {
            get
            {
                IServiceProvider sp = (IServiceProvider)this.package;
                EnvDTE.DTE dte = (EnvDTE.DTE)sp.GetService(typeof(EnvDTE.DTE));
                return dte;
            }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects.Kind"]/*' />
        public virtual string Kind
        {
            get { return this.package.GetType().GUID.ToString(); }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects.Parent"]/*' />
        public virtual EnvDTE.DTE Parent
        {
            get { return null; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjects.Properties"]/*' />
        public virtual EnvDTE.Properties Properties
        {
            get { return null; }
        }
    }

    /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject"]/*' />
    [ComVisible(true), CLSCompliant(false)]
    public class OAProject : EnvDTE.Project
    {
        internal ProjectNode project;
        internal OAProjectItems items;

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.OAProject"]/*' />
        public OAProject(ProjectNode project)
        {
            this.project = project;
            ArrayList list = new ArrayList();
            for (HierarchyNode child = project.FirstChild; child != null; child = child.NextSibling)
            {
                list.Add(new OAProjectItem(this, child));
            }
            this.items = new OAProjectItems(this, list, project);
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.Name"]/*' />
        public virtual string Name
        {
            get
            {
                return project.Caption;
            }
            set
            {
                project.SetEditLabel(value);
            }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.FileName"]/*' />
        public virtual string FileName
        {
            get
            {
                return project.ProjectFile;
            }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.IsDirty"]/*' />
        public virtual bool IsDirty
        {
            get
            {
                int dirty;

                NativeMethods.ThrowOnFailure(project.IsDirty(out dirty));
                return dirty != 0;
            }
            set
            {
                project.SetProjectFileDirty(value);
            }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.Collection"]/*' />
        public virtual EnvDTE.Projects Collection
        {
            get { return null; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.DTE"]/*' />
        public virtual EnvDTE.DTE DTE
        {
            get
            {
                return (EnvDTE.DTE)this.project.Site.GetService(typeof(EnvDTE.DTE));
            }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.Kind"]/*' />
        public virtual string Kind
        {
            get { return project.GetType().GUID.ToString(); }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.ProjectItems"]/*' />
        public virtual EnvDTE.ProjectItems ProjectItems
        {
            get { return this.items; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.Properties"]/*' />
        public virtual EnvDTE.Properties Properties
        {
            get { return null; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.UniqueName"]/*' />
        public virtual string UniqueName
        {
            get { return project.Caption; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.Object"]/*' />
        public virtual object Object
        {
            get { return this.project; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.get_Extender"]/*' />
        public virtual object get_Extender(string name)
        {
            EnvDTE.ObjectExtenders extenderManager = (EnvDTE.ObjectExtenders)this.project.Site.GetService(typeof(EnvDTE.ObjectExtenders));
            if (extenderManager == null)
                throw new Exception(SR.GetString(SR.FailedToGetService));

            return extenderManager.GetExtender(this.ExtenderCATID, name, this);
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.ExtenderNames"]/*' />
        public virtual object ExtenderNames
        {
            get
            {
                EnvDTE.ObjectExtenders extenderManager = (EnvDTE.ObjectExtenders)this.project.Site.GetService(typeof(EnvDTE.ObjectExtenders));
                if (extenderManager == null)
                    throw new Exception(SR.GetString(SR.FailedToGetService));

                return extenderManager.GetExtenderNames(this.ExtenderCATID, this);
            }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.ExtenderCATID"]/*' />
        public virtual string ExtenderCATID
        {
            get
            {
                return this.project.GetProjectGuid().ToString("B");
            }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.FullName"]/*' />
        public virtual string FullName
        {
            get
            {
                string filename;
                uint format;
                NativeMethods.ThrowOnFailure(project.GetCurFile(out filename, out format));
                return filename;
            }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.Saved"]/*' />
        public virtual bool Saved
        {
            get { return !this.IsDirty; }
            set { project.SetProjectFileDirty(!value); }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.ConfigurationManager"]/*' />
        public virtual EnvDTE.ConfigurationManager ConfigurationManager
        {
            get { return null; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.Globals"]/*' />
        public virtual EnvDTE.Globals Globals
        {
            get { return null; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.SaveAs"]/*' />
        public virtual void SaveAs(string filename)
        {
            NativeMethods.ThrowOnFailure(project.Save(filename, 1, 0));
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.Save"]/*' />
        public virtual void Save(string filename)
        {
            NativeMethods.ThrowOnFailure(project.Save(filename, 0, 0));
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.ParentProjectItem"]/*' />
        public virtual EnvDTE.ProjectItem ParentProjectItem
        {
            get { return null; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.CodeModel"]/*' />
        public virtual EnvDTE.CodeModel CodeModel
        {
            get { return null; }
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProject.Delete"]/*' />
        public virtual void Delete()
        {
            this.project.Remove(true);
        }
    }

    /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem"]/*' />
    [ComVisible(true), CLSCompliant(false)]
    public class OAProjectItem : EnvDTE.ProjectItem
    {
        HierarchyNode node;
        OAProject project;
        OAProjectItems items;

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.OAProjectItem"]/*' />
        public OAProjectItem(OAProject proj, HierarchyNode node)
        {
            this.node = node;
            this.project = proj;
            ArrayList list = new ArrayList();
            for (HierarchyNode child = node.FirstChild; child != null; child = child.NextSibling)
            {
                list.Add(new OAProjectItem(this.project, child));
            }
            this.items = new OAProjectItems(this.project, list, node);
        }
            #region ProjectItem Members

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.Object"]/*' />
        public virtual object Object
        {
            get
            {
                return node;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.Remove"]/*' />
        public virtual void Remove()
        {
            node.Remove(false);
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.Document"]/*' />
        public virtual EnvDTE.Document Document
        {
            get
            {
                return null;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.FileCount"]/*' />
        public virtual short FileCount
        {
            get
            {
                return this.node is FileNode ? (short)1 : (short)this.items.Count;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.Properties"]/*' />
        public virtual EnvDTE.Properties Properties
        {
            get
            {
                return null;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.Delete"]/*' />
        public virtual void Delete()
        {
            node.Remove(true);
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.FileCodeModel"]/*' />
        public virtual EnvDTE.FileCodeModel FileCodeModel
        {
            get
            {
                return null;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.ProjectItems"]/*' />
        public virtual EnvDTE.ProjectItems ProjectItems
        {
            get
            {
                return items;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.Kind"]/*' />
        public virtual string Kind
        {
            get
            {
                return null;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.Save"]/*' />
        public virtual void Save(string FileName)
        {
            // TODO:  Add OAProjectItem.Save implementation
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.DTE"]/*' />
        public virtual EnvDTE.DTE DTE
        {
            get
            {
                return (EnvDTE.DTE)this.project.DTE;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.Collection"]/*' />
        public virtual EnvDTE.ProjectItems Collection
        {
            get
            {
                return items;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.ContainingProject"]/*' />
        public virtual EnvDTE.Project ContainingProject
        {
            get
            {
                return this.project;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.Saved"]/*' />
        public virtual bool Saved
        {
            get
            {
                // TODO:  Add OAProjectItem.Saved getter implementation
                return false;
            }
            set
            {
                // TODO:  Add OAProjectItem.Saved setter implementation
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.SaveAs"]/*' />
        public virtual bool SaveAs(string NewFileName)
        {
            // TODO:  Add OAProjectItem.SaveAs implementation
            return false;
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.get_IsOpen"]/*' />
        public virtual bool get_IsOpen(string ViewKind)
        {
            // TODO:  Add OAProjectItem.get_IsOpen implementation
            return false;
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.ConfigurationManager"]/*' />
        public virtual EnvDTE.ConfigurationManager ConfigurationManager
        {
            get
            {
                // TODO:  Add OAProjectItem.ConfigurationManager getter implementation
                return null;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.get_FileNames"]/*' />
        public virtual string get_FileNames(short index)
        {
            if (index < 1)
                throw new ArgumentOutOfRangeException("index");

            HierarchyNode node = null;
            if (this.node is FileNode)
            {
                node = this.node;
            }
            else
            {
                int i = index - 1; // index is 1-based        
                OAProjectItem item = (OAProjectItem)this.items.items[i];
                node = item.node;
            }
            if (node is FileNode)
            {
                FileNode file = (FileNode)node;
                return file.FileName;
            }
            return null;
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.ExpandView"]/*' />
        public virtual void ExpandView()
        {
            // TODO:  Add OAProjectItem.ExpandView implementation
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.get_Extender"]/*' />
        public virtual object get_Extender(string ExtenderName)
        {
            EnvDTE.ObjectExtenders extenderManager = (EnvDTE.ObjectExtenders)this.project.project.Site.GetService(typeof(EnvDTE.ObjectExtenders));
            if (extenderManager == null)
                throw new Exception(SR.GetString(SR.FailedToGetService));

            return extenderManager.GetExtender(this.ExtenderCATID, ExtenderName, this);
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.ExtenderNames"]/*' />
        public virtual object ExtenderNames
        {
            get
            {
                EnvDTE.ObjectExtenders extenderManager = (EnvDTE.ObjectExtenders)this.project.project.Site.GetService(typeof(EnvDTE.ObjectExtenders));
                if (extenderManager == null)
                    throw new Exception(SR.GetString(SR.FailedToGetService));

                return extenderManager.GetExtenderNames(this.ExtenderCATID, this);
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.ExtenderCATID"]/*' />
        public virtual string ExtenderCATID
        {
            get
            {
                return node.GetType().GUID.ToString("B");
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.Open"]/*' />
        public virtual EnvDTE.Window Open(string ViewKind)
        {
            // TODO:  Add OAProjectItem.Open implementation
            return null;
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.Name"]/*' />
        public virtual string Name
        {
            get
            {
                return node.Caption;
            }
            set
            {
                node.SetEditLabel(value);
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.SubProject"]/*' />
        public virtual EnvDTE.Project SubProject
        {
            get
            {
                // TODO:  Add OAProjectItem.SubProject getter implementation
                return null;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItem.IsDirty"]/*' />
        public virtual bool IsDirty
        {
            get
            {
                // TODO:  Add OAProjectItem.IsDirty getter implementation
                return false;
            }
            set
            {
                // TODO:  Add OAProjectItem.IsDirty setter implementation
            }
        }

            #endregion
    }
    /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItems"]/*' />
    [ComVisible(true), CLSCompliant(false)]
    public class OAProjectItems : EnvDTE.ProjectItems
    {
        internal OAProject project;
        internal ArrayList items;
        internal HierarchyNode node;

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItems.OAProjectItems"]/*' />
        public OAProjectItems(OAProject proj, ArrayList items, HierarchyNode node)
        {
            this.items = items;
            this.project = proj;
            this.node = node;
        }

            #region ProjectItems Members

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItems.AddFromDirectory"]/*' />
        public virtual EnvDTE.ProjectItem AddFromDirectory(string Directory)
        {
            // TODO:  Add OAProjectItems.AddFromDirectory implementation
            return null;
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItems.AddFromTemplate"]/*' />
        public virtual EnvDTE.ProjectItem AddFromTemplate(string FileName, string Name)
        {
            // TODO:  Add OAProjectItems.AddFromTemplate implementation
            return null;
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItems.Item"]/*' />
        public virtual EnvDTE.ProjectItem Item(object index)
        {
            if (index is int)
            {
                return (EnvDTE.ProjectItem)items[(int)index];
            }
            else if (index is string)
            {
                string name = (string)index;
                foreach (OAProjectItem item in items)
                {
                    if (NativeMethods.IsSamePath(item.Name, name))
                        return item;
                }
            }
            return null;
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItems.Count"]/*' />
        public virtual int Count
        {
            get
            {
                return items.Count;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItems.Parent"]/*' />
        public virtual object Parent
        {
            get
            {
                return project;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItems.Kind"]/*' />
        public virtual string Kind
        {
            get
            {
                // TODO:  Add OAProjectItems.Kind getter implementation
                return null;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItems.DTE"]/*' />
        public virtual EnvDTE.DTE DTE
        {
            get
            {
                return (EnvDTE.DTE)this.project.DTE;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItems.GetEnumerator"]/*' />
        public virtual IEnumerator GetEnumerator()
        {
            return items.GetEnumerator();
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItems.ContainingProject"]/*' />
        public virtual EnvDTE.Project ContainingProject
        {
            get
            {
                return this.project;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItems.AddFolder"]/*' />
        public virtual EnvDTE.ProjectItem AddFolder(string Name, string Kind)
        {
            // TODO:  Add OAProjectItems.AddFolder implementation
            return null;
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItems.AddFromFileCopy"]/*' />
        public virtual EnvDTE.ProjectItem AddFromFileCopy(string FilePath)
        {
            return AddItem(FilePath, VSADDITEMOPERATION.VSADDITEMOP_OPENFILE);
        }

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProjectItems.AddFromFile"]/*' />
        public virtual EnvDTE.ProjectItem AddFromFile(string FileName)
        {
            // todo: VSADDITEMOP_LINKTOFILE
            return AddItem(FileName, VSADDITEMOPERATION.VSADDITEMOP_OPENFILE);
        }
            #endregion

        EnvDTE.ProjectItem AddItem(string path, VSADDITEMOPERATION op)
        {
            ProjectNode proj = this.project.project;
            VSADDRESULT[] result = new VSADDRESULT[1];
            NativeMethods.ThrowOnFailure(proj.AddItem(node.ID, op, path, 0, new string[1] { path }, IntPtr.Zero, null));
            if (result[0] == VSADDRESULT.ADDRESULT_Success)
            {
                HierarchyNode child = node.LastChild;
                EnvDTE.ProjectItem item = new OAProjectItem(this.project, child);
                this.items.Add(item);
                return item;
            }
            return null;
        }
    }

    /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperties"]/*' />
    [CLSCompliant(false), ComVisible(true)]
    public class OAProperties : EnvDTE.Properties
    {
        internal object target;
        internal ArrayList properties;

        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperties.OAProperties"]/*' />
        public OAProperties(object target)
        {
            this.target = target;
            properties = new ArrayList();
            foreach (PropertyInfo pi in target.GetType().GetProperties(BindingFlags.Public))
            {
                bool visible = true;
                foreach (ComVisibleAttribute cva in pi.GetCustomAttributes(typeof(ComVisibleAttribute), true))
                {
                    if (!cva.Value)
                    {
                        visible = false;
                        break;
                    }
                }
                if (visible)
                {
                    properties.Add(new OAProperty(this, pi));
                }
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperties.Application"]/*' />
        public virtual object Application
        {
            get { return null; }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperties.Count"]/*' />
        public virtual int Count
        {
            get { return properties.Count; }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperties.DTE"]/*' />
        public virtual EnvDTE.DTE DTE
        {
            get
            {
                return (EnvDTE.DTE)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE));
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperties.GetEnumerator"]/*' />
        public virtual IEnumerator GetEnumerator()
        {
            return properties.GetEnumerator();
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperties.Item"]/*' />
        public virtual EnvDTE.Property Item(object index)
        {
            return (EnvDTE.Property)properties[(int)index];
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperties.Parent"]/*' />
        public virtual object Parent
        {
            get { return null; }
        }
    }

    /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty"]/*' />
    [CLSCompliant(false), ComVisible(true)]
    public class OAProperty : EnvDTE.Property
    {
        OAProperties parent;
        PropertyInfo pi;
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.OAProperty"]/*' />
        public OAProperty(OAProperties parent, PropertyInfo pi)
        {
            this.parent = parent;
            this.pi = pi;
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.Application"]/*' />
        public virtual object Application
        {
            get { return null; }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.Collection"]/*' />
        public virtual EnvDTE.Properties Collection
        {
            get
            {
                //todo: EnvDTE.Property.Collection
                return null;
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.DTE"]/*' />
        public virtual EnvDTE.DTE DTE
        {
            get { return null; }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.get_IndexedValue"]/*' />
        public virtual object get_IndexedValue(object index1, object index2, object index3, object index4)
        {
            ParameterInfo[] par = pi.GetIndexParameters();
            int len = Math.Min(par.Length, 4);
            if (len == 0) return this.Value;
            object[] index = new object[len];
            Array.Copy(new object[4] { index1, index2, index3, index4 }, index, len);
            return this.pi.GetValue(this.parent.target, index);
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.let_Value"]/*' />
        public virtual void let_Value(object value)
        {
            //todo: let_Value
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.Name"]/*' />
        public virtual string Name
        {
            get { return pi.Name; }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.NumIndices"]/*' />
        public virtual short NumIndices
        {
            get { return (short)pi.GetIndexParameters().Length; }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.Object"]/*' />
        public virtual object Object
        {
            get { return parent.target; }
            set
            { //???
            }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.Parent"]/*' />
        public virtual EnvDTE.Properties Parent
        {
            get { return this.parent; }
        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.set_IndexedValue"]/*' />
        public virtual void set_IndexedValue(object index1, object index2, object index3, object index4, object value)
        {
            ParameterInfo[] par = pi.GetIndexParameters();
            int len = Math.Min(par.Length, 4);
            if (len == 0)
            {
                this.Value = value;
            }
            else
            {
                object[] index = new object[len];
                Array.Copy(new object[4] { index1, index2, index3, index4 }, index, len);
                this.pi.SetValue(this.parent.target, value, index);
            }

        }
        /// <include file='doc\Automation.uex' path='docs/doc[@for="OAProperty.Value"]/*' />
        public virtual object Value
        {
            get { return pi.GetValue(this.parent.target, null); }
            set { pi.SetValue(this.parent.target, value, null); }
        }
    }
}
