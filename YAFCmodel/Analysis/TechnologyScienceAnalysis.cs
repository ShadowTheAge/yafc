using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace YAFC.Model
{
    public class TechnologyScienceAnalysis : Analysis
    {
        public static readonly TechnologyScienceAnalysis Instance = new TechnologyScienceAnalysis();
        public Mapping<Technology, Ingredient[]> allSciencePacks { get; private set; }

        public Ingredient GetMaxTechnologyIngredient(Technology tech)
        {
            var list = allSciencePacks[tech];
            Ingredient ingr = null;
            ulong order = 0;
            foreach (var elem in list)
            {
                var elemOrder = Milestones.Instance.milestoneResult[elem.goods.id] - 1;
                if (ingr == null || elemOrder > order)
                {
                    order = elemOrder;
                    ingr = elem;
                }
            }

            return ingr;
        }
        
        public override void Compute(Project project, ErrorCollector warnings)
        {
            var sciencePacks = Database.allSciencePacks;
            var sciencePackIndex = Database.goods.CreateMapping<int>();
            for (var i = 0; i < sciencePacks.Length; i++)
                sciencePackIndex[sciencePacks[i]] = i;
            var sciencePackCount = new Mapping<Technology, float>[sciencePacks.Length];
            for (var i = 0; i < sciencePacks.Length; i++)
                sciencePackCount[i] = Database.technologies.CreateMapping<float>();

            var processing = Database.technologies.CreateMapping<bool>();
            var requirementMap = Database.technologies.CreateMapping<Technology, bool>(Database.technologies);

            var queue = new Queue<Technology>();
            foreach (var tech in Database.technologies.all)
            {
                if (tech.prerequisites.Length == 0)
                {
                    processing[tech] = true;
                    queue.Enqueue(tech);
                }
            }
            var prerequisiteQueue = new Queue<Technology>();

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                
                // Fast processing for the first prerequisite (just copy everything)
                if (current.prerequisites.Length > 0)
                {
                    var firstRequirement = current.prerequisites[0];
                    foreach (var pack in sciencePackCount)
                        pack[current] += pack[firstRequirement];
                    requirementMap.CopyRow(firstRequirement, current);
                }

                requirementMap[current, current] = true;
                prerequisiteQueue.Enqueue(current);

                while (prerequisiteQueue.Count > 0)
                {
                    var prerequisite = prerequisiteQueue.Dequeue();
                    foreach (var ingredient in prerequisite.ingredients)
                    {
                        var science = sciencePackIndex[ingredient.goods];
                        sciencePackCount[science][current] += ingredient.amount * prerequisite.count;
                    }

                    foreach (var prerequisitePrerequisite in prerequisite.prerequisites)
                    {
                        if (!requirementMap[current, prerequisitePrerequisite])
                        {
                            prerequisiteQueue.Enqueue(prerequisitePrerequisite);
                            requirementMap[current, prerequisitePrerequisite] = true;
                        }
                    }
                }

                foreach (var unlocks in Dependencies.reverseDependencies[current])
                {
                    if (Database.objects[unlocks] is Technology tech && !processing[tech])
                    {
                        foreach (var techPreq in tech.prerequisites)
                        {
                            if (!processing[techPreq])
                                goto locked;
                        }

                        processing[tech] = true;
                        queue.Enqueue(tech);
                        
                        locked:;
                    }
                }
            }

            allSciencePacks = Database.technologies.CreateMapping(tech => sciencePackCount.Select((x, id) => x[tech] == 0 ? null : new Ingredient(sciencePacks[id], x[tech])).Where(x => x != null).ToArray());
        }

        public override string description =>
            "Technology analysis calculates the total amount of science packs required for each technology";
    }
}