using System;
using System.Numerics;
using System.Runtime.InteropServices;
using SDL2;

namespace YAFC.UI {
    // Main window is resizable and hardware-accelerated
    public abstract class WindowMain : Window {
        protected void Create(string title, int display) {
            if (visible)
                return;
            pixelsPerUnit = CalculateUnitsToPixels(display);
            int minwidth = MathUtils.Round(85f * pixelsPerUnit);
            int minheight = MathUtils.Round(60f * pixelsPerUnit);
            window = SDL.SDL_CreateWindow(title,
                SDL.SDL_WINDOWPOS_CENTERED_DISPLAY(display),
                SDL.SDL_WINDOWPOS_CENTERED_DISPLAY(display),
                minwidth, minheight,
                SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE | (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 0 : SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL)
            );
            SDL.SDL_SetWindowMinimumSize(window, minwidth, minheight);
            WindowResize();
            surface = new MainWindowDrawingSurface(this);
            base.Create();
        }

        protected override void BuildContents(ImGui gui) {
            BuildContent(gui);
            gui.SetContextRect(new Rect(default, size));
        }

        protected abstract void BuildContent(ImGui gui);

        protected override void OnRepaint() {
            rootGui.Rebuild();
            base.OnRepaint();
        }

        internal override void WindowResize() {
            SDL.SDL_GetWindowSize(window, out int windowWidth, out int windowHeight);
            contentSize = new Vector2(windowWidth / pixelsPerUnit, windowHeight / pixelsPerUnit);
            base.WindowResize();
        }

        protected WindowMain(Padding padding) : base(padding) { }
    }

    internal class MainWindowDrawingSurface : DrawingSurface {
        private readonly IconAtlas atlas = new IconAtlas();
        private readonly IntPtr circleTexture;

        public override Window window { get; }

        public MainWindowDrawingSurface(WindowMain window) : base(window.pixelsPerUnit) {
            this.window = window;
            renderer = SDL.SDL_CreateRenderer(window.window, 0, SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
            circleTexture = SDL.SDL_CreateTextureFromSurface(renderer, RenderingUtils.CircleSurface);
            byte colorMod = RenderingUtils.darkMode ? (byte)255 : (byte)0;
            _ = SDL.SDL_SetTextureColorMod(circleTexture, colorMod, colorMod, colorMod);
        }

        internal override void DrawIcon(SDL.SDL_Rect position, Icon icon, SchemeColor color) {
            atlas.DrawIcon(renderer, icon, position, color.ToSdlColor());
        }

        internal override void DrawBorder(SDL.SDL_Rect position, RectangleBorder border) {
            RenderingUtils.GetBorderParameters(pixelsPerUnit, border, out int top, out int side, out int bottom);
            RenderingUtils.GetBorderBatch(position, top, side, bottom, ref blitMapping);
            var bm = blitMapping;
            for (int i = 0; i < bm.Length; i++) {
                ref var cur = ref bm[i];
                _ = SDL.SDL_RenderCopy(renderer, circleTexture, ref cur.texture, ref cur.position);
            }
        }
    }
}
