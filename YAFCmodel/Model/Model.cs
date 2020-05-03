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
    
    public class RecipeRow : ModelObject, IDisposable
    {
        public Recipe recipe { get; }
        public readonly Group owner;
        internal readonly Variable recipesPerSecond;
        // Variable parameters
        public Entity entity { get; set; }
        public Goods fuel { get; set; }
        public ModuleSpec modules;

        // Computed variables
        public WarningFlags warningFlags;
        public float recipeTime;
        public float productionMultiplier;
        public float energyUsage;

        public RecipeRow(Group owner, Recipe recipe) : base(owner)
        {
            this.recipe = recipe;
            this.owner = owner;
            recipesPerSecond = owner.solver.MakeVar(0, double.PositiveInfinity, false, "building_count_" + recipe.name);
        }

        public void Dispose()
        {
            recipesPerSecond?.Dispose();
        }

        protected internal override void ThisChanged()
        {
            owner.RecipeChanged();
        }
    }

    public class GroupLink : ModelObject
    {
        public readonly Group group;
        public Goods goods { get; }
        public float amount { get; set; }
        public float temperature;
        internal readonly Constraint productionConstraint;
        
        public GroupLink(Group group, Goods goods)
        {
            this.goods = goods;
            this.group = group;
            productionConstraint = group.solver.MakeConstraint(0, 0);
        }
    }

    public class Group : ModelObject
    {
        public List<GroupLink> links { get; } = new List<GroupLink>();
        public List<RecipeRow> recipes { get; } = new List<RecipeRow>();
        public readonly Solver solver;
        public event Action metaInfoChanged;
        public event Action recipesChanged;

        public Group(ModelObject owner) : base(owner)
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

        protected internal override void ThisChanged()
        {
            metaInfoChanged?.Invoke();
        }

        public void RecipeChanged()
        {
            recipesChanged?.Invoke();
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

        public GroupLink GetLink(Goods goods)
        {
            foreach (var link in links)
            {
                if (link.goods == goods)
                    return link;
            }

            return null;
        }
    }
}