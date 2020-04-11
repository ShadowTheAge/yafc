using System.Drawing;
using UI;

namespace FactorioCalc
{
    public class WelcomeScreen : Panel, ICloseable
    {
        public override SchemeColor boxColor => SchemeColor.Background;
        private ScreenHeader header;
        private FontString text;
        private InputField input;

        public WelcomeScreen() : base(new SizeF(20f, 10f))
        {
            header = new ScreenHeader(this, "Screen header");
            text = new FontString(Font.text, true, "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.");
            input = new InputField(Font.text);
        }

        public override LayoutPosition BuildPanel(RenderBatch batch, LayoutPosition location)
        {
            location.Build(header, batch);
            location.Build(text, batch);
            location.Build(input, batch);
            return location;
        }

        public void Close()
        {
            
        }
    }
}