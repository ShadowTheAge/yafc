using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using SDL2;

namespace YAFC.UI
{
    public static class ImGuiUtils
    {
        public static readonly Padding DefaultButtonPadding = new Padding(1f, 0.5f);
        public static readonly Padding DefaultScreenPadding = new Padding(5f, 2f);
        public static readonly Padding DefaultIconPadding = new Padding(0.3f);
        
        public enum Event
        {
            None,
            Click,
            MouseOver,
            MouseDown,
        }

        public static Event BuildButton(this ImGui gui, Rect rect, SchemeColor normal, SchemeColor over, SchemeColor down = SchemeColor.None, uint button = SDL.SDL_BUTTON_LEFT)
        {
            if (button == 0)
                button = (uint)InputSystem.Instance.mouseDownButton;
            switch (gui.action)
            {
                case ImGuiAction.MouseMove:
                    var wasOver = gui.IsMouseOver(rect);
                    return gui.ConsumeMouseOver(rect, RenderingUtils.cursorHand) && !wasOver ? Event.MouseOver : Event.None;
                case ImGuiAction.MouseDown:
                    return gui.actionParameter == button && gui.ConsumeMouseDown(rect, button) ? Event.MouseDown : Event.None;
                case ImGuiAction.MouseUp:
                    return gui.actionParameter == button && gui.ConsumeMouseUp(rect, true, button) ? Event.Click : Event.None;
                case ImGuiAction.Build:
                    var color = gui.IsMouseOver(rect) ? (down != SchemeColor.None && gui.IsMouseDown(rect, button)) ? down : over : normal;
                    gui.DrawRectangle(rect, color);
                    return Event.None;
                default:
                    return Event.None;
            }
        }
        
        public static bool BuildLink(this ImGui gui, string text)
        {
            gui.BuildText(text, color:SchemeColor.Link);
            var rect = gui.lastRect;
            switch (gui.action)
            {
                case ImGuiAction.MouseMove:
                    gui.ConsumeMouseOver(rect, RenderingUtils.cursorHand);
                    break;
                case ImGuiAction.MouseDown:
                    if (gui.actionParameter == SDL.SDL_BUTTON_LEFT)
                        gui.ConsumeMouseDown(rect);
                    break;
                case ImGuiAction.MouseUp:
                    if (gui.ConsumeMouseUp(rect))
                        return true;
                    break;
                case ImGuiAction.Build:
                    if (gui.IsMouseOver(rect))
                        gui.DrawRectangle(new Rect(rect.X, rect.Bottom-0.2f, rect.Width, 0.1f), SchemeColor.Link);
                    break;
            }

            return false;
        }

        public static bool BuildButtonClick(this ImGui gui, Rect rect, uint button = SDL.SDL_BUTTON_LEFT)
        {
            if (gui.actionParameter == button)
            {
                if (gui.action == ImGuiAction.MouseDown)
                    gui.ConsumeMouseDown(rect);
                else if (gui.action == ImGuiAction.MouseUp)
                    return gui.ConsumeMouseUp(rect);
            }

            return false;
        }

        public static bool OnClick(this ImGui gui, Rect rect)
        {
            if (gui.action == ImGuiAction.MouseUp)
                return gui.ConsumeMouseUp(rect);
            if (gui.action == ImGuiAction.MouseDown && gui.actionParameter == SDL.SDL_BUTTON_LEFT)
                gui.ConsumeMouseDown(rect);
            return false;
        }
        
        public static bool BuildButton(this ImGui gui, string text, SchemeColor color = SchemeColor.Primary, Padding? padding = null, bool active = true)
        {
            if (!active)
                color = SchemeColor.Grey;
            using (gui.EnterGroup(padding ?? DefaultButtonPadding, active ? color+2 : color+3))
                gui.BuildText(text, Font.text, align:RectAlignment.Middle);

            return gui.BuildButton(gui.lastRect, color, color + 1) == Event.Click && active;
        }

        public static bool BuildContextMenuButton(this ImGui gui, string text, string rightText = null)
        {
            using (gui.EnterGroup(DefaultButtonPadding, RectAllocator.LeftRow, SchemeColor.BackgroundText))
            {
                gui.BuildText(text, Font.text, wrap:true);
                if (rightText != null)
                {
                    gui.allocator = RectAllocator.RightRow;
                    gui.BuildText(rightText, align:RectAlignment.MiddleRight);
                }
            }
            return gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.Grey) == Event.Click;
        }

