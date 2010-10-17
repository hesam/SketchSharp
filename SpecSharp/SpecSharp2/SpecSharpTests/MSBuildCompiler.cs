﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Build.BuildEngine;
using Microsoft.Cci;
using Microsoft.Cci.Ast;
using Microsoft.Cci.SpecSharp;

namespace SpecSharpTests
{
    internal class CompilerHostEnvironment : SourceEditHostEnvironment
    {
        public CompilerHostEnvironment()
            : base(new NameTable(), 4)
        {
            PeReader = new PeReader(this);
        }

        private readonly PeReader PeReader;

        public override IUnit LoadUnitFrom(string location)
        {
            if (!System.IO.File.Exists(location))
            {
                return Dummy.Unit;
            }
            var result = PeReader.OpenModule(BinaryDocument.GetBinaryDocumentForFile(location, this));
            if (result == Dummy.Module)
            {
                return Dummy.Unit;
            }
            RegisterAsLatest(result);
            return result;
        }
    }

    internal class MSBuildCompiler
    {
        private readonly Dictionary<string, Assembly> Compilations = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        private readonly CompilerHostEnvironment Environment = new CompilerHostEnvironment();

        [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands")]
        public Assembly CompileProject(string projectFileName)
        {
            Assembly existing;
            if (Compilations.TryGetValue(Path.GetFullPath(projectFileName), out existing))
            {
                return existing;
            }

            var project = new Microsoft.Build.BuildEngine.Project();
            project.Load(projectFileName);

            var projectName = Environment.NameTable.GetNameFor(project.EvaluatedProperties["AssemblyName"].Value);
            var projectPath = project.FullFileName;
            var compilerOptions = new SpecSharpOptions();
            var assemblyReferences = new List<IAssemblyReference>();
            var moduleReferences = new List<IModuleReference>();
            var programSources = new List<SpecSharpSourceDocument>();
            var assembly = new SpecSharpAssembly(projectName, projectPath, Environment, compilerOptions, assemblyReferences, moduleReferences, programSources);
            var helper = new SpecSharpCompilationHelper(assembly.Compilation);

            Compilations[Path.GetFullPath(projectFileName)] = assembly;

            assemblyReferences.Add(Environment.LoadAssembly(Environment.CoreAssemblySymbolicIdentity));
            project.Build("ResolveAssemblyReferences");
            foreach (BuildItem item in project.GetEvaluatedItemsByName("ReferencePath"))
            {
                var assemblyName = new System.Reflection.AssemblyName(item.GetEvaluatedMetadata("FusionName"));
                var name = Environment.NameTable.GetNameFor(assemblyName.Name);
                var culture = assemblyName.CultureInfo != null ? assemblyName.CultureInfo.Name : "";
                var version = assemblyName.Version == null ? new Version(0, 0) : assemblyName.Version;
                var token = assemblyName.GetPublicKeyToken();
                if (token == null) token = new byte[0];
                var location = item.FinalItemSpec;
                var identity = new AssemblyIdentity(name, culture, version, token, location);
                var reference = Environment.LoadAssembly(identity);
                assemblyReferences.Add(reference);
            }

            foreach (BuildItem item in project.GetEvaluatedItemsByName("ProjectReference"))
            {
                var name = Environment.NameTable.GetNameFor(Path.GetFileNameWithoutExtension(item.FinalItemSpec));
                var location = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project.FullFileName), item.FinalItemSpec));
                var reference = CompileProject(location);
                assemblyReferences.Add(reference);
            }

            foreach (BuildItem item in project.GetEvaluatedItemsByName("Compile"))
            {
                var location = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project.FullFileName), item.FinalItemSpec));
                var name = Environment.NameTable.GetNameFor(location);
                var programSource = new SpecSharpSourceDocument(helper, name, location, File.ReadAllText(location));
                programSources.Add(programSource);
            }

            return assembly;
        }
    }
}