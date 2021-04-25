using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Linq;
using YAFC.UI;
[assembly:InternalsVisibleTo("YAFCparser")]

namespace YAFC.Model
{
    public interface IFactorioObjectWrapper
    {
        string text { get; }
        FactorioObject target { get; }
        float amount { get; }
    }

    internal enum FactorioObjectSortOrder
    {
        SpecialGoods,
        Items,
        Fluids,
        Recipes,
        Mechanics,
        Technologies,
        Entities
    }
    
    public enum FactorioId {}
    
    public abstract class FactorioObject : IFactorioObjectWrapper, IComparable<FactorioObject>
    {
        public string factorioType { get; internal set; }
        public string name { get; internal set; }
        public string originalName { get; internal set; } // name without temperature
        public string typeDotName { get; internal set; }
        public string locName { get; internal set; }
        public string locDescr { get; internal set; }
        public FactorioIconPart[] iconSpec { get; internal set; }
        public Icon icon { get; internal set; }
        public FactorioId id { get; internal set; }
        internal abstract FactorioObjectSortOrder sortingOrder { get; }
        public FactorioObjectSpecialType specialType { get; internal set; }
        public abstract string type { get; }
        FactorioObject IFactorioObjectWrapper.target => this;
        float IFactorioObjectWrapper.amount => 1f;

        string IFactorioObjectWrapper.text => locName;

        public void FallbackLocalization(FactorioObject other, string description)
        {
            if (locName == null)
            {
                if (other == null)
                    locName = name;
                else
                {
                    locName = other.locName;
                    locDescr = description + " " + locName;
                }
            }
        }

        public abstract void GetDependencies(IDependencyCollector collector, List<FactorioObject> temp);

        public override string ToString() => name;

        public int CompareTo(FactorioObject other) => DataUtils.DefaultOrdering.Compare(this, other);
    }
    
    public class FactorioIconPart
    {
        public string path;
        public float size = 32;
        public float x, y, r = 1, g = 1, b = 1, a = 1;
        public float scale = 1;

        public bool IsSimple()
        {
            return x == 0 && y == 0 && r == 1 && g == 1 && b == 1 && a == 1 && scale == 1;
        }
    }

    [Flags]
    public enum RecipeFlags
    {
        UsesMiningProductivity = 1 << 0,
        UsesFluidTemperature = 1 << 2,
        ScaleProductionWithPower = 1 << 3,
        LimitedByTickRate = 1 << 4,
    }
    
    public abstract class RecipeOrTechnology : FactorioObject
    {
        public PackedList<Entity> crafters { get; internal set; }
        public Ingredient[] ingredients { get; internal set; }
        public Product[] products { get; internal set; }
        public Item[] modules { get; internal set; } = Array.Empty<Item>();
        public Entity sourceEntity { get; internal set; }
        public Goods mainProduct { get; internal set; }
        public float time { get; internal set; }
        public bool enabled { get; internal set; }
        public bool hidden { get; internal set; }
        public RecipeFlags flags { get; internal set; }
        public override string type => "Recipe";

        internal override FactorioObjectSortOrder sortingOrder => FactorioObjectSortOrder.Recipes;

        public override void GetDependencies(IDependencyCollector collector, List<FactorioObject> temp)
        {
            if (ingredients.Length > 0)
            {
                temp.Clear();
                foreach (var ingredient in ingredients)
                {
                    if (ingredient.variants != null)
                        collector.Add(new PackedList<Goods>(ingredient.variants), DependencyList.Flags.IngredientVariant);
                    else temp.Add(ingredient.goods);
                }
                if (temp.Count > 0)
                    collector.Add(temp, DependencyList.Flags.Ingredient);
            }
            collector.Add(crafters, DependencyList.Flags.CraftingEntity);
            if (sourceEntity != null)
                collector.Add(new[] {sourceEntity.id}, DependencyList.Flags.SourceEntity);
        }

        public bool CanFit(int itemInputs, int fluidInputs, Goods[] slots)
        {
            foreach (var ingredient in ingredients)
            {
                if (ingredient.goods is Item && --itemInputs < 0) return false;
                if (ingredient.goods is Fluid && --fluidInputs < 0) return false;
                if (slots != null && !slots.Contains(ingredient.goods)) return false;
            }
            return true;
        }
    }

