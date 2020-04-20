using System.Numerics;

namespace YAFC.UI
{
    public abstract class Tooltip : StandalonePanel
    {
        protected Tooltip() : base(new Vector2(20, 20)){}

        private RaycastResult<IMouseEnterHandle> raycast;

        protected override Vector2 CalculatePosition(Vector2 contentSize)
        {
            var owner = raycast.owner;
            var topleft = owner.ToRootPosition(raycast.rect.TopLeft);
            var bottomRight = owner.ToRootPosition(raycast.rect.BottomRight);
            var rect = Rect.SideRect(topleft, bottomRight);
            var windowSize = owner.window.size;

            var y = MathUtils.Clamp(rect.Y, 0, windowSize.Y - contentSize.Y);
            var x = rect.Right + contentSize.X <= windowSize.X ? rect.Right : rect.Left - contentSize.X;
            return new Vector2(x, y);
        }

        protected void ShowTooltip(RaycastResult<IMouseEnterHandle> raycast)
        {
            this.raycast = raycast;
        }
        
        public override void Build(LayoutState state)
        {
            if (raycast.target == null)
                return;
            if (!InputSystem.Instance.Raycast<IMouseEnterHandle>(out var result) || result.target != result.target)
            {
                raycast = default;
                return;
            }
            base.Build(state);
        }
    }
}