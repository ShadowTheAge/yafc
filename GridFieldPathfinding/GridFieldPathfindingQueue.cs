using System;
using System.Collections.Generic;

namespace Routing
{
    // Very fast priority queue implementation (but without requeuing and with expectations on weights)
    internal class GridFieldPathfindingQueue<T> : IComparer<(int, T)>
    {
        private int basePriority;
        private int maxAdded;
        private List<T>[] increments;
        private List<(int priority, T value)> excess = new List<(int priority, T value)>();
        
        public GridFieldPathfindingQueue(int maxHeuristicIncrement)
        {
            increments = new List<T>[maxHeuristicIncrement];
            for (var i = 0; i < maxHeuristicIncrement; i++)
            {
                increments[i] = new List<T>();
            }
        }

        public void Add(T item, int weight)
        {
            if (weight > maxAdded)
                maxAdded = weight;
            if (weight < basePriority)
                weight = basePriority;
            else if (weight >= basePriority + increments.Length)
            {
                var val = (weight, item);
                var pos = excess.BinarySearch(val, this);
                if (pos < 0)
                    pos = ~pos;
                excess.Insert(pos, val);
                return;
            }
            increments[weight % increments.Length].Add(item);
        }

        public bool GetNext(out T result)
        {
            int ecount;
            var criticalBaseWeight = Math.Min(basePriority + increments.Length, maxAdded);
            do
            {
                var l = increments[basePriority % increments.Length];
                var count = l.Count;
                if (count > 0)
                {
                    result = l[count - 1];
                    l.RemoveAt(count - 1);
                    return true;
                }
                
                ecount = excess.Count-1;
                if (ecount >= 0 && excess[ecount].priority == basePriority)
                {
                    result = excess[ecount].value;
                    excess.RemoveAt(ecount);
                    return true;
                }

                ++basePriority;
            } while (basePriority < criticalBaseWeight);

            if (ecount >= 0)
            {
                basePriority = excess[ecount].priority;
                result = excess[ecount].value;
                excess.RemoveAt(ecount);
                return true;
            }

            result = default;
            return false;
        }

        public void Reset()
        {
            basePriority = 0;
            foreach (var increment in increments)
                increment.Clear();
            excess.Clear();
        }

        public int Compare((int, T) x, (int, T) y)
        {
            return y.Item1.CompareTo(x.Item1);
        }
    }
}