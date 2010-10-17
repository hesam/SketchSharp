@echo off
rem unregister old binaries
regasm /nologo /silent /u Microsoft.VisualStudio.Package.dll
regasm /nologo /silent /u Microsoft.SpecSharp.dll
del LastKnownGoodSpecSharp.dll
del LastKnownGoodSpecSharp.pdb

rem check out old binaries
for %%f in (*.dll) do sd edit %%f
for %%f in (*.pdb) do sd edit %%f
sd edit 1033\Microsoft.SpecSharp.resources.dll
sd edit 1033\PropertyPageUI.dll
sd edit 1033\TaskManagerUI.dll
sd edit ssc.exe
sd edit Boogie.exe
sd edit DafnyPrelude.bpl
sd edit Prelude.bpl
sd edit TypedUnivBackPred.sx
sd edit UnivBackPred.sx
sd edit Z3.exe

rem copy new binaries over old ones
copy ..\Registration\*.dll .
copy ..\Registration\*.pdb .
copy ..\Registration\1033\*.dll 1033\.
copy ..\CommandLineCompiler\bin\ssc.exe .
copy ..\CommandLineCompiler\bin\ssc.pdb .
