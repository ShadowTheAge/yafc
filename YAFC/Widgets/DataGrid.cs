using System;
using System.Collections.Generic;
using System.Numerics;
using SDL2;

namespace YAFC.UI
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
        private readonly DataColumn<TData>[] columns;
        private readonly Padding innerPadding = new Padding(0.2f);
        public float width { get; private set; }
        private readonly float spacing;
        private Vector2 buildingStart;

        public DataGrid(DataColumn<TData>[] columns)
        {
            this.columns = columns;
            spacing = innerPadding.left + innerPadding.right;
        }

        public void BuildHeader(ImGui gui)
        {
            var spacing = innerPadding.left + innerPadding.right;
            var x = 0f;
            var topSeparator = gui.AllocateRect(0f, 0.1f);
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
                    x += column.width + spacing;
                }
            }
            width = x + 0.2f - spacing;

            var separator = gui.AllocateRect(x, 0.1f);
            if (gui.isBuilding)
            {
                topSeparator.Width = separator.Width;
                gui.DrawRectangle(topSeparator, SchemeColor.GreyAlt);
                gui.DrawRectangle(separator, SchemeColor.GreyAlt);
                //DrawVerticalGrid(gui, topSeparator.Bottom, separator.Top, SchemeColor.GreyAlt);
            }
        }

        public Rect BuildRow(ImGui gui, TData element, float startX = 0f)
        {
            var x = innerPadding.left;
            var rowColor = SchemeColor.None;
            var textColor = rowColor;

            using (var group = gui.EnterFixedPositioning(width, 0f, innerPadding, textColor))
            {
                foreach (var column in columns)
                {
                    if (column.width < column.minWidth)
                        column.width = column.minWidth;
                    @group.SetManualRect(new Rect(x, 0, column.width, 0f), RectAllocator.LeftRow);
                    column.build(gui, element);
                    x += column.width + spacing;
                }
            }

            var rect = gui.lastRect;
            var bottom = gui.lastRect.Bottom;
            if (gui.isBuilding)
                gui.DrawRectangle(new Rect(startX, bottom - 0.1f, x-startX, 0.1f), SchemeColor.Grey);
            return rect;
        }

        public void BeginBuildingContent(ImGui gui)
        {
            buildingStart = gui.statePosition.BottomLeft;
            gui.spacing = innerPadding.top + innerPadding.bottom;
        }

        public Rect EndBuildingContent(ImGui gui)
        {
            var bottom = gui.statePosition.Bottom;
            //DrawVerticalGrid(gui, buildingStart.Y, bottom);
            return new Rect(buildingStart.X, buildingStart.Y, width, bottom-buildingStart.Y);
        }

        public bool BuildContent(ImGui gui, IReadOnlyList<TData> data, out (TData from, TData to) reorder, out Rect rect)
        {
            BeginBuildingContent(gui);
            reorder = default;
            var hasReorder = false;
            for (var i = 0; i < data.Count; i++) // do not change to foreach
            {
                var t = data[i];
                var rowRect = BuildRow(gui, t);
                if (gui.DoListReordering(rowRect, rowRect, t, out var from))
                {
                    reorder = (@from, t);
                    hasReorder = true;
                }
            }

            rect = EndBuildingContent(gui);
            return hasReorder;
        }
    }
}