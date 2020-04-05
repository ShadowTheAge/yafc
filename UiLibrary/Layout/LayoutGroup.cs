using System.Collections.Generic;

namespace UI
{
    public abstract class LayoutGroup : LayoutContainer
    {
        protected readonly List<LayoutElement> children = new List<LayoutElement>();

        public override void AppendElement(LayoutElement child)
        {
            children.Add(child);
            child.SetParent(this);
        }

        public override void RemoveElement(LayoutElement child)
        {
            if (children.Remove(child))
                child.ClearParent(this);
        }
    }
}