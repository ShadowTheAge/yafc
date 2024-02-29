using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SDL2;

namespace YAFC.UI {
    // ButtonEvent implicitly converts to true if it is a click event, so for simple buttons that only handle clicks you can just use if() 
    public readonly struct ButtonEvent {
        private readonly int value;
        public static readonly ButtonEvent None = default;
        public static readonly ButtonEvent Click = new ButtonEvent(1);
        public static readonly ButtonEvent MouseOver = new ButtonEvent(2);
        public static readonly ButtonEvent MouseDown = new ButtonEvent(3);

        private ButtonEvent(int value) {
            this.value = value;
        }

        public static bool operator ==(ButtonEvent a, ButtonEvent b) {
            return a.value == b.value;
        }

        public static bool operator !=(ButtonEvent a, ButtonEvent b) {
            return a.value != b.value;
        }

        public bool Equals(ButtonEvent other) {
            return value == other.value;
        }

        public override bool Equals(object obj) {
            return obj is ButtonEvent other && Equals(other);
        }

        public override int GetHashCode() {
            return value;
        }

        public static implicit operator bool(ButtonEvent b) {
            return b == Click;
        }
    }

    public static class ImGuiUtils {
        public static readonly Padding DefaultButtonPadding = new Padding(1f, 0.5f);
        public static readonly Padding DefaultButtonPaddingText = new Padding(0f, 0.5f);
        public static readonly Padding DefaultScreenPadding = new Padding(5f, 2f);
        public static readonly Padding DefaultIconPadding = new Padding(0.3f);

        public static ButtonEvent BuildButton(this ImGui gui, Rect rect, SchemeColor normal, SchemeColor over, SchemeColor down = SchemeColor.None, uint button = SDL.SDL_BUTTON_LEFT) {
            if (button == 0)
                button = (uint)InputSystem.Instance.mouseDownButton;
            switch (gui.action) {
                case ImGuiAction.MouseMove:
                    var wasOver = gui.IsMouseOver(rect);
                    return gui.ConsumeMouseOver(rect, RenderingUtils.cursorHand) && !wasOver ? ButtonEvent.MouseOver : ButtonEvent.None;
                case ImGuiAction.MouseDown:
                    return gui.actionParameter == button && gui.ConsumeMouseDown(rect, button) ? ButtonEvent.MouseDown : ButtonEvent.None;
                case ImGuiAction.MouseUp:
                    return gui.actionParameter == button && gui.ConsumeMouseUp(rect, true, button) ? ButtonEvent.Click : ButtonEvent.None;
                case ImGuiAction.Build:
                    var color = gui.IsMouseOver(rect) ? (down != SchemeColor.None && gui.IsMouseDown(rect, button)) ? down : over : normal;
                    gui.DrawRectangle(rect, color);
                    return ButtonEvent.None;
                default:
                    return ButtonEvent.None;
            }
        }

        public static string ScanToString(SDL.SDL_Scancode scancode) {
            return SDL.SDL_GetKeyName(SDL.SDL_GetKeyFromScancode(scancode));
        }

        public static bool BuildLink(this ImGui gui, string text) {
            gui.BuildText(text, color: SchemeColor.Link);
            var rect = gui.lastRect;
            switch (gui.action) {
                case ImGuiAction.MouseMove:
                    _ = gui.ConsumeMouseOver(rect, RenderingUtils.cursorHand);
                    break;
                case ImGuiAction.MouseDown:
                    if (gui.actionParameter == SDL.SDL_BUTTON_LEFT)
                        _ = gui.ConsumeMouseDown(rect);
                    break;
                case ImGuiAction.MouseUp:
                    if (gui.ConsumeMouseUp(rect))
                        return true;
                    break;
                case ImGuiAction.Build:
                    if (gui.IsMouseOver(rect))
                        gui.DrawRectangle(new Rect(rect.X, rect.Bottom - 0.2f, rect.Width, 0.1f), SchemeColor.Link);
                    break;
            }

            return false;
        }

        public static bool OnClick(this ImGui gui, Rect rect) {
            if (gui.action == ImGuiAction.MouseUp)
                return gui.ConsumeMouseUp(rect);
            if (gui.action == ImGuiAction.MouseDown && gui.actionParameter == SDL.SDL_BUTTON_LEFT)
                _ = gui.ConsumeMouseDown(rect);
            return false;
        }

