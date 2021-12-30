using System;
using System.Collections.Generic;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class SummaryView : ProjectPageView<Summary>
    {
        private class SummaryTabColumn : TextDataColumn<ProjectPage>
        {
            public SummaryTabColumn() : base("Tab", 6f)
            {
            }

            public override void BuildElement(ImGui gui, ProjectPage page)
            {
                if (page?.contentType != typeof(ProductionTable))
                {
                    return;
                }

                using (gui.EnterGroup(new Padding(0.5f, 0.2f, 0.2f, 0.5f)))
                {
                    gui.spacing = 0.2f;
                    if (page.icon != null)
                        gui.BuildIcon(page.icon.icon);
                    else gui.AllocateRect(0f, 1.5f);
                    gui.BuildText(page.name);
                }
            }
        }

        private class SummaryDataColumn : TextDataColumn<ProjectPage>
        {
            protected readonly SummaryView view;
            private ProjectPage invokedPage;

            public SummaryDataColumn(SummaryView view) : base("Linked", float.MaxValue)
            {
                this.view = view;
            }

            public override void BuildElement(ImGui gui, ProjectPage page)
            {
                if (page?.contentType != typeof(ProductionTable))
                {
                    return;
                }

                var table = page.content as ProductionTable;
                using var grid = gui.EnterInlineGrid(3f, 1f);
                foreach (KeyValuePair<string, GoodDetails> entry in view.allGoods)
                {
                    float amountAvailable = entry.Value.totalProvided > 0 ? entry.Value.totalProvided : entry.Value.extraProduced;
                    float amountNeeded = entry.Value.totalProvided < 0 ? -entry.Value.totalProvided : entry.Value.totalNeeded;
                    if (DataUtils.FormatAmount(amountAvailable, UnitOfMeasure.None) == DataUtils.FormatAmount(amountNeeded, UnitOfMeasure.None))
                    {
                        continue;
                    }

                    grid.Next();
                    ProductionLink link = table.links.Find(x => x.goods.name == entry.Key);
                    if (link != null)
                    {
                        if (link.amount != 0f)
                        {
                            DrawProvideProduct(gui, link, page, entry.Value.extraProduced, amountAvailable >= entry.Value.totalNeeded);
                        }
                    }
                    else
                    {
                        if (Array.Exists(table.flow, x => x.goods.name == entry.Key))
                        {
                            ProductionTableFlow flow = Array.Find(table.flow, x => x.goods.name == entry.Key);
                            if (Math.Abs(flow.amount) > 1e-5f)
                            {

                                DrawRequestProduct(gui, flow, entry.Value.extraProduced >= entry.Value.totalNeeded);
                            }
                        }
                    }
                }
            }

            private void DrawProvideProduct(ImGui gui, ProductionLink element, ProjectPage page, float extraProduced, bool enoughOutput)
            {
                gui.allocator = RectAllocator.Stretch;
                gui.spacing = 0f;

                GoodsWithAmountEvent evt = gui.BuildFactorioObjectWithEditableAmount(element.goods, element.amount, element.goods.flowUnitOfMeasure, out float newAmount, (element.amount > 0 && enoughOutput) || (element.amount < 0 && DataUtils.FormatAmount(extraProduced, UnitOfMeasure.None) == DataUtils.FormatAmount(-element.amount, UnitOfMeasure.None)) ? SchemeColor.Primary : SchemeColor.Error);
                if (evt == GoodsWithAmountEvent.TextEditing && newAmount != 0)
                {
                    element.RecordUndo().amount = newAmount;
                    // Hack: Force recalculate the page (and make sure to catch the content change event caused by the recalculation)
                    invokedPage = page;
                    page.contentChanged += RebuildInvoked;
                    page.SetActive(true);
                    page.SetToRecalculate();
                    page.SetActive(false);
                }
            }
            static private void DrawRequestProduct(ImGui gui, ProductionTableFlow flow, bool enoughProduced)
            {
                gui.allocator = RectAllocator.Stretch;
                gui.spacing = 0f;
                gui.BuildFactorioObjectWithAmount(flow.goods, -flow.amount, flow.goods?.flowUnitOfMeasure ?? UnitOfMeasure.None, flow.amount > 1e-5f ? enoughProduced ? SchemeColor.Green : SchemeColor.Error : SchemeColor.None);
            }

            private void RebuildInvoked(bool visualOnly = false)
            {
                view.Rebuild(visualOnly);
                invokedPage.contentChanged -= RebuildInvoked;
            }
        }

        struct GoodDetails
        {
            public float totalProvided;
            public float totalNeeded;
            public float extraProduced;
        }

        private readonly MainScreen screen;

        private readonly DataGrid<ProjectPage> mainGrid;

        private readonly Dictionary<string, GoodDetails> allGoods = new Dictionary<string, GoodDetails>();


        public SummaryView(MainScreen screen)
        {
            this.screen = screen;
            var columns = new TextDataColumn<ProjectPage>[]
            {
                new SummaryTabColumn(),
                new SummaryDataColumn(this),
            };
            mainGrid = new DataGrid<ProjectPage>(columns);
        }

        protected override void BuildPageTooltip(ImGui gui, Summary contents)
        {
        }

        protected override void BuildContent(ImGui gui)
        {
            // TODO Can we detect if things changed?
            allGoods.Clear();
            foreach (Guid displayPage in screen.project.displayPages)
            {
                ProjectPage page = screen.project.FindPage(displayPage);
                ProductionTable content = page?.content as ProductionTable;
                if (content == null)
                {
                    continue;
                }

                foreach (ProductionLink link in content.links)
                {
                    if (link.amount != 0f)
                    {
                        GoodDetails value = allGoods.GetValueOrDefault(link.goods.name);
                        value.totalProvided += link.amount;
                        allGoods[link.goods.name] = value;
                    }
                }

                foreach (ProductionTableFlow flow in content.flow)
                {
                    if (flow.amount < -1e-5f)
                    {
                        GoodDetails value = allGoods.GetValueOrDefault(flow.goods.name);
                        value.totalNeeded -= flow.amount;
                        allGoods[flow.goods.name] = value;
                    }
                    else if (flow.amount > 1e-5f)
                    {
                        GoodDetails value = allGoods.GetValueOrDefault(flow.goods.name);
                        value.extraProduced += flow.amount;
                        allGoods[flow.goods.name] = value;
                    }
                }
            }

            foreach (Guid displayPage in screen.project.displayPages)
            {
                ProjectPage page = screen.project.FindPage(displayPage);
                mainGrid.BuildRow(gui, page);
            }
        }

        public override void CreateModelDropdown(ImGui gui, Type type, Project project)
        {
        }
    }
}