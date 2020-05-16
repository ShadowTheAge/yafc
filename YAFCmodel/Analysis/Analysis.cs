using System;
using System.Collections.Generic;

namespace YAFC.Model
{
    public abstract class Analysis
    {
        public abstract void Compute(Project project, List<string> warnings);

        private static readonly List<Analysis> analyses = new List<Analysis>();
        public static void RegisterAnalysis(Analysis analysis, params Analysis[] dependencies) // TODO don't ignore dependencies
        {
            analyses.Add(analysis);
        }

        internal static void ProcessAnalyses(IProgress<(string, string)> progress, Project project)
        {
            var warnings = new List<string>();
            foreach (var analysis in analyses)
            {
                progress.Report(("Running analysis algorithms", analysis.GetType().Name));
                analysis.Compute(project, warnings);
            }
        }
        
        public abstract string description { get; }
    }

    public static class AnalysisExtensions
    {
        public static bool IsAccessible(this FactorioObject obj) => Milestones.Instance.milestoneResult[obj] != 0;
        public static bool IsAccessibleWithCurrentMilestones(this FactorioObject obj) => Milestones.Instance.IsAccessibleWithCurrentMilesones(obj);
        public static bool IsAutomatable(this FactorioObject obj) => AutomationAnalysis.Instance.automatable[obj];
        public static float Cost(this FactorioObject goods) => CostAnalysis.Instance.cost[goods];
        public static float ApproximateFlow(this FactorioObject recipe) => CostAnalysis.Instance.flow[recipe];
        public static float ProductCost(this Recipe recipe) => CostAnalysis.Instance.recipeProductCost[recipe];
        public static float RecipeWaste(this Recipe recipe) => CostAnalysis.Instance.recipeWastePercentage[recipe];
        public static float RecipeBaseCost(this Recipe recipe) => CostAnalysis.Instance.recipeCost[recipe];
    }
}