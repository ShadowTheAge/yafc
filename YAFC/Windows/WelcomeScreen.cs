using System;
using System.IO;
using System.Linq;
using YAFC.UI;

namespace YAFC
{
    public class WelcomeScreen : WindowUtility, IProgress<(string, string)>
    {
        private bool loading;
        private bool recentSelected;
        private string currentLoad1, currentLoad2;
        private string workspace, factorio, mods;
        private bool expensive;
        private string createText;
        private bool canCreate;
        
        private enum EditType
        {
            Workspace, Factorio, Mods
        }

        public WelcomeScreen() : base(ImGuiUtils.DefaultScreenPadding)
        {
            var lastProject = Preferences.Instance.recentProjects.FirstOrDefault();
            workspace = lastProject.path;
            mods = lastProject.modFolder;
            factorio = Preferences.Instance.factorioLocation;
            ValidateSelection();
            Create("Welcome to YAFC", 45, null);
        }
        
        public override void Build(ImGui gui)
        {
            gui.spacing = 1.5f;
            gui.BuildText("Yet Another Factorio Calculator", Font.header, align:RectAlignment.Middle);
            if (loading)
            {
                gui.BuildText(currentLoad1, align:RectAlignment.Middle);
                gui.BuildText(currentLoad2, align:RectAlignment.Middle);
                gui.SetNextRebuild(Ui.time + 20);
            }
            else
            {
                BuildPathSelect(gui, ref workspace, "Project file location", "You can leave it empty for a new project", EditType.Workspace);
                BuildPathSelect(gui, ref factorio, "Factorio Data location*\nIt should contain folders 'base' and 'core'",
                    "e.g. C:/Games/Steam/SteamApps/common/Factorio/data", EditType.Factorio);
                BuildPathSelect(gui, ref mods, "Factorio Mods location (optional)\nIt should contain file 'mod-list.json'",
                    "If you don't use separate mod folder, leave it empty", EditType.Mods);

                gui.BuildCheckBox("Expensive recipes", expensive, out expensive);
                using (gui.EnterRow())
                {
                    if (Preferences.Instance.recentProjects.Length > 1)
                    {
                        if (gui.BuildButton("Recent projects", SchemeColor.Grey))
                        {
                            recentSelected = true;
                        }
                        if (recentSelected)
                        {
                            //recentProjectOverlay.SetAnchor(new Vector2(gui.lastRect.X, gui.lastRect.Y), Anchor.BottomLeft);
                            //recentProjectOverlay.Build(gui);
                        }
                    }
                    gui.RemainingRow().BuildButton(createText, active:canCreate);
                }
            }
        }

        public void Report((string, string) value) => (currentLoad1, currentLoad2) = value;
        private bool FactorioValid(string factorio) => factorio != "" && Directory.Exists(Path.Combine(factorio, "core"));
        private bool ModsValid(string mods) => mods == "" || File.Exists(Path.Combine(mods, "mod-list.json"));
        
        private void ValidateSelection()
        {
            var factorioValid = FactorioValid(factorio);
            var modsValid = ModsValid(mods);
            var projectExists = File.Exists(workspace);

            if (projectExists)
                createText = "Load '" + Path.GetFileNameWithoutExtension(workspace)+"'";
            else if (workspace != "")
                createText = "Create '" + Path.GetFileNameWithoutExtension(workspace)+"'";
            else createText = "Create new project";
            canCreate = factorioValid && modsValid;
        }

        private void BuildPathSelect(ImGui gui, ref string path, string description, string placeholder, EditType editType)
        {
            gui.BuildText(description, wrap:true);
            gui.spacing = 0.5f;
            using (gui.EnterGroup(default, RectAllocator.RightRow))
            {
                if (gui.BuildButton("..."))
                    ShowFileSelect(description, path, editType);
                if (gui.RemainingRow(0f).BuildTextInput(path, out path, placeholder))
                    ValidateSelection();
            }
            gui.spacing = 1.5f;
        }

        private Func<string, bool> GetFolderFilter(EditType type)
        {
            switch (type)
            {
                case EditType.Mods: return ModsValid;
                case EditType.Factorio: return FactorioValid;
                default: return null;
            }
        }

        private async void ShowFileSelect(string description, string path, EditType type)
        {
            var result = await new FilesystemScreen("Select folder", description, type == EditType.Workspace ? "Select" : "Select folder", path,
                type == EditType.Workspace ? FilesystemScreen.Mode.SelectOrCreateFile : FilesystemScreen.Mode.SelectFolder, "", this, GetFolderFilter(type),
                type == EditType.Workspace ? "yafc" : null);
            if (result != null)
            {
                if (type == EditType.Factorio)
                    factorio = result;
                else if (type == EditType.Mods)
                    mods = result;
                else workspace = result;
                ValidateSelection();
            }
        }
    }
}