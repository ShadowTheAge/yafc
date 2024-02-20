using System;
using System.Linq;

namespace YAFC.Model {
    public static class TechnologyLoopsFinder {
        public static void FindTechnologyLoops() {
            var graph = new Graph<Technology>();
            foreach (var technology in Database.technologies.all)
                foreach (var preq in technology.prerequisites)
                    graph.Connect(preq, technology);

            var merged = graph.MergeStrongConnectedComponents();
            var loops = false;
            foreach (var m in merged) {
                if (m.userdata.list != null) {
                    Console.WriteLine("Technology loop: " + string.Join(", ", m.userdata.list.Select(x => x.locName)));
                    loops = true;
                }
            }
            if (!loops)
                Console.WriteLine("No technology loops found");
        }
    }
}