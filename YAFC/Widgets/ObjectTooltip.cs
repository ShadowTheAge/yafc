using System;
using System.Collections.Generic;
using System.Globalization;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class ObjectTooltip : Tooltip
    {
        private static readonly Padding contentPadding = new Padding(1f, 0.25f);
        
        public ObjectTooltip() : base(new Padding(0f, 0f, 0f, 0.5f), 25f) {}
        
        private IFactorioObjectWrapper target;

        private void BuildHeader(ImGui gui)
        {
            using (gui.EnterGroup(new Padding(1f, 0.5f), RectAllocator.LeftAlign, spacing:0f))
            {
                gui.BuildText(target.text, Font.header, true);
                if (Milestones.milestoneResult[target.target.id] > 1)
                {
                    var spacing = MathF.Min(22f / Milestones.milestones.Count - 1f, 0f);
                    using (gui.EnterRow(spacing))
                    {
                        foreach (var milestone in Milestones.milestones)
                        {
                            if (milestone[target.target])
                                gui.BuildIcon(milestone.obj.icon, 1f, SchemeColor.Source);
                        }
                    }
                }
            }
            if (gui.isBuilding)
                gui.DrawRectangle(gui.lastRect, SchemeColor.Primary);
        }

        private void BuildSubHeader(ImGui gui, string text)
        {
            using (gui.EnterGroup(contentPadding))
                gui.BuildText(text, Font.subheader);
            if (gui.isBuilding)
                gui.DrawRectangle(gui.lastRect, SchemeColor.Grey);
        }

        private void BuildIconRow(ImGui gui, IReadOnlyList<FactorioObject> objects, int maxRows)
        {
            const int itemsPerRow = 9;
            var count = objects.Count;
            if (count == 0)
            {
                gui.BuildText("Nothing", color : SchemeColor.BackgroundTextFaint);
                return;
            }

            var arr = new List<FactorioObject>(count);
            arr.AddRange(objects);
            arr.Sort(DataUtils.DefaultOrdering);

            var index = 0;
            var rows = Math.Min(((count-1) / itemsPerRow)+1, maxRows);
            for (var i = 0; i < rows; i++)
            {
                using (gui.EnterRow())
                {
                    for (var j = 0; j < itemsPerRow; j++)
                    {
                        if (arr.Count <= index)
                            return;
                        gui.BuildFactorioObjectIcon(arr[index++]);
                    }
                }
            }

            if (rows*itemsPerRow < count)
                gui.BuildText("... and "+(count-rows*itemsPerRow)+" more");
        }

        private void BuildItem(ImGui gui, IFactorioObjectWrapper item)
        {
            using (gui.EnterRow())
            {
                gui.BuildFactorioObjectIcon(item.target);
                gui.BuildText(item.text, wrap:true);
            }
        }

        protected override void BuildContents(ImGui gui)
        {
            switch (target.target)
            {
                case Technology technology:
                    BuildTechnology(technology, gui);
                    break;
                case Recipe recipe:
                    BuildRecipe(recipe, gui);
                    break;
                case Goods goods:
                    BuildGoods(goods, gui);
                    break;
                case Entity entity:
                    BuildEntity(entity, gui);
                    break;
                default:
                    BuildCommon(target.target, gui);
                    break;
            }
        }

        private void BuildCommon(FactorioObject target, ImGui gui)
        {
            BuildHeader(gui);
            using (gui.EnterGroup(contentPadding))
            {
                if (InputSystem.Instance.control)
                    gui.BuildText(target.name);
                
                if (target.locDescr != null)
                    gui.BuildText(target.locDescr, wrap:true);
                if (!target.IsAccessible())
                    gui.BuildText("This " + target.GetType().Name + " is inaccessible, or it is only accessible through mod or map script. Middle click to open dependency analyser to investigate.", wrap:true);
                else gui.BuildText(CostAnalysis.GetDisplay(target), wrap:true);
            }
        }

        private void BuildEntity(Entity entity, ImGui gui)
        {
            BuildCommon(entity, gui);
            
            if (entity.loot.Length > 0)
            {
                BuildSubHeader(gui, "Loot");
                using (gui.EnterGroup(contentPadding))
                {
                    foreach (var product in entity.loot)
                        BuildItem(gui, product);
                }
            }
            
            if (entity.mapGenerated)
                using (gui.EnterGroup(contentPadding))
                    gui.BuildText("Generates on map (estimated density: "+(entity.mapGenDensity <= 0f ? "unknown" : DataUtils.FormatAmount(entity.mapGenDensity))+")", wrap:true);

            if (!entity.recipes.empty)
            {
                BuildSubHeader(gui, "Crafts");
                using (gui.EnterGroup(contentPadding))
                {
                    BuildIconRow(gui, entity.recipes, 2);
                    gui.BuildText("Crafting speed: " + entity.craftingSpeed);
                }
            }

            if (entity.energy != null)
            {
                BuildSubHeader(gui, "Energy usage: "+entity.power+" MW");
                using (gui.EnterGroup(contentPadding))
                {
                    BuildIconRow(gui, entity.energy.fuels, 2);
                    if (entity.energy.usesHeat)
                        gui.BuildText("Uses heat");
                }
            }
        }

        private void BuildGoods(Goods goods, ImGui gui)
        {
            BuildCommon(goods, gui);
            if (goods.production.Length > 0)
            {
                BuildSubHeader(gui, "Made with");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, goods.production, 2);
            }
            
            if (goods.loot.Length > 0)
            {
                BuildSubHeader(gui, "Looted from");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, goods.loot, 2);
            }

            if (goods.usages.Length > 0)
            {
                BuildSubHeader(gui, "Needs for");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, goods.usages, 4);
            }

            if (goods.fuelValue != 0f)
            {
                BuildSubHeader(gui, "Fuel value: "+goods.fuelValue+" MJ");
                if (goods is Item item && item.fuelResult != null)
                    using (gui.EnterGroup(contentPadding))
                        BuildItem(gui, item.fuelResult);
            }

            if (goods is Item item2 && item2.placeResult != null)
            {
                BuildSubHeader(gui, "Place result");
                using (gui.EnterGroup(contentPadding))
                    BuildItem(gui, item2.placeResult);
            }
        }

        private void BuildRecipe(Recipe recipe, ImGui gui)
        {
            BuildCommon(recipe, gui);
            using (gui.EnterGroup(contentPadding, RectAllocator.LeftRow))
            {
                gui.BuildIcon(Icon.Time, 2f, SchemeColor.BackgroundText);
                gui.BuildText(recipe.time.ToString(CultureInfo.InvariantCulture));
            }

            using (gui.EnterGroup(contentPadding))
            {
                foreach (var ingredient in recipe.ingredients)
                    BuildItem(gui, ingredient);
                if (recipe.IsSubOptimal())
                {
                    var productCost = 0f;
                    foreach (var product in recipe.products)
                        productCost += product.average * product.goods.Cost();
                    var wasteAmount = MathUtils.Round((1f - productCost / recipe.Cost()) * 100f);
                    if (wasteAmount > 0)
                    {
                        var wasteText = ". (Wasting " + wasteAmount + "% of YAFC cost)";
                    
                        if (recipe.products.Length == 1)
                            gui.BuildText("YAFC analysis: There are better recipes to create "+recipe.products[0].goods.locName+wasteText, wrap:true);
                        else if (recipe.products.Length > 0)
                            gui.BuildText("YAFC analysis: There are better recipes to create each of the products"+wasteText, wrap:true);
                        else gui.BuildText("YAFC analysis: This recipe wastes useful products. Don't do this recipe.");
                    }
                }
            }

            if (recipe.products.Length > 0 && !(recipe.products.Length == 1 && recipe.products[0].amount == 1 && recipe.products[0].goods is Item && recipe.products[0].probability == 1f))
            {
                BuildSubHeader(gui, "Products");
                using (gui.EnterGroup(contentPadding))
                    foreach (var product in recipe.products)
                        BuildItem(gui, product);

            }

            BuildSubHeader(gui, "Made in");
            using (gui.EnterGroup(contentPadding))
                BuildIconRow(gui, recipe.crafters, 2);
            
            if (!recipe.enabled)
            {
                BuildSubHeader(gui, "Unlocked by");
                using (gui.EnterGroup(contentPadding))
                {
                    BuildIconRow(gui, recipe.technologyUnlock, 1);
                }
            }
        }

        private void BuildTechnology(Technology technology, ImGui gui)
        {
            BuildRecipe(technology, gui);
            if (technology.prerequisites.Length > 0)
            {
                BuildSubHeader(gui, "Prerequisites");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, technology.prerequisites, 1);
            }
            
            if (technology.unlockRecipes.Length > 0)
            {
                BuildSubHeader(gui, "Unlocks recipes");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, technology.unlockRecipes, 2);
            }
        }

        public void Show(IFactorioObjectWrapper target, ImGui gui, Rect rect)
        {
            this.target = target;
            base.SetFocus(gui, rect);
        }
    }
}