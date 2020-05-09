using System;
using System.Collections.Generic;
using System.Numerics;
using YAFC.Model;
using YAFC.Parser;
using YAFC.UI;

namespace YAFC
{
    public class ProductionTableView : ProjectPageView
    {
        private DataColumn<RecipeRow>[] columns;

        private readonly List<ProductionLink> desiredProductsList = new List<ProductionLink>();
        private readonly List<ProductionLink> linkedProductsList = new List<ProductionLink>();
        
        private readonly VirtualScrollList<ProductionLink> desiredProducts;
        private readonly VirtualScrollList<ProductionLink> linkedProducts;
        private readonly ProductionTableFlatHierarchy flatHierarchyBuilder;
        
        private ProductionTable root;
        private ProjectPage projectPage;
        
        public ProductionTableView()
        {
            columns = new[]
            {
                new DataColumn<RecipeRow>("", BuildRecipePad, 3f),
                new DataColumn<RecipeRow>("Recipe", BuildRecipeName, 15f),
                new DataColumn<RecipeRow>("Entity", BuildRecipeEntity, 7f), 
                new DataColumn<RecipeRow>("Ingredients", BuildRecipeIngredients, 20f),
                new DataColumn<RecipeRow>("Products", BuildRecipeProducts, 20f),
            };
            var grid = new DataGrid<RecipeRow>(columns);
            desiredProducts = new VirtualScrollList<ProductionLink>(7, new Vector2(3, 5f), DrawDesiredProduct, 1) { spacing = 0.2f };
            linkedProducts = new VirtualScrollList<ProductionLink>(7, new Vector2(3, 5f), DrawLinkedProduct, 1) { spacing = 0.2f };
            flatHierarchyBuilder = new ProductionTableFlatHierarchy(grid);
        }

        public override void SetModel(ProjectPage page)
        {
            if (root != null)
                projectPage.contentChanged -= Rebuild;
            projectPage = page;
            root = page?.content as ProductionTable;
            if (root != null)
            {
                projectPage.contentChanged += Rebuild;
                Rebuild();
            }
        }

        public override void CreateModelDropdown(ImGui gui, Type type, Project project, ref bool close)
        {
            if (gui.BuildButton("Create production sheet"))
            {
                close = true;
                ProjectPageSettingsPanel.Show(null, (name, icon) =>
                {
                    var page = new ProjectPage(project, type) {icon = icon, name = name};
                    MainScreen.Instance.AddProjectPageAndSetActive(page);
                });
            }
        }

        private void AddRecipe(ProductionTable table, Recipe recipe)
        {
            var recipeRow = new RecipeRow(table, recipe);
            table.RecordUndo().recipes.Add(recipeRow);
            recipeRow.entity = recipe.crafters.AutoSelect(DataUtils.FavouriteCrafter);
            recipeRow.fuel = recipeRow.entity.energy.fuels.AutoSelect(DataUtils.FavouriteFuel);
        }
        
        private enum ProductDropdownType
        {
            DesiredProduct,
            LinkedProduct,
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

        private void DestroyLink(ProductionTable table, Goods goods)
        {
            if (table.linkMap.TryGetValue(goods, out var existing))
            {
                table.RecordUndo().links.Remove(existing);
                Rebuild();
            }
        }

        private void OpenProductDropdown(ImGui targetGui, Rect rect, Goods goods, ProductDropdownType type, RecipeRow recipe)
        {
            var linkRoot = recipe.linkRoot;
            linkRoot.linkMap.TryGetValue(goods, out var link);
            var comparer = DataUtils.GetRecipeComparerFor(goods);
            Action<Recipe> addRecipe = rec =>
            {
                CreateLink(linkRoot, goods);
                AddRecipe(linkRoot, rec);
            };
            var selectFuel = type != ProductDropdownType.Fuel ? null : (Action<Goods>)(fuel =>
            {
                recipe.RecordUndo().fuel = fuel;
                DataUtils.FavouriteFuel.AddToFavourite(fuel);
            });
            MainScreen.Instance.ShowDropDown(targetGui, rect, DropDownContent);

            void DropDownContent(ImGui gui, ref bool close)
            {
                if (type == ProductDropdownType.Fuel && (recipe.entity.energy.fuels.Count > 1 || recipe.entity.energy.fuels[0] != recipe.fuel))
                {
                    close |= gui.BuildInlineObejctListAndButton(recipe.entity.energy.fuels, DataUtils.FavouriteFuel, selectFuel, "Select fuel");
                }
                
                if (goods.production.Length > 0)
                {
                    close |= gui.BuildInlineObejctListAndButton(goods.production, comparer, addRecipe, "Add production recipe");
                }

                if (goods.usages.Length > 0 && gui.BuildButton("Add consumption recipe"))
                {
                    SelectObjectPanel.Select(goods.usages, "Select consumption recipe", addRecipe);
                    close = true;
                }

                if (link != null)
                {
                    if (link.amount != 0)
                        gui.BuildText(goods.locName + " is a desired product and cannot be unlinked.", wrap:true);
                    else gui.BuildText(goods.locName+" production is currently linked. This means that YAFC will try to match production with consumption.", wrap:true);
                    if (type == ProductDropdownType.DesiredProduct)
                    {
                        if (gui.BuildButton("Remove desired product"))
                            link.RecordUndo().amount = 0;
                        if (gui.BuildButton("Remove and unlink"))
                            DestroyLink(linkRoot, goods);
                    } else if (link.amount == 0 && gui.BuildButton("Unlink"))
                    {
                        DestroyLink(linkRoot, goods);
                        close = true;
                    }
                }
                else
                {
                    gui.BuildText(goods.locName+" production is currently NOT linked. This means that YAFC will make no attempt to match production with consumption.", wrap:true);
                    if (gui.BuildButton("Create link"))
                    {
                        CreateLink(linkRoot, goods);
                        close = true;
                    }
                }
            }
        }

