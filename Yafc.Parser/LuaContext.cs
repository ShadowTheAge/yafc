using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Yafc.Model;

namespace Yafc.Parser {
    public class LuaException(string luaMessage) : Exception(luaMessage) {
    }
    internal partial class LuaContext : IDisposable {
        private enum Result {
            LUA_OK = 0,
            LUA_YIELD = 1,
            LUA_ERRRUN = 2,
            LUA_ERRSYNTAX = 3,
            LUA_ERRMEM = 4,
            LUA_ERRGCMM = 5,
            LUA_ERRERR = 6
        }

        private enum Type {
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

        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial IntPtr luaL_newstate();
        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial IntPtr luaL_openlibs(IntPtr state);
        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial void lua_close(IntPtr state);

        [LibraryImport(LUA, StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial Result luaL_loadbufferx(IntPtr state, in byte buf, IntPtr sz, string name, string mode);
        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial Result lua_pcallk(IntPtr state, int nargs, int nresults, int msgh, IntPtr ctx, IntPtr k);
        [LibraryImport(LUA, StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial void luaL_traceback(IntPtr state, IntPtr state2, string msg, int level);

        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial Type lua_type(IntPtr state, int idx);
        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial IntPtr lua_tolstring(IntPtr state, int idx, out IntPtr len);
        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial int lua_toboolean(IntPtr state, int idx);
        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial double lua_tonumberx(IntPtr state, int idx, [MarshalAs(UnmanagedType.Bool)] out bool isnum);

        [LibraryImport(LUA, StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial int lua_getglobal(IntPtr state, string var);
        [LibraryImport(LUA, StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial void lua_setglobal(IntPtr state, string name);
        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial int luaL_ref(IntPtr state, int t);
        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial int lua_checkstack(IntPtr state, int n);

        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial void lua_pushnil(IntPtr state);
        [LibraryImport(LUA, StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        [UnmanagedCallConv(CallConvs = new System.Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
        private static partial IntPtr lua_pushstring(IntPtr state, string s);
        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial void lua_pushnumber(IntPtr state, double d);
        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial void lua_pushboolean(IntPtr state, int b);
        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial void lua_pushcclosure(IntPtr state, IntPtr callback, int n);
        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial void lua_createtable(IntPtr state, int narr, int nrec);

        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial void lua_rawgeti(IntPtr state, int idx, long n);
        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial void lua_rawget(IntPtr state, int idx);
        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial void lua_rawset(IntPtr state, int idx);
        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial void lua_rawseti(IntPtr state, int idx, long n);
        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial void lua_len(IntPtr state, int index);
        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial int lua_next(IntPtr state, int idx);

        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial int lua_gettop(IntPtr state);
        [LibraryImport(LUA)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial void lua_settop(IntPtr state, int idx);


        private IntPtr L;
        private readonly int tracebackReg;
        private readonly List<(string mod, string name)> fullChunkNames = [];
        private readonly Dictionary<string, int> required = [];
        private readonly Dictionary<(string mod, string name), byte[]> modFixes = [];

        public LuaContext() {
            L = luaL_newstate();
            _ = luaL_openlibs(L);
            RegisterApi(Log, "raw_log");
            RegisterApi(Require, "require");
            _ = lua_pushstring(L, Project.currentYafcVersion.ToString());
            lua_setglobal(L, "yafc_version");
            var mods = NewTable();
            foreach (var mod in FactorioDataSource.allMods) {
                mods[mod.Key] = mod.Value.version;
            }

            SetGlobal("mods", mods);

            LuaCFunction traceback = CreateErrorTraceback;
            neverCollect.Add(traceback);
            lua_pushcclosure(L, Marshal.GetFunctionPointerForDelegate(traceback), 0);
            tracebackReg = luaL_ref(L, REGISTRY);

            foreach (string file in Directory.EnumerateFiles("Data/Mod-fixes/", "*.lua")) {
                string fileName = Path.GetFileName(file);
                string[] modAndFile = fileName.Split('.');
                string assemble = string.Join('/', modAndFile.Skip(1).SkipLast(1));
                modFixes[(modAndFile[0], assemble + ".lua")] = File.ReadAllBytes(file);
            }
        }

        private int ParseTracebackEntry(string s, out int endOfName) {
            endOfName = 0;
            if (s.StartsWith("[string \"", StringComparison.Ordinal)) {
                int endOfNum = s.IndexOf(' ', 9);
                endOfName = s.IndexOf("\"]:", 9, StringComparison.Ordinal) + 2;
                if (endOfNum >= 0 && endOfName >= 0) {
                    return int.Parse(s[9..endOfNum]);
                }
            }

            return -1;
        }

        private int CreateErrorTraceback(IntPtr lua) {
            string message = GetString(1);
            luaL_traceback(L, L, message, 0);
            string actualTraceback = GetString(-1);
            string[] split = actualTraceback.Split("\n\t").ToArray();
            for (int i = 0; i < split.Length; i++) {
                int chunkId = ParseTracebackEntry(split[i], out int endOfName);
                if (chunkId >= 0) {
                    split[i] = fullChunkNames[chunkId] + split[i][endOfName..];
                }
            }

            string reassemble = string.Join("\n", split);
            _ = lua_pushstring(L, reassemble);
            return 1;
        }

        private int Log(IntPtr lua) {
            Console.WriteLine(GetString(1));
            return 0;
        }
        private void GetReg(int refId) {
            lua_rawgeti(L, REGISTRY, refId);
        }

        private void Pop(int popc) {
            lua_settop(L, lua_gettop(L) - popc);
        }

        public List<object> ArrayElements(int refId) {
            GetReg(refId); // 1
            lua_pushnil(L);
            List<object> list = [];
            while (lua_next(L, -2) != 0) {
                object value = PopManagedValue(1);
                object key = PopManagedValue(0);
                if (key is double) {
                    list.Add(value);
                }
                else {
                    break;
                }
            }
            Pop(1);
            return list;
        }

        public Dictionary<object, object> ObjectElements(int refId) {
            GetReg(refId); // 1
            lua_pushnil(L);
            Dictionary<object, object> dict = [];
            while (lua_next(L, -2) != 0) {
                object value = PopManagedValue(1);
                object key = PopManagedValue(0);
                if (key != null) {
                    dict[key] = value;
                }
            }
            Pop(1);
            return dict;
        }

        public LuaTable NewTable() {
            lua_createtable(L, 0, 0);
            return new LuaTable(this, luaL_ref(L, REGISTRY));
        }

        public object GetGlobal(string name) {
            _ = lua_getglobal(L, name); // 1
            return PopManagedValue(1);
        }

        public void SetGlobal(string name, object value) {
            PushManagedObject(value);
            lua_setglobal(L, name);
        }
        public object GetValue(int refId, int idx) {
            GetReg(refId); // 1
            lua_rawgeti(L, -1, idx); // 2
            return PopManagedValue(2);
        }

        public object GetValue(int refId, string idx) {
            GetReg(refId); // 1
            _ = lua_pushstring(L, idx); // 2
            lua_rawget(L, -2); // 3
            return PopManagedValue(3);
        }

        private object PopManagedValue(int popc) {
            object result = null;
            switch (lua_type(L, -1)) {
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
                    int refId = luaL_ref(L, REGISTRY);
                    LuaTable table = new LuaTable(this, refId);
                    if (popc == 0) {
                        GetReg(table.refId);
                    }
                    else {
                        popc--;
                    }

                    result = table;
                    break;
            }
            if (popc > 0) {
                Pop(popc);
            }

            return result;
        }

        private void PushManagedObject(object value) {
            if (value is double d) {
                lua_pushnumber(L, d);
            }
            else if (value is int i) {
                lua_pushnumber(L, i);
            }
            else if (value is string s) {
                _ = lua_pushstring(L, s);
            }
            else if (value is LuaTable t) {
                GetReg(t.refId);
            }
            else if (value is bool b) {
                lua_pushboolean(L, b ? 1 : 0);
            }
            else {
                lua_pushnil(L);
            }
        }

        public void SetValue(int refId, string idx, object value) {
            GetReg(refId); // 1;
            _ = lua_pushstring(L, idx); // 2
            PushManagedObject(value); // 3;
            lua_rawset(L, -3);
            Pop(3);
        }

        public void SetValue(int refId, int idx, object value) {
            GetReg(refId); // 1;
            PushManagedObject(value); // 2;
            lua_rawseti(L, -2, idx);
            Pop(2);
        }

        private string GetDirectoryName(string s) {
            int lastSlash = s.LastIndexOf('/');
            return lastSlash >= 0 ? s[..(lastSlash + 1)] : "";
        }

        private int Require(IntPtr lua) {
            string file = GetString(1); // 1
            string argument = file;
            if (file.Contains("..")) {
                throw new NotSupportedException("Attempt to traverse to parent directory");
            }

            if (file.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)) {
                file = file[..^4];
            }

            file = file.Replace('\\', '/');
            string origFile = file;
            file = file.Replace('.', '/');
            string fileExt = file + ".lua";
            Pop(1);
            luaL_traceback(L, L, null, 1); //2
            // TODO how to determine where to start require search? Parsing lua traceback output for now
            string tracebackS = GetString(-1);
            string[] tracebackVal = tracebackS.Split("\n\t");
            int traceId = -1;
            foreach (string traceLine in tracebackVal) // TODO slightly hacky
            {
                traceId = ParseTracebackEntry(traceLine, out _);
                if (traceId >= 0) {
                    break;
                }
            }
            var (mod, source) = fullChunkNames[traceId];

            (string mod, string path) requiredFile = (mod, fileExt);
            if (file.StartsWith("__")) {
                requiredFile = FactorioDataSource.ResolveModPath(mod, origFile, true);
            }
            else if (mod == "*") {
                byte[] localFile = File.ReadAllBytes("Data/" + fileExt);
                int result = Exec(localFile, "*", file);
                GetReg(result);
                return 1;
            }
            else if (FactorioDataSource.ModPathExists(requiredFile.mod, fileExt)) { }
            else if (FactorioDataSource.ModPathExists(requiredFile.mod, GetDirectoryName(source) + fileExt)) {
                requiredFile.path = GetDirectoryName(source) + fileExt;
            }
            else if (FactorioDataSource.ModPathExists("core", "lualib/" + fileExt)) {
                requiredFile.mod = "core";
                requiredFile.path = "lualib/" + fileExt;
            }
            else { // Just find anything ffs
                foreach (string path in FactorioDataSource.GetAllModFiles(requiredFile.mod, GetDirectoryName(source))) {
                    if (path.EndsWith(fileExt, StringComparison.OrdinalIgnoreCase)) {
                        requiredFile.path = path;
                        break;
                    }
                }
            }

            if (required.TryGetValue(argument, out int value)) {
                GetReg(value);
                return 1;
            }
            required[argument] = LUA_REFNIL;
            Console.WriteLine("Require " + requiredFile.mod + "/" + requiredFile.path);
            byte[] bytes = FactorioDataSource.ReadModFile(requiredFile.mod, requiredFile.path);
            if (bytes != null) {
                _ = lua_pushstring(L, argument);
                int argumentReg = luaL_ref(L, REGISTRY);
                int result = Exec(bytes, requiredFile.mod, requiredFile.path, argumentReg);
                if (modFixes.TryGetValue(requiredFile, out byte[] fix)) {
                    string modFixName = "mod-fix-" + requiredFile.mod + "." + requiredFile.path;
                    Console.WriteLine("Running mod-fix " + modFixName);
                    result = Exec(fix, "*", modFixName, result);
                }
                required[argument] = result;
                GetReg(result);
            }
            else {
                Console.Error.WriteLine("LUA require failed: mod " + mod + " file " + file);
                lua_pushnil(L);
            }
            return 1;
        }

        protected readonly List<object> neverCollect = []; // references callbacks that could be called from native code to not be garbage collected
        private void RegisterApi(LuaCFunction callback, string name) {
            neverCollect.Add(callback);
            lua_pushcclosure(L, Marshal.GetFunctionPointerForDelegate(callback), 0);
            lua_setglobal(L, name);
        }
        private byte[] GetData(int index) {
            nint ptr = lua_tolstring(L, index, out nint len);
            byte[] buf = new byte[(int)len];
            Marshal.Copy(ptr, buf, 0, buf.Length);
            return buf;
        }

        private string GetString(int index) {
            return Encoding.UTF8.GetString(GetData(index));
        }

        public int Exec(ReadOnlySpan<byte> chunk, string mod, string name, int argument = 0) {
            // since lua cuts file name to a few dozen symbols, add index to start of every name
            fullChunkNames.Add((mod, name));
            name = fullChunkNames.Count - 1 + " " + name;
            GetReg(tracebackReg);
            chunk = chunk.CleanupBom();

            var result = luaL_loadbufferx(L, in chunk.GetPinnableReference(), chunk.Length, name, null);
            if (result != Result.LUA_OK) {
                throw new LuaException("Loading terminated with code " + result + "\n" + GetString(-1));
            }

            int argcount = 0;
            if (argument > 0) {
                GetReg(argument);
                argcount = 1;
            }
            result = lua_pcallk(L, argcount, 1, -2 - argcount, IntPtr.Zero, IntPtr.Zero);
            if (result != Result.LUA_OK) {
                if (result == Result.LUA_ERRRUN) {
                    throw new LuaException(GetString(-1));
                }

                throw new LuaException("Execution " + mod + "/" + name + " terminated with code " + result + "\n" + GetString(-1));
            }
            return luaL_ref(L, REGISTRY);
        }

        public void Dispose() {
            lua_close(L);
            L = IntPtr.Zero;
        }

        public void DoModFiles(string[] modorder, string fileName, IProgress<(string, string)> progress) {
            string header = "Executing mods " + fileName;
            foreach (string mod in modorder) {
                required.Clear();
                FactorioDataSource.currentLoadingMod = mod;
                progress.Report((header, mod));
                byte[] bytes = FactorioDataSource.ReadModFile(mod, fileName);
                if (bytes == null) {
                    continue;
                }

                Console.WriteLine("Executing " + mod + "/" + fileName);
                _ = Exec(bytes, mod, fileName);
            }
        }

        public LuaTable data => GetGlobal("data") as LuaTable;
        public LuaTable defines => GetGlobal("defines") as LuaTable;
    }

    internal class LuaTable {
        public readonly LuaContext context;
        public readonly int refId;

        internal LuaTable(LuaContext context, int refId) {
            this.context = context;
            this.refId = refId;
        }

        public object this[int index] {
            get => context.GetValue(refId, index);
            set => context.SetValue(refId, index, value);
        }
        public object this[string index] {
            get => context.GetValue(refId, index);
            set => context.SetValue(refId, index, value);
        }

        public List<object> ArrayElements => context.ArrayElements(refId);
        public Dictionary<object, object> ObjectElements => context.ObjectElements(refId);
    }
}
