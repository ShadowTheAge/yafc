using System;
using System.Drawing;
using SDL2;

namespace UI
{
    public sealed class FontString : SdlResource, IWidget, IRenderable
    {
        public readonly Font font;
        public readonly bool wrap;
        private IntPtr texture;
        private int texWidth, texHeight;
        private float containerWidth;
        public SizeF textSize { get; private set; }
        private string _text;
        private RenderBatch batch;
        private SchemeColor _color;
        private readonly Alignment align;
        private bool transparent;

        public void SetTransparent(bool value)
        {
            if (transparent == value)
                return;
            transparent = value;
            if (texture != IntPtr.Zero)
                SDL.SDL_SetTextureAlphaMod(texture, transparent ? (byte)100 : (byte)255);
            batch?.SetDirty();
        }

        public SchemeColor color
        {
            get => _color;
            set {
                if (_color != value)
                {
                    _color = value;
                    if (texture != IntPtr.Zero)
                    {
                        var sdlColor = value.ToSdlColor();
                        SDL.SDL_SetTextureColorMod(texture, sdlColor.r, sdlColor.g, sdlColor.b);
                    }
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

        public FontString(Font font, string text = null, bool wrap = false, Alignment align = Alignment.Left, SchemeColor color = SchemeColor.BackgroundText)
        {
            this.font = font;
            this.wrap = wrap;
            this.align = align;
            _color = color;
            _text = text;
        }

        protected override void ReleaseUnmanagedResources()
        {
            batch = null;
            SDL.SDL_FreeSurface(_handle);
            _handle = IntPtr.Zero;
            if (texture != IntPtr.Zero)
            {
                SDL.SDL_DestroyTexture(texture);
                texture = IntPtr.Zero;
            }
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
                    _handle = wrap
                        ? SDL_ttf.TTF_RenderUNICODE_Blended_Wrapped(font.GetFontHandle(), text, RenderingUtils.White, RenderingUtils.UnitsToPixels(containerWidth))
                        : SDL_ttf.TTF_RenderUNICODE_Blended(font.GetFontHandle(), text, RenderingUtils.White);
                    ref var surfaceParams = ref RenderingUtils.AsSdlSurface(_handle);
                    texWidth = surfaceParams.w;
                    texHeight = surfaceParams.h;
                    textSize = new SizeF(surfaceParams.w / RenderingUtils.pixelsPerUnit, surfaceParams.h / RenderingUtils.pixelsPerUnit);
                }
                else
                {
                    textSize = new SizeF(0f, font.lineSize);
                }
            }
            this.batch = batch;

            var rect = location.IntoRect(textSize.Width, textSize.Height, align);
            if (_handle != IntPtr.Zero)
                batch.DrawRenderable(rect, this);
            return location;
        }

        public void Render(IntPtr renderer, RectangleF position)
        {
            if (texture == IntPtr.Zero)
            {
                texture = SDL.SDL_CreateTextureFromSurface(RenderingUtils.renderer, _handle);
                SDL.SDL_GetTextureColorMod(texture, out var r, out var g, out var b);
                var sdlColor = _color.ToSdlColor(); 
                SDL.SDL_SetTextureColorMod(texture, sdlColor.r, sdlColor.b, sdlColor.b);
                if (transparent)
                    SDL.SDL_SetTextureAlphaMod(texture, 100);
            }

            var destRect = position.ToSdlRect();
            var w = Math.Min(destRect.w, texWidth);
            var h = Math.Min(destRect.h, texHeight);
            var rect = new SDL.SDL_Rect {w = w, h = h};
            destRect.w = w;
            destRect.h = h;
            SDL.SDL_RenderCopy(renderer, texture, ref rect, ref destRect);
        }
    }
}