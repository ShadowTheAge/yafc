using System;
using System.Numerics;
using SDL2;

namespace YAFC.UI
{
    public abstract class VerticalScroll : IGui
    {
        private readonly ImGui contents;
        private readonly float height;

        private float contentHeight;
        private float scroll;
        private float maxScroll;

        public VerticalScroll(float height)
        {
            contents = new ImGui(this, clip:true);
            this.height = height;
        }
        
        public void Build(ImGui gui)
        {
            if (gui == contents)
                BuildContents(gui);
            else
            {
                var rect = gui.AllocateRect(gui.width, height);
                if (gui.action == ImGuiAction.Build)
                {
                    var innerRect = rect;
                    innerRect.Width -= 0.5f;
                    contentHeight = MeasureContent(innerRect, gui);
                    maxScroll = MathF.Max(contentHeight - height, 0f);
                    gui.DrawPanel(innerRect, contents);
                    scroll = MathUtils.Clamp(scroll, 0f, maxScroll);
                    contents.offset = new Vector2(0, -scroll);
                }
                
                var fullScrollRect = new Rect(rect.Right-0.5f, rect.Y, 0.5f, rect.Width);
                
                var scrollHeight = (height * height) / (height + maxScroll);
                var scrollStart = (scroll / maxScroll) * (height - scrollHeight);
                var scrollRect = new Rect(rect.Right - 0.5f, rect.Y + scrollStart, 0.5f, scrollHeight);
                
                switch (gui.action)
                {
                    case ImGuiAction.MouseDown:
                        if (scrollRect.Contains(gui.mousePosition))
                            gui.ConsumeMouseDown(fullScrollRect);
                        break;
                    case ImGuiAction.MouseMove:
                        if (gui.IsMouseDown(fullScrollRect) && gui.actionMouseButton == SDL.SDL_BUTTON_LEFT)
                        {
                            scroll += gui.actionDelta.Y * (height + maxScroll) / height;
                            gui.Rebuild();
                        }
                        break;
                    case ImGuiAction.Build:
                        gui.DrawRectangle(scrollRect, gui.IsMouseDown(fullScrollRect) ? SchemeColor.GreyAlt : SchemeColor.Grey);
                        break;
                    case ImGuiAction.MouseScroll:
                        if (gui.ConsumeEvent(rect))
                        {
                            scroll += gui.actionDelta.Y * 3f;
                            gui.Rebuild();
                        }
                        break;
                }
            }
        }

        protected virtual float MeasureContent(Rect rect, ImGui gui)
        {
            contents.Build(rect, gui, gui.pixelsPerUnit);
            return contents.layoutSize.Y;
        }

        protected abstract void BuildContents(ImGui gui);
    }
}