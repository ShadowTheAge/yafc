using System;
using System.Threading.Tasks;

namespace YAFC.Model
{
    public class ProjectPage : ModelObject
    {
        public readonly Project project;
        public FactorioObject icon { get; set; }
        public string name { get; set; } = "New page";
        public Type contentType { get; }
        public ProjectPageContents content { get; }
        public bool active { get; private set; }
        private uint lastSolvedVersion;
        private uint currentSolvingVersion;
        private uint actualVersion;
        public event Action<bool> contentChanged;

        public ProjectPage(Project project, Type contentType) : base(project)
        {
            this.project = project;
            this.contentType = contentType;
            content = Activator.CreateInstance(contentType, this) as ProjectPageContents;
        }
        
        public void SetActive(bool active)
        {
            this.active = active;
            CheckSolve();
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

        private async void RunSolveJob()
        {
            currentSolvingVersion = actualVersion;
            try
            {
                await content.Solve(this);
            }
            finally
            {
                lastSolvedVersion = currentSolvingVersion;
                currentSolvingVersion = 0;
                CheckSolve();
            }
        }
    }

    public abstract class ProjectPageContents : ModelObject
    {
        protected ProjectPageContents(ModelObject page) : base(page) {}
        public abstract Task Solve(ProjectPage page);
    }
}