    public enum FactorioObjectSpecialType
    {
        Normal,
        FilledBarrel,
        Barreling,
        Voiding,
        Unbarreling
    }
    
    public class Recipe : RecipeOrTechnology
    {
        public PackedList<Technology> technologyUnlock { get; internal set; }
        public bool HasIngredientVariants()
        {
            foreach (var ingr in ingredients)
            {
                if (ingr.variants != null)
                    return true;
            }

            return false;
        }

        public override void GetDependencies(IDependencyCollector collector, List<FactorioObject> temp)
        {
            base.GetDependencies(collector, temp);
            if (!enabled)
                collector.Add(technologyUnlock, DependencyList.Flags.TechnologyUnlock);
        }

        public bool IsProductivityAllowed()
        {
            foreach (var module in modules)
            {
                if (module.module.productivity != 0f)
                    return true;
            }

            return false;
        }
    }

    public class Mechanics : Recipe
    {
        internal override FactorioObjectSortOrder sortingOrder => FactorioObjectSortOrder.Mechanics;
        public override string type => "Mechanics";
    }
    
    public class Ingredient : IFactorioObjectWrapper
    {
        public readonly float amount;
        public Goods goods { get; internal set; }
        public Goods[] variants { get; internal set; }
        public TemperatureRange temperature { get; internal set; } = TemperatureRange.Any;
        public Ingredient(Goods goods, float amount)
        {
            this.goods = goods;
            this.amount = amount;
            if (goods is Fluid fluid)
                temperature = fluid.temperatureRange;
        }

        string IFactorioObjectWrapper.text
        {
            get
            {
                var text = goods.locName;
                if (amount != 1f)
                    text = amount + "x " + text;
                if (!temperature.IsAny())
                    text += " (" + temperature + ")";
                return text;
            }
        }

        FactorioObject IFactorioObjectWrapper.target => goods;

        float IFactorioObjectWrapper.amount => amount;

        public bool ContainsVariant(Goods product)
        {
            if (goods == product)
                return true;
            if (variants != null)
                return Array.IndexOf(variants, product) >= 0;
            return false;
        }
    }
    
    public class Product : IFactorioObjectWrapper
    {
        public readonly Goods goods;
        public readonly float amountMin;
        public readonly float amountMax;
        public readonly float probability;
        public readonly float amount; // This is average amount including probability and range
        public float productivityAmount { get; private set; }

        public void SetCatalyst(float catalyst)
        {
            var catalyticMin = amountMin - catalyst;
            var catalyticMax = amountMax - catalyst;
            if (catalyticMax <= 0)
                productivityAmount = 0f;
            else if (catalyticMin >= 0f)
                productivityAmount = (catalyticMin + catalyticMax) * 0.5f * probability;
            else
            {
                // TODO super duper rare case, might not be precise
                productivityAmount = probability * catalyticMax * catalyticMax * 0.5f / (catalyticMax - catalyticMin);
            }
        }

        public float GetAmount(float productivityBonus) => amount + productivityBonus * productivityAmount;

        public Product(Goods goods, float amount)
        {
            this.goods = goods;
            amountMin = amountMax = this.amount = productivityAmount = amount;
            probability = 1f;
        }
        
        public Product(Goods goods, float min, float max, float probability)
        {
            this.goods = goods;
            amountMin = min;
            amountMax = max;
            this.probability = probability;
            amount = productivityAmount = probability * (min + max) / 2;
        }

        public bool IsSimple => amountMin == amountMax && probability == 1f;

        FactorioObject IFactorioObjectWrapper.target => goods;

        string IFactorioObjectWrapper.text
        {
            get
            {
                var text = goods.locName;
                if (amountMin != 1f || amountMax != 1f)
                {
                    text = DataUtils.FormatAmount(amountMax, UnitOfMeasure.None) + "x " + text;
                    if (amountMin != amountMax)
                        text = DataUtils.FormatAmount(amountMin, UnitOfMeasure.None) + "-" + text;
                }
                if (probability != 1f)
                    text = DataUtils.FormatAmount(probability, UnitOfMeasure.Percent) + " " + text;
                return text;
            }
        }
        float IFactorioObjectWrapper.amount => amount;
    }

