using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Google.OrTools.LinearSolver;
using Serilog;
using Yafc.UI;

namespace Yafc.Model;
public static partial class DataUtils {
    private static readonly ILogger logger = Logging.GetLogger(typeof(DataUtils));

    public static readonly FactorioObjectComparer<FactorioObject> DefaultOrdering = new FactorioObjectComparer<FactorioObject>((x, y) => {
        float yFlow = y.ApproximateFlow();
        float xFlow = x.ApproximateFlow();

        if (xFlow != yFlow) {
            return xFlow.CompareTo(yFlow);
        }

        Recipe? rx = x as Recipe;
        Recipe? ry = y as Recipe;

        if (rx != null || ry != null) {
            float xWaste = rx?.RecipeWaste() ?? 0;
            float yWaste = ry?.RecipeWaste() ?? 0;

            return xWaste.CompareTo(yWaste);
        }

        return y.Cost().CompareTo(x.Cost());
    });
    public static readonly FactorioObjectComparer<Goods> FuelOrdering = new FactorioObjectComparer<Goods>((x, y) => {
        if (x.fuelValue <= 0f && y.fuelValue <= 0f) {
            if (x is Fluid fx && y is Fluid fy) {
                return (x.Cost() / fx.heatValue).CompareTo(y.Cost() / fy.heatValue);
            }

            return DefaultOrdering.Compare(x, y);
        }
        return (x.Cost() / x.fuelValue).CompareTo(y.Cost() / y.fuelValue);
    });
    public static readonly FactorioObjectComparer<Recipe> DefaultRecipeOrdering = new FactorioObjectComparer<Recipe>((x, y) => {
        float yFlow = y.ApproximateFlow();
        float xFlow = x.ApproximateFlow();

        if (yFlow != xFlow) {
            return yFlow > xFlow ? 1 : -1;
        }

        return x.RecipeWaste().CompareTo(y.RecipeWaste());
    });
    public static readonly FactorioObjectComparer<Recipe> AlreadySortedRecipe = new FactorioObjectComparer<Recipe>(DefaultRecipeOrdering.Compare);
    public static readonly FactorioObjectComparer<EntityCrafter> CrafterOrdering = new FactorioObjectComparer<EntityCrafter>((x, y) => {
        if (x.energy?.type != y.energy?.type) {
            return Comparer<EntityEnergyType?>.Default.Compare(x.energy?.type, y.energy?.type);
        }

        if (x.craftingSpeed != y.craftingSpeed) {
            return y.craftingSpeed.CompareTo(x.craftingSpeed);
        }

        return x.Cost().CompareTo(y.Cost());
    });

    public static FavoritesComparer<Goods> FavoriteFuel { get; private set; } = null!; // null-forgiving: Set by SetupForProject when loading a project.
    public static FavoritesComparer<EntityCrafter> FavoriteCrafter { get; private set; } = null!; // null-forgiving: Set by SetupForProject when loading a project.
    public static FavoritesComparer<Module> FavoriteModule { get; private set; } = null!; // null-forgiving: Set by SetupForProject when loading a project.

    public static readonly IComparer<FactorioObject> DeterministicComparer = new FactorioObjectDeterministicComparer();
    public static readonly IComparer<Fluid> FluidTemperatureComparer = new FluidTemperatureComparerImp();

    public static Bits GetMilestoneOrder(FactorioId id) {
        var ms = Milestones.Instance;

        if (ms.GetMilestoneResult(id).IsClear()) {
            // subtracting 1 of all zeros would set all bits ANDing this with lockedMask is equal to lockedMask
            return ms.lockedMask;
        }
        return (ms.GetMilestoneResult(id) - 1) & ms.lockedMask;
    }

    public static string dataPath { get; internal set; } = "";
    public static string modsPath { get; internal set; } = "";
    public static bool expensiveRecipes { get; internal set; }

