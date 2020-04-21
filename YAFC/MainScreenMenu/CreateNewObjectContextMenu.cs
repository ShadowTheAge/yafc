using System.Numerics;
using YAFC.Model;
using YAFC.UI;

namespace YAFC.MainScreenMenu
{
    public class CreateNewObjectContextMenu : ContextMenu
    {
        public static readonly CreateNewObjectContextMenu Instance = new CreateNewObjectContextMenu();

        private readonly FontString header = new FontString(Font.header, "Create object");
        private readonly FontString itemsHeader = new FontString(Font.subheader, "Select output (input) item");
        private readonly SearchableFactorioObjectList listItems = new SearchableFactorioObjectList(new Vector2(30, 20));
        private readonly FontString recipesHeader = new FontString(Font.subheader, "Or maybe recipe?");
        private readonly SearchableFactorioObjectList listRecipes = new SearchableFactorioObjectList(new Vector2(30, 20));

        public CreateNewObjectContextMenu()
        {
            padding = default;
            listItems.data = Database.allGoods;
            listRecipes.data = Database.allRecipes;
        }
        
        protected override void BuildContent(LayoutState state)
        {
            BuildUtils.BuildHeader(header, state);
            BuildUtils.BuildSubHeader(itemsHeader, state);
            state.Build(listItems);
            BuildUtils.BuildSubHeader(recipesHeader, state);
            state.Build(listRecipes);
        }

        protected override Vector2 CalculateSize(LayoutState state) => new Vector2(30, 0);
    }
}