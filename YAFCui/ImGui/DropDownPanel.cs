using System.Numerics;

namespace YAFC.UI
{
    public abstract class DropDownPanel : IGui
    {
        private readonly ImGui contents;
        private Rect sourceRect;
        private ImGui source;
        private readonly float width;
        protected virtual SchemeColor background => SchemeColor.PureBackground;

        protected DropDownPanel(Padding padding, float width)
        {
            contents = new ImGui(this, padding) {boxColor = background};
            this.width = width;
        }

        public void SetFocus(ImGui source, Rect rect)
        {
            this.source = source;
            sourceRect = rect;
            contents.Rebuild();
            contents.parent?.Rebuild();
        }
        
        public void Build(ImGui gui)
        {
            if (gui == contents)
            {
                BuildContents(gui);
            }
            else
            {
                if (source != null && source.IsLastMouseDown(sourceRect))
                {
                    if (gui.action == ImGuiAction.Build)
                    {
                        var topleft = gui.FromRootPosition(source.ToRootPosition(sourceRect.TopLeft));
                        var rect = Rect.SideRect(topleft, sourceRect.Size * (gui.pixelsPerUnit / source.pixelsPerUnit));
                        contents.CalculateState(rect.Position, width, gui, gui.pixelsPerUnit);
                        var position = CalculatePosition(gui, rect, contents.contentSize);
                        var parentRect = new Rect(position, contents.contentSize);
                        gui.DrawPanel(parentRect, contents);
                        gui.DrawRectangle(parentRect, SchemeColor.None, RectangleBorder.Thin);
                    }
                }
                else
                {
                    source = null;
                }
            }
        }

        protected abstract Vector2 CalculatePosition(ImGui gui, Rect targetRect, Vector2 contentSize);
        protected abstract void BuildContents(ImGui gui);
    }
}