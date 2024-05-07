using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Yafc.Model;

namespace Yafc.Parser {
    public static class FactorioDataSource {
        internal static Dictionary<string, ModInfo> allMods = new Dictionary<string, ModInfo>();
        public static readonly Version defaultFactorioVersion = new Version(1, 1);
        private static byte[] ReadAllBytes(this Stream stream, int length) {
            BinaryReader reader = new BinaryReader(stream);
            byte[] bytes = reader.ReadBytes(length);
            stream.Dispose();
            return bytes;
        }

        private static readonly byte[] bom = { 0xEF, 0xBB, 0xBF };

        public static ReadOnlySpan<byte> CleanupBom(this ReadOnlySpan<byte> span) {
            return span.StartsWith(bom) ? span[bom.Length..] : span;
        }

        private static readonly char[] fileSplittersLua = { '.', '/', '\\' };
        private static readonly char[] fileSplittersNormal = { '/', '\\' };
        public static string currentLoadingMod;

        public static (string mod, string path) ResolveModPath(string currentMod, string fullPath, bool isLuaRequire = false) {
            string mod = currentMod;
            char[] splitters = fileSplittersNormal;
            if (isLuaRequire && !fullPath.Contains('/')) {
                splitters = fileSplittersLua;
            }

            string[] path = fullPath.Split(splitters, StringSplitOptions.RemoveEmptyEntries);
            if (Array.IndexOf(path, "..") >= 0) {
                throw new InvalidOperationException("Attempt to traverse to parent directory");
            }

            IEnumerable<string> pathEnumerable = path;
            if (path[0].StartsWith("__") && path[0].EndsWith("__")) {
                mod = path[0][2..^2];
                pathEnumerable = pathEnumerable.Skip(1);
            }

            string resolved = string.Join("/", pathEnumerable);
            if (isLuaRequire) {
                resolved += ".lua";
            }

            return (mod, resolved);
        }

        public static bool ModPathExists(string modName, string path) {
            var info = allMods[modName];
            if (info.zipArchive != null) {
                return info.zipArchive.GetEntry(info.folder + path) != null;
            }

            string fileName = Path.Combine(info.folder, path);
            return File.Exists(fileName);
        }

        public static byte[] ReadModFile(string modName, string path) {
            var info = allMods[modName];
            if (info.zipArchive != null) {
                var entry = info.zipArchive.GetEntry(info.folder + path);
                if (entry == null) {
                    return null;
                }

                byte[] bytearr = new byte[entry.Length];
                using (var stream = entry.Open()) {
                    int read = 0;
                    while (read < bytearr.Length) {
                        read += stream.Read(bytearr, read, bytearr.Length - read);
                    }
                }

                return bytearr;
            }

            string fileName = Path.Combine(info.folder, path);
            return File.Exists(fileName) ? File.ReadAllBytes(fileName) : null;
        }

        private static void LoadModLocale(string modName, string locale) {
            foreach (string localeName in GetAllModFiles(modName, "locale/" + locale + "/")) {
                byte[] loaded = ReadModFile(modName, localeName);
                using MemoryStream ms = new MemoryStream(loaded);
                FactorioLocalization.Parse(ms);
            }
        }

        private static void FindMods(string directory, IProgress<(string, string)> progress, List<ModInfo> mods) {
            foreach (string entry in Directory.EnumerateDirectories(directory)) {
                string infoFile = Path.Combine(entry, "info.json");
                if (File.Exists(infoFile)) {
                    progress.Report(("Initializing", entry));
                    ModInfo info = ModInfo.FromJson(File.ReadAllBytes(infoFile));
                    info.folder = entry;
                    mods.Add(info);
                }
            }

            foreach (string fileName in Directory.EnumerateFiles(directory)) {
                if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
                    FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                    ZipArchive zipArchive = new ZipArchive(fileStream);
                    var infoEntry = zipArchive.Entries.FirstOrDefault(x =>
                        x.Name.Equals("info.json", StringComparison.OrdinalIgnoreCase) &&
                        x.FullName.IndexOf('/') == x.FullName.Length - "info.json".Length - 1);
                    if (infoEntry != null) {
                        ModInfo info = ModInfo.FromJson(infoEntry.Open().ReadAllBytes((int)infoEntry.Length));
                        info.folder = infoEntry.FullName[..^"info.json".Length];
                        info.zipArchive = zipArchive;
                        mods.Add(info);
                    }
                }
            }
        }

