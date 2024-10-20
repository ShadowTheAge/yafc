using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using SDL2;
using Serilog;
using Yafc.Model;
using Yafc.UI;

namespace Yafc.Parser;

internal partial class FactorioDataDeserializer {
    private static readonly ILogger logger = Logging.GetLogger<FactorioDataDeserializer>();
    private LuaTable raw = null!; // null-forgiving: Initialized at the beginning of LoadData.
    private bool GetRef<T>(LuaTable table, string key, [MaybeNullWhen(false)] out T result) where T : FactorioObject, new() {
        result = null;
        if (!table.Get(key, out string? name)) {
            return false;
        }

        result = GetObject<T>(name);

        return true;
    }

    private T? GetRef<T>(LuaTable table, string key) where T : FactorioObject, new() {
        _ = GetRef<T>(table, key, out var result);

        return result;
    }

    private Fluid GetFluidFixedTemp(string key, int temperature) {
        var basic = GetObject<Fluid>(key);

        if (basic.temperature == temperature) {
            return basic;
        }

        if (temperature < basic.temperatureRange.min) {
            temperature = basic.temperatureRange.min;
        }

        string idWithTemp = key + "@" + temperature;

        if (basic.temperature == 0) {
            basic.SetTemperature(temperature);
            registeredObjects[(typeof(Fluid), idWithTemp)] = basic;

            return basic;
        }

        if (registeredObjects.TryGetValue((typeof(Fluid), idWithTemp), out var fluidWithTemp)) {
            return (Fluid)fluidWithTemp;
        }

        var split = SplitFluid(basic, temperature);
        allObjects.Add(split);
        registeredObjects[(typeof(Fluid), idWithTemp)] = split;

        return split;
    }

    private void UpdateSplitFluids() {
        HashSet<List<Fluid>> processedFluidLists = [];

        foreach (var fluid in allObjects.OfType<Fluid>()) {
            if (fluid.temperature == 0) {
                fluid.temperature = fluid.temperatureRange.min;
            }

            if (fluid.variants == null || !processedFluidLists.Add(fluid.variants)) {
                continue;
            }

            fluid.variants.Sort(DataUtils.FluidTemperatureComparer);
            fluidVariants[fluid.type + "." + fluid.name] = fluid.variants;

            foreach (var variant in fluid.variants) {
                AddTemperatureToFluidIcon(variant);
                variant.name += "@" + variant.temperature;
            }
        }
    }

    private static void AddTemperatureToFluidIcon(Fluid fluid) {
        string iconStr = fluid.temperature + "d";
        fluid.iconSpec =
        [
            .. fluid.iconSpec,
            .. iconStr.Take(4).Select((x, n) => new FactorioIconPart("__.__/" + x) { y = -16, x = (n * 7) - 12, scale = 0.28f }),
        ];
    }

