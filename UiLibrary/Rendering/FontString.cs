using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SDL2;

namespace UI
{
    public sealed class FontString : UnmanagedResource
    {
        private readonly Font font;
        private readonly bool wrap;
        public string text { get; private set; }
        public SizeF size { get; private set; }
        private static readonly SDL.SDL_Color Black = new SDL.SDL_Color {a = 255};
        
        public FontString(Font font, bool wrap)
        {
            this.font = font;
            this.wrap = wrap;
        }

        public void Update(string text, float width)
        {
            this.text = text;
            var surface = wrap
                ? SDL_ttf.TTF_RenderUNICODE_Blended_Wrapped(font.handle, text, Black, RenderingUtils.UnitsToPixels(width))
                : SDL_ttf.TTF_RenderUTF8_Blended(font.handle, text, Black);
            ref var surfaceParams = ref RenderingUtils.AsSdlSurface(surface);
            size = new SizeF(width, surfaceParams.h / RenderingUtils.pixelsPerUnit);

            if (handle != IntPtr.Zero)
                SDL.SDL_DestroyTexture(handle);
            handle = SDL.SDL_CreateTextureFromSurface(RenderingUtils.renderer, surface);
            SDL.SDL_FreeSurface(surface);
        }

        protected override void ReleaseUnmanagedResources()
        {
            SDL.SDL_DestroyTexture(handle);
        }
    }
}