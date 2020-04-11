using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using SDL2;

namespace UI
{
    internal static class RenderingUtils
    {
        public static IntPtr renderer;
        public static float pixelsPerUnit = 20;

        public static ushort UnitsToPixels(float units) => (ushort) MathF.Round(units * pixelsPerUnit);
        public static float PixelsToUnits(int pixels) => pixels / pixelsPerUnit;
        public static SpriteAtlas atlas;
        
        public static SDL.SDL_Rect ToSdlRect(this RectangleF rect, SizeF offset = default)
        {
            return new SDL.SDL_Rect
            {
                x = UnitsToPixels(rect.X + offset.Width),
                y = UnitsToPixels(rect.Y + offset.Height),
                w = UnitsToPixels(rect.Width),
                h = UnitsToPixels(rect.Height)
            };
        }
        
        public static readonly IntPtr cursorCaret = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_IBEAM);
        public static readonly IntPtr cursorArrow = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_ARROW);

        private static SDL.SDL_Color ColorFromHex(int hex) => new SDL.SDL_Color {r = (byte)(hex >> 16), g = (byte)(hex >> 8), b = (byte) hex, a = 255};
        public static readonly SDL.SDL_Color Black = new SDL.SDL_Color {a = 255};
        public static readonly SDL.SDL_Color White = new SDL.SDL_Color {r = 255, g = 255, b = 255, a = 255};

        private static readonly SDL.SDL_Color[] SchemeColors =
        {
            default, new SDL.SDL_Color {b = 255, g = 128, a = 50}, default, // none group
            ColorFromHex(0xf4f4f4), ColorFromHex(0xe4e4e4), Black, // Background group 
            ColorFromHex(0x26c6da), ColorFromHex(0x0095a8), Black, // Primary group
            ColorFromHex(0xff9800), ColorFromHex(0xc66900), Black, // Secondary group
            ColorFromHex(0xbf360c), ColorFromHex(0x870000), White, // Error group
        };

        public static SDL.SDL_Color ToSdlColor(this SchemeColor color) => SchemeColors[(int) color];
        public static unsafe ref SDL.SDL_Surface AsSdlSurface(IntPtr ptr) => ref Unsafe.AsRef<SDL.SDL_Surface>((void*) ptr);
    }
}