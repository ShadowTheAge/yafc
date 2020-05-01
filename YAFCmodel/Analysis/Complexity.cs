/*using System;
using System.Collections.Generic;

namespace YAFC.Model
{
    public static class Complexity
    {
        private static int[] itemsComplexity;
        
        [Flags]
        private enum ProcessingFlags : byte
        {
            InQueue = 1,
            Initial = 2
        }
        public static string GetComplexityRatingName(int complexity)
        {
            if (complexity < 60)
            {
                if (complexity < 5)
                    return "Free";
                if (complexity < 10)
                    return "Trivial";
                if (complexity < 15)
                    return "Basic";
                if (complexity < 25)
                    return "Easy";
                if (complexity < 40)
                    return "Moderate";
                return "Medium";
            }
            if (complexity < 1000)
            {
                if (complexity < 90)
                    return "Solid";
                if (complexity < 140)
                    return "Demanding";
                if (complexity < 200)
                    return "Challenging";
                if (complexity < 300)
                    return "Hard";
                if (complexity < 450)
                    return "Severe";
                if (complexity < 700)
                    return "Tough";
                return "Ambitious";
            }
            if (complexity < 1500)
                return "Tremendous";
            if (complexity < 2500)
                return "Terrific";
            if (complexity < 4000)
                return "Overwhelming";
            if (complexity < 6000)
                return "Hopeless";
            if (complexity < 9000)
                return "Insurmountable";
            if (complexity < 13000)
                return "Unthinkable";
            if (complexity < 20000)
                return "Unimaginable";
            if (complexity < 30000)
                return "Inconcievable";
            return "Absurd";
        }

        public static void CalculateAll()
        {
            var count = Database.allObjects.Length;
            var result = new int[count];
            var processing = new ProcessingFlags[count];
            var dependencyList = Dependencies.dependencyList;
            var reverseDependencies = Dependencies.reverseDependencies;
            var processingStack = new Stack<int>();

            foreach (var rootAccessbile in Database.rootAccessible)
            {
                result[rootAccessbile.id] = 1;
                processingStack.Push(rootAccessbile.id);
                processing[rootAccessbile.id] = ProcessingFlags.Initial | ProcessingFlags.InQueue;
            }

            var opc = 0;
            while (processingStack.Count > 0)
            {
                var elem = processingStack.Pop();
                var entry = dependencyList[elem];

                var cur = result[elem];
                var complexity = 1;
                if ((processing[elem] & ProcessingFlags.Initial) == 0)
                {
                    processing[elem] = 0;
                    foreach (var list in entry)
                    {
                        if ((list.flags & DependencyList.Flags.OneTimeInvestment) != 0)
                            continue;
                        if ((list.flags & DependencyList.Flags.RequireEverything) != 0)
                        {
                            foreach (var req in list.elements)
                            {
                                var resComplexity = result[req];
                                if (resComplexity == 0)
                                    goto skip;
                                complexity += resComplexity;
                            }
                        }
                        else
                        {
                            var max = int.MaxValue;
                            foreach (var req in list.elements)
                            {
                                var resComplexity = result[req];
                                if (resComplexity == 0)
                                    continue;
                                if (resComplexity < max)
                                    max = resComplexity;
                            }

                            if (max == int.MaxValue)
                                goto skip;
                            complexity += max;
                        }
                    }
                    if (complexity == cur)
                        continue;

                    //Console.WriteLine("Added object "+obj.locName+" ["+obj.GetType().Name+"] with mask "+eflags.ToString("X") + " (was "+cur.ToString("X")+")");
                }
                    
                result[elem] = complexity;
                foreach (var revdep in reverseDependencies[elem])
                {
                    if (processing[revdep] != 0)
                        continue;
                    processing[revdep] = ProcessingFlags.InQueue;
                    processingStack.Push(revdep);
                }
                    
                skip:;
                
                if (++opc > 1000000)
                    break;
            }
            Console.WriteLine("Complexity calculation finished after "+opc+" steps");
            itemsComplexity = result;
        }

        public static int GetComplexity(this FactorioObject obj) => obj == null ? 0 : itemsComplexity[obj.id];
    }
}*/