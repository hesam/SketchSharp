@echo off

echo Expected processing time: 90 s > con

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe

%SSCEXE% /target:library /debug WindowsCard.ssc > Output.trace
%BGEXE% /level:2 /modifiesDefault:5 /z3opt:/rs:42 %* WindowsCard.dll /trace >> Output.trace
REM remove the times that are included in the /trace output
%SystemRoot%\system32\find /v "  [" < Output.trace
