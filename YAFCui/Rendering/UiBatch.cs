using System;
using System.Collections.Generic;
using System.Drawing;
using SDL2;

namespace YAFC.UI
{
    public interface IRenderable
    {
        void Render(IntPtr renderer, SDL.SDL_Rect position);
    }
    
    public sealed class UiBatch
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

        private long nextRebuildTimer = long.MaxValue;
        public float pixelsPerUnit { get; private set; } = 20;
        public bool clip { get; set; }
        private bool rebuildRequested = true;
        private readonly List<(RectangleF, SchemeColor, IMouseHandle)> rects = new List<(RectangleF, SchemeColor, IMouseHandle)>();
        private readonly List<(RectangleF, RectangleBorder)> borders = new List<(RectangleF, RectangleBorder)>();
        private readonly List<(RectangleF, Icon, SchemeColor)> icons = new List<(RectangleF, Icon, SchemeColor)>();
        private readonly List<(RectangleF, IRenderable)> renderables = new List<(RectangleF, IRenderable)>();
        private readonly List<(RectangleF, UiBatch, IMouseHandle)> subBatches = new List<(RectangleF, UiBatch, IMouseHandle)>();
        private UiBatch parent;
        public Window window { get; private set; }

        public SizeF size => contentSize;

        public ushort UnitsToPixels(float units) => (ushort) MathF.Round(units * pixelsPerUnit);
        public float PixelsToUnits(int pixels) => pixels / pixelsPerUnit;

        public SDL.SDL_Rect ToSdlRect(RectangleF rect, SizeF offset = default)
        {
            return new SDL.SDL_Rect
            {
                x = UnitsToPixels(rect.X + offset.Width),
                y = UnitsToPixels(rect.Y + offset.Height),
                w = UnitsToPixels(rect.Width),
                h = UnitsToPixels(rect.Height)
            };
        }

        public bool IsRebuildRequired() => rebuildRequested || Ui.time >= nextRebuildTimer;


        public T FindOwner<T>() where T:class, IPanel => panel is T t ? t : parent?.FindOwner<T>();

        public UiBatch(IPanel panel)
        {
            this.panel = panel;
        }

        public void Clear()
        {
            nextRebuildTimer = long.MaxValue;
            rects.Clear();
            borders.Clear();
            icons.Clear();
            renderables.Clear();
            subBatches.Clear();
        }

        internal void Rebuild(Window window, SizeF size, float pixelsPerUnit)
        {
            this.window = window;
            rebuildRequested = false;
            this.pixelsPerUnit = pixelsPerUnit;
            if (panel != null)
            {
                Clear();
                var state = new LayoutState(this, size.Width, panel.defaultAllocator);
                panel.BuildPanel(state);
                contentSize = new SizeF(size.Width, state.fullHeight);
            }
        }
        
        public void DrawRectangle(RectangleF rect, SchemeColor color, RectangleBorder border = RectangleBorder.None, IMouseHandle mouseHandle = null)
        {
            rects.Add((rect, color, mouseHandle));
            if (border != RectangleBorder.None)
                borders.Add((rect, border));
        }

        public void DrawIcon(RectangleF rect, Icon icon, SchemeColor color)
        {
            icons.Add((rect, icon, color));
        }

        public void DrawRenderable(RectangleF rect, IRenderable renderable)
        {
            renderables.Add((rect, renderable));
        }

        public void DrawSubBatch(RectangleF rect, UiBatch batch, IMouseHandle handle = null)
        {
            batch.parent = this;
            subBatches.Add((rect, batch, handle));
            if (batch.IsRebuildRequired())
                batch.Rebuild(window, rect.Size, pixelsPerUnit);
        }

        public bool Raycast<T>(PointF position, out T result, out UiBatch resultBatch) where T:class, IMouseHandle
        {
            position -= offset;
            for (var i = subBatches.Count - 1; i >= 0; i--)
            {
                var (rect, batch, handle) = subBatches[i];
                if (rect.Contains(position))
                {
                    if (batch.Raycast(new PointF(position.X - rect.X, position.Y - rect.Y), out result, out resultBatch))
                        return true;
                    if (handle is T t)
                    {
                        result = t;
                        resultBatch = this;
                        return true;
                    }

                    return false;
                }
            }

            foreach (var (rect, _, handle) in rects)
            {
                if (handle is T t && rect.Contains(position))
                {
                    result = t;
                    resultBatch = this;
                    return true;
                }
            }

            result = null;
            resultBatch = null;
            return false;
        }

        internal void Present(Window window, SizeF screenOffset, RectangleF screenClip)
        {
            this.window = window;
            var renderer = window.renderer;
            SDL.SDL_Rect prevClip = default;
            if (clip)
            {
                SDL.SDL_RenderGetClipRect(renderer, out prevClip);
                var clipRect = ToSdlRect(screenClip);
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
                var sdlRect = ToSdlRect(rect, screenOffset);
                SDL.SDL_RenderFillRect(renderer, ref sdlRect);
            }
            
            foreach (var (rect, type) in borders)
            {
                var sdlRect = ToSdlRect(rect, screenOffset);
                window.DrawBorder(sdlRect, type);
            }
            
            foreach (var (pos, icon, color) in icons)
            {
                if (!pos.IntersectsWith(localClip))
                    continue;
                var sdlpos = ToSdlRect(pos, screenOffset);
                window.DrawIcon(sdlpos, icon, color);
            }

            foreach (var (pos, renderable) in renderables)
            {
                if (!pos.IntersectsWith(localClip))
                    continue;
                renderable.Render(renderer, ToSdlRect(pos, screenOffset));
            }

            foreach (var (rect, batch, _) in subBatches)
            {
                var screenRect = new RectangleF(rect.Location + screenOffset, rect.Size);
                var intersection = RectangleF.Intersect(screenRect, localClip);
                if (intersection.IsEmpty)
                    continue;

                if (batch.IsRebuildRequired() || batch.buildSize != rect.Size)
                {
                    batch.buildSize = rect.Size;
                    batch.Rebuild(window, rect.Size, pixelsPerUnit);
                }
                batch.Present(window, new SizeF(screenRect.X, screenRect.Y), intersection);
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

        private static void CheckMainThread()
        {
            if (!Ui.IsMainThread())
                throw new NotSupportedException("This should be called from the main thread");
        }

        public void Rebuild()
        {
            if (!rebuildRequested)
            {
                CheckMainThread();
                rebuildRequested = true;
                window?.Repaint();
            }
        }

        public void MarkEverythingForRebuild()
        {
            CheckMainThread();
            rebuildRequested = true;
            foreach (var (_, batch, _) in subBatches)
                batch.MarkEverythingForRebuild();
        }

        public void SetNextRebuild(long nextRebuildTime)
        {
            if (nextRebuildTime < nextRebuildTimer)
            {
                CheckMainThread();
                nextRebuildTimer = nextRebuildTime;
                window.SetNextRepaint(nextRebuildTime);
            }
        }
    }
}