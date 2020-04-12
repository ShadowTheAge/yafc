using System;
using System.Runtime.InteropServices;
using SDL2;

namespace UI
{
    public abstract class UnmanagedResource : IDisposable
    {
        protected IntPtr _handle;
        
        protected abstract void ReleaseUnmanagedResources();

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                ReleaseUnmanagedResources();
                _handle = IntPtr.Zero;
                GC.SuppressFinalize(this);
            }
        }

        ~UnmanagedResource()
        {
            ReleaseUnmanagedResources();
        }
    }

    public abstract class SdlResource : UnmanagedResource
    {
        [DllImport("SHCore.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwareness(int awareness);
        
        static SdlResource()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                SetProcessDpiAwareness(2);
            
            SDL.SDL_Init(SDL.SDL_INIT_VIDEO);
            SDL.SDL_SetHint(SDL.SDL_HINT_RENDER_SCALE_QUALITY, "linear");
            SDL_ttf.TTF_Init();
            SDL_image.IMG_Init(SDL_image.IMG_InitFlags.IMG_INIT_PNG | SDL_image.IMG_InitFlags.IMG_INIT_JPG);
        }
    }
}