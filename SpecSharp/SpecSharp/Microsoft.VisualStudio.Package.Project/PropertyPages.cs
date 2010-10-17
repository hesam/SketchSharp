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

namespace Microsoft.VisualStudio.Package
{
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="OutputType"]/*' />
    [TypeConverter(typeof(OutputTypeConverter))]
    public enum OutputType { Library, WinExe, Exe }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DebugMode"]/*' />
    [TypeConverter(typeof(DebugModeConverter))]
    public enum DebugMode { Project, Program, URL }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildAction"]/*' />
    [TypeConverter(typeof(BuildActionConverter))]
    public enum BuildAction { None, Compile, Content, EmbeddedResource }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildVerbosity"]/*' />
    [TypeConverter(typeof(BuildVerbosityConverter))]
    public enum BuildVerbosity { Quiet, Minimal, Normal, Detailed, Diagnostic }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PlatformType"]/*' />
    [TypeConverter(typeof(PlatformTypeConverter))]
    public enum PlatformType { notSpecified, v1, v11, v2, cli1 }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PropPageStatus"]/*' />
    [Flags]
    public enum PropPageStatus
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PropPageStatus1"]/*' />
    {
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PropPageStatus.Dirty"]/*' />
        Dirty = 0x1,
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PropPageStatus.Validate"]/*' />
        Validate = 0x2,
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PropPageStatus.Clean"]/*' />
        Clean = 0x4
    }
    /// <include file='doc\Nodes.uex' path='docs/doc[@for="ModuleKindFlags"]/*' />
    [Flags]
    public enum ModuleKindFlags {
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
    [ComVisible(true)]
    public class LocalizableProperties : ICustomTypeDescriptor
    {
        #region ICustomTypeDescriptor
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetAttributes"]/*' />
        /// <summary>
        /// Delegates to TypeDescriptor.
        /// </summary>
        public AttributeCollection GetAttributes() {
            AttributeCollection col = TypeDescriptor.GetAttributes(this, true);
            return col;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetDefaultEvent"]/*' />
        /// <summary>
        /// Delegates to TypeDescriptor.
        /// </summary>
        public EventDescriptor GetDefaultEvent() {
            EventDescriptor ed = TypeDescriptor.GetDefaultEvent(this, true);
            return ed;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetDefaultProperty"]/*' />
        /// <summary>
        /// Delegates to TypeDescriptor.
        /// </summary>
        public PropertyDescriptor GetDefaultProperty() {
            PropertyDescriptor pd = TypeDescriptor.GetDefaultProperty(this, true);
            return pd;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetEditor"]/*' />
        /// <summary>
        /// Retrieves the an editor for this object.
        /// </summary>
        public object GetEditor(Type editorBaseType) {
            object o = TypeDescriptor.GetEditor(this, editorBaseType, true);
            return o;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetEvents"]/*' />
        /// <summary>
        /// Delegates to TypeDescriptor.
        /// </summary>
        public EventDescriptorCollection GetEvents() {
            EventDescriptorCollection edc = TypeDescriptor.GetEvents(this, true);
            return edc;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetEvents1"]/*' />
        /// <summary>
        /// Delegates to TypeDescriptor.
        /// </summary>
        public EventDescriptorCollection GetEvents(System.Attribute[] attributes) {
            EventDescriptorCollection edc = TypeDescriptor.GetEvents(this, attributes, true);
            return edc;
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
            PropertyDescriptorCollection pcol = GetProperties(null);
            return pcol;
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
                newList.Add(CreateDesignPropertyDescriptor(props[i]));

            return new PropertyDescriptorCollection((PropertyDescriptor[])newList.ToArray(typeof(PropertyDescriptor)));;
        }

        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.CreateDesignPropertyDescriptor"]/*' />
        /// <summary>
        /// Return a DesignPropertyDescriptor wrapper on the given property descriptor. 
        /// </summary>
        public virtual DesignPropertyDescriptor CreateDesignPropertyDescriptor(PropertyDescriptor p) {
            return new DesignPropertyDescriptor(p);
        }

        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetComponentName"]/*' />
        /// <summary>
        /// Get the name of the component.
        /// </summary>
        public string GetComponentName() {
            string name = TypeDescriptor.GetComponentName(this, true);
            return name;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocalizableProperties.GetConverter"]/*' />
        /// <summary>
        /// Retrieves the type converter for this object.
        /// </summary>
        public TypeConverter GetConverter() {
            TypeConverter tc = TypeDescriptor.GetConverter(this, true);
            return tc;
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
    public class NodeProperties : LocalizableProperties, ISpecifyPropertyPages, IVsGetCfgProvider
    {
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="NodeProperties.Node;"]/*' />
        public HierarchyNode Node;
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="NodeProperties.project;"]/*' />
        public ProjectNode project;
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="NodeProperties.NodeProperties"]/*' />
        public NodeProperties(HierarchyNode node)
        {
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
        public virtual int GetCfgProvider(out IVsCfgProvider p) {
            return ((IVsGetCfgProvider)Node.ProjectMgr).GetCfgProvider(out p);
        }
        #endregion 

        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="NodeProperties.GetProperty"]/*' />
        protected string GetProperty(string name, string def)
        {
            string a = Node.ItemNode.GetAttribute(name);
            return (a == null) ? def : a;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="NodeProperties.SetProperty"]/*' />
        protected void SetProperty(string name, string value)
        {
            Node.ItemNode.SetAttribute(name, value);
        }
    }

    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor"]/*' />
    /// <summary>
    /// The purpose of DesignPropertyDescriptor is to allow us to customize the
    /// display name of the property in the property grid.  None of the CLR
    /// implementations of PropertyDescriptor allow you to change the DisplayName.
    /// </summary>
    public class DesignPropertyDescriptor : PropertyDescriptor
    {
        private string displayName; // Custom display name
        private PropertyDescriptor property;    // Base property descriptor
        private Hashtable editors = new Hashtable(); // Type -> editor instance
        private TypeConverter converter;

        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.DisplayName"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override string DisplayName
        {
            get
            {
                return this.displayName;
            }
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.ComponentType"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override Type ComponentType
        {
            get
            {
                return this.property.ComponentType;
            }
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.IsReadOnly"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override bool IsReadOnly
        {
            get
            {
                return this.property.IsReadOnly;
            }
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.PropertyType"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override Type PropertyType
        {
            get
            {
                return this.property.PropertyType;
            }
        }

        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.GetEditor"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override object GetEditor(Type editorBaseType)
        {
            object editor = this.editors[editorBaseType];
            if (editor == null) {
                for (int i = 0; i < this.Attributes.Count; i++)
                {
                    EditorAttribute attr = Attributes[i] as EditorAttribute;
                    if (attr == null)
                    {
                        continue;
                    }
                    Type editorType = Type.GetType(attr.EditorBaseTypeName);
                    if (editorBaseType == editorType)
                    {
                        Type type = GetTypeFromNameProperty(attr.EditorTypeName);
                        if (type != null)
                        {
                            editor = CreateInstance(type);
                            this.editors[type] = editor; // cache it
                            break;
                        }
                    }
                }
            }
            return editor;
        }

        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.Converter"]/*' />
        /// <summary>
        /// Return type converter for property
        /// </summary>
        public override TypeConverter Converter
        {
            get
            {
                if (converter == null)
                {
                    TypeConverterAttribute attr = (TypeConverterAttribute)Attributes[typeof(TypeConverterAttribute)];
                    if (attr.ConverterTypeName != null && attr.ConverterTypeName.Length > 0)
                    {
                        Type converterType = GetTypeFromNameProperty(attr.ConverterTypeName);
                        if (converterType != null && typeof(TypeConverter).IsAssignableFrom(converterType))
                        {
                            converter = (TypeConverter)CreateInstance(converterType);
                        }
                    }

                    if (converter == null)
                    {
                        converter = TypeDescriptor.GetConverter(PropertyType);
                    }
                }
                return converter;
            }
        }


        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.GetEditorType"]/*' />
        /// <summary>
        /// Convert name to a Type object.
        /// </summary>
        public virtual Type GetTypeFromNameProperty(string typeName)
        {
            return Type.GetType(typeName);
        }

        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.CanResetValue"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override bool CanResetValue(object component)
        {
            bool result = this.property.CanResetValue(component);
            return result;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.GetValue"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override object GetValue(object component)
        {
            object value = this.property.GetValue(component);
            return value;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.ResetValue"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override void ResetValue(object component)
        {
            this.property.ResetValue(component);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.SetValue"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override void SetValue(object component, object value)
        {
            this.property.SetValue(component, value);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.ShouldSerializeValue"]/*' />
        /// <summary>
        /// Delegates to base.
        /// </summary>
        public override bool ShouldSerializeValue(object component)
        {
            bool result = this.property.ShouldSerializeValue(component);
            return result;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DesignPropertyDescriptor.DesignPropertyDescriptor"]/*' />
        /// <summary>
        /// Constructor.  Copy the base property descriptor and also hold a pointer
        /// to it for calling its overridden abstract methods.
        /// </summary>
        public DesignPropertyDescriptor(PropertyDescriptor prop)
      : base(prop)
        {
            this.property = prop;

            Attribute attr = prop.Attributes[typeof(DisplayNameAttribute)];

            if (attr is DisplayNameAttribute)
            {
                this.displayName = ((DisplayNameAttribute)attr).DisplayName;
            }
            else
            {
                this.displayName = prop.Name;
            }
        }
    }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    internal sealed class LocDisplayNameAttribute : DisplayNameAttribute {
        string name;
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocDisplayNameAttribute.DisplayNameAttribute"]/*' />
        public LocDisplayNameAttribute(string name) {
            this.name = name;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="LocDisplayNameAttribute.DisplayName"]/*' />
        public override string DisplayName {
            get {
                string result = SR.GetString(this.name);
                if (result == null) {
                    Debug.Assert(false, "String resource '" + this.name + "' is missing");
                    result = this.name;
                }
                return result;
            }
        }
    }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FileProperties"]/*' />
    [CLSCompliant(false), ComVisible(true)]
    public class FileProperties : NodeProperties
    {
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FileProperties.FileProperties"]/*' />
        public FileProperties(HierarchyNode node) : base(node)
        {
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FileProperties.GetClassName"]/*' />
        public override string GetClassName()
        {
            return SR.GetString("FileProperties");
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FileProperties.BuildAction"]/*' />
        [SRCategoryAttribute(SR.Advanced)]
        [LocDisplayName(SR.BuildAction)]
        [SRDescriptionAttribute(SR.BuildActionDescription)]
        public BuildAction BuildAction
        {
            get
            {
                string value = GetProperty("BuildAction", BuildAction.None.ToString());
                if (value == null || value.Length == 0)
                    return BuildAction.None;
                return (BuildAction)Enum.Parse(typeof(BuildAction), value);
            }
            set
            {
                SetProperty("BuildAction", value.ToString());
            }
        }

        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FileProperties.FileName"]/*' />
        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FileName)]
        [SRDescriptionAttribute(SR.FileNameDescription)]
        public string FileName {
            get {
                return Node.Caption;
            }
            set {
                this.Node.SetEditLabel(value);
            }
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FileProperties.FullPath"]/*' />
        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FullPath)]
        [SRDescriptionAttribute(SR.FullPathDescription)]
        public string FullPath {
            get {
                return Node.Url;
            }
        }
    }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ProjectProperties"]/*' />
    [CLSCompliant(false), ComVisible(true)]
    public class ProjectProperties : NodeProperties {
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ProjectProperties.ProjectProperties"]/*' />
        public ProjectProperties(ProjectNode node) : base(node) {
            this.project = node;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ProjectProperties.GetClassName"]/*' />
        public override string GetClassName() {
            return SR.GetString("ProjectProperties");
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ProjectProperties.ProjectFolder"]/*' />
        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.ProjectFolder)]
        [SRDescriptionAttribute(SR.ProjectFolderDescription)]
        public string ProjectFolder {
            get {
                return this.project.ProjectFolder;
            }
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ProjectProperties.ProjectFile"]/*' />
        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.ProjectFile)]
        [SRDescriptionAttribute(SR.ProjectFileDescription)]
        public string ProjectFile
        {
            get
            {
                return this.project.ProjectFile;
            }
            set
            {
                this.project.ProjectFile = value;
            }
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ProjectProperties.Verbosity"]/*' />
        [SRCategoryAttribute(SR.Advanced)]
        [LocDisplayName(SR.BuildVerbosity)]
        [SRDescriptionAttribute(SR.BuildVerbosityDescription)]
        public BuildVerbosity Verbosity
        {
            get
            {
                string verbosity = this.project.GetProjectProperty("BuildVerbosity");
                if (verbosity == null)
                    return BuildVerbosity.Normal;
                return (BuildVerbosity)Enum.Parse(typeof(BuildVerbosity), verbosity);
            }
            set
            {
                this.project.SetProjectProperty("BuildVerbosity", value.ToString());
            }
        }
    }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FolderProperties"]/*' />
    [CLSCompliant(false), ComVisible(true)]
    public class FolderProperties : NodeProperties {
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FolderProperties.FolderProperties"]/*' />
        public FolderProperties(HierarchyNode node) : base(node) {
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FolderProperties.GetClassName"]/*' />
        public override string GetClassName() {
            return SR.GetString("FolderProperties");
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="FolderProperties.FolderName"]/*' />
        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FolderName)]
        [SRDescriptionAttribute(SR.FolderNameDescription)]
        public string FolderName {
            get {
                return this.Node.Caption;
            }
            set {
                this.Node.SetEditLabel(value);
            }
        }
    }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ReferenceProperties"]/*' />
    [CLSCompliant(false), ComVisible(true)]
    public class ReferenceProperties : NodeProperties {
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ReferenceProperties.ReferenceProperties"]/*' />
        public ReferenceProperties(HierarchyNode node) : base(node) {
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ReferenceProperties.GetClassName"]/*' />
        public override string GetClassName() {
            return SR.GetString("ReferenceProperties");
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ReferenceProperties.Name"]/*' />
        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.RefName)]
        [SRDescriptionAttribute(SR.RefNameDescription)]
        public string Name {
            get {
                return this.Node.Caption;
            }
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="ReferenceProperties.CopyToLocal"]/*' />
        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.CopyToLocal)]
        [SRDescriptionAttribute(SR.CopyToLocalDescription)]
        public bool CopyToLocal
        {
            get
            {
                string copyLocal = this.GetProperty("Private", "False");
                if (copyLocal == null || copyLocal.Length == 0)
                    return false;
                return bool.Parse(copyLocal);
            }
            set
            {
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
    [CLSCompliant(false), ComVisible(true)]
    public abstract class SettingsPage : LocalizableProperties, IPropertyPage {
        Panel panel;
        // <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.nodes;"]/*' />
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.nodes;"]/*' />
        protected NodeProperties[] nodes;
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.project;"]/*' />        
        protected ProjectNode project;
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.projectConfigs;"]/*' />
        protected ProjectConfig[] projectConfigs;
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.grid;"]/*' />
        protected IVSMDPropertyGrid grid;
        bool active;
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.strName;"]/*' />
        public string strName;
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.ProjectMgr"]/*' />
        [BrowsableAttribute(false)]
        public ProjectNode ProjectMgr {
            get {
                if (this.project != null) {
                    return this.project;
                } else if (this.projectConfigs != null) {
                    return this.projectConfigs[0].ProjectMgr;
                }

                return null;
            }
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.UpdateObjects"]/*' />
        protected void UpdateObjects() {
            if ((this.nodes != null && this.nodes.Length > 0) || this.projectConfigs != null) {
                IntPtr p = Marshal.GetIUnknownForObject(this);
                IntPtr ppUnk = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(IntPtr)));
                try {
                    Marshal.WriteIntPtr(ppUnk, p);
                    BindProperties();
                    // BUGBUG -- this is really bad casting a pointer to "int"...
                    this.grid.SetSelectedObjects(1, ppUnk.ToInt32());
                    this.grid.Refresh();
                } finally {
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
        protected bool IsDirty {
            get {
                return this.dirty;
            }
            set {
                if (this.dirty != value) {
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
                NativeMethods.SetParent(this.panel.Handle, parent);
            }

            if (this.grid == null && project != null) {
                IVSMDPropertyBrowser pb = project.Site.GetService(typeof(IVSMDPropertyBrowser)) as IVSMDPropertyBrowser;
                this.grid = pb.CreatePropertyGrid();
            }

            this.active = true;

            Control cGrid = Control.FromHandle(new IntPtr(this.grid.Handle));

            cGrid.Parent = Control.FromHandle(parent);//this.panel;
            cGrid.Size = new Size(544, 294);
            cGrid.Location = new Point(3, 3);
            cGrid.Visible = true;
            this.grid.SetOption(_PROPERTYGRIDOPTION.PGOPT_TOOLBAR, false);
            this.grid.GridSort = _PROPERTYGRIDSORT.PGSORT_CATEGORIZED;
            NativeMethods.SetParent(new IntPtr(this.grid.Handle), this.panel.Handle);
            UpdateObjects();
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.Apply"]/*' />
        public virtual int Apply() {
            if (IsDirty)
                ApplyChanges();
            return NativeMethods.S_OK;
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
            return (IsDirty ? (int)NativeMethods.S_OK : (int)NativeMethods.S_FALSE);
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

            if ((msg.message < NativeMethods.WM_KEYFIRST || msg.message > NativeMethods.WM_KEYLAST) && (msg.message < NativeMethods.WM_MOUSEFIRST || msg.message > NativeMethods.WM_MOUSELAST))
                return 1;

            return (NativeMethods.IsDialogMessageA(this.panel.Handle, ref msg)) ? 0 : 1;
        }
        #endregion 

        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.GetProperty"]/*' />
        public string GetProperty(string propertyName)
        {
            if (this.ProjectMgr != null)
            {
                string property = this.ProjectMgr.MSBuildProject.GlobalProperties[propertyName].Value;

                if (property != null)
                {
                    return property;
                }
            }

            return String.Empty;
        }


        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.GetTypedProperty"]/*' />
        public object GetTypedProperty(string name, Type type) {
            string value = GetProperty(name);
            if (string.IsNullOrEmpty(value)) return null;

            try {
                TypeConverter tc = TypeDescriptor.GetConverter(type);
                return tc.ConvertFromInvariantString(value);
            } catch (Exception) {
                return null;
            }
        }

        // relative to active configuration.
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.GetConfigProperty"]/*' />
        public string GetConfigProperty(string propertyName)
        {
            if (this.ProjectMgr != null)
            {
                string unifiedResult = null;
                bool cacheNeedReset = true;

                for (int i = 0; i < this.projectConfigs.Length; i++)
                {
                    ProjectConfig config = projectConfigs[i];
                    string property = config.GetConfigurationProperty(propertyName, cacheNeedReset);
                    cacheNeedReset = false;

                    if (property != null)
                    {
                        string text = property.Trim();

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

        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.GetTypedConfigProperty"]/*' />
        public object GetTypedConfigProperty(string name, Type type) {
            string value = GetConfigProperty(name);
            if (string.IsNullOrEmpty(value)) return null;

            try {
                TypeConverter tc = TypeDescriptor.GetConverter(type);
                return tc.ConvertFromInvariantString(value);
            } catch (Exception) {
                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////////////////
        // the set config property is robust in the sense that they will create the 
        //    attributes if needed.  If value is null it will set empty string.
        ///////////////////////////////////////////////////////////////////////////////////
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="SettingsPage.SetConfigProperty"]/*' />
        public void SetConfigProperty(string attrName, string value)
        {
            CCITracing.TraceCall();
            if (this.ProjectMgr != null)
            {
                for (int i = 0, n = this.projectConfigs.Length; i < n; i++)
                {
                    ProjectConfig config = projectConfigs[i];

                    if (value == null)
                    {
                        value = "";
                    }

                    config.SetConfigurationProperty(attrName, value);
                }

                this.ProjectMgr.SetProjectFileDirty(true);
            }
        }
    }
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="OutputTypeConverter"]/*' />
    public class OutputTypeConverter : TypeConverter {
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="OutputTypeConverter.CanConvertFrom"]/*' />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) {
            if (sourceType == typeof(string)) return true;

            return base.CanConvertFrom(context, sourceType);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="OutputTypeConverter.ConvertFrom"]/*' />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) {
            string str = value as string;

            if (str != null) {
                if (str == SR.GetString(SR.Exe, culture)) return OutputType.Exe;
                if (str == SR.GetString(SR.Library, culture)) return OutputType.Library;
                if (str == SR.GetString(SR.WinExe, culture)) return OutputType.WinExe;
            }

            return base.ConvertFrom(context, culture, value);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="OutputTypeConverter.ConvertTo"]/*' />
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType) {
            if (destinationType == typeof(string)) {
                string result = SR.GetString(((OutputType)value).ToString(), culture);

                if (result != null) return result;
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="OutputTypeConverter.GetStandardValuesSupported"]/*' />
        public override bool GetStandardValuesSupported(System.ComponentModel.ITypeDescriptorContext context) {
            return true;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="OutputTypeConverter.GetStandardValues"]/*' />
        public override System.ComponentModel.TypeConverter.StandardValuesCollection GetStandardValues(System.ComponentModel.ITypeDescriptorContext context) {
            return new StandardValuesCollection(new OutputType[] { OutputType.Exe, OutputType.Library, OutputType.WinExe });
        }
    }
/// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DebugModeConverter"]/*' />
    public class DebugModeConverter : TypeConverter {
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DebugModeConverter.CanConvertFrom"]/*' />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) {
            if (sourceType == typeof(string)) return true;

            return base.CanConvertFrom(context, sourceType);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DebugModeConverter.ConvertFrom"]/*' />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) {
            string str = value as string;

            if (str != null) {
                if (str == SR.GetString(SR.Program, culture)) return DebugMode.Program;

                if (str == SR.GetString(SR.Project, culture)) return DebugMode.Project;

                if (str == SR.GetString(SR.URL, culture)) return DebugMode.URL;
            }

            return base.ConvertFrom(context, culture, value);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DebugModeConverter.ConvertTo"]/*' />
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType) {
            if (destinationType == typeof(string)) {
                string result = SR.GetString(((DebugMode)value).ToString(), culture);

                if (result != null) return result;
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DebugModeConverter.GetStandardValuesSupported"]/*' />
        public override bool GetStandardValuesSupported(System.ComponentModel.ITypeDescriptorContext context) {
            return true;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="DebugModeConverter.GetStandardValues"]/*' />
        public override System.ComponentModel.TypeConverter.StandardValuesCollection GetStandardValues(System.ComponentModel.ITypeDescriptorContext context) {
            return new StandardValuesCollection(new DebugMode[] { DebugMode.Program, DebugMode.Project, DebugMode.URL });
        }
    }
/// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildActionConverter"]/*' />
    public class BuildActionConverter : TypeConverter {
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildActionConverter.CanConvertFrom"]/*' />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) {
            if (sourceType == typeof(string)) return true;

            return base.CanConvertFrom(context, sourceType);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildActionConverter.ConvertFrom"]/*' />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) {
            string str = value as string;

            if (str != null) {
                if (str == SR.GetString(SR.Compile, culture)) return BuildAction.Compile;

                if (str == SR.GetString(SR.Content, culture)) return BuildAction.Content;

                if (str == SR.GetString(SR.EmbeddedResource, culture)) return BuildAction.EmbeddedResource;

                if (str == SR.GetString(SR.None, culture)) return BuildAction.None;
            }

            return base.ConvertFrom(context, culture, value);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildActionConverter.ConvertTo"]/*' />
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType) {
            if (destinationType == typeof(string)) {
                string result = SR.GetString(((BuildAction)value).ToString(), culture);

                if (result != null) return result;
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildActionConverter.GetStandardValuesSupported"]/*' />
        public override bool GetStandardValuesSupported(System.ComponentModel.ITypeDescriptorContext context) {
            return true;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildActionConverter.GetStandardValues"]/*' />
        public override System.ComponentModel.TypeConverter.StandardValuesCollection GetStandardValues(System.ComponentModel.ITypeDescriptorContext context) {
            return new StandardValuesCollection(new BuildAction[] { BuildAction.Compile, BuildAction.Content, BuildAction.EmbeddedResource, BuildAction.None });
        }
    }

/// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildActionConverter"]/*' />
    public class BuildVerbosityConverter : TypeConverter
    {
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildVerbosityConverter.CanConvertFrom"]/*' />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            if (sourceType == typeof(string)) return true;

            return base.CanConvertFrom(context, sourceType);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildVerbosityConverter.ConvertFrom"]/*' />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            string str = value as string;

            if (str == null)
                return BuildVerbosity.Normal;
            if (str == SR.GetString(SR.Normal, culture))
                return BuildVerbosity.Normal;
            if (str == SR.GetString(SR.Quiet, culture))
                return BuildVerbosity.Quiet;
            if (str == SR.GetString(SR.Minimal, culture))
                return BuildVerbosity.Minimal;
            if (str == SR.GetString(SR.Detailed, culture))
                return BuildVerbosity.Detailed;
            if (str == SR.GetString(SR.Diagnostic, culture))
                return BuildVerbosity.Diagnostic;

            return base.ConvertFrom(context, culture, value);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildVerbosityConverter.ConvertTo"]/*' />
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                string result = SR.GetString(((BuildVerbosity)value).ToString(), culture);

                if (result != null) return result;
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildVerbosityConverter.GetStandardValuesSupported"]/*' />
        public override bool GetStandardValuesSupported(System.ComponentModel.ITypeDescriptorContext context)
        {
            return true;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="BuildVerbosityConverter.GetStandardValues"]/*' />
        public override System.ComponentModel.TypeConverter.StandardValuesCollection GetStandardValues(System.ComponentModel.ITypeDescriptorContext context)
        {
            return new StandardValuesCollection(new BuildVerbosity[] { BuildVerbosity.Quiet, BuildVerbosity.Minimal, BuildVerbosity.Normal, BuildVerbosity.Detailed, BuildVerbosity.Diagnostic });
        }
    }
    
    /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PlatformTypeConverter"]/*' />
    public class PlatformTypeConverter : TypeConverter {
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PlatformTypeConverter.CanConvertFrom"]/*' />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) {
            if (sourceType == typeof(string)) return true;

            return base.CanConvertFrom(context, sourceType);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PlatformTypeConverter.ConvertFrom"]/*' />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) {
            string str = value as string;

            if (str != null) {
                if (str == SR.GetString(SR.v1, culture)) return PlatformType.v1;

                if (str == SR.GetString(SR.v11, culture)) return PlatformType.v11;

                if (str == SR.GetString(SR.v2, culture)) return PlatformType.v2;

                if (str == SR.GetString(SR.cli1, culture)) return PlatformType.cli1;
            }

            return base.ConvertFrom(context, culture, value);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PlatformTypeConverter.ConvertTo"]/*' />
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType) {
            if (destinationType == typeof(string)) {
                string result = SR.GetString(((PlatformType)value).ToString(), culture);

                if (result != null) return result;
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PlatformTypeConverter.GetStandardValuesSupported"]/*' />
        public override bool GetStandardValuesSupported(System.ComponentModel.ITypeDescriptorContext context) {
            return true;
        }
        /// <include file='doc\PropertyPages.uex' path='docs/doc[@for="PlatformTypeConverter.GetStandardValues"]/*' />
        public override System.ComponentModel.TypeConverter.StandardValuesCollection GetStandardValues(System.ComponentModel.ITypeDescriptorContext context) {
            return new StandardValuesCollection(new PlatformType[] { PlatformType.v1, PlatformType.v11, PlatformType.v2, PlatformType.cli1 });
        }
    }
}
