using System;
using System.Collections.Generic;
using System.Linq;
using YAFC.Model;
using YAFC.UI;

namespace YAFC.Parser
{
    internal partial class FactorioDataDeserializer
    {
        private const float EstimationDistancFromCenter = 3000f;
        private bool GetFluidBoxFilter(LuaTable table, string fluidBoxName, int temperature, out Fluid fluid)
        {
            fluid = null;
            if (!table.Get(fluidBoxName, out LuaTable fluidBoxData))
                return false;
            if (!fluidBoxData.Get("filter", out string fluidName))
                return false;
            fluid = temperature == 0 ? GetObject<Fluid>(fluidName) : GetFluidFixedTemp(fluidName, temperature);
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
            energy.type = burns ? EntityEnergyType.FluidFuel : EntityEnergyType.FluidHeat;

            energy.temperature = TemperatureRange.Any;
            if (energySource.Get("fluid_usage_per_tick", out float fuelLimit))
                energy.fluidLimit = fuelLimit * 60f;

            if (GetFluidBoxFilter(energySource, "fluid_box", 0, out var fluid))
            {
                var fuelCategory = SpecialNames.SpecificFluid + fluid.name;
                fuelUsers.Add(entity, fuelCategory);
                if (!burns)
                {
                    var temperature = fluid.temperatureRange;
                    var maxT = energySource.Get("maximum_temperature", int.MaxValue);
                    temperature.max = Math.Min(temperature.max, maxT);
                    energy.temperature = temperature;
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
            energySource.Get("type", out string type, "burner");
            energy.emissions = energySource.Get("emissions_per_minute", 0f);
            energy.effectivity = energySource.Get("effectivity", 1f);
            switch (type)
            {
                case "electric":
                    fuelUsers.Add(entity, SpecialNames.Electricity);
                    energy.type = EntityEnergyType.Electric;
                    break;
                case "void":
                    energy.effectivity = float.PositiveInfinity;
                    energy.type = EntityEnergyType.Void;
                    fuelUsers.Add(entity, SpecialNames.Void);
                    break;
                case "burner":
                    energy.type = EntityEnergyType.SolidFuel;
                    if (energySource.Get("fuel_categories", out LuaTable categories))
                        foreach (var cat in categories.ArrayElements<string>())
                            fuelUsers.Add(entity, cat);
                    else fuelUsers.Add(entity, energySource.Get("fuel_category", "chemical"));
                    break;
                case "heat":
                    energy.type = EntityEnergyType.Heat;
                    fuelUsers.Add(entity, SpecialNames.Heat);
                    energy.temperature = new TemperatureRange(energySource.Get("min_working_temperature", 15), energySource.Get("max_temperature", 15));
                    break;
                case "fluid":
                    ReadFluidEnergySource(energySource,  entity);
                    break;
            }
        }

        private int GetSize(LuaTable box)
        {
            box.Get(1, out LuaTable topleft);
            box.Get(2, out LuaTable bottomRight);
            topleft.Get(1, out float x0);
            topleft.Get(2, out float y0);
            bottomRight.Get(1, out float x1);
            bottomRight.Get(2, out float y1);
            return Math.Max(MathUtils.Round(x1 - x0), MathUtils.Round(y1 - y0));
        }

        private void ParseModules(LuaTable table, Entity entity)
        {
            if (table.Get("allowed_effects", out object obj))
            {
                if (obj is string s)
                    entity.allowedEffects = (AllowedEffects)Enum.Parse(typeof(AllowedEffects), s, true);
                else if (obj is LuaTable t)
                {
                    entity.allowedEffects = AllowedEffects.None;
                    foreach (var str in t.ArrayElements<string>())
                        entity.allowedEffects |= (AllowedEffects)Enum.Parse(typeof(AllowedEffects), str, true);
                }
            }

            if (table.Get("module_specification", out LuaTable moduleSpec))
                entity.moduleSlots = moduleSpec.Get("module_slots", 0);
        }
        
        private void DeserializeEntity(LuaTable table)
        {
            var factorioType = table.Get("type", "");
            var name = table.Get("name", "");
            switch (factorioType)
            {
                case "transport-belt":
                    GetObject<Entity, EntityBelt>(name).beltItemsPerSecond = table.Get("speed", 0f) * 480f;;
                    break;
                case "inserter":
                    var inserter = GetObject<Entity, EntityInserter>(name);
                    inserter.inserterSwingTime = 1f / (table.Get("rotation_speed", 1f) * 60);
                    inserter.isStackInserter = table.Get("stack", false);
                    break;
                case "accumulator":
                    GetObject<Entity, EntityAccumulator>(name);
                    break;
                case "reactor":
                    GetObject<Entity, EntityReactor>(name).reactorNeighbourBonus = table.Get("neighbour_bonus", 1f);
                    break;
                case "beacon":
                    GetObject<Entity, EntityBeacon>(name).beaconEfficiency = table.Get("distribution_effectivity", 0f);
                    break;
                case "logistic-container": case "container":
                    var container = GetObject<Entity, EntityContainer>(name);
                    container.inventorySize = table.Get("inventory_size", 0);
                    if (factorioType == "logistic-container")
                    {
                        container.logisticMode = table.Get("logistic_mode", "");
                        container.logisticSlotsCount = table.Get("logistic_slots_count", 0);
                        if (container.logisticSlotsCount == 0)
                            container.logisticSlotsCount = table.Get("max_logistic_slots", 1000);
                    }
                    break;
            }
            
            var entity = DeserializeCommon<Entity>(table, "entity");

            if (table.Get("loot", out LuaTable lootList))
            {
                entity.loot = lootList.ArrayElements<LuaTable>().Select(x =>
                {
                    var product = new Product(GetObject<Item>(x.Get("item", "")), x.Get("count_min", 1f), x.Get("count_max", 1f), x.Get("probability", 1f));
                    return product;
                }).ToArray();
            }

            if (table.Get("minable", out LuaTable minable))
            {
                var products = LoadProductList(minable);
                if (factorioType == "resource")
                {
                    // mining resource is processed as a recipe
                    table.Get("category", out var category, "basic-solid");
                    var recipe = CreateSpecialRecipe( entity, SpecialNames.MiningRecipe + category, "mining");
                    recipe.flags = RecipeFlags.UsesMiningProductivity | RecipeFlags.LimitedByTickRate;
                    recipe.time = minable.Get("mining_time", 1f);
                    recipe.products = products;
                    recipe.modules = allModules;
                    recipe.sourceEntity = entity;
                    if (minable.Get("required_fluid", out string requiredFluid))
                    {
                        minable.Get("fluid_amount", out float amount);
                        recipe.ingredients = new Ingredient(GetObject<Fluid>(requiredFluid), amount/10f).SingleElementArray(); // 10x difference is correct but why?
                    }
                    else recipe.ingredients = Array.Empty<Ingredient>();
                }
                else
                {
                    // otherwise it is processed as loot
                    entity.loot = products;
                }
            }

            entity.size = table.Get("selection_box", out LuaTable box) ? GetSize(box) : 3;

            table.Get("energy_source", out LuaTable energySource);
            if (factorioType != "generator" && factorioType != "solar-panel" && factorioType != "accumulator" && factorioType != "burner-generator" && energySource != null)
                ReadEnergySource(energySource, entity);
            entity.productivity = table.Get("base_productivity", 0f);

            if (table.Get("autoplace", out LuaTable generation))
            {
                entity.mapGenerated = true;
                rootAccessible.Add(entity);
                if (generation.Get("probability_expression", out LuaTable prob))
                {
                    var probability = EstimateNoiseExpression(prob);
                    var richness = generation.Get("richness_expression", out LuaTable rich) ? EstimateNoiseExpression(rich) : probability;
                    entity.mapGenDensity = richness * probability;
                }
                else if (generation.Get("coverage", out float coverage))
                {
                    var richBase = generation.Get("richness_base", 0f);
                    var richMult = generation.Get("richness_multiplier", 0f);
                    var richMultDist = generation.Get("richness_multiplier_distance_bonus", 0f);
                    var estimatedAmount = coverage * (richBase + richMult + richMultDist * EstimationDistancFromCenter);
                    entity.mapGenDensity = estimatedAmount;
                }
            }

            switch (factorioType)
            {
                case "character":
                    entity.itemInputs = 255;
                    if (table.Get("mining_categories", out LuaTable resourceCategories)) 
                        foreach (var playerMining in resourceCategories.ArrayElements<string>())
                            recipeCrafters.Add(entity, SpecialNames.MiningRecipe + playerMining);
                    if (table.Get("crafting_categories", out LuaTable craftingCategories))
                        foreach (var playerCrafting in craftingCategories.ArrayElements<string>())
                            recipeCrafters.Add(entity, playerCrafting);
                    entity.energy = laborEntityEnergy;
                    if (entity.name == "character")
                    {
                        character = entity;
                        entity.mapGenerated = true;
                        rootAccessible.Insert(0, entity);
                    }
                    break;
                case "boiler":
                    table.Get("energy_consumption", out string usesPower);
                    entity.power = ParseEnergy(usesPower);
                    entity.fluidInputs = 1;
                    var hasOutput = table.Get("mode", out string mode) && mode == "output-to-separate-pipe";
                    GetFluidBoxFilter(table, "fluid_box", 0, out var input);
                    table.Get("target_temperature", out int targetTemp);
                    var output = hasOutput ? GetFluidBoxFilter(table, "output_fluid_box", targetTemp, out var fluid) ? fluid : null : input;
                    if (input == null || output == null) // TODO - boiler works with any fluid - not supported
                        break;
                    // otherwise convert boiler production to a recipe
                    var category = SpecialNames.BoilerRecipe + entity.name;
                    var recipe = CreateSpecialRecipe(output, category, "boiling to "+targetTemp+"°");
                    recipeCrafters.Add(entity, category);
                    recipe.flags |= RecipeFlags.UsesFluidTemperature;
                    recipe.ingredients = new Ingredient(input, 60).SingleElementArray();
                    recipe.products = new Product(output, 60).SingleElementArray();
                    // This doesn't mean anything as RecipeFlags.UsesFluidTemperature overrides recipe time, but looks nice in the tooltip
                    recipe.time = input.heatCapacity * 60 * (output.temperature - Math.Max(input.temperature, input.temperatureRange.min)) / entity.power; 
                    entity.craftingSpeed = 1f / entity.power;
                    break;
                case "assembling-machine":
                case "rocket-silo":
                case "furnace":
                    table.Get("energy_usage", out usesPower);
                    ParseModules(table, entity);
                    entity.power = ParseEnergy(usesPower);
                    entity.craftingSpeed = table.Get("crafting_speed", 1f);
                    entity.itemInputs = factorioType == "furnace" ? table.Get("source_inventory_size", 1) : table.Get("ingredient_count", 255);
                    if (table.Get("fluid_boxes", out LuaTable fluidBoxes))
                        entity.fluidInputs = CountFluidBoxes(fluidBoxes, true);
                    Recipe fixedRecipe = null;
                    if (table.Get("fixed_recipe", out string fixedRecipeName))
                    {
                        var fixedRecipeCategoryName = SpecialNames.FixedRecipe + fixedRecipeName;
                        fixedRecipe = GetObject<Recipe>(fixedRecipeName);
                        recipeCrafters.Add(entity, fixedRecipeCategoryName);
                        recipeCategories.Add(fixedRecipeCategoryName, fixedRecipe);
                    }
                    else
                    {
                        table.Get("crafting_categories", out craftingCategories);
                        foreach (var categoryName in craftingCategories.ArrayElements<string>())
                            recipeCrafters.Add(entity, categoryName);
                    }

                    if (factorioType == "rocket-silo" && fixedRecipe != null)
                    {
                        var launchCategory = SpecialNames.RocketLaunch + entity.name;
                        var launchRecipe = CreateSpecialRecipe(entity, launchCategory, "launch");
                        recipeCrafters.Add(entity, launchCategory);
                        table.Get("rocket_parts_required", out var partsRequired, 100);
                        launchRecipe.ingredients = fixedRecipe.products.Select(x => new Ingredient(x.goods, x.amount * partsRequired)).ToArray();
                        launchRecipe.products = new Product(rocketLaunch, 1).SingleElementArray();
                        launchRecipe.time = 40.33f; // TODO what to put here?
                        recipeCrafters.Add(entity, SpecialNames.RocketLaunch);
                    }
                    break;
                case "beacon":
                    table.Get("energy_usage", out usesPower);
                    ParseModules(table, entity);
                    entity.power = ParseEnergy(usesPower);
                    break;
                case "generator": case "burner-generator":
                    // generator energy input config is strange
                    if (table.Get("max_power_output", out string maxPowerOutput))
                        entity.power = ParseEnergy(maxPowerOutput);
                    if ((factorioVersion < v0_18 || factorioType == "burner-generator") && table.Get("burner", out LuaTable burnerSource))
                    {
                        ReadEnergySource(burnerSource, entity);
                    }
                    else
                    {
                        entity.energy = new EntityEnergy {effectivity = table.Get("effectivity", 1f)};
                        ReadFluidEnergySource(table, entity);
                    }
                    recipeCrafters.Add(entity, SpecialNames.GeneratorRecipe);
                    break;
                case "mining-drill":
                    table.Get("energy_usage", out usesPower);
                    entity.power = ParseEnergy(usesPower);
                    ParseModules(table, entity);
                    entity.craftingSpeed = table.Get("mining_speed", 1f);
                    table.Get("resource_categories", out resourceCategories);
                    if (table.Get("input_fluid_box", out LuaTable _))
                        entity.fluidInputs = 1;
                    foreach (var resource in resourceCategories.ArrayElements<string>())
                        recipeCrafters.Add(entity, SpecialNames.MiningRecipe + resource);
                    break;
                case "offshore-pump":
                    entity.craftingSpeed = table.Get("pumping_speed", 20) / 20f;
                    table.Get("fluid", out string fluidName);
                    var pumpingFluid = GetFluidFixedTemp(fluidName, 0);
                    var recipeCategory = SpecialNames.PumpingRecipe + pumpingFluid.name;
                    recipe = CreateSpecialRecipe(pumpingFluid, recipeCategory, "pumping");
                    recipeCrafters.Add(entity, recipeCategory);
                    entity.energy = voidEntityEnergy;
                    if (recipe.products == null)
                    {
                        recipe.products = new Product(pumpingFluid, 1200f).SingleElementArray(); // set to Factorio default pump amounts - looks nice in tooltip
                        recipe.ingredients = Array.Empty<Ingredient>();
                        recipe.time = 1f;
                    }
                    break;
                case "lab":
                    table.Get("energy_usage", out usesPower);
                    entity.power = ParseEnergy(usesPower);
                    entity.craftingSpeed = table.Get("researching_speed", 1f);
                    recipeCrafters.Add(entity, SpecialNames.Labs); 
                    table.Get("inputs", out LuaTable inputs);
                    entity.inputs = inputs.ArrayElements<string>().Select(GetObject<Item>).ToArray();
                    sciencePacks.UnionWith(entity.inputs.Select(x => (Item)x));
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
                case "electric-energy-interface":
                    if (entity.energy == null)
                        entity.energy = voidEntityEnergy;
                    if (table.Get("energy_production", out string interfaceProduction))
                    {
                        recipeCrafters.Add(entity, SpecialNames.GeneratorRecipe);
                        entity.craftingSpeed = ParseEnergy(interfaceProduction);
                    }
                    break;
                case "constant-combinator":
                    if (name == "constant-combinator")
                        Database.constantCombinatorCapacity = table.Get("item_slot_count", 18);
                    break;
                case "accumulator":
                    if (energySource != null && energySource.Get("buffer_capacity", out string capacity))
                        ((EntityAccumulator) entity).accumulatorCapacity = ParseEnergy(capacity);
                    break;
            }

            if (entity.loot == null)
                entity.loot = Array.Empty<Product>();

            if (entity.energy == voidEntityEnergy || entity.energy == laborEntityEnergy)
                fuelUsers.Add(entity, SpecialNames.Void);
        }
        
        private float EstimateArgument(LuaTable args, string name, float def = 0) => args.Get(name, out LuaTable res) ? EstimateNoiseExpression(res) : def;
        private float EstimateArgument(LuaTable args, int index, float def = 0) => args.Get(index, out LuaTable res) ? EstimateNoiseExpression(res) : def;

        private float EstimateNoiseExpression(LuaTable expression)
        {
            var type = expression.Get("type", "typed"); 
            switch (type)
            {
                case "variable":
                    var varname = expression.Get("variable_name", "");
                    if (varname == "x" || varname == "y" || varname == "distance")
                        return EstimationDistancFromCenter;
                    if (((LuaTable)raw["noise-expression"]).Get(varname, out LuaTable noiseExpr))
                        return EstimateArgument(noiseExpr, "expression");
                    return 1f;
                case "function-application":
                    var funName = expression.Get("function_name", "");
                    var args = expression.Get<LuaTable>("arguments", null);
                    switch (funName)
                    {
                        case "add":
                            var res = 0f;
                            foreach (var el in args.ArrayElements<LuaTable>())
                                res += EstimateNoiseExpression(el);
                            return res;
                        case "multiply":
                            res = 1f;
                            foreach (var el in args.ArrayElements<LuaTable>())
                                res *= EstimateNoiseExpression(el);
                            return res;
                        case "subtract":
                            return EstimateArgument(args, 1) - EstimateArgument(args, 2);
                        case "divide":
                            return EstimateArgument(args, 1) / EstimateArgument(args, 2);
                        case "exponentiate":
                            return MathF.Pow(EstimateArgument(args, 1), EstimateArgument(args, 2));
                        case "absolute-value":
                            return MathF.Abs(EstimateArgument(args, 1));
                        case "clamp":
                            return MathUtils.Clamp(EstimateArgument(args, 1), EstimateArgument(args, 2), EstimateArgument(args, 3));
                        case "log2":
                            return MathF.Log(EstimateArgument(args, 1), 2f);
                        case "distance-from-nearest-point":
                            return EstimateArgument(args, "maximum_distance");
                        case "ridge":
                            return (EstimateArgument(args, 2) + EstimateArgument(args, 3)) * 0.5f; // TODO
                        case "terrace":
                            return EstimateArgument(args, "value"); // TODO what terrace does
                        case "random-penalty":
                            var source = EstimateArgument(args, "source");
                            var penalty = EstimateArgument(args, "amplitude");
                            if (penalty > source)
                                return source / penalty;
                            return (source + source - penalty) / 2;
                        case "spot-noise":
                            var quantity = EstimateArgument(args, "spot_quantity_expression");
                            float spotCount;
                            if (args.Get("candidate_spot_count", out LuaTable spots))
                                spotCount = EstimateNoiseExpression(spots);
                            else spotCount = EstimateArgument(args, "candidate_point_count", 256) / EstimateArgument(args, "skip_span", 1);
                            var regionSize = EstimateArgument(args, "region_size", 512);
                            regionSize *= regionSize;
                            var count = spotCount * quantity / regionSize;
                            return count;
                        case "factorio-basis-noise":
                        case "factorio-quick-multioctave-noise":
                        case "factorio-multioctave-noise":
                            var outputScale = EstimateArgument(args, "output_scale", 1f);
                            return 0.1f * outputScale;
                        default:
                            return 0f;
                    }
                case "procedure-delimiter":
                    return EstimateArgument(expression, "expression");
                case "literal-number":
                    return expression.Get("literal_value", 0f);
                case "literal-expression":
                    return EstimateArgument(expression, "literal_value");
                default:
                    return 0f;
            }
        }
    }
}