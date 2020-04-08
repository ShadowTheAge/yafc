using System.Drawing;

namespace UI
{
    public interface ICloseable
    {
        void Close();
    }
    
    public class ScreenHeader : WidgetContainer
    {
        private readonly IconButton closeButton;
        private readonly FontString fontString = new FontString(DefaultStyle.header, false);

        public ScreenHeader(ICloseable closeable)
        {
            closeButton = new IconButton(Sprite.Close, button =>
            {
                if (button == 0)
                    closeable.Close();
            });
        }

        public string text
        {
            get => fontString.text;
            set => fontString.text = value;
        }

        public override RectangleF Build(RenderBatch batch, LayoutPosition location, Alignment align)
        {
            var closeRect = closeButton.Build(batch, location, Alignment.Right);
            location.PadRight(closeRect);
            return base.Build(batch, location, align);
        }

        public override RectangleF BuildContent(RenderBatch batch, LayoutPosition location, Alignment align)
        {
            return fontString.Build(batch, location, align);
        }
    }
}