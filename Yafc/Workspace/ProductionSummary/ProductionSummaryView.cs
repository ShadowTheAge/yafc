using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Yafc.Model;
using Yafc.UI;

namespace Yafc;

public class ProductionSummaryView : ProjectPageView<ProductionSummary> {
    private readonly DataGrid<ProductionSummaryEntry> grid;
    private readonly FlatHierarchy<ProductionSummaryEntry, ProductionSummaryGroup> flatHierarchy;
    private Goods? filteredGoods;
    private readonly Dictionary<ProductionSummaryColumn, GoodsColumn> goodsToColumn = [];
    private readonly PaddingColumn padding;
    private readonly SummaryColumn firstColumn;
    private readonly RestGoodsColumn lastColumn;

    public ProductionSummaryView() {
        padding = new PaddingColumn(this);
        firstColumn = new SummaryColumn(this);
        lastColumn = new RestGoodsColumn(this);
        grid = new DataGrid<ProductionSummaryEntry>(padding, firstColumn, lastColumn) { headerHeight = 4.2f };
        flatHierarchy = new FlatHierarchy<ProductionSummaryEntry, ProductionSummaryGroup>(grid, null, buildExpandedGroupRows: true);
    }

    private class PaddingColumn(ProductionSummaryView view) : DataColumn<ProductionSummaryEntry>(3f) {
        public override void BuildHeader(ImGui gui) { }

        public override void BuildElement(ImGui gui, ProductionSummaryEntry row) {
            gui.allocator = RectAllocator.Center;
            gui.spacing = 0f;
            if (row.subgroup != null) {
                if (gui.BuildButton(row.subgroup.expanded ? Icon.ShevronDown : Icon.ShevronRight)) {
                    row.subgroup.RecordChange().expanded = !row.subgroup.expanded;
                    view.flatHierarchy.SetData(view.model.group);
                }
            }
        }
    }

    private class SummaryColumn(ProductionSummaryView view) : DataColumn<ProductionSummaryEntry>(20f, 10f, 30f) {
        private readonly ProductionSummaryView view = view;
        private SearchQuery productionTableSearchQuery;

        public override void BuildHeader(ImGui gui) => BuildButtons(gui, 2f, view.model.group);

        private void BuildButtons(ImGui gui, float size, ProductionSummaryGroup group) {
            using (gui.EnterRow()) {
                if (gui.BuildButton(Icon.Plus, SchemeColor.Primary, SchemeColor.PrimaryAlt, SchemeColor.PrimaryAlt, size)) {
                    SearchableList<ProjectPage> pagesDropdown = new(30f, new Vector2(20f, 2f), PagesDropdownDrawer(group), PagesDropdownFilter) {
                        data = Project.current.pages.Where(x => x.content is ProductionTable).ToArray(),
                        filter = productionTableSearchQuery = new SearchQuery()
                    };
                    gui.ShowDropDown(AddProductionTableDropdown(pagesDropdown));
                }

                if (gui.BuildButton(Icon.Folder, SchemeColor.Primary, SchemeColor.PrimaryAlt, SchemeColor.PrimaryAlt, size)) {
                    ProductionSummaryEntry entry = new ProductionSummaryEntry(group);
                    entry.subgroup = new ProductionSummaryGroup(entry);
                    group.RecordUndo().elements.Add(entry);
                }
            }
        }

        private GuiBuilder AddProductionTableDropdown(SearchableList<ProjectPage> pagesDropdown) => gui => {
            using (gui.EnterGroup(new Padding(1f))) {
                if (gui.BuildSearchBox(productionTableSearchQuery, out productionTableSearchQuery)) {
                    pagesDropdown.filter = productionTableSearchQuery;
                }
            }
            pagesDropdown.Build(gui);
        };

