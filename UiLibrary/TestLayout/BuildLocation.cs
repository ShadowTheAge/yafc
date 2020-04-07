using System.Drawing;

namespace UI.TestLayout
{
    public struct Padding
    {
        public float top;
        public float bottom;
        public float left;
        public float right;
    }

    public enum Alignment
    {
        Fill,
        Left,
        Center,
        Right
    }
    public struct BuildLocation
    {
        public Alignment align;
        public float y;
        public float x1, x2;
        public float width => x2 - x1;

        public RectangleF Rect(float width, float height)
        {
            var result = new RectangleF(x1, y, width, height);
            y += height;
            switch (align)
            {
                case Alignment.Fill:
                    result.Width = x2 - x1;
                    return result;
                case Alignment.Left: default:
                    return result;
                case Alignment.Right:
                    result.X = x2 - width;
                    return result;
                case Alignment.Center:
                    result.X = 0.5f * (x1 + x2 - width);
                    return result;
            }
        }

        public BuildLocation(Alignment align, float y, float x1, float x2)
        {
            this.y = y;
            this.x1 = x1;
            this.x2 = x2;
            this.align = align;
        }
    }

    public interface IWidget
    {
        RectangleF Build(RenderBatch batch, BuildLocation location);
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

        public virtual RectangleF Build(RenderBatch batch, BuildLocation location)
        {
            this.batch = batch;
            var paddedLine = new BuildLocation(location.align, location.y + _padding.top, location.x1 + _padding.left, location.x2 - _padding.right);
            var subrect = BuildContent(batch, paddedLine);
            return new RectangleF(subrect.X - _padding.left, subrect.Y - _padding.top, subrect.Width + _padding.left + _padding.right, subrect.Height + _padding.top + _padding.bottom);
        }

        public abstract RectangleF BuildContent(RenderBatch batch, BuildLocation location);
    }
}