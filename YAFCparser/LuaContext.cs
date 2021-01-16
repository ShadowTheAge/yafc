using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using YAFC.Model;

namespace YAFC.Parser
{
    public class LuaException : Exception
    {
        public LuaException(string luaMessage) : base(luaMessage) {}
    }
    internal class LuaContext : IDisposable
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
        
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private static extern Result luaL_loadbufferx(IntPtr state, byte[] buf, IntPtr sz, string name, string mode);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern Result lua_pcallk(IntPtr state, int nargs, int nresults, int msgh, IntPtr ctx, IntPtr k);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private static extern void luaL_traceback(IntPtr state, IntPtr state2, string msg, int level);

        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern Type lua_type(IntPtr state, int idx);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr lua_tolstring(IntPtr state, int idx, out IntPtr len);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern int lua_toboolean(IntPtr state, int idx);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern double lua_tonumberx(IntPtr state, int idx, out bool isnum);

        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private static extern int lua_getglobal(IntPtr state, string var);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private static extern void lua_setglobal(IntPtr state, string name);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern int luaL_ref(IntPtr state, int t);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern int lua_checkstack(IntPtr state, int n);
        
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_pushnil(IntPtr state);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private static extern IntPtr lua_pushstring(IntPtr state, string s);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_pushnumber(IntPtr state, double d);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_pushboolean(IntPtr state, int b);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_pushcclosure(IntPtr state, IntPtr callback, int n);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_createtable(IntPtr state, int narr, int nrec);
        
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_rawgeti(IntPtr state, int idx, long n);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_rawget(IntPtr state, int idx);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_rawset(IntPtr state, int idx);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_rawseti(IntPtr state, int idx, long n);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_len(IntPtr state, int index);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern int lua_next (IntPtr state, int idx);
        
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern int lua_gettop(IntPtr state);
        [DllImport(LUA, CallingConvention = CallingConvention.Cdecl)] private static extern void lua_settop(IntPtr state, int idx);
        
        
        private IntPtr L;
        private readonly int tracebackReg;
        private readonly List<(string mod, string name)> fullChunkNames = new List<(string, string)>();
        private readonly Dictionary<(string mod, string filename), int> required = new Dictionary<(string mod, string filename), int>();
        private readonly Dictionary<(string mod, string name), byte[]> modFixes = new Dictionary<(string mod, string name), byte[]>();

        public LuaContext()
        {
            L = luaL_newstate();
            luaL_openlibs(L);
            RegisterApi(Log, "raw_log");
            RegisterApi(Require, "require");
            lua_pushstring(L, Project.currentYafcVersion.ToString());
            lua_setglobal(L, "yafc_version");
            var mods = NewTable();
            foreach (var mod in FactorioDataSource.allMods)
                mods[mod.Key] = mod.Value.version;
            SetGlobal("mods", mods);

            var traceback = (LuaCFunction) CreateErrorTraceback;
            neverCollect.Add(traceback);
            lua_pushcclosure(L, Marshal.GetFunctionPointerForDelegate(traceback), 0);
            tracebackReg = luaL_ref(L, REGISTRY);

            foreach (var file in Directory.EnumerateFiles("Data/Mod-fixes/", "*.lua"))
            {
                var fileName = Path.GetFileName(file);
                var modAndFile = fileName.Split('.');
                var assemble = string.Join('/', modAndFile.Skip(1).SkipLast(1));
                modFixes[(modAndFile[0], assemble + ".lua")] = File.ReadAllBytes(file);
            }
        }

        private int ParseTracebackEntry(string s, out int endOfName)
        {
            endOfName = 0;
            if (s.StartsWith("[string \"", StringComparison.Ordinal))
            {
                var endOfNum = s.IndexOf(" ", 9, StringComparison.Ordinal);
                endOfName = s.IndexOf("\"]:", 9, StringComparison.Ordinal) + 2;
                if (endOfNum >= 0 && endOfName >= 0)
                    return int.Parse(s.Substring(9, endOfNum - 9));
            }

            return -1;
        }

