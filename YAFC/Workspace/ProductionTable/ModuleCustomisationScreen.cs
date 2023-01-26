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

        private RecipeRow recipe;
        private ProjectModuleTemplate template;
        private ModuleTemplate modules;

        public static void Show(RecipeRow recipe)
        {
            Instance.template = null;
            Instance.recipe = recipe;
            Instance.modules = recipe.modules;
            MainScreen.Instance.ShowPseudoScreen(Instance);
        }
        
        public static void Show(ProjectModuleTemplate template)
        {
            Instance.recipe = null;
            Instance.template = template;
            Instance.modules = template.template;
            MainScreen.Instance.ShowPseudoScreen(Instance);
        }
        
        public override void Build(ImGui gui)
        {
            BuildHeader(gui, "Module customisation");
            if (template != null)
            {
                using (gui.EnterRow())
                {
                    if (gui.BuildFactorioObjectButton(template.icon) == Click.Left)
                        SelectSingleObjectPanel.Select(Database.objects.all, "Select icon", x =>
                        {
                            template.RecordUndo().icon = x;
                            Rebuild();
                        });
                    if (gui.BuildTextInput(template.name, out var newName, "Enter name", delayed: true) && newName != "")
                        template.RecordUndo().name = newName;
                }
                gui.BuildText("Filter by crafting buildings (Optional):");
                using (var grid = gui.EnterInlineGrid(2f, 1f))
                {
                    for (var i = 0; i < template.filterEntities.Count; i++)
                    {
                        var entity = template.filterEntities[i];
                        grid.Next();
                        gui.BuildFactorioObjectIcon(entity, MilestoneDisplay.Contained);
                        if (gui.BuildMouseOverIcon(Icon.Close, SchemeColor.Error))
                            template.RecordUndo().filterEntities.RemoveAt(i);
                    }
                    grid.Next();
                    if (gui.BuildButton(Icon.Plus, SchemeColor.Primary, SchemeColor.PrimalyAlt, size:1.5f))
                    {
                        SelectSingleObjectPanel.Select(Database.allCrafters.Where(x => x.allowedEffects != AllowedEffects.None && !template.filterEntities.Contains(x)), "Add module template filter", sel =>
                        {
                            template.RecordUndo().filterEntities.Add(sel);
                            gui.Rebuild();
                        });
                    }
                }
            }
            if (modules == null)
            {
                if (gui.BuildButton("Enable custom modules"))
                {
                    recipe.RecordUndo().modules = new ModuleTemplate(recipe);
                    modules = recipe.modules;
                }
            }
            else
            {
                var effects = new ModuleEffects();
                if (recipe == null || recipe.entity?.moduleSlots > 0)
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
                if (modules.beacon == null)
                {
                    gui.BuildText("Use default parameters");
                    if (gui.BuildButton("Override beacons as well"))
                        SelectBeacon(gui);
                    var defaultFiller = recipe?.GetModuleFiller();
                    if (defaultFiller?.GetBeaconsForCrafter(recipe.entity) is var beaconsToUse && beaconsToUse.beaconModule != null)
                        effects.AddModules(beaconsToUse.beaconModule.module, beaconsToUse.beacon.beaconEfficiency * beaconsToUse.beacon.moduleSlots * beaconsToUse.beaconCount);
                }
                else
                {
                    if (gui.BuildFactorioObjectButtonWithText(modules.beacon) == Click.Left)
                        SelectBeacon(gui);
                    gui.BuildText("Input the amount of modules, not the amount of beacons. Single beacon can hold "+modules.beacon.moduleSlots+" modules.", wrap:true);
                    DrawRecipeModules(gui, modules.beacon, ref effects);
                }

                if (recipe != null)
                {
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
            }
            
            gui.AllocateSpacing(3f);
            using (gui.EnterRow(allocator:RectAllocator.RightRow))
            {
                if (gui.BuildButton("Done"))
                    Close();
                gui.allocator = RectAllocator.LeftRow;
                if (modules != null && recipe != null && gui.BuildRedButton("Remove module customisation"))
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
                if (modules != null)
                    modules.RecordUndo().beacon = sel;
                contents.Rebuild();
            }, "Select beacon", allowNone:modules.beacon != null);
        }

        private ICollection<Item> GetModules(EntityBeacon beacon)
        {
            var modules = (beacon == null && recipe != null) ? recipe.recipe.modules : Database.allModules;
            var filter = ((EntityWithModules)beacon) ?? recipe?.entity;
            if (filter == null)
                return modules;
            return modules.Where(x => filter.CanAcceptModule(x.module)).ToArray();
        }

        private void DrawRecipeModules(ImGui gui, EntityBeacon beacon, ref ModuleEffects effects)
        {
            var remainingModules = recipe?.entity?.moduleSlots ?? 0;
            using (var grid = gui.EnterInlineGrid(3f, 1f))
            {
                var list = beacon != null ? modules.beaconList : modules.list;
                foreach (var module in list)
                {
                    grid.Next();
                    var evt = gui.BuildFactorioObjectWithEditableAmount(module.module, module.fixedCount, UnitOfMeasure.None, out var newAmount);
                    if (evt == GoodsWithAmountEvent.LeftButtonClick)
                    {
                        SelectSingleObjectPanel.Select(GetModules(beacon), "Select module", sel =>
                        {
                            if (sel == null)
                                modules.RecordUndo().list.Remove(module);
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
                        modules.RecordUndo();
                        list.Add(new RecipeRowCustomModule(modules, sel));
                        gui.Rebuild();
                    }, "Select module");
                }
            }
        }
    }
}