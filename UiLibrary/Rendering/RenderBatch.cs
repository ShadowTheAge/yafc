using System;
using System.Collections.Generic;
using System.Drawing;
using SDL2;

namespace UI
{
    public interface IRenderable
    {
        void Render(IntPtr renderer, RectangleF position);
    }
    
    public sealed class RenderBatch
    {
        private readonly IPanel panel;
        public SizeF offset { get; set; }
        public bool clip { get; set; }
        public bool dirty { get; private set; }
        private readonly List<(RectangleF, SchemeColor, IMouseHandle)> rects = new List<(RectangleF, SchemeColor, IMouseHandle)>();
        private readonly List<(RectangleF, RectangleShadow)> shadows = new List<(RectangleF, RectangleShadow)>();
        private readonly List<(RectangleF, Sprite, SchemeColor)> sprites = new List<(RectangleF, Sprite, SchemeColor)>();
        private readonly List<(RectangleF, IRenderable)> renderables = new List<(RectangleF, IRenderable)>();
        private readonly List<(RectangleF, RenderBatch, IMouseHandle)> subBatches = new List<(RectangleF, RenderBatch, IMouseHandle)>();
        private RenderBatch parent;

        public RenderBatch(IPanel panel)
        {
            this.panel = panel;
        }

        public void Clear()
        {
            rects.Clear();
            shadows.Clear();
            sprites.Clear();
            renderables.Clear();
        }

        public void Rebuild()
        {
            dirty = false;
            if (panel != null)
            {
                Clear();
                panel.Build(this);
            }
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

        public void DrawRenderable(RectangleF rect, IRenderable renderable)
        {
            renderables.Add((rect, renderable));
        }

        public void DrawSubBatch(RectangleF rect, RenderBatch batch, IMouseHandle handle = null)
        {
            batch.parent = this;
            subBatches.Add((rect, batch, handle));
        }

        public T Raycast<T>(PointF position) where T:class, IMouseHandle
        {
            position -= offset;
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

        internal void Present(IntPtr renderer, SizeF offset = default)
        {
            offset += this.offset;
            var currentColor = (SchemeColor) (-1);
            for (var i = rects.Count - 1; i >= 0; i--)
            {
                var (rect, color, _) = rects[i];
                if (color == SchemeColor.None)
                    continue;
                if (color != currentColor)
                {
                    currentColor = color;
                    var sdlColor = currentColor.ToSdlColor();
                    SDL.SDL_SetRenderDrawColor(renderer, sdlColor.r, sdlColor.g, sdlColor.b, sdlColor.a);
                }
                var sdlRect = rect.ToSdlRect(offset);
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
                var sdlpos = pos.ToSdlRect(offset);
                if (currentColor != color)
                {
                    currentColor = color;
                    var sdlColor = currentColor.ToSdlColor();
                    SDL.SDL_SetTextureColorMod(atlasHandle, sdlColor.r, sdlColor.g, sdlColor.b);
                }
                SDL.SDL_RenderCopy(renderer, atlasHandle, ref rect, ref sdlpos);
            }

            foreach (var (pos, renderable) in renderables)
                renderable.Render(renderer, new RectangleF(pos.Location + offset, pos.Size));

            foreach (var (rect, batch, _) in subBatches)
            {
                if (batch.clip)
                {
                    SDL.SDL_RenderGetClipRect(renderer, out var prevClip);
                    var clipRect = rect.ToSdlRect(offset);
                    SDL.SDL_RenderSetClipRect(renderer, ref clipRect);
                    batch.Present(renderer, offset);
                    SDL.SDL_RenderSetClipRect(renderer, ref prevClip);
                } else
                    batch.Present(renderer, offset);
            }
        }

        public void SetDirty()
        {
            if (!dirty)
            {
                dirty = true;
                parent?.SetDirty();
            }
        }
    }
}