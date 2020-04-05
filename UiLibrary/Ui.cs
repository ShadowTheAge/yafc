using System;
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
        
        public bool quit { get; private set; }

        public Ui()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                SetProcessDpiAwareness(2);
            
            SDL.SDL_Init(SDL.SDL_INIT_VIDEO);
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
            var rect = new SDL.SDL_Rect {x = 10, y = 10, w = 100, h = 100};
            SDL.SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);
            SDL.SDL_RenderDrawRect(renderer, ref rect);
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