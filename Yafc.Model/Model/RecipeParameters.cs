using System;

namespace Yafc.Model {
    [Flags]
    public enum WarningFlags {
        // Non-errors
        AssumesNauvisSolarRatio = 1 << 0,
        ReactorsNeighborsFromPrefs = 1 << 1,
        RecipeTickLimit = 1 << 2,
        FuelUsageInputLimited = 1 << 3,

        // Static errors
        EntityNotSpecified = 1 << 8,
        FuelNotSpecified = 1 << 9,
        FuelTemperatureExceedsMaximum = 1 << 10,
        FuelDoesNotProvideEnergy = 1 << 11,
        FuelWithTemperatureNotLinked = 1 << 12,

        // Solution errors
        DeadlockCandidate = 1 << 16,
        OverproductionRequired = 1 << 17,
        ExceedsBuiltCount = 1 << 18,

        // Not implemented warnings
        TemperatureForIngredientNotMatch = 1 << 24,
    }

    public struct UsedModule {
        public (Module module, int count, bool beacon)[]? modules;
        public Entity? beacon;
        public int beaconCount;
    }

    internal class RecipeParameters(float recipeTime, float fuelUsagePerSecondPerBuilding, float productivity, WarningFlags warningFlags, ModuleEffects activeEffects, UsedModule modules) {
        public const float MIN_RECIPE_TIME = 1f / 60;
        public float recipeTime { get; } = recipeTime;
        public float fuelUsagePerSecondPerBuilding { get; } = fuelUsagePerSecondPerBuilding;
        public float productivity { get; } = productivity;
        public WarningFlags warningFlags { get; internal set; } = warningFlags;
        public ModuleEffects activeEffects { get; } = activeEffects;
        public UsedModule modules { get; } = modules;

        public static RecipeParameters Empty = new(0, 0, 0, 0, default, default);

        public float fuelUsagePerSecondPerRecipe => recipeTime * fuelUsagePerSecondPerBuilding;

