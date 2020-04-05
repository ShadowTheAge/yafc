using System.Drawing;

namespace UI
{
    public class Box : LayoutContainer
    {
        private LayoutElement boxed;
        public SchemeColor color;
        public RectangleShadow shadow;

        public Box(SchemeColor color, RectangleShadow shadow = RectangleShadow.None)
        {
            this.color = color;
            this.shadow = shadow;
        }
        
        public override void AppendElement(LayoutElement child)
        {
            if (boxed != null)
                boxed.ClearParent(this);
            boxed = child;
            child.SetParent(this);
        }

        public override void RemoveElement(LayoutElement child)
        {
            if (boxed == child)
            {
                boxed = null;
                child.ClearParent(this);
            }
        }
        
        protected override float DrawContent(RenderBatch batch, PointF position, float width)
        {
            var height = boxed.DrawAndGetHeight(batch, position, width);
            var rect = new RectangleF(position, new SizeF(width, height));
            batch.DrawRectangle(rect, color, shadow);
            return height;
        }
    }
}