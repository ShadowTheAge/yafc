using System;

namespace YAFC.Model
{
    public static class FactorioObjectOrdering
    {
        public static readonly Func<int, ulong> byMilestonesId = x => Milestones.milestoneResult[x] - 1;
        public static readonly Func<FactorioObject, ulong> byMilestones = x => Milestones.milestoneResult[x.id] - 1;
        public static readonly Func<FactorioObject, int> byComplexity = x => x.GetComplexity();
    }
}