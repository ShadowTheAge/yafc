using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SDL2;
using Yafc.Blueprints;
using Yafc.Model;
using Yafc.Parser;
using Yafc.UI;

namespace Yafc {
    public class ProductionTableView : ProjectPageView<ProductionTable> {
        private readonly FlatHierarchy<RecipeRow, ProductionTable> flatHierarchyBuilder;

        public ProductionTableView() {
            DataGrid<RecipeRow> grid = new DataGrid<RecipeRow>(new RecipePadColumn(this), new RecipeColumn(this), new EntityColumn(this), new IngredientsColumn(this), new ProductsColumn(this), new ModulesColumn(this));
            flatHierarchyBuilder = new FlatHierarchy<RecipeRow, ProductionTable>(grid, BuildSummary, "This is a nested group. You can drag&drop recipes here. Nested groups can have their own linked materials.");
        }

        /// <param name="widthStorage">If not <see langword="null"/>, names an instance property in <see cref="Preferences"/> that will be used to store the width of this column.
        /// If the current value of the property is out of range, the initial width will be <paramref name="initialWidth"/>.</param>
        private abstract class ProductionTableDataColumn(ProductionTableView view, string header, float initialWidth, float minWidth = 0, float maxWidth = 0, bool hasMenu = true, string? widthStorage = null)
            : TextDataColumn<RecipeRow>(header, initialWidth, minWidth, maxWidth, hasMenu, widthStorage) {
            protected readonly ProductionTableView view = view;
        }

        private class RecipePadColumn(ProductionTableView view) : ProductionTableDataColumn(view, "", 3f, hasMenu: false) {
            public override void BuildElement(ImGui gui, RecipeRow row) {
                gui.allocator = RectAllocator.Center;
                gui.spacing = 0f;
                if (row.subgroup != null) {
                    if (gui.BuildButton(row.subgroup.expanded ? Icon.ShevronDown : Icon.ShevronRight)) {
                        if (InputSystem.Instance.control) {
                            toggleAll(!row.subgroup.expanded, view.model);
                        }
                        else {
                            row.subgroup.RecordChange().expanded = !row.subgroup.expanded;
                        }

                        view.flatHierarchyBuilder.SetData(view.model);
                    }
                }


                if (row.warningFlags != 0) {
                    bool isError = row.warningFlags >= WarningFlags.EntityNotSpecified;
                    bool hover;
                    if (isError) {
                        hover = gui.BuildRedButton(Icon.Error, invertedColors: true) == ButtonEvent.MouseOver;
                    }
                    else {
                        using (gui.EnterGroup(ImGuiUtils.DefaultIconPadding)) {
                            gui.BuildIcon(Icon.Help);
                        }

                        hover = gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.Grey) == ButtonEvent.MouseOver;
                    }
                    if (hover) {
                        gui.ShowTooltip(g => {
                            if (isError) {
                                g.boxColor = SchemeColor.Error;
                                g.textColor = SchemeColor.ErrorText;
                            }
                            foreach (var (flag, text) in WarningsMeaning) {
                                if ((row.warningFlags & flag) != 0) {
                                    g.BuildText(text, TextBlockDisplayStyle.WrappedText);
                                }
                            }
                        });
                    }
                }
                else {
                    if (row.tag != 0) {
                        BuildRowMarker(gui, row);
                    }
                }

                static void toggleAll(bool state, ProductionTable table) {
                    foreach (var subgroup in table.recipes.Select(r => r.subgroup).WhereNotNull()) {
                        subgroup.RecordChange().expanded = state;
                        toggleAll(state, subgroup);
                    }
                }
            }

