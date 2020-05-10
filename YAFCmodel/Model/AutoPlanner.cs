using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Google.OrTools.LinearSolver;

namespace YAFC.Model
{
    [Serializable]
    public class AutoPlannerGoal
    {
        public Goods item { get; set; }
        public float amount { get; set; }
    }

    public class AutoPlannerRecipe
    {
        public Recipe recipe;
        public int tier;
        public float recipesPerSecond;
        public HashSet<Recipe> downstream = new HashSet<Recipe>();
        public HashSet<Recipe> upstream = new HashSet<Recipe>();
    }
    
    public class AutoPlanner : ProjectPageContents
    {
        public AutoPlanner(ModelObject page) : base(page) {}

        public List<AutoPlannerGoal> goals { get; } = new List<AutoPlannerGoal>();
        public HashSet<Recipe> done { get; } = new HashSet<Recipe>();
        public HashSet<Goods> roots { get; } = new HashSet<Goods>();

        public AutoPlannerRecipe[][] tiers { get; private set; }
        private static readonly object RootMarker = new object();

        public override Task Solve(ProjectPage page)
        {
            var processed = new object[Database.allObjects.Length];
            var processingStack = new Queue<Goods>();
            var solver = DataUtils.CreateSolver("BestFlowSolver");
            foreach (var root in roots)
                processed[root.id] = RootMarker;
            foreach (var goal in goals)
            {
                processed[goal.item.id] = solver.MakeConstraint(1d, double.PositiveInfinity, goal.item.name);
                processingStack.Enqueue(goal.item);
            } 
            var objective = solver.Objective();
            
            while (processingStack.Count > 0)
            {
                var item = processingStack.Dequeue();
                var constraint = processed[item.id] as Constraint;
                foreach (var recipe in item.production)
                {
                    if (!recipe.IsAccessibleWithCurrentMilestones())
                        continue;
                    if (processed[recipe.id] is Variable var)
                    {
                        constraint.SetCoefficient(var, constraint.GetCoefficient(var) + recipe.GetProduction(item));
                    }
                    else
                    {
                        var = solver.MakeNumVar(0d, double.PositiveInfinity, recipe.name);
                        objective.SetCoefficient(var, recipe.RecipeBaseCost());
                        processed[recipe.id] = var;

                        foreach (var product in recipe.products)
                        {
                            if (processed[product.goods.id] is Constraint constr && !processingStack.Contains(product.goods)) 
                                constr.SetCoefficient(var, constr.GetCoefficient(var) + product.amount);
                        }

                        foreach (var ingredient in recipe.ingredients)
                        {
                            var proc = processed[ingredient.goods.id];
                            if (proc == RootMarker)
                                continue;
                            if (processed[ingredient.goods.id] is Constraint constr)
                                constr.SetCoefficient(var, constr.GetCoefficient(var) - ingredient.amount);
                            else
                            {
                                constr = solver.MakeConstraint(0, double.PositiveInfinity, ingredient.goods.name);
                                processed[ingredient.goods.id] = constr;
                                processingStack.Enqueue(ingredient.goods);
                                constr.SetCoefficient(var, -ingredient.amount);
                            }
                        }
                    }
                }
            }
            
            var dependencies = new Dictionary<FactorioObject, HashSet<Recipe>>();
            var usedRecipes = new List<Recipe>();
            foreach (var goal in goals)
                GetObjectDependencies(goal.item);
            HashSet<Recipe> GetObjectDependencies(FactorioObject obj)
            {
                if (dependencies.TryGetValue(obj, out var result))
                    return result;
                var dep = new HashSet<Recipe>();
                dependencies[obj] = dep;
                if (obj is Recipe recipe)
                {
                    usedRecipes.Add(recipe);
                    foreach (var ingr in recipe.ingredients)
                    {
                        if (processed[ingr.goods.id] != RootMarker)
                            dependencies[obj].UnionWith(GetObjectDependencies(ingr.goods));
                    }
                } 
                else if (obj is Goods goods)
                {
                    foreach (var production in goods.production)
                    {
                        if (processed[production.id] is Variable var && var.BasisStatus() != Solver.BasisStatus.AT_LOWER_BOUND)
                        {
                            dependencies[obj].Add(production);
                            dependencies[obj].UnionWith(GetObjectDependencies(production));
                        }
                    }
                }

                return dep;
            }
            
            var tiers = new List<AutoPlannerRecipe[]>();
            var remainingRecipes = new HashSet<Recipe>(usedRecipes);
            var currentTier = new List<Recipe>();
            while (remainingRecipes.Count > 0)
            {
                currentTier.Clear();
                // First attempt to create tier: Immediately accessible recipe
                foreach (var recipe in remainingRecipes)
                {
                    var recipeDeps = dependencies[recipe];
                    if (recipeDeps.Contains(recipe)) // recursive recipe: it is OK to add if non-recursive dependencies are met
                    {
                        foreach (var dependency in recipeDeps)
                        {
                            if (remainingRecipes.Contains(dependency) && !dependencies[dependency].Contains(dependency))
                                goto nope;
                        }
                        currentTier.Add(recipe);
                        nope: ;
                    } 
                    else if (!dependencies[recipe].Overlaps(remainingRecipes))
                        currentTier.Add(recipe);
                }

                if (currentTier.Count == 0) // whoops, give up
                {
                    currentTier.AddRange(remainingRecipes);
                    Console.WriteLine("Tier creation failure");
                }
                remainingRecipes.ExceptWith(currentTier);
                tiers.Add(currentTier.Select(x => new AutoPlannerRecipe
                {
                    recipe = x,
                    tier = tiers.Count,
                    recipesPerSecond = (float)(processed[x.id] as Variable).SolutionValue()
                }).ToArray());
            }

            this.tiers = tiers.ToArray();
            solver.Dispose();
            return Task.CompletedTask;
        }

    }
}