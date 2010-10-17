@echo off
setlocal

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set SSC_ARGS=/verifyopt:/nologo,%1 /verifyopt:/nologo,%2 /verifyopt:/nologo,%3 /verifyopt:/nologo,%4 /verifyopt:/nologo,%5 /verifyopt:/nologo,%6 /verifyopt:/nologo,%7 /verifyopt:/nologo,%8 /verifyopt:/nologo,%9

for %%f in (Bag0.ssc Bag1.ssc Bag2.ssc 
            Bag3.ssc Bag4.ssc Bag5.ssc
                     Bag7.ssc Bag8.ssc
            Bag9.ssc) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug /verify %SSC_ARGS% %%f
)
