using UI;

namespace FactorioCalc
{
    internal static class Program
    {        
        static void Main(string[] args)
        {
            Font.header = new Font("data/Roboto-Light.ttf", 1.5f);
            Font.text = new Font("data/Roboto-Regular.ttf", 1f);
            var window = new Window(new RootUiPanel());
            while (!Ui.quit)
            {
                Ui.ProcessEvents();
                Ui.Render();
            }
        }
    }
}