del /s /q Build 
dotnet publish Yafc/Yafc.csproj -r win-x64 -c Release -o Build/Windows
dotnet publish Yafc/Yafc.csproj -r osx-x64 --self-contained false -c Release -o Build/OSX
dotnet publish Yafc/Yafc.csproj -r linux-x64 --self-contained false -c Release -o Build/Linux

cd Build
%SystemRoot%\System32\tar.exe -czf Linux.tar.gz Linux
%SystemRoot%\System32\tar.exe -czf OSX.tar.gz OSX
powershell Compress-Archive Windows Windows.zip

pause;

