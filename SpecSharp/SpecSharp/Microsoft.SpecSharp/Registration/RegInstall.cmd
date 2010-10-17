@echo off
set FROM=%1
set TO=%2
set PDB=%FROM:~0,-4%.pdb
set ASSEMBLY=%TO:~0,-4%
if exist %2 regasm /nologo /silent /u %2  
copy /y %1 %1.temp > nul
if ERRORLEVEL 1 (
  echo ERROR copying %1 to %1.temp
  exit 1
)
copy /y %1.temp %2 > nul
if ERRORLEVEL 1 (
  echo ERROR copying %1.temp to %2
  exit 1
)
if exist %PDB% copy /y %PDB% > nul
regasm /nologo /codebase %2
if ERRORLEVEL 1 echo ERROR installing %ASSEMBLY% into the Registry
