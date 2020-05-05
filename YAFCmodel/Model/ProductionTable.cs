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
    
    public class RecipeRow : ModelObject, IGoodsWithAmount
    {
        public Recipe recipe { get; }
        public readonly ProductionTable owner;
        // Variable parameters
        public Entity entity { get; set; }
        public Goods fuel { get; set; }
        public ModuleSpec modules;

        // Computed variables
        public WarningFlags warningFlags;
        public float recipeTime;
        public float fuelUsagePerSecond;
        public float productionMultiplier;
        public float recipesPerSecond;

        public RecipeRow(ProductionTable owner, Recipe recipe) : base(owner)
        {
            this.recipe = recipe;
            this.owner = owner;
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
        
        // computed variables
        public float minProductTemperature { get; internal set; }
        public float maxProductTemperature { get; internal set; }
        public float resultTemperature { get; internal set; }

        public GroupLink(ProductionTable group, Goods goods) : base(group)
        {
            this.goods = goods;
            this.group = group;
        }

        protected internal override void ThisChanged()
        {
            base.ThisChanged();
            group.MetaChanged();
        }
    }

    public partial class ProductionTable : ProjectPageContents
    {
        public List<GroupLink> links { get; } = new List<GroupLink>();
        public List<RecipeRow> recipes { get; } = new List<RecipeRow>();
        public event Action metaInfoChanged;
        public event Action recipesChanged;
        public ProductionTable(ModelObject owner) : base(owner) {}
        private bool active;
        private bool solutionInProgress;
        private uint lastSolvedVersion;

        public override void SetActive(bool active)
        {
            this.active = active;
            if (active && hierarchyVersion > lastSolvedVersion)
                Solve();
        }
        
        protected internal override void ThisChanged() => MetaChanged();

        public void MetaChanged()
        {
            metaInfoChanged?.Invoke();
            if (active)
                Solve();
        }

        public void RecipeChanged()
        {
            recipesChanged?.Invoke();
            if (active)
                Solve();
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