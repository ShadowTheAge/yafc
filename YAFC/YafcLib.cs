using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using SDL2;
using YAFC.Model;
using YAFC.Parser;
using YAFC.UI;

namespace YAFC {
    public static class YafcLib {
        public static Version version { get; private set; }
        public static string initialWorkDir;

        public static void Init() {
            initialWorkDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            version = new Version(v.Major, v.Minor, v.Build, v.Revision);
            Project.currentYafcVersion = version;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                NativeLibrary.SetDllImportResolver(typeof(SDL).Assembly, DllResolver);
                NativeLibrary.SetDllImportResolver(typeof(Ui).Assembly, DllResolver);
                NativeLibrary.SetDllImportResolver(typeof(FactorioDataSource).Assembly, DllResolver);
            }
        }

        private static string GetLinuxMappedLibraryName(string libraryname) {
            switch (libraryname) {
                case "lua52": return "liblua52.so";
                case "SDL2.dll": return "SDL2-2.0.so.0";
                case "SDL2_ttf.dll": return "SDL2_ttf-2.0.so.0";
                case "SDL2_image.dll": return "SDL2_image-2.0.so.0";
                default: return libraryname;
            }
        }

        private static string GetOsxMappedLibraryName(string libraryname) {
            switch (libraryname) {
                case "lua52": return "liblua52.dylib";
                case "SDL2.dll": return "libSDL2.dylib";
                case "SDL2_ttf.dll": return "libSDL2_ttf.dylib";
                case "SDL2_image.dll": return "libSDL2_image.dylib";
                default: return libraryname;
            }
        }

        private static IntPtr DllResolver(string libraryname, Assembly assembly, DllImportSearchPath? searchpath) {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                libraryname = GetLinuxMappedLibraryName(libraryname);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                libraryname = GetOsxMappedLibraryName(libraryname);
            return NativeLibrary.Load(libraryname, assembly, DllImportSearchPath.SafeDirectories);
        }

        public static void RegisterDefaultAnalysis() {
            Analysis.RegisterAnalysis(Milestones.Instance);
            Analysis.RegisterAnalysis(AutomationAnalysis.Instance);
            Analysis.RegisterAnalysis(TechnologyScienceAnalysis.Instance);
            Analysis.RegisterAnalysis(CostAnalysis.Instance);
            Analysis.RegisterAnalysis(CostAnalysis.InstanceAtMilestones);
        }
    }
}
