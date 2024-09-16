using System;
using System.Collections.Generic;
using System.Linq;

namespace Yafc.Model;

public abstract class Analysis {
    internal readonly HashSet<FactorioObject> excludedObjects = [];

    public abstract void Compute(Project project, ErrorCollector warnings);

    private static readonly List<Analysis> analyses = [];

    // TODO don't ignore dependencies
    public static void RegisterAnalysis(Analysis analysis, params Analysis[] dependencies) => analyses.Add(analysis);

    public static void ProcessAnalyses(IProgress<(string, string)> progress, Project project, ErrorCollector errors) {
        foreach (var analysis in analyses) {
            progress.Report(("Running analysis algorithms", analysis.GetType().Name));
            analysis.Compute(project, errors);
        }
    }

    public abstract string description { get; }

    public static void Do<T>(Project project) where T : Analysis {
        foreach (var analysis in analyses) {
            if (analysis is T t) {
                t.Compute(project, new ErrorCollector());
            }
        }
    }

    /// <summary>
    /// Call to exclude the specified <see cref="FactorioObject"/> from one of the analyses.
    /// </summary>
    /// <typeparam name="T">The analysis that should ignore <paramref name="obj"/>.</typeparam>
    /// <param name="obj">The object to be excluded from analysis.</param>
    public static void ExcludeFromAnalysis<T>(FactorioObject obj) where T : Analysis {
        foreach (T analysis in analyses.OfType<T>()) {
            analysis.excludedObjects.Add(obj);
        }
    }
}

public static class AnalysisExtensions {
    public static bool IsAccessible(this FactorioObject obj) => Milestones.Instance.GetMilestoneResult(obj) != 0;

    public static bool IsAccessibleWithCurrentMilestones(this FactorioObject obj) => Milestones.Instance.IsAccessibleWithCurrentMilestones(obj);

    public static bool IsAutomatable(this FactorioObject obj) => AutomationAnalysis.Instance.automatable[obj] != AutomationStatus.NotAutomatable;

    public static bool IsAutomatableWithCurrentMilestones(this FactorioObject obj) => AutomationAnalysis.Instance.automatable[obj] == AutomationStatus.AutomatableNow;

    public static float Cost(this FactorioObject goods, bool atCurrentMilestones = false) => CostAnalysis.Get(atCurrentMilestones).cost[goods];

    public static float ApproximateFlow(this FactorioObject recipe, bool atCurrentMilestones = false) => CostAnalysis.Get(atCurrentMilestones).flow[recipe];

    public static float ProductCost(this Recipe recipe, bool atCurrentMilestones = false) => CostAnalysis.Get(atCurrentMilestones).recipeProductCost[recipe];

    public static float RecipeWaste(this Recipe recipe, bool atCurrentMilestones = false) => CostAnalysis.Get(atCurrentMilestones).recipeWastePercentage[recipe];

    public static float RecipeBaseCost(this Recipe recipe, bool atCurrentMilestones = false) => CostAnalysis.Get(atCurrentMilestones).recipeCost[recipe];

    /// <summary>
    /// Filters a list of <see cref="FactorioObject"/>s down to those that were not excluded by the specified <see cref="Analysis"/>
    /// </summary>
    /// <typeparam name="T">The type of objects in the list.</typeparam>
    /// <param name="values">The values that should be filtered.</param>
    /// <param name="analysis">The <see cref="Analysis"/> that is requesting the filtering. In most circumstances, pass <see langword="this"/>.</param>
    /// <returns></returns>
    public static IEnumerable<T> ExceptExcluded<T>(this IEnumerable<T> values, Analysis analysis) where T : FactorioObject =>
        values.Except(analysis.excludedObjects.OfType<T>());
}
