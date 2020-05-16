using System;
using System.Collections.Generic;
using System.Globalization;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class ObjectTooltip : Tooltip
    {
        public static readonly Padding contentPadding = new Padding(1f, 0.25f);
        
        public ObjectTooltip() : base(new Padding(0f, 0f, 0f, 0.5f), 25f) {}
        
        private IFactorioObjectWrapper target;
        private bool extendHeader;

        private void BuildHeader(ImGui gui)
        {
            using (gui.EnterGroup(new Padding(1f, 0.5f), RectAllocator.LeftAlign, spacing:0f))
            {
                var name = target.text;
                if (extendHeader)
                {
                    if (target is Recipe recipe && recipe.mainProduct != null && recipe.mainProduct.locName == name)
                        name += " (Recipe)";
                    else if (target is Entity entity)
                    {
                        foreach (var placer in entity.itemsToPlace)
                        {
                            if (placer.locName == name)
                            {
                                name += " (Entity)";
                                break;
                            }
                        }
                    }
                }
                gui.BuildText(name, Font.header, true);
                var milestoneMask = Milestones.Instance.milestoneResult[target.target];
                if (milestoneMask > 1)
                {
                    var spacing = MathF.Min(22f / Milestones.Instance.currentMilestones.Length - 1f, 0f);
                    using (gui.EnterRow(spacing))
                    {
                        var mask = 2ul;
                        foreach (var milestone in Milestones.Instance.currentMilestones)
                        {
                            if ((milestoneMask & mask) != 0)
                                gui.BuildIcon(milestone.icon, 1f, SchemeColor.Source);
                            mask <<= 1;
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
                    gui.BuildText(target.typeDotName);
                
                if (target.locDescr != null)
                    gui.BuildText(target.locDescr, wrap:true);
                if (!target.IsAccessible())
                    gui.BuildText("This " + target.nameOfType + " is inaccessible, or it is only accessible through mod or map script. Middle click to open dependency analyser to investigate.", wrap:true);
                else if (!target.IsAutomatable())
                    gui.BuildText("This " + target.nameOfType + " cannot be fully automated. This means that it requires either manual crafting, or manual labor such as cutting trees", wrap:true);
                else gui.BuildText(CostAnalysis.GetDisplayCost(target), wrap:true);
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
                    if (entity.craftingSpeed != 1f)
                        gui.BuildText("Crafting speed: " + DataUtils.FormatPercentage(entity.craftingSpeed));
                    if (entity.productivity != 0f)
                        gui.BuildText("Crafting productivity: " + DataUtils.FormatPercentage(entity.productivity));
                    if (entity.moduleSlots > 0)
                    {
                        gui.BuildText("Module slots: " + entity.moduleSlots);
                        if (entity.allowedEffects != AllowedEffects.All)
                            gui.BuildText("Only allowed effects: "+entity.allowedEffects, wrap:true);
                    }
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
                    if (entity.energy.emissions != 0f)
                    {
                        var emissionColor = SchemeColor.BackgroundText;
                        if (entity.energy.emissions < 0f)
                        {
                            emissionColor = SchemeColor.Green;
                            gui.BuildText("This building absorbs pollution", color:emissionColor);
                        } 
                        else if (entity.energy.emissions >= 10f)
                        {
                            emissionColor = SchemeColor.Error;
                            gui.BuildText("This building contributes to global warning!", color:emissionColor);
                        }
                        gui.BuildText("Emissions: "+DataUtils.FormatAmount(entity.energy.emissions), color:emissionColor);
                    }
                }
            }
        }

        private void BuildGoods(Goods goods, ImGui gui)
        {
            BuildCommon(goods, gui);
            using (gui.EnterGroup(contentPadding))
                gui.BuildText("Middle mouse button to open Never Enough Items Explorer for this "+goods.nameOfType, wrap:true);
            if (goods.production.Length > 0)
            {
                BuildSubHeader(gui, "Made with");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, goods.production, 2);
            }
            
            if (goods.miscSources.Length > 0)
            {
                BuildSubHeader(gui, "Sources");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, goods.miscSources, 2);
            }

            if (goods.usages.Length > 0)
            {
                BuildSubHeader(gui, "Needs for");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, goods.usages, 4);
            }

            if (goods.fuelValue > 0f)
                BuildSubHeader(gui, "Fuel value: "+goods.fuelValue+" MJ");

            if (goods is Item item)
            {
                if (goods.fuelValue > 0f && item.fuelResult != null)
                    using (gui.EnterGroup(contentPadding))
                        BuildItem(gui, item.fuelResult);
                if (item.placeResult != null)
                {
                    BuildSubHeader(gui, "Place result");
                    using (gui.EnterGroup(contentPadding))
                        BuildItem(gui, item.placeResult);
                }

                if (item.module != null)
                {
                    BuildSubHeader(gui, "Module parameters");
                    using (gui.EnterGroup(contentPadding))
                    {
                        if (item.module.productivity != 0f)
                            gui.BuildText("Productivity: "+DataUtils.FormatPercentage(item.module.productivity));
                        if (item.module.speed != 0f)
                            gui.BuildText("Speed: "+DataUtils.FormatPercentage(item.module.speed));
                        if (item.module.consumption != 0f)
                            gui.BuildText("Consumption: "+DataUtils.FormatPercentage(item.module.consumption));
                        if (item.module.pollution != 0f)
                            gui.BuildText("Pollution: "+DataUtils.FormatPercentage(item.module.consumption));
                    }
                    if (item.module.limitation != null)
                    {
                        BuildSubHeader(gui, "Module limitation");
                        using (gui.EnterGroup(contentPadding))
                            BuildIconRow(gui, item.module.limitation, 2);
                    }
                }
            }
        }

        private void BuildRecipe(RecipeOrTechnology recipe, ImGui gui)
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
                if (recipe is Recipe rec)
                {
                    var waste = rec.RecipeWaste();
                    if (waste > 0.01f)
                    {
                        var wasteAmount = MathUtils.Round(waste * 100f);
                        var wasteText = ". (Wasting " + wasteAmount + "% of YAFC cost)";
                        var color = wasteAmount < 90 ? SchemeColor.BackgroundText : SchemeColor.Error;
                        if (recipe.products.Length == 1)
                            gui.BuildText("YAFC analysis: There are better recipes to create "+recipe.products[0].goods.locName+wasteText, wrap:true, color:color);
                        else if (recipe.products.Length > 0)
                            gui.BuildText("YAFC analysis: There are better recipes to create each of the products"+wasteText, wrap:true, color:color);
                        else gui.BuildText("YAFC analysis: This recipe wastes useful products. Don't do this recipe.", color:color);
                    }
                }
                if ((recipe.flags & RecipeFlags.UsesFluidTemperature) != 0)
                    gui.BuildText("Uses fluid temperature");
                if ((recipe.flags & RecipeFlags.UsesMiningProductivity) != 0)
                    gui.BuildText("Uses mining productivity");
                if ((recipe.flags & RecipeFlags.ScaleProductionWithPower) != 0)
                    gui.BuildText("Production scaled with power");
            }

            if (recipe.products.Length > 0 && !(recipe.products.Length == 1 && recipe.products[0].rawAmount == 1 && recipe.products[0].goods is Item && recipe.products[0].probability == 1f))
            {
                BuildSubHeader(gui, "Products");
                using (gui.EnterGroup(contentPadding))
                    foreach (var product in recipe.products)
                        BuildItem(gui, product);

            }

            BuildSubHeader(gui, "Made in");
            using (gui.EnterGroup(contentPadding))
                BuildIconRow(gui, recipe.crafters, 2);

            if (recipe.modules.Length > 0)
            {
                BuildSubHeader(gui, "Allowed modules");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, recipe.modules, 1);
            }

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

        public void SetFocus(IFactorioObjectWrapper target, ImGui gui, Rect rect, bool extendHeader = false)
        {
            this.extendHeader = extendHeader;
            this.target = target;
            base.SetFocus(gui, rect);
        }
    }
}