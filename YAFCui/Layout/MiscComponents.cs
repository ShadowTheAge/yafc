using System;
using System.Collections.Generic;
using System.Numerics;

namespace YAFC.UI
{
    public class SelectionButton : TextButton
    {
        public SelectionButton(Font font, string text, Action<UiBatch> clickCallback) : base(font, text, clickCallback) {}
        private bool _selected;

        public override SchemeColor boxColor => over ? SchemeColor.Grey : SchemeColor.None;

        protected override void BuildBox(LayoutState state, Rect rect)
        {
            if (selected)
                state.batch.DrawRectangle(new Rect(rect.X, rect.Bottom-0.3f, rect.Width, 0.3f), SchemeColor.Primary);
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

    public class CheckBox : WidgetContainer, IMouseHandle
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

        public void MouseEnter(HitTestResult<IMouseHandle> hitTest) {}
        public void MouseExit(UiBatch batch) {}

        public void MouseClick(int button, UiBatch batch)
        {
            check = !check;
        }

        protected override void BuildContent(LayoutState state)
        {
            using (state.EnterGroup(default, RectAllocator.LeftRow))
            {
                var rect = state.AllocateRect(1.5f, 1.5f);
                state.batch.DrawIcon(rect, _check ? Icon.CheckBoxCheck : Icon.CheckBoxEmpty, SchemeColor.BackgroundText);
                state.Build(content, 0.5f);
            }
        }
    }

    public class SimpleList<TData, TView> : WidgetContainer where TView : IListView<TData>, new()
    {
        private readonly List<TView> views = new List<TView>();
        private IEnumerable<TData> _data = Array.Empty<TData>();
        private readonly float spacing;

        public SimpleList(float spacing = 0f)
        {
            padding = default;
            this.spacing = spacing;
        }
        public IEnumerable<TData> data
        {
            get => _data;
            set
            {
                _data = value;
                Rebuild();
            }
        }
        protected override void BuildContent(LayoutState state)
        {
            state.spacing = spacing;
            var index = 0;
            foreach (var elem in _data)
            {
                if (views.Count == index)
                    views.Add(new TView());
                views[index++].BuildElement(elem, state);
            }
        }
    }
}