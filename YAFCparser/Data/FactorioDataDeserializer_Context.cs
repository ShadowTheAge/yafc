using System;
using System.Collections.Generic;
using System.Linq;
using YAFC.Model;

namespace YAFC.Parser
{
    internal partial class FactorioDataDeserializer
    {
        private readonly List<FactorioObject> allObjects = new List<FactorioObject>();
        private readonly List<FactorioObject> rootAccessible = new List<FactorioObject>();
        private readonly Dictionary<(Type type, string name), FactorioObject> registeredObjects = new Dictionary<(Type type, string name), FactorioObject>();
        private readonly DataBucket<string, Goods> fuels = new DataBucket<string, Goods>();
        private readonly DataBucket<Entity, string> fuelUsers = new DataBucket<Entity, string>();
        private readonly DataBucket<string, RecipeOrTechnology> recipeCategories = new DataBucket<string, RecipeOrTechnology>();
        private readonly DataBucket<Entity, string> recipeCrafters = new DataBucket<Entity, string>();
        private readonly DataBucket<Recipe, Item> recipeModules = new DataBucket<Recipe, Item>();
        private readonly List<Item> universalModules = new List<Item>();
        private Item[] allModules;
        private readonly HashSet<FactorioObject> milestones = new HashSet<FactorioObject>();
        
        private readonly bool expensiveRecipes;

        private Recipe generatorProduction;
        private Recipe reactorProduction;
        private Special voidEnergy;
        private Special heat;
        private Special electricity;
        private Special rocketLaunch;
        private EntityEnergy voidEntityEnergy;
        private EntityEnergy laborEntityEnergy;
        private Entity character;
        private readonly Version factorioVersion;
        
        private static readonly Version v0_18 = new Version(0, 18);

        public FactorioDataDeserializer(bool expensiveRecipes, Version factorioVersion)
        {
            this.expensiveRecipes = expensiveRecipes;
            this.factorioVersion = factorioVersion;
            RegisterSpecial();
        }

        private Special CreateSpecialObject(bool isPower, string name, string locName, string locDescr, string icon)
        {
            var obj = GetObject<Special>(name);
            obj.factorioType = "special";
            obj.locName = locName;
            obj.locDescr = locDescr;
            obj.iconSpec = new FactorioIconPart{path = icon}.SingleElementArray();
            obj.power = isPower;
            if (isPower)
                obj.fuelValue = 1f;
            return obj;
        }

        private void RegisterSpecial()
        {
            electricity = CreateSpecialObject(true, SpecialNames.Electricity, "Electricity", "This is an object that represents electric energy",
                "__core__/graphics/icons/alerts/electricity-icon-unplugged.png");
            fuels.Add(SpecialNames.Electricity, electricity);

            heat = CreateSpecialObject(true, SpecialNames.Heat, "Heat", "This is an object that represents heat energy", "__core__/graphics/arrows/heat-exchange-indication.png");
            fuels.Add(SpecialNames.Heat, heat);
            
            voidEnergy = CreateSpecialObject(true, SpecialNames.Void, "Void", "This is an object that represents infinite energy", "__core__/graphics/icons/mip/infinity.png");
            fuels.Add(SpecialNames.Void, voidEnergy);
            rootAccessible.Add(voidEnergy);

            rocketLaunch = CreateSpecialObject(false, SpecialNames.RocketLaunch, "Rocket launch", "This is a rocket ready to launch", "__base__/graphics/entity/rocket-silo/02-rocket.png");
            
            generatorProduction = CreateSpecialRecipe(electricity, SpecialNames.GeneratorRecipe, "generating");
            generatorProduction.products = new Product(electricity, 1f).SingleElementArray();
            generatorProduction.flags |= RecipeFlags.ScaleProductionWithPower;
            generatorProduction.ingredients = Array.Empty<Ingredient>();
            
            reactorProduction = CreateSpecialRecipe(heat, SpecialNames.ReactorRecipe, "generating");
            reactorProduction.products = new Product(heat, 1f).SingleElementArray();
            reactorProduction.flags |= RecipeFlags.ScaleProductionWithPower;
            reactorProduction.ingredients = Array.Empty<Ingredient>();

            voidEntityEnergy = new EntityEnergy {type = EntityEnergyType.Void};
            laborEntityEnergy = new EntityEnergy {type = EntityEnergyType.Labor};
        }
        
