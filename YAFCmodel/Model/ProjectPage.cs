using System;

namespace YAFC.Model
{
    public class ProjectPage : ModelObject
    {
        public readonly Project project;
        public FactorioObject icon { get; set; }
        public string name { get; set; } = "New page";
        public Type contentType { get; }
        public ProjectPageContents content { get; }

        public ProjectPage(Project project, Type contentType) : base(project)
        {
            this.project = project;
            this.contentType = contentType;
            content = Activator.CreateInstance(contentType, this) as ProjectPageContents;
        }
    }

    public abstract class ProjectPageContents : ModelObject
    {
        protected ProjectPageContents(ModelObject page) : base(page) {}
    }
}