using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLua;
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
        
        public Project LoadData(string projectPath, LuaTable data, IProgress<(string, string)> progress)
        {
            progress.Report(("Loading", "Loading items"));
            raw = (LuaTable)data["raw"];
            foreach (var prototypeName in ((LuaTable)data["Item types"]).ArrayElements<string>())
                DeserializePrototypes(raw, prototypeName, DeserializeItem, progress);
            recipeModules.SealAndDeduplicate(universalModules);
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
                allObjects[i].id = i;
            var iconRenderTask = Task.Run(RenderIcons);
            CalculateMaps();
            ExportBuiltData();
            progress.Report(("Post-processing", "Calculating dependencies"));
            Dependencies.Calculate();
            progress.Report(("Post-processing", "Creating project"));
            var project = Project.ReadFromFile(projectPath);
            progress.Report(("Post-processing", "Calculating milestones"));
            Milestones.Update(project);
            progress.Report(("Post-processing", "Calculating automatables"));
            AutomationAnalysis.Process();
            progress.Report(("Post-processing", "Calculating costs"));
            CostAnalysis.Process();
            progress.Report(("Post-processing", "Rendering icons"));
            iconRenderTask.Wait();
            return project;
        }

        private Icon CreateSimpleIcon(string graphicsPath)
        {
            return CreateIconFromSpec(new FactorioIconPart {path = "__core__/graphics/" + graphicsPath + ".png"});
        }

        private void RenderIcons()
        {
            DataUtils.NoFuelIcon = CreateSimpleIcon("fuel-icon-red");
            DataUtils.WarningIcon = CreateSimpleIcon("warning-icon");
            DataUtils.HandIcon = CreateSimpleIcon("hand");
            
            var simpleSpritesCache = new Dictionary<string, Icon>();
            
            foreach (var o in allObjects)
            {
                if (o.iconSpec != null && o.iconSpec.Length > 0)
                {
                    var simpleSprite = o.iconSpec.Length == 1 && o.iconSpec[0].IsSimple();
                    if (simpleSprite && simpleSpritesCache.TryGetValue(o.iconSpec[0].path, out var icon))
                    {
                        o.icon = icon;
                        continue;
                    }

                    o.icon = CreateIconFromSpec(o.iconSpec);
                    if (simpleSprite)
                        simpleSpritesCache[o.iconSpec[0].path] = o.icon;
                }
                else if (o is Recipe recipe && recipe.mainProduct != null)
                    o.icon = recipe.mainProduct.icon;
            }
        }

        private unsafe Icon CreateIconFromSpec(params FactorioIconPart[] spec)
        {
            const int IconSize = IconCollection.IconSize;
            var targetSurface = SDL.SDL_CreateRGBSurfaceWithFormat(0, IconSize, IconSize, 0, SDL.SDL_PIXELFORMAT_RGBA8888);
            foreach (var icon in spec)
            {
                var modpath = FactorioDataSource.ResolveModPath("", icon.path);
                var imageSource = FactorioDataSource.ReadModFile(modpath.mod, modpath.path);
                if (imageSource == null)
                    continue;
                fixed (byte* data = imageSource)
                {
                    var src = SDL.SDL_RWFromMem((IntPtr) data, imageSource.Length);
                    var image = SDL_image.IMG_Load_RW(src, (int) SDL.SDL_bool.SDL_TRUE);
                    var targetSize = MathUtils.Round(IconSize * icon.scale);
                    if (icon.r != 1f || icon.g != 1f || icon.b != 1f)
                        SDL.SDL_SetSurfaceColorMod(image, MathUtils.FloatToByte(icon.r), MathUtils.FloatToByte(icon.g), MathUtils.FloatToByte(icon.b));
                    if (icon.a != 1f)
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
                        targetRect.x += MathUtils.Round(icon.x * IconSize / icon.size);
                    if (icon.y != 0)
                        targetRect.y += MathUtils.Round(icon.y * IconSize / icon.size);
                    ref var sdlSurface = ref RenderingUtils.AsSdlSurface(image);
                    var srcRect = new SDL.SDL_Rect
                    {
                        w = sdlSurface.h, // That is correct (cutting mip maps)
                        h = sdlSurface.h
                    };
                    SDL.SDL_BlitScaled(image, ref srcRect, targetSurface, ref targetRect);
                    SDL.SDL_FreeSurface(image);
                }
            }

            return IconCollection.AddIcon(targetSurface);
        }

        private void DeserializePrototypes(LuaTable data, string type, Action<LuaTable> deserializer, IProgress<(string, string)> progress)
        {
            var table = data[type];
            progress.Report(("Building objects", type));
            if (!(table is LuaTable luaTable))
                return;
            foreach (var entry in luaTable.Values)
                if (entry is LuaTable entryTable)
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
            
            if (item.type == "module" && table.Get("effect", out LuaTable moduleEffect))
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
            result = FactorioLocalization.Localize(result);
            if (result == null)
                return false;
            for (var i = 1; i < 10; i++)
            {
                var sub = "__" + i + "__";
                if (result.Contains(sub) && table.Get(i + 1, out LuaTable t) && Localize(t, out var rep))
                    result = result.Replace(sub, rep, StringComparison.InvariantCulture);
                else break;
            }

            return true;
        }

        private T DeserializeCommon<T>(LuaTable table, string localeType) where T:FactorioObject, new()
        {
            table.Get("name", out string name);
            var target = GetObject<T>(name);
            target.type = table.Get("type", "");
            target.locName = table.Get("localised_name", out LuaTable loc) && Localize(loc, out var locale) ? locale : FactorioLocalization.Localize(localeType + "-name." + target.name);
            target.locDescr = table.Get("localised_description", out loc) && Localize(loc, out locale) ? locale : FactorioLocalization.Localize(localeType + "-description." + target.name);

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