        public static void CaptureException(this Task task)
        {
            task.ContinueWith(t => throw t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }

        public static Event BuildRedButton(this ImGui gui, string text)
        {
            Rect textRect;
            TextCache cache;
            using (gui.EnterGroup(DefaultButtonPadding))
                textRect = gui.AllocateTextRect(out cache, text, align:RectAlignment.Middle);
            var evt = gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.Error);
            if (gui.isBuilding)
                gui.DrawRenderable(textRect, cache, gui.IsMouseOver(gui.lastRect) ? SchemeColor.ErrorText : SchemeColor.Error);
            return evt;
        }
        
        public static Event BuildRedButton(this ImGui gui, Icon icon, float size = 1.5f)
        {
            Rect iconRect;
            using (gui.EnterGroup(new Padding(0.3f)))
                iconRect = gui.AllocateRect(size, size, RectAlignment.Middle);
            var evt = gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.Error);
            if (gui.isBuilding)
                gui.DrawIcon(iconRect, icon, gui.IsMouseOver(gui.lastRect) ? SchemeColor.ErrorText : SchemeColor.Error);
            return evt;
        }

        public static bool BuildButton(this ImGui gui, Icon icon, SchemeColor normal = SchemeColor.None, SchemeColor over = SchemeColor.Grey, SchemeColor down = SchemeColor.None, float size = 1.5f)
        {
            using (gui.EnterGroup(new Padding(0.3f)))
                gui.BuildIcon(icon, size);
            return gui.BuildButton(gui.lastRect, normal, over, down) == Event.Click;
        }

        public static bool BuildCheckBox(this ImGui gui, string text, bool value, out bool newValue, SchemeColor color = SchemeColor.None)
        {
            using (gui.EnterRow())
            {
                gui.BuildIcon(value ? Icon.CheckBoxCheck : Icon.CheckBoxEmpty, 1.5f, color);
                gui.BuildText(text, Font.text, color:color);
            }

            if (gui.OnClick(gui.lastRect))
            {
                newValue = !value;
                return true;
            }

            newValue = value;
            return false;
        }

        public static bool BuildRadioButton(this ImGui gui, string option, bool selected, SchemeColor color = SchemeColor.None)
        {
            using (gui.EnterRow())
            {
                gui.BuildIcon(selected ? Icon.RadioCheck : Icon.RadioEmpty, 1.5f, color);
                gui.BuildText(option, Font.text, color:color, wrap:true);
            }

            return !selected && gui.OnClick(gui.lastRect);
        }

        public static bool BuildRadioGroup(this ImGui gui, IReadOnlyList<string> options, int selected, out int newSelected, SchemeColor color = SchemeColor.None)
        {
            newSelected = selected;
            for (var i = 0; i < options.Count; i++)
            {
                if (BuildRadioButton(gui, options[i], selected==i, color))
                    newSelected = i;
            }

            return newSelected != selected;
        }

        public static bool BuildErrorRow(this ImGui gui, string text)
        {
            var closed = false;
            using (gui.EnterRow(allocator:RectAllocator.RightRow, textColor:SchemeColor.ErrorText))
            {
                if (gui.BuildButton(Icon.Close, size: 1f, over:SchemeColor.ErrorAlt))
                    closed = true;
                gui.RemainingRow().BuildText(text, align:RectAlignment.Middle);
            }
            if (gui.isBuilding)
                gui.DrawRectangle(gui.lastRect, SchemeColor.Error);
            return closed;
        }

        public static void ShowDropDown(this ImGui gui, Rect rect, SimpleDropDown.Builder builder, Padding padding, float width = 20f) => gui.window?.ShowDropDown(gui, rect, builder, padding, width);
        public static void ShowDropDown(this ImGui gui, SimpleDropDown.Builder builder, float width = 20f) => gui.window?.ShowDropDown(gui, gui.lastRect, builder, new Padding(1f), width);
        public static void ShowTooltip(this ImGui gui, Rect rect, GuiBuilder builder, float width = 20f) => gui.window?.ShowTooltip(gui, rect, builder, width);
        public static void ShowTooltip(this ImGui gui, GuiBuilder builder, float width = 20f) => gui.window?.ShowTooltip(gui, gui.lastRect, builder, width);
        
        public struct InlineGridBuilder : IDisposable
        {
            private ImGui.Context savedContext;
            private readonly ImGui gui;
            private readonly int elementsPerRow;
            private readonly float elementWidth;
            private readonly float spacing;
            private int currentRowIndex;

            internal InlineGridBuilder(ImGui gui, float elementWidth, float spacing, int elementsPerRow)
            {
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

            public void Next()
            {
                if (currentRowIndex == elementsPerRow-1)
                {
                    savedContext.Dispose();
                    savedContext = default;
                    currentRowIndex = -1;
                }
                currentRowIndex++;
                if (currentRowIndex == 0)
                {
                    savedContext = gui.EnterRow(0f);
                    gui.spacing = 0f;
                }
                savedContext.SetManualRect(new Rect((elementWidth + spacing) * currentRowIndex, 0f, elementWidth, 0f), RectAllocator.Stretch);
            }

            public void Dispose()
            {
                savedContext.Dispose();
            }
        }

        public static InlineGridBuilder EnterInlineGrid(this ImGui gui, float elementWidth, float spacing = 0f, int maxElemCount = 0)
        {
            return new InlineGridBuilder(gui, elementWidth, spacing, maxElemCount);
        }

        public static InlineGridBuilder EnterHorizontalSplit(this ImGui gui, int elementCount)
        {
            return new InlineGridBuilder(gui, gui.width / elementCount, 0f, elementCount);
        }

        public static bool DoListReordering<T>(this ImGui gui, Rect moveHandle, Rect contents, T index, out T moveFrom, SchemeColor backgroundColor = SchemeColor.PureBackground, bool updateDraggingObject = true)
        {
            var result = false;
            moveFrom = index;
            if (!gui.InitiateDrag(moveHandle, contents, index, backgroundColor) && gui.action == ImGuiAction.MouseDrag && gui.ConsumeDrag(contents.Center, index))
            {
                moveFrom = gui.GetDraggingObject<T>(); 
                if (updateDraggingObject)
                    gui.UpdateDraggingObject(index);
                result = true;
            }
            return result;
        }

        public static bool InitiateDrag<T>(this ImGui gui, Rect moveHandle, Rect contents, T index, SchemeColor backgroundColor = SchemeColor.PureBackground)
        {
            if (gui.action == ImGuiAction.MouseDown)
                gui.ConsumeMouseDown(moveHandle);
            if (gui.ShouldEnterDrag(moveHandle) || (gui.action == ImGuiAction.Build && gui.IsDragging(index)))
            {
                gui.SetDraggingArea(contents, index, backgroundColor);
                return true;
            }
            return false;
        }

        public static bool BuildSlider(this ImGui gui, float value, out float newValue, float width = 10f)
        {
            var sliderRect = gui.AllocateRect(width, 2f, RectAlignment.Full);
            var handleStart = (sliderRect.Width - 1f) * value;
            var handleRect = new Rect(sliderRect.X + handleStart, sliderRect.Y, 1f, sliderRect.Height);
            var update = false;
            newValue = value;

            switch (gui.action)
            {
                case ImGuiAction.Build:
                    gui.DrawRectangle(handleRect, gui.IsMouseOverOrDown(sliderRect) ? SchemeColor.Background : SchemeColor.PureBackground, RectangleBorder.Thin);
                    sliderRect.Y += (sliderRect.Height - 0.3f) / 2f;
                    sliderRect.Height = 0.3f;
                    gui.DrawRectangle(sliderRect, SchemeColor.Grey);
                    break;
                case ImGuiAction.MouseMove:
                    if (gui.IsMouseDown(sliderRect))
                        update = true;
                    else gui.ConsumeMouseOver(sliderRect, RenderingUtils.cursorHand);
                    break;
                case ImGuiAction.MouseDown:
                    if (gui.IsMouseOver(sliderRect))
                    {
                        gui.ConsumeMouseDown(sliderRect);
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
    }
}