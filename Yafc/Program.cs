using System;
using System.IO;
using Yafc.Model;
using Yafc.UI;

namespace Yafc {
    public static class Program {
        public static bool hasOverriddenFont;

        private static void Main(string[] args) {
            YafcLib.Init();
            YafcLib.RegisterDefaultAnalysis();
            Ui.Start();
            string overrideFont = Preferences.Instance.overrideFont;
            FontFile overriddenFontFile = null;
            try {
                if (!string.IsNullOrEmpty(overrideFont) && File.Exists(overrideFont)) {
                    overriddenFontFile = new FontFile(overrideFont);
                }
            }
            catch (Exception ex) {
                Console.Error.WriteException(ex);
            }
            hasOverriddenFont = overriddenFontFile != null;
            Font.header = new Font(overriddenFontFile ?? new FontFile("Data/Roboto-Light.ttf"), 2f);
            var regular = overriddenFontFile ?? new FontFile("Data/Roboto-Regular.ttf");
            Font.subheader = new Font(regular, 1.5f);
            Font.productionTableHeader = new Font(regular, 1.23f);
            Font.text = new Font(regular, 1f);

            ProjectDefinition cliProject = CommandLineParser.Parse(args);

            if (CommandLineParser.errorOccured || CommandLineParser.helpRequested) {
                Console.WriteLine("YAFC CE v" + YafcLib.version.ToString(3));
                Console.WriteLine();

                if (CommandLineParser.errorOccured) {
                    Console.WriteLine($"Error: {CommandLineParser.lastError}");
                    Console.WriteLine();
                    Environment.ExitCode = 1;
                }

                CommandLineParser.PrintHelp();
            }
            else {
                _ = new WelcomeScreen(cliProject);
                Ui.MainLoop();
            }
        }
    }
}
