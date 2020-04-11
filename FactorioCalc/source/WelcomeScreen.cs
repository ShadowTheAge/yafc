using UI;

namespace FactorioCalc
{
    public class WelcomeScreen : WidgetContainer
    {
        public override SchemeColor boxColor => SchemeColor.Background;

        private FontString header;
        private FontString text;
        private InputField input;
        
        protected override LayoutPosition BuildContent(RenderBatch batch, LayoutPosition location)
        {
            location.Build(header, batch, 2f);
            location.Build(text, batch);
            location.Build(input, batch);
            return location;
        }

        public WelcomeScreen()
        {
            header = new FontString(Font.header, "Yet Another Factorio Calculator", align:Alignment.Center);
            text = new FontString(Font.text, "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.", true);
            input = new InputField(Font.text) {placeholder = "Input something here"};
            padding = new Padding(5f, 2f);
        }
    }
}