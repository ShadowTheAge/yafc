using System;
using System.Drawing;

namespace YAFC.UI
{
    public enum RectAllocator
    {
        Stretch,
        LeftAlign,
        RightAlign,
        Center,
        LeftRow,
        RightRow,
        RemainigRow,
        FixedRect,
    }
    
    public readonly struct Padding
    {
        public readonly float left, right, top, bottom;

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
    
    public class LayoutState
    {
        public readonly UiBatch batch;
        private CopyableState state;
        public RectangleF lastRect { get; private set; }
        public float width => state.right - state.left;
        public float fullHeight => state.bottom;
        public ref RectAllocator allocator => ref state.allocator;
        public ref float spacing => ref state.spacing;

        public LayoutState(UiBatch batch, float sizeWidth, RectAllocator allocator)
        {
            this.batch = batch;
            state.right = sizeWidth;
            state.spacing = 0.5f;
            state.allocator = allocator;
        }

        public void AllocateSpacing(float spacing)
        {
            if (state.allocator == RectAllocator.LeftRow || state.allocator == RectAllocator.RightRow)
                AllocateRect(spacing, 0f);
            else AllocateRect(0f, spacing);
        }

        public void AllocateSpacing() => AllocateSpacing(state.spacing);

        public RectangleF AllocateRect(float width, float height, bool centrify = false)
        {
            lastRect = state.AllocateRect(width, height);
            state.EncapsulateRect(lastRect);
            if (centrify && (allocator == RectAllocator.Stretch || allocator == RectAllocator.LeftRow || allocator == RectAllocator.RightRow || allocator == RectAllocator.FixedRect))
                return new RectangleF(lastRect.X+(lastRect.Width-width)*0.5f, lastRect.Y+(lastRect.Height-height)*0.5f, width, height);
            return lastRect;
        }

        public LayoutState Build(IWidget widget)
        {
            widget.Build(this);
            AllocateSpacing();
            return this;
        }
        
        public LayoutState Build(IWidget widget, float spacing)
        {
            widget.Build(this);
            AllocateSpacing(spacing);
            return this;
        }

        public LayoutState Align(RectAllocator allocator)
        {
            this.allocator = allocator;
            return this;
        }

        public Context EnterGroup(Padding padding, RectAllocator allocator)
        {
            var ctx = new Context(this, padding);
            state.allocator = allocator;
            return ctx;
        }

        public Context EnterRow() => EnterGroup(default, RectAllocator.LeftRow);

        public Context EnterManualPositioning(float width, float height, Padding padding, out RectangleF rect)
        {
            var context = new Context(this, padding);
            rect = AllocateRect(width, height);
            return context;
        }
        
        private struct CopyableState
        {
            public RectAllocator allocator;
            public float left, right, top, bottom;
            public RectangleF contextRect;
            public float spacing;
            
            public RectangleF AllocateRect(float width, float height)
            {
                width = Math.Min(width, right - left);
                switch (allocator)
                {
                    case RectAllocator.Stretch:
                        return new RectangleF(left, top, right-left, height);
                    case RectAllocator.LeftAlign:
                        return new RectangleF(left, top, width, height);
                    case RectAllocator.RightAlign:
                        return new RectangleF(right-width, top, width, height);
                    case RectAllocator.Center:
                        return new RectangleF((right+left-width) * 0.5f, top, width, height);
                    case RectAllocator.LeftRow:
                        bottom = MathF.Max(bottom, top + height);
                        return new RectangleF(left, top, width, bottom - top);
                    case RectAllocator.RightRow:
                        bottom = MathF.Max(bottom, top + height);
                        return new RectangleF(right-width, top, width, bottom - top);
                    case RectAllocator.RemainigRow:
                        bottom = MathF.Max(bottom, top + height);
                        return new RectangleF(left, top, right-left, bottom - top);
                    case RectAllocator.FixedRect:
                        return new RectangleF(left, top, right-left, bottom - top);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            
            public void EncapsulateRect(RectangleF rect)
            {
                contextRect = contextRect == RectangleF.Empty ? rect : RectangleF.Union(contextRect, rect);
                switch (allocator)
                {
                    case RectAllocator.Stretch: case RectAllocator.RightAlign: case RectAllocator.LeftAlign: case RectAllocator.Center:
                        top = bottom = rect.Bottom;
                        break;
                    case RectAllocator.LeftRow:
                        left = rect.Right;
                        bottom = rect.Bottom;
                        break;
                    case RectAllocator.RightRow:
                        right = rect.Left;
                        bottom = rect.Bottom;
                        break;
                }
            }
        }

        public readonly struct Context : IDisposable
        {
            private readonly LayoutState layout;
            private readonly CopyableState state;
            private readonly Padding padding;

            public Context(LayoutState layout, Padding padding)
            {
                this.layout = layout;
                this.padding = padding;
                ref var cstate = ref layout.state;
                state = cstate;
                cstate.contextRect = RectangleF.Empty;
                cstate.left += padding.left;
                cstate.right -= padding.right;
                cstate.top += padding.top;
                cstate.bottom -= padding.bottom;
            }

            public void Dispose()
            {
                var rect = layout.state.contextRect;
                layout.state = state;
                rect.X -= padding.left;
                rect.Y -= padding.top;
                rect.Width += (padding.left + padding.right);
                rect.Height += (padding.top + padding.bottom);
                layout.lastRect = rect;
                layout.state.EncapsulateRect(rect);
            }

            public void SetManualRect(RectangleF rect)
            {
                ref var cstate = ref layout.state;
                cstate.left = rect.X + state.left + padding.left;
                cstate.right = cstate.left + rect.Width;
                cstate.top = rect.Y + state.top + padding.top;
                cstate.bottom = cstate.top + rect.Height;
                cstate.allocator = RectAllocator.FixedRect;
            }
        }
    }
}