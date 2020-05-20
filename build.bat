del /s /q Build 
dotnet publish YAFC/YAFC.csproj -r win-x64 --self-contained false -c Release -o Build/Windows
dotnet publish YAFC/YAFC.csproj -r win-x64 -c Release -o Build/WindowsFat
dotnet publish YAFC/YAFC.csproj -r osx-x64 --self-contained false -c Release -o Build/OSX
dotnet publish YAFC/YAFC.csproj -r linux-x64 --self-contained false -c Release -o Build/Linux