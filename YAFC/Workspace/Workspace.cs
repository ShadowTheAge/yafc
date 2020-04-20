using System;
using System.Collections.Generic;
using System.Numerics;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class Workspace : IPanel, IWidget, IMouseDragHandle, IMouseScrollHandle
    {
        private readonly UiBatch batch;
        private Dictionary<NodeId, NodeView> nodes = new Dictionary<NodeId, NodeView>();
        private static float[] zoomLevels = new[] {1f, 2f/3f, 1f/2f, 1f/3f, 1f/5f, 1f/10f};
        private int zoomLevel;

        public Workspace()
        {
            batch = new UiBatch(this);
        }

        private void DrawGrid(UiBatch batch, Rect rect)
        {
            for (var x = MathF.Floor(rect.Right / 3) * 3f - 0.05f; x > rect.X; x-=3f)
                batch.DrawRectangle(new Rect(x, rect.Y, 0.1f, rect.Height), SchemeColor.Grey);
            for (var y = MathF.Floor(rect.Bottom / 3) * 3f - 0.05f; y > rect.Y; y-=3f)
                batch.DrawRectangle(new Rect(rect.X, y, rect.Width, 0.1f), SchemeColor.Grey);
        }
        
        
        private FontString testText = new FontString(Font.text, "0.336");
        public Vector2 BuildPanel(UiBatch batch, Vector2 size)
        {
            var state = new LayoutState(batch, 0, RectAllocator.FixedRect);
            
            batch.DrawRectangle(new Rect(10, 10, 10, 10), SchemeColor.Primary);
            testText.Build(new LayoutState(batch, 10, RectAllocator.LeftAlign));
            for (var i = 0; i < IconCollection.IconCount; i++)
            {
                batch.DrawIcon(new Rect(i*3f, 0, 3f, 3f), (Icon) i, SchemeColor.Source);
            }
            
            if (batch.pixelsPerUnit >= 10f)
                DrawGrid(batch, new Rect(-batch.offset, size / batch.scale));
            
            foreach (var (_, node) in nodes)
            {
                node.Build(state);
            }

            return default;
        }

        public void Build(LayoutState state)
        {
            state.batch.DrawSubBatch(new Rect(default, state.batch.window.size), batch, this);
        }

        public void MouseEnter(HitTestResult<IMouseHandle> hitTest) {}

        public void MouseExit(UiBatch batch) {}

        public void MouseClick(int button, UiBatch batch) {}

        private Vector2 dragAnchor;
        public void BeginDrag(Vector2 position, int button, UiBatch batch)
        {
            dragAnchor = position;
        }

        public void Drag(Vector2 position, UiBatch batch)
        {
            batch.offset += (position - dragAnchor) / batch.scale;
            dragAnchor = position;
            batch.Rebuild();
        }

        public void EndDrag(Vector2 position, UiBatch batch)
        {
            
        }

        public void Scroll(int delta, UiBatch batch)
        {
            var position = InputSystem.Instance.mousePosition;
            batch.offset -= position / batch.scale;
            zoomLevel = MathUtils.Clamp(zoomLevel + delta, 0, zoomLevels.Length-1);
            batch.scale = zoomLevels[zoomLevel];
            batch.offset += position / batch.scale;
        }
    }
}