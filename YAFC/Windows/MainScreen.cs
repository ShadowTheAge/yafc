/*using System.Collections.Generic;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class MainScreen : WindowMain
    {
        public static MainScreen Instance { get; private set; }
        private readonly ObjectTooltip tooltip = new ObjectTooltip();
        private ContextMenu activeContextMenu;
        private readonly Workspace workspace = new Workspace();
        private readonly List<PseudoScreen> pseudoScreenStack = new List<PseudoScreen>();

        public override SchemeColor boxColor => SchemeColor.BackgroundAlt;

        public MainScreen(int display)
        {
            Instance = this;
            Create("Factorio Calculator", display);
        }
        protected override void BuildContent(LayoutState state)
        {
            workspace.Build(state);
            if (pseudoScreenStack.Count > 0)
                pseudoScreenStack[pseudoScreenStack.Count-1].Build(state);
            activeContextMenu?.Build(state);
            tooltip.Build(state);
        }

        public void ShowContextMenu(ContextMenu menu)
        {
            activeContextMenu = menu;
            menu.Show();
            Rebuild();
        }
        
        public void PushScreen(PseudoScreen screen)
        {
            pseudoScreenStack.Remove(screen);
            pseudoScreenStack.Add(screen);
        }

        public void PopScreen(PseudoScreen screen)
        {
            pseudoScreenStack.Remove(screen);
        }

        public void ShowTooltip(FactorioObject target, HitTestResult<IMouseHandle> hitTest)
        {
            tooltip.Show(target, hitTest);
        }
    }
}*/