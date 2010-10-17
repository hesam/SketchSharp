//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.Contracts;

namespace System.Compiler.Analysis.PointsTo {

  public class PointsToAndEffectsAnnotations {
    public static List<string> assumedPureMethods = null;
    public static bool WorstCase = false;
    public static bool assumeMorePures = false;
    public static bool writeConfinedByDefault = false;
    #region Methods to decide if the method is assumed as Pure
    /// <summary>
    /// Determine if a method has to be assumed as Pure.
    /// That is because it is annotaed as pure or confined
    /// Or belong to a ser of well know methods (that should be annotated in the future)
    /// </summary>
    /// <param name="m"></param>
    /// <returns></returns>
    /// 
    internal static bool IsAssumedPureMethod(Method m) {
      if (WorstCase)
        return false;

      bool res = false;
      res = res || IsDeclaredPure(m) || IsAssumedConfinedMethod(m);

      return res;
      }

    internal static bool IsAssumedConfinedMethod(Method m) {
      List<string> assumedPureCtorTypes = new List<string>(new string[] { "System.Object", "System.Collections" });
      // Some native method that we assume as pure..
      if (assumedPureMethods == null)
        RetriveAssumedPureMethods("assumedPure.txt");


      bool res = false;
      string methodName = m.Name.Name;
      string methodFullName = m.GetFullUnmangledNameWithTypeParameters();
      string methodDeclaringType = m.DeclaringType.FullName;

      // Declared pure method are assumed pure
      res = res || m.IsConfined || m.IsStateIndependent;

      // It should be readed from a contract
      // REMOVE!!!!!!!!!!!!!!!!
      res = res || (methodName.Equals("Equals"));
      res = res || (methodName.Equals("GetHashCode"));
      res = res || (methodName.Equals("ToString"));
      // Contracts are ignored
      res = res || (methodDeclaringType.StartsWith("Microsoft.Contracts"));
      res = res || methodFullName.EndsWith("get_SpecSharp::FrameGuard");

      // DELETE THIS. Just for  benchmarking standalone app
      if (PointsToAndEffectsAnnotations.assumeMorePures) {
        res = res || methodName.Contains("GetEnumerator");
        res = res || (methodName.StartsWith("Is"));
        res = res || (methodName.StartsWith("Get_"));
        res = res || (methodName.StartsWith("get_"));
        res = res || (methodName.StartsWith("GetString"));
        res = res || (methodName.StartsWith("IndexOf"));
        res = res || (m is InstanceInitializer);
        res = res || methodFullName.StartsWith("System.ThrowHelper");
        }

      // Some assumed pure methods.
      res = res || starWith(methodFullName, assumedPureMethods);
      // Some constructors assumed pure 
      res = res || (m is InstanceInitializer) && starWith(methodDeclaringType, assumedPureCtorTypes);

      // It should be readed from a contract
      res = res || (methodName.Equals("Parse") && m.DeclaringType.IsPrimitiveNumeric);

      // Properties
      //res = res || m.IsPropertyGetter;
      ////
      //res = res || (methodName.StartsWith("Is"));
      //res = res || (methodName.StartsWith("Get_"));
      //res = res || (methodName.StartsWith("IndexOf"));
      return res;
      }
    internal static void RetriveAssumedPureMethods(string filename) {
      try {
        using (StreamReader sr = new StreamReader(filename)) {
          assumedPureMethods = new List<string>();
          while (!sr.EndOfStream) {
            string line = sr.ReadLine().Trim();
            if (line.StartsWith("//") || line.Length == 0) {
              }
            else
              assumedPureMethods.Add(line);
            }
          }
        }
      catch (Exception) {
        string[] sAssumedPureMethods = { 
                    "Linq.Enumerable.",
                    "System.Query.",
                    "System.Query.Sequence.Where",
                    "System.Query.Sequence.Select",
                    "System.Environment.GetResourceFromDefault(System.String)",
                    "System.Threading.Interlocked.CompareExchange(System.Object@,System.Object,System.Object)",
                    "System.Threading.Monitor.Enter(System.Object)",
                    "System.Threading.Monitor.Exit(System.Object)",
                    "System.Collections.ICollection.get_SyncRoot",
                    "System.Activator.CreateInstance",
                    "System.ArgumentNullException"
                };
        assumedPureMethods = new List<string>();
        assumedPureMethods.AddRange(sAssumedPureMethods);

        }
      }




    // Check whether the method was declared as pure of confined
    public static bool IsDeclaredPure(Method method) {
      if (WorstCase)
        return false;

      if (method == null)
        return false;

      return method.IsPure || method.IsConfined || method.IsStateIndependent;
      }

