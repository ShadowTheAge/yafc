using System;
using System.Collections.Generic;
using System.Numerics;

namespace YAFC.UI
{
    public partial class ImGui
    {
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
            panel.Build(rect.Width, this, pixelsPerUnit);
        }
        
        private readonly ImGuiCache<TextCache, (FontFile.FontSize size, string text, uint wrapWidth)>.Cache textCache = new ImGuiCache<TextCache, (FontFile.FontSize size, string text, uint wrapWidth)>.Cache();

        public void BuildText(string text, Font font = null, SchemeColor color = SchemeColor.BackgroundText, bool wrap = false, RectAlignment align = RectAlignment.MiddleLeft)
        {
            var fontSize = (font ?? Font.text).GetFontSize(pixelsPerUnit);
            var cache = textCache.GetCached((fontSize, text, wrap ? (uint) UnitsToPixels(width) : uint.MaxValue));
            var rect = AllocateRect(cache.texRect.w / pixelsPerUnit, cache.texRect.h / pixelsPerUnit, align);
            if (action == ImGuiAction.Build)
            {
                DrawRenderable(rect, cache, color);
            }
        }
        
        public void BuildIcon(Icon icon, SchemeColor color, float size = 1f)
        {
            var rect = AllocateRect(size, size, RectAlignment.Middle);
            batch.DrawIcon(rect, icon, color);
        }

        public Vector2 mousePosition { get; private set; }
        private Rect mouseDownRect;
        private Rect mouseOverRect = Rect.VeryBig;
        private readonly RectAllocator defaultAllocator;
        public event Action CollectCustomCache;

        private bool DoGui(ImGuiAction action, bool forceBuild = false)
        {
            this.action = action;
            ResetLayout();
            gui.Build(this);
            eventArg = 0;
            var consumed = action == ImGuiAction.Consumed;
            if (forceBuild || consumed)
                Build(buildWidth);
            this.action = ImGuiAction.Consumed;
            return consumed;
        }

        private void Build(float width)
        {
            buildWidth = width;
            nextRebuildTimer = long.MaxValue;
            rects.Clear();
            borders.Clear();
            icons.Clear();
            renderables.Clear();
            panels.Clear();
            DoGui(ImGuiAction.Build);
            contentSize = layoutSize;
            textCache.PurgeUnused();
            CollectCustomCache?.Invoke();
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
    }
}