using System;
using System.Threading.Tasks;
using YAFC.UI;

namespace YAFC.Model
{
    public class ProjectPage : ModelObject<Project>
    {
        public FactorioObject icon { get; set; }
        public string name { get; set; } = "New page";
        public Guid guid { get; private set; }
        public Type contentType { get; }
        public ProjectPageContents content { get; }
        public bool active { get; private set; }
        public bool visible { get; internal set; }
        [SkipSerialization] public string modelError { get; set; }

        private uint lastSolvedVersion;
        private uint currentSolvingVersion;
        private uint actualVersion;
        public event Action<bool> contentChanged;

        public ProjectPage(Project project, Type contentType, Guid guid = default) : base(project)
        {
            this.guid = guid == default ? Guid.NewGuid() : guid;
            actualVersion = project.projectVersion;
            this.contentType = contentType;
            content = Activator.CreateInstance(contentType, this) as ProjectPageContents;
        }

        public void GenerateNewGuid()
        {
            guid = Guid.NewGuid();
        }
        
        public void SetActive(bool active)
        {
            this.active = active;
            if (active)
                CheckSolve();
        }

        public void SetToRecalculate()
        {
            lastSolvedVersion = 0;
            if (currentSolvingVersion > 0)
                currentSolvingVersion = 1;
            else CheckSolve();
        }

        public void ContentChanged(bool visualOnly)
        {
            if (!visualOnly)
            {
                actualVersion = hierarchyVersion;
                CheckSolve();
            }
            contentChanged?.Invoke(visualOnly);
        }

        private void CheckSolve()
        {
            if (active && content != null && actualVersion > lastSolvedVersion && currentSolvingVersion == 0)
                RunSolveJob();
        }

        protected internal override void ThisChanged(bool visualOnly)
        {
            // Dont propagate page changes to project
        }

        private async void RunSolveJob()
        {
            currentSolvingVersion = actualVersion;
            try
            {
                var error = await content.Solve(this);
                await Ui.EnterMainThread();
                if (modelError != error)
                    modelError = error;
                contentChanged?.Invoke(false);
            }
            finally
            {
                await Ui.EnterMainThread();
                lastSolvedVersion = currentSolvingVersion;
                currentSolvingVersion = 0;
                CheckSolve();
            }
        }
    }

    public abstract class ProjectPageContents : ModelObject<ModelObject>
    {
        protected ProjectPageContents(ModelObject page) : base(page) {}
        public abstract Task<string> Solve(ProjectPage page);
    }
}