using System;
using System.Drawing;
using SDL2;

namespace YAFC.UI
{
    public class FontString : UnmanagedResource, IWidget, IRenderable, IListView<string>
    {
        public readonly Font font;
        public readonly bool wrap;
        public readonly RectAlignment align;
        private IntPtr texture;
        private int texWidth, texHeight;
        private int pixelWidth = -1;
        public SizeF textSize { get; private set; }
        private string _text;
        private UiBatch batch;
        private SchemeColor _color;
        private bool transparent;

        public void SetTransparent(bool value)
        {
            if (transparent == value)
                return;
            transparent = value;
            if (texture != IntPtr.Zero)
                SDL.SDL_SetTextureAlphaMod(texture, transparent ? (byte)100 : (byte)255);
            batch?.Rebuild();
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
                    batch?.Rebuild();
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
                    pixelWidth = -1;
                    batch?.Rebuild();
                }
            }
        }

        public FontString(Font font, string text = null, bool wrap = false, SchemeColor color = SchemeColor.BackgroundText, RectAlignment align = RectAlignment.MiddleLeft)
        {
            this.font = font;
            this.wrap = wrap;
            this.align = align;
            _color = color;
            _text = text;
        }

        public FontString() : this(Font.text) {}

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

        public void Build(LayoutState state)
        {
            var newPixelWidth = state.batch.UnitsToPixels(state.width);
            if (newPixelWidth != pixelWidth)
            {
                pixelWidth = newPixelWidth;
                if (_handle != IntPtr.Zero)
                    ReleaseUnmanagedResources();
                if (!string.IsNullOrEmpty(text))
                {
                    _handle = wrap
                        ? SDL_ttf.TTF_RenderUNICODE_Blended_Wrapped(font.GetHandle(state.batch), text, RenderingUtils.White, newPixelWidth)
                        : SDL_ttf.TTF_RenderUNICODE_Blended(font.GetHandle(state.batch), text, RenderingUtils.White);
                    ref var surfaceParams = ref RenderingUtils.AsSdlSurface(_handle);
                    texWidth = surfaceParams.w;
                    texHeight = surfaceParams.h;
                    textSize = new SizeF(surfaceParams.w / state.batch.pixelsPerUnit, surfaceParams.h / state.batch.pixelsPerUnit);
                }
                else
                {
                    textSize = new SizeF(0f, font.GetLineSize(state.batch));
                }
            }
            batch = state.batch;

            var rect = state.AllocateRect(textSize.Width, textSize.Height, align);
            if (_handle != IntPtr.Zero)
                batch.DrawRenderable(rect, this);
        }

        public void Render(IntPtr renderer, SDL.SDL_Rect position)
        {
            if (texture == IntPtr.Zero)
            {
                texture = SDL.SDL_CreateTextureFromSurface(renderer, _handle);
                var sdlColor = _color.ToSdlColor(); 
                SDL.SDL_SetTextureColorMod(texture, sdlColor.r, sdlColor.g, sdlColor.b);
                if (transparent)
                    SDL.SDL_SetTextureAlphaMod(texture, 100);
            }

            var w = Math.Min(position.w, texWidth);
            var h = Math.Min(position.h, texHeight);
            var rect = new SDL.SDL_Rect {w = w, h = h};
            position.w = w;
            position.h = h;
            SDL.SDL_RenderCopy(renderer, texture, ref rect, ref position);
        }

        public void BuildElement(string element, LayoutState state)
        {
            if (element != _text)
            {
                _text = element;
                pixelWidth = -1;
            }
            Build(state);
        }
    }
}