@echo off
setlocal

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe
set SSC_ARGS=/verifyopt:/nologo,%1 /verifyopt:/nologo,%2 /verifyopt:/nologo,%3 /verifyopt:/nologo,%4 /verifyopt:/nologo,%5 /verifyopt:/nologo,%6 /verifyopt:/nologo,%7 /verifyopt:/nologo,%8 /verifyopt:/nologo,%9

FOR %%f in (AllocBeforeLoop CallLoop DefaultLoopInv0) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* /noinfer %%f.dll
)

FOR %%f in (Factory.ssc FieldUpdateLoop.ssc ModifyOther.ssc
            WhileTrueLoop.ssc Irreducible.ssc PeerConsistentLoop.ssc) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug /verify %SSC_ARGS% %%f
)
