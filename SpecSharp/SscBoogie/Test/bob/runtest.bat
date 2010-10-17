@echo off
setlocal

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe
set SSC_ARGS=/verifyopt:/nologo,%1 /verifyopt:/nologo,%2 /verifyopt:/nologo,%3 /verifyopt:/nologo,%4 /verifyopt:/nologo,%5 /verifyopt:/nologo,%6 /verifyopt:/nologo,%7 /verifyopt:/nologo,%8 /verifyopt:/nologo,%9

for %%f in (Change769.ssc Change773.ssc Change774.ssc Change775.ssc Bug163.ssc IntToString.ssc StaticField.ssc IndexerInExpr.ssc Literals.ssc) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug /verify %SSC_ARGS% %%f
)

for %%f in (loopinv0-nonnull) do (
  echo.
  echo -------------------- loopinv0-nonnull\%%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll
)

for %%f in (AbsInt.dll AIFramework.dll BoogiePlugin.dll BytecodeTranslation.dll VCGeneration.dll Core.dll DriverHelper.dll) do (
  echo.
  echo -------------------- %%f --------------------
  "%BGEXE%" %* /noVerify "%BOOGIEDIR%/%%f"
)
