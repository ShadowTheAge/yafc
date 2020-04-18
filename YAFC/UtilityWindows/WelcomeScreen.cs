using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SDL2;
using YAFC.Parser;
using YAFC.UI;

namespace YAFC
{
    public class WelcomeScreen : Window
    {
        public FontString header;
        private PathSelect workspace;
        private PathSelect factorio;
        private PathSelect mods;
        private TextButton create;

        private CheckBox expensive;

        public WelcomeScreen()
        {
            header = new FontString(Font.header, "Yet Another Factorio Calculator", align:RectAlignment.Middle);

            var lastProject = Preferences.Instance.recentProjects.FirstOrDefault();

            workspace = new PathSelect("Project file location", "You can leave it empty for a new project", lastProject.path, ValidateSelection, x => true,
                FilesystemPanel.Mode.SelectOrCreateFile) {extension = "yafc"};
            factorio = new PathSelect("Factorio Data location*\nIt should contain folders 'base' and 'core'", "e.g. C:/Games/Steam/SteamApps/common/Factorio/data",
                Preferences.Instance.factorioLocation, ValidateSelection, x => Directory.Exists(Path.Combine(x, "core")), FilesystemPanel.Mode.SelectFolder);
            mods = new PathSelect("Factorio Mods location (optional)\nIt should contain file 'mod-list.json'", "If you don't use separate mod folder, leave it empty",
                lastProject.modFolder ?? Preferences.Instance.modsLocation, ValidateSelection, x => File.Exists(Path.Combine(x, "mod-list.json")), FilesystemPanel.Mode.SelectFolder);
            create = new TextButton(Font.text, "Create new project", LoadProject);
            
            expensive = new CheckBox(Font.text, "Expensive recipes");

            ValidateSelection();
            factorio.path = Preferences.Instance.factorioLocation;
            Create("Welcome to YAFC", 45, true, null);
        }

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
            var (factorioPath, modsPath, projectPath, expensiveRecipes) = (factorio.path, mods.path, workspace.path, expensive.check);
            Preferences.Instance.factorioLocation = factorioPath;
            Preferences.Instance.modsLocation = modsPath;
            
            await Ui.ExitMainThread();
            FactorioDataSource.Parse(factorioPath, modsPath, expensiveRecipes);
            await Ui.EnterMainThread();

            if (workspace.path != "")
            {
                Preferences.Instance.AddProject(projectPath, modsPath, expensiveRecipes);
            }
            Preferences.Instance.Save();
        }

        protected override void BuildContent(LayoutState state)
        {
            state.spacing = 1.5f;
            state.Build(header).Build(workspace).Build(factorio).Build(mods).Build(expensive).Build(create);
        }

        protected override void Close()
        {
            base.Close();
            Ui.Quit();
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