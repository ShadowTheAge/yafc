using System;
using System.Collections.Generic;
using System.Numerics;

namespace YAFC.UI.Table
{
    public class DataColumn<TData>
    {
        public readonly Action<ImGui, TData> build;
        public string header;
        public float width;
        public float minWidth;

        public DataColumn(string header, Action<ImGui, TData> build, float width)
        {
            this.header = header;
            this.build = build;
            this.width = width;
        }
    }
    
    public class DataGrid<TData>
    {
        private DataColumn<TData>[] columns;
        private Vector2 scroll;
        private Padding innerPadding = new Padding(0.2f);

        public DataGrid(DataColumn<TData>[] columns)
        {
            this.columns = columns;
        }

        public void BuildHeader(ImGui gui)
        {
            var spacing = innerPadding.left + innerPadding.right;
            var x = 0f;
            var topSeparator = gui.AllocateRect(0f, 0.2f);
            using (var group = gui.EnterManualPositioning(0f, 1f, innerPadding))
            {
                foreach (var column in columns)
                {
                    if (column.width < column.minWidth)
                        column.width = column.minWidth;
                    group.SetManualRect(new Rect(x, 0, column.width, 0f), RectAllocator.LeftRow);
                    gui.BuildText(column.header);
                    x += column.width + spacing;
                }
            }

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
            if (gui.action == ImGuiAction.Build)
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
            
            var isBuilding = gui.action == ImGuiAction.Build;
            var bottom = gui.statePosition.Bottom;
            var top = bottom;
            var x = 0f;
            foreach (var element in data)
            {
                x = 0f;
                using (var group = gui.EnterManualPositioning(0f, 0f, innerPadding))
                {
                    foreach (var column in columns)
                    {
                        if (column.width < column.minWidth)
                            column.width = column.minWidth;
                        group.SetManualRect(new Rect(x, 0, column.width, 0f), RectAllocator.LeftRow);
                        column.build(gui, element);
                        x += column.width + spacing;
                    }
                }
                bottom = gui.lastRect.Bottom;
                if (isBuilding)
                    gui.DrawRectangle(new Rect(0, bottom-0.1f, x, 0.2f), SchemeColor.Grey);
            }
            
            DrawVerticalGrid(gui, top, bottom);
            return new Rect(0, top, x, bottom-top);
        }
    }
}