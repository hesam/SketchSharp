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
  [TypeConverter(typeof(OutputTypeConverter))]
  public enum OutputType{Library, WinExe, Exe}
  [TypeConverter(typeof(DebugModeConverter))]
  public enum DebugMode{Project, Program, URL}
  [TypeConverter(typeof(BuildActionConverter))]
  public enum BuildAction{None, Compile, Content, EmbeddedResource}
  [TypeConverter(typeof(PlatformTypeConverter))]
  public enum PlatformType{notSpecified, v1, v11, v12, cli1}

  [Flags]
  public enum PropPageStatus {
    Dirty = 0x1,
    Validate = 0x2,
    Clean = 0x4
  }


  public class LocalizableProperties : ICustomTypeDescriptor
  {      
    #region ICustomTypeDescriptor
    /// <summary>
    ///     Delegates to TypeDescriptor.
    /// </summary>
    public System.ComponentModel.AttributeCollection GetAttributes() 
    {
      return TypeDescriptor.GetAttributes( this, true );
    }
        
    /// <summary>
    ///     Delegates to TypeDescriptor.
    /// </summary>
    public EventDescriptor GetDefaultEvent() 
    {
      return TypeDescriptor.GetDefaultEvent( this, true );
    }

    /// <summary>
    ///     Delegates to TypeDescriptor.
    /// </summary>
    public PropertyDescriptor GetDefaultProperty() 
    {
      return TypeDescriptor.GetDefaultProperty( this, true );
    }

    /// <summary>
    ///      Retrieves the an editor for this object.
    /// </summary>
    public object GetEditor(Type editorBaseType) 
    {
      return TypeDescriptor.GetEditor( this, editorBaseType, true );
    }

    /// <summary>
    ///     Delegates to TypeDescriptor.
    /// </summary>
    public EventDescriptorCollection GetEvents() 
    {
      return TypeDescriptor.GetEvents( this, true );
    }

    /// <summary>
    ///     Delegates to TypeDescriptor.
    /// </summary>
    public EventDescriptorCollection GetEvents(System.Attribute[] attributes) 
    {
      return TypeDescriptor.GetEvents( this, attributes, true );
    }

    /// <summary>
    ///     Returns the browsable object.
    /// </summary>
    public object GetPropertyOwner(PropertyDescriptor pd) 
    {
      return this;
    }

    /// <summary>
    /// Returns the properties for selected object using the attribute array as a
    /// filter.
    /// </summary>
    public PropertyDescriptorCollection GetProperties() 
    {
      return GetProperties(null);
    }

    /// <summary>
    /// Returns the properties for selected object using the attribute array as a
    /// filter.
    /// </summary>
    public PropertyDescriptorCollection GetProperties(System.Attribute[] attributes)
    {
      ArrayList newList = new ArrayList();
      PropertyDescriptorCollection props = TypeDescriptor.GetProperties(this, attributes, true);
      for (int i = 0; i < props.Count; i++) 
        newList.Add( new DesignPropertyDescriptor(props[i]) );
      return new PropertyDescriptorCollection((PropertyDescriptor[])newList.ToArray(typeof(PropertyDescriptor)));;
    }
 
    /// <summary>
    ///  Get the name of the component.
    /// </summary>
    public string GetComponentName()
    {
      return TypeDescriptor.GetComponentName( this, true );
    }

    /// <summary>
    ///      Retrieves the type converter for this object.
    /// </summary>
    public TypeConverter GetConverter() 
    {
      return TypeDescriptor.GetConverter( this, true );
    }

    /// <summary>
    ///     Delegates to TypeDescriptor.
    /// </summary>
    public virtual string GetClassName() 
    {
      return "CustomObject";
    }

    #endregion ICustomTypeDescriptor
  }

  //=========================================================================
  // TODO: This actually works when you "open" the project from within VS
  // but it doesn't update when you select different nodes in the tree.
  // We also need different wrappers for different node types and we need
  // to localize the category names and descriptions.
  public class NodeProperties : LocalizableProperties, ISpecifyPropertyPages, IVsGetCfgProvider
  { 
    public HierarchyNode Node;
    BuildAction build;
    ProjectManager project;
    string tool;
    string toolNs;

    public NodeProperties(HierarchyNode node) {
      this.Node = node;
      project = node.ProjectMgr;
      build = (BuildAction)Enum.Parse(typeof(BuildAction), GetProperty("*/*/Files/@BuildAction", "None"));
      tool = GetProperty("*/*/Files/@CustomTool", "");
      toolNs = GetProperty("*/*/Files/@CustomToolNamespace", "");
    }

    #region ISpecifyPropertyPages methods
    public virtual void GetPages(CAUUID[] ppages) {
      ppages[0] = new CAUUID();
      if (Node == Node.ProjectMgr) {
        Guid[] guids = Node.GetPropertyPageGuids();        
        ppages[0].cElems = (uint)guids.Length;
        int size = Marshal.SizeOf(typeof(Guid));
        ppages[0].pElems = Marshal.AllocCoTaskMem(guids.Length*size);
        IntPtr ptr = ppages[0].pElems;
        for (int i = 0; i < guids.Length; i++) {      
          Marshal.StructureToPtr(guids[i], ptr, false);
          ptr = new IntPtr(ptr.ToInt64() + size);
        }
      } else {
        ppages[0].cElems = 0;
      }
    }
    #endregion 

    #region IVsGetCfgProvider methods
    public virtual void GetCfgProvider(out IVsCfgProvider p) {
      ((IVsGetCfgProvider)Node.ProjectMgr).GetCfgProvider(out p);
    }
    #endregion 

    [LocCategory(UIStringNames.Advanced)]
    [DisplayName(UIStringNames.BuildAction)]
    [LocDescription(UIStringNames.BuildActionDescription)]
    public BuildAction BuildAction 
    {
      get 
      {
        return build;
      }
      set 
      {
        build = value;
        SetProperty("*/*/Files/@BuildAction", value.ToString());
      }
    }

    [LocCategory(UIStringNames.Advanced)]
    [DisplayName(UIStringNames.CustomTool)]
    [LocDescription(UIStringNames.CustomToolDescription)]
    public string CustomTool 
    {
      get 
      {
        return tool;
      }
      set 
      {
        tool = value;
        SetProperty("*/*/Files/@CustomTool", value);
      }
    }

    [LocCategory(UIStringNames.Advanced)]
    [DisplayName(UIStringNames.CustomToolNamespace)]
    [LocDescription(UIStringNames.CustomToolNamespaceDescription)]
    public string CustomToolNamespace 
    {
      get 
      {
        return toolNs;
      }
      set 
      {
        toolNs = value;
        SetProperty("*/*/Files/@CustomToolNamespace", value);
      }
    }
    [LocCategory(UIStringNames.Misc)]
    [DisplayName(UIStringNames.FileName)]
    [LocDescription(UIStringNames.FileNameDescription)]
    public string FileName 
    {
      get 
      {
        return Node.Caption;
      }
      // todo: this should also be settable, and then it renames the file
      // both in the project and on disk, updating the editor caption if the file is open, etc.
    }
    [LocCategory(UIStringNames.Misc)]
    [DisplayName(UIStringNames.FullPath)]
    [LocDescription(UIStringNames.FullPathDescription)]
    public string FullPath
    {
      get 
      {
        return Node.Url;
      }
    }

    string GetProperty(string path, string def) 
    {
      if (project != null) 
      {
        XmlNode n = project.StateElement.SelectSingleNode(path);
        if (n != null) 
        {
          return n.InnerText;
        }
      }
      return def;
    }
    void SetProperty(string path, string value) 
    {
      if (project != null) 
      {
        XmlNode n = project.StateElement.SelectSingleNode(path);
        if (n != null) 
        {
          n.InnerText = value;
        }
      }
    }
  }
  /////////////////////////////////////////////////////////////////////////////////////////////



  /////////////////////////////////////////////////////////////////////////////////////////////
  ///
  ///   The base class for property pages
  ///
  /////////////////////////////////////////////////////////////////////////////////////////////
  public abstract class SettingsPage : LocalizableProperties, IPropertyPage {
    Panel panel;
    protected NodeProperties[] nodes;
    protected ProjectManager project;
    protected ProjectConfig[] projectConfigs;
    protected IVSMDPropertyGrid grid;
    bool active;
    public string strName;

    static Guid SID_SVSMDPropertyBrowser = new Guid("74946810-37A0-11D2-A273-00C04F8EF4FF");

    [BrowsableAttribute(false)]
    public ProjectManager ProjectMgr {
      get { 
        if (this.project != null) {
          return this.project; 
        } else if (this.projectConfigs != null) {
          return this.projectConfigs[0].ProjectMgr;
        }
        return null;
      }
    }


    protected void UpdateObjects() {      
      if ((this.nodes != null && this.nodes.Length>0) || this.projectConfigs != null) {
        IntPtr p = Marshal.GetIUnknownForObject(this);
        IntPtr ppUnk = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(IntPtr)));
        Marshal.WriteIntPtr(ppUnk, p);
        BindProperties();
        // BUGBUG -- this is really bad casting a pointer to "int"...
        this.grid.SetSelectedObjects(1, ppUnk.ToInt32());    
        Marshal.FreeCoTaskMem(ppUnk); 
        Marshal.Release(p);
        this.grid.Refresh(); 
      }
    }

    public abstract void BindProperties();
    public abstract void ApplyChanges();

    private bool dirty = false;

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
    public virtual void Activate(IntPtr parent, RECT[] pRect, int bModal) {

      if (this.panel == null) {
        this.panel = new Panel();        
        this.panel.Size = new Size(pRect[0].right - pRect[0].left, pRect[0].bottom - pRect[0].top);
        this.panel.Text = "Settings";// TODO localization
        this.panel.Visible = false;
        this.panel.Size = new Size(550,300);
        this.panel.CreateControl();        
        NativeWindowHelper.SetParent(this.panel.Handle, parent);
      }      

      if (this.grid == null){
        IVSMDPropertyBrowser pb = (IVSMDPropertyBrowser)project.Site.QueryService(SID_SVSMDPropertyBrowser, typeof(IVSMDPropertyBrowser));
        this.grid = pb.CreatePropertyGrid();
      }

      this.active = true;

      Control cGrid = Control.FromHandle(new IntPtr(this.grid.Handle));
      cGrid.Parent = Control.FromHandle(parent);//this.panel;
      cGrid.Size = new Size(544,294);
      cGrid.Location = new Point(3,3);
      cGrid.Visible = true;

      this.grid.SetOption(_PROPERTYGRIDOPTION.PGOPT_TOOLBAR, false);
      this.grid.GridSort = _PROPERTYGRIDSORT.PGSORT_CATEGORIZED;      

      NativeWindowHelper.SetParent(new IntPtr(this.grid.Handle), this.panel.Handle);

      UpdateObjects();
    }

    public virtual void Apply() {
      if (IsDirty) ApplyChanges();
    }

    public virtual void Deactivate() {
      this.panel.Dispose();
      this.active = false;
    }

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

    public virtual void Help(string helpDir) {
    }

    public virtual int IsPageDirty() {
        // Note this returns an HRESULT not a Bool.
        return (IsDirty ? (int)HResult.S_OK : (int)HResult.S_FALSE);
    }

    public virtual void Move(RECT[] arrRect) {
      RECT r = arrRect[0]; 
      this.panel.Location = new Point(r.left, r.top);
      this.panel.Size = new Size(r.right - r.left, r.bottom - r.top);
    }

    public virtual void SetObjects(uint count, object[] punk) {
      if (count>0) {
        // check the kind.
        if (punk[0] is NodeProperties) {
          this.nodes = new NodeProperties[count];
          System.Array.Copy(punk, 0, this.nodes, 0, (int)count);
          if (this.nodes != null && this.nodes.Length>0) {
            this.project = this.nodes[0].Node.ProjectMgr;
          }
        }
        else if (punk[0] is ProjectConfig) {
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
    public virtual void SetPageSite(IPropertyPageSite site) {
      _site = site;      
    }

    public virtual void Show(uint cmd) {
      this.panel.Visible = true; // TODO: pass SW_SHOW* flags through      
      this.panel.Show();
    }


    public virtual int TranslateAccelerator(MSG[] arrMsg) {
      MSG msg = arrMsg[0]; 
      if ((msg.message < NativeWindowHelper.WM_KEYFIRST || msg.message > NativeWindowHelper.WM_KEYLAST) &&
        (msg.message < NativeWindowHelper.WM_MOUSEFIRST || msg.message > NativeWindowHelper.WM_MOUSELAST))
        return 1;

      return (NativeWindowHelper.IsDialogMessageA(this.panel.Handle, ref msg)) ? 0 : 1;
    }
    #endregion 

    public string GetProperty(string path) {
      if (this.ProjectMgr != null) {
        XmlNode n = this.ProjectMgr.StateElement.SelectSingleNode(path);
        if (n != null) {
          return n.InnerText;
        }
      }
      return String.Empty;
    }

    // relative to active configuration.
    public string GetConfigProperty(string path) {
      if (this.ProjectMgr != null) {
        string unifiedResult = null;
        for (int i = 0, n = this.projectConfigs.Length; i < n; i++) {
          ProjectConfig config = projectConfigs[i];
          XmlNode node = config.Node.SelectSingleNode(path);
          if (node != null) {
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
    public void SetConfigProperty(string attrName, string value) {
      CCITracing.TraceCall();
      if (this.ProjectMgr != null) {
        for (int i = 0, n = this.projectConfigs.Length; i < n; i++) {
          ProjectConfig config = projectConfigs[i];
          if (value == null) {
            value = "";
          }
          config.Node.SetAttribute(attrName, value);          
        }
        this.ProjectMgr.SetProjectFileDirty(true);
      }
    }
  }

  [ComVisible(true), Guid("9864D4AD-569A-4daf-8CBC-548F6E24C111")]
  public class GeneralPropertyPage : SettingsPage{
    private string assemblyName;
    private OutputType outputType;
    private string defaultNamespace;
    private string startupObject;
    private string applicationIcon;
    private PlatformType targetPlatform = PlatformType.v11;
    private string targetPlatformLocation;
   
    public GeneralPropertyPage() {
      this.strName = UIStrings.GetString(UIStringNames.GeneralCaption);
    }

    public override string GetClassName(){
      return this.GetType().FullName;
    }
    public override void BindProperties(){
      XmlElement project = this.ProjectMgr.StateElement;
      if (project == null) {Debug.Assert(false); return;}
      XmlElement settings = (XmlElement)project.SelectSingleNode("Build/Settings");
      if (settings == null) {Debug.Assert(false); return;}
      this.assemblyName = settings.GetAttribute("AssemblyName");
      string outputType = settings.GetAttribute("OutputType");
      if (outputType != null && outputType.Length > 0)
        try{this.outputType = (OutputType)Enum.Parse(typeof(OutputType), outputType);} catch {} //Should only fail if project file is corrupt
      this.defaultNamespace = settings.GetAttribute("RootNamespace");
      this.startupObject = settings.GetAttribute("StartupObject");
      this.applicationIcon = settings.GetAttribute("ApplicationIcon");
      string targetPlatform = settings.GetAttribute("TargetPlatform");
      if (targetPlatform != null && targetPlatform.Length > 0)
        try {this.targetPlatform = (PlatformType)Enum.Parse(typeof(PlatformType), targetPlatform);} catch{}
      this.targetPlatformLocation = settings.GetAttribute("TargetPlatformLocation");
    }

    public override void ApplyChanges(){
      XmlElement project = this.ProjectMgr.StateElement;
      if (project == null) {Debug.Assert(false); return;}
      XmlElement settings = (XmlElement)project.SelectSingleNode("Build/Settings");
      if (settings == null) {Debug.Assert(false); return;}
      settings.SetAttribute("AssemblyName", this.assemblyName);
      settings.SetAttribute("OutputType", this.outputType.ToString());
      settings.SetAttribute("RootNamespace", this.defaultNamespace);
      settings.SetAttribute("StartupObject", this.startupObject);
      settings.SetAttribute("ApplicationIcon", this.applicationIcon);
      settings.SetAttribute("TargetPlatform", this.targetPlatform.ToString());
      settings.SetAttribute("TargetPlatformLocation", this.targetPlatformLocation.ToString());
      this.IsDirty = false;
    } 
    [LocCategory(UIStringNames.Application)]
    [DisplayName(UIStringNames.AssemblyName)]
    [LocDescription(UIStringNames.AssemblyNameDescription)]    
    public string AssemblyName{
      get {return this.assemblyName;}
      set {this.assemblyName = value; this.IsDirty = true;}
    }
    [LocCategory(UIStringNames.Application)]
    [DisplayName(UIStringNames.OutputType)]
    [LocDescription(UIStringNames.OutputTypeDescription)]
    public OutputType OutputType{
      get {return this.outputType;}
      set {this.outputType = value; this.IsDirty = true;}
    }
    [LocCategory(UIStringNames.Application)]
    [DisplayName(UIStringNames.DefaultNamespace)]
    [LocDescription(UIStringNames.DefaultNamespaceDescription)]
    public string DefaultNamespace{
      get {return  this.defaultNamespace;}
      set {this.defaultNamespace = value; this.IsDirty = true;}
    }
    [LocCategory(UIStringNames.Application)]
    [DisplayName(UIStringNames.StartupObject)]
    [LocDescription(UIStringNames.StartupObjectDescription)]
    public string StartupObject{
      get {return this.startupObject;}
      set {this.startupObject = value; this.IsDirty = true;}
    }
    [LocCategory(UIStringNames.Application)]
    [DisplayName(UIStringNames.ApplicationIcon)]
    [LocDescription(UIStringNames.ApplicationIconDescription)]
    public string ApplicationIcon{
      get {return this.applicationIcon;}
      set {this.applicationIcon = value; this.IsDirty = true;}
    }
    [LocCategory(UIStringNames.Project)]
    [DisplayName(UIStringNames.ProjectFile)]
    [LocDescription(UIStringNames.ProjectFileDescription)]
    public string ProjectFile{
      get {return Path.GetFileName(this.ProjectMgr.ProjectFile);}
    }
    [LocCategory(UIStringNames.Project)]
    [DisplayName(UIStringNames.ProjectFolder)]
    [LocDescription(UIStringNames.ProjectFolderDescription)]
    public string ProjectFolder{
      get {return Path.GetDirectoryName(this.ProjectMgr.ProjectFolder);}
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
    [LocCategory(UIStringNames.Project)]
    [DisplayName(UIStringNames.TargetPlatform)]
    [LocDescription(UIStringNames.TargetPlatformDescription)]
    public PlatformType TargetPlatform{
      get {return this.targetPlatform;}
      set {this.targetPlatform = value; IsDirty = true;}
    }
    [LocCategory(UIStringNames.Project)]
    [DisplayName(UIStringNames.TargetPlatformLocation)]
    [LocDescription(UIStringNames.TargetPlatformLocationDescription)]
    public string TargetPlatformLocation  {
      get {return this.targetPlatformLocation;}
      set {this.targetPlatformLocation = value; IsDirty = true;}
    }
  }

  /////////////////////////////////////////////////////////////////////////////////////////////
  ///
  ///   The debug property page dialog
  ///
  /////////////////////////////////////////////////////////////////////////////////////////////
  [ComVisible(true), Guid("5BC9517D-EF54-4b12-A617-EB38B1F38250")]
  public sealed class DebugPropertyPage : SettingsPage{   
    public DebugPropertyPage() {
      this.strName = UIStrings.GetString(UIStringNames.DebugCaption);
    }

    public override string GetClassName() {
      CCITracing.TraceCall();
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

    public override void BindProperties() {
      // TODO: figure out where to get these options from...
      try  {_DebugMode = (DebugMode) DebugMode.Parse(typeof(DebugMode), GetConfigProperty("@DebugMode")) ; } catch  {};
      _StartApplication = GetConfigProperty("@StartProgram"); 
      _StartURL = GetConfigProperty("@StartURL"); 
      _StartPage = GetConfigProperty("@StartPage"); 
      _CommandLineArguments = GetConfigProperty("@CmdArgs"); 
      _WorkingDirectory = GetConfigProperty("@WorkingDirectory"); 
       try { _UseInternetExplorer = bool.Parse(GetConfigProperty("@UseIE")); } catch {}
       try { _EnableRemoteDebugging = bool.Parse(GetConfigProperty("@EnableRemoteDebugging")); } catch {}
      _RemoteDebugMachine = GetConfigProperty("@RemoteDebugMachine");       
    }


    public override void ApplyChanges() {
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
    public DebugMode DebugMode { 
      get { return _DebugMode; }
      set { _DebugMode = value; IsDirty = true;}
    }
    */
    [LocCategory(UIStringNames.StartAction)]
    [DisplayName(UIStringNames.StartApplication)]
    [LocDescription(UIStringNames.StartApplicationDescription)]
    public string StartApplication {
      get { return _StartApplication; }
      set { _StartApplication = value; IsDirty = true;}
    }
    /**** temporarily removed
    [LocCategory(UIStringNames.StartAction)]
    [DisplayName(UIStringNames.StartURL)]
    [LocDescription(UIStringNames.StartURLDescription)]
    public string StartURL {
      get { return _StartURL; }
      set { _StartURL = value; IsDirty = true;}
    }
    [LocCategory(UIStringNames.StartAction)]
    [DisplayName(UIStringNames.StartPage)]
    [LocDescription(UIStringNames.StartPageDescription)]
    public string StartPage {
      get { return _StartPage; }
      set { _StartPage = value; IsDirty = true;}
    }
    */
    [LocCategory(UIStringNames.StartOptions)]
    [DisplayName(UIStringNames.CommandLineArguments)]
    [LocDescription(UIStringNames.CommandLineArgumentsDescription)]
    public string CommandLineArguments {
      get { return _CommandLineArguments; }
      set { _CommandLineArguments = value; IsDirty = true;}
    }
    [LocCategory(UIStringNames.StartOptions)]
    [DisplayName(UIStringNames.WorkingDirectory)]
    [LocDescription(UIStringNames.WorkingDirectoryDescription)]
    public string WorkingDirectory {
      get { return _WorkingDirectory; }
      set { _WorkingDirectory = value; IsDirty = true;}
    }
    /**** temporarily removed
    [LocCategory(UIStringNames.StartOptions)]
    [DisplayName(UIStringNames.UseInternetExplorer)]
    [LocDescription(UIStringNames.UseInternetExplorerDescription)]
    public bool UseInternetExplorer {
      get { return _UseInternetExplorer; }
      set { _UseInternetExplorer = value; IsDirty = true;}
    }
    [LocCategory(UIStringNames.StartOptions)]
    [DisplayName(UIStringNames.EnableRemoteDebugging)]
    [LocDescription(UIStringNames.EnableRemoteDebuggingDescription)]
    public bool EnableRemoteDebugging  {
      get { return _EnableRemoteDebugging; }
      set { _EnableRemoteDebugging = value; IsDirty = true;}
    }
    */
    [LocCategory(UIStringNames.StartOptions)]
    [DisplayName(UIStringNames.RemoteDebugMachine)]
    [LocDescription(UIStringNames.RemoteDebugMachineDescription)]
    public string RemoteDebugMachine {
      get { return _RemoteDebugMachine; }
      set { _RemoteDebugMachine = value; IsDirty = true;}
    }
  }

  /////////////////////////////////////////////////////////////////////////////////////////////
  ///
  ///   The build property page dialog
  ///
  /////////////////////////////////////////////////////////////////////////////////////////////
  [ComVisible(true), Guid("873D1121-908A-433e-9135-06F248149EC5")]
  public class BuildPropertyPage : SettingsPage {
    public BuildPropertyPage() {
      this.strName = UIStrings.GetString(UIStringNames.BuildCaption);
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

    public override void BindProperties() {
      _DefineConstants = GetConfigProperty("@DefineConstants");
      try { _OptimizeCode = bool.Parse(GetConfigProperty("@Optimize")); } catch {}
      try { _CheckArithmeticOverflow = bool.Parse(GetConfigProperty("@CheckForOverflowUnderflow")); } catch {}
      try { _AllowUnsafeCode = bool.Parse(GetConfigProperty("@AllowUnsafeBlocks")); } catch {}
      try { _WarningLevel = int.Parse(GetConfigProperty("@WarningLevel")); } catch {}
      try { _TreatWarningsAsErrors = bool.Parse(GetConfigProperty("@TreatWarningsAsErrors")); } catch {}
      _OutputPath = GetConfigProperty("@OutputPath"); 
      _XMLDocumentationFile = GetConfigProperty("@DocumentationFile");
      try { _GenerateDebuggingInformation = bool.Parse(GetConfigProperty("@DebugSymbols")); } catch {}
      try { _RegisterForComInterop = bool.Parse(GetConfigProperty("@RegisterForComInterop")); } catch {}

    }
    public override void ApplyChanges() {
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

    public override string GetClassName() {
      CCITracing.TraceCall();
      return this.GetType().FullName;
    }
    [LocCategory(UIStringNames.CodeGeneration)]
    [DisplayName(UIStringNames.DefineConstants)]
    [LocDescription(UIStringNames.DefineConstantsDescription)]
    public string DefineConstants { 
      get { return _DefineConstants; }
      set { _DefineConstants = value; IsDirty = true; }
    }
    [LocCategory(UIStringNames.CodeGeneration)]
    [DisplayName(UIStringNames.OptimizeCode)]
    [LocDescription(UIStringNames.OptimizeCodeDescription)]
    public bool OptimizeCode { 
      get { return _OptimizeCode;  }
      set {  _OptimizeCode = value;  IsDirty = true; }
    }
    [LocCategory(UIStringNames.CodeGeneration)]
    [DisplayName(UIStringNames.CheckArithmeticOverflow)]
    [LocDescription(UIStringNames.CheckArithmeticOverflowDescription)]
    public bool CheckArithmeticOverflow { 
      get { return _CheckArithmeticOverflow; }
      set { _CheckArithmeticOverflow = value; IsDirty = true; }
    }
    /**** temporarily removed
    [LocCategory(UIStringNames.CodeGeneration)]
    [DisplayName(UIStringNames.AllowUnsafeCode)]
    [LocDescription(UIStringNames.AllowUnsafeCodeDescription)]
    public bool AllowUnsafeCode { 
      get { return _AllowUnsafeCode; }
      set { _AllowUnsafeCode = value; IsDirty = true; }
    }
    */
    [LocCategory(UIStringNames.ErrorsAndWarnings)]
    [DisplayName(UIStringNames.WarningLevel)]
    [LocDescription(UIStringNames.WarningLevelDescription)]
    public int WarningLevel { 
      get { return _WarningLevel; }
      set { _WarningLevel = value; IsDirty = true; }
    }
    [LocCategory(UIStringNames.ErrorsAndWarnings)]
    [DisplayName(UIStringNames.TreatWarningsAsErrors)]
    [LocDescription(UIStringNames.TreatWarningsAsErrorsDescription)]
    public bool TreatWarningsAsErrors { 
      get { return _TreatWarningsAsErrors; }
      set { _TreatWarningsAsErrors = value; IsDirty = true; }
    }

    [LocCategory(UIStringNames.Outputs)]
    [DisplayName(UIStringNames.OutputPath)]
    [LocDescription(UIStringNames.OutputPathDescription)]
    public string OutputPath { 
      get { return _OutputPath; }
      set { _OutputPath = value; IsDirty = true; }
    }
    [LocCategory(UIStringNames.Outputs)]
    [DisplayName(UIStringNames.XMLDocumentationFile)]
    [LocDescription(UIStringNames.XMLDocumentationFileDescription)]
    public string XMLDocumentationFile { 
      get { return _XMLDocumentationFile;  }
      set { _XMLDocumentationFile = value; IsDirty = true; }
    }

    [LocCategory(UIStringNames.Outputs)]
    [DisplayName(UIStringNames.GenerateDebuggingInformation)]
    [LocDescription(UIStringNames.GenerateDebuggingInformationDescription)]
    public bool GenerateDebuggingInformation { 
      get { return _GenerateDebuggingInformation; }
      set { _GenerateDebuggingInformation = value;  IsDirty = true; }
    }
    /**** temporarily removed
    [LocCategory(UIStringNames.Outputs)]
    [DisplayName(UIStringNames.RegisterForCOMInterop)]
    [LocDescription(UIStringNames.RegisterForCOMInteropDescription)]
    public bool RegisterForComInterop  { 
      get { return _RegisterForComInterop; }
      set { _RegisterForComInterop = value;  IsDirty = true; }
    }
    */
  }

  /////////////////////////////////////////////////////////////////////////////////////////////
  ///
  ///   The advanced property page dialog
  ///
  /////////////////////////////////////////////////////////////////////////////////////////////
  [ComVisible(true), Guid("3f5e7baa-7f96-4e64-8dce-9593ab90e996")]
  public class AdvancedPropertyPage : SettingsPage {
   
    public AdvancedPropertyPage() {
      this.strName = UIStrings.GetString(UIStringNames.Advanced);
    }

    bool _IncrementalBuild;
    long _BaseAddress;
    int _FileAlignment;

    public override void BindProperties() {
      try { _IncrementalBuild= bool.Parse(GetConfigProperty("@IncrementalBuild")); } catch {}
      try { _BaseAddress = long.Parse(GetConfigProperty("@BaseAddress")); } catch {}
      try { _FileAlignment = int.Parse(GetConfigProperty("@FileAlignment")); } catch {}
    }
    public override void ApplyChanges() {

      SetConfigProperty("IncrementalBuild", _IncrementalBuild.ToString());
      SetConfigProperty("BaseAddress", _BaseAddress.ToString());
      SetConfigProperty("FileAlignment", _FileAlignment.ToString());
      IsDirty = false;
    }

    public override string GetClassName() {
      CCITracing.TraceCall();
      return this.GetType().FullName;
    }
    /**** temporarily removed
    [LocCategory(UIStringNames.GeneralCaption)]
    [DisplayName(UIStringNames.IncrementalBuild)]
    [LocDescription(UIStringNames.IncrementalBuildDescription)]
    public bool IncrementalBuild  { 
      get { return _IncrementalBuild; }
      set { _IncrementalBuild = value; IsDirty = true; }
    }
    */
    [LocCategory(UIStringNames.GeneralCaption)]
    [DisplayName(UIStringNames.BaseAddress)]
    [LocDescription(UIStringNames.BaseAddressDescription)]
    public long BaseAddress  { 
      get { return _BaseAddress; }
      set { _BaseAddress = value;  IsDirty = true; }
    }
    [LocCategory(UIStringNames.GeneralCaption)]
    [DisplayName(UIStringNames.FileAlignment)]
    [LocDescription(UIStringNames.FileAlignmentDescription)]
    public int FileAlignment  { 
      get { return _FileAlignment; }
      set { _FileAlignment = value;  IsDirty = true; }
    }
  }

  [AttributeUsage (AttributeTargets.Property | AttributeTargets.Field,AllowMultiple = false)]
  internal sealed class LocDescriptionAttribute : DescriptionAttribute 
  {
    private bool replaced = false;
    UIStringNames name;

    public LocDescriptionAttribute(UIStringNames name) : base(name.ToString()) {
      this.name = name;
    }

    public override string Description {
      get {
        if (!replaced) {
          replaced = true;
          this.DescriptionValue = UIStrings.GetString(this.name);
        }
        return base.Description;
      }
    }
  }

  [AttributeUsage (AttributeTargets.Property | AttributeTargets.Field,AllowMultiple = false)]
  internal sealed class LocCategoryAttribute : CategoryAttribute 
  {
    UIStringNames name;

    public LocCategoryAttribute(UIStringNames name) : base(name.ToString()) {
      this.name = name;
    }
    protected override string GetLocalizedString(string value) {
      return UIStrings.GetString(this.name);
    }
  }

  [AttributeUsage (AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field,
    Inherited = false,AllowMultiple = false) ]
  internal sealed class DisplayNameAttribute : Attribute {
    UIStringNames name;

    public DisplayNameAttribute(UIStringNames name) {
      this.name = name;
    }

    public string Name {
      get { 
        string result = UIStrings.GetString(this.name);
        if (result == null) {
          Debug.Assert(false); // resource is missing!
        }
        return result;
      }
    }
  }

  /// <summary>
  /// The purpose of DesignPropertyDescriptor is to allow us to customize the
  /// display name of the property in the property grid.  None of the CLR
  /// implementations of PropertyDescriptor allow you to change the DisplayName.
  /// </summary>
  
  internal class DesignPropertyDescriptor : PropertyDescriptor {
    private string displayName; // Custom display name
    private PropertyDescriptor property;	// Base property descriptor
		
    /// <summary>
    /// Delegates to base.
    /// </summary>
    public override string DisplayName {
      get {
        return this.displayName;
      }
    }

    /// <summary>
    /// Delegates to base.
    /// </summary>
    public override Type ComponentType {
      get {
        return this.property.ComponentType;

      }
    }

    /// <summary>
    /// Delegates to base.
    /// </summary>
    public override bool IsReadOnly {
      get {
        return this.property.IsReadOnly;
      }
    }
    
    /// <summary>
    /// Delegates to base.
    /// </summary>
    public override Type PropertyType {
      get {
        return this.property.PropertyType;
      }
    }

    /// <summary>
    /// Delegates to base.
    /// </summary>
    public override bool CanResetValue(object component) {
      return this.property.CanResetValue(component);
    }

    /// <summary>
    /// Delegates to base.
    /// </summary>
    public override object GetValue(object component) {
      return this.property.GetValue(component);
    }

    /// <summary>
    /// Delegates to base.
    /// </summary>
    public override void ResetValue(object component) {
      this.property.ResetValue(component);
    }

    /// <summary>
    /// Delegates to base.
    /// </summary>
    public override void SetValue(object component, object value) {
      this.property.SetValue(component, value);
    }
    
    /// <summary>
    /// Delegates to base.
    /// </summary>
    public override bool ShouldSerializeValue(object component) {
      return this.property.ShouldSerializeValue(component);
    }

    /// <summary>
    /// Constructor.  Copy the base property descriptor and also hold a pointer
    /// to it for calling its overridden abstract methods.
    /// </summary>
    public DesignPropertyDescriptor(PropertyDescriptor prop)
      : base(prop) {
      this.property = prop;

      DisplayNameAttribute displayName= (DisplayNameAttribute)prop.Attributes[typeof(DisplayNameAttribute)];

      if (displayName != null)
        this.displayName = displayName.Name;
      else
        this.displayName = prop.Name;
    }
  }
  public class OutputTypeConverter : TypeConverter{

    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType){
      if (sourceType == typeof(string)) return true;
      return base.CanConvertFrom(context, sourceType);
    }
    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value){
      string str = value as string;
      if (str != null){
        if (str == UIStrings.GetString(UIStringNames.Exe, culture)) return OutputType.Exe;
        if (str == UIStrings.GetString(UIStringNames.Library, culture)) return OutputType.Library;
        if (str == UIStrings.GetString(UIStringNames.WinExe, culture)) return OutputType.WinExe;
      }
      return base.ConvertFrom(context, culture, value);
    }
    public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType){  
      if (destinationType == typeof(string)){
        string result = UIStrings.GetString(((OutputType)value).ToString(), culture);
        if (result != null) return result;
      }
      return base.ConvertTo(context, culture, value, destinationType);
    }
    public override bool GetStandardValuesSupported(System.ComponentModel.ITypeDescriptorContext context){
      return true;
    }
    public override System.ComponentModel.TypeConverter.StandardValuesCollection GetStandardValues(System.ComponentModel.ITypeDescriptorContext context){        
      return new StandardValuesCollection(new OutputType[]{OutputType.Exe, OutputType.Library, OutputType.WinExe});       
    }
  }
  public class DebugModeConverter : TypeConverter{

    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType){
      if (sourceType == typeof(string)) return true;
      return base.CanConvertFrom(context, sourceType);
    }
    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value){
      string str = value as string;
      if (str != null){
        if (str == UIStrings.GetString(UIStringNames.Program, culture)) return DebugMode.Program;
        if (str == UIStrings.GetString(UIStringNames.Project, culture)) return DebugMode.Project;
        if (str == UIStrings.GetString(UIStringNames.URL, culture)) return DebugMode.URL;
      }
      return base.ConvertFrom(context, culture, value);
    }
    public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType){  
      if (destinationType == typeof(string)){
        string result = UIStrings.GetString(((DebugMode)value).ToString(), culture);
        if (result != null) return result;
      }
      return base.ConvertTo(context, culture, value, destinationType);
    }
    public override bool GetStandardValuesSupported(System.ComponentModel.ITypeDescriptorContext context){
      return true;
    }
    public override System.ComponentModel.TypeConverter.StandardValuesCollection GetStandardValues(System.ComponentModel.ITypeDescriptorContext context){        
      return new StandardValuesCollection(new DebugMode[]{DebugMode.Program, DebugMode.Project, DebugMode.URL});       
    }
  }
  public class BuildActionConverter : TypeConverter{

    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType){
      if (sourceType == typeof(string)) return true;
      return base.CanConvertFrom(context, sourceType);
    }
    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value){
      string str = value as string;
      if (str != null){
        if (str == UIStrings.GetString(UIStringNames.Compile, culture)) return BuildAction.Compile;
        if (str == UIStrings.GetString(UIStringNames.Content, culture)) return BuildAction.Content;
        if (str == UIStrings.GetString(UIStringNames.EmbeddedResource, culture)) return BuildAction.EmbeddedResource;
        if (str == UIStrings.GetString(UIStringNames.None, culture)) return BuildAction.None;
      }
      return base.ConvertFrom(context, culture, value);
    }
    public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType){  
      if (destinationType == typeof(string)){
        string result = UIStrings.GetString(((BuildAction)value).ToString(), culture);
        if (result != null) return result;
      }
      return base.ConvertTo(context, culture, value, destinationType);
    }
    public override bool GetStandardValuesSupported(System.ComponentModel.ITypeDescriptorContext context){
      return true;
    }
    public override System.ComponentModel.TypeConverter.StandardValuesCollection GetStandardValues(System.ComponentModel.ITypeDescriptorContext context){        
      return new StandardValuesCollection(new BuildAction[]{BuildAction.Compile, BuildAction.Content, BuildAction.EmbeddedResource, BuildAction.None});       
    }
  }
  public class PlatformTypeConverter : TypeConverter{

    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType){
      if (sourceType == typeof(string)) return true;
      return base.CanConvertFrom(context, sourceType);
    }
    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value){
      string str = value as string;
      if (str != null){
        if (str == UIStrings.GetString(UIStringNames.v1, culture)) return PlatformType.v1;
        if (str == UIStrings.GetString(UIStringNames.v11, culture)) return PlatformType.v11;
        if (str == UIStrings.GetString(UIStringNames.v12, culture)) return PlatformType.v12;
        if (str == UIStrings.GetString(UIStringNames.cli1, culture)) return PlatformType.cli1;
      }
      return base.ConvertFrom(context, culture, value);
    }
    public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType){  
      if (destinationType == typeof(string)){
        string result = UIStrings.GetString(((PlatformType)value).ToString(), culture);
        if (result != null) return result;
      }
      return base.ConvertTo(context, culture, value, destinationType);
    }
    public override bool GetStandardValuesSupported(System.ComponentModel.ITypeDescriptorContext context){
      return true;
    }
    public override System.ComponentModel.TypeConverter.StandardValuesCollection GetStandardValues(System.ComponentModel.ITypeDescriptorContext context){        
      return new StandardValuesCollection(new PlatformType[]{PlatformType.v1, PlatformType.v11, PlatformType.v12, PlatformType.cli1});       
    }
  }
}
