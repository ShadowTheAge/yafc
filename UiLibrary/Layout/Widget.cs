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

        public Padding padding
        {
            get => _padding;
            set
            {
                _padding = value;
                SetDirty();
            }
        }

        protected void SetDirty()
        {
            batch?.SetDirty();
        }

        public virtual LayoutPosition Build(RenderBatch batch, LayoutPosition location)
        {
            this.batch = batch;
            var padded = location.AddTopPadding(_padding);
            var content = BuildContent(batch, padded);
            var bottom = content.AddBottomPadding(_padding);
            var box = boxColor;
            if (box != SchemeColor.None)
            {
                var rect = bottom.GetRect(location);
                batch.DrawRectangle(rect, boxColor);
            }
            return bottom;
        }

        protected abstract LayoutPosition BuildContent(RenderBatch batch, LayoutPosition location);
    }

    public abstract class Panel : WidgetContainer, IPanel
    {
        private readonly RenderBatch subBatch;
        private readonly SizeF size;

        protected Panel(SizeF size)
        {
            subBatch = new RenderBatch(this);
            this.size = size;
        }
        
        protected override LayoutPosition BuildContent(RenderBatch batch, LayoutPosition location)
        {
            var contentRect = location.IntoRect(size.Width, size.Height);
            batch.DrawSubBatch(contentRect, subBatch);
            return location;
        }
        public abstract LayoutPosition BuildPanel(RenderBatch batch, LayoutPosition location);
    }
}