using System.Collections.Generic;

namespace YAFC.Model
{
    public class Project
    {
        public ProjectSettings settings { get; } = new ProjectSettings();
    }

    public class ProjectSettings
    {
        public List<MilestoneSettings> milestones { get; } = new List<MilestoneSettings>();
    }

    public class MilestoneSettings
    {
        public FactorioObject obj { get; set; }
        public bool unlocked { get; set; }
    }
}