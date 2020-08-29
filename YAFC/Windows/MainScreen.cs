using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using SDL2;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class MainScreen : WindowMain, IKeyboardFocus, IProgress<(string, string)>
    {
        public static MainScreen Instance { get; private set; }
        private readonly ObjectTooltip objectTooltip = new ObjectTooltip();
        private readonly List<PseudoScreen> pseudoScreens = new List<PseudoScreen>();
        private readonly VirtualScrollList<ProjectPage> allPages;
        private readonly MainScreenTabBar tabBar;
        private readonly FadeDrawer fadeDrawer = new FadeDrawer();

        private PseudoScreen topScreen;
        public Project project { get; private set; }
        public ProjectPage activePage { get; private set; }
        private ProjectPageView activePageView;
        private bool analysisUpdatePending;
        private SearchQuery pageSearch;
        private SearchQuery pageListSearch;
        private readonly List<ProjectPage> sortedAndFilteredPageList = new List<ProjectPage>();
        private readonly ImGui searchGui;
        private Rect searchBoxRect;

        private readonly Dictionary<Type, ProjectPageView> registeredPageViews = new Dictionary<Type, ProjectPageView>();

        public MainScreen(int display, Project project) : base(default)
        {
            RegisterPageView<ProductionTable>(new ProductionTableView());
            RegisterPageView<AutoPlanner>(new AutoPlannerView());
            searchGui = new ImGui(BuildSearch, new Padding(1f)) {boxShadow = RectangleBorder.Thin, boxColor = SchemeColor.Background};
            Instance = this;
            tabBar = new MainScreenTabBar(this);
            allPages = new VirtualScrollList<ProjectPage>(30, new Vector2(0f, 2f), BuildPage, collapsible:true);
            Create("Yet Another Factorio Calculator v"+Program.version, display);
            SetProject(project);
        }

        private void SetProject(Project project)
        {
            if (this.project != null)
            {
                this.project.metaInfoChanged -= ProjectOnMetaInfoChanged;
                this.project.settings.changed -= ProjectSettingsChanged;
            }
            Project.current = project;
            this.project = project;
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
            project.settings.changed += ProjectSettingsChanged;
            InputSystem.Instance.SetDefaultKeyboardFocus(this);
        }

        private void ProjectSettingsChanged(bool visualOnly)
        {
            if (visualOnly)
                return;
            if (topScreen == null)
                ReRunAnalysis();
            else analysisUpdatePending = true;
        }

        private void ReRunAnalysis()
        {
            analysisUpdatePending = false;
            var collector = new ErrorCollector();
            Analysis.ProcessAnalyses(this, project, collector);
            rootGui.MarkEverythingForRebuild();
            if (collector.severity > ErrorSeverity.None)
                ErrorListPanel.Show(collector);
        }

        private void BuildPage(ImGui gui, ProjectPage element, int index)
        {
            using (gui.EnterGroup(new Padding(1f, 0.25f), RectAllocator.LeftRow))
            {
                if (element.icon != null)
                    gui.BuildIcon(element.icon.icon);
                gui.RemainingRow().BuildText(element.name, color:element.visible ? SchemeColor.BackgroundText : SchemeColor.BackgroundTextFaint);
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
                    project.displayPages.Insert(0, page.guid);
                }
                page.SetActive(true);
                activePageView = registeredPageViews[page.content.GetType()];
                activePageView.SetModel(page);
                activePageView.SetSearchQuery(pageSearch);
            }
            else activePageView = null;
            Rebuild();
        }

        public void RegisterPageView<T>(ProjectPageView pageView) where T : ProjectPageContents
        {
            registeredPageViews[typeof(T)] = pageView;
        }
        
        public void RebuildProjectView()
        {
            rootGui.MarkEverythingForRebuild();
            activePageView.headerContent.MarkEverythingForRebuild();
            activePageView.bodyContent.MarkEverythingForRebuild();
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
                    project.undo.Resume();
                    InputSystem.Instance.SetDefaultKeyboardFocus(this);
                    topScreen = null;
                    if (analysisUpdatePending)
                        ReRunAnalysis();
                }
                BuildHeader(gui);
                BuildPage(gui);
            }
        }

        private void UpdatePageList()
        {
            sortedAndFilteredPageList.Clear();
            foreach (var page in project.pages)
            {
                if (pageListSearch.Match(page.name))
                    sortedAndFilteredPageList.Add(page);
            }
            sortedAndFilteredPageList.Sort((a, b) => a.visible == b.visible ? string.Compare(a.name, b.name, StringComparison.InvariantCultureIgnoreCase) : a.visible ? -1 : 1);
            allPages.data = sortedAndFilteredPageList;
        }

        private void BuildHeader(ImGui gui)
        {
            using (gui.EnterRow())
            {
                gui.spacing = 0f;
                if (gui.BuildButton(Icon.Menu))
                    gui.ShowDropDown(gui.lastRect, SettingsDropdown, new Padding(0f, 0f, 0f, 0.5f));
                if (gui.BuildButton(Icon.Plus))
                    gui.ShowDropDown(gui.lastRect, CreatePageDropdown, new Padding(0f, 0f, 0f, 0.5f));

                gui.allocator = RectAllocator.RightRow;
                var spaceForDropdown = gui.AllocateRect(2.1f, 2.1f);
                tabBar.Build(gui);
                if (project.hiddenPages > 0 || tabBar.maxScroll > 0f)
                {
                    if (gui.isBuilding)
                        gui.DrawIcon(spaceForDropdown.Expand(-0.3f), Icon.DropDown, SchemeColor.BackgroundText);
                    if (gui.BuildButton(spaceForDropdown, SchemeColor.None, SchemeColor.Grey) == ImGuiUtils.Event.Click)
                    {
                        UpdatePageList();
                        ShowDropDown(gui, spaceForDropdown, MissingPagesDropdown, new Padding(0f, 0f, 0f, 0.5f), 30f);
                    }
                }
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
            {
                activePageView.Build(gui, pageVisibleSize);
                if (pageSearch.query != null && gui.isBuilding)
                {
                    var searchSize = searchGui.CalculateState(30, gui.pixelsPerUnit);
                    gui.DrawPanel(new Rect(pageVisibleSize.X-searchSize.X, usedHeaderSpace, searchSize.X, searchSize.Y), searchGui);
                }
            }
            else
            {
                if (gui.isBuilding && Database.objectsByTypeName.TryGetValue("Entity.compilatron", out var compilatron))
                {
                    gui.AllocateSpacing((pageVisibleSize.Y - 3f) / 2);
                    gui.BuildIcon(compilatron.icon, 3f);
                }
            }
        }
        
        public ProjectPage AddProjectPage(string name, FactorioObject icon, Type contentType, bool setActive)
        {
            var page = new ProjectPage(project, contentType) {name = name, icon = icon};
            project.RecordUndo().pages.Add(page);
            if (setActive)
                SetActivePage(page);
            return page;
        }

        private void CreatePageDropdown(ImGui gui, ref bool closed)
        {
            foreach (var (type, view) in registeredPageViews)
                view.CreateModelDropdown(gui, type, project, ref closed);
            if (SDL.SDL_HasClipboardText() == SDL.SDL_bool.SDL_TRUE)
            {
                gui.AllocateSpacing();
                if (gui.BuildContextMenuButton("Import page from clipboard") && (closed = true))
                    ProjectPageSettingsPanel.LoadProjectPageFromClipboard();
            }
        }

        private void MissingPagesDropdown(ImGui gui, ref bool closed)
        {
            using (gui.EnterGroup(new Padding(1f)))
            {
                if (gui.BuildSearchBox(pageListSearch, out pageListSearch))
                    UpdatePageList();
            }
            allPages.Build(gui);
        }
        
        public void BuildSubHeader(ImGui gui, string text)
        {
            using (gui.EnterGroup(ObjectTooltip.contentPadding))
                gui.BuildText(text, Font.subheader);
            if (gui.isBuilding)
                gui.DrawRectangle(gui.lastRect, SchemeColor.GreyAlt);
        }

        private void ShowNeie()
        {
            SelectObjectPanel.Select(Database.goods.all, "Open NEIE", NeverEnoughItemsPanel.Show);
        }

        private void SetSearch(SearchQuery searchQuery)
        {
            pageSearch = searchQuery;
            activePageView?.SetSearchQuery(searchQuery);
            Rebuild();
        }

        private void ShowSearch()
        {
            SetSearch(new SearchQuery(""));
            if (searchBoxRect != default)
                searchGui.SetTextInputFocus(searchBoxRect, "");
        }

        private void BuildSearch(ImGui gui)
        {
            gui.BuildText("Find on page:");
            gui.AllocateSpacing();
            gui.allocator = RectAllocator.RightRow;
            if (gui.BuildButton(Icon.Close))
            {
                SetSearch(default);
                return;
            }
            if (gui.BuildSearchBox(pageSearch, out pageSearch))
                SetSearch(pageSearch);

            if (searchBoxRect == default)
                gui.SetTextInputFocus(gui.lastRect, pageSearch.query);
            searchBoxRect = gui.lastRect;
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
            if (gui.BuildContextMenuButton("Find on page", "Ctrl+F") && (closed = true))
                ShowSearch();
            if (gui.BuildContextMenuButton("Load another project (Same mods)") && (closed = true))
                LoadProjectLight();
            if (gui.BuildContextMenuButton("Return to starting screen"))
                LoadProjectHeavy();
            BuildSubHeader(gui, "Tools");
            if (gui.BuildContextMenuButton("Milestones") && (closed = true))
                ShowPseudoScreen(MilestonesPanel.Instance);

            if (gui.BuildContextMenuButton("Preferences") && (closed = true))
                PreferencesScreen.Show();

            if (gui.BuildContextMenuButton("Never Enough Items Explorer", "Ctrl+N") && (closed = true))
                ShowNeie();

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
            
            if (gui.BuildContextMenuButton("Check for updates") && (closed = true))
                DoCheckForUpdates();

            if (gui.BuildContextMenuButton("About YAFC") && (closed = true))
                new AboutScreen(this);
        }

        private bool saveConfirmationActive;
        public override bool preventQuit => true;

        protected override async void Close()
        {
            if (!saveConfirmationActive && project.unsavedChangesCount > 0 && !await ConfirmUnsavedChanges())
                return;
            Instance = null;
            base.Close();
        }

        private async Task<bool> ConfirmUnsavedChanges()
        {
            var unsavedCount = "You have " + project.unsavedChangesCount + " unsaved changes";
            if (!string.IsNullOrEmpty(project.attachedFileName))
                unsavedCount += " to " + project.attachedFileName;
            saveConfirmationActive = true;
            var (hasChoice, choice) = await MessageBox.Show("Save unsaved changes?", unsavedCount, "Save", "Don't save");
            saveConfirmationActive = false;
            if (!hasChoice)
                return false;
            if (choice)
            {
                var saved = await SaveProject();
                if (!saved)
                    return false;
            }

            return true;
        }

        private class GithubReleaseInfo
        {
            public string html_url { get; set; }
            public string tag_name { get; set; }
        }
        private async void DoCheckForUpdates()
        {
            try
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "YAFC (check for updates)");
                var result = await client.GetStringAsync(new Uri("https://api.github.com/repos/ShadowTheAge/yafc/releases/latest"));
                var release = JsonSerializer.Deserialize<GithubReleaseInfo>(result);
                var version = release.tag_name.StartsWith("v", StringComparison.Ordinal) ? release.tag_name.Substring(1) : release.tag_name;
                if (new Version(version) > Program.version)
                {
                    var (_, answer) = await MessageBox.Show("New version availible!", "There is a new version availible: " + release.tag_name, "Visit release page", "Close");
                    if (answer)
                        AboutScreen.VisitLink(release.html_url);
                    return;
                }
                MessageBox.Show("No newer version", "You are running the latest version!", "Ok");
            }
            catch (Exception)
            {
                MessageBox.Show((hasAnswer, answer) =>
                {
                    if (answer)
                        AboutScreen.VisitLink("https://github.com/ShadowTheAge/yafc/releases");
                }, "Network error", "There were an error while checking versions.", "Open releases url", "Close");
            }
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
            project.undo.Suspend();
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
                else if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_Z)
                {
                    if ((key.mod & SDL.SDL_Keymod.KMOD_SHIFT) != 0)
                        project.undo.PerformRedo();
                    else project.undo.PerformUndo();
                    activePageView?.Rebuild(false);
                } else if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_Y)
                {
                    project.undo.PerformRedo();
                    activePageView?.Rebuild(false);
                } else if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_N)
                    ShowNeie();
                else if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_F)
                    ShowSearch();
                else activePageView?.ControlKey(key.scancode);
            }

            if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE && pageSearch.query != null)
                SetSearch(default);
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
        
        private async void LoadProjectLight()
        {
            if (project.unsavedChangesCount > 0 && !await ConfirmUnsavedChanges())
                return;
            var path = await new FilesystemScreen("Load project", "Load another .yafc project", "Select",
                string.IsNullOrEmpty(project.attachedFileName) ? null : Path.GetDirectoryName(project.attachedFileName), FilesystemScreen.Mode.SelectOrCreateFile, "project", this,
                null, "yafc");
            if (path == null)
                return;
            var errors = new ErrorCollector();
            try
            {
                var project = Project.ReadFromFile(path, errors);
                Analysis.ProcessAnalyses(this, project, errors);
                SetProject(project);
            }
            catch (Exception ex)
            {
                errors.Exception(ex, "Critical loading exception", ErrorSeverity.Important);
            }
            if (errors.severity != ErrorSeverity.None)
                ErrorListPanel.Show(errors);
        }

        private async void LoadProjectHeavy()
        {
            if (project.unsavedChangesCount > 0 && !await ConfirmUnsavedChanges())
                return;
            SetActivePage(null);
            new WelcomeScreen();
            Close();
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

        public void Report((string, string) value)
        {
            Console.WriteLine(value); // TODO
        }

        public bool IsSameObjectHovered(ImGui gui, FactorioObject obj) => objectTooltip.IsSameObjectHovered(gui, obj);
    }
}