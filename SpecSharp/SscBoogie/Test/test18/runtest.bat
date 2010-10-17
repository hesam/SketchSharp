@echo off
setlocal

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe

for %%f in (BagDataRefinement) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc /cc-
  "%BGEXE%" %* %%f.dll
)
for %%f in (PureIsBaseCall ConstructorInContract) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll
)
