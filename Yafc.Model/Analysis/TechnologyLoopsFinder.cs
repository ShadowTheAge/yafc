using System;
using System.Linq;

namespace Yafc.Model {
    public static class TechnologyLoopsFinder {
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
                    Console.WriteLine("Technology loop: " + string.Join(", ", m.userData.list.Select(x => x.locName)));
                    loops = true;
                }
            }
            if (!loops) {
                Console.WriteLine("No technology loops found");
            }
        }
    }
}
