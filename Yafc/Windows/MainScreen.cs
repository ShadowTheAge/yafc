using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using SDL2;
using Yafc.Model;
using Yafc.UI;
using YAFC.Model;

namespace Yafc {
    public partial class MainScreen : WindowMain, IKeyboardFocus, IProgress<(string, string)> {
        ///<summary>Unique ID for the Summary page</summary>
        public static readonly Guid SummaryGuid = Guid.Parse("9bdea333-4be2-4be3-b708-b36a64672a40");
        public static MainScreen Instance { get; private set; } = null!; // null-forgiving: Set by the instance constructor
        private readonly ObjectTooltip objectTooltip = new ObjectTooltip();
        private readonly List<PseudoScreen> pseudoScreens = [];
        private readonly VirtualScrollList<ProjectPage> allPages;
        private readonly MainScreenTabBar tabBar;
        private readonly FadeDrawer fadeDrawer = new FadeDrawer();

        private PseudoScreen? topScreen;
        public Project project { get; private set; }
        private ProjectPage? _activePage;
        public ProjectPage? activePage => _activePage;
        public ProjectPageView? activePageView => _activePageView;
        private ProjectPageView? _activePageView;
        private ProjectPage? _secondaryPage;
        public ProjectPage? secondaryPage => _secondaryPage;
        private ProjectPageView? secondaryPageView;
        private readonly SummaryView summaryView;

        private bool analysisUpdatePending;
        private SearchQuery pageSearch;
        private readonly PageListSearch pageListSearch = new();
        private readonly ImGui searchGui;
        private Rect searchBoxRect;

        private readonly Dictionary<Type, ProjectPageView> registeredPageViews = [];
        private readonly Dictionary<Type, ProjectPageView> secondaryPageViews = [];

        public MainScreen(int display, Project project) : base(default) {
            summaryView = new SummaryView(project);
            RegisterPageView<ProductionTable>(new ProductionTableView());
            RegisterPageView<AutoPlanner>(new AutoPlannerView());
            RegisterPageView<ProductionSummary>(new ProductionSummaryView());
            RegisterPageView<Summary>(summaryView);
            searchGui = new ImGui(BuildSearch, new Padding(1f)) { boxShadow = RectangleBorder.Thin, boxColor = SchemeColor.Background };
            Instance = this;
            tabBar = new MainScreenTabBar(this);
            allPages = new VirtualScrollList<ProjectPage>(30, new Vector2(0f, 2f), BuildPage, collapsible: true);
            Create("Yet Another Factorio Calculator CE v" + YafcLib.version, display);
            SetProject(project);
        }

        [MemberNotNull(nameof(project))]
        private void SetProject(Project project) {
            if (this.project != null) {
                this.project.metaInfoChanged -= ProjectOnMetaInfoChanged;
                this.project.settings.changed -= ProjectSettingsChanged;
            }
            Project.current = project;
            DataUtils.SetupForProject(project);
            this.project = project;
            if (project.justCreated) {
                _ = ShowPseudoScreen(new MilestonesPanel());
            }

            if (project.pages.Count == 0) {
                ProjectPage firstPage = new ProjectPage(project, typeof(ProductionTable));
                project.pages.Add(firstPage);
            }

            if (project.displayPages.Count == 0) {
                project.displayPages.Add(project.pages[0].guid);
            }

            // Hack to activate all page solvers for the summary view
            foreach (var page in project.pages) {
                page.SetActive(true);
                page.SetActive(false);
            }

            SetActivePage(project.FindPage(project.displayPages[0]));
            project.metaInfoChanged += ProjectOnMetaInfoChanged;
            project.settings.changed += ProjectSettingsChanged;
            InputSystem.Instance.SetDefaultKeyboardFocus(this);
        }

        private void ProjectSettingsChanged(bool visualOnly) {
            if (visualOnly) {
                return;
            }

            if (topScreen == null) {
                ReRunAnalysis();
            }
            else {
                analysisUpdatePending = true;
            }
        }

        private void ReRunAnalysis() {
            analysisUpdatePending = false;
            ErrorCollector collector = new ErrorCollector();
            Analysis.ProcessAnalyses(this, project, collector);
            rootGui.MarkEverythingForRebuild();
            if (collector.severity > ErrorSeverity.None) {
                ErrorListPanel.Show(collector);
            }
        }

