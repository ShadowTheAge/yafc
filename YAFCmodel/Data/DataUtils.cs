using System;
using System.Collections.Generic;
using System.Text;
using YAFC.UI;

namespace YAFC.Model
{
    public static class DataUtils
    {
        public static readonly FactorioObjectComparer<FactorioObject> DefaultOrdering = new FactorioObjectComparer<FactorioObject>((x, y) => x.Cost().CompareTo(y.Cost()));
        public static readonly FactorioObjectComparer<Goods> FuelOrdering = new FactorioObjectComparer<Goods>((x, y) => (x.Cost()/x.fuelValue).CompareTo(y.Cost()/y.fuelValue));
        
        public static readonly FavouritesComparer<Goods> FavouriteFuel = new FavouritesComparer<Goods>(FuelOrdering);
        public static readonly FavouritesComparer<Entity> FavouriteCrafter = new FavouritesComparer<Entity>(DefaultOrdering);

        public static ulong GetMilestoneOrder(int id) => (Milestones.milestoneResult[id] - 1) & Milestones.lockedMask;

        public class FactorioObjectComparer<T> : IComparer<T> where T : FactorioObject
        {
            private readonly Comparison<T> similarComparison;
            public FactorioObjectComparer(Comparison<T> similarComparison)
            {
                this.similarComparison = similarComparison;
            }
            public int Compare(T x, T y)
            {
                var msx = x == null ? ulong.MaxValue : GetMilestoneOrder(x.id);
                var msy = y == null ? ulong.MaxValue : GetMilestoneOrder(y.id);
                if (msx != msy)
                    return msx.CompareTo(msy);
                return similarComparison(x, y);
            }
        }

        public class FavouritesComparer<T> : IComparer<T> where T : FactorioObject
        {
            private readonly Dictionary<T, int> bumps = new Dictionary<T, int>();
            private readonly IComparer<T> def;
            public FavouritesComparer(IComparer<T> def)
            {
                this.def = def;
            }

            public void AddToFavourite(T x)
            {
                bumps.TryGetValue(x, out var prev);
                bumps[x] = prev+1;
            }
            public int Compare(T x, T y)
            {
                bumps.TryGetValue(x, out var ix);
                bumps.TryGetValue(y, out var iy);
                if (ix == iy)
                    return def.Compare(x, y);
                return iy.CompareTo(ix);
            }
        }

        public static float GetProduction(this Recipe recipe, Goods product)
        {
            var amount = 0f;
            foreach (var p in recipe.products)
            {
                if (p.goods == product)
                    amount += p.amount * p.probability;
            }

            foreach (var ingredient in recipe.ingredients)
            {
                if (ingredient.goods == product)
                    amount -= ingredient.amount;
            }

            return amount;
        }
        
        public static FactorioObjectComparer<Recipe> GetRecipeComparerFor(Goods goods)
        {
            return new FactorioObjectComparer<Recipe>((x, y) => (x.Cost()/x.GetProduction(goods)).CompareTo(y.Cost()/y.GetProduction(goods)));
        }

        public static Icon NoFuelIcon;
        public static Icon WarningIcon;
        public static Icon HandIcon;

        public static T AutoSelect<T>(this IEnumerable<T> list, IComparer<T> comparer = default)
        {
            if (comparer == null)
                comparer = Comparer<T>.Default;
            var first = true;
            T best = default;
            foreach (var elem in list)
            {
                if (first || comparer.Compare(best, elem) > 0)
                {
                    first = false;
                    best = elem;
                }
            }
            return best;
        }
        
        private const char no = (char) 0;
        private static readonly (char suffix, float multiplier, float dec)[] FormatSpec =
        {
            ('μ', 1e8f,  100f),
            ('μ', 1e8f,  100f),
            ('μ', 1e7f,  10f),
            ('μ', 1e6f,  1f),
            ('μ', 1e6f,  1f), // skipping m (milli-) because too similar to M (mega-)
            (no,  1e4f,  10000f),
            (no,  1e3f,  1000f),
            (no,  1e2f,  100f),
            (no,  1e1f,  10f), // [1-10]
            (no,  1e0f,  1f), 
            (no,  1e0f,  1f),
            ('K', 1e-2f, 10f),
            ('K', 1e-3f, 1f),
            ('M', 1e-4f, 100f),
            ('M', 1e-5f, 10f),
            ('M', 1e-6f, 1f),
            ('G', 1e-7f, 100f),
            ('G', 1e-8f, 10f),
            ('G', 1e-9f, 1f),
            ('T', 1e-10f, 100f),
            ('T', 1e-11f, 10f),
            ('T', 1e-12f, 1f),
        };

        private static readonly StringBuilder amountBuilder = new StringBuilder();
        public static string FormatAmount(float amount, bool isPower = false)
        {
            if (amount <= 0)
                return "0";
            amountBuilder.Clear();
            if (amount < 0)
            {
                amountBuilder.Append('-');
                amount = -amount;
            }
            if (isPower)
                amount *= 1e6f;
            var idx = MathUtils.Clamp(MathUtils.Floor(MathF.Log10(amount)) + 8, 0, FormatSpec.Length-1);
            var val = FormatSpec[idx];
            amountBuilder.Append(MathUtils.Round(amount * val.multiplier) / val.dec);
            if (val.suffix != no)
                amountBuilder.Append(val.suffix);
            if (isPower)
                amountBuilder.Append("W");
            return amountBuilder.ToString();
        }

        public static bool TryParseAmount(string str, out float amount, bool isPower)
        {
            var lastValidChar = 0;
            amount = 0;
            foreach (var c in str)
            {
                if (c >= '0' && c <= '9' || c == '.' || c == '-' || c == 'e')
                    ++lastValidChar;
                else
                {
                    if (lastValidChar == 0)
                        return false;
                    float multiplier;
                    switch (c)
                    {
                        case 'k': case 'K':
                            multiplier = 1e3f;
                            break;
                        case 'm': case 'M':
                            multiplier = 1e6f;
                            break;
                        case 'g': case 'G':
                            multiplier = 1e9f;
                            break;
                        case 't': case 'T':
                            multiplier = 1e12f;
                            break;
                        default:
                            return false;
                    }

                    if (isPower)
                        multiplier /= 1e6f;

                    var substr = str.Substring(0, lastValidChar);
                    if (!float.TryParse(substr, out amount)) return false;
                    amount *= multiplier;
                    if (amount > 1e15)
                        return false;
                    return true;
                }
            }

            var valid = float.TryParse(str, out amount);
            if (amount > 1e15)
                return false;
            return valid;
        }
    }
}