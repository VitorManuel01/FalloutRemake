using System;

namespace FalloutRemake.Geometry;

public readonly record struct SightPoint(double X, double Y, double Z);

/// <summary>Finite segment/AABB intersection, independent of rendering and navigation.</summary>
public static class SightOcclusion
{
    public static bool Intersects(SightPoint from, SightPoint to, SightPoint center, SightPoint half)
    {
        static bool Finite(SightPoint p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);
        if (!Finite(from) || !Finite(to) || !Finite(center) || !Finite(half) || half.X < 0 || half.Y < 0 || half.Z < 0)
            throw new ArgumentOutOfRangeException(nameof(half));
        double enter = 0, leave = 1;
        bool Axis(double origin, double delta, double min, double max)
        {
            if (Math.Abs(delta) < 1e-12) return origin >= min && origin <= max;
            double a = (min - origin) / delta, b = (max - origin) / delta;
            if (a > b) (a, b) = (b, a);
            enter = Math.Max(enter, a); leave = Math.Min(leave, b);
            return enter <= leave;
        }
        return Axis(from.X, to.X - from.X, center.X - half.X, center.X + half.X)
            && Axis(from.Y, to.Y - from.Y, center.Y - half.Y, center.Y + half.Y)
            && Axis(from.Z, to.Z - from.Z, center.Z - half.Z, center.Z + half.Z);
    }
}
