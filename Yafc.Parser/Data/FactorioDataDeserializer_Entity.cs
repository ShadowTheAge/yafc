using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Yafc.Model;
using Yafc.UI;

namespace Yafc.Parser;

internal partial class FactorioDataDeserializer {
    private const float EstimationDistanceFromCenter = 3000f;
    private bool GetFluidBoxFilter(LuaTable table, string fluidBoxName, int temperature, [NotNullWhen(true)] out Fluid? fluid, out TemperatureRange range) {
        fluid = null;
        range = default;

        if (!table.Get(fluidBoxName, out LuaTable? fluidBoxData)) {
            return false;
        }

        if (!fluidBoxData.Get("filter", out string? fluidName)) {
            return false;
        }

        fluid = temperature == 0 ? GetObject<Fluid>(fluidName) : GetFluidFixedTemp(fluidName, temperature);
        _ = fluidBoxData.Get("minimum_temperature", out range.min, fluid.temperatureRange.min);
        _ = fluidBoxData.Get("maximum_temperature", out range.max, fluid.temperatureRange.max);

        return true;
    }

    private static int CountFluidBoxes(LuaTable list, bool input) {
        int count = 0;

        foreach (var fluidBox in list.ArrayElements<LuaTable>()) {
            if (fluidBox.Get("production_type", out string? prodType) && (prodType == "input-output" || (input && prodType == "input") || (!input && prodType == "output"))) {
                ++count;
            }
        }

        return count;
    }

    private void ReadFluidEnergySource(LuaTable energySource, Entity entity) {
        var energy = entity.energy;
        _ = energySource.Get("burns_fluid", out bool burns, false);
        energy.type = burns ? EntityEnergyType.FluidFuel : EntityEnergyType.FluidHeat;
        energy.workingTemperature = TemperatureRange.Any;

        if (energySource.Get("fluid_usage_per_tick", out float fuelLimit)) {
            energy.fuelConsumptionLimit = fuelLimit * 60f;
        }

        if (GetFluidBoxFilter(energySource, "fluid_box", 0, out var fluid, out var filterTemperature)) {
            string fuelCategory = SpecialNames.SpecificFluid + fluid.name;
            fuelUsers.Add(entity, fuelCategory);
            if (!burns) {
                var temperature = fluid.temperatureRange;
                int maxT = energySource.Get("maximum_temperature", int.MaxValue);
                temperature.max = Math.Min(temperature.max, maxT);
                energy.workingTemperature = temperature;
                energy.acceptedTemperature = filterTemperature;
            }
        }
        else if (burns) {
            fuelUsers.Add(entity, SpecialNames.BurnableFluid);
        }
        else {
            fuelUsers.Add(entity, SpecialNames.HotFluid);
        }
    }

    private void ReadEnergySource(LuaTable energySource, Entity entity, float defaultDrain = 0f) {
        _ = energySource.Get("type", out string type, "burner");

        if (type == "void") {
            entity.energy = voidEntityEnergy;
            return;
        }

        EntityEnergy energy = new EntityEnergy();
        entity.energy = energy;
        energy.emissions = energySource.Get("emissions_per_minute", 0f);
        energy.effectivity = energySource.Get("effectivity", 1f);

        switch (type) {
            case "electric":
                fuelUsers.Add(entity, SpecialNames.Electricity);
                energy.type = EntityEnergyType.Electric;
                string? drainS = energySource.Get<string>("drain");
                energy.drain = drainS == null ? defaultDrain : ParseEnergy(drainS);
                break;
            case "burner":
                energy.type = EntityEnergyType.SolidFuel;
                if (energySource.Get("fuel_categories", out LuaTable? categories)) {
                    foreach (string cat in categories.ArrayElements<string>()) {
                        fuelUsers.Add(entity, cat);
                    }
                }

                break;
            case "heat":
                energy.type = EntityEnergyType.Heat;
                fuelUsers.Add(entity, SpecialNames.Heat);
                energy.workingTemperature = new TemperatureRange(energySource.Get("min_working_temperature", 15), energySource.Get("max_temperature", 15));
                break;
            case "fluid":
                ReadFluidEnergySource(energySource, entity);
                break;
        }
    }

