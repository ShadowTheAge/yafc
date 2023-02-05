using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json;

namespace YAFC.Model
{
    public class Project : ModelObject
    {
        public static Project current { get; set; }
        public static Version currentYafcVersion { get; set; } = new Version(0, 4, 0);
        public uint projectVersion => undo.version;
        public string attachedFileName { get; private set; }
        public bool justCreated { get; private set; } = true;
        public ProjectSettings settings { get; }
        public ProjectPreferences preferences { get; }

        public List<ProjectModuleTemplate> sharedModuleTemplates { get; } = new List<ProjectModuleTemplate>();
        public string yafcVersion { get; set; }
        public List<ProjectPage> pages { get; } = new List<ProjectPage>();
        public List<Guid> displayPages { get; } = new List<Guid>();
        private Dictionary<Guid, ProjectPage> pagesByGuid;
        public int hiddenPages { get; private set; }
        public new UndoSystem undo => base.undo;
        private uint lastSavedVersion;
        public uint unsavedChangesCount => projectVersion - lastSavedVersion;

        public Project() : base(new UndoSystem())
        {
            settings = new ProjectSettings(this);
            preferences = new ProjectPreferences(this);
        }

        public event Action metaInfoChanged;

        public override ModelObject ownerObject
        {
            get => null;
            internal set => throw new NotSupportedException();
        }

        private void UpdatePageMapping()
        {
            hiddenPages = 0;
            if (pagesByGuid == null)
                pagesByGuid = new Dictionary<Guid, ProjectPage>();
            else pagesByGuid.Clear();
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
            foreach (var page in pages)
                page.SetToRecalculate();
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
                        collector.Error("This file was created with future YAFC version. This may lose data.", ErrorSeverity.Important);
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

        public void RecalculateDisplayPages()
        {
            foreach (var page in displayPages)
                FindPage(page)?.SetToRecalculate();
        }

        public (float multiplier, string suffix) ResolveUnitOfMeasure(UnitOfMeasure unit)
        {
            switch (unit)
            {
                case UnitOfMeasure.None: default:
                    return (1f, null);
                case UnitOfMeasure.Percent:
                    return (100f, "%");
                case UnitOfMeasure.Second:
                    return (1f, "s");
                case UnitOfMeasure.PerSecond:
                    return preferences.GetPerTimeUnit();
                case UnitOfMeasure.ItemPerSecond:
                    return preferences.GetItemPerTimeUnit();
                case UnitOfMeasure.FluidPerSecond:
                    return preferences.GetFluidPerTimeUnit();
                case UnitOfMeasure.Megawatt:
                    return (1e6f, "W");
                case UnitOfMeasure.Megajoule:
                    return (1e6f, "J");
                case UnitOfMeasure.Celsius:
                    return (1f, "°");
            }
        }

        public ProjectPage FindPage(Guid guid)
        {
            if (pagesByGuid == null)
                UpdatePageMapping();
            return pagesByGuid.TryGetValue(guid, out var page) ? page : null;
        }

        public void RemovePage(ProjectPage page)
        {
            page.MarkAsDeleted();
            this.RecordUndo().pages.Remove(page);
        }
    }

    public class ProjectSettings : ModelObject<Project>
    {
        public List<FactorioObject> milestones { get; } = new List<FactorioObject>();
        public SortedList<FactorioObject, ProjectPerItemFlags> itemFlags { get; } = new SortedList<FactorioObject, ProjectPerItemFlags>(DataUtils.DeterministicComparer);
        public float miningProductivity { get; set; }
        public int reactorSizeX { get; set; } = 2;
        public int reactorSizeY { get; set; } = 2;
        public event Action<bool> changed;
        protected internal override void ThisChanged(bool visualOnly)
        {
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
        public float GetReactorBonusMultiplier() => 4f - 2f / reactorSizeX - 2f / reactorSizeY;
    }

    public class ProjectPreferences : ModelObject<Project>
    {
        public int time { get; set; } = 1;
        public float itemUnit { get; set; }
        public float fluidUnit { get; set; }
        public ProjectPreferences(Project owner) : base(owner) {}
        public EntityBelt defaultBelt { get; set; }
        public EntityInserter defaultInserter { get; set; }
        public int inserterCapacity { get; set; } = 1;
        public HashSet<FactorioObject> sourceResources { get; } = new HashSet<FactorioObject>();
        public HashSet<FactorioObject> favourites { get; } = new HashSet<FactorioObject>();
        public Technology targetTechnology { get; set; }

        protected internal override void AfterDeserialize()
        {
            base.AfterDeserialize();
            if (defaultBelt == null)
                defaultBelt = Database.allBelts.OrderBy(x => x.beltItemsPerSecond).FirstOrDefault();
            if (defaultInserter == null)
                defaultInserter = Database.allInserters.OrderBy(x => x.energy.type).ThenBy(x => 1f/x.inserterSwingTime).FirstOrDefault();
        }

        public (float multiplier, string suffix) GetTimeUnit()
        {
            switch (time)
            {
                case 1: case 0:
                    return (1f, "s");
                case 60:
                    return (1f/60f, "m");
                case 3600:
                    return (1f/3600f, "h");
                default:
                    return (1f/time, "t");
            }
        }

        public (float multiplier, string suffix) GetPerTimeUnit()
        {
            switch (time)
            {
                case 1: case 0:
                    return (1f, "/s");
                case 60:
                    return (60f, "/m");
                case 3600:
                    return (3600f, "/h");
                default:
                    return ((float) time, "/t");
            }
        }

        public (float multiplier, string suffix) GetItemPerTimeUnit()
        {
            if (itemUnit == 0f)
                return GetPerTimeUnit();
            return ((1f/itemUnit), "b");
        }
        
        public (float multiplier, string suffix) GetFluidPerTimeUnit()
        {
            if (fluidUnit == 0f)
                return GetPerTimeUnit();
            return ((1f/fluidUnit), "p");
        }

        public void SetSourceResource(Goods goods, bool value)
        {
            this.RecordUndo();
            if (value)
                sourceResources.Add(goods);
            else sourceResources.Remove(goods);
        }

        protected internal override void ThisChanged(bool visualOnly)
        {
            // Don't propagate preferences changes to project
        }

        public void ToggleFavourite(FactorioObject obj)
        {
            this.RecordUndo(true);
            if (favourites.Contains(obj))
                favourites.Remove(obj);
            else favourites.Add(obj);
        }
    }

    [Flags]
    public enum ProjectPerItemFlags
    {
        MilestoneUnlocked = 1 << 0,
        MarkedAccessible = 1 << 1,
        MarkedInaccessible = 1 << 2,
    }
}