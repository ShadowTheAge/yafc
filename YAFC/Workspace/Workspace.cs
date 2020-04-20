using System.Collections.Generic;
using System.Numerics;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class Workspace : IPanel, IWidget, IMouseClickHandle
    {
        private UiBatch batch;
        private Dictionary<NodeId, NodeView> nodes = new Dictionary<NodeId, NodeView>();

        public Workspace()
        {
            batch = new UiBatch(this);
        }
        
        public Vector2 BuildPanel(UiBatch batch, Vector2 size)
        {
            var state = new LayoutState(batch, 0, RectAllocator.FixedRect);
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

        public void MouseClickUpdateState(bool mouseOverAndDown, int button, UiBatch batch) {}

        public void MouseClick(int button, UiBatch batch)
        {
            
        }
    }
}