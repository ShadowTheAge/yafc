namespace UI
{
    public abstract class LayoutContainer : LayoutElement
    {
        public abstract void AppendElement(LayoutElement child);
        public abstract void RemoveElement(LayoutElement child);
    }
}