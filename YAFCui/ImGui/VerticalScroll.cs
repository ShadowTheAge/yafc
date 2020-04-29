using System;
using System.Collections.Generic;
using System.Numerics;
using SDL2;

namespace YAFC.UI
{
    public abstract class VerticalScroll : IGui
    {
        protected readonly ImGui contents;
        protected readonly float height;

        protected float contentHeight;
        protected float maxScroll;

        public VerticalScroll(float height)
        {
            contents = new ImGui(this, new Padding(1f), clip:true);
            this.height = height;
        }
        
        public void Build(ImGui gui)
        {
            if (gui == contents)
                BuildContents(gui);
            else
            {
                var rect = gui.AllocateRect(gui.width, height);
                if (gui.action == ImGuiAction.Build)
                {
                    var innerRect = rect;
                    innerRect.Width -= 0.5f;
                    contentHeight = MeasureContent(innerRect, gui);
                    maxScroll = MathF.Max(contentHeight - height, 0f);
                    gui.DrawPanel(innerRect, contents);
                    scroll = MathUtils.Clamp(scroll, 0f, maxScroll);
                    contents.offset = new Vector2(0, -scroll);
                }
                
                var fullScrollRect = new Rect(rect.Right-0.5f, rect.Y, 0.5f, rect.Width);
                
                var scrollHeight = (height * height) / (height + maxScroll);
                var scrollStart = (scroll / maxScroll) * (height - scrollHeight);
                var scrollRect = new Rect(rect.Right - 0.5f, rect.Y + scrollStart, 0.5f, scrollHeight);
                
                switch (gui.action)
                {
                    case ImGuiAction.MouseDown:
                        if (scrollRect.Contains(gui.mousePosition))
                            gui.ConsumeMouseDown(fullScrollRect);
                        break;
                    case ImGuiAction.MouseMove:
                        if (gui.IsMouseDown(fullScrollRect, SDL.SDL_BUTTON_LEFT))
                            scroll += InputSystem.Instance.mouseDelta.Y * (height + maxScroll) / height;
                        break;
                    case ImGuiAction.Build:
                        gui.DrawRectangle(scrollRect, gui.IsMouseDown(fullScrollRect, SDL.SDL_BUTTON_LEFT) ? SchemeColor.GreyAlt : SchemeColor.Grey);
                        break;
                    case ImGuiAction.MouseScroll:
                        if (gui.ConsumeEvent(rect))
                            scroll += gui.actionParameter * 3f;
                        break;
                }
            }
        }

        private float _scroll;

        public virtual float scroll
        {
            get => _scroll;
            set
            {
                value = MathUtils.Clamp(value, 0f, maxScroll);
                if (value != _scroll)
                {
                    _scroll = value;
                    contents.parent?.Rebuild();
                }
            }
        }

        protected virtual float MeasureContent(Rect rect, ImGui gui)
        {
            return contents.CalculateState(rect.Width, gui.pixelsPerUnit).Y;
        }

        protected abstract void BuildContents(ImGui gui);
    }

    public class VirtualScrollList<TData> : VerticalScroll
    {
        private readonly Vector2 elementSize;
        protected readonly int bufferRows;
        protected int firstVisibleBlock;
        protected int elementsPerRow;
        private IReadOnlyList<TData> _data = Array.Empty<TData>();
        private readonly int maxRowsVisible;
        private readonly Drawer drawer;

        public delegate void Drawer(ImGui gui, TData element, int index);

        public IReadOnlyList<TData> data
        {
            get => _data;
            set
            {
                _data = value ?? Array.Empty<TData>();
                contents.Rebuild();
            }
        }

        public VirtualScrollList(float height, Vector2 elementSize, Drawer drawer, int bufferRows = 4) : base(height)
        {
            this.elementSize = elementSize;
            maxRowsVisible = MathUtils.Ceil(height / this.elementSize.Y) + bufferRows + 1;
            this.bufferRows = bufferRows;
            this.drawer = drawer;
        }

        private int CalcFirstBlock() => Math.Max(0, MathUtils.Floor((scroll - contents.initialPadding.top) / (elementSize.Y * bufferRows)));

        public override float scroll
        {
            get => base.scroll;
            set
            {
                base.scroll = value;
                var row = CalcFirstBlock();
                if (row != firstVisibleBlock)
                    contents.Rebuild();
            }
        }

        protected override void BuildContents(ImGui gui)
        {
            elementsPerRow = MathUtils.Floor(gui.width / elementSize.X);
            if (elementsPerRow < 1)
                elementsPerRow = 1;
            var rowCount = (_data.Count - 1) / elementsPerRow + 1;
            firstVisibleBlock = CalcFirstBlock();
            var firstRow = firstVisibleBlock * bufferRows;
            var index = firstRow * elementsPerRow;
            if (index >= _data.Count)
                return;
            var lastRow = firstRow + maxRowsVisible;
            using (var manualPlacing = gui.EnterManualPositioning(gui.width, rowCount * elementSize.Y, default, out var rect))
            {
                var elementWidth = rect.Width / elementsPerRow;
                var cell = new Rect(0f, 0f, elementWidth, elementSize.Y);
                for (var row = firstRow; row < lastRow; row++)
                {
                    cell.Y = row * elementSize.Y;
                    for (var elem = 0; elem < elementsPerRow; elem++)
                    {
                        cell.X = elem * elementWidth;
                        manualPlacing.SetManualRect(cell);
                        BuildElement(gui, _data[index], index);
                        if (++index >= _data.Count)
                            return;
                    }
                }
            }
        }

        protected virtual void BuildElement(ImGui gui, TData element, int index)
        {
            drawer(gui, element, index);
        }
    }
}