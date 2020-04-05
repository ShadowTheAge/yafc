using System;
using System.Collections.Generic;
using System.Drawing;
using SDL2;

namespace UI
{
    public sealed class RenderBatch
    {
        private readonly List<(RectangleF, SchemeColor)> rects = new List<(RectangleF, SchemeColor)>();
        private readonly List<(RectangleF, RectangleShadow)> shadows = new List<(RectangleF, RectangleShadow)>();
        private readonly List<(RectangleF, Sprite)> sprites = new List<(RectangleF, Sprite)>();
        private readonly List<(RectangleF, FontString)> texts = new List<(RectangleF, FontString)>();
        private readonly List<(RectangleF, RenderBatch)> subBatches = new List<(RectangleF, RenderBatch)>();

        public void Clear()
        {
            rects.Clear();
            shadows.Clear();
            sprites.Clear();
            texts.Clear();
        }
        
        public void DrawRectangle(RectangleF rect, SchemeColor color, RectangleShadow shadow = RectangleShadow.None)
        {
            if (color != SchemeColor.None)
                rects.Add((rect, color));
            if (shadow != RectangleShadow.None)
                shadows.Add((rect, shadow));
        }

        public void DrawSprite(RectangleF rect, Sprite sprite)
        {
            sprites.Add((rect, sprite));
        }

        public void DrawText(RectangleF rect, FontString text)
        {
            texts.Add((rect, text));
        }

        public void DrawSubBatch(RectangleF rect, RenderBatch batch)
        {
            subBatches.Add((rect, batch));
        }

        internal void Present(IntPtr renderer)
        {
            var currentColor = (SchemeColor) (-1);
            for (var i = rects.Count - 1; i >= 0; i--)
            {
                var (rect, color) = rects[i];
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

            foreach (var sprite in sprites)
            {
                
            }

            foreach (var text in texts)
            {
                
            }

            foreach (var batch in subBatches)
                batch.Item2.Present(renderer);
        }
    }
}