    // Abstract base for anything that can be produced or consumed by recipes (etc)
    public abstract class Goods : FactorioObject
    {
        public float fuelValue { get; internal set; }
        public abstract bool isPower { get; }
        public virtual Fluid fluid => null;
        public Recipe[] production { get; internal set; }
        public Recipe[] usages { get; internal set; }
        public FactorioObject[] miscSources { get; internal set; }
        public Entity[] fuelFor { get; internal set; }
        public abstract UnitOfMeasure flowUnitOfMeasure { get; }

        public override void GetDependencies(IDependencyCollector collector, List<FactorioObject> temp)
        {
            collector.Add(new PackedList<FactorioObject>(production.Concat(miscSources)), DependencyList.Flags.Source);
        }

        public virtual bool HasSpentFuel(out Item spent)
        {
            spent = null;
            return false;
        }
    }
    
    public class Item : Goods
    {
        public Item fuelResult { get; internal set; }
        public int stackSize { get; internal set; }
        public Entity placeResult { get; internal set; }
        public ModuleSpecification module { get; internal set; }
        public override bool isPower => false;
        public override string type => "Item";
        internal override FactorioObjectSortOrder sortingOrder => FactorioObjectSortOrder.Items;
        public override UnitOfMeasure flowUnitOfMeasure => UnitOfMeasure.ItemPerSecond;

        public override bool HasSpentFuel(out Item spent)
        {
            spent = fuelResult;
            return spent != null;
        }
    }
    
    public class Fluid : Goods
    {
        public override Fluid fluid => this;
        public override string type => "Fluid";
        public float heatCapacity { get; internal set; } = 1e-3f;
        public TemperatureRange temperatureRange { get; internal set; }
        public int temperature { get; internal set; }
        public float heatValue { get; internal set; }
        public List<Fluid> variants { get; internal set; }
        public override bool isPower => false;
        public override UnitOfMeasure flowUnitOfMeasure => UnitOfMeasure.FluidPerSecond;
        internal override FactorioObjectSortOrder sortingOrder => FactorioObjectSortOrder.Fluids;
        internal Fluid Clone() => MemberwiseClone() as Fluid;

        internal void SetTemperature(int temp)
        {
            temperature = temp;
            heatValue = (temp - temperatureRange.min) * heatCapacity;
        }
    }
    
    public class Special : Goods
    {
        public string virtualSignal { get; internal set; }
        internal bool power;
        public override bool isPower => power;
        public override string type => isPower ? "Power" : "Special";
        public override UnitOfMeasure flowUnitOfMeasure => isPower ? UnitOfMeasure.Megawatt : UnitOfMeasure.PerSecond;
        internal override FactorioObjectSortOrder sortingOrder => FactorioObjectSortOrder.SpecialGoods;
    }

    [Flags]
    public enum AllowedEffects
    {
        Speed = 1 << 0,
        Productivity = 1 << 1,
        Consumption = 1 << 2,
        Pollution = 1 << 3,
        
        All = Speed | Productivity | Consumption | Pollution,
        None = 0
    }
    
    public class Entity : FactorioObject
    {
        public Product[] loot { get; internal set; } = Array.Empty<Product>();
        public PackedList<RecipeOrTechnology> recipes { get; internal set; }
        public bool mapGenerated { get; internal set; }
        public float mapGenDensity { get; internal set; }
        public float power { get; internal set; }
        public EntityEnergy energy { get; internal set; }
        public float craftingSpeed { get; internal set; } = 1f;
        public float productivity { get; internal set; }
        public PackedList<Item> itemsToPlace { get; internal set; }
        public int itemInputs { get; internal set; }
        public int fluidInputs { get; internal set; } // fluid inputs for recipe, not including power
        public Goods[] inputs { get; internal set; }
        public AllowedEffects allowedEffects { get; internal set; } = AllowedEffects.All;
        public int moduleSlots { get; internal set; }
        public int size { get; internal set; } 
        internal override FactorioObjectSortOrder sortingOrder => FactorioObjectSortOrder.Entities;
        public override string type => "Entity";

