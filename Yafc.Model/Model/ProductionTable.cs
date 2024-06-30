using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Google.OrTools.LinearSolver;
using Yafc.UI;

namespace Yafc.Model {
    public struct ProductionTableFlow(Goods goods, float amount, ProductionLink? link) {
        public Goods goods = goods;
        public float amount = amount;
        public ProductionLink? link = link;
    }

    public class ProductionTable : ProjectPageContents, IComparer<ProductionTableFlow>, IElementGroup<RecipeRow> {
        [SkipSerialization] public Dictionary<Goods, ProductionLink> linkMap { get; } = [];
        List<RecipeRow> IElementGroup<RecipeRow>.elements => recipes;
        [NoUndo]
        public bool expanded { get; set; } = true;
        public List<ProductionLink> links { get; } = [];
        public List<RecipeRow> recipes { get; } = [];
        public ProductionTableFlow[] flow { get; private set; } = [];
        public ModuleFillerParameters? modules { get; set; }
        public bool containsDesiredProducts { get; private set; }

        public ProductionTable(ModelObject owner) : base(owner) {
            if (owner is ProjectPage) {
                modules = new ModuleFillerParameters(this);
            }
        }

        protected internal override void ThisChanged(bool visualOnly) {
            RebuildLinkMap();
            if (owner is ProjectPage page) {
                page.ContentChanged(visualOnly);
            }
            else if (owner is RecipeRow recipe) {
                recipe.ThisChanged(visualOnly);
            }
        }

        public void RebuildLinkMap() {
            linkMap.Clear();
            foreach (var link in links) {
                linkMap[link.goods] = link;
            }
        }

        private void Setup(List<RecipeRow> allRecipes, List<ProductionLink> allLinks) {
            containsDesiredProducts = false;
            foreach (var link in links) {
                if (link.amount != 0f) {
                    containsDesiredProducts = true;
                }

                allLinks.Add(link);
                link.capturedRecipes.Clear();
            }

            foreach (var recipe in recipes) {
                if (!recipe.enabled) {
                    ClearDisabledRecipeContents(recipe);
                    continue;
                }

                recipe.hierarchyEnabled = true;
                allRecipes.Add(recipe);
                recipe.subgroup?.Setup(allRecipes, allLinks);
            }
        }

        private static void ClearDisabledRecipeContents(RecipeRow recipe) {
            recipe.recipesPerSecond = 0;
            recipe.parameters.Clear();
            recipe.hierarchyEnabled = false;
            var subgroup = recipe.subgroup;
            if (subgroup != null) {
                subgroup.flow = [];
                foreach (var link in subgroup.links) {
                    link.flags = 0;
                    link.linkFlow = 0;
                }
                foreach (var sub in subgroup.recipes) {
                    ClearDisabledRecipeContents(sub);
                }
            }
        }

