using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace YAFC.Model
{
    public class Project : Serializable
    {
        public string attachedFileName { get; private set; }
        public bool justCreated { get; private set; } = true;
        public ProjectSettings settings { get; }
        public List<Group> groups { get; } = new List<Group>();
        public Project() : base(new UndoSystem())
        {
            settings = new ProjectSettings(this);
        }

        public static Project ReadFromFile(string path)
        {
            Project proj;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var reader = new Utf8JsonReader(File.ReadAllBytes(path));
                proj = SerializationMap<Project>.DeserializeFromJson(null, ref reader);
                proj.justCreated = false;
            } else proj = new Project();
            proj.attachedFileName = path;
            return proj;
        }

        public void Save()
        {
            
        }
    }

    public class ProjectSettings : Serializable
    {
        public readonly Project project;
        public List<FactorioObject> milestones { get; } = new List<FactorioObject>();
        public ulong milestonesUnlockedMask { get; set; }
        public ProjectSettings(Project project) : base(project)
        {
            this.project = project;
        }
    }
}