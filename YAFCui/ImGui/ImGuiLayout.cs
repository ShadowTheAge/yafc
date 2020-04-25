using System;
using System.Numerics;

namespace YAFC.UI
{
    public partial class ImGui
    {
        private CopyableState state;
        public Rect lastRect { get; private set; }
        public float width => state.right - state.left;
        public float fullHeight => state.bottom;
        public ref RectAllocator allocator => ref state.allocator;
        public ref float spacing => ref state.spacing;
        public Vector2 layoutSize => new Vector2(state.right, state.bottom);
        public Rect layoutRect => new Rect(state.left, state.top, state.bottom - state.top, state.right - state.left);

        private void ResetLayout()
        {
            state = default;
            lastRect = default;
            state.right = buildWidth;
            state.spacing = 0.5f;
        }

        public void AllocateSpacing(float spacing)
        {
            AllocateRect(0f, 0f, spacing);
        }

        public void AllocateSpacing() => AllocateSpacing(state.spacing);

        public Rect AllocateRect(float width, float height, float spacing = float.NegativeInfinity)
        {
            lastRect = state.AllocateRect(width, height, spacing);
            state.EncapsulateRect(lastRect);
            return lastRect;
        }

        public void EncapsulateRect(Rect rect)
        {
            lastRect = rect;
            state.EncapsulateRect(rect);
        }

        public Rect AllocateRect(float width, float height, RectAlignment alignment, float spacing = float.NegativeInfinity)
        {
            var bigRect = AllocateRect(width, height, spacing);
            if (alignment == RectAlignment.Full || allocator == RectAllocator.Center || allocator == RectAllocator.LeftAlign || allocator == RectAllocator.RightAlign)
                return bigRect;
            switch (alignment)
            {
                case RectAlignment.Middle:
                    return new Rect(bigRect.X + (bigRect.Width - width) * 0.5f, bigRect.Y + (bigRect.Height-height) * 0.5f, width, height);
                case RectAlignment.MiddleLeft:
                    return new Rect(bigRect.X, bigRect.Y + (bigRect.Height-height) * 0.5f, width, height);
                case RectAlignment.MiddleRight:
                    return new Rect(bigRect.X, bigRect.Y + (bigRect.Height-height) * 0.5f, width, height);
                default:
                    return bigRect;
            }
        }

        public ImGui Build(ImGui widget)
        {
            widget.Build(this);
            return this;
        }
        
        public ImGui Build(ImGui widget, float spacing)
        {
            var tmp = state.spacing;
            state.spacing = spacing;
            widget.Build(this);
            state.spacing = tmp;
            return this;
        }

        public ImGui RemainingRow(float spacing = float.NegativeInfinity)
        {
            state.AllocateSpacing(spacing);
            allocator = RectAllocator.RemainigRow;
            return this;
        }

        public Context EnterGroup(Padding padding, RectAllocator allocator, SchemeColor textColor = SchemeColor.None, float spacing = float.NegativeInfinity)
        {
            state.AllocateSpacing();
            var ctx = new Context(this, padding);
            state.allocator = allocator;
            if (!float.IsNegativeInfinity(spacing))
                state.spacing = spacing;
            if (textColor != SchemeColor.None)
                state.textColor = textColor;
            return ctx;
        }

        public Context EnterGroup(Padding padding, SchemeColor textColor = SchemeColor.None) => EnterGroup(padding, allocator, textColor);

        public Context EnterRow(float spacing = 0.5f, RectAllocator allocator = RectAllocator.LeftRow, SchemeColor textColor = SchemeColor.None) => EnterGroup(default, allocator, textColor, spacing);

        public Context EnterManualPositioning(float width, float height, Padding padding, out Rect rect)
        {
            var context = new Context(this, padding);
            rect = AllocateRect(width, height);
            return context;
        }
        
        private struct CopyableState
        {
            public RectAllocator allocator;
            public float left, right, top, bottom;
            public Rect contextRect;
            public float spacing;
            public bool hasContent;
            public SchemeColor textColor;

            public Rect AllocateRect(float width, float height, float spacing)
            {
                AllocateSpacing(spacing);
                width = Math.Min(width, right - left);
                switch (allocator)
                {
                    case RectAllocator.Stretch:
                        return new Rect(left, top, right-left, height);
                    case RectAllocator.LeftAlign:
                        return new Rect(left, top, width, height);
                    case RectAllocator.RightAlign:
                        return new Rect(right-width, top, width, height);
                    case RectAllocator.Center:
                        return new Rect((right+left-width) * 0.5f, top, width, height);
                    case RectAllocator.LeftRow:
                        bottom = MathF.Max(bottom, top + height);
                        return new Rect(left, top, width, bottom - top);
                    case RectAllocator.RightRow:
                        bottom = MathF.Max(bottom, top + height);
                        return new Rect(right-width, top, width, bottom - top);
                    case RectAllocator.RemainigRow:
                        bottom = MathF.Max(bottom, top + height);
                        return new Rect(left, top, right-left, bottom - top);
                    case RectAllocator.FixedRect:
                        return new Rect(left, top, right-left, bottom - top);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            public void AllocateSpacing(float amount = float.NegativeInfinity)
            {
                if (!hasContent)
                    return;
                if (float.IsNegativeInfinity(amount))
                    amount = spacing;
                switch (allocator)
                {
                    case RectAllocator.Stretch:
                    case RectAllocator.LeftAlign:
                    case RectAllocator.RightAlign:
                    case RectAllocator.Center:
                        top += amount;
                        break;
                    case RectAllocator.LeftRow:
                        left += amount;
                        break;
                    case RectAllocator.RightRow:
                        right -= amount;
                        break;
                }
            }
            
            public void EncapsulateRect(Rect rect)
            {
                contextRect = hasContent ? Rect.Union(contextRect, rect) : rect;
                hasContent = true;
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
            private readonly ImGui gui;
            private readonly CopyableState state;
            private readonly Padding padding;

            public Context(ImGui gui, Padding padding)
            {
                this.gui = gui;
                this.padding = padding;
                ref var cstate = ref gui.state;
                state = cstate;
                cstate.contextRect = default;
                cstate.hasContent = false;
                cstate.left += padding.left;
                cstate.right -= padding.right;
                cstate.top += padding.top;
                cstate.bottom -= padding.bottom;
            }

            public void Dispose()
            {
                var rect = gui.state.contextRect;
                gui.state = state;
                rect.X -= padding.left;
                rect.Y -= padding.top;
                rect.Width += (padding.left + padding.right);
                rect.Height += (padding.top + padding.bottom);
                gui.lastRect = rect;
                gui.state.EncapsulateRect(rect);
            }

            public void SetManualRect(Rect rect, RectAllocator allocator = RectAllocator.FixedRect)
            {
                ref var cstate = ref gui.state;
                cstate.left = rect.X + state.left + padding.left;
                cstate.right = cstate.left + rect.Width;
                cstate.top = rect.Y + state.top + padding.top;
                cstate.bottom = cstate.top + rect.Height;
                cstate.allocator = allocator;
            }
        }
    }
}