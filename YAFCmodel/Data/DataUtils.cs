using System;
using System.Collections.Generic;
using YAFC.UI;

namespace YAFC.Model
{
    public static class DataUtils
    {
        public static readonly Func<int, ulong> OrderByMilestonesId = x => Milestones.milestoneResult[x] - 1;
        public static readonly Func<FactorioObject, ulong> OrderByMilestones = x => Milestones.milestoneResult[x.id] - 1;
        public static readonly Func<FactorioObject, (ulong, float)> DefaultOrdering = x => (Milestones.milestoneResult[x.id] - 1, x.Cost());
        public static readonly Func<Goods, (ulong, float)> FuelOrdering = x => (Milestones.milestoneResult[x.id] - 1, x.Cost() / x.fuelValue);

        public static Icon NoFuelIcon;
        public static Icon WarningIcon;
        public static Icon HandIcon;

        public static Goods AutoSelectFuel(this IEnumerable<Goods> fuels) => SelectMin(fuels, FuelOrdering);
        public static T AutoSelect<T>(this IEnumerable<T> list) where T:FactorioObject
        {
            return list.SelectMin<T, (ulong, float)>(DefaultOrdering);
        }

        public static T SelectMin<T, TVal>(this IEnumerable<T> list, Func<T, TVal> selector)
        {
            var first = true;
            T best = default;
            TVal bestVal = default;
            var comparer = Comparer<TVal>.Default;
            foreach (var elem in list)
            {
                var val = selector(elem);
                if (first || comparer.Compare(val, bestVal) < 0)
                {
                    best = elem;
                    bestVal = val;
                    first = false;
                }
            }
            return best;
        }
    }
}