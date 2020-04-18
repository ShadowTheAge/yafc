using System;
using System.ComponentModel.Design;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SDL2;
using YAFC.Parser;
using YAFC.UI;

namespace YAFC
{
    public class WelcomeScreen : Window, IProgress<(string, string)>
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
        private readonly FontString recentProjectHeader;
        private readonly VirtualScrollList<RecentProject, RecentProjectView> recentProjectList;
        
        private bool loading;
        private bool recentSelected;
        private string currentLoad1, currentLoad2;

        public WelcomeScreen()
        {
            header = new FontString(Font.header, "Yet Another Factorio Calculator", align:RectAlignment.Middle);

            var lastProject = Preferences.Instance.recentProjects.FirstOrDefault();

            workspace = new PathSelect("Project file location", "You can leave it empty for a new project", lastProject.path, ValidateSelection, x => true,
                FilesystemPanel.Mode.SelectOrCreateFile) {extension = "yafc"};
            factorio = new PathSelect("Factorio Data location*\nIt should contain folders 'base' and 'core'", "e.g. C:/Games/Steam/SteamApps/common/Factorio/data",
                Preferences.Instance.factorioLocation, ValidateSelection, x => Directory.Exists(Path.Combine(x, "core")), FilesystemPanel.Mode.SelectFolder);
            mods = new PathSelect("Factorio Mods location (optional)\nIt should contain file 'mod-list.json'", "If you don't use separate mod folder, leave it empty",
                lastProject.modFolder, ValidateSelection, x => File.Exists(Path.Combine(x, "mod-list.json")), FilesystemPanel.Mode.SelectFolder);
            create = new TextButton(Font.text, "Create new project", LoadProject);
            recentButton = new TextButton(Font.text, "Recent projects", RecentClick, SchemeColor.Grey);
            
            expensive = new CheckBox(Font.text, "Expensive recipes");
            loadingLine1 = new FontString(Font.text, align:RectAlignment.Middle);
            loadingLine2 = new FontString(Font.text, align:RectAlignment.Middle);
            recentProjectHeader = new FontString(Font.subheader, "Recent projects:", align:RectAlignment.Middle);
            recentProjectList = new VirtualScrollList<RecentProject, RecentProjectView>(new SizeF(10, 15.5f), 1);
            recentProjectList.data = Preferences.Instance.recentProjects;

            ValidateSelection();
            factorio.path = Preferences.Instance.factorioLocation;
            Create("Welcome to YAFC", 45, true, null);
        }

        private void RecentClick(UiBatch obj)
        {
            recentSelected = !recentSelected;
            recentButton.text = recentSelected ? "Create new or load other" : "Recent projects";
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
            else if (recentSelected)
            {
                state.Build(recentProjectHeader).Build(recentProjectList).Build(recentButton);
            }
            else
            {
                state.Build(workspace).Build(factorio).Build(mods).Build(expensive);
                using (state.EnterRow())
                {
                    state.Build(recentButton).Align(RectAllocator.RemainigRow).Build(create);
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

        protected override void Close()
        {
            base.Close();
            Ui.Quit();
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
                var rect = state.AllocateRect(1f, 1f, RectAlignment.Middle);
                state.batch.DrawIcon(rect, Icon.Settings, SchemeColor.BackgroundText);
                state.AllocateSpacing(0.5f);
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
            private readonly FilesystemPanel.Mode mode;

            public string path
            {
                get => location.text;
                set => location.text = path;
            }

            public PathSelect(string description, string empty, string initial, Action change, Func<string, bool> filter, FilesystemPanel.Mode mode)
            {
                this.description = new FontString(Font.text, description, true);
                this.filter = filter;
                this.mode = mode;
                location = new InputField(Font.text) {placeholder = empty, text = initial, onChange = change};
                dots = new TextButton(Font.text, "...", OpenEditing);
            }

            public void Build(LayoutState state)
            {
                state.Build(description, 0.5f);
                using (state.EnterGroup(default, RectAllocator.RightRow))
                {
                    state.Build(dots, 0f).Align(RectAllocator.RemainigRow).Build(location);
                }
            }
            
            private async void OpenEditing(UiBatch batch)
            {
                var result = await new FilesystemPanel("Select folder", description.text, mode == FilesystemPanel.Mode.SelectFolder ? "Select folder" : "Select", location.text, mode, "", batch.window, filter, extension);
                if (result != null)
                    location.text = result;
            }
        }
    }
}