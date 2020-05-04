using System;
using System.Collections.Generic;
using System.Linq;
using Google.OrTools.LinearSolver;

namespace YAFC.Model
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

    public struct ModuleSpec
    {
        public float speed;
        public float productivity;
        public float efficiency;

        public float energyUsageMod => efficiency > 0.8f ? 0.2f : (1f - efficiency);

        public static ModuleSpec operator *(ModuleSpec spec, float number)
        {
            return new ModuleSpec
            {
                speed = spec.speed * number,
                productivity = spec.productivity * number,
                efficiency = spec.efficiency * number
            };
        }

        public static ModuleSpec operator +(ModuleSpec a, ModuleSpec b)
        {
            return new ModuleSpec
            {
                speed = a.speed + b.speed,
                productivity = a.productivity + b.productivity,
                efficiency = a.efficiency + b.efficiency
            };
        }
    }
    
    public class RecipeRow : ModelObject, IDisposable, IGoodsWithAmount
    {
        public Recipe recipe { get; }
        public readonly ProductionTable owner;
        internal Variable recipesPerSecond;
        // Variable parameters
        public Entity entity { get; set; }
        public Goods fuel { get; set; }
        public ModuleSpec modules;

        // Computed variables
        public WarningFlags warningFlags;
        public float recipeTime;
        public float productionMultiplier;
        public float energyUsage;

        public RecipeRow(ProductionTable owner, Recipe recipe) : base(owner)
        {
            this.recipe = recipe;
            this.owner = owner;
        }

        public void Dispose()
        {
            recipesPerSecond?.Dispose();
        }

        protected internal override void ThisChanged()
        {
            owner.RecipeChanged();
        }

        Goods IGoodsWithAmount.goods => fuel;
        float IGoodsWithAmount.amount => 1f; // todo
    }

    public interface IGoodsWithAmount
    {
        Goods goods { get; }
        float amount { get; }
    }

    public class GroupLink : ModelObject, IGoodsWithAmount
    {
        public readonly ProductionTable group;
        public Goods goods { get; }
        public float amount { get; set; }
        public float temperature;
        internal Constraint productionConstraint;
        
        public GroupLink(ProductionTable group, Goods goods) : base(group)
        {
            this.goods = goods;
            this.group = group;
        }
    }

    public class ProductionTable : ProjectPageContents
    {
        public List<GroupLink> links { get; } = new List<GroupLink>();
        public List<RecipeRow> recipes { get; } = new List<RecipeRow>();
        public event Action metaInfoChanged;
        public event Action recipesChanged;

        public ProductionTable(ModelObject owner) : base(owner) {}

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

                    coefArray[i] += product.average * recipe.productionMultiplier;
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