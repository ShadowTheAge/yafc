using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Google.OrTools.LinearSolver;
using YAFC.Model;

namespace YAFC
{
    [Flags]
    public enum WarningFlags
    {
        // Static errors
        EntityNotSpecified = 1 << 0,
        FuelNotLinked = 1 << 1,
        BoilerInputNotLinked = 1 << 2,
        LinkedProductionNotConsumed = 1 << 3,
        LinkedConsumptionNotProduced = 1 << 4,
        
        // Not implemented warnings
        TemperatureForIngredientNotMatch = 1 << 8,
        TemperatureRangeForFuelNotImplemented = 1 << 9,
        TemperatureRangeForBoilerNotImplemented = 1 << 10,
        
        // Solutionerrors
        UnfeasibleCandidate = 1 << 16
    }
    
    public class RecipeRow : IDisposable
    {
        public readonly Recipe recipe;
        public readonly Group owner;
        internal readonly Variable recipesPerSecond;
        // Variable parameters
        public Entity entity;
        public ModuleSpec modules;
        public Goods fuel;

        // Computed variables
        public WarningFlags warningFlags;
        public float recipeTime;
        public float productionMultiplier;
        public float energyUsage;

        public RecipeRow(Group owner, Recipe recipe)
        {
            this.recipe = recipe;
            this.owner = owner;
            recipesPerSecond = owner.solver.MakeVar(0, double.PositiveInfinity, false, "building_count_" + recipe.name);
        }

        public void Dispose()
        {
            recipesPerSecond?.Dispose();
        }
    }

    public class GroupLink : IDisposable
    {
        public readonly Group owner;
        public readonly Goods goods;
        internal readonly Constraint productionConstraint;
        
        public GroupLink(Group owner, Goods goods)
        {
            this.goods = goods;
            this.owner = owner;
            productionConstraint = owner.solver.MakeConstraint(0, 0);
        }

        public float temperature;

        public void Dispose()
        {
            productionConstraint?.Dispose();
        }
    }

    public class DesiredProduct : IFactorioObjectWrapper
    {
        public readonly Goods goods;
        public float amount = 1f;
        public DesiredProduct(Goods goods)
        {
            this.goods = goods;
        }

        public string text => amount.ToString();
        public FactorioObject target => goods;
    }

    public class Group
    {
        public List<GroupLink> links = new List<GroupLink>();
        public List<RecipeRow> recipes = new List<RecipeRow>();
        public List<DesiredProduct> desiredProducts = new List<DesiredProduct>();
        public readonly Solver solver;

        public Group()
        {
            solver = Solver.CreateSolver("GroupSolver", "GLOP_LINEAR_PROGRAMMING");
        }

        public void SetupSolving(Solver solver)
        {
            solver.Reset();
            foreach (var recipe in recipes)
            {
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
                    recipe.productionMultiplier = (recipe.recipe.flags & RecipeFlags.ProductivityDisabled) != 0 ? 1f : (1f + recipe.modules.productivity) * (1f + recipe.entity.productivity);
                    recipe.energyUsage = recipe.entity.power * recipe.modules.energyUsageMod / recipe.entity.energy.effectivity;

                    // Set up flags that will be cleared if this is actually specified
                    if ((recipe.recipe.flags & RecipeFlags.ScaleProductionWithPower) != 0)
                        recipe.warningFlags |= WarningFlags.FuelNotLinked;
                    if ((recipe.recipe.flags & RecipeFlags.UsesFluidTemperature) != 0)
                        recipe.warningFlags |= WarningFlags.BoilerInputNotLinked;
                }
            }
            
            var coefArray = new float[recipes.Count];
            // process heat and electricity last, as production of heat and electricity depends on other 
            
            foreach (var link in links)
            {
                SetupSolverLink(link, coefArray);
            }
        }

        private void SetupSolverLink(GroupLink link, float[] coefArray)
        {
            Array.Clear(coefArray, 0, coefArray.Length);
            var goods = link.goods;
            var constraint = link.productionConstraint;
            var fluid = goods as Fluid;
            float minTemp = float.PositiveInfinity, maxTemp = float.NegativeInfinity;
            bool hasProduction = false, hasConsumption = false;
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

                    coefArray[i] += product.amount * recipe.productionMultiplier;
                    hasProduction = true;
                }
            }

            var hasTemperatureRange = minTemp != maxTemp;

            for (var i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                if (recipe.fuel == goods && recipe.entity != null)
                {
                    var energy = recipe.entity.energy;
                    var usesHeat = fluid != null && energy.usesHeat;
                    if (hasTemperatureRange && usesHeat)
                    {
                        // TODO research this case;
                        recipe.warningFlags |= WarningFlags.TemperatureRangeForFuelNotImplemented;
                    }
                    
                    float actualFuelUsage;

                    if (usesHeat)
                    {
                        var heatCap = fluid.heatCapacity;
                        var energyPerUnitOfFluid = (minTemp - energy.minTemperature) * heatCap;
                        var maxEnergyProduction = energy.fluidLimit * energyPerUnitOfFluid;
                        if (maxEnergyProduction < recipe.energyUsage || recipe.energyUsage == 0) // limited by fluid limit
                        {
                            if (recipe.energyUsage != 0)
                                recipe.recipeTime *= recipe.energyUsage / maxEnergyProduction;
                            recipe.energyUsage = maxEnergyProduction;
                            actualFuelUsage = energy.fluidLimit;
                        }
                        else // limited by energy usage
                            actualFuelUsage = recipe.energyUsage / energyPerUnitOfFluid;
                    }
                    else
                        actualFuelUsage = recipe.energyUsage / recipe.fuel.fuelValue;
                    if ((recipe.recipe.flags & RecipeFlags.ScaleProductionWithPower) != 0)
                    {
                        // TODO this affects other links
                        recipe.productionMultiplier *= recipe.energyUsage * energy.effectivity;
                        recipe.warningFlags &= ~WarningFlags.FuelNotLinked;
                    }

                    coefArray[i] -= actualFuelUsage * recipe.recipeTime;
                    hasConsumption = true;
                }

                foreach (var ingredient in recipe.recipe.ingredients)
                {
                    if (ingredient.goods != goods)
                        continue;
                    if (fluid != null && (recipe.recipe.flags & RecipeFlags.UsesFluidTemperature) != 0)
                    {
                        recipe.warningFlags &= ~WarningFlags.BoilerInputNotLinked;
                        if (hasTemperatureRange)
                            recipe.warningFlags |= WarningFlags.TemperatureRangeForBoilerNotImplemented;
                        var outputTemp = recipe.recipe.products[0].temperature;
                        var deltaTemp = (outputTemp - minTemp);
                        var energyPerUnitOfFluid = deltaTemp * fluid.heatCapacity;
                        if (deltaTemp > 0)
                            recipe.recipeTime = energyPerUnitOfFluid / recipe.energyUsage;
                    }

                    coefArray[i] -= ingredient.amount;
                    hasConsumption = true;
                }
            }

            if (hasProduction && hasConsumption)
            {
                for (var i = 0; i < coefArray.Length; i++)
                {
                    if (coefArray[i] != 0f)
                        constraint.SetCoefficient(recipes[i].recipesPerSecond, coefArray[i]);
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
    }
}