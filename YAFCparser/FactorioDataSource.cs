using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using YAFC.Model;

namespace YAFC.Parser
{
    public static class FactorioDataSource
    {
        internal static Dictionary<string, ModInfo> allMods = new Dictionary<string, ModInfo>();

        private static string ReadAllText(this Stream stream, int length)
        {
            var reader = new BinaryReader(stream);
            var bytes = reader.ReadBytes(length);
            stream.Dispose();
            return Encoding.UTF8.GetString(bytes);
        }

        private static readonly char[] fileSplittersLua = {'.', '/', '\\'};
        private static readonly char[] fileSplittersNormal = {'/', '\\'};

        public static (string mod, string path) ResolveModPath(string currentMod, string fullPath, bool isLuaRequire = false)
        {
            var mod = currentMod;
            var path = fullPath.Split(isLuaRequire ? fileSplittersLua : fileSplittersNormal, StringSplitOptions.RemoveEmptyEntries);
            if (Array.IndexOf(path, "..") >= 0)
                throw new InvalidOperationException("Attempt to traverse to parent directory");
            var pathEnumerable = (IEnumerable<string>) path;
            if (path[0].StartsWith("__") && path[0].EndsWith("__"))
            {
                mod = path[0].Substring(2, path[0].Length - 4);
                pathEnumerable = pathEnumerable.Skip(1);
            }

            var resolved = string.Join("/", pathEnumerable);
            if (isLuaRequire)
                resolved += ".lua";
            return (mod, resolved);
        }

        public static bool ModPathExists(string modName, string path)
        {
            var info = allMods[modName];
            if (info.zipArchive != null)
                return info.zipArchive.GetEntry(info.folder + path) != null;
            var fileName = Path.Combine(info.folder, path);
            return File.Exists(fileName);
        }

        public static byte[] ReadModFile(string modName, string path)
        {
            var info = allMods[modName];
            if (info.zipArchive != null)
            {
                var entry = info.zipArchive.GetEntry(info.folder + path);
                if (entry == null)
                    return null;
                var bytearr = new byte[entry.Length];
                using (var stream = entry.Open())
                {
                    var read = 0;
                    while (read < bytearr.Length)
                        read += stream.Read(bytearr, read, bytearr.Length - read);
                }

                return bytearr;
            }

            var fileName = Path.Combine(info.folder, path);
            return File.Exists(fileName) ? File.ReadAllBytes(fileName) : null;
        }

        private static void LoadMods(string directory, IProgress<(string, string)> progress)
        {
            foreach (var entry in Directory.EnumerateDirectories(directory))
            {
                var infoFile = Path.Combine(entry, "info.json");
                if (File.Exists(infoFile))
                {
                    progress.Report(("Initializing", entry));
                    var info = JsonSerializer.Deserialize<ModInfo>(File.ReadAllText(infoFile));
                    if (!string.IsNullOrEmpty(info.name) && allMods.ContainsKey(info.name))
                    {
                        info.folder = entry;
                        allMods[info.name] = info;
                        var localeDirName = Path.Combine(entry, "locale/en");
                        if (Directory.Exists(localeDirName))
                        {
                            foreach (var file in Directory.EnumerateFiles(localeDirName))
                            {
                                if (file.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase))
                                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                                        FactorioLocalization.Parse(fs);
                            }
                        }
                    }
                }
            }