    private static int GetSize(LuaTable box) {
        _ = box.Get(1, out LuaTable? topLeft);
        _ = box.Get(2, out LuaTable? bottomRight);
        _ = topLeft.Get(1, out float x0);
        _ = topLeft.Get(2, out float y0);
        _ = bottomRight.Get(1, out float x1);
        _ = bottomRight.Get(2, out float y1);

        return Math.Max(MathUtils.Round(x1 - x0), MathUtils.Round(y1 - y0));
    }

    private static void ParseModules(LuaTable table, EntityWithModules entity, AllowedEffects def) {
        if (table.Get("allowed_effects", out object? obj)) {
            if (obj is string s) {
                entity.allowedEffects = (AllowedEffects)Enum.Parse(typeof(AllowedEffects), s, true);
            }
            else if (obj is LuaTable t) {
                entity.allowedEffects = AllowedEffects.None;
                foreach (string str in t.ArrayElements<string>()) {
                    entity.allowedEffects |= (AllowedEffects)Enum.Parse(typeof(AllowedEffects), str, true);
                }
            }
        }
        else {
            entity.allowedEffects = def;
        }

        if (table.Get("allowed_module_categories", out LuaTable? categories)) {
            entity.allowedModuleCategories = categories.ArrayElements<string>().ToArray();
        }

        entity.moduleSlots = table.Get("module_slots", 0);
    }

    private Recipe CreateLaunchRecipe(EntityCrafter entity, Recipe recipe, int partsRequired, int outputCount) {
        string launchCategory = SpecialNames.RocketCraft + entity.name;
        var launchRecipe = CreateSpecialRecipe(recipe, launchCategory, "launch");
        recipeCrafters.Add(entity, launchCategory);
        launchRecipe.ingredients = recipe.products.Select(x => new Ingredient(x.goods, x.amount * partsRequired)).ToArray();
        launchRecipe.products = [new Product(rocketLaunch, outputCount)];
        launchRecipe.time = 40.33f / outputCount;
        recipeCrafters.Add(entity, SpecialNames.RocketLaunch);

        return launchRecipe;
    }