        public static RecipeParameters CalculateParameters(RecipeRow row) {
            WarningFlags warningFlags = 0;
            EntityCrafter? entity = row.entity;
            RecipeOrTechnology recipe = row.recipe;
            Goods? fuel = row.fuel;
            float recipeTime, fuelUsagePerSecondPerBuilding = 0, productivity;
            ModuleEffects activeEffects = default;
            UsedModule modules = default;

            if (entity == null) {
                warningFlags |= WarningFlags.EntityNotSpecified;
                recipeTime = recipe.time;
                productivity = 0f;
            }
            else {
                recipeTime = recipe.time / entity.craftingSpeed;
                productivity = entity.productivity;
                var energy = entity.energy;
                float energyUsage = entity.power;
                float energyPerUnitOfFuel = 0f;

                // Special case for fuel
                if (fuel != null) {
                    var fluid = fuel.fluid;
                    energyPerUnitOfFuel = fuel.fuelValue;

                    if (energy.type == EntityEnergyType.FluidHeat) {
                        if (fluid == null) {
                            warningFlags |= WarningFlags.FuelWithTemperatureNotLinked;
                        }
                        else {
                            int temperature = fluid.temperature;
                            if (temperature > energy.workingTemperature.max) {
                                temperature = energy.workingTemperature.max;
                                warningFlags |= WarningFlags.FuelTemperatureExceedsMaximum;
                            }

                            float heatCap = fluid.heatCapacity;
                            energyPerUnitOfFuel = (temperature - energy.workingTemperature.min) * heatCap;
                        }
                    }

                    if (fluid != null && !energy.acceptedTemperature.Contains(fluid.temperature)) {
                        warningFlags |= WarningFlags.FuelDoesNotProvideEnergy;
                    }

                    if (energyPerUnitOfFuel > 0f) {
                        fuelUsagePerSecondPerBuilding = energyUsage <= 0f ? 0f : energyUsage / (energyPerUnitOfFuel * energy.effectivity);
                    }
                    else {
                        fuelUsagePerSecondPerBuilding = 0;
                        warningFlags |= WarningFlags.FuelDoesNotProvideEnergy;
                    }
                }
                else {
                    fuelUsagePerSecondPerBuilding = energyUsage;
                    warningFlags |= WarningFlags.FuelNotSpecified;
                }

                // Special case for generators
                if (recipe.flags.HasFlags(RecipeFlags.ScaleProductionWithPower) && energyPerUnitOfFuel > 0 && entity.energy.type != EntityEnergyType.Void) {
                    if (energyUsage == 0) {
                        fuelUsagePerSecondPerBuilding = energy.fuelConsumptionLimit;
                        recipeTime = 1f / (energy.fuelConsumptionLimit * energyPerUnitOfFuel * energy.effectivity);
                    }
                    else {
                        recipeTime = 1f / energyUsage;
                    }
                }

                // Special case for boilers
                if (recipe.flags.HasFlags(RecipeFlags.UsesFluidTemperature)) {
                    var fluid = recipe.ingredients[0].goods.fluid;
                    if (fluid != null) {
                        float inputTemperature = fluid.temperature;
                        foreach (Fluid variant in fluid.variants ?? []) {
                            if (variant.originalName == fluid.originalName) {
                                inputTemperature = variant.temperature;
                            }
                        }

                        int outputTemp = recipe.products[0].goods.fluid!.temperature; // null-forgiving: UsesFluidTemperature tells us this is a special "Fluid boiling to ??°" recipe, with one output fluid.
                        float deltaTemp = outputTemp - inputTemperature;
                        float energyPerUnitOfFluid = deltaTemp * fluid.heatCapacity;
                        if (deltaTemp > 0 && fuel != null) {
                            recipeTime = 60 * energyPerUnitOfFluid / (fuelUsagePerSecondPerBuilding * fuel.fuelValue * energy.effectivity);
                        }
                    }
                }

                bool isMining = recipe.flags.HasFlags(RecipeFlags.UsesMiningProductivity);
                activeEffects = new ModuleEffects();
                if (isMining) {
                    productivity += Project.current.settings.miningProductivity;
                }
                else if (recipe is Technology) {
                    productivity += Project.current.settings.researchProductivity;
                }

                if (entity is EntityReactor reactor && reactor.reactorNeighborBonus > 0f) {
                    productivity += reactor.reactorNeighborBonus * Project.current.settings.GetReactorBonusMultiplier();
                    warningFlags |= WarningFlags.ReactorsNeighborsFromPrefs;
                }

                if (entity.factorioType == "solar-panel") {
                    warningFlags |= WarningFlags.AssumesNauvisSolarRatio;
                }

                modules = default;
                if (recipe.modules.Length > 0 && entity.allowedEffects != AllowedEffects.None) {
                    row.GetModulesInfo((recipeTime, fuelUsagePerSecondPerBuilding), entity, ref activeEffects, ref modules);
                    productivity += activeEffects.productivity;
                    recipeTime /= activeEffects.speedMod;
                    fuelUsagePerSecondPerBuilding *= activeEffects.energyUsageMod;
                }

                if (energy.drain > 0f) {
                    fuelUsagePerSecondPerBuilding += energy.drain / energyPerUnitOfFuel;
                }

                if (fuelUsagePerSecondPerBuilding > energy.fuelConsumptionLimit) {
                    recipeTime *= fuelUsagePerSecondPerBuilding / energy.fuelConsumptionLimit;
                    fuelUsagePerSecondPerBuilding = energy.fuelConsumptionLimit;
                    warningFlags |= WarningFlags.FuelUsageInputLimited;
                }
            }

            if (recipeTime < MIN_RECIPE_TIME && recipe.flags.HasFlags(RecipeFlags.LimitedByTickRate)) {
                if (productivity > 0f) {
                    productivity *= (MIN_RECIPE_TIME / recipeTime); // Recipe time is affected by the minimum time while productivity bonus aren't
                }

                recipeTime = MIN_RECIPE_TIME;
                warningFlags |= WarningFlags.RecipeTickLimit;
            }

            return new RecipeParameters(recipeTime, fuelUsagePerSecondPerBuilding, productivity, warningFlags, activeEffects, modules);
        }
    }
}
