using System;
using System.Linq;
using YAFC.Model;

namespace YAFC.Parser
{
    internal partial class FactorioDataDeserializer
    {
        private T DeserializeWithDifficulty<T>(LuaTable table, string prototypeType, Action<T, LuaTable, bool> loader) where T : FactorioObject, new()
        {
            var obj = DeserializeCommon<T>(table, prototypeType);
            var current = expensiveRecipes ? table["expensive"] : table["normal"];
            var fallback = expensiveRecipes ? table["normal"] : table["expensive"];
            if (current is LuaTable)
                loader(obj, current as LuaTable, false);
            else if (fallback is LuaTable)
                loader(obj, fallback as LuaTable, current is bool b && !b);
            else loader(obj, table, false);
            return obj;
        }

        private void DeserializeRecipe(LuaTable table)
        {
            var recipe = DeserializeWithDifficulty<Recipe>(table, "recipe", LoadRecipeData);
            table.Get("category", out string recipeCategory, "crafting");
            recipeCategories.Add(recipeCategory, recipe);
            recipe.modules = recipeModules.GetArray(recipe);
            if (table.Get("main_product", out string mainProductName))
                recipe.mainProduct = recipe.products.FirstOrDefault(x => x.goods.name == mainProductName)?.goods;
            else if (recipe.products.Length == 1)
                recipe.mainProduct = recipe.products[0]?.goods;
        }

        private void DeserializeFlags(LuaTable table, RecipeOrTechnology recipe, bool forceDisable)
        {
            recipe.hidden = table.Get("hidden", true);
            if (forceDisable)
                recipe.enabled = false;
            else recipe.enabled = table.Get("enabled", true);
        }

        private void DeserializeTechnology(LuaTable table)
        {
            var technology = DeserializeWithDifficulty<Technology>(table, "technology", LoadTechnologyData);
            recipeCategories.Add(SpecialNames.Labs, technology);
            technology.products = Array.Empty<Product>();
        }

        private void UpdateRecipeIngredientFluids()
        {
            foreach (var recipe in allObjects.OfType<Recipe>())
            {
                foreach (var ingredient in recipe.ingredients)
                {
                    if (ingredient.goods is Fluid fluid && fluid.variants != null)
                    {
                        
                    }
                }
            }
        }

        private void LoadTechnologyData(Technology technology, LuaTable table, bool forceDisable)
        {
            table.Get("unit", out LuaTable unit);
            technology.ingredients = LoadIngredientList(unit);
            DeserializeFlags(table, technology, forceDisable);
            technology.time = unit.Get("time", 1f);
            technology.count = table.Get("count", 1000f);
            if (table.Get("prerequisites", out LuaTable preqList))
                technology.prerequisites = preqList.ArrayElements<string>().Select(GetObject<Technology>).ToArray();
            if (table.Get("effects", out LuaTable modifiers))
                technology.unlockRecipes = modifiers.ArrayElements<LuaTable>()
                .Select(x => x.Get("type", out string type) && type == "unlock-recipe" && GetRef<Recipe>(x,"recipe", out var recipe) ? recipe : null).Where(x => x != null)
                .ToArray();
        }

        private Product LoadProduct(LuaTable table)
        {
            var haveExtraData = LoadItemData(out var goods, out var amount, table, true);
            if (haveExtraData && amount == 0)
            {
                table.Get("amount_min", out float min);
                table.Get("amount_max", out float max);
                amount = (min + max) / 2;
            }
            var product = new Product(goods, amount) {probability = table.Get("probability", 1f)};
            return product;
        }

        private Product[] LoadProductList(LuaTable table)
        {
            if (table.Get("results", out LuaTable resultList))
            {
                return resultList.ArrayElements<LuaTable>().Select(LoadProduct).ToArray();
            }

            table.Get("result", out string name);
            if (name == null)
                return Array.Empty<Product>();
            var singleProduct = new Product(GetObject<Item>(name), table.Get("result_count", out float amount) ? amount : table.Get("count", 1));
            return singleProduct.SingleElementArray();
        }

        private Ingredient[] LoadIngredientList(LuaTable table)
        {
            table.Get("ingredients", out LuaTable ingrList);
            return ingrList.ArrayElements<LuaTable>().Select(x =>
            {
                var haveExtraData = LoadItemData(out var goods, out var amount, x, true);
                var ingredient = new Ingredient(goods, amount);
                if (haveExtraData && goods is Fluid f)
                {
                    ingredient.temperature = x.Get("temperature", out int temp)
                        ? new TemperatureRange(temp)
                        : new TemperatureRange(x.Get("minimum_temperature", f.temperatureRange.min), x.Get("maximum_temperature", f.temperatureRange.max));
                }
                return ingredient;
            }).ToArray();
        }

        private void LoadRecipeData(Recipe recipe, LuaTable table, bool forceDisable)
        {
            recipe.ingredients = LoadIngredientList(table);
            recipe.products = LoadProductList(table);

            recipe.time = table.Get("energy_required", 0.5f);
            DeserializeFlags(table, recipe, forceDisable);
        }
    }
}