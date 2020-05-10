using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Google.OrTools.LinearSolver;
using YAFC.UI;

namespace YAFC.Model
{
    public struct ProductionTableFlow
    {
        public Goods goods;
        public float amount;
        public float temperature;

        public ProductionTableFlow(Goods goods, float amount, float temperature)
        {
            this.goods = goods;
            this.amount = amount;
            this.temperature = temperature;
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

        private void AddFlow(RecipeRow recipe, Dictionary<Goods, (double prod, double cons, double temp)> summer)
        {
            foreach (var product in recipe.recipe.products)
            {
                summer.TryGetValue(product.goods, out var prev);
                var amount = recipe.recipesPerSecond * recipe.productionMultiplier * product.amount;
                prev.prod += amount;
                prev.temp += product.temperature * amount;
                summer[product.goods] = prev;
            }

            foreach (var ingredient in recipe.recipe.ingredients)
            {
                summer.TryGetValue(ingredient.goods, out var prev);
                prev.cons += recipe.recipesPerSecond * ingredient.amount;
                summer[ingredient.goods] = prev;
            }

            if (recipe.fuel != null && !float.IsNaN(recipe.fuelUsagePerSecondPerBuilding))
            {
                summer.TryGetValue(recipe.fuel, out var prev);
                var fuelUsage = recipe.fuelUsagePerSecondPerBuilding * recipe.recipesPerSecond * recipe.recipeTime;
                prev.cons += fuelUsage;
                summer[recipe.fuel] = prev;
                if (recipe.fuel.HasSpentFuel(out var spentFuel))
                {
                    summer.TryGetValue(spentFuel, out prev);
                    prev.prod += fuelUsage;
                    summer[spentFuel] = prev;
                }
            }
        }

        private void CalculateFlow(RecipeRow include)
        {
            var flowDict = new Dictionary<Goods, (double prod, double cons, double temp)>();
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
                        prev.prod += elem.amount;
                        if (elem.amount > 0f)
                            prev.temp += elem.amount * elem.temperature;
                        flowDict[elem.goods] = prev;
                    }
                }
                else
                {
                    AddFlow(recipe, flowDict);
                }
            }

            foreach (var link in links)
            {
                (double prod, double cons, double temp) flowParams;
                if ((link.flags & ProductionLink.Flags.LinkNotMatched) == 0)
                {
                    flowDict.Remove(link.goods, out flowParams);
                }
                else flowDict.TryGetValue(link.goods, out flowParams);
                link.resultTemperature = (float)(flowParams.temp/flowParams.prod);
                link.linkFlow = (float)flowParams.prod;
            }

