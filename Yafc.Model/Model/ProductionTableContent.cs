using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Yafc.UI;

namespace Yafc.Model {
    public struct ModuleEffects {
        public float speed;
        public float productivity;
        public float consumption;
        public readonly float speedMod => MathF.Max(1f + speed, 0.2f);
        public readonly float energyUsageMod => MathF.Max(1f + consumption, 0.2f);
        public void AddModules(ModuleSpecification module, float count, AllowedEffects allowedEffects) {
            if (allowedEffects.HasFlags(AllowedEffects.Speed)) {
                speed += module.speed * count;
            }

            if (allowedEffects.HasFlags(AllowedEffects.Productivity) && module.productivity > 0f) {
                productivity += module.productivity * count;
            }

            if (allowedEffects.HasFlags(AllowedEffects.Consumption)) {
                consumption += module.consumption * count;
            }
        }

        public void AddModules(ModuleSpecification module, float count) {
            speed += module.speed * count;
            if (module.productivity > 0f) {
                productivity += module.productivity * count;
            }

            consumption += module.consumption * count;
        }

        public readonly int GetModuleSoftLimit(ModuleSpecification module, int hardLimit) {
            if (module == null) {
                return 0;
            }

            if (module.productivity > 0f || module.speed > 0f || module.pollution < 0f) {
                return hardLimit;
            }

            if (module.consumption < 0f) {
                return MathUtils.Clamp(MathUtils.Ceil(-(consumption + 0.8f) / module.consumption), 0, hardLimit);
            }

            return 0;
        }
    }

    /// <summary>
    /// One module that is (or will be) applied to a <see cref="RecipeRow"/>, and the number of times it should appear.
    /// </summary>
    /// <remarks>Immutable. To modify, modify the owning <see cref="ModuleTemplate"/>.</remarks>
    [Serializable]
    public class RecipeRowCustomModule(ModuleTemplate owner, Module module, int fixedCount = 0) : ModelObject<ModuleTemplate>(owner) {
        public Module module { get; } = module ?? throw new ArgumentNullException(nameof(module));
        public int fixedCount { get; } = fixedCount;
    }

    /// <summary>
    /// The template that determines what modules are (or will be) applied to a <see cref="RecipeRow"/>.
    /// </summary>
    /// <remarks>Immutable. To modify, call <see cref="GetBuilder"/>, modify the builder, and call <see cref="ModuleTemplateBuilder.Build"/>.</remarks>
    [Serializable, DeserializeWithNonPublicConstructor]
    public class ModuleTemplate : ModelObject<ModelObject> {
        /// <summary>
        /// The beacon to use, if any, for the associated <see cref="RecipeRow"/>.
        /// </summary>
        public EntityBeacon? beacon { get; }
        /// <summary>
        /// The modules, if any, to directly insert into the crafting entity.
        /// </summary>
        public ReadOnlyCollection<RecipeRowCustomModule> list { get; private set; } = new([]); // Must be a distinct collection object to accomodate the deserializer.
        /// <summary>
        /// The modules, if any, to insert into beacons that affect the crafting entity.
        /// </summary>
        public ReadOnlyCollection<RecipeRowCustomModule> beaconList { get; private set; } = new([]); // Must be a distinct collection object to accomodate the deserializer.

        private ModuleTemplate(ModelObject owner, EntityBeacon? beacon) : base(owner) => this.beacon = beacon;

        public bool IsCompatibleWith([NotNullWhen(true)] RecipeRow? row) {
            if (row?.entity == null) {
                return false;
            }

            bool hasFloodfillModules = false;
            bool hasCompatibleFloodfill = false;
            int totalModules = 0;
            foreach (var module in list) {
                bool isCompatibleWithModule = row.recipe.CanAcceptModule(module.module) && row.entity.CanAcceptModule(module.module.moduleSpecification);
                if (module.fixedCount == 0) {
                    hasFloodfillModules = true;
                    hasCompatibleFloodfill |= isCompatibleWithModule;
                }
                else {
                    if (!isCompatibleWithModule) {
                        return false;
                    }

                    totalModules += module.fixedCount;
                }
            }

            return (!hasFloodfillModules || hasCompatibleFloodfill) && row.entity.moduleSlots >= totalModules;
        }

