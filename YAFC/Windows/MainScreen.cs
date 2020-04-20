using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class MainScreen : WindowMain
    {
        public static MainScreen Instance { get; private set; }
        private readonly ObjectTooltip tooltip = new ObjectTooltip();
        private readonly Workspace workspace = new Workspace();
        public MainScreen(int display)
        {
            Instance = this;
            Create("Factorio Calculator", display);
        }
        protected override void BuildContent(LayoutState state)
        {
            workspace.Build(state);
            tooltip.Build(state);
        }

        public void ShowTooltip(FactorioObject target, RaycastResult<IMouseEnterHandle> raycast)
        {
            tooltip.Show(target, raycast);
        }
    }
}