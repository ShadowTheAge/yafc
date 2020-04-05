using System;
using System.Drawing;

namespace UI
{
    public class Panel : Box
    {
        private float _maxWidth;
        public float width { get; private set; }
        public float height { get; private set; }
        private readonly RenderBatch batch;

        public Panel(float maxWidth, SchemeColor color, RectangleShadow shadow = RectangleShadow.None) : base(color, shadow)
        {
            _maxWidth = maxWidth;
            batch = new RenderBatch();
        }

        public float maxWidth
        {
            get => _maxWidth;
            set
            {
                _maxWidth = value;
                SetDirty();
            }
        }

        protected override float DrawContent(RenderBatch parentBatch, PointF position, float width)
        {
            var shouldWidth = MathF.Min(width, maxWidth); 
            if (dirty || this.width != shouldWidth)
            {
                batch.Clear();
                this.width = shouldWidth;
                height = base.DrawContent(batch, default, width);
            }
            
            var rect = new RectangleF(position.X + (width-shouldWidth)*0.5f, position.Y, shouldWidth, height);
            parentBatch.DrawSubBatch(rect, batch);
            return height;
        }
    }
}