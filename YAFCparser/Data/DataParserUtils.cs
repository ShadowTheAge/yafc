using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YAFC.Parser
{
    public static class DataParserUtils
    {
        private static class ConvertersFromLua<T>
        {
            public static Func<object, T, T> convert;
        }

        static DataParserUtils()
        {
            ConvertersFromLua<int>.convert = (o, def) => o is long l ? (int) l : o is double d ? (int) d : def;
            ConvertersFromLua<float>.convert = (o, def) => o is long l ? (float) l : o is double d ? (float) d : def;
        }

        private static bool Parse<T>(object value, out T result, T def = default)
        {
            if (value == null)
            {
                result = def;
                return false;
            }

            if (value is T t)
            {
                result = t;
                return true;
            }
            var converter = ConvertersFromLua<T>.convert;
            if (converter == null)
            {
                result = def;
                return false;
            }

            result = converter(value, def);
            return true;
        }
        
        public static bool Get<T>(this LuaTable table, string key, out T result, T def = default) =>
            Parse(table[key], out result, def);
        public static bool Get<T>(this LuaTable table, int key, out T result, T def = default) =>
            Parse(table[key], out result, def);
        public static T Get<T>(this LuaTable table, string key, T def)
        {
            Parse(table[key], out var result, def);
            return result;
        }
        
        public static T Get<T>(this LuaTable table, int key, T def)
        {
            Parse(table[key], out var result, def);
            return result;
        }
        
        public static T[] SingleElementArray<T>(this T item) => new T[] {item};

        public static IEnumerable<T> ArrayElements<T>(this LuaTable table) => table.ArrayElements.OfType<T>();

        public static void WriteException(this TextWriter writer, Exception ex)
        {
            writer.WriteLine("Exception: "+ex.Message);
            writer.WriteLine(ex.StackTrace);
        }
    }
    
    public static class SpecialNames
    {
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
        public const string RocketLaunch = "launch";
        public const string ReactorRecipe = "reactor";
    }
}