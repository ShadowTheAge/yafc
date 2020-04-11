using System;
using System.Drawing;
using SDL2;

namespace UI
{
    public class Window : SdlResource
    {
        private readonly IPanel contents;
        private readonly IntPtr renderer;
        public readonly RenderBatch rootBatch;
        public readonly uint id;

        public Window(IPanel contents)
        {
            this.contents = contents;
            _handle = SDL.SDL_CreateWindow("Factorio Calculator",
                SDL.SDL_WINDOWPOS_CENTERED,
                SDL.SDL_WINDOWPOS_CENTERED,
                1028,
                800,
                SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL
            );

            renderer = SDL.SDL_CreateRenderer(_handle, 0, SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);

            RenderingUtils.renderer = renderer;
            SDL.SDL_SetRenderDrawBlendMode(renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            rootBatch = new RenderBatch(contents);
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
            SDL.SDL_GetRendererOutputSize(renderer, out var w, out var h);
            var screenSize = new SizeF(RenderingUtils.PixelsToUnits(w), RenderingUtils.PixelsToUnits(h));
            if (rootBatch.dirty)
                rootBatch.Rebuild(screenSize);
            SDL.SDL_SetRenderDrawColor(renderer, 0,0,0, 255);
            SDL.SDL_RenderClear(renderer);
            rootBatch.Present(renderer, default, new RectangleF(default, screenSize));
            SDL.SDL_RenderPresent(renderer);
        }

        public T Raycast<T>(PointF position) where T : class, IMouseHandle => rootBatch.Raycast<T>(position);
    }
}