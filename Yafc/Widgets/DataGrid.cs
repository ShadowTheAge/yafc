using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using SDL2;

namespace Yafc.UI;
public abstract class DataColumn<TData> {
    public readonly float minWidth;
    public readonly float maxWidth;
    public bool isFixedSize => minWidth == maxWidth;
    private readonly Func<float> getWidth;
    private readonly Action<float> setWidth;
    private float _width;

    /// <param name="widthStorage">If not <see langword="null"/>, names a public read-write instance property in <see cref="Preferences"/> that will be used to store the width of this column.
    /// If the current value of the property is out of range, the initial width will be <paramref name="initialWidth"/>.</param>
    public DataColumn(float initialWidth, float minWidth = 0f, float maxWidth = 0f, string? widthStorage = null) {
        this.minWidth = minWidth == 0f ? initialWidth : minWidth;
        this.maxWidth = maxWidth == 0f ? initialWidth : maxWidth;
        if (widthStorage != null) {
            (getWidth, setWidth) = getStorage(widthStorage);
        }
        else {
            getWidth = () => _width;
            setWidth = f => _width = f;
        }

        if (width < this.minWidth || width > this.maxWidth) {
            width = initialWidth;
        }

        static (Func<float>, Action<float>) getStorage(string storage) {
            try {
                PropertyInfo? property = typeof(Preferences).GetProperty(storage);
                Func<float>? getMethod = property?.GetGetMethod()?.CreateDelegate<Func<float>>(Preferences.Instance);
                Action<float>? setMethod = property?.GetSetMethod()?.CreateDelegate<Action<float>>(Preferences.Instance);
                if (getMethod == null || setMethod == null) {
                    throw new ArgumentException($"'{storage}' is not a public read-write property in {nameof(Preferences)}.");
                }
                return (getMethod, setMethod);
            }
            catch (ArgumentException) {
                // Not including the CreateDelegate's exception, because YAFC displays only the innermost exception message.
                throw new ArgumentException($"'{storage}' is not a instance property of type {typeof(float).Name} in {nameof(Preferences)}.");
            }
        }
    }

    public float width {
        get => getWidth();
        set => setWidth(value);
    }

    public abstract void BuildHeader(ImGui gui);
    public abstract void BuildElement(ImGui gui, TData data);
}

/// <param name="widthStorage">If not <see langword="null"/>, names an instance property in <see cref="Preferences"/> that will be used to store the width of this column.
/// If the current value of the property is out of range, the initial width will be <paramref name="initialWidth"/>.</param>
public abstract class TextDataColumn<TData>(string header, float initialWidth, float minWidth = 0, float maxWidth = 0, bool hasMenu = false, string? widthStorage = null)
    : DataColumn<TData>(initialWidth, minWidth, maxWidth, widthStorage) {
    public override void BuildHeader(ImGui gui) {
        gui.BuildText(header);
        if (hasMenu) {
            var rect = gui.statePosition;
            Rect menuRect = new Rect(rect.Right - 1.7f, rect.Y, 1.5f, 1.5f);
            if (gui.isBuilding) {
                gui.DrawIcon(menuRect, Icon.DropDown, SchemeColor.BackgroundText);
            }

            if (gui.BuildButton(menuRect, SchemeColor.None, SchemeColor.Grey)) {
                gui.ShowDropDown(menuRect, BuildMenu, new Padding(1f));
            }
        }
    }

    public virtual void BuildMenu(ImGui gui) { }
}

public class DataGrid<TData> where TData : class {
    public readonly List<DataColumn<TData>> columns;
    private readonly Padding innerPadding = new Padding(0.2f);
    public float width { get; private set; }
    private readonly float spacing;
    private Vector2 buildingStart;
    private ImGui? contentGui;
    public float headerHeight = 1.3f;

    public DataGrid(params DataColumn<TData>[] columns) {
        this.columns = new List<DataColumn<TData>>(columns);
        spacing = innerPadding.left + innerPadding.right;
    }


