using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class ProductionSummaryView : ProjectPageView<ProductionSummary>
    {
        private readonly DataGrid<ProductionSummaryEntry> grid;
        private readonly SearchableList<ProjectPage> pagesDropdown;
        private SearchQuery searchQuery;
        private Goods filteredGoods;
        private Func<ProductionSummaryEntry, bool> filteredGoodsFilter;
        private readonly Dictionary<ProductionSummaryColumn, GoodsColumn> goodsToColumn = new Dictionary<ProductionSummaryColumn, GoodsColumn>();

        public ProductionSummaryView()
        {
            grid = new DataGrid<ProductionSummaryEntry>(new SummaryColumn(this)) {headerHeight = 4f};
            pagesDropdown = new SearchableList<ProjectPage>(30f, new Vector2(20f, 2f), PagesDropdownDrawer, PagesDropdownFilter);
        }

        private class SummaryColumn : DataColumn<ProductionSummaryEntry>
        {
            private readonly ProductionSummaryView view;
            public SummaryColumn(ProductionSummaryView view) : base(20f, 10f, 30f)
            {
                this.view = view;
            }

            public override void BuildHeader(ImGui gui)
            {
                gui.allocator = RectAllocator.LeftAlign;
                if (gui.BuildButton(Icon.Plus, SchemeColor.Primary, SchemeColor.PrimalyAlt, SchemeColor.PrimalyAlt, 2))
                {
                    view.pagesDropdown.data = Project.current.pages.Where(x => x.content is ProductionTable).ToArray();
                    view.pagesDropdown.filter = view.searchQuery = new SearchQuery();
                    gui.ShowDropDown(view.AddProductionTableDropdown);
                }
            }

            public override void BuildElement(ImGui gui, ProductionSummaryEntry entry)
            {
                gui.spacing = 0.2f;
                var icon = entry.icon;
                if (icon != Icon.None)
                    gui.BuildIcon(entry.icon);
                gui.BuildText(entry.name);

                if (gui.action == ImGuiAction.MouseMove)
                {
                    var fullRect = gui.statePosition;
                    fullRect.Height = 4.5f;
                    if (gui.ConsumeMouseOver(fullRect))
                        MainScreen.Instance.ShowTooltip(gui, entry.page.page, false, fullRect);
                }
            }
        }

        protected override void ModelContentsChanged(bool visualOnly)
        {
            base.ModelContentsChanged(visualOnly);
            RebuildColumns();
        }

        private class GoodsColumn : DataColumn<ProductionSummaryEntry>
        {
            public readonly ProductionSummaryColumn column;
            private readonly ProductionSummaryView view;
            public Goods goods => column.goods;

            public GoodsColumn(ProductionSummaryColumn column, ProductionSummaryView view) : base(4f)
            {
                this.column = column;
                this.view = view;
            }

            public override void BuildHeader(ImGui gui)
            {
                var moveHandle = gui.statePosition;
                moveHandle.Height = 5f;
                
                if (gui.BuildFactorioObjectWithAmount(goods, view.model.GetTotalFlow(goods), goods.flowUnitOfMeasure, view.filteredGoods == goods ? SchemeColor.Primary : SchemeColor.None))
                {
                    if (view.filteredGoods == goods)
                        view.ApplyFilter(null);
                    else view.ApplyFilter(goods);
                }

                if (!gui.InitiateDrag(moveHandle, moveHandle, column) && gui.ConsumeDrag(moveHandle.Center, column))
                {
                    view.model.RecordUndo(true).columns.MoveListElement(gui.GetDraggingObject<ProductionSummaryColumn>(), column);
                    view.RebuildColumns();
                }
            }

            public override void BuildElement(ImGui gui, ProductionSummaryEntry data)
            {
                var amount = data.GetAmount(goods);
                if (amount != 0)
                    gui.BuildFactorioObjectWithAmount(goods, data.GetAmount(goods), goods.flowUnitOfMeasure);
            }
        }

        private void ApplyFilter(Goods goods)
        {
            filteredGoods = goods;
            filteredGoodsFilter = goods == null ? null : (Func<ProductionSummaryEntry, bool>) (entry => entry.flow.TryGetValue(goods, out var amount) && amount != 0); 
            Rebuild();
        }

        private bool PagesDropdownFilter(ProjectPage data, SearchQuery searchtokens) => searchtokens.Match(data.name);

        private void PagesDropdownDrawer(ImGui gui, ProjectPage element, int index)
        {
            using (gui.EnterGroup(new Padding(1f, 0.25f), RectAllocator.LeftRow))
            {
                if (element.icon != null)
                    gui.BuildIcon(element.icon.icon);
                gui.RemainingRow().BuildText(element.name, color:element.visible ? SchemeColor.BackgroundText : SchemeColor.BackgroundTextFaint);
            }

            if (gui.BuildButton(gui.lastRect, SchemeColor.BackgroundAlt, SchemeColor.Background))
            {
                model.RecordUndo().list.Add(new ProductionSummaryEntry(model, new PageReference(element)));
            }
        }

        private bool IsColumnsSynced()
        {
            if (grid.columns.Count != model.columns.Count + 1)
                return false;
            var index = 1;
            foreach (var column in model.columns)
            {
                if (!(grid.columns[index++] is GoodsColumn goodsColumn) || goodsColumn.goods != column.goods)
                    return false;
            }

            return true;
        }

        private void RebuildColumns()
        {
            if (!IsColumnsSynced())
                SyncGridHeaderWithColumns();
        }

        private void SyncGridHeaderWithColumns()
        {
            var columns = grid.columns;
            var modelColumns = model.columns;
            columns.RemoveRange(1, grid.columns.Count - 1);
            foreach (var column in modelColumns)
            {
                if (!goodsToColumn.TryGetValue(column, out var currentColumn))
                {
                    currentColumn = new GoodsColumn(column, this);
                    goodsToColumn[column] = currentColumn;
                }
                columns.Add(currentColumn);
            }
        }

        protected override void BuildHeader(ImGui gui)
        {
            if (model == null)
                return;
            grid.BuildHeader(gui);
            base.BuildHeader(gui);
        }

        protected override void BuildContent(ImGui gui)
        {
            if (model == null)
                return;
            var hasReorder = grid.BuildContent(gui, model.list, out var reorder, out var rect, filteredGoodsFilter);
            gui.SetMinWidth(grid.width);
            
            if (hasReorder)
                model.RecordUndo(true).list.MoveListElement(reorder.from, reorder.to);

            if (model.list.Count == 0)
                gui.BuildText("Add your existing sheets here to keep track of what you have in your base and to see what shortages you may have");
            else gui.BuildText("List of other things produced/consumed by added blocks. Click on any of these to add it to the table.");
            using (var igrid = gui.EnterInlineGrid(3f, 1f))
            {
                foreach (var element in model.nonCapturedFlow)
                {
                    igrid.Next();
                    if (gui.BuildFactorioObjectWithAmount(element.goods, element.amount, element.goods.flowUnitOfMeasure))
                    {
                        model.RecordUndo().columns.Add(new ProductionSummaryColumn(model, element.goods));
                    }
                }
            }
        }

        private void AddProductionTableDropdown(ImGui gui)
        {
            using (gui.EnterGroup(new Padding(1f)))
            {
                if (gui.BuildSearchBox(searchQuery, out searchQuery))
                    pagesDropdown.filter = searchQuery;
            }
            pagesDropdown.Build(gui);
        }

        private void BuildSummary(ImGui gui)
        {
            
        }

        public override void CreateModelDropdown(ImGui gui, Type type, Project project)
        {
            if (gui.BuildContextMenuButton("Create production summary (Preview)") && gui.CloseDropdown())
                ProjectPageSettingsPanel.Show(null, (name, icon) => MainScreen.Instance.AddProjectPage(name, icon, typeof(ProductionSummary), true, true));
        }

        protected override void BuildPageTooltip(ImGui gui, ProductionSummary contents)
        {
            
        }
    }
}