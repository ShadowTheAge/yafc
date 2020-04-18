using System.Drawing;

namespace YAFC.UI
{
    public interface IWidget
    {
        void Build(LayoutState state);
    }
    public interface IPanel
    {
        void BuildPanel(LayoutState state);
        RectAllocator defaultAllocator { get; }
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

        protected virtual void BuildBox(LayoutState state, RectangleF rect)
        {
            var box = boxColor;
            var handle = interactable ? this as IMouseHandle : null;
            if (box != SchemeColor.None || handle != null)
                state.batch.DrawRectangle(rect, boxColor, RectangleBorder.None, handle);
        }

        protected abstract void BuildContent(LayoutState state);
    }

    public abstract class Panel : WidgetContainer, IPanel
    {
        protected readonly UiBatch subBatch;
        protected readonly SizeF size;
        public readonly RectAllocator allocator;
        public RectAllocator defaultAllocator => allocator;

        protected Panel(SizeF size, RectAllocator allocator)
        {
            subBatch = new UiBatch(this);
            this.allocator = allocator;
            this.size = size;
        }
        
        protected override void BuildContent(LayoutState state)
        {
            var rect = state.AllocateRect(size.Width, size.Height);
            state.batch.DrawSubBatch(rect, subBatch, interactable ? this as IMouseHandle : null);
        }

        public abstract void BuildPanel(LayoutState state);

        protected void RebuildContents()
        {
            subBatch?.Rebuild();
        }
    }

    public abstract class Overlay : WidgetContainer, IPanel
    {
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
        
        public virtual RectAllocator defaultAllocator => RectAllocator.Stretch;
        protected readonly UiBatch subBatch;
        private SizeF size;

        protected Overlay(SizeF size)
        {
            subBatch = new UiBatch(this);
            padding = new Padding(0.5f);
            this.size = size;
        }

        public void BuildAtPoint(PointF point, Anchor anchor, LayoutState state)
        {
            if (subBatch.IsRebuildRequired())
                subBatch.Rebuild(state.batch.window, size, state.batch.pixelsPerUnit);
            var rect = new RectangleF(point, subBatch.size);
            var ofX = (int) anchor % 3;
            var ofY = (int) anchor / 3;
            rect.X -= subBatch.size.Width * 0.5f * ofX;
            rect.Y -= subBatch.size.Height * 0.5f * ofY;
            state.batch.DrawSubBatch(rect, subBatch, this as IMouseHandle);
            state.batch.DrawRectangle(rect, SchemeColor.None, RectangleBorder.Thin);
        }

        void IPanel.BuildPanel(LayoutState state)
        {
            Build(state);
            state.batch.DrawRectangle(new RectangleF(default, new SizeF(state.width, state.fullHeight)), SchemeColor.Background);
        }
    }
}