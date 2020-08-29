using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Google.OrTools.LinearSolver;
using SDL2;
using YAFC.UI;

namespace YAFC.Model
{
    public class CostAnalysis : Analysis
    {
        public static readonly CostAnalysis Instance = new CostAnalysis(false); 
        public static readonly CostAnalysis InstanceAtMilestones = new CostAnalysis(true); 
        public static CostAnalysis Get(bool atCurrentMilestones) => atCurrentMilestones ? InstanceAtMilestones : Instance;
        
        private const float CostPerSecond = 0.1f;
        private const float CostPerIngredient = 0.2f;
        private const float CostPerProduct = 0.4f;
        private const float CostPerItem = 0.02f;
        private const float CostPerFluid = 0.001f;
        private const float CostPerPollution = 0.1f;
        private const float CostLowerLimit = -10f;
        private const float CostLimitWhenGeneratesOnMap = 1e4f;
        private const float MiningPenalty = 5f; // Penalty for any mining
        private const float MiningMaxDensityForPenalty = 2000; // Mining things with less density than this gets extra penalty
        private const float MiningMaxExtraPenaltyForRarity = 10f;

        public Mapping<FactorioObject, float> cost;
        public Mapping<Recipe, float> recipeCost;
        public Mapping<RecipeOrTechnology, float> recipeProductCost;
        public Mapping<FactorioObject, float> flow;
        public Mapping<Recipe, float> recipeWastePercentage;
        public float flowRecipeScaleCoef = 1f;
        public Goods[] importantItems;
        private readonly bool onlyCurrentMilestones;

        public CostAnalysis(bool onlyCurrentMilestones)
        {
            this.onlyCurrentMilestones = onlyCurrentMilestones;
        }

        private bool ShouldInclude(FactorioObject obj)
        {
            return onlyCurrentMilestones ? obj.IsAutomatableWithCurrentMilestones() : obj.IsAutomatable();
        }

