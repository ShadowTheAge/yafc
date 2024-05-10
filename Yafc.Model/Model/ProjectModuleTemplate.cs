using System.Collections.Generic;

namespace Yafc.Model {
    public class ProjectModuleTemplate : ModelObject<Project> {
        public ProjectModuleTemplate(Project owner) : base(owner) {
            template = new ModuleTemplate(this);
        }

        public ModuleTemplate template { get; }
        public FactorioObject icon { get; set; }
        public string name { get; set; }
        public List<Entity> filterEntities { get; } = [];
    }
}
