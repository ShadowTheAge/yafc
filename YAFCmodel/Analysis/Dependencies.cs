using System;
using System.Collections.Generic;

namespace YAFC.Model
{
    public interface IDependencyCollector
    {
        void Add(FactorioId[] raw, DependencyList.Flags flags);
        void Add(IReadOnlyList<FactorioObject> raw, DependencyList.Flags flags);
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
            TechnologyPrerequisites = 8 | RequireEverything | OneTimeInvestment,
            IngredientVariant = 9,
            Hidden = 10,
        }

        public Flags flags;
        public FactorioId[] elements;
    }
    
    public static class Dependencies
    {
        public static Mapping<FactorioObject, DependencyList[]> dependencyList;
        public static Mapping<FactorioObject, List<FactorioId>> reverseDependencies;

        public static void Calculate()
        {
            dependencyList = Database.objects.CreateMapping<DependencyList[]>();
            reverseDependencies = Database.objects.CreateMapping<List<FactorioId>>();
            foreach (var obj in Database.objects.all) 
                reverseDependencies[obj] = new List<FactorioId>();
            
            var collector = new DependencyCollector();
            var temp = new List<FactorioObject>();
            foreach (var obj in Database.objects.all)
            {
                obj.GetDependencies(collector, temp);
                var packed = collector.Pack();
                dependencyList[obj] = packed;

                foreach (var group in packed)
                    foreach (var req in group.elements)
                        if (!reverseDependencies[req].Contains(obj.id))
                            reverseDependencies[req].Add(obj.id);
                        
            }
        }

        private class DependencyCollector : IDependencyCollector
        {
            private readonly List<DependencyList> list = new List<DependencyList>();

            public void Add(FactorioId[] elements, DependencyList.Flags flags)
            {
                // Only add lists that actually contain elements, lists that are used to hide objects, or lists to unlock technologies (because of the lack of unlocking dependencies those should be unavailable)
                if (elements.Length > 0 || flags == DependencyList.Flags.Hidden || flags == DependencyList.Flags.TechnologyUnlock)
                {
                    list.Add(new DependencyList { elements = elements, flags = flags });
                }
            }

            public void Add(IReadOnlyList<FactorioObject> readOnlyList, DependencyList.Flags flags)
            {
                var elems = new FactorioId[readOnlyList.Count];
                for (var i = 0; i < readOnlyList.Count; i++)
                    elems[i] = readOnlyList[i].id;
                Add(elems, flags);
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