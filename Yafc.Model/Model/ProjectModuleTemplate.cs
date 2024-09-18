using System.Collections.Generic;

namespace Yafc.Model;

public class ProjectModuleTemplate : ModelObject<Project> {
    public ProjectModuleTemplate(Project owner, string name) : base(owner) {
        template = new ModuleTemplateBuilder().Build(this);
        this.name = name;
    }

    public ModuleTemplate template { get; set; }
    public FactorioObject? icon { get; set; }
    public string name { get; set; }
    public List<Entity> filterEntities { get; } = [];
}
