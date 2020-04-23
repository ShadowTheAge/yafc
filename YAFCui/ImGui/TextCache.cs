using System;
using SDL2;

namespace YAFC.UI
{
    public class TextCache : ImGuiCache<TextCache, (FontFile.FontSize size, string text, uint wrapWidth)>, IRenderable
    {
        private IntPtr texture;
        private readonly IntPtr surface;
        internal SDL.SDL_Rect texRect;
        private SDL.SDL_Color curColor = RenderingUtils.White;

        private TextCache((FontFile.FontSize size, string text, uint wrapWidth) key)
        {
            surface = key.wrapWidth == uint.MaxValue
                ? SDL_ttf.TTF_RenderUNICODE_Blended(key.size.handle, key.text, RenderingUtils.White)
                : SDL_ttf.TTF_RenderUNICODE_Blended_Wrapped(key.size.handle, key.text, RenderingUtils.White, key.wrapWidth);
            
            if (key.wrapWidth == 0f)
            {
                ref var surfaceParams = ref RenderingUtils.AsSdlSurface(surface);
                texRect = new SDL.SDL_Rect {w = surfaceParams.w, h = surfaceParams.h};
            }
        }

        protected override TextCache CreateForKey((FontFile.FontSize size, string text, uint wrapWidth) key) => new TextCache(key);
        public override void Dispose()
        {
            if (surface != IntPtr.Zero)
                SDL.SDL_FreeSurface(surface);
            if (texture != IntPtr.Zero)
                SDL.SDL_DestroyTexture(texture);
        }

        public void Render(IntPtr renderer, SDL.SDL_Rect position, SDL.SDL_Color color)
        {
            if (texture == IntPtr.Zero)
            {
                texture = SDL.SDL_CreateTextureFromSurface(renderer, surface);
            }
            
            if (color.r != curColor.r || color.g != curColor.g || color.b != curColor.b)
                SDL.SDL_SetTextureColorMod(texture, color.r, color.g, color.b);
            if (color.a != curColor.a)
                SDL.SDL_SetTextureAlphaMod(texture, color.a);
            curColor = color;
            SDL.SDL_RenderCopy(renderer, texture, ref texRect, ref position);
        }
    }
}