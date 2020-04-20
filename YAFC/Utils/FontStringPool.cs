using System;
using YAFC.UI;

namespace YAFC
{
    public struct FontStringPool
    {
        private FontString[] objects;
        private int index;
        private readonly Font font;
        private readonly bool wrap;
        private readonly SchemeColor color;
        private readonly RectAlignment align;

        public FontStringPool(Font font, SchemeColor color, bool wrap, RectAlignment align = RectAlignment.MiddleLeft)
        {
            objects = null;
            index = 0;
            this.color = color;
            this.align = align;
            this.font = font;
            this.wrap = wrap;
        }
        
        public void Reset()
        {
            index = 0;
        }
        
        public FontString Get()
        {
            if (objects == null || objects.Length < index)
                Array.Resize(ref objects, objects?.Length*2 ?? 4);
            return objects[index] ?? (objects[index] = new FontString(font, color:color, wrap:wrap, align:align));
        }
    }
}