        public static Project Parse(string factorioPath, string modPath, string projectPath, bool expensive, IProgress<(string, string)> progress, ErrorCollector errorCollector, string locale, bool renderIcons = true) {
            LuaContext dataContext = null;
            try {
                currentLoadingMod = null;
                string modSettingsPath = Path.Combine(modPath, "mod-settings.dat");
                progress.Report(("Initializing", "Loading mod list"));
                string modListPath = Path.Combine(modPath, "mod-list.json");
                Dictionary<string, Version> versionSpecifiers = new Dictionary<string, Version>();
                if (File.Exists(modListPath)) {
                    var mods = JsonSerializer.Deserialize<ModList>(File.ReadAllText(modListPath));
                    allMods = mods.mods.Where(x => x.enabled).Select(x => x.name).ToDictionary(x => x, x => (ModInfo)null);
                    versionSpecifiers = mods.mods.Where(x => x.enabled && !string.IsNullOrEmpty(x.version)).ToDictionary(x => x.name, x => Version.Parse(x.version));
                }
                else {
                    allMods = new Dictionary<string, ModInfo> { { "base", null } };
                }

                allMods["core"] = null;
                Console.WriteLine("Mod list parsed");

                List<ModInfo> allFoundMods = new List<ModInfo>();
                FindMods(factorioPath, progress, allFoundMods);
                if (modPath != factorioPath && modPath != "") {
                    FindMods(modPath, progress, allFoundMods);
                }

                Version factorioVersion = null;
                foreach (var mod in allFoundMods) {
                    currentLoadingMod = mod.name;
                    if (mod.name == "base") {
                        mod.parsedFactorioVersion = mod.parsedVersion;
                        if (factorioVersion == null || mod.parsedVersion > factorioVersion) {
                            factorioVersion = mod.parsedVersion;
                        }
                    }
                }

                foreach (var mod in allFoundMods) {
                    currentLoadingMod = mod.name;
                    if (mod.ValidForFactorioVersion(factorioVersion) && allMods.TryGetValue(mod.name, out var existing) && (existing == null || mod.parsedVersion > existing.parsedVersion || (mod.parsedVersion == existing.parsedVersion && existing.zipArchive != null && mod.zipArchive == null)) && (!versionSpecifiers.TryGetValue(mod.name, out var version) || mod.parsedVersion == version)) {
                        existing?.Dispose();
                        allMods[mod.name] = mod;
                    }
                    else {
                        mod.Dispose();
                    }
                }

                foreach (var (name, mod) in allMods) {
                    currentLoadingMod = name;
                    if (mod == null) {
                        throw new NotSupportedException("Mod not found: " + name + ". Try loading this pack in Factorio first.");
                    }

                    mod.ParseDependencies();
                }


                List<string> modsToDisable = new List<string>();
                do {
                    modsToDisable.Clear();
                    foreach (var (name, mod) in allMods) {
                        currentLoadingMod = name;
                        if (!mod.CheckDependencies(allMods, modsToDisable)) {
                            modsToDisable.Add(name);
                        }
                    }

                    currentLoadingMod = null;

                    foreach (string mod in modsToDisable) {
                        _ = allMods.Remove(mod, out var disabled);
                        disabled?.Dispose();
                    }
                } while (modsToDisable.Count > 0);

                currentLoadingMod = null;
                progress.Report(("Initializing", "Creating Lua context"));

                HashSet<string> modsToLoad = allMods.Keys.ToHashSet();
                string[] modLoadOrder = new string[modsToLoad.Count];
                modLoadOrder[0] = "core";
                _ = modsToLoad.Remove("core");
                int index = 1;
                List<string> sortedMods = modsToLoad.ToList();
                sortedMods.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));
                List<string> currentLoadBatch = new List<string>();
                while (modsToLoad.Count > 0) {
                    currentLoadBatch.Clear();
                    foreach (string mod in sortedMods) {
                        if (allMods[mod].CanLoad(allMods, modsToLoad)) {
                            currentLoadBatch.Add(mod);
                        }
                    }
                    if (currentLoadBatch.Count == 0) {
                        throw new NotSupportedException("Mods dependencies are circular. Unable to load mods: " + string.Join(", ", modsToLoad));
                    }

                    foreach (string mod in currentLoadBatch) {
                        modLoadOrder[index++] = mod;
                        _ = modsToLoad.Remove(mod);
                    }

                    _ = sortedMods.RemoveAll(x => !modsToLoad.Contains(x));
                }

