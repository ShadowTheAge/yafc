using System;
using System.Collections.Generic;

namespace Routing
{
    [Flags]
    public enum Direction : byte
    {
        Up = 0,
        Left = 1,
        Down = 2,
        Right = 3,
        Invalid = 4,
    }
    
    public struct PathFindingPos
    {
        public GridPos pos;
        public Direction dir;
        public int clusterId;

        public void Forward()
        {
            switch (dir)
            {
                case Direction.Up: pos.y -= 1; break;
                case Direction.Left: pos.x -= 1; break;
                case Direction.Down: pos.y += 1; break;
                case Direction.Right: pos.x += 1; break;
            }
        }

        public void Left()
        {
            dir = ((dir + 1) & GridFieldPathfinding.MASK);
            Forward();
        }
        
        public void Right()
        {
            dir = ((dir - 1) & GridFieldPathfinding.MASK);
            Forward();
        }
    }

    [Flags]
    public enum Channels : byte
    {
        None = 0,
        Horizontal1 = 0x1,
        Horizontal2 = 0x2,
        Horizontal3 = 0x3,
        Horizontal4 = 0x4,
        AllHorizontal = 0xF,
        Vertical1 = 0x10,
        Vertical2 = 0x20,
        Vertical3 = 0x40,
        Vertical4 = 0x80,
        AllVertical = 0xF0,
        All = 0xFF
    }

    public enum PathSegmentType : byte
    {
        Intermediate,
        Initial,
        Terminal,
        InitialAndTerminal
    }
    
    public struct PathSegment
    {
        public GridPos pos;
        public Direction dir;
        public PathSegmentType type;
        public Channels startChannel;
        public Channels channel;
        public Channels endChannel;
        public ushort length;
    }

    internal struct SegmentHead
    {
        public PathFindingPos pos;
        public int length;
        public Channels channel;
        public Channels outerChannel;
        public int cluster;
    }

    public class GridFieldPathfinding
    {
        public const Direction MASK = (Direction) 3;
        public const Direction INVERT = (Direction) 2;
        public const Direction HORIZONTAL = (Direction) 1;
        public const Direction POSITIVE = (Direction) 2;
        
        public const int TURN_PENALTY = 4;
        private readonly GridField field;

        public GridFieldPathfinding(GridField field)
        {
            this.field = field;
        }

        private GridFieldPathfindingQueue<PathFindingPos> queue = new GridFieldPathfindingQueue<PathFindingPos>(16);

        private int uniqueClusters;
        private int clusterCount;
        internal int pfId;
        private (int indirection, PathFindingPos source, int heads, int mergeOrder)[] clusters;
        private List<SegmentHead> segmentHeads = new List<SegmentHead>();
        private List<PathSegment> builtSegments = new List<PathSegment>();

        private (int, int) Transform(int x, int y, Direction dir)
        {
            switch (dir)
            {
                case Direction.Up: default: return (x, y);
                case Direction.Left: return (-y, x);
                case Direction.Down: return (-x, -y);
                case Direction.Right: return (y, -x);
            }
        }

        private int GetMinWeight(PathFindingPos a, PathFindingPos b)
        {
            var (dx, dy) = Transform(b.pos.x - a.pos.x, b.pos.y - a.pos.y, a.dir);
            var bdir = ((Direction)(a.dir - b.dir) & MASK);
            var dist = Math.Abs(dx) + Math.Abs(dy);
            var minturns = 2;
            switch (bdir)
            {
                case Direction.Left:
                    minturns = dx <= 0 && dy <= 0 ? 1 : 3;
                    break;
                case Direction.Down:
                    minturns = dy <= 0 ? dx == 0 ? 0 : 2 : 4;
                    break;
                case Direction.Right:
                    minturns = dx >= 0 && dy <= 0 ? 1 : 3;
                    break;
            }

            return dist + minturns * TURN_PENALTY;
        }

