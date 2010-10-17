using Microsoft.Win32;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

namespace Microsoft.VisualStudio.Package{

  public class RegistrationInfo{
    public static readonly string ManagedOnlyDebuggerEEGuid = "{449EC4CC-30D2-4032-9256-EE18EB41B62B}";
    public static readonly string ManagedPlusNativeDebuggerEEGuid = "{92EF0900-2251-11D2-B72E-0000F87572EF}";  

    public bool InstalVSIPLicense; // allow packages to load without package key

    //Provide these strings if you have a language service or a project
    public string LanguageName;
    public string LanguageShortName;
    public string SourceFileExtension;
    public string Win32ResourcesDllPath;

    // Provide these strings if you have a language service.
    public int LanguageServiceLoadKeyId = 1;
    public bool UseStockColors = true;
    public int SourceFileIconId = 1; // these are zero-based indexes not actual icon ids.
    public Type LanguageServiceType;
    public int CodeSenseDelay = 1000; // milliseconds

    // Provide your package type information here.
    public Type PackageType;
    public string PackageName;
    public string CompanyName;
    public string ProductName;
    public string ProductShortName;
    public string ProductVersion;
    public int PackageLoadKeyId = 1;

    // Provide these options if you implemented your own EditorPackage and EditorFactory.
    // Setting these pulls in InstallEditorPackage.rgs.
    public Type EditorFactoryType;
    public string EditorName;

    // Provide these strings if you have a Project Package
    public string MenuResourceId = "1000";
    public int ProjectFileIconId = 0; // these are zero-indexes not actual icon id's.
    public Type ProjectFactoryType;
    public Type ProjectManager;
    public string ProjectFileExtension;
    public string TemplateDirectory;

    // Provide these options if you have a Debugger
    public string DebuggerEEGuid;
    public string DebuggerLanguageGuid;

    // Provide these options if you have a CodeDomProvider
    public int ASPWarningLevel = 1;
    public Type CodeDomProvider;
    public string ASPExtensionAliases;

    /// <summary>
    /// This method is purely for "dev use only".  Real product 
    /// registration goes through windows setup.
    /// The cool thing about this is that this "dev" code path is 
    /// using the same .rgs files that the windows setup uses.
    /// </summary>
    public virtual void Register(){
      NameValueCollection args = this.GetRegistrationScriptArguments();
      string script = this.GetRegistrationScript();
      PackageInstaller pi = new PackageInstaller();
      pi.Register(script, args);
      if (this.CodeDomProvider != null) {
        pi.RegisterCodeDomProvider(this.CodeDomProvider, this.SourceFileExtension, this.ASPExtensionAliases, this.ASPWarningLevel);
      }
    }        
    public virtual void Unregister(){
      NameValueCollection args = this.GetRegistrationScriptArguments();
      string script = this.GetRegistrationScript();
      PackageInstaller pi = new PackageInstaller();
      pi.Unregister(script, args); 
      if (this.CodeDomProvider != null) {
        pi.UnregisterCodeDomProvider(this.SourceFileExtension);
      }
    }
    public string GetVersionFromStrongName(string assemblyName) {
      int v = assemblyName.IndexOf(", Version=");
      string name = v > 0 ? assemblyName.Substring(0, v) : assemblyName;
      int c = assemblyName.IndexOf(", Culture=");
      string version = c > 0 ? assemblyName.Substring(v + 10, c - v - 10) : null;
      return version;
    }
    public virtual string GetCurrentVisualStudioVersion() {
      //bugbug: this is not cross-platform.      
      Version v = new Version(GetVersionFromStrongName(typeof(object).Assembly.FullName));
      // Version v = SystemTypes.SystemAssembly.Version;
      if (v.Major == 1 && v.Minor == 0 && v.Build <= 3705){
        using(RegistryKey keyVS = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\VisualStudio\\7.0", true)){
          if (keyVS != null)
            return "7.0"; //Running on 1.0 and Vs7.0 is installed
        }
      }
      if (v.Major == 1 && v.Minor == 0){
	      using (RegistryKey keyVS = Microsoft.Win32.Registry.LocalMachine.OpenSubKey ("Software\\Microsoft\\VisualStudio\\7.1", true)){
		      if (keyVS != null)
			      return "7.1";
	      }
      }
      if (v.Major == 1 && v.Minor == 2){
	      using (RegistryKey keyVS = Microsoft.Win32.Registry.LocalMachine.OpenSubKey ("Software\\Microsoft\\VisualStudio\\8.0", true)){
		      if (keyVS != null)
			      return "8.0";
	      }
      }
      return "7.0";
    }
    public virtual string GetUrtVersion() {
      string dir = Path.GetDirectoryName(typeof(object).Assembly.Location);
      return Path.GetFileName(dir);
    }