        private void BuildPage(ImGui gui, ProjectPage element, int index) {
            using (gui.EnterGroup(new Padding(1f, 0.25f), RectAllocator.LeftRow)) {
                if (element.icon != null) {
                    gui.BuildIcon(element.icon.icon);
                }

                gui.RemainingRow().BuildText(element.name, color: element.visible ? SchemeColor.BackgroundText : SchemeColor.BackgroundTextFaint);
            }
            var evt = gui.BuildButton(gui.lastRect, SchemeColor.PureBackground, SchemeColor.Grey, button: 0);
            if (evt) {
                if (gui.actionParameter == SDL.SDL_BUTTON_MIDDLE) {
                    ProjectPageSettingsPanel.Show(element);
                    dropDown?.Close();
                }
                else {
                    SetActivePage(element);
                }
            }
            else if (evt == ButtonEvent.MouseOver) {
                ShowTooltip(gui, element, true, gui.lastRect);
            }
        }

        private void ProjectOnMetaInfoChanged() {
            if (_activePage != null && project.FindPage(_activePage.guid) != _activePage) {
                SetActivePage(null);
            }
        }

        private void ChangePage(ref ProjectPage? activePage, ProjectPage? newPage, ref ProjectPageView? activePageView, ProjectPageView? newPageView) {
            activePageView?.SetModel(null);
            activePage?.SetActive(false);
            activePage = newPage;
            if (newPage != null) {
                if (!project.displayPages.Contains(newPage.guid)) {
                    _ = project.RecordUndo(true);
                    project.displayPages.Insert(0, newPage.guid);
                }
                newPage.SetActive(true);
                newPageView ??= registeredPageViews[newPage.content.GetType()];
                activePageView = newPageView;
                activePageView.SetModel(newPage);
                activePageView.SetSearchQuery(pageSearch);
            }
            else {
                activePageView = null;
            }

            Rebuild();
        }

        public void SetActivePage(ProjectPage? page) {
            if (page != null && _secondaryPage == page) {
                SetSecondaryPage(null);
            }

            ChangePage(ref _activePage, page, ref _activePageView, null);
        }

        public void SetSecondaryPage(ProjectPage? page) {
            if (page == null || page == _activePage) {
                ChangePage(ref _secondaryPage, null, ref secondaryPageView, null);
            }
            else {
                var contentType = page.content.GetType();
                if (!secondaryPageViews.TryGetValue(contentType, out var view)) {
                    view = secondaryPageViews[contentType] = registeredPageViews[contentType].CreateSecondaryView();
                }

                ChangePage(ref _secondaryPage, page, ref secondaryPageView, view);
            }
        }

        public void RegisterPageView<T>(ProjectPageView pageView) where T : ProjectPageContents => registeredPageViews[typeof(T)] = pageView;

        public void RebuildProjectView() {
            rootGui.MarkEverythingForRebuild();
            _activePageView?.headerContent.MarkEverythingForRebuild();
            _activePageView?.bodyContent.MarkEverythingForRebuild();
            secondaryPageView?.headerContent.MarkEverythingForRebuild();
            secondaryPageView?.bodyContent.MarkEverythingForRebuild();
        }

        protected override void BuildContent(ImGui gui) {
            if (pseudoScreens.Count > 0) {
                var top = pseudoScreens[0];
                if (gui.isBuilding) {
                    gui.DrawRenderable(new Rect(default, size), fadeDrawer, SchemeColor.None);
                }

                if (top != topScreen) {
                    topScreen = top;
                    InputSystem.Instance.SetDefaultKeyboardFocus(top);
                }
                top.Build(gui, size);
            }
            else {
                if (topScreen != null) {
                    project.undo.Resume();
                    InputSystem.Instance.SetDefaultKeyboardFocus(this);
                    topScreen = null;
                    if (analysisUpdatePending) {
                        ReRunAnalysis();
                    }
                }
                BuildTabBar(gui);
                BuildPage(gui);
            }
        }