                Console.WriteLine("All mods found! Loading order: " + string.Join(", ", modLoadOrder));

                if (locale != "en") {
                    foreach (string mod in modLoadOrder) {
                        currentLoadingMod = mod;
                        LoadModLocale(mod, "en");
                    }
                }
                if (locale != null) {
                    foreach (string mod in modLoadOrder) {
                        currentLoadingMod = mod;
                        LoadModLocale(mod, locale);
                    }
                }

                byte[] preProcess = File.ReadAllBytes("Data/Sandbox.lua");
                byte[] postProcess = File.ReadAllBytes("Data/Postprocess.lua");
                DataUtils.allMods = modLoadOrder;
                DataUtils.dataPath = factorioPath;
                DataUtils.modsPath = modPath;
                DataUtils.expensiveRecipes = expensive;


                dataContext = new LuaContext();
                object settings;
                if (File.Exists(modSettingsPath)) {
                    using (FileStream fs = new FileStream(Path.Combine(modPath, "mod-settings.dat"), FileMode.Open, FileAccess.Read)) {
                        settings = FactorioPropertyTree.ReadModSettings(new BinaryReader(fs), dataContext);
                    }

                    Console.WriteLine("Mod settings parsed");
                }
                else {
                    settings = dataContext.NewTable();
                }

                // TODO default mod settings
                dataContext.SetGlobal("settings", settings);

                _ = dataContext.Exec(preProcess, "*", "pre");
                dataContext.DoModFiles(modLoadOrder, "data.lua", progress);
                dataContext.DoModFiles(modLoadOrder, "data-updates.lua", progress);
                dataContext.DoModFiles(modLoadOrder, "data-final-fixes.lua", progress);
                _ = dataContext.Exec(postProcess, "*", "post");
                currentLoadingMod = null;

