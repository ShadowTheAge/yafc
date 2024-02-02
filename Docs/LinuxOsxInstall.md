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
The AUR package `factorio-yafc-git` points to the original upstream project by ShadowTheAge. Clone the AUR package and edit the url to the following: https://github.com/have-fun-was-taken/yafc-ce
In the same folder, run `makepkg -sri` to build YAFC. Run with `factorio-yafc`. 
### Debian
- Download the latest release from this repo.
- [Install dotnet core (v6.0 or later)](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian)
- Install SDL2:
  - `sudo apt-get install libsdl2-2.0-0`
  - `sudo apt-get install libsdl2-image-2.0-0`
  - `sudo apt-get install libsdl2-ttf-2.0-0`
  - For reference, have following libraries: SDL2-2.0.so.0, SDL2_ttf-2.0.so.0, SDL2_image-2.0.so.0
- Make sure you have OpenGL available
- To run either use `dotnet YAFC.dll` in the terminal, or run `YAFC` as an executable
### Other
In general, ensure you have SDL2, OpenGL and dotnet 6 or later. 

### Flathub
Note that [the version available on Flathub](https://flathub.org/apps/details/com.github.petebuffon.yafc) is not the Community Edition. Its repo can be found at https://github.com/petebuffon/yafc. 
