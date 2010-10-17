@echo off

REM
echo @todo mschwerhoff This file is probably redundant since RegisterLKG.cmd
echo		accepts the path of RegAsm.exe as a parameter.
REM

set REGASM=c:\Windows\Microsoft.NET\Framework\v2.0.50727\RegAsm.exe
%REGASM% /nologo /codebase Microsoft.VisualStudio.Package.dll
%REGASM% /nologo /codebase Microsoft.SpecSharp.dll
%REGASM% /nologo /codebase ContractsPropertyPane.dll
rem devenv /setup
