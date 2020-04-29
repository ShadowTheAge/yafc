using System;
using System.Numerics;

namespace YAFC.UI
{
    public abstract class AttachedPanel : IGui
    {
        protected readonly ImGui contents;
        protected Rect sourceRect;
        protected ImGui source;
        private ImGui owner;
        private readonly float width;
        protected virtual SchemeColor background => SchemeColor.PureBackground;

        protected AttachedPanel(Padding padding, float width)
        {
            contents = new ImGui(this, padding) {boxColor = background, boxShadow = RectangleBorder.Thin};
            this.width = width;
        }
        
        public virtual void SetFocus(ImGui source, Rect rect)
        {
            this.source = source;
            sourceRect = rect;
            contents.Rebuild();
            owner?.Rebuild();
        }
        
        public void Build(ImGui gui)
        {
            if (gui == contents)
            {
                BuildContents(gui);
            }
            else
            {
                owner = gui;
                if (source != null && gui.action == ImGuiAction.Build)
                {
                    var topleft = gui.FromWindowPosition(source.ToWindowPosition(sourceRect.TopLeft));
                    var rect = new Rect(topleft, sourceRect.Size * (gui.pixelsPerUnit / source.pixelsPerUnit));
                    if (ShoudBuild(source, sourceRect, gui, rect))
                    {
                        var contentSize = contents.CalculateState(width, gui.pixelsPerUnit);
                        var position = CalculatePosition(gui, rect, contentSize);
                        var parentRect = new Rect(position, contentSize);
                        gui.DrawPanel(parentRect, contents);
                    }
                    else
                    {
                        source = null;
                    }
                }
            }
        }

        protected abstract Vector2 CalculatePosition(ImGui gui, Rect targetRect, Vector2 contentSize);
        protected abstract bool ShoudBuild(ImGui source, Rect sourceRect, ImGui parent, Rect parentRect);
        protected abstract void BuildContents(ImGui gui);
    }

    public abstract class DropDownPanel : AttachedPanel
    {
        protected DropDownPanel(Padding padding, float width) : base(padding, width) {}
        protected override bool ShoudBuild(ImGui source, Rect sourceRect, ImGui parent, Rect parentRect) => source.IsLastMouseDown(sourceRect);
    }

    public abstract class Tooltip : AttachedPanel
    {
        protected Tooltip(Padding padding, float width) : base(padding, width)
        {
            contents.mouseCapture = false;
        }
        protected override bool ShoudBuild(ImGui source, Rect sourceRect, ImGui parent, Rect parentRect)
        {
            var window = source.window;
            if (InputSystem.Instance.mouseOverWindow != window)
                return false;
            return parentRect.Contains(parent.mousePosition);
        }

        protected override Vector2 CalculatePosition(ImGui gui, Rect targetRect, Vector2 contentSize)
        {
            var x = targetRect.Right + contentSize.X <= gui.contentSize.X ? targetRect.Right :
                targetRect.X >= contentSize.X ? targetRect.X - contentSize.X : (gui.contentSize.X - contentSize.X) / 2;
            var y = MathUtils.Clamp(targetRect.Y, 0f, gui.contentSize.Y - contentSize.Y);
            return new Vector2(x, y);
        }
    }
}