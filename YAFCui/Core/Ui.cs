using System;
using System.Collections.Generic;
using System.Text;
using SDL2;

namespace YAFC.UI
{
    public static class Ui
    {
        public static bool quit { get; private set; }

        private static Dictionary<uint, Window> windows = new Dictionary<uint, Window>();
        internal static void RegisterWindow(uint id, Window window)
        {
            windows[id] = window;
        }

        public static void ProcessEvents()
        {
            try
            {
                var inputSystem = InputSystem.Instance;
                var minNextEvent = long.MaxValue - 1;
                foreach (var (_, window) in windows)
                    minNextEvent = Math.Min(minNextEvent, window.nextRepaintTime);
                var delta = Math.Min(1 + (minNextEvent - InputSystem.time), int.MaxValue);
                var hasEvents = SDL.SDL_WaitEventTimeout(out var evt, (int) delta) != 0;
                inputSystem.UpdateTime();
                while (hasEvents)
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
                            var y = -evt.wheel.y;
                            if (evt.wheel.direction == (uint) SDL.SDL_MouseWheelDirection.SDL_MOUSEWHEEL_FLIPPED)
                                y = -y;
                            inputSystem.MouseScroll(y);
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
                            var window = windows[evt.window.windowID];
                            switch (evt.window.windowEvent)
                            {
                                case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_ENTER:
                                    inputSystem.MouseEnterWindow(window);
                                    break;
                                case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_LEAVE:
                                    inputSystem.MouseExitWindow(window);
                                    break;
                                case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_CLOSE:
                                    window.Close();
                                    break;
                                case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_LOST:
                                    inputSystem.SetKeyboardFocus(null);
                                    window.FocusLost();
                                    break;
                                case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_MOVED:
                                    window.WindowMoved();
                                    break;
                                default:
                                    Console.WriteLine("Window event of type " + evt.window.windowEvent);
                                    break;
                            }

                            break;
                        case SDL.SDL_EventType.SDL_RENDER_TARGETS_RESET: break;
                        default:
                            Console.WriteLine("Event of type " + evt.type);
                            break;
                    }

                    hasEvents = SDL.SDL_PollEvent(out evt) != 0;
                }
                inputSystem.Update();
            }
            catch (Exception ex)
            {
                ExceptionScreen.ShowException(ex);
            }
        }

        public static void Render()
        {
            foreach (var (_, window) in windows)
            {
                try
                {
                    window.Render();
                }
                catch (Exception ex)
                {
                    ExceptionScreen.ShowException(ex);
                }
            }
        }
        
        
        public static void Quit()
        {
            quit = true;
            SDL_ttf.TTF_Quit();
            SDL_image.IMG_Quit();
            SDL.SDL_Quit();
        }
    }
}