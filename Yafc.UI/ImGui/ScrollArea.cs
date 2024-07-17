using System;
using System.Collections.Generic;
using System.Numerics;
using SDL2;

namespace Yafc.UI {
    /// <summary> Provide scrolling support for any component.</summary>
    /// <remarks> The component should use the <see cref="scroll"/> property to get the offset of rendering the contents. </remarks>
    public abstract class Scrollable(bool vertical, bool horizontal, bool collapsible) : IKeyboardFocus {
        /// <summary>Required size to fit Scrollable (child) contents</summary>
        private Vector2 requiredContentSize;
        /// <summary>This rectangle contains the available size (and position) of the scrollable content</summary>
        private Rect contentRect;
        /// <summary>Maximum scroller offset, calculated with the <see cref="requiredContentSize"/> and the available size</summary>
        private Vector2 maxScroll;
        private Vector2 _scroll;
        private ImGui? gui;
        public const float ScrollbarSize = 1f;

        // Padding to add at the bottom of the scroll area to be able to scroll past the
        // last item; needs useBottomPadding to be set to true in method Build()
        private const float BottomPaddingInPixels = 100f;

        protected abstract void PositionContent(ImGui gui, Rect viewport);

        /// <param name="availableHeight">Available height without in parent context for the Scrollable</param>
        public void Build(ImGui gui, float availableHeight, bool useBottomPadding = false) {
            this.gui = gui;
            var rect = gui.statePosition;
            float width = rect.Width;
            if (vertical) {
                width -= ScrollbarSize;
            }

            if (gui.isBuilding) {
                // Calculate required size, including padding if needed
                requiredContentSize = MeasureContent(width, gui);
                if (requiredContentSize.Y > availableHeight && useBottomPadding) {
                    requiredContentSize.Y += BottomPaddingInPixels / gui.pixelsPerUnit;
                }
            }

            float realHeight = collapsible ? MathF.Min(requiredContentSize.Y, availableHeight) : availableHeight;

            if (gui.isBuilding) {
                contentRect = rect;
                contentRect.Width = width;

                maxScroll = Vector2.Max(requiredContentSize - new Vector2(contentRect.Width, availableHeight), Vector2.Zero);
                scroll = Vector2.Clamp(scroll, Vector2.Zero, maxScroll);

                contentRect.Height = realHeight;
                if (horizontal && maxScroll.X > 0) {
                    contentRect.Height -= ScrollbarSize;
                }

                PositionContent(gui, contentRect);
            }

            rect.Height = realHeight;
            _ = gui.EncapsulateRect(rect);

            // Calculate scroller dimensions.
            Vector2 size = new Vector2(width, availableHeight);
            var scrollerSize = size * size / (size + maxScroll);
            scrollerSize = Vector2.Max(scrollerSize, Vector2.One);
            var scrollerStart = _scroll / maxScroll * (size - scrollerSize);

            if ((gui.action == ImGuiAction.MouseDown || gui.action == ImGuiAction.MouseScroll) && rect.Contains(gui.mousePosition)) {
                InputSystem.Instance.SetKeyboardFocus(this);
            }

            if (gui.action == ImGuiAction.MouseScroll) {
                if (gui.ConsumeEvent(rect)) {
                    if (vertical && (!horizontal || !InputSystem.Instance.control)) {
                        scrollY += gui.actionParameter * 3f;
                    }
                    else {
                        scrollX += gui.actionParameter * 3f;
                    }
                }
            }
            else {
                if (horizontal && maxScroll.X > 0f) {
                    Rect scrollbarRect = new Rect(rect.X, rect.Bottom - ScrollbarSize, rect.Width, ScrollbarSize);
                    Rect scrollerRect = new Rect(rect.X + scrollerStart.X, scrollbarRect.Y, scrollerSize.X, ScrollbarSize);
                    BuildScrollBar(gui, 0, in scrollbarRect, in scrollerRect);
                }

                if (vertical && maxScroll.Y > 0f) {
                    Rect scrollbarRect = new Rect(rect.Right - ScrollbarSize, rect.Y, ScrollbarSize, rect.Height);
                    Rect scrollerRect = new Rect(scrollbarRect.X, rect.Y + scrollerStart.Y, ScrollbarSize, scrollerSize.Y);
                    BuildScrollBar(gui, 1, in scrollbarRect, in scrollerRect);
                }
            }
        }

