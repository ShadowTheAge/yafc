using UI;

namespace FactorioCalc
{
    internal static class Program
    {        
        static void Main(string[] args)
        {
            using (var ui = new Ui(new RootUiPanel()))
            {
                while (!ui.quit)
                {
                    ui.ProcessEvents();
                    ui.Render();
                }
            }
        }
    }
}