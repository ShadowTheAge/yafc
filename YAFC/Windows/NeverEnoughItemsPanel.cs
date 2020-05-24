using System.Collections.Generic;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public interface IRecipeItemFlowProvider
    {
        float GetRecipeFlow(Recipe recipe);
        float GetGoodsFlow(Goods goods);
    }

    public class NeverEnoughItemsPanel : PseudoScreen, IRecipeItemFlowProvider, IComparer<NeverEnoughItemsPanel.RecipeEntry>
    {
        private static NeverEnoughItemsPanel Instance = new NeverEnoughItemsPanel(); 
        private IRecipeItemFlowProvider provider;
        private Goods current;
        private Goods changing;
        private float currentFlow;
        private EntryStatus showRecipesRange;
        private readonly List<Goods> recent = new List<Goods>();

        private readonly VerticalScrollCustom productionList;
        private readonly VerticalScrollCustom usageList;
        
        private enum EntryStatus
        {
            NotAccessible,
            NotAccessibleWithCurrentMilestones,
            Wasteful,
            Normal,
            Useful
        }
        
        private readonly struct RecipeEntry
        {
            public readonly Recipe recipe;
            public readonly float recipeFlow;
            public readonly float flow;
            public readonly float specificEfficiency;
            public readonly EntryStatus entryStatus;

            public RecipeEntry(Recipe recipe, bool isProduction, IRecipeItemFlowProvider provider, Goods currentItem)
            {
                this.recipe = recipe;
                var amount = isProduction ? recipe.GetProduction(currentItem) : recipe.GetConsumption(currentItem);
                recipeFlow = provider.GetRecipeFlow(recipe); 
                flow = recipeFlow * amount;
                specificEfficiency = isProduction ? recipe.Cost() / amount : 0f;
                if (!recipe.IsAccessible())
                    entryStatus = EntryStatus.NotAccessible;
                else if (!recipe.IsAccessibleWithCurrentMilestones())
                    entryStatus = EntryStatus.NotAccessibleWithCurrentMilestones;
                else
                {
                    var waste = recipe.RecipeWaste();
                    if (waste > 0.95f)
                        entryStatus = EntryStatus.Wasteful;
                    else if (waste > 0f)
                        entryStatus = EntryStatus.Normal;
                    else entryStatus = EntryStatus.Useful;
                }
            }
        }

        private readonly List<RecipeEntry> productions = new List<RecipeEntry>();
        private readonly List<RecipeEntry> usages = new List<RecipeEntry>();
        
        public NeverEnoughItemsPanel() : base(76f)
        {
            productionList = new VerticalScrollCustom(40f, BuildItemProduction, new Padding(0.5f));
            usageList = new VerticalScrollCustom(40f, BuildItemUsages, new Padding(0.5f));
        }

        private void SetItem(Goods current)
        {
            recent.Remove(current);
            if (recent.Count > 18)
                recent.RemoveAt(0);
            if (this.current != null)
                recent.Add(this.current);
            this.current = current;
            currentFlow = provider.GetGoodsFlow(current);
            productions.Clear();
            foreach (var recipe in current.production)
                productions.Add(new RecipeEntry(recipe, true, provider, current));
            productions.Sort(this);
            usages.Clear();
            foreach (var usage in current.usages)
                usages.Add(new RecipeEntry(usage, false, provider, current));
            usages.Sort(this);
            showRecipesRange = EntryStatus.Normal;
            if (productions.Count > 0 && productions[0].entryStatus < showRecipesRange)
                showRecipesRange = productions[0].entryStatus;
            Rebuild();
            productionList.Rebuild();
            usageList.Rebuild();
        }

        private void DrawIngredients(ImGui gui, Recipe recipe)
        {
            if (recipe.ingredients.Length > 8)
            {
                DrawTooManyThings(gui, recipe.ingredients, 8);
                return;
            }
            foreach (var ingredient in recipe.ingredients)
                if (gui.BuildFactorioObjectWithAmount(ingredient.goods, ingredient.amount, UnitOfMeasure.None))
                    changing = ingredient.goods;
        }

        private void DrawProducts(ImGui gui, Recipe recipe)
        {
            if (recipe.products.Length > 7)
            {
                DrawTooManyThings(gui, recipe.products, 7);
                return;
            }
            for (var i = recipe.products.Length - 1; i >= 0; i--)
            {
                var product = recipe.products[i];
                if (gui.BuildFactorioObjectWithAmount(product.goods, product.amount, UnitOfMeasure.None))
                    changing = product.goods;
            }
        }

        private void DrawTooManyThings(ImGui gui, IEnumerable<IFactorioObjectWrapper> list, int maxElemCount)
        {
            using (var grid = gui.EnterInlineGrid(3f, maxElemCount))
            {
                foreach (var item in list)
                {
                    grid.Next();
                    if (gui.BuildFactorioObjectWithAmount(item.target, item.amount, UnitOfMeasure.None))
                        changing = item.target as Goods;
                }
            }
        }

        private void CheckChanging()
        {
            if (changing != null)
            {
                SetItem(changing);
                changing = null;
            }
        }

        private void DrawRecipeEntry(ImGui gui, RecipeEntry entry, bool production)
        {
            var textcolor = SchemeColor.BackgroundText;
            var bgColor = SchemeColor.Background;
            var isBuilding = gui.isBuilding;
            var recipe = entry.recipe;
            var waste = recipe.RecipeWaste();
            if (isBuilding)
            {
                if (entry.entryStatus == EntryStatus.NotAccessible)
                {
                    bgColor = SchemeColor.None;
                    textcolor = SchemeColor.BackgroundTextFaint;
                } else if (entry.flow > 0f)
                {
                    bgColor = SchemeColor.Secondary;
                    textcolor = SchemeColor.SecondaryText;
                }
                else if (waste > 0.95f)
                {
                    bgColor = SchemeColor.Error;
                    textcolor = SchemeColor.ErrorText;
                }
            }
            using (gui.EnterGroup(new Padding(0.5f), production ? RectAllocator.LeftRow : RectAllocator.RightRow, textcolor))
            {
                using (gui.EnterFixedPositioning(4f, 0f, default))
                {
                    gui.allocator = RectAllocator.Stretch;
                    gui.spacing = 0f;
                    gui.BuildFactorioObjectButton(entry.recipe, 4f, MilestoneDisplay.Contained);
                    gui.BuildText(DataUtils.FormatAmount(recipe.Cost(), UnitOfMeasure.None, "¥"), align:RectAlignment.Middle);
                }
                gui.AllocateSpacing();
                gui.allocator = production ? RectAllocator.LeftAlign : RectAllocator.RightAlign;
                gui.BuildText(recipe.locName, wrap:true);
                if (recipe.ingredients.Length + recipe.products.Length <= 8)
                {
                    using (gui.EnterRow())
                    {
                        DrawIngredients(gui, entry.recipe);
                        gui.allocator = RectAllocator.RightRow;
                        DrawProducts(gui, entry.recipe);
                        if (recipe.products.Length < 3 && recipe.ingredients.Length < 5)
                            gui.AllocateSpacing((3 - entry.recipe.products.Length) * 3f);
                        else if (recipe.products.Length < 3)
                            gui.allocator = RectAllocator.RemainigRow;
                        gui.BuildIcon(Icon.ArrowRight, 3f);
                    }
                }
                else
                {
                    using (gui.EnterRow())
                        DrawIngredients(gui, entry.recipe);

                    using (gui.EnterRow())
                    {
                        gui.BuildIcon(Icon.ArrowDownRight, 3f);
                        gui.allocator = RectAllocator.RightRow;
                        DrawProducts(gui, entry.recipe);
                    }
                }
                var importance = CostAnalysis.Instance.GetBuildingAmount(recipe, entry.recipeFlow);
                if (importance != null)
                    gui.BuildText(importance, wrap:true);
            }

            if (isBuilding)
            {
                var rect = gui.lastRect;
                if (entry.flow > 0f)
                {
                    var percentFlow = MathUtils.Clamp(entry.flow / currentFlow, 0f, 1f);
                    rect.Width *= percentFlow;
                    gui.DrawRectangle(rect, SchemeColor.Primary);
                } else if (waste <= 0f)
                    bgColor = SchemeColor.Secondary;
                else
                {
                    rect.Width *= (1f - entry.recipe.RecipeWaste());
                    gui.DrawRectangle(rect, SchemeColor.Secondary);
                }
                gui.DrawRectangle(gui.lastRect, bgColor);
            }
        }

        private void DrawEntryList(ImGui gui, List<RecipeEntry> entries, bool production)
        {
            foreach (var entry in entries)
            {
                if (entry.entryStatus < showRecipesRange)
                {
                    gui.BuildText(entry.entryStatus == EntryStatus.Wasteful ? "There are more recipes, but they are wasteful" :
                        entry.entryStatus == EntryStatus.NotAccessibleWithCurrentMilestones ? "There are more recipes, but they are locked based on current milestones" :
                        "There are more recipes but they are inaccessible", wrap:true);
                    if (gui.BuildButton("Show more recipes"))
                    {
                        showRecipesRange = entry.entryStatus;
                        Rebuild();
                    }
                    break;
                }
                DrawRecipeEntry(gui, entry, production);
            }
            CheckChanging();
        }

        private void BuildItemProduction(ImGui gui) => DrawEntryList(gui, productions, true);
        private void BuildItemUsages(ImGui gui) => DrawEntryList(gui, usages, false);
        
        public override void Build(ImGui gui)
        {
            BuildHeader(gui, "Never Enough Items Explorer");
            using (gui.EnterRow())
            {
                if (recent.Count == 0)
                    gui.AllocateRect(0f, 3f);
                for (var i = recent.Count - 1; i >= 0; i--)
                {
                    var elem = recent[i];
                    if (gui.BuildFactorioObjectButton(elem, 3f))
                        changing = elem;
                }
            }
            using (gui.EnterGroup(new Padding(0.5f), RectAllocator.LeftRow))
            {
                gui.spacing = 0.2f;
                gui.BuildFactorioObjectIcon(current, size:3f);
                gui.BuildText(current.locName, Font.subheader);
                gui.allocator = RectAllocator.RightAlign;
                gui.BuildText(CostAnalysis.GetDisplayCost(current));
                var amount = CostAnalysis.Instance.GetItemAmount(current);
                if (amount != null)
                    gui.BuildText(amount, wrap:true);
            }

            if (gui.BuildFactorioObjectButton(gui.lastRect, current, SchemeColor.Grey))
                SelectObjectPanel.Select(Database.goods.all, "Select item", SetItem);
                
            using (var split = gui.EnterHorizontalSplit(2))
            {
                split.Next();
                gui.BuildText("Production:", Font.subheader);
                productionList.Build(gui);
                split.Next();
                gui.BuildText("Usages:", Font.subheader);
                usageList.Build(gui);
            }
            CheckChanging();
            using (gui.EnterRow())
            {
                gui.BuildText("Legend:");
                gui.BuildText("This color is flow (Estimated fraction of item production/consumption)");
                gui.DrawRectangle(gui.lastRect, SchemeColor.Primary);
                gui.BuildText("This color is estimated recipe efficiency");
                gui.DrawRectangle(gui.lastRect, SchemeColor.Secondary);
            }
        }

        public static void Show(Goods goods, IRecipeItemFlowProvider provider)
        {
            if (Instance.opened)
            {
                Instance.changing = goods;
                return;
            }
            Instance.provider = provider ?? Instance;
            Instance.SetItem(goods);
            MainScreen.Instance.ShowPseudoScreen(Instance);
        }

        public float GetRecipeFlow(Recipe recipe) => recipe.ApproximateFlow();
        public float GetGoodsFlow(Goods goods) => goods.ApproximateFlow();
        int IComparer<RecipeEntry>.Compare(RecipeEntry x, RecipeEntry y)
        {
            if (x.entryStatus != y.entryStatus)
                return y.entryStatus - x.entryStatus;
            if (x.flow != y.flow)
                return y.flow.CompareTo(x.flow);
            return x.recipe.RecipeWaste().CompareTo(y.recipe.RecipeWaste());
        }
    }
}