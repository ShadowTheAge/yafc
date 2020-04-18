using System;
using System.Collections.Generic;
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
        private bool GetRef<T>(LuaTable table, string key, out T result) where T:FactorioObject, new()
        {
            result = null;
            if (!table.Get(key, out string name))
                return false;
            result = GetObject<T>(name);
            return true;
        }
        
        public void LoadData(LuaTable data, IProgress<(string, string)> progress)
        {
            Console.WriteLine("Loading prototypes and rendering icons");
            var raw = (LuaTable)data["raw"];
            
            foreach (var prototypeName in ((LuaTable)data["Item types"]).ArrayElements<string>())
                DeserializePrototypes(raw, prototypeName, DeserializeItem, progress);
            DeserializePrototypes(raw, "fluid", DeserializeFluid, progress);
            DeserializePrototypes(raw, "recipe", DeserializeRecipe, progress);
            DeserializePrototypes(raw, "technology", DeserializeTechnology, progress);
            foreach (var prototypeName in ((LuaTable) data["Entity types"]).ArrayElements<string>())
                DeserializePrototypes(raw, prototypeName, DeserializeEntity, progress);
            var iconRenderTask = Task.Run(RenderIcons);
            progress.Report(("Post-processing", "Computing maps"));
            CalculateMaps();
            ExportBuiltData();
            progress.Report(("Post-processing", "Calculating dependencies"));
            Dependencies.Calculate();
            progress.Report(("Post-processing", "Calculating milestones"));
            Milestones.CreateDefault();
            progress.Report(("Post-processing", "Calculating complexity"));
            Complexity.CalculateAll();
            progress.Report(("Post-processing", "Rendering icons"));
            iconRenderTask.Wait();
        }

        private void RenderIcons()
        {
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

        private unsafe Icon CreateIconFromSpec(FactorioIconPart[] spec)
        {
            const int IconSize = IconCollection.IconSize;
            var targetSurface = SDL.SDL_CreateRGBSurfaceWithFormat(0, IconSize, IconSize, 0, SDL.SDL_PIXELFORMAT_RGBA8888);
            foreach (var icon in spec)
            {
                var modpath = FactorioDataSource.ResolveModPath("", icon.path);
                var imageSource = FactorioDataSource.ReadModFile(modpath.mod, modpath.path);
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
                    SDL.SDL_BlitSurface(image, ref srcRect, targetSurface, ref targetRect);
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
            GetRef(table,"place_result", out item.placeResult);
            if (table.Get("fuel_value", out string fuelValue))
            {
                item.fuelValue = ParseEnergy(fuelValue);
                GetRef(table,"burnt_result", out item.fuelResult);
                table.Get("fuel_category", out string category);
                fuels.Add(category, item);
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
            table.Get("default_temperature", out fluid.minTemperature);
            table.Get("max_temperature", out fluid.maxTemperature);
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

        private T DeserializeCommon<T>(LuaTable table, string localeType) where T:FactorioObject, new()
        {
            table.Get("name", out string name);
            var target = GetObject<T>(name);
            table.Get("type", out target.type);
            target.locName = FactorioLocalization.Localize(
                table.Get("localised_name", out LuaTable loc) && loc.Get(0, out string locale) ? locale : localeType + "-name." + target.name);
            target.locDescr = FactorioLocalization.Localize(
                table.Get("localised_description", out loc) && loc.Get(0, out locale) ? locale : localeType + "-description." + target.name);

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