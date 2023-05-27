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
        public ProductionLink link;

        public ProductionTableFlow(Goods goods, float amount, ProductionLink link)
        {
            this.goods = goods;
            this.amount = amount;
            this.link = link;
        }
    }

    public class ProductionTable : ProjectPageContents, IComparer<ProductionTableFlow>, IElementGroup<RecipeRow>
    {
        [SkipSerialization] public Dictionary<Goods, ProductionLink> linkMap { get; } = new Dictionary<Goods, ProductionLink>();
        List<RecipeRow> IElementGroup<RecipeRow>.elements => recipes;
        public bool expanded { get; set; } = true;
        public List<ProductionLink> links { get; } = new List<ProductionLink>();
        public List<RecipeRow> recipes { get; } = new List<RecipeRow>();
        public ProductionTableFlow[] flow { get; private set; } = Array.Empty<ProductionTableFlow>();
        public ModuleFillerParameters modules { get; set; }
        public bool containsDesiredProducts { get; private set; }

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

        public void RebuildLinkMap()
        {
            linkMap.Clear();
            foreach (var link in links)
                linkMap[link.goods] = link;
        }

        private void Setup(List<RecipeRow> allRecipes, List<ProductionLink> allLinks)
        {
            containsDesiredProducts = false;
            foreach (var link in links)
            {
                if (link.amount != 0f)
                    containsDesiredProducts = true;
                allLinks.Add(link);
                link.capturedRecipes.Clear();
                link.lastRecipe = null;
            }

            foreach (var recipe in recipes)
            {
                if (!recipe.enabled)
                {
                    ClearDisabledRecipeContents(recipe);
                    continue;
                }

                recipe.hierarchyEnabled = true;
                allRecipes.Add(recipe);
                recipe.subgroup?.Setup(allRecipes, allLinks);
            }
        }

        private void ClearDisabledRecipeContents(RecipeRow recipe)
        {
            recipe.recipesPerSecond = 0;
            recipe.parameters.Clear();
            recipe.hierarchyEnabled = false;
            var subgroup = recipe.subgroup;
            if (subgroup != null)
            {
                subgroup.flow = Array.Empty<ProductionTableFlow>();
                foreach (var link in subgroup.links)
                {
                    link.flags = 0;
                    link.linkFlow = 0;
                }
                foreach (var sub in subgroup.recipes)
                {
                    ClearDisabledRecipeContents(sub);
                }
            }
        }

        public bool Search(SearchQuery query)
        {
            var hasMatch = false;

            foreach (var recipe in recipes)
            {
                recipe.visible = false;
                if (recipe.subgroup != null && recipe.subgroup.Search(query))
                    goto match;
                if (recipe.recipe.Match(query) || recipe.fuel.Match(query) || recipe.entity.Match(query))
                    goto match;
                foreach (var ingr in recipe.recipe.ingredients)
                {
                    if (ingr.goods.Match(query))
                        goto match;
                }

                foreach (var product in recipe.recipe.products)
                {
                    if (product.goods.Match(query))
                        goto match;
                }
                continue; // no match;
                match:
                hasMatch = true;
                recipe.visible = true;
            }

            if (hasMatch)
                return true;
            foreach (var link in links)
            {
                if (link.goods.Match(query))
                    return true;
            }

            return false;
        }

        private void AddFlow(RecipeRow recipe, Dictionary<Goods, (double prod, double cons)> summer)
        {
            foreach (var product in recipe.recipe.products)
            {
                summer.TryGetValue(product.goods, out var prev);
                var amount = recipe.recipesPerSecond * product.GetAmount(recipe.parameters.productivity);
                prev.prod += amount;
                summer[product.goods] = prev;
            }

            for (var i = 0; i < recipe.recipe.ingredients.Length; i++)
            {
                var ingredient = recipe.recipe.ingredients[i];
                var linkedGoods = recipe.links.ingredientGoods[i];
                summer.TryGetValue(linkedGoods, out var prev);
                prev.cons += recipe.recipesPerSecond * ingredient.amount;
                summer[linkedGoods] = prev;
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
            var flowDict = new Dictionary<Goods, (double prod, double cons)>();
            if (include != null)
                AddFlow(include, flowDict);
            foreach (var recipe in recipes)
            {
                if (!recipe.enabled)
                    continue;
                if (recipe.subgroup != null)
                {
                    recipe.subgroup.CalculateFlow(recipe);
                    foreach (var elem in recipe.subgroup.flow)
                    {
                        flowDict.TryGetValue(elem.goods, out var prev);
                        if (elem.amount > 0f)
                        {
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
                (double prod, double cons) flowParams;
                if (!link.flags.HasFlagAny(ProductionLink.Flags.LinkNotMatched))
                    flowDict.Remove(link.goods, out flowParams);
                else
                {
                    flowDict.TryGetValue(link.goods, out flowParams);
                    if (Math.Abs(flowParams.prod - flowParams.cons) > 1e-8f && link.owner.owner is RecipeRow recipe && recipe.owner.FindLink(link.goods, out var parent))
                        parent.flags |= ProductionLink.Flags.ChildNotMatched | ProductionLink.Flags.LinkNotMatched;
                }
                link.linkFlow = (float)flowParams.prod;
            }

            var flowArr = new ProductionTableFlow[flowDict.Count];
            var index = 0;
            foreach (var (k, (prod, cons)) in flowDict)
            {
                FindLink(k, out var link);
                flowArr[index++] = new ProductionTableFlow(k, (float)(prod - cons), link);
            }
            Array.Sort(flowArr, 0, flowArr.Length, this);
            flow = flowArr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddLinkCoef(Constraint cst, Variable var, ProductionLink link, RecipeRow recipe, float amount)
        {
            if (link.lastRecipe == recipe.recipe)
                amount += (float)cst.GetCoefficient(var);
            else link.capturedRecipes.Add(recipe);
            link.lastRecipe = recipe.recipe;
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
                recipe.parameters.CalculateParameters(recipe.recipe, recipe.entity, recipe.fuel, recipe.variants, recipe); 
                var variable = solver.MakeNumVar(0f, double.PositiveInfinity, recipe.recipe.name);
                if (recipe.fixedBuildings > 0f)
                {
                    var fixedRps = (double)recipe.fixedBuildings / recipe.parameters.recipeTime;
                    variable.SetBounds(fixedRps, fixedRps);
                }
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
                var links = recipe.links;
                
                for (var j = 0; j < recipe.recipe.products.Length; j++)
                {
                    var product = recipe.recipe.products[j];
                    if (product.amount <= 0f)
                        continue;
                    if (recipe.FindLink(product.goods, out var link))
                    {
                        link.flags |= ProductionLink.Flags.HasProduction;
                        var added = product.GetAmount(recipe.parameters.productivity);
                        AddLinkCoef(constraints[link.solverIndex], recipeVar, link, recipe, added);
                        var cost = product.goods.Cost();
                        if (cost > 0f)
                            objCoefs[i] += added * cost;
                    }

                    links.products[j] = link;
                }

                for (var j = 0; j < recipe.recipe.ingredients.Length; j++)
                {
                    var ingredient = recipe.recipe.ingredients[j];
                    var option = ingredient.variants == null ? ingredient.goods : recipe.GetVariant(ingredient.variants);
                    if (recipe.FindLink(option, out var link))
                    {
                        link.flags |= ProductionLink.Flags.HasConsumption;
                        AddLinkCoef(constraints[link.solverIndex], recipeVar, link, recipe, -ingredient.amount);
                    }

                    links.ingredients[j] = link;
                    links.ingredientGoods[j] = option;
                }
                
                links.fuel = links.spentFuel = null;

                if (recipe.fuel != null)
                {
                    var fuelAmount = recipe.parameters.fuelUsagePerSecondPerRecipe;
                    if (recipe.FindLink(recipe.fuel, out var link))
                    {
                        links.fuel = link;
                        link.flags |= ProductionLink.Flags.HasConsumption;
                        AddLinkCoef(constraints[link.solverIndex], recipeVar, link, recipe, -fuelAmount);
                    }

                    if (recipe.fuel.HasSpentFuel(out var spentFuel) && recipe.FindLink(spentFuel, out link))
                    {
                        links.spentFuel = link;
                        link.flags |= ProductionLink.Flags.HasProduction;
                        AddLinkCoef(constraints[link.solverIndex], recipeVar, link, recipe, fuelAmount);
                        if (spentFuel.Cost() > 0f)
                            objCoefs[i] += fuelAmount * spentFuel.Cost();
                    }
                }

                recipe.links = links;
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
                
                Console.WriteLine("Solver finished with result "+result);
                await Ui.EnterMainThread();
                
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
                    solver.Dispose();
                    if (result == Solver.ResultStatus.INFEASIBLE)
                        return "YAFC tried to solve this model and failed. It then tried to find a deadlock loop, but failed again";
                    if (result == Solver.ResultStatus.ABNORMAL)
                        return "This model has numerical errors (probably too small or too large numbers) and cannot be solved";
                    return "Unaccounted error: MODEL_" + result;
                }
            }
                
            for (var i = 0; i < allLinks.Count; i++)
            {
                var link = allLinks[i];
                var constraint = constraints[i];
                link.dualValue = (float)constraint.DualValue();
                if (constraint == null)
                    continue;
                var basisStatus = constraint.BasisStatus();
                if ((basisStatus == Solver.BasisStatus.BASIC || basisStatus == Solver.BasisStatus.FREE) && (link.notMatchedFlow != 0 || link.algorithm != LinkAlgorithm.Match))
                {
                    link.flags |= ProductionLink.Flags.LinkNotMatched;
                }

            }
                
            for (var i = 0; i < allRecipes.Count; i++)
            {
                var recipe = allRecipes[i];
                recipe.recipesPerSecond = vars[i].SolutionValue();
            }

            CalculateFlow(null);
            solver.Dispose();
            return null;
        }

        private void FindAllRecipeLinks(RecipeRow recipe, List<ProductionLink> sources, List<ProductionLink> targets)
        {
            sources.Clear();
            targets.Clear();
            foreach (var link in recipe.links.products)
                if (link != null)
                    targets.Add(link);
            foreach (var link in recipe.links.ingredients)
                if (link != null)
                    sources.Add(link);
            if (recipe.links.fuel != null)
                sources.Add(recipe.links.fuel);
            if (recipe.links.spentFuel != null)
                targets.Add(recipe.links.spentFuel);
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
    }
}