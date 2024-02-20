using System;
using System.Collections.Generic;
using YAFC.Model;
using YAFC.UI;

namespace YAFC {
    public class ObjectTooltip : Tooltip {
        public static readonly Padding contentPadding = new Padding(1f, 0.25f);

        public ObjectTooltip() : base(new Padding(0f, 0f, 0f, 0.5f), 25f) { }

        private IFactorioObjectWrapper target;
        private bool extendHeader;

        private void BuildHeader(ImGui gui) {
            using (gui.EnterGroup(new Padding(1f, 0.5f), RectAllocator.LeftAlign, spacing: 0f)) {
                var name = target.text;
                if (extendHeader && !(target is Goods))
                    name = name + " (" + target.target.type + ")";
                gui.BuildText(name, Font.header, true);
                var milestoneMask = Milestones.Instance.GetMilestoneResult(target.target);
                if (milestoneMask.HighestBitSet() > 0) {
                    var spacing = MathF.Min(22f / Milestones.Instance.currentMilestones.Length - 1f, 0f);
                    using (gui.EnterRow(spacing)) {
                        var maskBit = 1;
                        foreach (var milestone in Milestones.Instance.currentMilestones) {
                            if (milestoneMask[maskBit])
                                gui.BuildIcon(milestone.icon, 1f, SchemeColor.Source);
                            maskBit++;
                        }
                    }
                }
            }
            if (gui.isBuilding)
                gui.DrawRectangle(gui.lastRect, SchemeColor.Primary);
        }

        private void BuildSubHeader(ImGui gui, string text) {
            using (gui.EnterGroup(contentPadding))
                gui.BuildText(text, Font.subheader);
            if (gui.isBuilding)
                gui.DrawRectangle(gui.lastRect, SchemeColor.Grey);
        }

        private void BuildIconRow(ImGui gui, IReadOnlyList<FactorioObject> objects, int maxRows) {
            const int itemsPerRow = 9;
            var count = objects.Count;
            if (count == 0) {
                gui.BuildText("Nothing", color: SchemeColor.BackgroundTextFaint);
                return;
            }

            var arr = new List<FactorioObject>(count);
            arr.AddRange(objects);
            arr.Sort(DataUtils.DefaultOrdering);

            if (count <= maxRows) {
                for (var i = 0; i < count; i++)
                    gui.BuildFactorioObjectButtonWithText(arr[i]);
                return;
            }

            var index = 0;
            if (count - 1 < (maxRows - 1) * itemsPerRow) {
                gui.BuildFactorioObjectButtonWithText(arr[0]);
                index++;
            }

            var rows = Math.Min(((count - 1 - index) / itemsPerRow) + 1, maxRows);
            for (var i = 0; i < rows; i++) {
                using (gui.EnterRow()) {
                    for (var j = 0; j < itemsPerRow; j++) {
                        if (arr.Count <= index)
                            return;
                        gui.BuildFactorioObjectIcon(arr[index++]);
                    }
                }
            }

            if (rows * itemsPerRow < count)
                gui.BuildText("... and " + (count - rows * itemsPerRow) + " more");
        }

        private void BuildItem(ImGui gui, IFactorioObjectWrapper item) {
            using (gui.EnterRow()) {
                gui.BuildFactorioObjectIcon(item.target);
                gui.BuildText(item.text, wrap: true);
            }
        }

