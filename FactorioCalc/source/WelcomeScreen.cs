using System.Collections.Generic;
using System.Drawing;
using UI;

namespace FactorioCalc
{
    public class WelcomeScreen : Window
    {
        public override SchemeColor boxColor => SchemeColor.Background;

        private FontString header;
        private FontString text;
        private InputField input;
        private ScrollListTest scrollArea;
        private VirtualScrollList<string, FontString> scrollList;
        private TextButton button;

        private void ClickMeClick()
        {
            new FilesystemPanel("Select something", "Please select something", "Okay", null, true, "txt", "wow");
        }

        protected override LayoutPosition BuildContent(RenderBatch batch, LayoutPosition location)
        {
            location.Build(header, batch, 2f);
            location.Build(text, batch);
            location.Build(input, batch);
            location.Build(scrollArea, batch);
            location.Build(scrollList, batch);
            location.Build(button, batch);
            return location;
        }

        public WelcomeScreen()
        {
            header = new FontString(Font.header, "Yet Another Factorio Calculator", align:Alignment.Center);
            text = new FontString(Font.text, "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.", true);
            input = new InputField(Font.text) {placeholder = "Input something here"};
            scrollArea = new ScrollListTest();
            padding = new Padding(5f, 2f);
            scrollList = new VirtualScrollList<string, FontString>(new SizeF(10, 10), 2);
            button = new TextButton(Font.text, "Click me", ClickMeClick);

            var arr = new string[20];
            for (var i = 0; i < 20; i++)
                arr[i] = i.ToString();
            scrollList.data = arr;
            Create("Welcome", 50f, true);
        }

        private class ScrollListTest : VerticalScroll
        {
            private readonly FontString superLongText = new FontString(Font.text,
                "At vero eos et accusamus et iusto odio dignissimos ducimus qui blanditiis praesentium voluptatum deleniti atque corrupti quos dolores et quas molestias excepturi sint occaecati cupiditate non provident, similique sunt in culpa qui officia deserunt mollitia animi, id est laborum et dolorum fuga. Et harum quidem rerum facilis est et expedita distinctio. Nam libero tempore, cum soluta nobis est eligendi optio cumque nihil impedit quo minus id quod maxime placeat facere possimus, omnis voluptas assumenda est, omnis dolor repellendus. Temporibus autem quibusdam et aut officiis debitis aut rerum necessitatibus saepe eveniet ut et voluptates repudiandae sint et molestiae non recusandae. Itaque earum rerum hic tenetur a sapiente delectus, ut aut reiciendis voluptatibus maiores alias consequatur aut perferendis doloribus asperiores repellat.",
                true);
            public ScrollListTest() : base(new SizeF(10f, 10f)) {}

            protected override LayoutPosition BuildScrollContents(RenderBatch batch, LayoutPosition position)
            {
                position.Build(superLongText, batch);
                return position;
            }
        }
    }
}