        public override void BuildElement(ImGui gui, ProductionSummaryEntry entry) {
            gui.allocator = RectAllocator.LeftAlign;

            if (entry.subgroup != null) {
                if (entry.subgroup.expanded) {
                    BuildButtons(gui, 1.5f, entry.subgroup);
                }
                else {
                    if (gui.BuildTextInput(entry.subgroup.name, out string newText, "Group name", delayed: true)) {
                        entry.subgroup.RecordUndo().name = newText;
                    }
                }
            }
            else if (entry.page != null) { // The constructor should have thrown if this check fails, but it helps the nullability analysis
                using (gui.EnterGroup(new Padding(0.3f), RectAllocator.LeftRow, SchemeColor.None, 0.2f)) {
                    var icon = entry.icon;
                    if (icon != Icon.None) {
                        gui.BuildIcon(entry.icon);
                    }

                    gui.BuildText(entry.name);
                }

                var buttonEvent = gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.BackgroundAlt);
                if (buttonEvent == ButtonEvent.MouseOver) {
                    MainScreen.Instance.ShowTooltip(gui, entry.page.page, false, gui.lastRect);
                }
                else if (buttonEvent == ButtonEvent.Click) {
                    gui.ShowDropDown(dropdownGui => {
                        if (dropdownGui.BuildButton("Go to page") && dropdownGui.CloseDropdown()) {
                            MainScreen.Instance.SetActivePage(entry.page.page);
                        }

                        if (dropdownGui.BuildRedButton("Remove") && dropdownGui.CloseDropdown()) {
                            _ = entry.owner.RecordUndo().elements.Remove(entry);
                        }
                    });
                }
            }

            using (gui.EnterFixedPositioning(3f, 2f, default)) {
                gui.allocator = RectAllocator.LeftRow;
                gui.BuildText("x");
                DisplayAmount amount = entry.multiplier;
                if (gui.BuildFloatInput(amount, TextBoxDisplayStyle.FactorioObjectInput with { ColorGroup = SchemeColorGroup.Grey, Alignment = RectAlignment.MiddleLeft })
                    && amount.Value >= 0) {

                    entry.SetMultiplier(amount.Value);
                }
            }
        }

        private bool PagesDropdownFilter(ProjectPage data, SearchQuery searchTokens) => searchTokens.Match(data.name);

