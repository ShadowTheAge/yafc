using System;
using System.Collections.Generic;
using SDL2;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class MainScreen : WindowMain, IKeyboardFocus
    {
        public static MainScreen Instance { get; private set; }
        private readonly ObjectTooltip tooltip = new ObjectTooltip();

        private readonly List<PseudoScreen> pseudoScreens = new List<PseudoScreen>();
        private readonly ProjectPage activePage = new WorkspacePage();
        private PseudoScreen topScreen;
        private readonly SimpleDropDown dropDown;
        public readonly Project project;

        public MainScreen(int display, Project project) : base(default)
        {
            Instance = this;
            this.project = project;
            dropDown = new SimpleDropDown(new Padding(1f), 20f);
            Create("Factorio Calculator", display);
            if (project.justCreated)
            {
                ShowPseudoScreen(MilestonesPanel.Instance);
            }
        }
        
        public void ShowDropDown(ImGui targetGui, Rect target, SimpleDropDown.Builder builder)
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
                    InputSystem.Instance.SetDefaultKeyboardFocus(this);
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
            screen.Open();
            rootGui.Rebuild();
            return true;
        }

        public void ClosePseudoScreen(PseudoScreen screen)
        {
            pseudoScreens.Remove(screen);
            rootGui.Rebuild();
        }

        public void KeyDown(SDL.SDL_Keysym key)
        {
            if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_S && ((key.mod & SDL.SDL_Keymod.KMOD_CTRL) != 0))
                project.Save(project.attachedFileName);
        }

        public void TextInput(string input) {}
        public void KeyUp(SDL.SDL_Keysym key) {}
        public void FocusChanged(bool focused) {}
    }
}