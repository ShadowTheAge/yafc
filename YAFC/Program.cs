using YAFC.UI;

namespace YAFC
{
    public static class Program
    {        
        static void Main(string[] args)
        {
            YafcLib.Init();
            YafcLib.RegisterDefaultAnalysis();
            Ui.Start();
            Font.header = new Font(new FontFile("Data/Roboto-Light.ttf"), 2f);
            var regular = new FontFile("Data/Roboto-Regular.ttf");
            Font.subheader = new Font(regular, 1.5f);
            Font.text = new Font(regular, 1f);
            var window = new WelcomeScreen();
            Ui.MainLoop();
        }
    }
}