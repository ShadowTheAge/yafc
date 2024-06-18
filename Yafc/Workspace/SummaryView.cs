using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Yafc.Model;
using Yafc.UI;
using YAFC.Model;

namespace Yafc {
    public class SummaryView : ProjectPageView<Summary> {
        private class SummaryScrollArea(GuiBuilder builder) : ScrollArea(DefaultHeight, builder, MainScreen.Instance.InputSystem, horizontal: true) {
            private static readonly float DefaultHeight = 10;

            public new void Build(ImGui gui) =>
                // Maximize scroll area to fit parent area (minus header and 'show issues' heights, and some (3) padding probably)
                Build(gui, gui.valid && gui.parent is not null ? gui.parent.contentSize.Y - Font.header.size - Font.text.size - 3 : DefaultHeight);
        }

        private class SummaryTabColumn : TextDataColumn<ProjectPage> {
            private const float FirstColumnWidth = 14f; // About 20 'o' wide

            public SummaryTabColumn() : base("Tab", FirstColumnWidth) {
            }

            public override void BuildElement(ImGui gui, ProjectPage page) {
                if (page?.contentType != typeof(ProductionTable)) {
                    return;
                }

                using (gui.EnterGroup(new Padding(0.5f, 0.2f, 0.2f, 0.5f))) {
                    gui.spacing = 0.2f;
                    if (page.icon != null) {
                        gui.BuildIcon(page.icon.icon);
                    }
                    else {
                        _ = gui.AllocateRect(0f, 1.5f);
                    }

                    gui.BuildText(page.name);
                }
            }
        }

        private class SummaryDataColumn : TextDataColumn<ProjectPage> {
            protected readonly SummaryView view;

            public SummaryDataColumn(SummaryView view) : base("Linked", float.MaxValue) => this.view = view;

            public override void BuildElement(ImGui gui, ProjectPage page) {
                if (page?.contentType != typeof(ProductionTable)) {
                    return;
                }

                ProductionTable table = (ProductionTable)page.content;
                using var grid = gui.EnterInlineGrid(ElementWidth, ElementSpacing);
                foreach (KeyValuePair<string, GoodDetails> goodInfo in view.allGoods) {
                    if (!view.searchQuery.Match(goodInfo.Key)) {
                        continue;
                    }

                    float amountAvailable = YafcRounding((goodInfo.Value.totalProvided > 0 ? goodInfo.Value.totalProvided : 0) + goodInfo.Value.extraProduced);
                    float amountNeeded = YafcRounding((goodInfo.Value.totalProvided < 0 ? -goodInfo.Value.totalProvided : 0) + goodInfo.Value.totalNeeded);
                    if (view.model.showOnlyIssues && (Math.Abs(amountAvailable - amountNeeded) < Epsilon || amountNeeded == 0)) {
                        continue;
                    }

                    grid.Next();
                    bool enoughProduced = amountAvailable >= amountNeeded;
                    ProductionLink? link = table.links.Find(x => x.goods.name == goodInfo.Key);
                    if (link != null) {
                        if (link.amount != 0f) {
                            DrawProvideProduct(gui, link, page, goodInfo.Value, enoughProduced);
                        }
                    }
                    else {
                        if (Array.Exists(table.flow, x => x.goods.name == goodInfo.Key)) {
                            ProductionTableFlow flow = Array.Find(table.flow, x => x.goods.name == goodInfo.Key);
                            if (Math.Abs(flow.amount) > Epsilon) {

                                DrawRequestProduct(gui, flow, enoughProduced);
                            }
                        }
                    }
                }
            }

            private static void DrawProvideProduct(ImGui gui, ProductionLink element, ProjectPage page, GoodDetails goodInfo, bool enoughProduced) {
                SchemeColor iconColor;
                if (element.amount > 0 && enoughProduced) {
                    // Production matches consumption
                    iconColor = SchemeColor.Primary;
                }
                else if (element.amount < 0 && goodInfo.extraProduced == -element.amount) {
                    // Extra produced matches input goal
                    iconColor = SchemeColor.Primary;
                }
                else {
                    // Production and consumption does not match
                    iconColor = SchemeColor.Error;
                }

                gui.allocator = RectAllocator.Stretch;
                gui.spacing = 0f;
                GoodsWithAmountEvent evt = gui.BuildFactorioObjectWithEditableAmount(element.goods, element.amount, element.goods.flowUnitOfMeasure, out float newAmount, iconColor);
                if (evt == GoodsWithAmountEvent.TextEditing && newAmount != 0) {
                    SetProviderAmount(element, page, newAmount);
                }
                else if (evt == GoodsWithAmountEvent.LeftButtonClick) {
                    SetProviderAmount(element, page, YafcRounding(goodInfo.sum));
                }
            }
            private static void DrawRequestProduct(ImGui gui, ProductionTableFlow flow, bool enoughExtraProduced) {
                SchemeColor iconColor;
                if (flow.amount > Epsilon) {
                    if (!enoughExtraProduced) {
                        // Not enough in extra production, while being depended on
                        iconColor = SchemeColor.Error;
                    }
                    else {
                        // Enough extra product produced
                        iconColor = SchemeColor.Green;
                    }
                }
                else {
                    // Flow amount negative or (almost, due to rounding errors) zero, just a regular request (nothing to indicate)
                    iconColor = SchemeColor.None;
                }

                gui.allocator = RectAllocator.Stretch;
                gui.spacing = 0f;
                _ = gui.BuildFactorioObjectWithAmount(flow.goods, -flow.amount, flow.goods?.flowUnitOfMeasure ?? UnitOfMeasure.None, iconColor);
            }