        private void OpenObjectSelectDropdown<T>(ImGui targetGui, Rect rect, IReadOnlyList<T> list, IComparer<T> ordering, string header, Action<T> select) where T:FactorioObject
        {
            MainScreen.Instance.ShowDropDown(targetGui, rect, DropDownContent);

            void DropDownContent(ImGui gui, ref bool close)
            {
                close = gui.BuildInlineObejctListAndButton(list, ordering, select, header);
            }
        }

        private void DrawLinkedProduct(ImGui gui, ProductionLink element, int index)
        {
            BuildGoodsIcon(gui, element.goods, element.amount, ProductDropdownType.LinkedProduct, null);
        }

        private void DrawDesiredProduct(ImGui gui, ProductionLink element, int index)
        {
            gui.allocator = RectAllocator.Stretch;
            gui.spacing = 0f;
            if (element == null)
            {
                if (gui.BuildButton(Icon.Plus, SchemeColor.Primary, SchemeColor.PrimalyAlt, size:3f))
                {
                    SelectObjectPanel.Select(Database.allGoods, "Add desired product", product =>
                    {
                        if (root.linkMap.TryGetValue(product, out var existing))
                        {
                            if (existing.amount != 0)
                                return;
                            existing.RecordUndo().amount = 1f;
                        }
                        else
                        {
                            root.RecordUndo().links.Add(new ProductionLink(root, product) {amount = 1f});
                        }
                    });
                }
            }
            else
            {
                var evt = gui.BuildGoodsWithEditableAmount(element.goods, element.amount, out var newAmount, SchemeColor.Primary);
                if (evt == GoodsWithAmountEvent.ButtonClick)
                    OpenProductDropdown(gui, gui.lastRect, element.goods, ProductDropdownType.DesiredProduct, null);
                else if (evt == GoodsWithAmountEvent.TextEditing && newAmount != 0)
                    element.RecordUndo().amount = newAmount;
            }
        }

        public override void Rebuild(bool visuaOnly = false)
        {
            base.Rebuild(visuaOnly);
            flatHierarchyBuilder.SetData(root);
            desiredProductsList.Clear();
            linkedProductsList.Clear();
            foreach (var link in root.links)
            {
                if (link.amount == 0f)
                    linkedProductsList.Add(link);
                else desiredProductsList.Add(link);
            }
            desiredProductsList.Add(null);
            desiredProducts.data = desiredProductsList;
            linkedProducts.data = linkedProductsList;
            headerContent?.Rebuild();
            bodyContent?.Rebuild();
            bodyContent?.Rebuild();
        }

        private void BuildRecipeEntity(ImGui gui, RecipeRow recipe)
        {
            if (gui.BuildObjectWithAmount(recipe.entity, recipe.recipesPerSecond * recipe.recipeTime) && recipe.recipe.crafters.Count > 0)
            {
                OpenObjectSelectDropdown(gui, gui.lastRect, recipe.recipe.crafters, DataUtils.FavouriteCrafter, "Select crafting entity", sel =>
                {
                    DataUtils.FavouriteCrafter.AddToFavourite(sel);
                    if (recipe.entity == sel)
                        return;
                    recipe.RecordUndo().entity = sel;
                    if (!recipe.entity.energy.fuels.Contains(recipe.fuel))
                        recipe.fuel = recipe.entity.energy.fuels.AutoSelect(DataUtils.FavouriteFuel);
                });
            }

            gui.AllocateSpacing(0.5f);
            BuildGoodsIcon(gui, recipe.fuel, recipe.fuelUsagePerSecond * recipe.recipesPerSecond * recipe.recipeTime, ProductDropdownType.Fuel, recipe, true);
        }

        private void BuildGoodsIcon(ImGui gui, Goods goods, float amount, ProductDropdownType dropdownType, RecipeRow recipe, bool isPowerDefault = false)
        {
            var linked = recipe?.linkRoot.linkMap.ContainsKey(goods) ?? false;
            if (gui.BuildObjectWithAmount(goods, amount, linked ? SchemeColor.Primary : SchemeColor.None, goods?.isPower ?? isPowerDefault) && goods != Database.voidEnergy)
            {
                OpenProductDropdown(gui, gui.lastRect, goods, dropdownType, recipe);
            }
        }

