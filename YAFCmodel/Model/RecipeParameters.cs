using System;

namespace YAFC.Model
{
    [Flags]
    public enum WarningFlags
    {
        // Non-errors
        AssumesNauvisSolarRatio = 1 << 0,
        AssumesThreeReactors = 1 << 1,
        RecipeTickLimit = 1 << 2,
        
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
            public (Item module, int count)[] modules;
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
        
        public void CalculateParameters(Recipe recipe, Entity entity, Goods fuel, IModuleFiller moduleFiller)
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
                var energyUsage = entity.power / entity.energy.effectivity;
                
                if (recipe.flags.HasFlags(RecipeFlags.ScaleProductionWithPower) && fuel != Database.voidEnergy)
                    warningFlags |= WarningFlags.FuelWithTemperatureNotLinked;

                // Special case for fuel
                if (fuel != null)
                {
                    var fluid = fuel.fluid;
                    var energy = entity.energy;
                    if (fluid != null && energy.type == EntityEnergyType.FluidHeat)
                    {
                        var temperature = fluid.temperature;
                        // TODO research this case;
                        if (temperature > energy.temperature.max)
                        {
                            temperature = energy.temperature.max;
                            warningFlags |= WarningFlags.FuelTemperatureExceedsMaximum;
                        }

                        var heatCap = fluid.heatCapacity;
                        var energyPerUnitOfFluid = (temperature - energy.temperature.min) * heatCap;
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
                    else
                        fuelUsagePerSecondPerBuilding = energyUsage / fuel.fuelValue;

                    if (recipe.flags.HasFlags(RecipeFlags.ScaleProductionWithPower) && energyUsage > 0f)
                    {
                        recipeTime = 1f / (energyUsage * entity.energy.effectivity);
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
                        float inputTemperature = fluid.temperature;
                        var outputTemp = recipe.products[0].goods.fluid.temperature;
                        var deltaTemp = (outputTemp - inputTemperature);
                        var energyPerUnitOfFluid = deltaTemp * fluid.heatCapacity;
                        if (deltaTemp > 0 && fuel != null)
                            recipeTime = 60 * energyPerUnitOfFluid / (fuelUsagePerSecondPerBuilding * fuel.fuelValue);
                    }
                }
                
                var isMining = recipe.flags.HasFlags(RecipeFlags.UsesMiningProductivity);
                activeEffects = new ModuleEffects();
                if (isMining)
                    productivity += Project.current.settings.miningProductivity;

                if (entity is EntityReactor reactor && reactor.reactorNeighbourBonus > 0f)
                {
                    productivity += reactor.reactorNeighbourBonus * 2f;
                    warningFlags |= WarningFlags.AssumesThreeReactors;
                }

                if (entity.factorioType == "solar-panel")
                    warningFlags |= WarningFlags.AssumesNauvisSolarRatio;

                modules = default;
                if (moduleFiller != null && recipe.modules.Length > 0 && entity.allowedEffects != AllowedEffects.None)
                {
                    moduleFiller.GetModulesInfo(this, recipe, entity, fuel, ref activeEffects, ref modules);
                    productivity += activeEffects.productivity;
                    recipeTime /= (1f + activeEffects.speed);
                    fuelUsagePerSecondPerBuilding *= activeEffects.energyUsageMod;
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