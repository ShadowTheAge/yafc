using System;
using System.Collections.Generic;

namespace Yafc.Model {
    public interface IModuleFiller {
        void GetModulesInfo(RecipeParameters recipeParams, Recipe recipe, EntityCrafter entity, Goods? fuel, ref ModuleEffects effects, ref RecipeParameters.UsedModule used);
    }

    /// <summary>
    /// An entry in the per-crafter beacon override configuration. It must specify both a beacon and a module, but it may specify zero beacons.
    /// </summary>
    /// <param name="beacon">The beacon to use for this crafter.</param>
    /// <param name="beaconCount">The number of beacons to use. The total number of modules in beacons is this value times the number of modules that can be placed in a beacon.</param>
    /// <param name="beaconModule">The module to place in the beacon.</param>
    [Serializable]
    public record BeaconOverrideConfiguration(EntityBeacon beacon, int beaconCount, Module beaconModule) {
        /// <summary>
        /// Gets or sets the beacon to use for this crafter.
        /// </summary>
        public EntityBeacon beacon { get; set; } = beacon;
        /// <summary>
        /// Gets or sets the number of beacons to use. The total number of modules in beacons is this value times the number of modules that can be placed in a beacon.
        /// </summary>
        public int beaconCount { get; set; } = beaconCount;
        /// <summary>
        /// Gets or sets the module to place in the beacon.
        /// </summary>
        public Module beaconModule { get; set; } = beaconModule;
    }

    /// <summary>
    /// The result of applying the various beacon preferences to a crafter; this may result in a desired configuration where the beacon or module is not specified.
    /// </summary>
    /// <param name="beacon">The beacon to use for this crafter, or <see langword="null"/> if no beacons or beacon modules should be used.</param>
    /// <param name="beaconCount">The number of beacons to use. The total number of modules in beacons is this value times the number of modules that can be placed in a beacon.</param>
    /// <param name="beaconModule">The module to place in the beacon, or <see langword="null"/> if no beacons or beacon modules should be used.</param>
    [Serializable]
    public record BeaconConfiguration(EntityBeacon? beacon, int beaconCount, Module? beaconModule) {
        public static implicit operator BeaconConfiguration(BeaconOverrideConfiguration beaconConfiguration) => new(beaconConfiguration.beacon, beaconConfiguration.beaconCount, beaconConfiguration.beaconModule);
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
        public SortedList<EntityCrafter, BeaconOverrideConfiguration> overrideCrafterBeacons { get; } = new(DataUtils.DeterministicComparer);

        [Obsolete("Moved to project settings", true)]
        public int miningProductivity {
            set {
                if (GetRoot() is Project rootProject && rootProject.settings.miningProductivity < value * 0.01f) {
                    rootProject.settings.miningProductivity = value * 0.01f;
                }
            }
        }

        /// <summary>
        /// Given a building that accepts beacon effects, return the type and number of beacons that should affect that building, along with the module to place in those beacons.
        /// </summary>
        /// <param name="crafter">The building to be affected by beacons.</param>
        /// <returns>The type and number of beacons to apply to that type of building, along with the module that will be placed in the beacons.</returns>
        public BeaconConfiguration GetBeaconsForCrafter(EntityCrafter? crafter) {
            if (crafter is not null && overrideCrafterBeacons.TryGetValue(crafter, out var result)) {
                return result;
            }
            return new(beacon, beaconsPerBuilding, beaconModule);
        }

        public void AutoFillBeacons(RecipeParameters recipeParams, Recipe recipe, EntityCrafter entity, Goods? fuel, ref ModuleEffects effects, ref RecipeParameters.UsedModule used) {
            BeaconConfiguration beaconsToUse = GetBeaconsForCrafter(entity);
            if (!recipe.flags.HasFlags(RecipeFlags.UsesMiningProductivity) && beaconsToUse.beacon is EntityBeacon beacon && beaconsToUse.beaconModule != null) {
                effects.AddModules(beaconsToUse.beaconModule.moduleSpecification, beaconsToUse.beaconCount * beacon.beaconEfficiency * beacon.moduleSlots, entity.allowedEffects);
                used.beacon = beacon;
                used.beaconCount = beaconsToUse.beaconCount;
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
