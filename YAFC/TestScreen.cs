using System.Numerics;
using System.Reflection;
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
        private int numRebuilded;
        private bool checkbox;
        private string editing;
        private string editing2;

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

                if (gui.BuildTextInput(editing, out editing, "Edit something"))
                {
                    numClicked += 1;
                }
                
                if (gui.BuildTextInput(editing2, out editing2, "Edit something"))
                {
                    numClicked += 1;
                }
                gui.BuildText("Clicked "+numClicked+" times");
                if (gui.action == ImGuiAction.Build)
                    numRebuilded++;
                gui.BuildText("Rebuilded "+numRebuilded+" times");
                gui.BuildText("Next rebuild time = "+typeof(TestScreen).GetField("nextRepaintTime", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this));
            }
        }
    }
}