                FactorioDataDeserializer deserializer = new FactorioDataDeserializer(expensive, factorioVersion ?? defaultFactorioVersion);
                var project = deserializer.LoadData(projectPath, dataContext.data, dataContext.defines["prototypes"] as LuaTable, progress, errorCollector, renderIcons);
                Console.WriteLine("Completed!");
                progress.Report(("Completed!", ""));
                return project;
            }
            finally {
                dataContext?.Dispose();
                foreach (var mod in allMods) {
                    mod.Value?.Dispose();
                }

                allMods.Clear();
            }
        }

        internal class ModEntry {
            public string name { get; set; }
            public bool enabled { get; set; }
            public string version { get; set; }
        }

        internal class ModList {
            public ModEntry[] mods { get; set; }
        }

        internal class ModInfo : IDisposable {
            private static readonly string[] defaultDependencies = { "base" };
            private static readonly Regex dependencyRegex = new Regex("^\\(?([?!~]?)\\)?\\s*([\\w- ]+?)(?:\\s*[><=]+\\s*[\\d.]*)?\\s*$");
            public string name { get; set; }
            public string version { get; set; }
            public string factorio_version { get; set; }
            public Version parsedVersion { get; set; }
            public Version parsedFactorioVersion { get; set; }
            public string[] dependencies { get; set; } = defaultDependencies;
            private (string mod, bool optional)[] parsedDependencies;
            private string[] incompatibilities = Array.Empty<string>();

            public ZipArchive zipArchive;
            public string folder;

            public static ModInfo FromJson(ReadOnlySpan<byte> json) {
                var info = JsonSerializer.Deserialize<ModInfo>(json.CleanupBom());
                _ = Version.TryParse(info.version, out var parsedV);
                info.parsedVersion = parsedV ?? new Version();
                _ = Version.TryParse(info.factorio_version, out parsedV);
                info.parsedFactorioVersion = parsedV ?? defaultFactorioVersion;

                return info;
            }

            public void ParseDependencies() {
                List<(string mod, bool optional)> dependencyList = new List<(string mod, bool optional)>();
                List<string> incompatibilities = null;
                foreach (string dependency in dependencies) {
                    var match = dependencyRegex.Match(dependency);
                    if (match.Success) {
                        string modifier = match.Groups[1].Value;
                        if (modifier == "!") {
                            incompatibilities ??= new List<string>();
                            incompatibilities.Add(match.Groups[2].Value);
                            continue;
                        }
                        if (modifier == "~") {
                            continue;
                        }

                        dependencyList.Add((match.Groups[2].Value, modifier == "?"));
                    }
                }

                parsedDependencies = dependencyList.ToArray();
                if (incompatibilities != null) {
                    this.incompatibilities = incompatibilities.ToArray();
                }
            }

            private bool MajorMinorEquals(Version a, Version b) {
                return a.Major == b.Major && a.Minor == b.Minor;
            }

            public bool ValidForFactorioVersion(Version factorioVersion) {
                return factorioVersion == null || MajorMinorEquals(factorioVersion, parsedFactorioVersion) ||
                       (MajorMinorEquals(factorioVersion, new Version(1, 0)) && MajorMinorEquals(parsedFactorioVersion, new Version(0, 18))) || name == "core";
            }

            public bool CheckDependencies(Dictionary<string, ModInfo> allMods, List<string> modsToDisable) {
                foreach (var (mod, optional) in parsedDependencies) {
                    if (!optional && !allMods.ContainsKey(mod)) {
                        return false;
                    }
                }

                foreach (string incompatibility in incompatibilities) {
                    if (allMods.ContainsKey(incompatibility) && !modsToDisable.Contains(incompatibility)) {
                        return false;
                    }
                }

                return true;
            }

            public bool CanLoad(Dictionary<string, ModInfo> mods, HashSet<string> nonLoadedMods) {
                foreach (var (mod, _) in parsedDependencies) {
                    if (nonLoadedMods.Contains(mod)) {
                        return false;
                    }
                }

                return true;
            }

            public void Dispose() {
                if (zipArchive != null) {
                    zipArchive.Dispose();
                    zipArchive = null;
                }
            }
        }

        public static IEnumerable<string> GetAllModFiles(string mod, string prefix) {
            var info = allMods[mod];
            if (info.zipArchive != null) {
                prefix = info.folder + prefix;
                foreach (var entry in info.zipArchive.Entries) {
                    if (entry.FullName.StartsWith(prefix, StringComparison.Ordinal)) {
                        yield return entry.FullName[info.folder.Length..];
                    }
                }
            }
            else {
                string dirFrom = Path.Combine(info.folder, prefix);
                if (Directory.Exists(dirFrom)) {
                    foreach (string file in Directory.EnumerateFiles(dirFrom, "*", SearchOption.AllDirectories)) {
                        yield return file[(info.folder.Length + 1)..];
                    }
                }
            }
        }
    }
}