    /// <summary>
    /// Process the data loaded from Factorio and the mods, and load the project specified by <paramref name="projectPath"/>.
    /// </summary>
    /// <param name="projectPath">The path to the project file to create or load. May be <see langword="null"/> or empty.</param>
    /// <param name="data">The Lua table data (containing data.raw) that was populated by the lua scripts.</param>
    /// <param name="prototypes">The Lua table defines.prototypes that was populated by the lua scripts.</param>
    /// <param name="netProduction">If <see langword="true"/>, recipe selection windows will only display recipes that provide net production or consumption 
    /// of the <see cref="Goods"/> in question.
    /// If <see langword="false"/>, recipe selection windows will show all recipes that produce or consume any quantity of that <see cref="Goods"/>.<br/>
    /// For example, Kovarex enrichment will appear for both production and consumption of both U-235 and U-238 when <see langword="false"/>,
    /// but will appear as only producing U-235 and consuming U-238 when <see langword="true"/>.</param>
    /// <param name="progress">An <see cref="IProgress{T}"/> that receives two strings describing the current loading state.</param>
    /// <param name="errorCollector">An <see cref="ErrorCollector"/> that will collect the errors and warnings encountered while loading and processing the file and data.</param>
    /// <param name="renderIcons">If <see langword="true"/>, Yafc will render the icons necessary for UI display.</param>
    /// <returns>A <see cref="Project"/> containing the information loaded from <paramref name="projectPath"/>. Also sets the <see langword="static"/> properties
    /// in <see cref="Database"/>.</returns>
    public Project LoadData(string projectPath, LuaTable data, LuaTable prototypes, bool netProduction,
        IProgress<(string, string)> progress, ErrorCollector errorCollector, bool renderIcons) {

        progress.Report(("Loading", "Loading items"));
        raw = (LuaTable?)data["raw"] ?? throw new ArgumentException("Could not load data.raw from data argument", nameof(data));
        LuaTable itemPrototypes = (LuaTable?)prototypes?["item"] ?? throw new ArgumentException("Could not load prototypes.item from data argument", nameof(prototypes));

        foreach (object prototypeName in itemPrototypes.ObjectElements.Keys) {
            DeserializePrototypes(raw, (string)prototypeName, DeserializeItem, progress, errorCollector);
        }

        allModules.AddRange(allObjects.OfType<Module>());
        progress.Report(("Loading", "Loading tiles"));
        DeserializePrototypes(raw, "tile", DeserializeTile, progress, errorCollector);
        progress.Report(("Loading", "Loading fluids"));
        DeserializePrototypes(raw, "fluid", DeserializeFluid, progress, errorCollector);
        progress.Report(("Loading", "Loading recipes"));
        DeserializePrototypes(raw, "recipe", DeserializeRecipe, progress, errorCollector);
        progress.Report(("Loading", "Loading technologies"));
        DeserializePrototypes(raw, "technology", DeserializeTechnology, progress, errorCollector);
        progress.Report(("Loading", "Loading entities"));
        LuaTable entityPrototypes = (LuaTable?)prototypes["entity"] ?? throw new ArgumentException("Could not load prototypes.item from data argument", nameof(prototypes));

        foreach (object prototypeName in entityPrototypes.ObjectElements.Keys) {
            DeserializePrototypes(raw, (string)prototypeName, DeserializeEntity, progress, errorCollector);
        }

        ParseModYafcHandles(data["script_enabled"] as LuaTable);
        progress.Report(("Post-processing", "Computing maps"));
        // Deterministically sort all objects

        allObjects.Sort((a, b) => a.sortingOrder == b.sortingOrder ? string.Compare(a.typeDotName, b.typeDotName, StringComparison.Ordinal) : a.sortingOrder - b.sortingOrder);

        for (int i = 0; i < allObjects.Count; i++) {
            allObjects[i].id = (FactorioId)i;
        }

        UpdateSplitFluids();
        var iconRenderTask = renderIcons ? Task.Run(RenderIcons) : Task.CompletedTask;
        UpdateRecipeIngredientFluids();
        UpdateRecipeCatalysts();
        CalculateMaps(netProduction);
        ExportBuiltData();
        progress.Report(("Post-processing", "Calculating dependencies"));
        Dependencies.Calculate();
        TechnologyLoopsFinder.FindTechnologyLoops();
        progress.Report(("Post-processing", "Creating project"));
        Project project = Project.ReadFromFile(projectPath, errorCollector);
        Analysis.ProcessAnalyses(progress, project, errorCollector);
        progress.Report(("Rendering icons", ""));
        iconRenderedProgress = progress;
        iconRenderTask.Wait();

        return project;
    }

    private IProgress<(string, string)>? iconRenderedProgress;

    private Icon CreateSimpleIcon(Dictionary<(string mod, string path), IntPtr> cache, string graphicsPath)
        => CreateIconFromSpec(cache, new FactorioIconPart("__core__/graphics/" + graphicsPath + ".png"));

    private void RenderIcons() {
        Dictionary<(string mod, string path), IntPtr> cache = [];
        try {
            foreach (char digit in "0123456789d") {
                cache[(".", digit.ToString())] = SDL_image.IMG_Load("Data/Digits/" + digit + ".png");
            }

            DataUtils.NoFuelIcon = CreateSimpleIcon(cache, "fuel-icon-red");
            DataUtils.WarningIcon = CreateSimpleIcon(cache, "warning-icon");
            DataUtils.HandIcon = CreateSimpleIcon(cache, "hand");

            Dictionary<string, Icon> simpleSpritesCache = [];
            int rendered = 0;

            foreach (var o in allObjects) {
                if (++rendered % 100 == 0) {
                    iconRenderedProgress?.Report(("Rendering icons", $"{rendered}/{allObjects.Count}"));
                }

                if (o.iconSpec != null && o.iconSpec.Length > 0) {
                    bool simpleSprite = o.iconSpec.Length == 1 && o.iconSpec[0].IsSimple();

                    if (simpleSprite && simpleSpritesCache.TryGetValue(o.iconSpec[0].path, out var icon)) {
                        o.icon = icon;
                        continue;
                    }

                    try {
                        o.icon = CreateIconFromSpec(cache, o.iconSpec);

                        if (simpleSprite) {
                            simpleSpritesCache[o.iconSpec[0].path] = o.icon;
                        }
                    }
                    catch (Exception ex) {
                        Console.Error.WriteException(ex);
                    }
                }
                else if (o is Recipe recipe && recipe.mainProduct != null) {
                    o.icon = recipe.mainProduct.icon;
                }
            }
        }
        finally {
            foreach (var (_, image) in cache) {
                if (image != IntPtr.Zero) {
                    SDL.SDL_FreeSurface(image);
                }
            }
        }
    }

