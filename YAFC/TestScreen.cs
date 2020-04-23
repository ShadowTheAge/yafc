using System.Numerics;
using YAFC.UI;

namespace YAFC
{
    public class TestScreen : WindowUtility
    {
        protected override void BuildContent(LayoutState state)
        {
            
        }

        public TestScreen()
        {
            base.Create("Welcome to YAFC", 40f, null);
        }

        private int numClicked;
        private bool checkbox;

        public override void Build(ImGui gui)
        {
            using (gui.EnterGroup(ImGuiComponents.DefaultScreenPadding))
            {
                gui.BuildText("Welcome to YAFC", Font.header, align:RectAlignment.Middle);
                if (gui.BuildButton("My button"))
                {
                    numClicked++;
                }

                if (gui.BuildCheckBox("Check me!", checkbox, out checkbox))
                {
                    numClicked += 2;
                } 
                gui.BuildText("Clicked "+numClicked+" times");
            }
        }
    }
}