using System.Numerics;
using SDL2;

namespace YAFC.UI
{
    public static class ImGuiUtils
    {
        public static readonly Padding DefaultButtonPadding = new Padding(1f, 0.5f);
        public static readonly Padding DefaultScreenPadding = new Padding(5f, 2f);
        
        public enum Event
        {
            None,
            Click,
            MouseOver,
            MouseDown,
        }

        public static Event BuildButton(this ImGui gui, Rect rect, SchemeColor normal, SchemeColor over, SchemeColor down = SchemeColor.None)
        {
            switch (gui.action)
            {
                case ImGuiAction.MouseMove:
                    return gui.ConsumeMouseOver(rect, RenderingUtils.cursorHand) ? Event.MouseOver : Event.None;
                case ImGuiAction.MouseDown:
                    return gui.actionParameter == SDL.SDL_BUTTON_LEFT && gui.ConsumeMouseDown(rect) ? Event.MouseDown : Event.None;
                case ImGuiAction.MouseUp:
                    return gui.ConsumeMouseUp(rect) ? Event.Click : Event.None;
                case ImGuiAction.Build:
                    var color = gui.IsMouseOver(rect) ? (down != SchemeColor.None && gui.IsMouseDown(rect, SDL.SDL_BUTTON_LEFT)) ? down : over : normal;
                    gui.DrawRectangle(rect, color);
                    return Event.None;
                default:
                    return Event.None;
            }
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
            using (gui.EnterGroup(padding ?? DefaultButtonPadding, color+2))
            {
                gui.BuildText(text, Font.text, align:RectAlignment.Middle);
            }

            return gui.BuildButton(gui.lastRect, color, color + 1) == Event.Click && active;
        }

        public static bool BuildButton(this ImGui gui, Icon icon, SchemeColor normal, SchemeColor over, SchemeColor down = SchemeColor.None)
        {
            using (gui.EnterGroup(new Padding(0.3f)))
                gui.BuildIcon(icon, 1.5f);
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
    }
}