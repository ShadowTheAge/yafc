using System;
using System.Collections.Generic;
using System.Numerics;
using YAFC.UI;

namespace YAFC
{
    public class Workspace : IPanel
    {
        private UiBatch batch;
        private Dictionary<NodeId, Node> nodes = new Dictionary<NodeId, Node>();

        public Workspace()
        {
            batch = new UiBatch(this);
        }
        public Vector2 BuildPanel(UiBatch batch, Vector2 size)
        {
            throw new NotImplementedException();
        }
    }
}