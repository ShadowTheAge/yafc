using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SDL2;
using YAFC.Model;
using YAFC.UI;

namespace YAFC.Parser
{    
    public partial class FactorioDataDeserializer
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
        
        public Project LoadData(string projectPath, LuaTable data, IProgress<(string, string)> progress, ErrorCollector errorCollector)
        {
            progress.Report(("Loading", "Loading items"));
            raw = (LuaTable)data["raw"];
            foreach (var prototypeName in ((LuaTable)data["Item types"]).ArrayElements<string>())
                DeserializePrototypes(raw, prototypeName, DeserializeItem, progress);
            recipeModules.SealAndDeduplicate(universalModules.ToArray());
            progress.Report(("Loading", "Loading fluids"));
            DeserializePrototypes(raw, "fluid", DeserializeFluid, progress);
            progress.Report(("Loading", "Loading recipes"));
            DeserializePrototypes(raw, "recipe", DeserializeRecipe, progress);
            progress.Report(("Loading", "Loading technologies"));
            DeserializePrototypes(raw, "technology", DeserializeTechnology, progress);
            progress.Report(("Loading", "Loading entities"));
            foreach (var prototypeName in ((LuaTable) data["Entity types"]).ArrayElements<string>())
                DeserializePrototypes(raw, prototypeName, DeserializeEntity, progress);
            progress.Report(("Post-processing", "Computing maps"));
            // Deterministically sort all objects
            allObjects.Sort((a, b) => a.sortingOrder == b.sortingOrder ? string.Compare(a.typeDotName, b.typeDotName, StringComparison.Ordinal) : a.sortingOrder - b.sortingOrder);
            for (var i = 0; i < allObjects.Count; i++)
                allObjects[i].id = (FactorioId)i;
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
                    if (o.name == "steam-cracking-methane")
                        ;
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

                        o.icon = CreateIconFromSpec(cache, o.iconSpec);
                        if (simpleSprite)
                            simpleSpritesCache[o.iconSpec[0].path] = o.icon;
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
                            image = cache[modpath] = SDL_image.IMG_Load_RW(src, (int) SDL.SDL_bool.SDL_TRUE);
                        }
                    }
                    
                } 
                if (image == IntPtr.Zero)
                    continue;
                
                ref var sdlSurface = ref RenderingUtils.AsSdlSurface(image);
                var targetSize = icon.scale == 1f ? IconSize : MathUtils.Round(sdlSurface.h * icon.scale);
                SDL.SDL_SetSurfaceColorMod(image, MathUtils.FloatToByte(icon.r), MathUtils.FloatToByte(icon.g), MathUtils.FloatToByte(icon.b));
                SDL.SDL_SetSurfaceAlphaMod(image, MathUtils.FloatToByte(icon.a));
                var basePosition = (IconSize - targetSize) / 2;
                var targetRect = new SDL.SDL_Rect
                {
                    x = basePosition,
                    y = basePosition,
                    w = targetSize,
                    h = targetSize
                };
                if (icon.x != 0)
                    targetRect.x += MathUtils.Round(icon.x * IconSize);
                if (icon.y != 0)
                    targetRect.y += MathUtils.Round(icon.y * IconSize);
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

        private bool Localize(LuaTable table, out string result)
        {
            if (!table.Get(1, out result))
                return false;
            if (result == "")
            {
                var sb = new StringBuilder();
                foreach (var elem in table.ArrayElements)
                {
                    if (elem is LuaTable sub)
                        sb.Append(Localize(sub, out var part) ? part : "???");
                    else sb.Append(elem);
                }

                result = sb.ToString();
                return true;
            }
            result = FactorioLocalization.Localize(result, result);
            if (result == null)
                return false;
            for (var i = 1; i < 10; i++)
            {
                var sub = "__" + i + "__";
                if (result.Contains(sub))
                {
                    if (table.Get(i+1, out string s))
                        result = s;
                    else if (table.Get(i + 1, out LuaTable t) && Localize(t, out var rep))
                        result = result.Replace(sub, rep, StringComparison.InvariantCulture);
                    else break;
                } else break;
            }

            if (result.Contains("__"))
            {
                Console.WriteLine("Localization is too complex: Unable to parse "+result);
                return false;
            }
            return true;
        }

        private string LocalizeSimple(string key, string fallback)
        {
            var str = FactorioLocalization.Localize(key);
            if (str == null)
                return null;
            if (str.Contains("__"))
                return fallback;
            return str;
        }

        private T DeserializeCommon<T>(LuaTable table, string localeType) where T:FactorioObject, new()
        {
            table.Get("name", out string name);
            var target = GetObject<T>(name);
            target.factorioType = table.Get("type", "");
            target.locName = table.Get("localised_name", out LuaTable loc) && Localize(loc, out var locale) ? locale : LocalizeSimple(localeType + "-name." + target.name, target.name);
            target.locDescr = table.Get("localised_description", out loc) && Localize(loc, out locale) ? locale : LocalizeSimple(localeType + "-description." + target.name, "Unable to parse localized description");

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