all: Microsoft.VisualStudio.OLE.Interop.dll \
  Microsoft.VisualStudio.Designer.Interfaces.dll \
  Microsoft.VisualStudio.Shell.Interop.dll  \
  Microsoft.VisualStudio.TextManager.Interop.dll \
  Microsoft.VisualStudio.Package.dll \
  Microsoft.VisualStudio.IntegrationHelper.dll \
  System.Compiler.dll \
  System.Compiler.Runtime.dll \
  System.Compiler.Framework.dll \
  System.Compiler.Sql.dll \
  System.Compiler.Sql.Runtime.dll \
  Microsoft.SpecSharp.Runtime.dll \
  Microsoft.SpecSharp.dll \
  Mscorlib.Contracts.dll \
  System.Contracts.dll \
  System.Compiler.Contracts.dll \
  1033\Microsoft.SpecSharp.resources.dll \
  ssc.exe \

Microsoft.VisualStudio.OLE.Interop.dll: ..\..\Common\bin\$@  
  CopyBits ..\..\Common\bin\$@ $@

Microsoft.VisualStudio.Designer.Interfaces.dll: ..\..\Common\bin\$@  
  CopyBits ..\..\Common\bin\$@ $@

Microsoft.VisualStudio.Shell.Interop.dll: ..\..\Common\bin\$@
  CopyBits ..\..\Common\bin\$@ $@

Microsoft.VisualStudio.TextManager.Interop.dll: ..\..\Common\bin\$@
  CopyBits ..\..\Common\bin\$@ $@
  
Microsoft.VisualStudio.Package.dll: ..\..\Microsoft.VisualStudio.Package\bin\$(BUILD)\$@
  Register ..\..\Microsoft.VisualStudio.Package\bin\$(BUILD)\$@ $@
  
Microsoft.VisualStudio.IntegrationHelper.dll: ..\..\Microsoft.VisualStudio.IntegrationHelper\bin\$(BUILD)\$@
  CopyBits ..\..\Microsoft.VisualStudio.IntegrationHelper\bin\$(BUILD)\$@ $@
  
System.Compiler.dll : ..\..\System.Compiler\bin\$@
  CopyBits ..\..\System.Compiler\bin\$@ $@

System.Compiler.Runtime.dll : ..\..\System.Compiler.Runtime\bin\$@  
  CopyBits ..\..\System.Compiler.Runtime\bin\$@ $@

System.Compiler.Framework.dll : ..\..\System.Compiler.Framework\bin\$@
  CopyBits ..\..\System.Compiler.Framework\bin\$@ $@

System.Compiler.Sql.dll : ..\..\System.Compiler.Sql\bin\$(BUILD)\$@
  CopyBits ..\..\System.Compiler.Sql\bin\$(BUILD)\$@ $@
  
System.Compiler.Sql.Runtime.dll : ..\..\System.Compiler.Sql.Runtime\bin\$(BUILD)\$@
  CopyBits ..\..\System.Compiler.Sql.Runtime\bin\$(BUILD)\$@ $@
  
Microsoft.SpecSharp.Runtime.dll : ..\Runtime\Bin\$@
  CopyBits ..\Runtime\Bin\$@ $@

Microsoft.SpecSharp.dll : ..\bin\$(BUILD)\$@
  RegInstall ..\bin\$(BUILD)\$@ $@

Mscorlib.Contracts.dll : ..\..\Contracts\Mscorlib\bin\$@
  CopyBits ..\..\Contracts\Mscorlib\bin\$@ $@

System.Contracts.dll : ..\..\Contracts\System\bin\$@
  CopyBits ..\..\Contracts\System\bin\$@ $@

System.Compiler.Contracts.dll : ..\..\Contracts\System.Compiler\bin\$@
  CopyBits ..\..\Contracts\System.Compiler\bin\$@ $@

1033\Microsoft.SpecSharp.resources.dll: ..\Resources\Microsoft.SpecSharp.resources.dll
  if not exist 1033 mkdir 1033
  copy ..\Resources\Microsoft.SpecSharp.resources.dll $@
  devenv /setup

ssc.exe: ..\CommandLineCompiler\bin\ssc.exe
  CopyBits ..\CommandLineCompiler\bin\$@ $@
