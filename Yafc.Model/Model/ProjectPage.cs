using System;
using System.Threading.Tasks;
using Yafc.UI;

namespace Yafc.Model;
public class ProjectPage : ModelObject<Project> {
    public FactorioObject? icon { get; set; }
    public string name { get; set; } = "New page";
    public Guid guid { get; private set; }
    public Type contentType { get; }
    public ProjectPageContents content { get; }
    public bool active { get; private set; }
    public bool visible { get; internal set; }
    [SkipSerialization] public string? modelError { get; set; }
    public bool deleted { get; private set; }
    [SkipSerialization]
    public bool canDelete => contentType != typeof(Summary);

    private uint lastSolvedVersion;
    private uint currentSolvingVersion;
    private uint actualVersion;
    public event Action<bool>? contentChanged;

    public ProjectPage(Project project, Type contentType, Guid guid = default) : base(project) {
        this.guid = guid == default ? Guid.NewGuid() : guid;
        actualVersion = project.projectVersion;
        this.contentType = contentType;
        content = Activator.CreateInstance(contentType, this) as ProjectPageContents
            ?? throw new ArgumentException($"{nameof(contentType)} must derive from {nameof(ProjectPageContents)}", nameof(contentType));
    }

    protected internal override void AfterDeserialize() {
        base.AfterDeserialize();
        deleted = false;
    }

    internal void MarkAsDeleted() {
        deleted = true;
    }

    public void GenerateNewGuid() {
        guid = Guid.NewGuid();
    }

    public void SetActive(bool active) {
        this.active = active;
        if (active) {
            CheckSolve();
        }
    }

    public void SetToRecalculate() {
        lastSolvedVersion = 0;
        if (currentSolvingVersion > 0) {
            currentSolvingVersion = 1;
        }
        else {
            CheckSolve();
        }
    }

    public void ContentChanged(bool visualOnly) {
        if (!visualOnly) {
            actualVersion = hierarchyVersion;
            CheckSolve();
        }
        contentChanged?.Invoke(visualOnly);
    }

    private Task CheckSolve() {
        if (active && IsSolutionStale()) {
            return RunSolveJob();
        }
        return Task.CompletedTask;
    }

    public bool IsSolutionStale() {
        return content != null && actualVersion > lastSolvedVersion && currentSolvingVersion == 0;
    }

    protected internal override void ThisChanged(bool visualOnly) {
        // Don't propagate page changes to project
    }

    public async Task<string?> ExternalSolve() {
        if (!IsSolutionStale()) {
            return modelError;
        }

        currentSolvingVersion = actualVersion;
        try {
            string? error = await content.Solve(this);
            await Ui.EnterMainThread();
            return error;
        }
        finally {
            await Ui.EnterMainThread();
            lastSolvedVersion = currentSolvingVersion;
            currentSolvingVersion = 0;
        }
    }

    public async Task RunSolveJob() {
        modelError = await ExternalSolve();
        contentChanged?.Invoke(false);
        await CheckSolve();
    }
}

public abstract class ProjectPageContents(ModelObject page) : ModelObject<ModelObject>(page) {
    public virtual void InitNew() { }
    public abstract Task<string?> Solve(ProjectPage page);

    protected internal override void ThisChanged(bool visualOnly) {
        if (owner is ProjectPage page) {
            page.ContentChanged(visualOnly);
        }

        base.ThisChanged(visualOnly);
    }
}
