using System.Collections.Generic;
using System.Drawing;

namespace UI
{
    public class RenderBatch
    {
        private readonly List<(RectangleF, SchemeColor)> rects = new List<(RectangleF, SchemeColor)>();
        private readonly List<(RectangleF, RectangleShadow)> shadows = new List<(RectangleF, RectangleShadow)>();
        private readonly List<(RectangleF, Sprite)> sprites = new List<(RectangleF, Sprite)>();

        public void Clear()
        {
            rects.Clear();
            shadows.Clear();
            sprites.Clear();
        }
        
        public void DrawRectangle(RectangleF rect, SchemeColor color, RectangleShadow shadow = RectangleShadow.None)
        {
            rects.Add((rect, color));
            if (shadow != RectangleShadow.None)
                shadows.Add((rect, shadow));
        }

        public void DrawSprite(RectangleF rect, Sprite sprite)
        {
            sprites.Add((rect, sprite));
        }
    }
}