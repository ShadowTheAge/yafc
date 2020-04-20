using System.Numerics;

namespace YAFC.UI
{
    public abstract class Tooltip : StandalonePanel
    {
        protected Tooltip() : base(new Vector2(20, 20)){}

        private HitTestResult<IMouseHandle> _hitTest;

        protected override Vector2 CalculatePosition(Vector2 contentSize)
        {
            var owner = _hitTest.batch;
            var topleft = owner.ToRootPosition(_hitTest.rect.TopLeft);
            var bottomRight = owner.ToRootPosition(_hitTest.rect.BottomRight);
            var rect = Rect.SideRect(topleft, bottomRight);
            var windowSize = owner.window.size;

            var y = MathUtils.Clamp(rect.Y, 0, windowSize.Y - contentSize.Y);
            var x = rect.Right + contentSize.X <= windowSize.X ? rect.Right : rect.Left - contentSize.X;
            return new Vector2(x, y);
        }

        protected void ShowTooltip(HitTestResult<IMouseHandle> hitTest)
        {
            this._hitTest = hitTest;
        }
        
        public override void Build(LayoutState state)
        {
            if (_hitTest.target == null)
                return;
            if (!InputSystem.Instance.HitTest<IMouseHandle>(out var result) || result.target != result.target)
            {
                _hitTest = default;
                return;
            }
            base.Build(state);
        }
    }
}