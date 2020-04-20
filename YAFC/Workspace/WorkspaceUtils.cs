using System.Numerics;
using Routing;

namespace YAFC
{
    public static class WorkspaceUtils
    {
        public static Vector2 ToWorkspacePos(this GridPos pos) => new Vector2(pos.x*3, pos.y*3);
    }
}