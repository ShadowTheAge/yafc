using System;
using System.Collections.Generic;
using System.Linq;
using Google.OrTools.LinearSolver;
using YAFC.UI;

namespace YAFC.Model
{
    public struct ModuleEffects
    {
        public float speed;
        public float productivity;
        public float consumption;
        public float energyUsageMod => consumption < -0.8f ? -0.8f : 1f + consumption;
        public void AddModules(ModuleSpecification module, float count, AllowedEffects allowedEffects)
        {
            if ((allowedEffects & AllowedEffects.Speed) != 0)
                speed += module.speed * count;
            if ((allowedEffects & AllowedEffects.Productivity) != 0)
                productivity += module.productivity * count;
            if (((allowedEffects & AllowedEffects.Consumption) != 0))
                consumption += module.consumption * count;
        }
        
        public void AddModules(ModuleSpecification module, float count)
        {
            speed += module.speed * count;
            productivity += module.productivity * count;
            consumption += module.consumption * count;
        }

        public int GetModuleSoftLimit(ModuleSpecification module, int hardLimit)
        {
            if (module.productivity > 0f || module.speed > 0f || module.pollution < 0f)
                return hardLimit;
            if (module.consumption < 0f)
                return MathUtils.Clamp(MathUtils.Ceil(-(consumption + 0.8f) / module.consumption), 0, hardLimit);
            return 0;
        }
    }
    
    public class RecipeRow : ModelObject<ProductionTable>
    {
        public Recipe recipe { get; }
        // Variable parameters
        public Entity entity { get; set; }
        public Goods fuel { get; set; }
        public ProductionTable subgroup { get; set; }
        public bool hasVisibleChildren => subgroup != null && subgroup.expanded;
        public ModuleEffects modules;
        [SkipSerialization] public ProductionTable linkRoot => subgroup ?? owner;

        // Computed variables
        public RecipeParameters parameters { get; } = new RecipeParameters();
        public double recipesPerSecond { get; internal set; }
        public bool FindLink(Goods goods, out ProductionLink link) => linkRoot.FindLink(goods, out link);
        public bool isOverviewMode => subgroup != null && !subgroup.expanded;

        public RecipeRow(ProductionTable owner, Recipe recipe) : base(owner)
        {
            this.recipe = recipe;
        }

        protected internal override void ThisChanged(bool visualOnly)
        {
            owner.ThisChanged(visualOnly);
        }

        public void SetOwner(ProductionTable parent)
        {
            owner = parent;
        }
    }

    public class ProductionLink : ModelObject<ProductionTable>
    {
        [Flags]
        public enum Flags
        {
            LinkIsRecirsive = 1 << 0,
            LinkNotMatched = 1 << 1,
            HasConsumption = 1 << 2,
            HasProduction = 1 << 3,
            HasProductionAndConsumption = HasProduction | HasConsumption,
        }
        
        public Goods goods { get; }
        public float amount { get; set; }
        
        // computed variables
        public float minProductTemperature { get; internal set; }
        public float maxProductTemperature { get; internal set; }
        public float resultTemperature { get; internal set; }
        public Flags flags { get; internal set; }
        public float linkFlow { get; internal set; }
        internal int solverIndex;
        internal FactorioId lastRecipe;

        public ProductionLink(ProductionTable group, Goods goods) : base(group)
        {
            this.goods = goods;
        }

        protected internal override void ThisChanged(bool visualOnly)
        {
            base.ThisChanged(visualOnly);
            owner.ThisChanged(visualOnly);
        }
    }
}