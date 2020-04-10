using System.Drawing;
using UI;

namespace FactorioCalc
{
    public class RootUiPanel : IPanel
    {
        private WelcomeScreen welcomeScreen = new WelcomeScreen();

        public LayoutPosition BuildPanel(RenderBatch batch, LayoutPosition location)
        {
            location.Build(welcomeScreen, batch);
            return location;
        }
    }
}