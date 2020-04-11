using System;
using System.Drawing;
using System.Linq;
using SDL2;
using UI;

namespace FactorioData
{
    public interface IFactorioObjectWrapper
    {
        string text { get; }
        FactorioObject target { get; }
    }

    public abstract class FactorioObject : IFactorioObjectWrapper, IComparable<FactorioObject>
    {
        public string name;
        public string type;
        public string locName;
        public string locDescr;
        public FactorioIconPart[] iconSpec;
        public Sprite icon;
        public int id;
        
        protected enum SortingPriority
        {
            Item,
            Recipe,
            Entity,
            Technology,
            MilestonesNotMet,
            NotAccessible,
        }

        FactorioObject IFactorioObjectWrapper.target => this;
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

        public int CompareTo(FactorioObject other)
        {
            var thisPrio = GetSortingPriorityBase();
            var otherPrio = other.GetSortingPriorityBase();

            if (thisPrio.Item1 != otherPrio.Item1)
                return thisPrio.Item1.CompareTo(otherPrio.Item1);

            return thisPrio.Item2.CompareTo(otherPrio.Item2);
        }

        private (SortingPriority, int) GetSortingPriorityBase()
        {
            if (!this.IsAccessible())
                return (SortingPriority.NotAccessible, 0);
            return GetSortingPriority();
        }

        protected abstract (SortingPriority, int) GetSortingPriority();
    }
    
    public class FactorioIconPart
    {
        public string path;
        public float size = 32;
        public Color color;
        public float x, y, r, g, b, a;
        public float scale = 1;
    }

    [Flags]
    public enum RecipeFlags
    {
        UsesMiningProductivity = 1 << 0,
        ProductivityDisabled = 1 << 1,
        UsesFluidTemperature = 1 << 2,
        ScaleProductionWithPower = 1 << 3,
    }
    
    public class Recipe : FactorioObject
    {
        public PackedList<Entity> crafters;
        public Ingredient[] ingredients;
        public Product[] products;
        public PackedList<Technology> technologyUnlock;
        public Entity sourceEntity;
        public Goods mainProduct;
        public float time;
        public bool enabled;
        public bool hidden;
        public RecipeFlags flags;

        public override void GetDependencies(IDependencyCollector collector)
        {
            if (ingredients.Length > 0)
            {
                var ingList = new int[ingredients.Length];
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

        protected override (SortingPriority, int) GetSortingPriority() => (SortingPriority.Recipe, ingredients.Length);

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
    
    public class Mechanics : Recipe {}
    
    public class Ingredient : IFactorioObjectWrapper
    {
        public Goods goods;
        public float amount;
        public float minTemperature;
        public float maxTemperature;

        public Ingredient() {}
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
    }
    
    public class Product : IFactorioObjectWrapper
    {
        public Goods goods;
        public float amount;
        public float temperature;
        public float probability = 1;

        FactorioObject IFactorioObjectWrapper.target => goods;

        string IFactorioObjectWrapper.text
        {
            get
            {
                var text = goods.locName;
                if (amount != 1f)
                    text = amount + "x " + text;
                if (probability != 1)
                    text = (probability * 100) + "% " + text;
                if (temperature != 0)
                    text += " (" + temperature + "°)";
                return text;
            }
        }
    }

    // Abstract base for anything that can be produced or consumed by recipes (etc)
    public abstract class Goods : FactorioObject
    {
        public float fuelValue;
        
        public Recipe[] production;
        public Recipe[] usages;
        public Entity[] loot;

        public override void GetDependencies(IDependencyCollector collector)
        {
            collector.Add(new PackedList<FactorioObject>(production.Concat<FactorioObject>(loot)), DependencyList.Flags.Source);
        }

        protected override (SortingPriority, int) GetSortingPriority() => (SortingPriority.Item, -usages.Length);
    }
    
    public class Item : Goods
    {
        public Item fuelResult;
        public Entity placeResult;
    }
    
    public class Fluid : Goods
    {
        public float heatCapacity = 1e-3f;
        public float minTemperature;
        public float maxTemperature;
    }
    
    public class Special : Goods
    {
        public bool isPower;
    } 
    
    public class Entity : FactorioObject
    {
        public Product[] loot;
        public PackedList<Recipe> recipes;
        public bool mapGenerated;
        public float power;
        public EntityEnergy energy;
        public float craftingSpeed = 1f;
        public float productivity;
        public PackedList<Item> itemsToPlace;
        public int itemInputs;
        public int fluidInputs; // fluid inputs for recipe, not including power
        public Goods[] inputs;

        public override void GetDependencies(IDependencyCollector collector)
        {
            if (energy != null)
                collector.Add(energy.fuels, DependencyList.Flags.Fuel);
            if (mapGenerated)
                return;
            collector.Add(itemsToPlace, DependencyList.Flags.ItemToPlace);
        }

        protected override (SortingPriority, int) GetSortingPriority() => (SortingPriority.Entity, 0);
    }

    public class Technology : Recipe // Technology is very similar to recipe
    {
        public float count; // TODO support formula count
        public Technology[] prerequisites;
        public Recipe[] unlockRecipes;

        public override void GetDependencies(IDependencyCollector collector)
        {
            base.GetDependencies(collector);
            if (prerequisites.Length > 0)
                collector.Add(new PackedList<Technology>(prerequisites), DependencyList.Flags.TechnologyPrerequisites);
        }

        protected override (SortingPriority, int) GetSortingPriority() => (SortingPriority.Technology, 0);
    }

    public class EntityEnergy
    {
        public bool usesHeat;
        public float minTemperature;
        public float maxTemperature;
        public float fluidLimit = float.PositiveInfinity;
        public PackedList<Goods> fuels;
        public float effectivity = 1f;
    }
}