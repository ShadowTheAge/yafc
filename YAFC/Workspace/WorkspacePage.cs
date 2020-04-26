using System.Collections.Generic;
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
                new DataColumn<RecipeRow>("Ingredients", BuildRecipeIngredients, 20f),
                new DataColumn<RecipeRow>("Products", BuildRecipeProducts, 20f),
            };
            grid = new DataGrid<RecipeRow>(columns);
        }

        private void BuildRecipeProducts(ImGui gui, RecipeRow recipe)
        {
            foreach (var product in recipe.recipe.products)
            {
                gui.BuildIcon(product.goods.icon);
            }
        }

        private void BuildRecipeIngredients(ImGui gui, RecipeRow recipe)
        {
            foreach (var ingredient in recipe.recipe.ingredients)
            {
                gui.BuildIcon(ingredient.goods.icon);
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
                
            }
        }
    }
}