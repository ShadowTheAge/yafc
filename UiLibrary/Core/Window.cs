using System;
using System.Drawing;
using SDL2;

namespace UI
{
    public class Window : SdlResource, IPanel
    {
        private readonly IWidget contents;
        internal readonly IntPtr renderer;
        internal readonly IntPtr surface;
        public readonly RenderBatch rootBatch;
        public readonly uint id;
        private float contentWidth, contentHeight;
        private int windowWidth, windowHeight;
        private bool repaintRequired = true;

        public Window(IWidget contents, float width, bool software)
        {
            this.contents = contents;
            contentWidth = width;
            rootBatch = new RenderBatch(this);
            rootBatch.Rebuild(new SizeF(width, 100f));
            windowWidth = RenderingUtils.UnitsToPixels(contentWidth);
            windowHeight = RenderingUtils.UnitsToPixels(contentHeight);
            _handle = SDL.SDL_CreateWindow("Factorio Calculator",
                SDL.SDL_WINDOWPOS_CENTERED,
                SDL.SDL_WINDOWPOS_CENTERED,
                windowWidth,
                windowHeight,
                SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL
            );

            if (software)
            {
                surface = SDL.SDL_GetWindowSurface(_handle);
                renderer = SDL.SDL_CreateSoftwareRenderer(surface);
            }
            else
            {
                renderer = SDL.SDL_CreateRenderer(_handle, 0, SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
            }

            SDL.SDL_SetRenderDrawBlendMode(renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            id = SDL.SDL_GetWindowID(_handle);
            Ui.RegisterWindow(id, this);
        }

        protected override void ReleaseUnmanagedResources()
        {
            SDL.SDL_DestroyRenderer(renderer);
            SDL.SDL_DestroyWindow(_handle);
        }

        public void Render()
        {
            if (!repaintRequired)
                return;
            repaintRequired = false;
            if (rootBatch.rebuildRequired)
                rootBatch.Rebuild(new SizeF(contentWidth, 100f));
            var bgColor = SchemeColor.Background.ToSdlColor();
            SDL.SDL_SetRenderDrawColor(renderer, bgColor.r,bgColor.g,bgColor.b, bgColor.a);
            SDL.SDL_RenderClear(renderer);
            
            var newWindowWidth = RenderingUtils.UnitsToPixels(contentWidth);
            var newWindowHeight = RenderingUtils.UnitsToPixels(contentHeight);
            if (windowWidth != newWindowWidth || windowHeight != newWindowHeight)
            {
                windowWidth = newWindowWidth;
                windowHeight = newWindowHeight;
                SDL.SDL_SetWindowSize(_handle, newWindowWidth, newWindowHeight);
            }
            
            rootBatch.Present(this, default, new RectangleF(default, new SizeF(contentWidth, contentHeight)));
            SDL.SDL_RenderPresent(renderer);
            if (surface != IntPtr.Zero)
            {
                SDL.SDL_UpdateWindowSurface(_handle);
            }
        }

        public T Raycast<T>(PointF position) where T : class, IMouseHandle => rootBatch.Raycast<T>(position);

        public LayoutPosition BuildPanel(RenderBatch batch, LayoutPosition location)
        {
            var result = contents.Build(batch, location);
            contentHeight = result.y;
            return result;
        }

        internal void DrawIcon(SDL.SDL_Rect position, Icon icon, SchemeColor color)
        {
            var iconSurface = IconCollection.GetIconSurface(icon);
            SDL.SDL_BlitScaled(iconSurface, ref IconCollection.IconRect, surface, ref position);
        }

        public void Rebuild()
        {
            repaintRequired = true;
        }
    }
}