        public static bool BuildButton(this ImGui gui, string text, SchemeColor color = SchemeColor.Primary, Padding? padding = null, bool active = true) {
            if (!active)
                color = SchemeColor.Grey;
            using (gui.EnterGroup(padding ?? DefaultButtonPadding, active ? color + 2 : color + 3))
                gui.BuildText(text, Font.text, align: RectAlignment.Middle);

            return gui.BuildButton(gui.lastRect, color, color + 1) && active;
        }

        public static ButtonEvent BuildContextMenuButton(this ImGui gui, string text, string rightText = null, Icon icon = default, bool disabled = false) {
            gui.allocator = RectAllocator.Stretch;
            using (gui.EnterGroup(DefaultButtonPadding, RectAllocator.LeftRow, SchemeColor.BackgroundText)) {
                var textColor = disabled ? gui.textColor + 1 : gui.textColor;
                if (icon != default)
                    gui.BuildIcon(icon, color: icon >= Icon.FirstCustom ? disabled ? SchemeColor.SourceFaint : SchemeColor.Source : textColor);
                gui.BuildText(text, Font.text, true, color: textColor);
                if (rightText != null) {
                    gui.allocator = RectAllocator.RightRow;
                    gui.BuildText(rightText, align: RectAlignment.MiddleRight);
                }
            }
            return gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.Grey);
        }

        public static void CaptureException(this Task task) {
            _ = task.ContinueWith(t => throw t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }

        public static bool BuildMouseOverIcon(this ImGui gui, Icon icon, SchemeColor color = SchemeColor.BackgroundText) {
            if (gui.isBuilding && gui.IsMouseOver(gui.lastRect))
                gui.DrawIcon(gui.lastRect, icon, color);
            return gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.BackgroundAlt);
        }

