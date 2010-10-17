rem @echo off
rem Builds Spec# into the NewLKG directory

if "%1"=="" (
  echo Missing command-line argument.
  echo Usage: makespecsharp 1.0.5016.0
  goto errorEnd
)

echo Using version quad "%1". This should be something like "1.0.5016.0". Make sure this is different from the current LKG version in Microsoft.SpecSharp\LastKnownGood. Continue?
pause

rmdir /s /q NewLKG
mkdir NewLKG
if errorlevel 1 goto errorEnd
mkdir NewLKG\Bin
mkdir NewLKG\Obj

echo using System.Reflection;[assembly:AssemblyVersion("%1")][assembly:AssemblyFileVersion("%1")] > NewLKG\Obj\version.cs

cd ..

cd System.Compiler.Runtime
csc /target:library /out:..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.Runtime.dll /define:TRACE;LKG /debug:pdbonly AssemblyInfo.cs Classes.cs ..\Microsoft.SpecSharp\NewLKG\Obj\version.cs
if errorlevel 1 goto errorEnd
cd ..

cd System.Compiler
resgen ExceptionStrings.resx ..\Microsoft.SpecSharp\NewLKG\Obj\ExceptionStrings.resources
if errorlevel 1 goto errorEnd
csc /target:library /out:..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.dll /unsafe /define:ExtendedRuntime /debug:pdbonly /reference:..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.Runtime.dll AssemblyCache.cs AssemblyInfo.cs Comparer.cs DoubleVisitor.cs Duplicator.cs ExceptionStrings.cs /resource:..\Microsoft.SpecSharp\NewLKG\Obj\ExceptionStrings.resources,System.Compiler.ExceptionStrings.resources FastFileIO.cs ListTemplate.cs MemoryMappedFile.cs Metadata.cs Nodes.cs OpCode.cs Reader.cs Specializer.cs StandardIds.cs StandardVisitor.cs SystemTypes.cs Unstacker.cs Updater.cs ..\Microsoft.SpecSharp\NewLKG\Obj\version.cs Writer.cs
if errorlevel 1 goto errorEnd
del ..\Microsoft.SpecSharp\NewLKG\Obj\ExceptionStrings.resources
cd ..

cd System.Compiler.Framework
resgen Compiler\ErrorMessages.resx ..\Microsoft.SpecSharp\NewLKG\Obj\ErrorMessages.resources
if errorlevel 1 goto errorEnd
csc /target:library /out:..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.Framework.dll /unsafe /debug:pdbonly /reference:..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.Runtime.dll /reference:..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.dll AssemblyInfo.cs /resource:..\Microsoft.SpecSharp\NewLKG\Obj\ErrorMessages.resources,System.Compiler.Compiler.ErrorMessages.resources ..\Microsoft.SpecSharp\NewLKG\Obj\version.cs Analysis\Analyzer.cs Analysis\CciUtils.cs Analysis\CFG.cs Analysis\CFGanalysis.cs Analysis\CodeFlattener.cs Analysis\CodePrinter.cs Analysis\ControlFlow.cs Analysis\DataStructUtils.cs Analysis\DefiniteAssignmentAnalysis.cs Analysis\EGraph.cs Analysis\EmptyVisitor.cs Analysis\InstructionVisitor.cs Analysis\NonNullAnalysis.cs Analysis\ObservationalPurityAnalysis.cs Analysis\PurityAnalysis.cs Analysis\StackDepthAnalysis.cs Analysis\StronglyConnectedComps.cs Compiler\Checker.cs Compiler\CodeDom.cs Compiler\Compiler.cs Compiler\Declarer.cs Compiler\Error.cs Compiler\Evaluator.cs Compiler\Finders.cs Compiler\GuardedFieldAccessInstrumenter.cs Compiler\Looker.cs Compiler\Normalizer.cs Compiler\Optimizer.cs Compiler\Partitioner.cs Compiler\Passer.cs Compiler\Resolver.cs Compiler\Runtime.cs Compiler\Scoper.cs Compiler\TypeSystem.cs Debugger\Debugger.cs Debugger\ExpressionEval.cs LanguageService\LanguageService.cs LanguageService\Scanner.cs Query\QueryComposer.cs Query\Runtime.cs Query\TypeSystem.cs Query\XmlHelper.cs Serializer\DeserializerParser.cs Serializer\DeserializerScanner.cs Serializer\Serializer.cs
if errorlevel 1 goto errorEnd
del ..\Microsoft.SpecSharp\NewLKG\Obj\ErrorMessages.resources
cd ..

