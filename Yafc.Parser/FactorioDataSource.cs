using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Serilog;
using Yafc.Model;
using Yafc.UI;

namespace Yafc.Parser;
public static partial class FactorioDataSource {
    /* If you're wondering why this class is partial,
     * please check the implementation comment of ModInfo.
     */

    private static readonly ILogger logger = Logging.GetLogger(typeof(FactorioDataSource));
    internal static Dictionary<string, ModInfo> allMods = [];
    internal static HashSet<string> disabledMods = [];
    public static readonly Version defaultFactorioVersion = new Version(1, 1);
    private static byte[] ReadAllBytes(this Stream stream, int length) {
        BinaryReader reader = new BinaryReader(stream);
        byte[] bytes = reader.ReadBytes(length);
        stream.Dispose();

        return bytes;
    }

    private static readonly byte[] bom = [0xEF, 0xBB, 0xBF];

    public static ReadOnlySpan<byte> CleanupBom(this ReadOnlySpan<byte> span) => span.StartsWith(bom) ? span[bom.Length..] : span;

    private static readonly char[] fileSplittersLua = ['.', '/', '\\'];
    private static readonly char[] fileSplittersNormal = ['/', '\\'];
#pragma warning disable CA2211 // Non-constant fields should not be visible.
    // Suppressed because the only place where it's read is when there is an exception.
    public static string? currentLoadingMod;
#pragma warning restore CA2211 // Non-constant fields should not be visible

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
                return [];
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