    public string GetResourceAsString(Type installer, string resourceName) {
      Stream stm = installer.Assembly.GetManifestResourceStream(resourceName);
      StreamReader rs = new StreamReader(stm);
      string s = rs.ReadToEnd();
      rs.Close();
      stm.Close(); 
      return s;
    }

    public virtual string GetRegistrationScript(){
      string packageScript = GetResourceAsString(typeof(PackageInstaller), "Microsoft.VisualStudio.Project.InstallPackage.rgs");
      string projectScript = GetResourceAsString(typeof(PackageInstaller), "Microsoft.VisualStudio.Project.InstallProject.rgs");
      string languageScript = GetResourceAsString(typeof(PackageInstaller), "Microsoft.VisualStudio.Project.InstallLanguage.rgs");
      string editorScript = GetResourceAsString(typeof(PackageInstaller), "Microsoft.VisualStudio.Project.InstallEditor.rgs");
      string debuggerScript = GetResourceAsString(typeof(PackageInstaller), "Microsoft.VisualStudio.Project.InstallDebugger.rgs");
      string vsipLicense = GetResourceAsString(typeof(PackageInstaller), "Microsoft.VisualStudio.Project.InstallVSIPLicense.rgs");
      string result = packageScript;
      if (this.LanguageServiceType != null) result += languageScript;
      if (this.EditorFactoryType != null) result += editorScript;
      if (this.ProjectFactoryType != null) result += projectScript;
      if (this.DebuggerEEGuid != null) result += debuggerScript;
      if (this.InstalVSIPLicense) result += vsipLicense;
      CCITracing.Trace(result);
      return result;
    }
    public virtual NameValueCollection GetRegistrationScriptArguments(){
      NameValueCollection args = new NameValueCollection();
      string binDirectory = Path.GetDirectoryName(this.GetType().Assembly.Location)+"\\";
      args.Add("BinDir", binDirectory);
      args.Add("SystemFolder", System.Environment.SystemDirectory+@"\");
      string vsVersion = this.GetCurrentVisualStudioVersion();
      args.Add("VsVersion", vsVersion);
      if (this.LanguageName != null) args.Add("LanguageName", this.LanguageName);
      string DevEnvPath = "";
      using(RegistryKey keyVS = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\VisualStudio\"+vsVersion, true)){
        if (keyVS != null)
          DevEnvPath = (string)keyVS.GetValue("InstallDir")+"devenv.exe";
      }

      args.Add("ASSEMBLY", "Microsoft.VisualStudio.Project");
      args.Add("ASSEMBLYVERSION", GetVersionFromStrongName(typeof(ProjectManager).Assembly.FullName));      
      args.Add("URTVER", GetUrtVersion());
      args.Add("MODULE", System.Environment.SystemDirectory+"\\mscoree.dll");

      if (this.PackageType != null || LanguageServiceType != null || ProjectFactoryType != null){
        args.Add("Win32ResourcesDllPath", this.Win32ResourcesDllPath);
        args.Add("SatelliteDll", Path.GetFileName(this.Win32ResourcesDllPath));
        args.Add("DevEnvCommand", "\"" + DevEnvPath + "\" \"%1\"");
      }
      if (this.PackageType != null) {
        args.Add("PackageGuid", "{" + this.PackageType.GUID + "}" );
        args.Add("PackageClassName", this.PackageType.FullName);
        args.Add("PackageAssemblyName", this.PackageType.Assembly.FullName);
        args.Add("PackageName", this.PackageName);
        args.Add("CompanyName", this.CompanyName);
        args.Add("ProductName", this.ProductName);
        args.Add("ProductVersion", this.ProductVersion);
        args.Add("PackageLoadKeyId", this.PackageLoadKeyId.ToString()); 
        args.Add("MenuResourceId", this.MenuResourceId);
        args.Add("ProductShortName", this.ProductShortName);
      }
      if (LanguageServiceType != null) {
        args.Add("LanguageShortName", this.LanguageShortName);
        args.Add("SourceFileExtension", this.SourceFileExtension);
        args.Add("SourceFileIconId", this.SourceFileIconId.ToString());
        args.Add("DevEnvPath", "\""+DevEnvPath+"\"");
        args.Add("OpenCommand", "Open(\"%1\")");
        args.Add("LanguageServiceGuid", "{"+this.LanguageServiceType.GUID.ToString()+"}");
        args.Add("RequestStockColors", this.UseStockColors ? "1" : "0");
        args.Add("CodeSenseDelay", this.CodeSenseDelay.ToString());
      }

      if (this.EditorFactoryType != null){
        args.Add("EditorFactoryGuid", "{" + this.EditorFactoryType.GUID + "}" );
        args.Add("EditorFactoryClassName", this.EditorFactoryType.FullName);
        args.Add("EditorFactoryAssemblyName", this.EditorFactoryType.Assembly.FullName);
        args.Add("EditorName", this.EditorName);
      }

      if (ProjectFactoryType != null) {
        args.Add("ProjectFactoryGuid", "{"+ this.ProjectFactoryType.GUID.ToString()+"}");
        args.Add("ProjectFactoryClassName", this.ProjectFactoryType.FullName);
        args.Add("ProjectFactoryAssemblyName", this.ProjectFactoryType.Assembly.FullName);
        args.Add("ProjectFileExtension", this.ProjectFileExtension);
        if (this.TemplateDirectory == null) this.TemplateDirectory = binDirectory+"..\\Templates\\";
        args.Add("TemplatePath", this.TemplateDirectory);
        args.Add("ProjectFileIconId", this.ProjectFileIconId.ToString());
      }
      if (DebuggerEEGuid != null) {
        args.Add("DebuggerLanguageGuid", this.DebuggerLanguageGuid);
        args.Add("DebuggerEEGuid", this.DebuggerEEGuid);
        args.Add("ManagedOnlyDebuggerEEGuid", RegistrationInfo.ManagedOnlyDebuggerEEGuid);
        args.Add("ManagedPlusNativeDebuggerEEGuid", RegistrationInfo.ManagedPlusNativeDebuggerEEGuid);
      }
      return args;
    }


    public void CheckArguments(string url, string xpath) {
      NameValueCollection col = GetRegistrationScriptArguments();
      NameValueCollection different = new NameValueCollection();

      XmlDocument doc = new XmlDocument();
      doc.Load(url);
      foreach (XmlElement var in doc.SelectNodes(xpath)){
        string name = var.InnerText;
        string current = var.GetAttribute("Value");
        string value = col.Get(name);
        if (value == null) {
          Console.WriteLine("Unnecessary: {0}", name); 
        } else {
          col.Remove(name);
          if (value != current) {
            Console.WriteLine("Different: {0}\n Current: {1}\n New:     {2}", name, current, value);
          }
        }
      }
      foreach (string key in col.Keys) {
        Console.WriteLine("Missing: '{0}' value '{1}'", key, col[key]);
      }
    }
  }

