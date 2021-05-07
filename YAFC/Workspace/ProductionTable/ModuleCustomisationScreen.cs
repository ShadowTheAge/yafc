using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class ModuleCustomisationScreen : PseudoScreen
    {
        private static readonly ModuleCustomisationScreen Instance = new ModuleCustomisationScreen();
        public static MemoryStream copiedModuleSettings;

        private RecipeRow recipe;

        public static void Show(RecipeRow recipe)
        {
            Instance.recipe = recipe;
            MainScreen.Instance.ShowPseudoScreen(Instance);
        }
        
        public override void Build(ImGui gui)
        {
            BuildHeader(gui, "Module customisation");
            if (recipe.modules == null)
            {
                if (gui.BuildButton("Enable custom modules"))
                    recipe.RecordUndo().modules = new ModuleTemplate(recipe);
            }
            else
            {
                var effects = new ModuleEffects();
                if (recipe.entity?.moduleSlots > 0)
                {
                    gui.BuildText("Internal modules:", Font.subheader);
                    gui.BuildText("Leave zero amount to fill the remainings slots");
                    DrawRecipeModules(gui, null, ref effects);
                }
                else
                {
                    gui.BuildText("This building doesn't have module slots, but can be affected by beacons");
                }
                gui.BuildText("Beacon modules:", Font.subheader);
                if (recipe.modules.beacon == null)
                {
                    gui.BuildText("Use default parameters");
                    if (gui.BuildButton("Override beacons as well"))
                        SelectBeacon(gui);
                    var defaultFiller = recipe.GetModuleFiller();
                    if (defaultFiller != null && defaultFiller.beacon != null && defaultFiller.beaconModule != null)
                        effects.AddModules(defaultFiller.beaconModule.module, defaultFiller.beacon.beaconEfficiency * defaultFiller.beacon.moduleSlots * defaultFiller.beaconsPerBuilding);
                }
                else
                {
                    if (gui.BuildFactorioObjectButtonWithText(recipe.modules.beacon))
                        SelectBeacon(gui);
                    gui.BuildText("Input the amount of modules, not the amount of beacons. Single beacon can hold "+recipe.modules.beacon.moduleSlots+" modules.", wrap:true);
                    DrawRecipeModules(gui, recipe.modules.beacon, ref effects);
                }

                var craftingSpeed = (recipe.entity?.craftingSpeed ?? 1f) * effects.speedMod;
                
                gui.BuildText("Current effects:", Font.subheader);
                gui.BuildText("Productivity bonus: "+DataUtils.FormatAmount(effects.productivity, UnitOfMeasure.Percent));
                gui.BuildText("Speed bonus: "+DataUtils.FormatAmount(effects.speedMod-1, UnitOfMeasure.Percent) + " (Crafting speed: "+DataUtils.FormatAmount(craftingSpeed, UnitOfMeasure.None)+")");
                var energyUsageLine = "Energy usage: " + DataUtils.FormatAmount(effects.energyUsageMod, UnitOfMeasure.Percent);
                if (recipe.entity != null)
                {
                    var power = effects.energyUsageMod * recipe.entity.power / recipe.entity.energy.effectivity;
                    if (!recipe.recipe.flags.HasFlagAny(RecipeFlags.UsesFluidTemperature | RecipeFlags.ScaleProductionWithPower) && recipe.entity != null)
                        energyUsageLine += " (" + DataUtils.FormatAmount(power, UnitOfMeasure.Megawatt) + " per building)";
                    gui.BuildText(energyUsageLine);

                    var pps = craftingSpeed * (1f + MathF.Max(0f, effects.productivity)) / recipe.recipe.time;
                    gui.BuildText("Overall crafting speed (including productivity): "+DataUtils.FormatAmount(pps, UnitOfMeasure.PerSecond));
                    gui.BuildText("Energy cost per recipe output: "+DataUtils.FormatAmount(power / pps, UnitOfMeasure.Megajoule));
                } else
                    gui.BuildText(energyUsageLine);
            }
            
            gui.AllocateSpacing(3f);
            using (gui.EnterRow(allocator:RectAllocator.RightRow))
            {
                if (gui.BuildButton("Done"))
                    Close();
                if (recipe.modules != null && gui.BuildButton("Copy settings", SchemeColor.Grey))
                {
                    if (copiedModuleSettings == null)
                        MessageBox.Show("Info", "Use ctrl+click on module slot to paste settings", "Ok");
                    copiedModuleSettings = JsonUtils.SaveToJson(recipe.modules);
                }
                gui.allocator = RectAllocator.LeftRow;
                if (recipe.modules != null && gui.BuildRedButton("Remove module customisation") == ImGuiUtils.Event.Click)
                {
                    recipe.RecordUndo().modules = null;
                    Close();
                }
            }
        }

        private void SelectBeacon(ImGui gui)
        {
            gui.BuildObjectSelectDropDown<EntityBeacon>(Database.allBeacons, DataUtils.DefaultOrdering, sel =>
            {
                if (recipe.modules != null)
                    recipe.modules.RecordUndo().beacon = sel;
                contents.Rebuild();
            }, "Select beacon", allowNone:recipe.modules.beacon != null);
        }

        private IReadOnlyList<Item> GetModules(Entity beacon)
        {
            IEnumerable<Item> modules = beacon == null ? recipe.recipe.modules : Database.allModules;
            var filter = beacon ?? recipe.entity;
            return modules.Where(x => filter.CanAcceptModule(x.module)).ToArray();
        }

        private void DrawRecipeModules(ImGui gui, EntityBeacon beacon, ref ModuleEffects effects)
        {
            var remainingModules = recipe.entity?.moduleSlots ?? 0;
            using (var grid = gui.EnterInlineGrid(3f, 1f))
            {
                var list = beacon != null ? recipe.modules.beaconList : recipe.modules.list;
                foreach (var module in list)
                {
                    grid.Next();
                    var evt = gui.BuildFactorioObjectWithEditableAmount(module.module, module.fixedCount, UnitOfMeasure.None, out var newAmount);
                    if (evt == GoodsWithAmountEvent.ButtonClick)
                    {
                        SelectObjectPanel.Select(GetModules(beacon), "Select module", sel =>
                        {
                            if (sel == null)
                                recipe.modules.RecordUndo().list.Remove(module);
                            else module.RecordUndo().module = sel;
                            gui.Rebuild();
                        }, DataUtils.FavouriteModule, true);
                    } 
                    else if (evt == GoodsWithAmountEvent.TextEditing)
                    {
                        var amountInt = MathUtils.Floor(newAmount);
                        if (amountInt < 0)
                            amountInt = 0;
                        module.RecordUndo().fixedCount = amountInt;
                    }

                    if (beacon == null)
                    {
                        var count = Math.Min(remainingModules, module.fixedCount > 0 ? module.fixedCount : int.MaxValue);
                        if (count > 0)
                        {
                            effects.AddModules(module.module.module, count);
                            remainingModules -= count;
                        }
                    }
                    else
                    {
                        effects.AddModules(module.module.module, module.fixedCount * beacon.beaconEfficiency);
                    }
                }
                
                grid.Next();
                if (gui.BuildButton(Icon.Plus, SchemeColor.Primary, SchemeColor.PrimalyAlt, size:2.5f))
                {
                    gui.BuildObjectSelectDropDown(GetModules(beacon), DataUtils.FavouriteModule, sel =>
                    {
                        recipe.modules.RecordUndo();
                        list.Add(new RecipeRowCustomModule(recipe.modules, sel));
                        gui.Rebuild();
                    }, "Select module");
                }
            }
        }
    }
}