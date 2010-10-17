cscript /e:jscript /nologo CleanReg.js

rem ..\..\RegPkg\bin\Debug\regpkg.exe /root:Software\Microsoft\VisualStudio\8.0Exp /unregister Microsoft.SpecSharp.dll
if exist *.dll del /Q *.dll
if exist *.pdb del /Q *.pdb
if exist *.exe del /Q *.exe
