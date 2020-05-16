using System;

namespace YAFC.Model
{
    [Flags]
    public enum WarningFlags
    {
        // Static errors
        EntityNotSpecified = 1 << 0,
        FuelNotSpecified = 1 << 1,
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
        public Item usedModule;

        public float fuelUsagePerSecondPerRecipe => recipeTime * fuelUsagePerSecondPerBuilding;
        
        public void CalculateParameters(Recipe recipe, Entity entity, Goods fuel, IInputSettingsProvider settingsProvider, float modulesPayback = 0f)
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
                
                if ((recipe.flags & RecipeFlags.ScaleProductionWithPower) != 0)
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

                            var heatCap = fluid.heatCapacity;
                            var energyPerUnitOfFluid = (minTemp - energy.minTemperature) * heatCap;
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

                    if ((recipe.flags & RecipeFlags.ScaleProductionWithPower) != 0 && energyUsage > 0f)
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
                if ((recipe.flags & RecipeFlags.UsesFluidTemperature) != 0)
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

                usedModule = null;
                activeEffects = default;
                if (modulesPayback > 0f && recipe.modules.Length > 0 && entity.moduleSlots > 0)
                {
                    var productivityValue = recipe.Cost() / recipeTime;
                    var effValue = fuelUsagePerSecondPerBuilding * fuel?.Cost() ?? 0;
                    var bestModuleValue = 0f;
                    foreach (var module in recipe.modules)
                    {
                        if (module.IsAccessibleWithCurrentMilestones() && entity.CanAcceptModule(module.module))
                        {
                            var bonus = module.module.productivity * productivityValue - module.module.consumption * effValue;
                            if (bonus > bestModuleValue && module.Cost() / bonus <= modulesPayback)
                            {
                                bestModuleValue = bonus;
                                usedModule = module;
                            }
                        }
                    }

                    if (usedModule != null)
                    {
                        activeEffects.AddModules(usedModule.module, entity.moduleSlots);
                        productionMultiplier *= (1f + activeEffects.productivity);
                        recipeTime /= (1f + activeEffects.speed);
                        fuelUsagePerSecondPerBuilding *= activeEffects.energyUsageMod;
                    }
                }
                
                
            }
        }
    }
}