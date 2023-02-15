using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using YAFC.UI;

namespace YAFC.Model
{
    public class Milestones : Analysis
    {
        public static readonly Milestones Instance = new Milestones();

        public FactorioObject[] currentMilestones;
        public Mapping<FactorioObject, ulong> milestoneResult;
        public ulong lockedMask { get; private set; }
        private Project project;

        public bool IsAccessibleWithCurrentMilesones(FactorioId obj) => (milestoneResult[obj] & lockedMask) == 1;
        public bool IsAccessibleWithCurrentMilesones(FactorioObject obj) => (milestoneResult[obj] & lockedMask) == 1;
        public bool IsAccessibleAtNextMilestone(FactorioObject obj)
        {
            var milestoneMask = milestoneResult[obj] & lockedMask;
            if (milestoneMask == 1)
                return true;
            if ((milestoneMask & 1) != 0)
                return false;
            return ((milestoneMask - 1) & (milestoneMask - 2)) == 0; // milestoneMask is a power of 2 + 1
        }

        private void GetLockedMaskFromProject()
        {
            lockedMask = ~0ul;
            var index = 0;
            foreach (var milestone in currentMilestones)
            {
                index++;
                if (project.settings.Flags(milestone).HasFlags(ProjectPerItemFlags.MilestoneUnlocked))
                    lockedMask &= ~(1ul << index);
            }
        }

        private void ProjectSettingsChanged(bool visualOnly)
        {
            if (!visualOnly)
                GetLockedMaskFromProject();
        }

        public FactorioObject GetHighest(FactorioObject target, bool all)
        {
            if (target == null)
                return null;
            var ms = milestoneResult[target];
            if (!all)
                ms &= lockedMask;
            if (ms == 0)
                return null;
            var msb = MathUtils.HighestBitSet(ms) - 1;
            return msb < 0 || msb >= currentMilestones.Length ? null : currentMilestones[msb];
        }

        [Flags]
        private enum ProcessingFlags : byte
        {
            InQueue = 1,
            Initial = 2,
            MilestoneNeedOrdering = 4,
            ForceInaccessible = 8
        }

        public override void Compute(Project project, ErrorCollector warnings)
        {
            if (project.settings.milestones.Count == 0)
                ComputeWithParameters(project, warnings, Database.allSciencePacks, true);
            else ComputeWithParameters(project, warnings, project.settings.milestones.ToArray(), false);
        }

