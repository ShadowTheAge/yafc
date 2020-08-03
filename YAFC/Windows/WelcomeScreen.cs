using System;
using System.Collections.Generic;
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
        private bool splitFluidsByTemperature = true;
        private string createText;
        private bool canCreate;
        private readonly VerticalScrollCustom errorScroll;
        private readonly VerticalScrollCustom recentProjectScroll;
        private string errorMessage;
        private bool closeRecentProjects;

        private static Dictionary<string, string> languageMapping = new Dictionary<string, string>()
        {
            {"en", "English"},
            {"ca", "Catalan"},
            {"cs", "Czech"},
            {"da", "Danish"},
            {"nl", "Dutch"},
            {"de", "German"},
            {"fi", "Finnish"},
            {"fr", "French"},
            {"hu", "Hungarian"},
            {"it", "Italian"},
            {"pl", "Polish"},
            {"pt-BR", "Portuguese (Brasilian)"},
            {"ru", "Russian"},
            {"es-ES", "Spanish"},
            {"tr", "Turkish"},
            {"uk", "Ukrainian"},
        };

        private enum EditType
        {
            Workspace, Factorio, Mods
        }

        public WelcomeScreen() : base(ImGuiUtils.DefaultScreenPadding)
        {
            RenderingUtils.SetColorScheme(Preferences.Instance.darkMode);
            var lastProject = Preferences.Instance.recentProjects.FirstOrDefault();
            SetProject(lastProject);
            errorScroll = new VerticalScrollCustom(20f, BuildError, collapsible:true);
            recentProjectScroll = new VerticalScrollCustom(20f, BuildRecentProjectList, collapsible:true);
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

                using (gui.EnterRow())
                {
                    gui.BuildCheckBox("Split fluids by temperature", splitFluidsByTemperature, out splitFluidsByTemperature);
                }

                using (gui.EnterRow())
                {
                    gui.BuildCheckBox("Expensive recipes", expensive, out expensive);
                    gui.allocator = RectAllocator.RightRow;
                    var lang = Preferences.Instance.language;
                    if (languageMapping.TryGetValue(Preferences.Instance.language, out var mapped))
                        lang = mapped;
                    if (gui.BuildLink(lang))
                        gui.ShowDropDown(LanguageSelection);
                    gui.BuildText("In-game objects language:");
                }
                
                using (gui.EnterRow())
                {
                    if (Preferences.Instance.recentProjects.Length > 1)
                    {
                        if (gui.BuildButton("Recent projects", SchemeColor.Grey))
                            gui.ShowDropDown(BuildRecentProjectsDropdown, 35f);
                    }
                    if (gui.BuildButton(Icon.Help))
                        new AboutScreen(this);
                    if (gui.BuildButton(Icon.DarkMode))
                    {
                        Preferences.Instance.darkMode = !Preferences.Instance.darkMode;
                        RenderingUtils.SetColorScheme(Preferences.Instance.darkMode);
                        Preferences.Instance.Save();
                    }
                    if (gui.RemainingRow().BuildButton(createText, active:canCreate))
                        LoadProject();
                }
            }
        }

        private void LanguageSelection(ImGui gui, ref bool closed)
        {
            gui.spacing = 0f;
            gui.allocator = RectAllocator.LeftAlign;
            gui.BuildText("Only languages with more than 90% translation support and with 'european' glyphs are shown. Mods may not support your language, using English as a fallback.", wrap:true);
            gui.AllocateSpacing(0.5f);
            foreach (var (k, v) in languageMapping)
            {
                if (gui.BuildLink(v))
                {
                    Preferences.Instance.language = k;
                    Preferences.Instance.Save();
                    closed = true;
                }
            }
            gui.AllocateSpacing(0.5f);
            gui.BuildText("If your language is missing visit");
            if (gui.BuildLink("this link for a workaround"))
                AboutScreen.VisitLink("https://github.com/ShadowTheAge/yafc/blob/master/Docs/MoreLanguagesSupport.md");
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
            splitFluidsByTemperature = project.splitFluidsByTemperature;
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
                Preferences.Instance.AddProject(projectPath, dataPath, modsPath, expensiveRecipes, splitFluidsByTemperature);
                Preferences.Instance.Save();

                loading = true;
                rootGui.Rebuild();

                await Ui.ExitMainThread();
                var collector = new ErrorCollector();
                var project = FactorioDataSource.Parse(dataPath, modsPath, projectPath, expensiveRecipes, splitFluidsByTemperature, this, collector, Preferences.Instance.language);
                await Ui.EnterMainThread();
                Console.WriteLine("Opening main screen");
                new MainScreen(displayIndex, project);
                if (collector.severity > ErrorSeverity.None)
                    ErrorListPanel.Show(collector);
                Close();
                GC.Collect();
                Console.WriteLine("GC: " + GC.GetTotalMemory(false));
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
            closeRecentProjects = false;
            recentProjectScroll.Build(gui);
            closed = closeRecentProjects;
        }

        private void BuildRecentProjectList(ImGui gui)
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
                    closeRecentProjects = true;
                }
            }
        }
    }
}