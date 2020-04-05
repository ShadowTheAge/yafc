using System;
using System.Collections.Generic;
using System.Drawing;
using SDL2;

namespace UI
{
    public sealed class RenderBatch
    {
        private readonly List<(RectangleF, SchemeColor, IMouseHandle)> rects = new List<(RectangleF, SchemeColor, IMouseHandle)>();
        private readonly List<(RectangleF, RectangleShadow)> shadows = new List<(RectangleF, RectangleShadow)>();
        private readonly List<(RectangleF, Sprite, SchemeColor)> sprites = new List<(RectangleF, Sprite, SchemeColor)>();
        private readonly List<(RectangleF, FontString)> texts = new List<(RectangleF, FontString)>();
        private readonly List<(RectangleF, RenderBatch, IMouseHandle)> subBatches = new List<(RectangleF, RenderBatch, IMouseHandle)>();

        public void Clear()
        {
            rects.Clear();
            shadows.Clear();
            sprites.Clear();
            texts.Clear();
        }
        
        public void DrawRectangle(RectangleF rect, SchemeColor color, RectangleShadow shadow = RectangleShadow.None, IMouseHandle mouseHandle = null)
        {
            rects.Add((rect, color, mouseHandle));
            if (shadow != RectangleShadow.None)
                shadows.Add((rect, shadow));
        }

        public void DrawSprite(RectangleF rect, Sprite sprite, SchemeColor color)
        {
            sprites.Add((rect, sprite, color));
        }

        public void DrawText(RectangleF rect, FontString text)
        {
            texts.Add((rect, text));
        }

        public void DrawSubBatch(RectangleF rect, RenderBatch batch, IMouseHandle handle = null)
        {
            subBatches.Add((rect, batch, handle));
        }

        public T Raycast<T>(PointF position) where T:class, IMouseHandle
        {
            for (var i = subBatches.Count - 1; i >= 0; i--)
            {
                var (rect, batch, handle) = subBatches[i];
                if (rect.Contains(position))
                {
                    var subcast = batch.Raycast<T>(position);
                    if (subcast != null)
                        return subcast;
                    if (handle is T t)
                        return t;
                }
            }

            foreach (var (rect, _, handle) in rects)
            {
                if (handle is T t && rect.Contains(position))
                    return t;
            }

            return null;
        }

        internal void Present(IntPtr renderer)
        {
            var currentColor = (SchemeColor) (-1);
            for (var i = rects.Count - 1; i >= 0; i--)
            {
                var (rect, color, handle) = rects[i];
                if (color == SchemeColor.None)
                    continue;
                if (color != currentColor)
                {
                    currentColor = color;
                    var sdlColor = currentColor.ToSdlColor();
                    SDL.SDL_SetRenderDrawColor(renderer, sdlColor.r, sdlColor.g, sdlColor.b, sdlColor.a);
                }
                var sdlRect = rect.ToSdlRect();
                SDL.SDL_RenderFillRect(renderer, ref sdlRect);
            }

            foreach (var shadow in shadows)
            {
                // TODO
            }

            currentColor = (SchemeColor) (-1);
            var atlasHandle = RenderingUtils.atlas.handle;
            
            foreach (var (pos, sprite, color) in sprites)
            {
                var rect = SpriteAtlas.SpriteToRect(sprite);
                var sdlpos = pos.ToSdlRect();
                if (currentColor != color)
                {
                    currentColor = color;
                    var sdlColor = currentColor.ToSdlColor();
                    SDL.SDL_SetTextureColorMod(atlasHandle, sdlColor.r, sdlColor.g, sdlColor.b);
                }
                SDL.SDL_RenderCopy(renderer, atlasHandle, ref rect, ref sdlpos);
            }

            foreach (var text in texts)
            {
                // TODO
            }

            foreach (var (rect, batch, handle) in subBatches)
                batch.Present(renderer);
        }
    }
}