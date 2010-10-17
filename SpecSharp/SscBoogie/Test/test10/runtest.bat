@echo off
setlocal

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe

for %%f in (AssignToRepField PeerFields) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* /level:1 /modifiesDefault:4 %%f.dll
)
for %%f in (Assumptions RepAssign Capture AssignToNonInvariantField) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll
)
