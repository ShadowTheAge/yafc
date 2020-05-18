using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using SDL2;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class MainScreen : WindowMain, IKeyboardFocus
    {
        public static MainScreen Instance { get; private set; }
        private readonly ObjectTooltip objectTooltip = new ObjectTooltip();

        private readonly List<PseudoScreen> pseudoScreens = new List<PseudoScreen>();
        private readonly VirtualScrollList<ProjectPage> hiddenPagesView;
        private PseudoScreen topScreen;
        public readonly Project project;
        private readonly FadeDrawer fadeDrawer = new FadeDrawer();

        private ProjectPage activePage;
        private ProjectPageView activePageView;
        private uint lastSavedState;
        public string attachedFileName { get; private set; }

        private readonly Dictionary<Type, ProjectPageView> registeredPageViews = new Dictionary<Type, ProjectPageView>();

        public MainScreen(int display, Project project) : base(default)
        {
            RegisterPageView<ProductionTable>(new ProductionTableView());
            RegisterPageView<AutoPlanner>(new AutoPlannerView());
            Instance = this;
            hiddenPagesView = new VirtualScrollList<ProjectPage>(15, new Vector2(0f, 1.5f), BuildHiddenPage, collapsible:true);
            this.project = project;
            Create("Yet Another Factorio Calculator", display);
            if (project.justCreated)
            {
                ShowPseudoScreen(MilestonesPanel.Instance);
            }

            if (project.pages.Count == 0)
            {
                var firstPage = new ProjectPage(project, typeof(ProductionTable));
                project.pages.Add(firstPage);
            }

            if (project.displayPages.Count == 0)
                project.displayPages.Add(project.pages[0].guid);
            
            SetActivePage(project.FindPage(project.displayPages[0]));
            project.metaInfoChanged += ProjectOnMetaInfoChanged;
            InputSystem.Instance.SetDefaultKeyboardFocus(this);
        }

        private void BuildHiddenPage(ImGui gui, ProjectPage element, int index)
        {
            using (gui.EnterRow())
            {
                if (element.icon != null)
                    gui.BuildIcon(element.icon.icon);
                gui.RemainingRow().BuildText(element.name);
            }

            if (gui.BuildButton(gui.lastRect, SchemeColor.PureBackground, SchemeColor.Grey) == ImGuiUtils.Event.Click)
                SetActivePage(element);
        }

        private void ProjectOnMetaInfoChanged()
        {
            if (activePage != null && project.FindPage(activePage.guid) != activePage)
                SetActivePage(null);
        }

        public void SetActivePage(ProjectPage page)
        {
            if (activePageView != null)
                activePageView.SetModel(null);
            activePage?.SetActive(false);
            activePage = page;
            if (page != null)
            {
                if (!project.displayPages.Contains(page.guid))
                {
                    project.RecordUndo(true);
                    if (project.displayPages.Count > 8)
                        project.displayPages.RemoveRange(8, project.displayPages.Count-8);
                    project.displayPages.Insert(0, page.guid);
                }
                page.SetActive(true);
                activePageView = registeredPageViews[page.content.GetType()];
                activePageView.SetModel(page);
            }
            else activePageView = null;
            Rebuild();
        }

        public void RegisterPageView<T>(ProjectPageView pageView) where T : ProjectPageContents
        {
            registeredPageViews[typeof(T)] = pageView;
        }

        protected override void BuildContent(ImGui gui)
        {            
            if (pseudoScreens.Count > 0)
            {
                var top = pseudoScreens[0];
                if (gui.isBuilding)
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
                BuildHeader(gui);
                BuildPage(gui);
            }
        }

        private void BuildHeader(ImGui gui)
        {
            using (gui.EnterRow())
            {
                gui.spacing = 0f;
                if (gui.BuildButton(Icon.Menu))
                    gui.ShowDropDown(gui.lastRect, SettingsDropdown, new Padding(0f, 0f, 0f, 0.5f));
                if (gui.BuildButton(Icon.Plus))
                {
                    hiddenPagesView.data = project.pages.Where(x => !x.visible).ToArray();
                    gui.ShowDropDown(CreatePageDropdown);
                }
                var changePage = false;
                ProjectPage changePageTo = null;
                ProjectPage prevPage = null;
                for (var i = 0; i < project.displayPages.Count; i++)
                {
                    var pageGuid = project.displayPages[i];
                    var page = project.FindPage(pageGuid);
                    if (page == null) continue;
                    if (changePage && changePageTo == null)
                        changePageTo = page;
                    using (gui.EnterGroup(new Padding(0.5f, 0.2f, 0.2f, 0.5f)))
                    {
                        gui.spacing = 0.2f;
                        if (page.icon != null)
                            gui.BuildIcon(page.icon.icon);
                        else gui.AllocateRect(0f, 1.5f);
                        gui.BuildText(page.name);
                        if (gui.BuildButton(Icon.Close, size:0.8f))
                        {
                            changePageTo = prevPage;
                            changePage = true;
                            project.RecordUndo(true).displayPages.RemoveAt(i);
                            i--;
                        }
                    }

                    if (gui.DoListReordering(gui.lastRect, gui.lastRect, i, out var from))
                        project.RecordUndo(true).displayPages.MoveListElementIndex(from, i);

                    var isActive = activePage == page;
                    if (isActive && gui.isBuilding)
                        gui.DrawRectangle(new Rect(gui.lastRect.X, gui.lastRect.Bottom - 0.3f, gui.lastRect.Width, 0.3f), SchemeColor.Primary);
                    var evt = gui.BuildButton(gui.lastRect, isActive ? SchemeColor.Background : SchemeColor.BackgroundAlt,
                        isActive ? SchemeColor.Background : SchemeColor.Grey);
                    if (evt == ImGuiUtils.Event.Click)
                    {
                        if (!isActive)
                        {
                            changePage = true;
                            changePageTo = page;
                        }
                        else ProjectPageSettingsPanel.Show(page);
                    }

                    prevPage = page;

                    if (gui.width <= 0f)
                        break;
                }

                if (changePage)
                    SetActivePage(changePageTo);
            }
            if (gui.isBuilding)
                gui.DrawRectangle(gui.lastRect, SchemeColor.PureBackground);
        }

        private void BuildPage(ImGui gui)
        {
            var usedHeaderSpace = gui.statePosition.Y;
            var pageVisibleSize = size;
            pageVisibleSize.Y -= usedHeaderSpace; // remaining size minus header
            if (activePageView != null)
                activePageView.Build(gui, pageVisibleSize);
            else
            {
                if (gui.isBuilding && Database.objectsByTypeName.TryGetValue("unit.compilatron", out var compilatron))
                {
                    gui.AllocateSpacing((pageVisibleSize.Y - 3f) / 2);
                    gui.BuildIcon(compilatron.icon, 3f);
                }
            }
        }

        public T AddProjectPageAndSetActive<T>(string name, FactorioObject icon) where T : ProjectPageContents
        {
            var page = AddProjectPageAndSetActive(name, icon, typeof(T));
            return page.content as T;
        }
        
        public ProjectPage AddProjectPageAndSetActive(string name, FactorioObject icon, Type contentType)
        {
            var page = new ProjectPage(project, contentType) {name = name, icon = icon};
            project.RecordUndo().pages.Add(page);
            SetActivePage(project.pages[project.pages.Count-1]);
            return page;
        }

        private void CreatePageDropdown(ImGui gui, ref bool closed)
        {
            foreach (var (type, view) in registeredPageViews)
            {
                view.CreateModelDropdown(gui, type, project, ref closed);
            }

            if (project.hiddenPages > 0)
            {
                gui.BuildText("Hidden pages:", Font.subheader);
                hiddenPagesView.Build(gui);
            }
        }
        
        public void BuildSubHeader(ImGui gui, string text)
        {
            using (gui.EnterGroup(ObjectTooltip.contentPadding))
                gui.BuildText(text, Font.subheader);
            if (gui.isBuilding)
                gui.DrawRectangle(gui.lastRect, SchemeColor.GreyAlt);
        }

        private void SettingsDropdown(ImGui gui, ref bool closed)
        {
            gui.boxColor = SchemeColor.Background;
            if (gui.BuildContextMenuButton("Undo", "Ctrl+Z") && (closed = true))
                project.undo.PerformUndo();
            if (gui.BuildContextMenuButton("Save", "Ctrl+S") && (closed = true))
                SaveProject().CaptureException();
            if (gui.BuildContextMenuButton("Save As") && (closed = true))
                SaveProjectAs().CaptureException();
            BuildSubHeader(gui, "Tools");
            if (gui.BuildContextMenuButton("Milestones") && (closed = true))
                ShowPseudoScreen(MilestonesPanel.Instance);

            if (gui.BuildContextMenuButton("Never Enough Items Explorer") && (closed = true))
                SelectObjectPanel.Select(Database.goods.all, "Open NEIE", x => NeverEnoughItemsPanel.Show(x, null));

            if (gui.BuildContextMenuButton("Dependency Explorer") && (closed = true))
                SelectObjectPanel.Select(Database.objects.all, "Open Dependency Explorer", DependencyExplorer.Show);
            
            BuildSubHeader(gui, "Extra");

            if (gui.BuildContextMenuButton("Run Factorio"))
            {
                var factorioPath = DataUtils.dataPath + "/../bin/x64/factorio";
                var args = string.IsNullOrEmpty(DataUtils.modsPath) ? null : "--mod-directory \"" + DataUtils.modsPath + "\"";
                Process.Start(new ProcessStartInfo(factorioPath, args) {UseShellExecute = true});
                closed = true;
            }

            if (gui.BuildContextMenuButton("About YAFC") && (closed = true))
                new AboutScreen(this);
        }

        public override bool preventQuit => true;

        protected override void Close()
        {
            if (project.unsavedChangesCount > 0)
            {
                var unsavedCount = "You have " + project.unsavedChangesCount + " unsaved changes";
                if (!string.IsNullOrEmpty(project.attachedFileName))
                    unsavedCount += " to " + project.attachedFileName;
                MessageBox.Show(SaveCallback, "Save unsaved changes?", unsavedCount, "Save", "Don't save");
                return;
            }
            base.Close();
        }

        private async void SaveCallback(bool hasChoice, bool choice)
        {
            if (!hasChoice)
                return;
            if (choice)
            {
                var saved = await SaveProject();
                if (!saved)
                    return;
            } 
            base.Close();
        }

        public void ShowTooltip(IFactorioObjectWrapper obj, ImGui source, Rect sourceRect, bool extendHeader = false)
        {
            objectTooltip.SetFocus(obj, source, sourceRect, extendHeader);
            ShowTooltip(objectTooltip);
        }

        public bool ShowPseudoScreen(PseudoScreen screen)
        {
            if (topScreen == null)
            {
                Ui.DispatchInMainThread(x => fadeDrawer.CreateDownscaledImage(), null);
            }
            screen.Rebuild();
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
                    SaveProject().CaptureException();
                if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_Z)
                {
                    if ((key.mod & SDL.SDL_Keymod.KMOD_SHIFT) != 0)
                        project.undo.PerformRedo();
                    else project.undo.PerformUndo();
                    activePageView?.Rebuild(false);
                }

                if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_Y)
                {
                    project.undo.PerformRedo();
                    activePageView?.Rebuild(false);
                }
            }
        }

        private async Task<bool> SaveProjectAs()
        {
            var path = await new FilesystemScreen("Save project", "Save project as", "Save", string.IsNullOrEmpty(project.attachedFileName) ? null : Path.GetDirectoryName(project.attachedFileName),
                FilesystemScreen.Mode.SelectOrCreateFile, "project", this, null, "yafc");
            if (path != null)
            {
                project.Save(path);
                Preferences.Instance.AddProject(path, DataUtils.dataPath, DataUtils.modsPath, DataUtils.expensiveRecipes);
                return true;
            }

            return false;
        }

        private Task<bool> SaveProject()
        {
            if (!string.IsNullOrEmpty(project.attachedFileName))
            {
                project.Save(project.attachedFileName);
                return Task.FromResult(true);
            }
            return SaveProjectAs();
        }

        public void TextInput(string input) {}
        public void KeyUp(SDL.SDL_Keysym key) {}
        public void FocusChanged(bool focused) {}
        
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
    }
}