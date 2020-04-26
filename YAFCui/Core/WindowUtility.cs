using System;
using System.Numerics;
using SDL2;

namespace YAFC.UI
{
    // Utility window is not hardware-accelerated and auto-size (and not resizable)
    public abstract class WindowUtility : Window
    {
        private int windowWidth, windowHeight;
        private IntPtr surface;
        private Window parent;
        
        public WindowUtility(Padding padding) : base(padding) {}
        
        protected void Create(string title, float width, Window parent)
        {
            if (visible)
                return;
            this.parent = parent;
            contentSize.X = width;
            var display = parent == null ? 0 : SDL.SDL_GetWindowDisplayIndex(parent.window);
            SDL.SDL_GetDisplayDPI(display, out var ddpi, out _, out _);
            pixelsPerUnit = UnitsToPixelsFromDpi(ddpi);
            SDL.SDL_GetDisplayBounds(display, out var rect);
            contentSize = rootGui.CalculateState(new Rect(0, 0, width, 0), null, pixelsPerUnit);
            windowWidth = rootGui.UnitsToPixels(contentSize.X);
            windowHeight = rootGui.UnitsToPixels(contentSize.Y);
            var flags = (SDL.SDL_WindowFlags) 0;
            if (parent != null)
                flags |= SDL.SDL_WindowFlags.SDL_WINDOW_UTILITY | SDL.SDL_WindowFlags.SDL_WINDOW_SKIP_TASKBAR;
            window = SDL.SDL_CreateWindow(title,
                SDL.SDL_WINDOWPOS_CENTERED_DISPLAY(display),
                SDL.SDL_WINDOWPOS_CENTERED_DISPLAY(display),
                windowWidth,
                windowHeight,
                flags
            );
            
            surface = SDL.SDL_GetWindowSurface(window);
            renderer = SDL.SDL_CreateSoftwareRenderer(surface);
            base.Create();
        }
        
        internal override void DrawIcon(SDL.SDL_Rect position, Icon icon, SchemeColor color)
        {
            var sdlColor = color.ToSdlColor();
            var iconSurface = IconCollection.GetIconSurface(icon);
            SDL.SDL_SetSurfaceColorMod(iconSurface, sdlColor.r, sdlColor.g, sdlColor.b);
            SDL.SDL_SetSurfaceAlphaMod(iconSurface, sdlColor.a);
            SDL.SDL_BlitScaled(iconSurface, ref IconCollection.IconRect, surface, ref position);
        }

        internal override void DrawBorder(SDL.SDL_Rect position, RectangleBorder border)
        {
            RenderingUtils.GetBorderParameters(pixelsPerUnit, border, out var top, out var side, out var bottom);
            RenderingUtils.GetBorderBatch(position, top, side, bottom, ref blitMapping);
            var bm = blitMapping;
            for (var i = 0; i < bm.Length; i++)
            {
                ref var cur = ref bm[i];
                SDL.SDL_BlitScaled(RenderingUtils.CircleSurface, ref cur.texture, surface, ref cur.position);
            }
        }

        private void CheckSizeChange()
        {
            var newWindowWidth = rootGui.UnitsToPixels(contentSize.X);
            var newWindowHeight = rootGui.UnitsToPixels(contentSize.Y);
            if (windowWidth != newWindowWidth || windowHeight != newWindowHeight)
            {
                windowWidth = newWindowWidth;
                windowHeight = newWindowHeight;
                SDL.SDL_SetWindowSize(window, newWindowWidth, newWindowHeight);
                WindowResize();
            }
        }

        public override SDL.SDL_Rect SetClip(SDL.SDL_Rect clip)
        {
            SDL.SDL_SetClipRect(surface, ref clip);
            return base.SetClip(clip);
        }

        internal override void MainRender()
        {
            CheckSizeChange();
            base.MainRender();
            if (surface != IntPtr.Zero)
                SDL.SDL_UpdateWindowSurface(window);
        }

        protected internal override void Close()
        {
            base.Close();
            surface = IntPtr.Zero;
            parent = null;
        }

        // TODO this is work-around for inability to create utility or modal window in SDL2
        // Fake utility windows are closed on focus lost
        public override void FocusLost()
        {
            if (parent != null)
            {
                Close();
            }
        }

        internal override void WindowResize()
        {
            surface = SDL.SDL_GetWindowSurface(window);
            renderer = SDL.SDL_CreateSoftwareRenderer(surface);
            SDL.SDL_SetRenderDrawBlendMode(renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            base.WindowResize();
        }
    }
}