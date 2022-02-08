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
        private readonly Dictionary<Goods, GoodsColumn> goodsToColumn = new Dictionary<Goods, GoodsColumn>();

        public ProductionSummaryView()
        {
            grid = new DataGrid<ProductionSummaryEntry>(new SummaryColumn(this));
            pagesDropdown = new SearchableList<ProjectPage>(10f, new Vector2(15f, 2f), PagesDropdownDrawer, PagesDropdownFilter);
        }

        private class SummaryColumn : TextDataColumn<ProductionSummaryEntry>
        {
            private readonly ProductionSummaryView view;
            public SummaryColumn(ProductionSummaryView view) : base("Production block", 20f, 10f, 30f)
            {
                this.view = view;
            }

            public override void BuildElement(ImGui gui, ProductionSummaryEntry entry)
            {
                var icon = entry.icon;
                if (icon != Icon.None)
                    gui.BuildIcon(entry.icon);
                gui.BuildText(entry.name);
            }
        }

        protected override void ModelContentsChanged(bool visualOnly)
        {
            base.ModelContentsChanged(visualOnly);
            if (!IsColumnsSynced())
                SyncGridHeaderWithColumns();
        }

        private class GoodsColumn : DataColumn<ProductionSummaryEntry>
        {
            public readonly Goods goods;
            private readonly ProductionSummaryView view;

            public GoodsColumn(Goods goods, ProductionSummaryView view) : base(4f)
            {
                this.goods = goods;
                this.view = view;
            }

            public override void BuildHeader(ImGui gui)
            {
                gui.BuildFactorioObjectIcon(goods);
            }

            public override void BuildElement(ImGui gui, ProductionSummaryEntry data)
            {
                gui.BuildFactorioObjectWithAmount(goods, data.GetAmount(goods), goods.flowUnitOfMeasure);
            }
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

        private void SyncGridHeaderWithColumns()
        {
            var columns = grid.columns;
            var modelColumns = model.columns;
            columns.RemoveRange(1, grid.columns.Count - 1);
            foreach (var column in modelColumns)
            {
                if (!goodsToColumn.TryGetValue(column.goods, out var currentColumn))
                {
                    currentColumn = new GoodsColumn(column.goods, this);
                    goodsToColumn[column.goods] = currentColumn;
                }
                columns.Add(currentColumn);
            }
        }

        protected override void BuildContent(ImGui gui)
        {
            if (model == null)
                return;
            BuildSummary(gui);
            gui.AllocateSpacing();
            grid.BuildHeader(gui);
            var hasReorder = grid.BuildContent(gui, model.list, out var reorder, out var rect);
            gui.SetMinWidth(grid.width);
            
            if (hasReorder)
                model.RecordUndo(true).list.MoveListElement(reorder.from, reorder.to);

            gui.BuildText("List of other things produced/consumed by added blocks. Click on any of these to add it to the table.");
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
            
            using (gui.EnterRow())
            {
                gui.BuildText("Add");
                if (gui.BuildButton("Recipe"))
                {
                    
                }
                gui.BuildText("or");
                if (gui.BuildButton("Production table"))
                {
                    pagesDropdown.data = Project.current.pages.Where(x => x.content is ProductionTable).ToArray();
                    pagesDropdown.filter = searchQuery = new SearchQuery();
                    gui.ShowDropDown(AddProductionTableDropdown);
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
                ProjectPageSettingsPanel.Show(null, (name, icon) => MainScreen.Instance.AddProjectPage(name, icon, typeof(ProductionSummary), true));
        }

        protected override void BuildPageTooltip(ImGui gui, ProductionSummary contents)
        {
            
        }
    }
}