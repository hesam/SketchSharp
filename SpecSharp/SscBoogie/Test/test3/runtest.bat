@echo off
setlocal

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set BGEXE=%BOOGIEDIR%\SscBoogie.exe
set SSC_ARGS=/verifyopt:/nologo,%1 /verifyopt:/nologo,%2 /verifyopt:/nologo,%3 /verifyopt:/nologo,%4 /verifyopt:/nologo,%5 /verifyopt:/nologo,%6 /verifyopt:/nologo,%7 /verifyopt:/nologo,%8 /verifyopt:/nologo,%9

echo ------------------------------ AddMethod.ssc ---------------------
%SSCEXE% /target:library /debug AddMethod.ssc
%BGEXE% %* AddMethod.dll

echo ------------------------------ Array.ssc ---------------------
%SSCEXE% /target:library /debug Array.ssc
%BGEXE% %* Array.dll

echo ------------------------------ Array2.ssc ---------------------
%SSCEXE% /target:library /debug Array2.ssc
%BGEXE% %* Array2.dll

echo ------------------------------ AssertFalse.ssc ---------------------
%SSCEXE% /target:library /debug AssertFalse.ssc
%BGEXE% %* AssertFalse.dll

echo ------------------------------ Strings.ssc ---------------------
%SSCEXE% /target:library /debug Strings.ssc
%BGEXE% %* Strings.dll

echo ------------------------------ Immutable.ssc ---------------------
%SSCEXE% /target:library /debug Immutable.ssc
%BGEXE% %* Immutable.dll

echo ------------------------------ B.ssc BClient.ssc ---------------------
%SSCEXE% /target:library /debug B.ssc
%SSCEXE% /debug /reference:B.dll BClient.ssc
%BGEXE% %* /simplifyMatchDepth:4 BClient.exe

echo ------------------------------ Call.ssc ---------------------
%SSCEXE% /debug Call.ssc
%BGEXE% %* Call.exe

echo ------------------------------ Global.ssc ---------------------
%SSCEXE% /target:library /debug Global.ssc
%BGEXE% %* Global.dll

echo ------------------------------ Types.ssc ---------------------
%SSCEXE% /target:library /debug Types.ssc
%BGEXE% %* /orderStrength:1 Types.dll

echo ------------------------------ DynamicTypes.ssc ---------------------
%SSCEXE% /target:library /debug DynamicTypes.ssc
%BGEXE% %* DynamicTypes.dll

echo ------------------------------ AdvancedTypes.ssc ---------------------
%SSCEXE% /target:library /debug AdvancedTypes.ssc
%BGEXE% %* /orderStrength:1 AdvancedTypes.dll

echo ------------------------------ ExactTypes.ssc ---------------------
%SSCEXE% /target:library /debug ExactTypes.ssc
%BGEXE% %* ExactTypes.dll

echo ------------------------------ ConstructorVisibility.ssc ---------------------
%SSCEXE% /target:library /debug ConstructorVisibility.ssc
%BGEXE% %* ConstructorVisibility.dll

echo ------------------------------ Alloc.ssc ---------------------
%SSCEXE% /target:library /debug Alloc.ssc
%BGEXE% %* Alloc.dll

echo ------------------------------ Strengthen.ssc ---------------------
%SSCEXE% /debug Strengthen.ssc /verify %SSC_ARGS% /verifyOpt:/simplifyMatchDepth:4

echo ------------------------------ Generic.ssc ---------------------
%SSCEXE% Generic.ssc /verify %SSC_ARGS%

echo ------------------------------ ParamInAssert.ssc ---------------------
%SSCEXE% /t:library /debug /verify %SSC_ARGS% ParamInAssert.ssc

echo ------------------------------ Typeof.ssc ---------------------
%SSCEXE% /t:library /debug /verify %SSC_ARGS% Typeof.ssc

echo ------------------------------ Subclass.ssc ---------------------
%SSCEXE% /t:library /debug /verify %SSC_ARGS% Subclass.ssc

echo ------------------------------ Iff.ssc ---------------------
%SSCEXE% /t:library /debug Iff.ssc
%BGEXE% %* Iff.dll

echo ------------------------------ Operators.ssc ---------------------
%SSCEXE% /t:library /debug Operators.ssc
%BGEXE% %* Operators.dll

echo ------------------------------ EnsuresFalse.ssc ---------------------
%SSCEXE% /t:library /debug EnsuresFalse.ssc
%BGEXE% %* EnsuresFalse.dll

echo ------------------------------ Main.ssc ---------------------
%SSCEXE% /debug Main.ssc
%BGEXE% %* Main.exe