  /// <summary>
  /// This is a helper class for doing rgs based installation of a visual studio package.
  /// </summary>
  [ComVisible(true), Guid("4582389d-48de-4cdc-8e96-719ee2a0a3fe")]
  public sealed class PackageInstaller {

    /// <summary>
    /// This method takes the script returned by and CreateRgsScript and executes it in “install” mode. 
    /// </summary>
    public void Register(string rgsScript, NameValueCollection args) {
      RgsParser p = new RgsParser();
      Key root = p.Parse(rgsScript, args);
      foreach (Key key in root.children.Values) {
        if (key.Name == "HKCR") {
          Register(key, Registry.ClassesRoot);
        } else if (key.Name == "HKLM") {
          Register(key, Registry.LocalMachine);
        } else if (key.Name == "HKCU") {
          Register(key, Registry.CurrentUser);
        } else {
          Error("Root keys must be 'HKCR', 'HKLM' or 'HKCU'");
        }
      }
    }

    void Register(Key key, RegistryKey reg) {
      if (key.DefaultValue != null) {
        object value = key.DefaultValue;
        reg.SetValue(null, value);
      }
      if (key.values != null) {
        foreach (string name in key.values.Keys) {
          object value = key.values[name];
          reg.SetValue(name, value);
        }
      }
      if (key.children != null) {
        foreach (Key child in key.children.Values) {        
          using (RegistryKey subKey = reg.CreateSubKey(child.Name)) {
            Register(child, subKey);
          }
        }
      }
    }

