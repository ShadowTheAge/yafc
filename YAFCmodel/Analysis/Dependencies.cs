using System;
using System.Collections.Generic;

namespace YAFC.Model
{
    public interface IDependencyCollector
    {
        void Add(int[] raw, DependencyList.Flags flags);
        void Add<T>(PackedList<T> list, DependencyList.Flags flags) where T : FactorioObject;
    }

    public struct DependencyList
    {
        [Flags]
        public enum Flags
        {            
            RequireEverything = 0x100,
            OneTimeInvestment = 0x200,

            Ingredient = 1 | RequireEverything,
            CraftingEntity = 2 | OneTimeInvestment,
            SourceEntity = 3 | OneTimeInvestment,
            TechnologyUnlock = 4 | OneTimeInvestment,
            Source = 5,
            Fuel = 6,
            ItemToPlace = 7,
            TechnologyPrerequisites = 8 | RequireEverything | OneTimeInvestment
        }

        public Flags flags;
        public int[] elements;
    }
    
    public static class Dependencies
    {
        public static DependencyList[][] dependencyList;
        public static List<int>[] reverseDependencies;

        public static void Calculate()
        {
            dependencyList = new DependencyList[Database.allObjects.Length][];
            reverseDependencies = new List<int>[Database.allObjects.Length];
            for (var i = 0; i < reverseDependencies.Length; i++)
                reverseDependencies[i] = new List<int>();
            
            var collector = new DependencyCollector();
            for (var i = 0; i < dependencyList.Length; i++)
            {
                Database.allObjects[i].GetDependencies(collector);
                var packed = collector.Pack();
                dependencyList[i] = packed;

                foreach (var group in packed)
                    foreach (var req in group.elements)
                        if (!reverseDependencies[req].Contains(i))
                            reverseDependencies[req].Add(i);
                        
            }
        }

        private class DependencyCollector : IDependencyCollector
        {
            private List<DependencyList> list = new List<DependencyList>();

            public void Add(int[] raw, DependencyList.Flags flags)
            {
                list.Add(new DependencyList {elements = raw, flags = flags});
            }

            public void Add<T>(PackedList<T> packedList, DependencyList.Flags flags) where T : FactorioObject
            {
                var dependency = new DependencyList {elements = packedList.raw, flags = flags};
                list.Add(dependency);
            }

            public DependencyList[] Pack()
            {
                var packed = list.ToArray();
                list.Clear();
                return packed;
            }
        }
        
    }
}