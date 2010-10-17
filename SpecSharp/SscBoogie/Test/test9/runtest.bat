@echo off
setlocal

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe

REM just check translation of these files
for %%f in (RoadblockDup) do (
  echo.
  echo -------------------- %%f --------------------
  %SSCEXE% /target:library /debug %%f.ssc
  %BGEXE% %* %%f.dll /noVerify
)

for %%f in (ParamOut MethodCallInContracts) do (
  echo.
  echo -------------------- %%f --------------------
  %SSCEXE% /target:library /debug %%f.ssc
  %BGEXE% %* %%f.dll
)
