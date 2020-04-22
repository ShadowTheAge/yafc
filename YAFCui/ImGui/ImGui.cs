using System;
using System.Numerics;
using SDL2;

namespace YAFC.UI
{
    public enum ImGuiType
    {
        Measure,
        Render,
    }
    
    public class ImGui
    {
        private float pixelsPerUnit;
        private ImGuiType type;
        public LayoutState state;
        private IntPtr renderer;
        
        public int UnitsToPixels(float units) => (int) MathF.Round(units * pixelsPerUnit);
        public float PixelsToUnits(int pixels) => pixels / pixelsPerUnit;
        private Vector2 mousePosition;
        
        public SDL.SDL_Rect ToSdlRect(Rect rect, Vector2 offset = default)
        {
            return new SDL.SDL_Rect
            {
                x = UnitsToPixels(rect.X + offset.X),
                y = UnitsToPixels(rect.Y + offset.Y),
                w = UnitsToPixels(rect.Width),
                h = UnitsToPixels(rect.Height)
            };
        }

        private readonly ImGuiCache<TextCache, (FontFile.FontSize size, string text, uint wrapWidth)>.Cache textCache = new ImGuiCache<TextCache, (FontFile.FontSize size, string text, uint wrapWidth)>.Cache();

        public void BuildText(string text, Font font = null, SchemeColor color = SchemeColor.BackgroundText, bool wrap = false, RectAlignment align = RectAlignment.MiddleLeft)
        {
            var fontSize = (font ?? Font.text).GetFontSize(pixelsPerUnit);
            var cache = textCache.GetCached((fontSize, text, wrap ? (uint) state.batch.UnitsToPixels(state.width) : uint.MaxValue));
            var rect = state.AllocateRect(cache.texRect.w / state.batch.pixelsPerUnit, cache.texRect.h / state.batch.pixelsPerUnit, align);
            if (type == ImGuiType.Render)
            {
                cache.Render(renderer, ToSdlRect(rect), color.ToSdlColor());
            }
        }

        public bool IsMouseOver(Rect rect)
        {
            return rect.Contains(mousePosition);
        }

        public void ConsumeEvent()
        {
            throw new EventConsumedException(); 
        }

        public void PurgeCaches()
        {
            textCache.PurgeUnused();
        }

        private class EventConsumedException : Exception
        {
            
        }
    }
}