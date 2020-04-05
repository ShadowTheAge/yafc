using System;
using System.Drawing;

namespace UI
{
    public class VerticalGroup : LayoutGroup
    {
        protected override float MeasureContentWidth()
        {
            var width = 0f;
            foreach (var element in children)
                width = MathF.Max(width, element.MeasureWidth());
            return width;
        }

        protected override float DrawContent(RenderBatch batch, PointF position, float width)
        {
            var startPosition = position.Y;
            foreach (var element in children)
                position.Y += element.DrawAndGetHeight(batch, position, width);
            return position.Y - startPosition;
        }
    }
}