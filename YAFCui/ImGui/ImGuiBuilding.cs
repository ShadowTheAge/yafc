using System;
using System.Collections.Generic;
using System.Numerics;
using SDL2;

namespace YAFC.UI
{
    public partial class ImGui
    {
        private readonly List<(Rect, SchemeColor)> rects = new List<(Rect, SchemeColor)>();
        private readonly List<(Rect, RectangleBorder)> borders = new List<(Rect, RectangleBorder)>();
        private readonly List<(Rect, Icon, SchemeColor)> icons = new List<(Rect, Icon, SchemeColor)>();
        private readonly List<(Rect, IRenderable, SchemeColor)> renderables = new List<(Rect, IRenderable, SchemeColor)>();
        private readonly List<(Rect, IPanel)> panels = new List<(Rect, IPanel)>();
        public SchemeColor initialTextColor { get; set; } = SchemeColor.BackgroundText;
        public SchemeColor boxColor { get; set; } = SchemeColor.None;
        public Padding initialPadding { get; set; }

        public void DrawRectangle(Rect rect, SchemeColor color, RectangleBorder border = RectangleBorder.None)
        {
            CheckBuilding();
            rects.Add((rect, color));
            if (border != RectangleBorder.None)
                borders.Add((rect, border));
        }

        public void DrawIcon(Rect rect, Icon icon, SchemeColor color)
        {
            CheckBuilding();
            if (icon == Icon.None)
                return;
            icons.Add((rect, icon, color));
        }

        public void DrawRenderable(Rect rect, IRenderable renderable, SchemeColor color)
        {
            CheckBuilding();
            renderables.Add((rect, renderable, color));
        }

        public void DrawPanel(Rect rect, IPanel panel)
        {
            CheckBuilding();
            panels.Add((rect, panel));
            panel.CalculateState(rect + screenOffset, this, pixelsPerUnit);
        }
        
        public readonly ImGuiCache<TextCache, (FontFile.FontSize size, string text, uint wrapWidth)>.Cache textCache = new ImGuiCache<TextCache, (FontFile.FontSize size, string text, uint wrapWidth)>.Cache();

        public FontFile.FontSize GetFontSize(Font font = null) => (font ?? Font.text).GetFontSize(pixelsPerUnit);

        public void BuildText(string text, Font font = null, bool wrap = false, RectAlignment align = RectAlignment.MiddleLeft, SchemeColor color = SchemeColor.None)
        {
            if (color == SchemeColor.None)
                color = state.textColor;
            var fontSize = GetFontSize(font);
            if (string.IsNullOrEmpty(text))
            {
                AllocateRect(0f, fontSize.lineSize / pixelsPerUnit);
                return;
            }
            var cache = textCache.GetCached((fontSize, text, wrap ? (uint) UnitsToPixels(MathF.Max(width, 5f)) : uint.MaxValue));
            var rect = AllocateRect(cache.texRect.w / pixelsPerUnit, cache.texRect.h / pixelsPerUnit, align);
            if (action == ImGuiAction.Build)
                DrawRenderable(rect, cache, color);
        }

        private ImGuiTextInputHelper textInputHelper;
        public bool BuildTextInput(string text, out string newText, string placeholder, bool delayed = false)
        {
            if (textInputHelper == null)
                textInputHelper = new ImGuiTextInputHelper(this);
            return textInputHelper.BuildTextInput(text, out newText, placeholder, GetFontSize(), delayed);
        }
        
        public void BuildIcon(Icon icon, float size = 1.5f, SchemeColor color = SchemeColor.None)
        {
            if (color == SchemeColor.None)
                color = icon >= Icon.FirstCustom ? SchemeColor.Source : state.textColor;
            var rect = AllocateRect(size, size, RectAlignment.Middle);
            if (action == ImGuiAction.Build)
                DrawIcon(rect, icon, color);
        }

        public Vector2 mousePosition => InputSystem.Instance.mousePosition - screenOffset;
        public bool mousePresent { get; private set; }
        private Rect mouseDownRect;
        private Rect mouseOverRect = Rect.VeryBig;
        private readonly RectAllocator defaultAllocator;
        private int mouseDownButton = -1;
        public event Action CollectCustomCache;

