using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace YAFC.Model
{
    public static class AutomationAnalysis
    {
        private static Mapping<FactorioObject, bool> automatable;

        public static bool IsAutomatable(this FactorioObject obj) => automatable[obj];
        
        private enum ProcessingState : byte
        {
            None, InQueue, Automatable, NotAutomatable
        }

        public static void Process()
        {
            var time = Stopwatch.StartNew();
            var state = Database.objects.CreateMapping<ProcessingState>();
            var processingQueue = new Queue<int>(Database.recipes.count);
            foreach (var obj in Database.objects.all)
                if (!obj.IsAccessible())
                    state[obj] = ProcessingState.NotAutomatable;
            foreach (var recipe in Database.recipes.all)
            {
                if (state[recipe] == ProcessingState.NotAutomatable)
                    continue;
                var hasAutomatableCrafter = false;
                foreach (var crafter in recipe.crafters)
                {
                    if (crafter != Database.character && crafter.IsAccessible())
                        hasAutomatableCrafter = true;
                }
                if (!hasAutomatableCrafter)
                    state[recipe] = ProcessingState.NotAutomatable;
                else
                {
                    state[recipe] = ProcessingState.InQueue;
                    processingQueue.Enqueue(recipe.id);
                }
            }

            while (processingQueue.Count > 0)
            {
                var index = processingQueue.Dequeue();
                var dependencies = Dependencies.dependencyList[index];
                var automatable = true;
                foreach (var depGroup in dependencies)
                {
                    if ((depGroup.flags & DependencyList.Flags.OneTimeInvestment) != 0)
                        continue;
                    if ((depGroup.flags & DependencyList.Flags.RequireEverything) != 0)
                    {
                        foreach (var element in depGroup.elements)
                            if (state[element] != ProcessingState.Automatable)
                            {
                                automatable = false;
                                break;
                            }
                    }
                    else
                    {
                        automatable = false;
                        foreach (var element in depGroup.elements)
                            if (state[element] == ProcessingState.Automatable)
                            {
                                automatable = true;
                                break;
                            }
                    }
                    if (!automatable)
                        break;
                }
                state[index] = automatable ? ProcessingState.Automatable : ProcessingState.None;
                if (automatable)
                {
                    foreach (var revDep in Dependencies.reverseDependencies[index])
                    {
                        if (state[revDep] == ProcessingState.None)
                        {
                            processingQueue.Enqueue(revDep);
                            state[revDep] = ProcessingState.InQueue;
                        }
                    }
                }
            }

            var result = Database.objects.CreateMapping<bool>();
            for (var i = 0; i < result.Count; i++)
                if (state[i] == ProcessingState.Automatable)
                    result[i] = true;
            automatable = result;
            Console.WriteLine("Automation analysis finished in "+time.ElapsedMilliseconds+" ms");
        }
    }
}