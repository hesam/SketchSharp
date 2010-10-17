//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
#if CCINamespace
using Microsoft.Cci;
using Cci = Microsoft.Cci;
#else
using System.Compiler;
using Cci = System.Compiler;
#endif
using System;
using System.Compiler.WPurity;
using System.Compiler.Analysis;

namespace Microsoft.SpecSharp
{
    public class Analyzer : Cci.Analyzer
    {
        public Analyzer(TypeSystem t, Compilation c)
            : base(t, c)
        {
            if (c != null)
            {
                SpecSharpCompilerOptions ssco = c.CompilerParameters as SpecSharpCompilerOptions;
                if (ssco != null)
                {
                    if (ssco.Compatibility)
                        this.NonNullChecking = false; // i.e., turn it off if we need to be compatible
                    this.WeakPurityAnalysis = ssco.CheckPurity;
                    // Diego said it is important that the same instance of the purity analysis is used across all
                    // methods in the compilation unit. So create it here and just call it for each method in 
                    // the override for language specific analysis.
                    // PointsToAnalysis.verbose = true;
                    if (this.WeakPurityAnalysis)
                    {
                        
                        // InterProcedural bottom up traversal with fixpoint
                        //this.WeakPurityAnalyzer = new WeakPurityAndWriteEffectsAnalysis(this,typeSystem, c,true,true);
                        
                        // Only Intraprocedural (in this mode doesnot support delegates...)
                        //this.WeakPurityAnalyzer = new WeakPurityAndWriteEffectsAnalysis(this, typeSystem, c, false);
                        
                        // Interprocedural top-down inlining simulation (with max-depth)
                        this.WeakPurityAnalyzer = new WeakPurityAndWriteEffectsAnalysis(this, typeSystem, c, true,false,2);

                        this.WeakPurityAnalyzer.StandAloneApp = false;
                        this.WeakPurityAnalyzer.BoogieMode = true;
                    }
                    
                    /// Reentrancy ANALYSIS
                    
                    this.ObjectExposureAnalysis = false;
                    ObjectConsistencyAnalysis.verbose = false;
                    if (ObjectExposureAnalysis)
                    {
                        this.ObjectExposureAnalyzer = new ObjectExposureAnalysis(this, typeSystem, c, true, false, 4);
                    }
                    this.ReentrancyAnalysis = false;
                    if (ReentrancyAnalysis)
                    {
                        this.ReentrancyAnalyzer = new ReentrancyAnalysis(this, typeSystem, c, true, false, 4);
                    }
                    
                }
            }
        }
        /// <summary>
        /// Implications from command line switches (with defaults)
        /// </summary>
        protected bool WeakPurityAnalysis = false;
        protected bool ReentrancyAnalysis = false;
        protected bool ObjectExposureAnalysis = false;

        internal WeakPurityAndWriteEffectsAnalysis WeakPurityAnalyzer = null;
        internal ReentrancyAnalysis ReentrancyAnalyzer = null;
        internal ReentrancyAnalysis ObjectExposureAnalyzer = null;

        protected override void LanguageSpecificAnalysis(Method method)
        {
            if (!this.CodeIsWellFormed)
                return;
            if (Cci.Analyzer.Debug)
            {
                ControlFlowGraph cfg = GetCFG(method);
                if (cfg != null) cfg.Display(Console.Out);
            }
            if (method.Name.Name.StartsWith("Microsoft.Contracts"))
                return;
            // Weak Purity and Effects Analysis
            if (this.WeakPurityAnalysis && this.WeakPurityAnalyzer != null)
            {
                this.WeakPurityAnalyzer.VisitMethod(method); // computes points-to and effects
                // processes results from VisitMethod: issues errors and potentially prints out detailed info.
                this.WeakPurityAnalyzer.ProcessResultsMethod(method);

                // Admissibility Check 
                PointsToAndWriteEffects ptwe = this.WeakPurityAnalyzer.GetPurityAnalysisWithDefault(method);
                if (method.IsConfined || method.IsStateIndependent)
                {
                    Microsoft.SpecSharp.ReadEffectAdmissibilityChecker reac = new Microsoft.SpecSharp.ReadEffectAdmissibilityChecker(method, typeSystem.ErrorHandler);
                    reac.CheckReadEffectAdmissibility(ptwe.ComputeEffects(ptwe.ReadEffects));
                }

            }

            if (ReentrancyAnalysis)
            {
                if (this.ReentrancyAnalyzer != null)
                {
                    this.ReentrancyAnalyzer.VisitMethod(method);
                }
            }

            if (ObjectExposureAnalysis)
            {
                if (this.ObjectExposureAnalyzer != null)
                {
                    this.ObjectExposureAnalyzer.VisitMethod(method);
                }
            }
        }


        public override CompilationUnit VisitCompilationUnit(CompilationUnit cUnit)
        {

            CompilationUnit retCUnit = base.VisitCompilationUnit(cUnit);

            if (cUnit != null && cUnit.Name != null && this.WeakPurityAnalyzer != null)
            {
                Console.Out.WriteLine("*** Declared Pure:{0}, Pure: {1}, Methods {3}, {2}",
                    this.WeakPurityAnalyzer.numberOfDeclaredPure,
                    this.WeakPurityAnalyzer.numberOfPures, cUnit.Name, this.WeakPurityAnalyzer.numberOfMethods);
            }
            return retCUnit;
        }
    }
}