        private static VirtualScrollList<ProjectPage>.Drawer PagesDropdownDrawer(ProductionSummaryGroup group) => (gui, element, _) => {
            using (gui.EnterGroup(new Padding(1f, 0.25f), RectAllocator.LeftRow)) {
                if (element.icon != null) {
                    gui.BuildIcon(element.icon.icon);
                }

                gui.RemainingRow().BuildText(element.name, TextBlockDisplayStyle.Default(element.visible ? SchemeColor.BackgroundText : SchemeColor.BackgroundTextFaint));
            }

            if (gui.BuildButton(gui.lastRect, SchemeColor.BackgroundAlt, SchemeColor.Background)) {
                group.RecordUndo().elements.Add(new ProductionSummaryEntry(group) { page = new PageReference(element) });
            }
        };
    }

    protected override void ModelContentsChanged(bool visualOnly) {
        base.ModelContentsChanged(visualOnly);
        RebuildColumns();
    }

    private class GoodsColumn(ProductionSummaryColumn column, ProductionSummaryView view) : DataColumn<ProductionSummaryEntry>(4f) {
        public readonly ProductionSummaryColumn column = column;

        public Goods goods => column.goods;

        public override void BuildHeader(ImGui gui) {
            var moveHandle = gui.statePosition;
            moveHandle.Height = 5f;

            if (gui.BuildFactorioObjectWithAmount(goods, new(view.model.GetTotalFlow(goods), goods.flowUnitOfMeasure),
                ButtonDisplayStyle.ProductionTableScaled(view.filteredGoods == goods ? SchemeColor.Primary : SchemeColor.None)) == Click.Left) {

                view.ApplyFilter(goods);
            }

            if (!gui.InitiateDrag(moveHandle, moveHandle, column) && gui.ConsumeDrag(moveHandle.Center, column)
                && gui.GetDraggingObject<ProductionSummaryColumn>() is ProductionSummaryColumn draggingColumn) {

                view.model.RecordUndo(true).columns.MoveListElement(draggingColumn, column);
                view.RebuildColumns();
            }
        }

        public override void BuildElement(ImGui gui, ProductionSummaryEntry data) {
            float amount = data.GetAmount(goods);
            if (amount != 0) {
                if (gui.BuildFactorioObjectWithAmount(goods, new(data.GetAmount(goods), goods.flowUnitOfMeasure), ButtonDisplayStyle.ProductionTableUnscaled) == Click.Left) {
                    view.ApplyFilter(goods);
                }
            }
        }
    }

    private class RestGoodsColumn(ProductionSummaryView view) : TextDataColumn<ProductionSummaryEntry>("Other", 30f, 5f, 40f) {
        public override void BuildElement(ImGui gui, ProductionSummaryEntry data) {
            using var grid = gui.EnterInlineGrid(2.1f);
            foreach (var (goods, amount) in data.flow) {
                if (amount == 0f) {
                    continue;
                }

                if (!view.model.columnsExist.Contains(goods)) {
                    grid.Next();
                    var evt = gui.BuildButton(goods.icon, amount > 0f ? SchemeColor.Green : SchemeColor.None, size: 1.5f);
                    if (evt == ButtonEvent.Click) {
                        view.AddOrRemoveColumn(goods);
                    }
                    else if (evt == ButtonEvent.MouseOver) {
                        ImmediateWidgets.ShowPrecisionValueTooltip(gui, new(amount, goods.flowUnitOfMeasure), goods);
                    }
                }
            }
        }
    }

    private void ApplyFilter(Goods goods) {
        var filter = filteredGoods == goods ? null : goods;
        filteredGoods = filter;
        model.group.UpdateFilter(goods, default);
        Rebuild();
    }

    private bool IsColumnsSynced() {
        if (grid.columns.Count != model.columns.Count + 3) {
            return false;
        }

        int index = 2;
        foreach (var column in model.columns) {
            if (grid.columns[index++] is not GoodsColumn goodsColumn || goodsColumn.goods != column.goods) {
                return false;
            }
        }

        return true;
    }

    private void RebuildColumns() {
        if (!IsColumnsSynced()) {
            SyncGridHeaderWithColumns();
        }
    }

    private void SyncGridHeaderWithColumns() {
        var columns = grid.columns;
        var modelColumns = model.columns;
        columns.Clear();
        columns.Add(padding);
        columns.Add(firstColumn);
        foreach (var column in modelColumns) {
            if (!goodsToColumn.TryGetValue(column, out var currentColumn)) {
                currentColumn = new GoodsColumn(column, this);
                goodsToColumn[column] = currentColumn;
            }
            columns.Add(currentColumn);
        }
        columns.Add(lastColumn);
    }

    protected override void BuildHeader(ImGui gui) {
        if (model == null) {
            return;
        }

        grid.BuildHeader(gui);
        base.BuildHeader(gui);
    }

    private void AddOrRemoveColumn(Goods goods) {
        _ = model.RecordUndo();
        bool found = false;
        for (int i = 0; i < model.columns.Count; i++) {
            var column = model.columns[i];
            if (column.goods == goods) {
                model.columns.RemoveAt(i);
                found = true;
                break;
            }
        }

        if (!found) {
            model.columns.Add(new ProductionSummaryColumn(model, goods));
        }
    }

    protected override void BuildContent(ImGui gui) {
        if (model == null) {
            return;
        }

        flatHierarchy.Build(gui);
        gui.SetMinWidth(grid.width);

        gui.AllocateSpacing(1f);
        using (gui.EnterGroup(new Padding(1))) {
            if (model.group.elements.Count == 0) {
                gui.BuildText("Add your existing sheets here to keep track of what you have in your base and to see what shortages you may have");
            }
            else {
                gui.BuildText("List of goods produced/consumed by added blocks. Click on any of these to add it to (or remove it from) the table.");
            }

            using var inlineGrid = gui.EnterInlineGrid(3f, 1f);
            foreach (var (goods, amount) in model.sortedFlow) {
                inlineGrid.Next();
                if (gui.BuildFactorioObjectWithAmount(goods, new(amount, goods.flowUnitOfMeasure),
                    ButtonDisplayStyle.ProductionTableScaled(model.columnsExist.Contains(goods) ? SchemeColor.Primary : SchemeColor.None)) == Click.Left) {

                    AddOrRemoveColumn(goods);
                }
            }
        }
        if (gui.isBuilding) {
            gui.DrawRectangle(gui.lastRect, SchemeColor.Background, RectangleBorder.Thin);
        }
    }

    public override void Rebuild(bool visualOnly = false) {
        flatHierarchy.SetData(model.group);
        base.Rebuild(visualOnly);
    }

    protected override void BuildPageTooltip(ImGui gui, ProductionSummary contents) {

    }
}
