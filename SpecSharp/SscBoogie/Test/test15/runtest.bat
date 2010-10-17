@echo off
setlocal

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe
set Z3EXE=%BOOGIEDIR%\z3.exe

for %%f in (BagAdding) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll /enhancedErrorMessages:1
)
for %%f in (BinarySearch) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll /infer:p /enhancedErrorMessages:1
)
for %%f in (BoundedLinearSearch CapturedParameter ChunkerInv DivisionByZero Factorial0 Factorial1) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll /errorLimit:1 /enhancedErrorMessages:1
)
for %%f in (OldInQuantifier) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll /level:1 /errorLimit:1 /enhancedErrorMessages:1
)
for %%f in (HiddenConstructor) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll /level:2 /enhancedErrorMessages:1
)
for %%f in (MissingPeerDec Summation SummationToN SumZeroToN0 SumZeroToN1 SumZeroToN2) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll /errorLimit:1 /enhancedErrorMessages:1
)
for %%f in (ModifiesMoreThanAllowed ModifiesOrderAssignmentMethod ModifiesSubsetMethodCall ModifiesSubsetMethodCall1 Rectangle) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll /level:2 /enhancedErrorMessages:1
)
for %%f in (SumZeroToN1) do (
  echo.
  echo -------------------- %%f --------------------
  "%BGEXE%" %* %%f.dll /printModel:1 /proverLog:%%f.sx > %%f.tmp
  "%Z3EXE%" PARTIAL_MODEL=true MODEL_VALUE_COMPLETION=false HIDE_UNUSED_PARTITIONS=false /m /si /mam:0 < %%f.sx | %SystemRoot%\System32\find /v "labels:" > %%f.tmp2
  fc %%f.tmp %%f.tmp2 
)
for %%f in (ArrayIndexOutOfBounds) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll /level:1 /errorLimit:1 /enhancedErrorMessages:1
)
for %%f in (ObjectInv) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll /enhancedErrorMessages:1
)
