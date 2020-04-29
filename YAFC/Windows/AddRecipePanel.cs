using System;
using System.Numerics;
using System.Threading.Tasks;
using SDL2;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class AddRecipePanel : PseudoScreen<Recipe>
    {
        private static readonly AddRecipePanel Instance = new AddRecipePanel();
        private readonly SearchableList<Recipe> recipes;
        private string filter;
        private Rect searchBox;
        public AddRecipePanel() : base(40f)
        {
            recipes = new SearchableList<Recipe>(30, new Vector2(2.5f, 2.5f), ElementDrawer, ElementFilter);
            recipes.data = Database.allRecipes;
        }

        private bool ElementFilter(Recipe data, string[] searchTokens)
        {
            foreach (var token in searchTokens)
            {   
                if (data.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0 &&
                    data.locName.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (data.locDescr == null || data.locDescr.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)) 
                    return false;
            }

            return true;
        }

        public static Task<Recipe> Show()
        {
            if (MainScreen.Instance.ShowPseudoScreen(Instance))
                return (Instance.taskSource = new TaskCompletionSource<Recipe>()).Task;
            return Task.FromResult<Recipe>(null);
        }

        private void ElementDrawer(ImGui gui, Recipe element, int index)
        {
            var rect = gui.AllocateRect(3f, 3f);
            if (gui.action == ImGuiAction.Build)
            {
                gui.DrawIcon(rect.Expand(-0.2f), element.icon, SchemeColor.Source);
                var milestone = Milestones.GetHighest(element);
                if (milestone != null)
                {
                    var milestoneIcon = new Rect(rect.BottomRight - Vector2.One, Vector2.One);
                    gui.DrawIcon(milestoneIcon, milestone.icon, SchemeColor.Source);
                }
            }

            var buttonEvent = gui.BuildButton(rect, SchemeColor.None, SchemeColor.Grey);
            if (buttonEvent == ImGuiUtils.Event.Click) 
                CloseWithResult(element);
            else if (buttonEvent == ImGuiUtils.Event.MouseOver)
                MainScreen.Instance.ShowTooltip(element, gui, gui.lastRect);
        }

        public override void Build(ImGui gui)
        {
            gui.BuildText("Add new recipe", Font.header, align: RectAlignment.Middle);
            if (gui.BuildTextInput(filter, out filter, "Start typing for search", icon:Icon.Search))
                recipes.filter = filter;
            searchBox = gui.lastRect;
            recipes.Build(gui);
        }

        public override void KeyDown(SDL.SDL_Keysym key)
        {
            contents.SetTextInputFocus(searchBox);
        }
    }
}