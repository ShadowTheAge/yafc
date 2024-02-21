using System.Numerics;

namespace YAFC.UI {
    public abstract class AttachedPanel {
        protected readonly ImGui contents;
        protected Rect sourceRect;
        protected ImGui source;
        private ImGui owner;
        protected float width;
        protected virtual SchemeColor background => SchemeColor.PureBackground;

        protected AttachedPanel(Padding padding, float width) {
            contents = new ImGui(BuildContents, padding) { boxColor = background, boxShadow = RectangleBorder.Thin };
            this.width = width;
        }

        public bool active => source != null;

        public virtual void SetFocus(ImGui source, Rect rect) {
            this.source = source;
            sourceRect = rect;
            contents.Rebuild();
            owner?.Rebuild();
        }

        public void Close() {
            source = null;
            owner?.Rebuild();
        }

        public void Build(ImGui gui) {
            owner = gui;
            if (source != null && gui.isBuilding) {
                var rect = source.TranslateRect(sourceRect, gui);
                if (ShoudBuild(source, sourceRect, gui, rect)) {
                    var contentSize = contents.CalculateState(width, gui.pixelsPerUnit);
                    var position = CalculatePosition(gui, rect, contentSize);
                    var parentRect = new Rect(position, contentSize);
                    gui.DrawPanel(parentRect, contents);
                }
                else {
                    source = null;
                }
            }
        }

        protected abstract Vector2 CalculatePosition(ImGui gui, Rect targetRect, Vector2 contentSize);
        protected abstract bool ShoudBuild(ImGui source, Rect sourceRect, ImGui parent, Rect parentRect);
        protected abstract void BuildContents(ImGui gui);
    }

    public abstract class DropDownPanel : AttachedPanel, IMouseFocus {
        private bool focused;
        protected DropDownPanel(Padding padding, float width) : base(padding, width) { }
        protected override bool ShoudBuild(ImGui source, Rect sourceRect, ImGui parent, Rect parentRect) => focused;
        public override void SetFocus(ImGui source, Rect rect) {
            InputSystem.Instance.SetMouseFocus(this);
            base.SetFocus(source, rect);
        }

        public bool FilterPanel(IPanel panel) {
            while (panel != null) {
                if (panel == contents)
                    return true;
                panel = panel.Parent;
            }

            return false;
        }

        public void FocusChanged(bool focused) {
            this.focused = focused;
            contents.parent?.Rebuild();
        }
    }

    public class SimpleDropDown : DropDownPanel {
        private GuiBuilder builder;

        public SimpleDropDown() : base(new Padding(1f), 20f) {
            contents.AddMessageHandler<ImGuiUtils.CloseDropdownEvent>(HandleDropdownClosed);
        }

        private bool HandleDropdownClosed(ImGuiUtils.CloseDropdownEvent _) {
            Close();
            return true;
        }

        public delegate void Builder(ImGui gui, ref bool closed);

        public void SetPadding(Padding padding) => contents.initialPadding = padding;

        public void SetFocus(ImGui source, Rect rect, GuiBuilder builder, float width = 20f) {
            this.width = width;
            this.builder = builder;
            base.SetFocus(source, rect);
        }

        protected override Vector2 CalculatePosition(ImGui gui, Rect targetRect, Vector2 contentSize) {
            var size = gui.contentSize;
            var targetY = targetRect.Bottom + contentSize.Y > size.Y && targetRect.Y >= contentSize.Y ? targetRect.Y - contentSize.Y : targetRect.Bottom;
            var x = MathUtils.Clamp(targetRect.X, 0, size.X - contentSize.X);
            var y = MathUtils.Clamp(targetY, 0, size.Y - contentSize.Y);
            return new Vector2(x, y);
        }

        protected override void BuildContents(ImGui gui) {
            gui.boxColor = SchemeColor.PureBackground;
            gui.textColor = SchemeColor.BackgroundText;
            if (builder != null)
                builder.Invoke(gui);
            else Close();
        }
    }

    public abstract class Tooltip : AttachedPanel {
        protected Tooltip(Padding padding, float width) : base(padding, width) {
            contents.mouseCapture = false;
        }
        protected override bool ShoudBuild(ImGui source, Rect sourceRect, ImGui parent, Rect parentRect) {
            var window = source.window;
            if (InputSystem.Instance.mouseOverWindow != window)
                return false;
            return parentRect.Contains(parent.mousePosition);
        }

        protected override Vector2 CalculatePosition(ImGui gui, Rect targetRect, Vector2 contentSize) {
            float x, y;
            if (targetRect.Bottom < 4) {
                y = MathUtils.Clamp(targetRect.Bottom, 0f, gui.contentSize.Y - contentSize.Y);
                x = MathUtils.Clamp(targetRect.X, 0f, gui.contentSize.X - contentSize.X);
            }
            else {
                x = targetRect.Right + contentSize.X <= gui.contentSize.X ? targetRect.Right :
                    targetRect.X >= contentSize.X ? targetRect.X - contentSize.X : (gui.contentSize.X - contentSize.X) / 2;
                y = MathUtils.Clamp(targetRect.Y, 0f, gui.contentSize.Y - contentSize.Y);
            }
            return new Vector2(x, y);
        }
    }

    public class SimpleTooltip : Tooltip {
        private GuiBuilder builder;
        public void Show(GuiBuilder builder, ImGui gui, Rect rect, float width = 30f) {
            this.width = width;
            this.builder = builder;
            base.SetFocus(gui, rect);
        }

        public SimpleTooltip() : base(new Padding(0.5f), 30f) { }

        protected override void BuildContents(ImGui gui) {
            gui.boxColor = SchemeColor.PureBackground;
            gui.textColor = SchemeColor.BackgroundText;
            builder?.Invoke(gui);
        }
    }
}
