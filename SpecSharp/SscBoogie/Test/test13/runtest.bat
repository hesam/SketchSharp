@echo off
setlocal

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe
set BPLEXE=%BOOGIEDIR%\Boogie.exe

for %%f in (ExistsUnique IntArrayRanges LoopInvariantForQuantifiers ArgList
            Summations) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll
)
for %%f in (Product) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll
)
for %%f in (ImplicitInterfaceImplementation ErrorTraceTestLoopInvViolation
            ErrorTraceTestDefaultLoopInvViolation DelayedPreconditions ArrayCopy) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll
)
for %%f in (PreviousErrorRelated OwnershipTest) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll
)
for %%f in (Test1 GenericOwnership) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* /errorLimit:1 /level:1 /modifiesDefault:4 %%f.dll
)
for %%f in (Param TwoTypes Enums PeerElementsAndCapture) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll
)
for %%f in (UseAssertInitialized) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug /d:DEBUG %%f.ssc
  "%BGEXE%" %* %%f.dll
)