        private bool DoGui(ImGuiAction action)
        {
            this.action = action;
            ResetLayout();
            using (EnterGroup(initialPadding, defaultAllocator, initialTextColor))
            {
                gui.Build(this);
            }
            actionParameter = 0;
            if (action == ImGuiAction.Build)
                return false;
            var consumed = this.action == ImGuiAction.Consumed;
            if (IsRebuildRequired())
                BuildGui(buildWidth);
            this.action = ImGuiAction.Consumed;
            return consumed;
        }

        private void BuildGui(float width)
        {
            buildWidth = width;
            nextRebuildTimer = long.MaxValue;
            rebuildRequested = false;
            rects.Clear();
            borders.Clear();
            icons.Clear();
            renderables.Clear();
            panels.Clear();
            DoGui(ImGuiAction.Build);
            contentSize = lastRect.BottomRight;
            if (boxColor != SchemeColor.None)
                rects.Add((new Rect(default, contentSize), boxColor));
            textCache.PurgeUnused();
            CollectCustomCache?.Invoke();
            Repaint();
        }

        public void MouseMove(int mouseDownButton)
        {
            actionParameter = mouseDownButton;
            mousePresent = true;
            if (!mouseOverRect.Contains(mousePosition))
            {
                mouseOverRect = Rect.VeryBig;
                rebuildRequested = true;
                SDL.SDL_SetCursor(RenderingUtils.cursorArrow);
            }

            DoGui(ImGuiAction.MouseMove);
        }

        public void MouseDown(int button)
        {
            mouseDownButton = button;
            actionParameter = button;
            mouseDownRect = default;
            DoGui(ImGuiAction.MouseDown);
        }

        public void MouseUp(int button)
        {
            mouseDownButton = -1;
            actionParameter = button;
            DoGui(ImGuiAction.MouseUp);
        }

        public void MouseScroll(int delta)
        {
            actionParameter = delta;
            if (!DoGui(ImGuiAction.MouseScroll))
                parent?.MouseScroll(delta);
        }

        public void MouseExit()
        {
            mousePresent = false;
            if (mouseOverRect != Rect.VeryBig)
            {
                mouseOverRect = Rect.VeryBig;
                SDL.SDL_SetCursor(RenderingUtils.cursorArrow);
                BuildGui(buildWidth);
            }
        }

        public bool ConsumeMouseDown(Rect rect)
        {
            if (action == ImGuiAction.MouseDown && mousePresent && rect.Contains(mousePosition))
            {
                action = ImGuiAction.Consumed;
                rebuildRequested = true;
                mouseDownRect = rect;
                return true;
            }

            return false;
        }

        public bool ConsumeMouseOver(Rect rect, IntPtr cursor = default, bool rebuild = true)
        {
            if (action == ImGuiAction.MouseMove && mousePresent && rect.Contains(mousePosition))
            {
                action = ImGuiAction.Consumed;
                if (mouseOverRect != rect)
                {
                    if (rebuild)
                        rebuildRequested = true;
                    mouseOverRect = rect;
                    SDL.SDL_SetCursor(cursor == default ? RenderingUtils.cursorArrow : cursor);
                }
                return true;
            }

            return false;
        }

        public bool ConsumeMouseUp(Rect rect, bool inside = true)
        {
            if (action == ImGuiAction.MouseUp && rect == mouseDownRect && (!inside || rect.Contains(mousePosition)))
            {
                action = ImGuiAction.Consumed;
                Rebuild();
                return true;
            }

            return false;
        }

        public bool ConsumeEvent(Rect rect)
        {
            if (action == ImGuiAction.MouseScroll && rect.Contains(mousePosition))
            {
                action = ImGuiAction.Consumed;
                return true;
            }

            return false;
        }

        public bool IsMouseOver(Rect rect) => rect == mouseOverRect;
        public bool IsMouseDown(Rect rect, uint button) => rect == mouseDownRect && mouseDownButton == button;
        public bool IsLastMouseDown(Rect rect) => rect == mouseDownRect;

        public void ClearFocus()
        {
            if (mouseDownRect != default)
            {
                mouseDownRect = default;
                Rebuild();
            }
        }
    }
}