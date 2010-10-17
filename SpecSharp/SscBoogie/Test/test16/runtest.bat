@echo off
setlocal

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe
set BPLEXE=%BOOGIEDIR%\Boogie.exe
set SSC_ARGS=/verifyopt:/nologo,%1 /verifyopt:/nologo,%2 /verifyopt:/nologo,%3 /verifyopt:/nologo,%4 /verifyopt:/nologo,%5 /verifyopt:/nologo,%6 /verifyopt:/nologo,%7 /verifyopt:/nologo,%8 /verifyopt:/nologo,%9

for %%f in (FoxtrotChecking) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc /verify /verifyopt:/methodology:vs /verifyopt:/level:1 /verifyopt:/modifiesDefault:4 %SSC_ARGS%
)
for %%f in (Chunker) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* /methodology:visible-state %%f.dll
)

echo -------------------- FoxtrotLoops1.ssc --------------------
"%SSCEXE%" /target:library /debug FoxtrotLoops1.ssc
"%BGEXE%" %* /logPrefix:-s FoxtrotLoops1.dll
echo -------------------- FoxtrotLoops1.ssc /loopUnroll:1 --------------------
"%BGEXE%" %* /logPrefix:-lu1 FoxtrotLoops1.dll /loopUnroll:1

echo -------------------- FoxtrotLoops5.ssc --------------------
"%SSCEXE%" /target:library /debug FoxtrotLoops5.ssc
"%BGEXE%" %* /logPrefix:-s FoxtrotLoops5.dll
echo -------------------- FoxtrotLoops5.ssc /loopUnroll:2 --------------------
"%BGEXE%" %* /logPrefix:-lu2 FoxtrotLoops5.dll /loopUnroll:2
echo -------------------- FoxtrotLoops5.ssc /loopUnroll:3 --------------------
"%BGEXE%" %* /logPrefix:-lu3 FoxtrotLoops5.dll /loopUnroll:3

