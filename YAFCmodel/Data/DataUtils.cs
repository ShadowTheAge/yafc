using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Unicode;
using Google.OrTools.LinearSolver;
using SDL2;
using YAFC.UI;

namespace YAFC.Model
{
    public static class DataUtils
    {
        public static readonly FactorioObjectComparer<FactorioObject> DefaultOrdering = new FactorioObjectComparer<FactorioObject>((x, y) =>
        {
            var yflow = y.ApproximateFlow();
            var xflow = x.ApproximateFlow();
            if (xflow != yflow)
                return xflow.CompareTo(yflow);

            var rx = x as Recipe;
            var ry = y as Recipe;
            if (rx != null || ry != null)
            {
                var xwaste = rx?.RecipeWaste() ?? 0;
                var ywaste = ry?.RecipeWaste() ?? 0;
                return xwaste.CompareTo(ywaste);
            }

            return y.Cost().CompareTo(x.Cost());
        });
        public static readonly FactorioObjectComparer<Goods> FuelOrdering = new FactorioObjectComparer<Goods>((x, y) =>
        {
            if (x.fuelValue <= 0f && y.fuelValue <= 0f)
            {
                if (x is Fluid fx && y is Fluid fy)
                    return (x.Cost() / fx.heatValue).CompareTo(y.Cost() / fy.heatValue);
                return DefaultOrdering.Compare(x, y);
            } 
            return (x.Cost() / x.fuelValue).CompareTo(y.Cost() / y.fuelValue);
        });
        public static readonly FactorioObjectComparer<Recipe> DefaultRecipeOrdering = new FactorioObjectComparer<Recipe>((x, y) =>
        {
            var yflow = y.ApproximateFlow();
            var xflow = x.ApproximateFlow();
            if (yflow != xflow)
                return yflow > xflow ? 1 : -1;
            return x.RecipeWaste().CompareTo(y.RecipeWaste());
        });
        public static readonly FactorioObjectComparer<EntityCrafter> CrafterOrdering = new FactorioObjectComparer<EntityCrafter>((x, y) =>
        {
            if (x.energy.type != y.energy.type)
                return x.energy.type.CompareTo(y.energy.type);
            if (x.craftingSpeed != y.craftingSpeed)
                return y.craftingSpeed.CompareTo(x.craftingSpeed);
            return x.Cost().CompareTo(y.Cost());
        });
        
        public static FavouritesComparer<Goods> FavouriteFuel { get; private set; }
        public static FavouritesComparer<EntityCrafter> FavouriteCrafter { get; private set; }
        public static FavouritesComparer<Item> FavouriteModule { get; private set; }
        
        public static readonly IComparer<FactorioObject> DeterministicComparer = new FactorioObjectDeterministicComparer();
        public static readonly IComparer<Fluid> FluidTemperatureComparer = new FluidTemperatureComparerImp();

        public static ulong GetMilestoneOrder(FactorioId id)
        {
            var ms = Milestones.Instance;
            return (ms.milestoneResult[id] - 1) & ms.lockedMask;
        }

        public static string dataPath { get; internal set; }
        public static string modsPath { get; internal set; }
        public static bool expensiveRecipes { get; internal set; }
        public static string[] allMods { get; internal set; }
        public static readonly Random random = new Random();

        public static bool SelectSingle<T>(this T[] list, out T element) where T:FactorioObject
        {
            var userFavourites = Project.current.preferences.favourites;
            var acceptOnlyFavourites = false;
            element = null;
            foreach (var elem in list)
            {
                if (!elem.IsAccessibleWithCurrentMilestones() || elem.specialType != FactorioObjectSpecialType.Normal)
                    continue;
                if (userFavourites.Contains(elem))
                {
                    if (!acceptOnlyFavourites || element == null)
                    {
                        element = elem;
                        acceptOnlyFavourites = true;
                    }
                    else
                    {
                        element = null;
                        return false;
                    }
                }
                else if (!acceptOnlyFavourites)
                {
                    if (element == null)
                        element = elem;
                    else
                    {
                        element = null;
                        acceptOnlyFavourites = true;
                    }
                }
            }

            return element != null;
        }

        public static void SetupForProject(Project project)
        {
            FavouriteFuel = new FavouritesComparer<Goods>(project, FuelOrdering);
            FavouriteCrafter = new FavouritesComparer<EntityCrafter>(project, CrafterOrdering);
            FavouriteModule = new FavouritesComparer<Item>(project, DefaultOrdering);
        }

