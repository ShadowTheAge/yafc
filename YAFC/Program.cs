using System;
using System.IO;
using YAFC.Model;
using YAFC.UI;

namespace YAFC {
    public static class Program {
        public static bool hasOverriddenFont;
        static void Main(string[] args) {
            YafcLib.Init();
            YafcLib.RegisterDefaultAnalysis();
            Ui.Start();
            var overrideFont = Preferences.Instance.overrideFont;
            FontFile overriddenFontFile = null;
            try {
                if (!string.IsNullOrEmpty(overrideFont) && File.Exists(overrideFont))
                    overriddenFontFile = new FontFile(overrideFont);
            }
            catch (Exception ex) {
                Console.Error.WriteException(ex);
            }
            hasOverriddenFont = overriddenFontFile != null;
            Font.header = new Font(overriddenFontFile ?? new FontFile("Data/Roboto-Light.ttf"), 2f);
            var regular = overriddenFontFile ?? new FontFile("Data/Roboto-Regular.ttf");
            Font.subheader = new Font(regular, 1.5f);
            Font.text = new Font(regular, 1f);

            ProjectDefinition cliProject = null;

            if (args.Length > 0) {
                if (args.Length < 3) {
                    Console.WriteLine("Usage: YAFC <projectPath> <dataPath> <modsPath> [expensive]");
                    Console.WriteLine("<projectPath> - path to the project file");
                    Console.WriteLine("<dataPath>    - path to the data folder, e.g. C:\\Factorio\\data or /home/user/Factorio/data");
                    Console.WriteLine("<modsPath>    - path to the mods folder, e.g. C:\\Factorio\\mods or /home/user/Factorio/mods");
                    Console.WriteLine("expensive     - optional, if provided YAFC will use expensive recipes");
                    return;
                }

                cliProject = new ProjectDefinition {
                    path = args[0],
                    dataPath = args[1],
                    modsPath = args[2]
                };

                if (args.Length >= 4) {
                    cliProject.expensive = args[3] == "expensive";
                }

                if (!File.Exists(cliProject.path)) {
                    Console.WriteLine("Project file not found: " + cliProject.path);
                    return;
                }

                if (!Directory.Exists(cliProject.dataPath)) {
                    Console.WriteLine("Data folder not found: " + cliProject.dataPath);
                    return;
                }

                if (!Directory.Exists(cliProject.modsPath)) {
                    Console.WriteLine("Mods folder not found: " + cliProject.modsPath);
                    return;
                }
            }

            var window = new WelcomeScreen(cliProject);
            Ui.MainLoop();
        }
    }
}
