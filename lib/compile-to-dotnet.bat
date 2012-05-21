"D:\Tools\ikvm\ikvmbin-7.1.4507\bin\ikvmc" ^
-nostdlib ^
-assembly:.\..\target\de.devsurf.azure.IKVM ^
-lib:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319" ^
-lib:"C:\Program Files\Windows Azure SDK\v1.6\bin\runtimes\base" ^
-r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\mscorlib.dll" ^
-r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll" ^
-r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Data.dll" ^
-r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll" ^
-r:"C:\Program Files\Windows Azure SDK\v1.6\bin\runtimes\base\Microsoft.WindowsAzure.ServiceRuntime.dll" ^
-platform:x64 ^
.\..\target\ikvm-for-azure-0.0.1-SNAPSHOT.jar
pause
