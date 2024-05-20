using System;
using System.Collections.Generic;
using System.Linq;
using Yafc.Model;

namespace Yafc.Parser {
    internal partial class FactorioDataDeserializer {
        private readonly List<FactorioObject> allObjects = [];
        private readonly List<FactorioObject> rootAccessible = [];
        private readonly Dictionary<(Type? type, string? name), FactorioObject> registeredObjects = [];
        private readonly DataBucket<string, Goods> fuels = new DataBucket<string, Goods>();
        private readonly DataBucket<Entity, string> fuelUsers = new DataBucket<Entity, string>();
        private readonly DataBucket<string, RecipeOrTechnology> recipeCategories = new DataBucket<string, RecipeOrTechnology>();
        private readonly DataBucket<EntityCrafter, string> recipeCrafters = new DataBucket<EntityCrafter, string>();
        private readonly DataBucket<Recipe, Module> recipeModules = new DataBucket<Recipe, Module>();
        private readonly Dictionary<Item, List<string>> placeResults = [];
        private readonly List<Module> universalModules = [];
        private readonly List<Module> allModules = [];
        private readonly HashSet<Item> sciencePacks = [];
        private readonly Dictionary<string, List<Fluid>> fluidVariants = [];
        private readonly Dictionary<string, FactorioObject> formerAliases = [];
        private readonly Dictionary<string, int> rocketInventorySizes = [];

        private readonly bool expensiveRecipes;

        private readonly Recipe generatorProduction;
        private readonly Recipe reactorProduction;
        private readonly Special voidEnergy;
        private readonly Special heat;
        private readonly Special electricity;
        private readonly Special rocketLaunch;
        private readonly EntityEnergy voidEntityEnergy;
        private readonly EntityEnergy laborEntityEnergy;
        private Entity? character;
        private readonly Version factorioVersion;

        private static readonly Version v0_18 = new Version(0, 18);

        public FactorioDataDeserializer(bool expensiveRecipes, Version factorioVersion) {
            this.expensiveRecipes = expensiveRecipes;
            this.factorioVersion = factorioVersion;

            Special createSpecialObject(bool isPower, string name, string locName, string locDescr, string icon, string signal) {
                var obj = GetObject<Special>(name);
                obj.virtualSignal = signal;
                obj.factorioType = "special";
                obj.locName = locName;
                obj.locDescr = locDescr;
                obj.iconSpec = new FactorioIconPart(icon).SingleElementArray();
                obj.power = isPower;
                if (isPower) {
                    obj.fuelValue = 1f;
                }

                return obj;
            }

            electricity = createSpecialObject(true, SpecialNames.Electricity, "Electricity", "This is an object that represents electric energy",
                "__core__/graphics/icons/alerts/electricity-icon-unplugged.png", "signal-E");
            fuels.Add(SpecialNames.Electricity, electricity);

            heat = createSpecialObject(true, SpecialNames.Heat, "Heat", "This is an object that represents heat energy", "__core__/graphics/arrows/heat-exchange-indication.png", "signal-H");
            fuels.Add(SpecialNames.Heat, heat);

            voidEnergy = createSpecialObject(true, SpecialNames.Void, "Void", "This is an object that represents infinite energy", "__core__/graphics/icons/mip/infinity.png", "signal-V");
            fuels.Add(SpecialNames.Void, voidEnergy);
            rootAccessible.Add(voidEnergy);

            rocketLaunch = createSpecialObject(false, SpecialNames.RocketLaunch, "Rocket launch slot", "This is a slot in a rocket ready to be launched", "__base__/graphics/entity/rocket-silo/02-rocket.png", "signal-R");

            generatorProduction = CreateSpecialRecipe(electricity, SpecialNames.GeneratorRecipe, "generating");
            generatorProduction.products = new Product(electricity, 1f).SingleElementArray();
            generatorProduction.flags |= RecipeFlags.ScaleProductionWithPower;
            generatorProduction.ingredients = [];

            reactorProduction = CreateSpecialRecipe(heat, SpecialNames.ReactorRecipe, "generating");
            reactorProduction.products = new Product(heat, 1f).SingleElementArray();
            reactorProduction.flags |= RecipeFlags.ScaleProductionWithPower;
            reactorProduction.ingredients = [];

            voidEntityEnergy = new EntityEnergy { type = EntityEnergyType.Void, effectivity = float.PositiveInfinity };
            laborEntityEnergy = new EntityEnergy { type = EntityEnergyType.Labor, effectivity = float.PositiveInfinity };
        }

