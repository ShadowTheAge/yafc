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
                    float amountAvailable = YAFCRounding((entry.Value.totalProvided > 0 ? entry.Value.totalProvided : 0) + entry.Value.extraProduced);
                    float amountNeeded = YAFCRounding((entry.Value.totalProvided < 0 ? -entry.Value.totalProvided : 0) + entry.Value.totalNeeded);
                    if (Math.Abs(amountAvailable - amountNeeded) < Epsilon || amountNeeded == 0)
                    {
                        continue;
                    }

                    grid.Next();
                    ProductionLink link = table.links.Find(x => x.goods.name == entry.Key);
                    if (link != null)
                    {
                        if (link.amount != 0f)
                        {
                            DrawProvideProduct(gui, link, page, entry.Value.extraProduced, amountAvailable >= amountNeeded);
                        }
                    }
                    else
                    {
                        if (Array.Exists(table.flow, x => x.goods.name == entry.Key))
                        {
                            ProductionTableFlow flow = Array.Find(table.flow, x => x.goods.name == entry.Key);
                            if (Math.Abs(flow.amount) > Epsilon)
                            {
                                DrawRequestProduct(gui, flow, amountAvailable >= amountNeeded);
                            }
                        }
                    }
                }
            }

            private void DrawProvideProduct(ImGui gui, ProductionLink element, ProjectPage page, float extraProduced, bool enoughOutput)
            {
                gui.allocator = RectAllocator.Stretch;
                gui.spacing = 0f;

                GoodsWithAmountEvent evt = gui.BuildFactorioObjectWithEditableAmount(element.goods, element.amount, element.goods.flowUnitOfMeasure, out float newAmount, (element.amount > 0 && enoughOutput) || (element.amount < 0 && extraProduced == -element.amount) ? SchemeColor.Primary : SchemeColor.Error);
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
                gui.BuildFactorioObjectWithAmount(flow.goods, -flow.amount, flow.goods?.flowUnitOfMeasure ?? UnitOfMeasure.None, flow.amount > Epsilon ? enoughProduced ? SchemeColor.Green : SchemeColor.Error : SchemeColor.None);
            }

            private void RebuildInvoked(bool visualOnly = false)
            {
                view.Rebuild(visualOnly);
                invokedPage.contentChanged -= RebuildInvoked;
            }
        }

        static readonly float Epsilon = 1e-5f;

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
                        value.totalProvided += YAFCRounding(link.amount); ;
                        allGoods[link.goods.name] = value;
                    }
                }

                foreach (ProductionTableFlow flow in content.flow)
                {
                    if (flow.amount < -Epsilon)
                    {
                        GoodDetails value = allGoods.GetValueOrDefault(flow.goods.name);
                        value.totalNeeded -= YAFCRounding(flow.amount); ;
                        allGoods[flow.goods.name] = value;
                    }
                    else if (flow.amount > Epsilon)
                    {
                        if (!content.links.Exists(x => x.goods == flow.goods))
                        {
                            // Only count extras if not linked
                            GoodDetails value = allGoods.GetValueOrDefault(flow.goods.name);
                            value.extraProduced += YAFCRounding(flow.amount);
                            allGoods[flow.goods.name] = value;
                        }
                    }
                }
            }

            foreach (Guid displayPage in screen.project.displayPages)
            {
                ProjectPage page = screen.project.FindPage(displayPage);
                mainGrid.BuildRow(gui, page);
            }
        }

        // Convert/truncate value as shown in UI to prevent slight mismatches
        static private float YAFCRounding(float value)
        {
#pragma warning disable CA1806 // We don't care about the returned value as result is updated independently whether the function return true or not
            DataUtils.TryParseAmount(DataUtils.FormatAmount(value, UnitOfMeasure.Second), out float result, UnitOfMeasure.Second);
#pragma warning restore CA1806

            return result;
        }

        public override void CreateModelDropdown(ImGui gui, Type type, Project project)
        {
        }
    }
}