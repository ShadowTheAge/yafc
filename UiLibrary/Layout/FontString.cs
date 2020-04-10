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
        private SchemeColor _color;

        public SchemeColor color
        {
            get => _color;
            set {
                if (_color != value)
                {
                    _color = value;
                    batch?.SetDirty();
                }
            }
        }

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
        
        public FontString(Font font, bool wrap, string text = null, SchemeColor color = SchemeColor.BackgroundText)
        {
            this.font = font;
            this.wrap = wrap;
            this._color = color;
            _text = text;
        }

        protected override void ReleaseUnmanagedResources()
        {
            batch = null;
            SDL.SDL_DestroyTexture(_handle);
        }

        public LayoutPosition Build(RenderBatch batch, LayoutPosition location)
        {
            this.batch = batch;
            var newWidth = location.width;
            if (width != newWidth && text != null)
            {
                width = newWidth;
                if (_handle != IntPtr.Zero)
                    ReleaseUnmanagedResources();
                var surface = wrap
                    ? SDL_ttf.TTF_RenderUNICODE_Blended_Wrapped(font.GetFontHandle(), text, RenderingUtils.White, RenderingUtils.UnitsToPixels(width))
                    : SDL_ttf.TTF_RenderUNICODE_Blended(font.GetFontHandle(), text, RenderingUtils.White);
                ref var surfaceParams = ref RenderingUtils.AsSdlSurface(surface);
                texWidth = surfaceParams.w;
                texHeight = surfaceParams.h;
                height = surfaceParams.h / RenderingUtils.pixelsPerUnit;
                _handle = SDL.SDL_CreateTextureFromSurface(RenderingUtils.renderer, surface);
                SDL.SDL_FreeSurface(surface);
            }
            
            SDL.SDL_GetTextureColorMod(_handle, out var r, out var g, out var b);
            var sdlColor = _color.ToSdlColor();
            if (sdlColor.r != r || sdlColor.g != g || sdlColor.b != b)
                SDL.SDL_SetTextureColorMod(_handle, sdlColor.r, sdlColor.b, sdlColor.b);

            var rect = location.IntoRect(width, height);
            batch.DrawRenderable(rect, this);
            return location;
        }

        public void Render(IntPtr renderer, RectangleF position)
        {
            var rect = new SDL.SDL_Rect {w = texWidth, h = texHeight};
            var destRect = position.ToSdlRect();
            destRect.w = rect.w;
            destRect.h = rect.h;
            SDL.SDL_RenderCopy(renderer, _handle, ref rect, ref destRect);
        }
    }
}