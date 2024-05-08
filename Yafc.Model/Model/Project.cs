using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json;

namespace Yafc.Model {
    public class Project : ModelObject {
        public static Project current { get; set; }
        public static Version currentYafcVersion { get; set; } = new Version(0, 4, 0);
        public uint projectVersion => undo.version;
        public string attachedFileName { get; private set; }
        public bool justCreated { get; private set; } = true;
        public ProjectSettings settings { get; }
        public ProjectPreferences preferences { get; }

        public List<ProjectModuleTemplate> sharedModuleTemplates { get; } = [];
        public string yafcVersion { get; set; }
        public List<ProjectPage> pages { get; } = [];
        public List<Guid> displayPages { get; } = [];
        private readonly Dictionary<Guid, ProjectPage> pagesByGuid = [];
        public int hiddenPages { get; private set; }
        public new UndoSystem undo => base.undo;
        private uint lastSavedVersion;
        public uint unsavedChangesCount => projectVersion - lastSavedVersion;

        public Project() : base(new UndoSystem()) {
            settings = new ProjectSettings(this);
            preferences = new ProjectPreferences(this);
        }

        public event Action metaInfoChanged;

        public override ModelObject ownerObject {
            get => null;
            internal set => throw new NotSupportedException();
        }

        private void UpdatePageMapping() {
            hiddenPages = 0;
            pagesByGuid.Clear();
            foreach (var page in pages) {
                pagesByGuid[page.guid] = page;
                page.visible = false;
            }
            foreach (var page in displayPages) {
                if (pagesByGuid.TryGetValue(page, out var dpage)) {
                    dpage.visible = true;
                }
            }

            foreach (var page in pages) {
                if (!page.visible) {
                    hiddenPages++;
                }
            }
        }

        protected internal override void ThisChanged(bool visualOnly) {
            UpdatePageMapping();
            base.ThisChanged(visualOnly);
            foreach (var page in pages) {
                page.SetToRecalculate();
            }

            metaInfoChanged?.Invoke();
        }

        public static Project ReadFromFile(string path, ErrorCollector collector) {
            Project proj;
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
                Utf8JsonReader reader = new Utf8JsonReader(File.ReadAllBytes(path));
                _ = reader.Read();
                DeserializationContext context = new DeserializationContext(collector);
                proj = SerializationMap<Project>.DeserializeFromJson(null, ref reader, context);
                if (!reader.IsFinalBlock) {
                    collector.Error("Json was not consumed to the end!", ErrorSeverity.MajorDataLoss);
                }

                if (proj == null) {
                    throw new SerializationException("Unable to load project file");
                }

                proj.justCreated = false;
                Version version = new Version(proj.yafcVersion ?? "0.0");
                if (version != currentYafcVersion) {
                    if (version > currentYafcVersion) {
                        collector.Error("This file was created with future YAFC version. This may lose data.", ErrorSeverity.Important);
                    }

                    proj.yafcVersion = currentYafcVersion.ToString();
                }
                context.Notify();
            }
            else {
                proj = new Project();
            }

            proj.attachedFileName = path;
            proj.lastSavedVersion = proj.projectVersion;
            return proj;
        }

        public void Save(string fileName) {
            if (lastSavedVersion == projectVersion && fileName == attachedFileName) {
                return;
            }

            using (MemoryStream ms = new MemoryStream()) {
                using (Utf8JsonWriter writer = new Utf8JsonWriter(ms, JsonUtils.DefaultWriterOptions)) {
                    SerializationMap<Project>.SerializeToJson(this, writer);
                }

                ms.Position = 0;
                using FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
                ms.CopyTo(fs);
            }
            attachedFileName = fileName;
            lastSavedVersion = projectVersion;
        }

        public void RecalculateDisplayPages() {
            foreach (var page in displayPages) {
                FindPage(page)?.SetToRecalculate();
            }
        }

        public (float multiplier, string suffix) ResolveUnitOfMeasure(UnitOfMeasure unit) {
            return unit switch {
                UnitOfMeasure.Percent => (100f, "%"),
                UnitOfMeasure.Second => (1f, "s"),
                UnitOfMeasure.PerSecond => preferences.GetPerTimeUnit(),
                UnitOfMeasure.ItemPerSecond => preferences.GetItemPerTimeUnit(),
                UnitOfMeasure.FluidPerSecond => preferences.GetFluidPerTimeUnit(),
                UnitOfMeasure.Megawatt => (1e6f, "W"),
                UnitOfMeasure.Megajoule => (1e6f, "J"),
                UnitOfMeasure.Celsius => (1f, "°"),
                _ => (1f, null),
            };
        }

