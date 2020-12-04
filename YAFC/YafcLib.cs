using System;
using System.Reflection;
using YAFC.Model;

namespace YAFC
{
    public static class YafcLib
    {
        public static Version version { get; private set; }

        public static void Init()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            version = new Version(v.Major, v.Minor, v.Build);
            Project.currentYafcVersion = version;
        }

        public static void RegisterDefaultAnalysis()
        {
            Analysis.RegisterAnalysis(Milestones.Instance);
            Analysis.RegisterAnalysis(AutomationAnalysis.Instance);
            Analysis.RegisterAnalysis(CostAnalysis.Instance);
            Analysis.RegisterAnalysis(CostAnalysis.InstanceAtMilestones);
            Analysis.RegisterAnalysis(TechnologyScienceAnalysis.Instance);
        }
    }
}