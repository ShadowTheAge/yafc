using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace YAFC.Model
{
    public enum AutomationStatus : sbyte
    {
        NotAutomatable = -1, AutomatableLater = 2, AutomatableNow = 3,
    }
    
    public class AutomationAnalysis : Analysis
    {
        public static readonly AutomationAnalysis Instance = new AutomationAnalysis();
        public Mapping<FactorioObject, AutomationStatus> automatable;

        private const AutomationStatus Unknown = (AutomationStatus) 0;
        private const AutomationStatus UnknownInQueue = (AutomationStatus) 1;

        public override void Compute(Project project, ErrorCollector warnings)
        {
            var time = Stopwatch.StartNew();
            var state = Database.objects.CreateMapping<AutomationStatus>();
            state[Database.voidEnergy] = AutomationStatus.AutomatableNow;
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
                    state[recipe] = AutomationStatus.NotAutomatable;
            }
            
            foreach (var obj in Database.objects.all)
            {
                if (!obj.IsAccessible())
                    state[obj] = AutomationStatus.NotAutomatable;
                else if (state[obj] == Unknown)
                {
                    unknowns++;
                    state[obj] = UnknownInQueue;
                    processingQueue.Enqueue(obj.id);
                }
            }

            while (processingQueue.Count > 0)
            {
                var index = processingQueue.Dequeue();
                var dependencies = Dependencies.dependencyList[index];
                var automationState = Milestones.Instance.IsAccessibleWithCurrentMilesones(index) ? AutomationStatus.AutomatableNow : AutomationStatus.AutomatableLater;
                if (Database.objects[index].name == "iron-ore")
                    ;
                foreach (var depGroup in dependencies)
                {
                    if (depGroup.flags.HasFlags(DependencyList.Flags.OneTimeInvestment))
                    {
                        if (automationState < AutomationStatus.AutomatableNow)
                            continue;
                        if (!depGroup.flags.HasFlags(DependencyList.Flags.RequireEverything))
                        {
                            var isAccessible = false;
                            foreach (var element in depGroup.elements)
                            {
                                if (Milestones.Instance.IsAccessibleWithCurrentMilesones(element))
                                {
                                    isAccessible = true;
                                    break;
                                }
                            }

                            if (!isAccessible)
                                automationState = AutomationStatus.AutomatableLater;
                        }
                    }
                    else
                    {
                        if (depGroup.flags.HasFlags(DependencyList.Flags.RequireEverything))
                        {
                            foreach (var element in depGroup.elements)
                                if (state[element] < automationState)
                                    automationState = state[element];
                        }
                        else
                        {
                            var localHighest = AutomationStatus.NotAutomatable;
                            foreach (var element in depGroup.elements)
                            {
                                if (state[element] > localHighest)
                                    localHighest = state[element];
                            }

                            if (localHighest < automationState)
                                automationState = localHighest;
                        }
                    }
                }

                if (automationState == UnknownInQueue)
                    automationState = Unknown;

                state[index] = automationState;
                if (automationState != Unknown)
                {
                    unknowns--;
                    foreach (var revDep in Dependencies.reverseDependencies[index])
                    {
                        var oldState = state[revDep];
                        if (oldState == Unknown || oldState == AutomationStatus.AutomatableLater && automationState == AutomationStatus.AutomatableNow)
                        {
                            processingQueue.Enqueue(revDep);
                            state[revDep] = UnknownInQueue;
                        }
                    }
                }
            }
            state[Database.voidEnergy] = AutomationStatus.NotAutomatable;
            
            Console.WriteLine("Automation analysis (first pass) finished in "+time.ElapsedMilliseconds+" ms. Unknowns left: "+unknowns);
            if (unknowns > 0)
            {
                // TODO run graph analysis if there are any unknowns left... Right now assume they are not automatable
                foreach (var (k, v) in state)
                {
                    if (v == Unknown)
                        state[k] = AutomationStatus.NotAutomatable;
                }
            }
            automatable = state;
        }

        public override string description => "Automation analysis tries to find what objects can be automated. Object cannot be automated if it requires looting an entity or manual crafting.";
    }
}