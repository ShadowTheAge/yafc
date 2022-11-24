using System;
using System.Collections.Generic;
using System.Numerics;
using SDL2;

namespace YAFC.UI
{
    public partial class ImGui
    {
        private readonly struct DrawCommand<T>
        {
            public readonly Rect rect;
            public readonly T data;
            public readonly SchemeColor color;

            public DrawCommand(Rect rect, T data, SchemeColor color)
            {
                this.rect = rect;
                this.data = data;
                this.color = color;
            }
            public void Deconstruct(out Rect rect, out T data, out SchemeColor color)
            {
                rect = this.rect;
                data = this.data;
                color = this.color;
            }
        }

        private readonly List<DrawCommand<RectangleBorder>> rects = new List<DrawCommand<RectangleBorder>>();
        private readonly List<DrawCommand<Icon>> icons = new List<DrawCommand<Icon>>();
        private readonly List<DrawCommand<IRenderable>> renderables = new List<DrawCommand<IRenderable>>();
        private readonly List<DrawCommand<IPanel>> panels = new List<DrawCommand<IPanel>>();
        public SchemeColor initialTextColor { get; set; } = SchemeColor.BackgroundText;
        public SchemeColor boxColor { get; set; } = SchemeColor.None;
        public RectangleBorder boxShadow { get; set; } = RectangleBorder.None;
        public Padding initialPadding { get; set; }

        public void DrawRectangle(Rect rect, SchemeColor color, RectangleBorder border = RectangleBorder.None)
        {
            if (action != ImGuiAction.Build)
                return;
            rects.Add(new DrawCommand<RectangleBorder>(rect, border, color));
        }

        public void DrawIcon(Rect rect, Icon icon, SchemeColor color)
        {
            if (action != ImGuiAction.Build || icon == Icon.None)
                return;
            icons.Add(new DrawCommand<Icon>(rect, icon, color));
        }

        public void DrawRenderable(Rect rect, IRenderable renderable, SchemeColor color)
        {
            if (action != ImGuiAction.Build || renderable == null)
                return;
            renderables.Add(new DrawCommand<IRenderable>(rect, renderable, color));
        }

        public void DrawPanel(Rect rect, IPanel panel)
        {
            if (action != ImGuiAction.Build || panel == null)
                return;
            panels.Add(new DrawCommand<IPanel>(rect, panel, 0));
            panel.CalculateState(rect.Width, pixelsPerUnit);
        }

        private void ClearDrawCommandList()
        {
            rects.Clear();
            icons.Clear();
            renderables.Clear();
            panels.Clear();
        }

        public void ManualDrawingClear()
        {
            if (guiBuilder == null)
                ClearDrawCommandList();
        }

        public readonly ImGuiCache<TextCache, (FontFile.FontSize size, string text, uint wrapWidth)>.Cache textCache = new ImGuiCache<TextCache, (FontFile.FontSize size, string text, uint wrapWidth)>.Cache();

        public FontFile.FontSize GetFontSize(Font font = null) => (font ?? Font.text).GetFontSize(pixelsPerUnit);
        public SchemeColor textColor
        {
            get => state.textColor;
            set => state.textColor = value;
        }

        public void BuildText(string text, Font font = null, bool wrap = false, RectAlignment align = RectAlignment.MiddleLeft, SchemeColor color = SchemeColor.None, float topOffset = 0f)
        {
            if (color == SchemeColor.None)
                color = state.textColor;
            var rect = AllocateTextRect(out var cache, text, font, wrap, align, topOffset);
            if (action == ImGuiAction.Build && cache != null)
                DrawRenderable(rect, cache, color);
        }

        public Rect AllocateTextRect(out TextCache cache, string text, Font font = null, bool wrap = false, RectAlignment align = RectAlignment.MiddleLeft, float topOffset = 0f)
        {
            var fontSize = GetFontSize(font);
            Rect rect;
            if (string.IsNullOrEmpty(text))
            {
                cache = null;
                rect = AllocateRect(0f, topOffset + fontSize.lineSize / pixelsPerUnit);
            }
            else
            {
                cache = textCache.GetCached((fontSize, text, wrap ? (uint)UnitsToPixels(MathF.Max(width, 5f)) : uint.MaxValue));
                rect = AllocateRect(cache.texRect.w / pixelsPerUnit, topOffset + cache.texRect.h / pixelsPerUnit, align);
            }

            if (topOffset != 0f)
                rect.Top += topOffset;
            return rect;
        }

        public void DrawText(Rect rect, string text, RectAlignment alignment = RectAlignment.MiddleLeft, Font font = null, SchemeColor color = SchemeColor.None)
        {
            if (color == SchemeColor.None)
                color = state.textColor;
            var fontSize = GetFontSize(font);
            var cache = textCache.GetCached((fontSize, text, uint.MaxValue));
            var realRect = AlignRect(rect, alignment, cache.texRect.w / pixelsPerUnit, cache.texRect.h / pixelsPerUnit);
            if (action == ImGuiAction.Build)
                DrawRenderable(realRect, cache, color);
        }

        private ImGuiTextInputHelper textInputHelper;
        public bool BuildTextInput(string text, out string newText, string placeholder, Icon icon = Icon.None, bool delayed = false)
        {
            var padding = new Padding(icon == Icon.None ? 0.8f : 0.5f, 0.5f);
            return BuildTextInput(text, out newText, placeholder, icon, delayed, padding);
        }