        private T GetObject<T>(string name) where T : FactorioObject, new() {
            return GetObject<T, T>(name);
        }

        private TActual GetObject<TNominal, TActual>(string name) where TNominal : FactorioObject where TActual : TNominal, new() {
            var key = (typeof(TNominal), name);
            if (registeredObjects.TryGetValue(key, out FactorioObject? existing)) {
                return (TActual)existing;
            }

            TActual newItem = new TActual { name = name };
            allObjects.Add(newItem);
            registeredObjects[key] = newItem;
            return newItem;
        }

        private int Skip(int from, FactorioObjectSortOrder sortOrder) {
            for (; from < allObjects.Count; from++) {
                if (allObjects[from].sortingOrder != sortOrder) {
                    break;
                }
            }

            return from;
        }

        private void ExportBuiltData() {
            Database.rootAccessible = rootAccessible.ToArray();
            Database.objectsByTypeName = allObjects.ToDictionary(x => x.typeDotName = x.type + "." + x.name);
            foreach (var alias in formerAliases) {
                _ = Database.objectsByTypeName.TryAdd(alias.Key, alias.Value);
            }

            Database.allSciencePacks = sciencePacks.ToArray();
            Database.voidEnergy = voidEnergy;
            Database.electricity = electricity;
            Database.electricityGeneration = generatorProduction;
            Database.heat = heat;
            Database.character = character;
            int firstSpecial = 0;
            int firstItem = Skip(firstSpecial, FactorioObjectSortOrder.SpecialGoods);
            int firstFluid = Skip(firstItem, FactorioObjectSortOrder.Items);
            int firstRecipe = Skip(firstFluid, FactorioObjectSortOrder.Fluids);
            int firstMechanics = Skip(firstRecipe, FactorioObjectSortOrder.Recipes);
            int firstTechnology = Skip(firstMechanics, FactorioObjectSortOrder.Mechanics);
            int firstEntity = Skip(firstTechnology, FactorioObjectSortOrder.Technologies);
            int last = Skip(firstEntity, FactorioObjectSortOrder.Entities);
            if (last != allObjects.Count) {
                throw new Exception("Something is not right");
            }

            Database.objects = new FactorioIdRange<FactorioObject>(0, last, allObjects);
            Database.specials = new FactorioIdRange<Special>(firstSpecial, firstItem, allObjects);
            Database.items = new FactorioIdRange<Item>(firstItem, firstFluid, allObjects);
            Database.fluids = new FactorioIdRange<Fluid>(firstFluid, firstRecipe, allObjects);
            Database.goods = new FactorioIdRange<Goods>(firstSpecial, firstRecipe, allObjects);
            Database.recipes = new FactorioIdRange<Recipe>(firstRecipe, firstTechnology, allObjects);
            Database.mechanics = new FactorioIdRange<Mechanics>(firstMechanics, firstTechnology, allObjects);
            Database.recipesAndTechnologies = new FactorioIdRange<RecipeOrTechnology>(firstRecipe, firstEntity, allObjects);
            Database.technologies = new FactorioIdRange<Technology>(firstTechnology, firstEntity, allObjects);
            Database.entities = new FactorioIdRange<Entity>(firstEntity, last, allObjects);
            Database.fluidVariants = fluidVariants;

            Database.allModules = [.. allModules];
            Database.allBeacons = Database.entities.all.OfType<EntityBeacon>().ToArray();
            Database.allCrafters = Database.entities.all.OfType<EntityCrafter>().ToArray();
            Database.allBelts = Database.entities.all.OfType<EntityBelt>().ToArray();
            Database.allInserters = Database.entities.all.OfType<EntityInserter>().ToArray();
            Database.allAccumulators = Database.entities.all.OfType<EntityAccumulator>().ToArray();
            Database.allContainers = Database.entities.all.OfType<EntityContainer>().ToArray();
        }