        private int CreateErrorTraceback(IntPtr lua)
        {
            var message = GetString(1);
            luaL_traceback(L, L, message, 0);
            var actualTraceback = GetString(-1);
            var split = actualTraceback.Split("\n\t").ToArray();
            for (var i = 0; i < split.Length; i++)
            {
                var chunkId = ParseTracebackEntry(split[i], out var endOfName);
                if (chunkId >= 0)
                    split[i] = fullChunkNames[chunkId] + split[i].Substring(endOfName);
            }

            var reassemble = string.Join("\n", split);
            lua_pushstring(L, reassemble);
            return 1;
        }

        private int Log(IntPtr lua)
        {
            Console.WriteLine(GetString(1));
            return 0;
        }
        private void GetReg(int refId) => lua_rawgeti(L, REGISTRY, refId);
        private void Pop(int popc) => lua_settop(L, lua_gettop(L) - popc);

        public List<object> ArrayElements(int refId)
        {
            GetReg(refId); // 1
            lua_pushnil(L);
            var list = new List<object>();
            while (lua_next(L, -2) != 0)
            {
                var value = PopManagedValue(1);
                var key = PopManagedValue(0);
                if (key is double)
                    list.Add(value);
                else break;
            }
            Pop(1);
            return list;
        }

        public Dictionary<object, object> ObjectElements(int refId)
        {    
            GetReg(refId); // 1
            lua_pushnil(L);
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
        
        public void SetValue(int refId, int idx, object value)
        {
            GetReg(refId); // 1;
            PushManagedObject(value); // 2;
            lua_rawseti(L, -2, idx);
            Pop(2);
        }

        private string GetDirectoryName(string s)
        {
            var lastSlash = s.LastIndexOf('/');
            return lastSlash >= 0 ? s.Substring(0, lastSlash + 1) : "";
        }

        private int Require(IntPtr lua)
        {
            var file = GetString(1); // 1
            if (file.Contains(".."))
                throw new NotSupportedException("Attempt to traverse to parent directory");
            if (file.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                file = file.Substring(0,file.Length - 4);
            file = file.Replace('.', '/');
            file = file.Replace('\\', '/');
            var fileExt = file + ".lua";
            Pop(1);
            luaL_traceback(L, L, null, 1); //2
            // TODO how to determine where to start require search? Parsing lua traceback output for now
            var tracebackS = GetString(-1);
            var tracebackVal = tracebackS.Split("\n\t");
            var traceId = -1;
            foreach (var traceLine in tracebackVal) // TODO slightly hacky
            {
                traceId = ParseTracebackEntry(traceLine, out _);
                if (traceId >= 0)
                    break;
            }
            var (mod, source) = fullChunkNames[traceId];

            (string mod, string path) requiredFile = (mod, fileExt);
            if (file.StartsWith("__"))
            {
                requiredFile = FactorioDataSource.ResolveModPath(mod, file, true);
            }
            else if (mod == "*")
            {
                var localFile = File.ReadAllBytes("Data/" + fileExt);
                var result = Exec(localFile, localFile.Length, "*", file);
                GetReg(result);
                return 1;
            }
            else if (FactorioDataSource.ModPathExists(requiredFile.mod, fileExt)) { }
            else if (FactorioDataSource.ModPathExists(requiredFile.mod, GetDirectoryName(source) + fileExt))
                requiredFile.path = GetDirectoryName(source) + fileExt;
            else if (FactorioDataSource.ModPathExists("core", "lualib/" + fileExt))
            {
                requiredFile.mod = "core";
                requiredFile.path = "lualib/" + fileExt;
            }
            else { // Just find anything ffs
                foreach (var path in FactorioDataSource.GetAllModFiles(requiredFile.mod, GetDirectoryName(source)))
                {
                    if (path.EndsWith(fileExt, StringComparison.OrdinalIgnoreCase))
                    {
                        requiredFile.path = path;
                        break;
                    }
                }
            }
            
            if (required.TryGetValue(requiredFile, out var value))
            {
                GetReg(value);
                return 1;
            }
            required[requiredFile] = LUA_REFNIL;
            Console.WriteLine("Require "+requiredFile.mod +"/"+requiredFile.path);
            var bytes = FactorioDataSource.ReadModFile(requiredFile.mod, requiredFile.path);
            if (bytes != null)
            {
                var result = Exec(bytes, bytes.Length, requiredFile.mod, requiredFile.path);
                if (modFixes.TryGetValue(requiredFile, out var fix))
                {
                    var modFixName = "mod-fix-" + requiredFile.mod + "." + requiredFile.path;
                    Console.WriteLine("Running mod-fix "+modFixName);
                    result = Exec(fix, fix.Length, "*", modFixName, result);
                }
                required[requiredFile] = result;
                GetReg(result);
            }
            else
            {
                Console.Error.WriteLine("LUA require failed: mod "+mod+" file "+file);
                lua_pushnil(L);
            }
            return 1;
        }

        protected readonly List<object> neverCollect = new List<object>(); // references callbacks that could be called from native code to not be garbage collected
        private void RegisterApi(LuaCFunction callback, string name)
        {
            neverCollect.Add(callback);
            lua_pushcclosure(L, Marshal.GetFunctionPointerForDelegate(callback), 0);
            lua_setglobal(L, name);
        }
        private byte[] GetData(int index)
        {
            var ptr = lua_tolstring(L, index, out var len);
            var buf = new byte[(int)len];
            Marshal.Copy(ptr, buf, 0, buf.Length);
            return buf;
        }

        private string GetString(int index) => Encoding.UTF8.GetString(GetData(index));

        public int Exec(byte[] chunk, int length, string mod, string name, int argument = 0)
        {
            // since lua cuts file name to a few dozen symbols, add index to start of every name
            fullChunkNames.Add((mod, name));
            name = (fullChunkNames.Count - 1) + " " + name;
            GetReg(tracebackReg);
            
            // Remove the byte-order mark (replace with spaces)
            if (chunk.Length >= 3 && chunk[0] == 0xEF)
            {
                chunk[0] = 0x20;
                if (chunk[1] == 0xBB) chunk[1] = 0x20;
                if (chunk[2] == 0xBF) chunk[2] = 0x20;
            }
            
            var result = luaL_loadbufferx(L, chunk, (IntPtr) length, name, null);
            if (result != Result.LUA_OK)
            {
                throw new LuaException("Loading terminated with code "+result + "\n"+GetString(-1));
            }

            var argcount = 0;
            if (argument > 0)
            {
                GetReg(argument);
                argcount = 1;
            }
            result = lua_pcallk(L, argcount, 1, -2-argcount, IntPtr.Zero, IntPtr.Zero);
            if (result != Result.LUA_OK)
            {
                if (result == Result.LUA_ERRRUN)
                    throw new LuaException(GetString(-1));
                throw new LuaException("Execution "+mod + "/" + name+" terminated with code "+result + "\n"+GetString(-1));
            }
            return luaL_ref(L, REGISTRY);
        }

        public void Dispose()
        {
            lua_close(L);
            L = IntPtr.Zero;
        }

        public void DoModFiles(string[] modorder, string fileName, IProgress<(string, string)> progress)
        {
            var header = "Executing mods " + fileName;
            foreach (var mod in modorder)
            {
                required.Clear();
                FactorioDataSource.currentLoadingMod = mod;
                progress.Report((header, mod));
                var bytes = FactorioDataSource.ReadModFile(mod, fileName);
                if (bytes == null)
                    continue;
                Console.WriteLine("Executing " + mod + "/" + fileName);
                Exec(bytes, bytes.Length, mod, fileName);
            }
        }
        
        public LuaTable data => GetGlobal("data") as LuaTable;
    }

    internal class LuaTable
    {
        public readonly LuaContext context;
        public readonly int refId;

        internal LuaTable(LuaContext context, int refId)
        {
            this.context = context;
            this.refId = refId;
        }

        public object this[int index]
        {
            get => context.GetValue(refId, index);
            set => context.SetValue(refId, index, value);
        }
        public object this[string index]
        {
            get => context.GetValue(refId, index);
            set => context.SetValue(refId, index, value);
        }

        public List<object> ArrayElements => context.ArrayElements(refId);
        public Dictionary<object, object> ObjectElements => context.ObjectElements(refId);
    }
}