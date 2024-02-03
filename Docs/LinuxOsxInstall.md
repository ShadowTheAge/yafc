# Both OSX and Linux builds are experimental!

## OSX installation instructions
- [Install dotnet core (v6.0 or later)](https://dotnet.microsoft.com/download)
- [Install brew](https://brew.sh/)
- Install SDL2 using brew (type the following in the terminal):
    - `brew install SDL2`
	- `brew install SDL2_image`
	- `brew install SDL2_ttf`
	- For reference, have following libraries: libSDL2.dylib, libSDL2_ttf.dylib, libSDL2_image.dylib
- To run either use `dotnet YAFC.dll` in the terminal, or run `YAFC` as an executable
- Make sure you have OpenGL availible

## Linux installation instructions

### Arch 
There is an AUR package for yafc-ce: [`factorio-yafc-ce-git`](https://aur.archlinux.org/packages/factorio-yafc-ce-git) 
Once the package is installed, it can be run with `factorio-yafc`. Note that at least dotnet 6 or later is required.

### Debian (and Debian-based distributions)
- Download the latest release from this repo.
- [Install dotnet core (v6.0 or later)](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian)
- Install SDL2:
  - `sudo apt-get install libsdl2-2.0-0`
  - `sudo apt-get install libsdl2-image-2.0-0`
  - `sudo apt-get install libsdl2-ttf-2.0-0`
  - For reference, have following libraries: SDL2-2.0.so.0, SDL2_ttf-2.0.so.0, SDL2_image-2.0.so.0
- Make sure you have OpenGL available
- Make `YAFC` executable with `chmod +x YAFC`
- Use the `YAFC` executable to run.

### Other
In general, ensure you have SDL2, OpenGL and dotnet 6 or later. Make `YAFC` executable with `chmod +x YAFC` and run it.

### Flathub
Note that [the version available on Flathub](https://flathub.org/apps/details/com.github.petebuffon.yafc) is not the Community Edition. Its repo can be found at https://github.com/petebuffon/yafc. 
