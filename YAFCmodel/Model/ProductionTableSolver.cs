using System;
using System.Collections.Generic;
using System.Linq;
using Google.OrTools.LinearSolver;

namespace YAFC.Model
{
    public partial class ProductionTable
    {
        private void Solve()
        {
            var solver = DataUtils.CreateSolver("ProductionTableSolver");
            var objective = solver.Objective();
            objective.SetMinimization();
            var vars = new Variable[recipes.Count];
            var objCoefs = new float[recipes.Count];
            var linkMapping = new Dictionary<Goods, GroupLink>();
            var coefArray = new float[recipes.Count];
            // Step 1: Set up mapping goods => link (this is temporary)
            foreach (var link in links)
            {
                if (link.goods is Fluid fluid)
                {
                    link.maxProductTemperature = fluid.minTemperature;
                    link.minProductTemperature = fluid.maxTemperature;
                }
                linkMapping[link.goods] = link;
            }

            // Step 2: Calculate links temperature
            foreach (var recipe in recipes)
            {
                foreach (var product in recipe.recipe.products)
                {
                    if (product.goods is Fluid fluid && linkMapping.TryGetValue(fluid, out var fluidLink))
                    {
                        fluidLink.maxProductTemperature = MathF.Max(fluidLink.maxProductTemperature, product.temperature);
                        fluidLink.minProductTemperature = MathF.Min(fluidLink.minProductTemperature, product.temperature);
                    }
                }
            }
            
            // Step 3: Calculate base recipe and fuel parameters
            for (var i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                recipe.warningFlags = 0;
                if (recipe.entity == null)
                {
                    recipe.warningFlags |= WarningFlags.EntityNotSpecified;
                    recipe.recipeTime = recipe.recipe.time;
                    recipe.productionMultiplier = 1f;
                }
                else
                {
                    recipe.recipeTime = recipe.recipe.time / (recipe.entity.craftingSpeed * (1f + recipe.modules.speed));
                    recipe.productionMultiplier = (1f + recipe.modules.productivity) * (1f + recipe.entity.productivity);
                    var energyUsage = recipe.entity.power * recipe.modules.energyUsageMod / recipe.entity.energy.effectivity;
                    
                    if ((recipe.recipe.flags & RecipeFlags.ScaleProductionWithPower) != 0)
                        recipe.warningFlags |= WarningFlags.FuelWithTemperatureNotLinked;

                    // Special case for fuel
                    if (recipe.fuel != null)
                    {
                        var fluid = recipe.fuel as Fluid;
                        var energy = recipe.entity.energy;
                        linkMapping.TryGetValue(recipe.fuel, out var link);
                        var usesHeat = fluid != null && energy.usesHeat;
                        if (usesHeat)
                        {
                            if (link == null)
                            {
                                recipe.fuelUsagePerSecond = float.NaN;
                            }
                            else
                            {
                                // TODO research this case;
                                if (link.maxProductTemperature != link.minProductTemperature)
                                    recipe.warningFlags |= WarningFlags.TemperatureRangeForFuelNotImplemented;

                                var heatCap = fluid.heatCapacity;
                                var energyPerUnitOfFluid = (link.minProductTemperature - energy.minTemperature) * heatCap;
                                var maxEnergyProduction = energy.fluidLimit * energyPerUnitOfFluid;
                                if (maxEnergyProduction < energyUsage || energyUsage <= 0) // limited by fluid limit
                                {
                                    if (energyUsage <= 0)
                                        recipe.recipeTime *= energyUsage / maxEnergyProduction;
                                    energyUsage = maxEnergyProduction;
                                    recipe.fuelUsagePerSecond = energy.fluidLimit;
                                }
                                else // limited by energy usage
                                    recipe.fuelUsagePerSecond = energyUsage / energyPerUnitOfFluid;
                            }
                        }
                        else
                            recipe.fuelUsagePerSecond = energyUsage / recipe.fuel.fuelValue;

                        if ((recipe.recipe.flags & RecipeFlags.ScaleProductionWithPower) != 0 && link != null)
                        {
                            recipe.recipeTime = 1f / energyUsage;
                            recipe.warningFlags &= ~WarningFlags.FuelWithTemperatureNotLinked;
                        }
                    }
                    else
                    {
                        recipe.fuelUsagePerSecond = energyUsage;
                        recipe.warningFlags |= WarningFlags.FuelNotSpecified;
                    }

                    // Special case for boilers
                    if ((recipe.recipe.flags & RecipeFlags.UsesFluidTemperature) != 0)
                    {
                        var fluid = recipe.recipe.ingredients[0].goods as Fluid;
                        if (fluid == null)
                            continue;
                        float inputTemperature;
                        if (linkMapping.TryGetValue(fluid, out var link))
                        {
                            if (link.maxProductTemperature != link.minProductTemperature)
                                recipe.warningFlags |= WarningFlags.TemperatureRangeForBoilerNotImplemented;
                            inputTemperature = link.minProductTemperature;
                        }
                        else inputTemperature = fluid.minTemperature;
                            
                        var outputTemp = recipe.recipe.products[0].temperature;
                        var deltaTemp = (outputTemp - inputTemperature);
                        var energyPerUnitOfFluid = deltaTemp * fluid.heatCapacity;
                        if (deltaTemp > 0 && recipe.fuel != null)
                            recipe.recipeTime = energyPerUnitOfFluid / (recipe.fuelUsagePerSecond * recipe.fuel.fuelValue);
                    }
                }
                var variable = solver.MakeNumVar(0d, double.PositiveInfinity, recipe.recipe.name);
                vars[i] = variable;
            }
            
            // Step 4: Set up link constraints

            foreach (var link in links)
            {
                var constraint = solver.MakeConstraint(link.amount, double.PositiveInfinity);
                Array.Clear(coefArray, 0, coefArray.Length);
                var goods = link.goods;
                var fluid = goods as Fluid;
                float minTemp = float.PositiveInfinity, maxTemp = float.NegativeInfinity;
                var hasProduction = link.amount < 0f;
                var hasConsumption = link.amount > 0f;
                for (var i = 0; i < recipes.Count; i++)
                {
                    var recipe = recipes[i];
                    foreach (var product in recipe.recipe.products)
                    {
                        if (product.goods != goods)
                            continue;
                        if (fluid != null)
                        {
                            minTemp = MathF.Min(minTemp, product.temperature);
                            maxTemp = MathF.Max(maxTemp, product.temperature);
                        }
                        var added = product.average * recipe.productionMultiplier;

                        coefArray[i] += added;
                        objCoefs[i] -= added;
                        hasProduction = true;
                    }
                }
                for (var i = 0; i < recipes.Count; i++)
                {
                    var recipe = recipes[i];
                    if (recipe.fuel == goods && recipe.entity != null)
                    {
                        coefArray[i] -= recipe.fuelUsagePerSecond * recipe.recipeTime;
                        objCoefs[i] += recipe.fuelUsagePerSecond * recipe.recipeTime;
                        hasConsumption = true;
                    }

                    foreach (var ingredient in recipe.recipe.ingredients)
                    {
                        if (ingredient.goods != goods)
                            continue;
                        coefArray[i] -= ingredient.amount;
                        objCoefs[i] += ingredient.amount;
                        hasConsumption = true;
                    }
                }

                if (hasProduction && hasConsumption)
                {
                    for (var i = 0; i < coefArray.Length; i++)
                    {
                        if (coefArray[i] != 0f)
                            constraint.SetCoefficient(vars[i], coefArray[i]);
                    }
                }
                else
                {
                    if (hasProduction)
                    {
                        foreach (var recipe in recipes.Where(recipe => recipe.recipe.products.Any(product => product.goods == goods)))
                            recipe.warningFlags |= WarningFlags.LinkedProductionNotConsumed;
                    }

                    if (hasConsumption)
                    {
                        foreach (var recipe in recipes.Where(recipe => recipe.fuel == goods || recipe.recipe.ingredients.Any(ingredient => ingredient.goods == goods)))
                            recipe.warningFlags |= WarningFlags.LinkedConsumptionNotProduced;
                    }
                }
            }
            
            // Now solve

            for (var i = 0; i < recipes.Count; i++)
                objective.SetCoefficient(vars[i], recipes[i].recipe.Cost());
            
            var result = solver.Solve();
            lastSolvedVersion = hierarchyVersion;
            Console.WriteLine("Solver finished with result "+result);
            Console.WriteLine(solver.ExportModelAsLpFormat(false));
            if (result != Solver.ResultStatus.OPTIMAL || result == Solver.ResultStatus.FEASIBLE)
            {
                solver.Dispose();
                return;
            }
            for (var i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                recipe.recipesPerSecond = (float)vars[i].SolutionValue();
            }
            metaInfoChanged?.Invoke();
        }
    }
}