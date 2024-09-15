using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Yafc.Model;
/// <summary>
/// An entry in the per-crafter beacon override configuration. It must specify both a beacon and a module, but it may specify zero beacons.
/// </summary>
/// <param name="beacon">The beacon to use for this crafter.</param>
/// <param name="beaconCount">The number of beacons to use. The total number of modules in beacons is this value times the number of modules that can be placed in a beacon.</param>
/// <param name="beaconModule">The module to place in the beacon.</param>
[Serializable]
public record BeaconOverrideConfiguration(EntityBeacon beacon, int beaconCount, Module beaconModule);

/// <summary>
/// The result of applying the various beacon preferences to a crafter; this may result in a desired configuration where the beacon or module is not specified.
/// </summary>
/// <param name="beacon">The beacon to use for this crafter, or <see langword="null"/> if no beacons or beacon modules should be used.</param>
/// <param name="beaconCount">The number of beacons to use. The total number of modules in beacons is this value times the number of modules that can be placed in a beacon.</param>
/// <param name="beaconModule">The module to place in the beacon, or <see langword="null"/> if no beacons or beacon modules should be used.</param>
[Serializable]
public record BeaconConfiguration(EntityBeacon? beacon, int beaconCount, Module? beaconModule) {
    public static implicit operator BeaconConfiguration(BeaconOverrideConfiguration beaconConfiguration) =>
        new(beaconConfiguration.beacon, beaconConfiguration.beaconCount, beaconConfiguration.beaconModule);
}

/// <summary>
/// The settings (used by root <see cref="ProductionTable"/>s) for what modules and beacons should be used for a recipe, in the absence of any per-<see cref="RecipeRow"/> settings.
/// </summary>
/// <remarks>This class handles its own <see cref="DataUtils.RecordUndo{T}(T, bool)"/> calls.</remarks>
[Serializable]
public class ModuleFillerParameters : ModelObject<ModelObject> {
    private bool _fillMiners;
    private float _autoFillPayback;
    private Module? _fillerModule;
    private EntityBeacon? _beacon;
    private Module? _beaconModule;
    private int _beaconsPerBuilding = 8;

    public ModuleFillerParameters(ModelObject owner) : base(owner) => overrideCrafterBeacons.OverrideSettingChanging += ModuleFillerParametersChanging;

    public bool fillMiners {
        get => _fillMiners;
        set => ChangeModuleFillerParameters(ref _fillMiners, value);
    }
    public float autoFillPayback {
        get => _autoFillPayback;
        set => ChangeModuleFillerParameters(ref _autoFillPayback, value);
    }
    public Module? fillerModule {
        get => _fillerModule;
        set => ChangeModuleFillerParameters(ref _fillerModule, value);
    }
    public EntityBeacon? beacon {
        get => _beacon;
        set => ChangeModuleFillerParameters(ref _beacon, value);
    }
    public Module? beaconModule {
        get => _beaconModule;
        set => ChangeModuleFillerParameters(ref _beaconModule, value);
    }
    public int beaconsPerBuilding {
        get => _beaconsPerBuilding;
        set => ChangeModuleFillerParameters(ref _beaconsPerBuilding, value);
    }
    public OverrideCrafterBeacons overrideCrafterBeacons { get; } = [];

    [Obsolete("Moved to project settings", true)]
    public int miningProductivity {
        set {
            if (GetRoot() is Project rootProject && rootProject.settings.miningProductivity < value * 0.01f) {
                rootProject.settings.miningProductivity = value * 0.01f;
            }
        }
    }

    private void ChangeModuleFillerParameters<T>(ref T field, T value) {
        Action? action = ModuleFillerParametersChanging();
        field = value;
        action?.Invoke();
    }

