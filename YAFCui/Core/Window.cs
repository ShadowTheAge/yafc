using System;
using System.Drawing;
using SDL2;

namespace YAFC.UI
{
    public abstract class Window : WidgetContainer, IPanel
    {
        public readonly RenderBatch rootBatch;
        private IntPtr window;
        internal IntPtr renderer;
        internal IntPtr surface;
        public uint id { get; private set; }
        private float contentWidth, contentHeight;
        private int windowWidth, windowHeight;
        private bool repaintRequired = true;
        private bool software;
        protected SchemeColor backgroundColor = SchemeColor.Background;

        protected Window()
        {
            padding = new Padding(5f, 2f);
            rootBatch = new RenderBatch(this);
        }

        protected void Create(string title, float width, bool software)
        {
            this.software = software;
            contentWidth = width;
            rootBatch.Rebuild(new SizeF(contentWidth, 100f));
            windowWidth = RenderingUtils.UnitsToPixels(contentWidth);
            windowHeight = RenderingUtils.UnitsToPixels(contentHeight);
            window = SDL.SDL_CreateWindow(title,
                SDL.SDL_WINDOWPOS_CENTERED,
                SDL.SDL_WINDOWPOS_CENTERED,
                windowWidth,
                windowHeight,
                SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL
            );

            if (software)
            {
                surface = SDL.SDL_GetWindowSurface(window);
                renderer = SDL.SDL_CreateSoftwareRenderer(surface);
            }
            else
            {
                renderer = SDL.SDL_CreateRenderer(window, 0, SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
            }

            SDL.SDL_SetRenderDrawBlendMode(renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            id = SDL.SDL_GetWindowID(window);
            Ui.RegisterWindow(id, this);
        }

        public void Render()
        {
            if (!repaintRequired)
                return;
            repaintRequired = false;
            if (rootBatch.rebuildRequired)
                rootBatch.Rebuild(new SizeF(contentWidth, 100f));
            var bgColor = backgroundColor.ToSdlColor();
            SDL.SDL_SetRenderDrawColor(renderer, bgColor.r,bgColor.g,bgColor.b, bgColor.a);
            SDL.SDL_RenderClear(renderer);
            
            var newWindowWidth = RenderingUtils.UnitsToPixels(contentWidth);
            var newWindowHeight = RenderingUtils.UnitsToPixels(contentHeight);
            if (windowWidth != newWindowWidth || windowHeight != newWindowHeight)
            {
                windowWidth = newWindowWidth;
                windowHeight = newWindowHeight;
                SDL.SDL_SetWindowSize(window, newWindowWidth, newWindowHeight);
            }
            
            rootBatch.Present(this, default, new RectangleF(default, new SizeF(contentWidth, contentHeight)));
            SDL.SDL_RenderPresent(renderer);
            if (surface != IntPtr.Zero)
            {
                SDL.SDL_UpdateWindowSurface(window);
            }
        }

        public bool Raycast<T>(PointF position, out T result, out RenderBatch batch) where T : class, IMouseHandle => rootBatch.Raycast<T>(position, out result, out batch);

        public void BuildPanel(LayoutState state)
        {
            Build(state);
            contentHeight = state.fullHeight;
        }

        internal void DrawIcon(SDL.SDL_Rect position, Icon icon, SchemeColor color)
        {
            if (software)
            {
                var sdlColor = color.ToSdlColor();
                var iconSurface = IconCollection.GetIconSurface(icon);
                SDL.SDL_SetSurfaceColorMod(iconSurface, sdlColor.r, sdlColor.g, sdlColor.b);
                SDL.SDL_BlitScaled(iconSurface, ref IconCollection.IconRect, surface, ref position);
            }
        }

        public void Repaint()
        {
            repaintRequired = true;
        }
    }
}