            foreach (var fileName in Directory.EnumerateFiles(directory))
            {
                if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                    var zipArchive = new ZipArchive(fileStream);
                    var infoEntry = zipArchive.Entries.FirstOrDefault(x =>
                        x.Name.Equals("info.json", StringComparison.OrdinalIgnoreCase) &&
                        x.FullName.IndexOf('/') == x.FullName.Length - "info.json".Length - 1);
                    if (infoEntry != null)
                    {
                        var info = JsonSerializer.Deserialize<ModInfo>(infoEntry.Open().ReadAllText((int) infoEntry.Length));
                        if (!string.IsNullOrEmpty(info.name) && allMods.ContainsKey(info.name))
                        {
                            info.folder = infoEntry.FullName.Substring(0, infoEntry.FullName.Length - "info.json".Length);
                            info.zipArchive = zipArchive;
                            allMods[info.name] = info;
                            var localeDirName = info.folder + "locale/en";
                            foreach (var entry in zipArchive.Entries.Where(x =>
                                x.FullName.StartsWith(localeDirName) && x.FullName.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase)))
                            {
                                using (var fs = entry.Open())
                                    FactorioLocalization.Parse(fs);
                            }
                        }
                    }
                }
            }
        }

        public static Project Parse(string factorioPath, string modPath, string projectPath, bool expensive, IProgress<(string, string)> progress, ErrorCollector errorCollector)
        {
            var modSettingsPath = Path.Combine(modPath, "mod-settings.dat");
            progress.Report(("Initializing", "Loading mod settings"));

            progress.Report(("Initializing", "Loading mod list"));
            var modListPath = Path.Combine(modPath, "mod-list.json");
            if (File.Exists(modListPath))
            {
                var mods = JsonSerializer.Deserialize<ModList>(File.ReadAllText(modListPath));
                allMods = mods.mods.Where(x => x.enabled).Select(x => x.name).ToDictionary(x => x, x => (ModInfo) null);
            }
            else
                allMods = new Dictionary<string, ModInfo> {{"base", null}};

            allMods["core"] = null;
            Console.WriteLine("Mod list parsed");

            LoadMods(factorioPath, progress);
            if (modPath != factorioPath && modPath != "")
                LoadMods(modPath, progress);

            foreach (var (_, mod) in allMods)
                mod.ParseDependencies();
            var modsToDisable = new List<string>();
            do
            {
                modsToDisable.Clear();
                foreach (var (name, mod) in allMods)
                {
                    if (!mod.CheckDependencies(allMods))
                        modsToDisable.Add(name);
                }

                foreach (var mod in modsToDisable)
                    allMods.Remove(mod);
            } while (modsToDisable.Count > 0);

            foreach (var mod in allMods)
                if (mod.Value == null)
                    throw new NotSupportedException("Mod not found: " + mod.Key);
            progress.Report(("Initializing", "Creating Lua context"));
            var factorioVersion = allMods.TryGetValue("base", out var baseMod) ? baseMod.version : "0.18.0";

            var modsToLoad = allMods.Keys.ToHashSet();
            var modLoadOrder = new string[modsToLoad.Count];
            modLoadOrder[0] = "core";
            modsToLoad.Remove("core");
            var index = 1;
            while (modsToLoad.Count > 0)
            {
                ModInfo bestNextMod = null;
                var bestLoadWeight = (int.MaxValue, 0, 0);
                foreach (var modName in modsToLoad)
                {
                    var mod = allMods[modName];
                    var modLoadWeight = mod.GetLoadWeight(allMods, modsToLoad);
                    var compare = modLoadWeight.CompareTo(bestLoadWeight);
                    if (compare < 0 || (compare == 0 && string.Compare(mod.name, bestNextMod.name, StringComparison.Ordinal) < 0))
                    {
                        bestNextMod = mod;
                        bestLoadWeight = modLoadWeight;
                    }
                }

                modLoadOrder[index++] = bestNextMod.name;
                modsToLoad.Remove(bestNextMod.name);
            }

            Console.WriteLine("All mods found! Loading order: " + string.Join(", ", modLoadOrder));

            var preprocess = File.ReadAllBytes("Data/Sandbox.lua");
            var postprocess = File.ReadAllBytes("Data/Postprocess.lua");
            DataUtils.allMods = modLoadOrder;
            DataUtils.dataPath = factorioPath;
            DataUtils.modsPath = modPath;
            DataUtils.expensiveRecipes = expensive;

            using (var dataContext = new LuaContext())
            {
                object settings;
                if (File.Exists(modSettingsPath))
                {
                    using (var fs = new FileStream(Path.Combine(modPath, "mod-settings.dat"), FileMode.Open, FileAccess.Read))
                    {
                        settings = FactorioPropertyTree.ReadModSettings(new BinaryReader(fs), dataContext);
                    }

                    Console.WriteLine("Mod settings parsed");
                }
                else settings = dataContext.NewTable();

                // TODO default mod settings
                dataContext.SetGlobal("settings", settings);

                dataContext.Exec(preprocess, preprocess.Length, "*", "pre");
                dataContext.DoModFiles(modLoadOrder, "data.lua", progress);
                dataContext.DoModFiles(modLoadOrder, "data-updates.lua", progress);
                dataContext.DoModFiles(modLoadOrder, "data-final-fixes.lua", progress);
                dataContext.Exec(postprocess, postprocess.Length, "*", "post");

                var deserializer = new FactorioDataDeserializer(expensive, new Version(factorioVersion));
                var project = deserializer.LoadData(projectPath, dataContext.data, progress, errorCollector);
                Console.WriteLine("Completed!");
                progress.Report(("Completed!", ""));
                return project;
            }
        }

        internal class ModEntry
        {
            public string name { get; set; }
            public bool enabled { get; set; }
        }

        internal class ModList
        {
            public ModEntry[] mods { get; set; }
        }

        internal class ModInfo : IComparable<ModInfo>
        {
            private static readonly string[] defaultDependencies = {"core", "base"};
            private static readonly Regex dependencyRegex = new Regex("^\\(?([?!]?)\\)?\\s*([\\w- ]+?)[\\s\\d.><=]*$");
            public string name { get; set; }
            public string version { get; set; }
            public string[] dependencies { get; set; } = defaultDependencies;
            private (string mod, bool optional)[] parsedDependencies;

            public ZipArchive zipArchive;
            public string folder;

            public void ParseDependencies()
            {
                var dependencyList = new List<(string mod, bool optional)>();
                for (var i = 0; i < dependencies.Length; i++)
                {
                    var match = dependencyRegex.Match(dependencies[i]);
                    if (match.Success)
                    {
                        var modifier = match.Groups[1].Value;
                        if (modifier == "!")
                            continue;
                        dependencyList.Add((match.Groups[2].Value, modifier == "?"));
                    }
                }

                parsedDependencies = dependencyList.ToArray();
            }

            public int DependencyStrength(ModInfo other)
            {
                foreach (var dependency in parsedDependencies)
                {
                    if (dependency.mod == other.name)
                        return dependency.optional ? 1 : 2;
                }

                return 0;
            }

            public int CompareTo(ModInfo other)
            {
                if (name == "core")
                    return -1;
                if (other.name == "core")
                    return 1;
                var str0 = DependencyStrength(other);
                var str1 = other.DependencyStrength(this);
                if (str0 != str1)
                    return str0 - str1;
                if (dependencies.Length != other.dependencies.Length)
                    return dependencies.Length - other.dependencies.Length;
                return string.CompareOrdinal(name, other.name);
            }

            public bool CheckDependencies(Dictionary<string, ModInfo> allMods)
            {
                foreach (var dependency in parsedDependencies)
                {
                    if (!dependency.optional && !allMods.ContainsKey(dependency.mod))
                        return false;
                }

                return true;
            }

            public (int req, int opt, int deps) GetLoadWeight(Dictionary<string, ModInfo> mods, HashSet<string> nonLoadedMods)
            {
                int reqDepsNotLoaded = 0, optDepsNotLoaded = 0, depsCount = 0;
                foreach (var dep in parsedDependencies)
                {
                    if (nonLoadedMods.Contains(dep.mod))
                    {
                        if (dep.optional)
                            optDepsNotLoaded++;
                        else reqDepsNotLoaded++;
                        ++depsCount;
                    }
                    else if (mods.ContainsKey(dep.mod))
                        ++depsCount;
                }

                return (reqDepsNotLoaded, optDepsNotLoaded, depsCount);
            }
        }

        public static IEnumerable<string> GetAllModFiles(string mod, string prefix)
        {
            var info = allMods[mod];
            if (info.zipArchive != null)
            {
                prefix = info.folder + prefix;
                foreach (var entry in info.zipArchive.Entries)
                    if (entry.FullName.StartsWith(prefix, StringComparison.Ordinal))
                        yield return entry.FullName.Substring(info.folder.Length);
            }
            else {
                var dirFrom = Path.Combine(info.folder, prefix);
                foreach (var file in Directory.EnumerateFiles(dirFrom, null, SearchOption.AllDirectories))
                    yield return file.Substring(info.folder.Length);
            }
        }
    }
}