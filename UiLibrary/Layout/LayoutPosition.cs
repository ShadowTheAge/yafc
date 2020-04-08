using System.Drawing;

namespace UI
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
    public struct LayoutPosition
    {
        public float y;
        public float x1, x2;
        public float width => x2 - x1;

        public RectangleF Rect(float width, float height, Alignment align)
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

        public RectangleF Rect(float height)
        {
            var result = new RectangleF(x1, y, x2-x1, height);
            y += height;
            return result;
        }

        public LayoutPosition(float y, float x1, float x2)
        {
            this.y = y;
            this.x1 = x1;
            this.x2 = x2;
        }

        public void PadLeft(RectangleF rect, float spacing = 0f)
        {
            x1 = rect.Right + spacing;
        }

        public void PadRight(RectangleF rect, float spacing = 0f)
        {
            x2 = rect.Left - spacing;
        }

        public void Pad(RectangleF rect, float spacing = 0f)
        {
            y = rect.Bottom + spacing;
        }

        public LayoutPosition LeftArea(float width)
        {
            var result = new LayoutPosition(y, x1, x1 + width);
            x1 += width;
            return result;
        }
        
        public LayoutPosition RightArea(float width)
        {
            var result = new LayoutPosition(y, x2 - width, x2);
            x2 -= width;
            return result;
        }

        public void Build(IWidget widget, RenderBatch batch, Alignment align, float spacing = 1f)
        {
            var rect = widget.Build(batch, this, align);
            Pad(rect, spacing);
        }
    }
}