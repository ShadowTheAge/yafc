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
        FuelNotSpecified = 1 << 1,
        LinkedProductionNotConsumed = 1 << 3,
        LinkedConsumptionNotProduced = 1 << 4,
        FuelWithTemperatureNotLinked = 1 << 5,
        
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
    
    public class RecipeRow : ModelObject
    {
        public Recipe recipe { get; }
        [SkipSerialization] public new ProductionTable owner { get; private set; }
        // Variable parameters
        public Entity entity { get; set; }
        public Goods fuel { get; set; }
        public ProductionTable subgroup { get; set; }
        public bool hasVisibleChildren => subgroup != null && subgroup.expanded;
        public ModuleSpec modules;
        [SkipSerialization] public ProductionTable linkRoot => subgroup ?? owner;

        // Computed variables
        public WarningFlags warningFlags { get; internal set; }
        public float recipeTime { get; internal set; }
        public float fuelUsagePerSecond { get; internal set; }
        public float productionMultiplier { get; internal set; }
        public float recipesPerSecond { get; internal set; }

        public bool FindLink(Goods goods, out ProductionLink link)
        {
            var searchFrom = linkRoot;
            while (true)
            {
                if (searchFrom.linkMap.TryGetValue(goods, out link))
                    return true;
                if (searchFrom.owner is RecipeRow row)
                    searchFrom = row.owner;
                else return false;
            }
        }

        public RecipeRow(ProductionTable owner, Recipe recipe) : base(owner)
        {
            this.recipe = recipe;
            this.owner = owner;
        }

        protected internal override void ThisChanged(bool visualOnly)
        {
            owner.ThisChanged(visualOnly);
        }

        public void SetOwner(ProductionTable parent)
        {
            owner = parent;
            base.owner = parent;
        }
    }

    public class ProductionLink : ModelObject
    {
        public readonly ProductionTable group;
        public Goods goods { get; }
        public float amount { get; set; }
        
        // computed variables
        public float minProductTemperature { get; internal set; }
        public float maxProductTemperature { get; internal set; }
        public float resultTemperature { get; internal set; }

        public ProductionLink(ProductionTable group, Goods goods) : base(group)
        {
            this.goods = goods;
            this.group = group;
        }

        protected internal override void ThisChanged(bool visualOnly)
        {
            base.ThisChanged(visualOnly);
            group.ThisChanged(visualOnly);
        }
    }
}