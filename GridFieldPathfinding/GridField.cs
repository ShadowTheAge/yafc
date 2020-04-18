using System;
using System.Collections.Generic;

namespace Routing
{
    public struct GridPos : IEquatable<GridPos>
    {
        public int x;
        public int y;

        public GridPos(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public override int GetHashCode() => x * 397 + y;
        public override bool Equals(object obj)
        {
            return obj is GridPos other && Equals(other);
        }

        public bool Equals(GridPos other)
        {
            return x == other.x && y == other.y;
        }
        
        public GridPos chunkPos => new GridPos(x & Chunk.NOT_CHUNK_MASK, y & Chunk.NOT_CHUNK_MASK);
    }

    public struct GridRect
    {
        public GridPos start;
        public GridPos end;
        
        public GridPos size => new GridPos(end.x-start.x, end.y-start.y);

        public GridRect(GridPos corner, int width, int height)
        {
            start = corner;
            end = new GridPos(corner.x + width, corner.y + height);
        }
    }

    internal class Chunk // each chunk is 32x32
    {
        public const int CHUNK_SIZE_BITS = 5;
        public const int CHUNK_MASK = (1 << CHUNK_SIZE_BITS) - 1;
        public const int NOT_CHUNK_MASK = ~CHUNK_MASK;
        public const int ARRAY_SIZE = 1 << (CHUNK_SIZE_BITS + CHUNK_SIZE_BITS);
        
        private GridCellInfo[] cells = new GridCellInfo[ARRAY_SIZE];
        private GridPathFindingInfo[] paths = new GridPathFindingInfo[ARRAY_SIZE];
        private int lastPfId;
        
        public ref GridCellInfo this[int x, int y] => ref cells[((x & CHUNK_MASK) << CHUNK_SIZE_BITS) | (y & CHUNK_MASK)];

        public ref GridPathFindingInfo this[int x, int y, int pfId]
        {
            get
            {
                if (pfId != lastPfId)
                {
                    Array.Clear(paths, 0, ARRAY_SIZE);
                    lastPfId = pfId;
                }
                return ref paths[((x & CHUNK_MASK) << CHUNK_SIZE_BITS) | (y & CHUNK_MASK)];
            }
        }
    }

    internal struct GridPathFindingInfo
    {
        public Direction from;
        public Channels avchannels;
        public ushort len;
        public ushort cluster;
    }

    public struct GridCellInfo
    {
        public Channels channels;
        public byte ocount;
    }
    
    public class GridField
    {
        private GridPos lastAccessedChunkPos = new GridPos(int.MinValue, int.MinValue);
        private Chunk lastAccessedChunk;
        private Dictionary<GridPos, Chunk> chunks = new Dictionary<GridPos, Chunk>();
        private int pathfindingId;

        public int GetPathFindingId() => ++pathfindingId;

        internal Chunk GetChunk(GridPos pos)
        {
            var chunkPos = pos.chunkPos;
            if (chunkPos.x == lastAccessedChunkPos.x && chunkPos.y == lastAccessedChunkPos.y)
                return lastAccessedChunk;
            if (!chunks.TryGetValue(chunkPos, out var chunk))
                chunks[chunkPos] = chunk = new Chunk();
            lastAccessedChunkPos = pos;
            lastAccessedChunk = chunk;
            return chunk;
        }

        public ref GridCellInfo this[GridPos pos] => ref GetChunk(pos)[pos.x, pos.y];
        public ref GridCellInfo this[int x, int y] => ref this[new GridPos(x, y)];

        public void AddObstacle(GridRect rect)
        {
            for (var x = rect.start.x; x < rect.end.x; x++)
                for (var y = rect.start.y; y < rect.end.y; y++)
                {
                    ref var ocount = ref this[x, y].ocount;
                    if (ocount < 255)
                        ocount++;
                }
        }
        
        public void RemoveObstacle(GridRect rect)
        {
            for (var x = rect.start.x; x <= rect.end.x; x++)
                for (var y = rect.start.y; y <= rect.end.y; y++)
                {
                    ref var ocount = ref this[x, y].ocount;
                    if (ocount > 0)
                        ocount--;
                }
        }

        public void ClearPathReservation(PathSegment[] path)
        {
            foreach (var segment in path)
            {
                if (segment.channel == Channels.None)
                    continue;
                var mask = ~segment.channel;
                var dx = segment.dir == Direction.Right ? 1 : segment.dir == Direction.Left ? -1 : 0;
                var dy = segment.dir == Direction.Down ? 1 : segment.dir == Direction.Up ? -1 : 0;
                var pos = segment.pos;
                for (var i = 0; i <= segment.length; i++)
                {
                    this[pos].channels &= mask;
                    pos.x += dx;
                    pos.y += dy;
                }
            }
        }
    }
}