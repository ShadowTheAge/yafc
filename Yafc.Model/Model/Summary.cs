using System.Threading.Tasks;
using Yafc.Model;

namespace YAFC.Model {
    public class Summary : ProjectPageContents {

        public bool showOnlyIssues { get; set; }

        public Summary(ModelObject page) : base(page) { }

        public override Task<string?> Solve(ProjectPage page) {
            return Task.FromResult<string?>(null);
        }
    }
}