        private void BuildRecipeProducts(ImGui gui, RecipeRow recipe)
        {
            foreach (var product in recipe.recipe.products)
                BuildGoodsIcon(gui, product.goods, product.average * recipe.recipesPerSecond * recipe.productionMultiplier, ProductDropdownType.Product, recipe);
        }

        private void BuildRecipeIngredients(ImGui gui, RecipeRow recipe)
        {
            foreach (var ingredient in recipe.recipe.ingredients)
                BuildGoodsIcon(gui, ingredient.goods, ingredient.amount * recipe.recipesPerSecond, ProductDropdownType.Ingredient, recipe);
        }

        private void BuildRecipeName(ImGui gui, RecipeRow recipe)
        {
            gui.spacing = 0.5f;
            if (gui.BuildFactorioObjectButton(recipe.recipe, 3f))
            {
                MainScreen.Instance.ShowDropDown(gui, gui.lastRect, delegate(ImGui imgui, ref bool closed)
                {
                    if (recipe.subgroup == null && imgui.BuildButton("Create nested table"))
                    {
                        recipe.RecordUndo().subgroup = new ProductionTable(recipe);
                        closed = true;
                    }

                    if (recipe.subgroup != null && imgui.BuildButton("Remove nested table"))
                    {
                        var evacuate = recipe.subgroup.recipes;
                        recipe.RecordUndo().subgroup = null;
                        var index = recipe.owner.recipes.IndexOf(recipe);
                        foreach (var evacRecipe in evacuate)
                            evacRecipe.SetOwner(recipe.owner);
                        recipe.owner.RecordUndo().recipes.InsertRange(index+1, evacuate);
                        closed = true;
                    }
                    
                    if (recipe.subgroup == null && imgui.BuildButton("Delete recipe"))
                    {
                        recipe.owner.RecordUndo().recipes.Remove(recipe);
                        closed = true;
                    }
                });
            }
            gui.BuildText(recipe.recipe.locName, wrap:true);
        }

        public override void BuildHeader(ImGui gui)
        {
            using (gui.EnterRow())
            {
                using (gui.EnterFixedPositioning(20f, 0f, new Padding(1f)))
                {
                    gui.BuildText("Desired products");
                    desiredProducts.Build(gui);
                }

                using (gui.EnterFixedPositioning(20f, 0f, new Padding(1f)))
                {
                    gui.BuildText("Linked materials");
                    linkedProducts.Build(gui);
                }
            }

            flatHierarchyBuilder.BuildHeader(gui);
        }

        private static readonly Dictionary<WarningFlags, string> WarningsMeaning = new Dictionary<WarningFlags, string>
        {
            {WarningFlags.UnfeasibleCandidate, "Unable to find solution, it may be impossible. This is one of the candidates that may make solution impossible"},
            {WarningFlags.EntityNotSpecified, "Crafter not specified. Solution is inaccurate." },
            {WarningFlags.FuelNotSpecified, "Fuel not specified. Solution is inaccurate." },
            {WarningFlags.LinkedConsumptionNotProduced, "Linked consumption not produced (link ignored)"},
            {WarningFlags.LinkedProductionNotConsumed, "Linked production not consumed (link ignored)"},
            {WarningFlags.FuelWithTemperatureNotLinked, "This recipe uses fuel with temperature. Should link with producing entity to determine temperature."},
            {WarningFlags.TemperatureForIngredientNotMatch, "This recipe does care about ingridient temperature, and the temperature range does not match"},
            {WarningFlags.TemperatureRangeForBoilerNotImplemented, "Boiler have linked different inputs with different temperatures. Reasonong about resulting temperature is not implemented, using minimal temperature instead"},
            {WarningFlags.TemperatureRangeForFuelNotImplemented, "Fuel have linked with production with different temperatures.  Reasonong about resulting temperature is not implemented, using minimal temperature instead"}
        };
        
        private void BuildRecipePad(ImGui gui, RecipeRow row)
        {
            gui.allocator = RectAllocator.Center;
            gui.spacing = 0f;
            if (row.subgroup != null)
            {
                if (gui.BuildButton(row.subgroup.expanded ? Icon.Collapse : Icon.Expand, SchemeColor.None, SchemeColor.Grey))
                {
                    row.subgroup.RecordUndo(true).expanded = !row.subgroup.expanded;
                    flatHierarchyBuilder.SetData(root);
                }
            }
            
            
            if (row.warningFlags != 0)
            {
                if (gui.BuildRedButton(Icon.Error) == ImGuiUtils.Event.MouseOver)
                {
                    MainScreen.Instance.ShowTooltip(gui, gui.lastRect, g =>
                    {
                        g.boxColor = SchemeColor.Error;
                        g.textColor = SchemeColor.ErrorText;
                        foreach (var (flag, text) in WarningsMeaning)
                        {
                            if ((row.warningFlags & flag) != 0)
                                g.BuildText(text, wrap:true);
                        }
                    });
                }
            }
            else
            {
                //gui.BuildText((index+1).ToString()); TODO
            }
        }

        public override void BuildContent(ImGui gui)
        {
            flatHierarchyBuilder.Build(gui);
        }
    }
}