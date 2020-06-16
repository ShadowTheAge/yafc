using System;

namespace YAFC.Model
{
    [Flags]
    public enum WarningFlags
    {
        // Non-errors
        AssumesNauvisSolarRation = 1 << 0,
        AssumesThreeReactors = 1 << 1,
        
        // Static errors
        EntityNotSpecified = 1 << 8,
        FuelNotSpecified = 1 << 9,
        FuelTemperatureExceedsMaximum = 1 << 10,
        FuelTemperatureLessThanMinimum = 1 << 11,
        FuelWithTemperatureNotLinked = 1 << 12,
        
        // Solution errors
        DeadlockCandidate = 1 << 16,
        OverproductionRequired = 1 << 17,
        
        // Not implemented warnings
        TemperatureForIngredientNotMatch = 1 << 24,
        TemperatureRangeForFuelNotImplemented = 1 << 25,
        TemperatureRangeForBoilerNotImplemented = 1 << 26,
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
            public (Item module, int count)[] modules;
            public Entity beacon;
            public int beaconCount;
        }

        public float fuelUsagePerSecondPerRecipe => recipeTime * fuelUsagePerSecondPerBuilding;
        
        public void CalculateParameters(Recipe recipe, Entity entity, Goods fuel, IInputSettingsProvider settingsProvider, IModuleFiller moduleFiller)
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
                            fuelUsagePerSecondPerBuilding = 0;
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
                
                var isMining = recipe.flags.HasFlags(RecipeFlags.UsesMiningProductivity);
                activeEffects = new ModuleEffects();
                if (isMining)
                    activeEffects.productivity += Project.current.settings.miningProductivity;

                if (entity.reactorNeighbourBonus > 0f)
                {
                    productionMultiplier *= (1f + entity.reactorNeighbourBonus * 2f);
                    warningFlags |= WarningFlags.AssumesThreeReactors;
                }

                if (entity.factorioType == "solar-panel")
                    warningFlags |= WarningFlags.AssumesNauvisSolarRation;

                modules = default;
                if (moduleFiller != null && recipe.modules.Length > 0 && entity.moduleSlots > 0 && recipe.IsAutomatable())
                {
                    moduleFiller.GetModulesInfo(this, recipe, entity, fuel, ref activeEffects, ref modules);
                    productionMultiplier *= (1f + activeEffects.productivity);
                    recipeTime /= (1f + activeEffects.speed);
                    fuelUsagePerSecondPerBuilding *= activeEffects.energyUsageMod;
                }
            }
        }
    }
}