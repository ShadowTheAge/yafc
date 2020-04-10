using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
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
        private InputSystem inputSystem;
        
        public bool quit { get; private set; }

        public Ui(IPanel rootPanel)
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
            rootBatch = new RenderBatch(rootPanel);
            inputSystem = new InputSystem(rootBatch);
            //rootBatch.DrawSprite(new RectangleF(8, 8, 10, 10), Sprite.Settings, SchemeColor.BackgroundText);
            //rootBatch.DrawRectangle(new RectangleF(7, 7, 12, 12), SchemeColor.Background);
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
                    case SDL.SDL_EventType.SDL_MOUSEBUTTONUP:
                        inputSystem.MouseUp(evt.button.button);
                        break;
                    case SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN:
                        inputSystem.MouseDown(evt.button.button);
                        break;
                    case SDL.SDL_EventType.SDL_MOUSEWHEEL:
                        inputSystem.MouseScroll(evt.wheel.y);
                        break;
                    case SDL.SDL_EventType.SDL_MOUSEMOTION:
                        inputSystem.MouseMove(evt.motion.x, evt.motion.y);
                        break;
                    case SDL.SDL_EventType.SDL_KEYDOWN:
                        inputSystem.KeyDown(evt.key.keysym);
                        break;
                    case SDL.SDL_EventType.SDL_KEYUP:
                        inputSystem.KeyUp(evt.key.keysym);
                        break;
                    case SDL.SDL_EventType.SDL_TEXTINPUT:
                        unsafe
                        {
                            var term = 0;
                            while (evt.text.text[term] != 0)
                                ++term;
                            var inputString = new string((sbyte*) evt.text.text, 0, term, Encoding.UTF8);
                            inputSystem.TextInput(inputString);
                        }
                        break;
                    case SDL.SDL_EventType.SDL_WINDOWEVENT:
                        switch (evt.window.windowEvent)
                        {
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_ENTER:
                                inputSystem.MouseEnterWindow();
                                break;
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_LEAVE:
                                inputSystem.MouseExitWindow();
                                break;
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_CLOSE:
                                quit = true;
                                break;
                            default:
                                Console.WriteLine("Window event of type "+evt.window.windowEvent);
                                break;
                        }
                        break;
                    case SDL.SDL_EventType.SDL_RENDER_TARGETS_RESET: break;
                    default:
                        Console.WriteLine("Event of type "+evt.type);
                        break;
                }
            }
            inputSystem.Update();
        }

        public SizeF ScreenSize()
        {
            SDL.SDL_GetRendererOutputSize(renderer, out var w, out var h);
            return new SizeF(w / RenderingUtils.pixelsPerUnit, h / RenderingUtils.pixelsPerUnit);
        }

        public void Render()
        {
            var screenSize = ScreenSize();
            if (rootBatch.dirty)
                rootBatch.Rebuild(screenSize);
            SDL.SDL_SetRenderDrawColor(renderer, 0,0,0, 255);
            SDL.SDL_RenderClear(renderer);
            rootBatch.Present(renderer, default, new RectangleF(default, screenSize));
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