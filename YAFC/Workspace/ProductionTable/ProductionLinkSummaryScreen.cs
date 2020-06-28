using System.Collections.Generic;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class ProductionLinkSummaryScreen : PseudoScreen, IComparer<(RecipeRow row, float flow)>
    {
        private static readonly ProductionLinkSummaryScreen Instance = new ProductionLinkSummaryScreen();
        private ProductionLink link;
        private readonly List<(RecipeRow row, float flow)> input = new List<(RecipeRow, float)>();
        private readonly List<(RecipeRow row, float flow)> output = new List<(RecipeRow, float)>();
        private float totalInput, totalOutput;
        private readonly VerticalScrollCustom scrollArea;

        private ProductionLinkSummaryScreen() : base(70f)
        {
            scrollArea = new VerticalScrollCustom(30, BuildScrollArea);
        }

        private void BuildScrollArea(ImGui gui)
        {
            using (var split = gui.EnterHorizontalSplit(2, 2f))
            {
                split.Next();
                gui.BuildText("Total input: "+DataUtils.FormatAmount(totalInput, link.goods.flowUnitOfMeasure), Font.subheader);
                BuildFlow(gui, input, totalInput);
                split.Next();
                gui.BuildText("Total output: "+DataUtils.FormatAmount(totalOutput, link.goods.flowUnitOfMeasure), Font.subheader);
                BuildFlow(gui, output, totalOutput);
            }
        }

        public override void Build(ImGui gui)
        {
            BuildHeader(gui, "Link summary");
            scrollArea.Build(gui);
        }

        private void BuildFlow(ImGui gui, List<(RecipeRow row, float flow)> list, float total)
        {
            foreach (var (row, flow) in list)
            {
                gui.BuildFactorioObjectButtonWithText(row.recipe, DataUtils.FormatAmount(flow, link.goods.flowUnitOfMeasure));
                if (gui.isBuilding)
                {
                    var lastRect = gui.lastRect;
                    lastRect.Width *= (flow / total);
                    gui.DrawRectangle(lastRect, SchemeColor.Primary);
                }
            }

        }

        private void CalculateFlow(ProductionLink link)
        {
            this.link = link;
            input.Clear();
            output.Clear();
            totalInput = 0;
            totalOutput = 0;
            foreach (var recipe in link.capturedRecipes)
            {
                var production = recipe.recipe.GetProduction(link.goods) * recipe.parameters.productionMultiplier;
                var consumption = recipe.recipe.GetConsumption(link.goods);
                var fuelUsage = recipe.fuel == link.goods ? recipe.parameters.fuelUsagePerSecondPerRecipe : 0;
                var localFlow = (float)((production - consumption - fuelUsage) * recipe.recipesPerSecond);
                if (localFlow > 0)
                {
                    input.Add((recipe, localFlow));
                    totalInput += localFlow;
                } 
                else if (localFlow < 0)
                {
                    output.Add((recipe, -localFlow));
                    totalOutput -= localFlow;
                }
            }
            input.Sort(this);
            output.Sort(this);
            Rebuild();
            scrollArea.RebuildContents();
        }

        public static void Show(ProductionLink link)
        {
            Instance.CalculateFlow(link);
            MainScreen.Instance.ShowPseudoScreen(Instance);
        }

        public int Compare((RecipeRow row, float flow) x, (RecipeRow row, float flow) y) => y.flow.CompareTo(x.flow);
    }
}