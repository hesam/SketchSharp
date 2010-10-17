//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Designer.Interfaces;
using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml;
using System.Diagnostics;

namespace Microsoft.VisualStudio.Package{
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="OutputType"]/*' />
    [TypeConverter(typeof(OutputTypeConverter))]
    public enum OutputType{ Library, WinExe, Exe }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DebugMode"]/*' />
    [TypeConverter(typeof(DebugModeConverter))]
    public enum DebugMode{ Project, Program, URL }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildAction"]/*' />
    [TypeConverter(typeof(BuildActionConverter))]
    public enum BuildAction{ None, Compile, Content, EmbeddedResource }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PlatformType"]/*' />
    [TypeConverter(typeof(PlatformTypeConverter))]
    public enum PlatformType{ notSpecified, v1, v11, v12, v2, cli1 }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PropPageStatus"]/*' />
    [Flags]
    public enum PropPageStatus{
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PropPageStatus.Dirty"]/*' />
        Dirty = 0x1,
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PropPageStatus.Validate"]/*' />
        Validate = 0x2,
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PropPageStatus.Clean"]/*' />
        Clean = 0x4
    }
    /// <include file='doc\Nodes.uex' path='docs/doc[@for="ModuleKindFlags"]/*' />
    [Flags]
    public enum ModuleKindFlags{
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ModuleKindFlags.ConsoleApplication"]/*' />
        ConsoleApplication,
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ModuleKindFlags.WindowsApplication"]/*' />
        WindowsApplication,
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ModuleKindFlags.DynamicallyLinkedLibrary"]/*' />
        DynamicallyLinkedLibrary,
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ModuleKindFlags.ManifestResourceFile"]/*' />
        ManifestResourceFile,
        /// <include file='doc\Nodes.uex' path='docs/doc[@for="ModuleKindFlags.UnmanagedDynamicallyLinkedLibrary"]/*' />
        UnmanagedDynamicallyLinkedLibrary
    }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties"]/*' />
    public class LocalizableProperties : ICustomTypeDescriptor{      
        #region ICustomTypeDescriptor
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetAttributes"]/*' />
        /// <summary>
        /// Delegates to TypeDescriptor.
        /// </summary>
        public System.ComponentModel.AttributeCollection GetAttributes() {
            return TypeDescriptor.GetAttributes(this, true);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetDefaultEvent"]/*' />
        /// <summary>
        /// Delegates to TypeDescriptor.
        /// </summary>
        public EventDescriptor GetDefaultEvent() {
            return TypeDescriptor.GetDefaultEvent(this, true);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetDefaultProperty"]/*' />
        /// <summary>
        /// Delegates to TypeDescriptor.
        /// </summary>
        public PropertyDescriptor GetDefaultProperty() {
            return TypeDescriptor.GetDefaultProperty(this, true);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetEditor"]/*' />
        /// <summary>
        /// Retrieves the an editor for this object.
        /// </summary>
        public object GetEditor(Type editorBaseType) {
            return TypeDescriptor.GetEditor(this, editorBaseType, true);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetEvents"]/*' />
        /// <summary>
        /// Delegates to TypeDescriptor.
        /// </summary>
        public EventDescriptorCollection GetEvents() {
            return TypeDescriptor.GetEvents(this, true);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetEvents1"]/*' />
        /// <summary>
        /// Delegates to TypeDescriptor.
        /// </summary>
        public EventDescriptorCollection GetEvents(System.Attribute[] attributes) {
            return TypeDescriptor.GetEvents(this, attributes, true);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetPropertyOwner"]/*' />
        /// <summary>
        /// Returns the browsable object.
        /// </summary>
        public object GetPropertyOwner(PropertyDescriptor pd) {
            return this;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetProperties"]/*' />
        /// <summary>
        /// Returns the properties for selected object using the attribute array as a
        /// filter.
        /// </summary>
        public PropertyDescriptorCollection GetProperties() {
            return GetProperties(null);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetProperties1"]/*' />
        /// <summary>
        /// Returns the properties for selected object using the attribute array as a
        /// filter.
        /// </summary>
        public PropertyDescriptorCollection GetProperties(System.Attribute[] attributes) {
            ArrayList newList = new ArrayList();
            PropertyDescriptorCollection props = TypeDescriptor.GetProperties(this, attributes, true);

            for (int i = 0; i < props.Count; i++)
                newList.Add(new DesignPropertyDescriptor(props[i]));

            return new PropertyDescriptorCollection((PropertyDescriptor[])newList.ToArray(typeof(PropertyDescriptor)));;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetComponentName"]/*' />
        /// <summary>
        /// Get the name of the component.
        /// </summary>
        public string GetComponentName() {
            return TypeDescriptor.GetComponentName(this, true);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetConverter"]/*' />
        /// <summary>
        /// Retrieves the type converter for this object.
        /// </summary>
        public TypeConverter GetConverter() {
            return TypeDescriptor.GetConverter(this, true);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetClassName"]/*' />
        /// <summary>
        /// Delegates to TypeDescriptor.
        /// </summary>
        public virtual string GetClassName() {
            return "CustomObject";
        }

        #endregion ICustomTypeDescriptor
    }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="NodeProperties"]/*' />
    /// <devdoc>
    /// To create your own localizable node properties, subclass this and add public properties
    /// decorated with your own localized display name, category and description attributes.
    /// </devdoc>
    [CLSCompliant(false), ComVisible(true)]
    public class NodeProperties : LocalizableProperties, ISpecifyPropertyPages, IVsGetCfgProvider{
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="NodeProperties.Node;"]/*' />
        public HierarchyNode Node;
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="NodeProperties.project;"]/*' />
        public Project project;
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="NodeProperties.NodeProperties"]/*' />
        public NodeProperties(HierarchyNode node){
            this.Node = node;
            project = node.ProjectMgr;
        }

        #region ISpecifyPropertyPages methods
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="NodeProperties.GetPages"]/*' />
        public virtual void GetPages(CAUUID[] ppages) {
            ppages[0] = new CAUUID();
            if (Node.ProjectMgr != null) {
                Guid[] guids = Node.ProjectMgr.GetPropertyPageGuids();

                if (guids != null) {
                    ppages[0].cElems = (uint)guids.Length;

                    int size = Marshal.SizeOf(typeof(Guid));

                    ppages[0].pElems = Marshal.AllocCoTaskMem(guids.Length * size);

                    IntPtr ptr = ppages[0].pElems;

                    for (int i = 0; i < guids.Length; i++) {
                        Marshal.StructureToPtr(guids[i], ptr, false);
                        ptr = new IntPtr(ptr.ToInt64() + size);
                    }
                }
            } else {
                ppages[0].cElems = 0;
            }
        }
        #endregion 

        #region IVsGetCfgProvider methods
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="NodeProperties.GetCfgProvider"]/*' />
        public virtual int GetCfgProvider(out IVsCfgProvider p){
          ((IVsGetCfgProvider)Node.ProjectMgr).GetCfgProvider(out p);
          return 0;
        }
        #endregion 

        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="NodeProperties.GetProperty"]/*' />
        protected string GetProperty(string name, string def){
          string a = Node.XmlNode.GetAttribute(name);
          return (a == null) ? def : a;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="NodeProperties.SetProperty"]/*' />
        protected void SetProperty(string name, string value){
          Node.XmlNode.SetAttribute(name, value);
        }
    }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor"]/*' />
    /// <summary>
    /// The purpose of DesignPropertyDescriptor is to allow us to customize the
    /// display name of the property in the property grid.  None of the CLR
    /// implementations of PropertyDescriptor allow you to change the DisplayName.
    /// </summary>
    public class DesignPropertyDescriptor : PropertyDescriptor{
        private string displayName; // Custom display name
        private PropertyDescriptor property;	// Base property descriptor
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.DisplayName"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override string DisplayName{
            get{
                return this.displayName;
            }
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.ComponentType"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override Type ComponentType{
            get{
                return this.property.ComponentType;
            }
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.IsReadOnly"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override bool IsReadOnly{
            get{
                return this.property.IsReadOnly;
            }
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.PropertyType"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override Type PropertyType{
            get{
                return this.property.PropertyType;
            }
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.CanResetValue"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override bool CanResetValue(object component){
            return this.property.CanResetValue(component);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.GetValue"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override object GetValue(object component){
            return this.property.GetValue(component);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.ResetValue"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override void ResetValue(object component){
            this.property.ResetValue(component);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.SetValue"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override void SetValue(object component, object value){
            this.property.SetValue(component, value);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.ShouldSerializeValue"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override bool ShouldSerializeValue(object component){
            return this.property.ShouldSerializeValue(component);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.DesignPropertyDescriptor"]/*' />
        /// <summary>
        /// Constructor.  Copy the base property descriptor and also hold a pointer
        /// to it for calling its overridden abstract methods.
        /// </summary>
        public DesignPropertyDescriptor(PropertyDescriptor prop)
      : base(prop){
            this.property = prop;

            Attribute attr = prop.Attributes[typeof(DisplayNameAttribute)];

            if (attr is DisplayNameAttribute){
                this.displayName = ((DisplayNameAttribute)attr).DisplayName;
            } else{
                this.displayName = prop.Name;
            }
        }
    }
  /*
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    internal sealed class LocDisplayNameAttribute : DisplayNameAttribute{
        string name;
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocDisplayNameAttribute.DisplayNameAttribute"]/*' />
        public LocDisplayNameAttribute(string name){
            this.name = name;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocDisplayNameAttribute.DisplayName"]/*' />
        public override string DisplayName{
            get{
                string result = SR.GetString(this.name);
                if (result == null){
                    Debug.Assert(false, "String resource '" + this.name + "' is missing");
                    result = this.name;
                }
                return result;
            }
        }
    }
 */
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FileProperties"]/*' />
  [CLSCompliant(false), ComVisible(true)]
  public class FileProperties : NodeProperties{
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FileProperties.FileProperties"]/*' />
    public FileProperties(HierarchyNode node) : base(node){
    }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FileProperties.GetClassName"]/*' />
    public override string GetClassName(){
      return SR.GetString(SR.FileProperties);
    }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FileProperties.BuildAction"]/*' />
//        [SRCategoryAttribute(SR.Advanced)]
    [DisplayName(SR.BuildAction)]
//        [SRDescriptionAttribute(SR.BuildActionDescription)]
    public BuildAction BuildAction{
      get{
        string value = GetProperty("BuildAction", BuildAction.None.ToString());
        return (BuildAction)Enum.Parse(typeof(BuildAction), value);
      }
      set{
        SetProperty("BuildAction", value.ToString());
      }
    }
    //    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FileProperties.CustomTool"]/*' />
    //    [SRCategoryAttribute(SR.Advanced)]
    //    [LocDisplayName(SR.CustomTool)]
    //    [SRDescriptionAttribute(SR.CustomToolDescription)]
    //    public string CustomTool
    //   {
    //      get
    //     {
    //        return tool;
    //      }
    //      set
    //     {
    //        tool = value;
    //        SetProperty("*/*/Files/@CustomTool", value);
    //      }
    //    }
    //    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FileProperties.CustomToolNamespace"]/*' />
    //    [SRCategoryAttribute(SR.Advanced)]
    //    [LocDisplayName(SR.CustomToolNamespace)]
    //    [SRDescriptionAttribute(SR.CustomToolNamespaceDescription)]
    //    public string CustomToolNamespace
    //   {
    //      get
    //     {
    //        return toolNs;
    //      }
    //      set
    //     {
    //        toolNs = value;
    //        SetProperty("*/*/Files/@CustomToolNamespace", value);
    //      }
    //    }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FileProperties.FileName"]/*' />
//        [SRCategoryAttribute(SR.Misc)]
    [DisplayName(SR.FileName)]
//        [SRDescriptionAttribute(SR.FileNameDescription)]
    public string FileName{
      get{
        return Node.Caption;
      }
      set{
        this.Node.SetEditLabel(value);
      }
    }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FileProperties.FullPath"]/*' />
//        [SRCategoryAttribute(SR.Misc)]
    [DisplayName(SR.FullPath)]
//        [SRDescriptionAttribute(SR.FullPathDescription)]
    public string FullPath{
      get{
        return Node.FullPath;
      }
    }
  }

    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ProjectProperties"]/*' />
  [CLSCompliant(false), ComVisible(true)]
  public class ProjectProperties : NodeProperties{
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ProjectProperties.ProjectProperties"]/*' />
    public ProjectProperties(Project node) : base(node){
      this.project = node;
    }

        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ProjectProperties.GetClassName"]/*' />
    public override string GetClassName(){
      return SR.GetString(SR.ProjectProperties);
    }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ProjectProperties.ProjectFolder"]/*' />
//        [SRCategoryAttribute(SR.Misc)]
    [DisplayName(SR.ProjectFolder)]
//        [SRDescriptionAttribute(SR.ProjectFolderDescription)]
    public string ProjectFolder{
      get{
        return this.project.ProjectFolder;
      }
    }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ProjectProperties.ProjectFile"]/*' />
//        [SRCategoryAttribute(SR.Misc)]
    [DisplayName(SR.ProjectFile)]
//        [SRDescriptionAttribute(SR.ProjectFileDescription)]
    public string ProjectFile{
      get{
        return this.project.ProjectFile;
      }
//      set{
//        this.project.ProjectFile = value;
//      }
    }
  }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FolderProperties"]/*' />
  [CLSCompliant(false), ComVisible(true)]
  public class FolderProperties : NodeProperties{
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FolderProperties.FolderProperties"]/*' />
    public FolderProperties(HierarchyNode node) : base(node){
    }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FolderProperties.GetClassName"]/*' />
    public override string GetClassName(){
      return SR.GetString(SR.FolderProperties);
    }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FolderProperties.FolderName"]/*' />
//        [SRCategoryAttribute(SR.Misc)]
    [DisplayName(SR.FolderName)]
//        [SRDescriptionAttribute(SR.FolderNameDescription)]
    public string FolderName{
      get{
        return this.Node.Caption;
      }
      set{
        this.Node.SetEditLabel(value);
      }
    }
  }

    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ReferenceProperties"]/*' />
  [CLSCompliant(false), ComVisible(true)]
  public class ReferenceProperties : NodeProperties{
 
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ReferenceProperties.ReferenceProperties"]/*' />
    public ReferenceProperties(HierarchyNode node) : base(node){
    }

        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ReferenceProperties.GetClassName"]/*' />
    public override string GetClassName(){
      return SR.GetString(SR.ReferenceProperties);
    }
        
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ReferenceProperties.Name"]/*' />
//        [SRCategoryAttribute(SR.Misc)]
    [DisplayName(SR.RefName)]
//        [SRDescriptionAttribute(SR.RefNameDescription)]
    public string Name{
      get{
        return this.Node.Caption;
      }
    }
 
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ReferenceProperties.CopyToLocal"]/*' />
//        [SRCategoryAttribute(SR.Misc)]
    [DisplayName(SR.CopyToLocal)]
//        [SRDescriptionAttribute(SR.CopyToLocalDescription)]
    public bool CopyToLocal{
      get{
        return bool.Parse(this.GetProperty("Private", "False"));
      }
      set{
        this.SetProperty("Private", value.ToString());
      }
    }
  }

    /////////////////////////////////////////////////////////////////////////////////////////////
    /////////////////////////////////////////////////////////////////////////////////////////////
    ///
    ///   The base class for property pages
    ///
    /////////////////////////////////////////////////////////////////////////////////////////////
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage"]/*' />
    [CLSCompliant(false)]
    public abstract class SettingsPage : LocalizableProperties, IPropertyPage{
        Panel panel;
        // <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.nodes;"]/*' />
        protected NodeProperties[] nodes;
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.project;"]/*' />        
        protected Project project;
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.projectConfigs;"]/*' />
        protected ProjectConfig[] projectConfigs;
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.grid;"]/*' />
        protected IVSMDPropertyGrid grid;
        bool active;
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.strName;"]/*' />
        public string strName;
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.ProjectMgr"]/*' />
        [BrowsableAttribute(false)]
        public Project ProjectMgr{
            get{
                if (this.project != null){
                    return this.project;
                } else if (this.projectConfigs != null){
                    return this.projectConfigs[0].ProjectMgr;
                }

                return null;
            }
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.UpdateObjects"]/*' />
        protected void UpdateObjects(){
            if ((this.nodes != null && this.nodes.Length > 0) || this.projectConfigs != null){
                IntPtr p = Marshal.GetIUnknownForObject(this);
                IntPtr ppUnk = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(IntPtr)));
                try{
                    Marshal.WriteIntPtr(ppUnk, p);
                    BindProperties();
                    // Even bigger BUGBUG: this.grid shouldn't ever be null, but ...
                    if (this.grid != null) {
                      // BUGBUG -- this is really bad casting a pointer to "int"...
                      this.grid.SetSelectedObjects(1, ppUnk.ToInt32());
                      this.grid.Refresh();
                    } else {
                      Debug.Assert(false);
                    }
                } finally{
                    Marshal.FreeCoTaskMem(ppUnk);
                    Marshal.Release(p);
                }
            }
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.BindProperties"]/*' />
        public abstract void BindProperties();
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.ApplyChanges"]/*' />
        public abstract void ApplyChanges();
        private bool dirty = false;
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.IsDirty"]/*' />
        protected bool IsDirty{
            get{
                return this.dirty;
            }
            set{
                if (this.dirty != value){
                    this.dirty = value;
                    if (_site != null)
                        _site.OnStatusChange((uint)(this.dirty ? PropPageStatus.Dirty : PropPageStatus.Clean));
                }
            }
        }
  
        #region IPropertyPage methods.
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.Activate"]/*' />
        public virtual void Activate(IntPtr parent, RECT[] pRect, int bModal) {
            if (this.panel == null) {
                this.panel = new Panel();
                this.panel.Size = new Size(pRect[0].right - pRect[0].left, pRect[0].bottom - pRect[0].top);
                this.panel.Text = "Settings";// TODO localization
                this.panel.Visible = false;
                this.panel.Size = new Size(550, 300);
                this.panel.CreateControl();
                NativeWindowHelper.SetParent(this.panel.Handle, parent);
            }

            Debug.Assert(project != null);
            if (this.grid == null && project != null) {
                IVSMDPropertyBrowser pb = project.Site.GetService(typeof(IVSMDPropertyBrowser)) as IVSMDPropertyBrowser;
                this.grid = pb.CreatePropertyGrid();
            }
            Debug.Assert(this.grid != null);

            this.active = true;

            if (this.grid != null) {
              Control cGrid = Control.FromHandle(new IntPtr(this.grid.Handle));

              cGrid.Parent = Control.FromHandle(parent);//this.panel;
              cGrid.Size = new Size(544, 294);
              cGrid.Location = new Point(3, 3);
              cGrid.Visible = true;
              this.grid.SetOption(_PROPERTYGRIDOPTION.PGOPT_TOOLBAR, false);
              this.grid.GridSort = _PROPERTYGRIDSORT.PGSORT_CATEGORIZED;
              NativeWindowHelper.SetParent(new IntPtr(this.grid.Handle), this.panel.Handle);
              UpdateObjects();
            }
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.Apply"]/*' />
        public virtual int Apply() {
          if (IsDirty) ApplyChanges();
          return 0;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.Deactivate"]/*' />
        public virtual void Deactivate() {
            this.panel.Dispose();
            this.active = false;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.GetPageInfo"]/*' />
        public virtual void GetPageInfo(PROPPAGEINFO[] arrInfo) {
            PROPPAGEINFO info = new PROPPAGEINFO();

            info.cb = (uint)Marshal.SizeOf(typeof(PROPPAGEINFO));
            info.dwHelpContext = 0;
            info.pszDocString = null;
            info.pszHelpFile = null;
            info.pszTitle = this.strName;
            info.SIZE.cx = 550;
            info.SIZE.cy = 300;
            arrInfo[0] = info;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.Help"]/*' />
        public virtual void Help(string helpDir) {
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.IsPageDirty"]/*' />
        public virtual int IsPageDirty() {
            // Note this returns an HRESULT not a Bool.
            return (IsDirty ? (int)HResult.S_OK : (int)HResult.S_FALSE);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.Move"]/*' />
        public virtual void Move(RECT[] arrRect) {
            RECT r = arrRect[0];

            this.panel.Location = new Point(r.left, r.top);
            this.panel.Size = new Size(r.right - r.left, r.bottom - r.top);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.SetObjects"]/*' />
        public virtual void SetObjects(uint count, object[] punk) {
            if (count > 0) {
                // check the kind.
                if (punk[0] is NodeProperties) {
                    this.nodes = new NodeProperties[count];
                    System.Array.Copy(punk, 0, this.nodes, 0, (int)count);
                    if (this.nodes != null && this.nodes.Length > 0) {
                        this.project = this.nodes[0].Node.ProjectMgr;
                    }
                } else if (punk[0] is ProjectConfig) {
                    ArrayList configs = new ArrayList();

                    for (int i = 0; i < count; i++) {
                        ProjectConfig config = (ProjectConfig)punk[i];

                        if (this.project == null) this.project = config.ProjectMgr;

                        configs.Add(config);
                    }

                    this.projectConfigs = (ProjectConfig[])configs.ToArray(typeof(ProjectConfig));
                }
            } else {
                this.nodes = null;
                this.project = null;
            }

            if (this.active) UpdateObjects();
        }
        IPropertyPageSite _site;
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.SetPageSite"]/*' />
        public virtual void SetPageSite(IPropertyPageSite site) {
            _site = site;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.Show"]/*' />
        public virtual void Show(uint cmd) {
            this.panel.Visible = true; // TODO: pass SW_SHOW* flags through      
            this.panel.Show();
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.TranslateAccelerator"]/*' />
        public virtual int TranslateAccelerator(MSG[] arrMsg) {
            MSG msg = arrMsg[0];

            if ((msg.message < NativeWindowHelper.WM_KEYFIRST || msg.message > NativeWindowHelper.WM_KEYLAST) && (msg.message < NativeWindowHelper.WM_MOUSEFIRST || msg.message > NativeWindowHelper.WM_MOUSELAST))
                return 1;

            return (NativeWindowHelper.IsDialogMessageA(this.panel.Handle, ref msg)) ? 0 : 1;
        }
        #endregion 

        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.GetProperty"]/*' />
        public string GetProperty(string path){
            if (this.ProjectMgr != null){
                XmlNode n = this.ProjectMgr.StateElement.SelectSingleNode(path);

                if (n != null){
                    return n.InnerText;
                }
            }

            return String.Empty;
        }
        // relative to active configuration.
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.GetConfigProperty"]/*' />
        public string GetConfigProperty(string path){
            if (this.ProjectMgr != null){
                string unifiedResult = null;

                for (int i = 0, n = this.projectConfigs.Length; i < n; i++){
                    ProjectConfig config = projectConfigs[i];
                    XmlNode node = config.Node.SelectSingleNode(path);

                    if (node != null){
                        string text = node.InnerText.Trim();

                        if (i == 0)
                            unifiedResult = text;
                        else if (unifiedResult != text)
                            return ""; // tristate value is blank then
                    }
                }

                return unifiedResult;
            }

            return String.Empty;
        }
        ///////////////////////////////////////////////////////////////////////////////////
        // the set config property is robust in the sense that they will create the 
        //    attributes if needed.  If value is null it will set empty string.
        ///////////////////////////////////////////////////////////////////////////////////
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.SetConfigProperty"]/*' />
        public void SetConfigProperty(string attrName, string value){
            if (this.ProjectMgr != null){
                for (int i = 0, n = this.projectConfigs.Length; i < n; i++){
                    ProjectConfig config = projectConfigs[i];

                    if (value == null){
                        value = "";
                    }

                    config.Node.SetAttribute(attrName, value);
                }

                this.ProjectMgr.SetProjectFileDirty(true);
            }
        }
    }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="GeneralPropertyPage"]/*' />
  [ComVisible(true), Guid("9864D4AD-569A-4daf-8CBC-548F6E24C111")]
  public class GeneralPropertyPage : SettingsPage{
    private string assemblyName;
    private OutputType outputType;
    private string defaultNamespace;
    private string startupObject;
    private string applicationIcon;
    private string shadowedAssembly;
    private string standardLibraryLocation;
    private PlatformType targetPlatform = PlatformType.v11;
    private string targetPlatformLocation;
    private bool nostdlib;
   
    public GeneralPropertyPage(){
      this.strName = SR.GetString(UIStringNames.GeneralCaption);
    }

    public override string GetClassName(){
      return this.GetType().FullName;
    }
    public override void BindProperties(){
      XmlElement project = this.ProjectMgr.StateElement;
      if (project == null){Debug.Assert(false); return;}
      XmlElement settings = (XmlElement)project.SelectSingleNode("Build/Settings");
      if (settings == null){Debug.Assert(false); return;}
      this.assemblyName = settings.GetAttribute("AssemblyName");
      string outputType = settings.GetAttribute("OutputType");
      if (outputType != null && outputType.Length > 0)
        try{this.outputType = (OutputType)Enum.Parse(typeof(OutputType), outputType);} catch{} //Should only fail if project file is corrupt
      this.defaultNamespace = settings.GetAttribute("RootNamespace");
      this.startupObject = settings.GetAttribute("StartupObject");
      this.applicationIcon = settings.GetAttribute("ApplicationIcon");
      this.shadowedAssembly = settings.GetAttribute("ShadowedAssembly");
      this.standardLibraryLocation = settings.GetAttribute("StandardLibraryLocation");
      string targetPlatform = settings.GetAttribute("TargetPlatform");
      if (targetPlatform != null && targetPlatform.Length > 0)
        try{this.targetPlatform = (PlatformType)Enum.Parse(typeof(PlatformType), targetPlatform);} catch{}
      this.targetPlatformLocation = settings.GetAttribute("TargetPlatformLocation");
      string nostdlib = settings.GetAttribute("NoStandardLibraries");
      if (nostdlib != null && nostdlib.Length > 0)
        try{this.nostdlib = bool.Parse(nostdlib);} catch{}
    }

    public override void ApplyChanges(){
      XmlElement project = this.ProjectMgr.StateElement;
      if (project == null){Debug.Assert(false); return;}
      XmlElement settings = (XmlElement)project.SelectSingleNode("Build/Settings");
      if (settings == null){Debug.Assert(false); return;}
      settings.SetAttribute("AssemblyName", this.assemblyName);
      settings.SetAttribute("OutputType", this.outputType.ToString());
      settings.SetAttribute("RootNamespace", this.defaultNamespace);
      settings.SetAttribute("StartupObject", this.startupObject);
      settings.SetAttribute("ApplicationIcon", this.applicationIcon);
      settings.SetAttribute("ShadowedAssembly", this.shadowedAssembly);
      settings.SetAttribute("StandardLibraryLocation", this.standardLibraryLocation);
      settings.SetAttribute("TargetPlatform", this.targetPlatform.ToString());
      settings.SetAttribute("TargetPlatformLocation", this.targetPlatformLocation);
      settings.SetAttribute("NoStandardLibraries", this.nostdlib.ToString());
      this.IsDirty = false;
    } 
    [LocCategory(UIStringNames.Application)]
    [DisplayName(UIStringNames.AssemblyName)]
    [LocDescription(UIStringNames.AssemblyNameDescription)]    
    public string AssemblyName{
      get{return this.assemblyName;}
      set{this.assemblyName = value; this.IsDirty = true;}
    }
    [LocCategory(UIStringNames.Application)]
    [DisplayName(UIStringNames.OutputType)]
    [LocDescription(UIStringNames.OutputTypeDescription)]
    public OutputType OutputType{
      get{return this.outputType;}
      set{this.outputType = value; this.IsDirty = true;}
    }
    [LocCategory(UIStringNames.Application)]
    [DisplayName(UIStringNames.DefaultNamespace)]
    [LocDescription(UIStringNames.DefaultNamespaceDescription)]
    public string DefaultNamespace{
      get{return  this.defaultNamespace;}
      set{this.defaultNamespace = value; this.IsDirty = true;}
    }
    [LocCategory(UIStringNames.Application)]
    [DisplayName(UIStringNames.StartupObject)]
    [LocDescription(UIStringNames.StartupObjectDescription)]
    public string StartupObject{
      get{return this.startupObject;}
      set{this.startupObject = value; this.IsDirty = true;}
    }
    [LocCategory(UIStringNames.Application)]
    [DisplayName(UIStringNames.ApplicationIcon)]
    [LocDescription(UIStringNames.ApplicationIconDescription)]
    public string ApplicationIcon{
      get{return this.applicationIcon;}
      set{this.applicationIcon = value; this.IsDirty = true;}
    }
    [LocCategory(UIStringNames.Project)]
    [DisplayName(UIStringNames.ProjectFile)]
    [LocDescription(UIStringNames.ProjectFileDescription)]
    public string ProjectFile{
      get{return Path.GetFileName(this.ProjectMgr.ProjectFile);}
    }
    [LocCategory(UIStringNames.Project)]
    [DisplayName(UIStringNames.ProjectFolder)]
    [LocDescription(UIStringNames.ProjectFolderDescription)]
    public string ProjectFolder{
      get{return Path.GetDirectoryName(this.ProjectMgr.ProjectFolder);}
    }
    [LocCategory(UIStringNames.Project)]
    [DisplayName(UIStringNames.OutputFile)]
    [LocDescription(UIStringNames.OutputFileDescription)]
    public string OutputFile{
      get{
        switch (this.outputType){
          case OutputType.Exe: return this.assemblyName+".exe";
          default: return this.assemblyName+".dll";
        }
      }
    }
    [LocCategory(UIStringNames.Specialized)]
    [DisplayName(UIStringNames.ShadowedAssembly)]
    [LocDescription(UIStringNames.ShadowedAssemblyDescription)]
    public string ShadowedAssembly {
      get{return this.shadowedAssembly;}
      set{this.shadowedAssembly = value; IsDirty = true;}
    }
    [LocCategory(UIStringNames.Specialized)]
    [DisplayName(UIStringNames.StandardLibraryLocation)]
    [LocDescription(UIStringNames.StandardLibraryLocationDescription)]
    public string StandardLibraryLocation {
      get{return this.standardLibraryLocation;}
      set{this.standardLibraryLocation = value; IsDirty = true;}
    }
    [LocCategory(UIStringNames.Specialized)]
    [DisplayName(UIStringNames.TargetPlatform)]
    [LocDescription(UIStringNames.TargetPlatformDescription)]
    public PlatformType TargetPlatform{
      get{return this.targetPlatform;}
      set{this.targetPlatform = value; IsDirty = true;}
    }
    [LocCategory(UIStringNames.Specialized)]
    [DisplayName(UIStringNames.TargetPlatformLocation)]
    [LocDescription(UIStringNames.TargetPlatformLocationDescription)]
    public string TargetPlatformLocation {
      get{return this.targetPlatformLocation;}
      set{this.targetPlatformLocation = value; IsDirty = true;}
    }
    [LocCategory(UIStringNames.Specialized)]
    [DisplayName(UIStringNames.NoStandardLibraries)]
    [LocDescription(UIStringNames.NoStandardLibrariesDescription)]
    public bool NoStandardLibraries{
      get{return this.nostdlib;}
      set{this.nostdlib = value; IsDirty = true;}
    }
  }

  /////////////////////////////////////////////////////////////////////////////////////////////
  ///
  ///   The debug property page dialog
  ///
  /////////////////////////////////////////////////////////////////////////////////////////////
  [ComVisible(true), Guid("5BC9517D-EF54-4b12-A617-EB38B1F38250")]
  public sealed class DebugPropertyPage : SettingsPage{   
    public DebugPropertyPage(){
      this.strName = SR.GetString(UIStringNames.DebugCaption);
    }

    public override string GetClassName(){
      return this.GetType().FullName;
    }

    DebugMode _DebugMode;
    string _StartApplication;
    string _StartURL;
    string _StartPage;
    string _CommandLineArguments;
    string _WorkingDirectory;
    bool _UseInternetExplorer;
    bool _EnableRemoteDebugging;
    string _RemoteDebugMachine;

    public override void BindProperties(){
      // TODO: figure out where to get these options from...
      try {_DebugMode = (DebugMode) DebugMode.Parse(typeof(DebugMode), GetConfigProperty("@DebugMode")) ; } catch {};
      _StartApplication = GetConfigProperty("@StartProgram"); 
      _StartURL = GetConfigProperty("@StartURL"); 
      _StartPage = GetConfigProperty("@StartPage"); 
      _CommandLineArguments = GetConfigProperty("@CmdArgs"); 
      _WorkingDirectory = GetConfigProperty("@WorkingDirectory"); 
       try{ _UseInternetExplorer = bool.Parse(GetConfigProperty("@UseIE")); } catch{}
       try{ _EnableRemoteDebugging = bool.Parse(GetConfigProperty("@EnableRemoteDebugging")); } catch{}
      _RemoteDebugMachine = GetConfigProperty("@RemoteDebugMachine");       
    }


    public override void ApplyChanges(){
      SetConfigProperty("DebugMode", _DebugMode.ToString());
      SetConfigProperty("StartProgram", _StartApplication);
      SetConfigProperty("StartURL", _StartURL);
      SetConfigProperty("StartPage", _StartPage);
      SetConfigProperty("CmdArgs", _CommandLineArguments);
      SetConfigProperty("WorkingDirectory", _WorkingDirectory);
      SetConfigProperty("UseIE", _UseInternetExplorer.ToString());
      SetConfigProperty("EnableRemoteDebugging", _EnableRemoteDebugging.ToString());
      SetConfigProperty("RemoteDebugMachine", _RemoteDebugMachine);
      IsDirty = false;
    }
    /**** temporarily removed    
    [LocCategory(UIStringNames.StartAction)]
    [DisplayName(UIStringNames.DebugMode)]
    [LocDescription(UIStringNames.DebugModeDescription)]
    public DebugMode DebugMode{ 
      get{ return _DebugMode; }
      set{ _DebugMode = value; IsDirty = true;}
    }
    */
    [LocCategory(UIStringNames.StartAction)]
    [DisplayName(UIStringNames.StartApplication)]
    [LocDescription(UIStringNames.StartApplicationDescription)]
    public string StartApplication{
      get{ return _StartApplication; }
      set{ _StartApplication = value; IsDirty = true;}
    }
    /**** temporarily removed
    [LocCategory(UIStringNames.StartAction)]
    [DisplayName(UIStringNames.StartURL)]
    [LocDescription(UIStringNames.StartURLDescription)]
    public string StartURL{
      get{ return _StartURL; }
      set{ _StartURL = value; IsDirty = true;}
    }
    [LocCategory(UIStringNames.StartAction)]
    [DisplayName(UIStringNames.StartPage)]
    [LocDescription(UIStringNames.StartPageDescription)]
    public string StartPage{
      get{ return _StartPage; }
      set{ _StartPage = value; IsDirty = true;}
    }
    */
    [LocCategory(UIStringNames.StartOptions)]
    [DisplayName(UIStringNames.CommandLineArguments)]
    [LocDescription(UIStringNames.CommandLineArgumentsDescription)]
    public string CommandLineArguments{
      get{ return _CommandLineArguments; }
      set{ _CommandLineArguments = value; IsDirty = true;}
    }
    [LocCategory(UIStringNames.StartOptions)]
    [DisplayName(UIStringNames.WorkingDirectory)]
    [LocDescription(UIStringNames.WorkingDirectoryDescription)]
    public string WorkingDirectory{
      get{ return _WorkingDirectory; }
      set{ _WorkingDirectory = value; IsDirty = true;}
    }
    /**** temporarily removed
    [LocCategory(UIStringNames.StartOptions)]
    [DisplayName(UIStringNames.UseInternetExplorer)]
    [LocDescription(UIStringNames.UseInternetExplorerDescription)]
    public bool UseInternetExplorer{
      get{ return _UseInternetExplorer; }
      set{ _UseInternetExplorer = value; IsDirty = true;}
    }
    [LocCategory(UIStringNames.StartOptions)]
    [DisplayName(UIStringNames.EnableRemoteDebugging)]
    [LocDescription(UIStringNames.EnableRemoteDebuggingDescription)]
    public bool EnableRemoteDebugging {
      get{ return _EnableRemoteDebugging; }
      set{ _EnableRemoteDebugging = value; IsDirty = true;}
    }
    */
    [LocCategory(UIStringNames.StartOptions)]
    [DisplayName(UIStringNames.RemoteDebugMachine)]
    [LocDescription(UIStringNames.RemoteDebugMachineDescription)]
    public string RemoteDebugMachine{
      get{ return _RemoteDebugMachine; }
      set{ _RemoteDebugMachine = value; IsDirty = true;}
    }
  }

  /////////////////////////////////////////////////////////////////////////////////////////////
  ///
  ///   The build property page dialog
  ///
  /////////////////////////////////////////////////////////////////////////////////////////////
  [ComVisible(true), Guid("873D1121-908A-433e-9135-06F248149EC5")]
  public class BuildPropertyPage : SettingsPage{
    public BuildPropertyPage(){
      this.strName = SR.GetString(UIStringNames.BuildCaption);
    }

    string _DefineConstants;
    bool _OptimizeCode;
    bool _CheckArithmeticOverflow;
    bool _AllowUnsafeCode;
    int _WarningLevel;
    bool _TreatWarningsAsErrors;
    string _OutputPath;
    string _XMLDocumentationFile;
    bool _GenerateDebuggingInformation;
    bool _RegisterForComInterop;

    public override void BindProperties(){
      _DefineConstants = GetConfigProperty("@DefineConstants");
      try{ _OptimizeCode = bool.Parse(GetConfigProperty("@Optimize")); } catch{}
      try{ _CheckArithmeticOverflow = bool.Parse(GetConfigProperty("@CheckForOverflowUnderflow")); } catch{}
      try{ _AllowUnsafeCode = bool.Parse(GetConfigProperty("@AllowUnsafeBlocks")); } catch{}
      try{ _WarningLevel = int.Parse(GetConfigProperty("@WarningLevel")); } catch{}
      try{ _TreatWarningsAsErrors = bool.Parse(GetConfigProperty("@TreatWarningsAsErrors")); } catch{}
      _OutputPath = GetConfigProperty("@OutputPath"); 
      _XMLDocumentationFile = GetConfigProperty("@DocumentationFile");
      try{ _GenerateDebuggingInformation = bool.Parse(GetConfigProperty("@DebugSymbols")); } catch{}
      try{ _RegisterForComInterop = bool.Parse(GetConfigProperty("@RegisterForComInterop")); } catch{}

    }
    public override void ApplyChanges(){
      SetConfigProperty("DefineConstants", _DefineConstants);
      SetConfigProperty("Optimize", _OptimizeCode.ToString());
      SetConfigProperty("CheckForOverflowUnderflow", _CheckArithmeticOverflow.ToString());
      SetConfigProperty("AllowUnsafeBlocks", _AllowUnsafeCode.ToString());
      SetConfigProperty("WarningLevel", _WarningLevel.ToString());
      SetConfigProperty("TreatWarningsAsErrors", _TreatWarningsAsErrors.ToString());
      SetConfigProperty("OutputPath", _OutputPath);
      SetConfigProperty("DocumentationFile", _XMLDocumentationFile);
      SetConfigProperty("DebugSymbols", _GenerateDebuggingInformation.ToString());
      SetConfigProperty("RegisterForComInterop", _RegisterForComInterop.ToString());
 
      IsDirty = false;
    }

    public override string GetClassName(){
      return this.GetType().FullName;
    }
    [LocCategory(UIStringNames.CodeGeneration)]
    [DisplayName(UIStringNames.DefineConstants)]
    [LocDescription(UIStringNames.DefineConstantsDescription)]
    public string DefineConstants{ 
      get{ return _DefineConstants; }
      set{ _DefineConstants = value; IsDirty = true; }
    }
    [LocCategory(UIStringNames.CodeGeneration)]
    [DisplayName(UIStringNames.OptimizeCode)]
    [LocDescription(UIStringNames.OptimizeCodeDescription)]
    public bool OptimizeCode{ 
      get{ return _OptimizeCode;  }
      set{  _OptimizeCode = value;  IsDirty = true; }
    }
    [LocCategory(UIStringNames.CodeGeneration)]
    [DisplayName(UIStringNames.CheckArithmeticOverflow)]
    [LocDescription(UIStringNames.CheckArithmeticOverflowDescription)]
    public bool CheckArithmeticOverflow{ 
      get{ return _CheckArithmeticOverflow; }
      set{ _CheckArithmeticOverflow = value; IsDirty = true; }
    }
    [LocCategory(UIStringNames.CodeGeneration)]
    [DisplayName(UIStringNames.AllowUnsafeCode)]
    [LocDescription(UIStringNames.AllowUnsafeCodeDescription)]
    public bool AllowUnsafeCode{ 
      get{ return _AllowUnsafeCode; }
      set{ _AllowUnsafeCode = value; IsDirty = true; }
    }
    [LocCategory(UIStringNames.ErrorsAndWarnings)]
    [DisplayName(UIStringNames.WarningLevel)]
    [LocDescription(UIStringNames.WarningLevelDescription)]
    public int WarningLevel{ 
      get{ return _WarningLevel; }
      set{ _WarningLevel = value; IsDirty = true; }
    }
    [LocCategory(UIStringNames.ErrorsAndWarnings)]
    [DisplayName(UIStringNames.TreatWarningsAsErrors)]
    [LocDescription(UIStringNames.TreatWarningsAsErrorsDescription)]
    public bool TreatWarningsAsErrors{ 
      get{ return _TreatWarningsAsErrors; }
      set{ _TreatWarningsAsErrors = value; IsDirty = true; }
    }

    [LocCategory(UIStringNames.Outputs)]
    [DisplayName(UIStringNames.OutputPath)]
    [LocDescription(UIStringNames.OutputPathDescription)]
    public string OutputPath{ 
      get{ return _OutputPath; }
      set{ _OutputPath = value; IsDirty = true; }
    }
    [LocCategory(UIStringNames.Outputs)]
    [DisplayName(UIStringNames.XMLDocumentationFile)]
    [LocDescription(UIStringNames.XMLDocumentationFileDescription)]
    public string XMLDocumentationFile{ 
      get{ return _XMLDocumentationFile;  }
      set{ _XMLDocumentationFile = value; IsDirty = true; }
    }

    [LocCategory(UIStringNames.Outputs)]
    [DisplayName(UIStringNames.GenerateDebuggingInformation)]
    [LocDescription(UIStringNames.GenerateDebuggingInformationDescription)]
    public bool GenerateDebuggingInformation{ 
      get{ return _GenerateDebuggingInformation; }
      set{ _GenerateDebuggingInformation = value;  IsDirty = true; }
    }
    /**** temporarily removed
    [LocCategory(UIStringNames.Outputs)]
    [DisplayName(UIStringNames.RegisterForCOMInterop)]
    [LocDescription(UIStringNames.RegisterForCOMInteropDescription)]
    public bool RegisterForComInterop { 
      get{ return _RegisterForComInterop; }
      set{ _RegisterForComInterop = value;  IsDirty = true; }
    }
    */
  }

  /////////////////////////////////////////////////////////////////////////////////////////////
  ///
  ///   The advanced property page dialog
  ///
  /////////////////////////////////////////////////////////////////////////////////////////////
  [ComVisible(true), Guid("3f5e7baa-7f96-4e64-8dce-9593ab90e996")]
  public class AdvancedPropertyPage : SettingsPage{
   
    public AdvancedPropertyPage(){
      this.strName = SR.GetString(UIStringNames.Advanced);
    }

    bool _IncrementalBuild;
    long _BaseAddress;
    int _FileAlignment;
    bool _DisableInternalChecks;
    bool _DisableAssumeChecks;
    bool _DisableDefensiveChecks;
    bool _DisableGuardedClassesChecks;
    bool _DisableInternalContractsMetadata;
    bool _DisablePublicContractsMetadata;

    public override void BindProperties(){
      try{ _IncrementalBuild= bool.Parse(GetConfigProperty("@IncrementalBuild")); } catch{}
      try{ _BaseAddress = long.Parse(GetConfigProperty("@BaseAddress")); } catch{}
      try{ _FileAlignment = int.Parse(GetConfigProperty("@FileAlignment")); } catch{}
      try{ _DisableAssumeChecks = bool.Parse(GetConfigProperty("@DisableAssumeChecks")); } catch{}
      try{ _DisableDefensiveChecks = bool.Parse(GetConfigProperty("@DisableDefensiveChecks")); } catch{}
      try{ _DisableGuardedClassesChecks = bool.Parse(GetConfigProperty("@DisableGuardedClassesChecks")); } catch{}
      try{ _DisableInternalChecks = bool.Parse(GetConfigProperty("@DisableInternalChecks")); } catch{}
      try{ _DisableInternalContractsMetadata = bool.Parse(GetConfigProperty("@DisableInternalContractsMetadata")); } catch{}
      try{ _DisablePublicContractsMetadata = bool.Parse(GetConfigProperty("@DisablePublicContractsMetadata")); } catch{}
    }
    public override void ApplyChanges(){

      SetConfigProperty("IncrementalBuild", _IncrementalBuild.ToString());
      SetConfigProperty("BaseAddress", _BaseAddress.ToString());
      SetConfigProperty("FileAlignment", _FileAlignment.ToString());
      SetConfigProperty("DisableAssumeChecks", _DisableAssumeChecks.ToString());
      SetConfigProperty("DisableDefensiveChecks", _DisableDefensiveChecks.ToString());
      SetConfigProperty("DisableGuardedClassesChecks", _DisableGuardedClassesChecks.ToString());
      SetConfigProperty("DisableInternalChecks", _DisableInternalChecks.ToString());
      SetConfigProperty("DisableInternalContractsMetadata", _DisableInternalContractsMetadata.ToString());
      SetConfigProperty("DisablePublicContractsMetadata", _DisablePublicContractsMetadata.ToString());
      IsDirty = false;
    }

    public override string GetClassName(){
      return this.GetType().FullName;
    }
    /**** temporarily removed
    [LocCategory(UIStringNames.GeneralCaption)]
    [DisplayName(UIStringNames.IncrementalBuild)]
    [LocDescription(UIStringNames.IncrementalBuildDescription)]
    public bool IncrementalBuild { 
      get{ return _IncrementalBuild; }
      set{ _IncrementalBuild = value; IsDirty = true; }
    }
    */
    [LocCategory(UIStringNames.GeneralCaption)]
    [DisplayName(UIStringNames.BaseAddress)]
    [LocDescription(UIStringNames.BaseAddressDescription)]
    public long BaseAddress { 
      get{ return _BaseAddress; }
      set{ _BaseAddress = value;  IsDirty = true; }
    }
    [LocCategory(UIStringNames.GeneralCaption)]
    [DisplayName(UIStringNames.FileAlignment)]
    [LocDescription(UIStringNames.FileAlignmentDescription)]
    public int FileAlignment { 
      get{ return _FileAlignment; }
      set{ _FileAlignment = value;  IsDirty = true; }
    }
    [LocCategory(UIStringNames.RunTimeChecks)]
    [DisplayName(UIStringNames.DisableAssumeChecks)]
    [LocDescription(UIStringNames.DisableAssumeChecksDescription)]
    public bool DisableAssumeChecks {
      get{ return _DisableAssumeChecks; }
      set{ _DisableAssumeChecks = value;  IsDirty = true; }
    }
    [LocCategory(UIStringNames.RunTimeChecks)]
    [DisplayName(UIStringNames.DisableDefensiveChecks)]
    [LocDescription(UIStringNames.DisableDefensiveChecksDescription)]
    public bool DisableDefensiveChecks {
      get{ return _DisableDefensiveChecks; }
      set{ _DisableDefensiveChecks = value;  IsDirty = true; }
    }
    [LocCategory(UIStringNames.RunTimeChecks)]
    [DisplayName(UIStringNames.DisableGuardedClassesChecks)]
    [LocDescription(UIStringNames.DisableGuardedClassesChecksDescription)]
    public bool DisableGuardedClassesChecks {
      get{ return _DisableGuardedClassesChecks; }
      set{ _DisableGuardedClassesChecks = value;  IsDirty = true; }
    }
    [LocCategory(UIStringNames.RunTimeChecks)]
    [DisplayName(UIStringNames.DisableInternalChecks)]
    [LocDescription(UIStringNames.DisableInternalChecksDescription)]
    public bool DisableInternalChecks {
      get{ return _DisableInternalChecks; }
      set{ _DisableInternalChecks = value;  IsDirty = true; }
    }
    [LocCategory(UIStringNames.Metadata)]
    [DisplayName(UIStringNames.DisableInternalContractsMetadata)]
    [LocDescription(UIStringNames.DisableInternalContractsMetadataDescription)]
    public bool DisableInternalContractsMetadata {
      get{ return _DisableInternalContractsMetadata; }
      set{ _DisableInternalContractsMetadata = value;  IsDirty = true; }
    }
    [LocCategory(UIStringNames.Metadata)]
    [DisplayName(UIStringNames.DisablePublicContractsMetadata)]
    [LocDescription(UIStringNames.DisablePublicContractsMetadataDescription)]
    public bool DisablePublicContractsMetadata {
      get{ return _DisablePublicContractsMetadata; }
      set{ _DisablePublicContractsMetadata = value;  IsDirty = true; }
    }
  }

  [AttributeUsage (AttributeTargets.Property | AttributeTargets.Field,AllowMultiple = false)]
  internal sealed class LocDescriptionAttribute : DescriptionAttribute 
 {
    private bool replaced = false;
    UIStringNames name;

    public LocDescriptionAttribute(UIStringNames name) : base(name.ToString()){
      this.name = name;
    }

    public override string Description{
      get{
        if (!replaced){
          replaced = true;
          this.DescriptionValue = SR.GetString(this.name);
        }
        return base.Description;
      }
    }
  }

  [AttributeUsage (AttributeTargets.Property | AttributeTargets.Field,AllowMultiple = false)]
  internal sealed class LocCategoryAttribute : CategoryAttribute 
 {
    UIStringNames name;

    public LocCategoryAttribute(UIStringNames name) : base(name.ToString()){
      this.name = name;
    }
    protected override string GetLocalizedString(string value){
      return SR.GetString(this.name);
    }
  }

  [AttributeUsage (AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field,
    Inherited = false,AllowMultiple = false) ]
  internal class DisplayNameAttribute : Attribute{
    UIStringNames name;

    public DisplayNameAttribute(UIStringNames name){
      this.name = name;
    }

    public virtual string DisplayName{
      get{ 
        string result = SR.GetString(this.name);
        if (result == null){
#if FAIL_ON_MISSING_PROPERTY_DISPLAY_NAMES
          Debug.Fail(string.Format("Property display name for property {0} missing from the resource file.", this.name));
#endif
          result = this.name.ToString();
        }
        return result;
      }
    }
  }

    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="OutputTypeConverter"]/*' />
    public class OutputTypeConverter : TypeConverter{
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="OutputTypeConverter.CanConvertFrom"]/*' />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType){
            if (sourceType == typeof(string)) return true;

            return base.CanConvertFrom(context, sourceType);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="OutputTypeConverter.ConvertFrom"]/*' />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value){
            string str = value as string;

            if (str != null){
                if (str == SR.GetString(SR.Exe, culture)) return OutputType.Exe;
                if (str == SR.GetString(SR.Library, culture)) return OutputType.Library;
                if (str == SR.GetString(SR.WinExe, culture)) return OutputType.WinExe;
            }

            return base.ConvertFrom(context, culture, value);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="OutputTypeConverter.ConvertTo"]/*' />
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType){
            if (destinationType == typeof(string)){
                string result = SR.GetString(((OutputType)value).ToString(), culture);

                if (result != null) return result;
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="OutputTypeConverter.GetStandardValuesSupported"]/*' />
        public override bool GetStandardValuesSupported(System.ComponentModel.ITypeDescriptorContext context){
            return true;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="OutputTypeConverter.GetStandardValues"]/*' />
        public override System.ComponentModel.TypeConverter.StandardValuesCollection GetStandardValues(System.ComponentModel.ITypeDescriptorContext context){
            return new StandardValuesCollection(new OutputType[]{ OutputType.Exe, OutputType.Library, OutputType.WinExe });
        }
    }
/// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DebugModeConverter"]/*' />
    public class DebugModeConverter : TypeConverter{
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DebugModeConverter.CanConvertFrom"]/*' />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType){
            if (sourceType == typeof(string)) return true;

            return base.CanConvertFrom(context, sourceType);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DebugModeConverter.ConvertFrom"]/*' />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value){
            string str = value as string;

            if (str != null){
                if (str == SR.GetString(SR.Program, culture)) return DebugMode.Program;

                if (str == SR.GetString(SR.Project, culture)) return DebugMode.Project;

                if (str == SR.GetString(SR.URL, culture)) return DebugMode.URL;
            }

            return base.ConvertFrom(context, culture, value);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DebugModeConverter.ConvertTo"]/*' />
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType){
            if (destinationType == typeof(string)){
                string result = SR.GetString(((DebugMode)value).ToString(), culture);

                if (result != null) return result;
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DebugModeConverter.GetStandardValuesSupported"]/*' />
        public override bool GetStandardValuesSupported(System.ComponentModel.ITypeDescriptorContext context){
            return true;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DebugModeConverter.GetStandardValues"]/*' />
        public override System.ComponentModel.TypeConverter.StandardValuesCollection GetStandardValues(System.ComponentModel.ITypeDescriptorContext context){
            return new StandardValuesCollection(new DebugMode[]{ DebugMode.Program, DebugMode.Project, DebugMode.URL });
        }
    }
/// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildActionConverter"]/*' />
    public class BuildActionConverter : TypeConverter{
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildActionConverter.CanConvertFrom"]/*' />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType){
            if (sourceType == typeof(string)) return true;

            return base.CanConvertFrom(context, sourceType);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildActionConverter.ConvertFrom"]/*' />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value){
            string str = value as string;

            if (str != null){
                if (str == SR.GetString(SR.Compile, culture)) return BuildAction.Compile;

                if (str == SR.GetString(SR.Content, culture)) return BuildAction.Content;

                if (str == SR.GetString(SR.EmbeddedResource, culture)) return BuildAction.EmbeddedResource;

                if (str == SR.GetString(SR.None, culture)) return BuildAction.None;
            }

            return base.ConvertFrom(context, culture, value);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildActionConverter.ConvertTo"]/*' />
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType){
            if (destinationType == typeof(string)){
                string result = SR.GetString(((BuildAction)value).ToString(), culture);

                if (result != null) return result;
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildActionConverter.GetStandardValuesSupported"]/*' />
        public override bool GetStandardValuesSupported(System.ComponentModel.ITypeDescriptorContext context){
            return true;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildActionConverter.GetStandardValues"]/*' />
        public override System.ComponentModel.TypeConverter.StandardValuesCollection GetStandardValues(System.ComponentModel.ITypeDescriptorContext context){
            return new StandardValuesCollection(new BuildAction[]{ BuildAction.Compile, BuildAction.Content, BuildAction.EmbeddedResource, BuildAction.None });
        }
    }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PlatformTypeConverter"]/*' />
    public class PlatformTypeConverter : TypeConverter{
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PlatformTypeConverter.CanConvertFrom"]/*' />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType){
            if (sourceType == typeof(string)) return true;

            return base.CanConvertFrom(context, sourceType);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PlatformTypeConverter.ConvertFrom"]/*' />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value){
            string str = value as string;

            if (str != null){
                if (str == SR.GetString(SR.v1, culture)) return PlatformType.v1;

                if (str == SR.GetString(SR.v11, culture)) return PlatformType.v11;

                if (str == SR.GetString(SR.v2, culture)) return PlatformType.v2;

                if (str == SR.GetString(SR.cli1, culture)) return PlatformType.cli1;
            }

            return base.ConvertFrom(context, culture, value);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PlatformTypeConverter.ConvertTo"]/*' />
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType){
            if (destinationType == typeof(string)){
                string result = SR.GetString(((PlatformType)value).ToString(), culture);

                if (result != null) return result;
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PlatformTypeConverter.GetStandardValuesSupported"]/*' />
        public override bool GetStandardValuesSupported(System.ComponentModel.ITypeDescriptorContext context){
            return true;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PlatformTypeConverter.GetStandardValues"]/*' />
        public override System.ComponentModel.TypeConverter.StandardValuesCollection GetStandardValues(System.ComponentModel.ITypeDescriptorContext context){
            return new StandardValuesCollection(new PlatformType[]{ PlatformType.v1, PlatformType.v11, PlatformType.v2, PlatformType.cli1 });
        }
    }
}
