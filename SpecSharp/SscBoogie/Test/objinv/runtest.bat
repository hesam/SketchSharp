@echo off
setlocal

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe
set SSC_ARGS=/verifyopt:/nologo,%1 /verifyopt:/nologo,%2 /verifyopt:/nologo,%3 /verifyopt:/nologo,%4 /verifyopt:/nologo,%5 /verifyopt:/nologo,%6 /verifyopt:/nologo,%7 /verifyopt:/nologo,%8 /verifyopt:/nologo,%9

for %%f in (Interval) do (
  echo.
  echo -------------------- %%f.ssc --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* /simplifyMatchDepth:5 %%f.dll
)
for %%f in (ValidAndPrevalid ExposelessUpdates) do (
  echo.
  echo -------------------- %%f.ssc --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll
)
for %%f in (BasicMethodology.ssc Pack.ssc) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug /verify %SSC_ARGS% %%f
)
