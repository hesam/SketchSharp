SSC = ..\Microsoft.SpecSharp\commandlinecompiler\bin\ssc.exe
MSCORLIB7 = c:\Windows\Microsoft.Net\Framework\v1.1.4322\mscorlib.dll
SYSTEM7 = c:\Windows\Microsoft.Net\Framework\v1.1.4322\System.dll

BINDIR = bin\Debug7

scr7: $(BINDIR)
	$(SSC) /noconfig /platform:v11 /nostdlib /r:$(MSCORLIB7) /r:$(SYSTEM7) /debug /d:DEBUG /d:NoData /t:library /out:$(BINDIR)\System.Compiler.Runtime.dll Classes.cs AssemblyInfo.cs ..\Common\Include\version.cs

$(BINDIR):
	mkdir $(BINDIR)

