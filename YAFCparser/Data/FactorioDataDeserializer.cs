using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using SDL2;
using YAFC.Model;
using YAFC.UI;

namespace YAFC.Parser
{    
    internal partial class FactorioDataDeserializer
    {
        private LuaTable raw;
        private bool GetRef<T>(LuaTable table, string key, out T result) where T:FactorioObject, new()
        {
            result = null;
            if (!table.Get(key, out string name))
                return false;
            result = GetObject<T>(name);
            return true;
        }

        private T GetRef<T>(LuaTable table, string key) where T:FactorioObject, new()
        {
            GetRef<T>(table, key, out var result);
            return result;
        }

        private string MakeFluidTemperatureNameSuffix(float min, float max)
        {
            var nameSuffix = "__" +
                (min == max
                ? ((int)min).ToString()
                : ((int)min).ToString() + "_" + ((int)max).ToString());

            return nameSuffix;
        }

        private string MakeFluidTemperatureLocNameSuffix(float min, float max)
        {
            var locNameSuffix = " " +
                (min == max
                ? ((int)min).ToString() + "°"
                : ((int)min).ToString() + "°-" + ((int)max).ToString() + "°");

            return locNameSuffix;
        }

        private Fluid MakeFluidWithDifferentTemperatures(Fluid fluid, float newMinTemperature, float newMaxTemperature)
        {
            var nameSuffix = MakeFluidTemperatureNameSuffix(newMinTemperature, newMaxTemperature);
            var locNameSuffix = MakeFluidTemperatureLocNameSuffix(newMinTemperature, newMaxTemperature);

            Fluid copy = fluid.Clone();

            copy.name = fluid.name + nameSuffix;
            copy.locName = fluid.locName + locNameSuffix;
            copy.minTemperature = newMinTemperature;
            copy.maxTemperature = newMaxTemperature;

            return copy;
        }

        private Recipe ReplaceRecipeFluidIngredient(Recipe recipe, Ingredient ingredient, Fluid newFluid)
        {
            var nameSuffix = MakeFluidTemperatureNameSuffix(newFluid.minTemperature, newFluid.maxTemperature);

            Recipe copy = recipe.Clone();

            copy.ingredients = recipe.ingredients.Select(i => {
                if (i == ingredient)
                {
                    return new Ingredient(newFluid, i.amount);
                }
                else
                {
                    return i;
                }
            }).ToArray();
            copy.name = recipe.name + nameSuffix;

            return copy;
        }


        private Recipe ReplaceRecipeFluidProduct(Recipe recipe, Product product, Fluid newFluid)
        {
            var nameSuffix = MakeFluidTemperatureNameSuffix(newFluid.minTemperature, newFluid.maxTemperature);

            Recipe copy = recipe.Clone();

            copy.name = recipe.name + nameSuffix;

            if (recipe.mainProduct == product.goods)
            {
                copy.mainProduct = newFluid;
            }

            copy.products = recipe.products.Select(p => {
                return p == product
                    ? new Product(newFluid, p.amount) { temperature = product.temperature }
                    : p;
            }).ToArray();

            return copy;
        }


        private List<Recipe> SplitRecipeByIngredientTemperature(Recipe parentRecipe, Fluid parentFluid, List<Fluid> partitionedFluid)
        {
            var ingredient = parentRecipe.ingredients.First(i => i.goods == parentFluid);

            return partitionedFluid
                .Where(f => 
                    f.minTemperature >= ingredient.minTemperature - 0.001 
                    && f.maxTemperature <= ingredient.maxTemperature + 0.001)
                .Select(f => ReplaceRecipeFluidIngredient(parentRecipe, ingredient, f)).ToList();
        }

        private List<Recipe> SplitRecipeByProductTemperature(Recipe parentRecipe, Fluid parentFluid, List<Fluid> partitionedFluid)
        {
            var product = parentRecipe.products.First(i => i.goods == parentFluid);

            return partitionedFluid
                .Where(f =>
                    product.temperature >= f.minTemperature - 0.001 
                    && product.temperature <= f.maxTemperature + 0.001)
                .Select(f => ReplaceRecipeFluidProduct(parentRecipe, product, f)).ToList();
        }

