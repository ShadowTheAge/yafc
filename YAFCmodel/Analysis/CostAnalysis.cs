using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Google.OrTools.LinearSolver;

namespace YAFC.Model
{
    public static class CostAnalysis
    {
        private const float CostPerSecond = 0.1f;
        private const float CostPerIngredient = 0.2f;
        private const float CostPerProduct = 0.4f;
        private const float CostPerItem = 0.02f;
        private const float CostPerFluid = 0.001f;
        public const float UnboundedCost = 1e10f;
        private const float CostLowerLimit = -10f;
        private const float MiningPenalty = 1f; // Penalty for any mining
        private const float MiningMaxDensityForPenalty = 1000; // Mining things with less density than this gets extra penalty
        private const float MiningMaxExtraPenaltyForRarity = 10f;

        private static float[] cost;
        private static float[] recipeCost;
        private static bool[] suboptimalRecipes;

        public static float Cost(this FactorioObject goods) => cost[goods.id];
        public static bool IsSubOptimal(this Recipe recipe) => suboptimalRecipes[recipe.id];
        public static float RecipeBaseCost(this Recipe recipe) => recipeCost[recipe.id];

        public static void Process()
        {
            var solver = Solver.CreateSolver("WorkspaceSolver", "GLOP_LINEAR_PROGRAMMING");
            var objective = solver.Objective();
            objective.SetMaximization();
            var time = Stopwatch.StartNew();
            
            var variables = new Variable[Database.allObjects.Length];
            var constraints = new Constraint[Database.allRecipes.Length];
            foreach (var goods in Database.allGoods)
            {
                if (!goods.IsAccessible())
                    continue;
                var variable = solver.MakeVar(CostLowerLimit, UnboundedCost, false, goods.name);
                objective.SetCoefficient(variable, 1);
                variables[goods.id] = variable;
            }
            
            var export = new float[Database.allObjects.Length];
            recipeCost = new float[Database.allObjects.Length];

            var boundedGoods = new HashSet<Goods>();
            var lastRecipe = new Recipe[Database.allObjects.Length];
            for (var i = 0; i < Database.allRecipes.Length; i++)
            {
                var recipe = Database.allRecipes[i];
                if (!recipe.IsAccessible())
                    continue;
                var logisticsCost = (CostPerIngredient * recipe.ingredients.Length + CostPerProduct * recipe.products.Length + CostPerSecond) * recipe.time;

                // TODO incorporate fuel selection. Now just select fuel if it only uses 1 fuel
                Goods singleUsedFuel = null;
                var singleUsedFuelAmount = 0f;
                var hasAutomatableEntities = false;
                foreach (var crafter in recipe.crafters)
                {
                    if (!hasAutomatableEntities && crafter.IsAccessible() && crafter.type != "character")
                        hasAutomatableEntities = true;
                    if (crafter.energy.usesHeat)
                        break;
                    foreach (var fuel in crafter.energy.fuels)
                    {
                        var amount = (recipe.time * crafter.power) / (crafter.energy.effectivity * fuel.fuelValue);
                        if (singleUsedFuel == null)
                        {
                            singleUsedFuel = fuel;
                            singleUsedFuelAmount = amount;
                        }
                        else if (singleUsedFuel == fuel)
                        {
                            singleUsedFuelAmount = MathF.Min(singleUsedFuelAmount, amount);
                        }
                        else
                        {
                            singleUsedFuel = null;
                            break;
                        }
                    }
                    if (singleUsedFuel == null)
                        break;
                }

                if (!hasAutomatableEntities)
                {
                    export[recipe.id] = float.PositiveInfinity;
                    continue;
                }

                if (singleUsedFuel == Database.electricity)
                    singleUsedFuel = null;
                
                var constraint = solver.MakeConstraint(double.NegativeInfinity, 0, recipe.name);
                constraints[i] = constraint;

                
                foreach (var product in recipe.products)
                {
                    var var = variables[product.goods.id];
                    var average = product.average;
                    constraint.SetCoefficient(var, average);
                    lastRecipe[product.goods.id] = recipe;
                    boundedGoods.Add(product.goods);
                    if (product.goods is Item)
                        logisticsCost += average * CostPerItem;
                    else if (product.goods is Fluid)
                        logisticsCost += average * CostPerFluid;
                }

                if (singleUsedFuel != null)
                {
                    var var = variables[singleUsedFuel.id];
                    double coef = -singleUsedFuelAmount;
                    if (lastRecipe[singleUsedFuel.id] == recipe)
                        coef += constraint.GetCoefficient(var);
                    constraint.SetCoefficient(var, coef);
                }

                foreach (var ingredient in recipe.ingredients)
                {
                    var var = variables[ingredient.goods.id];
                    double coef = -ingredient.amount;
                    if (lastRecipe[ingredient.goods.id] == recipe)
                        coef += constraint.GetCoefficient(var);
                    constraint.SetCoefficient(var, coef);
                    if (ingredient.goods is Item)
                        logisticsCost += ingredient.amount * CostPerItem;
                    else if (ingredient.goods is Fluid)
                        logisticsCost += ingredient.amount * CostPerFluid;
                }

                if (recipe.sourceEntity != null && recipe.sourceEntity.mapGenerated)
                {
                    logisticsCost += MiningPenalty;
                    var totalMining = 0f;
                    foreach (var product in recipe.products)
                        totalMining += product.amount;
                    var totalDensity = recipe.sourceEntity.mapGenDensity / totalMining;
                    if (totalDensity < MiningMaxDensityForPenalty)
                    {
                        var extraPenalty = MathF.Log( MiningMaxDensityForPenalty / totalDensity);
                        logisticsCost += Math.Min(extraPenalty, MiningMaxExtraPenaltyForRarity);
                    }
                }
                
                constraint.SetUb(logisticsCost);
                export[recipe.id] = logisticsCost;
                recipeCost[recipe.id] = logisticsCost;
            }

            foreach (var goods in boundedGoods)
            {
                variables[goods.id].SetUb(double.PositiveInfinity);
            }
            
            //Console.WriteLine(solver.ExportModelAsLpFormat(false));
            
            var result = solver.Solve();
            Console.WriteLine("Cost analysis completed in "+time.ElapsedMilliseconds+" ms. with result "+result);
            foreach (var g in Database.allGoods)
            {
                if (!g.IsAccessible())
                    continue;
                var value = (float) variables[g.id].SolutionValue();
                if (value >= UnboundedCost)
                    value = float.PositiveInfinity;
                export[g.id] = value;
            }
            foreach (var o in Database.allObjects)
            {
                var id = o.id;
                if (!o.IsAccessible())
                {
                    export[id] = float.PositiveInfinity;
                    continue;
                }

                if (o is Recipe recipe)
                {
                    foreach (var ingredient in recipe.ingredients)
                        export[id] += export[ingredient.goods.id] * ingredient.amount;
                } 
                else if (o is Entity entity)
                {
                    var minimal = float.PositiveInfinity;
                    foreach (var item in entity.itemsToPlace)
                    {
                        if (export[item.id] < minimal)
                            minimal = export[item.id];
                    }
                    export[id] = minimal;
                }
            }
            cost = export;

            var exportFlags = new bool[Database.allObjects.Length];
            for (var i = 0; i < constraints.Length; i++)
            {
                if (constraints[i] != null && constraints[i].BasisStatus() == Solver.BasisStatus.BASIC)
                {
                    exportFlags[Database.allRecipes[i].id] = true;
                } 
            }

            suboptimalRecipes = exportFlags;

            solver.Dispose();
        }

