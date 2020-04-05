using System.Drawing;

namespace UI
{
    public class WrapBox : Box
    {
        public float width { get; private set; }
        public float height { get; private set; }
        protected readonly RenderBatch batch;

        public WrapBox(SchemeColor color, RectangleShadow shadow = RectangleShadow.None) : base(color, shadow)
        {
            batch = new RenderBatch();
        }

        protected override float DrawContent(RenderBatch parentBatch, PointF position, float width)
        {
            if (dirty || this.width != width)
            {
                batch.Clear();
                this.width = width;
                height = base.DrawContent(batch, default, width);
            }
            
            var rect = new RectangleF(position.X, position.Y, width, height);
            parentBatch.DrawSubBatch(rect, batch);
            return height;
        }
    }
}