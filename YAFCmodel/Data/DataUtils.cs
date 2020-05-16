using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Google.OrTools.LinearSolver;
using SDL2;
using YAFC.UI;

namespace YAFC.Model
{
    public static class DataUtils
    {
        public static readonly FactorioObjectComparer<FactorioObject> DefaultOrdering = new FactorioObjectComparer<FactorioObject>((x, y) => x.Cost().CompareTo(y.Cost()));
        public static readonly FactorioObjectComparer<Goods> FuelOrdering = new FactorioObjectComparer<Goods>((x, y) => (x.Cost()/x.fuelValue).CompareTo(y.Cost()/y.fuelValue));
        public static readonly FactorioObjectComparer<Recipe> DefaultRecipeOrdering = new FactorioObjectComparer<Recipe>((x, y) =>
        {
            var yflow = y.ApproximateFlow();
            var xflow = x.ApproximateFlow();
            if (yflow != xflow)
                return yflow > xflow ? 1 : -1;
            return x.RecipeWaste().CompareTo(y.RecipeWaste());
        });
        public static readonly FactorioObjectComparer<Entity> CrafterOrdering = new FactorioObjectComparer<Entity>((x, y) =>
        {
            if (x.energy.type != y.energy.type)
                return x.energy.type.CompareTo(y.energy.type);
            if (x.craftingSpeed != y.craftingSpeed)
                return y.craftingSpeed.CompareTo(x.craftingSpeed);
            return x.Cost().CompareTo(y.Cost());
        });
        
        public static readonly FavouritesComparer<Goods> FavouriteFuel = new FavouritesComparer<Goods>(FuelOrdering);
        public static readonly FavouritesComparer<Entity> FavouriteCrafter = new FavouritesComparer<Entity>(CrafterOrdering);
        
        public static readonly IComparer<FactorioObject> DeterministicComparer = new FactorioObjectDeterministicComparer();

        public static ulong GetMilestoneOrder(FactorioId id)
        {
            var ms = Milestones.Instance;
            return (ms.milestoneResult[id] - 1) & ms.lockedMask;
        }

        public static string factorioPath { get; internal set; }
        public static string modsPath { get; internal set; }
        public static string[] allMods { get; internal set; }
        public static readonly Random random = new Random();

        private class FactorioObjectDeterministicComparer : IComparer<FactorioObject>
        {
            public int Compare(FactorioObject x, FactorioObject y) => x.id.CompareTo(y.id); // id comparison is deterministic because objects are sorted deterministicaly
        }

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
        
        public static Solver CreateSolver(string name)
        {
            var solver = Solver.CreateSolver(name, "GLOP_LINEAR_PROGRAMMING");
            // Relax solver parameters as returning imprecise solution is better than no solution at all
            // It is not like we need 8 digits of precision after all, most computations in YAFC are done in singles
            // see all properties here: https://github.com/google/or-tools/blob/stable/ortools/glop/parameters.proto
            solver.SetSolverSpecificParametersAsString("solution_feasibility_tolerance:1e-1");
            return solver;
        }

        public static Solver.ResultStatus TrySolvewithDifferentSeeds(this Solver solver)
        {
            for (var i = 0; i < 3; i++)
            {
                var time = Stopwatch.StartNew();
                var result = solver.Solve();
                Console.WriteLine("Solution completed in "+time.ElapsedMilliseconds+" ms with result "+result);
                if (result == Solver.ResultStatus.ABNORMAL)
                {
                    solver.SetSolverSpecificParametersAsString("random_seed:" + random.Next());
                    continue;
                } /*else 
                    VerySlowTryFindBadObjective(solver);*/
                return result;
            }
            return Solver.ResultStatus.ABNORMAL;
        }

        public static void VerySlowTryFindBadObjective(Solver solver)
        {
            var vars = solver.variables();
            var obj = solver.Objective();
            foreach (var v in vars)
            {
                obj.SetCoefficient(v, 0);
                var result = solver.Solve();
                if (result == Solver.ResultStatus.OPTIMAL)
                {
                    Console.WriteLine("Infeasibility candidate: "+v.Name());
                    return;
                }
            }
        }

