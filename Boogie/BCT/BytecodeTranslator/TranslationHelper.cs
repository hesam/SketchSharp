﻿//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Cci;
using Microsoft.Cci.MetadataReader;
using Microsoft.Cci.MutableCodeModel;
using Microsoft.Cci.Contracts;
using Microsoft.Cci.ILToCodeModel;

using Bpl = Microsoft.Boogie;


namespace BytecodeTranslator {

  public class MethodParameter {
    
    /// <summary>
    /// All parameters of the method get an associated in parameter
    /// in the Boogie procedure except for out parameters.
    /// </summary>
    public Bpl.Formal/*?*/ inParameterCopy;
    
    /// <summary>
    /// A local variable when the underlyingParameter is an in parameter
    /// and a formal (out) parameter when the underlyingParameter is
    /// a ref or out parameter.
    /// </summary>
    public Bpl.Variable outParameterCopy;

    public IParameterDefinition underlyingParameter;

    public MethodParameter(IParameterDefinition parameterDefinition) {

      this.underlyingParameter = parameterDefinition;

      Bpl.Type ptype = Bpl.Type.Int;

      var parameterToken = parameterDefinition.Token();
      var typeToken = parameterDefinition.Type.Token();
      var parameterName = parameterDefinition.Name.Value;

      if (!parameterDefinition.IsOut) {
        this.inParameterCopy = new Bpl.Formal(parameterToken, new Bpl.TypedIdent(typeToken, parameterName + "$in", ptype), true);
      }
      if (parameterDefinition.IsByReference || parameterDefinition.IsOut) {
        this.outParameterCopy = new Bpl.Formal(parameterToken, new Bpl.TypedIdent(typeToken, parameterName + "$out", ptype), false);
      } else {
        this.outParameterCopy = new Bpl.LocalVariable(parameterToken, new Bpl.TypedIdent(typeToken, parameterName, ptype));
      }
      
    }

    public override string ToString() {
      return this.underlyingParameter.Name.Value;
    }
  }

    /// <summary>
    /// Class containing several static helper functions to convert
    /// from Cci to Boogie
    /// </summary>
  static class TranslationHelper {

    public static Bpl.IToken Token(this IObjectWithLocations objectWithLocations) {
      //TODO: use objectWithLocations.Locations!
      Bpl.IToken tok = Bpl.Token.NoToken;
      return tok;
    }

    private static int tmpVarCounter = 0;
    public static string GenerateTempVarName() {
      return "$tmp" + (tmpVarCounter++).ToString();
    }

    public static string CreateUniqueMethodName(IMethodDefinition method) {
      return method.ContainingType.ToString() + "." + method.Name.Value + "$" + method.Type.ResolvedType.ToString();
    }

    #region Temp Stuff that must be replaced as soon as there is real code to deal with this

    public static Bpl.Type CciTypeToBoogie(ITypeReference type) {
      return Bpl.Type.Int;
    }

    public static Bpl.Variable TempHeapVar() {
      return new Bpl.GlobalVariable(Bpl.Token.NoToken, new Bpl.TypedIdent(Bpl.Token.NoToken, "$Heap", Bpl.Type.Int)); 
    }

    public static Bpl.Variable TempThisVar() {
      return new Bpl.GlobalVariable(Bpl.Token.NoToken, new Bpl.TypedIdent(Bpl.Token.NoToken, "this", Bpl.Type.Int));
    }

    #endregion

  }
}