cd Microsoft.VisualStudio.Package
resgen Project\PropertySheet.resx ..\Microsoft.SpecSharp\NewLKG\Obj\PropertySheet.resources
if errorlevel 1 goto errorEnd
resgen Project\UIStrings.resx ..\Microsoft.SpecSharp\NewLKG\Obj\UIStrings.resources
if errorlevel 1 goto errorEnd
csc /target:library /out:..\Microsoft.SpecSharp\NewLKG\Bin\Microsoft.VisualStudio.Package.dll /define:TRACE /debug:pdbonly /reference:..\Common\Bin\Microsoft.VisualStudio.TextManager.Interop.dll /reference:..\Common\Bin\Microsoft.VisualStudio.Designer.Interfaces.dll /reference:..\Common\Bin\microsoft.visualstudio.ole.interop.dll /reference:..\Common\Bin\Microsoft.VisualStudio.Shell.Interop.dll /reference:%WINDIR%\Microsoft.NET\Framework\v1.1.4322\envdte.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll AssemblyInfo.cs DebugTools.cs ..\Microsoft.SpecSharp\NewLKG\Obj\version.cs LanguageService\CodeWindowManager.cs LanguageService\Colorizer.cs /resource:LanguageService\completionset.bmp,Microsoft.VisualStudio.Package.LanguageService.completionset.bmp LanguageService\EditorView.cs LanguageService\LanguageService.cs LanguageService\Scanner.cs LanguageService\Source.cs LanguageService\ViewFilter.cs Project\Automation.cs Project\configprovider.cs Project\DataObject.cs Project\EditorFactory.cs /resource:Project\folders.bmp,Microsoft.VisualStudio.Package.Project.folders.bmp Project\Hierarchy.cs Project\hierarchyitem.cs Project\ImageList.cs /resource:Project\InstallDebugger.rgs,Microsoft.VisualStudio.Package.Project.InstallDebugger.rgs /resource:Project\InstallEditor.rgs,Microsoft.VisualStudio.Package.Project.InstallEditor.rgs /resource:Project\InstallLanguage.rgs,Microsoft.VisualStudio.Package.Project.InstallLanguage.rgs /resource:Project\InstallPackage.rgs,Microsoft.VisualStudio.Package.Project.InstallPackage.rgs /resource:Project\InstallProject.rgs,Microsoft.VisualStudio.Package.Project.InstallProject.rgs /resource:Project\InstallVSIPLicense.rgs,Microsoft.VisualStudio.Package.Project.InstallVSIPLicense.rgs Project\Package.cs Project\Project.cs Project\ProjectConfig.cs Project\ProjectFactory.cs Project\PropertyPages.cs Project\PropertySheet.cs /resource:..\Microsoft.SpecSharp\NewLKG\Obj\PropertySheet.resources,Microsoft.VisualStudio.Package.Project.PropertySheet.resources Project\Registration.cs Project\RgsParser.cs Project\selection.cs Project\TaskItem.cs Project\UIStrings.cs /resource:..\Microsoft.SpecSharp\NewLKG\Obj\UIStrings.resources,Microsoft.VisualStudio.Package.Project.UIStrings.resources Project\Utilities.cs Project\VsCommands.cs
if errorlevel 1 goto errorEnd
del ..\Microsoft.SpecSharp\NewLKG\Obj\PropertySheet.resources
del ..\Microsoft.SpecSharp\NewLKG\Obj\UIStrings.resources
cd ..

cd Microsoft.VisualStudio.IntegrationHelper
csc /target:library /out:..\Microsoft.SpecSharp\NewLKG\Bin\Microsoft.VisualStudio.IntegrationHelper.dll /define:TRACE /debug:pdbonly /reference:..\Common\Bin\Microsoft.VisualStudio.TextManager.Interop.dll /reference:..\Common\Bin\Microsoft.VisualStudio.Designer.Interfaces.dll /reference:..\Common\Bin\microsoft.visualstudio.ole.interop.dll /reference:..\Common\Bin\Microsoft.VisualStudio.Shell.Interop.dll /reference:envdte.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:..\Microsoft.SpecSharp\NewLKG\Bin\Microsoft.VisualStudio.Package.dll /reference:..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.Framework.dll /reference:..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.dll AssemblyInfo.cs Helpers.cs ..\Microsoft.SpecSharp\NewLKG\Obj\version.cs Debugger\DebuggerHelpers.cs Debugger\DebuggerInterop.cs Debugger\Nodes.cs Debugger\Symbol.cs Debugger\Values.cs
if errorlevel 1 goto errorEnd
cd ..

cd Microsoft.SpecSharp\LastKnownGood
gacutil /nologo /i System.Compiler.Runtime.dll /f
if errorlevel 1 goto errorEnd
gacutil /nologo /i Microsoft.SpecSharp.Runtime.dll /f
if errorlevel 1 goto errorEnd
cd ..\..

