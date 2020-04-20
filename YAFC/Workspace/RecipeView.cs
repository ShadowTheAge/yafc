using System;
using Routing;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class RecipeView : NodeView<RecipeConfiguration>
    {
        private readonly IngredientView[] ingredients;
        private readonly ProductView[] products;

        public RecipeView(RecipeConfiguration configuration) : base(configuration, GetRecipeConfigurationSize(configuration))
        {
            
        }

        private static GridPos GetRecipeConfigurationSize(RecipeConfiguration configuration)
        {
            return new GridPos(4, Math.Max(Math.Max(configuration.ingredients.Length, configuration.products.Length), 2) + 1);
        }

        protected override void BuildContent(LayoutState state)
        {
            
        }
    }
}