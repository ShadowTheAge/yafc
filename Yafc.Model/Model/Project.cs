using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json;

namespace Yafc.Model;

public class Project : ModelObject {
    public static Project current { get; set; } = null!; // null-forgiving: MainScreen.SetProject will set this to a non-null value
    public static Version currentYafcVersion { get; set; } = new Version(0, 4, 0);
    public uint projectVersion => undo.version;
    public string? attachedFileName { get; private set; }
    public bool justCreated { get; private set; } = true;
    public ProjectSettings settings { get; }
    public ProjectPreferences preferences { get; }

    public List<ProjectModuleTemplate> sharedModuleTemplates { get; } = [];
    public string? yafcVersion { get; set; }
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

    public event Action? metaInfoChanged;

    public override ModelObject? ownerObject {
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
        Project? proj;

        if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
            proj = Read(File.ReadAllBytes(path), collector);
        }
        else {
            proj = new Project();
        }

        proj.attachedFileName = path;
        proj.lastSavedVersion = proj.projectVersion;

        return proj;
    }

    public static Project Read(byte[] bytes, ErrorCollector collector) {
        Project? proj;
        Utf8JsonReader reader = new Utf8JsonReader(bytes);
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

        return proj;
    }

    public void Save(string fileName) {
        if (lastSavedVersion == projectVersion && fileName == attachedFileName) {
            return;
        }

        using (FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write)) {
            Save(fs);
        }
        attachedFileName = fileName;
        lastSavedVersion = projectVersion;
    }

    public void Save(Stream stream) {
        using Utf8JsonWriter writer = new Utf8JsonWriter(stream, JsonUtils.DefaultWriterOptions);
        SerializationMap<Project>.SerializeToJson(this, writer);
    }

    public void RecalculateDisplayPages() {
        foreach (var page in displayPages) {
            FindPage(page)?.SetToRecalculate();
        }
    }

    public (float multiplier, string suffix) ResolveUnitOfMeasure(UnitOfMeasure unit) => unit switch {
        UnitOfMeasure.Percent => (100f, "%"),
        UnitOfMeasure.Second => (1f, "s"),
        UnitOfMeasure.PerSecond => preferences.GetPerTimeUnit(),
        UnitOfMeasure.ItemPerSecond => preferences.GetItemPerTimeUnit(),
        UnitOfMeasure.FluidPerSecond => preferences.GetFluidPerTimeUnit(),
        UnitOfMeasure.Megawatt => (1e6f, "W"),
        UnitOfMeasure.Megajoule => (1e6f, "J"),
        UnitOfMeasure.Celsius => (1f, "°"),
        _ => (1f, ""),
    };

    public ProjectPage? FindPage(Guid guid) => pagesByGuid.TryGetValue(guid, out var page) ? page : null;

    public void RemovePage(ProjectPage page) {
        page.MarkAsDeleted();
        _ = this.RecordUndo();
        _ = pages.Remove(page);
        _ = displayPages.Remove(page.guid);
    }

    /// <summary>
    /// Get the page that is visually next (i.e. to the right of the current selected page on the tab bar)
    /// from the specified one.
    /// </summary>
    /// <param name="currentPage">The page to get the next page from (probably the current page).
    /// This is a nullable parameter because sometimes there isn't a current page at all; if it's
    /// null, we'll return the first display page (if any).</param>
    /// <param name="forward">Whether to move visually-forward (true), i.e. left to right, or
    /// visually-backward (false), i.e. right-to-left.</param>
    /// <returns>The page object that should be set to active.</returns>
    public ProjectPage? VisibleNeighborOfPage(ProjectPage? currentPage, bool forward) {
        if (currentPage == null) {
            if (displayPages.Count == 0) {
                return null;
            }
            return pagesByGuid[displayPages.First()];
        }

        var currentGuid = currentPage.guid;
        int currentVisualIndex = displayPages.IndexOf(currentGuid);

        return pagesByGuid[displayPages[forward ? nextVisualIndex() : previousVisualIndex()]];

        int nextVisualIndex() {
            int naiveNextVisualIndex = currentVisualIndex + 1;
            return naiveNextVisualIndex >= displayPages.Count ? 0 : naiveNextVisualIndex;
        }

        int previousVisualIndex() {
            int naivePreviousVisualIndex = currentVisualIndex - 1;
            return naivePreviousVisualIndex < 0 ? displayPages.Count - 1 : naivePreviousVisualIndex;
        }
    }

    /// <summary> Swaps the specified two pages in tab order. </summary>
    public void ReorderPages(ProjectPage? page1, ProjectPage? page2) {
        if (page1 is null || page2 is null) {
            return;
        }

        _ = this.RecordUndo();
        int index1 = displayPages.IndexOf(page1.guid);
        int index2 = displayPages.IndexOf(page2.guid);

        if (index1 == -1 || index2 == -1 || index1 == index2) {
            return;
        }

        displayPages[index1] = page2.guid;
        displayPages[index2] = page1.guid;
    }
}

