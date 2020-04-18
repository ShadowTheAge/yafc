using System.Numerics;
using YAFC.UI;

namespace YAFC
{
    public enum NodeId
    {
        None = 0
    }
    
    public abstract class Node : ManualPositionPanel
    {
        public Node(Vector2 size) : base(size) {}
    }
}