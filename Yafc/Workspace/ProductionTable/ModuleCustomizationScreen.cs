using System;
using System.Collections.Generic;
using System.Linq;
using Yafc.Model;
using Yafc.UI;

namespace Yafc {
    public class ModuleCustomizationScreen : PseudoScreen {
        private static readonly ModuleCustomizationScreen Instance = new ModuleCustomizationScreen();

        private RecipeRow? recipe;
        private ProjectModuleTemplate? template;
        private ModuleTemplate? modules;

        public static void Show(RecipeRow recipe) {
            Instance.template = null;
            Instance.recipe = recipe;
            Instance.modules = recipe.modules;
            _ = MainScreen.Instance.ShowPseudoScreen(Instance);
        }

        public static void Show(ProjectModuleTemplate template) {
            Instance.recipe = null;
            Instance.template = template;
            Instance.modules = template.template;
            _ = MainScreen.Instance.ShowPseudoScreen(Instance);
        }

        public override void Build(ImGui gui) {
            BuildHeader(gui, "Module customization");
            if (template != null) {
                using (gui.EnterRow()) {
                    if (gui.BuildFactorioObjectButton(template.icon, ButtonDisplayStyle.Default) == Click.Left) {
                        SelectSingleObjectPanel.SelectWithNone(Database.objects.all, "Select icon", x => {
                            template.RecordUndo().icon = x;
                            Rebuild();
                        });
                    }

                    if (gui.BuildTextInput(template.name, out string newName, "Enter name", delayed: true) && newName != "") {
                        template.RecordUndo().name = newName;
                    }
                }
                gui.BuildText("Filter by crafting buildings (Optional):");
                using var grid = gui.EnterInlineGrid(2f, 1f);
                for (int i = 0; i < template.filterEntities.Count; i++) {
                    var entity = template.filterEntities[i];
                    grid.Next();
                    gui.BuildFactorioObjectIcon(entity, new(2, MilestoneDisplay.Contained, false));
                    if (gui.BuildMouseOverIcon(Icon.Close, SchemeColor.Error)) {
                        template.RecordUndo().filterEntities.RemoveAt(i);
                    }
                }
                grid.Next();
                if (gui.BuildButton(Icon.Plus, SchemeColor.Primary, SchemeColor.PrimaryAlt, size: 1.5f)) {
                    SelectSingleObjectPanel.Select(Database.allCrafters.Where(x => x.allowedEffects != AllowedEffects.None && !template.filterEntities.Contains(x)), "Add module template filter", sel => {
                        template.RecordUndo().filterEntities.Add(sel);
                        gui.Rebuild();
                    });
                }
            }
            if (modules == null) {
                if (gui.BuildButton("Enable custom modules")) {
                    // null-forgiving: If modules is null, we got here from Show(RecipeRow recipe), and recipe is not null.
                    recipe!.RecordUndo().modules = new ModuleTemplate(recipe!);
                    modules = recipe!.modules;
                }
            }
            else {
                ModuleEffects effects = new ModuleEffects();
                if (recipe == null || recipe.entity?.moduleSlots > 0) {
                    gui.BuildText("Internal modules:", Font.subheader);
                    gui.BuildText("Leave zero amount to fill the remaining slots");
                    DrawRecipeModules(gui, null, ref effects);
                }
                else {
                    gui.BuildText("This building doesn't have module slots, but can be affected by beacons");
                }
                gui.BuildText("Beacon modules:", Font.subheader);
                if (modules.beacon == null) {
                    gui.BuildText("Use default parameters");
                    if (gui.BuildButton("Override beacons as well")) {
                        SelectBeacon(gui);
                    }

                    var defaultFiller = recipe?.GetModuleFiller();
                    if (defaultFiller?.GetBeaconsForCrafter(recipe?.entity) is BeaconConfiguration { beacon: not null, beaconModule: not null } beaconsToUse) {
                        effects.AddModules(beaconsToUse.beaconModule.moduleSpecification, beaconsToUse.beacon.beaconEfficiency * beaconsToUse.beacon.moduleSlots * beaconsToUse.beaconCount);
                    }
                }
                else {
                    if (gui.BuildFactorioObjectButtonWithText(modules.beacon) == Click.Left) {
                        SelectBeacon(gui);
                    }

                    gui.BuildText("Input the amount of modules, not the amount of beacons. Single beacon can hold " + modules.beacon.moduleSlots + " modules.", TextBlockDisplayStyle.WrappedText);
                    DrawRecipeModules(gui, modules.beacon, ref effects);
                }

                if (recipe != null) {
                    float craftingSpeed = (recipe.entity?.craftingSpeed ?? 1f) * effects.speedMod;
                    gui.BuildText("Current effects:", Font.subheader);
                    gui.BuildText("Productivity bonus: " + DataUtils.FormatAmount(effects.productivity, UnitOfMeasure.Percent));
                    gui.BuildText("Speed bonus: " + DataUtils.FormatAmount(effects.speedMod - 1, UnitOfMeasure.Percent) + " (Crafting speed: " + DataUtils.FormatAmount(craftingSpeed, UnitOfMeasure.None) + ")");
                    string energyUsageLine = "Energy usage: " + DataUtils.FormatAmount(effects.energyUsageMod, UnitOfMeasure.Percent);
                    if (recipe.entity != null) {
                        float power = effects.energyUsageMod * recipe.entity.power / recipe.entity.energy.effectivity;
                        if (!recipe.recipe.flags.HasFlagAny(RecipeFlags.UsesFluidTemperature | RecipeFlags.ScaleProductionWithPower) && recipe.entity != null) {
                            energyUsageLine += " (" + DataUtils.FormatAmount(power, UnitOfMeasure.Megawatt) + " per building)";
                        }

                        gui.BuildText(energyUsageLine);

                        float pps = craftingSpeed * (1f + MathF.Max(0f, effects.productivity)) / recipe.recipe.time;
                        gui.BuildText("Overall crafting speed (including productivity): " + DataUtils.FormatAmount(pps, UnitOfMeasure.PerSecond));
                        gui.BuildText("Energy cost per recipe output: " + DataUtils.FormatAmount(power / pps, UnitOfMeasure.Megajoule));
                    }
                    else {
                        gui.BuildText(energyUsageLine);
                    }
                }
            }

            gui.AllocateSpacing(3f);
            using (gui.EnterRow(allocator: RectAllocator.RightRow)) {
                if (gui.BuildButton("Done")) {
                    Close();
                }

                gui.allocator = RectAllocator.LeftRow;
                if (modules != null && recipe != null && gui.BuildRedButton("Remove module customization")) {
                    recipe.RecordUndo().modules = null;
                    Close();
                }
            }
        }

