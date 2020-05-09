using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.OrTools.LinearSolver;
using YAFC.UI;

namespace YAFC.Model
{
    public struct ProductionTableFlow
    {
        public Goods goods;
        public float amount;

        public ProductionTableFlow(Goods goods, float amount)
        {
            this.goods = goods;
            this.amount = amount;
        }
    }
    public class ProductionTable : ProjectPageContents, IComparer<ProductionTableFlow>
    {
        public Dictionary<Goods, ProductionLink> linkMap { get; } = new Dictionary<Goods, ProductionLink>();
        public bool expanded { get; set; } = true;
        public List<ProductionLink> links { get; } = new List<ProductionLink>();
        public List<RecipeRow> recipes { get; } = new List<RecipeRow>();
        public ProductionTableFlow[] flow { get; private set; } = Array.Empty<ProductionTableFlow>();
        public ProductionTable(ModelObject owner) : base(owner) {}

        protected internal override void ThisChanged(bool visualOnly)
        {
            RebuildLinkMap();
            if (owner is ProjectPage page)
                page.ContentChanged(visualOnly);
            else if (owner is RecipeRow recipe)
                recipe.ThisChanged(visualOnly);
        }

        private void RebuildLinkMap()
        {
            linkMap.Clear();
            foreach (var link in links)
                linkMap[link.goods] = link;
        }

        private void Setup(List<RecipeRow> allRecipes, List<ProductionLink> allLinks)
        {
            foreach (var link in links)
            {
                allLinks.Add(link);
                if (link.goods is Fluid fluid)
                {
                    link.maxProductTemperature = fluid.minTemperature;
                    link.minProductTemperature = fluid.maxTemperature;
                }
            }

            foreach (var recipe in recipes)
            {
                allRecipes.Add(recipe);
                recipe.subgroup?.Setup(allRecipes, allLinks);
                
                foreach (var product in recipe.recipe.products)
                {
                    if (product.goods is Fluid fluid && recipe.FindLink(fluid, out var fluidLink))
                    {
                        fluidLink.maxProductTemperature = MathF.Max(fluidLink.maxProductTemperature, product.temperature);
                        fluidLink.minProductTemperature = MathF.Min(fluidLink.minProductTemperature, product.temperature);
                    }
                }
            }
        }

        private void AddFlow(RecipeRow recipe, Dictionary<Goods, float> summer)
        {
            foreach (var product in recipe.recipe.products)
            {
                summer.TryGetValue(product.goods, out var prev);
                summer[product.goods] = prev + recipe.recipesPerSecond * recipe.productionMultiplier * product.amount;
            }

            foreach (var ingredient in recipe.recipe.ingredients)
            {
                summer.TryGetValue(ingredient.goods, out var prev);
                summer[ingredient.goods] = prev - recipe.recipesPerSecond * ingredient.amount;
            }

            if (recipe.fuel != null && !float.IsNaN(recipe.fuelUsagePerSecondPerBuilding))
            {
                summer.TryGetValue(recipe.fuel, out var prev);
                summer[recipe.fuel] = prev - recipe.fuelUsagePerSecondPerBuilding * recipe.recipesPerSecond * recipe.recipeTime;
            }
        }

        private void CalculateFlow(RecipeRow include)
        {
            var flowDict = new Dictionary<Goods, float>();
            if (include != null)
                AddFlow(include, flowDict);
            foreach (var recipe in recipes)
            {
                if (recipe.subgroup != null)
                {
                    recipe.subgroup.CalculateFlow(recipe);
                    foreach (var elem in recipe.subgroup.flow)
                    {
                        flowDict.TryGetValue(elem.goods, out var prev);
                        flowDict[elem.goods] = prev + elem.amount;
                    }
                }
                else
                {
                    AddFlow(recipe, flowDict);
                }
            }

            foreach (var link in links)
            {
                if ((link.flags & ProductionLink.Flags.LinkNotMatched) == 0)
                    flowDict.Remove(link.goods);
            }

            var flowArr = new ProductionTableFlow[flowDict.Count];
            var index = 0;
            foreach (var (k, v) in flowDict)
                flowArr[index++] = new ProductionTableFlow(k, v);
            Array.Sort(flowArr, 0, flowArr.Length, this);
            flow = flowArr;
        }

