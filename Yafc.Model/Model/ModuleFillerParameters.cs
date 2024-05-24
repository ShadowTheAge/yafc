using System;

namespace Yafc.Model {
    public interface IModuleFiller {
        void GetModulesInfo(RecipeParameters recipeParams, Recipe recipe, EntityCrafter entity, Goods? fuel, ref ModuleEffects effects, ref RecipeParameters.UsedModule used);
    }

    [Serializable]
    public class ModuleFillerParameters : ModelObject<ModelObject>, IModuleFiller {
        public ModuleFillerParameters(ModelObject owner) : base(owner) { }

        public bool fillMiners { get; set; }
        public float autoFillPayback { get; set; }
        public Module? fillerModule { get; set; }
        public EntityBeacon? beacon { get; set; }
        public Module? beaconModule { get; set; }
        public int beaconsPerBuilding { get; set; } = 8;

        [Obsolete("Moved to project settings", true)]
        public int miningProductivity {
            set {
                if (GetRoot() is Project rootProject && rootProject.settings.miningProductivity < value * 0.01f) {
                    rootProject.settings.miningProductivity = value * 0.01f;
                }
            }
        }

        public void AutoFillBeacons(RecipeParameters recipeParams, Recipe recipe, EntityCrafter entity, Goods? fuel, ref ModuleEffects effects, ref RecipeParameters.UsedModule used) {
            if (!recipe.flags.HasFlags(RecipeFlags.UsesMiningProductivity) && beacon != null && beaconModule != null) {
                effects.AddModules(beaconModule.moduleSpecification, beaconsPerBuilding * beacon.beaconEfficiency * beacon.moduleSlots, entity.allowedEffects);
                used.beacon = beacon;
                used.beaconCount = beaconsPerBuilding;
            }
        }

        public void AutoFillModules(RecipeParameters recipeParams, Recipe recipe, EntityCrafter entity, Goods? fuel, ref ModuleEffects effects, ref RecipeParameters.UsedModule used) {
            if (autoFillPayback > 0 && (fillMiners || !recipe.flags.HasFlags(RecipeFlags.UsesMiningProductivity))) {
                float productivityEconomy = recipe.Cost() / recipeParams.recipeTime;
                float effectivityEconomy = recipeParams.fuelUsagePerSecondPerBuilding * fuel?.Cost() ?? 0;
                if (effectivityEconomy < 0f) {
                    effectivityEconomy = 0f;
                }

                float bestEconomy = 0f;
                Module? usedModule = null;
                foreach (var module in recipe.modules) {
                    if (module.IsAccessibleWithCurrentMilestones() && entity.CanAcceptModule(module.moduleSpecification)) {
                        float economy = (MathF.Max(0f, module.moduleSpecification.productivity) * productivityEconomy) - (module.moduleSpecification.consumption * effectivityEconomy);
                        if (economy > bestEconomy && module.Cost() / economy <= autoFillPayback) {
                            bestEconomy = economy;
                            usedModule = module;
                        }
                    }
                }

                if (usedModule != null) {
                    int count = effects.GetModuleSoftLimit(usedModule.moduleSpecification, entity.moduleSlots);
                    if (count > 0) {
                        effects.AddModules(usedModule.moduleSpecification, count);
                        used.modules = new[] { (usedModule, count, false) };
                        return;
                    }
                }
            }

            if (fillerModule?.moduleSpecification != null && entity.CanAcceptModule(fillerModule.moduleSpecification) && recipe.CanAcceptModule(fillerModule)) {
                AddModuleSimple(fillerModule, ref effects, entity, ref used);
            }
        }

        public void GetModulesInfo(RecipeParameters recipeParams, Recipe recipe, EntityCrafter entity, Goods? fuel, ref ModuleEffects effects, ref RecipeParameters.UsedModule used) {
            AutoFillBeacons(recipeParams, recipe, entity, fuel, ref effects, ref used);
            AutoFillModules(recipeParams, recipe, entity, fuel, ref effects, ref used);
        }

        private void AddModuleSimple(Module module, ref ModuleEffects effects, EntityCrafter entity, ref RecipeParameters.UsedModule used) {
            if (module.moduleSpecification != null) {
                int fillerLimit = effects.GetModuleSoftLimit(module.moduleSpecification, entity.moduleSlots);
                effects.AddModules(module.moduleSpecification, fillerLimit);
                used.modules = new[] { (module, fillerLimit, false) };
            }
        }
    }
}
