using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly ProjectPage activePage;
        private PseudoScreen topScreen;
        private readonly SimpleDropDown dropDown;
        public readonly Project project;
        private readonly FadeDrawer fadeDrawer = new FadeDrawer();

        private class FadeDrawer : IRenderable
        {
            private SDL.SDL_Rect srcRect;
            private IntPtr blurredBackgroundTexture;

            public void CreateDownscaledImage()
            {
                if (blurredBackgroundTexture != IntPtr.Zero)
                    SDL.SDL_DestroyTexture(blurredBackgroundTexture);
                var renderer = Instance.renderer;
                var texture = Instance.RenderToTexture(out var size);
                for (var i = 0; i < 2; i++)
                {
                    var halfSize = new SDL.SDL_Rect() {w = size.w/2, h = size.h/2};
                    var halfTexture = SDL.SDL_CreateTexture(renderer, SDL.SDL_PIXELFORMAT_RGBA8888, (int) SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET, halfSize.w, halfSize.h);
                    SDL.SDL_SetRenderTarget(renderer, halfTexture);
                    var bgColor = SchemeColor.PureBackground.ToSdlColor();
                    SDL.SDL_SetRenderDrawColor(renderer, bgColor.r, bgColor.g, bgColor.b, bgColor.a);
                    SDL.SDL_RenderClear(renderer);
                    SDL.SDL_SetTextureBlendMode(texture, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
                    SDL.SDL_SetTextureAlphaMod(texture, 120);
                    SDL.SDL_RenderCopy(renderer, texture, ref size, ref halfSize);
                    SDL.SDL_DestroyTexture(texture);
                    texture = halfTexture;
                    size = halfSize;
                }
                SDL.SDL_SetRenderTarget(renderer, IntPtr.Zero);
                srcRect = size;
                blurredBackgroundTexture = texture;
            }

            public void Render(IntPtr renderer, SDL.SDL_Rect position, SDL.SDL_Color color)
            {
                if (blurredBackgroundTexture != IntPtr.Zero)
                    SDL.SDL_RenderCopy(renderer, blurredBackgroundTexture, ref srcRect, ref position);
            }
        }

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

            if (project.groups.Count == 0)
            {
                var firstGroup = new Group(project);
                project.groups.Add(firstGroup);
            }
            
            activePage = new WorkspacePage(project.groups[0]);
            InputSystem.Instance.SetDefaultKeyboardFocus(this);
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
                if (gui.action == ImGuiAction.Build)
                    gui.DrawRenderable(new Rect(default, size), fadeDrawer, SchemeColor.None);
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
                
                if (gui.BuildButton(Icon.Settings, SchemeColor.None, SchemeColor.Grey))
                {
                    ShowDropDown(gui, gui.lastRect, SettingsDropdown);
                }
                if (activePage != null)
                    activePage.Build(gui, 50);
            }
            
            dropDown.Build(gui);
            tooltip.Build(gui);
        }

        private void SettingsDropdown(ImGui gui, ref bool closed)
        {
            if (gui.BuildButton("Milestones"))
            {
                ShowPseudoScreen(MilestonesPanel.Instance);
                closed = true;
            }

            if (gui.BuildButton("Flow analysis"))
            {
                SelectObjectPanel.Select(Database.allGoods, "Flow analysis target", g =>
                {
                    var result = BestFlowAnalysis.PerformFlowAnalysis(g);
                    if (result != null)
                        FlowAnalysisScreen.Show(g, result);
                });
                closed = true;
            }
        }

        public void ShowTooltip(IFactorioObjectWrapper obj, ImGui source, Rect sourceRect)
        {
            tooltip.Show(obj, source, sourceRect);
        }

        public bool ShowPseudoScreen(PseudoScreen screen)
        {
            if (topScreen == null)
            {
                Ui.ExecuteInMainThread(x => fadeDrawer.CreateDownscaledImage(), null);
            }
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
            var ctrl = (key.mod & SDL.SDL_Keymod.KMOD_CTRL) != 0;
            if (ctrl)
            {
                if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_S)
                    project.Save(project.attachedFileName);
                if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_Z)
                {
                    if ((key.mod & SDL.SDL_Keymod.KMOD_SHIFT) != 0)
                        project.undo.PerformRedo();
                    else project.undo.PerformUndo();
                    activePage.Rebuild(false);
                }

                if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_Y)
                {
                    project.undo.PerformRedo();
                    activePage.Rebuild(false);
                }
            }
        }

        public void TextInput(string input) {}
        public void KeyUp(SDL.SDL_Keysym key) {}
        public void FocusChanged(bool focused) {}
    }
}