        private static bool AreInverseRecipes(Recipe packing, Recipe unpacking) {
            var packedProduct = packing.products[0];

            // Check for deterministic production
            if (packedProduct.probability != 1f || unpacking.products.Any(p => p.probability != 1)) {
                return false;
            }
            if (packedProduct.amountMin != packedProduct.amountMax || unpacking.products.Any(p => p.amountMin != p.amountMax)) {
                return false;
            }
            if (unpacking.ingredients.Length != 1 || packing.ingredients.Length != unpacking.products.Length) {
                return false;
            }

            // Check for 'packing.ingredients == unpacking.products'.
            float ratio = 0;
            Recipe? largerRecipe = null;

            // Check for 'packing.ingredients == unpacking.products'.
            if (!checkRatios(packing, unpacking, ref ratio, ref largerRecipe)) {
                return false;
            }

            // Check for 'unpacking.ingredients == packing.products'.
            if (!checkRatios(unpacking, packing, ref ratio, ref largerRecipe)) {
                return false;
            }

            // Some mods add productivity permissions to the recipes, but not to the crafters; still allow these to be matched as inverses.
            // TODO: Consider removing this check entirely?
            if ((unpacking.crafters.OfType<EntityWithModules>().Any(c => c.moduleSlots > 0 && c.allowedEffects.HasFlag(AllowedEffects.Productivity)) && unpacking.IsProductivityAllowed())
                || (packing.crafters.OfType<EntityWithModules>().Any(c => c.moduleSlots > 0 && c.allowedEffects.HasFlag(AllowedEffects.Productivity)) && packing.IsProductivityAllowed())) {
                return false;
            }

            return true;


            // Test to see if running `first` M times and `second` once, or vice versa, can reproduce all the original input.
            // Track which recipe is larger to keep ratio an integer and prevent floating point rounding issues.
            static bool checkRatios(Recipe first, Recipe second, ref float ratio, ref Recipe? larger) {
                Dictionary<Goods, float> ingredients = [];

                foreach (var item in first.ingredients) {
                    if (ingredients.ContainsKey(item.goods)) {
                        return false; // Refuse to deal with duplicate ingredients.
                    }
                    ingredients[item.goods] = item.amount;
                }

                foreach (var item in second.products) {
                    if (!ingredients.TryGetValue(item.goods, out float count)) {
                        return false;
                    }
                    if (count > item.amount) {
                        if (!checkProportions(first, count, item.amount, ref ratio, ref larger)) {
                            return false;
                        }
                    }
                    else if (count == item.amount) {
                        if (ratio != 0 && ratio != 1) {
                            return false;
                        }
                        ratio = 1;
                    }
                    else {
                        if (!checkProportions(second, item.amount, count, ref ratio, ref larger)) {
                            return false;
                        }
                    }
                }
                return true;
            }

            // Within the previous check, make sure the ratio is an integer.
            // If the ratio was set by a previous ingredient/product Goods, make sure this ratio matches the previous one.
            static bool checkProportions(Recipe currentLargerRecipe, float largerCount, float smallerCount, ref float ratio, ref Recipe? larger) {
                if (largerCount / smallerCount != MathF.Floor(largerCount / smallerCount)) {
                    return false;
                }
                if (ratio != 0 && ratio != largerCount / smallerCount) {
                    return false;
                }
                if (larger != null && larger != currentLargerRecipe) {
                    return false;
                }
                ratio = largerCount / smallerCount;
                larger = currentLargerRecipe;
                return true;
            }
        }

