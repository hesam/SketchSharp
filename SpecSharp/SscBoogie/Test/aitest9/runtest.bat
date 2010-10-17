@echo off
setlocal

set BOOGIEDIR=..\..
set SSCEXE=%BOOGIEDIR%\Binaries\ssc.exe
set BOOGIEXE=%BOOGIEDIR%\Binaries\SscBoogie.exe
set BPLEXE=%BOOGIEDIR%\Binaries\Boogie.exe


for %%f in (SimpleAssignments0.ssc SimpleAssignments1.ssc SimpleAssignments2.ssc 
		SimpleAssignments3.ssc 
) do (
  %SSCEXE%   %%f  /debug /target:library 
)

for %%f in (SimpleAssignments0.dll SimpleAssignments1.dll SimpleAssignments2.dll 
		SimpleAssignments3.dll 
) do (
  echo. 
  echo -------------------- %%f -------------------- 
  %BOOGIEXE% %*  %%f  /infer:i 
)

for %%f in (SimpleAssignments4.ssc
) do (
  %SSCEXE%   %%f  /debug /target:library 
)

for %%f in (SimpleAssignments4.dll
) do (
  echo. 
  echo -------------------- %%f -------------------- 
  %BOOGIEXE% %*  %%f  /infer:i /vc:local /level:0
)

for %%f in (SimpleWhile0.ssc SimpleWhile1.ssc SimpleWhile2.ssc SimpleWhile3.ssc 
		SimpleWhile4.ssc SimpleWhile5.ssc 
)do (
  %SSCEXE%  %%f  /debug /target:library
)

for %%f in (SimpleWhile0.dll SimpleWhile1.dll SimpleWhile2.dll SimpleWhile3.dll 
		SimpleWhile4.dll SimpleWhile5.dll 
)do (
  echo. 
  echo -------------------- %%f -------------------- 
  %BOOGIEXE% %* %%f  /infer:i
)


for %%f in (Recursion0.ssc 
)do (
  %SSCEXE%  %%f  /debug /target:library
)


for %%f in (Recursion0.dll
) do (
  echo. 
  echo -------------------- %%f -------------------- 
  %BOOGIEXE% %*  %%f  /infer:i 
)


for %%f in (ForLoop0.ssc
)do (
  %SSCEXE%  %%f  /debug /target:library
)


for %%f in (ForLoop0.dll
) do (
  echo. 
  echo -------------------- %%f -------------------- 
  %BOOGIEXE% %*  %%f  /infer:i
)
