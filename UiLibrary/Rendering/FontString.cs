using System;
using System.Drawing;
using SDL2;
using UI.TestLayout;

namespace UI
{
    public sealed class FontString : UnmanagedResource, IWidget
    {
        private readonly Font font;
        private readonly bool wrap;
        private float width, height;
        private string _text;

        public string text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    width = 0f;
                }
            }
        } 
        
        public FontString(Font font, bool wrap, string text)
        {
            this.font = font;
            this.wrap = wrap;
            _text = text;
        }

        protected override void ReleaseUnmanagedResources()
        {
            SDL.SDL_DestroyTexture(handle);
        }

        public RectangleF Build(RenderBatch batch, BuildLocation location)
        {
            var newWidth = location.width;
            if (width != newWidth)
            {
                width = newWidth;
                if (handle != IntPtr.Zero)
                    ReleaseUnmanagedResources();
                var surface = wrap
                    ? SDL_ttf.TTF_RenderUNICODE_Blended_Wrapped(font.handle, text, RenderingUtils.White, RenderingUtils.UnitsToPixels(width))
                    : SDL_ttf.TTF_RenderUTF8_Blended(font.handle, text, RenderingUtils.White);
                ref var surfaceParams = ref RenderingUtils.AsSdlSurface(surface);
                height = surfaceParams.h / RenderingUtils.pixelsPerUnit;
                handle = SDL.SDL_CreateTextureFromSurface(RenderingUtils.renderer, surface);
                SDL.SDL_FreeSurface(surface);
            }

            var rect = location.Rect(width, height);
            batch.DrawText(rect, this);
            return rect;
        }
    }
}