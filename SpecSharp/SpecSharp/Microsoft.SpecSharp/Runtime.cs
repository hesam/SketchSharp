//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
//
#if CCINamespace
using Microsoft.Cci;
using Cci = Microsoft.Cci;
#else
using System.Compiler;
using Cci = System.Compiler;
#endif
using System;
using System.Diagnostics;
using System.IO;


namespace Microsoft.SpecSharp{
  internal sealed class RuntimeAssemblyLocation {
    internal static string Location {
      get { return location; }
      set {
        if (!File.Exists(location)) location = typeof(dummy).Module.Assembly.Location;
        location = value;
        Identifier id = Identifier.For("Microsoft.SpecSharp.Runtime");
        AssemblyReference aref = (AssemblyReference)Cci.TargetPlatform.AssemblyReferenceFor[id.UniqueIdKey];
        if (aref == null) {
          aref = new AssemblyReference(typeof(dummy).Module.Assembly.FullName);
          Cci.TargetPlatform.AssemblyReferenceFor[id.UniqueIdKey] = aref;
        }
        aref.Location = value;
      }
    }
    private static string location = null; //Can be set by compiler in cross compilation scenarios
    internal static AssemblyNode ParsedAssembly;

  }
  public sealed class TargetPlatform{

    public static bool UseGenerics { get { return Cci.TargetPlatform.UseGenerics; } }
    public static void SetToV1(){
      TargetPlatform.SetToV1(null);
    }
    public static void SetToV1(string platformAssembliesLocation){
      Cci.TargetPlatform.SetToV1(platformAssembliesLocation);
      RuntimeAssemblyLocation.Location = Path.Combine(Path.GetDirectoryName(SystemAssemblyLocation.Location), "Microsoft.SpecSharp.Runtime.dll");
    }
    public static void SetToV1_1(){
      TargetPlatform.SetToV1_1(null);
    }
    public static void SetToV1_1(string platformAssembliesLocation){
      Cci.TargetPlatform.SetToV1_1(platformAssembliesLocation);
      RuntimeAssemblyLocation.Location = Path.Combine(Path.GetDirectoryName(SystemAssemblyLocation.Location), "Microsoft.SpecSharp.Runtime.dll");
    }
    public static void SetToV2(){
      TargetPlatform.SetToV2(null);
    }
    public static void SetToV2(string platformAssembliesLocation){
      Cci.TargetPlatform.SetToV2(platformAssembliesLocation);
      RuntimeAssemblyLocation.Location = Path.Combine(Path.GetDirectoryName(SystemAssemblyLocation.Location), "Microsoft.SpecSharp.Runtime.dll");
    }
    /// <summary>
    /// Use this to set the target platform to a platform with a superset of the platform assemblies in version 1.1, but
    /// where the public key tokens and versions numbers are determined by reading in the actual assemblies from
    /// the supplied location. Only assemblies recognized as platform assemblies in version 1.1 will be unified.
    /// </summary>
    public static void SetToPostV1_1(string platformAssembliesLocation){
      Cci.TargetPlatform.SetToPostV1_1(platformAssembliesLocation);
      RuntimeAssemblyLocation.Location = Path.Combine(Path.GetDirectoryName(SystemAssemblyLocation.Location), "Microsoft.SpecSharp.Runtime.dll");
    }
  }
  public sealed class Runtime{
    private static Identifier SpecSharp = Identifier.For("Microsoft.SpecSharp");
    public static AssemblyNode RuntimeAssembly;
    
    internal static TypeNode PostCompilationPluginAttributeType;
    internal static InstanceInitializer ObjectConstructor; 
    internal static Method IListAdd;

    public static Class/*!*/ MustOverrideAttribute;

    static Runtime(){
      Runtime.Initialize();
    }    
    internal static void Initialize(){
      RuntimeAssembly = Runtime.GetRuntimeAssembly();

      PostCompilationPluginAttributeType = RuntimeAssembly.GetType(Identifier.For("Microsoft.SpecSharp"), Identifier.For("PostCompilationPluginAttribute"));
      ObjectConstructor = SystemTypes.Object.GetConstructor(); 
      IListAdd = SystemTypes.IList.GetMethod(StandardIds.Add, SystemTypes.Object);

#if CCINamespace
      const string ContractsNs = "Microsoft.Contracts";
#else
      const string ContractsNs = "Microsoft.Contracts";
#endif

      MustOverrideAttribute = (Class)GetCompilerRuntimeTypeNodeFor(ContractsNs, "MustOverrideAttribute");
    
    }
    private static TypeNode/*!*/ GetCompilerRuntimeTypeNodeFor(string/*!*/ nspace, string/*!*/ name) {
      return Runtime.GetCompilerRuntimeTypeNodeFor(nspace, name, 0);
    }
    private static TypeNode/*!*/ GetCompilerRuntimeTypeNodeFor(string/*!*/ nspace, string/*!*/ name, int numParams) {
      if (Cci.TargetPlatform.GenericTypeNamesMangleChar != 0 && numParams > 0)
        name = name + Cci.TargetPlatform.GenericTypeNamesMangleChar + numParams;
      TypeNode result = null;
      if (RuntimeAssembly == null)
        Debug.Assert(false);
      else
        result = RuntimeAssembly.GetType(Identifier.For(nspace), Identifier.For(name));
      //if (result == null) result = CoreSystemTypes.GetDummyTypeNode(RuntimeAssembly, nspace, name);
      //result.typeCode = typeCode;
      return result;
    }

    private static AssemblyNode GetRuntimeAssembly(){
      AssemblyNode result = RuntimeAssemblyLocation.ParsedAssembly;
      if (result != null) return result;
      if (RuntimeAssemblyLocation.Location == null){
        //This happens when there was no call to SetToXXX. Use the location of the assembly linked into the compiler.
        RuntimeAssemblyLocation.Location = typeof(dummy).Module.Assembly.Location;
        result = AssemblyNode.GetAssembly((RuntimeAssemblyLocation.Location));
      }else{
        result = AssemblyNode.GetAssembly(RuntimeAssemblyLocation.Location);
        if (result == null){
          //This happens when the location was set by a call to SetToXXX, but the runtime assembly is not actually deployed
          //in the directory containing the target platform assemblies. This is really an error, but as a convenience during
          //development of the compiler, the assembly linked into the compiler is used instead.
          RuntimeAssemblyLocation.Location = typeof(dummy).Module.Assembly.Location;
          result = AssemblyNode.GetAssembly((RuntimeAssemblyLocation.Location));
        }
      }
      if (result == null){
        result = new AssemblyNode();
        result.Name = "Microsoft.SpecSharp.Runtime";
        result.Version = Cci.TargetPlatform.TargetVersion;
      }
      Cci.TargetPlatform.AssemblyReferenceFor[Identifier.For(result.Name).UniqueIdKey] = new AssemblyReference(result);
      return result;
    }
  }
}