        private List<Recipe> SplitRecipeByIngredientsTemperatures(Recipe parentRecipe, List<(Fluid, List<Fluid>)> partitionedFluids)
        {
            var matchingFluids = partitionedFluids.Where(f => parentRecipe.ingredients.Any(i => i.goods == f.Item1));

            var partitionedRecipes = new List<Recipe>
            {
                parentRecipe
            };

            foreach (var fluid in matchingFluids)
            {
                partitionedRecipes = partitionedRecipes
                    .SelectMany(r => SplitRecipeByIngredientTemperature(r, fluid.Item1, fluid.Item2)).ToList();
            }

            return partitionedRecipes;
        }

        private List<Recipe> SplitRecipeByProductsTemperatures(Recipe parentRecipe, List<(Fluid, List<Fluid>)> partitionedFluids)
        {
            var matchingFluids = partitionedFluids.Where(f => parentRecipe.products.Any(i => i.goods == f.Item1));

            var partitionedRecipes = new List<Recipe>
            {
                parentRecipe
            };

            foreach (var fluid in matchingFluids)
            {
                partitionedRecipes = partitionedRecipes
                    .SelectMany(r => SplitRecipeByProductTemperature(r, fluid.Item1, fluid.Item2)).ToList();
            }

            return partitionedRecipes;
        }

        private List<Recipe> SplitRecipeByIngredientsProductsTemperatures(Recipe parentRecipe, List<(Fluid, List<Fluid>)> partitionedFluids)
        {
            return SplitRecipeByIngredientsTemperatures(parentRecipe, partitionedFluids)
                .SelectMany(r => SplitRecipeByProductsTemperatures(r, partitionedFluids)).ToList();
        }

        private List<Fluid> SplitFluidByTemperature(Fluid fluid, IEnumerable<Recipe> recipes)
        {
            var partitionPoints =
                recipes
                .Where(r => r.products.Any(p => p.goods == fluid))
                .Select(r => r.products.First(p => p.goods == fluid).temperature).ToImmutableSortedSet();

            return partitionPoints.Select(p => MakeFluidWithDifferentTemperatures(fluid, p, p)).ToList();
        }

        private void SplitFluidsByTemperature()
        {
            var recipes = allObjects.OfType<Recipe>();

            var fluids = allObjects.OfType<Fluid>();

            var newFluids = new List<(Fluid, List<Fluid>)>();

            foreach (var fluid in fluids)
            {
                var partitionedFluid = SplitFluidByTemperature(fluid, recipes);
                // Only split fluids when there's actually a reason.
                if (partitionedFluid.Count > 1)
                {
                    newFluids.Add((fluid, partitionedFluid));
                }
            }

            var newRecipes = new List<(Recipe, List<Recipe>)>();
            foreach(var recipe in recipes)
            {
                var partitionedRecipe = SplitRecipeByIngredientsProductsTemperatures(recipe, newFluids);
                // If we split the fluid we have to split the recipes even if there's only one.
                // Otherwise we end up with references to removed fluids.
                if (partitionedRecipe.Count > 0)
                {
                    newRecipes.Add((recipe, partitionedRecipe));
                }
            }

            foreach(var tech in allObjects.OfType<Technology>())
            {
                tech.unlockRecipes =
                    tech.unlockRecipes.SelectMany(unlockRecipe =>
                    {
                        var newUnlockRecipe = newRecipes.Where(newRecipe => unlockRecipe == newRecipe.Item1);
                        if (newUnlockRecipe.Any())
                        {
                            return newUnlockRecipe.First().Item2;
                        }
                        else
                        {
                            return new List<Recipe> { unlockRecipe };
                        }
                    }).ToArray();
            }

            foreach(var fluid in newFluids)
            {
                var fuelCategory = SpecialNames.SpecificFluid + fluid.Item1.name;
                foreach(var newFluid in fluid.Item2)
                {
                    fuels.Add(fuelCategory, newFluid, true);
                    if (newFluid.fuelValue > 0f)
                    {
                        fuels.Add(SpecialNames.BurnableFluid, newFluid);
                    }
                }
            }

            var recipesToRemove = newRecipes.Select(r => r.Item1).ToHashSet();
            var recipesToAdd = newRecipes.SelectMany(r => r.Item2).ToArray();

            var fluidsToRemove = newFluids.Select(r => r.Item1).ToHashSet();
            var fluidsToAdd = newFluids.SelectMany(r => r.Item2).ToArray();

            allObjects.RemoveAll(o => recipesToRemove.Contains(o));
            allObjects.RemoveAll(o => fluidsToRemove.Contains(o));

            allObjects.AddRange(fluidsToAdd);
            allObjects.AddRange(recipesToAdd);
        }

