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

        public override void Build(ImGui gui)
        {
            gui.BuildText("Welcome to YAFC", Font.header, align:RectAlignment.Middle);
        }
    }
}