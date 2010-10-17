//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Diagnostics;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  public class Runtime{
#if CCINamespace
    private static Identifier SystemCompilerId = Identifier.For("Microsoft.Cci");
#else
    private static Identifier SystemCompilerId = Identifier.For("System.Compiler");
#endif  
    public static Method Combine;
    public static Method CreateInstance;
    public static Method GenericCreateInstance;
    public static Method GetCurrent;
    public static Method GetEnumerator;
    public static Method GetOffsetToStringData;
    public static new Method GetType;
    public static Method GetTypeFromHandle;
    public static Method IDisposableDispose;
    public static Method IsInterned;
    public static new Method MemberwiseClone;
    public static Method MonitorEnter;
    public static Method MonitorExit;
    public static Method MoveNext;
    public static Method ObjectToString;
    public static Method Reset;
    public static Method Remove;
    public static Method StringConcatObjects;
    public static Method StringConcatStrings;
    public static Method StringEquals;

    public static AttributeNode Debuggable;
    public static AttributeNode OptimizedButDebuggable;
    public static AttributeNode OptimizedWithPDBOnly;

    public static Identifier IsConsistentId = Identifier.For("IsConsistent");
    public static Identifier IsVirtualConsistentId = Identifier.For("IsVirtualConsistent");
    public static Identifier IsPeerConsistentId = Identifier.For("IsPeerConsistent");
    public static Identifier IsExposableId = Identifier.For("IsExposable");
    public static Identifier IsNewId = Identifier.For("IsNew");

    static Runtime(){
      Runtime.Initialize();
    }

    public static void Initialize(){
      TypeNode RuntimeHelpers = SystemTypes.SystemAssembly.GetType(Identifier.For("System.Runtime.CompilerServices"), Identifier.For("RuntimeHelpers"));
      if (RuntimeHelpers != null){
        Runtime.GetOffsetToStringData = RuntimeHelpers.GetMethod(Identifier.For("get_OffsetToStringData"));
      }
      Runtime.Combine = SystemTypes.Delegate.GetMethod(StandardIds.Combine, SystemTypes.Delegate, SystemTypes.Delegate);
      Runtime.CreateInstance = SystemTypes.Activator.GetMethod(StandardIds.CreateInstance, SystemTypes.Type);
      Runtime.GenericCreateInstance = SystemTypes.Activator.GetMethod(StandardIds.CreateInstance);
      Runtime.GetCurrent = SystemTypes.IEnumerator.GetMethod(StandardIds.getCurrent);
      Runtime.GetEnumerator = SystemTypes.IEnumerable.GetMethod(StandardIds.GetEnumerator);
      Runtime.GetType = SystemTypes.Object.GetMethod(Identifier.For("GetType"));
      Runtime.GetTypeFromHandle = SystemTypes.Type.GetMethod(Identifier.For("GetTypeFromHandle"), SystemTypes.RuntimeTypeHandle);
      Runtime.IDisposableDispose = SystemTypes.IDisposable.GetMethod(StandardIds.Dispose);
      Runtime.IsInterned = SystemTypes.String.GetMethod(StandardIds.IsInterned, SystemTypes.String);
      Runtime.MemberwiseClone = SystemTypes.Object.GetMethod(StandardIds.MemberwiseClone);
      Runtime.MonitorEnter = SystemTypes.Monitor.GetMethod(StandardIds.Enter, SystemTypes.Object);
      Runtime.MonitorExit = SystemTypes.Monitor.GetMethod(StandardIds.Exit, SystemTypes.Object);
      Runtime.MoveNext = SystemTypes.IEnumerator.GetMethod(StandardIds.MoveNext);
      Runtime.ObjectToString = SystemTypes.Object.GetMethod(StandardIds.ToString);
      Runtime.Reset = SystemTypes.IEnumerator.GetMethod(StandardIds.Reset);
      Runtime.Remove = SystemTypes.Delegate.GetMethod(StandardIds.Remove, SystemTypes.Delegate, SystemTypes.Delegate);
      Runtime.StringConcatObjects = SystemTypes.String.GetMethod(StandardIds.Concat, SystemTypes.Object, SystemTypes.Object);
      Runtime.StringConcatStrings = SystemTypes.String.GetMethod(StandardIds.Concat, SystemTypes.String, SystemTypes.String);
      Runtime.StringEquals = SystemTypes.String.GetMethod(StandardIds.Equals, SystemTypes.String, SystemTypes.String);
      InstanceInitializer dbgConstr = null;
      if (SystemTypes.DebuggableAttribute != null){
        if (SystemTypes.DebuggingModes != null)
          dbgConstr = SystemTypes.DebuggableAttribute.GetConstructor(SystemTypes.DebuggingModes);
        else
          dbgConstr = SystemTypes.DebuggableAttribute.GetConstructor(SystemTypes.Boolean, SystemTypes.Boolean);
      }
      MemberBinding debuggableAttributeCtor = null;
      if (dbgConstr != null) debuggableAttributeCtor = new MemberBinding(null, dbgConstr);
      if (debuggableAttributeCtor != null) {
        Literal trueLit = new Literal(true, SystemTypes.Boolean);
        Literal falseLit = new Literal(false, SystemTypes.Boolean);
        Runtime.Debuggable = new AttributeNode();
        Runtime.Debuggable.Constructor = debuggableAttributeCtor; //TODO: will need to fix this up in the case where the type is compiled from source
        Runtime.Debuggable.Expressions = new ExpressionList(2);
        if (SystemTypes.DebuggingModes != null)
          Runtime.Debuggable.Expressions.Add(new Literal(0x107, SystemTypes.DebuggingModes));
        else {
          Runtime.Debuggable.Expressions.Add(trueLit);
          Runtime.Debuggable.Expressions.Add(trueLit);
        }
        Runtime.OptimizedButDebuggable = new AttributeNode();
        Runtime.OptimizedButDebuggable.Constructor = debuggableAttributeCtor;
        Runtime.OptimizedButDebuggable.Expressions = new ExpressionList(2);
        if (SystemTypes.DebuggingModes != null)
          Runtime.OptimizedButDebuggable.Expressions.Add(new Literal(0x007, SystemTypes.DebuggingModes));
        else {
          Runtime.OptimizedButDebuggable.Expressions.Add(trueLit);
          Runtime.OptimizedButDebuggable.Expressions.Add(falseLit);
        }
        Runtime.OptimizedWithPDBOnly = new AttributeNode();
        Runtime.OptimizedWithPDBOnly.Constructor = debuggableAttributeCtor;
        Runtime.OptimizedWithPDBOnly.Expressions = new ExpressionList(2);
        if (SystemTypes.DebuggingModes != null)
          Runtime.OptimizedWithPDBOnly.Expressions.Add(new Literal(0x006, SystemTypes.DebuggingModes));
        else {
          Runtime.OptimizedWithPDBOnly.Expressions.Add(falseLit);
          Runtime.OptimizedWithPDBOnly.Expressions.Add(falseLit);
        }
      }
    }
  }
}
