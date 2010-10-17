@echo off
setlocal

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe

set TIME_COMMAND=time /t

echo Writing to Output.trace for /trace filtering...
echo Start > Output.trace

echo. >> Output.trace
echo -------------------- Core.dll -------------------- >> Output.trace
echo Expected processing time for Core: 1 min + 3 min > con
%TIME_COMMAND% > con

"%BGEXE%" /trace %* %BOOGIEDIR%\Core.dll /trFile:Xml.ssc >> Output.trace

%TIME_COMMAND% > con
"%BGEXE%" /trace %* %BOOGIEDIR%\Core.dll /trFile:CommandLineOptions.ssc >> Output.trace

echo. >> Output.trace
echo -------------------- BytecodeTranslation.dll -------------------- >> Output.trace
echo Expected processing time for BytecodeTranslation: 10 s > con
%TIME_COMMAND% > con

"%BGEXE%" /trace %* %BOOGIEDIR%\BytecodeTranslation.dll /trClass:Microsoft.Boogie.FlowedValue.Type /modifiesDefault:1 >> Output.trace

echo. >> Output.trace
echo -------------------- DafnyPipeline.dll -------------------- >> Output.trace
echo Expected processing time for Dafny: 30 s > con
%TIME_COMMAND% > con

"%BGEXE%" /trace %* %BOOGIEDIR%\DafnyPipeline.dll /trFile:DafnyAst.ssc >> Output.trace

echo. >> Output.trace
echo -------------------- VCGeneration.dll -------------------- >> Output.trace
echo Expected processing time for VCGeneration: 20s + 30s + 60s + 15s > con
%TIME_COMMAND% > con

"%BGEXE%" /trace %* %BOOGIEDIR%\VCGeneration.dll /trClass:ProverException /trClass:UnexpectedProverOutputException  >> Output.trace

echo. >> Output.trace
"%BGEXE%" /trace %* %BOOGIEDIR%\Provers.Simplify.dll /trClass:ProverProcess  >> Output.trace

%TIME_COMMAND% > con
echo. >> Output.trace
"%BGEXE%" /trace %* %BOOGIEDIR%\Provers.Simplify.dll /trClass:SimplifyProverProcess  >> Output.trace

%TIME_COMMAND% > con
echo. >> Output.trace
"%BGEXE%" /trace /enhancedErrorMessages:1 /vc:n /fcoStrength:2 %* %BOOGIEDIR%\Provers.Z3.dll /trClass:Z3ProverProcess /trExclude:Z3ProverProcess.ParseModel /trExclude:Z3ProverProcess..ctor >> Output.trace

%TIME_COMMAND% > con
"%BGEXE%" /trace /enhancedErrorMessages:1 /vc:n /fcoStrength:2 %* %BOOGIEDIR%\Provers.Z3.dll /trMethod:Z3ProverProcess.ParseModel /trMethod:Z3ProverProcess.ParseModelZidAndIdentifiers >> Output.trace


REM ----- TODO ----- "%BGEXE%" /trace /enhancedErrorMessages:1 /vc:n /fcoStrength:2 %* %BOOGIEDIR%\Provers.Z3.dll /trMethod:Z3ProverProcess..ctor >> Output.trace
REM ----- TODO ----- "%BGEXE%" /trace /enhancedErrorMessages:1 /vc:n /fcoStrength:2 %* %BOOGIEDIR%\Provers.Z3.dll /trMethod:Z3ProverProcess.ParseModelMapping >> Output.trace
REM ----- TODO ----- "%BGEXE%" /trace /enhancedErrorMessages:1 /vc:n /fcoStrength:2 %* %BOOGIEDIR%\Provers.Z3.dll /trMethod:Z3ProverProcess.ParseModelFunctions /vcsMaxKeepGoingSplits:10 /vcsFinalAssertTimeout:600 /vcsKeepGoingTimeout:10 >> Output.trace


echo End: > con
%TIME_COMMAND% > con

REM remove the times that are included in the /trace output
%SystemRoot%\system32\find /v "  [" < Output.trace | %SystemRoot%\system32\find /v "checking split"