        public void GetModulesInfo(RecipeParameters recipeParams, RecipeOrTechnology recipe, EntityCrafter entity, Goods? fuel, ref ModuleEffects effects, ref RecipeParameters.UsedModule used, ModuleFillerParameters? filler) {
            List<(Module module, int count, bool beacon)> buffer = [];
            int beaconedModules = 0;
            Item? nonBeacon = null;
            used.modules = null;
            int remaining = entity.moduleSlots;
            foreach (var module in list) {
                if (!entity.CanAcceptModule(module.module.moduleSpecification) || !recipe.CanAcceptModule(module.module)) {
                    continue;
                }

                if (remaining <= 0) {
                    break;
                }

                int count = Math.Min(module.fixedCount == 0 ? int.MaxValue : module.fixedCount, remaining);
                remaining -= count;
                nonBeacon ??= module.module;
                buffer.Add((module.module, count, false));
                effects.AddModules(module.module.moduleSpecification, count);
            }

            if (beacon != null) {
                foreach (var module in beaconList) {
                    beaconedModules += module.fixedCount;
                    buffer.Add((module.module, module.fixedCount, true));
                    effects.AddModules(module.module.moduleSpecification, beacon.beaconEfficiency * module.fixedCount);
                }

                if (beaconedModules > 0) {
                    used.beacon = beacon;
                    used.beaconCount = ((beaconedModules - 1) / beacon.moduleSlots) + 1;
                }
            }
            else {
                filler?.AutoFillBeacons(recipeParams, recipe, entity, fuel, ref effects, ref used);
            }

            used.modules = [.. buffer];
        }

        public int CalcBeaconCount() {
            if (beacon is null) {
                throw new InvalidOperationException($"Must not call {nameof(CalcBeaconCount)} when {nameof(beacon)} is null.");
            }
            int moduleCount = 0;
            foreach (var element in beaconList) {
                moduleCount += element.fixedCount;
            }

            return ((moduleCount - 1) / beacon.moduleSlots) + 1;
        }

        /// <summary>
        /// Get a <see cref="ModuleTemplateBuilder"/> initialized to rebuild the contents of this <see cref="ModuleTemplate"/>.
        /// </summary>
        public ModuleTemplateBuilder GetBuilder() => new() {
            beacon = beacon,
            list = [.. list.Select(m => (m.module, m.fixedCount))],
            beaconList = [.. beaconList.Select(m => (m.module, m.fixedCount))]
        };

