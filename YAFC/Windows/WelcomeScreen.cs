using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using YAFC.Model;
using YAFC.Parser;
using YAFC.UI;

namespace YAFC
{
    public class WelcomeScreen : WindowUtility, IProgress<(string, string)>
    {
        private bool loading;
        private string currentLoad1, currentLoad2;
        private string path = "", dataPath = "", modsPath = "";
        private bool expensive;
        private string createText;
        private bool canCreate;
        private readonly VerticalScrollCustom errorScroll;
        private string errorMessage;

        private enum EditType
        {
            Workspace, Factorio, Mods
        }

        public WelcomeScreen() : base(ImGuiUtils.DefaultScreenPadding)
        {
            var lastProject = Preferences.Instance.recentProjects.FirstOrDefault();
            SetProject(lastProject);
            errorScroll = new VerticalScrollCustom(20f, BuildError, collapsible:true);
            Create("Welcome to YAFC v"+Program.version.ToString(3), 45, null);
            IconCollection.ClearCustomIcons();
        }

        private void BuildError(ImGui gui)
        {
            gui.allocator = RectAllocator.Stretch;
            gui.BuildText(errorMessage, Font.text, color:SchemeColor.ErrorText, wrap:true);
            gui.DrawRectangle(gui.lastRect, SchemeColor.Error);
        }

        protected override void BuildContents(ImGui gui)
        {
            gui.spacing = 1.5f;
            gui.BuildText("Yet Another Factorio Calculator", Font.header, align:RectAlignment.Middle);
            if (loading)
            {
                gui.BuildText(currentLoad1, align:RectAlignment.Middle);
                gui.BuildText(currentLoad2, align:RectAlignment.Middle);
                gui.SetNextRebuild(Ui.time + 30);
            }
            else if (errorMessage != null)
            {
                errorScroll.Build(gui);
                gui.BuildText("This error is critical. Unable to load project.");
                if (gui.BuildButton("Back"))
                {
                    errorMessage = null;
                    Rebuild();
                }
            } 
            else 
            {
                BuildPathSelect(gui, ref path, "Project file location", "You can leave it empty for a new project", EditType.Workspace);
                BuildPathSelect(gui, ref dataPath, "Factorio Data location*\nIt should contain folders 'base' and 'core'",
                    "e.g. C:/Games/Steam/SteamApps/common/Factorio/data", EditType.Factorio);
                BuildPathSelect(gui, ref modsPath, "Factorio Mods location (optional)\nIt should contain file 'mod-list.json'",
                    "If you don't use separate mod folder, leave it empty", EditType.Mods);

                gui.BuildCheckBox("Expensive recipes", expensive, out expensive);
                using (gui.EnterRow())
                {
                    if (Preferences.Instance.recentProjects.Length > 1)
                    {
                        if (gui.BuildButton("Recent projects", SchemeColor.Grey))
                            gui.ShowDropDown(BuildRecentProjectsDropdown, 35f);
                    }
                    if (gui.BuildButton(Icon.Help))
                        new AboutScreen(this);
                    if (gui.RemainingRow().BuildButton(createText, active:canCreate))
                        LoadProject();
                }
            }
        }

        public void Report((string, string) value) => (currentLoad1, currentLoad2) = value;
        private bool FactorioValid(string factorio) => !string.IsNullOrEmpty(factorio) && Directory.Exists(Path.Combine(factorio, "core"));
        private bool ModsValid(string mods) => string.IsNullOrEmpty(mods) || File.Exists(Path.Combine(mods, "mod-list.json"));
        
        private void ValidateSelection()
        {
            var factorioValid = FactorioValid(dataPath);
            var modsValid = ModsValid(modsPath);
            var projectExists = File.Exists(path);

            if (projectExists)
                createText = "Load '" + Path.GetFileNameWithoutExtension(path)+"'";
            else if (path != "")
                createText = "Create '" + Path.GetFileNameWithoutExtension(path)+"'";
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
        
        private void SetProject(RecentProject project)
        {
            expensive = project.expensive;
            modsPath = project.modsPath ?? "";
            path = project.path ?? "";
            dataPath = project.dataPath ?? "";
            if (dataPath == "" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var possibleDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam/steamApps/common/Factorio/data");
                if (FactorioValid(possibleDataPath))
                    dataPath = possibleDataPath;
            }
            ValidateSelection();
            rootGui.ClearFocus();
            rootGui.Rebuild();
        }
        
        private async void LoadProject()
        {
            try
            {
                var (dataPath, modsPath, projectPath, expensiveRecipes) = (this.dataPath, this.modsPath, path, expensive);
                Preferences.Instance.AddProject(projectPath, dataPath, modsPath, expensiveRecipes);
                Preferences.Instance.Save();

                loading = true;
                rootGui.Rebuild();

                await Ui.ExitMainThread();
                var collector = new ErrorCollector();
                var project = FactorioDataSource.Parse(dataPath, modsPath, projectPath, expensiveRecipes, this, collector);
                await Ui.EnterMainThread();
                Console.WriteLine("Opening main screen");
                new MainScreen(displayIndex, project);
                if (collector.severity > ErrorSeverity.None)
                    ErrorListPanel.Show(collector);
                Close();
                GC.Collect();
            }
            catch (Exception ex)
            {
                await Ui.EnterMainThread();
                while (ex.InnerException != null)
                    ex = ex.InnerException;
                if (ex is LuaException lua)
                    errorMessage = lua.Message;
                else errorMessage = ex.Message + "\n" + ex.StackTrace;
            }
            finally
            {
                loading = false;
                rootGui.Rebuild();
            }
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
                    dataPath = result;
                else if (type == EditType.Mods)
                    modsPath = result;
                else this.path = result;
                Rebuild();
                ValidateSelection();
            }
        }
        
        private void BuildRecentProjectsDropdown(ImGui gui, ref bool closed)
        {
            gui.spacing = 0f;
            foreach (var project in Preferences.Instance.recentProjects)
            {
                if (string.IsNullOrEmpty(project.path))
                    continue;
                using (gui.EnterGroup(new Padding(0.5f, 0.25f), RectAllocator.LeftRow))
                {
                    gui.BuildIcon(Icon.Settings);
                    gui.RemainingRow(0.5f).BuildText(project.path);
                }

                if (gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.Grey) == ImGuiUtils.Event.Click)
                {
                    var owner = gui.window as WelcomeScreen;
                    owner.SetProject(project);
                    closed = true;
                }
            }
        }
    }
}