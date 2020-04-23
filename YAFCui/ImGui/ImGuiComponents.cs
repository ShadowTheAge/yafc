using SDL2;

namespace YAFC.UI
{
    public static class ImGuiComponents
    {
        public static readonly Padding DefaultButtonPadding = new Padding(1f, 0.5f);

        public static bool Button(this ImGui gui, Rect rect, SchemeColor normal, SchemeColor over, SchemeColor down)
        {
            switch (gui.action)
            {
                case ImGuiAction.MouseMove:
                    gui.ConsumeMouseOver(rect);
                    return false;
                case ImGuiAction.MouseDown:
                    if (gui.eventArg == SDL.SDL_BUTTON_LEFT)
                        gui.ConsumeMouseDown(rect);
                    return false;
                case ImGuiAction.MouseUp:
                    return gui.ConsumeMouseUp(rect);
                case ImGuiAction.Build:
                    var color = gui.IsMouseOver(rect) ? gui.IsMouseDown(rect) ? down : over : normal;
                    gui.DrawRectangle(rect, color);
                    return false;
                default:
                    return false;
            }
        }
        
        public static bool Button(this ImGui gui, string text, SchemeColor color, Padding? padding = null)
        {
            using (gui.state.EnterGroup(padding ?? DefaultButtonPadding, RectAllocator.Center))
            {
                gui.BuildText(text, Font.text, color);
            }

            return gui.Button(gui.state.lastRect, color, color + 1, color + 1);
        }
    }
}