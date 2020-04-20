using System;
using System.Numerics;

namespace YAFC.UI
{
    public abstract class VerticalScroll : Panel, IMouseScrollHandle
    {
        private ScrollbarHandle scrollbar;
        protected VerticalScroll(Vector2 size, RectAllocator allocator = RectAllocator.Stretch) : base(size, allocator)
        {
            subBatch.clip = true;
            scrollbar = new ScrollbarHandle(this);
            padding = new Padding(0f, 0.5f, 0f, 0f);
        }

        public virtual float scroll
        {
            get => -subBatch.offset.Y;
            set
            {
                var newPosition = MathUtils.Clamp(-value, -maxScroll, 0);
                if (newPosition != subBatch.offset.Y)
                {
                    subBatch.offset = new Vector2(0, newPosition);
                    Rebuild();
                }
            }
        }
        public float maxScroll { get; private set; }

        protected override void BuildBox(LayoutState state, Rect rect)
        {
            if (maxScroll > 0)
            {
                var scrollHeight = (size.Y * size.Y) / (size.Y + maxScroll);
                var scrollStart = (scroll / maxScroll) * (size.Y - scrollHeight);
                state.batch.DrawRectangle(new Rect(rect.Right - 0.5f, rect.Y + scrollStart, 0.5f, scrollHeight), SchemeColor.BackgroundAlt, RectangleBorder.None, scrollbar);
            }
            base.BuildBox(state, rect);
        }
        
        private void ScrollbarDrag(float delta)
        {
            scroll += delta * (size.Y + maxScroll) / size.Y;
        }

        public sealed override Vector2 BuildPanel(UiBatch batch, Vector2 size)
        {
            var state = new LayoutState(batch, size.X, defaultAllocator);
            BuildScrollContents(state);
            maxScroll = Math.Max(state.fullHeight - size.Y, 0f);
            scroll = scroll;
            return state.size;
        }

        protected abstract void BuildScrollContents(LayoutState state);

        public void Scroll(int delta, UiBatch batch)
        {
            scroll += delta * 3;
        }

        private class ScrollbarHandle : IMouseDragHandle
        {
            private readonly VerticalScroll scroll;
            private float dragPosition;
            public ScrollbarHandle(VerticalScroll scroll)
            {
                this.scroll = scroll;
            }

            public void MouseDown(Vector2 position, UiBatch batch)
            {
                dragPosition = position.Y;
            }

            public void Drag(Vector2 position, UiBatch batch)
            {
                var delta = position.Y - dragPosition;
                scroll.ScrollbarDrag(delta);
                dragPosition = position.Y;
            }

            public void EndDrag(Vector2 position, UiBatch batch) {}
            public void MouseEnter(HitTestResult<IMouseHandle> hitTest) {}
            public void MouseExit(UiBatch batch) {}
            public void MouseDown(Vector2 position, int button, UiBatch batch) {}
            public void MouseClick(int button, UiBatch batch) {}
        }
    }
}