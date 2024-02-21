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

            if (args != null && args.Length >= 3) {
                cliProject = new ProjectDefinition {
                    path = args[0],
                    dataPath = args[1],
                    modsPath = args[2]
                };
                if (args.Length >= 4) {
                    cliProject.expensive = args[3] == "expensive";
                }
            }

            var window = new WelcomeScreen(cliProject);
            Ui.MainLoop();
        }
    }
}
