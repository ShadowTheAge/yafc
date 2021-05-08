using System;
using System.Collections.Generic;
using System.Numerics;
using SDL2;

namespace YAFC.UI
{
    public class DataColumn<TData>
    {
        public readonly Action<ImGui, TData> build;
        public readonly SimpleDropDown.Builder menuBuilder;
        public readonly string header;
        public readonly float minWidth;
        public readonly float maxWidth;
        public readonly bool isFixedSize;
        public float width;

        public DataColumn(string header, Action<ImGui, TData> build, SimpleDropDown.Builder menuBuilder, float width, float minWidth = 0f, float maxWidth = 0f)
        {
            this.build = build;
            this.menuBuilder = menuBuilder;
            
            this.header = header;
            this.width = width;
            this.minWidth = minWidth == 0f ? width : minWidth;
            this.maxWidth = maxWidth == 0f ? width : maxWidth;
            isFixedSize = minWidth == maxWidth;
        }
    }
    
    public class DataGrid<TData> where TData:class
    {
        private readonly DataColumn<TData>[] columns;
        private readonly Padding innerPadding = new Padding(0.2f);
        public float width { get; private set; }
        private readonly float spacing;
        private Vector2 buildingStart;
        private ImGui contentGui;

        public DataGrid(DataColumn<TData>[] columns)
        {
            this.columns = columns;
            spacing = innerPadding.left + innerPadding.right;
        }

        private void BuildHeaderResizer(ImGui gui, DataColumn<TData> column, Rect rect)
        {
            switch (gui.action)
            {
                case ImGuiAction.Build:
                    var center = rect.X + rect.Width * 0.5f;
                    if (gui.IsMouseDown(rect, SDL.SDL_BUTTON_LEFT))
                    {
                        var unclampedWidth = gui.mousePosition.X - rect.Center.X + column.width;
                        var clampedWidth = MathUtils.Clamp(unclampedWidth, column.minWidth, column.maxWidth);
                        center = center - column.width + clampedWidth;
                    }
                    var viewRect = new Rect(center - 0.1f, rect.Y, 0.2f, rect.Height);
                    gui.DrawRectangle(viewRect, SchemeColor.GreyAlt);
                    break;
                case ImGuiAction.MouseMove:
                    gui.ConsumeMouseOver(rect, RenderingUtils.cursorHorizontalResize);
                    if (gui.IsMouseDown(rect, SDL.SDL_BUTTON_LEFT))
                        gui.Rebuild();
                    break;
                case ImGuiAction.MouseDown:
                    gui.ConsumeMouseDown(rect, cursor:RenderingUtils.cursorHorizontalResize);
                    break;
                case ImGuiAction.MouseUp:
                    if (gui.ConsumeMouseUp(rect, false))
                    {
                        var unclampedWidth = gui.mousePosition.X - rect.Center.X + column.width;
                        column.width = MathUtils.Clamp(unclampedWidth, column.minWidth, column.maxWidth);
                        contentGui?.Rebuild();
                    }
                    break;
            }
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

                    if (!column.isFixedSize)
                    {
                        BuildHeaderResizer(gui, column, new Rect(x-0.7f, y, 1f, 2.2f));
                    }

                    if (column.menuBuilder != null)
                    {
                        var menuRect = new Rect(rect.Right-1.7f, rect.Y + 0.3f, 1.5f, 1.5f);
                        if (gui.isBuilding)
                            gui.DrawIcon(menuRect, Icon.DropDown, SchemeColor.BackgroundText);
                        if (gui.BuildButton(menuRect, SchemeColor.None, SchemeColor.Grey))
                            gui.ShowDropDown(menuRect, column.menuBuilder, new Padding(1f));
                    }
                }
            }
            width = x + 0.2f - spacing;

            var separator = gui.AllocateRect(x, 0.1f);
            if (gui.isBuilding)
            {
                topSeparator.Width = separator.Width = width;
                gui.DrawRectangle(topSeparator, SchemeColor.GreyAlt);
                gui.DrawRectangle(separator, SchemeColor.GreyAlt);
                //DrawVerticalGrid(gui, topSeparator.Bottom, separator.Top, SchemeColor.GreyAlt);
            }
        }

        public Rect BuildRow(ImGui gui, TData element, float startX = 0f)
        {
            contentGui = gui;
            var x = innerPadding.left;
            var rowColor = SchemeColor.None;
            var textColor = rowColor;

            if (gui.ShouldBuildGroup(element, out var buildGroup))
            {
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
                buildGroup.Complete();
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