            var flowArr = new ProductionTableFlow[flowDict.Count];
            var index = 0;
            foreach (var (k, (prod, cons, temp)) in flowDict)
            {
                flowArr[index++] = new ProductionTableFlow(k, (float)(prod - cons), (float) (temp / prod));
            }
            Array.Sort(flowArr, 0, flowArr.Length, this);
            flow = flowArr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddLinkCoef(Constraint cst, Variable var, ProductionLink link, RecipeRow recipe, float amount)
        {
            if (link.lastRecipe == recipe.recipe.id)
                amount += (float)cst.GetCoefficient(var);
            link.lastRecipe = recipe.recipe.id;
            cst.SetCoefficient(var, amount);
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
                    var energyUsage = recipe.entity.power * recipe.modules.energyUsageMod * recipe.entity.energy.effectivity;
                    
                    if ((recipe.recipe.flags & RecipeFlags.ScaleProductionWithPower) != 0)
                        recipe.warningFlags |= WarningFlags.FuelWithTemperatureNotLinked;

                    // Special case for fuel
                    if (recipe.fuel != null)
                    {
                        var fluid = recipe.fuel.fluid;
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
                                    energyUsage = maxEnergyProduction * recipe.entity.energy.effectivity;
                                    recipe.fuelUsagePerSecondPerBuilding = energy.fluidLimit;
                                }
                                else // limited by energy usage
                                    recipe.fuelUsagePerSecondPerBuilding = energyUsage / energyPerUnitOfFluid;
                            }
                        }
                        else
                            recipe.fuelUsagePerSecondPerBuilding = energyUsage / recipe.fuel.fuelValue;

                        if ((recipe.recipe.flags & RecipeFlags.ScaleProductionWithPower) != 0 && energyUsage > 0f)
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
                        if (recipe.FindLink(fluid, out var link))
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
            for (var i = 0; i < allLinks.Count; i++)
            {
                var link = allLinks[i];
                var constraint = solver.MakeConstraint(link.amount, double.PositiveInfinity, link.goods.name+"_recipe");
                constraints[i] = constraint;
                link.solverIndex = i;
                link.flags = link.amount > 0 ? ProductionLink.Flags.HasConsumption : link.amount < 0 ? ProductionLink.Flags.HasProduction : 0;
            }

            for (var i = 0; i < allRecipes.Count; i++)
            {
                var recipe = allRecipes[i];
                var recipeVar = vars[i];
                foreach (var product in recipe.recipe.products)
                {
                    if (product.amount <= 0f)
                        continue;
                    if (recipe.FindLink(product.goods, out var link))
                    {
                        link.flags |= ProductionLink.Flags.HasProduction;
                        if (product.goods.fluid != null)
                        {
                            link.minProductTemperature = MathF.Min(link.minProductTemperature, product.temperature);
                            link.maxProductTemperature = MathF.Max(link.maxProductTemperature, product.temperature);
                        }

                        var added = product.amount * recipe.productionMultiplier;
                        AddLinkCoef(constraints[link.solverIndex], recipeVar, link, recipe, added);
                        objCoefs[i] -= added;
                    }
                }

                foreach (var ingredient in recipe.recipe.ingredients)
                {
                    if (recipe.FindLink(ingredient.goods, out var link))
                    {
                        link.flags |= ProductionLink.Flags.HasConsumption;
                        AddLinkCoef(constraints[link.solverIndex], recipeVar, link, recipe, -ingredient.amount);
                        objCoefs[i] += ingredient.amount;
                    }
                }

                if (recipe.fuel != null)
                {
                    var fuelAmount = recipe.fuelUsagePerSecondPerBuilding * recipe.recipeTime;
                    if (recipe.FindLink(recipe.fuel, out var link))
                    {
                        link.flags |= ProductionLink.Flags.HasConsumption;
                        AddLinkCoef(constraints[link.solverIndex], recipeVar, link, recipe, -fuelAmount);
                        objCoefs[i] += fuelAmount;
                    }

                    if (recipe.fuel.HasSpentFuel(out var spentFuel) && recipe.FindLink(spentFuel, out link))
                    {
                        link.flags |= ProductionLink.Flags.HasProduction;
                        AddLinkCoef(constraints[link.solverIndex], recipeVar, link, recipe, fuelAmount);
                        objCoefs[i] -= fuelAmount;
                    }
                }
            }

            foreach (var link in allLinks)
            {
                if ((link.flags & ProductionLink.Flags.HasProductionAndConsumption) != ProductionLink.Flags.HasProductionAndConsumption)
                {
                    if ((link.flags & ProductionLink.Flags.HasProductionAndConsumption) == 0)
                        (link.owner as ProductionTable).RecordUndo(true).links.Remove(link);
                    link.flags |= ProductionLink.Flags.LinkNotMatched;
                    constraints[link.solverIndex].SetBounds(double.NegativeInfinity, double.PositiveInfinity); // remove link constraints
                }
            }

            await Ui.ExitMainThread();
            for (var i = 0; i < allRecipes.Count; i++)
                objective.SetCoefficient(vars[i], allRecipes[i].recipe.Cost());
            var result = solver.Solve();
            Console.WriteLine("Solver finished with result "+result);
            await Ui.EnterMainThread();
            if (result == Solver.ResultStatus.OPTIMAL || result == Solver.ResultStatus.FEASIBLE)
            {
                for (var i = 0; i < allLinks.Count; i++)
                {
                    var link = allLinks[i];
                    var constraint = constraints[i];
                    if (constraint == null)
                        continue;
                    var basisStatus = constraint.BasisStatus();
                    if (basisStatus != Solver.BasisStatus.AT_LOWER_BOUND)
                    {
                        link.flags |= ProductionLink.Flags.LinkIsRecirsive;
                        if (basisStatus == Solver.BasisStatus.FREE)
                            link.flags |= ProductionLink.Flags.LinkNotMatched;
                    }

                }
                
                for (var i = 0; i < allRecipes.Count; i++)
                {
                    var recipe = allRecipes[i];
                    recipe.recipesPerSecond = vars[i].SolutionValue();
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
            var amt1 = x.goods.fluid != null ? x.amount / 50f : x.amount;
            var amt2 = y.goods.fluid != null ? y.amount / 50f : y.amount;
            return amt1.CompareTo(amt2);
        }
    }
}