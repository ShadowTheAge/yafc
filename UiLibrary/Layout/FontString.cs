using System;
using System.Drawing;
using SDL2;

namespace UI
{
    public sealed class FontString : UnmanagedResource, IWidget, IRenderable
    {
        private readonly Font font;
        private readonly bool wrap;
        private int texWidth, texHeight;
        private float width, height;
        private string _text;
        private RenderBatch batch;

        public string text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    width = 0f;
                    batch?.SetDirty();
                }
            }
        } 
        
        public FontString(Font font, bool wrap, string text = null)
        {
            this.font = font;
            this.wrap = wrap;
            _text = text;
        }

        protected override void ReleaseUnmanagedResources()
        {
            batch = null;
            SDL.SDL_DestroyTexture(handle);
        }

        public RectangleF Build(RenderBatch batch, LayoutPosition location, Alignment align)
        {
            this.batch = batch;
            var newWidth = location.width;
            if (width != newWidth)
            {
                width = newWidth;
                if (handle != IntPtr.Zero)
                    ReleaseUnmanagedResources();
                var surface = wrap
                    ? SDL_ttf.TTF_RenderUNICODE_Blended_Wrapped(font.handle, text, RenderingUtils.White, RenderingUtils.UnitsToPixels(width))
                    : SDL_ttf.TTF_RenderUNICODE_Blended(font.handle, text, RenderingUtils.White);
                ref var surfaceParams = ref RenderingUtils.AsSdlSurface(surface);
                texWidth = surfaceParams.w;
                texHeight = surfaceParams.h;
                height = surfaceParams.h / RenderingUtils.pixelsPerUnit;
                handle = SDL.SDL_CreateTextureFromSurface(RenderingUtils.renderer, surface);
                SDL.SDL_FreeSurface(surface);
            }

            var rect = location.Rect(width, height, align);
            batch.DrawRenderable(rect, this);
            return rect;
        }

        public void Render(IntPtr renderer, RectangleF position)
        {
            var rect = new SDL.SDL_Rect {w = texWidth, h = texHeight};
            var destRect = position.ToSdlRect();
            SDL.SDL_RenderCopy(renderer, handle, ref rect, ref destRect);
        }
    }
}