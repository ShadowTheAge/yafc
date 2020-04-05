using System.Drawing;
using SDL2;

namespace UI
{
    public class Font : UnmanagedResource
    {
        public Font(string name, int size)
        {
            handle = SDL_ttf.TTF_OpenFont(name, size);
        }

        protected override void ReleaseUnmanagedResources()
        {
            SDL_ttf.TTF_CloseFont(handle);
        }

        public SizeF Measure(string str)
        {
            SDL_ttf.TTF_SizeUNICODE(handle, str, out var w, out var h);
            return new SizeF(w, h);
        }
    }
}