            private void BuildRowMarker(ImGui gui, RecipeRow row) {
                int markerId = row.tag;
                if (markerId < 0 || markerId >= tagIcons.Length) {
                    markerId = 0;
                }

                var (icon, color) = tagIcons[markerId];
                gui.BuildIcon(icon, color: color);
                if (gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.BackgroundAlt)) {
                    gui.ShowDropDown(imGui => view.DrawRecipeTagSelect(imGui, row));
                }
            }
        }

        private class RecipeColumn(ProductionTableView view) : ProductionTableDataColumn(view, "Recipe", 13f, 13f, 30f, widthStorage: nameof(Preferences.recipeColumnWidth)) {
            public override void BuildElement(ImGui gui, RecipeRow recipe) {
                gui.spacing = 0.5f;
                switch (gui.BuildFactorioObjectButton(recipe.recipe, ButtonDisplayStyle.ProductionTableUnscaled)) {
                    case Click.Left:
                        gui.ShowDropDown(delegate (ImGui imgui) {
                            view.DrawRecipeTagSelect(imgui, recipe);

                            if (recipe.subgroup == null && imgui.BuildButton("Create nested table") && imgui.CloseDropdown()) {
                                recipe.RecordUndo().subgroup = new ProductionTable(recipe);
                            }

                            if (recipe.subgroup != null && imgui.BuildButton("Add nested desired product") && imgui.CloseDropdown()) {
                                view.AddDesiredProductAtLevel(recipe.subgroup);
                            }

                            if (recipe.subgroup != null) {
                                BuildRecipeButton(imgui, recipe.subgroup);
                            }

                            if (recipe.subgroup != null && imgui.BuildButton("Unpack nested table").WithTooltip(imgui, recipe.subgroup.expanded ? "Shortcut: right-click" : "Shortcut: Expand, then right-click") && imgui.CloseDropdown()) {
                                unpackNestedTable();
                            }

                            if (recipe.subgroup != null && imgui.BuildButton("ShoppingList") && imgui.CloseDropdown()) {
                                view.BuildShoppingList(recipe);
                            }

                            if (imgui.BuildCheckBox("Enabled", recipe.enabled, out bool newEnabled)) {
                                recipe.RecordUndo().enabled = newEnabled;
                            }

                            BuildFavorites(imgui, recipe.recipe, "Add recipe to favorites");

                            if (recipe.subgroup != null && imgui.BuildRedButton("Delete nested table").WithTooltip(imgui, recipe.subgroup.expanded ? "Shortcut: Collapse, then right-click" : "Shortcut: right-click") && imgui.CloseDropdown()) {
                                _ = recipe.owner.RecordUndo().recipes.Remove(recipe);
                            }

                            if (recipe.subgroup == null && imgui.BuildRedButton("Delete recipe").WithTooltip(imgui, "Shortcut: right-click") && imgui.CloseDropdown()) {
                                _ = recipe.owner.RecordUndo().recipes.Remove(recipe);
                            }
                        });
                        break;
                    case Click.Right when recipe.subgroup?.expanded ?? false: // With expanded subgroup
                        unpackNestedTable();
                        break;
                    case Click.Right: // With collapsed or no subgroup
                        _ = recipe.owner.RecordUndo().recipes.Remove(recipe);
                        break;
                }

                if (!recipe.enabled) {
                    gui.textColor = SchemeColor.BackgroundTextFaint;
                }
                else if (view.flatHierarchyBuilder.nextRowIsHighlighted) {
                    gui.textColor = view.flatHierarchyBuilder.nextRowTextColor;
                }
                else {
                    gui.textColor = recipe.hierarchyEnabled ? SchemeColor.BackgroundText : SchemeColor.BackgroundTextFaint;
                }

                gui.BuildText(recipe.recipe.locName, TextBlockDisplayStyle.WrappedText);

                void unpackNestedTable() {
                    var evacuate = recipe.subgroup.recipes;
                    _ = recipe.subgroup.RecordUndo();
                    recipe.RecordUndo().subgroup = null;
                    int index = recipe.owner.recipes.IndexOf(recipe);
                    foreach (var evacRecipe in evacuate) {
                        evacRecipe.SetOwner(recipe.owner);
                    }

                    recipe.owner.RecordUndo().recipes.InsertRange(index + 1, evacuate);
                }
            }

            private void RemoveZeroRecipes(ProductionTable productionTable) {
                _ = productionTable.RecordUndo().recipes.RemoveAll(x => x.subgroup == null && x.recipesPerSecond == 0);
                foreach (var recipe in productionTable.recipes) {
                    if (recipe.subgroup != null) {
                        RemoveZeroRecipes(recipe.subgroup);
                    }
                }
            }

            public override void BuildMenu(ImGui gui) {
                BuildRecipeButton(gui, view.model);

                gui.BuildText("Export inputs and outputs to blueprint with constant combinators:", TextBlockDisplayStyle.WrappedText);
                using (gui.EnterRow()) {
                    gui.BuildText("Amount per:");
                    if (gui.BuildLink("second") && gui.CloseDropdown()) {
                        ExportIo(1f);
                    }

                    if (gui.BuildLink("minute") && gui.CloseDropdown()) {
                        ExportIo(60f);
                    }

                    if (gui.BuildLink("hour") && gui.CloseDropdown()) {
                        ExportIo(3600f);
                    }
                }

                if (gui.BuildButton("Remove all zero-building recipes") && gui.CloseDropdown()) {
                    RemoveZeroRecipes(view.model);
                }

                if (gui.BuildRedButton("Clear recipes") && gui.CloseDropdown()) {
                    view.model.RecordUndo().recipes.Clear();
                }

                if (InputSystem.Instance.control && gui.BuildButton("Add ALL recipes") && gui.CloseDropdown()) {
                    foreach (var recipe in Database.recipes.all) {
                        if (!recipe.IsAccessible()) {
                            continue;
                        }

                        foreach (var ingredient in recipe.ingredients) {
                            if (ingredient.goods.production.Length == 0) {
                                // 'goto' is a readable way to break out of a nested loop.
                                // See https://stackoverflow.com/questions/324831/breaking-out-of-a-nested-loop
                                goto goodsHaveNoProduction;
                            }
                        }
                        foreach (var product in recipe.products) {
                            view.CreateLink(view.model, product.goods);
                        }

                        view.model.AddRecipe(recipe, DefaultVariantOrdering);
goodsHaveNoProduction:;
                    }
                }
            }

            /// <summary>
            /// Build the "Add raw recipe" button and handle its clicks.
            /// </summary>
            /// <param name="table">The table that will receive the new recipes or technologies, if any are selected</param>
            private static void BuildRecipeButton(ImGui gui, ProductionTable table) {
                if (gui.BuildButton("Add raw recipe").WithTooltip(gui, "Ctrl-click to add a technology instead") && gui.CloseDropdown()) {
                    if (InputSystem.Instance.control) {
                        SelectMultiObjectPanel.Select(Database.technologies.all, "Select technology", r => table.AddRecipe(r, DefaultVariantOrdering), checkMark: r => table.recipes.Any(rr => rr.recipe == r));
                    }
                    else {
                        SelectMultiObjectPanel.Select(Database.recipes.all, "Select raw recipe", r => table.AddRecipe(r, DefaultVariantOrdering), checkMark: r => table.recipes.Any(rr => rr.recipe == r));
                    }
                }
            }

            private void ExportIo(float multiplier) {
                List<(Goods, int)> goods = [];
                foreach (var link in view.model.links) {
                    int rounded = MathUtils.Round(link.amount * multiplier);
                    if (rounded == 0) {
                        continue;
                    }

                    goods.Add((link.goods, rounded));
                }

                foreach (var flow in view.model.flow) {
                    int rounded = MathUtils.Round(flow.amount * multiplier);
                    if (rounded == 0) {
                        continue;
                    }

                    goods.Add((flow.goods, rounded));
                }

                _ = BlueprintUtilities.ExportConstantCombinators(view.projectPage!.name, goods); // null-forgiving: An active view always has an active page.
            }
        }

        private class EntityColumn(ProductionTableView view) : ProductionTableDataColumn(view, "Entity", 8f) {
            public override void BuildElement(ImGui gui, RecipeRow recipe) {
                if (recipe.isOverviewMode) {
                    return;
                }

                Click click;
                using (var group = gui.EnterGroup(default, RectAllocator.Stretch, spacing: 0f)) {
                    group.SetWidth(3f);
                    if (recipe.fixedBuildings > 0 && !recipe.fixedFuel && recipe.fixedIngredient == null && recipe.fixedProduct == null) {
                        DisplayAmount amount = recipe.fixedBuildings;
                        GoodsWithAmountEvent evt = gui.BuildFactorioObjectWithEditableAmount(recipe.entity, amount, ButtonDisplayStyle.ProductionTableUnscaled);
                        if (evt == GoodsWithAmountEvent.TextEditing && amount.Value >= 0) {
                            recipe.RecordUndo().fixedBuildings = amount.Value;
                        }

                        click = (Click)evt;
                    }
                    else {
                        click = gui.BuildFactorioObjectWithAmount(recipe.entity, recipe.buildingCount, ButtonDisplayStyle.ProductionTableUnscaled);
                    }

                    if (recipe.builtBuildings != null) {
                        DisplayAmount amount = recipe.builtBuildings.Value;
                        if (gui.BuildFloatInput(amount, TextBoxDisplayStyle.FactorioObjectInput with { ColorGroup = SchemeColorGroup.Grey }) && amount.Value >= 0) {
                            recipe.RecordUndo().builtBuildings = (int)amount.Value;
                        }
                    }
                }

                if (recipe.recipe.crafters.Length == 0) {
                    // ignore all clicks
                }
                else if (click == Click.Left) {
                    ShowEntityDropdown(gui, recipe);
                }
                else if (click == Click.Right) {
                    EntityCrafter favoriteCrafter = recipe.recipe.crafters.AutoSelect(DataUtils.FavoriteCrafter)!; // null-forgiving: We know recipe.recipe.crafters is not empty, so AutoSelect can't return null.
                    if (favoriteCrafter != null && recipe.entity != favoriteCrafter) {
                        _ = recipe.RecordUndo();
                        recipe.entity = favoriteCrafter;
                        if (!recipe.entity.energy.fuels.Contains(recipe.fuel)) {
                            recipe.fuel = recipe.entity.energy.fuels.AutoSelect(DataUtils.FavoriteFuel);
                        }
                    }
                    else if (recipe.fixedBuildings > 0) {
                        recipe.RecordUndo().fixedBuildings = 0;
                    }
                    else if (recipe.builtBuildings != null) {
                        recipe.RecordUndo().builtBuildings = null;
                    }
                }

                gui.AllocateSpacing(0.5f);
                if (recipe.fuel != Database.voidEnergy || recipe.entity == null || recipe.entity.energy.type != EntityEnergyType.Void) {
                    var (fuel, fuelAmount, fuelLink, _) = recipe.FuelInformation;
                    view.BuildGoodsIcon(gui, fuel, fuelLink, fuelAmount, ProductDropdownType.Fuel, recipe, recipe.linkRoot, HintLocations.OnProducingRecipes);
                }
                else {
                    if (recipe.recipe == Database.electricityGeneration && recipe.entity.factorioType == "solar-panel") {
                        BuildSolarPanelAccumulatorView(gui, recipe);
                    }
                }
            }

            private static void BuildSolarPanelAccumulatorView(ImGui gui, RecipeRow recipe) {
                var accumulator = recipe.GetVariant(Database.allAccumulators);
                float requiredMj = recipe.entity?.craftingSpeed * recipe.buildingCount * (70 / 0.7f) ?? 0; // 70 seconds of charge time to last through the night
                float requiredAccumulators = requiredMj / accumulator.accumulatorCapacity;
                if (gui.BuildFactorioObjectWithAmount(accumulator, requiredAccumulators, ButtonDisplayStyle.ProductionTableUnscaled) == Click.Left) {
                    ShowAccumulatorDropdown(gui, recipe, accumulator);
                }
            }

            private static void ShowAccumulatorDropdown(ImGui gui, RecipeRow recipe, Entity currentAccumulator) => gui.ShowDropDown(imGui => {
                imGui.BuildInlineObjectListAndButton<EntityAccumulator>(Database.allAccumulators, DataUtils.DefaultOrdering,
                    newAccumulator => recipe.RecordUndo().ChangeVariant(currentAccumulator, newAccumulator), "Select accumulator",
                    extra: x => DataUtils.FormatAmount(x.accumulatorCapacity, UnitOfMeasure.Megajoule));
            });

            private static void ShowEntityDropdown(ImGui imgui, RecipeRow recipe) => imgui.ShowDropDown(gui => {
                EntityCrafter? favoriteCrafter = recipe.recipe.crafters.AutoSelect(DataUtils.FavoriteCrafter);
                if (favoriteCrafter == recipe.entity) { favoriteCrafter = null; }
                bool willResetFixed = favoriteCrafter == null, willResetBuilt = willResetFixed && recipe.fixedBuildings == 0;

                gui.BuildInlineObjectListAndButton(recipe.recipe.crafters, DataUtils.FavoriteCrafter, sel => {
                    if (recipe.entity == sel) {
                        return;
                    }

                    _ = recipe.RecordUndo();
                    recipe.entity = sel;
                    if (!sel.energy.fuels.Contains(recipe.fuel)) {
                        recipe.fuel = recipe.entity.energy.fuels.AutoSelect(DataUtils.FavoriteFuel);
                    }
                }, "Select crafting entity", extra: x => DataUtils.FormatAmount(x.craftingSpeed, UnitOfMeasure.Percent));

                gui.AllocateSpacing(0.5f);

                if (recipe.fixedBuildings > 0f && (recipe.fixedFuel || recipe.fixedIngredient != null || recipe.fixedProduct != null)) {
                    ButtonEvent evt = gui.BuildButton("Clear fixed recipe multiplier");
                    if (willResetFixed) {
                        _ = evt.WithTooltip(gui, "Shortcut: right-click");
                    }
                    if (evt && gui.CloseDropdown()) {
                        recipe.RecordUndo().fixedBuildings = 0f;
                    }
                }

                using (gui.EnterRowWithHelpIcon("Tell YAFC how many buildings it must use when solving this page.\nUse this to ask questions like 'What does it take to handle the output of ten miners?'")) {
                    gui.allocator = RectAllocator.RemainingRow;
                    if (recipe.fixedBuildings > 0f && !recipe.fixedFuel && recipe.fixedIngredient == null && recipe.fixedProduct == null) {
                        ButtonEvent evt = gui.BuildButton("Clear fixed building count");
                        if (willResetFixed) {
                            _ = evt.WithTooltip(gui, "Shortcut: right-click");
                        }
                        if (evt && gui.CloseDropdown()) {
                            recipe.RecordUndo().fixedBuildings = 0f;
                        }
                    }
                    else if (gui.BuildButton("Set fixed building count") && gui.CloseDropdown()) {
                        recipe.RecordUndo().fixedBuildings = recipe.buildingCount <= 0f ? 1f : recipe.buildingCount;
                        recipe.fixedFuel = false;
                        recipe.fixedIngredient = null;
                        recipe.fixedProduct = null;
                    }
                }

                using (gui.EnterRowWithHelpIcon("Tell YAFC how many of these buildings you have in your factory.\nYAFC will warn you if you need to build more buildings.")) {
                    gui.allocator = RectAllocator.RemainingRow;
                    if (recipe.builtBuildings != null) {
                        ButtonEvent evt = gui.BuildButton("Clear built building count");
                        if (willResetBuilt) {
                            _ = evt.WithTooltip(gui, "Shortcut: right-click");
                        }
                        if (evt && gui.CloseDropdown()) {
                            recipe.RecordUndo().builtBuildings = null;
                        }
                    }
                    else if (gui.BuildButton("Set built building count") && gui.CloseDropdown()) {
                        recipe.RecordUndo().builtBuildings = Math.Max(0, Convert.ToInt32(Math.Ceiling(recipe.buildingCount)));
                    }
                }

                if (recipe.entity != null) {
                    using (gui.EnterRowWithHelpIcon("Generate a blueprint for one of these buildings, with the recipe and internal modules set.")) {
                        gui.allocator = RectAllocator.RemainingRow;
                        if (gui.BuildButton("Create single building blueprint") && gui.CloseDropdown()) {
                            BlueprintEntity entity = new BlueprintEntity { index = 1, name = recipe.entity.name };
                            if (recipe.recipe is not Mechanics) {
                                entity.recipe = recipe.recipe.name;
                            }

                            var modules = recipe.usedModules.modules;
                            if (modules != null) {
                                entity.items = [];
                                foreach (var (module, count, beacon) in modules) {
                                    if (!beacon) {
                                        entity.items[module.name] = count;
                                    }
                                }
                            }
                            BlueprintString bp = new BlueprintString(recipe.recipe.locName) { blueprint = { entities = { entity } } };
                            _ = SDL.SDL_SetClipboardText(bp.ToBpString());
                        }
                    }

                    if (recipe.recipe.crafters.Length > 1) {
                        BuildFavorites(gui, recipe.entity, "Add building to favorites");
                    }
                }
            });

            public override void BuildMenu(ImGui gui) {
                if (gui.BuildButton("Mass set assembler") && gui.CloseDropdown()) {
                    SelectSingleObjectPanel.Select(Database.allCrafters, "Set assembler for all recipes", set => {
                        DataUtils.FavoriteCrafter.AddToFavorite(set, 10);
                        foreach (var recipe in view.GetRecipesRecursive()) {
                            if (recipe.recipe.crafters.Contains(set)) {
                                _ = recipe.RecordUndo();
                                recipe.entity = set;
                                if (!set.energy.fuels.Contains(recipe.fuel)) {
                                    recipe.fuel = recipe.entity.energy.fuels.AutoSelect(DataUtils.FavoriteFuel);
                                }
                            }
                        }
                    }, DataUtils.FavoriteCrafter);
                }

                if (gui.BuildButton("Mass set fuel") && gui.CloseDropdown()) {
                    SelectSingleObjectPanel.Select(Database.goods.all.Where(x => x.fuelValue > 0), "Set fuel for all recipes", set => {
                        DataUtils.FavoriteFuel.AddToFavorite(set, 10);
                        foreach (var recipe in view.GetRecipesRecursive()) {
                            if (recipe.entity != null && recipe.entity.energy.fuels.Contains(set)) {
                                recipe.RecordUndo().fuel = set;
                            }
                        }
                    }, DataUtils.FavoriteFuel);
                }

                if (gui.BuildButton("Shopping list") && gui.CloseDropdown()) {
                    view.BuildShoppingList(null);
                }
            }
        }

        private class IngredientsColumn(ProductionTableView view) : ProductionTableDataColumn(view, "Ingredients", 32f, 16f, 100f, hasMenu: false, nameof(Preferences.ingredientsColumWidth)) {
            public override void BuildElement(ImGui gui, RecipeRow recipe) {
                var grid = gui.EnterInlineGrid(3f, 1f);
                if (recipe.isOverviewMode) {
                    view.BuildTableIngredients(gui, recipe.subgroup, recipe.owner, ref grid);
                }
                else {
                    foreach (var (goods, amount, link, variants) in recipe.Ingredients) {
                        grid.Next();
                        view.BuildGoodsIcon(gui, goods, link, amount, ProductDropdownType.Ingredient, recipe, recipe.linkRoot, HintLocations.OnProducingRecipes, variants);
                    }
                }
                grid.Dispose();
            }
        }

        private class ProductsColumn(ProductionTableView view) : ProductionTableDataColumn(view, "Products", 12f, 10f, 70f, hasMenu: false, nameof(Preferences.productsColumWidth)) {
            public override void BuildElement(ImGui gui, RecipeRow recipe) {
                var grid = gui.EnterInlineGrid(3f, 1f);
                if (recipe.isOverviewMode) {
                    view.BuildTableProducts(gui, recipe.subgroup, recipe.owner, ref grid, false);
                }
                else {
                    foreach (var (goods, amount, link) in recipe.Products) {
                        grid.Next();
                        view.BuildGoodsIcon(gui, goods, link, amount, ProductDropdownType.Product, recipe, recipe.linkRoot, HintLocations.OnConsumingRecipes);
                    }
                }
                grid.Dispose();
            }
        }

        private class ModulesColumn : ProductionTableDataColumn {
            private readonly VirtualScrollList<ProjectModuleTemplate> moduleTemplateList;
            private RecipeRow editingRecipeModules = null!; // null-forgiving: This is set as soon as we open a module dropdown.

            public ModulesColumn(ProductionTableView view) : base(view, "Modules", 10f, 7f, 16f, widthStorage: nameof(Preferences.modulesColumnWidth))
                => moduleTemplateList = new VirtualScrollList<ProjectModuleTemplate>(15f, new Vector2(20f, 2.5f), ModuleTemplateDrawer, collapsible: true);

            private void ModuleTemplateDrawer(ImGui gui, ProjectModuleTemplate element, int index) {
                var evt = gui.BuildContextMenuButton(element.name, icon: element.icon?.icon ?? default, disabled: !element.template.IsCompatibleWith(editingRecipeModules));
                if (evt == ButtonEvent.Click && gui.CloseDropdown()) {
                    var copied = JsonUtils.Copy(element.template, editingRecipeModules, null);
                    editingRecipeModules.RecordUndo().modules = copied;
                    view.Rebuild();
                }
                else if (evt == ButtonEvent.MouseOver) {
                    ShowModuleTemplateTooltip(gui, element.template);
                }
            }

            public override void BuildElement(ImGui gui, RecipeRow recipe) {
                if (recipe.isOverviewMode) {
                    return;
                }

                if (recipe.entity == null || recipe.entity.allowedEffects == AllowedEffects.None || recipe.recipe.modules.Length == 0) {
                    return;
                }

                using var grid = gui.EnterInlineGrid(3f);
                if (recipe.usedModules.modules == null || recipe.usedModules.modules.Length == 0) {
                    drawItem(gui, null, 0);
                }
                else {
                    bool wasBeacon = false;
                    foreach (var (module, count, beacon) in recipe.usedModules.modules) {
                        if (beacon && !wasBeacon) {
                            wasBeacon = true;
                            if (recipe.usedModules.beacon != null) {
                                drawItem(gui, recipe.usedModules.beacon, recipe.usedModules.beaconCount);
                            }
                        }
                        drawItem(gui, module, count);
                    }
                }

                void drawItem(ImGui gui, FactorioObject? item, int count) {
                    grid.Next();
                    switch (gui.BuildFactorioObjectWithAmount(item, count, ButtonDisplayStyle.ProductionTableUnscaled)) {
                        case Click.Left:
                            ShowModuleDropDown(gui, recipe);
                            break;
                        case Click.Right when recipe.modules != null:
                            recipe.RecordUndo().RemoveFixedModules();
                            break;
                    }
                }
            }

            private void ShowModuleTemplateTooltip(ImGui gui, ModuleTemplate template) => gui.ShowTooltip(imGui => {
                if (!template.IsCompatibleWith(editingRecipeModules)) {
                    imGui.BuildText("This module template seems incompatible with the recipe or the building", TextBlockDisplayStyle.WrappedText);
                }

                using var grid = imGui.EnterInlineGrid(3f, 1f);
                foreach (var module in template.list) {
                    grid.Next();
                    _ = imGui.BuildFactorioObjectWithAmount(module.module, module.fixedCount, ButtonDisplayStyle.ProductionTableUnscaled);
                }

                if (template.beacon != null) {
                    grid.Next();
                    _ = imGui.BuildFactorioObjectWithAmount(template.beacon, template.CalcBeaconCount(), ButtonDisplayStyle.ProductionTableUnscaled);
                    foreach (var module in template.beaconList) {
                        grid.Next();
                        _ = imGui.BuildFactorioObjectWithAmount(module.module, module.fixedCount, ButtonDisplayStyle.ProductionTableUnscaled);
                    }
                }
            });

            private void ShowModuleDropDown(ImGui gui, RecipeRow recipe) {
                var modules = recipe.recipe.modules.Where(x => recipe.entity?.CanAcceptModule(x.moduleSpecification) ?? false).ToArray();
                editingRecipeModules = recipe;
                moduleTemplateList.data = [.. Project.current.sharedModuleTemplates
                    .Where(x => x.filterEntities.Count == 0 || x.filterEntities.Contains(recipe.entity!)) // null-forgiving: non-nullable collections are happy to report they don't contain null values.
                    .OrderByDescending(x => x.template.IsCompatibleWith(recipe))];

                gui.ShowDropDown(dropGui => {
                    if (recipe.modules != null && dropGui.BuildButton("Use default modules").WithTooltip(dropGui, "Shortcut: right-click") && dropGui.CloseDropdown()) {
                        recipe.RemoveFixedModules();
                    }

                    if (recipe.entity?.moduleSlots > 0) {
                        dropGui.BuildInlineObjectListAndButton(modules, DataUtils.FavoriteModule, recipe.SetFixedModule, "Select fixed module");
                    }

                    if (moduleTemplateList.data.Count > 0) {
                        dropGui.BuildText("Use module template:", Font.subheader);
                        moduleTemplateList.Build(dropGui);
                    }
                    if (dropGui.BuildButton("Configure module templates") && dropGui.CloseDropdown()) {
                        ModuleTemplateConfiguration.Show();
                    }

                    if (dropGui.BuildButton("Customize modules") && dropGui.CloseDropdown()) {
                        ModuleCustomizationScreen.Show(recipe);
                    }
                });
            }

            public override void BuildMenu(ImGui gui) {
                var model = view.model;

                gui.BuildText("Auto modules", Font.subheader);
                ModuleFillerParametersScreen.BuildSimple(gui, model.modules!); // null-forgiving: owner is a ProjectPage, so modules is not null.
                if (gui.BuildButton("Module settings") && gui.CloseDropdown()) {
                    ModuleFillerParametersScreen.Show(model.modules!);
                }
            }
        }

        public static void BuildFavorites(ImGui imgui, FactorioObject? obj, string prompt) {
            if (obj == null) {
                return;
            }

            bool isFavorite = Project.current.preferences.favorites.Contains(obj);
            using (imgui.EnterRow(0.5f, RectAllocator.LeftRow)) {
                imgui.BuildIcon(isFavorite ? Icon.StarFull : Icon.StarEmpty);
                imgui.RemainingRow().BuildText(isFavorite ? "Favorite" : prompt);
            }
            if (imgui.OnClick(imgui.lastRect)) {
                Project.current.preferences.ToggleFavorite(obj);
            }
        }

        public override float CalculateWidth() => flatHierarchyBuilder.width;

        public static void CreateProductionSheet() => ProjectPageSettingsPanel.Show(null, (name, icon) => MainScreen.Instance.AddProjectPage(name, icon, typeof(ProductionTable), true, true));

        private static readonly IComparer<Goods> DefaultVariantOrdering = new DataUtils.FactorioObjectComparer<Goods>((x, y) => (y.ApproximateFlow() / MathF.Abs(y.Cost())).CompareTo(x.ApproximateFlow() / MathF.Abs(x.Cost())));

        private enum ProductDropdownType {
            DesiredProduct,
            Fuel,
            Ingredient,
            Product,
            DesiredIngredient,
        }

        private void CreateLink(ProductionTable table, Goods goods) {
            if (table.linkMap.ContainsKey(goods)) {
                return;
            }

            ProductionLink link = new ProductionLink(table, goods);
            Rebuild();
            table.RecordUndo().links.Add(link);
        }

        private void DestroyLink(ProductionLink link) {
            if (link.owner.links.Contains(link)) {
                _ = link.owner.RecordUndo().links.Remove(link);
                Rebuild();
            }
        }

        private void CreateNewProductionTable(Goods goods, float amount) {
            var page = MainScreen.Instance.AddProjectPage(goods.locName, goods, typeof(ProductionTable), true, false);
            ProductionTable content = (ProductionTable)page.content;
            ProductionLink link = new ProductionLink(content, goods) { amount = amount > 0 ? amount : 1 };
            content.links.Add(link);
            content.RebuildLinkMap();
        }

        private void OpenProductDropdown(ImGui targetGui, Rect rect, Goods goods, float amount, ProductionLink? link, ProductDropdownType type, RecipeRow? recipe, ProductionTable context, Goods[]? variants = null) {
            if (InputSystem.Instance.shift) {
                Project.current.preferences.SetSourceResource(goods, !goods.IsSourceResource());
                targetGui.Rebuild();
                return;
            }

            var comparer = DataUtils.GetRecipeComparerFor(goods);
            HashSet<RecipeOrTechnology> allRecipes = new HashSet<RecipeOrTechnology>(context.recipes.Select(x => x.recipe));
            bool recipeExists(RecipeOrTechnology rec) {
                return allRecipes.Contains(rec);
            }

            Goods? selectedFuel = null;
            Goods? spentFuel = null;
            async void addRecipe(RecipeOrTechnology rec) {
                if (variants == null) {
                    CreateLink(context, goods);
                }
                else {
                    foreach (var variant in variants) {
                        if (rec.GetProductionPerRecipe(variant) > 0f) {
                            CreateLink(context, variant);
                            if (variant != goods) {
                                recipe!.RecordUndo().ChangeVariant(goods, variant); // null-forgiving: If variants is not null, neither is recipe: Only the call from BuildGoodsIcon sets variants, and the only call to BuildGoodsIcon that sets variants also sets recipe.
                            }

                            break;
                        }
                    }
                }
                if (!allRecipes.Contains(rec) || (await MessageBox.Show("Recipe already exists", $"Add a second copy of {rec.locName}?", "Add a copy", "Cancel")).choice) {
                    context.AddRecipe(rec, DefaultVariantOrdering, selectedFuel, spentFuel);
                }
            }

            if (InputSystem.Instance.control) {
                bool isInput = type <= ProductDropdownType.Ingredient;
                var recipeList = isInput ? goods.production : goods.usages;
                if (recipeList.SelectSingle(out _) is Recipe selected) {
                    addRecipe(selected);
                    return;
                }
            }

            Recipe[] allProduction = variants == null ? goods.production : variants.SelectMany(x => x.production).Distinct().ToArray();
            Recipe[] fuelUseList = goods.fuelFor.OfType<EntityCrafter>()
                .SelectMany(e => e.recipes).OfType<Recipe>()
                .Distinct().OrderBy(e => e, DataUtils.DefaultRecipeOrdering).ToArray();
            Recipe[] spentFuelRecipes = goods.miscSources.OfType<Item>()
                .SelectMany(e => e.fuelFor.OfType<EntityCrafter>())
                .SelectMany(e => e.recipes).OfType<Recipe>()
                .Distinct().OrderBy(e => e, DataUtils.DefaultRecipeOrdering).ToArray();

            targetGui.ShowDropDown(rect, dropDownContent, new Padding(1f), 25f);

            void dropDownContent(ImGui gui) {
                if (type == ProductDropdownType.Fuel && recipe?.entity != null) {
                    EntityEnergy? energy = recipe.entity.energy;

                    if (energy == null || energy.fuels.Length == 0) {
                        gui.BuildText("This entity has no known fuels");
                    }
                    else if (energy.fuels.Length > 1 || energy.fuels[0] != recipe.fuel) {
                        Func<Goods, string> fuelDisplayFunc = energy.type == EntityEnergyType.FluidHeat
                             ? g => DataUtils.FormatAmount(g.fluid?.heatValue ?? 0, UnitOfMeasure.Megajoule)
                             : g => DataUtils.FormatAmount(g.fuelValue, UnitOfMeasure.Megajoule);

                        BuildFavorites(gui, recipe.fuel, "Add fuel to favorites");
                        gui.BuildInlineObjectListAndButton(energy.fuels, DataUtils.FavoriteFuel,
                            fuel => recipe.RecordUndo().fuel = fuel, "Select fuel", extra: fuelDisplayFunc);
                    }
                }

                if (variants != null) {
                    gui.BuildText("Accepted fluid variants:");
                    using (var grid = gui.EnterInlineGrid(3f)) {
                        foreach (var variant in variants) {
                            grid.Next();
                            if (gui.BuildFactorioObjectButton(variant, ButtonDisplayStyle.ProductionTableScaled(variant == goods ? SchemeColor.Primary : SchemeColor.None), tooltipOptions: HintLocations.OnProducingRecipes) == Click.Left &&
                                variant != goods) {
                                recipe!.RecordUndo().ChangeVariant(goods, variant); // null-forgiving: If variants is not null, neither is recipe: Only the call from BuildGoodsIcon sets variants, and the only call to BuildGoodsIcon that sets variants also sets recipe.
                                if (recipe!.fixedIngredient == goods) {
                                    recipe.fixedIngredient = variant;
                                }
                                _ = gui.CloseDropdown();
                            }
                        }
                    }

                    gui.allocator = RectAllocator.Stretch;
                }

                if (link != null) {
                    if (!link.flags.HasFlags(ProductionLink.Flags.HasProduction)) {
                        gui.BuildText("This link has no production (Link ignored)", TextBlockDisplayStyle.ErrorText);
                    }

                    if (!link.flags.HasFlags(ProductionLink.Flags.HasConsumption)) {
                        gui.BuildText("This link has no consumption (Link ignored)", TextBlockDisplayStyle.ErrorText);
                    }

                    if (link.flags.HasFlags(ProductionLink.Flags.ChildNotMatched)) {
                        gui.BuildText("Nested table link have unmatched production/consumption. These unmatched products are not captured by this link.", TextBlockDisplayStyle.ErrorText);
                    }

                    if (!link.flags.HasFlags(ProductionLink.Flags.HasProductionAndConsumption) && link.owner.owner is RecipeRow recipeRow && recipeRow.FindLink(link.goods, out _)) {
                        gui.BuildText("Nested tables have their own set of links that DON'T connect to parent links. To connect this product to the outside, remove this link", TextBlockDisplayStyle.ErrorText);
                    }

                    if (link.flags.HasFlags(ProductionLink.Flags.LinkRecursiveNotMatched)) {
                        if (link.notMatchedFlow <= 0f) {
                            gui.BuildText("YAFC was unable to satisfy this link (Negative feedback loop). This doesn't mean that this link is the problem, but it is part of the loop.", TextBlockDisplayStyle.ErrorText);
                        }
                        else {
                            gui.BuildText("YAFC was unable to satisfy this link (Overproduction). You can allow overproduction for this link to solve the error.", TextBlockDisplayStyle.ErrorText);
                        }
                    }
                }

                #region Recipe selection
                int numberOfShownRecipes = 0;
                if (goods.name == SpecialNames.ResearchUnit) {
                    if (gui.BuildButton("Add technology") && gui.CloseDropdown()) {
                        SelectMultiObjectPanel.Select(Database.technologies.all, "Select technology", r => context.AddRecipe(r, DefaultVariantOrdering), checkMark: r => context.recipes.Any(rr => rr.recipe == r));
                    }
                }
                else if (type <= ProductDropdownType.Ingredient && allProduction.Length > 0) {
                    gui.BuildInlineObjectListAndButton(allProduction, comparer, addRecipe, "Add production recipe", 6, true, recipeExists);
                    numberOfShownRecipes += allProduction.Length;
                    if (link == null) {
                        Rect iconRect = new Rect(gui.lastRect.Right - 2f, gui.lastRect.Top, 2f, 2f);
                        gui.DrawIcon(iconRect.Expand(-0.2f), Icon.OpenNew, gui.textColor);
                        var evt = gui.BuildButton(iconRect, SchemeColor.None, SchemeColor.Grey);
                        if (evt == ButtonEvent.Click && gui.CloseDropdown()) {
                            CreateNewProductionTable(goods, amount);
                        }
                        else if (evt == ButtonEvent.MouseOver) {
                            gui.ShowTooltip(iconRect, "Create new production table for " + goods.locName);
                        }
                    }
                }

                if (type <= ProductDropdownType.Ingredient && spentFuelRecipes.Length > 0) {
                    gui.BuildInlineObjectListAndButton(
                        spentFuelRecipes,
                        DataUtils.AlreadySortedRecipe,
                        (x) => { spentFuel = goods; addRecipe(x); },
                        "Produce it as a spent fuel",
                        3,
                        true,
                        recipeExists);
                    numberOfShownRecipes += spentFuelRecipes.Length;
                }

                if (type >= ProductDropdownType.Product && goods.usages.Length > 0) {
                    gui.BuildInlineObjectListAndButton(
                        goods.usages,
                        DataUtils.DefaultRecipeOrdering,
                        addRecipe,
                        "Add consumption recipe",
                        6,
                        true,
                        recipeExists);
                    numberOfShownRecipes += goods.usages.Length;
                }

                if (type >= ProductDropdownType.Product && fuelUseList.Length > 0) {
                    gui.BuildInlineObjectListAndButton(
                        fuelUseList,
                        DataUtils.AlreadySortedRecipe,
                        (x) => { selectedFuel = goods; addRecipe(x); },
                        "Add fuel usage",
                        6,
                        true,
                        recipeExists);
                    numberOfShownRecipes += fuelUseList.Length;
                }

                if (type >= ProductDropdownType.Product && Database.allSciencePacks.Contains(goods)
                    && gui.BuildButton("Add consumption technology") && gui.CloseDropdown()) {
                    // Select from the technologies that consume this science pack.
                    SelectMultiObjectPanel.Select(Database.technologies.all.Where(t => t.ingredients.Select(i => i.goods).Contains(goods)), "Add technology", addRecipe, checkMark: recipeExists);
                }

                if (type >= ProductDropdownType.Product && allProduction.Length > 0) {
                    gui.BuildInlineObjectListAndButton(allProduction, comparer, addRecipe, "Add production recipe", 1, true, recipeExists);
                    numberOfShownRecipes += allProduction.Length;
                }

                if (numberOfShownRecipes > 1) {
                    gui.BuildText("Hint: ctrl+click to add multiple", TextBlockDisplayStyle.HintText);
                }
                #endregion

                #region Link management
                if (link != null && gui.BuildCheckBox("Allow overproduction", link.algorithm == LinkAlgorithm.AllowOverProduction, out bool newValue)) {
                    link.RecordUndo().algorithm = newValue ? LinkAlgorithm.AllowOverProduction : LinkAlgorithm.Match;
                }

                if (link != null && gui.BuildButton("View link summary") && gui.CloseDropdown()) {
                    ProductionLinkSummaryScreen.Show(link);
                }

                if (link != null && link.owner == context) {
                    if (link.amount != 0) {
                        gui.BuildText(goods.locName + " is a desired product and cannot be unlinked.", TextBlockDisplayStyle.WrappedText);
                    }
                    else {
                        gui.BuildText(goods.locName + " production is currently linked. This means that YAFC will try to match production with consumption.", TextBlockDisplayStyle.WrappedText);
                    }

                    if (type is ProductDropdownType.DesiredIngredient or ProductDropdownType.DesiredProduct) {
                        if (gui.BuildButton("Remove desired product") && gui.CloseDropdown()) {
                            link.RecordUndo().amount = 0;
                        }

                        if (gui.BuildButton("Remove and unlink").WithTooltip(gui, "Shortcut: right-click") && gui.CloseDropdown()) {
                            DestroyLink(link);
                        }
                    }
                    else if (link.amount == 0 && gui.BuildButton("Unlink").WithTooltip(gui, "Shortcut: right-click") && gui.CloseDropdown()) {
                        DestroyLink(link);
                    }
                }
                else if (goods != null) {
                    if (link != null) {
                        gui.BuildText(goods.locName + " production is currently linked, but the link is outside this nested table. Nested tables can have its own separate set of links", TextBlockDisplayStyle.WrappedText);
                    }
                    else {
                        gui.BuildText(goods.locName + " production is currently NOT linked. This means that YAFC will make no attempt to match production with consumption.", TextBlockDisplayStyle.WrappedText);
                    }

                    if (gui.BuildButton("Create link").WithTooltip(gui, "Shortcut: right-click") && gui.CloseDropdown()) {
                        CreateLink(context, goods);
                    }
                }
                #endregion

                #region Fixed production/consumption
                if (goods != null && recipe != null) {
                    if (recipe.fixedBuildings == 0
                        || (type == ProductDropdownType.Fuel && !recipe.fixedFuel)
                        || (type == ProductDropdownType.Ingredient && recipe.fixedIngredient != goods)
                        || (type == ProductDropdownType.Product && recipe.fixedProduct != goods)) {
                        string? prompt = type switch {
                            ProductDropdownType.Fuel => "Set fixed fuel consumption",
                            ProductDropdownType.Ingredient => "Set fixed ingredient consumption",
                            ProductDropdownType.Product => "Set fixed production amount",
                            _ => null
                        };
                        if (prompt != null) {
                            ButtonEvent evt;
                            if (recipe.fixedBuildings == 0) {
                                evt = gui.BuildButton(prompt);
                            }
                            else {
                                using (gui.EnterRowWithHelpIcon("This will replace the other fixed amount in this row.")) {
                                    gui.allocator = RectAllocator.RemainingRow;
                                    evt = gui.BuildButton(prompt);
                                }
                            }
                            if (evt && gui.CloseDropdown()) {
                                recipe.RecordUndo().fixedBuildings = recipe.buildingCount <= 0 ? 1 : recipe.buildingCount;
                                switch (type) {
                                    case ProductDropdownType.Fuel:
                                        recipe.fixedFuel = true;
                                        recipe.fixedIngredient = null;
                                        recipe.fixedProduct = null;
                                        break;
                                    case ProductDropdownType.Ingredient:
                                        recipe.fixedFuel = false;
                                        recipe.fixedIngredient = goods;
                                        recipe.fixedProduct = null;
                                        break;
                                    case ProductDropdownType.Product:
                                        recipe.fixedFuel = false;
                                        recipe.fixedIngredient = null;
                                        recipe.fixedProduct = goods;
                                        break;
                                    default:
                                        break;
                                }
                                targetGui.Rebuild();
                            }
                        }
                    }

                    if (recipe.fixedBuildings != 0
                        && ((type == ProductDropdownType.Fuel && recipe.fixedFuel)
                        || (type == ProductDropdownType.Ingredient && recipe.fixedIngredient == goods)
                        || (type == ProductDropdownType.Product && recipe.fixedProduct == goods))) {
                        string? prompt = type switch {
                            ProductDropdownType.Fuel => "Clear fixed fuel consumption",
                            ProductDropdownType.Ingredient => "Clear fixed ingredient consumption",
                            ProductDropdownType.Product => "Clear fixed production amount",
                            _ => null
                        };
                        if (prompt != null && gui.BuildButton(prompt) && gui.CloseDropdown()) {
                            recipe.RecordUndo().fixedBuildings = 0;
                        }
                        targetGui.Rebuild();
                    }
                }
                #endregion

                if (goods is Item) {
                    BuildBeltInserterInfo(gui, amount, recipe?.buildingCount ?? 0);
                }
            }
        }

        public override void SetSearchQuery(SearchQuery query) {
            _ = model.Search(query);
            bodyContent.Rebuild();
        }

        private void DrawDesiredProduct(ImGui gui, ProductionLink element) {
            gui.allocator = RectAllocator.Stretch;
            gui.spacing = 0f;
            SchemeColor iconColor = SchemeColor.Primary;

            if (element.flags.HasFlags(ProductionLink.Flags.LinkNotMatched)) {
                if (element.linkFlow > element.amount && CheckPossibleOverproducing(element)) {
                    // Actual overproduction occurred for this product
                    iconColor = SchemeColor.Magenta;
                }
                else {
                    // There is not enough production (most likely none at all, otherwise the analyzer will have a deadlock)
                    iconColor = SchemeColor.Error;
                }
            }

            ObjectTooltipOptions tooltipOptions = element.amount < 0 ? HintLocations.OnConsumingRecipes : HintLocations.OnProducingRecipes;
            DisplayAmount amount = new(element.amount, element.goods.flowUnitOfMeasure);
            switch (gui.BuildFactorioObjectWithEditableAmount(element.goods, amount, ButtonDisplayStyle.ProductionTableScaled(iconColor), tooltipOptions: tooltipOptions)) {
                case GoodsWithAmountEvent.LeftButtonClick:
                    OpenProductDropdown(gui, gui.lastRect, element.goods, element.amount, element, element.amount < 0 ? ProductDropdownType.DesiredIngredient : ProductDropdownType.DesiredProduct, null, element.owner);
                    break;
                case GoodsWithAmountEvent.RightButtonClick:
                    DestroyLink(element);
                    break;
                case GoodsWithAmountEvent.TextEditing when amount.Value != 0:
                    element.RecordUndo().amount = amount.Value;
                    break;
            }
        }

        public override void Rebuild(bool visualOnly = false) {
            flatHierarchyBuilder.SetData(model);
            base.Rebuild(visualOnly);
        }

        private void BuildGoodsIcon(ImGui gui, Goods? goods, ProductionLink? link, float amount, ProductDropdownType dropdownType, RecipeRow? recipe, ProductionTable context, ObjectTooltipOptions tooltipOptions, Goods[]? variants = null) {
            SchemeColor iconColor;
            if (link != null) {
                // The icon is part of a production link
                if ((link.flags & (ProductionLink.Flags.HasProductionAndConsumption | ProductionLink.Flags.LinkRecursiveNotMatched | ProductionLink.Flags.ChildNotMatched)) != ProductionLink.Flags.HasProductionAndConsumption) {
                    // The link has production and consumption sides, but either the production and consumption is not matched, or 'child was not matched'
                    iconColor = SchemeColor.Error;
                }
                else if (dropdownType >= ProductDropdownType.Product && CheckPossibleOverproducing(link)) {
                    // Actual overproduction occurred in the recipe
                    iconColor = SchemeColor.Magenta;
                }
                else if (link.owner != context) {
                    // It is a foreign link (e.g. not part of the sub group)
                    iconColor = SchemeColor.Secondary;
                }
                else {
                    // Regular (nothing going on) linked icon
                    iconColor = SchemeColor.Primary;
                }
            }
            else {
                // The icon is not part of a production link
                iconColor = goods.IsSourceResource() ? SchemeColor.Green : SchemeColor.None;
            }

            // TODO: See https://github.com/have-fun-was-taken/yafc-ce/issues/91
            //       and https://github.com/have-fun-was-taken/yafc-ce/pull/86#discussion_r1550377021
            SchemeColor textColor = flatHierarchyBuilder.nextRowTextColor;
            if (!flatHierarchyBuilder.nextRowIsHighlighted) {
                textColor = SchemeColor.None;
            }
            else if (recipe is { enabled: false }) {
                textColor = SchemeColor.BackgroundTextFaint;
            }

            GoodsWithAmountEvent evt;
            DisplayAmount displayAmount = new(amount, goods?.flowUnitOfMeasure ?? UnitOfMeasure.None);

            if (recipe != null && recipe.fixedBuildings > 0
                && ((dropdownType == ProductDropdownType.Fuel && recipe.fixedFuel)
                || (dropdownType == ProductDropdownType.Ingredient && recipe.fixedIngredient == goods)
                || (dropdownType == ProductDropdownType.Product && recipe.fixedProduct == goods))) {
                evt = gui.BuildFactorioObjectWithEditableAmount(goods, displayAmount, ButtonDisplayStyle.ProductionTableScaled(iconColor), tooltipOptions: tooltipOptions);
            }
            else {
                evt = (GoodsWithAmountEvent)gui.BuildFactorioObjectWithAmount(goods, displayAmount, ButtonDisplayStyle.ProductionTableScaled(iconColor), TextBlockDisplayStyle.Centered with { Color = textColor }, tooltipOptions: tooltipOptions);
            }

            switch (evt) {
                case GoodsWithAmountEvent.LeftButtonClick when goods is not null:
                    OpenProductDropdown(gui, gui.lastRect, goods, amount, link, dropdownType, recipe, context, variants);
                    break;
                case GoodsWithAmountEvent.RightButtonClick when goods is not null && (link is null || link.owner != context):
                    CreateLink(context, goods);
                    break;
                case GoodsWithAmountEvent.RightButtonClick when link?.amount == 0 && link.owner == context:
                    DestroyLink(link);
                    break;
                case GoodsWithAmountEvent.TextEditing when displayAmount.Value >= 0:
                    // The amount is always stored in fixedBuildings. Scale it to match the requested change to this item.
                    recipe!.RecordUndo().fixedBuildings *= displayAmount.Value / amount;
                    break;
            }
        }

        /// <summary>
        /// Checks some criteria that are necessary but not sufficient to consider something overproduced.
        /// </summary>
        private static bool CheckPossibleOverproducing(ProductionLink link) => link.algorithm == LinkAlgorithm.AllowOverProduction && link.flags.HasFlag(ProductionLink.Flags.LinkNotMatched);

        /// <param name="isForSummary">If <see langword="true"/>, this call is for a summary box, at the top of a root-level or nested table.
        /// If <see langword="false"/>, this call is for collapsed recipe row.</param>
        /// <param name="initializeDrawArea">If not <see langword="null"/>, this will be called before drawing the first element. This method may choose not to draw
        /// some or all of a table's extra products, and this lets the caller suppress the surrounding UI elements if no product end up being drawn.</param>
        private void BuildTableProducts(ImGui gui, ProductionTable table, ProductionTable context, ref ImGuiUtils.InlineGridBuilder grid, bool isForSummary, Action<ImGui>? initializeDrawArea = null) {
            var flow = table.flow;
            int firstProduct = Array.BinarySearch(flow, new ProductionTableFlow(Database.voidEnergy, 1e-9f, null), model);
            if (firstProduct < 0) {
                firstProduct = ~firstProduct;
            }

            for (int i = firstProduct; i < flow.Length; i++) {
                float amt = flow[i].amount;
                if (isForSummary) {
                    amt -= flow[i].link?.amount ?? 0;
                }
                if (amt <= 0f) {
                    continue;
                }

                initializeDrawArea?.Invoke(gui);
                initializeDrawArea = null;

                grid.Next();
                BuildGoodsIcon(gui, flow[i].goods, flow[i].link, amt, ProductDropdownType.Product, null, context, HintLocations.OnConsumingRecipes);
            }
        }

        private void FillRecipeList(ProductionTable table, List<RecipeRow> list) {
            foreach (var recipe in table.recipes) {
                list.Add(recipe);
                if (recipe.subgroup != null) {
                    FillRecipeList(recipe.subgroup, list);
                }
            }
        }

        private void FillLinkList(ProductionTable table, List<ProductionLink> list) {
            list.AddRange(table.links);
            foreach (var recipe in table.recipes) {
                if (recipe.subgroup != null) {
                    FillLinkList(recipe.subgroup, list);
                }
            }
        }

        private List<RecipeRow> GetRecipesRecursive() {
            List<RecipeRow> list = [];
            FillRecipeList(model, list);
            return list;
        }

        private List<RecipeRow> GetRecipesRecursive(RecipeRow recipeRoot) {
            List<RecipeRow> list = [recipeRoot];
            if (recipeRoot.subgroup != null) {
                FillRecipeList(recipeRoot.subgroup, list);
            }

            return list;
        }

        private void BuildShoppingList(RecipeRow? recipeRoot) {
            Dictionary<FactorioObject, int> shopList = [];
            var recipes = recipeRoot == null ? GetRecipesRecursive() : GetRecipesRecursive(recipeRoot);
            foreach (var recipe in recipes) {
                if (recipe.entity != null) {
                    FactorioObject shopItem = recipe.entity.itemsToPlace?.FirstOrDefault() ?? (FactorioObject)recipe.entity;
                    _ = shopList.TryGetValue(shopItem, out int prev);
                    int count = MathUtils.Ceil(recipe.builtBuildings ?? recipe.buildingCount);
                    shopList[shopItem] = prev + count;
                    if (recipe.usedModules.modules != null) {
                        foreach (var module in recipe.usedModules.modules) {
                            if (!module.beacon) {
                                _ = shopList.TryGetValue(module.module, out prev);
                                shopList[module.module] = prev + (count * module.count);
                            }
                        }
                    }
                }
            }
            ShoppingListScreen.Show(shopList);
        }

        private void BuildBeltInserterInfo(ImGui gui, float amount, float buildingCount) {
            var prefs = Project.current.preferences;
            var belt = prefs.defaultBelt;
            var inserter = prefs.defaultInserter;
            if (belt == null || inserter == null) {
                return;
            }

            float beltCount = amount / belt.beltItemsPerSecond;
            float buildingsPerHalfBelt = belt.beltItemsPerSecond * buildingCount / (amount * 2f);
            bool click = false;

            using (gui.EnterRow()) {
                click |= gui.BuildFactorioObjectButton(belt, ButtonDisplayStyle.Default) == Click.Left;
                gui.BuildText(DataUtils.FormatAmount(beltCount, UnitOfMeasure.None));
                if (buildingsPerHalfBelt > 0f) {
                    gui.BuildText("(Buildings per half belt: " + DataUtils.FormatAmount(buildingsPerHalfBelt, UnitOfMeasure.None) + ")");
                }
            }

            using (gui.EnterRow()) {
                int capacity = prefs.inserterCapacity;
                float inserterBase = inserter.inserterSwingTime * amount / capacity;
                click |= gui.BuildFactorioObjectButton(inserter, ButtonDisplayStyle.Default) == Click.Left;
                string text = DataUtils.FormatAmount(inserterBase, UnitOfMeasure.None);
                if (buildingCount > 1) {
                    text += " (" + DataUtils.FormatAmount(inserterBase / buildingCount, UnitOfMeasure.None) + "/building)";
                }

                gui.BuildText(text);
                if (capacity > 1) {
                    float withBeltSwingTime = inserter.inserterSwingTime + (2f * (capacity - 1.5f) / belt.beltItemsPerSecond);
                    float inserterToBelt = amount * withBeltSwingTime / capacity;
                    click |= gui.BuildFactorioObjectButton(belt, ButtonDisplayStyle.Default) == Click.Left;
                    gui.AllocateSpacing(-1.5f);
                    click |= gui.BuildFactorioObjectButton(inserter, ButtonDisplayStyle.Default) == Click.Left;
                    text = DataUtils.FormatAmount(inserterToBelt, UnitOfMeasure.None, "~");
                    if (buildingCount > 1) {
                        text += " (" + DataUtils.FormatAmount(inserterToBelt / buildingCount, UnitOfMeasure.None) + "/b)";
                    }

                    gui.BuildText(text);
                }
            }

            if (click && gui.CloseDropdown()) {
                PreferencesScreen.Show();
            }
        }

        private void BuildTableIngredients(ImGui gui, ProductionTable table, ProductionTable context, ref ImGuiUtils.InlineGridBuilder grid) {
            foreach (var flow in table.flow) {
                if (flow.amount >= 0f) {
                    break;
                }

                grid.Next();
                BuildGoodsIcon(gui, flow.goods, flow.link, -flow.amount, ProductDropdownType.Ingredient, null, context, HintLocations.OnProducingRecipes);
            }
        }

        private void DrawRecipeTagSelect(ImGui gui, RecipeRow recipe) {
            using (gui.EnterRow()) {
                for (int i = 0; i < tagIcons.Length; i++) {
                    var (icon, color) = tagIcons[i];
                    bool selected = i == recipe.tag;
                    gui.BuildIcon(icon, color: selected ? SchemeColor.Background : color);
                    if (selected) {
                        gui.DrawRectangle(gui.lastRect, color);
                    }
                    else {
                        var evt = gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.BackgroundAlt, SchemeColor.BackgroundAlt);
                        if (evt) {
                            recipe.RecordUndo(true).tag = i;
                        }
                    }
                }
            }
        }

        protected override void BuildHeader(ImGui gui) {
            base.BuildHeader(gui);
            flatHierarchyBuilder.BuildHeader(gui);
        }

        protected override void BuildPageTooltip(ImGui gui, ProductionTable contents) {
            foreach (var link in contents.links) {
                if (link.amount != 0f) {
                    using (gui.EnterRow()) {
                        gui.BuildFactorioObjectIcon(link.goods);
                        using (gui.EnterGroup(default, RectAllocator.LeftAlign, spacing: 0)) {
                            gui.BuildText(link.goods.locName);
                            gui.BuildText(DataUtils.FormatAmount(link.amount, link.goods.flowUnitOfMeasure));
                        }
                    }
                }
            }

            foreach (var row in contents.recipes) {
                if (row.fixedBuildings != 0 && row.entity != null) {
                    using (gui.EnterRow()) {
                        gui.BuildFactorioObjectIcon(row.recipe);
                        using (gui.EnterGroup(default, RectAllocator.LeftAlign, spacing: 0)) {
                            gui.BuildText(row.recipe.locName);
                            gui.BuildText(row.entity.locName + ": " + DataUtils.FormatAmount(row.fixedBuildings, UnitOfMeasure.None));
                        }
                    }
                }

                if (row.subgroup != null) {
                    BuildPageTooltip(gui, row.subgroup);
                }
            }
        }

        private static readonly Dictionary<WarningFlags, string> WarningsMeaning = new Dictionary<WarningFlags, string>
        {
            {WarningFlags.DeadlockCandidate, "Contains recursive links that cannot be matched. No solution exists."},
            {WarningFlags.OverproductionRequired, "This model cannot be solved exactly, it requires some overproduction. You can allow overproduction for any link. This recipe contains one of the possible candidates."},
            {WarningFlags.EntityNotSpecified, "Crafter not specified. Solution is inaccurate." },
            {WarningFlags.FuelNotSpecified, "Fuel not specified. Solution is inaccurate." },
            {WarningFlags.FuelWithTemperatureNotLinked, "This recipe uses fuel with temperature. Should link with producing entity to determine temperature."},
            {WarningFlags.FuelTemperatureExceedsMaximum, "Fluid temperature is higher than generator maximum. Some energy is wasted."},
            {WarningFlags.FuelDoesNotProvideEnergy, "This fuel cannot provide any energy to this building. The building won't work."},
            {WarningFlags.FuelUsageInputLimited, "This building has max fuel consumption. The rate at which it works is limited by it."},
            {WarningFlags.TemperatureForIngredientNotMatch, "This recipe does care about ingredient temperature, and the temperature range does not match"},
            {WarningFlags.ReactorsNeighborsFromPrefs, "Assumes reactor formation from preferences"},
            {WarningFlags.AssumesNauvisSolarRatio, "Energy production values assumes Nauvis solar ration (70% power output). Don't forget accumulators."},
            {WarningFlags.RecipeTickLimit, "Production is limited to 60 recipes per second (1/tick). This interacts weirdly with productivity bonus - actual productivity may be imprecise and may depend on your setup - test your setup before committing to it."},
            {WarningFlags.ExceedsBuiltCount, "This recipe requires more buildings than are currently built."}
        };

        private static readonly (Icon icon, SchemeColor color)[] tagIcons = [
            (Icon.Empty, SchemeColor.BackgroundTextFaint),
            (Icon.Check, SchemeColor.Green),
            (Icon.Warning, SchemeColor.Secondary),
            (Icon.Error, SchemeColor.Error),
            (Icon.Edit, SchemeColor.Primary),
            (Icon.Help, SchemeColor.BackgroundText),
            (Icon.Time, SchemeColor.BackgroundText),
            (Icon.DarkMode, SchemeColor.BackgroundText),
            (Icon.Settings, SchemeColor.BackgroundText),
        ];



        protected override void BuildContent(ImGui gui) {
            if (model == null) {
                return;
            }

            BuildSummary(gui, model);
            gui.AllocateSpacing();
            flatHierarchyBuilder.Build(gui);
            gui.SetMinWidth(flatHierarchyBuilder.width);
        }

        private void AddDesiredProductAtLevel(ProductionTable table) => SelectMultiObjectPanel.Select(
            Database.goods.all.Except(table.linkMap.Where(p => p.Value.amount != 0).Select(p => p.Key)), "Add desired product", product => {
                if (table.linkMap.TryGetValue(product, out var existing)) {
                    if (existing.amount != 0) {
                        return;
                    }

                    existing.RecordUndo().amount = 1f;
                }
                else {
                    table.RecordUndo().links.Add(new ProductionLink(table, product) { amount = 1f });
                }
            });

        private void BuildSummary(ImGui gui, ProductionTable table) {
            bool isRoot = table == model;
            if (!isRoot && !table.containsDesiredProducts) {
                return;
            }

            int elementsPerRow = MathUtils.Floor((flatHierarchyBuilder.width - 2f) / 4f);
            gui.spacing = 1f;
            Padding pad = new Padding(1f, 0.2f);
            using (gui.EnterGroup(pad)) {
                gui.BuildText("Desired products and amounts (Use negative for input goal):");
                using var grid = gui.EnterInlineGrid(3f, 1f, elementsPerRow);
                foreach (var link in table.links.ToList()) {
                    if (link.amount != 0f) {
                        grid.Next();
                        DrawDesiredProduct(gui, link);
                    }
                }

                grid.Next();
                if (gui.BuildButton(Icon.Plus, SchemeColor.Primary, SchemeColor.PrimaryAlt, size: 2.5f)) {
                    AddDesiredProductAtLevel(table);
                }
            }
            if (gui.isBuilding) {
                gui.DrawRectangle(gui.lastRect, SchemeColor.Background, RectangleBorder.Thin);
            }

            if (table.flow.Length > 0 && table.flow[0].amount < 0) {
                using (gui.EnterGroup(pad)) {
                    gui.BuildText(isRoot ? "Summary ingredients:" : "Import ingredients:");
                    var grid = gui.EnterInlineGrid(3f, 1f, elementsPerRow);
                    BuildTableIngredients(gui, table, table, ref grid);
                    grid.Dispose();
                }
                if (gui.isBuilding) {
                    gui.DrawRectangle(gui.lastRect, SchemeColor.Background, RectangleBorder.Thin);
                }
            }

            if (table.flow.Length > 0 && table.flow[^1].amount > 0) {
                ImGui.Context? context = null;
                ImGuiUtils.InlineGridBuilder grid = default;
                void initializeGrid(ImGui gui) {
                    context = gui.EnterGroup(pad);
                    gui.BuildText(isRoot ? "Extra products:" : "Export products:");
                    grid = gui.EnterInlineGrid(3f, 1f, elementsPerRow);
                }

                BuildTableProducts(gui, table, table, ref grid, true, initializeGrid);

                if (context != null) {
                    grid.Dispose();
                    context.Value.Dispose();

                    if (gui.isBuilding) {
                        gui.DrawRectangle(gui.lastRect, SchemeColor.Background, RectangleBorder.Thin);
                    }
                }
            }
        }
    }
}
