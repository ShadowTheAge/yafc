using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace YAFC.Model
{
    public class AutomationAnalysis : Analysis
    {
        public static readonly AutomationAnalysis Instance = new AutomationAnalysis();
        public Mapping<FactorioObject, bool> automatable;

        private enum ProcessingState : sbyte
        {
            NotAutomatable = -1, Unknown, UnknownInQueue, Automatable
        }

        public override void Compute(Project project, ErrorCollector warnings)
        {
            var time = Stopwatch.StartNew();
            var state = Database.objects.CreateMapping<ProcessingState>();
            state[Database.voidEnergy] = ProcessingState.Automatable;
            var processingQueue = new Queue<FactorioId>(Database.objects.count);
            var unknowns = 0;
            foreach (var recipe in Database.recipes.all)
            {
                var hasAutomatableCrafter = false;
                foreach (var crafter in recipe.crafters)
                {
                    if (crafter != Database.character && crafter.IsAccessible())
                        hasAutomatableCrafter = true;
                }
                if (!hasAutomatableCrafter)
                    state[recipe] = ProcessingState.NotAutomatable;
            }
            
            foreach (var obj in Database.objects.all)
            {
                if (!obj.IsAccessible())
                    state[obj] = ProcessingState.NotAutomatable;
                else if (state[obj] == ProcessingState.Unknown)
                {
                    unknowns++;
                    state[obj] = ProcessingState.UnknownInQueue;
                    processingQueue.Enqueue(obj.id);
                }
            }

            while (processingQueue.Count > 0)
            {
                var index = processingQueue.Dequeue();
                var dependencies = Dependencies.dependencyList[index];
                var automationState = ProcessingState.Automatable;
                foreach (var depGroup in dependencies)
                {
                    if (depGroup.flags.HasFlags(DependencyList.Flags.OneTimeInvestment))
                        continue;
                    if (depGroup.flags.HasFlag(DependencyList.Flags.RequireEverything))
                    {
                        foreach (var element in depGroup.elements)
                            if (state[element] < automationState)
                                automationState = state[element];
                    }
                    else
                    {
                        var localHighest = ProcessingState.NotAutomatable;
                        foreach (var element in depGroup.elements)
                        {
                            if (state[element] > localHighest)
                                localHighest = state[element];
                        }

                        if (localHighest < automationState)
                            automationState = localHighest;
                    }
                }

                if (automationState == ProcessingState.UnknownInQueue)
                    automationState = ProcessingState.Unknown;

                state[index] = automationState;
                if (automationState != ProcessingState.Unknown)
                {
                    unknowns--;
                    foreach (var revDep in Dependencies.reverseDependencies[index])
                    {
                        if (state[revDep] == ProcessingState.Unknown)
                        {
                            processingQueue.Enqueue(revDep);
                            state[revDep] = ProcessingState.UnknownInQueue;
                        }
                    }
                }
            }
            state[Database.voidEnergy] = ProcessingState.NotAutomatable;
            
            Console.WriteLine("Automation analysis (first pass) finished in "+time.ElapsedMilliseconds+" ms. Unknowns left: "+unknowns);
            if (unknowns > 0)
            {
                // TODO run graph analysis if there are any unknowns left... Right now assume they are not automatable
            }
            automatable = state.Remap((_, s) => s == ProcessingState.Automatable);
        }

        public override string description => "Automation analysis tries to find what objects can be automated. Object cannot be automated if it requires looting an entity or manual crafting.";
    }
}