using System.Threading.Tasks;

namespace YAFC.Model
{
    public class AutoPlanner : ProjectPageContents
    {
        public AutoPlanner(ModelObject page) : base(page) {}

        public override Task Solve(ProjectPage page)
        {
            throw new System.NotImplementedException();
        }
    }
}