        public override void Compute(Project project, ErrorCollector warnings)
        {
            var solver = DataUtils.CreateSolver("WorkspaceSolver");
            var objective = solver.Objective();
            objective.SetMaximization();
            var time = Stopwatch.StartNew();

            var variables = Database.goods.CreateMapping<Variable>();
            var constraints = Database.recipes.CreateMapping<Constraint>();

            var sciencePackUsage = new Dictionary<Goods, float>();
            foreach (var technology in Database.technologies.all)
            {
                if (technology.IsAccessible())
                {
                    foreach (var ingredient in technology.ingredients)
                    {
                        if (ingredient.goods.IsAutomatable())
                        {
                            if (onlyCurrentMilestones && !Milestones.Instance.IsAccessibleAtNextMilestone(ingredient.goods))
                                continue;
                            sciencePackUsage.TryGetValue(ingredient.goods, out var prev);
                            sciencePackUsage[ingredient.goods] = prev + ingredient.amount * technology.count;
                        }
                    }
                }
            }
            
            
            foreach (var goods in Database.goods.all)
            {
                if (!ShouldInclude(goods))
                    continue;
                var mapGeneratedAmount = 0f;
                foreach (var src in goods.miscSources)
                {
                    if (src is Entity ent && ent.mapGenerated)
                    {
                        foreach (var product in ent.loot)
                        {
                            if (product.goods == goods)
                                mapGeneratedAmount += product.amount;
                        }
                    }
                }
                var variable = solver.MakeVar(CostLowerLimit, CostLimitWhenGeneratesOnMap / mapGeneratedAmount, false, goods.name);
                var baseItemCost = (goods.usages.Length + 1) * 0.01f;
                if (goods is Item item && (item.factorioType != "item" || item.placeResult != null)) 
                    baseItemCost += 0.1f;
                if (goods.fuelValue > 0f)
                    baseItemCost += goods.fuelValue * 0.0001f;
                objective.SetCoefficient(variable, baseItemCost);
                variables[goods] = variable;
            }

            foreach (var (item, count) in sciencePackUsage)
                objective.SetCoefficient(variables[item], count / 1000f);

            var export = Database.objects.CreateMapping<float>();
            var recipeProductionCost = Database.recipesAndTechnologies.CreateMapping<float>();
            recipeCost = Database.recipes.CreateMapping<float>();
            flow = Database.objects.CreateMapping<float>();
            var lastVariable = Database.goods.CreateMapping<Variable>();
            foreach (var recipe in Database.recipes.all)
            {
                if (!ShouldInclude(recipe))
                    continue;
                if (onlyCurrentMilestones && !recipe.IsAccessibleWithCurrentMilestones())
                    continue;
                var logisticsCost = (CostPerIngredient * recipe.ingredients.Length + CostPerProduct * recipe.products.Length + CostPerSecond) * recipe.time;

                // TODO incorporate fuel selection. Now just select fuel if it only uses 1 fuel
                Goods singleUsedFuel = null;
                var singleUsedFuelAmount = 0f;
                var minEmissions = 100f;
                foreach (var crafter in recipe.crafters)
                {
                    minEmissions = MathF.Min(crafter.energy.emissions, minEmissions);
                    if (crafter.energy.type == EntityEnergyType.Heat)
                        break;
                    foreach (var fuel in crafter.energy.fuels)
                    {
                        if (!ShouldInclude(fuel))
                            continue;
                        if (fuel.fuelValue <= 0f)
                        {
                            singleUsedFuel = null;
                            break;
                        }
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

                if (singleUsedFuel == Database.electricity || singleUsedFuel == Database.voidEnergy || singleUsedFuel == Database.heat)
                    singleUsedFuel = null;
                
                var constraint = solver.MakeConstraint(double.NegativeInfinity, 0, recipe.name);
                constraints[recipe] = constraint;

                foreach (var product in recipe.products)
                {
                    var var = variables[product.goods];
                    var amount = product.amount;
                    constraint.SetCoefficientCheck(var, amount, ref lastVariable[product.goods]);
                    if (product.goods is Item)
                        logisticsCost += amount * CostPerItem;
                    else if (product.goods is Fluid)
                        logisticsCost += amount * CostPerFluid;
                }

                if (singleUsedFuel != null)
                {
                    var var = variables[singleUsedFuel];
                    constraint.SetCoefficientCheck(var, -singleUsedFuelAmount, ref lastVariable[singleUsedFuel]);
                }

                foreach (var ingredient in recipe.ingredients)
                {
                    var var = variables[ingredient.goods]; // TODO split cost analysis
                    constraint.SetCoefficientCheck(var, -ingredient.amount, ref lastVariable[ingredient.goods]);
                    if (ingredient.goods is Item)
                        logisticsCost += ingredient.amount * CostPerItem;
                    else if (ingredient.goods is Fluid)
                        logisticsCost += ingredient.amount * CostPerFluid;
                }

                if (recipe.sourceEntity != null && recipe.sourceEntity.mapGenerated)
                {
                    var totalMining = 0f;
                    foreach (var product in recipe.products)
                        totalMining += product.amount;
                    var miningPenalty = MiningPenalty;
                    var totalDensity = recipe.sourceEntity.mapGenDensity / totalMining;
                    if (totalDensity < MiningMaxDensityForPenalty)
                    {
                        var extraPenalty = MathF.Log( MiningMaxDensityForPenalty / totalDensity);
                        miningPenalty += Math.Min(extraPenalty, MiningMaxExtraPenaltyForRarity);
                    }

                    logisticsCost *= miningPenalty;
                }

                if (minEmissions >= 0f)
                    logisticsCost += minEmissions * CostPerPollution * recipe.time;
                else logisticsCost = MathF.Max(logisticsCost * 0.5f, logisticsCost + minEmissions * CostPerPollution * recipe.time); // only allow cut logistics cost by half with negative emissions
                
                constraint.SetUb(logisticsCost);
                export[recipe] = logisticsCost;
                recipeCost[recipe] = logisticsCost;
            }

            // TODO this is temporary fix for strange item sources (make the cost of item not higher than the cost of its source)
            foreach (var item in Database.items.all)
            {
                if (ShouldInclude(item))
                {
                    foreach (var source in item.miscSources)
                    {
                        if (source is Goods g && ShouldInclude(g))
                        {
                            var constraint = solver.MakeConstraint(double.NegativeInfinity, 0, "source-"+item.locName);
                            constraint.SetCoefficient(variables[g], -1);
                            constraint.SetCoefficient(variables[item], 1);
                        }
                    }
                }
            }
            
            // TODO this is temporary fix for fluid temperatures (make the cost of fluid with lower temp not higher than the cost of fluid with higher temp)
            foreach (var (name, fluids) in Database.fluidVariants)
            {
                var prev = fluids[0];
                for (var i = 1; i < fluids.Count; i++)
                {
                    var cur = fluids[i];
                    var constraint = solver.MakeConstraint(double.NegativeInfinity, 0, "fluid-"+name+"-"+prev.temperature);
                    constraint.SetCoefficient(variables[prev], 1);
                    constraint.SetCoefficient(variables[cur], -1);
                    prev = cur;
                }
            }

            var result = solver.TrySolvewithDifferentSeeds();
            Console.WriteLine("Cost analysis completed in "+time.ElapsedMilliseconds+" ms. with result "+result);
            var sumImportance = 1f;
            var totalRecipes = 0;
            if (result == Solver.ResultStatus.OPTIMAL || result == Solver.ResultStatus.FEASIBLE)
            {
                var objectiveValue = (float)objective.Value();
                Console.WriteLine("Estimated modpack cost: "+DataUtils.FormatAmount(objectiveValue*1000f, UnitOfMeasure.None));
                foreach (var g in Database.goods.all)
                {
                    if (variables[g] == null)
                        continue;
                    var value = (float) variables[g].SolutionValue();
                    export[g] = value;
                }

                foreach (var recipe in Database.recipes.all)
                {
                    if (constraints[recipe] == null)
                        continue;
                    var recipeFlow = (float) constraints[recipe].DualValue();
                    if (recipeFlow > 0f)
                    {
                        totalRecipes++;
                        sumImportance += recipeFlow;
                        flow[recipe] = recipeFlow;
                        foreach (var product in recipe.products)
                            flow[product.goods] += recipeFlow * product.amount;
                    }
                }

                flowRecipeScaleCoef = (1e2f * totalRecipes) / (sumImportance * MathF.Sqrt(MathF.Sqrt(objectiveValue)));
            }
            foreach (var o in Database.objects.all)
            {
                if (!ShouldInclude(o))
                {
                    export[o] = float.PositiveInfinity;
                    continue;
                }

                if (o is RecipeOrTechnology recipe)
                {
                    foreach (var ingredient in recipe.ingredients) // TODO split
                        export[o] += export[ingredient.goods] * ingredient.amount;
                    foreach (var product in recipe.products)
                        recipeProductionCost[recipe] += product.amount * export[product.goods];
                } 
                else if (o is Entity entity)
                {
                    var minimal = float.PositiveInfinity;
                    foreach (var item in entity.itemsToPlace)
                    {
                        if (export[item] < minimal)
                            minimal = export[item];
                    }
                    export[o] = minimal;
                }
            }
            cost = export;
            recipeProductCost = recipeProductionCost;

            recipeWastePercentage = Database.recipes.CreateMapping<float>();
            if (result == Solver.ResultStatus.OPTIMAL || result == Solver.ResultStatus.FEASIBLE)
            {
                foreach (var (recipe, constraint) in constraints)
                {
                    if (constraint == null)
                        continue;
                    var productCost = 0f;
                    foreach (var product in recipe.products)
                        productCost += product.amount * product.goods.Cost();
                    recipeWastePercentage[recipe] = 1f - productCost / cost[recipe];
                }
            }
            else
            {
                if (!onlyCurrentMilestones)
                    warnings.Error("Cost analysis was unable to process this modpack. This may mean YAFC bug.", ErrorSeverity.AnalysisWarning);
            }

            importantItems = Database.goods.all.Where(x => x.usages.Length > 1).OrderByDescending(x => flow[x] * cost[x] * x.usages.Count(y => ShouldInclude(y) && recipeWastePercentage[y] == 0f)).ToArray();

            solver.Dispose();
        }

        public override string description => "Cost analysis computes a hypothetical late-game base. This simulation has two very important results: How much does stuff (items, recipes, etc) cost and how much of stuff do you need. " +
                                              "It also collects a bunch of auxilary results, for example how efficient are different recipes. These results are used as heuristics and weights for calculations, and are also useful by themselves.";

        private static readonly string[] CostRatings = {
            "This is expensive!",
            "This is very expensive!",
            "This is EXTREMELY expensive!",
            "This is ABSURDLY expensive",
            "RIP you"
        };

        private static StringBuilder sb = new StringBuilder();
        public static string GetDisplayCost(FactorioObject goods)
        {
            var cost = goods.Cost();
            var costNow = goods.Cost(true);
            if (float.IsPositiveInfinity(cost))
                return "YAFC analysis: Unable to find a way to fully automate this";

            sb.Clear();
            
            var compareCost = cost;
            var compareCostNow = costNow;
            string costPrefix;
            if (goods is Fluid)
            {
                compareCost = cost * 50;
                compareCostNow = costNow * 50;
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
            if (cost <= 0f)
            {
                if (cost < 0f && goods is Goods g)
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

            sb.Append(costPrefix).Append(" ¥").Append(DataUtils.FormatAmount(compareCost, UnitOfMeasure.None));
            if (compareCostNow > compareCost && !float.IsPositiveInfinity(compareCostNow))
                sb.Append(" (Currently ¥").Append(DataUtils.FormatAmount(compareCostNow, UnitOfMeasure.None)).Append(")");
            return sb.ToString();
        }
        
        private static readonly string[] BuildingCount = {
            "YAFC analysis: You probably want multiple buildings making this",
            "YAFC analysis: You probably want dozens of buildings making this",
            "YAFC analysis: You probably want HUNDREDS of buildings making this",
        };
        
        private static readonly string[] ItemCount = {
            " in thousands",
            " in tens of thousands",
            " in hundreds of thousands",
            " in millions",
            " in tens of millions",
            " in hundreds of millions",
            " in BILLIONS",
            " in TENS OF BILLIONS",
            " in HUNDREDS OF BILLIONS",
            " in TRILLIONS",
        };

        public string GetBuildingAmount(Recipe recipe, float flow)
        {
            var coef = recipe.time * flow * flowRecipeScaleCoef;
            if (coef < 1f)
                return null;
            var log = MathF.Log10(coef);
            sb.Clear();
            sb.Append(BuildingCount[MathUtils.Clamp(MathUtils.Floor(log), 0, BuildingCount.Length - 1)]);
            sb.Append(" (Say, ").Append(DataUtils.FormatAmount(MathF.Ceiling(coef), UnitOfMeasure.None)).Append(", depends on crafting speed)");
            return sb.ToString();
        }

        public string GetItemAmount(Goods goods)
        {
            var itemFlow = flow[goods];
            if (itemFlow <= 1f || itemFlow * goods.Cost() < 10000f)
                return null;
            var log = MathUtils.Floor(MathF.Log10(itemFlow));
            sb.Clear();
            sb.Append("YAFC analysis: You will need this ").Append(goods.type).Append(ItemCount[Math.Min(log, ItemCount.Length - 1)]).Append(" (for all researches)");
            return sb.ToString();
        }
    }
}