        private void AssignRecipesToCategories()
        {
            foreach (var recipe in allObjects.OfType<Recipe>())
            {
                recipeCategories.Add(recipe.category, recipe);
            }
        }

        private void AssignObjectIds()
        {
            for (var i = 0; i < allObjects.Count; i++)
            {
                allObjects[i].id = (FactorioId)i;
            }
        }
        
        public Project LoadData(string projectPath, LuaTable data, IProgress<(string, string)> progress, ErrorCollector errorCollector)
        {
            progress.Report(("Loading", "Loading items"));

            raw = (LuaTable)data["raw"];
            foreach (var prototypeName in ((LuaTable)data["Item types"]).ArrayElements<string>())
                DeserializePrototypes(raw, prototypeName, DeserializeItem, progress);

            recipeModules.SealAndDeduplicate(universalModules.ToArray());

            allModules = allObjects.OfType<Item>().Where(x => x.module != null).ToArray();

            progress.Report(("Loading", "Loading fluids"));
            DeserializePrototypes(raw, "fluid", DeserializeFluid, progress);

            progress.Report(("Loading", "Loading recipes"));
            DeserializePrototypes(raw, "recipe", DeserializeRecipe, progress);

            progress.Report(("Loading", "Loading technologies"));
            DeserializePrototypes(raw, "technology", DeserializeTechnology, progress);

            progress.Report(("Loading", "Loading entities"));
            foreach (var prototypeName in ((LuaTable) data["Entity types"]).ArrayElements<string>())
                DeserializePrototypes(raw, prototypeName, DeserializeEntity, progress);

            if (splitFluidsByTemperature)
            {
                progress.Report(("Post-processing", "Understanding fluid temperatures"));
                SplitFluidsByTemperature();
            }

            // Moved to here because we remove recipes earlier - we would otherwise point to inexisting recipes
            AssignRecipesToCategories();

            progress.Report(("Post-processing", "Computing maps"));

            // Deterministically sort all objects
            allObjects.Sort((a, b) => a.sortingOrder == b.sortingOrder ? string.Compare(a.typeDotName, b.typeDotName, StringComparison.Ordinal) : a.sortingOrder - b.sortingOrder);

            AssignObjectIds();

            var iconRenderTask = Task.Run(RenderIcons);

            CalculateMaps();
            ExportBuiltData();

            progress.Report(("Post-processing", "Calculating dependencies"));
            Dependencies.Calculate();
            TechnologyLoopsFinder.FindTechnologyLoops();

            progress.Report(("Post-processing", "Creating project"));
            var project = Project.ReadFromFile(projectPath, errorCollector);
            Analysis.ProcessAnalyses(progress, project, errorCollector);
            
            progress.Report(("Rendering icons", ""));
            iconRenderedProgress = progress;
            iconRenderTask.Wait();

            return project;
        }

        private volatile IProgress<(string, string)> iconRenderedProgress;

        private Icon CreateSimpleIcon(Dictionary<(string mod, string path),IntPtr> cache, string graphicsPath)
        {
            return CreateIconFromSpec(cache, new FactorioIconPart {path = "__core__/graphics/" + graphicsPath + ".png"});
        }

        private void RenderIcons()
        {
            var cache = new Dictionary<(string mod, string path), IntPtr>();
            try
            {
                DataUtils.NoFuelIcon = CreateSimpleIcon(cache, "fuel-icon-red");
                DataUtils.WarningIcon = CreateSimpleIcon(cache, "warning-icon");
                DataUtils.HandIcon = CreateSimpleIcon(cache, "hand");

                var simpleSpritesCache = new Dictionary<string, Icon>();
                var rendered = 0;

                foreach (var o in allObjects)
                {
                    if (++rendered % 100 == 0)
                        iconRenderedProgress?.Report(("Rendering icons", $"{rendered}/{allObjects.Count}"));
                    if (o.iconSpec != null && o.iconSpec.Length > 0)
                    {
                        var simpleSprite = o.iconSpec.Length == 1 && o.iconSpec[0].IsSimple();
                        if (simpleSprite && simpleSpritesCache.TryGetValue(o.iconSpec[0].path, out var icon))
                        {
                            o.icon = icon;
                            continue;
                        }

                        try
                        {
                            o.icon = CreateIconFromSpec(cache, o.iconSpec);
                            if (simpleSprite)
                                simpleSpritesCache[o.iconSpec[0].path] = o.icon;
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteException(ex);
                        }
                    }
                    else if (o is Recipe recipe && recipe.mainProduct != null)
                        o.icon = recipe.mainProduct.icon;
                }
            }
            finally
            {
                foreach (var (_, image) in cache)
                {
                    if (image != IntPtr.Zero)
                        SDL.SDL_FreeSurface(image);
                }
            }
        }

