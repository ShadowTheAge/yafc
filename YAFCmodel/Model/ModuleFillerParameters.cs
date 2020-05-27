using System;

namespace YAFC.Model
{
    [Serializable]
    public class ModuleFillerParameters : ModelObject<ModelObject>
    {
        public ModuleFillerParameters(ModelObject owner) : base(owner) {}
        
        public bool fillMiners { get; set; }
        public float autoFillPayback { get; set; }
        public Item fillerModule { get; set; }
        public Entity beacon { get; set; }
        public Item beaconModule { get; set; }
        public int beaconsPerBuilding { get; set; } = 8;
        public int miningProductivity { get; set; }
        
        protected internal override void ThisChanged(bool visualOnly)
        {
            owner.ThisChanged(visualOnly);
        }

        private void AddModuleSimple(Item module, ref ModuleEffects effects, Entity entity, ref RecipeParameters.UsedModule used)
        {
            if (module.module != null)
            {
                var fillerLimit = effects.GetModuleSoftLimit(module.module, entity.moduleSlots);
                effects.AddModules(module.module, fillerLimit);
                used.module = module;
                used.count = fillerLimit;
            }
        }

        public bool FillModules(RecipeParameters recipeParams, Recipe recipe, Entity entity, Goods fuel, Item forceModule, out ModuleEffects effects, out RecipeParameters.UsedModule used)
        {
            effects = new ModuleEffects();
            var isMining = recipe.flags.HasFlags(RecipeFlags.UsesMiningProductivity);
            var hasEffects = false;
            if (isMining && miningProductivity > 0f)
            {
                effects.productivity += 0.01f * miningProductivity;
                hasEffects = true;
            }
            used = default;
            if (!isMining && beacon != null && beaconModule != null)
            {
                effects.AddModules(beaconModule.module, beaconsPerBuilding * beacon.beaconEfficiency * beacon.moduleSlots, entity.allowedEffects);
                used.beacon = beacon;
                used.beaconCount = beaconsPerBuilding;
                hasEffects = true;
            }
            if (forceModule != null)
            {
                AddModuleSimple(forceModule, ref effects, entity, ref used);
                return true;
            }
            if (fillMiners || !isMining)
            {
                var productivityEconomy = recipe.Cost() / recipeParams.recipeTime;
                var effectivityEconomy = recipeParams.fuelUsagePerSecondPerBuilding * fuel?.Cost() ?? 0;
                var bestEconomy = 0f;
                Item usedModule = null;
                foreach (var module in recipe.modules)
                {
                    if (module.IsAccessibleWithCurrentMilestones() && entity.CanAcceptModule(module.module))
                    {
                        var economy = MathF.Max(0f, module.module.productivity) * productivityEconomy - module.module.consumption * effectivityEconomy;
                        if (economy > bestEconomy && module.Cost() / economy <= autoFillPayback)
                        {
                            bestEconomy = economy;
                            usedModule = module;
                        }
                    }
                }

                if (usedModule != null)
                {
                    effects.AddModules(usedModule.module, entity.moduleSlots);
                    used.module = usedModule;
                    used.count = entity.moduleSlots;
                    return true;
                }
            }

            if (fillerModule?.module != null && entity.CanAcceptModule(fillerModule.module))
            {
                AddModuleSimple(fillerModule, ref effects, entity, ref used);
                hasEffects = true;
            }
            return hasEffects;
        }
    }
}