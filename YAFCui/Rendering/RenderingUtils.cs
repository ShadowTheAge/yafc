using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using SDL2;

namespace YAFC.UI
{
    public static class RenderingUtils
    {
        public static readonly IntPtr cursorCaret = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_IBEAM);
        public static readonly IntPtr cursorArrow = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_ARROW);
        public static readonly IntPtr cursorHand = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_HAND);

        private static SDL.SDL_Color ColorFromHex(int hex) => new SDL.SDL_Color {r = (byte)(hex >> 16), g = (byte)(hex >> 8), b = (byte) hex, a = 255};
        public static readonly SDL.SDL_Color Black = new SDL.SDL_Color {a = 255};
        public static readonly SDL.SDL_Color White = new SDL.SDL_Color {r = 255, g = 255, b = 255, a = 255};

        private static readonly SDL.SDL_Color[] SchemeColors =
        {
            default, new SDL.SDL_Color {b = 255, g = 128, a = 50}, White, // none group
            ColorFromHex(0x0645AD), White, White,
            
            ColorFromHex(0xf4f4f4), ColorFromHex(0xe4e4e4), Black, // Background group 
            ColorFromHex(0x26c6da), ColorFromHex(0x0095a8), Black, // Primary group
            ColorFromHex(0xff9800), ColorFromHex(0xc66900), Black, // Secondary group
            ColorFromHex(0xbf360c), ColorFromHex(0x870000), White, // Error group
        };

        public static SDL.SDL_Color ToSdlColor(this SchemeColor color) => SchemeColors[(int) color];
        public static unsafe ref SDL.SDL_Surface AsSdlSurface(IntPtr ptr) => ref Unsafe.AsRef<SDL.SDL_Surface>((void*) ptr);
    }
}