@echo off
setlocal

set SSCBOOGIE_DIR=.
set COMPILER_DIR=..\..\SpecSharp\Microsoft.SpecSharp\LastKnownGood9
set DEST_DIR=export

if exist %DEST_DIR%\1033 del /q %DEST_DIR%\1033\*
if exist %DEST_DIR% del /q %DEST_DIR%\*
if not exist %DEST_DIR% mkdir %DEST_DIR%
if not exist %DEST_DIR%\1033 mkdir %DEST_DIR%\1033

REM Copy the compiler stuff ----------------------
for %%f in (
  ContractsPropertyPane.dll ContractsPropertyPane.pdb
  IPropertyPane.dll IPropertyPane.pdb
  ITaskManager.dll ITaskManager.pdb
  Microsoft.SpecSharp.Runtime.dll Microsoft.SpecSharp.Runtime.pdb
  Microsoft.SpecSharp.dll Microsoft.SpecSharp.pdb
  Microsoft.SpecSharp.targets
  Microsoft.VisualStudio.Designer.Interfaces.dll
  Microsoft.VisualStudio.IntegrationHelper.dll Microsoft.VisualStudio.IntegrationHelper.pdb
  Microsoft.VisualStudio.Package.dll Microsoft.VisualStudio.Package.pdb
  Microsoft.VisualStudio.Shell.Interop.dll
  Microsoft.VisualStudio.TextManager.Interop.dll
  Microsoft.Visualstudio.OLE.Interop.dll
  Mscorlib.Contracts.dll Mscorlib.Contracts.pdb
  PropertyPage.dll PropertyPage.pdb
  System.Compiler.Contracts.dll System.Compiler.Contracts.pdb
  System.Compiler.Framework.Contracts.dll System.Compiler.Framework.Contracts.pdb
  System.Compiler.Framework.dll System.Compiler.Framework.pdb System.Compiler.Framework.xml
  System.Compiler.Runtime.dll System.Compiler.Runtime.pdb
  System.Compiler.dll System.Compiler.pdb System.Compiler.xml
  System.Contracts.dll System.Contracts.pdb
  System.Xml.Contracts.dll System.Xml.Contracts.pdb
  TaskManager.dll TaskManager.pdb
  ssc.exe ssc.pdb
  LastKnownGood.vcproj
  Microsoft.Build.Utilities.Contracts.dll
  PresentationCore.Contracts.dll
  PresentationFramework.Contracts.dll
  Clean.cmd RegisterLKG.ccnet.cmd RegisterLKG.cmd Register.cmd UpdateLKG.cmd
  Microsoft.SpecSharp.resources.dll
  PropertyPageUI.dll
  TaskManagerUI.dll
) do (
  copy %COMPILER_DIR%\%%f %DEST_DIR%
)
REM ...and the 1033 directory --------------------
for %%f in (
  Microsoft.SpecSharp.resources.dll
  PropertyPageUI.dll
  TaskManagerUI.dll
) do (
  copy %COMPILER_DIR%\1033\%%f %DEST_DIR%\1033
)

rem Copy SscBoogie ---------------------------------------
rem First, pieces from Boogie ----------------------------
for %%f in (
  AbsInt.dll AbsInt.pdb
  AIFramework.dll AIFramework.pdb
  Basetypes.dll Basetypes.pdb
  Core.dll Core.pdb
  Graph.dll Graph.pdb
  Provers.Simplify.dll Provers.Simplify.pdb
  Provers.SMTLib.dll Provers.SMTLib.pdb
  Provers.Z3.dll Provers.Z3.pdb
  VCExpr.dll VCExpr.pdb
  VCGeneration.dll VCGeneration.pdb
  TypedUnivBackPred2.sx UnivBackPred2.sx UnivBackPred2.smt
  FSharp.Core.dll
) do (
  copy %SSCBOOGIE_DIR%\%%f %DEST_DIR%
)
rem Next, SscBoogie specific pieces ----------------------
for %%f in (
  BoogiePlugin.dll BoogiePlugin.pdb
  BytecodeTranslation.dll BytecodeTranslation.pdb
  DriverHelper.dll DriverHelper.pdb
  PRELUDE.bpl
  Prelude.dll Prelude.pdb
  SscBoogie.exe SscBoogie.pdb
) do (
  copy %SSCBOOGIE_DIR%\%%f %DEST_DIR%
)

echo Done.  Now, manually put the contents of the %DEST_DIR% directory into SpecSharp.zip