cd Microsoft.SpecSharp
resgen ErrorMessages.resx NewLKG\Obj\ErrorMessages.resources
if errorlevel 1 goto errorEnd
csc /target:library /out:NewLKG\Bin\Microsoft.SpecSharp.dll /debug:pdbonly /reference:..\Common\Bin\Microsoft.VisualStudio.Shell.Interop.dll /reference:..\Common\Bin\Microsoft.VisualStudio.OLE.Interop.dll /reference:..\Common\Bin\Microsoft.VisualStudio.TextManager.Interop.dll /reference:NewLKG\Bin\Microsoft.VisualStudio.IntegrationHelper.dll /reference:NewLKG\Bin\Microsoft.VisualStudio.Package.dll /reference:NewLKG\Bin\System.Compiler.Runtime.dll /reference:NewLKG\Bin\System.Compiler.Framework.dll /reference:NewLKG\Bin\System.Compiler.dll /reference:LastKnownGood\Microsoft.SpecSharp.Runtime.dll Analyzer.cs AssemblyInfo.cs Checker.cs Compiler.cs CustomChecker.cs Debugger.cs DebugTools.cs Declarer.cs Duplicator.cs Error.cs /resource:NewLKG\Obj\ErrorMessages.resources,Microsoft.SpecSharp.ErrorMessages.resources /resource:Folders.bmp,Microsoft.SpecSharp.Folders.bmp /resource:Install.rgs,Microsoft.SpecSharp.Install.rgs LanguageService.cs Looker.cs Nodes.cs Normalizer.cs Parser.cs Resolver.cs Runtime.cs Scanner.cs Scoper.cs Specializer.cs TypeSystem.cs NewLKG\Obj\version.cs VisualStudioIntegration.cs
if errorlevel 1 goto errorEnd
del NewLKG\Obj\ErrorMessages.resources
cd ..

cd Microsoft.SpecSharp\CommandLineCompiler
resgen Messages.resx ..\NewLKG\Obj\Messages.resources
mkdir ..\NewLKG\Bin\Bin
csc /out:..\NewLKG\Bin\Bin\ssc.exe /debug:pdbonly /reference:..\NewLKG\Bin\Microsoft.SpecSharp.dll /reference:..\NewLKG\Bin\System.Compiler.Framework.dll /reference:..\NewLKG\Bin\System.Compiler.dll AssemblyInfo.cs Main.cs /resource:..\NewLKG\Obj\Messages.resources,ssc.Messages.resources ..\NewLKG\Obj\version.cs
if errorlevel 1 goto errorEnd
del ..\NewLKG\Obj\Messages.resources
move ..\NewLKG\Bin\Bin\ssc.exe ..\NewLKG\Bin\ssc.exe
move ..\NewLKG\Bin\Bin\ssc.pdb ..\NewLKG\Bin\ssc.pdb
rmdir ..\NewLKG\Bin\Bin
cd ..\..

cd Microsoft.SpecSharp\Resources
rc /fo ..\NewLKG\Obj\resources.res resources.rc
if errorlevel 1 goto errorEnd
csc /target:library /win32res:..\NewLKG\Obj\resources.res /out:..\NewLKG\Bin\Microsoft.SpecSharp.resources.dll AssemblyInfo.cs
if errorlevel 1 goto errorEnd
del ..\NewLKG\Obj\resources.res
mkdir ..\NewLKG\Bin\1033
move ..\NewLKG\Bin\Microsoft.SpecSharp.resources.dll ..\NewLKG\Bin\1033
cd ..\..

cd Microsoft.SpecSharp\Runtime
..\NewLKG\Bin\ssc /target:library /out:..\NewLKG\Bin\Microsoft.SpecSharp.Runtime.dll /define:TRACE /debug:pdbonly /nostdlib /reference:%WINDIR%\Microsoft.NET\Framework\v1.1.4322\mscorlib.dll /reference:..\NewLKG\Bin\System.Compiler.Runtime.dll ..\NewLKG\Obj\version.cs Collections.ssc AssemblyInfo.ssc Classes.ssc
if errorlevel 1 goto errorEnd
cd ..\..