        private T GetObject<T>(string name) where T : FactorioObject, new()
        {
            var key = (typeof(T), name);
            if (registeredObjects.TryGetValue(key, out FactorioObject existing))
                return existing as T;
            var newItem = new T {name = name};
            allObjects.Add(newItem);
            registeredObjects[key] = newItem;
            return newItem;
        }

        private int Skip(int from, FactorioObjectSortOrder sortOrder)
        {
            for (; from < allObjects.Count; from++)
                if (allObjects[from].sortingOrder != sortOrder)
                    break;
            return from;
        }

        private void ExportBuiltData()
        {
            Database.rootAccessible = rootAccessible.ToArray();
            Database.objectsByTypeName = allObjects.ToDictionary(x => x.typeDotName = x.type + "." + x.name);
            Database.allSciencePacks = milestones.ToArray();
            Database.voidEnergy = voidEnergy;
            Database.electricity = electricity;
            Database.character = character;
            var firstSpecial = 0;
            var firstItem = Skip(firstSpecial, FactorioObjectSortOrder.SpecialGoods);
            var firstFluid = Skip(firstItem, FactorioObjectSortOrder.Items);
            var firstRecipe = Skip(firstFluid, FactorioObjectSortOrder.Fluids);
            var firstMechanics = Skip(firstRecipe, FactorioObjectSortOrder.Recipes);
            var firstTechnology = Skip(firstMechanics, FactorioObjectSortOrder.Mechanics);
            var firstEntity = Skip(firstTechnology, FactorioObjectSortOrder.Technologies);
            var last = Skip(firstEntity, FactorioObjectSortOrder.Entities);
            if (last != allObjects.Count)
                throw new Exception("Something is not right");
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

            Database.allModules = allModules;
            Database.allBeacons = Database.entities.all.Where(x => x.beaconEfficiency > 0f).ToArray();
        }
        
        private void CalculateMaps()
        {
            var itemUsages = new DataBucket<Goods, Recipe>();
            var itemProduction = new DataBucket<Goods, Recipe>();
            var miscSources = new DataBucket<Goods, FactorioObject>();
            var entityPlacers = new DataBucket<Entity, Item>();
            var recipeUnlockers = new DataBucket<RecipeOrTechnology, Technology>();
            // Because actual recipe availibility may be different than just "all recipes from that category" because of item slot limit and fluid usage restriction, calculate it here
            var actualRecipeCrafters = new DataBucket<RecipeOrTechnology, Entity>();
            
            // step 1 - collect maps

            foreach (var o in allObjects)
            {
                switch (o)
                {
                    case Technology technology:
                        foreach (var recipe in technology.unlockRecipes)
                            recipeUnlockers.Add(recipe, technology);
                        break;
                    case Recipe recipe:
                        foreach (var product in recipe.products)
                            if (product.amount > 0)
                                itemProduction.Add(product.goods, recipe);
                        foreach (var ingredient in recipe.ingredients)
                            itemUsages.Add(ingredient.goods, recipe);
                        break;
                    case Item item:
                        if (item.placeResult != null)
                            entityPlacers.Add(item.placeResult, item);
                        if (item.fuelResult != null)
                            miscSources.Add(item.fuelResult, item);
                        break;
                    case Entity entity:
                        foreach (var product in entity.loot)
                            miscSources.Add(product.goods, entity);
                        entity.recipes = new PackedList<RecipeOrTechnology>(recipeCrafters.GetRaw(entity)
                            .SelectMany(x => recipeCategories.GetRaw(x).Where(y => y.CanFit(entity.itemInputs, entity.fluidInputs, entity.inputs))));
                        foreach (var recipeId in entity.recipes.raw)
                            actualRecipeCrafters.Add(allObjects[(int)recipeId] as RecipeOrTechnology, entity, true);
                        break;
                }
            }
            
            // step 2 - fill maps

            foreach (var o in allObjects)
            {
                switch (o)
                {
                    case RecipeOrTechnology recipe:
                        recipe.FallbackLocalization(recipe.mainProduct, "A recipe to create");
                        recipe.technologyUnlock = new PackedList<Technology>(recipeUnlockers.GetRaw(recipe));
                        recipe.crafters = new PackedList<Entity>(actualRecipeCrafters.GetRaw(recipe));
                        break;
                    case Goods goods:
                        goods.usages = itemUsages.GetArray(goods);
                        goods.production = itemProduction.GetArray(goods);
                        goods.miscSources = miscSources.GetArray(goods);
                        if (o is Item item)
                        {
                            if (item.placeResult != null)
                                item.FallbackLocalization(item.placeResult, "An item to build");
                        }
                        break;
                    case Entity entity:
                        entity.itemsToPlace = new PackedList<Item>(entityPlacers.GetRaw(entity));
                        if (entity.energy != null)
                            entity.energy.fuels = new PackedList<Goods>(fuelUsers.GetRaw(entity).SelectMany(fuels.GetRaw));
                        break;
                }
            }

            foreach (var any in allObjects)
            {
                if (any.locName == null)
                    any.locName = any.name;
            }
        }

