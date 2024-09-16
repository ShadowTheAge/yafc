using System.Threading.Tasks;

namespace Yafc.Model;

public class Summary : ProjectPageContents {

    public bool showOnlyIssues { get; set; }

    public Summary(ModelObject page) : base(page) { }

    public override Task<string?> Solve(ProjectPage page) => Task.FromResult<string?>(null);
}
