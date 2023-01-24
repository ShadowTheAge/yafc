using System;
using System.Collections.Generic;

namespace YAFC.Model
{
    public interface IModuleFiller
    {
        void GetModulesInfo(RecipeParameters recipeParams, Recipe recipe, EntityCrafter entity, Goods fuel, ref ModuleEffects effects, ref RecipeParameters.UsedModule used);
    }
    
    [Serializable]
    public record BeaconConfiguration(EntityBeacon beacon, int beaconCount)
    {
        public EntityBeacon beacon { get; set; } = beacon;
        public int beaconCount { get; set; } = beaconCount;
    }

    [Serializable]
    public class ModuleFillerParameters : ModelObject<ModelObject>, IModuleFiller
    {
        public ModuleFillerParameters(ModelObject owner) : base(owner) {}
        
        public bool fillMiners { get; set; }
        public float autoFillPayback { get; set; }
        public Item fillerModule { get; set; }
        public EntityBeacon beacon { get; set; }
        public Item beaconModule { get; set; }
        public int beaconsPerBuilding { get; set; } = 8;
        public SortedList<EntityCrafter, BeaconConfiguration> overrideCrafterBeacons { get; } = new(DataUtils.DeterministicComparer);

        [Obsolete("Moved to project settings", true)]
        public int miningProductivity
        {
            set
            {
                if (GetRoot() is Project rootProject && rootProject.settings.miningProductivity < value * 0.01f)
                    rootProject.settings.miningProductivity = value * 0.01f;
            }
        }

        public BeaconConfiguration GetBeaconsForCrafter(EntityCrafter crafter)
        {
            if (overrideCrafterBeacons.TryGetValue(crafter, out var result))
                return result;
            return new(beacon, beaconsPerBuilding);
        }

        public void AutoFillBeacons(RecipeParameters recipeParams, Recipe recipe, EntityCrafter entity, Goods fuel, ref ModuleEffects effects, ref RecipeParameters.UsedModule used)
        {
            BeaconConfiguration beaconsToUse = GetBeaconsForCrafter(entity);
            if (!recipe.flags.HasFlags(RecipeFlags.UsesMiningProductivity) && beaconsToUse.beacon is EntityBeacon beacon && beaconModule != null)
            {
                effects.AddModules(beaconModule.module, beaconsToUse.beaconCount * beacon.beaconEfficiency * beacon.moduleSlots, entity.allowedEffects);
                used.beacon = beacon;
                used.beaconCount = beaconsToUse.beaconCount;
            }
        }

        public void AutoFillModules(RecipeParameters recipeParams, Recipe recipe, EntityCrafter entity, Goods fuel, ref ModuleEffects effects, ref RecipeParameters.UsedModule used)
        {
            if (autoFillPayback > 0 && (fillMiners || !recipe.flags.HasFlags(RecipeFlags.UsesMiningProductivity)))
            {
                var productivityEconomy = recipe.Cost() / recipeParams.recipeTime;
                var effectivityEconomy = recipeParams.fuelUsagePerSecondPerBuilding * fuel?.Cost() ?? 0;
                if (effectivityEconomy < 0f)
                    effectivityEconomy = 0f;
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
                    var count = effects.GetModuleSoftLimit(usedModule.module, entity.moduleSlots);
                    if (count > 0)
                    {
                        effects.AddModules(usedModule.module, count);
                        used.modules = new[] {(usedModule, count, false)};
                        return;
                    }
                }
            }

            if (fillerModule?.module != null && entity.CanAcceptModule(fillerModule.module) && recipe.CanAcceptModule(fillerModule))
                AddModuleSimple(fillerModule, ref effects, entity, ref used);
        }

        public void GetModulesInfo(RecipeParameters recipeParams, Recipe recipe, EntityCrafter entity, Goods fuel, ref ModuleEffects effects, ref RecipeParameters.UsedModule used)
        {
            AutoFillModules(recipeParams, recipe, entity, fuel, ref effects, ref used);
            AutoFillBeacons(recipeParams, recipe, entity, fuel, ref effects, ref used);
        }

        private void AddModuleSimple(Item module, ref ModuleEffects effects, EntityCrafter entity, ref RecipeParameters.UsedModule used)
        {
            if (module.module != null)
            {
                var fillerLimit = effects.GetModuleSoftLimit(module.module, entity.moduleSlots);
                effects.AddModules(module.module, fillerLimit);
                used.modules = new[] {(module, fillerLimit, false)};
            }
        }
    }
}