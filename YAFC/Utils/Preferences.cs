using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using YAFC.Model;
using YAFC.Parser;

namespace YAFC
{
    public class Preferences
    {
        public static readonly Preferences Instance;
        public static readonly string appDataFolder;
        private static readonly string fileName;

        static Preferences()
        {
            appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "YAFC");
            if (!Directory.Exists(appDataFolder))
                Directory.CreateDirectory(appDataFolder);
            
            fileName = Path.Combine(appDataFolder, "yafc.config");
            if (File.Exists(fileName))
            {
                try
                {
                    Instance = JsonSerializer.Deserialize<Preferences>(File.ReadAllBytes(fileName));
                    return;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteException(ex);
                }
            }
            Instance = new Preferences();
        }

        public void Save()
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(this, JsonUtils.DefaultOptions);
            File.WriteAllBytes(fileName, data);
        }
        public RecentProject[] recentProjects { get; set; } = Array.Empty<RecentProject>();

        public void AddProject(string path, string dataPath, string modsPath, bool expensiveRecipes)
        {
            recentProjects = recentProjects.Where(x => string.Compare(path, x.path, StringComparison.InvariantCultureIgnoreCase) != 0).Take(9)
                .Prepend(new RecentProject {path = path, modsPath = modsPath, dataPath = dataPath, expensive = expensiveRecipes}).ToArray();
            Save();
        }
    }

    public struct RecentProject
    {
        public string path { get; set; }
        public string dataPath { get; set; }
        public string modsPath { get; set; }
        public bool expensive { get; set; }
    }
}