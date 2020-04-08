using System.Drawing;

namespace UI
{
    public interface IWidget
    {
        RectangleF Build(RenderBatch batch, LayoutPosition location, Alignment align);
    }

    public interface IPanel
    {
        RectangleF Build(RenderBatch batch);
    }

    public abstract class WidgetContainer : IWidget
    {
        private Padding _padding;
        private RenderBatch batch;

        public Padding padding
        {
            get => _padding;
            set
            {
                _padding = value;
                SetDirty();
            }
        }

        protected void SetDirty()
        {
            batch?.SetDirty();
        }

        public virtual RectangleF Build(RenderBatch batch, LayoutPosition location, Alignment align)
        {
            this.batch = batch;
            var paddedLine = new LayoutPosition(location.y + _padding.top, location.x1 + _padding.left, location.x2 - _padding.right);
            var subrect = BuildContent(batch, paddedLine, align);
            return new RectangleF(subrect.X - _padding.left, subrect.Y - _padding.top, subrect.Width + _padding.left + _padding.right, subrect.Height + _padding.top + _padding.bottom);
        }

        public abstract RectangleF BuildContent(RenderBatch batch, LayoutPosition location, Alignment align);
    }
}