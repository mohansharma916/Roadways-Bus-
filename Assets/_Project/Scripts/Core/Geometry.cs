using System;
using System.Numerics;

namespace RoadwaysGame.Core
{
    /// <summary>Oriented rectangle on the ground plane. X = world X, Y = world Z (screen-up).</summary>
    public readonly struct Obb
    {
        public readonly Vector2 Center;
        public readonly Vector2 Axis; // unit forward
        public readonly float HalfLength;
        public readonly float HalfWidth;

        public Obb(Vector2 center, Vector2 axis, float halfLength, float halfWidth)
        {
            Center = center;
            Axis = Vector2.Normalize(axis);
            HalfLength = halfLength;
            HalfWidth = halfWidth;
        }

        public Vector2 Side => new Vector2(-Axis.Y, Axis.X);

        /// <summary>Half the projected size of this box onto unit axis n.</summary>
        public float ExtentAlong(Vector2 n) =>
            HalfLength * MathF.Abs(Vector2.Dot(Axis, n)) + HalfWidth * MathF.Abs(Vector2.Dot(Side, n));

        /// <summary>Separating-axis test; touching counts as not overlapping.</summary>
        public bool Overlaps(in Obb o) =>
            !SeparatedOn(o, Axis) && !SeparatedOn(o, Side) && !SeparatedOn(o, o.Axis) && !SeparatedOn(o, o.Side);

        private bool SeparatedOn(in Obb o, Vector2 n) =>
            MathF.Abs(Vector2.Dot(o.Center - Center, n)) >= ExtentAlong(n) + o.ExtentAlong(n);

        /// <summary>Point test with an outward margin, so small vehicles still get a full-size touch target.</summary>
        public bool Contains(Vector2 p, float margin)
        {
            Vector2 d = p - Center;
            return MathF.Abs(Vector2.Dot(d, Axis)) <= HalfLength + margin
                && MathF.Abs(Vector2.Dot(d, Side)) <= HalfWidth + margin;
        }
    }
}
