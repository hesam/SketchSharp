@echo off

echo Expected processing time: 6 s > con
set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe

REM added next line
set SSC_ARGS=/verifyopt:/nologo,%1 /verifyopt:/nologo,%2 /verifyopt:/nologo,%3 /verifyopt:/nologo,%4 /verifyopt:/nologo,%5 /verifyopt:/nologo,%6 /verifyopt:/nologo,%7 /verifyopt:/nologo,%8 /verifyopt:/nologo,%9

echo Start > Output.trace

echo ------------------------------ BoundedStack.ssc --------------------- >> Output.trace
%SSCEXE% /target:library /debug BoundedStack.ssc  >> Output.trace
%BGEXE% %* BoundedStack.dll /trace >> Output.trace


REM remove the times that are included in the /trace output
%SystemRoot%\system32\find /v "  [" < Output.trace
