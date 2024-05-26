using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using SDL2;
using Yafc.Model;
using Yafc.Parser;
using Yafc.UI;

namespace Yafc {
    public static class YafcLib {
        public static Version version { get; private set; }
        public static string initialWorkDir;

        static YafcLib() {
            initialWorkDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            var v = Assembly.GetExecutingAssembly().GetName().Version!;
            version = new Version(v.Major, v.Minor, v.Build, v.Revision);
            Project.currentYafcVersion = version;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                NativeLibrary.SetDllImportResolver(typeof(SDL).Assembly, DllResolver);
                NativeLibrary.SetDllImportResolver(typeof(Ui).Assembly, DllResolver);
                NativeLibrary.SetDllImportResolver(typeof(FactorioDataSource).Assembly, DllResolver);
            }
        }

        private static string GetLinuxMappedLibraryName(string libraryname) => libraryname switch {
            "lua52" => "liblua52.so",
            "SDL2.dll" => "SDL2-2.0.so.0",
            "SDL2_ttf.dll" => "SDL2_ttf-2.0.so.0",
            "SDL2_image.dll" => "SDL2_image-2.0.so.0",
            _ => libraryname,
        };

        private static string GetOsxMappedLibraryName(string libraryname) => libraryname switch {
            "lua52" => "liblua52.dylib",
            "SDL2.dll" => "libSDL2.dylib",
            "SDL2_ttf.dll" => "libSDL2_ttf.dylib",
            "SDL2_image.dll" => "libSDL2_image.dylib",
            _ => libraryname,
        };

        private static IntPtr DllResolver(string libraryname, Assembly assembly, DllImportSearchPath? searchpath) {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                libraryname = GetLinuxMappedLibraryName(libraryname);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                libraryname = GetOsxMappedLibraryName(libraryname);
            }

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