        internal static ModuleTemplate Build(ModelObject owner, ModuleTemplateBuilder builder) {
#pragma warning disable IDE0017 // False positive: convertList cannot be called before the assignment completes
            ModuleTemplate modules = new(owner, builder.beacon);
#pragma warning restore IDE0017
            modules.list = convertList(builder.list);
            modules.beaconList = convertList(builder.beaconList);
            return modules;

            ReadOnlyCollection<RecipeRowCustomModule> convertList(List<(Module module, int fixedCount)> list)
                => list.Select(m => new RecipeRowCustomModule(modules, m.module, m.fixedCount)).ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// An object that can be used to configure and build a <see cref="ModuleTemplate"/>.
    /// </summary>
    public class ModuleTemplateBuilder {
        /// <summary>
        /// The beacon to be stored in <see cref="ModuleTemplate.beacon"/> after building.
        /// </summary>
        public EntityBeacon? beacon { get; set; }
        /// <summary>
        /// The list of <see cref="Module"/>s and counts to be stored in <see cref="ModuleTemplate.list"/> after building.
        /// </summary>
        public List<(Module module, int fixedCount)> list { get; set; } = [];
        /// <summary>
        /// The list of <see cref="Module"/>s and counts to be stored in <see cref="ModuleTemplate.beaconList"/> after building.
        /// </summary>
        public List<(Module module, int fixedCount)> beaconList { get; set; } = [];

        /// <summary>
        /// Builds a <see cref="ModuleTemplate"/> from this <see cref="ModuleTemplateBuilder"/>.
        /// </summary>
        /// <param name="owner">The <see cref="RecipeRow"/> or <see cref="ProjectModuleTemplate"/> that will own the built <see cref="ModuleTemplate"/>.</param>
        public ModuleTemplate Build(ModelObject owner) => ModuleTemplate.Build(owner, this);
    }

    // Stores collection on ProductionLink recipe was linked to the previous computation
    public struct RecipeLinks {
        public Goods[] ingredientGoods;
        public ProductionLink?[] ingredients;
        public ProductionLink?[] products;
        public ProductionLink? fuel;
        public ProductionLink? spentFuel;
    }

    public interface IElementGroup<TElement> {
        List<TElement> elements { get; }
        bool expanded { get; set; }
    }

    public interface IGroupedElement<TGroup> {
        void SetOwner(TGroup newOwner);
        TGroup? subgroup { get; }
        bool visible { get; }
    }

    public class RecipeRow : ModelObject<ProductionTable>, IModuleFiller, IGroupedElement<ProductionTable> {
        private EntityCrafter? _entity;
        private Goods? _fuel;
        private float _fixedBuildings;
        private ModuleTemplate? _modules;

        public RecipeOrTechnology recipe { get; }
        // Variable parameters
        public EntityCrafter? entity {
            get => _entity;
            set {
                if (SerializationMap.IsDeserializing || fixedBuildings == 0) {
                    _entity = value;
                }
                else if (fixedFuel && !(value?.energy.fuels ?? []).Contains(_fuel)) {
                    // We're changing both the entity and the fuel (changing between electric, fluid-burning, item-burning, heat-powered, and steam-powered crafter categories)
                    // Don't try to preserve fuel consumption in this case.
                    fixedBuildings = 0;
                    _entity = value;
                }
                else {
                    if (fixedProduct is Item && !(value?.energy.fuels ?? []).Contains(_fuel)) {
                        // We're changing both the entity and the fuel (changing between electric, fluid-burning, item-burning, heat-powered, and steam-powered crafter categories)
                        // Preserve fixed production of an item that is possibly also a spent fuel by changing the fuel to void first.
                        fuel = Database.voidEnergy;
                    }
                    RecipeParameters oldParameters = new();
                    oldParameters.CalculateParameters(recipe, entity, fuel, variants, this);
                    _entity = value;
                    RecalculateFixedAmount(oldParameters);
                }
            }
        }
        public Goods? fuel {
            get => _fuel;
            set {
                if (SerializationMap.IsDeserializing || fixedBuildings == 0) {
                    _fuel = value;
                }
                else if (fixedProduct != null && ((fuel as Item)?.fuelResult == fixedProduct || (value as Item)?.fuelResult == fixedProduct)) {
                    if (recipe.products.SingleOrDefault(p => p.goods == fixedProduct, false) is not Product product) {
                        fixedBuildings = 0; // We couldn't find the Product corresponding to fixedProduct. Just clear the fixed amount.
                        _fuel = value;
                    }
                    else {
                        // We're changing the fuel and at least one of the current or new fuel burns to the fixed product
                        double oldAmount = recipesPerSecond * product.GetAmount(parameters.productivity);
                        if ((fuel as Item)?.fuelResult == fixedProduct) {
                            oldAmount += parameters.fuelUsagePerSecondPerRecipe * recipesPerSecond;
                        }
                        _fuel = value;
                        parameters.CalculateParameters(recipe, entity, fuel, variants, this);
                        double newAmount = recipesPerSecond * product.GetAmount(parameters.productivity);
                        if ((fuel as Item)?.fuelResult == fixedProduct) {
                            newAmount += parameters.fuelUsagePerSecondPerRecipe * recipesPerSecond;
                        }
                        fixedBuildings *= (float)(oldAmount / newAmount);
                    }
                }
                else if (fixedFuel) {
                    if (value == null || _fuel == null || _fuel.fuelValue == 0) {
                        fixedBuildings = 0;
                    }
                    else {
                        fixedBuildings *= value.fuelValue / _fuel.fuelValue;
                    }
                    _fuel = value;
                }
                else {
                    _fuel = value;
                }
            }
        }
        public RecipeLinks links { get; internal set; }
        /// <summary>
        /// If not zero, the fixed building count entered by the user, or the number of buildings required to generate the specified fixed consumption/production.
        /// Read <see cref="fixedFuel"/>, <see cref="fixedIngredient"/>, and <see cref="fixedProduct"/> to determine which value was fixed in the UI.
        /// This property is set/modified so the solver gets the correct answer without testing the values of those properties.
        /// </summary>
        public float fixedBuildings {
            get => _fixedBuildings;
            set {
                _fixedBuildings = value;
                if (value == 0) {
                    fixedFuel = false;
                    fixedIngredient = null;
                    fixedProduct = null;
                }
            }
        }
        /// <summary>
        /// If <see langword="true"/>, <see cref="fixedBuildings"/> is set to control the fuel consumption.
        /// </summary>
        public bool fixedFuel { get; set; }
        /// <summary>
        /// If not <see langword="null"/>, <see cref="fixedBuildings"/> is set to control the consumption of this ingredient.
        /// </summary>
        public Goods? fixedIngredient { get; set; }
        /// <summary>
        /// If not <see langword="null"/>, <see cref="fixedBuildings"/> is set to control the production of this product.
        /// </summary>
        public Goods? fixedProduct { get; set; }
        public int? builtBuildings { get; set; }
        /// <summary>
        /// If <see langword="true"/>, the enabled checkbox for this recipe is checked.
        /// </summary>
        public bool enabled { get; set; } = true;
        /// <summary>
        /// If <see langword="true"/>, the enabled checkboxes for this recipe and all its parent recipes are checked.
        /// If <see langword="false"/>, at least one enabled checkbox for this recipe or its ancestors is unchecked.
        /// </summary>
        public bool hierarchyEnabled { get; internal set; }
        public int tag { get; set; }

        public RowHighlighting highlighting =>
            tag switch {
                1 => RowHighlighting.Green,
                2 => RowHighlighting.Yellow,
                3 => RowHighlighting.Red,
                4 => RowHighlighting.Blue,
                _ => RowHighlighting.None
            };

        [Obsolete("Deprecated", true)]
        public Module module {
            set {
                if (value != null) {
                    modules = new ModuleTemplateBuilder { list = { (value, 0) } }.Build(this);
                }
            }
        }

        public ModuleTemplate? modules {
            get => _modules;
            set {
                if (SerializationMap.IsDeserializing || fixedBuildings == 0) {
                    _modules = value;
                }
                else {
                    RecipeParameters oldParameters = new();
                    oldParameters.CalculateParameters(recipe, entity, fuel, variants, this);
                    _modules = value;
                    RecalculateFixedAmount(oldParameters);
                }
            }
        }

        private void RecalculateFixedAmount(RecipeParameters oldParameters) {
            this.RecordUndo(); // Unnecessary when called by set_modules or set_entity. Required when called by ModuleFillerParametersChanging.
            parameters.CalculateParameters(recipe, entity, fuel, variants, this);
            if (fixedFuel) {
                fixedBuildings *= oldParameters.fuelUsagePerSecondPerBuilding / parameters.fuelUsagePerSecondPerBuilding;
            }
            else if (fixedIngredient != null) {
                fixedBuildings *= parameters.recipeTime / oldParameters.recipeTime;
            }
            else if (fixedProduct != null) {
                if (recipe.products.SingleOrDefault(p => p.goods == fixedProduct, false) is not Product product) {
                    fixedBuildings = 0; // We couldn't find the Product corresponding to fixedProduct. Just clear the fixed amount.
                }
                else {
                    float oldAmount = product.GetAmount(oldParameters.productivity) / oldParameters.recipeTime;
                    float newAmount = product.GetAmount(parameters.productivity) / parameters.recipeTime;
                    fixedBuildings *= oldAmount / newAmount;
                }
            }
        }

        public ProductionTable? subgroup { get; set; }
        public HashSet<FactorioObject> variants { get; } = [];
        [SkipSerialization] public ProductionTable linkRoot => subgroup ?? owner;

        // Computed variables
        public RecipeParameters parameters { get; } = new RecipeParameters();
        public double recipesPerSecond { get; internal set; }
        public bool FindLink(Goods goods, [MaybeNullWhen(false)] out ProductionLink link) {
            return linkRoot.FindLink(goods, out link);
        }

        public T GetVariant<T>(T[] options) where T : FactorioObject {
            foreach (var option in options) {
                if (variants.Contains(option)) {
                    return option;
                }
            }

            return options[0];
        }

        public void ChangeVariant<T>(T was, T now) where T : FactorioObject {
            _ = variants.Remove(was);
            _ = variants.Add(now);
        }

        [MemberNotNullWhen(true, nameof(subgroup))]
        public bool isOverviewMode => subgroup != null && !subgroup.expanded;
        public float buildingCount => (float)recipesPerSecond * parameters.recipeTime;
        public bool visible { get; internal set; } = true;

        public RecipeRow(ProductionTable owner, RecipeOrTechnology recipe) : base(owner) {
            this.recipe = recipe ?? throw new ArgumentNullException(nameof(recipe), "Recipe does not exist");
            links = new RecipeLinks {
                ingredients = new ProductionLink[recipe.ingredients.Length],
                ingredientGoods = new Goods[recipe.ingredients.Length],
                products = new ProductionLink[recipe.products.Length]
            };
        }

        protected internal override void ThisChanged(bool visualOnly) {
            owner.ThisChanged(visualOnly);
        }

        public void SetOwner(ProductionTable parent) {
            owner = parent;
        }

        public void RemoveFixedModules() {
            if (modules == null) {
                return;
            }

            CreateUndoSnapshot();
            modules = null;
        }
        public void SetFixedModule(Module? module) {
            if (module == null) {
                RemoveFixedModules();
                return;
            }

            ModuleTemplateBuilder builder = modules?.GetBuilder() ?? new();
            builder.list = [(module, 0)];
            this.RecordUndo().modules = builder.Build(this);
        }

        public ModuleFillerParameters? GetModuleFiller() {
            var table = linkRoot;
            while (table != null) {
                if (table.modules != null) {
                    return table.modules;
                }

                table = (table.owner as RecipeRow)?.owner;
            }

            return null;
        }

        public void GetModulesInfo(RecipeParameters recipeParams, RecipeOrTechnology recipe, EntityCrafter entity, Goods? fuel, ref ModuleEffects effects, ref RecipeParameters.UsedModule used) {
            ModuleFillerParameters? filler = null;
            var useModules = modules;
            if (useModules == null || useModules.beacon == null) {
                filler = GetModuleFiller();
            }

            if (useModules == null) {
                filler?.GetModulesInfo(recipeParams, recipe, entity, fuel, ref effects, ref used);
            }
            else {
                useModules.GetModulesInfo(recipeParams, recipe, entity, fuel, ref effects, ref used, filler);
            }
        }

        /// <summary>
        /// Call to inform this <see cref="RecipeRow"/> that the applicable <see cref="ModuleFillerParameters"/> are about to change.
        /// </summary>
        /// <returns>If not <see langword="null"/>, an <see cref="Action"/> to perform after the change has completed that will update <see cref="fixedBuildings"/> to account for the new modules.</returns>
        internal Action? ModuleFillerParametersChanging() {
            if (fixedFuel || fixedIngredient != null || fixedProduct != null) {
                RecipeParameters oldParameters = new();
                oldParameters.CalculateParameters(recipe, entity, fuel, variants, this);
                return () => RecalculateFixedAmount(oldParameters);
            }
            return null;
        }
    }

    public enum RowHighlighting {
        None,
        Green,
        Yellow,
        Red,
        Blue
    }

    public enum LinkAlgorithm {
        Match,
        AllowOverProduction,
        AllowOverConsumption,
    }

    /// <summary>
    /// A Link is goods whose production and consumption is attempted to be balanced by YAFC across the sheet.
    /// </summary>
    public class ProductionLink(ProductionTable group, Goods goods) : ModelObject<ProductionTable>(group) {
        [Flags]
        public enum Flags {
            LinkNotMatched = 1 << 0,
            /// <summary>
            /// Indicates if there is a feedback loop that could not get balanced. 
            /// It doesn't mean that this link is the problem, but it's a part of the loop.
            /// </summary>
            LinkRecursiveNotMatched = 1 << 1,
            HasConsumption = 1 << 2,
            HasProduction = 1 << 3,
            /// <summary>
            /// The production and consumption of the child link are not matched.
            /// </summary>
            ChildNotMatched = 1 << 4,
            HasProductionAndConsumption = HasProduction | HasConsumption,
        }

        public Goods goods { get; } = goods ?? throw new ArgumentNullException(nameof(goods), "Linked product does not exist");
        public float amount { get; set; }
        public LinkAlgorithm algorithm { get; set; }

        // computed variables
        public Flags flags { get; internal set; }
        /// <summary>
        /// Probably the total production of the goods in the link. TODO: Needs to be investigated if it is indeed so.
        /// </summary>
        public float linkFlow { get; internal set; }
        public float notMatchedFlow { get; internal set; }
        /// <summary>
        /// List of recipes belonging to this production link
        /// </summary>
        [SkipSerialization] public HashSet<RecipeRow> capturedRecipes { get; } = [];
        internal int solverIndex;
        public float dualValue { get; internal set; }
    }
}
