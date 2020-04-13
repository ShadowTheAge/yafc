using System;
using System.Drawing;

namespace YAFC.UI
{
    public abstract class VerticalScroll : Panel, IMouseScrollHandle
    {
        private ScrollbarHandle scrollbar;
        protected VerticalScroll(SizeF size) : base(size)
        {
            subBatch.clip = true;
            scrollbar = new ScrollbarHandle(this);
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

        protected override void BuildBox(RenderBatch batch, RectangleF rect)
        {
            if (maxScroll > 0)
            {
                var scrollHeight = (size.Height * size.Height) / (size.Height + maxScroll);
                var scrollStart = (scroll / maxScroll) * (size.Height - scrollHeight);
                batch.DrawRectangle(new RectangleF(rect.Right - 0.5f, rect.Y + scrollStart, 0.5f, scrollHeight), SchemeColor.BackgroundAlt, RectangleShadow.None, scrollbar);
            }
            base.BuildBox(batch, rect);
        }
        
        private void ScrollbarDrag(float delta)
        {
            scroll += delta * (size.Height + maxScroll) / size.Height;
        }

        public sealed override LayoutPosition BuildPanel(RenderBatch batch, LayoutPosition location)
        {
            var result = BuildScrollContents(batch, location);
            maxScroll = Math.Max(result.y - size.Height, 0f);
            scroll = scroll;
            return result;
        }

        protected abstract LayoutPosition BuildScrollContents(RenderBatch batch, LayoutPosition position);

        public void Scroll(int delta, RenderBatch batch)
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

            public void MouseDown(PointF position, RenderBatch batch)
            {
                dragPosition = position.Y;
            }

            public void BeginDrag(PointF position, RenderBatch batch) {}

            public void Drag(PointF position, RenderBatch batch)
            {
                var delta = position.Y - dragPosition;
                scroll.ScrollbarDrag(delta);
                dragPosition = position.Y;
            }

            public void EndDrag(PointF position, RenderBatch batch) {}
        }
    }
}