        private int GetHeuristics(PathFindingPos pos)
        {
            var cluster = clusters[pos.clusterId].indirection;
            var maxWeight = int.MaxValue;
            for (var i = 0; i < clusterCount; i++)
            {
                if (clusters[i].indirection != cluster)
                {
                    var weight = GetMinWeight(pos, clusters[i].source);
                    if (weight < maxWeight)
                        maxWeight = weight;
                }
            }

            return maxWeight;
        }

        private void MergeClusters(int idFrom, int idTo)
        {
            uniqueClusters--;
            ref var toHeads = ref clusters[idTo].heads;
            clusters[idTo].mergeOrder = uniqueClusters;
            for (var i = 0; i < clusterCount; i++)
            {
                ref var cluster = ref clusters[i];
                if (cluster.indirection == idFrom)
                {
                    cluster.indirection = idTo;
                    toHeads += cluster.heads;
                    cluster.heads = 0;
                }
            }
        }

        private bool Add(ref PathFindingPos pos, int len, ref int heads, Channels channels)
        {
            var chunk = field.GetChunk(pos.pos);
            ref var p = ref chunk[pos.pos.x, pos.pos.y, pfId];
            ref var cell = ref chunk[pos.pos.x, pos.pos.y]; 
            if (cell.ocount > 0)
                return false;
            if ((channels | cell.channels) != channels)
            {
                channels |= cell.channels;
                if (channels == Channels.All)
                    return false;
                len++;
            }
            if (p.len != 0)
            {
                var idFrom = clusters[pos.clusterId].indirection;
                var idTo = clusters[p.cluster].indirection;
                if (idFrom != idTo)
                {
                    MergeClusters(idFrom, idTo);
                    var isFrontToBackMerge = pos.dir == (p.from ^ INVERT);
                    var channel = FindChannel(channels, pos.dir);
                    var outerChannel = isFrontToBackMerge ? channel : FindChannel(p.avchannels, p.from);
                    segmentHeads.Add(new SegmentHead {pos = pos, length = len, channel = channel, cluster = pos.clusterId, outerChannel = isFrontToBackMerge ? Channels.All : outerChannel});
                    var original = pos;
                    original.dir = p.from;
                    segmentHeads.Add(new SegmentHead {pos = original, length = p.len, channel = outerChannel, cluster = p.cluster, outerChannel = isFrontToBackMerge ? Channels.All : channel});
                    return true;
                }
                return false;
            }

            heads++;
            p.from = pos.dir;
            p.len = (ushort)len;
            p.cluster = (ushort)pos.clusterId;
            p.avchannels = channels;
            queue.Add(pos, len + GetHeuristics(pos));
            return false;
        }

        private ref int GetHeads(int clusterId) => ref clusters[clusters[clusterId].indirection].heads;
        
        public PathSegment[] FindPath(PathFindingPos[] points, ushort limit = 60000)
        {
            pfId = field.GetPathFindingId();
            segmentHeads.Clear();
            if (clusters == null || clusters.Length < points.Length)
                clusters = new (int, PathFindingPos, int, int)[points.Length];
            clusterCount = points.Length;
            for (var i = 0; i < points.Length; i++)
            {
                ref var pt = ref points[i];
                pt.clusterId = i;
                clusters[i] = (i, pt, 1, -1);
                ref var data = ref field.GetChunk(pt.pos)[pt.pos.x, pt.pos.y, pfId];
                if (data.len != 0)
                    clusterCount--;
                data.cluster = (ushort) i;
                data.len = 1;
                data.avchannels = ((pt.dir & HORIZONTAL) == 0) ? Channels.AllHorizontal : Channels.AllVertical;
                data.@from = pt.dir;
            }
            queue.Reset();
            
            uniqueClusters = clusterCount;
            for (var i = 0; i < points.Length; i++)
            {
                var pos = points[i];
                queue.Add(pos, GetHeuristics(pos));
            }

            var opc = limit;
            while (queue.GetNext(out var pos) && uniqueClusters > 1 && opc > 0)
            {
                var p = pos.pos;
                var chunk = field.GetChunk(p);
                ref var pfInfo = ref chunk[p.x, p.y, pfId];
                var channels = pfInfo.avchannels;
                var len = pfInfo.len;
                ref var heads = ref GetHeads(pos.clusterId);
                heads--;

                var next = pos;
                next.Forward();
                if (Add(ref next, len+1, ref heads, channels))
                    heads = ref GetHeads(pos.clusterId);

                var sideChannels = ((pos.dir & HORIZONTAL) == 0) ? Channels.AllVertical : Channels.AllHorizontal;

                next = pos;
                next.Left();
                if (Add(ref next, len + TURN_PENALTY, ref heads, sideChannels))
                    heads = ref GetHeads(pos.clusterId);
                
                next = pos;
                next.Right();
                if (Add(ref next, len + TURN_PENALTY, ref heads, sideChannels))
                    heads = ref GetHeads(pos.clusterId);
                --opc;
                if (heads == 0)
                    uniqueClusters--;
            }
            
            BuildSegmentList();
            return builtSegments.ToArray();
        }

