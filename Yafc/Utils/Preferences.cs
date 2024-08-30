using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Yafc.Model;

namespace Yafc {
    public class Preferences {
        public static readonly Preferences Instance;
        public static readonly string appDataFolder;
        private static readonly string fileName;

        public static readonly string autosaveFilename;

        static Preferences() {
            appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                appDataFolder = Path.Combine(appDataFolder, "YAFC");
            }

            if (!string.IsNullOrEmpty(appDataFolder) && !Directory.Exists(appDataFolder)) {
                _ = Directory.CreateDirectory(appDataFolder);
            }

            autosaveFilename = Path.Combine(appDataFolder, "autosave.yafc");
            fileName = Path.Combine(appDataFolder, "yafc.config");
            if (File.Exists(fileName)) {
                try {
                    Instance = JsonSerializer.Deserialize<Preferences>(File.ReadAllBytes(fileName))!;
                    return;
                }
                catch (Exception ex) {
                    Console.Error.WriteException(ex);
                }
            }
            Instance = new Preferences();
        }

        public void Save() {
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(this, JsonUtils.DefaultOptions);
            File.WriteAllBytes(fileName, data);
        }
        public ProjectDefinition[] recentProjects { get; set; } = [];
        public bool darkMode { get; set; }
        public string language { get; set; } = "en";
        public string? overrideFont { get; set; }
        /// <summary>
        /// Whether or not the main screen should be created maximized.
        /// </summary>
        public bool maximizeMainScreen { get; set; }
        /// <summary>
        /// The initial width of the main screen or the width the main screen will be after being restored, depending on whether it starts restored or maximized.
        /// </summary>
        public float initialMainScreenWidth { get; set; }
        /// <summary>
        /// The initial height of the main screen or the height the main screen will be after being restored, depending on whether it starts restored or maximized.
        /// </summary>
        public float initialMainScreenHeight { get; set; }
        public float recipeColumnWidth { get; set; }
        public float ingredientsColumWidth { get; set; }
        public float productsColumWidth { get; set; }
        public float modulesColumnWidth { get; set; }
        /// <summary>
        /// Set to always use a software renderer even where we'd normally use a hardware renderer if you know that we make bad decisions about hardware renderers for your system.
        /// 
        /// The current known cases in which you should do this are:
        /// - Your system has a very old graphics card that is not supported by Windows DX12
        /// </summary>
        public bool forceSoftwareRenderer { get; set; } = false;

        public void AddProject(string path, string dataPath, string modsPath, bool expensiveRecipes, bool netProduction) {
            recentProjects = recentProjects.Where(x => string.Compare(path, x.path, StringComparison.InvariantCultureIgnoreCase) != 0)
                .Prepend(new ProjectDefinition(path, dataPath, modsPath, expensiveRecipes, netProduction))
                .ToArray();
            Save();
        }
    }

    public class ProjectDefinition {
        public ProjectDefinition() {
            path = "";
            dataPath = "";
            modsPath = "";
            expensive = false;
            netProduction = false;
        }

        public ProjectDefinition(string path, string dataPath, string modsPath, bool expensive, bool netProduction) {
            this.path = path;
            this.dataPath = dataPath;
            this.modsPath = modsPath;
            this.expensive = expensive;
            this.netProduction = netProduction;
        }

        public string path { get; set; }
        public string dataPath { get; set; }
        public string modsPath { get; set; }
        public bool expensive { get; set; }
        /// <summary>
        /// If <see langword="true"/>, the recipe-selection windows will only display the recipes that provide net-production or consumption of the <see cref="Goods"/> in question.<br/>
        /// If <see langword="false"/>, the recipe-selection windows will show all recipes that produce or consume any quantity of that <see cref="Goods"/>.<br/>
        /// For example, the Kovarex enrichment will appear for both production and consumption of both U-235 and U-238 when <see langword="false"/>,
        /// but will appear as only producing U-235 and consuming U-238 when <see langword="true"/>.
        /// </summary>
        public bool netProduction { get; set; }
    }
}
