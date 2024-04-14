rm -rf Build
dotnet publish Yafc/Yafc.csproj -r win-x64 -c Release -o Build/Windows -p:PublishTrimmed=true
dotnet publish Yafc/Yafc.csproj -r osx-x64 --self-contained false -c Release -o Build/OSX
dotnet publish Yafc/Yafc.csproj -r linux-x64 --self-contained false -c Release -o Build/Linux

pushd Build
tar czf Linux.tar.gz Linux
tar czf OSX.tar.gz OSX
zip -r Windows.zip Windows
popd