        /// <summary>
        /// Draws the tab bar across the top of the window, including the pancake menu, add page button, and search pages dropdown.
        /// </summary>
        private void BuildTabBar(ImGui gui) {
            using (gui.EnterRow()) {
                gui.spacing = 0f;
                if (gui.BuildButton(Icon.Menu)) {
                    gui.ShowDropDown(gui.lastRect, SettingsDropdown, new Padding(0f, 0f, 0f, 0.5f));
                }

                if (gui.BuildButton(Icon.Plus)) {
                    gui.ShowDropDown(gui.lastRect, CreatePageDropdown, new Padding(0f, 0f, 0f, 0.5f));
                }

                gui.allocator = RectAllocator.RightRow;
                var spaceForDropdown = gui.AllocateRect(2.1f, 2.1f);
                tabBar.Build(gui);
                if (gui.isBuilding) {
                    gui.DrawIcon(spaceForDropdown.Expand(-0.3f), Icon.DropDown, SchemeColor.BackgroundText);
                }

                if (gui.BuildButton(spaceForDropdown, SchemeColor.None, SchemeColor.Grey)) {
                    updatePageList();
                    ShowDropDown(gui, spaceForDropdown, missingPagesDropdown, new Padding(0f, 0f, 0f, 0.5f), 30f);
                }
            }
            if (gui.isBuilding) {
                gui.DrawRectangle(gui.lastRect, SchemeColor.PureBackground);
            }

            void updatePageList() {
                List<ProjectPage> sortedAndFilteredPageList = pageListSearch.Search(project.pages).ToList();
                sortedAndFilteredPageList.Sort((a, b) => a.visible == b.visible ? string.Compare(a.name, b.name, StringComparison.InvariantCultureIgnoreCase) : a.visible ? -1 : 1);
                allPages.data = sortedAndFilteredPageList;
            }

            void missingPagesDropdown(ImGui gui) {
                pageListSearch.Build(gui, updatePageList);
                allPages.Build(gui);
            }
        }

        private void BuildPage(ImGui gui) {
            float usedHeaderSpace = gui.statePosition.Y;
            var pageVisibleSize = size;
            pageVisibleSize.Y -= usedHeaderSpace; // remaining size minus header
            if (_activePageView != null) {
                if (secondaryPageView != null) {
                    var visibleSize = pageVisibleSize;
                    visibleSize.Y /= 2f;
                    _activePageView.Build(gui, visibleSize);
                    secondaryPageView.Build(gui, visibleSize);
                }
                else {
                    _activePageView.Build(gui, pageVisibleSize);
                }

                if (pageSearch.query != null && gui.isBuilding) {
                    var searchSize = searchGui.CalculateState(30, gui.pixelsPerUnit);
                    gui.DrawPanel(new Rect(pageVisibleSize.X - searchSize.X, usedHeaderSpace, searchSize.X, searchSize.Y), searchGui);
                }
            }
            else {
                if (gui.isBuilding && Database.objectsByTypeName.TryGetValue("Entity.compilatron", out var compilatron)) {
                    gui.AllocateSpacing((pageVisibleSize.Y - 3f) / 2);
                    gui.BuildIcon(compilatron.icon, 3f);
                }
            }
        }

        public ProjectPage AddProjectPage(string name, FactorioObject? icon, Type contentType, bool setActive, bool initNew) {
            ProjectPage page = new ProjectPage(project, contentType) { name = name, icon = icon };
            if (initNew) {
                page.content.InitNew();
            }

            project.RecordUndo().pages.Add(page);
            if (setActive) {
                SetActivePage(page);
            }

            return page;
        }

        private void CreatePageDropdown(ImGui gui) {
            foreach (var (type, view) in registeredPageViews) {
                view.CreateModelDropdown(gui, type, project);
            }

            if (SDL.SDL_HasClipboardText() == SDL.SDL_bool.SDL_TRUE) {
                gui.AllocateSpacing();
                if (gui.BuildContextMenuButton("Import page from clipboard") && gui.CloseDropdown()) {
                    ProjectPageSettingsPanel.LoadProjectPageFromClipboard();
                }
            }
        }

        public void BuildSubHeader(ImGui gui, string text) {
            using (gui.EnterGroup(ObjectTooltip.contentPadding)) {
                gui.BuildText(text, Font.subheader);
            }

            if (gui.isBuilding) {
                gui.DrawRectangle(gui.lastRect, SchemeColor.GreyAlt);
            }
        }

        private void ShowNeie() => SelectSingleObjectPanel.Select(Database.goods.all, "Open NEIE", NeverEnoughItemsPanel.Show);

        private void SetSearch(SearchQuery searchQuery) {
            pageSearch = searchQuery;
            _activePageView?.SetSearchQuery(searchQuery);
            secondaryPageView?.SetSearchQuery(searchQuery);
            Rebuild();
        }

        private void ShowSearch() {
            SetSearch(new SearchQuery(""));
            if (searchBoxRect != default) {
                searchGui.SetTextInputFocus(searchBoxRect, "");
            }
        }

