using System;
using System.Collections.Generic;
using System.Numerics;
using SDL2;

namespace YAFC.UI.Table
{
    public class DataColumn<TData>
    {
        public readonly Action<ImGui, TData, int> build;
        public string header;
        public float width;
        public float minWidth;

        public DataColumn(string header, Action<ImGui, TData, int> build, float width)
        {
            this.header = header;
            this.build = build;
            this.width = width;
        }
    }
    
    public class DataGrid<TData>
    {
        private readonly DataColumn<TData>[] columns;
        private readonly Padding innerPadding = new Padding(0.2f);
        private readonly Func<TData, SchemeColor> rowColor;
        private float width;
        private List<(Rect, SchemeColor)> deferredRects = new List<(Rect, SchemeColor)>();

        public DataGrid(DataColumn<TData>[] columns, Func<TData, SchemeColor> rowColor)
        {
            this.columns = columns;
            this.rowColor = rowColor;
        }

        public void BuildHeader(ImGui gui)
        {
            var spacing = innerPadding.left + innerPadding.right;
            var x = 0f;
            var topSeparator = gui.AllocateRect(0f, 0.2f);
            var y = gui.statePosition.Y;
            using (var group = gui.EnterFixedPositioning(0f, 1f, innerPadding))
            {
                foreach (var column in columns)
                {
                    if (column.width < column.minWidth)
                        column.width = column.minWidth;
                    var rect = new Rect(x, y, column.width, 0f);
                    group.SetManualRectRaw(rect, RectAllocator.LeftRow);
                    gui.BuildText(column.header);
                    rect.Bottom = gui.statePosition.Y;
                    if (gui.action == ImGuiAction.MouseDown && gui.actionParameter == SDL.SDL_BUTTON_LEFT && gui.ConsumeMouseDown(rect))
                        MainScreen.Instance.imGuiDragHelper.BeginDraggingContent(gui, rect);
                    x += column.width + spacing;
                }
            }
            width = x + 0.2f - spacing;

            var separator = gui.AllocateRect(x+0.2f, 0.2f);
            if (gui.isBuilding)
            {
                topSeparator.Width = separator.Width;
                gui.DrawRectangle(topSeparator, SchemeColor.GreyAlt);
                gui.DrawRectangle(separator, SchemeColor.GreyAlt);
                DrawVerticalGrid(gui, topSeparator.Bottom, separator.Top, SchemeColor.GreyAlt);
            }
        }

        private void DrawVerticalGrid(ImGui gui, float top, float bottom, SchemeColor color = SchemeColor.Grey)
        {
            if (gui.isBuilding)
            {
                var spacing = innerPadding.left + innerPadding.right;
                var x = 0f;
                foreach (var column in columns)
                {
                    x += column.width + spacing;
                    gui.DrawRectangle(new Rect(x, top, 0.2f, bottom-top), color); 
                }
            }
        }

        public Rect BuildContent(ImGui gui, IEnumerable<TData> data)
        {
            gui.spacing = innerPadding.top + innerPadding.bottom;
            var spacing = innerPadding.left + innerPadding.right;
            deferredRects.Clear();
            
            var isBuilding = gui.isBuilding;
            var bottom = gui.statePosition.Bottom;
            var top = bottom;
            var x = innerPadding.left;
            var index = 0;
            foreach (var element in data)
            {
                x = innerPadding.left;
                var rowColor = SchemeColor.None;
                var textColor = rowColor;
                if (isBuilding && this.rowColor != null)
                {
                    rowColor = this.rowColor(element);
                    if (rowColor != SchemeColor.None)
                        textColor = rowColor + 2;
                }
                using (var group = gui.EnterFixedPositioning(width, 0f, innerPadding, textColor))
                {
                    foreach (var column in columns)
                    {
                        if (column.width < column.minWidth)
                            column.width = column.minWidth;
                        group.SetManualRect(new Rect(x, 0, column.width, 0f), RectAllocator.LeftRow);
                        column.build(gui, element, index);
                        x += column.width + spacing;
                    } 
                }

                MainScreen.Instance.imGuiDragHelper.TryDrag(gui, gui.lastRect, gui.lastRect);
                if (rowColor != SchemeColor.None)
                    deferredRects.Add((gui.lastRect, rowColor));
                bottom = gui.lastRect.Bottom;
                if (isBuilding)
                    gui.DrawRectangle(new Rect(0, bottom-0.1f, x, 0.2f), SchemeColor.Grey);
                index++;
            }
            
            DrawVerticalGrid(gui, top, bottom);

            if (deferredRects.Count > 0)
            {
                foreach (var (rect, color) in deferredRects)
                    gui.DrawRectangle(rect, color);
            }
            return new Rect(0, top, x, bottom-top);
        }
    }
}