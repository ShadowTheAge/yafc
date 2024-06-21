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

        public void AddProject(string path, string dataPath, string modsPath, bool expensiveRecipes, bool netProduction) {
            recentProjects = recentProjects.Where(x => string.Compare(path, x.path, StringComparison.InvariantCultureIgnoreCase) != 0)
                .Prepend(new ProjectDefinition { path = path, modsPath = modsPath, dataPath = dataPath, expensive = expensiveRecipes, netProduction = netProduction }).ToArray();
            Save();
        }
    }

    public class ProjectDefinition {
        public string? path { get; set; }
        public string? dataPath { get; set; }
        public string? modsPath { get; set; }
        public bool expensive { get; set; }
        /// <summary>
        /// If <see langword="true"/>, recipe selection windows will only display recipes that provide net production or consumption of the <see cref="Goods"/> in question.
        /// If <see langword="false"/>, recipe selection windows will show all recipes that produce or consume any quantity of that <see cref="Goods"/>.<br/>
        /// For example, Kovarex enrichment will appear for both production and consumption of both U-235 and U-238 when <see langword="false"/>,
        /// but will appear as only producing U-235 and consuming U-238 when <see langword="true"/>.
        /// </summary>
        public bool netProduction { get; set; }
    }
}