        private void BuildSearch(ImGui gui) {
            gui.BuildText("Find on page:");
            gui.AllocateSpacing();
            gui.allocator = RectAllocator.RightRow;
            if (gui.BuildButton(Icon.Close)) {
                SetSearch(default);
                return;
            }
            if (gui.BuildSearchBox(pageSearch, out pageSearch)) {
                SetSearch(pageSearch);
            }

            if (searchBoxRect == default) {
                gui.SetTextInputFocus(gui.lastRect, pageSearch.query);
            }

            searchBoxRect = gui.lastRect;
        }

        private void SettingsDropdown(ImGui gui) {
            gui.boxColor = SchemeColor.Background;
            if (gui.BuildContextMenuButton("Undo", "Ctrl+" + ImGuiUtils.ScanToString(SDL.SDL_Scancode.SDL_SCANCODE_Z)) && gui.CloseDropdown()) {
                project.undo.PerformUndo();
            }

            if (gui.BuildContextMenuButton("Save", "Ctrl+" + ImGuiUtils.ScanToString(SDL.SDL_Scancode.SDL_SCANCODE_S)) && gui.CloseDropdown()) {
                SaveProject().CaptureException();
            }

            if (gui.BuildContextMenuButton("Save As") && gui.CloseDropdown()) {
                SaveProjectAs().CaptureException();
            }

            if (gui.BuildContextMenuButton("Find on page", "Ctrl+" + ImGuiUtils.ScanToString(SDL.SDL_Scancode.SDL_SCANCODE_F)) && gui.CloseDropdown()) {
                ShowSearch();
            }

            if (gui.BuildContextMenuButton("Load another project (Same mods)") && gui.CloseDropdown()) {
                LoadProjectLight();
            }

            if (gui.BuildContextMenuButton("Return to starting screen") && gui.CloseDropdown()) {
                LoadProjectHeavy();
            }

            BuildSubHeader(gui, "Tools");
            if (gui.BuildContextMenuButton("Milestones") && gui.CloseDropdown()) {
                _ = ShowPseudoScreen(new MilestonesPanel());
            }

            if (gui.BuildContextMenuButton("Preferences") && gui.CloseDropdown()) {
                PreferencesScreen.Show();
            }

            if (gui.BuildContextMenuButton("Summary") && gui.CloseDropdown()) {
                ShowSummaryTab();
            }

            if (gui.BuildContextMenuButton("Never Enough Items Explorer", "Ctrl+" + ImGuiUtils.ScanToString(SDL.SDL_Scancode.SDL_SCANCODE_N)) && gui.CloseDropdown()) {
                ShowNeie();
            }

            if (gui.BuildContextMenuButton("Dependency Explorer") && gui.CloseDropdown()) {
                SelectSingleObjectPanel.Select(Database.objects.all, "Open Dependency Explorer", DependencyExplorer.Show);
            }

            BuildSubHeader(gui, "Extra");

            if (gui.BuildContextMenuButton("Run Factorio")) {
                string factorioPath = DataUtils.dataPath + "/../bin/x64/factorio";
                string? args = string.IsNullOrEmpty(DataUtils.modsPath) ? null : "--mod-directory \"" + DataUtils.modsPath + "\"";
                _ = Process.Start(new ProcessStartInfo(factorioPath, args!) { UseShellExecute = true }); // null-forgiving: ProcessStartInfo permits null args.
                _ = gui.CloseDropdown();
            }

            if (gui.BuildContextMenuButton("Check for updates") && gui.CloseDropdown()) {
                DoCheckForUpdates();
            }

            if (gui.BuildContextMenuButton("About YAFC") && gui.CloseDropdown()) {
                _ = new AboutScreen(this);
            }
        }

        private bool saveConfirmationActive;
        public override bool preventQuit => true;

        protected override async void Close() {
            if (!saveConfirmationActive && project.unsavedChangesCount > 0 && !await ConfirmUnsavedChanges()) {
                return;
            }

            ForceClose();
        }

        public void ForceClose() {
            base.Close();
        }

        private async Task<bool> ConfirmUnsavedChanges() {
            string unsavedCount = "You have " + project.unsavedChangesCount + " unsaved changes";
            if (!string.IsNullOrEmpty(project.attachedFileName)) {
                unsavedCount += " to " + project.attachedFileName;
            }

            saveConfirmationActive = true;
            var (hasChoice, choice) = await MessageBox.Show("Save unsaved changes?", unsavedCount, "Save", "Don't save");
            saveConfirmationActive = false;
            if (!hasChoice) {
                return false;
            }

            if (choice) {
                bool saved = await SaveProject();
                if (!saved) {
                    return false;
                }
            }

            return true;
        }

