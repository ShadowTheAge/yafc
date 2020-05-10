using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Google.OrTools.Graph;
using Google.OrTools.LinearSolver;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Single;
using SDL2;

namespace YAFC.Model
{
    public static class BestFlowAnalysis
    {
        public static List<(Recipe recipe, float value, int cluster)> PerformFlowAnalysis(Goods target)
        {
            var processed = new object[Database.allObjects.Length];
            var processingStack = new Queue<Goods>();
            processingStack.Enqueue(target);
            var solver = DataUtils.CreateSolver("BestFlowSolver");
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
                                constr.SetCoefficient(var, constr.GetCoefficient(var) + product.amount);
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

            var resultList = new List<(Recipe, float, int cluster)>();
            foreach (var recipe in recipeOrdering)
            {
                var var = processed[recipe.id] as Variable;
                var value = (float) var.SolutionValue();
                if (value <= 0f)
                    continue;
                resultList.Add((recipe, value, 0));
            }


            var dict = new Dictionary<Goods, List<(int, float)>>();
            var adjacencyMatrix = Matrix.Build.SparseDiagonal(resultList.Count, 10f);
            var index = 0;
            foreach (var (recipe, value, _) in resultList)
            {
                foreach (var product in recipe.products)
                    UpdateAdjMatrix(index, value * (10f + product.amount), product.goods, adjacencyMatrix, dict);
                foreach (var ingredient in recipe.ingredients)
                    UpdateAdjMatrix(index, value * (10f + ingredient.amount), ingredient.goods, adjacencyMatrix, dict);
                index++;
            }
            solver.Dispose();

            time = Stopwatch.StartNew();
            var clusterId = MarkovClusters.ComputeMarkovClustering(adjacencyMatrix, 3);
            Console.WriteLine("Markov clustering completed in "+time.ElapsedMilliseconds+" ms");
            for (var i = 0; i < resultList.Count; i++)
            {
                var prev = resultList[i];
                prev.cluster = clusterId[i];
                resultList[i] = prev;
            }
            resultList.Sort((a, b) => a.cluster == b.cluster ? a.Item2.CompareTo(b.Item2) : a.cluster - b.cluster);
            ExportGraph(resultList);

            return resultList;
        }

        private static void UpdateAdjMatrix(int index, float amount, Goods goods, Matrix<float> mx, Dictionary<Goods, List<(int, float)>> dict)
        {
            if (!dict.TryGetValue(goods, out var list))
                dict[goods] = new List<(int, float)> {(index, amount)};
            else
            {
                foreach (var (id, weight) in list)
                    mx[index, id] = mx[id, index] = mx[id, index] + 10f;//MathF.Min(weight, amount);
                list.Add((index, amount));
            }
        }

        private static void ExportGraph(List<(Recipe, float, int)> recipeOrdering)
        {
            var dot = new StringBuilder();
            dot.AppendLine("digraph g {");
            foreach (var (recipe, _, cluster) in recipeOrdering)
            {
                dot.Append("  \".").Append(recipe.name).Append("\" [type=").Append(cluster).AppendLine("];");
                foreach (var product in recipe.products)
                    dot.Append("  \".").Append(recipe.name).Append("\" -> \"").Append(product.goods.name).Append("\";\n");
                foreach (var ingr in recipe.ingredients)
                    dot.Append("  \"").Append(ingr.goods.name).Append("\" -> \".").Append(recipe.name).Append("\";\n");
            }

            dot.Append('}');
            var dots = dot.ToString();
            Console.WriteLine("Graph result:");
            SDL.SDL_SetClipboardText(dots);
        }
    }
}