using System;
using System.Runtime.CompilerServices;
using SDL2;

namespace YAFC.UI {
    public static class RenderingUtils {
        public static readonly IntPtr cursorCaret = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_IBEAM);
        public static readonly IntPtr cursorArrow = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_ARROW);
        public static readonly IntPtr cursorHand = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_HAND);
        public static readonly IntPtr cursorHorizontalResize = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZEWE);
        public const byte SEMITRANSPARENT = 100;

        private static SDL.SDL_Color ColorFromHex(int hex) {
            return new SDL.SDL_Color { r = (byte)(hex >> 16), g = (byte)(hex >> 8), b = (byte)hex, a = 255 };
        }

        public static readonly SDL.SDL_Color Black = new SDL.SDL_Color { a = 255 };
        public static readonly SDL.SDL_Color White = new SDL.SDL_Color { r = 255, g = 255, b = 255, a = 255 };
        public static readonly SDL.SDL_Color BlackTransparent = new SDL.SDL_Color { a = SEMITRANSPARENT };
        public static readonly SDL.SDL_Color WhiteTransparent = new SDL.SDL_Color { r = 255, g = 255, b = 255, a = SEMITRANSPARENT };

        public static SchemeColor GetTextColorFromBackgroundColor(SchemeColor color) {
            return (SchemeColor)((int)color & ~3) + 2;
        }

        private static readonly SDL.SDL_Color[] LightModeScheme = {
            default, new SDL.SDL_Color {b = 255, g = 128, a = 60}, ColorFromHex(0x0645AD), ColorFromHex(0x1b5e20), // Special group
            White, Black, White, WhiteTransparent, // pure group
            
            ColorFromHex(0xf4f4f4), White, Black, BlackTransparent, // Background group 
            ColorFromHex(0x26c6da), ColorFromHex(0x0095a8), Black, BlackTransparent, // Primary group
            ColorFromHex(0xff9800), ColorFromHex(0xc66900), Black, BlackTransparent, // Secondary group
            ColorFromHex(0xbf360c), ColorFromHex(0x870000), White, WhiteTransparent, // Error group
            ColorFromHex(0xe4e4e4), ColorFromHex(0xc4c4c4), Black, BlackTransparent, // Grey group
            ColorFromHex(0xbd33a4), ColorFromHex(0x8b008b), Black, BlackTransparent, // Magenta group
            ColorFromHex(0x6abf69), ColorFromHex(0x388e3c), Black, BlackTransparent, // Green group
        };

        private static readonly SDL.SDL_Color[] DarkModeScheme = {
            default, new SDL.SDL_Color {b = 255, g = 128, a = 120}, ColorFromHex(0xff9800), ColorFromHex(0x1b5e20), // Special group
            Black, White, White, WhiteTransparent, // pure group

            ColorFromHex(0x141414), Black, White, WhiteTransparent, // Background group 
            ColorFromHex(0x006978), ColorFromHex(0x0097a7), White, WhiteTransparent, // Primary group
            ColorFromHex(0x5b2800), ColorFromHex(0x8c5100), White, WhiteTransparent, // Secondary group
            ColorFromHex(0xbf360c), ColorFromHex(0x870000), White, WhiteTransparent, // Error group
            ColorFromHex(0x343434), ColorFromHex(0x545454), White, WhiteTransparent, // Grey group
            ColorFromHex(0x8b008b), ColorFromHex(0xbd33a4), Black, BlackTransparent, // Magenta group
            ColorFromHex(0x00600f), ColorFromHex(0x00701a), Black, BlackTransparent, // Green group
        };

        private static SDL.SDL_Color[] SchemeColors = LightModeScheme;

        public static void SetColorScheme(bool darkMode) {
            RenderingUtils.darkMode = darkMode;
            SchemeColors = darkMode ? DarkModeScheme : LightModeScheme;
            byte col = darkMode ? (byte)0 : (byte)255;
            _ = SDL.SDL_SetSurfaceColorMod(CircleSurface, col, col, col);
        }

        public static SDL.SDL_Color ToSdlColor(this SchemeColor color) {
            return SchemeColors[(int)color];
        }

        public static unsafe ref SDL.SDL_Surface AsSdlSurface(IntPtr ptr) {
            return ref Unsafe.AsRef<SDL.SDL_Surface>((void*)ptr);
        }

        public static readonly IntPtr CircleSurface;
        private static readonly SDL.SDL_Rect CircleTopLeft, CircleTopRight, CircleBottomLeft, CircleBottomRight, CircleTop, CircleBottom, CircleLeft, CircleRight;
        public static bool darkMode { get; private set; }

        static unsafe RenderingUtils() {
            var surfacePtr = SDL.SDL_CreateRGBSurfaceWithFormat(0, 32, 32, 0, SDL.SDL_PIXELFORMAT_RGBA8888);
            ref var surface = ref AsSdlSurface(surfacePtr);

            const int circleSize = 32;
            const float center = (circleSize - 1) / 2f;

            uint* pixels = (uint*)surface.pixels;
            for (int x = 0; x < 32; x++) {
                for (int y = 0; y < 32; y++) {
                    float dx = (center - x) / center;
                    float dy = (center - y) / center;
                    float dist = MathF.Sqrt((dx * dx) + (dy * dy));
                    *pixels++ = 0xFFFFFF00 | (dist >= 1f ? 0 : (uint)MathUtils.Round(38 * (1f - dist)));
                }
            }

            const int halfCircle = (circleSize / 2) - 1;
            const int halfStride = circleSize - halfCircle;

            CircleTopLeft = new SDL.SDL_Rect { x = 0, y = 0, w = halfCircle, h = halfCircle };
            CircleTopRight = new SDL.SDL_Rect { x = halfStride, y = 0, w = halfCircle, h = halfCircle };
            CircleBottomLeft = new SDL.SDL_Rect { x = 0, y = halfStride, w = halfCircle, h = halfCircle };
            CircleBottomRight = new SDL.SDL_Rect { x = halfStride, y = halfStride, w = halfCircle, h = halfCircle };
            CircleTop = new SDL.SDL_Rect { x = halfCircle, y = 0, w = 2, h = halfCircle };
            CircleBottom = new SDL.SDL_Rect { x = halfCircle, y = halfStride, w = 2, h = halfCircle };
            CircleLeft = new SDL.SDL_Rect { x = 0, y = halfCircle, w = halfCircle, h = 2 };
            CircleRight = new SDL.SDL_Rect { x = halfStride, y = halfCircle, w = halfCircle, h = 2 };
            CircleSurface = surfacePtr;
            _ = SDL.SDL_SetSurfaceColorMod(CircleSurface, 0, 0, 0);
        }

        public struct BlitMapping {
            public SDL.SDL_Rect position;
            public SDL.SDL_Rect texture;

            public BlitMapping(SDL.SDL_Rect texture, SDL.SDL_Rect position) {
                this.texture = texture;
                this.position = position;
            }
        }

        public static void GetBorderParameters(float unitsToPixels, RectangleBorder border, out int top, out int side, out int bottom) {
            if (border == RectangleBorder.Full) {
                top = MathUtils.Round(unitsToPixels * 0.5f);
                side = MathUtils.Round(unitsToPixels);
                bottom = MathUtils.Round(unitsToPixels * 2f);
            }
            else {
                top = MathUtils.Round(unitsToPixels * 0.2f);
                side = MathUtils.Round(unitsToPixels * 0.3f);
                bottom = MathUtils.Round(unitsToPixels * 0.5f);
            }
        }

        public static void GetBorderBatch(SDL.SDL_Rect position, int shadowTop, int shadowSide, int shadowBottom, ref BlitMapping[] result) {
            if (result == null || result.Length != 8) {
                Array.Resize(ref result, 8);
            }

            SDL.SDL_Rect rect = new SDL.SDL_Rect { h = shadowTop, x = position.x - shadowSide, y = position.y - shadowTop, w = shadowSide };
            result[0] = new BlitMapping(CircleTopLeft, rect);
            rect.x = position.x;
            rect.w = position.w;
            result[1] = new BlitMapping(CircleTop, rect);
            rect.x += rect.w;
            rect.w = shadowSide;
            result[2] = new BlitMapping(CircleTopRight, rect);
            rect.y = position.y;
            rect.h = position.h;
            result[3] = new BlitMapping(CircleRight, rect);
            rect.y += rect.h;
            rect.h = shadowBottom;
            result[4] = new BlitMapping(CircleBottomRight, rect);
            rect.x = position.x;
            rect.w = position.w;
            result[5] = new BlitMapping(CircleBottom, rect);
            rect.x -= shadowSide;
            rect.w = shadowSide;
            result[6] = new BlitMapping(CircleBottomLeft, rect);
            rect.y = position.y;
            rect.h = position.h;
            result[7] = new BlitMapping(CircleLeft, rect);
        }
    }
}