        private class GithubReleaseInfo {
            public string html_url { get; set; } = null!; // null-forgiving: Set by Deserialize
            public string tag_name { get; set; } = null!; // null-forgiving: Set by Deserialize
        }
        private async void DoCheckForUpdates() {
            try {
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "YAFC-CE (check for updates)");
                string result = await client.GetStringAsync(new Uri("https://api.github.com/repos/have-fun-was-taken/yafc-ce/releases/latest"));
                var release = JsonSerializer.Deserialize<GithubReleaseInfo>(result)!;
                string version = release.tag_name.StartsWith("v", StringComparison.Ordinal) ? release.tag_name[1..] : release.tag_name;
                if (new Version(version) > YafcLib.version) {
                    var (_, answer) = await MessageBox.Show("New version available!", "There is a new version available: " + release.tag_name, "Visit release page", "Close");
                    if (answer) {
                        Ui.VisitLink(release.html_url);
                    }

                    return;
                }
                MessageBox.Show("No newer version", "You are running the latest version!", "Ok");
            }
            catch (Exception) {
                MessageBox.Show((hasAnswer, answer) => {
                    if (answer) {
                        Ui.VisitLink(AboutScreen.Github + "/releases");
                    }
                }, "Network error", "There were an error while checking versions.", "Open releases url", "Close");
            }
        }

        public void ShowTooltip(IFactorioObjectWrapper obj, ImGui source, Rect sourceRect, bool extendHeader = false) {
            objectTooltip.SetFocus(obj, source, sourceRect, extendHeader);
            ShowTooltip(objectTooltip);
        }

        public bool ShowPseudoScreen(PseudoScreen screen) {
            if (topScreen == null) {
                Ui.DispatchInMainThread(x => fadeDrawer.CreateDownscaledImage(), null);
            }
            project.undo.Suspend();
            screen.Rebuild();
            pseudoScreens.Insert(0, screen);
            screen.Open();
            rootGui.Rebuild();
            return true;
        }

        public void ClosePseudoScreen(PseudoScreen screen) {
            _ = pseudoScreens.Remove(screen);
            if (pseudoScreens.Count > 0) {
                pseudoScreens[^1].Activated();
            }

            rootGui.Rebuild();
        }

        public void ClosePage(Guid page) => _ = project.RecordUndo(true).displayPages.Remove(page);

        public void ShowSummaryTab() {

            ProjectPage? summaryPage = project.FindPage(SummaryGuid);
            if (summaryPage == null) {

                summaryPage = new ProjectPage(project, typeof(Summary), false, SummaryGuid) {
                    name = "Summary",
                };
                project.pages.Add(summaryPage);
            }

            SetActivePage(summaryPage);
        }

