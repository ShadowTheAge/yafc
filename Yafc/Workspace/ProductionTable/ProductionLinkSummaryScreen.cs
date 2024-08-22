using System;
using System.Collections.Generic;
using Yafc.Model;
using Yafc.UI;

namespace Yafc {
    public class ProductionLinkSummaryScreen : PseudoScreen, IComparer<(RecipeRow row, float flow)> {
        private readonly ProductionLink link;
        private readonly List<(RecipeRow row, float flow)> input = [];
        private readonly List<(RecipeRow row, float flow)> output = [];
        private float totalInput, totalOutput;
        private readonly ScrollArea scrollArea;

        private ProductionLinkSummaryScreen(ProductionLink link) {
            scrollArea = new ScrollArea(30, BuildScrollArea);
            this.link = link;
            CalculateFlow(link);
        }

        private void BuildScrollArea(ImGui gui) {
            gui.BuildText("Production: " + DataUtils.FormatAmount(totalInput, link.goods.flowUnitOfMeasure), Font.subheader);
            BuildFlow(gui, input, totalInput);
            gui.spacing = 0.5f;
            gui.BuildText("Consumption: " + DataUtils.FormatAmount(totalOutput, link.goods.flowUnitOfMeasure), Font.subheader);
            BuildFlow(gui, output, totalOutput);
            if (link.amount != 0) {
                gui.spacing = 0.5f;
                gui.BuildText((link.amount > 0 ? "Requested production: " : "Requested consumption: ") + DataUtils.FormatAmount(MathF.Abs(link.amount), link.goods.flowUnitOfMeasure), new TextBlockDisplayStyle(Font.subheader, Color: SchemeColor.GreenAlt));
            }
            if (link.flags.HasFlags(ProductionLink.Flags.LinkNotMatched) && totalInput != totalOutput + link.amount) {
                float amount = totalInput - totalOutput - link.amount;
                gui.spacing = 0.5f;
                gui.BuildText((amount > 0 ? "Overproduction: " : "Overconsumption: ") + DataUtils.FormatAmount(MathF.Abs(amount), link.goods.flowUnitOfMeasure), new TextBlockDisplayStyle(Font.subheader, Color: SchemeColor.Error));
            }
        }

        public override void Build(ImGui gui) {
            BuildHeader(gui, "Link summary");
            scrollArea.Build(gui);
            if (gui.BuildButton("Done")) {
                Close();
            }
        }

        protected override void ReturnPressed() => Close();

        private void BuildFlow(ImGui gui, List<(RecipeRow row, float flow)> list, float total) {
            gui.spacing = 0f;
            foreach (var (row, flow) in list) {
                _ = gui.BuildFactorioObjectButtonWithText(row.recipe, DataUtils.FormatAmount(flow, link.goods.flowUnitOfMeasure));
                if (gui.isBuilding) {
                    var lastRect = gui.lastRect;
                    lastRect.Width *= (flow / total);
                    gui.DrawRectangle(lastRect, SchemeColor.Primary);
                }
            }

        }

        private void CalculateFlow(ProductionLink link) {
            totalInput = 0;
            totalOutput = 0;
            foreach (var recipe in link.capturedRecipes) {
                float production = recipe.GetProductionForRow(link.goods);
                float consumption = recipe.GetConsumptionForRow(link.goods);
                float fuelUsage = recipe.fuel == link.goods ? recipe.FuelInformation.Amount : 0;
                float localFlow = production - consumption - fuelUsage;
                if (localFlow > 0) {
                    input.Add((recipe, localFlow));
                    totalInput += localFlow;
                }
                else if (localFlow < 0) {
                    output.Add((recipe, -localFlow));
                    totalOutput -= localFlow;
                }
            }
            input.Sort(this);
            output.Sort(this);
            Rebuild();
            scrollArea.RebuildContents();
        }

        public static void Show(ProductionLink link) {
            _ = MainScreen.Instance.ShowPseudoScreen(new ProductionLinkSummaryScreen(link));
        }

        public int Compare((RecipeRow row, float flow) x, (RecipeRow row, float flow) y) => y.flow.CompareTo(x.flow);
    }
}
