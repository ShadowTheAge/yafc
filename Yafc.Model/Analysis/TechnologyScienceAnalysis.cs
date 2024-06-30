using System.Collections.Generic;
using System.Linq;

namespace Yafc.Model {
    public class TechnologyScienceAnalysis : Analysis {
        public static readonly TechnologyScienceAnalysis Instance = new TechnologyScienceAnalysis();
        public Mapping<Technology, Ingredient[]> allSciencePacks { get; private set; }

        public Ingredient? GetMaxTechnologyIngredient(Technology tech) {
            Ingredient[] list = allSciencePacks[tech];
            Ingredient? ingredient = null;
            Bits order = new Bits();
            foreach (Ingredient entry in list) {
                Bits entryOrder = Milestones.Instance.GetMilestoneResult(entry.goods.id);
                if (entryOrder != 0) {
                    entryOrder -= 1;
                }// else: The science pack is not accessible *and* not a milestone. We may still display it, but any actual milestone will win.

                if (ingredient == null || entryOrder > order) {
                    order = entryOrder;
                    ingredient = entry;
                }
            }

            return ingredient;
        }

        public override void Compute(Project project, ErrorCollector warnings) {
            var sciencePacks = Database.allSciencePacks;
            var sciencePackIndex = Database.goods.CreateMapping<int>();
            for (int i = 0; i < sciencePacks.Length; i++) {
                sciencePackIndex[sciencePacks[i]] = i;
            }

            Mapping<Technology, float>[] sciencePackCount = new Mapping<Technology, float>[sciencePacks.Length];
            for (int i = 0; i < sciencePacks.Length; i++) {
                sciencePackCount[i] = Database.technologies.CreateMapping<float>();
            }

            var processing = Database.technologies.CreateMapping<bool>();
            var requirementMap = Database.technologies.CreateMapping<Technology, bool>(Database.technologies);

            Queue<Technology> queue = new Queue<Technology>();
            foreach (Technology tech in Database.technologies.all.ExceptExcluded(this)) {
                if (tech.prerequisites.Length == 0) {
                    processing[tech] = true;
                    queue.Enqueue(tech);
                }
            }
            Queue<Technology> prerequisiteQueue = new Queue<Technology>();

            while (queue.Count > 0) {
                var current = queue.Dequeue();

                // Fast processing for the first prerequisite (just copy everything)
                if (current.prerequisites.Length > 0) {
                    var firstRequirement = current.prerequisites[0];
                    foreach (var pack in sciencePackCount) {
                        pack[current] += pack[firstRequirement];
                    }

                    requirementMap.CopyRow(firstRequirement, current);
                }

                requirementMap[current, current] = true;
                prerequisiteQueue.Enqueue(current);

                while (prerequisiteQueue.Count > 0) {
                    var prerequisite = prerequisiteQueue.Dequeue();
                    foreach (var ingredient in prerequisite.ingredients) {
                        int science = sciencePackIndex[ingredient.goods];
                        sciencePackCount[science][current] += ingredient.amount * prerequisite.count;
                    }

                    foreach (var prerequisitePrerequisite in prerequisite.prerequisites) {
                        if (!requirementMap[current, prerequisitePrerequisite]) {
                            prerequisiteQueue.Enqueue(prerequisitePrerequisite);
                            requirementMap[current, prerequisitePrerequisite] = true;
                        }
                    }
                }

                foreach (var unlocks in Dependencies.reverseDependencies[current]) {
                    if (Database.objects[unlocks] is Technology tech && !processing[tech]) {
                        foreach (var techPrerequisite in tech.prerequisites) {
                            if (!processing[techPrerequisite]) {
                                goto locked;
                            }
                        }

                        processing[tech] = true;
                        queue.Enqueue(tech);

locked:;
                    }
                }
            }

            allSciencePacks = Database.technologies.CreateMapping(tech => sciencePackCount.Select((x, id) => x[tech] == 0 ? null : new Ingredient(sciencePacks[id], x[tech])).WhereNotNull().ToArray());
        }

        public override string description =>
            "Technology analysis calculates the total amount of science packs required for each technology";
    }
}