    private unsafe Icon CreateIconFromSpec(Dictionary<(string mod, string path), IntPtr> cache, params FactorioIconPart[] spec) {
        const int iconSize = IconCollection.IconSize;
        nint targetSurface = SDL.SDL_CreateRGBSurfaceWithFormat(0, iconSize, iconSize, 0, SDL.SDL_PIXELFORMAT_RGBA8888);
        _ = SDL.SDL_SetSurfaceBlendMode(targetSurface, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);

        foreach (var icon in spec) {
            var modpath = FactorioDataSource.ResolveModPath("", icon.path);

            if (!cache.TryGetValue(modpath, out nint image)) {
                byte[] imageSource = FactorioDataSource.ReadModFile(modpath.mod, modpath.path);

                if (imageSource == null) {
                    image = cache[modpath] = IntPtr.Zero;
                }
                else {
                    fixed (byte* data = imageSource) {
                        nint src = SDL.SDL_RWFromMem((IntPtr)data, imageSource.Length);
                        image = SDL_image.IMG_Load_RW(src, (int)SDL.SDL_bool.SDL_TRUE);

                        if (image != IntPtr.Zero) {
                            ref var surface = ref RenderingUtils.AsSdlSurface(image);
                            uint format = Unsafe.AsRef<SDL.SDL_PixelFormat>((void*)surface.format).format;

                            if (format != SDL.SDL_PIXELFORMAT_RGB24 && format != SDL.SDL_PIXELFORMAT_RGBA8888) {
                                // SDL is failing to blit palette surfaces, converting them
                                nint old = image;
                                image = SDL.SDL_ConvertSurfaceFormat(old, SDL.SDL_PIXELFORMAT_RGBA8888, 0);
                                SDL.SDL_FreeSurface(old);
                            }

                            if (surface.h > iconSize * 2) {
                                image = SoftwareScaler.DownscaleIcon(image, iconSize);
                            }
                        }
                        cache[modpath] = image;
                    }
                }
            }

            if (image == IntPtr.Zero) {
                continue;
            }

            ref var sdlSurface = ref RenderingUtils.AsSdlSurface(image);
            int targetSize = icon.scale == 1f ? iconSize : MathUtils.Ceil(icon.size * icon.scale) * (iconSize / 32); // TODO research formula
            _ = SDL.SDL_SetSurfaceColorMod(image, MathUtils.FloatToByte(icon.r), MathUtils.FloatToByte(icon.g), MathUtils.FloatToByte(icon.b));
            //SDL.SDL_SetSurfaceAlphaMod(image, MathUtils.FloatToByte(icon.a));
            int basePosition = (iconSize - targetSize) / 2;
            SDL.SDL_Rect targetRect = new SDL.SDL_Rect {
                x = basePosition,
                y = basePosition,
                w = targetSize,
                h = targetSize
            };

            if (icon.x != 0) {
                targetRect.x = MathUtils.Clamp(targetRect.x + MathUtils.Round(icon.x * iconSize / icon.size), 0, iconSize - targetRect.w);
            }

            if (icon.y != 0) {
                targetRect.y = MathUtils.Clamp(targetRect.y + MathUtils.Round(icon.y * iconSize / icon.size), 0, iconSize - targetRect.h);
            }

            SDL.SDL_Rect srcRect = new SDL.SDL_Rect {
                w = sdlSurface.h, // That is correct (cutting mip maps)
                h = sdlSurface.h
            };
            _ = SDL.SDL_BlitScaled(image, ref srcRect, targetSurface, ref targetRect);
        }

        return IconCollection.AddIcon(targetSurface);
    }