        public void ComputeWithParameters(Project project, ErrorCollector warnings, FactorioObject[] milestones, bool autoSort)
        {
            if (this.project == null)
            {
                this.project = project;
                project.settings.changed += ProjectSettingsChanged;
            }

            var time = Stopwatch.StartNew();
            var result = Database.objects.CreateMapping<ulong>();
            var processing = Database.objects.CreateMapping<ProcessingFlags>();
            var processingQueue = new Queue<FactorioId>();

            foreach (var rootAccessbile in Database.rootAccessible)
            {
                result[rootAccessbile] = 1;
                processingQueue.Enqueue(rootAccessbile.id);
                processing[rootAccessbile] = ProcessingFlags.Initial | ProcessingFlags.InQueue;
            }

            foreach (var (obj, flag) in project.settings.itemFlags)
            {
                if (flag.HasFlags(ProjectPerItemFlags.MarkedAccessible))
                {
                    result[obj] = 1;
                    processingQueue.Enqueue(obj.id);
                    processing[obj] = ProcessingFlags.Initial | ProcessingFlags.InQueue;
                }
                else if (flag.HasFlag(ProjectPerItemFlags.MarkedInaccessible))
                    processing[obj] = ProcessingFlags.ForceInaccessible;
            }

            if (autoSort)
            {
                // Set special flag to auto-order the milestones, keep currentMilestones empty, as the milestones needs to be added later so they get ordered according to their dependencies
                foreach (var milestone in milestones)
                    processing[milestone] |= ProcessingFlags.MilestoneNeedOrdering;
                currentMilestones = new FactorioObject[milestones.Length];
            }
            else
            {
                currentMilestones = milestones;
                for (var i = 0; i < milestones.Length; i++)
                    result[milestones[i]] = (1ul << (i + 1)) | 1;
            }

            var dependencyList = Dependencies.dependencyList;
            var reverseDependencies = Dependencies.reverseDependencies;
            List<FactorioObject> milestonesNotReachable = null;

            var nextMilestoneMask = 0x2ul;
            var nextMilestoneIndex = 0;
            var accessibleObjects = 0;

            var flagMask = 0ul;
            for (var i = 0; i <= currentMilestones.Length; i++)
            {
                flagMask |= 1ul << i;
                if (i > 0)
                {
                    var milestone = currentMilestones[i - 1];
                    if (milestone == null)
                    {
                        milestonesNotReachable = new List<FactorioObject>();
                        foreach (var pack in Database.allSciencePacks)
                        {
                            if (Array.IndexOf(currentMilestones, pack) == -1)
                            {
                                currentMilestones[nextMilestoneIndex++] = pack;
                                milestonesNotReachable.Add(pack);
                            }
                        }
                        Array.Resize(ref currentMilestones, nextMilestoneIndex);
                        break;
                    }
                    else
                    {
                        Console.WriteLine("Queuing milestone {0} ({1}) [{2}]", milestone.name, milestone.id, milestone.GetType().Name);

                        processingQueue.Enqueue(milestone.id);
                        processing[milestone] = ProcessingFlags.Initial | ProcessingFlags.InQueue;
                    }
                }

                while (processingQueue.Count > 0)
                {
                    var elem = processingQueue.Dequeue();
                    var elemDeps = dependencyList[elem];

                    var cur = result[elem];
                    var eflags = cur;
                    var isInitial = (processing[elem] & ProcessingFlags.Initial) != 0;
                    processing[elem] &= ProcessingFlags.MilestoneNeedOrdering;

                    // Console.WriteLine("Processing {0} ({1}) [{2}] -> isInitial: {3}", Database.objects[elem].name, Database.objects[elem].id, Database.objects[elem].GetType().Name, isInitial);

                    foreach (var list in elemDeps)
                    {
                        // Console.WriteLine("  -> {0}: req all {1}", list.flags, list.flags & DependencyList.Flags.RequireEverything);
                        if ((list.flags & DependencyList.Flags.RequireEverything) != 0)
                        {
                            foreach (var req in list.elements)
                            {
                                var reqFlags = result[req];
                                // Console.WriteLine("    -> dep all {0} ({1}) [{2}]: reqflags {3}", Database.objects[req].name, Database.objects[req].id, Database.objects[req].GetType().Name, reqFlags);
                                if (reqFlags == 0 && !isInitial)
                                    goto skip;
                                eflags |= reqFlags;
                            }
                        }
                        else
                        {
                            if (list.elements.Length == 0)
                            {
                                Console.WriteLine("Unexpected: {0} ({1}) [{2}] - {3} group deps empty, will cause unreachable elements", Database.objects[elem].name, Database.objects[elem].id, Database.objects[elem].GetType().Name, list.flags);
                            }

                            // Minimize group  (dependency) cost
                            var groupFlags = 0ul;
                            foreach (var req in list.elements)
                            {
                                var reqFlags = result[req];
                                // Console.WriteLine("    -> dep grp {0} ({1}) [{2}]: reqflags {3}, groupFlags {4}", Database.objects[req].name, Database.objects[req].id, Database.objects[req].GetType().Name, reqFlags, groupFlags);
                                if (reqFlags == 0)
                                    // Dependency is not available/processed yet, so check next
                                    continue;

                                if (reqFlags < groupFlags || groupFlags == 0ul)
                                    groupFlags = reqFlags;
                            }

                            // Console.WriteLine("    -> group flags {0}", groupFlags);
                            if (groupFlags == 0 && !isInitial)
                                goto skip;
                            eflags |= groupFlags;
                        }
                    }
                    if (!isInitial)
                    {
                        if (eflags == cur || (eflags | flagMask) != flagMask)
                        {
                            // Console.WriteLine("  -> Skipping: eflags {0}, flagMask {1}, cur {2}", eflags, flagMask, cur);
                            continue;
                        }
                    }
                    else eflags &= flagMask;

                    accessibleObjects++;
                    // Console.WriteLine("  -> Added object {0} ({1}) [{2}] with eflags {3} (was {4})", Database.objects[elem].name, Database.objects[elem].id, Database.objects[elem].GetType().Name, eflags, cur);

                    if (processing[elem] == ProcessingFlags.MilestoneNeedOrdering)
                    {
                        // Auto-sorting, elem was not added to currentMilestones yet, so add now its dependencies are solved
                        processing[elem] = 0;
                        eflags |= nextMilestoneMask;
                        nextMilestoneMask <<= 1;
                        currentMilestones[nextMilestoneIndex++] = Database.objects[elem];
                    }

                    result[elem] = eflags;
                    // Add reverse dependencies to feed the algorithm with new processable elements
                    foreach (var revdep in reverseDependencies[elem])
                    {
                        if ((processing[revdep] & ~ProcessingFlags.MilestoneNeedOrdering) != 0 || result[revdep] != 0)
                            // Already/About to be processed
                            continue;

                        // Console.WriteLine("    -> Queuing rev dep {0} ({1}) [{2}]", Database.objects[revdep].name, Database.objects[revdep].id, Database.objects[revdep].GetType().Name);

                        processing[revdep] |= ProcessingFlags.InQueue;
                        processingQueue.Enqueue(revdep);
                    }

                skip:;
                }
            }

            if (!project.settings.milestones.SequenceEqual(currentMilestones))
            {
                project.settings.RecordUndo();
                project.settings.milestones.Clear();
                project.settings.milestones.AddRange(currentMilestones);
            }
            GetLockedMaskFromProject();

            var hasAutomatableRocketLaunch = result[Database.objectsByTypeName["Special.launch"]] != 0;
            if (accessibleObjects < Database.objects.count / 2)
            {
                warnings.Error("More than 50% of all in-game objects appear to be inaccessible in this project with your current mod list. This can have a variety of reasons like objects being accessible via scripts," +
                               MaybeBug + MilestoneAnalysisIsImportant + UseDependencyExplorer, ErrorSeverity.AnalysisWarning);
            }
            else if (!hasAutomatableRocketLaunch)
            {
                warnings.Error("Rocket launch appear to be inaccessible. This means that rocket may not be launched in this mod pack, or it requires mod script to spawn or unlock some items," +
                               MaybeBug + MilestoneAnalysisIsImportant + UseDependencyExplorer, ErrorSeverity.AnalysisWarning);
            }
            else if (milestonesNotReachable != null)
            {
                warnings.Error("There are some milestones that are not accessible: " + string.Join(", ", milestonesNotReachable.Select(x => x.locName)) + ". You may remove these from milestone list," +
                               MaybeBug + MilestoneAnalysisIsImportant + UseDependencyExplorer, ErrorSeverity.AnalysisWarning);
            }
            Console.WriteLine("Milestones calculation finished in {0} ms.", time.ElapsedMilliseconds);
            milestoneResult = result;
        }

        private const string MaybeBug = " or it might be due to a bug inside a mod or YAFC.";
        private const string MilestoneAnalysisIsImportant = "\nA lot of YAFC's systems rely on objects being accessible, so some features may not work as intended.";
        private const string UseDependencyExplorer = "\n\nFor this reason YAFC has a Dependency Explorer that allows you to manually enable some of the core recipes. YAFC will iteratively try to unlock all the dependencies after each recipe you manually enabled. For most modpacks it's enough to unlock a few early recipes like any special recipes for plates that everything in the mod is based on.";

        public override string description => "Milestone analysis starts from objects that are placed on map by the map generator and tries to find all objects that are accessible from that, taking notes about which objects are locked behind which milestones.";
    }
}