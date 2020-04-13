using System;
using System.Drawing;
using SDL2;

namespace UI
{
    public class Font : SdlResource
    {
        public static Font header;
        public static Font subheader;
        public static Font text;
        
        public readonly float size;
        public readonly float lineSize; 

        public Font(string name, float size)
        {
            this.size = size;
            _handle = SDL_ttf.TTF_OpenFont(name, RenderingUtils.UnitsToPixels(size));
            lineSize = RenderingUtils.PixelsToUnits(SDL_ttf.TTF_FontLineSkip(_handle));
            SDL_ttf.TTF_SetFontKerning(_handle, 1);
        }

        public IntPtr GetFontHandle()
        {
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