        public bool Search(SearchQuery query) {
            bool hasMatch = false;

            foreach (var recipe in recipes) {
                recipe.visible = false;
                if (recipe.subgroup != null && recipe.subgroup.Search(query)) {
                    goto match;
                }

                if (recipe.recipe.Match(query) || recipe.fuel.Match(query) || recipe.entity.Match(query)) {
                    goto match;
                }

                foreach (var ingr in recipe.recipe.ingredients) {
                    if (ingr.goods.Match(query)) {
                        goto match;
                    }
                }

                foreach (var product in recipe.recipe.products) {
                    if (product.goods.Match(query)) {
                        goto match;
                    }
                }
                continue; // no match;
match:
                hasMatch = true;
                recipe.visible = true;
            }

            if (hasMatch) {
                return true;
            }

            foreach (var link in links) {
                if (link.goods.Match(query)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Add a recipe to this table, and configure the recipe's crafter and fuel to reasonable values.
        /// </summary>
        /// <param name="recipe">The recipe to add.</param>
        /// <param name="ingredientVariantComparer">If not <see langword="null"/>, the comparer to use when deciding which fluid variants to use.</param>
        /// <param name="selectedFuel">If not <see langword="null"/>, this method will select a crafter that can use this fuel, assuming such an entity exists.
        /// For example, if the selected fuel is coal, the recipe will be configured with a burner assembler if any are available.</param>
        public void AddRecipe(Recipe recipe, IComparer<Goods>? ingredientVariantComparer = null, Goods? selectedFuel = null) {
            RecipeRow recipeRow = new RecipeRow(this, recipe);
            this.RecordUndo().recipes.Add(recipeRow);
            EntityCrafter? selectedFuelCrafter = selectedFuel?.fuelFor.OfType<EntityCrafter>().Where(e => e.recipes.OfType<Recipe>().Contains(recipe)).AutoSelect(DataUtils.FavoriteCrafter);
            recipeRow.entity = selectedFuelCrafter ?? recipe.crafters.AutoSelect(DataUtils.FavoriteCrafter);
            if (recipeRow.entity != null) {
                recipeRow.fuel = recipeRow.entity.energy.fuels.FirstOrDefault(e => e == selectedFuel) ?? recipeRow.entity.energy.fuels.AutoSelect(DataUtils.FavoriteFuel);
            }

            foreach (Ingredient ingredient in recipeRow.recipe.ingredients) {
                if (ingredient.variants != null) {
                    _ = recipeRow.variants.Add(ingredient.variants.AutoSelect(ingredientVariantComparer)!); // null-forgiving: variants is never empty, and AutoSelect never returns null from a non-empty collection (of non-null items).
                }
            }
        }

        /// <summary>
        /// Get all <see cref="RecipeRow"/>s contained in this <see cref="ProductionTable"/>, in a depth-first ordering. (The same as in the UI when all nested tables are expanded.)
        /// </summary>
        public IEnumerable<RecipeRow> GetAllRecipes() {
            return flatten(recipes);

            static IEnumerable<RecipeRow> flatten(IEnumerable<RecipeRow> rows) {
                foreach (var row in rows) {
                    yield return row;
                    if (row.subgroup is not null) {
                        foreach (var row2 in flatten(row.subgroup.GetAllRecipes())) {
                            yield return row2;
                        }
                    }
                }
            }
        }

        private static void AddFlow(RecipeRow recipe, Dictionary<Goods, (double prod, double cons)> summer) {
            foreach (var product in recipe.recipe.products) {
                _ = summer.TryGetValue(product.goods, out var prev);
                double amount = recipe.recipesPerSecond * product.GetAmount(recipe.parameters.productivity);
                prev.prod += amount;
                summer[product.goods] = prev;
            }

            for (int i = 0; i < recipe.recipe.ingredients.Length; i++) {
                var ingredient = recipe.recipe.ingredients[i];
                var linkedGoods = recipe.links.ingredientGoods[i];
                _ = summer.TryGetValue(linkedGoods, out var prev);
                prev.cons += recipe.recipesPerSecond * ingredient.amount;
                summer[linkedGoods] = prev;
            }

            if (recipe.fuel != null && !float.IsNaN(recipe.parameters.fuelUsagePerSecondPerBuilding)) {
                _ = summer.TryGetValue(recipe.fuel, out var prev);
                double fuelUsage = recipe.parameters.fuelUsagePerSecondPerRecipe * recipe.recipesPerSecond;
                prev.cons += fuelUsage;
                summer[recipe.fuel] = prev;
                if (recipe.fuel.HasSpentFuel(out var spentFuel)) {
                    _ = summer.TryGetValue(spentFuel, out prev);
                    prev.prod += fuelUsage;
                    summer[spentFuel] = prev;
                }
            }
        }

        private void CalculateFlow(RecipeRow? include) {
            Dictionary<Goods, (double prod, double cons)> flowDict = [];
            if (include != null) {
                AddFlow(include, flowDict);
            }

            foreach (var recipe in recipes) {
                if (!recipe.enabled) {
                    continue;
                }

                if (recipe.subgroup != null) {
                    recipe.subgroup.CalculateFlow(recipe);
                    foreach (var elem in recipe.subgroup.flow) {
                        _ = flowDict.TryGetValue(elem.goods, out var prev);
                        if (elem.amount > 0f) {
                            prev.prod += elem.amount;
                        }
                        else {
                            prev.cons -= elem.amount;
                        }

                        flowDict[elem.goods] = prev;
                    }
                }
                else {
                    AddFlow(recipe, flowDict);
                }
            }

            foreach (ProductionLink link in links) {
                (double prod, double cons) flowParams;
                if (!link.flags.HasFlagAny(ProductionLink.Flags.LinkNotMatched)) {
                    _ = flowDict.Remove(link.goods, out flowParams);
                }
                else {
                    _ = flowDict.TryGetValue(link.goods, out flowParams);
                    if (Math.Abs(flowParams.prod - flowParams.cons) > 1e-8f && link.owner.owner is RecipeRow recipe && recipe.owner.FindLink(link.goods, out var parent)) {
                        parent.flags |= ProductionLink.Flags.ChildNotMatched | ProductionLink.Flags.LinkNotMatched;
                    }
                }
                link.linkFlow = (float)flowParams.prod;
            }

            ProductionTableFlow[] flowArr = new ProductionTableFlow[flowDict.Count];
            int index = 0;
            foreach (var (k, (prod, cons)) in flowDict) {
                _ = FindLink(k, out var link);
                flowArr[index++] = new ProductionTableFlow(k, (float)(prod - cons), link);
            }
            Array.Sort(flowArr, 0, flowArr.Length, this);
            flow = flowArr;
        }

        /// <summary>
        /// Add/update the variable value for the constraint with the given amount, and store the recipe to the production link.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddLinkCoef(Constraint cst, Variable var, ProductionLink link, RecipeRow recipe, float amount) {
            // GetCoefficient will return 0 when the variable is not available in the constraint
            amount += (float)cst.GetCoefficient(var);
            link.capturedRecipes.Add(recipe);
            cst.SetCoefficient(var, amount);
        }

        public override async Task<string?> Solve(ProjectPage page) {
            using var productionTableSolver = DataUtils.CreateSolver();
            var objective = productionTableSolver.Objective();
            objective.SetMinimization();
            List<RecipeRow> allRecipes = [];
            List<ProductionLink> allLinks = [];
            Setup(allRecipes, allLinks);
            Variable[] vars = new Variable[allRecipes.Count];
            float[] objCoefs = new float[allRecipes.Count];

            for (int i = 0; i < allRecipes.Count; i++) {
                var recipe = allRecipes[i];
                recipe.parameters.CalculateParameters(recipe.recipe, recipe.entity, recipe.fuel, recipe.variants, recipe);
                var variable = productionTableSolver.MakeNumVar(0f, double.PositiveInfinity, recipe.recipe.name);
                if (recipe.fixedBuildings > 0f) {
                    double fixedRps = (double)recipe.fixedBuildings / recipe.parameters.recipeTime;
                    variable.SetBounds(fixedRps, fixedRps);
                }
                vars[i] = variable;
            }

            Constraint[] constraints = new Constraint[allLinks.Count];
            for (int i = 0; i < allLinks.Count; i++) {
                var link = allLinks[i];
                float min = link.algorithm == LinkAlgorithm.AllowOverConsumption ? float.NegativeInfinity : link.amount;
                float max = link.algorithm == LinkAlgorithm.AllowOverProduction ? float.PositiveInfinity : link.amount;
                var constraint = productionTableSolver.MakeConstraint(min, max, link.goods.name + "_recipe");
                constraints[i] = constraint;
                link.solverIndex = i;
                link.flags = link.amount > 0 ? ProductionLink.Flags.HasConsumption : link.amount < 0 ? ProductionLink.Flags.HasProduction : 0;
            }

            for (int i = 0; i < allRecipes.Count; i++) {
                var recipe = allRecipes[i];
                var recipeVar = vars[i];
                var links = recipe.links;

                for (int j = 0; j < recipe.recipe.products.Length; j++) {
                    var product = recipe.recipe.products[j];
                    if (product.amount <= 0f) {
                        continue;
                    }

                    if (recipe.FindLink(product.goods, out var link)) {
                        link.flags |= ProductionLink.Flags.HasProduction;
                        float added = product.GetAmount(recipe.parameters.productivity);
                        AddLinkCoef(constraints[link.solverIndex], recipeVar, link, recipe, added);
                        float cost = product.goods.Cost();
                        if (cost > 0f) {
                            objCoefs[i] += added * cost;
                        }
                    }

                    links.products[j] = link;
                }

                for (int j = 0; j < recipe.recipe.ingredients.Length; j++) {
                    var ingredient = recipe.recipe.ingredients[j];
                    var option = ingredient.variants == null ? ingredient.goods : recipe.GetVariant(ingredient.variants);
                    if (recipe.FindLink(option, out var link)) {
                        link.flags |= ProductionLink.Flags.HasConsumption;
                        AddLinkCoef(constraints[link.solverIndex], recipeVar, link, recipe, -ingredient.amount);
                    }

                    links.ingredients[j] = link;
                    links.ingredientGoods[j] = option;
                }

                links.fuel = links.spentFuel = null;

                if (recipe.fuel != null) {
                    float fuelAmount = recipe.parameters.fuelUsagePerSecondPerRecipe;
                    if (recipe.FindLink(recipe.fuel, out var link)) {
                        links.fuel = link;
                        link.flags |= ProductionLink.Flags.HasConsumption;
                        AddLinkCoef(constraints[link.solverIndex], recipeVar, link, recipe, -fuelAmount);
                    }

                    if (recipe.fuel.HasSpentFuel(out var spentFuel) && recipe.FindLink(spentFuel, out link)) {
                        links.spentFuel = link;
                        link.flags |= ProductionLink.Flags.HasProduction;
                        AddLinkCoef(constraints[link.solverIndex], recipeVar, link, recipe, fuelAmount);
                        if (spentFuel.Cost() > 0f) {
                            objCoefs[i] += fuelAmount * spentFuel.Cost();
                        }
                    }
                }

                recipe.links = links;
            }

            foreach (var link in allLinks) {
                link.notMatchedFlow = 0f;
                if (!link.flags.HasFlags(ProductionLink.Flags.HasProductionAndConsumption)) {
                    if (!link.flags.HasFlagAny(ProductionLink.Flags.HasProductionAndConsumption) && !link.owner.HasDisabledRecipeReferencing(link.goods)) {
                        _ = link.owner.RecordUndo(true).links.Remove(link);
                    }

                    link.flags |= ProductionLink.Flags.LinkNotMatched;
                    constraints[link.solverIndex].SetBounds(double.NegativeInfinity, double.PositiveInfinity); // remove link constraints
                }
            }

            await Ui.ExitMainThread();
            for (int i = 0; i < allRecipes.Count; i++) {
                objective.SetCoefficient(vars[i], allRecipes[i].recipe.RecipeBaseCost());
            }

            var result = productionTableSolver.Solve();
            if (result is not Solver.ResultStatus.FEASIBLE and not Solver.ResultStatus.OPTIMAL) {
                objective.Clear();
                var (deadlocks, splits) = GetInfeasibilityCandidates(allRecipes);
                (Variable? positive, Variable? negative)[] slackVars = new (Variable? positive, Variable? negative)[allLinks.Count];
                // Solution does not exist. Adding slack variables to find the reason
                foreach (var link in deadlocks) {
                    // Adding negative slack to possible deadlocks (loops)
                    var constraint = constraints[link.solverIndex];
                    float cost = MathF.Abs(link.goods.Cost());
                    var negativeSlack = productionTableSolver.MakeNumVar(0d, double.PositiveInfinity, "negative-slack." + link.goods.name);
                    constraint.SetCoefficient(negativeSlack, cost);
                    objective.SetCoefficient(negativeSlack, 1f);
                    slackVars[link.solverIndex].negative = negativeSlack;
                }

                foreach (var link in splits) {
                    // Adding positive slack to splits
                    float cost = MathF.Abs(link.goods.Cost());
                    var constraint = constraints[link.solverIndex];
                    var positiveSlack = productionTableSolver.MakeNumVar(0d, double.PositiveInfinity, "positive-slack." + link.goods.name);
                    constraint.SetCoefficient(positiveSlack, -cost);
                    objective.SetCoefficient(positiveSlack, 1f);
                    slackVars[link.solverIndex].positive = positiveSlack;
                }

                result = productionTableSolver.Solve();

                Console.WriteLine("Solver finished with result " + result);
                await Ui.EnterMainThread();

                if (result is Solver.ResultStatus.OPTIMAL or Solver.ResultStatus.FEASIBLE) {
                    List<ProductionLink> linkList = [];
                    for (int i = 0; i < allLinks.Count; i++) {
                        var (posSlack, negSlack) = slackVars[i];
                        if (posSlack is not null && posSlack.BasisStatus() != Solver.BasisStatus.AT_LOWER_BOUND) {
                            linkList.Add(allLinks[i]);
                            allLinks[i].notMatchedFlow += (float)posSlack.SolutionValue();
                        }

                        if (negSlack is not null && negSlack.BasisStatus() != Solver.BasisStatus.AT_LOWER_BOUND) {
                            linkList.Add(allLinks[i]);
                            allLinks[i].notMatchedFlow -= (float)negSlack.SolutionValue();
                        }
                    }

                    foreach (var link in linkList) {
                        if (link.notMatchedFlow == 0f) {
                            continue;
                        }

                        link.flags |= ProductionLink.Flags.LinkNotMatched | ProductionLink.Flags.LinkRecursiveNotMatched;
                        RecipeRow? ownerRecipe = link.owner.owner as RecipeRow;
                        while (ownerRecipe != null) {
                            if (link.notMatchedFlow > 0f) {
                                ownerRecipe.parameters.warningFlags |= WarningFlags.OverproductionRequired;
                            }
                            else {
                                ownerRecipe.parameters.warningFlags |= WarningFlags.DeadlockCandidate;
                            }

                            ownerRecipe = ownerRecipe.owner.owner as RecipeRow;
                        }
                    }

                    foreach (var recipe in allRecipes) {
                        FindAllRecipeLinks(recipe, linkList, linkList);
                        foreach (var link in linkList) {
                            if (link.flags.HasFlags(ProductionLink.Flags.LinkRecursiveNotMatched)) {
                                if (link.notMatchedFlow > 0f) {
                                    recipe.parameters.warningFlags |= WarningFlags.OverproductionRequired;
                                }
                                else {
                                    recipe.parameters.warningFlags |= WarningFlags.DeadlockCandidate;
                                }
                            }
                        }
                    }
                }
                else {
                    if (result == Solver.ResultStatus.INFEASIBLE) {
                        return "YAFC failed to solve the model and to find deadlock loops. As a result, the model was not updated.";
                    }

                    if (result == Solver.ResultStatus.ABNORMAL) {
                        return "This model has numerical errors (probably too small or too large numbers) and cannot be solved";
                    }

                    return "Unaccounted error: MODEL_" + result;
                }
            }

            for (int i = 0; i < allLinks.Count; i++) {
                var link = allLinks[i];
                var constraint = constraints[i];
                link.dualValue = (float)constraint.DualValue();
                if (constraint == null) {
                    continue;
                }

                var basisStatus = constraint.BasisStatus();
                if ((basisStatus == Solver.BasisStatus.BASIC || basisStatus == Solver.BasisStatus.FREE) && (link.notMatchedFlow != 0 || link.algorithm != LinkAlgorithm.Match)) {
                    link.flags |= ProductionLink.Flags.LinkNotMatched;
                }

            }

            for (int i = 0; i < allRecipes.Count; i++) {
                var recipe = allRecipes[i];
                recipe.recipesPerSecond = vars[i].SolutionValue();
            }

            bool builtCountExceeded = CheckBuiltCountExceeded();

            CalculateFlow(null);
            return builtCountExceeded ? "This model requires more buildings than are currently built" : null;
        }

        /// <summary>
        /// Search the disabled recipes in this table and see if any of them produce or consume <paramref name="goods"/>. If they do, the corresponding <see cref="ProductionLink"/> should not be deleted.
        /// </summary>
        /// <param name="goods">The <see cref="Goods"/> that might have its link removed.</param>
        /// <returns><see langword="true"/> if the link should be preserved, or <see langword="false"/> if it is ok to delete the link.</returns>
        private bool HasDisabledRecipeReferencing(Goods goods)
            => GetAllRecipes().Any(row => !row.hierarchyEnabled && row.recipe.ingredients.Any(i => i.goods == goods) || row.recipe.products.Any(p => p.goods == goods) || row.fuel == goods);

        private bool CheckBuiltCountExceeded() {
            bool builtCountExceeded = false;
            for (int i = 0; i < recipes.Count; i++) {
                var recipe = recipes[i];
                if (recipe.buildingCount > recipe.builtBuildings) {
                    recipe.parameters.warningFlags |= WarningFlags.ExceedsBuiltCount;
                    builtCountExceeded = true;
                }
                else if (recipe.subgroup != null) {
                    if (recipe.subgroup.CheckBuiltCountExceeded()) {
                        recipe.parameters.warningFlags |= WarningFlags.ExceedsBuiltCount;
                        builtCountExceeded = true;
                    }
                }
            }

            return builtCountExceeded;
        }

        private static void FindAllRecipeLinks(RecipeRow recipe, List<ProductionLink> sources, List<ProductionLink> targets) {
            sources.Clear();
            targets.Clear();
            foreach (var link in recipe.links.products) {
                if (link != null) {
                    targets.Add(link);
                }
            }

            foreach (var link in recipe.links.ingredients) {
                if (link != null) {
                    sources.Add(link);
                }
            }

            if (recipe.links.fuel != null) {
                sources.Add(recipe.links.fuel);
            }

            if (recipe.links.spentFuel != null) {
                targets.Add(recipe.links.spentFuel);
            }
        }

        private static (List<ProductionLink> merges, List<ProductionLink> splits) GetInfeasibilityCandidates(List<RecipeRow> recipes) {
            Graph<ProductionLink> graph = new Graph<ProductionLink>();
            List<ProductionLink> sources = [];
            List<ProductionLink> targets = [];
            List<ProductionLink> splits = [];

            foreach (var recipe in recipes) {
                FindAllRecipeLinks(recipe, sources, targets);
                foreach (var src in sources) {
                    foreach (var tgt in targets) {
                        graph.Connect(src, tgt);
                    }
                }

                if (targets.Count > 1) {
                    splits.AddRange(targets);
                }
            }

            var loops = graph.MergeStrongConnectedComponents();
            sources.Clear();
            foreach (var possibleLoop in loops) {
                if (possibleLoop.userData.list != null) {
                    var list = possibleLoop.userData.list;
                    var last = list[^1];
                    sources.Add(last);
                    for (int i = 0; i < list.Length - 1; i++) {
                        for (int j = i + 2; j < list.Length; j++) {
                            if (graph.HasConnection(list[i], list[j])) {
                                sources.Add(list[i]);
                                break;
                            }
                        }
                    }
                }
            }

            return (sources, splits);
        }

        public bool FindLink(Goods goods, [MaybeNullWhen(false)] out ProductionLink link) {
            if (goods == null) {
                link = null;
                return false;
            }
            var searchFrom = this;
            while (true) {
                if (searchFrom.linkMap.TryGetValue(goods, out link)) {
                    return true;
                }

                if (searchFrom.owner is RecipeRow row) {
                    searchFrom = row.owner;
                }
                else {
                    return false;
                }
            }
        }

        public int Compare(ProductionTableFlow x, ProductionTableFlow y) {
            float amt1 = x.goods.fluid != null ? x.amount / 50f : x.amount;
            float amt2 = y.goods.fluid != null ? y.amount / 50f : y.amount;
            return amt1.CompareTo(amt2);
        }
    }
}

