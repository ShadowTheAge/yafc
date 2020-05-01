using System;
using System.Collections.Generic;

namespace YAFC.Model
{
    [Flags]
    public enum ObjectFlag : byte
    {
        RootAccessible = 1 << 0,
        Automatable = 1 << 1,
    }
    
    public static class ObjectFlags
    {
        private static ObjectFlag[] objectFlags;

        public static bool IsRequiredManualLabor(FactorioObject obj) => (objectFlags[obj.id] & ObjectFlag.Automatable) == 0; 
        
        public static void Process()
        {
            var count = Database.allObjects.Length;
            var result = new ObjectFlag[count];
            var queueFlags = new bool[count];
            var processingStack = new Stack<int>();
            var dependencyList = Dependencies.dependencyList;

            foreach (var o in Database.rootAccessible)
            {
                processingStack.Push(o.id);
                result[o.id] = ObjectFlag.RootAccessible;
            }

            objectFlags = result;
        }
    }
}