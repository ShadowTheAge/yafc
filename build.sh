rm -rf Build

VERSION=$(grep -oPm1 "(?<=<AssemblyVersion>)[^<]+" Yafc/Yafc.csproj)
echo "Building YAFC version $VERSION..."

dotnet publish Yafc/Yafc.csproj -r win-x64 -c Release -o Build/Windows
dotnet publish Yafc/Yafc.csproj -r osx-x64 --self-contained false -c Release -o Build/OSX
dotnet publish Yafc/Yafc.csproj -r linux-x64 --self-contained false -c Release -o Build/Linux

pushd Build
tar czf Yafc-CE-Linux-$VERSION.tar.gz Linux
tar czf Yafc-CE-OSX-$VERSION.tar.gz OSX
zip -r Yafc-CE-Windows-$VERSION.zip Windows
popd

