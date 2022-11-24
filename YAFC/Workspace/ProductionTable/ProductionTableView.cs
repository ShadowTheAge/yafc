using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using SDL2;
using YAFC.Blueprints;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class ProductionTableView : ProjectPageView<ProductionTable>
    {
        private readonly FlatHierarchy<RecipeRow, ProductionTable> flatHierarchyBuilder;

        public ProductionTableView()
        {
            var grid = new DataGrid<RecipeRow>(new RecipePadColumn(this), new RecipeColumn(this), new EntityColumn(this), new IngredientsColumn(this), new ProductsColumn(this), new ModulesColumn(this));
            flatHierarchyBuilder = new FlatHierarchy<RecipeRow, ProductionTable>(grid, BuildSummary, "This is a nested group. You can drag&drop recipes here. Nested groups can have its own linked materials");
        }

        private abstract class ProductionTableDataColumn : TextDataColumn<RecipeRow>
        {
            protected readonly ProductionTableView view;

            protected ProductionTableDataColumn(ProductionTableView view, string header, float width, float minWidth = 0, float maxWidth = 0, bool hasMenu = true) : base(header, width, minWidth, maxWidth, hasMenu)
            {
                this.view = view;
            }
        }

        private class RecipePadColumn : ProductionTableDataColumn
        {
            public RecipePadColumn(ProductionTableView view) : base(view, "", 3f, hasMenu:false) {}

            public override void BuildElement(ImGui gui, RecipeRow row)
            {
                gui.allocator = RectAllocator.Center;
                gui.spacing = 0f;
                if (row.subgroup != null)
                {
                    if (gui.BuildButton(row.subgroup.expanded ? Icon.ShevronDown : Icon.ShevronRight))
                    {
                        row.subgroup.RecordUndo(true).expanded = !row.subgroup.expanded;
                        view.flatHierarchyBuilder.SetData(view.model);
                    }
                }
            
            
                if (row.parameters.warningFlags != 0)
                {
                    var isError = row.parameters.warningFlags >= WarningFlags.EntityNotSpecified;
                    bool hover;
                    if (isError)
                        hover = gui.BuildRedButton(Icon.Error) == ButtonEvent.MouseOver;
                    else
                    {
                        using (gui.EnterGroup(ImGuiUtils.DefaultIconPadding))
                            gui.BuildIcon(Icon.Help); 
                        hover = gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.Grey) == ButtonEvent.MouseOver;
                    } 
                    if (hover)
                    {
                        gui.ShowTooltip(g =>
                        {
                            if (isError)
                            {
                                g.boxColor = SchemeColor.Error;
                                g.textColor = SchemeColor.ErrorText;
                            }
                            foreach (var (flag, text) in WarningsMeaning)
                            {
                                if ((row.parameters.warningFlags & flag) != 0)
                                    g.BuildText(text, wrap:true);
                            }
                        });
                    }
                }
                else
                {
                    if (row.tag != 0)
                        BuildRowMarker(gui, row);
                }
            }
            
            private void BuildRowMarker(ImGui gui, RecipeRow row)
            {
                var markerId = row.tag;
                if (markerId < 0 || markerId >= tagIcons.Length)
                    markerId = 0;
                var (icon, color) = tagIcons[markerId];
                gui.BuildIcon(icon, color:color);
                if (gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.BackgroundAlt))
                {
                    gui.ShowDropDown(imGui => view.DrawRecipeTagSelect(imGui, row));
                }
            }
        }

        private class RecipeColumn : ProductionTableDataColumn
        {
            public RecipeColumn(ProductionTableView view) : base(view, "Recipe", 13f, 13f, 30f) {}

            public override void BuildElement(ImGui gui, RecipeRow recipe)
            {
                gui.spacing = 0.5f;
                if (gui.BuildFactorioObjectButton(recipe.recipe, 3f))
                {
                    gui.ShowDropDown(delegate(ImGui imgui)
                    {
                        view.DrawRecipeTagSelect(imgui, recipe);
                        
                        if (recipe.subgroup == null && imgui.BuildButton("Create nested table") && imgui.CloseDropdown())
                            recipe.RecordUndo().subgroup = new ProductionTable(recipe);
                        
                        if (recipe.subgroup != null && imgui.BuildButton("Add nested desired product") && imgui.CloseDropdown())
                            view.AddDesiredProductAtLevel(recipe.subgroup);

                        if (recipe.subgroup != null && imgui.BuildButton("Add raw recipe") && imgui.CloseDropdown())
                            SelectObjectPanel.Select(Database.recipes.all, "Select raw recipe", r => view.AddRecipe(recipe.subgroup, r));

                        if (recipe.subgroup != null && imgui.BuildButton("Unpack nested table"))
                        {
                            var evacuate = recipe.subgroup.recipes;
                            recipe.subgroup.RecordUndo();
                            recipe.RecordUndo().subgroup = null;
                            var index = recipe.owner.recipes.IndexOf(recipe);
                            foreach (var evacRecipe in evacuate)
                                evacRecipe.SetOwner(recipe.owner);
                            recipe.owner.RecordUndo().recipes.InsertRange(index+1, evacuate);
                            imgui.CloseDropdown();
                        }

                        if (recipe.subgroup != null && imgui.BuildButton("ShoppingList") && imgui.CloseDropdown())
                            view.BuildShoppngList(recipe);
                        
                        if (imgui.BuildCheckBox("Enabled", recipe.enabled, out var newEnabled))
                            recipe.RecordUndo().enabled = newEnabled;

                        BuildFavourites(imgui, recipe.recipe, "Add recipe to favourites");
                        
                        if (recipe.subgroup != null && imgui.BuildRedButton("Delete nested table") && imgui.CloseDropdown())
                            recipe.owner.RecordUndo().recipes.Remove(recipe);
                        
                        if (recipe.subgroup == null && imgui.BuildRedButton("Delete recipe") && imgui.CloseDropdown())
                            recipe.owner.RecordUndo().recipes.Remove(recipe); 
                    });
                }

                gui.textColor = recipe.hierarchyEnabled ? SchemeColor.BackgroundText : SchemeColor.BackgroundTextFaint;
                gui.BuildText(recipe.recipe.locName, wrap:true);
            }

            public override void BuildMenu(ImGui gui)
            {
                if (gui.BuildButton("Add recipe") && gui.CloseDropdown())
                {
                    SelectObjectPanel.Select(Database.recipes.all, "Select raw recipe", r => view.AddRecipe(view.model, r));
                }

                gui.BuildText("Export inputs and outputs to blueprint with constant combinators:", wrap: true);
                using (gui.EnterRow())
                {
                    gui.BuildText("Amount per:");
                    if (gui.BuildLink("second") && gui.CloseDropdown())
                        ExportIo(1f);
                    if (gui.BuildLink("minute") && gui.CloseDropdown())
                        ExportIo(60f);
                    if (gui.BuildLink("hour") && gui.CloseDropdown())
                        ExportIo(3600f);
                }
            }
            
            private void ExportIo(float multiplier)
            {
                var goods = new List<(Goods, int)>();
                foreach (var link in view.model.links)
                {
                    var rounded = MathUtils.Round(link.amount * multiplier);
                    if (rounded == 0)
                        continue;
                    goods.Add((link.goods, rounded));
                }

                foreach (var flow in view.model.flow)
                {
                    var rounded = MathUtils.Round(flow.amount * multiplier);
                    if (rounded == 0)
                        continue;
                    goods.Add((flow.goods, rounded));
                }

                BlueprintUtilities.ExportConstantCombinators(view.projectPage.name, goods);
            }
        }

        private class EntityColumn : ProductionTableDataColumn
        {
            public EntityColumn(ProductionTableView view) : base(view, "Entity", 8f) {}

            public override void BuildElement(ImGui gui, RecipeRow recipe)
            {
                if (recipe.isOverviewMode)
                    return;
                bool clicked;
                if (recipe.fixedBuildings > 0)
                {
                    var evt = gui.BuildFactorioObjectWithEditableAmount(recipe.entity, recipe.fixedBuildings, UnitOfMeasure.None, out var newAmount);
                    if (evt == GoodsWithAmountEvent.TextEditing)
                        recipe.RecordUndo().fixedBuildings = newAmount;
                    clicked = evt == GoodsWithAmountEvent.ButtonClick;
                }
                else
                    clicked = gui.BuildFactorioObjectWithAmount(recipe.entity, recipe.buildingCount, UnitOfMeasure.None) && recipe.recipe.crafters.Length > 0; 
            
            
                if (clicked)
                {
                    ShowEntityDropPown(gui, recipe);
                }

                gui.AllocateSpacing(0.5f);
                if (recipe.fuel != Database.voidEnergy || recipe.entity == null || recipe.entity.energy.type != EntityEnergyType.Void)
                {
                    view.BuildGoodsIcon(gui, recipe.fuel, recipe.links.fuel, (float) (recipe.parameters.fuelUsagePerSecondPerRecipe * recipe.recipesPerSecond), ProductDropdownType.Fuel, recipe, recipe.linkRoot);
                }
                else
                {
                    if (recipe.recipe == Database.electricityGeneration && recipe.entity.factorioType == "solar-panel")
                        BuildSolarPanelAccumulatorView(gui, recipe);
                }
            }
            
            private void BuildSolarPanelAccumulatorView(ImGui gui, RecipeRow recipe)
            {
                var accumulator = recipe.GetVariant(Database.allAccumulators);
                var requiredMj = recipe.entity.craftingSpeed * recipe.buildingCount * (70 / 0.7f); // 70 seconds of charge time to last through the night
                var requiredAccumulators = requiredMj / accumulator.accumulatorCapacity;
                if (gui.BuildFactorioObjectWithAmount(accumulator, requiredAccumulators, UnitOfMeasure.None))
                    ShowAccumulatorDropdown(gui, recipe, accumulator);
            }

            private void ShowAccumulatorDropdown(ImGui gui, RecipeRow recipe, Entity accumulator)
            {
                gui.ShowDropDown(imGui =>
                {
                    imGui.BuildInlineObejctListAndButton<EntityAccumulator>(Database.allAccumulators, DataUtils.DefaultOrdering,
                        accum => recipe.RecordUndo().ChangeVariant(accumulator, accum), "Select accumulator",
                        extra: x => DataUtils.FormatAmount(x.accumulatorCapacity, UnitOfMeasure.Megajoule));
                });
            }
            
            private void ShowEntityDropPown(ImGui imgui, RecipeRow recipe)
            {
                imgui.ShowDropDown(gui =>
                {
                    gui.BuildInlineObejctListAndButton(recipe.recipe.crafters, DataUtils.FavouriteCrafter, sel =>
                    {
                        if (recipe.entity == sel)
                            return;
                        recipe.RecordUndo().entity = sel;
                        if (!sel.energy.fuels.Contains(recipe.fuel))
                            recipe.fuel = recipe.entity.energy.fuels.AutoSelect(DataUtils.FavouriteFuel);
                    }, "Select crafting entity", extra: x => DataUtils.FormatAmount(x.craftingSpeed, UnitOfMeasure.Percent));

                    if (recipe.fixedBuildings > 0f)
                    {
                        if (gui.BuildButton("Clear fixed building count") && gui.CloseDropdown())
                            recipe.RecordUndo().fixedBuildings = 0f;
                    } 
                    else
                    {
                        if (gui.BuildButton("Set fixed building count") && gui.CloseDropdown())
                            recipe.RecordUndo().fixedBuildings = recipe.buildingCount <= 0f ? 1f : recipe.buildingCount;
                    }
                    
                    if (recipe.entity != null && gui.BuildButton("Create single building blueprint") && gui.CloseDropdown())
                    {
                        var entity = new BlueprintEntity {index = 1, name = recipe.entity.name};
                        if (!(recipe.recipe is Mechanics))
                            entity.recipe = recipe.recipe.name;
                        var modules = recipe.parameters.modules.modules;
                        if (modules != null)
                        {
                            entity.items = new Dictionary<string, int>();
                            foreach (var (module, count, beacon) in recipe.parameters.modules.modules)
                                if (!beacon)
                                    entity.items[module.name] = count;
                        }
                        var bp = new BlueprintString {blueprint = {label = recipe.recipe.locName, entities = { entity }}};
                        SDL.SDL_SetClipboardText(bp.ToBpString());
                    }
                    
                    if (recipe.recipe.crafters.Length > 1)
                        BuildFavourites(gui, recipe.entity, "Add building to favourites");
                });
            }
            
            public override void BuildMenu(ImGui gui)
            {
                if (gui.BuildButton("Mass set assembler") && gui.CloseDropdown())
                {
                    SelectObjectPanel.Select(Database.allCrafters, "Set assembler for all recipes", set =>
                    {
                        DataUtils.FavouriteCrafter.AddToFavourite(set, 10);
                        foreach (var recipe in view.GetRecipesRecursive())
                        {
                            if (recipe.recipe.crafters.Contains(set))
                            {
                                recipe.RecordUndo().entity = set;
                                if (!set.energy.fuels.Contains(recipe.fuel))
                                    recipe.fuel = recipe.entity.energy.fuels.AutoSelect(DataUtils.FavouriteFuel);
                            }
                        }
                    }, DataUtils.FavouriteCrafter, false);
                }

                if (gui.BuildButton("Mass set fuel") && gui.CloseDropdown())
                {
                    SelectObjectPanel.Select(Database.goods.all.Where(x => x.fuelValue > 0), "Set fuel for all recipes", set =>
                    {
                        DataUtils.FavouriteFuel.AddToFavourite(set, 10);
                        foreach (var recipe in view.GetRecipesRecursive())
                        {
                            if (recipe.entity != null && recipe.entity.energy.fuels.Contains(set))
                                recipe.RecordUndo().fuel = set;
                        }
                    }, DataUtils.FavouriteFuel, false);
                }

                if (gui.BuildButton("Shopping list") && gui.CloseDropdown())
                    view.BuildShoppngList(null);
            }
        }

        private class IngredientsColumn : ProductionTableDataColumn
        {
            public IngredientsColumn(ProductionTableView view) : base(view, "Ingredients", 32f, 16f, 100f, hasMenu:false) {}

            public override void BuildElement(ImGui gui, RecipeRow recipe)
            {
                var grid = gui.EnterInlineGrid(3f, 1f);
                if (recipe.isOverviewMode)
                {
                    view.BuildTableIngredients(gui, recipe.subgroup, recipe.owner, ref grid);
                }
                else
                {
                    for (var i = 0; i < recipe.recipe.ingredients.Length; i++)
                    {
                        var ingredient = recipe.recipe.ingredients[i];
                        var link = recipe.links.ingredients[i];
                        var goods = recipe.links.ingredientGoods[i];
                        grid.Next();
                        view.BuildGoodsIcon(gui, goods, link, (float) (ingredient.amount * recipe.recipesPerSecond), ProductDropdownType.Ingredient, recipe, recipe.linkRoot, ingredient.variants);
                    }
                }
                grid.Dispose();
            }
        }
        
        private class ProductsColumn : ProductionTableDataColumn
        {
            public ProductsColumn(ProductionTableView view) : base(view, "Products", 12f, 10f, 70f, hasMenu:false) {}

            public override void BuildElement(ImGui gui, RecipeRow recipe)
            {
                var grid = gui.EnterInlineGrid(3f, 1f);
                if (recipe.isOverviewMode)
                {
                    view.BuildTableProducts(gui, recipe.subgroup, recipe.owner, ref grid);
                }
                else
                {
                    for (var i = 0; i < recipe.recipe.products.Length; i++)
                    {
                        var product = recipe.recipe.products[i];
                        grid.Next();
                        view.BuildGoodsIcon(gui, product.goods, recipe.links.products[i], (float) (recipe.recipesPerSecond * product.GetAmount(recipe.parameters.productivity)), ProductDropdownType.Product,
                            recipe, recipe.linkRoot);
                    }
                }
                grid.Dispose();
            }
        }

        private class ModulesColumn : ProductionTableDataColumn
        {
            private readonly VirtualScrollList<ProjectModuleTemplate> moduleTemplateList;
            private RecipeRow editingRecipeModules;

            public ModulesColumn(ProductionTableView view) : base(view, "Modules", 10f, 7f, 16f)
            {
                moduleTemplateList = new VirtualScrollList<ProjectModuleTemplate>(15f, new Vector2(20f, 2.5f), ModuleTemplateDrawer, collapsible:true);
            }
            
            private void ModuleTemplateDrawer(ImGui gui, ProjectModuleTemplate element, int index)
            {
                var evt = gui.BuildContextMenuButton(element.name, icon: element.icon?.icon ?? default, disabled:!element.template.IsCompatibleWith(editingRecipeModules));
                if (evt == ButtonEvent.Click && gui.CloseDropdown())
                {
                    var copied = JsonUtils.Copy(element.template, editingRecipeModules, null);
                    editingRecipeModules.RecordUndo().modules = copied;
                    view.Rebuild();
                }
                else if (evt == ButtonEvent.MouseOver)
                    ShowModuleTemplateTooltip(gui, element.template);
            }

            public override void BuildElement(ImGui gui, RecipeRow recipe)
            {
                if (recipe.isOverviewMode)
                    return;
                if (recipe.entity == null || recipe.entity.allowedEffects == AllowedEffects.None || recipe.recipe.modules.Length == 0)
                    return;
                using (var grid = gui.EnterInlineGrid(3f))
                {
                    if (recipe.parameters.modules.modules == null || recipe.parameters.modules.modules.Length == 0)
                    {
                        grid.Next();
                        if (gui.BuildFactorioObjectWithAmount(null,0, UnitOfMeasure.None))
                            ShowModuleDropDown(gui, recipe);
                    }
                    else
                    {
                        var wasbeacon = false;
                        foreach (var (module, count, beacon) in recipe.parameters.modules.modules)
                        {
                            if (beacon && !wasbeacon)
                            {
                                wasbeacon = true;
                                if (recipe.parameters.modules.beacon != null)
                                {
                                    grid.Next();
                                    if (gui.BuildFactorioObjectWithAmount(recipe.parameters.modules.beacon, recipe.parameters.modules.beaconCount, UnitOfMeasure.None))
                                        ShowModuleDropDown(gui, recipe);
                                }
                            }
                            grid.Next();
                            if (gui.BuildFactorioObjectWithAmount(module,count, UnitOfMeasure.None))
                                ShowModuleDropDown(gui, recipe);
                        }
                    }
                }
            }
            
            private void ShowModuleTemplateTooltip(ImGui gui, ModuleTemplate template)
            {
                gui.ShowTooltip(imGui =>
                {
                    if (!template.IsCompatibleWith(editingRecipeModules))
                        imGui.BuildText("This module template seems incompatible with the recipe or the building", wrap:true);
                    using (var grid = imGui.EnterInlineGrid(3f, 1f))
                    {
                        foreach (var module in template.list)
                        {
                            grid.Next();
                            imGui.BuildFactorioObjectWithAmount(module.module, module.fixedCount, UnitOfMeasure.None);
                        }
                    
                        if (template.beacon != null)
                        {
                            grid.Next();
                            imGui.BuildFactorioObjectWithAmount(template.beacon, template.CalcBeaconCount(), UnitOfMeasure.None);
                            foreach (var module in template.beaconList)
                            {
                                grid.Next();
                                imGui.BuildFactorioObjectWithAmount(module.module, module.fixedCount, UnitOfMeasure.None);
                            }
                        }
                    }
                });
            }
            
            private void ShowModuleDropDown(ImGui gui, RecipeRow recipe)
            {
                var modules = recipe.recipe.modules.Where(x => recipe.entity?.CanAcceptModule(x.module) ?? false).ToArray();
                editingRecipeModules = recipe;
                moduleTemplateList.data = Project.current.sharedModuleTemplates.Where(x => x.filterEntities.Count == 0 || x.filterEntities.Contains(recipe.entity))
                    .OrderByDescending(x => x.template.IsCompatibleWith(recipe)).ToArray();

                gui.ShowDropDown(dropGui =>
                {
                    if (dropGui.BuildButton("Use default modules") && dropGui.CloseDropdown())
                    {
                        recipe.RemoveFixedModules();
                    }

                    if (recipe.entity?.moduleSlots > 0)
                        dropGui.BuildInlineObejctListAndButton(modules, DataUtils.FavouriteModule, recipe.SetFixedModule, "Select fixed module");

                    if (moduleTemplateList.data.Count > 0)
                    {
                        dropGui.BuildText("Use module template:", wrap:true, font:Font.subheader);
                        moduleTemplateList.Build(dropGui);
                    }
                    if (dropGui.BuildButton("Configure module templates") && dropGui.CloseDropdown())
                        ModuleTemplateConfiguration.Show();

                    if (dropGui.BuildButton("Customize modules") && dropGui.CloseDropdown())
                        ModuleCustomisationScreen.Show(recipe);
                });
            }

            public override void BuildMenu(ImGui gui)
            {
                var model = view.model;
                if (model.modules == null)
                    model.RecordUndo(true).modules = new ModuleFillerParameters(model);
                gui.BuildText("Auto modules", Font.subheader);
                ModuleFillerParametersScreen.BuildSimple(gui, model.modules);
                if (gui.BuildButton("More settings") && gui.CloseDropdown())
                    ModuleFillerParametersScreen.Show(model.modules);
            }
        }
        
        public static void BuildFavourites(ImGui imgui, FactorioObject obj, string prompt)
        {
            if (obj == null)
                return;
            var isFavourite = Project.current.preferences.favourites.Contains(obj);
            using (imgui.EnterRow(0.5f, RectAllocator.LeftRow))
            {
                imgui.BuildIcon(isFavourite ? Icon.StarFull : Icon.StarEmpty);
                imgui.RemainingRow().BuildText(isFavourite ? "Favourite" : prompt);
            }
            if (imgui.OnClick(imgui.lastRect))
                Project.current.preferences.ToggleFavourite(obj);
        }

        public override float CalculateWidth() => flatHierarchyBuilder.width;

        public override void CreateModelDropdown(ImGui gui, Type type, Project project)
        {
            if (gui.BuildContextMenuButton("Create production sheet") && gui.CloseDropdown())
                ProjectPageSettingsPanel.Show(null, (name, icon) => MainScreen.Instance.AddProjectPage(name, icon, typeof(ProductionTable), true, true));
        }

        private static readonly IComparer<Goods> DefaultVariantOrdering = new DataUtils.FactorioObjectComparer<Goods>((x, y) => (y.ApproximateFlow() / MathF.Abs(y.Cost())).CompareTo(x.ApproximateFlow() / MathF.Abs(x.Cost())));
        private void AddRecipe(ProductionTable table, Recipe recipe)
        {
            var recipeRow = new RecipeRow(table, recipe);
            table.RecordUndo().recipes.Add(recipeRow);
            recipeRow.entity = recipe.crafters.AutoSelect(DataUtils.FavouriteCrafter);
            recipeRow.fuel = recipeRow.entity.energy.fuels.AutoSelect(DataUtils.FavouriteFuel);
            foreach (var ingr in recipeRow.recipe.ingredients)
            {
                if (ingr.variants != null)
                    recipeRow.variants.Add(ingr.variants.AutoSelect(DefaultVariantOrdering));
            }
        }
        
        private enum ProductDropdownType
        {
            DesiredProduct,
            Ingredient,
            Product,
            Fuel
        }

        private void CreateLink(ProductionTable table, Goods goods)
        {
            if (table.linkMap.ContainsKey(goods))
                return;
            var link = new ProductionLink(table, goods);
            Rebuild();
            table.RecordUndo().links.Add(link);
        }

        private void DestroyLink(ProductionLink link)
        {
            if (link.owner.links.Contains(link))
            {
                link.owner.RecordUndo().links.Remove(link);
                Rebuild();
            }
        }

        private void CreateNewProductionTable(Goods goods, float amount)
        {
            var page = MainScreen.Instance.AddProjectPage(goods.locName, goods, typeof(ProductionTable), true, false);
            var content = page.content as ProductionTable;
            var link = new ProductionLink(content, goods) {amount = amount > 0 ? amount : 1};
            content.links.Add(link);
            content.RebuildLinkMap();
        }

        private void OpenProductDropdown(ImGui targetGui, Rect rect, Goods goods, float amount, ProductionLink link, ProductDropdownType type, RecipeRow recipe, ProductionTable context, Goods[] variants = null)
        {
            if (InputSystem.Instance.shift)
            {
                Project.current.preferences.SetSourceResource(goods, !goods.IsSourceResource());
                targetGui.Rebuild();
                return;
            }
            
            var comparer = DataUtils.GetRecipeComparerFor(goods);
            var allRecipes = new HashSet<Recipe>(context.recipes.Select(x => x.recipe));
            Predicate<Recipe> recipeExists = rec => allRecipes.Contains(rec); 
            Action<Recipe> addRecipe = async rec =>
            {
                if (variants == null)
                    CreateLink(context, goods);
                else
                {
                    foreach (var variant in variants)
                    {
                        if (rec.GetProduction(variant) > 0f)
                        {
                            CreateLink(context, variant);
                            if (variant != goods)
                                recipe.RecordUndo().ChangeVariant(goods, variant);
                            break;
                        }
                    }
                }
                if (!allRecipes.Contains(rec) || (await MessageBox.Show("Recipe already exists", "Add a second copy?", "Add a copy", "Cancel")).choice)
                    AddRecipe(context, rec);
            };
            
            if (InputSystem.Instance.control)
            {
                var isInput = type == ProductDropdownType.Fuel || type == ProductDropdownType.Ingredient || (type == ProductDropdownType.DesiredProduct && amount > 0);
                var recipeList = isInput ? goods.production : goods.usages;
                if (recipeList.SelectSingle(out var selected))
                {
                    addRecipe(selected);
                    return;
                }
            }
            
            
            var selectFuel = type != ProductDropdownType.Fuel ? null : (Action<Goods>)(fuel =>
            {
                recipe.RecordUndo().fuel = fuel;
            });
            var allProduction = goods == null ? Array.Empty<Recipe>() : variants == null ? goods.production : variants.SelectMany(x => x.production).Distinct().ToArray();
            var fuelDisplayFunc = recipe?.entity?.energy.type == EntityEnergyType.FluidHeat
                ? (Func<Goods, string>) (g => DataUtils.FormatAmount(g.fluid?.heatValue ?? 0, UnitOfMeasure.Megajoule))
                : g => DataUtils.FormatAmount(g.fuelValue, UnitOfMeasure.Megajoule);
            
            targetGui.ShowDropDown(rect, DropDownContent, new Padding(1f), 25f);

            void DropDownContent(ImGui gui)
            {
                if (type == ProductDropdownType.Fuel && recipe?.entity != null)
                {
                    if (recipe.entity.energy.fuels.Length == 0)
                        gui.BuildText("This entity has no known fuels");
                    else if (recipe.entity.energy.fuels.Length > 1 || recipe.entity.energy.fuels[0] != recipe.fuel)
                    {
                        BuildFavourites(gui, recipe.fuel, "Add fuel to favourites");
                        gui.BuildInlineObejctListAndButton(recipe.entity.energy.fuels, DataUtils.FavouriteFuel, selectFuel, "Select fuel", extra: fuelDisplayFunc);
                    }
                }
                    

                if (variants != null)
                {
                    gui.BuildText("Accepted fluid variants:");
                    using (var grid = gui.EnterInlineGrid(3f))
                    {
                        foreach (var variant in variants)
                        {
                            grid.Next();
                            if (gui.BuildFactorioObjectButton(variant, 3f, MilestoneDisplay.Contained, variant == goods ? SchemeColor.Primary : SchemeColor.None) &&
                                variant != goods)
                            {
                                recipe.RecordUndo().ChangeVariant(goods, variant);
                                gui.CloseDropdown();
                            }
                        }
                    }

                    gui.allocator = RectAllocator.Stretch;
                }

                if (link != null)
                {
                    if (!link.flags.HasFlags(ProductionLink.Flags.HasProduction))
                        gui.BuildText("This link has no production (Link ignored)", wrap:true, color:SchemeColor.Error);
                    if (!link.flags.HasFlags(ProductionLink.Flags.HasConsumption))
                        gui.BuildText("This link has no consumption (Link ignored)", wrap:true, color:SchemeColor.Error);
                    if (link.flags.HasFlags(ProductionLink.Flags.ChildNotMatched))
                        gui.BuildText("Nested table link have unmatched production/consumption. These unmatched products are not captured by this link.", wrap:true, color:SchemeColor.Error);
                    if (!link.flags.HasFlags(ProductionLink.Flags.HasProductionAndConsumption) && link.owner.owner is RecipeRow recipeRow && recipeRow.FindLink(link.goods, out _))
                        gui.BuildText("Nested tables have their own set of links that DON'T connect to parent links. To connect this product to the outside, remove this link", wrap:true, color:SchemeColor.Error);
                    if (link.flags.HasFlags(ProductionLink.Flags.LinkRecursiveNotMatched))
                    {
                        if (link.notMatchedFlow <= 0f)
                            gui.BuildText("YAFC was unable to satisfy this link (Negative feedback loop). This doesn't mean that this link is the problem, but it is part of the loop.", wrap:true, color:SchemeColor.Error);
                        else gui.BuildText("YAFC was unable to satisfy this link (Overproduction). You can allow overproduction for this link to solve the error.", wrap:true, color:SchemeColor.Error);
                    }
                }

                if (type != ProductDropdownType.Product && goods != null && allProduction.Length > 0)
                {
                    gui.BuildInlineObejctListAndButton(allProduction, comparer, addRecipe, "Add production recipe", 6, true, recipeExists);
                    if (link == null)
                    {
                        var iconRect = new Rect(gui.lastRect.Right - 2f, gui.lastRect.Top, 2f, 2f);
                        gui.DrawIcon(iconRect.Expand(-0.2f), Icon.OpenNew, gui.textColor);
                        var evt = gui.BuildButton(iconRect, SchemeColor.None, SchemeColor.Grey); 
                        if (evt == ButtonEvent.Click && gui.CloseDropdown())
                            CreateNewProductionTable(goods, amount);
                        else if (evt == ButtonEvent.MouseOver)
                            gui.ShowTooltip(iconRect, "Create new production table for "+goods.locName);
                    }
                }

                if (type != ProductDropdownType.Fuel && goods != null &&  type != ProductDropdownType.Ingredient && goods.usages.Length > 0)
                    gui.BuildInlineObejctListAndButton(goods.usages, DataUtils.DefaultRecipeOrdering, addRecipe, "Add consumption recipe", type == ProductDropdownType.Product ? 6 : 3, true, recipeExists);
                
                if (type == ProductDropdownType.Product && goods != null && allProduction.Length > 0)
                    gui.BuildInlineObejctListAndButton(allProduction, comparer, addRecipe, "Add production recipe", 1, true, recipeExists);
                
                if (link != null && gui.BuildCheckBox("Allow overproduction", link.algorithm == LinkAlgorithm.AllowOverProduction, out var newValue))
                    link.RecordUndo().algorithm = newValue ? LinkAlgorithm.AllowOverProduction : LinkAlgorithm.Match;

                if (link != null && gui.BuildButton("View link summary") && gui.CloseDropdown())
                    ProductionLinkSummaryScreen.Show(link);

                if (link != null && link.owner == context)
                {
                    if (link.amount != 0)
                        gui.BuildText(goods.locName + " is a desired product and cannot be unlinked.", wrap:true);
                    else gui.BuildText(goods.locName+" production is currently linked. This means that YAFC will try to match production with consumption.", wrap:true);
                    if (type == ProductDropdownType.DesiredProduct)
                    {
                        if (gui.BuildButton("Remove desired product") && gui.CloseDropdown())
                            link.RecordUndo().amount = 0;

                        if (gui.BuildButton("Remove and unlink") && gui.CloseDropdown())
                            DestroyLink(link);
                    } 
                    else if (link.amount == 0 && gui.BuildButton("Unlink") && gui.CloseDropdown())
                        DestroyLink(link);
                }
                else if (goods != null)
                {
                    if (link != null)
                        gui.BuildText(goods.locName+" production is currently linked, but the link is outside this nested table. Nested tables can have its own separate set of links", wrap:true);
                    else gui.BuildText(goods.locName+" production is currently NOT linked. This means that YAFC will make no attempt to match production with consumption.", wrap:true);
                    
                    if (gui.BuildButton("Create link") && gui.CloseDropdown())
                        CreateLink(context, goods);
                }
                
                if (goods is Item)
                    BuildBeltInserterInfo(gui, amount, recipe?.buildingCount ?? 0);
            }
        }

        public override void SetSearchQuery(SearchQuery query)
        {
            model.Search(query);
            bodyContent.Rebuild();
        }

        private void DrawDesiredProduct(ImGui gui, ProductionLink element)
        {
            gui.allocator = RectAllocator.Stretch;
            gui.spacing = 0f;
            var error = element.flags.HasFlags(ProductionLink.Flags.LinkNotMatched); 
            var evt = gui.BuildFactorioObjectWithEditableAmount(element.goods, element.amount, element.goods.flowUnitOfMeasure, out var newAmount, error ? SchemeColor.Error : SchemeColor.Primary);
            if (evt == GoodsWithAmountEvent.ButtonClick)
                OpenProductDropdown(gui, gui.lastRect, element.goods, element.amount, element, ProductDropdownType.DesiredProduct, null, element.owner);
            else if (evt == GoodsWithAmountEvent.TextEditing && newAmount != 0)
                element.RecordUndo().amount = newAmount;
        }

        public override void Rebuild(bool visualOnly = false)
        {
            flatHierarchyBuilder.SetData(model);
            base.Rebuild(visualOnly);
        }

        private void BuildGoodsIcon(ImGui gui, Goods goods, ProductionLink link, float amount, ProductDropdownType dropdownType, RecipeRow recipe, ProductionTable context, Goods[] variants = null)
        {
            var linkIsError = link != null && ((link.flags & (ProductionLink.Flags.HasProductionAndConsumption | ProductionLink.Flags.LinkRecursiveNotMatched | ProductionLink.Flags.ChildNotMatched)) != ProductionLink.Flags.HasProductionAndConsumption);
            var linkIsForeign = link != null && link.owner != context;
            if (gui.BuildFactorioObjectWithAmount(goods, amount, goods?.flowUnitOfMeasure ?? UnitOfMeasure.None, link != null ? linkIsError ? SchemeColor.Error : linkIsForeign ? SchemeColor.Secondary : SchemeColor.Primary : goods.IsSourceResource() ? SchemeColor.Green : SchemeColor.None))
            {
                OpenProductDropdown(gui, gui.lastRect, goods, amount, link, dropdownType, recipe, context, variants);
            }
        }

        private void BuildTableProducts(ImGui gui, ProductionTable table, ProductionTable context, ref ImGuiUtils.InlineGridBuilder grid)
        {
            var flow = table.flow;
            var firstProduct = Array.BinarySearch(flow, new ProductionTableFlow(Database.voidEnergy, 1e-9f, null), model);
            if (firstProduct < 0)
                firstProduct = ~firstProduct;
            for (var i = firstProduct; i < flow.Length; i++)
            { 
                grid.Next();
                BuildGoodsIcon(gui, flow[i].goods, flow[i].link, flow[i].amount, ProductDropdownType.Product, null, context);
            }
        }

        private void FillRecipeList(ProductionTable table, List<RecipeRow> list)
        {
            foreach (var recipe in table.recipes)
            {
                list.Add(recipe);
                if (recipe.subgroup != null)
                    FillRecipeList(recipe.subgroup, list);
            }
        }
        
        private void FillLinkList(ProductionTable table, List<ProductionLink> list)
        {
            list.AddRange(table.links);
            foreach (var recipe in table.recipes)
            {
                if (recipe.subgroup != null)
                    FillLinkList(recipe.subgroup, list);
            }
        }
        
        private List<RecipeRow> GetRecipesRecursive()
        {
            var list = new List<RecipeRow>();
            FillRecipeList(model, list);
            return list;
        }
        
        private List<RecipeRow> GetRecipesRecursive(RecipeRow recipeRoot)
        {
            var list = new List<RecipeRow> {recipeRoot};
            if (recipeRoot.subgroup != null)
                FillRecipeList(recipeRoot.subgroup, list);
            return list;
        }
        
        private List<ProductionLink> GetLinksRecursive()
        {
            var list = new List<ProductionLink>();
            FillLinkList(model, list);
            return list;
        }

        private void BuildShoppngList(RecipeRow recipeRoot)
        {
            var shopList = new Dictionary<FactorioObject, int>();
            var recipes = recipeRoot == null ? GetRecipesRecursive() : GetRecipesRecursive(recipeRoot);
            foreach (var recipe in recipes)
            {
                if (recipe.entity != null)
                {
                    shopList.TryGetValue(recipe.entity, out var prev);
                    var count = MathUtils.Ceil(recipe.buildingCount);
                    shopList[recipe.entity] = prev + count;
                    if (recipe.parameters.modules.modules != null)
                    {
                        foreach (var module in recipe.parameters.modules.modules)
                        {
                            if (!module.beacon)
                            {
                                shopList.TryGetValue(module.module, out prev);
                                shopList[module.module] = prev + count * module.count;
                            }
                        }
                    }
                }
            }
            ShoppingListScreen.Show(shopList);
        }

        private void BuildBeltInserterInfo(ImGui gui, float amount, float buildingCount)
        {
            var prefs = Project.current.preferences;
            var belt = prefs.defaultBelt;
            var inserter = prefs.defaultInserter;
            if (belt == null || inserter == null)
                return;
            
            var beltCount = amount / belt.beltItemsPerSecond;
            var buildingsPerHalfBelt = belt.beltItemsPerSecond * buildingCount / (amount * 2f);
            var click = false;

            using (gui.EnterRow())
            {
                click |= gui.BuildFactorioObjectButton(belt);
                gui.BuildText(DataUtils.FormatAmount(beltCount, UnitOfMeasure.None));
                if (buildingsPerHalfBelt > 0f)
                    gui.BuildText("(Buildings per half belt: "+DataUtils.FormatAmount(buildingsPerHalfBelt, UnitOfMeasure.None) + ")");
            }

            using (gui.EnterRow())
            {
                var capacity = prefs.inserterCapacity;
                var inserterBase = inserter.inserterSwingTime * amount / capacity;
                click |= gui.BuildFactorioObjectButton(inserter);
                var text = DataUtils.FormatAmount(inserterBase, UnitOfMeasure.None);
                if (buildingCount > 1)
                    text += " (" + DataUtils.FormatAmount(inserterBase / buildingCount, UnitOfMeasure.None) + "/building)";
                gui.BuildText(text);
                if (capacity > 1)
                {
                    var withBeltSwingTime = inserter.inserterSwingTime + 2f * (capacity - 1.5f) / belt.beltItemsPerSecond;
                    var inserterToBelt = amount * withBeltSwingTime / capacity;
                    click |= gui.BuildFactorioObjectButton(belt);
                    gui.AllocateSpacing(-1.5f);
                    click |= gui.BuildFactorioObjectButton(inserter);
                    text = DataUtils.FormatAmount(inserterToBelt, UnitOfMeasure.None, "~");
                    if (buildingCount > 1)
                        text += " (" + DataUtils.FormatAmount(inserterToBelt / buildingCount, UnitOfMeasure.None) + "/b)"; 
                    gui.BuildText(text);
                }
            }

            if (click && gui.CloseDropdown())
                PreferencesScreen.Show();
        }

        private void BuildTableIngredients(ImGui gui, ProductionTable table, ProductionTable context, ref ImGuiUtils.InlineGridBuilder grid)
        {
            foreach (var flow in table.flow)
            {
                if (flow.amount >= 0f)
                    break;
                grid.Next();
                BuildGoodsIcon(gui, flow.goods, flow.link, -flow.amount, ProductDropdownType.Ingredient, null, context);
            }
        }

        private void DrawRecipeTagSelect(ImGui gui, RecipeRow recipe)
        {
            using (gui.EnterRow())
            {
                for (var i = 0; i < tagIcons.Length; i++)
                {
                    var (icon, color) = tagIcons[i];
                    var selected = i == recipe.tag;
                    gui.BuildIcon(icon, color:selected ? SchemeColor.Background : color);
                    if (selected)
                        gui.DrawRectangle(gui.lastRect, color);
                    else
                    {
                        var evt = gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.BackgroundAlt, SchemeColor.BackgroundAlt); 
                        if (evt)
                            recipe.RecordUndo(true).tag = i;
                    }
                }
            }
        }

        protected override void BuildHeader(ImGui gui)
        {
            base.BuildHeader(gui);
            flatHierarchyBuilder.BuildHeader(gui);
        }

        protected override void BuildPageTooltip(ImGui gui, ProductionTable contents)
        {
            foreach (var link in contents.links)
            {
                if (link.amount != 0f)
                {
                    using (gui.EnterRow())
                    {
                        gui.BuildFactorioObjectIcon(link.goods);
                        using (gui.EnterGroup(default, RectAllocator.LeftAlign, spacing:0))
                        {
                            gui.BuildText(link.goods.locName);
                            gui.BuildText(DataUtils.FormatAmount(link.amount, link.goods.flowUnitOfMeasure));
                        }
                    }
                }
            }

            foreach (var row in contents.recipes)
            {
                if (row.fixedBuildings != 0 && row.entity != null)
                {
                    using (gui.EnterRow())
                    {
                        gui.BuildFactorioObjectIcon(row.recipe);
                        using (gui.EnterGroup(default, RectAllocator.LeftAlign, spacing:0))
                        {
                            gui.BuildText(row.recipe.locName);
                            gui.BuildText(row.entity.locName+": "+DataUtils.FormatAmount(row.fixedBuildings, UnitOfMeasure.None));
                        }
                    }
                }

                if (row.subgroup != null)
                    BuildPageTooltip(gui, row.subgroup);
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
            {WarningFlags.TemperatureForIngredientNotMatch, "This recipe does care about ingridient temperature, and the temperature range does not match"},
            {WarningFlags.ReactorsNeighboursFromPrefs, "Assumes reactor formation from preferences"},
            {WarningFlags.AssumesNauvisSolarRatio, "Energy production values assumes Nauvis solar ration (70% power output). Don't forget accumulators."},
            {WarningFlags.RecipeTickLimit, "Production is limited to 60 recipes per second (1/tick). This interacts weirdly with productivity bonus - actual productivity may be imprecise and may depend on your setup - test your setup before commiting to it."}
        };
        
        private void BuildRecipePad(ImGui gui, RecipeRow row)
        {
            
        }

        private static readonly (Icon icon, SchemeColor color)[] tagIcons = {
            (Icon.Empty, SchemeColor.BackgroundTextFaint),
            (Icon.Check, SchemeColor.Green),
            (Icon.Warning, SchemeColor.Secondary),
            (Icon.Error, SchemeColor.Error),
            (Icon.Edit, SchemeColor.Primary),
            (Icon.Help, SchemeColor.BackgroundText),
            (Icon.Time, SchemeColor.BackgroundText),
            (Icon.DarkMode, SchemeColor.BackgroundText),
            (Icon.Settings, SchemeColor.BackgroundText),
        };

        

        protected override void BuildContent(ImGui gui)
        {
            if (model == null)
                return;
            BuildSummary(gui, model);
            gui.AllocateSpacing();
            flatHierarchyBuilder.Build(gui);
            gui.SetMinWidth(flatHierarchyBuilder.width);
        }

        private void AddDesiredProductAtLevel(ProductionTable table)
        {
            SelectObjectPanel.Select(Database.goods.all, "Add desired product", product =>
            {
                if (table.linkMap.TryGetValue(product, out var existing))
                {
                    if (existing.amount != 0)
                        return;
                    existing.RecordUndo().amount = 1f;
                }
                else
                {
                    table.RecordUndo().links.Add(new ProductionLink(table, product) {amount = 1f});
                }
            });
        }

        private void BuildSummary(ImGui gui, ProductionTable table)
        {
            var isRoot = table == model;
            if (!isRoot && !table.containsDesiredProducts)
                return;
            var elementsPerRow = MathUtils.Floor((flatHierarchyBuilder.width-2f) / 4f);
            gui.spacing = 1f;
            var pad = new Padding(1f, 0.2f);
            using (gui.EnterGroup(pad))
            {
                gui.BuildText("Desired products and amounts (Use negative for input goal):");
                using (var grid = gui.EnterInlineGrid(3f, 1f, elementsPerRow))
                {
                    foreach (var link in table.links)
                    {
                        if (link.amount != 0f)
                        {
                            grid.Next();
                            DrawDesiredProduct(gui, link);
                        }
                    }

                    grid.Next();
                    if (gui.BuildButton(Icon.Plus, SchemeColor.Primary, SchemeColor.PrimalyAlt, size:2.5f))
                        AddDesiredProductAtLevel(table);
                }
            }
            if (gui.isBuilding)
                gui.DrawRectangle(gui.lastRect, SchemeColor.Background, RectangleBorder.Thin);

            if (table.flow.Length > 0 && table.flow[0].amount < 0) 
            {
                using (gui.EnterGroup(pad))
                {
                    gui.BuildText(isRoot ? "Summary ingredients:" : "Import ingredients:");
                    var grid = gui.EnterInlineGrid(3f, 1f, elementsPerRow);
                    BuildTableIngredients(gui, table, table, ref grid);
                    grid.Dispose();
                }
                if (gui.isBuilding)
                    gui.DrawRectangle(gui.lastRect, SchemeColor.Background, RectangleBorder.Thin);
            }
            
            if (table.flow.Length > 0 && table.flow[table.flow.Length - 1].amount > 0)
            {
                using (gui.EnterGroup(pad))
                {
                    gui.BuildText(isRoot ? "Extra products:" : "Export products:");
                    var grid = gui.EnterInlineGrid(3f, 1f, elementsPerRow);
                    BuildTableProducts(gui, table, table, ref grid);
                    grid.Dispose();
                }
                if (gui.isBuilding)
                    gui.DrawRectangle(gui.lastRect, SchemeColor.Background, RectangleBorder.Thin);
            }
        }
    }
}