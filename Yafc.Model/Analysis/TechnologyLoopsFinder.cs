using System.Linq;
using Serilog;
using Yafc.UI;

namespace Yafc.Model {
    public static class TechnologyLoopsFinder {
        private static readonly ILogger logger = Logging.GetLogger(typeof(TechnologyLoopsFinder));

        public static void FindTechnologyLoops() {
            Graph<Technology> graph = new Graph<Technology>();
            foreach (var technology in Database.technologies.all) {
                foreach (var prerequisite in technology.prerequisites) {
                    graph.Connect(prerequisite, technology);
                }
            }

            var merged = graph.MergeStrongConnectedComponents();
            bool loops = false;
            foreach (var m in merged) {
                if (m.userData.list != null) {
                    logger.Information("Technology loop: " + string.Join(", ", m.userData.list.Select(x => x.locName)));
                    loops = true;
                }
            }
            if (!loops) {
                logger.Information("No technology loops found");
            }
        }
    }
}
