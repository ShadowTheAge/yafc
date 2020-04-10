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

        public override SchemeColor boxColor => SchemeColor.Primary;

        public ScreenHeader(ICloseable closeable, string text)
        {
            fontString.text = text;
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

        public override LayoutPosition Build(RenderBatch batch, LayoutPosition location)
        {
            var closeRect = closeButton.Build(batch, location.Align(Alignment.Right));
            location.PadRight(closeRect);
            return base.Build(batch, location);
        }

        protected override LayoutPosition BuildContent(RenderBatch batch, LayoutPosition location)
        {
            return fontString.Build(batch, location);
        }
    }
}