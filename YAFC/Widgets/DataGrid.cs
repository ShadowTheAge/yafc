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
            using (var group = gui.EnterManualPositioning(0f, 0f, innerPadding, out _))
            {
                var x = 0f;
                foreach (var column in columns)
                {
                    if (column.width < column.minWidth)
                        column.width = column.minWidth;
                    group.SetManualRect(new Rect(x, 0, column.width, 0f), RectAllocator.LeftRow);
                    gui.BuildText(column.header);
                    x += column.width + spacing;
                }
            }
            DrawVerticalGrid(gui, gui.lastRect.Bottom);
        }

        private void DrawVerticalGrid(ImGui gui, float bottom)
        {
            var spacing = innerPadding.left + innerPadding.right;
            if (gui.action == ImGuiAction.Build)
            {
                var x = 0f;
                foreach (var column in columns)
                {
                    x += column.width + spacing;
                    gui.DrawRectangle(new Rect(x, 0, 0.1f, bottom), SchemeColor.Grey); 
                }
            }
        }

        public void BuildContent(ImGui gui, IEnumerable<TData> data)
        {
            gui.spacing = innerPadding.top + innerPadding.bottom;
            var spacing = innerPadding.left + innerPadding.right;
            
            var isBuilding = gui.action == ImGuiAction.Build;
            var bottom = gui.lastRect.Bottom;
            foreach (var element in data)
            {
                using (var group = gui.EnterManualPositioning(0f, 0f, innerPadding, out _))
                {
                    var x = 0f;
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
                    gui.DrawRectangle(new Rect(0, bottom+innerPadding.bottom, gui.lastRect.Width, 0.1f), SchemeColor.Grey);
            }

            DrawVerticalGrid(gui, bottom);
        }
    }
}