        public static ButtonEvent BuildRedButton(this ImGui gui, string text) {
            Rect textRect;
            TextCache cache;
            using (gui.EnterGroup(DefaultButtonPadding))
                textRect = gui.AllocateTextRect(out cache, text, align: RectAlignment.Middle);
            var evt = gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.Error);
            if (gui.isBuilding)
                gui.DrawRenderable(textRect, cache, gui.IsMouseOver(gui.lastRect) ? SchemeColor.ErrorText : SchemeColor.Error);
            return evt;
        }

        public static ButtonEvent BuildRedButton(this ImGui gui, Icon icon, float size = 1.5f) {
            Rect iconRect;
            using (gui.EnterGroup(new Padding(0.3f)))
                iconRect = gui.AllocateRect(size, size, RectAlignment.Middle);
            var evt = gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.Error);
            if (gui.isBuilding)
                gui.DrawIcon(iconRect, icon, gui.IsMouseOver(gui.lastRect) ? SchemeColor.ErrorText : SchemeColor.Error);
            return evt;
        }

        public static ButtonEvent BuildButton(this ImGui gui, Icon icon, SchemeColor normal = SchemeColor.None, SchemeColor over = SchemeColor.Grey, SchemeColor down = SchemeColor.None, float size = 1.5f) {
            using (gui.EnterGroup(new Padding(0.3f)))
                gui.BuildIcon(icon, size);
            return gui.BuildButton(gui.lastRect, normal, over, down);
        }

        public static ButtonEvent BuildButton(this ImGui gui, Icon icon, string text, SchemeColor normal = SchemeColor.None, SchemeColor over = SchemeColor.Grey, SchemeColor down = SchemeColor.None, float size = 1.5f) {
            using (gui.EnterGroup(new Padding(0.3f), RectAllocator.LeftRow)) {
                gui.BuildIcon(icon, size);
                gui.BuildText(text);
            }
            return gui.BuildButton(gui.lastRect, normal, over, down);
        }

        public static bool WithTooltip(this ButtonEvent evt, ImGui gui, string tooltip) {
            if (evt == ButtonEvent.MouseOver)
                gui.ShowTooltip(gui.lastRect, tooltip);
            return evt;
        }

        public static bool BuildCheckBox(this ImGui gui, string text, bool value, out bool newValue, SchemeColor color = SchemeColor.None, RectAllocator allocator = RectAllocator.LeftRow) {
            using (gui.EnterRow(allocator: allocator)) {
                gui.BuildIcon(value ? Icon.CheckBoxCheck : Icon.CheckBoxEmpty, 1.5f, color);
                gui.BuildText(text, Font.text, color: color);
            }

            if (gui.OnClick(gui.lastRect)) {
                newValue = !value;
                return true;
            }

            newValue = value;
            return false;
        }

        public static bool BuildRadioButton(this ImGui gui, string option, bool selected, SchemeColor color = SchemeColor.None) {
            using (gui.EnterRow()) {
                gui.BuildIcon(selected ? Icon.RadioCheck : Icon.RadioEmpty, 1.5f, color);
                gui.BuildText(option, Font.text, color: color, wrap: true);
            }

            return !selected && gui.OnClick(gui.lastRect);
        }

        public static bool BuildRadioGroup(this ImGui gui, IReadOnlyList<string> options, int selected, out int newSelected, SchemeColor color = SchemeColor.None) {
            newSelected = selected;
            for (var i = 0; i < options.Count; i++) {
                if (BuildRadioButton(gui, options[i], selected == i, color))
                    newSelected = i;
            }

            return newSelected != selected;
        }

        public static bool BuildErrorRow(this ImGui gui, string text) {
            var closed = false;
            using (gui.EnterRow(allocator: RectAllocator.RightRow, textColor: SchemeColor.ErrorText)) {
                if (gui.BuildButton(Icon.Close, size: 1f, over: SchemeColor.ErrorAlt))
                    closed = true;
                gui.RemainingRow().BuildText(text, align: RectAlignment.Middle);
            }
            if (gui.isBuilding)
                gui.DrawRectangle(gui.lastRect, SchemeColor.Error);
            return closed;
        }

        public static bool BuildIntegerInput(this ImGui gui, int value, out int newValue) {
            if (gui.BuildTextInput(value.ToString(), out var newText, null, delayed: true) && int.TryParse(newText, out newValue))
                return true;
            newValue = value;
            return false;
        }

        public static void ShowDropDown(this ImGui gui, Rect rect, GuiBuilder builder, Padding padding, float width = 20f) {
            gui.window?.ShowDropDown(gui, rect, builder, padding, width);
        }

        public static void ShowDropDown(this ImGui gui, GuiBuilder builder, float width = 20f) {
            gui.window?.ShowDropDown(gui, gui.lastRect, builder, new Padding(1f), width);
        }

        public static void ShowTooltip(this ImGui gui, Rect rect, GuiBuilder builder, float width = 20f) {
            gui.window?.ShowTooltip(gui, rect, builder, width);
        }

        public static void ShowTooltip(this ImGui gui, Rect rect, string text, float width = 20f) {
            gui.window?.ShowTooltip(gui, rect, x => x.BuildText(text, wrap: true), width);
        }

        public static void ShowTooltip(this ImGui gui, GuiBuilder builder, float width = 20f) {
            gui.window?.ShowTooltip(gui, gui.lastRect, builder, width);
        }

        public struct InlineGridBuilder : IDisposable {
            private ImGui.Context savedContext;
            private readonly ImGui gui;
            private readonly int elementsPerRow;
            private readonly float elementWidth;
            private readonly float spacing;
            private int currentRowIndex;

            internal InlineGridBuilder(ImGui gui, float elementWidth, float spacing, int elementsPerRow) {
                savedContext = default;
                this.gui = gui;
                this.spacing = spacing;
                gui.allocator = RectAllocator.LeftAlign;
                this.elementWidth = MathF.Min(elementWidth, gui.width);
                this.elementsPerRow = elementsPerRow == 0 ? MathUtils.Floor((gui.width + spacing) / (elementWidth + spacing)) : elementsPerRow;
                currentRowIndex = -1;
                if (elementWidth <= 0)
                    this.elementsPerRow = 1;
            }

            public void Next() {
                if (currentRowIndex == elementsPerRow - 1) {
                    savedContext.Dispose();
                    savedContext = default;
                    currentRowIndex = -1;
                }
                currentRowIndex++;
                if (currentRowIndex == 0) {
                    savedContext = gui.EnterRow(0f);
                    gui.spacing = 0f;
                }
                savedContext.SetManualRect(new Rect((elementWidth + spacing) * currentRowIndex, 0f, elementWidth, 0f), RectAllocator.Stretch);
            }

            public bool isEmpty() {
                return gui == null;
            }

            public void Dispose() {
                savedContext.Dispose();
            }
        }

        public static InlineGridBuilder EnterInlineGrid(this ImGui gui, float elementWidth, float spacing = 0f, int maxElemCount = 0) {
            return new InlineGridBuilder(gui, elementWidth, spacing, maxElemCount);
        }

        public static InlineGridBuilder EnterHorizontalSplit(this ImGui gui, int elementCount, float spacing = 0f) {
            return new InlineGridBuilder(gui, ((gui.width + spacing) / elementCount) - spacing, spacing, elementCount);
        }

        public static bool DoListReordering<T>(this ImGui gui, Rect moveHandle, Rect contents, T index, out T moveFrom, SchemeColor backgroundColor = SchemeColor.PureBackground, bool updateDraggingObject = true) {
            var result = false;
            moveFrom = index;
            if (!gui.InitiateDrag(moveHandle, contents, index, backgroundColor) && gui.action == ImGuiAction.MouseDrag && gui.ConsumeDrag(contents.Center, index)) {
                moveFrom = gui.GetDraggingObject<T>();
                if (updateDraggingObject)
                    gui.UpdateDraggingObject(index);
                result = true;
            }
            return result;
        }

        public static bool InitiateDrag<T>(this ImGui gui, Rect moveHandle, Rect contents, T index, SchemeColor backgroundColor = SchemeColor.PureBackground) {
            if (gui.action == ImGuiAction.MouseDown)
                _ = gui.ConsumeMouseDown(moveHandle);
            if (gui.ShouldEnterDrag(moveHandle) || (gui.action == ImGuiAction.Build && gui.IsDragging(index))) {
                gui.SetDraggingArea(contents, index, backgroundColor);
                return true;
            }
            return false;
        }

        public static bool BuildSlider(this ImGui gui, float value, out float newValue, float width = 10f) {
            var sliderRect = gui.AllocateRect(width, 2f, RectAlignment.Full);
            var handleStart = (sliderRect.Width - 1f) * value;
            var handleRect = new Rect(sliderRect.X + handleStart, sliderRect.Y, 1f, sliderRect.Height);
            var update = false;
            newValue = value;

            switch (gui.action) {
                case ImGuiAction.Build:
                    gui.DrawRectangle(handleRect, gui.IsMouseOverOrDown(sliderRect) ? SchemeColor.Background : SchemeColor.PureBackground, RectangleBorder.Thin);
                    sliderRect.Y += (sliderRect.Height - 0.3f) / 2f;
                    sliderRect.Height = 0.3f;
                    gui.DrawRectangle(sliderRect, SchemeColor.Grey);
                    break;
                case ImGuiAction.MouseMove:
                    if (gui.IsMouseDown(sliderRect))
                        update = true;
                    else _ = gui.ConsumeMouseOver(sliderRect, RenderingUtils.cursorHand);
                    break;
                case ImGuiAction.MouseDown:
                    if (gui.IsMouseOver(sliderRect)) {
                        _ = gui.ConsumeMouseDown(sliderRect);
                        update = true;
                    }
                    break;
            }

            if (!update)
                return false;
            var positionX = (gui.mousePosition.X - sliderRect.X - 0.5f) / (sliderRect.Width - 1f);
            newValue = MathUtils.Clamp(positionX, 0f, 1f);
            gui.Rebuild();
            return true;
        }

        public static bool BuildSearchBox(this ImGui gui, SearchQuery searchQuery, out SearchQuery newQuery, string placeholder = "Search") {
            newQuery = searchQuery;
            if (gui.BuildTextInput(searchQuery.query, out var newText, placeholder, Icon.Search)) {
                newQuery = new SearchQuery(newText);
                return true;
            }

            return false;
        }

        public struct CloseDropdownEvent { }

        public static bool CloseDropdown(this ImGui gui) {
            gui.PropagateMessage<CloseDropdownEvent>(default);
            return true;
        }
    }
}