    private static void DeserializePrototypes(LuaTable data, string type, Action<LuaTable, ErrorCollector> deserializer,
        IProgress<(string, string)> progress, ErrorCollector errorCollector) {

        object? table = data[type];
        progress.Report(("Building objects", type));

        if (table is not LuaTable luaTable) {
            return;
        }

        foreach (var entry in luaTable.ObjectElements) {
            if (entry.Value is LuaTable entryTable) {
                deserializer(entryTable, errorCollector);
            }
        }
    }

    private static float ParseEnergy(string? energy) {
        if (energy is null || energy.Length < 2) {
            return 0f;
        }

        char energyMul = energy[^2];
        // internally store energy in megawatts / megajoules to be closer to 1
        if (char.IsLetter(energyMul)) {
            float energyBase = float.Parse(energy[..^2]);

            switch (energyMul) {
                case 'k': return energyBase * 1e-3f;
                case 'M': return energyBase;
                case 'G': return energyBase * 1e3f;
                case 'T': return energyBase * 1e6f;
                case 'P': return energyBase * 1e9f;
                case 'E': return energyBase * 1e12f;
                case 'Z': return energyBase * 1e15f;
                case 'Y': return energyBase * 1e18f;
                case 'R': return energyBase * 1e21f;
                case 'Q': return energyBase * 1e24f;
            }
        }
        return float.Parse(energy[..^1]) * 1e-6f;
    }

    private static Effect ParseEffect(LuaTable table) {
        return new Effect {
            consumption = table.Get("consumption", 0f),
            speed = table.Get("speed", 0f),
            productivity = table.Get("productivity", 0f),
            pollution = table.Get("pollution", 0f),
            quality = table.Get("quality", 0f),
        };
    }

    private static EffectReceiver ParseEffectReceiver(LuaTable? table) {
        if (table == null) {
            return new EffectReceiver {
                baseEffect = new Effect(),
                usesModuleEffects = true,
                usesBeaconEffects = true,
                usesSurfaceEffects = true
            };
        }

        return new EffectReceiver {
            baseEffect = table.Get("base_effect", out LuaTable? baseEffect) ? ParseEffect(baseEffect) : new Effect(),
            usesModuleEffects = table.Get("uses_module_effects", true),
            usesBeaconEffects = table.Get("uses_beacon_effects ", true),
            usesSurfaceEffects = table.Get("uses_surface_effects ", true),
        };
    }

    private void DeserializeItem(LuaTable table, ErrorCollector _) {
        if (table.Get("type", "") == "module" && table.Get("effect", out LuaTable? moduleEffect)) {
            string name = table.Get("name", "");
            Module module = GetObject<Item, Module>(name);
            var effect = ParseEffect(moduleEffect);
            module.moduleSpecification = new ModuleSpecification {
                category = table.Get("category", ""),
                consumption = effect.consumption,
                speed = effect.speed,
                productivity = effect.productivity,
                pollution = effect.pollution,
                quality = effect.quality,
            };
        }

        Item item = DeserializeCommon<Item>(table, "item");

        if (table.Get("place_result", out string? placeResult) && !string.IsNullOrEmpty(placeResult)) {
            placeResults[item] = [placeResult];
        }

        item.stackSize = table.Get("stack_size", 1);

        if (item.locName == null && table.Get("placed_as_equipment_result", out string? result)) {
            Localize("equipment-name." + result, null);

            if (localeBuilder.Length > 0) {
                item.locName = FinishLocalize();
            }
        }
        if (table.Get("fuel_value", out string? fuelValue)) {
            item.fuelValue = ParseEnergy(fuelValue);
            item.fuelResult = GetRef<Item>(table, "burnt_result");

            if (table.Get("fuel_category", out string? category)) {
                fuels.Add(category, item);
            }
        }

        if (table.Get("send_to_orbit_mode", "not-sendable") != "not-sendable") {
            Product[] launchProducts;
            if (table.Get("rocket_launch_products", out LuaTable? products)) {
                launchProducts = products.ArrayElements<LuaTable>().Select(LoadProduct(item.typeDotName, item.stackSize)).ToArray();
            }
            else {
                launchProducts = [];
            }

            var recipe = CreateSpecialRecipe(item, SpecialNames.RocketLaunch, "launched");
            recipe.ingredients =
            [
                new Ingredient(item, item.stackSize),
                new Ingredient(rocketLaunch, 1)
            ];
            recipe.products = launchProducts;
            recipe.time = 0f; // TODO what to put here?
        }

        if (GetRef<Item>(table, "spoil_result", out Item? spoiled)) {
            var recipe = CreateSpecialRecipe(item, SpecialNames.SpoilRecipe, "spoiling");
            recipe.ingredients = [new Ingredient(item, 1)];
            recipe.products = [new Product(spoiled, 1)];
            recipe.time = table.Get("spoil_ticks", 0) / 60f;
        }

        if (table.Get("plant_result", out string? plantResult) && !string.IsNullOrEmpty(plantResult)) {
            plantResults[item] = plantResult;
        }
    }

