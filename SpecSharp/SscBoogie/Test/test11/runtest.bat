@echo off
setlocal

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe

for %%f in (Boxes MustOverride WhereClause Switch StrictReadOnly) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll
)

for %%f in (LocalExpose) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* /level:1 /modifiesDefault:4 %%f.dll
)

for %%f in (SiblingConstructors) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll
)

for %%f in (AdditiveMethods) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* /level:1 /modifiesDefault:4 %%f.dll
)

for %%f in (PureReceiverMightBeCommitted
            ResultNotNewlyAllocated Branching QuantifierVisibilityInvariant
            DeferringConstructor ArrayInit CommittedOblivious ModelfieldTest CheckingConsistency) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* %%f.dll
)

for %%f in (Modifies PureAxioms ExposeVersion ExplicitExposeVersionHavoc StructTests) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug %%f.ssc
  "%BGEXE%" %* /level:2 %%f.dll
)

echo.
echo -------------------- Modifies2 --------------------

"%SSCEXE%" /target:library /debug Modifies2.ssc
"%BGEXE%" %* /level:2 /logPrefix:0 /localModifiesChecks:0 Modifies2.dll

echo.
echo -------------------- Modifies2 --------------------

"%BGEXE%" %* /level:2 /logPrefix:1 /localModifiesChecks:1 Modifies2.dll
