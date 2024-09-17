using System.Threading.Tasks;

namespace Yafc.Model;

public class Summary(ModelObject page) : ProjectPageContents(page) {

    public bool showOnlyIssues { get; set; }

    public override Task<string?> Solve(ProjectPage page) => Task.FromResult<string?>(null);
}