        private void SelectBeacon(ImGui gui) {
            if (modules!.beacon is null) { // null-forgiving: Both calls are from places where we know modules is not null
                gui.BuildObjectSelectDropDown<EntityBeacon>(Database.allBeacons, DataUtils.DefaultOrdering, sel => {
                    modules.RecordUndo().beacon = sel;
                    contents.Rebuild();
                }, "Select beacon");
            }
            else {
                gui.BuildObjectSelectDropDownWithNone<EntityBeacon>(Database.allBeacons, DataUtils.DefaultOrdering, sel => {
                    modules.RecordUndo().beacon = sel;
                    contents.Rebuild();
                }, "Select beacon");
            }
        }

        private ICollection<Module> GetModules(EntityBeacon? beacon) {
            var modules = (beacon == null && recipe != null) ? recipe.recipe.modules : Database.allModules;
            var filter = ((EntityWithModules?)beacon) ?? recipe?.entity;
            if (filter == null) {
                return modules;
            }

            return modules.Where(x => filter.CanAcceptModule(x.moduleSpecification)).ToArray();
        }

        private void DrawRecipeModules(ImGui gui, EntityBeacon? beacon, ref ModuleEffects effects) {
            int remainingModules = recipe?.entity?.moduleSlots ?? 0;
            using var grid = gui.EnterInlineGrid(3f, 1f);
            var list = beacon != null ? modules!.beaconList : modules!.list;// null-forgiving: Both calls are from places where we know modules is not null
            foreach (RecipeRowCustomModule rowCustomModule in list) {
                grid.Next();
                DisplayAmount amount = rowCustomModule.fixedCount;
                switch (gui.BuildFactorioObjectWithEditableAmount(rowCustomModule.module, amount, ButtonDisplayStyle.ProductionTableUnscaled)) {
                    case GoodsWithAmountEvent.LeftButtonClick:
                        SelectSingleObjectPanel.SelectWithNone(GetModules(beacon), "Select module", sel => {
                            if (sel == null) {
                                _ = modules.RecordUndo();
                                list.Remove(rowCustomModule);
                            }
                            else {
                                rowCustomModule.RecordUndo().module = sel;
                            }

                            gui.Rebuild();
                        }, DataUtils.FavoriteModule);
                        break;

                    case GoodsWithAmountEvent.TextEditing when amount.Value >= 0:
                        rowCustomModule.RecordUndo().fixedCount = (int)amount.Value;
                        break;
                }

                if (beacon == null) {
                    int count = Math.Min(remainingModules, rowCustomModule.fixedCount > 0 ? rowCustomModule.fixedCount : int.MaxValue);
                    if (count > 0) {
                        effects.AddModules(rowCustomModule.module.moduleSpecification, count);
                        remainingModules -= count;
                    }
                }
                else {
                    effects.AddModules(rowCustomModule.module.moduleSpecification, rowCustomModule.fixedCount * beacon.beaconEfficiency);
                }
            }

            grid.Next();
            if (gui.BuildButton(Icon.Plus, SchemeColor.Primary, SchemeColor.PrimaryAlt, size: 2.5f)) {
                gui.BuildObjectSelectDropDown(GetModules(beacon), DataUtils.FavoriteModule, sel => {
                    _ = modules.RecordUndo();
                    list.Add(new RecipeRowCustomModule(modules, sel));
                    gui.Rebuild();
                }, "Select module");
            }
        }

        protected override void ReturnPressed() => Close();
    }
}
