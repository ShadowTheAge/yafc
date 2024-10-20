using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Yafc.Parser;

internal static class DataParserUtils {
    private static class ConvertersFromLua<T> {
        public static Func<object, T, T>? convert;
    }

    static DataParserUtils() {
        ConvertersFromLua<int>.convert = (o, def) => o is long l ? (int)l : o is double d ? (int)d : o is string s && int.TryParse(s, out int res) ? res : def;
        ConvertersFromLua<float>.convert = (o, def) => o is long l ? l : o is double d ? (float)d : o is string s && float.TryParse(s, out float res) ? res : def;
        ConvertersFromLua<bool>.convert = delegate (object src, bool def) {

            if (src is bool b) {
                return b;
            }

            if (src == null) {
                return def;
            }

            if (src.Equals("true")) {
                return true;
            }

            if (src.Equals("false")) {
                return false;
            }

            return def;
        };
    }

    private static bool Parse<T>(object? value, out T result, T def) {
        if (value == null) {
            result = def;
            return false;
        }

        if (value is T t) {
            result = t;
            return true;
        }
        var converter = ConvertersFromLua<T>.convert;
        if (converter == null) {
            result = def;
            return false;
        }

        result = converter(value, def);
        return true;
    }

    private static bool Parse<T>(object? value, [MaybeNullWhen(false)] out T result) => Parse(value, out result, default!); // null-forgiving: The three-argument Parse takes a non-null default to guarantee a non-null result. We don't make that guarantee.

    public static bool Get<T>(this LuaTable? table, string key, out T result, T def) => Parse(table?[key], out result, def);

    public static bool Get<T>(this LuaTable? table, int key, out T result, T def) => Parse(table?[key], out result, def);

    public static bool Get<T>(this LuaTable? table, string key, [NotNullWhen(true)] out T? result) => Parse(table?[key], out result);

    public static bool Get<T>(this LuaTable? table, int key, [NotNullWhen(true)] out T? result) => Parse(table?[key], out result);

    public static T Get<T>(this LuaTable? table, string key, T def) {
        _ = Parse(table?[key], out var result, def);
        return result;
    }

    public static T Get<T>(this LuaTable table, int key, T def) {
        _ = Parse(table[key], out var result, def);
        return result;
    }

    public static T? Get<T>(this LuaTable table, string key) {
        _ = Parse(table[key], out T? result);
        return result;
    }

    public static T? Get<T>(this LuaTable table, int key) {
        _ = Parse(table[key], out T? result);
        return result;
    }

    public static IEnumerable<T> ArrayElements<T>(this LuaTable? table) => table?.ArrayElements.OfType<T>() ?? [];
}

public static class SpecialNames {
    public const string BurnableFluid = "burnable-fluid.";
    public const string Heat = "heat";
    public const string Void = "void";
    public const string Electricity = "electricity";
    public const string HotFluid = "hot-fluid";
    public const string SpecificFluid = "fluid.";
    public const string MiningRecipe = "mining.";
    public const string BoilerRecipe = "boiler.";
    public const string FakeRecipe = "fake-recipe";
    public const string FixedRecipe = "fixed-recipe.";
    public const string GeneratorRecipe = "generator";
    public const string PumpingRecipe = "pump.";
    public const string Labs = "labs.";
    public const string TechnologyTrigger = "technology-trigger";
    public const string RocketLaunch = "launch";
    public const string RocketCraft = "rocket.";
    public const string ReactorRecipe = "reactor";
    public const string ResearchUnit = "research-unit";
    public const string SpoilRecipe = "spoil";
    public const string PlantRecipe = "plant";
}