        public override void GetDependencies(IDependencyCollector collector, List<FactorioObject> temp)
        {
            if (energy != null)
                collector.Add(energy.fuels, DependencyList.Flags.Fuel);
            if (mapGenerated)
                return;
            collector.Add(itemsToPlace, DependencyList.Flags.ItemToPlace);
        }

        public bool CanAcceptModule(ModuleSpecification module)
        {
            var effects = allowedEffects;
            // Check most common cases first
            if (effects == AllowedEffects.All)
                return true;
            if (effects == (AllowedEffects.Consumption | AllowedEffects.Pollution | AllowedEffects.Speed))
                return module.productivity == 0f;
            if (effects == AllowedEffects.None)
                return false;
            // Check the rest
            if (module.productivity != 0f && (effects & AllowedEffects.Productivity) == 0)
                return false;
            if (module.consumption != 0f && (effects & AllowedEffects.Consumption) == 0)
                return false;
            if (module.pollution != 0f && (effects & AllowedEffects.Pollution) == 0)
                return false;
            if (module.speed != 0f && (effects & AllowedEffects.Speed) == 0)
                return false;
            return true;
        }
    }

    public class EntityInserter : Entity
    {
        public bool isStackInserter { get; internal set; }
        public float inserterSwingTime { get; internal set; }
    }

    public class EntityAccumulator : Entity
    {
        public float accumulatorCapacity { get; internal set; }
    }

    public class EntityBelt : Entity
    {
        public float beltItemsPerSecond { get; internal set; }
    }

    public class EntityReactor : Entity
    {
        public float reactorNeighbourBonus { get; internal set; }
    }

    public class EntityBeacon : Entity
    {
        public float beaconEfficiency { get; internal set; }
    }

    public class EntityContainer : Entity
    {
        public int inventorySize { get; internal set; }
        public string logisticMode { get; internal set; }
        public int logisticSlotsCount { get; internal set; }
    }

    public class Technology : RecipeOrTechnology // Technology is very similar to recipe
    {
        public float count { get; internal set; } // TODO support formula count
        public Technology[] prerequisites { get; internal set; } = Array.Empty<Technology>();
        public Recipe[] unlockRecipes { get; internal set; } = Array.Empty<Recipe>();
        internal override FactorioObjectSortOrder sortingOrder => FactorioObjectSortOrder.Technologies;
        public override string type => "Technology";

        public override void GetDependencies(IDependencyCollector collector, List<FactorioObject> temp)
        {
            base.GetDependencies(collector, temp);
            if (prerequisites.Length > 0)
                collector.Add(new PackedList<Technology>(prerequisites), DependencyList.Flags.TechnologyPrerequisites);
            if (hidden && !enabled)
                collector.Add(Array.Empty<FactorioId>(), DependencyList.Flags.Hidden);
        }
    }

    public enum EntityEnergyType
    {
        Void,
        Electric,
        Heat,
        SolidFuel,
        FluidFuel,
        FluidHeat,
        Labor, // Special energy type for character
    }

    public class EntityEnergy
    {
        public EntityEnergyType type { get; internal set; }
        public TemperatureRange workingTemperature { get; internal set; }
        public TemperatureRange acceptedTemperature { get; internal set; } = TemperatureRange.Any;
        public float emissions { get; internal set; }
        public float fuelConsumptionLimit { get; internal set; } = float.PositiveInfinity;
        public PackedList<Goods> fuels { get; internal set; }
        public float effectivity { get; internal set; } = 1f;
    }

    public class ModuleSpecification
    {
        public float consumption { get; internal set; }
        public float speed { get; internal set; }
        public float productivity { get; internal set; }
        public float pollution { get; internal set; }
        public Recipe[] limitation { get; internal set; }
    }
    
    public struct TemperatureRange
    {
        public int min;
        public int max;
        
        public static readonly TemperatureRange Any = new TemperatureRange(int.MinValue, int.MaxValue);
        public bool IsAny() => min == int.MinValue && max == int.MaxValue;
        public bool IsSingle() => min == max;

        public TemperatureRange(int min, int max)
        {
            this.min = min;
            this.max = max;
        }
        
        public TemperatureRange(int single) : this(single, single) {}

        public override string ToString()
        {
            if (min == max)
                return min + "°";
            return min + "°-" + max + "°";
        }

        public bool Contains(int value) => min <= value && max >= value;
    }
}