    private Fluid SplitFluid(Fluid basic, int temperature) {
        logger.Information("Splitting fluid {FluidName} at {Temperature}", basic.name, temperature);
        basic.variants ??= [basic];
        var copy = basic.Clone();
        copy.SetTemperature(temperature);
        copy.variants!.Add(copy); // null-forgiving: Clone was given a non-null variants.

        if (copy.fuelValue > 0f) {
            fuels.Add(SpecialNames.BurnableFluid, copy);
        }

        fuels.Add(SpecialNames.SpecificFluid + basic.name, copy);

        return copy;
    }

    private void DeserializeTile(LuaTable table, ErrorCollector _) {
        var tile = DeserializeCommon<Tile>(table, "tile");

        if (table.Get("fluid", out string? fluid)) {
            Fluid pumpingFluid = GetObject<Fluid>(fluid);
            tile.Fluid = pumpingFluid;

            string recipeCategory = SpecialNames.PumpingRecipe + "tile";
            Recipe recipe = CreateSpecialRecipe(pumpingFluid, recipeCategory, "pumping");

            if (recipe.products == null) {
                recipe.products = [new Product(pumpingFluid, 1200f)]; // set to Factorio default pump amounts - looks nice in tooltip
                recipe.ingredients = [];
                recipe.time = 1f;
            }
        }
    }

    private void DeserializeFluid(LuaTable table, ErrorCollector _) {
        var fluid = DeserializeCommon<Fluid>(table, "fluid");
        fluid.originalName = fluid.name;

        if (table.Get("fuel_value", out string? fuelValue)) {
            fluid.fuelValue = ParseEnergy(fuelValue);
            fuels.Add(SpecialNames.BurnableFluid, fluid);
        }

        fuels.Add(SpecialNames.SpecificFluid + fluid.name, fluid);

        if (table.Get("heat_capacity", out string? heatCap)) {
            fluid.heatCapacity = ParseEnergy(heatCap);
        }

        fluid.temperatureRange = new TemperatureRange(table.Get("default_temperature", 0), table.Get("max_temperature", 0));
    }

    private Goods? LoadItemOrFluid(LuaTable table, bool useTemperature) {
        if (table.Get("type", out string? type) && table.Get("name", out string? name)) {
            if (type == "item") {
                return GetObject<Item>(name);
            }
            else if (type == "fluid") {
                if (useTemperature && table.Get("temperature", out int temperature)) {
                    return GetFluidFixedTemp(name, temperature);
                }
                return GetObject<Fluid>(name);
            }
        }

        return null;
    }

    private readonly StringBuilder localeBuilder = new StringBuilder();

    private void Localize(object obj) {
        if (obj is LuaTable table) {
            if (!table.Get(1, out string? key)) {
                return;
            }

            Localize(key, table);
        }
        else {
            _ = localeBuilder.Append(obj);
        }
    }

    private string FinishLocalize() {
        _ = localeBuilder.Replace("\\n", "\n");

        // Cleaning up tags using simple state machine
        // 0 = outside of tag, 1 = first potential tag char, 2 = inside possible tag, 3 = inside definite tag
        // tag is definite when it contains '=' or starts with '/' or '.'
        int state = 0, tagStart = 0;
        for (int i = 0; i < localeBuilder.Length; i++) {
            char chr = localeBuilder[i];

            switch (state) {
                case 0:
                    if (chr == '[') {
                        state = 1;
                        tagStart = i;
                    }
                    break;
                case 1:
                    if (chr == ']') {
                        state = 0;
                    }
                    else {
                        state = (chr is '/' or '.') ? 3 : 2;
                    }

                    break;
                case 2:
                    if (chr == '=') {
                        state = 3;
                    }
                    else if (chr == ']') {
                        state = 0;
                    }

                    break;
                case 3:
                    if (chr == ']') {
                        _ = localeBuilder.Remove(tagStart, i - tagStart + 1);
                        i = tagStart - 1;
                        state = 0;
                    }
                    break;
            }
        }

        string s = localeBuilder.ToString();
        _ = localeBuilder.Clear();

        return s;
    }

