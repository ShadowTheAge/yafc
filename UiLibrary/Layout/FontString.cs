using System;
using System.Drawing;
using SDL2;

namespace UI
{
    public sealed class FontString : SdlResource, IWidget, IRenderable
    {
        public readonly Font font;
        public readonly bool wrap;
        private int texWidth, texHeight;
        private float containerWidth;
        public SizeF textSize { get; private set; }
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
                    containerWidth = -1f;
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
            var newWidth = location.width;
            if (containerWidth != newWidth)
            {
                containerWidth = newWidth;
                if (_handle != IntPtr.Zero)
                    ReleaseUnmanagedResources();
                if (!string.IsNullOrEmpty(text))
                {
                    var surface = wrap
                        ? SDL_ttf.TTF_RenderUNICODE_Blended_Wrapped(font.GetFontHandle(), text, RenderingUtils.White, RenderingUtils.UnitsToPixels(containerWidth))
                        : SDL_ttf.TTF_RenderUNICODE_Blended(font.GetFontHandle(), text, RenderingUtils.White);
                    ref var surfaceParams = ref RenderingUtils.AsSdlSurface(surface);
                    texWidth = surfaceParams.w;
                    texHeight = surfaceParams.h;
                    textSize = new SizeF(surfaceParams.w / RenderingUtils.pixelsPerUnit, surfaceParams.h / RenderingUtils.pixelsPerUnit);
                    _handle = SDL.SDL_CreateTextureFromSurface(RenderingUtils.renderer, surface);
                    SDL.SDL_FreeSurface(surface);
                }
                else
                {
                    textSize = new SizeF(0f, font.lineSize);
                }
            }
            this.batch = batch;
            
            SDL.SDL_GetTextureColorMod(_handle, out var r, out var g, out var b);
            var sdlColor = _color.ToSdlColor();
            if (sdlColor.r != r || sdlColor.g != g || sdlColor.b != b)
                SDL.SDL_SetTextureColorMod(_handle, sdlColor.r, sdlColor.b, sdlColor.b);

            var rect = location.IntoRect(textSize.Width, textSize.Height);
            if (_handle != null)
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