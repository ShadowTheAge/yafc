using System;
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
        private readonly SimpleDropDown dropDown;

        public MainScreen(int display) : base(default)
        {
            Instance = this;
            dropDown = new SimpleDropDown(new Padding(1f), 20f);
            Create("Factorio Calculator", display);
        }
        
        public void ShowDropDown(ImGui targetGui, Rect target, Func<ImGui, bool> builder)
        {
            dropDown.SetFocus(targetGui, target, builder);
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
            
            dropDown.Build(gui);
            tooltip.Build(gui);
        }

        public void ShowTooltip(IFactorioObjectWrapper obj, ImGui source, Rect sourceRect)
        {
            tooltip.Show(obj, source, sourceRect);
        }

        public bool ShowPseudoScreen(PseudoScreen screen)
        {
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