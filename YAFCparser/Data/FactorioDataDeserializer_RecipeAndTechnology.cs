using System;
using System.Linq;
using FactorioData;
using NLua;

namespace FactorioParser
{
    public partial class FactorioDataDeserializer
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
            if (table.Get("main_product", out string mainProductName))
                recipe.mainProduct = recipe.products.FirstOrDefault(x => x.goods.name == mainProductName)?.goods;
            else if (recipe.products.Length == 1)
                recipe.mainProduct = recipe.products[0]?.goods;
        }

        private void DeserializeFlags(LuaTable table, Recipe recipe, bool forceDisable)
        {
            table.Get("hidden", out recipe.hidden, true);
            if (forceDisable)
                recipe.enabled = false;
            else table.Get("enabled", out recipe.enabled, true);
        }

        private void DeserializeTechnology(LuaTable table)
        {
            var technology = DeserializeWithDifficulty<Technology>(table, "technology", LoadTechnologyData);
            recipeCategories.Add(SpecialNames.Labs, technology);
            technology.products = new Product[0];
        }

        private void LoadTechnologyData(Technology technology, LuaTable table, bool forceDisable)
        {
            table.Get("unit", out LuaTable unit);
            technology.ingredients = LoadIngredientList(unit);
            DeserializeFlags(table, technology, forceDisable);
            unit.Get("time", out technology.time);
            table.Get("count", out technology.count, 1000f);
            table.Get("prerequisites", out LuaTable preqList, emptyTable);
            technology.prerequisites = preqList.ArrayElements<string>().Select(GetObject<Technology>).ToArray();
            table.Get("effects", out LuaTable modifiers, emptyTable);
            technology.unlockRecipes = modifiers.ArrayElements<LuaTable>()
                .Select(x => x.Get("type", out string type) && type == "unlock-recipe" && GetRef<Recipe>(x,"recipe", out var recipe) ? recipe : null).Where(x => x != null)
                .ToArray();
        }

        private Product LoadProduct(LuaTable table)
        {
            var product = new Product();
            if (LoadItemData(out product.goods, out product.amount, table))
            {
                table.Get("probability", out product.probability, 1f);
                table.Get("temperature", out product.temperature);
                if (product.amount == 0)
                {
                    table.Get("amount_min", out float min);
                    table.Get("amount_max", out float max);
                    product.amount = (min + max) / 2;
                }
            }

            return product;
        }

        private Product[] LoadProductList(LuaTable table)
        {
            if (table.Get("results", out LuaTable resultList))
            {
                return resultList.ArrayElements<LuaTable>().Select(LoadProduct).ToArray();
            }
            else
            {
                table.Get("result", out string name);
                if (name == null)
                    return Array.Empty<Product>();
                var singleProduct = new Product();
                if (!table.Get("result_count", out singleProduct.amount))
                    table.Get("count", out singleProduct.amount, 1);
                singleProduct.goods = GetObject<Item>(name);
                return singleProduct.SingleElementArray();
            }
        }

        private Ingredient[] LoadIngredientList(LuaTable table)
        {
            table.Get("ingredients", out LuaTable ingrList);
            return ingrList.ArrayElements<LuaTable>().Select(x =>
            {
                var ingredient = new Ingredient();
                if (LoadItemData(out ingredient.goods, out ingredient.amount, x))
                {
                    if (x.Get("temperature", out float temp))
                    {
                        ingredient.minTemperature = temp;
                        ingredient.maxTemperature = temp;
                    }
                    else
                    {
                        x.Get("minimum_temperature", out ingredient.minTemperature);
                        x.Get("maximum_temperature", out ingredient.maxTemperature);
                    }
                }
                return ingredient;
            }).ToArray();
        }

        private void LoadRecipeData(Recipe recipe, LuaTable table, bool forceDisable)
        {
            recipe.ingredients = LoadIngredientList(table);
            recipe.products = LoadProductList(table);

            table.Get("energy_required", out recipe.time, 0.5f);
            DeserializeFlags(table, recipe, forceDisable);
        }
    }
}