        public ProjectPage FindPage(Guid guid) {
            if (pagesByGuid == null) {
                UpdatePageMapping();
            }

            return pagesByGuid.TryGetValue(guid, out var page) ? page : null;
        }

        public void RemovePage(ProjectPage page) {
            page.MarkAsDeleted();
            _ = this.RecordUndo().pages.Remove(page);
        }
    }

    public class ProjectSettings(Project project) : ModelObject<Project>(project) {
        public List<FactorioObject> milestones { get; } = [];
        public SortedList<FactorioObject, ProjectPerItemFlags> itemFlags { get; } = new SortedList<FactorioObject, ProjectPerItemFlags>(DataUtils.DeterministicComparer);
        public float miningProductivity { get; set; }
        public int reactorSizeX { get; set; } = 2;
        public int reactorSizeY { get; set; } = 2;
        public float PollutionCostModifier { get; set; } = 0;
        public event Action<bool> changed;
        protected internal override void ThisChanged(bool visualOnly) {
            changed?.Invoke(visualOnly);
        }

        public void SetFlag(FactorioObject obj, ProjectPerItemFlags flag, bool set) {
            _ = itemFlags.TryGetValue(obj, out var flags);
            var newFlags = set ? flags | flag : flags & ~flag;
            if (newFlags != flags) {
                _ = this.RecordUndo();
                itemFlags[obj] = newFlags;
            }
        }

        public ProjectPerItemFlags Flags(FactorioObject obj) {
            return itemFlags.TryGetValue(obj, out var val) ? val : 0;
        }

        public float GetReactorBonusMultiplier() {
            return 4f - (2f / reactorSizeX) - (2f / reactorSizeY);
        }
    }

    public class ProjectPreferences(Project owner) : ModelObject<Project>(owner) {
        public int time { get; set; } = 1;
        public float itemUnit { get; set; }
        public float fluidUnit { get; set; }
        public EntityBelt defaultBelt { get; set; }
        public EntityInserter defaultInserter { get; set; }
        public int inserterCapacity { get; set; } = 1;
        public HashSet<FactorioObject> sourceResources { get; } = [];
        public HashSet<FactorioObject> favorites { get; } = [];
        /// <summary> Target technology for cost analysis - required item counts are estimated for researching this. If null, the is to research all finite technologies. </summary>
        public Technology targetTechnology { get; set; }

        protected internal override void AfterDeserialize() {
            base.AfterDeserialize();
            defaultBelt ??= Database.allBelts.OrderBy(x => x.beltItemsPerSecond).FirstOrDefault();
            defaultInserter ??= Database.allInserters.OrderBy(x => x.energy.type).ThenBy(x => 1f / x.inserterSwingTime).FirstOrDefault();
        }

        public (float multiplier, string suffix) GetTimeUnit() {
            return time switch {
                1 or 0 => (1f, "s"),
                60 => (1f / 60f, "m"),
                3600 => (1f / 3600f, "h"),
                _ => (1f / time, "t"),
            };
        }

        public (float multiplier, string suffix) GetPerTimeUnit() {
            return time switch {
                1 or 0 => (1f, "/s"),
                60 => (60f, "/m"),
                3600 => (3600f, "/h"),
                _ => (time, "/t"),
            };
        }

        public (float multiplier, string suffix) GetItemPerTimeUnit() {
            if (itemUnit == 0f) {
                return GetPerTimeUnit();
            }

            return (1f / itemUnit, "b");
        }

        public (float multiplier, string suffix) GetFluidPerTimeUnit() {
            if (fluidUnit == 0f) {
                return GetPerTimeUnit();
            }

            return (1f / fluidUnit, "p");
        }

        public void SetSourceResource(Goods goods, bool value) {
            _ = this.RecordUndo();
            if (value) {
                _ = sourceResources.Add(goods);
            }
            else {
                _ = sourceResources.Remove(goods);
            }
        }

        protected internal override void ThisChanged(bool visualOnly) {
            // Don't propagate preferences changes to project
        }

        public void ToggleFavorite(FactorioObject obj) {
            _ = this.RecordUndo(true);
            if (favorites.Contains(obj)) {
                _ = favorites.Remove(obj);
            }
            else {
                _ = favorites.Add(obj);
            }
        }
    }

    [Flags]
    public enum ProjectPerItemFlags {
        MilestoneUnlocked = 1 << 0,
        MarkedAccessible = 1 << 1,
        MarkedInaccessible = 1 << 2,
    }
}