    /// <summary>
    /// If <see langword="true"/>, recipe selection windows will only display recipes that provide net production or consumption of the <see cref="Goods"/> in question.
    /// If <see langword="false"/>, recipe selection windows will show all recipes that produce or consume any quantity of that <see cref="Goods"/>.<br/>
    /// For example, Kovarex enrichment will appear for both production and consumption of both U-235 and U-238 when <see langword="false"/>,
    /// but will appear as only producing U-235 and consuming U-238 when <see langword="true"/>.
    /// </summary>
    public static bool netProduction { get; internal set; }
    public static Icon NoFuelIcon { get; internal set; }
    public static Icon WarningIcon { get; internal set; }
    public static Icon HandIcon { get; internal set; }

    public static readonly Random random = new Random();

    /// <summary>
    /// Call to get the favorite or only useful item in the list, considering milestones, accessibility, and <see cref="FactorioObject.specialType"/>,
    /// provided there is exactly one such item.
    /// If no best item exists, returns <see langword="null"/>. Always returns a tooltip applicable to using ctrl+click to add a recipe.
    /// </summary>
    /// <typeparam name="T">The element type of <paramref name="list"/>. This type must be derived from <see cref="FactorioObject"/>.</typeparam>
    /// <param name="list">The array of items to search.</param>
    /// <param name="recipeHint">Upon return, contains a hint that is applicable to using ctrl+click to add a recipe.
    /// This will either suggest using ctrl+click, or explain why ctrl+click cannot be used.
    /// It is not useful when <typeparamref name="T"/> is not <see cref="Recipe"/>.</param>
    /// <returns>Items that are not accessible at the current milestones are always ignored. After those have been discarded, 
    /// the return value is the first applicable entry in the following list:
    /// <list type="bullet">
    /// <item>The only normal item in <paramref name="list"/>.</item>
    /// <item>The only normal user favorite in <paramref name="list"/>.</item>
    /// <item>The only item in <paramref name="list"/>, considering both normal and special items.</item>
    /// <item>The only user favorite in <paramref name="list"/>, considering both normal and special items.</item>
    /// <item>If no previous options are applicable, <see langword="null"/>.</item>
    /// </list></returns>
    public static T? SelectSingle<T>(this T[] list, out string recipeHint) where T : FactorioObject {
        return @internal(list, true, out recipeHint) ?? @internal(list, false, out recipeHint);

        static T? @internal(T[] list, bool excludeSpecial, out string recipeHint) {
            HashSet<FactorioObject> userFavorites = Project.current.preferences.favorites;
            bool acceptOnlyFavorites = false;
            T? element = null;

            if (list.Any(t => t.IsAccessible())) {
                recipeHint = "Hint: Complete milestones to enable ctrl+click";
            }
            else {
                recipeHint = "Hint: Mark a recipe as accessible to enable ctrl+click";
            }

            foreach (T elem in list) {
                // Always consider normal entries. A list with two normals and one special should select nothing, rather than selecting the only special item.
                if (!elem.IsAccessibleWithCurrentMilestones() || (elem.specialType != FactorioObjectSpecialType.Normal && excludeSpecial)) {
                    continue;
                }

                if (userFavorites.Contains(elem)) {
                    if (!acceptOnlyFavorites || element == null) {
                        element = elem;
                        recipeHint = "Hint: ctrl+click to add your favorited recipe";
                        acceptOnlyFavorites = true;
                    }
                    else {
                        recipeHint = "Hint: Cannot ctrl+click with multiple favorited recipes";

                        return null;
                    }
                }
                else if (!acceptOnlyFavorites) {
                    if (element == null) {
                        element = elem;
                        recipeHint = excludeSpecial ? "Hint: ctrl+click to add the accessible normal recipe" : "Hint: ctrl+click to add the accessible recipe";
                    }
                    else {
                        element = null;
                        recipeHint = "Hint: Set a favorite recipe to add it with ctrl+click";
                        acceptOnlyFavorites = true;
                    }
                }
            }

            return element;
        }
    }

