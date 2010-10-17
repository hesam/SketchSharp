@echo off
setlocal

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe

echo Writing to Output.trace for /trace filtering...
echo Start > Output.trace

for %%f in (VisibilityBasedInvariants Spouse) do (
  echo. >> Output.trace
  echo -------------------- %%f -------------------- >> Output.trace
  "%SSCEXE%" /target:library /debug %%f.ssc >> Output.trace
  "%BGEXE%" %* /trace /level:1 %%f.dll >> Output.trace
)
for %%f in (List2) do (
  echo. >> Output.trace
  echo -------------------- %%f -------------------- >> Output.trace
  "%SSCEXE%" /target:library /debug %%f.ssc >> Output.trace
  "%BGEXE%" %* /trace %%f.dll >> Output.trace
)

REM remove the times that are included in the /trace output
type Output.trace | %SystemRoot%\system32\find /v "  [" | %SystemRoot%\system32\find /v " ..."
