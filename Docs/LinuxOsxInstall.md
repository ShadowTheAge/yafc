## Linux installation instructions

### Arch 
There is an AUR package for yafc-ce: [`factorio-yafc-ce-git`](https://aur.archlinux.org/packages/factorio-yafc-ce-git) 
Once the package is installed, it can be run with `factorio-yafc`. Note that at least dotnet 6 or later is required.

### Debian (and Debian-based distributions)
- Download the latest release from this repo.
- [Install dotnet core (v8.0 or later)](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian)
- Install SDL2:
  - `sudo apt-get install libsdl2-2.0-0`
  - `sudo apt-get install libsdl2-image-2.0-0`
  - `sudo apt-get install libsdl2-ttf-2.0-0`
  - For reference, have following libraries: SDL2-2.0.so.0, SDL2_ttf-2.0.so.0, SDL2_image-2.0.so.0
- Make sure you have OpenGL available
- Use the `Yafc` executable to run.

### Other
In general, ensure you have SDL2, OpenGL and dotnet 6 or later. Use the `Yafc` executable to run.

### Flathub
Note that [the version available on Flathub](https://flathub.org/apps/details/com.github.petebuffon.yafc) is not the Community Edition. Its repo can be found at https://github.com/petebuffon/yafc. 

## OSX installation instructions
Below are the instructions for the arm64 Macs. To use them for Intel Macs, please apply the changes to the folder `Yafc/lib/osx` instead of `Yafc/lib/osx-arm64`.
- [Install dotnet core (v8.0 or later)](https://dotnet.microsoft.com/download)
- Download and extract the [lua 5.2.1 source code](https://www.lua.org/ftp/lua-5.2.1.tar.gz)
- Apply the [.patch](https://github.com/shpaass/yafc-ce/blob/master/lua-5.2.1.patch) file to the extracted lua source code.
- Modify the `src/Makefile` to include the following two lines. Note that the second line must start with a tab:
```
liblua.dylib: $(CORE_O) $(LIB_O)
        $(CC) -dynamiclib -o $@ $^ $(LIBS)
```
- Run `make macosx` in the root directory
- Run `make -C src liblua.dylib`. This will create `src/liblia.a` and `src/liblua.dylib`.
- Overwrite `liblua52.a` and `liblua52.dylib` in `Yafc/lib/osx-arm64` with the two created files:
```
cp src/liblua.a <yafc repo>/Yafc/lib.osx-arm64/liblua52.a
cp src/liblua.dylib <yafc repo>/Yafc/lib.osx-arm64/liblua52.dylib
```
- To get the SDL libraries, [install brew](https://brew.sh/) and then install the packages:
```
brew install SDL2
brew install SDL2_image
brew install SDL2_ttf
```
- Copy the following files from `(brew --prefix)/lib`, which is usually `/opt/homebrew/lib/`, to `Yafc/lib/osx-arm64`:
```
libSDL2.dylib
libSDL2_image.dylib
libSDL2_tff.dylib
```
- Run `build.sh` to build Yafc. If you get an error `grep: invalid option -- P`, then you need to either remove the mentions of `VERSION` from `build.sh` so `grep` is not used, or you need to install GNU grep with `brew install grep` and change `grep` in `build.sh` to `ggrep`.
- The folder `Build/OSX-arm64` will contain all of the arm64 files and should run in place. It will also create an archive `OSX-arm64.tar.gz` for the distro.
- Make sure you have OpenGL available.
- To run the app, either use `dotnet Yafc.dll` in the terminal, or run `Yafc` as an executable.
