using System.Drawing;

namespace UI
{
    public interface IWidget
    {
        LayoutPosition Build(RenderBatch batch, LayoutPosition location);
    }
    public interface IPanel
    {
        LayoutPosition BuildPanel(RenderBatch batch, LayoutPosition location);
    }

    public abstract class WidgetContainer : IWidget
    {
        private Padding _padding = new Padding(1f, 0.5f);
        private RenderBatch batch;
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

        public virtual LayoutPosition Build(RenderBatch batch, LayoutPosition location)
        {
            this.batch = batch;
            var padded = location.AddTopPadding(_padding);
            var content = BuildContent(batch, padded);
            var bottom = content.AddBottomPadding(_padding);
            var rect = bottom.GetRect(location);
            BuildBox(batch, rect);
            return bottom;
        }

        protected virtual void BuildBox(RenderBatch batch, RectangleF rect)
        {
            var box = boxColor;
            var handle = interactable ? this as IMouseHandle : null;
            if (box != SchemeColor.None || handle != null)
                batch.DrawRectangle(rect, boxColor, RectangleShadow.None, handle);
        }

        protected abstract LayoutPosition BuildContent(RenderBatch batch, LayoutPosition location);
    }

    public abstract class Panel : WidgetContainer, IPanel
    {
        protected readonly RenderBatch subBatch;
        protected readonly SizeF size;
        public readonly Alignment align;

        protected Panel(SizeF size, Alignment align = Alignment.Fill)
        {
            subBatch = new RenderBatch(this);
            this.align = align;
            this.size = size;
        }
        
        protected override LayoutPosition BuildContent(RenderBatch batch, LayoutPosition location)
        {
            var contentRect = location.IntoRect(size.Width, size.Height, align);
            batch.DrawSubBatch(contentRect, subBatch, interactable ? this as IMouseHandle : null);
            return location;
        }

        public abstract LayoutPosition BuildPanel(RenderBatch batch, LayoutPosition location);
        
        protected void RebuildContents()
        {
            subBatch?.Rebuild();
        }
    }
}