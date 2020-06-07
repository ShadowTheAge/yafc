using System.Collections.Generic;
using System.Linq;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class ModuleCustomisation : PseudoScreen
    {
        private static readonly ModuleCustomisation Instance = new ModuleCustomisation();

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
                    recipe.RecordUndo().modules = new CustomModules(recipe);
            }
            else
            {
                gui.BuildText("Internal modules:", Font.subheader);
                gui.BuildText("Leave zero amount to fill the remainings slots");
                DrawRecipeModules(gui, null);
                gui.BuildText("Beacon modules:", Font.subheader);
                if (recipe.modules.beacon == null)
                {
                    gui.BuildText("Use default parameters");
                    if (gui.BuildButton("Override beacons as well"))
                        gui.ShowDropDown(SelectBeacon);
                }
                else
                {
                    if (gui.BuildFactorioObjectButtonWithText(recipe.modules.beacon))
                        gui.ShowDropDown(SelectBeacon);
                    gui.BuildText("Input the amount of modules, not the amount of beacons. Single beacon can hold "+recipe.modules.beacon.moduleSlots+" modules.", wrap:true);
                    DrawRecipeModules(gui, recipe.modules.beacon);
                }
            }
            
            gui.AllocateSpacing(3f);
            if (gui.BuildButton("Done"))
                Close();
        }

        private void SelectBeacon(ImGui gui, ref bool closed)
        {
            closed = gui.BuildInlineObejctListAndButton<Entity>(Database.allBeacons, DataUtils.DefaultOrdering, sel =>
            {
                if (recipe.modules != null)
                    recipe.modules.RecordUndo().beacon = sel;
                contents.Rebuild();
            }, "Select beacon", allowNone:recipe.modules.beacon != null);
        }

        private IEnumerable<Item> GetModules(Entity beacon)
        {
            IEnumerable<Item> modules = beacon == null ? recipe.recipe.modules : Database.allModules;
            var filter = beacon ?? recipe.entity;
            return modules.Where(x => filter.CanAcceptModule(x.module));
        }

        private void DrawRecipeModules(ImGui gui, Entity beacon)
        {
            using (var grid = gui.EnterInlineGrid(3f, 1f))
            {
                foreach (var module in recipe.modules.list)
                {
                    if (module.inBeacon != (beacon != null))
                        continue;
                    grid.Next();
                    var evt = gui.BuildFactorioGoodsWithEditableAmount(module.module, module.fixedCount, UnitOfMeasure.None, out var newAmount);
                    if (evt == GoodsWithAmountEvent.ButtonClick)
                    {
                        SelectObjectPanel.Select(GetModules(beacon), "Select module", sel =>
                        {
                            if (sel == null)
                                recipe.modules.RecordUndo().list.Remove(module);
                            else module.module = sel;
                            gui.Rebuild();
                        }, DataUtils.FavouriteModule, true);
                    } 
                    else if (evt == GoodsWithAmountEvent.TextEditing)
                    {
                        var amountInt = MathUtils.Floor(newAmount);
                        if (amountInt < 0)
                            amountInt = 0;
                        module.fixedCount = amountInt;
                    }
                }
                
                grid.Next();
                if (gui.BuildButton(Icon.Plus, SchemeColor.Primary, SchemeColor.PrimalyAlt, size:2.5f))
                {
                    SelectObjectPanel.Select(GetModules(beacon), "Select module", sel =>
                    {
                        recipe.modules.RecordUndo().list.Add(new RecipeRowCustomModule(recipe.modules, sel) {inBeacon = beacon != null});
                        gui.Rebuild();
                    }, DataUtils.FavouriteModule, false);
                }
            }
        }
    }
}