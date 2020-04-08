using System.Drawing;

namespace UI
{
    public class VerticalScroll : WidgetContainer, IPanel
    {
        private RenderBatch subBatch;
        private float height;
        private IWidget content;

        public VerticalScroll(IWidget content, float height)
        {
            this.content = content;
            this.height = height;
            subBatch = new RenderBatch(this) {clip = true};
        }

        public override RectangleF BuildContent(RenderBatch batch, LayoutPosition location, Alignment align)
        {
            var contentRect = location.Rect(height);
            if (subBatch.dirty)
                content.Build(subBatch, new LayoutPosition(0, 0, location.width), align);
            batch.DrawSubBatch(contentRect, batch);
            return contentRect;
        }

        public RectangleF Build(RenderBatch batch)
        {
            
        }
    }
}