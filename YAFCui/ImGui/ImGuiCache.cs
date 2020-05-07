using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SDL2;

namespace YAFC.UI
{
    public abstract class ImGuiCache<T, TKey> : IDisposable where T:ImGuiCache<T, TKey> where TKey : IEquatable<TKey>
    {
        private static readonly T Constructor = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

        public class Cache : IDisposable
        {
            private readonly Dictionary<TKey, T> activeCached = new Dictionary<TKey, T>();
            private readonly HashSet<TKey> unused = new HashSet<TKey>();
            
            public T GetCached(TKey key)
            {
                if (activeCached.TryGetValue(key, out var value))
                {
                    unused.Remove(key);
                    return value;
                }

                return activeCached[key] = Constructor.CreateForKey(key);
            }

            public void PurgeUnused()
            {
                foreach (var key in unused)
                {
                    if (activeCached.Remove(key, out var value))
                        value.Dispose();
                }
                unused.Clear();
                unused.UnionWith(activeCached.Keys);
            }

            public void Dispose()
            {
                foreach (var item in activeCached)
                    item.Value.Dispose();
                activeCached.Clear();
                unused.Clear();
            }
        }


        protected abstract T CreateForKey(TKey key);
        public abstract void Dispose();
    }
    
    public class TextCache : ImGuiCache<TextCache, (FontFile.FontSize size, string text, uint wrapWidth)>, IRenderable
    {
        private IntPtr texture;
        private IntPtr surface;
        internal SDL.SDL_Rect texRect;
        private SDL.SDL_Color curColor = RenderingUtils.White;

        private TextCache((FontFile.FontSize size, string text, uint wrapWidth) key)
        {
            surface = key.wrapWidth == uint.MaxValue
                ? SDL_ttf.TTF_RenderUNICODE_Blended(key.size.handle, key.text, RenderingUtils.White)
                : SDL_ttf.TTF_RenderUNICODE_Blended_Wrapped(key.size.handle, key.text, RenderingUtils.White, key.wrapWidth);
            
            ref var surfaceParams = ref RenderingUtils.AsSdlSurface(surface);
            texRect = new SDL.SDL_Rect {w = surfaceParams.w, h = surfaceParams.h};
        }

        protected override TextCache CreateForKey((FontFile.FontSize size, string text, uint wrapWidth) key) => new TextCache(key);
        public override void Dispose()
        {
            if (surface != IntPtr.Zero)
            {
                SDL.SDL_FreeSurface(surface);
                surface = IntPtr.Zero;
            }

            if (texture != IntPtr.Zero)
            {
                SDL.SDL_DestroyTexture(texture);
                texture = IntPtr.Zero;
            }
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