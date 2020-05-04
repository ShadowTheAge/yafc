using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Google.OrTools.LinearSolver;
using SDL2;

namespace YAFC.Model
{
    public static class BestFlowAnalysis
    {
        public static List<(Recipe recipe, float value)> PerformFlowAnalysis(Goods target)
        {
            var processed = new object[Database.allObjects.Length];
            var processingStack = new Queue<Goods>();
            processingStack.Enqueue(target);
            var solver = Solver.CreateSolver("BestFlowSolver", "GLOP_LINEAR_PROGRAMMING");
            processed[target.id] = solver.MakeConstraint(1d, double.PositiveInfinity, target.name);
            var objective = solver.Objective();
            var time = Stopwatch.StartNew();
            
            var recipeOrdering = new List<Recipe>();
            
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
                        recipeOrdering.Add(recipe);
                        var = solver.MakeNumVar(0d, double.PositiveInfinity, recipe.name);
                        objective.SetCoefficient(var, recipe.RecipeBaseCost());
                        processed[recipe.id] = var;

                        foreach (var product in recipe.products)
                        {
                            if (processed[product.goods.id] is Constraint constr && !processingStack.Contains(product.goods)) 
                                constr.SetCoefficient(var, constr.GetCoefficient(var) + product.average);
                        }

                        foreach (var ingredient in recipe.ingredients)
                        {
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

            var result = solver.Solve();
            Console.WriteLine("Flow analysis completed in "+time.ElapsedMilliseconds+" ms. with result "+result);
            if (result != Solver.ResultStatus.OPTIMAL)
            {
                Console.WriteLine(solver.ExportModelAsLpFormat(false));
                return null;
            }

            var dot = new StringBuilder();
            dot.AppendLine("digraph g {");
            var resultList = new List<(Recipe, float)>();
            foreach (var recipe in recipeOrdering)
            {
                var var = processed[recipe.id] as Variable;
                var value = var.SolutionValue();
                if (value > 0f)
                {
                    resultList.Add((recipe, (float)value));
                    foreach (var product in recipe.products)
                        dot.Append("  \".").Append(recipe.name).Append("\" -> \"").Append(product.goods.name).Append("\";\n");
                    foreach (var ingr in recipe.ingredients)
                        dot.Append("  \"").Append(ingr.goods.name).Append("\" -> \".").Append(recipe.name).Append("\";\n");
                }
            }

            dot.Append('}');
            var dots = dot.ToString();
            Console.WriteLine("Graph result:");
            SDL.SDL_SetClipboardText(dots);
            Console.WriteLine(dots);
            
            solver.Dispose();
            return resultList;
        }
    }
}