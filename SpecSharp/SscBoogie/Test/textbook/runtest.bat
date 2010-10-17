@echo off

echo Expected processing time: 48 min > con

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe
set SSC_ARGS=/verifyopt:/nologo,%1 /verifyopt:/nologo,%2 /verifyopt:/nologo,%3 /verifyopt:/nologo,%4 /verifyopt:/nologo,%5 /verifyopt:/nologo,%6 /verifyopt:/nologo,%7 /verifyopt:/nologo,%8 /verifyopt:/nologo,%9

echo Start > Output.trace

REM ======================
REM ====================== Examples that for Simplify need /arithDistributionAxioms
REM ======================
for %%f in (Square Sqrt) do (
  echo. >> Output.trace
  echo ------------------------------ %%f --------------------- >> Output.trace
  %SSCEXE% /target:library /debug %%f.ssc >> Output.trace
  %BGEXE% /trace %* /arithDistributionAxioms %%f.dll >> Output.trace
)

REM ======================
REM ====================== Examples that need /cc- and /noConsistencyChecks
REM ======================
for %%f in (Fibonacci Factorial_rec) do (
  echo. >> Output.trace
  echo ------------------------------ %%f --------------------- >> Output.trace
  %SSCEXE% /target:library /debug /checkcontracts- %%f.ssc >> Output.trace
  %BGEXE% /trace %* /noConsistencyChecks %%f.dll >> Output.trace
)

REM ======================
REM ====================== Examples that use /infer:p
REM ======================
for %%f in (BinarySearch Cubes) do (
  echo. >> Output.trace
  echo ------------------------------ %%f --------------------- >> Output.trace
  %SSCEXE% /target:library /debug %%f.ssc >> Output.trace
  %BGEXE% /trace %* /infer:p %%f.dll >> Output.trace
)

REM ======================
REM ====================== Examples that just run
REM ======================
for %%f in (DutchNationalFlag Sum Multiply Quadruple Double
            InsertionSort BubbleSort BoundedLinearSearch
            Stack Queue CircularQueue BinarySearchTree
            Factorial) do (
  echo. >> Output.trace
  echo ------------------------------ %%f --------------------- >> Output.trace
  %SSCEXE% /target:library /debug %%f.ssc >> Output.trace
  %BGEXE% /trace %* %%f.dll >> Output.trace
)

REM ======================
REM ====================== More examples that just run.  These use comprehensions.
REM ======================
for %%f in (SegmentSum Sum_x_values SumClass SumEven SumEvenClassPure SumEvenClassPure1
            SumEvenFilters Sum_2DArray
            min_max_xy MaxMinClass CountNonNulls CountQuantifier
            CoincidenceCount CoincidenceCountEfficient2 CoincidenceCountAlterInvariant
            CoincidenceCountEfficient CoincidenceCountEfficient1) do (
  echo. >> Output.trace
  echo ------------------------------ %%f --------------------- >> Output.trace
  %SSCEXE% /target:library /debug %%f.ssc >> Output.trace
  %BGEXE% /trace %* %%f.dll >> Output.trace
)

REM ======================
REM ====================== Examples that need /inductiveMinMax:4
REM ======================
for %%f in (MinimalSegmentSum) do (
  echo. >> Output.trace
  echo ------------------------------ %%f --------------------- >> Output.trace
  %SSCEXE% /target:library /debug %%f.ssc >> Output.trace
  %BGEXE% /trace %* /inductiveMinMax:4 %%f.dll >> Output.trace
)

REM remove the times that are included in the /trace output
%SystemRoot%\system32\find /v "  [" < Output.trace
