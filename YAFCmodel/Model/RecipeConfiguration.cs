using System;
using System.Collections.Generic;

namespace YAFC.Model
{
    public struct SolverParams
    {
        public float min;
        public float penalty;

        public bool restricted => penalty == float.PositiveInfinity;
        public float max => restricted ? min : float.PositiveInfinity;
    }
    
    public class ConnectionConfiguration : Configuration
    {
        public readonly WorkspaceConfiguration workspace;
        public readonly Goods type;
        public float temperature;
        public readonly List<NodePort> ports = new List<NodePort>();
        internal int solverTag;
        public ConnectionState connectionState { get; internal set; }

        public SolverParams GetSolverParams() => new SolverParams {penalty = type.GetComplexity()};

        public void AddPort(NodePort port)
        {
            RecordUndo(false);
            port.connection = this;
            ports.Add(port);
        }

        public void SetSomputationState(ConnectionState state)
        {
            connectionState = state;
        }

        public ConnectionConfiguration(WorkspaceConfiguration workspace, Goods type)
        {
            this.workspace = workspace;
            this.type = type;
        }

        internal override WorkspaceConfiguration ownerWorkspace => workspace;

        internal override object CreateUndoSnapshot() => new StoredConnection(this);

        internal override void RevertToUndoSnapshot(object snapshot)
        {
            foreach (var port in ports)
                if (port.connection == this)
                    port.connection = null;
            ports.Clear();
            ((StoredConnection) snapshot).Deserialize(this, ProjectObserver.validator);
        }

        internal override void Unspawn()
        {
            workspace.RemoveConnection(this);
        }

        internal override void Spawn()
        {
            workspace.AddConnection(this);
        }
    }
    
    public struct ModuleSpec
    {
        public float speed;
        public float productivity;
        public float efficiency;

        public static ModuleSpec operator *(ModuleSpec spec, float number)
        {
            return new ModuleSpec
            {
                speed = spec.speed * number,
                productivity = spec.productivity * number,
                efficiency = spec.efficiency * number
            };
        }

        public static ModuleSpec operator +(ModuleSpec a, ModuleSpec b)
        {
            return new ModuleSpec
            {
                speed = a.speed + b.speed,
                productivity = a.productivity + b.productivity,
                efficiency = a.efficiency + b.efficiency
            };
        }
    }
    
    public abstract class NodeConfiguration : Configuration
    {
        public readonly RecipeProduct[] products;
        public readonly RecipeIngredient[] ingredients;
        public readonly NodeId nodeId;
        public readonly WorkspaceConfiguration workspace;
        
        public float resultPerSecond;
        public int x, y;

        public int solverTag;

        private static readonly Random random = new Random(); // temp
        public abstract string varname { get; }

        protected NodeConfiguration(WorkspaceConfiguration workspace, int productCount, int ingredientCount, NodeId nodeId)
        {
            products = new RecipeProduct[productCount];
            ingredients = new RecipeIngredient[ingredientCount];
            this.workspace = workspace;
            this.nodeId = nodeId;
        }

        public void SetComputationResult(float perSecond)
        {
            resultPerSecond = perSecond;
        }

        internal override WorkspaceConfiguration ownerWorkspace => workspace;
        public virtual float excessAmount => 0f;

        public abstract bool valid { get; }
        public abstract SolverParams GetSolverParams();
        internal override void Unspawn()
        {
            workspace.RemoveNode(this);
        }

        internal override void Spawn()
        {
            workspace.AddNode(this);
        }
    }
    
    public sealed class BufferConfiguration : NodeConfiguration
    {
        public readonly Goods type;
        public float requestedAmount;

        public BufferConfiguration(WorkspaceConfiguration workspace, Goods type, NodeId nodeId) : base(workspace, 1, 1, nodeId)
        {
            this.type = type;
            ingredients[0] = new RecipeIngredient(this, 0, new Ingredient(type, 0f)) {flowAmount = -1f};
            products[0] = new RecipeProduct(this, 0, new Product {goods = type}) {flowAmount = 1f};
        }

        public override string varname => "result_"+type.type+"_"+type.name;

        public override float excessAmount => requestedAmount;
        public override bool valid => type != null;
        public override SolverParams GetSolverParams() => new SolverParams {min = requestedAmount, penalty = type.GetComplexity()};

        internal override object CreateUndoSnapshot() => new StoredBuffer(this);

        internal override void RevertToUndoSnapshot(object snapshot)
        {
            ((StoredBuffer) snapshot).Deserialize(this, ProjectObserver.validator);
        }
    }

    public abstract class NodePort
    {
        public readonly NodeConfiguration configuration;
        public readonly int index;
        public readonly bool input;
        public abstract Goods goods { get; }
        public float flowAmount; // positive for production, negative for consumption
        public ConnectionConfiguration connection { get; internal set; }

        protected NodePort(NodeConfiguration configuration, int index, bool input)
        {
            this.configuration = configuration;
            this.index = index;
            this.input = input;
        }
    }

    public sealed class RecipeProduct : NodePort
    {
        public readonly Product product;

