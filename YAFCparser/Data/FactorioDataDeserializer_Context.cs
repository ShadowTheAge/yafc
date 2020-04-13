using System;
using System.Collections.Generic;
using System.Linq;
using NLua;
using YAFC.Model;

namespace YAFC.Parser
{
    public partial class FactorioDataDeserializer
    {
        private readonly List<FactorioObject> allObjects = new List<FactorioObject>();
        private readonly List<Goods> allGoods = new List<Goods>();
        private readonly List<FactorioObject> rootAccessible = new List<FactorioObject>();
        private readonly Dictionary<(Type type, string name), FactorioObject> registeredObjects = new Dictionary<(Type type, string name), FactorioObject>();
        private readonly DataBucket<string, Goods> fuels = new DataBucket<string, Goods>();
        private readonly DataBucket<Entity, string> fuelUsers = new DataBucket<Entity, string>();
        private readonly DataBucket<string, Recipe> recipeCategories = new DataBucket<string, Recipe>();
        private readonly DataBucket<Entity, string> recipeCrafters = new DataBucket<Entity, string>();
        private readonly HashSet<FactorioObject> milestones = new HashSet<FactorioObject>();
        
        private readonly bool expensiveRecipes;
        private readonly LuaTable emptyTable;
        
        private Recipe generatorProduction;
        private Recipe reactorProduction;
        private Special voidEnergy;
        private Special heat;
        private Special electricity;
        private Special rocketLaunch;
        private EntityEnergy voidEntityEnergy;
        private Version factorioVersion;
        
        private static Version v0_18 = new Version(0, 18);

        public FactorioDataDeserializer(bool expensiveRecipes, LuaTable emptyTable, Version factorioVersion)
        {
            this.expensiveRecipes = expensiveRecipes;
            this.emptyTable = emptyTable;
            this.factorioVersion = factorioVersion;
            RegisterSpecial();
        }

        private Special CreateSpecialObject(bool isPower, string name, string locName, string locDescr, string icon)
        {
            var obj = GetObject<Special>(name);
            obj.type = "special";
            obj.locName = locName;
            obj.locDescr = locDescr;
            obj.iconSpec = new FactorioIconPart{path = icon, size = 32}.SingleElementArray();
            obj.isPower = isPower;
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
            
            voidEnergy = CreateSpecialObject(true, SpecialNames.Void, "Void", "This is an object that represents infinite energy", "__core__/graphics/icons/trash/infinity.png");
            fuels.Add(SpecialNames.Void, voidEnergy);
            rootAccessible.Add(voidEnergy);

            rocketLaunch = CreateSpecialObject(false, SpecialNames.RocketLaunch, "Rocket launch", "This is a rocket ready to launch", "__base__/graphics/entity/rocket-silo/02-11-rocket/02-rocket.png");
            
            generatorProduction = CreateSpecialRecipe( electricity, SpecialNames.GeneratorRecipe, "generating");
            generatorProduction.products = new Product {goods = electricity, amount = 1f}.SingleElementArray();
            generatorProduction.flags |= RecipeFlags.ScaleProductionWithPower;
            generatorProduction.ingredients = Array.Empty<Ingredient>();
            
            reactorProduction = CreateSpecialRecipe( heat, SpecialNames.ReactorRecipe, "generating");
            reactorProduction.products = new Product {goods = heat, amount = 1f}.SingleElementArray();
            reactorProduction.flags |= RecipeFlags.ScaleProductionWithPower;
            reactorProduction.ingredients = Array.Empty<Ingredient>();

            voidEntityEnergy = new EntityEnergy();
        }
        
        private T GetObject<T>(string name) where T : FactorioObject, new()
        {
            var key = (typeof(T), name);
            if (registeredObjects.TryGetValue(key, out FactorioObject existing))
                return existing as T;
            var newItem = new T {name = name, id = allObjects.Count};
            allObjects.Add(newItem);
            registeredObjects[key] = newItem;
            return newItem;
        }

