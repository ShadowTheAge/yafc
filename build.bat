dotnet publish -r win-x64 --self-contained false -o Build/Windows
dotnet publish -r osx-x64 --self-contained false -o Build/OSX
dotnet publish -r linux-x64 --self-contained false -o Build/Linux