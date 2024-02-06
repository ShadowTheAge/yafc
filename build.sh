rm -rf Build
dotnet publish YAFC/YAFC.csproj -r win-x64 -c Release -o Build/Windows -p:PublishTrimmed=true
dotnet publish YAFC/YAFC.csproj -r osx-x64 --self-contained false -c Release -o Build/OSX
dotnet publish YAFC/YAFC.csproj -r linux-x64 --self-contained false -c Release -o Build/Linux

pushd Build
tar czf Linux.tar.gz Linux
tar czf OSX.tar.gz OSX
zip -r Windows.zip Windows
popd