    private void DeserializeEntity(LuaTable table, ErrorCollector errorCollector) {
        string factorioType = table.Get("type", "");
        string name = table.Get("name", "");
        string? usesPower;
        float defaultDrain = 0f;

        if (table.Get("placeable_by", out LuaTable? placeableBy) && placeableBy.Get("item", out string? itemName)) {
            var item = GetObject<Item>(itemName);
            if (!placeResults.TryGetValue(item, out var resultNames)) {
                resultNames = placeResults[item] = [];
            }
            resultNames.Add(name);
        }

        switch (factorioType) {
            case "transport-belt":
                GetObject<Entity, EntityBelt>(name).beltItemsPerSecond = table.Get("speed", 0f) * 480f;

                break;
            case "inserter":
                var inserter = GetObject<Entity, EntityInserter>(name);
                inserter.inserterSwingTime = 1f / (table.Get("rotation_speed", 1f) * 60);
                inserter.isBulkInserter = table.Get("bulk", false);

                break;
            case "accumulator":
                var accumulator = GetObject<Entity, EntityAccumulator>(name);

                if (table.Get("energy_source", out LuaTable? accumulatorEnergy) && accumulatorEnergy.Get("buffer_capacity", out string? capacity)) {
                    accumulator.accumulatorCapacity = ParseEnergy(capacity);
                }

                break;
            case "reactor":
                var reactor = GetObject<Entity, EntityReactor>(name);
                reactor.reactorNeighborBonus = table.Get("neighbour_bonus", 1f); // Keep UK spelling for Factorio/LUA data objects
                _ = table.Get("consumption", out usesPower);
                reactor.power = ParseEnergy(usesPower);
                reactor.craftingSpeed = reactor.power;
                recipeCrafters.Add(reactor, SpecialNames.ReactorRecipe);

                break;
            case "beacon":
                var beacon = GetObject<Entity, EntityBeacon>(name);
                beacon.beaconEfficiency = table.Get("distribution_effectivity", 0f);
                _ = table.Get("energy_usage", out usesPower);
                ParseModules(table, beacon, AllowedEffects.None);
                beacon.power = ParseEnergy(usesPower);

                break;
            case "logistic-container":
            case "container":
                var container = GetObject<Entity, EntityContainer>(name);
                container.inventorySize = table.Get("inventory_size", 0);

                if (factorioType == "logistic-container") {
                    container.logisticMode = table.Get("logistic_mode", "");
                    container.logisticSlotsCount = table.Get("logistic_slots_count", 0);
                    if (container.logisticSlotsCount == 0) {
                        container.logisticSlotsCount = table.Get("max_logistic_slots", 1000);
                    }
                }

                break;
            case "character":
                var character = GetObject<Entity, EntityCrafter>(name);
                character.itemInputs = 255;

                if (table.Get("mining_categories", out LuaTable? resourceCategories)) {
                    foreach (string playerMining in resourceCategories.ArrayElements<string>()) {
                        recipeCrafters.Add(character, SpecialNames.MiningRecipe + playerMining);
                    }
                }

                if (table.Get("crafting_categories", out LuaTable? craftingCategories)) {
                    foreach (string playerCrafting in craftingCategories.ArrayElements<string>()) {
                        recipeCrafters.Add(character, playerCrafting);
                    }
                }

                recipeCrafters.Add(character, SpecialNames.TechnologyTrigger);

                character.energy = laborEntityEnergy;
                if (character.name == "character") {
                    this.character = character;
                    character.mapGenerated = true;
                    rootAccessible.Insert(0, character);
                }

                break;
            case "boiler":
                var boiler = GetObject<Entity, EntityCrafter>(name);
                _ = table.Get("energy_consumption", out usesPower);
                boiler.power = ParseEnergy(usesPower);
                boiler.fluidInputs = 1;
                bool hasOutput = table.Get("mode", out string? mode) && mode == "output-to-separate-pipe";
                _ = GetFluidBoxFilter(table, "fluid_box", 0, out Fluid? input, out var acceptTemperature);
                _ = table.Get("target_temperature", out int targetTemp);
                Fluid? output = hasOutput ? GetFluidBoxFilter(table, "output_fluid_box", targetTemp, out var fluid, out _) ? fluid : null : input;

                if (input == null || output == null) { // TODO - boiler works with any fluid - not supported
                    break;
                }

                // otherwise convert boiler production to a recipe
                string category = SpecialNames.BoilerRecipe + boiler.name;
                var recipe = CreateSpecialRecipe(output, category, "boiling to " + targetTemp + "°");
                recipeCrafters.Add(boiler, category);
                recipe.flags |= RecipeFlags.UsesFluidTemperature;
                // TODO: input fluid amount now depends on its temperature, using min temperature should be OK for non-modded
                var inputEnergyPerOneFluid = (targetTemp - acceptTemperature.min) * input.heatCapacity;
                recipe.ingredients = [new Ingredient(input, boiler.power / inputEnergyPerOneFluid) { temperature = acceptTemperature }];
                var outputEnergyPerOneFluid = (targetTemp - output.temperatureRange.min) * output.heatCapacity;
                recipe.products = [new Product(output, boiler.power / outputEnergyPerOneFluid)];
                recipe.time = 1f;
                boiler.craftingSpeed = 1f;

                break;
            case "assembling-machine":
            case "rocket-silo":
            case "furnace":
                var crafter = GetObject<Entity, EntityCrafter>(name);
                _ = table.Get("energy_usage", out usesPower);
                ParseModules(table, crafter, AllowedEffects.None);
                crafter.power = ParseEnergy(usesPower);
                defaultDrain = crafter.power / 30f;
                crafter.craftingSpeed = table.Get("crafting_speed", 1f);
                crafter.itemInputs = factorioType == "furnace" ? table.Get("source_inventory_size", 1) : table.Get("ingredient_count", 255);

                if (table.Get("fluid_boxes", out LuaTable? fluidBoxes)) {
                    crafter.fluidInputs = CountFluidBoxes(fluidBoxes, true);
                }

                Recipe? fixedRecipe = null;

                if (table.Get("fixed_recipe", out string? fixedRecipeName)) {
                    string fixedRecipeCategoryName = SpecialNames.FixedRecipe + fixedRecipeName;
                    fixedRecipe = GetObject<Recipe>(fixedRecipeName);
                    recipeCrafters.Add(crafter, fixedRecipeCategoryName);
                    recipeCategories.Add(fixedRecipeCategoryName, fixedRecipe);
                }
                else {
                    _ = table.Get("crafting_categories", out craftingCategories);
                    foreach (string categoryName in craftingCategories.ArrayElements<string>()) {
                        recipeCrafters.Add(crafter, categoryName);
                    }
                }

                if (factorioType == "rocket-silo") {
                    bool launchToSpacePlatforms = table.Get("launch_to_space_platforms", false);
                    int rocketInventorySize = table.Get("to_be_inserted_to_rocket_inventory_size", 0);

                    if (rocketInventorySize > 0 && !launchToSpacePlatforms) {
                        _ = table.Get("rocket_parts_required", out int partsRequired, 100);

                        if (fixedRecipe != null) {
                            var launchRecipe = CreateLaunchRecipe(crafter, fixedRecipe, partsRequired, rocketInventorySize);
                            formerAliases["Mechanics.launch" + crafter.name + "." + crafter.name] = launchRecipe;
                        }
                        else {
                            foreach (string categoryName in recipeCrafters.GetRaw(crafter).ToArray()) {
                                foreach (var possibleRecipe in recipeCategories.GetRaw(categoryName)) {
                                    if (possibleRecipe is Recipe rec) {
                                        _ = CreateLaunchRecipe(crafter, rec, partsRequired, rocketInventorySize);
                                    }
                                }
                            }
                        }
                    }
                }

                break;
            case "generator":
            case "burner-generator":
                var generator = GetObject<Entity, EntityCrafter>(name);

                // generator energy input config is strange
                if (table.Get("max_power_output", out string? maxPowerOutput)) {
                    generator.power = ParseEnergy(maxPowerOutput);
                }

                if ((factorioVersion < v0_18 || factorioType == "burner-generator") && table.Get("burner", out LuaTable? burnerSource)) {
                    ReadEnergySource(burnerSource, generator);
                }
                else {
                    generator.energy = new EntityEnergy { effectivity = table.Get("effectivity", 1f) };
                    ReadFluidEnergySource(table, generator);
                }

                recipeCrafters.Add(generator, SpecialNames.GeneratorRecipe);

                break;
            case "mining-drill":
                var drill = GetObject<Entity, EntityCrafter>(name);
                _ = table.Get("energy_usage", out usesPower);
                drill.power = ParseEnergy(usesPower);
                ParseModules(table, drill, AllowedEffects.All);
                drill.craftingSpeed = table.Get("mining_speed", 1f);
                _ = table.Get("resource_categories", out resourceCategories);

                if (table.Get("input_fluid_box", out LuaTable? _)) {
                    drill.fluidInputs = 1;
                }

                foreach (string resource in resourceCategories.ArrayElements<string>()) {
                    recipeCrafters.Add(drill, SpecialNames.MiningRecipe + resource);
                }

                break;
            case "offshore-pump":
                var pump = GetObject<Entity, EntityCrafter>(name);
                _ = table.Get("energy_usage", out usesPower);
                pump.power = ParseEnergy(usesPower);
                pump.craftingSpeed = table.Get("pumping_speed", 20f) / 20f;

                if (table.Get("fluid_box", out LuaTable? fluidBox) && fluidBox.Get("fluid", out string? fluidName)) {
                    var pumpingFluid = GetFluidFixedTemp(fluidName, 0);
                    string recipeCategory = SpecialNames.PumpingRecipe + pumpingFluid.name;
                    recipe = CreateSpecialRecipe(pumpingFluid, recipeCategory, "pumping");
                    recipeCrafters.Add(pump, recipeCategory);
                    pump.energy = voidEntityEnergy;

                    if (recipe.products == null) {
                        recipe.products = [new Product(pumpingFluid, 1200f)]; // set to Factorio default pump amounts - looks nice in tooltip
                        recipe.ingredients = [];
                        recipe.time = 1f;
                    }
                }
                else {
                    string recipeCategory = SpecialNames.PumpingRecipe + "tile";
                    recipeCrafters.Add(pump, recipeCategory);
                    pump.energy = voidEntityEnergy;
                }

                break;
            case "lab":
                var lab = GetObject<Entity, EntityCrafter>(name);
                _ = table.Get("energy_usage", out usesPower);
                ParseModules(table, lab, AllowedEffects.All ^ AllowedEffects.Quality);
                lab.power = ParseEnergy(usesPower);
                lab.craftingSpeed = table.Get("researching_speed", 1f);
                recipeCrafters.Add(lab, SpecialNames.Labs);
                _ = table.Get("inputs", out LuaTable? inputs);
                lab.inputs = inputs.ArrayElements<string>().Select(GetObject<Item>).ToArray();
                sciencePacks.UnionWith(lab.inputs.Select(x => (Item)x));
                lab.itemInputs = lab.inputs.Length;

                break;
            case "solar-panel":
                var solarPanel = GetObject<Entity, EntityCrafter>(name);
                solarPanel.energy = voidEntityEnergy;
                _ = table.Get("production", out string? powerProduction);
                recipeCrafters.Add(solarPanel, SpecialNames.GeneratorRecipe);
                solarPanel.craftingSpeed = ParseEnergy(powerProduction) * 0.7f; // 0.7f is a solar panel ratio on nauvis

                break;
            case "electric-energy-interface":
                var eei = GetObject<Entity, EntityCrafter>(name);
                eei.energy = voidEntityEnergy;

                if (table.Get("energy_production", out string? interfaceProduction)) {
                    eei.craftingSpeed = ParseEnergy(interfaceProduction);
                    if (eei.craftingSpeed > 0) {
                        recipeCrafters.Add(eei, SpecialNames.GeneratorRecipe);
                    }
                }

                break;
            case "constant-combinator":
                if (name == "constant-combinator") {
                    Database.constantCombinatorCapacity = table.Get("item_slot_count", 18);
                }

                break;
        }

        var entity = DeserializeCommon<Entity>(table, "entity");

        if (table.Get("loot", out LuaTable? lootList)) {
            entity.loot = lootList.ArrayElements<LuaTable>().Select(x => {
                Product product = new Product(GetObject<Item>(x.Get("item", "")), x.Get("count_min", 1f), x.Get("count_max", 1f), x.Get("probability", 1f));
                return product;
            }).ToArray();
        }

        if (table.Get("minable", out LuaTable? minable)) {
            var products = LoadProductList(minable, "minable");

            if (factorioType == "resource") {
                // mining resource is processed as a recipe
                _ = table.Get("category", out string category, "basic-solid");
                var recipe = CreateSpecialRecipe(entity, SpecialNames.MiningRecipe + category, "mining");
                recipe.flags = RecipeFlags.UsesMiningProductivity;
                recipe.time = minable.Get("mining_time", 1f);
                recipe.products = products;
                recipe.allowedEffects = AllowedEffects.All;
                recipe.sourceEntity = entity;

                if (minable.Get("required_fluid", out string? requiredFluid)) {
                    _ = minable.Get("fluid_amount", out float amount);
                    recipe.ingredients = [new Ingredient(GetObject<Fluid>(requiredFluid), amount / 10f)]; // 10x difference is correct but why?
                }
                else {
                    recipe.ingredients = [];
                }
            }
            else {
                // otherwise it is processed as loot
                entity.loot = products;
            }
        }

        entity.size = table.Get("selection_box", out LuaTable? box) ? GetSize(box) : 3;

        _ = table.Get("energy_source", out LuaTable? energySource);

        // These types have already called ReadEnergySource/ReadFluidEnergySource (generator, burner generator) or don't consume energy from YAFC's point of view (pump to EII).
        // TODO: Work with AAI-I to support offshore pumps that consume energy.
        if (factorioType is not "generator" and not "burner-generator" and not "offshore-pump" and not "solar-panel" and not "accumulator" and not "electric-energy-interface"
            && energySource != null) {

            ReadEnergySource(energySource, entity, defaultDrain);
        }

        if (entity is EntityCrafter entityCrafter) {
            _ = table.Get("effect_receiver", out LuaTable? effectReceiver);
            entityCrafter.effectReceiver = ParseEffectReceiver(effectReceiver);
        }

        if (table.Get("autoplace", out LuaTable? generation)) {
            entity.mapGenerated = true;
            rootAccessible.Add(entity);

            if (generation.Get("probability_expression", out LuaTable? prob)) {
                float probability = EstimateNoiseExpression(prob);
                float richness = generation.Get("richness_expression", out LuaTable? rich) ? EstimateNoiseExpression(rich) : probability;
                entity.mapGenDensity = richness * probability;
            }
            else if (generation.Get("coverage", out float coverage)) {
                float richBase = generation.Get("richness_base", 0f);
                float richMultiplier = generation.Get("richness_multiplier", 0f);
                float richMultiplierDist = generation.Get("richness_multiplier_distance_bonus", 0f);
                float estimatedAmount = coverage * (richBase + richMultiplier + (richMultiplierDist * EstimationDistanceFromCenter));
                entity.mapGenDensity = estimatedAmount;
            }
        }

        entity.loot ??= [];

        if (entity.energy == voidEntityEnergy || entity.energy == laborEntityEnergy) {
            fuelUsers.Add(entity, SpecialNames.Void);
        }
    }