        private void BuildScrollBar(ImGui gui, int axis, in Rect scrollbarRect, in Rect scrollerRect) {
            switch (gui.action) {
                case ImGuiAction.MouseDown:
                    if (scrollerRect.Contains(gui.mousePosition)) {
                        _ = gui.ConsumeMouseDown(scrollbarRect);
                    }

                    break;
                case ImGuiAction.MouseMove:
                    if (gui.IsMouseDown(scrollbarRect, SDL.SDL_BUTTON_LEFT)) {
                        if (axis == 0) {
                            scrollX += InputSystem.Instance.mouseDelta.X * requiredContentSize.X / scrollbarRect.Width;
                        }
                        else {
                            scrollY += InputSystem.Instance.mouseDelta.Y * requiredContentSize.Y / scrollbarRect.Height;
                        }
                    }
                    break;
                case ImGuiAction.Build:
                    gui.DrawRectangle(scrollerRect, SchemeColor.Grey);
                    break;
            }
        }

        /// <summary>X and Y positions of the scrollers</summary>
        public virtual Vector2 scroll {
            get => _scroll;
            set {
                value = Vector2.Clamp(value, Vector2.Zero, maxScroll);
                if (value != _scroll) {
                    _scroll = value;
                    gui?.Rebuild();
                }
            }
        }

        /// <summary>Position of the Y scroller</summary>
        public float scrollY {
            get => _scroll.Y;
            set => scroll = new Vector2(_scroll.X, value);
        }

        /// <summary>Position of the X scroller</summary>
        public float scrollX {
            get => _scroll.X;
            set => scroll = new Vector2(value, _scroll.Y);
        }

        ///<summary>This method is called when the required area of the <see cref="Scrollable"/> for the provided <paramref name="width"/> is needed.</summary>
        /// <returns>The required area of the contents of the <see cref="Scrollable"/>.</returns>
        protected abstract Vector2 MeasureContent(float width, ImGui gui);

        public bool KeyDown(SDL.SDL_Keysym key) {
            switch (key.scancode) {
                case SDL.SDL_Scancode.SDL_SCANCODE_UP:
                    scrollY -= 3;
                    return true;
                case SDL.SDL_Scancode.SDL_SCANCODE_DOWN:
                    scrollY += 3;
                    return true;
                case SDL.SDL_Scancode.SDL_SCANCODE_LEFT:
                    scrollX -= 3;
                    return true;
                case SDL.SDL_Scancode.SDL_SCANCODE_RIGHT:
                    scrollX += 3;
                    return true;
                case SDL.SDL_Scancode.SDL_SCANCODE_PAGEDOWN:
                    scrollY += contentRect.Height;
                    return true;
                case SDL.SDL_Scancode.SDL_SCANCODE_PAGEUP:
                    scrollY -= contentRect.Height;
                    return true;
                case SDL.SDL_Scancode.SDL_SCANCODE_HOME:
                    scrollY = 0;
                    return true;
                case SDL.SDL_Scancode.SDL_SCANCODE_END:
                    scrollY = maxScroll.Y;
                    return true;
                default:
                    return false;
            }
        }

        public bool TextInput(string input) => false;

        public bool KeyUp(SDL.SDL_Keysym key) => false;

        public void FocusChanged(bool focused) { }
    }

    /// <summary>Provides a builder to the Scrollable to render the contents.</summary>
    public abstract class ScrollAreaBase : Scrollable {
        protected ImGui contents;
        protected readonly float height;

        public ScrollAreaBase(float height, Padding padding, bool collapsible = false, bool vertical = true, bool horizontal = false) : base(vertical, horizontal, collapsible) {
            contents = new ImGui(BuildContents, padding, clip: true);
            this.height = height;
        }

        protected override void PositionContent(ImGui gui, Rect viewport) {
            gui.DrawPanel(viewport, contents);
            contents.offset = -scroll;
        }

        public void Build(ImGui gui) => Build(gui, height);

        protected abstract void BuildContents(ImGui gui);

        public void RebuildContents() => contents.Rebuild();

        protected override Vector2 MeasureContent(float width, ImGui gui) => contents.CalculateState(width, gui.pixelsPerUnit);
    }

