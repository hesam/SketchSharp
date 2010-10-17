using Microsoft.Win32;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

namespace System.Compiler{

  public class RegistrationInfo{
    public static readonly string ManagedOnlyDebuggerEEGuid = "{449EC4CC-30D2-4032-9256-EE18EB41B62B}";
    public static readonly string ManagedPlusNativeDebuggerEEGuid = "{92EF0900-2251-11D2-B72E-0000F87572EF}";  

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
    public string GetVersionFromStrongName(string name) {
      AssemblyReference ar = new AssemblyReference(name);
      return ar.Version.ToString();
    }
    public virtual string GetCurrentVisualStudioVersion() {
      Version v = SystemTypes.SystemAssembly.Version;
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
      if ((v.Major == 1 && v.Minor == 2) || v.Major == 2){
	      using (RegistryKey keyVS = Microsoft.Win32.Registry.LocalMachine.OpenSubKey ("Software\\Microsoft\\VisualStudio\\8.0", true)){
		      if (keyVS != null)
			      return "8.0";
	      }
      }
      return "7.0";
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
      string packageScript = GetResourceAsString(typeof(PackageInstaller), "System.Compiler.InstallPackage.rgs");
      string projectScript = GetResourceAsString(typeof(PackageInstaller), "System.Compiler.InstallProject.rgs");
      string languageScript = GetResourceAsString(typeof(PackageInstaller), "System.Compiler.InstallLanguage.rgs");
      string editorScript = GetResourceAsString(typeof(PackageInstaller), "System.Compiler.InstallEditor.rgs");
      string debuggerScript = GetResourceAsString(typeof(PackageInstaller), "System.Compiler.InstallDebugger.rgs");
      string result = packageScript;
      if (this.LanguageServiceType != null) result += languageScript;
      if (this.EditorFactoryType != null) result += editorScript;
      if (this.ProjectFactoryType != null) result += projectScript;
      if (this.DebuggerEEGuid != null) result += debuggerScript;
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
        if (this.TemplateDirectory == null) this.TemplateDirectory = binDirectory+"..\\Templates";
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


  public sealed class RgsParser {
    NameValueCollection args;
    TextReader input;
    char current;
    bool EOF;
    int line;
    int pos;

    string ResolveArg(string name) {
      string result = args[name];
      if (result == null) {
        Error("Missing argument '{0}'", name);
      }
      return result;
    }

    char NextChar() {
      int ch = input.Read();
      if (ch == -1) EOF = true;
      if (ch == 0xd) {
        line++;
        if (input.Peek() == 0xa) {
          input.Read();
        }
        pos = 0;
      } else if (ch == 0xa) {
        line++;
        pos = 0;
      } else {
        pos++;
      }
      this.current = (char)ch;
      return this.current;
    }
  
    public Key Parse(string script, NameValueCollection args) {
      this.args = args;
      this.input = new StringReader(script);
      NextChar();
      Key root = new Key();
      while (! EOF){
        char ch = SkipWhitespace();
        if (!EOF) {
          ParseKey(root);      
        } 
      }
      return root;
    }

    void ParseKey(Key parent) {
      char ch = SkipWhitespace();
      if (ch == '}') 
        return;

      Key key = new Key();
      string name = ParseIdentifier("{=");
      if (name == "val") {
        // this is not a sub key, it is just value in the parent key.
        string id = ParseIdentifier("=");
        object value = ParseValue();
        parent.AddValue(id, value);
        return;
      } 
      if (name == "NoRemove") {
        key.Removal = Removal.NoRemove;
        name = ParseIdentifier("{=");
      } else if (name == "ForceRemove") {
        key.Removal = Removal.ForceRemove;
        name = ParseIdentifier("{=");
      }
      key.Name = name;
      ch = SkipWhitespace();
      if (ch == '=') {
        object def = ParseValue();
        key.DefaultValue = def;
        ch = SkipWhitespace();
      }
      if (ch == '{') {
        ch = NextChar();    
        while (!EOF && ch != '}') {
          ParseKey(key);
          ch = SkipWhitespace();
        }
        if (ch != '}') {
          Error("Expected '{0}'", "}");
        }
        NextChar(); // consume closing brace
      }
      parent.AddChild(key);
    }

    object ParseValue() {
      // var id = s 'literal'
      // var id = d 0
      // var id = d 0xddd
      char ch = SkipWhitespace();
      if (ch != '=') {
        Error("Expected '{0}'", "=");
      }
      NextChar();
      string litType = ParseIdentifier(" ");
      if (litType == "s") {
        string value = ParseLiteral();
        return value;
      } else if (litType == "d") {
        int value = ParseNumeric();
        return value;
      } else {
        Error("Expected '{0}'", "s|d");
      }
      return null;
    }

    StringBuilder litBuilder = new StringBuilder();

    string ParseLiteral() {
      litBuilder.Length = 0;
      char ch = SkipWhitespace();
      if (this.EOF || (ch != '\'' && ch != '"'))
        Error("Expected string literal");
      char delimiter = ch;
      ch = NextChar();
      while (! this.EOF && ch != delimiter && ch != 0xd) {
        if (ch == '%') {
          string value = ParseArg();
          litBuilder.Append(value);
        } else {
          litBuilder.Append(ch);
        }
        ch = NextChar();
      }
      if (ch == 0xd && this.EOF) {
        Error("Unclosed string literal");
      }
      NextChar(); // consume delimiter
      return litBuilder.ToString();
    }

    int ParseNumeric() {
      char ch = SkipWhitespace();
      litBuilder.Length = 0;
      while (!this.EOF && ! Char.IsWhiteSpace(ch)) {
        if (ch == '%') {
          litBuilder.Append(ParseArg());
        } else {
          litBuilder.Append(ch);
        }
        ch = NextChar();
      }
      string value = litBuilder.ToString();
      return Int32.Parse(value);    
    }

    StringBuilder idBuilder = new StringBuilder();

    string ParseIdentifier(string followSet) {
      char ch = SkipWhitespace();
      if (ch == '\'' || ch == '"') {
        return ParseLiteral();
      }
      string id = null;
      idBuilder.Length = 0;
      if (ch == '{') { 
        // special case so GUID's can be used as key names.
        idBuilder.Append(ch);
        ch = NextChar();
      }
      while (!EOF && ! Char.IsWhiteSpace(ch) && followSet.IndexOf(ch)<0) {
        if (ch == '%') {
          string value = ParseArg();
          idBuilder.Append(value);
        } else {    
          idBuilder.Append(ch);
        }
        ch = NextChar();
      }  
      id = idBuilder.ToString();
      if (id == null || id == "") {
        Error("Missing key name");
      }
      return id;
    }

    StringBuilder argBuilder = new StringBuilder();

    string ParseArg() {
      char ch = NextChar(); // consume opening '%'
      argBuilder.Length = 0;
      while (!EOF && ! Char.IsWhiteSpace(ch) && ch != '%') {
        argBuilder.Append(ch);
        ch = NextChar();
      }
      if (ch != '%' || argBuilder.Length == 0) {
        Error("Expected '{0}'", "%");
      }    
      return ResolveArg(argBuilder.ToString());
    }

    char SkipWhitespace() {
      char ch = this.current;
      while (!EOF && ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r') {
        ch = NextChar();
      }
      this.current = ch;
      return ch;
    }

    void Error(string msg, params string[] args) {
      throw new Exception(String.Format("Error: "+ msg + " at line " + (line+1) + ", position " + (pos+1), args));
    }
  }

  public enum Removal {
    None,
    NoRemove,
    ForceRemove
  }

  public sealed class Key {
    public string Name;
    public object DefaultValue;
    public Removal Removal;
    public Hashtable values;
    public Hashtable children;

    public void AddValue(string name, object value) {
      if (values == null) values = new Hashtable();
      if (values.Contains(name)) {
        throw new ArgumentException(String.Format("Value named '{0}' inside key {1} is already defined", name, this.Name));
      }
      values.Add(name, value);
    }
    public void AddChild(Key child) {
      if (children == null) children = new Hashtable();
      if (children.Contains(child.Name)) {
        // need to merge them
        Key existingChild = (Key)children[child.Name];
        existingChild.Merge(child);
      } else {
        children.Add(child.Name, child);
      }
    }
    public void Merge(Key key) {
      if (key.values != null) {
        foreach (string var in key.values.Keys) {
          this.AddValue(var, key.values[var]);
        }
      }
      if (key.children != null) {
        foreach (string name in key.children.Keys) {
          AddChild((Key)key.children[name]);
        }
      }
    }
  }

}