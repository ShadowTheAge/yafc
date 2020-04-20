using System.Numerics;

namespace YAFC.UI
{
    public interface IWidget
    {
        void Build(LayoutState state);
    }
    public interface IPanel
    {
        Vector2 BuildPanel(UiBatch batch, Vector2 size);
    }

    public abstract class WidgetContainer : IWidget
    {
        private Padding _padding = new Padding(1f, 0.5f);
        private UiBatch batch;
        public virtual SchemeColor boxColor => SchemeColor.None;
        
        protected bool _interactable = true;

        public bool interactable
        {
            get => _interactable;
            set
            {
                _interactable = value;
                Rebuild();
            }
        }

        public Padding padding
        {
            get => _padding;
            set
            {
                _padding = value;
                Rebuild();
            }
        }

        protected void Rebuild()
        {
            batch?.Rebuild();
        }

        public virtual void Build(LayoutState state)
        {
            batch = state.batch;
            using (state.EnterGroup(_padding, state.allocator))
            {
                BuildContent(state);
            }

            var rect = state.lastRect;
            BuildBox(state, rect);
        }

        protected virtual void BuildBox(LayoutState state, Rect rect)
        {
            var box = boxColor;
            var handle = interactable ? this as IMouseHandleBase : null;
            if (box != SchemeColor.None || handle != null)
                state.batch.DrawRectangle(rect, boxColor, RectangleBorder.None, handle);
        }

        protected abstract void BuildContent(LayoutState state);
    }

    public abstract class Panel : WidgetContainer, IPanel
    {
        protected readonly UiBatch subBatch;
        protected readonly Vector2 size;
        public readonly RectAllocator allocator;
        public RectAllocator defaultAllocator => allocator;

        protected Panel(Vector2 size, RectAllocator allocator)
        {
            subBatch = new UiBatch(this);
            this.allocator = allocator;
            this.size = size;
        }
        
        protected override void BuildContent(LayoutState state)
        {
            var rect = state.AllocateRect(size.X, size.Y);
            state.batch.DrawSubBatch(rect, subBatch, interactable ? this as IMouseHandleBase : null);
        }

        public abstract Vector2 BuildPanel(UiBatch batch, Vector2 size);

        protected void RebuildContents()
        {
            subBatch?.Rebuild();
        }
    }
    
    public enum Anchor
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    public abstract class StandalonePanel : WidgetContainer, IPanel
    {
        protected readonly UiBatch subBatch;
        private readonly Vector2 size;

        protected StandalonePanel(Vector2 size)
        {
            subBatch = new UiBatch(this);
            padding = new Padding(0.5f);
            this.size = size;
        }

        public override void Build(LayoutState state)
        {
            if (subBatch.IsRebuildRequired())
                subBatch.Rebuild(state.batch.window, size, state.batch.pixelsPerUnit);
            var pos = CalculatePosition(subBatch.size);
            var rect = new Rect(pos, subBatch.size);
            state.batch.DrawSubBatch(rect, subBatch, this as IMouseHandleBase);
            state.batch.DrawRectangle(rect, SchemeColor.None, RectangleBorder.Thin);
        }

        protected abstract Vector2 CalculatePosition(Vector2 contentSize);
        

        Vector2 IPanel.BuildPanel(UiBatch batch, Vector2 size)
        {
            var state = new LayoutState(batch, size.X, RectAllocator.Stretch);
            base.Build(state);
            state.batch.DrawRectangle(new Rect(default, new Vector2(state.width, state.fullHeight)), SchemeColor.Background);
            return state.size;
        }
    }

    public abstract class ManualAnchorPanel : StandalonePanel
    {
        private Vector2 point;
        private Anchor anchor;
        
        protected ManualAnchorPanel(Vector2 size) : base(size) {}
        
        public void SetAnchor(Vector2 point, Anchor anchor)
        {
            this.anchor = anchor;
            this.point = point;
        }
        
        protected override Vector2 CalculatePosition(Vector2 contentSize)
        {
            var position = point;
            var ofX = (int) anchor % 3;
            var ofY = (int) anchor / 3;
            position.X -= subBatch.size.X * 0.5f * ofX;
            position.Y -= subBatch.size.Y * 0.5f * ofY;
            return position;
        }
    }
}