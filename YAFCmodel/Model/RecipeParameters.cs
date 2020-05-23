using System;

namespace YAFC.Model
{
    [Flags]
    public enum WarningFlags
    {
        // Static errors
        EntityNotSpecified = 1 << 0,
        FuelNotSpecified = 1 << 1,
        FuelTemperatureExceedsMaximum = 1 << 2,
        FuelTemperatureLessThanMinimum = 1 << 3,
        FuelWithTemperatureNotLinked = 1 << 5,
        
        // Not implemented warnings
        TemperatureForIngredientNotMatch = 1 << 8,
        TemperatureRangeForFuelNotImplemented = 1 << 9,
        TemperatureRangeForBoilerNotImplemented = 1 << 10,
        
        // Solutionerrors
        UnfeasibleCandidate = 1 << 16
    }

    public interface IInputSettingsProvider
    {
        bool GetTemperature(Fluid input, out float min, out float max);
    }

    public class RecipeParameters
    {
        public float recipeTime;
        public float fuelUsagePerSecondPerBuilding;
        public float productionMultiplier;
        public WarningFlags warningFlags;
        public ModuleEffects activeEffects;
        public UsedModule modules;
        
        public struct UsedModule
        {
            public Item module;
            public int count;
            public Entity beacon;
            public int beaconCount;
        }

        public float fuelUsagePerSecondPerRecipe => recipeTime * fuelUsagePerSecondPerBuilding;
        
        public void CalculateParameters(Recipe recipe, Entity entity, Goods fuel, IInputSettingsProvider settingsProvider, ModuleFillerParameters moduleFiller)
        {
            warningFlags = 0;
            if (entity == null)
            {
                warningFlags |= WarningFlags.EntityNotSpecified;
                recipeTime = recipe.time;
                productionMultiplier = 1f;
            }
            else
            {
                recipeTime = recipe.time / entity.craftingSpeed;
                productionMultiplier = 1f * (1f + entity.productivity);
                var energyUsage = entity.power * entity.energy.effectivity;
                
                if (recipe.flags.HasFlags(RecipeFlags.ScaleProductionWithPower) && fuel != Database.voidEnergy)
                    warningFlags |= WarningFlags.FuelWithTemperatureNotLinked;

                // Special case for fuel
                if (fuel != null)
                {
                    var fluid = fuel.fluid;
                    var energy = entity.energy;
                    var usesHeat = fluid != null && energy.usesHeat;
                    if (usesHeat)
                    {
                        if (!settingsProvider.GetTemperature(fluid, out var minTemp, out var maxTemp))
                        {
                            fuelUsagePerSecondPerBuilding = float.NaN;
                        }
                        else
                        {
                            // TODO research this case;
                            if (minTemp != maxTemp)
                                warningFlags |= WarningFlags.TemperatureRangeForFuelNotImplemented;
                            if (minTemp > energy.maxTemperature)
                            {
                                minTemp = energy.maxTemperature;
                                warningFlags |= WarningFlags.FuelTemperatureExceedsMaximum;
                            }

                            var heatCap = fluid.heatCapacity;
                            var energyPerUnitOfFluid = (minTemp - energy.minTemperature) * heatCap;
                            if (energyPerUnitOfFluid <= 0f)
                            {
                                fuelUsagePerSecondPerBuilding = float.NaN;
                                warningFlags |= WarningFlags.FuelTemperatureLessThanMinimum;
                            }
                            var maxEnergyProduction = energy.fluidLimit * energyPerUnitOfFluid;
                            if (maxEnergyProduction < energyUsage || energyUsage <= 0) // limited by fluid limit
                            {
                                if (energyUsage <= 0)
                                    recipeTime *= energyUsage / maxEnergyProduction;
                                energyUsage = maxEnergyProduction * entity.energy.effectivity;
                                fuelUsagePerSecondPerBuilding = energy.fluidLimit;
                            }
                            else // limited by energy usage
                                fuelUsagePerSecondPerBuilding = energyUsage / energyPerUnitOfFluid;
                        }
                    }
                    else
                        fuelUsagePerSecondPerBuilding = energyUsage / fuel.fuelValue;

                    if (recipe.flags.HasFlags(RecipeFlags.ScaleProductionWithPower) && energyUsage > 0f)
                    {
                        recipeTime = 1f / energyUsage;
                        warningFlags &= ~WarningFlags.FuelWithTemperatureNotLinked;
                    }
                }
                else
                {
                    fuelUsagePerSecondPerBuilding = energyUsage;
                    warningFlags |= WarningFlags.FuelNotSpecified;
                }

                // Special case for boilers
                if (recipe.flags.HasFlags(RecipeFlags.UsesFluidTemperature))
                {
                    var fluid = recipe.ingredients[0].goods.fluid;
                    if (fluid != null)
                    {
                        float inputTemperature;
                        if (settingsProvider.GetTemperature(fluid, out var minTemp, out var maxTemp))
                        {
                            if (minTemp != maxTemp)
                                warningFlags |= WarningFlags.TemperatureRangeForBoilerNotImplemented;
                            inputTemperature = minTemp;
                        }
                        else inputTemperature = fluid.minTemperature;
                        
                        var outputTemp = recipe.products[0].temperature;
                        var deltaTemp = (outputTemp - inputTemperature);
                        var energyPerUnitOfFluid = deltaTemp * fluid.heatCapacity;
                        if (deltaTemp > 0 && fuel != null)
                            recipeTime = energyPerUnitOfFluid / (fuelUsagePerSecondPerBuilding * fuel.fuelValue);
                    }
                }

                modules = default;
                activeEffects = default;
                if (moduleFiller != null && recipe.modules.Length > 0 && entity.moduleSlots > 0 && recipe.IsAutomatable())
                {
                    if (moduleFiller.FillModules(this, recipe, entity, fuel, out activeEffects, out modules))
                    {
                        productionMultiplier *= (1f + activeEffects.productivity);
                        recipeTime /= (1f + activeEffects.speed);
                        fuelUsagePerSecondPerBuilding *= activeEffects.energyUsageMod;
                    }
                }
            }
        }
    }
}