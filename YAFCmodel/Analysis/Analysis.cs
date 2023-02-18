using System;
using System.Collections.Generic;

namespace YAFC.Model
{
    public abstract class Analysis
    {
        public abstract void Compute(Project project, ErrorCollector warnings);

        private static readonly List<Analysis> analyses = new List<Analysis>();
        public static void RegisterAnalysis(Analysis analysis, params Analysis[] dependencies) // TODO don't ignore dependencies
        {
            analyses.Add(analysis);
        }

        public static void ProcessAnalyses(IProgress<(string, string)> progress, Project project, ErrorCollector errors)
        {
            foreach (var analysis in analyses)
            {
                progress.Report(("Running analysis algorithms", analysis.GetType().Name));
                analysis.Compute(project, errors);
            }
        }
        
        public abstract string description { get; }

        public static void Do<T>(Project project) where T:Analysis
        {
            foreach (var analysis in analyses)
            {
                if (analysis is T t)
                    t.Compute(project, new ErrorCollector());
            }
        }
    }

    public static class AnalysisExtensions
    {
        public static bool IsAccessible(this FactorioObject obj) => Milestones.Instance.GetMilestoneResult(obj) != 0;
        public static bool IsAccessibleWithCurrentMilestones(this FactorioObject obj) => Milestones.Instance.IsAccessibleWithCurrentMilestones(obj);
        public static bool IsAutomatable(this FactorioObject obj) => AutomationAnalysis.Instance.automatable[obj] != AutomationStatus.NotAutomatable;
        public static bool IsAutomatableWithCurrentMilestones(this FactorioObject obj) => AutomationAnalysis.Instance.automatable[obj] == AutomationStatus.AutomatableNow;
        public static float Cost(this FactorioObject goods, bool atCurrentMilestones = false) => CostAnalysis.Get(atCurrentMilestones).cost[goods];
        public static float ApproximateFlow(this FactorioObject recipe, bool atCurrentMilestones = false) => CostAnalysis.Get(atCurrentMilestones).flow[recipe];
        public static float ProductCost(this Recipe recipe, bool atCurrentMilestones = false) => CostAnalysis.Get(atCurrentMilestones).recipeProductCost[recipe];
        public static float RecipeWaste(this Recipe recipe, bool atCurrentMilestones = false) => CostAnalysis.Get(atCurrentMilestones).recipeWastePercentage[recipe];
        public static float RecipeBaseCost(this Recipe recipe, bool atCurrentMilestones = false) => CostAnalysis.Get(atCurrentMilestones).recipeCost[recipe];
    }
}