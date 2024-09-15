using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Serilog;
using Yafc.UI;

namespace Yafc.Model;
public class Milestones : Analysis {
    public static readonly Milestones Instance = new Milestones();

    private static readonly ILogger logger = Logging.GetLogger<Milestones>();

    public FactorioObject[] currentMilestones = [];
    private Mapping<FactorioObject, Bits> milestoneResult;
    public Bits lockedMask { get; private set; } = new();
    private Project? project;

    public bool IsAccessibleWithCurrentMilestones(FactorioId obj) {
        return (milestoneResult[obj] & lockedMask) == 1;
    }

    public bool IsAccessibleWithCurrentMilestones(FactorioObject obj) {
        return (milestoneResult[obj] & lockedMask) == 1;
    }

    public bool IsAccessibleAtNextMilestone(FactorioObject obj) {
        var milestoneMask = milestoneResult[obj] & lockedMask;
        if (milestoneMask == 1) {
            return true;
        }

        if (milestoneMask[0]) {
            return false;
        }
        // TODO Always returns false -> milestoneMask is a power of 2 + 1 always has bit 0 set, as x pow 2 sets one (high) bit, so the + 1 adds bit 0, which is detected by (milestoneMask & 1) != 0
        // return ((milestoneMask - 1) & (milestoneMask - 2)) == 0; // milestoneMask is a power of 2 + 1
        return false;
    }

    public Bits GetMilestoneResult(FactorioId obj) {
        // Return a copy of Bits
        return new Bits(milestoneResult[obj]);
    }

    public Bits GetMilestoneResult(FactorioObject obj) {
        // Return a copy of Bits
        return new Bits(milestoneResult[obj]);
    }

    private void GetLockedMaskFromProject() {
        if (project is null) {
            throw new InvalidOperationException($"{nameof(project)} must be set before calling {nameof(GetLockedMaskFromProject)}");
        }

        Bits bits = new(true); // The first bit is skipped (index is increased before the first bit is written) and always set
        int index = 0;
        foreach (var milestone in currentMilestones) {
            index++;
            bits[index] = !project.settings.Flags(milestone).HasFlags(ProjectPerItemFlags.MilestoneUnlocked);
        }
        lockedMask = bits;
    }

    private void ProjectSettingsChanged(bool visualOnly) {
        if (!visualOnly) {
            GetLockedMaskFromProject();
        }
    }

    public FactorioObject? GetHighest(FactorioObject target, bool all) {
        if (target == null) {
            return null;
        }

        var ms = milestoneResult[target];
        if (!all) {
            ms &= lockedMask;
        }

        if (ms == 0) {
            return null;
        }

        int msb = ms.HighestBitSet() - 1;
        return msb < 0 || msb >= currentMilestones.Length ? null : currentMilestones[msb];
    }

    [Flags]
    private enum ProcessingFlags : byte {
        InQueue = 1,
        Initial = 2,
        MilestoneNeedOrdering = 4,
        ForceInaccessible = 8
    }

    public override void Compute(Project project, ErrorCollector warnings) {
        if (project.settings.milestones.Count == 0) {
            ComputeWithParameters(project, warnings, Database.allSciencePacks, true);
        }
        else {
            ComputeWithParameters(project, warnings, project.settings.milestones.ToArray(), false);
        }
    }

