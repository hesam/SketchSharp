//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;

#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  /// <summary>
  /// A version of System.IO.Path that does not throw exceptions.
  /// </summary>
  public sealed class PathWrapper{
    public static string Combine(string path1, string path2){
      if (path1 == null || path1.Length == 0) return path2;
      if (path2 == null || path2.Length == 0) return path1;
      char ch = path2[0];
      if (ch == System.IO.Path.DirectorySeparatorChar || ch == System.IO.Path.AltDirectorySeparatorChar || (path2.Length >= 2 && path2[1] == System.IO.Path.VolumeSeparatorChar))
        return path2;
      ch = path1[path1.Length - 1];
      if (ch != System.IO.Path.DirectorySeparatorChar && ch != System.IO.Path.AltDirectorySeparatorChar && ch != System.IO.Path.VolumeSeparatorChar)
        return (path1 + System.IO.Path.DirectorySeparatorChar + path2);
      return path1 + path2;
    }
    public static string GetExtension(string path) {
      if (path == null) return null;
      int length = path.Length;
      for (int i = length; --i >= 0; ) {
        char ch = path[i];
        if (ch == '.') {
          if (i != length - 1)
            return path.Substring(i, length - i);
          else
            return String.Empty;
        }
        if (ch == System.IO.Path.DirectorySeparatorChar || ch == System.IO.Path.AltDirectorySeparatorChar || ch == System.IO.Path.VolumeSeparatorChar)
          break;
      }
      return string.Empty;
    }
    public static String GetFileName(string path){
      if (path == null) return null;
      int length = path.Length;
      for (int i = length; --i >= 0; ) {
        char ch = path[i];
        if (ch == System.IO.Path.DirectorySeparatorChar || ch == System.IO.Path.AltDirectorySeparatorChar || ch == System.IO.Path.VolumeSeparatorChar)
          return path.Substring(i+1);
      }
      return path;
    }
    public static String GetDirectoryName(string path){
      if (path == null) return null;
      int length = path.Length;
      for (int i = length; --i >= 0; ) {
        char ch = path[i];
        if (ch == System.IO.Path.DirectorySeparatorChar || ch == System.IO.Path.AltDirectorySeparatorChar || ch == System.IO.Path.VolumeSeparatorChar)
          return path.Substring(0, i);
      }
      return path;
    }
    public static bool IsPathRooted(string path) {
      if (path != null) {
        int num1 = path.Length;
        if ((num1 >= 1 && (path[0] == System.IO.Path.DirectorySeparatorChar || path[0] == System.IO.Path.AltDirectorySeparatorChar)) || 
          (num1 >= 2 && path[1] == System.IO.Path.VolumeSeparatorChar)) {
          return true;
        }
      }
      return false;
    }
  }

}
