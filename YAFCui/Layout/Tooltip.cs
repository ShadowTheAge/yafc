using System.Numerics;

namespace YAFC.UI
{
    public abstract class Tooltip : StandalonePanel
    {
        private HitTestResult<IMouseHandle> _hitTest;

        protected override Vector2 CalculatePosition(LayoutState state, Vector2 contentSize)
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
            _hitTest = hitTest;
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

    public abstract class ContextMenu : StandalonePanel, IMouseFocus
    {
        private Vector2 anchorPositiion;
        private bool active;
        protected override Vector2 CalculatePosition(LayoutState state, Vector2 contentSize)
        {
            var windowSize = state.batch.window.size;

            var y = MathUtils.Clamp(anchorPositiion.Y, 0, windowSize.Y - contentSize.Y);
            var x = anchorPositiion.X + contentSize.X <= windowSize.X ? anchorPositiion.X : anchorPositiion.X - contentSize.X;
            return new Vector2(x, y);
        }

        public void Show()
        {
            anchorPositiion = InputSystem.Instance.mousePosition;
            InputSystem.Instance.SetMouseFocus(this);
        }

        public override void Build(LayoutState state)
        {
            if (!active)
                return;
            base.Build(state);
        }

        public void FocusChanged(bool focused)
        {
            active = focused;
            RebuildParent();
        }
    }
}