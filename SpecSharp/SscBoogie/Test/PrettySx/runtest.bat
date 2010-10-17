@echo off

echo Expected processing time: 30 s > con

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe

echo Start > Output.trace

%SSCEXE% /target:library /debug PrettySx.ssc >> Output.trace
%BGEXE% %* /trace PrettySx.dll >> Output.trace

REM remove the times that are included in the /trace output
%SystemRoot%\system32\find /v "  [" < Output.trace
