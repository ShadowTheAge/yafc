using System;
using System.Collections.Generic;
using System.Numerics;
using SDL2;

namespace YAFC.UI
{
    public interface IRenderable
    {
        void Render(IntPtr renderer, SDL.SDL_Rect position);
    }
    
    public sealed class UiBatch
    {
        private Vector2 buildSize;
        private Vector2 contentSize;
        private readonly IPanel panel;
        private Vector2 _offset;

        public Vector2 offset
        {
            get => _offset;
            set
            {
                _offset = value;
                Repaint();
            }
        }

        private long nextRebuildTimer = long.MaxValue;
        public float pixelsPerUnit { get; private set; }
        public bool clip { get; set; }
        private bool rebuildRequested = true;
        private readonly List<(Rect, SchemeColor, IMouseHandle)> rects = new List<(Rect, SchemeColor, IMouseHandle)>();
        private readonly List<(Rect, RectangleBorder)> borders = new List<(Rect, RectangleBorder)>();
        private readonly List<(Rect, Icon, SchemeColor)> icons = new List<(Rect, Icon, SchemeColor)>();
        private readonly List<(Rect, IRenderable)> renderables = new List<(Rect, IRenderable)>();
        private readonly List<(Rect, UiBatch, IMouseHandle)> subBatches = new List<(Rect, UiBatch, IMouseHandle)>();
        private UiBatch parent;
        public Window window { get; private set; }
        public Vector2 size => contentSize;
        private bool scaled;
        private float scale = 1f;

        public void SetScale(float scale)
        {
            this.scale = scale;
            scaled = scale != 1f;
            Rebuild();
        }

        public ushort UnitsToPixels(float units) => (ushort) MathF.Round(units * pixelsPerUnit);
        public float PixelsToUnits(int pixels) => pixels / pixelsPerUnit;

        public SDL.SDL_Rect ToSdlRect(Rect rect, Vector2 offset = default)
        {
            return new SDL.SDL_Rect
            {
                x = UnitsToPixels(rect.X + offset.X),
                y = UnitsToPixels(rect.Y + offset.Y),
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

        internal void Rebuild(Window window, Vector2 size, float pixelsPerUnit)
        {
            this.window = window;
            rebuildRequested = false;
            this.pixelsPerUnit = pixelsPerUnit;
            if (scaled)
            {
                this.pixelsPerUnit *= scale;
                size /= scale;
            }
            if (panel != null)
            {
                Clear();
                contentSize = panel.BuildPanel(this, size);
            }
        }
        
        public void DrawRectangle(Rect rect, SchemeColor color, RectangleBorder border = RectangleBorder.None, IMouseHandle mouseHandle = null)
        {
            rects.Add((rect, color, mouseHandle));
            if (border != RectangleBorder.None)
                borders.Add((rect, border));
        }

        public void DrawIcon(Rect rect, Icon icon, SchemeColor color)
        {
            icons.Add((rect, icon, color));
        }

        public void DrawRenderable(Rect rect, IRenderable renderable)
        {
            renderables.Add((rect, renderable));
        }

        public void DrawSubBatch(Rect rect, UiBatch batch, IMouseHandle handle = null)
        {
            batch.parent = this;
            subBatches.Add((rect, batch, handle));
            if (batch.IsRebuildRequired())
                batch.Rebuild(window, rect.Size, pixelsPerUnit);
        }

        public bool Raycast<T>(Vector2 position, out T result, out UiBatch resultBatch) where T:class, IMouseHandle
        {
            position -= offset;
            if (scaled)
            {
                position.X *= scale;
                position.Y *= scale;
            }
            for (var i = subBatches.Count - 1; i >= 0; i--)
            {
                var (rect, batch, handle) = subBatches[i];
                if (rect.Contains(position))
                {
                    if (batch.Raycast(new Vector2(position.X - rect.X, position.Y - rect.Y), out result, out resultBatch))
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

        internal void Present(Window window, Vector2 screenOffset, Rect screenClip)
        {
            this.window = window;
            var renderer = window.renderer;
            SDL.SDL_Rect prevClip = default;
            if (scaled)
            {
                
            }
            screenOffset += offset;
            if (clip)
            {
                SDL.SDL_RenderGetClipRect(renderer, out prevClip);
                var clipRect = ToSdlRect(screenClip);
                SDL.SDL_RenderSetClipRect(renderer, ref clipRect);
            }
            var localClip = new Rect(screenClip.Location - screenOffset, screenClip.Size);
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
                var screenRect = new Rect(rect.Location + screenOffset, rect.Size);
                var intersection = Rect.Intersect(screenRect, localClip);
                if (intersection == default)
                    continue;

                if (batch.IsRebuildRequired() || batch.buildSize != rect.Size)
                {
                    batch.buildSize = rect.Size;
                    batch.Rebuild(window, rect.Size, pixelsPerUnit);
                }
                batch.Present(window, new Vector2(screenRect.X, screenRect.Y), intersection);
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