        /// <summary>
        /// Locates and stores all the links between different objects, e.g. which crafters can be used by a recipe, which recipes produce a particular product, and so on.
        /// </summary>
        /// <param name="netProduction">If <see langword="true"/>, recipe selection windows will only display recipes that provide net production or consumption of the <see cref="Goods"/> in question.
        /// If <see langword="false"/>, recipe selection windows will show all recipes that produce or consume any quantity of that <see cref="Goods"/>.<br/>
        /// For example, Kovarex enrichment will appear for both production and consumption of both U-235 and U-238 when <see langword="false"/>,
        /// but will appear as only producing U-235 and consuming U-238 when <see langword="true"/>.</param>
        private void CalculateMaps(bool netProduction) {
            DataBucket<Goods, Recipe> itemUsages = new DataBucket<Goods, Recipe>();
            DataBucket<Goods, Recipe> itemProduction = new DataBucket<Goods, Recipe>();
            DataBucket<Goods, FactorioObject> miscSources = new DataBucket<Goods, FactorioObject>();
            DataBucket<Entity, Item> entityPlacers = new DataBucket<Entity, Item>();
            DataBucket<Recipe, Technology> recipeUnlockers = new DataBucket<Recipe, Technology>();
            // Because actual recipe availability may be different than just "all recipes from that category" because of item slot limit and fluid usage restriction, calculate it here
            DataBucket<RecipeOrTechnology, EntityCrafter> actualRecipeCrafters = new DataBucket<RecipeOrTechnology, EntityCrafter>();
            DataBucket<Goods, Entity> usageAsFuel = new DataBucket<Goods, Entity>();
            List<Recipe> allRecipes = [];
            List<Mechanics> allMechanics = [];

            // step 1 - collect maps

            foreach (var o in allObjects) {
                switch (o) {
                    case Technology technology:
                        foreach (var recipe in technology.unlockRecipes) {
                            recipeUnlockers.Add(recipe, technology);
                        }

                        break;
                    case Recipe recipe:
                        allRecipes.Add(recipe);
                        foreach (var product in recipe.products) {
                            // If the ingredient has variants and is an output, we aren't doing catalyst things: water@15-90 to water@90 does produce water@90,
                            // even if it consumes 10 water@15-90 to produce 9 water@90.
                            Ingredient? ingredient = recipe.ingredients.FirstOrDefault(i => i.goods == product.goods && i.variants is null);
                            float inputAmount = netProduction ? (ingredient?.amount ?? 0) : 0;
                            float outputAmount = product.amount;
                            if (outputAmount > inputAmount) {
                                itemProduction.Add(product.goods, recipe);
                            }
                        }

                        foreach (var ingredient in recipe.ingredients) {
                            // The reverse also applies. 9 water@15-90 to produce 10 water@15 consumes water@90, even though it's a net water producer.
                            float inputAmount = ingredient.amount;
                            Product? product = ingredient.variants is null ? recipe.products.FirstOrDefault(p => p.goods == ingredient.goods) : null;
                            float outputAmount = netProduction ? (product?.amount ?? 0) : 0;

                            if (ingredient.variants == null && inputAmount > outputAmount) {
                                itemUsages.Add(ingredient.goods, recipe);
                            }
                            else if (ingredient.variants != null) {
                                ingredient.goods = ingredient.variants[0];
                                foreach (var variant in ingredient.variants) {
                                    itemUsages.Add(variant, recipe);
                                }
                            }
                        }
                        if (recipe is Mechanics mechanics) {
                            allMechanics.Add(mechanics);
                        }

                        break;
                    case Item item:
                        if (placeResults.TryGetValue(item, out var placeResultNames)) {
                            item.placeResult = GetObject<Entity>(placeResultNames[0]);
                            foreach (string name in placeResultNames) {
                                entityPlacers.Add(GetObject<Entity>(name), item);
                            }
                        }
                        if (item.fuelResult != null) {
                            miscSources.Add(item.fuelResult, item);
                        }

                        break;
                    case Entity entity:
                        foreach (var product in entity.loot) {
                            miscSources.Add(product.goods, entity);
                        }

                        if (entity is EntityCrafter crafter) {
                            crafter.recipes = recipeCrafters.GetRaw(crafter).SelectMany(x => recipeCategories.GetRaw(x).Where(y => y.CanFit(crafter.itemInputs, crafter.fluidInputs, crafter.inputs))).ToArray();
                            foreach (var recipe in crafter.recipes) {
                                actualRecipeCrafters.Add(recipe, crafter, true);
                            }
                        }
                        if (entity.energy != null && entity.energy != voidEntityEnergy) {
                            var fuelList = fuelUsers.GetRaw(entity).SelectMany(fuels.GetRaw);
                            if (entity.energy.type == EntityEnergyType.FluidHeat) {
                                fuelList = fuelList.Where(x => x is Fluid f && entity.energy.acceptedTemperature.Contains(f.temperature) && f.temperature > entity.energy.workingTemperature.min);
                            }

                            var fuelListArr = fuelList.ToArray();
                            entity.energy.fuels = fuelListArr;
                            foreach (var fuel in fuelListArr) {
                                usageAsFuel.Add(fuel, entity);
                            }
                        }
                        break;
                }
            }

            voidEntityEnergy.fuels = [voidEnergy];

            actualRecipeCrafters.Seal();
            usageAsFuel.Seal();
            recipeUnlockers.Seal();
            entityPlacers.Seal();

            // step 2 - fill maps

            foreach (var o in allObjects) {
                switch (o) {
                    case RecipeOrTechnology recipeOrTechnology:
                        if (recipeOrTechnology is Recipe recipe) {
                            recipe.FallbackLocalization(recipe.mainProduct, "A recipe to create");
                            recipe.technologyUnlock = recipeUnlockers.GetArray(recipe);
                        }
                        recipeOrTechnology.crafters = actualRecipeCrafters.GetArray(recipeOrTechnology);
                        break;
                    case Goods goods:
                        goods.usages = itemUsages.GetArray(goods);
                        goods.production = itemProduction.GetArray(goods);
                        goods.miscSources = miscSources.GetArray(goods);
                        if (o is Item item) {
                            if (item.placeResult != null) {
                                item.FallbackLocalization(item.placeResult, "An item to build");
                            }
                        }
                        else if (o is Fluid fluid && fluid.variants != null) {
                            string temperatureDescr = "Temperature: " + fluid.temperature + "°";
                            if (fluid.locDescr == null) {
                                fluid.locDescr = temperatureDescr;
                            }
                            else {
                                fluid.locDescr = temperatureDescr + "\n" + fluid.locDescr;
                            }
                        }

                        goods.fuelFor = usageAsFuel.GetArray(goods);
                        break;
                    case Entity entity:
                        entity.itemsToPlace = entityPlacers.GetArray(entity);
                        break;
                }
            }

            foreach (var mechanic in allMechanics) {
                mechanic.locName = mechanic.source.locName + " " + mechanic.locName;
                mechanic.locDescr = mechanic.source.locDescr;
                mechanic.iconSpec = mechanic.source.iconSpec;
            }

            // step 3 - detect packing/unpacking (e.g. barreling/unbarreling, stacking/unstacking, etc.) and voiding recipes
            foreach (var recipe in allRecipes) {
                if (recipe.specialType != FactorioObjectSpecialType.Normal) {
                    continue;
                }

                if (recipe.products.Length == 0) {
                    recipe.specialType = FactorioObjectSpecialType.Voiding;
                    continue;
                }
                if (recipe.products.Length != 1 || recipe.ingredients.Length == 0) {
                    continue;
                }

                Goods packed = recipe.products[0].goods;
                if (countNonDsrRecipes(packed.usages) != 1 && countNonDsrRecipes(packed.production) != 1) {
                    continue;
                }

                if (recipe.ingredients.Sum(i => i.amount) <= recipe.products.Sum(p => p.amount)) {
                    // If `recipe` is part of packing/unpacking pair, it's the unpacking half. Ignore it until we find the packing half of the pair.
                    continue;
                }

                foreach (var unpacking in packed.usages) {
                    if (AreInverseRecipes(recipe, unpacking)) {
                        if (packed is Fluid && unpacking.products.All(p => p.goods is Fluid)) {
                            recipe.specialType = FactorioObjectSpecialType.Pressurization;
                            unpacking.specialType = FactorioObjectSpecialType.Pressurization;
                            packed.specialType = FactorioObjectSpecialType.Pressurization;
                        }
                        else if (packed is Item && unpacking.products.All(p => p.goods is Item)) {
                            if (unpacking.products.Length == 1) {
                                recipe.specialType = FactorioObjectSpecialType.Stacking;
                                unpacking.specialType = FactorioObjectSpecialType.Stacking;
                                packed.specialType = FactorioObjectSpecialType.Stacking;
                            }
                            else {
                                recipe.specialType = FactorioObjectSpecialType.Crating;
                                unpacking.specialType = FactorioObjectSpecialType.Crating;
                                packed.specialType = FactorioObjectSpecialType.Crating;
                            }
                        }
                        else if (packed is Item && unpacking.products.Any(p => p.goods is Item) && unpacking.products.Any(p => p.goods is Fluid)) {
                            recipe.specialType = FactorioObjectSpecialType.Barreling;
                            unpacking.specialType = FactorioObjectSpecialType.Barreling;
                            packed.specialType = FactorioObjectSpecialType.Barreling;
                        }
                        else { continue; }

                        // The packed good is used in other recipes or is fuel, constructs a building, or is a module. Only the unpacking recipe should be flagged as special.
                        if (countNonDsrRecipes(packed.usages) != 1 || (packed is Item item && (item.fuelValue != 0 || item.placeResult != null || item is Module))) {
                            recipe.specialType = FactorioObjectSpecialType.Normal;
                            packed.specialType = FactorioObjectSpecialType.Normal;
                        }

                        // The packed good can be mined or has a non-packing source. Only the packing recipe should be flagged as special.
                        if (packed.miscSources.OfType<Entity>().Any() || countNonDsrRecipes(packed.production) > 1) {
                            unpacking.specialType = FactorioObjectSpecialType.Normal;
                            packed.specialType = FactorioObjectSpecialType.Normal;
                        }
                    }
                }
            }

            foreach (var any in allObjects) {
                any.locName ??= any.name;
            }

            foreach (var (_, list) in fluidVariants) {
                foreach (var fluid in list) {
                    fluid.locName += " " + fluid.temperature + "°";
                }
            }
            // The recipes added by deadlock_stacked_recipes (with CompressedFluids, if present) need to be filtered out to get decent results.
            static int countNonDsrRecipes(IEnumerable<Recipe> recipes) {
                return recipes.Count(r => !r.name.Contains("StackedRecipe-") && !r.name.Contains("DSR_HighPressure-"));
            }
        }