        public RecipeProduct(NodeConfiguration configuration, int index, Product product) : base(configuration, index, false)
        {
            this.product = product;
        }

        public override Goods goods => product.goods;
    }

    public sealed class RecipeIngredient : NodePort
    {
        public readonly Ingredient ingredient;

        public RecipeIngredient(NodeConfiguration configuration, int index, Ingredient ingredient) : base(configuration, index, true)
        {
            this.ingredient = ingredient;
        }
        public override Goods goods => ingredient.goods;

        public float GetTemperatureOrDefault(float def) => connection?.temperature ?? def;
    }
    
    public sealed class RecipeConfiguration : NodeConfiguration
    {
        public readonly Recipe recipe;
        public Entity entity;
        public ModuleSpec modules;
        public Goods fuel;

        public float actualRecipeTime;

        public RecipeConfiguration(WorkspaceConfiguration workspace, Recipe recipe, NodeId nodeId) : base(workspace, recipe.products.Length, recipe.ingredients.Length+1, nodeId)
        {
            this.recipe = recipe;
            for (var i = 0; i < products.Length; i++)
                products[i] = new RecipeProduct(this, i, recipe.products[i]);
            ingredients[0] = new RecipeIngredient(this,0 ,new Ingredient(fuel ?? Database.voidEnergy, 1f));
            for (var i = 1; i < ingredients.Length; i++)
                ingredients[i] = new RecipeIngredient(this, i, recipe.ingredients[i-1]);
        }

        public override string varname => "recipe_"+recipe.name;

        public override bool valid => recipe != null;
        public override SolverParams GetSolverParams() => default;

        internal void Recalculate()
        {
            ingredients[0].ingredient.goods = fuel;
            actualRecipeTime = recipe.time / (entity.craftingSpeed * (1f + modules.speed));
            var actualModuleEfficiency = modules.efficiency < 0.8f ? modules.efficiency : 0.8f;
            var actualEnergyUsage = entity.power * (1f - actualModuleEfficiency) / entity.energy.effectivity;
            
            if ((recipe.flags & RecipeFlags.UsesFluidTemperature) != 0)
            {
                var outputTemp = products[0].product.temperature;
                var input = ingredients[1].ingredient;
                var inputTemp = ingredients[1].GetTemperatureOrDefault(input.minTemperature);
                var deltaTemp = (outputTemp - inputTemp);
                var energyPerUnitOfFluid = deltaTemp * (input.goods as Fluid)?.heatCapacity ?? 1f;
                if (deltaTemp > 0)
                    actualRecipeTime = energyPerUnitOfFluid / actualEnergyUsage;
            }
            
            float actualFuelUsage;
            if (entity.energy.usesHeat)
            {
                var fluid = fuel as Fluid; // should always be fluid
                var temp = entity.energy.maxTemperature;
                var fuelTemperature = ingredients[0].GetTemperatureOrDefault(float.PositiveInfinity);
                if (fuelTemperature < temp)
                    temp = fuelTemperature;
                if (fluid != null && fluid.maxTemperature < temp)
                    temp = fluid.maxTemperature;

                var minTemp = entity.energy.minTemperature;
                if (fluid != null && fluid.minTemperature > minTemp)
                    minTemp = fluid.minTemperature;

                var heatCap = fluid?.heatCapacity ?? 1f;
                var energyPerUnitOfFluid = (temp - minTemp) * heatCap;
                var maxEnergyProduction = entity.energy.fluidLimit * energyPerUnitOfFluid;
                if (maxEnergyProduction < actualEnergyUsage || actualEnergyUsage == 0) // limited by fluid limit
                {
                    if (actualEnergyUsage != 0)
                        actualRecipeTime *= actualEnergyUsage / maxEnergyProduction; 
                    actualEnergyUsage = maxEnergyProduction;
                    actualFuelUsage = entity.energy.fluidLimit;
                }
                else // limited by energy usage
                    actualFuelUsage = actualEnergyUsage / energyPerUnitOfFluid;
            }
            else
                actualFuelUsage = actualEnergyUsage / fuel.fuelValue;

            ingredients[0].flowAmount = -actualFuelUsage;
            var index = 1;
            foreach (var ingr in recipe.ingredients)
                ingredients[index++].flowAmount = -ingr.amount / actualRecipeTime;
            index = 0;
            var actualProductivity = (recipe.flags & RecipeFlags.ProductivityDisabled) != 0 ? 1f : (1f + modules.productivity) * (1f + entity.productivity);
            if ((recipe.flags & RecipeFlags.ScaleProductionWithPower) != 0)
                actualProductivity *= (actualEnergyUsage * entity.energy.effectivity * actualRecipeTime);
            foreach (var product in recipe.products)
                products[index++].flowAmount = product.amount * product.probability * actualProductivity / actualRecipeTime;
        }

        internal override object CreateUndoSnapshot() => new StoredRecipe(this);

        internal override void RevertToUndoSnapshot(object snapshot)
        {
            ((StoredRecipe) snapshot).Deserialize(this, ProjectObserver.validator);
        }
    }
}