    public void ComputeWithParameters(Project project, ErrorCollector warnings, FactorioObject[] milestones, bool autoSort) {
        if (this.project == null) {
            this.project = project;
            project.settings.changed += ProjectSettingsChanged;
        }

        Stopwatch time = Stopwatch.StartNew();
        var result = Database.objects.CreateMapping<Bits>();
        var processing = Database.objects.CreateMapping<ProcessingFlags>();
        Queue<FactorioId> processingQueue = new Queue<FactorioId>();

        foreach (var rootAccessible in Database.rootAccessible) {
            result[rootAccessible] = new Bits(true);
            processingQueue.Enqueue(rootAccessible.id);
            processing[rootAccessible] = ProcessingFlags.Initial | ProcessingFlags.InQueue;
        }

        foreach (var (obj, flag) in project.settings.itemFlags) {
            if (flag.HasFlags(ProjectPerItemFlags.MarkedAccessible)) {
                result[obj] = new Bits(true);
                processingQueue.Enqueue(obj.id);
                processing[obj] = ProcessingFlags.Initial | ProcessingFlags.InQueue;
            }
            else if (flag.HasFlag(ProjectPerItemFlags.MarkedInaccessible)) {
                processing[obj] = ProcessingFlags.ForceInaccessible;
            }
        }

        if (autoSort) {
            // Adding default milestones AND special flag to auto-order them
            foreach (var milestone in milestones) {
                processing[milestone] |= ProcessingFlags.MilestoneNeedOrdering;
            }

            currentMilestones = new FactorioObject[milestones.Length];
        }
        else {
            currentMilestones = milestones;
            for (int i = 0; i < milestones.Length; i++) {
                //  result[milestones[i]] = (1ul << (i + 1)) | 1;
                Bits b = new Bits(true);
                b[i + 1] = true;
                result[milestones[i]] = b;
            }
        }

        var dependencyList = Dependencies.dependencyList;
        var reverseDependencies = Dependencies.reverseDependencies;
        List<FactorioObject>? milestonesNotReachable = null;

        Bits nextMilestoneMask = new Bits();
        nextMilestoneMask[1] = true;
        int nextMilestoneIndex = 0;
        int accessibleObjects = 0;

        Bits flagMask = new Bits();
        for (int i = 0; i <= currentMilestones.Length; i++) {
            flagMask[i] = true;
            if (i > 0) {
                var milestone = currentMilestones[i - 1];
                if (milestone == null) {
                    milestonesNotReachable = [];
                    foreach (var pack in Database.allSciencePacks) {
                        if (Array.IndexOf(currentMilestones, pack) == -1) {
                            currentMilestones[nextMilestoneIndex++] = pack;
                            milestonesNotReachable.Add(pack);
                        }
                    }
                    Array.Resize(ref currentMilestones, nextMilestoneIndex);
                    break;
                }
                logger.Information("Processing milestone {Milestone}", milestone.locName);
                processingQueue.Enqueue(milestone.id);
                processing[milestone] = ProcessingFlags.Initial | ProcessingFlags.InQueue;
            }

            while (processingQueue.Count > 0) {
                var elem = processingQueue.Dequeue();
                var entry = dependencyList[elem];


                var cur = result[elem];
                var elementFlags = cur;
                bool isInitial = (processing[elem] & ProcessingFlags.Initial) != 0;
                processing[elem] &= ProcessingFlags.MilestoneNeedOrdering;

                foreach (var list in entry) {
                    if ((list.flags & DependencyList.Flags.RequireEverything) != 0) {
                        foreach (var req in list.elements) {
                            var reqFlags = result[req];
                            if (reqFlags.IsClear() && !isInitial) {
                                goto skip;
                            }

                            elementFlags |= reqFlags;
                        }
                    }
                    else {
                        Bits groupFlags = new Bits();
                        foreach (var req in list.elements) {
                            var acc = result[req];
                            if (acc.IsClear()) {
                                continue;
                            }

                            if (acc < groupFlags || groupFlags.IsClear()) {
                                groupFlags = acc;
                            }
                        }

                        if (groupFlags.IsClear() && !isInitial) {
                            goto skip;
                        }

                        elementFlags |= groupFlags;
                    }
                }

                if (!isInitial) {
                    if (elementFlags == cur || (elementFlags | flagMask) != flagMask) {
                        continue;
                    }
                }
                else {
                    elementFlags &= flagMask;
                }

                accessibleObjects++;
                //var obj = Database.objects[elem];
                //logger.Information("Added object {LocalizedName} [{Type}] with mask {MilestoneMask} (was {PreviousMask})", obj.locName, obj.GetType().Name, elementFlags, cur);
                if (processing[elem] == ProcessingFlags.MilestoneNeedOrdering) {
                    processing[elem] = 0;
                    elementFlags |= nextMilestoneMask;
                    nextMilestoneMask <<= 1;
                    currentMilestones[nextMilestoneIndex++] = Database.objects[elem];
                }

                result[elem] = elementFlags;
                foreach (var reverseDependency in reverseDependencies[elem]) {
                    if ((processing[reverseDependency] & ~ProcessingFlags.MilestoneNeedOrdering) != 0 || !result[reverseDependency].IsClear()) {
                        continue;
                    }

                    processing[reverseDependency] |= ProcessingFlags.InQueue;
                    processingQueue.Enqueue(reverseDependency);
                }

skip:;
            }
        }

        if (!project.settings.milestones.SequenceEqual(currentMilestones)) {
            _ = project.settings.RecordUndo();
            project.settings.milestones.Clear();
            project.settings.milestones.AddRange(currentMilestones);
        }
        GetLockedMaskFromProject();

        bool hasAutomatableRocketLaunch = result[Database.objectsByTypeName["Special.launch"]] != 0;
        if (accessibleObjects < Database.objects.count / 2) {
            warnings.Error("More than 50% of all in-game objects appear to be inaccessible in this project with your current mod list. This can have a variety of reasons like objects being accessible via scripts," +
                           MaybeBug + MilestoneAnalysisIsImportant + UseDependencyExplorer, ErrorSeverity.AnalysisWarning);
        }
        else if (!hasAutomatableRocketLaunch) {
            warnings.Error("Rocket launch appear to be inaccessible. This means that rocket may not be launched in this mod pack, or it requires mod script to spawn or unlock some items," +
                           MaybeBug + MilestoneAnalysisIsImportant + UseDependencyExplorer, ErrorSeverity.AnalysisWarning);
        }
        else if (milestonesNotReachable != null) {
            warnings.Error("There are some milestones that are not accessible: " + string.Join(", ", milestonesNotReachable.Select(x => x.locName)) + ". You may remove these from milestone list," +
                           MaybeBug + MilestoneAnalysisIsImportant + UseDependencyExplorer, ErrorSeverity.AnalysisWarning);
        }
        logger.Information("Milestones calculation finished in {ElapsedTime}ms.", time.ElapsedMilliseconds);
        milestoneResult = result;
    }

    private const string MaybeBug = " or it might be due to a bug inside a mod or YAFC.";
    private const string MilestoneAnalysisIsImportant = "\nA lot of YAFC's systems rely on objects being accessible, so some features may not work as intended.";
    private const string UseDependencyExplorer = "\n\nFor this reason YAFC has a Dependency Explorer that allows you to manually enable some of the core recipes. YAFC will iteratively try to unlock all the dependencies after each recipe you manually enabled. For most modpacks it's enough to unlock a few early recipes like any special recipes for plates that everything in the mod is based on.";

    public override string description => "Milestone analysis starts from objects that are placed on map by the map generator and tries to find all objects that are accessible from that, taking notes about which objects are locked behind which milestones.";
}
