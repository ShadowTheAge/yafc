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

    public class ProductionTable : ProjectPageContents, IComparer<ProductionTableFlow>, IInputSettingsProvider
    {
        public Dictionary<Goods, ProductionLink> linkMap { get; } = new Dictionary<Goods, ProductionLink>();
        public bool expanded { get; set; } = true;
        public List<ProductionLink> links { get; } = new List<ProductionLink>();
        public List<RecipeRow> recipes { get; } = new List<RecipeRow>();
        public ProductionTableFlow[] flow { get; private set; } = Array.Empty<ProductionTableFlow>();
        public ModuleFillerParameters modules { get; set; }

        public ProductionTable(ModelObject owner) : base(owner)
        {
            if (owner is ProjectPage)
                modules = new ModuleFillerParameters(this);
        }

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
                var amount = recipe.recipesPerSecond * recipe.parameters.productionMultiplier * product.amount;
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

            if (recipe.fuel != null && !float.IsNaN(recipe.parameters.fuelUsagePerSecondPerBuilding))
            {
                summer.TryGetValue(recipe.fuel, out var prev);
                var fuelUsage = recipe.parameters.fuelUsagePerSecondPerRecipe * recipe.recipesPerSecond;
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
                        if (elem.amount > 0f)
                        {
                            prev.temp += elem.amount * elem.temperature;
                            prev.prod += elem.amount;
                        }
                        else prev.cons -= elem.amount;
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
                if (!link.flags.HasFlagAny(ProductionLink.Flags.LinkNotMatched))
                    flowDict.Remove(link.goods, out flowParams);
                else
                {
                    flowDict.TryGetValue(link.goods, out flowParams);
                    if (Math.Abs(flowParams.prod - flowParams.cons) > 1e-8f && link.owner.owner is RecipeRow recipe && recipe.owner.FindLink(link.goods, out var parent))
                        parent.flags |= ProductionLink.Flags.ChildNotMatched | ProductionLink.Flags.LinkNotMatched;
                }
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

        public override async Task<string> Solve(ProjectPage page)
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
                recipe.parameters.CalculateParameters(recipe.recipe, recipe.entity, recipe.fuel, recipe.module, recipe.linkRoot, modules);
                var variable = solver.MakeNumVar(0d, double.PositiveInfinity, recipe.recipe.name);
                vars[i] = variable;
            }

            var constraints = new Constraint[allLinks.Count];
            for (var i = 0; i < allLinks.Count; i++)
            {
                var link = allLinks[i];
                var min = link.algorithm == LinkAlgorithm.AllowOverConsumption ? float.NegativeInfinity : link.amount;
                var max = link.algorithm == LinkAlgorithm.AllowOverProduction ? float.PositiveInfinity : link.amount;
                var constraint = solver.MakeConstraint(min, max, link.goods.name+"_recipe");
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

                        var added = product.amount * recipe.parameters.productionMultiplier;
                        AddLinkCoef(constraints[link.solverIndex], recipeVar, link, recipe, added);
                        var cost = product.goods.Cost();
                        if (cost > 0f)
                            objCoefs[i] += added * cost;
                    }
                }

                foreach (var ingredient in recipe.recipe.ingredients)
                {
                    if (recipe.FindLink(ingredient.goods, out var link))
                    {
                        link.flags |= ProductionLink.Flags.HasConsumption;
                        AddLinkCoef(constraints[link.solverIndex], recipeVar, link, recipe, -ingredient.amount);
                    }
                }

                if (recipe.fuel != null)
                {
                    var fuelAmount = recipe.parameters.fuelUsagePerSecondPerRecipe;
                    if (recipe.FindLink(recipe.fuel, out var link))
                    {
                        link.flags |= ProductionLink.Flags.HasConsumption;
                        AddLinkCoef(constraints[link.solverIndex], recipeVar, link, recipe, -fuelAmount);
                    }

                    if (recipe.fuel.HasSpentFuel(out var spentFuel) && recipe.FindLink(spentFuel, out link))
                    {
                        link.flags |= ProductionLink.Flags.HasProduction;
                        AddLinkCoef(constraints[link.solverIndex], recipeVar, link, recipe, fuelAmount);
                        if (spentFuel.Cost() > 0f)
                            objCoefs[i] += fuelAmount * spentFuel.Cost();
                    }
                }
            }

            foreach (var link in allLinks)
            {
                link.notMatchedFlow = 0f;
                if (!link.flags.HasFlags(ProductionLink.Flags.HasProductionAndConsumption))
                {
                    if (!link.flags.HasFlagAny(ProductionLink.Flags.HasProductionAndConsumption))
                        link.owner.RecordUndo(true).links.Remove(link);
                    link.flags |= ProductionLink.Flags.LinkNotMatched;
                    constraints[link.solverIndex].SetBounds(double.NegativeInfinity, double.PositiveInfinity); // remove link constraints
                }
            }

            (Variable positive, Variable negative)[] slackVars = null;
            await Ui.ExitMainThread();
            for (var i = 0; i < allRecipes.Count; i++)
                objective.SetCoefficient(vars[i], allRecipes[i].recipe.RecipeBaseCost());
            var result = solver.Solve();
            if (result != Solver.ResultStatus.FEASIBLE && result != Solver.ResultStatus.OPTIMAL)
            {
                objective.Clear();
                var (deadlocks, splits) = GetInfeasibilityCandidates(allRecipes);
                slackVars = new (Variable positive, Variable negative)[allLinks.Count];
                // Solution does not exist. Adding slack variables to find the reason
                foreach (var link in deadlocks)
                {
                    // Adding negative slack to possible deadlocks (loops)
                    var constraint = constraints[link.solverIndex];
                    var cost = MathF.Abs(link.goods.Cost());
                    var negativeSlack = solver.MakeNumVar(0d, double.PositiveInfinity, "negative-slack." + link.goods.name);
                    constraint.SetCoefficient(negativeSlack, cost);
                    objective.SetCoefficient(negativeSlack, 1f);
                    slackVars[link.solverIndex].negative = negativeSlack;
                }

                foreach (var link in splits)
                {
                    // Adding positive slack to splits
                    var cost = MathF.Abs(link.goods.Cost());
                    var constraint = constraints[link.solverIndex];
                    var positiveSlack = solver.MakeNumVar(0d, double.PositiveInfinity, "positive-slack." + link.goods.name);
                    constraint.SetCoefficient(positiveSlack, -cost);
                    objective.SetCoefficient(positiveSlack, 1f);
                    slackVars[link.solverIndex].positive = positiveSlack;
                }

                result = solver.Solve();
                if (result == Solver.ResultStatus.OPTIMAL || result == Solver.ResultStatus.FEASIBLE)
                {
                    var linkList = new List<ProductionLink>();
                    for (var i = 0; i < allLinks.Count; i++)
                    {
                        var (posSlack, negSlack) = slackVars[i];
                        if (posSlack != null && posSlack.BasisStatus() != Solver.BasisStatus.AT_LOWER_BOUND)
                        {
                            linkList.Add(allLinks[i]);
                            allLinks[i].notMatchedFlow += (float) posSlack.SolutionValue();
                        }

                        if (negSlack != null && negSlack.BasisStatus() != Solver.BasisStatus.AT_LOWER_BOUND)
                        {
                            linkList.Add(allLinks[i]);
                            allLinks[i].notMatchedFlow -= (float) negSlack.SolutionValue();
                        }
                    }

                    foreach (var link in linkList)
                    {
                        if (link.notMatchedFlow == 0f)
                            continue;
                        link.flags |= ProductionLink.Flags.LinkNotMatched | ProductionLink.Flags.LinkRecursiveNotMatched;
                        var ownerRecipe = link.owner.owner as RecipeRow;
                        while (ownerRecipe != null)
                        {
                            if (link.notMatchedFlow > 0f)
                                ownerRecipe.parameters.warningFlags |= WarningFlags.OverproductionRequired;
                            else ownerRecipe.parameters.warningFlags |= WarningFlags.DeadlockCandidate;
                            ownerRecipe = ownerRecipe.owner.owner as RecipeRow;
                        }
                    }
                    
                    foreach (var recipe in allRecipes)
                    {
                        FindAllRecipeLinks(recipe, linkList, linkList);
                        foreach (var link in linkList)
                        {
                            if (link.flags.HasFlags(ProductionLink.Flags.LinkRecursiveNotMatched))
                            {
                                if (link.notMatchedFlow > 0f)
                                    recipe.parameters.warningFlags |= WarningFlags.OverproductionRequired;
                                else recipe.parameters.warningFlags |= WarningFlags.DeadlockCandidate;
                            }
                        }
                    }
                }
                else
                {
                    if (result == Solver.ResultStatus.INFEASIBLE)
                        return "YAFC tried to solve this model and failed. It then tried to find a deadlock loop, but failed again";
                    if (result == Solver.ResultStatus.ABNORMAL)
                        return "This model has numerical errors (probably too small or too large numbers) and cannot be solved";
                    return "Unaccounted error: MODEL_" + result;
                }
            }
                
            
            
            Console.WriteLine("Solver finished with result "+result);
            await Ui.EnterMainThread();
            for (var i = 0; i < allLinks.Count; i++)
            {
                var link = allLinks[i];
                var constraint = constraints[i];
                if (constraint == null)
                    continue;
                var basisStatus = constraint.BasisStatus();
                if (basisStatus == Solver.BasisStatus.BASIC || basisStatus == Solver.BasisStatus.FREE)
                {
                    link.flags |= ProductionLink.Flags.LinkNotMatched;
                }

            }
                
            for (var i = 0; i < allRecipes.Count; i++)
            {
                var recipe = allRecipes[i];
                recipe.recipesPerSecond = vars[i].SolutionValue();
                var slack = slackVars?[i].positive;
                if (slack != null && slack.BasisStatus() != Solver.BasisStatus.AT_LOWER_BOUND)
                    recipe.recipesPerSecond += slack.SolutionValue();
            }

            CalculateFlow(null);
            solver.Dispose();
            return null;
        }

        private void FindAllRecipeLinks(RecipeRow recipe, List<ProductionLink> sources, List<ProductionLink> targets)
        {
            sources.Clear();
            targets.Clear();
            ProductionLink link;
            foreach (var product in recipe.recipe.products)
                if (recipe.FindLink(product.goods, out link))
                    targets.Add(link);
            foreach (var ingr in recipe.recipe.ingredients)
                if (recipe.FindLink(ingr.goods, out link))
                    sources.Add(link);
            if (recipe.fuel != null)
            {
                if (recipe.FindLink(recipe.fuel, out link))
                    sources.Add(link);
                if (recipe.fuel.HasSpentFuel(out var spent) && recipe.FindLink(spent, out link))
                    targets.Add(link);
            }
        }
        
        private (List<ProductionLink> merges, List<ProductionLink> splits) GetInfeasibilityCandidates(List<RecipeRow> recipes)
        {
            var graph = new Graph<ProductionLink>();
            var sources = new List<ProductionLink>();
            var targets = new List<ProductionLink>();
            var splits = new List<ProductionLink>();

            foreach (var recipe in recipes)
            {
                FindAllRecipeLinks(recipe, sources, targets);
                foreach (var src in sources)
                    foreach (var tgt in targets)
                        graph.Connect(src, tgt);
                if (targets.Count > 1)
                    splits.AddRange(targets);
            }

            var loops = graph.MergeStrongConnectedComponents();
            sources.Clear();
            foreach (var possibleLoop in loops)
            {
                if (possibleLoop.userdata.list != null)
                {
                    var list = possibleLoop.userdata.list;
                    var last = list[list.Length - 1];
                    sources.Add(last);
                    for (var i = 0; i < list.Length-1; i++)
                    {
                        for (var j = i+2; j < list.Length; j++)
                        {
                            if (graph.HasConnection(list[i], list[j]))
                            {
                                sources.Add(list[i]);
                                break;
                            }
                        }
                    }
                }
            }

            return (sources, splits);
        }

        public bool FindLink(Goods goods, out ProductionLink link)
        {
            if (goods == null)
            {
                link = null;
                return false;
            }
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

        public bool GetTemperature(Fluid input, out float min, out float max)
        {
            if (FindLink(input, out var link))
            {
                min = link.minProductTemperature;
                max = link.minProductTemperature;
                return true;
            }

            min = input.minTemperature;
            max = input.maxTemperature;
            return false;
        }
    }
}