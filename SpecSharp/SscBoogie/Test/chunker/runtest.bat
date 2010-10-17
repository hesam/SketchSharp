@echo off
setlocal

set BOOGIEDIR=..\..\Binaries
set SSCEXE=%BOOGIEDIR%\ssc.exe
set SSC_ARGS=/verifyopt:/nologo,%1 /verifyopt:/nologo,%2 /verifyopt:/nologo,%3 /verifyopt:/nologo,%4 /verifyopt:/nologo,%5 /verifyopt:/nologo,%6 /verifyopt:/nologo,%7 /verifyopt:/nologo,%8 /verifyopt:/nologo,%9

for %%f in (Chunker0.ssc Chunker1.ssc Chunker2.ssc Chunker3.ssc
            Chunker4.ssc Chunker5.ssc Chunker6.ssc Chunker7.ssc
            Chunker8.ssc Chunker9.ssc Chunker10.ssc Chunker11.ssc
            ChunkerAssume.ssc Chunker10-old.ssc
            Chunker11-AdditiveExpose.ssc Chunker11a.ssc Chunker11b.ssc Chunker11c.ssc
            Chunker.ssc) do (
  echo.
  echo -------------------- %%f --------------------
  "%SSCEXE%" /target:library /debug /verify %SSC_ARGS% %%f
)
