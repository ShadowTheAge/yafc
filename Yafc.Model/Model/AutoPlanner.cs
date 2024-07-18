using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.OrTools.LinearSolver;
using Serilog;
using Yafc.UI;

#nullable disable warnings // Disabling nullable in legacy code.

namespace Yafc.Model {
    [Serializable]
    public class AutoPlannerGoal {
        private Goods _item;
        public Goods item {
            get => _item;
            set => _item = value ?? throw new ArgumentNullException(nameof(value), "Auto planner goal no longer exist");
        }
        public float amount { get; set; }
    }

    public class AutoPlannerRecipe {
        public Recipe recipe;
        public int tier;
        public float recipesPerSecond;
        public HashSet<Recipe> downstream = [];
        public HashSet<Recipe> upstream = [];
    }

    public class AutoPlanner(ModelObject page) : ProjectPageContents(page) {
        private static readonly ILogger logger = Logging.GetLogger<AutoPlanner>();
        public List<AutoPlannerGoal> goals { get; } = [];
        public HashSet<Recipe> done { get; } = [];
        public HashSet<Goods> roots { get; } = [];

        public AutoPlannerRecipe[][] tiers { get; private set; }

        public override async Task<string> Solve(ProjectPage page) {
            var processedGoods = Database.goods.CreateMapping<Constraint>();
            var processedRecipes = Database.recipes.CreateMapping<Variable>();
            Queue<Goods> processingStack = new Queue<Goods>();
            var bestFlowSolver = DataUtils.CreateSolver();
            var rootConstraint = bestFlowSolver.MakeConstraint();
            foreach (var root in roots) {
                processedGoods[root] = rootConstraint;
            }

            foreach (var goal in goals) {
                processedGoods[goal.item] = bestFlowSolver.MakeConstraint(goal.amount, double.PositiveInfinity, goal.item.name);
                processingStack.Enqueue(goal.item);
            }

            await Ui.ExitMainThread();
            var objective = bestFlowSolver.Objective();
            objective.SetMinimization();
            processingStack.Enqueue(null); // depth marker;
            int depth = 0;

            List<Recipe> allRecipes = [];
            while (processingStack.Count > 1) {
                var item = processingStack.Dequeue();
                if (item == null) {
                    processingStack.Enqueue(null);
                    depth++;
                    continue;
                }

                var constraint = processedGoods[item];
                foreach (var recipe in item.production) {
                    if (!recipe.IsAccessibleWithCurrentMilestones()) {
                        continue;
                    }

                    if (processedRecipes[recipe] is Variable var) {
                        constraint.SetCoefficient(var, constraint.GetCoefficient(var) + recipe.GetProduction(item));
                    }
                    else {
                        allRecipes.Add(recipe);
                        var = bestFlowSolver.MakeNumVar(0, double.PositiveInfinity, recipe.name);
                        objective.SetCoefficient(var, recipe.RecipeBaseCost() * (1 + (depth * 0.5)));
                        processedRecipes[recipe] = var;

                        foreach (var product in recipe.products) {
                            if (processedGoods[product.goods] is Constraint constr && !processingStack.Contains(product.goods)) {
                                constr.SetCoefficient(var, constr.GetCoefficient(var) + product.amount);
                            }
                        }

                        foreach (var ingredient in recipe.ingredients) {
                            var proc = processedGoods[ingredient.goods];
                            if (proc == rootConstraint) {
                                continue;
                            }

                            if (processedGoods[ingredient.goods] is Constraint constr) {
                                constr.SetCoefficient(var, constr.GetCoefficient(var) - ingredient.amount);
                            }
                            else {
                                constr = bestFlowSolver.MakeConstraint(0, double.PositiveInfinity, ingredient.goods.name);
                                processedGoods[ingredient.goods] = constr;
                                processingStack.Enqueue(ingredient.goods);
                                constr.SetCoefficient(var, -ingredient.amount);
                            }
                        }
                    }
                }
            }

            var solverResult = bestFlowSolver.Solve();
            logger.Information("Solution completed with result {result}", solverResult);
            if (solverResult is not Solver.ResultStatus.OPTIMAL and not Solver.ResultStatus.FEASIBLE) {
                logger.Information(bestFlowSolver.ExportModelAsLpFormat(false));
                this.tiers = null;
                return "Model has no solution";
            }

            Graph<Recipe> graph = new Graph<Recipe>();
            _ = allRecipes.RemoveAll(x => {
                if (processedRecipes[x] is not Variable variable) {
                    return true;
                }

                if (variable.BasisStatus() != Solver.BasisStatus.BASIC || variable.SolutionValue() <= 1e-6d) {
                    processedRecipes[x] = null;
                    return true;
                }
                return false;
            });

            foreach (var recipe in allRecipes) {
                foreach (var ingredient in recipe.ingredients) {
                    foreach (var productionRecipe in ingredient.goods.production) {
                        if (processedRecipes[productionRecipe] != null) {
                            // TODO think about heuristics for selecting first recipe. Now chooses first (essentially random)
                            graph.Connect(recipe, productionRecipe);
                            //break;
                        }
                    }
                }
            }

            var subgraph = graph.MergeStrongConnectedComponents();
            var allDependencies = subgraph.Aggregate(x => new HashSet<(Recipe, Recipe[])>(), (set, item, subset) => {
                _ = set.Add(item);
                set.UnionWith(subset);
            });
            Dictionary<Recipe, HashSet<Recipe>> downstream = [];
            Dictionary<Recipe, HashSet<Recipe>> upstream = [];
            foreach (var ((single, list), dependencies) in allDependencies) {
                HashSet<Recipe> deps = [];
                foreach (var (singleDep, listDep) in dependencies) {
                    var elem = singleDep;
                    if (listDep != null) {
                        deps.UnionWith(listDep);
                        elem = listDep[0];
                    }
                    else {
                        _ = deps.Add(singleDep);
                    }

                    if (!upstream.TryGetValue(elem, out var set)) {
                        set = [];
                        if (listDep != null) {
                            foreach (var recipe in listDep) {
                                upstream[recipe] = set;
                            }
                        }
                        else {
                            upstream[singleDep] = set;
                        }
                    }

                    if (list != null) {
                        set.UnionWith(list);
                    }
                    else {
                        _ = set.Add(single);
                    }
                }

                if (list != null) {
                    foreach (var recipe in list) {
                        downstream[recipe] = deps;
                    }
                }
                else {
                    downstream[single] = deps;
                }
            }

            HashSet<(Recipe, Recipe[])> remainingNodes = new HashSet<(Recipe, Recipe[])>(subgraph.Select(x => x.userData));
            List<(Recipe, Recipe[])> nodesToClear = [];
            List<AutoPlannerRecipe[]> tiers = [];
            List<Recipe> currentTier = [];
            while (remainingNodes.Count > 0) {
                currentTier.Clear();
                // First attempt to create tier: Immediately accessible recipe
                foreach (var node in remainingNodes) {
                    if (node.Item2 != null && currentTier.Count > 0) {
                        continue;
                    }

                    foreach (var dependency in subgraph.GetConnections(node)) {
                        if (dependency.userData != node && remainingNodes.Contains(dependency.userData)) {
                            goto nope;
                        }
                    }

                    nodesToClear.Add(node);
                    if (node.Item2 != null) {
                        currentTier.AddRange(node.Item2);
                        break;
                    }
                    currentTier.Add(node.Item1);
nope:;
                }
                remainingNodes.ExceptWith(nodesToClear);

                if (currentTier.Count == 0) // whoops, give up
                {
                    foreach (var (single, multiple) in remainingNodes) {
                        if (multiple != null) {
                            currentTier.AddRange(multiple);
                        }
                        else {
                            currentTier.Add(single);
                        }
                    }
                    remainingNodes.Clear();
                    logger.Information("Tier creation failure");
                }
                tiers.Add(currentTier.Select(x => new AutoPlannerRecipe {
                    recipe = x,
                    tier = tiers.Count,
                    recipesPerSecond = (float)processedRecipes[x].SolutionValue(),
                    downstream = downstream.TryGetValue(x, out var res) ? res : null,
                    upstream = upstream.TryGetValue(x, out var res2) ? res2 : null
                }).ToArray());
            }
            bestFlowSolver.Dispose();
            await Ui.EnterMainThread();

            this.tiers = [.. tiers];
            return null;
        }

    }
}
