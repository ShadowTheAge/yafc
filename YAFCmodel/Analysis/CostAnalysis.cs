using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Google.OrTools.LinearSolver;

namespace YAFC.Model
{
    public static class CostAnalysis
    {
        private const float CostPerRecipe = 1f;
        private const float CostPerIngredient = 0.2f;
        private const float CostPerProduct = 0.4f;
        private const float CostPerItemPerSecond = 0.02f;
        private const float CostPerFluidPerSecond = 0.001f;
        private const float CostPerSecond = 0.1f;
        public const float UnboundedCost = 1e10f;
        private const float CostLowerLimit = -100f;

        private static float[] cost;

        public static float Cost(this FactorioObject goods) => cost[goods.id];

        public static void Process()
        {
            var solver = Solver.CreateSolver("WorkspaceSolver", "GLOP_LINEAR_PROGRAMMING");
            var objective = solver.Objective();
            objective.SetMaximization();
            var time = Stopwatch.StartNew();
            
            var variables = new Variable[Database.allObjects.Length];
            for (var i = 0; i < Database.allGoods.Length; i++)
            {
                var goods = Database.allGoods[i];
                if (!goods.IsAccessible())
                    continue;
                var variable = solver.MakeVar(CostLowerLimit, UnboundedCost, false, goods.name);
                objective.SetCoefficient(variable, 1);
                variables[goods.id] = variable;
            }
            
            var export = new float[Database.allObjects.Length];
            Array.Fill(export, float.NegativeInfinity);

            var boundedGoods = new HashSet<Goods>();
            var lastRecipe = new Recipe[Database.allObjects.Length];
            foreach (var recipe in Database.allRecipes)
            {
                if (!recipe.IsAccessible())
                    continue;
                var logisticsCost = CostPerRecipe + CostPerIngredient * recipe.ingredients.Length + CostPerProduct * recipe.products.Length + CostPerSecond * recipe.time;
                var constraint = solver.MakeConstraint(double.NegativeInfinity, 0, recipe.name);
                foreach (var product in recipe.products)
                {
                    var var = variables[product.goods.id];
                    var amortAmount = product.amount * product.probability;
                    constraint.SetCoefficient(var, amortAmount);
                    lastRecipe[product.goods.id] = recipe;
                    boundedGoods.Add(product.goods);
                    if (product.goods is Item)
                        logisticsCost += amortAmount * CostPerItemPerSecond / recipe.time;
                    else if (product.goods is Fluid)
                        logisticsCost += amortAmount * CostPerFluidPerSecond / recipe.time;
                }

                foreach (var ingredient in recipe.ingredients)
                {
                    var var = variables[ingredient.goods.id];
                    double coef = -ingredient.amount;
                    if (lastRecipe[ingredient.goods.id] == recipe)
                        coef += constraint.GetCoefficient(var);
                    constraint.SetCoefficient(var, coef);
                    if (ingredient.goods is Item)
                        logisticsCost += ingredient.amount * CostPerItemPerSecond / recipe.time;
                    else if (ingredient.goods is Fluid)
                        logisticsCost += ingredient.amount * CostPerFluidPerSecond / recipe.time;
                }
                constraint.SetUb(logisticsCost);
                export[recipe.id] = logisticsCost;
            }

            foreach (var goods in boundedGoods)
            {
                variables[goods.id].SetUb(double.PositiveInfinity);
            }
            
            var model = solver.ExportModelAsLpFormat(false);
            Console.WriteLine(model);
            
            var result = solver.Solve();
            Console.WriteLine("Cost analysis completed in "+time.ElapsedMilliseconds+" ms. with result "+result);
            foreach (var g in Database.allGoods)
                export[g.id] = g.IsAccessible() ? (float)variables[g.id].SolutionValue() : float.PositiveInfinity;
            cost = export;
            
            solver.Dispose();
        }

        private static readonly string[] CostRatings = new[]
        {
            "Free", "Trivial", "Basic", "Easy", "Moderate", "Medium", "Solid", "Demanding", "Challenging", "Hard", "Severe", "Tough", "Ambitious", "Tremendous", "Terrific",
            "Overwhelming", "Hopeless", "Insurmountable", "Unthinkable", "Unimaginable", "Unimaginable", "Inconcievable"
        };

        private static StringBuilder sb = new StringBuilder();
        public static string GetGoodsDisplay(Goods goods)
        {
            var cost = goods.Cost();
            if (cost >= UnboundedCost)
                return "YAFC analysis: Unable to find a way to fully automate this";
            if (float.IsNegativeInfinity(cost))
                return "YAFC analysis: This is inaccessible";
            
            sb.Clear();
            
            var compareCost = cost;
            string costPrefix;
            if (goods is Fluid)
            {
                compareCost = cost * 10;
                costPrefix = "YAFC cost per 10 units of fluid:"; 
            }
            else if (goods is Item)
                costPrefix = "YAFC cost per item:"; 
            else if (goods is Special special && special.isPower)
                costPrefix = "YAFC cost per 1 MW:";
            else costPrefix = "YAFC cost per thing:";
            
            var logCost = MathF.Log10(MathF.Abs(compareCost));
            var roundPower = Math.Pow(10, (int)Math.Floor(logCost - 1));
            var roundedCompareCost = Math.Round(compareCost / roundPower) * roundPower;
            
            if (cost < 0f)
            {
                if (cost <= CostLowerLimit)
                    sb.Append("YAFC analysis: This looks like trash that is hard to get rid of\n");
                sb.Append("YAFC analysis: This looks like junk that is more trouble than it is worth\n");
            }
            else
            {
                string costRating;
                var logIndex = (int) (logCost / 2f) + 3;
                if (logIndex < 0)
                    costRating = "Free";
                else if (logIndex >= CostRatings.Length)
                    costRating = "Absurd";
                else costRating = CostRatings[logIndex];

                sb.Append("YAFC cost rating: ").Append(costRating).Append("\n");
            }

            sb.Append(costPrefix).Append(' ').Append(roundedCompareCost);
            return sb.ToString();
        }
    }
}