        private Recipe CreateSpecialRecipe(FactorioObject production, string category, string hint) {
            string fullName = category + (category.EndsWith('.') ? "" : ".") + production.name;
            if (registeredObjects.TryGetValue((typeof(Mechanics), fullName), out var recipeRaw)) {
                return (Recipe)recipeRaw;
            }

            var recipe = GetObject<Mechanics>(fullName);
            recipe.time = 1f;
            recipe.factorioType = SpecialNames.FakeRecipe;
            recipe.name = fullName;
            recipe.source = production;
            recipe.locName = hint;
            recipe.enabled = true;
            recipe.hidden = true;
            recipe.technologyUnlock = [];
            recipeCategories.Add(category, recipe);
            return recipe;
        }

        private class DataBucket<TKey, TValue> : IEqualityComparer<List<TValue>> where TKey : notnull where TValue : notnull {
            private readonly Dictionary<TKey, IList<TValue>> storage = [];
            /// <summary>This function provides a default list of values for the key for when the key is not present in the storage.</summary>
            /// <remarks>The provided function must *must not* return null.</remarks>
            private Func<TKey, IEnumerable<TValue>> defaultList = NoExtraItems;

            /// <summary>When true, it is not allowed to add new items to this bucket.</summary>
            private bool isSealed;

