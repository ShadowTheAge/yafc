using System.Collections.Generic;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class MainScreen : WindowMain
    {
        public static MainScreen Instance { get; private set; }
        private ObjectTooltip tooltip = new ObjectTooltip();

        private List<PseudoScreen> pseudoScreens = new List<PseudoScreen>();
        private ProjectPage activePage = new WorkspacePage();
        private PseudoScreen topScreen;

        public MainScreen(int display) : base(default)
        {
            Instance = this;
            Create("Factorio Calculator", display);
        }

        protected override void BuildContent(ImGui gui)
        {
            if (pseudoScreens.Count > 0)
            {
                var top = pseudoScreens[0];
                if (top != topScreen)
                {
                    topScreen = top;
                    InputSystem.Instance.SetDefaultKeyboardFocus(top);
                }
                top.Build(gui, size);
            }
            else
            {
                if (topScreen != null)
                {
                    InputSystem.Instance.SetDefaultKeyboardFocus(null);
                    topScreen = null;
                }
                if (activePage != null)
                    activePage.Build(gui, 50);
            }
            
            tooltip.Build(gui);
        }

        public void ShowTooltip(IFactorioObjectWrapper obj, ImGui source, Rect sourceRect)
        {
            tooltip.Show(obj, source, sourceRect);
        }

        public bool ShowPseudoScreen(PseudoScreen screen)
        {
            if (pseudoScreens.Contains(screen))
                return false;
            pseudoScreens.Insert(0, screen);
            rootGui.Rebuild();
            return true;
        }

        public void ClosePseudoScreen(PseudoScreen screen)
        {
            pseudoScreens.Remove(screen);
            rootGui.Rebuild();
        }
    }
}