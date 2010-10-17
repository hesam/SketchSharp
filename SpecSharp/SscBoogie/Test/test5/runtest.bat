@echo off
setlocal

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe
set SSC_ARGS=/verifyopt:/nologo,%1 /verifyopt:/nologo,%2 /verifyopt:/nologo,%3 /verifyopt:/nologo,%4 /verifyopt:/nologo,%5 /verifyopt:/nologo,%6 /verifyopt:/nologo,%7 /verifyopt:/nologo,%8 /verifyopt:/nologo,%9

FOR %%f in (InitialValuesGood.ssc InitialValuesBad.ssc
            WhileGood.ssc WhileBad.ssc DoWhileGood.ssc
            DoWhileBad.ssc Ensures.ssc PureCall.ssc
            StaticFields.ssc Cast.ssc BoxedInt.ssc
            DefinedExpressions.ssc Finally.ssc) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug /verify %SSC_ARGS% %%f
)

echo.
echo -------------------- Search.ssc --------------------
"%SSCEXE%" /target:library /debug Search.ssc
"%BGEXE%" %* /noinfer Search.dll
echo -------------------- Search.ssc with inference --------------------
rem "%BGEXE%" %* -infer:p Search.dll

echo.
echo -------------------- WhereMotivation.ssc with modifiesOnLoop:0 --------------------
"%SSCEXE%" /target:library /debug WhereMotivation.ssc
"%BGEXE%" %* WhereMotivation.dll /level:1 /modifiesDefault:4 /modifiesOnLoop:0 /logPrefix:-mol0
echo -------------------- WhereMotivation.ssc with modifiesOnLoop:2 --------------------
"%BGEXE%" %* WhereMotivation.dll /level:1 /modifiesDefault:4 /modifiesOnLoop:2 /logPrefix:-mol2

echo.
echo -------------------- AndNumbers.ssc --------------------
"%SSCEXE%" /target:library /debug AndNumbers.ssc
"%BGEXE%" %* AndNumbers.dll

echo.
echo -------------------- Unsigned.ssc --------------------
"%SSCEXE%" /target:library /debug Unsigned.ssc
"%BGEXE%" %* Unsigned.dll

echo.
echo -------------------- ModifiesClauses.ssc --------------------
rem The following test needs to go through "ssc /verify" because of "modifies (this).*;"
"%SSCEXE%" /target:library /debug /verify  %SSC_ARGS% /verifyopt:/level:2 /verifyopt:/modifiesDefault:4 ModifiesClauses.ssc

echo.
echo -------------------- PeerModifiesClauses.ssc --------------------
"%SSCEXE%" /target:library /debug PeerModifiesClauses.ssc
"%BGEXE%" %* /level:2 PeerModifiesClauses.dll

FOR %%f in (
            TypeReconstruction
           ) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll
)
