all: Microsoft.SpecSharp.Resources.dll

CS=csc.exe
RC=rc.exe
CTC=CTC.EXE

Microsoft.SpecSharp.Resources.dll: AssemblyInfo.cs resources.res 
	$(CS) /target:library /win32res:resources.res /nowarn:1699 /out:Microsoft.SpecSharp.Resources.dll AssemblyInfo.cs

resources.res: resources.rc SpecSharp.ico SpecSharpProject.ico SpecSharp.bmp
    $(RC) resources.rc
