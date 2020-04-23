using System;
using System.Collections.Generic;
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

    public interface IGuiPanel
    {
        void MouseDown(int button);
        void MouseUp(int button);
        void MouseMove(Vector2 position);
        void MouseScroll(int delta);
        void MarkEverythingForRebuild();
        Vector2 Build(float maxWidth, ImGui parent);
        void Present(Window window, Rect position, Rect screenClip);
    }
    
    public class ImGui : IDisposable, IGuiPanel
    {
        public readonly IGui gui;
        public ImGui(IGui gui, RectAllocator defaultAllocator = RectAllocator.Stretch, bool clip = false)
        {
            this.gui = gui;
            this.defaultAllocator = defaultAllocator;
            this.clip = clip;
        }

        #region Draw commands

        private readonly List<(Rect, SchemeColor)> rects = new List<(Rect, SchemeColor)>();
        private readonly List<(Rect, RectangleBorder)> borders = new List<(Rect, RectangleBorder)>();
        private readonly List<(Rect, Icon, SchemeColor)> icons = new List<(Rect, Icon, SchemeColor)>();
        private readonly List<(Rect, IRenderable, SchemeColor)> renderables = new List<(Rect, IRenderable, SchemeColor)>();
        private readonly List<(Rect, IGuiPanel)> panels = new List<(Rect, IGuiPanel)>();
        
        public void DrawRectangle(Rect rect, SchemeColor color, RectangleBorder border = RectangleBorder.None)
        {
            rects.Add((rect, color));
            if (border != RectangleBorder.None)
                borders.Add((rect, border));
        }

        public void DrawIcon(Rect rect, Icon icon, SchemeColor color)
        {
            if (icon == Icon.None)
                return;
            icons.Add((rect, icon, color));
        }

        public void DrawRenderable(Rect rect, IRenderable renderable, SchemeColor color)
        {
            renderables.Add((rect, renderable, color));
        }

        public void DrawPanel(Rect rect, IGuiPanel panel)
        {
            panels.Add((rect, panel));
            panel.Build(rect.Width, this);
        }
        
        private readonly ImGuiCache<TextCache, (FontFile.FontSize size, string text, uint wrapWidth)>.Cache textCache = new ImGuiCache<TextCache, (FontFile.FontSize size, string text, uint wrapWidth)>.Cache();

        public void BuildText(string text, Font font = null, SchemeColor color = SchemeColor.BackgroundText, bool wrap = false, RectAlignment align = RectAlignment.MiddleLeft)
        {
            var fontSize = (font ?? Font.text).GetFontSize(pixelsPerUnit);
            var cache = textCache.GetCached((fontSize, text, wrap ? (uint) state.batch.UnitsToPixels(state.width) : uint.MaxValue));
            var rect = state.AllocateRect(cache.texRect.w / state.batch.pixelsPerUnit, cache.texRect.h / state.batch.pixelsPerUnit, align);
            if (action == ImGuiAction.Build)
            {
                DrawRenderable(rect, cache, color);
            }
        }

        #endregion

        #region utilities

        private float pixelsPerUnit;
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
        
        private void ReleaseUnmanagedResources()
        {
            textCache.PurgeAll();
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
        
        private static void CheckMainThread()
        {
            if (!Ui.IsMainThread())
                throw new NotSupportedException("This should be called from the main thread");
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

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~ImGui()
        {
            ReleaseUnmanagedResources();
        }

        #endregion

        #region actions
        public Window window { get; private set; }
        private ImGui parent;
        private bool rebuildRequested = true;
        private float buildWidth;
        public ImGuiAction action { get; private set; }
        public int eventArg { get; private set; }
        public readonly LayoutState state = new LayoutState();
        private long nextRebuildTimer = long.MaxValue;
        public bool IsRebuildRequired() => rebuildRequested || Ui.time >= nextRebuildTimer;
        public Vector2 mousePosition { get; private set; }
        private Rect mouseDownRect;
        private Rect mouseOverRect = Rect.VeryBig;
        private readonly RectAllocator defaultAllocator;

        private bool DoGui(ImGuiAction action, bool forceBuild = false)
        {
            this.action = action;
            state.Reset(buildWidth, defaultAllocator);
            gui.Build(this);
            eventArg = 0;
            var consumed = action == ImGuiAction.Consumed;
            if (forceBuild || consumed)
                Build(buildWidth);
            this.action = ImGuiAction.Consumed;
            return consumed;
        }

        internal void Build(float width)
        {
            buildWidth = width;
            pixelsPerUnit = parent?.pixelsPerUnit ?? window.pixelsPerUnit;
            nextRebuildTimer = long.MaxValue;
            rects.Clear();
            borders.Clear();
            icons.Clear();
            renderables.Clear();
            panels.Clear();
            DoGui(ImGuiAction.Build);
            textCache.PurgeUnused();
        }

        public void MouseMove(Vector2 mousePosition)
        {
            this.mousePosition = mousePosition;
            if (!DoGui(ImGuiAction.MouseMove, !mouseOverRect.Contains(mousePosition)))
                mouseOverRect = Rect.VeryBig;
        }

        public void MouseDown(int button)
        {
            eventArg = button;
            DoGui(ImGuiAction.MouseDown);
        }

        public void MouseUp(int button)
        {
            eventArg = button;
            DoGui(ImGuiAction.MouseUp);
        }

        public void MouseScroll(int delta)
        {
            eventArg = delta;
            DoGui(ImGuiAction.MouseScroll);
        }

        #endregion
        
        #region input

        public bool ConsumeMouseDown(Rect rect)
        {
            if (action == ImGuiAction.MouseDown && rect.Contains(mousePosition))
            {
                action = ImGuiAction.Consumed;
                mouseDownRect = rect;
                return true;
            }

            return false;
        }

        public bool ConsumeMouseOver(Rect rect)
        {
            if (action == ImGuiAction.MouseMove && rect.Contains(mousePosition))
            {
                action = ImGuiAction.Consumed;
                mouseOverRect = rect;
                return true;
            }

            return false;
        }

        public bool ConsumeMouseUp(Rect rect, bool inside = true)
        {
            if (action == ImGuiAction.MouseUp && rect == mouseDownRect && (!inside || rect.Contains(mousePosition)))
            {
                action = ImGuiAction.Consumed;
                return true;
            }

            return false;
        }

        public void ConsumeEvent(Rect rect)
        {
            if (action == ImGuiAction.MouseScroll && rect.Contains(mousePosition))
                action = ImGuiAction.Consumed;
        }

        public bool IsMouseOver(Rect rect) => rect == mouseOverRect;
        public bool IsMouseDown(Rect rect) => rect == mouseDownRect;

        #endregion
        
        #region rendering
        
        private float scale = 1f;
        public readonly bool clip;
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
        
        public void Repaint()
        {
            window?.Repaint();
        }
        
        public Vector2 Build(float maxWidth, ImGui parent)
        {
            this.parent = parent;
            if (IsRebuildRequired() || buildWidth != maxWidth)
                Build(maxWidth);
            return state.size;
        }

        public void Present(Window window, Rect position, Rect screenClip)
        {
            if (IsRebuildRequired() || buildWidth != position.Width)
                Build(position.Width);
            
            this.window = window;
            var renderer = window.renderer;
            SDL.SDL_Rect prevClip = default;
            var screenOffset = position.Location * scale + offset;
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

        #endregion
    }
}