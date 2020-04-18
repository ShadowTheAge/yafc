using System;
using System.Numerics;
using SDL2;

namespace YAFC.UI
{
    // Main window is resizable and hardware-accelerated, it also starts maximized
    public abstract class WindowMain : Window
    {
        private IconAtlas atlas = new IconAtlas();
        private IntPtr circleTexture;
        protected void Create(string title, int display)
        {
            if (visible)
                return;
            SDL.SDL_GetDisplayDPI(display, out var ddpi, out _, out _);
            unitsToPixels = UnitsToPixelsFromDpi(ddpi);
            SDL.SDL_GetDisplayBounds(display, out var rect);
            window = SDL.SDL_CreateWindow(title,
                SDL.SDL_WINDOWPOS_CENTERED_DISPLAY(display),
                SDL.SDL_WINDOWPOS_CENTERED_DISPLAY(display),
                rect.w/2,
                rect.w/2,
                SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL | SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL.SDL_WindowFlags.SDL_WINDOW_MAXIMIZED
            );
            WindowResize();
            rootBatch.Rebuild(this, contentSize, unitsToPixels);
            renderer = SDL.SDL_CreateRenderer(window, 0, SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
            circleTexture = SDL.SDL_CreateTextureFromSurface(renderer, RenderingUtils.CircleSurface);
            base.Create();
        }

        internal override void WindowResize()
        {
            SDL.SDL_GetWindowSize(window, out var windowWidth, out var windowHeight);
            contentSize = new Vector2(windowWidth/unitsToPixels, windowHeight/unitsToPixels);
            base.WindowResize();
        }

        internal override void DrawIcon(SDL.SDL_Rect position, Icon icon, SchemeColor color)
        {
            atlas.DrawIcon(renderer, icon, position, color.ToSdlColor());
        }

        internal override void DrawBorder(SDL.SDL_Rect position, RectangleBorder border)
        {
            RenderingUtils.GetBorderParameters(unitsToPixels, border, out var top, out var side, out var bottom);
            RenderingUtils.GetBorderBatch(position, top, side, bottom, ref blitMapping);
            var bm = blitMapping;
            for (var i = 0; i < bm.Length; i++)
            {
                ref var cur = ref bm[i];
                SDL.SDL_RenderCopy(renderer, circleTexture, ref cur.texture, ref cur.position);
            }
        }
    }
}