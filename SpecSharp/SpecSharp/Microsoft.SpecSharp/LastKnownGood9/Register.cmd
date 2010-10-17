@echo off
setlocal

REM See http://blog.borngeek.com/2008/05/22/batch-file-exit-codes/
REM for the peculiarities of batch scripts and status codes / error levels.

set ERRLVL=0

if "%1"=="Clean" (
	set OPTS=/nologo /silent /u
) else (
	if "%1"=="RegisterLKG" (
		set OPTS=/nologo /codebase
	) else (
		echo "Register.cmd <Clean|Register> [path to regasm]"
		set ERRLVL=1
		goto :quit
	)
)

set REGASM=%2
if "%2"=="" set REGASM=regasm

call :register Microsoft.VisualStudio.Package.dll
call :register Microsoft.SpecSharp.dll
call :register ContractsPropertyPane.dll
goto :quit

:register
	%REGASM% %OPTS% %1
	if %ERRORLEVEL% NEQ 0 set ERRLVL=%ERRORLEVEL%

	
:quit
	exit /b %ERRLVL%