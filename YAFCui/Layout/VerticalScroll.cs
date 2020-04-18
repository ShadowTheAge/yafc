using System;
using System.Drawing;

namespace YAFC.UI
{
    public abstract class VerticalScroll : Panel, IMouseScrollHandle
    {
        private ScrollbarHandle scrollbar;
        protected VerticalScroll(SizeF size, RectAllocator allocator = RectAllocator.Stretch) : base(size, allocator)
        {
            subBatch.clip = true;
            scrollbar = new ScrollbarHandle(this);
            padding = new Padding(0f, 0.5f, 0f, 0f);
        }

        public virtual float scroll
        {
            get => -subBatch.offset.Height;
            set
            {
                var newPosition = MathUtils.Clamp(-value, -maxScroll, 0);
                if (newPosition != subBatch.offset.Height)
                {
                    subBatch.offset = new SizeF(0, newPosition);
                    Rebuild();
                }
            }
        }
        public float maxScroll { get; private set; }

        protected override void BuildBox(LayoutState state, RectangleF rect)
        {
            if (maxScroll > 0)
            {
                var scrollHeight = (size.Height * size.Height) / (size.Height + maxScroll);
                var scrollStart = (scroll / maxScroll) * (size.Height - scrollHeight);
                state.batch.DrawRectangle(new RectangleF(rect.Right - 0.5f, rect.Y + scrollStart, 0.5f, scrollHeight), SchemeColor.BackgroundAlt, RectangleBorder.None, scrollbar);
            }
            base.BuildBox(state, rect);
        }
        
        private void ScrollbarDrag(float delta)
        {
            scroll += delta * (size.Height + maxScroll) / size.Height;
        }

        public sealed override void BuildPanel(LayoutState state)
        {
            BuildScrollContents(state);
            maxScroll = Math.Max(state.fullHeight - size.Height, 0f);
            scroll = scroll;
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

            public void MouseDown(PointF position, UiBatch batch)
            {
                dragPosition = position.Y;
            }

            public void BeginDrag(PointF position, UiBatch batch) {}

            public void Drag(PointF position, UiBatch batch)
            {
                var delta = position.Y - dragPosition;
                scroll.ScrollbarDrag(delta);
                dragPosition = position.Y;
            }

            public void EndDrag(PointF position, UiBatch batch) {}
        }
    }
}