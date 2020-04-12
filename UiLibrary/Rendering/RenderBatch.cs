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
        private SizeF buildSize;
        private SizeF contentSize;
        private readonly IPanel panel;
        private SizeF _offset;

        public SizeF offset
        {
            get => _offset;
            set
            {
                _offset = value;
                Repaint();
            }
        }

        public bool clip { get; set; }
        public bool rebuildRequired { get; private set; } = true;
        private readonly List<(RectangleF, SchemeColor, IMouseHandle)> rects = new List<(RectangleF, SchemeColor, IMouseHandle)>();
        private readonly List<(RectangleF, RectangleShadow)> shadows = new List<(RectangleF, RectangleShadow)>();
        private readonly List<(RectangleF, Icon, SchemeColor)> icons = new List<(RectangleF, Icon, SchemeColor)>();
        private readonly List<(RectangleF, IRenderable)> renderables = new List<(RectangleF, IRenderable)>();
        private readonly List<(RectangleF, RenderBatch, IMouseHandle)> subBatches = new List<(RectangleF, RenderBatch, IMouseHandle)>();
        private RenderBatch parent;
        private Window window;

        public RenderBatch(IPanel panel)
        {
            this.panel = panel;
        }

        public void Clear()
        {
            rects.Clear();
            shadows.Clear();
            icons.Clear();
            renderables.Clear();
            subBatches.Clear();
        }

        public void Rebuild(SizeF size)
        {
            rebuildRequired = false;
            if (panel != null)
            {
                Clear();
                var contentBottom = panel.BuildPanel(this, new LayoutPosition(size.Width));
                contentSize = new SizeF(contentBottom.right, contentBottom.y);
            }
        }
        
        public void DrawRectangle(RectangleF rect, SchemeColor color, RectangleShadow shadow = RectangleShadow.None, IMouseHandle mouseHandle = null)
        {
            rects.Add((rect, color, mouseHandle));
            if (shadow != RectangleShadow.None)
                shadows.Add((rect, shadow));
        }

        public void DrawIcon(RectangleF rect, Icon icon, SchemeColor color)
        {
            icons.Add((rect, icon, color));
        }

        public void DrawRenderable(RectangleF rect, IRenderable renderable)
        {
            renderables.Add((rect, renderable));
        }

        public void DrawSubBatch(RectangleF rect, RenderBatch batch, IMouseHandle handle = null)
        {
            batch.parent = this;
            subBatches.Add((rect, batch, handle));
            if (batch.rebuildRequired)
                batch.Rebuild(rect.Size);
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

        internal void Present(Window window, SizeF screenOffset, RectangleF screenClip)
        {
            this.window = window;
            var renderer = window.renderer;
            SDL.SDL_Rect prevClip = default;
            if (clip)
            {
                SDL.SDL_RenderGetClipRect(renderer, out prevClip);
                var clipRect = screenClip.ToSdlRect();
                SDL.SDL_RenderSetClipRect(renderer, ref clipRect);
            }
            screenOffset += offset;
            var localClip = new RectangleF(screenClip.Location - screenOffset, screenClip.Size);
            var currentColor = (SchemeColor) (-1);
            for (var i = rects.Count - 1; i >= 0; i--)
            {
                var (rect, color, _) = rects[i];
                if (color == SchemeColor.None || !rect.IntersectsWith(localClip))
                    continue;
                if (color != currentColor)
                {
                    currentColor = color;
                    var sdlColor = currentColor.ToSdlColor();
                    SDL.SDL_SetRenderDrawColor(renderer, sdlColor.r, sdlColor.g, sdlColor.b, sdlColor.a);
                }
                var sdlRect = rect.ToSdlRect(screenOffset);
                SDL.SDL_RenderFillRect(renderer, ref sdlRect);
            }

            foreach (var shadow in shadows)
            {
                // TODO
            }
            
            foreach (var (pos, icon, color) in icons)
            {
                if (!pos.IntersectsWith(localClip))
                    continue;
                var sdlpos = pos.ToSdlRect(screenOffset);
                window.DrawIcon(sdlpos, icon, color);
            }

            foreach (var (pos, renderable) in renderables)
            {
                if (!pos.IntersectsWith(localClip))
                    continue;
                renderable.Render(renderer, new RectangleF(pos.Location + screenOffset, pos.Size));
            }

            foreach (var (rect, batch, _) in subBatches)
            {
                var screenRect = new RectangleF(rect.Location + screenOffset, rect.Size);
                var intersection = RectangleF.Intersect(screenRect, localClip);
                if (intersection.IsEmpty)
                    continue;

                if (batch.rebuildRequired || batch.buildSize != rect.Size)
                {
                    batch.buildSize = rect.Size;
                    batch.Rebuild(rect.Size);
                }
                batch.Present(window, screenOffset + new SizeF(screenRect.X, screenRect.Y), intersection);
            }

            if (clip)
            {
                if (prevClip.w == 0)
                    SDL.SDL_RenderSetClipRect(renderer, IntPtr.Zero);
                else SDL.SDL_RenderSetClipRect(renderer, ref prevClip);
            }
        }

        public void Repaint()
        {
            window?.Repaint();
        }

        public void Rebuild()
        {
            if (!rebuildRequired)
            {
                rebuildRequired = true;
                window?.Repaint();
            }
        }
    }
}