using System;
using System.Reflection;
using System.Runtime.InteropServices;
using SDL2;
using YAFC.Model;
using YAFC.Parser;
using YAFC.UI;

namespace YAFC
{
    internal static class Program
    {        
        static void Main(string[] args)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                NativeLibrary.SetDllImportResolver(typeof(SDL).Assembly, DllResolver);
                NativeLibrary.SetDllImportResolver(typeof(LuaContext).Assembly, DllResolver);
            }
            
            Ui.Start();
            Font.header = new Font(new FontFile("data/Roboto-Light.ttf"), 2f);
            var regular = new FontFile("data/Roboto-Regular.ttf");
            Font.subheader = new Font(regular, 1.5f);
            Font.text = new Font(regular, 1f);
            var window = new WelcomeScreen();
            Analysis.RegisterAnalysis(Milestones.Instance);
            Analysis.RegisterAnalysis(AutomationAnalysis.Instance);
            Analysis.RegisterAnalysis(CostAnalysis.Instance);
            Ui.MainLoop();
        }

        private static string GetLinuxMappedLibraryName(string libraryname)
        {
            switch (libraryname)
            {
                case "lua52" : return "liblua52.so";
                case "SDL2.dll": return "SDL2-2.0.so";
                case "SDL2_ttf.dll": return "SDL2_ttf-2.0.so.0";
                case "SDL2_image.dll": return "SDL2_image-2.0.so.0";
                default: return libraryname;
            }
        }
        
        private static string GetOsxMappedLibraryName(string libraryname)
        {
            switch (libraryname)
            {
                case "lua52" : return "liblua52.dylib";
                case "SDL2.dll": return "libSDL2.dylib";
                case "SDL2_ttf.dll": return "libSDL2_ttf.dylib";
                case "SDL2_image.dll": return "libSDL2_image.dylib";
                default: return libraryname;
            }
        }

        private static IntPtr DllResolver(string libraryname, Assembly assembly, DllImportSearchPath? searchpath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                libraryname = GetLinuxMappedLibraryName(libraryname);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                libraryname = GetOsxMappedLibraryName(libraryname);
            return NativeLibrary.Load(libraryname, assembly, DllImportSearchPath.SafeDirectories);
        }
    }
}