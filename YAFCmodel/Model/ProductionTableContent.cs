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
        public float energyUsageMod => MathF.Max(1f + consumption, 0.2f);
        public void AddModules(ModuleSpecification module, float count, AllowedEffects allowedEffects)
        {
            if (allowedEffects.HasFlags(AllowedEffects.Speed))
                speed += module.speed * count;
            if (allowedEffects.HasFlags(AllowedEffects.Productivity) && module.productivity > 0f)
                productivity += module.productivity * count;
            if (allowedEffects.HasFlags(AllowedEffects.Consumption))
                consumption += module.consumption * count;
        }
        
        public void AddModules(ModuleSpecification module, float count)
        {
            speed += module.speed * count;
            if (module.productivity > 0f)
                productivity += module.productivity * count;
            consumption += module.consumption * count;
        }

        public int GetModuleSoftLimit(ModuleSpecification module, int hardLimit)
        {
            if (module == null)
                return 0;
            if (module.productivity > 0f || module.speed > 0f || module.pollution < 0f)
                return hardLimit;
            if (module.consumption < 0f)
                return MathUtils.Clamp(MathUtils.Ceil(-(consumption + 0.8f) / module.consumption), 0, hardLimit);
            return 0;
        }
    }
    
    [Serializable]
    public class RecipeRowCustomModule : ModelObject<CustomModules>
    {
        private Item _module;
        public Item module
        { 
            get => _module;
            set => _module = value ?? throw new ArgumentNullException(nameof(value));
        }
        public int fixedCount { get; set; }

        public RecipeRowCustomModule(CustomModules owner, Item module) : base(owner)
        {
            this.module = module;
        }
    }

    [Serializable]
    public class CustomModules : ModelObject<RecipeRow>
    {
        public Entity beacon { get; set; }
        public List<RecipeRowCustomModule> list { get; } = new List<RecipeRowCustomModule>();
        public List<RecipeRowCustomModule> beaconList { get; } = new List<RecipeRowCustomModule>();
        public CustomModules(RecipeRow owner) : base(owner) {}
        private static List<(Item module, int count)> buffer = new List<(Item module, int count)>();
        public float adjacency;
        public void GetModulesInfo(RecipeParameters recipeParams, Recipe recipe, Entity entity, Goods fuel, ref ModuleEffects effects, ref RecipeParameters.UsedModule used, ModuleFillerParameters filler)
        {
            var beaconedModules = 0;
            Item nonBeacon = null;
            buffer.Clear();
            used.modules = new (Item module, int count)[list.Count];
            var remaining = entity.moduleSlots;
            foreach (var module in list)
            {
                if (remaining <= 0)
                    break;
                var count = Math.Min(module.fixedCount == 0 ? int.MaxValue : module.fixedCount, remaining);
                remaining -= count;
                if (nonBeacon == null)
                    nonBeacon = module.module;
                buffer.Add((module.module, count));
                effects.AddModules(module.module.module, count);
            }

            used.modules = buffer.ToArray();

            if (beacon != null)
            {
                foreach (var module in beaconList)
                {
                    beaconedModules += module.fixedCount;
                    effects.AddModules(module.module.module, beacon.beaconEfficiency * module.fixedCount);
                }
                
                if (beaconedModules > 0)
                {
                    used.beacon = beacon;
                    used.beaconCount = ((beaconedModules-1) / beacon.moduleSlots + 1);
                }
            } else 
                filler?.AutoFillBeacons(recipeParams, recipe, entity, fuel, ref effects, ref used);
        }
    }
    
    public class RecipeRow : ModelObject<ProductionTable>, IModuleFiller
    {
        public Recipe recipe { get; }
        // Variable parameters
        public Entity entity { get; set; }
        public Goods fuel { get; set; }

        [Obsolete("Deprecated", true)]
        public Item module
        {
            set
            {
                if (value != null)
                {
                    modules = new CustomModules(this);
                    modules.list.Add(new RecipeRowCustomModule(modules, value));
                }
            }
        }

        public CustomModules modules { get; set; }
        public ProductionTable subgroup { get; set; }
        public bool hasVisibleChildren => subgroup != null && subgroup.expanded;
        public ModuleEffects moduleEffects;
        [SkipSerialization] public ProductionTable linkRoot => subgroup ?? owner;

        // Computed variables
        public RecipeParameters parameters { get; } = new RecipeParameters();
        public double recipesPerSecond { get; internal set; }
        public bool FindLink(Goods goods, out ProductionLink link) => linkRoot.FindLink(goods, out link);
        public bool isOverviewMode => subgroup != null && !subgroup.expanded;
        public float buildingCount => (float) recipesPerSecond * parameters.recipeTime;

        public RecipeRow(ProductionTable owner, Recipe recipe) : base(owner)
        {
            this.recipe = recipe ?? throw new ArgumentNullException(nameof(recipe), "Recipe does not exist");
        }

        protected internal override void ThisChanged(bool visualOnly)
        {
            owner.ThisChanged(visualOnly);
        }

        public void SetOwner(ProductionTable parent)
        {
            owner = parent;
        }

        public void RemoveFixedModules()
        {
            if (modules == null)
                return;
            CreateUndoSnapshot();
            modules = null;
        }
        public void SetFixedModule(Item module)
        {
            if (module == null)
            {
                RemoveFixedModules();
                return;
            }

            if (modules == null)
                this.RecordUndo().modules = new CustomModules(this);
            var list = modules.RecordUndo().list;
            list.Clear();
            list.Add(new RecipeRowCustomModule(modules, module));
        }

        public void GetModulesInfo(RecipeParameters recipeParams, Recipe recipe, Entity entity, Goods fuel, ref ModuleEffects effects, ref RecipeParameters.UsedModule used)
        {
            ModuleFillerParameters filler = null;
            if (modules == null || modules.beacon == null)
            {
                var table = linkRoot;
                while (table != null)
                {
                    if (table.modules != null)
                    {
                        filler = table.modules;
                        break;
                    }
                    table = (table.owner as RecipeRow)?.owner;
                }
            }

            if (modules == null)
                filler?.GetModulesInfo(recipeParams, recipe, entity, fuel, ref effects, ref used);
            else modules.GetModulesInfo(recipeParams, recipe, entity, fuel, ref effects, ref used, filler);

        }
    }
    
    public enum LinkAlgorithm
    {
        Match,
        AllowOverProduction,
        AllowOverConsumption,
    }

    public class ProductionLink : ModelObject<ProductionTable>
    {
        [Flags]
        public enum Flags
        {
            LinkNotMatched = 1 << 0,
            LinkRecursiveNotMatched = 1 << 1,
            HasConsumption = 1 << 2,
            HasProduction = 1 << 3,
            ChildNotMatched = 1 << 4,
            HasProductionAndConsumption = HasProduction | HasConsumption,
        }
        
        public Goods goods { get; }
        public float amount { get; set; }
        public LinkAlgorithm algorithm { get; set; }
        
        // computed variables
        public float minProductTemperature { get; internal set; }
        public float maxProductTemperature { get; internal set; }
        public float resultTemperature { get; internal set; }
        public Flags flags { get; internal set; }
        public float linkFlow { get; internal set; }
        public float notMatchedFlow { get; internal set; }
        internal int solverIndex;
        internal FactorioId lastRecipe;

        public ProductionLink(ProductionTable group, Goods goods) : base(group)
        {
            this.goods = goods ?? throw new ArgumentNullException(nameof(goods), "Linked product does not exist");
        }
    }
}