        return File.Exists(fileName) ? File.ReadAllBytes(fileName) : [];
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
                ModInfo info = new(entry, File.ReadAllBytes(infoFile));
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
                    ModInfo info = new(infoEntry.FullName[..^"info.json".Length], infoEntry.Open().ReadAllBytes((int)infoEntry.Length)) {
                        zipArchive = zipArchive
                    };
                    mods.Add(info);
                }
            }
        }
    }

    /// <summary>
    /// Create or load the file <paramref name="projectPath"/> (if specified), with the Factorio data at <paramref name="factorioPath"/> and <paramref name="modPath"/>.
    /// </summary>
    /// <param name="factorioPath">The path to the data/ folder, containing the base and core folders.</param>
    /// <param name="modPath">The path to the mods/ folder, containing mod-list.json and the mods. Both zipped and unzipped mods are supported.
    /// May be empty (but not <see langword="null"/>) to load only vanilla Factorio data.</param>
    /// <param name="projectPath">The path to the project file to create or load. May be <see langword="null"/> or empty.</param>
    /// <param name="expensive">Whether to use expensive recipes.</param>
    /// <param name="netProduction">If <see langword="true"/>, recipe selection windows will only display recipes that provide net production or consumption
    /// of the <see cref="Goods"/> in question.
    /// If <see langword="false"/>, recipe selection windows will show all recipes that produce or consume any quantity of that <see cref="Goods"/>.<br/>
    /// For example, Kovarex enrichment will appear for both production and consumption of both U-235 and U-238 when <see langword="false"/>,
    /// but will appear as only producing U-235 and consuming U-238 when <see langword="true"/>.</param>
    /// <param name="progress">An <see cref="IProgress{T}"/> that receives two strings describing the current loading state.</param>
    /// <param name="errorCollector">An <see cref="ErrorCollector"/> that will collect the errors and warnings encountered while loading and processing the file and data.</param>
    /// <param name="locale">One of the languages supported by Factorio. Typically just the two-letter language code, e.g. en,
    /// but occasionally also includes the region code, e.g. pt-PT.</param>
    /// <param name="renderIcons">If <see langword="true"/>, Yafc will render the icons necessary for UI display.</param>
    /// <returns>A <see cref="Project"/> containing the information loaded from <paramref name="projectPath"/>.
    /// Also sets the <see langword="static"/> properties in <see cref="Database"/>.</returns>
    /// <exception cref="NotSupportedException">Thrown if a mod enabled in mod-list.json could not be found in <paramref name="modPath"/>.</exception>
    public static Project Parse(string factorioPath, string modPath, string projectPath, bool expensive, bool netProduction,
        IProgress<(string MajorState, string MinorState)> progress, ErrorCollector errorCollector, string locale, bool renderIcons = true) {

        LuaContext? dataContext = null;

        try {
            currentLoadingMod = null;
            string modSettingsPath = Path.Combine(modPath, "mod-settings.dat");
            progress.Report(("Initializing", "Loading mod list"));
            string modListPath = Path.Combine(modPath, "mod-list.json");
            Dictionary<string, Version> versionSpecifiers = [];

            if (File.Exists(modListPath)) {
                var mods = JsonSerializer.Deserialize<ModList>(File.ReadAllText(modListPath)) ?? throw new($"Could not read mod list from {modListPath}");
                allMods = mods.mods.Where(x => x.enabled).Select(x => x.name).ToDictionary(x => x, x => (ModInfo)null!);
                versionSpecifiers = mods.mods.Where(x => x.enabled && !string.IsNullOrEmpty(x.version)).ToDictionary(x => x.name, x => Version.Parse(x.version!)); // null-forgiving: null version strings are filtered by the Where.
            }
            else {
                allMods = new Dictionary<string, ModInfo> { { "base", null! } };
            }

            allMods["core"] = null!;
            foreach (string disabledMod in disabledMods) {
                _ = allMods.Remove(disabledMod);
            }
            logger.Information("Mod list parsed");

            List<ModInfo> allFoundMods = [];
            FindMods(factorioPath, progress, allFoundMods);

            if (modPath != factorioPath && modPath != "") {
                FindMods(modPath, progress, allFoundMods);
            }

            Version? factorioVersion = null;

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

            string? missingMod = null;

            foreach (var (name, mod) in allMods) {
                currentLoadingMod = name;

                if (mod == null) {
                    missingMod ??= name;
                    logger.Error("Mod not found: {ModName}.", name);
                }

                mod?.ParseDependencies();
            }

            if (missingMod != null) {
                throw new NotSupportedException("Mod not found: " + missingMod + ". Try loading this pack in Factorio first.");
            }


            List<string> modsToDisable = [];
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

            HashSet<string> modsToLoad = [.. allMods.Keys];
            string[] modLoadOrder = new string[modsToLoad.Count];
            modLoadOrder[0] = "core";
            _ = modsToLoad.Remove("core");
            int index = 1;
            List<string> sortedMods = [.. modsToLoad];
            sortedMods.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));
            List<string> currentLoadBatch = [];

            while (modsToLoad.Count > 0) {
                currentLoadBatch.Clear();

                foreach (string mod in sortedMods) {
                    if (allMods[mod].CanLoad(modsToLoad)) {
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

            logger.Information("All mods found! Loading order: {LoadOrder}", string.Join(", ", modLoadOrder));

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
            DataUtils.dataPath = factorioPath;
            DataUtils.modsPath = modPath;
            DataUtils.expensiveRecipes = expensive;
            DataUtils.netProduction = netProduction;


            currentLoadingMod = null;
            dataContext = new LuaContext();
            object? settings = null;

            if (File.Exists(modSettingsPath)) {
                using (FileStream fs = new FileStream(Path.Combine(modPath, "mod-settings.dat"), FileMode.Open, FileAccess.Read)) {
                    settings = FactorioPropertyTree.ReadModSettings(new BinaryReader(fs), dataContext);
                }

                logger.Information("Mod settings parsed");
            }
            settings ??= dataContext.NewTable();

            // TODO default mod settings
            dataContext.SetGlobal("settings", settings);

            _ = dataContext.Exec(preProcess, "*", "pre");
            dataContext.DoModFiles(modLoadOrder, "data.lua", progress);
            dataContext.DoModFiles(modLoadOrder, "data-updates.lua", progress);
            dataContext.DoModFiles(modLoadOrder, "data-final-fixes.lua", progress);
            currentLoadingMod = null;
            _ = dataContext.Exec(postProcess, "*", "post");

            FactorioDataDeserializer deserializer = new FactorioDataDeserializer(expensive, factorioVersion ?? defaultFactorioVersion);
            var project = deserializer.LoadData(projectPath, dataContext.data, (LuaTable)dataContext.defines["prototypes"]!, netProduction, progress, errorCollector, renderIcons);
            logger.Information("Completed!");
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
        public string name { get; set; } = null!; // null-forgiving: Initialized by the Json reader.
        public bool enabled { get; set; }
        public string? version { get; set; }
    }

    internal class ModList {
        public ModEntry[] mods { get; set; } = null!; // null-forgiving: Initialized by the Json reader.
    }

    internal partial class ModInfo : IDisposable {
        /* This class is partial because we want to generate a Regex at compile time.
         * For that, we use a GeneratedRegex annotation that can be applied only to partial, parameterless,
         * non-generic methods that are typed to return Regex.
         * Due to the Regex method being partial, so is the ModInfo class, so is the FactorioDataSource class.
         */

        private static readonly string[] defaultDependencies = ["base"];
        public string name { get; set; } = null!; // null-forgiving: Set by JsonSerializer.Populate
        public string version { get; set; } = null!; // null-forgiving: Set by JsonSerializer.Populate
        public string? factorio_version { get; set; }
        public Version parsedVersion { get; set; }
        public Version parsedFactorioVersion { get; set; }
        public string[] dependencies { get; set; } = defaultDependencies;
        private readonly List<(string mod, bool optional)> parsedDependencies = [];
        private readonly List<string> incompatibilities = [];

        public ZipArchive? zipArchive;
        public string folder;

        public ModInfo(string folder, byte[] json, ZipArchive? zipArchive = null) {
            this.folder = folder;
            this.zipArchive = zipArchive;
            Newtonsoft.Json.JsonSerializer.Create().Populate(new StreamReader(new MemoryStream(json)), this);
            _ = Version.TryParse(version, out var parsedV);
            parsedVersion = parsedV ?? new Version();
            _ = Version.TryParse(factorio_version, out parsedV);
            parsedFactorioVersion = parsedV ?? defaultFactorioVersion;
        }

        public void ParseDependencies() {
            foreach (string dependency in dependencies) {
                var match = DependencyRegex().Match(dependency);

                if (match.Success) {
                    string modifier = match.Groups[1].Value;

                    if (modifier == "!") {
                        incompatibilities.Add(match.Groups[2].Value);
                        continue;
                    }
                    if (modifier == "~") {
                        continue;
                    }

                    parsedDependencies.Add((match.Groups[2].Value, modifier == "?"));
                }
            }
        }

        private bool MajorMinorEquals(Version a, Version b) => a.Major == b.Major && a.Minor == b.Minor;

        public bool ValidForFactorioVersion(Version? factorioVersion) => factorioVersion == null || MajorMinorEquals(factorioVersion, parsedFactorioVersion) ||
            (MajorMinorEquals(factorioVersion, new Version(1, 0)) && MajorMinorEquals(parsedFactorioVersion, new Version(0, 18))) || name == "core";

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

        public bool CanLoad(HashSet<string> notLoadedMods) {
            foreach (var (mod, _) in parsedDependencies) {
                if (notLoadedMods.Contains(mod)) {
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

        [GeneratedRegex("^\\(?([?!~]?)\\)?\\s*([\\w- ]+?)(?:\\s*[><=]+\\s*[\\d.]*)?\\s*$")]
        private static partial Regex DependencyRegex();
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

    /// <summary>
    /// Instructs Yafc not to load the specified mod, even if it is listed in mod-list.json. Mods that depend on this mod will also not be loaded.
    /// </summary>
    /// <param name="modName">The name of the mod to disable.</param>
    public static void DisableMod(string modName) => disabledMods.Add(modName);
    /// <summary>
    /// Instructs Yafc to forget about all mods names previously passed to <see cref="DisableMod"/>, and resume loading all mods in mod-list.json.
    /// </summary>
    public static void ClearDisabledMods() => disabledMods.Clear();
}
