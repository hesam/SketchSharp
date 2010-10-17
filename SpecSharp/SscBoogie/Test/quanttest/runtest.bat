@echo off
setlocal

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe

for %%f in (quant0 quant1) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll
)