        public static void SetCoefficientCheck(this Constraint cstr, Variable var, float amount, ref Variable prev)
        {
            if (prev == var)
                amount += (float) cstr.GetCoefficient(var);
            else prev = var;
            cstr.SetCoefficient(var, amount);
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
                    amount += p.amount;
            }
            return amount;
        }

        public static float GetConsumption(this Recipe recipe, Goods product)
        {
            var amount = 0f;
            foreach (var ingredient in recipe.ingredients)
            {
                if (ingredient.goods == product)
                    amount += ingredient.amount;
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
        public static Icon CompilatronIcon;

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

        public static void MoveListElementIndex<T>(this IList<T> list, int from, int to)
        {
            var moving = list[from];
            if (from > to)
            {
                for (var i = from-1; i >= to; i--)
                    list[i + 1] = list[i];
            }
            else
            {
                for (var i = from; i < to; i++)
                    list[i] = list[i + 1];
            }

            list[to] = moving;
        }
        
        public static T RecordUndo<T>(this T target, bool visualOnly = false) where T : ModelObject
        {
            target.CreateUndoSnapshot(visualOnly);
            return target;
        }

        public static void MoveListElement<T>(this IList<T> list, T from, T to)
        {
            var fromIndex = list.IndexOf(from);
            var toIndex = list.IndexOf(to);
            if (fromIndex >= 0 && toIndex >= 0)
                MoveListElementIndex(list, fromIndex, toIndex);
        }

        private const char no = (char) 0;
        private static readonly (char suffix, float multiplier, string format)[] FormatSpec =
        {
            ('μ', 1e6f,  "0.##"),
            ('μ', 1e6f,  "0.##"),
            ('μ', 1e6f,  "0.#"),
            ('μ', 1e6f,  "0"),
            ('μ', 1e6f,  "0"), // skipping m (milli-) because too similar to M (mega-)
            (no,  1e0f,  "0.####"),
            (no,  1e0f,  "0.###"),
            (no,  1e0f,  "0.##"),
            (no,  1e0f,  "0.#"), // [1-10]
            (no,  1e0f,  "0"), 
            (no,  1e0f,  "0"),
            ('K', 1e-3f, "0.#"),
            ('K', 1e-3f, "0"),
            ('K', 1e-3f, "0"),
            ('M', 1e-6f, "0.#"),
            ('M', 1e-6f, "0"),
            ('M', 1e-6f, "0"),
            ('G', 1e-9f, "0.#"),
            ('G', 1e-9f, "0"),
            ('G', 1e-9f, "0"),
            ('T', 1e-12f, "0.#"),
            ('T', 1e-12f, "0"),
        };

        private static readonly StringBuilder amountBuilder = new StringBuilder();

        public static string FormatPercentage(float value) => MathUtils.Round(value * 100f) + "%";
        public static bool HasFlags<T>(this T enunmeration, T flags) where T:unmanaged, Enum
        {
            var target = Unsafe.As<T, int>(ref flags);
            return (Unsafe.As<T, int>(ref enunmeration) & target) == target;
        }
        
        public static bool HasFlagAny<T>(this T enunmeration, T flags) where T:unmanaged, Enum
        {
            return (Unsafe.As<T, int>(ref enunmeration) & Unsafe.As<T, int>(ref flags)) != 0;
        }

        public static string FormatTime(float time)
        {
            amountBuilder.Clear();
            if (time < 10f)
                return $"{time:#.#} seconds";
            if (time < 60f)
                return $"{time:#} seconds";
            if (time < 600f)
                return $"{time/60f:#.#} minutes";
            if (time < 3600f)
                return $"{time/60f:#} minutes";
            if (time < 36000f)
                return $"{time/3600f:#.#} hours";
            return $"{time/3600f:#} hours";
        }
        
        public static string FormatAmount(float amount, bool isPower = false, string prefix = null)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount))
                return "-";
            if (amount == 0f)
                return "0";
            amountBuilder.Clear();
            if (prefix != null)
                amountBuilder.Append(prefix);
            if (amount < 0)
            {
                amountBuilder.Append('-');
                amount = -amount;
            }
            if (isPower)
                amount *= 1e6f;
            var idx = MathUtils.Clamp(MathUtils.Floor(MathF.Log10(amount)) + 8, 0, FormatSpec.Length-1);
            var val = FormatSpec[idx];
            amountBuilder.Append((amount * val.multiplier).ToString(val.format));
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