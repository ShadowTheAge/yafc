using System;
using System.Collections.Generic;
using YAFC.UI;

namespace YAFC.Model {
    public struct ModuleEffects {
        public float speed;
        public float productivity;
        public float consumption;
        public float speedMod => MathF.Max(1f + speed, 0.2f);
        public float energyUsageMod => MathF.Max(1f + consumption, 0.2f);
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

        public int GetModuleSoftLimit(ModuleSpecification module, int hardLimit) {
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

    [Serializable]
    public class RecipeRowCustomModule : ModelObject<ModuleTemplate> {
        private Item _module;
        public Item module {
            get => _module;
            set => _module = value ?? throw new ArgumentNullException(nameof(value));
        }
        public int fixedCount { get; set; }

        public RecipeRowCustomModule(ModuleTemplate owner, Item module) : base(owner) {
            this.module = module;
        }
    }

    [Serializable]
    public class ModuleTemplate : ModelObject<ModelObject> {
        public EntityBeacon beacon { get; set; }
        public List<RecipeRowCustomModule> list { get; } = new List<RecipeRowCustomModule>();
        public List<RecipeRowCustomModule> beaconList { get; } = new List<RecipeRowCustomModule>();
        public ModuleTemplate(ModelObject owner) : base(owner) { }

        public bool IsCompatibleWith(RecipeRow row) {
            if (row.entity == null) {
                return false;
            }

            bool hasFloodfillModules = false;
            bool hasCompatibleFloodfill = false;
            int totalModules = 0;
            foreach (var module in list) {
                bool isCompatibleWithModule = row.recipe.CanAcceptModule(module.module) && row.entity.CanAcceptModule(module.module.module);
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


        private static readonly List<(Item module, int count, bool beacon)> buffer = new List<(Item module, int count, bool beacon)>();
        public void GetModulesInfo(RecipeParameters recipeParams, Recipe recipe, EntityCrafter entity, Goods fuel, ref ModuleEffects effects, ref RecipeParameters.UsedModule used, ModuleFillerParameters filler) {
            int beaconedModules = 0;
            Item nonBeacon = null;
            buffer.Clear();
            used.modules = null;
            int remaining = entity.moduleSlots;
            foreach (var module in list) {
                if (!entity.CanAcceptModule(module.module.module) || !recipe.CanAcceptModule(module.module)) {
                    continue;
                }

                if (remaining <= 0) {
                    break;
                }

                int count = Math.Min(module.fixedCount == 0 ? int.MaxValue : module.fixedCount, remaining);
                remaining -= count;
                nonBeacon ??= module.module;
                buffer.Add((module.module, count, false));
                effects.AddModules(module.module.module, count);
            }

            if (beacon != null) {
                foreach (var module in beaconList) {
                    beaconedModules += module.fixedCount;
                    buffer.Add((module.module, module.fixedCount, true));
                    effects.AddModules(module.module.module, beacon.beaconEfficiency * module.fixedCount);
                }

                if (beaconedModules > 0) {
                    used.beacon = beacon;
                    used.beaconCount = ((beaconedModules - 1) / beacon.moduleSlots) + 1;
                }
            }
            else {
                filler?.AutoFillBeacons(recipeParams, recipe, entity, fuel, ref effects, ref used);
            }

            used.modules = buffer.ToArray();
        }

        public int CalcBeaconCount() {
            int moduleCount = 0;
            foreach (var element in beaconList) {
                moduleCount += element.fixedCount;
            }

            return ((moduleCount - 1) / beacon.moduleSlots) + 1;
        }
    }

    // Stores collection on ProductionLink recipe was linked to the previous computation
    public struct RecipeLinks {
        public Goods[] ingredientGoods;
        public ProductionLink[] ingredients;
        public ProductionLink[] products;
        public ProductionLink fuel;
        public ProductionLink spentFuel;
    }

    public interface IElementGroup<TElement> {
        List<TElement> elements { get; }
        bool expanded { get; set; }
    }

    public interface IGroupedElement<TGroup> {
        void SetOwner(TGroup newOwner);
        TGroup subgroup { get; }
        bool visible { get; }
    }

    public class RecipeRow : ModelObject<ProductionTable>, IModuleFiller, IGroupedElement<ProductionTable> {
        public Recipe recipe { get; }
        // Variable parameters
        public EntityCrafter entity { get; set; }
        public Goods fuel { get; set; }
        public RecipeLinks links { get; internal set; }
        public float fixedBuildings { get; set; }
        public int? builtBuildings { get; set; }
        public bool enabled { get; set; } = true;
        public bool hierarchyEnabled { get; internal set; }
        public int tag { get; set; }

        [Obsolete("Deprecated", true)]
        public Item module {
            set {
                if (value != null) {
                    modules = new ModuleTemplate(this);
                    modules.list.Add(new RecipeRowCustomModule(modules, value));
                }
            }
        }

        private ModuleTemplate _modules;

        public ModuleTemplate modules {
            get => _modules;
            set {
                if (value != null) {
                    _modules = value;
                }
                else {
                    _modules = null;
                }
            }
        }

        public ProductionTable subgroup { get; set; }
        public HashSet<FactorioObject> variants { get; } = new HashSet<FactorioObject>();
        [SkipSerialization] public ProductionTable linkRoot => subgroup ?? owner;

        // Computed variables
        public RecipeParameters parameters { get; } = new RecipeParameters();
        public double recipesPerSecond { get; internal set; }
        public bool FindLink(Goods goods, out ProductionLink link) {
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
        public bool isOverviewMode => subgroup != null && !subgroup.expanded;
        public float buildingCount => (float)recipesPerSecond * parameters.recipeTime;
        public bool visible { get; internal set; } = true;

        public RecipeRow(ProductionTable owner, Recipe recipe) : base(owner) {
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
        public void SetFixedModule(Item module) {
            if (module == null) {
                RemoveFixedModules();
                return;
            }

            if (modules == null) {
                this.RecordUndo().modules = new ModuleTemplate(this);
            }

            var list = modules.RecordUndo().list;
            list.Clear();
            list.Add(new RecipeRowCustomModule(modules, module));
        }

        public ModuleFillerParameters GetModuleFiller() {
            var table = linkRoot;
            while (table != null) {
                if (table.modules != null) {
                    return table.modules;
                }

                table = (table.owner as RecipeRow)?.owner;
            }

            return null;
        }

        public void GetModulesInfo(RecipeParameters recipeParams, Recipe recipe, EntityCrafter entity, Goods fuel, ref ModuleEffects effects, ref RecipeParameters.UsedModule used) {
            ModuleFillerParameters filler = null;
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
    }

    public enum LinkAlgorithm {
        Match,
        AllowOverProduction,
        AllowOverConsumption,
    }

    /// <summary>
    /// Link is goods whose production and consumption is attempted to be balanced by YAFC across the sheet.
    /// </summary>
    public class ProductionLink : ModelObject<ProductionTable> {
        [Flags]
        public enum Flags {
            LinkNotMatched = 1 << 0,
            LinkRecursiveNotMatched = 1 << 1,
            HasConsumption = 1 << 2,
            HasProduction = 1 << 3,
            /// <summary>
            /// The production and consumption of the child link are not matched.
            /// </summary>
            ChildNotMatched = 1 << 4,
            HasProductionAndConsumption = HasProduction | HasConsumption,
        }

        public Goods goods { get; }
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
        [SkipSerialization] public List<RecipeRow> capturedRecipes { get; } = new List<RecipeRow>();
        internal int solverIndex;
        public float dualValue { get; internal set; }

        public ProductionLink(ProductionTable group, Goods goods) : base(group) {
            this.goods = goods ?? throw new ArgumentNullException(nameof(goods), "Linked product does not exist");
        }
    }
}