    public static void SetupForProject(Project project) {
        FavoriteFuel = new FavoritesComparer<Goods>(project, FuelOrdering);
        FavoriteCrafter = new FavoritesComparer<EntityCrafter>(project, CrafterOrdering);
        FavoriteModule = new FavoritesComparer<Module>(project, DefaultOrdering);
    }

    private class FactorioObjectDeterministicComparer : IComparer<FactorioObject> {
        // id comparison is deterministic because objects are sorted deterministically
        public int Compare(FactorioObject? x, FactorioObject? y) => Comparer<int?>.Default.Compare((int?)x?.id, (int?)y?.id);
    }

    private class FluidTemperatureComparerImp : IComparer<Fluid> {
        public int Compare(Fluid? x, Fluid? y) => Comparer<int?>.Default.Compare(x?.temperature, y?.temperature);
    }

    public class FactorioObjectComparer<T>(Comparison<T> similarComparison) : IComparer<T> where T : FactorioObject {
        private readonly Comparison<T> similarComparison = similarComparison;

        public int Compare(T? x, T? y) {
            if (x == null) {
                return y == null ? 0 : 1;
            }

            if (y == null) {
                return -1;
            }

            if (x.specialType != y.specialType) {
                return x.specialType - y.specialType;
            }

            var msx = GetMilestoneOrder(x.id);
            var msy = GetMilestoneOrder(y.id);

            if (msx != msy) {
                return msx.CompareTo(msy);
            }

            return similarComparison(x, y);
        }
    }

    public static Solver CreateSolver() {
        Solver solver = Solver.CreateSolver("GLOP_LINEAR_PROGRAMMING");
        // Relax solver parameters as returning imprecise solution is better than no solution at all
        // It is not like we need 8 digits of precision after all, most computations in YAFC are done in singles
        // see all properties here: https://github.com/google/or-tools/blob/stable/ortools/glop/parameters.proto
        _ = solver.SetSolverSpecificParametersAsString("solution_feasibility_tolerance:1e-1");
        return solver;
    }

    public static Solver.ResultStatus TrySolveWithDifferentSeeds(this Solver solver) {
        for (int i = 0; i < 3; i++) {
            Stopwatch time = Stopwatch.StartNew();
            var result = solver.Solve();
            logger.Information("Solution completed in {ElapsedTime}ms with result {result}", time.ElapsedMilliseconds, result);

            if (result == Solver.ResultStatus.ABNORMAL) {
                _ = solver.SetSolverSpecificParametersAsString("random_seed:" + random.Next());
                continue;
            } /*else
                VerySlowTryFindBadObjective(solver);*/

            return result;
        }
        return Solver.ResultStatus.ABNORMAL;
    }

    public static void VerySlowTryFindBadObjective(Solver solver) {
        var vars = solver.variables();
        var obj = solver.Objective();
        logger.Information(solver.ExportModelAsLpFormat(false));

        foreach (var v in vars) {
            obj.SetCoefficient(v, 0);
            var result = solver.Solve();

            if (result == Solver.ResultStatus.OPTIMAL) {
                logger.Warning("Infeasibility candidate: {candidate}", v.Name());

                return;
            }
        }
    }

    public static bool RemoveValue<TKey, TValue>(this Dictionary<TKey, TValue> dict, TValue value) where TKey : notnull {
        var comparer = EqualityComparer<TValue>.Default;
        foreach (var (k, v) in dict) {
            if (comparer.Equals(v, value)) {
                _ = dict.Remove(k);

                return true;
            }
        }

        return false;
    }

    public static void SetCoefficientCheck(this Constraint cstr, Variable var, float amount, ref Variable prev) {
        if (prev == var) {
            amount += (float)cstr.GetCoefficient(var);
        }
        else {
            prev = var;
        }

        cstr.SetCoefficient(var, amount);
    }

    public class FavoritesComparer<T>(Project project, IComparer<T> def) : IComparer<T> where T : FactorioObject {
        private readonly Dictionary<T, int> bumps = [];
        private readonly IComparer<T> def = def;
        private readonly HashSet<FactorioObject> userFavorites = project.preferences.favorites;

