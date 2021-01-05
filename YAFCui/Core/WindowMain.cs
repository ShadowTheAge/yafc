using System;
using System.Numerics;
using System.Runtime.InteropServices;
using SDL2;

namespace YAFC.UI
{
    // Main window is resizable and hardware-accelerated
    public abstract class WindowMain : Window
    {
        private IconAtlas atlas = new IconAtlas();
        private IntPtr circleTexture;
        protected void Create(string title, int display)
        {
            if (visible)
                return;
            pixelsPerUnit = CalculateUnitsToPixels(display);
            var minwidth = MathUtils.Round(82f * pixelsPerUnit);
            var minheight = MathUtils.Round(60f * pixelsPerUnit); 
            window = SDL.SDL_CreateWindow(title,
                SDL.SDL_WINDOWPOS_CENTERED_DISPLAY(display),
                SDL.SDL_WINDOWPOS_CENTERED_DISPLAY(display),
                minwidth, minheight,
                SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE | (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 0 : SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL)
            );
            SDL.SDL_SetWindowMinimumSize(window, minwidth, minheight);
            WindowResize();
            renderer = SDL.SDL_CreateRenderer(window, 0, SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
            circleTexture = SDL.SDL_CreateTextureFromSurface(renderer, RenderingUtils.CircleSurface);
            var colorMod = RenderingUtils.darkMode ? (byte) 255 : (byte) 0;
            SDL.SDL_SetTextureColorMod(circleTexture, colorMod, colorMod, colorMod);
            base.Create();
        }

        protected override void BuildContents(ImGui gui)
        {
            BuildContent(gui);
            gui.SetContextRect(new Rect(default, size));
        }

        protected abstract void BuildContent(ImGui gui);

        protected override void OnRepaint()
        {
            rootGui.Rebuild();
            base.OnRepaint();
        }

        internal override void WindowResize()
        {
            SDL.SDL_GetWindowSize(window, out var windowWidth, out var windowHeight);
            contentSize = new Vector2(windowWidth/pixelsPerUnit, windowHeight/pixelsPerUnit);
            base.WindowResize();
        }

        internal override void DrawIcon(SDL.SDL_Rect position, Icon icon, SchemeColor color)
        {
            atlas.DrawIcon(renderer, icon, position, color.ToSdlColor());
        }

        internal override void DrawBorder(SDL.SDL_Rect position, RectangleBorder border)
        {
            RenderingUtils.GetBorderParameters(pixelsPerUnit, border, out var top, out var side, out var bottom);
            RenderingUtils.GetBorderBatch(position, top, side, bottom, ref blitMapping);
            var bm = blitMapping;
            for (var i = 0; i < bm.Length; i++)
            {
                ref var cur = ref bm[i];
                SDL.SDL_RenderCopy(renderer, circleTexture, ref cur.texture, ref cur.position);
            }
        }

        protected WindowMain(Padding padding) : base(padding) {}
    }
}