        private static readonly string[] CostRatings = new[]
        {
            "This is expensive!",
            "This is very expensive!",
            "This is EXTREMELY expensive!",
            "This is ABSURDLY expensive",
        };

        private static StringBuilder sb = new StringBuilder();
        public static string GetDisplay(FactorioObject goods)
        {
            var cost = goods.Cost();
            if (float.IsPositiveInfinity(cost))
                return "YAFC analysis: Unable to find a way to fully automate this";

            sb.Clear();
            
            var compareCost = cost;
            string costPrefix;
            if (goods is Fluid)
            {
                compareCost = cost * 50;
                costPrefix = "YAFC cost per 50 units of fluid:"; 
            }
            else if (goods is Item)
                costPrefix = "YAFC cost per item:"; 
            else if (goods is Special special && special.isPower)
                costPrefix = "YAFC cost per 1 MW:";
            else if (goods is Recipe)
                costPrefix = "YAFC cost per recipe:";
            else costPrefix = "YAFC cost:";
            
            var logCost = MathF.Log10(MathF.Abs(compareCost));
            var roundPower = Math.Pow(10, (int)MathF.Floor(logCost - 1));
            var roundedCompareCost = compareCost == 0 ? 0 : Math.Round(compareCost / roundPower) * roundPower;
            
            if (cost <= 0f && goods is Goods g)
            {
                if (cost < 0f)
                {
                    if (g.fuelValue > 0f)
                        sb.Append("YAFC analysis: This looks like junk, but at least it can be burned\n");
                    else if (cost <= CostLowerLimit)
                        sb.Append("YAFC analysis: This looks like trash that is hard to get rid of\n");
                    else sb.Append("YAFC analysis: This looks like junk that needs to be disposed\n");
                }
            }
            else
            {
                var costRating = (int) logCost - 3;
                if (costRating >= 0)
                    sb.Append("YAFC analysis: ").Append(CostRatings[Math.Min(costRating, CostRatings.Length - 1)]).Append('\n');
            }

            sb.Append(costPrefix).Append(' ').Append(roundedCompareCost);
            return sb.ToString();
        }
    }
}