    private void BuildHeaderResizer(ImGui gui, DataColumn<TData> column, Rect rect) {
        switch (gui.action) {
            case ImGuiAction.Build:
                float center = rect.X + (rect.Width * 0.5f);
                if (gui.IsMouseDown(rect, SDL.SDL_BUTTON_LEFT)) {
                    float unclampedWidth = gui.mousePosition.X - rect.Center.X + column.width;
                    float clampedWidth = MathUtils.Clamp(unclampedWidth, column.minWidth, column.maxWidth);
                    center = center - column.width + clampedWidth;
                }
                Rect viewRect = new Rect(center - 0.1f, rect.Y, 0.2f, rect.Height);
                gui.DrawRectangle(viewRect, SchemeColor.GreyAlt);
                break;
            case ImGuiAction.MouseMove:
                _ = gui.ConsumeMouseOver(rect, RenderingUtils.cursorHorizontalResize);
                if (gui.IsMouseDown(rect, SDL.SDL_BUTTON_LEFT)) {
                    gui.Rebuild();
                }

                break;
            case ImGuiAction.MouseDown:
                _ = gui.ConsumeMouseDown(rect, cursor: RenderingUtils.cursorHorizontalResize);
                break;
            case ImGuiAction.MouseUp:
                if (gui.ConsumeMouseUp(rect, false)) {
                    float unclampedWidth = gui.mousePosition.X - rect.Center.X + column.width;
                    column.width = MathUtils.Clamp(unclampedWidth, column.minWidth, column.maxWidth);
                    contentGui?.Rebuild();
                }
                break;
        }
    }

    private void CalculateWidth(ImGui gui) {
        float x = 0f;
        foreach (var column in columns) {
            x += column.width + spacing;
        }
        width = MathF.Max(x + 0.2f - spacing, gui.width - 1f);
    }

    public void BuildHeader(ImGui gui) {
        float spacing = innerPadding.left + innerPadding.right;
        float x = 0f;
        var topSeparator = gui.AllocateRect(0f, 0.1f);
        float y = gui.statePosition.Y;
        using (var group = gui.EnterFixedPositioning(0f, headerHeight, innerPadding)) {
            for (int index = 0; index < columns.Count; index++) // Do not change to foreach
            {
                var column = columns[index];
                if (column.width < column.minWidth) {
                    column.width = column.minWidth;
                }

                Rect rect = new Rect(x, y, column.width, 0f);
                @group.SetManualRectRaw(rect, RectAllocator.LeftRow);
                column.BuildHeader(gui);
                rect.Bottom = gui.statePosition.Y;
                x += column.width + spacing;

                if (!column.isFixedSize) {
                    BuildHeaderResizer(gui, column, new Rect(x - 0.7f, y, 1f, headerHeight + 0.9f));
                }
            }
        }
        CalculateWidth(gui);

        var separator = gui.AllocateRect(x, 0.1f);
        if (gui.isBuilding) {
            topSeparator.Width = separator.Width = width;
            gui.DrawRectangle(topSeparator, SchemeColor.GreyAlt);
            gui.DrawRectangle(separator, SchemeColor.GreyAlt);
            //DrawVerticalGrid(gui, topSeparator.Bottom, separator.Top, SchemeColor.GreyAlt);
        }
    }

    public Rect BuildRow(ImGui gui, TData element, float startX = 0f) {
        contentGui = gui;
        float x = innerPadding.left;
        var rowColor = SchemeColor.None;
        var textColor = rowColor;

        if (gui.ShouldBuildGroup(element, out var buildGroup)) {
            using (var group = gui.EnterFixedPositioning(width, 0f, innerPadding, textColor)) {
                foreach (var column in columns) {
                    if (column.width < column.minWidth) {
                        column.width = column.minWidth;
                    }

                    @group.SetManualRect(new Rect(x, 0, column.width, 0f), RectAllocator.LeftRow);
                    column.BuildElement(gui, element);
                    x += column.width + spacing;
                }
            }
            buildGroup.Complete();
        }

        CalculateWidth(gui);
        var rect = gui.lastRect;
        float bottom = gui.lastRect.Bottom;
        if (gui.isBuilding) {
            gui.DrawRectangle(new Rect(startX, bottom - 0.1f, width - startX, 0.1f), SchemeColor.Grey);
        }

        return rect;
    }

    public void BeginBuildingContent(ImGui gui) {
        buildingStart = gui.statePosition.BottomLeft;
        gui.spacing = innerPadding.top + innerPadding.bottom;
    }

    public Rect EndBuildingContent(ImGui gui) {
        float bottom = gui.statePosition.Bottom;
        return new Rect(buildingStart.X, buildingStart.Y, width, bottom - buildingStart.Y);
    }

    public bool BuildContent(ImGui gui, IReadOnlyList<TData> data, out (TData from, TData to) reorder, out Rect rect, Func<TData, bool>? filter = null) {
        BeginBuildingContent(gui);
        reorder = default;
        bool hasReorder = false;
        for (int i = 0; i < data.Count; i++) // do not change to foreach
        {
            var t = data[i];
            if (filter != null && !filter(t)) {
                continue;
            }

            var rowRect = BuildRow(gui, t);
            if (!hasReorder && gui.DoListReordering(rowRect, rowRect, t, out var from, SchemeColor.PureBackground, false)) {
                reorder = (@from, t);
                hasReorder = true;
            }
        }

        rect = EndBuildingContent(gui);
        return hasReorder;
    }
}
