using YAFC.UI;

namespace YAFC
{
    public static class BuildUtils
    {
        public static void BuildHeader(FontString header, LayoutState state)
        {
            using (state.EnterGroup(new Padding(1f, 0.5f), RectAllocator.Stretch))
            {
                state.Build(header);
            }
            state.batch.DrawRectangle(state.lastRect, SchemeColor.Primary);
        }

        public static void BuildSubHeader(FontString header, LayoutState state)
        {
            using (state.EnterGroup(new Padding(1f, 0.25f), RectAllocator.Stretch))
            {
                state.Build(header);
            }
            state.batch.DrawRectangle(state.lastRect, SchemeColor.Grey);
        }
    }
}