        public bool BuildTextInput(string text, out string newText, string placeholder, Icon icon, bool delayed, Padding padding, RectAlignment alignment = RectAlignment.MiddleLeft, SchemeColor color = SchemeColor.Grey)
        {
            if (textInputHelper == null)
                textInputHelper = new ImGuiTextInputHelper(this);
            return textInputHelper.BuildTextInput(text, out newText, placeholder, GetFontSize(), delayed, icon, padding, alignment, color);
        }

        public void BuildIcon(Icon icon, float size = 1.5f, SchemeColor color = SchemeColor.None)
        {
            if (color == SchemeColor.None)
                color = icon >= Icon.FirstCustom ? SchemeColor.Source : state.textColor;
            var rect = AllocateRect(size, size, RectAlignment.Middle);
            if (action == ImGuiAction.Build)
                DrawIcon(rect, icon, color);
        }

        public Vector2 mousePosition => InputSystem.Instance.mousePosition - screenRect.Position;
        public bool mousePresent { get; private set; }
        public Rect mouseDownRect { get; private set; }
        public Rect mouseOverRect { get; private set; } = Rect.VeryBig;
        private readonly RectAllocator defaultAllocator;
        private int mouseDownButton = -1;
        private float buildingWidth;
        public event Action CollectCustomCache;

        private bool DoGui(ImGuiAction action)
        {
            if (guiBuilder == null)
                return false;
            this.action = action;
            ResetLayout();
            buildingWidth = buildWidth;
            buildGroupsIndex = -1;
            using (EnterGroup(initialPadding, defaultAllocator, initialTextColor))
            {
                guiBuilder(this);
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
            if (guiBuilder == null)
                return;
            buildWidth = width;
            nextRebuildTimer = long.MaxValue;
            rebuildRequested = false;
            ClearDrawCommandList();
            DoGui(ImGuiAction.Build);
            contentSize = new Vector2(lastRect.Right, lastRect.Bottom);
            if (boxColor != SchemeColor.None)
            {
                var rect = new Rect(default, contentSize);
                rects.Add(new DrawCommand<RectangleBorder>(rect, boxShadow, boxColor));
            }
            textCache.PurgeUnused();
            CollectCustomCache?.Invoke();
            Repaint();
        }

        public void MouseMove(int mouseDownButton)
        {
            actionParameter = mouseDownButton;
            mousePresent = true;
            if (currentDraggingObject != null)
            {
                DoGui(ImGuiAction.MouseDrag);
                return;
            }
            if (!mouseOverRect.Contains(mousePosition))
            {
                mouseOverRect = Rect.VeryBig;
                rebuildRequested = true;
                if (!cursorSetByMouseDown)
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

        public void MouseLost()
        {
            mouseDownButton = -1;
            mouseDownRect = default;
            mouseOverRect = Rect.VeryBig;
        }

        public void MouseUp(int button)
        {
            mouseDownButton = -1;
            if (currentDraggingObject != null)
            {
                currentDraggingObject = null;
                rebuildRequested = true;
                return;
            }

            if (cursorSetByMouseDown)
            {
                SDL.SDL_SetCursor(RenderingUtils.cursorHand);
                cursorSetByMouseDown = false;
            }

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

        private bool cursorSetByMouseDown;
        public bool ConsumeMouseDown(Rect rect, uint button = SDL.SDL_BUTTON_LEFT, IntPtr cursor = default)
        {
            if (action == ImGuiAction.MouseDown && mousePresent && rect.Contains(mousePosition) && actionParameter == button)
            {
                action = ImGuiAction.Consumed;
                rebuildRequested = true;
                mouseDownRect = rect;
                if (cursor != default)
                {
                    SDL.SDL_SetCursor(cursor);
                    cursorSetByMouseDown = true;
                }
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
                    if (!cursorSetByMouseDown)
                        SDL.SDL_SetCursor(cursor == default ? RenderingUtils.cursorArrow : cursor);
                    return true;
                }
            }
            return false;
        }

        public bool ConsumeMouseUp(Rect rect, bool inside = true, uint button = SDL.SDL_BUTTON_LEFT)
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
        public bool IsMouseDown(Rect rect, uint button = SDL.SDL_BUTTON_LEFT) => rect == mouseDownRect && mouseDownButton == button;
        public bool IsMouseOverOrDown(Rect rect, uint button = SDL.SDL_BUTTON_LEFT) => mouseOverRect == rect || (mouseDownRect == rect && mouseDownButton == button);
        public bool IsLastMouseDown(Rect rect) => rect == mouseDownRect;

        public void ClearFocus()
        {
            if (mouseDownRect != default)
            {
                mouseDownRect = default;
                Rebuild();
            }
        }

        public void SetFocus(Rect rect)
        {
            mouseDownRect = rect;
            Rebuild();
        }

        public void SetTextInputFocus(Rect rect, string text)
        {
            if (textInputHelper != null && InputSystem.Instance.currentKeyboardFocus != textInputHelper)
            {
                SetFocus(rect);
                textInputHelper.SetFocus(rect, text);
            }
        }

        public void SaveToImage()
        {
        }
    }
}