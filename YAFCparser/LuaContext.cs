using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace YAFC.Parser
{
    public class LuaContext : IDisposable
    {
        private enum Result
        {
            LUA_OK = 0,
            LUA_YIELD = 1,
            LUA_ERRRUN = 2,
            LUA_ERRSYNTAX = 3,
            LUA_ERRMEM = 4,
            LUA_ERRGCMM = 5,
            LUA_ERRERR = 6
        }
        
        private enum Type
        {
            LUA_TNONE = -1,
            LUA_TNIL = 0,
            LUA_TBOOLEAN = 1,
            LUA_TLIGHTUSERDATA = 2,
            LUA_TNUMBER = 3,
            LUA_TSTRING = 4,
            LUA_TTABLE = 5,
            LUA_TFUNCTION = 6,
            LUA_TUSERDATA = 7,
            LUA_TTHREAD = 8,
        }


        private const int LUA_REFNIL = -1;
        private const int REGISTRY = -1001000;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int LuaCFunction(IntPtr lua);
        private const string LUA = "lua52";
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr luaL_newstate();
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr luaL_openlibs(IntPtr state);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_close(IntPtr state);
        
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern Result luaL_loadbufferx(IntPtr state, byte[] buf, IntPtr sz, string name, string mode);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern Result lua_pcallk(IntPtr state, int nargs, int nresults, int msgh, int ctx, IntPtr k);
        
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern Type lua_type(IntPtr state, int idx);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr lua_tolstring(IntPtr state, int idx, out IntPtr len);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern int lua_toboolean(IntPtr state, int idx);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern double lua_tonumberx(IntPtr state, int idx, out bool isnum);

        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern int lua_getglobal(IntPtr state, string var);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_setglobal(IntPtr state, string name);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern int luaL_ref(IntPtr state, int t);
        
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_pushnil(IntPtr state);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr lua_pushstring(IntPtr state, string s);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_pushnumber(IntPtr state, double d);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_pushboolean(IntPtr state, int b);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_pushcclosure(IntPtr state, IntPtr callback, int n);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_createtable(IntPtr state, int narr, int nrec);
        
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_rawgeti(IntPtr state, int idx, long n);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_rawget(IntPtr state, int idx);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_rawset(IntPtr state, int idx);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_len(IntPtr state, int index);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern int lua_next (IntPtr state, int idx);
        
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern int lua_gettop(IntPtr state);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_settop(IntPtr state, int idx);
        
        
        private IntPtr L;

        public LuaContext(object modSettings)
        {
            L = luaL_newstate();
            luaL_openlibs(L);
            RegisterApi(Log, "log");
            RegisterApi(Require, "require");
            var mods = NewTable();
            foreach (var mod in FactorioDataSource.allMods)
                mods[mod.Key] = mod.Value.version;
            SetGlobal("mods", mods);
        }

        private int Log(IntPtr lua)
        {
            Console.WriteLine(GetString(1));
            return 0;
        }
        private void GetReg(int refId) => lua_rawgeti(L, REGISTRY, refId);
        private void Pop(int popc) => lua_settop(L, lua_gettop(L) - popc);

        public object[] ArrayElements(int refId)
        {
            GetReg(refId); // +1
            lua_len(L, -1); // +2
            var count = (int) lua_tonumberx(L, -1, out _);
            var result = new object[count];
            for (var i = 0; i < count; i++)
            {
                lua_rawgeti(L, -2, i+1);
                result[i] = PopManagedValue(1);
            }
            Pop(2);
            return result;
        }

        public Dictionary<object, object> ObjectElements(int refId)
        {    
            GetReg(refId); // 1
            lua_pushnil(L);
            var top = lua_gettop(L);
            var dict = new Dictionary<object, object>();
            while (lua_next(L, -2) != 0)
            {
                var value = PopManagedValue(1);
                var key = PopManagedValue(0);
                if (key != null)
                    dict[key] = value;
            }
            Pop(1);
            return dict;
        }

        public LuaTable NewTable()
        {
            lua_createtable(L, 0, 0);
            return new LuaTable(this, luaL_ref(L, REGISTRY));
        }

        public object GetGlobal(string name)
        {
            lua_getglobal(L, name); // 1
            return PopManagedValue(1);
        }

        public void SetGlobal(string name, object value)
        {
            PushManagedObject(value);
            lua_setglobal(L, name);
        }
        public object GetValue(int refId, int idx)
        {
            GetReg(refId); // 1
            lua_rawgeti(L, -1, idx); // 2
            return PopManagedValue(2);
        }
        
        public object GetValue(int refId, string idx)
        {
            GetReg(refId); // 1
            lua_pushstring(L, idx); // 2
            lua_rawget(L, -2); // 3
            return PopManagedValue(3);
        }
        
        private object PopManagedValue(int popc)
        {
            object result = null;
            switch (lua_type(L, -1))
            {
                case Type.LUA_TBOOLEAN:
                    result = lua_toboolean(L, -1) != 0;
                    break;
                case Type.LUA_TNUMBER:
                    result = lua_tonumberx(L, -1, out _);
                    break;
                case Type.LUA_TSTRING:
                    result = GetString(-1);
                    break; 
                case Type.LUA_TTABLE:
                    var refId = luaL_ref(L, REGISTRY);
                    var table = new LuaTable(this, refId);
                    if (popc == 0)
                        GetReg(table.refId);
                    else popc--;
                    result = table;
                    break;
            }
            if (popc > 0)
                Pop(popc);
            return result;
        }

        private void PushManagedObject(object value)
        {
            if (value is double d)
                lua_pushnumber(L, d);
            else if (value is int i)
                lua_pushnumber(L, i);
            else if (value is string s)
                lua_pushstring(L, s);
            else if (value is LuaTable t)
                GetReg(t.refId);
            else if (value is bool b)
                lua_pushboolean(L, b ? 1 : 0);
            else lua_pushnil(L);
        }
        
        public void SetValue(int refId, string idx, object value)
        {
            GetReg(refId); // 1;
            lua_pushstring(L, idx); // 2
            PushManagedObject(value); // 3;
            lua_rawset(L, -3);
            Pop(3);
        }

        private int Require(IntPtr lua)
        {
            var file = GetString(1); // 1
            Pop(1);
            var key = FactorioDataSource.ResolveModPath(currentMod, file, true);
            if (required.TryGetValue(key, out var value))
            {
                GetReg(value);
                return 1;
            }
            required[key] = LUA_REFNIL;
            var path = key.path + ".lua";
            //FactorioDataSource.FindModFile()
            var bytes = FactorioDataSource.ReadModFile(key.mod, path) ?? FactorioDataSource.ReadModFile("core", "lualib/" + path);
            if (bytes != null)
            {
                var result = Exec(bytes, bytes.Length, key.mod + " - " + path);
                required[key] = result;
                GetReg(result);
            }
            else lua_pushnil(L);
            return 1;
        }

        private void RegisterApi(LuaCFunction callback, string name)
        {
            lua_pushcclosure(L, Marshal.GetFunctionPointerForDelegate(callback), 0);
            lua_setglobal(L, name);
            Pop(1);
        }
        private byte[] GetData(int index)
        {
            var ptr = lua_tolstring(L, index, out var len);
            var buf = new byte[(int)len];
            Marshal.Copy(ptr, buf, 0, buf.Length);
            return buf;
        }

        private string GetString(int index) => Encoding.UTF8.GetString(GetData(index));

        public int Exec(byte[] code, int length, string name)
        {
            var result = luaL_loadbufferx(L, code, (IntPtr) length, name, null); 
            if (result != Result.LUA_OK)
                throw new IOException("Loading terminated with code "+code);
            result = lua_pcallk(L, 0, 1, 0, 0, IntPtr.Zero);
            if (result != Result.LUA_OK)
            {
                if (result == Result.LUA_ERRRUN)
                    throw new IOException(GetString(-1));
                throw new IOException("Execution terminated with code "+code);
            }
            return luaL_ref(L, REGISTRY);
        }

        public void Dispose()
        {
            lua_close(L);
            L = IntPtr.Zero;
        }
        
        private Dictionary<(string mod, string filename), int> required = new Dictionary<(string mod, string filename), int>();
        private string currentMod;

        public void DoModFiles(string[] modorder, string fileName, IProgress<(string, string)> progress)
        {
            var header = "Executing mods " + fileName;
            foreach (var mod in modorder)
            {
                progress.Report((header, mod));
                var bytes = FactorioDataSource.ReadModFile(mod, fileName);
                if (bytes == null)
                    continue;
                Console.WriteLine("Executing " + mod + "/" + fileName);
                currentMod = mod;
                Exec(bytes, bytes.Length, mod + ":" + fileName);
            }
        }
        
        public LuaTable data => GetGlobal("data") as LuaTable;
    }

    public class LuaTable
    {
        public readonly LuaContext context;
        public readonly int refId;

        internal LuaTable(LuaContext context, int refId)
        {
            this.context = context;
            this.refId = refId;
        }
        public object this[int index] => context.GetValue(refId, index);
        public object this[string index]
        {
            get => context.GetValue(refId, index);
            set => context.SetValue(refId, index, value);
        }

        public object[] ArrayElements => context.ArrayElements(refId);
        public Dictionary<object, object> ObjectElements => context.ObjectElements(refId).ToDictionary(x => x.Key, x => x.Value);
    }
}