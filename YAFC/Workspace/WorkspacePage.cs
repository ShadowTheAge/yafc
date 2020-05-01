using YAFC.Model;
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
        
        private readonly Group group = new Group();

        public WorkspacePage() : base(WorkspaceId.None)
        {
            columns = new[]
            {
                new DataColumn<RecipeRow>("Recipe", BuildRecipeName, 10f),
                new DataColumn<RecipeRow>("Entity", BuildRecipeEntity, 7f), 
                new DataColumn<RecipeRow>("Ingredients", BuildRecipeIngredients, 20f),
                new DataColumn<RecipeRow>("Products", BuildRecipeProducts, 20f),
            };
            grid = new DataGrid<RecipeRow>(columns);
        }

        private void SetModelDirty()
        {
            Rebuild(false);
        }

        private void BuildRecipeEntity(ImGui gui, RecipeRow recipe)
        {
            if (gui.BuildFactorioObjectButton(recipe.entity, 3f, true))
            {
                SelectObjectPanel.Select(recipe.recipe.crafters, "Select crafter", sel =>
                {
                    if (recipe.entity == sel)
                        return;
                    recipe.entity = sel;
                    if (!recipe.entity.energy.fuels.Contains(recipe.fuel))
                        recipe.fuel = recipe.entity.energy.fuels.AutoSelectFuel();
                    SetModelDirty();
                });
            }

            if (gui.BuildFactorioObjectButton(recipe.fuel, 3f, true) && recipe.entity != null)
            {
                SelectObjectPanel.Select(recipe.entity.energy.fuels, "Select fuel", sel =>
                {
                    if (recipe.fuel != sel)
                    {
                        recipe.fuel = sel;
                        SetModelDirty();
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

        public override void BuildContent(ImGui gui)
        {
            grid.BuildContent(gui, group.recipes);
            if (gui.BuildButton("Add recipe"))
            {
                SelectObjectPanel.Select(Database.allRecipes, "Add new recipe", recipe =>
                {
                    var recipeRow = new RecipeRow(group, recipe);
                    group.recipes.Add(recipeRow);
                    recipeRow.entity = recipe.crafters.AutoSelect();
                    recipeRow.fuel = recipeRow.entity.energy.fuels.AutoSelectFuel();
                    SetModelDirty();
                });

            }
        }
    }
}