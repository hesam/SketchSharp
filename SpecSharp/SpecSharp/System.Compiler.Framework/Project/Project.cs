using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  /// <summary>
  /// Manages the state of the project (References, options, files, etc.)
  /// </summary>
  //TODO: remove this class from the Framework
  public class Project{
    /// <summary>
    /// The compilation parameters that are used for this compilation.
    /// </summary>
    public System.CodeDom.Compiler.CompilerParameters CompilerParameters;
    /// <summary>
    /// The paths to the artifacts (eg. files) in which the source texts are stored. Used together with IndexForFullPath.
    /// </summary>
    public StringList FullPathsToSources;
    /// <summary>
    /// A scope for symbols that belong to the compilation as a whole. No C# equivalent. Null if not applicable.
    /// </summary>
    public Scope GlobalScope;
    /// <summary>
    /// Set to true if this project has been constructed automatically to contain a single file that is being edited out of context.
    /// In such cases it does not makes sense to produce semantic errors, since assembly references will be missing, as will other
    /// source files that need to be compiled together with the single file in the dummy project.
    /// </summary>
    public bool IsDummy;
    /// <summary>
    /// The source texts to be compiled together with the appropriate parser to call and possibly the resulting analyzed abstract syntax tree.
    /// </summary>
    public CompilationUnitSnippetList CompilationUnitSnippets;
    /// <summary>
    /// A queriable collection of all the types in the parse trees. When the project is compiled, this also represents
    /// compilation output. 
    /// </summary>
    public Module SymbolTable;
    public Compilation Compilation;
    /// <summary>
    /// Helps client code keep track of when the project was last modified.
    /// </summary>
    public DateTime LastModifiedTime = DateTime.Now;
    /// <summary>
    /// An index over FullPathsToSources. Allows quick retrieval of sources and parse trees, given the path.
    /// </summary>
    public TrivialHashtable IndexForFullPath = new TrivialHashtable();

    /// <summary>
    /// Helps clients keep track of the Project instance that caused the environment to open the specified file
    /// </summary>
    public static Project For(string sourceFileUri){
      WeakReference weakRef = (WeakReference)Project.projects[sourceFileUri];
      if (weakRef == null || !weakRef.IsAlive) return null;
      return (Project)weakRef.Target;
    }
    private static Hashtable projects = Hashtable.Synchronized(new Hashtable());
  }
  //TODO: move these to Nodes
  /// <summary>
  /// Use this after a source text has already been scanned and parsed. This allows the source text to get released
  /// if there is memory pressure, while still allowing portions of it to be retrieved on demand. This is useful when
  /// a large number of source files are read in, but only infrequent references are made to them.
  /// </summary>
  public sealed class CollectibleSourceText : ISourceText{
    private string filePath;
    private WeakReference fileContent;
    private int length;

    public CollectibleSourceText(string filePath, int length){
      Debug.Assert(File.Exists(filePath));
      this.filePath = filePath;
      this.fileContent = new WeakReference(null);
      this.length = length;
    }
    private string ReadFile(){
      StreamReader sr = new StreamReader(filePath);
      string content = sr.ReadToEnd();
      this.length = content.Length;
      sr.Close();
      return content;
    }
    private string GetSourceText(){
      string source = null;
      if (this.fileContent.IsAlive) source = (string)this.fileContent.Target;
      if (source != null) return source;
      source = this.ReadFile();
      this.fileContent.Target = source;
      return source;
    }

    int ISourceText.Length{get{return this.length;}}
    unsafe byte* ISourceText.Buffer{get{return null;}}
    string ISourceText.Substring(int startIndex, int length){
      return this.GetSourceText().Substring(startIndex, length);
    }
    char ISourceText.this[int index]{
      get{
        return this.GetSourceText()[index];
      }
    }
    void ISourceText.MakeCollectible(){
      this.fileContent.Target = null;
    }
  }
  /// <summary>
  /// This class is used to wrap the string contents of a source file with an ISourceText interface. It is used while compiling
  /// a project the first time in order to obtain a symbol table. After that the StringSourceText instance is typically replaced with
  /// a CollectibleSourceText instance, so that the actual source text string can be collected. When a file is edited, 
  /// and the editor does not provide its own ISourceText wrapper for its edit buffer, this class can be used to wrap a copy of the edit buffer.
  /// </summary>
  public sealed class StringSourceText : ISourceText{
    /// <summary>
    /// The wrapped string used to implement ISourceText. Use this value when unwrapping.
    /// </summary>
    public readonly string SourceText;
    /// <summary>
    /// True when the wrapped string is the contents of a file. Typically used to check if it safe to replace this
    /// StringSourceText instance with a CollectibleSourceText instance.
    /// </summary>
    public bool IsSameAsFileContents;

    public StringSourceText(string sourceText, bool isSameAsFileContents){
      this.SourceText = sourceText;
      this.IsSameAsFileContents = isSameAsFileContents;
    }
    int ISourceText.Length{get{return this.SourceText.Length;}}
    unsafe byte* ISourceText.Buffer{get{return null;}}
    string ISourceText.Substring(int startIndex, int length){
      return this.SourceText.Substring(startIndex, length);
    }
    char ISourceText.this[int index]{
      get{
        return this.SourceText[index];
      }
    }
    void ISourceText.MakeCollectible(){
    }
  }
}
