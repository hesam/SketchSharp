@echo off
setlocal

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe

for %%f in (Delegate) do (
  echo.
  echo -------------------- %%f --------------------
  %SSCEXE% /target:library /debug /d:DEBUG %%f.ssc
  %BGEXE% %* %%f.dll
)
