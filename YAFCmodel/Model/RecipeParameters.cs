using System;
using System.Collections.Generic;

namespace YAFC.Model
{
    [Flags]
    public enum WarningFlags
    {
        // Non-errors
        AssumesNauvisSolarRatio = 1 << 0,
        ReactorsNeighboursFromPrefs = 1 << 1,
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
        
        // Not implemented warnings
        TemperatureForIngredientNotMatch = 1 << 24,
    }

    public class RecipeParameters
    {
        public const float MIN_RECIPE_TIME = 1f / 60;
        
        public float recipeTime;
        public float fuelUsagePerSecondPerBuilding;
        public float productivity;
        public WarningFlags warningFlags;
        public ModuleEffects activeEffects;
        public UsedModule modules;
        
        public struct UsedModule
        {
            public (Item module, int count, bool beacon)[] modules;
            public Entity beacon;
            public int beaconCount;
        }

        public float fuelUsagePerSecondPerRecipe => recipeTime * fuelUsagePerSecondPerBuilding;
        
        public void Clear()
        {
            recipeTime = 1;
            fuelUsagePerSecondPerBuilding = 0;
            productivity = 0;
            warningFlags = 0;
            activeEffects = default;
            modules = default;
        }
        
        public void CalculateParameters(Recipe recipe, EntityCrafter entity, Goods fuel, HashSet<FactorioObject> variants, IModuleFiller moduleFiller)
        {
            warningFlags = 0;
            if (entity == null)
            {
                warningFlags |= WarningFlags.EntityNotSpecified;
                recipeTime = recipe.time;
                productivity = 0f;
            }
            else
            {
                recipeTime = recipe.time / entity.craftingSpeed;
                productivity = entity.productivity;
                var energy = entity.energy;
                var energyUsage = entity.power;
                var energyPerUnitOfFuel = 0f;

                // Special case for fuel
                if (fuel != null)
                {
                    var fluid = fuel.fluid;
                    energyPerUnitOfFuel = fuel.fuelValue;

                    if (energy.type == EntityEnergyType.FluidHeat)
                    {
                        if (fluid == null)
                            warningFlags |= WarningFlags.FuelWithTemperatureNotLinked;
                        else
                        {
                            var temperature = fluid.temperature;
                            if (temperature > energy.workingTemperature.max)
                            {
                                temperature = energy.workingTemperature.max;
                                warningFlags |= WarningFlags.FuelTemperatureExceedsMaximum;
                            }

                            var heatCap = fluid.heatCapacity;
                            energyPerUnitOfFuel = (temperature - energy.workingTemperature.min) * heatCap;
                        }
                    }

                    if (fluid != null && !energy.acceptedTemperature.Contains(fluid.temperature))
                        warningFlags |= WarningFlags.FuelDoesNotProvideEnergy;

                    if (energyPerUnitOfFuel > 0f)
                        fuelUsagePerSecondPerBuilding = energyUsage <= 0f ? 0f : energyUsage / (energyPerUnitOfFuel * energy.effectivity);
                    else
                    {
                        fuelUsagePerSecondPerBuilding = 0;
                        warningFlags |= WarningFlags.FuelDoesNotProvideEnergy;
                    }
                }
                else
                {
                    fuelUsagePerSecondPerBuilding = energyUsage;
                    warningFlags |= WarningFlags.FuelNotSpecified;
                }
                
                // Special case for generators
                if (recipe.flags.HasFlags(RecipeFlags.ScaleProductionWithPower) && energyPerUnitOfFuel > 0 && entity.energy.type != EntityEnergyType.Void)
                {
                    if (energyUsage == 0)
                    {
                        fuelUsagePerSecondPerBuilding = energy.fuelConsumptionLimit;
                        recipeTime = 1f / (energy.fuelConsumptionLimit * energyPerUnitOfFuel * energy.effectivity);
                    }
                    else
                    {
                        recipeTime = 1f / energyUsage;
                    }
                }

                // Special case for boilers
                if (recipe.flags.HasFlags(RecipeFlags.UsesFluidTemperature))
                {
                    var fluid = recipe.ingredients[0].goods.fluid;
                    if (fluid != null)
                    {
                        float inputTemperature = fluid.temperature;
                        foreach (var variant in variants)
                        {
                            if (variant is Fluid fluidVariant && fluidVariant.originalName == fluid.originalName)
                            {
                                inputTemperature = fluidVariant.temperature;
                            }
                        }

                        var outputTemp = recipe.products[0].goods.fluid.temperature;
                        var deltaTemp = (outputTemp - inputTemperature);
                        var energyPerUnitOfFluid = deltaTemp * fluid.heatCapacity;
                        if (deltaTemp > 0 && fuel != null)
                            recipeTime = 60 * energyPerUnitOfFluid / (fuelUsagePerSecondPerBuilding * fuel.fuelValue * energy.effectivity);
                    }
                }
                
                var isMining = recipe.flags.HasFlags(RecipeFlags.UsesMiningProductivity);
                activeEffects = new ModuleEffects();
                if (isMining)
                    productivity += Project.current.settings.miningProductivity;

                if (entity is EntityReactor reactor && reactor.reactorNeighbourBonus > 0f)
                {
                    productivity += reactor.reactorNeighbourBonus * Project.current.settings.GetReactorBonusMultiplier();
                    warningFlags |= WarningFlags.ReactorsNeighboursFromPrefs;
                }

                if (entity.factorioType == "solar-panel")
                    warningFlags |= WarningFlags.AssumesNauvisSolarRatio;

                modules = default;
                if (moduleFiller != null && recipe.modules.Length > 0 && entity.allowedEffects != AllowedEffects.None)
                {
                    moduleFiller.GetModulesInfo(this, recipe, entity, fuel, ref activeEffects, ref modules);
                    productivity += activeEffects.productivity;
                    recipeTime /= activeEffects.speedMod;
                    fuelUsagePerSecondPerBuilding *= activeEffects.energyUsageMod;
                }

                if (energy.drain > 0f)
                    fuelUsagePerSecondPerBuilding += energy.drain / energyPerUnitOfFuel;
                
                if (fuelUsagePerSecondPerBuilding > energy.fuelConsumptionLimit)
                {
                    recipeTime *= fuelUsagePerSecondPerBuilding / energy.fuelConsumptionLimit;
                    fuelUsagePerSecondPerBuilding = energy.fuelConsumptionLimit;
                    warningFlags |= WarningFlags.FuelUsageInputLimited;
                }
            }

            if (recipeTime < MIN_RECIPE_TIME && recipe.flags.HasFlags(RecipeFlags.LimitedByTickRate))
            {
                if (productivity > 0f)
                    productivity *= (MIN_RECIPE_TIME / recipeTime); // Recipe time is affected by the minimum time while productivity bonus aren't
                recipeTime = MIN_RECIPE_TIME;
                warningFlags |= WarningFlags.RecipeTickLimit;
            }
        }
    }
}