        private unsafe Icon CreateIconFromSpec(Dictionary<(string mod, string path),IntPtr> cache, params FactorioIconPart[] spec)
        {
            const int IconSize = IconCollection.IconSize;
            var targetSurface = SDL.SDL_CreateRGBSurfaceWithFormat(0, IconSize, IconSize, 0, SDL.SDL_PIXELFORMAT_RGBA8888);
            SDL.SDL_SetSurfaceBlendMode(targetSurface, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            foreach (var icon in spec)
            {
                var modpath = FactorioDataSource.ResolveModPath("", icon.path);
                if (!cache.TryGetValue(modpath, out var image))
                {
                    var imageSource = FactorioDataSource.ReadModFile(modpath.mod, modpath.path);
                    if (imageSource == null)
                        image = cache[modpath] = IntPtr.Zero;
                    else
                    {
                        fixed (byte* data = imageSource)
                        {
                            var src = SDL.SDL_RWFromMem((IntPtr) data, imageSource.Length);
                            image = SDL_image.IMG_Load_RW(src, (int) SDL.SDL_bool.SDL_TRUE);
                            if (image != IntPtr.Zero)
                            {
                                ref var surface = ref RenderingUtils.AsSdlSurface(image);
                                var format = Unsafe.AsRef<SDL.SDL_PixelFormat>((void*) surface.format).format;
                                if (format != SDL.SDL_PIXELFORMAT_RGB24 && format != SDL.SDL_PIXELFORMAT_RGBA8888)
                                {
                                    // SDL is failing to blit patelle surfaces, converting them
                                    var old = image;
                                    image = SDL.SDL_ConvertSurfaceFormat(old, SDL.SDL_PIXELFORMAT_RGBA8888, 0);
                                    SDL.SDL_FreeSurface(old);
                                }

                                if (surface.h > IconSize * 2)
                                {
                                    image = SoftwareScaler.DownscaleIcon(image, IconSize);
                                }
                            }
                            cache[modpath] = image;
                        }
                    }
                } 
                if (image == IntPtr.Zero)
                    continue;
                
                ref var sdlSurface = ref RenderingUtils.AsSdlSurface(image);
                var targetSize = icon.scale == 1f ? IconSize : MathUtils.Ceil(icon.size * icon.scale) * (IconSize/32); // TODO research formula
                SDL.SDL_SetSurfaceColorMod(image, MathUtils.FloatToByte(icon.r), MathUtils.FloatToByte(icon.g), MathUtils.FloatToByte(icon.b));
                //SDL.SDL_SetSurfaceAlphaMod(image, MathUtils.FloatToByte(icon.a));
                var basePosition = (IconSize - targetSize) / 2;
                var targetRect = new SDL.SDL_Rect
                {
                    x = basePosition,
                    y = basePosition,
                    w = targetSize,
                    h = targetSize
                };
                if (icon.x != 0)
                    targetRect.x = MathUtils.Clamp(targetRect.x + MathUtils.Round(icon.x * IconSize / icon.size), 0, IconSize - targetRect.w);
                if (icon.y != 0)
                    targetRect.y = MathUtils.Clamp(targetRect.y + MathUtils.Round(icon.y * IconSize / icon.size), 0, IconSize - targetRect.h);
                var srcRect = new SDL.SDL_Rect
                {
                    w = sdlSurface.h, // That is correct (cutting mip maps)
                    h = sdlSurface.h
                };
                SDL.SDL_BlitScaled(image, ref srcRect, targetSurface, ref targetRect);
            }
            return IconCollection.AddIcon(targetSurface);
        }

        private void DeserializePrototypes(LuaTable data, string type, Action<LuaTable> deserializer, IProgress<(string, string)> progress)
        {
            var table = data[type];
            progress.Report(("Building objects", type));
            if (!(table is LuaTable luaTable))
                return;
            foreach (var entry in luaTable.ObjectElements)
                if (entry.Value is LuaTable entryTable)
                    deserializer(entryTable);
        }

        private float ParseEnergy(string energy)
        {
            var len = energy.Length - 2;
            if (len < 0f)
                return 0f;
            var energyMul = energy[len];
            // internaly store energy in megawatts / megajoules to be closer to 1
            if (char.IsLetter(energyMul))
            {
                var energyBase = float.Parse(energy.Substring(0, len));
                switch (energyMul)
                {
                    case 'k':
                    case 'K': return energyBase * 1e-3f;
                    case 'M': return energyBase;
                    case 'G': return energyBase * 1e3f;
                    case 'T': return energyBase * 1e6f;
                    case 'P': return energyBase * 1e9f;
                    case 'E': return energyBase * 1e12f;
                    case 'Z': return energyBase * 1e15f;
                    case 'Y': return energyBase * 1e18f;
                }
            }
            return float.Parse(energy.Substring(0, len + 1)) * 1e-6f;
        }

        private void DeserializeItem(LuaTable table)
        {
            var item = DeserializeCommon<Item>(table, "item");
            item.placeResult = GetRef<Entity>(table, "place_result");
            if (item.locName == null && table.Get("placed_as_equipment_result", out string result))
            {
                localeBuilder.Clear();
                Localize("equipment-name."+result, null);
                if (localeBuilder.Length > 0)
                    item.locName = localeBuilder.ToString();
            }
            if (table.Get("fuel_value", out string fuelValue))
            {
                item.fuelValue = ParseEnergy(fuelValue);
                item.fuelResult = GetRef<Item>(table,"burnt_result");
                table.Get("fuel_category", out string category);
                fuels.Add(category, item);
            }
            
            if (item.factorioType == "module" && table.Get("effect", out LuaTable moduleEffect))
            {
                item.module = new ModuleSpecification
                {
                    consumption = moduleEffect.Get("consumption", out LuaTable t) ? t.Get("bonus", 0f) : 0f,
                    speed = moduleEffect.Get("speed", out t) ? t.Get("bonus", 0f) : 0f,
                    productivity = moduleEffect.Get("productivity", out t) ? t.Get("bonus", 0f) : 0f,
                    pollution = moduleEffect.Get("pollution", out t) ? t.Get("bonus", 0f) : 0f,
                };
                if (table.Get("limitation", out LuaTable limitation))
                {
                    item.module.limitation = limitation.ArrayElements<string>().Select(GetObject<Recipe>).ToArray();
                    foreach (var recipe in item.module.limitation)
                        recipeModules.Add(recipe, item, true);
                } else universalModules.Add(item);
            }

            Product[] launchProducts = null;
            if (table.Get("rocket_launch_product", out LuaTable product))
                launchProducts = LoadProduct(product).SingleElementArray();
            else if (table.Get("rocket_launch_products", out LuaTable products))
                launchProducts = product.ArrayElements<LuaTable>().Select(LoadProduct).ToArray();
            if (launchProducts != null && launchProducts.Length > 0)
            {
                var recipe = CreateSpecialRecipe(item, SpecialNames.RocketLaunch, "launched");
                recipe.ingredients = new[]
                {
                    new Ingredient(item, 1),
                    new Ingredient(rocketLaunch, 1)
                };
                recipe.products = launchProducts;
                recipe.time = 30f; // TODO what to put here?
            }
        }

        private void DeserializeFluid(LuaTable table)
        {
            var fluid = DeserializeCommon<Fluid>(table, "fluid");
            if (table.Get("fuel_value", out string fuelValue))
            {
                fluid.fuelValue = ParseEnergy(fuelValue);
                fuels.Add(SpecialNames.BurnableFluid, fluid);
            }
            if (table.Get("heat_capacity", out string heatCap))
                fluid.heatCapacity = ParseEnergy(heatCap);
            fluid.minTemperature = table.Get("default_temperature", 0f);
            fluid.maxTemperature = table.Get("max_temperature", 0f);
        }

        private Goods LoadItemOrFluid(LuaTable table, string nameField = "name")
        {
            if (table.Get("type", out string type) && type == "fluid")
                return GetRef<Fluid>(table, nameField, out var fluid) ? fluid : null;
            return GetRef<Item>(table,nameField, out var item) ? item : null;
        }

        private bool LoadItemData(out Goods goods, out float amount, LuaTable table)
        {
            if (table.Get("name", out string name))
            {
                goods = LoadItemOrFluid(table);
                table.Get("amount", out amount);
                return true; // true means 'may have extra data'
            }
            else
            {
                table.Get(1, out name);
                table.Get(2, out amount);
                goods = GetObject<Item>(name);
                return false;
            }
        }
        
        private readonly StringBuilder localeBuilder = new StringBuilder();

        private void Localize(LuaTable table)
        {
            if (!table.Get(1, out string key))
                return;
            Localize(key, table);
        }

        private void Localize(string key, LuaTable table)
        {
            if (key == "")
            {
                if (table == null)
                    return;
                foreach (var elem in table.ArrayElements)
                {
                    if (elem is LuaTable sub)
                        Localize(sub);
                    else localeBuilder.Append(elem);
                }
                return;
            }

            key = FactorioLocalization.Localize(key);
            if (key == null)
                return;

            if (!key.Contains("__"))
            {
                localeBuilder.Append(key);
                return;
            }

            using (var parts = ((IEnumerable<string>) key.Split("__")).GetEnumerator())
            {
                while (parts.MoveNext())
                {
                    localeBuilder.Append(parts.Current);
                    if (!parts.MoveNext())
                        break;
                    var control = parts.Current;
                    if (control == "ITEM" || control == "FLUID" || control == "RECIPE" || control == "ENTITY")
                    {
                        if (!parts.MoveNext())
                            break;
                        var subKey = control.ToLowerInvariant() + "-name." + parts.Current;
                        Localize(subKey, null);
                    }
                    else if (control == "CONTROL")
                    {
                        if (!parts.MoveNext())
                            break;
                        localeBuilder.Append(parts.Current);
                    }
                    else if (control == "ALT_CONTROL")
                    {
                        if (!parts.MoveNext() || !parts.MoveNext())
                            break;
                        localeBuilder.Append(parts.Current);
                    }
                    else if (table != null && int.TryParse(control, out var i))
                    {
                        if (table.Get(i + 1, out string s))
                            Localize(s, null);
                        else if (table.Get(i + 1, out LuaTable t))
                            Localize(t);
                    }
                    else if (control.StartsWith("plural"))
                    {
                        localeBuilder.Append("(???)");
                        if (!parts.MoveNext())
                            break;
                    }
                    else
                    {
                        // Not supported token... Append everything else as-is
                        while (parts.MoveNext())
                            localeBuilder.Append(parts.Current);
                        break;
                    }
                }
            }
        }

        private T DeserializeCommon<T>(LuaTable table, string localeType) where T:FactorioObject, new()
        {
            table.Get("name", out string name);
            var target = GetObject<T>(name);
            target.factorioType = table.Get("type", "");
            
            localeBuilder.Clear();
            if (table.Get("localised_name", out LuaTable loc))
                Localize(loc);
            else Localize(localeType + "-name." + target.name, null);
            target.locName = localeBuilder.Length == 0 ? null : localeBuilder.ToString();
            
            localeBuilder.Clear();
            if (table.Get("localised_description", out loc))
                Localize(loc);
            else Localize(localeType + "-description." + target.name, null);
            target.locDescr = localeBuilder.Length == 0 ? null : localeBuilder.ToString();

            table.Get("icon_size", out float defaultIconSize);
            if (table.Get("icon", out string s))
            {
                target.iconSpec = new FactorioIconPart {path = s, size = defaultIconSize}.SingleElementArray();
            } 
            else if (table.Get("icons", out LuaTable iconList))
            {
                target.iconSpec = iconList.ArrayElements<LuaTable>().Select(x =>
                {
                    var part = new FactorioIconPart();
                    x.Get("icon", out part.path);
                    x.Get("icon_size", out part.size, defaultIconSize);
                    x.Get("scale", out part.scale, 1f);
                    if (x.Get("shift", out LuaTable shift))
                    {
                        shift.Get(1, out part.x);
                        shift.Get(2, out part.y);
                    }

                    if (x.Get("tint", out LuaTable tint))
                    {
                        tint.Get("r", out part.r, 1f);
                        tint.Get("g", out part.g, 1f);
                        tint.Get("b", out part.b, 1f);
                        tint.Get("a", out part.a, 1f);
                    }
                    return part;
                }).ToArray();
            }

            return target;
        }
    }
}