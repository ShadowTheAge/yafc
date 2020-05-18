using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text.Json;

namespace YAFC.Model
{
    public class Project : ModelObject
    {
        public static readonly Version currentYafcVersion = new Version(0, 4);
        public uint projectVersion => undo.version;
        public string attachedFileName { get; private set; }
        public bool justCreated { get; private set; } = true;
        public ProjectSettings settings { get; }
        public string yafcVersion { get; set; }
        public List<ProjectPage> pages { get; } = new List<ProjectPage>();
        public List<string> displayPages { get; } = new List<string>();
        private Dictionary<string, ProjectPage> pagesByGuid;
        public int hiddenPages { get; private set; }
        public new UndoSystem undo => base.undo;
        private uint lastSavedVersion;
        public uint unsavedChangesCount => projectVersion - lastSavedVersion;

        public Project() : base(new UndoSystem())
        {
            settings = new ProjectSettings(this);
        }

        public event Action metaInfoChanged;

        public override ModelObject ownerObject
        {
            get => null;
            internal set => throw new NotSupportedException();
        }

        private void UpdatePageMapping()
        {
            if (pagesByGuid == null)
                pagesByGuid = new Dictionary<string, ProjectPage>();
            hiddenPages = 0;
            pagesByGuid.Clear();
            foreach (var page in pages)
            {
                pagesByGuid[page.guid] = page;
                page.visible = false;
            }
            foreach (var page in displayPages)
                if (pagesByGuid.TryGetValue(page, out var dpage))
                    dpage.visible = true;
            foreach (var page in pages)
                if (!page.visible)
                    hiddenPages++;
        }

        protected internal override void ThisChanged(bool visualOnly)
        { 
            UpdatePageMapping();
            base.ThisChanged(visualOnly);
            metaInfoChanged?.Invoke();
        }

        public static Project ReadFromFile(string path, ErrorCollector collector)
        {
            Project proj;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var reader = new Utf8JsonReader(File.ReadAllBytes(path));
                reader.Read();
                var context = new DeserializationContext(collector);
                proj = SerializationMap<Project>.DeserializeFromJson(null, ref reader, context);
                if (!reader.IsFinalBlock)
                    collector.Error("Json was not consumed to the end!", ErrorSeverity.MajorDataLoss);
                if (proj == null)
                    throw new SerializationException("Unable to load project file");
                proj.justCreated = false;
                var version = new Version(proj.yafcVersion ?? "0.0");
                if (version != currentYafcVersion)
                {
                    if (version > currentYafcVersion)
                        collector.Error("This file was created with future YAFC version. This may lose data.", ErrorSeverity.SuperImportant);
                    proj.yafcVersion = currentYafcVersion.ToString();
                }
                context.Notify();
            } else proj = new Project();
            proj.attachedFileName = path;
            proj.lastSavedVersion = proj.projectVersion;
            return proj;
        }

        public void Save(string fileName)
        {
            if (lastSavedVersion == projectVersion && fileName == attachedFileName)
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
            lastSavedVersion = projectVersion;
        }

        public ProjectPage FindPage(string guid)
        {
            if (pagesByGuid == null)
                UpdatePageMapping();
            return pagesByGuid.TryGetValue(guid, out var page) ? page : null;
        }
    }

    public class ProjectSettings : ModelObject<Project>
    {
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
        public ProjectSettings(Project project) : base(project) {}
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