            /// <summary>
            /// Replaces the list values in storage with array values while (optionally) adding extra values depending on the item.
            /// </summary>
            /// <param name="addExtraItems">Function to provide extra items, *must not* return null.</param>
            public void Seal(Func<TKey, IEnumerable<TValue>>? addExtraItems = null) {
                if (isSealed) {
                    throw new InvalidOperationException("Data bucket is already sealed");
                }

                if (addExtraItems != null) {
                    defaultList = addExtraItems;
                }

                KeyValuePair<TKey, IList<TValue>>[] values = storage.ToArray();
                foreach ((TKey key, IList<TValue> value) in values) {
                    if (value is not List<TValue> list) {
                        // Unexpected type, (probably) never happens
                        continue;
                    }

                    // Add the extra values to the list when provided before storing the complete array.
                    IEnumerable<TValue> completeList = addExtraItems != null ? list.Concat(addExtraItems(key)) : list;
                    TValue[] completeArray = completeList.ToArray();

                    storage[key] = completeArray;
                }

                isSealed = true;
            }

            public void Add(TKey key, TValue value, bool checkUnique = false) {
                if (isSealed) {
                    throw new InvalidOperationException("Data bucket is sealed");
                }

                if (key == null) {
                    return;
                }

                if (!storage.TryGetValue(key, out var list)) {
                    storage[key] = [value];
                }
                else if (!checkUnique || !list.Contains(value)) {
                    list.Add(value);
                }
            }

