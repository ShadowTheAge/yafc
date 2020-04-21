using System;
using System.Collections.Generic;
using System.Numerics;

namespace YAFC.UI
{
    public interface IListView<in T>
    {
        void BuildElement(T element, LayoutState state);
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
                Rebuild();
            }
        }

        public int rowCount => elementsPerRow == 1 ? _data.Count : (_data.Count - 1) / elementsPerRow + 1;

        public VirtualScrollList(Vector2 size, float elementHeight, int elementsPerRow = 1) : base(size)
        {
            this.elementHeight = elementHeight;
            this.elementsPerRow = elementsPerRow;
            maxRowsVisible = (MathUtils.Ceil(size.Y / elementHeight) + 1);
            bufferView = new TView[maxRowsVisible * elementsPerRow];
        }

        private void BuildSegment(int firstRow, LayoutState state, float fullHeight)
        {
            currentFirstRow = firstRow;
            var width = state.allocator == RectAllocator.Stretch ? state.width : size.X;
            var elementWidth = width / elementsPerRow;
            var index = firstRow * elementsPerRow;
            if (index >= _data.Count)
                return;
            var bufferIndex = index % bufferView.Length;
            var lastRow = firstRow + maxRowsVisible;
            using (var manualPlacing = state.EnterManualPositioning(width, fullHeight, padding, out _))
            {
                var cell = new Rect(0f, 0f, elementWidth, elementHeight);
                for (var row = firstRow; row < lastRow; row++)
                {
                    cell.Y = row * elementHeight;
                    for (var elem = 0; elem < elementsPerRow; elem++)
                    {
                        cell.X = elem * elementWidth;
                        manualPlacing.SetManualRect(cell);
                        ref var view = ref bufferView[bufferIndex];
                        if (view == null)
                            view = new TView();
                        state.allocator = RectAllocator.LeftRow;
                        view.BuildElement(_data[index], state);
                        if (++index >= _data.Count)
                            return;
                        if (++bufferIndex >= bufferView.Length)
                            bufferIndex = 0;
                    }
                }
            }
        }

        protected override void BuildScrollContents(LayoutState state)
        {
            var fullHeight = rowCount * elementHeight;
            var maxScroll = MathF.Max(0, fullHeight - size.Y);
            var scroll = MathF.Min(this.scroll, maxScroll);
            var firstRow = MathUtils.Floor(scroll / elementHeight);
            BuildSegment(firstRow, state, fullHeight);
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