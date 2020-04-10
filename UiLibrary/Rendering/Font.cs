using System;
using System.Drawing;
using SDL2;

namespace UI
{
    public class Font : UnmanagedResource
    {
        private readonly string fontName;
        private readonly int baseFontSize;
        
        public Font(string name, int size)
        {
            fontName = name;
            baseFontSize = size;
        }

        public IntPtr GetFontHandle()
        {
            if (_handle == IntPtr.Zero)
                _handle = SDL_ttf.TTF_OpenFont(fontName, baseFontSize);
            return _handle;
        }

        protected override void ReleaseUnmanagedResources()
        {
            SDL_ttf.TTF_CloseFont(_handle);
        }

        public SizeF Measure(string str)
        {
            SDL_ttf.TTF_SizeUNICODE(GetFontHandle(), str, out var w, out var h);
            return new SizeF(w, h);
        }
    }
}