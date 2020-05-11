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
        public static Mapping<FactorioObject, DependencyList[]> dependencyList;
        public static Mapping<FactorioObject, List<int>> reverseDependencies;

        public static void Calculate()
        {
            dependencyList = Database.objects.CreateMapping<DependencyList[]>();
            reverseDependencies = Database.objects.CreateMapping<List<int>>();
            for (var i = 0; i < reverseDependencies.Count; i++)
                reverseDependencies[i] = new List<int>();
            
            var collector = new DependencyCollector();
            for (var i = 0; i < dependencyList.Count; i++)
            {
                Database.objects[i].GetDependencies(collector);
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
            private readonly List<DependencyList> list = new List<DependencyList>();

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