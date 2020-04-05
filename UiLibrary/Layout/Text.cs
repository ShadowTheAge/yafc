using System.Drawing;

namespace UI
{
    public class Text : LayoutElement
    {
        private string _text;

        private readonly FontString fontString;
        
        public Text(Font font, bool wrap)
        {
            fontString = new FontString(font, wrap);
        }

        public string text
        {
            get => _text;
            set
            {
                if (_text == value)
                    return;
                _text = value;
                SetDirty();
            }
        }

        protected override float DrawContent(RenderBatch batch, PointF position, float width)
        {
            if (!ReferenceEquals(fontString.text, _text) || width != fontString.size.Width)
                fontString.Update(_text, width);
            var rect = new RectangleF(position, fontString.size);
            batch.DrawText(rect, fontString);
            return fontString.size.Height;
        }
    }
}