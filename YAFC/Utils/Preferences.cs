using System;
using System.IO;
using System.Text.Json;
using YAFC.Parser;

namespace YAFC
{
    public class Preferences
    {
        public static readonly Preferences Instance;
        public static readonly string appDataFolder;
        private static readonly string fileName;
        public static readonly JsonSerializerOptions serializerOptions = new JsonSerializerOptions {WriteIndented = true};

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
            var data = JsonSerializer.SerializeToUtf8Bytes(this, serializerOptions);
            File.WriteAllBytes(fileName, data);
        }


        public string factorioLocation;
        public RecentProject[] recentProjects = Array.Empty<RecentProject>();
    }

    public struct RecentProject
    {
        public string path;
        public string modFolder;
    }
}