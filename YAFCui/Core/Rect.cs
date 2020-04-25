using System;
using System.Numerics;

namespace YAFC.UI
{
    public struct Rect
    {
        public float X, Y;
        public float Width, Height;

        public float Right => X + Width;
        public float Bottom => Y + Height;
        public float Left => X;
        public float Top => Y;
        
        public static readonly Rect VeryBig = new Rect(-float.MaxValue/2, -float.MaxValue/2, float.MaxValue, float.MaxValue);

        public Rect(Vector2 position, Vector2 size) : this(position.X, position.Y, size.X, size.Y) {}
        
        public Rect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public static Rect SideRect(float left, float right, float top, float bottom)
        {
            return new Rect(left, top, right-left, bottom-top);
        }
        
        public static Rect SideRect(Vector2 topleft, Vector2 bottomRight)
        {
            return SideRect(topleft.X, bottomRight.X, topleft.Y, bottomRight.Y);
        }

        public static Rect Union(Rect a, Rect b)
        {
            return SideRect(MathF.Min(a.X, a.Y), MathF.Max(a.Right, b.Right), MathF.Min(a.Y, b.Y), MathF.Max(a.Bottom, b.Bottom));
        }
        
        public Vector2 Size => new Vector2(Width, Height);

        public Vector2 Position => new Vector2(X, Y);

        public Vector2 TopLeft => new Vector2(X, Y);
        public Vector2 TopRight => new Vector2(Right, Y);
        public Vector2 BottomRight => new Vector2(Right, Bottom);
        public Vector2 BottomLeft => new Vector2(X, Bottom);

        public bool Contains(Vector2 position)
        {
            return position.X >= X && position.Y >= Y && position.X <= Right && position.Y <= Bottom;
        }

        public bool IntersectsWith(Rect other)
        {
            return X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
        }

        public bool Contains(Rect rect)
        {
            return X <= rect.X && Y <= rect.Y && Right >= rect.Right && Bottom >= rect.Bottom;
        }

        public static Rect Intersect(Rect a, Rect b)
        {
            var left = MathF.Max(a.X, b.X);
            var right = MathF.Min(a.Right, b.Right);
            if (right <= left)
                return default;
            var top = MathF.Max(a.Y, b.Y);
            var bottom = MathF.Min(a.Bottom, b.Bottom);
            if (bottom <= top)
                return default;
            return SideRect(left, right, top, bottom);
        }

        public bool Equals(Rect other) => this == other;

        public override bool Equals(object obj)
        {
            return obj is Rect other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = X.GetHashCode();
                hashCode = (hashCode * 397) ^ Y.GetHashCode();
                hashCode = (hashCode * 397) ^ Width.GetHashCode();
                hashCode = (hashCode * 397) ^ Height.GetHashCode();
                return hashCode;
            }
        }

        public static Rect operator +(Rect source, Vector2 offset)
        {
            return new Rect(source.Position + offset, source.Size);
        }

        public static bool operator ==(Rect a, Rect b)
        {
            return a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
        }

        public static bool operator !=(Rect a, Rect b)
        {
            return !(a == b);
        }
    }
}