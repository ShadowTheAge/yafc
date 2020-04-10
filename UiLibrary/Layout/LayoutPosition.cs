using System.Drawing;

namespace UI
{
    public struct Padding
    {
        public float left;
        public float right;
        public float top;
        public float bottom;

        public Padding(float allOffsets)
        {
            top = bottom = left = right = allOffsets;
        }

        public Padding(float leftRight, float topBottom)
        {
            left = right = leftRight;
            top = bottom = topBottom;
        }

        public Padding(float left, float right, float top, float bottom)
        {
            this.left = left;
            this.right = right;
            this.top = top;
            this.bottom = bottom;
        }
    }

    public enum Alignment
    {
        Fill,
        Left,
        Center,
        Right,
    }
    public struct LayoutPosition
    {
        public float y;
        public float x1, x2;
        public Alignment align;
        public float width => x2 - x1;

        public RectangleF IntoRect(float width, float height)
        {
            switch (align)
            {
                case Alignment.Fill: default:
                    break;
                case Alignment.Left:
                    x2 = x1 + width;
                    break;
                case Alignment.Right:
                    x1 = x2 - width;
                    break;
                case Alignment.Center:
                    x1 = (x2 + x1 - width) * 0.5f;
                    x2 = x1 + width;
                    break;
            }
            var result = new RectangleF(x1, y, x2-x1, height);
            y = result.Bottom;
            return result;
        }

        public RectangleF Rect(float height)
        {
            var result = new RectangleF(x1, y, x2-x1, height);
            y += height;
            return result;
        }

        public LayoutPosition(float y, float x1, float x2, Alignment align)
        {
            this.y = y;
            this.x1 = x1;
            this.x2 = x2;
            this.align = align;
        }

        public LayoutPosition(float width) : this(0f, 0f, width, Alignment.Fill) {}

        public void PadLeft(LayoutPosition rect, float spacing = 0f)
        {
            x1 = rect.x2 + spacing;
        }

        public void PadRight(LayoutPosition rect, float spacing = 0f)
        {
            x2 = rect.x1 - spacing;
        }

        public void Pad(LayoutPosition pos, float spacing = 0f)
        {
            y = pos.y + spacing;
        }

        public LayoutPosition LeftArea(float width)
        {
            var result = new LayoutPosition(y, x1, x1 + width, Alignment.Fill);
            x1 += width;
            return result;
        }
        
        public LayoutPosition RightArea(float width)
        {
            var result = new LayoutPosition(y, x2 - width, x2, Alignment.Fill);
            x2 -= width;
            return result;
        }

        public void Build(IWidget widget, RenderBatch batch, float spacing = 1f)
        {
            var rect = widget.Build(batch, this);
            Pad(rect, spacing);
        }

        public RectangleF GetRect(LayoutPosition from)
        {
            return new RectangleF(x1, from.y, x2-x1, y-from.y);
        }

        public LayoutPosition AddTopPadding(Padding padding)
        {
            return new LayoutPosition(y + padding.top, x1 + padding.left, x2 - padding.right, align);
        }

        public LayoutPosition AddBottomPadding(Padding padding)
        {
            return new LayoutPosition(y + padding.bottom, x1 - padding.left, x2 + padding.right, align);
        }

        public LayoutPosition Align(Alignment align)
        {
            var copy = this;
            copy.align = align;
            return copy;
        }
    }
}