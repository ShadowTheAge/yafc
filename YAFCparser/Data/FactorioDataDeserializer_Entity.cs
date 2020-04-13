using System;
using System.Linq;
using NLua;
using YAFC.Model;

namespace YAFC.Parser
{
    public partial class FactorioDataDeserializer
    {
        private bool GetFluidBoxFilter(LuaTable table, string fluidBoxName, out Fluid fluid)
        {
            fluid = null;
            if (!table.Get(fluidBoxName, out LuaTable fluidBoxData))
                return false;
            if (!fluidBoxData.Get("filter", out string fluidName))
                return false;
            fluid = GetObject<Fluid>(fluidName);
            return true;
        }

        private int CountFluidBoxes(LuaTable list, bool input)
        {
            var count = 0;
            foreach (var fluidBox in list.ArrayElements<LuaTable>())
                if (fluidBox.Get("production_type", out string prodType) && (prodType == "input-output" || input && prodType == "input" || !input && prodType == "output"))
                    ++count;
            return count;
        }

        private void ReadFluidEnergySource(LuaTable energySource, Entity entity)
        {
            var energy = entity.energy;
            energySource.Get("burns_fluid", out var burns, false);
            if (!burns)
                energy.usesHeat = true;

            energySource.Get("maximum_temperature", out energy.maxTemperature, float.PositiveInfinity);
            if (energySource.Get("fluid_usage_per_tick", out float fuelLimit))
                energy.fluidLimit = fuelLimit * 60f;

            if (GetFluidBoxFilter(energySource, "fluid_box", out var fluid))
            {
                var fuelCategory = SpecialNames.SpecificFluid + fluid.name;
                fuelUsers.Add(entity, fuelCategory);
                fuels.Add(fuelCategory, fluid, true);
                if (!burns)
                {
                    if (fluid.maxTemperature < energy.maxTemperature)
                        energy.maxTemperature = fluid.maxTemperature;
                    energy.minTemperature = fluid.minTemperature;
                }
            }
            else if (burns)
                fuelUsers.Add(entity, SpecialNames.BurnableFluid);
            else fuelUsers.Add(entity, SpecialNames.HotFluid);
        }

        private void ReadEnergySource(LuaTable energySource, Entity entity)
        {
            var energy = new EntityEnergy();
            entity.energy = energy;
            energySource.Get("type", out string type);
            energySource.Get("effectivity", out energy.effectivity, 1f);
            switch (type)
            {
                case "electric":
                    fuelUsers.Add(entity, SpecialNames.Electricity);
                    break;
                case "void":
                    energy.effectivity = float.PositiveInfinity;
                    fuelUsers.Add(entity, SpecialNames.Void);
                    break;
                case "burner":
                    if (energySource.Get("fuel_category", out string category))
                        fuelUsers.Add(entity, category);
                    else if (energySource.Get("fuel_categories", out LuaTable categories))
                        foreach (var cat in categories.ArrayElements<string>())
                            fuelUsers.Add(entity, cat);
                    break;
                case "heat":
                    fuelUsers.Add(entity, SpecialNames.Heat);
                    energySource.Get("min_working_temperature", out energy.minTemperature);
                    energySource.Get("max_temperature", out energy.maxTemperature);
                    break;
                case "fluid":
                    ReadFluidEnergySource(energySource,  entity);
                    break;
            }
        }
        
