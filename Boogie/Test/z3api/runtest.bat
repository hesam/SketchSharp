@echo off
setlocal

set BGEXE=..\..\Binaries\Boogie.exe

for %%f in (boog0.bpl boog1.bpl boog2.bpl boog3.bpl boog4.bpl boog5.bpl boog6.bpl boog7.bpl boog8.bpl boog9.bpl boog10.bpl boog11.bpl boog12.bpl boog13.bpl boog14.bpl boog15.bpl boog16.bpl boog17.bpl boog18.bpl boog19.bpl boog20.bpl boog21.bpl boog22.bpl boog23.bpl boog24.bpl boog25.bpl boog28.bpl boog29.bpl boog30.bpl boog31.bpl boog34.bpl) do (
  echo.
  echo -------------------- %%f --------------------
  %BGEXE% %* /nologo /prover:z3api %%f
)
