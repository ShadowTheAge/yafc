using System.Collections.Generic;

namespace YAFC.Model
{
    public class Project : Serializable
    {
        public ProjectSettings settings { get; }
        public List<Group> groups { get; } = new List<Group>();
        public Project() : base(new UndoSystem())
        {
            settings = new ProjectSettings(this);
        }
    }

    public class ProjectSettings : Serializable
    {
        public List<MilestoneSettings> milestones { get; } = new List<MilestoneSettings>();
        public readonly Project project;
        public ProjectSettings(Project project) : base(project)
        {
            this.project = project;
        }
    }

    public class MilestoneSettings
    {
        public FactorioObject obj { get; set; }
        public bool unlocked { get; set; }
    }
}