    public static bool IsPurityForcedByUser(Method method) {
      if (WorstCase)
        return false;
      bool res = false;
      AttributeNode attr = method.GetAttribute(SystemTypes.PureAttribute);
      if (attr != null) {
        PureAttribute purity = (PureAttribute)attr.GetRuntimeAttribute();
        if (purity != null)
          return purity.IsAssumedPure;
        }

      return res;

      }

    public static bool IsAssumedPureDelegate(DelegateNode n) {
      if (WorstCase)
        return false;
      bool res = false;
      res = n.GetAttribute(SystemTypes.PureAttribute) != null;
      return res;
      }
    public static bool IsAssumedPureDelegate(Parameter p) {
      if (WorstCase)
        return false;

      bool res = false;
      res = p.GetAttribute(SystemTypes.PureAttribute) != null;
      return res;

      }
    public static bool IsDeclaredReadingGlobals(Method m) {

      if (WorstCase)
        return true;

      bool res = false;

      AttributeNode attr = m.GetAttribute(SystemTypes.GlobalReadAttribute);
      if (attr != null) {
        GlobalReadAttribute readAttr = (GlobalReadAttribute)attr.GetRuntimeAttribute();
        if (readAttr != null)
          res = readAttr.Value;
        }
      else {

        if (!IsDeclaredAccessingGlobals(m))
          return false;
        if (IsAssumedConfinedMethod(m))
          return false;
        if(IsDeclaredWriteConfined(m))
          return false;
        }
      return res;
      }

    public static bool IsDeclaredWritingGlobals(Method m) {
      if (WorstCase)
        return true;

      bool res = false;

      AttributeNode attr = m.GetAttribute(SystemTypes.GlobalWriteAttribute);
      if (attr != null) {
        GlobalWriteAttribute writeAttr = (GlobalWriteAttribute)attr.GetRuntimeAttribute();
        if (writeAttr != null)
          res = writeAttr.Value;
        }
      else {

        if (!IsDeclaredAccessingGlobals(m))
          return false;
        if (IsAssumedPureMethod(m)
            || IsDeclaredWriteConfined(m))
          return false;
        }
      return res;
      }
    public static bool IsDeclaredAccessingGlobals(Method m) {
      if (WorstCase)
        return true;

      bool res = true;


      AttributeNode attr = m.GetAttribute(SystemTypes.GlobalAccessAttribute);
      if (attr != null) {
        GlobalAccessAttribute readAttr = (GlobalAccessAttribute)attr.GetRuntimeAttribute();
        if (readAttr != null)
          res = readAttr.Value;
        }
      else {
        if (IsAssumedConfinedMethod(m))
          return false;

        // We assume thet constructor are not writing globals
        if (CciHelper.IsConstructor(m))
          res = false;

        //if (m.DeclaringType != null && CciHelper.IsInterface(m.DeclaringType) 
        //    && m.DeclaringType.FullName.StartsWith("System.Collections"))
        //    return false;
        }
      return res;
      }
    public static bool IsDeclaredFresh(Method m) {
      if (WorstCase)
        return false;

      bool res = false;
      res = m.GetAttribute(SystemTypes.FreshAttribute) != null;
      if (m.ReturnAttributes != null && m.ReturnAttributes.Count != 0) {
        foreach (AttributeNode attr in m.ReturnAttributes) {
          res = res || attr.Type.Equals(SystemTypes.FreshAttribute);
          }
        }
      return res;
      }
    public static bool IsDeclaredFresh(Parameter p) {
      if (WorstCase)
        return false;

      bool res = false;
      res = p.GetAttribute(SystemTypes.FreshAttribute) != null;
      return res;
      }

    public static bool IsDeclaredWriteConfined(Method m) {
      if (WorstCase)
        return false;
      if (PointsToAndEffectsAnnotations.writeConfinedByDefault) return true;
      return m.IsWriteConfined;
      }
    public static bool IsDeclaredWriteConfined(Parameter p) {
      if (WorstCase)
        return false;
      if (PointsToAndEffectsAnnotations.writeConfinedByDefault) return true;
      bool res = false;
      AttributeNode attr = p.GetAttribute(SystemTypes.WriteConfinedAttribute);
      if (attr != null) {
        WriteConfinedAttribute wAttr = (WriteConfinedAttribute)attr.GetRuntimeAttribute();
        res = wAttr.Value;
        }
      else
        res = false;
      // res = p.GetAttribute(SystemTypes.WriteConfinedAttribute) != null;
      return res;
      }