    /// <summary>
    /// This method takes the script returned by and CreateRgsScript and executes it in “uninstall” mode. 
    /// </summary>
    public void Unregister(string rgsScript, NameValueCollection args) {
      RgsParser p = new RgsParser();
      Key root = p.Parse(rgsScript, args);
      foreach (Key key in root.children.Values) {
        if (key.Name == "HKCR") {
          Unregister(key, Registry.ClassesRoot);
        } else if (key.Name == "HKLM") {
          Unregister(key, Registry.LocalMachine);
        } else if (key.Name == "HKCU") {
          Unregister(key, Registry.CurrentUser);
        } else {
          Error("Root keys must be 'HKCR', 'HKLM' or 'HKCU'");
        }
      }    
    }  

    void Unregister(Key key, RegistryKey reg) {
      if (key.values != null) {
        foreach (string name in key.values.Keys) {
          reg.DeleteValue(name, false);
        }
      }
      if (key.children != null) {
        foreach (Key child in key.children.Values) {        
          if (child.Removal == Removal.ForceRemove) {
            QuietDeleteSubKeyTree(reg, child.Name);
          } else {
            using (RegistryKey subKey = reg.OpenSubKey(child.Name, true)) {
              if (subKey != null) {
                Unregister(child, subKey);
              }
            }
          }
        }
      }
    
    }

    void QuietDeleteSubKeyTree(RegistryKey key, string name){
      RegistryKey subKey = key.OpenSubKey(name,true);
      if (subKey != null) {
        subKey.Close();
        key.DeleteSubKeyTree(name);
      }
    }

    void Error(string msg, params string[] args) {
      throw new ApplicationException(String.Format(msg, args));
    } 

    private string GetMachineConfigPath() {
      using(RegistryKey keyAspDotNet = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\ASP.NET", true)){
        if (keyAspDotNet == null) return null;
        string version = (string)keyAspDotNet.GetValue("RootVer");
        if (version == null) return null;
        using(RegistryKey verKey = keyAspDotNet.OpenSubKey(version, true)){
          string path = (string)verKey.GetValue("Path");
          return path + "\\CONFIG\\machine.config";
        }
      }
    }

    /// <summary>
    /// Installs the CodeDomProvider in machine.config for the current version of the .NET Frameworks
    /// being used by ASP.NET so that the compiler can be used from ASP.NET.
    /// </summary>
    /// <param name="provider">Your CodeDomProvider class.</param>
    /// <param name="extension">The file extension</param>
    /// <param name="aliases">Semicolon separated list of aliases for your language.  Fore example "c#;csharp"</param>
    /// <param name="warningLevel">e.g. "1"</param>
    /// <returns>0 if successful. Throws exception on error.</returns>
    public int RegisterCodeDomProvider(Type provider, string extension, string aliases, int warningLevel){
      XmlDocument doc = new XmlDocument();
      string sConfigFilePath = this.GetMachineConfigPath();
      if (sConfigFilePath == null || !File.Exists(sConfigFilePath)) return 0; // nothing to install
      doc.Load(sConfigFilePath);
      XmlElement root = (XmlElement)doc.SelectSingleNode("//compilation/compilers");
      if (root == null) return 0;
      XmlElement ext = (XmlElement)root.SelectSingleNode("compiler[@extension='"+extension+"']");      
      if (ext == null){
        root.AppendChild(doc.CreateTextNode("\r\n\t\t\t\t"));
        ext = doc.CreateElement("compiler");
        root.AppendChild(ext);           
        root.AppendChild(doc.CreateTextNode("\r\n\t\t\t"));
      }
      string strongName = provider.AssemblyQualifiedName;
      ext.SetAttribute("extension", extension);
      if (aliases != null) ext.SetAttribute("language", aliases);
      ext.SetAttribute("type", strongName);
      ext.SetAttribute("warningLevel", warningLevel.ToString());
      doc.Save(sConfigFilePath);
      return 0;
    }

    /// <summary>
    /// Removes the registration of the given language from machine.config.
    /// </summary>
    /// <returns>0 if successful</returns>
    public int UnregisterCodeDomProvider(string extension) {
      XmlDocument doc = new XmlDocument();
      string sConfigFilePath = this.GetMachineConfigPath();
      if (sConfigFilePath == null) return 0;
      doc.Load(sConfigFilePath);
      XmlElement root = (XmlElement)doc.SelectSingleNode("//compilation/compilers");
      if (root == null) return 0;
      XmlElement ext = (XmlElement)root.SelectSingleNode("compiler[@extension='"+extension+"']");      
      if (ext == null) return 0;
      root.RemoveChild(ext);
      doc.Save(sConfigFilePath);
      return 0;
    }
  }



}