        private Recipe CreateSpecialRecipe(FactorioObject production, string category, string hint)
        {
            var fullName = category + (category.EndsWith(".") ? "" : ".") + production.name;
            if (registeredObjects.TryGetValue((typeof(Mechanics), fullName), out var recipeRaw))
            {
                (recipeRaw as Recipe).category = category;
                return recipeRaw as Recipe;
            }
            var recipe = GetObject<Mechanics>(fullName);
            recipe.time = 1f;
            recipe.factorioType = SpecialNames.FakeRecipe;
            recipe.name = fullName;
            recipe.iconSpec = production.iconSpec;
            recipe.locName = production.locName + " " + hint;
            recipe.locDescr = production.locDescr;
            recipe.enabled = true;
            recipe.hidden = true;
            recipe.technologyUnlock = new PackedList<Technology>();
            recipe.category = category;
            return recipe;
        }
        
        private class DataBucket<TKey, TValue>: IEqualityComparer<List<TValue>>
        {
            private readonly Dictionary<TKey, IList<TValue>> storage = new Dictionary<TKey, IList<TValue>>();
            private TValue[] def = Array.Empty<TValue>();
            private bool seal;

            // Replaces lists in storage with arrays. List with same contents get replaced with the same arrays
            public void SealAndDeduplicate(TValue[] addExtra = null)
            {
                def = addExtra;
                var mapDict = new Dictionary<List<TValue>, TValue[]>(this);
                var vals = storage.ToArray();
                foreach (var (key, value) in vals)
                {
                    if (!(value is List<TValue> list)) 
                        continue;
                    if (mapDict.TryGetValue(list, out var prev))
                        storage[key] = prev;
                    else
                    {
                        var mergedList = addExtra == null ? list : list.Concat(addExtra); 
                        var arr = mergedList.ToArray();
                        mapDict[list] = arr;
                        storage[key] = arr;
                    }
                }
                seal = true;
            }

            public void Add(TKey key, TValue value, bool checkUnique = false)
            {
                if (seal)
                    throw new InvalidOperationException("Data bucket is sealed");
                if (key == null)
                    return;
                if (!storage.TryGetValue(key, out var list))
                    storage[key] = new List<TValue> {value};
                else if (!checkUnique || !list.Contains(value))
                    list.Add(value);
            }

            public TValue[] GetArray(TKey key)
            {
                if (!storage.TryGetValue(key, out var list))
                    return def;
                return list is TValue[] value ? value : list.ToArray();
            }

            public IList<TValue> GetRaw(TKey key)
            {
                if (!storage.TryGetValue(key, out var list))
                    return storage[key] = def;
                return list;
            }

            public bool Equals(List<TValue> x, List<TValue> y)
            {
                if (x.Count != y.Count)
                    return false;
                var comparer = EqualityComparer<TValue>.Default; 
                for (var i = 0; i < x.Count; i++)
                    if (!comparer.Equals(x[i], y[i]))
                        return false;
                return true;
            }

            public int GetHashCode(List<TValue> obj)
            {
                var count = obj.Count;
                return count == 0 ? 0 : (obj.Count * 347 + obj[0].GetHashCode()) * 347 + obj[count-1].GetHashCode();
            }
        }
    }
}