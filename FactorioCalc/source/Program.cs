using UI;

namespace FactorioCalc
{
    internal static class Program
    {        
        static void Main(string[] args)
        {
            Font.header = new Font("data/Roboto-Light.ttf", 2f);
            Font.text = new Font("data/Roboto-Regular.ttf", 1f);
            var window = new Window(new WelcomeScreen(), 50f);
            while (!Ui.quit)
            {
                Ui.ProcessEvents();
                Ui.Render();
            }
        }
    }
}