    private Action? ModuleFillerParametersChanging(EntityCrafter? crafter = null) {
        if (SerializationMap.IsDeserializing) { return null; } // Deserializing; don't do anything fancy.

        _ = this.RecordUndo();
        ModelObject parent = owner;
        while (parent.ownerObject is not ProjectPage and not null) {
            parent = parent.ownerObject;
        }
        ProductionTable table = (ProductionTable)parent;

        Action? result = null;
        foreach (RecipeRow recipe in table.GetAllRecipes()) {
            if (crafter == null || crafter == recipe.entity) {
                result += recipe.ModuleFillerParametersChanging();
            }
        }
        return result;
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

    internal void AutoFillBeacons(RecipeOrTechnology recipe, EntityCrafter entity, ref ModuleEffects effects, ref UsedModule used) {
        BeaconConfiguration beaconsToUse = GetBeaconsForCrafter(entity);
        if (!recipe.flags.HasFlags(RecipeFlags.UsesMiningProductivity) && beaconsToUse.beacon is EntityBeacon beacon && beaconsToUse.beaconModule != null) {
            effects.AddModules(beaconsToUse.beaconModule.moduleSpecification, beaconsToUse.beaconCount * beacon.beaconEfficiency * beacon.moduleSlots, entity.allowedEffects);
            used.beacon = beacon;
            used.beaconCount = beaconsToUse.beaconCount;
        }
    }

    private void AutoFillModules((float recipeTime, float fuelUsagePerSecondPerBuilding) partialParams, RecipeRow row,
        EntityCrafter entity, ref ModuleEffects effects, ref UsedModule used) {

        RecipeOrTechnology recipe = row.recipe;

        if (autoFillPayback > 0 && (fillMiners || !recipe.flags.HasFlags(RecipeFlags.UsesMiningProductivity))) {
            /*
                Auto Fill Calculation
                The goal is to find the best module to fill the building with, based on the economy (cost per second) of the configuration.
                A module can provide productivity, speed or efficiency (energy).

                Productivity stats reduces the recipe cost
                Speed stats reduces the building cost
                Efficiency stats (effectivity) reduces the energy/fuel cost

                The user can also set a payback time, which is the time it takes for the module to pay for itself.
                The payback time is calculated as the module cost divided by the economy gain per second.
                For the sake of simplicity, payback time is calculated assuming we fill the same module as many times as possible in all buildings.

                Note:
                - when the payback time is short, speed modules are mathematically preferred over efficiency modules.
                - However if the payback time is infinite, the only long term benefit of speed modules is to reduce the area of the factory which can not be modeled here.
                - But in real game play, late game power has very low marginal cost, reducing factory size is more useful than reducing power consumption. the current model cannot reflect this.
                - This calculation also does not take into account the effect of beacons.
                - This calculation also does not take into account the effect of modules on input cost.
            */


            float productivityEconomy = recipe.Cost() / partialParams.recipeTime;
            float speedEconomy = Math.Max(0.0001f, entity.Cost()) / autoFillPayback;
            float effectivityEconomy = partialParams.fuelUsagePerSecondPerBuilding * row.fuel?.Cost() ?? 0f;

            if (effectivityEconomy < 0f) {
                effectivityEconomy = 0f;
            }

            float bestEconomy = 0f;
            Module? usedModule = null;

            foreach (var module in recipe.modules) {
                if (module.IsAccessibleWithCurrentMilestones() && entity.CanAcceptModule(module.moduleSpecification)) {
                    float economy = module.moduleSpecification.productivity * productivityEconomy
                                  + module.moduleSpecification.speed * speedEconomy
                                  - module.moduleSpecification.consumption * effectivityEconomy;

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

    internal void GetModulesInfo((float recipeTime, float fuelUsagePerSecondPerBuilding) partialParams, RecipeRow row, EntityCrafter entity, ref ModuleEffects effects, ref UsedModule used) {
        AutoFillBeacons(row.recipe, entity, ref effects, ref used);
        AutoFillModules(partialParams, row, entity, ref effects, ref used);
    }

    private void AddModuleSimple(Module module, ref ModuleEffects effects, EntityCrafter entity, ref UsedModule used) {
        if (module.moduleSpecification != null) {
            int fillerLimit = effects.GetModuleSoftLimit(module.moduleSpecification, entity.moduleSlots);
            effects.AddModules(module.moduleSpecification, fillerLimit);
            used.modules = new[] { (module, fillerLimit, false) };
        }
    }
}

/// <summary>
/// An observable dictionary, that will raise <see cref="OverrideSettingChanging"/> when the settings for a crafter are about to about to change.
/// </summary>
public class OverrideCrafterBeacons : IDictionary<EntityCrafter, BeaconOverrideConfiguration> {
    private readonly SortedList<EntityCrafter, BeaconOverrideConfiguration> storage = new(DataUtils.DeterministicComparer);

    /// <summary>
    /// Raised before changing (including adding/removing) the value associated with a particular crafter.
    /// The return value, if not <see langword="null"/>, will be called after the change has been performed.
    /// </summary>
    internal event Func<EntityCrafter, Action?>? OverrideSettingChanging;

    /// <inheritdoc/>
    public BeaconOverrideConfiguration this[EntityCrafter key] {
        get => storage[key];
        set {
            Action? action = OverrideSettingChanging?.Invoke(key);
            storage[key] = value;
            action?.Invoke();
        }
    }

    /// <inheritdoc/>
    public ICollection<EntityCrafter> Keys => storage.Keys;

    /// <inheritdoc/>
    public ICollection<BeaconOverrideConfiguration> Values => storage.Values;

    /// <inheritdoc/>
    public int Count => storage.Count;

    /// <inheritdoc/>
    bool ICollection<KeyValuePair<EntityCrafter, BeaconOverrideConfiguration>>.IsReadOnly => false;

    /// <inheritdoc/>
    public void Add(EntityCrafter key, BeaconOverrideConfiguration value) {
        Action? action = OverrideSettingChanging?.Invoke(key);
        storage.Add(key, value);
        action?.Invoke();
    }

    /// <inheritdoc/>
    void ICollection<KeyValuePair<EntityCrafter, BeaconOverrideConfiguration>>.Add(KeyValuePair<EntityCrafter, BeaconOverrideConfiguration> item)
        => Add(item.Key, item.Value);

    /// <inheritdoc/>
    public void Clear() => storage.Clear();
    /// <inheritdoc/>
    bool ICollection<KeyValuePair<EntityCrafter, BeaconOverrideConfiguration>>.Contains(KeyValuePair<EntityCrafter, BeaconOverrideConfiguration> item)
        => ((ICollection<KeyValuePair<EntityCrafter, BeaconOverrideConfiguration>>)storage).Contains(item);

    /// <inheritdoc/>
    public bool ContainsKey(EntityCrafter key) => storage.ContainsKey(key);

    /// <inheritdoc/>
    void ICollection<KeyValuePair<EntityCrafter, BeaconOverrideConfiguration>>.CopyTo(KeyValuePair<EntityCrafter, BeaconOverrideConfiguration>[] array, int arrayIndex)
        => ((ICollection<KeyValuePair<EntityCrafter, BeaconOverrideConfiguration>>)storage).CopyTo(array, arrayIndex);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<EntityCrafter, BeaconOverrideConfiguration>> GetEnumerator() => storage.GetEnumerator();

    /// <inheritdoc/>
    public bool Remove(EntityCrafter key) {
        Action? action = OverrideSettingChanging?.Invoke(key);
        bool result = storage.Remove(key);
        action?.Invoke();

        return result;
    }

    /// <inheritdoc/>
    bool ICollection<KeyValuePair<EntityCrafter, BeaconOverrideConfiguration>>.Remove(KeyValuePair<EntityCrafter, BeaconOverrideConfiguration> item) {
        Action? action = OverrideSettingChanging?.Invoke(item.Key);
        bool result = ((ICollection<KeyValuePair<EntityCrafter, BeaconOverrideConfiguration>>)storage).Remove(item);
        action?.Invoke();

        return result;
    }

    /// <inheritdoc/>
    public bool TryGetValue(EntityCrafter key, [MaybeNullWhen(false)] out BeaconOverrideConfiguration value) => storage.TryGetValue(key, out value);

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => storage.GetEnumerator();
}