    ///<summary>Area with scrollbars, which will be visible if it does not fit in the parent area in order to let the user fully view the content of the area.</summary>
    public class ScrollArea(float height, GuiBuilder builder, Padding padding = default, bool collapsible = false, bool vertical = true, bool horizontal = false) : ScrollAreaBase(height, padding, collapsible, vertical, horizontal) {
        protected override void BuildContents(ImGui gui) => builder(gui);

        public void Rebuild() => RebuildContents();
    }

    public class VirtualScrollList<TData> : ScrollAreaBase {
        private readonly Vector2 elementSize;
        // When rendering the scrollable content, render 'blocks' of 4 rows at a time. (As far as I can tell, any positive value works. Shadow picked 4, so I kept that.)
        private readonly int bufferRows = 4;
        // The first block of bufferRows that was rendered last time BuildContents was called. If it changes while scrolling, we need to re-render the scrollable content.
        private int firstVisibleBlock;
        private int elementsPerRow;
        private IReadOnlyList<TData> _data = [];
        private readonly int maxRowsVisible;
        private readonly Drawer drawer;
        private float _spacing;
        private readonly Action<int, int>? reorder;

        public float spacing {
            get => _spacing;
            set {
                _spacing = value;
                RebuildContents();
            }
        }

        public delegate void Drawer(ImGui gui, TData element, int index);

        public IReadOnlyList<TData> data {
            get => _data;
            set {
                _data = value ?? [];
                RebuildContents();
            }
        }

        public VirtualScrollList(float height, Vector2 elementSize, Drawer drawer, Padding padding = default, Action<int, int>? reorder = null, bool collapsible = false) : base(height, padding, collapsible) {
            this.elementSize = elementSize;
            maxRowsVisible = MathUtils.Ceil(height / this.elementSize.Y) + bufferRows + 1;
            this.drawer = drawer;
            this.reorder = reorder;
        }

        private int CalcFirstBlock() => Math.Max(0, MathUtils.Floor((scrollY - contents.initialPadding.top) / (elementSize.Y * bufferRows)));

        public override Vector2 scroll {
            get => base.scroll;
            set {
                base.scroll = value;
                int row = CalcFirstBlock();
                if (row != firstVisibleBlock) {
                    RebuildContents();
                }
            }
        }

        protected override void BuildContents(ImGui gui) {
            elementsPerRow = MathUtils.Floor((gui.width + _spacing) / (elementSize.X + _spacing));
            if (elementsPerRow < 1) {
                elementsPerRow = 1;
            }

            int rowCount = ((_data.Count - 1) / elementsPerRow) + 1;
            firstVisibleBlock = CalcFirstBlock();
            // Scroll up until there are maxRowsVisible, or to the top.
            int firstRow = Math.Max(0, Math.Min(firstVisibleBlock * bufferRows, rowCount - maxRowsVisible));
            int index = firstRow * elementsPerRow;
            if (index >= _data.Count) {
                // If _data is empty, there's nothing to draw. Make sure MeasureContent reports that, instead of the size of the most recent non-empty content.
                // This will remove the scroll bar when the search doesn't match anything.
                gui.lastContentRect = new Rect(gui.lastContentRect.X, gui.lastContentRect.Y, 0, 0);
                return;
            }

            int lastRow = firstRow + maxRowsVisible;
            using var manualPlacing = gui.EnterFixedPositioning(gui.width, rowCount * elementSize.Y, default);
            var offset = gui.statePosition.Position;
            float elementWidth = gui.width / elementsPerRow;
            Rect cell = new Rect(offset.X, offset.Y, elementWidth - _spacing, elementSize.Y);
            for (int row = firstRow; row < lastRow; row++) {
                cell.Y = row * (elementSize.Y + _spacing);
                for (int elem = 0; elem < elementsPerRow; elem++) {
                    cell.X = elem * elementWidth;
                    manualPlacing.SetManualRectRaw(cell);
                    BuildElement(gui, _data[index], index);
                    if (reorder != null) {
                        if (gui.DoListReordering(cell, cell, index, out int fromIndex)) {
                            reorder(fromIndex, index);
                        }
                    }
                    if (++index >= _data.Count) {
                        return;
                    }
                }
            }
        }

        protected virtual void BuildElement(ImGui gui, TData element, int index) => drawer(gui, element, index);
    }
}
