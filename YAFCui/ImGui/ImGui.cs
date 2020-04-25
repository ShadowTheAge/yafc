using System;
using System.Numerics;
using SDL2;

namespace YAFC.UI
{
    public enum ImGuiAction
    {
        Consumed,
        Build,
        MouseMove,
        MouseDown,
        MouseUp,
        MouseScroll
    }

    public interface IGui
    {
        void Build(ImGui gui);
    }

    public interface IPanel
    {
        void MouseDown(int button);
        void MouseUp(int button);
        void MouseMove(int mouseDownButton);
        void MouseScroll(int delta);
        void MarkEverythingForRebuild();
        Vector2 Build(Rect position, ImGui parent, float pixelsPerUnit);
        void Present(Window window, Rect position, Rect screenClip);
        IPanel HitTest(Vector2 position);
        void MouseExit();
    }

    public interface IRenderable
    {
        void Render(IntPtr renderer, SDL.SDL_Rect position, SDL.SDL_Color color);
    }

    public enum RectAllocator
    {
        Stretch,
        LeftAlign,
        RightAlign,
        Center,
        LeftRow,
        RightRow,
        RemainigRow,
        FixedRect,
    }
    
    public sealed partial class ImGui : IDisposable, IPanel
    {
        public ImGui(IGui gui, Padding padding, RectAllocator defaultAllocator = RectAllocator.Stretch, bool clip = false)
        {
            this.gui = gui;
            this.defaultAllocator = defaultAllocator;
            this.clip = clip;
            initialPadding = padding;
        }
        
        public readonly IGui gui;
        public Window window { get; private set; }
        public ImGui parent { get; private set; }
        private bool rebuildRequested = true;
        private float buildWidth;
        private Vector2 contentSize;
        public ImGuiAction action { get; private set; }
        public int actionParameter { get; private set; }
        private long nextRebuildTimer = long.MaxValue;
        public float pixelsPerUnit { get; private set; }

        private float scale = 1f;
        private readonly bool clip;
        private Vector2 _offset;
        private Vector2 screenOffset;
        
        public Vector2 offset
        {
            get => _offset;
            set
            {
                _offset = value;
                Repaint();
            }
        }

        public bool IsRebuildRequired() => rebuildRequested || Ui.time >= nextRebuildTimer;

        public void Rebuild()
        {
            rebuildRequested = true;
            Repaint();
        }

        public void MarkEverythingForRebuild()
        {
            CheckMainThread();
            rebuildRequested = true;
            foreach (var (_, panel) in panels)
                panel.MarkEverythingForRebuild();
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
        
        public void Repaint()
        {
            window?.Repaint();
        }
        
        public Vector2 Build(Rect position, ImGui parent, float pixelsPerUnit)
        {
            this.parent = parent;
            this.pixelsPerUnit = pixelsPerUnit;
            if (IsRebuildRequired() || buildWidth != position.Width)
                BuildGui(position.Width);
            screenOffset = position.Location * scale + offset;
            return contentSize;
        }

        public void Present(Window window, Rect position, Rect screenClip)
        {
            if (IsRebuildRequired() || buildWidth != position.Width)
                BuildGui(position.Width);
            
            this.window = window;
            var renderer = window.renderer;
            SDL.SDL_Rect prevClip = default;
            screenOffset = position.Location * scale + offset;
            if (clip)
            {
                SDL.SDL_RenderGetClipRect(renderer, out prevClip);
                var clipRect = ToSdlRect(screenClip);
                SDL.SDL_RenderSetClipRect(renderer, ref clipRect);
            }
            var localClip = new Rect(screenClip.Location - screenOffset, screenClip.Size / scale);
            var currentColor = (SchemeColor) (-1);
            for (var i = rects.Count - 1; i >= 0; i--)
            {
                var (rect, color) = rects[i];
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

            foreach (var (pos, renderable, color) in renderables)
            {
                if (!pos.IntersectsWith(localClip))
                    continue;
                renderable.Render(renderer, ToSdlRect(pos, screenOffset), color.ToSdlColor());
            }

            foreach (var (rect, batch) in panels)
            {
                var intersection = Rect.Intersect(rect, localClip);
                if (intersection == default)
                    continue;
                batch.Present(window, rect + screenOffset, intersection + screenOffset);
            }

            if (clip)
            {
                if (prevClip.w == 0)
                    SDL.SDL_RenderSetClipRect(renderer, IntPtr.Zero);
                else SDL.SDL_RenderSetClipRect(renderer, ref prevClip);
            }
        }
        
        public IPanel HitTest(Vector2 position)
        {
            for (var i = panels.Count - 1; i >= 0; i--)
            {
                var (rect, panel) = panels[i];
                if (rect.Contains(position))
                    return panel.HitTest(position);
            }

            return this;
        }

        public int UnitsToPixels(float units) => (int) MathF.Round(units * pixelsPerUnit);
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
        
        private static void CheckMainThread()
        {
            if (!Ui.IsMainThread())
                throw new NotSupportedException("This should be called from the main thread");
        }
        
        private void CheckBuilding()
        {
            if (action != ImGuiAction.Build)
                throw new NotSupportedException("This can only be called during Build action");
        }

        public Vector2 ToRootPosition(Vector2 localPosition)
        {
            localPosition = localPosition * scale + offset;
            return parent?.ToRootPosition(localPosition) ?? localPosition;
        }

        public Vector2 FromRootPosition(Vector2 rootPosition)
        {
            rootPosition = (rootPosition - offset) / scale;
            return parent?.FromRootPosition(rootPosition) ?? rootPosition;
        }
        
        private void ReleaseUnmanagedResources()
        {
            textCache.Dispose();
        }
        
        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~ImGui()
        {
            ReleaseUnmanagedResources();
        }
    }
}