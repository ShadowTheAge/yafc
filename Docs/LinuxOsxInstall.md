# Both OSX and Linux builds are experimental!

## OSX installation instructions

- [Install dotnet core (v3.1 or later)](https://dotnet.microsoft.com/download)
- [Install brew](https://brew.sh/)
- Install SDL2 using brew (type the following in the terminal):
    - `brew install SDL2`
	- `brew install SDL2_image`
	- `brew install SDL2_ttf`
	- For reference, have following libraries: libSDL2.dylib, libSDL2_ttf.dylib, libSDL2_image.dylib
- To run either use `dotnet YAFC.dll` in the terminal, or run `YAFC` as an executable
- Make sure you have OpenGL availible

## Linux installation instructions

1. [YAFC is available on Flathub.](https://flathub.org/apps/details/com.github.petebuffon.yafc)
2. Download  
  - [Install dotnet core (v3.1 or later)](https://dotnet.microsoft.com/download)
  - Install SDL2 (The following commands for debian):
    - `sudo apt-get install libsdl2-2.0-0`
    - `sudo apt-get install libsdl2-image-2.0-0`
    - `sudo apt-get install libsdl2-ttf-2.0-0`
    - If you have other distribution, visit https://wiki.libsdl.org/Installation
    - For reference, have following libraries: SDL2-2.0.so.0, SDL2_ttf-2.0.so.0, SDL2_image-2.0.so.0
  - To run either use `dotnet YAFC.dll` in the terminal, or run `YAFC` as an executable
  - Make sure you have OpenGL availible