    private float EstimateArgument(LuaTable args, string name, float def = 0) => args.Get(name, out LuaTable? res) ? EstimateNoiseExpression(res) : def;

    private float EstimateArgument(LuaTable args, int index, float def = 0) => args.Get(index, out LuaTable? res) ? EstimateNoiseExpression(res) : def;

    private float EstimateNoiseExpression(LuaTable expression) {
        string type = expression.Get("type", "typed");

        switch (type) {
            case "variable":
                string varname = expression.Get("variable_name", "");

                if (varname is "x" or "y" or "distance") {
                    return EstimationDistanceFromCenter;
                }

                if (((LuaTable?)raw["noise-expression"]).Get(varname, out LuaTable? noiseExpr)) {
                    return EstimateArgument(noiseExpr, "expression");
                }

                return 1f;
            case "function-application":
                string funName = expression.Get("function_name", "");
                var args = expression.Get<LuaTable>("arguments");

                if (args is null) {
                    return 0;
                }

                switch (funName) {
                    case "add":
                        float res = 0f;

                        foreach (var el in args.ArrayElements<LuaTable>()) {
                            res += EstimateNoiseExpression(el);
                        }

                        return res;

                    case "multiply":
                        res = 1f;

                        foreach (var el in args.ArrayElements<LuaTable>()) {
                            res *= EstimateNoiseExpression(el);
                        }

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
                        float source = EstimateArgument(args, "source");
                        float penalty = EstimateArgument(args, "amplitude");

                        if (penalty > source) {
                            return source / penalty;
                        }

                        return (source + source - penalty) / 2;

                    case "spot-noise":
                        float quantity = EstimateArgument(args, "spot_quantity_expression");
                        float spotCount;

                        if (args.Get("candidate_spot_count", out LuaTable? spots)) {
                            spotCount = EstimateNoiseExpression(spots);
                        }
                        else {
                            spotCount = EstimateArgument(args, "candidate_point_count", 256) / EstimateArgument(args, "skip_span", 1);
                        }

                        float regionSize = EstimateArgument(args, "region_size", 512);
                        regionSize *= regionSize;
                        float count = spotCount * quantity / regionSize;

                        return count;

                    case "factorio-basis-noise":
                    case "factorio-quick-multioctave-noise":
                    case "factorio-multioctave-noise":
                        float outputScale = EstimateArgument(args, "output_scale", 1f);
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