        private void DeserializeEntity(LuaTable table)
        {
            var entity = DeserializeCommon<Entity>(table, "entity");
            if (table.Get("loot", out LuaTable lootList))
            {
                entity.loot = lootList.ArrayElements<LuaTable>().Select(x =>
                {
                    var product = new Product();
                    x.Get("item", out string itemName);
                    product.goods = GetObject<Item>(itemName);
                    x.Get("probability", out product.probability, 1f);
                    x.Get("count_min", out float min);
                    x.Get("count_max", out float max);
                    product.amount = (min + max) / 2;
                    return product;
                }).ToArray();
            }

            if (table.Get("minable", out LuaTable minable))
            {
                var products = LoadProductList(minable);
                if (entity.type == "resource")
                {
                    // mining resource is processed as a recipe
                    table.Get("category", out var category, "basic-solid");
                    var recipe = CreateSpecialRecipe( entity, SpecialNames.MiningRecipe + category, "mining");
                    recipe.flags = RecipeFlags.UsesMiningProductivity;
                    minable.Get("mining_time", out recipe.time);
                    recipe.products = products;
                    recipe.sourceEntity = entity;
                    if (minable.Get("required_fluid", out string name))
                    {
                        minable.Get("fluid_amount", out float amount);
                        recipe.ingredients = new Ingredient(GetObject<Fluid>(name), amount).SingleElementArray();
                    }
                    else recipe.ingredients = Array.Empty<Ingredient>();
                }
                else
                {
                    // otherwise it is processed as loot
                    entity.loot = products;
                }
            }

            if (entity.type != "generator" && entity.type != "solar-panel" && table.Get("energy_source", out LuaTable energySource))
                ReadEnergySource(energySource, entity);
            table.Get("base_productivity", out entity.productivity);

            if (table.Get("autoplace", out LuaTable _))
            {
                entity.mapGenerated = true;
                rootAccessible.Add(entity);
            }

            switch (entity.type)
            {
                case "character":
                    table.Get("mining_categories", out LuaTable resourceCategories);
                    table.Get("crafting_categories", out LuaTable craftingCategories);
                    entity.itemInputs = 255;
                    foreach (var playerMining in resourceCategories.ArrayElements<string>())
                        recipeCrafters.Add(entity, SpecialNames.MiningRecipe + playerMining);
                    foreach (var playerCrafting in craftingCategories.ArrayElements<string>())
                        recipeCrafters.Add(entity, playerCrafting);
                    entity.energy = new EntityEnergy {fuels = new PackedList<Goods>(voidEnergy.SingleElementArray())};
                    if (entity.name == "character")
                    {
                        entity.mapGenerated = true;
                        rootAccessible.Insert(0, entity);
                    }
                    entity.energy = voidEntityEnergy;
                    break;
                case "boiler":
                    table.Get("energy_consumption", out string usesPower);
                    entity.power = ParseEnergy(usesPower);
                    entity.fluidInputs = 1;
                    var hasOutput = table.Get("mode", out string mode) && mode == "output-to-separate-pipe";
                    GetFluidBoxFilter(table, "fluid_box", out var input);
                    var output = hasOutput ? GetFluidBoxFilter(table, "output_fluid_box", out var fluid) ? fluid : null : input;
                    if (input == null || output == null) // TODO - boiler works with any fluid - not supported
                        break;
                    // otherwise convert boiler production to a recipe
                    var category = SpecialNames.BoilerRecipe + entity.name;
                    var recipe = CreateSpecialRecipe(output, category, "boiling");
                    recipeCrafters.Add(entity, category);
                    recipe.flags |= RecipeFlags.UsesFluidTemperature;
                    table.Get("target_temperature", out float targetTemp);
                    recipe.ingredients = new Ingredient(input, 1f).SingleElementArray();
                    recipe.products = new Product{goods = output, amount = 1, temperature = targetTemp}.SingleElementArray();
                    recipe.time = input.heatCapacity;
                    entity.craftingSpeed = 1f / entity.power;
                    break;
                case "assembling-machine":
                case "rocket-silo":
                case "furnace":
                    table.Get("crafting_speed", out entity.craftingSpeed);
                    table.Get("energy_usage", out usesPower);
                    table.Get("ingredient_count", out entity.itemInputs, 255);
                    if (table.Get("fluid_boxes", out LuaTable fluidBoxes))
                        entity.fluidInputs = CountFluidBoxes(fluidBoxes, true);
                    entity.power = ParseEnergy(usesPower);
                    if (table.Get("fixed_recipe", out string fixedRecipeName))
                    {
                        var fixedRecipeCategoryName = SpecialNames.FixedRecipe + fixedRecipeName;
                        var fixedRecipe = GetObject<Recipe>(fixedRecipeName);
                        recipeCrafters.Add(entity, fixedRecipeCategoryName);
                        recipeCategories.Add(fixedRecipeCategoryName, fixedRecipe);
                    }
                    else
                    {
                        table.Get("crafting_categories", out craftingCategories);
                        foreach (var categoryName in craftingCategories.ArrayElements<string>())
                            recipeCrafters.Add(entity, categoryName);
                    }

                    if (entity.type == "rocket-silo")
                    {
                        var launchCategory = SpecialNames.RocketLaunch + entity.name;
                        var launchRecipe = CreateSpecialRecipe(entity, launchCategory, "launch");
                        recipeCrafters.Add(entity, launchCategory);
                        table.Get("rocket_parts_required", out var partsRequired, 100);
                        launchRecipe.ingredients = new Ingredient(GetObject<Item>("rocket-part"),  partsRequired).SingleElementArray(); // TODO is rocket-part really hardcoded?
                        launchRecipe.products = new Product {goods = rocketLaunch, amount = 1}.SingleElementArray();
                        launchRecipe.time = 30f; // TODO what to put here?
                        recipeCrafters.Add(entity, SpecialNames.RocketLaunch);
                    }
                    break;
                case "generator":
                    // generator energy input config is strange
                    if (table.Get("max_power_output", out string maxPowerOutput))
                        entity.power = ParseEnergy(maxPowerOutput);
                    if (factorioVersion < v0_18 && table.Get("burner", out LuaTable burnerSource))
                    {
                        ReadEnergySource(burnerSource, entity);
                        entity.craftingSpeed = 1f / entity.power;
                    }
                    else
                    {
                        entity.energy = new EntityEnergy();
                        ReadFluidEnergySource(table, entity);
                        table.Get("fluid_usage_per_tick", out float fluidUsage);
                        entity.craftingSpeed = fluidUsage * 60; // tick-to-second
                    }
                    recipeCrafters.Add(entity, SpecialNames.GeneratorRecipe);
                    break;
                case "mining-drill":
                    table.Get("energy_usage", out usesPower);
                    entity.power = ParseEnergy(usesPower);
                    table.Get("mining_speed", out entity.craftingSpeed);
                    table.Get("resource_categories", out resourceCategories);
                    if (table.Get("input_fluid_box", out LuaTable _))
                        entity.fluidInputs = 1;
                    foreach (var resource in resourceCategories.ArrayElements<string>())
                        recipeCrafters.Add(entity, SpecialNames.MiningRecipe + resource);
                    break;
                case "offshore-pump":
                    table.Get("pumping_speed", out entity.craftingSpeed);
                    GetRef<Fluid>(table, "fluid", out var pumpingFluid);
                    var recipeCategory = SpecialNames.PumpingRecipe + pumpingFluid.name;
                    recipe = CreateSpecialRecipe(pumpingFluid, recipeCategory, "pumping");
                    recipeCrafters.Add(entity, recipeCategory);
                    entity.energy = voidEntityEnergy;
                    if (recipe.products == null)
                    {
                        recipe.products = new Product{goods = pumpingFluid, amount = 60f}.SingleElementArray(); // 60 because pumping speed is per tick and calculator operates in seconds
                        recipe.ingredients = Array.Empty<Ingredient>();
                        recipe.time = 1f;
                    }
                    break;
                case "lab":
                    table.Get("energy_usage", out usesPower);
                    entity.power = ParseEnergy(usesPower);
                    table.Get("researching_speed", out entity.craftingSpeed);
                    recipeCrafters.Add(entity, SpecialNames.Labs); 
                    table.Get("inputs", out LuaTable inputs);
                    entity.inputs = inputs.ArrayElements<string>().Select(GetObject<Item>).ToArray();
                    milestones.UnionWith(entity.inputs);
                    entity.itemInputs = entity.inputs.Length;
                    break;
                case "reactor":
                    table.Get("consumption", out usesPower);
                    entity.power = ParseEnergy(usesPower);
                    entity.craftingSpeed = 1f / entity.power;
                    recipeCrafters.Add(entity, SpecialNames.ReactorRecipe);
                    break;
                case "solar-panel":
                    entity.energy = voidEntityEnergy;
                    table.Get("production", out string powerProduction);
                    recipeCrafters.Add(entity, SpecialNames.GeneratorRecipe);
                    entity.craftingSpeed = ParseEnergy(powerProduction) * 0.7f; // 0.7f is a solar panel ratio on nauvis
                    break;
            }

            if (entity.loot == null)
                entity.loot = Array.Empty<Product>();

            if (entity.energy == voidEntityEnergy)
                fuelUsers.Add(entity, SpecialNames.Void);
        }
    }
}