    private void Localize(string? key, LuaTable? table) {
        if (string.IsNullOrEmpty(key)) {
            if (table == null) {
                return;
            }

            foreach (object? elem in table.ArrayElements) {
                if (elem is LuaTable sub) {
                    Localize(sub);
                }
                else {
                    _ = localeBuilder.Append(elem);
                }
            }
            return;
        }

        key = FactorioLocalization.Localize(key);

        if (key == null) {
            if (table != null) {
                _ = localeBuilder.Append(string.Join(" ", table.ArrayElements<string>()));
            }

            return;
        }

        if (!key.Contains("__")) {
            _ = localeBuilder.Append(key);
            return;
        }

        using var parts = ((IEnumerable<string>)key.Split("__")).GetEnumerator();

        while (parts.MoveNext()) {
            _ = localeBuilder.Append(parts.Current);

            if (!parts.MoveNext()) {
                break;
            }

            string control = parts.Current;

            if (control is "ITEM" or "FLUID" or "RECIPE" or "ENTITY") {
                if (!parts.MoveNext()) {
                    break;
                }

                string subKey = control.ToLowerInvariant() + "-name." + parts.Current;
                Localize(subKey, null);
            }
            else if (control == "CONTROL") {
                if (!parts.MoveNext()) {
                    break;
                }

                _ = localeBuilder.Append(parts.Current);
            }
            else if (control == "ALT_CONTROL") {
                if (!parts.MoveNext() || !parts.MoveNext()) {
                    break;
                }

                _ = localeBuilder.Append(parts.Current);
            }
            else if (table != null && int.TryParse(control, out int i)) {
                if (table.Get(i + 1, out string? s)) {
                    Localize(s, null);
                }
                else if (table.Get(i + 1, out LuaTable? t)) {
                    Localize(t);
                }
                else if (table.Get(i + 1, out float f)) {
                    _ = localeBuilder.Append(f);
                }
            }
            else if (control.StartsWith("plural")) {
                _ = localeBuilder.Append("(???)");

                if (!parts.MoveNext()) {
                    break;
                }
            }
            else {
                // Not supported token... Append everything else as-is
                while (parts.MoveNext()) {
                    _ = localeBuilder.Append(parts.Current);
                }

                break;
            }
        }
    }

    private T DeserializeCommon<T>(LuaTable table, string prototypeType) where T : FactorioObject, new() {
        if (!table.Get("name", out string? name)) {
            throw new NotSupportedException($"Read a definition of a {prototypeType} that does not have a name.");
        }

        var target = GetObject<T>(name);
        target.factorioType = table.Get("type", "");

        if (table.Get("localised_name", out object? loc)) {  // Keep UK spelling for Factorio/LUA data objects
            Localize(loc);
        }
        else {
            Localize(prototypeType + "-name." + target.name, null);
        }

        target.locName = localeBuilder.Length == 0 ? null! : FinishLocalize(); // null-forgiving: We have another chance at the end of CalculateMaps.

        if (table.Get("localised_description", out loc)) {  // Keep UK spelling for Factorio/LUA data objects
            Localize(loc);
        }
        else {
            Localize(prototypeType + "-description." + target.name, null);
        }

        target.locDescr = localeBuilder.Length == 0 ? null : FinishLocalize();
        _ = table.Get("icon_size", out float defaultIconSize);

        if (table.Get("icon", out string? s)) {
            target.iconSpec = [new FactorioIconPart(s) { size = defaultIconSize }];
        }
        else if (table.Get("icons", out LuaTable? iconList)) {
            target.iconSpec = iconList.ArrayElements<LuaTable>().Select(x => {
                if (!x.Get("icon", out string? path)) {
                    throw new NotSupportedException($"One of the icon layers for {name} does not have a path.");
                }

                FactorioIconPart part = new FactorioIconPart(path);
                _ = x.Get("icon_size", out part.size, defaultIconSize);
                _ = x.Get("scale", out part.scale, 1f);

                if (x.Get("shift", out LuaTable? shift)) {
                    _ = shift.Get(1, out part.x);
                    _ = shift.Get(2, out part.y);
                }

                if (x.Get("tint", out LuaTable? tint)) {
                    _ = tint.Get("r", out part.r, 1f);
                    _ = tint.Get("g", out part.g, 1f);
                    _ = tint.Get("b", out part.b, 1f);
                    _ = tint.Get("a", out part.a, 1f);
                }

                return part;
            }).ToArray();
        }

        return target;
    }
}