        protected override void BuildContents(ImGui gui) {
            switch (target.target) {
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

        private void BuildCommon(FactorioObject target, ImGui gui) {
            BuildHeader(gui);
            using (gui.EnterGroup(contentPadding)) {
                if (InputSystem.Instance.control)
                    gui.BuildText(target.typeDotName);

                if (target.locDescr != null)
                    gui.BuildText(target.locDescr, wrap: true);
                if (!target.IsAccessible())
                    gui.BuildText("This " + target.type + " is inaccessible, or it is only accessible through mod or map script. Middle click to open dependency analyser to investigate.", wrap: true);
                else if (!target.IsAutomatable())
                    gui.BuildText("This " + target.type + " cannot be fully automated. This means that it requires either manual crafting, or manual labor such as cutting trees", wrap: true);
                else gui.BuildText(CostAnalysis.GetDisplayCost(target), wrap: true);
                if (target.IsAccessibleWithCurrentMilestones() && !target.IsAutomatableWithCurrentMilestones())
                    gui.BuildText("This " + target.type + " cannot be fully automated at current milestones.", wrap: true);
                if (target.specialType != FactorioObjectSpecialType.Normal)
                    gui.BuildText("Special: " + target.specialType);
            }
        }

        private static readonly Dictionary<EntityEnergyType, string> EnergyDescriptions = new Dictionary<EntityEnergyType, string>
        {
            {EntityEnergyType.Electric, "Power usage: "},
            {EntityEnergyType.Heat, "Heat energy usage: "},
            {EntityEnergyType.Labor, "Labor energy usage: "},
            {EntityEnergyType.Void, "Free energy usage: "},
            {EntityEnergyType.FluidFuel, "Fluid fuel energy usage: "},
            {EntityEnergyType.FluidHeat, "Fluid heat energy usage: "},
            {EntityEnergyType.SolidFuel, "Solid fuel energy usage: "},
        };

        private void BuildEntity(Entity entity, ImGui gui) {
            BuildCommon(entity, gui);

            if (entity.loot.Length > 0) {
                BuildSubHeader(gui, "Loot");
                using (gui.EnterGroup(contentPadding)) {
                    foreach (var product in entity.loot)
                        BuildItem(gui, product);
                }
            }

            if (entity.mapGenerated)
                using (gui.EnterGroup(contentPadding))
                    gui.BuildText("Generates on map (estimated density: " + (entity.mapGenDensity <= 0f ? "unknown" : DataUtils.FormatAmount(entity.mapGenDensity, UnitOfMeasure.None)) + ")", wrap: true);

            if (entity is EntityCrafter crafter) {
                if (crafter.recipes.Length > 0) {
                    BuildSubHeader(gui, "Crafts");
                    using (gui.EnterGroup(contentPadding)) {
                        BuildIconRow(gui, crafter.recipes, 2);
                        if (crafter.craftingSpeed != 1f)
                            gui.BuildText(DataUtils.FormatAmount(crafter.craftingSpeed, UnitOfMeasure.Percent, "Crafting speed: "));
                        if (crafter.productivity != 0f)
                            gui.BuildText(DataUtils.FormatAmount(crafter.productivity, UnitOfMeasure.Percent, "Crafting productivity: "));
                        if (crafter.allowedEffects != AllowedEffects.None) {
                            gui.BuildText("Module slots: " + crafter.moduleSlots);
                            if (crafter.allowedEffects != AllowedEffects.All)
                                gui.BuildText("Only allowed effects: " + crafter.allowedEffects, wrap: true);
                        }
                    }
                }

                if (crafter.inputs != null) {
                    BuildSubHeader(gui, "Allowed inputs:");
                    using (gui.EnterGroup(contentPadding))
                        BuildIconRow(gui, crafter.inputs, 2);
                }
            }

            if (entity.energy != null) {
                var energyUsage = EnergyDescriptions[entity.energy.type] + DataUtils.FormatAmount(entity.power, UnitOfMeasure.Megawatt);
                if (entity.energy.drain > 0f)
                    energyUsage += " + " + DataUtils.FormatAmount(entity.energy.drain, UnitOfMeasure.Megawatt);
                BuildSubHeader(gui, energyUsage);
                using (gui.EnterGroup(contentPadding)) {
                    if (entity.energy.type == EntityEnergyType.FluidFuel || entity.energy.type == EntityEnergyType.SolidFuel || entity.energy.type == EntityEnergyType.FluidHeat)
                        BuildIconRow(gui, entity.energy.fuels, 2);
                    if (entity.energy.emissions != 0f) {
                        var emissionColor = SchemeColor.BackgroundText;
                        if (entity.energy.emissions < 0f) {
                            emissionColor = SchemeColor.Green;
                            gui.BuildText("This building absorbs pollution", color: emissionColor);
                        }
                        else if (entity.energy.emissions >= 20f) {
                            emissionColor = SchemeColor.Error;
                            gui.BuildText("This building contributes to global warning!", color: emissionColor);
                        }
                        gui.BuildText("Emissions: " + DataUtils.FormatAmount(entity.energy.emissions, UnitOfMeasure.None), color: emissionColor);
                    }
                }
            }

            string miscText = null;

            switch (entity) {
                case EntityBelt belt:
                    miscText = "Belt throughput (Items): " + DataUtils.FormatAmount(belt.beltItemsPerSecond, UnitOfMeasure.PerSecond);
                    break;
                case EntityInserter inserter:
                    miscText = "Swing time: " + DataUtils.FormatAmount(inserter.inserterSwingTime, UnitOfMeasure.Second);
                    break;
                case EntityBeacon beacon:
                    miscText = "Beacon efficiency: " + DataUtils.FormatAmount(beacon.beaconEfficiency, UnitOfMeasure.Percent);
                    break;
                case EntityAccumulator accumulator:
                    miscText = "Accumulator charge: " + DataUtils.FormatAmount(accumulator.accumulatorCapacity, UnitOfMeasure.Megajoule);
                    break;
                case EntityCrafter solarPanel:
                    if (solarPanel.craftingSpeed > 0f && entity.factorioType == "solar-panel")
                        miscText = "Power production (average): " + DataUtils.FormatAmount(solarPanel.craftingSpeed, UnitOfMeasure.Megawatt);
                    break;
            }

            if (miscText != null)
                using (gui.EnterGroup(contentPadding))
                    gui.BuildText(miscText);
        }

        private void BuildGoods(Goods goods, ImGui gui) {
            BuildCommon(goods, gui);
            using (gui.EnterGroup(contentPadding))
                gui.BuildText("Middle mouse button to open Never Enough Items Explorer for this " + goods.type, wrap: true);
            if (goods.production.Length > 0) {
                BuildSubHeader(gui, "Made with");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, goods.production, 2);
            }

            if (goods.miscSources.Length > 0) {
                BuildSubHeader(gui, "Sources");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, goods.miscSources, 2);
            }

            if (goods.usages.Length > 0) {
                BuildSubHeader(gui, "Needed for");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, goods.usages, 4);
            }

