using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace YAFC.Model
{
    public class Project : ModelObject
    {
        public uint projectVersion => undo.version;
        public string attachedFileName { get; private set; }
        public bool justCreated { get; private set; } = true;
        public ProjectSettings settings { get; }
        public List<ProjectPage> pages { get; } = new List<ProjectPage>();
        public new UndoSystem undo => base.undo;
        private uint lastSavedState;
        public Project() : base(new UndoSystem())
        {
            settings = new ProjectSettings(this);
        }

        public event Action metaInfoChanged;

        protected internal override void ThisChanged(bool visualOnly)
        {
            base.ThisChanged(visualOnly);
            metaInfoChanged?.Invoke();
        }

        public static Project ReadFromFile(string path)
        {
            Project proj;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var reader = new Utf8JsonReader(File.ReadAllBytes(path));
                reader.Read();
                proj = SerializationMap<Project>.DeserializeFromJson(null, ref reader);
                proj.justCreated = false;
                if (!reader.IsFinalBlock)
                    throw new JsonException("Json was not consumed to the end!");
            } else proj = new Project();
            proj.attachedFileName = path;
            return proj;
        }

        public void Save(string fileName)
        {
            if (lastSavedState == projectVersion && fileName == attachedFileName)
                return;
            using (var ms = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(ms, JsonUtils.DefaultWriterOptions))
                    SerializationMap<Project>.SerializeToJson(this, writer);
                ms.Position = 0;
                using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                    ms.CopyTo(fs);
            }
            attachedFileName = fileName;
            lastSavedState = projectVersion;
        }
    }

    public class ProjectSettings : ModelObject
    {
        public readonly Project project;
        public List<FactorioObject> milestones { get; } = new List<FactorioObject>();
        public SortedList<FactorioObject, ProjectPerItemFlags> itemFlags { get; } = new SortedList<FactorioObject, ProjectPerItemFlags>(DataUtils.DeterministicComparer);
        public event Action<bool> changed;
        protected internal override void ThisChanged(bool visualOnly)
        {
            base.ThisChanged(visualOnly);
            changed?.Invoke(visualOnly);
        }

        public void SetFlag(FactorioObject obj, ProjectPerItemFlags flag, bool set)
        {
            itemFlags.TryGetValue(obj, out var flags);
            var newFlags = set ? flags | flag : flags & ~flag;
            if (newFlags != flags)
            {
                this.RecordUndo();
                itemFlags[obj] = newFlags;
            }
        }

        public ProjectPerItemFlags Flags(FactorioObject obj) => itemFlags.TryGetValue(obj, out var val) ? val : 0;
        public ProjectSettings(Project project) : base(project)
        {
            this.project = project;
        }
    }

    [Flags]
    public enum ProjectPerItemFlags
    {
        MilestoneUnlocked = 1 << 0,
        MarkedAccessible = 1 << 1,
        MarkedInaccessible = 1 << 2,
        MarkedAutomated = 1 << 3,
    }
}