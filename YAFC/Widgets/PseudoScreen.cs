using System.Numerics;
using YAFC.UI;

namespace YAFC
{
    // Pseudo screen is not an actual screen, it is a panel shown in the middle of the main screen
    public abstract class PseudoScreen : StandalonePanel
    {
        protected override RectangleBorder border => RectangleBorder.Full;

        protected override Vector2 CalculatePosition(LayoutState state, Vector2 contentSize)
        {
            var windowSize = state.batch.window.size;
            return (windowSize - contentSize) / 2;
        }

        public override void Build(LayoutState state)
        {
            base.Build(state);
            state.batch.DrawRectangle(new Rect(default, state.batch.window.size), SchemeColor.BlackTransparent);
        }
    }
}