            if (goods.fuelFor.Length > 0) {
                if (goods.fuelValue > 0f)
                    BuildSubHeader(gui, "Fuel value " + DataUtils.FormatAmount(goods.fuelValue, UnitOfMeasure.Megajoule) + " used for:");
                else BuildSubHeader(gui, "Can be used as fuel for:");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, goods.fuelFor, 2);
            }

            if (goods is Item item) {
                if (goods.fuelValue > 0f && item.fuelResult != null)
                    using (gui.EnterGroup(contentPadding))
                        BuildItem(gui, item.fuelResult);
                if (item.placeResult != null) {
                    BuildSubHeader(gui, "Place result");
                    using (gui.EnterGroup(contentPadding))
                        BuildItem(gui, item.placeResult);
                }

                if (item.module != null) {
                    BuildSubHeader(gui, "Module parameters");
                    using (gui.EnterGroup(contentPadding)) {
                        if (item.module.productivity != 0f)
                            gui.BuildText(DataUtils.FormatAmount(item.module.productivity, UnitOfMeasure.Percent, "Productivity: "));
                        if (item.module.speed != 0f)
                            gui.BuildText(DataUtils.FormatAmount(item.module.speed, UnitOfMeasure.Percent, "Speed: "));
                        if (item.module.consumption != 0f)
                            gui.BuildText(DataUtils.FormatAmount(item.module.consumption, UnitOfMeasure.Percent, "Consumption: "));
                        if (item.module.pollution != 0f)
                            gui.BuildText(DataUtils.FormatAmount(item.module.consumption, UnitOfMeasure.Percent, "Pollution: "));
                    }
                    if (item.module.limitation != null) {
                        BuildSubHeader(gui, "Module limitation");
                        using (gui.EnterGroup(contentPadding))
                            BuildIconRow(gui, item.module.limitation, 2);
                    }
                }

                using (gui.EnterGroup(contentPadding))
                    gui.BuildText("Stack size: " + item.stackSize);
            }
        }

        private void BuildRecipe(RecipeOrTechnology recipe, ImGui gui) {
            BuildCommon(recipe, gui);
            using (gui.EnterGroup(contentPadding, RectAllocator.LeftRow)) {
                gui.BuildIcon(Icon.Time, 2f, SchemeColor.BackgroundText);
                gui.BuildText(DataUtils.FormatAmount(recipe.time, UnitOfMeasure.Second));
            }

            using (gui.EnterGroup(contentPadding)) {
                foreach (var ingredient in recipe.ingredients)
                    BuildItem(gui, ingredient);
                if (recipe is Recipe rec) {
                    var waste = rec.RecipeWaste();
                    if (waste > 0.01f) {
                        var wasteAmount = MathUtils.Round(waste * 100f);
                        var wasteText = ". (Wasting " + wasteAmount + "% of YAFC cost)";
                        var color = wasteAmount < 90 ? SchemeColor.BackgroundText : SchemeColor.Error;
                        if (recipe.products.Length == 1)
                            gui.BuildText("YAFC analysis: There are better recipes to create " + recipe.products[0].goods.locName + wasteText, wrap: true, color: color);
                        else if (recipe.products.Length > 0)
                            gui.BuildText("YAFC analysis: There are better recipes to create each of the products" + wasteText, wrap: true, color: color);
                        else gui.BuildText("YAFC analysis: This recipe wastes useful products. Don't do this recipe.", wrap: true, color: color);
                    }
                }
                if (recipe.flags.HasFlags(RecipeFlags.UsesFluidTemperature))
                    gui.BuildText("Uses fluid temperature");
                if (recipe.flags.HasFlags(RecipeFlags.UsesMiningProductivity))
                    gui.BuildText("Uses mining productivity");
                if (recipe.flags.HasFlags(RecipeFlags.ScaleProductionWithPower))
                    gui.BuildText("Production scaled with power");
            }

            if (recipe.products.Length > 0 && !(recipe.products.Length == 1 && recipe.products[0].IsSimple && recipe.products[0].goods is Item && recipe.products[0].amount == 1f)) {
                BuildSubHeader(gui, "Products");
                using (gui.EnterGroup(contentPadding))
                    foreach (var product in recipe.products)
                        BuildItem(gui, product);

            }

            BuildSubHeader(gui, "Made in");
            using (gui.EnterGroup(contentPadding))
                BuildIconRow(gui, recipe.crafters, 2);

            if (recipe.modules.Length > 0) {
                BuildSubHeader(gui, "Allowed modules");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, recipe.modules, 1);
                var crafterCommonModules = AllowedEffects.All;
                foreach (var crafter in recipe.crafters) {
                    if (crafter.moduleSlots > 0)
                        crafterCommonModules &= crafter.allowedEffects;
                }

                foreach (var module in recipe.modules) {
                    if (!EntityWithModules.CanAcceptModule(module.module, crafterCommonModules)) {
                        using (gui.EnterGroup(contentPadding))
                            gui.BuildText("Some crafters restrict module usage");
                        break;
                    }
                }
            }

            if (recipe is Recipe lockedRecipe && !lockedRecipe.enabled) {
                BuildSubHeader(gui, "Unlocked by");
                using (gui.EnterGroup(contentPadding)) {
                    if (lockedRecipe.technologyUnlock.Length > 2) {
                        BuildIconRow(gui, lockedRecipe.technologyUnlock, 1);
                    }
                    else {
                        foreach (var technology in lockedRecipe.technologyUnlock) {
                            var ingredient = TechnologyScienceAnalysis.Instance.GetMaxTechnologyIngredient(technology);
                            using (gui.EnterRow(allocator: RectAllocator.RightRow)) {
                                gui.spacing = 0f;
                                if (ingredient != null) {
                                    gui.BuildFactorioObjectIcon(ingredient.goods);
                                    gui.BuildText(DataUtils.FormatAmount(ingredient.amount, UnitOfMeasure.None));
                                }

                                gui.allocator = RectAllocator.RemainingRow;
                                gui.BuildFactorioObjectButtonWithText(technology);
                            }
                        }
                    }
                }
            }
        }

        private void BuildTechnology(Technology technology, ImGui gui) {
            BuildRecipe(technology, gui);
            if (technology.hidden && !technology.enabled) {
                using (gui.EnterGroup(contentPadding))
                    gui.BuildText("This technology is hidden from the list and cannot be researched.", wrap: true);
            }

            if (technology.prerequisites.Length > 0) {
                BuildSubHeader(gui, "Prerequisites");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, technology.prerequisites, 1);
            }

            if (technology.unlockRecipes.Length > 0) {
                BuildSubHeader(gui, "Unlocks recipes");
                using (gui.EnterGroup(contentPadding))
                    BuildIconRow(gui, technology.unlockRecipes, 2);
            }

            var packs = TechnologyScienceAnalysis.Instance.allSciencePacks[technology];
            if (packs.Length > 0) {
                BuildSubHeader(gui, "Total science required");
                using (gui.EnterGroup(contentPadding)) {
                    using (var grid = gui.EnterInlineGrid(3f)) {
                        foreach (var pack in packs) {
                            grid.Next();
                            gui.BuildFactorioObjectWithAmount(pack.goods, pack.amount, UnitOfMeasure.None);
                        }
                    }
                }
            }
        }

        public void SetFocus(IFactorioObjectWrapper target, ImGui gui, Rect rect, bool extendHeader = false) {
            this.extendHeader = extendHeader;
            this.target = target;
            base.SetFocus(gui, rect);
        }

        public bool IsSameObjectHovered(ImGui gui, FactorioObject factorioObject) => source == gui && factorioObject == target.target && gui.IsMouseOver(sourceRect);
    }
}
