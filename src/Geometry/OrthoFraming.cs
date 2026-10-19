using System;
using System.Collections.Generic;

namespace FalloutRemake.Geometry;

public readonly record struct FramePoint(double X, double Y);
public readonly record struct FrameRect(double X, double Y, double Width, double Height);
public readonly record struct OrthoFrame(double Size, double TargetX, double TargetY);

/// <summary>Fit camera-plane points into a screen rectangle with no dependency on Godot or map IDs.</summary>
public static class OrthoFraming
{
    public static OrthoFrame Fit(IReadOnlyList<FramePoint> points, double viewportWidth, double viewportHeight,
        FrameRect safe, double widthPerSize, double heightPerSize, double padding = .6, double minimum = 8, double maximum = 220)
    {
        static bool Positive(double x) => double.IsFinite(x) && x > 0;
        if (points.Count is < 1 or > 100000 || !Positive(viewportWidth) || !Positive(viewportHeight)
            || !Positive(widthPerSize) || !Positive(heightPerSize) || !Positive(minimum) || !Positive(maximum) || maximum < minimum
            || !double.IsFinite(padding) || padding < 0 || !double.IsFinite(safe.X) || !double.IsFinite(safe.Y)
            || !Positive(safe.Width) || !Positive(safe.Height) || safe.X < 0 || safe.Y < 0
            || safe.X + safe.Width > viewportWidth || safe.Y + safe.Height > viewportHeight)
            throw new ArgumentException("Invalid orthographic framing inputs.");
        double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
        double minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
        foreach (FramePoint point in points)
        {
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) throw new ArgumentException("Nonfinite camera-plane point.");
            minX = Math.Min(minX, point.X); maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y); maxY = Math.Max(maxY, point.Y);
        }
        double size = Math.Max(minimum, 1.02 * Math.Max((maxX - minX + 2 * padding) * viewportWidth / (safe.Width * widthPerSize),
            (maxY - minY + 2 * padding) * viewportHeight / (safe.Height * heightPerSize)));
        if (!double.IsFinite(size) || size > maximum) throw new InvalidOperationException("Route exceeds the camera framing limit.");
        double targetX = minX / 2 + maxX / 2 - ((safe.X + safe.Width / 2) / viewportWidth - .5) * widthPerSize * size;
        double targetY = minY / 2 + maxY / 2 - (.5 - (safe.Y + safe.Height / 2) / viewportHeight) * heightPerSize * size;
        if (!double.IsFinite(targetX) || !double.IsFinite(targetY)) throw new InvalidOperationException("Framing target is nonfinite.");
        return new OrthoFrame(size, targetX, targetY);
    }
}
