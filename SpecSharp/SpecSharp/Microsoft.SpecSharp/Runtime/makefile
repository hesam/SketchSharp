all: Microsoft.SpecSharp.Runtime.dll

Microsoft.SpecSharp.Runtime.dll: AssemblyInfo.ssc Classes.ssc Collections.ssc
	..\LastKnownGood8\ssc.exe /p:v2 /debug /t:library /nostdlib /r:mscorlib.dll /r:..\..\System.Compiler.Runtime\bin\System.Compiler.Runtime.dll /out:bin\Microsoft.SpecSharp.Runtime.dll /keyfile:..\..\Common\InterimKey.snk ..\..\Common\Include\Version.cs Classes.ssc Collections.ssc
	copy bin\Microsoft.SpecSharp.Runtime.dll "..\..\Microsoft.XamlCompiler"
	copy bin\Microsoft.SpecSharp.Runtime.dll "..\Checkin Tests"
  copy ..\..\System.Compiler.Runtime\bin\System.Compiler.Runtime.dll "..\Checkin Tests"

clean:
	del Microsoft.SpecSharp.Runtime.dll
	del "..\..\Microsoft.XamlCompiler\Microsoft.SpecSharp.Runtime.dll
	del "..\Checkin Tests"\Microsoft.SpecSharp.Runtime.dll
	del "..\Checkin Tests"\System.Compiler.Runtime.dll

cross:
	..\Commandlinecompiler\bin\ssc.exe /break /noconfig /p:v11 /debug /t:library /nostdlib /r:mscorlib.dll /r:..\..\System.Compiler.Runtime\bin\Debug\System.Compiler.Runtime.dll /out:bin\Microsoft.SpecSharp.Runtime.dll /keyfile:..\..\Common\InterimKey.snk ..\..\Common\Include\Version.cs Classes.ssc Collections.ssc

