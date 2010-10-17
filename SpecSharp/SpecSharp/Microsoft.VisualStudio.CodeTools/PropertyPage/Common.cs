//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
namespace Microsoft.VisualStudio.CodeTools
{
  using System;
  using System.Collections.Generic;
  using Microsoft.VisualStudio.Shell.Interop;
  
  // Common static helper functions
  static internal class Common
  {
    #region Private fields
    static private IServiceProvider serviceProvider;
    #endregion

    #region Construction
    static public void Initialize(IServiceProvider provider)
    {
      serviceProvider = provider;
    }

    static public void Release()
    {
      serviceProvider = null;
    }
    #endregion

    #region Utilities
    static public void Trace(string message)
    {
      System.Diagnostics.Trace.WriteLine("CodeTools: " + message);
    }

    static public object GetService(Type serviceType)
    {
      if (serviceProvider != null)
        return serviceProvider.GetService(serviceType);
      else
        return null;
    }
    
    static public String GetLocalRegistryRoot()
    {
      // Get the visual studio root key
      string vsRoot = null;
      ILocalRegistry2 localReg = GetService(typeof(ILocalRegistry)) as ILocalRegistry2;
      if (localReg != null) {
        localReg.GetLocalRegistryRoot(out vsRoot);
        if (vsRoot != null) {
          return vsRoot;
        }
      }
      return @"Software\Microsoft\VisualStudio\9.0"; // Guess on failure
    }
    
    /*
    static public void ShowHelp(string helpKeyword)
    {
      if (helpKeyword != null && helpKeyword.Length > 0) {
        IVsHelpSystem help = GetService(typeof(SVsHelpService)) as IVsHelpSystem;
        if (help != null) {
          help.KeywordSearch(helpKeyword, 0, 0);
          help.ActivateHelpSystem(0);
        }
      }
    }
    */

    #endregion
  }
}
