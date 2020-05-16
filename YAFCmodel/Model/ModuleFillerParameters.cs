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
        
        protected internal override void ThisChanged(bool visualOnly)
        {
            owner.ThisChanged(visualOnly);
        }

        public bool FillModules(RecipeParameters recipeParams, Recipe recipe, Entity entity, Goods fuel, out ModuleEffects effects, out RecipeParameters.UsedModule used)
        {
            effects = new ModuleEffects();
            var isMining = (recipe.flags & RecipeFlags.UsesMiningProductivity) != 0;
            var hasEffects = false;
            used = default;
            if (!isMining && beacon != null && beaconModule != null)
            {
                effects.AddModules(beaconModule.module, beaconsPerBuilding * beacon.beaconEfficiency, entity.allowedEffects);
                used.beacon = beacon;
                used.beaconCount = beaconsPerBuilding;
                hasEffects = true;
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
                        var economy = module.module.productivity * productivityEconomy - module.module.consumption * effectivityEconomy;
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
                var fillerLimit = effects.GetModuleSoftLimit(fillerModule.module, entity.moduleSlots);
                effects.AddModules(fillerModule.module, fillerLimit);
                used.module = fillerModule;
                used.count = fillerLimit;
                hasEffects = true;
            }
            else
                used = default;

            return hasEffects;
        }
    }
}