            public TValue[] GetArray(TKey key) {
                if (!storage.TryGetValue(key, out var list)) {
                    return defaultList(key).ToArray();
                }

                return list is TValue[] value ? value : list.ToArray();
            }

            public IList<TValue> GetRaw(TKey key) {
                if (!storage.TryGetValue(key, out var list)) {
                    list = defaultList(key).ToList();
                    if (isSealed) {
                        list = list.ToArray();
                    }

                    storage[key] = list;
                }
                return list;
            }

            ///<summary>Just return an empty enumerable.</summary>
            private static IEnumerable<TValue> NoExtraItems(TKey item) {
                return [];
            }

            public bool Equals(List<TValue>? x, List<TValue>? y) {
                if (x is null && y is null) {
                    return true;
                }

                if (x is null || y is null || x.Count != y.Count) {
                    return false;
                }

                var comparer = EqualityComparer<TValue>.Default;
                for (int i = 0; i < x.Count; i++) {
                    if (!comparer.Equals(x[i], y[i])) {
                        return false;
                    }
                }

                return true;
            }

            public int GetHashCode(List<TValue> obj) {
                int count = obj.Count;
                return count == 0 ? 0 : (((obj.Count * 347) + obj[0].GetHashCode()) * 347) + obj[count - 1].GetHashCode();
            }
        }

        public Type? TypeNameToType(string? typeName) {
            return typeName switch {
                "item" => typeof(Item),
                "fluid" => typeof(Fluid),
                "technology" => typeof(Technology),
                "recipe" => typeof(Recipe),
                "entity" => typeof(Entity),
                _ => null,
            };
        }

        private void ParseModYafcHandles(LuaTable? scriptEnabled) {
            if (scriptEnabled != null) {
                foreach (object? element in scriptEnabled.ArrayElements) {
                    if (element is LuaTable table) {
                        _ = table.Get("type", out string? type);
                        _ = table.Get("name", out string? name);
                        if (registeredObjects.TryGetValue((TypeNameToType(type), name), out var existing)) {
                            rootAccessible.Add(existing);
                        }
                    }
                }
            }
        }
    }
}
