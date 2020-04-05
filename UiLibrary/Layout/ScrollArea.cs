using System;
using System.Drawing;

namespace UI
{
    public class ScrollArea : WrapBox, IMouseScrollHandle
    {
        private readonly float viewportHeight;
        private float contentHeight;

        public ScrollArea(float viewportHeight, SchemeColor color, RectangleShadow shadow = RectangleShadow.None) : base(color, shadow)
        {
            this.viewportHeight = viewportHeight;
            batch.clip = true;
        }

        protected override float DrawContent(RenderBatch batch, PointF position, float width)
        {
            contentHeight = base.DrawContent(batch, position, width);
            batch.offset = ClampScroll(batch.offset);
            return viewportHeight;
        }

        private SizeF ClampScroll(SizeF val)
        {
            val.Width = 0f;
            var maxHeight = contentHeight - viewportHeight;
            if (maxHeight <= 0f)
                val.Height = 0f;
            else if (val.Height < 0f)
                val.Height = 0f;
            else if (val.Height > maxHeight)
                val.Height = maxHeight;
            return val;
        }

        public void Scroll(int delta)
        {
            var offset = batch.offset;
            offset.Height += delta;
            batch.offset = ClampScroll(offset);
        }
    }
}