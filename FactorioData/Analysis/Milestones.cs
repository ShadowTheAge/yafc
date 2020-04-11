using System;
using System.Collections.Generic;
using System.Linq;

namespace FactorioData
{
    public class Milestone : IFactorioObjectWrapper
    {
        public FactorioObject obj;
        public int index;
        public Milestone(FactorioObject obj)
        {
            this.obj = obj;
        }

        public bool this[FactorioObject obj] => obj != null && (Milestones.milestoneResult[obj.id] & (1ul << index)) != 0;

        public string text => obj.locName;
        public FactorioObject target => obj;
    }
    
    public static class Milestones
    {
        public static ulong[] milestoneResult;
        public static List<Milestone> milestones = new List<Milestone>();
        
        public static void CreateDefault()
        {
            milestones.Clear();
            milestones.AddRange(Database.defaultMilestones.Select(x => new Milestone(x)));
            CalculateAll();
        }

        public static bool IsAccessible(this FactorioObject obj) => milestoneResult[obj.id] != 0;

        private static int HighestBitSet(ulong x)
        {
            var set = 0;
            if (x > 0xFFFFFFFF) { set += 32; x >>= 32; }
            if (x > 0xFFFF) { set += 16; x >>= 16; }
            if (x > 0xFF) { set += 8; x >>= 8; }
            if (x > 0xF) { set += 4; x >>= 4; }
            if (x > 0x3) { set += 2; x >>= 2; }
            if (x > 0x1) { set += 1; }
            return set;
        }

        public static FactorioObject GetHighest(FactorioObject target)
        {
            if (target == null)
                return null;
            var ms = milestoneResult[target.id];
            var msb = HighestBitSet(ms)-1;
            return msb < 0 || msb >= milestones.Count ? null : milestones[msb].obj;
        }
        
        [Flags]
        private enum ProcessingFlags : byte
        {
            InQueue = 1,
            Initial = 2
        }

        private static void CalculateAll()
        {
            var count = Database.allObjects.Length;
            var result = new ulong[count];
            var processing = new ProcessingFlags[count];
            var dependencyList = Dependencies.dependencyList;
            var reverseDependencies = Dependencies.reverseDependencies;
            var processingStack = new Stack<int>();

            for (var i = 0; i < milestones.Count; i++)
            {
                var milestone = milestones[i];
                milestone.index = i+1;
                result[milestone.obj.id] = (1ul << (i + 1)) | 1;
            }

            foreach (var rootAccessbile in Database.rootAccessible)
            {
                result[rootAccessbile.id] = 1;
                processingStack.Push(rootAccessbile.id);
                processing[rootAccessbile.id] = ProcessingFlags.Initial | ProcessingFlags.InQueue;
            }

            var flagMask = 0ul;
            var opc = 0;
            for (var i = 0; i <= milestones.Count; i++)
            {
                flagMask |= 1ul << i;
                if (i > 0)
                {
                    var milestone = milestones[i-1];
                    Console.WriteLine("Processing milestone "+milestone.obj.locName);
                    processingStack.Push(milestone.obj.id);
                    processing[milestone.obj.id] = ProcessingFlags.Initial | ProcessingFlags.InQueue;
                }

                while (processingStack.Count > 0)
                {
                    var elem = processingStack.Pop();
                    var entry = dependencyList[elem];

                    var cur = result[elem];
                    var eflags = cur;
                    var isInitial = (processing[elem] & ProcessingFlags.Initial) != 0;
                    processing[elem] = 0;

                    foreach (var list in entry)
                    {
                        if ((list.flags & DependencyList.Flags.RequireEverything) != 0)
                        {
                            foreach (var req in list.elements)
                            {
                                var reqFlags = result[req];
                                if (reqFlags == 0 && !isInitial)
                                    goto skip;
                                eflags |= result[req];
                            }
                        }
                        else
                        {
                            var groupFlags = 0ul;
                            foreach (var req in list.elements)
                            {
                                var acc = result[req];
                                if (acc == 0)
                                    continue;
                                if (acc < groupFlags || groupFlags == 0ul)
                                    groupFlags = acc;
                            }

                            if (groupFlags == 0 && !isInitial)
                                goto skip;
                            eflags |= groupFlags;
                        }
                    }
                    if (!isInitial)
                    {
                        if (eflags == cur || (eflags | flagMask) != flagMask)
                            continue;
                    }
                    else eflags &= flagMask;

                    //Debug.Log("Added object "+obj.locName+" ["+obj.GetType().Name+"] with mask "+eflags.ToString("X") + " (was "+cur.ToString("X")+")");
                    
                    result[elem] = eflags;
                    foreach (var revdep in reverseDependencies[elem])
                    {
                        if (processing[revdep] != 0 || (result[revdep] & eflags) == eflags)
                            continue;
                        processing[revdep] = ProcessingFlags.InQueue;
                        processingStack.Push(revdep);
                    }
                    
                    skip:;

                    if (++opc > 1000000)
                        goto stop;
                }
            }
            
            stop:;
            Console.WriteLine("Milestones calculation finished after "+opc+" steps");
            milestoneResult = result;
        }
    }
}