cd Microsoft.SpecSharp
resgen ErrorMessages.resx NewLKG\Obj\ErrorMessages.resources
if errorlevel 1 goto errorEnd
csc /target:library /out:NewLKG\Bin\Microsoft.SpecSharp.dll /debug:pdbonly /reference:..\Common\Bin\Microsoft.VisualStudio.Shell.Interop.dll /reference:..\Common\Bin\Microsoft.VisualStudio.OLE.Interop.dll /reference:..\Common\Bin\Microsoft.VisualStudio.TextManager.Interop.dll /reference:NewLKG\Bin\Microsoft.VisualStudio.IntegrationHelper.dll /reference:NewLKG\Bin\Microsoft.VisualStudio.Package.dll /reference:NewLKG\Bin\System.Compiler.Runtime.dll /reference:NewLKG\Bin\System.Compiler.Framework.dll /reference:NewLKG\Bin\System.Compiler.dll /reference:NewLKG\Bin\Microsoft.SpecSharp.Runtime.dll Analyzer.cs AssemblyInfo.cs Checker.cs Compiler.cs CustomChecker.cs Debugger.cs DebugTools.cs Declarer.cs Duplicator.cs Error.cs /resource:NewLKG\Obj\ErrorMessages.resources,Microsoft.SpecSharp.ErrorMessages.resources /resource:Folders.bmp,Microsoft.SpecSharp.Folders.bmp /resource:Install.rgs,Microsoft.SpecSharp.Install.rgs LanguageService.cs Looker.cs Nodes.cs Normalizer.cs Parser.cs Resolver.cs Runtime.cs Scanner.cs Scoper.cs Specializer.cs TypeSystem.cs NewLKG\Obj\version.cs VisualStudioIntegration.cs
if errorlevel 1 goto errorEnd
del NewLKG\Obj\ErrorMessages.resources
cd ..

cd System.Compiler.Sql.Runtime
csc /target:library /out:..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.Sql.Runtime.dll /define:TRACE /debug:pdbonly /reference:..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.Runtime.dll AssemblyInfo.cs Sql.cs ..\Microsoft.SpecSharp\NewLKG\Obj\version.cs
if errorlevel 1 goto errorEnd
cd ..

cd System.Compiler.Sql
resgen ErrorMessages.resx ..\Microsoft.SpecSharp\NewLKG\Obj\ErrorMessages.resources
if errorlevel 1 goto errorEnd
csc /target:library /out:..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.Sql.dll /define:TRACE /debug:pdbonly /reference:..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.Framework.dll /reference:..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.Runtime.dll /reference:..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.Sql.Runtime.dll /reference:..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.dll AssemblyInfo.cs Checker.cs composer.cs Error.cs /resource:..\Microsoft.SpecSharp\NewLKG\Obj\ErrorMessages.resources,System.Compiler.Sql.ErrorMessages.resources Normalizer.cs Runtime.cs ..\Microsoft.SpecSharp\NewLKG\Obj\version.cs
if errorlevel 1 goto errorEnd
del NewLKG\Obj\ErrorMessages.resources
cd ..

cd Contracts\Mscorlib
..\..\Microsoft.SpecSharp\NewLKG\Bin\ssc /target:library /out:..\..\Microsoft.SpecSharp\NewLKG\Bin\Mscorlib.Contracts.dll /define:TRACE;DEBUG /nostdlib /shadow:%WINDIR%\Microsoft.NET\Framework\v1.1.4322\mscorlib.dll /reference:%WINDIR%\Microsoft.NET\Framework\v1.1.4322\mscorlib.dll /reference:..\..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.Runtime.dll Microsoft.*.ssc System.*.ssc
if errorlevel 1 goto errorEnd
cd ..\..

cd Contracts\System
..\..\Microsoft.SpecSharp\NewLKG\Bin\ssc /target:library /out:..\..\Microsoft.SpecSharp\NewLKG\Bin\System.Contracts.dll /define:TRACE;DEBUG /nostdlib /shadow:%WINDIR%\Microsoft.NET\Framework\v1.1.4322\System.dll /reference:%WINDIR%\Microsoft.NET\Framework\v1.1.4322\mscorlib.dll /reference:%WINDIR%\Microsoft.NET\Framework\v1.1.4322\System.dll /reference:..\..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.Runtime.dll Microsoft.*.ssc System.*.ssc
if errorlevel 1 goto errorEnd
cd ..\..

cd Contracts\System.Compiler
..\..\Microsoft.SpecSharp\NewLKG\Bin\ssc /target:library /out:..\..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.Contracts.dll /define:TRACE;DEBUG /nostdlib /shadow:..\..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.dll /reference:%WINDIR%\Microsoft.NET\Framework\v1.1.4322\mscorlib.dll /reference:..\..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.Runtime.dll /reference:..\..\Microsoft.SpecSharp\NewLKG\Bin\System.Compiler.dll System.Compiler.ssc
if errorlevel 1 goto errorEnd
cd ..\..

goto end

:errorEnd
exit /b 1

:end
exit /b 0