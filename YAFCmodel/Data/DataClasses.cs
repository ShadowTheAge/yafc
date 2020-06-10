using System.Runtime.CompilerServices;
using System;
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
        public string typeDotName { get; internal set; }
        public string locName { get; internal set; }
        public string locDescr { get; internal set; }
        public FactorioIconPart[] iconSpec { get; internal set; }
        public Icon icon { get; internal set; }
        public FactorioId id { get; internal set; }
        internal abstract FactorioObjectSortOrder sortingOrder { get; }
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

        public abstract void GetDependencies(IDependencyCollector collector);

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
        ScaleProductionWithPower = 1 << 3
    }
    
    public abstract class RecipeOrTechnology : FactorioObject
    {
        public PackedList<Entity> crafters { get; internal set; }
        public Ingredient[] ingredients { get; internal set; }
        public Product[] products { get; internal set; }
        public Item[] modules { get; internal set; } = Array.Empty<Item>();
        public PackedList<Technology> technologyUnlock { get; internal set; }
        public Entity sourceEntity { get; internal set; }
        public Goods mainProduct { get; internal set; }
        public float time { get; internal set; }
        public bool enabled { get; internal set; }
        public bool hidden { get; internal set; }
        public RecipeFlags flags { get; internal set; }
        public override string type => "Recipe";

        internal override FactorioObjectSortOrder sortingOrder => FactorioObjectSortOrder.Recipes;

        public override void GetDependencies(IDependencyCollector collector)
        {
            if (ingredients.Length > 0)
            {
                var ingList = new FactorioId[ingredients.Length];
                for (var i = 0; i < ingredients.Length; i++)
                    ingList[i] = ingredients[i].goods.id;
                collector.Add(ingList, DependencyList.Flags.Ingredient);
            }
            collector.Add(crafters, DependencyList.Flags.CraftingEntity);
            if (sourceEntity != null)
                collector.Add(new[] {sourceEntity.id}, DependencyList.Flags.SourceEntity);
            if (!enabled)
                collector.Add(technologyUnlock, DependencyList.Flags.TechnologyUnlock);
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
    
    public class Recipe : RecipeOrTechnology {}

    public class Mechanics : Recipe
    {
        internal override FactorioObjectSortOrder sortingOrder => FactorioObjectSortOrder.Mechanics;
        public override string type => "Mechanics";
    }
    
    public class Ingredient : IFactorioObjectWrapper
    {
        public readonly Goods goods;
        public readonly float amount;
        public float minTemperature { get; internal set; }
        public float maxTemperature { get; internal set; }
        public Ingredient(Goods goods, float amount)
        {
            this.goods = goods;
            this.amount = amount;
            if (goods is Fluid fluid)
            {
                minTemperature = fluid.minTemperature;
                maxTemperature = fluid.maxTemperature;
            }
        }

        string IFactorioObjectWrapper.text
        {
            get
            {
                var text = goods.locName;
                if (amount != 1f)
                    text = amount + "x " + text;
                if (minTemperature != 0 || maxTemperature != 0)
                    text += " ("+minTemperature+"°-"+maxTemperature+"°)";
                return text;
            }
        }

        FactorioObject IFactorioObjectWrapper.target => goods;

        float IFactorioObjectWrapper.amount => amount;
    }
    
    public class Product : IFactorioObjectWrapper
    {
        public readonly Goods goods;
        public readonly float rawAmount; // This is average amount
        public Product(Goods goods, float amount)
        {
            this.goods = goods;
            this.rawAmount = amount;
        }

        public float amount => rawAmount * probability;
        public float temperature { get; internal set; }
        public float probability { get; internal set; } = 1;

        FactorioObject IFactorioObjectWrapper.target => goods;

        string IFactorioObjectWrapper.text
        {
            get
            {
                var text = goods.locName;
                if (rawAmount != 1f)
                    text = rawAmount + "x " + text;
                if (probability != 1)
                    text = (probability * 100) + "% " + text;
                if (temperature != 0)
                    text += " (" + temperature + "°)";
                return text;
            }
        }
        float IFactorioObjectWrapper.amount => amount;
    }

    // Abstract base for anything that can be produced or consumed by recipes (etc)
    public abstract class Goods : FactorioObject
    {
        public float fuelValue;
        public abstract bool isPower { get; }
        public virtual Fluid fluid => null;
        public Recipe[] production { get; internal set; }
        public Recipe[] usages { get; internal set; }
        public FactorioObject[] miscSources { get; internal set; }
        public abstract UnitOfMeasure flowUnitOfMeasure { get; }

        public override void GetDependencies(IDependencyCollector collector)
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
        public float minTemperature { get; internal set; }
        public float maxTemperature { get; internal set; }
        public override bool isPower => false;
        public override UnitOfMeasure flowUnitOfMeasure => UnitOfMeasure.FluidPerSecond;
        internal override FactorioObjectSortOrder sortingOrder => FactorioObjectSortOrder.Fluids;
    }
    
    public class Special : Goods
    {
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
        public float beltItemsPerSecond { get; internal set; }
        public EntityEnergy energy { get; internal set; }
        public float craftingSpeed { get; internal set; } = 1f;
        public float productivity { get; internal set; }
        public PackedList<Item> itemsToPlace { get; internal set; }
        public int itemInputs { get; internal set; }
        public int fluidInputs { get; internal set; } // fluid inputs for recipe, not including power
        public Goods[] inputs { get; internal set; }
        public AllowedEffects allowedEffects { get; internal set; } = AllowedEffects.All;
        public int moduleSlots { get; internal set; }
        internal override FactorioObjectSortOrder sortingOrder => FactorioObjectSortOrder.Entities;
        public float beaconEfficiency { get; internal set; }
        public override string type => "Entity";

        public override void GetDependencies(IDependencyCollector collector)
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

    public class Technology : RecipeOrTechnology // Technology is very similar to recipe
    {
        public float count { get; internal set; } // TODO support formula count
        public Technology[] prerequisites { get; internal set; } = Array.Empty<Technology>();
        public Recipe[] unlockRecipes { get; internal set; } = Array.Empty<Recipe>();
        internal override FactorioObjectSortOrder sortingOrder => FactorioObjectSortOrder.Technologies;
        public override string type => "Technology";

        public override void GetDependencies(IDependencyCollector collector)
        {
            base.GetDependencies(collector);
            if (prerequisites.Length > 0)
                collector.Add(new PackedList<Technology>(prerequisites), DependencyList.Flags.TechnologyPrerequisites);
        }
    }

    public enum EntityEnergyType
    {
        Void,
        Electric,
        Heat,
        SolidFuel,
        FluidFuel,
        Labor, // Special energy type for character
    }

    public class EntityEnergy
    {
        public EntityEnergyType type { get; internal set; }
        public bool usesHeat { get; internal set; }
        public float minTemperature { get; internal set; }
        public float maxTemperature { get; internal set; }
        public float emissions { get; internal set; }
        public float fluidLimit { get; internal set; } = float.PositiveInfinity;
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
}