        public void AddToFavorite(T x, int amount = 1) {
            if (x == null) {
                return;
            }

            _ = bumps.TryGetValue(x, out int prev);
            bumps[x] = prev + amount;
        }
        public int Compare(T? x, T? y) {
            if (x is null || y is null) {
                return Comparer<object>.Default.Compare(x, y);
            }

            bool hasX = userFavorites.Contains(x);
            bool hasY = userFavorites.Contains(y);

            if (hasX != hasY) {
                return hasY.CompareTo(hasX);
            }

            _ = bumps.TryGetValue(x, out int ix);
            _ = bumps.TryGetValue(y, out int iy);

            if (ix == iy) {
                return def.Compare(x, y);
            }

            return iy.CompareTo(ix);
        }
    }

    public static float GetProductionPerRecipe(this RecipeOrTechnology recipe, Goods product) {
        float amount = 0f;

        foreach (var p in recipe.products) {
            if (p.goods == product) {
                amount += p.amount;
            }
        }
        return amount;
    }

    public static float GetProductionForRow(this RecipeRow row, Goods product) {
        float amount = 0f;

        foreach (var p in row.recipe.products) {
            if (p.goods == product) {
                amount += p.GetAmountForRow(row);
            }
        }
        return amount;
    }

    public static float GetConsumptionPerRecipe(this RecipeOrTechnology recipe, Goods product) {
        float amount = 0f;

        foreach (var ingredient in recipe.ingredients) {
            if (ingredient.ContainsVariant(product)) {
                amount += ingredient.amount;
            }
        }
        return amount;
    }

    public static float GetConsumptionForRow(this RecipeRow row, Goods ingredient) {
        float amount = 0f;

        foreach (var i in row.recipe.ingredients) {
            if (i.ContainsVariant(ingredient)) {
                amount += i.amount * (float)row.recipesPerSecond;
            }
        }
        return amount;
    }

    public static FactorioObjectComparer<Recipe> GetRecipeComparerFor(Goods goods) => new FactorioObjectComparer<Recipe>((x, y) => (x.Cost(true) / x.GetProductionPerRecipe(goods)).CompareTo(y.Cost(true) / y.GetProductionPerRecipe(goods)));