        public override async Task Solve(ProjectPage page)
        {
            var solver = DataUtils.CreateSolver("ProductionTableSolver");
            var objective = solver.Objective();
            objective.SetMinimization();
            var allRecipes = new List<RecipeRow>();
            var allLinks = new List<ProductionLink>();
            Setup(allRecipes, allLinks);
            var vars = new Variable[allRecipes.Count];
            var objCoefs = new float[allRecipes.Count];
            var coefArray = new float[allRecipes.Count];

            for (var i = 0; i < allRecipes.Count; i++)
            {
                var recipe = allRecipes[i];
                recipe.warningFlags = 0;
                if (recipe.entity == null)
                {
                    recipe.warningFlags |= WarningFlags.EntityNotSpecified;
                    recipe.recipeTime = recipe.recipe.time;
                    recipe.productionMultiplier = 1f;
                }
                else
                {
                    recipe.recipeTime = recipe.recipe.time / (recipe.entity.craftingSpeed * (1f + recipe.modules.speed));
                    recipe.productionMultiplier = (1f + recipe.modules.productivity) * (1f + recipe.entity.productivity);
                    var energyUsage = recipe.entity.power * recipe.modules.energyUsageMod / recipe.entity.energy.effectivity;
                    
                    if ((recipe.recipe.flags & RecipeFlags.ScaleProductionWithPower) != 0)
                        recipe.warningFlags |= WarningFlags.FuelWithTemperatureNotLinked;

                    // Special case for fuel
                    if (recipe.fuel != null)
                    {
                        var fluid = recipe.fuel as Fluid;
                        var energy = recipe.entity.energy;
                        recipe.FindLink(recipe.fuel, out var link);
                        var usesHeat = fluid != null && energy.usesHeat;
                        if (usesHeat)
                        {
                            if (link == null)
                            {
                                recipe.fuelUsagePerSecondPerBuilding = float.NaN;
                            }
                            else
                            {
                                // TODO research this case;
                                if (link.maxProductTemperature != link.minProductTemperature)
                                    recipe.warningFlags |= WarningFlags.TemperatureRangeForFuelNotImplemented;

                                var heatCap = fluid.heatCapacity;
                                var energyPerUnitOfFluid = (link.minProductTemperature - energy.minTemperature) * heatCap;
                                var maxEnergyProduction = energy.fluidLimit * energyPerUnitOfFluid;
                                if (maxEnergyProduction < energyUsage || energyUsage <= 0) // limited by fluid limit
                                {
                                    if (energyUsage <= 0)
                                        recipe.recipeTime *= energyUsage / maxEnergyProduction;
                                    energyUsage = maxEnergyProduction;
                                    recipe.fuelUsagePerSecondPerBuilding = energy.fluidLimit;
                                }
                                else // limited by energy usage
                                    recipe.fuelUsagePerSecondPerBuilding = energyUsage / energyPerUnitOfFluid;
                            }
                        }
                        else
                            recipe.fuelUsagePerSecondPerBuilding = energyUsage / recipe.fuel.fuelValue;

                        if ((recipe.recipe.flags & RecipeFlags.ScaleProductionWithPower) != 0 && link != null)
                        {
                            recipe.recipeTime = 1f / energyUsage;
                            recipe.warningFlags &= ~WarningFlags.FuelWithTemperatureNotLinked;
                        }
                    }
                    else
                    {
                        recipe.fuelUsagePerSecondPerBuilding = energyUsage;
                        recipe.warningFlags |= WarningFlags.FuelNotSpecified;
                    }

                    // Special case for boilers
                    if ((recipe.recipe.flags & RecipeFlags.UsesFluidTemperature) != 0)
                    {
                        var fluid = recipe.recipe.ingredients[0].goods as Fluid;
                        if (fluid == null)
                            continue;
                        float inputTemperature;
                        if (recipe.FindLink(recipe.fuel, out var link))
                        {
                            if (link.maxProductTemperature != link.minProductTemperature)
                                recipe.warningFlags |= WarningFlags.TemperatureRangeForBoilerNotImplemented;
                            inputTemperature = link.minProductTemperature;
                        }
                        else inputTemperature = fluid.minTemperature;
                            
                        var outputTemp = recipe.recipe.products[0].temperature;
                        var deltaTemp = (outputTemp - inputTemperature);
                        var energyPerUnitOfFluid = deltaTemp * fluid.heatCapacity;
                        if (deltaTemp > 0 && recipe.fuel != null)
                            recipe.recipeTime = energyPerUnitOfFluid / (recipe.fuelUsagePerSecondPerBuilding * recipe.fuel.fuelValue);
                    }
                }
                var variable = solver.MakeNumVar(0d, double.PositiveInfinity, recipe.recipe.name);
                vars[i] = variable;
            }

            var constraints = new Constraint[allLinks.Count];

            for (var index = 0; index < allLinks.Count; index++)
            {
                var link = allLinks[index];
                var constraint = solver.MakeConstraint(link.amount, double.PositiveInfinity);
                constraints[index] = constraint;
                Array.Clear(coefArray, 0, coefArray.Length);
                var goods = link.goods;
                var fluid = goods as Fluid;
                float minTemp = float.PositiveInfinity, maxTemp = float.NegativeInfinity;
                var hasProduction = link.amount < 0f;
                var hasConsumption = link.amount > 0f;
                for (var i = 0; i < allRecipes.Count; i++)
                {
                    var recipe = allRecipes[i];
                    foreach (var product in recipe.recipe.products)
                    {
                        if (product.goods != goods)
                            continue;
                        if (fluid != null)
                        {
                            minTemp = MathF.Min(minTemp, product.temperature);
                            maxTemp = MathF.Max(maxTemp, product.temperature);
                        }

                        var added = product.average * recipe.productionMultiplier;

                        coefArray[i] += added;
                        objCoefs[i] -= added;
                        hasProduction = true;
                    }
                }

                for (var i = 0; i < allRecipes.Count; i++)
                {
                    var recipe = allRecipes[i];
                    if (recipe.fuel == goods && recipe.entity != null)
                    {
                        coefArray[i] -= recipe.fuelUsagePerSecondPerBuilding * recipe.recipeTime;
                        objCoefs[i] += recipe.fuelUsagePerSecondPerBuilding * recipe.recipeTime;
                        hasConsumption = true;
                    }

                    foreach (var ingredient in recipe.recipe.ingredients)
                    {
                        if (ingredient.goods != goods)
                            continue;
                        coefArray[i] -= ingredient.amount;
                        objCoefs[i] += ingredient.amount;
                        hasConsumption = true;
                    }
                }

                if (hasProduction && hasConsumption)
                {
                    for (var i = 0; i < coefArray.Length; i++)
                    {
                        if (coefArray[i] != 0f)
                            constraint.SetCoefficient(vars[i], coefArray[i]);
                    }
                }
                else
                {
                    if (hasProduction)
                    {
                        foreach (var recipe in allRecipes.Where(recipe => recipe.recipe.products.Any(product => product.goods == goods)))
                            recipe.warningFlags |= WarningFlags.LinkedProductionNotConsumed;
                    }

                    if (hasConsumption)
                    {
                        foreach (var recipe in allRecipes.Where(recipe => recipe.fuel == goods || recipe.recipe.ingredients.Any(ingredient => ingredient.goods == goods)))
                            recipe.warningFlags |= WarningFlags.LinkedConsumptionNotProduced;
                    }
                }
            }

            await Ui.ExitMainThread();
            for (var i = 0; i < allRecipes.Count; i++)
                objective.SetCoefficient(vars[i], allRecipes[i].recipe.Cost());
            var result = solver.Solve();
            Console.WriteLine("Solver finished with result "+result);
            Console.WriteLine(solver.ExportModelAsLpFormat(false));
            await Ui.EnterMainThread();
            if (result == Solver.ResultStatus.OPTIMAL || result == Solver.ResultStatus.FEASIBLE)
            {
                for (var i = 0; i < allLinks.Count; i++)
                {
                    var link = allLinks[i];
                    var constraint = constraints[i];
                    var basisStatus = constraint.BasisStatus();
                    link.flags = 0;
                    if (basisStatus != Solver.BasisStatus.AT_LOWER_BOUND)
                        link.flags |= ProductionLink.Flags.LinkNotMatched;
                }
                
                for (var i = 0; i < allRecipes.Count; i++)
                {
                    var recipe = allRecipes[i];
                    recipe.recipesPerSecond = (float)vars[i].SolutionValue();
                }

                CalculateFlow(null);
            }
            solver.Dispose();
        }

        public bool FindLink(Goods goods, out ProductionLink link)
        {
            var searchFrom = this;
            while (true)
            {
                if (searchFrom.linkMap.TryGetValue(goods, out link))
                    return true;
                if (searchFrom.owner is RecipeRow row)
                    searchFrom = row.owner;
                else return false;
            }
        }

        public int Compare(ProductionTableFlow x, ProductionTableFlow y)
        {
            var amt1 = x.goods.isFluid ? x.amount / 50f : x.amount;
            var amt2 = y.goods.isFluid ? y.amount / 50f : y.amount;
            return amt1.CompareTo(amt2);
        }
    }
}