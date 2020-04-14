public enum Alignment
{
    Fill,
    Left,
    Center,
    Right,
}

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

public struct LayoutPosition {}

/*using System;
using System.Drawing;

namespace YAFC.UI
{
    

    public struct LayoutPosition
    {
        public float y;
        public float left, right;
        public SizeF offset => new SizeF(left, y);
        public float width => right - left;

        public RectangleF IntoRect(float width, float height, Alignment align = Alignment.Fill)
        {
            width = Math.Min(width, right - left);
            switch (align)
            {
                case Alignment.Fill: default:
                    break;
                case Alignment.Left:
                    right = left + width;
                    break;
                case Alignment.Right:
                    left = right - width;
                    break;
                case Alignment.Center:
                    left = (right + left - width) * 0.5f;
                    right = left + width;
                    break;
            }
            var result = new RectangleF(left, y, right-left, height);
            y = result.Bottom;
            return result;
        }

        public RectangleF Rect(float height)
        {
            var result = new RectangleF(left, y, right-left, height);
            y += height;
            return result;
        }

        public LayoutPosition(float y, float left, float right)
        {
            this.y = y;
            this.left = left;
            this.right = right;
        }

        public LayoutPosition(float width) : this(0f, 0f, width) {}

        public void PadLeft(LayoutPosition rect, float spacing = 0f)
        {
            left = rect.right + spacing;
        }

        public void PadRight(LayoutPosition rect, float spacing = 0f)
        {
            right = rect.left - spacing;
        }

        public void Pad(LayoutPosition pos, float spacing = 0f)
        {
            y = pos.y + spacing;
        }

        public LayoutPosition LeftArea(float width)
        {
            var result = new LayoutPosition(y, left, left + width);
            left += width;
            return result;
        }
        
        public LayoutPosition RightArea(float width)
        {
            var result = new LayoutPosition(y, right - width, right);
            right -= width;
            return result;
        }

        public RectangleF LeftRect(float width, float height, float space = 0.5f)
        {
            var result = new RectangleF(left, y, width, height);
            left += width + space;
            return result;
        }
        
        public RectangleF RightRect(float width, float height, float space = 0.5f)
        {
            var start = right - width;
            right = start - space;
            return new RectangleF(start, y, width, height);
        }

        public void Build(IWidget widget, RenderBatch batch, float spacing = 1f)
        {
            var pos = widget.Build(batch, this);
            Pad(pos, spacing);
        }

        public RectangleF GetRect(LayoutPosition from)
        {
            return new RectangleF(left, from.y, right-left, y-from.y);
        }

        public LayoutPosition AddTopPadding(Padding padding)
        {
            return new LayoutPosition(y + padding.top, left + padding.left, right - padding.right);
        }

        public LayoutPosition AddBottomPadding(Padding padding)
        {
            return new LayoutPosition(y + padding.bottom, left - padding.left, right + padding.right);
        }

        public void Space(float space) => y += space;

        public RectangleF ToRect(float height)
        {
            return new RectangleF(left, y, right-left, height);
        }
    }
}*/