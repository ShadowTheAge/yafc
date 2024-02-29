using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.OrTools.LinearSolver;
using YAFC.UI;

namespace YAFC.Model {
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
        public HashSet<Recipe> downstream = new HashSet<Recipe>();
        public HashSet<Recipe> upstream = new HashSet<Recipe>();
    }

    public class AutoPlanner : ProjectPageContents {
        public AutoPlanner(ModelObject page) : base(page) { }

        public List<AutoPlannerGoal> goals { get; } = new List<AutoPlannerGoal>();
        public HashSet<Recipe> done { get; } = new HashSet<Recipe>();
        public HashSet<Goods> roots { get; } = new HashSet<Goods>();

        public AutoPlannerRecipe[][] tiers { get; private set; }

        public override async Task<string> Solve(ProjectPage page) {
            var processedGoods = Database.goods.CreateMapping<Constraint>();
            var processedRecipes = Database.recipes.CreateMapping<Variable>();
            var processingStack = new Queue<Goods>();
            var solver = DataUtils.CreateSolver("BestFlowSolver");
            var rootConstraint = solver.MakeConstraint();
            foreach (var root in roots)
                processedGoods[root] = rootConstraint;
            foreach (var goal in goals) {
                processedGoods[goal.item] = solver.MakeConstraint(goal.amount, double.PositiveInfinity, goal.item.name);
                processingStack.Enqueue(goal.item);
            }

            await Ui.ExitMainThread();
            var objective = solver.Objective();
            objective.SetMinimization();
            processingStack.Enqueue(null); // depth marker;
            var depth = 0;

            var allRecipes = new List<Recipe>();
            while (processingStack.Count > 1) {
                var item = processingStack.Dequeue();
                if (item == null) {
                    processingStack.Enqueue(null);
                    depth++;
                    continue;
                }

                var constraint = processedGoods[item];
                foreach (var recipe in item.production) {
                    if (!recipe.IsAccessibleWithCurrentMilestones())
                        continue;
                    if (processedRecipes[recipe] is Variable var) {
                        constraint.SetCoefficient(var, constraint.GetCoefficient(var) + recipe.GetProduction(item));
                    }
                    else {
                        allRecipes.Add(recipe);
                        var = solver.MakeNumVar(0, double.PositiveInfinity, recipe.name);
                        objective.SetCoefficient(var, recipe.RecipeBaseCost() * (1 + depth * 0.5));
                        processedRecipes[recipe] = var;

                        foreach (var product in recipe.products) {
                            if (processedGoods[product.goods] is Constraint constr && !processingStack.Contains(product.goods))
                                constr.SetCoefficient(var, constr.GetCoefficient(var) + product.amount);
                        }

                        foreach (var ingredient in recipe.ingredients) {
                            var proc = processedGoods[ingredient.goods];
                            if (proc == rootConstraint)
                                continue;
                            if (processedGoods[ingredient.goods] is Constraint constr)
                                constr.SetCoefficient(var, constr.GetCoefficient(var) - ingredient.amount);
                            else {
                                constr = solver.MakeConstraint(0, double.PositiveInfinity, ingredient.goods.name);
                                processedGoods[ingredient.goods] = constr;
                                processingStack.Enqueue(ingredient.goods);
                                constr.SetCoefficient(var, -ingredient.amount);
                            }
                        }
                    }
                }
            }

            var solverResult = solver.Solve();
            Console.WriteLine("Solution completed with result " + solverResult);
            if (solverResult != Solver.ResultStatus.OPTIMAL && solverResult != Solver.ResultStatus.FEASIBLE) {
                Console.WriteLine(solver.ExportModelAsLpFormat(false));
                this.tiers = null;
                return "Model have no solution";
            }

            var graph = new Graph<Recipe>();
            allRecipes.RemoveAll(x => {
                if (processedRecipes[x] is not Variable variable)
                    return true;
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
                set.Add(item);
                set.UnionWith(subset);
            });
            var downstream = new Dictionary<Recipe, HashSet<Recipe>>();
            var upstream = new Dictionary<Recipe, HashSet<Recipe>>();
            foreach (var ((single, list), dependencies) in allDependencies) {
                var deps = new HashSet<Recipe>();
                foreach (var (singleDep, listDep) in dependencies) {
                    var elem = singleDep;
                    if (listDep != null) {
                        deps.UnionWith(listDep);
                        elem = listDep[0];
                    }
                    else deps.Add(singleDep);

                    if (!upstream.TryGetValue(elem, out var set)) {
                        set = new HashSet<Recipe>();
                        if (listDep != null) {
                            foreach (var recipe in listDep)
                                upstream[recipe] = set;
                        }
                        else upstream[singleDep] = set;
                    }

                    if (list != null)
                        set.UnionWith(list);
                    else set.Add(single);
                }

                if (list != null) {
                    foreach (var recipe in list)
                        downstream[recipe] = deps;
                }
                else downstream[single] = deps;
            }

            var remainingNodes = new HashSet<(Recipe, Recipe[])>(subgraph.Select(x => x.userdata));
            var nodesToClear = new List<(Recipe, Recipe[])>();
            var tiers = new List<AutoPlannerRecipe[]>();
            var currentTier = new List<Recipe>();
            while (remainingNodes.Count > 0) {
                currentTier.Clear();
                // First attempt to create tier: Immediately accessible recipe
                foreach (var node in remainingNodes) {
                    if (node.Item2 != null && currentTier.Count > 0)
                        continue;
                    foreach (var dependency in subgraph.GetConnections(node)) {
                        if (dependency.userdata != node && remainingNodes.Contains(dependency.userdata))
                            goto nope;
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
                        if (multiple != null)
                            currentTier.AddRange(multiple);
                        else currentTier.Add(single);
                    }
                    remainingNodes.Clear();
                    Console.WriteLine("Tier creation failure");
                }
                tiers.Add(currentTier.Select(x => new AutoPlannerRecipe {
                    recipe = x,
                    tier = tiers.Count,
                    recipesPerSecond = (float)processedRecipes[x].SolutionValue(),
                    downstream = downstream.TryGetValue(x, out var res) ? res : null,
                    upstream = upstream.TryGetValue(x, out var res2) ? res2 : null
                }).ToArray());
            }
            solver.Dispose();
            await Ui.EnterMainThread();

            this.tiers = tiers.ToArray();
            return null;
        }

    }
}
