using System;
using System.IO;
using System.Linq;
using System.Numerics;
using YAFC.Parser;
using YAFC.UI;

namespace YAFC
{
    public class WelcomeScreen : WindowUtility, IProgress<(string, string)>
    {
        private readonly FontString header;
        private readonly PathSelect workspace;
        private readonly PathSelect factorio;
        private readonly PathSelect mods;
        private readonly TextButton create;
        private readonly TextButton recentButton;
        private readonly CheckBox expensive;
        private readonly FontString loadingLine1;
        private readonly FontString loadingLine2;
        private readonly RecentProjectOverlay recentProjectOverlay;
        
        private bool loading;
        private bool recentSelected;
        private string currentLoad1, currentLoad2;

        public WelcomeScreen()
        {
            header = new FontString(Font.header, "Yet Another Factorio Calculator", align:RectAlignment.Middle);

            var lastProject = Preferences.Instance.recentProjects.FirstOrDefault();

            workspace = new PathSelect("Project file location", "You can leave it empty for a new project", lastProject.path, ValidateSelection, x => true,
                FilesystemScreen.Mode.SelectOrCreateFile) {extension = "yafc"};
            factorio = new PathSelect("Factorio Data location*\nIt should contain folders 'base' and 'core'", "e.g. C:/Games/Steam/SteamApps/common/Factorio/data",
                Preferences.Instance.factorioLocation, ValidateSelection, x => Directory.Exists(Path.Combine(x, "core")), FilesystemScreen.Mode.SelectFolder);
            mods = new PathSelect("Factorio Mods location (optional)\nIt should contain file 'mod-list.json'", "If you don't use separate mod folder, leave it empty",
                lastProject.modFolder, ValidateSelection, x => File.Exists(Path.Combine(x, "mod-list.json")), FilesystemScreen.Mode.SelectFolder);
            create = new TextButton(Font.text, "Create new project", LoadProject);
            recentButton = new TextButton(Font.text, "Recent projects", RecentClick, SchemeColor.Grey);
            recentProjectOverlay = new RecentProjectOverlay();
            
            expensive = new CheckBox(Font.text, "Expensive recipes");
            loadingLine1 = new FontString(Font.text, align:RectAlignment.Middle);
            loadingLine2 = new FontString(Font.text, align:RectAlignment.Middle);

            ValidateSelection();
            factorio.path = Preferences.Instance.factorioLocation;
            Create("Welcome to YAFC", 45, null);
        }

        private void RecentClick(UiBatch obj)
        {
            recentSelected = !recentSelected;
            Rebuild();
        }

        public void Report((string, string) value) => (currentLoad1, currentLoad2) = value;

        private void ValidateSelection()
        {
            var factorioValid = factorio.path != "" && Directory.Exists(Path.Combine(factorio.path, "core"));
            var modsValid = mods.path == "" || File.Exists(Path.Combine(mods.path, "mod-list.json"));
            var projectExists = File.Exists(workspace.path);

            if (projectExists)
                create.text = "Load '" + Path.GetFileNameWithoutExtension(workspace.path)+"'";
            else if (workspace.path != "")
                create.text = "Create '" + Path.GetFileNameWithoutExtension(workspace.path)+"'";
            else create.text = "Create new project";

            create.interactable = factorioValid && modsValid;
        }

        private async void LoadProject(UiBatch batch)
        {
            try
            {
                var (factorioPath, modsPath, projectPath, expensiveRecipes) = (factorio.path, mods.path, workspace.path, expensive.check);
                Preferences.Instance.factorioLocation = factorioPath;
                Preferences.Instance.Save();

                loading = true;
                Rebuild();

                await Ui.ExitMainThread();
                FactorioDataSource.Parse(factorioPath, modsPath, expensiveRecipes, this);
                await Ui.EnterMainThread();
                
                if (workspace.path != "")
                    Preferences.Instance.AddProject(projectPath, modsPath, expensiveRecipes);
                
                new MainScreen(displayIndex);
                Close();
            }
            finally
            {
                await Ui.EnterMainThread();
                loading = false;
                Rebuild();
            }
        }

        protected override void BuildContent(LayoutState state)
        {
            state.spacing = 1.5f;
            state.Build(header);
            if (loading)
            {
                loadingLine1.text = currentLoad1;
                loadingLine2.text = currentLoad2;
                state.Build(loadingLine1).Build(loadingLine2);
                state.batch.SetNextRebuild(Ui.time + 20);
            }
            else
            {
                state.Build(workspace).Build(factorio).Build(mods).Build(expensive);
                using (state.EnterRow())
                {
                    if (Preferences.Instance.recentProjects.Length > 1)
                    {
                        state.Build(recentButton);
                        if (recentSelected)
                        {
                            recentProjectOverlay.SetAnchor(new Vector2(state.lastRect.X, state.lastRect.Y), Anchor.BottomLeft);
                            recentProjectOverlay.Build(state);
                        }
                    }
                    state.BuildRemaining(create);
                }
            }
        }
        
        private void SetProject(RecentProject data)
        {
            expensive.check = data.expensive;
            mods.path = data.modFolder;
            workspace.path = data.path;
            RecentClick(null);
            ValidateSelection();
        }

        private class RecentProjectOverlay : ManualAnchorPanel
        {
            private readonly SimpleList<RecentProject, RecentProjectView> recentProjectList;

            public RecentProjectOverlay() : base(35f)
            {
                recentProjectList = new SimpleList<RecentProject, RecentProjectView>();
                recentProjectList.data = Preferences.Instance.recentProjects;
            }
            
            protected override void BuildContent(LayoutState state)
            {
                state.Build(recentProjectList);
            }
        }

        private class RecentProjectView : SelectableElement<RecentProject>
        {
            private FontString text;

            public RecentProjectView()
            {
                text = new FontString(Font.text);
            }
            protected override void BuildContent(LayoutState state)
            {
                state.allocator = RectAllocator.LeftRow;
                var rect = state.AllocateRect(1f, 1f, RectAlignment.Middle);
                state.batch.DrawIcon(rect, Icon.Settings, SchemeColor.BackgroundText);
                state.spacing = 0.5f;
                state.allocator = RectAllocator.RemainigRow;
                text.BuildElement(data.path, state);
            }

            public override void Click(UiBatch batch)
            {
                var owner = batch.FindOwner<WelcomeScreen>();
                owner.SetProject(data);
            }
        }

        private class PathSelect : IWidget
        {
            private readonly FontString description;
            private readonly InputField location;
            private readonly TextButton dots;
            public string extension;
            private readonly Func<string, bool> filter;
            private readonly FilesystemScreen.Mode mode;

            public string path
            {
                get => location.text;
                set => location.text = value;
            }

            public PathSelect(string description, string empty, string initial, Action change, Func<string, bool> filter, FilesystemScreen.Mode mode)
            {
                this.description = new FontString(Font.text, description, true);
                this.filter = filter;
                this.mode = mode;
                location = new InputField(Font.text) {placeholder = empty, text = initial, onChange = change};
                dots = new TextButton(Font.text, "...", OpenEditing);
            }

            public void Build(LayoutState state)
            {
                state.Build(description);
                using (state.EnterGroup(default, RectAllocator.RightRow, 0.5f))
                {
                    state.Build(dots).BuildRemaining(location, 0f);
                }
            }
            
            private async void OpenEditing(UiBatch batch)
            {
                var result = await new FilesystemScreen("Select folder", description.text, mode == FilesystemScreen.Mode.SelectFolder ? "Select folder" : "Select", location.text, mode, "", batch.window, filter, extension);
                if (result != null)
                    location.text = result;
            }
        }
    }
}