        private Channels FindChannel(Channels occupied, Direction dir)
        {
            var offset = ((dir & HORIZONTAL) == 0) ? Channels.Vertical1 : Channels.Horizontal1;
            for (var i = 0; i < 4; i++)
            {
                var ch = (Channels)((int)offset << i);
                if ((occupied & ch) == 0)
                    return ch;
            }
            return Channels.None;
        }

        private void BuildSegmentsFrom(PathFindingPos pos, Channels channel, Channels outerChannel)
        {
            if (pos.dir == Direction.Invalid)
                return;
            
            ref var cellInfo = ref field.GetChunk(pos.pos)[pos.pos.x, pos.pos.y, pfId];
            var terminateNextSegment = cellInfo.len == 1 && cellInfo.from == pos.dir;
            pos.dir ^= INVERT;
            var segment = new PathSegment {type = PathSegmentType.Initial, pos = pos.pos, dir = pos.dir, channel = channel, startChannel = outerChannel};
            while (true)
            {
                pos.Forward();
                segment.length++;
                var chunk = field.GetChunk(pos.pos);
                cellInfo = ref chunk[pos.pos.x, pos.pos.y, pfId];
                chunk[pos.pos.x, pos.pos.y].channels |= segment.channel;
                var from = cellInfo.@from ^ INVERT;
                cellInfo.from = Direction.Invalid; // mark the cell so other segments won't go from here
                cellInfo.cluster = (ushort)segment.channel; // and also mark the channel for the possible junction here
                if (from != segment.dir || terminateNextSegment || cellInfo.len == 0)
                {
                    if (terminateNextSegment || from >= Direction.Invalid || cellInfo.len == 0)
                    {
                        segment.type |= PathSegmentType.Terminal;
                        segment.endChannel = @from >= Direction.Invalid ? (Channels) cellInfo.cluster : (pos.dir & POSITIVE) != 0 ? Channels.None : Channels.All;
                        builtSegments.Add(segment);
                        break;
                    }

                    segment.endChannel = FindChannel(cellInfo.avchannels, from);
                    builtSegments.Add(segment);
                    segment = new PathSegment {type = PathSegmentType.Intermediate, pos = pos.pos, dir = from, startChannel = segment.channel, channel = segment.endChannel};
                    pos.dir = from;
                }

                terminateNextSegment = cellInfo.len == 1;
            }
        }

        private void BuildSegmentList()
        {
            builtSegments.Clear();
            // segment processing order is important to ensure that channel junctions are calculated correctly
            segmentHeads.Sort((a, b) =>
            {
                var mergeOrder = clusters[a.cluster].mergeOrder - clusters[b.cluster].mergeOrder;
                return mergeOrder != 0 ? mergeOrder : b.length - a.length;
            });
            foreach (var head in segmentHeads)
            {
                BuildSegmentsFrom(head.pos, head.channel, head.outerChannel);
            }
        }
    }
}