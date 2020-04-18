using System;
using System.Drawing;

namespace YAFC.UI
{
    public class SelectionButton : TextButton
    {
        public SelectionButton(Font font, string text, Action<UiBatch> clickCallback) : base(font, text, clickCallback) {}
        private bool _selected;

        public override SchemeColor boxColor => state == State.Over ? SchemeColor.BackgroundAlt : SchemeColor.Background;

        protected override void BuildBox(LayoutState state, RectangleF rect)
        {
            if (selected)
                state.batch.DrawRectangle(new RectangleF(rect.X, rect.Bottom-0.3f, rect.Width, 0.3f), SchemeColor.Primary);
            base.BuildBox(state, rect);
        }

        public bool selected
        {
            get => _selected;
            set
            {
                _selected = value;
                Rebuild();
            }
        }
    }

    public class CheckBox : WidgetContainer, IMouseClickHandle
    {
        private readonly FontString content;
        private bool _check;

        public bool check
        {
            get => _check;
            set
            {
                if (_check == value)
                    return;
                _check = value;
                Rebuild();
            }
        }
        

        public CheckBox(Font font, string text)
        {
            padding = default;
            content = new FontString(font, text);
        }
        
        public void MouseClickUpdateState(bool mouseOverAndDown, int button, UiBatch batch) {}

        public void MouseClick(int button, UiBatch batch)
        {
            check = !check;
        }

        protected override void BuildContent(LayoutState state)
        {
            using (state.EnterGroup(default, RectAllocator.LeftRow))
            {
                var rect = state.AllocateRect(1.5f, 1.5f);
                state.AllocateSpacing(0.5f);
                state.batch.DrawIcon(rect, _check ? Icon.CheckBoxCheck : Icon.CheckBoxEmpty, SchemeColor.BackgroundText);
                state.Build(content);
            }
        }
    }
}