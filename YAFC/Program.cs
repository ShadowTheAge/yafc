using YAFC.UI;

namespace YAFC
{
    internal static class Program
    {        
        static void Main(string[] args)
        {
            Ui.Start();
            Font.header = new Font(new FontFile("data/Roboto-Light.ttf"), 2f);
            var regular = new FontFile("data/Roboto-Regular.ttf");
            Font.subheader = new Font(regular, 1.5f);
            Font.text = new Font(regular, 1f);
            var window = new TestScreen();
            Ui.MainLoop();
        }
    }
}