public class ProjectSettings(Project project) : ModelObject<Project>(project) {
    public List<FactorioObject> milestones { get; } = [];
    public SortedList<FactorioObject, ProjectPerItemFlags> itemFlags { get; } = new SortedList<FactorioObject, ProjectPerItemFlags>(DataUtils.DeterministicComparer);
    public float miningProductivity { get; set; }
    public float researchSpeedBonus { get; set; }
    public float researchProductivity { get; set; }
    public int reactorSizeX { get; set; } = 2;
    public int reactorSizeY { get; set; } = 2;
    public float PollutionCostModifier { get; set; } = 0;
    public event Action<bool>? changed;
    protected internal override void ThisChanged(bool visualOnly) => changed?.Invoke(visualOnly);

    public void SetFlag(FactorioObject obj, ProjectPerItemFlags flag, bool set) {
        _ = itemFlags.TryGetValue(obj, out var flags);
        var newFlags = set ? flags | flag : flags & ~flag;

        if (newFlags != flags) {
            _ = this.RecordUndo();
            itemFlags[obj] = newFlags;
        }
    }

    public ProjectPerItemFlags Flags(FactorioObject obj) => itemFlags.TryGetValue(obj, out var val) ? val : 0;

    public float GetReactorBonusMultiplier() => 4f - (2f / reactorSizeX) - (2f / reactorSizeY);
}

public class ProjectPreferences(Project owner) : ModelObject<Project>(owner) {
    public int time { get; set; } = 1;
    public float itemUnit { get; set; }
    public float fluidUnit { get; set; }
    public EntityBelt? defaultBelt { get; set; }
    public EntityInserter? defaultInserter { get; set; }
    public int inserterCapacity { get; set; } = 1;
    public HashSet<FactorioObject> sourceResources { get; } = [];
    public HashSet<FactorioObject> favorites { get; } = [];
    /// <summary> Target technology for cost analysis - required item counts are estimated for researching this. If null, the is to research all finite technologies. </summary>
    public Technology? targetTechnology { get; set; }
    /// <summary>
    /// The scale to use when drawing icons that have information stored in their background color, stored as a ratio from 0 to 1.
    /// </summary>
    public float iconScale { get; set; } = .9f;

    protected internal override void AfterDeserialize() {
        base.AfterDeserialize();
        defaultBelt ??= Database.allBelts.OrderBy(x => x.beltItemsPerSecond).FirstOrDefault();
        defaultInserter ??= Database.allInserters.OrderBy(x => x.energy.type).ThenBy(x => 1f / x.inserterSwingTime).FirstOrDefault();
    }

    public (float multiplier, string suffix) GetTimeUnit() => time switch {
        1 or 0 => (1f, "s"),
        60 => (1f / 60f, "m"),
        3600 => (1f / 3600f, "h"),
        _ => (1f / time, "t"),
    };

    public (float multiplier, string suffix) GetPerTimeUnit() => time switch {
        1 or 0 => (1f, "/s"),
        60 => (60f, "/m"),
        3600 => (3600f, "/h"),
        _ => (time, "/t"),
    };

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
