# Things to do before each release:
# * Update Yafc version in Yafc.csproj
# * Update changelog with the version and date
# * Update the latest version in the Discord sticky-message.

rm -rf Build

VERSION=$(grep -oPm1 "(?<=<AssemblyVersion>)[^<]+" Yafc/Yafc.csproj)
echo "Building YAFC version $VERSION..."

dotnet publish Yafc/Yafc.csproj -r win-x64 -c Release -o Build/Windows
dotnet publish Yafc/Yafc.csproj -r osx-x64 --self-contained false -c Release -o Build/OSX
dotnet publish Yafc/Yafc.csproj -r osx-arm64 --self-contained false -c Release -o Build/OSX-arm64
dotnet publish Yafc/Yafc.csproj -r linux-x64 --self-contained false -c Release -o Build/Linux

echo "The libraries of this release were scanned on Virustotal, but we could not reproduce the checksums." > Build/OSX-arm64/_WARNING.TXT
echo "If you want to help with the checksums, please navigate to https://github.com/shpaass/yafc-ce/issues/274" >> Build/OSX-arm64/_WARNING.TXT

pushd Build
tar czf Yafc-CE-Linux-$VERSION.tar.gz Linux
tar czf Yafc-CE-OSX-intel-$VERSION.tar.gz OSX
tar czf Yafc-CE-OSX-arm64-$VERSION.tar.gz OSX-arm64
zip -r Yafc-CE-Windows-$VERSION.zip Windows
popd