        public bool KeyDown(SDL.SDL_Keysym key) {
            bool ctrl = (key.mod & SDL.SDL_Keymod.KMOD_CTRL) != 0;
            if (ctrl) {
                if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_S) {
                    SaveProject().CaptureException();
                }
                else if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_Z) {
                    if ((key.mod & SDL.SDL_Keymod.KMOD_SHIFT) != 0) {
                        project.undo.PerformRedo();
                    }
                    else {
                        project.undo.PerformUndo();
                    }

                    _activePageView?.Rebuild(false);
                    secondaryPageView?.Rebuild(false);
                }
                else if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_Y) {
                    project.undo.PerformRedo();
                    _activePageView?.Rebuild(false);
                    secondaryPageView?.Rebuild(false);
                }
                else if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_N) {
                    ShowNeie();
                }
                else if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_F) {
                    ShowSearch();
                }
                else {
                    if (_activePageView?.ControlKey(key.scancode) != true) {
                        _ = (secondaryPageView?.ControlKey(key.scancode));
                    }
                }
            }

            if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE && pageSearch.query != null) {
                SetSearch(default);
            }

            return true;
        }

        private async Task<bool> SaveProjectAs() {
            string? path = await new FilesystemScreen("Save project", "Save project as", "Save", string.IsNullOrEmpty(project.attachedFileName) ? null : Path.GetDirectoryName(project.attachedFileName),
                FilesystemScreen.Mode.SelectOrCreateFile, "project", this, null, "yafc");
            if (path != null) {
                project.Save(path);
                Preferences.Instance.AddProject(path, DataUtils.dataPath, DataUtils.modsPath, DataUtils.expensiveRecipes, DataUtils.netProduction);
                return true;
            }

            return false;
        }

        private Task<bool> SaveProject() {
            if (!string.IsNullOrEmpty(project.attachedFileName)) {
                project.Save(project.attachedFileName);
                return Task.FromResult(true);
            }

            return SaveProjectAs();
        }

        private async void LoadProjectLight() {
            if (project.unsavedChangesCount > 0 && !await ConfirmUnsavedChanges()) {
                return;
            }

            string? path = await new FilesystemScreen("Load project", "Load another .yafc project", "Select",
                string.IsNullOrEmpty(project.attachedFileName) ? null : Path.GetDirectoryName(project.attachedFileName), FilesystemScreen.Mode.SelectOrCreateFile, "project", this,
                null, "yafc");
            if (path == null) {
                return;
            }

            ErrorCollector errors = new ErrorCollector();
            try {
                Project project = Project.ReadFromFile(path, errors);
                Analysis.ProcessAnalyses(this, project, errors);
                SetProject(project);
            }
            catch (Exception ex) {
                errors.Exception(ex, "Critical loading exception", ErrorSeverity.Important);
            }
            if (errors.severity != ErrorSeverity.None) {
                ErrorListPanel.Show(errors);
            }
        }

        private async void LoadProjectHeavy() {
            if (project.unsavedChangesCount > 0 && !await ConfirmUnsavedChanges()) {
                return;
            }

            SetActivePage(null);
            _ = new WelcomeScreen();
            ForceClose();
        }

        public bool TextInput(string input) => true;

        public bool KeyUp(SDL.SDL_Keysym key) => true;

        public void FocusChanged(bool focused) { }

        private class FadeDrawer : IRenderable {
            private SDL.SDL_Rect srcRect;
            private TextureHandle blurredFade;

            public void CreateDownscaledImage() {
                nint renderer = Instance.surface!.renderer; // null-forgiving: surface has been set earlier in the render loop.
                blurredFade = blurredFade.Destroy();
                var texture = Instance.surface.BeginRenderToTexture(out var size);
                Instance.MainRender();
                Instance.surface.EndRenderToTexture();
                for (int i = 0; i < 2; i++) {
                    SDL.SDL_Rect halfSize = new SDL.SDL_Rect() { w = size.w / 2, h = size.h / 2 };
                    var halfTexture = Instance.surface.CreateTexture(SDL.SDL_PIXELFORMAT_RGBA8888, (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_TARGET, halfSize.w, halfSize.h);
                    _ = SDL.SDL_SetRenderTarget(renderer, halfTexture.handle);
                    var bgColor = SchemeColor.PureBackground.ToSdlColor();
                    _ = SDL.SDL_SetRenderDrawColor(renderer, bgColor.r, bgColor.g, bgColor.b, bgColor.a);
                    _ = SDL.SDL_RenderClear(renderer);
                    _ = SDL.SDL_SetTextureBlendMode(texture.handle, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
                    _ = SDL.SDL_SetTextureAlphaMod(texture.handle, 120);
                    _ = SDL.SDL_RenderCopy(renderer, texture.handle, ref size, ref halfSize);
                    _ = texture.Destroy();
                    texture = halfTexture;
                    size = halfSize;
                }
                _ = SDL.SDL_SetRenderTarget(renderer, IntPtr.Zero);
                srcRect = size;
                blurredFade = texture;
            }

            public void Render(DrawingSurface surface, SDL.SDL_Rect position, SDL.SDL_Color color) {
                if (blurredFade.valid) {
                    _ = SDL.SDL_RenderCopy(surface.renderer, blurredFade.handle, ref srcRect, ref position);
                }
            }
        }

        public void Report((string, string) value) => Console.WriteLine(value); // TODO

        public bool IsSameObjectHovered(ImGui gui, FactorioObject? obj) => objectTooltip.IsSameObjectHovered(gui, obj);

        public void ShowTooltip(ImGui gui, ProjectPage page, bool isMiddleEdit, Rect rect) {
            if (page == null || !registeredPageViews.TryGetValue(page.content.GetType(), out var pageView)) {
                return;
            }

            ShowTooltip(gui, rect, x => {
                pageView.BuildPageTooltip(x, page.content);
                if (isMiddleEdit) {
                    x.BuildText("Middle mouse button to edit", Font.text, true, color: SchemeColor.BackgroundTextFaint);
                }
            });
        }
    }
}
