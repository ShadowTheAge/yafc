using System;
using System.Collections.Generic;
using System.Drawing;

namespace YAFC.UI
{
    public interface IListView<in T>
    {
        LayoutPosition BuildElement(T element, RenderBatch batch, LayoutPosition position);
    }
    
    public class VirtualScrollList<TData, TView> : VerticalScroll where TView : IListView<TData>, new()
    {
        public readonly float elementHeight;
        public readonly int elementsPerRow;
        private readonly int maxRowsVisible;
        private int currentFirstRow;

        private IReadOnlyList<TData> _data = Array.Empty<TData>();
        private readonly TView[] bufferView;

        public IReadOnlyList<TData> data
        {
            get => _data;
            set
            {
                _data = value;
                RebuildContents();
            }
        }

        public int rowCount => elementsPerRow == 1 ? _data.Count : (_data.Count - 1) / elementsPerRow + 1;

        public VirtualScrollList(SizeF size, float elementHeight, int elementsPerRow = 1) : base(size)
        {
            this.elementHeight = elementHeight;
            this.elementsPerRow = elementsPerRow;
            maxRowsVisible = (MathUtils.Ceil(size.Height / elementHeight) + 1);
            bufferView = new TView[maxRowsVisible * elementsPerRow];
        }

        private void BuildSegment(int firstRow, LayoutPosition position, RenderBatch batch)
        {
            currentFirstRow = firstRow;
            var width = align == Alignment.Fill ? position.width : size.Width;
            var elementWidth = width / elementsPerRow;
            var index = firstRow * elementsPerRow;
            if (index >= _data.Count)
                return;
            var bufferIndex = index % bufferView.Length;
            var lastRow = firstRow + maxRowsVisible;
            for (var row = firstRow; row < lastRow; row++)
            {
                LayoutPosition buildPosition;
                buildPosition.y = position.y + row * elementHeight;
                buildPosition.left = position.left;
                for (var elem = 0; elem < elementsPerRow; elem++)
                {
                    buildPosition.right = buildPosition.left + elementWidth;
                    ref var view = ref bufferView[bufferIndex];
                    if (view == null)
                        view = new TView();
                    view.BuildElement(_data[index], batch, buildPosition);
                    if (++index >= _data.Count)
                        return;
                    if (++bufferIndex >= bufferView.Length)
                        bufferIndex = 0;
                    buildPosition.left = buildPosition.right;
                }
            }
        }

        protected override LayoutPosition BuildScrollContents(RenderBatch batch, LayoutPosition position)
        {
            var fullHeight = rowCount * elementHeight;
            var maxScroll = MathF.Max(0, fullHeight - size.Height);
            var scroll = MathF.Min(this.scroll, maxScroll);
            var firstRow = MathUtils.Floor(scroll / elementHeight);
            BuildSegment(firstRow, position, batch);
            position.y += fullHeight;
            return position;
        }

        public override float scroll
        {
            get => base.scroll;
            set
            {
                base.scroll = value;
                var firstRow = MathUtils.Floor(scroll / elementHeight);
                if (firstRow != currentFirstRow)
                    RebuildContents();
            }
        }
    }
}