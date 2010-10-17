@echo off
rem Usage: runtest.bat <dir>
if "%1" == "" goto noDirSpecified
if not exist %1\nul goto noDirExists
echo ----- Running regression test %1
pushd %1
if not exist runtest.bat goto noRunTest
call runtest.bat -nologo -logPrefix:%1 %2 %3 %4 %5 %6 %7 %8 %9 > Output
fc /W Answer Output > nul
if not errorlevel 1 goto passTest
echo %1 FAILED
goto errorEnd

:passTest
echo %1 Succeeded
goto end

:noDirSpecified
echo runtest: Error: Syntax: runtest testDirectory [ additionalTestArguments ... ]
goto errorEnd

:noDirExists
echo runtest: Error: There is no test directory %1
goto errorEnd

:noRunTest
echo runtest: Error: no runtest.bat found in test directory %1
goto errorEnd

:errorEnd
popd
exit /b 1

:end
popd
exit /b 0