    public static T? AutoSelect<T>(this IEnumerable<T> list, IComparer<T>? comparer = default) {
        if (comparer == null) {
            if (DefaultOrdering is IComparer<T> defaultComparer) {
                comparer = defaultComparer;
            }
            else {
                comparer = Comparer<T>.Default;
            }
        }

        bool first = true;
        T? best = default;

        foreach (var elem in list) {
            if (first || comparer.Compare(best, elem) > 0) {
                first = false;
                best = elem;
            }
        }
        return best;
    }

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> values) where T : notnull => values.Where(x => x is not null)!; // null-forgiving: We're filtering out the nulls.

    /// <summary>
    /// As <see cref="Enumerable.SingleOrDefault{TSource}(IEnumerable{TSource})"/>, but multiple values do not cause an exception when
    /// <paramref name="throwIfMultiple"/> is <see langword="false"/>. in that case, this method returns the default value for <typeparamref name="T"/> instead.
    /// </summary>
    public static T? SingleOrDefault<T>(this IEnumerable<T> values, bool throwIfMultiple)
        => throwIfMultiple ? values.SingleOrDefault() : values.SingleOrDefault(null!, false); // null-forgiving: Our SingleOrDefault allows a null predicate when throwIfMultiple is false.

    /// <summary>
    /// As <see cref="Enumerable.SingleOrDefault{TSource}(IEnumerable{TSource}, Func{TSource, bool})"/>, but multiple matching values do not cause an exception when
    /// <paramref name="throwIfMultiple"/> is <see langword="false"/>. in that case, this method returns the default value for <typeparamref name="T"/> instead.
    /// </summary>
    public static T? SingleOrDefault<T>(this IEnumerable<T> values, Func<T, bool> predicate, bool throwIfMultiple) {
        if (throwIfMultiple) {
            return values.SingleOrDefault(predicate);
        }

        bool found = false;
        T? foundItem = default;

        foreach (T item in values) {
            if (predicate?.Invoke(item) ?? true) { // defend against null here to allow the other overload to pass null, rather than re-implementing the loop.
                if (found) {
                    return default;
                }

                found = true;
                foundItem = item;
            }
        }
        return foundItem;
    }

    public static void MoveListElementIndex<T>(this IList<T> list, int from, int to) {
        var moving = list[from];

        if (from > to) {
            for (int i = from - 1; i >= to; i--) {
                list[i + 1] = list[i];
            }
        }
        else {
            for (int i = from; i < to; i++) {
                list[i] = list[i + 1];
            }
        }

        list[to] = moving;
    }

    public static T RecordUndo<T>(this T target, bool visualOnly = false) where T : ModelObject {
        target.CreateUndoSnapshot(visualOnly);
        return target;
    }

    public static T RecordChange<T>(this T target) where T : ModelObject {
        target.undo.RecordChange();
        return target;
    }

    public static void MoveListElement<T>(this IList<T> list, T from, T to) where T : notnull {
        int fromIndex = list.IndexOf(from);
        int toIndex = list.IndexOf(to);
        if (fromIndex >= 0 && toIndex >= 0) {
            MoveListElementIndex(list, fromIndex, toIndex);
        }
    }

    private const char NO = (char)0;
    public static readonly (char suffix, float multiplier, string format)[] FormatSpec =
    [
        ('μ', 1e6f, "0.##"),
        ('μ', 1e6f, "0.##"),
        ('μ', 1e6f, "0.#"),
        ('μ', 1e6f, "0"),
        ('μ', 1e6f, "0"), // skipping m (milli-) because too similar to M (mega-)
        (NO, 1e0f, "0.####"),
        (NO, 1e0f, "0.###"),
        (NO, 1e0f, "0.##"),
        (NO, 1e0f, "0.##"), // [1-10]
        (NO, 1e0f, "0.#"),
        (NO, 1e0f, "0"),
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
    ];

    public static readonly (char suffix, float multiplier, string format)[] PreciseFormat =
    [
        ('μ', 1e6f, "0.000000"),
        ('μ', 1e6f, "0.000000"),
        ('μ', 1e6f, "0.00000"),
        ('μ', 1e6f, "0.0000"),
        ('μ', 1e6f, "0.0000"), // skipping m (milli-) because too similar to M (mega-)
        (NO, 1e0f, "0.00000000"),
        (NO, 1e0f, "0.0000000"),
        (NO, 1e0f, "0.000000"),
        (NO, 1e0f, "0.000000"), // [1-10]
        (NO, 1e0f, "00.00000"),
        (NO, 1e0f, "000.0000"),
        (NO, 1e0f, "0 000.000"),
        (NO, 1e0f, "00 000.00"),
        (NO, 1e0f, "000 000.0"),
        (NO, 1e0f, "0 000 000"),
    ];

    private static readonly StringBuilder amountBuilder = new StringBuilder();
    public static bool HasFlags<T>(this T enumeration, T flags) where T : unmanaged, Enum {
        int target = Unsafe.As<T, int>(ref flags);

        return (Unsafe.As<T, int>(ref enumeration) & target) == target;
    }

    public static bool HasFlagAny<T>(this T enumeration, T flags) where T : unmanaged, Enum => (Unsafe.As<T, int>(ref enumeration) & Unsafe.As<T, int>(ref flags)) != 0;

    public static string FormatTime(float time) {
        _ = amountBuilder.Clear();

        if (time < 10f) {
            return $"{time:#.#} seconds";
        }

        if (time < 60f) {
            return $"{time:#} seconds";
        }

        if (time < 600f) {
            return $"{time / 60f:#.#} minutes";
        }

        if (time < 3600f) {
            return $"{time / 60f:#} minutes";
        }

        if (time < 36000f) {
            return $"{time / 3600f:#.#} hours";
        }

        return $"{time / 3600f:#} hours";
    }

    public static string FormatAmount(float amount, UnitOfMeasure unit, string? prefix = null, string? suffix = null, bool precise = false) {
        var (multiplier, unitSuffix) = Project.current == null ? (1f, null) : Project.current.ResolveUnitOfMeasure(unit);

        return FormatAmountRaw(amount, multiplier, unitSuffix, precise ? PreciseFormat : FormatSpec, prefix, suffix);
    }

    public static string FormatAmountRaw(float amount, float unitMultiplier, string? unitSuffix, (char suffix, float multiplier, string format)[] formatSpec, string? prefix = null, string? suffix = null) {
        if (float.IsNaN(amount) || float.IsInfinity(amount)) {
            return "-";
        }

        if (amount == 0f) {
            return "0";
        }

        _ = amountBuilder.Clear();
        if (prefix != null) {
            _ = amountBuilder.Append(prefix);
        }

        if (amount < 0) {
            _ = amountBuilder.Append('-');
            amount = -amount;
        }

        amount *= unitMultiplier;
        int idx = MathUtils.Clamp(MathUtils.Floor(MathF.Log10(amount)) + 8, 0, formatSpec.Length - 1);
        var val = formatSpec[idx];
        _ = amountBuilder.Append((amount * val.multiplier).ToString(val.format));

        if (val.suffix != NO) {
            _ = amountBuilder.Append(val.suffix);
        }

        _ = amountBuilder.Append(unitSuffix);

        if (suffix != null) {
            _ = amountBuilder.Append(suffix);
        }

        return amountBuilder.ToString();
    }

    [GeneratedRegex(@"^([-+0-9.e]+)([μukmgt]?)(/[hmst]|[WJsbp%]?)$", RegexOptions.IgnoreCase)]
    private static partial Regex ParseAmountRegex();

    /// <summary>
    /// Tries to parse a user-supplied production rate into the standard internal format, as specified by <paramref name="unit"/>.
    /// Values are accepted in any format that YAFC can display, and consist of:<br/>
    /// * A floating point number<br/>
    /// * An optional SI prefix from μukMGT, case sensitive only for u and μ. (For historical reasons, m and M are both mega-; the milli- prefix is unused and unavailable.)<br/>
    /// * An optional W, J, s, b, p, %, /s, /m, /h, or /t, case insensitive and permitted when appropriate (e.g. W only for power; no b on fluids; p only if Project.current.preferences.fluidUnit has a value).
    /// </summary>
    /// <param name="str">The string to parse.</param>
    /// <param name="amount">The parsed amount, or an unspecified value if the string could not be parsed.</param>
    /// <param name="unit">The unit that applies to this value. <see cref="UnitOfMeasure.Celsius"/> is not supported.</param>
    /// <returns>True if the string could be parsed as the specified unit, false otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="unit"/> is <see cref="UnitOfMeasure.Celsius"/>.</exception>
    public static bool TryParseAmount(string str, out float amount, UnitOfMeasure unit) {
        if (unit is UnitOfMeasure.Celsius) { throw new ArgumentException("Parsing to UnitOfMeasure.Celcius is not supported.", nameof(unit)); }

        var (mul, _) = Project.current.ResolveUnitOfMeasure(unit);
        float multiplier = unit is UnitOfMeasure.Megawatt or UnitOfMeasure.Megajoule ? 1e6f : 1f;

        str = str.Replace(" ", ""); // Remove spaces to support parsing from the "10 000" precise format, and to simplify the regex.
        var groups = ParseAmountRegex().Match(str).Groups;
        amount = 0;

        if (groups.Count < 4 || !float.TryParse(groups[1].Value, out amount)) {
            return false;
        }

        switch (groups[2].Value.SingleOrDefault()) { // μukMGT
            case 'u' or 'μ':
                multiplier = 1e-6f;
                break;
            case 'k' or 'K':
                multiplier = 1e3f;
                break;
            case 'm' or 'M':
                multiplier = 1e6f;
                break;
            case 'g' or 'G':
                multiplier = 1e9f;
                break;
            case 't' or 'T':
                multiplier = 1e12f;
                break;
            case 'U' or 'Μ' or 'K': // capital u or μ, or Kelvin symbol; false positive in the regex match
                return false;
        }

        switch (groups[3].Value.ToUpperInvariant()) { // JWsbp% or /hms
            case "W" when unit is UnitOfMeasure.Megawatt:
            case "J" when unit is UnitOfMeasure.Megajoule:
                if (groups[2].Value.Length == 0) {
                    // "10", "10M" and "10MW" should all be parsed as ten megawatts, but "10W" should be parsed as ten watts.
                    multiplier = 1;
                }
                break;
            case "S" when unit is UnitOfMeasure.Second:
            case "%" when unit is UnitOfMeasure.Percent:
                break;
            case "W" or "J" or "S" or "%":
                // Text units that don't match the expected units.
                return false;
            case not "" when unit is not UnitOfMeasure.ItemPerSecond and not UnitOfMeasure.FluidPerSecond and not UnitOfMeasure.PerSecond:
                // Time-based modifiers on non-time-based units.
                return false;
            case "B":
                if (unit != UnitOfMeasure.ItemPerSecond) {
                    return false; // allow belts for items only
                }
                if (Project.current.preferences.itemUnit > 0) {
                    mul = 1 / Project.current.preferences.itemUnit;
                }
                else if (Project.current.preferences.defaultBelt is not null) {
                    mul = 1 / Project.current.preferences.defaultBelt.beltItemsPerSecond;
                }
                else {
                    return false; // I don't know what to divide by when setting mul
                }
                break;
            case "P":
                // allow pipes only for fluids, and only when the pipe throughput is specified
                if (unit != UnitOfMeasure.FluidPerSecond || Project.current.preferences.fluidUnit == 0) {
                    return false;
                }
                mul = 1 / Project.current.preferences.fluidUnit;
                break;
            case "/S":
                mul = 1;
                break;
            case "/M":
                mul = 60;
                break;
            case "/H":
                mul = 3600;
                break;
            case "/T":
                (mul, _) = Project.current.preferences.GetPerTimeUnit();
                break;
        }

        multiplier /= mul;
        amount *= multiplier;

        return amount is <= 1e15f and >= -1e15f;
    }

    public static void WriteException(this TextWriter writer, Exception ex) {
        writer.WriteLine("Exception: " + ex.Message);
        writer.WriteLine(ex.StackTrace);
    }

    public static string? ReadLine(byte[] buffer, ref int position) {
        if (position > buffer.Length) {
            return null;
        }

        int nextPosition = Array.IndexOf(buffer, (byte)'\n', position);

        if (nextPosition == -1) {
            nextPosition = buffer.Length;
        }

        string str = Encoding.UTF8.GetString(buffer, position, nextPosition - position);
        position = nextPosition + 1;

        return str;
    }

    public static bool Match(this FactorioObject? obj, SearchQuery query) {
        if (query.empty) {
            return true;
        }

        if (obj == null) {
            return false;
        }

        foreach (string token in query.tokens) {
            if (obj.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0
                && obj.locName.IndexOf(token, StringComparison.InvariantCultureIgnoreCase) < 0
                && (obj.locDescr == null || obj.locDescr.IndexOf(token, StringComparison.InvariantCultureIgnoreCase) < 0)
                && (obj.factorioType == null || obj.factorioType.IndexOf(token, StringComparison.InvariantCultureIgnoreCase) < 0)) {

                return false;
            }
        }

        return true;
    }

    public static bool IsSourceResource(this FactorioObject? obj) => Project.current.preferences.sourceResources.Contains(obj!); // null-forgiving: non-nullable collections are happy to report they don't contain null values.
}

public enum UnitOfMeasure {
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
