using System.Numerics;
using Routing;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public abstract class NodeView : StandalonePanel
    {
        public abstract NodeConfiguration nodeConfig { get; }
        private readonly GridPos size;

        protected NodeView(GridPos size)
        {
            this.size = size;
        }

        protected override Vector2 CalculateSize(LayoutState state) => size.ToWorkspacePos();

        protected override Vector2 CalculatePosition(LayoutState state, Vector2 contentSize)
        {
            var pos = new GridPos(nodeConfig.x, nodeConfig.y);
            return pos.ToWorkspacePos();
        }
    }

    public abstract class NodeView<T> : NodeView where T : NodeConfiguration
    {
        private readonly T configuration;
        protected NodeView(T configuration, GridPos size) : base(size)
        {
            this.configuration = configuration;
        }

        public override NodeConfiguration nodeConfig => configuration;
    }
}