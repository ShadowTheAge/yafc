using System.Collections.Generic;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class NeverEnoughItemsPanel : PseudoScreen, IComparer<NeverEnoughItemsPanel.RecipeEntry>
    {
        private static readonly NeverEnoughItemsPanel Instance = new NeverEnoughItemsPanel(); 
        private Goods current;
        private Goods changing;
        private float currentFlow;
        private EntryStatus showRecipesRange = EntryStatus.Normal;
        private bool truncated = true;
        private readonly List<Goods> recent = new List<Goods>();
        private bool atCurrentMilestones;

        private readonly VerticalScrollCustom productionList;
        private readonly VerticalScrollCustom usageList;
        
        private enum EntryStatus
        {
            NotAccessible,
            NotAccessibleWithCurrentMilestones,
            Special,
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

            public RecipeEntry(Recipe recipe, bool isProduction, Goods currentItem, bool atCurrentMilestones)
            {
                this.recipe = recipe;
                var amount = isProduction ? recipe.GetProduction(currentItem) : recipe.GetConsumption(currentItem);
                recipeFlow = recipe.ApproximateFlow(atCurrentMilestones);
                flow = recipeFlow * amount;
                specificEfficiency = isProduction ? recipe.Cost() / amount : 0f;
                if (!recipe.IsAccessible())
                    entryStatus = EntryStatus.NotAccessible;
                else if (!recipe.IsAccessibleWithCurrentMilestones())
                    entryStatus = EntryStatus.NotAccessibleWithCurrentMilestones;
                else
                {
                    var waste = recipe.RecipeWaste(atCurrentMilestones);
                    if (recipe.specialType != FactorioObjectSpecialType.Normal && recipeFlow <= 0.01f)
                        entryStatus = EntryStatus.Special;
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
            if (current == this.current)
                return;
            recent.Remove(current);
            if (recent.Count > 18)
                recent.RemoveAt(0);
            if (this.current != null)
                recent.Add(this.current);
            this.current = current;
            currentFlow = current.ApproximateFlow(atCurrentMilestones);
            productions.Clear();
            foreach (var recipe in current.production)
                productions.Add(new RecipeEntry(recipe, true,  current, atCurrentMilestones));
            productions.Sort(this);
            usages.Clear();
            foreach (var usage in current.usages)
                usages.Add(new RecipeEntry(usage, false, current, atCurrentMilestones));
            usages.Sort(this);
            truncated = true;
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
                {
                    if (ingredient.variants != null)
                    {
                        gui.ShowDropDown((ImGui imGui, ref bool closed) =>
                        {
                            if (imGui.BuildInlineObejctListAndButton<Goods>(ingredient.variants, DataUtils.DefaultOrdering, SetItem, "Accepted fluid variants"))
                                closed = true;
                        });
                    } else
                        changing = ingredient.goods;
                }
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
            using (var grid = gui.EnterInlineGrid(3f, 0f, maxElemCount))
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
            var waste = recipe.RecipeWaste(atCurrentMilestones);
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
                    gui.BuildText(DataUtils.FormatAmount(recipe.Cost(atCurrentMilestones), UnitOfMeasure.None, "¥"), align:RectAlignment.Middle);
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
                    rect.Width *= (1f - waste);
                    gui.DrawRectangle(rect, SchemeColor.Secondary);
                }
                gui.DrawRectangle(gui.lastRect, bgColor);
            }
        }

        private void DrawEntryFooter(ImGui gui, bool production)
        {
            if (!production && current.fuelFor.Length > 0)
            {
                using (gui.EnterGroup(new Padding(0.5f), RectAllocator.LeftAlign))
                {
                    gui.BuildText(current.fuelValue > 0f ? "Fuel value "+DataUtils.FormatAmount(current.fuelValue, UnitOfMeasure.Megajoule)+" can be used for:" : "Can be used to fuel:");
                    using (var grid = gui.EnterInlineGrid(3f))
                    {
                        foreach (var fuelUsage in current.fuelFor)
                        {
                            grid.Next();
                            gui.BuildFactorioObjectButton(fuelUsage, 3f, MilestoneDisplay.Contained);
                        }
                    }
                }
                if (gui.isBuilding)
                    gui.DrawRectangle(gui.lastRect, SchemeColor.Primary);
            }
        }

        private void ChangeShowStatus(EntryStatus status)
        {
            showRecipesRange = status;
            Rebuild();
            productionList.Rebuild();
            usageList.Rebuild();
        }

        private void DrawEntryList(ImGui gui, List<RecipeEntry> entries, bool production)
        {
            var footerDrawn = false;
            var prevEntryStatus = EntryStatus.Normal;
            FactorioObject prevLatestMilestone = null;
            var count = 0;
            foreach (var entry in entries)
            {
                if (++count >= 100 && truncated)
                {
                    gui.BuildText("The list is truncated to avoid slowdown");
                    if (gui.BuildButton("Show all"))
                    {
                        truncated = false;
                        ChangeShowStatus(showRecipesRange);
                    }
                    break;
                }
                
                var status = entry.entryStatus;

                if (status < showRecipesRange)
                {
                    DrawEntryFooter(gui, production);
                    footerDrawn = true;
                    gui.BuildText(entry.entryStatus == EntryStatus.Special ? "Show special recipes (barreling / voiding)" :
                        entry.entryStatus == EntryStatus.NotAccessibleWithCurrentMilestones ? "There are more recipes, but they are locked based on current milestones" :
                        "There are more recipes but they are inaccessible", wrap:true);
                    if (gui.BuildButton("Show more recipes"))
                        ChangeShowStatus(status);
                    break;
                }
                
                if (status < prevEntryStatus)
                {
                    prevEntryStatus = status;
                    using (gui.EnterRow())
                    {
                        gui.BuildText(status == EntryStatus.Special ? "Special recipes:" : status == EntryStatus.NotAccessibleWithCurrentMilestones ? "Locked recipes:" : "Inaccessible recipes:");
                        if (gui.BuildLink("hide"))
                            ChangeShowStatus(status + 1);
                    }
                }

                if (status == EntryStatus.NotAccessibleWithCurrentMilestones)
                {
                    var latest = Milestones.Instance.GetHighest(entry.recipe, false);
                    if (latest != prevLatestMilestone)
                    {
                        gui.BuildFactorioObjectButtonWithText(latest, size:3f, display:MilestoneDisplay.None);
                        prevLatestMilestone = latest;
                    }
                }
                
                DrawRecipeEntry(gui, entry, production);
            }
            if (!footerDrawn)
                DrawEntryFooter(gui, production);
            CheckChanging();
        }

        private void BuildItemProduction(ImGui gui) => DrawEntryList(gui, productions, true);
        private void BuildItemUsages(ImGui gui) => DrawEntryList(gui, usages, false);

        private void FullRebuild()
        {
            var item = current;
            current = null;
            SetItem(item);
        }
        
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
                if (gui.BuildLink("What do colored bars mean?"))
                {
                    MessageBox.Show("How to read colored bars", 
                        "Blue bar means estimated production or comsumption of the thing you selected. Blue bar at 50% means that that recipe produces(consumes) 50% of the product.\n\n" +
                        "Orange bar means estimated recipe efficiency. If it is not full, the recipe looks inefficient to YAFC.\n\n" +
                        "It is possible for a recipe to be efficient but not useful - for example a recipe that produces something that is not useful.\n\n" +
                        "YAFC only estimates things that are required for science recipes. So buildings, belts, weapons, fuel - are not shown in estimations.", "Ok");
                }
                if (gui.BuildCheckBox("Current milestones info", atCurrentMilestones, out atCurrentMilestones, allocator:RectAllocator.RightRow))
                {
                    var item = current;
                    current = null;
                    SetItem(item);
                }
            }
        }

        public static void Show(Goods goods)
        {
            if (Instance.opened)
            {
                Instance.changing = goods;
                return;
            }
            Instance.SetItem(goods);
            MainScreen.Instance.ShowPseudoScreen(Instance);
        }
        int IComparer<RecipeEntry>.Compare(RecipeEntry x, RecipeEntry y)
        {
            if (x.entryStatus != y.entryStatus)
                return y.entryStatus - x.entryStatus;
            if (x.entryStatus == EntryStatus.NotAccessibleWithCurrentMilestones)
            {
                var xMilestone = DataUtils.GetMilestoneOrder(x.recipe.id);
                var yMilestone = DataUtils.GetMilestoneOrder(y.recipe.id);
                if (xMilestone != yMilestone)
                    return xMilestone.CompareTo(yMilestone);
            }
            if (x.flow != y.flow)
                return y.flow.CompareTo(x.flow);
            return x.recipe.RecipeWaste(atCurrentMilestones).CompareTo(y.recipe.RecipeWaste(atCurrentMilestones));
        }
    }
}