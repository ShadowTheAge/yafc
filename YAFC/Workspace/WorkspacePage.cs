using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using YAFC.Model;
using YAFC.Parser;
using YAFC.UI;
using YAFC.UI.Table;

namespace YAFC
{
    public class WorkspacePage : ProjectPage
    {
        public override Icon icon => Icon.Time;
        public override string header => "Test header";
        
        private DataColumn<RecipeRow>[] columns;
        private readonly DataGrid<RecipeRow> grid;
        private VirtualScrollList<DesiredProduct> desiredProducts;
        private readonly Group group;
        
        public WorkspacePage(Group group) : base(WorkspaceId.None)
        {
            this.@group = group;
            columns = new[]
            {
                new DataColumn<RecipeRow>("Recipe", BuildRecipeName, 15f),
                new DataColumn<RecipeRow>("Entity", BuildRecipeEntity, 7f), 
                new DataColumn<RecipeRow>("Ingredients", BuildRecipeIngredients, 20f),
                new DataColumn<RecipeRow>("Products", BuildRecipeProducts, 20f),
            };
            grid = new DataGrid<RecipeRow>(columns);
            desiredProducts = new VirtualScrollList<DesiredProduct>(7, new Vector2(3, 5f), DrawDesiredProduct, 1) { spacing = 0.5f};
        }

        private void AddRecipe(Recipe recipe)
        {
            var recipeRow = new RecipeRow(group, recipe);
            group.RecordUndo().recipes.Add(recipeRow);
            recipeRow.entity = recipe.crafters.AutoSelect();
            recipeRow.fuel = recipeRow.entity.energy.fuels.AutoSelect(DataUtils.FuelOrdering);
        }
        
        private enum ProductDropdownType
        {
            DesiredProduct
        }

        private void OpenProductDropdown(ImGui targetGui, Rect rect, Goods goods, ProductDropdownType type)
        {
            var linkOptions = new[] {"Unlinked", "Linked"};
            var linkIndex = group.links.FindIndex(x => x.goods == goods);
            var productIndex = @group.desiredProducts.FindIndex(x => x.goods == goods);
            var linkSelect = linkIndex == -1 ? 0 : 1;
            var comparer = DataUtils.GetRecipeComparerFor(goods);
            var addRecipe = (Action<Recipe>) AddRecipe;
            MainScreen.Instance.ShowDropDown(targetGui, rect, DropDownContent);

            void DropDownContent(ImGui gui, ref bool close)
            {
                if (goods.production.Length > 0)
                {
                    close = gui.BuildInlineObejctListAndButton(goods.production, comparer, addRecipe, "Add production recipe");
                }

                if (goods.usages.Length > 0 && gui.BuildButton("Add consumption recipe"))
                {
                    SelectObjectPanel.Select(goods.usages, "Select consumption recipe", AddRecipe);
                    close = true;
                }

                if (type == ProductDropdownType.DesiredProduct && productIndex >= 0 && @group.desiredProducts[productIndex].goods == goods && gui.BuildButton("Remove desired product"))
                {
                    group.RecordUndo().desiredProducts.RemoveAt(productIndex);
                    productIndex = -1;
                    close = true;
                }

                if (gui.BuildRadioGroup(linkOptions, linkSelect, out linkSelect))
                {
                    // TODO implement recipe-based linking
                }
            }
        }

        private void DrawDesiredProduct(ImGui gui, DesiredProduct element, int index)
        {
            gui.allocator = RectAllocator.Stretch;
            gui.spacing = 0f;
            if (element == null)
            {
                if (gui.BuildButton(Icon.Plus, SchemeColor.Primary, SchemeColor.PrimalyAlt, size:3f))
                {
                    SelectObjectPanel.Select(Database.allGoods, "Add desired product", product =>
                    {
                        var desiredProduct = new DesiredProduct(product);
                        group.RecordUndo().desiredProducts.Add(desiredProduct);
                    });
                }
            }
            else
            {
                if (gui.BuildFactorioObjectButton(element.goods, 3f, MilestoneDisplay.Contained))
                {
                    OpenProductDropdown(gui, gui.lastRect, element.goods, ProductDropdownType.DesiredProduct);
                }
                if (gui.BuildTextInput(DataUtils.FormatAmount(element.amount), out var newText, null, false, Icon.None, default, RectAlignment.Middle, SchemeColor.Secondary))
                {
                    if (DataUtils.TryParseAmount(newText, out var newAmount))
                    {
                        group.RecordUndo();
                        element.amount = newAmount;
                    }
                }
            }
        }

        public override void Rebuild(bool visuaOnly = false)
        {
            base.Rebuild(visuaOnly);
            if (!visuaOnly)
            {
                desiredProducts.data = group.desiredProducts.Append(null).ToArray();
                SetModelDirty();
            }
        }

        private void SetModelDirty()
        {
            
        }

        private void BuildRecipeEntity(ImGui gui, RecipeRow recipe)
        {
            if (gui.BuildFactorioObjectButton(recipe.entity, 3f, MilestoneDisplay.Contained))
            {
                SelectObjectPanel.Select(recipe.recipe.crafters, "Select crafter", sel =>
                {
                    if (recipe.entity == sel)
                        return;
                    recipe.RecordUndo().entity = sel;
                    if (!recipe.entity.energy.fuels.Contains(recipe.fuel))
                        recipe.fuel = recipe.entity.energy.fuels.AutoSelect(DataUtils.FuelOrdering);
                });
            }

            if (gui.BuildFactorioObjectButton(recipe.fuel, 3f, MilestoneDisplay.Contained) && recipe.entity != null)
            {
                SelectObjectPanel.Select(recipe.entity.energy.fuels, "Select fuel", sel =>
                {
                    if (recipe.fuel != sel)
                    {
                        recipe.RecordUndo().fuel = sel;
                    }
                }, DataUtils.FuelOrdering);
            }
        }

        private void BuildRecipeProducts(ImGui gui, RecipeRow recipe)
        {
            foreach (var product in recipe.recipe.products)
            {
                gui.BuildIcon(product.goods.icon, 3f);
            }
        }

        private void BuildRecipeIngredients(ImGui gui, RecipeRow recipe)
        {
            foreach (var ingredient in recipe.recipe.ingredients)
            {
                gui.BuildIcon(ingredient.goods.icon, 3f);
            }
        }

        private void BuildRecipeName(ImGui gui, RecipeRow recipe)
        {
            gui.BuildText(recipe.recipe.locName);
        }

        public override void BuildHeader(ImGui gui)
        {
            grid.BuildHeader(gui);
        }

        private void BuildWorkspaceHeader(ImGui gui)
        {
            
        }

        public override void BuildContent(ImGui gui)
        {
            gui.BuildText("Desired products");
            desiredProducts.Build(gui);
            BuildWorkspaceHeader(gui);
            grid.BuildContent(gui, group.recipes);
            if (gui.BuildButton("Add recipe"))
                SelectObjectPanel.Select(Database.allRecipes, "Add new recipe", AddRecipe);
        }
    }
}