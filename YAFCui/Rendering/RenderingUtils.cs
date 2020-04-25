using System;
using System.Runtime.CompilerServices;
using SDL2;

namespace YAFC.UI
{
    public static class RenderingUtils
    {
        public static readonly IntPtr cursorCaret = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_IBEAM);
        public static readonly IntPtr cursorArrow = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_ARROW);
        public static readonly IntPtr cursorHand = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_HAND);
        public const byte SEMITRANSPARENT = 100;

        private static SDL.SDL_Color ColorFromHex(int hex) => new SDL.SDL_Color {r = (byte)(hex >> 16), g = (byte)(hex >> 8), b = (byte) hex, a = 255};
        public static readonly SDL.SDL_Color Black = new SDL.SDL_Color {a = 255};
        public static readonly SDL.SDL_Color White = new SDL.SDL_Color {r = 255, g = 255, b = 255, a = 255};
        public static readonly SDL.SDL_Color BlackTransparent = new SDL.SDL_Color {a = SEMITRANSPARENT};
        public static readonly SDL.SDL_Color WhiteTransparent = new SDL.SDL_Color {r = 255, g = 255, b = 255, a = SEMITRANSPARENT};

        public static SchemeColor GetTextColorFromBackgroundColor(SchemeColor color) => (SchemeColor) ((int) color & ~3) + 2;

        private static readonly SDL.SDL_Color[] SchemeColors =
        {
            default, new SDL.SDL_Color {b = 255, g = 128, a = 60}, ColorFromHex(0x0645AD), White, // Special group
            White, Black, White, WhiteTransparent, // pure group
            
            ColorFromHex(0xf4f4f4), White, Black, BlackTransparent, // Background group 
            ColorFromHex(0x26c6da), ColorFromHex(0x0095a8), Black, BlackTransparent, // Primary group
            ColorFromHex(0xff9800), ColorFromHex(0xc66900), Black, BlackTransparent, // Secondary group
            ColorFromHex(0xbf360c), ColorFromHex(0x870000), White, WhiteTransparent, // Error group
            ColorFromHex(0xe4e4e4), ColorFromHex(0xc4c4c4), Black, BlackTransparent // Grey group
        };

        public static SDL.SDL_Color ToSdlColor(this SchemeColor color) => SchemeColors[(int) color];
        public static unsafe ref SDL.SDL_Surface AsSdlSurface(IntPtr ptr) => ref Unsafe.AsRef<SDL.SDL_Surface>((void*) ptr);

        public static readonly IntPtr CircleSurface;
        private static readonly SDL.SDL_Rect CircleTopLeft, CircleTopRight, CircleBottomLeft, CircleBottomRight, CircleTop, CircleBottom, CircleLeft, CircleRight;

        static unsafe RenderingUtils()
        {
            var surfacePtr = SDL.SDL_CreateRGBSurfaceWithFormat(0, 32, 32, 0, SDL.SDL_PIXELFORMAT_RGBA8888);
            ref var surface = ref AsSdlSurface(surfacePtr);

            const int circleSize = 32;
            const float center = (circleSize - 1) / 2f;

            var pixels = (int*)surface.pixels;
            for (var x = 0; x < 32; x++)
            {
                for (var y = 0; y < 32; y++)
                {
                    var dx = (center - x)/center;
                    var dy = (center - y)/center;
                    var dist = MathF.Sqrt(dx * dx + dy * dy);
                    *pixels++ = dist >= 1f ? 0 : MathUtils.Round(40 * (1f - dist));
                }
            }

            const int halfcircle = (circleSize / 2) - 1;
            const int halfStride = circleSize - halfcircle;

            CircleTopLeft = new SDL.SDL_Rect {x = 0, y = 0, w = halfcircle, h = halfcircle};
            CircleTopRight = new SDL.SDL_Rect {x = halfStride, y = 0, w = halfcircle, h = halfcircle};
            CircleBottomLeft = new SDL.SDL_Rect {x = 0, y = halfStride, w = halfcircle, h = halfcircle};
            CircleBottomRight = new SDL.SDL_Rect {x = halfStride, y = halfStride, w = halfcircle, h = halfcircle};
            CircleTop = new SDL.SDL_Rect {x = halfcircle, y = 0, w = 2, h = halfcircle};
            CircleBottom = new SDL.SDL_Rect {x = halfcircle, y = halfStride, w = 2, h = halfcircle};
            CircleLeft = new SDL.SDL_Rect {x = 0, y = halfcircle, w = halfcircle, h = 2};
            CircleRight = new SDL.SDL_Rect {x = halfStride, y = halfcircle, w = halfcircle, h = 2};
            CircleSurface = surfacePtr;
        }
        
        public struct BlitMapping
        {
            public SDL.SDL_Rect position;
            public SDL.SDL_Rect texture;

            public BlitMapping(SDL.SDL_Rect texture, SDL.SDL_Rect position)
            {
                this.texture = texture;
                this.position = position;
            }
        }

        public static void GetBorderParameters(float unitsToPixels, RectangleBorder border, out int top, out int side, out int bottom)
        {
            if (border == RectangleBorder.Full)
            {
                top = MathUtils.Round(unitsToPixels * 0.5f);
                side = MathUtils.Round(unitsToPixels);
                bottom = MathUtils.Round(unitsToPixels * 2f);
            }
            else
            {
                top = MathUtils.Round(unitsToPixels * 0.2f);
                side = MathUtils.Round(unitsToPixels * 0.3f);
                bottom = MathUtils.Round(unitsToPixels * 0.5f);
            }
        }

        public static void GetBorderBatch(SDL.SDL_Rect position, int shadowTop, int shadowSide, int shadowBottom, ref BlitMapping[] result)
        {
            if (result == null || result.Length != 8)
                Array.Resize(ref result, 8);
            var rect = new SDL.SDL_Rect {h = shadowTop, x = position.x - shadowSide, y = position.y-shadowTop, w = shadowSide};
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