using System;
using System.Drawing;
using System.Runtime.InteropServices;
using SDL2;

namespace UI
{
    public class Ui : IDisposable
    {
        [DllImport("SHCore.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwareness(int awareness);

        private IntPtr renderer;
        private IntPtr window;
        private RenderBatch rootBatch;
        
        public bool quit { get; private set; }

        public Ui()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                SetProcessDpiAwareness(2);
            
            SDL.SDL_Init(SDL.SDL_INIT_VIDEO);
            SDL.SDL_SetHint(SDL.SDL_HINT_RENDER_SCALE_QUALITY, "linear");
            window = SDL.SDL_CreateWindow("Factorio Calculator",
                SDL.SDL_WINDOWPOS_CENTERED,
                SDL.SDL_WINDOWPOS_CENTERED,
                1028,
                800,
                SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL
            );

            renderer = SDL.SDL_CreateRenderer(window, 0, SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
            
            SDL_ttf.TTF_Init();
            SDL_image.IMG_Init(SDL_image.IMG_InitFlags.IMG_INIT_PNG | SDL_image.IMG_InitFlags.IMG_INIT_JPG);

            RenderingUtils.renderer = renderer;
            RenderingUtils.atlas = new SpriteAtlas();
            rootBatch = new RenderBatch();
            rootBatch.DrawSprite(new RectangleF(8, 8, 10, 10), Sprite.Settings, SchemeColor.BackgroundText);
            rootBatch.DrawRectangle(new RectangleF(7, 7, 12, 12), SchemeColor.Background);
        }

        public void ProcessEvents()
        {
            while (SDL.SDL_PollEvent(out var evt) != 0)
            {
                switch (evt.type)
                {
                    case SDL.SDL_EventType.SDL_QUIT:
                        quit = true;
                        break;
                }
            }
        }

        public void Render()
        {
            SDL.SDL_SetRenderDrawColor(renderer, 0,0,0, 255);
            SDL.SDL_RenderClear(renderer);
            rootBatch.Present(renderer);
            SDL.SDL_RenderPresent(renderer);
        }
        
        
        public void Dispose()
        {
            SDL_ttf.TTF_Quit();
            SDL_image.IMG_Quit();
            SDL.SDL_DestroyWindow(window);
            SDL.SDL_Quit();
        }
    }
}