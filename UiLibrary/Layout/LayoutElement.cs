using System.Drawing;

namespace UI
{
    public abstract class LayoutElement
    {
        private LayoutContainer parent;
        public float paddingLeft { get; set; }
        public float paddingRight { get; set; }
        public float paddingTop { get; set; }
        public float paddingBottom { get; set; }
        protected bool dirty = true;

        protected internal virtual float DrawAndGetHeight(RenderBatch batch, PointF position, float width)
        {
            var pos = new PointF(position.X + paddingLeft, position.Y + paddingTop);
            var height = DrawContent(batch, pos, width-paddingLeft-paddingRight) + paddingTop + paddingBottom;
            dirty = false;
            return height;
        }
        
        protected abstract float DrawContent(RenderBatch batch, PointF position, float width);
        protected virtual void AddedToDisplay() {}
        protected virtual void RemovedFromDisplay() {}

        internal virtual void SetParent(LayoutContainer parent)
        {
            if (parent == this.parent)
                return;
            if (this.parent != null)
                this.parent.RemoveElement(this);
            this.parent = parent;
            AddedToDisplay();
        }

        internal virtual void ClearParent(LayoutContainer parent)
        {
            if (this.parent == parent)
            {
                this.parent = null;
                RemovedFromDisplay();
            }
        }

        public void SetDirty()
        {
            if (!dirty)
            {
                dirty = true;
                parent?.SetDirty();
            }
        }
    }
}