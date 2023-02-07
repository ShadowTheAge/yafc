using System;
using System.IO;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public static class Program
    {
        public static bool hasOverriddenFont;
        static void Main(string[] args)
        {
            YafcLib.Init();
            YafcLib.RegisterDefaultAnalysis();
            Ui.Start();
            var overrideFont = Preferences.Instance.overrideFont;
            FontFile overriddenFontFile = null;
            try
            {
                if (!string.IsNullOrEmpty(overrideFont) && File.Exists(overrideFont))
                    overriddenFontFile = new FontFile(overrideFont);
            }
            catch (Exception ex)
            {
                Console.Error.WriteException(ex);
            }
            hasOverriddenFont = overriddenFontFile != null;
            Font.header = new Font(overriddenFontFile ?? new FontFile("Data/Roboto-Light.ttf"), 2f);
            var regular = overriddenFontFile ?? new FontFile("Data/Roboto-Regular.ttf");
            Font.subheader = new Font(regular, 1.5f);
            Font.text = new Font(regular, 1f);
            var window = new WelcomeScreen(args.Length > 0 ? args[0] : null);
            Ui.MainLoop();
        }
    }
}