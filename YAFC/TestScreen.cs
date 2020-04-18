using System.Numerics;
using YAFC.UI;

namespace YAFC
{
    public class TestScreen : WindowUtility
    {
        private FontString header;
        private FontString text;
        private InputField input;
        private ScrollListTest scrollArea;
        private VirtualScrollList<string, FontString> scrollList;
        private TextButton aboutButton;

        private void AboutClick(UiBatch batch)
        {
            new AboutScreen(this);
        }

        protected override void BuildContent(LayoutState state)
        {
            state.Build(header).Build(text).Build(input).Build(scrollArea).Build(scrollList);
            using (state.EnterGroup(default, RectAllocator.LeftRow))
            {
                state.Build(aboutButton);
            }
        }

        public TestScreen()
        {
            header = new FontString(Font.header, "Yet Another Factorio Calculator");
            text = new FontString(Font.text, "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.", true);
            input = new InputField(Font.text) {placeholder = "Input something here"};
            scrollArea = new ScrollListTest();
            scrollList = new VirtualScrollList<string, FontString>(new Vector2(10, 10), 2);
            aboutButton = new TextButton(Font.text, "About", AboutClick);

            var arr = new string[20];
            for (var i = 0; i < 20; i++)
                arr[i] = i.ToString();
            scrollList.data = arr;
            Create("Welcome", 50f, null);
        }

        private class ScrollListTest : VerticalScroll
        {
            private readonly FontString superLongText = new FontString(Font.text,
                "At vero eos et accusamus et iusto odio dignissimos ducimus qui blanditiis praesentium voluptatum deleniti atque corrupti quos dolores et quas molestias excepturi sint occaecati cupiditate non provident, similique sunt in culpa qui officia deserunt mollitia animi, id est laborum et dolorum fuga. Et harum quidem rerum facilis est et expedita distinctio. Nam libero tempore, cum soluta nobis est eligendi optio cumque nihil impedit quo minus id quod maxime placeat facere possimus, omnis voluptas assumenda est, omnis dolor repellendus. Temporibus autem quibusdam et aut officiis debitis aut rerum necessitatibus saepe eveniet ut et voluptates repudiandae sint et molestiae non recusandae. Itaque earum rerum hic tenetur a sapiente delectus, ut aut reiciendis voluptatibus maiores alias consequatur aut perferendis doloribus asperiores repellat.",
                true);
            public ScrollListTest() : base(new Vector2(10f, 10f)) {}

            protected override void BuildScrollContents(LayoutState state)
            {
                state.Build(superLongText);
            }
        }
    }
}