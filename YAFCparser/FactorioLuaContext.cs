using System;
using System.Collections.Generic;
using NLua;
using NLua.Exceptions;

namespace YAFC.Parser
{
    public  class FactorioLuaContext : IDisposable
    {
        private Dictionary<(string mod, string filename), object> required = new Dictionary<(string mod, string filename), object>();
        private string currentMod;
        private Lua lua;

        public LuaTable CreateEmptyTable()
        {
            lua.NewTable("__empty");
            return lua.GetTable("__empty");
        }

        public FactorioLuaContext(object settings = null)
        {
            lua = new Lua();
            lua["require"] = (Func<string, object>) Require;
            lua["log"] = (Action<object>) Console.WriteLine;
            lua.NewTable("mods");
            var mods = lua.GetTable("mods");
            foreach (var mod in FactorioDataSource.allMods)
                mods[mod.Key] = mod.Value.version;
            lua["settings"] = settings;
        }

        public void Run(string luaCode)
        {
            lua.DoString(luaCode);
        }

        public void DoModFiles(string[] modorder, string fileName)
        {
            try
            {   
                foreach (var mod in modorder)
                {
                    var bytes = FactorioDataSource.ReadModFile(mod, fileName);
                    if (bytes == null)
                        continue;
                    Console.WriteLine("Executing " + mod + "/" + fileName);
                    currentMod = mod;
                    lua.DoString(bytes, mod + ":" + fileName);
                }
            }
            catch (LuaException ex)
            {
                Console.Error.WriteLine(ex.Message);
                throw;
            }
        }

        public LuaTable data => lua.GetTable("data");

        private object Require(string file)
        {
            var key = FactorioDataSource.ResolveModPath(currentMod, file, true);
            if (required.TryGetValue(key, out var value))
                return value;
            required[key] = default;
            var path = key.path + ".lua";
            var bytes = FactorioDataSource.ReadModFile(key.mod, path) ?? FactorioDataSource.ReadModFile("core", "lualib/" + path);
            if (bytes != null)
            {
                var result = lua.DoString(bytes, key.mod + " - " + path)?[0];
                return required[key] = result;
            }

            return null;
        }

        public void Dispose()
        {
            lua?.Dispose();
        }
    }
}