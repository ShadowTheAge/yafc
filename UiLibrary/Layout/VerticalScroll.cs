using System;
using System.Collections.Generic;
using System.Drawing;

namespace UI
{
    public abstract class VerticalScroll : Panel, IMouseScrollHandle
    {
        protected VerticalScroll(SizeF size) : base(size)
        {
        }

        private float maxScroll;

        private void UpdateScrollPosition(float delta)
        {
            subBatch.offset = new SizeF(0, MathUtils.Clamp(subBatch.offset.Height - delta, -maxScroll, 0));
        }

        public sealed override LayoutPosition BuildPanel(RenderBatch batch, LayoutPosition location)
        {
            var result = BuildScrollContents(batch, location);
            maxScroll = Math.Max(result.y - size.Height, 0f);
            UpdateScrollPosition(0f);
            return result;
        }

        protected abstract LayoutPosition BuildScrollContents(RenderBatch batch, LayoutPosition position);

        public void Scroll(int delta)
        {
            UpdateScrollPosition(delta);
        }
    }

    public abstract class VerticalScrollList<T> : VerticalScroll
    {
        public readonly float spacing;
        private IEnumerable<T> _data;
        
        protected VerticalScrollList(SizeF size, float spacing = 0.5f) : base(size)
        {
            this.spacing = spacing;
        }

        public IEnumerable<T> data
        {
            get => _data;
            set
            {
                _data = value;
                RebuildContents();
            }
        }

        protected override LayoutPosition BuildScrollContents(RenderBatch batch, LayoutPosition position)
        {
            foreach (var element in data)
            {
                var builtPosition = BuildElement(element, batch, position);
                position.Pad(builtPosition, spacing);
            }
            return position;
        }

        protected abstract LayoutPosition BuildElement(T element, RenderBatch batch, LayoutPosition position);
    }

    public abstract class VerticalScrollGrid<T> : VerticalScroll
    {
        private readonly float elementWidth;
        private IEnumerable<T> _data;
        
        protected VerticalScrollGrid(SizeF size, float elementWidth) : base(size)
        {
            this.elementWidth = elementWidth;
        }
        
        public IEnumerable<T> data
        {
            get => _data;
            set
            {
                _data = value;
                RebuildContents();
            }
        }
        
        protected override LayoutPosition BuildScrollContents(RenderBatch batch, LayoutPosition position)
        {
            var elementsPerRow = Math.Max(MathUtils.Floor(position.width / elementWidth), 1);
            var maxY = position.y;
            var x = position.left;
            var rowId = -1;
            foreach (var element in data)
            {
                if (rowId++ == elementsPerRow)
                {
                    x = position.left;
                    rowId = 0;
                    position.y = maxY;
                }

                var nextX = x + elementWidth;
                var builtPosition = new LayoutPosition(position.y, x, nextX);
                x = nextX;
                var built = BuildElement(element, batch, builtPosition);
                maxY = MathF.Max(maxY, built.y);
            }

            position.y = maxY;
            return position;
        }
        
        protected abstract LayoutPosition BuildElement(T element, RenderBatch batch, LayoutPosition position);
    }
}