        private class FactorioObjectDeterministicComparer : IComparer<FactorioObject>
        {
            public int Compare(FactorioObject x, FactorioObject y) => x.id.CompareTo(y.id); // id comparison is deterministic because objects are sorted deterministicaly
        }

        private class FluidTemperatureComparerImp : IComparer<Fluid>
        {
            public int Compare(Fluid x, Fluid y) => x.temperature.CompareTo(y.temperature);
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
                if (x == null)
                    return y == null ? 0 : 1;
                if (y == null)
                    return -1;
                if (x.specialType != y.specialType)
                    return x.specialType - y.specialType;
                var msx = GetMilestoneOrder(x.id);
                var msy = GetMilestoneOrder(y.id);
                if (msx != msy)
                    return msx.CompareTo(msy);
                return similarComparison(x, y);
            }
        }
        
        public static Solver CreateSolver(string name)
        {
            var solver = Solver.CreateSolver("GLOP_LINEAR_PROGRAMMING");
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
            Console.WriteLine(solver.ExportModelAsLpFormat(false));
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

        public static bool RemoveValue<TKey, TValue>(this Dictionary<TKey, TValue> dict, TValue value)
        {
            var comparer = EqualityComparer<TValue>.Default;
            foreach (var (k, v) in dict)
            {
                if (comparer.Equals(v, value))
                {
                    dict.Remove(k);
                    return true;
                }
            }

            return false;
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
            private readonly HashSet<FactorioObject> userFavourites;
            public FavouritesComparer(Project project, IComparer<T> def)
            {
                this.def = def;
                userFavourites = project.preferences.favourites;
            }

            public void AddToFavourite(T x, int amount = 1)
            {
                if (x == null)
                    return;
                bumps.TryGetValue(x, out var prev);
                bumps[x] = prev+amount;
            }
            public int Compare(T x, T y)
            {
                var hasX = userFavourites.Contains(x);
                var hasY = userFavourites.Contains(y);
                if (hasX != hasY)
                    return hasY.CompareTo(hasX);

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
        
        public static float GetProduction(this Recipe recipe, Goods product, float productivity)
        {
            var amount = 0f;
            foreach (var p in recipe.products)
            {
                if (p.goods == product)
                    amount += p.GetAmount(productivity);
            }
            return amount;
        }

        public static float GetConsumption(this Recipe recipe, Goods product)
        {
            var amount = 0f;
            foreach (var ingredient in recipe.ingredients)
            {
                if (ingredient.ContainsVariant(product))
                    amount += ingredient.amount;
            }
            return amount;
        }

        public static FactorioObjectComparer<Recipe> GetRecipeComparerFor(Goods goods)
        {
            return new FactorioObjectComparer<Recipe>((x, y) => (x.Cost(true)/x.GetProduction(goods)).CompareTo(y.Cost(true)/y.GetProduction(goods)));
        }

        public static Icon NoFuelIcon;
        public static Icon WarningIcon;
        public static Icon HandIcon;

        public static T AutoSelect<T>(this IEnumerable<T> list, IComparer<T> comparer = default)
        {
            if (comparer == null)
            {
                if (DefaultOrdering is IComparer<T> defaultComparer)
                    comparer = defaultComparer;
                else comparer = Comparer<T>.Default;
            }
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
        public static readonly (char suffix, float multiplier, string format)[] FormatSpec =
        {
            ('μ', 1e6f,  "0.##"),
            ('μ', 1e6f,  "0.##"),
            ('μ', 1e6f,  "0.#"),
            ('μ', 1e6f,  "0"),
            ('μ', 1e6f,  "0"), // skipping m (milli-) because too similar to M (mega-)
            (no,  1e0f,  "0.####"),
            (no,  1e0f,  "0.###"),
            (no,  1e0f,  "0.##"),
            (no,  1e0f,  "0.##"), // [1-10]
            (no,  1e0f,  "0.#"), 
            (no,  1e0f,  "0"),
            ('k', 1e-3f, "0.##"),
            ('k', 1e-3f, "0.#"),
            ('k', 1e-3f, "0"),
            ('M', 1e-6f, "0.##"),
            ('M', 1e-6f, "0.#"),
            ('M', 1e-6f, "0"),
            ('G', 1e-9f, "0.##"),
            ('G', 1e-9f, "0.#"),
            ('G', 1e-9f, "0"),
            ('T', 1e-12f, "0.##"),
            ('T', 1e-12f, "0.#"),
        };
        
        public static readonly (char suffix, float multiplier, string format)[] PreciseFormat =
        {
            ('μ', 1e6f,  "0.000000"),
            ('μ', 1e6f,  "0.000000"),
            ('μ', 1e6f,  "0.00000"),
            ('μ', 1e6f,  "0.0000"),
            ('μ', 1e6f,  "0.0000"), // skipping m (milli-) because too similar to M (mega-)
            (no,  1e0f,  "0.00000000"),
            (no,  1e0f,  "0.0000000"),
            (no,  1e0f,  "0.000000"),
            (no,  1e0f,  "0.000000"), // [1-10]
            (no,  1e0f,  "00.00000"), 
            (no,  1e0f,  "000.0000"),
            (no,  1e0f,  "0 000.000"),
            (no,  1e0f,  "00 000.00"),
            (no,  1e0f,  "000 000.0"),
            (no,  1e0f,  "0 000 000"),
        };

        private static readonly StringBuilder amountBuilder = new StringBuilder();
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
        
        public static string FormatAmount(float amount, UnitOfMeasure unit, string prefix = null, string suffix = null, bool precise = false)
        {
            var (multiplier, unitSuffix) = Project.current == null ? (1f, null) : Project.current.ResolveUnitOfMeasure(unit);
            return FormatAmountRaw(amount, multiplier, unitSuffix, prefix, suffix, precise ? PreciseFormat : FormatSpec);
        }

        public static string FormatAmountRaw(float amount, float unitMultiplier, string unitSuffix, string prefix = null, string suffix = null, (char suffix, float multiplier, string format)[] formatSpec = null)
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

            amount *= unitMultiplier;
            var idx = MathUtils.Clamp(MathUtils.Floor(MathF.Log10(amount)) + 8, 0, formatSpec.Length-1);
            var val = formatSpec[idx];
            amountBuilder.Append((amount * val.multiplier).ToString(val.format));
            if (val.suffix != no)
                amountBuilder.Append(val.suffix);
            amountBuilder.Append(unitSuffix);
            if (suffix != null)
                amountBuilder.Append(suffix);
            return amountBuilder.ToString();
        }

        public static bool TryParseAmount(string str, out float amount, UnitOfMeasure unit)
        {
            var (mul, _) = Project.current.ResolveUnitOfMeasure(unit);
            var lastValidChar = 0;
            var multiplier = unit == UnitOfMeasure.Megawatt ? 1e6f : 1f;
            amount = 0;
            foreach (var c in str)
            {
                if (c >= '0' && c <= '9' || c == '.' || c == '-' || c == 'e')
                    ++lastValidChar;
                else
                {
                    if (lastValidChar == 0)
                        return false;
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
                        case 'μ': case 'u':
                            multiplier = 1e-6f;
                            break;
                    }
                    break;
                }
            }
            multiplier /= mul;
            var substr = str.Substring(0, lastValidChar);
            if (!float.TryParse(substr, out amount)) return false;
            amount *= multiplier;
            if (amount > 1e15)
                return false;
            return true;
        }
        
        public static void WriteException(this TextWriter writer, Exception ex)
        {
            writer.WriteLine("Exception: "+ex.Message);
            writer.WriteLine(ex.StackTrace);
        }
        
        public static string ReadLine(byte[] buffer, ref int position)
        {
            if (position > buffer.Length)
                return null;
            var nextPosition = Array.IndexOf(buffer, (byte) '\n', position);
            if (nextPosition == -1)
                nextPosition = buffer.Length;
            var str = Encoding.UTF8.GetString(buffer, position, nextPosition - position);
            position = nextPosition+1;
            return str;
        }
        
        public static bool Match(this FactorioObject obj, SearchQuery query)
        {
            if (query.empty)
                return true;
            if (obj == null)
                return false;
            foreach (var token in query.tokens)
            {   
                if (obj.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0 &&
                    obj.locName.IndexOf(token, StringComparison.InvariantCultureIgnoreCase) < 0 &&
                    (obj.locDescr == null || obj.locDescr.IndexOf(token, StringComparison.InvariantCultureIgnoreCase) < 0)) 
                    return false;
            }

            return true;
        }

        public static bool IsSourceResource(this FactorioObject obj)
        {
            return Project.current.preferences.sourceResources.Contains(obj);
        }
    }

    public enum UnitOfMeasure
    {
        None,
        Percent,
        Second,
        PerSecond,
        ItemPerSecond,
        FluidPerSecond,
        Megawatt,
        Megajoule,
        Celsius,
    }
}