            private static void SetProviderAmount(ProductionLink element, ProjectPage page, float newAmount) {
                element.RecordUndo().amount = newAmount;
                // Hack: Force recalculate the page (and make sure to catch the content change event caused by the recalculation)
                page.SetActive(true);
                page.SetToRecalculate();
                page.SetActive(false);
            }
        }

        private static readonly float Epsilon = 1e-5f;
        private static readonly float ElementWidth = 3;
        private static readonly float ElementSpacing = 1;

        private struct GoodDetails {
            public float totalProvided;
            public float totalNeeded;
            public float extraProduced;
            public float sum;
        }

        private Project project;
        private SearchQuery searchQuery;

        private readonly SummaryScrollArea scrollArea;
        private readonly SummaryDataColumn goodsColumn;
        private readonly DataGrid<ProjectPage> mainGrid;

        private readonly Dictionary<string, GoodDetails> allGoods = [];


        public SummaryView(Project project) {
            goodsColumn = new SummaryDataColumn(this);
            TextDataColumn<ProjectPage>[] columns = new TextDataColumn<ProjectPage>[]
            {
                new SummaryTabColumn(),
                goodsColumn,
            };
            scrollArea = new SummaryScrollArea(BuildScrollArea);
            mainGrid = new DataGrid<ProjectPage>(columns);

            SetProject(project);
        }

        [MemberNotNull(nameof(project))]
        public void SetProject(Project project) {
            if (this.project != null) {
                this.project.metaInfoChanged -= Recalculate;
                foreach (ProjectPage page in project.pages) {
                    page.contentChanged -= Recalculate;
                }
            }

            this.project = project;

            project.metaInfoChanged += Recalculate;
            foreach (ProjectPage page in project.pages) {
                page.contentChanged += Recalculate;
            }
        }

        protected override void BuildPageTooltip(ImGui gui, Summary contents) {
        }

        protected override void BuildHeader(ImGui gui) {
            base.BuildHeader(gui);

            gui.allocator = RectAllocator.Center;
            gui.BuildText("Production Sheet Summary", Font.header, false, RectAlignment.Middle);
            gui.allocator = RectAllocator.LeftAlign;
        }

        protected override void BuildContent(ImGui gui) {
            if (gui.BuildCheckBox("Only show issues", model.showOnlyIssues, out bool newValue)) {
                model.showOnlyIssues = newValue;
                Recalculate();
            }

            scrollArea.Build(gui);
        }

        private void BuildScrollArea(ImGui gui) {
            foreach (Guid displayPage in project.displayPages) {
                ProjectPage? page = project.FindPage(displayPage);
                if (page?.contentType != typeof(ProductionTable)) {
                    continue;
                }

                _ = mainGrid.BuildRow(gui, page);
            }
        }

        private void Recalculate() => Recalculate(false);

        private void Recalculate(bool visualOnly) {
            allGoods.Clear();
            foreach (Guid displayPage in project.displayPages) {
                ProjectPage? page = project.FindPage(displayPage);
                ProductionTable? content = page?.content as ProductionTable;
                if (content == null) {
                    continue;
                }

                foreach (ProductionLink link in content.links) {
                    if (link.amount != 0f) {
                        GoodDetails value = allGoods.GetValueOrDefault(link.goods.name);
                        value.totalProvided += YafcRounding(link.amount); ;
                        allGoods[link.goods.name] = value;
                    }
                }

                foreach (ProductionTableFlow flow in content.flow) {
                    if (flow.amount < -Epsilon) {
                        GoodDetails value = allGoods.GetValueOrDefault(flow.goods.name);
                        value.totalNeeded -= YafcRounding(flow.amount); ;
                        value.sum -= YafcRounding(flow.amount); ;
                        allGoods[flow.goods.name] = value;
                    }
                    else if (flow.amount > Epsilon) {
                        if (!content.links.Exists(x => x.goods == flow.goods)) {
                            // Only count extras if not linked
                            GoodDetails value = allGoods.GetValueOrDefault(flow.goods.name);
                            value.extraProduced += YafcRounding(flow.amount);
                            value.sum -= YafcRounding(flow.amount);
                            allGoods[flow.goods.name] = value;
                        }
                    }
                }
            }

            int count = 0;
            foreach (KeyValuePair<string, GoodDetails> entry in allGoods) {
                float amountAvailable = YafcRounding((entry.Value.totalProvided > 0 ? entry.Value.totalProvided : 0) + entry.Value.extraProduced);
                float amountNeeded = YafcRounding((entry.Value.totalProvided < 0 ? -entry.Value.totalProvided : 0) + entry.Value.totalNeeded);
                if (model != null && model.showOnlyIssues && (Math.Abs(amountAvailable - amountNeeded) < Epsilon || amountNeeded == 0)) {
                    continue;
                }
                count++;
            }

            goodsColumn.width = count * (ElementWidth + ElementSpacing);

            Rebuild(visualOnly);
            scrollArea.RebuildContents();
        }

        // Convert/truncate value as shown in UI to prevent slight mismatches
        private static float YafcRounding(float value) {
            _ = DataUtils.TryParseAmount(DataUtils.FormatAmount(value, UnitOfMeasure.Second), out float result, UnitOfMeasure.Second);
            return result;
        }

        public override void SetSearchQuery(SearchQuery query) {
            searchQuery = query;
            bodyContent.Rebuild();
            scrollArea.Rebuild();
        }

        public override void CreateModelDropdown(ImGui gui, Type type, Project project) {
        }
    }
}