        private void ExportBuiltData()
        {
            Database.allObjects = allObjects.ToArray();
            Database.allGoods = allGoods.ToArray();
            Database.rootAccessible = rootAccessible.ToArray();
            Database.objectsByTypeName = new Dictionary<(string, string), FactorioObject>();
            foreach (var obj in allObjects)
                Database.objectsByTypeName[(obj.type, obj.name)] = obj;
            Database.defaultMilestones = milestones.ToArray();
            Database.voidEnergy = voidEnergy;
        }
        
        private void CalculateMaps()
        {
            var itemUsages = new DataBucket<Goods, Recipe>();
            var itemProduction = new DataBucket<Goods, Recipe>();
            var itemLoot = new DataBucket<Goods, Entity>();
            var entityPlacers = new DataBucket<Entity, Item>();
            var recipeUnlockers = new DataBucket<Recipe, Technology>();
            // Because actual recipe availibility may be different than just "all recipes from that category" because of item slot limit and fluid usage restriction, calculate it here
            var actualRecipeCrafters = new DataBucket<Recipe, Entity>();
            
            // step 1 - collect maps

            foreach (var o in allObjects)
            {
                if (o is Goods g)
                    allGoods.Add(g);
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
                        break;
                    case Entity entity:
                        foreach (var product in entity.loot)
                            itemLoot.Add(product.goods, entity);
                        entity.recipes = new PackedList<Recipe>(recipeCrafters.GetList(entity)
                            .SelectMany(x => recipeCategories.GetList(x).Where(y => y.CanFit(entity.itemInputs, entity.fluidInputs, entity.inputs))));
                        foreach (var recipeId in entity.recipes.raw)
                            actualRecipeCrafters.Add(allObjects[recipeId] as Recipe, entity);
                        break;
                }
            }
            
            // step 2 - fill maps

            foreach (var o in allObjects)
            {
                switch (o)
                {
                    case Recipe recipe:
                        recipe.FallbackLocalization(recipe.mainProduct, "A recipe to create");
                        recipe.technologyUnlock = new PackedList<Technology>(recipeUnlockers.GetList(recipe));
                        recipe.crafters = new PackedList<Entity>(actualRecipeCrafters.GetList(recipe));
                        break;
                    case Goods goods:
                        goods.usages = itemUsages.GetArray(goods);
                        goods.production = itemProduction.GetArray(goods);
                        goods.loot = itemLoot.GetArray(goods);
                        if (o is Item item)
                            item.FallbackLocalization(item.placeResult, "An item to build");
                        break;
                    case Entity entity:
                        entity.itemsToPlace = new PackedList<Item>(entityPlacers.GetList(entity));
                        if (entity.energy != null)
                            entity.energy.fuels = new PackedList<Goods>(fuelUsers.GetList(entity).SelectMany(fuels.GetList));
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
                return recipeRaw as Recipe;
            var recipe = GetObject<Mechanics>(fullName);
            recipe.time = 1f;
            recipe.type = SpecialNames.FakeRecipe;
            recipe.flags = RecipeFlags.ProductivityDisabled;
            recipe.name = fullName;
            recipe.iconSpec = production.iconSpec;
            recipe.locName = production.locName + " " + hint;
            recipe.locDescr = production.locDescr;
            recipe.enabled = true;
            recipe.hidden = true;
            recipe.technologyUnlock = new PackedList<Technology>();
            recipeCategories.Add(category, recipe);
            return recipe;
        }
        
        private class DataBucket<TKey, TValue>
        {
            private readonly Dictionary<TKey, List<TValue>> storage = new Dictionary<TKey, List<TValue>>();

            public void Add(TKey key, TValue value, bool checkUnique = false)
            {
                if (!storage.TryGetValue(key, out var list))
                    storage[key] = new List<TValue> {value};
                else if (!checkUnique || !list.Contains(value))
                    list.Add(value);
            }

            public TValue[] GetArray(TKey key)
            {
                if (!storage.TryGetValue(key, out var list))
                    return Array.Empty<TValue>();
                return list.ToArray();
            }

            public List<TValue> GetList(TKey key)
            {
                if (!storage.TryGetValue(key, out var list))
                    return storage[key] = list = new List<TValue>();
                list.TrimExcess();
                return list;
            }
        }
    }
}