    public static bool IsDeclaredWrite(Method m) {
      if (WorstCase)
        return true;
      bool res = true;
      AttributeNode attr = m.GetAttribute(SystemTypes.WriteAttribute);
      if (attr != null) {
        WriteAttribute wAttr = (WriteAttribute)attr.GetRuntimeAttribute();
        res = wAttr.Value;
        }
      else
        res = true;

      return res;
      }
    public static bool IsDeclaredWrite(Parameter p) {
      if (WorstCase)
        return true;
      bool res = true;
      AttributeNode attr = p.GetAttribute(SystemTypes.WriteAttribute);
      if (attr != null) {
        WriteAttribute wAttr = (WriteAttribute)attr.GetRuntimeAttribute();
        res = wAttr.Value;
        }
      else
        res = true;

      return res;
      }
    public static bool IsDeclaredRead(Parameter p) {
      if (WorstCase)
        return true;
      bool res = true;
      AttributeNode attr = p.GetAttribute(SystemTypes.ReadAttribute);
      if (attr != null) {
        ReadAttribute rAttr = (ReadAttribute)attr.GetRuntimeAttribute();
        res = rAttr.Value;
        }
      return res;
      }



    public static bool IsEscaping(Method m, out bool owned) {
      if (WorstCase) {
        owned = true;
        return true;
        }

      bool res = false;
      owned = false;
      if (!m.IsStatic) {
        if (m.GetAttribute(SystemTypes.CapturedAttribute) != null) {
          owned = true;
          return true;
          }
        }
      AttributeNode attr = m.GetAttribute(SystemTypes.EscapesAttribute);
      if (attr != null) {
        EscapesAttribute escAttr = (EscapesAttribute)attr.GetRuntimeAttribute();
        if (escAttr != null) {
          res = escAttr.Value;
          owned = escAttr.Owned;
          }
        }
      return res && !IsConstructor(m);
      }
    public static bool IsDeclaredEscaping(Parameter p, out bool owned) {
      if (WorstCase) {
        owned = true;
        return true;
        }
      bool res = false;
      owned = false;
      if (p.GetAttribute(SystemTypes.CapturedAttribute) != null) {
        owned = true;
        return true;
        }
      if (p is This) {
        Method m = ((This)p).DeclaringMethod;
        return IsEscaping(m, out owned);
        }
      AttributeNode attr = p.GetAttribute(SystemTypes.EscapesAttribute);
      if (attr != null) {
        EscapesAttribute escAttr = (EscapesAttribute)attr.GetRuntimeAttribute();
        if (escAttr != null) {
          res = escAttr.Value;
          owned = escAttr.Owned;
          }
        }
      return res;
      }

    // Used for determining Write and WriteConfined knowin the callee
    public static bool IsWriteConfinedParameter(Method callee, Parameter p) {
      bool isWriteConfined = !PointsToAndEffectsAnnotations.IsAssumedPureMethod(callee)
                              && (PointsToAndEffectsAnnotations.IsDeclaredWriteConfined(callee)
                                  || PointsToAndEffectsAnnotations.IsDeclaredWriteConfined(p));
      return isWriteConfined;
      }

    public static bool IsWriteParameter(Method callee, Parameter p) {
      bool isWrite = (!PointsToAndEffectsAnnotations.IsAssumedPureMethod(callee)
                        && PointsToAndEffectsAnnotations.IsDeclaredWrite(p))
                     || p.IsOut;
      /*bool isWrite = (!PointsToAndEffectsAnnotations.IsDeclaredPure(callee)
                        && PointsToAndEffectsAnnotations.IsDeclaredWrite(p))
                     || p.IsOut;
      */
      isWrite = isWrite && !IsConstructor(callee);
      return isWrite;
      }
    public static bool IsAnnotated(Method m) {
      bool res = m.GetAttribute(SystemTypes.WriteAttribute) != null;
      res = res || m.GetAttribute(SystemTypes.WriteConfinedAttribute) != null;
      res = res || m.GetAttribute(SystemTypes.GlobalWriteAttribute) != null;
      res = res || m.GetAttribute(SystemTypes.GlobalReadAttribute) != null;
      res = res || m.GetAttribute(SystemTypes.PureAttribute) != null;
      if (m.Parameters != null) {
        foreach (Parameter p in m.Parameters) {
          res = res || p.GetAttribute(SystemTypes.ReadAttribute) != null;
          res = res || p.GetAttribute(SystemTypes.PureAttribute) != null;
          res = res || p.GetAttribute(SystemTypes.WriteAttribute) != null;
          res = res || p.GetAttribute(SystemTypes.WriteConfinedAttribute) != null;
          res = res || p.GetAttribute(SystemTypes.EscapesAttribute) != null;
          }
        }


      return res;
      }

    //private static bool starWith(string key, string[] names)
    private static bool starWith(string key, List<string> names) {
      bool res = false;

      foreach (string s in names) {
        if (key.StartsWith(s)) {
          return true;
          }
        }
      return res;
      }
    public static bool IsConstructor(Method m) {
      bool isCtor = m is InstanceInitializer;
      return isCtor;
      }
    #endregion
    }
  }
